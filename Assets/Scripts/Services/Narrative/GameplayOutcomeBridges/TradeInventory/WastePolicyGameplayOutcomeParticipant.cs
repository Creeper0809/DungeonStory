using System;
using DungeonStory.Narrative.Korean;

public sealed class WastePolicyGameplayOutcomeParticipant :
    IWastePolicyGameplayOutcomeParticipant
{
    private readonly ITradeInventoryOutcomeCommitter committer;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameCalendar calendar;

    public WastePolicyGameplayOutcomeParticipant(
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
        in WastePolicyGameplayOutcomePreview preview,
        out IPreparedWastePolicyGameplayOutcome prepared,
        out string failureReason)
    {
        prepared = null;
        failureReason = string.Empty;
        if (!preview.IsValid)
        {
            failureReason = "waste-policy-outcome-preview-invalid";
            return false;
        }

        try
        {
            string origin = ((int)preview.Origin).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            string definitionId = "waste-policy-definition:" + origin;
            string instanceId = "waste-policy:" + origin;
            KoreanNameSnapshot displayName = TradeInventoryOutcomeNames.Snapshot(
                preview.DisplayName,
                "waste-policy:" + origin + ":v1");
            GameplayEntityId definition = new(
                TradeInventoryOutcomeIds.WastePolicyDefinitionKind,
                definitionId);
            GameplayEntityId instance = new(
                TradeInventoryOutcomeIds.WastePolicyInstanceKind,
                instanceId);
            GameplayEntityId operation = new(
                TradeInventoryOutcomeIds.OperationKind,
                preview.OperationId);
            string slug = TradeInventoryOutcomeIds.Slug(
                TradeInventoryOutcomeKind.WastePolicyCommand);
            GameplayOutcomeParticipant[] participants =
            {
                new(
                    instance,
                    TradeInventoryOutcomeIds.PolicyRole,
                    GameplayParticipationKind.Direct,
                    true,
                    displayName),
                new(
                    definition,
                    TradeInventoryOutcomeIds.PolicyRole,
                    GameplayParticipationKind.Direct,
                    false,
                    displayName)
            };
            GameplayOutcomeMetric[] metrics =
            {
                new(
                    TradeInventoryOutcomeIds.KindMetric,
                    (int)TradeInventoryOutcomeKind.WastePolicyCommand,
                    TradeInventoryOutcomeIds.EnumUnit,
                    new GameplayEntityId(
                        TradeInventoryOutcomeIds.OutcomeKind,
                        slug)),
                new(
                    TradeInventoryOutcomeIds.ResultRevisionMetric,
                    preview.OwnerRevision,
                    TradeInventoryOutcomeIds.RevisionUnit,
                    operation),
                new(
                    TradeInventoryOutcomeIds.BeforeDispositionMetric,
                    (int)preview.BeforeDisposition,
                    TradeInventoryOutcomeIds.EnumUnit,
                    instance),
                new(
                    TradeInventoryOutcomeIds.AfterDispositionMetric,
                    (int)preview.AfterDisposition,
                    TradeInventoryOutcomeIds.EnumUnit,
                    instance),
                new(
                    TradeInventoryOutcomeIds.BeforeEnabledMetric,
                    preview.BeforeEnabled ? 1d : 0d,
                    TradeInventoryOutcomeIds.EnumUnit,
                    instance),
                new(
                    TradeInventoryOutcomeIds.AfterEnabledMetric,
                    preview.AfterEnabled ? 1d : 0d,
                    TradeInventoryOutcomeIds.EnumUnit,
                    instance),
                new(
                    TradeInventoryOutcomeIds.BeforePercentMetric,
                    preview.BeforeMaximumFeedContamination,
                    TradeInventoryOutcomeIds.PercentUnit,
                    instance),
                new(
                    TradeInventoryOutcomeIds.AfterPercentMetric,
                    preview.AfterMaximumFeedContamination,
                    TradeInventoryOutcomeIds.PercentUnit,
                    instance)
            };
            GameplayOutcomeSubjectLink[] subjects =
            {
                new(
                    instance,
                    0.32f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0)
            };
            GameplayOutcomeTagId[] tags =
            {
                TradeInventoryOutcomeIds.TradeInventoryTag,
                TradeInventoryOutcomeIds.EconomyTag
            };
            GameplayOutcomeProvenanceReference[] provenance =
            {
                TradeInventoryOutcomeIds.RuntimeReceiptProvenance,
                new("source-receipt", slug),
                new("domain-commit", preview.OperationId)
            };
            TradeInventoryOutcomeReceipt receipt = new(
                TradeInventoryOutcomeKind.WastePolicyCommand,
                preview.OperationId,
                preview.OwnerRevision,
                preview.OwnerRevision,
                Math.Max(1, calendar.Day),
                GameplayOutcomeStatus.Succeeded,
                participants,
                metrics,
                subjects,
                tags,
                provenance,
                Array.Empty<GameplayOutcomeFact>());
            if (!committer.TryPrepare(
                    receipt,
                    out PreparedTradeInventoryOutcome tradePrepared,
                    out failureReason))
                return false;
            prepared = new PreparedWastePolicyOutcome(
                committer,
                diagnostics,
                tradePrepared);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failureReason = "waste-policy-outcome-prepare-invalid:"
                + exception.Message;
            return false;
        }
    }

    private sealed class PreparedWastePolicyOutcome :
        IPreparedWastePolicyGameplayOutcome
    {
        private readonly ITradeInventoryOutcomeCommitter committer;
        private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
        private readonly PreparedTradeInventoryOutcome prepared;
        private bool terminal;

        internal PreparedWastePolicyOutcome(
            ITradeInventoryOutcomeCommitter committer,
            IGameplayOutcomeDiagnosticsQuery diagnostics,
            in PreparedTradeInventoryOutcome prepared)
        {
            this.committer = committer;
            this.diagnostics = diagnostics;
            this.prepared = prepared;
        }

        public WastePolicyOutcomeKey ResultKey => new(
            prepared.ResultKey.ProducerId,
            prepared.ResultKey.OperationId.Value,
            prepared.ResultKey.CommitRevision,
            prepared.ResultKey.LocalResultIndex);
        public bool IsCanonicalReplay => prepared.IsCanonicalReplay;

        public bool TryCommit(
            long expectedOwnerRevision,
            out WastePolicyOutcomeAttachment attachment,
            out bool canonicalCommitted,
            out string failureReason)
        {
            attachment = default;
            canonicalCommitted = false;
            if (terminal)
            {
                failureReason = "waste-policy-outcome-already-terminal";
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
                failureReason = "waste-policy-outcome-commit-exception:"
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
                            "waste-policy-outcome-canonical-identity-missing";
                    return false;
                }
                attachment = new WastePolicyOutcomeAttachment(
                    new WastePolicyOutcomeKey(
                        identity.ResultKey.ProducerId,
                        identity.ResultKey.OperationId.Value,
                        identity.ResultKey.CommitRevision,
                        identity.ResultKey.LocalResultIndex),
                    identity.OutcomeId.RunId.Value,
                    identity.OutcomeId.Sequence,
                    (int)identity.State,
                    identity.State is >= GameplayOutcomeReplayState
                            .PublishedAcknowledged
                        and <= GameplayOutcomeReplayState.Forgotten,
                    identity.CanonicalPayloadHash);
                if (!attachment.IsValid)
                {
                    failureReason =
                        "waste-policy-outcome-canonical-identity-invalid";
                    return false;
                }
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                failureReason =
                    "waste-policy-outcome-canonical-identity-pending:"
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
