using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
[DrawWithUnity]
public sealed class CharacterBlackboard : SerializedMonoBehaviour
{
    private const float DefaultFailureCooldownSeconds = 8f;
    private const float DefaultCommitSeconds = 1.75f;
    private const float DefaultCommitmentScoreBonus = 0.16f;
    private const int MaxDecisionTraceEntries = 24;

    [SerializeField, ReadOnly] private CharacterActor actor;
    [SerializeField, ReadOnly] private CharacterAiBranch currentBranch;
    [SerializeField, ReadOnly] private string currentIntent;
    [SerializeField, ReadOnly] private string currentTask;
    [SerializeField, ReadOnly] private string currentStatus;
    [SerializeField, ReadOnly] private AIActionSet committedAction;
    [SerializeField, ReadOnly] private BuildableObject committedDestination;
    [SerializeField, ReadOnly] private float minCommitUntil;
    [SerializeField, ReadOnly] private CharacterAiIntentState softLockIntent = new CharacterAiIntentState();
    [SerializeField, ReadOnly] private CharacterMacroGoal activeMacroGoal = new CharacterMacroGoal();
    [SerializeField, ReadOnly] private CharacterMoodImpulse activeMoodImpulse = new CharacterMoodImpulse();
    [SerializeField, ReadOnly] private Dictionary<BuildableObject, float> failedBuildingCooldowns =
        new Dictionary<BuildableObject, float>();
    [SerializeField, ReadOnly] private Dictionary<string, int> recentFailureCounts =
        new Dictionary<string, int>();
    [SerializeField, ReadOnly] private Dictionary<CharacterAiBranch, float> jobGiverUtilityScores =
        new Dictionary<CharacterAiBranch, float>();
    [SerializeField, ReadOnly] private Dictionary<CharacterAiBranch, float> routineGroupPriorityScores =
        new Dictionary<CharacterAiBranch, float>();
    [SerializeField, ReadOnly] private string lastJobGiverUtilitySummary;
    [SerializeField, ReadOnly] private string selectedJobGiverUtilitySummary;
    [SerializeField, ReadOnly] private string lastRoutineGroupPrioritySummary;
    [SerializeField, ReadOnly] private string lastUtilityBreakdownSummary;
    [SerializeField, ReadOnly] private string lastDecisionContextSummary;
    [SerializeField, ReadOnly] private List<string> topUtilityBreakdowns =
        new List<string>();
    [SerializeField, ReadOnly] private List<string> recentDecisionTrace =
        new List<string>();
    [NonSerialized] private Dictionary<CharacterAiBranch, CharacterAiJobCandidate> cachedJobGiverCandidates;
    [NonSerialized] private IReadOnlyDictionary<string, int> recentFailureCountsView;
    [NonSerialized] private IReadOnlyDictionary<CharacterAiBranch, float> jobGiverUtilityScoresView;
    [NonSerialized] private IReadOnlyDictionary<CharacterAiBranch, float> routineGroupPriorityScoresView;
    [NonSerialized] private IReadOnlyList<string> topUtilityBreakdownsView;
    [NonSerialized] private IReadOnlyList<string> recentDecisionTraceView;
    [SerializeField, ReadOnly] private string lastCommitBreakReason;
    [SerializeField, ReadOnly] private string lastFailureReason;
    [SerializeField, Min(0.1f)] private float failureCooldownSeconds = DefaultFailureCooldownSeconds;
    [SerializeField, Min(0f)] private float defaultCommitSeconds = DefaultCommitSeconds;
    [SerializeField, Range(0f, 0.5f)] private float commitmentScoreBonus = DefaultCommitmentScoreBonus;
    [NonSerialized] private IGameClock gameClock;

    private float Now => gameClock != null ? gameClock.Time : 0f;
    private bool DetailedDiagnosticsEnabled =>
        actor == null || actor.ShouldCollectDetailedAiDiagnostics;

