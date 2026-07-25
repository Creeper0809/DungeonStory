using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Combat/Armor", order = 11)]
public sealed class CombatArmorSO : CombatEquipmentDefinitionSO
{
    [SerializeField] private CombatArmorLayer layer = CombatArmorLayer.Clothing;
    [SerializeField] private string collisionTag = string.Empty;
    [SerializeField] private List<CombatArmorPartValue> bodyPartDefense = new List<CombatArmorPartValue>();

    public override CombatEquipmentKind Kind => CombatEquipmentKind.Armor;
    public CombatArmorLayer Layer => layer;
    public string CollisionTag => collisionTag?.Trim() ?? string.Empty;
    public IReadOnlyList<CombatArmorPartValue> BodyPartDefense =>
        bodyPartDefense ??= new List<CombatArmorPartValue>();
}
