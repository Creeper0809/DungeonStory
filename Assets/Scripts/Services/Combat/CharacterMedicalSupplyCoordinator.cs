using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class CharacterMedicalSupplyCoordinator
{
    private readonly CharacterMedicalWorldServices world;
    private readonly IResourceEconomyContentCatalog resourceCatalog;

    public CharacterMedicalSupplyCoordinator(
        CharacterMedicalWorldServices world,
        IResourceEconomyContentCatalog resourceCatalog)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.resourceCatalog = resourceCatalog
            ?? throw new ArgumentNullException(nameof(resourceCatalog));
    }

    public bool EnsureTreatmentSupplyReady(
        CharacterMedicalOrder order,
        BuildableObject facility)
    {
        if (order == null || facility == null)
        {
            return false;
        }

        EnsureDestination(order);
        if (order.treatmentSupply == CharacterMedicalSupplyKind.None
            && !TryRequestMedicine(order, facility)
            && !TryRequestExtractedBlood(order, facility))
        {
            order.SetStatus(CharacterMedicalStatusCode.SupplyUnavailable);
            return false;
        }

        if (order.treatmentSupplyConsumed)
        {
            return true;
        }

        bool consumed = TryConsumeAssignedSupply(order);
        if (!consumed)
        {
            order.SetStatus(
                order.treatmentSupply == CharacterMedicalSupplyKind.Medicine
                    ? CharacterMedicalStatusCode.AwaitingMedicineDelivery
                    : CharacterMedicalStatusCode.AwaitingExtractedBloodDelivery);
            return false;
        }

        ApplyState(
            order,
            CharacterMedicalSupplyPolicy.MarkConsumed(CreateCurrentState(order)));
        return true;
    }

    private static void EnsureDestination(CharacterMedicalOrder order)
    {
        if (!string.IsNullOrWhiteSpace(order.treatmentMaterialDestinationId))
        {
            return;
        }

        order.treatmentMaterialDestinationId =
            WorldItemStackRuntime.FacilityInputDestinationPrefix
            + $"medical:{order.orderId}";
    }

    private bool TryConsumeAssignedSupply(CharacterMedicalOrder order)
    {
        if (order.treatmentSupply == CharacterMedicalSupplyKind.Medicine
            && !string.IsNullOrWhiteSpace(order.treatmentItemId))
        {
            Dictionary<string, int> exactCost = new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                [order.treatmentItemId] = 1
            };
            return world.ItemStacks.TryConsumeFacilityItemBuffer(
                order.treatmentMaterialDestinationId,
                exactCost,
                out _);
        }

        IReadOnlyDictionary<StockCategory, int> cost =
            order.treatmentSupply == CharacterMedicalSupplyKind.Medicine
                ? CharacterMedicalOrderPersistence.MedicineCost
                : CharacterMedicalOrderPersistence.ExtractedBloodCost;
        return world.ItemStacks.TryConsumeFacilityBuffer(
            order.treatmentMaterialDestinationId,
            cost,
            out _);
    }

    private bool TryRequestMedicine(
        CharacterMedicalOrder order,
        BuildableObject facility)
    {
        if (order.treatmentSupplyDeliveryRequested)
        {
            return order.treatmentSupply == CharacterMedicalSupplyKind.Medicine;
        }

        CharacterMedicalMedicineCandidate[] candidates = resourceCatalog.Items
            .Where(item => item != null
                && item.Kind == ResourceItemKind.Medicine
                && item.SupportsInjuryTreatment)
            .Select(item => new CharacterMedicalMedicineCandidate(
                item.ItemId,
                item.UnitPrice,
                item.TreatmentPotency,
                item.InfectionReduction,
                item.PainReduction))
            .ToArray();
        IReadOnlyList<CharacterMedicalMedicineCandidate> ranked =
            CharacterMedicalSupplyPolicy.RankMedicines(
                candidates,
                order.requiredTreatmentWork);
        foreach (CharacterMedicalMedicineCandidate medicine in ranked)
        {
            Dictionary<string, int> bufferedCost = new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                [medicine.ItemId] = 1
            };
            if (world.ItemStacks.TryConsumeFacilityItemBuffer(
                    order.treatmentMaterialDestinationId,
                    bufferedCost,
                    out _))
            {
                ApplyState(
                    order,
                    CharacterMedicalSupplyPolicy.CreateMedicine(
                        medicine,
                        consumed: true));
                order.SetStatus(
                    CharacterMedicalStatusCode.MedicineReady,
                    medicine.ItemId);
                return true;
            }

            if (!world.ItemStacks.TryRequestItemDelivery(
                    medicine.ItemId,
                    1,
                    facility.centerPos,
                    order.treatmentMaterialDestinationId,
                    out int requested,
                    out _)
                || requested < 1)
            {
                continue;
            }

            ApplyState(
                order,
                CharacterMedicalSupplyPolicy.CreateMedicine(
                    medicine,
                    consumed: false));
            order.SetStatus(
                CharacterMedicalStatusCode.AwaitingMedicineDelivery,
                medicine.ItemId);
            return true;
        }

        return false;
    }

    private bool TryRequestExtractedBlood(
        CharacterMedicalOrder order,
        BuildableObject facility)
    {
        if (!world.ItemStacks.TryRequestFacilityDelivery(
                StockCategory.Biological,
                1,
                facility.centerPos,
                order.treatmentMaterialDestinationId,
                out int requested,
                out _)
            || requested < 1)
        {
            return false;
        }

        ApplyState(order, CharacterMedicalSupplyPolicy.CreateExtractedBlood());
        return true;
    }

    private static CharacterMedicalSupplyState CreateCurrentState(
        CharacterMedicalOrder order) =>
        new CharacterMedicalSupplyState(
            order.treatmentSupply,
            order.treatmentSupplyConsumed,
            order.treatmentSupplyDeliveryRequested,
            order.treatmentItemId,
            order.treatmentPotency,
            order.treatmentInfectionReduction,
            order.treatmentPainReduction);

    private static void ApplyState(
        CharacterMedicalOrder order,
        CharacterMedicalSupplyState state)
    {
        order.treatmentSupply = state.Kind;
        order.treatmentSupplyConsumed = state.Consumed;
        order.treatmentSupplyDeliveryRequested = state.DeliveryRequested;
        order.treatmentItemId = state.ItemId;
        order.treatmentPotency = state.Potency;
        order.treatmentInfectionReduction = state.InfectionReduction;
        order.treatmentPainReduction = state.PainReduction;
    }
}
