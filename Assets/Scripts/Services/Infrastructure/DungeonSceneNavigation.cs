using System;
using System.Collections;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer.Unity;

public enum DungeonGameplayLaunchMode
{
    None,
    NewRun,
    PreparedNewRun,
    LoadSlot
}

public readonly struct DungeonGameplayLaunchRequest
{
    public DungeonGameplayLaunchRequest(
        DungeonGameplayLaunchMode mode,
        string slotId = "",
        DungeonDifficulty difficulty = DungeonDifficulty.Normal,
        PreparedStartPartySnapshot preparedStartParty = null,
        DungeonSurvivalPressure survivalPressure =
            DungeonSurvivalPressure.Standard)
    {
        Mode = mode;
        SlotId = slotId ?? string.Empty;
        Difficulty = difficulty;
        PreparedStartParty = preparedStartParty;
        SurvivalPressure =
            DungeonSurvivalPressureRules.Normalize((int)survivalPressure);
    }

    public DungeonGameplayLaunchMode Mode { get; }
    public string SlotId { get; }
    public DungeonDifficulty Difficulty { get; }
    public PreparedStartPartySnapshot PreparedStartParty { get; }
    public DungeonSurvivalPressure SurvivalPressure { get; }
}

public readonly struct DungeonPreparationLaunchRequest
{
    public DungeonPreparationLaunchRequest(
        DungeonDifficulty difficulty,
        int runSeed,
        DungeonSurvivalPressure survivalPressure =
            DungeonSurvivalPressure.Standard)
    {
        Difficulty = difficulty;
        RunSeed = runSeed;
        SurvivalPressure =
            DungeonSurvivalPressureRules.Normalize((int)survivalPressure);
    }

    public DungeonDifficulty Difficulty { get; }
    public int RunSeed { get; }
    public DungeonSurvivalPressure SurvivalPressure { get; }
}

public interface IDungeonSceneNavigator
{
    bool IsTransitioning { get; }
    bool StartNewGame();
    bool StartNewGame(DungeonDifficulty difficulty);
    bool StartNewGame(
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure);
    bool StartNewPreparation(DungeonDifficulty difficulty);
    bool StartNewPreparation(
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure);
    bool StartPreparedNewGame(PreparedStartPartySnapshot preparedStartParty);
    bool LoadGame(string slotId);
    bool LoadTitle(string message = "");
    bool TryConsumePreparationLaunch(out DungeonPreparationLaunchRequest request);
    bool TryConsumeGameplayLaunch(out DungeonGameplayLaunchRequest request);
    string ConsumeTitleMessage();
}

public sealed class DungeonSceneNavigator : IDungeonSceneNavigator
{
    public const string TitleSceneName = "TitleScene";
    public const string PreparationSceneName = "StartPreparationScene";
    public const string GameplaySceneName = "GameplayScene";
    public const string DebugSampleSceneName = "SampleScene";

    private readonly IGameTimeScaleController timeScaleController;
    private readonly IUiClock uiClock;

