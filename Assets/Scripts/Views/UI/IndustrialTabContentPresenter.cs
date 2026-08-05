using System;
using System.Linq;

public sealed class IndustrialTabContentPresenter : IUITabContentPresenter
{
    private readonly IPowerInfrastructureQuery power;
    private readonly IFluidInfrastructureQuery water;
    private readonly IConveyorInfrastructureQuery conveyors;
    private readonly IAutomationInfrastructureQuery automation;

    public IndustrialTabContentPresenter(
        IPowerInfrastructureQuery power,
        IFluidInfrastructureQuery water,
        IConveyorInfrastructureQuery conveyors,
        IAutomationInfrastructureQuery automation)
    {
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.conveyors = conveyors
            ?? throw new ArgumentNullException(nameof(conveyors));
        this.automation = automation
            ?? throw new ArgumentNullException(nameof(automation));
    }

    public TabId Id => TabId.Industry;

    public string Build()
    {
        int conveyorProblems = conveyors.Networks.Count(network =>
            network.State is ConveyorNetworkState.Stalled
                or ConveyorNetworkState.Deadlocked);
        return string.Join(
            "\n",
            $"전력망: {power.Networks.Count}",
            $"상하수도망: {water.Networks.Count}",
            $"컨베이어망: {conveyors.Networks.Count}",
            $"정체·교착: {conveyorProblems}",
            $"자동화 시설: {automation.Facilities.Count}");
    }
}
