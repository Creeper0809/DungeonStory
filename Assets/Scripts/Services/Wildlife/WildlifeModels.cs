using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IWildlifeSpeciesCatalogProvider :
    IWildlifeSpeciesDefinitionCatalog
{
    IReadOnlyList<WildlifeSpeciesDefinition> All { get; }
    WildlifeSpeciesDefinition GetRandomSpecies(IRandomStream randomStream);
}

public sealed class ResourceWildlifeSpeciesCatalogProvider : IWildlifeSpeciesCatalogProvider
{
    private readonly IReadOnlyList<WildlifeSpeciesDefinition> species;
    private readonly IReadOnlyDictionary<string, WildlifeSpeciesDefinition> speciesById;

    public ResourceWildlifeSpeciesCatalogProvider(
        IGameContentCatalog contentCatalog,
        IItemDefinitionCatalog itemCatalog)
    {
        if (contentCatalog == null)
        {
            throw new ArgumentNullException(nameof(contentCatalog));
        }
        if (itemCatalog == null)
        {
            throw new ArgumentNullException(nameof(itemCatalog));
        }

        WildlifeSpeciesSO[] authored = contentCatalog.GetAll<WildlifeSpeciesSO>()
            .Where(asset => asset != null)
            .ToArray();
        if (authored.Length == 0)
        {
            throw new InvalidOperationException(
                "Game content catalog has no authored wildlife species.");
        }

        string[] duplicateIds = authored
            .GroupBy(asset => asset.SpeciesId?.Trim() ?? string.Empty, StringComparer.Ordinal)
            .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1)
            .Select(group => string.IsNullOrWhiteSpace(group.Key) ? "<empty>" : group.Key)
            .ToArray();
        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                "Wildlife species catalog has empty or duplicate ids: "
                + string.Join(", ", duplicateIds));
        }

        foreach (WildlifeSpeciesSO asset in authored)
        {
            if (string.IsNullOrWhiteSpace(asset.DisplayName))
            {
                throw new InvalidOperationException(
                    $"Wildlife species '{asset.name}' has no authored display name.");
            }
            if (!Enum.IsDefined(typeof(WildlifeDietType), asset.Diet))
            {
                throw new InvalidOperationException(
                    $"Wildlife species '{asset.SpeciesId}' has an invalid authored diet.");
            }
            foreach (WildlifeButcherYield yieldItem in asset.ButcherYields
                         ?? Array.Empty<WildlifeButcherYield>())
            {
                ItemDefinitionId itemId = new(yieldItem?.itemId);
                if (!itemId.IsValid || !itemCatalog.TryGet(itemId, out _))
                {
                    throw new InvalidOperationException(
                        $"Wildlife species '{asset.SpeciesId}' references unknown butcher item '{itemId.Value}'.");
                }
            }
            foreach (WildlifeHusbandryProductDefinition product in
                     asset.Husbandry.Products)
            {
                ItemDefinitionId itemId = new(product?.ItemId);
                if (!itemId.IsValid || !itemCatalog.TryGet(itemId, out _))
                {
                    throw new InvalidOperationException(
                        $"Wildlife species '{asset.SpeciesId}' references unknown husbandry item '{itemId.Value}'.");
                }
            }
        }

        species = authored
            .Select(asset => asset.ToDefinition())
            .OrderBy(definition => definition.SpeciesId, StringComparer.Ordinal)
            .ToArray();
        speciesById = species.ToDictionary(
            definition => definition.SpeciesId,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<WildlifeSpeciesDefinition> All
    {
        get
        {
            return species;
        }
    }

    public bool TryGetSpecies(string speciesId, out WildlifeSpeciesDefinition definition)
    {
        string normalized = speciesId?.Trim() ?? string.Empty;
        return speciesById.TryGetValue(normalized, out definition);
    }

    public WildlifeSpeciesDefinition GetRandomSpecies(IRandomStream randomStream)
    {
        if (randomStream == null)
        {
            throw new ArgumentNullException(nameof(randomStream));
        }

        float total = species.Sum(candidate => Mathf.Max(0f, candidate.SpawnWeight));
        if (total <= 0f)
        {
            return species[0];
        }

        float roll = randomStream.NextFloat() * total;
        foreach (WildlifeSpeciesDefinition candidate in species)
        {
            roll -= Mathf.Max(0f, candidate.SpawnWeight);
            if (roll <= 0f)
            {
                return candidate;
            }
        }

        return species[species.Count - 1];
    }
}

