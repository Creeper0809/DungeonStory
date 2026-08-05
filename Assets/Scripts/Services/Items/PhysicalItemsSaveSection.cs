using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPhysicalItemRestoreStaging
{
    IDungeonSaveRestoreStage StageRestore(DungeonPhysicalItemSaveData snapshot);
}

public sealed class PhysicalItemsSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "items.physical";

    private readonly IWorldItemStackRuntime runtime;
    private readonly IPhysicalItemRestoreStaging restoreStaging;

    public PhysicalItemsSaveSection(
        IWorldItemStackRuntime runtime,
        IPhysicalItemRestoreStaging restoreStaging)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.restoreStaging = restoreStaging
            ?? throw new ArgumentNullException(nameof(restoreStaging));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonPhysicalItemSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Items;
    public IReadOnlyList<string> DependsOn => new[]
    {
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id
    };

    public string Capture()
    {
        return JsonUtility.ToJson(runtime.Capture());
    }

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                $"Unsupported physical item section version {sectionVersion}; expected {SectionVersion}.");
            return;
        }

        DungeonPhysicalItemSaveData payload = Deserialize(payloadJson, report);
        if (payload != null)
        {
            PhysicalItemSaveValidation.Validate(
                payload,
                report,
                runtime.CatalogProvider);
        }
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IDungeonSaveRestoreStage stage = StageRestore(
            payloadJson,
            sectionVersion,
            report);
        if (report.Success)
        {
            stage.Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (!report.Success)
        {
            return new DungeonDelegateSaveRestoreStage(Id, _ => { });
        }

        DungeonPhysicalItemSaveData payload = JsonUtility.FromJson<
            DungeonPhysicalItemSaveData>(payloadJson);
        return restoreStaging.StageRestore(payload);
    }

    private static DungeonPhysicalItemSaveData Deserialize(
        string payloadJson,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            report.AddError("Physical item payload JSON is empty.");
            return null;
        }

        try
        {
            DungeonPhysicalItemSaveData payload =
                JsonUtility.FromJson<DungeonPhysicalItemSaveData>(payloadJson);
            if (payload == null)
            {
                report.AddError("Physical item payload deserialized to null.");
            }
            return payload;
        }
        catch (Exception ex)
        {
            report.AddError(
                $"Physical item payload JSON is invalid: {ex.Message}");
            return null;
        }
    }
}
