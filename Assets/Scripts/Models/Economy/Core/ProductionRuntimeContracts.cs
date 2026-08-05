using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-facing production ports that still require legacy runtime actors.
/// Pure production value types and persistence contracts live in
/// DungeonStory.Production.
/// </summary>
public interface IProductionBillQuery
{
    int Version { get; }
    IReadOnlyList<ProductionBillSnapshot> GetBills(BuildableObject facility);
    bool HasStockSensor(BuildableObject facility);
}

public interface IProductionBillOrderCommand :
    IProductionDistributionPolicyCommand
{
    ProductionBillCommandResult AddBill(
        BuildableObject facility,
        string recipeId,
        ProductionOrderMode mode,
        int amount);
    ProductionBillCommandResult RemoveBill(
        ProductionBillId billId,
        bool returnMaterials);
    ProductionBillCommandResult MoveBill(
        ProductionBillId billId,
        int targetIndex);
    ProductionBillCommandResult SetSuspended(
        ProductionBillId billId,
        bool suspended);
    ProductionBillCommandResult SetStockPolicy(
        ProductionBillId billId,
        int minimumReserve,
        int targetStock);
    ProductionBillCommandResult SetOrderMode(
        ProductionBillId billId,
        ProductionOrderMode mode,
        int amount);
    ProductionBillCommandResult RequestStockSensorInstallation(
        BuildableObject facility);
    ProductionBillCommandResult AcknowledgeStockSensorUnlock(
        BuildableObject facility);
    ProductionBillCommandResult RemoveStockSensor(
        BuildableObject facility);
}

public interface IProductionBillWorkExecution
{
    ProductionWorkAvailabilityResult CheckWorkAvailability(
        BuildableObject facility,
        WorkTypeId workTypeId);
    ProductionWorkBeginResult BeginWork(
        CharacterActor worker,
        BuildableObject facility,
        WorkTypeId workTypeId);
    ProductionWorkExecutionResult ExecuteWork(
        CharacterActor worker,
        BuildableObject facility,
        ProductionBillId billId,
        float amount);
}

public readonly struct ProductionOutputContext
{
    public ProductionOutputContext(
        ProductionRecipeSO recipe,
        BuildableObject facility,
        CharacterActor worker,
        string itemId,
        int amount,
        float qualityModifier = 0f)
    {
        Recipe = recipe;
        Facility = facility;
        Worker = worker;
        ItemId = itemId ?? string.Empty;
        Amount = Mathf.Max(1, amount);
        QualityModifier = qualityModifier;
    }

    public ProductionRecipeSO Recipe { get; }
    public BuildableObject Facility { get; }
    public CharacterActor Worker { get; }
    public string ItemId { get; }
    public int Amount { get; }
    public float QualityModifier { get; }
}

public interface IProductionOutputHandler
{
    bool CanHandle(string itemId);
    bool TryProduce(
        ProductionOutputContext context,
        out string failureReason);
}

/// <summary>
/// Localization-neutral production output boundary for handlers that expose
/// stable domain failures. Legacy handlers can continue implementing
/// <see cref="IProductionOutputHandler"/> until their own domain is migrated.
/// </summary>
public interface IDomainFailureProductionOutputHandler
{
    bool TryProduce(
        ProductionOutputContext context,
        out DomainFailure failure);
}
