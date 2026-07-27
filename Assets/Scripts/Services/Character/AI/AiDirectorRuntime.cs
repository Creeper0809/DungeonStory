using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
[DrawWithUnity]
public sealed class AiDirectorRuntime : SerializedMonoBehaviour
{
    [SerializeField, Min(1f)] private float evaluationIntervalSeconds = 8f;
    [SerializeField, Min(1)] private int maxActorsEvaluatedPerTick = 8;
    [SerializeField, Range(0f, 100f)] private float lowMoodThreshold = 18f;
    [SerializeField, Min(1f)] private float routineMacroGoalIntervalSeconds = 30f;
    [SerializeField, Range(0f, 100f)] private float routineMoodThreshold = 75f;
    [SerializeField, Range(0f, 100f)] private float routineNeedThreshold = 85f;
    [SerializeField, Range(0f, 100f)] private float moodImpulseThreshold = 55f;
    [SerializeField, Min(1f)] private float moodImpulseIntervalSeconds = 20f;
    [SerializeField, Range(0f, 1f)] private float moodImpulseMacroPromotionStrength = 0.76f;
    [SerializeField, Min(1)] private int repeatedFailureThreshold = 3;
    [SerializeField, Min(200)] private int maxPromptCharacters = 1800;
    [SerializeField, ReadOnly] private float nextEvaluationTime;
    [SerializeField, ReadOnly] private string lastRequestDebug;
    [SerializeField, ReadOnly] private CharacterMacroGoalType lastAppliedMacroGoalType = CharacterMacroGoalType.None;
    [SerializeField, ReadOnly] private string lastAppliedMacroActorName;
    [SerializeField, ReadOnly] private string lastAppliedMacroGoalDebug;
    [SerializeField, ReadOnly] private CharacterMoodImpulseType lastAppliedMoodImpulseType = CharacterMoodImpulseType.None;
    [SerializeField, ReadOnly] private string lastAppliedMoodImpulseActorName;
    [SerializeField, ReadOnly] private string lastAppliedMoodImpulseDebug;
    [SerializeField, ReadOnly] private string lastError;
    [SerializeField, ReadOnly] private bool suppressWarningLogsForDebug;

    private int roundRobinIndex;
    private readonly Dictionary<CharacterActor, float> nextRoutineMacroGoalTimeByActor =
        new Dictionary<CharacterActor, float>();
    private readonly Dictionary<CharacterActor, float> nextMoodImpulseTimeByActor =
        new Dictionary<CharacterActor, float>();
    private ILocalLlmRuntimeProvider llmRuntimeProvider;
    private IAiDirectorContextSceneQuery contextSceneQuery;
    private ICharacterAiSchedulingService aiSchedulingService;
    private ICharacterAiFacilityLookup facilityLookup;
    private IGameClock gameClock;
    private IUiClock uiClock;

    public string LastRequestDebug => lastRequestDebug;
    public CharacterMacroGoalType LastAppliedMacroGoalType => lastAppliedMacroGoalType;
    public string LastAppliedMacroActorName => lastAppliedMacroActorName;
    public string LastAppliedMacroGoalDebug => lastAppliedMacroGoalDebug;
    public CharacterMoodImpulseType LastAppliedMoodImpulseType => lastAppliedMoodImpulseType;
    public string LastAppliedMoodImpulseActorName => lastAppliedMoodImpulseActorName;
    public string LastAppliedMoodImpulseDebug => lastAppliedMoodImpulseDebug;
    public string LastError => lastError;
    private float DirectorTime => uiClock != null
        ? uiClock.Time
        : gameClock != null ? gameClock.Time : 0f;

    [Inject]
    public void ConstructAiDirectorRuntime(
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        IAiDirectorContextSceneQuery contextSceneQuery,
        ICharacterAiSchedulingService aiSchedulingService,
        ICharacterAiFacilityLookup facilityLookup,
        IGameClock gameClock,
        IUiClock uiClock = null)
    {
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.contextSceneQuery = contextSceneQuery
            ?? throw new ArgumentNullException(nameof(contextSceneQuery));
        this.aiSchedulingService = aiSchedulingService
            ?? throw new ArgumentNullException(nameof(aiSchedulingService));
        this.facilityLookup = facilityLookup
            ?? throw new ArgumentNullException(nameof(facilityLookup));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.uiClock = uiClock;
    }

