using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

public interface ICertifiedSeedCommand
{
    bool TryPlan(
        string actionId,
        string cropId,
        string facilityInstanceId,
        out DomainFailure failure);

    int CompleteDeliveredPlans();
}

/// <summary>
/// Converts one authored physical seed lot and one certified-seed kit at the
/// cultivar greenhouse. The destination id is the persistent order record, so
/// pending hauling survives save/restore without a second shadow inventory.
/// </summary>
public sealed class CertifiedSeedRuntime :
    ICertifiedSeedCommand,
    ICertifiedSeedPersistence
{
    private const string FacilityDefinitionId = "building:8893";
    private const string CertificationKitItemId = "supply:certified-seed-kit";
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IResourceEconomyContentCatalog crops;
    private readonly IStockQuery stock;
    private readonly IItemTransferService transfers;
    private readonly IPhysicalSeedLotGateway seedLots;
    private readonly Dictionary<string, CertifiedSeedOrderSaveData> orders =
        new(StringComparer.Ordinal);
    private int nextOrderSequence;

    internal IReadOnlyCollection<CertifiedSeedOrderSaveData> PhysicalOrders =>
        orders.Values;

    public CertifiedSeedRuntime(
        IFacilityCapabilityQuery facilities,
        IResourceEconomyContentCatalog crops,
        IStockQuery stock,
        IItemTransferService transfers,
        IPhysicalSeedLotGateway seedLots)
    {
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.crops = crops ?? throw new ArgumentNullException(nameof(crops));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
        this.seedLots = seedLots
            ?? throw new ArgumentNullException(nameof(seedLots));
    }

    public bool TryPlan(
        string actionId,
        string cropId,
        string facilityInstanceId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        string normalizedAction = actionId?.Trim() ?? string.Empty;
        string normalizedCrop = cropId?.Trim() ?? string.Empty;
        string normalizedFacility = facilityInstanceId?.Trim() ?? string.Empty;
        if (normalizedAction.Length == 0
            || normalizedCrop.Length == 0
            || !crops.TryGetCrop(normalizedCrop, out CropDefinitionSO crop)
            || crop == null
            || string.IsNullOrWhiteSpace(crop.SeedItemId))
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        BuildableObject facility = FindFacility(normalizedFacility);
        if (facility == null)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
            return false;
        }

        string destinationId = DestinationId(
            facility.PersistentInstanceId.Value,
            normalizedCrop,
            nextOrderSequence);
        CertifiedSeedOrderSaveData existing = orders.Values
            .FirstOrDefault(value => string.Equals(
                    value.facilityInstanceId,
                    facility.PersistentInstanceId.Value,
                    StringComparison.Ordinal)
                && string.Equals(value.cropId, normalizedCrop, StringComparison.Ordinal));
        if (existing != null)
        {
            // A persistent domain order, rather than a transient destination,
            // is the sole duplicate-planning authority.
            return true;
        }

        int orderSequence = nextOrderSequence;
        string orderId = $"certified-seed-order:{orderSequence:D8}";
        CertifiedSeedOrderSaveData order = new()
        {
            orderId = orderId,
            orderSequence = orderSequence,
            actionId = normalizedAction,
            facilityInstanceId = facility.PersistentInstanceId.Value,
            cropId = normalizedCrop,
            destinationId = destinationId,
            phase = CertifiedSeedOrderPhase.Planned
        };
        orders.Add(orderId, order);
        nextOrderSequence = checked(nextOrderSequence + 1);

        if (!seedLots.RequestBestSeedLot(
                crop.SeedItemId,
                normalizedCrop,
                facility.centerPos,
                destinationId,
                out int requestedSeed,
                out failure)
            || requestedSeed != 1)
        {
            orders.Remove(orderId);
            nextOrderSequence = orderSequence;
            return false;
        }

        if (!transfers.TryRequestItemDelivery(
                CertificationKitItemId,
                1,
                facility.centerPos,
                destinationId,
                out int requestedKit,
                out failure)
            || requestedKit != 1)
        {
            transfers.ReleaseDestination(destinationId, facility.centerPos);
            orders.Remove(orderId);
            nextOrderSequence = orderSequence;
            return false;
        }
        transfers.PrioritizeDestination(destinationId);
        return true;
    }

    public int CompleteDeliveredPlans()
    {
        int completed = 0;
        foreach (CertifiedSeedOrderSaveData order in orders.Values
                     .OrderBy(value => value.orderId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (TryComplete(order)) completed++;
        }
        return completed;
    }

    private bool TryComplete(CertifiedSeedOrderSaveData order)
    {
        if (order == null
            || !crops.TryGetCrop(order.cropId, out CropDefinitionSO crop)
            || crop == null)
        {
            return false;
        }
        BuildableObject facility = FindFacility(order.facilityInstanceId);
        if (facility == null) return false;

        Dictionary<string, int> inputs = new(StringComparer.Ordinal)
        {
            [crop.SeedItemId] = 1,
            [CertificationKitItemId] = 1
        };
        if (order.phase == CertifiedSeedOrderPhase.Planned)
        {
            if (!HasDelivered(order.destinationId, inputs)
                || !CropPhysicalTransactionOutbox.TryCommitOrResume(
                    order.pendingInput,
                    CropPhysicalTransactionOutbox.FormatCertifiedOperationId(
                        order.orderId),
                    CropPhysicalTransactionOutbox.CertifiedReasonCode,
                    order.orderSequence,
                    order.destinationId,
                    inputs,
                    crop.SeedItemId,
                    order.cropId,
                    seedLots,
                    out SeedLotState source,
                    out _))
            {
                return false;
            }
            SeedLotState certified = source.Clone();
            certified.pathogenLoad = Mathf.Clamp(
                certified.pathogenLoad - 30f,
                0f,
                100f);
            order.certifiedSeedLot = certified;
            order.phase = CertifiedSeedOrderPhase.InputCommitted;
        }

        if (order.phase == CertifiedSeedOrderPhase.InputCommitted)
        {
            string outputOperationId = "certified-seed-output:"
                + order.orderId;
            string outputDestinationId = "certified-seed-output|"
                + Uri.EscapeDataString(order.facilityInstanceId);
            if (!seedLots.TryEnsureSeedLotOutput(
                    crop.SeedItemId,
                    order.certifiedSeedLot,
                    facility.centerPos,
                    WorldItemStackState.FacilityOutputBuffer,
                    outputDestinationId,
                    outputOperationId,
                    out string outputCommitId,
                    out _))
            {
                return false;
            }
            order.outputOperationId = outputOperationId;
            order.outputCommitId = outputCommitId;
            order.phase = CertifiedSeedOrderPhase.OutputPublished;
            order.pendingInput.phase = CropPhysicalCommitPhase.OutcomePublished;
        }

        if (order.phase != CertifiedSeedOrderPhase.OutputPublished
            || !CropPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                order.pendingInput,
                seedLots,
                out _))
        {
            return false;
        }
        return orders.Remove(order.orderId);
    }

    private bool HasDelivered(
        string destinationId,
        IReadOnlyDictionary<string, int> requirements) =>
        requirements.All(requirement => stock.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && value.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ItemId,
                    requirement.Key,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity) >= requirement.Value);

    public CertifiedSeedWorldSaveData Capture() => new()
    {
        nextOrderSequence = nextOrderSequence,
        orders = orders.Values
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .Select(value => value.DeepClone())
            .ToList()
    };

    public CertifiedSeedRestoreCandidate BuildRestore(
        CertifiedSeedWorldSaveData snapshot)
    {
        if (snapshot == null
            || snapshot.version != CertifiedSeedWorldSaveData.CurrentVersion
            || snapshot.nextOrderSequence < 0
            || snapshot.orders == null
            || snapshot.orders.Count > 256)
            throw new InvalidOperationException(
                "Certified-seed payload is missing or invalid.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        List<CertifiedSeedOrderSaveData> restored = new();
        foreach (CertifiedSeedOrderSaveData source in snapshot.orders)
        {
            CertifiedSeedOrderSaveData order = source?.DeepClone()
                ?? throw new InvalidOperationException(
                    "Certified-seed payload contains a null order.");
            ValidateOrder(order);
            if (!ids.Add(order.orderId))
                throw new InvalidOperationException(
                    "Certified-seed order IDs are duplicated.");
            restored.Add(order);
        }
        if (restored.Any(value => value.orderSequence >= snapshot.nextOrderSequence))
            throw new InvalidOperationException(
                "Certified-seed next sequence does not dominate active orders.");
        return new CertifiedSeedRestoreCandidate(
            snapshot.nextOrderSequence,
            restored);
    }

    public void Restore(CertifiedSeedRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        orders.Clear();
        foreach (CertifiedSeedOrderSaveData order in candidate.Orders)
            orders.Add(order.orderId, order.DeepClone());
        nextOrderSequence = candidate.NextOrderSequence;
    }

    private void ValidateOrder(CertifiedSeedOrderSaveData order)
    {
        if (!IsCanonical(order.orderId)
            || order.orderSequence < 0
            || !IsCanonical(order.actionId)
            || !IsCanonical(order.facilityInstanceId)
            || !IsCanonical(order.cropId)
            || !IsCanonical(order.destinationId)
            || !TryParseDestination(
                order.destinationId,
                out string facilityId,
                out string cropId,
                out int sequence)
            || sequence != order.orderSequence
            || !string.Equals(facilityId, order.facilityInstanceId, StringComparison.Ordinal)
            || !string.Equals(cropId, order.cropId, StringComparison.Ordinal)
            || !crops.TryGetCrop(order.cropId, out CropDefinitionSO crop)
            || crop == null
            || order.pendingInput == null)
            throw new InvalidOperationException(
                "Certified-seed order provenance is invalid.");
        if (order.phase == CertifiedSeedOrderPhase.Planned)
        {
            if (order.pendingInput.phase != CropPhysicalCommitPhase.None
                || order.certifiedSeedLot != null
                || order.outputOperationId.Length != 0
                || order.outputCommitId.Length != 0)
                throw new InvalidOperationException(
                    "Planned certified-seed order contains committed state.");
            return;
        }
        Dictionary<string, int> requirements = new(StringComparer.Ordinal)
        {
            [crop.SeedItemId] = 1,
            [CertificationKitItemId] = 1
        };
        if (!CropPhysicalTransactionOutbox.ValidateProvenance(
                order.pendingInput,
                CropPhysicalTransactionOutbox.FormatCertifiedOperationId(
                    order.orderId),
                CropPhysicalTransactionOutbox.CertifiedReasonCode,
                order.orderSequence,
                order.destinationId,
                requirements,
                crop.SeedItemId,
                order.cropId,
                out string failureReason)
            || order.certifiedSeedLot == null)
            throw new InvalidOperationException(
                "Certified-seed physical owner is invalid: " + failureReason);
        bool outputPublished = order.phase == CertifiedSeedOrderPhase.OutputPublished;
        if (outputPublished !=
                (order.pendingInput.phase == CropPhysicalCommitPhase.OutcomePublished)
            || outputPublished != IsCanonical(order.outputOperationId)
            || outputPublished != IsCanonical(order.outputCommitId))
            throw new InvalidOperationException(
                "Certified-seed output state contradicts its input owner.");
    }

    private BuildableObject FindFacility(string facilityInstanceId) =>
        facilities.FindOperational(
                FacilityCapabilityKind.None,
                FacilityDefinitionId)
            .FirstOrDefault(value => string.IsNullOrWhiteSpace(facilityInstanceId)
                || string.Equals(
                    value.PersistentInstanceId.Value,
                    facilityInstanceId,
                    StringComparison.Ordinal));

    private static string DestinationId(
        string facilityId,
        string cropId,
        int sequence) =>
        string.Join(
            "|",
            "certified-seed",
            Uri.EscapeDataString(facilityId?.Trim() ?? string.Empty),
            Uri.EscapeDataString(cropId?.Trim() ?? string.Empty),
            Math.Max(0, sequence).ToString(
                "D8",
                System.Globalization.CultureInfo.InvariantCulture));

    private static bool TryParseDestination(
        string destinationId,
        out string facilityId,
        out string cropId,
        out int sequence)
    {
        facilityId = string.Empty;
        cropId = string.Empty;
        sequence = -1;
        string[] parts = (destinationId ?? string.Empty).Split('|');
        if (parts.Length != 4
            || !string.Equals(parts[0], "certified-seed", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            facilityId = Uri.UnescapeDataString(parts[1]);
            cropId = Uri.UnescapeDataString(parts[2]);
            return facilityId.Length > 0
                && cropId.Length > 0
                && int.TryParse(
                    parts[3],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out sequence)
                && sequence >= 0;
        }
        catch (UriFormatException)
        {
            facilityId = string.Empty;
            cropId = string.Empty;
            sequence = -1;
            return false;
        }
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

/// <summary>
/// Completes hauled certification orders and exposes the player action through
/// the existing persistent event-alert UI. Two alerts are used because the
/// alert model intentionally caps one card at four choices.
/// </summary>
public sealed class CertifiedSeedApplicationAdapter : IStartable, IDisposable
{
    private readonly ICertifiedSeedCommand commands;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IResourceEconomyContentCatalog crops;
    private readonly IGameEventBus events;
    private IDisposable daySubscription;

    public CertifiedSeedApplicationAdapter(
        ICertifiedSeedCommand commands,
        IFacilityCapabilityQuery facilities,
        IResourceEconomyContentCatalog crops,
        IGameEventBus events)
    {
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.crops = crops ?? throw new ArgumentNullException(nameof(crops));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start() => daySubscription ??=
        events.Subscribe<OperatingDayStartedEvent>(OnDayStarted);

    public void Dispose()
    {
        daySubscription?.Dispose();
        daySubscription = null;
    }

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        commands.CompleteDeliveredPlans();
        BuildableObject facility = facilities.FindOperational(
                FacilityCapabilityKind.None,
                FacilityDefinitionId)
            .FirstOrDefault();
        if (facility == null) return;

        CropDefinitionSO[] authored = crops.Crops
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.SeedItemId))
            .OrderBy(value => value.CropId, StringComparer.Ordinal)
            .ToArray();
        for (int offset = 0; offset < authored.Length; offset += 4)
        {
            EventAlertChoice[] choices = authored.Skip(offset).Take(4)
                .Select(crop => new EventAlertChoice(
                    crop.DisplayName,
                    "기존 종자 로트와 인증 꾸러미를 운반해 품질을 높이고 병원체 부하를 낮춥니다.",
                    V21ContentAlertActionIds.CertifiedSeed(
                        crop.CropId,
                        facility.PersistentInstanceId.Value)))
                .ToArray();
            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "인증 품종 종자 생산",
                "육종 온실에서 인증할 작물을 선택하십시오. 재료가 실제로 도착한 뒤 종자 로트가 배출됩니다.",
                EventAlertImportance.Medium,
                "V21 농업",
                choices,
                $"certified-seed:{facility.PersistentInstanceId.Value}:{offset / 4}")));
        }
    }

    private const string FacilityDefinitionId = "building:8893";
}
