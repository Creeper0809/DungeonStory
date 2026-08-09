using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionAggregateStateStore
{
    private readonly ProductionAggregateStateSession session;

    public ProductionAggregateStateStore(
        DungeonRuntimeAggregateRootStore rootStore)
    {
        session = new ProductionAggregateStateSession(rootStore);
    }

    public IReadOnlyList<ProductionBillRecord> Bills => session.Bills;
    internal int NextBillSequence
    {
        get => session.NextBillSequence;
        set => session.NextBillSequence = value;
    }
    internal int BillVersion => session.BillVersion;
    internal int StockSensorVersion => session.StockSensorVersion;
    internal IReadOnlyCollection<string> InstalledStockSensorFacilityIds =>
        session.InstalledStockSensorFacilityIds;
    internal IReadOnlyCollection<string> AcknowledgedStockSensorFacilityIds =>
        session.AcknowledgedStockSensorFacilityIds;
    internal void AddBill(ProductionBillRecord bill) => session.AddBill(bill);
    internal bool RemoveBill(ProductionBillRecord bill) => session.RemoveBill(bill);
    internal void MoveBill(
        ProductionBillRecord bill,
        ProductionBillRecord anchor,
        bool insertAfter) => session.MoveBill(bill, anchor, insertAfter);
    internal void IncrementBillVersion() => session.IncrementBillVersion();
    internal void IncrementStockSensorVersion() => session.IncrementStockSensorVersion();
    internal bool HasInstalledSensor(string id) => session.HasInstalledSensor(id);
    internal bool HasAcknowledgedSensor(string id) => session.HasAcknowledgedSensor(id);
    internal bool AddInstalledSensor(string id) => session.AddInstalledSensor(id);
    internal bool RemoveInstalledSensor(string id) => session.RemoveInstalledSensor(id);
    internal bool AddAcknowledgedSensor(string id) => session.AddAcknowledgedSensor(id);
    internal bool RemoveAcknowledgedSensor(string id) => session.RemoveAcknowledgedSensor(id);
    internal void Replace(ProductionBillRestoreCandidate candidate) =>
        session.Restore(candidate);
}

internal static class ProductionBillStateCodec
{
    private const int MaximumPrefetchBatches = 3;

    internal static DungeonProductionBillSaveData Capture(
        int nextBillSequence,
        IEnumerable<ProductionBillRecord> bills,
        IEnumerable<string> installedStockSensorFacilityIds,
        IEnumerable<string> acknowledgedStockSensorFacilityIds)
    {
        return new DungeonProductionBillSaveData
        {
            nextBillSequence = nextBillSequence,
            bills = (bills ?? Array.Empty<ProductionBillRecord>())
                .Select(ToSaveData)
                .ToList(),
            installedStockSensorFacilityIds = CanonicalIds(
                installedStockSensorFacilityIds),
            acknowledgedStockSensorFacilityIds = CanonicalIds(
                acknowledgedStockSensorFacilityIds)
        };
    }

    internal static ProductionBillRestoreCandidate CreateRestoreCandidate(
        DungeonProductionBillSaveData snapshot,
        IResourceEconomyContentCatalog catalog,
        int nextBillVersion,
        int nextStockSensorVersion)
    {
        Validate(snapshot, catalog);
        return ProductionBillRestoreCandidate.Create(
            snapshot,
            nextBillVersion,
            nextStockSensorVersion);
    }

    internal static void Validate(
        DungeonProductionBillSaveData snapshot,
        IResourceEconomyContentCatalog catalog)
    {
        if (snapshot == null)
        {
            throw new InvalidOperationException(
                "Production-bill payload is null.");
        }
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }
        if (snapshot.version != DungeonProductionBillSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Production-bill payload version {snapshot.version} is unsupported.");
        }
        if (snapshot.nextBillSequence <= 0
            || snapshot.bills == null
            || snapshot.installedStockSensorFacilityIds == null
            || snapshot.acknowledgedStockSensorFacilityIds == null)
        {
            throw new InvalidOperationException(
                "Production-bill payload has missing collections or an invalid next sequence.");
        }

        ValidateCanonicalBuildingIds(
            snapshot.installedStockSensorFacilityIds,
            "installed stock sensors");
        ValidateCanonicalBuildingIds(
            snapshot.acknowledgedStockSensorFacilityIds,
            "acknowledged stock sensors");
        HashSet<string> installed = new(
            snapshot.installedStockSensorFacilityIds,
            StringComparer.Ordinal);
        if (snapshot.acknowledgedStockSensorFacilityIds.Any(
                id => !installed.Contains(id)))
        {
            throw new InvalidOperationException(
                "Acknowledged stock sensors must be a subset of installed sensors.");
        }

