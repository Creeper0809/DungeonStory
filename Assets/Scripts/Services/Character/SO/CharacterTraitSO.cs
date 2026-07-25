using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Character/Trait", order = 0)]
public class CharacterTraitSO : DataScriptableObject
{
    public string traitName;
    [TextArea] public string description;
    public CharacterStatBlock statBonus = new CharacterStatBlock();
    public CharacterModelModifiers modifiers = new CharacterModelModifiers();
    public CharacterCombatAbilityCollection combatAbilities = new CharacterCombatAbilityCollection();
}
