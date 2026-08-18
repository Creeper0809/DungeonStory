using System;
using System.Linq;
using UnityEngine;

public sealed class WaterFixtureUseRuntime : IWaterFixtureUseRuntime
{
    private readonly IFluidInfrastructureTransaction water;
    private readonly IFluidWastewaterTransaction wastewater;
    private readonly IWorldItemStackRuntime items;
    private readonly IWorldFilthQuery filth;
    private readonly ICharacterNeedBalanceRuntime needBalance;
    private readonly IWorkforceReplanService workforce;

    public WaterFixtureUseRuntime(
        IFluidInfrastructureTransaction water,
        IFluidWastewaterTransaction wastewater,
        IWorldItemStackRuntime items,
        IWorldFilthQuery filth,
        ICharacterNeedBalanceRuntime needBalance,
        IWorkforceReplanService workforce)
    {
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.wastewater = wastewater
            ?? throw new ArgumentNullException(nameof(wastewater));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
        this.needBalance = needBalance
            ?? throw new ArgumentNullException(nameof(needBalance));
        this.workforce = workforce
            ?? throw new ArgumentNullException(nameof(workforce));
    }

    public bool TryBeginUse(
        BuildableObject fixture,
        CharacterId protectedCharacterId,
        out WaterFixtureUseTicket ticket,
        out DomainFailure failure)
    {
        ticket = default;
        failure = DomainFailure.None;
        BuildingWaterFixtureAbility ability =
            fixture?.BuildingData?.GetAbility<BuildingWaterFixtureAbility>();
        if (fixture == null || ability == null)
        {
            return true;
        }

        BuildingInstanceId fixtureId = new BuildingInstanceId(
            IndustrialInfrastructureIdentity.GetNodeId(fixture));
        float personalWater =
            needBalance.ApplyPersonalContinuousWaterMultiplier(
                ability.cleanWaterPerUse);
        float wastewaterAmount = ability.cleanWaterPerUse > 0f
            ? ability.wastewaterPerUse
                * personalWater
                / ability.cleanWaterPerUse
            : ability.wastewaterPerUse;
        if (water.TryConsume(
                fixture,
                ability.minimumQuality,
                personalWater,
                out _,
                out DomainFailure pipeFailure))
        {
            ticket = new WaterFixtureUseTicket(
                fixtureId,
                WaterFixtureSupplyKind.Piped,
                wastewaterAmount);
            return true;
        }

        if (ability.allowsManualWaterFallback)
        {
            string destinationId = CreateManualDestinationId(fixtureId.Value);
            bool routeExisted = HasRoutedManualWater(destinationId);
            if (water.TryConsumeManualContainer(
                    fixture,
                    destinationId,
                    personalWater,
                    out _))
            {
                ticket = new WaterFixtureUseTicket(
                    fixtureId,
                    WaterFixtureSupplyKind.ManualContainer,
                    wastewaterAmount);
                return true;
            }

            if (!routeExisted
                && HasRoutedManualWater(destinationId))
            {
                workforce.RequestOneHaulerToReplan(
                    clearFailures: true,
                    forceInterrupt: true,
                    protectedCharacterId: protectedCharacterId,
                    forcePriorityWakeFanout: true);
            }

            // TryConsumeManualContainer owns the exact missing-quantity
            // calculation and publishes the physical delivery request. Do not
            // request again here: repeated facility-use retries would otherwise
            // duplicate the same manual-water commitment.
        }

        if (ability.allowsDryFallback)
        {
            ticket = new WaterFixtureUseTicket(
                fixtureId,
                WaterFixtureSupplyKind.DryFallback,
                wastewaterAmount);
            return true;
        }

        failure = ability.allowsManualWaterFallback
            ? new DomainFailure(FailureCode.FluidManualWaterUnavailable)
            : pipeFailure;
        return false;
    }

    public void CompleteUse(
        BuildableObject fixture,
        WaterFixtureUseTicket ticket)
    {
        if (fixture == null
            || !ticket.IsValid
            || !ticket.FixtureId.Equals(new BuildingInstanceId(
                IndustrialInfrastructureIdentity.GetNodeId(fixture))))
        {
            return;
        }

        if (ticket.SupplyKind == WaterFixtureSupplyKind.DryFallback)
        {
            filth.AddFilth(
                WorldFilthType.Sewage,
                fixture.centerPos,
                8f,
                string.Empty,
                0.45f);
            return;
        }

        if (ticket.WastewaterAmount <= 0f)
        {
            return;
        }

        if (wastewater.CanAcceptWastewater(
                fixture,
                ticket.WastewaterAmount,
                out _))
        {
            wastewater.TryAddWastewater(
                fixture,
                ticket.WastewaterAmount,
                out _,
                out _);
            return;
        }

        BuildingWaterFixtureAbility ability =
            fixture.BuildingData?.GetAbility<BuildingWaterFixtureAbility>();
        if (!string.IsNullOrWhiteSpace(ability?.manualWasteItemId)
            && items.SpawnItemAt(
                ability.manualWasteItemId,
                Mathf.Max(1, Mathf.CeilToInt(ticket.WastewaterAmount)),
                fixture.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            && spawned > 0)
        {
            return;
        }

        wastewater.TryAddWastewater(
            fixture,
            ticket.WastewaterAmount,
            out _,
            out _);
    }

    private static string CreateManualDestinationId(string fixtureId) =>
        $"plumbing:manual-water:{fixtureId}";

    private bool HasRoutedManualWater(string destinationId)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            return false;
        }

        string[] waterItemIds = items.CatalogProvider.All
            .Where(definition => definition != null
                && definition.StockCategory == StockCategory.Water)
            .Select(definition => definition.ItemId)
            .Where(itemId => !string.IsNullOrWhiteSpace(itemId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return items.GetAllStacks().Any(stack => stack != null
                && stack.Quantity > 0
                && waterItemIds.Contains(stack.ItemId, StringComparer.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destination,
                    StringComparison.Ordinal))
            || waterItemIds.Any(itemId =>
                items.GetCommittedHaulDeliveryQuantity(destination, itemId) > 0);
    }
}
