using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal enum SurvivalTreatmentSupplyStatus
{
    Missing = 0,
    DeliveryRequested = 1,
    DeliveryInTransit = 2,
    Ready = 3,
    CapacityUnavailable = 4,
    DeliveryNoPath = 5,
    Processing = 6,
    AwaitingRequest = 7
}

internal enum SurvivalTreatmentCapacityProbe
{
    AuthorityUnavailable = 0,
    Unavailable = 1,
    Available = 2
}

internal readonly struct SurvivalTreatmentMaterialSelection
{
    internal SurvivalTreatmentMaterialSelection(
        string itemId,
        ItemStackId stackId,
        string destinationId,
        bool usedBloodSubstitute,
        SurvivalTreatmentSupplyStatus status)
    {
        ItemId = itemId ?? string.Empty;
        StackId = stackId;
        DestinationId = destinationId ?? string.Empty;
        UsedBloodSubstitute = usedBloodSubstitute;
        Status = status;
    }

    internal string ItemId { get; }
    internal ItemStackId StackId { get; }
    internal string DestinationId { get; }
    internal bool UsedBloodSubstitute { get; }
    internal SurvivalTreatmentSupplyStatus Status { get; }
    internal bool IsReady => Status == SurvivalTreatmentSupplyStatus.Ready
        && ItemId.Length > 0
        && StackId.IsValid
        && DestinationId.Length > 0;
}

internal sealed class SurvivalFoodStockRuntime
{
    internal const string FacilityFuelOwnerDomain = "survival.environment-fuel";
    internal const string FacilityFuelDispositionReason =
        "survival-facility-fuel-combustion";
    private const long FacilityFuelCapacitySchemaRevision = 1L;
    private const string FacilityFuelDestinationPrefix =
        ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
        + FacilityFuelOwnerDomain + ":";

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly IStockQuery stockQuery;
    private readonly IPhysicalFacilityItemSinkGateway physicalSinks;
    private readonly IPackagedLotTareDispositionService packagedTare;
    private readonly IWorkforceReplanService workforce;
    private readonly IFacilityBufferMassCapacityQuery treatmentBufferCapacities;
    private readonly IFacilityBufferPhysicalOccupancyQuery treatmentBufferOccupancy;
    private readonly IFacilityBufferDestinationClaimQuery treatmentBufferClaims;
    private readonly IFacilityBufferDestinationLifecycleCommand fuelBufferLifecycle;
    private readonly IFacilityBufferDestinationReleaseService fuelBufferRelease;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private IReadOnlyList<WorldItemStackSnapshot> cachedItemStacks =
        Array.Empty<WorldItemStackSnapshot>();
    private int cachedItemStackVersion = -1;
    private int fuelAuthorityBuildingVersion = int.MinValue;

