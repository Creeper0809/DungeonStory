using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

public readonly struct GuestRequestDeliveryReceipt
{
    public GuestRequestDeliveryReceipt(
        PhysicalItemDispositionKind kind,
        string operationId,
        string reasonCode,
        string requestFingerprint,
        IReadOnlyList<string> sourceStackIds,
        int quantity,
        long inputMassGrams)
    {
        Kind = kind;
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        RequestFingerprint = requestFingerprint ?? string.Empty;
        SourceStackIds = (sourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Quantity = quantity;
        InputMassGrams = inputMassGrams;
        CommitId = $"physical-batch-disposition:{(int)kind}:{OperationId}:"
            + $"{quantity}:{inputMassGrams}";
    }

    public static GuestRequestDeliveryReceipt FromPhysical(
        PhysicalItemBatchDispositionReceipt receipt) =>
        new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            receipt.RequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams);

    public PhysicalItemDispositionKind Kind { get; }
    public string OperationId { get; }
    public string ReasonCode { get; }
    public string RequestFingerprint { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
    public int Quantity { get; }
    public long InputMassGrams { get; }
    public string CommitId { get; }
    public bool IsCommitted => Kind == PhysicalItemDispositionKind.Transfer
        && EconomyProjectInputOwnerAuthority.IsCanonical(OperationId)
        && EconomyProjectInputOwnerAuthority.IsCanonical(ReasonCode)
        && EconomyProjectInputOwnerAuthority.IsCanonical(RequestFingerprint)
        && SourceStackIds.Count > 0
        && SourceStackIds.All(EconomyProjectInputOwnerAuthority.IsCanonical)
        && SourceStackIds.Distinct(StringComparer.Ordinal).Count()
            == SourceStackIds.Count
        && Quantity > 0
        && InputMassGrams > 0L;
}

public sealed class GuestRequestDeliveryItemProgressSnapshot
{
    public GuestRequestDeliveryItemProgressSnapshot(
        string itemId,
        string displayName,
        int required,
        int assigned,
        int hauling,
        int arrived,
        int delivered)
    {
        ItemId = itemId ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? ItemId
            : displayName;
        Required = Math.Max(0, required);
        Assigned = Math.Max(0, assigned);
        Hauling = Math.Max(0, hauling);
        Arrived = Math.Max(0, arrived);
        Delivered = Math.Max(0, delivered);
    }

    public string ItemId { get; }
    public string DisplayName { get; }
    public int Required { get; }
    public int Assigned { get; }
    public int Hauling { get; }
    public int Arrived { get; }
    public int Delivered { get; }
}

public sealed class GuestRequestDeliverySnapshot
{
    public string InstanceId { get; internal set; } = string.Empty;
    public string DefinitionId { get; internal set; } = string.Empty;
    public GuestRequestDeliveryPhase Phase { get; internal set; }
    public string VenueFacilityInstanceId { get; internal set; } = string.Empty;
    public string VenueDisplayName { get; internal set; } = string.Empty;
    public Vector2Int VenueAccessCell { get; internal set; }
    public string VenueCapacitySummary { get; internal set; } = string.Empty;
    public string DestinationId { get; internal set; } = string.Empty;
    public Vector2Int Destination { get; internal set; }
    public string OperationId { get; internal set; } = string.Empty;
    public string CommitId { get; internal set; } = string.Empty;
    public string RequestFingerprint { get; internal set; } = string.Empty;
    public string TerminalResolutionId { get; internal set; } = string.Empty;
    public string StatusReason { get; internal set; } = string.Empty;
    public IReadOnlyList<GuestRequestDeliveryItemProgressSnapshot> Items
        { get; internal set; } =
        Array.Empty<GuestRequestDeliveryItemProgressSnapshot>();
}

public interface IGuestRequestDeliveryQuery
{
    IReadOnlyList<GuestRequestDeliverySnapshot> ActiveDeliveries { get; }
    bool TryGetDelivery(
        string instanceId,
        out GuestRequestDeliverySnapshot snapshot);
}

public static class GuestRequestDeliveryMaterialRules
{
    public static IReadOnlyDictionary<string, int> BuildMaterialRequirements(
        GuestRequestDefinitionSO request)
    {
        V20ItemAmountRequirement[] material =
            (request?.serviceRequirements?.items
                    ?? new List<V20ItemAmountRequirement>())
                .Where(value => value?.consume == true)
                .ToArray();
        if (material.Any(value =>
                !EconomyProjectInputOwnerAuthority.IsCanonical(
                    value.itemDefinitionId)
                || value.amount <= 0))
        {
            throw new InvalidOperationException(
                $"Guest request '{request?.StableId}' has an invalid physical "
                + "material requirement.");
        }
        return material
            .GroupBy(
                value => value.itemDefinitionId,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => checked(group.Sum(value => value.amount)),
                StringComparer.Ordinal);
    }

    public static bool HasPhysicalDelivery(GuestRequestDefinitionSO request) =>
        BuildMaterialRequirements(request).Count > 0;

    public static V20ContentRequirementSet BuildNonDeliveryRequirements(
        GuestRequestDefinitionSO request)
    {
        V20ContentRequirementSet source = request?.serviceRequirements
            ?? new V20ContentRequirementSet();
        return new V20ContentRequirementSet
        {
            items = (source.items ?? new List<V20ItemAmountRequirement>())
                .Where(value => value != null && !value.consume)
                .ToList(),
            facilities = new List<V20FacilityRequirement>(),
            research = (source.research ?? new List<V20ResearchRequirement>())
                .Where(value => value != null)
                .ToList(),
            characters = (source.characters ?? new List<V20CharacterRequirement>())
                .Where(value => value != null)
                .ToList(),
            factions = (source.factions ?? new List<V20FactionRequirement>())
                .Where(value => value != null)
                .ToList(),
            worldMetrics = (source.worldMetrics
                    ?? new List<V20WorldMetricRequirement>())
                .Where(value => value != null)
                .ToList(),
            requiredFlags = (source.requiredFlags ?? new List<string>()).ToList(),
            excludedFlags = (source.excludedFlags ?? new List<string>()).ToList()
        };
    }
}

public static class GuestRequestDeliveryOutbox
{
    public const string TransferReason = "guest-request-delivery";
    public const string ReplanDisposition = "replan";
    public const string DeclineDisposition = "decline";
    public const string ExpiredDisposition = "expired";
    private const string OperationPrefix = "guest-request-transfer:";

    public static string FormatOperationId(string instanceId)
    {
        RequireCanonical(instanceId, nameof(instanceId));
        return OperationPrefix + Uri.EscapeDataString(instanceId);
    }

    public static string FormatDestinationId(string instanceId) =>
        EconomyProjectInputOwnerAuthority.BuildGuestRequestDestinationId(
            instanceId);

    public static bool IsOwnedOperationId(string operationId) =>
        operationId != null
        && operationId.StartsWith(OperationPrefix, StringComparison.Ordinal);

    public static void RequireValidState(
        V20ActiveEventSaveData active,
        GuestRequestDefinitionSO definition,
        bool activeCollection)
    {
        GuestRequestDeliverySaveData state = active?.guestDelivery;
        if (active == null || state == null || state.sourceStackIds == null)
            throw new InvalidOperationException(
                $"Guest-request delivery save state for '{active?.instanceId}' is missing.");
        if (state.phase == GuestRequestDeliveryPhase.None)
        {
            if (!IsCanonicalEmpty(state))
                throw new InvalidOperationException(
                    $"Empty guest-request delivery state for "
                    + $"'{active.instanceId}' is not canonical.");
            return;
        }
        if (!activeCollection
            || definition == null
            || !GuestRequestDeliveryMaterialRules.HasPhysicalDelivery(definition)
            || (state.phase == GuestRequestDeliveryPhase.RewardPublished
                ? !string.Equals(
                    active.selectedChoiceId,
                    state.terminalResolutionId,
                    StringComparison.Ordinal)
                : !string.Equals(
                    active.selectedChoiceId,
                    "fulfill",
                    StringComparison.Ordinal))
            || (state.phase == GuestRequestDeliveryPhase.RewardPublished
                ? !string.Equals(
                    active.resolutionId,
                    state.terminalResolutionId,
                    StringComparison.Ordinal)
                : !string.IsNullOrEmpty(active.resolutionId))
            || !EconomyProjectInputOwnerAuthority.IsCanonical(
                state.acceptedActionId)
            || !string.Equals(
                state.acceptedActionId,
                V21ContentAlertActionIds.Society(active.instanceId, "fulfill"),
                StringComparison.Ordinal)
            || !IsOptionalCanonicalText(state.failureReason))
        {
            throw new InvalidOperationException(
                $"Guest-request delivery owner identity for '{active.instanceId}' is invalid.");
        }

        bool hasAssignment =
            EconomyProjectInputOwnerAuthority.IsCanonical(
                state.venueFacilityInstanceId)
            && EconomyProjectInputOwnerAuthority.IsCanonical(
                state.destinationId)
            && string.Equals(
                state.destinationId,
                FormatDestinationId(active.instanceId),
                StringComparison.Ordinal);
        bool hasProjection = state.inputCapacityGrams > 0L
            && state.inputMassAuthorityRevision > 0L
            && EconomyProjectInputOwnerAuthority.IsCanonical(
                state.inputCapacityFingerprint);
        bool emptyAssignment = string.IsNullOrEmpty(
                state.venueFacilityInstanceId)
            && string.IsNullOrEmpty(state.destinationId)
            && state.destinationX == 0
            && state.destinationY == 0;
        bool emptyProjection = state.inputCapacityGrams == 0L
            && state.inputMassAuthorityRevision == 0L
            && string.IsNullOrEmpty(state.inputCapacityFingerprint);
        bool emptyReceipt = IsEmptyReceipt(state);
        bool canonicalOwnedReceipt = IsCanonicalReceipt(state)
            && string.Equals(
                state.operationId,
                FormatOperationId(active.instanceId),
                StringComparison.Ordinal)
            && state.committedQuantity == GuestRequestDeliveryMaterialRules
                .BuildMaterialRequirements(definition)
                .Values.Sum()
            && (!state.inputOwnerActive
                || state.committedMassGrams == state.inputCapacityGrams);
        bool valid = state.phase switch
        {
            GuestRequestDeliveryPhase.AwaitingVenue =>
                !active.resolved
                && emptyAssignment
                && !state.inputOwnerActive
                && emptyProjection
                && emptyReceipt
                && string.IsNullOrEmpty(state.terminalResolutionId),
            GuestRequestDeliveryPhase.Delivering =>
                !active.resolved
                && hasAssignment
                && state.inputOwnerActive
                && hasProjection
                && emptyReceipt
                && string.IsNullOrEmpty(state.terminalResolutionId),
            GuestRequestDeliveryPhase.ReleasePending =>
                !active.resolved
                && emptyReceipt
                && (state.inputOwnerActive
                    ? hasAssignment && hasProjection
                    : emptyProjection)
                && IsReleaseDisposition(state.terminalResolutionId),
            GuestRequestDeliveryPhase.PhysicalCommitted =>
                !active.resolved
                && hasAssignment
                && state.inputOwnerActive
                && hasProjection
                && string.IsNullOrEmpty(state.terminalResolutionId)
                && canonicalOwnedReceipt,
            GuestRequestDeliveryPhase.RewardPublished =>
                active.resolved
                && hasAssignmentOrTerminalWithoutVenue(
                    hasAssignment,
                    state.terminalResolutionId)
                && (state.inputOwnerActive ? hasProjection : emptyProjection)
                && (string.Equals(
                        state.terminalResolutionId,
                        "fulfill",
                        StringComparison.Ordinal)
                    ? canonicalOwnedReceipt
                    : IsTerminalFailure(state.terminalResolutionId)
                        && !state.inputOwnerActive
                        && emptyReceipt),
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException(
                $"Guest-request delivery occurrence '{active.instanceId}' "
                + $"has invalid phase '{state.phase}'.");

        static bool hasAssignmentOrTerminalWithoutVenue(
            bool assignment,
            string resolutionId) =>
            assignment || IsTerminalFailure(resolutionId);
    }

    public static bool IsCanonicalEmpty(GuestRequestDeliverySaveData state) =>
        state != null
        && state.phase == GuestRequestDeliveryPhase.None
        && string.IsNullOrEmpty(state.acceptedActionId)
        && string.IsNullOrEmpty(state.venueFacilityInstanceId)
        && string.IsNullOrEmpty(state.destinationId)
        && state.destinationX == 0
        && state.destinationY == 0
        && !state.inputOwnerActive
        && state.inputCapacityGrams == 0L
        && state.inputMassAuthorityRevision == 0L
        && string.IsNullOrEmpty(state.inputCapacityFingerprint)
        && IsEmptyReceipt(state)
        && string.IsNullOrEmpty(state.terminalResolutionId)
        && string.IsNullOrEmpty(state.failureReason);

    public static bool IsCanonicalReceipt(GuestRequestDeliverySaveData state) =>
        state != null
        && EconomyProjectInputOwnerAuthority.IsCanonical(state.operationId)
        && EconomyProjectInputOwnerAuthority.IsCanonical(state.commitId)
        && EconomyProjectInputOwnerAuthority.IsCanonical(
            state.requestFingerprint)
        && state.sourceStackIds != null
        && state.sourceStackIds.Count > 0
        && state.sourceStackIds.All(
            EconomyProjectInputOwnerAuthority.IsCanonical)
        && state.sourceStackIds.SequenceEqual(
            state.sourceStackIds.OrderBy(value => value, StringComparer.Ordinal),
            StringComparer.Ordinal)
        && state.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
            == state.sourceStackIds.Count
        && state.committedQuantity > 0
        && state.committedMassGrams > 0L
        && string.Equals(
            state.commitId,
            FormatCommitId(state),
            StringComparison.Ordinal);

    public static bool MatchesSaved(
        GuestRequestDeliverySaveData state,
        PhysicalItemBatchDispositionReceipt receipt) =>
        IsCanonicalReceipt(state)
        && receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(receipt.OperationId, state.operationId,
            StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode, TransferReason,
            StringComparison.Ordinal)
        && string.Equals(receipt.CommitId, state.commitId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.RequestFingerprint,
            state.requestFingerprint,
            StringComparison.Ordinal)
        && receipt.Quantity == state.committedQuantity
        && receipt.InputMassGrams == state.committedMassGrams
        && (receipt.SourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(state.sourceStackIds, StringComparer.Ordinal);

    public static void Record(
        GuestRequestDeliverySaveData state,
        string instanceId,
        GuestRequestDeliveryReceipt receipt)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (state.phase != GuestRequestDeliveryPhase.Delivering
            || !receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(
                receipt.OperationId,
                FormatOperationId(instanceId),
                StringComparison.Ordinal)
            || !string.Equals(receipt.ReasonCode, TransferReason,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Guest-request delivery receipt is not canonical.");
        }
        state.phase = GuestRequestDeliveryPhase.PhysicalCommitted;
        state.operationId = receipt.OperationId;
        state.commitId = receipt.CommitId;
        state.requestFingerprint = receipt.RequestFingerprint;
        state.sourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        state.committedQuantity = receipt.Quantity;
        state.committedMassGrams = receipt.InputMassGrams;
        state.failureReason = string.Empty;
    }

    public static void ClearReceipt(GuestRequestDeliverySaveData state)
    {
        state.operationId = string.Empty;
        state.commitId = string.Empty;
        state.requestFingerprint = string.Empty;
        state.sourceStackIds.Clear();
        state.committedQuantity = 0;
        state.committedMassGrams = 0L;
    }

    private static string FormatCommitId(GuestRequestDeliverySaveData state) =>
        $"physical-batch-disposition:1:{state.operationId}:"
        + $"{state.committedQuantity}:{state.committedMassGrams}";

    private static bool IsEmptyReceipt(GuestRequestDeliverySaveData state) =>
        state != null
        && string.IsNullOrEmpty(state.operationId)
        && string.IsNullOrEmpty(state.commitId)
        && string.IsNullOrEmpty(state.requestFingerprint)
        && (state.sourceStackIds?.Count ?? 0) == 0
        && state.committedQuantity == 0
        && state.committedMassGrams == 0L;

    private static bool IsReleaseDisposition(string value) =>
        string.Equals(value, ReplanDisposition, StringComparison.Ordinal)
        || IsTerminalFailure(value);

    private static bool IsTerminalFailure(string value) =>
        string.Equals(value, DeclineDisposition, StringComparison.Ordinal)
        || string.Equals(value, ExpiredDisposition, StringComparison.Ordinal);

    private static bool IsOptionalCanonicalText(string value) =>
        value != null
        && (value.Length == 0
            || string.Equals(value, value.Trim(), StringComparison.Ordinal));

    private static void RequireCanonical(string value, string parameterName)
    {
        if (!EconomyProjectInputOwnerAuthority.IsCanonical(value))
            throw new ArgumentException(
                "Guest-request delivery requires a canonical ID.",
                parameterName);
    }
}

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
    private readonly IGameEventBus events;
    private readonly Dictionary<string, string> publishedAlertFingerprints =
        new(StringComparer.Ordinal);
    private float nextEvaluationTime;

    public GuestRequestDeliveryRuntime(
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
        nextEvaluationTime = Time.time + EvaluationIntervalSeconds;
    }

    public void Tick()
    {
        if (Time.time < nextEvaluationTime)
            return;
        nextEvaluationTime = Time.time + EvaluationIntervalSeconds;
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
                PublishAlertIfChanged(view);
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
            V20SocietyEventAlertProjection.PublishResolved(
                events,
                resolved,
                physicalEffectsApplied: true);
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
        publishedAlertFingerprints.Remove(active.instanceId);
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

    private void PublishAlertIfChanged(GuestRequestDeliverySnapshot snapshot)
    {
        string fingerprint = BuildAlertFingerprint(snapshot);
        if (publishedAlertFingerprints.TryGetValue(
                snapshot.InstanceId,
                out string current)
            && string.Equals(current, fingerprint, StringComparison.Ordinal))
        {
            return;
        }
        GuestRequestDefinitionSO definition = catalog.GuestRequests.Single(value =>
            string.Equals(
                value.StableId,
                snapshot.DefinitionId,
                StringComparison.Ordinal));
        V20ActiveEventSaveData active = FindActive(snapshot.InstanceId);
        EventAlertChoice[] choices = snapshot.Phase is
                GuestRequestDeliveryPhase.AwaitingVenue
                or GuestRequestDeliveryPhase.Delivering
            ? new[]
            {
                new EventAlertChoice(
                    "요청 취소",
                    "아직 인도되지 않은 물품을 기존 물류 소유권으로 회수한 뒤 요청을 거절합니다.",
                    V21ContentAlertActionIds.Society(
                        snapshot.InstanceId,
                        "decline"))
            }
            : Array.Empty<EventAlertChoice>();
        string venue = string.IsNullOrEmpty(snapshot.VenueFacilityInstanceId)
            ? "배정 대기"
            : $"{snapshot.VenueDisplayName} ({snapshot.Destination.x}, {snapshot.Destination.y})"
                + (string.IsNullOrWhiteSpace(snapshot.VenueCapacitySummary)
                    ? string.Empty
                    : $"\n장소 여유: {snapshot.VenueCapacitySummary}");
        string progress = string.Join("\n", snapshot.Items.Select(value =>
            $"- {value.DisplayName}: 배정 {value.Assigned}, 운반 {value.Hauling}, "
            + $"도착 {value.Arrived}, 인도 {value.Delivered}/{value.Required}"));
        string reason = string.IsNullOrWhiteSpace(snapshot.StatusReason)
            ? string.Empty
            : "\n상태 이유: " + snapshot.StatusReason;
        string risk = V20SocietyEventAlertProjection.FormatRisk(
            active.riskTier,
            active.riskReason);
        events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
            definition.DisplayName,
            $"{definition.Description}\n납품 장소: {venue}\n"
            + $"진행: {DescribePhase(snapshot.Phase)}\n{progress}{reason}\n{risk}\n"
            + $"기한: {active?.deadlineAbsoluteDay ?? 0}일",
            V20SocietyEventAlertProjection.ToAlertImportance(
                active.riskTier),
            "V21 사회 사건",
            choices,
            snapshot.InstanceId)));
        publishedAlertFingerprints[snapshot.InstanceId] = fingerprint;
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

    private static string BuildAlertFingerprint(
        GuestRequestDeliverySnapshot value) =>
        string.Join("|",
            value.Phase,
            value.VenueFacilityInstanceId,
            value.VenueAccessCell,
            value.VenueCapacitySummary,
            value.Destination,
            value.OperationId,
            value.CommitId,
            value.TerminalResolutionId,
            value.StatusReason,
            string.Join(";", value.Items.Select(item =>
                $"{item.ItemId}:{item.Assigned}:{item.Hauling}:"
                + $"{item.Arrived}:{item.Delivered}/{item.Required}")));

    private static string DescribePhase(GuestRequestDeliveryPhase phase) =>
        phase switch
        {
            GuestRequestDeliveryPhase.AwaitingVenue => "시설 배정 대기",
            GuestRequestDeliveryPhase.Delivering => "물품 운반 중",
            GuestRequestDeliveryPhase.ReleasePending => "물품 회수 중",
            GuestRequestDeliveryPhase.PhysicalCommitted => "물품 인도 완료 · 보상 정산 대기",
            GuestRequestDeliveryPhase.RewardPublished => "보상 반영 완료 · 인도 확인 중",
            _ => "대기"
        };

}
