using System;

[Serializable]
public sealed class CharacterFatalityFactData
{
    public string characterId = string.Empty;
    public string characterName = string.Empty;
    public int absoluteDay;
    public CharacterDeathCauseCode cause;
    public string reasonCode = string.Empty;
    public float healthBefore;
    public float maximumHealth;
    public float healthAfter;
    public bool downedBefore;
    public bool downedAfter;
    public CharacterFatalityFactData Clone() => (CharacterFatalityFactData)MemberwiseClone();
}

[Serializable]
public sealed class CharacterFatalityPendingFactData
{
    public string deathPublicationKey = string.Empty;
    public string criticalPublicationKey = string.Empty;
    public string downedPublicationKey = string.Empty;
    public CharacterFatalityFactData fact = new();
    public CharacterFatalityPendingFactData Clone() => new()
    {
        deathPublicationKey = deathPublicationKey,
        criticalPublicationKey = criticalPublicationKey,
        downedPublicationKey = downedPublicationKey,
        fact = fact?.Clone()
    };
}
