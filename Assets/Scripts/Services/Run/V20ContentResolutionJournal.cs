using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;
using static V20ContentEffectEvidence;
using EffectCommitPlan = V20ContentEffectTransaction.EffectCommitPlan;
using RetirementSchedulePlan = V20RetirementSchedulePlanner.RetirementSchedulePlan;

internal sealed class V20ContentResolutionJournal
{
    private readonly V20CampaignRuntime live;
    private readonly ICareerPersistence careers;
    private readonly IPhysicalItemExactSourcePublicationService exactSources;
    private readonly IWorldItemStackRuntime items;
    private readonly IGameMoneyAccount money;
    private readonly ISocialRitualFactPublisher factPublisher;
    private readonly V20ContentTypedEffectRuntime typedEffects;
    private readonly V20ContentEffectRollback effectRollback;
    private readonly V20RetirementSchedulePlanner retirementPlanner;

    internal V20ContentResolutionJournal(
        V20CampaignRuntime live,
        ICareerPersistence careers,
        IPhysicalItemExactSourcePublicationService exactSources,
        IWorldItemStackRuntime items,
        IGameMoneyAccount money,
        ISocialRitualFactPublisher factPublisher,
        V20ContentTypedEffectRuntime typedEffects,
        V20ContentEffectRollback effectRollback,
        V20RetirementSchedulePlanner retirementPlanner)
    {
        this.live = live;
        this.careers = careers;
        this.exactSources = exactSources;
        this.items = items;
        this.money = money;
        this.factPublisher = factPublisher;
        this.typedEffects = typedEffects;
        this.effectRollback = effectRollback;
        this.retirementPlanner = retirementPlanner;
    }

    internal static V20ContentResolutionRecoverySaveData CreateRecovery(
        ContentResolutionRequest request,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        Vector2Int grantDropoff,
        V20ContentResolutionRecoveryPhase phase,
        V20ContentEffectCommitReferences childCommits,
        string stateBeforeFingerprint,
        string stateAfterFingerprint) => new()
    {
        actionId = request.ActionId.Trim(),
        absoluteDay = Math.Max(1, request.AbsoluteDay),
        requestKind = request.Kind,
        phase = phase,
        grantDropoffX = grantDropoff.x,
        grantDropoffY = grantDropoff.y,
        childCommitEvidenceVersion = 2,
        moneyTransactionId = childCommits?.MoneyTransactionId ?? string.Empty,
        itemConsumptionOperationId = childCommits
            ?.ItemConsumptionOperationId ?? string.Empty,
        itemGrantBatchCommitId = childCommits?.ItemGrantBatchCommitId
            ?? string.Empty,
        itemConsumptionCommitId = childCommits?.ItemConsumptionCommitId ?? string.Empty,
        itemGrantReceiptId = childCommits?.ItemGrantReceiptId ?? string.Empty,
        durableEquipmentEvidenceVersion = string.IsNullOrWhiteSpace(
            childCommits?.DurableEquipmentContext)
                ? 0
                : 1,
        durableEquipmentStackId = childCommits?.DurableEquipmentStackId
            ?? string.Empty,
        durableEquipmentItemId = childCommits?.DurableEquipmentItemId
            ?? string.Empty,
        durableEquipmentContext = childCommits?.DurableEquipmentContext
            ?? string.Empty,
        outcomeEvidenceVersion = 1,
        stateBeforeFingerprint = stateBeforeFingerprint ?? string.Empty,
        stateAfterFingerprint = stateAfterFingerprint ?? string.Empty,
        resolutions = (resolutions ?? Array.Empty<V20ResolvedEventResult>())
            .Select(ToRecovery)
            .ToList()
    };

    internal static V20ResolvedEventRecoverySaveData ToRecovery(
        V20ResolvedEventResult value) => new()
    {
        definitionId = value.DefinitionId,
        resolutionId = value.ResolutionId,
        effects = value.Effects.Select(effect => new V20ContentEffect
        {
            kind = effect.kind,
            targetId = effect.targetId,
            amount = effect.amount,
            durationDays = effect.durationDays
        }).ToList(),
        participantCharacterIds = value.ParticipantCharacterIds.ToList(),
        contextFactionId = value.ContextFactionId,
        instanceId = value.InstanceId,
        displayName = value.DisplayName,
        riskTier = value.RiskTier,
        riskReason = value.RiskReason,
        terminalActionLabel = value.TerminalActionLabel,
        terminalOutcomeNarrative = value.TerminalOutcomeNarrative,
        terminalEffectKinds = value.TerminalEffectKinds.ToList(),
        observedCause = value.ObservedCause,
        occurrenceInstanceId = value.OccurrenceInstanceId,
        applications = value.Applications
            .Select(application => application.Clone())
            .ToList()
    };

