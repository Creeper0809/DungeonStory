using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

// 물리 배송, 보상 게시와 ACK 사이의 계약 영수증 수명을 소유한다.
internal sealed class V20FactionContractDeliveryRuntime
{
    private readonly V20CampaignRuntime live;
    private readonly V20StoryContentCatalog catalog;
    private readonly IContentRequirementEvaluator requirements;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService physicalDispositions;
    private readonly IPhysicalFacilityItemBatchTransferGateway factionContractTransfers;
    private readonly IEconomyProjectInputOwnerPort factionContractInputOwners;
    private readonly V20FactionContractQuery factionContractQuery;

    internal V20FactionContractDeliveryRuntime(V20CampaignRuntime live, V20StoryContentCatalog catalog,
        IContentRequirementEvaluator requirements, IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService physicalDispositions,
        IPhysicalFacilityItemBatchTransferGateway factionContractTransfers,
        IEconomyProjectInputOwnerPort factionContractInputOwners, V20FactionContractQuery factionContractQuery)
    {
        this.live = live ?? throw new ArgumentNullException(nameof(live));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        this.items = items;
        this.physicalDispositions = physicalDispositions;
        this.factionContractTransfers = factionContractTransfers;
        this.factionContractInputOwners = factionContractInputOwners;
        this.factionContractQuery = factionContractQuery ?? throw new ArgumentNullException(nameof(factionContractQuery));
    }

    public bool RequiresAdvance => live.CaptureFactions().factions
        .Where(value => value != null)
        .Any(value => FactionContractDeliveryOutbox.HasPending(value)
            || value.deliveryTerminalCleanupPending
            || (!string.IsNullOrWhiteSpace(value.activeContractId)
                && catalog.Contracts.Any(contract =>
                    contract != null
                    && string.Equals(
                        contract.StableId,
                        value.activeContractId,
                        StringComparison.Ordinal)
                    && FactionContractMaterialRules.HasMaterialRequirements(
                        contract))));

