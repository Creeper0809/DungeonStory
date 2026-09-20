using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using VContainer;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum LocalLlmQueueFullBehavior
{
    Fail,
    RejectQuietly,
    Drop
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class LocalLlmRequestProfile
{
    public LocalLlmRequestProfile(
        string id,
        int priority,
        float temperature = 0.4f,
        LocalLlmQueueFullBehavior queueFullBehavior = LocalLlmQueueFullBehavior.Fail,
        bool canBeEvictedForQueuePressure = false,
        float maxQueueAgeSeconds = 0f,
        bool logFailureWarnings = false,
        int maxOutputTokens = 256)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A local LLM request profile requires a stable id.", nameof(id));
        }

        Id = id.Trim();
        Priority = priority;
        Temperature = Mathf.Clamp(temperature, 0f, 2f);
        QueueFullBehavior = queueFullBehavior;
        CanBeEvictedForQueuePressure = canBeEvictedForQueuePressure;
        MaxQueueAgeSeconds = Mathf.Max(0f, maxQueueAgeSeconds);
        LogFailureWarnings = logFailureWarnings;
        MaxOutputTokens = Mathf.Max(64, maxOutputTokens);
    }

    public string Id { get; }
    public int Priority { get; }
    public float Temperature { get; }
    public LocalLlmQueueFullBehavior QueueFullBehavior { get; }
    public bool CanBeEvictedForQueuePressure { get; }
    public float MaxQueueAgeSeconds { get; }
    public bool LogFailureWarnings { get; }
    public int MaxOutputTokens { get; }

    public LocalLlmRequestProfile WithMaxQueueAge(float maxQueueAgeSeconds)
    {
        return new LocalLlmRequestProfile(
            Id,
            Priority,
            Temperature,
            QueueFullBehavior,
            CanBeEvictedForQueuePressure,
            maxQueueAgeSeconds,
            LogFailureWarnings,
            MaxOutputTokens);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class LocalLlmRequestProfiles
{
    public static readonly LocalLlmRequestProfile CharacterSkill = new LocalLlmRequestProfile(
        "CharacterSkill",
        40,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.Fail,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile CharacterSkillModuleSelection = new LocalLlmRequestProfile(
        "CharacterSkillModuleSelection",
        40,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.Fail,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile CharacterSkillLegacyV2 = new LocalLlmRequestProfile(
        "CharacterSkillLegacyV2",
        40,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.Fail,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile Persona = new LocalLlmRequestProfile("Persona", 30);
    public static readonly LocalLlmRequestProfile MacroGoal = new LocalLlmRequestProfile(
        "MacroGoal",
        20,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly);
    public static readonly LocalLlmRequestProfile MoodImpulse = new LocalLlmRequestProfile(
        "MoodImpulse",
        18,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly);
    public static readonly LocalLlmRequestProfile FacilityEvolution = new LocalLlmRequestProfile(
        "FacilityEvolution",
        16,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile FacilityEvolutionModuleSelection = new LocalLlmRequestProfile(
        "FacilityEvolutionModuleSelection",
        16,
        temperature: 0.35f,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile FacilityEvolutionLegacyV2 = new LocalLlmRequestProfile(
        "FacilityEvolutionLegacyV2",
        16,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile EvolutionHistory = new LocalLlmRequestProfile(
        "EvolutionHistory",
        17,
        temperature: 0.65f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile EvolutionHistoryLegacyV2 = new LocalLlmRequestProfile(
        "EvolutionHistoryLegacyV2",
        17,
        temperature: 0.65f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile EquipmentChoiceLegacyV2 = new LocalLlmRequestProfile(
        "EquipmentChoiceLegacyV2",
        17,
        temperature: 0f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 32);
    public static readonly LocalLlmRequestProfile EquipmentEvolutionModuleSelection = new LocalLlmRequestProfile(
        "EquipmentEvolutionModuleSelection",
        17,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 512);
    public static readonly LocalLlmRequestProfile AcquiredTrait = new LocalLlmRequestProfile(
        "AcquiredTrait",
        17,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile AcquiredTraitModuleSelection = new LocalLlmRequestProfile(
        "AcquiredTraitModuleSelection",
        17,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 512);
    public static readonly LocalLlmRequestProfile AcquiredTraitLegacyV2 = new LocalLlmRequestProfile(
        "AcquiredTraitLegacyV2",
        17,
        temperature: 0.35f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile SocialRumor = new LocalLlmRequestProfile(
        "SocialRumor",
        15,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile CharacterRecord = new LocalLlmRequestProfile(
        "CharacterRecord",
        12,
        temperature: 0.7f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly);
    public static readonly LocalLlmRequestProfile MultiPerspective = new LocalLlmRequestProfile(
        "MultiPerspective",
        18,
        temperature: 0.65f,
        queueFullBehavior: LocalLlmQueueFullBehavior.Fail,
        logFailureWarnings: false,
        maxOutputTokens: 768);
    public static readonly LocalLlmRequestProfile BubbleLine = new LocalLlmRequestProfile(
        "BubbleLine",
        10,
        queueFullBehavior: LocalLlmQueueFullBehavior.Drop,
        canBeEvictedForQueuePressure: true,
        maxQueueAgeSeconds: 3f);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum LocalLlmRequestStatus
{
    Succeeded,
    Failed,
    Dropped,
    TimedOut,
    Cancelled
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ILocalLlmRuntime
{
    bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback);
    bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback);
    bool GenerateCharacterRecordAsync(string prompt, string originalText, Action<LocalLlmResult> callback);
    bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback);
}

public enum LocalLlmRuntimeReadinessState
{
    Unknown = 0,
    Starting = 1,
    Ready = 2,
    Failed = 3
}

public readonly struct LocalLlmRuntimeReadinessSnapshot
{
    public LocalLlmRuntimeReadinessSnapshot(
        LocalLlmRuntimeReadinessState state,
        string failureReason = "")
    {
        State = state;
        FailureReason = failureReason?.Trim() ?? string.Empty;
        if (state == LocalLlmRuntimeReadinessState.Unknown)
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                "Local LLM readiness must be explicit.");
        }
        if (state == LocalLlmRuntimeReadinessState.Failed
            && FailureReason.Length == 0)
        {
            throw new ArgumentException(
                "Failed local LLM readiness requires an explicit reason.",
                nameof(failureReason));
        }
    }

    public LocalLlmRuntimeReadinessState State { get; }
    public string FailureReason { get; }
}

public interface ILocalLlmRuntimeReadiness
{
    LocalLlmRuntimeReadinessSnapshot CaptureReadiness();
}

/// <summary>
/// An implementation returning true owns the complete lifetime of every
/// request it accepts: queue wait, transport timeout, terminal failure, and
/// cancellation. Each accepted request must invoke its callback exactly once.
/// </summary>
public interface ILocalLlmAcceptedRequestCompletionOwner
{
    bool OwnsAcceptedRequestCompletion { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICorrelatedCharacterSkillLlmRuntime
{
    bool GenerateCharacterSkillAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    void CancelCharacterSkillRequest(string requestKey);
}

public interface ICharacterSkillModuleSelectionLlmRuntime
{
    bool GenerateCharacterSkillModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

/// <summary>
/// A runtime that can return every unresolved CharacterSkill module-selection
/// candidate in one exact-count response. Legacy/test runtimes may continue to
/// implement the single-selection interface and retain the service watchdog.
/// </summary>
public interface ICharacterSkillModuleSelectionBatchLlmRuntime
{
    bool GenerateCharacterSkillModuleSelectionBatchAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICorrelatedEvolutionHistoryLlmRuntime
{
    bool GenerateEvolutionHistoryAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    bool GenerateEvolutionHistoryLegacyV2Async(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    void CancelEvolutionHistoryRequest(string requestKey);
}

public interface ICorrelatedEquipmentEvolutionModuleSelectionLlmRuntime
{
    bool GenerateEquipmentEvolutionModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

public interface ICorrelatedFacilityEvolutionModuleSelectionLlmRuntime
{
    bool GenerateFacilityEvolutionModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

public interface ICorrelatedAcquiredTraitLlmRuntime
{
    bool GenerateAcquiredTraitAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    void CancelAcquiredTraitRequest(string requestKey);
}

public interface ICorrelatedAcquiredTraitModuleSelectionLlmRuntime
{
    bool GenerateAcquiredTraitModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);
}

public interface IConstrainedEquipmentChoiceLlmRuntime
{
    bool GenerateEquipmentChoiceAsync(
        string requestKey,
        string prompt,
        int candidateCount,
        Action<LocalLlmChoiceResult> callback);
}

public interface IMultiPerspectiveNarrativeLlmRuntime
{
    bool GenerateMultiPerspectiveAsync(
        NarrativeMultiPerspectiveRequest request,
        string prompt,
        Action<LocalLlmResult> callback);
}

public readonly struct LocalLlmChoiceResult
{
    public LocalLlmChoiceResult(bool succeeded, int selectedIndex, string error)
    {
        Succeeded = succeeded;
        SelectedIndex = selectedIndex;
        Error = error ?? string.Empty;
    }

    public bool Succeeded { get; }
    public int SelectedIndex { get; }
    public string Error { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct LocalLlmResult
{
    public LocalLlmResult(
        LocalLlmRequestStatus status,
        string content,
        string error,
        string originalText,
        NarrativeGenerationTrace narrativeTrace = null)
    {
        Status = status;
        Content = content ?? string.Empty;
        Error = error ?? string.Empty;
        OriginalText = originalText ?? string.Empty;
        NarrativeTrace = narrativeTrace;
    }

    public LocalLlmRequestStatus Status { get; }
    public string Content { get; }
    public string Error { get; }
    public string OriginalText { get; }
    public NarrativeGenerationTrace NarrativeTrace { get; }
    public bool IsSuccess => Status == LocalLlmRequestStatus.Succeeded;
    public bool IsCancelled => Status == LocalLlmRequestStatus.Cancelled;
}

internal sealed class LocalLlmQueuedRequest : IContextAwareLlmRequest
{
    public LocalLlmQueuedRequest(
        LocalLlmRequestProfile profile,
        string prompt,
        string originalText,
        float timeoutSeconds,
        float enqueuedAt,
        string correlationId,
        NarrativeSchedulingMetadata scheduling,
        Action<LocalLlmResult> callback,
        bool isEquipmentChoice = false,
        int candidateCount = 0)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        OriginalText = originalText ?? string.Empty;
        TimeoutSeconds = Mathf.Max(0.1f, timeoutSeconds);
        EnqueuedAt = enqueuedAt;
        CorrelationId = correlationId ?? string.Empty;
        Scheduling = scheduling ?? NarrativeSchedulingMetadata.CreateDefault(
            profile,
            prompt,
            correlationId,
            enqueuedAt);
        Callback = callback;
        IsEquipmentChoice = isEquipmentChoice;
        CandidateCount = candidateCount;
    }

    public LocalLlmRequestProfile Profile { get; }
    public string Prompt { get; }
    public string OriginalText { get; }
    public float TimeoutSeconds { get; }
    public float EnqueuedAt { get; }
    public string CorrelationId { get; }
    public NarrativeSchedulingMetadata Scheduling { get; }
    public bool IsEquipmentChoice { get; }
    public int CandidateCount { get; }
    int IContextAwareLlmRequest.Priority => Profile.Priority;
    public float StartedAt { get; private set; } = -1f;
    private Action<LocalLlmResult> Callback { get; set; }
    private UnityWebRequest ActiveWebRequest { get; set; }

    public bool IsCompleted { get; private set; }

    public bool TryTakeCallback(out Action<LocalLlmResult> callback)
    {
        if (IsCompleted)
        {
            callback = null;
            return false;
        }

        IsCompleted = true;
        callback = Callback;
        Callback = null;
        return true;
    }

    public void Attach(UnityWebRequest request, float startedAt)
    {
        ActiveWebRequest = request;
        StartedAt = startedAt;
    }

    public void Detach()
    {
        ActiveWebRequest = null;
    }

    public void Abort()
    {
        ActiveWebRequest?.Abort();
    }
}

[DisallowMultipleComponent]
[DrawWithUnity]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class LocalLlmRequestQueue :
    SerializedMonoBehaviour,
    ILocalLlmRuntime,
    ILocalLlmRuntimeReadiness,
    ILocalLlmAcceptedRequestCompletionOwner,
    ICorrelatedCharacterSkillLlmRuntime,
    ICharacterSkillModuleSelectionLlmRuntime,
    ICharacterSkillModuleSelectionBatchLlmRuntime,
    ICorrelatedEvolutionHistoryLlmRuntime,
    ICorrelatedEquipmentEvolutionModuleSelectionLlmRuntime,
    ICorrelatedFacilityEvolutionModuleSelectionLlmRuntime,
    ICorrelatedAcquiredTraitLlmRuntime,
    ICorrelatedAcquiredTraitModuleSelectionLlmRuntime,
    IConstrainedEquipmentChoiceLlmRuntime,
    IMultiPerspectiveNarrativeLlmRuntime
{
    private const float AcquiredTraitTimeoutSeconds = 60f;

    [SerializeField] private string endpointUrl;
    [SerializeField] private string modelName = "DungeonStory-Qwen3-1.7B-Q4_K_M";
    [SerializeField, Min(1)] private int maxQueueSize = 64;
    [SerializeField, Range(1, 2)] private int maxConcurrentRequests = 2;
    [SerializeField, Min(0.1f)] private float personaTimeoutSeconds = 20f;
    [SerializeField, Min(0.1f)] private float characterSkillTimeoutSeconds = 60f;
    [SerializeField, Min(0.1f)] private float macroGoalTimeoutSeconds = 12f;
    [SerializeField, Min(0.1f)] private float moodImpulseTimeoutSeconds = 8f;
    [SerializeField, Min(0.1f)] private float socialRumorTimeoutSeconds = 8f;
    [SerializeField, Min(0.1f)] private float facilityEvolutionTimeoutSeconds = 10f;
    [SerializeField, Min(0.1f)] private float characterRecordTimeoutSeconds = 8f;
    [SerializeField, Min(0.1f)] private float bubbleTimeoutSeconds = 4f;
    [SerializeField, Min(0.1f)] private float bubbleMaxQueueAgeSeconds = 3f;
    [FormerlySerializedAs("droppedBubbleCount")]
    [SerializeField, ReadOnly] private int droppedEphemeralRequestCount;
    [SerializeField, ReadOnly] private int timeoutCount;
    [SerializeField, ReadOnly] private string lastError;
    [SerializeField, ReadOnly] private string lastCompletionDiagnostic;
    [SerializeField, ReadOnly] private StructuredOutputCapability structuredOutputCapability;
    [SerializeField, ReadOnly] private string lastSchemaId;
    [SerializeField, ReadOnly] private int lastSchemaVersion;
    [SerializeField, ReadOnly] private string lastSchemaHash;
    [SerializeField, ReadOnly] private NarrativeQualityVerdict lastNarrativeQualityVerdict;
    [SerializeField, ReadOnly] private bool suppressWarningLogsForDebug;

    private readonly List<LocalLlmQueuedRequest> queue = new List<LocalLlmQueuedRequest>();
    private readonly HashSet<LocalLlmQueuedRequest> runningRequests = new HashSet<LocalLlmQueuedRequest>();
    private IUiClock uiClock;
    private DungeonStoryHostStructuredChatBackend structuredBackend;
    private readonly INarrativeTextQualityGate narrativeQualityGate =
        new NarrativeTextQualityGate();
    private DungeonStoryLlmHostProcess localHost;
    private Task<HostStartupResult> hostStartupTask;
    private PrefixAffinityKey currentAffinityKey;
    private int currentAffinityBurst;
    private bool isSuspended;

    public int QueuedCount => queue.Count;
    public int RunningCount => runningRequests.Count;
    public int DroppedEphemeralRequestCount => droppedEphemeralRequestCount;
    public int DroppedBubbleCount => droppedEphemeralRequestCount;
    public int TimeoutCount => timeoutCount;
    public int MaxQueueSize => maxQueueSize;
    public string LastError => lastError;
    public string LastCompletionDiagnostic => lastCompletionDiagnostic;
    public StructuredOutputCapability StructuredOutputCapability => structuredOutputCapability;
    public string LastSchemaId => lastSchemaId;
    public int LastSchemaVersion => lastSchemaVersion;
    public string LastSchemaHash => lastSchemaHash;
    public NarrativeQualityVerdict LastNarrativeQualityVerdict =>
        lastNarrativeQualityVerdict;
    public bool HasConfiguredEndpoint => !string.IsNullOrWhiteSpace(endpointUrl)
        && !string.IsNullOrWhiteSpace(modelName);
    public bool IsBundledModelStarting => hostStartupTask != null && !hostStartupTask.IsCompleted;
    public bool IsBundledModelRunning => localHost?.IsRunning == true;
    public string BundledModelVersion => localHost?.ModelVersion ?? string.Empty;
    public string BundledModelTrainingState => localHost?.TrainingState ?? string.Empty;
    public bool IsBundledModelReleaseCertified => localHost?.ReleaseCertified == true;
    public bool OwnsAcceptedRequestCompletion => true;
    private float Now => uiClock != null
        ? uiClock.Time
        : throw new InvalidOperationException(
            "LocalLlmRequestQueue requires a scoped IUiClock before use.");

    [Inject]
    public void Construct(IUiClock uiClock)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public string PeekNextProfileIdForDebug()
    {
        return queue.Count > 0
            ? queue[FindNextRequestIndex()].Profile.Id
            : string.Empty;
    }

    public string GetRequestDiagnosticsForDebug()
    {
        float now = Now;
        string running = string.Join(",", runningRequests
            .Where(request => request != null)
            .Select(request => $"{request.Profile.Id}:age={Mathf.Max(0f, now - request.StartedAt):0.0}s/timeout={request.TimeoutSeconds:0.0}s/prompt={request.Prompt.Length}"));
        string waiting = string.Join(",", queue
            .Where(request => request != null)
            .Take(8)
            .Select(request => $"{request.Profile.Id}:wait={Mathf.Max(0f, now - request.EnqueuedAt):0.0}s/prompt={request.Prompt.Length}"));
        return $"running=[{running}] queued=[{waiting}] last=[{lastCompletionDiagnostic}]";
    }

    public LocalLlmRuntimeReadinessSnapshot CaptureReadiness()
    {
        if (isSuspended)
        {
            return new LocalLlmRuntimeReadinessSnapshot(
                LocalLlmRuntimeReadinessState.Failed,
                "Local LLM queue is suspended.");
        }
        if (!isActiveAndEnabled)
        {
            return new LocalLlmRuntimeReadinessSnapshot(
                LocalLlmRuntimeReadinessState.Failed,
                "Local LLM queue is not active.");
        }
        if (hostStartupTask != null)
        {
            return new LocalLlmRuntimeReadinessSnapshot(
                LocalLlmRuntimeReadinessState.Starting);
        }
        if (localHost != null && !localHost.IsRunning)
        {
            return new LocalLlmRuntimeReadinessSnapshot(
                LocalLlmRuntimeReadinessState.Failed,
                string.IsNullOrWhiteSpace(localHost.LastError)
                    ? "DungeonStory narrative host stopped before request dispatch."
                    : localHost.LastError);
        }
        if (HasConfiguredEndpoint)
        {
            return new LocalLlmRuntimeReadinessSnapshot(
                LocalLlmRuntimeReadinessState.Ready);
        }

        return new LocalLlmRuntimeReadinessSnapshot(
            LocalLlmRuntimeReadinessState.Failed,
            string.IsNullOrWhiteSpace(lastError)
                ? "Local LLM endpoint or model is not configured."
                : lastError);
    }

    public void ConfigureBubblePolicyForDebug(float timeoutSeconds, float maxQueueAgeSeconds)
    {
        bubbleTimeoutSeconds = Mathf.Max(0.1f, timeoutSeconds);
        bubbleMaxQueueAgeSeconds = Mathf.Max(0.1f, maxQueueAgeSeconds);
    }

    public void ConfigureTimeoutsForDebug(
        float personaSeconds,
        float macroGoalSeconds,
        float socialRumorSeconds,
        float bubbleSeconds,
        float moodImpulseSeconds = 8f,
        float facilityEvolutionSeconds = 10f,
        float characterRecordSeconds = 8f,
        float characterSkillSeconds = 30f)
    {
        personaTimeoutSeconds = Mathf.Max(0.1f, personaSeconds);
        macroGoalTimeoutSeconds = Mathf.Max(0.1f, macroGoalSeconds);
        moodImpulseTimeoutSeconds = Mathf.Max(0.1f, moodImpulseSeconds);
        socialRumorTimeoutSeconds = Mathf.Max(0.1f, socialRumorSeconds);
        facilityEvolutionTimeoutSeconds = Mathf.Max(0.1f, facilityEvolutionSeconds);
        characterRecordTimeoutSeconds = Mathf.Max(0.1f, characterRecordSeconds);
        characterSkillTimeoutSeconds = Mathf.Max(0.1f, characterSkillSeconds);
        bubbleTimeoutSeconds = Mathf.Max(0.1f, bubbleSeconds);
    }

    public void ClearForDebug()
    {
        ResetTransientState("Local LLM queue was cleared for debug.", remainSuspended: false);
    }

    public void AbortAllForDebug()
    {
        ResetTransientState("Local LLM requests were aborted for debug.", remainSuspended: false);
    }

    public void SetWarningLogsSuppressedForDebug(bool value)
    {
        suppressWarningLogsForDebug = value;
    }

    private void OnEnable()
    {
        isSuspended = false;
        structuredBackend = new DungeonStoryHostStructuredChatBackend(
            () => localHost?.SessionToken ?? string.Empty);
        endpointUrl = string.Empty;
        string streamingAssetsPath = Application.streamingAssetsPath;
        hostStartupTask = Task.Run(() =>
        {
            bool started = DungeonStoryLlmHostProcess.TryStart(
                streamingAssetsPath,
                out DungeonStoryLlmHostProcess host,
                out string error);
            return new HostStartupResult(started, host, error);
        });
    }

    private void OnDisable()
    {
        ResetTransientState("Local LLM queue was disabled.", remainSuspended: true);
        Task<HostStartupResult> pendingStartup = hostStartupTask;
        hostStartupTask = null;
        if (pendingStartup != null)
        {
            pendingStartup.ContinueWith(task =>
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    task.Result.Host?.Dispose();
                }
            }, TaskScheduler.Default);
        }
        localHost?.Dispose();
        localHost = null;
        endpointUrl = string.Empty;
    }

    private void ResetTransientState(string reason, bool remainSuspended)
    {
        isSuspended = true;
        List<LocalLlmQueuedRequest> cancelled = new List<LocalLlmQueuedRequest>(
            queue.Count + runningRequests.Count);
        cancelled.AddRange(queue);
        cancelled.AddRange(runningRequests);

        StopAllCoroutines();
        queue.Clear();
        runningRequests.Clear();

        LocalLlmResult result = new LocalLlmResult(
            LocalLlmRequestStatus.Cancelled,
            string.Empty,
            reason,
            string.Empty);
        foreach (LocalLlmQueuedRequest request in cancelled)
        {
            request.Abort();
            Complete(request, new LocalLlmResult(
                result.Status,
                result.Content,
                result.Error,
                request.OriginalText),
                logFailure: false);
        }

        droppedEphemeralRequestCount = 0;
        timeoutCount = 0;
        lastError = string.Empty;
        lastCompletionDiagnostic = string.Empty;
        suppressWarningLogsForDebug = false;
        isSuspended = remainSuspended;
    }

    private void Update()
    {
        PublishHostStartupIfReady();
        if (localHost != null && !localHost.IsRunning)
        {
            lastError = string.IsNullOrWhiteSpace(localHost.LastError)
                ? "DungeonStory narrative host stopped; deterministic prose is active."
                : localHost.LastError;
            localHost.Dispose();
            localHost = null;
            endpointUrl = string.Empty;
        }
        DropExpiredRequests();
        if (hostStartupTask != null)
        {
            // Persistent narrative requests may be queued during scene startup.
            // Do not fail them merely because model hashing/loading is still in progress.
            return;
        }
        while (RunningCount < Mathf.Max(1, maxConcurrentRequests) && queue.Count > 0)
        {
            int index = FindNextRequestIndex();
            if (index < 0
                || !ContextAwareLlmScheduler.CanDispatch(queue[index], Now, queue.Count))
            {
                break;
            }
            LocalLlmQueuedRequest request = queue[index];
            queue.RemoveAt(index);
            if (request.Scheduling != null
                && request.Scheduling.AffinityKey == currentAffinityKey)
            {
                currentAffinityBurst++;
            }
            else
            {
                currentAffinityKey = request.Scheduling?.AffinityKey ?? default;
                currentAffinityBurst = 1;
            }
            StartCoroutine(ProcessRequest(request));
        }
    }

    private void PublishHostStartupIfReady()
    {
        if (hostStartupTask == null || !hostStartupTask.IsCompleted)
        {
            return;
        }

        Task<HostStartupResult> completed = hostStartupTask;
        hostStartupTask = null;
        if (completed.IsFaulted)
        {
            lastError = "DungeonStory narrative host startup failed closed: "
                + completed.Exception?.GetBaseException().Message;
            return;
        }

        HostStartupResult result = completed.Result;
        if (!result.Started || result.Host == null)
        {
            lastError = result.Error;
            return;
        }

        localHost = result.Host;
        endpointUrl = localHost.Endpoint;
        modelName = "DungeonStory-Qwen3-1.7B-Q4_K_M";
    }

    private readonly struct HostStartupResult
    {
        public HostStartupResult(
            bool started,
            DungeonStoryLlmHostProcess host,
            string error)
        {
            Started = started;
            Host = host;
            Error = error ?? string.Empty;
        }

        public bool Started { get; }
        public DungeonStoryLlmHostProcess Host { get; }
        public string Error { get; }
    }

    public bool EnqueuePersona(string prompt, Action<LocalLlmResult> callback)
    {
        return GeneratePersonaAsync(prompt, callback);
    }

    public bool EnqueueCharacterSkill(string prompt, Action<LocalLlmResult> callback)
    {
        return GenerateCharacterSkillAsync(prompt, callback);
    }

    public bool EnqueueMacroGoal(string prompt, Action<LocalLlmResult> callback)
    {
        return GenerateMacroGoalAsync(prompt, callback);
    }

    public bool EnqueueMoodImpulse(string prompt, Action<LocalLlmResult> callback)
    {
        return GenerateMoodImpulseAsync(prompt, callback);
    }

    public bool EnqueueSocialRumor(string prompt, Action<LocalLlmResult> callback)
    {
        return GenerateSocialRumorAsync(prompt, callback);
    }

    public bool EnqueueFacilityEvolution(string prompt, Action<LocalLlmResult> callback)
    {
        return GenerateFacilityEvolutionAsync(prompt, callback);
    }

    public bool EnqueueCharacterRecord(string prompt, string originalText, Action<LocalLlmResult> callback)
    {
        return GenerateCharacterRecordAsync(prompt, originalText, callback);
    }

    public bool EnqueueBubbleLine(string prompt, string originalText, Action<LocalLlmResult> callback)
    {
        return GenerateBubbleLineAsync(prompt, originalText, callback);
    }

    public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback)
    {
        return Enqueue(LocalLlmRequestProfiles.Persona, prompt, string.Empty, personaTimeoutSeconds, callback);
    }

    public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback)
    {
        return Enqueue(
            LocalLlmRequestProfiles.CharacterSkill,
            prompt,
            string.Empty,
            characterSkillTimeoutSeconds,
            callback);
    }

    public bool GenerateCharacterSkillAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        return Enqueue(
            LocalLlmRequestProfiles.CharacterSkill,
            prompt,
            string.Empty,
            characterSkillTimeoutSeconds,
            callback,
            requestKey);
    }

    public bool GenerateCharacterSkillModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        return Enqueue(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection,
            prompt,
            string.Empty,
            characterSkillTimeoutSeconds,
            callback,
            requestKey);
    }

    public bool GenerateCharacterSkillModuleSelectionBatchAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        return Enqueue(
            LocalLlmRequestProfiles.CharacterSkillModuleSelection,
            prompt,
            string.Empty,
            characterSkillTimeoutSeconds,
            callback,
            requestKey);
    }

    public void CancelCharacterSkillRequest(string requestKey)
    {
        CancelCorrelatedRequest(
            requestKey,
            "Character skill request was cancelled.");
    }

    public bool GenerateEvolutionHistoryAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        return Enqueue(
            LocalLlmRequestProfiles.EvolutionHistory,
            prompt,
            string.Empty,
            facilityEvolutionTimeoutSeconds,
            callback,
            requestKey);
    }

    public bool GenerateEvolutionHistoryLegacyV2Async(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        return Enqueue(
            LocalLlmRequestProfiles.EvolutionHistoryLegacyV2,
            prompt,
            string.Empty,
            facilityEvolutionTimeoutSeconds,
            callback,
            requestKey);
    }

    public bool GenerateEquipmentChoiceAsync(
        string requestKey,
        string prompt,
        int candidateCount,
        Action<LocalLlmChoiceResult> callback)
    {
        ChoicePromptDiagnostic canonical = default;
        string canonicalError = string.Empty;
        if (candidateCount < 2 || candidateCount > 3
            || !ChoicePromptCanonicalizer.TryCanonicalize(
                prompt,
                out canonical,
                out canonicalError))
        {
            callback?.Invoke(new LocalLlmChoiceResult(false, -1,
                candidateCount < 2 || candidateCount > 3
                    ? "EquipmentChoice.CandidateCountInvalid"
                    : "EquipmentChoice.PromptInvalid: " + canonicalError));
            return false;
        }

        if (isSuspended || !isActiveAndEnabled || !HasConfiguredEndpoint)
        {
            callback?.Invoke(new LocalLlmChoiceResult(
                false,
                -1,
                "EquipmentChoice.RuntimeUnavailable"));
            return false;
        }

        LocalLlmRequestProfile profile = LocalLlmRequestProfiles.EquipmentChoiceLegacyV2;
        float now = Now;
        NarrativeSchedulingMetadata scheduling = NarrativeSchedulingMetadata.CreateDefault(
            profile,
            canonical.Prompt,
            requestKey,
            now);
        queue.Add(new LocalLlmQueuedRequest(
            profile,
            canonical.Prompt,
            string.Empty,
            1f,
            now,
            requestKey,
            scheduling,
            result =>
            {
                int selected = -1;
                string parseError = string.Empty;
                bool valid = result.IsSuccess
                    && EquipmentChoiceResultParser.TryParse(
                        result.Content,
                        candidateCount,
                        out selected,
                        out parseError);
                callback?.Invoke(new LocalLlmChoiceResult(
                    valid,
                    valid ? selected : -1,
                    valid
                        ? string.Empty
                        : !result.IsSuccess
                            ? result.Error
                            : parseError));
            },
            isEquipmentChoice: true,
            candidateCount: candidateCount));
        return true;
    }

    public void CancelEvolutionHistoryRequest(string requestKey)
    {
        CancelCorrelatedRequest(
            requestKey,
            "Evolution history request was cancelled.");
    }

    public bool GenerateEquipmentEvolutionModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        string canonicalKey = requestKey?.Trim() ?? string.Empty;
        if (canonicalKey.Length == 0
            || !string.Equals(requestKey, canonicalKey, StringComparison.Ordinal))
            return false;
        if (queue.Concat(runningRequests).Any(request => request != null
                && string.Equals(request.CorrelationId, canonicalKey, StringComparison.Ordinal)))
            return false;
        return Enqueue(
            LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection,
            prompt,
            string.Empty,
            AcquiredTraitTimeoutSeconds,
            callback,
            canonicalKey);
    }

    public bool GenerateFacilityEvolutionModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        string canonicalKey = requestKey?.Trim() ?? string.Empty;
        if (canonicalKey.Length == 0
            || !string.Equals(requestKey, canonicalKey, StringComparison.Ordinal))
            return false;
        if (queue.Concat(runningRequests).Any(request => request != null
                && string.Equals(request.CorrelationId, canonicalKey, StringComparison.Ordinal)))
            return false;
        return Enqueue(
            LocalLlmRequestProfiles.FacilityEvolutionModuleSelection,
            prompt,
            string.Empty,
            facilityEvolutionTimeoutSeconds,
            callback,
            canonicalKey);
    }

    public bool GenerateAcquiredTraitAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        string canonicalKey = requestKey?.Trim() ?? string.Empty;
        if (canonicalKey.Length == 0
            || !string.Equals(requestKey, canonicalKey, StringComparison.Ordinal))
        {
            InvokeCallbackSafely(callback, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                "AcquiredTrait request key is missing or non-canonical.",
                string.Empty));
            return false;
        }
        if (queue.Concat(runningRequests).Any(request => request != null
                && string.Equals(
                    request.CorrelationId,
                    canonicalKey,
                    StringComparison.Ordinal)))
        {
            lastError = "AcquiredTrait: Skipped - correlated request is already pending.";
            return false;
        }

        return Enqueue(
            LocalLlmRequestProfiles.AcquiredTrait,
            prompt,
            string.Empty,
            AcquiredTraitTimeoutSeconds,
            callback,
            canonicalKey);
    }

    public void CancelAcquiredTraitRequest(string requestKey)
    {
        CancelCorrelatedRequest(
            requestKey,
            "Acquired-trait request was cancelled.");
    }

    public bool GenerateAcquiredTraitModuleSelectionAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        string canonicalKey = requestKey?.Trim() ?? string.Empty;
        if (canonicalKey.Length == 0
            || !string.Equals(requestKey, canonicalKey, StringComparison.Ordinal))
        {
            InvokeCallbackSafely(callback, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                "AcquiredTraitModuleSelection request key is missing or non-canonical.",
                string.Empty));
            return false;
        }
        if (queue.Concat(runningRequests).Any(request => request != null
                && string.Equals(request.CorrelationId, canonicalKey, StringComparison.Ordinal)))
        {
            lastError = "AcquiredTraitModuleSelection: Skipped - correlated request is already pending.";
            return false;
        }
        return Enqueue(
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection,
            prompt,
            string.Empty,
            AcquiredTraitTimeoutSeconds,
            callback,
            canonicalKey);
    }

    public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback)
    {
        return Enqueue(LocalLlmRequestProfiles.MacroGoal, prompt, string.Empty, macroGoalTimeoutSeconds, callback);
    }

    public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback)
    {
        return Enqueue(LocalLlmRequestProfiles.MoodImpulse, prompt, string.Empty, moodImpulseTimeoutSeconds, callback);
    }

    public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback)
    {
        return Enqueue(LocalLlmRequestProfiles.SocialRumor, prompt, string.Empty, socialRumorTimeoutSeconds, callback);
    }

    public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback)
    {
        return Enqueue(LocalLlmRequestProfiles.FacilityEvolution, prompt, string.Empty, facilityEvolutionTimeoutSeconds, callback);
    }

    private void CancelCorrelatedRequest(string requestKey, string reason)
    {
        if (string.IsNullOrWhiteSpace(requestKey))
        {
            return;
        }

        LocalLlmQueuedRequest[] cancelled = queue
            .Concat(runningRequests)
            .Where(request => request != null
                && string.Equals(
                    request.CorrelationId,
                    requestKey,
                    StringComparison.Ordinal))
            .Distinct()
            .ToArray();
        foreach (LocalLlmQueuedRequest request in cancelled)
        {
            queue.Remove(request);
            request.Abort();
            Complete(request, new LocalLlmResult(
                    LocalLlmRequestStatus.Cancelled,
                    string.Empty,
                    reason ?? string.Empty,
                    request.OriginalText),
                logFailure: false);
        }
    }

    public bool GenerateCharacterRecordAsync(
        string prompt,
        string originalText,
        Action<LocalLlmResult> callback)
    {
        return Enqueue(
            LocalLlmRequestProfiles.CharacterRecord,
            prompt,
            originalText,
            characterRecordTimeoutSeconds,
            callback);
    }

    public bool GenerateMultiPerspectiveAsync(
        NarrativeMultiPerspectiveRequest request,
        string prompt,
        Action<LocalLlmResult> callback)
    {
        string validationError = "Multi-perspective request is missing.";
        if (request == null || !request.TryValidate(out validationError))
        {
            InvokeCallbackSafely(callback, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                validationError,
                string.Empty));
            return false;
        }

        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.MultiPerspective.Id);
        NarrativeViewpointRequest first = request.viewpoints[0];
        NarrativeSchedulingMetadata scheduling = new NarrativeSchedulingMetadata
        {
            AffinityKey = new PrefixAffinityKey(
                schema.Hash,
                first.eventId,
                NarrativeSchedulingMetadata.StableUtf8Hash(request.sharedFactPacket),
                first.knowledgeSnapshotVersion,
                request.CultureStyleVersion),
            Persistent = true,
            Urgent = false,
            ExpiresAt = float.PositiveInfinity
        };
        return Enqueue(
            LocalLlmRequestProfiles.MultiPerspective,
            prompt,
            string.Empty,
            characterRecordTimeoutSeconds,
            callback,
            first.eventId,
            scheduling);
    }

    public bool GenerateBubbleLineAsync(string prompt, string originalText, Action<LocalLlmResult> callback)
    {
        return Enqueue(
            LocalLlmRequestProfiles.BubbleLine.WithMaxQueueAge(bubbleMaxQueueAgeSeconds),
            prompt,
            originalText,
            bubbleTimeoutSeconds,
            callback);
    }

    public bool Enqueue(
        LocalLlmRequestProfile profile,
        string prompt,
        string originalText,
        float timeoutSeconds,
        Action<LocalLlmResult> callback,
        string correlationId = "",
        NarrativeSchedulingMetadata scheduling = null)
    {
        string profileId = profile != null ? profile.Id : "Unknown";
        if (isSuspended || !isActiveAndEnabled)
        {
            lastError = $"{profileId}: Skipped - Local LLM queue is not active.";
            return false;
        }

        if (profile == null)
        {
            lastError = "Unknown: Failed - Local LLM request profile is null.";
            InvokeCallbackSafely(callback, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                "Local LLM request profile is null.",
                originalText));
            return false;
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            InvokeCallbackSafely(callback, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                "LLM prompt is empty.",
                originalText));
            return false;
        }

        if (queue.Count >= maxQueueSize && !TryDropLowestPriorityEvictableRequest())
        {
            if (profile.QueueFullBehavior == LocalLlmQueueFullBehavior.Drop)
            {
                droppedEphemeralRequestCount++;
                InvokeCallbackSafely(callback, new LocalLlmResult(
                    LocalLlmRequestStatus.Dropped,
                    string.Empty,
                    $"{profile.Id} request dropped because the LLM queue is full.",
                    originalText));
                return false;
            }

            if (profile.QueueFullBehavior == LocalLlmQueueFullBehavior.RejectQuietly)
            {
                lastError = $"{profile.Id}: Skipped - LLM queue is full.";
                return false;
            }

            lastError = $"{profile.Id}: Failed - LLM queue is full and no queued request can be evicted.";
            InvokeCallbackSafely(callback, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                "LLM queue is full and no queued request can be evicted.",
                originalText));
            if (profile.LogFailureWarnings)
            {
                LogWarningIfAllowed(lastError);
            }
            return false;
        }

        string contextualPrompt = prompt.Contains(NarrativeRequestContext.BeginMarker)
            ? prompt
            : NarrativeCultureStyleCatalog.Create(
                    profile.Id,
                    string.Empty,
                    requireCharacterFact: false,
                    requireMotif: LlmStaticSchemaCatalog.Require(profile.Id)
                        .PersistentNarrative)
                .AppendToPrompt(prompt);
        queue.Add(new LocalLlmQueuedRequest(
            profile,
            contextualPrompt,
            originalText,
            timeoutSeconds,
            Now,
            correlationId,
            scheduling,
            callback));
        return true;
    }

    private IEnumerator ProcessRequest(LocalLlmQueuedRequest request)
    {
        if (request == null || request.IsCompleted || !runningRequests.Add(request))
        {
            yield break;
        }

        try
        {
            if (request.IsEquipmentChoice)
            {
                yield return ProcessEquipmentChoiceRequest(request);
                yield break;
            }

            if (!HasConfiguredEndpoint)
            {
                Complete(request, new LocalLlmResult(
                    LocalLlmRequestStatus.Failed,
                    string.Empty,
                    "Local LLM endpoint or model is not configured.",
                    request.OriginalText));
                yield break;
            }

            LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(request.Profile.Id);
            lastSchemaId = schema.ProfileId;
            lastSchemaVersion = schema.Version;
            lastSchemaHash = schema.Hash;
            int maximumAttempts = schema.PersistentNarrative ? 2 : 1;
            string activePrompt = request.Prompt;
            for (int attempt = 0; attempt < maximumAttempts; attempt++)
            {
                using UnityWebRequest webRequest = structuredBackend.BuildRequest(
                    endpointUrl,
                    modelName,
                    request.Profile,
                    schema,
                    activePrompt);
                request.Attach(webRequest, Now);
                webRequest.timeout = Mathf.CeilToInt(request.TimeoutSeconds);
                UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
                float timeoutAt = Now + Mathf.Max(0.1f, request.TimeoutSeconds);
                while (!operation.isDone
                    && !request.IsCompleted
                    && Now < timeoutAt)
                {
                    yield return null;
                }

                if (request.IsCompleted)
                {
                    yield break;
                }

                if (!operation.isDone)
                {
                    webRequest.Abort();
                    Complete(request, new LocalLlmResult(
                        LocalLlmRequestStatus.TimedOut,
                        string.Empty,
                        "Request timeout",
                        request.OriginalText));
                    yield break;
                }

                if (webRequest.result == UnityWebRequest.Result.ConnectionError
                    || webRequest.result == UnityWebRequest.Result.ProtocolError
                    || webRequest.result == UnityWebRequest.Result.DataProcessingError)
                {
                    structuredOutputCapability = StructuredOutputCapability.Unavailable;
                    LocalLlmRequestStatus status = webRequest.error != null
                        && webRequest.error.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                            ? LocalLlmRequestStatus.TimedOut
                            : LocalLlmRequestStatus.Failed;
                    Complete(request, new LocalLlmResult(
                        status,
                        string.Empty,
                        webRequest.error,
                        request.OriginalText));
                    yield break;
                }

                if (!structuredBackend.TryExtractContent(
                        webRequest.downloadHandler.text,
                        out string content,
                        out string error))
                {
                    structuredOutputCapability = StructuredOutputCapability.Unavailable;
                    Complete(request, new LocalLlmResult(
                        LocalLlmRequestStatus.Failed,
                        string.Empty,
                        error,
                        request.OriginalText));
                    yield break;
                }

                structuredOutputCapability = StructuredOutputCapability.Supported;
                if (NarrativeExactKeyContract.IsRegisteredProfile(request.Profile.Id))
                {
                    if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                            request.Profile.Id,
                            content,
                            out string exactJson,
                            out string exactError))
                    {
                        if (attempt + 1 < maximumAttempts)
                        {
                            activePrompt = request.Prompt
                                + "\n교정 요청: 이전 응답은 exact-key 계약 위반으로 거부되었다: "
                                + exactError
                                + " 스키마에 선언된 키만 가진 단일 JSON 객체로 다시 작성한다.";
                            continue;
                        }

                        Complete(request, new LocalLlmResult(
                            LocalLlmRequestStatus.Failed,
                            string.Empty,
                            "Exact-key contract reject: " + exactError,
                            request.OriginalText));
                        yield break;
                    }
                    content = exactJson;
                }
                NarrativeQualityResult quality = narrativeQualityGate.Evaluate(
                    request.Profile,
                    request.Prompt,
                    content);
                lastNarrativeQualityVerdict = quality.Verdict;
                if (!quality.IsAccepted)
                {
                    if (attempt + 1 < maximumAttempts)
                    {
                        activePrompt = request.Prompt
                            + "\n교정 요청: 이전 응답은 다음 이유로 거부되었다: "
                            + quality.Error
                            + " 제공된 Fxx/Mxx 참조만 사용하고 같은 스키마으로 다시 작성한다.";
                        continue;
                    }

                    Complete(request, new LocalLlmResult(
                        LocalLlmRequestStatus.Failed,
                        string.Empty,
                        "Narrative quality hard reject: " + quality.Error,
                        request.OriginalText));
                    yield break;
                }

                NarrativeRequestContext.TryParse(
                    request.Prompt,
                    out NarrativeRequestContext narrativeContext);
                NarrativeGenerationTrace trace = new NarrativeGenerationTrace
                {
                    schemaId = schema.ProfileId,
                    schemaVersion = schema.Version,
                    schemaHash = schema.Hash,
                    cultureStyleId = narrativeContext?.CultureStyleId ?? string.Empty,
                    usedMotifIds = quality.MotifIds,
                    usedCharacterFactIds = quality.CharacterFactIds,
                    verdict = quality.Verdict,
                    retryCount = attempt,
                    usedFallback = false
                };
                Complete(request, new LocalLlmResult(
                    LocalLlmRequestStatus.Succeeded,
                    content,
                    string.Empty,
                    request.OriginalText,
                    trace));
                yield break;
            }
        }
        finally
        {
            request.Detach();
            runningRequests.Remove(request);
        }
    }

    private IEnumerator ProcessEquipmentChoiceRequest(LocalLlmQueuedRequest request)
    {
        if (!ChoicePromptCanonicalizer.TryCanonicalize(
                request.Prompt,
                out ChoicePromptDiagnostic canonical,
                out string canonicalError))
        {
            Complete(request, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                canonicalError,
                request.OriginalText));
            yield break;
        }

        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(
            LocalLlmRequestProfiles.EquipmentChoiceLegacyV2.Id);
        lastSchemaId = schema.ProfileId;
        lastSchemaVersion = schema.Version;
        lastSchemaHash = schema.Hash;

        using UnityWebRequest webRequest = structuredBackend.BuildChoiceRequest(
            endpointUrl,
            request.CorrelationId,
            canonical,
            request.CandidateCount);
        request.Attach(webRequest, Now);
        webRequest.timeout = 1;
        UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
        float timeoutAt = Now + 1f;
        while (!operation.isDone && !request.IsCompleted && Now < timeoutAt)
        {
            yield return null;
        }

        if (!operation.isDone)
        {
            webRequest.Abort();
            Complete(request, new LocalLlmResult(
                LocalLlmRequestStatus.TimedOut,
                string.Empty,
                "EquipmentChoice.TimedOut",
                request.OriginalText));
            yield break;
        }

        int selectedIndex = -1;
        string error = string.Empty;
        if (webRequest.result != UnityWebRequest.Result.Success
            || !structuredBackend.TryExtractChoice(
                webRequest.downloadHandler.text,
                request.CandidateCount,
                out selectedIndex,
                out error))
        {
            Complete(request, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                string.IsNullOrWhiteSpace(error) ? webRequest.error : error,
                request.OriginalText));
            yield break;
        }

        Complete(request, new LocalLlmResult(
            LocalLlmRequestStatus.Succeeded,
            "{\"selectedIndex\":" + selectedIndex + "}",
            string.Empty,
            request.OriginalText));
    }

    private void Complete(
        LocalLlmQueuedRequest request,
        LocalLlmResult result,
        bool logFailure = true)
    {
        if (request == null || !request.TryTakeCallback(out Action<LocalLlmResult> callback))
        {
            return;
        }

        if (result.Status == LocalLlmRequestStatus.TimedOut)
        {
            timeoutCount++;
        }

        lastCompletionDiagnostic =
            $"{request.Profile.Id}:{result.Status}:schema={lastSchemaId}@{lastSchemaVersion}:{lastSchemaHash}:response={result.Content.Length}:error={result.Error}";

        if (!result.IsSuccess
            && !result.IsCancelled
            && result.Status != LocalLlmRequestStatus.Dropped
            && logFailure)
        {
            lastError = $"{request.Profile.Id}: {result.Status} - {result.Error}";
            if (request.Profile.LogFailureWarnings)
            {
                LogWarningIfAllowed(lastError);
            }
        }

        InvokeCallbackSafely(callback, result);
    }

    private void InvokeCallbackSafely(Action<LocalLlmResult> callback, LocalLlmResult result)
    {
        if (callback == null)
        {
            return;
        }

        try
        {
            callback(result);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void LogWarningIfAllowed(string message)
    {
        if (suppressWarningLogsForDebug)
        {
            return;
        }

        Debug.Log($"{name}: {message}", this);
    }

    private bool TryDropLowestPriorityEvictableRequest()
    {
        int index = -1;
        int lowestPriority = int.MaxValue;
        float oldestTime = float.MaxValue;
        for (int i = 0; i < queue.Count; i++)
        {
            LocalLlmQueuedRequest request = queue[i];
            if (!request.Profile.CanBeEvictedForQueuePressure
                || request.Profile.Priority > lowestPriority
                || (request.Profile.Priority == lowestPriority && request.EnqueuedAt >= oldestTime))
            {
                continue;
            }

            lowestPriority = request.Profile.Priority;
            oldestTime = request.EnqueuedAt;
            index = i;
        }

        if (index < 0)
        {
            return false;
        }

        LocalLlmQueuedRequest dropped = queue[index];
        queue.RemoveAt(index);
        droppedEphemeralRequestCount++;
        Complete(dropped, new LocalLlmResult(
            LocalLlmRequestStatus.Dropped,
            string.Empty,
            $"{dropped.Profile.Id} request dropped by queue pressure.",
            dropped.OriginalText));
        return true;
    }

    private void DropExpiredRequests()
    {
        if (queue.Count == 0)
        {
            return;
        }

        for (int i = queue.Count - 1; i >= 0; i--)
        {
            LocalLlmQueuedRequest request = queue[i];
            if (request.Profile.MaxQueueAgeSeconds <= 0f
                || Now - request.EnqueuedAt <= request.Profile.MaxQueueAgeSeconds)
            {
                continue;
            }

            queue.RemoveAt(i);
            droppedEphemeralRequestCount++;
            Complete(request, new LocalLlmResult(
                LocalLlmRequestStatus.Dropped,
                string.Empty,
                $"{request.Profile.Id} request expired in the LLM queue.",
                request.OriginalText));
        }
    }

    private int FindNextRequestIndex()
    {
        return ContextAwareLlmScheduler.FindNext(
            queue,
            Now,
            currentAffinityKey,
            currentAffinityBurst);
    }

}