    internal static V20ResolvedEventResult FromRecovery(
        V20ResolvedEventRecoverySaveData value) => new(
        value.definitionId,
        value.resolutionId,
        (value.effects ?? new List<V20ContentEffect>()).Select(effect =>
            new V20ContentEffect
            {
                kind = effect.kind,
                targetId = effect.targetId,
                amount = effect.amount,
                durationDays = effect.durationDays
            }).ToArray(),
        value.participantCharacterIds?.ToArray() ?? Array.Empty<string>(),
        value.contextFactionId,
        value.instanceId,
        value.displayName,
        value.riskTier,
        value.riskReason,
        value.terminalActionLabel,
        value.terminalOutcomeNarrative,
        value.terminalEffectKinds?.ToArray()
            ?? Array.Empty<V20ContentEffectKind>(),
        value.observedCause,
        value.occurrenceInstanceId,
        value.applications?.Select(application => application?.Clone())
            .Where(application => application != null)
            .ToArray()
            ?? Array.Empty<V20EffectApplicationEvidence>());

    internal static void UpsertRecovery(
        RunMilestoneAggregateState milestones,
        V20ContentResolutionRecoverySaveData recovery)
    {
        milestones.Data.pendingContentResolutions.RemoveAll(value =>
            value != null && string.Equals(
                value.actionId,
                recovery.actionId,
                StringComparison.Ordinal));
        milestones.Data.pendingContentResolutions.Add(recovery);
        milestones.Data.pendingContentResolutions = milestones.Data
            .pendingContentResolutions
            .OrderBy(value => value.actionId, StringComparer.Ordinal)
            .ToList();
    }

