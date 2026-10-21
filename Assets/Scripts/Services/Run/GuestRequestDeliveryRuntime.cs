using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class GuestRequestDeliveryRuntime :
    IGuestRequestDeliveryQuery,
    IStartable,
    ITickable
{
    private const float EvaluationIntervalSeconds = 0.5f;
    private readonly ISocietyEventQuery society;
    private readonly IGuestRequestDeliveryStateCommand stateCommands;
    private readonly IContentResolutionService content;
    private readonly V20StoryContentCatalog catalog;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly ISocietyVenueQuery venues;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IEconomyProjectInputOwnerPort inputOwners;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalFacilityItemBatchTransferGateway transfers;
    private readonly IV20MilestoneWorldSnapshotQuery world;
    private readonly IGameCalendar calendar;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    private float nextEvaluationTime;

    public GuestRequestDeliveryRuntime(
        IGameClock clock,
        ISocietyEventQuery society,
        IGuestRequestDeliveryStateCommand stateCommands,
        IContentResolutionService content,
        V20StoryContentCatalog catalog,
        IFacilityCapabilityQuery facilities,
        ISocietyVenueQuery venues,
        IWorldDropZoneQuery dropZones,
        IEconomyProjectInputOwnerPort inputOwners,
        IWorldItemStackRuntime items,
        IPhysicalFacilityItemBatchTransferGateway transfers,
        IV20MilestoneWorldSnapshotQuery world,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.society = society ?? throw new ArgumentNullException(nameof(society));
        this.stateCommands = stateCommands
            ?? throw new ArgumentNullException(nameof(stateCommands));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.venues = venues ?? throw new ArgumentNullException(nameof(venues));
        this.dropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public IReadOnlyList<GuestRequestDeliverySnapshot> ActiveDeliveries =>
        society.ActiveSocietyEvents
            .Where(value => value != null
                && (value.guestDelivery?.phase
                        ?? GuestRequestDeliveryPhase.None)
                    != GuestRequestDeliveryPhase.None)
            .OrderBy(value => value.instanceId, StringComparer.Ordinal)
            .Select(BuildSnapshot)
            .ToArray();

    public bool TryGetDelivery(
        string instanceId,
        out GuestRequestDeliverySnapshot snapshot)
    {
        V20ActiveEventSaveData active = society.ActiveSocietyEvents
            .FirstOrDefault(value => value != null && string.Equals(
                value.instanceId,
                instanceId,
                StringComparison.Ordinal));
        if (active == null
            || (active.guestDelivery?.phase
                    ?? GuestRequestDeliveryPhase.None)
                == GuestRequestDeliveryPhase.None)
        {
            snapshot = null;
            return false;
        }
        snapshot = BuildSnapshot(active);
        return true;
    }

    public void Start()
    {
        AdvanceAll();
        nextEvaluationTime = clock.Time + EvaluationIntervalSeconds;
    }

    public void Tick()
    {
        if (clock.Time < nextEvaluationTime)
            return;
        nextEvaluationTime = clock.Time + EvaluationIntervalSeconds;
        AdvanceAll();
    }

    private void AdvanceAll()
    {
        string[] instanceIds = society.ActiveSocietyEvents
            .Where(value => value != null
                && (value.guestDelivery?.phase
                        ?? GuestRequestDeliveryPhase.None)
                    != GuestRequestDeliveryPhase.None)
            .Select(value => value.instanceId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string instanceId in instanceIds)
        {
            AdvanceOne(instanceId);
            if (TryGetDelivery(instanceId, out GuestRequestDeliverySnapshot view))
                PublishProgress(view);
        }
    }

    private void AdvanceOne(string instanceId)
    {
        V20ActiveEventSaveData active = FindActive(instanceId);
        if (active == null)
            return;
        GuestRequestDeliverySaveData delivery = active.guestDelivery;
        int absoluteDay = Math.Max(1, calendar.Day);
        if ((delivery.phase is GuestRequestDeliveryPhase.AwaitingVenue
                or GuestRequestDeliveryPhase.Delivering)
            && absoluteDay > active.deadlineAbsoluteDay)
        {
            RequireMutation(stateCommands.TryRequestGuestRequestDeliveryRelease(
                    instanceId,
                    GuestRequestDeliveryOutbox.ExpiredDisposition,
                    "요청 기한이 지나 운반 중인 물품을 회수합니다.",
                    out string expiryFailure),
                expiryFailure,
                instanceId);
            return;
        }

        switch (delivery.phase)
        {
            case GuestRequestDeliveryPhase.AwaitingVenue:
                TryAssignVenue(active);
                return;
            case GuestRequestDeliveryPhase.Delivering:
                AdvanceDelivery(active, absoluteDay);
                return;
            case GuestRequestDeliveryPhase.ReleasePending:
                AdvanceRelease(active, absoluteDay);
                return;
            case GuestRequestDeliveryPhase.PhysicalCommitted:
                PublishOutcome(active, "fulfill", absoluteDay);
                return;
            case GuestRequestDeliveryPhase.RewardPublished:
                AdvanceRewardCleanup(active);
                return;
        }
    }

    private void TryAssignVenue(V20ActiveEventSaveData active)
    {
        GuestRequestDefinitionSO definition = RequireDefinition(active);
        if (!dropZones.TryGetDeliveryDropoff(out Vector2Int origin))
        {
            SetFailure(
                active.instanceId,
                "납품 거리 산정에 사용할 실제 보급 인도 지점이 없습니다.");
            return;
        }
        if (!venues.TryFindDeliveryVenues(
                definition.venueRequirements,
                origin,
                active.instanceId,
                out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
                out string venueFailure))
        {
            SetFailure(active.instanceId, venueFailure);
            return;
        }
        IReadOnlyDictionary<string, int> material =
            GuestRequestDeliveryMaterialRules.BuildMaterialRequirements(
                definition);
        string destinationId = GuestRequestDeliveryOutbox.FormatDestinationId(
            active.instanceId);
        string lastFailure = string.Empty;
        foreach (SocietyVenueCandidateSnapshot candidate in candidates)
        {
            if (!inputOwners.TryEnsure(
                    EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                    active.instanceId,
                    destinationId,
                    candidate.Facility.centerPos,
                    EconomyProjectInputOwnerAnchorKind.LiveFacility,
                    candidate.FacilityInstanceId,
                    material,
                    0L,
                    0L,
                    string.Empty,
                    out EconomyProjectInputOwnerProjection projection,
                    out lastFailure))
            {
                continue;
            }
            bool assigned = stateCommands.TryAssignGuestRequestDeliveryVenue(
                active.instanceId,
                candidate.FacilityInstanceId,
                destinationId,
                candidate.Facility.centerPos,
                projection,
                out string assignmentFailure);
            if (!assigned)
            {
                if (!inputOwners.TryRetireDestination(
                        EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                        destinationId,
                        EconomyProjectInputOwnerAuthority
                            .GuestRequestReplanReason,
                        out string compensationFailure))
                {
                    throw new InvalidOperationException(
                        $"Guest request '{active.instanceId}' venue assignment "
                        + "failed and input-owner compensation also failed: "
                        + assignmentFailure + "; " + compensationFailure);
                }
                throw new InvalidOperationException(
                    $"Guest request '{active.instanceId}' venue assignment failed: "
                    + assignmentFailure);
            }
            return;
        }
        SetFailure(
            active.instanceId,
            "적격 시설의 납품 수용 공간을 확보할 수 없습니다."
            + (string.IsNullOrWhiteSpace(lastFailure)
                ? string.Empty
                : " " + lastFailure));
    }

    private void AdvanceDelivery(
        V20ActiveEventSaveData active,
        int absoluteDay)
    {
        GuestRequestDefinitionSO definition = RequireDefinition(active);
        GuestRequestDeliverySaveData delivery = active.guestDelivery;
        string venueFailure = string.Empty;
        bool venueValid = dropZones.TryGetDeliveryDropoff(out Vector2Int origin);
        if (venueValid)
        {
            venueValid = venues.TryValidateDeliveryVenue(
                definition.venueRequirements,
                origin,
                active.instanceId,
                delivery.venueFacilityInstanceId,
                new Vector2Int(delivery.destinationX, delivery.destinationY),
                out _,
                out venueFailure);
        }
        if (!venueValid && string.IsNullOrWhiteSpace(venueFailure))
            venueFailure = "현재 보급 인도 지점에서 배정 장소를 다시 검증할 수 없습니다.";
        string ownerFailure = string.Empty;
        bool ownerValid = venueValid && inputOwners.TryValidate(
                EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                active.instanceId,
                delivery.destinationId,
                new Vector2Int(delivery.destinationX, delivery.destinationY),
                EconomyProjectInputOwnerAnchorKind.LiveFacility,
                delivery.venueFacilityInstanceId,
                GuestRequestDeliveryMaterialRules.BuildMaterialRequirements(
                    definition),
                delivery.inputCapacityGrams,
                delivery.inputMassAuthorityRevision,
                delivery.inputCapacityFingerprint,
                out ownerFailure);
        if (!venueValid || !ownerValid)
        {
            string reason = !string.IsNullOrWhiteSpace(venueFailure)
                ? venueFailure
                : "배정 시설의 납품 수용 권위가 유효하지 않습니다. "
                    + ownerFailure;
            RequireMutation(stateCommands.TryRequestGuestRequestDeliveryRelease(
                    active.instanceId,
                    GuestRequestDeliveryOutbox.ReplanDisposition,
                    reason,
                    out string releaseFailure),
                releaseFailure,
                active.instanceId);
            return;
        }

        string operationId = GuestRequestDeliveryOutbox.FormatOperationId(
            active.instanceId);
        if (transfers.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt existing))
        {
            RecordReceipt(active, existing);
            return;
        }

        IReadOnlyDictionary<string, int> material =
            GuestRequestDeliveryMaterialRules.BuildMaterialRequirements(
                definition);
        Vector2Int destination = new(
            delivery.destinationX,
            delivery.destinationY);
        foreach (KeyValuePair<string, int> required in material)
        {
            int assigned = CountQuantity(
                delivery.destinationId,
                required.Key,
                stack => stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                    or WorldItemStackState.FacilityOutputBuffer
                    or WorldItemStackState.InTransit
                    or WorldItemStackState.FacilityBuffer)
                + CountCommittedHaulQuantity(
                    delivery.destinationId,
                    required.Key);
            int missing = Math.Max(0, required.Value - assigned);
            if (missing <= 0)
                continue;
            if (!items.TryRequestItemDelivery(
                    required.Key,
                    missing,
                    destination,
                    delivery.destinationId,
                    out int requested,
                    out string requestFailure)
                || requested != missing)
            {
                SetFailure(
                    active.instanceId,
                    string.IsNullOrWhiteSpace(requestFailure)
                        ? $"물품 {required.Key} 운반 요청이 일부만 배정되었습니다: {requested}/{missing}."
                        : requestFailure);
                return;
            }
        }

        bool allArrived = material.All(required => CountQuantity(
            delivery.destinationId,
            required.Key,
            stack => stack.State == WorldItemStackState.FacilityBuffer)
            >= required.Value);
        if (!allArrived)
        {
            SetFailure(active.instanceId, string.Empty);
            return;
        }
        if (absoluteDay > active.deadlineAbsoluteDay)
        {
            RequireMutation(stateCommands.TryRequestGuestRequestDeliveryRelease(
                    active.instanceId,
                    GuestRequestDeliveryOutbox.ExpiredDisposition,
                    "요청 기한이 지나 도착 물품을 회수합니다.",
                    out string expiryFailure),
                expiryFailure,
                active.instanceId);
            return;
        }
        if (!transfers.TryCommitTransferPending(
                delivery.destinationId,
                material,
                operationId,
                GuestRequestDeliveryOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string transferFailure))
        {
            SetFailure(active.instanceId, transferFailure);
            return;
        }
        RecordReceipt(active, receipt);
    }

    private void RecordReceipt(
        V20ActiveEventSaveData active,
        PhysicalItemBatchDispositionReceipt receipt)
    {
        if (!stateCommands.TryRecordGuestRequestDeliveryReceipt(
                active.instanceId,
                GuestRequestDeliveryReceipt.FromPhysical(receipt),
                out string failure))
        {
            throw new InvalidOperationException(
                $"Guest request '{active.instanceId}' committed physical "
                + "receipt could not be recorded: "
                + failure);
        }
    }

    private void AdvanceRelease(
        V20ActiveEventSaveData active,
        int absoluteDay)
    {
        GuestRequestDeliverySaveData delivery = active.guestDelivery;
        if (delivery.inputOwnerActive)
        {
            string reason = string.Equals(
                    delivery.terminalResolutionId,
                    GuestRequestDeliveryOutbox.ReplanDisposition,
                    StringComparison.Ordinal)
                ? EconomyProjectInputOwnerAuthority.GuestRequestReplanReason
                : EconomyProjectInputOwnerAuthority.GuestRequestTerminalReason;
            if (!inputOwners.TryRetireDestination(
                    EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                    delivery.destinationId,
                    reason,
                    out string retireFailure))
            {
                SetFailure(active.instanceId, retireFailure);
                return;
            }
            RequireMutation(
                stateCommands.TryMarkGuestRequestInputOwnerRetired(
                    active.instanceId,
                    out string markFailure),
                markFailure,
                active.instanceId);
            return;
        }
        if (string.Equals(
                delivery.terminalResolutionId,
                GuestRequestDeliveryOutbox.ReplanDisposition,
                StringComparison.Ordinal))
        {
            RequireMutation(
                stateCommands.TryResumeGuestRequestVenueSelection(
                    active.instanceId,
                    out string resumeFailure),
                resumeFailure,
                active.instanceId);
            return;
        }
        PublishOutcome(active, delivery.terminalResolutionId, absoluteDay);
    }

    private void PublishOutcome(
        V20ActiveEventSaveData active,
        string resolutionId,
        int absoluteDay)
    {
        string actionId = string.Equals(
                resolutionId,
                "fulfill",
                StringComparison.Ordinal)
            ? active.guestDelivery.acceptedActionId
            : V21ContentAlertActionIds.Society(
                active.instanceId,
                resolutionId);
        ContentResolutionRequest request = new()
        {
            ActionId = actionId,
            Kind = ContentResolutionRequestKind.GuestRequestDeliveryOutcome,
            InstanceId = active.instanceId,
            ChoiceId = resolutionId,
            AbsoluteDay = absoluteDay,
            Requirements = world.Build(absoluteDay)
        };
        if (!content.TryExecute(
                request,
                out ContentResolutionResult result,
                out DomainFailure failure))
        {
            SetFailure(
                active.instanceId,
                "손님 요청 결과 확정을 재시도합니다: " + failure.Code);
            return;
        }
        foreach (V20ResolvedEventResult resolved in result.Resolutions)
        {
            events.Publish(new V20ContentEffectsResolvedEvent(
                resolved,
                physicalEffectsApplied: true));
        }
    }

    private void AdvanceRewardCleanup(V20ActiveEventSaveData active)
    {
        GuestRequestDeliverySaveData delivery = active.guestDelivery;
        if (delivery.inputOwnerActive)
        {
            if (!inputOwners.TryRetireDestination(
                    EconomyProjectInputOwnerAuthority.GuestRequestDomain,
                    delivery.destinationId,
                    EconomyProjectInputOwnerAuthority.GuestRequestTerminalReason,
                    out string retireFailure))
            {
                SetFailure(active.instanceId, retireFailure);
                return;
            }
            RequireMutation(
                stateCommands.TryMarkGuestRequestInputOwnerRetired(
                    active.instanceId,
                    out string markFailure),
                markFailure,
                active.instanceId);
            return;
        }

        if (string.Equals(
                delivery.terminalResolutionId,
                "fulfill",
                StringComparison.Ordinal))
        {
            if (!transfers.TryGetPending(
                    delivery.operationId,
                    out PhysicalItemBatchDispositionReceipt receipt)
                || !GuestRequestDeliveryOutbox.MatchesSaved(delivery, receipt))
            {
                SetFailure(
                    active.instanceId,
                    "완료된 손님 요청의 물리 인도 receipt가 일치하지 않습니다.");
                return;
            }
            if (!transfers.Acknowledge(
                    receipt.CommitId,
                    out string acknowledgementFailure))
            {
                SetFailure(active.instanceId, acknowledgementFailure);
                return;
            }
        }
        RequireMutation(
            stateCommands.TryFinalizeGuestRequestDelivery(
                active.instanceId,
                out string finalizeFailure),
            finalizeFailure,
            active.instanceId);
        events.Publish(new GuestRequestDeliveryFinalizedFact(active.instanceId));
    }

    private GuestRequestDeliverySnapshot BuildSnapshot(
        V20ActiveEventSaveData active)
    {
        GuestRequestDefinitionSO definition = RequireDefinition(active);
        GuestRequestDeliverySaveData delivery = active.guestDelivery;
        BuildableObject facility = facilities
            .FindOperational(FacilityCapabilityKind.None)
            .FirstOrDefault(value => value != null && string.Equals(
                value.PersistentInstanceId.Value,
                delivery.venueFacilityInstanceId,
                StringComparison.Ordinal));
        IReadOnlyDictionary<string, int> material =
            GuestRequestDeliveryMaterialRules.BuildMaterialRequirements(
                definition);
        bool delivered = (delivery.phase is
                GuestRequestDeliveryPhase.PhysicalCommitted
                or GuestRequestDeliveryPhase.RewardPublished)
            && GuestRequestDeliveryOutbox.IsCanonicalReceipt(delivery);
        SocietyVenueCandidateSnapshot venue = null;
        if (!string.IsNullOrEmpty(delivery.venueFacilityInstanceId)
            && dropZones.TryGetDeliveryDropoff(out Vector2Int origin))
        {
            venues.TryValidateDeliveryVenue(
                definition.venueRequirements,
                origin,
                active.instanceId,
                delivery.venueFacilityInstanceId,
                new Vector2Int(delivery.destinationX, delivery.destinationY),
                out venue,
                out _);
        }
        GuestRequestDeliveryItemProgressSnapshot[] progress = material
            .Select(required =>
            {
                string displayName = items.CatalogProvider.TryGetDefinition(
                        required.Key,
                        out DungeonItemDefinition item)
                    ? item.DisplayName
                    : required.Key;
                return new GuestRequestDeliveryItemProgressSnapshot(
                    required.Key,
                    displayName,
                    required.Value,
                    CountQuantity(delivery.destinationId, required.Key,
                        stack => stack.State is WorldItemStackState.Loose
                            or WorldItemStackState.Stored
                            or WorldItemStackState.FacilityOutputBuffer),
                    CountQuantity(delivery.destinationId, required.Key,
                        stack => stack.State == WorldItemStackState.InTransit)
                        + CountCommittedHaulQuantity(
                            delivery.destinationId,
                            required.Key),
                    CountQuantity(delivery.destinationId, required.Key,
                        stack => stack.State == WorldItemStackState.FacilityBuffer),
                    delivered ? required.Value : 0);
            })
            .ToArray();
        return new GuestRequestDeliverySnapshot
        {
            InstanceId = active.instanceId,
            DefinitionId = active.definitionId,
            Phase = delivery.phase,
            VenueFacilityInstanceId = delivery.venueFacilityInstanceId,
            VenueDisplayName = facility?.BuildingData?.objectName
                ?? delivery.venueFacilityInstanceId,
            VenueAccessCell = venue?.AccessCell ?? default,
            VenueCapacitySummary = venue?.CapacitySummary ?? string.Empty,
            DestinationId = delivery.destinationId,
            Destination = new Vector2Int(
                delivery.destinationX,
                delivery.destinationY),
            OperationId = delivery.operationId,
            CommitId = delivery.commitId,
            RequestFingerprint = delivery.requestFingerprint,
            TerminalResolutionId = delivery.terminalResolutionId,
            StatusReason = delivery.failureReason,
            Items = progress
        };
    }

    private void PublishProgress(GuestRequestDeliverySnapshot snapshot)
    {
        GuestRequestDefinitionSO definition = catalog.GuestRequests.Single(value =>
            string.Equals(value.StableId, snapshot.DefinitionId, StringComparison.Ordinal));
        V20ActiveEventSaveData active = FindActive(snapshot.InstanceId);
        events.Publish(new GuestRequestDeliveryProgressedFact(
            snapshot, definition.DisplayName, definition.Description,
            active.riskTier, active.riskReason, active.deadlineAbsoluteDay));
    }

    private int CountQuantity(
        string destinationId,
        string itemId,
        Func<WorldItemStackSnapshot, bool> predicate) =>
        string.IsNullOrWhiteSpace(destinationId)
            ? 0
            : items.GetAllStacks()
                .Where(stack => stack != null
                    && stack.Quantity > 0
                    && string.Equals(stack.DestinationId, destinationId,
                        StringComparison.Ordinal)
                    && string.Equals(stack.ItemId, itemId,
                        StringComparison.Ordinal)
                    && (predicate?.Invoke(stack) ?? true))
                .Sum(stack => stack.Quantity);

    private int CountCommittedHaulQuantity(
        string destinationId,
        string itemId) =>
        string.IsNullOrWhiteSpace(destinationId)
            ? 0
            : items.CaptureHaulDeliveryIntentsByDestination(destinationId)
                .Where(intent => intent != null)
                .SelectMany(intent => intent.commitments
                    ?? new List<HaulDeliveryItemCommitmentSaveData>())
                .Where(commitment => commitment != null
                    && commitment.quantity > 0
                    && string.Equals(
                        commitment.itemId,
                        itemId,
                        StringComparison.Ordinal))
                .Sum(commitment => commitment.quantity);

    private V20ActiveEventSaveData FindActive(string instanceId) =>
        society.ActiveSocietyEvents.FirstOrDefault(value => value != null
            && string.Equals(value.instanceId, instanceId,
                StringComparison.Ordinal));

    private GuestRequestDefinitionSO RequireDefinition(
        V20ActiveEventSaveData active) =>
        catalog.GuestRequests.Single(value => active != null && string.Equals(
            value.StableId,
            active.definitionId,
            StringComparison.Ordinal));

    private void SetFailure(string instanceId, string failureReason)
    {
        V20ActiveEventSaveData active = FindActive(instanceId);
        if (active == null
            || string.Equals(
                active.guestDelivery?.failureReason,
                failureReason?.Trim() ?? string.Empty,
                StringComparison.Ordinal))
        {
            return;
        }
        RequireMutation(stateCommands.TrySetGuestRequestDeliveryFailure(
                instanceId,
                failureReason,
                out string failure),
            failure,
            instanceId);
    }

    private static void RequireMutation(
        bool succeeded,
        string failure,
        string instanceId)
    {
        if (!succeeded)
            throw new InvalidOperationException(
                $"Guest request '{instanceId}' state mutation failed: "
                + failure);
    }
}
