using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>The applied garment changes, frozen independently of logging and live definitions.</summary>
public sealed class ApparelWorkCompletedFact
{
    private readonly ApparelWorkCompletedData frozen;
    public ApparelWorkCompletedFact(ApparelWorkCompletedData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        frozen = source.Clone();
    }
    public ApparelWorkCompletedData Snapshot() => frozen.Clone();
}

[Serializable]
public sealed class ApparelWorkCompletedData
{
    public string orderId = string.Empty;
    public ApparelWorkOrderKind kind;
    public string facilityInstanceId = string.Empty;
    public int absoluteDay;
    public float completedWork;
    public string materialOperationId = string.Empty;
    public string materialCommitId = string.Empty;
    public List<string> materialSourceStackIds = new();
    public int materialQuantity;
    public long materialMassGrams;
    public List<CraftContributionSaveData> contributions = new();
    public List<ApparelWorkOrderOutcomeItemStateSaveData> itemStates = new();

    public ApparelWorkCompletedData Clone() => new()
    {
        orderId = orderId, kind = kind, facilityInstanceId = facilityInstanceId,
        absoluteDay = absoluteDay, completedWork = completedWork,
        materialOperationId = materialOperationId, materialCommitId = materialCommitId,
        materialSourceStackIds = materialSourceStackIds?.ToList(), materialQuantity = materialQuantity,
        materialMassGrams = materialMassGrams,
        contributions = contributions?.Select(value => value == null ? null : new CraftContributionSaveData
        { characterId = value.characterId, contributedWork = value.contributedWork, relevantSkill = value.relevantSkill }).ToList(),
        itemStates = itemStates?.Select(CloneItem).ToList()
    };

    public static ApparelWorkOrderOutcomeItemStateSaveData CloneItem(ApparelWorkOrderOutcomeItemStateSaveData value) =>
        value == null ? null : new ApparelWorkOrderOutcomeItemStateSaveData
        {
            itemInstanceId = value.itemInstanceId, stackId = value.stackId,
            apparelDisplayName = value.apparelDisplayName,
            beforeStatePayload = value.beforeStatePayload, afterStatePayload = value.afterStatePayload
        };

    public static ApparelWorkCompletedData FromOwner(ApparelWorkOrderSaveData owner) => new()
    {
        orderId = owner.orderId, kind = owner.kind, facilityInstanceId = owner.facilityInstanceId,
        absoluteDay = owner.terminalAbsoluteDay, completedWork = owner.completedWork,
        materialOperationId = owner.terminalMaterialOperationId,
        materialCommitId = owner.terminalMaterialCommitId,
        materialSourceStackIds = owner.terminalMaterialSourceStackIds?.ToList(),
        materialQuantity = owner.terminalMaterialQuantity, materialMassGrams = owner.terminalMaterialMassGrams,
        contributions = owner.contributions?.Select(value => value == null ? null : new CraftContributionSaveData
        { characterId = value.characterId, contributedWork = value.contributedWork, relevantSkill = value.relevantSkill })
            .OrderBy(value => value?.characterId, StringComparer.Ordinal).ToList(),
        itemStates = owner.terminalOutcomeItemStates?.Select(CloneItem).ToList()
    };

    public void Validate()
    {
        if (!Id(orderId) || !Id(facilityInstanceId) || absoluteDay < 1
            || kind is not (ApparelWorkOrderKind.Laundry or ApparelWorkOrderKind.Drying
                or ApparelWorkOrderKind.Repair or ApparelWorkOrderKind.Alteration)
            || !float.IsFinite(completedWork) || completedWork <= 0f
            || !OptionalId(materialOperationId) || !OptionalId(materialCommitId)
            || materialSourceStackIds == null || !OrderedIds(materialSourceStackIds)
            || materialQuantity < 0 || materialMassGrams < 0
            || contributions == null || !OrderedIds(contributions.Select(value => value?.characterId))
            || contributions.Any(value => value == null || !float.IsFinite(value.contributedWork)
                || value.contributedWork <= 0f || !float.IsFinite(value.relevantSkill) || value.relevantSkill < 0f)
            || itemStates == null || itemStates.Count is < 1 or > 12
            || !OrderedIds(itemStates.Select(value => value?.itemInstanceId))
            || itemStates.Any(value => value == null || !Id(value.stackId)
                || !DomainFactValueSyntax.IsText(value.apparelDisplayName, 256, false)
                || !DomainFactValueSyntax.IsText(value.beforeStatePayload, 3500, false)
                || !DomainFactValueSyntax.IsText(value.afterStatePayload, 3500, false)))
            throw new InvalidOperationException("apparel-completed-fact-invalid");
        bool anyMaterial = materialOperationId.Length > 0 || materialCommitId.Length > 0
            || materialSourceStackIds.Count > 0 || materialQuantity != 0 || materialMassGrams != 0;
        if (anyMaterial && (kind != ApparelWorkOrderKind.Repair || materialOperationId.Length == 0
            || materialCommitId.Length == 0 || materialSourceStackIds.Count == 0
            || materialQuantity <= 0 || materialMassGrams <= 0))
            throw new InvalidOperationException("apparel-completed-material-invalid");
    }

    public static void ValidateOwner(ApparelWorkOrderSaveData owner)
    {
        if (owner == null || !DomainFactValueSyntax.IsText(owner.terminalPublicationKey, 1024, true)
            || owner.terminalMaterialSourceStackIds == null || owner.terminalOutcomeItemStates == null)
            throw new InvalidOperationException("apparel-completed-owner-invalid");
        if (!owner.terminalOutcomeReady)
        {
            if (owner.terminalPublicationKey != string.Empty || owner.terminalPublicationConfirmed
                || owner.terminalAbsoluteDay != 0 || owner.terminalOutcomeItemStates.Count != 0
                || owner.terminalMaterialOperationId != string.Empty || owner.terminalMaterialCommitId != string.Empty
                || owner.terminalMaterialSourceStackIds.Count != 0 || owner.terminalMaterialQuantity != 0
                || owner.terminalMaterialMassGrams != 0)
                throw new InvalidOperationException("apparel-completed-owner-without-fact");
            return;
        }
        FromOwner(owner).Validate();
        if (owner.completedWork != owner.requiredWork
            || owner.terminalPublicationConfirmed && owner.terminalPublicationKey == string.Empty)
            throw new InvalidOperationException("apparel-completed-owner-publication-invalid");
    }

    private static bool Id(string value) => DomainFactValueSyntax.IsIdentity(value);
    private static bool OptionalId(string value) => value == string.Empty || Id(value);
    private static bool OrderedIds(IEnumerable<string> values)
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
