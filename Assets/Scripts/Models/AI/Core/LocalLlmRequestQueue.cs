using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        logFailureWarnings: false);
    public static readonly LocalLlmRequestProfile EvolutionHistory = new LocalLlmRequestProfile(
        "EvolutionHistory",
        17,
        temperature: 0.65f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly,
        logFailureWarnings: false,
        maxOutputTokens: 384);
    public static readonly LocalLlmRequestProfile SocialRumor = new LocalLlmRequestProfile(
        "SocialRumor",
        15,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly);
    public static readonly LocalLlmRequestProfile CharacterRecord = new LocalLlmRequestProfile(
        "CharacterRecord",
        12,
        temperature: 0.7f,
        queueFullBehavior: LocalLlmQueueFullBehavior.RejectQuietly);
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICorrelatedCharacterSkillLlmRuntime
{
    bool GenerateCharacterSkillAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    void CancelCharacterSkillRequest(string requestKey);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICorrelatedEvolutionHistoryLlmRuntime
{
    bool GenerateEvolutionHistoryAsync(
        string requestKey,
        string prompt,
        Action<LocalLlmResult> callback);

    void CancelEvolutionHistoryRequest(string requestKey);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct LocalLlmResult
{
    public LocalLlmResult(
        LocalLlmRequestStatus status,
        string content,
        string error,
        string originalText)
    {
        Status = status;
        Content = content ?? string.Empty;
        Error = error ?? string.Empty;
        OriginalText = originalText ?? string.Empty;
    }

    public LocalLlmRequestStatus Status { get; }
    public string Content { get; }
    public string Error { get; }
    public string OriginalText { get; }
    public bool IsSuccess => Status == LocalLlmRequestStatus.Succeeded;
    public bool IsCancelled => Status == LocalLlmRequestStatus.Cancelled;
}

internal sealed class LocalLlmQueuedRequest
{
    public LocalLlmQueuedRequest(
        LocalLlmRequestProfile profile,
        string prompt,
        string originalText,
        float timeoutSeconds,
        float enqueuedAt,
        string correlationId,
        Action<LocalLlmResult> callback)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        OriginalText = originalText ?? string.Empty;
        TimeoutSeconds = Mathf.Max(0.1f, timeoutSeconds);
        EnqueuedAt = enqueuedAt;
        CorrelationId = correlationId ?? string.Empty;
        Callback = callback;
    }

    public LocalLlmRequestProfile Profile { get; }
    public string Prompt { get; }
    public string OriginalText { get; }
    public float TimeoutSeconds { get; }
    public float EnqueuedAt { get; }
    public string CorrelationId { get; }
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
    ICorrelatedCharacterSkillLlmRuntime,
    ICorrelatedEvolutionHistoryLlmRuntime
{
    [SerializeField] private string endpointUrl = "http://localhost:11434/v1/chat/completions";
    [SerializeField] private string modelName = "llama3.1";
    [SerializeField] private bool enableJsonMode = true;
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
    [SerializeField, ReadOnly] private bool suppressWarningLogsForDebug;

    private readonly List<LocalLlmQueuedRequest> queue = new List<LocalLlmQueuedRequest>();
    private readonly HashSet<LocalLlmQueuedRequest> runningRequests = new HashSet<LocalLlmQueuedRequest>();
    private IUiClock uiClock;
    private bool isSuspended;

    public int QueuedCount => queue.Count;
    public int RunningCount => runningRequests.Count;
    public int DroppedEphemeralRequestCount => droppedEphemeralRequestCount;
    public int DroppedBubbleCount => droppedEphemeralRequestCount;
    public int TimeoutCount => timeoutCount;
    public int MaxQueueSize => maxQueueSize;
    public string LastError => lastError;
    public string LastCompletionDiagnostic => lastCompletionDiagnostic;
    public bool HasConfiguredEndpoint => !string.IsNullOrWhiteSpace(endpointUrl)
        && !string.IsNullOrWhiteSpace(modelName);
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
    }

    private void OnDisable()
    {
        ResetTransientState("Local LLM queue was disabled.", remainSuspended: true);
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
        DropExpiredRequests();
        while (RunningCount < Mathf.Max(1, maxConcurrentRequests) && queue.Count > 0)
        {
            int index = FindNextRequestIndex();
            LocalLlmQueuedRequest request = queue[index];
            queue.RemoveAt(index);
            StartCoroutine(ProcessRequest(request));
        }
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

    public void CancelEvolutionHistoryRequest(string requestKey)
    {
        CancelCorrelatedRequest(
            requestKey,
            "Evolution history request was cancelled.");
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
        string correlationId = "")
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

        queue.Add(new LocalLlmQueuedRequest(
            profile,
            prompt,
            originalText,
            timeoutSeconds,
            Now,
            correlationId,
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
            if (!HasConfiguredEndpoint)
            {
                Complete(request, new LocalLlmResult(
                    LocalLlmRequestStatus.Failed,
                    string.Empty,
                    "Local LLM endpoint or model is not configured.",
                    request.OriginalText));
                yield break;
            }

            using UnityWebRequest webRequest = BuildRequest(request.Profile, request.Prompt);
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

            if (!TryExtractContent(webRequest.downloadHandler.text, out string content, out string error))
            {
                Complete(request, new LocalLlmResult(
                    LocalLlmRequestStatus.Failed,
                    string.Empty,
                    error,
                    request.OriginalText));
                yield break;
            }

            Complete(request, new LocalLlmResult(
                LocalLlmRequestStatus.Succeeded,
                content,
                string.Empty,
                request.OriginalText));
        }
        finally
        {
            request.Detach();
            runningRequests.Remove(request);
        }
    }

    private UnityWebRequest BuildRequest(LocalLlmRequestProfile profile, string prompt)
    {
        OpenAiChatRequest payload = new OpenAiChatRequest
        {
            model = modelName,
            temperature = profile.Temperature,
            max_tokens = profile.MaxOutputTokens,
            messages = new[]
            {
                new OpenAiChatMessage
                {
                    role = "system",
                    content = "Return exactly one compact JSON object. Do not add markdown."
                },
                new OpenAiChatMessage
                {
                    role = "user",
                    content = prompt
                }
            },
            response_format = enableJsonMode ? new OpenAiResponseFormat { type = "json_object" } : null
        };

        string json = JsonUtility.ToJson(payload);
        UnityWebRequest request = new UnityWebRequest(endpointUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
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

        lastCompletionDiagnostic = $"{request.Profile.Id}:{result.Status}:response={result.Content.Length}:error={result.Error}";

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
        int bestIndex = 0;
        int bestPriority = int.MinValue;
        float bestEnqueuedAt = float.MaxValue;
        for (int i = 0; i < queue.Count; i++)
        {
            int priority = queue[i].Profile.Priority;
            if (priority > bestPriority
                || (priority == bestPriority && queue[i].EnqueuedAt < bestEnqueuedAt))
            {
                bestIndex = i;
                bestPriority = priority;
                bestEnqueuedAt = queue[i].EnqueuedAt;
            }
        }

        return bestIndex;
    }

    private static bool TryExtractContent(string responseJson, out string content, out string error)
    {
        content = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            error = "LLM HTTP response is empty.";
            return false;
        }

        OpenAiChatResponse response;
        try
        {
            response = JsonUtility.FromJson<OpenAiChatResponse>(responseJson);
        }
        catch (Exception exception)
        {
            error = $"LLM HTTP response parse failed: {exception.Message}";
            return false;
        }

        if (response == null || response.choices == null || response.choices.Length == 0)
        {
            error = "LLM HTTP response has no choices.";
            return false;
        }

        content = response.choices[0].message != null
            ? response.choices[0].message.content
            : response.choices[0].text;
        if (string.IsNullOrWhiteSpace(content))
        {
            error = "LLM HTTP choice has no content.";
            return false;
        }

        return true;
    }

    [Serializable]
    private sealed class OpenAiChatRequest
    {
        public string model;
        public OpenAiChatMessage[] messages;
        public float temperature;
        public int max_tokens;
        public OpenAiResponseFormat response_format;
    }

    [Serializable]
    private sealed class OpenAiChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private sealed class OpenAiResponseFormat
    {
        public string type;
    }

    [Serializable]
    private sealed class OpenAiChatResponse
    {
        public OpenAiChoice[] choices;
    }

    [Serializable]
    private sealed class OpenAiChoice
    {
        public OpenAiChatMessage message;
        public string text;
    }
}
