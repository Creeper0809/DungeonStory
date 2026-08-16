#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using Task = BehaviorDesigner.Runtime.Tasks.Task;

internal static class CharacterAiCoverageSourceInventory
{
    private const string ProductionExternalBehaviorPath =
        "Assets/Behavior Designer/External Behaviors/CharacterAIExternalBehavior.asset";

    private static readonly string[] CoverageCriticalSourceRoots =
    {
        "Assets/Scripts/Services/Character/AI",
        "Assets/Scripts/Services/Infrastructure/AI",
        "Assets/Scripts/Services/Character/Work",
        "Assets/Scripts/Models/AI",
        "Assets/Scripts/Models/Work"
    };

    private static readonly Dictionary<string, string[]>
        EvidenceTransitiveSourcePathsByVerifier =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                {
                    "OffenseStrategicPlayModeVerifier",
                    new[]
                    {
                        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandBattleDirector.cs",
                        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandResolutionAdapter.cs",
                        "Assets/Scripts/Services/Offense/OffenseBattleRuntime.cs",
                        "Assets/Scripts/Services/Offense/OffenseBattleModel.cs"
                    }
                },
                {
                    "OffenseJourneyPlayModeFacade",
                    new[]
                    {
                        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandBattleDirector.cs",
                        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandResolutionAdapter.cs",
                        "Assets/Scripts/Services/Offense/OffenseBattleRuntime.cs",
                        "Assets/Scripts/Services/Offense/OffenseBattleModel.cs",
                        "Assets/Scripts/Services/Offense/OffensePreparationService.cs",
                        "Assets/Scripts/Services/Economy/ProductionItemGateway.cs",
                        "Assets/Scripts/Services/Items/FacilityBufferDestinationClaimRegistry.cs",
                        "Assets/Scripts/Services/Items/WorldItemHaulDestinationAuthority.cs",
                        "Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs",
                        "Assets/Scripts/Services/Items/ItemTransferService.cs",
                        "Assets/Scripts/Services/Items/AbilityHaul.cs"
                    }
                },
                {
                    "OffenseTacticalJourneyPlayModeVerifier",
                    new[]
                    {
                        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandBattleDirector.cs",
                        "Assets/Scripts/Services/Offense/Strategic/OffenseCommandResolutionAdapter.cs",
                        "Assets/Scripts/Services/Offense/OffenseBattleRuntime.cs",
                        "Assets/Scripts/Services/Offense/OffenseBattleModel.cs"
                    }
                },
                {
                    "DungeonAiActionSaveLoadPlayModeVerifier",
                    new[]
                    {
                        "Assets/Scripts/Services/Infrastructure/DungeonGameSaveService.cs",
                        "Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs",
                        "Assets/Scripts/Services/Infrastructure/Save/HaulDeliveryIntentRestoreCoordinator.cs",
                        "Assets/Scripts/Services/Items/FacilityBufferDestinationClaimRegistry.cs",
                        "Assets/Scripts/Services/Items/WorldItemHaulDestinationAuthority.cs",
                        "Assets/Scripts/Services/Items/HaulDeliveryIntentRuntime.cs",
                        "Assets/Scripts/Services/Items/ItemQuantityReservationService.cs",
                        "Assets/Scripts/Services/Items/WorldItemStackRuntime.cs",
                        "Assets/Scripts/Services/Items/AbilityHaul.cs"
                    }
                },
                {
                    "PhysicalItemLogisticsPlayModeVerifier",
                    new[]
                    {
                        "Assets/Scripts/Services/Items/FacilityBufferDestinationClaimRegistry.cs",
                        "Assets/Scripts/Services/Items/WorldItemHaulDestinationAuthority.cs",
                        "Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs",
                        "Assets/Scripts/Services/Items/ItemTransferService.cs",
                        "Assets/Scripts/Services/Items/AbilityHaul.cs",
                        "Assets/Scripts/Services/Infrastructure/Save/HaulDeliveryIntentRestoreCoordinator.cs",
                        "Assets/Scripts/Services/Economy/ProductionItemGateway.cs",
                        "Assets/Scripts/Services/Offense/OffensePreparationService.cs",
                        "Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntimeServices.cs",
                        "Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs"
                    }
                },
                {
                    "SurgeryPlayModeVerifier",
                    new[]
                    {
                        "Assets/Scripts/Services/Medical/SurgeryRuntimeServices.cs",
                        "Assets/Scripts/Services/Medical/SurgeryRuntime.cs",
                        "Assets/Scripts/Services/Medical/SurgeryRestoreCoordinator.cs",
                        "Assets/Scripts/Services/Medical/SurgeryLogisticsRuntime.cs",
                        "Assets/Scripts/Models/Medical/Core/SurgerySaveValidation.cs",
                        "Assets/Scripts/Services/Items/FacilityBufferDestinationClaimRegistry.cs",
                        "Assets/Scripts/Services/Items/WorldItemHaulDestinationAuthority.cs",
                        "Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs",
                        "Assets/Scripts/Services/Items/ItemTransferService.cs",
                        "Assets/Scripts/Services/Items/AbilityHaul.cs"
                    }
                },
                {
                    "CaptivityWildlifeLifecyclePlayModeVerifier",
                    new[]
                    {
                        "Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs",
                        "Assets/Scripts/Services/Captivity/CaptivityAbilityUnityAdapters.cs",
                        "Assets/Scripts/Services/Infrastructure/Core/Captivity/AbilityWildlifeCaptureTransportAdapter.cs",
                        "Assets/Scripts/Services/Wildlife/WildlifeActor.cs",
                        "Assets/Scripts/Services/Wildlife/WildlifeRuntime.cs",
                        "Assets/Scripts/Services/Character/Core/CharacterActor.cs",
                        "Assets/Scripts/Services/Character/Core/CharacterLifecycle.cs",
                        "Assets/Scripts/Services/Character/AI/CharacterAlarmResponseRuntime.cs",
                        "Assets/Scripts/Services/Survival/CharacterDeprivationRuntime.cs"
                    }
                },
                {
                    "FirstRunObjectivePlayModeVerifier",
                    new[]
                    {
                        "Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs",
                        "Assets/Scripts/Services/Infrastructure/BlueprintResearchSaveSection.cs",
                        "Assets/Scripts/Services/Infrastructure/ResearchBlueprintArchiveAdapter.cs",
                        "Assets/Scripts/Services/Economy/ProductionItemGateway.cs",
                        "Assets/Scripts/Services/Items/FacilityBufferDestinationClaimRegistry.cs",
                        "Assets/Scripts/Services/Items/WorldItemHaulDestinationAuthority.cs",
                        "Assets/Scripts/Services/Items/WorldItemHaulPlanningService.cs",
                        "Assets/Scripts/Services/Items/ItemTransferService.cs",
                        "Assets/Scripts/Services/Items/AbilityHaul.cs"
                    }
                }
            };

    internal static readonly string[] ExpectedAuthoredActionAssets =
    {
        "Drink", "Eat", "ExitDungeon", "Haul", "Hunt", "Hygiene",
        "LookAround", "PrimitiveBucketWash", "PrimitiveFieldMeal",
        "PrimitiveFloorRest", "PrimitiveLatrine", "Recreation", "Rescue",
        "Rest", "Shopping", "SubstanceUse", "Toilet", "Wait", "Work"
    };

    internal static readonly string[] ExpectedConcreteActionTypes =
    {
        "AICollapse", "AIDesperateDrink", "AIDesperateEat",
        "AIDesperateRelief", "AIDrink", "AIEat", "AIExitDungeon",
        "AIFacilityRoleAction", "AIHaul", "AIHunt", "AILookAround",
        "AIPrimitiveBucketWash", "AIPrimitiveFieldMeal",
        "AIPrimitiveFloorRest", "AIPrimitiveLatrine", "AIRescue", "AIRest",
        "AIShopping", "AISubstanceUse", "AIViolentBreakdown", "AIWait",
        "AIWork"
    };

    internal static readonly string[] ExpectedDeprivationActionTypes =
    {
        "AICollapse", "AIDesperateDrink", "AIDesperateEat",
        "AIDesperateRelief", "AIViolentBreakdown"
    };

    internal static readonly string[] ExpectedBranches =
    {
        "None", "Critical", "DeprivationBreakdown", "LockedAction",
        "SoftLock", "InterruptCheck", "MacroGoal", "Emergency",
        "RoutineUtility", "ContinueCurrent", "StopCurrent", "SurvivalNeeds",
        "DutyWork", "LeisureVisit", "ExitDungeon", "Eat", "Rest", "Work",
        "Shopping", "LookAround", "Wait", "Idle", "Toilet", "Hygiene",
        "Drink"
    };

    internal static readonly string[] ExpectedBehaviorOperations =
    {
        "None", "RunDeprivationBreakdown", "RunLockedAction",
        "RunEmergencyDecision", "RunRoutineUtilityDecision", "ClearMacroGoal",
        "RunMacroGoalDecision", "RunCriticalState", "ContinueCurrentAction",
        "StopCurrentActionForReplan", "SelectJobGiverAction",
        "RunSelectedAction", "RunIdleBehavior"
    };

    internal static readonly string[] ExpectedBehaviorTaskTypes =
    {
        "AmbientIdleJobGiverBranch", "CanInterruptCurrentAction",
        "ClearMacroGoal", "ContinueCurrentAction", "DrinkJobGiverBranch",
        "DutyWorkRoutineBranch", "EmitContextBubble", "ExitDungeonJobGiverBranch",
        "GetFoodJobGiverBranch", "HasContinuableCurrentAction", "HasCriticalState",
        "HasDeprivationBreakdown", "HasLockedAction", "HasMacroGoal",
        "HasMacroGoalType", "HygieneJobGiverBranch", "IdleRoutineBranch",
        "LeisureVisitRoutineBranch", "LookAroundJobGiverBranch",
        "RecordBtDecisionTrace", "RestJobGiverBranch", "RunAvoidFacilityMacroGoal",
        "RunComplainMacroGoal", "RunCriticalState", "RunDeprivationBreakdown",
        "RunEmergencyDecision", "RunExitDungeonMacroGoal", "RunIdleBehavior",
        "RunLockedAction", "RunMacroGoalDecision", "RunRoutineUtilityDecision",
        "RunSelectedCharacterAction", "RunVandalizeMacroGoal",
        "SelectDrinkAction", "SelectEatAction", "SelectExitDungeonAction",
        "SelectHygieneAction", "SelectLookAroundAction", "SelectRestAction",
        "SelectShoppingAction", "SelectToiletAction", "SelectWaitAction",
        "SelectWorkAction", "ShoppingJobGiverBranch", "ShouldStopCurrentAction",
        "StopCurrentActionForReplan", "SurvivalNeedsRoutineBranch",
        "ToiletJobGiverBranch", "WaitJobGiverBranch", "WorkJobGiverBranch"
    };

    // These are the only DungeonStory task adapters connected beneath the
    // serialized production ExternalBehaviorTree root. Routine selection is
    // intentionally centralized in RunRoutineUtilityDecision; the older
    // per-routine and per-JobGiver visual helpers remain source-compatible but
    // are not live execution evidence.
    internal static readonly string[] ExpectedLiveAttachedBehaviorTaskTypes =
    {
        "CanInterruptCurrentAction", "ClearMacroGoal", "HasCriticalState",
        "HasDeprivationBreakdown", "HasLockedAction", "HasMacroGoalType",
        "RecordBtDecisionTrace", "RunAvoidFacilityMacroGoal",
        "RunComplainMacroGoal", "RunCriticalState", "RunDeprivationBreakdown",
        "RunEmergencyDecision", "RunExitDungeonMacroGoal", "RunIdleBehavior",
        "RunLockedAction", "RunMacroGoalDecision", "RunRoutineUtilityDecision",
        "RunVandalizeMacroGoal", "StopCurrentActionForReplan"
    };

    internal static readonly string[] ExpectedJobGiverTypes =
    {
        "DrinkJobGiver", "ExitDungeonJobGiver", "GetFoodJobGiver",
        "HygieneJobGiver", "LookAroundJobGiver", "RecreationJobGiver",
        "RestJobGiver", "ShoppingJobGiver", "ToiletJobGiver", "WaitJobGiver",
        "WorkJobGiver"
    };

    private static readonly ExternalIntentCallsiteExpectation[] ExpectedExternalIntentCallsites =
    {
        new ExternalIntentCallsiteExpectation(
            "Assets/Scripts/Services/Captivity/CaptivityAbilityUnityAdapters.cs", 2,
            "captivity:escort"),
        new ExternalIntentCallsiteExpectation(
            "Assets/Scripts/Services/Survival/CharacterSafeReliefRunner.cs", 2,
            "survival:safe-relief"),
        new ExternalIntentCallsiteExpectation(
            "Assets/Scripts/Services/Survival/CharacterPrimitiveSurvivalRunner.cs", 1,
            "survival:primitive"),
        new ExternalIntentCallsiteExpectation(
            "Assets/Scripts/Services/Survival/CharacterBreakdownActionRunner.cs", 2,
            "survival:breakdown")
    };

    internal static readonly DomainSurface[] DomainRegistry =
    {
        new DomainSurface("combat:defense-autonomy", "DefenseStatusRuntime"),
        new DomainSurface("combat:commands-rescue", "CharacterCombatCommandRuntime"),
        new DomainSurface("medical:autonomous-rescue-treatment", "AbilityRescue"),
        new DomainSurface("medical:surgery", "SurgeryRuntime"),
        new DomainSurface("invasion:defense-engagement", "InvasionCampaignRuntime"),
        new DomainSurface("wildlife:hunt", "WildlifeRuntime"),
        new DomainSurface("wildlife:capture-transport", "WildlifeCaptureRuntime"),
        new DomainSurface("wildlife:animal-care", "WildlifeCaptureCareContext"),
        new DomainSurface("captivity:warden-interactions-labor", "CaptivityRuntime"),
        new DomainSurface("captivity:escort", "AbilityCaptiveEscort"),
        new DomainSurface("captivity:escape", "AbilityCaptiveEscape"),
        new DomainSurface("captivity:recapture", "CaptivityRuntime"),
        new DomainSurface("visitor:customer-lifecycle", "CustomerPersonaRuntime"),
        new DomainSurface("offense:strategic-ui", "OffenseWorldMapRuntime"),
        new DomainSurface("offense:journey-battle-reward", "OffenseExpeditionRuntime"),
        new DomainSurface("offense:enemy-tactics", "EnemyTacticalDecisionService")
    };

    internal static SourceInventorySnapshot Capture()
    {
        string[] authoredAssets = AssetDatabase
            .FindAssets("t:AIActionSet", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        string[] concreteActionTypes = TypeCache.GetTypesDerivedFrom<AIActionSet>()
            .Where(IsProductionConcrete)
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        string[] branches = Enum.GetNames(typeof(CharacterAiBranch))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] operations = Enum.GetNames(typeof(DungeonStory.AI.CharacterAiBehaviorOperation))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        string[] taskTypes = TypeCache
            .GetTypesDerivedFrom<BehaviorDesigner.Runtime.Tasks.Task>()
            .Where(type => IsConcrete(type)
                && string.Equals(
                    type.Namespace,
                    "BehaviorDesigner.Runtime.Tasks.DungeonStory",
                    StringComparison.Ordinal))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        BehaviorTaskAttachmentInventory behaviorTaskAttachments =
            CaptureBehaviorTaskAttachments(taskTypes);

        string[] jobGiverTypes = TypeCache
            .GetTypesDerivedFrom<CharacterAiJobGiver>()
            .Where(IsProductionConcrete)
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        ExternalIntentCallsite[] externalIntentCallsites =
            CaptureExternalIntentCallsites();

        return new SourceInventorySnapshot(
            authoredAssets,
            concreteActionTypes,
            branches,
            operations,
            taskTypes,
            behaviorTaskAttachments,
            jobGiverTypes,
            externalIntentCallsites,
            DomainRegistry);
    }

    private static BehaviorTaskAttachmentInventory CaptureBehaviorTaskAttachments(
        IReadOnlyCollection<string> productionTaskTypes)
    {
        ExternalBehaviorTree externalBehavior =
            AssetDatabase.LoadAssetAtPath<ExternalBehaviorTree>(
                ProductionExternalBehaviorPath);
        if (externalBehavior == null)
        {
            return BehaviorTaskAttachmentInventory.Unresolved(
                ProductionExternalBehaviorPath,
                "production ExternalBehaviorTree asset is missing",
                productionTaskTypes);
        }

        try
        {
            // ExternalBehaviorTree keeps the authoritative topology in its
            // serialized BehaviorSource payload. Init deserializes that payload;
            // without it RootTask can remain null even though the asset contains
            // a valid live graph.
            externalBehavior.Init();
        }
        catch (Exception exception)
        {
            return BehaviorTaskAttachmentInventory.Unresolved(
                ProductionExternalBehaviorPath,
                "production ExternalBehaviorTree deserialization failed: "
                    + exception.GetType().Name + ": " + exception.Message,
                productionTaskTypes);
        }

        BehaviorSource source = externalBehavior.BehaviorSource;
        Task root = source?.RootTask;
        if (root == null)
        {
            return BehaviorTaskAttachmentInventory.Unresolved(
                ProductionExternalBehaviorPath,
                "production ExternalBehaviorTree has no deserialized root task",
                productionTaskTypes);
        }

        HashSet<string> attached = new HashSet<string>(StringComparer.Ordinal);
        CollectAttachedDungeonStoryTasks(root, attached);

        HashSet<string> detached = new HashSet<string>(StringComparer.Ordinal);
        foreach (Task detachedRoot in source.DetachedTasks
                     ?? new List<Task>())
        {
            CollectAttachedDungeonStoryTasks(detachedRoot, detached);
        }

        BehaviorTaskAttachment[] rows = ExpectedBehaviorTaskTypes
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(typeName =>
            {
                if (!productionTaskTypes.Contains(typeName))
                {
                    return new BehaviorTaskAttachment(
                        typeName,
                        BehaviorTaskAttachmentStatus.Missing,
                        "production task type is absent from the compiled source inventory");
                }

                if (attached.Contains(typeName))
                {
                    return new BehaviorTaskAttachment(
                        typeName,
                        BehaviorTaskAttachmentStatus.LiveAttached,
                        "reachable beneath serialized production ExternalBehaviorTree.RootTask");
                }

                string reason = detached.Contains(typeName)
                    ? "serialized as a detached task and unreachable from RootTask"
                    : DormantBehaviorTaskReason(typeName);
                return new BehaviorTaskAttachment(
                    typeName,
                    BehaviorTaskAttachmentStatus.DormantLegacy,
                    reason);
            })
            .ToArray();

        string[] unexpectedAttached = attached
            .Where(value => !ExpectedBehaviorTaskTypes.Contains(
                value,
                StringComparer.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return BehaviorTaskAttachmentInventory.ResolvedInventory(
            ProductionExternalBehaviorPath,
            rows,
            unexpectedAttached);
    }

    private static void CollectAttachedDungeonStoryTasks(
        Task task,
        ISet<string> attached)
    {
        if (task == null)
        {
            return;
        }

        Type type = task.GetType();
        if (string.Equals(
                type.Namespace,
                "BehaviorDesigner.Runtime.Tasks.DungeonStory",
                StringComparison.Ordinal))
        {
            attached.Add(type.Name);
        }

        if (task is not ParentTask parent || parent.Children == null)
        {
            return;
        }

        foreach (Task child in parent.Children)
        {
            CollectAttachedDungeonStoryTasks(child, attached);
        }
    }

    private static string DormantBehaviorTaskReason(string typeName)
    {
        if (typeName == "HasContinuableCurrentAction"
            || typeName == "ContinueCurrentAction")
        {
            return "legacy continuation helper; live graph uses HasLockedAction -> RunLockedAction";
        }

        if (typeName == "ShouldStopCurrentAction")
        {
            return "legacy stop guard; live graph uses CanInterruptCurrentAction -> StopCurrentActionForReplan";
        }

        if (typeName == "HasMacroGoal")
        {
            return "legacy generic macro guard; live graph uses typed HasMacroGoalType branches";
        }

        if (typeName == "EmitContextBubble")
        {
            return "legacy visual helper; context feedback is emitted by the central decision pipeline";
        }

        return "legacy decomposed routine/JobGiver helper; live graph delegates selection and execution to RunRoutineUtilityDecision";
    }

    internal static EvidenceSourceFreshness CaptureEvidenceSourceFreshness(
        string verifierTypeName)
    {
        SortedSet<string> paths = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string root in CoverageCriticalSourceRoots)
        {
            if (!Directory.Exists(root))
                continue;
            foreach (string path in Directory.GetFiles(
                         root,
                         "*.cs",
                         SearchOption.AllDirectories)
                     .Select(NormalizePath)
                     .Where(path => path.IndexOf("/Editor/", StringComparison.Ordinal) < 0))
            {
                paths.Add(path);
            }
        }

        foreach (string actionPath in AssetDatabase.FindAssets(
                     "t:AIActionSet",
                     new[] { "Assets" })
                 .Select(AssetDatabase.GUIDToAssetPath)
                 .Where(path => !string.IsNullOrWhiteSpace(path))
                 .Select(NormalizePath))
        {
            paths.Add(actionPath);
        }

        if (File.Exists(ProductionExternalBehaviorPath))
            paths.Add(NormalizePath(ProductionExternalBehaviorPath));

        string verifierPath = FindTypeSourcePath(verifierTypeName);
        if (!string.IsNullOrWhiteSpace(verifierPath))
            paths.Add(verifierPath);

        string[] transitivePaths =
            CaptureEvidenceTransitiveSourcePaths(verifierTypeName);
        string[] unresolvedTransitivePaths = transitivePaths
                .Where(path => !File.Exists(path))
                .ToArray();
        if (unresolvedTransitivePaths.Length > 0)
        {
            return EvidenceSourceFreshness.Invalid(
                verifierTypeName,
                "transitive evidence source paths could not be resolved: "
                + string.Join(",", unresolvedTransitivePaths));
        }

        foreach (string path in transitivePaths)
            paths.Add(path);

        foreach (DomainSurface domain in DomainRegistry)
        {
            string authorityPath = FindTypeSourcePath(domain.AuthorityType);
            if (!string.IsNullOrWhiteSpace(authorityPath))
                paths.Add(authorityPath);
        }

        string[] existing = paths
            .Where(File.Exists)
            .ToArray();
        if (string.IsNullOrWhiteSpace(verifierPath)
            || !File.Exists(verifierPath)
            || existing.Length == 0)
        {
            return EvidenceSourceFreshness.Invalid(
                verifierTypeName,
                string.IsNullOrWhiteSpace(verifierPath)
                    ? "verifier source could not be resolved"
                    : "coverage-critical source set is empty");
        }

        string latestPath = existing
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenBy(path => path, StringComparer.Ordinal)
            .First();
        return EvidenceSourceFreshness.Valid(
            verifierPath,
            existing.Length,
            latestPath,
            File.GetLastWriteTimeUtc(latestPath));
    }

    internal static string[] CaptureEvidenceTransitiveSourcePaths(
        string verifierTypeName)
    {
        return !string.IsNullOrWhiteSpace(verifierTypeName)
            && EvidenceTransitiveSourcePathsByVerifier.TryGetValue(
                verifierTypeName,
                out string[] paths)
            ? paths.Select(NormalizePath).ToArray()
            : Array.Empty<string>();
    }

    private static string FindTypeSourcePath(string simpleTypeName)
    {
        if (string.IsNullOrWhiteSpace(simpleTypeName))
            return string.Empty;

        Type target = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(GetLoadableTypes)
            .FirstOrDefault(type => string.Equals(
                type.Name,
                simpleTypeName,
                StringComparison.Ordinal));
        if (target == null)
            return string.Empty;

        foreach (string guid in AssetDatabase.FindAssets(
                     simpleTypeName + " t:MonoScript",
                     new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.GetClass() == target)
                return NormalizePath(path);
        }
        return string.Empty;
    }

    private static IEnumerable<Type> GetLoadableTypes(System.Reflection.Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (System.Reflection.ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type != null);
        }
    }

    internal static string[] CompareExact(
        IEnumerable<string> expected,
        IEnumerable<string> actual,
        string label)
    {
        HashSet<string> expectedSet = new HashSet<string>(
            expected ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        HashSet<string> actualSet = new HashSet<string>(
            actual ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        List<string> gaps = new List<string>();
        foreach (string missing in expectedSet.Except(actualSet).OrderBy(value => value, StringComparer.Ordinal))
            gaps.Add(label + ":missing:" + missing);
        foreach (string extra in actualSet.Except(expectedSet).OrderBy(value => value, StringComparer.Ordinal))
            gaps.Add(label + ":unexpected:" + extra);
        return gaps.ToArray();
    }

    internal static string[] CompareExternalIntentCallsites(
        IReadOnlyList<ExternalIntentCallsite> actual)
    {
        List<string> gaps = new List<string>();
        Dictionary<string, int> actualCounts = actual
            .GroupBy(row => row.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        foreach (ExternalIntentCallsiteExpectation expected in ExpectedExternalIntentCallsites)
        {
            actualCounts.TryGetValue(expected.Path, out int count);
            if (count != expected.Count)
                gaps.Add("external-intent:count:" + expected.Path + ":" + count + "/" + expected.Count);
        }

        HashSet<string> expectedPaths = new HashSet<string>(
            ExpectedExternalIntentCallsites.Select(row => row.Path),
            StringComparer.Ordinal);
        foreach (string unexpected in actualCounts.Keys
            .Where(path => !expectedPaths.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            gaps.Add("external-intent:unexpected:" + unexpected + ":" + actualCounts[unexpected]);
        }

        return gaps.ToArray();
    }

    internal static string ResolveExternalIntentOwner(string path)
    {
        ExternalIntentCallsiteExpectation match = ExpectedExternalIntentCallsites
            .FirstOrDefault(row => string.Equals(row.Path, path, StringComparison.Ordinal));
        return match.OwnerId ?? "unregistered";
    }

    private static ExternalIntentCallsite[] CaptureExternalIntentCallsites()
    {
        const string token = "TryBeginExternallyDrivenAction(";
        List<ExternalIntentCallsite> rows = new List<ExternalIntentCallsite>();
        foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
            .Select(NormalizePath)
            .Where(path => path.IndexOf("/Editor/", StringComparison.Ordinal) < 0)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                if (line.IndexOf(token, StringComparison.Ordinal) < 0
                    || line.IndexOf("public bool " + token, StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                rows.Add(new ExternalIntentCallsite(
                    path,
                    index + 1,
                    ResolveExternalIntentOwner(path)));
            }
        }

        return rows.ToArray();
    }

    private static bool IsConcrete(Type type) =>
        type != null && !type.IsAbstract && !type.ContainsGenericParameters;

    private static bool IsProductionConcrete(Type type)
    {
        if (!IsConcrete(type)) return false;
        string assemblyName = type.Assembly?.GetName().Name ?? string.Empty;
        return assemblyName.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static string NormalizePath(string path) =>
        (path ?? string.Empty).Replace('\\', '/');

    private readonly struct ExternalIntentCallsiteExpectation
    {
        public ExternalIntentCallsiteExpectation(string path, int count, string ownerId)
        {
            Path = path;
            Count = count;
            OwnerId = ownerId;
        }

        public string Path { get; }
        public int Count { get; }
        public string OwnerId { get; }
    }
}

internal sealed class SourceInventorySnapshot
{
    public SourceInventorySnapshot(
        string[] authoredActionAssetPaths,
        string[] concreteActionTypes,
        string[] branches,
        string[] behaviorOperations,
        string[] behaviorTaskTypes,
        BehaviorTaskAttachmentInventory behaviorTaskAttachments,
        string[] jobGiverTypes,
        ExternalIntentCallsite[] externalIntentCallsites,
        DomainSurface[] domains)
    {
        AuthoredActionAssetPaths = authoredActionAssetPaths ?? Array.Empty<string>();
        ConcreteActionTypes = concreteActionTypes ?? Array.Empty<string>();
        Branches = branches ?? Array.Empty<string>();
        BehaviorOperations = behaviorOperations ?? Array.Empty<string>();
        BehaviorTaskTypes = behaviorTaskTypes ?? Array.Empty<string>();
        BehaviorTaskAttachments = behaviorTaskAttachments
            ?? BehaviorTaskAttachmentInventory.Unresolved(
                string.Empty,
                "attachment inventory was not captured",
                Array.Empty<string>());
        JobGiverTypes = jobGiverTypes ?? Array.Empty<string>();
        ExternalIntentCallsites = externalIntentCallsites ?? Array.Empty<ExternalIntentCallsite>();
        Domains = domains ?? Array.Empty<DomainSurface>();
    }

    public string[] AuthoredActionAssetPaths { get; }
    public string[] ConcreteActionTypes { get; }
    public string[] Branches { get; }
    public string[] BehaviorOperations { get; }
    public string[] BehaviorTaskTypes { get; }
    public BehaviorTaskAttachmentInventory BehaviorTaskAttachments { get; }
    public string[] JobGiverTypes { get; }
    public ExternalIntentCallsite[] ExternalIntentCallsites { get; }
    public DomainSurface[] Domains { get; }
}

internal enum BehaviorTaskAttachmentStatus
{
    LiveAttached,
    DormantLegacy,
    Missing
}

internal readonly struct BehaviorTaskAttachment
{
    public BehaviorTaskAttachment(
        string typeName,
        BehaviorTaskAttachmentStatus status,
        string reason)
    {
        TypeName = typeName ?? string.Empty;
        Status = status;
        Reason = reason ?? string.Empty;
    }

    public string TypeName { get; }
    public BehaviorTaskAttachmentStatus Status { get; }
    public string Reason { get; }
}

internal sealed class BehaviorTaskAttachmentInventory
{
    private BehaviorTaskAttachmentInventory(
        bool resolved,
        string externalBehaviorPath,
        string failureReason,
        BehaviorTaskAttachment[] rows,
        string[] unexpectedAttachedTypes)
    {
        Resolved = resolved;
        ExternalBehaviorPath = externalBehaviorPath ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
        Rows = rows ?? Array.Empty<BehaviorTaskAttachment>();
        UnexpectedAttachedTypes = unexpectedAttachedTypes ?? Array.Empty<string>();
    }

    public bool Resolved { get; }
    public string ExternalBehaviorPath { get; }
    public string FailureReason { get; }
    public BehaviorTaskAttachment[] Rows { get; }
    public string[] UnexpectedAttachedTypes { get; }

    public static BehaviorTaskAttachmentInventory ResolvedInventory(
        string externalBehaviorPath,
        BehaviorTaskAttachment[] rows,
        string[] unexpectedAttachedTypes) =>
        new BehaviorTaskAttachmentInventory(
            true,
            externalBehaviorPath,
            string.Empty,
            rows,
            unexpectedAttachedTypes);

    public static BehaviorTaskAttachmentInventory Unresolved(
        string externalBehaviorPath,
        string failureReason,
        IEnumerable<string> taskTypes) =>
        new BehaviorTaskAttachmentInventory(
            false,
            externalBehaviorPath,
            failureReason,
            (taskTypes ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .Select(value => new BehaviorTaskAttachment(
                    value,
                    BehaviorTaskAttachmentStatus.Missing,
                    failureReason))
                .ToArray(),
            Array.Empty<string>());
}

internal readonly struct ExternalIntentCallsite
{
    public ExternalIntentCallsite(string path, int line, string ownerId)
    {
        Path = path ?? string.Empty;
        Line = line;
        OwnerId = ownerId ?? string.Empty;
    }

    public string Path { get; }
    public int Line { get; }
    public string OwnerId { get; }
}

internal readonly struct DomainSurface
{
    public DomainSurface(string id, string authorityType)
    {
        Id = id ?? string.Empty;
        AuthorityType = authorityType ?? string.Empty;
    }

    public string Id { get; }
    public string AuthorityType { get; }
}

internal readonly struct EvidenceSourceFreshness
{
    private EvidenceSourceFreshness(
        bool resolved,
        string verifierSourcePath,
        int sourceCount,
        string latestSourcePath,
        DateTime latestWriteUtc,
        string failureReason)
    {
        Resolved = resolved;
        VerifierSourcePath = verifierSourcePath ?? string.Empty;
        SourceCount = sourceCount;
        LatestSourcePath = latestSourcePath ?? string.Empty;
        LatestWriteUtc = latestWriteUtc;
        FailureReason = failureReason ?? string.Empty;
    }

    public bool Resolved { get; }
    public string VerifierSourcePath { get; }
    public int SourceCount { get; }
    public string LatestSourcePath { get; }
    public DateTime LatestWriteUtc { get; }
    public string FailureReason { get; }

    public static EvidenceSourceFreshness Valid(
        string verifierSourcePath,
        int sourceCount,
        string latestSourcePath,
        DateTime latestWriteUtc) =>
        new EvidenceSourceFreshness(
            true,
            verifierSourcePath,
            sourceCount,
            latestSourcePath,
            latestWriteUtc,
            string.Empty);

    public static EvidenceSourceFreshness Invalid(
        string verifierTypeName,
        string failureReason) =>
        new EvidenceSourceFreshness(
            false,
            string.Empty,
            0,
            string.Empty,
            DateTime.MinValue,
            (verifierTypeName ?? string.Empty) + ": " + failureReason);
}
#endif
