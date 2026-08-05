using System;
using UnityEngine.Scripting.APIUpdating;

public interface IRunResultPanelService
{
    RunResultPanel Show(RunResultSnapshot result);
}

public interface IRunResultPanelRegistry
{
    RunResultPanel Current { get; }
    void Register(RunResultPanel panel);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RunResultPanelRegistry : IRunResultPanelRegistry
{
    public RunResultPanelRegistry(RunResultPanel initialPanel = null) => Current = initialPanel;
    public RunResultPanel Current { get; private set; }
    public void Register(RunResultPanel panel) => Current = panel ?? throw new ArgumentNullException(nameof(panel));
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RunResultPanelService : IRunResultPanelService
{
    private readonly IRunResultPanelRegistry registry;
    private readonly IRunResultPanelFactory factory;
    public RunResultPanelService(IRunResultPanelRegistry registry, IRunResultPanelFactory factory)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }
    public RunResultPanel Show(RunResultSnapshot result)
    {
        RunResultPanel panel = registry.Current ?? factory.CreateDefaultPanel();
        panel.Render(result); return panel;
    }
}
