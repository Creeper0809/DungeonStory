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
#if UNITY_EDITOR
    private const string LiveNarrativeCaptureVariable =
        "DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE";
#endif

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

    private readonly LocalLlmRequestState requests = new();
    private IUiClock uiClock;
    private DungeonStoryHostStructuredChatBackend structuredBackend;
    private readonly INarrativeTextQualityGate narrativeQualityGate =
        new NarrativeTextQualityGate();
    private DungeonStoryLlmHostProcess localHost;
    private Task<HostStartupResult> hostStartupTask;
    private bool isSuspended;
#if UNITY_EDITOR
    private bool isIsolatedRequestCaptureMode;
    private bool isolatedAutomaticDispatchEnabled = true;
    private Action<LocalLlmCapturedRequest> isolatedRequestCaptureObserver;
    private Func<LocalLlmCapturedRequest, string> isolatedTestResponseProvider;
#endif

    public int QueuedCount => requests.QueuedCount;
    public int RunningCount => requests.RunningCount;
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
        return requests.PeekNextProfile(Now);
    }

    public string GetRequestDiagnosticsForDebug()
    {
        float now = Now;
        string running = string.Join(",", requests.Running
            .Where(request => request != null)
            .Select(request => $"{request.Profile.Id}:age={Mathf.Max(0f, now - request.StartedAt):0.0}s/timeout={request.TimeoutSeconds:0.0}s/prompt={request.Prompt.Length}"));
        string waiting = string.Join(",", requests.Queued
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
#if UNITY_EDITOR
        if (isIsolatedRequestCaptureMode)
        {
            if (isolatedRequestCaptureObserver == null)
            {
                return new LocalLlmRuntimeReadinessSnapshot(
                    LocalLlmRuntimeReadinessState.Starting);
            }
            if (string.IsNullOrWhiteSpace(modelName))
            {
                return new LocalLlmRuntimeReadinessSnapshot(
                    LocalLlmRuntimeReadinessState.Failed,
                    "Isolated request capture requires the queue's configured model name.");
            }

            return new LocalLlmRuntimeReadinessSnapshot(
                LocalLlmRuntimeReadinessState.Ready);
        }
#endif
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

#if UNITY_EDITOR
    public void ConfigureIsolatedRequestCapture(
        Action<LocalLlmCapturedRequest> observer)
    {
        if (!isIsolatedRequestCaptureMode)
        {
            throw new InvalidOperationException(
                "Isolated request capture is not active for this queue instance.");
        }
        if (observer == null)
        {
            throw new ArgumentNullException(nameof(observer));
        }
        if (isolatedRequestCaptureObserver != null)
        {
            throw new InvalidOperationException(
                "An isolated request capture observer is already registered.");
        }

        isolatedRequestCaptureObserver = observer;
    }

    // Deterministic orchestration for an already accepted request in the
    // non-network isolated verifier. The request still uses ProcessRequest and
    // the production schema/wire serializer; this only bypasses competition
    // from unrelated response-less capture requests in Update scheduling.
    public bool TryDispatchQueuedRequestForIsolatedCapture(
        string correlationId)
    {
        if (!isIsolatedRequestCaptureMode
            || isolatedRequestCaptureObserver == null
            || !Application.isBatchMode
            || !DungeonPersistencePaths.IsIsolatedVerification)
        {
            throw new InvalidOperationException(
                "Targeted isolated dispatch requires active batch capture verification.");
        }

        string canonical = correlationId?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(correlationId, canonical, StringComparison.Ordinal))
        {
            return false;
        }

        if (!requests.TryBeginCorrelated(canonical, out LocalLlmQueuedRequest request))
            return false;
        StartExecution(request);
        return true;
    }

    // Defer unrelated transport in an explicitly isolated replay. Accepted
    // requests remain queued: no fabricated reply, cancellation callback or
    // retry is produced. The exact target still uses the normal ProcessRequest
    // path through TryDispatchQueuedRequestForIsolatedCapture.
    public void ConfigureIsolatedAutomaticDispatch(bool enabled)
    {
        if (!isIsolatedRequestCaptureMode || isolatedRequestCaptureObserver == null
            || !Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification)
            throw new InvalidOperationException("Automatic dispatch control requires active isolated batch capture.");
        isolatedAutomaticDispatchEnabled = enabled;
    }

    // Explicit integration fixture, never a timeout fallback or a model response.
    public void ConfigureIsolatedShopTestResponses(Func<LocalLlmCapturedRequest, string> provider)
    {
        if (!isIsolatedRequestCaptureMode || !Application.isBatchMode
            || !DungeonPersistencePaths.IsIsolatedVerification
            || Environment.GetEnvironmentVariable("DUNGEONSTORY_SHOP_CONTINUATION_VERIFY") != "1")
            throw new InvalidOperationException("Shop test responses require the explicit isolated Shop verifier.");
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (isolatedTestResponseProvider != null)
            throw new InvalidOperationException("An isolated test response provider is already registered.");
        isolatedTestResponseProvider = provider;
    }

    public void ClearIsolatedShopTestResponses() => isolatedTestResponseProvider = null;

    // Explicit facility-campaign visitor preparation fixture. This is limited
    // to the isolated source-v9 facility intake and never replaces the
    // facility evolution request or any production gameplay outcome.
    public void ConfigureIsolatedFacilityVisitorTestResponses(
        Func<LocalLlmCapturedRequest, string> provider)
    {
        if (!isIsolatedRequestCaptureMode || !Application.isBatchMode
            || !DungeonPersistencePaths.IsIsolatedVerification
            || Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_SOURCE_V9_FACILITY_INTAKE") != "1")
        {
            throw new InvalidOperationException(
                "Facility visitor test responses require the explicit isolated source-v9 facility verifier.");
        }
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (isolatedTestResponseProvider != null)
        {
            throw new InvalidOperationException(
                "An isolated test response provider is already registered.");
        }
        isolatedTestResponseProvider = provider;
    }

    public void ClearIsolatedFacilityVisitorTestResponses() =>
        isolatedTestResponseProvider = null;

    // Explicit source-v9 module replay fixture. This is available only in the
    // isolated campaign verifier and is never a timeout fallback or model call.
    public void ConfigureIsolatedModuleReplayTestResponses(
        Func<LocalLlmCapturedRequest, string> provider)
    {
        if (!isIsolatedRequestCaptureMode || !Application.isBatchMode
            || !DungeonPersistencePaths.IsIsolatedVerification
            || (Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_SOURCE_V9_EQUIPMENT_REPLAY") != "1"
                && Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_SOURCE_V9_SKILL_REPLAY") != "1"
                && Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_SOURCE_V9_TRAIT_REPLAY") != "1"
                && Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_SOURCE_V9_FACILITY_REPLAY") != "1"
                && Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_ORIGINAL_SKILL_PENDING_DIAGNOSTICS") != "1"))
        {
            throw new InvalidOperationException(
                "Module replay responses require the explicit isolated source-v9 verifier.");
        }
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (isolatedTestResponseProvider != null)
            throw new InvalidOperationException(
                "An isolated test response provider is already registered.");
        isolatedTestResponseProvider = provider;
    }

    public void ClearIsolatedModuleReplayTestResponses() =>
        isolatedTestResponseProvider = null;

    // A saved support continuation can need one real new visitor before an
    // expedition. Its offered preparation responses are an explicit fixture,
    // not replay of the saved skill and never a production/network fallback.
    public void ConfigureIsolatedSkillVisitorTestResponses(
        Func<LocalLlmCapturedRequest, string> provider)
    {
        if (!isIsolatedRequestCaptureMode || !Application.isBatchMode
            || !DungeonPersistencePaths.IsIsolatedVerification
            || Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE") != "1"
            || Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_BATTLE_DIAGNOSTICS") != "1"
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_BATTLE_ACTOR_ID"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_BATTLE_SKILL_ID")))
        {
            throw new InvalidOperationException(
                "Skill visitor test responses require the explicit isolated saved support verifier.");
        }
        if (provider == null) throw new ArgumentNullException(nameof(provider));
        if (isolatedTestResponseProvider != null)
            throw new InvalidOperationException("An isolated test response provider is already registered.");
        isolatedTestResponseProvider = provider;
    }

    public void ClearIsolatedSkillVisitorTestResponses() =>
        isolatedTestResponseProvider = null;
#endif

    private void OnEnable()
    {
        isSuspended = false;
#if UNITY_EDITOR
        isIsolatedRequestCaptureMode = ShouldEnableIsolatedRequestCapture();
        isolatedAutomaticDispatchEnabled = true;
        isolatedRequestCaptureObserver = null;
        isolatedTestResponseProvider = null;
        if (isIsolatedRequestCaptureMode)
        {
            structuredBackend = new DungeonStoryHostStructuredChatBackend();
            // Capture the same model alias that normal host startup selects,
            // not a stale serialized scene value. No host/network is started.
            modelName = "DungeonStory-Qwen3-1.7B-Q4_K_M";
            endpointUrl = string.Empty;
            hostStartupTask = null;
            return;
        }
#endif
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
#if UNITY_EDITOR
        isolatedRequestCaptureObserver = null;
        isolatedTestResponseProvider = null;
        isIsolatedRequestCaptureMode = false;
        isolatedAutomaticDispatchEnabled = true;
#endif
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

#if UNITY_EDITOR
    private static bool ShouldEnableIsolatedRequestCapture()
    {
        bool requested = string.Equals(
            Environment.GetEnvironmentVariable(LiveNarrativeCaptureVariable),
            "1",
            StringComparison.Ordinal);
        if (!requested)
        {
            return false;
        }

        bool isBatchMode = Application.isBatchMode;
        bool isIsolatedVerification = isBatchMode
            && DungeonPersistencePaths.IsIsolatedVerification;
        return ResolveIsolatedRequestCaptureMode(
            requested,
            isBatchMode,
            isIsolatedVerification);
    }

    internal static bool ResolveIsolatedRequestCaptureMode(
        bool requested,
        bool isBatchMode,
        bool isIsolatedVerification)
    {
        if (!requested)
        {
            return false;
        }
        if (!isBatchMode)
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE=1 requires Unity Editor batch mode.");
        }
        if (!isIsolatedVerification)
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE=1 requires isolated verification persistence.");
        }

        return true;
    }
#endif

    private void ResetTransientState(string reason, bool remainSuspended)
    {
        isSuspended = true;
        IReadOnlyList<LocalLlmQueuedRequest> cancelled = requests.CapturePending();
        StopAllCoroutines();
        requests.Clear();

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
#if UNITY_EDITOR
        if (isIsolatedRequestCaptureMode
            && (!isolatedAutomaticDispatchEnabled || isolatedRequestCaptureObserver == null))
        {
            return;
        }
#endif
        DropExpiredRequests();
        if (hostStartupTask != null)
        {
            // Persistent narrative requests may be queued during scene startup.
            // Do not fail them merely because model hashing/loading is still in progress.
            return;
        }
        while (requests.TryBeginNext(Now, maxConcurrentRequests, out LocalLlmQueuedRequest request))
            StartExecution(request);
    }

    private void StartExecution(LocalLlmQueuedRequest request)
    {
        try
        {
            Coroutine execution = StartCoroutine(ProcessRequest(request));
            if (execution == null && requests.IsRunning(request))
                throw new InvalidOperationException("Local LLM request execution did not start.");
        }
        catch (Exception exception)
        {
            try
            {
                request.Abort();
                Complete(request, new LocalLlmResult(
                    LocalLlmRequestStatus.Failed, string.Empty,
                    "Local LLM request execution failed: " + exception.Message,
                    request.OriginalText));
            }
            finally { requests.Finish(request); }
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

#if UNITY_EDITOR
        if (isIsolatedRequestCaptureMode)
        {
            const string captureError =
                "EquipmentChoice binary requests are unsupported in isolated request capture mode.";
            lastError = "EquipmentChoice: Failed - " + captureError;
            callback?.Invoke(new LocalLlmChoiceResult(false, -1, captureError));
            return false;
        }
#endif

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
        return AdmitRequest(new LocalLlmQueuedRequest(
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
        if (requests.ContainsCorrelation(canonicalKey))
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
        if (requests.ContainsCorrelation(canonicalKey))
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
        if (requests.ContainsCorrelation(canonicalKey))
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
        if (requests.ContainsCorrelation(canonicalKey))
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

        IReadOnlyList<LocalLlmQueuedRequest> cancelled = requests.CancelCorrelation(requestKey);
        foreach (LocalLlmQueuedRequest request in cancelled)
        {
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

        string contextualPrompt = prompt.Contains(NarrativeRequestContext.BeginMarker)
            ? prompt
            : NarrativeCultureStyleCatalog.Create(
                    profile.Id,
                    string.Empty,
                    requireCharacterFact: false,
                    requireMotif: LlmStaticSchemaCatalog.Require(profile.Id)
                        .PersistentNarrative)
                .AppendToPrompt(prompt);
        return AdmitRequest(new LocalLlmQueuedRequest(
            profile,
            contextualPrompt,
            originalText,
            timeoutSeconds,
            Now,
            correlationId,
            scheduling,
            callback));
    }

    private bool AdmitRequest(LocalLlmQueuedRequest request)
    {
        LocalLlmRequestAdmission admission = requests.Admit(
            request, Mathf.Max(1, maxQueueSize), out LocalLlmQueuedRequest evicted);
        switch (admission)
        {
            case LocalLlmRequestAdmission.Accepted:
                if (evicted != null)
                {
                    droppedEphemeralRequestCount++;
                    Complete(evicted, new LocalLlmResult(
                        LocalLlmRequestStatus.Dropped, string.Empty,
                        $"{evicted.Profile.Id} request dropped by queue pressure.",
                        evicted.OriginalText));
                }
                return true;
            case LocalLlmRequestAdmission.DuplicateCorrelation:
                lastError = $"{request.Profile.Id}: Skipped - correlated request is already pending.";
                return false;
            case LocalLlmRequestAdmission.Dropped:
                droppedEphemeralRequestCount++;
                Complete(request, new LocalLlmResult(
                    LocalLlmRequestStatus.Dropped, string.Empty,
                    $"{request.Profile.Id} request dropped because the LLM queue is full.",
                    request.OriginalText));
                return false;
            case LocalLlmRequestAdmission.RejectedQuietly:
                lastError = $"{request.Profile.Id}: Skipped - LLM queue is full.";
                return false;
            default:
                Complete(request, new LocalLlmResult(
                    LocalLlmRequestStatus.Failed, string.Empty,
                    "LLM queue is full and no queued request can be evicted.",
                    request.OriginalText));
                return false;
        }
    }

    private IEnumerator ProcessRequest(LocalLlmQueuedRequest request)
    {
        try
        {
            if (!requests.IsRunning(request) || request.IsCompleted)
                yield break;
            if (request.IsEquipmentChoice)
            {
#if UNITY_EDITOR
                if (isIsolatedRequestCaptureMode)
                {
                    Complete(request, new LocalLlmResult(
                        LocalLlmRequestStatus.Failed,
                        string.Empty,
                        "EquipmentChoice binary requests are unsupported in isolated request capture mode.",
                        request.OriginalText));
                    yield break;
                }
#endif
                yield return ProcessEquipmentChoiceRequest(request);
                yield break;
            }

#if UNITY_EDITOR
            if (isIsolatedRequestCaptureMode)
            {
                yield return ProcessIsolatedRequestCapture(request);
                yield break;
            }
#endif

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

                string rawModelContent = content;
                if (structuredBackend is DungeonStoryHostStructuredChatBackend
                    && !DungeonStoryHostStructuredChatBackend.TryResolveModelContent(
                        request.Profile, activePrompt, content, out content, out string resolutionError))
                {
                    Complete(request, new LocalLlmResult(LocalLlmRequestStatus.Failed, string.Empty,
                        "Model reference reject: " + resolutionError, request.OriginalText,
                        rawModelContent: rawModelContent));
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
                            request.OriginalText, rawModelContent: rawModelContent));
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
                        request.OriginalText, rawModelContent: rawModelContent));
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
                    trace,
                    rawModelContent));
                yield break;
            }
        }
        finally
        {
            request.Detach();
            requests.Finish(request);
        }
    }

