using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ContentReferenceSelectionDebugScenarios
{
    private sealed class AssetSnapshot
    {
        public Object Asset;
        public string Json;
        public bool Dirty;
    }

    public static void RunAll()
    {
        var temporaryObjects = new List<Object>();
        var sourceSnapshots = new List<AssetSnapshot>();
        try
        {
            VerifyCatalogEntries();
            VerifyAnatomyNodeEntries();
            VerifySelectionsAndPersistence(temporaryObjects, sourceSnapshots);
            VerifySemanticKinds(temporaryObjects, sourceSnapshots);
            VerifySourceAssetsUnchanged(sourceSnapshots);
            Debug.Log("CONTENT_REFERENCE_AUTHORING=PASS");
        }
        finally
        {
            for (int index = temporaryObjects.Count - 1; index >= 0; index--)
            {
                if (temporaryObjects[index] != null)
                    Object.DestroyImmediate(temporaryObjects[index]);
            }
        }
    }

    private static void VerifyCatalogEntries()
    {
        foreach (ContentReferenceKind kind in
                 (ContentReferenceKind[])Enum.GetValues(typeof(ContentReferenceKind)))
        {
            if (kind is ContentReferenceKind.EffectTarget
                or ContentReferenceKind.WeightedTarget)
            {
                continue;
            }

            IReadOnlyList<ContentReferenceSelection.Entry> entries =
                ContentReferenceSelection.Entries(kind);
            Require(entries.Count > 0, $"{kind} has no catalog entries.");
            Type expectedType = ContentReferenceSelection.AssetType(kind);
            foreach (ContentReferenceSelection.Entry entry in entries)
            {
                Require(entry.Asset != null, $"{kind} contains a null asset.");
                Require(
                    AssetDatabase.Contains(entry.Asset),
                    $"{kind} contains an unregistered asset '{entry.Asset.name}'.");
                Require(
                    expectedType.IsInstanceOfType(entry.Asset),
                    $"{kind} contains the wrong asset type '{entry.Asset.GetType().Name}'.");
                Require(
                    !string.IsNullOrWhiteSpace(entry.Id)
                    && string.Equals(entry.Id, entry.Id.Trim(), StringComparison.Ordinal),
                    $"{kind} contains a non-canonical ID on '{entry.Asset.name}'.");
            }

            int distinctPairs = entries
                .Select(entry => $"{entry.Asset.GetInstanceID()}\n{entry.Id}")
                .Distinct(StringComparer.Ordinal)
                .Count();
            Require(
                distinctPairs == entries.Count,
                $"{kind} contains duplicate asset/ID entries.");
            if (kind != ContentReferenceKind.AnatomyNode)
            {
                Require(
                    entries.Select(entry => entry.Id)
                        .Distinct(StringComparer.Ordinal).Count() == entries.Count,
                    $"{kind} contains duplicate IDs.");
                Require(
                    entries.Select(entry => entry.Asset)
                        .Distinct().Count() == entries.Count,
                    $"{kind} contains more than one ID for an asset.");
            }
        }
    }

    private static void VerifyAnatomyNodeEntries()
    {
        AnatomyProfileSO[] profiles = ContentReferenceSelection
            .Entries(ContentReferenceKind.AnatomyProfile)
            .Select(entry => entry.Asset)
            .OfType<AnatomyProfileSO>()
            .ToArray();
        IReadOnlyList<ContentReferenceSelection.Entry> nodeEntries =
            ContentReferenceSelection.Entries(ContentReferenceKind.AnatomyNode);
        foreach (AnatomyProfileSO profile in profiles)
        {
            string[] expected = profile.Nodes.Select(node =>
            {
                Require(node != null, $"Anatomy profile '{profile.name}' contains a null node.");
                return node.NodeId;
            }).ToArray();
            string[] actual = nodeEntries
                .Where(entry => entry.Asset == profile)
                .Select(entry => entry.Id)
                .ToArray();
            Require(
                actual.SequenceEqual(expected, StringComparer.Ordinal),
                $"Anatomy node choices do not preserve '{profile.name}' Nodes.NodeId values.");
        }
        Require(
            nodeEntries.All(entry => entry.Asset is AnatomyProfileSO profile
                && profiles.Contains(profile)),
            "Anatomy node choices contain a profile outside the profile catalog.");
    }

    private static void VerifySelectionsAndPersistence(
        List<Object> temporaryObjects,
        List<AssetSnapshot> sourceSnapshots)
    {
        ContentReferenceSelection.Entry item = First(ContentReferenceKind.Item);
        ContentReferenceSelection.Entry research = First(ContentReferenceKind.Research);
        ContentReferenceSelection.Entry researchNumeric =
            First(ContentReferenceKind.ResearchNumeric);
        ContentReferenceSelection.Entry building = ContentReferenceSelection.Entries(ContentReferenceKind.BuildingNumeric)
            .First(entry => ((BuildingSO)entry.Asset).id > 0);
        ProductionRecipeSO productionSource = ContentReferenceSelection
            .Entries(ContentReferenceKind.Recipe)
            .Select(entry => entry.Asset)
            .OfType<ProductionRecipeSO>()
            .FirstOrDefault();
        SurgicalProcedureSO surgerySource = LoadFirstAsset<SurgicalProcedureSO>();
        CharacterAmbitionDefinitionSO ambitionSource =
            (CharacterAmbitionDefinitionSO)First(ContentReferenceKind.Ambition).Asset;

        Require(productionSource != null, "The recipe catalog has no production recipe.");
        Require(surgerySource != null, "No surgical procedure asset is available.");
        Remember(sourceSnapshots, item.Asset);
        Remember(sourceSnapshots, research.Asset);
        Remember(sourceSnapshots, researchNumeric.Asset);
        Remember(sourceSnapshots, building.Asset);
        Remember(sourceSnapshots, productionSource);
        Remember(sourceSnapshots, surgerySource);
        Remember(sourceSnapshots, ambitionSource);

        ProductionRecipeSO production = Clone(productionSource, temporaryObjects);
        SurgicalProcedureSO surgery = Clone(surgerySource, temporaryObjects);
        CharacterAmbitionDefinitionSO ambition = Clone(ambitionSource, temporaryObjects);

        using (var serialized = new SerializedObject(production))
        {
            serialized.Update();
            SerializedProperty itemProperty = EnsureArrayChild(
                serialized,
                "inputs",
                "itemId");
            Assign(itemProperty, ContentReferenceKind.Item, item);
            SerializedProperty researchProperty = RequireProperty(
                serialized,
                "requiredResearchId");
            Assign(researchProperty, ContentReferenceKind.Research, research);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        using (var serialized = new SerializedObject(surgery))
        {
            serialized.Update();
            SerializedProperty buildingProperty = RequireProperty(
                serialized,
                "primaryFacilityDefinitionId");
            Assign(buildingProperty, ContentReferenceKind.BuildingNumeric, building);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        VerifyJsonRoundTrip(
            production,
            surgery,
            item.Id,
            research.Id,
            building.Id,
            temporaryObjects);
        VerifyUnregisteredSelectionRejected(
            production,
            research,
            temporaryObjects);
        VerifyLegacyIdPreserved(production);
        VerifyMixedSelection(
            productionSource,
            research,
            temporaryObjects);

        using (var serialized = new SerializedObject(surgery))
        {
            serialized.Update();
            SerializedProperty buildingProperty = RequireProperty(
                serialized,
                "primaryFacilityDefinitionId");
            ContentReferenceSelection.WriteId(
                buildingProperty,
                ContentReferenceKind.BuildingNumeric,
                string.Empty, emptyNumericId: 0);
            Require(buildingProperty.intValue == 0, "Clearing the surgical facility restriction did not write 0.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(
                surgery.PrimaryFacilityDefinitionId == 0,
                "The cleared building sentinel was not serialized.");
        }

        using (var serialized = new SerializedObject(ambition))
        {
            SerializedProperty facility = EnsureNestedArrayChild(serialized,
                "activationRequirements.facilities", "buildingDefinitionId");
            var landmark = ContentReferenceSelection.Entries(ContentReferenceKind.Building)
                .Single(entry => entry.Id == "building:landmark:truth-observatory");
            Assign(facility, ContentReferenceKind.Building, landmark);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(ambition.activationRequirements.facilities.Last().buildingDefinitionId
                == ((BuildingSO)landmark.Asset).ContentDefinitionId,
                "String building selection lost the runtime content definition ID.");
            var clone = Clone(ambition, temporaryObjects);
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(ambition), clone);
            Require(clone.activationRequirements.facilities.Last().buildingDefinitionId
                == "building:landmark:truth-observatory", "Landmark ID did not survive JSON.");
        }

        using (var serialized = new SerializedObject(ambition))
        {
            serialized.Update();
            SerializedProperty numericProperty = EnsureNestedArrayChild(
                serialized,
                "activationRequirements.research",
                "researchNumericId");
            Assign(
                numericProperty,
                ContentReferenceKind.ResearchNumeric,
                researchNumeric);
            Require(numericProperty.intValue > 0, "Numeric research selection was not applied.");
            ContentReferenceSelection.WriteId(
                numericProperty,
                ContentReferenceKind.ResearchNumeric,
                string.Empty);
            Require(numericProperty.intValue == 0, "Clearing numeric research did not write 0.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void VerifyJsonRoundTrip(
        ProductionRecipeSO production,
        SurgicalProcedureSO surgery,
        string itemId,
        string researchId,
        string buildingId,
        List<Object> temporaryObjects)
    {
        ProductionRecipeSO productionRoundTrip = Create<ProductionRecipeSO>(temporaryObjects);
        EditorJsonUtility.FromJsonOverwrite(
            EditorJsonUtility.ToJson(production),
            productionRoundTrip);
        using (var serialized = new SerializedObject(productionRoundTrip))
        {
            serialized.Update();
            Require(
                ContentReferenceSelection.ReadId(
                    EnsureArrayChild(serialized, "inputs", "itemId"),
                    ContentReferenceKind.Item) == itemId,
                "Item ID did not survive Editor JSON roundtrip.");
            Require(
                ContentReferenceSelection.ReadId(
                    RequireProperty(serialized, "requiredResearchId"),
                    ContentReferenceKind.Research) == researchId,
                "Research ID did not survive Editor JSON roundtrip.");
        }

        SurgicalProcedureSO surgeryRoundTrip = Create<SurgicalProcedureSO>(temporaryObjects);
        EditorJsonUtility.FromJsonOverwrite(
            EditorJsonUtility.ToJson(surgery),
            surgeryRoundTrip);
        using var surgerySerialized = new SerializedObject(surgeryRoundTrip);
        surgerySerialized.Update();
        Require(
            ContentReferenceSelection.ReadId(
                RequireProperty(surgerySerialized, "primaryFacilityDefinitionId"),
                ContentReferenceKind.BuildingNumeric) == buildingId,
            "Building ID did not survive Editor JSON roundtrip.");
    }

    private static void VerifyUnregisteredSelectionRejected(
        ProductionRecipeSO production,
        ContentReferenceSelection.Entry research,
        List<Object> temporaryObjects)
    {
        ResearchProjectSO transientResearch = Create<ResearchProjectSO>(temporaryObjects);
        using var serialized = new SerializedObject(production);
        serialized.Update();
        SerializedProperty property = RequireProperty(serialized, "requiredResearchId");
        property.stringValue = research.Id;
        string before = ContentReferenceSelection.ReadId(
            property,
            ContentReferenceKind.Research);
        bool assigned = ContentReferenceSelection.TryAssign(
            property,
            ContentReferenceKind.Research,
            transientResearch,
            out string error);
        Require(!assigned, "An unregistered transient definition was accepted.");
        Require(!string.IsNullOrWhiteSpace(error), "Rejected selection did not explain its failure.");
        Require(
            ContentReferenceSelection.ReadId(property, ContentReferenceKind.Research) == before,
            "Rejected selection changed the existing ID.");
    }

    private static void VerifyLegacyIdPreserved(ProductionRecipeSO production)
    {
        const string legacyId = "legacy:missing-research";
        using (var serialized = new SerializedObject(production))
        {
            serialized.Update();
            SerializedProperty property = RequireProperty(serialized, "requiredResearchId");
            property.stringValue = legacyId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        string beforeJson = EditorJsonUtility.ToJson(production);
        using (var serialized = new SerializedObject(production))
        {
            serialized.Update();
            SerializedProperty property = RequireProperty(serialized, "requiredResearchId");
            Require(
                ContentReferenceSelection.ReadId(property, ContentReferenceKind.Research)
                    == legacyId,
                "A legacy ID was not returned verbatim for display.");
            Require(property.stringValue == legacyId, "Reading a legacy ID mutated its value.");
        }
        Require(
            EditorJsonUtility.ToJson(production) == beforeJson,
            "Reading a legacy ID changed serialized data.");
    }

    private static void VerifyMixedSelection(
        ProductionRecipeSO source,
        ContentReferenceSelection.Entry research,
        List<Object> temporaryObjects)
    {
        ProductionRecipeSO left = Clone(source, temporaryObjects);
        ProductionRecipeSO right = Clone(source, temporaryObjects);
        WriteString(left, "requiredResearchId", "legacy:left");
        WriteString(right, "requiredResearchId", "legacy:right");
        using (var mixed = new SerializedObject(new Object[] { left, right }))
        {
            mixed.Update();
            SerializedProperty property = RequireProperty(mixed, "requiredResearchId");
            Require(property.hasMultipleDifferentValues, "Mixed-value setup was not preserved.");
            Assign(property, ContentReferenceKind.Research, research);
            mixed.ApplyModifiedPropertiesWithoutUndo();
        }
        Require(
            ReadId(left, "requiredResearchId", ContentReferenceKind.Research) == research.Id
            && ReadId(right, "requiredResearchId", ContentReferenceKind.Research) == research.Id,
            "Explicit mixed-object selection did not update both clones.");
    }

    private static void VerifySemanticKinds(
        List<Object> temporaryObjects,
        List<AssetSnapshot> sourceSnapshots)
    {
        CharacterAmbitionDefinitionSO ambitionSource =
            (CharacterAmbitionDefinitionSO)First(ContentReferenceKind.Ambition).Asset;
        SpeciesCultureDefinitionSO cultureSource =
            (SpeciesCultureDefinitionSO)First(ContentReferenceKind.Culture).Asset;
        CharacterBackgroundDefinitionSO backgroundSource =
            LoadFirstAsset<CharacterBackgroundDefinitionSO>();
        Require(backgroundSource != null, "No character background asset is available.");
        Remember(sourceSnapshots, ambitionSource);
        Remember(sourceSnapshots, cultureSource);
        Remember(sourceSnapshots, backgroundSource);

        CharacterAmbitionDefinitionSO ambition = Clone(ambitionSource, temporaryObjects);
        SpeciesCultureDefinitionSO culture = Clone(cultureSource, temporaryObjects);
        CharacterBackgroundDefinitionSO background = Clone(backgroundSource, temporaryObjects);

        using (var serialized = new SerializedObject(ambition))
        {
            serialized.Update();
            SerializedProperty property = EnsureArrayChild(
                serialized,
                "relatedEventWeights",
                "id");
            Require(
                ContentReferenceSelection.ResolveKind(
                    ContentReferenceKind.WeightedTarget,
                    property) == ContentReferenceKind.LifeEvent,
                "relatedEventWeights did not resolve to LifeEvent.");
        }
        using (var serialized = new SerializedObject(culture))
        {
            serialized.Update();
            SerializedProperty property = EnsureArrayChild(
                serialized,
                "otherCultureAttitudes",
                "id");
            Require(
                ContentReferenceSelection.ResolveKind(
                    ContentReferenceKind.WeightedTarget,
                    property) == ContentReferenceKind.Culture,
                "otherCultureAttitudes did not resolve to Culture.");
        }
        using (var serialized = new SerializedObject(background))
        {
            serialized.Update();
            SerializedProperty reaction = EnsureArrayChild(
                serialized,
                "factionReactions",
                "id");
            Require(
                ContentReferenceSelection.ResolveKind(
                    ContentReferenceKind.WeightedTarget,
                    reaction) == null,
                "factionReactions was incorrectly treated as an SO reference.");

            SerializedProperty effects = RequireProperty(serialized, "startingEffects");
            effects.arraySize = Math.Max(1, effects.arraySize);
            SerializedProperty effect = effects.GetArrayElementAtIndex(0);
            SerializedProperty kind = effect.FindPropertyRelative("kind");
            SerializedProperty target = effect.FindPropertyRelative("targetId");
            Require(kind != null && target != null, "Effect fields are unavailable.");
            VerifyEffectKind(kind, target, V20ContentEffectKind.FactionRapport,
                ContentReferenceKind.Faction, "affected-faction");
            VerifyEffectKind(kind, target, V20ContentEffectKind.ItemGrant,
                ContentReferenceKind.Item, "item:test");
            VerifyEffectKind(kind, target, V20ContentEffectKind.Mood,
                null, "mood-label");
            VerifyEffectKind(kind, target, V20ContentEffectKind.SkillExperience,
                null, "totalXP");
        }
    }

    private static void VerifyEffectKind(
        SerializedProperty kindProperty,
        SerializedProperty targetProperty,
        V20ContentEffectKind effectKind,
        ContentReferenceKind? expected,
        string semanticTarget)
    {
        kindProperty.intValue = (int)effectKind;
        targetProperty.stringValue = semanticTarget;
        Require(
            ContentReferenceSelection.ResolveKind(
                ContentReferenceKind.EffectTarget,
                targetProperty) == expected,
            $"{effectKind} resolved to the wrong target kind.");
        Require(
            targetProperty.stringValue == semanticTarget,
            $"{effectKind} target semantics were mutated during resolution.");
    }

    private static void VerifySourceAssetsUnchanged(List<AssetSnapshot> snapshots)
    {
        foreach (AssetSnapshot snapshot in snapshots)
        {
            Require(
                EditorJsonUtility.ToJson(snapshot.Asset) == snapshot.Json,
                $"Source asset '{snapshot.Asset.name}' was mutated.");
            Require(
                EditorUtility.IsDirty(snapshot.Asset) == snapshot.Dirty,
                $"Source asset '{snapshot.Asset.name}' dirty state changed.");
        }
    }

    private static void Assign(
        SerializedProperty property,
        ContentReferenceKind kind,
        ContentReferenceSelection.Entry entry)
    {
        Require(
            ContentReferenceSelection.TryAssign(
                property,
                kind,
                entry.Asset,
                out string error),
            $"Could not assign {kind} '{entry.Id}': {error}");
        Require(
            ContentReferenceSelection.ReadId(property, kind) == entry.Id,
            $"Assigning {kind} did not preserve ID '{entry.Id}'.");
    }

    private static ContentReferenceSelection.Entry First(ContentReferenceKind kind)
    {
        IReadOnlyList<ContentReferenceSelection.Entry> entries =
            ContentReferenceSelection.Entries(kind);
        Require(entries.Count > 0, $"{kind} has no catalog entry.");
        return entries[0];
    }

    private static SerializedProperty EnsureArrayChild(
        SerializedObject serialized,
        string arrayPath,
        string childName)
    {
        return EnsureNestedArrayChild(serialized, arrayPath, childName);
    }

    private static SerializedProperty EnsureNestedArrayChild(
        SerializedObject serialized,
        string arrayPath,
        string childName)
    {
        SerializedProperty array = RequireProperty(serialized, arrayPath);
        Require(array.isArray, $"'{arrayPath}' is not an array.");
        array.arraySize = Math.Max(1, array.arraySize);
        SerializedProperty child = array.GetArrayElementAtIndex(0)
            .FindPropertyRelative(childName);
        Require(child != null, $"'{arrayPath}' has no child '{childName}'.");
        return child;
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string path)
    {
        SerializedProperty property = serialized.FindProperty(path);
        Require(property != null, $"Serialized property '{path}' is unavailable.");
        return property;
    }

    private static string ReadId(
        Object target,
        string propertyPath,
        ContentReferenceKind kind)
    {
        using var serialized = new SerializedObject(target);
        serialized.Update();
        return ContentReferenceSelection.ReadId(
            RequireProperty(serialized, propertyPath),
            kind);
    }

    private static void WriteString(Object target, string propertyPath, string value)
    {
        using var serialized = new SerializedObject(target);
        serialized.Update();
        RequireProperty(serialized, propertyPath).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T LoadFirstAsset<T>() where T : ScriptableObject
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .FirstOrDefault(asset => asset != null);
    }

    private static T Clone<T>(T source, List<Object> temporaryObjects)
        where T : ScriptableObject
    {
        T clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        temporaryObjects.Add(clone);
        return clone;
    }

    private static T Create<T>(List<Object> temporaryObjects)
        where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        instance.hideFlags = HideFlags.HideAndDontSave;
        temporaryObjects.Add(instance);
        return instance;
    }

    private static void Remember(List<AssetSnapshot> snapshots, Object asset)
    {
        if (snapshots.Any(snapshot => snapshot.Asset == asset))
            return;
        snapshots.Add(new AssetSnapshot
        {
            Asset = asset,
            Json = EditorJsonUtility.ToJson(asset),
            Dirty = EditorUtility.IsDirty(asset)
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
