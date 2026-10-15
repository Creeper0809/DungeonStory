using System;
using DungeonStory.Foundation.Facts;

public readonly struct WildlifeHuntAttackResolvedEvent
{
    public WildlifeHuntAttackResolvedEvent(CharacterId hunter, string hunterName, string wildlifeId,
        string wildlifeName, CombatBodyPart bodyPart, int appliedDamage, bool hit, bool coverBlocked, bool killed)
    {
        if (!hunter.IsValid || !Text(hunterName) || !Text(wildlifeId) || !Text(wildlifeName) || appliedDamage < 0)
            throw new ArgumentException("wildlife-hunt-attack-fact-invalid");
        Hunter = hunter; HunterName = hunterName; WildlifeId = wildlifeId; WildlifeName = wildlifeName;
        BodyPart = bodyPart; AppliedDamage = appliedDamage; Hit = hit; CoverBlocked = coverBlocked; Killed = killed;
    }
    public CharacterId Hunter { get; }
    public string HunterName { get; }
    public string WildlifeId { get; }
    public string WildlifeName { get; }
    public CombatBodyPart BodyPart { get; }
    public int AppliedDamage { get; }
    public bool Hit { get; }
    public bool CoverBlocked { get; }
    public bool Killed { get; }
    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 16384, true);
}

public readonly struct WildlifeHuntCompletedEvent
{
    public WildlifeHuntCompletedEvent(CharacterId hunter, string speciesId, bool dangerous, float maximumHealth, int absoluteDay)
    {
        if (!hunter.IsValid || !DomainFactValueSyntax.IsText(speciesId, 16384, true)
            || !float.IsFinite(maximumHealth) || maximumHealth < 0f || absoluteDay < 0)
            throw new ArgumentException("wildlife-hunt-completed-fact-invalid");
        Hunter = hunter; SpeciesId = speciesId; Dangerous = dangerous; MaximumHealth = maximumHealth; AbsoluteDay = absoluteDay;
    }
    public CharacterId Hunter { get; }
    public string SpeciesId { get; }
    public bool Dangerous { get; }
    public float MaximumHealth { get; }
    public int AbsoluteDay { get; }
}
