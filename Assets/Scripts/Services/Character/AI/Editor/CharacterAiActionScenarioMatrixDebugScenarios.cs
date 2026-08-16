using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CharacterAiActionScenarioMatrixDebugScenarios
{
    private const string ReportPath =
        "Artifacts/QA/character-ai-action-scenario-matrix.txt";

    private static readonly ActionRow[] AuthoredActions =
    {
        new("Drink", typeof(AIDrink), "AbilityUseSubstance", "inventory", "none", "item lease", "SelfCare: drink"),
        new("Eat", typeof(AIEat), "AbilityShopping", "meal facility", "facility path", "visit + item lease", "DailyRoutine; shared facility faults only"),
        new("ExitDungeon", typeof(AIExitDungeon), "AbilityMove", "dungeon exit", "system path", "none", "SaveLoad movement only"),
        new("Haul", typeof(AIHaul), "AbilityHaul", "world stack + destination", "two-leg path", "quantity lease", "PhysicalItemLogistics"),
        new("Hunt", typeof(AIHunt), "AbilityHunt", "wildlife actor", "target path", "hunt ownership", "WildlifeAiHunt"),
        new("Hygiene", typeof(AIFacilityRoleAction), "AbilityShopping", "hygiene facility", "facility path", "visit", "DailyRoutine; shared facility faults only"),
        new("LookAround", typeof(AILookAround), "AbilityMove", "wander cell", "idle path", "none", "Priority/Stress; no injected path fault"),
        new("PrimitiveBucketWash", typeof(AIPrimitiveBucketWash), "CharacterPrimitiveSurvivalRunner", "physical water stack", "primitive movement", "late quantity lease", "FaultRecovery primitive path/target invalidation"),
        new("PrimitiveFieldMeal", typeof(AIPrimitiveFieldMeal), "CharacterPrimitiveSurvivalRunner", "physical meal stack", "primitive movement", "physical commit", "FaultRecovery primitive commit loss"),
        new("PrimitiveFloorRest", typeof(AIPrimitiveFloorRest), "CharacterPrimitiveSurvivalRunner", "current floor cell", "none", "external intent", "FaultRecovery primitive interruption terminal"),
        new("PrimitiveLatrine", typeof(AIPrimitiveLatrine), "CharacterPrimitiveSurvivalRunner", "designated primitive cell", "primitive movement", "external intent", "FaultRecovery primitive path/target invalidation"),
        new("Recreation", typeof(AIFacilityRoleAction), "AbilityShopping", "entertainment facility", "facility path", "visit", "DailyRoutine; shared facility faults only"),
        new("Rescue", typeof(AIRescue), "AbilityRescue", "downed actor + bed", "multi-leg path", "patient/bed/item", "AutonomousMedical"),
        new("Rest", typeof(AIRest), "AbilityShopping", "rest facility", "facility path", "visit", "FaultRecovery 61/61 exact"),
        new("Shopping", typeof(AIShopping), "AbilityShopping", "shop facility", "facility path", "visit + stock", "Customer scenarios; shared facility faults only"),
        new("SubstanceUse", typeof(AISubstanceUse), "AbilityUseSubstance", "inventory", "none", "item lease", "SelfCare: substance"),
        new("Toilet", typeof(AIFacilityRoleAction), "AbilityShopping", "toilet facility", "facility path", "visit", "DailyRoutine; shared facility faults only"),
        new("Wait", typeof(AIWait), "IdleBehaviorRunner/AbilityMove", "idle cell", "idle path", "none", "Priority/Stress; no injected path fault"),
        new("Work", typeof(AIWork), "AbilityWork", "work target/facility", "work-defined", "work/facility/item", "Alarm + Lifecycle; work-type fault gaps")
    };

    private static readonly Type[] RuntimeOnlyActions =
    {
        typeof(AIDesperateRelief),
        typeof(AIDesperateDrink),
        typeof(AIDesperateEat),
        typeof(AICollapse),
        typeof(AIViolentBreakdown)
    };

    private static readonly string[] BehaviorTreeTaskTypes =
    {
        "HasCriticalState", "RunCriticalState",
        "HasMacroGoal", "RunMacroGoalDecision",
        "HasDeprivationBreakdown", "RunDeprivationBreakdown",
        "HasLockedAction", "RunLockedAction",
        "CanInterruptCurrentAction", "ShouldStopCurrentAction",
        "StopCurrentActionForReplan", "ContinueCurrentAction",
        "RunEmergencyDecision", "RunRoutineUtilityDecision",
        "SurvivalNeedsRoutineBranch", "DutyWorkRoutineBranch",
        "LeisureVisitRoutineBranch", "IdleRoutineBranch",
        "ExitDungeonJobGiverBranch", "GetFoodJobGiverBranch",
        "DrinkJobGiverBranch", "RestJobGiverBranch",
        "ToiletJobGiverBranch", "HygieneJobGiverBranch",
        "WorkJobGiverBranch", "ShoppingJobGiverBranch",
        "LookAroundJobGiverBranch", "WaitJobGiverBranch",
        "AmbientIdleJobGiverBranch", "RunSelectedCharacterAction",
        "RunIdleBehavior"
    };

    [MenuItem("Tools/DungeonStory/Debug/AI/Run action scenario matrix audit")]
    public static void RunFromMenu()
    {
        string result = RunAll();
        Debug.Log(result);
    }

    public static string RunAll()
    {
        List<string> failures = new();
        StringBuilder report = new();
        report.AppendLine("# Character AI action / target / path / reservation matrix");
        report.AppendLine("authority=authored action assets + concrete runtime action types + BehaviorDesigner task types");
        report.AppendLine();

        AuditAuthoredActions(report, failures);
        AuditRuntimeActions(report, failures);
        AuditBehaviorTreeTasks(report, failures);
        AuditCooldownAndOutcomeContracts(report, failures);
        AppendKnownCoverageGaps(report);

        report.AppendLine();
        report.AppendLine($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}");
        foreach (string failure in failures)
        {
            report.AppendLine("FAIL " + failure);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Artifacts/QA");
        File.WriteAllText(ReportPath, report.ToString(), new UTF8Encoding(false));
        AssetDatabase.Refresh();
        return $"CHARACTER_AI_ACTION_SCENARIO_MATRIX={(failures.Count == 0 ? "PASS" : "FAIL")}; "
            + $"failures={failures.Count}; report={ReportPath}";
    }

    private static void AuditAuthoredActions(
        StringBuilder report,
        ICollection<string> failures)
    {
        report.AppendLine("## Authored action assets (19)");
        report.AppendLine("asset\ttype\tbranch\texecutor\ttarget\tpath\treservation\tcoverage");
        foreach (ActionRow row in AuthoredActions)
        {
            string assetPath = $"Assets/Resources/SO/AI/Action/{row.AssetName}.asset";
            AIActionSet asset = AssetDatabase.LoadAssetAtPath<AIActionSet>(assetPath);
            if (asset == null)
            {
                failures.Add($"missing action asset: {assetPath}");
                continue;
            }

            if (asset.GetType() != row.ActionType)
            {
                failures.Add($"{row.AssetName} type={asset.GetType().Name}, expected={row.ActionType.Name}");
            }

            MethodInfo execute = row.ActionType.GetMethod(
                nameof(AIActionSet.Execute),
                BindingFlags.Instance | BindingFlags.Public);
            if (execute == null || execute.DeclaringType == typeof(AIActionSet))
            {
                failures.Add($"{row.ActionType.Name} has no concrete Execute owner");
            }

            report.Append(row.AssetName).Append('\t')
                .Append(asset.GetType().Name).Append('\t')
                .Append(asset.Branch).Append('\t')
                .Append(row.Executor).Append('\t')
                .Append(row.Target).Append('\t')
                .Append(row.Path).Append('\t')
                .Append(row.Reservation).Append('\t')
                .AppendLine(row.Coverage);
        }
    }

    private static void AuditRuntimeActions(
        StringBuilder report,
        ICollection<string> failures)
    {
        Type[] concrete = typeof(AIActionSet).Assembly.GetTypes()
            .Where(type => typeof(AIActionSet).IsAssignableFrom(type)
                && !type.IsAbstract)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .ToArray();
        report.AppendLine();
        report.AppendLine($"## Concrete runtime action types ({concrete.Length})");
        foreach (Type type in concrete)
        {
            MethodInfo execute = type.GetMethod(
                nameof(AIActionSet.Execute),
                BindingFlags.Instance | BindingFlags.Public);
            bool ownsExecutor = execute != null
                && execute.DeclaringType != typeof(AIActionSet);
            report.AppendLine($"{type.Name}\texecutorOwner={execute?.DeclaringType?.Name ?? "missing"}");
            if (!ownsExecutor)
            {
                failures.Add($"runtime action {type.Name} inherits empty AIActionSet.Execute");
            }
        }

        foreach (Type type in RuntimeOnlyActions)
        {
            if (!concrete.Contains(type))
            {
                failures.Add($"runtime deprivation action missing: {type.Name}");
            }
        }
    }

    private static void AuditBehaviorTreeTasks(
        StringBuilder report,
        ICollection<string> failures)
    {
        report.AppendLine();
        report.AppendLine($"## BehaviorTree task branches ({BehaviorTreeTaskTypes.Length})");
        Assembly assembly = typeof(AIBrain).Assembly;
        foreach (string shortName in BehaviorTreeTaskTypes)
        {
            string fullName = "BehaviorDesigner.Runtime.Tasks.DungeonStory." + shortName;
            bool exists = assembly.GetType(fullName, throwOnError: false) != null;
            report.AppendLine($"{shortName}\t{(exists ? "present" : "missing")}");
            if (!exists)
            {
                failures.Add("BehaviorTree task missing: " + fullName);
            }
        }
    }

    private static void AuditCooldownAndOutcomeContracts(
        StringBuilder report,
        ICollection<string> failures)
    {
        report.AppendLine();
        report.AppendLine("## Narrow deterministic contracts");
        Type evaluatorType = typeof(AIBrain).Assembly.GetType(
            "AIBrainActionEvaluator",
            throwOnError: true);
        object evaluator = Activator.CreateInstance(
            evaluatorType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object[]
            {
                CharacterAiEditorTestDependencies.GameClock,
                CharacterAiEditorTestDependencies.TestPerformanceRecorder
            },
            culture: null);

        MatrixDestinationAction actionA = ScriptableObject.CreateInstance<MatrixDestinationAction>();
        MatrixDestinationAction actionB = ScriptableObject.CreateInstance<MatrixDestinationAction>();
        GameObject firstObject = new("AI matrix first destination");
        GameObject secondObject = new("AI matrix second destination");
        BuildableObject first = firstObject.AddComponent<BuildableObject>();
        BuildableObject second = secondObject.AddComponent<BuildableObject>();
        try
        {
            actionA.Candidates = new[] { first, second };
            Invoke(evaluatorType, evaluator, "StartCooldown", actionA, 30f);
            Invoke(evaluatorType, evaluator, "StartDestinationCooldown", actionA, first, 30f);
            Invoke(evaluatorType, evaluator, "StartDestinationCooldown", actionA, second, 30f);
            Invoke(evaluatorType, evaluator, "StartCooldown", actionB, 30f);
            Invoke(evaluatorType, evaluator, "ClearCooldown", actionA, first);

            bool actionACooling = InvokeBool(evaluatorType, evaluator, "IsCoolingDown", actionA);
            bool actionBCooling = InvokeBool(evaluatorType, evaluator, "IsCoolingDown", actionB);
            bool firstCooling = InvokeBool(
                evaluatorType, evaluator, "IsDestinationCoolingDown", actionA, first);
            bool secondCooling = InvokeBool(
                evaluatorType, evaluator, "IsDestinationCoolingDown", actionA, second);
            Check(report, failures, "SCOPED_CLEAR_CURRENT_ONLY",
                !actionACooling && actionBCooling && !firstCooling && secondCooling,
                $"actionA={actionACooling}; actionB={actionBCooling}; first={firstCooling}; second={secondCooling}");

            Invoke(evaluatorType, evaluator, "ClearCooldowns");
            Invoke(evaluatorType, evaluator, "StartDestinationCooldown", actionA, first, 30f);
            AIAction candidate = new(actionA, AIActionPlan.None);
            MethodInfo tryEvaluate = evaluatorType.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(method => method.Name == "TryEvaluate"
                    && method.GetParameters().Length == 3);
            object[] arguments = { null, candidate, null };
            bool selected = (bool)tryEvaluate.Invoke(evaluator, arguments);
            object evaluation = arguments[2];
            BuildableObject selectedDestination = evaluation?.GetType()
                .GetProperty("Destination", BindingFlags.Instance | BindingFlags.Public)
                ?.GetValue(evaluation) as BuildableObject;
            Check(report, failures, "COOLED_DESTINATION_ALTERNATE",
                selected && ReferenceEquals(selectedDestination, second),
                $"selected={selected}; destination={selectedDestination?.name ?? "none"}");

            Check(report, failures, "DEFERRED_IS_NON_TERMINAL_SIGNAL",
                AIActionFailure.Create(AIActionFailureKind.PathSearchDeferred).IsDeferred,
                AIActionFailureKind.PathSearchDeferred.ToString());
            Check(report, failures, "STARVATION_GATE_IS_TERMINAL_TYPED_FAILURE",
                !AIActionFailure.Create(AIActionFailureKind.PathSearchStarved).IsDeferred,
                AIActionFailureKind.PathSearchStarved.ToString());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(actionA);
            UnityEngine.Object.DestroyImmediate(actionB);
            UnityEngine.Object.DestroyImmediate(firstObject);
            UnityEngine.Object.DestroyImmediate(secondObject);
        }
    }

    private static object Invoke(
        Type type,
        object instance,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == methodName
                && candidate.GetParameters().Length == arguments.Length);
        return method.Invoke(instance, arguments);
    }

    private static bool InvokeBool(
        Type type,
        object instance,
        string methodName,
        params object[] arguments) =>
        (bool)Invoke(type, instance, methodName, arguments);

    private static void Check(
        StringBuilder report,
        ICollection<string> failures,
        string id,
        bool passed,
        string detail)
    {
        report.AppendLine($"{(passed ? "PASS" : "FAIL")}\t{id}\t{detail}");
        if (!passed)
        {
            failures.Add(id + ": " + detail);
        }
    }

    private static void AppendKnownCoverageGaps(StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("## Remaining injected-fault coverage gaps (non-failing audit rows)");
        report.AppendLine("COVERED Eat/Toilet/Hygiene/Recreation/Shopping: action-specific approach/queue/interaction destruction matrix in CharacterAiFaultRecoveryPlayModeVerifier.RequestRun().");
        report.AppendLine("GAP Work: each WorkType target invalidation, recipe/facility/item reservation failure, and checkpoint cancellation.");
        report.AppendLine("GAP Haul: source despawn/quantity shrink/destination destruction at both path legs under the live Brain/BT pipeline.");
        report.AppendLine("GAP Rescue: patient death/despawn, bed destruction, medicine lease loss, and save/load at each leg.");
        report.AppendLine("GAP Hunt: prey despawn/path invalidation and hunter down/death during chase/attack/recovery.");
        report.AppendLine("GAP Drink/SubstanceUse/PrimitiveFieldMeal: reserved item spoil/despawn/quantity loss immediately before commit.");
        report.AppendLine("COVERED LookAround/Wait/ExitDungeon: bounded deferred recovery and PathSearchStarved terminal matrix in CharacterAiFaultRecoveryPlayModeVerifier.RequestRun().");
        report.AppendLine("COVERED Deprivation breakdowns: five runtime-only branches prove live start, external lease release, and exactly-one terminal in CharacterAiFaultRecoveryPlayModeVerifier.RequestRun().");
    }

    private readonly struct ActionRow
    {
        public ActionRow(
            string assetName,
            Type actionType,
            string executor,
            string target,
            string path,
            string reservation,
            string coverage)
        {
            AssetName = assetName;
            ActionType = actionType;
            Executor = executor;
            Target = target;
            Path = path;
            Reservation = reservation;
            Coverage = coverage;
        }

        public string AssetName { get; }
        public Type ActionType { get; }
        public string Executor { get; }
        public string Target { get; }
        public string Path { get; }
        public string Reservation { get; }
        public string Coverage { get; }
    }

    private sealed class MatrixDestinationAction : AIActionSet
    {
        private static readonly CharacterAiActionDescriptor MatrixDescriptor =
            new(CharacterAiBranch.Wait, "Matrix destination action");

        public IReadOnlyList<BuildableObject> Candidates { get; set; } =
            Array.Empty<BuildableObject>();

        public override CharacterAiActionDescriptor Descriptor => MatrixDescriptor;

        public override IReadOnlyList<BuildableObject> GetDestinationCandidates(
            CharacterActor actor,
            GridPathSearchResult searchResult) => Candidates;

        public override void Execute(CharacterActor actor)
        {
        }
    }
}
