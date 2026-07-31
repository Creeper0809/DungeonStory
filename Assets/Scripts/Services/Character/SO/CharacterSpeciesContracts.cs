using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SpeciesOwnerSelectionPolicy
{
    Selectable = 0,
    NpcOnly = 1
}

public enum SpeciesMetabolismKind
{
    Biological = 0,
    Construct = 1
}

public enum SpeciesTreatmentKind
{
    BiologicalMedicine = 0,
    MechanicalMaintenance = 1
}

[Serializable]
public sealed class SpeciesNeedProfile
{
    [Min(0f)] public float hungerRateMultiplier = 1f;
    [Min(0f)] public float thirstRateMultiplier = 1f;
    [Min(0f)] public float sleepRateMultiplier = 1f;
    [Min(0f)] public float hygieneRateMultiplier = 1f;
    [Min(0f)] public float socialNeedMultiplier = 1f;
    [Min(0f)] public float chargeRateMultiplier = 1f;
    [Min(0f)] public float integrityWearMultiplier = 1f;
    public MealDietClass diet = MealDietClass.Mixed;
    public SpeciesMetabolismKind metabolism = SpeciesMetabolismKind.Biological;
    public SpeciesTreatmentKind treatment = SpeciesTreatmentKind.BiologicalMedicine;

    public bool UsesChargeInsteadOfFood =>
        metabolism == SpeciesMetabolismKind.Construct;
    public bool UsesMaintenanceInsteadOfSurgery =>
        treatment == SpeciesTreatmentKind.MechanicalMaintenance;
}

[Serializable]
public sealed class SpeciesEnvironmentProfile
{
    public float comfortMinimum = 15f;
    public float comfortMaximum = 27f;
    public float safeMinimum;
    public float safeMaximum = 40f;
    public float lethalMinimum = -10f;
    public float lethalMaximum = 48f;
    [Range(0f, 100f)] public float comfortableAirMinimum = 70f;
    [Range(0f, 100f)] public float comfortableLightMinimum = 40f;
    [Range(0f, 100f)] public float comfortableLightMaximum = 100f;
    [Range(0.05f, 2f)] public float airborneExposureMultiplier = 1f;
    [Range(0.05f, 2f)] public float visualStrainMultiplier = 1f;
    [Range(0f, 1f)] public float preferredHumidity = 0.5f;
    [Range(0f, 2f)] public float drynessSensitivity = 1f;

    public SpeciesThermalProfile ToThermalProfile()
    {
        float lethalMin = Mathf.Min(lethalMinimum, lethalMaximum - 4f);
        float lethalMax = Mathf.Max(lethalMaximum, lethalMin + 4f);
        float safeMin = Mathf.Clamp(safeMinimum, lethalMin + 2f, lethalMax - 2f);
        float safeMax = Mathf.Clamp(safeMaximum, safeMin, lethalMax - 2f);
        float comfortMin = Mathf.Clamp(comfortMinimum, safeMin, safeMax);
        float comfortMax = Mathf.Clamp(comfortMaximum, comfortMin, safeMax);
        return new SpeciesThermalProfile(
            comfortMin,
            comfortMax,
            safeMin,
            safeMax,
            lethalMin,
            lethalMax);
    }
}

[Serializable]
public sealed class SpeciesIncidentDefinition
{
    public string incidentId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    public FacilityRole mitigatingRoles;
    public string[] triggerTags = Array.Empty<string>();

    public string StableId => incidentId?.Trim() ?? string.Empty;
}

[Serializable]
public sealed class SpeciesPassiveDefinition
{
    public string passiveId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    public string[] mechanicTags = Array.Empty<string>();

    public string StableId => passiveId?.Trim() ?? string.Empty;
}

