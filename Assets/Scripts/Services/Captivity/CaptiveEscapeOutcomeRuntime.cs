using System;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>Domain publication lifetime; state effects remain owned by completion/defection commands.</summary>
internal sealed class CaptiveEscapeOutcomeRuntime
{
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameEventBus eventBus;
    private CaptiveEscapePublication active;
    private bool recovering;

    public CaptiveEscapeOutcomeRuntime(IDomainFactStagingPublisher publisher,
        IDomainFactRecoveryPublisher recovery, IGameEventBus eventBus)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public bool TryPrepare(CaptiveState state, CaptiveEscapeKind kind, string trigger,
        CaptivityStatus previousStatus, float resultingRetaliationPressure, int absoluteDay,
        out CaptiveEscapePublication prepared, out long ownerRevision,
        out string normalizedTrigger, out string failureReason)
    {
        prepared = null; ownerRevision = 0L;
        normalizedTrigger = trigger?.Trim() ?? string.Empty;
        failureReason = string.Empty;
        if (active != null || recovering)
        { failureReason = "captive-escape-command-active"; return false; }
        if (state == null || state.status != previousStatus || state.escapeOutcomeRevision != 0L
            || state.escapeObservation == null || state.escapeObservation.HasFact)
        { failureReason = "captive-escape-source-state-invalid"; return false; }
        IDomainFactStage stage = null;
        try
        {
            ownerRevision = checked(state.escapeOutcomeRevision + 1L);
            var fact = new CaptiveEscapedFact(new CaptiveEscapedFactData
            {
                captiveId = state.captiveId, displayName = state.displayName?.Trim(), kind = kind,
                trigger = normalizedTrigger, previousStatus = previousStatus,
                retaliationPressure = resultingRetaliationPressure, revision = ownerRevision, absoluteDay = absoluteDay
            });
            if (!publisher.TryStage(fact, out stage, out failureReason)) return false;
            if (stage == null || !stage.IsActive || string.IsNullOrWhiteSpace(stage.OperationKey)
                || stage.OwnerRevision != ownerRevision)
            { failureReason = "captive-escape-stage-invalid"; return false; }
            prepared = new CaptiveEscapePublication(this, state, fact, stage);
            active = prepared;
            stage = null;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failureReason = "captive-escape-prepare:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally
        {
            try { stage?.Cancel(); }
            catch (Exception exception) when (Recoverable(exception))
            { Debug.LogWarning("captive-escape-prepare-cancel:" + exception); }
        }
    }

    public DomainFactDelivery Commit(CaptiveEscapePublication prepared)
    {
        if (prepared == null || prepared != active) return DomainFactDelivery.Reject("captive-escape-publication-inactive");
        if (prepared.HandedOff) return DomainFactDelivery.Uncertain("captive-escape-publication-already-handed-off");
        var value = prepared.Fact.Snapshot();
        var state = prepared.State;
        if (state.captiveId != value.captiveId || state.status != CaptivityStatus.Escaped
            || state.escapeOutcomeRevision != value.revision || state.retaliationPressure != value.retaliationPressure)
            return DomainFactDelivery.Reject("captive-escape-applied-state-conflict");
        state.escapeObservation = new CaptiveEscapeObservationData
        {
            fact = value, publicationKey = prepared.Stage.OperationKey,
            pending = true, observersPending = !prepared.IsReplay
        };
        prepared.HandedOff = true;
        DomainFactDelivery result;
        try { result = prepared.Stage.Commit(); }
        catch (Exception exception) when (Recoverable(exception))
        { result = DomainFactDelivery.Uncertain("captive-escape-commit:" + exception.GetType().Name); }
        if (!result.MayBeCommitted) state.escapeObservation = new CaptiveEscapeObservationData();
        else if (Delivered(state.escapeObservation.publicationKey, result)) state.escapeObservation.pending = false;
        return result;
    }

    public void Cancel(CaptiveEscapePublication prepared)
    {
        if (prepared != active) return;
        try { prepared?.Stage.Cancel(); }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogWarning("captive-escape-cancel:" + exception); }
    }
    internal void Release(CaptiveEscapePublication prepared)
    {
        if (prepared != active) return;
        try { prepared.Stage.Cancel(); }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogWarning("captive-escape-cancel:" + exception); }
        finally { active = null; }
    }
    public void ValidateBeforeCapture()
    {
        if (active != null || recovering) throw new InvalidOperationException("captive-escape-command-active");
    }

    public void NotifyCommitted(CaptiveEscapePublication prepared)
    {
        if (prepared == null || prepared != active || !prepared.HandedOff) return;
        Notify(prepared.State.escapeObservation);
    }

    public bool Recover(CaptiveState state, out string failureReason)
    {
        failureReason = string.Empty;
        if (active != null || recovering)
        { failureReason = "captive-escape-command-active"; return false; }
        var observation = state?.escapeObservation;
        if (observation == null) { failureReason = "captive-escape-observation-missing"; return false; }
        if (!observation.HasFact) return true;
        recovering = true;
        try
        {
            if (observation.fact.captiveId != state.captiveId || observation.fact.revision != state.escapeOutcomeRevision)
            { failureReason = "captive-escape-observation-owner-conflict"; return false; }
            if (observation.pending)
            {
                var fact = new CaptiveEscapedFact(observation.fact);
                var result = new DomainFactDelivery[1];
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(observation.publicationKey, fact) }, result, out failureReason)
                    || !Delivered(observation.publicationKey, result[0]))
                {
                    failureReason = "captive-escape-recovery-pending:" + failureReason + ":" + result[0].Detail;
                    return false;
                }
                observation.pending = false;
            }
            Notify(observation);
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failureReason = "captive-escape-recovery:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { recovering = false; }
    }

    private void Notify(CaptiveEscapeObservationData observation)
    {
        if (observation?.HasFact != true || observation.pending || !observation.observersPending) return;
        var value = new CaptiveEscapedFact(observation.fact).Snapshot();
        observation.observersPending = false;
        try { eventBus.Publish(new CaptiveEscapedEvent(value.captiveId, value.trigger, CaptiveEscapedFact.IsBetrayal(value.kind))); }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogWarning("포로 탈출 이벤트 관찰자가 실패했습니다: " + exception); }
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
}

internal sealed class CaptiveEscapePublication : IDisposable
{
    private readonly CaptiveEscapeOutcomeRuntime owner;
    internal CaptiveEscapePublication(CaptiveEscapeOutcomeRuntime owner, CaptiveState state,
        CaptiveEscapedFact fact, IDomainFactStage stage)
    { this.owner = owner; State = state; Fact = fact; Stage = stage; }
    internal CaptiveState State { get; }
    internal CaptiveEscapedFact Fact { get; }
    internal IDomainFactStage Stage { get; }
    internal bool HandedOff { get; set; }
    public bool IsReplay => Stage.IsReplay;
    public void Dispose() => owner.Release(this);
}
