using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using UnityEngine;
using static CharacterAiPlanDebugFixtures;

public static class CharacterAiPlanDebugScenarios
{
    private static bool playModeSocialLlmProbeAccepted;
    private static bool playModeSocialLlmProbeCompleted;
    private static LocalLlmRequestStatus playModeSocialLlmProbeStatus;
    private static string playModeSocialLlmProbeReport;
    private static bool playModeSocialEndToEndAccepted;
    private static float playModeSocialEndToEndStartedAt;
    private static int playModeSocialEndToEndAppliedBefore;
    private static GameObject playModeSocialEndToEndSpeakerObject;
    private static GameObject playModeSocialEndToEndListenerObject;
    private static GameObject playModeSocialEndToEndFacilityObject;
    private static CharacterSO playModeSocialEndToEndSpeakerData;
    private static CharacterSO playModeSocialEndToEndListenerData;
    private static BuildingSO playModeSocialEndToEndFacilityData;
    private static bool playModeSocialLogEventTriggered;
    private static bool playModeSocialLogEventObserved;
    private static bool playModeSocialLogRequestAcceptedObserved;
    private static float playModeSocialLogStartedAt;
    private static int playModeSocialLogAppliedBefore;
    private static GameObject playModeSocialLogSpeakerObject;
    private static GameObject playModeSocialLogListenerObject;
    private static GameObject playModeSocialLogFacilityObject;
    private static CharacterSO playModeSocialLogSpeakerData;
    private static CharacterSO playModeSocialLogListenerData;
    private static BuildingSO playModeSocialLogFacilityData;
    private static bool playModePersonaMacroPersonaAccepted;
    private static bool playModePersonaMacroMacroAccepted;
    private static float playModePersonaMacroStartedAt;
    private static GameObject playModePersonaMacroPersonaObject;
    private static GameObject playModePersonaMacroMacroObject;
    private static GameObject playModePersonaMacroDirectorObject;
    private static CharacterSO playModePersonaMacroPersonaData;
    private static CharacterSO playModePersonaMacroMacroData;
    private static bool playModeBubbleAccepted;
    private static float playModeBubbleStartedAt;
    private static string playModeBubbleOriginalLine;
    private static GameObject playModeBubbleObject;
    private static CharacterSO playModeBubbleData;
    private static bool playModeMoodImpulseLlmAccepted;
    private static bool playModeMoodImpulseLlmPromptRecorded;
    private static float playModeMoodImpulseLlmStartedAt;
    private static GameObject playModeMoodImpulseLlmActorObject;
    private static GameObject playModeMoodImpulseLlmDirectorObject;
    private static AiDirectorRuntime playModeMoodImpulseLlmDirector;
    private static CharacterSO playModeMoodImpulseLlmData;

    private static FacilityScoringContext CreateSocialScoringContext(SocialReputationRuntime runtime)
    {
        return new FacilityScoringContext(
            new SocialReputationBiasService(new CharacterSceneRuntimeReferences(
                null,
                runtime,
                null,
                null,
                null,
                null,
                null,
                null)),
            new RoomFacilityPolicyService(new RoomLayoutCache()));
    }

    private static void ResetSchedulerPathBudgetForDebug()
    {
        CharacterAiScheduler scheduler = new DungeonSceneComponentQuery()
            .First<CharacterAiScheduler>(includeInactive: true);
        scheduler?.ResetPathSearchBudgetForDebugInstance();
    }

    private static ICharacterAiDecisionPipeline CreateDebugDecisionPipeline()
    {
        return new CharacterAiDecisionPipeline(
            NoCharacterDeprivationBoundary.Instance,
            NoCharacterDeprivationBoundary.Instance);
    }

    [MenuItem("DungeonStory/Debug/Character/Prepare Plan Character AI Test Scene")]
    public static void PrepareTestScene()
    {
        Debug.Log(CharacterAiPlanTestScenePreparer.Prepare());
    }

    [MenuItem("DungeonStory/Debug/Character/Run Plan Character AI Scenarios")]
    public static void RunAll()
    {
        List<string> errors = new List<string>();
        RunScenario("LLM JSON parser", VerifyLlmJsonParser, errors);
        RunScenario("LLM social rumor parser", VerifySocialRumorJsonParser, errors);
        RunScenario("Blackboard facility cooldown", VerifyBlackboardCooldown, errors);
        RunScenario("Social rumor spread and reputation", VerifySocialRumorSpreadAndReputation, errors);
        RunScenario("Social character relationship and source trust", VerifySocialRelationshipAndSourceTrust, errors);
        RunScenario("Social reputation affects facility utility", VerifySocialReputationAffectsFacilityScore, errors);
        RunScenario("RimWorld-style BT job giver responsibility", VerifyRimWorldStyleBtUtilityResponsibility, errors);
        RunScenario("Mood impulse JSON parser", VerifyMoodImpulseJsonParser, errors);
        RunScenario("Mood impulse biases JobGiver utility", VerifyMoodImpulseBiasesJobGiverUtility, errors);
        RunScenario("Mood impulse interrupts current action", VerifyMoodImpulseInterruptsCurrentAction, errors);
        RunScenario("Director mood impulse trigger", VerifyDirectorMoodImpulseTrigger, errors);
        RunScenario("BT continues current action before utility", VerifyBtContinuesCurrentActionBeforeUtility, errors);
        RunScenario("BT stops current action before utility", VerifyBtStopsCurrentActionBeforeUtility, errors);
        RunScenario("BT decision trace records routing", VerifyBtDecisionTraceRecordsRouting, errors);
        RunScenario("No flat utility selection API", VerifyNoFlatUtilitySelectionApi, errors);
        RunScenario("JobGiver invalid reevaluation clears cached candidate", VerifyJobGiverInvalidReevaluationClearsCachedCandidate, errors);
        RunScenario("Macro goals use JobGiver candidates", VerifyMacroGoalsUseJobGiverCandidates, errors);
        RunScenario("BehaviorTree wrapper debug", VerifyBehaviorTreeWrapperDebug, errors);
        RunScenario("BehaviorTree visual graph", VerifyBehaviorTreeVisualGraph, errors);
        RunScenario("BehaviorTree runtime branch selection", VerifyBehaviorTreeRuntimeBranchSelection, errors);
        RunScenario("GameManager has no runtime service locator", VerifyGameManagerHasNoRuntimeServiceLocator, errors);
        RunScenario("Runtime actor BehaviorTree contract", VerifyRuntimeActorBehaviorTreeContract, errors);
        RunScenario("Director context compression", VerifyDirectorContextCompression, errors);
        RunScenario("Director prompt numeric contract", VerifyDirectorPromptNumericContract, errors);
        RunScenario("Director routine macro trigger", VerifyDirectorRoutineMacroTrigger, errors);
        RunScenario("Director rejected macro request starts backoff", VerifyDirectorRejectedMacroRequestStartsBackoff, errors);
        RunScenario("Social rejected request starts cooldown", VerifySocialRejectedRequestStartsCooldown, errors);
        RunScenario("LLM queue bubble drop policy", VerifyLlmQueueDropPolicy, errors);
        RunScenario("LLM queue capacity protects non-bubble requests", VerifyLlmQueueCapacityProtectsNonBubbleRequests, errors);
        RunScenario("LLM queue supports open request profiles", VerifyLlmQueueSupportsOpenRequestProfiles, errors);
        RunScenario("LLM queue cancellation completes exactly once", VerifyLlmQueueCancellationCompletesExactlyOnce, errors);
        RunScenario("LLM persona request lifecycle", VerifyPersonaRequestLifecycle, errors);
        RunScenario("LLM persona invalid JSON rejection", VerifyPersonaRejectsInvalidJson, errors);
        RunScenario("Bubble has no original-text fallback", VerifyBubbleNoFallback, errors);
        RunScenario("Vandalize macro damages real facility state", VerifyVandalizeMacro, errors);

        if (errors.Count > 0)
        {
            throw new System.Exception("Plan Character AI scenarios failed:\n" + string.Join("\n", errors));
        }

        Debug.Log("Plan Character AI scenarios passed.");
    }

