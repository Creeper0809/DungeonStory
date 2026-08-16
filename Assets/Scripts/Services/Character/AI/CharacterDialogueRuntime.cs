using System;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterActor))]
[RequireComponent(typeof(CharacterLog))]
public sealed class CharacterDialogueRuntime : MonoBehaviour
{
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.75f, 0f);
    [SerializeField] private float visibleDuration = 2.5f;
    [SerializeField] private float minSecondsBetweenRequests = 1.5f;
    [SerializeField] private TextMeshPro text;
    [SerializeField] private string lastBubbleLine;
    [SerializeField] private string lastGeneratedBubbleLine;
    [SerializeField] private string lastError;

    private CharacterActor actor;
    private CharacterLog characterLog;
    private CharacterVisual visual;
    private float visibleUntil;
    private float nextRequestTime;
    private int nextHiddenRefreshFrame;
    private ILocalLlmRuntimeProvider llmRuntimeProvider;
    private ICharacterAiSchedulingService aiSchedulingService;
    private ICharacterDialogueBubbleFactory bubbleFactory;
    private IGameClock gameClock;
    private IDynamicFrameWorkBudget frameWorkBudget;
    private bool runtimeInjected;
    private bool logSubscribed;

    public string LastBubbleLine => lastBubbleLine;
    public string LastGeneratedBubbleLine => lastGeneratedBubbleLine;
    public string LastError => lastError;

    [Inject]
    public void ConstructCharacterDialogueRuntime(
        ILocalLlmRuntimeProvider llmRuntimeProvider,
        ICharacterAiSchedulingService aiSchedulingService,
        ICharacterDialogueBubbleFactory bubbleFactory,
        IGameClock gameClock,
        IDynamicFrameWorkBudget frameWorkBudget)
    {
        this.llmRuntimeProvider = llmRuntimeProvider
            ?? throw new ArgumentNullException(nameof(llmRuntimeProvider));
        this.aiSchedulingService = aiSchedulingService
            ?? throw new ArgumentNullException(nameof(aiSchedulingService));
        this.bubbleFactory = bubbleFactory
            ?? throw new ArgumentNullException(nameof(bubbleFactory));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.frameWorkBudget = frameWorkBudget;
        runtimeInjected = true;
        TrySubscribeToLog();
    }

    private void Awake()
    {
        EnsureRuntimeReferences();
    }

    private void OnEnable()
    {
        EnsureRuntimeReferences();
        nextHiddenRefreshFrame = (gameClock != null ? gameClock.FrameCount : 0)
            + Mathf.Abs(actor != null ? actor.GetInstanceID() : GetInstanceID()) % 8;
        TrySubscribeToLog();
    }

    private void OnDisable()
    {
        UnsubscribeFromLog();

        if (text != null)
        {
            text.gameObject.SetActive(false);
        }
    }

    internal void PrepareForScopeTeardown()
    {
        UnsubscribeFromLog();
        HideLine();
    }

    private void TrySubscribeToLog()
    {
        if (logSubscribed
            || !runtimeInjected
            || !isActiveAndEnabled
            || characterLog == null)
        {
            return;
        }

        characterLog.OnLogAdded += OnLogAdded;
        logSubscribed = true;
    }

    private void UnsubscribeFromLog()
    {
        if (!logSubscribed)
        {
            return;
        }

        characterLog.OnLogAdded -= OnLogAdded;
        logSubscribed = false;
    }

    private void LateUpdate()
    {
        if (text == null)
        {
            return;
        }

        bool currentlyVisible = text.gameObject.activeSelf;
        if (!currentlyVisible
            && gameClock != null
            && gameClock.FrameCount < nextHiddenRefreshFrame)
        {
            return;
        }

        if (!currentlyVisible
            && frameWorkBudget != null
            && !frameWorkBudget.CanStart(
                DynamicFrameWorkDomain.Presentation))
        {
            nextHiddenRefreshFrame = gameClock.FrameCount + 1;
            return;
        }

        if (gameClock == null || aiSchedulingService == null)
        {
            EnsureRuntimeReferences();
        }

        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        nextHiddenRefreshFrame = gameClock.FrameCount + 8;
        if (gameClock.Time > visibleUntil
            || !RequireAiSchedulingService().ShouldShowCharacterFeedback(actor))
        {
            text.gameObject.SetActive(false);
            ReportPresentationWork(started);
            return;
        }

        text.transform.localPosition = GetLocalOffset();
        ReportPresentationWork(started);
    }

    private void ReportPresentationWork(long started)
    {
        if (frameWorkBudget == null)
        {
            return;
        }

        frameWorkBudget.ReportConsumed(
            DynamicFrameWorkDomain.Presentation,
            (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency);
    }

    public void ShowLine(string line)
    {
        EnsureRuntimeReferences();
        if (string.IsNullOrWhiteSpace(line)
            || !RequireAiSchedulingService().ShouldShowCharacterFeedback(actor))
        {
            return;
        }

        EnsureText();
        lastBubbleLine = line.Trim();
        text.text = lastBubbleLine;
        text.gameObject.SetActive(true);
        visibleUntil = gameClock.Time + Mathf.Max(0.1f, visibleDuration);
    }

    private void OnLogAdded(CharacterLogEntry entry)
    {
        EnsureRuntimeReferences();
        if (gameClock.Time < nextRequestTime || !ShouldRequestBubble(entry))
        {
            return;
        }

        nextRequestTime = gameClock.Time + Mathf.Max(0.1f, minSecondsBetweenRequests);
        string original = !string.IsNullOrWhiteSpace(entry.DisplayLine)
            ? entry.DisplayLine
            : entry.OriginalMessage;
        lastGeneratedBubbleLine = string.Empty;
        if (!TryGetLlmRuntime(out ILocalLlmRuntime queue))
        {
            HideLine();
            return;
        }

        if (!queue.GenerateBubbleLineAsync(BuildPrompt(entry), original, OnBubbleResult))
        {
            lastError = "Bubble request was not accepted by LocalLlmRequestQueue.";
            HideLine();
        }
    }

    private void OnBubbleResult(LocalLlmResult result)
    {
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        if (result.IsSuccess)
        {
            if (!LlmJsonResponseParser.TryParse(result.Content, out BubbleLineJsonDto dto, out string parseError))
            {
                lastError = parseError;
                Debug.LogError($"{name}: Bubble JSON rejected: {parseError}", this);
                HideLine();
                return;
            }

            lastError = string.Empty;
            lastGeneratedBubbleLine = dto.line.Trim();
            ShowLine(lastGeneratedBubbleLine);
            return;
        }

        if (result.IsCancelled)
        {
            lastError = string.Empty;
            HideLine();
            return;
        }

        lastError = $"{result.Status}: {result.Error}";
        if (result.Status != LocalLlmRequestStatus.Dropped)
        {
            Debug.Log($"{name}: Bubble request failed: {lastError}", this);
        }

        HideLine();
    }

    private bool TryGetLlmRuntime(out ILocalLlmRuntime queue)
    {
        if (llmRuntimeProvider == null)
        {
            throw new InvalidOperationException($"{nameof(CharacterDialogueRuntime)} requires {nameof(ILocalLlmRuntimeProvider)} injection.");
        }

        if (llmRuntimeProvider.TryGetRuntime(out queue))
        {
            return true;
        }

        lastError = $"{nameof(LocalLlmRequestQueue)} is missing.";
        return false;
    }

    private ICharacterAiSchedulingService RequireAiSchedulingService()
    {
        return aiSchedulingService
            ?? throw new InvalidOperationException($"{nameof(CharacterDialogueRuntime)} requires {nameof(ICharacterAiSchedulingService)} injection.");
    }

    private void HideLine()
    {
        lastBubbleLine = string.Empty;
        lastGeneratedBubbleLine = string.Empty;
        visibleUntil = 0f;
        if (text != null)
        {
            text.gameObject.SetActive(false);
        }
    }

    private string BuildPrompt(CharacterLogEntry entry)
    {
        string persona = actor != null
            && actor.TryGetComponent(out CustomerPersonaRuntime runtime)
            && runtime.Persona != null
                ? runtime.Persona.traitName
                : "unknown";
        string prompt = "Write one short in-character speech bubble as JSON {\"line\":\"...\"}.\n"
            + "The line must be 80 characters or fewer. Use 4 to 10 words. No narration, no markdown, no extra keys.\n"
            + "Do not copy the original event text verbatim; rewrite it as a natural speech bubble.\n"
            + $"persona: {persona}\n"
            + $"eventKind: {entry.Activity?.KindId}\n"
            + $"outcome: {entry.Activity?.OutcomeId}\n"
            + $"reasonCode: {entry.Activity?.ReasonCode}\n"
            + $"event: {entry.OriginalMessage}";
        return NarrativeRequestContextBuilder.ForActor(
                LocalLlmRequestProfiles.BubbleLine.Id,
                actor,
                requireCharacterFact: false,
                requireMotif: false)
            .AppendToPrompt(prompt);
    }

    private static bool ShouldRequestBubble(CharacterLogEntry entry)
    {
        return entry.Activity != null
            && entry.Activity.VisibleToPlayer
            && entry.Activity.BubbleEligible;
    }

    private void EnsureText()
    {
        if (text != null)
        {
            return;
        }

        text = RequireBubbleFactory().Create(transform);
    }

    private Vector3 GetLocalOffset()
    {
        EnsureRuntimeReferences();
        if (visual == null)
        {
            return localOffset;
        }

        float y = Mathf.Max(localOffset.y, visual.GetVisualTopLocalY() + 0.75f);
        return new Vector3(localOffset.x, y, localOffset.z);
    }

    private ICharacterDialogueBubbleFactory RequireBubbleFactory()
    {
        return bubbleFactory
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterDialogueRuntime)} requires {nameof(ICharacterDialogueBubbleFactory)} injection.");
    }

    private void EnsureRuntimeReferences()
    {
        if (actor == null)
        {
            actor = GetComponent<CharacterActor>();
        }

        if (characterLog == null)
        {
            characterLog = GetComponent<CharacterLog>();
        }

        if (visual == null)
        {
            visual = GetComponent<CharacterVisual>();
        }
    }

}
