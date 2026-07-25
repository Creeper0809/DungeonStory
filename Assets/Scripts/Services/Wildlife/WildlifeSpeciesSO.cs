using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Wildlife/Species", order = 0)]
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

    public string SpeciesId => string.IsNullOrWhiteSpace(speciesId) ? name : speciesId.Trim();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? SpeciesId : displayName.Trim();
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
    public WildlifeDietType Diet => ResolveDiet();
    public IReadOnlyList<WildlifeHabitatType> PreferredHabitats => preferredHabitats;
    public float TerritoryRadius => Mathf.Clamp(territoryRadius, 2f, 18f);
    public float DailyFoodNeed => Mathf.Clamp(dailyFoodNeed, 0.1f, 4f);
    public float DailyWaterNeed => Mathf.Clamp(dailyWaterNeed, 0.1f, 4f);
    public float RestPreference => Mathf.Clamp01(restPreference);
    public float PredationDrive => Mathf.Clamp01(Mathf.Max(predationDrive, Aggression >= 0.75f ? 0.7f : 0f));
    public float FleePreference => Mathf.Clamp01(fleePreference);
    public IReadOnlyList<WildlifeButcherYield> ButcherYields => butcherYields;

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
            FleePreference);
    }

    private WildlifeDietType ResolveDiet()
    {
        if (diet == WildlifeDietType.Herbivore && Aggression >= 0.85f)
        {
            return WildlifeDietType.Carnivore;
        }

        if (diet == WildlifeDietType.Herbivore && Aggression >= 0.45f)
        {
            return WildlifeDietType.Omnivore;
        }

        return diet;
    }
}
