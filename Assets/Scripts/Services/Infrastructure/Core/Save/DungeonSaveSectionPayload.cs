using System;
using System.Linq;
using UnityEngine;

public static class DungeonSaveSectionPayload
{
    // JsonUtility's native double reader can change an R-format receipt value
    // by one ULP, including copies inside skill/evolution evidence. Use the same
    // exact reader for section staging and cross-owner preflight. Save shapes,
    // JsonUtility writers and integrity checks are deliberately unchanged.
    public static TPayload Deserialize<TPayload>(string json) where TPayload : class =>
        DungeonSaveJson.Deserialize<TPayload>(json);

    public static bool TryRead<TPayload>(
        DungeonGameSaveData saveData,
        string sectionId,
        out TPayload payload)
        where TPayload : class, new()
    {
        payload = null;
        DungeonSaveSectionEnvelope envelope = saveData?.sections?
            .FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.sectionId?.Trim(),
                    sectionId?.Trim(),
                    StringComparison.Ordinal));
        if (envelope == null || string.IsNullOrWhiteSpace(envelope.payloadJson))
        {
            return false;
        }

        payload = Deserialize<TPayload>(envelope.payloadJson);
        return payload != null;
    }

    public static TPayload ReadOrNew<TPayload>(
        DungeonGameSaveData saveData,
        string sectionId)
        where TPayload : class, new()
    {
        return TryRead(saveData, sectionId, out TPayload payload)
            ? payload
            : new TPayload();
    }

    public static void Write<TPayload>(
        DungeonGameSaveData saveData,
        string sectionId,
        int sectionVersion,
        DungeonSaveRestorePhase restorePhase,
        TPayload payload)
        where TPayload : class, new()
    {
        if (saveData == null)
        {
            throw new ArgumentNullException(nameof(saveData));
        }

        string normalizedId = sectionId?.Trim() ?? string.Empty;
        if (normalizedId.Length == 0)
        {
            throw new ArgumentException(
                "Save section id is required.",
                nameof(sectionId));
        }

        DungeonSaveSectionEnvelope envelope = saveData.sections
            .FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.sectionId?.Trim(),
                    normalizedId,
                    StringComparison.Ordinal));
        if (envelope == null)
        {
            envelope = new DungeonSaveSectionEnvelope();
            saveData.sections.Add(envelope);
        }

        envelope.sectionId = normalizedId;
        envelope.sectionVersion = Math.Max(1, sectionVersion);
        envelope.restorePhase = restorePhase;
        envelope.payloadJson = JsonUtility.ToJson(payload ?? new TPayload());
    }
}
