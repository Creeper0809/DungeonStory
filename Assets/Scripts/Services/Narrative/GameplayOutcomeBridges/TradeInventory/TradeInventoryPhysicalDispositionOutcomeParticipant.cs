using System;

public sealed class DefaultPhysicalBatchDispositionOutcomeParticipant :
    IDefaultPhysicalItemBatchDispositionOutcomeParticipant
{
    private readonly ITradeInventoryOutcomeCommitter committer;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameCalendar calendar;

    public DefaultPhysicalBatchDispositionOutcomeParticipant(
        ITradeInventoryOutcomeCommitter committer,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameCalendar calendar)
    {
        this.committer = committer ?? throw new ArgumentNullException(nameof(committer));
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public bool TryPrepare(
        in PhysicalItemBatchDispositionReceipt exactReceipt,
        long expectedOwnerRevision,
        out IPreparedPhysicalItemGameplayOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        failureReason = string.Empty;
        int sourceCount = exactReceipt.SourceFacts?.Count ?? 0;
        if (!exactReceipt.IsCommitted
            || expectedOwnerRevision < 0L
            || exactReceipt.OwnerRevision != expectedOwnerRevision
            || sourceCount == 0)
        {
            failureReason = "physical-outcome-exact-receipt-invalid";
            return false;
        }

        try
        {
            for (int index = 0; index < sourceCount; index++)
            {
                if (!exactReceipt.SourceFacts[index].IsValid)
                {
                    failureReason = "physical-outcome-source-fact-invalid";
                    return false;
                }
            }

            TradeInventoryOutcomeReceipt receipt =
                PhysicalItemDispositionFactObservation.Capture(
                    new PhysicalItemDisposedFact(exactReceipt, calendar.Day));
            if (!committer.TryPrepare(
                    receipt,
                    out PreparedTradeInventoryOutcome preparedTrade,
                    out failureReason))
                return false;
            prepared = new PreparedPhysicalDispositionOutcome(
                committer,
                diagnostics,
                preparedTrade);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "physical-outcome-prepare-invalid:"
                + exception.Message;
            return false;
        }
    }

    private sealed class PreparedPhysicalDispositionOutcome :
        IPreparedPhysicalItemGameplayOutcomeToken
    {
        private readonly ITradeInventoryOutcomeCommitter committer;
        private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
        private readonly PreparedTradeInventoryOutcome prepared;
        private bool terminal;

        internal PreparedPhysicalDispositionOutcome(
            ITradeInventoryOutcomeCommitter committer,
            IGameplayOutcomeDiagnosticsQuery diagnostics,
            in PreparedTradeInventoryOutcome prepared)
        {
            this.committer = committer;
            this.diagnostics = diagnostics;
            this.prepared = prepared;
        }

        public GameplayResultKey ResultKey => prepared.ResultKey;
        public bool IsCanonicalReplay => prepared.IsCanonicalReplay;
        public PreparedOutcomeToken Token => prepared.Token;

        public bool TryCommit(
            long expectedOwnerRevision,
            out PhysicalGameplayOutcomeAttachment attachment,
            out bool canonicalCommitted,
            out string failureReason)
        {
            attachment = default;
            canonicalCommitted = false;
            if (terminal)
            {
                failureReason = "physical-outcome-prepared-already-terminal";
                return false;
            }
            bool commitSucceeded;
            try
            {
                commitSucceeded = committer.TryCommit(
                    prepared,
                    expectedOwnerRevision,
                    out canonicalCommitted,
                    out failureReason);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failureReason = "physical-outcome-commit-exception:"
                    + exception.Message;
                return false;
            }
            if (!commitSucceeded && !canonicalCommitted)
                return false;
            if (canonicalCommitted)
                terminal = true;
            try
            {
                if (!diagnostics.TryGetResultIdentity(
                        prepared.ResultKey,
                        out GameplayOutcomeReplayIdentity identity)
                    || !identity.ResultKey.Equals(prepared.ResultKey))
                {
                    if (failureReason.Length == 0)
                        failureReason =
                            "physical-outcome-canonical-identity-missing";
                    return false;
                }
                attachment = new PhysicalGameplayOutcomeAttachment(
                    identity.ResultKey,
                    identity.OutcomeId,
                    identity.State,
                    identity.CanonicalPayloadHash);
                if (!attachment.IsValid)
                {
                    failureReason =
                        "physical-outcome-canonical-identity-invalid";
                    return false;
                }
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failureReason =
                    "physical-outcome-canonical-identity-pending:"
                    + exception.Message;
                return false;
            }
            terminal = true;
            return true;
        }

        public void Cancel()
        {
            if (terminal) return;
            committer.Cancel(prepared);
            terminal = true;
        }

        private static bool IsRecoverable(Exception exception) =>
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException;
    }
}
