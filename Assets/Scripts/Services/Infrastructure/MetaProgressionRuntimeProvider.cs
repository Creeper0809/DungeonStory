using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public interface IStartingOwnerTraitCountBonusQuery
{
    bool TryGetStartingOwnerTraitCountBonus(out int bonus, out string failureReason);
}

public sealed class LiveStartingOwnerTraitCountBonusQuery :
    IStartingOwnerTraitCountBonusQuery
{
    private readonly IMetaProgressionRuntimeReader reader;

    public LiveStartingOwnerTraitCountBonusQuery(
        IMetaProgressionRuntimeReader reader)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public bool TryGetStartingOwnerTraitCountBonus(
        out int bonus,
        out string failureReason)
    {
        bonus = reader.GetStartingOwnerTraitCandidateBonus();
        if (bonus is 0 or 1)
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = "starting-owner-trait-count-bonus-out-of-range:"
            + bonus;
        return false;
    }
}

public sealed class ProfileStoreStartingOwnerTraitCountBonusQuery :
    IStartingOwnerTraitCountBonusQuery
{
    private readonly IMetaProfileStore profileStore;
    private readonly IMetaUpgradeDefinitionCatalog catalog;

    public ProfileStoreStartingOwnerTraitCountBonusQuery(
        IMetaProfileStore profileStore,
        IMetaUpgradeDefinitionCatalog catalog)
    {
        this.profileStore = profileStore
            ?? throw new ArgumentNullException(nameof(profileStore));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public bool TryGetStartingOwnerTraitCountBonus(
        out int bonus,
        out string failureReason)
    {
        bonus = 0;
        failureReason = string.Empty;
        string profilePath = profileStore.ProfilePath;
        if (string.IsNullOrWhiteSpace(profilePath))
        {
            failureReason = "starting-owner-trait-count-profile-path-missing";
            return false;
        }

        MetaUpgradeDefinition definition = catalog.Get(
            MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne);
        if (definition == null || definition.maxLevel < 1)
        {
            failureReason = "starting-owner-trait-count-definition-invalid:" +
                MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne;
            return false;
        }

        if (!File.Exists(profilePath))
        {
            return true;
        }

        if (!profileStore.TryLoad(out DungeonMetaProfileData profile)
            || profile == null)
        {
            failureReason = "starting-owner-trait-count-profile-load-failed:" + profilePath;
            return false;
        }

        if (profile.version != DungeonMetaProfileData.CurrentVersion
            || profile.upgradeLevels == null)
        {
            failureReason = "starting-owner-trait-count-profile-invalid-schema:" + profilePath;
            return false;
        }

        int level = 0;
        bool foundConsumedUpgrade = false;
        foreach (DungeonStringIntSaveEntry entry in profile.upgradeLevels)
        {
            if (entry == null
                || !string.Equals(
                    entry.key?.Trim(),
                    MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.Equals(
                    entry.key,
                    MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne,
                    StringComparison.Ordinal)
                || foundConsumedUpgrade
                || entry.value < 0
                || entry.value > definition.maxLevel)
            {
                failureReason = "starting-owner-trait-count-profile-invalid-consumed-upgrade:"
                    + profilePath;
                return false;
            }

            foundConsumedUpgrade = true;
            level = entry.value;
        }

        IReadOnlyDictionary<string, int> levels =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne] = level
            };
        bonus = MetaProgressionEffects.GetIntegerBonus(
            catalog.All,
            levels,
            MetaUpgradeEffectIds.StartingOwnerTraitCandidates);
        if (bonus is 0 or 1)
        {
            return true;
        }

        failureReason = "starting-owner-trait-count-bonus-out-of-range:" + bonus;
        return false;
    }
}

public interface IMetaProgressionRuntimeReader
{
    int GetStartingFacilityCandidateBonus();
    int GetStartingOwnerTraitCandidateBonus();
    float GetOwnerMaxHealthMultiplier();
    float GetInvasionWarningThresholdMultiplier();
    float GetCommerceStockCostMultiplier(StockCategory category);
    float GetFortressFacilityCostMultiplier(BuildingSO building);
    float GetArcaneResearchWorkMultiplier();
    bool IsRecipePreserved(string recipeId);
    IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings);
}

public sealed class MetaProgressionRuntimeReader : IMetaProgressionRuntimeReader
{
    private readonly MetaProgressionRuntime runtime;

    public MetaProgressionRuntimeReader(
        ProgressionSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .MetaProgression
            ?? throw new InvalidOperationException(
                $"{nameof(MetaProgressionRuntimeReader)} requires a loaded {nameof(MetaProgressionRuntime)}.");
    }

    public int GetStartingFacilityCandidateBonus()
    {
        return runtime.GetStartingFacilityCandidateBonus();
    }

    public int GetStartingOwnerTraitCandidateBonus()
    {
        return runtime.GetStartingOwnerTraitCandidateBonus();
    }

    public float GetOwnerMaxHealthMultiplier()
    {
        return runtime.GetOwnerMaxHealthMultiplier();
    }

    public float GetInvasionWarningThresholdMultiplier()
    {
        return runtime.GetInvasionWarningThresholdMultiplier();
    }

    public float GetCommerceStockCostMultiplier(StockCategory category)
    {
        return runtime.GetCommerceStockCostMultiplier(
            category == StockCategory.Food || category == StockCategory.General);
    }

    public float GetFortressFacilityCostMultiplier(BuildingSO building)
    {
        return runtime.GetFortressFacilityCostMultiplier(
            building?.Defense != null && building.Defense.IsDefenseFacility);
    }

    public float GetArcaneResearchWorkMultiplier()
    {
        return runtime.GetArcaneResearchWorkMultiplier();
    }

    public bool IsRecipePreserved(string recipeId)
    {
        return runtime.IsRecipePreserved(recipeId);
    }

    public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)
    {
        return runtime.GetExpandedBasicPurchaseBuildingIds((buildings ?? Array.Empty<BuildingSO>())
            .Where(building => building != null)
            .Select(building => new MetaFacilityCandidateSnapshot(
                building.id,
                !building.IsGridMovement
                && !building.IsWall
                && FacilityShopService.GetBuildingStar(building) <= 1)));
    }
}
