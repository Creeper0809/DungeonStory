using System;

internal sealed class InvasionIntruderContentBinding
{
    private IInvasionIntruderPatternDefinitionCatalog patternCatalog;

    public InvasionIntruderPatternDefinition Default => RequireCatalog().Default;

    public void Configure(
        IInvasionIntruderPatternDefinitionCatalog patternCatalog)
    {
        this.patternCatalog = patternCatalog
            ?? throw new ArgumentNullException(nameof(patternCatalog));
    }

    public InvasionIntruderPatternDefinition Resolve(string id)
    {
        IInvasionIntruderPatternDefinitionCatalog catalog = RequireCatalog();
        return catalog.Get(id) ?? catalog.Default;
    }

    private IInvasionIntruderPatternDefinitionCatalog RequireCatalog()
    {
        return patternCatalog ?? throw new InvalidOperationException(
            "Invasion intruder runtime requires authored invasion pattern content before use.");
    }
}
