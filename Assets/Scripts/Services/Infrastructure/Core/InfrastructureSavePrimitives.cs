using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IDungeonGameSaveService
{
    DungeonGameSaveData Capture();
    string ToJson(DungeonGameSaveData saveData, bool prettyPrint = false);
    DungeonGameSaveData FromJson(string json);
    bool TryRestore(DungeonGameSaveData saveData, out DungeonGameRestoreReport report);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IDungeonGameSaveSlotService
{
    string Save(string slotId, bool prettyPrint = false);
    bool TryLoad(string slotId, out DungeonGameRestoreReport report);
    bool HasSave(string slotId);
    IReadOnlyList<DungeonSaveSlotInfo> GetSlots();
    bool Delete(string slotId);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IDungeonSaveSlotCatalog
{
    bool HasSave(string slotId);
    IReadOnlyList<DungeonSaveSlotInfo> GetSlots();
    bool Delete(string slotId);
    string GetPath(string slotId);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonGameSaveData
{
    public const int CurrentVersion = 23;

    public int version = CurrentVersion;
    public string savedAtUtc = string.Empty;
    public string sceneName = string.Empty;
    public DungeonSaveManifestData manifest;
    public List<DungeonSaveSectionEnvelope> sections = new List<DungeonSaveSectionEnvelope>();
}

[Serializable]
public sealed class DungeonSaveManifestData
{
    public int compatibilityGeneration;
    public List<DungeonSaveManifestSectionData> sections =
        new List<DungeonSaveManifestSectionData>();
}

[Serializable]
public sealed class DungeonSaveManifestSectionData
{
    public string sectionId = string.Empty;
    public int sectionVersion;
    public bool optional;
}

public static class DungeonSaveManifest
{
    public static DungeonSaveManifestData Capture(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes)
    {
        return new DungeonSaveManifestData
        {
            compatibilityGeneration = DungeonGameSaveData.CurrentVersion,
            sections = (envelopes ?? Array.Empty<DungeonSaveSectionEnvelope>())
                .Where(envelope => envelope != null)
                .OrderBy(envelope => envelope.sectionId, StringComparer.Ordinal)
                .Select(envelope => new DungeonSaveManifestSectionData
                {
                    sectionId = envelope.sectionId?.Trim() ?? string.Empty,
                    sectionVersion = envelope.sectionVersion,
                    optional = envelope.optional
                })
                .ToList()
        };
    }

    public static bool TryValidate(
        DungeonSaveManifestData manifest,
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        out string reason)
    {
        if (manifest == null
            || manifest.compatibilityGeneration != DungeonGameSaveData.CurrentVersion)
        {
            reason = "V23 save manifest is missing or has an incompatible generation.";
            return false;
        }

        Dictionary<string, DungeonSaveManifestSectionData> declared =
            new Dictionary<string, DungeonSaveManifestSectionData>(StringComparer.Ordinal);
        foreach (DungeonSaveManifestSectionData section in manifest.sections
                     ?? new List<DungeonSaveManifestSectionData>())
        {
            string id = section?.sectionId?.Trim() ?? string.Empty;
            if (id.Length == 0 || !declared.TryAdd(id, section))
            {
                reason = "V23 save manifest contains an empty or duplicate section id.";
                return false;
            }
        }

        Dictionary<string, DungeonSaveSectionEnvelope> payloads =
            new Dictionary<string, DungeonSaveSectionEnvelope>(StringComparer.Ordinal);
        foreach (DungeonSaveSectionEnvelope envelope in envelopes
                     ?? Array.Empty<DungeonSaveSectionEnvelope>())
        {
            string id = envelope?.sectionId?.Trim() ?? string.Empty;
            if (id.Length == 0 || !payloads.TryAdd(id, envelope))
            {
                reason = "V23 save payload contains an empty or duplicate section id.";
                return false;
            }
        }

        foreach (KeyValuePair<string, DungeonSaveManifestSectionData> pair in declared)
        {
            if (!payloads.TryGetValue(pair.Key, out DungeonSaveSectionEnvelope payload))
            {
                if (!pair.Value.optional)
                {
                    reason = $"V23 save is missing manifest-required section '{pair.Key}'.";
                    return false;
                }

                continue;
            }

            if (payload.sectionVersion != pair.Value.sectionVersion
                || payload.optional != pair.Value.optional)
            {
                reason = $"V23 save section '{pair.Key}' does not match its manifest.";
                return false;
            }
        }

        string undeclared = payloads.Keys.FirstOrDefault(id => !declared.ContainsKey(id));
        if (!string.IsNullOrEmpty(undeclared))
        {
            reason = $"V23 save contains undeclared section '{undeclared}'.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}

public static class DungeonSaveCompatibility
{
    public const string PreV23IncompatibilityReason =
        "V23 무등급 재료·작업자 지정·품질 반복 생산 개편 이전 저장 — 새 게임 필요";

    public const string PreV22IncompatibilityReason =
        "V22 해부학·재료 계보 의복 개편 이전 저장 — 새 게임 필요";

    public const string PreV21IncompatibilityReason = PreV22IncompatibilityReason;

    public static bool TryGetIncompatibilityReason(
        int version,
        out string incompatibilityReason)
    {
        if (version == DungeonGameSaveData.CurrentVersion)
        {
            incompatibilityReason = string.Empty;
            return false;
        }

        incompatibilityReason = version < DungeonGameSaveData.CurrentVersion
            ? PreV23IncompatibilityReason
            : $"현재 빌드보다 새로운 저장 버전입니다. 저장 V{version}, 지원 V{DungeonGameSaveData.CurrentVersion}";
        return true;
    }
}
