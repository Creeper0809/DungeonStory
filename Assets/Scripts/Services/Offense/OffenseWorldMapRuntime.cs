using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
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
    private IDomainFactStagingPublisher facts;
    private IDomainFactRecoveryPublisher recovery;
    private IUiClock uiClock;
    private float nextTruthRetry;
    private IGameCalendar gameCalendar;

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
        IUiClock uiClock,
        IGameEventBus gameEventBus,
        IExternalInfluenceRuntime externalInfluence,
        IOffenseCampaignStateAuthority campaign,
        IOffenseCampaignRuntime campaignPersistence,
        IOffenseCampaignCatalog targetCatalog,
        IDomainFactStagingPublisher facts,
        IDomainFactRecoveryPublisher recovery,
        IGameCalendar gameCalendar)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
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
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.gameCalendar = gameCalendar
            ?? throw new ArgumentNullException(nameof(gameCalendar));
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
            message = "정찰 범위가 이미 최대입니다.";
            return false;
        }

        int newlyRevealed = OffenseWorldMapService.RevealTargetsInRange(MutableState, targets);
        externalInfluence?.AddScoutingLabor(
            60f,
            MutableState.ReconLevel,
            newlyRevealed);
        message = $"정찰 Lv.{MutableState.ReconLevel}: 새 원정 대상 {newlyRevealed}개 발견";
        gameEventBus.RaiseAlert(
            "정찰 강화",
            message,
            EventAlertImportance.Medium,
            "원정");
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
            message = "諛쒓껄?섏? ?딆? ?먯젙 ??곸엯?덈떎";
            return false;
        }

        if (!OffenseWorldMapService.CanAttemptTarget(MutableState, target, out message))
        {
            snapshot = target.ToSnapshot(HasPreciseIntel(target.id), MutableState);
            return false;
        }

        MutableState.SetSelectedTarget(target.id);
        snapshot = target.ToSnapshot(HasPreciseIntel(target.id), MutableState);
        message = $"{snapshot.title} ?좏깮";
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
        if (campaign.TruthObservation.pending && !TryRecoverPendingTruth(out message))
        { completedTarget = null; return false; }
        OffenseTargetDefinition target = targets.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.id, targetId, StringComparison.Ordinal));
        if (target == null || !MutableState.KnowTarget(targetId))
        {
            completedTarget = null;
            message = "?????녿뒗 ?ㅽ렂??紐⑺몴?낅땲??";
            return false;
        }

        if (target.revealsTruth && MutableState.RevealedTruthTargetId == target.id)
        {
            completedTarget = target.ToSnapshot(HasPreciseIntel(target.id), MutableState);
            message = "이미 지상의 진실을 밝혔습니다.";
            return true;
        }

        if (!OffenseWorldMapService.CanAttemptTarget(MutableState, target, out message))
        {
            completedTarget = target.ToSnapshot(
                HasPreciseIntel(target.id),
                MutableState);
            return false;
        }

        if (target.revealsTruth)
        {
            return TryCommitTruthReveal(
                target,
                addKnownTarget: false,
                out completedTarget,
                out message);
        }

        if (!MutableState.MarkTargetCompleted(target.id))
        {
            completedTarget = target.ToSnapshot(
                HasPreciseIntel(target.id),
                MutableState);
            return false;
        }

        completedTarget = target.ToSnapshot(
            HasPreciseIntel(target.id),
            MutableState);
        message = $"?ㅽ렂??紐⑺몴 ?꾨즺 {MutableState.CompletedTargetCount}/{targets.Count}";
        RaiseChanged();
        gameEventBus.RaiseAlert(
            "원정 진척",
            message,
            EventAlertImportance.Medium,
            "원정");

        return true;
    }

    public bool TryRecordStrategicTruthReveal(
        string targetId,
        out string message)
    {
        EnsureInitialized();
        if (campaign.TruthObservation.pending && !TryRecoverPendingTruth(out message)) return false;
        OffenseTargetDefinition target = targets.FirstOrDefault(candidate =>
            candidate != null
            && candidate.revealsTruth
            && string.Equals(
                candidate.id,
                targetId,
                StringComparison.Ordinal));
        if (target == null)
        {
            message = "전략 최종 목표와 연결된 진실 목표가 없습니다.";
            return false;
        }

        if (MutableState.TruthRevealed)
        {
            if (MutableState.RevealedTruthTargetId != target.id)
            { message = "이미 공개한 진실의 목표와 일치하지 않습니다."; return false; }
            message = "이미 지상의 진실을 밝혔습니다.";
            return true;
        }

        return TryCommitTruthReveal(
            target,
            addKnownTarget: true,
            out _,
            out message);
    }

    private bool TryCommitTruthReveal(
        OffenseTargetDefinition target, bool addKnownTarget,
        out OffenseTargetSnapshot completedTarget, out string message)
    {
        completedTarget = null;
        OffenseTruthRevealedFact fact;
        try
        {
            fact = new OffenseTruthRevealedFact(new OffenseTruthFactData
            {
                targetId = target.id, targetDisplayName = target.title?.Trim(),
                truthTitle = OffenseWorldMapService.TruthTitle,
                truthText = target.truthText?.Trim(), absoluteDay = gameCalendar.Day,
                ownerRevision = MutableState.CompletedTargetCount + (MutableState.IsTargetCompleted(target.id) ? 0L : 1L)
            });
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { message = "진실 공개 사실이 유효하지 않습니다: " + exception.Message; return false; }

        if (!campaign.TryRevealTruth(fact, addKnownTarget, facts, out string failure))
        { message = "진실 공개를 확정하지 못했습니다: " + failure; return false; }
        completedTarget = target.ToSnapshot(HasPreciseIntel(target.id), MutableState);
        NotifyDeliveredTruth();
        message = campaign.TruthObservation.pending
            ? "진실 공개 적용됨 · 결과 전달 대기: " + failure
            : "최종 작전을 마치고 지상의 진실을 공개했습니다!";
        return true;
    }

    public bool TryRecoverPendingTruth(out string failure)
    {
        if (!campaign.TryRecoverTruth(recovery, out failure)) return false;
        NotifyDeliveredTruth();
        return true;
    }

    private void NotifyDeliveredTruth()
    {
        var observation = campaign.TruthObservation;
        if (observation.pending || !observation.notificationPending) return;
        var fact = campaign.TakeTruthNotification();
        if (fact == null) return;
        try { RaiseChanged(); }
        catch (Exception exception) when (Recoverable(exception)) { Debug.LogException(exception); }
        try { gameEventBus.Publish(fact); }
        catch (Exception exception) when (Recoverable(exception)) { Debug.LogException(exception); }
    }

    private void Update()
    {
        if (campaign == null || !campaign.HasPendingTruthWork || uiClock.Time < nextTruthRetry) return;
        nextTruthRetry = uiClock.Time + 1f;
        if (!TryRecoverPendingTruth(out string failure)) Debug.LogWarning(failure);
    }
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;

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