#if UNITY_EDITOR
public static class WildlifeTestFixtures
{
    public static readonly WildlifeSpeciesDefinition CaveRat = Create(
        "cave_rat",
        "동굴쥐",
        "틈새와 하차장 주변을 훑는 작고 빠른 짐승.",
        maxHealth: 10,
        moveSpeed: 1.25f,
        fear: 1.45f,
        aggression: 0.05f,
        retaliation: 0,
        weight: 4.5f,
        herd: 2,
        canEnterDungeon: false,
        carcassWeight: 2.5f,
        food: 1,
        husbandry: H(
            0.35f,
            2f,
            24f,
            2f,
            false,
            0.2f,
            1.5f));

    public static readonly WildlifeSpeciesDefinition ShadowHare = Create(
        "shadow_hare",
        "그림자토끼",
        "발소리보다 그림자가 먼저 도망가는 초반 식량원.",
        maxHealth: 8,
        moveSpeed: 1.65f,
        fear: 1.8f,
        aggression: 0f,
        retaliation: 0,
        weight: 3.5f,
        herd: 2,
        canEnterDungeon: false,
        carcassWeight: 3f,
        food: 2,
        husbandry: H(
            0.4f,
            3f,
            30f,
            3f,
            false,
            0.25f,
            2f));

    public static readonly WildlifeSpeciesDefinition MossBoar = Create(
        "moss_boar",
        "이끼멧돼지",
        "축축한 등가죽 아래 힘이 좋은 짐승. 몰리면 들이받는다.",
        maxHealth: 34,
        moveSpeed: 0.85f,
        fear: 0.75f,
        aggression: 0.55f,
        retaliation: 7,
        weight: 2.2f,
        herd: 1,
        canEnterDungeon: false,
        carcassWeight: 28f,
        food: 8,
        hide: 2,
        husbandry: H(
            0.65f,
            6f,
            55f,
            6f,
            false,
            1.4f,
            1f));

    public static readonly WildlifeSpeciesDefinition RuneDeer = Create(
        "rune_deer",
        "룬사슴",
        "뿔에 흐릿한 문양이 남는 희귀한 먹잇감.",
        maxHealth: 22,
        moveSpeed: 1.45f,
        fear: 1.35f,
        aggression: 0.15f,
        retaliation: 2,
        weight: 0.7f,
        herd: 1,
        canEnterDungeon: false,
        carcassWeight: 22f,
        food: 6,
        runeDust: 2,
        husbandry: H(
            0.72f,
            8f,
            70f,
            7f,
            false,
            1.1f,
            1.5f,
            P("resource:horn", 1, 8f)));

    public static readonly WildlifeSpeciesDefinition ShadowWolf = Create(
        "shadow_wolf",
        "그림자늑대",
        "외부 길목을 배회하며 약한 사냥꾼을 노리는 포식자.",
        maxHealth: 28,
        moveSpeed: 1.2f,
        fear: 0.45f,
        aggression: 1.2f,
        retaliation: 9,
        weight: 0.9f,
        herd: 1,
        canEnterDungeon: true,
        carcassWeight: 18f,
        food: 4,
        hide: 2,
        fang: 2,
        husbandry: H(
            0.9f,
            8f,
            60f,
            7f,
            false,
            1f,
            1.5f));

    public static readonly WildlifeSpeciesDefinition AshGoat = Create(
        "ash_goat",
        "잿빛 산양",
        "그을린 비탈에서도 젖과 털을 내주는 온순한 가축.",
        maxHealth: 24,
        moveSpeed: 0.95f,
        fear: 0.8f,
        aggression: 0.12f,
        retaliation: 2,
        weight: 0.8f,
        herd: 2,
        canEnterDungeon: false,
        carcassWeight: 20f,
        food: 5,
        hide: 1,
        husbandry: H(
            0.32f,
            6f,
            65f,
            5f,
            false,
            1f,
            1.25f,
            P("resource:milk", 2, 1.5f, femaleOnly: true),
            P("resource:wool", 2, 4f)));

