using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DungeonSaveSlotCatalog : IDungeonSaveSlotCatalog
{
    private readonly string saveDirectory;

    [VContainer.Inject]
    public DungeonSaveSlotCatalog()
        : this(Path.Combine(Application.persistentDataPath, "Saves"))
    {
    }

    internal DungeonSaveSlotCatalog(string saveDirectory)
    {
        this.saveDirectory = string.IsNullOrWhiteSpace(saveDirectory)
            ? throw new ArgumentException("Save directory is required.", nameof(saveDirectory))
            : saveDirectory;
    }

    public bool HasSave(string slotId)
    {
        return File.Exists(GetPath(slotId));
    }

    public IReadOnlyList<DungeonSaveSlotInfo> GetSlots()
    {
        if (!Directory.Exists(saveDirectory))
        {
            return Array.Empty<DungeonSaveSlotInfo>();
        }

        List<DungeonSaveSlotInfo> slots = new List<DungeonSaveSlotInfo>();
        foreach (string path in Directory.GetFiles(saveDirectory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            string slotId = Path.GetFileNameWithoutExtension(path);

            try
            {
                DungeonSaveSlotHeaderData data = JsonUtility.FromJson<DungeonSaveSlotHeaderData>(File.ReadAllText(path));
                ModularFacilityWorldSaveData world = ReadSectionPayload<ModularFacilityWorldSaveData>(
                    data?.sections,
                    ModularFacilityWorldSaveSection.Id);
                DungeonDebugRunSaveData debug = ReadSectionPayload<DungeonDebugRunSaveData>(
                    data?.sections,
                    DungeonDebugSaveSection.Id);
                slots.Add(new DungeonSaveSlotInfo(
                    slotId,
                    path,
                    data?.savedAtUtc,
                    data?.sceneName,
                    world?.gameData?.day ?? 1,
                    world?.gameData?.holdingMoney ?? 0,
                    data != null && data.version == DungeonGameSaveData.CurrentVersion,
                    debug != null && debug.debugModified));
            }
            catch
            {
                slots.Add(new DungeonSaveSlotInfo(slotId, path));
            }
        }

        return slots;
    }

    public bool Delete(string slotId)
    {
        string path = GetPath(slotId);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    public string GetPath(string slotId)
    {
        return Path.Combine(saveDirectory, NormalizeSlotId(slotId) + ".json");
    }

    private static string NormalizeSlotId(string slotId)
    {
        string normalized = (slotId ?? string.Empty).Trim();
        if (normalized.Length == 0
            || normalized.Any(character => !char.IsLetterOrDigit(character) && character != '-' && character != '_'))
        {
            throw new ArgumentException("Save slot ids may only contain letters, numbers, '-' and '_'.", nameof(slotId));
        }

        return normalized;
    }

    private static TPayload ReadSectionPayload<TPayload>(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        string sectionId)
        where TPayload : class
    {
        DungeonSaveSectionEnvelope envelope = envelopes?.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sectionId?.Trim(),
                sectionId,
                StringComparison.Ordinal));
        return envelope == null || string.IsNullOrWhiteSpace(envelope.payloadJson)
            ? null
            : JsonUtility.FromJson<TPayload>(envelope.payloadJson);
    }

    [Serializable]
    private sealed class DungeonSaveSlotHeaderData
    {
        public int version;
        public string savedAtUtc;
        public string sceneName;
        public List<DungeonSaveSectionEnvelope> sections;
    }
}

public sealed class DungeonGameSaveService : IDungeonGameSaveService
{
    private readonly IDungeonSaveSectionRegistry saveSectionRegistry;

    public DungeonGameSaveService(IDungeonSaveSectionRegistry saveSectionRegistry)
    {
        this.saveSectionRegistry = saveSectionRegistry
            ?? throw new ArgumentNullException(nameof(saveSectionRegistry));
    }

    public DungeonGameSaveData Capture()
    {
        DungeonGameSaveData save = new DungeonGameSaveData
        {
            version = DungeonGameSaveData.CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            sceneName = SceneManager.GetActiveScene().name,
            sections = saveSectionRegistry.CaptureAll()
        };

        return save;
    }

