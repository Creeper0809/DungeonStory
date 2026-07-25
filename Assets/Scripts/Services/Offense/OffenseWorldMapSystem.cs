using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using VContainer;

public class OffenseWorldMapRuntime : MonoBehaviour
{
    [SerializeField] private List<OffenseTargetDefinition> configuredTargets = new List<OffenseTargetDefinition>();
    [SerializeField] private bool preciseIntel;

    private readonly OffenseWorldMapState state = new OffenseWorldMapState();
    private List<OffenseTargetDefinition> targets;
    private IOffensePanelService panelService;
    private IGameEventBus gameEventBus;

    public event Action Changed;
    public event Action<OffenseTargetSnapshot> TargetSelected;

    public IOffenseWorldMapStateView State => state;
    public IReadOnlyList<OffenseTargetDefinition> TargetDefinitions
    {
        get
        {
            EnsureInitialized();
            return Array.AsReadOnly(targets.ToArray());
        }
    }

    public IReadOnlyList<OffenseTargetSnapshot> VisibleTargets
    {
        get
        {
            EnsureInitialized();
            return OffenseWorldMapService.GetVisibleTargetSnapshots(state, targets, preciseIntel);
        }
    }

    public float CurrentScanRange => OffenseWorldMapService.GetScanRange(state.ReconLevel);
    public int CampaignTargetCount
    {
        get
        {
            EnsureInitialized();
            return targets.Count;
        }
    }

