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
        if (!SupportsSectionVersion(sectionVersion))
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}.");
        }

        TPayload payload = string.IsNullOrWhiteSpace(payloadJson)
            ? new TPayload()
            : JsonUtility.FromJson<TPayload>(payloadJson) ?? new TPayload();
        payload = MigratePayload(payload, sectionVersion, report) ?? new TPayload();
        RestorePayload(payload, report);
    }

    protected virtual bool SupportsSectionVersion(int sectionVersion)
    {
        return sectionVersion == SectionVersion;
    }

    protected virtual TPayload MigratePayload(
        TPayload payload,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        return payload;
    }

    protected abstract TPayload CapturePayload();
    protected abstract void RestorePayload(
        TPayload payload,
        DungeonGameRestoreReport report);
}
