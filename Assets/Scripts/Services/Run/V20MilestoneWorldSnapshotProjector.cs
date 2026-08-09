using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IV20MilestoneWorldSnapshotQuery
{
    IReadOnlyList<CharacterActor> LivingCharacters { get; }
    RunMilestoneEvaluationSnapshot Build(int absoluteDay);
}

public sealed class V20MilestoneWorldSnapshotProjector :
    IV20MilestoneWorldSnapshotQuery
{
    private readonly ICharacterWorldQuery characters;
    private readonly IBuildingWorldQuery buildings;
    private readonly IStockQuery stock;
    private readonly ProgressionSceneRuntimeReferences progression;
    private readonly IFactionCampaignQuery factions;
    private readonly IGameMoneyAccount money;
    private readonly IOffenseQuery offense;
    private readonly IKinshipQuery kinship;
    private IReadOnlyList<CharacterActor> livingCharacters =
        Array.Empty<CharacterActor>();

    public IReadOnlyList<CharacterActor> LivingCharacters => livingCharacters;

    public V20MilestoneWorldSnapshotProjector(
        ICharacterWorldQuery characters,
        IBuildingWorldQuery buildings,
        IStockQuery stock,
        ProgressionSceneRuntimeReferences progression,
        IFactionCampaignQuery factions,
        IGameMoneyAccount money,
        IOffenseQuery offense,
        IKinshipQuery kinship)
    {
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.progression = progression
            ?? throw new ArgumentNullException(nameof(progression));
        this.factions = factions
            ?? throw new ArgumentNullException(nameof(factions));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.offense = offense ?? throw new ArgumentNullException(nameof(offense));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
    }

    public RunMilestoneEvaluationSnapshot Build(int absoluteDay)
    {
        RunMilestoneEvaluationSnapshot snapshot = new()
        {
            AbsoluteDay = Math.Max(0, absoluteDay)
        };
        ProjectResearch(snapshot);

        WorldItemStackSnapshot[] stacks = stock.GetAllStacks()
            .Where(value => value != null && value.Quantity > 0)
            .ToArray();
        foreach (IGrouping<string, WorldItemStackSnapshot> group in stacks
                     .GroupBy(value => value.ItemId, StringComparer.Ordinal))
            snapshot.ItemQuantities[group.Key] = group.Sum(value => value.Quantity);

        BuildableObject[] activeBuildings = buildings.Buildings
            .Where(value => value != null
                && !value.IsBuildingDestroyed
                && value.BuildingData != null)
            .ToArray();
        foreach (IGrouping<string, BuildableObject> group in activeBuildings
                     .GroupBy(value => BuildingId(value.BuildingData),
                         StringComparer.Ordinal))
            snapshot.FacilityCounts[group.Key] = group.Count();

        foreach (FactionCampaignStateSaveData faction in factions.Factions)
            snapshot.Factions[faction.factionId] = faction;

        CharacterActor[] living = characters.Characters
            .Where(value => value != null
                && value.CurrentHealth > 0f
                && value.Identity != null)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToArray();
        livingCharacters = living;
        snapshot.EligibleCharacterCount = living.Length;
        snapshot.WorldMetrics[V20WorldMetricKind.Population] = living.Length;
        snapshot.WorldMetrics[V20WorldMetricKind.Money] = money.Balance;

        int foodUnits = stacks
            .Where(value => value.StockCategory == StockCategory.Food)
            .Sum(value => value.Quantity);
        int waterUnits = stacks
            .Where(value => value.StockCategory == StockCategory.Water)
            .Sum(value => value.Quantity);
        float foodDays = living.Length == 0
            ? 0f
            : foodUnits / (float)living.Length;
        snapshot.WorldMetrics[V20WorldMetricKind.FoodDays] = foodDays;

        float defense = Mathf.Clamp(
            activeBuildings.Sum(DefenseValue),
            0f,
            100f);
        float automation = Mathf.Clamp(
            activeBuildings.Sum(AutomationValue)
                + (snapshot.CompletedResearchIds.Contains(7173) ? 20f : 0f),
            0f,
            100f);
        float runePower = Mathf.Clamp(
            activeBuildings.Sum(RunePowerValue),
            0f,
            100f);
        snapshot.WorldMetrics[V20WorldMetricKind.DefenseReadiness] = defense;
        snapshot.WorldMetrics[V20WorldMetricKind.ProductionAutomation] =
            automation;
        snapshot.WorldMetrics[V20WorldMetricKind.RunePower] = runePower;

        int generation = living.Length == 0
            ? 0
            : living.Max(value => kinship.GetGeneration(
                CharacterPersistentIdentity.Require(value)));
        snapshot.WorldMetrics[V20WorldMetricKind.CompletedGenerations] =
            generation;

        OffenseCampaignSnapshot offenseState = offense.Capture();
        int defeatedBranches = offenseState?.IsAvailable == true
            ? offenseState.CompletedTargetCount
            : 0;
        snapshot.WorldMetrics[V20WorldMetricKind.DefeatedHumanBranches] =
            defeatedBranches;

        bool hasFoodProduction = activeBuildings.Any(value =>
            (value.category is BuildingCategory.Production
                or BuildingCategory.Resource)
            && ContainsAny(BuildingId(value.BuildingData),
                "farm", "crop", "garden", "food", "mushroom",
                "pasture", "kitchen", "greenhouse"));
        if (living.Length > 0
            && foodDays >= 7f
            && waterUnits >= living.Length * 3
            && hasFoodProduction)
            snapshot.WorldFlags.Add("ecology:self-sufficient-today");

        if (snapshot.Factions.Count == 6
            && snapshot.Factions.Values.All(value =>
                value.rapport >= 40 && value.grievance <= 40))
            snapshot.WorldFlags.Add("faction:all-six-allied");
        if (offenseState?.TruthRevealed == true)
            snapshot.WorldFlags.Add("story:truth-core-secured");
        if (defeatedBranches >= 5)
            snapshot.WorldFlags.Add("offense:surface-command-broken");
        if (generation >= 3 && snapshot.CompletedResearchIds.Contains(7240))
            snapshot.WorldFlags.Add("lineage:three-generations");
        if (defense >= 80f && money.Balance >= 1_000 && living.Length >= 20)
            snapshot.WorldFlags.Add("economy:sovereign-ready");
        if (automation >= 100f && snapshot.CompletedResearchIds.Contains(7244))
            snapshot.WorldFlags.Add("industry:self-maintaining");
        if (runePower >= 100f && snapshot.CompletedResearchIds.Contains(7238))
            snapshot.WorldFlags.Add("arcane:grid-integrated");
        if (living.Length >= 60
            && snapshot.CompletedResearchIds.Contains(7271)
            && activeBuildings.Any(value => ContainsAny(
                BuildingId(value.BuildingData),
                "temporal", "time-stasis", "time-fix")))
            snapshot.WorldFlags.Add("temporal:population-sustained");

        return snapshot;
    }

    private void ProjectResearch(RunMilestoneEvaluationSnapshot snapshot)
    {
        BlueprintResearchRuntime research = progression.BlueprintResearch;
        if (research == null)
            return;
        foreach (ResearchProjectSO project in research.ProjectCatalog.Projects
                     .Where(value => value != null
                         && research.State.Projects.IsCompleted(value.ProjectId)))
            snapshot.CompletedResearchIds.Add(project.id);
    }

    private static float DefenseValue(BuildableObject building)
    {
        float value = building.category == BuildingCategory.Wall ? 2f : 0f;
        if ((building.Facility?.roles & FacilityRole.Security) != 0)
            value += 10f;
        if (ContainsAny(BuildingId(building.BuildingData),
                "defense", "guard", "trap", "turret", "wall"))
            value += 8f;
        return value;
    }

    private static float AutomationValue(BuildableObject building)
    {
        string id = BuildingId(building.BuildingData);
        float value = building.category is BuildingCategory.Production
            or BuildingCategory.Crafting ? 3f : 0f;
        if (ContainsAny(id, "conveyor", "automatic", "automation",
                "stock-sensor", "powered", "industrial"))
            value += 12f;
        return value;
    }

    private static float RunePowerValue(BuildableObject building)
    {
        string id = BuildingId(building.BuildingData);
        float value = (building.Facility?.roles & FacilityRole.Mana) != 0
            ? 10f
            : 0f;
        if (ContainsAny(id, "rune", "mana", "arcane"))
            value += 10f;
        return value;
    }

    private static string BuildingId(BuildingSO building) =>
        !string.IsNullOrWhiteSpace(building.ContentDefinitionId)
            ? building.ContentDefinitionId
            : $"building:{building.id}";

    private static bool ContainsAny(string value, params string[] fragments)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return fragments.Any(fragment => normalized.IndexOf(
            fragment,
            StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
