using System.Collections.Generic;
using System.Linq;
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
    public IReadOnlyList<GameplayEffectBinding> Effects =>
        effects ??= new List<GameplayEffectBinding>();

    public IReadOnlyList<string> ValidateGameplayEffects()
    {
        List<string> errors = new();
        string speciesId = DefinitionId.Value;
        if (Effects.Any(value => value == null))
        {
            errors.Add(
                $"Character species '{speciesId}' contains a null gameplay effect binding.");
        }
        foreach (GameplayEffectBinding binding in Effects.Where(value => value != null))
        {
            if (!binding.IsValidFor(SourceRef, out string reason))
                errors.Add($"Character species '{speciesId}' {reason}.");
        }
        if (Effects
            .Where(value => value != null)
            .GroupBy(
                value => value.bindingId?.Trim() ?? string.Empty,
                System.StringComparer.Ordinal)
            .Any(group => group.Count() > 1))
        {
            errors.Add(
                $"Character species '{speciesId}' contains duplicate gameplay effect binding ids.");
        }
        return errors;
    }

#if UNITY_EDITOR
    public void ConfigureGameplayEffects(
        IEnumerable<GameplayEffectBinding> values)
    {
        effects = values != null
            ? new List<GameplayEffectBinding>(values)
            : new List<GameplayEffectBinding>();
    }
#endif
}
