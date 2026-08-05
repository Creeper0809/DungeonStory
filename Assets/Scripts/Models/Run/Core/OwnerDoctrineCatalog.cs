using System.Collections.Generic;

/// <summary>
/// Stable protocol IDs. Doctrine content is authored in GameDomainContentCatalogSO.
/// </summary>
public static class OwnerDoctrineIds
{
    public const string SlimeStewardship = "owner:doctrine:slime-stewardship";
    public const string OrcWarCamp = "owner:doctrine:orc-war-camp";
    public const string VampireForbiddenStudy = "owner:doctrine:vampire-forbidden-study";
}

public sealed class OwnerDoctrineDefinition
{
    public OwnerDoctrineDefinition(
        string id,
        string speciesTag,
        string title,
        string benefit,
        string tradeoff,
        IReadOnlyList<IRunVariableEffect> effects)
    {
        this.id = id?.Trim() ?? string.Empty;
        this.speciesTag = speciesTag?.Trim() ?? string.Empty;
        this.title = title?.Trim() ?? string.Empty;
        this.benefit = benefit?.Trim() ?? string.Empty;
        this.tradeoff = tradeoff?.Trim() ?? string.Empty;
        this.effects = EventPayloadSnapshot.Copy(effects);
    }

    public string id { get; }
    public string speciesTag { get; }
    public string title { get; }
    public string benefit { get; }
    public string tradeoff { get; }
    public IReadOnlyList<IRunVariableEffect> effects { get; }
}
