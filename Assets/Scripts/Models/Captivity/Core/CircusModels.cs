using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CircusShowState
{
    Composition,
    ParticipantEscort,
    AudienceEntering,
    Performing,
    Settlement,
    CleanupAndTreatment,
    Completed,
    Cancelled
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CircusLethalityPolicy
{
    StopWhenDowned,
    AllowAccidents,
    FightToDeath,
    ExecuteDesignatedTarget
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CapturedWildlifeTransportState
{
    AwaitingTransport,
    Transporting,
    Penned,
    MovingToShow,
    Performing,
    ReturningToPen,
    Released,
    Escaped
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CircusProgramModule
{
    public string programId = string.Empty;
    public string displayName = string.Empty;
    public bool requiresCaptive;
    public bool requiresWildlife;
    public bool usesCombat;
    public bool publiclyCruel;
    [Range(0f, 1f)] public float baseAccidentRisk;
    public float baseAudienceSatisfaction;
    public float basePerformerFame;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CircusShowOrder
{
    public string orderId = string.Empty;
    public string stageId = string.Empty;
    public Vector2Int stagePosition;
    public int roomId;
    public string programId = string.Empty;
    public CircusLethalityPolicy lethality;
    public CircusShowState state = CircusShowState.Composition;
    public List<string> performerIds = new List<string>();
    public List<string> wildlifeIds = new List<string>();
    public List<string> audienceIds = new List<string>();
    public List<Vector2Int> performerPositions = new List<Vector2Int>();
    public List<Vector2Int> wildlifePositions = new List<Vector2Int>();
    public List<Vector2Int> audiencePositions = new List<Vector2Int>();
    public float preparationWorkRequired;
    public float preparationWorkCompleted;
    public float elapsedShowSeconds;
    public float showDurationSeconds;
    public float nextCombatExchangeAt;
    public float phaseElapsedSeconds;
    public int ticketPrice;
    public int revenue;
    public float satisfaction;
    public float venueSatisfactionBonus;
    public float venueAccidentRiskBonus;
    public float venueAccidentDamageMultiplier = 1f;
    public float venueFilthMultiplier = 1f;
    public float venueWitnessMoodPenalty;
    public float venueGamblingVariance;
    public int venueFlatRevenuePerAudience;
    public bool accidentResolved;
    public string statusMessage = string.Empty;
    public bool cleanupRequired;
    public bool treatmentRequired;
    public bool betrayalCheckCompleted;
    public int nextSupplyOperationSequence = 1;
    public CircusShowSupplyCommitPhase pendingSupplyPhase;
    public int pendingSupplyOperationSequence;
    public string pendingSupplyOperationId = string.Empty;
    public string pendingSupplyReasonCode = string.Empty;
    public string pendingSupplyCommitId = string.Empty;
    public List<string> pendingSupplySourceStackIds = new List<string>();
    public int pendingSupplyQuantity;
    public long pendingSupplyMassGrams;
    public string pendingSupplyCartStackId = string.Empty;
    public float pendingSupplyCartDurabilityBefore;
    public float pendingSupplyCartDurabilityAfter;
    public bool preparationSuppliesCommitted;
    public string preparationSupplyCommitId = string.Empty;

    public bool IsTerminal =>
        state is CircusShowState.Completed or CircusShowState.Cancelled;

    public CircusShowOrder Clone()
    {
        CircusShowOrder clone = (CircusShowOrder)MemberwiseClone();
        clone.performerIds = new List<string>(performerIds ?? new List<string>());
        clone.wildlifeIds = new List<string>(wildlifeIds ?? new List<string>());
        clone.audienceIds = new List<string>(audienceIds ?? new List<string>());
        clone.performerPositions = new List<Vector2Int>(
            performerPositions ?? new List<Vector2Int>());
        clone.wildlifePositions = new List<Vector2Int>(
            wildlifePositions ?? new List<Vector2Int>());
        clone.audiencePositions = new List<Vector2Int>(
            audiencePositions ?? new List<Vector2Int>());
        clone.pendingSupplySourceStackIds = new List<string>(
            pendingSupplySourceStackIds ?? new List<string>());
        return clone;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CircusSaveData
{
    public const int CurrentVersion = 5;
    public int version = CurrentVersion;
    public int nextOrderSequence;
    public List<CircusShowOrder> orders = new List<CircusShowOrder>();
    public List<CapturedWildlifeState> capturedWildlife =
        new List<CapturedWildlifeState>();
}

public enum CircusShowSupplyCommitPhase
{
    None = 0,
    ItemCommitted = 1,
    OutcomesPublished = 2
}

/// <summary>
/// Cross-assembly physical contract for one circus preparation commit. These
/// are validation constants, not authored balance writers; the corresponding
/// item assets remain the mass authority and focused tests detect drift.
/// </summary>
public static class CircusPerformanceSupplyContracts
{
    public const string PerformancePropBoxItemId =
        "supply:performance-prop-box";
    public const string BanquetCartItemId = "tool:banquet-cart";
    public const long PerformancePropBoxMassGrams = 1_950L;
    public const long BanquetCartMassGrams = 3_150L;
    public const double BanquetCartWearPerShow = 4d;
}

public enum CapturedWildlifeFeedCommitPhase
{
    None = 0,
    ItemCommitted = 1,
    CarePublished = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CapturedWildlifeRoleId :
    IEquatable<CapturedWildlifeRoleId>
{
    private readonly string value;

    public CapturedWildlifeRoleId(string value)
    {
        this.value = value ?? string.Empty;
    }

    public string Value => value ?? string.Empty;
    public bool IsValid => Value.Length > 0
        && string.Equals(Value, Value.Trim(), StringComparison.Ordinal);

    public bool Equals(CapturedWildlifeRoleId other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);
    public override bool Equals(object obj) =>
        obj is CapturedWildlifeRoleId other && Equals(other);
    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Value);
    public override string ToString() => Value;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CapturedWildlifeRoleIds
{
    public static readonly CapturedWildlifeRoleId None =
        new CapturedWildlifeRoleId("wildlife-role:none");
    public static readonly CapturedWildlifeRoleId Companion =
        new CapturedWildlifeRoleId("wildlife-role:companion");
    public static readonly CapturedWildlifeRoleId Haul =
        new CapturedWildlifeRoleId("wildlife-role:haul");
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CapturedWildlifeCapabilityState
{
    public const int CurrentEnvelopeVersion = 1;

    public int version = CurrentEnvelopeVersion;
    public string roleId = CapturedWildlifeRoleIds.None.Value;
    public int payloadVersion;
    public string payloadJson = string.Empty;

    public CapturedWildlifeCapabilityState Clone() =>
        (CapturedWildlifeCapabilityState)MemberwiseClone();

    public static CapturedWildlifeCapabilityState None() => new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class CapturedWildlifeCompanionPayloadV1
{
    public string ownerCharacterId = string.Empty;
    public float nextAttackAt;
}

public enum CapturedWildlifeHaulPhase
{
    Idle = 0,
    Reserved = 1,
    CargoOwned = 2,
    ReleasePending = 3
}

public enum CapturedWildlifeHaulDestinationKind
{
    Warehouse = 0,
    FacilityBuffer = 1
}

public readonly struct CapturedWildlifeHaulPayloadSnapshot
{
    public CapturedWildlifeHaulPayloadSnapshot(
        CapturedWildlifeHaulPhase phase,
        float nextDecisionAt,
        string operationId,
        string leaseId,
        string sourceStackId,
        string cargoStackId,
        string itemId,
        string expectedStackSignature,
        int quantity,
        Vector2Int pickupStandPosition,
        CapturedWildlifeHaulDestinationKind destinationKind,
        string destinationId,
        Vector2Int deliveryPosition,
        Vector2Int dropPosition)
    {
        Phase = phase;
        NextDecisionAt = nextDecisionAt;
        OperationId = operationId ?? string.Empty;
        LeaseId = leaseId ?? string.Empty;
        SourceStackId = sourceStackId ?? string.Empty;
        CargoStackId = cargoStackId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        ExpectedStackSignature = expectedStackSignature ?? string.Empty;
        Quantity = quantity;
        PickupStandPosition = pickupStandPosition;
        DestinationKind = destinationKind;
        DestinationId = destinationId ?? string.Empty;
        DeliveryPosition = deliveryPosition;
        DropPosition = dropPosition;
    }

    public CapturedWildlifeHaulPhase Phase { get; }
    public float NextDecisionAt { get; }
    public string OperationId { get; }
    public string LeaseId { get; }
    public string SourceStackId { get; }
    public string CargoStackId { get; }
    public string ItemId { get; }
    public string ExpectedStackSignature { get; }
    public int Quantity { get; }
    public Vector2Int PickupStandPosition { get; }
    public CapturedWildlifeHaulDestinationKind DestinationKind { get; }
    public string DestinationId { get; }
    public Vector2Int DeliveryPosition { get; }
    public Vector2Int DropPosition { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class CapturedWildlifeHaulPayloadV1
{
    public CapturedWildlifeHaulPhase phase;
    public float nextDecisionAt;
    public string operationId = string.Empty;
    public string leaseId = string.Empty;
    public string sourceStackId = string.Empty;
    public string cargoStackId = string.Empty;
    public string itemId = string.Empty;
    public string expectedStackSignature = string.Empty;
    public int quantity;
    public int pickupGridX;
    public int pickupGridY;
    public CapturedWildlifeHaulDestinationKind destinationKind;
    public string destinationId = string.Empty;
    public int deliveryGridX;
    public int deliveryGridY;
    public int dropGridX;
    public int dropGridY;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CapturedWildlifeCapabilityStateCodec
{
    public const int CompanionPayloadVersion = 1;
    public const int HaulPayloadVersion = 1;

    private delegate bool StateReader(
        CapturedWildlifeCapabilityState state,
        out string failureReason);

    private static readonly IReadOnlyDictionary<CapturedWildlifeRoleId, StateReader>
        Readers = new Dictionary<CapturedWildlifeRoleId, StateReader>
        {
            [CapturedWildlifeRoleIds.None] = ReadNone,
            [CapturedWildlifeRoleIds.Companion] = ReadCompanion,
            [CapturedWildlifeRoleIds.Haul] = ReadHaul
        };

    public static CapturedWildlifeCapabilityState CreateNone() =>
        CapturedWildlifeCapabilityState.None();

    public static CapturedWildlifeCapabilityState CreateCompanion(
        CharacterId ownerId,
        float nextAttackAt)
    {
        if (!ownerId.IsValid)
        {
            throw new ArgumentException(
                "A canonical companion owner ID is required.",
                nameof(ownerId));
        }
        if (!IsFiniteNonNegative(nextAttackAt))
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextAttackAt),
                "Companion attack time must be finite and non-negative.");
        }

        CapturedWildlifeCompanionPayloadV1 payload = new()
        {
            ownerCharacterId = ownerId.Value,
            nextAttackAt = nextAttackAt
        };
        return new CapturedWildlifeCapabilityState
        {
            version = CapturedWildlifeCapabilityState.CurrentEnvelopeVersion,
            roleId = CapturedWildlifeRoleIds.Companion.Value,
            payloadVersion = CompanionPayloadVersion,
            payloadJson = JsonUtility.ToJson(payload, false)
        };
    }

    public static CapturedWildlifeCapabilityState CreateHaul(
        CapturedWildlifeHaulPhase phase,
        float nextDecisionAt,
        string operationId = "",
        string leaseId = "",
        string sourceStackId = "",
        string cargoStackId = "",
        string itemId = "",
        string expectedStackSignature = "",
        int quantity = 0,
        Vector2Int pickupStandPosition = default,
        CapturedWildlifeHaulDestinationKind destinationKind =
            CapturedWildlifeHaulDestinationKind.Warehouse,
        string destinationId = "",
        Vector2Int deliveryPosition = default,
        Vector2Int dropPosition = default)
    {
        CapturedWildlifeHaulPayloadV1 payload = new()
        {
            phase = phase,
            nextDecisionAt = nextDecisionAt,
            operationId = operationId ?? string.Empty,
            leaseId = leaseId ?? string.Empty,
            sourceStackId = sourceStackId ?? string.Empty,
            cargoStackId = cargoStackId ?? string.Empty,
            itemId = itemId ?? string.Empty,
            expectedStackSignature = expectedStackSignature ?? string.Empty,
            quantity = quantity,
            pickupGridX = pickupStandPosition.x,
            pickupGridY = pickupStandPosition.y,
            destinationKind = destinationKind,
            destinationId = destinationId ?? string.Empty,
            deliveryGridX = deliveryPosition.x,
            deliveryGridY = deliveryPosition.y,
            dropGridX = dropPosition.x,
            dropGridY = dropPosition.y
        };
        string json = JsonUtility.ToJson(payload, false);
        CapturedWildlifeCapabilityState state = new()
        {
            version = CapturedWildlifeCapabilityState.CurrentEnvelopeVersion,
            roleId = CapturedWildlifeRoleIds.Haul.Value,
            payloadVersion = HaulPayloadVersion,
            payloadJson = json
        };
        if (!TryReadHaul(state, out _, out string failureReason))
        {
            throw new ArgumentException(
                "Invalid wildlife haul capability: " + failureReason);
        }
        return state;
    }

    public static bool TryRead(
        CapturedWildlifeCapabilityState state,
        out CapturedWildlifeRoleId roleId,
        out string failureReason)
    {
        roleId = default;
        failureReason = string.Empty;
        if (state == null
            || state.version
                != CapturedWildlifeCapabilityState.CurrentEnvelopeVersion
            || state.roleId == null
            || state.payloadJson == null)
        {
            failureReason = "Capability envelope is missing or has an unknown version.";
            return false;
        }

        roleId = new CapturedWildlifeRoleId(state.roleId);
        if (!roleId.IsValid
            || !string.Equals(roleId.Value, state.roleId, StringComparison.Ordinal))
        {
            failureReason = "Capability role ID is not canonical.";
            return false;
        }

        if (Readers.TryGetValue(roleId, out StateReader reader))
        {
            return reader(state, out failureReason);
        }

        failureReason = $"Unknown captured-wildlife role '{roleId.Value}'.";
        return false;
    }

    public static bool TryReadHaul(
        CapturedWildlifeCapabilityState state,
        out CapturedWildlifeHaulPayloadSnapshot assignment,
        out string failureReason)
    {
        assignment = default;
        failureReason = string.Empty;
        if (state == null
            || state.version != CapturedWildlifeCapabilityState.CurrentEnvelopeVersion
            || !string.Equals(state.roleId, CapturedWildlifeRoleIds.Haul.Value,
                StringComparison.Ordinal)
            || state.payloadVersion != HaulPayloadVersion
            || string.IsNullOrEmpty(state.payloadJson))
        {
            failureReason = "Haul capability envelope is invalid.";
            return false;
        }

        CapturedWildlifeHaulPayloadV1 payload;
        try
        {
            payload = JsonUtility.FromJson<CapturedWildlifeHaulPayloadV1>(
                state.payloadJson);
        }
        catch (Exception exception)
        {
            failureReason = "Haul capability payload is invalid JSON: "
                + exception.Message;
            return false;
        }

        if (payload == null
            || !Enum.IsDefined(typeof(CapturedWildlifeHaulPhase), payload.phase)
            || !Enum.IsDefined(typeof(CapturedWildlifeHaulDestinationKind),
                payload.destinationKind)
            || !IsFiniteNonNegative(payload.nextDecisionAt)
            || !IsCanonical(payload.operationId)
            || !IsCanonical(payload.leaseId)
            || !IsCanonical(payload.sourceStackId)
            || !IsCanonical(payload.cargoStackId)
            || !IsCanonical(payload.itemId)
            || !IsCanonical(payload.expectedStackSignature)
            || !IsCanonical(payload.destinationId))
        {
            failureReason = "Haul capability payload contains invalid values.";
            return false;
        }

        bool emptyTrip = payload.operationId.Length == 0
            && payload.leaseId.Length == 0
            && payload.sourceStackId.Length == 0
            && payload.cargoStackId.Length == 0
            && payload.itemId.Length == 0
            && payload.expectedStackSignature.Length == 0
            && payload.quantity == 0
            && payload.pickupGridX == 0
            && payload.pickupGridY == 0
            && payload.destinationId.Length == 0
            && payload.deliveryGridX == 0
            && payload.deliveryGridY == 0
            && payload.dropGridX == 0
            && payload.dropGridY == 0;
        bool commonTrip = payload.operationId.Length > 0
            && payload.sourceStackId.Length > 0
            && payload.itemId.Length > 0
            && payload.expectedStackSignature.Length > 0
            && payload.quantity > 0
            && payload.destinationId.Length > 0;
        bool phaseShape = payload.phase switch
        {
            CapturedWildlifeHaulPhase.Idle => emptyTrip,
            CapturedWildlifeHaulPhase.Reserved => commonTrip
                && payload.leaseId.Length > 0
                && payload.cargoStackId.Length == 0,
            CapturedWildlifeHaulPhase.CargoOwned => commonTrip
                && payload.leaseId.Length == 0
                && payload.cargoStackId.Length > 0,
            CapturedWildlifeHaulPhase.ReleasePending => commonTrip
                && payload.leaseId.Length == 0
                && payload.cargoStackId.Length > 0,
            _ => false
        };
        if (!phaseShape
            || !string.Equals(JsonUtility.ToJson(payload, false),
                state.payloadJson, StringComparison.Ordinal))
        {
            failureReason = "Haul capability payload has a non-canonical phase shape.";
            return false;
        }

        assignment = new CapturedWildlifeHaulPayloadSnapshot(
            payload.phase,
            payload.nextDecisionAt,
            payload.operationId,
            payload.leaseId,
            payload.sourceStackId,
            payload.cargoStackId,
            payload.itemId,
            payload.expectedStackSignature,
            payload.quantity,
            new Vector2Int(payload.pickupGridX, payload.pickupGridY),
            payload.destinationKind,
            payload.destinationId,
            new Vector2Int(payload.deliveryGridX, payload.deliveryGridY),
            new Vector2Int(payload.dropGridX, payload.dropGridY));
        return true;
    }

    public static bool TryReadCompanion(
        CapturedWildlifeCapabilityState state,
        out CharacterId ownerId,
        out float nextAttackAt,
        out string failureReason)
    {
        ownerId = default;
        nextAttackAt = 0f;
        failureReason = string.Empty;
        if (state == null
            || state.version
                != CapturedWildlifeCapabilityState.CurrentEnvelopeVersion
            || !string.Equals(
                state.roleId,
                CapturedWildlifeRoleIds.Companion.Value,
                StringComparison.Ordinal)
            || state.payloadVersion != CompanionPayloadVersion
            || string.IsNullOrEmpty(state.payloadJson))
        {
            failureReason = "Companion capability envelope is invalid.";
            return false;
        }

        CapturedWildlifeCompanionPayloadV1 payload;
        try
        {
            payload = JsonUtility.FromJson<CapturedWildlifeCompanionPayloadV1>(
                state.payloadJson);
        }
        catch (Exception exception)
        {
            failureReason = "Companion capability payload is invalid JSON: "
                + exception.Message;
            return false;
        }

        ownerId = new CharacterId(payload?.ownerCharacterId);
        nextAttackAt = payload?.nextAttackAt ?? 0f;
        if (payload == null
            || !ownerId.IsValid
            || !string.Equals(
                ownerId.Value,
                payload.ownerCharacterId,
                StringComparison.Ordinal)
            || !IsFiniteNonNegative(nextAttackAt))
        {
            failureReason = "Companion capability payload has an invalid owner or cooldown.";
            return false;
        }

        string canonical = JsonUtility.ToJson(payload, false);
        if (!string.Equals(canonical, state.payloadJson, StringComparison.Ordinal))
        {
            failureReason = "Companion capability payload is not canonical JSON.";
            return false;
        }
        return true;
    }

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool IsCanonical(string value) => value != null
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool ReadNone(
        CapturedWildlifeCapabilityState state,
        out string failureReason)
    {
        if (state.payloadVersion == 0 && state.payloadJson.Length == 0)
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason = "The explicit no-role capability must have an empty payload.";
        return false;
    }

    private static bool ReadCompanion(
        CapturedWildlifeCapabilityState state,
        out string failureReason) =>
        TryReadCompanion(state, out _, out _, out failureReason);

    private static bool ReadHaul(
        CapturedWildlifeCapabilityState state,
        out string failureReason) =>
        TryReadHaul(state, out _, out failureReason);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CapturedWildlifeState
{
    public string wildlifeId = string.Empty;
    public string speciesId = string.Empty;
    public string penId = string.Empty;
    public Vector2Int penPosition;
    public Vector2Int capturePosition;
    public string reservedCarrierId = string.Empty;
    public string assignedShowOrderId = string.Empty;
    public CapturedWildlifeTransportState transportState =
        CapturedWildlifeTransportState.AwaitingTransport;
    public bool escaped;
    public bool isTamed;
    public Vector2Int escapeDestination;
    public float nextCareAt;
    [Range(0f, 100f)] public float escapeRisk;
    public bool foodDeliveryPending;
    public bool waterDeliveryPending;
    public string lastFeedItemId = string.Empty;
    [Range(0f, 100f)] public float feedSicknessSeverity;
    [Range(0f, 1f)] public float lastFeedDiseaseChance;
    public string lastCareStatus = string.Empty;
    public int nextFeedOperationSequence;
    public int pendingFeedOperationSequence;
    public CapturedWildlifeFeedCommitPhase pendingFeedPhase;
    public string pendingFeedOperationId = string.Empty;
    public string pendingFeedReasonCode = string.Empty;
    public string pendingFeedCommitId = string.Empty;
    public List<string> pendingFeedSourceStackIds = new List<string>();
    public int pendingFeedQuantity;
    public long pendingFeedMassGrams;
    public string pendingFeedItemId = string.Empty;
    [Range(0f, 1f)] public float pendingFeedNutrition;
    [Range(0f, 1f)] public float pendingFeedDiseaseChance;
    public bool pendingFeedDiseaseTriggered;
    [Range(0f, 1f)] public float pendingFeedHungerTarget;
    [Min(0)] public int pendingFeedHealthTarget;
    [Range(0f, 100f)] public float pendingFeedSicknessTarget;
    public CapturedWildlifeCapabilityState capabilityState =
        CapturedWildlifeCapabilityState.None();

    public CapturedWildlifeState Clone()
    {
        CapturedWildlifeState clone =
            (CapturedWildlifeState)MemberwiseClone();
        clone.pendingFeedSourceStackIds = new List<string>(
            pendingFeedSourceStackIds ?? new List<string>());
        clone.capabilityState = capabilityState?.Clone();
        return clone;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CircusProgramSettlement
{
    public CircusProgramSettlement(
        float satisfaction,
        float fame,
        bool cleanupRequired,
        bool treatmentRequired,
        string message)
    {
        Satisfaction = satisfaction;
        Fame = fame;
        CleanupRequired = cleanupRequired;
        TreatmentRequired = treatmentRequired;
        Message = message ?? string.Empty;
    }

    public float Satisfaction { get; }
    public float Fame { get; }
    public bool CleanupRequired { get; }
    public bool TreatmentRequired { get; }
    public string Message { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CircusProgramForecast
{
    public CircusProgramForecast(
        int expectedRevenue,
        float minimumSatisfaction,
        float maximumSatisfaction,
        float accidentChance,
        float renown,
        float dread,
        float hostileRumor,
        float injuryChance,
        float deathChance,
        bool canSchedule,
        string participantRequirement,
        string failureReason)
    {
        ExpectedRevenue = Mathf.Max(0, expectedRevenue);
        MinimumSatisfaction = Mathf.Clamp(minimumSatisfaction, 0f, 100f);
        MaximumSatisfaction = Mathf.Clamp(maximumSatisfaction, 0f, 100f);
        AccidentChance = Mathf.Clamp01(accidentChance);
        Renown = Mathf.Max(0f, renown);
        Dread = Mathf.Max(0f, dread);
        HostileRumor = Mathf.Max(0f, hostileRumor);
        InjuryChance = Mathf.Clamp01(injuryChance);
        DeathChance = Mathf.Clamp01(deathChance);
        CanSchedule = canSchedule;
        ParticipantRequirement = participantRequirement ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public int ExpectedRevenue { get; }
    public float MinimumSatisfaction { get; }
    public float MaximumSatisfaction { get; }
    public float AccidentChance { get; }
    public float Renown { get; }
    public float Dread { get; }
    public float HostileRumor { get; }
    public float InjuryChance { get; }
    public float DeathChance { get; }
    public bool CanSchedule { get; }
    public string ParticipantRequirement { get; }
    public string FailureReason { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICircusProgramHandler
{
    CircusProgramModule Definition { get; }
    bool Validate(
        CircusShowOrder order,
        IReadOnlyList<CaptiveState> performers,
        out string failureReason);
    CircusProgramSettlement Settle(
        CircusShowOrder order,
        IReadOnlyList<CaptiveState> performers);
}
