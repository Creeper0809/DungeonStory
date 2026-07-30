using System;
using UnityEngine;

[Serializable]
[BuildingAbilityDisplayName("용병 고용")]
public sealed class BuildingMercenaryHiringAbility : BuildingAbility
{
    [Min(0), InspectorName("역할 가산금")]
    public int rolePremium;

    [Range(0f, 100f), InspectorName("최소 후보 만족도")]
    public float minimumCandidateSatisfaction = 65f;
}