    internal bool TryRecoverPendingContentResolutions(
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        RunMilestoneAggregateState milestones = live.PrepareMilestones(
            live.CaptureMilestones());
        V20ContentResolutionRecoverySaveData[] pending = milestones.Data
            .pendingContentResolutions
            .OrderBy(value => value.actionId, StringComparer.Ordinal)
            .ToArray();
        foreach (V20ContentResolutionRecoverySaveData row in pending)
        {
            V20ContentResolutionRecoverySaveData current = row;
            string actionId = current.actionId;
            V20ResolvedEventResult[] resolved = current.resolutions
                .Select(FromRecovery)
                .ToArray();
            V20ContentEffectCommitReferences childCommits = V20ContentEffectCommitReferences.From(
                current);
            if (!TryValidateRecoveredChildCommits(
                    current,
                    resolved,
                    out failure))
            {
                return false;
            }
            if (current.phase == V20ContentResolutionRecoveryPhase.ItemCostsCommitted)
            {
                EffectCommitPlan plan = new(current.actionId)
                {
                    Dropoff = new Vector2Int(
                        current.grantDropoffX,
                        current.grantDropoffY)
                };
                plan.Grants.AddRange(resolved
                    .SelectMany(value => value.Effects)
                    .Where(value => value.kind
                        == V20ContentEffectKind.ItemGrant));
                if (plan.Grants.Count > 0)
                {
                    if (!exactSources.TryPrepare(plan.CreateGrantPublicationPlan(),
                            out PhysicalItemExactSourcePublicationTransaction transaction, out string prepareFailure))
                    {
                        failure = new DomainFailure(FailureCode.ProductionOutputUnavailable, prepareFailure);
                        return false;
                    }
                    if (transaction.BatchCommitId != childCommits.ItemGrantBatchCommitId)
                        throw new InvalidOperationException("Recovered grant changed its batch identity.");
                    if (!exactSources.TryCommitReleased(transaction, plan.Dropoff, "content-resolution-grant",
                            out PhysicalItemExactSourcePublicationReceipt receipt, out string commitFailure))
                    {
                        bool rolledBack = exactSources.TryRollback(transaction, "content-grant-retry-failed",
                            out string rollbackFailure);
                        failure = new DomainFailure(FailureCode.ProductionOutputUnavailable,
                            commitFailure + (rolledBack ? "; uncommitted grant retired" : "; forward retry required: " + rollbackFailure));
                        return false;
                    }
                    if (receipt.BatchCommitId != childCommits.ItemGrantBatchCommitId
                        || string.IsNullOrWhiteSpace(receipt.RecoveryCommitId))
                        throw new InvalidOperationException("Committed grant is missing its exact durable receipt.");
                    current.itemGrantReceiptId = receipt.RecoveryCommitId;
                    childCommits.ItemGrantReceiptId = receipt.RecoveryCommitId;
                }
                current.phase =
                    V20ContentResolutionRecoveryPhase.ItemGrantsCommitted;
                live.PublishMilestones(milestones);
                milestones = live.PrepareMilestones(live.CaptureMilestones());
                current = milestones.Data.pendingContentResolutions.Single(value =>
                    string.Equals(
                        value.actionId,
                        actionId,
                        StringComparison.Ordinal));
            }

            if (current.phase == V20ContentResolutionRecoveryPhase.ItemGrantsCommitted)
            {
                V20ContentEffectOwnerSnapshot before = effectRollback.CaptureEffectOwners(resolved);
                string recoveryBeforeFingerprint =
                    current.outcomeEvidenceVersion == 1
                    && !string.IsNullOrWhiteSpace(
                        current.stateBeforeFingerprint)
                        ? current.stateBeforeFingerprint
                        : CaptureEffectOwnerFingerprint(before);
                CharacterCareerAggregate latestCareer = null;
                try
                {
                    ContentResolutionRequest recoveryRequest = new()
                    {
                        ActionId = current.actionId,
                        AbsoluteDay = current.absoluteDay,
                        Kind = current.requestKind
                    };
                    if (!retirementPlanner.TryPrepareRetirementSchedule(
                            recoveryRequest,
                            resolved,
                            out RetirementSchedulePlan retirementPlan,
                            out DomainFailure retirementFailure))
                    {
                        failure = retirementFailure;
                        return false;
                    }
                    typedEffects.ApplyTypedDomainEffects(current.absoluteDay, resolved);
                    if (retirementPlan.IsRequired)
                    {
                        latestCareer = careers.PrepareRestore(careers.Capture());
                        string retirementFailureReason =
                            "character is no longer an eligible living elder";
                        if (!retirementPlanner.IsEligibleLivingElder(retirementPlan.CharacterId)
                            || !latestCareer.TryScheduleRetirement(
                                retirementPlan.CharacterId,
                                retirementPlan.EventId,
                                retirementPlan.ChoiceId,
                                retirementPlan.DecisionAbsoluteDay,
                                retirementPlan.DueAbsoluteDay,
                                out retirementFailureReason))
                        {
                            throw new InvalidOperationException(
                                "Retirement recovery could not schedule the frozen decision: "
                                + retirementFailureReason);
                        }
                    }
                }
                catch
                {
                    effectRollback.RestoreTypedEffectOwners(before);
                    failure = new DomainFailure(
                        FailureCode.ExternalInfluenceUnavailable,
                        "content-typed-effect-recovery-failed");
                    return false;
                }
                current.phase =
                    V20ContentResolutionRecoveryPhase.TypedEffectsApplied;
                current.outcomeEvidenceVersion = 1;
                current.stateBeforeFingerprint = recoveryBeforeFingerprint;
                V20ContentEffectOwnerSnapshot after = effectRollback.CaptureEffectOwners(resolved);
                after.Milestones = milestones.Data;
                if (latestCareer != null)
                    after.Careers = latestCareer.CaptureWorld();
                resolved = AttachApplicationEvidence(
                    resolved,
                    before,
                    after,
                    childCommits).ToArray();
                current.resolutions = resolved.Select(ToRecovery).ToList();
                current.stateAfterFingerprint =
                    CaptureEffectOwnerFingerprint(after);
                if (latestCareer != null)
                {
                    live.PublishContentResolution(
                        live.PrepareSeasonal(live.CaptureSeasonal()),
                        live.PrepareSociety(live.CaptureSociety()),
                        live.PrepareFactions(live.CaptureFactions()),
                        milestones,
                        latestCareer);
                }
                else
                {
                    live.PublishMilestones(milestones);
                }
                milestones = live.PrepareMilestones(live.CaptureMilestones());
                current = milestones.Data.pendingContentResolutions.Single(value =>
                    string.Equals(
                        value.actionId,
                        actionId,
                        StringComparison.Ordinal));
            }

            if (current.phase == V20ContentResolutionRecoveryPhase.TypedEffectsApplied)
            {
                try
                {
                    PublishResolvedFacts(
                        current.actionId,
                        current.absoluteDay,
                        resolved,
                        childCommits,
                        string.IsNullOrWhiteSpace(
                            current.stateBeforeFingerprint)
                            ? "legacy-unavailable"
                            : current.stateBeforeFingerprint,
                        string.IsNullOrWhiteSpace(
                            current.stateAfterFingerprint)
                            ? CaptureEffectOwnerFingerprint(
                                effectRollback.CaptureEffectOwners(resolved))
                            : current.stateAfterFingerprint);
                }
                catch
                {
                    failure = new DomainFailure(
                        FailureCode.ExternalInfluenceUnavailable,
                        "content-outcome-recovery-failed");
                    return false;
                }
                current.phase =
                    V20ContentResolutionRecoveryPhase.OutcomesCommitted;
                live.PublishMilestones(milestones);
                milestones = live.PrepareMilestones(live.CaptureMilestones());
                current = milestones.Data.pendingContentResolutions.Single(value =>
                    string.Equals(
                        value.actionId,
                        actionId,
                        StringComparison.Ordinal));
            }

            if (current.phase == V20ContentResolutionRecoveryPhase.OutcomesCommitted)
            {
                RemoveContentResolutionRecovery(current.actionId);
                milestones = live.PrepareMilestones(live.CaptureMilestones());
            }
        }
        return true;
    }