public static class CharacterSpeciesIncidentIds
{
    public const string None = "";
    public const string SlimeContamination = "species-incident:slime-contamination";
    public const string OrcRampage = "species-incident:orc-rampage";
    public const string VampireFear = "species-incident:vampire-fear";
    public const string BeastkinCommotion = "species-incident:beastkin-commotion";
    public const string DemonContractCurse = "species-incident:demon-contract-curse";
    public const string KoboldPartsHoarding = "species-incident:kobold-parts-hoarding";
    public const string MyconidSporeBloom = "species-incident:myconid-spore-bloom";
    public const string HarpyGaleCommotion = "species-incident:harpy-gale-commotion";
    public const string GolemCoreOverload = "species-incident:golem-core-overload";

    public static string FromLegacy(CharacterSpeciesIncidentType legacy)
    {
        return legacy switch
        {
            CharacterSpeciesIncidentType.SlimeContamination => SlimeContamination,
            CharacterSpeciesIncidentType.OrcRampage => OrcRampage,
            CharacterSpeciesIncidentType.VampireFear => VampireFear,
            _ => None
        };
    }
}

public static class CharacterSpeciesExpansionDefaults
{
    private static readonly string[] Tags =
    {
        "Beastkin",
        "Demon",
        "Kobold",
        "Myconid",
        "Harpy",
        "Golem"
    };

