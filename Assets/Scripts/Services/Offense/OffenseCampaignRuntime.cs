using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class DungeonOffenseCampaignSaveData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;
    public int reconLevel;
    public string selectedTargetId = string.Empty;
    public List<string> knownTargetIds = new List<string>();
    public List<string> completedTargetIds = new List<string>();
    public string revealedTruthTargetId = string.Empty;
    public OffenseTruthObservationData truthObservation = new();
}

public interface IOffenseCampaignRuntime
{
    IOffenseWorldMapStateView State { get; }
    DungeonOffenseCampaignSaveData Capture();
    OffenseCampaignRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseCampaignSaveData source);
    void PublishRestoreCandidate(OffenseCampaignRestoreCandidate candidate);
}

/// <summary>
/// Mutable campaign-state boundary used only by the world-map command adapter.
/// The implementation is a scoped plain runtime object, never the scene
/// MonoBehaviour, so save ownership cannot silently return to the view adapter.
/// </summary>
public interface IOffenseCampaignStateAuthority
{
    IOffenseWorldMapStateView State { get; }
    OffenseWorldMapState MutableState { get; }
    void ConfigureTargets(IEnumerable<OffenseTargetDefinition> definitions);
    void Reset(int reconLevel);
    OffenseTruthObservationData TruthObservation { get; }
    bool HasPendingTruthWork { get; }
    bool TryRevealTruth(OffenseTruthRevealedFact fact, bool addKnownTarget,
        IDomainFactStagingPublisher publisher, out string failure);
    bool TryRecoverTruth(IDomainFactRecoveryPublisher recovery, out string failure);
    OffenseTruthRevealedFact TakeTruthNotification();
}

public interface IOffenseTruthPublicationTransaction
{
    void BeginTruthDeferral();
    void CompleteTruthDeferral();
    void CancelTruthDeferral();
}

public interface IOffenseCampaignQuery
{
    IOffenseWorldMapStateView State { get; }
    IReadOnlyList<OffenseTargetDefinition> TargetDefinitions { get; }
    IReadOnlyList<OffenseTargetSnapshot> VisibleTargets { get; }
    float CurrentScanRange { get; }
    int CampaignTargetCount { get; }
    bool TryGetKnownTargetSnapshot(
        string targetId,
        out OffenseTargetSnapshot snapshot);
    bool TryGetTargetDefinition(
        string targetId,
        out OffenseTargetDefinition definition);
}

public interface IOffenseCampaignCommands
{
    bool TryUpgradeRecon(out string message);
    bool TrySelectTarget(
        string targetId,
        out OffenseTargetSnapshot snapshot,
        out string message);
    bool TryRecordSuccessfulExpedition(
        string targetId,
        out OffenseTargetSnapshot completedTarget,
        out string message);
    bool TryRecordStrategicTruthReveal(
        string targetId,
        out string message);
}

public sealed class OffenseCampaignRestoreCandidate
{
    internal OffenseCampaignRestoreCandidate(OffenseWorldMapState state, OffenseTruthObservationData observation = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        Observation = observation?.Clone() ?? new OffenseTruthObservationData();
    }

    internal OffenseWorldMapState State { get; }
    internal OffenseTruthObservationData Observation { get; }
}

