using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public enum ItemReservationPurpose
{
    Hauling,
    Meal,
    ProductionInput,
    Construction,
    Medical,
    Equipment,
    Trade,
    FacilityBuffer,
    WasteProcessing,
    DirectPlayerOrder,
    Hygiene,
    PersonalConsumption
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldItemStackState
{
    Loose = 0,
    Stored = 1,
    FacilityBuffer = 2,
    Carried = 3,
    ExpeditionPacked = 4,
    FacilityOutputBuffer = 5,
    InTransit = 6
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldItemDropDisposition
{
    None = 0,
    TransientCarryRecoveryDrop = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldItemCarryInterruptionKind
{
    None = 0,
    Downed = 1,
    Dead = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WasteOriginKind
{
    Unknown = 0,
    Plant = 1,
    Animal = 2,
    Mixed = 3,
    Forbidden = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct EquipmentStoredEvent
{
    public EquipmentStoredEvent(string equipmentId, int quantity)
    {
        EquipmentId = equipmentId?.Trim() ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
    }

    public string EquipmentId { get; }
    public int Quantity { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ItemHaulingSettingsSnapshot
{
    public float maxCarryMultiplier = CharacterCarryTuning.DefaultMaxCarryMultiplier;

    public void Normalize()
    {
        maxCarryMultiplier = CharacterCarryTuning.ClampMaxCarryMultiplier(
            maxCarryMultiplier);
    }
}

/// <summary>
/// Single authored-code authority for character carry-capacity tuning. Character
/// performance remains the per-actor source; these values only define the
/// nominal kilogram baseline, harness projection and accessibility bounds.
/// </summary>
public static class CharacterCarryTuning
{
    public const float NominalBaseCapacityKilograms = 25f;
    public const float HaulingHarnessMultiplier = 1.25f;
    public const float DefaultMaxCarryMultiplier = 1.5f;
    public const float MinimumMaxCarryMultiplier = 1f;
    public const float MaximumMaxCarryMultiplier = 2.5f;
    public const float MinimumCapacityKilograms = 0.01f;

    public static float ResolveSoftCapacityKilograms(
        float performanceFactor,
        bool haulingHarnessEquipped)
    {
        if (float.IsNaN(performanceFactor)
            || float.IsInfinity(performanceFactor)
            || performanceFactor <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(performanceFactor),
                performanceFactor,
                "Carry-capacity performance must be finite and greater than zero.");
        }

        float capacity = NominalBaseCapacityKilograms * performanceFactor;
        if (haulingHarnessEquipped)
        {
            capacity *= HaulingHarnessMultiplier;
        }

        return Mathf.Max(MinimumCapacityKilograms, capacity);
    }

    public static float ResolveHardCapacityKilograms(
        float performanceFactor,
        bool haulingHarnessEquipped,
        float maxCarryMultiplier) =>
        ResolveSoftCapacityKilograms(
            performanceFactor,
            haulingHarnessEquipped)
        * ClampMaxCarryMultiplier(maxCarryMultiplier);

    public static float ClampMaxCarryMultiplier(float value) =>
        Mathf.Clamp(
            value,
            MinimumMaxCarryMultiplier,
            MaximumMaxCarryMultiplier);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonPhysicalItemSaveData
{
    public const int CurrentVersion = 18;

    public int version = CurrentVersion;
    public long nextHaulOperationSequence = 1;
    public ItemHaulingSettingsSnapshot haulingSettings =
        new ItemHaulingSettingsSnapshot();
    public List<WorldItemStackSaveData> stacks =
        new List<WorldItemStackSaveData>();
    public List<UniqueItemInstanceSaveData> uniqueItems = new();
    public List<ItemReservationIntentSaveData> reservationIntents = new();
    public List<PhysicalItemBatchDispositionSaveData> pendingBatchDispositions = new();
    public List<FacilityOutputExactRouteOutboxSaveData> pendingExactOutputRoutes =
        new();
    public List<ProductionPhysicalCustodyDrainSaveData>
        pendingProductionCustodyDrains = new();
    public List<ProductionInputDestinationCustodyDrainSaveData>
        pendingProductionInputDestinationDrains = new();
    public List<ProductionCapacityRoutingDrainSaveData>
        pendingCapacityRoutingDrains = new();
    public long lastConfirmedExactRouteCheckpointSequence;
    public string lastConfirmedExactRouteCheckpointDigest = string.Empty;
}

[Serializable]
public sealed class PhysicalItemBatchDispositionSaveData
{
    public int kind;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string requestFingerprint = string.Empty;
    public List<string> sourceStackIds = new();
    public int quantity;
    public long inputMassGrams;
    public string commitId = string.Empty;
}

[Serializable]
public sealed class ItemReservationClaimHintSaveData
{
    public string claimHintId = string.Empty;
    public string originStackId = string.Empty;
    public string preferredPhysicalStackId = string.Empty;
    public string itemId = string.Empty;
    public string expectedStackSignature = string.Empty;
    public int quantity;
    public ItemReservationPurpose purpose;
    public string aggregationCohortId = string.Empty;
    public int claimOrdinal;
}

public interface IItemReservationIntentSaveData
{
    string OwnerOperationId { get; }
    bool HadActiveItemReservation { get; }
    IReadOnlyList<ItemReservationClaimHintSaveData> ReservationHints { get; }
}

[Serializable]
public sealed class ItemReservationIntentSaveData :
    IItemReservationIntentSaveData
{
    public string ownerOperationId = string.Empty;
    public string ownerCharacterId = string.Empty;
    public bool hadActiveItemReservation;
    public List<ItemReservationClaimHintSaveData> reservationHints = new();

    public string OwnerOperationId => ownerOperationId;
    public bool HadActiveItemReservation => hadActiveItemReservation;
    public IReadOnlyList<ItemReservationClaimHintSaveData> ReservationHints =>
        reservationHints ?? new List<ItemReservationClaimHintSaveData>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class UniqueItemInstanceSaveData
{
    public string itemInstanceId = string.Empty;
    public string definitionId = string.Empty;
    public List<ItemInstanceComponentSaveData> components = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorldItemStackSaveData
{
    public string stackId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public WorldItemStackState state = WorldItemStackState.Loose;
    public int gridX;
    public int gridY;
    public string reservedByPersistentId = string.Empty;
    public string destinationId = string.Empty;
    public string aggregationCohortId = string.Empty;
    public string sourceStorageDestinationId = string.Empty;
    public bool hasDestinationPosition;
    public int destinationGridX;
    public int destinationGridY;
    public bool forbidden;
    public string sourceCharacterId = string.Empty;
    public string sourceDisplayName = string.Empty;
    public string sourceSpeciesTag = string.Empty;
    public string sourceDeathReason = string.Empty;
    public bool emergencyButcheryAllowed;
    public WasteOriginKind wasteOrigin;
    [Range(0f, 100f)] public float contamination;
    public List<ItemInstanceComponentSaveData> components =
        new List<ItemInstanceComponentSaveData>();
    public WorldItemDropDisposition dropDisposition;
    public string recoveryOwnerOperationId = string.Empty;
    public string recoverySourceStackId = string.Empty;
    public string recoveryCarrierPersistentId = string.Empty;
    public WorldItemCarryInterruptionKind recoveryInterruptionKind;
    public double droppedAtGameTime;
    public double recoveryDeadlineGameTime;

    public string GetStackSignature() =>
        ItemStackSignature.Create(itemId, components);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ItemStateValueKind
{
    String = 0,
    Integer = 1,
    Decimal = 2,
    Boolean = 3
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ItemStateValueSaveData
{
    public string key = string.Empty;
    public ItemStateValueKind kind;
    public string stringValue = string.Empty;
    public long integerValue;
    public double decimalValue;
    public bool booleanValue;

    public string ToCanonicalString()
    {
        string value = kind switch
        {
            ItemStateValueKind.Integer => integerValue.ToString(CultureInfo.InvariantCulture),
            ItemStateValueKind.Decimal => decimalValue.ToString("R", CultureInfo.InvariantCulture),
            ItemStateValueKind.Boolean => booleanValue ? "1" : "0",
            _ => stringValue?.Trim() ?? string.Empty
        };
        return $"{key?.Trim()}={Convert.ToInt32(kind, CultureInfo.InvariantCulture)}:{value}";
    }
}

/// <summary>
/// Versioned mutable state attached to one physical item instance or stack. Definition SOs
/// remain immutable; new systems add a component instead of widening the generic stack DTO.
/// </summary>
[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ItemInstanceComponentSaveData
{
    public string componentTypeId = string.Empty;
    [Min(1)] public int schemaVersion = 1;
    public bool affectsStacking = true;
    public List<ItemStateValueSaveData> values = new List<ItemStateValueSaveData>();

    public string ToCanonicalString()
    {
        string fields = string.Join(",", (values ?? new List<ItemStateValueSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.key, StringComparer.Ordinal)
            .Select(value => value.ToCanonicalString()));
        return $"{componentTypeId?.Trim()}@{Math.Max(1, schemaVersion)}[{fields}]";
    }

    public ItemInstanceComponentSaveData Clone() => new ItemInstanceComponentSaveData
    {
        componentTypeId = componentTypeId?.Trim() ?? string.Empty,
        schemaVersion = Math.Max(1, schemaVersion),
        affectsStacking = affectsStacking,
        values = (values ?? new List<ItemStateValueSaveData>())
            .Where(value => value != null)
            .Select(value => new ItemStateValueSaveData
            {
                key = value.key?.Trim() ?? string.Empty,
                kind = value.kind,
                stringValue = value.stringValue ?? string.Empty,
                integerValue = value.integerValue,
                decimalValue = value.decimalValue,
                booleanValue = value.booleanValue
            })
            .ToList()
    };
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ItemStackSignature
{
    public static string Create(
        string definitionId,
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        string state = string.Join("|", (components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null && component.affectsStacking)
            .OrderBy(component => component.componentTypeId, StringComparer.Ordinal)
            .Select(component => component.ToCanonicalString()));
        return $"{definitionId?.Trim() ?? string.Empty}::{state}";
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ItemInstanceComponentIds
{
    public const string Freshness = "item-state:freshness";
    public const string Durability = "item-state:durability";
    public const string Quality = "item-state:quality";
    public const string Contamination = "item-state:contamination";
    public const string Equipment = "item-state:equipment";
    public const string EquipmentModule = "item-state:equipment-module";
    public const string Provenance = "item-state:provenance";
    public const string ProductionOutputCommit = "item-state:production-output-commit";
    public const string SeedLot = "item-state:seed-lot";
    public const string FiberBatch = "item-state:fiber-batch";
    public const string Apparel = "item-state:apparel";
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class DurableToolItemRules
{
    public const string BanquetCart = "tool:banquet-cart";
    public const string ArcaneIndex = "record:arcane-index";
    public const string BreedingLedger = "record:breeding-ledger";
    public const string CareerLedger = "record:career-ledger";
    public const string SeasonalAlmanac = "book:seasonal-almanac";
    public const string WeatherObservationKit = "tool:weather-observation-kit";
    public const string InspectionGauge = "tool:inspection-gauge";
    public const string PrisonerWorkKit = "tool:prisoner-work-kit";
    public const string ReinforcedRestraint = "tool:reinforced-restraint";
    public const string RuneIdentificationLens = "tool:rune-identification-lens";
    public const string AdministrativeSeal = "tool:administrative-seal";
    public const string HaulingHarness = "tool:hauling-harness";
    public const string WatchSignalHorn = "tool:watch-signal-horn";

    public static bool TryGetMaximumDurability(string itemId, out float durability)
    {
        durability = (itemId?.Trim() ?? string.Empty) switch
        {
            BanquetCart => 120f,
            ArcaneIndex => 160f,
            BreedingLedger => 140f,
            CareerLedger => 140f,
            SeasonalAlmanac => 180f,
            WeatherObservationKit => 120f,
            InspectionGauge => 90f,
            PrisonerWorkKit => 100f,
            ReinforcedRestraint => 140f,
            RuneIdentificationLens => 80f,
            AdministrativeSeal => 160f,
            HaulingHarness => 120f,
            WatchSignalHorn => 120f,
            _ => 0f
        };
        return durability > 0f;
    }

    public static ItemInstanceComponentSaveData CreateDurability(
        string itemId,
        float current = -1f)
    {
        if (!TryGetMaximumDurability(itemId, out float maximum))
        {
            return null;
        }

        float resolvedCurrent = current < 0f
            ? maximum
            : Mathf.Clamp(current, 0f, maximum);
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.Durability,
            values = new List<ItemStateValueSaveData>
            {
                new ItemStateValueSaveData
                {
                    key = "current",
                    kind = ItemStateValueKind.Decimal,
                    decimalValue = resolvedCurrent
                },
                new ItemStateValueSaveData
                {
                    key = "maximum",
                    kind = ItemStateValueKind.Decimal,
                    decimalValue = maximum
                }
            }
        };
    }

    public static float ReadCurrentDurability(
        string itemId,
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        TryGetMaximumDurability(itemId, out float fallback);
        ItemInstanceComponentSaveData durability = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .FirstOrDefault(component => component != null
                && string.Equals(
                    component.componentTypeId,
                    ItemInstanceComponentIds.Durability,
                    StringComparison.Ordinal));
        ItemStateValueSaveData current = durability?.values?
            .FirstOrDefault(value => value != null
                && string.Equals(value.key, "current", StringComparison.Ordinal)
                && value.kind == ItemStateValueKind.Decimal);
        return Mathf.Clamp(
            current != null ? (float)current.decimalValue : fallback,
            0f,
            fallback);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldItemHaulDestinationKind
{
    Warehouse = 0,
    FacilityBuffer = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WorldItemHaulPlanUnloadReason
{
    None = 0,
    LoadLimitReached = 1,
    NoPickupCandidate = 2,
    JobChanged = 3,
    Idle = 4,
    Interrupted = 5,
    Completed = 6,

    // Appended to preserve the serialized numeric values of the original reasons.
    PickupReservationLost = 7,
    DeliveryUnavailable = 8,
    DepositRejected = 9
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCarriedItemSaveData
{
    public string carriedStackId = string.Empty;
    public string sourceStackId = string.Empty;
    public string ownerOperationId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;
    public WasteOriginKind wasteOrigin;
    [Range(0f, 100f)] public float contamination;
    public List<ItemInstanceComponentSaveData> components =
        new List<ItemInstanceComponentSaveData>();

    public string GetStackSignature() =>
        string.IsNullOrWhiteSpace(itemInstanceId)
            ? ItemStackSignature.Create(itemId, components)
            : $"{ItemStackSignature.Create(itemId, components)}#instance={itemInstanceId.Trim()}";
}

/// <summary>
/// Durable physical commitment for one carried stack in a haul delivery.
/// Runtime lease IDs are deliberately absent: restore rebinds the saved
/// operation to the newly-grandfathered lease whose slice matches this stack,
/// signature and quantity exactly.
/// </summary>
[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class HaulDeliveryItemCommitmentSaveData
{
    public string carriedStackId = string.Empty;
    public string sourceStackId = string.Empty;
    public string itemId = string.Empty;
    public string expectedStackSignature = string.Empty;
    public int quantity;
}

/// <summary>
/// Save authority for a single physical haul plan after pickup. Authored item
/// content is immutable; the operation ID, exact destination and carried
/// stack commitments are the only durable delivery intent. Pre-pickup plans
/// have no commitments and are intentionally replanned after restore.
/// </summary>
[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class HaulDeliveryIntentSaveData
{
    public string operationId = string.Empty;
    public string ownerCharacterId = string.Empty;
    public WorldItemHaulDestinationKind destinationKind;
    public string destinationId = string.Empty;
    public int deliveryGridX;
    public int deliveryGridY;
    public int dropGridX;
    public int dropGridY;
    public List<WarehouseHaulAdmissionSaveData> warehouseAdmissions = new();
    public List<HaulDeliveryItemCommitmentSaveData> commitments = new();

    public bool HasCommittedPickup => commitments != null
        && commitments.Any(commitment => commitment != null
            && commitment.quantity > 0);

    /// <summary>
    /// Unity's JSON projection may materialize an omitted optional reference as
    /// a default-constructed object.  Only this exact all-default shape means
    /// "no saved intent"; any partial identity, admission or commitment remains
    /// a malformed current-format authority and must fail validation.
    /// </summary>
    public bool IsDefaultEmptyProjection =>
        string.IsNullOrEmpty(operationId)
        && string.IsNullOrEmpty(ownerCharacterId)
        && destinationKind == WorldItemHaulDestinationKind.Warehouse
        && string.IsNullOrEmpty(destinationId)
        && deliveryGridX == 0
        && deliveryGridY == 0
        && dropGridX == 0
        && dropGridY == 0
        && (warehouseAdmissions == null || warehouseAdmissions.Count == 0)
        && (commitments == null || commitments.Count == 0);
}

/// <summary>
/// Durable projection of a destination-warehouse gram reservation owned by a
/// haul operation. Pre-pickup plans are not saved. Once pickup commits, this
/// projection is saved with the delivery intent and rebuilt before AI wake.
/// </summary>
[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WarehouseHaulAdmissionSaveData
{
    public string tokenId = string.Empty;
    public string ownerAdmissionOperationId = string.Empty;
    public string warehouseId = string.Empty;
    public string sourceWarehouseId = string.Empty;
    public string sourceStackId = string.Empty;
    public string itemId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string lotFingerprint = string.Empty;
    public int quantity;
    public long reservedMassGrams;
    public long catalogRevision;
    public long sourceRevision;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterCarryInventorySaveData
{
    public List<CharacterCarriedItemSaveData> items =
        new List<CharacterCarriedItemSaveData>();
}
