using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterDefinitionBoundaryDebugScenarios
{
    public static void RunForBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Character definition verification requires isolated batch execution.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("Character definition evidence directory is required.");
        var rows = new List<string> { "case\tresult" };
        try
        {
            Run("authored-definitions-factory-editor-json-clone-and-failure-boundaries", VerifyAuthoredDefinitions, rows);
            Run("populated-skill-json-and-detached-nested-clone", VerifyPopulatedSkill, rows);
            Run("personality-null-receiver-before-action-and-semantic-priority", VerifyPersonality, rows);
            Run("existing-action-descriptor-production-policy", () => Require(
                CharacterAiActionDescriptorDebugScenarios.RunAll(true), "Action descriptor regression failed."), rows);
            Run("existing-character-model-profile-and-actor-connection", () => Require(
                CharacterModelDebugScenarios.RunAll(true), "Character model regression failed."), rows);
            Run("existing-work-priority-migration-ui-and-runtime-consumers", () => Require(
                WorkPriorityDebugScenarios.RunAll(true), "Work priority regression failed."), rows);
        }
        finally
        {
            Directory.CreateDirectory(output);
            using FileStream file = new(Path.Combine(output, "character-definition-boundary.tsv"), FileMode.CreateNew, FileAccess.Write);
            using StreamWriter writer = new(file);
            foreach (string row in rows) writer.WriteLine(row);
        }
    }

    private static void Run(string name, Action action, List<string> rows)
    {
        try { action(); rows.Add(name + "\tPASS"); }
        catch (Exception exception)
        {
            rows.Add(name + "\tFAIL:" + exception.GetType().Name + ":" + exception.Message.Replace('\n', ' '));
            throw;
        }
    }

    private static void VerifyAuthoredDefinitions()
    {
        IGameContentCatalog catalog = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        var factory = new CharacterRuntimeProfileFactory(catalog);
        CharacterSO[] definitions = catalog.GetAll<CharacterSO>()
            .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal).ToArray();
        Require(definitions.Length > 0, "No authored character definitions were loaded.");
        int skills = 0;
        foreach (CharacterSO definition in definitions)
        {
            string before = JsonUtility.ToJson(definition);
            CharacterSpawnRequest request = CharacterSpawnRequest.FromAuthoring(definition);
            CharacterRuntimeProfile profile = factory.Create(request);
            CharacterRuntimeProfile editor = CharacterRuntimeProfileFactory.CreateEditorSnapshot(definition);
            Require(profile.CharacterArchetypeId.Equals(editor.CharacterArchetypeId)
                && profile.PhenotypeSpeciesId.Equals(editor.PhenotypeSpeciesId)
                && profile.VisualVariantId == editor.VisualVariantId
                && profile.ExpressedTraitIds.SequenceEqual(editor.ExpressedTraitIds)
                && profile.LatentTraitIds.SequenceEqual(editor.LatentTraitIds)
                && JsonUtility.ToJson(profile.GetNeedProfile()) == JsonUtility.ToJson(editor.GetNeedProfile())
                && JsonUtility.ToJson(profile.GetEnvironmentProfile()) == JsonUtility.ToJson(editor.GetEnvironmentProfile()),
                "Runtime/editor profile construction disagreed: " + definition.DefinitionId.Value);
            CharacterSO restored = ScriptableObject.CreateInstance<CharacterSO>();
            try
            {
                JsonUtility.FromJsonOverwrite(before, restored);
                Require(JsonUtility.ToJson(restored) == before,
                    "Authored character JSON changed through restoration: " + definition.DefinitionId.Value);
                CharacterRuntimeProfile restoredProfile = CharacterRuntimeProfileFactory.CreateEditorSnapshot(restored);
                Require(restoredProfile.ExpressedTraitIds.SequenceEqual(profile.ExpressedTraitIds)
                    && restoredProfile.CharacterArchetypeId.Equals(profile.CharacterArchetypeId),
                    "Restored definition did not construct its original profile.");
                foreach (CharacterSkillInstance skill in definition.OwnerFixedSkills)
                {
                    Require(skill != null, "Authored fixed skill is null.");
                    CharacterSkillInstance clone = skill.Clone();
                    Require(!ReferenceEquals(skill, clone), "Fixed-skill clone retained the source instance.");
                    string originalId = skill.id;
                    clone.id = "qa-detached-skill";
                    Require(skill.id == originalId, "Fixed-skill clone changed the authored definition.");
                    if (skill.modules is { Count: > 0 })
                    {
                        Require(!ReferenceEquals(skill.modules, clone.modules)
                            && !ReferenceEquals(skill.modules[0], clone.modules[0]), "Fixed-skill modules were not detached.");
                    }
                    skills++;
                }
            }
            finally { Object.DestroyImmediate(restored); }
            Require(JsonUtility.ToJson(definition) == before, "Profile/clone verification changed an authored asset.");
        }
        CharacterSO first = definitions[0];
        RequireThrows<ArgumentNullException>(() => factory.Create(null), "request");
        RequireThrows<ArgumentNullException>(() => CharacterRuntimeProfileFactory.CreateEditorSnapshot(null), "archetype");
        RequireThrows<KeyNotFoundException>(() => factory.Create(new CharacterSpawnRequest(
            new CharacterArchetypeId("qa:missing-character"), first.species.DefinitionId,
            first.VisualVariantId, ReproductiveRole.None, Array.Empty<CharacterTraitId>())));
        RequireThrows<KeyNotFoundException>(() => factory.Create(new CharacterSpawnRequest(
            first.DefinitionId, new CharacterSpeciesId("qa:missing-species"),
            first.VisualVariantId, ReproductiveRole.None, Array.Empty<CharacterTraitId>())));
        Debug.Log($"CHARACTER_DEFINITION_AUTHORED=PASS; definitions={definitions.Length}; fixedSkills={skills}; factory/editor/JSON/clone/failure");
    }

    private static void VerifyPopulatedSkill()
    {
        var skill = new CharacterSkillInstance
        {
            id = "qa:definition-skill", displayName = "Definition boundary", formulaVersion = 1,
            modules = new List<CharacterSkillModuleSelection>
            {
                new() { moduleId = "qa:module", variantId = "qa:variant" }
            },
            drawbackEffect = new CharacterSkillDrawbackEffectEnvelope
            {
                drawbackModuleId = "qa:drawback", effectId = "qa:effect", targetId = "qa:target", value = .5f
            },
            evidenceIds = new List<string> { "qa:evidence" },
            formulaCapabilities = new List<CharacterSkillFormulaCapabilityEnvelope>
            {
                new()
                {
                    capabilityId = "qa:capability", formatterId = "qa:formatter", applicatorId = "qa:applicator",
                    parameters = new List<CharacterSkillFormulaParameter> { new() { parameterId = "qa:parameter", units = 17 } }
                }
            }
        };
        string before = JsonUtility.ToJson(skill);
        CharacterSkillInstance restored = JsonUtility.FromJson<CharacterSkillInstance>(before);
        Require(JsonUtility.ToJson(restored) == before, "Populated skill JSON restoration changed serialized fields.");
        CharacterSkillInstance clone = restored.Clone();
        Require(JsonUtility.ToJson(clone) == before, "Populated skill clone lost serialized fields.");
        clone.modules[0].variantId = "qa:changed";
        clone.drawbackEffect.value = 9f;
        clone.evidenceIds[0] = "qa:changed";
        clone.formulaCapabilities[0].parameters[0].units = 99;
        Require(JsonUtility.ToJson(restored) == before && JsonUtility.ToJson(skill) == before,
            "Nested skill clone mutation changed the restored or original definition.");
    }

    private static void VerifyPersonality()
    {
        DescriptorProbe action = ScriptableObject.CreateInstance<DescriptorProbe>();
        try
        {
            CharacterAiPersonality missing = null;
            action.Reads = 0;
            RequireThrows<NullReferenceException>(() => missing.GetActionMultiplier(action));
            Require(action.Reads == 0, "Null personality evaluated the action before failing.");
            RequireThrows<NullReferenceException>(() => missing.GetActionMultiplier(null));
            var personality = new CharacterAiPersonality { diligence = 9f, curiosity = -2f };
            action.Value = new CharacterAiActionDescriptor(CharacterAiBranch.Work, "probe",
                CharacterAiActionTags.Work, CharacterAiActionTags.Curiosity);
            Require(Mathf.Approximately(personality.GetActionMultiplier(action), 2f), "Work tag priority/clamp changed.");
            action.Value = new CharacterAiActionDescriptor(CharacterAiBranch.Work, "probe", CharacterAiActionTags.Curiosity);
            Require(Mathf.Approximately(personality.GetActionMultiplier(action), .25f), "Curiosity clamp changed.");
            Require(Mathf.Approximately(personality.GetActionMultiplier(null), 1f), "Null action behavior changed.");
        }
        finally { Object.DestroyImmediate(action); }
    }

    private static void RequireThrows<T>(Action action, string parameter = null) where T : Exception
    {
        try { action(); }
        catch (Exception exception)
        {
            Require(exception.GetType() == typeof(T), "Unexpected failure type: " + exception.GetType().Name);
            if (parameter != null) Require(exception is ArgumentException argument && argument.ParamName == parameter,
                "Argument failure identity changed.");
            return;
        }
        throw new InvalidOperationException("Expected failure did not occur: " + typeof(T).Name);
    }

    private static void Require(bool valid, string failure)
    {
        if (!valid) throw new InvalidOperationException(failure);
    }

    private sealed class DescriptorProbe : AIActionSet
    {
        public int Reads;
        public CharacterAiActionDescriptor Value;
        public override CharacterAiActionDescriptor Descriptor { get { Reads++; return Value; } }
    }
}
