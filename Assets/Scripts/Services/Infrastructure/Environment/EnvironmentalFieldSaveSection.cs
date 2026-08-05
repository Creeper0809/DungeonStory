using System;
using System.Collections.Generic;
using UnityEngine;
using EnvironmentalFieldRestoreCandidate =
    DungeonStory.Environment.EnvironmentalFieldRestoreCandidate;

public sealed class EnvironmentalFieldSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "environment.field";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id
    };

    private readonly IEnvironmentalFieldPersistence persistence;

    public EnvironmentalFieldSaveSection(
        IEnvironmentalFieldPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonEnvironmentalFieldSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;

    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        persistence.PrepareRestore(Parse(payloadJson));
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
        EnvironmentalFieldRestoreCandidate candidate =
            persistence.PrepareRestore(Parse(payloadJson));
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

    private DungeonEnvironmentalFieldSaveData Parse(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"{SectionId} payload is empty.");
        }
        try
        {
            return JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(
                    payloadJson)
                ?? throw new InvalidOperationException(
                    $"{SectionId} payload deserialized to null.");
        }
        catch (Exception exception)
            when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{SectionId} payload JSON is invalid: {exception.Message}",
                exception);
        }
    }
}
