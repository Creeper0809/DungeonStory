using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProductionBillsSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.production-bills";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };

    private readonly IProductionBillPersistence persistence;

    public ProductionBillsSaveSection(IProductionBillPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonProductionBillSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;

    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        persistence.BuildRestore(Parse(payloadJson, report));
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
        RequireVersion(sectionVersion);
        ProductionBillRestoreCandidate candidate =
            persistence.BuildRestore(Parse(payloadJson, report));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }

    private void RequireVersion(int sectionVersion)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}; expected {SectionVersion}.");
        }
    }

    private DungeonProductionBillSaveData Parse(
        string payloadJson,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"{SectionId} payload is empty.");
        }
        try
        {
            DungeonProductionBillSaveData payload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(payloadJson)
                ?? throw new InvalidOperationException(
                    $"{SectionId} payload deserialized to null.");
            V18WorkProductionCharacterReferenceRestoreNormalizer.Normalize(
                payload,
                (value, path) =>
                    V18TypedCharacterReferenceRestoreNormalizer
                        .RewriteLegacyReference(
                            value,
                            report,
                            SectionId,
                            path));
            return payload;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{SectionId} payload JSON is invalid: {exception.Message}",
                exception);
        }
    }
}
