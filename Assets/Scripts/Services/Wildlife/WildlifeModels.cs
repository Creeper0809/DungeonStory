using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WildlifeSpeciesDefinition
{
    public WildlifeSpeciesDefinition(
        string speciesId,
        string displayName,
        string description,
        Sprite sprite,
        int maxHealth,
        float moveSpeed,
        float fearSensitivity,
        float aggression,
        int retaliationDamage,
        float spawnWeight,
        int herdSize,
        bool canEnterDungeon,
        float carcassWeight,
        IEnumerable<WildlifeButcherYield> butcherYields,
        WildlifeDietType diet = WildlifeDietType.Herbivore,
        IEnumerable<WildlifeHabitatType> preferredHabitats = null,
        float territoryRadius = 6f,
        float dailyFoodNeed = 1f,
        float dailyWaterNeed = 1f,
        float restPreference = 0.5f,
        float predationDrive = 0f,
        float fleePreference = 0.5f)
    {
        SpeciesId = string.IsNullOrWhiteSpace(speciesId) ? "wildlife:unknown" : speciesId.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? SpeciesId : displayName.Trim();
        Description = description?.Trim() ?? string.Empty;
        Sprite = sprite;
        MaxHealth = Mathf.Max(1, maxHealth);
        MoveSpeed = Mathf.Max(0.1f, moveSpeed);
        FearSensitivity = Mathf.Clamp(fearSensitivity, 0f, 2f);
        Aggression = Mathf.Clamp(aggression, 0f, 2f);
        RetaliationDamage = Mathf.Max(0, retaliationDamage);
        SpawnWeight = Mathf.Max(0f, spawnWeight);
        HerdSize = Mathf.Max(1, herdSize);
        CanEnterDungeon = canEnterDungeon;
        CarcassWeight = Mathf.Max(0.1f, carcassWeight);
        Diet = ResolveDiet(diet, Aggression);
        TerritoryRadius = Mathf.Clamp(territoryRadius, 2f, 18f);
        DailyFoodNeed = Mathf.Clamp(dailyFoodNeed, 0.1f, 4f);
        DailyWaterNeed = Mathf.Clamp(dailyWaterNeed, 0.1f, 4f);
        RestPreference = Mathf.Clamp01(restPreference);
        PredationDrive = Mathf.Clamp01(predationDrive);
        FleePreference = Mathf.Clamp01(fleePreference);
        PreferredHabitats = (preferredHabitats ?? GetDefaultHabitats(Diet, Aggression))
            .Distinct()
            .ToArray();
        ButcherYields = (butcherYields ?? Array.Empty<WildlifeButcherYield>())
            .Where(yieldItem => yieldItem != null
                && yieldItem.amount > 0
                && !string.IsNullOrWhiteSpace(yieldItem.itemId))
            .Select(yieldItem => new WildlifeButcherYield
            {
                itemId = yieldItem.itemId.Trim(),
                amount = Mathf.Max(0, yieldItem.amount)
            })
            .ToArray();
    }

    public string SpeciesId { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public Sprite Sprite { get; }
    public int MaxHealth { get; }
    public float MoveSpeed { get; }
    public float FearSensitivity { get; }
    public float Aggression { get; }
    public int RetaliationDamage { get; }
    public float SpawnWeight { get; }
    public int HerdSize { get; }
    public bool CanEnterDungeon { get; }
    public float CarcassWeight { get; }
    public WildlifeDietType Diet { get; }
    public IReadOnlyList<WildlifeHabitatType> PreferredHabitats { get; }
    public float TerritoryRadius { get; }
    public float DailyFoodNeed { get; }
    public float DailyWaterNeed { get; }
    public float RestPreference { get; }
    public float PredationDrive { get; }
    public float FleePreference { get; }
    public IReadOnlyList<WildlifeButcherYield> ButcherYields { get; }
    public string CarcassItemId => WildlifeItemDefinitions.GetCarcassItemId(SpeciesId);
    public bool IsPredator => Aggression >= 0.75f;
    public bool IsDangerous => RetaliationDamage > 0 || Aggression >= 0.5f;

    private static WildlifeDietType ResolveDiet(WildlifeDietType diet, float aggression)
    {
        if (diet == WildlifeDietType.Herbivore && aggression >= 0.85f)
        {
            return WildlifeDietType.Carnivore;
        }

        if (diet == WildlifeDietType.Herbivore && aggression >= 0.45f)
        {
            return WildlifeDietType.Omnivore;
        }

        return diet;
    }

    private static IEnumerable<WildlifeHabitatType> GetDefaultHabitats(WildlifeDietType diet, float aggression)
    {
        yield return WildlifeHabitatType.Water;
        if (diet == WildlifeDietType.Carnivore || aggression >= 0.75f)
        {
            yield return WildlifeHabitatType.Lair;
            yield return WildlifeHabitatType.Brush;
            yield break;
        }

        yield return WildlifeHabitatType.Grass;
        yield return WildlifeHabitatType.Brush;
        yield return WildlifeHabitatType.Burrow;
    }
}

public interface IWildlifeSpeciesCatalogProvider
{
    IReadOnlyList<WildlifeSpeciesDefinition> All { get; }
    bool TryGetSpecies(string speciesId, out WildlifeSpeciesDefinition species);
    WildlifeSpeciesDefinition GetRandomSpecies(IRandomStream randomStream);
}

public sealed class ResourceWildlifeSpeciesCatalogProvider : IWildlifeSpeciesCatalogProvider
{
    private const string ResourcePath = "SO/Wildlife/Species";
    private List<WildlifeSpeciesDefinition> species;

    public IReadOnlyList<WildlifeSpeciesDefinition> All
    {
        get
        {
            EnsureLoaded();
            return species;
        }
    }

    public bool TryGetSpecies(string speciesId, out WildlifeSpeciesDefinition definition)
    {
        string normalized = speciesId?.Trim() ?? string.Empty;
        EnsureLoaded();
        definition = species.FirstOrDefault(candidate =>
            string.Equals(candidate.SpeciesId, normalized, StringComparison.Ordinal));
        return definition != null;
    }

    public WildlifeSpeciesDefinition GetRandomSpecies(IRandomStream randomStream)
    {
        if (randomStream == null)
        {
            throw new ArgumentNullException(nameof(randomStream));
        }

        EnsureLoaded();
        float total = species.Sum(candidate => Mathf.Max(0f, candidate.SpawnWeight));
        if (total <= 0f)
        {
            return species.Count > 0 ? species[0] : WildlifeBuiltIns.CaveRat;
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

    private void EnsureLoaded()
    {
        if (species != null)
        {
            return;
        }

        species = Resources
            .LoadAll<WildlifeSpeciesSO>(ResourcePath)
            .Where(asset => asset != null)
            .Select(asset => asset.ToDefinition())
            .Where(definition => !string.IsNullOrWhiteSpace(definition.SpeciesId))
            .GroupBy(definition => definition.SpeciesId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();

        if (species.Count == 0)
        {
            species.AddRange(WildlifeBuiltIns.All);
        }
    }
}

public static class WildlifeBuiltIns
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
        food: 1);

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
        food: 2);

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
        hide: 2);

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
        runeDust: 2);

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
        fang: 2);

    public static IReadOnlyList<WildlifeSpeciesDefinition> All { get; } =
        new[] { CaveRat, ShadowHare, MossBoar, RuneDeer, ShadowWolf };

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
        int runeDust = 0)
    {
        List<WildlifeButcherYield> yields = new List<WildlifeButcherYield>();
        AddYield(yields, DungeonItemCatalogSO.StockItemId(StockCategory.Food), food);
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
            yields);
    }

    private static void AddYield(List<WildlifeButcherYield> yields, string itemId, int amount)
    {
        if (amount > 0)
        {
            yields.Add(new WildlifeButcherYield { itemId = itemId, amount = amount });
        }
    }
}

