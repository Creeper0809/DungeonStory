using System;

[Serializable]
public sealed class GameplayNarrativeEventContext
{
    public string eventInstanceId = string.Empty;
    public string chainId = string.Empty;
    public string locationId = string.Empty;
    public string locationDisplayName = string.Empty;
    public string actorId = string.Empty;
    public string actorDisplayName = string.Empty;
    public string counterpartyId = string.Empty;
    public string counterpartyDisplayName = string.Empty;
    public string usedObjectId = string.Empty;
    public string usedObjectDisplayName = string.Empty;
    public string resultDetail = string.Empty;
    public int occurredDay;
    public long sequence;

    public bool HasNarrativeDetail =>
        !string.IsNullOrWhiteSpace(locationDisplayName)
        && !string.IsNullOrWhiteSpace(actorDisplayName)
        && (!string.IsNullOrWhiteSpace(counterpartyDisplayName)
            || !string.IsNullOrWhiteSpace(usedObjectDisplayName)
            || !string.IsNullOrWhiteSpace(resultDetail));

    public GameplayNarrativeEventContext Clone() => new GameplayNarrativeEventContext
    {
        eventInstanceId = Canonical(eventInstanceId),
        chainId = Canonical(chainId),
        locationId = Canonical(locationId),
        locationDisplayName = Canonical(locationDisplayName),
        actorId = Canonical(actorId),
        actorDisplayName = Canonical(actorDisplayName),
        counterpartyId = Canonical(counterpartyId),
        counterpartyDisplayName = Canonical(counterpartyDisplayName),
        usedObjectId = Canonical(usedObjectId),
        usedObjectDisplayName = Canonical(usedObjectDisplayName),
        resultDetail = Canonical(resultDetail),
        occurredDay = Math.Max(0, occurredDay),
        sequence = Math.Max(0L, sequence)
    };

    // Transitional caller adapter; state owners accept only domain provenance.
    public CharacterEventContext ToDomainContext()
    {
        var source = Clone();
        return new CharacterEventContext
        {
            eventInstanceId = source.eventInstanceId, chainId = source.chainId,
            locationId = source.locationId, locationDisplayName = source.locationDisplayName,
            actorId = source.actorId, actorDisplayName = source.actorDisplayName,
            counterpartyId = source.counterpartyId, counterpartyDisplayName = source.counterpartyDisplayName,
            usedObjectId = source.usedObjectId, usedObjectDisplayName = source.usedObjectDisplayName,
            resultDetail = source.resultDetail, occurredDay = source.occurredDay, sequence = source.sequence
        };
    }

    private static string Canonical(string value) => value?.Trim() ?? string.Empty;
}
