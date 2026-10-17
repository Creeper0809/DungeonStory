using System;
using System.Collections.Generic;
using System.Linq;

public static class CharacterAcquiredTraitEffectSourceProjection
{
    private sealed class AcquiredTraitEffectSource : IGameplayEffectSource
    {
        public AcquiredTraitEffectSource(
            string instanceId,
            IReadOnlyList<GameplayEffectBinding> effects)
        {
            SourceRef = new GameplayEffectSourceRef(
                GameplayEffectSourceKind.AcquiredTrait,
                instanceId);
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        }

        public GameplayEffectSourceRef SourceRef { get; }
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
    }

    public static IReadOnlyList<IGameplayEffectSource> Project(
        CharacterAcquiredTraitAggregateState state,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> moduleDefinitions)
    {
        if (state != null
            && !state.HasPersistentData
            && state.formatVersion
                == CharacterAcquiredTraitAggregateState.CurrentFormatVersion
            && state.instances != null
            && state.processedMilestones != null
            && state.pendingRequests != null)
        {
            return Array.Empty<IGameplayEffectSource>();
        }

        IReadOnlyList<CharacterAcquiredTraitValidationIssue> structural =
            CharacterAcquiredTraitStateValidator.ValidatePersistentState(
                state,
                ledger);
        if (structural.Count > 0)
            throw InvalidState(structural);
        CharacterAcquiredTraitModuleSO[] modules =
            (moduleDefinitions ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .ToArray();
        IReadOnlyList<CharacterAcquiredTraitValidationIssue> validation =
            CharacterAcquiredTraitStateValidator.ValidateWithDefinitions(
                state,
                ledger,
                settings,
                modules);
        if (validation.Count > 0)
            throw InvalidState(validation);

        Dictionary<string, CharacterAcquiredTraitModuleSO> modulesById = modules
            .Where(value => value != null)
            .ToDictionary(value => value.ModuleId, StringComparer.Ordinal);
        return state.CaptureActiveInstances()
            .Select(instance => (IGameplayEffectSource)new AcquiredTraitEffectSource(
                instance.EffectSourceId,
                ResolveEffects(instance, settings, modulesById)))
            .OrderBy(source => source.SourceRef.SourceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<GameplayEffectBinding> ResolveEffects(
        CharacterAcquiredTraitInstanceState instance,
        CharacterAcquiredTraitSettingsSO settings,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById)
    {
        IEnumerable<GameplayEffectBinding> authoredSource;
        if (instance.formulaVersion >= 2)
        {
            IEnumerable<GameplayEffectBinding> benefits =
                (instance.benefitModuleIds ?? new List<string>())
                .SelectMany(moduleId => modulesById[moduleId].Effects.Where(binding =>
                    binding != null && !modulesById[moduleId].DrawbackBindingIds.Contains(
                        binding.bindingId, StringComparer.Ordinal)));
            Dictionary<string, CharacterAcquiredTraitDrawbackCapabilityDefinition> drawbacks =
                (settings?.DrawbackCapabilities
                    ?? Array.Empty<CharacterAcquiredTraitDrawbackCapabilityDefinition>())
                .Where(value => value != null)
                .ToDictionary(value => value.DrawbackId, StringComparer.Ordinal);
            IEnumerable<GameplayEffectBinding> costs =
                (instance.drawbackCapabilityIds ?? new List<string>())
                .SelectMany(drawbackId => drawbacks[drawbackId].Effects);
            authoredSource = benefits.Concat(costs);
        }
        else
        {
            authoredSource = (instance.moduleIds ?? new List<string>())
                .SelectMany(moduleId => modulesById[moduleId].Effects);
        }
        GameplayEffectBinding[] authored = authoredSource
            .Where(binding => binding != null)
            .OrderBy(binding => binding.bindingId, StringComparer.Ordinal)
            .ToArray();
        if (instance.formulaVersion == 0)
            return authored;

        Dictionary<string, CharacterAcquiredTraitEffectOverride> overrides =
            (instance.effectOverrides ?? new List<CharacterAcquiredTraitEffectOverride>())
            .ToDictionary(value => value.bindingId, value => value, StringComparer.Ordinal);
        return authored.Select(binding =>
        {
            if (!overrides.TryGetValue(binding.bindingId, out CharacterAcquiredTraitEffectOverride value))
                throw new InvalidOperationException(
                    $"Formula acquired trait '{instance.instanceId}' omits override for '{binding.bindingId}'.");
            return new GameplayEffectBinding
            {
                bindingId = binding.bindingId,
                definition = binding.definition,
                condition = binding.condition,
                value = value.value
            };
        }).ToArray();
    }

    private static InvalidOperationException InvalidState(
        IEnumerable<CharacterAcquiredTraitValidationIssue> issues) =>
        new("Acquired-trait effect projection rejected invalid state: "
            + string.Join(" | ", issues.Select(value => value.ToString())));
}
