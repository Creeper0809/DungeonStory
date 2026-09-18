using System;

/// <summary>
/// Adapts the certified-seed owner's exact, preallocated output publication
/// preview into the environment ledger.  The publication service owns the
/// physical apply/rollback boundary; this participant owns only the prepared
/// ledger record and never performs a second inventory mutation.
/// </summary>
public sealed class CertifiedSeedOutputOutcomeParticipant :
    IProductionDomainOutputGameplayOutcomeParticipant
{
    private readonly IEnvironmentGameplayOutcomeCommitter committer;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameCalendar calendar;
    private readonly CertifiedSeedPlanExecutionReceipt source;
    private readonly string facilityDisplayName;
    private readonly string cropDisplayName;

    public CertifiedSeedOutputOutcomeParticipant(
        IEnvironmentGameplayOutcomeCommitter committer,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameCalendar calendar,
        CertifiedSeedPlanExecutionReceipt source,
        string facilityDisplayName,
        string cropDisplayName)
    {
        this.committer = committer
            ?? throw new ArgumentNullException(nameof(committer));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.source = source ?? throw new ArgumentNullException(nameof(source));
        this.facilityDisplayName = RequireDisplay(
            facilityDisplayName,
            nameof(facilityDisplayName));
        this.cropDisplayName = RequireDisplay(
            cropDisplayName,
            nameof(cropDisplayName));
    }

    public bool TryPrepare(
        in ProductionDomainOutputGameplayOutcomePreview exactPreview,
        out IPreparedPhysicalItemGameplayOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        failureReason = string.Empty;
        if (!exactPreview.IsValid
            || exactPreview.Physical.Facts.Count != 1
            || exactPreview.Physical.OwnerRevision <= 0L
            || !string.Equals(
                exactPreview.OwnerId,
                source.OrderId,
                StringComparison.Ordinal))
        {
            failureReason = "certified-seed-outcome-preview-invalid";
            return false;
        }

        PreparedFacilityBufferOutputFact output =
            exactPreview.Physical.Facts[0];
        if (!output.IsValid
            || string.IsNullOrWhiteSpace(output.DisplayName.DisplayText))
        {
            failureReason = "certified-seed-output-display-snapshot-missing";
            return false;
        }

        CertifiedSeedCompletionOutcomeReceipt receipt;
        try
        {
            receipt = EnvironmentOutcomeReceiptFactory
                .CreateCertifiedSeedCompletion(
                    source,
                    facilityDisplayName,
                    cropDisplayName,
                    output.Stack.ItemDefinitionId.Value,
                    output.DisplayName.DisplayText,
                    output.Stack.Quantity,
                    output.Stack.MassGrams,
                    exactPreview.OutcomeFingerprint,
                    Math.Max(1, calendar.Day),
                    exactPreview.Physical.OwnerRevision);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failureReason = "certified-seed-outcome-receipt-invalid:"
                + exception.Message;
            return false;
        }

        if (committer.TryPrepare(
                receipt,
                out PreparedEnvironmentOutcome preparedEnvironment,
                out failureReason))
        {
            prepared = new PreparedEnvironmentPhysicalOutcome(
                committer,
                diagnostics,
                preparedEnvironment,
                receipt.Payload.ResultKey,
                isCanonicalReplay: false);
            return true;
        }

        if (!committer.IsCanonicalAcknowledgedReplay(
                receipt,
                out string replayFailure))
        {
            failureReason = failureReason + ":" + replayFailure;
            return false;
        }
        prepared = new PreparedEnvironmentPhysicalOutcome(
            committer,
            diagnostics,
            default,
            receipt.Payload.ResultKey,
            isCanonicalReplay: true);
        failureReason = string.Empty;
        return true;
    }

    private static string RequireDisplay(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(
                "An immutable display snapshot is required.",
                parameterName)
            : value.Trim();

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;
}

internal sealed class PreparedEnvironmentPhysicalOutcome :
    IPreparedPhysicalItemGameplayOutcome
{
    private readonly IEnvironmentGameplayOutcomeCommitter committer;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly PreparedEnvironmentOutcome prepared;
    private readonly GameplayResultKey resultKey;
    private readonly bool canonicalReplay;
    private bool terminal;

    internal PreparedEnvironmentPhysicalOutcome(
        IEnvironmentGameplayOutcomeCommitter committer,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        in PreparedEnvironmentOutcome prepared,
        GameplayResultKey resultKey,
        bool isCanonicalReplay)
    {
        this.committer = committer;
        this.diagnostics = diagnostics;
        this.prepared = prepared;
        this.resultKey = resultKey;
        canonicalReplay = isCanonicalReplay;
    }

    public GameplayResultKey ResultKey => resultKey;
    public bool IsCanonicalReplay => canonicalReplay;

    public bool TryCommit(
        long expectedOwnerRevision,
        out PhysicalGameplayOutcomeAttachment attachment,
        out bool canonicalCommitted,
        out string failureReason)
    {
        attachment = default;
        canonicalCommitted = false;
        failureReason = string.Empty;
        if (terminal)
        {
            failureReason = "environment-physical-outcome-already-terminal";
            return false;
        }

        EnvironmentOutcomeCommitResult result;
        try
        {
            result = canonicalReplay
                ? committer.Reconcile(resultKey)
                : committer.Commit(prepared, expectedOwnerRevision);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            result = committer.Reconcile(resultKey);
            failureReason = "environment-physical-outcome-commit-exception:"
                + exception.Message;
        }

        canonicalCommitted = result.DurablyCommitted;
        if (!canonicalCommitted)
        {
            failureReason = failureReason.Length == 0
                ? result.DetailCode
                : failureReason + ":" + result.DetailCode;
            return false;
        }
        terminal = true;
        try
        {
            if (!diagnostics.TryGetResultIdentity(
                    resultKey,
                    out GameplayOutcomeReplayIdentity identity)
                || !identity.ResultKey.Equals(resultKey))
            {
                failureReason = "environment-physical-outcome-identity-pending";
                return false;
            }
            attachment = new PhysicalGameplayOutcomeAttachment(
                identity.ResultKey,
                identity.OutcomeId,
                identity.State,
                identity.CanonicalPayloadHash);
            if (!attachment.IsValid)
            {
                failureReason = "environment-physical-outcome-identity-invalid";
                return false;
            }
            return true;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failureReason = "environment-physical-outcome-identity-exception:"
                + exception.Message;
            return false;
        }
    }

    public void Cancel()
    {
        if (terminal) return;
        if (prepared.IsValid)
            committer.Cancel(prepared);
        terminal = true;
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;
}