    public static IReadOnlyList<CharacterSpeciesSO> MergeWithFallbacks(
        IEnumerable<CharacterSpeciesSO> loaded)
    {
        List<CharacterSpeciesSO> values = (loaded
                ?? Array.Empty<CharacterSpeciesSO>())
            .Where(value => value != null)
            .ToList();
        HashSet<string> present = values
            .Where(value => !string.IsNullOrWhiteSpace(value.speciesTag))
            .Select(value => value.speciesTag.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (string tag in Tags)
        {
            if (!present.Contains(tag))
            {
                values.Add(Create(tag));
            }
        }

        return values
            .OrderBy(value => value.id)
            .ThenBy(value => value.speciesTag, StringComparer.Ordinal)
            .ToArray();
    }

    private static CharacterSpeciesSO Create(string tag)
    {
        CharacterSpeciesSO value =
            ScriptableObject.CreateInstance<CharacterSpeciesSO>();
        value.name = $"RuntimeSpecies_{tag}";
        value.speciesTag = tag;
        value.ownerSelectionPolicy = SpeciesOwnerSelectionPolicy.NpcOnly;
        value.statBonus = CharacterStatBlock.CreateDefault(0);
        value.modifiers = new CharacterModelModifiers();
        value.combatAbilities = new CharacterCombatAbilityCollection();

        switch (tag)
        {
            case "Beastkin":
                Configure(
                    value, 4, "수인", DungeonFactionIds.Beastkin,
                    "anatomy:humanoid",
                    Needs(1.35f, 1.15f, 1f, 1f, 1.4f, MealDietClass.Carnivore),
                    Environment(10f, 29f, -4f, 40f, -12f, 48f, 70f),
                    new[] { "야외", "무리", "소음" },
                    new[] { "감지", "재장전", "번개" },
                    FacilityWorkType.Haul | FacilityWorkType.Restock
                        | FacilityWorkType.Hunt | FacilityWorkType.Reception,
                    FacilityWorkType.Research | FacilityWorkType.Surgery,
                    new[] { "고기 식당", "상점", "야외 휴식처" },
                    new[] { "장기 고립", "비좁은 침실", "채식 전용 식당" },
                    CharacterSpeciesIncidentIds.BeastkinCommotion,
                    "수인 소동",
                    FacilityRole.Rest | FacilityRole.Entertainment,
                    "갈퀴 돌진",
                    new OffenseDamageEffect(1.15f, 2f, 2));
                break;
            case "Demon":
                Configure(
                    value, 5, "데몬", DungeonFactionIds.Demon,
                    "anatomy:humanoid",
                    Needs(1f, 0.9f, 1f, 1f, 0.8f, MealDietClass.Mixed),
                    Environment(20f, 34f, 10f, 46f, -2f, 56f, 60f),
                    new[] { "화염", "마력", "고급" },
                    new[] { "화염", "번개", "공포" },
                    FacilityWorkType.Research | FacilityWorkType.Guard,
                    FacilityWorkType.Clean | FacilityWorkType.AnimalCare,
                    new[] { "마나실", "고급 객실", "연구실" },
                    new[] { "저온 창고", "저가 숙소", "단순 배식대" },
                    CharacterSpeciesIncidentIds.DemonContractCurse,
                    "계약 저주",
                    FacilityRole.Administration | FacilityRole.Mana,
                    "잿불 계약",
                    new OffenseDamageEffect(1.4f, 4f));
                break;
            case "Kobold":
                Configure(
                    value, 6, "코볼트", DungeonFactionIds.Kobold,
                    "anatomy:humanoid",
                    Needs(0.85f, 0.9f, 1f, 1f, 0.9f, MealDietClass.Mixed),
                    Environment(11f, 28f, -2f, 40f, -10f, 48f, 60f),
                    new[] { "질서", "협소", "기계" },
                    new[] { "기계", "수리", "탄약" },
                    FacilityWorkType.Quarry | FacilityWorkType.Repair
                        | FacilityWorkType.Refuel,
                    FacilityWorkType.Reception | FacilityWorkType.Perform,
                    new[] { "정비대", "채굴 작업장", "좁은 숙소" },
                    new[] { "대형 연회장", "무질서한 창고", "야외 노숙지" },
                    CharacterSpeciesIncidentIds.KoboldPartsHoarding,
                    "부품 사재기",
                    FacilityRole.Logistics | FacilityRole.Security,
                    "급조 함정",
                    new OffenseDelayEffect(0.35f));
                break;
            case "Myconid":
                Configure(
                    value, 7, "균사인", DungeonFactionIds.Myconid,
                    "anatomy:fungal",
                    Needs(0.75f, 1.2f, 1f, 0.8f, 0.7f, MealDietClass.Vegan),
                    Environment(8f, 22f, 0f, 32f, -8f, 40f, 35f, 0.55f),
                    new[] { "습기", "오염", "야외" },
                    new[] { "독", "포자", "제독" },
                    FacilityWorkType.Sow | FacilityWorkType.Harvest
                        | FacilityWorkType.Treat | FacilityWorkType.Clean,
                    FacilityWorkType.Guard | FacilityWorkType.Perform,
                    new[] { "재배실", "퇴비실", "제독실" },
                    new[] { "건조한 객실", "강한 조명", "화염 통로" },
                    CharacterSpeciesIncidentIds.MyconidSporeBloom,
                    "포자 개화",
                    FacilityRole.Hygiene | FacilityRole.Medical,
                    "회복 포자",
                    new OffenseHealEffect(12f));
                break;
            case "Harpy":
                Configure(
                    value, 8, "하피", DungeonFactionIds.Harpy,
                    "anatomy:avian",
                    Needs(1.1f, 1f, 1f, 1f, 1.15f, MealDietClass.Mixed),
                    Environment(7f, 25f, -5f, 36f, -15f, 44f, 80f),
                    new[] { "야외", "청정", "개방" },
                    new[] { "경보", "원거리", "외부 엄호" },
                    FacilityWorkType.Reception | FacilityWorkType.Guard
                        | FacilityWorkType.Hunt,
                    FacilityWorkType.Quarry | FacilityWorkType.Construct,
                    new[] { "전망대", "접수실", "야외 휴식처" },
                    new[] { "오염 공기", "낮은 천장", "혼잡 통로" },
                    CharacterSpeciesIncidentIds.HarpyGaleCommotion,
                    "돌풍 소동",
                    FacilityRole.Rest | FacilityRole.Logistics,
                    "폭풍 사격",
                    new OffenseDamageEffect(1.1f, 3f));
                break;
            case "Golem":
                SpeciesNeedProfile constructNeeds = Needs(
                    0f, 0f, 0.35f, 0.25f, 0.2f, MealDietClass.Mixed);
                constructNeeds.metabolism = SpeciesMetabolismKind.Construct;
                constructNeeds.treatment =
                    SpeciesTreatmentKind.MechanicalMaintenance;
                constructNeeds.chargeRateMultiplier = 1f;
                constructNeeds.integrityWearMultiplier = 1f;
                Configure(
                    value, 9, "골렘", DungeonFactionIds.Golem,
                    "anatomy:construct",
                    constructNeeds,
                    Environment(-5f, 35f, -20f, 50f, -35f, 65f, 20f),
                    new[] { "질서", "마력", "기계" },
                    new[] { "방벽", "중화기", "시설 복구" },
                    FacilityWorkType.Haul | FacilityWorkType.Construct
                        | FacilityWorkType.Repair | FacilityWorkType.Plumbing,
                    FacilityWorkType.Reception | FacilityWorkType.Research,
                    new[] { "충전실", "정비대", "중량 하역장" },
                    new[] { "침수 구역", "부식 오염", "장기 무전력 구역" },
                    CharacterSpeciesIncidentIds.GolemCoreOverload,
                    "핵 과부하",
                    FacilityRole.Mana | FacilityRole.Logistics,
                    "주조 방벽",
                    new OffenseGuardEffect(0.45f, 2));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(tag), tag, null);
        }

        return value;
    }

