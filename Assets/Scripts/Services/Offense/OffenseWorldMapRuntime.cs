using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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
    private IOffenseTruthRevealOutcomeCommitter truthOutcomeCommitter;
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
        IGameEventBus gameEventBus,
        IExternalInfluenceRuntime externalInfluence,
        IOffenseCampaignStateAuthority campaign,
        IOffenseCampaignRuntime campaignPersistence,
        IOffenseCampaignCatalog targetCatalog,
        IOffenseTruthRevealOutcomeCommitter truthOutcomeCommitter,
        IGameCalendar gameCalendar)
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
        this.truthOutcomeCommitter = truthOutcomeCommitter
            ?? throw new ArgumentNullException(nameof(truthOutcomeCommitter));
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
        externalInfluence?.AddScoutingLabor(60f);
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
        OffenseTargetDefinition target = targets.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.id, targetId, StringComparison.Ordinal));
        if (target == null || !MutableState.KnowTarget(targetId))
        {
            completedTarget = null;
            message = "?????녿뒗 ?ㅽ렂??紐⑺몴?낅땲??";
            return false;
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
        OffenseTargetDefinition target,
        bool addKnownTarget,
        out OffenseTargetSnapshot completedTarget,
        out string message)
    {
        completedTarget = null;
        long ownerRevision = MutableState.CompletedTargetCount
            + (MutableState.IsTargetCompleted(target.id) ? 0L : 1L);
        OffenseTruthRevealOutcomeReceipt receipt;
        try
        {
            receipt = new OffenseTruthRevealOutcomeReceipt(
                target.id,
                ownerRevision,
                target.title,
                OffenseWorldMapService.TruthTitle,
                target.truthText,
                Math.Max(0, gameCalendar.Day));
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            message = "진실 공개 기록이 유효하지 않습니다: " + exception.Message;
            return false;
        }

        if (!truthOutcomeCommitter.TryPrepare(
                receipt,
                out PreparedExternalFactionOutcome prepared,
                out string prepareFailure))
        {
            message = "진실 공개 기록 용량을 확보하지 못했습니다: "
                + prepareFailure;
            return false;
        }

        OffenseCampaignRestoreCandidate rollback;
        OffenseCampaignRestoreCandidate candidate;
        try
        {
            DungeonOffenseCampaignSaveData source =
                campaignPersistence.Capture();
            rollback = campaignPersistence.BuildRestoreCandidate(source);
            candidate = campaignPersistence.BuildRestoreCandidate(source);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            truthOutcomeCommitter.Cancel(prepared);
            message = "진실 공개 상태 후보를 만들지 못했습니다: "
                + exception.Message;
            return false;
        }
        if (addKnownTarget)
            candidate.State.AddKnownTarget(target.id);
        if (!candidate.State.IsTargetCompleted(target.id)
            && !candidate.State.MarkTargetCompleted(target.id))
        {
            truthOutcomeCommitter.Cancel(prepared);
            message = "진실 공개 목표를 완료 상태로 전환하지 못했습니다.";
            return false;
        }
        candidate.State.RevealTruth(target.id);
        if (!candidate.State.TruthRevealed)
        {
            truthOutcomeCommitter.Cancel(prepared);
            message = "진실 공개 상태를 확정하지 못했습니다.";
            return false;
        }

        campaignPersistence.PublishRestoreCandidate(candidate);
        if (!truthOutcomeCommitter.TryCommit(
                prepared,
                ownerRevision,
                out string commitFailure))
        {
            campaignPersistence.PublishRestoreCandidate(rollback);
            message = "진실 공개 기록을 원자적으로 확정하지 못했습니다: "
                + commitFailure;
            return false;
        }

        completedTarget = target.ToSnapshot(
            HasPreciseIntel(target.id),
            MutableState);
        RaiseChanged();
        gameEventBus.RaiseAlert(
            OffenseWorldMapService.TruthTitle,
            target.truthText,
            EventAlertImportance.High,
            "원정");
        gameEventBus.Publish(new OffenseTruthRevealedEvent(
            target.id,
            OffenseWorldMapService.TruthTitle,
            target.truthText));
        message = "최종 작전을 마치고 지상의 진실을 공개했습니다!";
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

