using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class WildlifeCarcassProcessingData
{
    public string operationId = string.Empty;
    public string transformOperationId = string.Empty;
    public int absoluteDay;
    public bool taboo;
    public string actorId = string.Empty;
    public string actorDisplayName = string.Empty;
    public string speciesId = string.Empty;
    public string speciesDisplayName = string.Empty;
    public string carcassStackId = string.Empty;
    public string facilityId = string.Empty;
    public int inputQuantity;
    public int outputQuantity;
    public string sourceCharacterId = string.Empty;
    public string sourceDisplayName = string.Empty;
    public bool sameSpecies;

    public WildlifeCarcassProcessingData Clone() => (WildlifeCarcassProcessingData)MemberwiseClone();
    public void Validate()
    {
        if (!Id(operationId) || !Id(transformOperationId) || !Id(carcassStackId) || absoluteDay < 1
            || !OptionalId(actorId) || !OptionalId(facilityId) || !OptionalId(sourceCharacterId)
            || inputQuantity <= 0 || outputQuantity <= 0
            || !DomainFactValueSyntax.IsText(actorDisplayName, 256, actorId == string.Empty)
            || actorId == string.Empty && actorDisplayName != string.Empty
            || !DomainFactValueSyntax.IsText(sourceDisplayName, 256, !taboo))
            throw new InvalidOperationException("carcass-processing-fact-invalid");
        if (taboo)
        {
            if (actorId == string.Empty || speciesId != string.Empty || speciesDisplayName != string.Empty)
                throw new InvalidOperationException("carcass-processing-taboo-context-invalid");
        }
        else if (!Id(speciesId) || !DomainFactValueSyntax.IsText(speciesDisplayName, 256, false)
                 || sourceCharacterId != string.Empty || sourceDisplayName != string.Empty || sameSpecies)
            throw new InvalidOperationException("carcass-processing-species-context-invalid");
    }
    private static bool Id(string value) => DomainFactValueSyntax.IsIdentity(value);
    private static bool OptionalId(string value) => value == string.Empty || Id(value);
}

public sealed class WildlifeCarcassProcessedFact
{
    private readonly WildlifeCarcassProcessingData frozen;
    public WildlifeCarcassProcessedFact(WildlifeCarcassProcessingData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate();
        frozen = source.Clone();
    }
    public WildlifeCarcassProcessingData Snapshot() => frozen.Clone();
}

[Serializable]
public sealed class WildlifeCarcassProcessingSaveData
{
    public WildlifeCarcassProcessingData fact = new();
    public string publicationKey = string.Empty;
    public bool publicationConfirmed;
    public WildlifeCarcassProcessingSaveData Clone() => new()
    { fact = fact?.Clone(), publicationKey = publicationKey, publicationConfirmed = publicationConfirmed };
    public void Validate()
    {
        if (fact == null || !DomainFactValueSyntax.IsText(publicationKey, 1024, false))
            throw new InvalidOperationException("carcass-processing-journal-invalid");
        fact.Validate();
    }
}
