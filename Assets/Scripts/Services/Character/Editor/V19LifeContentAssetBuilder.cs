#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V19LifeContentAssetBuilder
{
    private const string DomainCatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string RootFolder = "Assets/Resources/SO/Population";
    private const string LifeFolder = RootFolder + "/Life";
    private const string ReproductionFolder = RootFolder + "/Reproduction";
    private const string FuneralFolder = RootFolder + "/Funeral";
    private const string FestivalFolder = RootFolder + "/Festivals";
    private const string CareerFolder = RootFolder + "/Careers";
    private const string ConditionFolder = RootFolder + "/AgeConditions";

    private sealed class SpeciesManifest
    {
        public string Tag;
        public int Infant;
        public int Adolescent;
        public int Adult;
        public int Elder;
        public float Expected;
        public ReproductionMode Mode;
        public string FuneralName;
        public string FuneralFacilityTag;
        public ReproductionPhaseDefinition[] Phases;
        public bool Construct;
    }

    private static readonly SpeciesManifest[] Species =
    {
        New("Slime", 1, 6, 8, 42, 74.2f, ReproductionMode.CoreDivision,
            "용해지", "facility:funeral:dissolution-pool",
            Phase(ReproductionPhaseKind.BiomassAccumulation, 15),
            Phase(ReproductionPhaseKind.CoreDivision, 5),
            Phase(ReproductionPhaseKind.Stabilization, 10)),
        New("Orc", 2, 11, 14, 45, 77.2f, ReproductionMode.Pregnancy,
            "무기 철야·화장", "facility:funeral:orc-vigil",
            Phase(ReproductionPhaseKind.Pregnancy, 40),
            Phase(ReproductionPhaseKind.Delivery, 1),
            Phase(ReproductionPhaseKind.Recovery, 20)),
        New("Vampire", 3, 14, 18, 130, 162.2f, ReproductionMode.Pregnancy,
            "혈향 촛불", "facility:funeral:blood-incense",
            Phase(ReproductionPhaseKind.Pregnancy, 60),
            Phase(ReproductionPhaseKind.Delivery, 1),
            Phase(ReproductionPhaseKind.Recovery, 30)),
        New("Beastkin", 2, 11, 14, 48, 80.2f, ReproductionMode.Pregnancy,
            "무리 장례", "facility:funeral:pack-farewell",
            Phase(ReproductionPhaseKind.Pregnancy, 32),
            Phase(ReproductionPhaseKind.Delivery, 1),
            Phase(ReproductionPhaseKind.Recovery, 15)),
        New("Demon", 3, 14, 18, 85, 117.2f, ReproductionMode.Pregnancy,
            "계약 소각", "facility:funeral:contract-burning",
            Phase(ReproductionPhaseKind.Pregnancy, 50),
            Phase(ReproductionPhaseKind.Delivery, 1),
            Phase(ReproductionPhaseKind.Recovery, 25)),
        New("Kobold", 1, 8, 10, 38, 70.2f, ReproductionMode.Egg,
            "도구 매장", "facility:funeral:tool-burial",
            Phase(ReproductionPhaseKind.EggFormation, 10),
            Phase(ReproductionPhaseKind.Incubation, 20),
            Phase(ReproductionPhaseKind.Recovery, 10)),
        New("Myconid", 1, 4, 6, 30, 62.2f, ReproductionMode.Spore,
            "포자 정원", "facility:funeral:spore-garden",
            Phase(ReproductionPhaseKind.SporeMixing, 5),
            Phase(ReproductionPhaseKind.MycelialExpansion, 20),
            Phase(ReproductionPhaseKind.Fruiting, 10)),
        New("Harpy", 2, 11, 14, 52, 84.2f, ReproductionMode.Egg,
            "하늘장", "facility:funeral:sky-burial",
            Phase(ReproductionPhaseKind.EggFormation, 12),
            Phase(ReproductionPhaseKind.Incubation, 24),
            Phase(ReproductionPhaseKind.Recovery, 12)),
        New("Golem", -1, -1, 0, 90, 122.2f, ReproductionMode.GolemAssembly,
            "핵 안치", "facility:funeral:core-rest",
            Phase(ReproductionPhaseKind.FrameAssembly, 15),
            Phase(ReproductionPhaseKind.CoreInscription, 10),
            Phase(ReproductionPhaseKind.PersonalityInscription, 10),
            Phase(ReproductionPhaseKind.Activation, 5),
            construct: true),
        New("Adventurer", 3, 13, 18, 55, 87.2f, ReproductionMode.Pregnancy,
            "Adventurer burial", "facility:funeral:adventurer-burial",
            Phase(ReproductionPhaseKind.Pregnancy, 40),
            Phase(ReproductionPhaseKind.Delivery, 1),
            Phase(ReproductionPhaseKind.Recovery, 20))
    };

    [MenuItem("DungeonStory/V19/Build Life Content")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources/SO", "Population");
        EnsureFolder(RootFolder, "Life");
        EnsureFolder(RootFolder, "Reproduction");
        EnsureFolder(RootFolder, "Funeral");
        EnsureFolder(RootFolder, "Festivals");
        EnsureFolder(RootFolder, "Careers");
        EnsureFolder(RootFolder, "AgeConditions");

        GameDomainContentCatalogSO domain = AssetDatabase.LoadAssetAtPath<
            GameDomainContentCatalogSO>(DomainCatalogPath);
        if (domain == null)
        {
            throw new InvalidOperationException(
                $"Required domain catalog is missing at '{DomainCatalogPath}'.");
        }

        EnsureAdventurerSpecies(domain);

        CharacterSO[] archetypes = domain
            .GetAll<CharacterSO>()
            .Where(value => value != null)
            .OrderBy(value => value.id)
            .ToArray();
        IGrouping<int, CharacterSO> duplicateArchetype = archetypes
            .GroupBy(value => value.id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateArchetype != null)
        {
            throw new InvalidOperationException(
                $"Duplicate character archetype numeric ID '{duplicateArchetype.Key}'.");
        }

        foreach (CharacterSO archetype in archetypes)
        {
            archetype.ConfigureStableIdentity(
                $"character-archetype:{archetype.id}",
                $"visual-variant:{archetype.id}");
            EditorUtility.SetDirty(archetype);
        }

        Dictionary<string, CharacterSpeciesSO> speciesByTag = domain
            .GetAll<CharacterSpeciesSO>()
            .ToDictionary(value => value.speciesTag, StringComparer.Ordinal);
        List<ScriptableObject> created = new();
        foreach (SpeciesManifest manifest in Species)
        {
            if (!speciesByTag.TryGetValue(manifest.Tag, out CharacterSpeciesSO species))
            {
                throw new InvalidOperationException(
                    $"Authored species '{manifest.Tag}' is missing from the root catalog.");
            }

            SpeciesLifeHistorySO life = CreateRequired<SpeciesLifeHistorySO>(
                $"{LifeFolder}/Life_{manifest.Tag}.asset");
            life.definitionId = $"life-history:{manifest.Tag.ToLowerInvariant()}";
            life.speciesTag = manifest.Tag;
            life.infantEndAgeYears = manifest.Infant;
            life.adolescentStartAgeYears = manifest.Adolescent;
            life.adultAgeYears = manifest.Adult;
            life.elderAgeYears = manifest.Elder;
            life.untreatedExpectedLifeYears = manifest.Expected;
            life.construct = manifest.Construct;

            ReproductionProfileSO reproduction = CreateRequired<ReproductionProfileSO>(
                $"{ReproductionFolder}/Reproduction_{manifest.Tag}.asset");
            reproduction.definitionId = $"reproduction:{manifest.Tag.ToLowerInvariant()}";
            reproduction.speciesTag = manifest.Tag;
            reproduction.mode = manifest.Mode;
            reproduction.baseSuccessChance = 0.35f;
            reproduction.viableTemperatureMinimum = manifest.Construct ? 0f : 10f;
            reproduction.viableTemperatureMaximum = manifest.Construct ? 45f : 32f;
            reproduction.phases = manifest.Phases.ToList();

            FuneralCultureSO funeral = CreateRequired<FuneralCultureSO>(
                $"{FuneralFolder}/Funeral_{manifest.Tag}.asset");
            funeral.cultureId = $"funeral-culture:{manifest.Tag.ToLowerInvariant()}";
            funeral.speciesTag = manifest.Tag;
            funeral.ritualName = manifest.FuneralName;
            funeral.requiredFacilityTag = manifest.FuneralFacilityTag;

            species.lifeHistory = life;
            species.reproduction = reproduction;
            species.funeralCulture = funeral;
            EditorUtility.SetDirty(life);
            EditorUtility.SetDirty(reproduction);
            EditorUtility.SetDirty(funeral);
            EditorUtility.SetDirty(species);
            created.Add(life);
            created.Add(reproduction);
            created.Add(funeral);
        }

        created.Add(CreateFestival(
            "festival:sprout",
            "새싹제",
            Season.Spring,
            15,
            convertsActiveGrief: false));
        created.Add(CreateFestival(
            "festival:high-sun",
            "고일제",
            Season.Summer,
            15,
            convertsActiveGrief: false));
        created.Add(CreateFestival(
            "festival:storage",
            "저장제",
            Season.Autumn,
            25,
            convertsActiveGrief: false));
        created.Add(CreateFestival(
            "festival:long-night-memorial",
            "긴밤 추모제",
            Season.Winter,
            30,
            convertsActiveGrief: true));

        created.Add(CreateCareerPosition(
            "career-position:steward", "관리인",
            CareerPositionKind.Steward, CareerPositionScopeKind.Global));
        created.Add(CreateCareerPosition(
            "career-position:chief-researcher", "수석 연구원",
            CareerPositionKind.ChiefResearcher, CareerPositionScopeKind.Global));
        created.Add(CreateCareerPosition(
            "career-position:chief-physician", "수석 의사",
            CareerPositionKind.ChiefPhysician, CareerPositionScopeKind.Global));
        created.Add(CreateCareerPosition(
            "career-position:guard-captain", "경비대장",
            CareerPositionKind.GuardCaptain, CareerPositionScopeKind.Global));
        created.Add(CreateCareerPosition(
            "career-position:foreman", "작업반장",
            CareerPositionKind.Foreman, CareerPositionScopeKind.Facility,
            "research-overhaul"));
        created.Add(CreateCareerPosition(
            "career-position:mentor", "멘토",
            CareerPositionKind.Mentor, CareerPositionScopeKind.Facility,
            "workstation:v19:mentor-academy"));

        created.Add(CreateAgeCondition(
            "age-cardiac-degeneration", "심장 기능 퇴행", false, "heart", "core"));
        created.Add(CreateAgeCondition(
            "age-neural-degeneration", "신경 기능 퇴행", false, "brain", "sensory-gel", "hypha-core", "mana-core"));
        created.Add(CreateAgeCondition(
            "age-organ-fibrosis", "장기 섬유화", false, "liver", "kidney:left", "kidney:right", "spore-sac"));
        created.Add(CreateAgeCondition(
            "core-corrosion", "핵 부식", true, "power-core"));
        created.Add(CreateAgeCondition(
            "rune-circuit-wear", "룬 회로 마모", true, "sensor-core"));
        created.Add(CreateAgeCondition(
            "frame-fatigue", "골격 피로", true, "torso", "arm:left", "arm:right", "leg:left", "leg:right"));

        domain.SetDefinitions(domain.Definitions.Concat(created));
        EditorUtility.SetDirty(domain);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        IReadOnlyList<string> catalogErrors = domain.ValidateCatalog();
        if (catalogErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "V19 life content failed root catalog validation: "
                + string.Join(" | ", catalogErrors));
        }

        Debug.Log(
            $"V19_LIFE_CONTENT=PASS; species={Species.Length}; definitions={created.Count}");
    }

    private static FestivalDefinitionSO CreateFestival(
        string festivalId,
        string displayName,
        Season season,
        int dayOfSeason,
        bool convertsActiveGrief)
    {
        string fileName = festivalId.Replace(':', '_');
        FestivalDefinitionSO festival = CreateRequired<FestivalDefinitionSO>(
            $"{FestivalFolder}/{fileName}.asset");
        festival.festivalId = festivalId;
        festival.displayName = displayName;
        festival.season = season;
        festival.dayOfSeason = dayOfSeason;
        festival.convertsActiveGrief = convertsActiveGrief;
        EditorUtility.SetDirty(festival);
        return festival;
    }

    private static CareerPositionDefinitionSO CreateCareerPosition(
        string definitionId,
        string displayName,
        CareerPositionKind position,
        CareerPositionScopeKind scope,
        string requiredFacilityTag = "")
    {
        string fileName = definitionId.Replace(':', '_');
        CareerPositionDefinitionSO definition =
            CreateRequired<CareerPositionDefinitionSO>(
                $"{CareerFolder}/{fileName}.asset");
        definition.definitionId = definitionId;
        definition.displayName = displayName;
        definition.position = position;
        definition.scope = scope;
        definition.maximumOccupants = 1;
        definition.requiredFacilityTag = requiredFacilityTag;
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static T CreateRequired<T>(string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            if (MonoScript.FromScriptableObject(existing) != null)
            {
                return existing;
            }

            if (!path.StartsWith(RootFolder + "/", StringComparison.Ordinal)
                || !AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException(
                    $"Broken V19 content asset '{path}' could not be replaced.");
            }
        }

        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            if (!path.StartsWith(RootFolder + "/", StringComparison.Ordinal)
                || !AssetDatabase.DeleteAsset(path))
            {
                throw new InvalidOperationException(
                    $"Asset path '{path}' is occupied by a different type.");
            }
        }

        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static AgeConditionDefinitionSO CreateAgeCondition(
        string suffix,
        string displayName,
        bool construct,
        params string[] nodes)
    {
        AgeConditionDefinitionSO condition = CreateRequired<AgeConditionDefinitionSO>(
            $"{ConditionFolder}/AgeCondition_{suffix}.asset");
        condition.conditionId = "condition:" + suffix;
        condition.displayName = displayName;
        condition.constructCondition = construct;
        condition.affectedAnatomyNodeIds = nodes.ToList();
        EditorUtility.SetDirty(condition);
        return condition;
    }

    private static SpeciesManifest New(
        string tag,
        int infant,
        int adolescent,
        int adult,
        int elder,
        float expected,
        ReproductionMode mode,
        string funeral,
        string funeralFacility,
        ReproductionPhaseDefinition phase1,
        ReproductionPhaseDefinition phase2,
        ReproductionPhaseDefinition phase3,
        ReproductionPhaseDefinition phase4 = null,
        bool construct = false) => new()
    {
        Tag = tag,
        Infant = infant,
        Adolescent = adolescent,
        Adult = adult,
        Elder = elder,
        Expected = expected,
        Mode = mode,
        FuneralName = funeral,
        FuneralFacilityTag = funeralFacility,
        Phases = new[] { phase1, phase2, phase3, phase4 }
            .Where(value => value != null)
            .ToArray(),
        Construct = construct
    };

    private static ReproductionPhaseDefinition Phase(
        ReproductionPhaseKind phase,
        int days) => new()
    {
        phase = phase,
        durationDays = days
    };

    private static void EnsureAdventurerSpecies(
        GameDomainContentCatalogSO domain)
    {
        const string path =
            "Assets/Resources/SO/Character/Species/Species_Adventurer.asset";
        CharacterSpeciesSO species = AssetDatabase.LoadAssetAtPath<
            CharacterSpeciesSO>(path);
        if (species == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                throw new InvalidOperationException(
                    $"Asset path '{path}' is occupied by a different type.");
            }

            species = ScriptableObject.CreateInstance<CharacterSpeciesSO>();
            AssetDatabase.CreateAsset(species, path);
        }

        species.id = 10;
        species.speciesTag = "Adventurer";
        species.displayName = "원정자";
        species.ownerSelectionPolicy = SpeciesOwnerSelectionPolicy.NpcOnly;
        species.homeFactionId = "faction:adventurer";
        species.anatomyProfileId = "anatomy:humanoid";
        species.needs = new SpeciesNeedProfile();
        species.environment = new SpeciesEnvironmentProfile();
        species.relationTags = new[] { "외부인", "침입자" };
        species.defenseAffinityTags = new[] { "물리" };
        species.strongWorkTypeIds = Array.Empty<string>();
        species.weakWorkTypeIds = Array.Empty<string>();
        species.shortDescription = "던전을 침입하는 외부 원정자";
        species.description =
            "던전 인구의 번식 대상이 아닌 적대적 외부 원정자 종족 정의다.";
        species.stayDurationMultiplier = 1f;
        species.crimeRiskMultiplier = 1f;
        species.incidentType = CharacterSpeciesIncidentType.None;
        species.incident = new SpeciesIncidentDefinition
        {
            incidentId = "species-incident:adventurer-incursion",
            displayName = "원정자 침입",
            description = "외부 원정자가 던전에 침입했다.",
            mitigatingRoles = FacilityRole.Security,
            triggerTags = new[] { "invasion" }
        };
        species.combatPassive = new SpeciesPassiveDefinition
        {
            passiveId = "species-passive:adventurer",
            displayName = "원정 숙련",
            description = "던전 침투와 전투에 익숙하다.",
            mechanicTags = new[] { "invasion", "combat" }
        };
        species.statBonus = new CharacterStatBlock();
        species.modifiers = new CharacterModelModifiers();
        EditorUtility.SetDirty(species);

        if (!domain.Definitions.Contains(species))
        {
            domain.SetDefinitions(domain.Definitions.Append(species));
            EditorUtility.SetDirty(domain);
        }

        CharacterSO[] adventurerArchetypes = domain.GetAll<CharacterSO>()
            .Where(value => value != null
                && string.Equals(
                    value.speciesTag,
                    "Adventurer",
                    StringComparison.Ordinal))
            .ToArray();
        if (adventurerArchetypes.Length != 1)
        {
            throw new InvalidOperationException(
                "Exactly one authored Adventurer character archetype is required.");
        }

        adventurerArchetypes[0].species = species;
        EditorUtility.SetDirty(adventurerArchetypes[0]);
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
