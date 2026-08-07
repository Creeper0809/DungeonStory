using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(menuName = "DungeonStory/Wildlife/Species", order = 0)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeSpeciesSO : ScriptableObject
{
    [SerializeField] private string speciesId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private Sprite sprite;
    [SerializeField, Min(1)] private int maxHealth = 10;
    [SerializeField, Min(0.1f)] private float moveSpeed = 1f;
    [SerializeField, Range(0f, 2f)] private float fearSensitivity = 1f;
    [SerializeField, Range(0f, 2f)] private float aggression = 0f;
    [SerializeField, Min(0)] private int retaliationDamage;
    [SerializeField, Min(0f)] private float spawnWeight = 1f;
    [SerializeField, Min(1)] private int herdSize = 1;
    [SerializeField] private bool canEnterDungeon;
    [SerializeField, Min(0.1f)] private float carcassWeight = 4f;
    [Header("Ecology")]
    [SerializeField] private WildlifeDietType diet = WildlifeDietType.Herbivore;
    [SerializeField] private List<WildlifeHabitatType> preferredHabitats = new List<WildlifeHabitatType>();
    [SerializeField, Min(2f)] private float territoryRadius = 6f;
    [SerializeField, Range(0.1f, 4f)] private float dailyFoodNeed = 1f;
    [SerializeField, Range(0.1f, 4f)] private float dailyWaterNeed = 1f;
    [SerializeField, Range(0f, 1f)] private float restPreference = 0.45f;
    [SerializeField, Range(0f, 1f)] private float predationDrive = 0f;
    [SerializeField, Range(0f, 1f)] private float fleePreference = 0.55f;
    [SerializeField] private List<WildlifeButcherYield> butcherYields = new List<WildlifeButcherYield>();
    [Header("Husbandry")]
    [SerializeField] private bool domesticable = true;
    [SerializeField, Range(0f, 1f)] private float tamingDifficulty = 0.45f;
    [SerializeField, Min(0.25f)] private float adultAgeDays = 4f;
    [SerializeField, Min(1f)] private float maximumAgeDays = 40f;
    [SerializeField, Min(0.25f)] private float gestationDays = 4f;
    [SerializeField] private bool laysEggs;
    [SerializeField, Min(0.1f)] private float bodySize = 1f;
    [SerializeField, Min(0.25f)] private float manureIntervalDays = 2f;
    [SerializeField] private List<WildlifeHusbandryProductDefinition> husbandryProducts =
        new List<WildlifeHusbandryProductDefinition>();
    [Header("V20 Authored Ecology")]
    [SerializeField, Min(1)] private int authoringRevision = 1;
    [SerializeField] private List<string> preySpeciesIds = new();
    [SerializeField] private List<string> predatorSpeciesIds = new();
    [SerializeField] private string nestTag = string.Empty;
    [SerializeField] private Season breedingSeason;
    [SerializeField] private string migrationPatternId = string.Empty;
    [SerializeField] private List<string> diseaseVectorIds = new();
    [SerializeField] private List<Season> activeSeasons = new();

    public string SpeciesId => speciesId?.Trim() ?? string.Empty;
    public string DisplayName => displayName?.Trim() ?? string.Empty;
    public string Description => description?.Trim() ?? string.Empty;
    public Sprite Sprite => sprite;
    public int MaxHealth => Mathf.Max(1, maxHealth);
    public float MoveSpeed => Mathf.Max(0.1f, moveSpeed);
    public float FearSensitivity => Mathf.Clamp(fearSensitivity, 0f, 2f);
    public float Aggression => Mathf.Clamp(aggression, 0f, 2f);
    public int RetaliationDamage => Mathf.Max(0, retaliationDamage);
    public float SpawnWeight => Mathf.Max(0f, spawnWeight);
    public int HerdSize => Mathf.Max(1, herdSize);
    public bool CanEnterDungeon => canEnterDungeon;
    public float CarcassWeight => Mathf.Max(0.1f, carcassWeight);
    public WildlifeDietType Diet => diet;
    public IReadOnlyList<WildlifeHabitatType> PreferredHabitats => preferredHabitats;
    public float TerritoryRadius => Mathf.Clamp(territoryRadius, 2f, 18f);
    public float DailyFoodNeed => Mathf.Clamp(dailyFoodNeed, 0.1f, 4f);
    public float DailyWaterNeed => Mathf.Clamp(dailyWaterNeed, 0.1f, 4f);
    public float RestPreference => Mathf.Clamp01(restPreference);
    public float PredationDrive => Mathf.Clamp01(Mathf.Max(predationDrive, Aggression >= 0.75f ? 0.7f : 0f));
    public float FleePreference => Mathf.Clamp01(fleePreference);
    public IReadOnlyList<WildlifeButcherYield> ButcherYields => butcherYields;
    public WildlifeHusbandryProfile Husbandry => new WildlifeHusbandryProfile(
        domesticable,
        tamingDifficulty,
        adultAgeDays,
        maximumAgeDays,
        gestationDays,
        laysEggs,
        bodySize,
        manureIntervalDays,
        husbandryProducts);
    public IReadOnlyList<string> PreySpeciesIds => preySpeciesIds;
    public IReadOnlyList<string> PredatorSpeciesIds => predatorSpeciesIds;
    public string NestTag => nestTag?.Trim() ?? string.Empty;
    public Season BreedingSeason => breedingSeason;
    public string MigrationPatternId => migrationPatternId?.Trim() ?? string.Empty;
    public IReadOnlyList<string> DiseaseVectorIds => diseaseVectorIds;
    public IReadOnlyList<Season> ActiveSeasons => activeSeasons;

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        WildlifeSpeciesDefinition runtime = ToDefinition();
        if (runtime.PreferredHabitats.Count == 0) errors.Add($"'{SpeciesId}' requires a preferred habitat.");
        int relationshipGroups = 0;
        if ((preySpeciesIds ?? new()).Count > 0 || (predatorSpeciesIds ?? new()).Count > 0) relationshipGroups++;
        if (!string.IsNullOrWhiteSpace(nestTag)) relationshipGroups++;
        if (!string.IsNullOrWhiteSpace(migrationPatternId)) relationshipGroups++;
        if ((diseaseVectorIds ?? new()).Count > 0) relationshipGroups++;
        if ((activeSeasons ?? new()).Distinct().Count() > 0) relationshipGroups++;
        if (relationshipGroups < 3) errors.Add($"'{SpeciesId}' requires at least three authored ecology relationship groups.");
        if (authoringRevision < 1) errors.Add($"'{SpeciesId}' authoring revision must be positive.");
        return errors;
    }

    public WildlifeSpeciesDefinition ToDefinition()
    {
        return new WildlifeSpeciesDefinition(
            SpeciesId,
            DisplayName,
            Description,
            Sprite,
            MaxHealth,
            MoveSpeed,
            FearSensitivity,
            Aggression,
            RetaliationDamage,
            SpawnWeight,
            HerdSize,
            CanEnterDungeon,
            CarcassWeight,
            butcherYields,
            Diet,
            PreferredHabitats,
            TerritoryRadius,
            DailyFoodNeed,
            DailyWaterNeed,
            RestPreference,
            PredationDrive,
            FleePreference,
            Husbandry);
    }

