using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(menuName = "DungeonStory/Character/Species", order = 0)]
public class CharacterSpeciesSO : CharacterSpeciesDefinitionSO, IGameplayEffectSource
{
    public CharacterModelModifiers modifiers = new();
    public CharacterCombatAbilityCollection combatAbilities = new();
    [SerializeField] private System.Collections.Generic.List<GameplayEffectBinding> effects = new();

    public GameplayEffectSourceRef SourceRef =>
        new(GameplayEffectSourceKind.Species, DefinitionId.Value);
    public System.Collections.Generic.IReadOnlyList<GameplayEffectBinding> Effects =>
        effects ??= new System.Collections.Generic.List<GameplayEffectBinding>();

#if UNITY_EDITOR
    public void ConfigureGameplayEffects(
        System.Collections.Generic.IEnumerable<GameplayEffectBinding> values)
    {
        effects = values != null
            ? new System.Collections.Generic.List<GameplayEffectBinding>(values)
            : new System.Collections.Generic.List<GameplayEffectBinding>();
    }
#endif
}
