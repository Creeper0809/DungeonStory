#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V20MilestoneContentAssetBuilder
{
    private const string CatalogPath = "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string ItemCatalogPath = "Assets/Resources/SO/Content/ItemDefinitionCatalog.asset";
    private const string Root = "Assets/Resources/SO/V20/Milestones";

    private sealed class Spec
    {
        public string Id, Name, Description, LandmarkId, LandmarkName;
        public RunMilestoneTier Tier;
        public int BuildingNumericId;
        public V20WorldMetricKind Metric;
        public float Target;
        public int ResearchId;
        public string RequiredFlag;
        public (string Id, int Amount)[] Materials;
    }

    private static readonly Spec[] Specs =
    {
        S("ending:truth-revealed", "진실의 폭로", "진실 코어와 원정 기록을 결합해 던전의 기원을 세계에 증명한다.", "building:landmark:truth-observatory", "진실 관측소", RunMilestoneTier.Legacy, 9201, V20WorldMetricKind.DefeatedHumanBranches, 1, 0, "story:truth-core-secured", B(("material:stone-block",24),("material:steel-ingot",12),("component:precision-parts",6),("component:prototype-package",4))),
        S("ending:monster-accord", "괴물 대협약", "여섯 던전 세력의 이해관계를 조정해 상호 방위 협약을 성립시킨다.", "building:landmark:accord-hall", "대협약 회당", RunMilestoneTier.Legacy, 9202, V20WorldMetricKind.Population, 40, 0, "faction:all-six-allied", B(("material:treated-lumber",24),("material:stone-block",18),("material:steel-ingot",8),("material:paper",12))),
        S("ending:surface-hegemony", "지상 패권", "인간 다섯 전쟁 계통의 핵심 전력을 무너뜨리고 지상 진입로를 장악한다.", "building:landmark:surface-gate", "지상 패권문", RunMilestoneTier.Legacy, 9203, V20WorldMetricKind.DefeatedHumanBranches, 5, 0, "offense:surface-command-broken", B(("material:stone-block",30),("material:steel-ingot",16),("material:blacksteel-ingot",6),("component:machine-parts",6))),
        S("ending:dungeon-sovereignty", "던전 주권국", "인구·경제·생산·방어 체계를 갖춘 독립 국가로서 던전을 선포한다.", "building:landmark:sovereign-citadel", "주권 성채", RunMilestoneTier.Legacy, 9204, V20WorldMetricKind.DefenseReadiness, 80, 0, "economy:sovereign-ready", B(("material:stone-block",36),("material:steel-ingot",18),("component:machine-parts",8),("component:prototype-package",4))),
        S("ending:sealed-paradise", "봉인된 낙원", "외부 조달 없이 주민과 생산 시설을 장기간 유지하는 폐쇄 생태계를 완성한다.", "building:landmark:sealed-garden", "봉인 생태정원", RunMilestoneTier.Legacy, 9205, V20WorldMetricKind.SelfSufficiencyDays, 120, 7255, "ecology:closed-cycle", B(("material:treated-lumber",30),("material:stone-block",18),("resource:clean-water",24),("component:machine-parts",6))),
        S("ending:eternal-lineage", "영원한 계보", "세 세대가 직업과 장비 계보를 이어 던전의 살아 있는 역사를 만든다.", "building:landmark:lineage-vault", "영원 계보전", RunMilestoneTier.Grand, 9206, V20WorldMetricKind.CompletedGenerations, 3, 7240, "lineage:three-generations", B(("material:stone-block",30),("material:steel-ingot",16),("component:precision-parts",8),("component:rune-conductor",4))),
        S("ending:timeless-sanctuary", "시간 없는 성역", "실제 인구가 거주하는 시간 고정망을 장기 가동해 노화 없는 구역을 유지한다.", "building:landmark:temporal-sanctum", "시간 고정 성소", RunMilestoneTier.Grand, 9207, V20WorldMetricKind.Population, 60, 7271, "temporal:population-sustained", B(("material:stone-block",40),("material:steel-ingot",20),("component:precision-parts",10),("component:rune-conductor",10),("resource:mana-crystal",12))),
        S("ending:arcane-ascension", "비전 승천", "룬 전력망과 비전 생산을 통합해 물질과 마력의 경계를 산업 체계로 바꾼다.", "building:landmark:arcane-spire", "비전 승천탑", RunMilestoneTier.Grand, 9208, V20WorldMetricKind.RunePower, 100, 7238, "arcane:grid-integrated", B(("material:stone-block",36),("material:steel-ingot",18),("component:rune-conductor",12),("resource:mana-crystal",16),("component:prototype-package",6))),
        S("ending:steel-apotheosis", "강철 신격화", "자동 생산·자가 정비·통합 방어가 사람의 개입 없이 순환하는 산업 신체를 완성한다.", "building:landmark:steel-colossus", "강철 신격상", RunMilestoneTier.Grand, 9209, V20WorldMetricKind.ProductionAutomation, 100, 7244, "industry:self-maintaining", B(("material:stone-block",32),("material:steel-ingot",30),("material:blacksteel-ingot",12),("component:machine-parts",12),("component:precision-parts",8)))
    };

    [MenuItem("DungeonStory/V20/Build Milestones And Landmarks (18)")]
    public static void Build()
    {
        if (Specs.Length != 9 || Specs.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != 9
            || Specs.Select(value => value.LandmarkId).Distinct(StringComparer.Ordinal).Count() != 9)
            throw new InvalidOperationException("V20 milestone manifest must contain exactly nine unique milestone/landmark pairs.");

        EnsureFolder("Assets/Resources/SO/V20", "Milestones");
        EnsureFolder(Root, "Endings");
        EnsureFolder(Root, "Landmarks");
        GameDomainContentCatalogSO catalog = AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Domain content catalog is missing.");
        ItemDefinitionCatalogSO itemCatalog = AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalogSO>(ItemCatalogPath)
            ?? throw new InvalidOperationException("Item definition catalog is missing.");
        HashSet<string> itemIds = itemCatalog.Definitions.Where(value => value != null)
            .Select(value => value.ItemId).ToHashSet(StringComparer.Ordinal);

        List<EndingDefinitionSO> endings = Specs.Select(CreateEnding).ToList();
        List<BuildingSO> landmarks = Specs.Select(value => CreateLandmark(value, itemIds)).ToList();
        List<string> errors = endings.SelectMany(value => value.ValidateDefinition()).ToList();
        foreach (Spec spec in Specs)
        {
            if (!landmarks.Any(value => value.ContentDefinitionId == spec.LandmarkId))
                errors.Add($"Milestone '{spec.Id}' has no physical landmark '{spec.LandmarkId}'.");
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" | ", errors));

        HashSet<string> ownedLandmarks = Specs.Select(value => value.LandmarkId).ToHashSet(StringComparer.Ordinal);
        catalog.SetDefinitions(catalog.Definitions
            .Where(value => value != null
                && value is not EndingDefinitionSO
                && !(value is BuildingSO building && ownedLandmarks.Contains(building.ContentDefinitionId)))
            .Concat(endings)
            .Concat(landmarks));
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("V20_MILESTONE_CONTENT=PASS; milestones=9; landmarks=9; netNew=18; v20NetNewTotal=450");
    }

    private static EndingDefinitionSO CreateEnding(Spec spec)
    {
        EndingDefinitionSO value = Asset<EndingDefinitionSO>($"{Root}/Endings/{File(spec.Id)}.asset");
        value.ConfigureMetadata(spec.Id, spec.Name, spec.Description, 1, "V20 hand-authored non-terminal run milestone.");
        value.tier = spec.Tier;
        value.landmarkBuildingId = spec.LandmarkId;
        value.completionRequirements = new V20ContentRequirementSet
        {
            research = spec.ResearchId > 0
                ? new List<V20ResearchRequirement> { new() { researchNumericId = spec.ResearchId } }
                : new List<V20ResearchRequirement>(),
            worldMetrics = new List<V20WorldMetricRequirement> { new() { kind = spec.Metric, minimumValue = spec.Target } },
            requiredFlags = new List<string> { spec.RequiredFlag }
        };
        value.permanentRewards = new List<V20ContentEffect>
        {
            new() { kind = V20ContentEffectKind.WorldFlag, targetId = "reward:" + spec.Id, amount = 1 }
        };
        value.counterPressures = new List<V20ContentEffect>
        {
            new() { kind = V20ContentEffectKind.MilestonePressure, targetId = "pressure:" + spec.Id, amount = spec.Tier == RunMilestoneTier.Grand ? 2 : 1 }
        };
        EditorUtility.SetDirty(value);
        return value;
    }

    private static BuildingSO CreateLandmark(Spec spec, ISet<string> itemIds)
    {
        BuildingSO value = Asset<BuildingSO>($"{Root}/Landmarks/{File(spec.LandmarkId)}.asset");
        value.id = spec.BuildingNumericId;
        value.objectName = spec.LandmarkName;
        value.ConfigureAuthoredContentIdentity(spec.LandmarkId, 1, "V20 hand-authored milestone landmark.");
        value.width = 3;
        value.height = 3;
        value.layer = GridLayer.Building;
        value.category = BuildingCategory.Special;
        value.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
        value.unlocked = false;
        BuildingAbilityCollection abilities = new();
        BuildingWorkAmountAbility work = new()
        {
            constructionWorkRequired = spec.Tier == RunMilestoneTier.Grand ? 960f : 420f,
            repairWorkRequired = spec.Tier == RunMilestoneTier.Grand ? 180f : 90f,
            cleanWorkRequired = 24f,
            researchWorkRequired = 1f,
            operateWorkRequired = 1f
        };
        work.SetConstructionMaterials(spec.Materials.Select(material =>
            new ItemAmountDefinition(material.Id, material.Amount)));
        work.ValidateConstructionMaterialsOrThrow(itemIds.Contains);
        abilities.Add(work);
        abilities.Add(new BuildingStructuralIntegrityAbility
        {
            maxHitPoints = spec.Tier == RunMilestoneTier.Grand ? 2400f : 1400f,
            toughness = spec.Tier == RunMilestoneTier.Grand ? 60f : 38f,
            repairHitPointsPerWork = 3f,
            breachable = true
        });
        abilities.EnsureStableIds();
        value.ReplaceAbilities(abilities);
        value.ValidateAbilitiesOrThrow();
        EditorUtility.SetDirty(value);
        return value;
    }

    private static Spec S(string id, string name, string description, string landmarkId, string landmarkName,
        RunMilestoneTier tier, int buildingNumericId, V20WorldMetricKind metric, float target,
        int researchId, string requiredFlag, (string Id, int Amount)[] materials) => new()
    {
        Id = id, Name = name, Description = description, LandmarkId = landmarkId,
        LandmarkName = landmarkName, Tier = tier, BuildingNumericId = buildingNumericId,
        Metric = metric, Target = target, ResearchId = researchId, RequiredFlag = requiredFlag,
        Materials = materials
    };

    private static (string Id, int Amount)[] B(params (string Id, int Amount)[] values) => values;
    private static string File(string id) => id.Replace(':', '_');
    private static T Asset<T>(string path) where T : ScriptableObject
    {
        UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
        if (existing != null && existing is not T) throw new InvalidOperationException($"Wrong asset type at '{path}'.");
        if (existing is T typed) return typed;
        T value = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(value, path);
        return value;
    }
    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
