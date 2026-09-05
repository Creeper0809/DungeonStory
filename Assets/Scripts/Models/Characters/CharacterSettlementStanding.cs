using System;

public enum CharacterSettlementStanding
{
    Unknown = 0,
    PreparedCandidate = 1,
    Visitor = 2,
    Resident = 3,
    Minion = 4
}

public readonly struct CharacterSettlementPopulationSnapshot
{
    public CharacterSettlementPopulationSnapshot(int residents, int minions)
    {
        Residents = Math.Max(0, residents);
        Minions = Math.Max(0, minions);
    }

    public int Residents { get; }
    public int Minions { get; }
    public int SettlementPopulation => Residents + Minions;
    public float MinionRatio => SettlementPopulation > 0
        ? Minions / (float)SettlementPopulation
        : 0f;
}

public static class CharacterSettlementStandingRules
{
    public static CharacterSettlementStanding NormalizeLegacy(
        CharacterSettlementStanding standing,
        bool isStaff,
        bool isVisiting,
        bool isOwner = false)
    {
        if (standing != CharacterSettlementStanding.Unknown)
        {
            return standing;
        }
        if (isOwner || isStaff)
        {
            return CharacterSettlementStanding.Resident;
        }
        return isVisiting
            ? CharacterSettlementStanding.Visitor
            : CharacterSettlementStanding.PreparedCandidate;
    }

    public static bool IsSettlementResident(
        CharacterSettlementStanding standing) => standing is
        CharacterSettlementStanding.Resident or CharacterSettlementStanding.Minion;
}
