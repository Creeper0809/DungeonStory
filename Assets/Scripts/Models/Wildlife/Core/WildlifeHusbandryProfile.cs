using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeHusbandryProductDefinition
{
    [SerializeField] private string itemId = string.Empty;
    [Min(1), SerializeField] private int amount = 1;
    [Min(0.1f), SerializeField] private float intervalDays = 1f;
    [SerializeField] private bool femaleOnly;
    [SerializeField] private bool adultOnly = true;

    public WildlifeHusbandryProductDefinition()
    {
    }

    public WildlifeHusbandryProductDefinition(
        string itemId,
        int amount,
        float intervalDays,
        bool femaleOnly = false,
        bool adultOnly = true)
    {
        this.itemId = itemId?.Trim() ?? string.Empty;
        this.amount = Mathf.Max(1, amount);
        this.intervalDays = Mathf.Max(0.1f, intervalDays);
        this.femaleOnly = femaleOnly;
        this.adultOnly = adultOnly;
    }

    public string ItemId => itemId?.Trim() ?? string.Empty;
    public int Amount => Mathf.Max(1, amount);
    public float IntervalDays => Mathf.Max(0.1f, intervalDays);
    public bool FemaleOnly => femaleOnly;
    public bool AdultOnly => adultOnly;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeHusbandryProfile
{
    public WildlifeHusbandryProfile(
        bool domesticable,
        float tamingDifficulty,
        float adultAgeDays,
        float maximumAgeDays,
        float gestationDays,
        bool laysEggs,
        float bodySize,
        float manureIntervalDays,
        IEnumerable<WildlifeHusbandryProductDefinition> products = null)
    {
        Domesticable = domesticable;
        TamingDifficulty = Mathf.Clamp01(tamingDifficulty);
        AdultAgeDays = Mathf.Max(0.25f, adultAgeDays);
        MaximumAgeDays = Mathf.Max(AdultAgeDays + 1f, maximumAgeDays);
        GestationDays = Mathf.Max(0.25f, gestationDays);
        LaysEggs = laysEggs;
        BodySize = Mathf.Max(0.1f, bodySize);
        ManureIntervalDays = Mathf.Max(0.25f, manureIntervalDays);
        Products = (products ?? Array.Empty<WildlifeHusbandryProductDefinition>())
            .Where(product => product != null
                && !string.IsNullOrWhiteSpace(product.ItemId))
            .ToArray();
    }

    public bool Domesticable { get; }
    public float TamingDifficulty { get; }
    public float AdultAgeDays { get; }
    public float MaximumAgeDays { get; }
    public float GestationDays { get; }
    public bool LaysEggs { get; }
    public float BodySize { get; }
    public float ManureIntervalDays { get; }
    public IReadOnlyList<WildlifeHusbandryProductDefinition> Products { get; }

}
