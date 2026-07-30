using System;
using UnityEngine;

public sealed class WaterFixtureUseRuntime : IWaterFixtureUseRuntime
{
    private readonly IWaterNetworkRuntime water;
    private readonly IWastewaterNetworkRuntime wastewater;
    private readonly IWorldItemStackRuntime items;
    private readonly IWorldFilthQuery filth;

    public WaterFixtureUseRuntime(
        IWaterNetworkRuntime water,
        IWastewaterNetworkRuntime wastewater,
        IWorldItemStackRuntime items,
        IWorldFilthQuery filth)
    {
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.wastewater = wastewater
            ?? throw new ArgumentNullException(nameof(wastewater));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
    }

    public bool TryBeginUse(
        BuildableObject fixture,
        out WaterFixtureUseTicket ticket,
        out string failureReason)
    {
        ticket = default;
        failureReason = string.Empty;
        BuildingWaterFixtureAbility ability =
            fixture?.BuildingData?.GetAbility<BuildingWaterFixtureAbility>();
        if (fixture == null || ability == null)
        {
            return true;
        }

        string fixtureId = IndustrialInfrastructureIdentity.GetNodeId(fixture);
        if (water.TryConsume(
                fixture,
                ability.minimumQuality,
                ability.cleanWaterPerUse,
                out _,
                out string pipeFailure))
        {
            ticket = new WaterFixtureUseTicket(
                fixtureId,
                WaterFixtureSupplyKind.Piped,
                ability.wastewaterPerUse);
            return true;
        }

        if (ability.allowsManualWaterFallback)
        {
            string destinationId = CreateManualDestinationId(fixtureId);
            if (water.TryConsumeManualContainer(
                    fixture,
                    destinationId,
                    ability.cleanWaterPerUse,
                    out _))
            {
                ticket = new WaterFixtureUseTicket(
                    fixtureId,
                    WaterFixtureSupplyKind.ManualContainer,
                    ability.wastewaterPerUse);
                return true;
            }

            items.TryRequestFacilityDelivery(
                StockCategory.Water,
                1,
                fixture.centerPos,
                destinationId,
                out _,
                out _);
        }

        if (ability.allowsDryFallback)
        {
            ticket = new WaterFixtureUseTicket(
                fixtureId,
                WaterFixtureSupplyKind.DryFallback,
                ability.wastewaterPerUse);
            return true;
        }

        failureReason = ability.allowsManualWaterFallback
            ? "물통 보충을 기다리는 중입니다."
            : pipeFailure;
        return false;
    }

    public void CompleteUse(
        BuildableObject fixture,
        WaterFixtureUseTicket ticket)
    {
        if (fixture == null || !ticket.IsValid)
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
