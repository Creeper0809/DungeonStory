using System.Collections.Generic;

/// <summary>
/// Narrow command boundary for editing a production bill's output routes.
/// </summary>
public interface IProductionDistributionPolicyCommand
{
    ProductionBillCommandResult SetDistributionPolicy(
        ProductionBillId billId,
        ProductionDistributionMode mode,
        IReadOnlyList<ProductionConsumerRoutePolicy> routes);
}
