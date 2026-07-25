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
        PreparedStartPartySnapshot preparedStartParty = null)
    {
        Mode = mode;
        SlotId = slotId ?? string.Empty;
        Difficulty = difficulty;
        PreparedStartParty = preparedStartParty;
    }

    public DungeonGameplayLaunchMode Mode { get; }
    public string SlotId { get; }
    public DungeonDifficulty Difficulty { get; }
    public PreparedStartPartySnapshot PreparedStartParty { get; }
}

public readonly struct DungeonPreparationLaunchRequest
{
    public DungeonPreparationLaunchRequest(DungeonDifficulty difficulty, int runSeed)
    {
        Difficulty = difficulty;
        RunSeed = runSeed;
    }

    public DungeonDifficulty Difficulty { get; }
    public int RunSeed { get; }
}

public interface IDungeonSceneNavigator
{
    bool IsTransitioning { get; }
    bool StartNewGame();
    bool StartNewGame(DungeonDifficulty difficulty);
    bool StartNewPreparation(DungeonDifficulty difficulty);
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

    private static DungeonSceneTransitionHost transitionHost;
    private static DungeonPreparationLaunchRequest? pendingPreparationLaunch;
    private static DungeonGameplayLaunchRequest? pendingGameplayLaunch;
    private static string pendingTitleMessage = string.Empty;
    private static bool isTransitioning;

    private readonly IGameTimeScaleController timeScaleController;
    private readonly IUiClock uiClock;

    public DungeonSceneNavigator()
        : this(new UnityGameTimeScaleController(), new UnityUiClock())
    {
    }

    public DungeonSceneNavigator(
        IGameTimeScaleController timeScaleController,
        IUiClock uiClock)
    {
        this.timeScaleController = timeScaleController
            ?? throw new ArgumentNullException(nameof(timeScaleController));
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
    }

    public bool IsTransitioning => isTransitioning;

    public bool StartNewGame()
    {
        return StartNewGame(DungeonDifficulty.Normal);
    }

    public bool StartNewGame(DungeonDifficulty difficulty)
    {
        return StartNewPreparation(difficulty);
    }

    public bool StartNewPreparation(DungeonDifficulty difficulty)
    {
        if (!BeginTransition(PreparationSceneName, HandlePreparationTransitionFailure))
        {
            return false;
        }

        pendingTitleMessage = string.Empty;
        pendingGameplayLaunch = null;
        pendingPreparationLaunch = new DungeonPreparationLaunchRequest(
            difficulty,
            CreateRunSeed(difficulty));
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
            preparedStartParty: preparedStartParty));
    }

    public bool StartNewGameDirectForDebug(DungeonDifficulty difficulty)
    {
        return BeginGameplayTransition(new DungeonGameplayLaunchRequest(
            DungeonGameplayLaunchMode.NewRun,
            difficulty: difficulty));
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

        pendingGameplayLaunch = null;
        pendingPreparationLaunch = null;
        pendingTitleMessage = message?.Trim() ?? string.Empty;
        return true;
    }

    public bool TryConsumePreparationLaunch(out DungeonPreparationLaunchRequest request)
    {
        if (!pendingPreparationLaunch.HasValue)
        {
            request = new DungeonPreparationLaunchRequest(
                DungeonDifficulty.Normal,
                CreateRunSeed(DungeonDifficulty.Normal));
            return false;
        }

        request = pendingPreparationLaunch.Value;
        pendingPreparationLaunch = null;
        return true;
    }

    public bool TryConsumeGameplayLaunch(out DungeonGameplayLaunchRequest request)
    {
        if (!pendingGameplayLaunch.HasValue)
        {
            request = default;
            return false;
        }

        request = pendingGameplayLaunch.Value;
        pendingGameplayLaunch = null;
        return request.Mode != DungeonGameplayLaunchMode.None;
    }

    public string ConsumeTitleMessage()
    {
        string message = pendingTitleMessage;
        pendingTitleMessage = string.Empty;
        return message;
    }

    private bool BeginGameplayTransition(DungeonGameplayLaunchRequest request)
    {
        if (!BeginTransition(GameplaySceneName, HandleGameplayTransitionFailure))
        {
            return false;
        }

        pendingTitleMessage = string.Empty;
        pendingPreparationLaunch = null;
        pendingGameplayLaunch = request;
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
        if (isTransitioning || string.IsNullOrWhiteSpace(targetScene))
        {
            return false;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            onFailure?.Invoke($"Scene '{targetScene}' is not available in build settings.");
            return false;
        }

        timeScaleController.Scale = 1f;
        isTransitioning = true;
        GameObject hostObject = new GameObject("DungeonSceneTransitionHost");
        UnityEngine.Object.DontDestroyOnLoad(hostObject);
        transitionHost = hostObject.AddComponent<DungeonSceneTransitionHost>();
        transitionHost.Begin(targetScene, CompleteTransition, onFailure, uiClock);
        return true;
    }

    private static void CompleteTransition()
    {
        isTransitioning = false;
        transitionHost = null;
    }

    private static void HandleGameplayTransitionFailure(string message)
    {
        pendingPreparationLaunch = null;
        pendingGameplayLaunch = null;
        pendingTitleMessage = string.IsNullOrWhiteSpace(message)
            ? "게임 화면을 불러오지 못했습니다."
            : message;
        CompleteTransition();
    }

    private static void HandleTitleTransitionFailure(string message)
    {
        pendingTitleMessage = string.IsNullOrWhiteSpace(message)
            ? "타이틀 화면을 불러오지 못했습니다."
            : message;
        CompleteTransition();
    }

    private static void HandlePreparationTransitionFailure(string message)
    {
        pendingPreparationLaunch = null;
        pendingTitleMessage = string.IsNullOrWhiteSpace(message)
            ? "준비 화면을 불러오지 못했습니다."
            : message;
        CompleteTransition();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        transitionHost = null;
        pendingPreparationLaunch = null;
        pendingGameplayLaunch = null;
        pendingTitleMessage = string.Empty;
        isTransitioning = false;
    }
}

