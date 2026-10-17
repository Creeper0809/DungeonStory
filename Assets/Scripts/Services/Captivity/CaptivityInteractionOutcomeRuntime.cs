using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

internal sealed class CaptivityInteractionOutcomeRuntime
{
    public const string OutputReasonCode = "captivity-interaction-output";
    private readonly IDomainFactStagingPublisher publisher;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IPhysicalItemSourcePublicationService physicalSources;
    private CaptivityInteractionPublication active;
    private bool recovering;
    public CaptivityInteractionOutcomeRuntime(IDomainFactStagingPublisher publisher,
        IDomainFactRecoveryPublisher recovery, IPhysicalItemSourcePublicationService physicalSources)
    {
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.physicalSources = physicalSources ?? throw new ArgumentNullException(nameof(physicalSources));
    }

    public bool TryPrepare(CaptiveState state, out CaptivityInteractionPublication prepared,
        out long ownerRevision, out string failureReason)
    {
        prepared = null; ownerRevision = 0L; failureReason = string.Empty;
        if (active != null || recovering) { failureReason = "captivity-interaction-command-active"; return false; }
        var terminal = state?.interactionTerminal;
        if (terminal?.HasOutcome != true || terminal.HasCommittedOutcome || terminal.deliveryPending
            || terminal.publicationKey != string.Empty)
        { failureReason = "captivity-interaction-unapplied-result-required"; return false; }
        IDomainFactStage stage = null;
        try
        {
            ownerRevision = checked(state.interactionOutcomeRevision + 1L);
            var fact = new CaptivityInteractionResolvedFact(state.captiveId, terminal, ownerRevision);
            if (!publisher.TryStage(fact, out stage, out failureReason)) return false;
            if (stage == null || !stage.IsActive || string.IsNullOrWhiteSpace(stage.OperationKey) || stage.OwnerRevision != ownerRevision)
            { failureReason = "captivity-interaction-stage-invalid"; return false; }
            // A canonical result cannot authorize applying the same owner effects again.
            // Applied terminals recover through TryEnsureDelivered, never this path.
            if (stage.IsReplay)
            { failureReason = "captivity-interaction-unapplied-replay-conflict"; return false; }
            active = prepared = new CaptivityInteractionPublication(this, state, fact, stage);
            stage = null; return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failureReason = "captivity-interaction-prepare:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { CancelStage(stage); }
    }

    public DomainFactDelivery Commit(CaptivityInteractionPublication prepared)
    {
        if (prepared == null || prepared != active) return DomainFactDelivery.Reject("captivity-interaction-publication-inactive");
        if (prepared.HandedOff) return DomainFactDelivery.Uncertain("captivity-interaction-already-handed-off");
        var state = prepared.State;
        var terminal = state.interactionTerminal;
        var expected = prepared.Fact.Snapshot();
        if (state.interactionOutcomeRevision != prepared.Fact.OwnerRevision || terminal == null
            || terminal.outcomeRevision != prepared.Fact.OwnerRevision
            || state.will != expected.willAfter || state.fear != expected.fearAfter || state.trust != expected.trustAfter
            || state.grudge != expected.grudgeAfter || state.corruption != expected.corruptionAfter)
            return DomainFactDelivery.Reject("captivity-interaction-applied-state-conflict");
        try
        {
            if (new CaptivityInteractionResolvedFact(state.captiveId, terminal, terminal.outcomeRevision).ComputeSemanticDigest()
                != prepared.Fact.ComputeSemanticDigest())
                return DomainFactDelivery.Reject("captivity-interaction-result-changed");
        }
        catch (ArgumentException) { return DomainFactDelivery.Reject("captivity-interaction-applied-result-invalid"); }
        terminal.publicationKey = prepared.Stage.OperationKey;
        terminal.deliveryPending = true;
        prepared.HandedOff = true;
        DomainFactDelivery result;
        try { result = prepared.Stage.Commit(); }
        catch (Exception exception) when (Recoverable(exception))
        { result = DomainFactDelivery.Uncertain("captivity-interaction-commit:" + exception.GetType().Name); }
        if (!result.MayBeCommitted) { terminal.publicationKey = string.Empty; terminal.deliveryPending = false; }
        else if (Delivered(terminal.publicationKey, result)) terminal.deliveryPending = false;
        return result;
    }
    public bool TryEnsureDelivered(CaptiveState state, out string failureReason)
    {
        failureReason = string.Empty;
        if (active != null || recovering) { failureReason = "captivity-interaction-command-active"; return false; }
        var terminal = state?.interactionTerminal;
        if (terminal?.HasCommittedOutcome != true || terminal.outcomeRevision != state.interactionOutcomeRevision
            || string.IsNullOrWhiteSpace(terminal.publicationKey))
        { failureReason = "captivity-interaction-applied-publication-missing"; return false; }
        if (!terminal.deliveryPending) return true;
        recovering = true;
        try
        {
            var fact = new CaptivityInteractionResolvedFact(state.captiveId, terminal, terminal.outcomeRevision);
            var results = new DomainFactDelivery[1];
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(terminal.publicationKey, fact) }, results, out failureReason)
                || !Delivered(terminal.publicationKey, results[0]))
            {
                failureReason = "captivity-interaction-delivery-pending:" + failureReason + ":" + results[0].Detail;
                return false;
            }
            terminal.deliveryPending = false;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failureReason = "captivity-interaction-recovery:" + exception.GetType().Name + ":" + exception.Message; return false; }
        finally { recovering = false; }
    }
    public void ValidateBeforeCapture()
    { if (active != null || recovering) throw new InvalidOperationException("captivity-interaction-command-active"); }
    public void Cancel(CaptivityInteractionPublication prepared)
    { if (prepared == active) CancelStage(prepared?.Stage); }
    internal void Release(CaptivityInteractionPublication prepared)
    {
        if (prepared != active) return;
        try { CancelStage(prepared.Stage); }
        finally { active = null; }
    }
    private static void CancelStage(IDomainFactStage stage)
    {
        try { stage?.Cancel(); }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogWarning("captivity-interaction-cancel:" + exception); }
    }
    private static bool Delivered(string key, DomainFactDelivery value)
    {
        if (!value.Completed || value.OperationKey != key || string.IsNullOrWhiteSpace(value.PublicationId)
            || value.Fingerprint.Length != 64) return false;
        foreach (char c in value.Fingerprint)
            if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')) return false;
        return true;
    }
    private static bool Recoverable(Exception value) => value is not OutOfMemoryException
        && value is not StackOverflowException && value is not AccessViolationException;

    public bool TryEnsureOutput(
        CaptiveState state,
        out string failureReason)
    {
        failureReason = string.Empty;
        CaptivityInteractionTerminalState terminal = state?.interactionTerminal;
        if (terminal?.HasDeliveredOutcome != true)
        {
            failureReason = "확정된 포로 상호작용 출력 상태가 없습니다.";
            return false;
        }
        if (!terminal.HasOutput)
            return true;
        string expectedOperation = CaptivityInteractionAttemptIdentity
            .FormatOutputOperationId(
            state.captiveId,
            terminal.attemptId);
        if (!string.Equals(
                terminal.outputOperationId,
                expectedOperation,
                StringComparison.Ordinal))
        {
            failureReason = "포로 상호작용 출력 작업 ID가 일치하지 않습니다.";
            return false;
        }
        if (terminal.outputPublished)
        {
            if (string.IsNullOrWhiteSpace(terminal.outputCommitId))
            {
                failureReason = "포로 상호작용 출력 커밋 ID가 없습니다.";
                return false;
            }
            return true;
        }

        Dictionary<string, int> outputs = new(StringComparer.Ordinal)
        {
            [terminal.outputItemId] = terminal.outputAmount
        };
        if (!physicalSources.TryEnsureLooseOutputs(
                outputs,
                terminal.ResultPosition,
                terminal.outputOperationId,
                OutputReasonCode,
                out PhysicalItemSourcePublicationReceipt receipt,
                out failureReason))
        {
            return false;
        }
        string commitId = receipt.OutputCommitIds?.SingleOrDefault()
            ?? string.Empty;
        if (!receipt.IsCommitted
            || !string.Equals(
                receipt.OperationId,
                terminal.outputOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                OutputReasonCode,
                StringComparison.Ordinal)
            || receipt.OutputQuantity != terminal.outputAmount
            || string.IsNullOrWhiteSpace(commitId))
        {
            failureReason = "포로 상호작용 출력 영수증이 요청과 다릅니다.";
            return false;
        }
        terminal.outputCommitId = commitId;
        terminal.outputPublished = true;
        return true;
    }


}

internal sealed class CaptivityInteractionPublication : IDisposable
{
    private readonly CaptivityInteractionOutcomeRuntime owner;
    internal CaptivityInteractionPublication(CaptivityInteractionOutcomeRuntime owner, CaptiveState state,
        CaptivityInteractionResolvedFact fact, IDomainFactStage stage)
    { this.owner = owner; State = state; Fact = fact; Stage = stage; }
    internal CaptiveState State { get; }
    internal CaptivityInteractionResolvedFact Fact { get; }
    internal IDomainFactStage Stage { get; }
    internal bool HandedOff { get; set; }
    public bool IsReplay => Stage.IsReplay;
    public void Dispose() => owner.Release(this);
}
