using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Projects frozen, non-cooldown skill burdens through the shared gameplay-effect
/// pipeline. A burden remains active only while its owning skill is committed to
/// the character; removing the skill removes the source without mutating any SO.
/// </summary>
public static class CharacterSkillDrawbackEffectSourceProjection
{
    private sealed class SkillDrawbackEffectSource : IGameplayEffectSource
    {
        public SkillDrawbackEffectSource(
            string skillId,
            GameplayEffectBinding effect)
        {
            SourceRef = new GameplayEffectSourceRef(
                GameplayEffectSourceKind.Status,
                "character-skill-burden:" + skillId);
            Effects = new[] { effect };
        }

        public GameplayEffectSourceRef SourceRef { get; }
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
    }

    public static IReadOnlyList<IGameplayEffectSource> Project(
        CharacterProgression progression)
    {
        if (progression == null)
            return Array.Empty<IGameplayEffectSource>();
        CharacterSkillSystemSettingsSO settings = progression.SkillSettings;
        if (settings == null)
            return Array.Empty<IGameplayEffectSource>();

        CharacterSkillInstance[] skills = progression.ActiveSkills
            .Concat(progression.PassiveSkills)
            .Concat(progression.OwnerFixedSkills)
            .Concat(progression.Ultimate == null
                ? Array.Empty<CharacterSkillInstance>()
                : new[] { progression.Ultimate })
            .Where(value => value?.drawbackEffect != null
                && !CharacterSkillDrawbackEffectEnvelope.IsExactSerializedAbsence(
                    value.drawbackEffect))
            .GroupBy(value => value.id, StringComparer.Ordinal)
            .Select(group => group.Single())
            .OrderBy(value => value.id, StringComparer.Ordinal)
            .ToArray();

        List<IGameplayEffectSource> sources = new();
        foreach (CharacterSkillInstance skill in skills)
        {
            if (skill.formulaVersion <
                CharacterSkillFormulaGeneration.RuntimeStatDrawbackFormulaVersion)
                throw new InvalidOperationException(
                    $"Legacy skill '{skill.id}' cannot carry a runtime stat drawback.");
            if (!skill.IsReady)
                throw new InvalidOperationException(
                    $"Unresolved skill '{skill.id}' cannot project a runtime stat drawback.");
            CharacterSkillFormulaGeneration.ValidateRestoredFormulaSkill(
                skill,
                settings);
            CharacterSkillDrawbackEffectEnvelope frozen = skill.drawbackEffect;
            CharacterSkillDrawbackCapabilityDefinition authored =
                settings.FindDrawback(frozen.drawbackModuleId)
                ?? throw new InvalidOperationException(
                    $"Skill '{skill.id}' references an unknown drawback effect.");
            GameplayEffectBinding binding = new GameplayEffectBinding
            {
                bindingId = "skill-burden:" + skill.id + ":" + frozen.effectId,
                definition = authored.effectDefinition,
                value = frozen.value
            };
            GameplayEffectSourceRef sourceRef = new(
                GameplayEffectSourceKind.Status,
                "character-skill-burden:" + skill.id);
            if (!binding.IsValidFor(sourceRef, out string reason))
                throw new InvalidOperationException(
                    $"Skill '{skill.id}' burden cannot enter the gameplay-effect runtime: {reason}.");
            sources.Add(new SkillDrawbackEffectSource(skill.id, binding));
        }
        return sources;
    }
}