    public DungeonSceneNavigator(
        IGameTimeScaleController timeScaleController,
        IUiClock uiClock)
    {
        this.timeScaleController = timeScaleController
            ?? throw new ArgumentNullException(nameof(timeScaleController));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public bool IsTransitioning => GetMailbox().IsTransitioning;

    public bool StartNewGame()
    {
        return StartNewGame(DungeonDifficulty.Normal);
    }

    public bool StartNewGame(DungeonDifficulty difficulty)
    {
        return StartNewGame(
            difficulty,
            DungeonSurvivalPressure.Standard);
    }

    public bool StartNewGame(
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure)
    {
        return StartNewPreparation(difficulty, survivalPressure);
    }

    public bool StartNewPreparation(DungeonDifficulty difficulty)
    {
        return StartNewPreparation(
            difficulty,
            DungeonSurvivalPressure.Standard);
    }

    public bool StartNewPreparation(
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure)
    {
        if (!BeginTransition(PreparationSceneName, HandlePreparationTransitionFailure))
        {
            return false;
        }

        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        mailbox.PendingTitleMessage = string.Empty;
        mailbox.PendingGameplayLaunch = null;
        mailbox.PendingPreparationLaunch = new DungeonPreparationLaunchRequest(
            difficulty,
            CreateRunSeed(difficulty),
            survivalPressure);
        return true;
    }

    public bool StartPreparedNewGame(PreparedStartPartySnapshot preparedStartParty)
    {
        if (preparedStartParty == null || !preparedStartParty.IsValid)
        {
            return false;
        }

        return BeginGameplayTransition(new DungeonGameplayLaunchRequest(
            DungeonGameplayLaunchMode.PreparedNewRun,
            difficulty: preparedStartParty.difficulty,
            preparedStartParty: preparedStartParty,
            survivalPressure: preparedStartParty.survivalPressure));
    }

    public bool StartNewGameDirectForDebug(DungeonDifficulty difficulty)
    {
        return StartNewGameDirectForDebug(
            difficulty,
            DungeonSurvivalPressure.Standard);
    }

    public bool StartNewGameDirectForDebug(
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure)
    {
        return BeginGameplayTransition(new DungeonGameplayLaunchRequest(
            DungeonGameplayLaunchMode.NewRun,
            difficulty: difficulty,
            survivalPressure: survivalPressure));
    }

    public bool LoadGame(string slotId)
    {
        if (string.IsNullOrWhiteSpace(slotId))
        {
            return false;
        }

        return BeginGameplayTransition(new DungeonGameplayLaunchRequest(
            DungeonGameplayLaunchMode.LoadSlot,
            slotId.Trim()));
    }

    public bool LoadTitle(string message = "")
    {
        if (!BeginTransition(TitleSceneName, HandleTitleTransitionFailure))
        {
            return false;
        }

        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        mailbox.PendingGameplayLaunch = null;
        mailbox.PendingPreparationLaunch = null;
        mailbox.PendingTitleMessage = message?.Trim() ?? string.Empty;
        return true;
    }

    public bool TryConsumePreparationLaunch(out DungeonPreparationLaunchRequest request)
    {
        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        if (!mailbox.PendingPreparationLaunch.HasValue)
        {
            request = new DungeonPreparationLaunchRequest(
                DungeonDifficulty.Normal,
                CreateRunSeed(DungeonDifficulty.Normal));
            return false;
        }

        request = mailbox.PendingPreparationLaunch.Value;
        mailbox.PendingPreparationLaunch = null;
        return true;
    }

    public bool TryConsumeGameplayLaunch(out DungeonGameplayLaunchRequest request)
    {
        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        if (!mailbox.PendingGameplayLaunch.HasValue)
        {
            request = default;
            return false;
        }

        request = mailbox.PendingGameplayLaunch.Value;
        mailbox.PendingGameplayLaunch = null;
        return request.Mode != DungeonGameplayLaunchMode.None;
    }

    public string ConsumeTitleMessage()
    {
        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        string message = mailbox.PendingTitleMessage;
        mailbox.PendingTitleMessage = string.Empty;
        return message;
    }

    private bool BeginGameplayTransition(DungeonGameplayLaunchRequest request)
    {
        if (!BeginTransition(GameplaySceneName, HandleGameplayTransitionFailure))
        {
            return false;
        }

        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        mailbox.PendingTitleMessage = string.Empty;
        mailbox.PendingPreparationLaunch = null;
        mailbox.PendingGameplayLaunch = request;
        return true;
    }

    private static int CreateRunSeed(DungeonDifficulty difficulty)
    {
        unchecked
        {
            int seed = Environment.TickCount;
            seed = (seed * 397) ^ DateTime.UtcNow.Ticks.GetHashCode();
            seed = (seed * 397) ^ difficulty.GetHashCode();
            return seed == 0 ? 1 : seed;
        }
    }

    private bool BeginTransition(string targetScene, Action<string> onFailure)
    {
        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        if (mailbox.IsTransitioning || string.IsNullOrWhiteSpace(targetScene))
        {
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            onFailure?.Invoke($"Scene '{targetScene}' is not available in build settings.");
            return false;
        }

        timeScaleController.Scale = 1f;
        mailbox.IsTransitioning = true;
        GameObject hostObject = new GameObject("DungeonSceneTransitionHost");
        UnityEngine.Object.DontDestroyOnLoad(hostObject);
        mailbox.TransitionHost = hostObject.AddComponent<DungeonSceneTransitionHost>();
        mailbox.TransitionHost.Begin(
            targetScene,
            CompleteTransition,
            onFailure,
            uiClock);
        return true;
    }

    private void CompleteTransition()
    {
        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        mailbox.IsTransitioning = false;
        mailbox.TransitionHost = null;
    }

    private void HandleGameplayTransitionFailure(string message)
    {
        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        mailbox.PendingPreparationLaunch = null;
        mailbox.PendingGameplayLaunch = null;
        mailbox.PendingTitleMessage = string.IsNullOrWhiteSpace(message)
            ? "게임 화면을 불러오지 못했습니다."
            : message;
        CompleteTransition();
    }

    private void HandleTitleTransitionFailure(string message)
    {
        GetMailbox().PendingTitleMessage = string.IsNullOrWhiteSpace(message)
            ? "타이틀 화면을 불러오지 못했습니다."
            : message;
        CompleteTransition();
    }

    private void HandlePreparationTransitionFailure(string message)
    {
        DungeonSceneTransitionMailbox mailbox = GetMailbox();
        mailbox.PendingPreparationLaunch = null;
        mailbox.PendingTitleMessage = string.IsNullOrWhiteSpace(message)
            ? "준비 화면을 불러오지 못했습니다."
            : message;
        CompleteTransition();
    }

    private static DungeonSceneTransitionMailbox GetMailbox()
    {
        DungeonSceneTransitionMailbox existing =
            UnityEngine.Object.FindFirstObjectByType<DungeonSceneTransitionMailbox>(
                FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        GameObject mailboxObject = new GameObject("DungeonSceneTransitionMailbox");
        UnityEngine.Object.DontDestroyOnLoad(mailboxObject);
        return mailboxObject.AddComponent<DungeonSceneTransitionMailbox>();
    }
}

public sealed class DungeonSceneTransitionMailbox : MonoBehaviour
{
    public DungeonPreparationLaunchRequest? PendingPreparationLaunch { get; set; }
    public DungeonGameplayLaunchRequest? PendingGameplayLaunch { get; set; }
    public string PendingTitleMessage { get; set; } = string.Empty;
    public bool IsTransitioning { get; set; }
    public DungeonSceneTransitionHost TransitionHost { get; set; }
}

public sealed class DungeonGameplayLaunchController : IStartable, ITickable
{
    private readonly IDungeonSceneNavigator sceneNavigator;
    private readonly IDungeonGameSaveSlotService slotService;
    private readonly DungeonSceneRuntimeReferences sceneReferences;
    private readonly InvasionThreatRuntime threat;
    private readonly IPreparedStartPartyGameplayApplier preparedStartPartyApplier;
    private readonly IStartPartyPreparationService startPartyPreparationService;
    private readonly IOwnerCandidateCatalog ownerCandidateCatalog;
    private readonly IDungeonSpaceExpansionCommand dungeonSpaceExpansion;

    private DungeonGameplayLaunchRequest request;
    private bool pending;
    private string pendingTitleFailure = string.Empty;

    public DungeonGameplayLaunchController(
        IDungeonSceneNavigator sceneNavigator,
        IDungeonGameSaveSlotService slotService,
        DungeonSceneRuntimeReferences sceneReferences,
        InvasionSceneRuntimeReferences invasionRuntimes,
        IPreparedStartPartyGameplayApplier preparedStartPartyApplier,
        IStartPartyPreparationService startPartyPreparationService,
        IOwnerCandidateCatalog ownerCandidateCatalog,
        IDungeonSpaceExpansionCommand dungeonSpaceExpansion)
    {
        this.sceneNavigator = sceneNavigator ?? throw new ArgumentNullException(nameof(sceneNavigator));
        this.slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
        threat = (invasionRuntimes
                ?? throw new ArgumentNullException(nameof(invasionRuntimes)))
            .Threat
            ?? throw new InvalidOperationException(
                $"{nameof(DungeonSceneNavigator)} requires a loaded {nameof(InvasionThreatRuntime)}.");
        this.preparedStartPartyApplier = preparedStartPartyApplier
            ?? throw new ArgumentNullException(nameof(preparedStartPartyApplier));
        this.startPartyPreparationService = startPartyPreparationService
            ?? throw new ArgumentNullException(nameof(startPartyPreparationService));
        this.ownerCandidateCatalog = ownerCandidateCatalog
            ?? throw new ArgumentNullException(nameof(ownerCandidateCatalog));
        this.dungeonSpaceExpansion = dungeonSpaceExpansion
            ?? throw new ArgumentNullException(nameof(dungeonSpaceExpansion));
    }

    public void Start()
    {
        pending = sceneNavigator.TryConsumeGameplayLaunch(out request);
    }

    public void Tick()
    {
        if (!string.IsNullOrWhiteSpace(pendingTitleFailure) && !sceneNavigator.IsTransitioning)
        {
            string message = pendingTitleFailure;
            pendingTitleFailure = string.Empty;
            sceneNavigator.LoadTitle(message);
            return;
        }

        if (!pending)
        {
            return;
        }

        pending = false;
        switch (request.Mode)
        {
            case DungeonGameplayLaunchMode.NewRun:
                if (!TryReconcileNewRunSpace())
                {
                    break;
                }
                DeleteRunSlots();
                ApplyNewRunDifficulty(request.Difficulty);
                ApplyDebugFallbackNewRun(
                    request.Difficulty,
                    request.SurvivalPressure);
                break;
            case DungeonGameplayLaunchMode.PreparedNewRun:
                if (!TryReconcileNewRunSpace())
                {
                    break;
                }
                DeleteRunSlots();
                ApplyNewRunDifficulty(request.Difficulty);
                if (!preparedStartPartyApplier.TryApply(request.PreparedStartParty, out string message))
                {
                    pendingTitleFailure = message;
                }

                break;
            case DungeonGameplayLaunchMode.LoadSlot:
                RestoreSlot(request.SlotId);
                break;
        }
    }

    private bool TryReconcileNewRunSpace()
    {
        if (dungeonSpaceExpansion.TryReconcileNewRunTierZero(
                out _,
                out string failureReason))
        {
            return true;
        }

        pendingTitleFailure =
            "새 게임의 초기 던전 공간을 준비하지 못했습니다. "
            + failureReason;
        return false;
    }

    private void RestoreSlot(string slotId)
    {
        if (slotService.TryLoad(slotId, out DungeonGameRestoreReport report))
        {
            RefreshOwnerSelection();
            return;
        }

        string reason = report?.Errors != null && report.Errors.Count > 0
            ? string.Join(" ", report.Errors)
            : "저장 데이터를 복원하지 못했습니다.";
        pendingTitleFailure = reason;
    }

    private void ApplyDebugFallbackNewRun(
        DungeonDifficulty difficulty,
        DungeonSurvivalPressure survivalPressure)
    {
        CharacterSO owner = ownerCandidateCatalog.OwnerCandidates
            .FirstOrDefault(candidate => candidate != null);
        if (owner == null)
        {
            pendingTitleFailure = "새 런을 시작할 사장 후보가 없습니다.";
            return;
        }

        if (!startPartyPreparationService.Begin(owner, out string beginMessage))
        {
            pendingTitleFailure = beginMessage;
            return;
        }

        int seed = Environment.TickCount == 0 ? 1 : Environment.TickCount;
        if (!startPartyPreparationService.TryCreatePreparedSnapshot(
                difficulty,
                survivalPressure,
                seed,
                out PreparedStartPartySnapshot snapshot,
                out string snapshotMessage))
        {
            startPartyPreparationService.Cancel();
            pendingTitleFailure = snapshotMessage;
            return;
        }

        startPartyPreparationService.Cancel();
        if (!preparedStartPartyApplier.TryApply(snapshot, out string applyMessage))
        {
            pendingTitleFailure = applyMessage;
        }
    }

    private void RefreshOwnerSelection()
    {
        sceneReferences.OwnerSelectionPanel?.RefreshVisibility();
    }

    private void DeleteRunSlots()
    {
        slotService.Delete(DungeonGameSaveSlotService.AutoSaveSlot);
        slotService.Delete(DungeonGameSaveSlotService.QuickSaveSlot);
        slotService.Delete(DungeonGameSaveSlotService.ManualSaveSlot);
    }

    private void ApplyNewRunDifficulty(DungeonDifficulty difficulty)
    {
        if (threat.Settings != null)
        {
            threat.Settings.difficulty = DungeonDifficultyRules.ToLegacy(difficulty);
        }
    }
}

public sealed class DungeonSceneTransitionHost : MonoBehaviour
{
    private const float FadeSeconds = 0.18f;

    private Image blocker;
    private Action onComplete;
    private Action<string> onFailure;

    private IUiClock uiClock;

    public void Begin(
        string targetScene,
        Action complete,
        Action<string> failure,
        IUiClock uiClock)
    {
        onComplete = complete;
        onFailure = failure;
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        CreateOverlay();
        StartCoroutine(Transition(targetScene));
    }

    private IEnumerator Transition(string targetScene)
    {
        yield return Fade(0f, 1f);

        AsyncOperation operation;
        try
        {
            operation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        }
        catch (Exception exception)
        {
            Fail(exception.Message);
            yield break;
        }

        if (operation == null)
        {
            Fail($"Scene '{targetScene}' did not start loading.");
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return Fade(1f, 0f);
        onComplete?.Invoke();
        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < FadeSeconds)
        {
            elapsed += uiClock.DeltaTime;
            SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / FadeSeconds)));
            yield return null;
        }

        SetAlpha(to);
    }

    private void CreateOverlay()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;
        gameObject.AddComponent<CanvasScaler>();
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject blockerObject = new GameObject("SceneTransitionInputBlocker", typeof(RectTransform), typeof(Image));
        blockerObject.transform.SetParent(transform, false);
        RectTransform rect = blockerObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        blocker = blockerObject.GetComponent<Image>();
        blocker.raycastTarget = true;
        SetAlpha(0f);
    }

    private void SetAlpha(float alpha)
    {
        if (blocker != null)
        {
            blocker.color = new Color(0.018f, 0.027f, 0.031f, Mathf.Clamp01(alpha));
        }
    }

    private void Fail(string message)
    {
        onFailure?.Invoke(message);
        Destroy(gameObject);
    }
}