    public string ToJson(DungeonGameSaveData saveData, bool prettyPrint = false)
    {
        return JsonUtility.ToJson(saveData ?? new DungeonGameSaveData(), prettyPrint);
    }

    public DungeonGameSaveData FromJson(string json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? new DungeonGameSaveData()
            : JsonUtility.FromJson<DungeonGameSaveData>(json) ?? new DungeonGameSaveData();
    }

    public bool TryRestore(DungeonGameSaveData saveData, out DungeonGameRestoreReport report)
    {
        report = new DungeonGameRestoreReport();
        if (saveData == null)
        {
            report.AddError("Save data is null.");
            return false;
        }

        if (saveData.version != DungeonGameSaveData.CurrentVersion)
        {
            report.AddError(
                $"저장 버전 {saveData.version}은 현재 저장 시스템과 호환되지 않습니다. 새 게임을 시작해 주세요.");
            return false;
        }

        try
        {
            if (!saveSectionRegistry.RestoreAll(saveData.sections, report))
            {
                return false;
            }
        }
        catch (Exception exception)
        {
            report.AddError(exception.Message);
        }

        return report.Success;
    }
}

public sealed class DungeonGameSaveSlotService : IDungeonGameSaveSlotService
{
    public const string AutoSaveSlot = "autosave";
    public const string QuickSaveSlot = "quicksave";
    public const string ManualSaveSlot = "manual";

    private readonly IDungeonGameSaveService saveService;
    private readonly IDungeonSaveSlotCatalog slotCatalog;

    [VContainer.Inject]
    public DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog)
    {
        this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        this.slotCatalog = slotCatalog ?? throw new ArgumentNullException(nameof(slotCatalog));
    }

    internal DungeonGameSaveSlotService(IDungeonGameSaveService saveService, string saveDirectory)
        : this(saveService, new DungeonSaveSlotCatalog(saveDirectory))
    {
    }

    public string Save(string slotId, bool prettyPrint = false)
    {
        string path = slotCatalog.GetPath(slotId);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.persistentDataPath);
        string temporaryPath = path + ".tmp";
        string backupPath = path + ".bak";
        File.WriteAllText(temporaryPath, saveService.ToJson(saveService.Capture(), prettyPrint));

        if (File.Exists(path))
        {
            File.Replace(temporaryPath, path, backupPath, true);
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        else
        {
            File.Move(temporaryPath, path);
        }

        return path;
    }

    public bool TryLoad(string slotId, out DungeonGameRestoreReport report)
    {
        string path = slotCatalog.GetPath(slotId);
        if (!File.Exists(path))
        {
            report = new DungeonGameRestoreReport();
            report.AddError($"Save slot '{slotId}' does not exist.");
            return false;
        }

        try
        {
            return saveService.TryRestore(saveService.FromJson(File.ReadAllText(path)), out report);
        }
        catch (Exception exception)
        {
            report = new DungeonGameRestoreReport();
            string preservedPath = PreserveUnreadableSave(path);
            report.AddError(string.IsNullOrWhiteSpace(preservedPath)
                ? exception.Message
                : $"{exception.Message} 손상된 저장 사본: {preservedPath}");
            return false;
        }
    }

    public bool HasSave(string slotId)
    {
        return slotCatalog.HasSave(slotId);
    }

    public IReadOnlyList<DungeonSaveSlotInfo> GetSlots()
    {
        return slotCatalog.GetSlots();
    }

    public bool Delete(string slotId)
    {
        return slotCatalog.Delete(slotId);
    }

    private string PreserveUnreadableSave(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            string corruptDirectory = Path.Combine(
                Path.GetDirectoryName(path) ?? Application.persistentDataPath,
                "Corrupt");
            Directory.CreateDirectory(corruptDirectory);
            string name = Path.GetFileNameWithoutExtension(path);
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            string preservedPath = Path.Combine(corruptDirectory, $"{name}-{timestamp}.json");
            File.Copy(path, preservedPath, overwrite: false);
            return preservedPath;
        }
        catch
        {
            return string.Empty;
        }
    }

}
