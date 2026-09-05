using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class OffenseStrategicDebugScenarios
{
    [MenuItem("Tools/DungeonStory/Validation/Run Offense Strategic Scenarios")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        Debug.Log(report);
    }

    public static string RunAll()
    {
        List<string> passed = new List<string>();
        Run("콘텐츠 카탈로그", VerifyContentCatalog, passed);
        Run("월드 생성 결정성", VerifyWorldDeterminism, passed);
        Run("육각 A*와 안전 이동", VerifyPathAndReturnSafety, passed);
        Run("긴급 거점 단계와 완화", VerifyUrgentSiteLifecycle, passed);
        Run("긴급 거점 물리 완화 작업", VerifyPhysicalUrgentMitigation, passed);
        Run("원정 보급 물리 집결", VerifyPhysicalExpeditionPacking, passed);
        Run("원정 정비 도구 물리 집결", VerifyPhysicalExpeditionToolPacking, passed);
        Run("Strategic 중간 상태 저장 왕복", VerifySaveRoundTrip, passed);
        Run("사건 선택 실제 결과", VerifyDecisionEffects, passed);
        Run("전술 사슬 전달률", VerifyChainFallbacks, passed);
        Run("명령 덱과 적 의도 단일 실행", VerifyCommandBattle, passed);
        return $"Offense Strategic scenarios passed ({passed.Count}): "
            + string.Join(", ", passed);
    }

    private static void VerifyContentCatalog()
    {
        EditorContentCatalog catalog = LoadCatalog();
        Require(catalog.SiteArchetypes.Count >= 12, "고정 보스를 포함한 거점 원형이 12개 미만입니다.");
        Require(catalog.UrgentSites.Count == 6, "긴급 거점 정의는 6개여야 합니다.");
        Require(catalog.DecisionCards.Count >= 48, "사건 카드는 최소 48개여야 합니다.");
        Require(catalog.Encounters.Count >= 6, "인카운터 정의가 6개 미만입니다.");
        Require(
            catalog.DecisionCards.All(card =>
                card != null
                && !string.IsNullOrWhiteSpace(card.cardId)
                && card.choices != null
                && card.choices.Count == 2
                && card.choices.All(choice =>
                    choice != null
                    && !string.IsNullOrWhiteSpace(choice.choiceId))),
            "모든 사건 카드는 정확히 두 개의 유효 선택지를 가져야 합니다.");
        string[] effectlessChoices = catalog.DecisionCards
            .SelectMany(card => card.choices.Select(choice =>
                new { card, choice }))
            .Where(pair => pair.choice.effects == null
                || pair.choice.effects.Count == 0)
            .Select(pair =>
                $"{pair.card.cardId}:{pair.choice.choiceId}"
                + $"({pair.choice.directionLabel})")
            .ToArray();
        Require(
            effectlessChoices.Length == 0,
            "실제 결과 모듈이 없는 사건 선택지가 있습니다: "
            + string.Join(", ", effectlessChoices));
        Require(
            catalog.DecisionCards
                .SelectMany(card => card.choices)
                .Where(choice => choice.mayStartCombat)
                .All(choice => choice.effects.Any(effect =>
                    effect is OffenseCombatDecisionEffect)),
            "전투 사건에 전투 실행 효과가 없습니다.");
        Require(
            catalog.DecisionCards
                .SelectMany(card => card.choices)
                .Where(choice => choice.mayMoveExpedition)
                .All(choice => choice.effects.Any(effect =>
                    effect is OffenseForcedMoveDecisionEffect)),
            "강제 이동 사건에 이동 실행 효과가 없습니다.");
        RequireUnique(
            catalog.DecisionCards.Select(card => card.cardId),
            "사건 카드 ID");
        RequireUnique(
            catalog.SiteArchetypes.Select(site => site.siteTypeId),
            "거점 원형 ID");
        RequireUnique(
            catalog.UrgentSites.Select(site => site.urgentSiteId),
            "긴급 거점 ID");
        Require(
            catalog.SiteArchetypes.All(site =>
                site != null
                && site.rewards != null
                && site.rewards.Any(reward =>
                    reward != null && reward.IsConfigured)),
            "실제 보상 정의가 없는 거점 원형이 있습니다.");
        Require(
            catalog.SiteArchetypes
                .Where(site => site != null
                    && site.pressureAxis != StrategicPressureAxis.None
                    && site.pressureAmount > 0f)
                .All(site => site.rewards.Any(reward =>
                    reward?.GrantSpec is OffenseRegionalPressureRewardSpec)),
            "전략 압력 거점에 지역 압력 보상이 연결되지 않았습니다.");
        Require(
            catalog.SiteArchetypes.Any(site =>
                site.rewards.Any(reward =>
                    reward?.GrantSpec is OffenseStockRewardSpec)),
            "실물 재고 보상을 지급하는 거점이 없습니다.");
        Require(
            catalog.SiteArchetypes.Any(site =>
                site.rewards.Any(reward =>
                    reward?.GrantSpec is OffensePrisonerRewardSpec)),
            "실물 포로 귀환 보상을 지급하는 거점이 없습니다.");
        Require(
            catalog.SiteArchetypes.Count(site =>
                site != null && !site.dynamicSpawnEligible) == 2,
            "고정 보스 원형 두 개가 동적 생성 제외로 설정되지 않았습니다.");
    }

    private static void VerifyWorldDeterminism()
    {
        EditorContentCatalog catalog = LoadCatalog();
        OffenseHexWorldSimulation first = CreateWorld(catalog, 71231);
        OffenseHexWorldSimulation second = CreateWorld(catalog, 71231);
        string firstJson = JsonUtility.ToJson(first.Capture());
        string secondJson = JsonUtility.ToJson(second.Capture());
        Require(firstJson == secondJson, "같은 시드의 월드 상태가 다릅니다.");
        Require(
            first.Tiles.Count == 1 + 3 * OffenseHexWorldSimulation.DefaultRadius
                * (OffenseHexWorldSimulation.DefaultRadius + 1),
            "육각 월드 타일 수가 반지름 공식과 다릅니다.");
        Require(
            first.Sites.Any(site =>
                site.siteId == OffenseHexWorldSimulation.RivalDungeonSiteId),
            "고정 경쟁 던전 권역이 없습니다.");
        Require(
            first.Sites.Any(site =>
                site.siteId == OffenseHexWorldSimulation.TruthCoreSiteId),
            "고정 진실 권역이 없습니다.");
        Require(
            first.Sites
                .Where(site => site != null && !site.fixedBoss)
                .All(site => catalog.SiteArchetypes.Any(archetype =>
                    archetype != null
                    && archetype.dynamicSpawnEligible
                    && archetype.siteTypeId == site.archetypeId)),
            "동적 거점에 고정 보스 전용 원형이 생성되었습니다.");
        OffenseWorldSiteStateData[] initialApproachSites = first.Sites
            .Where(site => site != null
                && !site.fixedBoss
                && site.state == OffenseWorldSiteState.Revealed)
            .ToArray();
        Require(
            initialApproachSites.Length
                == OffenseHexWorldSimulation.InitialRevealedSiteCount,
            "새 런 기본 정찰 거점 수가 다릅니다.");
        Require(
            initialApproachSites.All(site => site.strength <= 4),
            "기본 정찰 거점이 시작 직원 두 명으로 출정할 수 없습니다.");
    }

    private static void VerifyPathAndReturnSafety()
    {
        OffenseHexWorldSimulation world = CreateWorld(LoadCatalog(), 8031);
        OffenseWorldSiteStateData destination = world.Sites
            .Where(site => site != null && site.IsActive)
            .OrderByDescending(site =>
                world.GetMinimumStepDistance(world.DungeonCoord, site.Coord))
            .First();
        Require(
            world.TryFindPath(
                world.DungeonCoord,
                destination.Coord,
                OffenseTravelProfile.Default,
                out IReadOnlyList<OffenseHexCoord> path,
                out float cost),
            "거점까지 A* 경로를 찾지 못했습니다.");
        Require(path.Count > 0 && cost > 0f, "A* 경로 또는 비용이 비어 있습니다.");
        OffenseHexCoord previous = world.DungeonCoord;
        foreach (OffenseHexCoord step in path)
        {
            Require(previous.DistanceTo(step) == 1, "A* 경로에 인접하지 않은 이동이 있습니다.");
            previous = step;
        }

        OffenseReturnSafetyRuntime safety = new OffenseReturnSafetyRuntime(world);
        int minimumSteps = world.GetMinimumStepDistance(
            destination.Coord,
            world.DungeonCoord);
        int granted = safety.GrantForObjective(
            "expedition:test",
            destination.Coord,
            world.DungeonCoord);
        Require(granted == minimumSteps, "안전 이동 칸이 최단 칸 수와 다릅니다.");
        Require(safety.Get("expedition:test").StressMultiplier == 0.35f,
            "안전 이동 스트레스 배율이 다릅니다.");
        Require(safety.ConsumeMovedStep("expedition:test"), "안전 이동 칸을 소비하지 못했습니다.");
        Require(
            safety.Get("expedition:test").SafeStepBudget == granted - 1,
            "실제 한 칸 이동이 안전 이동 한 칸만 소비하지 않았습니다.");
        safety.RecordProtectedDangerousEvent("expedition:test", true);
        Require(safety.MustUseNonCombatCard("expedition:test"),
            "위험 사건 뒤 비전투 카드 pity가 켜지지 않았습니다.");
        Require(!safety.CanGenerateForcedCombat(
                "expedition:test",
                1f,
                false,
                true),
            "보호 이동의 강제 전투 상한이 적용되지 않았습니다.");
        safety.ClearForSiteAttack("expedition:test");
        Require(!safety.Get("expedition:test").IsProtected,
            "다른 거점 공격이 안전 이동을 해제하지 않았습니다.");
    }

    private static void VerifyUrgentSiteLifecycle()
    {
        EditorContentCatalog catalog = LoadCatalog();
        OffenseHexWorldSimulation world = CreateWorld(catalog, 4407);
        OffenseUrgentSiteDefinitionSO definition = catalog.UrgentSites[0];
        OffenseHexCoord coord = FindReachableEmptyCoord(world);
        Require(
            world.TrySpawnUrgentSite(definition.urgentSiteId, coord, out string siteId),
            "긴급 거점을 생성하지 못했습니다.");
        Require(world.TryGetUrgentSite(siteId, out OffenseUrgentSiteStateData site)
            && site.stage == OffenseUrgentSiteStage.Signal,
            "긴급 거점이 징후 단계에서 시작하지 않았습니다.");
        Require(world.TryMitigateUrgentSite(siteId, 1f),
            "긴급 거점을 완화하지 못했습니다.");
        Require(site.mitigation <= 0.6001f,
            "긴급 거점 완화가 60% 상한을 넘었습니다.");

        world.AdvanceHours(12f);
        Require(site.stage == OffenseUrgentSiteStage.Warning,
            "12시간 후 경고 단계가 되지 않았습니다.");
        world.AdvanceHours(18f);
        Require(site.stage == OffenseUrgentSiteStage.Crisis,
            "경고 18시간 후 위기 단계가 되지 않았습니다.");
        Require(
            ((IWorldThreatModifierQuery)world)
                .GetModifier(definition.modifierKind)
                .EffectiveStrength > 0f,
            "위기 거점이 실제 위협 보정을 만들지 않았습니다.");
        world.AdvanceHours(24f);
        Require(site.stage == OffenseUrgentSiteStage.Withdrawing,
            "위기 24시간 후 철수 준비 단계가 되지 않았습니다.");
        world.AdvanceHours(6f);
        Require(site.stage == OffenseUrgentSiteStage.Expired,
            "철수 준비 6시간 후 긴급 거점이 만료되지 않았습니다.");
    }

    private static void VerifyDecisionEffects()
    {
        OffenseHexWorldSimulation world = CreateWorld(LoadCatalog(), 99031);
        OffenseReturnSafetyRuntime safety = new OffenseReturnSafetyRuntime(world);
        OffenseTravelRuntime travel = new OffenseTravelRuntime(world, safety, fieldMedical: null);
        Require(
            travel.TryCreateExpedition("decision-effects", out string reason),
            reason);
        OffenseSupplyLoadout supplies = new OffenseSupplyLoadout(
            new Dictionary<OffenseSupplyType, int>
            {
                [OffenseSupplyType.Rations] = 3
            });
        OffenseExpeditionRun expedition = new OffenseExpeditionRun(
            "decision-effects",
            new OffenseTargetDefinition
            {
                id = "decision-target",
                title = "사건 대상",
                durationSeconds = 10f,
                campaignOrder = 1
            },
            Array.Empty<CharacterActor>(),
            0f,
            10f,
            null,
            supplies,
            null);
        FixedMoneyRuntime money = new FixedMoneyRuntime(500);
        OffenseDecisionEffectExecutor executor =
            new OffenseDecisionEffectExecutor(
                new IOffenseDecisionEffectHandler[]
                {
                    new OffenseSupplyDecisionEffectHandler(),
                    new OffenseGoldDecisionEffectHandler(),
                    new OffenseStressDecisionEffectHandler(),
                    new OffenseExposureDecisionEffectHandler(),
                    new OffenseInjuryDecisionEffectHandler(),
                    new OffenseLootDecisionEffectHandler(),
                    new OffenseReconDecisionEffectHandler(),
                    new OffenseTimeDecisionEffectHandler(),
                    new OffenseEquipmentWearDecisionEffectHandler(),
                    new OffenseForcedMoveDecisionEffectHandler(),
                    new OffenseCombatDecisionEffectHandler()
                });
        OffenseDecisionEffectContext context =
            new OffenseDecisionEffectContext(
                expedition,
                travel,
                world,
                money,
                deterministicRoll: 17, equipment: null);
        OffenseDecisionEffectDefinition[] effects =
        {
            new OffenseSupplyDecisionEffect
            {
                supplyType = OffenseSupplyType.Rations,
                amount = -1
            },
            new OffenseGoldDecisionEffect { amount = -100 },
            new OffenseExposureDecisionEffect { amount = 20f },
            new OffenseLootDecisionEffect
            {
                stockCategory = StockCategory.General,
                amount = 3
            },
            new OffenseReconDecisionEffect { revealCount = 1 },
            new OffenseForcedMoveDecisionEffect(),
            new OffenseCombatDecisionEffect()
        };
        Require(executor.CanExecute(effects, context, out reason), reason);
        executor.Execute(effects, context);
        Require(
            expedition.Supplies.Get(OffenseSupplyType.Rations) == 2,
            "사건이 실제 원정 식량을 소비하지 않았습니다.");
        Require(money.Balance == 400, "사건 뇌물이 실제 골드를 소비하지 않았습니다.");
        Require(
            travel.TryGetState(
                expedition.ExpeditionId,
                out OffenseTravelStateData state)
            && Mathf.Approximately(state.exposure, 20f),
            "사건 노출도가 이동 상태에 반영되지 않았습니다.");
        Require(
            expedition.GetCarriedStock(StockCategory.General) == 3,
            "사건 전리품이 원정 적재에 반영되지 않았습니다.");
        Require(
            context.ForcesMovement && context.StartsCombat,
            "사건 제어 효과가 이동·전투 후속 흐름을 만들지 않았습니다.");

        OffenseDecisionEffectContext insufficient =
            new OffenseDecisionEffectContext(
                expedition,
                travel,
                world,
                money,
                deterministicRoll: 18, equipment: null);
        Require(
            !executor.CanExecute(
                new OffenseDecisionEffectDefinition[]
                {
                    new OffenseGoldDecisionEffect { amount = -999 }
                },
                insufficient,
                out _),
            "부족한 골드 비용 선택이 실행 가능으로 판정됐습니다.");
        Require(money.Balance == 400, "실행 가능성 검사만으로 골드가 변했습니다.");
    }

    private static void VerifyChainFallbacks()
    {
        OffenseChainResolution start = new OffenseChainResolution(
            OffenseChainState.Full,
            1f,
            OffenseTacticalTag.Intercept,
            0);
        RequireApproximately(
            OffenseTacticalChainRules.Advance(
                start,
                OffenseTacticalTag.Maneuver,
                OffenseCommandOutcome.Executed,
                true).Multiplier,
            1f,
            "정상 실행 100%");
        RequireApproximately(
            OffenseTacticalChainRules.Advance(
                start,
                OffenseTacticalTag.Maneuver,
                OffenseCommandOutcome.Retargeted,
                true).Multiplier,
            0.75f,
            "자동 재지정 75%");
        RequireApproximately(
            OffenseTacticalChainRules.Advance(
                start,
                OffenseTacticalTag.Maneuver,
                OffenseCommandOutcome.ClashLost,
                false).Multiplier,
            0.5f,
            "맞대응 완패 50%");
        OffenseChainResolution residual = OffenseTacticalChainRules.Advance(
            start,
            OffenseTacticalTag.Maneuver,
            OffenseCommandOutcome.Unavailable,
            false);
        RequireApproximately(residual.Multiplier, 0.25f, "행동 불능 25%");
        OffenseChainResolution broken = OffenseTacticalChainRules.Advance(
            residual,
            OffenseTacticalTag.Break,
            OffenseCommandOutcome.Unavailable,
            false);
        Require(broken.State == OffenseChainState.Broken
            && broken.Multiplier == 0f,
            "연속 두 슬롯 행동 불능이 사슬을 끊지 않았습니다.");
    }

    private static void VerifyPhysicalUrgentMitigation()
    {
        EditorContentCatalog catalog = LoadCatalog();
        OffenseHexWorldSimulation world = CreateWorld(catalog, 8821);
        OffenseUrgentSiteDefinitionSO definition = catalog.UrgentSites
            .First(candidate =>
                candidate != null
                && !string.IsNullOrWhiteSpace(
                    candidate.mitigationWorkTypeId)
                && !string.IsNullOrWhiteSpace(
                    candidate.mitigationItemId)
                && candidate.mitigationItemAmount > 0
                && candidate.mitigationWork > 0f);
        OffenseHexCoord coord = FindReachableEmptyCoord(world);
        Require(
            world.TrySpawnUrgentSite(
                definition.urgentSiteId,
                coord,
                out string siteId),
            "물리 완화 검증용 긴급 거점을 생성하지 못했습니다.");

        GameObject facilityObject =
            new GameObject("OffenseMitigationFacilityFixture");
        BuildingSO facilityData = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            facilityData.id = 917001;
            facilityData.name = "OffenseMitigationFacilityFixture";
            facilityData.objectName = "완화 검증 시설";
            facilityData.width = 1;
            facilityData.height = 1;
            facilityData.category = BuildingCategory.Special;
            facilityData.layer = GridLayer.Building;
            facilityData.Facility = new FacilityData();
            facilityData.Facility.AddSupportedWorkTypeId(
                new WorkTypeId(definition.mitigationWorkTypeId));
            Facility facility = facilityObject.AddComponent<Facility>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            facility.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                        .Create<IBlueprintResearchWorkService>()),
                new FacilityCandidateCacheStore(
                    CharacterAiEditorTestDependencies.WorldRegistry,
                    frameWorkBudget: null),
                new RoomFacilityPolicyService(RoomRegistry.EditorCache),
                combatEquipmentRuntime: null,
                worldRegistry: null,
                worldItemStackRuntime: null,
                abilityRuntimeDispatcher: null,
                gameClock: null,
                paidFacilityContracts: null,
                evolutionState: new FacilityEvolutionStateComponentFactory());
            facility.Initialization(facilityData, new Vector2Int(3, 2));

            RecordingProductionItemGateway items =
                new RecordingProductionItemGateway();
            MutableGameClock clock = new MutableGameClock();
            OffenseUrgentMitigationRuntime runtime =
                new OffenseUrgentMitigationRuntime(
                    world,
                    catalog,
                    new FixedBuildingWorldQuery(facility),
                    items,
                    new RecordingUrgentMitigationInputOwnerRuntime(),
                    clock, workforce: null, facilityCandidates: null);
            runtime.Initialize();

            Require(runtime.TryStart(siteId, out string message), message);
            Require(
                RuntimeWorkCapabilityUtility.Supports(
                    facility,
                    BuiltInWorkTypeIds.ThreatMitigation),
                "완화 주문이 담당 시설에 런타임 작업 능력을 부여하지 않았습니다.");
            Require(
                items.RequestedAmount == definition.mitigationItemAmount,
                "완화 재료가 물리 운반 요청으로 발행되지 않았습니다.");
            Require(
                !runtime.TryGetWork(facility, null, out _),
                "재료 납품 전에 완화 작업이 시작 가능해졌습니다.");

            items.DeliverAll();
            clock.Advance(1f);
            runtime.Tick();
            Require(
                runtime.TryGetWork(
                    facility,
                    null,
                    out OffenseUrgentMitigationWorkSnapshot work)
                && work.Available,
                "재료 납품 후 완화 작업이 활성화되지 않았습니다.");
            items.FailNextWipAcknowledgement = true;
            Require(
                !runtime.ApplyWork(
                    facility,
                    null,
                    definition.mitigationWork,
                    out bool completed)
                && !completed,
                "WIP acknowledgement fault가 완화 주문을 조기에 완료했습니다.");
            OffenseUrgentMitigationOrderStateData pendingOutcome =
                runtime.Capture().Single();
            Require(
                pendingOutcome.physicalCommitPhase
                    == (int)OffenseUrgentMitigationCommitPhase.OutcomePublished
                && !pendingOutcome.physicalReceiptAcknowledged,
                "완화 결과 outbox가 acknowledgement fault를 보존하지 않았습니다.");
            Require(
                world.TryGetUrgentSite(
                    siteId,
                    out OffenseUrgentSiteStateData beforeRecovery)
                && beforeRecovery.mitigation > 0f,
                "acknowledgement fault 전에 완화 결과가 게시되지 않았습니다.");
            float publishedMitigation = beforeRecovery.mitigation;
            runtime = new OffenseUrgentMitigationRuntime(
                world,
                catalog,
                new FixedBuildingWorldQuery(facility),
                items,
                new RecordingUrgentMitigationInputOwnerRuntime(),
                clock,
                workforce: null,
                facilityCandidates: null);
            object restoreCandidate = typeof(OffenseUrgentMitigationRuntime)
                .GetMethod(
                    "PrepareRestore",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(
                    runtime,
                    new object[]
                    {
                        new[] { pendingOutcome }
                    });
            Require(
                restoreCandidate != null,
                "완화 outbox restore candidate를 만들지 못했습니다.");
            typeof(OffenseUrgentMitigationRuntime)
                .GetMethod(
                    "PublishRestore",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(runtime, new[] { restoreCandidate });
            runtime.Initialize();
            clock.Advance(1f);
            runtime.Tick();
            Require(
                items.WipTransferredAmount == definition.mitigationItemAmount
                && items.WipTransferredMassGrams
                    == (long)definition.mitigationItemAmount * 1_000L
                && items.WipAcknowledgementCount == 1
                && runtime.Capture().Count == 0,
                "완화 완료 시 납품 재료를 exact Transfer-to-WIP로 귀속하지 않았습니다.");
            Require(
                world.TryGetUrgentSite(
                    siteId,
                    out OffenseUrgentSiteStateData urgent)
                && Mathf.Abs(urgent.mitigation - publishedMitigation) <= 0.0001f,
                "완화 outbox 재시도가 결과를 중복 적용했습니다.");
            Require(
                !RuntimeWorkCapabilityUtility.Supports(
                    facility,
                    BuiltInWorkTypeIds.ThreatMitigation),
                "완료된 완화 주문의 임시 작업 능력이 남아 있습니다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(facilityData);
        }
    }

    private static void VerifyCommandBattle()
    {
        CharacterCombatAbilityDefinition statusOnlyAbility =
            new CharacterCombatAbilityDefinition(
                "skill:status-only",
                "취약 노출",
                "피해 없이 취약만 부여하는 검증 기술",
                0,
                OffenseBattleTargetRule.Enemy,
                new OffenseVulnerabilityEffect(0.25f, 2));
        OffenseBattleCombatant deckCombatant = new OffenseBattleCombatant(
            "ally:deck",
            "덱 검증원",
            "human",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(60f, 8f, 7f, 6f, 7f, 6f),
            60f,
            new[] { statusOnlyAbility });
        OffenseBattleSession deckSession = new OffenseBattleSession(
            "battle:deck",
            "expedition:deck",
            "site:deck",
            "덱 검증",
            DungeonDifficulty.Normal,
            new[] { deckCombatant },
            OffenseEditorTestDependencies.CreateCombatResolution(),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());
        IReadOnlyList<OffenseCommandCardStateData> generatedCards =
            OffenseStrategicBattleSetupFactory.CreateMemberDecks(deckSession)[0].cards;
        Require(
            generatedCards.Count == 8,
            "개인 명령 덱이 8장으로 구성되지 않았습니다.");
        Require(
            generatedCards[0].actionType == OffenseBattleActionType.BasicAttack
            && generatedCards[0].sourceSkillId.Length == 0
            && generatedCards[1].actionType == OffenseBattleActionType.Advance
            && generatedCards[1].sourceSkillId.Length == 0,
            "전략 덱의 기본 공격과 전진 행동 권위가 명시되지 않았습니다.");
        Require(
            generatedCards[0].displayName == "기본 공격"
            && generatedCards[1].displayName == "전진",
            "기본 공격과 전진 카드 이름이 계획된 한국어 이름과 다릅니다.");
        Require(
            generatedCards.Skip(2).All(
                card => card.actionType == OffenseBattleActionType.Ability
                    && card.sourceSkillId == statusOnlyAbility.Id),
            "일반 기술 카드가 캐릭터 액티브와 연결되지 않았습니다.");

        OffenseBattleCombatant frontAlly = new OffenseBattleCombatant(
            "ally:liveness",
            "전열 검증원",
            "human",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(60f, 8f, 7f, 6f, 7f, 6f),
            60f,
            formation: OffenseFormationSlot.Front);
        OffenseBattleCombatant protectedObjective = new OffenseBattleCombatant(
            "ally:objective",
            "보호 대상",
            "human",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(60f, 1f, 1f, 1f, 1f, 1f),
            60f,
            formation: OffenseFormationSlot.Rear,
            participatesInInitiative: false);
        OffenseBattleCombatant rearEnemy = new OffenseBattleCombatant(
            "enemy:liveness",
            "후열 근접 적",
            "human",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(60f, 8f, 7f, 6f, 7f, 6f),
            60f,
            formation: OffenseFormationSlot.Rear);
        OffenseBattleSession livenessSession = new OffenseBattleSession(
            "battle:liveness",
            "expedition:liveness",
            "site:liveness",
            "후열 근접 교착 검증",
            DungeonDifficulty.Normal,
            new[] { frontAlly, protectedObjective, rearEnemy },
            OffenseEditorTestDependencies.CreateCombatResolution(),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());
        IReadOnlyList<OffenseBattleMemberDeckSeed> livenessDecks =
            OffenseStrategicBattleSetupFactory.CreateMemberDecks(
                livenessSession);
        IReadOnlyList<OffenseEnemyIntentStateData> livenessIntents =
            OffenseStrategicBattleSetupFactory.CreateEnemyIntents(
                livenessSession);
        Require(
            livenessDecks.Count == 1
            && livenessDecks[0].characterId == frontAlly.PersistentId
            && livenessDecks[0].cards.Count(card =>
                card.actionType == OffenseBattleActionType.Advance) >= 1,
            "비선제 보호 대상이 덱을 소유하거나 전진 카드가 누락되었습니다.");
        Require(
            livenessIntents.Count == 1
            && livenessIntents[0].enemyId == rearEnemy.PersistentId
            && livenessIntents[0].actionType == OffenseBattleActionType.Advance
            && livenessIntents[0].targetCharacterId == rearEnemy.PersistentId,
            "후열 근접 적이 불법 기본 공격 대신 명시적 전진 의도를 만들지 않았습니다.");
        Require(
            livenessSession.PreparePlannedRound(1, out string livenessReason)
            && livenessSession.TryExecutePlannedCommand(
                new OffenseBattleCommand(
                    1,
                    rearEnemy.PersistentId,
                    livenessIntents[0].actionType,
                    livenessIntents[0].targetCharacterId,
                    livenessIntents[0].actionId),
                out OffenseBattleCommandResult livenessResult)
            && livenessResult.Accepted
            && rearEnemy.Formation == OffenseFormationSlot.Middle
            && livenessSession.LastProcessedCommandId == 1,
            "후열 근접 적의 typed 전진 명령이 진형과 command ID를 진행시키지 않았습니다. "
                + livenessReason);

        RecordingResolutionAdapter adapter = new RecordingResolutionAdapter();
        OffenseBattleDirector director = new OffenseBattleDirector(adapter);
        OffenseBattleMemberDeckSeed[] party =
        {
            CreateDeck("ally:1", OffenseFormationPosition.FrontLeft),
            CreateDeck("ally:2", OffenseFormationPosition.MiddleLeft)
        };
        OffenseEnemyIntentStateData[] intents =
        {
            CreateIntent("intent:1", "enemy:1", "ally:1"),
            CreateIntent("intent:2", "enemy:2", "ally:2")
        };
        Require(director.TryStartBattle(
                "battle:test",
                party,
                intents,
                91,
                out string reason),
            reason);
        Require(director.TryDrawTurn(out reason), reason);
        Require(director.State.decks.All(deck => deck.candidates.Count == 2),
            "각 캐릭터가 후보 카드 두 장을 뽑지 않았습니다.");

        foreach (OffenseCommandDeckStateData deck in director.State.decks)
        {
            deck.candidates[0].actionType = OffenseBattleActionType.Advance;
            Require(director.TryCommitCommand(
                    deck.characterId,
                    deck.candidates[0].instanceId,
                    "intent:1",
                    "enemy:1",
                    out reason),
                reason);
        }

        IReadOnlyList<OffenseResolvedCommand> resolved = director.ResolveTurn();
        Require(resolved.Count == 2, "두 아군 명령이 모두 해결되지 않았습니다.");
        Require(
            resolved[0].execution.Outcome == OffenseCommandOutcome.ClashLost
            && resolved[1].execution.Outcome == OffenseCommandOutcome.Executed
            && adapter.Requests.Count(request =>
                request.actionType == OffenseBattleActionType.Advance) == 1,
            "clash를 통과한 카드의 typed 전진 행동이 resolution request에 보존되지 않았습니다.");
        Require(
            adapter.Requests.Count(request => request.actorId == "enemy:1") == 1,
            "하나의 적 의도가 여러 번 실행되었습니다.");
        Require(
            adapter.Requests.Count(request => request.actorId == "enemy:2") == 1,
            "가로채지 않은 적 의도가 실행되지 않았습니다.");
        Require(adapter.FinalizeCount == 1, "한 명령열이 한 번만 마감되지 않았습니다.");

        RecordingResolutionAdapter unavailableAdapter =
            new RecordingResolutionAdapter();
        unavailableAdapter.UnavailableActorIds.Add("ally:unavailable");
        unavailableAdapter.FinalizationFailuresRemaining = 1;
        OffenseBattleDirector unavailableDirector =
            new OffenseBattleDirector(unavailableAdapter);
        OffenseBattleMemberDeckSeed unavailableDeck = CreateDeck(
            "ally:unavailable",
            OffenseFormationPosition.FrontLeft);
        foreach (OffenseCommandCardStateData card in unavailableDeck.cards)
        {
            card.executionStages = 3;
            card.speed = 999;
            card.power = 999;
        }
        OffenseEnemyIntentStateData survivingIntent = CreateIntent(
            "intent:survives-unavailable",
            "enemy:survives-unavailable",
            "ally:unavailable");
        survivingIntent.executionStages = 1;
        survivingIntent.speed = 1;
        survivingIntent.threat = 1;
        Require(unavailableDirector.TryStartBattle(
                "battle:unavailable-interception",
                new[] { unavailableDeck },
                new[] { survivingIntent },
                117,
                out reason),
            reason);
        Require(unavailableDirector.TryDrawTurn(out reason), reason);
        OffenseCommandDeckStateData unavailableDraw =
            unavailableDirector.State.decks[0];
        Require(unavailableDirector.TryCommitCommand(
                unavailableDraw.characterId,
                unavailableDraw.candidates[0].instanceId,
                survivingIntent.intentId,
                survivingIntent.enemyId,
                out reason),
            reason);
        IReadOnlyList<OffenseResolvedCommand> unavailableResolved =
            unavailableDirector.ResolveTurn();
        Require(
            unavailableResolved.Count == 1
            && unavailableResolved[0].execution.Outcome
                == OffenseCommandOutcome.Unavailable
            && unavailableResolved[0].execution.FailureReason
                == "focused-unavailable",
            "실행 불가 아군 명령이 typed Unavailable로 보존되지 않았습니다.");
        Require(
            unavailableAdapter.Requests.Count(request =>
                request.actorId == survivingIntent.enemyId
                && request.survivingExecutionStages
                    == survivingIntent.executionStages) == 1,
            "실행 불가 아군 명령이 가로챈 적 의도를 공짜로 소모했습니다.");
        Require(
            unavailableDirector.LastResolvedEnemyIntents.Count == 1
            && unavailableDirector.LastResolvedEnemyIntents[0]
                .intentId == survivingIntent.intentId
            && unavailableDirector.LastResolvedEnemyIntents[0]
                .requestedExecutionStages == survivingIntent.executionStages
            && unavailableDirector.LastResolvedEnemyIntents[0]
                .retainedFullExecutionStages
            && unavailableDirector.LastResolvedEnemyIntents[0]
                .execution.Outcome == OffenseCommandOutcome.Executed,
            "실행 불가 가로채기 뒤 적 의도의 full-stage typed 결과가 보존되지 않았습니다.");
        Require(
            !unavailableDirector.LastTurnFinalization.Succeeded
            && unavailableDirector.LastTurnFinalization.FailureReason
                == "focused-finalization-failure"
            && unavailableDirector.State.commandQueue.Count == 1,
            "마감 실패가 typed reason과 재시도 가능한 명령열을 보존하지 않았습니다.");
        int requestCountAfterResolution = unavailableAdapter.Requests.Count;
        int failedFinalizeCount = unavailableAdapter.FinalizeCount;
        IReadOnlyList<OffenseResolvedCommand> retriedResolution =
            unavailableDirector.ResolveTurn();
        Require(
            unavailableDirector.LastTurnFinalization.Succeeded
            && unavailableAdapter.FinalizeCount == failedFinalizeCount + 1
            && unavailableAdapter.Requests.Count == requestCountAfterResolution
            && unavailableDirector.State.commandQueue.Count == 0
            && retriedResolution.Count == unavailableResolved.Count,
            "마감 재시도가 명령·적 의도를 중복 실행하거나 완료 커밋에 실패했습니다.");
        int finalizeCountAfterResolution = unavailableAdapter.FinalizeCount;
        IReadOnlyList<OffenseResolvedCommand> duplicateResolution =
            unavailableDirector.ResolveTurn();
        Require(
            ReferenceEquals(duplicateResolution, unavailableDirector.LastResolvedTurn)
            && unavailableAdapter.Requests.Count == requestCountAfterResolution
            && unavailableAdapter.FinalizeCount == finalizeCountAfterResolution,
            "완료된 같은 명령열을 두 번 resolve하여 planned round를 중복 처리했습니다.");

        RecordingResolutionAdapter terminalAdapter =
            new RecordingResolutionAdapter();
        OffenseBattleDirector terminalDirector =
            new OffenseBattleDirector(terminalAdapter);
        OffenseBattleMemberDeckSeed terminalDeck = CreateDeck(
            "ally:terminal-reentry",
            OffenseFormationPosition.FrontLeft);
        OffenseEnemyIntentStateData terminalIntent = CreateIntent(
            "intent:terminal-reentry",
            "enemy:terminal-reentry",
            terminalDeck.characterId);
        Require(terminalDirector.TryStartBattle(
                "battle:terminal-reentry",
                new[] { terminalDeck },
                new[] { terminalIntent },
                221,
                out reason),
            reason);
        Require(terminalDirector.TryDrawTurn(out reason), reason);
        OffenseBattleDirectorStateData terminalStateSnapshot =
            terminalDirector.State;
        Require(terminalDirector.TryCommitCommand(
                terminalDeck.characterId,
                terminalStateSnapshot.decks[0].candidates[0].instanceId,
                terminalIntent.intentId,
                terminalIntent.enemyId,
                out reason),
            reason);
        terminalAdapter.FinalizeAction = terminalDirector.Clear;
        IReadOnlyList<OffenseResolvedCommand> terminalResolved =
            terminalDirector.ResolveTurn();
        Require(terminalDirector.State == null
            && terminalResolved.Count == 1
            && terminalDirector.LastResolvedTurn.Count == 1
            && terminalDirector.LastTurnFinalization.Succeeded,
            "전투 terminal 재진입이 완료 trace를 유실하거나 예외 없이 종료되지 못했습니다.");
        Require(terminalStateSnapshot.commandQueue.Count == 0
            && terminalStateSnapshot.decks[0].candidates.Count == 0
            && terminalStateSnapshot.finalizedTurn == terminalStateSnapshot.turn,
            "terminal 중 제거된 director 상태의 카드·queue·finalized fence가 exact cleanup되지 않았습니다.");

        RecordingResolutionAdapter replacementAdapter =
            new RecordingResolutionAdapter();
        OffenseBattleDirector replacementDirector =
            new OffenseBattleDirector(replacementAdapter);
        OffenseBattleMemberDeckSeed replacedDeck = CreateDeck(
            "ally:replaced-state",
            OffenseFormationPosition.FrontLeft);
        OffenseEnemyIntentStateData replacedIntent = CreateIntent(
            "intent:replaced-state",
            "enemy:replaced-state",
            replacedDeck.characterId);
        Require(replacementDirector.TryStartBattle(
                "battle:replaced-state-old",
                new[] { replacedDeck },
                new[] { replacedIntent },
                222,
                out reason),
            reason);
        Require(replacementDirector.TryDrawTurn(out reason), reason);
        Require(replacementDirector.TryCommitCommand(
                replacedDeck.characterId,
                replacementDirector.State.decks[0].candidates[0].instanceId,
                replacedIntent.intentId,
                replacedIntent.enemyId,
                out reason),
            reason);
        OffenseBattleMemberDeckSeed replacementDeck = CreateDeck(
            "ally:replacement-state",
            OffenseFormationPosition.MiddleLeft);
        OffenseEnemyIntentStateData replacementIntent = CreateIntent(
            "intent:replacement-state",
            "enemy:replacement-state",
            replacementDeck.characterId);
        OffenseBattleDirectorStateData replacementState = null;
        replacementAdapter.FinalizeAction = () =>
        {
            replacementDirector.Clear();
            if (!replacementDirector.TryStartBattle(
                    "battle:replacement-state-new",
                    new[] { replacementDeck },
                    new[] { replacementIntent },
                    223,
                    out string replacementReason)
                || !replacementDirector.TryDrawTurn(out replacementReason))
            {
                throw new InvalidOperationException(replacementReason);
            }

            replacementState = replacementDirector.State;
        };
        string replacementFailure = string.Empty;
        try
        {
            replacementDirector.ResolveTurn();
        }
        catch (InvalidOperationException exception)
        {
            replacementFailure = exception.Message;
        }

        Require(replacementFailure.Contains("state was replaced")
            && ReferenceEquals(replacementDirector.State, replacementState)
            && replacementState != null
            && replacementState.turn == 1
            && replacementState.commandQueue.Count == 0
            && replacementState.decks[0].candidates.Count == 2
            && replacementDirector.LastResolvedTurn.Count == 0
            && replacementDirector.LastResolvedEnemyIntents.Count == 0
            && replacementDirector.LastTurnFinalization.Succeeded,
            "이전 finalizer가 non-null replacement state의 pending/trace/card 소유권을 덮어썼습니다.");
        Require(director.TryReplaceEnemyIntents(
                new[] { CreateIntent("intent:next", "enemy:1", "ally:2") },
                out reason),
            reason);
        Require(director.TryDrawTurn(out reason), reason);
        Require(director.State.turn == 2
                && director.LastResolvedTurn.Count == 2,
            "다음 턴 draw가 완료된 직전 resolution trace를 지웠습니다.");
    }

    private static void VerifyPhysicalExpeditionPacking()
    {
        GameObject stagingObject =
            new GameObject("OffenseExpeditionStagingFixture");
        try
        {
            ExteriorZoneMarker staging =
                stagingObject.AddComponent<ExteriorZoneMarker>();
            typeof(BuildableObject)
                .GetProperty(
                    nameof(BuildableObject.centerPos),
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(staging, new Vector2Int(9, 4));
            RecordingProductionItemGateway items =
                new RecordingProductionItemGateway();
            RecordingOffenseSupplyPhysicalCustodyGateway custody = new();
            FacilityBufferDestinationClaimRegistry destinationClaims = new();
            DungeonOffensePreparationService preparation =
                CreatePreparationService(
                    new EmptyWarehouseInventoryQuery(),
                    items,
                    new FixedExteriorZoneQuery(staging),
                    destinationClaims,
                    custody,
                    out FacilityBufferMassAdmissionService destinationCapacities);
            OffenseSupplyLoadout loadout = new OffenseSupplyLoadout();
            loadout.Add(OffenseSupplyType.Rations, 2);

            Require(
                preparation.TryCommitLoadout(
                    loadout,
                    new OffenseExpeditionPreparation(supplyCapacity: 6),
                    "packing:cancel",
                    out string message),
                message);
            string cancelDestination = "expedition:packing:cancel";
            Require(
                destinationClaims.TryGetClaim(
                    cancelDestination,
                    staging.centerPos,
                    out FacilityBufferDestinationClaim cancelClaim)
                && cancelClaim.AnchorKind
                    == FacilityBufferDestinationAnchorKind.ReservedTarget
                && string.Equals(
                    cancelClaim.OwnerDomain,
                    "offense.expedition-supply",
                    StringComparison.Ordinal)
                && string.Equals(
                    cancelClaim.OwnerOperationId,
                    "packing:cancel",
                    StringComparison.Ordinal)
                && cancelClaim.OwnerFacilityId == null
                && destinationCapacities.TryGetCapacity(
                    cancelDestination,
                    staging.centerPos,
                    out FacilityBufferMassCapacitySnapshot cancelCapacity)
                && cancelCapacity.Profile.MaxMassGrams == 2_000L
                && cancelCapacity.Profile.CapacityRevision == 1L
                && cancelCapacity.Profile.OwnerFacilityId == null,
                "원정 보급 집결지의 exact ReservedTarget claim/profile pair가 없습니다.");
            OffenseSupplyPackingSnapshot pending =
                preparation.GetPackingSnapshot("packing:cancel");
            Require(
                pending.IsInTransit
                && pending.Delivered == 0
                && pending.Required == 2
                && !preparation.IsPackageReady("packing:cancel"),
                "운반 전 보급품이 집결 완료로 처리됐습니다.");
            Require(
                items.LastDestinationPosition == staging.centerPos,
                "보급 운반 목적지가 출정 집결지가 아닙니다.");
            IReadOnlyList<OffenseSupplyPackingStateData> savedPacking =
                preparation.CapturePackingState();
            Require(
                savedPacking.Count == 1
                && savedPacking[0].costs.Sum(cost => cost.amount) == 2,
                "운반 중 보급 패키지가 저장되지 않았습니다.");

            OffenseSupplyPackingStateData transactionPackage =
                new OffenseSupplyPackingStateData
                {
                    packageId = "packing:transaction-restored",
                    destinationId = "expedition:packing:transaction-restored",
                    stagingX = staging.centerPos.x,
                    stagingY = staging.centerPos.y,
                    consumed = false,
                    costs = new List<OffenseSupplyPackingItemStateData>
                    {
                        new OffenseSupplyPackingItemStateData
                        {
                            itemId = "food:preserved-ration",
                            amount = 2
                        }
                    }
            };
            preparation.BeginRestoreCandidate();
            destinationClaims.BeginRestoreCandidate();
            destinationCapacities.BeginRestoreCandidate();
            FacilityBufferDestinationClaim foreignCandidateClaim = new(
                "qa:foreign-buffer-destination",
                new Vector2Int(3, 7),
                "qa.foreign-domain",
                "qa:foreign-operation",
                ownerFacilityId: null,
                FacilityBufferDestinationAnchorKind.ReservedTarget);
            Require(
                destinationClaims.TryClaim(
                    foreignCandidateClaim,
                    out FacilityBufferDestinationClaimFailureCode foreignFailure,
                    out string foreignReason),
                $"foreign candidate claim staging failed: {foreignFailure}: {foreignReason}");
            bool preparationPublished = false;
            bool claimsPublished = false;
            bool capacitiesPublished = false;
            try
            {
                preparation.RestorePackingState(new[] { transactionPackage });
                Require(
                    preparation.GetPackingSnapshot("packing:cancel").Exists
                    && !preparation.GetPackingSnapshot(
                        "packing:transaction-restored").Exists
                    && destinationClaims.TryGetClaim(
                        cancelDestination,
                        staging.centerPos,
                        out _)
                    && destinationCapacities.TryGetCapacity(
                        cancelDestination,
                        staging.centerPos,
                        out _)
                    && !destinationCapacities.TryGetCapacity(
                        transactionPackage.destinationId,
                        transactionPackage.StagingPosition,
                        out _),
                    "restore staging이 publish 전에 live package/claim/profile을 변경했습니다.");

                preparation.PublishRestoreCandidate();
                preparationPublished = true;
                Require(
                    !preparation.GetPackingSnapshot("packing:cancel").Exists
                    && preparation.GetPackingSnapshot(
                        "packing:transaction-restored").Exists
                    && destinationClaims.TryGetClaim(
                        cancelDestination,
                        staging.centerPos,
                        out _),
                    "package participant publish 경계가 claim publish와 분리되지 않았습니다.");

                destinationClaims.PublishRestoreCandidate();
                claimsPublished = true;
                destinationCapacities.PublishRestoreCandidate();
                capacitiesPublished = true;
                Require(
                    destinationClaims.TryGetClaim(
                        transactionPackage.destinationId,
                        transactionPackage.StagingPosition,
                        out FacilityBufferDestinationClaim transactionClaim)
                    && string.Equals(
                        transactionClaim.OwnerOperationId,
                        transactionPackage.packageId,
                        StringComparison.Ordinal)
                    && destinationClaims.TryGetClaim(
                        foreignCandidateClaim.DestinationId,
                        foreignCandidateClaim.DropPosition,
                        out _)
                    && destinationCapacities.TryGetCapacity(
                        transactionPackage.destinationId,
                        transactionPackage.StagingPosition,
                        out FacilityBufferMassCapacitySnapshot restoredCapacity)
                    && restoredCapacity.Profile.MaxMassGrams == 2_000L,
                    "claim/profile participant가 staged 원정 목적지를 publish하지 않았습니다.");
                Require(
                    !destinationClaims.TryClaim(
                        new FacilityBufferDestinationClaim(
                            "qa:post-publish-mutation",
                            new Vector2Int(4, 7),
                            "qa.foreign-domain",
                            "qa:post-publish-operation",
                            ownerFacilityId: null,
                            FacilityBufferDestinationAnchorKind.ReservedTarget),
                        out FacilityBufferDestinationClaimFailureCode
                            postPublishFailure,
                        out _)
                    && postPublishFailure
                        == FacilityBufferDestinationClaimFailureCode
                            .RestoreMutationAfterPublish,
                    "claim registry가 restore publish 뒤 mutation을 fail-loud하지 않았습니다.");

                destinationCapacities.RollbackPublishedRestoreCandidate();
                capacitiesPublished = false;
                destinationClaims.RollbackPublishedRestoreCandidate();
                claimsPublished = false;
                preparation.RollbackPublishedRestoreCandidate();
                preparationPublished = false;
                Require(
                    preparation.GetPackingSnapshot("packing:cancel").Exists
                    && !preparation.GetPackingSnapshot(
                        "packing:transaction-restored").Exists
                    && destinationClaims.TryGetClaim(
                        cancelDestination,
                        staging.centerPos,
                        out _)
                    && !destinationClaims.TryGetClaim(
                        transactionPackage.destinationId,
                        transactionPackage.StagingPosition,
                        out _)
                    && !destinationClaims.TryGetClaim(
                        foreignCandidateClaim.DestinationId,
                        foreignCandidateClaim.DropPosition,
                        out _)
                    && destinationCapacities.TryGetCapacity(
                        cancelDestination,
                        staging.centerPos,
                        out _)
                    && !destinationCapacities.TryGetCapacity(
                        transactionPackage.destinationId,
                        transactionPackage.StagingPosition,
                        out _),
                    "later restore failure 뒤 package/claim/profile live image가 함께 rollback되지 않았습니다.");
            }
            finally
            {
                if (capacitiesPublished)
                    destinationCapacities.RollbackPublishedRestoreCandidate();
                else
                    destinationCapacities.DiscardRestoreCandidate();
                if (claimsPublished)
                    destinationClaims.RollbackPublishedRestoreCandidate();
                else
                    destinationClaims.DiscardRestoreCandidate();
                if (preparationPublished)
                    preparation.RollbackPublishedRestoreCandidate();
                else
                    preparation.DiscardRestoreCandidate();
            }

            FacilityBufferDestinationClaimRegistry restoredDestinationClaims =
                new();
            DungeonOffensePreparationService restoredPreparation =
                CreatePreparationService(
                    new EmptyWarehouseInventoryQuery(),
                    items,
                    new FixedExteriorZoneQuery(staging),
                    restoredDestinationClaims,
                    custody,
                    out FacilityBufferMassAdmissionService restoredCapacities);
            restoredPreparation.RestorePackingState(savedPacking);
            Require(
                restoredPreparation.GetPackingSnapshot("packing:cancel")
                    .IsInTransit
                && items.RequestedAmount == 2
                && restoredDestinationClaims.TryGetClaim(
                    cancelDestination,
                    staging.centerPos,
                    out FacilityBufferDestinationClaim restoredClaim)
                && restoredClaim.AnchorKind
                    == FacilityBufferDestinationAnchorKind.ReservedTarget
                && string.Equals(
                    restoredClaim.OwnerDomain,
                    "offense.expedition-supply",
                    StringComparison.Ordinal)
                && string.Equals(
                    restoredClaim.OwnerOperationId,
                    "packing:cancel",
                    StringComparison.Ordinal)
                && restoredDestinationClaims.CaptureClaims().Count == 1
                && restoredCapacities.TryGetCapacity(
                    cancelDestination,
                    staging.centerPos,
                    out FacilityBufferMassCapacitySnapshot restoredPackageCapacity)
                && restoredPackageCapacity.Profile.MaxMassGrams == 2_000L,
                "로드 후 보급 패키지 또는 기존 물리 예약이 중복 없이 복원되지 않았습니다.");
            preparation = restoredPreparation;
            destinationClaims = restoredDestinationClaims;
            destinationCapacities = restoredCapacities;

            preparation.ReturnSupplies(loadout, "packing:cancel");
            Require(
                items.ReleasedAmount == 2
                && string.Equals(
                    items.LastReleasedDestinationId,
                    cancelDestination,
                    StringComparison.Ordinal)
                && items.LastReleasedPosition == staging.centerPos
                && !preparation.GetPackingSnapshot("packing:cancel").Exists
                && destinationClaims.CaptureClaims().Count == 0
                && destinationCapacities.CaptureProfiles().Count == 0,
                "출정 취소 시 예약 물자가 정상 운반 흐름으로 반환되지 않았습니다.");

            Require(
                preparation.TryCommitLoadout(
                    loadout,
                    new OffenseExpeditionPreparation(supplyCapacity: 6),
                    "packing:depart",
                    out message),
                message);
            items.DeliverAll();
            Require(
                preparation.IsPackageReady("packing:depart"),
                "실제 납품 후에도 원정 보급이 준비되지 않았습니다.");
            Require(
                preparation.TryConsumePackedSupplies(
                    "packing:depart",
                    out message),
                message);
            OffenseSupplyPackingSnapshot consumed =
                preparation.GetPackingSnapshot("packing:depart");
            Require(
                consumed.Consumed
                && custody.TransferredQuantity == 2
                && custody.TransferredMassGrams == 2_000L
                && custody.AcknowledgedTransferCount == 1
                && destinationClaims.CaptureClaims().Count == 0
                && destinationCapacities.CaptureProfiles().Count == 0,
                "실제 출발 시 집결지 보급품을 exact Transfer custody로 넘기지 않았습니다.");

            OffenseSupplyLoadout unownedReturn = new();
            unownedReturn.Add(OffenseSupplyType.Rations, 3);
            bool rejectedUnownedReturn = false;
            try
            {
                preparation.ReturnSupplies(
                    unownedReturn,
                    "packing:depart");
            }
            catch (InvalidOperationException)
            {
                rejectedUnownedReturn = true;
            }
            Require(
                rejectedUnownedReturn
                && custody.ReturnPublicationCount == 0,
                "원정 custody가 소유하지 않은 보급품 반환을 거절하지 않았습니다.");

            OffenseSupplyLoadout returnedLoadout = new();
            returnedLoadout.Add(OffenseSupplyType.Rations, 1);
            preparation.ReturnSupplies(returnedLoadout, "packing:depart");
            IReadOnlyList<OffenseSupplyPackingStateData> returnedState =
                preparation.CapturePackingState();
            OffenseSupplyPackingStateData returnedPackage = returnedState.Single(
                value => string.Equals(
                    value.packageId,
                    "packing:depart",
                    StringComparison.Ordinal));
            Require(
                custody.ReturnedQuantity == 1
                && custody.ReturnedMassGrams == 1_000L
                && custody.ReturnPublicationCount == 1
                && returnedPackage.custodyPhase
                    == (int)OffenseSupplyCustodyPhase.Returned
                && returnedPackage.returnQuantity == 1
                && returnedPackage.returnMassGrams == 1_000L
                && returnedPackage.consumedOrLostMassGrams == 1_000L
                && returnedPackage.returnMassGrams
                    + returnedPackage.consumedOrLostMassGrams
                    == returnedPackage.custodyMassGrams,
                "원정 잔여 보급품의 Source 반환 또는 질량 폐쇄가 exact하지 않습니다.");
            preparation.ReturnSupplies(returnedLoadout, "packing:depart");
            Require(
                custody.ReturnPublicationCount == 1,
                "원정 보급품 반환 재시도가 물리 출력을 중복 생성했습니다.");

            FacilityBufferDestinationClaimRegistry returnRestoreClaims = new();
            RecordingOffenseSupplyPhysicalCustodyGateway returnRestoreCustody =
                new();
            DungeonOffensePreparationService returnRestored =
                CreatePreparationService(
                    new EmptyWarehouseInventoryQuery(),
                    items,
                    new FixedExteriorZoneQuery(staging),
                    returnRestoreClaims,
                    returnRestoreCustody,
                    out _);
            returnRestored.RestorePackingState(returnedState);
            OffenseSupplyPackingStateData roundTrippedReturn =
                returnRestored.CapturePackingState().Single(value =>
                    string.Equals(
                        value.packageId,
                        "packing:depart",
                        StringComparison.Ordinal));
            Require(
                roundTrippedReturn.custodyPhase
                    == (int)OffenseSupplyCustodyPhase.Returned
                && roundTrippedReturn.returnMassGrams == 1_000L
                && roundTrippedReturn.consumedOrLostMassGrams == 1_000L
                && returnRestoreClaims.CaptureClaims().Count == 0,
                "반환 완료 custody의 current-format 저장 복원이 질량 폐쇄를 보존하지 않았습니다.");
            returnRestored.ReturnSupplies(returnedLoadout, "packing:depart");
            Require(
                returnRestoreCustody.ReturnPublicationCount == 0,
                "반환 완료 custody 복원 후 물리 출력이 다시 생성됐습니다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stagingObject);
        }
    }

    private static void VerifyPhysicalExpeditionToolPacking()
    {
        GameObject stagingObject = new GameObject(
            "OffenseExpeditionToolStagingFixture");
        try
        {
            ExteriorZoneMarker staging =
                stagingObject.AddComponent<ExteriorZoneMarker>();
            RecordingProductionItemGateway items =
                new RecordingProductionItemGateway();
            RecordingOffenseSupplyPhysicalCustodyGateway custody = new();
            FacilityBufferDestinationClaimRegistry destinationClaims = new();
            DungeonOffensePreparationService preparation =
                CreatePreparationService(
                    new EmptyWarehouseInventoryQuery(),
                    items,
                    new FixedExteriorZoneQuery(staging),
                    destinationClaims,
                    custody,
                    out _);
            OffenseSupplyLoadout loadout = new OffenseSupplyLoadout();
            loadout.Add(OffenseSupplyType.Tools, 2);

            Require(
                preparation.TryCommitLoadout(
                    loadout,
                    new OffenseExpeditionPreparation(supplyCapacity: 6),
                    "packing:tools",
                    out string message),
                message);
            Require(
                string.Equals(
                    items.RequestedItemId,
                    "tool:field-repair-kit",
                    StringComparison.Ordinal),
                $"expedition tools requested {items.RequestedItemId}");
            items.DeliverAll();
            Require(
                preparation.TryConsumePackedSupplies(
                    "packing:tools",
                    out message),
                message);
            Require(
                custody.TransferredQuantity == 2
                && custody.TransferredMassGrams == 2_000L,
                "field repair kits were not transferred into expedition custody");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stagingObject);
        }
    }

    private static void VerifySaveRoundTrip()
    {
        GameObject stagingObject = new GameObject(
            "OffenseSaveRoundTripStagingFixture");
        ExteriorZoneMarker staging =
            stagingObject.AddComponent<ExteriorZoneMarker>();
        typeof(BuildableObject)
            .GetProperty(
                nameof(BuildableObject.centerPos),
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(staging, new Vector2Int(8, 3));
        try
        {
        const int worldSeed = 170017;
        const string expeditionId = "save:travel";
        EditorContentCatalog catalog = LoadCatalog();
        OffenseHexWorldSimulation world = CreateWorld(catalog, worldSeed);
        OffenseReturnSafetyRuntime safety = new OffenseReturnSafetyRuntime(world);
        OffenseTravelRuntime travel = new OffenseTravelRuntime(world, safety, fieldMedical: null);
        OffenseDecisionRuntime decisions = new OffenseDecisionRuntime(catalog, safety);
        OffenseBattleDirector battle =
            new OffenseBattleDirector(new RecordingResolutionAdapter());
        CapturingMitigationRuntime mitigation =
            new CapturingMitigationRuntime(new[]
            {
                new OffenseUrgentMitigationOrderStateData
                {
                    orderId = "mitigation:save",
                    siteId = "urgent:save",
                    definitionId = catalog.UrgentSites[0].urgentSiteId,
                    facilityPersistentId = "facility:save",
                    facilityX = 4,
                    facilityY = 2,
                    destinationId = OffenseUrgentMitigationInputOwnerAuthority
                        .BuildDestinationId("mitigation:save"),
                    inputBufferCapacityGrams = 3_000L,
                    inputMassAuthorityRevision = 1L,
                    inputCapacityFingerprint =
                        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                    requiredWork = 32f,
                    completedWork = 11f,
                    status = OffenseUrgentMitigationOrderStatus.InProgress,
                    statusText = "완화 작업 중"
                }
            });
        CapturingPreparationService preparation =
            new CapturingPreparationService(new[]
            {
                new OffenseSupplyPackingStateData
                {
                    packageId = "package:save",
                    destinationId = "expedition:package:save",
                    stagingX = 8,
                    stagingY = 3,
                    consumed = false,
                    costs = new List<OffenseSupplyPackingItemStateData>
                    {
                        new OffenseSupplyPackingItemStateData
                        {
                            itemId = "food:preserved-ration",
                            amount = 4
                        },
                        new OffenseSupplyPackingItemStateData
                        {
                            itemId = "medicine:standard",
                            amount = 2
                        }
                    }
                }
            });

        OffenseWorldSiteStateData destination = world.Sites
            .Where(site => site != null && site.IsActive)
            .OrderByDescending(site =>
                world.GetMinimumStepDistance(world.DungeonCoord, site.Coord))
            .First();
        Require(travel.TryCreateExpedition(expeditionId, out string reason), reason);
        Require(
            travel.TrySetDestination(
                expeditionId,
                destination.Coord,
                destination.siteId,
                OffenseTravelProfile.Default,
                startsSiteAttack: false,
                out reason),
            reason);
        safety.GrantForObjective(expeditionId, destination.Coord, world.DungeonCoord);
        Require(
            travel.TryAdvanceOneStep(
                expeditionId,
                forcedMovement: true,
                out _,
                out reason),
            reason);
        safety.RecordProtectedDangerousEvent(expeditionId, forcedCombat: true);
        Require(travel.TryAdjustExposure(expeditionId, 37f, out _),
            "이동 노출도를 변경하지 못했습니다.");
        Require(
            decisions.TryCreateDecision(
                new OffenseDecisionContext
                {
                    expeditionId = expeditionId,
                    sequence = 3,
                    stage = OffenseDecisionStage.Travel,
                    protectedMovement = true,
                    canGenerateForcedCombat = false
                },
                out _,
                out reason),
            reason);

        OffenseBattleMemberDeckSeed[] party =
        {
            CreateDeck("save:ally:1", OffenseFormationPosition.FrontLeft),
            CreateDeck("save:ally:2", OffenseFormationPosition.MiddleLeft)
        };
        OffenseEnemyIntentStateData[] intents =
        {
            CreateIntent("save:intent:1", "save:enemy:1", "save:ally:1")
        };
        Require(
            battle.TryStartBattle(
                "save:battle",
                party,
                intents,
                deterministicSeed: 731,
                out reason),
            reason);
        Require(battle.TryDrawTurn(out reason), reason);
        OffenseCommandDeckStateData firstDeck = battle.State.decks[0];
        Require(
            battle.TryCommitCommand(
                firstDeck.characterId,
                firstDeck.candidates[0].instanceId,
                intents[0].intentId,
                intents[0].enemyId,
                out reason),
            reason);

        OffenseWorldStateSaveCodec sourceSection = new OffenseWorldStateSaveCodec(
            world,
            travel,
            safety,
            decisions,
            battle,
            mitigation,
            preparation,
            EditorRuntimeReferenceFixtures.OffenseWithExpedition,
            new OffenseFieldMedicalRuntime());
        OffenseWorldSaveData sourceState = sourceSection.CaptureState();
        string sourceJson = JsonUtility.ToJson(sourceState);

        OffenseHexWorldSimulation restoredWorld =
            CreateWorld(catalog, worldSeed + 1);
        OffenseReturnSafetyRuntime restoredSafety =
            new OffenseReturnSafetyRuntime(restoredWorld);
        OffenseTravelRuntime restoredTravel =
            new OffenseTravelRuntime(restoredWorld, restoredSafety, fieldMedical: null);
        OffenseDecisionRuntime restoredDecisions =
            new OffenseDecisionRuntime(catalog, restoredSafety);
        OffenseBattleDirector restoredBattle =
            new OffenseBattleDirector(new RecordingResolutionAdapter());
        OffenseUrgentMitigationRuntime restoredMitigation =
            new OffenseUrgentMitigationRuntime(
                restoredWorld,
                catalog,
                new FixedBuildingWorldQuery(),
                new RecordingProductionItemGateway(),
                new RecordingUrgentMitigationInputOwnerRuntime(),
                new MutableGameClock(),
                workforce: null,
                facilityCandidates: null);
        FacilityBufferDestinationClaimRegistry restoredDestinationClaims = new();
        DungeonOffensePreparationService restoredPreparation =
            CreatePreparationService(
                new EmptyWarehouseInventoryQuery(),
                new RecordingProductionItemGateway(),
                new FixedExteriorZoneQuery(staging),
                restoredDestinationClaims,
                new RecordingOffenseSupplyPhysicalCustodyGateway(),
                out _);
        OffenseWorldStateSaveCodec restoredSection = new OffenseWorldStateSaveCodec(
            restoredWorld,
            restoredTravel,
            restoredSafety,
            restoredDecisions,
            restoredBattle,
            restoredMitigation,
            restoredPreparation,
            EditorRuntimeReferenceFixtures.OffenseWithExpedition,
            new OffenseFieldMedicalRuntime());
        DungeonGameRestoreReport restoreReport = new DungeonGameRestoreReport();
        OffenseWorldRuntimeRestoreCandidate restoreCandidate =
            restoredSection.BuildRestoreCandidate(sourceState, restoreReport);
        Require(
            restoreReport.Success,
            "Strategic state restore candidate validation failed.");
        restoredSection.PublishRestoreCandidate(restoreCandidate);
        string restoredJson = restoredSection.Capture();

        Require(
            string.Equals(sourceJson, restoredJson, StringComparison.Ordinal),
            "Strategic 중간 상태가 저장 후 동일하게 복원되지 않았습니다.");
        Require(
            restoredTravel.TryGetState(
                expeditionId,
                out OffenseTravelStateData restoredTravelState)
            && Mathf.Approximately(restoredTravelState.exposure, 37f)
            && restoredTravelState.remainingPath.Count > 0,
            "이동 경로와 노출도가 복원되지 않았습니다.");
        Require(
            restoredSafety.Get(expeditionId).ForcedCombatCount == 1
            && restoredSafety.Get(expeditionId).NonCombatPitySteps == 2,
            "안전 이동 pity 상태가 복원되지 않았습니다.");
        Require(
            restoredDecisions.TryGetActiveDecision(expeditionId, out _),
            "미해결 선택 카드가 복원되지 않았습니다.");
        Require(
            restoredBattle.State != null
            && restoredBattle.State.commandQueue.Count == 1,
            "확정 전 명령열 전투가 복원되지 않았습니다.");
        Require(
            restoredMitigation.Orders.Count == 1
            && Mathf.Approximately(
                restoredMitigation.Orders[0].completedWork,
                11f),
            "긴급 거점 완화 주문이 복원되지 않았습니다.");
        Require(
            restoredPreparation.CapturePackingState().Count == 1,
            "집결 중인 실물 보급 패키지가 복원되지 않았습니다.");
        Require(
            restoredDestinationClaims.TryGetClaim(
                "expedition:package:save",
                staging.centerPos,
                out FacilityBufferDestinationClaim restoredClaim)
            && restoredClaim.AnchorKind
                == FacilityBufferDestinationAnchorKind.ReservedTarget
            && string.Equals(
                restoredClaim.OwnerDomain,
                "offense.expedition-supply",
                StringComparison.Ordinal)
            && string.Equals(
                restoredClaim.OwnerOperationId,
                "package:save",
                StringComparison.Ordinal)
            && restoredClaim.OwnerFacilityId == null,
            "집결 중인 보급 패키지의 exact ReservedTarget claim이 복원되지 않았습니다.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(stagingObject);
        }
    }

    private static OffenseBattleMemberDeckSeed CreateDeck(
        string characterId,
        OffenseFormationPosition formation)
    {
        return new OffenseBattleMemberDeckSeed
        {
            characterId = characterId,
            formation = formation,
            cards = Enumerable.Range(0, 8)
                .Select(index => new OffenseCommandCardStateData
                {
                    instanceId = $"{characterId}:card:{index}",
                    actionType = OffenseBattleActionType.BasicAttack,
                    sourceSkillId = string.Empty,
                    displayName = $"검증 카드 {index + 1}",
                    tacticalTag = (OffenseTacticalTag)(1 + index % 5),
                    damageType = CombatDamageType.Slash,
                    executionStages = 1,
                    speed = 1,
                    power = 1
                })
                .ToList()
        };
    }

    private static OffenseEnemyIntentStateData CreateIntent(
        string intentId,
        string enemyId,
        string targetId)
    {
        return new OffenseEnemyIntentStateData
        {
            intentId = intentId,
            enemyId = enemyId,
            targetCharacterId = targetId,
            actionType = OffenseBattleActionType.BasicAttack,
            displayName = "검증 공격",
            tacticalTag = OffenseTacticalTag.Break,
            executionStages = 3,
            speed = 99,
            threat = 99
        };
    }

    private static OffenseHexWorldSimulation CreateWorld(
        EditorContentCatalog catalog,
        int seed)
    {
        OffenseHexWorldSimulation world = new OffenseHexWorldSimulation(
            EditorRuntimeReferenceFixtures.DungeonWithRunVariables,
            catalog,
            new GameEventBus());
        world.Initialize(seed);
        return world;
    }

    private static OffenseHexCoord FindReachableEmptyCoord(
        OffenseHexWorldSimulation world)
    {
        HashSet<OffenseHexCoord> occupied = world.Sites
            .Where(site => site != null && site.IsActive)
            .Select(site => site.Coord)
            .ToHashSet();
        return world.Tiles
            .Where(tile => tile != null
                && !tile.blocked
                && tile.Coord != world.DungeonCoord
                && !occupied.Contains(tile.Coord)
                && world.GetMinimumStepDistance(
                    world.DungeonCoord,
                    tile.Coord) is >= 1 and <= 12)
            .OrderBy(tile => tile.Coord.DistanceTo(world.DungeonCoord))
            .ThenBy(tile => tile.q)
            .ThenBy(tile => tile.r)
            .Select(tile => tile.Coord)
            .First();
    }

    private static EditorContentCatalog LoadCatalog()
    {
        return new EditorContentCatalog(
            LoadAssets<OffenseSiteArchetypeSO>(),
            LoadAssets<OffenseUrgentSiteDefinitionSO>(),
            LoadAssets<OffenseDecisionCardSO>(),
            LoadAssets<OffenseEncounterSO>());
    }

    private static IReadOnlyList<T> LoadAssets<T>()
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .OrderBy(asset => AssetDatabase.GetAssetPath(asset), StringComparer.Ordinal)
            .ToArray();
    }

    private static void Run(
        string label,
        Action scenario,
        ICollection<string> passed)
    {
        scenario();
        passed.Add(label);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireApproximately(
        float actual,
        float expected,
        string label)
    {
        Require(
            Mathf.Abs(actual - expected) <= 0.0001f,
            $"{label}: expected={expected}, actual={actual}");
    }

    private static void RequireUnique(
        IEnumerable<string> values,
        string label)
    {
        string[] ids = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        Require(ids.Length == ids.Distinct(StringComparer.Ordinal).Count(),
            $"{label}가 중복되었습니다.");
    }

    private sealed class CapturingMitigationRuntime :
        IOffenseUrgentMitigationRuntime
    {
        private readonly List<OffenseUrgentMitigationOrderStateData> orders =
            new List<OffenseUrgentMitigationOrderStateData>();

        public CapturingMitigationRuntime(
            IEnumerable<OffenseUrgentMitigationOrderStateData> initial = null)
        {
            Replace(initial?.ToArray()
                ?? Array.Empty<OffenseUrgentMitigationOrderStateData>());
        }

        public event Action Changed
        {
            add { }
            remove { }
        }

        public int Version { get; private set; }
        public IReadOnlyList<OffenseUrgentMitigationOrderStateData> Orders =>
            orders;

        public bool TryStart(string siteId, out string message)
        {
            message = "검증용 런타임에서는 시작하지 않습니다.";
            return false;
        }

        public bool TryCancel(string siteId, out string message)
        {
            message = "검증용 런타임에서는 취소하지 않습니다.";
            return false;
        }

        public bool TryGetOrder(
            string siteId,
            out OffenseUrgentMitigationOrderStateData order)
        {
            order = orders.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.siteId,
                    siteId,
                    StringComparison.Ordinal));
            return order != null;
        }

        public bool TryGetWork(
            BuildableObject facility,
            CharacterActor worker,
            out OffenseUrgentMitigationWorkSnapshot work)
        {
            work = default;
            return false;
        }

        public bool ApplyWork(
            BuildableObject facility,
            CharacterActor worker,
            float amount,
            out bool completed)
        {
            completed = false;
            return false;
        }

        public IReadOnlyList<OffenseUrgentMitigationOrderStateData> Capture()
        {
            return orders.Select(Clone).ToArray();
        }

        private void Replace(
            IReadOnlyList<OffenseUrgentMitigationOrderStateData> restored)
        {
            orders.Clear();
            orders.AddRange((restored
                    ?? Array.Empty<OffenseUrgentMitigationOrderStateData>())
                .Where(order => order != null)
                .Select(Clone));
            Version++;
        }

        private static OffenseUrgentMitigationOrderStateData Clone(
            OffenseUrgentMitigationOrderStateData source)
        {
            return new OffenseUrgentMitigationOrderStateData
            {
                orderId = source.orderId,
                siteId = source.siteId,
                definitionId = source.definitionId,
                facilityPersistentId = source.facilityPersistentId,
                facilityX = source.facilityX,
                facilityY = source.facilityY,
                destinationId = source.destinationId,
                inputBufferCapacityGrams = source.inputBufferCapacityGrams,
                inputMassAuthorityRevision =
                    source.inputMassAuthorityRevision,
                inputCapacityFingerprint = source.inputCapacityFingerprint,
                requiredWork = source.requiredWork,
                completedWork = source.completedWork,
                status = source.status,
                statusText = source.statusText,
                physicalCommitPhase = source.physicalCommitPhase,
                physicalOperationId = source.physicalOperationId,
                physicalCommitId = source.physicalCommitId,
                inputQuantity = source.inputQuantity,
                inputMassGrams = source.inputMassGrams,
                physicalReceiptAcknowledged =
                    source.physicalReceiptAcknowledged,
                mitigationBefore = source.mitigationBefore,
                mitigationAfter = source.mitigationAfter
            };
        }
    }

    private sealed class CapturingPreparationService :
        IOffensePreparationService
    {
        private readonly List<OffenseSupplyPackingStateData> packages =
            new List<OffenseSupplyPackingStateData>();

        public CapturingPreparationService(
            IEnumerable<OffenseSupplyPackingStateData> initial = null)
        {
            RestorePackingState(initial);
        }

        public OffensePreparationSnapshot Evaluate()
        {
            return new OffensePreparationSnapshot(
                new OffenseExpeditionPreparation(),
                new Dictionary<OffenseSupplyType, int>());
        }

        public OffenseSupplyPackingSnapshot GetPackingSnapshot(string packageId)
        {
            OffenseSupplyPackingStateData package = packages.FirstOrDefault(
                candidate => candidate != null
                    && string.Equals(
                        candidate.packageId,
                        packageId,
                        StringComparison.Ordinal));
            int required = package?.costs?.Sum(cost =>
                Mathf.Max(0, cost?.amount ?? 0)) ?? 0;
            return package != null
                ? new OffenseSupplyPackingSnapshot(
                    package.packageId,
                    required,
                    delivered: 0,
                    package.consumed)
                : default;
        }

        public bool IsPackageReady(string packageId) => false;

        public bool TryCommitLoadout(
            OffenseSupplyLoadout loadout,
            OffenseExpeditionPreparation preparation,
            string packageId,
            out string message)
        {
            message = "검증용 런타임에서는 보급을 등록하지 않습니다.";
            return false;
        }

        public bool TryConsumePackedSupplies(
            string packageId,
            out string message)
        {
            message = "검증용 런타임에서는 보급을 소비하지 않습니다.";
            return false;
        }

        public void ConsumePackedSupplies(string packageId)
        {
        }

        public void AbandonPackedSupplies(string packageId)
        {
        }

        public void ReturnSupplies(
            OffenseSupplyLoadout loadout,
            string packageId = "")
        {
        }

        public void DepositLoot(
            IReadOnlyDictionary<StockCategory, int> loot)
        {
        }

        public IReadOnlyList<OffenseSupplyPackingStateData> CapturePackingState()
        {
            return packages.Select(Clone).ToArray();
        }

        public void RestorePackingState(
            IEnumerable<OffenseSupplyPackingStateData> restored,
            DungeonGameRestoreReport report = null)
        {
            packages.Clear();
            packages.AddRange((restored
                    ?? Array.Empty<OffenseSupplyPackingStateData>())
                .Where(package => package != null)
                .Select(Clone));
        }

        private static OffenseSupplyPackingStateData Clone(
            OffenseSupplyPackingStateData source)
        {
            return new OffenseSupplyPackingStateData
            {
                packageId = source.packageId,
                destinationId = source.destinationId,
                stagingX = source.stagingX,
                stagingY = source.stagingY,
                consumed = source.consumed,
                custodyPhase = source.custodyPhase,
                custodyOperationId = source.custodyOperationId,
                custodyReasonCode = source.custodyReasonCode,
                custodyCommitId = source.custodyCommitId,
                custodySourceStackIds = new List<string>(
                    source.custodySourceStackIds ?? new List<string>()),
                custodyQuantity = source.custodyQuantity,
                custodyMassGrams = source.custodyMassGrams,
                custodyAcknowledged = source.custodyAcknowledged,
                returnOperationId = source.returnOperationId,
                returnReasonCode = source.returnReasonCode,
                returnX = source.returnX,
                returnY = source.returnY,
                returnOutputCommitIds = new List<string>(
                    source.returnOutputCommitIds ?? new List<string>()),
                returnQuantity = source.returnQuantity,
                returnMassGrams = source.returnMassGrams,
                consumedOrLostMassGrams = source.consumedOrLostMassGrams,
                returnedCosts = (source.returnedCosts
                        ?? new List<OffenseSupplyPackingItemStateData>())
                    .Where(cost => cost != null)
                    .Select(cost => new OffenseSupplyPackingItemStateData
                    {
                        itemId = cost.itemId,
                        amount = cost.amount
                    })
                    .ToList(),
                costs = (source.costs
                        ?? new List<OffenseSupplyPackingItemStateData>())
                    .Where(cost => cost != null)
                    .Select(cost => new OffenseSupplyPackingItemStateData
                    {
                        itemId = cost.itemId,
                        amount = cost.amount
                    })
                    .ToList()
            };
        }
    }

    private sealed class FixedBuildingWorldQuery : IBuildingWorldQuery
    {
        private readonly IReadOnlyList<BuildableObject> buildings;

        public FixedBuildingWorldQuery(params BuildableObject[] buildings)
        {
            this.buildings = buildings ?? Array.Empty<BuildableObject>();
        }

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings;
    }

    private sealed class RecordingUrgentMitigationInputOwnerRuntime :
        IOffenseUrgentMitigationInputOwnerRuntime
    {
        private const string Fingerprint =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        public bool TryEnsure(
            OffenseUrgentMitigationOrderStateData order,
            BuildableObject facility,
            out string failureReason)
        {
            if (order == null
                || facility == null
                || !string.Equals(
                    order.destinationId,
                    OffenseUrgentMitigationInputOwnerAuthority
                        .BuildDestinationId(order.orderId),
                    StringComparison.Ordinal))
            {
                failureReason = "qa-urgent-input-owner-invalid";
                return false;
            }
            order.inputBufferCapacityGrams = 3_000L;
            order.inputMassAuthorityRevision = 1L;
            order.inputCapacityFingerprint = Fingerprint;
            failureReason = string.Empty;
            return true;
        }

        public bool TryRetire(
            OffenseUrgentMitigationOrderStateData order,
            string reasonCode,
            out string failureReason)
        {
            OffenseUrgentMitigationInputOwnerAuthority
                .ClearStoredProjection(order);
            failureReason = string.Empty;
            return true;
        }

        public bool TryReplaceForRestore(
            IReadOnlyList<OffenseUrgentMitigationOrderStateData> orders,
            out string failureReason)
        {
            failureReason = string.Empty;
            return (orders ?? Array.Empty<
                    OffenseUrgentMitigationOrderStateData>())
                .All(order => order != null
                    && order.inputBufferCapacityGrams > 0L
                    && order.inputMassAuthorityRevision > 0L
                    && order.inputCapacityFingerprint?.Length == 64);
        }

        public bool TryValidateForCapture(
            IReadOnlyList<OffenseUrgentMitigationOrderStateData> orders,
            IReadOnlyList<BuildableObject> facilities,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class EmptyWarehouseInventoryQuery :
        IFacilityEvolutionWarehouseInventoryQuery
    {
        public IReadOnlyList<IWarehouseFacility> GetWarehouses()
        {
            return Array.Empty<IWarehouseFacility>();
        }

        public bool TryGetPending(
            string operationId,
            string reasonCode,
            out FacilityEvolutionMaterialCommitReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = string.Empty;
            return false;
        }

        public bool TryCommitPending(
            IReadOnlyList<FacilityEvolutionMaterialDebit> debits,
            string operationId,
            string reasonCode,
            out FacilityEvolutionMaterialCommitReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "offense-empty-warehouse-material-commit";
            return false;
        }

        public bool Acknowledge(string commitId, out string failureReason)
        {
            failureReason = string.Empty;
            return false;
        }
    }

    private sealed class FixedExteriorZoneQuery : IExteriorZoneQuery
    {
        private readonly ExteriorZoneMarker staging;

        public FixedExteriorZoneQuery(ExteriorZoneMarker staging)
        {
            this.staging = staging;
        }

        public IReadOnlyList<ExteriorZoneMarker> Zones =>
            staging != null
                ? new[] { staging }
                : Array.Empty<ExteriorZoneMarker>();

        public IEnumerable<ExteriorZoneMarker> GetZones(
            ExteriorZoneType zoneType)
        {
            return staging != null
                && zoneType == ExteriorZoneType.ExpeditionStaging
                    ? new[] { staging }
                    : Array.Empty<ExteriorZoneMarker>();
        }

        public bool TryGetZone(
            ExteriorZoneType zoneType,
            out ExteriorZoneMarker marker)
        {
            marker = zoneType == ExteriorZoneType.ExpeditionStaging
                ? staging
                : null;
            return marker != null;
        }

        public ExteriorActivityOverviewSnapshot GetOverview()
        {
            return default;
        }
    }

    private sealed class MutableGameClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused { get; set; }

        public void Advance(float seconds)
        {
            DeltaTime = Mathf.Max(0f, seconds);
            Time += DeltaTime;
            FrameCount++;
        }
    }

    private sealed class RecordingOffenseSupplyPhysicalCustodyGateway :
        IOffenseSupplyPhysicalCustodyGateway
    {
        private readonly Dictionary<string, OffenseSupplyCustodyReceipt> pending =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> acknowledged =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, PhysicalItemSourcePublicationReceipt>
            publishedReturns = new(StringComparer.Ordinal);

        public int TransferredQuantity { get; private set; }
        public long TransferredMassGrams { get; private set; }
        public int AcknowledgedTransferCount { get; private set; }
        public int ReturnedQuantity { get; private set; }
        public long ReturnedMassGrams { get; private set; }
        public int ReturnPublicationCount { get; private set; }

        public bool TryCommitTransferPending(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            string operationId,
            string reasonCode,
            out OffenseSupplyCustodyReceipt receipt,
            out string failureReason)
        {
            if (pending.TryGetValue(operationId, out receipt))
            {
                failureReason = string.Empty;
                return true;
            }

            KeyValuePair<string, int>[] exactCosts = (costs
                    ?? new Dictionary<string, int>())
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
            int quantity = exactCosts.Sum(pair => pair.Value);
            if (string.IsNullOrWhiteSpace(destinationId)
                || string.IsNullOrWhiteSpace(operationId)
                || string.IsNullOrWhiteSpace(reasonCode)
                || exactCosts.Length == 0
                || quantity <= 0)
            {
                receipt = default;
                failureReason = "offense-custody-fixture-invalid-request";
                return false;
            }

            long mass = checked((long)quantity * 1_000L);
            string commitId =
                $"physical-batch-disposition:1:{operationId}:{quantity}:{mass}";
            string[] sources = exactCosts
                .Select(pair => "fixture-stack:" + pair.Key)
                .ToArray();
            receipt = new OffenseSupplyCustodyReceipt(
                operationId,
                reasonCode,
                commitId,
                sources,
                quantity,
                mass);
            pending.Add(operationId, receipt);
            TransferredQuantity = checked(TransferredQuantity + quantity);
            TransferredMassGrams = checked(TransferredMassGrams + mass);
            failureReason = string.Empty;
            return true;
        }

        public bool TryGetPending(
            string operationId,
            out OffenseSupplyCustodyReceipt receipt) =>
            pending.TryGetValue(operationId ?? string.Empty, out receipt);

        public bool AcknowledgeTransfer(
            string commitId,
            out string failureReason)
        {
            if (acknowledged.Contains(commitId ?? string.Empty))
            {
                failureReason = string.Empty;
                return true;
            }

            KeyValuePair<string, OffenseSupplyCustodyReceipt> match =
                pending.FirstOrDefault(pair => string.Equals(
                    pair.Value.CommitId,
                    commitId,
                    StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(match.Key))
            {
                failureReason = "offense-custody-fixture-receipt-not-found";
                return false;
            }

            pending.Remove(match.Key);
            acknowledged.Add(commitId);
            AcknowledgedTransferCount++;
            failureReason = string.Empty;
            return true;
        }

        public bool TryEnsureReturnOutputs(
            IReadOnlyDictionary<string, int> outputs,
            Vector2Int outputPosition,
            string operationId,
            string reasonCode,
            out PhysicalItemSourcePublicationReceipt receipt,
            out string failureReason)
        {
            if (publishedReturns.TryGetValue(operationId, out receipt))
            {
                failureReason = string.Empty;
                return true;
            }

            KeyValuePair<string, int>[] exactOutputs = (outputs
                    ?? new Dictionary<string, int>())
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToArray();
            int quantity = exactOutputs.Sum(pair => pair.Value);
            if (string.IsNullOrWhiteSpace(operationId)
                || string.IsNullOrWhiteSpace(reasonCode)
                || exactOutputs.Length == 0
                || quantity <= 0)
            {
                receipt = default;
                failureReason = "offense-return-fixture-invalid-request";
                return false;
            }

            long mass = checked((long)quantity * 1_000L);
            string[] commits = exactOutputs
                .Select(pair =>
                    $"physical-source:{operationId}:{pair.Key}:{pair.Value}:{checked((long)pair.Value * 1_000L)}")
                .ToArray();
            receipt = new PhysicalItemSourcePublicationReceipt(
                operationId,
                reasonCode,
                commits,
                quantity,
                mass);
            publishedReturns.Add(operationId, receipt);
            ReturnedQuantity = checked(ReturnedQuantity + quantity);
            ReturnedMassGrams = checked(ReturnedMassGrams + mass);
            ReturnPublicationCount++;
            failureReason = string.Empty;
            return true;
        }
    }

    private static DungeonOffensePreparationService CreatePreparationService(
        IFacilityEvolutionWarehouseInventoryQuery inventory,
        IProductionItemGateway items,
        IExteriorZoneQuery exteriorZones,
        FacilityBufferDestinationClaimRegistry claims,
        IOffenseSupplyPhysicalCustodyGateway custody,
        out FacilityBufferMassAdmissionService capacities)
    {
        capacities = new FacilityBufferMassAdmissionService(
            claims,
            new EmptyFacilityBufferOccupancyQuery());
        FacilityBufferDestinationLifecycleService lifecycle = new(
            claims,
            claims,
            capacities,
            capacities);
        return new DungeonOffensePreparationService(
            inventory,
            items,
            exteriorZones,
            claims,
            capacities,
            lifecycle,
            custody);
    }

    private sealed class EmptyFacilityBufferOccupancyQuery :
        IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "qa-offense-no-physical-lot";
            return false;
        }
    }

    private sealed class RecordingProductionItemGateway :
        IProductionItemGateway
    {
        private string requestedItemId = string.Empty;
        private string requestedDestinationId = string.Empty;
        private int deliveredAmount;
        private string pendingWipOperationId = string.Empty;
        private ProductionWipInputReceipt pendingWipReceipt;
        private readonly HashSet<string> acknowledgedWipCommits =
            new(StringComparer.Ordinal);

        public int RequestedAmount { get; private set; }
        public string RequestedItemId => requestedItemId;
        public int ConsumedAmount { get; private set; }
        public int WipTransferredAmount { get; private set; }
        public long WipTransferredMassGrams { get; private set; }
        public int WipAcknowledgementCount { get; private set; }
        public bool FailNextWipAcknowledgement { get; set; }
        public int ReleasedAmount { get; private set; }
        public Vector2Int LastDestinationPosition { get; private set; }
        public string LastReleasedDestinationId { get; private set; } =
            string.Empty;
        public Vector2Int LastReleasedPosition { get; private set; }

        public bool TryGetStockCategory(
            string itemId,
            out StockCategory category)
        {
            category = StockCategory.General;
            return !string.IsNullOrWhiteSpace(itemId)
                && string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal);
        }

        public int CountDelivered(string itemId, string destinationId)
        {
            return Matches(itemId, destinationId)
                ? deliveredAmount
                : 0;
        }

        public int CountPending(string itemId, string destinationId)
        {
            return Matches(itemId, destinationId)
                ? RequestedAmount
                : 0;
        }

        public long CountPendingMassGrams(string destinationId) =>
            string.Equals(
                requestedDestinationId,
                destinationId,
                StringComparison.Ordinal)
                ? (long)RequestedAmount * 1_000L
                : 0L;

        public long GetDefinitionQuantityMassGrams(
            string itemId,
            int quantity) => checked((long)quantity * 1_000L);

        public int CountAvailableStock(
            string itemId,
            string excludedDestinationId)
        {
            return 999;
        }

        public bool RequestDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            requestedItemId = itemId ?? string.Empty;
            requestedDestinationId = destinationId ?? string.Empty;
            LastDestinationPosition = destinationPosition;
            requested = Mathf.Max(0, amount);
            RequestedAmount += requested;
            failureReason = string.Empty;
            return requested > 0;
        }

        public bool RequestDeliveryWithinMassCapacity(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            long maxDestinationMassGrams,
            out int requested,
            out string failureReason)
        {
            long requestedMass = GetDefinitionQuantityMassGrams(itemId, amount);
            if (CountPendingMassGrams(destinationId) + requestedMass
                > maxDestinationMassGrams)
            {
                requested = 0;
                failureReason =
                    "production-input-buffer-mass-capacity-unavailable";
                return false;
            }
            return RequestDelivery(
                itemId,
                amount,
                destinationPosition,
                destinationId,
                out requested,
                out failureReason);
        }

        public bool ConsumeDelivered(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            out string failureReason)
        {
            int amount = costs?.Values.Sum() ?? 0;
            if (!string.Equals(
                    requestedDestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                || deliveredAmount < amount)
            {
                failureReason = "납품량 부족";
                return false;
            }

            deliveredAmount -= amount;
            ConsumedAmount += amount;
            failureReason = string.Empty;
            return true;
        }

        public bool ConsumeDeliveredToWip(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            string operationId,
            out ProductionWipInputReceipt receipt,
            out string failureReason)
        {
            if (string.Equals(
                    pendingWipOperationId,
                    operationId,
                    StringComparison.Ordinal)
                && pendingWipReceipt.IsCommitted)
            {
                receipt = pendingWipReceipt;
                failureReason = string.Empty;
                return true;
            }

            int amount = costs?.Values.Sum() ?? 0;
            if (string.IsNullOrWhiteSpace(operationId)
                || !string.Equals(
                    requestedDestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                || deliveredAmount < amount
                || amount <= 0)
            {
                receipt = default;
                failureReason = "production-wip-input-missing";
                return false;
            }

            long mass = checked((long)amount * 1_000L);
            string commitId =
                $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Transfer}:{operationId}:{amount}:{mass}";
            receipt = new ProductionWipInputReceipt(commitId, amount, mass);
            pendingWipOperationId = operationId;
            pendingWipReceipt = receipt;
            deliveredAmount -= amount;
            WipTransferredAmount = checked(WipTransferredAmount + amount);
            WipTransferredMassGrams = checked(
                WipTransferredMassGrams + mass);
            failureReason = string.Empty;
            return true;
        }

        public bool AcknowledgeWipInput(
            string commitId,
            out string failureReason)
        {
            if (FailNextWipAcknowledgement)
            {
                FailNextWipAcknowledgement = false;
                failureReason = "injected-wip-acknowledgement-failure";
                return false;
            }
            if (acknowledgedWipCommits.Contains(commitId ?? string.Empty))
            {
                failureReason = string.Empty;
                return true;
            }
            if (!pendingWipReceipt.IsCommitted
                || !string.Equals(
                    pendingWipReceipt.CommitId,
                    commitId,
                    StringComparison.Ordinal))
            {
                failureReason = "production-wip-receipt-not-found";
                return false;
            }

            acknowledgedWipCommits.Add(commitId);
            pendingWipOperationId = string.Empty;
            pendingWipReceipt = default;
            WipAcknowledgementCount++;
            failureReason = string.Empty;
            return true;
        }

        public bool SpawnOutput(
            string itemId,
            int amount,
            Vector2Int position)
        {
            return true;
        }

        public bool CanSpawnOutput(
            string itemId,
            int amount,
            Vector2Int position,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return amount > 0;
        }

        public void PrioritizeDestination(string destinationId)
        {
        }

        public int ReleaseDestination(
            string destinationId,
            Vector2Int releasePosition)
        {
            LastReleasedDestinationId = destinationId?.Trim() ?? string.Empty;
            LastReleasedPosition = releasePosition;
            if (!string.Equals(
                    requestedDestinationId,
                    LastReleasedDestinationId,
                    StringComparison.Ordinal)
                || releasePosition != LastDestinationPosition)
            {
                return 0;
            }
            int released = RequestedAmount;
            ReleasedAmount += released;
            deliveredAmount = 0;
            RequestedAmount = 0;
            return released;
        }

        public bool TryReleaseDestinationAtomically(
            string destinationId,
            Vector2Int releasePosition,
            out int released,
            out string failureReason)
        {
            released = ReleaseDestination(destinationId, releasePosition);
            failureReason = string.Empty;
            return true;
        }

        public int RemoveDestination(string destinationId)
        {
            int removed = deliveredAmount;
            deliveredAmount = 0;
            RequestedAmount = 0;
            return removed;
        }

        public void DeliverAll()
        {
            deliveredAmount = RequestedAmount;
        }

        private bool Matches(string itemId, string destinationId)
        {
            return string.Equals(
                    requestedItemId,
                    itemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    requestedDestinationId,
                    destinationId,
                    StringComparison.Ordinal);
        }
    }

    private sealed class EditorContentCatalog : IOffenseContentCatalog
    {
        public EditorContentCatalog(
            IReadOnlyList<OffenseSiteArchetypeSO> sites,
            IReadOnlyList<OffenseUrgentSiteDefinitionSO> urgent,
            IReadOnlyList<OffenseDecisionCardSO> cards,
            IReadOnlyList<OffenseEncounterSO> encounters)
        {
            SiteArchetypes = sites;
            UrgentSites = urgent;
            DecisionCards = cards;
            Encounters = encounters;
        }

        public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes { get; }
        public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites { get; }
        public IReadOnlyList<OffenseDecisionCardSO> DecisionCards { get; }
        public IReadOnlyList<OffenseEncounterSO> Encounters { get; }
    }

    private sealed class RecordingResolutionAdapter :
        IOffenseCommandResolutionAdapter
    {
        public readonly List<OffenseCommandExecutionRequest> Requests =
            new List<OffenseCommandExecutionRequest>();
        public int FinalizeCount { get; private set; }
        public int FinalizationFailuresRemaining { get; set; }
        public Action FinalizeAction { get; set; }
        public readonly HashSet<string> UnavailableActorIds =
            new HashSet<string>(StringComparer.Ordinal);

        public OffenseCommandExecutionResult Execute(
            OffenseCommandExecutionRequest request)
        {
            Requests.Add(request);
            if (UnavailableActorIds.Contains(request.actorId))
            {
                return new OffenseCommandExecutionResult(
                    OffenseCommandOutcome.Unavailable,
                    false,
                    request.targetCombatantId,
                    "focused-unavailable");
            }
            return new OffenseCommandExecutionResult(
                OffenseCommandOutcome.Executed,
                true,
                request.targetCombatantId);
        }

        public OffenseTurnFinalizationResult FinalizeTurn(int directorTurn)
        {
            FinalizeCount++;
            if (FinalizationFailuresRemaining > 0)
            {
                FinalizationFailuresRemaining--;
                return new OffenseTurnFinalizationResult(
                    false,
                    "focused-finalization-failure");
            }

            FinalizeAction?.Invoke();
            return new OffenseTurnFinalizationResult(true, string.Empty);
        }
    }

    private sealed class FixedMoneyRuntime : IGameMoneyAccount
    {
        public FixedMoneyRuntime(int balance)
        {
            Balance = Mathf.Max(0, balance);
        }

        public int Balance { get; private set; }

        public bool CanSpend(int amount)
        {
            return Balance >= Mathf.Max(0, amount);
        }

        public bool TrySpend(int amount, out string reason)
        {
            int cost = Mathf.Max(0, amount);
            if (Balance < cost)
            {
                reason = "골드 부족";
                return false;
            }

            Balance -= cost;
            reason = string.Empty;
            return true;
        }

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            return TrySpend(amount, out reason);
        }

        public void Add(int amount)
        {
            Balance += Mathf.Max(0, amount);
        }

        public void Add(
            int amount,
            EconomyTransactionContext context)
        {
            Add(amount);
        }

        public void SetBalance(
            int amount,
            EconomyTransactionContext context)
        {
            Balance = Mathf.Max(0, amount);
        }
    }
}
