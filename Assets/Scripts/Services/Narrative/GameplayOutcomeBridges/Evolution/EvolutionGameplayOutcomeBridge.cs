using System;

public interface IProductQualityOutcomeCommitter
{
    bool TryPrepare(
        in ProductQualityOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    void Cancel(in PreparedEvolutionOutcome prepared);
    bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason);
}

public interface IHeritableTraitAnalysisOutcomeCommitter
{
    bool TryPrepare(
        in HeritableTraitAnalysisOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    void Cancel(in PreparedEvolutionOutcome prepared);
    bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason);
}

public interface ICharacterSkillAcquiredOutcomeCommitter
{
    bool TryPrepare(
        in CharacterSkillAcquiredOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    void Cancel(in PreparedEvolutionOutcome prepared);
    bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason);
}

public interface ICharacterAmbitionTransitionOutcomeCommitter
{
    bool TryPrepare(
        in CharacterAmbitionTransitionOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    void Cancel(in PreparedEvolutionOutcome prepared);
    bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason);
}


public interface IFacilityEvolutionOutcomeCommitter
{
    bool TryPrepare(
        in FacilityEvolutionOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason);
    void Cancel(in PreparedEvolutionOutcome prepared);
    bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason);
}


public sealed class EvolutionGameplayOutcomeBridge :
    IProductQualityOutcomeCommitter,
    ICharacterSkillAcquiredOutcomeCommitter,
    ICharacterAmbitionTransitionOutcomeCommitter,
    IHeritableTraitAnalysisOutcomeCommitter,
    IFacilityEvolutionOutcomeCommitter
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public EvolutionGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _ = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    bool IProductQualityOutcomeCommitter.TryPrepare(
        in ProductQualityOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason) => Prepare(
        receipt,
        receipt.ResultKey,
        out prepared,
        out failureReason);

    void IProductQualityOutcomeCommitter.Cancel(
        in PreparedEvolutionOutcome prepared) => CancelPrepared(prepared);

    bool IProductQualityOutcomeCommitter.TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason) => Commit(
        prepared,
        expectedOwnerRevision,
        out failureReason);

    bool ICharacterSkillAcquiredOutcomeCommitter.TryPrepare(
        in CharacterSkillAcquiredOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason) => Prepare(
        receipt,
        receipt.ResultKey,
        out prepared,
        out failureReason);

    void ICharacterSkillAcquiredOutcomeCommitter.Cancel(
        in PreparedEvolutionOutcome prepared) => CancelPrepared(prepared);

    bool ICharacterSkillAcquiredOutcomeCommitter.TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason) => Commit(
        prepared,
        expectedOwnerRevision,
        out failureReason);

    bool ICharacterAmbitionTransitionOutcomeCommitter.TryPrepare(
        in CharacterAmbitionTransitionOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason) => Prepare(
        receipt,
        receipt.ResultKey,
        out prepared,
        out failureReason);

    void ICharacterAmbitionTransitionOutcomeCommitter.Cancel(
        in PreparedEvolutionOutcome prepared) => CancelPrepared(prepared);

    bool ICharacterAmbitionTransitionOutcomeCommitter.TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason) => Commit(
        prepared,
        expectedOwnerRevision,
            out failureReason);


    bool IHeritableTraitAnalysisOutcomeCommitter.TryPrepare(
        in HeritableTraitAnalysisOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason) => Prepare(
        receipt,
        receipt.ResultKey,
        out prepared,
        out failureReason);

    void IHeritableTraitAnalysisOutcomeCommitter.Cancel(
        in PreparedEvolutionOutcome prepared) => CancelPrepared(prepared);

    bool IHeritableTraitAnalysisOutcomeCommitter.TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason) => Commit(
        prepared,
        expectedOwnerRevision,
        out failureReason);

    bool IFacilityEvolutionOutcomeCommitter.TryPrepare(
        in FacilityEvolutionOutcomeReceipt receipt,
        out PreparedEvolutionOutcome prepared,
        out string failureReason) => Prepare(receipt, receipt.ResultKey, out prepared, out failureReason);

    void IFacilityEvolutionOutcomeCommitter.Cancel(in PreparedEvolutionOutcome prepared) =>
        Cancel(prepared);

    bool IFacilityEvolutionOutcomeCommitter.TryCommit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason) => Commit(prepared, expectedOwnerRevision, out failureReason);


    public void Cancel(in PreparedEvolutionOutcome prepared) => CancelPrepared(prepared);

    public bool TryCommit(
        in PreparedEvolutionOutcome prepared,
        out string failureReason) => Commit(prepared, 0L, out failureReason);


    private bool Prepare<TReceipt>(
        in TReceipt receipt,
        GameplayResultKey resultKey,
        out PreparedEvolutionOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        failureReason = string.Empty;
        OutcomePrepareResult result = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken token);
        if (!result.Success)
        {
            if (IsCanonicalReplay(result, resultKey))
            {
                prepared = new PreparedEvolutionOutcome(
                    default,
                    resultKey,
                    alreadyCommitted: true);
                return true;
            }
            failureReason = Format("evolution-outcome-prepare", result);
            return false;
        }
        prepared = new PreparedEvolutionOutcome(token, resultKey, false);
        return true;
    }

    private bool Commit(
        in PreparedEvolutionOutcome prepared,
        long expectedOwnerRevision,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!prepared.IsValid)
        {
            failureReason = "evolution-outcome-prepared-token-invalid";
            return false;
        }
        if (prepared.AlreadyCommitted)
        {
            recorder.RetryPendingDeliveries(1);
            return true;
        }
        OutcomeCommitResult result = recorder.CommitPrepared(
            prepared.Prepared,
            expectedOwnerRevision,
            out CommittedOutcomeToken committed);
        if (!result.Success)
        {
            recorder.CancelPrepared(prepared.Prepared);
            failureReason = "evolution-outcome-commit-" + result.Code
                + ":" + result.DetailCode;
            return false;
        }
        OutcomeDeliveryResult delivery = recorder.TryDeliver(committed);
        if (delivery.Published)
            recorder.Acknowledge(committed);
        return true;
    }

    private void CancelPrepared(in PreparedEvolutionOutcome prepared)
    {
        if (prepared.Prepared.IsValid)
            recorder.CancelPrepared(prepared.Prepared);
    }

    private bool IsCommittedOrPublished(GameplayResultKey resultKey)
    {
        var rows = diagnostics.GetOutboxSnapshot();
        for (int index = 0; index < rows.Count; index++)
        {
            GameplayOutcomeOutboxSnapshot row = rows[index];
            if (row.ResultKey == resultKey
                && row.Lifecycle >= GameplayOutcomeRecordLifecycle.Committed)
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsCommittedReplayCandidate(
        in GameplayOutcomeReplayIdentity existing,
        GameplayResultKey resultKey) =>
        existing.ResultKey == resultKey
        && existing.HasCanonicalPayloadHash
        && existing.State is >= GameplayOutcomeReplayState.Committed
            and <= GameplayOutcomeReplayState.Forgotten;

    private static bool IsCanonicalReplay(
        in OutcomePrepareResult result,
        GameplayResultKey resultKey) =>
        IsCommittedReplayCandidate(result.Existing, resultKey)
        && result.Code is OutcomePrepareCode.AlreadyCommitted
            or OutcomePrepareCode.AlreadyPublished
            or OutcomePrepareCode.AlreadyTerminal;

    private static string Format(string prefix, OutcomePrepareResult result) =>
        prefix + "-" + result.Code + ":" + result.DetailCode;

}
