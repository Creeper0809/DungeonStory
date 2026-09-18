using System;
using System.Buffers;
using System.Collections.Generic;

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

        GameplayOutcomeParticipant[] participants =
            ArrayPool<GameplayOutcomeParticipant>.Shared.Rent(sourceCount * 2);
        GameplayOutcomeMetric[] metrics =
            ArrayPool<GameplayOutcomeMetric>.Shared.Rent(5 + sourceCount * 4);
        GameplayOutcomeSubjectLink[] subjects =
            ArrayPool<GameplayOutcomeSubjectLink>.Shared.Rent(sourceCount * 2);
        GameplayOutcomeTagId[] tags =
            ArrayPool<GameplayOutcomeTagId>.Shared.Rent(2);
        GameplayOutcomeProvenanceReference[] provenance =
            ArrayPool<GameplayOutcomeProvenanceReference>.Shared.Rent(3);
        GameplayOutcomeFact[] facts =
            ArrayPool<GameplayOutcomeFact>.Shared.Rent(2);
        int participantCount = 0;
        int metricCount = 0;
        int subjectCount = 0;
        try
        {
            GameplayEntityId operationEntity = new(
                TradeInventoryOutcomeIds.OperationKind,
                exactReceipt.OperationId);
            string slug = TradeInventoryOutcomeIds.Slug(
                TradeInventoryOutcomeKind.PhysicalItemBatchDisposition);
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.KindMetric,
                (int)TradeInventoryOutcomeKind.PhysicalItemBatchDisposition,
                TradeInventoryOutcomeIds.EnumUnit,
                new GameplayEntityId(
                    TradeInventoryOutcomeIds.OutcomeKind,
                    slug));
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.ResultRevisionMetric,
                expectedOwnerRevision,
                TradeInventoryOutcomeIds.RevisionUnit,
                operationEntity);
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.InputQuantityMetric,
                exactReceipt.Quantity,
                TradeInventoryOutcomeIds.CountUnit,
                operationEntity);
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.InputMassMetric,
                exactReceipt.InputMassGrams,
                TradeInventoryOutcomeIds.GramUnit,
                operationEntity);
            metrics[metricCount++] = new GameplayOutcomeMetric(
                TradeInventoryOutcomeIds.DispositionMetric,
                (int)exactReceipt.Kind,
                TradeInventoryOutcomeIds.EnumUnit,
                operationEntity);

            bool samePosition = true;
            UnityEngine.Vector2Int firstPosition =
                exactReceipt.SourceFacts[0].SourcePosition;
            for (int index = 0; index < sourceCount; index++)
            {
                PhysicalItemDispositionSourceFact source =
                    exactReceipt.SourceFacts[index];
                if (!source.IsValid)
                {
                    failureReason = "physical-outcome-source-fact-invalid";
                    return false;
                }
                GameplayEntityId stackEntity = new(
                    TradeInventoryOutcomeIds.ItemStackKind,
                    source.StackId);
                GameplayEntityId itemEntity = source.ItemInstanceId.Length > 0
                    ? new GameplayEntityId(
                        TradeInventoryOutcomeIds.ItemInstanceKind,
                        source.ItemInstanceId)
                    : new GameplayEntityId(
                        TradeInventoryOutcomeIds.ItemDefinitionKind,
                        source.ItemDefinitionId);
                participants[participantCount++] = new GameplayOutcomeParticipant(
                    stackEntity,
                    TradeInventoryOutcomeIds.SourceRole,
                    GameplayParticipationKind.Direct,
                    true,
                    source.DisplayName);
                subjects[subjectCount++] = new GameplayOutcomeSubjectLink(
                    stackEntity,
                    0.35f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0);
                if (!HasParticipant(
                        participants,
                        participantCount,
                        itemEntity,
                        TradeInventoryOutcomeIds.ItemRole))
                {
                    participants[participantCount++] =
                        new GameplayOutcomeParticipant(
                            itemEntity,
                            TradeInventoryOutcomeIds.ItemRole,
                            GameplayParticipationKind.Direct,
                            true,
                            source.DisplayName);
                    subjects[subjectCount++] = new GameplayOutcomeSubjectLink(
                        itemEntity,
                        0.55f,
                        NarrativeMemoryTier.Recent,
                        false,
                        false,
                        0);
                }
                metrics[metricCount++] = new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.QuantityMetric,
                    source.Quantity,
                    TradeInventoryOutcomeIds.CountUnit,
                    stackEntity);
                metrics[metricCount++] = new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.MassMetric,
                    source.MassGrams,
                    TradeInventoryOutcomeIds.GramUnit,
                    stackEntity);
                metrics[metricCount++] = new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.SourceXMetric,
                    source.SourcePosition.x,
                    TradeInventoryOutcomeIds.CellUnit,
                    stackEntity);
                metrics[metricCount++] = new GameplayOutcomeMetric(
                    TradeInventoryOutcomeIds.SourceYMetric,
                    source.SourcePosition.y,
                    TradeInventoryOutcomeIds.CellUnit,
                    stackEntity);
                samePosition &= source.SourcePosition == firstPosition;
            }

            tags[0] = TradeInventoryOutcomeIds.TradeInventoryTag;
            tags[1] = TradeInventoryOutcomeIds.PhysicalTag;
            provenance[0] = TradeInventoryOutcomeIds.RuntimeReceiptProvenance;
            provenance[1] = new GameplayOutcomeProvenanceReference(
                "source-receipt",
                slug);
            provenance[2] = new GameplayOutcomeProvenanceReference(
                "domain-commit",
                exactReceipt.CommitId);
            facts[0] = new GameplayOutcomeFact(
                TradeInventoryOutcomeIds.ReasonCodeFact,
                exactReceipt.ReasonCode);
            facts[1] = new GameplayOutcomeFact(
                TradeInventoryOutcomeIds.RequestFingerprintFact,
                exactReceipt.RequestFingerprint);
            GameplayLocationReference location = samePosition
                ? new GameplayLocationReference(
                    "dungeon",
                    string.Empty,
                    firstPosition.x,
                    firstPosition.y)
                : default;
            TradeInventoryOutcomeReceipt receipt = new(
                TradeInventoryOutcomeKind.PhysicalItemBatchDisposition,
                exactReceipt.OperationId,
                expectedOwnerRevision,
                expectedOwnerRevision,
                Math.Max(1, calendar.Day),
                GameplayOutcomeStatus.Succeeded,
                participants,
                participantCount,
                metrics,
                metricCount,
                subjects,
                subjectCount,
                tags,
                2,
                provenance,
                3,
                facts,
                2,
                location);
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
        finally
        {
            ArrayPool<GameplayOutcomeParticipant>.Shared.Return(
                participants,
                clearArray: true);
            ArrayPool<GameplayOutcomeMetric>.Shared.Return(
                metrics,
                clearArray: true);
            ArrayPool<GameplayOutcomeSubjectLink>.Shared.Return(
                subjects,
                clearArray: true);
            ArrayPool<GameplayOutcomeTagId>.Shared.Return(tags, clearArray: true);
            ArrayPool<GameplayOutcomeProvenanceReference>.Shared.Return(
                provenance,
                clearArray: true);
            ArrayPool<GameplayOutcomeFact>.Shared.Return(facts, clearArray: true);
        }
    }

    private static bool HasParticipant(
        GameplayOutcomeParticipant[] participants,
        int count,
        GameplayEntityId entity,
        GameplayRoleId role)
    {
        for (int index = 0; index < count; index++)
            if (participants[index].EntityId.Equals(entity)
                && participants[index].RoleId.Equals(role))
                return true;
        return false;
    }

    private sealed class PreparedPhysicalDispositionOutcome :
        IPreparedPhysicalItemGameplayOutcome
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
