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
public sealed class CertifiedSeedRuntime : ICertifiedSeedCommand
{
    private const string FacilityDefinitionId = "building:8893";
    private const string CertificationKitItemId = "supply:certified-seed-kit";
    private const string DestinationPrefix = "certified-seed|";
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IResourceEconomyContentCatalog crops;
    private readonly IStockQuery stock;
    private readonly IItemTransferService transfers;
    private readonly IPhysicalSeedLotGateway seedLots;

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
            normalizedCrop);
        if (stock.GetAllStacks().Any(value => value != null
                && value.Quantity > 0
                && string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)))
        {
            // The persisted physical delivery already represents this order.
            return true;
        }

        if (!seedLots.RequestBestSeedLot(
                crop.SeedItemId,
                normalizedCrop,
                facility.centerPos,
                destinationId,
                out int requestedSeed,
                out failure)
            || requestedSeed != 1)
        {
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
            return false;
        }
        transfers.PrioritizeDestination(destinationId);
        return true;
    }

    public int CompleteDeliveredPlans()
    {
        string[] destinations = stock.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && value.State == WorldItemStackState.FacilityBuffer
                && (value.DestinationId?.StartsWith(
                    DestinationPrefix,
                    StringComparison.Ordinal) ?? false))
            .Select(value => value.DestinationId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        int completed = 0;
        foreach (string destinationId in destinations)
        {
            if (TryComplete(destinationId)) completed++;
        }
        return completed;
    }

    private bool TryComplete(string destinationId)
    {
        if (!TryParseDestination(
                destinationId,
                out string facilityInstanceId,
                out string cropId)
            || !crops.TryGetCrop(cropId, out CropDefinitionSO crop)
            || crop == null)
        {
            return false;
        }
        BuildableObject facility = FindFacility(facilityInstanceId);
        if (facility == null) return false;

        Dictionary<string, int> inputs = new(StringComparer.Ordinal)
        {
            [crop.SeedItemId] = 1,
            [CertificationKitItemId] = 1
        };
        if (!seedLots.TryConsumeSowingInputs(
                destinationId,
                inputs,
                crop.SeedItemId,
                cropId,
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
        return seedLots.SpawnSeedLot(
            crop.SeedItemId,
            1,
            certified,
            facility.centerPos);
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

    private static string DestinationId(string facilityId, string cropId) =>
        string.Join(
            "|",
            "certified-seed",
            Uri.EscapeDataString(facilityId?.Trim() ?? string.Empty),
            Uri.EscapeDataString(cropId?.Trim() ?? string.Empty));

    private static bool TryParseDestination(
        string destinationId,
        out string facilityId,
        out string cropId)
    {
        facilityId = string.Empty;
        cropId = string.Empty;
        string[] parts = (destinationId ?? string.Empty).Split('|');
        if (parts.Length != 3
            || !string.Equals(parts[0], "certified-seed", StringComparison.Ordinal))
        {
            return false;
        }
        try
        {
            facilityId = Uri.UnescapeDataString(parts[1]);
            cropId = Uri.UnescapeDataString(parts[2]);
            return facilityId.Length > 0 && cropId.Length > 0;
        }
        catch (UriFormatException)
        {
            facilityId = string.Empty;
            cropId = string.Empty;
            return false;
        }
    }
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
