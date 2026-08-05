using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseDecisionStage
{
    Travel = 0,
    Reconnaissance = 1,
    Negotiation = 2,
    Infiltration = 3,
    Camp = 4,
    Loot = 5,
    Return = 6
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseDecisionChoiceDefinition
{
    public string choiceId = "choice";
    public string label = "선택";
    [TextArea] public string description;
    public string requiredTag;
    public string transformedLabel;
    [TextArea] public string transformedDescription;
    public string directionLabel = "변화";
    [Range(0, 3)] public int severity = 1;
    public bool mayStartCombat;
    public bool mayCauseInjury;
    public bool mayMoveExpedition;
    [SerializeReference]
    public List<OffenseDecisionEffectDefinition> effects =
        new List<OffenseDecisionEffectDefinition>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class OffenseDecisionEffectDefinition
{
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseSupplyDecisionEffect : OffenseDecisionEffectDefinition
{
    public OffenseSupplyType supplyType;
    public int amount;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseGoldDecisionEffect : OffenseDecisionEffectDefinition
{
    public int amount;
    public BribeOffer bribe;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum BribeOutcomeKind
{
    Passage = 0,
    HostilityDelay = 1,
    RiskReduction = 2,
    InformationPurchase = 3
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class BribeOffer
{
    public string offerId = string.Empty;
    public string factionId = string.Empty;
    public int price;
    public BribeOutcomeKind outcome;
    [Range(0, 100)] public int acceptancePercent = 100;
    public string acceptedResult = string.Empty;
    public string rejectedResult = string.Empty;

    public bool IsValid => !string.IsNullOrWhiteSpace(offerId)
        && price > 0;

    public bool IsAccepted(int deterministicRoll)
    {
        int roll = (int)((uint)deterministicRoll % 100u);
        return roll < Mathf.Clamp(acceptancePercent, 0, 100);
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseStressDecisionEffect : OffenseDecisionEffectDefinition
{
    public float amount;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseExposureDecisionEffect : OffenseDecisionEffectDefinition
{
    public float amount;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseInjuryDecisionEffect : OffenseDecisionEffectDefinition
{
    [Range(-0.5f, 0.5f)] public float maxHealthRatio;
    public bool nonLethal = true;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseLootDecisionEffect : OffenseDecisionEffectDefinition
{
    public StockCategory stockCategory = StockCategory.General;
    public int amount;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseReconDecisionEffect : OffenseDecisionEffectDefinition
{
    [Min(1)] public int revealCount = 1;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseTimeDecisionEffect : OffenseDecisionEffectDefinition
{
    [Min(0f)] public float elapsedHours = 1f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseEquipmentWearDecisionEffect :
    OffenseDecisionEffectDefinition
{
    [Min(0f)] public float durabilityDamage = 5f;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseForcedMoveDecisionEffect : OffenseDecisionEffectDefinition
{
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseCombatDecisionEffect : OffenseDecisionEffectDefinition
{
}
