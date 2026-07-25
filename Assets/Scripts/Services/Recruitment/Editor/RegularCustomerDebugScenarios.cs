using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RegularCustomerDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Recruitment/Run P2 Regular Customer Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P2 regular customer scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("방문 횟수와 평균 만족도 기록", VerifyVisitCountAndAverageSatisfaction, errors);
        RunScenario("단골 기준 달성", VerifyRegularThreshold, errors);
        RunScenario("영입 후보 기준 달성", VerifyRecruitCandidateThreshold, errors);
        RunScenario("기본 던전 만족도로 영입 루프 도달", VerifyDefaultRecruitmentThreshold, errors);
        RunScenario("영입 결과와 소비 고객 제외", VerifyRecruitmentConsumesCustomer, errors);
        RunScenario("낮은 만족도는 단골 제외", VerifyLowSatisfactionDoesNotBecomeRegular, errors);
        RunScenario("방문 이벤트 런타임 연결", VerifyRuntimeEvents, errors);

        RunScenario("offense reward promotes known visitors into recruit candidates", VerifyOffenseRewardPromotesCandidates, errors);
        RunScenario("visit event keeps an immutable customer snapshot", VerifyVisitEventSnapshotDoesNotDrift, errors);

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
            Debug.Log("P2 regular customer scenarios passed.");
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

    private static bool VerifyVisitCountAndAverageSatisfaction()
    {
        RegularCustomerState state = new RegularCustomerState();
        RegularCustomerRules rules = CreateTestRules();
        CharacterActor customer = CreateCustomer(101, "Slime Regular", "Slime", 80f);

        state.RecordVisit(CharacterActor.From(customer), rules);
        customer.stats[CharacterCondition.MOOD] = 60f;
        RegularCustomerVisitResult result = state.RecordVisit(CharacterActor.From(customer), rules);

        bool valid = result.Success
            && result.Record.VisitCount == 2
            && Mathf.Approximately(result.Record.AverageSatisfaction, 70f)
            && result.Record.Status == RegularCustomerStatus.Visitor;

        DestroyCustomer(customer);
        return valid;
    }

    private static bool VerifyRegularThreshold()
    {
        RegularCustomerState state = new RegularCustomerState();
        RegularCustomerRules rules = CreateTestRules();
        CharacterActor customer = CreateCustomer(102, "Orc Regular", "Orc", 70f);

        state.RecordVisit(CharacterActor.From(customer), rules);
        state.RecordVisit(CharacterActor.From(customer), rules);
        RegularCustomerVisitResult result = state.RecordVisit(CharacterActor.From(customer), rules);

        bool valid = result.Success
            && result.BecameRegular
            && result.Record.IsRegular
            && result.Record.Status == RegularCustomerStatus.Regular;

        DestroyCustomer(customer);
        return valid;
    }

    private static bool VerifyRecruitCandidateThreshold()
    {
        RegularCustomerState state = new RegularCustomerState();
        RegularCustomerRules rules = CreateTestRules();
        CharacterActor customer = CreateCustomer(103, "Vampire Candidate", "Vampire", 85f);

        state.RecordVisit(CharacterActor.From(customer), rules);
        state.RecordVisit(CharacterActor.From(customer), rules);
        state.RecordVisit(CharacterActor.From(customer), rules);
        RegularCustomerVisitResult result = state.RecordVisit(CharacterActor.From(customer), rules);

        bool valid = result.Success
            && result.BecameRecruitCandidate
            && result.Record.IsRecruitCandidate
            && result.Record.Status == RegularCustomerStatus.RecruitCandidate;

        DestroyCustomer(customer);
        return valid;
    }

    private static bool VerifyRecruitmentConsumesCustomer()
    {
        RegularCustomerState state = new RegularCustomerState();
        RegularCustomerRules rules = CreateTestRules();
        CharacterActor customer = CreateCustomer(104, "Goblin Recruit", "Goblin", 90f);

        for (int i = 0; i < 4; i++)
        {
            state.RecordVisit(CharacterActor.From(customer), rules);
        }

        string customerId = RegularCustomerService.GetCustomerId(customer);
        bool recruited = state.TryRecruit(customerId, out RegularCustomerRecruitResult result);
        bool canSpawnAgain = RegularCustomerService.CanSpawnAsCustomer(customer.data, state);
        bool valid = recruited
            && result.Success
            && result.ResultType == CharacterType.NPC
            && (result.Capabilities & RecruitCapability.Staff) != 0
            && (result.Capabilities & RecruitCapability.Defense) != 0
            && (result.Capabilities & RecruitCapability.Expedition) != 0
            && state.RecruitedCharacters.Count == 1
            && state.IsRecruited(customerId)
            && canSpawnAgain;

        DestroyCustomer(customer);
        return valid;
    }

    private static bool VerifyLowSatisfactionDoesNotBecomeRegular()
    {
        RegularCustomerState state = new RegularCustomerState();
        RegularCustomerRules rules = CreateTestRules();
        CharacterActor customer = CreateCustomer(105, "Low Mood Customer", "Slime", 30f);

        state.RecordVisit(CharacterActor.From(customer), rules);
        state.RecordVisit(CharacterActor.From(customer), rules);
        RegularCustomerVisitResult result = state.RecordVisit(CharacterActor.From(customer), rules);

        bool valid = result.Success
            && !result.Record.IsRegular
            && !result.Record.IsRecruitCandidate
            && result.Record.Status == RegularCustomerStatus.Visitor;

        DestroyCustomer(customer);
        return valid;
    }

    private static bool VerifyRuntimeEvents()
    {
        GameObject runtimeObject = new GameObject("RegularCustomerRuntime_Test");
        RegularCustomerRuntime runtime = runtimeObject.AddComponent<RegularCustomerRuntime>();
        runtime.ConstructRecruitmentRuntime(
            new FakeRecruitActivationService(),
            new DungeonStory.Foundation.GameEventBus());
        CharacterActor customer = CreateCustomer(106, "Runtime Candidate", "Orc", 85f);
        BuildableObject facility = CreateFacility();

        int regularCount = 0;
        int candidateCount = 0;
        void CountRegular(RegularCustomerSnapshot snapshot) => regularCount++;
        void CountCandidate(RegularCustomerSnapshot snapshot) => candidateCount++;
        runtime.BecameRegular += CountRegular;
        runtime.CandidateDiscovered += CountCandidate;

        for (int i = 0; i < 4; i++)
        {
            runtime.OnTriggerEvent(new FacilityVisitEvent(CharacterActor.From(customer), facility));
        }

        string customerId = RegularCustomerService.GetCustomerId(customer);
        bool recruitSuccess = runtime.TryRecruit(customerId, out RegularCustomerRecruitResult recruitResult);
        bool valid = regularCount == 1
            && candidateCount == 1
            && recruitSuccess
            && recruitResult.Success
            && runtime.State.IsRecruited(customerId);

        runtime.BecameRegular -= CountRegular;
        runtime.CandidateDiscovered -= CountCandidate;
        Object.DestroyImmediate(facility.BuildingData);
        Object.DestroyImmediate(facility.gameObject);
        DestroyCustomer(customer);
        Object.DestroyImmediate(runtimeObject);
        return valid;
    }

    private static bool VerifyVisitEventSnapshotDoesNotDrift()
    {
        RegularCustomerState state = new RegularCustomerState();
        RegularCustomerRules rules = CreateTestRules();
        CharacterActor customer = CreateCustomer(107, "Snapshot Customer", "Slime", 80f);
        try
        {
            RegularCustomerVisitResult first = state.RecordVisit(CharacterActor.From(customer), rules);
            RegularCustomerVisitEventSnapshot published = new RegularCustomerVisitEventSnapshot(first);
            state.RecordVisit(CharacterActor.From(customer), rules);

            return published.customer != null
                && published.customer.visitCount == 1
                && first.Record.VisitCount == 2;
        }
        finally
        {
            DestroyCustomer(customer);
        }
    }

    private static bool VerifyDefaultRecruitmentThreshold()
    {
        RegularCustomerState state = new RegularCustomerState();
        RegularCustomerRules rules = RegularCustomerRules.CreateDefault();
        CharacterActor customer = CreateCustomer(108, "Starter Dungeon Recruit", "Slime", 65f);

        for (int i = 0; i < rules.recruitCandidateVisitThreshold; i++)
        {
            state.RecordVisit(CharacterActor.From(customer), rules);
        }

        bool valid = state.TryGetRecord(RegularCustomerService.GetCustomerId(customer), out RegularCustomerRecord record)
            && record.Status == RegularCustomerStatus.RecruitCandidate;
        DestroyCustomer(customer);
        return valid;
    }

    private static bool VerifyOffenseRewardPromotesCandidates()
    {
        GameObject runtimeObject = new GameObject("RegularCustomerRewardPromotion_Test");
        RegularCustomerRuntime runtime = runtimeObject.AddComponent<RegularCustomerRuntime>();
        runtime.ConstructRecruitmentRuntime(
            new FakeRecruitActivationService(),
            new DungeonStory.Foundation.GameEventBus());
        RegularCustomerRules rules = RegularCustomerRules.CreateDefault();
        CharacterActor first = CreateCustomer(109, "Reward Visitor A", "Slime", 92f);
        CharacterActor second = CreateCustomer(110, "Reward Visitor B", "Slime", 74f);
        CharacterActor third = CreateCustomer(111, "Reward Visitor C", "Slime", 55f);

        try
        {
            runtime.State.RecordVisit(CharacterActor.From(first), rules);
            runtime.State.RecordVisit(CharacterActor.From(second), rules);
            runtime.State.RecordVisit(CharacterActor.From(third), rules);

            int candidateCount = 0;
            void CountCandidate(RegularCustomerSnapshot snapshot) => candidateCount++;
            runtime.CandidateDiscovered += CountCandidate;
            runtime.OnTriggerEvent(new OffenseRewardGrantedEvent(
                null,
                new[]
                {
                    new OffenseRewardGrantResult(
                        OffenseRewardCategory.RecruitCandidate,
                        "Reward recruits",
                        2,
                        2,
                        true,
                        "test")
                }));

            IReadOnlyList<RegularCustomerRecord> candidates = runtime.State.Records
                .Where(record => record.IsRecruitCandidate)
                .OrderByDescending(record => record.AverageSatisfaction)
                .ToArray();
            return candidates.Count == 2
                && candidates[0].CustomerId == RegularCustomerService.GetCustomerId(first)
                && candidates[1].CustomerId == RegularCustomerService.GetCustomerId(second)
                && candidateCount == 2;
        }
        finally
        {
            DestroyCustomer(first);
            DestroyCustomer(second);
            DestroyCustomer(third);
            Object.DestroyImmediate(runtimeObject);
        }
    }

    private static RegularCustomerRules CreateTestRules()
    {
        return new RegularCustomerRules
        {
            regularVisitThreshold = 3,
            regularAverageSatisfactionThreshold = 65f,
            recruitCandidateVisitThreshold = 4,
            recruitCandidateAverageSatisfactionThreshold = 75f,
            defaultRecruitCapabilities = RecruitCapability.All
        };
    }

    private static CharacterActor CreateCustomer(int id, string name, string speciesTag, float mood)
    {
        GameObject obj = new GameObject(name);
        obj.AddComponent<SpriteRenderer>();
        CharacterActor character = obj.AddComponent<CharacterActor>();

        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.id = id;
        data.characterType = CharacterType.Customer;
        data.role = CharacterRole.Regular;
        data.characterName = name;
        data.speciesTag = speciesTag;

        CharacterAiEditorTestDependencies.Inject(obj);
        character.data = data;
        character.characterType = CharacterType.Customer;
        character.Identity.SetPersistentId($"world:test:{id:D6}");
        character.stats ??= new Dictionary<CharacterCondition, float>();
        character.stats[CharacterCondition.HUNGER] = 100f;
        character.stats[CharacterCondition.SLEEP] = 100f;
        character.stats[CharacterCondition.FUN] = 100f;
        character.stats[CharacterCondition.MOOD] = mood;
        return character;
    }

    private static BuildableObject CreateFacility()
    {
        GameObject obj = new GameObject("Regular Customer Facility");
        BuildableObject facility = obj.AddComponent<BuildableObject>();
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.id = 9106;
        data.objectName = "Regular Customer Facility";
        data.width = 1;
        data.height = 1;
        data.category = BuildingCategory.Shop;
        data.Facility = new FacilityData
        {
            roles = FacilityRole.Meal,
            capacity = 1
        };
        data.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Operate });
        facility.Initialization(data, Vector2Int.zero);
        return facility;
    }

    private static void DestroyCustomer(CharacterActor character)
    {
        if (character == null) return;

        if (character.data != null)
        {
            Object.DestroyImmediate(character.data);
        }

        Object.DestroyImmediate(character.gameObject);
    }

    private sealed class FakeRecruitActivationService : IRecruitedCharacterActivationService
    {
        public bool TryActivate(
            RegularCustomerRecord record,
            out CharacterActor actor,
            out string message)
        {
            actor = record?.ActiveActor;
            if (actor != null)
            {
                actor.characterType = CharacterType.NPC;
                actor.SetLifecycleState(CharacterLifecycleState.Active);
                if (actor.GetComponent<AbilityWork>() == null)
                {
                    actor.gameObject.AddComponent<AbilityWork>();
                }

                actor.RefreshAbilityCache();
            }

            message = "activated";
            return actor != null;
        }
    }
}