public static class WildlifeItemDefinitions
{
    public const string CarcassPrefix = "wild:carcass:";
    public const string HideItemId = "wild:hide";
    public const string FangItemId = "wild:fang";
    public const string RuneDustItemId = "wild:rune_dust";
    public const string RotItemId = "wild:rot";

    public static string GetCarcassItemId(string speciesId)
    {
        string normalized = string.IsNullOrWhiteSpace(speciesId) ? "unknown" : speciesId.Trim();
        return CarcassPrefix + normalized;
    }

    public static bool TryGetSpeciesIdFromCarcass(string itemId, out string speciesId)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (normalized.StartsWith(CarcassPrefix, StringComparison.Ordinal))
        {
            speciesId = normalized.Substring(CarcassPrefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(speciesId);
        }

        speciesId = string.Empty;
        return false;
    }

    public static bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (TryGetSpeciesIdFromCarcass(normalized, out string speciesId))
        {
            definition = new DungeonItemDefinition(
                normalized,
                GetCarcassName(speciesId),
                "도축 시설로 옮기면 식량과 부산물을 얻습니다.",
                StockCategory.Food,
                4,
                null,
                GetCarcassWeight(speciesId),
                1);
            return true;
        }

        switch (normalized)
        {
            case HideItemId:
                definition = new DungeonItemDefinition(
                    HideItemId,
                    "야생 가죽",
                    "장비 제작과 거래에 쓸 수 있는 질긴 가죽.",
                    StockCategory.General,
                    8,
                    null,
                    1.2f,
                    50);
                return true;
            case FangItemId:
                definition = new DungeonItemDefinition(
                    FangItemId,
                    "그림자 송곳니",
                    "위험한 포식자에게서 얻은 날카로운 부산물.",
                    StockCategory.General,
                    14,
                    null,
                    0.35f,
                    50);
                return true;
            case RuneDustItemId:
                definition = new DungeonItemDefinition(
                    RuneDustItemId,
                    "룬 가루",
                    "마나 재료로 취급되는 희미한 결정 가루.",
                    StockCategory.Mana,
                    18,
                    null,
                    0.2f,
                    75);
                return true;
            case RotItemId:
                definition = new DungeonItemDefinition(
                    RotItemId,
                    "부패물",
                    "방치된 사체가 썩으며 남긴 오염원.",
                    StockCategory.General,
                    0,
                    null,
                    0.8f,
                    75);
                return true;
            default:
                definition = null;
                return false;
        }
    }

    private static string GetCarcassName(string speciesId)
    {
        if (WildlifeBuiltIns.All.FirstOrDefault(species =>
                string.Equals(species.SpeciesId, speciesId, StringComparison.Ordinal)) is { } species)
        {
            return species.DisplayName + " 사체";
        }

        return speciesId + " 사체";
    }

    private static float GetCarcassWeight(string speciesId)
    {
        WildlifeSpeciesDefinition species = WildlifeBuiltIns.All.FirstOrDefault(candidate =>
            string.Equals(candidate.SpeciesId, speciesId, StringComparison.Ordinal));
        return species != null ? species.CarcassWeight : 8f;
    }
}

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

public interface IWildlifeRuntime : IWildlifeQuery, IWildlifeHuntCommandService
{
    DungeonWildlifeSaveData Capture();
    void Restore(DungeonWildlifeSaveData saveData, DungeonGameRestoreReport report = null);
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
    void Restore(DungeonWildlifeEcosystemSaveData saveData);
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

public interface ISurvivalFoodRuntime
{
    DungeonSurvivalSaveData Capture();
    void Restore(DungeonSurvivalSaveData saveData);
    SurvivalFoodOverview GetOverview();
    bool TryGetItemStatus(string stackId, string itemId, out SurvivalItemStatus status);
    bool TryGetCharacterStatus(CharacterActor actor, out SurvivalCharacterStatus status);
    bool TryApplySurvivalWork(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId, out int amount, out string message);
    bool HasSurvivalWorkAvailable(BuildableObject building, WorkTypeId workTypeId);
    float GetSurvivalWorkUrgency(BuildableObject building, WorkTypeId workTypeId);
    int GetStoredStockCount(StockCategory category);
    int TryConsumeStoredStock(StockCategory category, int amount);
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
