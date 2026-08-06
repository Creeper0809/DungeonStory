using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using VContainer;

public class OffenseWorldMapRuntime : MonoBehaviour,
    IOffenseCampaignQuery,
    IOffenseCampaignCommands
{
    [SerializeField] private bool preciseIntel;

    private IOffenseCampaignStateAuthority campaign;
    private IOffenseCampaignRuntime campaignPersistence;
    private List<OffenseTargetDefinition> targets;
    private IGameEventBus gameEventBus;
    private IExternalInfluenceRuntime externalInfluence;
    private IOffenseCampaignCatalog targetCatalog;

    public event Action Changed;
    public event Action<OffenseTargetSnapshot> TargetSelected;

    public IOffenseWorldMapStateView State => campaign.State;
    public IOffenseCampaignRuntime Campaign => campaignPersistence
        ?? throw new InvalidOperationException(
            $"{nameof(OffenseWorldMapRuntime)} has no campaign persistence authority.");
    private OffenseWorldMapState MutableState => campaign.MutableState;
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
            return targets
                .Where(target => target != null
                    && MutableState.KnowTarget(target.id))
                .Select(target => target.ToSnapshot(
                    HasPreciseIntel(target.id),
                    MutableState))
                .ToList();
        }
    }

    public float CurrentScanRange => OffenseWorldMapService.GetScanRange(MutableState.ReconLevel);
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
        IGameEventBus gameEventBus,
        IExternalInfluenceRuntime externalInfluence,
        IOffenseCampaignStateAuthority campaign,
        IOffenseCampaignRuntime campaignPersistence,
        IOffenseCampaignCatalog targetCatalog)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.externalInfluence = externalInfluence;
        this.campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        this.campaignPersistence = campaignPersistence
            ?? throw new ArgumentNullException(nameof(campaignPersistence));
        if (!ReferenceEquals(campaign, campaignPersistence))
        {
            throw new InvalidOperationException(
                "Offense campaign query/command and persistence boundaries must share one authority instance.");
        }
        this.targetCatalog = targetCatalog
            ?? throw new ArgumentNullException(nameof(targetCatalog));
        StartWorldMap();
    }

    public void StartWorldMap(int reconLevel = 0)
    {
        targets = targetCatalog.Targets
            .Select(value => value?.CreateRuntimeCopy()
                ?? throw new InvalidOperationException(
                    "Offense campaign catalog contains a null target."))
            .ToList();
        campaign.ConfigureTargets(targets);
        campaign.Reset(reconLevel);
        OffenseWorldMapService.RevealTargetsInRange(MutableState, targets);
        RaiseChanged();
    }

    public bool TryUpgradeRecon(out string message)
    {
        EnsureInitialized();
        if (!MutableState.TryUpgradeRecon(OffenseWorldMapService.MaxReconLevel))
        {
            message = "정찰 범위가 이미 최대입니다";
            return false;
        }

        int newlyRevealed = OffenseWorldMapService.RevealTargetsInRange(MutableState, targets);
        externalInfluence?.AddScoutingLabor(60f);
        message = $"정찰 Lv.{MutableState.ReconLevel}: 새 원정 대상 {newlyRevealed}개 발견";
        gameEventBus.RaiseAlert("정찰 강화", message, EventAlertImportance.Medium, "오펜스");
        RaiseChanged();
        return true;
    }

    public bool TrySelectTarget(string targetId, out OffenseTargetSnapshot snapshot, out string message)
    {
        EnsureInitialized();
        OffenseTargetDefinition target = OffenseWorldMapService.FindKnownTarget(MutableState, targets, targetId);
        if (target == null)
        {
            snapshot = null;
            message = "발견되지 않은 원정 대상입니다";
            return false;
        }

        if (!OffenseWorldMapService.CanAttemptTarget(MutableState, target, out message))
        {
            snapshot = target.ToSnapshot(HasPreciseIntel(target.id), MutableState);
            return false;
        }

        MutableState.SetSelectedTarget(target.id);
        snapshot = target.ToSnapshot(HasPreciseIntel(target.id), MutableState);
        message = $"{snapshot.title} 선택";
        TargetSelected?.Invoke(snapshot);
        RaiseChanged();
        return true;
    }

    public bool TryGetKnownTargetSnapshot(string targetId, out OffenseTargetSnapshot snapshot)
    {
        EnsureInitialized();
        OffenseTargetDefinition target = OffenseWorldMapService.FindKnownTarget(MutableState, targets, targetId);
        if (target == null)
        {
            snapshot = null;
            return false;
        }

        snapshot = target.ToSnapshot(HasPreciseIntel(target.id), MutableState);
        return true;
    }

    public bool TryGetTargetDefinition(
        string targetId,
        out OffenseTargetDefinition definition)
    {
        EnsureInitialized();
        definition = targets.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.id,
                targetId,
                StringComparison.Ordinal));
        return definition != null;
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
        if (target == null || !MutableState.KnowTarget(targetId))
        {
            completedTarget = null;
            message = "알 수 없는 오펜스 목표입니다.";
            return false;
        }

        if (!OffenseWorldMapService.CanAttemptTarget(MutableState, target, out message)
            || !MutableState.MarkTargetCompleted(target.id))
        {
            completedTarget = target.ToSnapshot(
                HasPreciseIntel(target.id),
                MutableState);
            return false;
        }

        if (target.revealsTruth)
        {
            MutableState.RevealTruth(target.id);
        }

        completedTarget = target.ToSnapshot(
            HasPreciseIntel(target.id),
            MutableState);
        message = target.revealsTruth
            ? "최종 오펜스를 마치고 던전의 진실을 밝혔습니다."
            : $"오펜스 목표 완료 {MutableState.CompletedTargetCount}/{targets.Count}";
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

    public bool TryRecordStrategicTruthReveal(
        string targetId,
        out string message)
    {
        EnsureInitialized();
        OffenseTargetDefinition target = targets.FirstOrDefault(candidate =>
            candidate != null
            && candidate.revealsTruth
            && string.Equals(
                candidate.id,
                targetId,
                StringComparison.Ordinal));
        if (target == null)
        {
            message = "Strategic 최종 목표와 연결된 진실 목표가 없습니다.";
            return false;
        }

        if (MutableState.TruthRevealed)
        {
            message = "이미 던전의 진실을 밝혔습니다.";
            return true;
        }

        MutableState.AddKnownTarget(target.id);
        MutableState.MarkTargetCompleted(target.id);
        MutableState.RevealTruth(target.id);
        RaiseChanged();
        gameEventBus.RaiseAlert(
            OffenseWorldMapService.TruthTitle,
            target.truthText,
            EventAlertImportance.High,
            "오펜스");
        gameEventBus.Publish(new OffenseTruthRevealedEvent(
            target.id,
            OffenseWorldMapService.TruthTitle,
            target.truthText));
        message = "최종 오펜스를 마치고 던전의 진실을 밝혔습니다.";
        return true;
    }

    public void SetPreciseIntelForDebug(bool value)
    {
        preciseIntel = value;
        RaiseChanged();
    }

    public bool TryUnlockTargetIntel(
        string targetId,
        ExpeditionIntelPaymentMethod payment,
        out DomainFailure failure)
    {
        EnsureInitialized();
        if (OffenseWorldMapService.FindKnownTarget(
            MutableState,
            targets,
            targetId) == null)
        {
            failure = new DomainFailure(
                FailureCode.OffenseTargetUnknown,
                targetId ?? string.Empty);
            return false;
        }

        if (externalInfluence == null)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        bool unlocked = externalInfluence.TryUnlockIntel(
            targetId,
            payment,
            out failure);
        if (unlocked)
        {
            RaiseChanged();
        }

        return unlocked;
    }

    private bool HasPreciseIntel(string targetId)
    {
        return preciseIntel
            || externalInfluence?.IsIntelUnlocked(targetId) == true;
    }

    private void EnsureInitialized()
    {
        if (targets != null && campaign != null) return;
        throw new InvalidOperationException(
            $"{nameof(OffenseWorldMapRuntime)} has not received the authored campaign catalog.");
    }

    private void RaiseChanged()
    {
        if (targets == null)
        {
            return;
        }

        Changed?.Invoke();
    }

}

