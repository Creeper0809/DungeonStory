using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public static class CharacterLogNarrativePlayModeVerifier
{
    public const string ReportPath = "Temp/phase52-character-record-stories-report.txt";
    public const string CapturePath = "Temp/phase52-character-record-stories.png";
    public const string PendingCapturePath = "Temp/phase65-character-record-pending.png";

    [MenuItem("DungeonStory/Debug/QA/Run Character Log Narrative PlayMode Verification")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Character log narrative verification requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<CharacterLogNarrativePlayModeVerificationRunner>() != null)
        {
            Debug.LogWarning("Character log narrative verification is already running.");
            return;
        }

        new GameObject("Character Log Narrative PlayMode Verification Runner")
            .AddComponent<CharacterLogNarrativePlayModeVerificationRunner>();
    }
}

public sealed class CharacterLogNarrativePlayModeVerificationRunner : MonoBehaviour
{
    private const string StartEvent = "작업 시작 · 연금 연구 · 지하 연구실";
    private const string AlternateStartEvent = "작업 시작 · 무기 판매 · 무기상점";
    private const string EndEvent = "작업 종료 · 연금 연구 · 지하 연구실 · 새 제조법 정리 완료";
    private const string MajorStartEvent = "진실핵 조사를 시작";
    private const string MajorAlternateStartEvent = "봉인된 문장의 비밀 추적";
    private const string MajorEndEvent = "진실핵 단서 확보";
    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();
    private float originalTimeScale;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Temp");
        Application.logMessageReceived += OnLogMessageReceived;
        originalTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        yield return null;
        yield return VerifyDeterministicDisplayGating();
        yield return VerifyNarrativeRecords();
        Time.timeScale = originalTimeScale;
        Application.logMessageReceived -= OnLogMessageReceived;
        Finish();
        Destroy(gameObject);
    }

    private IEnumerator VerifyDeterministicDisplayGating()
    {
        DeferredCharacterRecordRuntime templateRuntime = new DeferredCharacterRecordRuntime();
        CharacterLogNarrativeService templateService = new CharacterLogNarrativeService(
            new FixedLocalLlmRuntimeProvider(templateRuntime));
        CharacterLog templateLog = CreateProbeLog("asd", templateService);
        templateLog.AddActivity(CreateWorkActivity(
            "작업 시작 · 연구 · 연구실",
            "work:research",
            CharacterActivityOutcomes.Started));
        Check(templateLog.Entries.Count == 1
            && templateRuntime.PendingRecordCount == 0
            && templateService.TemplateLineCount == 1
            && !templateLog.Entries[0].Contains("작업 시작"),
            "TEMPLATE_RECORD_IMMEDIATE",
            $"visible={Compact(templateLog.Entries)}; pending={templateRuntime.PendingRecordCount}; templates={templateService.TemplateLineCount}");

        DeferredCharacterRecordRuntime successRuntime = new DeferredCharacterRecordRuntime();
        CharacterLogNarrativeService successService = new CharacterLogNarrativeService(
            new FixedLocalLlmRuntimeProvider(successRuntime));
        CharacterLog successLog = CreateProbeLog("asd", successService);
        CharacterLogEntry successEntry = default;
        successLog.OnLogAdded += entry => successEntry = entry;
        successLog.AddActivity(CreateMajorActivity(
            StartEvent,
            CharacterActivityOutcomes.Started));

        Check(successEntry.EntryId > 0, "INTERNAL_EVENT_IMMEDIATE", successEntry.OriginalMessage);
        Check(successLog.Entries.Count == 0,
            "PENDING_ENTRY_PRIVATE",
            $"visible={successLog.Entries.Count}; pendingCallbacks={successRuntime.PendingRecordCount}");

        string requiredSubject = CharacterLogNarrativeService.BuildRequiredSubject(successLog.name);
        string successLine = CharacterLogNarrativeService.BuildControlledFallbackLine(
            successEntry,
            requiredSubject);
        successRuntime.CompleteNext(
            LocalLlmRequestStatus.Succeeded,
            JsonUtility.ToJson(new CharacterRecordJsonDto { line = successLine }));
        Check(successLog.Entries.Count == 1
            && successLog.Entries[0] == successLine
            && !successLog.Entries[0].Contains(StartEvent),
            "SUCCESS_REVEALS_FINAL_ONLY",
            Compact(successLog.Entries));

        DeferredCharacterRecordRuntime failureRuntime = new DeferredCharacterRecordRuntime();
        CharacterLogNarrativeService failureService = new CharacterLogNarrativeService(
            new FixedLocalLlmRuntimeProvider(failureRuntime));
        CharacterLog failureLog = CreateProbeLog("asd", failureService);
        failureLog.AddActivity(CreateMajorActivity(
            EndEvent,
            CharacterActivityOutcomes.Completed));
        bool failurePendingHidden = failureLog.Entries.Count == 0;
        failureRuntime.CompleteNext(LocalLlmRequestStatus.Failed, string.Empty, "forced failure");
        Check(failurePendingHidden
            && failureLog.Entries.Count == 1
            && !failureLog.Entries[0].Contains(EndEvent)
            && failureService.ControlledFallbackCount == 1,
            "FAILURE_REVEALS_SAFE_FALLBACK",
            Compact(failureLog.Entries));

        DeferredCharacterRecordRuntime rejectedRuntime = new DeferredCharacterRecordRuntime
        {
            AcceptRecordRequests = false
        };
        CharacterLogNarrativeService rejectedService = new CharacterLogNarrativeService(
            new FixedLocalLlmRuntimeProvider(rejectedRuntime));
        CharacterLog rejectedLog = CreateProbeLog("asd", rejectedService);
        rejectedLog.AddActivity(CreateMajorActivity(
            AlternateStartEvent,
            CharacterActivityOutcomes.Started));
        Check(rejectedLog.Entries.Count == 1
            && !rejectedLog.Entries[0].Contains(AlternateStartEvent)
            && rejectedService.ControlledFallbackCount == 1,
            "REJECTED_REQUEST_REVEALS_SAFE_FALLBACK",
            Compact(rejectedLog.Entries));

        DeferredCharacterRecordRuntime saturatedRuntime = new DeferredCharacterRecordRuntime();
        CharacterLogNarrativeService saturatedService = new CharacterLogNarrativeService(
            new FixedLocalLlmRuntimeProvider(saturatedRuntime));
        CharacterLog saturatedLog = CreateProbeLog("asd", saturatedService);
        saturatedLog.AddActivity(CreateMajorActivity(
            StartEvent,
            CharacterActivityOutcomes.Started));
        saturatedLog.AddActivity(CreateMajorActivity(
            AlternateStartEvent,
            CharacterActivityOutcomes.Started));
        saturatedLog.AddActivity(CreateMajorActivity(
            "작업 시작 · 청소 · 식당",
            CharacterActivityOutcomes.Started));
        saturatedLog.AddActivity(CreateMajorActivity(
            EndEvent,
            CharacterActivityOutcomes.Completed));
        Check(saturatedRuntime.PendingRecordCount == 3
            && saturatedLog.Entries.Count == 1
            && !saturatedLog.Entries[0].Contains(EndEvent)
            && saturatedService.ControlledFallbackCount == 1,
            "SATURATED_REQUESTS_KEEP_RAW_PRIVATE",
            $"pending={saturatedRuntime.PendingRecordCount}; visible={Compact(saturatedLog.Entries)}");

        DeferredCharacterRecordRuntime repeatedRuntime = new DeferredCharacterRecordRuntime();
        CharacterLogNarrativeService repeatedService = new CharacterLogNarrativeService(
            new FixedLocalLlmRuntimeProvider(repeatedRuntime));
        CharacterLog repeatedLog = CreateProbeLog("asd", repeatedService);
        repeatedLog.AddActivity(CreateMajorActivity(
            StartEvent,
            CharacterActivityOutcomes.Started));
        bool firstRepeatPendingHidden = repeatedLog.Entries.Count == 0;
        repeatedLog.AddActivity(CreateMajorActivity(
            StartEvent,
            CharacterActivityOutcomes.Started));
        Check(firstRepeatPendingHidden
            && repeatedLog.Entries.Count == 1
            && !repeatedLog.Entries[0].Contains(StartEvent)
            && repeatedService.ControlledFallbackCount == 1,
            "REPEATED_PENDING_ENTRY_FINALIZED",
            Compact(repeatedLog.Entries));

        Destroy(templateLog.gameObject);
        Destroy(successLog.gameObject);
        Destroy(failureLog.gameObject);
        Destroy(rejectedLog.gameObject);
        Destroy(saturatedLog.gameObject);
        Destroy(repeatedLog.gameObject);
        yield return null;
    }

    private IEnumerator VerifyNarrativeRecords()
    {
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        CharacterSummeryInfo summary = UnityEngine.Object.FindFirstObjectByType<CharacterSummeryInfo>();
        CharacterActor actor = UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null && candidate.LogComponent != null);
        CharacterLogNarrativeService narrative = scope != null && scope.Container != null
            ? scope.Container.Resolve<CharacterLogNarrativeService>()
            : null;
        LocalLlmRequestQueue queue = UnityEngine.Object.FindFirstObjectByType<LocalLlmRequestQueue>();

        Check(scope != null, "SCOPE", "runtime lifetime scope resolved");
        Check(summary != null, "SUMMARY", "character summary resolved");
        Check(actor != null, "ACTOR", "active character resolved");
        Check(narrative != null, "NARRATIVE_SERVICE", "narrative service resolved");
        Check(queue != null && queue.HasConfiguredEndpoint,
            "LOCAL_LLM", queue != null ? $"configured={queue.HasConfiguredEndpoint}" : "missing");
        if (summary == null || actor == null || narrative == null || queue == null)
        {
            yield break;
        }

        scope.Container.Resolve<DungeonStory.Foundation.IGameEventBus>().ShowInfo(actor);
        yield return null;
        Transform generated = summary.UI != null
            ? summary.UI.transform.Find("CharacterSummaryGeneratedView")
            : null;
        Button recordsTab = generated != null
            ? generated.Find("TabBar/RecordsTab")?.GetComponent<Button>()
            : null;
        GameObject recordsContent = generated != null
            ? generated.Find("Content/RecordsContent")?.gameObject
            : null;
        TMP_Text recordsText = recordsContent != null
            ? recordsContent.GetComponentInChildren<TMP_Text>(true)
            : null;
        Check(recordsTab != null && recordsContent != null && recordsText != null,
            "RECORDS_UI", "records tab and text resolved");
        if (recordsTab == null || recordsContent == null || recordsText == null)
        {
            yield break;
        }

        int requestedBefore = narrative.RequestedCount;
        int appliedBefore = narrative.AppliedCount;
        int fallbackBefore = narrative.ControlledFallbackCount;
        int templateBefore = narrative.TemplateLineCount;
        string displayName = actor.Identity != null && !string.IsNullOrWhiteSpace(actor.Identity.DisplayName)
            ? actor.Identity.DisplayName
            : actor.name;
        string requiredSubject = CharacterLogNarrativeService.BuildRequiredSubject(displayName);
        List<CharacterLogEntry> targetEntries = new List<CharacterLogEntry>();
        Action<CharacterLogEntry> captureTargetEntry = entry =>
        {
            if (entry.OriginalMessage == StartEvent
                || entry.OriginalMessage == AlternateStartEvent
                || entry.OriginalMessage == EndEvent)
            {
                targetEntries.Add(entry);
            }
        };
        actor.LogComponent.OnLogAdded += captureTargetEntry;
        actor.AddActivity(CreateWorkActivity(
            StartEvent,
            "work:alchemy-research",
            CharacterActivityOutcomes.Started));
        actor.AddActivity(CreateWorkActivity(
            AlternateStartEvent,
            "work:weapon-sales",
            CharacterActivityOutcomes.Started));
        actor.AddActivity(CreateWorkActivity(
            EndEvent,
            "work:alchemy-research",
            CharacterActivityOutcomes.Completed));
        actor.LogComponent.OnLogAdded -= captureTargetEntry;
        PressButton(recordsTab);

        IReadOnlyList<string> immediateEntries = actor.LogComponent.Entries;
        summary.RefreshLogText();
        bool exactTargetsCaptured = targetEntries.Count == 3;
        bool noTargetRawVisible = exactTargetsCaptured;
        if (exactTargetsCaptured)
        {
            for (int i = 0; i < targetEntries.Count; i++)
            {
                CharacterLogEntry target = targetEntries[i];
                if (actor.LogComponent.TryGetVisibleDisplayLine(target.EntryId, out string visibleLine)
                    && (visibleLine == target.DisplayLine || visibleLine == target.OriginalMessage))
                {
                    noTargetRawVisible = false;
                    break;
                }
            }
        }
        Check(exactTargetsCaptured,
            "TARGET_ENTRIES_CAPTURED",
            $"count={targetEntries.Count}");
        Check(
            noTargetRawVisible
                && !immediateEntries.Contains(StartEvent)
                && !immediateEntries.Contains(AlternateStartEvent)
                && !immediateEntries.Contains(EndEvent),
            "PENDING_RAW_ENTRIES_HIDDEN",
            Compact(immediateEntries));
        Check(
            !recordsText.text.Contains(StartEvent)
                && !recordsText.text.Contains(AlternateStartEvent)
                && !recordsText.text.Contains(EndEvent),
            "PENDING_RAW_UI_HIDDEN", recordsText.text);
        int immediateRequested = narrative.RequestedCount - requestedBefore;
        int immediateFallbacks = narrative.ControlledFallbackCount - fallbackBefore;
        int immediateTemplates = narrative.TemplateLineCount - templateBefore;
        Check(immediateTemplates >= 3 && immediateRequested == 0,
            "TEMPLATE_RECORDS_RESOLVED",
            $"templates={immediateTemplates}; requests={immediateRequested}; fallbacks={immediateFallbacks}");

        yield return new WaitForEndOfFrame();
        Texture2D pendingCapture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(
            CharacterLogNarrativePlayModeVerifier.PendingCapturePath,
            pendingCapture.EncodeToPNG());
        Destroy(pendingCapture);
        report.Add($"pendingCapture={CharacterLogNarrativePlayModeVerifier.PendingCapturePath}");

        float timeoutAt = Time.realtimeSinceStartup + 1f;
        while (exactTargetsCaptured
            && targetEntries.Any(entry =>
                !actor.LogComponent.TryGetVisibleDisplayLine(entry.EntryId, out _))
            && Time.realtimeSinceStartup < timeoutAt)
        {
            yield return null;
        }

        string rewrittenStart = string.Empty;
        string rewrittenAlternateStart = string.Empty;
        string rewrittenEnd = string.Empty;
        bool startFinalized = exactTargetsCaptured
            && actor.LogComponent.TryGetVisibleDisplayLine(
                targetEntries[0].EntryId,
                out rewrittenStart);
        bool alternateStartFinalized = exactTargetsCaptured
            && actor.LogComponent.TryGetVisibleDisplayLine(
                targetEntries[1].EntryId,
                out rewrittenAlternateStart);
        bool endFinalized = exactTargetsCaptured
            && actor.LogComponent.TryGetVisibleDisplayLine(
                targetEntries[2].EntryId,
                out rewrittenEnd);
        summary.RefreshLogText();
        bool allApplied = startFinalized && alternateStartFinalized && endFinalized;
        bool varied = rewrittenStart != StartEvent
            && rewrittenAlternateStart != AlternateStartEvent
            && rewrittenEnd != EndEvent
            && rewrittenStart != rewrittenAlternateStart
            && rewrittenStart != rewrittenEnd
            && !rewrittenStart.Contains(" · ")
            && !rewrittenAlternateStart.Contains(" · ")
            && !rewrittenEnd.Contains(" · ");
        string startCadence = BuildCadenceSignature(rewrittenStart, requiredSubject, StartEvent);
        string alternateCadence = BuildCadenceSignature(
            rewrittenAlternateStart,
            requiredSubject,
            AlternateStartEvent);
        bool visible = recordsContent.activeInHierarchy
            && recordsText.text.Contains(rewrittenStart)
            && recordsText.text.Contains(rewrittenAlternateStart)
            && recordsText.text.Contains(rewrittenEnd);
        Check(allApplied, "NARRATIVES_APPLIED",
            $"targetFinalized={startFinalized},{alternateStartFinalized},{endFinalized}; "
                + $"applied={appliedBefore}->{narrative.AppliedCount}; error={narrative.LastError}; response={narrative.LastResponse}");
        Check(
            narrative.ControlledFallbackCount >= fallbackBefore,
            "CONTROLLED_FALLBACK",
            $"count={fallbackBefore}->{narrative.ControlledFallbackCount}");
        Check(varied, "VARIED_RECORDS",
            $"{rewrittenStart} || {rewrittenAlternateStart} || {rewrittenEnd}");
        Check(
            !string.Equals(startCadence, alternateCadence, StringComparison.Ordinal),
            "VARIED_START_CADENCE",
            $"{startCadence} || {alternateCadence}");
        Check(
            rewrittenStart.Contains(requiredSubject, StringComparison.Ordinal)
                && rewrittenAlternateStart.Contains(requiredSubject, StringComparison.Ordinal)
                && rewrittenEnd.Contains(requiredSubject, StringComparison.Ordinal),
            "TEMPLATE_MICRO_STORIES",
            $"{rewrittenStart} || {rewrittenAlternateStart} || {rewrittenEnd}");
        Check(
            rewrittenStart.Length <= CharacterLogNarrativeService.MaxLineCharacters
                && rewrittenAlternateStart.Length <= CharacterLogNarrativeService.MaxLineCharacters
                && rewrittenEnd.Length <= CharacterLogNarrativeService.MaxLineCharacters,
            "COMPACT_BREATH",
            $"max={CharacterLogNarrativeService.MaxLineCharacters}; lengths={rewrittenStart.Length},{rewrittenAlternateStart.Length},{rewrittenEnd.Length}");
        Check(
            rewrittenStart.StartsWith(requiredSubject + " ", StringComparison.Ordinal)
                && rewrittenAlternateStart.StartsWith(requiredSubject + " ", StringComparison.Ordinal)
                && rewrittenEnd.StartsWith(requiredSubject + " ", StringComparison.Ordinal),
            "GRAMMATICAL_SUBJECT",
            $"required={requiredSubject}; {rewrittenStart} || {rewrittenAlternateStart} || {rewrittenEnd}");
        Check(visible, "REWRITES_VISIBLE", recordsText.text);

        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(CharacterLogNarrativePlayModeVerifier.CapturePath, capture.EncodeToPNG());
        Destroy(capture);
        report.Add($"capture={CharacterLogNarrativePlayModeVerifier.CapturePath}");
    }

    private static CharacterActivityEvent CreateWorkActivity(
        string factText,
        string actionId,
        string outcomeId)
    {
        return CharacterActivityEvent.Create(
            CharacterActivityKinds.Work,
            outcomeId,
            factText,
            actionId: actionId,
            narrativeEligible: true);
    }

    private static CharacterActivityEvent CreateMajorActivity(string factText, string outcomeId)
    {
        return CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            outcomeId,
            factText,
            actionId: "combat:truth-core:" + StableMajorHash(factText),
            targetName: "진실핵",
            reasonCode: "truth_core:" + StableMajorHash(factText),
            narrativeEligible: true);
    }

    private static int StableMajorHash(string value)
    {
        unchecked
        {
            int hash = 23;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    hash = hash * 31 + value[i];
                }
            }

            return Math.Abs(hash == int.MinValue ? 0 : hash);
        }
    }

    private static void PressButton(Button button)
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        };
        button.OnPointerClick(eventData);
    }

    private static CharacterLog CreateProbeLog(
        string objectName,
        ICharacterLogNarrativeService narrativeService)
    {
        GameObject probe = new GameObject(objectName);
        CharacterLog characterLog = probe.AddComponent<CharacterLog>();
        characterLog.ConstructCharacterLog(narrativeService);
        return characterLog;
    }

    private static string BuildCadenceSignature(
        string line,
        string requiredSubject,
        string source)
    {
        string signature = line ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(requiredSubject))
        {
            signature = signature.Replace(requiredSubject, "<subject>");
        }

        IReadOnlyList<string> phrases = CharacterLogNarrativeService.ExtractRequiredPhrases(source);
        foreach (string phrase in phrases.OrderByDescending(value => value.Length))
        {
            signature = signature.Replace(phrase, "<fact>");
        }

        return string.Join(
            " ",
            signature.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private void Check(bool passed, string key, string detail)
    {
        report.Add($"{key}={(passed ? "PASS" : "FAIL")}; {detail}");
        if (!passed)
        {
            failures.Add(key + ": " + detail);
        }
    }

    private void Finish()
    {
        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        bool passed = failures.Count == 0 && capturedErrors.Count == 0 && capturedWarnings.Count == 0;
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        File.WriteAllText(CharacterLogNarrativePlayModeVerifier.ReportPath, string.Join("\n", report));
        if (passed)
        {
            Debug.Log("Character log narrative PlayMode verification passed. "
                + CharacterLogNarrativePlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("Character log narrative PlayMode verification failed. "
                + CharacterLogNarrativePlayModeVerifier.ReportPath);
        }
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace)
                ? condition
                : condition + "\n" + stackTrace);
        }
    }

    private static string Compact(IReadOnlyList<string> values)
    {
        return values == null || values.Count == 0
            ? "<none>"
            : string.Join(" || ", values.Select(value => value.Replace('\n', ' ').Replace('\r', ' ')));
    }
}

