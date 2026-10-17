using System;

public enum CharacterWaterSourceKind
{
    WorldWater = 0,
    Facility = 1,
    ItemInstance = 2
}

[Serializable]
public sealed class CharacterWaterFactData
{
    public string characterId = string.Empty;
    public string characterName = string.Empty;
    public string sourceId = string.Empty;
    public string pathogenDiseaseId = string.Empty;
    public CharacterWaterSourceKind sourceKind;
    public WorldWaterQuality quality;
    public int absoluteDay;
    public float consumed;
    public float thirstBefore;
    public float thirstAfter;
    public float healthBefore;
    public float healthAfter;
    public float hygieneBefore;
    public float hygieneAfter;
    public float infectionBefore;
    public float infectionAfter;
    public float moodBefore;
    public float moodAfter;

    public CharacterWaterFactData Clone() => (CharacterWaterFactData)MemberwiseClone();
}

[Serializable]
public sealed class CharacterWaterPublicationData
{
    public string publicationKey = string.Empty;
    public CharacterWaterFactData fact = new();
    public bool notificationDispatched;

    public CharacterWaterPublicationData Clone() => new()
    {
        publicationKey = publicationKey,
        fact = fact?.Clone(),
        notificationDispatched = notificationDispatched
    };
}
