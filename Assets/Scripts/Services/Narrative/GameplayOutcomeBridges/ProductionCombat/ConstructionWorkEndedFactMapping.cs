using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public static class ConstructionWorkEndedFactMapping
{
    public static MigratedDomainFactCapture Capture(
        ConstructionWorkEndedFact fact)
    {
        ConstructionWorkEndedData data = (fact
            ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        data.Validate();

        string subjectId = data.terminalKind == WorkOrderTerminalKind.Completed
            ? data.finalFacilityId
            : data.constructionSitePersistentId;
        var facility = new GameplayEntityId(
            MigratedProducerOutcomeIds.FacilityKind,
            subjectId);
        string terminal = data.terminalKind == WorkOrderTerminalKind.Completed
            ? "completed"
            : "cancelled";
        var details = new MigratedProducerOutcomeDetails(
            new[]
            {
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.FacilityKind,
                    subjectId,
                    data.buildingDisplayName,
                    MigratedProducerOutcomeIds.FacilityRole)
            },
            new[]
            {
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.RequiredWorkMetric,
                    Math.Round(data.requiredWork * 1000d),
                    MigratedProducerOutcomeIds.MilliWorkUnit,
                    facility),
                new GameplayOutcomeMetric(
                    MigratedProducerOutcomeIds.CompletedWorkMetric,
                    Math.Round(data.completedWork * 1000d),
                    MigratedProducerOutcomeIds.MilliWorkUnit,
                    facility)
            },
            new[]
            {
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.WorkOrderFact,
                    data.workOrderId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.TerminalKindFact,
                    terminal),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ConstructionSiteFact,
                    data.constructionSitePersistentId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.BuildingDefinitionFact,
                    data.buildingDefinitionId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.FinalFacilityFact,
                    ValueOrNone(data.finalFacilityId)),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ReplacedFacilityFact,
                    ValueOrNone(data.replacedFacilityId)),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.SupportFacilitiesFact,
                    JoinOrNone(data.supportBuildingIds)),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.ContributorsFact,
                    EncodeContributors(data.contributions)),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.RequiredMaterialsFact,
                    EncodeMaterials(data.itemMaterials)),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.MaterialInputOperationFact,
                    ValueOrNone(data.materialInputOperationId)),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.RestitutionOperationFact,
                    ValueOrNone(data.restitutionOperationId)),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.PlacementOperationFact,
                    data.placementOperationId),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.QualityFact,
                    data.terminalKind == WorkOrderTerminalKind.Completed
                        ? data.qualityTier.ToString().ToLowerInvariant()
                        : "not-resolved"),
                new GameplayOutcomeFact(
                    MigratedProducerOutcomeIds.DetailFact,
                    data.gridX.ToString(CultureInfo.InvariantCulture)
                    + ","
                    + data.gridY.ToString(CultureInfo.InvariantCulture))
            },
            data.terminalKind == WorkOrderTerminalKind.Completed
                ? $"{data.buildingDisplayName} 공사가 완료되었다."
                : $"{data.buildingDisplayName} 공사가 취소되었다.",
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "construction-context-sha256",
                    ComputeContextSha256(data))
            });

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.WorkOrderTerminalResult,
            "work-order-terminal:" + data.workOrderId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static string ComputeContextSha256(ConstructionWorkEndedData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate();
        ConstructionWorkEndedData data = source.Clone();

        var canonical = new StringBuilder();
        Append(canonical, "construction-work-ended-context-v1");
        Append(canonical, data.workOrderId);
        Append(canonical, (int)data.terminalKind);
        Append(canonical, data.placementOperationId);
        Append(canonical, data.constructionSitePersistentId);
        Append(canonical, data.targetBuildingId);
        Append(canonical, data.buildingDefinitionId);
        Append(canonical, data.buildingDisplayName);
        Append(canonical, data.finalFacilityId);
        Append(canonical, data.finalFacilityDefinitionId);
        Append(canonical, data.replacedFacilityId);
        Append(canonical, data.replacedFacilityDefinitionId);
        Append(canonical, data.absoluteDay);
        Append(canonical, data.gridX);
        Append(canonical, data.gridY);
        Append(canonical, data.requiredWork);
        Append(canonical, data.completedWork);
        Append(canonical, (int)data.qualityTier);

        Append(canonical, data.supportBuildingIds.Count);
        foreach (string supportBuildingId in data.supportBuildingIds)
            Append(canonical, supportBuildingId);

        Append(canonical, data.contributions.Count);
        foreach (CraftContributionSaveData contribution in data.contributions)
        {
            Append(canonical, contribution.characterId);
            Append(canonical, contribution.contributedWork);
            Append(canonical, contribution.relevantSkill);
        }

        Append(canonical, data.itemMaterials.Count);
        foreach (WorkOrderItemMaterialSaveData material in data.itemMaterials)
        {
            Append(canonical, material.itemId);
            Append(canonical, material.required);
            Append(canonical, material.delivered);
        }

        Append(canonical, data.materialInputOperationId);
        Append(canonical, data.restitutionOperationId);
        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
    }

    private static string ValueOrNone(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();

    private static string JoinOrNone(IEnumerable<string> values)
    {
        string[] normalized = (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return normalized.Length == 0 ? "none" : string.Join(",", normalized);
    }

    private static string EncodeContributors(
        IEnumerable<CraftContributionSaveData> contributions)
    {
        string[] encoded = (contributions
                ?? Array.Empty<CraftContributionSaveData>())
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.characterId)
                && value.contributedWork > 0f)
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .Select(value => value.characterId.Trim()
                + "="
                + Math.Round(value.contributedWork * 1000d)
                    .ToString(CultureInfo.InvariantCulture))
            .ToArray();
        return encoded.Length == 0 ? "none" : string.Join(",", encoded);
    }

    private static string EncodeMaterials(
        IEnumerable<WorkOrderItemMaterialSaveData> materials)
    {
        string[] encoded = (materials
                ?? Array.Empty<WorkOrderItemMaterialSaveData>())
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.itemId)
                && value.required > 0)
            .OrderBy(value => value.itemId, StringComparer.Ordinal)
            .Select(value => value.itemId
                + "="
                + value.required.ToString(CultureInfo.InvariantCulture))
            .ToArray();
        return encoded.Length == 0 ? "none" : string.Join(",", encoded);
    }

    private static void Append(StringBuilder target, string value)
    {
        string text = value ?? string.Empty;
        target.Append(Encoding.UTF8.GetByteCount(text).ToString(
                CultureInfo.InvariantCulture))
            .Append(':')
            .Append(text)
            .Append('|');
    }

    private static void Append(StringBuilder target, int value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, float value) =>
        Append(target, value.ToString("R", CultureInfo.InvariantCulture));
}