    public static readonly WildlifeSpeciesDefinition TwilightChicken = Create(
        "twilight_chicken",
        "황혼닭",
        "어둠이 내려앉을 때 알을 낳는 작은 잡식성 가축.",
        maxHealth: 7,
        moveSpeed: 1.1f,
        fear: 1.4f,
        aggression: 0.03f,
        retaliation: 0,
        weight: 2.4f,
        herd: 3,
        canEnterDungeon: false,
        carcassWeight: 2.2f,
        food: 1,
        husbandry: H(
            0.22f,
            3f,
            28f,
            2f,
            true,
            0.18f,
            2f,
            P("resource:egg", 1, 1f, femaleOnly: true),
            P("resource:feather", 2, 4f)));

    public static IReadOnlyList<WildlifeSpeciesDefinition> All { get; } =
        new[]
        {
            CaveRat,
            ShadowHare,
            MossBoar,
            RuneDeer,
            ShadowWolf,
            AshGoat,
            TwilightChicken
        };

    private static WildlifeSpeciesDefinition Create(
        string id,
        string name,
        string description,
        int maxHealth,
        float moveSpeed,
        float fear,
        float aggression,
        int retaliation,
        float weight,
        int herd,
        bool canEnterDungeon,
        float carcassWeight,
        int food = 0,
        int hide = 0,
        int fang = 0,
        int runeDust = 0,
        WildlifeHusbandryProfile husbandry = null)
    {
        List<WildlifeButcherYield> yields = new List<WildlifeButcherYield>();
        AddYield(yields, "resource:meat", food);
        AddYield(yields, WildlifeItemDefinitions.HideItemId, hide);
        AddYield(yields, WildlifeItemDefinitions.FangItemId, fang);
        AddYield(yields, WildlifeItemDefinitions.RuneDustItemId, runeDust);
        return new WildlifeSpeciesDefinition(
            id,
            name,
            description,
            null,
            maxHealth,
            moveSpeed,
            fear,
            aggression,
            retaliation,
            weight,
            herd,
            canEnterDungeon,
            carcassWeight,
            yields,
            husbandry: husbandry);
    }

    private static WildlifeHusbandryProfile H(
        float tamingDifficulty,
        float adultAgeDays,
        float maximumAgeDays,
        float gestationDays,
        bool laysEggs,
        float bodySize,
        float manureIntervalDays,
        params WildlifeHusbandryProductDefinition[] products)
    {
        return new WildlifeHusbandryProfile(
            true,
            tamingDifficulty,
            adultAgeDays,
            maximumAgeDays,
            gestationDays,
            laysEggs,
            bodySize,
            manureIntervalDays,
            products);
    }

    private static WildlifeHusbandryProductDefinition P(
        string itemId,
        int amount,
        float intervalDays,
        bool femaleOnly = false)
    {
        return new WildlifeHusbandryProductDefinition(
            itemId,
            amount,
            intervalDays,
            femaleOnly);
    }

    private static void AddYield(List<WildlifeButcherYield> yields, string itemId, int amount)
    {
        if (amount > 0)
        {
            yields.Add(new WildlifeButcherYield { itemId = itemId, amount = amount });
        }
    }
}
#endif

public readonly struct WildlifeHuntJob
{
    public WildlifeHuntJob(WildlifeActor target)
    {
        Target = target;
        WildlifeId = target != null ? target.WildlifeId : string.Empty;
    }

    public WildlifeActor Target { get; }
    public string WildlifeId { get; }
    public bool IsValid => Target != null && !string.IsNullOrWhiteSpace(WildlifeId);
}

public interface IWildlifeQuery
{
    IReadOnlyList<WildlifeActor> Wildlife { get; }
}