    public CharacterAiBranch CurrentBranch => currentBranch;
    public string CurrentIntent => currentIntent;
    public string CurrentTask => currentTask;
    public string CurrentStatus => currentStatus;
    public AIActionSet CommittedAction => committedAction;
    public BuildableObject CommittedDestination => committedDestination;
    public float MinCommitUntil => minCommitUntil;
    public CharacterAiIntentState SoftLockIntent => softLockIntent;
    public CharacterMacroGoal ActiveMacroGoal => activeMacroGoal;
    public CharacterMoodImpulse ActiveMoodImpulse => activeMoodImpulse;
    public string LastCommitBreakReason => lastCommitBreakReason;
    public string LastFailureReason => lastFailureReason;
    public IReadOnlyDictionary<string, int> RecentFailureCounts => recentFailureCountsView;
    public IReadOnlyDictionary<CharacterAiBranch, float> JobGiverUtilityScores => jobGiverUtilityScoresView;
    public IReadOnlyDictionary<CharacterAiBranch, float> RoutineGroupPriorityScores => routineGroupPriorityScoresView;
    public string LastJobGiverUtilitySummary => lastJobGiverUtilitySummary;
    public string SelectedJobGiverUtilitySummary => selectedJobGiverUtilitySummary;
    public string LastRoutineGroupPrioritySummary => lastRoutineGroupPrioritySummary;
    public string LastUtilityBreakdownSummary => lastUtilityBreakdownSummary;
    public string LastDecisionContextSummary => lastDecisionContextSummary;
    public IReadOnlyList<string> TopUtilityBreakdowns => topUtilityBreakdownsView;
    public IReadOnlyList<string> RecentDecisionTrace => recentDecisionTraceView;
    public string LastDecisionTrace => recentDecisionTrace != null && recentDecisionTrace.Count > 0
        ? string.Join(" | ", recentDecisionTrace)
        : string.Empty;
    [ShowInInspector, ReadOnly]
    public string LastDecisionRouteSummary => BuildDecisionRouteSummary();

    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
    }

    [Inject]
    public void Construct(IGameClock gameClock)
    {
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public void Bind(CharacterActor owner)
    {
        actor = owner;
        activeMacroGoal ??= new CharacterMacroGoal();
        activeMoodImpulse ??= new CharacterMoodImpulse();
        softLockIntent ??= new CharacterAiIntentState();
        failedBuildingCooldowns ??= new Dictionary<BuildableObject, float>();
        recentFailureCounts ??= new Dictionary<string, int>();
        jobGiverUtilityScores ??= new Dictionary<CharacterAiBranch, float>();
        routineGroupPriorityScores ??= new Dictionary<CharacterAiBranch, float>();
        topUtilityBreakdowns ??= new List<string>();
        recentDecisionTrace ??= new List<string>();
        cachedJobGiverCandidates ??= new Dictionary<CharacterAiBranch, CharacterAiJobCandidate>();
        recentFailureCountsView ??= ReadOnlyView.Dictionary(recentFailureCounts);
        jobGiverUtilityScoresView ??= ReadOnlyView.Dictionary(jobGiverUtilityScores);
        routineGroupPriorityScoresView ??= ReadOnlyView.Dictionary(routineGroupPriorityScores);
        topUtilityBreakdownsView ??= ReadOnlyView.List(topUtilityBreakdowns);
        recentDecisionTraceView ??= ReadOnlyView.List(recentDecisionTrace);
        PruneFacilityCooldowns();
    }

    public void BeginDecisionTrace(int tick)
    {
        recentDecisionTrace ??= new List<string>();
        recentDecisionTrace.Clear();
        topUtilityBreakdowns ??= new List<string>();
        topUtilityBreakdowns.Clear();
        if (DetailedDiagnosticsEnabled)
        {
            AppendDecisionTrace($"Tick {tick}");
        }
    }

    public void RecordBtStatus(CharacterAiBranch branch, string taskName, string status)
    {
        currentBranch = branch;
        currentTask = taskName ?? string.Empty;
        currentStatus = status ?? string.Empty;
        if (DetailedDiagnosticsEnabled)
        {
            AppendDecisionTrace($"BT {branch}/{currentTask}: {TrimTrace(currentStatus)}");
        }
    }

    public void SetIntent(CharacterAiBranch branch, string intent, string taskName = "", string status = "")
    {
        currentBranch = branch;
        currentIntent = intent ?? string.Empty;
        currentTask = taskName ?? string.Empty;
        currentStatus = status ?? string.Empty;
        if (DetailedDiagnosticsEnabled)
        {
            AppendDecisionTrace(
                $"Intent {branch}/{currentTask}: {TrimTrace(currentIntent)}"
                + (string.IsNullOrWhiteSpace(currentStatus) ? string.Empty : $" [{TrimTrace(currentStatus)}]"));
        }
    }

    public void RecordJobGiverUtility(CharacterAiBranch branch, float utility, string detail)
    {
        if (branch == CharacterAiBranch.None)
        {
            return;
        }

        if (!DetailedDiagnosticsEnabled)
        {
            return;
        }

        jobGiverUtilityScores[branch] = Mathf.Max(0f, utility);
        lastJobGiverUtilitySummary = string.IsNullOrWhiteSpace(detail)
            ? $"{branch}: {utility:0.###}"
            : $"{branch}: {utility:0.###} ({detail})";
        AppendDecisionTrace($"Utility {branch}={utility:0.###} {TrimTrace(detail)}");
    }

    public void RecordSelectedJobGiverUtility(CharacterAiJobCandidate candidate)
    {
        if (!DetailedDiagnosticsEnabled)
        {
            return;
        }

        selectedJobGiverUtilitySummary = candidate.DebugSummary;
        AppendDecisionTrace($"Selected {candidate.Branch}: {TrimTrace(candidate.DebugSummary)}");
    }

    public void RecordSelectedUtilitySummary(string summary)
    {
        if (!DetailedDiagnosticsEnabled)
        {
            return;
        }

        selectedJobGiverUtilitySummary = summary ?? string.Empty;
        AppendDecisionTrace($"Selected: {TrimTrace(selectedJobGiverUtilitySummary)}");
    }

    public void RecordRoutineGroupPriority(CharacterAiBranch branch, float priority, string detail)
    {
        if (branch == CharacterAiBranch.None)
        {
            return;
        }

        if (!DetailedDiagnosticsEnabled)
        {
            return;
        }

        routineGroupPriorityScores[branch] = Mathf.Max(0f, priority);
        lastRoutineGroupPrioritySummary = string.IsNullOrWhiteSpace(detail)
            ? $"{branch}: {priority:0.###}"
            : $"{branch}: {priority:0.###} ({detail})";
        AppendDecisionTrace($"Priority {branch}={priority:0.###} {TrimTrace(detail)}");
    }

    public void RecordUtilityBreakdown(CharacterAiUtilityBreakdown breakdown)
    {
        if (breakdown == null || !DetailedDiagnosticsEnabled)
        {
            return;
        }

        string summary = breakdown.ToCompactString();
        lastUtilityBreakdownSummary = summary;
        topUtilityBreakdowns ??= new List<string>();
        topUtilityBreakdowns.Add(summary);
        topUtilityBreakdowns.Sort((left, right) => ExtractScore(right).CompareTo(ExtractScore(left)));
        while (topUtilityBreakdowns.Count > 5)
        {
            topUtilityBreakdowns.RemoveAt(topUtilityBreakdowns.Count - 1);
        }

        AppendDecisionTrace($"Breakdown {TrimTrace(summary)}");
    }

        public void RecordDecisionContext(CharacterAiDecisionContext context)
    {
        if (!DetailedDiagnosticsEnabled)
        {
            return;
        }

        lastDecisionContextSummary =
            $"가장 급한 욕구 {context.GetNeedLabel()} {context.StrongestNeedUrgency * 100f:0}%"
            + $" · 긴급 {context.EmergencyScore * 100f:0}%"
            + $" · 작업 {context.WorkPriority * 100f:0}%"
            + $" · 물류 {context.HaulPriority * 100f:0}%"
            + $" · 사냥 {context.HuntPriority * 100f:0}%"
            + $" · {context.WorldSignals.ToCompactString()}";
        AppendDecisionTrace($"Context {TrimTrace(lastDecisionContextSummary)}");
    }

    public void RecordBtDecisionTrace(string step, string status)
    {
        if (DetailedDiagnosticsEnabled)
        {
            AppendDecisionTrace($"BT-Step {TrimTrace(step)}: {TrimTrace(status)}");
        }
    }

    public void ClearJobGiverCandidateCache()
    {
        cachedJobGiverCandidates ??= new Dictionary<CharacterAiBranch, CharacterAiJobCandidate>();
        cachedJobGiverCandidates.Clear();
    }

    public void CacheJobGiverCandidate(CharacterAiJobCandidate candidate)
    {
        if (!candidate.IsValid || candidate.Branch == CharacterAiBranch.None)
        {
            return;
        }

        cachedJobGiverCandidates ??= new Dictionary<CharacterAiBranch, CharacterAiJobCandidate>();
        cachedJobGiverCandidates[candidate.Branch] = candidate;
    }

    public void RemoveJobGiverCandidateCache(CharacterAiBranch branch)
    {
        if (branch == CharacterAiBranch.None)
        {
            return;
        }

        cachedJobGiverCandidates ??= new Dictionary<CharacterAiBranch, CharacterAiJobCandidate>();
        cachedJobGiverCandidates.Remove(branch);
    }

    public bool TryGetCachedJobGiverCandidate(
        CharacterAiBranch branch,
        out CharacterAiJobCandidate candidate)
    {
        cachedJobGiverCandidates ??= new Dictionary<CharacterAiBranch, CharacterAiJobCandidate>();
        return cachedJobGiverCandidates.TryGetValue(branch, out candidate)
            && candidate.IsValid;
    }

    public void Commit(AIAction action, string intent)
    {
        if (action == null || action.actionset == null)
        {
            return;
        }

        committedAction = action.actionset;
        committedDestination = action.destination;
        minCommitUntil = Now + Mathf.Max(0f, defaultCommitSeconds);
        currentIntent = intent ?? GetActionLabel(action.actionset);
        CharacterAiNaturalnessSettingsSO settings = CharacterAiNaturalnessSettingsSO.Defaults;
        softLockIntent.Begin(
            action.actionset.Branch,
            CharacterAiUtilityText.GetIntention(action.actionset.Branch),
            currentIntent,
            action.destination,
            settings.SoftLockMinimumSeconds,
            settings.SoftLockMaximumSeconds,
            canInterrupt: true,
            now: Now);
    }

    public void RefreshCommitment(AIAction action)
    {
        if (action == null || action.actionset == null)
        {
            return;
        }

        if (committedAction == action.actionset && committedDestination == action.destination)
        {
            minCommitUntil = Mathf.Max(minCommitUntil, Now + Mathf.Max(0f, defaultCommitSeconds));
            CharacterAiNaturalnessSettingsSO settings = CharacterAiNaturalnessSettingsSO.Defaults;
            softLockIntent.Refresh(
                settings.SoftLockMinimumSeconds,
                settings.SoftLockMaximumSeconds,
                Now);
        }
    }

    public bool TryGetCommitmentBonus(AIAction action, out float bonus)
    {
        bonus = 0f;
        if (action == null || action.actionset == null || committedAction == null)
        {
            return false;
        }

        if (Now > minCommitUntil)
        {
            return false;
        }

        if (committedAction != action.actionset || committedDestination != action.destination)
        {
            return false;
        }

        if (IsDestinationInvalid(committedDestination))
        {
            ClearCommitment(CharacterAiInterruptReason.DestinationInvalid, "Committed destination is invalid.");
            return false;
        }

        bonus = commitmentScoreBonus;
        if (softLockIntent != null && softLockIntent.Matches(action))
        {
            bonus += softLockIntent.GetScoreBonus(
                Now,
                CharacterAiNaturalnessSettingsSO.Defaults.SoftLockScoreBonus);
        }

        return bonus > 0f;
    }

    public bool CanBreakCommitment(CharacterAiInterruptReason reason)
    {
        bool hardAllowed = reason == CharacterAiInterruptReason.Critical
            || reason == CharacterAiInterruptReason.DestinationInvalid
            || reason == CharacterAiInterruptReason.NoPath
            || reason == CharacterAiInterruptReason.FacilityUnavailable
            || reason == CharacterAiInterruptReason.PatienceExceeded
            || reason == CharacterAiInterruptReason.MacroGoalChanged
            || reason == CharacterAiInterruptReason.MoodImpulseChanged
            || reason == CharacterAiInterruptReason.SurvivalEmergency
            || reason == CharacterAiInterruptReason.ManualReplan
            || reason == CharacterAiInterruptReason.CurrentActionStopped;
        return hardAllowed
            && (softLockIntent == null || softLockIntent.CanBreak(reason, Now));
    }

    public void ClearCommitment(CharacterAiInterruptReason reason, string detail)
    {
        if (!CanBreakCommitment(reason))
        {
            return;
        }

        ApplyCommitmentBreak(reason, detail);
    }

    public void ForceClearCommitment(CharacterAiInterruptReason reason, string detail)
    {
        ApplyCommitmentBreak(reason, detail);
    }

    private void ApplyCommitmentBreak(CharacterAiInterruptReason reason, string detail)
    {
        committedAction = null;
        committedDestination = null;
        minCommitUntil = 0f;
        lastCommitBreakReason = string.IsNullOrWhiteSpace(detail)
            ? reason.ToString()
            : $"{reason}: {detail}";
        softLockIntent?.Break(reason, detail);
        if (DetailedDiagnosticsEnabled)
        {
            AppendDecisionTrace($"CommitBreak {TrimTrace(lastCommitBreakReason)}");
        }
    }

    public bool IsFacilityCoolingDown(BuildableObject building, out float remainingSeconds)
    {
        remainingSeconds = 0f;
        if (building == null)
        {
            return false;
        }

        PruneFacilityCooldowns();
        if (!failedBuildingCooldowns.TryGetValue(building, out float until))
        {
            return false;
        }

        remainingSeconds = Mathf.Max(0f, until - Now);
        return remainingSeconds > 0f;
    }

    public void PutFacilityOnCooldown(BuildableObject building, string reason)
    {
        if (building == null)
        {
            Debug.LogError($"{name}: Cannot put a null facility on AI cooldown.", this);
            return;
        }

        failedBuildingCooldowns[building] = Now + Mathf.Max(0.1f, failureCooldownSeconds);
        lastFailureReason = string.IsNullOrWhiteSpace(reason)
            ? $"Facility cooldown: {GetBuildingLabel(building)}"
            : $"Facility cooldown: {GetBuildingLabel(building)} - {reason}";
    }

    public void ReportActionFailure(AIActionSet actionSet, AIActionFailure failure)
    {
        if (!failure.HasFailure)
        {
            return;
        }

        string key = failure.Kind != AIActionFailureKind.Unknown
            ? failure.Kind.ToString()
            : failure.ToString();
        recentFailureCounts[key] = recentFailureCounts.TryGetValue(key, out int count) ? count + 1 : 1;
        lastFailureReason = $"{GetActionLabel(actionSet)}: {failure}";
        actor?.AiMemory?.RecordFailure(
            failure.Kind,
            lastFailureReason,
            actor != null ? actor.GetNowXY() : Vector2Int.zero);

        BuildableObject target = failure.Target;
        if (target == null && actionSet != null && actor != null && actor.Brain != null)
        {
            target = actor.Brain.bestAction != null && actor.Brain.bestAction.actionset == actionSet
                ? actor.Brain.bestAction.destination
                : null;
        }

        if (target != null && ShouldCooldownFacility(failure.Kind))
        {
            failedBuildingCooldowns[target] = Now + Mathf.Max(0.1f, failureCooldownSeconds);
        }

        if (failure.Kind == AIActionFailureKind.NoPath)
        {
            ClearCommitment(CharacterAiInterruptReason.NoPath, failure.ToString());
        }
        else if (failure.Kind == AIActionFailureKind.Destroyed)
        {
            ClearCommitment(CharacterAiInterruptReason.DestinationInvalid, failure.ToString());
        }
        else if (failure.Kind == AIActionFailureKind.DestinationOccupied
            || failure.Kind == AIActionFailureKind.NoDestination
            || failure.Kind == AIActionFailureKind.DestinationSelectionFailed)
        {
            ClearCommitment(CharacterAiInterruptReason.FacilityUnavailable, failure.ToString());
        }
    }

    public bool HasActiveMacroGoal()
    {
        if (activeMacroGoal == null)
        {
            return false;
        }

        if (activeMacroGoal.IsActive(Now))
        {
            return true;
        }

        ClearMacroGoal("Macro goal expired.");
        return false;
    }

    public bool HasActiveMoodImpulse()
    {
        if (activeMoodImpulse == null)
        {
            return false;
        }

        if (activeMoodImpulse.type == CharacterMoodImpulseType.None
            || activeMoodImpulse.strength <= 0f)
        {
            return false;
        }

        if (activeMoodImpulse.IsActive(Now))
        {
            return true;
        }

        ClearMoodImpulse("Mood impulse expired.");
        return false;
    }

    public void SetMacroGoal(CharacterMacroGoal goal)
    {
        if (goal == null)
        {
            Debug.LogError($"{name}: Tried to assign a null macro goal.", this);
            return;
        }

        activeMacroGoal ??= new CharacterMacroGoal();
        bool changed = !activeMacroGoal.IsEquivalentTo(goal);
        activeMacroGoal.type = goal.type;
        activeMacroGoal.reason = goal.reason;
        activeMacroGoal.targetFacilityId = goal.targetFacilityId;
        activeMacroGoal.targetFacilityTag = goal.targetFacilityTag;
        activeMacroGoal.validUntil = goal.validUntil;
        activeMacroGoal.source = goal.source;

        if (changed)
        {
            ClearCommitment(CharacterAiInterruptReason.MacroGoalChanged, $"Macro goal changed to {goal.type}.");
        }
    }

    public void ClearMacroGoal(string reason)
    {
        activeMacroGoal ??= new CharacterMacroGoal();
        activeMacroGoal.type = CharacterMacroGoalType.None;
        activeMacroGoal.reason = string.Empty;
        activeMacroGoal.targetFacilityId = -1;
        activeMacroGoal.targetFacilityTag = string.Empty;
        activeMacroGoal.validUntil = 0f;
        activeMacroGoal.source = string.Empty;
        currentStatus = reason ?? string.Empty;
    }

    public void SetMoodImpulse(CharacterMoodImpulse impulse)
    {
        if (impulse == null)
        {
            Debug.LogError($"{name}: Tried to assign a null mood impulse.", this);
            return;
        }

        impulse.strength = Mathf.Clamp01(impulse.strength);
        activeMoodImpulse ??= new CharacterMoodImpulse();
        bool changed = !activeMoodImpulse.IsEquivalentTo(impulse);
        activeMoodImpulse.type = impulse.type;
        activeMoodImpulse.strength = impulse.strength;
        activeMoodImpulse.targetFacilityId = impulse.targetFacilityId;
        activeMoodImpulse.targetFacilityTag = impulse.targetFacilityTag;
        activeMoodImpulse.reason = impulse.reason;
        activeMoodImpulse.validUntil = impulse.validUntil;
        activeMoodImpulse.source = impulse.source;

        if (changed && activeMoodImpulse.strength >= 0.55f)
        {
            ClearCommitment(
                CharacterAiInterruptReason.MoodImpulseChanged,
                $"Mood impulse changed to {impulse.type} ({impulse.strength:0.##}).");
        }

        if (DetailedDiagnosticsEnabled)
        {
            AppendDecisionTrace(
                $"MoodImpulse {impulse.type} strength={impulse.strength:0.###} {TrimTrace(impulse.reason)}");
        }
    }

    public void ClearMoodImpulse(string reason)
    {
        activeMoodImpulse ??= new CharacterMoodImpulse();
        activeMoodImpulse.type = CharacterMoodImpulseType.None;
        activeMoodImpulse.strength = 0f;
        activeMoodImpulse.targetFacilityId = -1;
        activeMoodImpulse.targetFacilityTag = string.Empty;
        activeMoodImpulse.reason = string.Empty;
        activeMoodImpulse.validUntil = 0f;
        activeMoodImpulse.source = string.Empty;
        if (DetailedDiagnosticsEnabled)
        {
            AppendDecisionTrace($"MoodImpulse cleared: {TrimTrace(reason)}");
        }
    }

    public int GetRecentFailureCount(AIActionFailureKind kind)
    {
        return recentFailureCounts.TryGetValue(kind.ToString(), out int count) ? count : 0;
    }

    public string GetSoftLockDebugSummary()
    {
        softLockIntent ??= new CharacterAiIntentState();
        return softLockIntent.ToDebugString(Now);
    }

    public string GetDebugSummary()
    {
        string cooldowns = string.Join(
            ", ",
            failedBuildingCooldowns
                .Where((pair) => pair.Key != null && Now < pair.Value)
                .Select((pair) => $"{GetBuildingLabel(pair.Key)} {pair.Value - Now:0.0}s"));
        string macro = HasActiveMacroGoal() ? activeMacroGoal.type.ToString() : "None";
        string moodImpulse = HasActiveMoodImpulse()
            ? $"{activeMoodImpulse.type} {activeMoodImpulse.strength:0.##}"
            : "None";
        return $"Branch: {currentBranch}/{currentTask}/{currentStatus}\n"
            + $"Intent: {currentIntent}\n"
            + $"Commit: {GetActionLabel(committedAction)} -> {GetBuildingLabel(committedDestination)} until {minCommitUntil:0.0}\n"
            + $"Macro: {macro}\n"
            + $"MoodImpulse: {moodImpulse}\n"
            + $"Context: {lastDecisionContextSummary}\n"
            + $"Utility: {lastUtilityBreakdownSummary}\n"
            + $"Cooldowns: {(string.IsNullOrWhiteSpace(cooldowns) ? "None" : cooldowns)}\n"
            + $"Route: {LastDecisionRouteSummary}\n"
            + $"Last failure: {lastFailureReason}\n"
            + $"Trace: {(string.IsNullOrWhiteSpace(LastDecisionTrace) ? "None" : LastDecisionTrace)}";
    }

    private string BuildDecisionRouteSummary()
    {
        string route = $"BT={currentBranch}/{currentTask}";
        if (!string.IsNullOrWhiteSpace(currentStatus))
        {
            route += $" status={TrimTrace(currentStatus)}";
        }

        if (!string.IsNullOrWhiteSpace(selectedJobGiverUtilitySummary))
        {
            route += $" Utility={TrimTrace(selectedJobGiverUtilitySummary)}";
        }

        if (!string.IsNullOrWhiteSpace(lastRoutineGroupPrioritySummary))
        {
            route += $" Routine={TrimTrace(lastRoutineGroupPrioritySummary)}";
        }

        if (!string.IsNullOrWhiteSpace(lastUtilityBreakdownSummary))
        {
            route += $" Breakdown={TrimTrace(lastUtilityBreakdownSummary)}";
        }

        if (HasActiveMoodImpulse())
        {
            route += $" Mood={activeMoodImpulse.type}:{activeMoodImpulse.strength:0.##}";
        }

        if (!string.IsNullOrWhiteSpace(lastCommitBreakReason))
        {
            route += $" CommitBreak={TrimTrace(lastCommitBreakReason)}";
        }

        return route;
    }

    private void AppendDecisionTrace(string entry)
    {
        if (!DetailedDiagnosticsEnabled || string.IsNullOrWhiteSpace(entry))
        {
            return;
        }

        recentDecisionTrace ??= new List<string>();
        recentDecisionTrace.Add(TrimTrace(entry));
        while (recentDecisionTrace.Count > MaxDecisionTraceEntries)
        {
            recentDecisionTrace.RemoveAt(0);
        }
    }

    private static string TrimTrace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string flattened = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return flattened.Length <= 140
            ? flattened
            : flattened.Substring(0, 137) + "...";
    }

    private static float ExtractScore(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return 0f;
        }

        int scoreIndex = summary.IndexOf("점", StringComparison.Ordinal);
        if (scoreIndex <= 0)
        {
            return 0f;
        }

        int start = scoreIndex - 1;
        while (start > 0)
        {
            char c = summary[start - 1];
            if (!char.IsDigit(c) && c != '.')
            {
                break;
            }

            start--;
        }

        return float.TryParse(summary.Substring(start, scoreIndex - start), out float score)
            ? score
            : 0f;
    }

    private void PruneFacilityCooldowns()
    {
        if (failedBuildingCooldowns.Count == 0)
        {
            return;
        }

        List<BuildableObject> expired = null;
        foreach (KeyValuePair<BuildableObject, float> pair in failedBuildingCooldowns)
        {
            if (pair.Key != null && !pair.Key.isDestroy && Now < pair.Value)
            {
                continue;
            }

            expired ??= new List<BuildableObject>();
            expired.Add(pair.Key);
        }

        if (expired == null)
        {
            return;
        }

        foreach (BuildableObject building in expired)
        {
            failedBuildingCooldowns.Remove(building);
        }
    }

    private static bool ShouldCooldownFacility(AIActionFailureKind kind)
    {
        return kind == AIActionFailureKind.DestinationOccupied
            || kind == AIActionFailureKind.NoDestination
            || kind == AIActionFailureKind.DestinationSelectionFailed
            || kind == AIActionFailureKind.NoPath
            || kind == AIActionFailureKind.Destroyed;
    }

    private static bool IsDestinationInvalid(BuildableObject destination)
    {
        return destination != null && destination.isDestroy;
    }

    private static string GetActionLabel(AIActionSet actionSet)
    {
        if (actionSet == null)
        {
            return "None";
        }

        return !string.IsNullOrWhiteSpace(actionSet.actionName)
            ? actionSet.actionName
            : actionSet.GetType().Name;
    }

    private static string GetBuildingLabel(BuildableObject building)
    {
        if (building == null)
        {
            return "None";
        }

        return building.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
            ? building.BuildingData.objectName
            : building.name;
    }
}
