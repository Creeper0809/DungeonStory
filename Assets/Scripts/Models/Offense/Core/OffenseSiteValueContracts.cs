using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum StrategicPressureAxis
{
    None = 0,
    Logistics = 1,
    Armament = 2,
    Manpower = 3,
    Intelligence = 4
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class OffenseRewardGrantSpec
{
    public abstract string RewardTypeId { get; }
    public abstract OffenseRewardCategory Category { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseRewardPreview
{
    [SerializeReference] private OffenseRewardGrantSpec grantSpec;
    [SerializeField] private string displayLabel;
    [SerializeField, Min(0)] private int configuredAmount;

    public OffenseRewardPreview(
        string label,
        int amount,
        OffenseRewardGrantSpec grantSpec)
    {
        displayLabel = label ?? string.Empty;
        configuredAmount = Mathf.Max(0, amount);
        this.grantSpec = grantSpec ?? throw new ArgumentNullException(nameof(grantSpec));
    }

    public OffenseRewardGrantSpec GrantSpec => grantSpec;
    public OffenseRewardCategory category => grantSpec?.Category ?? OffenseRewardCategory.Money;
    public string label => displayLabel;
    public int amount => Mathf.Max(0, configuredAmount);
    public bool IsConfigured => grantSpec != null && !string.IsNullOrWhiteSpace(grantSpec.RewardTypeId);

    public string ToSummaryText()
    {
        string name = string.IsNullOrWhiteSpace(label) ? category.ToString() : label;
        return amount > 0 ? $"{name} x{amount}" : name;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