    [MenuItem("DungeonStory/Debug/Character/Run Play Mode Social AI Probe")]
    public static void RunPlayModeSocialAiProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Play Mode Social AI Probe must run while Unity is in Play Mode.");
        }

        string report = RunPlayModeSocialRuntimeProbe();
        Debug.Log(report);
    }

    [MenuItem("DungeonStory/Debug/Character/Run Play Mode BT Activation Probe")]
    public static void RunPlayModeBehaviorTreeActivationProbeMenu()
    {
        Debug.Log(RunPlayModeBehaviorTreeActivationProbe());
    }

    [MenuItem("DungeonStory/Debug/Character/Run Play Mode RimWorld Style AI Probe")]
    public static void RunPlayModeRimWorldStyleAiProbeMenu()
    {
        Debug.Log(RunPlayModeRimWorldStyleAiProbe());
    }

    [MenuItem("DungeonStory/Debug/Character/Run Play Mode Mood Impulse AI Probe")]
    public static void RunPlayModeMoodImpulseAiProbeMenu()
    {
        Debug.Log(RunPlayModeMoodImpulseRuntimeProbe());
    }

    [MenuItem("DungeonStory/Debug/Character/Start Play Mode Mood Impulse LLM Probe")]
    public static void StartPlayModeMoodImpulseLlmProbeMenu()
    {
        Debug.Log(StartPlayModeMoodImpulseLlmProbe());
    }

    [MenuItem("DungeonStory/Debug/Character/Get Play Mode Mood Impulse LLM Probe")]
    public static void GetPlayModeMoodImpulseLlmProbeMenu()
    {
        Debug.Log(GetPlayModeMoodImpulseLlmProbeReport());
    }

    [MenuItem("DungeonStory/Debug/Character/Run Play Mode Relationship AI Probe")]
    public static void RunPlayModeRelationshipAiProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Play Mode Relationship AI Probe must run while Unity is in Play Mode.");
        }

        string report = RunPlayModeRelationshipRuntimeProbe();
        Debug.Log(report);
    }

    [MenuItem("DungeonStory/Debug/Character/Start Play Mode Persona Macro LLM Probe")]
    public static void StartPlayModePersonaMacroLlmProbeMenu()
    {
        Debug.Log(StartPlayModePersonaMacroLlmProbe());
    }

    [MenuItem("DungeonStory/Debug/Character/Get Play Mode Persona Macro LLM Probe")]
    public static void GetPlayModePersonaMacroLlmProbeMenu()
    {
        Debug.Log(GetPlayModePersonaMacroLlmProbeReport());
    }

    [MenuItem("DungeonStory/Debug/Character/Start Play Mode Bubble LLM Probe")]
    public static void StartPlayModeBubbleLlmProbeMenu()
    {
        Debug.Log(StartPlayModeBubbleLlmProbe());
    }

    [MenuItem("DungeonStory/Debug/Character/Get Play Mode Bubble LLM Probe")]
    public static void GetPlayModeBubbleLlmProbeMenu()
    {
        Debug.Log(GetPlayModeBubbleLlmProbeReport());
    }

    [MenuItem("DungeonStory/Debug/Character/Start Play Mode Social Log LLM Probe")]
    public static void StartPlayModeSocialLogLlmProbeMenu()
    {
        Debug.Log(StartPlayModeSocialLogLlmProbe());
    }

    [MenuItem("DungeonStory/Debug/Character/Trigger Play Mode Social Log LLM Probe")]
    public static void TriggerPlayModeSocialLogLlmProbeMenu()
    {
        Debug.Log(TriggerPlayModeSocialLogLlmProbeEvent());
    }

    [MenuItem("DungeonStory/Debug/Character/Get Play Mode Social Log LLM Probe")]
    public static void GetPlayModeSocialLogLlmProbeMenu()
    {
        Debug.Log(GetPlayModeSocialLogLlmProbeReport());
    }

    public static string RunPlayModeSocialRuntimeProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        GameObject runtimeObject = null;
        GameObject queueObject = null;
        SocialReputationRuntime runtime = FindSocialRuntimeInstance()
            ?? EnsureSocialRuntimeInstance(out runtimeObject);
        LocalLlmRequestQueue queue = FindQueueInstance()
            ?? EnsureQueueInstance(out queueObject);

        runtime.ClearForDebug();
        GameObject speakerObject = CreatePlayActorObject("PlayRumorSpeaker");
        GameObject listenerObject = CreatePlayActorObject("PlayRumorListener");
        GameObject goodObject = new GameObject("PlayGoodFacility", typeof(BuildableObject));
        GameObject badObject = new GameObject("PlayBadFacility", typeof(BuildableObject));
        CharacterSO speakerData = CreateCharacterData(CharacterType.NPC, "Play Speaker", "Slime");
        CharacterSO listenerData = CreateCharacterData(CharacterType.NPC, "Play Listener", "Slime");
        BuildingSO goodData = CreateBuildingData(910101, "Play Praised Rest Facility");
        BuildingSO badData = CreateBuildingData(910102, "Play Warned Rest Facility");
        try
        {
            CharacterActor speaker = speakerObject.GetComponent<CharacterActor>();
            CharacterActor listener = listenerObject.GetComponent<CharacterActor>();
            speaker.EnsureRuntimeState();
            listener.EnsureRuntimeState();
            speaker.Initialize(speakerData);
            listener.Initialize(listenerData);
            speaker.SetLifecycleState(CharacterLifecycleState.Active);
            listener.SetLifecycleState(CharacterLifecycleState.Active);
            listenerObject.transform.position = speakerObject.transform.position + Vector3.right;

            BuildableObject good = goodObject.GetComponent<BuildableObject>();
            BuildableObject bad = badObject.GetComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(good);
            CharacterAiEditorTestDependencies.Inject(bad);
            good.Initialization(goodData, Vector2Int.zero);
            bad.Initialization(badData, Vector2Int.right);

            bool socialMemoryReady = speaker.SocialMemory != null && listener.SocialMemory != null;
            bool appliedWarning = runtime.ApplyRumor(new SocialRumor
            {
                type = SocialRumorType.Warning,
                targetType = SocialRumorTargetType.Facility,
                targetFacilityId = badData.id,
                sentiment = -0.9f,
                spreadChance = 1f,
                trustImpact = 0.2f,
                validUntil = Time.time + 600f,
                summary = "play mode warning",
                source = "PlayModeProbe"
            }, speaker);

            bool listenerHeardWarning = listener.SocialMemory.RecentRumors.Count > 0;
            float listenerBadSentiment = listener.SocialMemory.GetFacilitySentiment(bad);
            float globalBadSentiment = runtime.GetGlobalFacilitySentiment(bad);

            bool appliedPraise = runtime.ApplyRumor(new SocialRumor
            {
                type = SocialRumorType.Praise,
                targetType = SocialRumorTargetType.Facility,
                targetFacilityId = goodData.id,
                sentiment = 0.9f,
                spreadChance = 0f,
                trustImpact = 0.1f,
                validUntil = Time.time + 600f,
                summary = "play mode praise",
                source = "PlayModeProbe"
            }, listener);

            FacilityScoringContext scoringContext = CreateSocialScoringContext(runtime);
            float goodScore = FacilityCandidateScorer.ScoreCandidate(
                listener,
                good,
                FacilityRole.Rest,
                null,
                scoringContext);
            float badScore = FacilityCandidateScorer.ScoreCandidate(
                listener,
                bad,
                FacilityRole.Rest,
                null,
                scoringContext);
            bool scoreBiasWorks = goodScore > badScore;

            bool valid = socialMemoryReady
                && appliedWarning
                && appliedPraise
                && listenerHeardWarning
                && listenerBadSentiment < -0.2f
                && globalBadSentiment < -0.2f
                && scoreBiasWorks;
            string report =
                "PLAY_RUNTIME_SOCIAL_PROBE "
                + $"runtime=true queue=true socialMemory={socialMemoryReady} "
                + $"appliedWarning={appliedWarning} appliedPraise={appliedPraise} "
                + $"listenerHeard={listenerHeardWarning} "
                + $"listenerBadSentiment={listenerBadSentiment:0.000} "
                + $"globalBadSentiment={globalBadSentiment:0.000} "
                + $"goodScore={goodScore:0.000} badScore={badScore:0.000} "
                + $"scoreBiasWorks={scoreBiasWorks} valid={valid}";
            if (!valid)
            {
                throw new System.InvalidOperationException(report);
            }

            return report;
        }
        finally
        {
            Object.DestroyImmediate(speakerObject);
            Object.DestroyImmediate(listenerObject);
            Object.DestroyImmediate(goodObject);
            Object.DestroyImmediate(badObject);
            Object.DestroyImmediate(speakerData);
            Object.DestroyImmediate(listenerData);
            Object.DestroyImmediate(goodData);
            Object.DestroyImmediate(badData);
            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }

            if (queueObject != null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    public static bool RunLlmBackpressureScenarios()
    {
        return VerifyDirectorRejectedMacroRequestStartsBackoff()
            && VerifySocialRejectedRequestStartsCooldown()
            && VerifyLlmQueueCapacityProtectsNonBubbleRequests();
    }

    public static string RunPlayModeMoodImpulseRuntimeProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        LocalLlmRequestQueue queue = FindQueueInstance() ?? EnsureQueueInstance(out _);
        AiDirectorRuntime director = GetOrCreatePlayModeAiDirector(out GameObject directorObject);
        GameObject actorObject = null;
        CharacterSO data = null;
        ProbeWorkActionSet workActionSet = null;
        ProbeWaitActionSet waitActionSet = null;
        try
        {
            queue.ClearForDebug();
            queue.ConfigureTimeoutsForDebug(45f, 45f, 45f, 20f, 20f);
            actorObject = CreatePlayActorObject("PlayMoodImpulseActor");
            actorObject.AddComponent<AbilityWork>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actorObject.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
            data = CreateCharacterData(CharacterType.Customer, "Play Mood Impulse", "Slime");
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 35f;

            workActionSet = ScriptableObject.CreateInstance<ProbeWorkActionSet>();
            workActionSet.actionName = "Play Probe Mood Work";
            workActionSet.probeScore = 1f;
            waitActionSet = ScriptableObject.CreateInstance<ProbeWaitActionSet>();
            waitActionSet.actionName = "Play Probe Mood Wait";
            waitActionSet.probeScore = 1f;
            actor.Brain.availableActions = new[]
            {
                new AIAction { actionset = workActionSet },
                new AIAction { actionset = waitActionSet }
            };

            bool shouldRequest = director != null && director.ShouldRequestMoodImpulse(actor);
            bool accepted = director != null && director.RequestMoodImpulse(actor);
            MethodInfo onResult = typeof(AiDirectorRuntime)
                .GetMethod("OnMoodImpulseResult", BindingFlags.Instance | BindingFlags.NonPublic);
            onResult?.Invoke(
                director,
                new object[]
                {
                    actor,
                    new LocalLlmResult(
                        LocalLlmRequestStatus.Succeeded,
                        "{\"moodImpulse\":\"IgnoreDuty\",\"strength\":0.9,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"play probe low mood ignores duty\",\"validSeconds\":30}",
                        string.Empty,
                        string.Empty)
                });

            bool applied = actor.Blackboard.HasActiveMoodImpulse()
                && actor.Blackboard.ActiveMoodImpulse.type == CharacterMoodImpulseType.IgnoreDuty
                && director.LastAppliedMoodImpulseType == CharacterMoodImpulseType.IgnoreDuty;
            bool workEvaluated = CharacterAiJobGiverRegistry.Work.TryEvaluate(
                actor,
                out CharacterAiJobCandidate workCandidate);
            bool waitEvaluated = CharacterAiJobGiverRegistry.Wait.TryEvaluate(
                actor,
                out CharacterAiJobCandidate waitCandidate);
            bool utilityBiased = workEvaluated
                && waitEvaluated
                && waitCandidate.Utility > workCandidate.Utility;
            bool promptRecorded = director != null
                && director.LastRequestDebug.Contains("moodImpulse", System.StringComparison.Ordinal)
                && director.LastRequestDebug.Contains("score bias", System.StringComparison.Ordinal);
            bool valid = shouldRequest
                && accepted
                && applied
                && utilityBiased
                && promptRecorded;

            return "PLAY_MOOD_IMPULSE_PROBE "
                + $"valid={valid} "
                + $"shouldRequest={shouldRequest} "
                + $"accepted={accepted} "
                + $"applied={applied} "
                + $"utilityBiased={utilityBiased} "
                + $"workUtility={(workEvaluated ? workCandidate.Utility : -1f):0.###} "
                + $"waitUtility={(waitEvaluated ? waitCandidate.Utility : -1f):0.###} "
                + $"promptRecorded={promptRecorded} "
                + $"queued={queue.QueuedCount}";
        }
        finally
        {
            queue.ClearForDebug();
            DestroyProbeObject(actorObject);
            DestroyProbeObject(data);
            DestroyProbeObject(workActionSet);
            DestroyProbeObject(waitActionSet);
            if (directorObject != null)
            {
                DestroyProbeObject(directorObject);
            }
        }
    }

    public static string StartPlayModeMoodImpulseLlmProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        CleanupPlayModeMoodImpulseLlmProbe();
        LocalLlmRequestQueue queue = FindQueueInstance() ?? EnsureQueueInstance(out _);
        queue.AbortAllForDebug();
        queue.ConfigureTimeoutsForDebug(45f, 45f, 45f, 20f, 30f);
        AiDirectorRuntime director = GetOrCreatePlayModeAiDirector(out playModeMoodImpulseLlmDirectorObject);
        if (director == null)
        {
            throw new System.InvalidOperationException("AiDirectorRuntime is missing in Play Mode.");
        }

        playModeMoodImpulseLlmDirector = director;
        playModeMoodImpulseLlmActorObject = CreatePlayActorObject("PlayMoodImpulseLlmActor");
        if (playModeMoodImpulseLlmActorObject.GetComponent<AbilityWork>() == null)
        {
            playModeMoodImpulseLlmActorObject.AddComponent<AbilityWork>();
        }

        if (playModeMoodImpulseLlmActorObject.GetComponent<AbilityShopping>() == null)
        {
            playModeMoodImpulseLlmActorObject.AddComponent<AbilityShopping>();
        }

        CharacterAiEditorTestDependencies.Inject(playModeMoodImpulseLlmActorObject);
        playModeMoodImpulseLlmActorObject
            .GetComponent<CharacterAbilityCache>()
            ?.RefreshAbilityCache();
        playModeMoodImpulseLlmData = CreateCharacterData(
            CharacterType.NPC,
            "Play Mood Impulse LLM",
            "Slime");
        playModeMoodImpulseLlmData.aiPersonality.diligence = 1.3f;

        CharacterActor actor = playModeMoodImpulseLlmActorObject.GetComponent<CharacterActor>();
        actor.EnsureRuntimeState();
        actor.Initialize(playModeMoodImpulseLlmData);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.stats[CharacterCondition.HUNGER] = 100f;
        actor.stats[CharacterCondition.SLEEP] = 100f;
        actor.stats[CharacterCondition.FUN] = 25f;
        actor.stats[CharacterCondition.MOOD] = 35f;
        actor.Brain.availableActions = AiDebugScenarioActionFactory.CreateCustomerActions();

        bool shouldRequest = director.ShouldRequestMoodImpulse(actor);
        playModeMoodImpulseLlmAccepted = director.RequestMoodImpulse(actor);
        playModeMoodImpulseLlmPromptRecorded = director.LastRequestDebug.Contains(
            "moodImpulse",
            System.StringComparison.Ordinal)
            && director.LastRequestDebug.Contains(
                "score bias",
                System.StringComparison.Ordinal);
        playModeMoodImpulseLlmStartedAt = Time.realtimeSinceStartup;
        return "PLAY_MOOD_IMPULSE_LLM_PROBE_START "
            + $"shouldRequest={shouldRequest} "
            + $"accepted={playModeMoodImpulseLlmAccepted} "
            + $"queued={queue.QueuedCount} running={queue.RunningCount} "
            + $"promptRecorded={playModeMoodImpulseLlmPromptRecorded}";
    }

    public static string GetPlayModeMoodImpulseLlmProbeReport(bool cleanupOnComplete = true)
    {
        CharacterActor actor = playModeMoodImpulseLlmActorObject != null
            ? playModeMoodImpulseLlmActorObject.GetComponent<CharacterActor>()
            : null;
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        AiDirectorRuntime director = playModeMoodImpulseLlmDirector != null
            ? playModeMoodImpulseLlmDirector
            : FindPlayModeAiDirector();
        LocalLlmRequestQueue queue = FindQueueInstance();
        CharacterMoodImpulse impulse = blackboard != null
            ? blackboard.ActiveMoodImpulse
            : null;
        CharacterMacroGoal macroGoal = blackboard != null
            ? blackboard.ActiveMacroGoal
            : null;

        bool applied = blackboard != null
            && blackboard.HasActiveMoodImpulse()
            && impulse != null
            && impulse.type != CharacterMoodImpulseType.None
            && string.Equals(impulse.source, "LocalLLM", System.StringComparison.Ordinal);
        bool observed = director != null
            && director.LastAppliedMoodImpulseType != CharacterMoodImpulseType.None
            && actor != null
            && string.Equals(
                director.LastAppliedMoodImpulseActorName,
                actor.name,
                System.StringComparison.Ordinal);
        bool macroPromoted = blackboard != null
            && blackboard.HasActiveMacroGoal()
            && macroGoal != null
            && string.Equals(macroGoal.source, "MoodImpulseLLM", System.StringComparison.Ordinal);
        bool promptRecorded = director != null
            && !string.IsNullOrWhiteSpace(director.LastRequestDebug)
            && director.LastRequestDebug.Contains("moodImpulse", System.StringComparison.Ordinal)
            && director.LastRequestDebug.Contains("score bias", System.StringComparison.Ordinal);
        promptRecorded |= playModeMoodImpulseLlmPromptRecorded;
        string biasDebug = string.Empty;
        bool utilityBiasVerified = applied
            && VerifyMoodImpulseRuntimeBias(actor, impulse.type, out biasDebug);
        bool valid = playModeMoodImpulseLlmAccepted
            && applied
            && promptRecorded
            && (utilityBiasVerified || macroPromoted);
        float elapsed = Time.realtimeSinceStartup - playModeMoodImpulseLlmStartedAt;
        bool finished = valid || elapsed > 35f;
        string report = "PLAY_MOOD_IMPULSE_LLM_PROBE "
            + $"valid={valid} "
            + $"accepted={playModeMoodImpulseLlmAccepted} "
            + $"applied={applied} "
            + $"observed={observed} "
            + $"directorType={(director != null ? director.LastAppliedMoodImpulseType.ToString() : "None")} "
            + $"directorActor={SafeDebugValue(director != null ? director.LastAppliedMoodImpulseActorName : string.Empty)} "
            + $"impulse={(impulse != null ? impulse.type.ToString() : "None")} "
            + $"strength={(impulse != null ? impulse.strength : 0f):0.###} "
            + $"source={SafeDebugValue(impulse != null ? impulse.source : string.Empty)} "
            + $"macroPromoted={macroPromoted} "
            + $"utilityBiasVerified={utilityBiasVerified} "
            + $"bias={SafeDebugValue(biasDebug, 140)} "
            + $"promptRecorded={promptRecorded} "
            + $"queued={(queue != null ? queue.QueuedCount : -1)} "
            + $"running={(queue != null ? queue.RunningCount : -1)} "
            + $"lastError={SafeDebugValue(director != null ? director.LastError : string.Empty, 140)} "
            + $"elapsed={elapsed:0.0}";

        if (finished && cleanupOnComplete)
        {
            CleanupPlayModeMoodImpulseLlmProbe();
        }

        return report;
    }

    public static void CleanupPlayModeMoodImpulseLlmProbe()
    {
        DestroyProbeObject(playModeMoodImpulseLlmActorObject);
        DestroyProbeObject(playModeMoodImpulseLlmData);
        if (playModeMoodImpulseLlmDirectorObject != null)
        {
            DestroyProbeObject(playModeMoodImpulseLlmDirectorObject);
        }

        playModeMoodImpulseLlmAccepted = false;
        playModeMoodImpulseLlmPromptRecorded = false;
        playModeMoodImpulseLlmStartedAt = 0f;
        playModeMoodImpulseLlmActorObject = null;
        playModeMoodImpulseLlmDirectorObject = null;
        playModeMoodImpulseLlmDirector = null;
        playModeMoodImpulseLlmData = null;
    }

    private static bool VerifyMoodImpulseRuntimeBias(
        CharacterActor actor,
        CharacterMoodImpulseType impulseType,
        out string debug)
    {
        debug = string.Empty;
        if (actor == null || impulseType == CharacterMoodImpulseType.None)
        {
            return false;
        }

        CharacterAiBranch branch = impulseType switch
        {
            CharacterMoodImpulseType.FollowRoutine => CharacterAiBranch.Work,
            CharacterMoodImpulseType.SeekFood => CharacterAiBranch.Eat,
            CharacterMoodImpulseType.SeekRest => CharacterAiBranch.Rest,
            CharacterMoodImpulseType.SeekFun => CharacterAiBranch.Shopping,
            CharacterMoodImpulseType.ImpulseShopping => CharacterAiBranch.Shopping,
            CharacterMoodImpulseType.Wander => CharacterAiBranch.LookAround,
            CharacterMoodImpulseType.Wait => CharacterAiBranch.Wait,
            CharacterMoodImpulseType.IgnoreDuty => CharacterAiBranch.Wait,
            CharacterMoodImpulseType.Complain => CharacterAiBranch.Wait,
            CharacterMoodImpulseType.ExitDungeon => CharacterAiBranch.ExitDungeon,
            CharacterMoodImpulseType.Vandalize => CharacterAiBranch.LookAround,
            _ => CharacterAiBranch.None
        };

        if (branch == CharacterAiBranch.None)
        {
            debug = $"No direct utility branch for {impulseType}.";
            return false;
        }

        float adjusted = CharacterMoodImpulseUtility.ApplyJobGiverBias(
            actor,
            branch,
            0.2f,
            out string reason);
        debug = $"{branch} base=0.2 adjusted={adjusted:0.###} reason={reason}";
        return adjusted > 0.2f
            && reason.Contains("moodImpulse", System.StringComparison.Ordinal);
    }

    public static string RunPlayModeBehaviorTreeActivationProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        CharacterActor[] actors = Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int checkedActors = 0;
        int runningTrees = 0;
        int externalTrees = 0;
        int tickedTrees = 0;
        int transitionedTrees = 0;
        int leafTransitionedTrees = 0;
        List<string> inactive = new List<string>();
        foreach (CharacterActor actor in actors)
        {
            if (actor == null)
            {
                continue;
            }

            BehaviorTree tree = actor.GetComponent<BehaviorTree>();
            checkedActors++;
            bool hasExternal = tree != null && tree.ExternalBehavior != null;
            bool running = tree != null && tree.ExecutionStatus == TaskStatus.Running;
            bool ticked = tree != null && tree.DungeonStoryTickCount > 0;
            bool transitioned = tree != null && HasRunningTransitionBelowRoot(tree);
            bool leafTransitioned = tree != null && HasRunningLeafTransitionBelowRoot(tree);
            if (hasExternal)
            {
                externalTrees++;
            }

            if (running)
            {
                runningTrees++;
            }

            if (ticked)
            {
                tickedTrees++;
            }

            if (transitioned)
            {
                transitionedTrees++;
            }

            if (leafTransitioned)
            {
                leafTransitionedTrees++;
            }

            if (!hasExternal || !running || !transitioned || !leafTransitioned)
            {
                inactive.Add(
                    $"{actor.name}: external={hasExternal}, running={running}, transitioned={transitioned}, leafTransitioned={leafTransitioned}, ticks={(tree != null ? tree.DungeonStoryTickCount : 0)}");
            }
        }

        bool valid = checkedActors > 0
            && runningTrees == checkedActors
            && externalTrees == checkedActors
            && transitionedTrees == checkedActors
            && leafTransitionedTrees == checkedActors;
        string report = "PLAY_BT_ACTIVATION_PROBE "
            + $"actors={checkedActors} "
            + $"running={runningTrees} "
            + $"external={externalTrees} "
            + $"ticked={tickedTrees} "
            + $"transitioned={transitionedTrees} "
            + $"leafTransitioned={leafTransitionedTrees} "
            + $"valid={valid}";
        if (inactive.Count > 0)
        {
            report += " inactive=" + string.Join(" | ", inactive);
        }

        if (!valid)
        {
            throw new System.InvalidOperationException(report);
        }

        return report;
    }

    public static string RunPlayModeRimWorldStyleAiProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        CharacterActor[] actors = Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        int checkedActors = 0;
        int coarseCategoryBranches = 0;
        int jobGiverBranches = 0;
        int metaBranches = 0;
        int utilitySelectedActions = 0;
        int normalRoutineTrees = 0;
        int routineGroupPriorityReports = 0;
        int jobGiverUtilityReports = 0;
        int ignoredActors = 0;
        List<string> states = new List<string>();
        foreach (CharacterActor actor in actors)
        {
            if (actor == null)
            {
                continue;
            }

            actor.EnsureRuntimeState();
            if (!actor.CanRunAi)
            {
                ignoredActors++;
                states.Add($"{actor.name}:ignored state={actor.CurrentLifecycleState}");
                continue;
            }

            BehaviorTree tree = actor.GetComponent<BehaviorTree>();
            tree?.DungeonStoryManualTick(actor);
            tree?.DungeonStoryRefreshVisualStatus(actor);
            if (HasNormalRoutineTree(tree))
            {
                normalRoutineTrees++;
            }

            CharacterAiBranch branch = actor.Blackboard != null
                ? actor.Blackboard.CurrentBranch
                : CharacterAiBranch.None;
            bool coarseCategoryBranch = branch == CharacterAiBranch.SurvivalNeeds
                || branch == CharacterAiBranch.DutyWork
                || branch == CharacterAiBranch.LeisureVisit;
            bool jobGiverBranch = branch == CharacterAiBranch.ExitDungeon
                || branch == CharacterAiBranch.Eat
                || branch == CharacterAiBranch.Rest
                || branch == CharacterAiBranch.Toilet
                || branch == CharacterAiBranch.Hygiene
                || branch == CharacterAiBranch.Work
                || branch == CharacterAiBranch.Shopping
                || branch == CharacterAiBranch.LookAround
                || branch == CharacterAiBranch.Wait;
            bool metaBranch = branch == CharacterAiBranch.Critical
                || branch == CharacterAiBranch.MacroGoal
                || branch == CharacterAiBranch.LockedAction
                || branch == CharacterAiBranch.InterruptCheck
                || branch == CharacterAiBranch.Emergency
                || branch == CharacterAiBranch.RoutineUtility
                || branch == CharacterAiBranch.ContinueCurrent
                || branch == CharacterAiBranch.StopCurrent
                || branch == CharacterAiBranch.Idle;
            AIAction selectedAction = actor.Brain != null ? actor.Brain.bestAction : null;
            string selectedActionLabel = selectedAction != null
                ? CharacterAiDecisionPipeline.GetActionLabel(selectedAction.actionset)
                : "None";
            string destinationLabel = selectedAction != null && selectedAction.destination != null
                ? selectedAction.destination.name
                : "None";

            checkedActors++;
            if (coarseCategoryBranch)
            {
                coarseCategoryBranches++;
            }

            if (jobGiverBranch)
            {
                jobGiverBranches++;
            }

            if (metaBranch)
            {
                metaBranches++;
            }

            if (selectedAction != null && selectedAction.actionset != null)
            {
                utilitySelectedActions++;
            }

            string jobGiverSummary = actor.Blackboard != null
                ? actor.Blackboard.SelectedJobGiverUtilitySummary
                : string.Empty;
            if ((jobGiverBranch
                    || branch == CharacterAiBranch.Emergency
                    || branch == CharacterAiBranch.RoutineUtility)
                && !string.IsNullOrWhiteSpace(jobGiverSummary))
            {
                jobGiverUtilityReports++;
            }

            if (HasRoutineGroupPriorityReport(actor))
            {
                routineGroupPriorityReports++;
            }

            states.Add(
                $"{actor.name}:{branch}/{actor.Blackboard?.CurrentTask}/intent={actor.Blackboard?.CurrentIntent}/action={selectedActionLabel}/dest={destinationLabel}/utility={SafeDebugValue(jobGiverSummary)}/route={SafeDebugValue(actor.Blackboard?.LastDecisionRouteSummary, 220)}/trace={SafeDebugValue(actor.Blackboard?.LastDecisionTrace)}/ticks={(tree != null ? tree.DungeonStoryTickCount : 0)}");
        }

        bool valid = checkedActors > 0
            && coarseCategoryBranches == 0
            && jobGiverBranches + metaBranches == checkedActors
            && normalRoutineTrees == checkedActors
            && routineGroupPriorityReports == checkedActors
            && jobGiverUtilityReports >= jobGiverBranches;
        string report = "PLAY_RIMWORLD_STYLE_AI_PROBE "
            + $"actors={checkedActors} "
            + $"ignored={ignoredActors} "
            + $"normalRoutineTrees={normalRoutineTrees} "
            + $"routineGroupPriorityReports={routineGroupPriorityReports} "
            + $"jobGiverBranches={jobGiverBranches} "
            + $"jobGiverUtilityReports={jobGiverUtilityReports} "
            + $"metaBranches={metaBranches} "
            + $"coarseCategoryBranches={coarseCategoryBranches} "
            + $"utilitySelectedActions={utilitySelectedActions} "
            + $"valid={valid} "
            + $"states={string.Join(" | ", states)}";
        if (!valid)
        {
            throw new System.InvalidOperationException(report);
        }

        return report;
    }

    private static bool HasNormalRoutineTree(BehaviorTree tree)
    {
        BehaviorDesigner.Runtime.BehaviorSource source = tree != null ? tree.GetBehaviorSource() : null;
        ParentTask root = source != null ? source.RootTask as ParentTask : null;
        if (root == null || root.Children == null || root.Children.Count < 7)
        {
            return false;
        }

        return TaskDisplayName(root.Children[0]) == "Critical"
            && TaskDisplayName(root.Children[1]) == "Locked Action"
            && TaskDisplayName(root.Children[2]) == "Interrupt Check"
            && TaskDisplayName(root.Children[3]) == "Macro Goals"
            && TaskDisplayName(root.Children[4]) == "Emergency"
            && TaskDisplayName(root.Children[5]) == "Routine Utility"
            && TaskDisplayName(root.Children[6]) == "Idle";
    }

    private static bool HasRoutineGroupPriorityReport(CharacterActor actor)
    {
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        if (blackboard == null)
        {
            return false;
        }

        return blackboard.RoutineGroupPriorityScores.ContainsKey(CharacterAiBranch.SurvivalNeeds)
            && blackboard.RoutineGroupPriorityScores.ContainsKey(CharacterAiBranch.DutyWork)
            && blackboard.RoutineGroupPriorityScores.ContainsKey(CharacterAiBranch.LeisureVisit)
            && blackboard.RoutineGroupPriorityScores.ContainsKey(CharacterAiBranch.Idle);
    }

    private static bool HasRunningTransitionBelowRoot(BehaviorTree tree)
    {
        ParentTask root = tree != null && tree.GetBehaviorSource() != null
            ? tree.GetBehaviorSource().RootTask as ParentTask
            : null;
        if (root == null || root.Children == null)
        {
            return false;
        }

        foreach (BehaviorDesigner.Runtime.Tasks.Task child in root.Children)
        {
            if (HasRunningNode(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRunningLeafTransitionBelowRoot(BehaviorTree tree)
    {
        ParentTask root = tree != null && tree.GetBehaviorSource() != null
            ? tree.GetBehaviorSource().RootTask as ParentTask
            : null;
        if (root == null || root.Children == null)
        {
            return false;
        }

        foreach (BehaviorDesigner.Runtime.Tasks.Task child in root.Children)
        {
            if (HasRunningLeafNode(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRunningNode(BehaviorDesigner.Runtime.Tasks.Task task)
    {
        if (task == null)
        {
            return false;
        }

        if (task.NodeData != null && task.NodeData.ExecutionStatus == TaskStatus.Running)
        {
            return true;
        }

        if (task is not ParentTask parent || parent.Children == null)
        {
            return false;
        }

        foreach (BehaviorDesigner.Runtime.Tasks.Task child in parent.Children)
        {
            if (HasRunningNode(child))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRunningLeafNode(BehaviorDesigner.Runtime.Tasks.Task task)
    {
        if (task == null)
        {
            return false;
        }

        if (!(task is ParentTask parent) || parent.Children == null || parent.Children.Count == 0)
        {
            return task.NodeData != null && task.NodeData.ExecutionStatus == TaskStatus.Running;
        }

        foreach (BehaviorDesigner.Runtime.Tasks.Task child in parent.Children)
        {
            if (HasRunningLeafNode(child))
            {
                return true;
            }
        }

        return false;
    }

    public static string RunPlayModeRelationshipRuntimeProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        GameObject runtimeObject = null;
        SocialReputationRuntime runtime = FindSocialRuntimeInstance()
            ?? EnsureSocialRuntimeInstance(out runtimeObject);

        runtime.ClearForDebug();
        GameObject speakerObject = CreatePlayActorObject("PlayRelationshipSpeaker");
        GameObject listenerObject = CreatePlayActorObject("PlayRelationshipListener");
        GameObject targetObject = CreatePlayActorObject("PlayRelationshipTarget");
        GameObject unrelatedFacilityObject = new GameObject("PlayRelationshipUnrelatedFacility", typeof(BuildableObject));
        CharacterSO speakerData = CreateCharacterData(CharacterType.NPC, "Play Trusted Speaker", "Slime");
        CharacterSO listenerData = CreateCharacterData(CharacterType.NPC, "Play Relationship Listener", "Slime");
        CharacterSO targetData = CreateCharacterData(CharacterType.NPC, "Play Rude Guest", "Imp");
        BuildingSO unrelatedFacilityData = CreateBuildingData(918204, "Play Relationship Unrelated Rest");
        speakerData.id = 918201;
        listenerData.id = 918202;
        targetData.id = 918203;
        try
        {
            CharacterActor speaker = speakerObject.GetComponent<CharacterActor>();
            CharacterActor listener = listenerObject.GetComponent<CharacterActor>();
            CharacterActor target = targetObject.GetComponent<CharacterActor>();
            speaker.EnsureRuntimeState();
            listener.EnsureRuntimeState();
            target.EnsureRuntimeState();
            speaker.Initialize(speakerData);
            listener.Initialize(listenerData);
            target.Initialize(targetData);
            target.Identity.SetPersistentId("character:world:test:target");
            speaker.SetLifecycleState(CharacterLifecycleState.Active);
            listener.SetLifecycleState(CharacterLifecycleState.Active);
            target.SetLifecycleState(CharacterLifecycleState.Active);
            speakerObject.transform.position = Vector3.zero;
            listenerObject.transform.position = Vector3.right;
            targetObject.transform.position = Vector3.right * 2f;

            BuildableObject unrelatedFacility = unrelatedFacilityObject.GetComponent<BuildableObject>();
            unrelatedFacility.Initialization(unrelatedFacilityData, Vector2Int.zero);

            bool applied = runtime.ApplyRumor(new SocialRumor
            {
                type = SocialRumorType.Complaint,
                targetType = SocialRumorTargetType.Character,
                targetCharacterId = target.Identity.PersistentId,
                targetCharacterName = targetData.characterName,
                sentiment = -0.75f,
                spreadChance = 1f,
                trustImpact = 0.6f,
                validUntil = Time.time + 600f,
                summary = "blocked a room and cut in line",
                source = "PlayModeProbe"
            }, speaker);

            float listenerRelationship = listener.SocialMemory.GetRelationshipSentiment(target);
            float listenerSourceTrust = listener.SocialMemory.GetSourceTrust(speaker);
            float speakerRelationship = speaker.SocialMemory.GetRelationshipSentiment(target);
            float unrelatedFacilityReputation = runtime.GetGlobalFacilitySentiment(unrelatedFacility);
            bool valid = applied
                && listener.SocialMemory.RecentRumors.Count >= 1
                && listenerRelationship < -0.2f
                && speakerRelationship < -0.2f
                && listenerSourceTrust > 1.05f
                && Mathf.Approximately(unrelatedFacilityReputation, 0f);
            string report =
                "PLAY_RELATIONSHIP_RUNTIME_PROBE "
                + $"applied={applied} "
                + $"listenerRumors={listener.SocialMemory.RecentRumors.Count} "
                + $"listenerRelationship={listenerRelationship:0.000} "
                + $"speakerRelationship={speakerRelationship:0.000} "
                + $"listenerSourceTrust={listenerSourceTrust:0.000} "
                + $"unrelatedFacilityReputation={unrelatedFacilityReputation:0.000} "
                + $"valid={valid}";
            if (!valid)
            {
                throw new System.InvalidOperationException(report);
            }

            return report;
        }
        finally
        {
            Object.DestroyImmediate(speakerObject);
            Object.DestroyImmediate(listenerObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(unrelatedFacilityObject);
            Object.DestroyImmediate(speakerData);
            Object.DestroyImmediate(listenerData);
            Object.DestroyImmediate(targetData);
            Object.DestroyImmediate(unrelatedFacilityData);
            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }
        }
    }

    public static string StartPlayModeSocialLlmProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        LocalLlmRequestQueue queue = FindQueueInstance()
            ?? EnsureQueueInstance(out _);
        queue.ConfigureTimeoutsForDebug(45f, 45f, 45f, 20f);
        queue.ClearForDebug();

        playModeSocialLlmProbeAccepted = false;
        playModeSocialLlmProbeCompleted = false;
        playModeSocialLlmProbeStatus = LocalLlmRequestStatus.Failed;
        playModeSocialLlmProbeReport = "pending";

        string prompt =
            "Interpret this NPC social event. Return exactly one compact JSON object and no markdown. "
            + "Required shape: {\"rumorType\":\"Recommendation\",\"targetType\":\"Facility\",\"targetFacilityId\":910201,\"targetFacilityTag\":\"Rest\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":0.75,\"summary\":\"short text\",\"spreadChance\":0.6,\"trustImpact\":0.1,\"validSeconds\":600}. "
            + "All numeric fields must be raw JSON numbers, never strings, words, or null. "
            + "targetFacilityId must be an integer; targetCharacterId must be a persistent-ID string. "
            + "sentiment and trustImpact must be between -1 and 1. spreadChance must be between 0 and 1. "
            + "validSeconds must be 600 for this probe and must never be 3600. "
            + "Allowed rumorType: None, Complaint, Recommendation, Warning, Praise. "
            + "Allowed targetType: None, Facility, Character. "
            + "Event: customer recommends the Rest facility after a good visit. "
            + "candidateFacilities: id=910201; name=Probe Rest; tag=Rest.";
        playModeSocialLlmProbeAccepted = queue.GenerateSocialRumorAsync(prompt, OnPlayModeSocialLlmProbeResult);
        playModeSocialLlmProbeReport = playModeSocialLlmProbeAccepted
            ? "PLAY_LLM_SOCIAL_PROBE accepted=true completed=false"
            : "PLAY_LLM_SOCIAL_PROBE accepted=false";
        return playModeSocialLlmProbeReport;
    }

    public static string GetPlayModeSocialLlmProbeReport()
    {
        return playModeSocialLlmProbeReport
            + $" accepted={playModeSocialLlmProbeAccepted}"
            + $" completed={playModeSocialLlmProbeCompleted}"
            + $" status={playModeSocialLlmProbeStatus}";
    }

    public static string GetPlayModeLlmQueueDebugReport()
    {
        LocalLlmRequestQueue queue = FindQueueInstance();
        return "PLAY_LLM_QUEUE_DEBUG "
            + $"exists={queue != null} "
            + $"enabled={(queue != null && queue.enabled)} "
            + $"active={(queue != null && queue.gameObject.activeInHierarchy)} "
            + $"queued={(queue != null ? queue.QueuedCount : -1)} "
            + $"running={(queue != null ? queue.RunningCount : -1)} "
            + $"timeouts={(queue != null ? queue.TimeoutCount : -1)} "
            + $"lastError={SafeDebugValue(queue != null ? queue.LastError : string.Empty)}";
    }

    public static string StartPlayModeSocialEndToEndProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        SocialReputationRuntime runtime = FindSocialRuntimeInstance()
            ?? EnsureSocialRuntimeInstance(out _);
        LocalLlmRequestQueue queue = FindQueueInstance() ?? EnsureQueueInstance(out _);
        queue.ConfigureTimeoutsForDebug(45f, 45f, 45f, 20f);

        CleanupPlayModeSocialEndToEndProbe();
        runtime.SetActorLogRequestsSuppressedForDebug(true);
        runtime.ClearForDebug();
        playModeSocialEndToEndSpeakerObject = CreatePlayActorObject("PlaySocialE2ESpeaker");
        playModeSocialEndToEndListenerObject = CreatePlayActorObject("PlaySocialE2EListener");
        playModeSocialEndToEndFacilityObject = new GameObject("PlaySocialE2EFacility", typeof(BuildableObject));
        playModeSocialEndToEndSpeakerData = CreateCharacterData(CharacterType.NPC, "Play E2E Speaker", "Slime");
        playModeSocialEndToEndListenerData = CreateCharacterData(CharacterType.NPC, "Play E2E Listener", "Slime");
        playModeSocialEndToEndFacilityData = CreateBuildingData(910301, "Play E2E Rest Facility");

        CharacterActor speaker = playModeSocialEndToEndSpeakerObject.GetComponent<CharacterActor>();
        CharacterActor listener = playModeSocialEndToEndListenerObject.GetComponent<CharacterActor>();
        speaker.EnsureRuntimeState();
        listener.EnsureRuntimeState();
        speaker.Initialize(playModeSocialEndToEndSpeakerData);
        listener.Initialize(playModeSocialEndToEndListenerData);
        speaker.SetLifecycleState(CharacterLifecycleState.Active);
        listener.SetLifecycleState(CharacterLifecycleState.Active);
        playModeSocialEndToEndListenerObject.transform.position =
            playModeSocialEndToEndSpeakerObject.transform.position + Vector3.right;

        BuildableObject facility = playModeSocialEndToEndFacilityObject.GetComponent<BuildableObject>();
        facility.Initialization(playModeSocialEndToEndFacilityData, Vector2Int.zero);

        playModeSocialEndToEndAppliedBefore = runtime.AppliedRumorCount;
        playModeSocialEndToEndStartedAt = Time.realtimeSinceStartup;
        playModeSocialEndToEndAccepted = runtime.ReportFacilityExperience(
            speaker,
            facility,
            "Recommendation",
            0.85f,
            "customer recommends the Rest facility after a good visit");

        return "PLAY_SOCIAL_E2E_PROBE "
            + $"accepted={playModeSocialEndToEndAccepted} "
            + $"appliedBefore={playModeSocialEndToEndAppliedBefore}";
    }

    public static string GetPlayModeSocialEndToEndProbeReport(bool cleanupOnComplete = true)
    {
        SocialReputationRuntime runtime = FindSocialRuntimeInstance();
        if (runtime == null)
        {
            return "PLAY_SOCIAL_E2E_PROBE runtime=false";
        }

        CharacterActor listener = playModeSocialEndToEndListenerObject != null
            ? playModeSocialEndToEndListenerObject.GetComponent<CharacterActor>()
            : null;
        BuildableObject facility = playModeSocialEndToEndFacilityObject != null
            ? playModeSocialEndToEndFacilityObject.GetComponent<BuildableObject>()
            : null;
        bool applied = runtime.AppliedRumorCount > playModeSocialEndToEndAppliedBefore;
        bool listenerHeard = listener != null
            && listener.SocialMemory != null
            && listener.SocialMemory.RecentRumors.Count > 0;
        float listenerSentiment = listener != null && listener.SocialMemory != null && facility != null
            ? listener.SocialMemory.GetFacilitySentiment(facility)
            : 0f;
        float globalSentiment = facility != null ? runtime.GetGlobalFacilitySentiment(facility) : 0f;
        bool valid = playModeSocialEndToEndAccepted
            && applied
            && listenerHeard
            && listenerSentiment > 0.1f
            && globalSentiment > 0.01f;
        float elapsed = Time.realtimeSinceStartup - playModeSocialEndToEndStartedAt;
        bool finished = valid || elapsed > 45f;
        string report = "PLAY_SOCIAL_E2E_PROBE "
            + $"accepted={playModeSocialEndToEndAccepted} "
            + $"applied={applied} "
            + $"listenerHeard={listenerHeard} "
            + $"listenerSentiment={listenerSentiment:0.000} "
            + $"globalSentiment={globalSentiment:0.000} "
            + $"valid={valid} "
            + $"elapsed={elapsed:0.0} "
            + $"lastRumor={runtime.LastRumorDebug} "
            + $"lastError={runtime.LastError}";

        if (cleanupOnComplete && finished)
        {
            CleanupPlayModeSocialEndToEndProbe();
        }

        return report;
    }

    public static void CleanupPlayModeSocialEndToEndProbe()
    {
        DestroyProbeObject(playModeSocialEndToEndSpeakerObject);
        DestroyProbeObject(playModeSocialEndToEndListenerObject);
        DestroyProbeObject(playModeSocialEndToEndFacilityObject);
        DestroyProbeObject(playModeSocialEndToEndSpeakerData);
        DestroyProbeObject(playModeSocialEndToEndListenerData);
        DestroyProbeObject(playModeSocialEndToEndFacilityData);
        playModeSocialEndToEndSpeakerObject = null;
        playModeSocialEndToEndListenerObject = null;
        playModeSocialEndToEndFacilityObject = null;
        playModeSocialEndToEndSpeakerData = null;
        playModeSocialEndToEndListenerData = null;
        playModeSocialEndToEndFacilityData = null;
        playModeSocialEndToEndAccepted = false;
        playModeSocialEndToEndStartedAt = 0f;
        playModeSocialEndToEndAppliedBefore = 0;
        SocialReputationRuntime runtime = FindSocialRuntimeInstance();
        if (runtime != null)
        {
            runtime.SetActorLogRequestsSuppressedForDebug(false);
        }
    }

    public static string StartPlayModeSocialLogLlmProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        SocialReputationRuntime runtime = FindSocialRuntimeInstance()
            ?? EnsureSocialRuntimeInstance(out _);
        LocalLlmRequestQueue queue = FindQueueInstance() ?? EnsureQueueInstance(out _);
        queue.ConfigureTimeoutsForDebug(45f, 45f, 45f, 20f);

        CleanupPlayModeSocialLogLlmProbe();
        runtime.SetActorLogRequestsSuppressedForDebug(false);
        runtime.ClearForDebug();
        playModeSocialLogSpeakerObject = CreatePlayActorObject("PlaySocialLogSpeaker");
        playModeSocialLogListenerObject = CreatePlayActorObject("PlaySocialLogListener");
        playModeSocialLogFacilityObject = new GameObject("PlaySocialLogRestFacility", typeof(BuildableObject));
        playModeSocialLogSpeakerData = CreateCharacterData(CharacterType.NPC, "Play Log Speaker", "Slime");
        playModeSocialLogListenerData = CreateCharacterData(CharacterType.NPC, "Play Log Listener", "Slime");
        playModeSocialLogFacilityData = CreateBuildingData(910401, "Play Log Rest Facility");

        CharacterActor speaker = playModeSocialLogSpeakerObject.GetComponent<CharacterActor>();
        CharacterActor listener = playModeSocialLogListenerObject.GetComponent<CharacterActor>();
        speaker.EnsureRuntimeState();
        listener.EnsureRuntimeState();
        speaker.Initialize(playModeSocialLogSpeakerData);
        listener.Initialize(playModeSocialLogListenerData);
        speaker.SetLifecycleState(CharacterLifecycleState.Active);
        listener.SetLifecycleState(CharacterLifecycleState.Active);
        runtime.RegisterActorForDebug(speaker);
        runtime.RegisterActorForDebug(listener);
        runtime.RestrictActorLogRequestsForDebug(speaker);
        playModeSocialLogSpeakerObject.transform.position = Vector3.zero;
        playModeSocialLogListenerObject.transform.position = Vector3.right;

        BuildableObject facility = playModeSocialLogFacilityObject.GetComponent<BuildableObject>();
        facility.Initialization(playModeSocialLogFacilityData, Vector2Int.zero);
        playModeSocialLogFacilityObject.transform.position = Vector3.right * 0.5f;

        playModeSocialLogEventTriggered = false;
        playModeSocialLogEventObserved = false;
        playModeSocialLogRequestAcceptedObserved = false;
        playModeSocialLogStartedAt = Time.realtimeSinceStartup;
        playModeSocialLogAppliedBefore = runtime.AppliedRumorCount;
        return "PLAY_SOCIAL_LOG_LLM_PROBE ready=true waitForActorRegistration=true";
    }

    public static string TriggerPlayModeSocialLogLlmProbeEvent()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        SocialReputationRuntime runtime = FindSocialRuntimeInstance();
        CharacterActor speaker = playModeSocialLogSpeakerObject != null
            ? playModeSocialLogSpeakerObject.GetComponent<CharacterActor>()
            : null;
        if (runtime == null || speaker == null)
        {
            return "PLAY_SOCIAL_LOG_LLM_PROBE triggered=false missingRuntimeOrSpeaker=true";
        }

        playModeSocialLogAppliedBefore = runtime.AppliedRumorCount;
        playModeSocialLogStartedAt = Time.realtimeSinceStartup;
        playModeSocialLogEventTriggered = true;
        int eventCountBefore = runtime.ActorLogEventCountForDebug;
        speaker.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Social,
            CharacterActivityOutcomes.Failed,
            "AI failure: blocked path to Rest facility id 910401; destination occupied; AUTO_SOCIAL_LOG_PROBE_910401.",
            actionId: "probe:social-log",
            targetId: "facility:910401",
            reasonCode: "destination-occupied",
            facilityId: 910401,
            sentiment: -0.65f,
            bubbleEligible: true));
        playModeSocialLogEventObserved = runtime.ActorLogEventCountForDebug > eventCountBefore;
        playModeSocialLogRequestAcceptedObserved = playModeSocialLogEventObserved
            && string.IsNullOrWhiteSpace(runtime.LastRequestSkipDebug)
            && !string.IsNullOrWhiteSpace(runtime.LastRequestDebug);
        bool requestObserved = runtime.LastRequestDebug.Contains(
            "AUTO_SOCIAL_LOG_PROBE_910401",
            System.StringComparison.Ordinal);
        return "PLAY_SOCIAL_LOG_LLM_PROBE "
            + $"triggered=true eventObserved={playModeSocialLogEventObserved} "
            + $"requestAccepted={playModeSocialLogRequestAcceptedObserved} "
            + $"promptMarkerObserved={requestObserved} "
            + $"appliedBefore={playModeSocialLogAppliedBefore}";
    }

    public static string GetPlayModeSocialLogLlmProbeReport(bool cleanupOnComplete = true)
    {
        SocialReputationRuntime runtime = FindSocialRuntimeInstance();
        if (runtime == null)
        {
            return "PLAY_SOCIAL_LOG_LLM_PROBE runtime=false";
        }

        CharacterActor listener = playModeSocialLogListenerObject != null
            ? playModeSocialLogListenerObject.GetComponent<CharacterActor>()
            : null;
        BuildableObject facility = playModeSocialLogFacilityObject != null
            ? playModeSocialLogFacilityObject.GetComponent<BuildableObject>()
            : null;
        bool promptMarkerObserved = runtime.LastRequestDebug.Contains(
            "AUTO_SOCIAL_LOG_PROBE_910401",
            System.StringComparison.Ordinal);
        bool applied = runtime.AppliedRumorCount > playModeSocialLogAppliedBefore;
        bool listenerHeard = listener != null
            && listener.SocialMemory != null
            && listener.SocialMemory.RecentRumors.Count > 0;
        float listenerSentiment = listener != null && listener.SocialMemory != null && facility != null
            ? listener.SocialMemory.GetFacilitySentiment(facility)
            : 0f;
        float globalSentiment = facility != null ? runtime.GetGlobalFacilitySentiment(facility) : 0f;
        bool valid = playModeSocialLogEventTriggered
            && playModeSocialLogEventObserved
            && playModeSocialLogRequestAcceptedObserved
            && applied
            && listenerHeard
            && listenerSentiment < -0.05f
            && globalSentiment < -0.01f;
        float elapsed = Time.realtimeSinceStartup - playModeSocialLogStartedAt;
        bool finished = valid || elapsed > 45f;
        string report = "PLAY_SOCIAL_LOG_LLM_PROBE "
            + $"triggered={playModeSocialLogEventTriggered} "
            + $"eventObserved={playModeSocialLogEventObserved} "
            + $"requestAccepted={playModeSocialLogRequestAcceptedObserved} "
            + $"promptMarkerObserved={promptMarkerObserved} "
            + $"applied={applied} "
            + $"listenerHeard={listenerHeard} "
            + $"listenerSentiment={listenerSentiment:0.000} "
            + $"globalSentiment={globalSentiment:0.000} "
            + $"valid={valid} "
            + $"elapsed={elapsed:0.0} "
            + $"logEvents={runtime.ActorLogEventCountForDebug} "
            + $"lastLog={SafeDebugValue(runtime.LastActorLogDebug)} "
            + $"skip={SafeDebugValue(runtime.LastRequestSkipDebug)} "
            + $"lastRumor={runtime.LastRumorDebug} "
            + $"lastError={runtime.LastError}";

        if (cleanupOnComplete && finished)
        {
            CleanupPlayModeSocialLogLlmProbe();
        }

        return report;
    }

    public static void CleanupPlayModeSocialLogLlmProbe()
    {
        DestroyProbeObject(playModeSocialLogSpeakerObject);
        DestroyProbeObject(playModeSocialLogListenerObject);
        DestroyProbeObject(playModeSocialLogFacilityObject);
        DestroyProbeObject(playModeSocialLogSpeakerData);
        DestroyProbeObject(playModeSocialLogListenerData);
        DestroyProbeObject(playModeSocialLogFacilityData);
        playModeSocialLogSpeakerObject = null;
        playModeSocialLogListenerObject = null;
        playModeSocialLogFacilityObject = null;
        playModeSocialLogSpeakerData = null;
        playModeSocialLogListenerData = null;
        playModeSocialLogFacilityData = null;
        playModeSocialLogEventTriggered = false;
        playModeSocialLogEventObserved = false;
        playModeSocialLogRequestAcceptedObserved = false;
        playModeSocialLogStartedAt = 0f;
        playModeSocialLogAppliedBefore = 0;
        SocialReputationRuntime runtime = FindSocialRuntimeInstance();
        if (runtime != null)
        {
            runtime.RestrictActorLogRequestsForDebug(null);
        }
    }

    public static string StartPlayModePersonaMacroLlmProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        CleanupPlayModePersonaMacroLlmProbe();
        LocalLlmRequestQueue queue = FindQueueInstance() ?? EnsureQueueInstance(out _);
        queue.ConfigureTimeoutsForDebug(45f, 45f, 45f, 20f);
        AiDirectorRuntime director = GetOrCreatePlayModeAiDirector(out playModePersonaMacroDirectorObject);
        if (director == null)
        {
            throw new System.InvalidOperationException("AiDirectorRuntime is missing in Play Mode.");
        }

        playModePersonaMacroPersonaObject = CreatePlayActorObject("PlayLlmPersonaCustomer");
        playModePersonaMacroMacroObject = CreatePlayActorObject("PlayLlmMacroActor");
        playModePersonaMacroPersonaData = CreateCharacterData(
            CharacterType.Customer,
            "Play Persona Customer",
            "Slime");
        playModePersonaMacroMacroData = CreateCharacterData(
            CharacterType.NPC,
            "Play Macro Actor",
            "Orc");

        CharacterActor personaActor = playModePersonaMacroPersonaObject.GetComponent<CharacterActor>();
        personaActor.EnsureRuntimeState();
        personaActor.Initialize(playModePersonaMacroPersonaData);
        personaActor.SetLifecycleState(CharacterLifecycleState.OnExpedition);
        playModePersonaMacroPersonaAccepted = personaActor.PersonaRuntime != null
            && personaActor.PersonaRuntime.PersonaRequestInProgress;

        CharacterActor macroActor = playModePersonaMacroMacroObject.GetComponent<CharacterActor>();
        macroActor.EnsureRuntimeState();
        macroActor.Initialize(playModePersonaMacroMacroData);
        macroActor.SetLifecycleState(CharacterLifecycleState.OnExpedition);
        macroActor.Stats.Stats[CharacterCondition.MOOD] = 10f;
        macroActor.Stats.Stats[CharacterCondition.HUNGER] = 100f;
        macroActor.Stats.Stats[CharacterCondition.SLEEP] = 100f;
        macroActor.Stats.Stats[CharacterCondition.FUN] = 100f;
        playModePersonaMacroMacroAccepted = director.RequestMacroGoal(macroActor);

        playModePersonaMacroStartedAt = Time.realtimeSinceStartup;
        return "PLAY_PERSONA_MACRO_LLM_PROBE "
            + $"personaAccepted={playModePersonaMacroPersonaAccepted} "
            + $"macroAccepted={playModePersonaMacroMacroAccepted} "
            + $"queued={queue.QueuedCount} running={queue.RunningCount}";
    }

    public static string GetPlayModePersonaMacroLlmProbeReport(bool cleanupOnComplete = true)
    {
        CharacterActor personaActor = playModePersonaMacroPersonaObject != null
            ? playModePersonaMacroPersonaObject.GetComponent<CharacterActor>()
            : null;
        CharacterActor macroActor = playModePersonaMacroMacroObject != null
            ? playModePersonaMacroMacroObject.GetComponent<CharacterActor>()
            : null;
        CustomerPersonaRuntime personaRuntime = personaActor != null
            ? personaActor.PersonaRuntime
            : null;
        CharacterMacroGoal macroGoal = macroActor != null && macroActor.Blackboard != null
            ? macroActor.Blackboard.ActiveMacroGoal
            : null;
        AiDirectorRuntime director = FindPlayModeAiDirector();
        LocalLlmRequestQueue queue = FindQueueInstance();

        bool personaGenerated = personaRuntime != null
            && personaRuntime.HasGeneratedPersona
            && personaRuntime.Persona != null
            && !string.IsNullOrWhiteSpace(personaRuntime.Persona.traitName);
        bool macroApplied = macroGoal != null
            && macroActor != null
            && macroActor.Blackboard != null
            && macroActor.Blackboard.HasActiveMacroGoal()
            && macroGoal.type != CharacterMacroGoalType.None
            && string.Equals(macroGoal.source, "LocalLLM", System.StringComparison.Ordinal);
        bool macroObserved = director != null
            && macroActor != null
            && director.LastAppliedMacroGoalType != CharacterMacroGoalType.None
            && string.Equals(
                director.LastAppliedMacroActorName,
                macroActor.name,
                System.StringComparison.Ordinal);
        bool directorPromptRecorded = director != null
            && !string.IsNullOrWhiteSpace(director.LastRequestDebug)
            && director.LastRequestDebug.Contains("one JSON object", System.StringComparison.Ordinal);
        bool valid = playModePersonaMacroPersonaAccepted
            && playModePersonaMacroMacroAccepted
            && personaGenerated
            && (macroApplied || macroObserved)
            && directorPromptRecorded;
        float elapsed = Time.realtimeSinceStartup - playModePersonaMacroStartedAt;
        bool finished = valid || elapsed > 35f;
        string report = "PLAY_PERSONA_MACRO_LLM_PROBE "
            + $"personaAccepted={playModePersonaMacroPersonaAccepted} "
            + $"personaGenerated={personaGenerated} "
            + $"trait={SafeDebugValue(personaRuntime != null ? personaRuntime.Persona.traitName : string.Empty)} "
            + $"macroAccepted={playModePersonaMacroMacroAccepted} "
            + $"macroApplied={macroApplied} "
            + $"macroObserved={macroObserved} "
            + $"macroGoal={(macroGoal != null ? macroGoal.type.ToString() : "None")} "
            + $"macroSource={SafeDebugValue(macroGoal != null ? macroGoal.source : string.Empty)} "
            + $"lastAppliedMacro={(director != null ? director.LastAppliedMacroGoalType.ToString() : "None")} "
            + $"lastAppliedActor={SafeDebugValue(director != null ? director.LastAppliedMacroActorName : string.Empty)} "
            + $"lastAppliedDebug={SafeDebugValue(director != null ? director.LastAppliedMacroGoalDebug : string.Empty)} "
            + $"promptRecorded={directorPromptRecorded} "
            + $"valid={valid} "
            + $"elapsed={elapsed:0.0} "
            + $"personaError={SafeDebugValue(personaRuntime != null ? personaRuntime.LastError : string.Empty)} "
            + $"directorError={SafeDebugValue(director != null ? director.LastError : string.Empty)} "
            + $"queueError={SafeDebugValue(queue != null ? queue.LastError : string.Empty)}";

        if (cleanupOnComplete && finished)
        {
            CleanupPlayModePersonaMacroLlmProbe();
        }

        return report;
    }

    public static void CleanupPlayModePersonaMacroLlmProbe()
    {
        DestroyProbeObject(playModePersonaMacroPersonaObject);
        DestroyProbeObject(playModePersonaMacroMacroObject);
        DestroyProbeObject(playModePersonaMacroDirectorObject);
        DestroyProbeObject(playModePersonaMacroPersonaData);
        DestroyProbeObject(playModePersonaMacroMacroData);
        playModePersonaMacroPersonaObject = null;
        playModePersonaMacroMacroObject = null;
        playModePersonaMacroDirectorObject = null;
        playModePersonaMacroPersonaData = null;
        playModePersonaMacroMacroData = null;
        playModePersonaMacroPersonaAccepted = false;
        playModePersonaMacroMacroAccepted = false;
        playModePersonaMacroStartedAt = 0f;
    }

    public static string StartPlayModeBubbleLlmProbe()
    {
        if (!Application.isPlaying)
        {
            throw new System.InvalidOperationException("Unity is not in Play Mode.");
        }

        LocalLlmRequestQueue queue = FindQueueInstance()
            ?? EnsureQueueInstance(out _);
        queue.ConfigureTimeoutsForDebug(45f, 45f, 45f, 20f);

        if (queue.QueuedCount > 0 || queue.RunningCount > 0)
        {
            playModeBubbleAccepted = false;
            return "PLAY_BUBBLE_LLM_PROBE accepted=false queueBusy=true "
                + $"queued={queue.QueuedCount} running={queue.RunningCount}";
        }

        queue.ConfigureBubblePolicyForDebug(20f, 30f);
        CleanupPlayModeBubbleLlmProbe();
        playModeBubbleObject = CreatePlayActorObject("PlayLlmBubbleActor");
        playModeBubbleData = CreateCharacterData(CharacterType.NPC, "Play Bubble Actor", "Slime");

        CharacterActor actor = playModeBubbleObject.GetComponent<CharacterActor>();
        actor.EnsureRuntimeState();
        actor.Initialize(playModeBubbleData);
        actor.SetLifecycleState(CharacterLifecycleState.Active);

        int queuedBefore = queue.QueuedCount;
        int runningBefore = queue.RunningCount;
        playModeBubbleOriginalLine =
            "AI failure: no path to destination occupied; customer is unhappy.";
        actor.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Wait,
            CharacterActivityOutcomes.Failed,
            playModeBubbleOriginalLine,
            actionId: "probe:bubble",
            reasonCode: "destination-occupied",
            sentiment: -0.6f,
            bubbleEligible: true));
        playModeBubbleAccepted = queue.QueuedCount > queuedBefore
            || queue.RunningCount > runningBefore;
        playModeBubbleStartedAt = Time.realtimeSinceStartup;

        return "PLAY_BUBBLE_LLM_PROBE "
            + $"accepted={playModeBubbleAccepted} "
            + $"queued={queue.QueuedCount} running={queue.RunningCount}";
    }

    public static string GetPlayModeBubbleLlmProbeReport(bool cleanupOnComplete = true)
    {
        CharacterActor actor = playModeBubbleObject != null
            ? playModeBubbleObject.GetComponent<CharacterActor>()
            : null;
        CharacterDialogueRuntime dialogue = actor != null
            ? actor.DialogueRuntime
            : null;
        LocalLlmRequestQueue queue = FindQueueInstance();
        string generated = dialogue != null ? dialogue.LastGeneratedBubbleLine : string.Empty;
        bool generatedLine = !string.IsNullOrWhiteSpace(generated);
        bool lengthValid = generatedLine && generated.Length <= 80;
        bool noOriginalFallback = generatedLine
            && !string.Equals(generated, playModeBubbleOriginalLine, System.StringComparison.Ordinal);
        bool noError = dialogue != null && string.IsNullOrWhiteSpace(dialogue.LastError);
        bool valid = playModeBubbleAccepted
            && generatedLine
            && lengthValid
            && noOriginalFallback
            && noError;
        float elapsed = Time.realtimeSinceStartup - playModeBubbleStartedAt;
        bool finished = valid
            || elapsed > 35f
            || (dialogue != null && !string.IsNullOrWhiteSpace(dialogue.LastError));
        string report = "PLAY_BUBBLE_LLM_PROBE "
            + $"accepted={playModeBubbleAccepted} "
            + $"generated={generatedLine} "
            + $"lengthValid={lengthValid} "
            + $"noOriginalFallback={noOriginalFallback} "
            + $"visible={!string.IsNullOrWhiteSpace(dialogue != null ? dialogue.LastBubbleLine : string.Empty)} "
            + $"valid={valid} "
            + $"elapsed={elapsed:0.0} "
            + $"line={SafeDebugValue(generated)} "
            + $"dialogueError={SafeDebugValue(dialogue != null ? dialogue.LastError : string.Empty)} "
            + $"queueError={SafeDebugValue(queue != null ? queue.LastError : string.Empty)}";

        if (cleanupOnComplete && finished)
        {
            CleanupPlayModeBubbleLlmProbe();
        }

        return report;
    }

    public static void CleanupPlayModeBubbleLlmProbe()
    {
        DestroyProbeObject(playModeBubbleObject);
        DestroyProbeObject(playModeBubbleData);
        playModeBubbleObject = null;
        playModeBubbleData = null;
        playModeBubbleAccepted = false;
        playModeBubbleStartedAt = 0f;
        playModeBubbleOriginalLine = string.Empty;
    }

    private static void OnPlayModeSocialLlmProbeResult(LocalLlmResult result)
    {
        playModeSocialLlmProbeCompleted = true;
        playModeSocialLlmProbeStatus = result.Status;
        if (!result.IsSuccess)
        {
            playModeSocialLlmProbeReport =
                $"PLAY_LLM_SOCIAL_PROBE accepted=true completed=true status={result.Status} error={result.Error}";
            return;
        }

        bool parsed = LlmJsonResponseParser.TryParse(
            result.Content,
            out SocialRumorJsonDto dto,
            out string parseError);
        playModeSocialLlmProbeReport = parsed
            ? $"PLAY_LLM_SOCIAL_PROBE accepted=true completed=true status={result.Status} parsed=true rumorType={dto.rumorType} targetType={dto.targetType} sentiment={dto.sentiment:0.000}"
            : $"PLAY_LLM_SOCIAL_PROBE accepted=true completed=true status={result.Status} parsed=false error={parseError} content={result.Content}";
    }

    private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (!scenario())
            {
                errors.Add(name);
            }
        }
        catch (System.Exception exception)
        {
            errors.Add($"{name}: {exception.Message}");
        }
    }

    private static bool VerifyLlmJsonParser()
    {
        string valid = "{\"macroGoal\":\"SeekFood\",\"reason\":\"hungry\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"Meal\",\"validSeconds\":10}";
        string fenced = "```json\n{\"line\":\"Need food.\"}\n```";
        string trailingComma = "prefix {\"line\":\"Need rest.\",} suffix";
        string invalid = "{\"macroGoal\":\"InventNewGoal\",\"reason\":\"bad\",\"validSeconds\":10}";
        string missingTarget = "{\"macroGoal\":\"Vandalize\",\"reason\":\"angry\",\"validSeconds\":10}";
        string zeroValidSeconds = "{\"macroGoal\":\"SeekFood\",\"reason\":\"hungry\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"Meal\",\"validSeconds\":0}";
        string missingReason = "{\"macroGoal\":\"SeekFood\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"Meal\",\"validSeconds\":10}";

        bool validParsed = LlmJsonResponseParser.TryParse(valid, out MacroGoalJsonDto macro, out _)
            && macro.macroGoal == "SeekFood";
        bool fencedParsed = LlmJsonResponseParser.TryParse(fenced, out BubbleLineJsonDto bubble, out _)
            && bubble.line == "Need food.";
        bool trailingParsed = LlmJsonResponseParser.TryParse(trailingComma, out BubbleLineJsonDto trailing, out _)
            && trailing.line == "Need rest.";
        bool invalidRejected = !LlmJsonResponseParser.TryParse(invalid, out MacroGoalJsonDto _, out string error)
            && !string.IsNullOrWhiteSpace(error);
        bool missingTargetRejected = !LlmJsonResponseParser.TryParse(missingTarget, out MacroGoalJsonDto _, out string targetError)
            && targetError.Contains("targetFacility", System.StringComparison.Ordinal);
        bool zeroValidSecondsRejected = !LlmJsonResponseParser.TryParse(zeroValidSeconds, out MacroGoalJsonDto _, out string zeroError)
            && zeroError.Contains("validSeconds", System.StringComparison.Ordinal);
        bool missingReasonRejected = !LlmJsonResponseParser.TryParse(missingReason, out MacroGoalJsonDto _, out string reasonError)
            && reasonError.Contains("reason", System.StringComparison.Ordinal);

        return validParsed
            && fencedParsed
            && trailingParsed
            && invalidRejected
            && missingTargetRejected
            && zeroValidSecondsRejected
            && missingReasonRejected;
    }

    private static bool VerifySocialRumorJsonParser()
    {
        string valid = "{\"rumorType\":\"Recommendation\",\"targetType\":\"Facility\",\"targetFacilityId\":120,\"targetFacilityTag\":\"Meal\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":0.7,\"summary\":\"meal was quick\",\"spreadChance\":0.8,\"trustImpact\":0.1,\"validSeconds\":600}";
        string none = "{\"rumorType\":\"None\",\"targetType\":\"None\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":0,\"summary\":\"\",\"spreadChance\":0,\"trustImpact\":0,\"validSeconds\":0}";
        string characterTarget = "{\"rumorType\":\"Complaint\",\"targetType\":\"Character\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"targetCharacterId\":\"world:test:777201\",\"targetCharacterName\":\"Rude Guest\",\"sentiment\":-0.65,\"summary\":\"cut in line\",\"spreadChance\":0.9,\"trustImpact\":0.2,\"validSeconds\":600}";
        string invalidSentiment = "{\"rumorType\":\"Warning\",\"targetType\":\"Facility\",\"targetFacilityId\":120,\"targetFacilityTag\":\"Meal\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":2,\"summary\":\"bad\",\"spreadChance\":0.5,\"trustImpact\":0,\"validSeconds\":600}";
        string missingFacility = "{\"rumorType\":\"Complaint\",\"targetType\":\"Facility\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":-0.5,\"summary\":\"bad\",\"spreadChance\":0.5,\"trustImpact\":0,\"validSeconds\":600}";
        string lowSpreadChance = "{\"rumorType\":\"Warning\",\"targetType\":\"Facility\",\"targetFacilityId\":120,\"targetFacilityTag\":\"Meal\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":-0.5,\"summary\":\"bad\",\"spreadChance\":0.01,\"trustImpact\":0,\"validSeconds\":600}";

        bool validParsed = LlmJsonResponseParser.TryParse(valid, out SocialRumorJsonDto dto, out _)
            && dto.rumorType == "Recommendation"
            && dto.targetFacilityId == 120;
        bool noneParsed = LlmJsonResponseParser.TryParse(none, out SocialRumorJsonDto noneDto, out _)
            && noneDto.rumorType == "None";
        bool characterParsed = LlmJsonResponseParser.TryParse(characterTarget, out SocialRumorJsonDto characterDto, out _)
            && characterDto.targetType == "Character"
            && characterDto.targetCharacterId == "world:test:777201"
            && characterDto.targetFacilityId == -1;
        bool invalidRejected = !LlmJsonResponseParser.TryParse(invalidSentiment, out SocialRumorJsonDto _, out string sentimentError)
            && sentimentError.Contains("sentiment", System.StringComparison.Ordinal);
        bool missingRejected = !LlmJsonResponseParser.TryParse(missingFacility, out SocialRumorJsonDto _, out string targetError)
            && targetError.Contains("targetFacility", System.StringComparison.Ordinal);
        bool lowSpreadRejected = !LlmJsonResponseParser.TryParse(lowSpreadChance, out SocialRumorJsonDto _, out string spreadError)
            && spreadError.Contains("spreadChance", System.StringComparison.Ordinal);

        return validParsed
            && noneParsed
            && characterParsed
            && invalidRejected
            && missingRejected
            && lowSpreadRejected;
    }

    private static bool VerifyBlackboardCooldown()
    {
        GameObject actorObject = CreateActorObject("BlackboardCooldownActor");
        GameObject buildingObject = new GameObject("BlackboardCooldownFacility", typeof(BuildableObject));
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            CharacterBlackboard blackboard = actor.Blackboard;
            BuildableObject building = buildingObject.GetComponent<BuildableObject>();
            blackboard.PutFacilityOnCooldown(building, "test");
            return blackboard.IsFacilityCoolingDown(building, out float remaining) && remaining > 0f;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(buildingObject);
        }
    }

    private static bool VerifySocialRumorSpreadAndReputation()
    {
        SocialReputationRuntime runtime = EnsureSocialRuntimeInstance(out GameObject runtimeObject);
        GameObject speakerObject = CreateActorObject("RumorSpeaker");
        GameObject listenerObject = CreateActorObject("RumorListener");
        GameObject buildingObject = new GameObject("RumorTargetFacility", typeof(BuildableObject));
        CharacterSO speakerData = CreateCharacterData(CharacterType.Customer, "Speaker", "Slime");
        CharacterSO listenerData = CreateCharacterData(CharacterType.Customer, "Listener", "Slime");
        BuildingSO buildingData = CreateBuildingData(777001, "Rumor Test Rest");
        try
        {
            runtime.ClearForDebug();
            CharacterActor speaker = speakerObject.GetComponent<CharacterActor>();
            CharacterActor listener = listenerObject.GetComponent<CharacterActor>();
            speaker.EnsureRuntimeState();
            listener.EnsureRuntimeState();
            speaker.Initialize(speakerData);
            listener.Initialize(listenerData);
            speaker.SetLifecycleState(CharacterLifecycleState.Active);
            listener.SetLifecycleState(CharacterLifecycleState.Active);
            listenerObject.transform.position = speakerObject.transform.position + Vector3.right;

            BuildableObject building = buildingObject.GetComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.Initialization(buildingData, Vector2Int.zero);
            SocialRumor rumor = new SocialRumor
            {
                type = SocialRumorType.Warning,
                targetType = SocialRumorTargetType.Facility,
                targetFacilityId = buildingData.id,
                targetFacilityTag = string.Empty,
                sentiment = -0.9f,
                spreadChance = 1f,
                trustImpact = 0.2f,
                validUntil = Time.time + 600f,
                summary = "rest room was blocked",
                source = "Test"
            };

            bool applied = runtime.ApplyRumor(rumor, speaker);
            float listenerSentiment = listener.SocialMemory.GetFacilitySentiment(building);
            float globalSentiment = runtime.GetGlobalFacilitySentiment(building);
            bool valid = applied
                && listener.SocialMemory.RecentRumors.Count == 1
                && listenerSentiment < -0.2f
                && globalSentiment < -0.2f
                && runtime.AppliedRumorCount == 1;
            if (!valid)
            {
                Debug.LogError(
                    "Social rumor spread detail: "
                    + $"applied={applied}; listeners={CharacterAiEditorTestDependencies.WorldRegistry.Characters.Count}; "
                    + $"recent={listener.SocialMemory.RecentRumors.Count}; "
                    + $"listenerSentiment={listenerSentiment:0.###}; globalSentiment={globalSentiment:0.###}; "
                    + $"appliedCount={runtime.AppliedRumorCount}; heardCount={runtime.HeardRumorCount}; "
                    + $"runtime={runtime.LastRumorDebug}");
            }

            return valid;
        }
        finally
        {
            Object.DestroyImmediate(speakerObject);
            Object.DestroyImmediate(listenerObject);
            Object.DestroyImmediate(buildingObject);
            Object.DestroyImmediate(speakerData);
            Object.DestroyImmediate(listenerData);
            Object.DestroyImmediate(buildingData);
            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }
        }
    }

    private static bool VerifySocialRelationshipAndSourceTrust()
    {
        SocialReputationRuntime runtime = EnsureSocialRuntimeInstance(out GameObject runtimeObject);
        GameObject speakerObject = CreateActorObject("RelationshipRumorSpeaker");
        GameObject listenerObject = CreateActorObject("RelationshipRumorListener");
        GameObject targetObject = CreateActorObject("RelationshipRumorTarget");
        GameObject unrelatedFacilityObject = new GameObject("RelationshipRumorUnrelatedFacility", typeof(BuildableObject));
        CharacterSO speakerData = CreateCharacterData(CharacterType.Customer, "Trusted Speaker", "Slime");
        CharacterSO listenerData = CreateCharacterData(CharacterType.Customer, "Relationship Listener", "Slime");
        CharacterSO targetData = CreateCharacterData(CharacterType.Customer, "Rude Guest", "Imp");
        BuildingSO unrelatedFacilityData = CreateBuildingData(777204, "Relationship Unrelated Rest");
        speakerData.id = 777201;
        listenerData.id = 777202;
        targetData.id = 777203;
        try
        {
            runtime.ClearForDebug();
            CharacterActor speaker = speakerObject.GetComponent<CharacterActor>();
            CharacterActor listener = listenerObject.GetComponent<CharacterActor>();
            CharacterActor target = targetObject.GetComponent<CharacterActor>();
            speaker.EnsureRuntimeState();
            listener.EnsureRuntimeState();
            target.EnsureRuntimeState();
            speaker.Initialize(speakerData);
            listener.Initialize(listenerData);
            target.Initialize(targetData);
            target.Identity.SetPersistentId("character:world:test:target-playmode");
            speaker.SetLifecycleState(CharacterLifecycleState.Active);
            listener.SetLifecycleState(CharacterLifecycleState.Active);
            target.SetLifecycleState(CharacterLifecycleState.Active);
            speakerObject.transform.position = Vector3.zero;
            listenerObject.transform.position = Vector3.right;
            targetObject.transform.position = Vector3.right * 2f;
            BuildableObject unrelatedFacility = unrelatedFacilityObject.GetComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(unrelatedFacility);
            unrelatedFacility.Initialization(unrelatedFacilityData, Vector2Int.zero);

            bool applied = runtime.ApplyRumor(new SocialRumor
            {
                type = SocialRumorType.Complaint,
                targetType = SocialRumorTargetType.Character,
                targetCharacterId = target.Identity.PersistentId,
                targetCharacterName = targetData.characterName,
                sentiment = -0.75f,
                spreadChance = 1f,
                trustImpact = 0.6f,
                validUntil = Time.time + 600f,
                summary = "cut in line and blocked a room",
                source = "Test"
            }, speaker);

            float listenerRelationship = listener.SocialMemory.GetRelationshipSentiment(target);
            float listenerSourceTrust = listener.SocialMemory.GetSourceTrust(speaker);
            float speakerRelationship = speaker.SocialMemory.GetRelationshipSentiment(target);
            bool facilityReputationUntouched = runtime.AppliedRumorCount == 1
                && runtime.GetGlobalFacilitySentiment(unrelatedFacility) == 0f;
            bool valid = applied
                && listener.SocialMemory.RecentRumors.Count >= 1
                && listenerRelationship < -0.2f
                && speakerRelationship < -0.2f
                && listenerSourceTrust > 1.05f
                && facilityReputationUntouched;
            if (!valid)
            {
                Debug.LogError(
                    "Social relationship rumor detail: "
                    + $"applied={applied}; listeners={CharacterAiEditorTestDependencies.WorldRegistry.Characters.Count}; "
                    + $"recent={listener.SocialMemory.RecentRumors.Count}; "
                    + $"listenerRelationship={listenerRelationship:0.###}; "
                    + $"speakerRelationship={speakerRelationship:0.###}; "
                    + $"sourceTrust={listenerSourceTrust:0.###}; "
                    + $"facilityUntouched={facilityReputationUntouched}; "
                    + $"heardCount={runtime.HeardRumorCount}; runtime={runtime.LastRumorDebug}");
            }

            return valid;
        }
        finally
        {
            Object.DestroyImmediate(speakerObject);
            Object.DestroyImmediate(listenerObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(unrelatedFacilityObject);
            Object.DestroyImmediate(speakerData);
            Object.DestroyImmediate(listenerData);
            Object.DestroyImmediate(targetData);
            Object.DestroyImmediate(unrelatedFacilityData);
            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }
        }
    }

    private static bool VerifySocialReputationAffectsFacilityScore()
    {
        SocialReputationRuntime runtime = EnsureSocialRuntimeInstance(out GameObject runtimeObject);
        GameObject actorObject = CreateActorObject("RumorUtilityActor");
        GameObject goodObject = new GameObject("GoodRumorFacility", typeof(BuildableObject));
        GameObject badObject = new GameObject("BadRumorFacility", typeof(BuildableObject));
        CharacterSO actorData = CreateCharacterData(CharacterType.Customer, "Utility Listener", "Slime");
        BuildingSO goodData = CreateBuildingData(777101, "Praised Rest Facility");
        BuildingSO badData = CreateBuildingData(777102, "Warned Rest Facility");
        try
        {
            runtime.ClearForDebug();
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(actorData);
            actor.SetLifecycleState(CharacterLifecycleState.Active);

            BuildableObject good = goodObject.GetComponent<BuildableObject>();
            BuildableObject bad = badObject.GetComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(good);
            CharacterAiEditorTestDependencies.Inject(bad);
            good.Initialization(goodData, Vector2Int.zero);
            bad.Initialization(badData, Vector2Int.right);

            runtime.ApplyRumor(new SocialRumor
            {
                type = SocialRumorType.Praise,
                targetType = SocialRumorTargetType.Facility,
                targetFacilityId = goodData.id,
                targetFacilityTag = string.Empty,
                sentiment = 0.9f,
                spreadChance = 0f,
                trustImpact = 0.1f,
                validUntil = Time.time + 600f,
                summary = "rest was comfortable",
                source = "Test"
            }, actor);
            runtime.ApplyRumor(new SocialRumor
            {
                type = SocialRumorType.Complaint,
                targetType = SocialRumorTargetType.Facility,
                targetFacilityId = badData.id,
                targetFacilityTag = string.Empty,
                sentiment = -0.9f,
                spreadChance = 0f,
                trustImpact = 0.1f,
                validUntil = Time.time + 600f,
                summary = "rest was uncomfortable",
                source = "Test"
            }, actor);

            FacilityScoringContext scoringContext = CreateSocialScoringContext(runtime);
            float goodScore = FacilityCandidateScorer.ScoreCandidate(
                actor,
                good,
                FacilityRole.Rest,
                null,
                scoringContext);
            float badScore = FacilityCandidateScorer.ScoreCandidate(
                actor,
                bad,
                FacilityRole.Rest,
                null,
                scoringContext);
            return goodScore > badScore
                && runtime.GetCombinedFacilitySentiment(actor, good) > 0f
                && runtime.GetCombinedFacilitySentiment(actor, bad) < 0f;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(goodObject);
            Object.DestroyImmediate(badObject);
            Object.DestroyImmediate(actorData);
            Object.DestroyImmediate(goodData);
            Object.DestroyImmediate(badData);
            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }
        }
    }

    private static bool VerifyRimWorldStyleBtUtilityResponsibility()
    {
        LocalLlmRequestQueue queue = EnsureQueueInstance(out GameObject queueObject);
        GameObject gridObject = EnsureGridForScenario(out bool createdGrid);
        GameObject actorObject = CreateActorObject("RimWorldStyleUtilityActor");
        actorObject.AddComponent<AbilityWork>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actorObject.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "RimWorld Utility Customer", "Slime");
        ProbeExitDungeonActionSet lowExitActionSet = ScriptableObject.CreateInstance<ProbeExitDungeonActionSet>();
        lowExitActionSet.actionName = "Probe Low Exit";
        lowExitActionSet.probeScore = 0.2f;
        ProbeEatActionSet highFoodActionSet = ScriptableObject.CreateInstance<ProbeEatActionSet>();
        highFoodActionSet.actionName = "Probe High Food";
        highFoodActionSet.probeScore = 1f;
        ProbeVolatileWorkActionSet selectedWorkActionSet = ScriptableObject.CreateInstance<ProbeVolatileWorkActionSet>();
        selectedWorkActionSet.actionName = "Probe Selected Work";
        selectedWorkActionSet.highScore = 0.95f;
        selectedWorkActionSet.laterScore = 0.2f;
        selectedWorkActionSet.highScoreEvaluationCount = 1;
        ProbeWorkActionSet competingWorkActionSet = ScriptableObject.CreateInstance<ProbeWorkActionSet>();
        competingWorkActionSet.actionName = "Probe Competing Work";
        competingWorkActionSet.probeScore = 0.6f;
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 100f;
            actor.Brain.availableActions = new[]
            {
                new AIAction { actionset = lowExitActionSet },
                new AIAction { actionset = highFoodActionSet },
                new AIAction { actionset = selectedWorkActionSet },
                new AIAction { actionset = competingWorkActionSet }
            };
            actor.Brain.RequestImmediateReplan(clearFailures: true);

            BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
            CharacterAiBehaviorDesignerGraphBuilder.EnsureVisualGraph(tree);
            bool handled = tree.DungeonStoryManualTick(actor);
            AIAction selected = actor.Brain.bestAction;
            bool hasWorkUtility = actor.Blackboard.JobGiverUtilityScores.TryGetValue(
                CharacterAiBranch.Work,
                out float workUtility);
            bool hasSurvivalPriority = actor.Blackboard.RoutineGroupPriorityScores.TryGetValue(
                CharacterAiBranch.SurvivalNeeds,
                out float survivalPriority);
            bool hasDutyPriority = actor.Blackboard.RoutineGroupPriorityScores.TryGetValue(
                CharacterAiBranch.DutyWork,
                out float dutyPriority);
            bool valid = queue != null
                && handled
                && actor.Blackboard.CurrentBranch == CharacterAiBranch.RoutineUtility
                && actor.Blackboard.CurrentTask == "Run Routine Utility"
                && actor.Blackboard.CurrentIntent == "Probe Selected Work"
                && hasSurvivalPriority
                && hasDutyPriority
                && dutyPriority > survivalPriority
                && hasWorkUtility
                && workUtility > 0f
                && selectedWorkActionSet.ScoreRequestCount == 2
                && selected != null
                && selected.actionset == selectedWorkActionSet
                && tree.DungeonStoryBranch == CharacterAiBranch.RoutineUtility.ToString();
            if (!valid)
            {
                Debug.LogError(
                    "RimWorld-style JobGiver probe failed: "
                    + $"handled={handled} branch={actor.Blackboard.CurrentBranch} "
                    + $"task={actor.Blackboard.CurrentTask} intent={actor.Blackboard.CurrentIntent} "
                    + $"selected={selected?.actionset?.actionName ?? "None"} "
                    + $"survivalPriority={(hasSurvivalPriority ? survivalPriority : -1f):0.###} "
                    + $"dutyPriority={(hasDutyPriority ? dutyPriority : -1f):0.###} "
                    + $"workUtility={(hasWorkUtility ? workUtility : -1f):0.###} "
                    + $"volatileScoreRequests={selectedWorkActionSet.ScoreRequestCount} "
                    + $"selectedJobGiver={actor.Blackboard.SelectedJobGiverUtilitySummary}");
            }

            return valid;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(lowExitActionSet);
            Object.DestroyImmediate(highFoodActionSet);
            Object.DestroyImmediate(selectedWorkActionSet);
            Object.DestroyImmediate(competingWorkActionSet);
            if (createdGrid && gridObject != null)
            {
                Object.DestroyImmediate(gridObject);
            }

            if (queueObject != null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyNoFlatUtilitySelectionApi()
    {
        BindingFlags flags = BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.Public
            | BindingFlags.NonPublic;
        return typeof(AIBrain).GetMethod("TrySelectAction", flags) == null
            && typeof(AIBrain).GetMethod("TryGetBestActionUtility", flags) == null
            && typeof(CharacterAiDecisionPipeline).GetMethod("SelectAction", flags) == null
            && typeof(CharacterAiDecisionPipeline).GetMethod("RunUtilityDecision", flags) == null;
    }

    private static bool VerifyMoodImpulseJsonParser()
    {
        string valid = "{\"moodImpulse\":\"SeekFun\",\"strength\":0.7,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"bored and cranky\",\"validSeconds\":30}";
        string stringStrength = "{\"moodImpulse\":\"SeekFun\",\"strength\":\"0.7\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"bad numeric contract\",\"validSeconds\":30}";
        string invalidImpulse = "{\"moodImpulse\":\"InventedImpulse\",\"strength\":0.7,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"bad enum\",\"validSeconds\":30}";
        string missingTarget = "{\"moodImpulse\":\"Vandalize\",\"strength\":0.8,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"angry\",\"validSeconds\":30}";
        string missingReason = "{\"moodImpulse\":\"IgnoreDuty\",\"strength\":0.6,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"validSeconds\":30}";

        bool validParsed = LlmJsonResponseParser.TryParse(valid, out MoodImpulseJsonDto dto, out _)
            && dto.moodImpulse == "SeekFun"
            && dto.ToRuntimeImpulse("Test", 0f).type == CharacterMoodImpulseType.SeekFun;
        bool stringStrengthRejected = !LlmJsonResponseParser.TryParse(
            stringStrength,
            out MoodImpulseJsonDto _,
            out string strengthError)
            && strengthError.Contains("strength", System.StringComparison.Ordinal);
        bool invalidRejected = !LlmJsonResponseParser.TryParse(
            invalidImpulse,
            out MoodImpulseJsonDto _,
            out string enumError)
            && enumError.Contains("Unsupported", System.StringComparison.Ordinal);
        bool missingTargetRejected = !LlmJsonResponseParser.TryParse(
            missingTarget,
            out MoodImpulseJsonDto _,
            out string targetError)
            && targetError.Contains("targetFacility", System.StringComparison.Ordinal);
        bool missingReasonRejected = !LlmJsonResponseParser.TryParse(
            missingReason,
            out MoodImpulseJsonDto _,
            out string reasonError)
            && reasonError.Contains("reason", System.StringComparison.Ordinal);

        return validParsed
            && stringStrengthRejected
            && invalidRejected
            && missingTargetRejected
            && missingReasonRejected;
    }

    private static bool VerifyMoodImpulseBiasesJobGiverUtility()
    {
        GameObject actorObject = CreateActorObject("MoodImpulseBiasActor");
        actorObject.AddComponent<AbilityWork>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actorObject.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "Mood Bias Worker", "Slime");
        data.aiPersonality.diligence = 1.4f;
        ProbeWorkActionSet workActionSet = ScriptableObject.CreateInstance<ProbeWorkActionSet>();
        workActionSet.actionName = "Probe Mood Work";
        workActionSet.probeScore = 1f;
        ProbeWaitActionSet waitActionSet = ScriptableObject.CreateInstance<ProbeWaitActionSet>();
        waitActionSet.actionName = "Probe Mood Wait";
        waitActionSet.probeScore = 1f;
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 50f;
            actor.Brain.availableActions = new[]
            {
                new AIAction { actionset = workActionSet },
                new AIAction { actionset = waitActionSet }
            };

            float normalWorkMultiplier = CharacterAiPersonalityUtility.GetActionScoreMultiplier(actor, workActionSet);
            actor.stats[CharacterCondition.MOOD] = 100f;
            float goodMoodWorkMultiplier = CharacterAiPersonalityUtility.GetActionScoreMultiplier(actor, workActionSet);

            actor.stats[CharacterCondition.MOOD] = 40f;
            actor.Blackboard.SetMoodImpulse(new CharacterMoodImpulse
            {
                type = CharacterMoodImpulseType.IgnoreDuty,
                strength = 1f,
                reason = "wants to slack off",
                validUntil = Time.time + 30f,
                source = "Test"
            });

            bool workEvaluated = CharacterAiJobGiverRegistry.Work.TryEvaluate(
                actor,
                out CharacterAiJobCandidate workCandidate);
            bool waitEvaluated = CharacterAiJobGiverRegistry.Wait.TryEvaluate(
                actor,
                out CharacterAiJobCandidate waitCandidate);
            bool idlePriorityBiased = CharacterAiRoutinePriority.GetPriority(
                    actor,
                    CharacterAiBranch.Idle,
                    out string idleReason) > 40f
                && idleReason.Contains("moodImpulse", System.StringComparison.Ordinal);

            bool valid = goodMoodWorkMultiplier > normalWorkMultiplier
                && workEvaluated
                && waitEvaluated
                && waitCandidate.Utility > workCandidate.Utility
                && waitCandidate.Reason.Contains("moodImpulse", System.StringComparison.Ordinal)
                && idlePriorityBiased;

            if (!valid)
            {
                Debug.LogError(
                    "Mood impulse bias probe failed: "
                    + $"normalWorkMultiplier={normalWorkMultiplier:0.###} "
                    + $"goodMoodWorkMultiplier={goodMoodWorkMultiplier:0.###} "
                    + $"workEvaluated={workEvaluated} work={workCandidate.DebugSummary} "
                    + $"waitEvaluated={waitEvaluated} wait={waitCandidate.DebugSummary} "
                    + $"idleReason={idleReason}");
            }

            return valid;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(workActionSet);
            Object.DestroyImmediate(waitActionSet);
        }
    }

    private static bool VerifyMoodImpulseInterruptsCurrentAction()
    {
        GameObject actorObject = CreateActorObject("MoodImpulseInterruptActor");
        actorObject.AddComponent<AbilityWork>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actorObject.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "Mood Interrupt Worker", "Slime");
        ProbeContinuableWorkActionSet workActionSet = ScriptableObject.CreateInstance<ProbeContinuableWorkActionSet>();
        workActionSet.actionName = "Probe Interruptible Work";
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 30f;
            SerializedObject brainFixture = new SerializedObject(actor.Brain);
            brainFixture.FindProperty("defaultActionPersistenceSeconds").floatValue = 0f;
            brainFixture.FindProperty("actionTransitionCooldown").floatValue = 0f;
            brainFixture.ApplyModifiedPropertiesWithoutUndo();
            AIAction runningAction = new AIAction { actionset = workActionSet };
            runningAction.MarkStarted(Mathf.Max(0f, Time.time - 1f));
            actor.Brain.bestAction = runningAction;
            actor.Blackboard.SetMoodImpulse(new CharacterMoodImpulse
            {
                type = CharacterMoodImpulseType.IgnoreDuty,
                strength = 0.9f,
                reason = "too annoyed to work",
                validUntil = Time.time + 30f,
                source = "Test"
            });

            bool canContinue = actor.Brain.CanContinueCurrentAction(out string continueStatus);
            bool shouldStop = actor.Brain.ShouldStopCurrentActionForReplan(out string stopReason);
            bool hasImpulse = actor.Blackboard.HasActiveMoodImpulse();
            CharacterAiBranch runningBranch = CharacterMoodImpulseUtility.GetBranchForActionSet(workActionSet);
            bool directInterrupt = CharacterMoodImpulseUtility.ShouldInterruptCurrentAction(
                actor,
                runningAction,
                out string directReason);
            bool valid = !canContinue
                && shouldStop
                && continueStatus.Contains("Mood impulse", System.StringComparison.Ordinal)
                && stopReason.Contains("Mood impulse", System.StringComparison.Ordinal);

            if (!valid)
            {
                Debug.LogError(
                    "Mood impulse interrupt probe failed: "
                    + $"canContinue={canContinue} continueStatus={continueStatus} "
                    + $"shouldStop={shouldStop} stopReason={stopReason} "
                    + $"hasImpulse={hasImpulse} branch={runningBranch} "
                    + $"directInterrupt={directInterrupt} directReason={directReason}");
            }

            return valid;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(workActionSet);
        }
    }

    private static bool VerifyDirectorMoodImpulseTrigger()
    {
        LocalLlmRequestQueue queue = EnsureQueueInstance(out GameObject queueObject);
        GameObject actorObject = CreateActorObject("DirectorMoodImpulseActor");
        GameObject directorObject = new GameObject("DirectorMoodImpulseRuntime", typeof(AiDirectorRuntime));
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "Mood Impulse Customer", "Slime");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Stats.Stats[CharacterCondition.MOOD] = 45f;
            actor.Stats.Stats[CharacterCondition.HUNGER] = 100f;
            actor.Stats.Stats[CharacterCondition.SLEEP] = 100f;
            actor.Stats.Stats[CharacterCondition.FUN] = 100f;

            AiDirectorRuntime director = directorObject.GetComponent<AiDirectorRuntime>();
            CharacterAiEditorTestDependencies.Inject(director);
            queue.ClearForDebug();
            bool shouldBefore = director.ShouldRequestMoodImpulse(actor);
            bool accepted = director.RequestMoodImpulse(actor);
            bool cooldownBlocks = !director.ShouldRequestMoodImpulse(actor);
            bool promptContract = director.LastRequestDebug.Contains("moodImpulse", System.StringComparison.Ordinal)
                && director.LastRequestDebug.Contains("score bias", System.StringComparison.Ordinal)
                && director.LastRequestDebug.Contains("raw JSON numbers", System.StringComparison.Ordinal);

            MethodInfo onResult = typeof(AiDirectorRuntime)
                .GetMethod("OnMoodImpulseResult", BindingFlags.Instance | BindingFlags.NonPublic);
            onResult?.Invoke(
                director,
                new object[]
                {
                    actor,
                    new LocalLlmResult(
                        LocalLlmRequestStatus.Succeeded,
                        "{\"moodImpulse\":\"Wait\",\"strength\":0.65,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"low mood wants to pause\",\"validSeconds\":30}",
                        string.Empty,
                        string.Empty)
                });

            bool applied = actor.Blackboard.HasActiveMoodImpulse()
                && actor.Blackboard.ActiveMoodImpulse.type == CharacterMoodImpulseType.Wait
                && director.LastAppliedMoodImpulseType == CharacterMoodImpulseType.Wait;

            return queue != null
                && shouldBefore
                && accepted
                && queue.QueuedCount == 1
                && cooldownBlocks
                && promptContract
                && applied;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(directorObject);
            Object.DestroyImmediate(data);
            if (queue != null)
            {
                queue.ClearForDebug();
            }

            if (queueObject != null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyBtContinuesCurrentActionBeforeUtility()
    {
        GameObject gridObject = EnsureGridForScenario(out bool createdGrid);
        GameObject actorObject = CreateActorObject("BtContinueCurrentActor");
        actorObject.AddComponent<AbilityWork>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actorObject.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "BT Continue Worker", "Slime");
        ProbeContinuableWorkActionSet actionSet = ScriptableObject.CreateInstance<ProbeContinuableWorkActionSet>();
        actionSet.actionName = "Probe Continuing Work";
        actionSet.highScore = 0.95f;
        actionSet.laterScore = 0.01f;
        actionSet.highScoreEvaluationCount = 1;
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 100f;
            actor.Brain.availableActions = new[]
            {
                new AIAction { actionset = actionSet }
            };

            BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
            CharacterAiBehaviorDesignerGraphBuilder.EnsureVisualGraph(tree);
            ResetSchedulerPathBudgetForDebug();

            bool firstHandled = tree.DungeonStoryManualTick(actor);
            AIAction firstAction = actor.Brain.bestAction;
            int scoreRequestsAfterFirstTick = actionSet.ScoreRequestCount;
            bool firstSelectedWork = firstHandled
                && actor.Blackboard.CurrentBranch == CharacterAiBranch.RoutineUtility
                && actor.Blackboard.CurrentTask == "Run Routine Utility"
                && firstAction != null
                && firstAction.actionset == actionSet
                && firstAction.HasStarted;

            bool secondHandled = tree.DungeonStoryManualTick(actor);
            bool secondContinued = secondHandled
                && actor.Blackboard.CurrentBranch == CharacterAiBranch.LockedAction
                && actor.Blackboard.CurrentTask == "Locked Action"
                && actor.Brain.bestAction == firstAction
                && actionSet.ScoreRequestCount == scoreRequestsAfterFirstTick;

            if (!firstSelectedWork || !secondContinued)
            {
                Debug.LogError(
                    "BT continue-current probe failed: "
                    + $"firstHandled={firstHandled} firstBranch={actor.Blackboard.CurrentBranch} "
                    + $"firstTask={actor.Blackboard.CurrentTask} firstStarted={firstAction?.HasStarted.ToString() ?? "null"} "
                    + $"secondHandled={secondHandled} secondBranch={actor.Blackboard.CurrentBranch} "
                    + $"secondTask={actor.Blackboard.CurrentTask} scoreRequests={actionSet.ScoreRequestCount} "
                    + $"scoreAfterFirst={scoreRequestsAfterFirstTick}");
            }

            return firstSelectedWork && secondContinued;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(actionSet);
            if (createdGrid && gridObject != null)
            {
                Object.DestroyImmediate(gridObject);
            }
        }
    }

    private static bool VerifyBtStopsCurrentActionBeforeUtility()
    {
        GameObject gridObject = EnsureGridForScenario(out bool createdGrid);
        GameObject actorObject = CreateActorObject("BtStopCurrentActor");
        actorObject.AddComponent<AbilityWork>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actorObject.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "BT Stop Worker", "Slime");
        ProbeContinuableWorkActionSet actionSet = ScriptableObject.CreateInstance<ProbeContinuableWorkActionSet>();
        actionSet.actionName = "Probe Stoppable Work";
        actionSet.highScore = 0.95f;
        actionSet.laterScore = 0.01f;
        actionSet.highScoreEvaluationCount = 1;
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 100f;
            actor.Brain.availableActions = new[]
            {
                new AIAction { actionset = actionSet }
            };

            BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
            CharacterAiBehaviorDesignerGraphBuilder.EnsureVisualGraph(tree);
            ResetSchedulerPathBudgetForDebug();

            bool firstHandled = tree.DungeonStoryManualTick(actor);
            AIAction firstAction = actor.Brain.bestAction;
            int scoreRequestsAfterFirstTick = actionSet.ScoreRequestCount;
            actionSet.canContinue = false;

            bool secondHandled = tree.DungeonStoryManualTick(actor);
            AbilityWork work = actor.GetComponent<AbilityWork>();
            bool secondStopped = secondHandled
                && actor.Blackboard.CurrentBranch == CharacterAiBranch.InterruptCheck
                && actor.Blackboard.CurrentTask == "Interrupt Check"
                && actor.Brain.bestAction == null
                && actionSet.ScoreRequestCount == scoreRequestsAfterFirstTick
                && actionSet.StopCalled
                && work != null
                && !work.isWorking
                && actor.Blackboard.LastCommitBreakReason.Contains("CurrentActionStopped");

            if (!firstHandled || firstAction == null || !secondStopped)
            {
                Debug.LogError(
                    "BT stop-current probe failed: "
                    + $"firstHandled={firstHandled} firstAction={firstAction?.actionset?.actionName ?? "None"} "
                    + $"secondHandled={secondHandled} secondBranch={actor.Blackboard.CurrentBranch} "
                    + $"secondTask={actor.Blackboard.CurrentTask} bestAfterStop={actor.Brain.bestAction?.actionset?.actionName ?? "None"} "
                    + $"scoreRequests={actionSet.ScoreRequestCount} scoreAfterFirst={scoreRequestsAfterFirstTick} "
                    + $"stopCalled={actionSet.StopCalled} workIsWorking={work?.isWorking.ToString() ?? "null"} "
                    + $"commitBreak={actor.Blackboard.LastCommitBreakReason}");
            }

            return firstHandled
                && firstAction != null
                && firstAction.actionset == actionSet
                && secondStopped;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(actionSet);
            if (createdGrid && gridObject != null)
            {
                Object.DestroyImmediate(gridObject);
            }
        }
    }

    private static bool VerifyBtDecisionTraceRecordsRouting()
    {
        GameObject gridObject = EnsureGridForScenario(out bool createdGrid);
        GameObject actorObject = CreateActorObject("BtDecisionTraceActor");
        actorObject.AddComponent<AbilityWork>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actorObject.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "BT Trace Worker", "Slime");
        ProbeContinuableWorkActionSet actionSet = ScriptableObject.CreateInstance<ProbeContinuableWorkActionSet>();
        actionSet.actionName = "Probe Trace Work";
        actionSet.highScore = 0.95f;
        actionSet.laterScore = 0.01f;
        actionSet.highScoreEvaluationCount = 1;
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 100f;
            actor.Brain.availableActions = new[]
            {
                new AIAction { actionset = actionSet }
            };

            BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
            CharacterAiBehaviorDesignerGraphBuilder.EnsureVisualGraph(tree);
            ResetSchedulerPathBudgetForDebug();

            bool handled = tree.DungeonStoryManualTick(actor);
            string trace = actor.Blackboard.LastDecisionTrace;
            bool valid = handled
                && ContainsTrace(actor, "Utility Work")
                && ContainsTrace(actor, "Selected Work")
                && ContainsTrace(actor, "BT RoutineUtility/Run Routine Utility")
                && actor.Blackboard.LastDecisionRouteSummary.Contains("BT=RoutineUtility/Run Routine Utility")
                && actor.Blackboard.LastDecisionRouteSummary.Contains("Utility=WorkJobGiver");

            if (!valid)
            {
                Debug.LogError(
                    "BT decision trace probe failed: "
                    + $"handled={handled} branch={actor.Blackboard.CurrentBranch} "
                    + $"task={actor.Blackboard.CurrentTask} "
                    + $"route={actor.Blackboard.LastDecisionRouteSummary} trace={trace}");
            }

            return valid;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(actionSet);
            if (createdGrid && gridObject != null)
            {
                Object.DestroyImmediate(gridObject);
            }
        }
    }

    private static bool ContainsTrace(CharacterActor actor, string token)
    {
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        return blackboard != null
            && blackboard.RecentDecisionTrace != null
            && blackboard.RecentDecisionTrace.Any((entry) => entry != null && entry.Contains(token));
    }

    private static bool VerifyBehaviorTreeWrapperDebug()
    {
        GameObject actorObject = CreateActorObject("BehaviorTreeDebugActor");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
            if (tree == null)
            {
                tree = actorObject.AddComponent<BehaviorTree>();
            }

            CharacterAiBehaviorDesignerGraphBuilder.EnsureVisualGraph(tree);
            bool handled = tree.DungeonStoryManualTick(actor);
            return !handled
                && tree.ExternalBehavior != null
                && tree.DungeonStoryBranch == CharacterAiBranch.Idle.ToString()
                && !string.IsNullOrWhiteSpace(tree.DungeonStoryStatus);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyBehaviorTreeVisualGraph()
    {
        GameObject actorObject = CreateActorObject("BehaviorTreeVisualGraphActor");
        try
        {
            BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
            bool changed = CharacterAiBehaviorDesignerGraphBuilder.EnsureVisualGraph(tree);
            BehaviorDesigner.Runtime.BehaviorSource source = tree.GetBehaviorSource();
            ExternalBehaviorTree externalBehavior = tree.ExternalBehavior as ExternalBehaviorTree;
            BehaviorDesigner.Runtime.BehaviorSource externalSource =
                externalBehavior != null ? externalBehavior.BehaviorSource : null;
            ParentTask externalRoot = externalSource != null ? externalSource.RootTask as ParentTask : null;
            bool valid = changed
                && externalBehavior != null
                && externalSource != null
                && source != null
                && source.RootTask == null
                && source.TaskData == null
                && HasNodeLayout(externalSource.EntryTask, "Entry", new Vector2(0f, -260f))
                && HasVisualRootLayout(externalRoot)
                && HasNoLayoutOverlap(externalSource.EntryTask, externalRoot)
                && HasCompactLayoutBounds(2300f, 1400f, externalSource.EntryTask, externalRoot);
            if (!valid)
            {
                throw new System.InvalidOperationException(
                    "External BT layout invalid. "
                    + $"changed={changed}, "
                    + $"external={(externalBehavior != null ? externalBehavior.name : "null")}, "
                    + $"sourceRoot={NodeName(source != null ? source.RootTask : null)}, "
                    + $"sourceTaskData={(source != null && source.TaskData != null ? "set" : "null")}, "
                    + $"externalEntry={NodeName(externalSource != null ? externalSource.EntryTask : null)}, "
                    + $"externalRoot={NodeName(externalRoot)}, "
                    + $"externalRootChildren={(externalRoot != null && externalRoot.Children != null ? externalRoot.Children.Count.ToString() : "null")}");
            }

            return true;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyBehaviorTreeRuntimeBranchSelection()
    {
        GameObject actorObject = CreateActorObject("BehaviorTreeRuntimeBranchActor");
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "BT Runtime Customer", "Slime");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
            CharacterAiBehaviorDesignerGraphBuilder.EnsureVisualGraph(tree);

            actor.SetLifecycleState(CharacterLifecycleState.ExitingDungeon);
            bool criticalSelected = tree.DungeonStoryManualTick(actor)
                && actor.Blackboard.CurrentBranch == CharacterAiBranch.Critical
                && tree.DungeonStoryBranch == CharacterAiBranch.Critical.ToString();

            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Blackboard.SetMacroGoal(new CharacterMacroGoal
            {
                type = CharacterMacroGoalType.Complain,
                reason = "branch test",
                validUntil = Time.time + 10f,
                source = "Test"
            });

            bool macroSelected = tree.DungeonStoryManualTick(actor)
                && actor.Blackboard.CurrentBranch == CharacterAiBranch.MacroGoal
                && actor.Blackboard.CurrentTask == "Complain"
                && !actor.Blackboard.HasActiveMacroGoal();

            return criticalSelected && macroSelected;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
        }
    }

    private static bool VerifyMacroGoalsUseJobGiverCandidates()
    {
        GameObject gridObject = EnsureGridForScenario(out bool createdGrid);
        GameObject actorObject = CreateActorObject("MacroJobGiverActor");
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "Macro JobGiver Customer", "Slime");
        ProbeEatActionSet eatActionSet = ScriptableObject.CreateInstance<ProbeEatActionSet>();
        eatActionSet.actionName = "Macro Probe Eat";
        eatActionSet.probeScore = 0.8f;
        ProbeShoppingActionSet shoppingActionSet = ScriptableObject.CreateInstance<ProbeShoppingActionSet>();
        shoppingActionSet.actionName = "Macro Probe Shopping";
        shoppingActionSet.probeScore = 0.7f;
        ProbeLookAroundActionSet lookAroundActionSet = ScriptableObject.CreateInstance<ProbeLookAroundActionSet>();
        lookAroundActionSet.actionName = "Macro Probe Look Around";
        lookAroundActionSet.probeScore = 1f;
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            ResetSchedulerPathBudgetForDebug();
            actor.Brain.availableActions = new[]
            {
                new AIAction { actionset = eatActionSet },
                new AIAction { actionset = shoppingActionSet },
                new AIAction { actionset = lookAroundActionSet }
            };

            actor.stats[CharacterCondition.HUNGER] = 0f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 100f;
            actor.Blackboard.SetMacroGoal(new CharacterMacroGoal
            {
                type = CharacterMacroGoalType.SeekFood,
                reason = "macro food test",
                validUntil = Time.time + 10f,
                source = "Test"
            });

            ICharacterAiDecisionPipeline decisionPipeline = CreateDebugDecisionPipeline();
            CharacterAiDecisionTickResult foodResult =
                decisionPipeline.RunMacroGoalDecision(actor);
            bool foodSelected = foodResult.Handled
                && actor.Blackboard.CurrentBranch == CharacterAiBranch.MacroGoal
                && actor.Blackboard.CurrentTask == "Run Seek Food Macro Action"
                && actor.Brain.bestAction != null
                && actor.Brain.bestAction.actionset == eatActionSet
                && !actor.Blackboard.HasActiveMacroGoal()
                && actor.Blackboard.SelectedJobGiverUtilitySummary.Contains("GetFoodJobGiver");

            actor.Brain.isBestActionEnd = true;
            actor.Brain.ClearPathSearchCache();
            ResetSchedulerPathBudgetForDebug();
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.FUN] = 0f;
            actor.stats[CharacterCondition.MOOD] = 100f;
            actor.Blackboard.SetMacroGoal(new CharacterMacroGoal
            {
                type = CharacterMacroGoalType.SeekFun,
                reason = "macro fun test",
                validUntil = Time.time + 10f,
                source = "Test"
            });

            CharacterAiDecisionTickResult funResult =
                decisionPipeline.RunMacroGoalDecision(actor);
            bool funSelected = funResult.Handled
                && actor.Blackboard.CurrentBranch == CharacterAiBranch.MacroGoal
                && actor.Blackboard.CurrentTask == "Run Seek Fun Macro Action"
                && actor.Brain.bestAction != null
                && actor.Brain.bestAction.actionset == shoppingActionSet
                && !actor.Blackboard.HasActiveMacroGoal()
                && actor.Blackboard.SelectedJobGiverUtilitySummary.Contains("ShoppingJobGiver");

            if (!foodSelected || !funSelected)
            {
                Debug.LogError(
                    "Macro JobGiver probe failed: "
                    + $"foodHandled={foodResult.Handled} foodTask={foodResult.Task} foodStatus={foodResult.Status} "
                    + $"foodSelected={foodSelected} "
                    + $"funHandled={funResult.Handled} funTask={funResult.Task} funStatus={funResult.Status} "
                    + $"funSelected={funSelected} "
                    + $"best={actor.Brain.bestAction?.actionset?.actionName ?? "None"} "
                    + $"selectedJobGiver={actor.Blackboard.SelectedJobGiverUtilitySummary} "
                    + $"hasMacro={actor.Blackboard.HasActiveMacroGoal()}");
            }

            return foodSelected && funSelected;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(eatActionSet);
            Object.DestroyImmediate(shoppingActionSet);
            Object.DestroyImmediate(lookAroundActionSet);
            if (createdGrid && gridObject != null)
            {
                Object.DestroyImmediate(gridObject);
            }
        }
    }

    private static bool VerifyJobGiverInvalidReevaluationClearsCachedCandidate()
    {
        GameObject gridObject = EnsureGridForScenario(out bool createdGrid);
        GameObject actorObject = CreateActorObject("JobGiverCacheInvalidationActor");
        actorObject.AddComponent<AbilityWork>();
        CharacterAiEditorTestDependencies.Inject(actorObject);
        actorObject.GetComponent<CharacterAbilityCache>()?.RefreshAbilityCache();
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "JobGiver Cache Customer", "Slime");
        ProbeOneShotWorkActionSet actionSet = ScriptableObject.CreateInstance<ProbeOneShotWorkActionSet>();
        actionSet.actionName = "Probe One Shot Work";
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 100f;
            actor.Brain.availableActions = new[]
            {
                new AIAction { actionset = actionSet }
            };
            actor.Blackboard.ClearJobGiverCandidateCache();
            actor.Brain.ClearPathSearchCache();
            ResetSchedulerPathBudgetForDebug();

            BehaviorDesigner.Runtime.Tasks.DungeonStory.WorkJobGiverBranch branch =
                new BehaviorDesigner.Runtime.Tasks.DungeonStory.WorkJobGiverBranch
                {
                    GameObject = actorObject,
                    Transform = actorObject.transform,
                    FriendlyName = "Work"
                };
            branch.OnAwake();
            float firstUtility = branch.GetUtility();
            bool cachedAfterValid = actor.Blackboard.TryGetCachedJobGiverCandidate(
                CharacterAiBranch.Work,
                out CharacterAiJobCandidate cachedCandidate);
            actionSet.canStart = false;
            actor.Brain.ClearPathSearchCache();
            ResetSchedulerPathBudgetForDebug();
            float secondUtility = branch.GetUtility();
            bool cachedAfterInvalid = actor.Blackboard.TryGetCachedJobGiverCandidate(
                CharacterAiBranch.Work,
                out _);

            return firstUtility > 0f
                && cachedAfterValid
                && cachedCandidate.ActionCandidate.ActionSet == actionSet
                && secondUtility <= 0f
                && !cachedAfterInvalid;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(actionSet);
            if (createdGrid && gridObject != null)
            {
                Object.DestroyImmediate(gridObject);
            }
        }
    }

    private static bool HasVisualRootLayout(ParentTask root)
    {
        if (!HasNodeLayout(root, "Character AI Root", new Vector2(0f, -140f))
            || root.Children == null
            || root.Children.Count != 8)
        {
            return false;
        }

        ParentTask critical = root.Children[0] as ParentTask;
        ParentTask deprivationBreakdown = root.Children[1] as ParentTask;
        ParentTask lockedAction = root.Children[2] as ParentTask;
        ParentTask interruptCheck = root.Children[3] as ParentTask;
        ParentTask macro = root.Children[4] as ParentTask;
        ParentTask emergency = root.Children[5] as ParentTask;
        ParentTask routineUtility = root.Children[6] as ParentTask;
        ParentTask idle = root.Children[7] as ParentTask;
        return HasCriticalBranchLayout(critical)
            && HasSimpleSequenceBranch(
                deprivationBreakdown,
                "Deprivation Breakdown",
                new Vector2(-730f, 20f),
                "Has Deprivation Breakdown?",
                new Vector2(-830f, 180f),
                "Run Deprivation Breakdown",
                new Vector2(-630f, 180f))
            && HasSimpleSequenceBranch(
                lockedAction,
                "Locked Action",
                new Vector2(-560f, 20f),
                "Has Locked Action?",
                new Vector2(-660f, 100f),
                "Run Locked Action",
                new Vector2(-460f, 100f))
            && HasSimpleSequenceBranch(
                interruptCheck,
                "Interrupt Check",
                new Vector2(-220f, 20f),
                "Can Interrupt?",
                new Vector2(-320f, 100f),
                "Stop Running Action",
                new Vector2(-120f, 100f))
            && HasMacroBranchLayout(macro)
            && HasSimpleSequenceBranch(
                emergency,
                "Emergency",
                new Vector2(500f, 20f),
                "Trace Emergency",
                new Vector2(400f, 100f),
                "Run Emergency Decision",
                new Vector2(600f, 100f))
            && HasSimpleSequenceBranch(
                routineUtility,
                "Routine Utility",
                new Vector2(820f, 20f),
                "Trace Routine Utility",
                new Vector2(720f, 100f),
                "Run Routine Utility",
                new Vector2(920f, 100f))
            && HasSimpleSequenceBranch(
                idle,
                "Idle",
                new Vector2(1140f, 20f),
                "Trace Idle",
                new Vector2(1040f, 100f),
                "Run Ambient Idle",
                new Vector2(1240f, 100f));
    }

    private static bool HasSimpleSequenceBranch(
        ParentTask branch,
        string branchName,
        Vector2 branchOffset,
        string firstChildName,
        Vector2 firstChildOffset,
        string secondChildName,
        Vector2 secondChildOffset)
    {
        return branch is Sequence
            && HasSelectorChildren(branch, branchName, branchOffset, 2)
            && HasNodeLayout(branch.Children[0], firstChildName, firstChildOffset)
            && HasNodeLayout(branch.Children[1], secondChildName, secondChildOffset);
    }

    private static bool HasSurvivalNeedsRoutine(ParentTask branch)
    {
        return branch is UtilitySelector
            && HasSelectorChildren(branch, "Survival Needs", new Vector2(700f, 150f), 5)
            && HasJobGiverBranch(branch.Children[0] as ParentTask, "Exit Dungeon", "Exit Dungeon Action", new Vector2(160f, 270f))
            && HasJobGiverBranch(branch.Children[1] as ParentTask, "Get Food", "Eat Action", new Vector2(440f, 270f))
            && HasJobGiverBranch(branch.Children[2] as ParentTask, "Toilet", "Toilet Action", new Vector2(720f, 270f))
            && HasJobGiverBranch(branch.Children[3] as ParentTask, "Rest", "Rest Action", new Vector2(1000f, 270f))
            && HasJobGiverBranch(branch.Children[4] as ParentTask, "Hygiene", "Hygiene Action", new Vector2(1280f, 270f));
    }

    private static bool HasDutyWorkRoutine(ParentTask branch)
    {
        return branch is UtilitySelector
            && HasSelectorChildren(branch, "Duty Work", new Vector2(1480f, 150f), 1)
            && HasJobGiverBranch(branch.Children[0] as ParentTask, "Work", "Work Action", new Vector2(1480f, 430f));
    }

    private static bool HasLeisureVisitRoutine(ParentTask branch)
    {
        return branch is UtilitySelector
            && HasSelectorChildren(branch, "Leisure Visit", new Vector2(700f, 530f), 2)
            && HasJobGiverBranch(branch.Children[0] as ParentTask, "Shopping", "Shopping Action", new Vector2(540f, 650f))
            && HasJobGiverBranch(branch.Children[1] as ParentTask, "Look Around", "Look Around Action", new Vector2(860f, 650f));
    }

    private static bool HasIdleRoutine(ParentTask branch)
    {
        return branch is UtilitySelector
            && HasSelectorChildren(branch, "Idle Routine", new Vector2(1220f, 530f), 2)
            && HasJobGiverBranch(branch.Children[0] as ParentTask, "Wait", "Wait Action", new Vector2(1140f, 650f))
            && HasAmbientIdleBranch(branch.Children[1] as ParentTask, new Vector2(1460f, 650f));
    }

    private static bool HasMacroBranchLayout(ParentTask branch)
    {
        return HasSelectorChildren(branch, "Macro Goals", new Vector2(220f, 20f), 9)
            && HasMacroTaskBranch(branch.Children[0] as ParentTask, "Continue Macro", "Has Continue?", "Clear Continue Macro", new Vector2(120f, 120f))
            && HasMacroTaskBranch(branch.Children[1] as ParentTask, "Avoid Facility Macro", "Has AvoidFacility?", "Run Avoid Facility", new Vector2(120f, 220f))
            && HasMacroTaskBranch(branch.Children[2] as ParentTask, "Complain Macro", "Has Complain?", "Run Complain", new Vector2(120f, 320f))
            && HasMacroTaskBranch(branch.Children[3] as ParentTask, "Vandalize Macro", "Has Vandalize?", "Run Vandalize", new Vector2(120f, 420f))
            && HasMacroTaskBranch(branch.Children[4] as ParentTask, "Exit Dungeon Macro", "Has ExitDungeon?", "Run Exit Dungeon Macro", new Vector2(120f, 520f))
            && HasMacroTaskBranch(branch.Children[5] as ParentTask, "Seek Food Macro", "Has SeekFood?", "Run Seek Food Macro", new Vector2(120f, 620f))
            && HasMacroTaskBranch(branch.Children[6] as ParentTask, "Seek Fun Macro", "Has SeekFun?", "Run Seek Fun Macro", new Vector2(120f, 720f))
            && HasMacroTaskBranch(branch.Children[7] as ParentTask, "Seek Toilet Macro", "Has SeekToilet?", "Run Seek Toilet Macro", new Vector2(120f, 820f))
            && HasMacroTaskBranch(branch.Children[8] as ParentTask, "Seek Hygiene Macro", "Has SeekHygiene?", "Run Seek Hygiene Macro", new Vector2(120f, 920f));
    }

    private static bool HasUtilityBranch(
        ParentTask branch,
        string branchName,
        string utilityName,
        Vector2 branchOffset,
        Vector2 utilityOffset)
    {
        return HasSelectorChildren(branch, branchName, branchOffset, 1)
            && HasNodeLayout(branch.Children[0], utilityName, utilityOffset);
    }

    private static bool HasJobGiverBranch(
        ParentTask branch,
        string branchName,
        string actionName,
        Vector2 branchOffset)
    {
        return HasSelectorChildren(branch, branchName, branchOffset, 2)
            && HasNodeLayout(branch.Children[0], $"Select {actionName}", branchOffset + new Vector2(-100f, 80f))
            && HasNodeLayout(branch.Children[1], $"Run {actionName}", branchOffset + new Vector2(100f, 80f));
    }

    private static bool HasAmbientIdleBranch(ParentTask branch, Vector2 branchOffset)
    {
        return HasSelectorChildren(branch, "Ambient Idle", branchOffset, 1)
            && HasNodeLayout(branch.Children[0], "Run Ambient Idle", branchOffset + new Vector2(0f, 80f));
    }

    private static bool HasMacroTaskBranch(
        ParentTask branch,
        string branchName,
        string conditionName,
        string taskName,
        Vector2 branchOffset)
    {
        return HasSelectorChildren(branch, branchName, branchOffset, 2)
            && HasNodeLayout(branch.Children[0], conditionName, branchOffset + new Vector2(-100f, 80f))
            && HasNodeLayout(branch.Children[1], taskName, branchOffset + new Vector2(100f, 80f));
    }

    private static bool HasSelectorChildren(
        ParentTask selector,
        string selectorName,
        Vector2 selectorOffset,
        int childCount)
    {
        return HasNodeLayout(selector, selectorName, selectorOffset)
            && selector.Children != null
            && selector.Children.Count == childCount;
    }

    private static bool HasActionBranch(
        ParentTask branch,
        string branchName,
        string selectName,
        string runName)
    {
        return branch != null
            && branch.NodeData != null
            && HasNodeLayout(branch, branchName, branch.NodeData.Offset)
            && branch.Children != null
            && branch.Children.Count == 2
            && HasNodeLayout(branch.Children[0], selectName, branch.NodeData.Offset + new Vector2(-80f, 95f))
            && HasNodeLayout(branch.Children[1], runName, branch.NodeData.Offset + new Vector2(80f, 95f));
    }

    private static bool HasCriticalBranchLayout(ParentTask branch)
    {
        return HasNodeLayout(branch, "Critical", new Vector2(-900f, 20f))
            && branch.Children != null
            && branch.Children.Count == 2
            && HasNodeLayout(branch.Children[0], "Has Critical?", new Vector2(-1000f, 120f))
            && HasNodeLayout(branch.Children[1], "Run Critical", new Vector2(-800f, 120f));
    }

    private static bool HasContinueCurrentBranchLayout(ParentTask branch)
    {
        return HasSelectorChildren(branch, "Continue Current", new Vector2(300f, 20f), 2)
            && HasNodeLayout(branch.Children[0], "Can Continue?", new Vector2(200f, 100f))
            && HasNodeLayout(branch.Children[1], "Keep Running Action", new Vector2(400f, 100f));
    }

    private static bool HasStopCurrentBranchLayout(ParentTask branch)
    {
        return HasSelectorChildren(branch, "Stop Current", new Vector2(580f, 20f), 2)
            && HasNodeLayout(branch.Children[0], "Should Stop?", new Vector2(480f, 100f))
            && HasNodeLayout(branch.Children[1], "Stop Running Action", new Vector2(680f, 100f));
    }

    private static bool HasMacroUtilityBranch(
        ParentTask branch,
        string branchName,
        string conditionName,
        string actionName)
    {
        return branch != null
            && branch.NodeData != null
            && HasNodeLayout(branch, branchName, branch.NodeData.Offset)
            && branch.Children != null
            && branch.Children.Count == 2
            && HasNodeLayout(branch.Children[0], conditionName, branch.NodeData.Offset + new Vector2(-70f, 100f))
            && HasNodeLayout(branch.Children[1], actionName, branch.NodeData.Offset + new Vector2(70f, 100f));
    }

    private static bool HasMacroActionBranch(
        ParentTask branch,
        string branchName,
        string conditionName,
        string selectName,
        string runName,
        string clearName)
    {
        return branch != null
            && branch.NodeData != null
            && HasNodeLayout(branch, branchName, branch.NodeData.Offset)
            && branch.Children != null
            && branch.Children.Count == 4
            && HasNodeLayout(branch.Children[0], conditionName, branch.NodeData.Offset + new Vector2(-180f, 100f))
            && HasNodeLayout(branch.Children[1], selectName, branch.NodeData.Offset + new Vector2(-60f, 100f))
            && HasNodeLayout(branch.Children[2], runName, branch.NodeData.Offset + new Vector2(60f, 100f))
            && HasNodeLayout(branch.Children[3], clearName, branch.NodeData.Offset + new Vector2(180f, 100f));
    }

    private static bool HasNodeLayout(BehaviorDesigner.Runtime.Tasks.Task task, string name, Vector2 offset)
    {
        return task != null
            && task.NodeData != null
            && TaskDisplayName(task) == name
            && string.IsNullOrEmpty(task.NodeData.Comment)
            && Vector2.Distance(task.NodeData.Offset, offset) < 0.01f;
    }

    private static bool HasNoLayoutOverlap(params BehaviorDesigner.Runtime.Tasks.Task[] roots)
    {
        const float minSameRowXDistance = 55f;
        const float minSameColumnYDistance = 45f;
        List<BehaviorDesigner.Runtime.Tasks.Task> tasks = new List<BehaviorDesigner.Runtime.Tasks.Task>();
        foreach (BehaviorDesigner.Runtime.Tasks.Task root in roots)
        {
            CollectTasks(root, tasks);
        }

        for (int i = 0; i < tasks.Count; i++)
        {
            BehaviorDesigner.Runtime.Tasks.Task a = tasks[i];
            if (a == null || a.NodeData == null)
            {
                return false;
            }

            for (int j = i + 1; j < tasks.Count; j++)
            {
                BehaviorDesigner.Runtime.Tasks.Task b = tasks[j];
                if (b == null || b.NodeData == null)
                {
                    return false;
                }

                Vector2 delta = a.NodeData.Offset - b.NodeData.Offset;
                if (Mathf.Abs(delta.x) < minSameRowXDistance
                    && Mathf.Abs(delta.y) < minSameColumnYDistance)
                {
                    throw new System.InvalidOperationException(
                        $"BT nodes overlap: {NodeName(a)} and {NodeName(b)}.");
                }
            }
        }

        return true;
    }

    private static bool HasCompactLayoutBounds(
        float maxWidth,
        float maxHeight,
        params BehaviorDesigner.Runtime.Tasks.Task[] roots)
    {
        List<BehaviorDesigner.Runtime.Tasks.Task> tasks = new List<BehaviorDesigner.Runtime.Tasks.Task>();
        foreach (BehaviorDesigner.Runtime.Tasks.Task root in roots)
        {
            CollectTasks(root, tasks);
        }

        if (tasks.Count == 0)
        {
            return false;
        }

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        foreach (BehaviorDesigner.Runtime.Tasks.Task task in tasks)
        {
            if (task == null || task.NodeData == null)
            {
                return false;
            }

            Vector2 offset = task.NodeData.Offset;
            minX = Mathf.Min(minX, offset.x);
            maxX = Mathf.Max(maxX, offset.x);
            minY = Mathf.Min(minY, offset.y);
            maxY = Mathf.Max(maxY, offset.y);
        }

        float width = maxX - minX;
        float height = maxY - minY;
        if (width > maxWidth || height > maxHeight)
        {
            throw new System.InvalidOperationException(
                $"BT layout too large: width={width}, height={height}, max={maxWidth}x{maxHeight}.");
        }

        return true;
    }

    private static void CollectTasks(
        BehaviorDesigner.Runtime.Tasks.Task task,
        List<BehaviorDesigner.Runtime.Tasks.Task> tasks)
    {
        if (task == null)
        {
            return;
        }

        tasks.Add(task);
        if (task is not ParentTask parent || parent.Children == null)
        {
            return;
        }

        foreach (BehaviorDesigner.Runtime.Tasks.Task child in parent.Children)
        {
            CollectTasks(child, tasks);
        }
    }

    private static string NodeName(BehaviorDesigner.Runtime.Tasks.Task task)
    {
        if (task == null)
        {
            return "null";
        }

        string name = TaskDisplayName(task);
        string offset = task.NodeData != null ? task.NodeData.Offset.ToString() : "no-offset";
        return $"{name}@{offset}";
    }

    private static string TaskDisplayName(BehaviorDesigner.Runtime.Tasks.Task task)
    {
        if (task == null)
        {
            return string.Empty;
        }

        return task.NodeData != null && !string.IsNullOrEmpty(task.NodeData.FriendlyName)
            ? task.NodeData.FriendlyName
            : task.FriendlyName;
    }

    private static bool VerifyGameManagerHasNoRuntimeServiceLocator()
    {
        GameObject managerObject = new GameObject("GameManagerRuntimeReuse");
        try
        {
            int queueCountBefore = Object.FindObjectsByType<LocalLlmRequestQueue>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
            managerObject.AddComponent<GameManager>();
            MethodInfo method = typeof(GameManager)
                .GetMethod("EnsureRuntimeComponent", BindingFlags.Instance | BindingFlags.NonPublic);
            int queueCountAfter = Object.FindObjectsByType<LocalLlmRequestQueue>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;

            return method == null
                && queueCountAfter == queueCountBefore
                && managerObject.GetComponent<LocalLlmRequestQueue>() == null;
        }
        finally
        {
            Object.DestroyImmediate(managerObject);
        }
    }

    private static bool VerifyRuntimeActorBehaviorTreeContract()
    {
        GameObject actorObject = CreateActorObject("RuntimeActorBehaviorTreeContract");
        GameObject schedulerObject = new GameObject("RuntimeActorBehaviorTreeScheduler");
        schedulerObject.SetActive(false);
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            ExternalBehaviorTree externalBehavior =
                CharacterAiBehaviorDesignerGraphBuilder.EnsureCharacterAiExternalBehavior();
            CharacterAiBehaviorDesignerGraphBuilder.BuildVisualCharacterAiBehaviorTrees();
            CharacterAiScheduler scheduler = schedulerObject.AddComponent<CharacterAiScheduler>();
            CharacterAiEditorTestDependencies.Inject(scheduler);
            SerializedObject schedulerSerialized = new SerializedObject(scheduler);
            SerializedProperty externalProperty = schedulerSerialized.FindProperty("characterAiExternalBehavior");
            externalProperty.objectReferenceValue = externalBehavior;
            schedulerSerialized.ApplyModifiedPropertiesWithoutUndo();
            schedulerObject.SetActive(true);
            scheduler.ClearRegistrationsForDebug();
            scheduler.RegisterActor(actor);
            BehaviorTree tree = actorObject.GetComponent<BehaviorTree>();
            return tree != null
                && scheduler.CharacterAiExternalBehavior == externalBehavior
                && tree.ExternalBehavior == externalBehavior
                && !tree.StartWhenEnabled
                && actor.Blackboard != null
                && actor.PersonaRuntime != null
                && actor.DialogueRuntime != null
                && actor.SocialMemory != null;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(schedulerObject);
        }
    }

    private static bool VerifyDirectorContextCompression()
    {
        LocalLlmRequestQueue queue = EnsureQueueInstance(out GameObject queueObject);
        GameObject actorObject = CreateActorObject("ContextCompressionActor");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Wait,
                CharacterActivityOutcomes.Failed,
                "AI failure: no path",
                actionId: "probe:context-compression",
                reasonCode: "no-path",
                sentiment: -0.5f,
                bubbleEligible: true));
            AiDirectorContextSummary summary = AiDirectorContextAggregator.Build(
                actor,
                new AiDirectorContextSceneSnapshot(new[] { actor }, new BuildableObject[0]));
            string prompt = summary.ToPromptText(256);
            return queue != null
                && prompt.Length <= 256
                && prompt.Contains("characterCount")
                && prompt.Contains("targetRecentEvents");
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            if (queueObject != null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyDirectorPromptNumericContract()
    {
        GameObject actorObject = CreateActorObject("DirectorPromptContractActor");
        GameObject directorObject = new GameObject("DirectorPromptContractRuntime", typeof(AiDirectorRuntime));
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            AiDirectorRuntime director = directorObject.GetComponent<AiDirectorRuntime>();
            CharacterAiEditorTestDependencies.Inject(director);
            MethodInfo method = typeof(AiDirectorRuntime)
                .GetMethod("BuildMacroGoalPrompt", BindingFlags.Instance | BindingFlags.NonPublic);
            string prompt = method?.Invoke(director, new object[] { actor }) as string;

            return !string.IsNullOrWhiteSpace(prompt)
                && prompt.Contains("validSeconds", System.StringComparison.Ordinal)
                && prompt.Contains("between 1 and 600", System.StringComparison.Ordinal)
                && prompt.Contains("Never use minutes", System.StringComparison.Ordinal)
                && prompt.Contains("one JSON object only", System.StringComparison.Ordinal);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(directorObject);
        }
    }

    private static bool VerifyDirectorRoutineMacroTrigger()
    {
        LocalLlmRequestQueue queue = EnsureQueueInstance(out GameObject queueObject);
        GameObject actorObject = CreateActorObject("DirectorRoutineMacroActor");
        GameObject directorObject = new GameObject("DirectorRoutineMacroRuntime", typeof(AiDirectorRuntime));
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "Routine Macro Customer", "Slime");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Stats.Stats[CharacterCondition.MOOD] = 70f;
            actor.Stats.Stats[CharacterCondition.HUNGER] = 100f;
            actor.Stats.Stats[CharacterCondition.SLEEP] = 100f;
            actor.Stats.Stats[CharacterCondition.FUN] = 100f;

            AiDirectorRuntime director = directorObject.GetComponent<AiDirectorRuntime>();
            CharacterAiEditorTestDependencies.Inject(director);
            queue.ClearForDebug();
            bool routinePredicateHasNoCooldownSideEffect = director.ShouldRequestMacroGoal(actor)
                && director.ShouldRequestMacroGoal(actor);
            bool accepted = director.RequestMacroGoal(actor);
            bool routineCooldownBlocksAfterAcceptedRequest = !director.ShouldRequestMacroGoal(actor);
            actor.Blackboard.SetMacroGoal(new CharacterMacroGoal
            {
                type = CharacterMacroGoalType.SeekFun,
                reason = "active macro blocks duplicate director requests",
                validUntil = Time.time + 10f,
                source = "Test"
            });

            bool activeMacroBlocksRequest = !director.ShouldRequestMacroGoal(actor);
            return queue != null
                && routinePredicateHasNoCooldownSideEffect
                && accepted
                && queue.QueuedCount == 1
                && routineCooldownBlocksAfterAcceptedRequest
                && activeMacroBlocksRequest;
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(directorObject);
            Object.DestroyImmediate(data);
            if (queue != null)
            {
                queue.ClearForDebug();
            }

            if (queueObject != null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyDirectorRejectedMacroRequestStartsBackoff()
    {
        LocalLlmRequestQueue queue = EnsureQueueInstance(out GameObject queueObject);
        GameObject actorObject = CreateActorObject("DirectorRejectedMacroActor");
        GameObject directorObject = new GameObject("DirectorRejectedMacroRuntime", typeof(AiDirectorRuntime));
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "Rejected Macro Customer", "Slime");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.Stats.Stats[CharacterCondition.MOOD] = 70f;
            actor.Stats.Stats[CharacterCondition.HUNGER] = 100f;
            actor.Stats.Stats[CharacterCondition.SLEEP] = 100f;
            actor.Stats.Stats[CharacterCondition.FUN] = 100f;

            AiDirectorRuntime director = directorObject.GetComponent<AiDirectorRuntime>();
            CharacterAiEditorTestDependencies.Inject(director);
            director.SetWarningLogsSuppressedForDebug(true);
            queue.ClearForDebug();
            queue.SetWarningLogsSuppressedForDebug(true);
            for (int i = 0; i < queue.MaxQueueSize; i++)
            {
                if (!queue.EnqueueSocialRumor(
                        "{\"rumorType\":\"None\",\"targetType\":\"None\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":0,\"summary\":\"none\",\"spreadChance\":0,\"trustImpact\":0,\"validSeconds\":0}",
                        null))
                {
                    return false;
                }
            }

            bool shouldBefore = director.ShouldRequestMacroGoal(actor);
            bool accepted = director.RequestMacroGoal(actor);
            bool shouldAfterRejected = director.ShouldRequestMacroGoal(actor);
            return shouldBefore
                && !accepted
                && !shouldAfterRejected
                && queue.QueuedCount == queue.MaxQueueSize
                && director.LastError.Contains("not accepted", System.StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AiDirectorRuntime director = directorObject != null
                ? directorObject.GetComponent<AiDirectorRuntime>()
                : null;
            director?.SetWarningLogsSuppressedForDebug(false);
            if (queue != null)
            {
                queue.SetWarningLogsSuppressedForDebug(false);
                queue.ClearForDebug();
            }

            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(directorObject);
            Object.DestroyImmediate(data);
            if (queueObject != null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifySocialRejectedRequestStartsCooldown()
    {
        LocalLlmRequestQueue queue = EnsureQueueInstance(out GameObject queueObject);
        GameObject actorObject = CreateActorObject("SocialRejectedRequestActor");
        SocialReputationRuntime runtime = EnsureSocialRuntimeInstance(out GameObject runtimeObject);
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "Social Retry Customer", "Slime");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(data);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            runtime.ClearForDebug();
            runtime.SetWarningLogsSuppressedForDebug(true);
            queue.ClearForDebug();
            queue.SetWarningLogsSuppressedForDebug(true);
            for (int i = 0; i < queue.MaxQueueSize; i++)
            {
                if (!queue.EnqueueMacroGoal(
                        "{\"macroGoal\":\"Continue\",\"reason\":\"capacity fill\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"validSeconds\":30}",
                        null))
                {
                    return false;
                }
            }

            CharacterActivityEvent activity = CharacterActivityEvent.Create(
                CharacterActivityKinds.Social,
                CharacterActivityOutcomes.Failed,
                "A tense encounter ended badly.",
                actionId: "social:test-retry",
                reasonCode: "rejected-request-probe",
                sentiment: -0.7f,
                narrativeEligible: true);
            CharacterLogEntry entry = new CharacterLogEntry(
                1,
                activity.KindId,
                "A tense encounter ended badly.",
                1,
                "A tense encounter ended badly.",
                activity);
            bool rejected = !runtime.RequestSocialInterpretation(actor, entry);
            bool cooldownAfterRejected = !runtime.RequestSocialInterpretation(actor, entry)
                && string.Equals(
                    runtime.LastRequestSkipDebug,
                    "cooldown",
                    System.StringComparison.OrdinalIgnoreCase);

            queue.ClearForDebug();
            queue.SetWarningLogsSuppressedForDebug(true);
            runtime.ClearForDebug();
            bool acceptedAfterRejected = runtime.RequestSocialInterpretation(actor, entry);
            bool cooldownAfterAccepted = !runtime.RequestSocialInterpretation(actor, entry)
                && string.Equals(runtime.LastRequestSkipDebug, "cooldown", System.StringComparison.OrdinalIgnoreCase);

            return rejected
                && cooldownAfterRejected
                && acceptedAfterRejected
                && cooldownAfterAccepted;
        }
        finally
        {
            runtime?.SetWarningLogsSuppressedForDebug(false);
            if (queue != null)
            {
                queue.SetWarningLogsSuppressedForDebug(false);
                queue.ClearForDebug();
            }

            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }

            if (queueObject != null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyLlmQueueDropPolicy()
    {
        LocalLlmRequestQueue existingQueue = FindQueueInstance();
        GameObject queueObject = existingQueue == null
            ? new GameObject("QueueDropPolicy", typeof(LocalLlmRequestQueue))
            : existingQueue.gameObject;
        int dropped = 0;
        bool droppedContentStayedEmpty = true;
        bool droppedOriginalStayedDebugOnly = true;
        try
        {
            LocalLlmRequestQueue queue = existingQueue != null
                ? existingQueue
                : queueObject.GetComponent<LocalLlmRequestQueue>();
            CharacterAiEditorTestDependencies.Inject(queue);
            queue.ClearForDebug();
            for (int i = 0; i < 80; i++)
            {
                queue.EnqueueBubbleLine(
                    "{\"line\":\"x\"}",
                    "original",
                    (result) =>
                    {
                        if (result.Status == LocalLlmRequestStatus.Dropped)
                        {
                            dropped++;
                            droppedContentStayedEmpty &= string.IsNullOrEmpty(result.Content);
                            droppedOriginalStayedDebugOnly &= result.OriginalText == "original";
                        }
                    });
            }

            return dropped > 0
                && droppedContentStayedEmpty
                && droppedOriginalStayedDebugOnly
                && queue.DroppedEphemeralRequestCount == dropped;
        }
        finally
        {
            if (existingQueue == null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyLlmQueueCapacityProtectsNonBubbleRequests()
    {
        LocalLlmRequestQueue existingQueue = FindQueueInstance();
        GameObject queueObject = existingQueue == null
            ? new GameObject("QueueCapacityPolicy", typeof(LocalLlmRequestQueue))
            : existingQueue.gameObject;
        int failed = 0;
        LocalLlmRequestStatus failureStatus = LocalLlmRequestStatus.Succeeded;
        try
        {
            LocalLlmRequestQueue queue = existingQueue != null
                ? existingQueue
                : queueObject.GetComponent<LocalLlmRequestQueue>();
            CharacterAiEditorTestDependencies.Inject(queue);
            queue.ClearForDebug();
            queue.SetWarningLogsSuppressedForDebug(true);
            for (int i = 0; i < queue.MaxQueueSize; i++)
            {
                bool accepted = queue.EnqueueSocialRumor(
                    "{\"rumorType\":\"None\",\"targetType\":\"None\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"targetCharacterId\":\"\",\"targetCharacterName\":\"\",\"sentiment\":0,\"summary\":\"none\",\"spreadChance\":0,\"trustImpact\":0,\"validSeconds\":0}",
                    null);
                if (!accepted)
                {
                    return false;
                }
            }

            bool extraAccepted = queue.Enqueue(
                LocalLlmRequestProfiles.Persona,
                "{\"macroGoal\":\"Continue\",\"reason\":\"capacity test\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"validSeconds\":30}",
                string.Empty,
                1f,
                (result) =>
                {
                    if (!result.IsSuccess)
                    {
                        failed++;
                        failureStatus = result.Status;
                    }
                });

            return !extraAccepted
                && failed == 1
                && failureStatus == LocalLlmRequestStatus.Failed
                && queue.QueuedCount == queue.MaxQueueSize
                && queue.LastError.Contains("queue is full", System.StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            LocalLlmRequestQueue queue = queueObject != null
                ? queueObject.GetComponent<LocalLlmRequestQueue>()
                : null;
            queue?.SetWarningLogsSuppressedForDebug(false);
            if (existingQueue == null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyLlmQueueSupportsOpenRequestProfiles()
    {
        LocalLlmRequestQueue existingQueue = FindQueueInstance();
        GameObject queueObject = existingQueue == null
            ? new GameObject("QueueOpenProfilePolicy", typeof(LocalLlmRequestQueue))
            : existingQueue.gameObject;
        try
        {
            LocalLlmRequestQueue queue = existingQueue != null
                ? existingQueue
                : queueObject.GetComponent<LocalLlmRequestQueue>();
            CharacterAiEditorTestDependencies.Inject(queue);
            queue.ClearForDebug();
            queue.SetWarningLogsSuppressedForDebug(true);

            LocalLlmRequestProfile lowPriority = new LocalLlmRequestProfile(
                "TestLowPriority",
                1,
                temperature: 0.2f);
            LocalLlmRequestProfile highPriority = new LocalLlmRequestProfile(
                "TestHighPriority",
                100,
                temperature: 0.9f,
                maxQueueAgeSeconds: 9f);
            bool lowAccepted = queue.Enqueue(lowPriority, "{}", string.Empty, 1f, null);
            bool highAccepted = queue.Enqueue(highPriority, "{}", string.Empty, 1f, null);
            bool customPriorityApplied = queue.PeekNextProfileIdForDebug() == highPriority.Id;
            bool profileIsImmutable = !typeof(LocalLlmRequestProfile)
                .GetProperty(nameof(LocalLlmRequestProfile.Priority))
                .CanWrite;

            queue.ClearForDebug();
            queue.SetWarningLogsSuppressedForDebug(true);
            for (int i = 0; i < queue.MaxQueueSize; i++)
            {
                if (!queue.Enqueue(LocalLlmRequestProfiles.SocialRumor, "{}", string.Empty, 1f, null))
                {
                    return false;
                }
            }

            int quietCallbackCount = 0;
            LocalLlmRequestProfile quietProfile = new LocalLlmRequestProfile(
                "TestQuietWhenFull",
                50,
                queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly);
            bool quietAccepted = queue.Enqueue(
                quietProfile,
                "{}",
                string.Empty,
                1f,
                _ => quietCallbackCount++);

            int dropCallbackCount = 0;
            LocalLlmRequestStatus dropStatus = LocalLlmRequestStatus.Succeeded;
            LocalLlmRequestProfile dropProfile = new LocalLlmRequestProfile(
                "TestDropWhenFull",
                2,
                queueFullBehavior: LocalLlmQueueFullBehavior.Drop,
                canBeEvictedForQueuePressure: true,
                maxQueueAgeSeconds: 2f);
            bool dropAccepted = queue.Enqueue(
                dropProfile,
                "{}",
                "original",
                1f,
                result =>
                {
                    dropCallbackCount++;
                    dropStatus = result.Status;
                });

            return lowAccepted
                && highAccepted
                && customPriorityApplied
                && profileIsImmutable
                && highPriority.Temperature == 0.9f
                && highPriority.MaxQueueAgeSeconds == 9f
                && !quietAccepted
                && quietCallbackCount == 0
                && queue.LastError.Contains(quietProfile.Id, System.StringComparison.Ordinal)
                && !dropAccepted
                && dropCallbackCount == 1
                && dropStatus == LocalLlmRequestStatus.Dropped
                && queue.DroppedEphemeralRequestCount == 1;
        }
        finally
        {
            LocalLlmRequestQueue queue = queueObject != null
                ? queueObject.GetComponent<LocalLlmRequestQueue>()
                : null;
            if (queue != null)
            {
                queue.SetWarningLogsSuppressedForDebug(false);
                queue.ClearForDebug();
            }

            if (existingQueue == null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyLlmQueueCancellationCompletesExactlyOnce()
    {
        LocalLlmRequestQueue existingQueue = FindQueueInstance();
        GameObject queueObject = existingQueue == null
            ? new GameObject("QueueCancellationPolicy", typeof(LocalLlmRequestQueue))
            : existingQueue.gameObject;
        try
        {
            LocalLlmRequestQueue queue = existingQueue != null
                ? existingQueue
                : queueObject.GetComponent<LocalLlmRequestQueue>();
            CharacterAiEditorTestDependencies.Inject(queue);
            queue.ClearForDebug();

            int callbackCount = 0;
            int cancelledCount = 0;
            int correlatedCallbackCount = 0;
            bool originalTextPreserved = true;
            bool personaAccepted = queue.EnqueuePersona(
                "{\"persona\":\"test\"}",
                result =>
                {
                    callbackCount++;
                    cancelledCount += result.IsCancelled ? 1 : 0;
                });
            bool bubbleAccepted = queue.EnqueueBubbleLine(
                "{\"line\":\"test\"}",
                "original line",
                result =>
                {
                    callbackCount++;
                    cancelledCount += result.IsCancelled ? 1 : 0;
                    originalTextPreserved &= result.OriginalText == "original line";
                });
            bool skillAccepted = queue.GenerateCharacterSkillAsync(
                "skill:cancel-test",
                "{\"skill\":\"test\"}",
                result =>
                {
                    callbackCount++;
                    correlatedCallbackCount++;
                    cancelledCount += result.IsCancelled ? 1 : 0;
                });

            queue.CancelCharacterSkillRequest("skill:cancel-test");
            bool correlatedOnlyCompleted = correlatedCallbackCount == 1
                && callbackCount == 1
                && cancelledCount == 1
                && queue.QueuedCount == 2;
            queue.CancelCharacterSkillRequest("skill:cancel-test");

            queue.AbortAllForDebug();
            bool firstAbortCompleted = callbackCount == 3
                && cancelledCount == 3
                && correlatedCallbackCount == 1
                && originalTextPreserved
                && queue.QueuedCount == 0
                && queue.RunningCount == 0;

            queue.AbortAllForDebug();
            return personaAccepted
                && bubbleAccepted
                && skillAccepted
                && correlatedOnlyCompleted
                && firstAbortCompleted
                && callbackCount == 3;
        }
        finally
        {
            if (existingQueue == null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyPersonaRequestLifecycle()
    {
        LocalLlmRequestQueue queue = EnsureQueueInstance(out GameObject queueObject);
        GameObject actorObject = CreateActorObject("PersonaRequestActor");
        CharacterSO data = CreateCharacterData(CharacterType.Customer, "Persona Test Customer", "Slime");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            queue.ClearForDebug();
            int queuedBefore = queue != null ? queue.QueuedCount : -1;
            actor.Initialize(data);

            CustomerPersonaRuntime personaRuntime = actor.PersonaRuntime;
            return queue != null
                && personaRuntime != null
                && personaRuntime.PersonaRequestInProgress
                && !personaRuntime.HasGeneratedPersona
                && queue.QueuedCount == queuedBefore + 1
                && !string.IsNullOrWhiteSpace(personaRuntime.LastPrompt);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(data);
            if (queueObject != null)
            {
                Object.DestroyImmediate(queueObject);
            }
        }
    }

    private static bool VerifyBubbleNoFallback()
    {
        GameObject actorObject = CreateActorObject("BubbleNoFallbackActor");
        try
        {
            CharacterDialogueRuntime dialogue = actorObject.GetComponent<CharacterDialogueRuntime>();
            dialogue.ShowLine("previous generated line");

            MethodInfo method = typeof(CharacterDialogueRuntime)
                .GetMethod("OnBubbleResult", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(dialogue, new object[]
            {
                new LocalLlmResult(
                    LocalLlmRequestStatus.Dropped,
                    "original line that must not be shown",
                    "forced drop",
                    "original line that must not be shown")
            });

            return string.IsNullOrEmpty(dialogue.LastBubbleLine)
                && dialogue.LastError.Contains("Dropped", System.StringComparison.Ordinal);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyPersonaRejectsInvalidJson()
    {
        GameObject actorObject = CreateActorObject("PersonaInvalidJsonActor");
        try
        {
            CustomerPersonaRuntime personaRuntime = actorObject.GetComponent<CustomerPersonaRuntime>();
            MethodInfo method = typeof(CustomerPersonaRuntime)
                .GetMethod("OnPersonaResult", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(personaRuntime, new object[]
            {
                new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    "{\"flavorText\":\"missing trait\",\"selfCareMultiplier\":1.0,\"curiosityMultiplier\":1.0,\"shoppingMultiplier\":1.0,\"patienceMultiplier\":1.0,\"hungerCurveMultiplier\":1.0,\"funCurveMultiplier\":1.0,\"moodCurveMultiplier\":1.0,\"preferredFacilityTags\":[\"Meal\"]}",
                    string.Empty,
                    string.Empty)
            });

            return !personaRuntime.HasGeneratedPersona
                && !personaRuntime.PersonaRequestInProgress
                && personaRuntime.LastError.Contains("traitName", System.StringComparison.Ordinal);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    private static bool VerifyVandalizeMacro()
    {
        GameObject actorObject = CreateActorObject("VandalizeMacroActor");
        GameObject buildingObject = new GameObject("VandalizeMacroFacility", typeof(BuildableObject));
        CharacterSO characterData = CreateCharacterData(CharacterType.NPC, "Vandalizer", "Orc");
        BuildingSO buildingData = CreateBuildingData(998877, "Vandalize Test Facility");
        try
        {
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Initialize(characterData);
            actor.SetLifecycleState(CharacterLifecycleState.Active);

            BuildableObject building = buildingObject.GetComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.Initialization(buildingData, Vector2Int.zero);

            actor.Blackboard.SetMacroGoal(new CharacterMacroGoal
            {
                type = CharacterMacroGoalType.Vandalize,
                reason = "test anger",
                targetFacilityId = buildingData.id,
                validUntil = Time.time + 10f,
                source = "Test"
            });

            CharacterAiDecisionTickResult result =
                CreateDebugDecisionPipeline().RunVandalizeMacro(
                    actor,
                    actor.Blackboard,
                    actor.Blackboard.ActiveMacroGoal);
            return result.Handled
                && building.IsDamaged
                && !actor.Blackboard.HasActiveMacroGoal()
                && actor.Blackboard.CurrentTask == "Vandalize";
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(buildingObject);
            Object.DestroyImmediate(characterData);
            Object.DestroyImmediate(buildingData);
        }
    }

}