#if UNITY_EDITOR
    private IEnumerator ProcessIsolatedRequestCapture(LocalLlmQueuedRequest request)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            Complete(request, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                "Isolated request capture requires the queue's configured model name.",
                request.OriginalText));
            yield break;
        }

        request.Attach(null, Now);
        if (!TryNotifyIsolatedRequestCapture(request, out LocalLlmCapturedRequest captured, out string captureError))
        {
            Complete(request, new LocalLlmResult(
                LocalLlmRequestStatus.Failed,
                string.Empty,
                captureError,
                request.OriginalText));
            yield break;
        }

        if (isolatedTestResponseProvider != null)
        {
            string response = null;
            string fixtureError = null;
            try { response = isolatedTestResponseProvider(captured); }
            catch (Exception exception) { fixtureError = "Isolated test response failed: " + exception.Message; }
            if (fixtureError != null)
            {
                Complete(request, new LocalLlmResult(LocalLlmRequestStatus.Failed, string.Empty,
                    fixtureError, request.OriginalText));
                yield break;
            }
            if (response != null)
            {
                CompleteIsolatedTestResponse(request, response);
                yield break;
            }
        }

        float timeoutAt = Now + Mathf.Max(0.1f, request.TimeoutSeconds);
        while (!request.IsCompleted && Now < timeoutAt)
        {
            yield return null;
        }

        if (!request.IsCompleted)
        {
            Complete(request, new LocalLlmResult(
                LocalLlmRequestStatus.TimedOut,
                string.Empty,
                "Isolated request capture received no model response.",
                request.OriginalText));
        }
    }

    private bool TryNotifyIsolatedRequestCapture(
        LocalLlmQueuedRequest request,
        out LocalLlmCapturedRequest captured,
        out string error)
    {
        captured = null;
        error = string.Empty;
        LlmStaticSchemaDefinition schema;
        byte[] payload;
        try
        {
            schema = LlmStaticSchemaCatalog.Require(request.Profile.Id);
            payload = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                modelName,
                request.Profile,
                schema,
                request.Prompt);
        }
        catch (Exception exception)
        {
            error = "Isolated request capture serialization failed: " + exception.Message;
            return false;
        }

        lastSchemaId = schema.ProfileId;
        lastSchemaVersion = schema.Version;
        lastSchemaHash = schema.Hash;
        captured = new LocalLlmCapturedRequest(
            request.Profile.Id,
            request.CorrelationId,
            0,
            request.Prompt,
            modelName,
            schema.Version,
            schema.Hash,
            payload);
        try
        {
            isolatedRequestCaptureObserver(captured);
        }
        catch (Exception exception)
        {
            error = "Isolated request capture observer failed: " + exception.Message;
            return false;
        }

        return true;
    }

    private void CompleteIsolatedTestResponse(LocalLlmQueuedRequest request, string response)
    {
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(request.Profile.Id, response,
                out string exactJson, out string exactError))
        {
            Complete(request, new LocalLlmResult(LocalLlmRequestStatus.Failed, string.Empty,
                "Isolated test exact-key reject: " + exactError, request.OriginalText));
            return;
        }
        NarrativeQualityResult quality = narrativeQualityGate.Evaluate(request.Profile, request.Prompt, exactJson);
        lastNarrativeQualityVerdict = quality.Verdict;
        if (!quality.IsAccepted)
        {
            Complete(request, new LocalLlmResult(LocalLlmRequestStatus.Failed, string.Empty,
                "Isolated test quality reject: " + quality.Error, request.OriginalText));
            return;
        }
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(request.Profile.Id);
        NarrativeRequestContext.TryParse(request.Prompt, out NarrativeRequestContext context);
        Complete(request, new LocalLlmResult(LocalLlmRequestStatus.Succeeded, exactJson, string.Empty,
            request.OriginalText, new NarrativeGenerationTrace
            {
                schemaId = schema.ProfileId, schemaVersion = schema.Version, schemaHash = schema.Hash,
                cultureStyleId = context?.CultureStyleId ?? string.Empty, usedMotifIds = quality.MotifIds,
                usedCharacterFactIds = quality.CharacterFactIds, verdict = quality.Verdict,
                retryCount = 0, usedFallback = false
            }));
    }
#endif

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

    private void DropExpiredRequests()
    {
        if (QueuedCount == 0) return;
        IReadOnlyList<LocalLlmQueuedRequest> expired = requests.TakeExpired(Now);
        droppedEphemeralRequestCount += expired.Count;
        foreach (LocalLlmQueuedRequest request in expired)
            Complete(request, new LocalLlmResult(
                LocalLlmRequestStatus.Dropped, string.Empty,
                $"{request.Profile.Id} request expired in the LLM queue.",
                request.OriginalText));
    }
}
