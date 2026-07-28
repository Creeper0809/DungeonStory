using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Economy/Craft Material", order = 3)]
public sealed class CraftMaterialDefinitionSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Economy/Materials";

    [SerializeField] private string materialId = string.Empty;
    [SerializeField] private string itemId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private CombatMaterialFamily family;
    [Min(0.01f), SerializeField] private float damageMultiplier = 1f;
    [Min(0.01f), SerializeField] private float penetrationDefenseMultiplier = 1f;
    [Min(0.01f), SerializeField] private float durabilityMultiplier = 1f;
    [Min(0.01f), SerializeField] private float weightMultiplier = 1f;
    [Min(0.01f), SerializeField] private float valueMultiplier = 1f;
    [Min(0f), SerializeField] private float insulation;
    [Min(0f), SerializeField] private float mentalResistance;
    [Min(0f), SerializeField] private float arcaneResistance;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private bool rareMaterial;
    [SerializeField] private string requiredResearchId = string.Empty;

    public string MaterialId => materialId?.Trim() ?? string.Empty;
    public string ItemId => itemId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? MaterialId : displayName.Trim();
    public CombatMaterialFamily Family => family;
    public float DamageMultiplier => Mathf.Max(0.01f, damageMultiplier);
    public float PenetrationDefenseMultiplier => Mathf.Max(0.01f, penetrationDefenseMultiplier);
    public float DurabilityMultiplier => Mathf.Max(0.01f, durabilityMultiplier);
    public float WeightMultiplier => Mathf.Max(0.01f, weightMultiplier);
    public float ValueMultiplier => Mathf.Max(0.01f, valueMultiplier);
    public float Insulation => Mathf.Max(0f, insulation);
    public float MentalResistance => Mathf.Max(0f, mentalResistance);
    public float ArcaneResistance => Mathf.Max(0f, arcaneResistance);
    public Color Tint => tint;
    public bool RareMaterial => rareMaterial;
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;

#if UNITY_EDITOR
    public void Configure(
        string stableId,
        string sourceItemId,
        string name,
        CombatMaterialFamily materialFamily,
        Vector4 combatMultipliers,
        float value,
        Vector3 specialResistances,
        Color materialTint,
        bool rare,
        string researchId)
    {
        materialId = stableId?.Trim() ?? string.Empty;
        itemId = sourceItemId?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        family = materialFamily;
        damageMultiplier = Mathf.Max(0.01f, combatMultipliers.x);
        penetrationDefenseMultiplier = Mathf.Max(0.01f, combatMultipliers.y);
        durabilityMultiplier = Mathf.Max(0.01f, combatMultipliers.z);
        weightMultiplier = Mathf.Max(0.01f, combatMultipliers.w);
        valueMultiplier = Mathf.Max(0.01f, value);
        insulation = Mathf.Max(0f, specialResistances.x);
        mentalResistance = Mathf.Max(0f, specialResistances.y);
        arcaneResistance = Mathf.Max(0f, specialResistances.z);
        tint = materialTint;
        rareMaterial = rare;
        requiredResearchId = researchId?.Trim() ?? string.Empty;
    }
#endif
}
