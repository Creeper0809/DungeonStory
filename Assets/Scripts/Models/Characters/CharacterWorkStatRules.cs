using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CharacterWorkStatRules
{
    public static CharacterStatType GetBestWorkStat(FacilityWorkType workTypes)
    {
        if ((workTypes & FacilityWorkType.Construct) != 0) return CharacterStatType.Dexterity;
        if ((workTypes & FacilityWorkType.Research) != 0) return CharacterStatType.Research;
        if ((workTypes & FacilityWorkType.Guard) != 0) return CharacterStatType.Attack;
        if ((workTypes & FacilityWorkType.Clean) != 0) return CharacterStatType.Cleaning;
        if ((workTypes & FacilityWorkType.DrawWater) != 0) return CharacterStatType.Endurance;
        if ((workTypes & FacilityWorkType.Cook) != 0) return CharacterStatType.Dexterity;
        if ((workTypes & FacilityWorkType.Treat) != 0) return CharacterStatType.Research;
        if ((workTypes & FacilityWorkType.Refuel) != 0) return CharacterStatType.Strength;
        if ((workTypes & FacilityWorkType.Restock) != 0) return CharacterStatType.Strength;
        if ((workTypes & FacilityWorkType.Repair) != 0) return CharacterStatType.Dexterity;
        if ((workTypes & FacilityWorkType.Operate) != 0) return CharacterStatType.Sales;
        if ((workTypes & FacilityWorkType.Rescue) != 0) return CharacterStatType.Toughness;
        return CharacterStatType.Endurance;
    }
}
