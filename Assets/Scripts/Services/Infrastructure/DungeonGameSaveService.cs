using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
                GameSessionSaveData session = ReadSectionPayload<GameSessionSaveData>(
                    data?.sections,
                    FoundationSessionSaveSection.Id);
                DungeonDebugRunSaveData debug = ReadSectionPayload<DungeonDebugRunSaveData>(
                    data?.sections,
                    DungeonDebugSaveSection.Id);
                DungeonRunVariableSaveData runVariables =
                    ReadSectionPayload<DungeonRunVariableSaveData>(
                        data?.sections,
                        RunVariableSaveSection.Id);
                string incompatibilityReason = GetIncompatibilityReason(data);
                slots.Add(new DungeonSaveSlotInfo(
                    new DungeonSaveSlotLocation(slotId, path),
                    new DungeonSaveSlotSummary(
                        data?.savedAtUtc,
                        data?.sceneName,
                        session?.absoluteDay ?? 1,
                        session?.money ?? 0,
                        debug != null && debug.debugModified,
                        (int)(runVariables?.startVariables?.survivalPressure
                            ?? DungeonSurvivalPressure.Standard)),
                    new DungeonSaveSlotCompatibility(
                        data != null
                            && data.version == DungeonGameSaveData.CurrentVersion
                            && string.IsNullOrWhiteSpace(incompatibilityReason),
                        incompatibilityReason)));
            }
            catch
            {
                slots.Add(new DungeonSaveSlotInfo(slotId, path));
            }
        }

        return slots;
    }

    private static string GetIncompatibilityReason(DungeonSaveSlotHeaderData data)
    {
        if (data == null)
        {
            return "저장 파일을 읽을 수 없습니다.";
        }

        if (DungeonSaveCompatibility.TryGetIncompatibilityReason(
                data.version,
                out string incompatibilityReason))
        {
            return incompatibilityReason;
        }

        return DungeonSaveManifest.TryValidate(
            data.manifest,
            data.sections,
            out string manifestError)
                ? string.Empty
                : manifestError;
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
        public DungeonSaveManifestData manifest;
        public List<DungeonSaveSectionEnvelope> sections;
    }
}

public sealed class DungeonGameSaveService : IDungeonGameSaveService
{
    private readonly IDungeonSaveSectionRegistry saveSectionRegistry;
    private readonly IReadOnlyList<IDungeonSavePreflightValidator> preflightValidators;
    private readonly IReadOnlyList<IDungeonSaveCaptureGuard> captureGuards;
    private readonly IReadOnlyList<IDungeonCapturedSavePreflightValidator>
        capturedSaveValidators;
    private readonly IReadOnlyList<IDungeonSaveRestoreCompletedHook> restoreCompletedHooks;

    public DungeonGameSaveService(
        IDungeonSaveSectionRegistry saveSectionRegistry,
        IEnumerable<IDungeonSavePreflightValidator> preflightValidators,
        IEnumerable<IDungeonSaveCaptureGuard> captureGuards,
        IEnumerable<IDungeonCapturedSavePreflightValidator>
            capturedSaveValidators,
        IEnumerable<IDungeonSaveRestoreCompletedHook> restoreCompletedHooks)
    {
        this.saveSectionRegistry = saveSectionRegistry
            ?? throw new ArgumentNullException(nameof(saveSectionRegistry));
        this.preflightValidators = (preflightValidators
                ?? throw new ArgumentNullException(nameof(preflightValidators)))
            .Where(validator => validator != null)
            .ToArray();
        this.captureGuards = (captureGuards
                ?? throw new ArgumentNullException(nameof(captureGuards)))
            .Where(guard => guard != null)
            .ToArray();
        this.capturedSaveValidators = (capturedSaveValidators
                ?? throw new ArgumentNullException(
                    nameof(capturedSaveValidators)))
            .Where(validator => validator != null)
            .OrderBy(validator => validator.GetType().FullName,
                StringComparer.Ordinal)
            .ToArray();
        this.restoreCompletedHooks = (restoreCompletedHooks
                ?? throw new ArgumentNullException(nameof(restoreCompletedHooks)))
            .Where(hook => hook != null)
            .ToArray();
    }

    public DungeonGameSaveData Capture()
    {
        for (int index = 0; index < captureGuards.Count; index++)
        {
            captureGuards[index].ValidateBeforeCapture();
        }

        List<DungeonSaveSectionEnvelope> sections = saveSectionRegistry.CaptureAll();
        DungeonGameSaveData save = new DungeonGameSaveData
        {
            version = DungeonGameSaveData.CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("O"),
            sceneName = SceneManager.GetActiveScene().name,
            manifest = DungeonSaveManifest.Capture(sections),
            sections = sections
        };

        DungeonGameRestoreReport captureReport = new();
        foreach (IDungeonCapturedSavePreflightValidator validator in
                 capturedSaveValidators)
        {
            try
            {
                validator.Validate(save, captureReport);
            }
            catch (Exception exception)
            {
                captureReport.AddError(
                    $"Captured-save aggregate preflight '{validator.GetType().Name}' failed: {exception.Message}");
            }
        }
        if (!captureReport.Success)
        {
            throw new InvalidOperationException(
                "Captured save failed aggregate preflight: "
                + string.Join(" | ", captureReport.Errors));
        }

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
            DungeonSaveCompatibility.TryGetIncompatibilityReason(
                saveData.version,
                out string incompatibilityReason);
            report.AddError(incompatibilityReason);
            return false;
        }