    public SurvivalFoodStockRuntime(
        IGridSystemProvider gridSystemProvider,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldItemStackRuntime itemStackRuntime,
        IItemDefinitionCatalog itemCatalog,
        IStockQuery stockQuery,
        IPhysicalFacilityItemSinkGateway physicalSinks = null,
        IPackagedLotTareDispositionService packagedTare = null,
        IWorkforceReplanService workforce = null,
        IFacilityBufferMassCapacityQuery treatmentBufferCapacities = null,
        IFacilityBufferPhysicalOccupancyQuery treatmentBufferOccupancy = null,
        IFacilityBufferDestinationClaimQuery treatmentBufferClaims = null,
        IFacilityBufferDestinationLifecycleCommand fuelBufferLifecycle = null,
        IFacilityBufferDestinationReleaseService fuelBufferRelease = null,
        DungeonRuntimeAggregateRootStore aggregateRootStore = null)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.itemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        this.stockQuery = stockQuery
            ?? throw new ArgumentNullException(nameof(stockQuery));
        this.physicalSinks = physicalSinks;
        this.packagedTare = packagedTare;
        this.workforce = workforce;
        this.treatmentBufferCapacities = treatmentBufferCapacities;
        this.treatmentBufferOccupancy = treatmentBufferOccupancy;
        this.treatmentBufferClaims = treatmentBufferClaims;
        this.fuelBufferLifecycle = fuelBufferLifecycle;
        this.fuelBufferRelease = fuelBufferRelease;
        this.aggregateRootStore = aggregateRootStore;
    }

    public bool HasPotentialFacilityFuel(BuildableObject facility)
    {
        if (!TryGetFacilityFuelAbility(
                facility,
                out BuildingFuelConsumerAbility fuel,
                out _))
        {
            return false;
        }

        string destinationId = BuildFacilityFuelDestinationId(facility);
        return GetCachedItemStacks().Any(stack => stack != null
            && stack.Quantity > 0
            && !stack.Forbidden
            && string.Equals(stack.ItemId, fuel.fuelItemId, StringComparison.Ordinal)
            && ((string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)
                    && stack.State is WorldItemStackState.FacilityBuffer
                        or WorldItemStackState.Carried)
                || ((stack.State is WorldItemStackState.Stored
                            or WorldItemStackState.Loose)
                        && stack.AvailableQuantity > 0)))
            || itemStackRuntime.GetCommittedHaulDeliveryQuantity(
                destinationId,
                fuel.fuelItemId) > 0;
    }

    public bool TryGetFacilityFuelSupplyPlan(
        BuildableObject facility,
        out string destinationId,
        out string itemId,
        out int requiredQuantity)
    {
        destinationId = string.Empty;
        itemId = string.Empty;
        requiredQuantity = 0;
        if (!TryGetFacilityFuelAbility(
                facility,
                out BuildingFuelConsumerAbility fuel,
                out _))
        {
            return false;
        }

        destinationId = BuildFacilityFuelDestinationId(facility);
        itemId = fuel.fuelItemId;
        requiredQuantity = Mathf.Max(1, fuel.fuelPerRefuel);
        return true;
    }

    public bool TryEnsureFacilityFuelSupply(
        IBuildingVisitorPort actor,
        BuildableObject facility,
        out bool completionOnly,
        out DomainFailure failure)
    {
        completionOnly = false;
        if (!TryGetFacilityFuelAbility(facility, out BuildingFuelConsumerAbility fuel, out failure))
        {
            return false;
        }
        TryRefreshFacilityFuelAuthorities();

        FacilityRuntimeState state = facility.FacilityState;
        if ((state.pendingFuel?.phase ?? 0) != (int)FacilityFuelCommitPhase.None)
        {
            if (!TryRecoverFacilityFuelCommit(facility, fuel, out _, out failure))
            {
                return false;
            }
            if (facility.HasFacilityFuelSupply)
            {
                completionOnly = true;
                return true;
            }
        }
        if (facility.HasFacilityFuelSupply)
        {
            completionOnly = true;
            failure = DomainFailure.None;
            return true;
        }

        string destinationId = BuildFacilityFuelDestinationId(facility);
        int quantity = Mathf.Max(1, fuel.fuelPerRefuel);
        IReadOnlyList<WorldItemStackSnapshot> stacks = GetCachedItemStacks();
        int readyQuantity = stacks
            .Where(stack => IsExactFacilityFuelStack(
                stack,
                fuel.fuelItemId,
                destinationId))
            .Where(stack => stack.State == WorldItemStackState.FacilityBuffer
                && stack.ReservedQuantity == 0
                && string.IsNullOrEmpty(stack.ReservedByPersistentId))
            .Sum(stack => stack.AvailableQuantity);
        if (readyQuantity >= quantity)
        {
            failure = DomainFailure.None;
            return true;
        }

        int awaitingPickupQuantity = stacks
            .Where(stack => IsExactFacilityFuelStack(
                stack,
                fuel.fuelItemId,
                destinationId))
            .Where(stack => stack.State is WorldItemStackState.Stored
                or WorldItemStackState.Loose)
            .Sum(stack => stack.TotalQuantity);
        int committedQuantity = itemStackRuntime.GetCommittedHaulDeliveryQuantity(
            destinationId,
            fuel.fuelItemId);
        int carriedQuantity = stacks
            .Where(stack => IsExactFacilityFuelStack(
                stack,
                fuel.fuelItemId,
                destinationId))
            .Where(stack => stack.State == WorldItemStackState.Carried)
            .Sum(stack => stack.TotalQuantity);
        // A committed haul intent is backed by its carried physical lot. Use the
        // larger observation instead of summing both views of the same delivery.
        int inTransitQuantity = Mathf.Max(committedQuantity, carriedQuantity);
        int ownedQuantity = checked(
            readyQuantity + awaitingPickupQuantity + inTransitQuantity);
        int missing = Mathf.Max(0, quantity - ownedQuantity);
        if (missing == 0)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalFuelStockMissing,
                "facility-fuel-delivery-in-transit:" + destinationId);
            return false;
        }

        if (!itemStackRuntime.TryRequestItemDelivery(
                fuel.fuelItemId,
                missing,
                facility.centerPos,
                destinationId,
                out int requested,
                out string requestFailure)
            || requested != missing)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalFuelStockMissing,
                string.IsNullOrWhiteSpace(requestFailure)
                    ? fuel.fuelItemId
                    : requestFailure.Trim());
            return false;
        }

        workforce?.RequestOneHaulerToReplan(
            clearFailures: true,
            forceInterrupt: true,
            protectedCharacterId: actor?.BuildingCharacterId ?? default);
        failure = new DomainFailure(
            FailureCode.SurvivalFuelStockMissing,
            "facility-fuel-delivery-requested:" + destinationId);
        return false;
    }

    public bool TryCommitFacilityFuel(
        BuildableObject facility,
        out int amount,
        out DomainFailure failure)
    {
        amount = 0;
        if (!TryGetFacilityFuelAbility(facility, out BuildingFuelConsumerAbility fuel, out failure))
        {
            return false;
        }
        TryRefreshFacilityFuelAuthorities();
        if ((facility.FacilityState.pendingFuel?.phase ?? 0)
            != (int)FacilityFuelCommitPhase.None)
        {
            return TryRecoverFacilityFuelCommit(facility, fuel, out amount, out failure);
        }
        if (facility.HasFacilityFuelSupply)
        {
            failure = DomainFailure.None;
            return true;
        }
        if (physicalSinks == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalFuelStockMissing,
                "facility-fuel-physical-sink-missing");
            return false;
        }

        int quantity = Mathf.Max(1, fuel.fuelPerRefuel);
        FacilityRuntimeState state = facility.FacilityState;
        int sequence = state.nextFuelOperationSequence;
        string destinationId = BuildFacilityFuelDestinationId(facility);
        string operationId = FormatFacilityFuelOperationId(facility, sequence);
        float before = state.remainingFuelGameSeconds;
        FacilityFuelCommitState pending = new()
        {
            phase = (int)FacilityFuelCommitPhase.IntentRecorded,
            operationSequence = sequence,
            operationId = operationId,
            destinationId = destinationId,
            itemId = fuel.fuelItemId,
            quantity = quantity,
            fuelSecondsBefore = before,
            fuelSecondsAfter = Mathf.Max(0.001f, fuel.fuelSecondsPerRefuel)
        };
        facility.ReplaceFacilityFuelState(before, sequence, pending);

        if (!physicalSinks.TryCommitSinkPending(
                destinationId,
                fuel.fuelItemId,
                quantity,
                operationId,
                FacilityFuelDispositionReason,
                out _,
                out string sinkFailure))
        {
            facility.ReplaceFacilityFuelState(
                before,
                sequence,
                new FacilityFuelCommitState());
            failure = new DomainFailure(
                FailureCode.SurvivalFuelStockMissing,
                string.IsNullOrWhiteSpace(sinkFailure)
                    ? destinationId
                    : sinkFailure.Trim());
            return false;
        }

        return TryRecoverFacilityFuelCommit(facility, fuel, out amount, out failure);
    }

    public void InvalidateFacilityFuelAuthorities() =>
        fuelAuthorityBuildingVersion = int.MinValue;

    public bool TryPrepareFacilityFuelRetirement(
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryGetFacilityFuelAbility(
                facility,
                out BuildingFuelConsumerAbility fuel,
                out DomainFailure profileFailure))
        {
            if (profileFailure.Code == FailureCode.SurvivalRefuelUnsupported)
            {
                return true;
            }
            failureReason = profileFailure.IsFailure
                ? profileFailure.Code.ToString()
                : "facility-fuel-retirement-profile-invalid";
            return false;
        }

        TryRefreshFacilityFuelAuthorities();
        if ((facility.FacilityState.pendingFuel?.phase ?? 0)
                != (int)FacilityFuelCommitPhase.None
            && !TryRecoverFacilityFuelCommit(
                facility,
                fuel,
                out _,
                out DomainFailure recoveryFailure))
        {
            failureReason = recoveryFailure.IsFailure
                ? recoveryFailure.Code.ToString()
                : "facility-fuel-retirement-recovery-deferred";
            return false;
        }
        if ((facility.FacilityState.pendingFuel?.phase ?? 0)
            != (int)FacilityFuelCommitPhase.None)
        {
            failureReason = "facility-fuel-retirement-pending-not-closed";
            return false;
        }
        if (fuelBufferRelease == null)
        {
            failureReason = "facility-fuel-retirement-release-unavailable";
            return false;
        }

        string destinationId = BuildFacilityFuelDestinationId(facility);
        if (!fuelBufferRelease.TryReleaseAtOwnerPosition(
                destinationId,
                facility.centerPos,
                "survival-facility-fuel-owner-retiring",
                out _,
                out string releaseFailure))
        {
            failureReason = string.IsNullOrWhiteSpace(releaseFailure)
                ? "facility-fuel-retirement-release-deferred:" + destinationId
                : releaseFailure.Trim();
            return false;
        }

        fuelAuthorityBuildingVersion = int.MinValue;
        return true;
    }

    public void TryRefreshFacilityFuelAuthorities()
    {
        if (fuelAuthorityBuildingVersion == worldRegistry.BuildingVersion)
        {
            return;
        }

        BuildableObject[] facilities = (worldRegistry.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where(SurvivalFacilityWorkRules.IsEnvironmentalFuelConsumer)
            .OrderBy(
                facility => facility.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .ToArray();
        if (fuelBufferLifecycle == null
            || fuelBufferRelease == null
            || treatmentBufferClaims == null
            || itemStackRuntime.MassQuery == null)
        {
            if (facilities.Length == 0)
            {
                fuelAuthorityBuildingVersion = worldRegistry.BuildingVersion;
                return;
            }
            throw new InvalidOperationException(
                "Facility fuel buffer lifecycle dependencies are unavailable.");
        }

        List<FacilityBufferDestinationClaim> claims = new(facilities.Length);
        List<FacilityBufferCapacityProfile> profiles = new(facilities.Length);
        foreach (BuildableObject facility in facilities)
        {
            if (!TryGetFacilityFuelAbility(facility, out BuildingFuelConsumerAbility fuel, out _))
            {
                continue;
            }
            string destinationId = BuildFacilityFuelDestinationId(facility);
            string facilityId = facility.RequirePersistentInstanceId().Value;
            long unitMassGrams = itemStackRuntime.MassQuery
                .GetDefinitionUnitMass((ItemDefinitionId)fuel.fuelItemId).Value;
            long maximumMassGrams = checked(
                unitMassGrams * Mathf.Max(1, fuel.fuelPerRefuel));
            claims.Add(new FacilityBufferDestinationClaim(
                destinationId,
                facility.centerPos,
                FacilityFuelOwnerDomain,
                destinationId,
                facilityId,
                FacilityBufferDestinationAnchorKind.LiveBuilding));
            profiles.Add(new FacilityBufferCapacityProfile(
                destinationId,
                facility.centerPos,
                FacilityFuelOwnerDomain,
                destinationId,
                facilityId,
                new PhysicalMassGrams(maximumMassGrams),
                FacilityFuelCapacitySchemaRevision));
        }

        if (aggregateRootStore?.IsRestoreStaging != true)
        {
            HashSet<string> desired = claims
                .Select(claim => claim.DestinationId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (FacilityBufferDestinationClaim retired in treatmentBufferClaims
                         .CaptureClaims()
                         .Where(claim => string.Equals(
                             claim.OwnerDomain,
                             FacilityFuelOwnerDomain,
                             StringComparison.Ordinal))
                         .Where(claim => !desired.Contains(claim.DestinationId))
                         .OrderBy(claim => claim.DestinationId, StringComparer.Ordinal))
            {
                if (!fuelBufferRelease.TryReleaseAtOwnerPosition(
                        retired.DestinationId,
                        retired.DropPosition,
                        "survival-facility-fuel-owner-retired",
                        out _,
                        out string releaseFailure))
                {
                    throw new InvalidOperationException(
                        $"Facility fuel buffer release failed for '{retired.DestinationId}': "
                        + releaseFailure);
                }
            }
        }

        if (!fuelBufferLifecycle.TryReplaceOwnedAuthorities(
                FacilityFuelOwnerDomain,
                claims,
                profiles,
                out string authorityFailure))
        {
            throw new InvalidOperationException(
                "Facility fuel buffer authority publication failed: "
                + authorityFailure);
        }
        fuelAuthorityBuildingVersion = worldRegistry.BuildingVersion;
    }

    public bool TryRecoverFacilityFuelCommits(out string failureReason)
    {
        failureReason = string.Empty;
        foreach (BuildableObject facility in (worldRegistry.Buildings
                     ?? Array.Empty<BuildableObject>())
                 .Where(SurvivalFacilityWorkRules.IsEnvironmentalFuelConsumer)
                 .OrderBy(
                     value => value.RequirePersistentInstanceId().Value,
                     StringComparer.Ordinal))
        {
            if ((facility.FacilityState.pendingFuel?.phase ?? 0)
                    == (int)FacilityFuelCommitPhase.None
                || !TryGetFacilityFuelAbility(
                    facility,
                    out BuildingFuelConsumerAbility fuel,
                    out _))
            {
                continue;
            }
            if (!TryRecoverFacilityFuelCommit(
                    facility,
                    fuel,
                    out _,
                    out DomainFailure failure))
            {
                failureReason = failure.IsFailure
                    ? failure.Code.ToString()
                    : "facility-fuel-recovery-incomplete";
                return false;
            }
        }
        return true;
    }

    public int CountStoredStock(StockCategory category)
    {
        int total = 0;
        foreach (IWarehouseFacility warehouse in GetWarehouses())
        {
            if (warehouse is not BuildableObject building)
            {
                continue;
            }

            total = SaturatingAdd(
                total,
                stockQuery.GetWarehouseQuantity(
                    building.RequirePersistentInstanceId(),
                    category));
        }

        return total;
    }

    public int CountLooseStock(StockCategory category)
    {
        int total = 0;
        IReadOnlyList<WorldItemStackSnapshot> stacks = GetCachedItemStacks();
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSnapshot stack = stacks[index];
            if (IsUsableCategoryStack(stack, category)
                && stack.State != WorldItemStackState.Stored
                && stack.State != WorldItemStackState.Carried)
            {
                total = SaturatingAdd(total, stack.Quantity);
            }
        }

        return total;
    }

    public int WithdrawStock(StockCategory category, int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        HashSet<string> warehouseDestinations = GetWarehouses()
            .Select(WarehouseStorageIdentity.RequireDestinationId)
            .ToHashSet(StringComparer.Ordinal);
        int remaining = amount;
        foreach (WorldItemStackSnapshot stack in GetCachedItemStacks()
                     .Where(stack => IsUsableCategoryStack(stack, category)
                         && stack.State == WorldItemStackState.Stored
                         && warehouseDestinations.Contains(
                             string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId)
                                 ? stack.DestinationId
                                 : stack.SourceStorageDestinationId))
                     .OrderBy(stack => stack.ItemId, StringComparer.Ordinal)
                     .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (remaining <= 0)
            {
                break;
            }

            int requested = Math.Min(remaining, stack.Quantity);
            if (itemStackRuntime.TryCommitPhysicalDisposition(
                    stack.StackId,
                    requested,
                    PhysicalItemDispositionKind.Sink,
                    $"survival-stock-withdraw:{category}:{stack.StackId}:{requested}",
                    "survival-treatment-material-consumed",
                    out PhysicalItemDispositionReceipt disposition,
                    out _))
            {
                remaining -= Math.Min(requested, disposition.Quantity);
            }
        }

        return amount - remaining;
    }

    public bool HasPotentialTreatmentMaterial(bool detoxOnly)
    {
        ItemDefinitionSO[] definitions = GetTreatmentDefinitions(detoxOnly);
        if (definitions.Length == 0)
        {
            return false;
        }
        HashSet<string> eligible = definitions
            .Select(value => value.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        return GetCachedItemStacks().Any(stack => stack != null
            && stack.Quantity > 0
            && !stack.Forbidden
            && eligible.Contains(stack.ItemId)
            && (((stack.State == WorldItemStackState.Stored
                        || stack.State == WorldItemStackState.Loose)
                    && stack.AvailableQuantity > 0)
                || CharacterConsumablesInputDestinationIdentity
                    .IsDestinationForKind(
                        stack.DestinationId,
                        CharacterConsumablesInputKind.MedicalTreatment)));
    }

    public bool TryInspectTreatmentMaterial(
        BuildableObject facility,
        bool detoxOnly,
        out SurvivalTreatmentMaterialSelection selection)
    {
        selection = default;
        if (facility == null || facility.isDestroy)
        {
            return false;
        }

        BuildingInstanceId facilityId = facility.RequirePersistentInstanceId();
        ItemDefinitionSO[] definitions = GetTreatmentDefinitions(detoxOnly);
        if (definitions.Length == 0)
        {
            return false;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks =
            itemStackRuntime.GetAllStacks() ?? Array.Empty<WorldItemStackSnapshot>();
        foreach (ItemDefinitionSO definition in definitions)
        {
            string destinationId = BuildMedicalDestination(
                facilityId,
                definition.ItemId);
            if (TryInspectExistingTreatmentRoute(
                    definition,
                    destinationId,
                    stacks,
                    out selection))
            {
                return true;
            }
        }

        SurvivalTreatmentMaterialSelection capacityUnavailable = default;
        bool foundCapacityUnavailable = false;
        bool foundUnknownAuthority = false;
        foreach (ItemDefinitionSO definition in definitions)
        {
            string destinationId = BuildMedicalDestination(
                facilityId,
                definition.ItemId);
            WorldItemStackSnapshot source = FindTreatmentDeliverySource(
                definition.ItemId,
                facility.centerPos,
                stacks);
            if (source == null)
            {
                continue;
            }

            SurvivalTreatmentCapacityProbe capacity = ProbeTreatmentCapacity(
                facility,
                destinationId,
                source);
            if (capacity == SurvivalTreatmentCapacityProbe.Available)
            {
                selection = CreateSelection(
                    definition,
                    source,
                    destinationId,
                    SurvivalTreatmentSupplyStatus.AwaitingRequest);
                return true;
            }
            if (capacity == SurvivalTreatmentCapacityProbe.AuthorityUnavailable)
            {
                foundUnknownAuthority = true;
                continue;
            }
            if (!foundCapacityUnavailable)
            {
                capacityUnavailable = CreateSelection(
                    definition,
                    source,
                    destinationId,
                    SurvivalTreatmentSupplyStatus.CapacityUnavailable);
                foundCapacityUnavailable = true;
            }
        }

        if (foundUnknownAuthority)
        {
            return false;
        }
        if (foundCapacityUnavailable)
        {
            selection = capacityUnavailable;
            return true;
        }

        ItemDefinitionSO missing = definitions.FirstOrDefault(definition =>
            HasExactTreatmentBufferAuthority(
                facility,
                BuildMedicalDestination(facilityId, definition.ItemId)));
        if (missing == null)
        {
            return false;
        }
        selection = CreateSelection(
            missing,
            null,
            BuildMedicalDestination(facilityId, missing.ItemId),
            SurvivalTreatmentSupplyStatus.Missing);
        return true;
    }

    public bool TryInspectPinnedTreatmentMaterial(
        BuildableObject facility,
        string itemId,
        string destinationId,
        out SurvivalTreatmentMaterialSelection selection)
    {
        selection = default;
        if (facility == null
            || facility.isDestroy
            || string.IsNullOrWhiteSpace(itemId)
            || string.IsNullOrWhiteSpace(destinationId)
            || !itemCatalog.TryGet(
                (ItemDefinitionId)itemId,
                out ItemDefinitionSO definition))
        {
            return false;
        }
        BuildingInstanceId facilityId = facility.RequirePersistentInstanceId();
        if (!string.Equals(
                destinationId,
                BuildMedicalDestination(facilityId, itemId),
                StringComparison.Ordinal))
        {
            return false;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks =
            itemStackRuntime.GetAllStacks() ?? Array.Empty<WorldItemStackSnapshot>();
        if (TryInspectExistingTreatmentRoute(
                definition,
                destinationId,
                stacks,
                out selection))
        {
            return true;
        }

        WorldItemStackSnapshot source = FindTreatmentDeliverySource(
            definition.ItemId,
            facility.centerPos,
            stacks);
        if (source == null)
        {
            if (!HasExactTreatmentBufferAuthority(facility, destinationId))
            {
                return false;
            }
            selection = CreateSelection(
                definition,
                null,
                destinationId,
                SurvivalTreatmentSupplyStatus.Missing);
            return true;
        }
        SurvivalTreatmentCapacityProbe capacity = ProbeTreatmentCapacity(
            facility,
            destinationId,
            source);
        if (capacity == SurvivalTreatmentCapacityProbe.AuthorityUnavailable)
        {
            return false;
        }
        selection = CreateSelection(
            definition,
            source,
            destinationId,
            capacity == SurvivalTreatmentCapacityProbe.Available
                ? SurvivalTreatmentSupplyStatus.AwaitingRequest
                : SurvivalTreatmentSupplyStatus.CapacityUnavailable);
        return true;
    }

    public bool TryEnsureTreatmentMaterial(
        BuildableObject facility,
        bool detoxOnly,
        CharacterId protectedCharacterId,
        out SurvivalTreatmentMaterialSelection selection,
        out string failureReason)
    {
        selection = default;
        failureReason = string.Empty;
        if (facility == null || facility.isDestroy)
        {
            failureReason = "treatment-supply-facility-missing";
            return false;
        }
        BuildingInstanceId facilityId = facility.RequirePersistentInstanceId();
        ItemDefinitionSO[] definitions = GetTreatmentDefinitions(detoxOnly);
        if (definitions.Length == 0)
        {
            failureReason = detoxOnly
                ? "detox-medicine-undefined"
                : "treatment-material-undefined";
            return false;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks = GetCachedItemStacks();
        foreach (ItemDefinitionSO definition in definitions)
        {
            string destinationId = BuildMedicalDestination(
                facilityId,
                definition.ItemId);
            WorldItemStackSnapshot ready = stacks
                .Where(stack => IsExactMedicalDestinationStack(
                    stack,
                    definition.ItemId,
                    destinationId))
                .Where(stack => stack.State == WorldItemStackState.FacilityBuffer
                    && stack.ReservedQuantity == 0
                    && string.IsNullOrEmpty(stack.ReservedByPersistentId)
                    && stack.AvailableQuantity > 0)
                .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (ready != null)
            {
                selection = CreateSelection(
                    definition,
                    ready,
                    destinationId,
                    SurvivalTreatmentSupplyStatus.Ready);
                return true;
            }
            bool inTransit = stacks.Any(stack => IsExactMedicalDestinationStack(
                    stack,
                    definition.ItemId,
                    destinationId))
                || itemStackRuntime.GetCommittedHaulDeliveryQuantity(
                    destinationId,
                    definition.ItemId) > 0;
            if (inTransit)
            {
                selection = CreateSelection(
                    definition,
                    null,
                    destinationId,
                    SurvivalTreatmentSupplyStatus.DeliveryInTransit);
                failureReason = "treatment-supply-delivery-in-transit:"
                    + destinationId;
                return false;
            }
        }

        string lastRequestFailure = string.Empty;
        foreach (ItemDefinitionSO definition in definitions)
        {
            string destinationId = BuildMedicalDestination(
                facilityId,
                definition.ItemId);
            if (!itemStackRuntime.TryRequestItemDelivery(
                    definition.ItemId,
                    1,
                    facility.centerPos,
                    destinationId,
                    out int requested,
                    out string requestFailure)
                || requested != 1)
            {
                if (!string.IsNullOrWhiteSpace(requestFailure))
                {
                    lastRequestFailure = requestFailure.Trim();
                }
                continue;
            }
            selection = CreateSelection(
                definition,
                null,
                destinationId,
                SurvivalTreatmentSupplyStatus.DeliveryRequested);
            workforce?.RequestOneHaulerToReplan(
                clearFailures: true,
                forceInterrupt: true,
                protectedCharacterId: protectedCharacterId);
            failureReason = "treatment-supply-delivery-requested:"
                + destinationId;
            return false;
        }

        failureReason = "treatment-supply-stock-missing"
            + (lastRequestFailure.Length > 0
                ? ":" + lastRequestFailure
                : string.Empty);
        return false;
    }

    public bool TryCommitTreatmentMaterialPending(
        string destinationId,
        string itemId,
        string operationId,
        string reasonCode,
        out PhysicalItemBatchDispositionReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        if (physicalSinks == null)
        {
            failureReason = "treatment-physical-sink-service-missing";
            return false;
        }
        return physicalSinks.TryCommitSinkPending(
            destinationId,
            itemId,
            1,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);
    }

    public bool TryGetPendingTreatmentMaterial(
        string operationId,
        out PhysicalItemBatchDispositionReceipt receipt)
    {
        receipt = default;
        return physicalSinks != null
            && physicalSinks.TryGetPending(operationId, out receipt);
    }

    public bool TryPublishTreatmentTareAndAcknowledge(
        string itemId,
        Vector2Int outputPosition,
        string commitId,
        out string failureReason)
    {
        if (physicalSinks == null)
        {
            failureReason = "treatment-physical-sink-service-missing";
            return false;
        }
        if (packagedTare == null)
        {
            failureReason = "treatment-packaged-tare-service-missing";
            return false;
        }
        if (!packagedTare.EnsureTerminalSinkOutputs(
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [itemId] = 1
                },
                outputPosition,
                commitId,
                out _,
                out failureReason))
        {
            return false;
        }
        return physicalSinks.Acknowledge(commitId, out failureReason);
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetCachedItemStacks()
    {
        if (cachedItemStackVersion == itemStackRuntime.ItemStackVersion)
        {
            return cachedItemStacks;
        }

        cachedItemStackVersion = itemStackRuntime.ItemStackVersion;
        cachedItemStacks = itemStackRuntime.GetAllStacks();
        return cachedItemStacks;
    }

    private IEnumerable<IWarehouseFacility> GetWarehouses()
    {
        if (!gridSystemProvider.TryGetGrid(out Grid grid))
        {
            return Array.Empty<IWarehouseFacility>();
        }

        IReadOnlyList<IWarehouseFacility> registered = worldRegistry.Warehouses;
        return registered.Count > 0
            ? registered.Where(warehouse => IsWarehouseOnGrid(warehouse, grid)).ToArray()
            : grid.FindAllOccupants(null)
                .OfType<IWarehouseFacility>()
                .Where(warehouse => IsWarehouseOnGrid(warehouse, grid))
                .ToArray();
    }

    private bool IsUsableCategoryStack(
        WorldItemStackSnapshot stack,
        StockCategory category)
    {
        return stack != null
            && stack.Quantity > 0
            && !stack.Forbidden
            && stack.Contamination <= 0.01f
            && itemCatalog.TryGet((ItemDefinitionId)stack.ItemId, out ItemDefinitionSO definition)
            && definition.StockCategory == category;
    }

    private ItemDefinitionSO[] GetTreatmentDefinitions(bool detoxOnly) =>
        itemCatalog.All
            .Where(definition => definition != null
                && (detoxOnly
                    ? definition.TryGetFeature(out MedicineItemFeature medicine)
                        && medicine.detoxReduction > 0f
                        && !float.IsNaN(medicine.detoxReduction)
                        && !float.IsInfinity(medicine.detoxReduction)
                    : definition.StockCategory is StockCategory.Medicine
                        or StockCategory.Biological))
            .OrderBy(definition => detoxOnly
                || definition.StockCategory == StockCategory.Medicine
                    ? 0
                    : 1)
            .ThenBy(definition => definition.ItemId, StringComparer.Ordinal)
            .ToArray();

    private bool TryInspectExistingTreatmentRoute(
        ItemDefinitionSO definition,
        string destinationId,
        IReadOnlyList<WorldItemStackSnapshot> stacks,
        out SurvivalTreatmentMaterialSelection selection)
    {
        selection = default;
        WorldItemStackSnapshot[] routed = (stacks
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(stack => IsExactMedicalDestinationStack(
                stack,
                definition.ItemId,
                destinationId))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        WorldItemStackSnapshot ready = routed.FirstOrDefault(stack =>
            stack.State == WorldItemStackState.FacilityBuffer
            && stack.ReservedQuantity == 0
            && string.IsNullOrEmpty(stack.ReservedByPersistentId)
            && stack.AvailableQuantity > 0);
        if (ready != null)
        {
            selection = CreateSelection(
                definition,
                ready,
                destinationId,
                SurvivalTreatmentSupplyStatus.Ready);
            return true;
        }
        if (HasExactTreatmentDeliveryPathFailure(
                destinationId,
                definition.ItemId))
        {
            selection = CreateSelection(
                definition,
                null,
                destinationId,
                SurvivalTreatmentSupplyStatus.DeliveryNoPath);
            return true;
        }
        if (itemStackRuntime.GetCommittedHaulDeliveryQuantity(
                destinationId,
                definition.ItemId) > 0
            || routed.Any(stack => stack.State == WorldItemStackState.Carried))
        {
            selection = CreateSelection(
                definition,
                null,
                destinationId,
                SurvivalTreatmentSupplyStatus.DeliveryInTransit);
            return true;
        }
        if (routed.Length == 0)
        {
            return false;
        }

        selection = CreateSelection(
            definition,
            routed[0],
            destinationId,
            SurvivalTreatmentSupplyStatus.DeliveryRequested);
        return true;
    }

    private bool HasExactTreatmentDeliveryPathFailure(
        string destinationId,
        string itemId)
    {
        foreach (CharacterActor actor in (worldRegistry.AllCharacters
                     ?? Array.Empty<CharacterActor>())
                 .Where(candidate => candidate != null)
                 .OrderBy(
                     candidate => candidate.Identity?.PersistentId,
                     StringComparer.Ordinal))
        {
            IHaulDeliveryPathFailureQuery haul =
                actor.GetComponent<AbilityHaul>();
            if (haul != null
                && haul.TryGetLastDeliveryPathFailure(
                    out HaulDeliveryPathFailureSnapshot failure)
                && failure.FailureKind == AIActionFailureKind.NoPath
                && string.Equals(
                    failure.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    failure.ItemId,
                    itemId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private WorldItemStackSnapshot FindTreatmentDeliverySource(
        string itemId,
        Vector2Int destinationPosition,
        IReadOnlyList<WorldItemStackSnapshot> stacks)
    {
        WorldItemStackSnapshot loose = (stacks
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(stack => IsUnassignedTreatmentSource(
                stack,
                itemId,
                WorldItemStackState.Loose,
                string.Empty))
            .OrderBy(stack => Manhattan(stack.Position, destinationPosition))
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (loose != null)
        {
            return loose;
        }

        foreach (IWarehouseFacility warehouse in GetWarehouses()
                     .OrderBy(candidate => candidate is BuildableObject building
                         ? Manhattan(building.centerPos, destinationPosition)
                         : int.MaxValue)
                     .ThenBy(
                         WarehouseStorageIdentity.RequireDestinationId,
                         StringComparer.Ordinal))
        {
            string warehouseDestinationId =
                WarehouseStorageIdentity.RequireDestinationId(warehouse);
            WorldItemStackSnapshot stored = (stacks
                    ?? Array.Empty<WorldItemStackSnapshot>())
                .Where(stack => IsUnassignedTreatmentSource(
                    stack,
                    itemId,
                    WorldItemStackState.Stored,
                    warehouseDestinationId))
                .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (stored != null)
            {
                return stored;
            }
        }
        return null;
    }

    private static bool IsUnassignedTreatmentSource(
        WorldItemStackSnapshot stack,
        string itemId,
        WorldItemStackState state,
        string expectedDestinationId) => stack != null
        && stack.Quantity > 0
        && stack.AvailableQuantity > 0
        && !stack.Forbidden
        && stack.State == state
        && !FacilityOutputExactRouteCustodyCodec.HasAnyCustody(stack.Components)
        && string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId)
        && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
        && string.Equals(
            stack.DestinationId ?? string.Empty,
            expectedDestinationId ?? string.Empty,
            StringComparison.Ordinal);

    private SurvivalTreatmentCapacityProbe ProbeTreatmentCapacity(
        BuildableObject facility,
        string destinationId,
        WorldItemStackSnapshot source)
    {
        if (facility == null
            || source == null
            || treatmentBufferOccupancy == null
            || itemStackRuntime.MassQuery == null)
        {
            return SurvivalTreatmentCapacityProbe.AuthorityUnavailable;
        }

        if (!TryGetExactTreatmentBufferCapacity(
                facility,
                destinationId,
                out FacilityBufferMassCapacitySnapshot capacity))
        {
            return SurvivalTreatmentCapacityProbe.AuthorityUnavailable;
        }

        try
        {
            IPhysicalItemMassQuery massQuery = itemStackRuntime.MassQuery;
            ItemDefinitionId itemId = (ItemDefinitionId)source.ItemId;
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                itemId,
                source.ItemInstanceId,
                source.Components);
            long lotMassGrams = massQuery.GetQuantityMass(
                itemId,
                subject,
                1).Value;
            FacilityBufferPhysicalOccupancySnapshot occupancy =
                treatmentBufferOccupancy.Capture(destinationId);
            long occupiedMassGrams = checked(
                occupancy.TotalMassGrams + capacity.ReservedMassGrams);
            long maximumMassGrams = capacity.Profile.MaxMassGrams;
            return lotMassGrams > 0L
                && occupiedMassGrams >= 0L
                && occupiedMassGrams <= maximumMassGrams
                && lotMassGrams <= maximumMassGrams - occupiedMassGrams
                    ? SurvivalTreatmentCapacityProbe.Available
                    : SurvivalTreatmentCapacityProbe.Unavailable;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or OverflowException)
        {
            return SurvivalTreatmentCapacityProbe.AuthorityUnavailable;
        }
    }

    private bool HasExactTreatmentBufferAuthority(
        BuildableObject facility,
        string destinationId) => TryGetExactTreatmentBufferCapacity(
        facility,
        destinationId,
        out _);

    private bool TryGetExactTreatmentBufferCapacity(
        BuildableObject facility,
        string destinationId,
        out FacilityBufferMassCapacitySnapshot capacity)
    {
        capacity = default;
        if (facility == null
            || treatmentBufferCapacities == null
            || treatmentBufferClaims == null)
        {
            return false;
        }
        Vector2Int dropPosition = facility.centerPos;
        string facilityId = facility.RequirePersistentInstanceId().Value;
        return treatmentBufferClaims.TryGetClaim(
                destinationId,
                dropPosition,
                out FacilityBufferDestinationClaim claim)
            && claim != null
            && treatmentBufferCapacities.TryGetCapacity(
                destinationId,
                dropPosition,
                out capacity)
            && capacity.Profile != null
            && string.Equals(
                claim.OwnerFacilityId,
                facilityId,
                StringComparison.Ordinal)
            && string.Equals(
                capacity.Profile.OwnerFacilityId,
                facilityId,
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerDomain,
                capacity.Profile.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerOperationId,
                capacity.Profile.OwnerOperationId,
                StringComparison.Ordinal);
    }

    private bool TryRecoverFacilityFuelCommit(
        BuildableObject facility,
        BuildingFuelConsumerAbility fuel,
        out int amount,
        out DomainFailure failure)
    {
        amount = 0;
        FacilityRuntimeState state = facility.FacilityState;
        FacilityFuelCommitState pending = state.pendingFuel
            ?? new FacilityFuelCommitState();
        FacilityFuelCommitPhase phase = (FacilityFuelCommitPhase)pending.phase;
        if (phase == FacilityFuelCommitPhase.None)
        {
            failure = DomainFailure.None;
            return true;
        }

        string destinationId = BuildFacilityFuelDestinationId(facility);
        float expectedAfter = Mathf.Max(0.001f, fuel.fuelSecondsPerRefuel);
        bool contractMatches = pending.operationSequence
                == state.nextFuelOperationSequence
            && pending.quantity == Mathf.Max(1, fuel.fuelPerRefuel)
            && string.Equals(
                pending.operationId,
                FormatFacilityFuelOperationId(facility, pending.operationSequence),
                StringComparison.Ordinal)
            && string.Equals(
                pending.destinationId,
                destinationId,
                StringComparison.Ordinal)
            && string.Equals(pending.itemId, fuel.fuelItemId, StringComparison.Ordinal)
            && Mathf.Approximately(pending.fuelSecondsBefore, 0f)
            && Mathf.Approximately(pending.fuelSecondsAfter, expectedAfter);
        if (!contractMatches)
        {
            throw new InvalidOperationException(
                $"Facility fuel commit '{pending.operationId}' conflicts with "
                + $"facility '{facility.PersistentInstanceId.Value}'.");
        }

        PhysicalItemBatchDispositionReceipt receipt = default;
        bool hasReceipt = physicalSinks != null
            && physicalSinks.TryGetPending(
                pending.operationId,
                out receipt);
        if (hasReceipt
            && (!receipt.IsCommitted
                || receipt.Kind != PhysicalItemDispositionKind.Sink
                || receipt.Quantity != pending.quantity
                || !string.Equals(
                    receipt.OperationId,
                    pending.operationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.ReasonCode,
                    FacilityFuelDispositionReason,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Facility fuel commit '{pending.operationId}' has a mismatched physical receipt.");
        }

        if (phase == FacilityFuelCommitPhase.IntentRecorded)
        {
            if (!hasReceipt)
            {
                facility.ReplaceFacilityFuelState(
                    pending.fuelSecondsBefore,
                    state.nextFuelOperationSequence,
                    new FacilityFuelCommitState());
                failure = DomainFailure.None;
                return true;
            }

            pending.phase = (int)FacilityFuelCommitPhase.OutcomePublished;
            pending.physicalCommitId = receipt.CommitId;
            pending.physicalSourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            pending.physicalInputMassGrams = receipt.InputMassGrams;
            facility.ReplaceFacilityFuelState(
                pending.fuelSecondsAfter,
                state.nextFuelOperationSequence,
                pending);
            amount = pending.quantity;
        }
        else if (!hasReceipt
            || !string.Equals(
                pending.physicalCommitId,
                receipt.CommitId,
                StringComparison.Ordinal)
            || pending.physicalInputMassGrams != receipt.InputMassGrams
            || !(pending.physicalSourceStackIds ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(
                    receipt.SourceStackIds.OrderBy(
                        value => value,
                        StringComparer.Ordinal),
                    StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Facility fuel commit '{pending.operationId}' lost its published physical receipt.");
        }

        amount = pending.quantity;
        if (!physicalSinks.Acknowledge(receipt.CommitId, out string acknowledgeFailure))
        {
            failure = new DomainFailure(
                FailureCode.SurvivalFuelStockMissing,
                string.IsNullOrWhiteSpace(acknowledgeFailure)
                    ? receipt.CommitId
                    : acknowledgeFailure.Trim());
            return false;
        }

        facility.ReplaceFacilityFuelState(
            facility.FacilityState.remainingFuelGameSeconds,
            checked(state.nextFuelOperationSequence + 1),
            new FacilityFuelCommitState());
        failure = DomainFailure.None;
        return true;
    }

    private bool TryGetFacilityFuelAbility(
        BuildableObject facility,
        out BuildingFuelConsumerAbility fuel,
        out DomainFailure failure)
    {
        fuel = facility?.BuildingData?
            .GetAbility<BuildingFuelConsumerAbility>();
        if (!SurvivalFacilityWorkRules.IsEnvironmentalFuelConsumer(facility)
            || fuel == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalRefuelUnsupported,
                facility?.PersistentInstanceId.Value ?? string.Empty);
            return false;
        }
        string itemId = fuel.fuelItemId ?? string.Empty;
        if (itemId.Length == 0
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || fuel.fuelPerRefuel <= 0
            || float.IsNaN(fuel.fuelSecondsPerRefuel)
            || float.IsInfinity(fuel.fuelSecondsPerRefuel)
            || fuel.fuelSecondsPerRefuel <= 0f
            || !itemCatalog.TryGet((ItemDefinitionId)itemId, out _))
        {
            throw new InvalidOperationException(
                $"Facility '{facility.RequirePersistentInstanceId().Value}' has an invalid explicit fuel profile.");
        }

        failure = DomainFailure.None;
        return true;
    }

    private static string BuildFacilityFuelDestinationId(
        BuildableObject facility) => FacilityFuelDestinationPrefix
        + Uri.EscapeDataString(facility.RequirePersistentInstanceId().Value);

    internal static string FormatFacilityFuelOperationId(
        BuildableObject facility,
        int sequence) => FacilityFuelOwnerDomain + ":"
        + Uri.EscapeDataString(facility.RequirePersistentInstanceId().Value)
        + $":{sequence:D8}";

    private static bool IsExactFacilityFuelStack(
        WorldItemStackSnapshot stack,
        string itemId,
        string destinationId) => stack != null
        && stack.Quantity > 0
        && !stack.Forbidden
        && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
        && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal);

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Math.Abs(left.x - right.x) + Math.Abs(left.y - right.y);

    private static string BuildMedicalDestination(
        BuildingInstanceId facilityId,
        string itemId) => CharacterConsumablesInputDestinationIdentity.Build(
        CharacterConsumablesInputKind.MedicalTreatment,
        facilityId,
        new ConsumableItemDefinitionId(itemId));

    private static bool IsExactMedicalDestinationStack(
        WorldItemStackSnapshot stack,
        string itemId,
        string destinationId) => stack != null
        && stack.Quantity > 0
        && !stack.Forbidden
        && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
        && string.Equals(
            stack.DestinationId,
            destinationId,
            StringComparison.Ordinal);

    private static SurvivalTreatmentMaterialSelection CreateSelection(
        ItemDefinitionSO definition,
        WorldItemStackSnapshot stack,
        string destinationId,
        SurvivalTreatmentSupplyStatus status) => new(
        definition.ItemId,
        stack == null ? default : new ItemStackId(stack.StackId),
        destinationId,
        definition.StockCategory == StockCategory.Biological,
        status);

    private static bool IsWarehouseOnGrid(IWarehouseFacility warehouse, Grid grid)
    {
        if (warehouse == null)
        {
            return false;
        }

        BuildableObject building = warehouse as BuildableObject;
        return building == null || building.Grid == grid;
    }

    private static int SaturatingAdd(int current, int value)
    {
        long total = (long)Math.Max(0, current) + Math.Max(0, value);
        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }
}

public sealed class SurvivalFoodRuntimeDependencies
{
    public SurvivalFoodRuntimeDependencies(
        IGridSystemProvider gridSystemProvider,
        IWorldItemStackRuntime itemStackRuntime,
        IItemDefinitionCatalog itemCatalog,
        IStockQuery stockQuery,
        IClimateQuery climate,
        IPhysicalFacilityItemSinkGateway physicalSinks = null,
        IPackagedLotTareDispositionService packagedTare = null,
        IWorkforceReplanService workforce = null,
        IFacilityBufferMassCapacityQuery treatmentBufferCapacities = null,
        IFacilityBufferPhysicalOccupancyQuery treatmentBufferOccupancy = null,
        IFacilityBufferDestinationClaimQuery treatmentBufferClaims = null,
        IFacilityBufferDestinationLifecycleCommand fuelBufferLifecycle = null,
        IFacilityBufferDestinationReleaseService fuelBufferRelease = null)
    {
        GridSystemProvider = gridSystemProvider
            ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        ItemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        ItemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        StockQuery = stockQuery
            ?? throw new ArgumentNullException(nameof(stockQuery));
        Climate = climate ?? throw new ArgumentNullException(nameof(climate));
        PhysicalSinks = physicalSinks;
        PackagedTare = packagedTare;
        Workforce = workforce;
        TreatmentBufferCapacities = treatmentBufferCapacities;
        TreatmentBufferOccupancy = treatmentBufferOccupancy;
        TreatmentBufferClaims = treatmentBufferClaims;
        FuelBufferLifecycle = fuelBufferLifecycle;
        FuelBufferRelease = fuelBufferRelease;
    }

    public IGridSystemProvider GridSystemProvider { get; }

    public IWorldItemStackRuntime ItemStackRuntime { get; }

    public IItemDefinitionCatalog ItemCatalog { get; }

    public IStockQuery StockQuery { get; }

    public IClimateQuery Climate { get; }

    public IPhysicalFacilityItemSinkGateway PhysicalSinks { get; }

    public IPackagedLotTareDispositionService PackagedTare { get; }

    public IWorkforceReplanService Workforce { get; }

    public IFacilityBufferMassCapacityQuery TreatmentBufferCapacities { get; }

    public IFacilityBufferPhysicalOccupancyQuery TreatmentBufferOccupancy { get; }

    public IFacilityBufferDestinationClaimQuery TreatmentBufferClaims { get; }

    public IFacilityBufferDestinationLifecycleCommand FuelBufferLifecycle { get; }

    public IFacilityBufferDestinationReleaseService FuelBufferRelease { get; }
}

internal sealed class SurvivalFoodOverviewCache
{
    private const float RefreshIntervalSeconds = 0.5f;

    private readonly IGameClock gameClock;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private SurvivalFoodOverview cachedOverview;
    private int cachedItemVersion = -1;
    private int cachedCharacterVersion = -1;
    private int cachedBuildingVersion = -1;
    private float cachedTime = float.NegativeInfinity;
    private bool hasCachedOverview;

    public SurvivalFoodOverviewCache(
        IGameClock gameClock,
        ICharacterAiWorldRegistry worldRegistry,
        IWorldItemStackRuntime itemStackRuntime)
    {
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
    }

    public SurvivalFoodOverview GetOrCreate(Func<SurvivalFoodOverview> factory)
    {
        _ = factory ?? throw new ArgumentNullException(nameof(factory));
        int itemVersion = itemStackRuntime.ItemStackVersion;
        int characterVersion = worldRegistry.CharacterVersion;
        int buildingVersion = worldRegistry.BuildingVersion;
        float now = gameClock.Time;
        bool refreshIntervalValid =
            now - cachedTime <= RefreshIntervalSeconds;
        if (hasCachedOverview
            && refreshIntervalValid
            && cachedItemVersion == itemVersion
            && cachedCharacterVersion == characterVersion
            && cachedBuildingVersion == buildingVersion)
        {
            return cachedOverview;
        }

        SurvivalFoodOverview overview = factory();
        cachedOverview = overview;
        cachedItemVersion = itemStackRuntime.ItemStackVersion;
        cachedCharacterVersion = worldRegistry.CharacterVersion;
        cachedBuildingVersion = worldRegistry.BuildingVersion;
        cachedTime = now;
        hasCachedOverview = true;

        return overview;
    }

    public void Invalidate()
    {
        hasCachedOverview = false;
        cachedTime = float.NegativeInfinity;
    }
}
