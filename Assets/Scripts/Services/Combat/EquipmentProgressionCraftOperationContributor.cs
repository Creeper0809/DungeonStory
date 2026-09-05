using System;
using System.Collections.Generic;
using System.Linq;

public sealed class LineageTransferCraftOperationContributor :
    ICraftPersistentOperationContributor
{
    public const string StableContributorId =
        "equipment-progression:lineage-transfer-work";

    private readonly ICombatEquipmentRuntime equipment;

    public LineageTransferCraftOperationContributor(
        ICombatEquipmentRuntime equipment)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
    }

    public string ContributorId => StableContributorId;

    public bool TryCapturePlan(
        CraftFacilityHandle facility,
        out CraftWorkExecutionPlan plan)
    {
        if (facility?.RuntimeObject is not BuildableObject building
            || !EquipmentProgressionFacilityContract.Matches(
                building,
                EquipmentProgressionWorkstationTags.LineageArchive))
        {
            plan = default;
            return false;
        }

        EquipmentHistoryTransferOrder order = equipment.HistoryTransferOrders
            .Where(value => value != null
                && !value.completed
                && string.Equals(
                    value.facilityPersistentId,
                    facility.PersistentId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (order == null)
        {
            plan = default;
            return false;
        }

        plan = new CraftWorkExecutionPlan(
            CraftWorkOperationKind.RegisteredCapability,
            order.orderId,
            order.requiredWork,
            order.completedWork,
            "계보 계승",
            ContributorId);
        return true;
    }

    public CraftWorkProgressResult ApplyProgress(
        CraftFacilityHandle facility,
        CraftWorkExecutionPlan plan,
        float amount)
    {
        if (facility?.RuntimeObject is not BuildableObject building
            || plan.Kind != CraftWorkOperationKind.RegisteredCapability
            || !string.Equals(
                plan.ContributorId,
                ContributorId,
                StringComparison.Ordinal)
            || amount <= 0f)
        {
            return new CraftWorkProgressResult(false, false);
        }

        bool succeeded = equipment.ApplyHistoryTransferWork(
            plan.OperationId,
            amount,
            building,
            out bool completed,
            out _);
        return new CraftWorkProgressResult(
            succeeded,
            succeeded && completed);
    }
}

public sealed class EquipmentProgressionFacilityOutputDispositionContributor :
    IProductionFacilityOutputDispositionContributor
{
    private readonly IReadOnlyDictionary<string, Descriptor> descriptors;

    public EquipmentProgressionFacilityOutputDispositionContributor()
    {
        Descriptor[] values =
        {
            new(
                EquipmentProgressionWorkstationTags.RuneTuning,
                "equipment-progression:rune-tuning-state",
                ProductionFacilityOutputRouteKind.CommandEffect),
            new(
                EquipmentProgressionWorkstationTags.LineageArchive,
                "equipment-progression:lineage-transfer",
                ProductionFacilityOutputRouteKind.InputTransfer)
        };
        descriptors = values.ToDictionary(
            value => value.WorkstationTag,
            StringComparer.Ordinal);
    }

    public string ContributorId =>
        "equipment-progression:facility-output-disposition";

    public int ContractVersion => 1;

    public ProductionFacilityOutputDispositionContribution Capture(
        BuildingSO definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));

        string workstationTag =
            definition.GetProductionWorkstationAbility()?.WorkstationTag
            ?? string.Empty;
        if (!descriptors.TryGetValue(
                workstationTag,
                out Descriptor descriptor))
        {
            return new ProductionFacilityOutputDispositionContribution(
                ContributorId,
                ContractVersion,
                Array.Empty<ProductionFacilityOutputDispositionClaim>());
        }

        return new ProductionFacilityOutputDispositionContribution(
            ContributorId,
            ContractVersion,
            new[]
            {
                new ProductionFacilityOutputDispositionClaim(
                    descriptor.CapabilityId,
                    ProductionFacilityOutputEffectKind.StateMutation,
                    descriptor.RouteKind,
                    true)
            });
    }

    private readonly struct Descriptor
    {
        public Descriptor(
            string workstationTag,
            string capabilityId,
            ProductionFacilityOutputRouteKind routeKind)
        {
            WorkstationTag = workstationTag;
            CapabilityId = capabilityId;
            RouteKind = routeKind;
        }

        public string WorkstationTag { get; }
        public string CapabilityId { get; }
        public ProductionFacilityOutputRouteKind RouteKind { get; }
    }
}
