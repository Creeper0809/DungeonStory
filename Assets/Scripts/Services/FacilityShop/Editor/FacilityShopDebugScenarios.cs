using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FacilityShopDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Facility Shop/Run P1 Facility Shop Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 facility shop scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        FacilityShopDomainDebugScenarios.Validate();

        List<string> errors = new List<string>();
        RunScenario("일일 상품 시설/설계도 포함", VerifyDailyOffersContainBuildingAndBlueprint, errors);
        RunScenario("희귀 상품 랜덤 등장", VerifyRareOffersAppearRandomly, errors);
        RunScenario("기본 구매 해금", VerifyBasicPurchaseUnlocksLowStarsOnly, errors);
        RunScenario("시설 구매 시 정적 자산 보존", VerifyBuildingPurchasePreservesStaticAsset, errors);
        RunScenario("설계도 구매", VerifyBlueprintPurchaseUsesMoneyAndRecordsBlueprint, errors);
        RunScenario("표시 이름과 시설 등급 분리", VerifyBuildingStarUsesQualityAbility, errors);
        RunScenario("새 상품 타입 다형 구매", VerifyCustomOfferPurchasesWithoutServiceBranch, errors);
        RunScenario("운영일 후 상점 갱신", VerifyRuntimeRefreshesAfterOperatingDay, errors);
        RunScenario("정산 보고서 시설 상점 항목", VerifySettlementReportIncludesFacilityShop, errors);
        RunScenario(
            "Discarded restore candidate preserves live facility shop",
            VerifyDiscardedRestoreLeavesLiveFacilityShopUntouched,
            errors);

        RunScenario("Day 1 strategy blueprint candidate", VerifyStartingBlueprintCandidateIsGuaranteed, errors);
        RunScenario("Run start refreshes Day 1 strategy offer", VerifyRunStartRefreshesStrategyOffer, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 facility shop scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyDailyOffersContainBuildingAndBlueprint()
    {
        IReadOnlyList<FacilityShopOffer> offers = CreateDailyOffersForScenario(1);

        return offers.Count >= 4
            && offers.OfType<FacilityBuildingOffer>().Any((offer) => offer.Building != null)
            && offers.OfType<FacilityBlueprintOffer>().Any((offer) => offer.Blueprint != null)
            && offers.All((offer) => offer.IsValid && offer.Cost > 0)
            && offers.All((offer) => offer is not FacilityBuildingOffer || offer.Star <= 2);
    }

    private static bool VerifyStartingBlueprintCandidateIsGuaranteed()
    {
        int[] strategyBlueprintIds =
        {
            RunStrategyBlueprintIds.CommerceBasics,
            RunStrategyBlueprintIds.FortressBasics,
            RunStrategyBlueprintIds.ArcaneBasics
        };

        foreach (int strategyBlueprintId in strategyBlueprintIds)
        {
            IReadOnlyList<FacilityShopOffer> offers = FacilityShopService.CreateDailyOffers(
                1,
                LoadAllBuildings(),
                LoadAllBlueprints(),
                0,
                DefaultBuildingCostMultiplier,
                DefaultBlueprintCostMultiplier,
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                new[] { strategyBlueprintId });
            if (!offers.OfType<FacilityBlueprintOffer>()
                .Any((offer) => offer.Blueprint != null && offer.Blueprint.id == strategyBlueprintId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool VerifyRareOffersAppearRandomly()
    {
        bool sawRare = false;
        bool sawNoRare = false;
        for (int day = 1; day <= 40; day++)
        {
            IReadOnlyList<FacilityShopOffer> offers = CreateDailyOffersForScenario(day);
            bool hasRare = offers.Any((offer) => offer.Rarity != FacilityShopRarity.Common);
            sawRare |= hasRare;
            sawNoRare |= !hasRare;
        }

        return sawRare && sawNoRare;
    }

    private static bool VerifyBasicPurchaseUnlocksLowStarsOnly()
    {
        BuildingSO oneStar = LoadBuilding("P1_SpikeTrap");
        BuildingSO twoStar = CreateSyntheticDefenseBuilding(9202, "2성 테스트 방어 시설", 2);
        BuildingSO threeStar = CreateSyntheticDefenseBuilding(9203, "3성 테스트 방어 시설", 3);
        FacilityShopUnlockState state = new FacilityShopUnlockState();

        bool oneUnlocked = state.UnlockBasicPurchase(oneStar);
        bool twoUnlocked = state.UnlockBasicPurchase(twoStar);
        bool threeRejected = !state.UnlockBasicPurchase(threeStar);
        state.UnlockBasicPurchaseById(threeStar.id);

        IReadOnlyList<FacilityShopOffer> offers = FacilityShopService.CreateBasicPurchaseOffers(
            new[] { oneStar, twoStar, threeStar },
            state,
            Array.Empty<int>(),
            DefaultBuildingCostMultiplier,
            CharacterAiEditorTestDependencies.AuthoredGameplay);

        bool valid = oneUnlocked
            && twoUnlocked
            && threeRejected
            && offers.OfType<FacilityBuildingOffer>().Any((offer) => offer.Building == oneStar && offer.IsBasicPurchase)
            && offers.OfType<FacilityBuildingOffer>().Any((offer) => offer.Building.id == 9202 && offer.IsBasicPurchase)
            && offers.All((offer) => offer.Star <= 2)
            && offers.OfType<FacilityBuildingOffer>().All((offer) => offer.Building.id != 9203);

        Object.DestroyImmediate(twoStar);
        Object.DestroyImmediate(threeStar);
        return valid;
    }

    private static bool VerifyBuildingPurchasePreservesStaticAsset()
    {
        BuildingSO source = LoadBuilding("P1_GuardRoom");
        BuildingSO building = Object.Instantiate(source);
        building.id = 9301;
        building.unlocked = false;
        GameSessionState gameData = CreateGameData(500);
        FacilityShopUnlockState state = new FacilityShopUnlockState();
        FacilityShopOffer offer = new FacilityBuildingOffer(
            building,
            120,
            FacilityShopRarity.Common,
            false,
            true);

        bool success = FacilityShopService.TryPurchaseOffer(
            new EditorGameMoneyAccount(gameData),
            offer,
            state,
            PurchaseContext("building"),
            DisabledDungeonDebugRuleQuery.Instance,
            out FacilityShopPurchaseResult result);

        bool valid = success
            && result.success
            && result.TryGetBuilding(out BuildingSO purchasedBuilding)
            && purchasedBuilding == building
            && !building.unlocked
            && gameData.holdingMoney.Value == 380
            && result.message.Contains("구매 완료");

        Object.DestroyImmediate(building);
        return valid;
    }

    private static bool VerifyBlueprintPurchaseUsesMoneyAndRecordsBlueprint()
    {
        FacilityBlueprintSO blueprint = LoadBlueprint("BP_CommercialBasics");
        GameSessionState gameData = CreateGameData(500);
        FacilityShopUnlockState state = new FacilityShopUnlockState();
        FacilityShopOffer offer = new FacilityBlueprintOffer(
            blueprint,
            100,
            blueprint.rarity,
            true);

        bool success = FacilityShopService.TryPurchaseOffer(
            new EditorGameMoneyAccount(gameData),
            offer,
            state,
            PurchaseContext("blueprint"),
            DisabledDungeonDebugRuleQuery.Instance,
            out FacilityShopPurchaseResult result);
        bool valid = success
            && result.success
            && result.TryGetBlueprint(out FacilityBlueprintSO purchasedBlueprint)
            && purchasedBlueprint == blueprint
            && gameData.holdingMoney.Value == 400
            && state.IsBlueprintAcquired(blueprint);

        return valid;
    }

    private static bool VerifyBuildingStarUsesQualityAbility()
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.objectName = "5성처럼 보이는 일반 시설";
        building.ReplaceAbilities(new BuildingAbilityCollection());

        int defaultStar = FacilityShopService.GetBuildingStar(building);
        building.AbilityModules.Add(new BuildingQualityAbility { star = 3 });
        int configuredStar = FacilityShopService.GetBuildingStar(building);

        Object.DestroyImmediate(building);
        return defaultStar == 1 && configuredStar == 3;
    }

    private static bool VerifyCustomOfferPurchasesWithoutServiceBranch()
    {
        GameSessionState gameData = CreateGameData(100);
        DebugFacilityShopOffer offer = new DebugFacilityShopOffer(35);

        bool success = FacilityShopService.TryPurchaseOffer(
            new EditorGameMoneyAccount(gameData),
            offer,
            new FacilityShopUnlockState(),
            PurchaseContext("custom"),
            DisabledDungeonDebugRuleQuery.Instance,
            out FacilityShopPurchaseResult result);

        bool valid = success
            && result.success
            && offer.ApplyCount == 1
            && result.offerTypeId == DebugFacilityShopOffer.TypeId
            && gameData.holdingMoney.Value == 65;

        return valid;
    }

    private static bool VerifyRunStartRefreshesStrategyOffer()
    {
        GameObject runtimeObject = new GameObject("DailyFacilityShopRuntime_StrategyStart_Test");
        DailyFacilityShopRuntime runtime = runtimeObject.AddComponent<DailyFacilityShopRuntime>();
        runtime.ConstructDailyFacilityShopRuntime(
            new EditorFacilityShopCatalog(),
            new FixedBlueprintCandidateRunVariableReader(RunStrategyBlueprintIds.CommerceBasics),
            new NeutralMetaProgressionReader(),
            new DungeonStory.Foundation.GameEventBus(),
            new EditorGameMoneyAccount(new GameSessionState()), autoProcurement: null,
            buildingCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
            aggregateRootStore: new DungeonRuntimeAggregateRootStore(),
            debugRules: DisabledDungeonDebugRuleQuery.Instance);

        runtime.OnTriggerEvent(new RunStartVariablesSelectedEvent(null));
        bool valid = runtime.CurrentOfferDay == 1
            && runtime.CurrentDailyOffers.OfType<FacilityBlueprintOffer>()
                .Any((offer) => offer.Blueprint != null
                    && offer.Blueprint.id == RunStrategyBlueprintIds.CommerceBasics);

        Object.DestroyImmediate(runtimeObject);
        return valid;
    }

    private static bool VerifyRuntimeRefreshesAfterOperatingDay()
    {
        GameObject runtimeObject = new GameObject("DailyFacilityShopRuntime_Test");
        DailyFacilityShopRuntime runtime = runtimeObject.AddComponent<DailyFacilityShopRuntime>();
        runtime.ConstructDailyFacilityShopRuntime(
            new EditorFacilityShopCatalog(),
            new NeutralRunVariableReader(),
            new NeutralMetaProgressionReader(),
            new DungeonStory.Foundation.GameEventBus(),
            new EditorGameMoneyAccount(new GameSessionState()), autoProcurement: null,
            buildingCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
            aggregateRootStore: new DungeonRuntimeAggregateRootStore(),
            debugRules: DisabledDungeonDebugRuleQuery.Instance);
        int refreshCount = 0;
        int lastRefreshDay = 0;
        void RecordRefresh(
            int day,
            IReadOnlyList<FacilityShopOffer> offers,
            IReadOnlyList<FacilityShopOffer> basicPurchaseOffers)
        {
            refreshCount++;
            lastRefreshDay = day;
        }

        runtime.Refreshed += RecordRefresh;

        runtime.OnTriggerEvent(new OperatingDayEndedEvent(2));
        bool valid = runtime.CurrentOfferDay == 3
            && runtime.CurrentDailyOffers.Count > 0
            && refreshCount == 1
            && lastRefreshDay == 3;

        runtime.Refreshed -= RecordRefresh;
        Object.DestroyImmediate(runtimeObject);
        return valid;
    }

    private static bool VerifySettlementReportIncludesFacilityShop()
    {
        GameObject settlementObject = new GameObject("Settlement_FacilityShop_Test");
        OperatingDaySettlementRuntime settlement = settlementObject.AddComponent<OperatingDaySettlementRuntime>();
        GameSessionState gameData = CreateGameData(0);
        EmptyWorldQuery worldQuery = new EmptyWorldQuery();
        DungeonStory.Foundation.GameEventBus gameEvents =
            new DungeonStory.Foundation.GameEventBus();
        FixedGameDataProvider gameDataProvider =
            new FixedGameDataProvider(gameData);
        EditorGameMoneyAccount moneyAccount =
            new EditorGameMoneyAccount(gameData);
        DungeonRuntimeAggregateRootStore aggregateRootStore =
            new DungeonRuntimeAggregateRootStore();
        TreasuryEconomyAggregateStateStore treasuryState =
            new TreasuryEconomyAggregateStateStore(aggregateRootStore);
        EmploymentContractRuntime employmentContracts =
            new EmploymentContractRuntime(
                worldQuery,
                worldQuery,
                OffenseEditorTestDependencies.CreateCombatEquipmentRuntime(),
                moneyAccount,
                gameEvents,
                treasuryState);
        PaidFacilityContractRuntime paidFacilityContracts =
            new PaidFacilityContractRuntime(
                gameDataProvider,
                moneyAccount,
                treasuryState);
        settlement.Construct(
            worldQuery,
            worldQuery,
            new EditorFacilityShopCatalog(),
            new NeutralRunVariableReader(),
            gameDataProvider,
            gameEvents,
            employmentContracts,
            moneyAccount,
            paidFacilityContracts,
            stockCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
            buildingCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
            aggregateRootStore);

        settlement.OnTriggerEvent(new OperatingDayEndedEvent(1));
        OperatingDayReport report = settlement.LatestReport;
        bool valid = report != null
            && report.refreshedFacilityShopOffers.Count > 0
            && report.refreshedFacilityShopOffers.Any((offer) => offer.offerTypeId == FacilityShopOfferTypeIds.Building)
            && report.refreshedFacilityShopOffers.Any((offer) => offer.offerTypeId == FacilityShopOfferTypeIds.Blueprint)
            && report.ToDetailText().Contains("시설 상점 갱신");

        Object.DestroyImmediate(settlementObject);
        return valid;
    }

    private static bool VerifyDiscardedRestoreLeavesLiveFacilityShopUntouched()
    {
        BuildingSO building = LoadBuilding("P1_SpikeTrap");
        FacilityBlueprintSO blueprint = LoadBlueprint("BP_CommercialBasics");
        if (building == null || blueprint == null)
        {
            return FailFacilityShopDiscard(
                "asset-load",
                $"building={building != null}, blueprint={blueprint != null}");
        }

        EditorFacilityShopCatalog catalog = new EditorFacilityShopCatalog();
        DungeonRuntimeAggregateRootStore sourceRoot =
            new DungeonRuntimeAggregateRootStore();
        GameObject sourceObject = new GameObject("FacilityShop_Discard_Source");
        GameObject targetObject = new GameObject("FacilityShop_Discard_Target");
        try
        {
            DailyFacilityShopRuntime source = CreateRuntime(
                sourceObject,
                catalog,
                sourceRoot);
            PublishState(
                source,
                9,
                new[] { building.id },
                new[] { blueprint.id });
            string candidatePayload = new FacilityShopSaveSection(
                source,
                catalog).Capture();

            DungeonRuntimeAggregateRootStore targetRoot =
                new DungeonRuntimeAggregateRootStore();
            DailyFacilityShopRuntime target = CreateRuntime(
                targetObject,
                catalog,
                targetRoot);
            PublishState(
                target,
                3,
                Array.Empty<int>(),
                Array.Empty<int>());
            FacilityShopSaveSection targetSection = new FacilityShopSaveSection(
                target,
                catalog);
            object sectionContract = targetSection;
            bool hasPreflight = sectionContract is IDungeonSaveSectionPreflight;
            bool isRollbackFree =
                sectionContract is IDungeonRollbackFreeSaveSection;
            bool isOptional = sectionContract is IOptionalDungeonSaveSection;
            bool isStagedOptional =
                sectionContract is IDungeonStagedOptionalSaveSection;
            if (!hasPreflight
                || !isRollbackFree
                || isOptional
                || isStagedOptional)
            {
                return FailFacilityShopDiscard(
                    "section-contract",
                    $"preflight={hasPreflight}, rollbackFree={isRollbackFree}, "
                    + $"optional={isOptional}, stagedOptional={isStagedOptional}");
            }

            string beforeInvalid = targetSection.Capture();
            DungeonFacilityShopSaveData legacy =
                JsonUtility.FromJson<DungeonFacilityShopSaveData>(
                    candidatePayload);
            legacy.version = DungeonFacilityShopSaveData.CurrentVersion - 1;
            if (!RejectsFacilityShopPayloadWithoutMutation(
                    targetSection,
                    legacy,
                    beforeInvalid,
                    out string legacyDetail))
            {
                return FailFacilityShopDiscard(
                    "legacy-version-rejection",
                    legacyDetail);
            }

            (string Stage, string PayloadJson)[] invalidUnlockLists =
            {
                (
                    "null-basic-purchase-buildings",
                    "{\"version\":"
                    + DungeonFacilityShopSaveData.CurrentVersion
                    + ",\"currentOfferDay\":9,"
                    + "\"basicPurchaseBuildingIds\":null,"
                    + $"\"acquiredBlueprintIds\":[{blueprint.id}]}}"),
                (
                    "missing-basic-purchase-buildings",
                    "{\"version\":"
                    + DungeonFacilityShopSaveData.CurrentVersion
                    + ",\"currentOfferDay\":9,"
                    + $"\"acquiredBlueprintIds\":[{blueprint.id}]}}"),
                (
                    "null-acquired-blueprints",
                    "{\"version\":"
                    + DungeonFacilityShopSaveData.CurrentVersion
                    + ",\"currentOfferDay\":9,"
                    + $"\"basicPurchaseBuildingIds\":[{building.id}],"
                    + "\"acquiredBlueprintIds\":null}"),
                (
                    "missing-acquired-blueprints",
                    "{\"version\":"
                    + DungeonFacilityShopSaveData.CurrentVersion
                    + ",\"currentOfferDay\":9,"
                    + $"\"basicPurchaseBuildingIds\":[{building.id}]}}")
            };
            foreach ((string stage, string payloadJson) in invalidUnlockLists)
            {
                if (!RejectsFacilityShopPayloadWithoutMutation(
                        targetSection,
                        payloadJson,
                        beforeInvalid,
                        out string missingUnlocksDetail))
                {
                    return FailFacilityShopDiscard(
                        stage,
                        missingUnlocksDetail);
                }
            }

            DungeonFacilityShopSaveData invalid =
                JsonUtility.FromJson<DungeonFacilityShopSaveData>(
                    candidatePayload);
            invalid.basicPurchaseBuildingIds.Add(-1);
            DungeonGameRestoreReport invalidReport =
                new DungeonGameRestoreReport();
            bool invalidRejected = false;
            try
            {
                targetSection.Restore(
                    JsonUtility.ToJson(invalid),
                    targetSection.SectionVersion,
                    invalidReport);
            }
            catch (InvalidOperationException)
            {
                invalidRejected = true;
            }
            if (!invalidRejected
                || !string.Equals(
                    targetSection.Capture(),
                    beforeInvalid,
                    StringComparison.Ordinal))
            {
                return FailFacilityShopDiscard(
                    "invalid-id-rejection",
                    $"rejected={invalidRejected}, liveUnchanged="
                    + string.Equals(
                        targetSection.Capture(),
                        beforeInvalid,
                        StringComparison.Ordinal));
            }

            FacilityShopFailureSection lateFailure =
                new FacilityShopFailureSection
                {
                    RemainingCommitFailures = 1
                };
            int revisionBefore = targetRoot.PublishedRestoreRevision;
            FacilityShopDiscardObserver observer =
                new FacilityShopDiscardObserver(target, building.id, blueprint.id);
            DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
                new IDungeonSaveSection[] { targetSection, lateFailure },
                targetRoot,
                new IDungeonRestoreTransactionParticipant[] { observer });
            List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
            envelopes.First(envelope => string.Equals(
                    envelope.sectionId,
                    FacilityShopSaveSection.Id,
                    StringComparison.Ordinal))
                .payloadJson = candidatePayload;

            DungeonGameRestoreReport restoreReport =
                new DungeonGameRestoreReport();
            bool restored = registry.RestoreAll(envelopes, restoreReport);
            string afterDiscard = targetSection.Capture();
            (string Name, bool Passed)[] checks =
            {
                ("restore-rejected", !restored),
                ("report-failed", !restoreReport.Success),
                ("injected-failure-consumed",
                    lateFailure.RemainingCommitFailures == 0),
                ("injected-failure-reported", restoreReport.Errors.Any(error =>
                    error.Contains(
                        "Injected late facility-shop restore failure.",
                        StringComparison.Ordinal))),
                ("discard-once", observer.DiscardCount == 1),
                ("observer-live-day", observer.ObservedOfferDay == 3),
                ("observer-no-building", !observer.ObservedCandidateBuilding),
                ("observer-no-blueprint", !observer.ObservedCandidateBlueprint),
                ("live-day", target.CurrentOfferDay == 3),
                ("live-no-building",
                    !target.UnlockState.BasicPurchaseBuildingIds.Contains(
                        building.id)),
                ("live-no-blueprint",
                    !target.UnlockState.AcquiredBlueprintIds.Contains(
                        blueprint.id)),
                ("live-json-unchanged", string.Equals(
                    afterDiscard,
                    beforeInvalid,
                    StringComparison.Ordinal)),
                ("staging-cleared", !targetRoot.IsRestoreStaging),
                ("revision-unchanged",
                    targetRoot.PublishedRestoreRevision == revisionBefore)
            };
            bool valid = checks.All(check => check.Passed);
            if (!valid)
            {
                Debug.LogError(
                    "Facility-shop discard detail: failedChecks="
                    + string.Join(
                        ",",
                        checks
                            .Where(check => !check.Passed)
                            .Select(check => check.Name))
                    + $", restored={restored}, "
                    + $"reportSuccess={restoreReport.Success}, "
                    + $"reportErrors={JoinErrors(restoreReport)}, "
                    + $"remainingFailures={lateFailure.RemainingCommitFailures}, "
                    + $"discard={observer.DiscardCount}, "
                    + $"observedDay={observer.ObservedOfferDay}, "
                    + $"observedBuilding={observer.ObservedCandidateBuilding}, "
                    + $"observedBlueprint={observer.ObservedCandidateBlueprint}, "
                    + $"liveDay={target.CurrentOfferDay}, "
                    + $"liveBuilding={target.UnlockState.BasicPurchaseBuildingIds.Contains(building.id)}, "
                    + $"liveBlueprint={target.UnlockState.AcquiredBlueprintIds.Contains(blueprint.id)}, "
                    + $"liveJsonUnchanged={string.Equals(afterDiscard, beforeInvalid, StringComparison.Ordinal)}, "
                    + $"staging={targetRoot.IsRestoreStaging}, "
                    + $"revisionBefore={revisionBefore}, "
                    + $"revisionAfter={targetRoot.PublishedRestoreRevision}");
            }
            return valid;
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    private static void PublishState(
        DailyFacilityShopRuntime runtime,
        int day,
        IEnumerable<int> buildingIds,
        IEnumerable<int> blueprintIds)
    {
        IFacilityShopPersistence persistence = runtime;
        persistence.PublishRestoreCandidate(
            persistence.BuildRestoreCandidate(new FacilityShopStateSnapshot(
                day,
                buildingIds,
                blueprintIds)));
    }

    private static bool RejectsFacilityShopPayloadWithoutMutation(
        FacilityShopSaveSection section,
        DungeonFacilityShopSaveData payload,
        string expectedLiveJson,
        out string detail) =>
        RejectsFacilityShopPayloadWithoutMutation(
            section,
            JsonUtility.ToJson(payload),
            expectedLiveJson,
            out detail);

    private static bool RejectsFacilityShopPayloadWithoutMutation(
        FacilityShopSaveSection section,
        string payloadJson,
        string expectedLiveJson,
        out string detail)
    {
        try
        {
            section.Restore(
                payloadJson,
                section.SectionVersion,
                new DungeonGameRestoreReport());
            detail = "payload was accepted; liveUnchanged="
                + string.Equals(
                    section.Capture(),
                    expectedLiveJson,
                    StringComparison.Ordinal);
            return false;
        }
        catch (InvalidOperationException exception)
        {
            bool liveUnchanged = string.Equals(
                section.Capture(),
                expectedLiveJson,
                StringComparison.Ordinal);
            detail = $"exception={exception.Message}, liveUnchanged={liveUnchanged}";
            return liveUnchanged;
        }
    }

    private static bool FailFacilityShopDiscard(string stage, string detail)
    {
        Debug.LogError(
            $"Facility-shop discard precondition failed: stage={stage}, "
            + $"detail={detail}");
        return false;
    }

    private static string JoinErrors(DungeonGameRestoreReport report) =>
        report == null || report.Errors.Count == 0
            ? "none"
            : string.Join(" | ", report.Errors);

    private static DailyFacilityShopRuntime CreateRuntime(
        GameObject host,
        IFacilityShopCatalog catalog,
        DungeonRuntimeAggregateRootStore rootStore)
    {
        DailyFacilityShopRuntime runtime =
            host.AddComponent<DailyFacilityShopRuntime>();
        runtime.ConstructDailyFacilityShopRuntime(
            catalog,
            new NeutralRunVariableReader(),
            new NeutralMetaProgressionReader(),
            new DungeonStory.Foundation.GameEventBus(),
            new EditorGameMoneyAccount(new GameSessionState()),
            autoProcurement: null,
            buildingCategoryCatalog: CharacterAiEditorTestDependencies.AuthoredGameplay,
            aggregateRootStore: rootStore,
            debugRules: DisabledDungeonDebugRuleQuery.Instance);
        return runtime;
    }

    private static BuildingSO LoadBuilding(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<BuildingSO>($"Assets/Resources/SO/Building/P1/{assetName}.asset");
    }

    private static FacilityBlueprintSO LoadBlueprint(string assetName)
    {
        return AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>($"Assets/Resources/SO/Blueprint/P1/{assetName}.asset");
    }

    private static IReadOnlyList<FacilityShopOffer> CreateDailyOffersForScenario(int day)
    {
        return FacilityShopService.CreateDailyOffers(
            day,
            LoadAllBuildings(),
            LoadAllBlueprints(),
            0,
            DefaultBuildingCostMultiplier,
            DefaultBlueprintCostMultiplier,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
    }

    private static IReadOnlyList<BuildingSO> LoadAllBuildings()
    {
        return AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where((building) => building != null)
            .ToArray();
    }

    private static IReadOnlyList<FacilityBlueprintSO> LoadAllBlueprints()
    {
        return AssetDatabase.FindAssets("t:FacilityBlueprintSO", new[] { "Assets/Resources/SO/Blueprint" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<FacilityBlueprintSO>)
            .Where((blueprint) => blueprint != null)
            .ToArray();
    }

    private static float DefaultBuildingCostMultiplier(BuildingSO building)
    {
        return 1f;
    }

    private static float DefaultBlueprintCostMultiplier(FacilityBlueprintSO blueprint)
    {
        return 1f;
    }

    private static BuildingSO CreateSyntheticDefenseBuilding(int id, string objectName, int star)
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = id;
        building.objectName = objectName;
        building.width = 1;
        building.height = 1;
        building.layer = GridLayer.Building;
        building.category = BuildingCategory.Special;
        building.runtimeArchetype = BuildingRuntimeArchetypeKind.DefenseFacility;
        building.Defense = new DefenseFacilityData
        {
            enabled = true,
            concept = DefenseAttackConcept.Physical,
            triggerTimings = DefenseTriggerTiming.OnEnter,
            targetRule = DefenseTargetRule.EnteringIntruder,
            star = star
        };
        return building;
    }

    private static EconomyTransactionContext PurchaseContext(string targetId)
    {
        return new EconomyTransactionContext(
            EconomyTransactionKind.ShopPurchase,
            "facility-shop-debug",
            targetId);
    }

    private static GameSessionState CreateGameData(int holdingMoney)
    {
        GameSessionState gameData = new GameSessionState();
        gameData.holdingMoney.Initialize(holdingMoney);
        return gameData;
    }

    private sealed class DebugFacilityShopOffer : FacilityShopOffer
    {
        public const string TypeId = "facility-shop.offer.debug";

        public DebugFacilityShopOffer(int cost)
            : base(cost, FacilityShopRarity.Special, false, true)
        {
        }

        public int ApplyCount { get; private set; }
        public override string OfferTypeId => TypeId;
        public override string TypeDisplayName => "테스트";
        public override bool IsValid => true;
        public override int Star => 0;
        public override int DataId => 1;
        public override string DisplayName => "확장 상품";

        protected override string ApplyPurchase(FacilityShopUnlockState unlockState)
        {
            ApplyCount++;
            return "확장 상품 구매 완료";
        }
    }

    private sealed class FacilityShopFailureSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public string SectionId => "facility-shop.debug.late-failure";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Presentation;
        public IReadOnlyList<string> DependsOn =>
            new[] { FacilityShopSaveSection.Id };
        public int RemainingCommitFailures { get; set; }

        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new InvalidOperationException(
                    "Facility-shop scenario version mismatch.");
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                if (RemainingCommitFailures <= 0)
                {
                    return;
                }

                RemainingCommitFailures--;
                throw new InvalidOperationException(
                    "Injected late facility-shop restore failure.");
            });
        }
    }

    private sealed class FacilityShopDiscardObserver :
        IDungeonRestoreTransactionParticipant
    {
        private readonly DailyFacilityShopRuntime runtime;
        private readonly int candidateBuildingId;
        private readonly int candidateBlueprintId;
        private bool hasCandidate;

        public FacilityShopDiscardObserver(
            DailyFacilityShopRuntime runtime,
            int candidateBuildingId,
            int candidateBlueprintId)
        {
            this.runtime = runtime;
            this.candidateBuildingId = candidateBuildingId;
            this.candidateBlueprintId = candidateBlueprintId;
        }

        public string ParticipantId => "facility-shop.debug.discard-observer";
        public int DiscardCount { get; private set; }
        public int ObservedOfferDay { get; private set; }
        public bool ObservedCandidateBuilding { get; private set; }
        public bool ObservedCandidateBlueprint { get; private set; }

        public void BeginRestoreCandidate()
        {
            hasCandidate = true;
        }

        public void PublishRestoreCandidate()
        {
            hasCandidate = false;
        }

        public void DiscardRestoreCandidate()
        {
            if (!hasCandidate)
            {
                return;
            }

            hasCandidate = false;
            DiscardCount++;
            ObservedOfferDay = runtime.CurrentOfferDay;
            ObservedCandidateBuilding =
                runtime.UnlockState.BasicPurchaseBuildingIds.Contains(
                    candidateBuildingId);
            ObservedCandidateBlueprint =
                runtime.UnlockState.AcquiredBlueprintIds.Contains(
                    candidateBlueprintId);
        }
    }

    private sealed class EditorFacilityShopCatalog :
        IFacilityShopCatalog,
        IFacilityShopDefinitionCatalog
    {
        public IReadOnlyCollection<BuildingSO> Buildings => LoadAllBuildings();
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => LoadAllBlueprints();
        IReadOnlyCollection<FacilityShopCatalogDefinition>
            IFacilityShopDefinitionCatalog.Buildings => Buildings
                .Select(building => new FacilityShopCatalogDefinition(
                    building.id,
                    FacilityShopService.GetBuildingName(building),
                    FacilityShopService.GetBuildingStar(building)))
                .ToArray();
        IReadOnlyCollection<int> IFacilityShopDefinitionCatalog.BlueprintIds =>
            Blueprints.Select(blueprint => blueprint.id).ToArray();

        public BuildingSO FindBuildingById(int buildingId)
        {
            return Buildings.FirstOrDefault(building => building != null && building.id == buildingId);
        }
    }

    private sealed class NeutralRunVariableReader : IRunVariableRuntimeReader
    {
        public int GetInitialShopSeed() => 0;
        public IReadOnlyList<int> GetStartingBlueprintCandidateIds() => System.Array.Empty<int>();
        public float GetGuestDemandMultiplier(string speciesTag) => 1f;
        public float GetStockCostMultiplier(StockCategory category) => 1f;
        public float GetFacilityShopCostMultiplier(BuildingSO building) => 1f;
        public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint) => 1f;
        public float GetThreatRiseMultiplier() => 1f;
        public float GetWarningThresholdMultiplier() => 1f;
        public DungeonSurvivalPressure GetSurvivalPressure() =>
            DungeonSurvivalPressure.Standard;
        public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source) => source;
    }

    private sealed class NeutralMetaProgressionReader : IMetaProgressionRuntimeReader
    {
        public int GetStartingFacilityCandidateBonus() => 0;
        public int GetStartingOwnerTraitCandidateBonus() => 0;
        public float GetOwnerMaxHealthMultiplier() => 1f;
        public float GetInvasionWarningThresholdMultiplier() => 1f;
        public float GetCommerceStockCostMultiplier(StockCategory category) => 1f;
        public float GetFortressFacilityCostMultiplier(BuildingSO building) => 1f;
        public float GetArcaneResearchWorkMultiplier() => 1f;
        public bool IsRecipePreserved(string recipeId) => false;

        public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(IEnumerable<BuildingSO> buildings)
        {
            return Array.Empty<int>();
        }
    }

    private sealed class FixedBlueprintCandidateRunVariableReader : IRunVariableRuntimeReader
    {
        private readonly int blueprintId;

        public FixedBlueprintCandidateRunVariableReader(int blueprintId)
        {
            this.blueprintId = blueprintId;
        }

        public int GetInitialShopSeed() => 0;
        public IReadOnlyList<int> GetStartingBlueprintCandidateIds() => new[] { blueprintId };
        public float GetGuestDemandMultiplier(string speciesTag) => 1f;
        public float GetStockCostMultiplier(StockCategory category) => 1f;
        public float GetFacilityShopCostMultiplier(BuildingSO building) => 1f;
        public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint) => 1f;
        public float GetThreatRiseMultiplier() => 1f;
        public float GetWarningThresholdMultiplier() => 1f;
        public DungeonSurvivalPressure GetSurvivalPressure() =>
            DungeonSurvivalPressure.Standard;
        public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source) => source;
    }

    private sealed class EmptyWorldQuery :
        ICharacterWorldQuery,
        ICharacterWorldPersistenceIdentityQuery,
        IBuildingWorldQuery
    {
        public int CharacterVersion => 0;
        public IReadOnlyList<CharacterActor> Characters => Array.Empty<CharacterActor>();
        public IReadOnlyCollection<CharacterId> GetPersistentCharacterIds() =>
            Array.Empty<CharacterId>();
        public IReadOnlyCollection<CharacterId> GetPersistentActorIds() =>
            Array.Empty<CharacterId>();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings => Array.Empty<BuildableObject>();
    }

    private sealed class FixedGameDataProvider : IGameSessionStateProvider
    {
        private readonly GameSessionState gameData;

        public FixedGameDataProvider(GameSessionState gameData)
        {
            this.gameData = gameData;
        }

        public bool TryGetSessionState(out GameSessionState resolvedGameData)
        {
            resolvedGameData = gameData;
            return resolvedGameData != null;
        }
    }

}