public partial class OffenseWorldMapPanel : MonoBehaviour
{
    private IOffenseCampaignQuery campaign;
    private IOffenseCampaignCommands commands;
    private TMP_Text headerText;
    private TMP_Text detailText;
    private RectTransform targetButtonRoot;
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();
    private IOffensePanelButtonFactory buttonFactory;

    public void Bind(
        IOffenseCampaignQuery source,
        IOffenseCampaignCommands commands,
        IOffensePanelButtonFactory buttonFactory)
    {
        campaign = source
            ?? throw new ArgumentNullException(nameof(source));
        this.commands = commands
            ?? throw new ArgumentNullException(nameof(commands));
        this.buttonFactory = buttonFactory
            ?? throw new ArgumentNullException(nameof(buttonFactory));
        EnsureView();
        gameObject.SetActive(true);
        Render();
    }

    public void Render()
    {
        if (campaign == null)
        {
            return;
        }

        EnsureView();
        if (CanRenderStrategic())
        {
            RenderStrategic();
            return;
        }
        headerText.text = $"월드맵 / 정찰 Lv.{campaign.State.ReconLevel} / 범위 {campaign.CurrentScanRange:0.#}";
        ClearButtons();

        foreach (OffenseTargetSnapshot target in campaign.VisibleTargets)
        {
            GameObject buttonObject = RequireButtonFactory().CreateButton(
                targetButtonRoot,
                target.title,
                17f,
                () =>
                {
                    if (commands.TrySelectTarget(target.id, out OffenseTargetSnapshot selected, out _))
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
                commands.TryUpgradeRecon(out string message);
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

        if (campaign.VisibleTargets.Count == 0)
        {
            detailText.text = "발견된 원정 대상이 없습니다.";
        }
        else if (!string.IsNullOrWhiteSpace(campaign.State.SelectedTargetId)
            && campaign.TryGetKnownTargetSnapshot(campaign.State.SelectedTargetId, out OffenseTargetSnapshot selected))
        {
            detailText.text = selected.ToDetailText();
        }
        else
        {
            detailText.text = campaign.VisibleTargets[0].ToDetailText();
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