#if UNITY_EDITOR
    public void ConfigureV20(
        string id,
        string name,
        string detail,
        WildlifeDietType authoredDiet,
        IReadOnlyList<WildlifeHabitatType> habitats,
        float authoredAggression,
        bool authoredDomesticable,
        IReadOnlyList<string> prey,
        IReadOnlyList<string> predators,
        string authoredNestTag,
        Season authoredBreedingSeason,
        string authoredMigrationPatternId,
        IReadOnlyList<string> diseaseVectors,
        IReadOnlyList<Season> seasons)
    {
        speciesId = id?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        description = detail?.Trim() ?? string.Empty;
        diet = authoredDiet;
        preferredHabitats = (habitats ?? new List<WildlifeHabitatType>()).Distinct().ToList();
        aggression = Mathf.Clamp(authoredAggression, 0f, 2f);
        predationDrive = Mathf.Clamp01(authoredAggression);
        domesticable = authoredDomesticable;
        preySpeciesIds = (prey ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
        predatorSpeciesIds = (predators ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
        nestTag = authoredNestTag?.Trim() ?? string.Empty;
        breedingSeason = authoredBreedingSeason;
        migrationPatternId = authoredMigrationPatternId?.Trim() ?? string.Empty;
        diseaseVectorIds = (diseaseVectors ?? new List<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
        activeSeasons = (seasons ?? new List<Season>()).Distinct().ToList();
        authoringRevision = 1;
    }
#endif

}