    private bool TryValidateRecoveredChildCommits(
        V20ContentResolutionRecoverySaveData recovery,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        try { V20ContentRecoveryEvidence.RequirePhysicalReceipts(recovery, items as IPhysicalEffectReceiptQuery); }
        catch (InvalidOperationException error)
        {
            failure = new DomainFailure(FailureCode.ItemTransferConsumptionFailed, error.Message);
            return false;
        }
        int moneyDelta = (resolutions ?? Array.Empty<V20ResolvedEventResult>())
            .SelectMany(value => value.Effects)
            .Where(value => value != null
                && value.IsValid
                && value.kind == V20ContentEffectKind.Money)
            .Sum(value => Mathf.RoundToInt(value.amount));
        if (moneyDelta == 0)
            return true;

        EconomyTransactionKind kind = moneyDelta > 0
            ? EconomyTransactionKind.ContractIncome
            : EconomyTransactionKind.LegacyExpense;
        string sourceId = "content:" + recovery.actionId;
        if (money is not IGameMoneyTransactionQuery query
            || !query.TryGetSuccessfulTransaction(
                kind,
                sourceId,
                out EconomyTransactionRecord record)
            || record == null
            || record.amount != moneyDelta
            || !string.Equals(
                record.transactionId,
                recovery.moneyTransactionId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "content-money-recovery-evidence-missing");
            return false;
        }
        return true;
    }

    internal void AdvanceContentResolutionRecovery(
        string actionId,
        V20ContentResolutionRecoveryPhase phase)
    {
        RunMilestoneAggregateState milestones = live.PrepareMilestones(
            live.CaptureMilestones());
        V20ContentResolutionRecoverySaveData recovery = milestones.Data
            .pendingContentResolutions.Single(value => value != null
                && string.Equals(
                    value.actionId,
                    actionId?.Trim(),
                    StringComparison.Ordinal));
        recovery.phase = phase;
        live.PublishMilestones(milestones);
    }