    [Inject]
    public void Construct(
        IOffensePanelService panelService,
        IGameEventBus gameEventBus)
    {
        this.panelService = panelService
            ?? throw new ArgumentNullException(nameof(panelService));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    private void Awake()
    {
        StartWorldMap();
    }

    public void StartWorldMap(int reconLevel = 0)
    {
        state.Reset(reconLevel);
        targets = OffenseWorldMapService.NormalizeTargets(configuredTargets).ToList();
        OffenseWorldMapService.RevealTargetsInRange(state, targets);
        RaiseChanged();
    }

    public void RestorePersistentState(
        int reconLevel,
        string selectedTargetId,
        IEnumerable<string> knownTargetIds)
    {
        RestorePersistentState(
            reconLevel,
            selectedTargetId,
            knownTargetIds,
            Array.Empty<string>(),
            string.Empty);
    }

    public void RestorePersistentState(
        int reconLevel,
        string selectedTargetId,
        IEnumerable<string> knownTargetIds,
        IEnumerable<string> completedTargetIds,
        string revealedTruthTargetId)
    {
        EnsureInitialized();
        Dictionary<string, OffenseTargetDefinition> validTargets = targets
            .Where(target => target != null)
            .GroupBy(target => target.id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        HashSet<string> validTargetIds = new HashSet<string>(
            validTargets.Keys,
            StringComparer.Ordinal);
        string validTruthTargetId = validTargets.TryGetValue(
                revealedTruthTargetId ?? string.Empty,
                out OffenseTargetDefinition truthTarget)
            && truthTarget.revealsTruth
                ? truthTarget.id
                : string.Empty;
        state.Restore(
            reconLevel,
            selectedTargetId,
            (knownTargetIds ?? Array.Empty<string>()).Where(validTargetIds.Contains),
            (completedTargetIds ?? Array.Empty<string>()).Where(validTargetIds.Contains),
            validTruthTargetId);
        RaiseChanged();
    }

    public bool TryUpgradeRecon(out string message)
    {
        EnsureInitialized();
        if (!state.TryUpgradeRecon(OffenseWorldMapService.MaxReconLevel))
        {
            message = "정찰 범위가 이미 최대입니다";
            return false;
        }

        int newlyRevealed = OffenseWorldMapService.RevealTargetsInRange(state, targets);
        message = $"정찰 Lv.{state.ReconLevel}: 새 원정 대상 {newlyRevealed}개 발견";
        gameEventBus.RaiseAlert("정찰 강화", message, EventAlertImportance.Medium, "오펜스");
        RaiseChanged();
        return true;
    }

    public bool TrySelectTarget(string targetId, out OffenseTargetSnapshot snapshot, out string message)
    {
        EnsureInitialized();
        OffenseTargetDefinition target = OffenseWorldMapService.FindKnownTarget(state, targets, targetId);
        if (target == null)
        {
            snapshot = null;
            message = "발견되지 않은 원정 대상입니다";
            return false;
        }

        if (!OffenseWorldMapService.CanAttemptTarget(state, target, out message))
        {
            snapshot = target.ToSnapshot(preciseIntel, state);
            return false;
        }

        state.SetSelectedTarget(target.id);
        snapshot = target.ToSnapshot(preciseIntel, state);
        message = $"{snapshot.title} 선택";
        TargetSelected?.Invoke(snapshot);
        RaiseChanged();
        return true;
    }

    public bool TryGetKnownTargetSnapshot(string targetId, out OffenseTargetSnapshot snapshot)
    {
        EnsureInitialized();
        OffenseTargetDefinition target = OffenseWorldMapService.FindKnownTarget(state, targets, targetId);
        if (target == null)
        {
            snapshot = null;
            return false;
        }

        snapshot = target.ToSnapshot(preciseIntel, state);
        return true;
    }

    public bool TryRecordSuccessfulExpedition(
        string targetId,
        out OffenseTargetSnapshot completedTarget,
        out string message)
    {
        EnsureInitialized();
        OffenseTargetDefinition target = targets.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.id, targetId, StringComparison.Ordinal));
        if (target == null || !state.KnowTarget(targetId))
        {
            completedTarget = null;
            message = "알 수 없는 오펜스 목표입니다.";
            return false;
        }

        if (!OffenseWorldMapService.CanAttemptTarget(state, target, out message)
            || !state.MarkTargetCompleted(target.id))
        {
            completedTarget = target.ToSnapshot(preciseIntel, state);
            return false;
        }

        if (target.revealsTruth)
        {
            state.RevealTruth(target.id);
        }

        completedTarget = target.ToSnapshot(preciseIntel, state);
        message = target.revealsTruth
            ? "최종 오펜스를 마치고 던전의 진실을 밝혔습니다."
            : $"오펜스 목표 완료 {state.CompletedTargetCount}/{targets.Count}";
        RaiseChanged();

        if (target.revealsTruth)
        {
            gameEventBus.RaiseAlert(
                OffenseWorldMapService.TruthTitle,
                target.truthText,
                EventAlertImportance.High,
                "오펜스");
            gameEventBus.Publish(new OffenseTruthRevealedEvent(
                target.id,
                OffenseWorldMapService.TruthTitle,
                target.truthText));
        }
        else
        {
            gameEventBus.RaiseAlert(
                "오펜스 진척",
                message,
                EventAlertImportance.Medium,
                "오펜스");
        }

        return true;
    }

    public OffenseWorldMapPanel ShowWorldMap()
    {
        EnsureInitialized();
        return ResolvePanelService().ShowWorldMap(this);
    }

    public void SetPreciseIntelForDebug(bool value)
    {
        preciseIntel = value;
        RaiseChanged();
    }

    public void SetTargetsForDebug(IEnumerable<OffenseTargetDefinition> debugTargets)
    {
        configuredTargets = debugTargets?.Where((target) => target != null).ToList()
            ?? new List<OffenseTargetDefinition>();
        StartWorldMap(state.ReconLevel);
    }

    private void EnsureInitialized()
    {
        if (targets != null) return;

        targets = OffenseWorldMapService.NormalizeTargets(configuredTargets).ToList();
        if (state.KnownTargetIds.Count == 0)
        {
            OffenseWorldMapService.RevealTargetsInRange(state, targets);
        }
    }

    private void RaiseChanged()
    {
        if (targets == null)
        {
            return;
        }

        Changed?.Invoke();
    }

