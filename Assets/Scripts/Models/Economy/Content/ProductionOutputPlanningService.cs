using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionOutputReservationPlan
{
    public ProductionOutputReservationPlan(
        string destinationId,
        IReadOnlyDictionary<string, int> reservations)
    {
        DestinationId = destinationId ?? string.Empty;
        Reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
    }

    public string DestinationId { get; }
    public IReadOnlyDictionary<string, int> Reservations { get; }
}

public interface IProductionOutputPlanningService
{
    bool TryCreateReservation(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        string destinationId,
        IReadOnlyDictionary<string, int> reservationsByOtherBills,
        bool alreadyReserved,
        out ProductionOutputReservationPlan plan,
        out string failureReason);
    bool HasCapacity(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        string destinationId,
        IReadOnlyDictionary<string, int> reservationsByOtherBills,
        bool alreadyReserved,
        out string failureReason);
    int ResolveCapacity(
        ProductionFacilityHandle facility,
        string itemId,
        int outputPerBatch);
    string ResolveDestinationId(ProductionFacilityHandle facility);
    float ResolveSupportModifier(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe,
        ProductionSupportModifierKind kind,
        float defaultValue,
        bool multiply);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionOutputPlanningService :
    IProductionOutputPlanningService
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionAssemblyBridge bridge;


    public ProductionOutputPlanningService(
        IResourceEconomyContentCatalog catalog,
        IProductionAssemblyBridge bridge)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    public bool TryCreateReservation(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        string destinationId,
        IReadOnlyDictionary<string, int> reservationsByOtherBills,
        bool alreadyReserved,
        out ProductionOutputReservationPlan plan,
        out string failureReason)
    {
        plan = null;
        failureReason = string.Empty;
        if (recipe == null || facility == null)
        {
            failureReason = "production-output-target-missing";
            return false;
        }

        destinationId = string.IsNullOrWhiteSpace(destinationId)
            ? ResolveDestinationId(facility)
            : destinationId;
        if (!alreadyReserved
            && !HasCapacity(
                recipe,
                facility,
                destinationId,
                reservationsByOtherBills,
                alreadyReserved,
                out failureReason))
        {
            return false;
        }

        Dictionary<string, int> requested = recipe.Outputs
            .Where(value => value != null && value.Amount > 0)
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.Amount),
                StringComparer.Ordinal);
        plan = new ProductionOutputReservationPlan(destinationId, requested);
        return true;
    }

    public bool HasCapacity(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        string destinationId,
        IReadOnlyDictionary<string, int> reservationsByOtherBills,
        bool alreadyReserved,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (recipe == null
            || facility == null
            || alreadyReserved)
        {
            return true;
        }

        destinationId = string.IsNullOrWhiteSpace(destinationId)
            ? ResolveDestinationId(facility)
            : destinationId;
        reservationsByOtherBills ??=
            new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (IGrouping<string, ProductionOutputDefinition> group in
                 recipe.Outputs
                     .Where(output => output != null && output.Amount > 0)
                     .GroupBy(output => output.ItemId, StringComparer.Ordinal))
        {
            int outputPerBatch = group.Sum(output => output.Amount);
            int buffered = bridge.CountBufferedOutput(group.Key, destinationId);
            int reserved = reservationsByOtherBills.TryGetValue(
                group.Key,
                out int amount)
                    ? amount
                    : 0;
            int capacity = ResolveCapacity(
                facility,
                group.Key,
                outputPerBatch);
            if (buffered + reserved + outputPerBatch > capacity)
            {
                failureReason =
                    $"production-output-full:{group.Key}:"
                    + $"{buffered + reserved}:{capacity}";
                return false;
            }
        }

        return true;
    }

    public int ResolveCapacity(
        ProductionFacilityHandle facility,
        string itemId,
        int outputPerBatch)
    {
        int stackLimit = catalog.TryGetItem(
                itemId,
                out ResourceItemDefinitionSO item)
            ? item.MaxStack
            : 1;
        int capacity = bridge.ResolveOutputCapacity(
            facility,
            itemId,
            outputPerBatch,
            stackLimit);
        int authoredMinimum = itemId switch
        {
            "material:lead-ingot" => 24,
            "material:lead-shot" => 96,
            "material:black-powder" => 48,
            "material:paper" => 64,
            "ammo:paper-cartridge" => 120,
            "component:machine-parts" => 16,
            "component:precision-parts" => 16,
            _ => 1
        };
        return Mathf.Max(capacity, authoredMinimum);
    }

    public string ResolveDestinationId(ProductionFacilityHandle facility)
    {
        return ProductionBillRuntime.OutputDestinationPrefix
            + (facility == null
                ? string.Empty
                : facility.InstanceId.Value);
    }

    public float ResolveSupportModifier(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe,
        ProductionSupportModifierKind kind,
        float defaultValue,
        bool multiply)
    {
        return bridge.ResolveSupportModifier(
            facility,
            recipe,
            kind,
            defaultValue,
            multiply);
    }
}
