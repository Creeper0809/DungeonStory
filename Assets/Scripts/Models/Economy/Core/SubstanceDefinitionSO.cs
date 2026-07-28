using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Economy/Substance", order = 4)]
public sealed class SubstanceDefinitionSO : DataScriptableObject
{
    public const string ResourcePath = "SO/Economy/Substances";

    [SerializeField] private string substanceId = string.Empty;
    [SerializeField] private string itemId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private SubstanceUseClass useClass;
    [Range(0f, 1f), SerializeField] private float addictionChance;
    [Range(0f, 1f), SerializeField] private float overdoseChance;
    [Min(0f), SerializeField] private float toleranceGain;
    [Min(0f), SerializeField] private float withdrawalPerHour;
    [SerializeField] private float moodEffect;
    [SerializeField] private float workSpeedEffect;
    [SerializeField] private float combatEffect;
    [Min(1f), SerializeField] private float durationSeconds = 120f;
    [SerializeField] private string requiredResearchId = string.Empty;

    public string SubstanceId => substanceId?.Trim() ?? string.Empty;
    public string ItemId => itemId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? SubstanceId : displayName.Trim();
    public SubstanceUseClass UseClass => useClass;
    public float AddictionChance => Mathf.Clamp01(addictionChance);
    public float OverdoseChance => Mathf.Clamp01(overdoseChance);
    public float ToleranceGain => Mathf.Max(0f, toleranceGain);
    public float WithdrawalPerHour => Mathf.Max(0f, withdrawalPerHour);
    public float MoodEffect => moodEffect;
    public float WorkSpeedEffect => workSpeedEffect;
    public float CombatEffect => combatEffect;
    public float DurationSeconds => Mathf.Max(1f, durationSeconds);
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;

#if UNITY_EDITOR
    public void Configure(
        string stableId,
        string sourceItemId,
        string name,
        SubstanceUseClass classification,
        float addiction,
        float overdose,
        float tolerance,
        float withdrawal,
        Vector3 effects,
        float duration,
        string researchId)
    {
        substanceId = stableId?.Trim() ?? string.Empty;
        itemId = sourceItemId?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        useClass = classification;
        addictionChance = Mathf.Clamp01(addiction);
        overdoseChance = Mathf.Clamp01(overdose);
        toleranceGain = Mathf.Max(0f, tolerance);
        withdrawalPerHour = Mathf.Max(0f, withdrawal);
        moodEffect = effects.x;
        workSpeedEffect = effects.y;
        combatEffect = effects.z;
        durationSeconds = Mathf.Max(1f, duration);
        requiredResearchId = researchId?.Trim() ?? string.Empty;
    }
#endif
}
