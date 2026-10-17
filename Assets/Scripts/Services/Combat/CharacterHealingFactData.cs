using System;

[Serializable]
public sealed class CharacterHealingFactData
{
    public string characterId = string.Empty;
    public string characterName = string.Empty;
    public int absoluteDay;
    public float healthBefore;
    public float maximumHealthBefore;
    public float healthAfter;
    public float maximumHealthAfter;
    public bool downedBefore;
    public bool downedAfter;

    public CharacterHealingFactData Clone() =>
        (CharacterHealingFactData)MemberwiseClone();
}

[Serializable]
public sealed class CharacterHealingPendingFactData
{
    public string criticalPublicationKey = string.Empty;
    public string recoveryPublicationKey = string.Empty;
    public CharacterHealingFactData fact = new();

    public CharacterHealingPendingFactData Clone()
    {
        var clone = (CharacterHealingPendingFactData)MemberwiseClone();
        clone.fact = fact?.Clone();
        return clone;
    }
}