    private IOffensePanelService ResolvePanelService()
    {
        return panelService
            ?? throw new InvalidOperationException($"{nameof(OffenseWorldMapRuntime)} requires {nameof(IOffensePanelService)} injection.");
    }
}

public class OffenseWorldMapPanel : MonoBehaviour
{
    private OffenseWorldMapRuntime runtime;
    private TMP_Text headerText;
    private TMP_Text detailText;
    private RectTransform targetButtonRoot;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private IOffensePanelButtonFactory buttonFactory;

    public void Bind(OffenseWorldMapRuntime source, IOffensePanelButtonFactory buttonFactory)
    {
        runtime = source
            ?? throw new ArgumentNullException(nameof(source));
        this.buttonFactory = buttonFactory
            ?? throw new ArgumentNullException(nameof(buttonFactory));
        EnsureView();
        gameObject.SetActive(true);
        Render();
    }

    public void Render()
    {
        if (runtime == null)
        {
            return;
        }

        EnsureView();
        headerText.text = $"월드맵 / 정찰 Lv.{runtime.State.ReconLevel} / 범위 {runtime.CurrentScanRange:0.#}";
        ClearButtons();

        foreach (OffenseTargetSnapshot target in runtime.VisibleTargets)
        {
            GameObject buttonObject = RequireButtonFactory().CreateButton(
                targetButtonRoot,
                target.title,
                17f,
                () =>
                {
                    if (runtime.TrySelectTarget(target.id, out OffenseTargetSnapshot selected, out _))
                    {
                        detailText.text = selected.ToDetailText();
                    }

                    Render();
                });
            spawnedButtons.Add(buttonObject);
        }

        GameObject upgradeButton = RequireButtonFactory().CreateButton(
            targetButtonRoot,
            "정찰 강화",
            17f,
            () =>
            {
                runtime.TryUpgradeRecon(out string message);
                detailText.text = message;
                Render();
            });
        spawnedButtons.Add(upgradeButton);

        GameObject closeButton = RequireButtonFactory().CreateButton(
            targetButtonRoot,
            "닫기",
            17f,
            Hide);
        spawnedButtons.Add(closeButton);

        if (runtime.VisibleTargets.Count == 0)
        {
            detailText.text = "발견된 원정 대상이 없습니다.";
        }
        else if (!string.IsNullOrWhiteSpace(runtime.State.SelectedTargetId)
            && runtime.TryGetKnownTargetSnapshot(runtime.State.SelectedTargetId, out OffenseTargetSnapshot selected))
        {
            detailText.text = selected.ToDetailText();
        }
        else
        {
            detailText.text = runtime.VisibleTargets[0].ToDetailText();
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void EnsureView()
    {
        if (headerText != null && detailText != null && targetButtonRoot != null) return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        headerText = texts.FirstOrDefault((text) => text.name == "OffenseWorldMapHeader");
        detailText = texts.FirstOrDefault((text) => text.name == "OffenseWorldMapDetail");
        targetButtonRoot = GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault((rect) => rect.name == "OffenseWorldMapTargets");
    }

    private void ClearButtons()
    {
        foreach (GameObject button in spawnedButtons)
        {
            RequireButtonFactory().Release(button);
        }

        spawnedButtons.Clear();
    }

    internal void BindGeneratedView(
        TMP_Text headerText,
        TMP_Text detailText,
        RectTransform targetButtonRoot)
    {
        this.headerText = headerText != null
            ? headerText
            : throw new ArgumentNullException(nameof(headerText));
        this.detailText = detailText != null
            ? detailText
            : throw new ArgumentNullException(nameof(detailText));
        this.targetButtonRoot = targetButtonRoot != null
            ? targetButtonRoot
            : throw new ArgumentNullException(nameof(targetButtonRoot));
    }

    private IOffensePanelButtonFactory RequireButtonFactory()
    {
        return buttonFactory
            ?? throw new InvalidOperationException(
                $"{nameof(OffenseWorldMapPanel)} requires {nameof(IOffensePanelButtonFactory)} binding.");
    }
}
