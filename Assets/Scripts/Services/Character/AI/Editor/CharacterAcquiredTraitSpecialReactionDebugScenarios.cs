#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CharacterAcquiredTraitSpecialReactionDebugScenarios
{
    [MenuItem("DungeonStory/Tests/Character/Run Acquired Trait Special Reaction Scenarios")]
    public static void RunAll()
    {
        V25AcquiredTraitContentAssetBuilder.Build();
        CharacterAcquiredTraitFormulaCatalogAssetBuilder.Build();

        CharacterAcquiredTraitModuleSO[] modules = AssetDatabase.FindAssets(
                "t:CharacterAcquiredTraitModuleSO",
                new[] { V25AcquiredTraitContentAssetBuilder.ModuleRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitModuleSO>)
            .Where(value => value != null)
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        Require(modules.Length == 8, "The approved catalog must retain eight modules.");
        Require(modules.Sum(value => value.SpecialReactions.Count) == 5,
            "The approved catalog must expose exactly five bounded special reactions.");
        Require(modules.SelectMany(value => value.SpecialReactions)
                .Select(value => value.ReactionId)
                .Distinct(StringComparer.Ordinal).Count() == 5,
            "Special reaction IDs must be globally distinct.");
        Require(modules.SelectMany(value => value.ValidateDefinition()).Count() == 0,
            "Authored special reaction definitions must validate.");

        CharacterAcquiredTraitModuleSO workModule = modules.Single(value =>
            string.Equals(value.ModuleId,
                "acquired-trait:module:steady-hands", StringComparison.Ordinal));
        CharacterAcquiredTraitSpecialReactionDefinition reaction =
            workModule.SpecialReactions.Single();
        CharacterAcquiredTraitInstanceState instance = new()
        {
            formulaVersion = CharacterAcquiredTraitFormulaGeneration
                .ModuleSelectionFormulaVersion,
            formulaCapabilities = new List<CharacterAcquiredTraitFormulaCapabilityEnvelope>
            {
                new()
                {
                    capabilityId = workModule.CapabilityId,
                    formatterId = workModule.CapabilityId,
                    applicatorId = workModule.CapabilityId,
                    parameters = new List<CharacterAcquiredTraitFormulaParameter>
                    {
                        new()
                        {
                            parameterId = NarrativeFormulaParameterIds.Magnitude,
                            units = workModule.RequireFormulaDescriptor()
                                .RequireRange(NarrativeFormulaParameterIds.Magnitude)
                                .MaximumUnits
                        }
                    }
                }
            }
        };
        float scale = CharacterAcquiredTraitSpecialReactionMath.ResolvePotencyScale(
            instance, workModule);
        float value = CharacterAcquiredTraitSpecialReactionMath.ResolveActionValue(
            reaction, scale);
        Require(scale > 1f && value > reaction.BaseValue,
            "Formula potency must scale the special action value upward.");
        string description = CharacterAcquiredTraitSpecialReactionMath
            .FormatMechanicalDescription(reaction, value);
        Require(description.Contains("경험치", StringComparison.Ordinal)
            && description.Contains("하루", StringComparison.Ordinal)
            && !description.Contains("턴", StringComparison.Ordinal),
            "Work reactions must describe day-based limits without combat turns.");

        CharacterAcquiredTraitReactionState counter = new()
        {
            reactionId = reaction.ReactionId,
            lastTriggerAbsoluteDay = 4,
            triggersOnLastDay = 1,
            totalTriggerCount = 1
        };
        Require(!CharacterAcquiredTraitSpecialReactionMath.CanTrigger(
                counter, reaction, 4),
            "Daily limits must reject a duplicate same-day trigger.");
        Require(CharacterAcquiredTraitSpecialReactionMath.CanTrigger(
                counter, reaction, 5),
            "A one-day cooldown must reopen on the next absolute day.");

        CharacterAcquiredTraitInstanceState cloned = new CharacterAcquiredTraitInstanceState
        {
            reactionStates = new List<CharacterAcquiredTraitReactionState> { counter }
        }.Clone();
        Require(cloned.reactionStates.Count == 1
            && cloned.reactionStates[0].lastTriggerAbsoluteDay == 4
            && !ReferenceEquals(cloned.reactionStates[0], counter),
            "Reaction counters must survive save-state cloning without aliasing.");

        CharacterAcquiredTraitSettingsSO settings = AssetDatabase
            .LoadAssetAtPath<CharacterAcquiredTraitSettingsSO>(
                V25AcquiredTraitContentAssetBuilder.SettingsPath);
        CharacterAcquiredTraitFormulaCatalogAssetBuilder.Validate(settings, modules);
        Debug.Log("ACQUIRED_TRAIT_SPECIAL_REACTIONS=PASS; modules=8; reactions=5; "
            + "dispatch=generic; cooldown=day-based; saveClone=pass; formulaScale=pass");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