internal sealed class FixedLocalLlmRuntimeProvider : ILocalLlmRuntimeProvider
{
    private readonly ILocalLlmRuntime runtime;

    public FixedLocalLlmRuntimeProvider(ILocalLlmRuntime runtime)
    {
        this.runtime = runtime;
    }

    public bool TryGetRuntime(out ILocalLlmRuntime resolvedRuntime)
    {
        resolvedRuntime = runtime;
        return resolvedRuntime != null;
    }

    public ILocalLlmRuntime GetRequiredRuntime()
    {
        return runtime ?? throw new InvalidOperationException("A fixed Local LLM runtime is required.");
    }
}

internal sealed class DeferredCharacterRecordRuntime : ILocalLlmRuntime
{
    private readonly List<Action<LocalLlmResult>> callbacks = new List<Action<LocalLlmResult>>();
    private readonly List<string> originalTexts = new List<string>();

    public bool AcceptRecordRequests { get; set; } = true;
    public int PendingRecordCount => callbacks.Count;

    public bool GenerateCharacterRecordAsync(
        string prompt,
        string originalText,
        Action<LocalLlmResult> callback)
    {
        if (!AcceptRecordRequests)
        {
            return false;
        }

        callbacks.Add(callback);
        originalTexts.Add(originalText ?? string.Empty);
        return true;
    }

    public void CompleteNext(
        LocalLlmRequestStatus status,
        string content,
        string error = "")
    {
        Action<LocalLlmResult> callback = callbacks[0];
        string originalText = originalTexts[0];
        callbacks.RemoveAt(0);
        originalTexts.RemoveAt(0);
        callback(new LocalLlmResult(status, content, error, originalText));
    }

    public bool GeneratePersonaAsync(string prompt, Action<LocalLlmResult> callback) => false;
    public bool GenerateCharacterSkillAsync(string prompt, Action<LocalLlmResult> callback) => false;
    public bool GenerateMacroGoalAsync(string prompt, Action<LocalLlmResult> callback) => false;
    public bool GenerateMoodImpulseAsync(string prompt, Action<LocalLlmResult> callback) => false;
    public bool GenerateSocialRumorAsync(string prompt, Action<LocalLlmResult> callback) => false;
    public bool GenerateFacilityEvolutionAsync(string prompt, Action<LocalLlmResult> callback) => false;
    public bool GenerateBubbleLineAsync(
        string prompt,
        string originalText,
        Action<LocalLlmResult> callback) => false;
}