    internal void RemoveContentResolutionRecovery(string actionId)
    {
        RunMilestoneAggregateState previous = live.PrepareMilestones(live.CaptureMilestones());
        RunMilestoneAggregateState next = live.PrepareMilestones(live.CaptureMilestones());
        var row = next.Data.pendingContentResolutions.Single(value => value.actionId == actionId);
        if (row.phase != V20ContentResolutionRecoveryPhase.OutcomesCommitted)
            throw new InvalidOperationException("Cannot retire physical receipts before parent outcome completion.");
        V20ContentRecoveryEvidence.RequirePhysicalReceipts(row, items as IPhysicalEffectReceiptQuery);
        next.Data.pendingContentResolutions.Remove(row);
        string[] receipts = new[] { row.itemConsumptionCommitId, row.itemGrantReceiptId }
            .Where(value => !string.IsNullOrEmpty(value)).ToArray();
        if (receipts.Length == 0) { live.PublishMilestones(next); return; }
        var acknowledgements = items as IPhysicalEffectReceiptAcknowledgement
            ?? throw new InvalidOperationException("Physical receipt acknowledgement authority is missing.");
        if (!acknowledgements.AcknowledgePhysicalEffectReceipts(receipts,
                new RecoveryCompletion(live, previous, next), out string reason))
            throw new InvalidOperationException("Content recovery joint acknowledgement failed: " + reason);
    }

    internal void PublishResolvedFacts(
        string operationId,
        int absoluteDay,
        IReadOnlyList<V20ResolvedEventResult> resolutions,
        V20ContentEffectCommitReferences childCommits,
        string stateBeforeFingerprint,
        string stateAfterFingerprint,
        DomainFactEmission sourceFact = null)
    {
        var concrete = (resolutions ?? Array.Empty<V20ResolvedEventResult>()).ToArray();
        var leases = new List<DomainFactLease>();
        var emissions = new List<DomainFactEmission>();
        try
        {
            for (int index = 0; index < concrete.Length; index++)
            {
                var fact = new ContentResolutionFact(new ContentResolutionFactData
                {
                    operationId = operationId.Trim(), absoluteDay = Math.Max(1, absoluteDay),
                    resolutionIndex = index, resolution = ToRecovery(concrete[index]),
                    moneyTransactionId = childCommits?.MoneyTransactionId ?? string.Empty,
                    itemConsumptionOperationId = childCommits?.ItemConsumptionOperationId ?? string.Empty,
                    itemGrantBatchCommitId = childCommits?.ItemGrantBatchCommitId ?? string.Empty,
                    durableEquipmentStackId = childCommits?.DurableEquipmentStackId ?? string.Empty,
                    durableEquipmentItemId = childCommits?.DurableEquipmentItemId ?? string.Empty,
                    durableEquipmentContext = childCommits?.DurableEquipmentContext ?? string.Empty,
                    stateBeforeFingerprint = stateBeforeFingerprint ?? string.Empty,
                    stateAfterFingerprint = stateAfterFingerprint ?? string.Empty
                });
                if (!factPublisher.TryReserve(fact, out var lease, out string failure))
                    throw new ContentResolutionFactRejectedException("content-resolution-fact-reservation:" + failure);
                leases.Add(lease);
                emissions.Add(DomainFactEmission.Create(lease, fact));
            }
            if (sourceFact != null) emissions.Add(sourceFact);
            if (emissions.Count == 0) return;
            var results = new DomainFactDelivery[emissions.Count];
            if (!factPublisher.PublishBatch(emissions, results, out string detail)
                || results.Any(value => !value.MayBeCommitted))
                throw new ContentResolutionFactRejectedException("content-resolution-fact-publication:" + detail);
            // Uncertain acknowledgement keeps the effects. Recovery replays only frozen facts.
        }
        catch
        {
            foreach (var lease in leases) factPublisher.Cancel(lease);
            if (sourceFact != null) factPublisher.Cancel(sourceFact.Lease);
            throw;
        }
    }

    private sealed class RecoveryCompletion : IPhysicalItemDispositionAcknowledgementParticipant
    {
        private readonly V20CampaignRuntime owner;
        private readonly RunMilestoneAggregateState previous;
        private readonly RunMilestoneAggregateState next;
        public RecoveryCompletion(V20CampaignRuntime owner, RunMilestoneAggregateState previous, RunMilestoneAggregateState next)
        { this.owner = owner; this.previous = previous; this.next = next; }
        public bool TryCommit(out string failureReason)
        { failureReason = string.Empty; owner.PublishMilestones(next); return true; }
        public void Rollback() => owner.PublishMilestones(previous);
    }
}
