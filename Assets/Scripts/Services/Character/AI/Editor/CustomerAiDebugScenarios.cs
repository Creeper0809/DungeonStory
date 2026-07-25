using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CustomerAiDebugScenarios
{
    [MenuItem("DungeonStory/Debug/AI/Run P1 Customer AI Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 customer AI scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("Action score compensation", VerifyActionScoreCompensation, errors);
        RunScenario("Toilet and hygiene facility recovers needs", VerifyToiletAndHygieneFacilityRecovery, errors);
        RunScenario("목적지 없는 행동은 no action 처리", VerifyUnavailableDestinationActionsReportNoAction, errors);
        RunScenario("갈 수 없는 고점 행동 대신 대체 행동 선택", VerifyUnavailableHighScoreNeedSelectsReachableAction, errors);

        RunScenario("역할별 후보 필터링", VerifyRoleCandidateFiltering, errors);
        RunScenario("욕구 점수가 행동 우선순위를 결정", VerifyNeedScoresDriveActionPriority, errors);
        RunScenario("종족 선호가 거리보다 우선 가능", VerifySpeciesPreferenceCanBeatDistance, errors);
        RunScenario("뱀파이어는 마나/연구 선호", VerifyVampireSelectsManaOrResearch, errors);
        RunScenario("이용 불가 시설 제외", VerifyUnavailableFacilitiesAreExcluded, errors);
        RunScenario("Unstaffed shop allows self-service checkout", VerifyUnstaffedShopAllowsSelfServiceCheckout, errors);
        RunScenario("Checkout patience stages scale by personality and queue", VerifyCheckoutPatienceStages, errors);
        RunScenario("Abandoned checkout preserves another shopping attempt", VerifyAbandonedCheckoutPreservesVisit, errors);
        RunScenario("Abandoned checkout creates personal facility memory", VerifyCheckoutComplaintCreatesFacilityMemory, errors);
        RunScenario("Staffed checkout coroutine reaches abandonment and releases queue", VerifyCheckoutWaitCoroutineAbandons, errors);
        RunScenario("Completed visit exits without optional look around", VerifyCompletedVisitExitsImmediately, errors);
        RunScenario("돈 부족 상점만 있으면 방문 종료", VerifyUnaffordableShopEndsVisitCycle, errors);
        RunScenario("방문자 필수 AI 행동 자동 보강", VerifyVisitorActionsAreCompletedFromPartialPrefab, errors);
        RunScenario("신규성/혼잡도 점수 반영", VerifyNoveltyAndCrowdScores, errors);

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
            Debug.Log("P1 customer AI scenarios passed.");
        }

        return true;
    }

    [MenuItem("DungeonStory/Debug/AI/Dump P1 Customer AI Diagnostics")]
    public static void DumpDiagnosticsFromMenu()
    {
        Debug.Log(DumpDiagnostics());
    }

    public static string DumpDiagnostics()
    {
        const string reportPath = "Temp/customer-ai-diagnostic-report.tsv";
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("section\tkey\tvalue");

        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject lowFood = world.Place("P1_LowFoodShop", new Vector2Int(4, 0));
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(10, 0));
        BuildableObject rest = world.Place("P1_RestRoom", new Vector2Int(16, 0));
        BuildableObject toilet = world.Place("P1_Toilet", new Vector2Int(22, 0));
        BuildableObject washroom = world.Place("P1_Washroom", new Vector2Int(28, 0));
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(34, 0));
        world.StaffShop(lowFood);
        world.StaffShop(general);

        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 10f, 20f, 10f, 20f);
        CharacterActor actor = CharacterActor.From(customer);
        GridPathSearchResult searchResult = world.Grid.SearchPath(Vector2Int.zero);
        RoomLayoutCache roomCache = new RoomLayoutCache();
        RoomLayout layout = roomCache.GetLayout(world.Grid);
        RoomFacilityPolicyService policy = new RoomFacilityPolicyService(roomCache);

        report.AppendLine($"grid\tversion\t{world.Grid.version}");
        report.AppendLine($"grid\treachableBuildings\t{searchResult.GetAllReachableBuilding().Count}");
        report.AppendLine($"rooms\tcount\t{layout.Rooms.Count}");
        int roomIndex = 0;
        foreach (RoomInstance room in layout.Rooms)
        {
            string cells = string.Join(",", room.Cells.Select((cell) => $"{cell.x}:{cell.y}"));
            string furniture = string.Join(",", room.Furniture.Select(Name));
            report.AppendLine(
                $"room\t{roomIndex++}\tusable={room.IsUsable}; self={room.IsSelfContained}; cells={room.Cells.Count}; open={room.OpenBoundaryCount}; solid={room.SolidBoundaryCount}; doors={room.Doors.Count}; walls={room.Walls.Count}; roles={room.FacilityRoles}; furniture={furniture}; cellList={cells}");
        }

        AppendFacility(report, actor, searchResult, policy, roomCache, lowFood, FacilityRole.Meal, "lowFood");
        AppendFacility(report, actor, searchResult, policy, roomCache, general, FacilityRole.Purchase, "general");
        AppendFacility(report, actor, searchResult, policy, roomCache, rest, FacilityRole.Rest, "rest");
        AppendFacility(report, actor, searchResult, policy, roomCache, toilet, FacilityRole.Toilet, "toilet");
        AppendFacility(report, actor, searchResult, policy, roomCache, washroom, FacilityRole.Hygiene, "washroom");
        AppendFacility(report, actor, searchResult, policy, roomCache, lab, FacilityRole.Research, "lab");

        AppendCandidates(report, actor, searchResult, FacilityRole.Meal, "Meal");
        AppendCandidates(report, actor, searchResult, CharacterVisitPolicy.CustomerInterestRoles, "Interest");
        AppendCandidates(report, actor, searchResult, FacilityRole.Rest, "Rest");
        AppendCandidates(report, actor, searchResult, FacilityRole.Toilet, "Toilet");
        AppendCandidates(report, actor, searchResult, FacilityRole.Hygiene, "Hygiene");

        if (general is Shop shop)
        {
            AbilityShopping shopping = customer.GetAbility<AbilityShopping>();
            List<Stock> stocks = shop.GetStock();
            bool canBuy = shopping.CanBuyFrom(shop, out string buyReason);
            bool canServe = shop.CanServeCustomer(actor, out string serveReason);
            report.AppendLine(
                $"shop\tgeneral\tactive={shop.ActiveStockCategory}; current={shop.CurrentStock}; stocks={stocks.Count}; canBuy={canBuy}; buyReason={buyReason}; canServe={canServe}; serveReason={serveReason}; prices={string.Join(",", stocks.Select((stock) => $"{stock.id}:{stock.cost}"))}");
        }

        System.IO.File.WriteAllText(reportPath, report.ToString());
        return reportPath;
    }

    [MenuItem("DungeonStory/Debug/AI/Dump P1 Customer AI Decision Diagnostics")]
    public static void DumpDecisionDiagnosticsFromMenu()
    {
        Debug.Log(DumpDecisionDiagnostics());
    }

    public static string DumpDecisionDiagnostics()
    {
        const string reportPath = "Temp/customer-ai-decision-diagnostic-report.tsv";
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("section\tkey\tvalue");

        AppendUnavailableDestinationDecision(report);
        AppendUnavailableHighScoreDecision(report);
        AppendNeedScoreDecision(report);

        System.IO.File.WriteAllText(reportPath, report.ToString());
        return reportPath;
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

    private static bool VerifyRoleCandidateFiltering()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject lowFood = world.Place("P1_LowFoodShop", new Vector2Int(3, 0));
        BuildableObject meat = world.Place("P1_MeatRestaurant", new Vector2Int(8, 0));
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(13, 0));
        BuildableObject weapon = world.Place("P1_WeaponShop", new Vector2Int(18, 0));
        BuildableObject rest = world.Place("P1_RestRoom", new Vector2Int(23, 0));
        BuildableObject toilet = world.Place("P1_Toilet", new Vector2Int(28, 0));
        BuildableObject washroom = world.Place("P1_Washroom", new Vector2Int(32, 0));
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(36, 0));
        world.PlaceRoomDoorsFor(toilet);
        world.PlaceRoomDoorsFor(washroom);
        world.StaffShop(lowFood);
        world.StaffShop(meat);
        world.StaffShop(general);
        world.StaffShop(weapon);
        CharacterActor slime = world.CreateCustomer("Slime", Vector2Int.zero, 20f, 70f, 20f, 70f);
        GridPathSearchResult searchResult = world.Grid.SearchPath(Vector2Int.zero);

        List<BuildableObject> mealCandidates = FacilityCandidateScorer.GetCandidates(CharacterActor.From(slime), searchResult, FacilityRole.Meal);
        List<BuildableObject> interestCandidates = FacilityCandidateScorer.GetCandidates(CharacterActor.From(slime), searchResult, CharacterVisitPolicy.CustomerInterestRoles);
        List<BuildableObject> restCandidates = FacilityCandidateScorer.GetCandidates(CharacterActor.From(slime), searchResult, FacilityRole.Rest);
        List<BuildableObject> toiletCandidates = FacilityCandidateScorer.GetCandidates(CharacterActor.From(slime), searchResult, FacilityRole.Toilet);
        List<BuildableObject> hygieneCandidates = FacilityCandidateScorer.GetCandidates(CharacterActor.From(slime), searchResult, FacilityRole.Hygiene);

        return mealCandidates.Contains(lowFood)
            && mealCandidates.Contains(meat)
            && !mealCandidates.Contains(general)
            && !mealCandidates.Contains(rest)
            && interestCandidates.Contains(general)
            && interestCandidates.Contains(weapon)
            && interestCandidates.Contains(toilet)
            && interestCandidates.Contains(washroom)
            && interestCandidates.Contains(lab)
            && !interestCandidates.Contains(lowFood)
            && restCandidates.SequenceEqual(new[] { rest })
            && toiletCandidates.SequenceEqual(new[] { toilet })
            && hygieneCandidates.SequenceEqual(new[] { washroom });
    }

    private static bool VerifyActionScoreCompensation()
    {
        AIWait actionSet = ScriptableObject.CreateInstance<AIWait>();
        FixedScoreConsideration highA = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        FixedScoreConsideration highB = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        FixedScoreConsideration overMax = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        FixedScoreConsideration half = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        FixedScoreConsideration midA = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        FixedScoreConsideration midB = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        FixedScoreConsideration midC = ScriptableObject.CreateInstance<FixedScoreConsideration>();

        try
        {
            highA.FixedScore = 0.9f;
            highB.FixedScore = 0.9f;
            SetConsiderations(actionSet, highA, highB);
            float highScore = new AIAction { actionset = actionSet }.CalculateScore((CharacterActor)null);
            bool highScoreUsesWeightedScore =
                NearlyEqual(highScore, ExpectedWeightedScore(0.9f, 0.9f))
                && highScore < 0.99f;

            overMax.FixedScore = 2f;
            half.FixedScore = 0.5f;
            SetConsiderations(actionSet, overMax, half);
            float clampedScore = new AIAction { actionset = actionSet }.CalculateScore((CharacterActor)null);
            bool clampsConsiderationScores =
                NearlyEqual(clampedScore, ExpectedWeightedScore(1f, 0.5f));

            midA.FixedScore = 0.5f;
            midB.FixedScore = 0.5f;
            midC.FixedScore = 0.5f;
            SetConsiderations(actionSet, midA, midB, midC);
            float threeScore = new AIAction { actionset = actionSet }.CalculateScore((CharacterActor)null);
            bool countDoesNotInflateScore =
                NearlyEqual(threeScore, ExpectedWeightedScore(0.5f, 0.5f, 0.5f));

            return highScoreUsesWeightedScore
                && clampsConsiderationScores
                && countDoesNotInflateScore;
        }
        finally
        {
            Object.DestroyImmediate(actionSet);
            Object.DestroyImmediate(highA);
            Object.DestroyImmediate(highB);
            Object.DestroyImmediate(overMax);
            Object.DestroyImmediate(half);
            Object.DestroyImmediate(midA);
            Object.DestroyImmediate(midB);
            Object.DestroyImmediate(midC);
        }
    }

    private static bool VerifyNeedScoresDriveActionPriority()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject lowFood = world.Place("P1_LowFoodShop", new Vector2Int(4, 0));
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(10, 0));
        world.Place("P1_RestRoom", new Vector2Int(16, 0));
        BuildableObject toilet = world.Place("P1_Toilet", new Vector2Int(22, 0));
        BuildableObject washroom = world.Place("P1_Washroom", new Vector2Int(28, 0));
        world.PlaceRoomDoorsFor(toilet);
        world.PlaceRoomDoorsFor(washroom);
        world.StaffShop(lowFood);
        world.StaffShop(general);
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 10f, 90f, 90f, 90f);
        CharacterActor customerActor = CharacterActor.From(customer);

        ConsiderationFacilityNeed mealNeed = CreateNeed(FacilityRole.Meal);
        ConsiderationFacilityNeed interestNeed = CreateNeed(CharacterVisitPolicy.CustomerInterestRoles);
        ConsiderationFacilityNeed restNeed = CreateNeed(FacilityRole.Rest);
        ConsiderationFacilityNeed toiletNeed = CreateNeed(FacilityRole.Toilet);
        ConsiderationFacilityNeed hygieneNeed = CreateNeed(FacilityRole.Hygiene);

        bool hungryPrefersMeal = mealNeed.ScoreConsideration(customerActor) > interestNeed.ScoreConsideration(customerActor)
            && mealNeed.ScoreConsideration(customerActor) > restNeed.ScoreConsideration(customerActor);

        SetStats(customer, 90f, 90f, 10f, 20f);
        bool boredPrefersInterest = interestNeed.ScoreConsideration(customerActor) > mealNeed.ScoreConsideration(customerActor)
            && interestNeed.ScoreConsideration(customerActor) > restNeed.ScoreConsideration(customerActor);

        SetStats(customer, 90f, 10f, 90f, 20f);
        bool tiredPrefersRest = restNeed.ScoreConsideration(customerActor) > mealNeed.ScoreConsideration(customerActor)
            && restNeed.ScoreConsideration(customerActor) > interestNeed.ScoreConsideration(customerActor);

        SetStats(customer, 90f, 90f, 90f, 90f, 5f, 90f);
        bool toiletNeedWins = toiletNeed.ScoreConsideration(customerActor) > mealNeed.ScoreConsideration(customerActor)
            && toiletNeed.ScoreConsideration(customerActor) > hygieneNeed.ScoreConsideration(customerActor);

        SetStats(customer, 90f, 90f, 90f, 90f, 90f, 5f);
        bool hygieneNeedWins = hygieneNeed.ScoreConsideration(customerActor) > mealNeed.ScoreConsideration(customerActor)
            && hygieneNeed.ScoreConsideration(customerActor) > toiletNeed.ScoreConsideration(customerActor);

        Object.DestroyImmediate(mealNeed);
        Object.DestroyImmediate(interestNeed);
        Object.DestroyImmediate(restNeed);
        Object.DestroyImmediate(toiletNeed);
        Object.DestroyImmediate(hygieneNeed);
        return hungryPrefersMeal
            && boredPrefersInterest
            && tiredPrefersRest
            && toiletNeedWins
            && hygieneNeedWins;
    }

    private static bool VerifySpeciesPreferenceCanBeatDistance()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(3, 0));
        BuildableObject weapon = world.Place("P1_WeaponShop", new Vector2Int(18, 0));
        world.StaffShop(general);
        world.StaffShop(weapon);
        GridPathSearchResult searchResult = world.Grid.SearchPath(Vector2Int.zero);

        CharacterActor orc = world.CreateCustomer("Orc", Vector2Int.zero, 90f, 90f, 10f, 30f);
        BuildableObject orcBest = SelectBest(orc, searchResult, CharacterVisitPolicy.CustomerInterestRoles);

        CharacterActor slime = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 30f);
        BuildableObject slimeBest = SelectBest(slime, searchResult, CharacterVisitPolicy.CustomerInterestRoles);

        return orcBest == weapon && slimeBest == general;
    }

    private static bool VerifyToiletAndHygieneFacilityRecovery()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject restRoom = world.Place("P1_RestRoom", new Vector2Int(4, 0));
        BuildableObject toilet = world.Place("P1_Toilet", new Vector2Int(8, 0));
        BuildableObject washroom = world.Place("P1_Washroom", new Vector2Int(12, 0));
        Facility restFacility = restRoom as Facility;
        Facility toiletFacility = toilet as Facility;
        Facility washroomFacility = washroom as Facility;
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 90f, 90f);
        SetStats(customer, 90f, 90f, 90f, 90f, 5f, 10f);

        toiletFacility?.ApplyConfiguredUseRecovery(CharacterActor.From(customer));
        washroomFacility?.ApplyConfiguredUseRecovery(CharacterActor.From(customer));

        return restFacility != null
            && toiletFacility != null
            && washroomFacility != null
            && restFacility.SupportsFacilityRole(FacilityRole.Rest)
            && !restFacility.SupportsFacilityRole(FacilityRole.Toilet)
            && !restFacility.SupportsFacilityRole(FacilityRole.Hygiene)
            && toiletFacility.SupportsFacilityRole(FacilityRole.Toilet)
            && !toiletFacility.SupportsFacilityRole(FacilityRole.Hygiene)
            && washroomFacility.SupportsFacilityRole(FacilityRole.Hygiene)
            && !washroomFacility.SupportsFacilityRole(FacilityRole.Toilet)
            && customer.stats[CharacterCondition.EXCRETION] > 5f
            && customer.stats[CharacterCondition.HYGIENE] > 10f;
    }

    private static bool VerifyVampireSelectsManaOrResearch()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(3, 0));
        BuildableObject lab = world.Place("P1_ResearchLab", new Vector2Int(16, 0));
        BuildableObject mana = world.Place("P1_ManaStorage", new Vector2Int(24, 0));
        world.StaffShop(general);
        GridPathSearchResult searchResult = world.Grid.SearchPath(Vector2Int.zero);

        CharacterActor vampire = world.CreateCustomer("Vampire", Vector2Int.zero, 90f, 90f, 10f, 10f);
        BuildableObject best = SelectBest(vampire, searchResult, CharacterVisitPolicy.CustomerInterestRoles);

        return best != general && (best == lab || best == mana);
    }

    private static bool VerifyUnavailableFacilitiesAreExcluded()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject lowFood = world.Place("P1_LowFoodShop", new Vector2Int(4, 0));
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(10, 0));
        BuildableObject rest = world.Place("P1_RestRoom", new Vector2Int(16, 0));
        world.StaffShop(general);
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 10f, 20f, 10f, 20f);
        GridPathSearchResult searchResult = world.Grid.SearchPath(Vector2Int.zero);

        ClearShopStock(lowFood);
        rest.SetDamaged(true);
        SetHoldingMoney(customer, 0);

        List<BuildableObject> mealCandidates = FacilityCandidateScorer.GetCandidates(CharacterActor.From(customer), searchResult, FacilityRole.Meal);
        List<BuildableObject> restCandidates = FacilityCandidateScorer.GetCandidates(CharacterActor.From(customer), searchResult, FacilityRole.Rest);
        List<BuildableObject> interestCandidates = FacilityCandidateScorer.GetCandidates(CharacterActor.From(customer), searchResult, CharacterVisitPolicy.CustomerInterestRoles);

        bool stockRejected = !mealCandidates.Contains(lowFood)
            && !lowFood.CanVisit(CharacterActor.From(customer), out string stockReason)
            && stockReason == "재고 없음";
        bool damageRejected = !restCandidates.Contains(rest)
            && !rest.CanVisit(CharacterActor.From(customer), out string damageReason)
            && damageReason == "시설 파손";
        bool moneyRejected = !interestCandidates.Contains(general)
            && customer.GetAbility<AbilityShopping>().CanBuyFrom((Shop)general, out string moneyReason) == false
            && moneyReason == "소지금 부족";

        return stockRejected && damageRejected && moneyRejected;
    }

    private static bool VerifySelfServiceCheckoutWithoutWorker()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(4, 0));
        Shop shop = general as Shop;
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 20f);
        CharacterActor calmCustomer = world.CreateCustomer("CalmSlime", new Vector2Int(1, 0), 90f, 90f, 90f, 90f);
        AbilityShopping shopping = customer.GetAbility<AbilityShopping>();
        GridPathSearchResult searchResult = world.Grid.SearchPath(Vector2Int.zero);

        int stockBefore = shop != null ? shop.CurrentStock : -1;
        bool waitsWithoutWorker = shop != null
            && !shop.HasServingWorker
            && !shop.CanServeCustomer(CharacterActor.From(customer), out string workerReason)
            && workerReason == "직원 없음";
        bool candidateWithoutWorker = shopping.CanBuyFrom(shop, out _)
            && FacilityCandidateScorer
            .GetCandidates(CharacterActor.From(customer), searchResult, CharacterVisitPolicy.CustomerInterestRoles)
            .Contains(general);
        bool noSelfCheckout = shop != null && shop.CurrentStock == stockBefore;

        world.StaffShop(general);
        bool availableWithWorker = shop != null
            && shop.HasServingWorker
            && shopping.CanBuyFrom(shop, out _)
            && FacilityCandidateScorer
                .GetCandidates(CharacterActor.From(customer), searchResult, CharacterVisitPolicy.CustomerInterestRoles)
                .Contains(general);

        return waitsWithoutWorker
            && candidateWithoutWorker
            && noSelfCheckout
            && availableWithWorker;
    }

    private static bool VerifyUnstaffedShopAllowsSelfServiceCheckout()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(4, 0));
        Shop shop = general as Shop;
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 20f);
        CharacterActor calmCustomer = world.CreateCustomer("CalmSlime", new Vector2Int(1, 0), 90f, 90f, 90f, 90f);
        AbilityShopping shopping = customer.GetAbility<AbilityShopping>();
        GridPathSearchResult searchResult = world.Grid.SearchPath(Vector2Int.zero);

        List<Stock> selfServiceStocks = shop != null ? shop.GetStock() : null;
        int selfServiceCost = selfServiceStocks != null && selfServiceStocks.Count > 0
            ? selfServiceStocks[0].cost
            : -1;
        float unstaffedCrimeChance = shop != null ? shop.GetCheckoutCrimeChance(customer, 1, selfServiceCost) : 0f;
        float calmCrimeChance = shop != null ? shop.GetCheckoutCrimeChance(calmCustomer, 1, selfServiceCost) : 0f;
        bool availableWithoutWorker = shop != null
            && !shop.HasServingWorker
            && shop.CanServeCustomer(CharacterActor.From(customer), out string workerReason)
            && string.IsNullOrEmpty(workerReason);
        bool candidateWithoutWorker = shopping.CanBuyFrom(shop, out _)
            && FacilityCandidateScorer
                .GetCandidates(CharacterActor.From(customer), searchResult, CharacterVisitPolicy.CustomerInterestRoles)
                .Contains(general);

        world.StaffShop(general);
        List<Stock> staffedStocks = shop != null ? shop.GetStock() : null;
        int staffedCost = staffedStocks != null && staffedStocks.Count > 0
            ? staffedStocks[0].cost
            : -1;
        float staffedCrimeChance = shop != null ? shop.GetCheckoutCrimeChance(customer, 1, staffedCost) : 0f;
        bool availableWithWorker = shop != null
            && shop.HasServingWorker
            && shopping.CanBuyFrom(shop, out _)
            && FacilityCandidateScorer
                .GetCandidates(CharacterActor.From(customer), searchResult, CharacterVisitPolicy.CustomerInterestRoles)
                .Contains(general);
        bool workerImprovesPrice = selfServiceCost > 0 && staffedCost > selfServiceCost;
        bool workerReducesCrime = unstaffedCrimeChance > staffedCrimeChance;
        bool customerContextRaisesCrime = unstaffedCrimeChance > calmCrimeChance;

        return availableWithoutWorker
            && candidateWithoutWorker
            && availableWithWorker
            && workerImprovesPrice
            && workerReducesCrime
            && customerContextRaisesCrime;
    }

    private static bool VerifyCheckoutPatienceStages()
    {
        CustomerCheckoutPatienceProfile impatient = CustomerCheckoutPatienceRules.Create(0.5f, 1f, 1);
        CustomerCheckoutPatienceProfile average = CustomerCheckoutPatienceRules.Create(1f, 1f, 1);
        CustomerCheckoutPatienceProfile patient = CustomerCheckoutPatienceRules.Create(1.8f, 1f, 1);
        CustomerCheckoutPatienceProfile crowded = CustomerCheckoutPatienceRules.Create(1f, 1f, 4);

        return impatient.AbandonSeconds < average.AbandonSeconds
            && average.AbandonSeconds < patient.AbandonSeconds
            && crowded.AbandonSeconds < average.AbandonSeconds
            && CustomerCheckoutPatienceRules.GetStage(average, average.RestlessSeconds - 0.01f)
                == CustomerCheckoutWaitStage.Waiting
            && CustomerCheckoutPatienceRules.GetStage(average, average.RestlessSeconds)
                == CustomerCheckoutWaitStage.Restless
            && CustomerCheckoutPatienceRules.GetStage(average, average.RequestServiceSeconds)
                == CustomerCheckoutWaitStage.RequestingService
            && CustomerCheckoutPatienceRules.GetStage(average, average.AbandonSeconds)
                == CustomerCheckoutWaitStage.Abandoned
            && impatient.AbandonMoodPenalty < patient.AbandonMoodPenalty
            && impatient.AbandonSentiment < patient.AbandonSentiment;
    }

    private static bool VerifyAbandonedCheckoutPreservesVisit()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject shop = world.Place("P1_GeneralStore", new Vector2Int(4, 0));
        BuildableObject alternative = world.Place("P1_WeaponShop", new Vector2Int(12, 0));
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 20f);
        AbilityShopping shopping = customer.GetAbility<AbilityShopping>();
        shopping.RestorePersistentState(2, 0, 100);

        shopping.BeginVisitInteraction(shop);
        shopping.SetVisitOutcome(shop, ShoppingVisitOutcome.Abandoned);
        shopping.RegisterAvoidedVisit(shop);

        return shopping.LastVisitOutcome == ShoppingVisitOutcome.Abandoned
            && shopping.HasVisited(shop)
            && !shopping.HasVisited(alternative)
            && shopping.visitCount == 2
            && shopping.IsThereVisitableBuilding()
            && shopping.FindShop() == alternative;
    }

    private static bool VerifyCheckoutComplaintCreatesFacilityMemory()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject shop = world.Place("P1_GeneralStore", new Vector2Int(4, 0));
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 20f);
        CharacterSocialMemory memory = customer.SocialMemory;
        float before = memory != null ? memory.GetFacilitySentiment(shop) : 0f;

        memory?.RememberFacilityExperience(
            shop,
            -0.75f,
            "계산이 너무 늦어 구매를 포기했다.",
            600f);

        float after = memory != null ? memory.GetFacilitySentiment(shop) : 0f;
        return memory != null
            && after < before
            && memory.RecentRumors.Any(rumor => rumor != null
                && rumor.type == SocialRumorType.Complaint
                && rumor.targetType == SocialRumorTargetType.Facility
                && rumor.targetFacilityId == shop.id);
    }

    private static bool VerifyCheckoutWaitCoroutineAbandons()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        Shop shop = world.Place("P1_GeneralStore", new Vector2Int(4, 0), requireStaffedService: true) as Shop;
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 70f);
        AbilityShopping shopping = customer.GetAbility<AbilityShopping>();
        if (shop == null || shopping == null || shop.HasServingWorker)
        {
            return false;
        }

        customer.Identity.Data.aiPersonality.patience = 0.25f;
        shopping.BeginVisitInteraction(shop);
        float moodBefore = customer.Mood.Value;

        Type sessionType = typeof(Shop).GetNestedType(
            "CheckoutWaitSession",
            BindingFlags.NonPublic);
        MethodInfo waitMethod = typeof(Shop).GetMethod(
            "WaitForServingWorkerWithPatience",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (sessionType == null || waitMethod == null)
        {
            return false;
        }

        object session = Activator.CreateInstance(sessionType);
        IEnumerator routine = waitMethod.Invoke(shop, new[] { customer, session }) as IEnumerator;
        int steps = 0;
        while (routine != null && routine.MoveNext() && steps++ < 200)
        {
        }

        bool abandoned = (bool)(sessionType.GetProperty("Abandoned")?.GetValue(session) ?? false);
        return routine != null
            && steps < 200
            && abandoned
            && shopping.LastVisitOutcome == ShoppingVisitOutcome.Abandoned
            && shop.WaitingCheckoutCount == 0
            && customer.Mood.Value < moodBefore
            && customer.SocialMemory.GetFacilitySentiment(shop) < 0f;
    }

    private static bool VerifyUnaffordableShopEndsVisitCycle()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(4, 0));
        world.StaffShop(general);
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 20f);
        AbilityShopping shopping = customer.GetAbility<AbilityShopping>();
        AILookAround lookAround = ScriptableObject.CreateInstance<AILookAround>();
        SetHoldingMoney(customer, 0);

        try
        {
            return general.CanVisit(CharacterActor.From(customer), out _)
                && !shopping.CanBuyFrom((Shop)general, out _)
                && !shopping.IsThereVisitableBuilding()
                && shopping.ShouldEndVisitCycle()
                && shopping.CanLookAround()
                && lookAround.CanStartWithContext(CharacterActor.From(customer), world.Grid.SearchPath(Vector2Int.zero), out _);
        }
        finally
        {
            Object.DestroyImmediate(lookAround);
        }
    }

    private static bool VerifyCompletedVisitExitsImmediately()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 20f);
        AbilityShopping shopping = customer.GetAbility<AbilityShopping>();
        AIExitDungeon exit = ScriptableObject.CreateInstance<AIExitDungeon>();
        shopping.RestorePersistentState(0, 0, 0);

        try
        {
            return shopping.ShouldEndVisitCycle()
                && !shopping.CanLookAround()
                && shopping.ShouldExitDungeon()
                && exit.CanStart(CharacterActor.From(customer));
        }
        finally
        {
            Object.DestroyImmediate(exit);
        }
    }

    private static bool VerifyVisitorActionsAreCompletedFromPartialPrefab()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 20f);
        customer.ai.availableActions = new[]
        {
            new AIAction { actionset = Resources.Load<AIActionSet>("SO/AI/Action/Eat") },
            new AIAction { actionset = Resources.Load<AIActionSet>("SO/AI/Action/Shopping") },
            new AIAction { actionset = Resources.Load<AIActionSet>("SO/AI/Action/Rest") },
        };

        customer.ai.Initializtion(customer.data);
        Type[] actionTypes = customer.ai.availableActions
            .Where((action) => action != null && action.actionset != null)
            .Select((action) => action.actionset.GetType())
            .ToArray();
        bool hasToiletAction = customer.ai.availableActions.Any((action) =>
            action != null
            && action.actionset is AIFacilityRoleAction roleAction
            && roleAction.Role == FacilityRole.Toilet);
        bool hasHygieneAction = customer.ai.availableActions.Any((action) =>
            action != null
            && action.actionset is AIFacilityRoleAction roleAction
            && roleAction.Role == FacilityRole.Hygiene);

        return actionTypes.Contains(typeof(AIEat))
            && actionTypes.Contains(typeof(AIRest))
            && actionTypes.Contains(typeof(AIShopping))
            && hasToiletAction
            && hasHygieneAction
            && actionTypes.Contains(typeof(AILookAround))
            && actionTypes.Contains(typeof(AIExitDungeon));
    }

    private static bool VerifyUnavailableDestinationActionsReportNoAction()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 5f, 5f, 5f, 5f);
        List<ScriptableObject> owned = new List<ScriptableObject>();

        try
        {
            AIEat eat = CreateScoredAction<AIEat>(owned, 1f);
            AIRest rest = CreateScoredAction<AIRest>(owned, 0.9f);
            AIShopping shopping = CreateScoredAction<AIShopping>(owned, 0.8f);
            customer.ai.availableActions = new[]
            {
                new AIAction { actionset = eat },
                new AIAction { actionset = rest },
                new AIAction { actionset = shopping }
            };

            GridPathSearchResult searchResult = customer.ai.GetPathSearch(CharacterActor.From(customer));
            bool eatUnavailable = !eat.CanStartWithContext(CharacterActor.From(customer), searchResult, out _);
            bool restUnavailable = !rest.CanStartWithContext(CharacterActor.From(customer), searchResult, out _);
            bool shoppingUnavailable = !shopping.CanStartWithContext(CharacterActor.From(customer), searchResult, out _);
            bool decided = customer.ai.DecideAction();

            return eatUnavailable
                && restUnavailable
                && shoppingUnavailable
                && decided
                && customer.ai.bestAction != null
                && customer.ai.bestAction.actionset is AILookAround
                && customer.ai.bestAction.planKind == AIActionPlanKind.NoDestination;
        }
        finally
        {
            DestroyScriptableObjects(owned);
        }
    }

    private static bool VerifyUnavailableHighScoreNeedSelectsReachableAction()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject restRoom = world.Place("P1_RestRoom", new Vector2Int(6, 0));
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 5f, 5f, 90f, 90f);
        List<ScriptableObject> owned = new List<ScriptableObject>();

        try
        {
            AIEat eat = CreateScoredAction<AIEat>(owned, 1f);
            AIRest rest = CreateScoredAction<AIRest>(owned, 0.6f);
            AIWait wait = CreateScoredAction<AIWait>(owned, 0.1f);
            customer.ai.availableActions = new[]
            {
                new AIAction { actionset = eat },
                new AIAction { actionset = rest },
                new AIAction { actionset = wait }
            };

            bool decided = customer.ai.DecideAction();
            return decided
                && customer.ai.bestAction != null
                && customer.ai.bestAction.actionset == rest
                && customer.ai.bestAction.destination == restRoom
                && customer.ai.bestAction.planKind == AIActionPlanKind.MovePath;
        }
        finally
        {
            DestroyScriptableObjects(owned);
        }
    }

    private static bool VerifyNoveltyAndCrowdScores()
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(4, 0));
        BuildableObject rest = world.Place("P1_RestRoom", new Vector2Int(10, 0));
        world.StaffShop(general);
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 10f);
        GridPathSearchResult searchResult = world.Grid.SearchPath(Vector2Int.zero);
        FacilityScoringContext scoringContext = CreateIsolatedFacilityScoringContext();

        float freshScore = FacilityCandidateScorer.ScoreCandidate(
            CharacterActor.From(customer),
            general,
            CharacterVisitPolicy.CustomerInterestRoles,
            searchResult,
            scoringContext);
        customer.GetAbility<AbilityShopping>().RegisterVisit(general);
        float revisitedScore = FacilityCandidateScorer.ScoreCandidate(
            CharacterActor.From(customer),
            general,
            CharacterVisitPolicy.CustomerInterestRoles,
            searchResult,
            scoringContext);

        float emptyRestScore = FacilityCandidateScorer.ScoreCandidate(
            CharacterActor.From(customer),
            rest,
            FacilityRole.Rest,
            searchResult,
            scoringContext);
        rest.TryBeginUse(CharacterActor.From(customer), out _);
        rest.TryBeginUse(CharacterActor.From(customer), out _);
        float crowdedRestScore = FacilityCandidateScorer.ScoreCandidate(
            CharacterActor.From(customer),
            rest,
            FacilityRole.Rest,
            searchResult,
            scoringContext);

        return revisitedScore < freshScore && crowdedRestScore < emptyRestScore;
    }

    private static BuildableObject SelectBest(
        CharacterActor character,
        GridPathSearchResult searchResult,
        FacilityRole role)
    {
        List<BuildableObject> candidates = FacilityCandidateScorer.GetCandidates(CharacterActor.From(character), searchResult, role);
        return FacilityCandidateScorer.SelectBest(
            CharacterActor.From(character),
            candidates,
            role,
            searchResult,
            CreateIsolatedFacilityScoringContext());
    }

    private static ConsiderationFacilityNeed CreateNeed(FacilityRole role)
    {
        ConsiderationFacilityNeed need = ScriptableObject.CreateInstance<ConsiderationFacilityNeed>();
        need.Role = role;
        return need;
    }

    private static FacilityScoringContext CreateIsolatedFacilityScoringContext()
    {
        return FacilityScoringContext.WithoutReputationBiasForIsolatedTest(
            new RoomFacilityPolicyService(new RoomLayoutCache()));
    }

    private static void SetStats(CharacterActor character, float hunger, float sleep, float fun, float mood)
    {
        SetStats(character, hunger, sleep, fun, mood, 100f, 100f);
    }

    private static void SetStats(
        CharacterActor character,
        float hunger,
        float sleep,
        float fun,
        float mood,
        float excretion,
        float hygiene)
    {
        character.stats[CharacterCondition.HUNGER] = hunger;
        character.stats[CharacterCondition.SLEEP] = sleep;
        character.stats[CharacterCondition.FUN] = fun;
        character.stats[CharacterCondition.MOOD] = mood;
        character.stats[CharacterCondition.EXCRETION] = excretion;
        character.stats[CharacterCondition.HYGIENE] = hygiene;
    }

    private static void SetHoldingMoney(CharacterActor character, int money)
    {
        AbilityShopping shopping = character.GetAbility<AbilityShopping>();
        FieldInfo field = typeof(AbilityShopping).GetField("holdingMoney", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(shopping, money);
    }

    private static void ClearShopStock(BuildableObject building)
    {
        FieldInfo field = typeof(Shop).GetField("stocks", BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(building, new List<RemainStock>());
    }

    private static void SetConsiderations(AIActionSet actionSet, params Consideration[] considerations)
    {
        FieldInfo field = typeof(AIActionSet).GetField(
            "<considerations>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(actionSet, considerations);
    }

    private static T CreateScoredAction<T>(List<ScriptableObject> owned, float score)
        where T : AIActionSet
    {
        T action = ScriptableObject.CreateInstance<T>();
        FixedScoreConsideration consideration = ScriptableObject.CreateInstance<FixedScoreConsideration>();
        consideration.FixedScore = score;
        SetConsiderations(action, consideration);
        owned.Add(action);
        owned.Add(consideration);
        return action;
    }

    private static void DestroyScriptableObjects(List<ScriptableObject> objects)
    {
        foreach (ScriptableObject obj in objects)
        {
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private static float ExpectedWeightedScore(params float[] scores)
    {
        if (scores == null || scores.Length == 0)
        {
            return 1f;
        }

        float totalScore = 0f;
        foreach (float score in scores)
        {
            float clampedScore = Mathf.Clamp01(score);
            if (clampedScore <= 0f)
            {
                return 0f;
            }

            totalScore += clampedScore;
        }

        return Mathf.Clamp01(totalScore / scores.Length);
    }

    private static bool NearlyEqual(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f;
    }

    private static void AppendUnavailableDestinationDecision(System.Text.StringBuilder report)
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 5f, 5f, 5f, 5f);
        List<ScriptableObject> owned = new List<ScriptableObject>();

        try
        {
            AIEat eat = CreateScoredAction<AIEat>(owned, 1f);
            AIRest rest = CreateScoredAction<AIRest>(owned, 0.9f);
            AIShopping shopping = CreateScoredAction<AIShopping>(owned, 0.8f);
            customer.ai.availableActions = new[]
            {
                new AIAction { actionset = eat },
                new AIAction { actionset = rest },
                new AIAction { actionset = shopping }
            };

            CharacterActor actor = CharacterActor.From(customer);
            GridPathSearchResult search = customer.ai.GetPathSearch(actor) ?? world.Grid.SearchPath(Vector2Int.zero);
            report.AppendLine(
                $"decision\tunavailable_pre\tcanRun={customer.CanRunAi}; visit={customer.GetAbility<AbilityShopping>().visitCount}; lookAround={AILookAround.CanUseVisitLookAround(actor)}; eatStart={eat.CanStartWithContext(actor, search, out string eatReason)}:{eatReason}; restStart={rest.CanStartWithContext(actor, search, out string restReason)}:{restReason}; shoppingStart={shopping.CanStartWithContext(actor, search, out string shoppingReason)}:{shoppingReason}");
            bool decided = customer.ai.DecideAction();
            AppendBrainDecision(report, "unavailable_post", customer, decided);
        }
        finally
        {
            DestroyScriptableObjects(owned);
        }
    }

    private static void AppendUnavailableHighScoreDecision(System.Text.StringBuilder report)
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject restRoom = world.Place("P1_RestRoom", new Vector2Int(6, 0));
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 5f, 5f, 90f, 90f);
        List<ScriptableObject> owned = new List<ScriptableObject>();

        try
        {
            AIEat eat = CreateScoredAction<AIEat>(owned, 1f);
            AIRest rest = CreateScoredAction<AIRest>(owned, 0.6f);
            AIWait wait = CreateScoredAction<AIWait>(owned, 0.1f);
            customer.ai.availableActions = new[]
            {
                new AIAction { actionset = eat },
                new AIAction { actionset = rest },
                new AIAction { actionset = wait }
            };

            CharacterActor actor = CharacterActor.From(customer);
            GridPathSearchResult search = customer.ai.GetPathSearch(actor) ?? world.Grid.SearchPath(Vector2Int.zero);
            report.AppendLine(
                $"decision\thighscore_pre\trestCandidate={FacilityCandidateScorer.IsCandidate(actor, restRoom, FacilityRole.Rest, out string restCandidateReason)}:{restCandidateReason}; restReachable={search.ContainsVisitableOccupant(restRoom)}; restCanVisit={restRoom.CanVisit(actor, out string restVisitReason)}:{restVisitReason}; eatStart={eat.CanStartWithContext(actor, search, out string eatReason)}:{eatReason}; restStart={rest.CanStartWithContext(actor, search, out string restReason)}:{restReason}; waitStart={wait.CanStartWithContext(actor, search, out string waitReason)}:{waitReason}");
            bool decided = customer.ai.DecideAction();
            AppendBrainDecision(report, "highscore_post", customer, decided);
        }
        finally
        {
            DestroyScriptableObjects(owned);
        }
    }

    private static void AppendNeedScoreDecision(System.Text.StringBuilder report)
    {
        using CustomerAiScenarioWorld world = new CustomerAiScenarioWorld();
        BuildableObject lowFood = world.Place("P1_LowFoodShop", new Vector2Int(4, 0));
        BuildableObject general = world.Place("P1_GeneralStore", new Vector2Int(10, 0));
        world.Place("P1_RestRoom", new Vector2Int(16, 0));
        BuildableObject toilet = world.Place("P1_Toilet", new Vector2Int(22, 0));
        BuildableObject washroom = world.Place("P1_Washroom", new Vector2Int(28, 0));
        world.PlaceRoomDoorsFor(toilet);
        world.PlaceRoomDoorsFor(washroom);
        world.StaffShop(lowFood);
        world.StaffShop(general);
        CharacterActor customer = world.CreateCustomer("Slime", Vector2Int.zero, 10f, 90f, 90f, 90f);
        CharacterActor actor = CharacterActor.From(customer);

        ConsiderationFacilityNeed mealNeed = CreateNeed(FacilityRole.Meal);
        ConsiderationFacilityNeed interestNeed = CreateNeed(CharacterVisitPolicy.CustomerInterestRoles);
        ConsiderationFacilityNeed restNeed = CreateNeed(FacilityRole.Rest);
        ConsiderationFacilityNeed toiletNeed = CreateNeed(FacilityRole.Toilet);
        ConsiderationFacilityNeed hygieneNeed = CreateNeed(FacilityRole.Hygiene);

        try
        {
            AppendNeedScores(report, "hungry", customer, actor, mealNeed, interestNeed, restNeed, toiletNeed, hygieneNeed);
            SetStats(customer, 90f, 90f, 10f, 20f);
            AppendNeedScores(report, "bored", customer, actor, mealNeed, interestNeed, restNeed, toiletNeed, hygieneNeed);
            SetStats(customer, 90f, 10f, 90f, 20f);
            AppendNeedScores(report, "tired", customer, actor, mealNeed, interestNeed, restNeed, toiletNeed, hygieneNeed);
            SetStats(customer, 90f, 90f, 90f, 90f, 5f, 90f);
            AppendNeedScores(report, "toilet", customer, actor, mealNeed, interestNeed, restNeed, toiletNeed, hygieneNeed);
            SetStats(customer, 90f, 90f, 90f, 90f, 90f, 5f);
            AppendNeedScores(report, "hygiene", customer, actor, mealNeed, interestNeed, restNeed, toiletNeed, hygieneNeed);
        }
        finally
        {
            Object.DestroyImmediate(mealNeed);
            Object.DestroyImmediate(interestNeed);
            Object.DestroyImmediate(restNeed);
            Object.DestroyImmediate(toiletNeed);
            Object.DestroyImmediate(hygieneNeed);
        }
    }

    private static void AppendNeedScores(
        System.Text.StringBuilder report,
        string key,
        CharacterActor customer,
        CharacterActor actor,
        ConsiderationFacilityNeed mealNeed,
        ConsiderationFacilityNeed interestNeed,
        ConsiderationFacilityNeed restNeed,
        ConsiderationFacilityNeed toiletNeed,
        ConsiderationFacilityNeed hygieneNeed)
    {
        report.AppendLine(
            $"need\t{key}\tH={customer.stats[CharacterCondition.HUNGER]}; S={customer.stats[CharacterCondition.SLEEP]}; F={customer.stats[CharacterCondition.FUN]}; M={customer.stats[CharacterCondition.MOOD]}; E={customer.stats[CharacterCondition.EXCRETION]}; Y={customer.stats[CharacterCondition.HYGIENE]}; meal={mealNeed.ScoreConsideration(actor):0.###}; interest={interestNeed.ScoreConsideration(actor):0.###}; rest={restNeed.ScoreConsideration(actor):0.###}; toilet={toiletNeed.ScoreConsideration(actor):0.###}; hygiene={hygieneNeed.ScoreConsideration(actor):0.###}");
    }

    private static void AppendBrainDecision(
        System.Text.StringBuilder report,
        string key,
        CharacterActor customer,
        bool decided)
    {
        AIAction best = customer.ai.bestAction;
        string bestAction = best?.actionset != null ? best.actionset.GetType().Name : "null";
        string destination = Name(best?.destination);
        string candidates = string.Join(
            ",",
            customer.ai.LastCandidateScores.Select((candidate) =>
                $"{candidate.ActionLabel}:{candidate.Score:0.###}:{candidate.Failure.Kind}:{Name(candidate.Destination)}"));
        report.AppendLine(
            $"decision\t{key}\tdecided={decided}; best={bestAction}; plan={best?.planKind}; destination={destination}; failure={customer.ai.LastActionFailure.Kind}; candidates={candidates}; summary={customer.ai.GetDebugSummary(8).Replace('\n', ' ')}");
    }

    private static void AppendFacility(
        System.Text.StringBuilder report,
        CharacterActor actor,
        GridPathSearchResult searchResult,
        IRoomFacilityPolicy policy,
        IRoomLayoutCache roomCache,
        BuildableObject building,
        FacilityRole role,
        string label)
    {
        bool roleAvailable = policy.IsFacilityRoleAvailable(building, role, out string roomReason);
        string visitReason = string.Empty;
        bool canVisit = building != null && building.CanVisit(actor, out visitReason);
        bool candidate = FacilityCandidateScorer.IsCandidate(actor, building, role, out string candidateReason);
        bool reachable = searchResult != null && searchResult.ContainsVisitableOccupant(building);
        bool hasRoom = roomCache.TryGetRoom(building, out RoomInstance room);
        string roomText = hasRoom
            ? $"roomId={room.Id}; usable={room.IsUsable}; self={room.IsSelfContained}; roles={room.FacilityRoles}"
            : "room=none";
        report.AppendLine(
            $"facility\t{label}\tname={Name(building)}; pos={building?.centerPos}; size={building?.BuildingData?.width}x{building?.BuildingData?.height}; roles={building?.Facility?.roles}; requiresRoom={building?.BuildingData.RequiresRoomRole()}; roleAvailable={roleAvailable}; roomReason={roomReason}; canVisit={canVisit}; visitReason={visitReason}; reachable={reachable}; candidate={candidate}; candidateReason={candidateReason}; {roomText}");
    }

    private static void AppendCandidates(
        System.Text.StringBuilder report,
        CharacterActor actor,
        GridPathSearchResult searchResult,
        FacilityRole role,
        string label)
    {
        List<BuildableObject> candidates = FacilityCandidateScorer.GetCandidates(actor, searchResult, role);
        report.AppendLine(
            $"candidates\t{label}\t{string.Join(",", candidates.Select(Name))}");
    }

    private static string Name(BuildableObject building)
    {
        if (building == null)
        {
            return "null";
        }

        return building.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building.name;
    }

    private sealed class FixedScoreConsideration : Consideration
    {
        public float FixedScore { get; set; }

        public override float ScoreConsideration(CharacterActor actor)
        {
            return FixedScore;
        }
    }

    internal sealed class CustomerAiScenarioWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CharacterAwakeMethod =
            typeof(CharacterActor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo CharacterAiSchedulerInstanceField =
            typeof(CharacterAiScheduler).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo ShopWorkerField =
            typeof(Shop).GetField("worker", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly CharacterAiScheduler previousScheduler;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();
        private readonly GameObject gridSystemObject;

        public CustomerAiScenarioWorld()
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;
            previousScheduler = CharacterAiSchedulerInstanceField?.GetValue(null) as CharacterAiScheduler;
            CharacterAiSchedulerInstanceField?.SetValue(null, null);
            RoomRegistry.Clear();
            FacilityCandidateCache.Clear();
            Grid = new Grid(40, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                Grid.RegisterOccupant(
                    new TestHallwayOccupant(),
                    GridLayer.Hallway,
                    new List<Vector2Int> { new Vector2Int(x, 0) },
                    false);
            }

            gridSystemObject = new GameObject("Customer AI Scenario GridSystemManager");
            objects.Add(gridSystemObject);
            GridSystemInstanceField?.SetValue(null, null);
            GridSystemManager manager = gridSystemObject.AddComponent<GridSystemManager>();
            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);
            PlaceRuntimeBoundary("Door", new Vector2Int(1, 0), BuildingCategory.Movement);
            PlaceRuntimeBoundary("Wall", new Vector2Int(Grid.width - 2, 0), BuildingCategory.Wall);
        }

        public Grid Grid { get; }

        public BuildableObject Place(
            string assetName,
            Vector2Int position,
            bool requireStaffedService = false)
        {
            BuildingSO buildingData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                $"Assets/Resources/SO/Building/P1/{assetName}.asset");
            if (buildingData == null)
            {
                throw new InvalidOperationException($"{assetName} asset not found.");
            }

            if (requireStaffedService && !buildingData.RequiresStaffedService())
            {
                buildingData = Object.Instantiate(buildingData);
                buildingData.name = $"{assetName} Staffed Checkout Test";
                buildingData.AbilityModules.Add(new BuildingStaffedServiceAbility());
                scriptableObjects.Add(buildingData);
            }

            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            if (building == null)
            {
                throw new InvalidOperationException($"{assetName} could not be created.");
            }

            objects.Add(building.gameObject);
            CharacterAiEditorTestDependencies.Inject(building);
            building.SetGrid(Grid);
            building.Initialization(buildingData, position);
            bool registered = Grid.RegisterOccupant(
                building,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            if (!registered)
            {
                throw new InvalidOperationException($"{assetName} could not be registered.");
            }

            if (building is Shop shop)
            {
                CharacterAiEditorTestDependencies.InjectShop(shop);
            }

            if (building.BuildingData.RequiresRoomRole())
            {
                PlaceRoomDoorsFor(building);
            }

            return building;
        }

        public void PlaceRoomDoorsFor(BuildableObject building)
        {
            // The scenario world is already one formal room spanning the row.
            // Extra doors beside each fixture would split that test interior.
        }

        private BuildableObject PlaceRuntimeDoor(Vector2Int position)
        {
            return PlaceRuntimeBoundary("Door", position, BuildingCategory.Movement);
        }

        private BuildableObject PlaceRuntimeBoundary(
            string objectName,
            Vector2Int position,
            BuildingCategory category)
        {
            if (!Grid.IsValidGridPos(position))
            {
                throw new InvalidOperationException($"Room boundary position {position} is outside the grid.");
            }

            GridCell cell = Grid.GetGridCell(position);
            BuildableObject existing = cell.GetBuildingInlayer(GridLayer.Building);
            if (existing != null)
            {
                return existing;
            }

            BuildingSO buildingData = ScriptableObject.CreateInstance<BuildingSO>();
            scriptableObjects.Add(buildingData);
            buildingData.id = category == BuildingCategory.Wall ? 902 : 901;
            buildingData.objectName = objectName;
            buildingData.width = 1;
            buildingData.height = 1;
            buildingData.layer = GridLayer.Building;
            buildingData.category = category;
            buildingData.type = category == BuildingCategory.Movement
                ? typeof(Door)
                : typeof(BuildableObject);
            buildingData.unlocked = true;
            buildingData.Facility = new FacilityData();

            GameObject obj = new GameObject($"Room Boundary {objectName}");
            objects.Add(obj);
            BuildableObject boundary = obj.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(boundary);
            boundary.SetGrid(Grid);
            boundary.Initialization(buildingData, position);
            bool registered = Grid.RegisterOccupant(
                boundary,
                GridLayer.Building,
                buildingData.GetGridPosList(position),
                category == BuildingCategory.Movement);
            if (!registered)
            {
                throw new InvalidOperationException($"Room boundary at {position} could not be registered.");
            }

            return boundary;
        }

        public CharacterActor CreateCustomer(
            string speciesTag,
            Vector2Int position,
            float hunger,
            float sleep,
            float fun,
            float mood)
        {
            GameObject obj = new GameObject($"Customer AI Test {speciesTag}");
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityShopping>();
            AIBrain brain = obj.AddComponent<AIBrain>();
            brain.availableActions = AiDebugScenarioActionFactory.CreateCustomerActions();
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(obj);
            CharacterAwakeMethod?.Invoke(character, null);

            CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
            scriptableObjects.Add(data);
            SetPrivateField(data, "frequencyVisitMin", 3);
            SetPrivateField(data, "frequencyVisitMax", 3);
            SetPrivateField(data, "minHoldingMoney", 500);
            SetPrivateField(data, "maxHoldingMoney", 600);
            data.characterType = CharacterType.Customer;
            data.characterName = speciesTag;
            data.speciesTag = speciesTag;

            obj.transform.position = Grid.GetWorldPos(position);
            character.Initialization(data);
            character.SetLifecycleState(CharacterLifecycleState.Active);
            SetStats(character, hunger, sleep, fun, mood);
            return character;
        }

        public void StaffShop(BuildableObject building)
        {
            if (building is not Shop shop)
            {
                return;
            }

            CharacterActor worker = CreateStaff($"Worker {building.name}", building.centerPos);
            ShopWorkerField?.SetValue(shop, worker);
            FacilityCandidateCache.MarkDynamicStateDirty();
        }

        private CharacterActor CreateStaff(string name, Vector2Int position)
        {
            GameObject obj = new GameObject($"Customer AI Test {name}");
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<AbilityMove>();
            obj.AddComponent<AbilityShopping>();
            obj.AddComponent<AbilityWork>();
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(obj);
            CharacterAwakeMethod?.Invoke(character, null);

            CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
            scriptableObjects.Add(data);
            data.characterType = CharacterType.NPC;
            data.characterName = name;
            data.speciesTag = "Slime";

            obj.transform.position = Grid.GetWorldPos(position);
            character.Initialization(data);
            character.SetLifecycleState(CharacterLifecycleState.Active);
            return character;
        }

        public void Dispose()
        {
            GridSystemInstanceField?.SetValue(null, previousGridSystem);
            CharacterAiSchedulerInstanceField?.SetValue(null, previousScheduler);
            RoomRegistry.Clear();
            FacilityCandidateCache.Clear();
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                if (Application.isPlaying)
                {
                    obj.SetActive(false);
                    Object.Destroy(obj);
                }
                else
                {
                    Object.DestroyImmediate(obj);
                }
            }

            foreach (ScriptableObject obj in scriptableObjects.Where((obj) => obj != null))
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(obj);
                }
                else
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(target, value);
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }
}
