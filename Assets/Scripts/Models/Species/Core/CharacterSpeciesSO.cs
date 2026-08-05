using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(menuName = "DungeonStory/Character/Species", order = 0)]
public class CharacterSpeciesSO : CharacterSpeciesDefinitionSO
{
    public CharacterStatBlock statBonus = new();
    public CharacterModelModifiers modifiers = new();
    public CharacterCombatAbilityCollection combatAbilities = new();
}