public interface IWildlifeHuntCommandService
{
    bool DesignateHunt(string wildlifeId, bool designated, bool priority = false);
}

public readonly struct WildlifeFoodRaidOrderSnapshot
{
    public WildlifeFoodRaidOrderSnapshot(
        string raidId,
        string wildlifeId,
        string targetStackId,
        WildlifeFoodRaidOrderState state,
        int stolenQuantity,
        string outcomeReason)
    {
        RaidId = raidId ?? string.Empty;
        WildlifeId = wildlifeId ?? string.Empty;
        TargetStackId = targetStackId ?? string.Empty;
        State = state;
        StolenQuantity = Mathf.Max(0, stolenQuantity);
        OutcomeReason = outcomeReason ?? string.Empty;
    }

    public string RaidId { get; }
    public string WildlifeId { get; }
    public string TargetStackId { get; }
    public WildlifeFoodRaidOrderState State { get; }
    public int StolenQuantity { get; }
    public string OutcomeReason { get; }
    public bool IsTerminal =>
        State == WildlifeFoodRaidOrderState.Stolen
        || State == WildlifeFoodRaidOrderState.Cancelled
        || State == WildlifeFoodRaidOrderState.Failed;
}

public interface IWildlifeRuntime : IWildlifeQuery, IWildlifeHuntCommandService
{
    DungeonWildlifeSaveData Capture();
    void ValidateRestorePayload(DungeonWildlifeSaveData saveData);
    WildlifeRestoreCandidate BuildRestoreCandidate(
        DungeonWildlifeSaveData saveData);
    void PublishRestoreCandidate(WildlifeRestoreCandidate candidate);
    bool HasAvailableHuntJob(CharacterActor actor);
    bool TryReserveBestHuntJob(CharacterActor actor, out WildlifeHuntJob job, out string reason);
    void ReleaseHuntReservation(string wildlifeId, CharacterActor actor);
    bool ApplyHuntHit(CharacterActor hunter, string wildlifeId, out string message);
    bool CanAttackHuntTargetFrom(
        CharacterActor hunter,
        WildlifeActor target,
        Grid grid,
        Vector2Int attackerCell);
    bool NeedsHuntReload(CharacterActor hunter);
    float GetHuntReloadDuration(CharacterActor hunter);
    bool TryReloadHuntWeapon(CharacterActor hunter, out string message);
    float GetHuntAttackInterval(CharacterActor hunter);
    bool TryButcherNextCarcass(CharacterActor butcher, BuildableObject building, out int produced, out string message);
    bool HasButcherWorkAvailable(BuildableObject building);
    float GetButcherWorkUrgency();
    bool DebugSpawn(string speciesId, int amount, Vector2Int position, out int spawned, out string message);
    bool TrySpawnArrival(
        string speciesId,
        Vector2Int position,
        out WildlifeActor actor,
        out string message);
    IReadOnlyList<WorldItemStackSnapshot> GetReachableFoodRaidTargets();
    IReadOnlyList<WildlifeFoodRaidOrderSnapshot> GetFoodRaidOrders();
    bool TryBeginFoodRaid(
        string raidId,
        int wolfCount,
        out IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders,
        out string failureReason);
    bool TrySpawnDomesticBirth(
        string speciesId,
        Vector2Int position,
        out WildlifeActor actor,
        out string message);
    bool TryRemoveArrival(string wildlifeId);
    bool DebugDelete(string wildlifeId);
    int DebugDeleteAll();
}