    private static void Configure(
        CharacterSpeciesSO value,
        int id,
        string displayName,
        string factionId,
        string anatomyId,
        SpeciesNeedProfile needs,
        SpeciesEnvironmentProfile environment,
        string[] relationTags,
        string[] defenseTags,
        FacilityWorkType strongWork,
        FacilityWorkType weakWork,
        string[] preferences,
        string[] dislikes,
        string incidentId,
        string incidentName,
        FacilityRole mitigatingRoles,
        string activeName,
        OffenseCombatEffectModule activeEffect)
    {
        value.id = id;
        value.displayName = displayName;
        value.homeFactionId = factionId;
        value.anatomyProfileId = anatomyId;
        value.needs = needs;
        value.environment = environment;
        value.relationTags = relationTags;
        value.defenseAffinityTags = defenseTags;
        value.strongWorkTypeIds = WorkTypeCatalog
            .Enumerate(strongWork)
            .Select(definition => definition.Id)
            .ToArray();
        value.weakWorkTypeIds = WorkTypeCatalog
            .Enumerate(weakWork)
            .Select(definition => definition.Id)
            .ToArray();
        value.preferredFacilityLabels = preferences;
        value.dislikedEnvironmentLabels = dislikes;
        value.shortDescription =
            $"{displayName} 운영·전투·생리 프로필";
        value.description =
            $"{displayName}은(는) 고유한 욕구와 작업 강점, 사고 및 방어 친화도를 가진다.";
        value.incident = new SpeciesIncidentDefinition
        {
            incidentId = incidentId,
            displayName = incidentName,
            description = $"{displayName}의 불만이 악화되면 발생하는 고유 사고.",
            mitigatingRoles = mitigatingRoles,
            triggerTags = relationTags
        };
        value.incidentName = incidentName;
        value.incidentDescription = value.incident.description;
        value.incidentMitigatingRoles = mitigatingRoles;
        value.combatPassive = new SpeciesPassiveDefinition
        {
            passiveId = $"species-passive:{value.speciesTag.ToLowerInvariant()}",
            displayName = $"{displayName} 본능",
            description = $"{displayName}의 운영 정체성을 전투에서 강화한다.",
            mechanicTags = defenseTags
        };
        value.combatAbilities.SetAbilities(new[]
        {
            new CharacterCombatAbilityDefinition(
                $"species-active:{value.speciesTag.ToLowerInvariant()}",
                activeName,
                $"{displayName} 종족 액티브",
                3,
                activeEffect is OffenseHealEffect or OffenseGuardEffect
                    ? OffenseBattleTargetRule.Ally
                    : OffenseBattleTargetRule.Enemy,
                activeEffect)
        });
    }

