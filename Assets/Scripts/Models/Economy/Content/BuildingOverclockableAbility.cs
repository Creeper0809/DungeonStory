using System;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[BuildingAbilityDisplayName("오버클럭 지원")]
public sealed class BuildingOverclockableAbility : BuildingAbility
{
    public bool allowControlled = true;
    public bool allowAggressive = true;
    public bool allowCritical = true;

    public bool Allows(OverclockTier tier)
    {
        return tier switch
        {
            OverclockTier.Controlled => allowControlled,
            OverclockTier.Aggressive => allowAggressive,
            OverclockTier.Critical => allowCritical,
            _ => false
        };
    }
}
