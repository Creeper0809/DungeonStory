using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class OffenseSiteRewardDefinition
{
    [SerializeReference] private OffenseRewardGrantSpec grantSpec;
    [SerializeField] private string displayLabel;
    [SerializeField, Min(0)] private int baseAmount;
    [SerializeField, Min(0)] private int amountPerStrength;

    public OffenseSiteRewardDefinition(
        string label,
        int baseAmount,
        int amountPerStrength,
        OffenseRewardGrantSpec grantSpec)
    {
        displayLabel = label ?? string.Empty;
        this.baseAmount = Mathf.Max(0, baseAmount);
        this.amountPerStrength = Mathf.Max(0, amountPerStrength);
        this.grantSpec = grantSpec
            ?? throw new ArgumentNullException(nameof(grantSpec));
    }

    public OffenseRewardGrantSpec GrantSpec => grantSpec;
    public bool IsConfigured =>
        grantSpec != null
        && !string.IsNullOrWhiteSpace(grantSpec.RewardTypeId);

    public OffenseRewardPreview CreatePreview(int siteStrength)
    {
        int amount = Mathf.Max(
            0,
            baseAmount + Mathf.Max(0, siteStrength - 1) * amountPerStrength);
        return new OffenseRewardPreview(displayLabel, amount, grantSpec);
    }
}

[CreateAssetMenu(
    fileName = "OffenseSiteArchetype",
    menuName = "DungeonStory/Offense/Site Archetype")]
public sealed class OffenseSiteArchetypeSO : DataScriptableObject
{
    public string siteTypeId = "site";
    public string displayName = "거점";
    [TextArea] public string description;
    public string factionId = "human";
    public StrategicPressureAxis pressureAxis;
    [Min(0f)] public float pressureAmount = 15f;
    [Min(1)] public int minimumStrength = 1;
    [Min(1)] public int maximumStrength = 5;
    [Min(1)] public int minimumLifetimeDays = 2;
    [Min(1)] public int maximumLifetimeDays = 6;
    public bool hiddenUntilDiscovered = true;
    public bool canMove;
    public bool dynamicSpawnEligible = true;
    public List<OffenseSiteRewardDefinition> rewards = new();
}
