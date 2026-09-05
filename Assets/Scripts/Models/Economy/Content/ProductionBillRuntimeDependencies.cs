using System;
using DungeonStory.Foundation;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionBillOrderDependencies
{
    public ProductionBillOrderDependencies(
        IResourceEconomyContentCatalog catalog,
        IProductionAssemblyBridge bridge,
        IProductionStockSensorRuntime stockSensors,
        ProductionAggregateStateStore stateStore,
        IProductionInputDestinationClaimRuntime inputDestinationClaims,
        IProductionFacilityMutationEpochQuery facilityMutationEpoch,
        IRecipeBalanceWorkCalculator balanceWorkCalculator = null)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        StockSensors = stockSensors
            ?? throw new ArgumentNullException(nameof(stockSensors));
        StateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        InputDestinationClaims = inputDestinationClaims
            ?? throw new ArgumentNullException(nameof(inputDestinationClaims));
        FacilityMutationEpoch = facilityMutationEpoch
            ?? throw new ArgumentNullException(nameof(facilityMutationEpoch));
        BalanceWorkCalculator = balanceWorkCalculator;
    }

    public IResourceEconomyContentCatalog Catalog { get; }
    public IProductionAssemblyBridge Bridge { get; }
    public IProductionStockSensorRuntime StockSensors { get; }
    public ProductionAggregateStateStore StateStore { get; }
    public IProductionInputDestinationClaimRuntime InputDestinationClaims { get; }
    public IProductionFacilityMutationEpochQuery FacilityMutationEpoch { get; }
    public IRecipeBalanceWorkCalculator BalanceWorkCalculator { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionBillExecutionDependencies
{
    public ProductionBillExecutionDependencies(
        IProductionOutputPlanningService outputPlanning,
        IProductionOutputExecutionService outputExecution,
        IProductionPreparedOutputExecutionPort preparedOutputExecution,
        IProductionRuinedBatchExecutionPort ruinedBatchExecution,
        IProductionBillSnapshotProjector snapshotProjector,
        IProductionAssemblyBridge bridge,
        IGameClock clock,
        IProductionPreparedOutputRoutingAuthority preparedOutputRouting = null,
        IProductionRecipeExecutionReceiptAuthority recipeExecutionReceipts = null)
    {
        OutputPlanning = outputPlanning
            ?? throw new ArgumentNullException(nameof(outputPlanning));
        OutputExecution = outputExecution
            ?? throw new ArgumentNullException(nameof(outputExecution));
        PreparedOutputExecution = preparedOutputExecution
            ?? throw new ArgumentNullException(nameof(preparedOutputExecution));
        RuinedBatchExecution = ruinedBatchExecution
            ?? throw new ArgumentNullException(nameof(ruinedBatchExecution));
        SnapshotProjector = snapshotProjector
            ?? throw new ArgumentNullException(nameof(snapshotProjector));
        Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        PreparedOutputRouting = preparedOutputRouting
            ?? EmptyProductionPreparedOutputRoutingAuthority.Instance;
        RecipeExecutionReceipts = recipeExecutionReceipts
            ?? EmptyProductionRecipeExecutionReceiptAuthority.Instance;
    }

    public IProductionOutputPlanningService OutputPlanning { get; }
    public IProductionOutputExecutionService OutputExecution { get; }
    public IProductionPreparedOutputExecutionPort PreparedOutputExecution { get; }
    public IProductionRuinedBatchExecutionPort RuinedBatchExecution { get; }
    public IProductionBillSnapshotProjector SnapshotProjector { get; }
    public IProductionAssemblyBridge Bridge { get; }
    public IGameClock Clock { get; }
    public IProductionPreparedOutputRoutingAuthority PreparedOutputRouting { get; }
    public IProductionRecipeExecutionReceiptAuthority RecipeExecutionReceipts
        { get; }
}

/// <summary>
/// Compatibility query used by narrow, non-migrated fixtures that construct
/// <see cref="ProductionBillExecutionDependencies"/> directly. Production
/// composition injects the durable authority. Mutation calls fail loudly so a
/// migrated prepared-output path cannot silently run without that authority.
/// </summary>
internal sealed class EmptyProductionPreparedOutputRoutingAuthority :
    IProductionPreparedOutputRoutingAuthority
{
    internal static readonly EmptyProductionPreparedOutputRoutingAuthority Instance =
        new();

    private EmptyProductionPreparedOutputRoutingAuthority()
    {
    }

    public void PublishCommittedBatch(
        ProductionPreparedOutputBatchSaveData completedBatch,
        BuildingInstanceId ownerFacilityId) => throw MissingAuthority();

    public System.Collections.Generic.IReadOnlyList<
        ProductionPreparedOutputRoutingLineSnapshot> CaptureAll() =>
        Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>();

    public System.Collections.Generic.IReadOnlyList<
        ProductionPreparedOutputRoutingLineSnapshot> CaptureBill(
            ProductionBillId ownerBillId) =>
        Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>();

    public System.Collections.Generic.IReadOnlyList<
        ProductionPreparedOutputRoutingLineSnapshot> CaptureDestination(
            string destinationId) =>
        Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>();

    public bool HasOutstandingForBill(ProductionBillId ownerBillId) => false;

    public bool CanRetireBill(ProductionBillId ownerBillId) => true;

    public ProductionPreparedOutputRouteRequestSnapshot PrepareRoute(
        string batchCommitId,
        string lineCommitId,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        int routedQuantity) => throw MissingAuthority();

    public System.Collections.Generic.IReadOnlyList<
        ProductionPreparedOutputRouteRequestSnapshot> CaptureRouteOperations() =>
        Array.Empty<ProductionPreparedOutputRouteRequestSnapshot>();

    public void CommitPhysicalRoute(
        ProductionPreparedOutputPhysicalRouteReceipt receipt) =>
        throw MissingAuthority();

    public void AcknowledgePhysicalRoute(
        string routeOperationId,
        string physicalReceiptFingerprint) => throw MissingAuthority();

    private static InvalidOperationException MissingAuthority() => new(
        "Prepared-output routing mutation requires the durable production routing authority.");
}
