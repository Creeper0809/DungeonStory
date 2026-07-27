using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Facility Shop/Blueprint", order = 0)]
public class FacilityBlueprintSO : DataScriptableObject
{
    public string blueprintName;
    [TextArea] public string description;
    public FacilityShopRarity rarity = FacilityShopRarity.Common;
    [Min(0)] public int defaultCost = 120;
    [Min(1f)] public float researchWorkRequired = 20f;
    [Tooltip("이 물리 설계도가 활성화하는 연구 프로젝트의 안정 ID")]
    public string targetResearchProjectId = string.Empty;
    public BlueprintUnlockCollection unlocks = new BlueprintUnlockCollection();

    public string DisplayName => string.IsNullOrWhiteSpace(blueprintName) ? name : blueprintName;
    public string TargetResearchProjectId => targetResearchProjectId?.Trim() ?? string.Empty;
    public string PhysicalItemId => $"research-blueprint:{id}";
    public System.Collections.Generic.IReadOnlyList<BlueprintUnlock> Unlocks =>
        (unlocks ??= new BlueprintUnlockCollection()).Items;
}