    internal void ValidateBeforeCapture()
    {
        FactionCampaignStateSaveData[] states = live.CaptureFactions().factions
            .Where(value => value != null)
            .ToArray();
        Dictionary<string, FactionCampaignStateSaveData> campaignPending = states
            .Where(FactionContractDeliveryOutbox.HasPending)
            .ToDictionary(
                value => value.deliveryOperationId,
                value => value,
                StringComparer.Ordinal);

        foreach (FactionCampaignStateSaveData state in campaignPending.Values)
        {
            if (!FactionContractDeliveryOutbox.HasCanonicalPending(state)
                || !factionContractTransfers.TryGetPending(
                    state.deliveryOperationId,
                    out PhysicalItemBatchDispositionReceipt physical)
                || !FactionContractDeliveryOutbox.ReceiptMatchesSaved(
                    state,
                    physical))
            {
                throw new InvalidOperationException(
                    "Faction-contract save capture crossed a campaign/physical "
                    + $"receipt transition for '{state.factionId}'.");
            }
        }

        foreach (FactionCampaignStateSaveData state in states.Where(value =>
                     !string.IsNullOrEmpty(value.activeContractOccurrenceId)
                     && !string.IsNullOrEmpty(value.activeContractId)))
        {
            FactionContractDefinitionSO contract = catalog.Contracts
                .FirstOrDefault(value => value != null
                    && string.Equals(
                        value.StableId,
                        state.activeContractId,
                        StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(value.seasonalEventId));
            if (contract == null
                || !FactionContractMaterialRules.HasMaterialRequirements(contract))
                continue;
            string operationId = FactionContractDeliveryOutbox.FormatOperationId(
                contract.StableId,
                state.activeContractOccurrenceId);
            if (factionContractTransfers.TryGetPending(operationId, out _)
                && !campaignPending.ContainsKey(operationId))
            {
                throw new InvalidOperationException(
                    "Faction-contract save capture crossed the physical-commit/"
                    + $"campaign-record transition for seasonal occurrence "
                    + $"'{state.activeContractOccurrenceId}'.");
            }
        }

        foreach (FactionContractDefinitionSO contract in catalog.Contracts
                     .Where(value => string.IsNullOrEmpty(value.seasonalEventId))
                     .Where(FactionContractMaterialRules.HasMaterialRequirements))
        {
            string operationId = FactionContractDeliveryOutbox.FormatOperationId(
                contract.StableId);
            if (factionContractTransfers.TryGetPending(operationId, out _)
                && !campaignPending.ContainsKey(operationId))
            {
                throw new InvalidOperationException(
                    "Faction-contract save capture crossed the physical-commit/"
                    + $"campaign-record transition for '{contract.StableId}'.");
            }
        }

    }

    public void AdvanceFactionContractDeliveries(
        int absoluteDay,
        RunMilestoneEvaluationSnapshot requirements,
        Func<ContentResolutionRequest, (bool Succeeded, DomainFailure Failure)> resolveOutcome)
    {
        if (requirements == null)
            return;
        foreach (FactionCampaignStateSaveData snapshot in
                 live.CaptureFactions().factions
                     .Where(value => value != null)
                     .OrderBy(value => value.factionId, StringComparer.Ordinal))
        {
            AdvanceFactionContractDelivery(
                snapshot.factionId,
                Math.Max(1, absoluteDay),
                requirements, resolveOutcome);
        }
    }

    private void AdvanceFactionContractDelivery(
        string factionId,
        int absoluteDay,
        RunMilestoneEvaluationSnapshot requirements,
        Func<ContentResolutionRequest, (bool Succeeded, DomainFailure Failure)> resolveOutcome)
    {
        if (!live.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData state))
            return;

        if (state.deliveryTerminalCleanupPending)
        {
            if (!TryRetireFactionContractInputOwner(
                    state,
                    out string retireFailure))
            {
                PublishDeliveryFailure(factionId, retireFailure);
                return;
            }
            state = RequireLiveFaction(factionId);
        }

        if (state.deliveryCommitPhase ==
            FactionContractDeliveryCommitPhase.RewardPublished)
        {
            if (!TryGetMatchingPendingReceipt(
                    state,
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out string receiptFailure))
            {
                PublishDeliveryFailure(factionId, receiptFailure);
                return;
            }
            if (!factionContractTransfers.Acknowledge(
                    receipt.CommitId,
                    out string acknowledgementFailure))
            {
                PublishDeliveryFailure(factionId, acknowledgementFailure);
                return;
            }
            PublishFactionMutation(
                factionId,
                (V20CampaignRuntime candidate, out string mutationFailure) =>
                    ((IFactionCampaignDeliveryCommand)candidate)
                        .TryClearDeliveryOutbox(
                            factionId,
                            out mutationFailure),
                "faction-contract-outbox-clear");
            return;
        }

        if (state.deliveryCommitPhase ==
            FactionContractDeliveryCommitPhase.PhysicalCommitted)
        {
            ContentResolutionRequest outcome = new()
            {
                ActionId = V21ContentAlertActionIds.FactionContractOutcome(
                    factionId,
                    succeeded: true),
                Kind = ContentResolutionRequestKind.FactionContractOutcome,
                FactionId = factionId,
                ContractSucceeded = true,
                AbsoluteDay = absoluteDay,
                Requirements = requirements
            };
            var result = resolveOutcome(outcome);
            if (!result.Succeeded)
                PublishDeliveryFailure(factionId, result.Failure.Code.ToString());
            return;
        }

        if (string.IsNullOrWhiteSpace(state.activeContractId))
            return;
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(
                value.StableId,
                state.activeContractId,
                StringComparison.Ordinal));
        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        if (material.Count == 0)
            return;
        if (!state.activeContractInputOwnerActive
            || string.IsNullOrWhiteSpace(state.activeContractDestinationId))
        {
            PublishDeliveryFailure(
                factionId,
                "faction-contract-input-owner-inactive");
            return;
        }