public sealed class DungeonGameplayLaunchController : IStartable, ITickable
{
    private readonly IDungeonSceneNavigator sceneNavigator;
    private readonly IDungeonGameSaveSlotService slotService;
    private readonly DungeonSceneRuntimeReferences sceneReferences;
    private readonly IInvasionThreatRuntimeProvider threatProvider;
    private readonly IPreparedStartPartyGameplayApplier preparedStartPartyApplier;
    private readonly IStartPartyPreparationService startPartyPreparationService;
    private readonly IOwnerCandidateCatalog ownerCandidateCatalog;

    private DungeonGameplayLaunchRequest request;
    private bool pending;
    private string pendingTitleFailure = string.Empty;

    public DungeonGameplayLaunchController(
        IDungeonSceneNavigator sceneNavigator,
        IDungeonGameSaveSlotService slotService,
        DungeonSceneRuntimeReferences sceneReferences,
        IInvasionThreatRuntimeProvider threatProvider,
        IPreparedStartPartyGameplayApplier preparedStartPartyApplier,
        IStartPartyPreparationService startPartyPreparationService,
        IOwnerCandidateCatalog ownerCandidateCatalog)
    {
        this.sceneNavigator = sceneNavigator ?? throw new ArgumentNullException(nameof(sceneNavigator));
        this.slotService = slotService ?? throw new ArgumentNullException(nameof(slotService));
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
        this.threatProvider = threatProvider ?? throw new ArgumentNullException(nameof(threatProvider));
        this.preparedStartPartyApplier = preparedStartPartyApplier
            ?? throw new ArgumentNullException(nameof(preparedStartPartyApplier));
        this.startPartyPreparationService = startPartyPreparationService
            ?? throw new ArgumentNullException(nameof(startPartyPreparationService));
        this.ownerCandidateCatalog = ownerCandidateCatalog
            ?? throw new ArgumentNullException(nameof(ownerCandidateCatalog));
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
                DeleteRunSlots();
                ApplyNewRunDifficulty(request.Difficulty);
                ApplyDebugFallbackNewRun(request.Difficulty);
                break;
            case DungeonGameplayLaunchMode.PreparedNewRun:
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

    private void ApplyDebugFallbackNewRun(DungeonDifficulty difficulty)
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
        if (threatProvider.TryGetRuntime(out InvasionThreatRuntime threat)
            && threat.Settings != null)
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
