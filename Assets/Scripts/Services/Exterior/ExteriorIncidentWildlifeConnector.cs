using System;
using VContainer.Unity;

public sealed class ExteriorIncidentWildlifeConnector : IStartable
{
    private readonly PredatorApproachExteriorIncidentHandler predatorHandler;
    private readonly IWildlifeRuntime wildlifeRuntime;

    public ExteriorIncidentWildlifeConnector(
        PredatorApproachExteriorIncidentHandler predatorHandler,
        IWildlifeRuntime wildlifeRuntime)
    {
        this.predatorHandler = predatorHandler
            ?? throw new ArgumentNullException(nameof(predatorHandler));
        this.wildlifeRuntime = wildlifeRuntime
            ?? throw new ArgumentNullException(nameof(wildlifeRuntime));
    }

    public void Start()
    {
        predatorHandler.BindWildlifeRuntime(wildlifeRuntime);
    }
}