        string operationId = FactionContractDeliveryOutbox.FormatOperationId(
            contract.StableId,
            state.activeContractOccurrenceId);
        if (factionContractTransfers.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt existingReceipt))
        {
            PublishFactionMutation(
                factionId,
                (V20CampaignRuntime candidate, out string mutationFailure) =>
                    ((IFactionCampaignDeliveryCommand)candidate)
                        .TryRecordDeliveryReceipt(
                            factionId,
                            FactionContractDeliveryReceipt.FromPhysical(
                                existingReceipt),
                            out mutationFailure),
                "faction-contract-existing-receipt-record");
            return;
        }

        Vector2Int destination = new(
            state.activeContractDestinationX,
            state.activeContractDestinationY);
        bool requestFailed = false;
        string requestFailure = string.Empty;
        foreach (KeyValuePair<string, int> required in material)
        {
            int assigned = factionContractQuery.CountFactionContractQuantity(
                state.activeContractDestinationId,
                required.Key,
                stack => stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                    or WorldItemStackState.FacilityOutputBuffer
                    or WorldItemStackState.Carried
                    or WorldItemStackState.InTransit
                    or WorldItemStackState.FacilityBuffer);
            int missing = Math.Max(0, required.Value - assigned);
            if (missing <= 0)
                continue;
            if (!items.TryRequestItemDelivery(
                    required.Key,
                    missing,
                    destination,
                    state.activeContractDestinationId,
                    out int requested,
                    out requestFailure)
                || requested != missing)
            {
                requestFailed = true;
                if (string.IsNullOrWhiteSpace(requestFailure))
                {
                    requestFailure =
                        $"faction-contract-delivery-request-partial:"
                        + $"{required.Key}:{requested}/{missing}";
                }
                break;
            }
        }
        if (requestFailed)
        {
            PublishDeliveryFailure(factionId, requestFailure);
            return;
        }

        bool allArrived = material.All(required =>
            factionContractQuery.CountFactionContractQuantity(
                state.activeContractDestinationId,
                required.Key,
                stack => stack.State == WorldItemStackState.FacilityBuffer)
            >= required.Value);
        if (!allArrived)
        {
            PublishDeliveryFailure(factionId, string.Empty);
            return;
        }

        if (absoluteDay > state.activeContractDeadlineAbsoluteDay)
        {
            PublishDeliveryFailure(
                factionId,
                "faction-contract-deadline-expired-before-transfer");
            return;
        }
        if (!this.requirements.TryEvaluate(
                FactionContractMaterialRules.BuildNonMaterialRequirements(
                    contract),
                requirements,
                Array.Empty<string>(),
                out DomainFailure eligibilityFailure))
        {
            PublishDeliveryFailure(
                factionId,
                "faction-contract-non-material-condition:"
                + eligibilityFailure.Code);
            return;
        }

        if (!factionContractTransfers.TryCommitTransferPending(
                state.activeContractDestinationId,
                material,
                operationId,
                FactionContractDeliveryOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt transferReceipt,
                out string transferFailure))
        {
            PublishDeliveryFailure(factionId, transferFailure);
            return;
        }
        PublishFactionMutation(
            factionId,
            (V20CampaignRuntime candidate, out string mutationFailure) =>
                ((IFactionCampaignDeliveryCommand)candidate)
                    .TryRecordDeliveryReceipt(
                        factionId,
                        FactionContractDeliveryReceipt.FromPhysical(
                            transferReceipt),
                        out mutationFailure),
            "faction-contract-receipt-record");
    }

    internal bool TryEnsureAcceptedMaterialInputOwner(
        V20CampaignRuntime candidate,
        string factionId,
        out bool ensured,
        out string destinationId,
        out DomainFailure failure)
    {
        ensured = false;
        destinationId = string.Empty;
        failure = DomainFailure.None;
        if (!candidate.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData state))
            return true;
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(
                value.StableId,
                state.activeContractId,
                StringComparison.Ordinal));
        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        if (material.Count == 0)
            return true;

        destinationId = state.activeContractDestinationId;
        if (!factionContractInputOwners.TryEnsure(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                state.activeContractDestinationOwnerId,
                destinationId,
                new Vector2Int(
                    state.activeContractDestinationX,
                    state.activeContractDestinationY),
                EconomyProjectInputOwnerAnchorKind.ReservedTarget,
                string.Empty,
                material,
                0L,
                0L,
                string.Empty,
                out EconomyProjectInputOwnerProjection projection,
                out string ownerFailure))
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable);
            return false;
        }
        ensured = true;
        if (!((IFactionCampaignDeliveryCommand)candidate)
                .TrySetInputOwnerProjection(
                    factionId,
                    projection.CapacityGrams,
                    projection.MassAuthorityRevision,
                    projection.Fingerprint,
                    out string projectionFailure))
        {
            CompensateAcceptedMaterialOwnerOrThrow(
                true,
                destinationId,
                projectionFailure);
            ensured = false;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable);
            return false;
        }
        return true;
    }

    internal void CompensateAcceptedMaterialOwnerOrThrow(
        bool ensured,
        string destinationId,
        string reason)
    {
        if (!ensured)
            return;
        if (!factionContractInputOwners.TryRetireDestination(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                destinationId,
                EconomyProjectInputOwnerAuthority.FactionContractTerminalReason,
                out string failureReason))
        {
            throw new InvalidOperationException(
                $"Faction-contract accept failed ({reason}) and input-owner compensation failed: "
                + failureReason);
        }
    }

    internal bool TryValidateLiveFactionDeliveryReceipt(
        string factionId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!live.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData state))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(
                value.StableId,
                state.activeContractId,
                StringComparison.Ordinal));
        if (!FactionContractMaterialRules.HasMaterialRequirements(contract))
            return true;
        if (!TryGetMatchingPendingReceipt(state, out _, out _))
        {
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }
        return true;
    }

    private bool TryGetMatchingPendingReceipt(
        FactionCampaignStateSaveData state,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (!FactionContractDeliveryOutbox.HasCanonicalPending(state)
            || !factionContractTransfers.TryGetPending(
                state.deliveryOperationId,
                out receipt)
            || !FactionContractDeliveryOutbox.ReceiptMatchesSaved(
                state,
                receipt))
        {
            failureReason =
                "faction-contract-delivery-receipt-missing-or-mismatch";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private bool TryRetireFactionContractInputOwner(
        FactionCampaignStateSaveData state,
        out string failureReason)
    {
        if (state == null || !state.activeContractInputOwnerActive)
        {
            failureReason = string.Empty;
            return true;
        }
        if (!factionContractInputOwners.TryRetireDestination(
                EconomyProjectInputOwnerAuthority.FactionContractDomain,
                state.activeContractDestinationId,
                EconomyProjectInputOwnerAuthority.FactionContractTerminalReason,
                out failureReason))
            return false;
        PublishFactionMutation(
            state.factionId,
            (V20CampaignRuntime candidate, out string mutationFailure) =>
                ((IFactionCampaignDeliveryCommand)candidate)
                    .TryMarkInputOwnerRetired(
                        state.factionId,
                        out mutationFailure),
            "faction-contract-input-owner-retire");
        return true;
    }

    private delegate bool FactionCandidateMutation(
        V20CampaignRuntime candidate,
        out string failure);

    private void PublishFactionMutation(
        string factionId,
        FactionCandidateMutation mutation,
        string operation)
    {
        V20CampaignRuntime candidate = live.CreateContentResolutionCandidate(physicalDispositions);
        if (!mutation(candidate, out string failure))
            throw new InvalidOperationException(
                $"{operation} rejected faction '{factionId}': {failure}");
        live.PublishFactions(live.PrepareFactions(candidate.CaptureFactions()));
    }

    private void PublishDeliveryFailure(string factionId, string reason)
    {
        FactionCampaignStateSaveData state = RequireLiveFaction(factionId);
        string canonical = reason?.Trim() ?? string.Empty;
        if (string.Equals(
                state.deliveryFailureReason,
                canonical,
                StringComparison.Ordinal))
            return;
        PublishFactionMutation(
            factionId,
            (V20CampaignRuntime candidate, out string mutationFailure) =>
                ((IFactionCampaignDeliveryCommand)candidate)
                    .TrySetDeliveryFailure(
                        factionId,
                        canonical,
                        out mutationFailure),
            "faction-contract-delivery-status");
    }

    private FactionCampaignStateSaveData RequireLiveFaction(string factionId)
    {
        if (!live.TryGetFaction(factionId, out FactionCampaignStateSaveData state))
            throw new InvalidOperationException(
                $"Unknown faction contract campaign '{factionId}'.");
        return state;
    }
}
