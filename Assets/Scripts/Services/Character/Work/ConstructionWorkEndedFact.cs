using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class ConstructionWorkEndedData
{
    public string workOrderId = string.Empty;
    public WorkOrderTerminalKind terminalKind;
    public string placementOperationId = string.Empty;
    public string constructionSitePersistentId = string.Empty;
    public int targetBuildingId;
    public string buildingDefinitionId = string.Empty;
    public string buildingDisplayName = string.Empty;
    public string finalFacilityId = string.Empty;
    public string finalFacilityDefinitionId = string.Empty;
    public string replacedFacilityId = string.Empty;
    public string replacedFacilityDefinitionId = string.Empty;
    public int absoluteDay;
    public int gridX;
    public int gridY;
    public float requiredWork;
    public float completedWork;
    public CraftsmanshipQualityTier qualityTier;
    public List<string> supportBuildingIds = new();
    public List<CraftContributionSaveData> contributions = new();
    public List<WorkOrderItemMaterialSaveData> itemMaterials = new();
    public string materialInputOperationId = string.Empty;
    public string restitutionOperationId = string.Empty;

    public ConstructionWorkEndedData Clone() => new()
    {
        workOrderId = workOrderId, terminalKind = terminalKind,
        placementOperationId = placementOperationId, constructionSitePersistentId = constructionSitePersistentId,
        targetBuildingId = targetBuildingId, buildingDefinitionId = buildingDefinitionId,
        buildingDisplayName = buildingDisplayName, finalFacilityId = finalFacilityId,
        finalFacilityDefinitionId = finalFacilityDefinitionId, replacedFacilityId = replacedFacilityId,
        replacedFacilityDefinitionId = replacedFacilityDefinitionId, absoluteDay = absoluteDay,
        gridX = gridX, gridY = gridY, requiredWork = requiredWork, completedWork = completedWork,
        qualityTier = qualityTier, materialInputOperationId = materialInputOperationId,
        restitutionOperationId = restitutionOperationId,
        supportBuildingIds = supportBuildingIds?.ToList(),
        contributions = contributions?.Select(value => value == null ? null : new CraftContributionSaveData
        { characterId = value.characterId, contributedWork = value.contributedWork, relevantSkill = value.relevantSkill }).ToList(),
        itemMaterials = itemMaterials?.Select(value => value == null ? null : new WorkOrderItemMaterialSaveData
        { itemId = value.itemId, required = value.required, delivered = value.delivered }).ToList()
    };

    public bool IsEmpty => workOrderId == string.Empty && terminalKind == WorkOrderTerminalKind.None
        && placementOperationId == string.Empty && constructionSitePersistentId == string.Empty
        && targetBuildingId == 0 && buildingDefinitionId == string.Empty && buildingDisplayName == string.Empty
        && finalFacilityId == string.Empty && finalFacilityDefinitionId == string.Empty
        && replacedFacilityId == string.Empty && replacedFacilityDefinitionId == string.Empty
        && absoluteDay == 0 && gridX == 0 && gridY == 0 && requiredWork == 0f && completedWork == 0f
        && qualityTier == default && supportBuildingIds?.Count == 0 && contributions?.Count == 0
        && itemMaterials?.Count == 0 && materialInputOperationId == string.Empty && restitutionOperationId == string.Empty;

    public void Validate()
    {
        if (!Id(workOrderId) || !Id(placementOperationId) || !Id(constructionSitePersistentId)
            || targetBuildingId <= 0 || !Id(buildingDefinitionId)
            || !DomainFactValueSyntax.IsText(buildingDisplayName, 256, false) || absoluteDay < 1
            || terminalKind is not (WorkOrderTerminalKind.Completed or WorkOrderTerminalKind.Cancelled)
            || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), qualityTier)
            || !float.IsFinite(requiredWork) || requiredWork <= 0f
            || !float.IsFinite(completedWork) || completedWork < 0f || completedWork > requiredWork
            || !OptionalId(materialInputOperationId) || !OptionalId(restitutionOperationId)
            || supportBuildingIds == null || contributions == null || itemMaterials == null
            || !SortedUnique(supportBuildingIds)
            || supportBuildingIds.Contains(constructionSitePersistentId)
            || contributions.Any(value => value == null || !Id(value.characterId)
                || !float.IsFinite(value.contributedWork) || value.contributedWork <= 0f
                || !float.IsFinite(value.relevantSkill) || value.relevantSkill < 0f)
            || !SortedUnique(contributions.Select(value => value.characterId))
            || itemMaterials.Any(value => value == null || !Id(value.itemId)
                || value.required <= 0 || value.delivered < 0)
            || !SortedUnique(itemMaterials.Select(value => value.itemId)))
            throw new InvalidOperationException("construction-ended-fact-invalid");
        if (terminalKind == WorkOrderTerminalKind.Completed)
        {
            if (!Id(finalFacilityId) || finalFacilityDefinitionId != buildingDefinitionId
                || completedWork != requiredWork || !OptionalId(replacedFacilityId)
                || !OptionalId(replacedFacilityDefinitionId)
                || (replacedFacilityId.Length == 0) != (replacedFacilityDefinitionId.Length == 0)
                || finalFacilityId == constructionSitePersistentId || supportBuildingIds.Contains(finalFacilityId)
                || replacedFacilityId.Length > 0 && (replacedFacilityId == finalFacilityId
                    || replacedFacilityId == constructionSitePersistentId || supportBuildingIds.Contains(replacedFacilityId)))
                throw new InvalidOperationException("construction-ended-placement-invalid");
        }
        else if (finalFacilityId != string.Empty || finalFacilityDefinitionId != string.Empty
                 || replacedFacilityId != string.Empty || replacedFacilityDefinitionId != string.Empty)
            throw new InvalidOperationException("construction-cancelled-with-placement");
    }

    public bool Matches(WorkOrderSaveData owner) => owner != null
        && owner.workTypeId == BuiltInWorkTypeIds.Construct.Value
        && owner.terminalPhase == WorkOrderTerminalPhase.EffectsAppliedPendingOutcome
        && workOrderId == owner.workOrderId && terminalKind == owner.terminalKind
        && placementOperationId == owner.placementOperationId
        && constructionSitePersistentId == owner.constructionSitePersistentId
        && targetBuildingId == owner.targetBuildingId && gridX == owner.gridX && gridY == owner.gridY
        && requiredWork == owner.requiredWork && completedWork == owner.completedWork
        && finalFacilityId == owner.finalFacilityId && finalFacilityDefinitionId == owner.finalFacilityDefinitionId
        && replacedFacilityId == owner.replacedFacilityId && replacedFacilityDefinitionId == owner.replacedFacilityDefinitionId
        && owner.materialTransfer != null && materialInputOperationId == owner.materialTransfer.operationId
        && restitutionOperationId == owner.materialTransfer.restitutionOperationId
        && supportBuildingIds.SequenceEqual(owner.supportBuildingIds ?? new List<string>(), StringComparer.Ordinal)
        && owner.contributions != null && contributions.Count == owner.contributions.Count
        && contributions.Zip(owner.contributions.OrderBy(value => value?.characterId, StringComparer.Ordinal), (left, right) => right != null
            && left.characterId == right.characterId && left.contributedWork == right.contributedWork
            && left.relevantSkill == right.relevantSkill).All(value => value)
        && owner.itemMaterials != null && itemMaterials.Count == owner.itemMaterials.Count
        && itemMaterials.Zip(owner.itemMaterials.OrderBy(value => value?.itemId, StringComparer.Ordinal), (left, right) => right != null && left.itemId == right.itemId
            && left.required == right.required && left.delivered == right.delivered).All(value => value);

    public static void ValidateOwner(WorkOrderSaveData owner)
    {
        if (owner == null || owner.terminalFact == null
            || !DomainFactValueSyntax.IsText(owner.terminalPublicationKey, 1024, true))
            throw new InvalidOperationException("construction-ended-owner-journal-invalid");
        if (!owner.terminalFactCaptured)
        {
            if (!owner.terminalFact.IsEmpty || owner.terminalPublicationKey != string.Empty
                || owner.terminalPhase != WorkOrderTerminalPhase.None)
                throw new InvalidOperationException("construction-ended-owner-fact-missing");
            return;
        }
        owner.terminalFact.Validate();
        if (!owner.terminalFact.Matches(owner))
            throw new InvalidOperationException("construction-ended-owner-fact-conflict");
    }

    private static bool Id(string value) => DomainFactValueSyntax.IsIdentity(value);
    private static bool OptionalId(string value) => value == string.Empty || Id(value);
    private static bool SortedUnique(IEnumerable<string> values)
    {
        string previous = null;
        foreach (string value in values)
        {
            if (!Id(value) || previous != null && string.CompareOrdinal(previous, value) >= 0) return false;
            previous = value;
        }
        return true;
    }
}

/// <summary>Actual construction completion/cancellation, independent of log storage and policy.</summary>
public sealed class ConstructionWorkEndedFact
{
    private readonly ConstructionWorkEndedData frozen;
    public ConstructionWorkEndedFact(ConstructionWorkEndedData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        frozen = source.Clone();
    }
    public ConstructionWorkEndedData Snapshot() => frozen.Clone();
}
