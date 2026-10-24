using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R20InspectorDebugScenarios
{
    // Invoke only through the isolated batch-mode Inspector contract runner.
    public static void VerifyInspectorGUI()
    {
        if (!Application.isBatchMode)
        {
            throw new InvalidOperationException(
                "R20 Inspector verification requires isolated batch mode.");
        }

        CareerPositionDefinitionSO careerSource = RequireSource<
            CareerPositionDefinitionSO>(value =>
                !string.IsNullOrWhiteSpace(value.requiredFacilityTag));
        ProductionRecipeSO productionSource = RequireSource<ProductionRecipeSO>(
            value => !string.IsNullOrWhiteSpace(value.FacilityTag));
        DiseaseDefinitionSO diseaseSource = RequireSource<DiseaseDefinitionSO>(
            value => value.fieldResponseIds?.Any(id =>
                !string.IsNullOrWhiteSpace(id)) == true);
        TraitProbe traitProbe = RequireTraitProbe();
        FacilityEvolutionRecipeSO evolutionSource = RequireSource<
            FacilityEvolutionRecipeSO>(value =>
                value.allowedMutationTags?.Any(tag =>
                    !string.IsNullOrWhiteSpace(tag)) == true
                && value.identityPressureWeights?.Any(weight =>
                    !string.IsNullOrWhiteSpace(weight.key)) == true);
        ProductionRecipeSO outputCostSource = RequireSource<ProductionRecipeSO>(
            value => !string.IsNullOrWhiteSpace(
                         value.OutputCostAllocation.CapabilityId)
                     && value.Outputs.Count > 0);
        BuildingLightingSettingsSO lightingSource =
            FindSources<BuildingLightingSettingsSO>().FirstOrDefault();

        ScriptableObject[] sources = new ScriptableObject[]
            {
                careerSource,
                productionSource,
                diseaseSource,
                traitProbe.Source,
                evolutionSource,
                outputCostSource,
                lightingSource
            }
            .Where(value => value != null)
            .Distinct()
            .ToArray();
        SourceSnapshot[] snapshots = sources
            .Select(SourceSnapshot.Capture)
            .ToArray();

        CareerPositionDefinitionSO career = Clone(careerSource);
        ProductionRecipeSO production = Clone(productionSource);
        DiseaseDefinitionSO disease = Clone(diseaseSource);
        CharacterTraitSO trait = Clone(traitProbe.Source);
        FacilityEvolutionRecipeSO evolution = Clone(evolutionSource);
        ProductionRecipeSO outputCost = Clone(outputCostSource);
        BuildingLightingSettingsSO lighting = lightingSource != null
            ? Clone(lightingSource)
            : CreateTransientLightingSettings();
        ScriptableObject[] clones =
        {
            career,
            production,
            disease,
            trait,
            evolution,
            outputCost,
            lighting
        };

        ContentReferenceInspectorProbeWindow window =
            ScriptableObject.CreateInstance<
                ContentReferenceInspectorProbeWindow>();
        var drawn = new List<string>();
        try
        {
            ContentReferenceSelection.Invalidate();
            window.position = new Rect(0, 0, 820, 460);
            window.ShowUtility();

            VerifyReference(
                window,
                career,
                nameof(CareerPositionDefinitionSO.requiredFacilityTag),
                ContentReferenceKind.BuildingSemanticTag,
                "BuildingSemanticTag career requiredFacilityTag",
                drawn,
                applyAndUndo: true);
            VerifyReference(
                window,
                production,
                "facilityTag",
                ContentReferenceKind.ProductionFacilityTag,
                "ProductionFacilityTag recipe facilityTag",
                drawn);
            VerifyReference(
                window,
                disease,
                "fieldResponseIds.Array.data[0]",
                ContentReferenceKind.DiseaseResponse,
                "DiseaseResponse fieldResponseIds",
                drawn);
            VerifyReference(
                window,
                trait,
                $"identityRules.Array.data[{traitProbe.RuleIndex}]."
                + traitProbe.MemberName,
                ContentReferenceKind.IdentityRuleProtocol,
                "IdentityRuleProtocol trait rule",
                drawn);
            VerifyReference(
                window,
                evolution,
                "allowedMutationTags.Array.data[0]",
                ContentReferenceKind.FacilityMutationTag,
                "FacilityMutationTag evolution tag",
                drawn);
            VerifyReference(
                window,
                evolution,
                "identityPressureWeights.Array.data[0].key",
                ContentReferenceKind.FacilityValueKey,
                "FacilityPressure identity weight",
                drawn);
            VerifyOutputCostDrawer(window, outputCost, drawn);
            VerifySortingLayerDrawer(window, lighting, drawn);

            Require(window.DrawCount >= drawn.Count,
                $"R20 Inspector draw count {window.DrawCount} is below "
                + $"the {drawn.Count} verified GUI surfaces.");
            Debug.Log(
                "R20_INSPECTOR_GUI=PASS; draws=" + window.DrawCount
                + "; surfaces=" + string.Join(", ", drawn)
                + "; source JSON and dirty state unchanged");
        }
        finally
        {
            window.Property = null;
            window.Close();
            if (window != null)
                Object.DestroyImmediate(window);
            foreach (ScriptableObject clone in clones)
            {
                Undo.ClearUndo(clone);
                Object.DestroyImmediate(clone);
            }
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    private static void VerifyReference(
        ContentReferenceInspectorProbeWindow window,
        ScriptableObject target,
        string propertyPath,
        ContentReferenceKind requestedKind,
        string context,
        ICollection<string> drawn,
        bool applyAndUndo = false)
    {
        string baseline = EditorJsonUtility.ToJson(target);
        Undo.ClearUndo(target);
        using SerializedObject serialized = new(target);
        SerializedProperty property = RequireProperty(serialized, propertyPath);
        var declared = new ContentReferenceAttribute(requestedKind);
        ContentReferenceAttribute resolved =
            ContentReferenceSelection.ResolveReference(declared, property);
        Require(resolved != null,
            context + " did not resolve its ContentReference kind.");
        ContentReferenceSelection.Entry[] candidates =
            ContentReferenceSelection.Entries(resolved, property).ToArray();
        string current = ContentReferenceSelection.ReadId(
            property,
            resolved.Kind,
            resolved.EmptyNumericId);
        Require(current.Length > 0
                && candidates.Any(value => string.Equals(
                    value.Id,
                    current,
                    StringComparison.Ordinal)),
            context + " candidates do not contain the actual authored value '"
            + current + "'.");

        DrawProbe(window, property, context);
        drawn.Add(context);
        if (!applyAndUndo)
            return;

        ContentReferenceSelection.Entry replacement = candidates.FirstOrDefault(
            value => !string.Equals(
                value.Id,
                current,
                StringComparison.Ordinal));
        Require(!string.IsNullOrEmpty(replacement.Id),
            context + " has no alternate registered value for Apply and Undo.");
        int undoGroup = BeginUndo("R20 " + context);
        ContentReferenceSelection.WriteId(
            property,
            resolved.Kind,
            replacement.Id,
            resolved.EmptyNumericId);
        Require(EditorJsonUtility.ToJson(target) == baseline,
            context + " applied a pending selection before ApplyModifiedProperties.");
        DrawProbe(window, property, context + " pending selection");
        drawn.Add(context + " pending selection");
        Require(serialized.ApplyModifiedProperties(),
            context + " pending selection did not apply.");
        EndUndo(target, baseline, undoGroup, context);
    }

    private static void VerifyOutputCostDrawer(
        ContentReferenceInspectorProbeWindow window,
        ProductionRecipeSO target,
        ICollection<string> drawn)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty allocation = RequireProperty(
            serialized,
            "outputCostAllocation");
        allocation.isExpanded = true;
        R20ProductionOutputCostInspection inspection =
            R20ProductionOutputCostDrawer.Inspect(allocation);
        Require(inspection.IsValid && !inspection.IsEmpty,
            "R20 output-cost drawer source is not a valid authored allocation: "
            + inspection.Error);
        DrawProbe(window, allocation, "R20ProductionOutputCostDrawer");
        drawn.Add("R20ProductionOutputCostDrawer");
    }

    private static void VerifySortingLayerDrawer(
        ContentReferenceInspectorProbeWindow window,
        BuildingLightingSettingsSO target,
        ICollection<string> drawn)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty layers = RequireProperty(
            serialized,
            nameof(BuildingLightingSettingsSO.targetSortingLayers));
        Require(layers.isArray && layers.arraySize > 0,
            "Lighting settings expose no sorting-layer fixture value.");
        SerializedProperty layer = layers.GetArrayElementAtIndex(0);
        string current = layer.stringValue ?? string.Empty;
        Require(R20SortingLayerDrawer.GetRegisteredLayerNames().Contains(
                current,
                StringComparer.Ordinal),
            "Lighting settings use an unregistered sorting layer '"
            + current + "'.");
        DrawProbe(window, layer, "RegisteredSortingLayer drawer");
        drawn.Add("RegisteredSortingLayer drawer");
    }

    private static void DrawProbe(
        ContentReferenceInspectorProbeWindow window,
        SerializedProperty property,
        string context)
    {
        int previous = window.DrawCount;
        window.Property = property;
        window.SendEvent(new Event { type = EventType.Layout });
        window.SendEvent(new Event { type = EventType.Repaint });
        window.Property = null;
        if (window.Failure != null)
            throw new InvalidOperationException(context, window.Failure);
        Require(window.DrawCount > previous,
            "Inspector GUI did not execute: " + context);
    }

    private static int BeginUndo(string name)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(name);
        return group;
    }

    private static void EndUndo(
        ScriptableObject target,
        string baseline,
        int undoGroup,
        string context)
    {
        Undo.CollapseUndoOperations(undoGroup);
        Require(EditorJsonUtility.ToJson(target) != baseline,
            context + " did not change the clone before Undo.");
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Require(EditorJsonUtility.ToJson(target) == baseline,
            context + " did not restore clone JSON with one Undo.");
    }

    private static TraitProbe RequireTraitProbe()
    {
        foreach (CharacterTraitSO source in FindSources<CharacterTraitSO>())
        {
            for (int ruleIndex = 0;
                 ruleIndex < (source.identityRules?.Count ?? 0);
                 ruleIndex++)
            {
                CharacterIdentityRule rule = source.identityRules[ruleIndex];
                if (rule == null)
                    continue;
                foreach (FieldInfo field in rule.GetType().GetFields(
                             BindingFlags.Instance | BindingFlags.Public))
                {
                    ContentReferenceAttribute reference = field
                        .GetCustomAttributes(
                            typeof(ContentReferenceAttribute),
                            inherit: true)
                        .OfType<ContentReferenceAttribute>()
                        .SingleOrDefault(value =>
                            value.Kind
                                == ContentReferenceKind.IdentityRuleProtocol);
                    string value = reference != null
                        ? field.GetValue(rule) as string ?? string.Empty
                        : string.Empty;
                    if (value.Length == 0
                        || !CharacterIdentityRuleProtocols.Keys(
                                rule.GetType(),
                                field.Name)
                            .Contains(value, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    return new TraitProbe(source, ruleIndex, field.Name);
                }
            }
        }

        throw new InvalidOperationException(
            "No actual CharacterTraitSO identity protocol value was found.");
    }

    private static T RequireSource<T>(Func<T, bool> predicate)
        where T : ScriptableObject
    {
        T source = FindSources<T>().FirstOrDefault(predicate);
        return source != null
            ? source
            : throw new InvalidOperationException(
                "No actual " + typeof(T).Name
                + " asset satisfies the R20 Inspector fixture.");
    }

    private static T[] FindSources<T>() where T : ScriptableObject =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .Distinct()
            .OrderBy(value => AssetDatabase.GetAssetPath(value),
                StringComparer.Ordinal)
            .ToArray();

    private static T Clone<T>(T source) where T : ScriptableObject
    {
        T clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        return clone;
    }

    private static BuildingLightingSettingsSO CreateTransientLightingSettings()
    {
        BuildingLightingSettingsSO value = ScriptableObject.CreateInstance<
            BuildingLightingSettingsSO>();
        value.hideFlags = HideFlags.HideAndDontSave;
        value.targetSortingLayers =
            BuildingLightingSettingsSO.DefaultTargetSortingLayers.ToArray();
        return value;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string path)
    {
        SerializedProperty property = serialized.FindProperty(path);
        Require(property != null,
            "Serialized property is unavailable: " + path);
        return property;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class TraitProbe
    {
        internal TraitProbe(
            CharacterTraitSO source,
            int ruleIndex,
            string memberName)
        {
            Source = source;
            RuleIndex = ruleIndex;
            MemberName = memberName;
        }

        internal CharacterTraitSO Source { get; }
        internal int RuleIndex { get; }
        internal string MemberName { get; }
    }

    private readonly struct SourceSnapshot
    {
        private readonly ScriptableObject source;
        private readonly string json;
        private readonly bool dirty;

        private SourceSnapshot(
            ScriptableObject source,
            string json,
            bool dirty)
        {
            this.source = source;
            this.json = json;
            this.dirty = dirty;
        }

        internal static SourceSnapshot Capture(ScriptableObject source) =>
            new SourceSnapshot(
                source,
                EditorJsonUtility.ToJson(source),
                EditorUtility.IsDirty(source));

        internal void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(source) == json,
                $"R20 Inspector fixture changed source JSON for '{source.name}'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                $"R20 Inspector fixture changed dirty state for '{source.name}'.");
        }
    }
}