    public void SetWarningLogsSuppressedForDebug(bool value)
    {
        suppressWarningLogsForDebug = value;
    }

    private void Update()
    {
        if (contextSceneQuery == null || aiSchedulingService == null || facilityLookup == null)
        {
            return;
        }

        if (DirectorTime < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime =
            DirectorTime + Mathf.Max(1f, evaluationIntervalSeconds);
        EvaluateOneActor();
    }

    public void EvaluateOneActor()
    {
        IReadOnlyList<CharacterActor> actors = RequireContextSceneQuery().Capture().Actors;
        if (actors.Count == 0)
        {
            return;
        }

        int evaluationCount = Mathf.Min(
            actors.Count,
            Mathf.Max(1, maxActorsEvaluatedPerTick));
        for (int i = 0; i < evaluationCount; i++)
        {
            roundRobinIndex %= actors.Count;
            CharacterActor actor = actors[roundRobinIndex++];
            if (ShouldRequestMoodImpulse(actor) && RequestMoodImpulse(actor))
            {
                return;
            }

            if (ShouldRequestMacroGoal(actor) && RequestMacroGoal(actor))
            {
                return;
            }
        }
    }

    public bool ShouldRequestMoodImpulse(CharacterActor actor)
    {
        if (actor == null || actor.Blackboard == null || !actor.CanRunAi)
        {
            return false;
        }

        if (actor.Blackboard.HasActiveMoodImpulse() || actor.Blackboard.HasActiveMacroGoal())
        {
            return false;
        }

        if (DirectorTime < GetNextMoodImpulseTime(actor))
        {
            return false;
        }

        return GetMood(actor) <= moodImpulseThreshold
            || GetCondition(actor, CharacterCondition.EXCRETION) <= 35f
            || GetCondition(actor, CharacterCondition.HYGIENE) <= 30f
            || HasRepeatedFailureReason(actor);
    }

    public bool ShouldRequestMacroGoal(CharacterActor actor)
    {
        if (actor == null || actor.Blackboard == null || !actor.CanRunAi)
        {
            return false;
        }

        if (actor.Blackboard.HasActiveMacroGoal())
        {
            return false;
        }

        if (HasUrgentDirectorReason(actor))
        {
            return true;
        }

        if (DirectorTime < GetNextRoutineMacroGoalTime(actor))
        {
            return false;
        }

        if (!HasRoutineDirectorReason(actor))
        {
            return false;
        }

        return true;
    }

    private bool HasUrgentDirectorReason(CharacterActor actor)
    {
        if (GetMood(actor) <= lowMoodThreshold)
        {
            return true;
        }

        return HasRepeatedFailureReason(actor);
    }

    private bool HasRepeatedFailureReason(CharacterActor actor)
    {
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        return blackboard != null
            && (blackboard.GetRecentFailureCount(AIActionFailureKind.NoPath) >= repeatedFailureThreshold
                || blackboard.GetRecentFailureCount(AIActionFailureKind.NoDestination) >= repeatedFailureThreshold
                || blackboard.GetRecentFailureCount(AIActionFailureKind.DestinationOccupied) >= repeatedFailureThreshold);
    }

    private bool HasRoutineDirectorReason(CharacterActor actor)
    {
        if (actor == null || actor.characterType != CharacterType.Customer)
        {
            return false;
        }

        if (GetMood(actor) <= routineMoodThreshold)
        {
            return true;
        }

        IReadOnlyList<CharacterNeedDefinition> needs =
            CharacterNeedCatalog.All;
        for (int index = 0; index < needs.Count; index++)
        {
            CharacterNeedDefinition definition = needs[index];
            if (definition.HasTag(CharacterNeedTag.DirectorRoutine)
                && GetCondition(actor, definition.Condition)
                    <= routineNeedThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private float GetNextRoutineMacroGoalTime(CharacterActor actor)
    {
        if (actor == null)
        {
            return float.PositiveInfinity;
        }

        return nextRoutineMacroGoalTimeByActor.TryGetValue(actor, out float nextTime)
            ? nextTime
            : 0f;
    }

    private void ScheduleNextRoutineMacroGoal(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        nextRoutineMacroGoalTimeByActor[actor] =
            DirectorTime + Mathf.Max(1f, routineMacroGoalIntervalSeconds);
    }

    private float GetNextMoodImpulseTime(CharacterActor actor)
    {
        if (actor == null)
        {
            return float.PositiveInfinity;
        }

        return nextMoodImpulseTimeByActor.TryGetValue(actor, out float nextTime)
            ? nextTime
            : 0f;
    }

    private void ScheduleNextMoodImpulse(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        nextMoodImpulseTimeByActor[actor] =
            DirectorTime + Mathf.Max(1f, moodImpulseIntervalSeconds);
    }

    public bool RequestMoodImpulse(CharacterActor actor)
    {
        if (actor == null || actor.Blackboard == null)
        {
            return false;
        }

        if (!TryGetLlmRuntime(out ILocalLlmRuntime queue))
        {
            return false;
        }

        string prompt = BuildMoodImpulsePrompt(actor);
        lastRequestDebug = prompt;
        if (!queue.GenerateMoodImpulseAsync(prompt, (result) => OnMoodImpulseResult(actor, result)))
        {
            lastError = "Mood impulse request was not accepted by LocalLlmRequestQueue.";
            ScheduleNextMoodImpulse(actor);
            return false;
        }

        ScheduleNextMoodImpulse(actor);
        lastError = string.Empty;
        return true;
    }

    public bool RequestMacroGoal(CharacterActor actor)
    {
        if (actor == null || actor.Blackboard == null)
        {
            return false;
        }

        if (!TryGetLlmRuntime(out ILocalLlmRuntime queue))
        {
            return false;
        }

        string prompt = BuildMacroGoalPrompt(actor);
        lastRequestDebug = prompt;
        if (!queue.GenerateMacroGoalAsync(prompt, (result) => OnMacroGoalResult(actor, result)))
        {
            lastError = "Macro goal request was not accepted by LocalLlmRequestQueue.";
            ScheduleNextRoutineMacroGoal(actor);
            return false;
        }

        ScheduleNextRoutineMacroGoal(actor);
        lastError = string.Empty;
        return true;
    }

    private void OnMacroGoalResult(CharacterActor actor, LocalLlmResult result)
    {
        if (actor == null || actor.Blackboard == null)
        {
            return;
        }

        if (result.IsCancelled)
        {
            lastError = string.Empty;
            return;
        }

        if (!result.IsSuccess)
        {
            lastError = result.Error;
            LogWarningIfAllowed($"Macro goal request failed: {result.Error}");
            return;
        }

        if (!LlmJsonResponseParser.TryParse(result.Content, out MacroGoalJsonDto dto, out string parseError))
        {
            lastError = parseError;
            LogWarningIfAllowed($"Macro goal JSON rejected: {parseError}");
            return;
        }

        CharacterMacroGoal runtimeGoal = dto.ToRuntimeGoal("LocalLLM", gameClock.Time);
        lastAppliedMacroGoalType = runtimeGoal.type;
        lastAppliedMacroActorName = actor.name;
        lastAppliedMacroGoalDebug =
            $"{runtimeGoal.type} targetId={runtimeGoal.targetFacilityId} tag={runtimeGoal.targetFacilityTag} reason={runtimeGoal.reason}";
        lastError = string.Empty;
        actor.Blackboard.SetMacroGoal(runtimeGoal);
        RequireAiSchedulingService().RequestImmediateDecision(actor);
    }

    private void OnMoodImpulseResult(CharacterActor actor, LocalLlmResult result)
    {
        if (actor == null || actor.Blackboard == null)
        {
            return;
        }

        if (result.IsCancelled)
        {
            lastError = string.Empty;
            return;
        }

        if (!result.IsSuccess)
        {
            lastError = result.Error;
            LogWarningIfAllowed($"Mood impulse request failed: {result.Error}");
            return;
        }

        if (!LlmJsonResponseParser.TryParse(result.Content, out MoodImpulseJsonDto dto, out string parseError))
        {
            lastError = parseError;
            LogWarningIfAllowed($"Mood impulse JSON rejected: {parseError}");
            return;
        }

        CharacterMoodImpulse impulse = dto.ToRuntimeImpulse("LocalLLM", gameClock.Time);
        if (!ValidateMoodImpulseTarget(impulse, out string targetError))
        {
            lastError = targetError;
            LogWarningIfAllowed($"Mood impulse target rejected: {targetError}");
            return;
        }

        lastAppliedMoodImpulseType = impulse.type;
        lastAppliedMoodImpulseActorName = actor.name;
        lastAppliedMoodImpulseDebug =
            $"{impulse.type} strength={impulse.strength:0.###} targetId={impulse.targetFacilityId} tag={impulse.targetFacilityTag} reason={impulse.reason}";
        lastError = string.Empty;

        if (impulse.type == CharacterMoodImpulseType.None || impulse.strength <= 0f)
        {
            actor.Blackboard.ClearMoodImpulse("LocalLLM returned no mood impulse.");
            return;
        }

        actor.Blackboard.SetMoodImpulse(impulse);
        ApplyMoodImpulseSideEffects(actor, impulse);
        RequireAiSchedulingService().RequestImmediateDecision(actor);
    }

    private bool TryGetLlmRuntime(out ILocalLlmRuntime queue)
    {
        if (llmRuntimeProvider == null)
        {
            throw new InvalidOperationException($"{nameof(AiDirectorRuntime)} requires {nameof(ILocalLlmRuntimeProvider)} injection.");
        }

        if (llmRuntimeProvider.TryGetRuntime(out queue))
        {
            return true;
        }

        lastError = $"{nameof(LocalLlmRequestQueue)} is missing.";
        LogWarningIfAllowed(lastError);
        return false;
    }

    private IAiDirectorContextSceneQuery RequireContextSceneQuery()
    {
        if (contextSceneQuery == null)
        {
            throw new InvalidOperationException($"{nameof(AiDirectorRuntime)} requires {nameof(IAiDirectorContextSceneQuery)} injection.");
        }

        return contextSceneQuery;
    }

    private ICharacterAiSchedulingService RequireAiSchedulingService()
    {
        if (aiSchedulingService == null)
        {
            throw new InvalidOperationException($"{nameof(AiDirectorRuntime)} requires {nameof(ICharacterAiSchedulingService)} injection.");
        }

        return aiSchedulingService;
    }

    private ICharacterAiFacilityLookup RequireFacilityLookup()
    {
        if (facilityLookup == null)
        {
            throw new InvalidOperationException($"{nameof(AiDirectorRuntime)} requires {nameof(ICharacterAiFacilityLookup)} injection.");
        }

        return facilityLookup;
    }

    private string BuildMacroGoalPrompt(CharacterActor actor)
    {
        AiDirectorContextSummary summary = AiDirectorContextAggregator.Build(
            actor,
            RequireContextSceneQuery().Capture());
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Choose one macro goal for the target customer.");
        builder.AppendLine("Allowed macroGoal values: Continue, SeekFood, SeekToilet, SeekHygiene, SeekFun, AvoidFacility, Complain, ExitDungeon, Vandalize.");
        builder.AppendLine("Return one JSON object only with macroGoal, reason, targetFacilityId, targetFacilityTag, validSeconds.");
        builder.AppendLine("targetFacilityId and validSeconds must be raw JSON numbers, not strings or null.");
        builder.AppendLine("validSeconds must be a number between 1 and 600. Never use minutes, hours, infinity, or values above 600.");
        builder.AppendLine("For ordinary goals, use validSeconds between 30 and 120.");
        builder.AppendLine("Use targetFacilityId -1 and targetFacilityTag \"\" unless AvoidFacility or Vandalize needs a concrete target.");
        builder.AppendLine("Choose AvoidFacility or Vandalize only when Dungeon summary lists a concrete target id or tag.");
        builder.AppendLine("If mood is 18 or lower, do not choose Continue; choose a concrete response such as Complain, ExitDungeon, SeekFood, or SeekFun.");
        builder.AppendLine("Example: {\"macroGoal\":\"Continue\",\"reason\":\"stable\",\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"validSeconds\":30}");
        builder.AppendLine("Target:");
        builder.AppendLine($"name: {actor.name}");
        builder.AppendLine($"mood: {GetMood(actor):0.0}");
        builder.AppendLine($"sleep: {GetCondition(actor, CharacterCondition.SLEEP):0.0}");
        builder.AppendLine($"hunger: {GetCondition(actor, CharacterCondition.HUNGER):0.0}");
        builder.AppendLine($"excretion: {GetCondition(actor, CharacterCondition.EXCRETION):0.0}");
        builder.AppendLine($"hygiene: {GetCondition(actor, CharacterCondition.HYGIENE):0.0}");
        builder.AppendLine("Dungeon summary:");
        builder.Append(summary.ToPromptText(maxPromptCharacters));

        string prompt = builder.ToString();
        if (prompt.Length > maxPromptCharacters)
        {
            return prompt.Substring(0, maxPromptCharacters);
        }

        return prompt;
    }

    private string BuildMoodImpulsePrompt(CharacterActor actor)
    {
        AiDirectorContextSummary summary = AiDirectorContextAggregator.Build(
            actor,
            RequireContextSceneQuery().Capture());
        CharacterBlackboard blackboard = actor != null ? actor.Blackboard : null;
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Choose one short-lived mood impulse for the target character.");
        builder.AppendLine("This is not a direct action command. The game will only use it as a score bias inside existing BT and Utility AI.");
        builder.AppendLine("Allowed moodImpulse values: None, FollowRoutine, SeekFood, SeekRest, SeekToilet, SeekHygiene, SeekFun, ImpulseShopping, Wander, Wait, IgnoreDuty, AvoidFacility, Complain, ExitDungeon, Vandalize.");
        builder.AppendLine("Return one JSON object only with moodImpulse, strength, targetFacilityId, targetFacilityTag, reason, validSeconds.");
        builder.AppendLine("strength, targetFacilityId, and validSeconds must be raw JSON numbers, not strings or null.");
        builder.AppendLine("strength must be between 0 and 1. validSeconds must be between 1 and 300.");
        builder.AppendLine("Use targetFacilityId -1 and targetFacilityTag \"\" unless AvoidFacility or Vandalize needs a concrete target.");
        builder.AppendLine("Choose Complain, ExitDungeon, or Vandalize only when the character context strongly supports an emotional outburst.");
        builder.AppendLine("If the character is only mildly unhappy, prefer SeekFun, SeekRest, SeekFood, SeekToilet, SeekHygiene, Wander, Wait, ImpulseShopping, or IgnoreDuty.");
        builder.AppendLine("Example: {\"moodImpulse\":\"SeekFun\",\"strength\":0.55,\"targetFacilityId\":-1,\"targetFacilityTag\":\"\",\"reason\":\"bored and low mood\",\"validSeconds\":45}");
        builder.AppendLine("Target:");
        builder.AppendLine($"name: {actor.name}");
        builder.AppendLine($"mood: {GetMood(actor):0.0}");
        builder.AppendLine($"sleep: {GetCondition(actor, CharacterCondition.SLEEP):0.0}");
        builder.AppendLine($"hunger: {GetCondition(actor, CharacterCondition.HUNGER):0.0}");
        builder.AppendLine($"fun: {GetCondition(actor, CharacterCondition.FUN):0.0}");
        builder.AppendLine($"excretion: {GetCondition(actor, CharacterCondition.EXCRETION):0.0}");
        builder.AppendLine($"hygiene: {GetCondition(actor, CharacterCondition.HYGIENE):0.0}");
        builder.AppendLine($"currentBranch: {(blackboard != null ? blackboard.CurrentBranch.ToString() : "None")}");
        builder.AppendLine($"currentIntent: {(blackboard != null ? blackboard.CurrentIntent : string.Empty)}");
        builder.AppendLine($"currentTask: {(blackboard != null ? blackboard.CurrentTask : string.Empty)}");
        builder.AppendLine($"recentNoPath: {(blackboard != null ? blackboard.GetRecentFailureCount(AIActionFailureKind.NoPath) : 0)}");
        builder.AppendLine($"recentNoDestination: {(blackboard != null ? blackboard.GetRecentFailureCount(AIActionFailureKind.NoDestination) : 0)}");
        builder.AppendLine($"recentDestinationOccupied: {(blackboard != null ? blackboard.GetRecentFailureCount(AIActionFailureKind.DestinationOccupied) : 0)}");
        builder.AppendLine("Dungeon summary:");
        builder.Append(summary.ToPromptText(maxPromptCharacters));

        string prompt = builder.ToString();
        if (prompt.Length > maxPromptCharacters)
        {
            return prompt.Substring(0, maxPromptCharacters);
        }

        return prompt;
    }

    private bool ValidateMoodImpulseTarget(CharacterMoodImpulse impulse, out string error)
    {
        error = string.Empty;
        if (impulse == null)
        {
            error = "Mood impulse is missing.";
            return false;
        }

        if (impulse.type != CharacterMoodImpulseType.AvoidFacility
            && impulse.type != CharacterMoodImpulseType.Vandalize)
        {
            return true;
        }

        BuildableObject target = RequireFacilityLookup().FindFacility(
            impulse.targetFacilityId,
            impulse.targetFacilityTag);
        if (target != null)
        {
            return true;
        }

        error = $"{impulse.type} target not found. id={impulse.targetFacilityId} tag={impulse.targetFacilityTag}";
        return false;
    }

    private void ApplyMoodImpulseSideEffects(CharacterActor actor, CharacterMoodImpulse impulse)
    {
        if (actor == null || actor.Blackboard == null || impulse == null)
        {
            return;
        }

        if (TryCreateMacroGoalFromMoodImpulse(impulse, out CharacterMacroGoal macroGoal))
        {
            actor.Blackboard.SetMacroGoal(macroGoal);
            return;
        }

        if (impulse.type != CharacterMoodImpulseType.AvoidFacility)
        {
            return;
        }

        BuildableObject target = RequireFacilityLookup().FindFacility(
            impulse.targetFacilityId,
            impulse.targetFacilityTag);
        if (target != null)
        {
            actor.Blackboard.PutFacilityOnCooldown(target, impulse.reason);
        }
    }

    private bool TryCreateMacroGoalFromMoodImpulse(
        CharacterMoodImpulse impulse,
        out CharacterMacroGoal macroGoal)
    {
        macroGoal = null;
        if (impulse == null || impulse.strength < moodImpulseMacroPromotionStrength)
        {
            return false;
        }

        CharacterMacroGoalType macroType = impulse.type switch
        {
            CharacterMoodImpulseType.AvoidFacility => CharacterMacroGoalType.AvoidFacility,
            CharacterMoodImpulseType.SeekToilet => CharacterMacroGoalType.SeekToilet,
            CharacterMoodImpulseType.SeekHygiene => CharacterMacroGoalType.SeekHygiene,
            CharacterMoodImpulseType.Complain => CharacterMacroGoalType.Complain,
            CharacterMoodImpulseType.ExitDungeon => CharacterMacroGoalType.ExitDungeon,
            CharacterMoodImpulseType.Vandalize => CharacterMacroGoalType.Vandalize,
            _ => CharacterMacroGoalType.None
        };

        if (macroType == CharacterMacroGoalType.None)
        {
            return false;
        }

        macroGoal = new CharacterMacroGoal
        {
            type = macroType,
            targetFacilityId = impulse.targetFacilityId,
            targetFacilityTag = impulse.targetFacilityTag,
            reason = impulse.reason,
            validUntil = impulse.validUntil,
            source = "MoodImpulseLLM"
        };
        return true;
    }

    private static float GetMood(CharacterActor actor)
    {
        return GetCondition(actor, CharacterCondition.MOOD);
    }

    private static float GetCondition(CharacterActor actor, CharacterCondition condition)
    {
        return actor != null
            && actor.Stats != null
            && actor.Stats.Stats != null
            && actor.Stats.Stats.TryGetValue(condition, out float value)
                ? value
                : 100f;
    }

    private void LogWarningIfAllowed(string message)
    {
        if (suppressWarningLogsForDebug)
        {
            return;
        }

        Debug.Log($"{name}: {message}", this);
    }
}
