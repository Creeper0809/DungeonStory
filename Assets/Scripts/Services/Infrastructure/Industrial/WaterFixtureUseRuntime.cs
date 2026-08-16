using System;
using UnityEngine;

public sealed class WaterFixtureUseRuntime : IWaterFixtureUseRuntime
{
    private readonly IFluidInfrastructureTransaction water;
    private readonly IFluidWastewaterTransaction wastewater;
    private readonly IWorldItemStackRuntime items;
    private readonly IWorldFilthQuery filth;
    private readonly ICharacterNeedBalanceRuntime needBalance;

    public WaterFixtureUseRuntime(
        IFluidInfrastructureTransaction water,
        IFluidWastewaterTransaction wastewater,
        IWorldItemStackRuntime items,
        IWorldFilthQuery filth,
        ICharacterNeedBalanceRuntime needBalance)
    {
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.wastewater = wastewater
            ?? throw new ArgumentNullException(nameof(wastewater));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
        this.needBalance = needBalance
            ?? throw new ArgumentNullException(nameof(needBalance));
    }

    public bool TryBeginUse(
        BuildableObject fixture,
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

            // A dry-capable fixture can complete this use without water. Do
            // not earmark the settlement's loose drinking stock for an
            // optional upgrade after the authoritative dry fallback has
            // already been selected. Fixtures that cannot run dry still
            // publish the physical delivery request required to unblock use.
            if (!ability.allowsDryFallback)
            {
                items.TryRequestFacilityDelivery(
                    StockCategory.Water,
                    1,
                    fixture.centerPos,
                    destinationId,
                    out _,
                    out _);
            }
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
}