    private static SpeciesNeedProfile Needs(
        float hunger,
        float thirst,
        float sleep,
        float hygiene,
        float social,
        MealDietClass diet)
    {
        return new SpeciesNeedProfile
        {
            hungerRateMultiplier = hunger,
            thirstRateMultiplier = thirst,
            sleepRateMultiplier = sleep,
            hygieneRateMultiplier = hygiene,
            socialNeedMultiplier = social,
            diet = diet
        };
    }

    private static SpeciesEnvironmentProfile Environment(
        float comfortMinimum,
        float comfortMaximum,
        float safeMinimum,
        float safeMaximum,
        float lethalMinimum,
        float lethalMaximum,
        float airMinimum,
        float preferredHumidity = 0.5f)
    {
        return new SpeciesEnvironmentProfile
        {
            comfortMinimum = comfortMinimum,
            comfortMaximum = comfortMaximum,
            safeMinimum = safeMinimum,
            safeMaximum = safeMaximum,
            lethalMinimum = lethalMinimum,
            lethalMaximum = lethalMaximum,
            comfortableAirMinimum = airMinimum,
            comfortableLightMinimum = 40f,
            comfortableLightMaximum = 100f,
            preferredHumidity = preferredHumidity,
            drynessSensitivity = preferredHumidity > 0.5f ? 1.5f : 1f,
            airborneExposureMultiplier =
                airMinimum >= 30f && airMinimum <= 40f ? 0.5f : 1f
        };
    }
}

public interface ICharacterSpeciesCatalog
{
    IReadOnlyList<CharacterSpeciesSO> All { get; }
    bool TryGet(string speciesTag, out CharacterSpeciesSO species);
}

public sealed class ResourceCharacterSpeciesCatalog : ICharacterSpeciesCatalog
{
    public const string ResourcePath = "SO/Character/Species";

    private readonly IReadOnlyList<CharacterSpeciesSO> all;
    private readonly IReadOnlyDictionary<string, CharacterSpeciesSO> byTag;

    public ResourceCharacterSpeciesCatalog(IResourcesAssetLoader resources)
    {
        all = CharacterSpeciesExpansionDefaults.MergeWithFallbacks(
            resources?.LoadAllOptional<CharacterSpeciesSO>(ResourcePath));
        byTag = all
            .Where(value => !string.IsNullOrWhiteSpace(value.speciesTag))
            .GroupBy(value => value.speciesTag.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<CharacterSpeciesSO> All => all;

    public bool TryGet(string speciesTag, out CharacterSpeciesSO species)
    {
        return byTag.TryGetValue(speciesTag?.Trim() ?? string.Empty, out species);
    }
}

public static class CharacterSpeciesResourceLookup
{
    private static Dictionary<string, CharacterSpeciesSO> byTag;

    public static bool TryGet(string speciesTag, out CharacterSpeciesSO species)
    {
        EnsureLoaded();
        return byTag.TryGetValue(speciesTag?.Trim() ?? string.Empty, out species);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        byTag = null;
    }

    private static void EnsureLoaded()
    {
        if (byTag != null)
        {
            return;
        }

        byTag = CharacterSpeciesExpansionDefaults.MergeWithFallbacks(
                Resources.LoadAll<CharacterSpeciesSO>(
                    ResourceCharacterSpeciesCatalog.ResourcePath))
            .Where(value => !string.IsNullOrWhiteSpace(value.speciesTag))
            .OrderBy(value => value.id)
            .GroupBy(value => value.speciesTag.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.OrdinalIgnoreCase);
    }
}
