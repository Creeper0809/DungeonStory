using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class DungeonJsonSaveSection<TPayload> : IDungeonSaveSection
    where TPayload : class, new()
{
    public abstract string SectionId { get; }
    public virtual int SectionVersion => 1;
    public abstract DungeonSaveRestorePhase RestorePhase { get; }
    public virtual IReadOnlyList<string> DependsOn => Array.Empty<string>();

    public string Capture()
    {
        return JsonUtility.ToJson(CapturePayload() ?? new TPayload());
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}.");
        }

        TPayload payload = string.IsNullOrWhiteSpace(payloadJson)
            ? new TPayload()
            : JsonUtility.FromJson<TPayload>(payloadJson) ?? new TPayload();
        RestorePayload(payload, report);
    }

    protected abstract TPayload CapturePayload();
    protected abstract void RestorePayload(
        TPayload payload,
        DungeonGameRestoreReport report);
}
