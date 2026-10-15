using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>Publication and finalization guards; money and physical release remain owned by CaptivityRansomRuntime.</summary>
internal sealed class CaptiveRansomOutcomeRuntime
{
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameEventBus eventBus;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private CaptiveRansomPublication active;
    private CaptiveState finalizing;
    private bool recovering;

    public CaptiveRansomOutcomeRuntime(IDomainFactStagingPublisher publisher,
        IDomainFactRecoveryPublisher recovery, IGameEventBus eventBus, ICharacterAiWorldRegistry worldRegistry)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
    }

    public bool TryPrepare(CaptiveState state, CaptivityStatus previousStatus, int amount,
        float resultingRetaliationPressure, int absoluteDay, out CaptiveRansomPublication prepared,
        out long ownerRevision, out string failureReason)
    {
        prepared = null; ownerRevision = 0L; failureReason = string.Empty;
        if (Busy) { failureReason = "captive-ransom-command-active"; return false; }
        if (state == null || state.status != previousStatus || state.ransomOutcomeRevision != 0L
            || state.ransomObservation == null || state.ransomObservation.HasFact)
        { failureReason = "captive-ransom-source-state-invalid"; return false; }
        IDomainFactStage stage = null;
        try
        {
            CharacterActor decider = worldRegistry.Characters.FirstOrDefault(value => value?.Identity?.IsOwner == true);
            string decisionMakerId = CharacterPersistentIdentity.TryGet(decider, out CharacterId id) ? id.Value : string.Empty;
            ownerRevision = checked(state.ransomOutcomeRevision + 1L);
            var fact = new CaptiveRansomAcceptedFact(new CaptiveRansomAcceptedFactData
            {
                captiveId = state.captiveId, displayName = state.displayName?.Trim(), decisionMakerId = decisionMakerId,
                previousStatus = previousStatus, amount = amount, retaliationPressure = resultingRetaliationPressure,
                revision = ownerRevision, absoluteDay = absoluteDay
            });
            if (!publisher.TryStage(fact, out stage, out failureReason)) return false;
            if (stage == null || !stage.IsActive || string.IsNullOrWhiteSpace(stage.OperationKey)
                || stage.OwnerRevision != ownerRevision)
            { failureReason = "captive-ransom-stage-invalid"; return false; }
            prepared = new CaptiveRansomPublication(this, state, fact, stage);
            active = prepared; stage = null;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failureReason = "captive-ransom-prepare:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { CancelStage(stage); }
    }

    public DomainFactDelivery Commit(CaptiveRansomPublication prepared)
    {
        if (prepared == null || prepared != active) return DomainFactDelivery.Reject("captive-ransom-publication-inactive");
        if (prepared.HandedOff) return DomainFactDelivery.Uncertain("captive-ransom-publication-already-handed-off");
        var value = prepared.Fact.Snapshot();
        var state = prepared.State;
        if (state.captiveId != value.captiveId || state.status != CaptivityStatus.Ransom
            || state.ransomOutcomeRevision != value.revision || state.ransomAcceptedAmount != value.amount
            || state.retaliationPressure != value.retaliationPressure || state.ransomIncomeCredited)
            return DomainFactDelivery.Reject("captive-ransom-applied-state-conflict");
        state.ransomObservation = new CaptiveRansomObservationData
        { fact = value, publicationKey = prepared.Stage.OperationKey, pending = true };
        state.ransomObserversPending = !prepared.IsReplay;
        prepared.HandedOff = true;
        DomainFactDelivery result;
        try { result = prepared.Stage.Commit(); }
        catch (Exception exception) when (Recoverable(exception))
        { result = DomainFactDelivery.Uncertain("captive-ransom-commit:" + exception.GetType().Name); }
        if (!result.MayBeCommitted)
        {
            state.ransomObservation = new CaptiveRansomObservationData();
            state.ransomObserversPending = false;
        }
        else if (Delivered(state.ransomObservation.publicationKey, result)) state.ransomObservation.pending = false;
        return result;
    }

    public void Cancel(CaptiveRansomPublication prepared)
    { if (prepared == active) CancelStage(prepared?.Stage); }
    internal void Release(CaptiveRansomPublication prepared)
    {
        if (prepared != active) return;
        try { CancelStage(prepared.Stage); }
        finally { active = null; }
    }
    public void ValidateBeforeCapture()
    { if (Busy) throw new InvalidOperationException("captive-ransom-command-active"); }
    private bool Busy => active != null || finalizing != null || recovering;

    public bool Recover(CaptiveState state, out string failureReason)
    {
        failureReason = string.Empty;
        if (Busy) { failureReason = "captive-ransom-command-active"; return false; }
        var observation = state?.ransomObservation;
        if (observation?.HasFact != true)
        { failureReason = "captive-ransom-observation-missing"; return false; }
        recovering = true;
        try
        {
            var fact = new CaptiveRansomAcceptedFact(observation.fact);
            var value = fact.Snapshot();
            if (value.captiveId != state.captiveId || value.revision != state.ransomOutcomeRevision
                || value.amount != state.ransomAcceptedAmount)
            { failureReason = "captive-ransom-observation-owner-conflict"; return false; }
            if (!observation.pending) return true;
            var result = new DomainFactDelivery[1];
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(observation.publicationKey, fact) }, result, out failureReason)
                || !Delivered(observation.publicationKey, result[0]))
            {
                failureReason = "captive-ransom-recovery-pending:" + failureReason + ":" + result[0].Detail;
                return false;
            }
            observation.pending = false;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failureReason = "captive-ransom-recovery:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { recovering = false; }
    }

    public bool TryBeginFinalization(CaptiveState state, out IDisposable lease, out string failureReason)
    {
        lease = null;
        if (!Recover(state, out failureReason)) return false;
        if (state.status != CaptivityStatus.Ransom)
        { failureReason = "captive-ransom-finalization-state-invalid"; return false; }
        finalizing = state;
        lease = new FinalizationLease(this, state);
        return true;
    }

    public void NotifyCommitted(CaptiveState state)
    {
        if (state != finalizing || state?.status != CaptivityStatus.Released || !state.ransomIncomeCredited
            || state.ransomObservation?.HasFact != true || state.ransomObservation.pending || !state.ransomObserversPending) return;
        var value = new CaptiveRansomAcceptedFact(state.ransomObservation.fact).Snapshot();
        state.ransomObserversPending = false;
        if (value.decisionMakerId.Length > 0)
        {
            try
            {
                eventBus.Publish(new PrisonerDecisionEvent(new CharacterId(value.decisionMakerId), new CharacterId(value.captiveId),
                    "ransom", CharacterCommandOrigin.DirectPlayerOrder, value.absoluteDay));
            }
            catch (Exception exception) when (Recoverable(exception))
            { Debug.LogWarning("확정된 몸값의 포로 결정 관찰자가 실패했습니다: " + exception); }
        }
        try { eventBus.Publish(new CaptiveRansomedEvent(value.captiveId, value.amount, value.retaliationPressure)); }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogWarning("확정된 몸값의 보복 관찰자가 실패했습니다: " + exception); }
    }

    private static void CancelStage(IDomainFactStage stage)
    {
        try { stage?.Cancel(); }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogWarning("captive-ransom-cancel:" + exception); }
    }
    private static bool Delivered(string key, DomainFactDelivery value)
    {
        if (!value.Completed || value.OperationKey != key || string.IsNullOrWhiteSpace(value.PublicationId)
            || value.Fingerprint.Length != 64) return false;
        foreach (char character in value.Fingerprint)
            if (!(character >= '0' && character <= '9' || character >= 'a' && character <= 'f'
                || character >= 'A' && character <= 'F')) return false;
        return true;
    }
    private static bool Recoverable(Exception value) => value is not OutOfMemoryException
        && value is not StackOverflowException && value is not AccessViolationException;
    private sealed class FinalizationLease : IDisposable
    {
        private CaptiveRansomOutcomeRuntime owner;
        private readonly CaptiveState state;
        public FinalizationLease(CaptiveRansomOutcomeRuntime owner, CaptiveState state) { this.owner = owner; this.state = state; }
        public void Dispose()
        {
            if (owner?.finalizing == state) owner.finalizing = null;
            owner = null;
        }
    }
}

internal sealed class CaptiveRansomPublication : IDisposable
{
    private readonly CaptiveRansomOutcomeRuntime owner;
    internal CaptiveRansomPublication(CaptiveRansomOutcomeRuntime owner, CaptiveState state,
        CaptiveRansomAcceptedFact fact, IDomainFactStage stage)
    { this.owner = owner; State = state; Fact = fact; Stage = stage; }
    internal CaptiveState State { get; }
    internal CaptiveRansomAcceptedFact Fact { get; }
    internal IDomainFactStage Stage { get; }
    internal bool HandedOff { get; set; }
    public bool IsReplay => Stage.IsReplay;
    public void Dispose() => owner.Release(this);
}
