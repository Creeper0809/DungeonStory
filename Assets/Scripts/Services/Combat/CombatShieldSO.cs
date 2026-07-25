using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Combat/Shield", order = 12)]
public sealed class CombatShieldSO : CombatEquipmentDefinitionSO
{
    [Range(0f, 1f), SerializeField] private float frontalBlockChance = 0.25f;
    [Min(0f), SerializeField] private float slashDefense = 10f;
    [Min(0f), SerializeField] private float pierceDefense = 8f;
    [Min(0f), SerializeField] private float bluntDefense = 5f;

    public override CombatEquipmentKind Kind => CombatEquipmentKind.Shield;
    public float FrontalBlockChance => Mathf.Clamp01(frontalBlockChance);
    public float SlashDefense => Mathf.Max(0f, slashDefense);
    public float PierceDefense => Mathf.Max(0f, pierceDefense);
    public float BluntDefense => Mathf.Max(0f, bluntDefense);
}