/// <summary>
/// Canonical runtime authority for campaign reconnaissance, discovery, selection,
/// completion, and truth-reveal state. The world-map MonoBehaviour is only a
/// presentation/side-effect adapter over this aggregate module.
/// </summary>
public sealed class OffenseCampaignRuntime :
    IOffenseCampaignRuntime,
    IOffenseCampaignStateAuthority,
    IOffenseTruthPublicationTransaction,
    IDungeonRestoreTransactionParticipant
{
    private OffenseWorldMapState state = new OffenseWorldMapState();
    private HashSet<string> targetIds = new HashSet<string>(StringComparer.Ordinal);
    private HashSet<string> truthTargetIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly DungeonRestorePublication restorePublication = new();
    private OffenseTruthObservationData truthObservation = new();
    private bool truthCommandActive, truthDeferred;
    private IDomainFactStage deferredStage;
    private OffenseWorldMapState stateBeforeDeferral;
    private OffenseTruthObservationData observationBeforeDeferral;

    public IOffenseWorldMapStateView State => state;
    public OffenseWorldMapState MutableState { get { RequireNoTruthCommand(); return state; } }
    public OffenseTruthObservationData TruthObservation => truthObservation.Clone();
    public bool HasPendingTruthWork => truthObservation.pending || truthObservation.notificationPending;

    public void ConfigureTargets(IEnumerable<OffenseTargetDefinition> definitions)
    {
        RequireIdle();
        List<OffenseTargetDefinition> targets = definitions?
            .Where(value => value != null && value.IsValid)
            .ToList()
            ?? throw new ArgumentNullException(nameof(definitions));
        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                "Offense campaign requires at least one valid target definition.");
        }

        targetIds = targets.Select(value => value.id)
            .ToHashSet(StringComparer.Ordinal);
        if (targetIds.Count != targets.Count)
        {
            throw new InvalidOperationException(
                "Offense campaign target definitions contain duplicate IDs.");
        }

        truthTargetIds = targets.Where(value => value.revealsTruth)
            .Select(value => value.id)
            .ToHashSet(StringComparer.Ordinal);
    }

    public void Reset(int reconLevel)
    {
        RequireIdle();
        state.Reset(reconLevel);
        truthObservation = new();
    }

    public DungeonOffenseCampaignSaveData Capture()
    {
        RequireIdle();
        RequireConfigured();
        return CaptureState();
    }

    private DungeonOffenseCampaignSaveData CaptureState()
    {
        return new DungeonOffenseCampaignSaveData
        {
            version = DungeonOffenseCampaignSaveData.CurrentVersion,
            reconLevel = state.ReconLevel,
            selectedTargetId = state.SelectedTargetId,
            knownTargetIds = state.KnownTargetIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            completedTargetIds = state.CompletedTargetIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            revealedTruthTargetId = state.RevealedTruthTargetId,
            truthObservation = truthObservation.Clone()
        };
    }

    public OffenseCampaignRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseCampaignSaveData source)
    {
        RequireIdle();
        return BuildCandidate(source);
    }

    private OffenseCampaignRestoreCandidate BuildCandidate(DungeonOffenseCampaignSaveData source)
    {
        RequireConfigured();
        if (source == null
            || source.version != DungeonOffenseCampaignSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported offense campaign payload version {source?.version.ToString() ?? "null"}; expected {DungeonOffenseCampaignSaveData.CurrentVersion}.");
        }
        if (source.reconLevel < 0
            || source.knownTargetIds == null
            || source.completedTargetIds == null
            || source.knownTargetIds.Any(value => !targetIds.Contains(value))
            || source.completedTargetIds.Any(value => !targetIds.Contains(value))
            || (!string.IsNullOrWhiteSpace(source.revealedTruthTargetId)
                && !truthTargetIds.Contains(source.revealedTruthTargetId)))
        {
            throw new InvalidOperationException(
                "Offense campaign restore references invalid or non-canonical target state.");
        }

        OffenseWorldMapState candidate = new OffenseWorldMapState();
        candidate.Restore(
            source.reconLevel,
            source.selectedTargetId,
            source.knownTargetIds,
            source.completedTargetIds,
            source.revealedTruthTargetId);
        if (!string.Equals(candidate.SelectedTargetId,
                source.selectedTargetId,
                StringComparison.Ordinal)
            || !string.Equals(candidate.RevealedTruthTargetId,
                source.revealedTruthTargetId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Offense campaign selection or revealed truth state is non-canonical.");
        }

        if (source.truthObservation == null) throw new ArgumentException("offense-truth-observation-missing");
        source.truthObservation.Validate(candidate.RevealedTruthTargetId, candidate.CompletedTargetCount);
        return new OffenseCampaignRestoreCandidate(candidate, source.truthObservation);
    }

    public void PublishRestoreCandidate(OffenseCampaignRestoreCandidate candidate)
    {
        RequireIdle();
        candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        if (restorePublication.TryStage(
                () => ApplyRestore(candidate),
                CaptureRestoreRollback))
        {
            return;
        }

        ApplyRestore(candidate);
    }

    private void ApplyRestore(OffenseCampaignRestoreCandidate candidate)
    { state = candidate.State; truthObservation = candidate.Observation.Clone(); }

    private Action CaptureRestoreRollback()
    {
        OffenseWorldMapState previousState = state;
        var previousObservation = truthObservation;
        return () => { state = previousState; truthObservation = previousObservation; };
    }

    public string ParticipantId => "offense.campaign";
    public void BeginRestoreCandidate() { RequireIdle(); restorePublication.Begin(); }
    public void PublishRestoreCandidate() => restorePublication.Publish();
    public void RollbackPublishedRestoreCandidate() => restorePublication.Discard();
    public void DiscardRestoreCandidate() => restorePublication.Discard();
    public void CompleteRestoreCandidate() => restorePublication.Complete();

    public bool TryRevealTruth(OffenseTruthRevealedFact fact, bool addKnownTarget,
        IDomainFactStagingPublisher publisher, out string failure)
    {
        failure = string.Empty;
        if (truthCommandActive) { failure = "offense-truth-command-active"; return false; }
        if (publisher == null || fact == null) throw new ArgumentNullException(publisher == null ? nameof(publisher) : nameof(fact));
        RequireConfigured();
        var data = fact.Snapshot();
        if (state.TruthRevealed)
        {
            bool same = state.RevealedTruthTargetId == data.targetId;
            failure = same ? (truthObservation.pending ? "offense-truth-publication-pending" : string.Empty) : "offense-truth-target-conflict";
            return same && !truthObservation.pending;
        }
        if (!truthTargetIds.Contains(data.targetId) || !addKnownTarget && !state.KnowTarget(data.targetId)
            || data.ownerRevision != state.CompletedTargetCount + (state.IsTargetCompleted(data.targetId) ? 0L : 1L))
        { failure = "offense-truth-authority-conflict"; return false; }

        truthCommandActive = true;
        IDomainFactStage stage = null;
        OffenseWorldMapState before = state;
        var observationBefore = truthObservation;
        bool appliedMustRemain = false;
        try
        {
            if (!publisher.TryStage(fact, out stage, out failure)) return false;
            if (stage == null || !stage.IsActive || stage.IsReplay || stage.OwnerRevision != data.ownerRevision
                || string.IsNullOrWhiteSpace(stage.OperationKey))
            { failure = "offense-truth-unapplied-publication-conflict"; return false; }

            var candidate = BuildCandidate(CaptureState());
            if (addKnownTarget) candidate.State.AddKnownTarget(data.targetId);
            if (!candidate.State.IsTargetCompleted(data.targetId) && !candidate.State.MarkTargetCompleted(data.targetId))
                throw new InvalidOperationException("offense-truth-target-completion-failed");
            candidate.State.RevealTruth(data.targetId);
            var applied = new OffenseTruthObservationData
            { hasFact = true, fact = data, publicationKey = stage.OperationKey, pending = true, notificationPending = true };
            applied.Validate(candidate.State.RevealedTruthTargetId, candidate.State.CompletedTargetCount);
            state = candidate.State;
            truthObservation = applied;
            if (truthDeferred)
            {
                deferredStage = stage; stage = null;
                appliedMustRemain = true;
                return true;
            }
            var delivery = CommitTruth(stage);
            appliedMustRemain = delivery.MayBeCommitted;
            if (!appliedMustRemain)
            {
                state = before; truthObservation = observationBefore;
                failure = "offense-truth-publication-rejected:" + delivery.Detail;
                return false;
            }
            failure = delivery.Completed ? string.Empty : "offense-truth-publication-pending:" + delivery.Detail;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            if (!appliedMustRemain) { state = before; truthObservation = observationBefore; }
            failure = "offense-truth-command:" + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        finally { CancelStage(stage); truthCommandActive = false; }
    }

    public bool TryRecoverTruth(IDomainFactRecoveryPublisher recovery, out string failure)
    {
        failure = string.Empty;
        if (truthCommandActive || truthDeferred) { failure = "offense-truth-command-active"; return false; }
        if (!truthObservation.pending) return true;
        truthCommandActive = true;
        try
        {
            var results = new DomainFactDelivery[1];
            var replay = DomainFactReplay.Create(truthObservation.publicationKey, new OffenseTruthRevealedFact(truthObservation.fact));
            if (!recovery.ReplayBatch(new[] { replay }, results, out failure)
                || !Delivered(truthObservation.publicationKey, results[0]))
            { failure = "offense-truth-recovery-pending:" + failure + ":" + results[0].Detail; return false; }
            truthObservation.pending = false;
            LastTruthPublicationFailure = string.Empty;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "offense-truth-recovery:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { truthCommandActive = false; }
    }

    public OffenseTruthRevealedFact TakeTruthNotification()
    {
        RequireIdle();
        if (truthObservation.pending || !truthObservation.notificationPending) return null;
        var fact = new OffenseTruthRevealedFact(truthObservation.fact);
        truthObservation.notificationPending = false;
        return fact;
    }

    public void BeginTruthDeferral()
    {
        RequireIdle();
        if (truthObservation.pending) throw new InvalidOperationException("offense-truth-publication-pending");
        stateBeforeDeferral = state;
        observationBeforeDeferral = truthObservation;
        truthDeferred = true;
    }

    public void CompleteTruthDeferral()
    {
        RequireNoTruthCommand();
        if (!truthDeferred) throw new InvalidOperationException("offense-truth-deferral-missing");
        truthCommandActive = true;
        try
        {
            // The enclosing expedition is already durable. Any failed acknowledgement is
            // saved-forward here, including a definite rejection of this child publication.
            if (deferredStage != null) _ = CommitTruth(deferredStage);
        }
        finally
        {
            CancelStage(deferredStage); deferredStage = null;
            stateBeforeDeferral = null; observationBeforeDeferral = null;
            truthDeferred = false; truthCommandActive = false;
        }
    }

    public void CancelTruthDeferral()
    {
        RequireNoTruthCommand();
        if (!truthDeferred) throw new InvalidOperationException("offense-truth-deferral-missing");
        truthCommandActive = true;
        try
        {
            state = stateBeforeDeferral;
            truthObservation = observationBeforeDeferral;
        }
        finally
        {
            CancelStage(deferredStage); deferredStage = null;
            stateBeforeDeferral = null; observationBeforeDeferral = null;
            truthDeferred = false; truthCommandActive = false;
        }
    }

    public string LastTruthPublicationFailure { get; private set; } = string.Empty;
    private DomainFactDelivery CommitTruth(IDomainFactStage stage)
    {
        DomainFactDelivery result;
        try { result = stage.Commit(); }
        catch (Exception exception) when (Recoverable(exception))
        { result = DomainFactDelivery.Uncertain("offense-truth-commit:" + exception.GetType().Name); }
        if (Delivered(truthObservation.publicationKey, result)) truthObservation.pending = false;
        else if (result.Completed) result = DomainFactDelivery.Uncertain("offense-truth-delivery-proof-invalid");
        LastTruthPublicationFailure = result.Completed ? string.Empty : result.Detail;
        return result;
    }
    private void CancelStage(IDomainFactStage stage)
    {
        try { stage?.Cancel(); }
        catch (Exception exception) when (Recoverable(exception))
        { LastTruthPublicationFailure = "offense-truth-stage-cancel:" + exception.GetType().Name; }
    }
    private static bool Delivered(string key, DomainFactDelivery result)
    {
        if (!result.Completed || result.OperationKey != key || string.IsNullOrWhiteSpace(result.PublicationId)
            || result.Fingerprint?.Length != 64) return false;
        return result.Fingerprint.All(value => value >= '0' && value <= '9' || value >= 'a' && value <= 'f' || value >= 'A' && value <= 'F');
    }
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        && exception is not StackOverflowException && exception is not AccessViolationException;
    private void RequireNoTruthCommand()
    { if (truthCommandActive) throw new InvalidOperationException("offense-truth-command-active"); }
    private void RequireIdle()
    { if (truthCommandActive || truthDeferred) throw new InvalidOperationException("offense-truth-command-active"); }

    private void RequireConfigured()
    {
        if (targetIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Offense campaign target definitions have not been configured.");
        }
    }
}
