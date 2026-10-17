using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionBillRuntime :
    IProductionBillCoreQuery,
    IProductionFacilityDestructiveDrainPreparedOutputQuery,
    IProductionBillCoreOrderCommand,
    IProductionBillCoreWorkExecution,
    IProductionBillDetachedFacilityPersistence,
    IProductionGenericBillWipTerminalCheckpointGcPort,
    ITickable
{
    public const string DestinationPrefix = ProductionBillIdentity.DestinationPrefix;
    public const string OutputDestinationPrefix = ProductionBillIdentity.OutputDestinationPrefix;
    public const string StockSensorDestinationPrefix = ProductionBillIdentity.StockSensorDestinationPrefix;
    public const string StockSensorItemId = ProductionBillIdentity.StockSensorItemId;
    public const string GenericBillLifecycleSchema = ProductionBillIdentity.GenericBillLifecycleSchema;

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionAssemblyBridge items;
    private readonly IProductionOutputExecutionService outputExecution;
    private readonly IProductionPreparedOutputExecutionPort preparedOutputExecution;
    private readonly IProductionStockSensorRuntime stockSensors;
    private readonly ProductionAggregateStateStore stateStore;
    private readonly IProductionAssemblyBridge buildingWorld;
    private readonly IProductionInputDestinationClaimRuntime inputDestinationClaims;
    private readonly ProductionBillRuntimeQuery billQueries;
    private readonly ProductionBillInputRuntime inputRuntime;
    private readonly ProductionBillCommandRuntime commandRuntime;
    private readonly ProductionBillTerminalRuntime terminalRuntime;
    private readonly ProductionBillExecutionRuntime executionRuntime;
    private IReadOnlyList<ProductionBillRecord> bills => stateStore.Bills;
    private int nextBillSequence
    {
        get => stateStore.NextBillSequence;
        set => stateStore.NextBillSequence = value;
    }

    public ProductionBillRuntime(
        ProductionBillOrderDependencies order,
        ProductionBillExecutionDependencies execution)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }
        if (execution == null)
        {
            throw new ArgumentNullException(nameof(execution));
        }
        catalog = order.Catalog;
        items = order.Bridge;
        stockSensors = order.StockSensors;
        stateStore = order.StateStore;
        outputExecution = execution.OutputExecution;
        preparedOutputExecution = execution.PreparedOutputExecution;
        buildingWorld = execution.Bridge;
        inputDestinationClaims = order.InputDestinationClaims;
        billQueries = new ProductionBillRuntimeQuery(order, execution);
        inputRuntime = new ProductionBillInputRuntime(order, execution, billQueries);
        commandRuntime = new ProductionBillCommandRuntime(order, execution, billQueries, inputRuntime);
        terminalRuntime = new ProductionBillTerminalRuntime(order, execution, billQueries, inputRuntime, commandRuntime);
        executionRuntime = new ProductionBillExecutionRuntime(
            order, execution, billQueries, inputRuntime, commandRuntime, terminalRuntime);
    }

    public int Version => stateStore.BillVersion;

    [GameplayInternalOnly("Shared production quality command", "ProductionBillSceneFacade")]
    public ProductionBillCommandResult SetMinimumCraftQuality(ProductionBillId billId, int minimumTier)
    {
        return commandRuntime.SetMinimumCraftQuality(billId, minimumTier);
    }

    public bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionGenericBillTerminalDrainSaveData> producers,
        out IProductionGenericBillWipTerminalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        return terminalRuntime.TryPrepareCheckpointGarbageCollection(producers, out candidate, out failureReason);
    }

    public bool TryPublishCheckpointGarbageCollection(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        return terminalRuntime.TryPublishCheckpointGarbageCollection(candidate, out failureReason);
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
    {
        terminalRuntime.RollbackCheckpointGarbageCollection(candidate);
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
    {
        terminalRuntime.CompleteCheckpointGarbageCollection(candidate);
    }

    public IReadOnlyList<ProductionBillSnapshot> GetBills(ProductionFacilityHandle facility)
    {
        return billQueries.GetBills(facility);
    }

    public ProductionFacilityBillLifecycleSnapshot CaptureFacilityLifecycle(
        BuildingInstanceId facilityId)
    {
        return billQueries.CaptureFacilityLifecycle(facilityId);
    }

    public IReadOnlyList<ProductionFacilityDestructiveDrainPreparedOutputOwner>
        CapturePreparedOutputOwners(BuildingInstanceId facilityId)
    {
        return billQueries.CapturePreparedOutputOwners(facilityId);
    }

    public ProductionBillCommandResult AddBill(
        ProductionFacilityHandle facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount)
    {
        return commandRuntime.AddBill(facility, recipeId, mode, amount);
    }

    public ProductionBillCommandResult RemoveBill(
        ProductionBillId billId,
        bool returnMaterials)
    {
        return terminalRuntime.RemoveBill(billId, returnMaterials);
    }

    public ProductionBillCommandResult MoveBill(
        ProductionBillId billId,
        int targetIndex)
    {
        return commandRuntime.MoveBill(billId, targetIndex);
    }

    public ProductionBillCommandResult SetSuspended(
        ProductionBillId billId,
        bool suspended)
    {
        return commandRuntime.SetSuspended(billId, suspended);
    }

    public ProductionBillCommandResult SetStockPolicy(
        ProductionBillId billId,
        int minimumReserve,
        int targetStock)
    {
        return commandRuntime.SetStockPolicy(billId, minimumReserve, targetStock);
    }

    public ProductionBillCommandResult SetOrderMode(
        ProductionBillId billId,
        ProductionOrderMode mode,
        int amount)
    {
        return commandRuntime.SetOrderMode(billId, mode, amount);
    }

    public ProductionBillCommandResult SetDistributionPolicy(
        ProductionBillId billId,
        ProductionDistributionMode mode,
        IReadOnlyList<ProductionConsumerRoutePolicy> routes)
    {
        return commandRuntime.SetDistributionPolicy(billId, mode, routes);
    }

    public ProductionBillCommandResult SetWorkerPolicy(
        ProductionBillId billId,
        WorkerSelectionPolicySaveData policy)
    {
        return commandRuntime.SetWorkerPolicy(billId, policy);
    }

    public ProductionBillCommandResult SetEmergencyWorker(
        ProductionBillId billId,
        string characterId)
    {
        return commandRuntime.SetEmergencyWorker(billId, characterId);
    }

    public bool HasStockSensor(ProductionFacilityHandle facility)
    {
        return commandRuntime.HasStockSensor(facility);
    }

    public ProductionBillCommandResult RequestStockSensorInstallation(
        ProductionFacilityHandle facility)
    {
        return commandRuntime.RequestStockSensorInstallation(facility);
    }

    public ProductionBillCommandResult RemoveStockSensor(
        ProductionFacilityHandle facility)
    {
        return commandRuntime.RemoveStockSensor(facility);
    }

    public ProductionBillCommandResult AcknowledgeStockSensorUnlock(
        ProductionFacilityHandle facility)
    {
        return commandRuntime.AcknowledgeStockSensorUnlock(facility);
    }

    public ProductionWorkAvailabilityResult CheckWorkAvailability(
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId)
        => executionRuntime.CheckWorkAvailability(facility, workTypeId);

    public ProductionWorkBeginResult BeginWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        WorkTypeId workTypeId)
        => executionRuntime.BeginWork(worker, facility, workTypeId);

    public ProductionWorkExecutionResult ExecuteWork(
        ProductionWorkerHandle worker,
        ProductionFacilityHandle facility,
        ProductionBillId billId,
        float amount)
        => executionRuntime.ExecuteWork(worker, facility, billId, amount);

    public void Tick()
        => executionRuntime.Tick();

    public DungeonProductionBillSaveData Capture()
    {
        commandRuntime.AssertCommandObservationIdle();
        return ProductionBillStateCodec.Capture(
            nextBillSequence,
            stateStore.NextCommandSequence,
            stateStore.PendingCommandObservations,
            bills,
            stockSensors.InstalledFacilityIds,
            stockSensors.AcknowledgedFacilityIds,
            stockSensors.PendingInstallations,
            stockSensors.InstalledSensors,
            stockSensors.PendingRemovals,
            stateStore.WipTerminalReceipts);
    }

    public ProductionBillRestoreCandidate BuildRestore(
        DungeonProductionBillSaveData snapshot)
    {
        commandRuntime.AssertCommandObservationIdle();
        ProductionBillRestoreCandidate candidate =
            ProductionBillStateCodec.CreateRestoreCandidate(
            snapshot,
            catalog,
            stateStore.BillVersion + 1,
            stateStore.StockSensorVersion + 1);
        foreach (ProductionBillRecord record in candidate.Bills)
        {
            ProductionRecipeSO recipe = billQueries.ResolveRecipe(record)
                ?? throw new InvalidOperationException(
                    "Production restore references a missing recipe: "
                    + (record?.recipeId ?? string.Empty));
            bool usesPreparedOutput = billQueries.UsesPreparedOutput(recipe);
            bool hasPreparedAuthority = record.preparedOutput != null
                && record.preparedOutput.phase !=
                    ProductionPreparedOutputPhase.Unresolved;
            if (usesPreparedOutput
                && ProductionPreparedOutputMigrationScope
                    .HasLegacyOutputAuthority(record))
            {
                throw new InvalidOperationException(
                    $"Production bill '{record.billId.Value}' restored legacy output authority for a standard prepared capability.");
            }
            if (!usesPreparedOutput && hasPreparedAuthority)
            {
                throw new InvalidOperationException(
                    $"Production bill '{record.billId.Value}' restored prepared authority for a non-standard output capability.");
            }
        }
        foreach (ProductionResolvedOutputSaveData output in candidate.Bills
                     .SelectMany(record => record.resolvedOutputs))
        {
            DomainFailure failure = outputExecution.ValidateOne(output);
            if (failure.IsFailure)
            {
                throw new InvalidOperationException(
                    "Production resolved-output capability restore validation failed: "
                    + failure.Code);
            }
        }
        foreach (ProductionPreparedOutputLineSaveData line in candidate.Bills
                     .Where(record => record.preparedOutput != null
                         && record.preparedOutput.phase !=
                            ProductionPreparedOutputPhase.Unresolved)
                     .SelectMany(record => record.preparedOutput.lines)
                     .Where(line => ProductionOutputRoleRules.IsPhysical(
                         line.role)))
        {
            ProductionOutputCapabilityDescriptor capability = new(
                line.outputLineId,
                line.itemId,
                line.outputCapabilityId,
                line.outputCapabilityVersion,
                line.outputComponentCodecId,
                line.outputComponentCodecVersion,
                line.outputCapabilityFingerprint);
            if (!items.TryValidateOutputCapability(
                    capability,
                    out DomainFailure failure))
            {
                throw new InvalidOperationException(
                    "Production prepared-output capability restore validation failed: "
                    + failure.Code);
            }
        }
        return candidate;
    }

    public void Restore(ProductionBillRestoreCandidate candidate)
    {
        commandRuntime.AssertCommandObservationIdle();
        Restore(candidate, buildingWorld.Facilities);
    }

    public void Restore(
        ProductionBillRestoreCandidate candidate,
        IReadOnlyList<ProductionFacilityHandle> detachedFacilities)
    {
        commandRuntime.AssertCommandObservationIdle();
        ProductionBillRestoreCandidate exact = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        IReadOnlyList<ProductionFacilityHandle> facilities = detachedFacilities
            ?? throw new ArgumentNullException(nameof(detachedFacilities));
        ProductionBillRecord[] inputDestinationBills = exact.Bills
            .Where(record => inputRuntime.RequiresPhysicalInputDestination(
                record,
                facilities))
            .ToArray();
        Dictionary<string, long> inputBufferMassGramsByBillId =
            inputDestinationBills
            .OrderBy(record => record.billId.Value, StringComparer.Ordinal)
            .ToDictionary(
                record => record.billId.Value,
                record => inputRuntime.ResolveInputBufferMassCapacity(
                    record,
                    facilities),
                StringComparer.Ordinal);
        if (!inputDestinationClaims.TryReplace(
                inputDestinationBills,
                facilities,
                inputBufferMassGramsByBillId,
                out string claimFailure))
        {
            throw new InvalidOperationException(
                "Production input destination claims could not be restored: "
                + claimFailure);
        }
        preparedOutputExecution.RestoreDestinationAuthorities(
            exact.Bills,
            facilities);
        if (!stockSensors.TryReconcileDestinationAuthorities(
                out string sensorFailure))
        {
            throw new InvalidOperationException(
                "Production stock-sensor destination authorities could not be restored: "
                + sensorFailure);
        }
        executionRuntime.NotifyOutputAuthoritiesRestored(buildingWorld.BuildingVersion);
        stateStore.Replace(
            exact);
    }

}
