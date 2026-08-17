#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ResearchEquipmentOverhaulDebugScenarios
{
    private const string LongswordResearchId =
        "research:equipment:weapon-patterns";

    private const string ProjectRoot = "Assets/Resources/SO/Research/Projects";
    private const string FacilityRoot = "Assets/Resources/SO/Building/ResearchOverhaul";
    private const string ItemRoot = "Assets/Resources/SO/Economy/Items/ResearchOverhaul";
    private const string RecipeRoot = "Assets/Resources/SO/Economy/Recipes/ResearchOverhaul";
    private const string ModuleRoot = "Assets/Resources/SO/Combat/EquipmentModules";
    private const string AppraisalFacilityPath =
        FacilityRoot + "/RF42_부품_감정대.asset";
    private const string RestorationFacilityPath =
        FacilityRoot + "/RF43_부품_복원_작업대.asset";
    private const string PrecisionFittingFacilityPath =
        FacilityRoot + "/RF44_정밀_장착대.asset";
    private const string WrongProgressionFacilityPath =
        "Assets/Resources/SO/Building/Modular/S08_대장작업대.asset";
    private const float EffectiveWorkPerDay =
        SettlementLaborAuthority.EffectiveOutputWuPerAdultDay;

    private static readonly string[] MedievalQueue =
    {
        "research:agriculture:indoor",
        "research:metallurgy:advanced",
        "research:textile:layered",
        "research:cuisine:livestock",
        "research:defense:tactical-command",
        "research:survival:field-rations",
        "research:medical:surgery"
    };

    private static readonly string[] EarlyIndustrialQueue =
    {
        "research:industry:steam-power",
        "research:industry:distribution",
        "research:industry:factory-layout",
        "research:equipment:black-powder",
        "research:equipment:engineering-drawing",
        "research:industry:powered-tools",
        "research:equipment:ignition-mechanisms",
        "research:equipment:ballistics",
        "research:equipment:standard-ammunition"
    };

    private static readonly string[] MatureIndustrialQueue =
    {
        "research:industry:high-speed-belts",
        "research:industry:precision",
        "research:equipment:precision-fitting",
        "research:industry:industrial-cooling"
    };

    private static readonly string[] LateIndustrialQueue =
    {
        "research:industry:rune-automation",
        "research:industry:dark-foundry",
        "research:plumbing:rune-purification",
        "research:equipment:rune-module-tuning",
        "research:equipment:lineage-binding",
        "research:equipment:powered-armor",
        "research:equipment:industrial-metrology"
    };

    private static readonly HashSet<string> V21AmmunitionIds = new(StringComparer.Ordinal)
    {
        "ammo:incendiary-arrow",
        "ammo:incendiary-bolt",
        "ammo:smoke-cartridge",
        "ammo:armor-piercing-cartridge",
        "ammo:scatter-cartridge",
        "ammo:signal-flare",
        "ammo:blacksteel-bolt",
        "ammo:rune-cartridge",
        "ammo:tranquilizer-dart",
        "ammo:mana-disruptor-bolt"
    };

    [MenuItem("Tools/DungeonStory/Research/Validate 180 Research Equipment Overhaul")]
    public static void RunFromMenu()
    {
        IReadOnlyList<string> failures = ValidateAll(out string pacingReport);
        if (failures.Count > 0)
        {
            foreach (string failure in failures)
            {
                Debug.LogError($"[180 Research Overhaul] {failure}");
            }
            throw new InvalidOperationException(
                $"180 research/equipment overhaul validation failed ({failures.Count}).");
        }

        Debug.Log($"180 research/equipment overhaul validation passed. {pacingReport}");
    }

    [MenuItem("Tools/DungeonStory/Research/Generate V21 Gameplay Connection Report")]
    public static void GenerateGameplayConnectionReport()
    {
        string outputPath = Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_CONNECTION_REPORT_PATH")
            ?.Trim();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = "docs/generated/V21_Gameplay_Connection_Report.md";
        }
        List<string> rows = new();
        AddConnectionRows<CharacterTraitSO>(rows, "일반 특성",
            value => value.DefinitionId.Value,
            "AI 효용·typed 반응·사건 가중치",
            "행동 선택·기분·사건 참여자/후보 가중치",
            "characters.narrative / characters.life");
        AddConnectionRows<HeritableTraitDefinitionSO>(rows, "유전 특성",
            value => value.traitId,
            "HeritableTraitRuntimeQuery",
            "환경·감염·가임·노화·필요·이동·마나 계산",
            "characters.narrative / characters.life");
        AddAuthoredRows<CharacterBackgroundDefinitionSO>(rows, "배경",
            "CharacterNarrativeRuntime.Register",
            "최초 XP·기억·관계·상태 적용",
            "characters.narrative");
        AddAuthoredRows<CharacterAmbitionDefinitionSO>(rows, "야망",
            "사회 사건·도메인 이벤트",
            "진행·실패·완료 보상",
            "characters.narrative");
        AddAuthoredRows<SpeciesCultureDefinitionSO>(rows, "문화",
            "식사·방·사건·관습 참여",
            "금기·선호·태도·동화",
            "characters.narrative");
        AddAuthoredRows<CulturalPracticeDefinitionSO>(rows, "문화 관습",
            "영속 알림 선택 디스패처",
            "실물 소비·성공/방치 효과·동화일",
            "characters.narrative / society.events");
        AddAuthoredRows<LifeEventDefinitionSO>(rows, "생애 사건",
            "IContentResolutionService",
            "typed 인물·관계·재화 효과",
            "characters.narrative / society.events");
        AddConnectionRows<FestivalDefinitionSO>(rows, "축제",
            value => value.StableId,
            "IFestivalCommand + 기능 알림",
            "준비품·참가자·성공/부분/실패",
            "society.events / characters.narrative");
        AddAuthoredRows<SeasonalWorldEventDefinitionSO>(rows, "계절 사건",
            "일일 캠페인 평가 + IContentResolutionService",
            "두 개 이상 도메인의 typed 효과",
            "world.seasonal-events / society.events");
        AddAuthoredRows<FactionChapterDefinitionSO>(rows, "세력 장",
            "기능 알림 선택 디스패처",
            "물품·시설·작업·관계 원자 처리",
            "factions.campaign");
        AddAuthoredRows<FactionContractDefinitionSO>(rows, "세력 계약",
            "기능 알림 선택 디스패처",
            "기한·물품·관계 원자 처리",
            "factions.campaign");
        AddAuthoredRows<GuestRequestDefinitionSO>(rows, "손님 요청",
            "기능 알림 선택 디스패처",
            "시설·물품·기한·거래 처리",
            "society.events / factions.campaign");
        AddAuthoredRows<ServiceIncidentDefinitionSO>(rows, "서비스 사고",
            "기능 알림 선택 디스패처",
            "대응별 인물·관계·재화 효과",
            "society.events / factions.campaign");

        ProductionRecipeSO[] recipes = Resources.LoadAll<ProductionRecipeSO>(
            ProductionRecipeSO.ResourcePath);
        AddConnectionRows<BuildingSO>(rows, "시설",
            value => value.ContentDefinitionId.Length > 0
                ? value.ContentDefinitionId
                : $"building:{value.id}",
            value => FacilityEntry(value, recipes),
            value => FacilityEffect(value, recipes),
            value => FacilitySaveOwner(value));
        AddConnectionRows<ResourceItemDefinitionSO>(rows, "물리 아이템",
            value => value.ItemId,
            "물리 아이템 예약·운반·내구/소비 그래프",
            "제작·건설·시술·행사·장전의 구체 소비",
            "items.world-stacks + owning aggregate");
        AddConnectionRows<CombatEquipmentDefinitionSO>(rows, "전투 장비",
            value => value.EquipmentId,
            "제작·노획·장비 장착",
            "역할형 전투 규칙·내구·무게·계보",
            "combat.equipment / items.world-stacks");
        AddConnectionRows<EnemyArchetypeDefinitionSO>(rows, "적 아키타입",
            value => value.stableId,
            "오펜스·디펜스 개인 생성",
            "물리 장비·전술·포획 동일성",
            "offense / invasion / captivity");
        AddConnectionRows<EnemyAbilityDefinitionSO>(rows, "적 능력",
            value => value.stableId,
            "전술 AI 의도 선택",
            "고유 능력 실행·상태·보스 단계",
            "offense / invasion");
        AddConnectionRows<OffenseEncounterSO>(rows, "전투 조우",
            value => value.encounterId,
            "원정/방어 조우 시작",
            "목표·환경·카운터·물리 전리품",
            "offense / invasion / items.world-stacks");
        AddConnectionRows<WildlifeSpeciesSO>(rows, "야생동물",
            value => value.SpeciesId,
            "일일 생태 시뮬레이션",
            "먹이망·둥지·번식·이동·질병 매개",
            "world.wildlife");
        AddConnectionRows<DiseaseDefinitionSO>(rows, "질병",
            value => value.stableId,
            "노출·진단·field response",
            "기관·작업·기분·행동·치료 효과",
            "characters.health");
        AddConnectionRows<CropGenomeDefinitionSO>(rows, "작물 품종",
            value => value.GenomeId,
            "종자 로트·파종·일일 성장",
            "6개 좌위의 온도·성장·질병·수확 계산",
            "economy.crop-plots / items.world-stacks");
        AddAuthoredRows<EndingDefinitionSO>(rows, "이정표",
            "실제 누적 기록 자동 평가",
            "랜드마크 잠금·영구 보상·신규 압력",
            "run.milestones");

        string[] invalid = rows.Where(value => value.Contains("||", StringComparison.Ordinal))
            .ToArray();
        if (invalid.Length > 0)
        {
            throw new InvalidOperationException(
                $"V21 connection report contains {invalid.Length} incomplete rows.");
        }
        StringBuilder report = new();
        report.AppendLine("# V21 실제 게임플레이 연결 보고서");
        report.AppendLine();
        report.AppendLine("> 생성 권위: `ResearchEquipmentOverhaulDebugScenarios.GenerateGameplayConnectionReport`");
        report.AppendLine("> 연결 기준: 정의 → 실행 입구 → 실제 효과 → 저장 소유자");
        report.AppendLine();
        report.AppendLine($"총 연결 행: **{rows.Count}**, 미연결: **0**");
        report.AppendLine();
        report.AppendLine("| 범주 | 정의 ID | 실행 입구 | 실제 효과 | 저장 소유자 |");
        report.AppendLine("|---|---|---|---|---|");
        foreach (string row in rows.OrderBy(value => value, StringComparer.Ordinal))
        {
            report.AppendLine(row);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllText(
            outputPath,
            report.ToString(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        AssetDatabase.Refresh();
        Debug.Log($"V21_GAMEPLAY_CONNECTION_REPORT=PASS; rows={rows.Count}; path={outputPath}");
    }

    public static IReadOnlyList<string> ValidateAll(out string pacingReport)
    {
        List<string> failures = new List<string>();
        ResearchProjectSO[] projects = LoadAssets<ResearchProjectSO>(ProjectRoot);
        CombatEquipmentDefinitionSO[] equipment = Resources
            .LoadAll<CombatEquipmentDefinitionSO>(ResourceCombatEquipmentCatalog.ResourcePath)
            .Where(item => item != null)
            .ToArray();
        EquipmentModuleDefinitionSO[] modules = LoadAssets<EquipmentModuleDefinitionSO>(ModuleRoot);

        ValidateResearchGraph(projects, failures);
        ValidateContentCounts(failures);
        ValidateContentEffectExecution(failures);
        ValidateResearchFacilityExecution(failures);
        ValidateRewards(projects, equipment, failures);
        ValidateEquipment(projects, equipment, modules, failures);
        ValidateRuntimeLocksModulesAndSave(failures);
        ValidateDeterministicDrops(failures);
        ValidatePacing(projects, failures, out pacingReport);
        return failures;
    }

    private static void ValidateResearchGraph(
        IReadOnlyList<ResearchProjectSO> projects,
        ICollection<string> failures)
    {
        Require(projects.Count == 180, $"research count {projects.Count}, expected 180", failures);
        Require(Mathf.Approximately(projects.Sum(project => project.RequiredWork), 63173f),
            $"research total work {projects.Sum(project => project.RequiredWork):0.##}, expected 63173",
            failures);
        Require(projects.Select(project => project.ProjectId.Value)
                .Distinct(StringComparer.Ordinal).Count() == projects.Count,
            "duplicate stable research ID", failures);
        Require(projects.Select(project => project.id).Distinct().Count() == projects.Count,
            "duplicate numeric research ID", failures);

        foreach (ResearchProjectSO project in projects)
        {
            foreach (string error in project.ValidateDefinition())
            {
                failures.Add(error);
            }
            Require(project.Prerequisites.Count <= 4,
                $"{project.ProjectId}: more than four direct prerequisites", failures);
            Require(project.PrerequisiteLinks.Count == project.Prerequisites.Count,
                $"{project.ProjectId}: causal link count mismatch", failures);
            foreach (ResearchPrerequisiteLink link in project.PrerequisiteLinks)
            {
                Require(link != null && link.IsValid,
                    $"{project.ProjectId}: invalid causal prerequisite link", failures);
            }
        }

        Dictionary<ResearchProjectSO, int> states = projects.ToDictionary(project => project, _ => 0);
        foreach (ResearchProjectSO project in projects)
        {
            if (HasCycle(project, states))
            {
                failures.Add($"research cycle reaches {project.ProjectId}");
                break;
            }
        }
    }

    private static bool HasCycle(
        ResearchProjectSO project,
        IDictionary<ResearchProjectSO, int> states)
    {
        if (!states.TryGetValue(project, out int state))
        {
            return false;
        }
        if (state == 1)
        {
            return true;
        }
        if (state == 2)
        {
            return false;
        }

        states[project] = 1;
        if (project.Prerequisites.Any(prerequisite => HasCycle(prerequisite, states)))
        {
            return true;
        }
        states[project] = 2;
        return false;
    }

    private static void ValidateContentCounts(ICollection<string> failures)
    {
        int rewardFacilityCount = LoadAssets<BuildingSO>(FacilityRoot).Length
            + LoadAssets<BuildingSO>(
                "Assets/Resources/SO/Building/V22Apparel").Length;
        Require(rewardFacilityCount == 115,
            $"research-linked facility count must be exactly 115, found {rewardFacilityCount}", failures);
        Require(LoadAssets<ResourceItemDefinitionSO>(ItemRoot).Length >= 30,
            "branched production item set is incomplete", failures);
        Require(LoadAssets<ProductionRecipeSO>(RecipeRoot).Length >= 29,
            "branched production recipe set is incomplete", failures);
        HashSet<string> authoredAmmunition = LoadAssets<ResourceItemDefinitionSO>(ItemRoot)
            .Where(item => item.Kind == ResourceItemKind.Ammunition
                && V21AmmunitionIds.Contains(item.ItemId))
            .Select(item => item.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        Require(authoredAmmunition.SetEquals(V21AmmunitionIds),
            "V21 physical ammunition set differs: "
            + string.Join(", ", authoredAmmunition.OrderBy(id => id, StringComparer.Ordinal)),
            failures);
    }

    private static void ValidateContentEffectExecution(
        ICollection<string> failures)
    {
        foreach (V20ContentEffectKind kind in Enum
                     .GetValues(typeof(V20ContentEffectKind))
                     .Cast<V20ContentEffectKind>()
                     .Where(value => value != V20ContentEffectKind.None))
        {
            Require(
                V21ContentEffectExecutionRegistry.HasExecutionOwner(kind),
                $"content effect {kind} has no typed command/save owner",
                failures);
        }

        List<V20ContentEffect> costs = new()
        {
            new V20ContentEffect
            {
                kind = V20ContentEffectKind.ItemConsume,
                targetId = "qa:item:a",
                amount = 3
            },
            new V20ContentEffect
            {
                kind = V20ContentEffectKind.ItemConsume,
                targetId = "qa:item:b",
                amount = 2
            }
        };
        List<WorldItemStackSnapshot> oneItemShort = new()
        {
            new WorldItemStackSnapshot
            {
                StackId = "qa:stack:a",
                ItemId = "qa:item:a",
                Quantity = 3
            },
            new WorldItemStackSnapshot
            {
                StackId = "qa:stack:b",
                ItemId = "qa:item:b",
                Quantity = 1
            }
        };
        bool failedWithoutPlan = !V21ContentEffectCommitPreflight
            .TryPlanItemCosts(
                costs,
                oneItemShort,
                out IReadOnlyList<ReservedItemConsumption> failedCosts,
                out string missingItemId)
            && failedCosts.Count == 0
            && string.Equals(
                missingItemId,
                "qa:item:b",
                StringComparison.Ordinal)
            && oneItemShort[0].Quantity == 3
            && oneItemShort[1].Quantity == 1
            && oneItemShort[0].AvailableQuantity > 0
            && oneItemShort[1].AvailableQuantity > 0;
        Require(
            failedWithoutPlan,
            "last required item failure mutated stock or leaked a partial commit plan",
            failures);

        oneItemShort[1].Quantity = 2;
        bool completePlan = V21ContentEffectCommitPreflight.TryPlanItemCosts(
                costs,
                oneItemShort,
                out IReadOnlyList<ReservedItemConsumption> planned,
                out missingItemId)
            && missingItemId.Length == 0
            && planned.Count == 2
            && planned.Sum(value => value.Quantity) == 5;
        Require(
            completePlan,
            "complete physical requirements did not produce one deterministic atomic plan",
            failures);
    }

    private static void ValidateResearchFacilityExecution(
        ICollection<string> failures)
    {
        BuildingSO[] facilities = LoadAssets<BuildingSO>(FacilityRoot);
        ProductionRecipeSO[] recipes = Resources.LoadAll<ProductionRecipeSO>(
            ProductionRecipeSO.ResourcePath);
        int recipeExecutors = 0;
        int commandExecutors = 0;
        foreach (BuildingSO facility in facilities)
        {
            if (facility.UseClassification == FacilityUseClassification.None)
            {
                failures.Add($"{facility.id}: missing gameplay use classification");
            }

            string workstation = facility.GetProductionWorkstationAbility()
                ?.WorkstationTag ?? string.Empty;
            bool hasRecipe = workstation.Length > 0
                && recipes.Any(recipe => string.Equals(
                    recipe.WorkstationTag,
                    workstation,
                    StringComparison.Ordinal));
            bool hasCommand = facility.ResearchFacilityCommand
                != ResearchFacilityCommandKind.None;
            if (hasRecipe)
            {
                recipeExecutors++;
            }
            if (hasCommand)
            {
                commandExecutors++;
                if (!ResearchFacilityCommandConsumerRegistry.HasExecutionContract(
                        facility.ResearchFacilityCommand))
                {
                    failures.Add(
                        $"{facility.id}: command {facility.ResearchFacilityCommand} has no runtime owner");
                }
            }
            if (!hasRecipe && !hasCommand)
            {
                failures.Add(
                    $"{facility.id}: no production recipe or typed command executor");
            }
        }

        Require(recipeExecutors == 63,
            $"research facility recipe executors {recipeExecutors}, expected 63",
            failures);
        Require(commandExecutors == 38,
            $"research facility command executors {commandExecutors}, expected 38",
            failures);
        Require(Enum.GetValues(typeof(ResearchFacilityCommandKind))
                .Cast<ResearchFacilityCommandKind>()
                .Where(value => value != ResearchFacilityCommandKind.None)
                .All(ResearchFacilityCommandConsumerRegistry.HasExecutionContract),
            "one or more typed facility command values have no runtime owner",
            failures);
    }

    private static void ValidateRewards(
        ResearchProjectSO[] projects,
        CombatEquipmentDefinitionSO[] equipment,
        ICollection<string> failures)
    {
        ResourceResearchProjectCatalog research = new ResourceResearchProjectCatalog(projects);
        BuildingSO[] buildings = AssetDatabase.FindAssets(
                "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
        ResourceItemDefinitionSO[] items = AssetDatabase.FindAssets("t:ResourceItemDefinitionSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
            .Where(item => item != null)
            .ToArray();
        ProductionRecipeSO[] recipes = Resources.LoadAll<ProductionRecipeSO>(
            ProductionRecipeSO.ResourcePath);
        ResourceEconomyContentCatalog economy = new ResourceEconomyContentCatalog(
            items,
            recipes,
            Resources.LoadAll<CropDefinitionSO>(CropDefinitionSO.ResourcePath),
            Resources.LoadAll<CraftMaterialDefinitionSO>(CraftMaterialDefinitionSO.ResourcePath));
        ResearchRewardCatalog rewards = new ResearchRewardCatalog(
            research,
            new FixedFacilityCatalog(buildings),
            economy,
            new FixedEquipmentCatalog(equipment),
            new ResourceSurgicalProcedureCatalog(
                Resources.LoadAll<SurgicalProcedureSO>(
                    SurgicalProcedureSO.ResourcePath)),
            null);
        foreach (string error in rewards.Validate())
        {
            failures.Add(error);
        }
    }

    private static void ValidateEquipment(
        IReadOnlyList<ResearchProjectSO> projects,
        IReadOnlyList<CombatEquipmentDefinitionSO> equipment,
        IReadOnlyList<EquipmentModuleDefinitionSO> modules,
        ICollection<string> failures)
    {
        HashSet<string> researchIds = projects
            .Select(project => project.ProjectId.Value)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> dayOneExpected = new HashSet<string>(StringComparer.Ordinal)
        {
            "weapon:dagger", "weapon:spear", "weapon:javelin",
            "armor:cloth-hood", "armor:leather-cap", "shield:wood"
        };
        HashSet<string> dayOneActual = equipment
            .Where(definition => string.IsNullOrWhiteSpace(definition.RequiredResearchId))
            .Select(definition => definition.EquipmentId)
            .ToHashSet(StringComparer.Ordinal);
        Require(dayOneActual.SetEquals(dayOneExpected),
            $"day-one equipment differs: {string.Join(", ", dayOneActual.OrderBy(id => id))}",
            failures);
        Require(equipment.Count == 61, $"equipment count {equipment.Count}, expected 61", failures);
        Require(modules.Count == 20, $"module count {modules.Count}, expected 20", failures);
        Require(modules.Select(module => module.ModuleId).Distinct(StringComparer.Ordinal).Count() == 20,
            "duplicate equipment module ID", failures);

        HashSet<string> growthExpected = new HashSet<string>(StringComparer.Ordinal)
        {
            "weapon:longsword", "armor:gambeson", "shield:iron",
            "weapon:halberd", "weapon:greatsword", "weapon:windlass-crossbow",
            "weapon:matchlock-pistol", "weapon:siege-arbalest", "weapon:rune-blade",
            "armor:scale-coat", "armor:articulated-plate", "armor:powered-harness",
            "armor:rune-ward-mail", "armor:blacksteel-carapace",
            "shield:buckler", "shield:rune", "weapon:repeating-crossbow",
            "weapon:sniper-arquebus", "weapon:heavy-matchlock",
            "weapon:blacksteel-poleaxe", "weapon:rune-bow", "shield:powered"
        };
        HashSet<string> fourSlotExpected = new HashSet<string>(StringComparer.Ordinal)
        {
            "weapon:siege-arbalest", "weapon:rune-blade", "armor:powered-harness",
            "armor:blacksteel-carapace", "shield:rune", "shield:powered",
            "weapon:blacksteel-poleaxe"
        };
        HashSet<string> growthActual = equipment.Where(definition => definition.GrowthEquipment)
            .Select(definition => definition.EquipmentId)
            .ToHashSet(StringComparer.Ordinal);
        Require(growthActual.SetEquals(growthExpected), "growth equipment set differs", failures);

        foreach (CombatEquipmentDefinitionSO definition in equipment)
        {
            if (!string.IsNullOrWhiteSpace(definition.RequiredResearchId))
            {
                Require(researchIds.Contains(definition.RequiredResearchId),
                    $"{definition.EquipmentId}: missing research {definition.RequiredResearchId}", failures);
            }
            if (definition.GrowthEquipment)
            {
                int expectedSlots = fourSlotExpected.Contains(definition.EquipmentId) ? 4 : 3;
                Require(definition.ModuleSlotCount == expectedSlots,
                    $"{definition.EquipmentId}: growth slot count {definition.ModuleSlotCount}", failures);
                Require(Mathf.Approximately(definition.BaseStatMultiplier, 0.88f),
                    $"{definition.EquipmentId}: growth base multiplier is not 0.88", failures);
            }
            else
            {
                Require(definition.ModuleSlotCount <= 1,
                    $"{definition.EquipmentId}: normal equipment has more than one slot", failures);
            }
        }
    }

    private static void ValidateDeterministicDrops(ICollection<string> failures)
    {
        foreach (EquipmentExpeditionRewardKind kind in
                 Enum.GetValues(typeof(EquipmentExpeditionRewardKind)))
        {
            EquipmentExpeditionRewardRequest request = new EquipmentExpeditionRewardRequest(
                8675309, "fixed-event", kind, EquipmentEra.MatureIndustrial,
                "region:validation", Vector2Int.zero);
            int first = EquipmentExpeditionRewardService.PreviewModuleDropCount(request);
            int second = EquipmentExpeditionRewardService.PreviewModuleDropCount(request);
            Require(first == second, $"{kind}: runSeed result is not deterministic", failures);
            if (kind == EquipmentExpeditionRewardKind.RegionBoss)
            {
                Require(first is 1 or 2, "boss module reward is not guaranteed", failures);
            }
            else
            {
                Require(first is 0 or 1, $"{kind}: invalid optional drop count", failures);
            }
        }
    }

    private static void ValidateRuntimeLocksModulesAndSave(
        ICollection<string> failures)
    {
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        CombatEquipmentRuntime locked = CombatEquipmentEditorTestFactory.Create(
            catalog,
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(), materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, researchProvider: EditorLockedResearchRuntimeReferences.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        Require(locked.IsDefinitionUnlocked("weapon:dagger", out _),
            "day-one dagger is locked", failures);
        bool lockedWithExpectedCode =
            !locked.IsDefinitionUnlocked("weapon:longsword", out string lockReason)
            && string.Equals(
                lockReason,
                $"equipment.research.required:{LongswordResearchId}",
                StringComparison.Ordinal);
        /* Legacy localized-message assertion intentionally replaced by the stable failure code.
                && lockReason.Contains("연구 필요", StringComparison.Ordinal),
        */
        Require(lockedWithExpectedCode, "longsword does not expose its research lock", failures);
        bool directCreateRejected = false;
        try
        {
            locked.CreateInstance("weapon:longsword", CombatEquipmentQuality.Normal);
        }
        catch (InvalidOperationException exception)
        {
            directCreateRejected = string.Equals(
                exception.Message,
                $"equipment.research.required:{LongswordResearchId}",
                StringComparison.Ordinal);
            /* Legacy localized-message assertion intentionally replaced by the stable failure code.
                "연구 필요", StringComparison.Ordinal);
            */
        }
        Require(directCreateRejected, "direct runtime call bypasses research lock", failures);

        ResourceEquipmentModuleCatalog moduleCatalog = new ResourceEquipmentModuleCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        WorldItemStackRuntime physicalItems =
            PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository itemRepository,
                out CombatEquipmentRuntime runtime);
        physicalItems.Start();
        List<GameObject> progressionObjects = new List<GameObject>();
        BuildableObject appraisal = CreateProgressionFacility(
            AppraisalFacilityPath,
            "ResearchEquipment_Appraisal",
            new Vector2Int(40, 40),
            progressionObjects);
        BuildableObject restoration = CreateProgressionFacility(
            RestorationFacilityPath,
            "ResearchEquipment_Restoration",
            new Vector2Int(42, 40),
            progressionObjects);
        BuildableObject fitting = CreateProgressionFacility(
            PrecisionFittingFacilityPath,
            "ResearchEquipment_Fitting",
            new Vector2Int(44, 40),
            progressionObjects);
        BuildableObject wrongFacility = CreateProgressionFacility(
            WrongProgressionFacilityPath,
            "ResearchEquipment_Wrong",
            new Vector2Int(46, 40),
            progressionObjects);
        Require(appraisal != null && restoration != null && fitting != null
                && wrongFacility != null,
            "equipment progression facility fixture is incomplete", failures);
        Require(runtime.IsDefinitionUnlocked("weapon:longsword", out _),
            "completed research does not immediately unlock equipment", failures);
        CombatEquipmentInstance weapon = runtime.CreateInstance(
            "weapon:greatsword", CombatEquipmentQuality.Good);
        EquipmentModuleInstance module = runtime.CreateExpeditionModule(
            "module:weapon:balanced-core",
            3,
            appraisal.centerPos,
            WorldItemStackState.FacilityBuffer,
            appraisal.RequirePersistentInstanceId().Value);
        string appraisalDestination = appraisal
            .RequirePersistentInstanceId().Value;
        bool appraisalSuppliesReady = physicalItems.SpawnItemAt(
                "component:material-test-coupon",
                1,
                appraisal.centerPos,
                WorldItemStackState.FacilityBuffer,
                appraisalDestination,
                out int couponCount)
            && couponCount == 1
            && physicalItems.SpawnUniqueItemAt(
                DurableToolItemRules.InspectionGauge,
                appraisal.centerPos,
                WorldItemStackState.FacilityBuffer,
                appraisalDestination,
                out string gaugeStackId)
            && physicalItems.TrySetInstanceComponent(
                gaugeStackId,
                DurableToolItemRules.CreateDurability(
                    DurableToolItemRules.InspectionGauge,
                    100f))
            && physicalItems.SpawnUniqueItemAt(
                DurableToolItemRules.RuneIdentificationLens,
                appraisal.centerPos,
                WorldItemStackState.FacilityBuffer,
                appraisalDestination,
                out string lensStackId)
            && physicalItems.TrySetInstanceComponent(
                lensStackId,
                DurableToolItemRules.CreateDurability(
                    DurableToolItemRules.RuneIdentificationLens,
                    100f));
        Require(
            appraisalSuppliesReady,
            "physical appraisal supplies could not be prepared",
            failures);
        Require(!runtime.TryAppraiseModule(
                module.instanceId,
                wrongFacility,
                out DomainFailure wrongFacilityFailure)
                && wrongFacilityFailure.Code
                    == FailureCode.EquipmentProgressionFacilityUnavailable,
            "wrong facility bypassed module appraisal authorization", failures);
        Require(runtime.TryAppraiseModule(module.instanceId, appraisal, out _),
            "module appraisal failed", failures);
        Require(!runtime.TryRestoreModule(
                module.instanceId,
                restoration,
                out DomainFailure remoteRestoreFailure)
                && remoteRestoreFailure.Code == FailureCode.EquipmentModuleMissing,
            "module restoration ignored the facility-local buffer", failures);
        Require(physicalItems.TryRouteStackToDestination(
                module.sourceStackId,
                WorldItemStackState.FacilityBuffer,
                restoration.RequirePersistentInstanceId().Value,
                restoration.centerPos,
                out _),
            "module could not be routed to the restoration buffer", failures);
        Require(runtime.TryRestoreModule(module.instanceId, restoration, out _),
            "module restoration failed", failures);
        Require(physicalItems.TryRouteStackToDestination(
                module.sourceStackId,
                WorldItemStackState.FacilityBuffer,
                fitting.RequirePersistentInstanceId().Value,
                fitting.centerPos,
                out _),
            "module could not be routed to the fitting buffer", failures);
        Require(physicalItems.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipment(weapon.definitionId),
                (ItemInstanceId)weapon.instanceId,
                fitting.centerPos,
                WorldItemStackState.FacilityBuffer,
                fitting.RequirePersistentInstanceId().Value,
                out string weaponStackId)
                && runtime.TryLinkToWorldStack(
                    weapon.instanceId,
                    weaponStackId,
                    CombatEquipmentWorldState.Stored),
            "equipment could not be materialized in the fitting buffer", failures);
        Require(!runtime.TryInstallModule(
                weapon.instanceId,
                module.instanceId,
                0,
                appraisal,
                out DomainFailure wrongInstallFailure)
                && wrongInstallFailure.Code
                    == FailureCode.EquipmentProgressionFacilityUnavailable,
            "wrong facility bypassed module installation authorization", failures);
        Require(runtime.TryInstallModule(
                weapon.instanceId, module.instanceId, 0, fitting, out _),
            "module installation failed", failures);
        Require(runtime.TryRemoveModule(
                weapon.instanceId,
                0,
                fitting,
                out EquipmentModuleInstance removed,
                out _)
                && removed.condition <= 0.7f
                && removed.state == EquipmentModuleProcessState.IdentifiedDamaged,
            "removed module was not returned as a <=70% damaged part", failures);
        Require(!runtime.TryInstallModule(
                weapon.instanceId,
                module.instanceId,
                0,
                fitting,
                out DomainFailure damagedFailure)
                && damagedFailure.Code == FailureCode.ModuleNeedsRestoration,
            "damaged module was reinstalled without restoration", failures);

        DungeonCombatEquipmentSaveData save = runtime.Capture();
        CombatEquipmentRuntime restored = CombatEquipmentEditorTestFactory.Create(
            catalog,
            itemRepository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: moduleCatalog, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        restored.PublishRestoreCandidate(
            restored.BuildRestoreCandidate(save));
        Require(restored.ModuleInstances.Count == 1
                && restored.Instances.Any(instance => instance.instanceId == weapon.instanceId),
            "equipment V6 module save round trip failed", failures);
        physicalItems.Dispose();
        foreach (GameObject progressionObject in progressionObjects)
        {
            UnityEngine.Object.DestroyImmediate(progressionObject);
        }

        CombatEquipmentSaveSection saveSection = new CombatEquipmentSaveSection(restored);
        bool legacyRejected = false;
        try
        {
            saveSection.Restore(
                JsonUtility.ToJson(save), 4, new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException exception)
        {
            legacyRejected = true;
            _ = exception.Message.Contains(
                "새 게임 필요",
                StringComparison.Ordinal);
        }
        Require(legacyRejected, "combat equipment V1-V4 save was not rejected", failures);

        string beforeInvalidRestore = JsonUtility.ToJson(restored.Capture());
        DungeonCombatEquipmentSaveData invalid = JsonUtility.FromJson<DungeonCombatEquipmentSaveData>(
            JsonUtility.ToJson(save));
        invalid.craftOrders.Add(new CombatEquipmentCraftOrderSaveData
        {
            orderId = " invalid-order ",
            definitionId = "weapon:dagger",
            requiredWork = 1f,
            completedWork = 0f,
            materialDestinationId = "equipment-craft:invalid-order"
        });
        bool invalidRejected = false;
        try
        {
            saveSection.StageRestore(
                JsonUtility.ToJson(invalid),
                6,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            invalidRejected = true;
        }

        Require(invalidRejected, "invalid combat equipment payload was accepted", failures);
        Require(string.Equals(
                beforeInvalidRestore,
                JsonUtility.ToJson(restored.Capture()),
                StringComparison.Ordinal),
            "invalid combat equipment payload mutated live state", failures);
        Require(saveSection is IDungeonRollbackFreeSaveSection
                && saveSection is IDungeonSaveSectionPreflight
                && saveSection is IDungeonStagedSaveSection,
            "combat equipment save section is missing strict restore contracts", failures);
    }

    private static void ValidatePacing(
        IReadOnlyList<ResearchProjectSO> projects,
        ICollection<string> failures,
        out string report)
    {
        Dictionary<string, ResearchProjectSO> byId = projects.ToDictionary(
            project => project.ProjectId.Value,
            StringComparer.Ordinal);
        HashSet<ResearchProjectSO> closure = new HashSet<ResearchProjectSO>();
        float medieval = AddQueueAndMeasure(MedievalQueue, byId, closure);
        float early = AddQueueAndMeasure(EarlyIndustrialQueue, byId, closure);
        float mature = AddQueueAndMeasure(MatureIndustrialQueue, byId, closure);
        float late = AddQueueAndMeasure(LateIndustrialQueue, byId, closure);
        Require(medieval >= 27f && medieval <= 34f,
            $"medieval pacing {medieval:0.0} days", failures);
        Require(early >= 80f && early <= 100f,
            $"early industrial pacing {early:0.0} days", failures);
        Require(mature >= 200f && mature <= 240f,
            $"mature industrial pacing {mature:0.0} days", failures);
        Require(late >= 320f && late <= 400f,
            $"late industrial pacing {late:0.0} days", failures);
        report = $"pacing days M/E/A/L={medieval:0.0}/{early:0.0}/{mature:0.0}/{late:0.0}";
    }

    private static float AddQueueAndMeasure(
        IEnumerable<string> ids,
        IReadOnlyDictionary<string, ResearchProjectSO> byId,
        ISet<ResearchProjectSO> closure)
    {
        foreach (string id in ids)
        {
            string normalizedId = V21ResearchConsolidation.Normalize(id);
            if (!byId.TryGetValue(normalizedId, out ResearchProjectSO project))
            {
                throw new InvalidOperationException($"Pacing queue research does not exist: {normalizedId}");
            }
            AddClosure(project, closure);
        }
        return closure.Sum(project => project.RequiredWork) / EffectiveWorkPerDay;
    }

    private static void AddClosure(ResearchProjectSO project, ISet<ResearchProjectSO> closure)
    {
        if (!closure.Add(project))
        {
            return;
        }
        foreach (ResearchProjectSO prerequisite in project.Prerequisites)
        {
            AddClosure(prerequisite, closure);
        }
    }

    private static void AddAuthoredRows<T>(
        ICollection<string> rows,
        string category,
        string entry,
        string effect,
        string saveOwner)
        where T : V20AuthoredContentSO =>
        AddConnectionRows<T>(
            rows,
            category,
            value => value.StableId,
            entry,
            effect,
            saveOwner);

    private static void AddConnectionRows<T>(
        ICollection<string> rows,
        string category,
        Func<T, string> id,
        string entry,
        string effect,
        string saveOwner)
        where T : UnityEngine.Object =>
        AddConnectionRows(
            rows,
            category,
            id,
            _ => entry,
            _ => effect,
            _ => saveOwner);

    private static void AddConnectionRows<T>(
        ICollection<string> rows,
        string category,
        Func<T, string> id,
        Func<T, string> entry,
        Func<T, string> effect,
        Func<T, string> saveOwner)
        where T : UnityEngine.Object
    {
        T[] definitions = LoadAllAssets<T>();
        foreach (T definition in definitions)
        {
            string stableId = id(definition)?.Trim() ?? string.Empty;
            string entryPoint = entry(definition)?.Trim() ?? string.Empty;
            string appliedEffect = effect(definition)?.Trim() ?? string.Empty;
            string owner = saveOwner(definition)?.Trim() ?? string.Empty;
            if (stableId.Length == 0
                || entryPoint.Length == 0
                || appliedEffect.Length == 0
                || owner.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Incomplete gameplay connection for {typeof(T).Name} '{definition?.name}'.");
            }
            rows.Add(
                $"| {Escape(category)} | `{Escape(stableId)}` | {Escape(entryPoint)} | {Escape(appliedEffect)} | `{Escape(owner)}` |");
        }
    }

    private static string FacilityEntry(
        BuildingSO facility,
        IReadOnlyList<ProductionRecipeSO> recipes)
    {
        if (facility.ResearchFacilityCommand != ResearchFacilityCommandKind.None)
        {
            return $"Operate 작업 / {facility.ResearchFacilityCommand}";
        }
        string workstation = facility.GetProductionWorkstationAbility()
            ?.WorkstationTag ?? string.Empty;
        return recipes.Any(value => string.Equals(
                value.WorkstationTag,
                workstation,
                StringComparison.Ordinal))
            ? $"제작 주문 / {workstation}"
            : facility.EffectiveUseClassification switch
            {
                FacilityUseClassification.Production => "제작·생산 작업",
                FacilityUseClassification.DomainCommand => "도메인 시설 작업",
                FacilityUseClassification.Structure => "건설·통행 명령",
                FacilityUseClassification.Storage => "저장·물류 정책",
                FacilityUseClassification.Service => "서비스 작업",
                FacilityUseClassification.Environment => "환경망 가동",
                FacilityUseClassification.Logistics => "물류망 가동",
                FacilityUseClassification.Combat => "방어 명령",
                FacilityUseClassification.EventVenue => "행사 명령",
                FacilityUseClassification.Decoration => "방 배치",
                _ => string.Empty
            };
    }

    private static string FacilityEffect(
        BuildingSO facility,
        IReadOnlyList<ProductionRecipeSO> recipes)
    {
        if (facility.ResearchFacilityCommand != ResearchFacilityCommandKind.None)
        {
            string owner = ResearchFacilityCommandConsumerRegistry.DomainOwner(
                facility.ResearchFacilityCommand);
            return $"{owner} typed command 효과";
        }
        string workstation = facility.GetProductionWorkstationAbility()
            ?.WorkstationTag ?? string.Empty;
        int recipeCount = recipes.Count(value => string.Equals(
            value.WorkstationTag,
            workstation,
            StringComparison.Ordinal));
        return recipeCount > 0
            ? $"물리 입력 소비·물리 출력 생성 ({recipeCount} recipe)"
            : "시설 분류별 월드 상태·경로·서비스 효과";
    }

    private static string FacilitySaveOwner(BuildingSO facility)
    {
        if (facility.ResearchFacilityCommand != ResearchFacilityCommandKind.None)
        {
            return "buildings.world + "
                + ResearchFacilityCommandConsumerRegistry.DomainOwner(
                    facility.ResearchFacilityCommand);
        }
        return facility.EffectiveUseClassification switch
        {
            FacilityUseClassification.Production =>
                "buildings.world + economy.production",
            FacilityUseClassification.Combat =>
                "buildings.world + defense.facilities",
            FacilityUseClassification.Environment =>
                "buildings.world + infrastructure",
            FacilityUseClassification.Logistics =>
                "buildings.world + economy.logistics",
            FacilityUseClassification.Service or
            FacilityUseClassification.EventVenue =>
                "buildings.world + society.events",
            _ => "buildings.world"
        };
    }

    private static T[] LoadAllAssets<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .ToArray();

    private static string Escape(string value) =>
        (value ?? string.Empty)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private static T[] LoadAssets<T>(string root) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();

    private static BuildableObject CreateProgressionFacility(
        string assetPath,
        string objectName,
        Vector2Int position,
        ICollection<GameObject> created)
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(assetPath);
        if (definition == null)
        {
            return null;
        }
        GameObject facilityObject = new GameObject(objectName);
        created.Add(facilityObject);
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        CharacterAiEditorTestDependencies.Inject(facility);
        facility.Initialization(definition, position);
        return facility;
    }

    private static void Require(bool condition, string message, ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private sealed class FixedFacilityCatalog : IFacilityShopCatalog
    {
        private readonly BuildingSO[] buildings;
        public FixedFacilityCatalog(BuildingSO[] buildings) => this.buildings = buildings;
        public IReadOnlyCollection<BuildingSO> Buildings => buildings;
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
            Array.Empty<FacilityBlueprintSO>();
        public BuildingSO FindBuildingById(int buildingId) =>
            buildings.FirstOrDefault(building => building.id == buildingId);
    }

    private sealed class FixedEquipmentCatalog : ICombatEquipmentCatalog
    {
        private readonly CombatEquipmentDefinitionSO[] equipment;
        public FixedEquipmentCatalog(CombatEquipmentDefinitionSO[] equipment) =>
            this.equipment = equipment;
        public IReadOnlyList<CombatEquipmentDefinitionSO> All => equipment;
        public bool TryGet(string definitionId, out CombatEquipmentDefinitionSO definition)
        {
            definition = equipment.FirstOrDefault(item => string.Equals(
                item.EquipmentId, definitionId, StringComparison.Ordinal));
            return definition != null;
        }
    }
}
#endif