        if (!DungeonSaveManifest.TryValidate(
                saveData.manifest,
                saveData.sections,
                out string manifestError))
        {
            report.AddError(manifestError);
            return false;
        }

        foreach (IDungeonSavePreflightValidator validator in preflightValidators)
        {
            try
            {
                validator.Validate(saveData, report);
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Save aggregate preflight '{validator.GetType().Name}' failed: {exception.Message}");
            }
        }

        if (!report.Success)
        {
            return false;
        }

        try
        {
            if (!saveSectionRegistry.RestoreAll(saveData.sections, report))
            {
                return false;
            }
            for (int index = 0; index < restoreCompletedHooks.Count; index++)
            {
                restoreCompletedHooks[index].OnRestoreCompleted();
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
    private readonly IDungeonDurableSaveCommitCoordinator durableSaveCommits;
    private readonly IDungeonAtomicSaveFilePort filePort;

    public DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog,
        IPreparedOutputCheckpointGcCoordinator checkpointGc)
        : this(
            saveService,
            slotCatalog,
            CreateLegacyDurableSaveCoordinator(checkpointGc),
            new DungeonAtomicSaveFilePort())
    {
    }

    [VContainer.Inject]
    public DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog,
        IDungeonDurableSaveCommitCoordinator durableSaveCommits)
        : this(
            saveService,
            slotCatalog,
            durableSaveCommits,
            new DungeonAtomicSaveFilePort())
    {
    }

    internal DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog,
        IPreparedOutputCheckpointGcCoordinator checkpointGc,
        IDungeonAtomicSaveFilePort filePort)
        : this(
            saveService,
            slotCatalog,
            CreateLegacyDurableSaveCoordinator(checkpointGc),
            filePort)
    {
    }

    internal DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        IDungeonSaveSlotCatalog slotCatalog,
        IDungeonDurableSaveCommitCoordinator durableSaveCommits,
        IDungeonAtomicSaveFilePort filePort)
    {
        this.saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        this.slotCatalog = slotCatalog ?? throw new ArgumentNullException(nameof(slotCatalog));
        this.durableSaveCommits = durableSaveCommits
            ?? throw new ArgumentNullException(nameof(durableSaveCommits));
        this.filePort = filePort ?? throw new ArgumentNullException(nameof(filePort));
    }

    internal DungeonGameSaveSlotService(
        IDungeonGameSaveService saveService,
        string saveDirectory,
        IPreparedOutputCheckpointGcCoordinator checkpointGc,
        IDungeonAtomicSaveFilePort filePort)
        : this(saveService, new DungeonSaveSlotCatalog(saveDirectory),
            checkpointGc, filePort)
    {
    }

    public string Save(string slotId, bool prettyPrint = false)
    {
        string path = slotCatalog.GetPath(slotId);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? Application.persistentDataPath);
        string temporaryPath = path + ".tmp";
        string backupPath = path + ".bak";
        string serialized = saveService.ToJson(saveService.Capture(), prettyPrint);
        byte[] serializedBytes = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetBytes(serialized);
        string serializedByteDigest = ComputeSha256(serializedBytes);
        try
        {
            filePort.WriteTemporary(temporaryPath, serializedBytes);
            filePort.CommitTemporary(temporaryPath, path, backupPath);
        }
        catch
        {
            filePort.TryDelete(temporaryPath);
            throw;
        }

        DungeonDurableSaveCommitResult commitResult = durableSaveCommits
            .OnDurableSaveCommitted(slotId, serializedByteDigest);
        if (commitResult.Status == DungeonDurableSaveCommitStatus.Corruption)
        {
            throw new InvalidOperationException(
                "Save bytes are durable, but durable-save commit processing failed at '"
                + commitResult.ParticipantId + "': " + commitResult.Message);
        }
        filePort.TryDelete(backupPath);

        return path;
    }

    private static IDungeonDurableSaveCommitCoordinator
        CreateLegacyDurableSaveCoordinator(
            IPreparedOutputCheckpointGcCoordinator checkpointGc) =>
        new DungeonDurableSaveCommitCoordinator(
            new IDungeonDurableSaveCommitParticipant[]
            {
                new PreparedOutputCheckpointGcDurableSaveParticipant(
                    checkpointGc ?? throw new ArgumentNullException(
                        nameof(checkpointGc)))
            });

    private static string ComputeSha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(bytes ?? Array.Empty<byte>());
        StringBuilder builder = new(digest.Length * 2);
        for (int index = 0; index < digest.Length; index++)
            builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
        return builder.ToString();
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

internal interface IDungeonAtomicSaveFilePort
{
    void WriteTemporary(string temporaryPath, byte[] serializedBytes);
    void CommitTemporary(
        string temporaryPath,
        string destinationPath,
        string backupPath);
    void TryDelete(string path);
}

internal sealed class DungeonAtomicSaveFilePort : IDungeonAtomicSaveFilePort
{
    public void WriteTemporary(string temporaryPath, byte[] serializedBytes)
    {
        string path = temporaryPath
            ?? throw new ArgumentNullException(nameof(temporaryPath));
        byte[] bytes = serializedBytes
            ?? throw new ArgumentNullException(nameof(serializedBytes));
        using FileStream stream = new(
            path,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(flushToDisk: true);
    }

    public void CommitTemporary(
        string temporaryPath,
        string destinationPath,
        string backupPath)
    {
        if (File.Exists(destinationPath))
            File.Replace(temporaryPath, destinationPath, backupPath, true);
        else
            File.Move(temporaryPath, destinationPath);
    }

    public void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Backup/temp cleanup is non-authoritative after atomic replacement.
        }
    }
}