        HashSet<ProductionBillId> billIds = new();
        int largestSequence = 0;
        foreach (ProductionBillSaveData saved in snapshot.bills)
        {
            ValidateBill(saved, catalog, billIds, ref largestSequence);
        }
        if (snapshot.nextBillSequence <= largestSequence)
        {
            throw new InvalidOperationException(
                "Production-bill next sequence collides with a persisted bill ID.");
        }
    }

    internal static ProductionBillSaveData ToSaveData(ProductionBillRecord record)
    {
        return new ProductionBillSaveData
        {
            billId = record.billId.Value,
            recipeId = record.recipeId,
            buildingInstanceId = record.buildingInstanceId.Value,
            mode = record.mode,
            remainingCycles = record.remainingCycles,
            targetStock = record.targetStock,
            minimumReserve = record.minimumReserve,
            suspended = record.suspended,
            materialsConsumed = record.materialsConsumed,
            processFluidConsumed = record.processFluidConsumed,
            completedWork = record.completedWork,
            batchStage = record.batchStage,
            remainingProcessingHours = record.remainingProcessingHours,
            batchIntegrity = record.batchIntegrity,
            utilityOutageHours = record.utilityOutageHours,
            temperatureOutageHours = record.temperatureOutageHours,
            occupiedSupportNodeId = record.occupiedSupportNodeId,
            blocked = CaptureFailure(record.blockedFailure),
            reservedWorkerId = string.Empty,
            materialDestinationId = record.materialDestinationId,
            prefetchBatchCount = record.prefetchBatchCount,
            estimatedDeliverySeconds = record.estimatedDeliverySeconds,
            estimatedProductionCycleSeconds =
                record.estimatedProductionCycleSeconds,
            logistics = CaptureLogistics(record.logisticsStatus),
            allowedMaterialIds = record.allowedMaterialIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            allowedWorkerIds = record.allowedWorkerIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            workerPolicy = record.workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(),
            workerContributions = record.workerContributions
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList(),
            hasPendingModeTransition = record.hasPendingModeTransition,
            pendingMode = record.pendingMode,
            outputDestinationId = record.outputDestinationId,
            outputReservations = record.outputReservations
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ProductionOutputReservationSaveData
                {
                    itemId = pair.Key,
                    amount = pair.Value
                })
                .ToList(),
            distributionMode = record.distributionMode,
            routePolicies = record.routePolicies
                .OrderBy(route => route.consumerId, StringComparer.Ordinal)
                .Select(route => route.Clone())
                .ToList(),
            selectedSupplies = record.selectedSupplies
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ProductionSelectedSupplySaveData
                {
                    supplyKey = pair.Key,
                    itemId = pair.Value
                })
                .ToList()
        };
    }

    private static void ValidateBill(
        ProductionBillSaveData saved,
        IResourceEconomyContentCatalog catalog,
        ISet<ProductionBillId> billIds,
        ref int largestSequence)
    {
        if (saved == null)
        {
            throw new InvalidOperationException(
                "Production-bill payload contains a null bill.");
        }
        string rawBillId = saved.billId ?? string.Empty;
        string rawBuildingId = saved.buildingInstanceId ?? string.Empty;
        ProductionBillId billId = (ProductionBillId)rawBillId;
        BuildingInstanceId buildingId =
            (BuildingInstanceId)rawBuildingId;
        if (!billId.IsValid
            || !string.Equals(
                billId.Value,
                rawBillId,
                StringComparison.Ordinal)
            || !TryParseSequence(billId, out int sequence)
            || !billIds.Add(billId)
            || !buildingId.IsValid
            || !string.Equals(
                buildingId.Value,
                rawBuildingId,
                StringComparison.Ordinal)
            || !IsCanonical(saved.recipeId)
            || !catalog.TryGetRecipe(saved.recipeId, out ProductionRecipeSO recipe))
        {
            throw new InvalidOperationException(
                "Production-bill payload contains an invalid/duplicate bill, building, or recipe ID.");
        }
        largestSequence = Math.Max(largestSequence, sequence);
        if (!Enum.IsDefined(typeof(ProductionOrderMode), saved.mode)
            || !Enum.IsDefined(typeof(ProductionBatchStage), saved.batchStage)
            || !Enum.IsDefined(
                typeof(ProductionDistributionMode),
                saved.distributionMode)
            || saved.remainingCycles < -1
            || saved.targetStock < 0
            || saved.minimumReserve < 0
            || saved.minimumReserve > saved.targetStock
            || !IsFiniteNonNegative(saved.completedWork)
            || !IsFiniteNonNegative(saved.remainingProcessingHours)
            || !IsFiniteInRange(saved.batchIntegrity, 0f, 100f)
            || !IsFiniteNonNegative(saved.utilityOutageHours)
            || !IsFiniteNonNegative(saved.temperatureOutageHours)
            || saved.prefetchBatchCount < 1
            || saved.prefetchBatchCount > MaximumPrefetchBatches
            || !IsFinitePositive(saved.estimatedDeliverySeconds)
            || !IsFiniteNonNegative(saved.estimatedProductionCycleSeconds))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' contains invalid scalar state.");
        }
        if (!string.Equals(
                saved.materialDestinationId,
                ProductionBillRuntime.DestinationPrefix + billId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                saved.outputDestinationId,
                ProductionBillRuntime.OutputDestinationPrefix + buildingId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' contains a non-canonical destination.");
        }
        ValidateStatus(saved.blocked, failure: true, billId.Value);
        ValidateStatus(saved.logistics, failure: false, billId.Value);
        ValidateCanonicalStrings(saved.allowedMaterialIds, "allowed material IDs");
        ValidateCanonicalStrings(saved.allowedWorkerIds, "allowed worker IDs");
        ValidateWorkerPolicy(saved.workerPolicy, billId);
        ValidateWorkerContributions(saved.workerContributions, billId);
        ValidateReservations(saved.outputReservations, catalog, billId);
        ValidateRoutes(saved.routePolicies, billId);
        ValidateSupplies(saved.selectedSupplies, catalog, billId);
        ValidateProcessState(saved, recipe, billId);
    }

    private static void ValidateWorkerPolicy(
        WorkerSelectionPolicySaveData policy,
        ProductionBillId billId)
    {
        if (policy == null
            || !Enum.IsDefined(typeof(WorkerSelectionMode), policy.mode)
            || !Enum.IsDefined(typeof(WorkerRequirementMatchMode), policy.matchMode)
            || !Enum.IsDefined(typeof(WorkerCandidateSortMode), policy.sortMode)
            || policy.minimumSkillExperience < 0
            || policy.minimumCareerRank < 0)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has an invalid worker policy.");
        }
        ValidateCanonicalStrings(policy.specificCharacterIds, "specific worker IDs");
        ValidateCanonicalStrings(policy.excludedCharacterIds, "excluded worker IDs");
        ValidateCanonicalStrings(policy.requiredTraitIds, "required worker traits");
        ValidateCanonicalStrings(policy.excludedTraitIds, "excluded worker traits");
    }

    private static void ValidateWorkerContributions(
        IReadOnlyList<CraftContributionSaveData> values,
        ProductionBillId billId)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (CraftContributionSaveData value in values
                     ?? Array.Empty<CraftContributionSaveData>())
        {
            if (value == null || !IsCanonical(value.characterId)
                || !ids.Add(value.characterId)
                || !IsFiniteNonNegative(value.contributedWork)
                || !IsFiniteNonNegative(value.relevantSkill))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has invalid worker contributions.");
            }
        }
    }

    private static void ValidateProcessState(
        ProductionBillSaveData saved,
        ProductionRecipeSO recipe,
        ProductionBillId billId)
    {
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch
            && saved.batchStage != ProductionBatchStage.None)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has an incompatible batch stage.");
        }
        if (saved.mode == ProductionOrderMode.RepeatCount
            && saved.remainingCycles < 0)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has invalid repeat-count state.");
        }
        if (saved.mode != ProductionOrderMode.RepeatCount
            && saved.remainingCycles != -1)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has non-canonical infinite-cycle state.");
        }
    }

    private static void ValidateStatus(
        ProductionStatusSaveData status,
        bool failure,
        string billId)
    {
        if (status == null || status.parameters == null)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has a missing status payload.");
        }
        bool codeValid = failure
            ? Enum.IsDefined(typeof(FailureCode), status.code)
                && status.outcome == ProductionBillOutcomeCode.None
            : status.code == FailureCode.None
                && Enum.IsDefined(
                    typeof(ProductionBillOutcomeCode),
                    status.outcome);
        if (!codeValid
            || status.parameters.Any(value => !IsCanonical(value)))
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has an invalid status code or parameter.");
        }
        if ((failure ? status.code == FailureCode.None
                : status.outcome == ProductionBillOutcomeCode.None)
            && status.parameters.Count != 0)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has parameters without a status code.");
        }
    }

    private static void ValidateReservations(
        IReadOnlyList<ProductionOutputReservationSaveData> reservations,
        IResourceEconomyContentCatalog catalog,
        ProductionBillId billId)
    {
        if (reservations == null)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has no output reservation list.");
        }
        string previous = string.Empty;
        foreach (ProductionOutputReservationSaveData reservation in reservations)
        {
            if (reservation == null
                || !IsCanonical(reservation.itemId)
                || !catalog.TryGetItem(reservation.itemId, out _)
                || reservation.amount <= 0
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, reservation.itemId) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has invalid output reservations.");
            }
            previous = reservation.itemId;
        }
    }

    private static void ValidateRoutes(
        IReadOnlyList<ProductionConsumerRoutePolicy> routes,
        ProductionBillId billId)
    {
        if (routes == null)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has no route-policy list.");
        }
        string previous = string.Empty;
        foreach (ProductionConsumerRoutePolicy route in routes)
        {
            if (route == null
                || !IsCanonical(route.consumerId)
                || route.minimumReserve < 0
                || route.targetStock < route.minimumReserve
                || route.weight < 1
                || route.weight > 10
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, route.consumerId) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has invalid route policies.");
            }
            previous = route.consumerId;
        }
    }

    private static void ValidateSupplies(
        IReadOnlyList<ProductionSelectedSupplySaveData> supplies,
        IResourceEconomyContentCatalog catalog,
        ProductionBillId billId)
    {
        if (supplies == null)
        {
            throw new InvalidOperationException(
                $"Production bill '{billId}' has no selected-supply list.");
        }
        string previous = string.Empty;
        foreach (ProductionSelectedSupplySaveData supply in supplies)
        {
            if (supply == null
                || !IsCanonical(supply.supplyKey)
                || !IsCanonical(supply.itemId)
                || !catalog.TryGetItem(supply.itemId, out _)
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, supply.supplyKey) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production bill '{billId}' has invalid selected supplies.");
            }
            previous = supply.supplyKey;
        }
    }

    private static void ValidateCanonicalBuildingIds(
        IReadOnlyList<string> ids,
        string label)
    {
        string previous = string.Empty;
        foreach (string id in ids)
        {
            if (!((BuildingInstanceId)id).IsValid
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, id) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production-bill {label} are invalid, duplicated, or unordered.");
            }
            previous = id;
        }
    }

    private static void ValidateCanonicalStrings(
        IReadOnlyList<string> values,
        string label)
    {
        if (values == null)
        {
            throw new InvalidOperationException(
                $"Production-bill {label} list is null.");
        }
        string previous = string.Empty;
        foreach (string value in values)
        {
            if (!IsCanonical(value)
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, value) >= 0))
            {
                throw new InvalidOperationException(
                    $"Production-bill {label} are invalid, duplicated, or unordered.");
            }
            previous = value;
        }
    }

    private static ProductionStatusSaveData CaptureFailure(
        DomainFailure failure)
    {
        return new ProductionStatusSaveData
        {
            code = failure.Code,
            outcome = ProductionBillOutcomeCode.None,
            parameters = failure.Parameters.ToArray().ToList()
        };
    }

    private static DomainFailure RestoreFailure(ProductionStatusSaveData saved) =>
        new(saved.code, saved.parameters.ToArray());

    private static ProductionStatusSaveData CaptureLogistics(
        ProductionLogisticsStatus status)
    {
        return new ProductionStatusSaveData
        {
            code = FailureCode.None,
            outcome = status.Code,
            parameters = status.Parameters.ToList()
        };
    }

    private static ProductionLogisticsStatus RestoreLogistics(
        ProductionStatusSaveData saved) =>
        new(saved.outcome, saved.parameters.ToArray());

    private static List<string> CanonicalIds(IEnumerable<string> source) =>
        (source ?? Array.Empty<string>())
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

    private static bool TryParseSequence(
        ProductionBillId billId,
        out int sequence)
    {
        const string prefix = "production-bill:";
        return int.TryParse(
            billId.Value.AsSpan(prefix.Length),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out sequence)
            && sequence > 0
            && string.Equals(
                billId.Value,
                prefix + sequence.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool IsFinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    private static bool IsFiniteInRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;
}
