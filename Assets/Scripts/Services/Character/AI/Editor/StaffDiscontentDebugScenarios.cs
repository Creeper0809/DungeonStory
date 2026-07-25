using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StaffDiscontentDebugScenarios
{
    private static readonly HashSet<string> ScenarioStaffNames = new HashSet<string>
    {
        "Low Satisfaction Staff",
        "Efficiency Drop Staff",
        "Work Disruption Staff",
        "Moderate Low Mood Staff",
        "Departing Staff",
        "Rebel Staff",
        "Escalating Rebel Staff"
    };

    [MenuItem("DungeonStory/Debug/Character/Run P2 Staff Discontent Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P2 staff discontent scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("Moderate low mood does not permanently remove staff", VerifyModerateLowMoodDoesNotDepart, errors);

        RunScenario("만족도 낮음 상태", VerifyLowSatisfactionStage, errors);
        RunScenario("1단계 효율 저하", VerifyEfficiencyDropMultiplier, errors);
        RunScenario("2단계 태업 작업 차단", VerifyWorkDisruptionBlocksWork, errors);
        RunScenario("3단계 이탈 영구 손실", VerifyDeparturePermanentLoss, errors);
        RunScenario("4단계 국지 반란", VerifyLocalRebellionPermanentLoss, errors);
        RunScenario("반란 장기 방치 사장 위협", VerifyOwnerThreatEscalation, errors);

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
            Debug.Log("P2 staff discontent scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        bool passed = false;
        try
        {
            passed = scenario();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            CleanupScenarioArtifacts();
        }

        if (!passed)
        {
            errors.Add(name);
        }
    }

    private static void CleanupScenarioArtifacts()
    {
        CharacterActor[] actors = Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (CharacterActor actor in actors)
        {
            if (actor == null || !ScenarioStaffNames.Contains(actor.name))
            {
                continue;
            }

            if (actor.data != null && !AssetDatabase.Contains(actor.data))
            {
                Object.DestroyImmediate(actor.data);
            }

            Object.DestroyImmediate(actor.gameObject);
        }

        StaffDiscontentRuntime[] runtimes = Object.FindObjectsByType<StaffDiscontentRuntime>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (StaffDiscontentRuntime runtime in runtimes)
        {
            if (runtime != null && runtime.name == "StaffDiscontentRuntime_Test")
            {
                Object.DestroyImmediate(runtime.gameObject);
            }
        }
    }

    private static bool VerifyLowSatisfactionStage()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(201, "Low Satisfaction Staff", 45f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.Warning
            && record.Stage == StaffDiscontentStage.LowSatisfaction
            && !record.IsPermanentLoss;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyEfficiencyDropMultiplier()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(202, "Efficiency Drop Staff", 34f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        float multiplier = runtime.Runtime.GetWorkEfficiencyMultiplier(CharacterActor.From(staff));
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.EfficiencyPenalty
            && record.Stage == StaffDiscontentStage.EfficiencyDrop
            && multiplier < 1f
            && multiplier > 0f;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyModerateLowMoodDoesNotDepart()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(206, "Moderate Low Mood Staff", 45f);
        StaffDiscontentRecord record = null;
        StaffDiscontentOutcome outcome = StaffDiscontentOutcome.None;
        for (int i = 0; i < 5; i++)
        {
            record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out outcome);
        }

        bool valid = record != null
            && record.Stage != StaffDiscontentStage.Departure
            && outcome != StaffDiscontentOutcome.PermanentDeparture
            && !record.IsPermanentLoss
            && staff.CurrentLifecycleState != CharacterLifecycleState.Despawned;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyWorkDisruptionBlocksWork()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(203, "Work Disruption Staff", 20f);
        AbilityWork work = staff.GetAbility<AbilityWork>();

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        bool canWork = work.CanStartWorkAction();
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.WorkDisruption
            && record.Stage == StaffDiscontentStage.WorkDisruption
            && !canWork
            && work.IsOffDuty;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyDeparturePermanentLoss()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(204, "Departing Staff", 10f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.PermanentDeparture
            && record.Stage == StaffDiscontentStage.Departure
            && record.IsPermanentLoss
            && record.IsDeparted
            && staff.CurrentLifecycleState == CharacterLifecycleState.Despawned;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyLocalRebellionPermanentLoss()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(205, "Rebel Staff", 5f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.LocalRebellion
            && record.Stage == StaffDiscontentStage.LocalRebellion
            && record.IsPermanentLoss
            && record.IsInLocalRebellion
            && !record.IsDeparted;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyOwnerThreatEscalation()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(206, "Escalating Rebel Staff", 5f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome firstOutcome);
        record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome secondOutcome);
        bool valid = firstOutcome == StaffDiscontentOutcome.LocalRebellion
            && secondOutcome == StaffDiscontentOutcome.OwnerThreat
            && record != null
            && record.IsOwnerThreat;

        DestroyStaff(staff);
        return valid;
    }

    private static CharacterActor CreateStaff(int id, string name, float mood)
    {
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.id = id;
        data.characterType = CharacterType.NPC;
        data.role = CharacterRole.Regular;
        data.characterName = name;
        data.speciesTag = "Orc";
        data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

        GameObject obj = new GameObject(name);
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityShopping>();
        obj.AddComponent<AbilityWork>();
        AIBrain brain = obj.AddComponent<AIBrain>();
        brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
        CharacterAiEditorTestDependencies.Inject(obj);
        CharacterActor character = obj.GetComponent<CharacterActor>();
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(character, null);
        character.RefreshAbilityCache();
        character.Initialization(data);
        character.Identity.SetPersistentId($"staff-discontent-test:{id}");
        character.SetLifecycleState(CharacterLifecycleState.Active);
        character.stats[CharacterCondition.SLEEP] = 50f;
        character.stats[CharacterCondition.HUNGER] = 50f;
        character.stats[CharacterCondition.FUN] = 50f;
        character.stats[CharacterCondition.EXCRETION] = 50f;
        character.stats[CharacterCondition.HYGIENE] = 50f;
        character.stats[CharacterCondition.MOOD] = mood;
        return character;
    }

    private static void DestroyStaff(CharacterActor staff)
    {
        if (staff == null) return;

        if (staff.data != null)
        {
            Object.DestroyImmediate(staff.data);
        }

        Object.DestroyImmediate(staff.gameObject);
    }

    private sealed class ScenarioRuntime : IDisposable
    {
        private readonly GameObject runtimeObject;

        public ScenarioRuntime()
        {
            runtimeObject = new GameObject("StaffDiscontentRuntime_Test");
            Runtime = runtimeObject.AddComponent<StaffDiscontentRuntime>();
            CharacterAiEditorTestDependencies.Inject(Runtime, System.Array.Empty<GameObject>());
        }

        public StaffDiscontentRuntime Runtime { get; }

        public void Dispose()
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }
}
