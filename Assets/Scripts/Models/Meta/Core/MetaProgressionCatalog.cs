using System.Collections.Generic;

public interface IMetaUpgradeDefinitionCatalog
{
    IReadOnlyCollection<MetaUpgradeDefinition> All { get; }
    MetaUpgradeDefinition Get(string id);
    MetaUpgradeDefinition Require(string id);
}

/// <summary>
/// Stable protocol IDs. Upgrade data itself is authored in GameDomainContentCatalogSO.
/// </summary>
public static class MetaUpgradeIds
{
    public const string StartingFacilityCandidatePlusOne = "meta:starting-facility-candidate";
    public const string StartingOwnerTraitCandidatePlusOne = "meta:starting-owner-trait-candidate";
    public const string BasicPurchaseListExpansion = "meta:basic-purchase-list-expansion";
    public const string SpecialRecipeRecordSlot = "meta:special-recipe-record-slot";
    public const string OwnerSurvivalBonus = "meta:owner-survival-bonus";
    public const string InvasionWarningAccuracy = "meta:invasion-warning-accuracy";
    public const string CommerceSupplyNetwork = "meta:commerce-supply-network";
    public const string FortressEngineering = "meta:fortress-engineering";
    public const string ArcaneResearchMethod = "meta:arcane-research-method";
}
