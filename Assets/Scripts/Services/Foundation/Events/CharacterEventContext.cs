using System;
using DungeonStory.Foundation.Facts;

// Domain provenance only. Observers decide whether/how it becomes a memory.
[Serializable]
public sealed class CharacterEventContext
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

    public CharacterEventContext Clone() => (CharacterEventContext)MemberwiseClone();
    public void Validate()
    {
        foreach (string text in new[] { eventInstanceId, chainId, locationId, locationDisplayName,
            actorId, actorDisplayName, counterpartyId, counterpartyDisplayName,
            usedObjectId, usedObjectDisplayName, resultDetail })
            if (!DomainFactValueSyntax.IsText(text, 16384, true))
                throw new ArgumentException("character-event-context-text-invalid");
        if (occurredDay < 0 || sequence < 0)
            throw new ArgumentException("character-event-context-time-invalid");
    }
}
