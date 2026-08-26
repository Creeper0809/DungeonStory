using System;
using System.Collections.Generic;
using UnityEngine;

public interface IPhysicalItemRestoreStaging
{
    IDungeonSaveRestoreStage StageRestore(DungeonPhysicalItemSaveData snapshot);
    IDungeonSaveRestoreStage StageTransactionalRestore(
        DungeonPhysicalItemSaveData snapshot,
        IRestoreWorldCandidateQuery restoreWorldCandidates);
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
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;

    public PhysicalItemsSaveSection(
        IWorldItemStackRuntime runtime,
        IPhysicalItemRestoreStaging restoreStaging,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.restoreStaging = restoreStaging
            ?? throw new ArgumentNullException(nameof(restoreStaging));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
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
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                $"Unsupported physical item section version {sectionVersion}; expected {SectionVersion}.");
            return new DungeonDelegateSaveRestoreStage(Id, _ => { });
        }

        DungeonPhysicalItemSaveData payload = Deserialize(payloadJson, report);
        if (payload != null)
        {
            PhysicalItemSaveValidation.Validate(
                payload,
                report,
                runtime.CatalogProvider);
        }
        if (!report.Success)
        {
            return new DungeonDelegateSaveRestoreStage(Id, _ => { });
        }

        IDungeonSaveRestoreStage inner =
            restoreStaging.StageTransactionalRestore(
            payload,
            restoreWorldCandidates);
        return new DungeonBeforeCommitSaveRestoreStage(
            inner,
            () => lifecycleRestoreCandidates.SetPhysicalItems(payload));
    }

    private DungeonPhysicalItemSaveData Deserialize(
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
            ValidateRequiredCurrentSchemaShape(payloadJson);
        }
        catch (Exception exception)
        {
            report.AddError(exception.Message);
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
            else
            {
                V18WorldEconomyCharacterReferenceRestoreNormalizer.Normalize(
                    payload,
                    (value, path) =>
                        V18TypedCharacterReferenceRestoreNormalizer
                            .RewriteLegacyReference(
                                value,
                                report,
                                SectionId,
                                path));
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

    internal static void ValidateRequiredCheckpointScalarFields(
        string payloadJson)
    {
        if (!HasTopLevelProperty(payloadJson, "version")
            || !HasTopLevelProperty(
                payloadJson,
                "lastConfirmedExactRouteCheckpointSequence")
            || !HasTopLevelProperty(
                payloadJson,
                "lastConfirmedExactRouteCheckpointDigest"))
        {
            throw new InvalidOperationException(
                "Physical item current schema is missing a required exact-route checkpoint scalar field.");
        }
    }

    public static void ValidateRequiredCurrentSchemaShape(string payloadJson)
    {
        DungeonStrictJsonShape.RequireTopLevelArrays(
            Id,
            payloadJson,
            new[]
            {
                "pendingExactOutputRoutes",
                "pendingProductionCustodyDrains",
                "pendingProductionInputDestinationDrains",
                "pendingCapacityRoutingDrains"
            });
        ValidateRequiredCheckpointScalarFields(payloadJson);
    }

    private static bool HasTopLevelProperty(string json, string propertyName)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName))
            return false;
        int depth = 0;
        for (int index = 0; index < json.Length; index++)
        {
            char character = json[index];
            if (character == '{' || character == '[')
            {
                depth++;
                continue;
            }
            if (character == '}' || character == ']')
            {
                depth--;
                continue;
            }
            if (character != '"')
                continue;

            int start = ++index;
            bool escaped = false;
            while (index < json.Length)
            {
                char current = json[index];
                if (!escaped && current == '"')
                    break;
                escaped = !escaped && current == '\\';
                if (current != '\\')
                    escaped = false;
                index++;
            }
            if (depth != 1
                || index >= json.Length
                || index - start != propertyName.Length
                || string.CompareOrdinal(
                    json,
                    start,
                    propertyName,
                    0,
                    propertyName.Length) != 0)
            {
                continue;
            }
            int separator = index + 1;
            while (separator < json.Length
                   && char.IsWhiteSpace(json[separator]))
            {
                separator++;
            }
            if (separator < json.Length && json[separator] == ':')
                return true;
        }
        return false;
    }
}
