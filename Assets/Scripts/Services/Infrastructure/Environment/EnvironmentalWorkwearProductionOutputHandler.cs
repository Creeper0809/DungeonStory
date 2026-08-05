using System;
using System.Collections.Generic;
using System.Globalization;

public sealed class EnvironmentalWorkwearProductionOutputHandler :
    IProductionOutputHandler,
    IDomainFailureProductionOutputHandler
{
    private readonly IEnvironmentalWorkwearCatalog catalog;
    private readonly IWorldItemStackRuntime items;

    public EnvironmentalWorkwearProductionOutputHandler(
        IEnvironmentalWorkwearCatalog catalog,
        IWorldItemStackRuntime items)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public bool CanHandle(string itemId)
    {
        return catalog.TryGetByItemDefinitionId(itemId, out _);
    }

    public bool TryProduce(
        ProductionOutputContext context,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (context.Facility == null
            || context.Amount <= 0
            || !catalog.TryGetByItemDefinitionId(context.ItemId, out _))
        {
            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearProductionContextInvalid,
                context.ItemId ?? string.Empty,
                context.Amount.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        string destinationId = ProductionBillRuntime.OutputDestinationPrefix
            + context.Facility.RequirePersistentInstanceId().Value;
        List<string> createdStackIds = new();
        for (int index = 0; index < context.Amount; index++)
        {
            if (items.SpawnUniqueItemAt(
                    context.ItemId,
                    context.Facility.centerPos,
                    WorldItemStackState.FacilityOutputBuffer,
                    destinationId,
                    out string stackId)
                && TryValidateUniquePhysicalOutput(stackId, context.ItemId))
            {
                createdStackIds.Add(stackId);
                continue;
            }

            foreach (string createdStackId in createdStackIds)
            {
                items.DeleteStack(createdStackId);
            }

            failure = new DomainFailure(
                FailureCode.EnvironmentWorkwearOutputSpawnFailed,
                context.ItemId,
                context.Amount.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        return true;
    }

    bool IProductionOutputHandler.TryProduce(
        ProductionOutputContext context,
        out string diagnosticCode)
    {
        bool succeeded = TryProduce(context, out DomainFailure failure);
        diagnosticCode = succeeded ? string.Empty : failure.Code.ToString();
        return succeeded;
    }

    private bool TryValidateUniquePhysicalOutput(string stackId, string itemId)
    {
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
        {
            if (stack != null
                && string.Equals(stack.StackId, stackId, StringComparison.Ordinal)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && stack.Quantity == 1
                && ((ItemInstanceId)stack.ItemInstanceId).IsValid)
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(stackId))
        {
            items.DeleteStack(stackId);
        }
        return false;
    }
}
