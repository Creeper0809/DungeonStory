using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SurgicalPartRuntime :
    ISurgicalPartRuntime,
    ISurgicalAugmentationQuery,
    ITickable
{
    private const float SecondsPerDay = 180f;
    private const float LooseFreshnessSeconds = SecondsPerDay * 2f;
    private const float StoredFreshnessRate = 2f / 15f;
    private const float FuelRefreshInterval = 0.75f;
    private const string FuelDestinationPrefix =
        "surgery-organ-storage-fuel:";
    private const string OrganPreservationCanisterItemId =
        "medical:organ-preservation-canister";

    private readonly IWorldItemStackRuntime items;
    private readonly IBuildingWorldQuery buildings;
    private readonly ISurgicalFacilityQuery facilities;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly IGameClock clock;
    private readonly SurgeryAggregateStateStore stateStore;
    private float nextFuelRefreshAt;

    private List<SurgicalPartInstance> parts => stateStore.State.Parts;
    private Dictionary<string, SurgicalOrganStorageState> storageStates =>
        stateStore.State.OrganStorage;
    private int sequence
    {
        get => stateStore.State.PartSequence;
        set => stateStore.State.PartSequence = value;
    }

    public SurgicalPartRuntime(
        IWorldItemStackRuntime items,
        IBuildingWorldQuery buildings,
        ISurgicalFacilityQuery facilities,
        IAnatomyProfileCatalog anatomyProfiles,
        IGameClock clock,
        SurgeryAggregateStateStore stateStore)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public IReadOnlyList<SurgicalPartInstance> Parts => parts;

    public void Tick()
    {
        if (!clock.IsPaused && clock.DeltaTime > 0f)
        {
            TickOrganStorageFuel(clock.DeltaTime);
            TickFreshness(clock.DeltaTime);
        }
    }

    public bool TryGet(string partInstanceId, out SurgicalPartInstance part)
    {
        part = parts.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.partInstanceId,
                partInstanceId?.Trim(),
                StringComparison.Ordinal));
        return part != null;
    }

    public bool TryCreateExtractedPart(
        SurgicalSubjectRef donor,
        string nodeId,
        SurgicalPartKind kind,
        float quality,
        Vector2Int position,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        part = null;
        failure = DomainFailure.None;
        if (donor == null || !donor.IsValid || string.IsNullOrWhiteSpace(nodeId))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeMissing,
                nodeId ?? string.Empty);
            return false;
        }

        SurgeryAggregateState state = stateStore.State;
        if (!state.TryPrepareNextPartIdentity(
                out int nextPartSequence,
                out string partInstanceId,
                out failure))
        {
            return false;
        }

        string itemId = kind == SurgicalPartKind.Prosthetic
            ? SurgeryItemDefinitions.GetProstheticItemId(nodeId)
            : SurgeryItemDefinitions.GetOrganItemId(nodeId);
        if (!items.SpawnUniqueItemAt(
                itemId,
                position,
                WorldItemStackState.Loose,
                string.Empty,
                out string stackId))
        {
            failure = new DomainFailure(FailureCode.SurgeryEffectFailed, itemId);
            return false;
        }

        part = new SurgicalPartInstance
        {
            partInstanceId = partInstanceId,
            kind = kind,
            nodeId = nodeId.Trim(),
            displayName = items.CatalogProvider.GetDefinition(itemId).DisplayName,
            donorId = donor.subjectId,
            donorName = donor.displayName,
            donorSpeciesId = donor.speciesId,
            anatomyFamily = ResolveAnatomyFamily(donor),
            quality = Mathf.Clamp(quality, 0.1f, 1.75f),
            specialEffectId = ResolveSpecialEffectId(
                donor.speciesId,
                nodeId),
            specialEffectStrength = ResolveSpecialEffectStrength(
                donor.speciesId,
                nodeId),
            freshnessSeconds = kind == SurgicalPartKind.NaturalOrgan
                ? LooseFreshnessSeconds
                : float.PositiveInfinity,
            worldStackId = stackId
        };
        sequence = nextPartSequence;
        parts.Add(part);
        RequestOrganStorage(part, position);
        return true;
    }

    public string GetSpecialEffectLabel(SurgicalPartInstance part)
    {
        return part?.specialEffectId switch
        {
            "graft:rune-deer-night-sight" => "룬사슴의 야간 시야",
            "graft:shadow-wolf-endurance" => "그림자늑대의 지구력",
            "graft:moss-boar-toughness" => "이끼멧돼지의 강인함",
            _ => string.Empty
        };
    }

    public bool TryCreateCraftedPart(
        string nodeId,
        string displayName,
        SurgicalPartKind kind,
        float quality,
        Vector2Int position,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        part = null;
        failure = DomainFailure.None;
        if (string.IsNullOrWhiteSpace(nodeId)
            || kind == SurgicalPartKind.NaturalOrgan)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeMissing,
                nodeId ?? string.Empty);
            return false;
        }

        SurgeryAggregateState state = stateStore.State;
        if (!state.TryPrepareNextPartIdentity(
                out int nextPartSequence,
                out string partInstanceId,
                out failure))
        {
            return false;
        }

        string itemId = SurgeryItemDefinitions.GetProstheticItemId(nodeId);
        if (!items.SpawnUniqueItemAt(
                itemId,
                position,
                WorldItemStackState.Loose,
                string.Empty,
                out string stackId))
        {
            failure = new DomainFailure(FailureCode.SurgeryEffectFailed, itemId);
            return false;
        }

        DungeonItemDefinition definition = items.CatalogProvider.GetDefinition(itemId);
        part = new SurgicalPartInstance
        {
            partInstanceId = partInstanceId,
            kind = kind,
            nodeId = nodeId.Trim(),
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? definition.DisplayName
                : displayName.Trim(),
            donorId = string.Empty,
            donorName = "제작품",
            donorSpeciesId = string.Empty,
            anatomyFamily = "humanoid",
            quality = Mathf.Clamp(quality, 0.1f, 1.75f),
            freshnessSeconds = float.PositiveInfinity,
            worldStackId = stackId
        };
        sequence = nextPartSequence;
        parts.Add(part);
        return true;
    }

    public bool TryReserveForOrder(
        string partInstanceId,
        string orderId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGet(partInstanceId, out SurgicalPartInstance part)
            || part.installed)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(part.reservedOrderId)
            && !string.Equals(part.reservedOrderId, orderId, StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                part.reservedOrderId);
            return false;
        }

        if (part.kind == SurgicalPartKind.NaturalOrgan
            && part.freshnessSeconds <= 0f)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryCorpseStale,
                partInstanceId ?? string.Empty);
            return false;
        }

        part.reservedOrderId = orderId ?? string.Empty;
        return true;
    }

    public void ReleaseReservation(string partInstanceId, string orderId)
    {
        if (TryGet(partInstanceId, out SurgicalPartInstance part)
            && string.Equals(part.reservedOrderId, orderId, StringComparison.Ordinal))
        {
            part.reservedOrderId = string.Empty;
        }
    }

    public bool TryConsumeForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGet(partInstanceId, out part)
            || part.installed
            || !string.Equals(part.reservedOrderId, orderId, StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(part.worldStackId)
            && !items.TryConsumeStackQuantity(part.worldStackId, 1, out _))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        part.installed = true;
        part.installedSubjectId = subjectId ?? string.Empty;
        part.worldStackId = string.Empty;
        part.storedFacilityId = string.Empty;
        part.reservedOrderId = string.Empty;
        return true;
    }

    public void TickFreshness(float deltaTime)
    {
        if (deltaTime <= 0f || parts.Count == 0)
        {
            return;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks = items.GetAllStacks();
        Dictionary<string, WorldItemStackSnapshot> byStack = stacks
            .Where(stack => stack != null)
            .ToDictionary(stack => stack.StackId, StringComparer.Ordinal);
        List<SurgicalPartInstance> expired = null;
        foreach (SurgicalPartInstance part in parts)
        {
            if (part == null
                || part.installed
                || part.kind != SurgicalPartKind.NaturalOrgan
                || float.IsPositiveInfinity(part.freshnessSeconds))
            {
                continue;
            }

            string storageId = string.Empty;
            bool inWorkingStorage = byStack.TryGetValue(
                    part.worldStackId,
                    out WorldItemStackSnapshot stack)
                && IsInWorkingOrganStorage(stack, out storageId);
            bool preserved = inWorkingStorage
                && TryEnsurePreservationCanister(part, stack, storageId);
            part.storedFacilityId = preserved ? storageId : string.Empty;
            part.freshnessSeconds -= deltaTime
                * (preserved ? StoredFreshnessRate : 1f);
            if (part.freshnessSeconds <= 0f)
            {
                expired ??= new List<SurgicalPartInstance>();
                expired.Add(part);
            }
        }

        foreach (SurgicalPartInstance part in expired
                     ?? Enumerable.Empty<SurgicalPartInstance>())
        {
            Vector2Int position = default;
            WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(
                candidate => candidate != null
                    && candidate.StackId == part.worldStackId);
            if (stack != null)
            {
                position = stack.Position;
                items.DeleteStack(stack.StackId);
                items.SpawnItemAt(
                    SurgeryItemDefinitions.ContaminatedTissueId,
                    1,
                    position,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out _);
            }

            parts.Remove(part);
        }
    }

    private bool TryEnsurePreservationCanister(
        SurgicalPartInstance part,
        WorldItemStackSnapshot organStack,
        string storageId)
    {
        if (part.preservationCanisterApplied)
        {
            return true;
        }

        if (items.TryConsumeFacilityItemBuffer(
                storageId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [OrganPreservationCanisterItemId] = 1
                },
                out _))
        {
            part.preservationCanisterApplied = true;
            return true;
        }

        bool deliveryPending = items.GetAllStacks().Any(candidate =>
            candidate != null
            && string.Equals(
                candidate.ItemId,
                OrganPreservationCanisterItemId,
                StringComparison.Ordinal)
            && string.Equals(
                candidate.DestinationId,
                storageId,
                StringComparison.Ordinal));
        if (!deliveryPending)
        {
            items.TryRequestItemDelivery(
                OrganPreservationCanisterItemId,
                1,
                organStack.Position,
                storageId,
                out _,
                out _);
        }

        return false;
    }

    public IReadOnlyList<SurgicalPartInstance> CaptureParts()
    {
        return parts.Select(SurgeryStateCloner.ClonePart).ToArray();
    }

    public IReadOnlyList<SurgicalOrganStorageState> CaptureStorageStates()
    {
        return storageStates.Values
            .Where(state => state != null
                && !string.IsNullOrWhiteSpace(state.facilityId))
            .OrderBy(state => state.facilityId, StringComparer.Ordinal)
            .Select(state => state.Clone())
            .ToArray();
    }

    public bool TryGetOrganStorageStatus(
        BuildableObject storage,
        out SurgicalOrganStorageSnapshot snapshot)
    {
        snapshot = default;
        BuildingOrganStorageAbility ability =
            storage?.BuildingData?.GetAbility<BuildingOrganStorageAbility>();
        if (storage == null || ability == null)
        {
            return false;
        }

        string facilityId = facilities.GetFacilityId(storage);
        SurgicalOrganStorageState state = GetOrCreateStorageState(facilityId);
        int stored = CountPartsRoutedTo(facilityId);
        bool powered = ability.fuelPerDay <= 0
            || state.fuelSecondsRemaining > 0.001f;
        snapshot = new SurgicalOrganStorageSnapshot(
            facilityId,
            stored,
            ability.capacity,
            powered,
            state.fuelSecondsRemaining);
        return true;
    }

    private void RequestOrganStorage(
        SurgicalPartInstance part,
        Vector2Int origin)
    {
        Dictionary<string, int> routedCounts = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.ItemId.StartsWith(
                    SurgeryItemDefinitions.OrganPrefix,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(stack.DestinationId))
            .GroupBy(stack => stack.DestinationId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(stack => stack.Quantity),
                StringComparer.Ordinal);
        BuildableObject storage = buildings.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && !building.IsDamaged
                && building.BuildingData?
                    .GetAbility<BuildingOrganStorageAbility>() != null)
            .Where(building =>
            {
                string id = facilities.GetFacilityId(building);
                routedCounts.TryGetValue(id, out int count);
                int capacity = building.BuildingData
                    .GetAbility<BuildingOrganStorageAbility>()
                    .capacity;
                return count < Mathf.Max(1, capacity);
            })
            .OrderBy(building =>
                Mathf.Abs(building.centerPos.x - origin.x)
                + Mathf.Abs(building.centerPos.y - origin.y))
            .FirstOrDefault();
        if (storage == null)
        {
            return;
        }

        string destinationId = facilities.GetFacilityId(storage);
        if (items.TryRequestStackDelivery(
                part.worldStackId,
                1,
                storage.centerPos,
                destinationId,
                out _,
                out _))
        {
            part.storedFacilityId = string.Empty;
        }
    }

    private bool IsInWorkingOrganStorage(
        WorldItemStackSnapshot stack,
        out string storageId)
    {
        storageId = string.Empty;
        if (stack == null || string.IsNullOrWhiteSpace(stack.DestinationId))
        {
            return false;
        }

        BuildableObject storage = buildings.Buildings.FirstOrDefault(building =>
            building != null
            && !building.isDestroy
            && !building.IsDamaged
            && building.BuildingData?.GetAbility<BuildingOrganStorageAbility>() != null
            && string.Equals(
                facilities.GetFacilityId(building),
                stack.DestinationId,
                StringComparison.Ordinal));
        if (storage == null)
        {
            return false;
        }

        BuildingOrganStorageAbility storageAbility =
            storage.BuildingData.GetAbility<BuildingOrganStorageAbility>();
        SurgicalOrganStorageState state = GetOrCreateStorageState(
            stack.DestinationId);
        if (storageAbility.fuelPerDay > 0
            && state.fuelSecondsRemaining <= 0.001f)
        {
            return false;
        }

        storageId = stack.DestinationId;
        return true;
    }

    private void TickOrganStorageFuel(float deltaTime)
    {
        foreach (SurgicalOrganStorageState state in storageStates.Values)
        {
            if (state != null && state.fuelSecondsRemaining > 0f)
            {
                state.fuelSecondsRemaining = Mathf.Max(
                    0f,
                    state.fuelSecondsRemaining - deltaTime);
            }
        }

        if (clock.Time < nextFuelRefreshAt)
        {
            return;
        }

        nextFuelRefreshAt = clock.Time + FuelRefreshInterval;
        foreach (BuildableObject storage in buildings.Buildings.Where(building =>
                     building != null
                     && !building.isDestroy
                     && !building.IsDamaged
                     && building.BuildingData?
                         .GetAbility<BuildingOrganStorageAbility>() != null))
        {
            BuildingOrganStorageAbility ability =
                storage.BuildingData.GetAbility<BuildingOrganStorageAbility>();
            string facilityId = facilities.GetFacilityId(storage);
            SurgicalOrganStorageState state = GetOrCreateStorageState(facilityId);
            if (ability.fuelPerDay <= 0)
            {
                state.fuelSecondsRemaining = SecondsPerDay;
                state.fuelDeliveryRequested = false;
                continue;
            }

            string destinationId = GetFuelDestinationId(facilityId);
            if (state.fuelSecondsRemaining <= SecondsPerDay * 0.25f
                && items.TryConsumeFacilityBuffer(
                    destinationId,
                    new Dictionary<StockCategory, int>
                    {
                        [StockCategory.Fuel] = 1
                    },
                    out _))
            {
                state.fuelSecondsRemaining +=
                    SecondsPerDay / Mathf.Max(1, ability.fuelPerDay);
                state.fuelDeliveryRequested = false;
            }

            int routedFuel = items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)
                    && stack.StockCategory == StockCategory.Fuel)
                .Sum(stack => stack.Quantity);
            state.fuelDeliveryRequested = routedFuel > 0;
            if (state.fuelSecondsRemaining <= SecondsPerDay * 0.5f
                && routedFuel == 0)
            {
                state.fuelDeliveryRequested =
                    items.TryRequestFacilityDelivery(
                        StockCategory.Fuel,
                        1,
                        storage.centerPos,
                        destinationId,
                        out int requested,
                        out _)
                    && requested > 0;
            }
        }
    }

    private SurgicalOrganStorageState GetOrCreateStorageState(
        string facilityId)
    {
        string normalized = facilityId?.Trim() ?? string.Empty;
        if (!storageStates.TryGetValue(
                normalized,
                out SurgicalOrganStorageState state))
        {
            state = new SurgicalOrganStorageState
            {
                facilityId = normalized
            };
            storageStates.Add(normalized, state);
        }

        return state;
    }

    private int CountPartsRoutedTo(string destinationId)
    {
        return items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && stack.ItemId.StartsWith(
                    SurgeryItemDefinitions.OrganPrefix,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
    }

    private static string GetFuelDestinationId(string facilityId)
    {
        return FuelDestinationPrefix + (facilityId?.Trim() ?? string.Empty);
    }

    private string ResolveAnatomyFamily(SurgicalSubjectRef donor)
    {
        if (!string.IsNullOrWhiteSpace(donor.anatomyProfileId)
            && anatomyProfiles.TryGet(
                donor.anatomyProfileId,
                out AnatomyProfileDefinition profile))
        {
            return profile.AnatomyFamily;
        }

        return anatomyProfiles.GetForSpecies(donor.speciesId).AnatomyFamily;
    }

    private static string ResolveSpecialEffectId(
        string speciesId,
        string nodeId)
    {
        if (string.Equals(speciesId, "rune_deer", StringComparison.OrdinalIgnoreCase)
            && nodeId?.StartsWith("eye:", StringComparison.Ordinal) == true)
        {
            return "graft:rune-deer-night-sight";
        }

        if (string.Equals(speciesId, "shadow_wolf", StringComparison.OrdinalIgnoreCase)
            && nodeId?.StartsWith("lung:", StringComparison.Ordinal) == true)
        {
            return "graft:shadow-wolf-endurance";
        }

        if (string.Equals(speciesId, "moss_boar", StringComparison.OrdinalIgnoreCase)
            && string.Equals(nodeId, "heart", StringComparison.Ordinal))
        {
            return "graft:moss-boar-toughness";
        }

        return string.Empty;
    }

    private static float ResolveSpecialEffectStrength(
        string speciesId,
        string nodeId)
    {
        return string.IsNullOrWhiteSpace(
            ResolveSpecialEffectId(speciesId, nodeId))
                ? 0f
                : 1f;
    }
}
