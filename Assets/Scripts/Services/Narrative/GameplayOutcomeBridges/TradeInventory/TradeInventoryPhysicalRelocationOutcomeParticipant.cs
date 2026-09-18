using System;

public sealed class DefaultPhysicalItemRelocationOutcomeParticipant :
    IPhysicalItemRelocationOutcomeParticipant
{
    private readonly ITradeInventoryOutcomeCommitter committer;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameCalendar calendar;

    public DefaultPhysicalItemRelocationOutcomeParticipant(
        ITradeInventoryOutcomeCommitter committer,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameCalendar calendar)
    {
        this.committer = committer
            ?? throw new ArgumentNullException(nameof(committer));
        this.diagnostics = diagnostics
            ?? throw new ArgumentNullException(nameof(diagnostics));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public bool TryPrepare(
        in PreparedPhysicalItemRelocationPreview preview,
        out IPreparedPhysicalItemGameplayOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        failureReason = string.Empty;
        if (!preview.IsValid)
        {
            failureReason = "physical-relocation-outcome-preview-invalid";
            return false;
        }

        try
        {
            PhysicalItemRelocationReceipt exact = preview.Receipt;
            string slug = TradeInventoryOutcomeIds.Slug(
                TradeInventoryOutcomeKind.PhysicalItemRelocation);
            GameplayEntityId sourceStack = new(
                TradeInventoryOutcomeIds.ItemStackKind,
                exact.SourceStackId);
            GameplayEntityId destinationStack = new(
                TradeInventoryOutcomeIds.ItemStackKind,
                exact.DestinationStackId);
            GameplayEntityId itemDefinition = new(
                TradeInventoryOutcomeIds.ItemDefinitionKind,
                exact.ItemId);
            GameplayEntityId operationEntity = new(
                TradeInventoryOutcomeIds.OperationKind,
                exact.OperationId);
            bool hasInstance = preview.ItemInstanceId.Length > 0;
            GameplayOutcomeParticipant[] participants = hasInstance
                ? new[]
                {
                    new GameplayOutcomeParticipant(
                        sourceStack,
                        TradeInventoryOutcomeIds.SourceRole,
                        GameplayParticipationKind.Direct,
                        true,
                        preview.DisplayName),
                    new GameplayOutcomeParticipant(
                        destinationStack,
                        TradeInventoryOutcomeIds.DestinationRole,
                        GameplayParticipationKind.Direct,
                        true,
                        preview.DisplayName),
                    new GameplayOutcomeParticipant(
                        itemDefinition,
                        TradeInventoryOutcomeIds.ItemRole,
                        GameplayParticipationKind.Direct,
                        true,
                        preview.DisplayName),
                    new GameplayOutcomeParticipant(
                        new GameplayEntityId(
                            TradeInventoryOutcomeIds.ItemInstanceKind,
                            preview.ItemInstanceId),
                        TradeInventoryOutcomeIds.ItemRole,
                        GameplayParticipationKind.Direct,
                        true,
                        preview.DisplayName)
                }
                : new[]
                {
                    new GameplayOutcomeParticipant(
                        sourceStack,
                        TradeInventoryOutcomeIds.SourceRole,
                        GameplayParticipationKind.Direct,
                        true,
                        preview.DisplayName),
                    new GameplayOutcomeParticipant(
                        destinationStack,
                        TradeInventoryOutcomeIds.DestinationRole,
                        GameplayParticipationKind.Direct,
                        true,
                        preview.DisplayName),
                    new GameplayOutcomeParticipant(
                        itemDefinition,
                        TradeInventoryOutcomeIds.ItemRole,
                        GameplayParticipationKind.Direct,
                        true,
                        preview.DisplayName)
                };
            GameplayOutcomeMetric[] metrics =
            {
                new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.KindMetric,
                    (int)TradeInventoryOutcomeKind.PhysicalItemRelocation,
                    TradeInventoryOutcomeIds.EnumUnit,
                    new GameplayEntityId(
                        TradeInventoryOutcomeIds.OutcomeKind,
                        slug)),
                new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.ResultRevisionMetric,
                    preview.OwnerRevision,
                    TradeInventoryOutcomeIds.RevisionUnit,
                    operationEntity),
                new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.QuantityMetric,
                    exact.Quantity,
                    TradeInventoryOutcomeIds.CountUnit,
                    itemDefinition),
                new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.MassMetric,
                    exact.MassGrams,
                    TradeInventoryOutcomeIds.GramUnit,
                    itemDefinition),
                new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.DestinationXMetric,
                    exact.DestinationPosition.x,
                    TradeInventoryOutcomeIds.CellUnit,
                    destinationStack),
                new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.DestinationYMetric,
                    exact.DestinationPosition.y,
                    TradeInventoryOutcomeIds.CellUnit,
                    destinationStack)
            };
            GameplayOutcomeSubjectLink[] subjects =
                sourceStack.Equals(destinationStack)
                    ? new[]
                    {
                        new GameplayOutcomeSubjectLink(
                            sourceStack,
                            0.45f,
                            NarrativeMemoryTier.Recent,
                            false,
                            false,
                            0),
                        new GameplayOutcomeSubjectLink(
                            itemDefinition,
                            0.5f,
                            NarrativeMemoryTier.Recent,
                            false,
                            false,
                            0)
                    }
                    : new[]
                    {
                        new GameplayOutcomeSubjectLink(
                            sourceStack,
                            0.35f,
                            NarrativeMemoryTier.Recent,
                            false,
                            false,
                            0),
                        new GameplayOutcomeSubjectLink(
                            destinationStack,
                            0.45f,
                            NarrativeMemoryTier.Recent,
                            false,
                            false,
                            0),
                        new GameplayOutcomeSubjectLink(
                            itemDefinition,
                            0.5f,
                            NarrativeMemoryTier.Recent,
                            false,
                            false,
                            0)
                    };
            GameplayOutcomeTagId[] tags =
            {
                TradeInventoryOutcomeIds.TradeInventoryTag,
                TradeInventoryOutcomeIds.PhysicalTag
            };
            GameplayOutcomeProvenanceReference[] provenance =
            {
                TradeInventoryOutcomeIds.RuntimeReceiptProvenance,
                new GameplayOutcomeProvenanceReference("source-receipt", slug),
                new GameplayOutcomeProvenanceReference(
                    "domain-commit",
                    "physical-relocation:" + exact.OperationId + ":"
                    + preview.OwnerRevision)
            };
            GameplayOutcomeFact[] facts =
            {
                new GameplayOutcomeFact(
                    TradeInventoryOutcomeIds.ReasonCodeFact,
                    exact.ReasonCode)
            };
            TradeInventoryOutcomeReceipt receipt = new(
                TradeInventoryOutcomeKind.PhysicalItemRelocation,
                exact.OperationId,
                0L,
                preview.OwnerRevision,
                Math.Max(1, calendar.Day),
                GameplayOutcomeStatus.Succeeded,
                participants,
                metrics,
                subjects,
                tags,
                provenance,
                facts,
                new GameplayLocationReference(
                    "dungeon",
                    string.Empty,
                    exact.SourcePosition.x,
                    exact.SourcePosition.y));
            if (!committer.TryPrepare(
                    receipt,
                    out PreparedTradeInventoryOutcome preparedTrade,
                    out failureReason))
                return false;
            prepared = new PreparedRelocationOutcome(
                committer,
                diagnostics,
                preparedTrade);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "physical-relocation-outcome-prepare-invalid:"
                + exception.Message;
            return false;
        }
    }

    private sealed class PreparedRelocationOutcome :
        IPreparedPhysicalItemGameplayOutcome
    {
        private readonly ITradeInventoryOutcomeCommitter committer;
        private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
        private readonly PreparedTradeInventoryOutcome prepared;
        private bool terminal;

        internal PreparedRelocationOutcome(
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
                failureReason =
                    "physical-relocation-outcome-prepared-already-terminal";
                return false;
            }
            bool callSucceeded;
            try
            {
                callSucceeded = committer.TryCommit(
                    prepared,
                    expectedOwnerRevision,
                    out canonicalCommitted,
                    out failureReason);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failureReason =
                    "physical-relocation-outcome-commit-exception:"
                    + exception.Message;
                return false;
            }
            if (!callSucceeded && !canonicalCommitted)
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
                            "physical-relocation-outcome-canonical-identity-pending";
                    return false;
                }
                attachment = new PhysicalGameplayOutcomeAttachment(
                    identity.ResultKey,
                    identity.OutcomeId,
                    identity.State,
                    identity.CanonicalPayloadHash);
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failureReason =
                    "physical-relocation-outcome-canonical-identity-pending:"
                    + exception.Message;
                return false;
            }
            if (!attachment.IsValid)
            {
                failureReason =
                    "physical-relocation-outcome-canonical-identity-invalid";
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