public interface IWildlifeEcosystemRuntime
{
    bool OverlayEnabled { get; }
    IReadOnlyList<WildlifeHabitatPatch> Patches { get; }
    WildlifeEcosystemOverview GetOverview(IReadOnlyList<WildlifeActor> wildlife);
    DungeonWildlifeEcosystemSaveData Capture();
    WildlifeEcosystemRestoreCandidate PrepareRestoreCandidate(
        DungeonWildlifeEcosystemSaveData saveData,
        Grid restoreGrid);
    void PublishRestoreCandidate(
        WildlifeEcosystemRestoreCandidate candidate);
    WildlifeEcosystemRestoreTransaction ApplyRestoreCandidate(
        WildlifeEcosystemRestoreCandidate candidate);
    void RollbackRestore(WildlifeEcosystemRestoreTransaction transaction);
    void CompleteRestore(WildlifeEcosystemRestoreTransaction transaction);
    void SetOverlayEnabled(bool enabled);
    void EnsureInitialized(Grid grid);
    void TickAnimal(WildlifeActor actor, Grid grid, float deltaTime);
    bool TryChooseEcologyTarget(
        WildlifeActor actor,
        Grid grid,
        IReadOnlyList<WildlifeActor> wildlife,
        IReadOnlyList<WorldItemStackSnapshot> itemStacks,
        out Vector2Int target,
        out WildlifeIntent intent,
        out string reason);
    bool TryConsumeRespawnOpportunity(
        float now,
        int aliveCount,
        IReadOnlyList<WildlifeSpeciesDefinition> species,
        out WildlifeSpeciesDefinition selectedSpecies);
    void NotifyWildlifeKilled(WildlifeActor actor, bool byHunt);
    bool ShouldRemoveLeavingAnimal(WildlifeActor actor, Grid grid);
}

public interface ISurvivalFoodQuery
{
    SurvivalFoodOverview GetOverview();
    bool TryGetItemStatus(string stackId, string itemId, out SurvivalItemStatus status);
    bool TryGetCharacterStatus(CharacterActor actor, out SurvivalCharacterStatus status);
    bool HasSurvivalWorkAvailable(BuildableObject building, WorkTypeId workTypeId);
    float GetSurvivalWorkUrgency(BuildableObject building, WorkTypeId workTypeId);
    int GetStoredStockCount(StockCategory category);
}

public interface ISurvivalFoodCommand
{
    bool TryApplySurvivalWork(
        IBuildingVisitorPort actor,
        BuildableObject building,
        WorkTypeId workTypeId,
        out int amount,
        out DomainFailure failure);
    int TryConsumeStoredStock(StockCategory category, int amount);
}

public interface ISurvivalServiceSessionCapability
{
    ServiceHubSnapshot GetHubSnapshot(BuildableObject hub);
    bool TryBeginSession(
        ServiceSessionRequest request,
        out ServiceSessionSnapshot session,
        out DomainFailure failure);
    bool TryCompleteSession(
        string sessionId,
        out ServiceSessionSnapshot completed,
        out DomainFailure failure);
    bool CancelSession(string sessionId, string reason);
}

public interface ISurvivalFoodPersistence
{
    DungeonSurvivalSaveData Capture();
    SurvivalFoodRestoreCandidate BuildRestoreCandidate(
        DungeonSurvivalSaveData saveData);
    void PublishRestoreCandidate(SurvivalFoodRestoreCandidate candidate);
}

public interface ISurvivalFoodDebugCommand
{
    void DebugSetWeather(SurvivalWeatherType weather);
    void DebugAdvanceSpoilage(float seconds);
    void DebugResetSpoilage();
}

public interface ICharacterNutritionRuntime
{
    int GetMealsConsumed(int day);
    int GetMealsConsumed(string characterId, int day);
    IReadOnlyList<CharacterMealLedgerSaveData> GetRecentMeals(int maximumCount = 30);
}

public interface ISurvivalEnvironmentQuery
{
    SurvivalEnvironmentSnapshot GetEnvironmentSnapshot();
}

public static class WildlifeButcherFacilityUtility
{
    public static bool IsButcherFacility(BuildableObject building)
    {
        if (building == null || building.isDestroy || building.Facility == null)
        {
            return false;
        }

        if (building.BuildingData != null
            && building.BuildingData.Abilities.Any(ability => ability is BuildingButcherAbility))
        {
            return true;
        }

        return building.Facility.SupportsRole(FacilityRole.Meal);
    }

    internal static FacilityWorkType AddFallbackWorkTypes(BuildableObject building, FacilityWorkType supportedTypes)
    {
        return IsButcherFacility(building)
            ? supportedTypes | FacilityWorkType.Butcher
            : supportedTypes;
    }
}
