using System;
using DungeonStory.Environment;

public sealed class EnvironmentalFireFuelOutcomeParticipant :
    IPhysicalItemBatchDispositionOutcomeParticipant
{
    private readonly IEnvironmentGameplayOutcomeCommitter committer;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameCalendar calendar;
    private readonly EnvironmentalFireFuelLossRequest request;
    private readonly CoreGridCell location;

    public EnvironmentalFireFuelOutcomeParticipant(
        IEnvironmentGameplayOutcomeCommitter committer,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameCalendar calendar,
        EnvironmentalFireFuelLossRequest request,
        CoreGridCell location)
    {
        this.committer = committer
            ?? throw new ArgumentNullException(nameof(committer));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.request = request ?? throw new ArgumentNullException(nameof(request));
        this.location = location;
    }

    public bool TryPrepare(
        in PhysicalItemBatchDispositionReceipt exactReceipt,
        long expectedOwnerRevision,
        out IPreparedPhysicalItemGameplayOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        if (!exactReceipt.IsCommitted
            || exactReceipt.Kind != PhysicalItemDispositionKind.Sink
            || exactReceipt.OwnerRevision != expectedOwnerRevision
            || exactReceipt.SourceFacts.Count != 1
            || exactReceipt.Quantity != request.Quantity)
        {
            failureReason = "environmental-fire-fuel-outcome-preview-invalid";
            return false;
        }
        PhysicalItemDispositionSourceFact source = exactReceipt.SourceFacts[0];
        if (!source.IsValid
            || !string.Equals(source.StackId, request.Target.TargetId,
                StringComparison.Ordinal))
        {
            failureReason = "environmental-fire-fuel-source-fact-invalid";
            return false;
        }
        var domain = new EnvironmentalFireFuelLossReceipt(
            request.Target,
            exactReceipt.OperationId,
            exactReceipt.Quantity,
            exactReceipt.InputMassGrams,
            exactReceipt.CommitId);
        EnvironmentalFireFuelOutcomeReceipt receipt;
        try
        {
            receipt = EnvironmentOutcomeReceiptFactory.CreateFireFuelLoss(
                domain,
                source.StackId,
                source.DisplayName.DisplayText,
                exactReceipt.RequestFingerprint,
                location,
                Math.Max(1, calendar.Day),
                expectedOwnerRevision);
        }
        catch (Exception exception) when (EnvironmentPhysicalOutcomeRules
            .IsRecoverable(exception))
        {
            failureReason = "environmental-fire-fuel-outcome-invalid:"
                + exception.Message;
            return false;
        }
        return EnvironmentPhysicalOutcomeRules.TryPrepare(
            committer,
            diagnostics,
            receipt,
            out prepared,
            out failureReason);
    }
}

public sealed class EnvironmentalFireWaterOutcomeParticipant :
    IPhysicalItemBatchDispositionOutcomeParticipant
{
    private readonly IEnvironmentGameplayOutcomeCommitter committer;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameCalendar calendar;
    private readonly string leaseId;
    private readonly CharacterId workerId;
    private readonly string workerDisplayName;
    private readonly CoreGridCell location;

    public EnvironmentalFireWaterOutcomeParticipant(
        IEnvironmentGameplayOutcomeCommitter committer,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameCalendar calendar,
        string leaseId,
        CharacterId workerId,
        string workerDisplayName,
        CoreGridCell location)
    {
        this.committer = committer
            ?? throw new ArgumentNullException(nameof(committer));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.leaseId = leaseId?.Trim() ?? string.Empty;
        this.workerId = workerId;
        this.workerDisplayName = workerDisplayName?.Trim() ?? string.Empty;
        this.location = location;
    }

    public bool TryPrepare(
        in PhysicalItemBatchDispositionReceipt exactReceipt,
        long expectedOwnerRevision,
        out IPreparedPhysicalItemGameplayOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        if (!exactReceipt.IsCommitted
            || exactReceipt.Kind != PhysicalItemDispositionKind.Sink
            || exactReceipt.OwnerRevision != expectedOwnerRevision
            || exactReceipt.SourceFacts.Count == 0
            || leaseId.Length == 0
            || !workerId.IsValid
            || workerDisplayName.Length == 0)
        {
            failureReason = "environmental-fire-water-outcome-preview-invalid";
            return false;
        }
        PhysicalItemDispositionSourceFact first = exactReceipt.SourceFacts[0];
        if (!first.IsValid)
        {
            failureReason = "environmental-fire-water-source-fact-invalid";
            return false;
        }
        for (int index = 1; index < exactReceipt.SourceFacts.Count; index++)
        {
            PhysicalItemDispositionSourceFact next = exactReceipt.SourceFacts[index];
            if (!next.IsValid
                || !string.Equals(next.ItemDefinitionId,
                    first.ItemDefinitionId, StringComparison.Ordinal)
                || !string.Equals(next.DisplayName.DisplayText,
                    first.DisplayName.DisplayText, StringComparison.Ordinal))
            {
                failureReason =
                    "environmental-fire-water-source-cohort-invalid";
                return false;
            }
        }
        var domain = new EnvironmentalFireWaterReceipt(
            exactReceipt.OperationId,
            leaseId,
            exactReceipt.Quantity,
            exactReceipt.CommitId);
        EnvironmentalFireWaterOutcomeReceipt receipt;
        try
        {
            receipt = EnvironmentOutcomeReceiptFactory.CreateFireWaterConsumed(
                domain,
                workerId,
                workerDisplayName,
                first.ItemDefinitionId,
                first.DisplayName.DisplayText,
                exactReceipt.InputMassGrams,
                exactReceipt.RequestFingerprint,
                location,
                Math.Max(1, calendar.Day),
                expectedOwnerRevision);
        }
        catch (Exception exception) when (EnvironmentPhysicalOutcomeRules
            .IsRecoverable(exception))
        {
            failureReason = "environmental-fire-water-outcome-invalid:"
                + exception.Message;
            return false;
        }
        return EnvironmentPhysicalOutcomeRules.TryPrepare(
            committer,
            diagnostics,
            receipt,
            out prepared,
            out failureReason);
    }
}

internal static class EnvironmentPhysicalOutcomeRules
{
    internal static bool TryPrepare<TReceipt>(
        IEnvironmentGameplayOutcomeCommitter committer,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        in TReceipt receipt,
        out IPreparedPhysicalItemGameplayOutcome prepared,
        out string failureReason)
        where TReceipt : struct, IEnvironmentOutcomeReceipt
    {
        if (committer.TryPrepare(
                receipt,
                out PreparedEnvironmentOutcome token,
                out failureReason))
        {
            prepared = new PreparedEnvironmentPhysicalOutcome(
                committer,
                diagnostics,
                token,
                receipt.Payload.ResultKey,
                isCanonicalReplay: false);
            return true;
        }
        if (!committer.IsCanonicalAcknowledgedReplay(
                receipt,
                out string replayFailure))
        {
            prepared = null;
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

    internal static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;
}
