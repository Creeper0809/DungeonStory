using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ContentReferenceSelectionDebugScenarios
{
    private const string AddressableSettingsPath =
        "Assets/AddressableAssetsData/AddressableAssetSettings.asset";
    private const string AddressableCurrentHashField = "m_currentHash";

    public static void RunWithUndoIsolation(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));
        Object settings = AssetDatabase.LoadMainAssetAtPath(AddressableSettingsPath);
        Require(settings != null,
            $"Addressables settings are missing at '{AddressableSettingsPath}'.");
        FieldInfo hashField = RequireAddressableCurrentHashField(settings);
        Hash128 beforeHash = (Hash128)hashField.GetValue(settings);
        string beforeJson = EditorJsonUtility.ToJson(settings);
        bool beforeDirty = EditorUtility.IsDirty(settings);
        JObject before = ParseAddressableSettings(beforeJson);

        try
        {
            action();
        }
        finally
        {
            settings = AssetDatabase.LoadMainAssetAtPath(AddressableSettingsPath);
            Require(settings != null,
                $"Addressables settings disappeared from '{AddressableSettingsPath}'.");
            hashField = RequireAddressableCurrentHashField(settings);
            Hash128 afterHash = (Hash128)hashField.GetValue(settings);
            bool afterDirty = EditorUtility.IsDirty(settings);
            bool hashChanged = !beforeHash.Equals(afterHash);
            if (hashChanged)
                hashField.SetValue(settings, beforeHash);

            // Editor JSON may wrap serialized fields in a MonoBehaviour object.
            // Restore only the known derived cache before comparing the complete
            // JSON; do not filter fields or discard any other settings change.
            JObject after = ParseAddressableSettings(EditorJsonUtility.ToJson(settings));
            if (!JToken.DeepEquals(before, after))
            {
                string changed = string.Join(", ", ChangedTopLevelFields(before, after));
                throw new InvalidOperationException(
                    "Undo changed serialized Addressables settings outside its derived current-hash cache: "
                    + changed);
            }

            if (!hashChanged && beforeDirty != afterDirty)
                throw new InvalidOperationException(
                    "Undo changed the Addressables settings dirty state without changing its current-hash cache.");

            if (beforeDirty) EditorUtility.SetDirty(settings);
            else EditorUtility.ClearDirty(settings);

            Hash128 restoredHash = (Hash128)RequireAddressableCurrentHashField(settings)
                .GetValue(settings);
            JObject restored = ParseAddressableSettings(
                EditorJsonUtility.ToJson(settings));
            Require(restoredHash.Equals(beforeHash),
                "Addressables current-hash cache did not restore to its original value.");
            Require(JToken.DeepEquals(before, restored),
                "Addressables settings JSON changed during current-hash restoration.");
            Require(EditorUtility.IsDirty(settings) == beforeDirty,
                "Addressables settings dirty state did not restore to its original value.");
        }
    }

    private static FieldInfo RequireAddressableCurrentHashField(Object settings)
    {
        FieldInfo field = settings.GetType().GetField(
            AddressableCurrentHashField,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null && field.FieldType == typeof(Hash128),
            $"Addressables settings have no private Hash128 field '{AddressableCurrentHashField}'.");
        return field;
    }

    private static JObject ParseAddressableSettings(string json) =>
        JObject.Parse(json ?? string.Empty);

    private static IEnumerable<string> ChangedTopLevelFields(
        JObject before,
        JObject after)
    {
        return before.Properties().Select(property => property.Name)
            .Concat(after.Properties().Select(property => property.Name))
            .Distinct(StringComparer.Ordinal)
            .Where(name => !JToken.DeepEquals(before[name], after[name]))
            .OrderBy(name => name, StringComparer.Ordinal);
    }

    public static void VerifyHeritableTraitConsequenceAuthoring()
    {
        var first = ScriptableObject.CreateInstance<HeritableTraitDefinitionSO>();
        var second = ScriptableObject.CreateInstance<HeritableTraitDefinitionSO>();
        first.hideFlags = HideFlags.HideAndDontSave;
        second.hideFlags = HideFlags.HideAndDontSave;
        first.consequences.Add(new HeritableTraitConsequence
        {
            kind = HeritableTraitConsequenceKind.Aptitude,
            targetId = string.Empty,
            multiplierDelta = 0.1f
        });
        second.consequences.Add(new HeritableTraitConsequence
        {
            kind = HeritableTraitConsequenceKind.EnvironmentalTolerance,
            targetId = "unknown-existing-target",
            multiplierDelta = 0.1f
        });
        try
        {
            Undo.ClearUndo(first);
            Undo.ClearUndo(second);
            using (var serialized = new SerializedObject(first))
            {
                SerializedProperty consequence = serialized.FindProperty(
                    "consequences.Array.data[0]");
                SerializedProperty kind = consequence.FindPropertyRelative("kind");
                SerializedProperty target = consequence.FindPropertyRelative("targetId");
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Heritable trait target selection probe");
                kind.intValue = (int)HeritableTraitConsequenceKind.Movement;
                Require(target.stringValue.Length == 0,
                    "A pending kind change replaced the blank target automatically.");
                Require(!HeritableTraitConsequenceDrawer.TryAssignTarget(
                        consequence, "unknown-target", out _)
                    && target.stringValue.Length == 0,
                    "An unknown target was accepted or replaced the existing value.");
                Require(HeritableTraitConsequenceDrawer.TryAssignTarget(
                        consequence, "move-speed", out string selectionError),
                    "The pending Movement kind did not expose move-speed: "
                    + selectionError);

                Require(serialized.ApplyModifiedProperties(),
                    "The selected consequence was not applied.");
                Undo.CollapseUndoOperations(undoGroup);
                Require(first.consequences[0].kind
                        == HeritableTraitConsequenceKind.Movement
                    && first.consequences[0].targetId == "move-speed",
                    "The selected consequence did not reach the serialized fixture.");
                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                serialized.Update();
                Require(first.consequences[0].kind
                        == HeritableTraitConsequenceKind.Aptitude
                    && first.consequences[0].targetId.Length == 0,
                    "Undo did not restore the original consequence kind and target.");
            }

            using (var mixedKinds = new SerializedObject(
                       new Object[] { first, second }))
            {
                SerializedProperty consequence = mixedKinds.FindProperty(
                    "consequences.Array.data[0]");
                Require(consequence.FindPropertyRelative("kind")
                        .hasMultipleDifferentValues,
                    "The mixed-kind fixture did not expose a mixed kind.");
                Require(!HeritableTraitConsequenceDrawer.TryAssignTarget(
                        consequence, "rune-tuning", out _),
                    "A target was assigned across mixed consequence kinds.");
                Require(first.consequences[0].targetId.Length == 0
                    && second.consequences[0].targetId
                        == "unknown-existing-target",
                    "The mixed-kind rejection changed an existing target.");
            }

            first.consequences[0].kind = HeritableTraitConsequenceKind.Aptitude;
            first.consequences[0].targetId = "rune-tuning";
            second.consequences[0].kind = HeritableTraitConsequenceKind.Aptitude;
            second.consequences[0].targetId = "work-burst";
            using (var mixedTargets = new SerializedObject(
                       new Object[] { first, second }))
            {
                SerializedProperty consequence = mixedTargets.FindProperty(
                    "consequences.Array.data[0]");
                Require(consequence.FindPropertyRelative("targetId")
                        .hasMultipleDifferentValues,
                    "The mixed-target fixture did not expose mixed values.");
                Require(HeritableTraitConsequenceDrawer.TryAssignTarget(
                        consequence, "work-burst", out string mixedError),
                    "A valid shared target could not be selected: " + mixedError);
                mixedTargets.ApplyModifiedProperties();
                Require(first.consequences[0].targetId == "work-burst"
                    && second.consequences[0].targetId == "work-burst",
                    "The shared target was not applied to every selected object.");
            }

            Debug.Log("HERITABLE_TRAIT_CONSEQUENCE_AUTHORING=PASS; pending-kind; explicit-target; undo; mixed-kind; mixed-target");
        }
        finally
        {
            Undo.ClearUndo(first);
            Undo.ClearUndo(second);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }
    }

    // Runs a real IMGUI property draw. Invoke only inside the isolated validation
    // desktop/server: no window is opened in the user's interactive Editor.
    public static void VerifyInspectorGUI()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Inspector verification requires isolated batch mode.");
        var first = ScriptableObject.CreateInstance<ContentReferenceInspectorProbeData>();
        var second = ScriptableObject.CreateInstance<ContentReferenceInspectorProbeData>();
        var recipe = ScriptableObject.CreateInstance<ProductionRecipeSO>();
        var window = ScriptableObject.CreateInstance<ContentReferenceInspectorProbeWindow>();
        try
        {
            window.position = new Rect(0, 0, 620, 160);
            window.ShowUtility();

            recipe.hideFlags = HideFlags.HideAndDontSave;
            using (var recipeSerialized = new SerializedObject(recipe))
            {
                var inputs = recipeSerialized.FindProperty("inputs");
                Require(inputs != null && inputs.isArray && recipe.Inputs.Count == 0,
                    "The production recipe pending-list fixture is invalid.");
                inputs.arraySize = 1;
                var itemId = inputs.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("itemId");
                Require(itemId != null && itemId.stringValue.Length == 0,
                    "The pending production input did not start with a blank item ID.");
                DrawProbe(window, itemId, "pending production item list element");
                Require(recipe.Inputs.Count == 0,
                    "Inspector drawing applied the pending production input early.");
                Require(itemId.stringValue.Length == 0,
                    "Inspector drawing changed the pending production item ID.");
                recipeSerialized.ApplyModifiedProperties();
                Require(recipe.Inputs.Count == 1
                        && string.IsNullOrEmpty(recipe.Inputs[0].ItemId),
                    "Applying the pending production input changed its blank item ID or count.");
            }

            using var serialized = new SerializedObject(first);
            var list = serialized.FindProperty("effects");
            list.arraySize = 1;
            var effect = list.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("kind").intValue = (int)AuthoredGameplayEffectKind.GuestDemandMultiplier;
            var target = effect.FindPropertyRelative("textValue");
            Require(first.effects.Count == 0, "The new-list fixture was already applied.");
            var reference = new ContentReferenceAttribute(ContentReferenceKind.AuthoredEffectText);
            Require(ContentReferenceSelection.ResolveReference(reference, target)?.Kind == ContentReferenceKind.Species,
                "Pending list element ignored its serialized kind.");
            DrawProbe(window, target, "pending new list element");
            Require(first.effects.Count == 0, "Inspector drawing unexpectedly applied the pending list.");
            serialized.ApplyModifiedProperties();

            Undo.IncrementCurrentGroup();
            first.managed = new AuthoredGameplayEffectRecord { kind = AuthoredGameplayEffectKind.GuestDemandMultiplier };
            serialized.Update();
            serialized.FindProperty("managed").managedReferenceValue = new AuthoredGameplayEffectRecord
                { kind = AuthoredGameplayEffectKind.IntruderPattern };
            target = serialized.FindProperty("managed.textValue");
            Require(ContentReferenceSelection.ResolveReference(reference, target)?.Kind == ContentReferenceKind.InvasionPattern,
                "Pending managed-reference replacement used the old owner.");
            DrawProbe(window, target, "pending managed-reference replacement");
            serialized.ApplyModifiedProperties();
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            serialized.Update();
            target = serialized.FindProperty("managed.textValue");
            Require(ContentReferenceSelection.ResolveReference(reference, target)?.Kind == ContentReferenceKind.Species,
                "Undo did not restore the reference context.");
            DrawProbe(window, target, "undo restored context");

            second.effects.Add(new AuthoredGameplayEffectRecord { kind = AuthoredGameplayEffectKind.IntruderPattern });
            using var mixed = new SerializedObject(new Object[] { first, second });
            var mixedTarget = mixed.FindProperty("effects.Array.data[0].textValue");
            Require(ContentReferenceSelection.HasMixedContext(reference, mixedTarget), "Mixed kinds were not detected.");
            DrawProbe(window, mixedTarget, "multi-object mixed context");
            Require(window.DrawCount >= 5, "No actual Inspector property GUI events executed.");
            Debug.Log("CONTENT_REFERENCE_INSPECTOR_GUI=PASS; item-unapplied-list; contextual-unapplied-list; managed-reference; undo; multi-object");
        }
        finally
        {
            window.Property = null;
            window.Close();
            Undo.ClearUndo(first);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(recipe);
        }
    }

    public static void VerifyOwnerFixedInspectorGUI()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Inspector verification requires isolated batch mode.");
        var owner = ScriptableObject.CreateInstance<CharacterSO>();
        var window = ScriptableObject.CreateInstance<ContentReferenceInspectorProbeWindow>();
        Editor editor = null;
        try
        {
            owner.hideFlags = HideFlags.HideAndDontSave;
            owner.role = CharacterRole.Owner;
            using var serialized = new SerializedObject(owner);
            var skills = serialized.FindProperty("ownerFixedSkills");
            skills.arraySize = 1;
            var skill = skills.GetArrayElementAtIndex(0);
            skill.FindPropertyRelative("displayName").stringValue = "Owner picker probe";
            var modules = skill.FindPropertyRelative("modules");
            modules.arraySize = 1;
            var module = modules.GetArrayElementAtIndex(0);
            module.FindPropertyRelative("moduleId").stringValue = "work_speed";
            module.FindPropertyRelative("variantId").stringValue = "small";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            window.position = new Rect(0, 0, 680, 850);
            window.ShowUtility();
            module.FindPropertyRelative("moduleId").stringValue = "damage";
            var variant = module.FindPropertyRelative("variantId");
            var reference = new ContentReferenceAttribute(ContentReferenceKind.SkillVariant);
            Require(ContentReferenceSelection.Entries(reference, variant).Any(entry => entry.Id == "light"),
                "Pending owner module did not expose its variants.");
            DrawProbe(window, variant, "pending owner module variant");
            Require(owner.OwnerFixedSkills.First().modules[0].moduleId == "work_speed"
                    && variant.stringValue == "small",
                "Drawing a different module silently applied or repaired the previous variant.");
            module.FindPropertyRelative("moduleId").stringValue = "work_speed";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (string derived in new[] { "ruleId", "combinationId", "formulaVersion", "primaryCapabilityId",
                "drawbackId", "formulaCapabilities", "evidenceIds", "narrativeTrace", "formulaCatalogSha256" })
                Require(!CharacterSOEditor.IsOwnerFixedInput(derived, 0),
                    "Generated snapshot field became an owner input: " + derived);
            Require(!CharacterSOEditor.IsOwnerFixedInput("modules", 1)
                    && CharacterSOEditor.IsOwnerFixedInput("modules", 0),
                "Frozen formula modules are editable or legacy module authoring is unavailable.");
            skill.FindPropertyRelative("formulaVersion").intValue = 1;
            skill.FindPropertyRelative("combinationId").stringValue = "frozen-existing-probe";
            CharacterSOEditor.ResizeOwnerFixedSkills(skills, 2);
            var added = skills.GetArrayElementAtIndex(1);
            Require(added.FindPropertyRelative("formulaVersion").intValue == 0
                    && added.FindPropertyRelative("combinationId").stringValue.Length == 0
                    && added.FindPropertyRelative("modules").arraySize == 0
                    && skills.GetArrayElementAtIndex(0).FindPropertyRelative("formulaVersion").intValue == 1
                    && owner.OwnerFixedSkills.Count == 1,
                "Adding an owner skill copied a frozen snapshot, changed an existing row or applied pending edits.");
            serialized.Update();
            string before = EditorJsonUtility.ToJson(owner);
            editor = Editor.CreateEditor(owner, typeof(CharacterSOEditor));
            var editorSkills = editor.serializedObject.FindProperty("ownerFixedSkills");
            editorSkills.isExpanded = true;
            var editorSkill = editorSkills.GetArrayElementAtIndex(0);
            editorSkill.isExpanded = true;
            editorSkill.FindPropertyRelative("modules").isExpanded = true;
            editorSkill.FindPropertyRelative("modules").GetArrayElementAtIndex(0).isExpanded = true;
            editorSkill.FindPropertyRelative("formulaVersion").isExpanded = true;
            window.Property = null;
            window.CustomEditor = editor;
            int previous = window.DrawCount;
            window.SendEvent(new Event { type = EventType.Layout });
            window.SendEvent(new Event { type = EventType.Repaint });
            if (window.Failure != null) throw new InvalidOperationException("Owner Inspector GUI", window.Failure);
            Require(window.DrawCount > previous && EditorJsonUtility.ToJson(owner) == before,
                "Owner custom Inspector failed to draw or changed its serialized authoring.");
            Debug.Log("N07_OWNER_INSPECTOR_GUI=PASS; pending-module-variant; readonly-snapshot; actual-custom-editor; unchanged-source");
        }
        finally
        {
            window.Property = null;
            window.CustomEditor = null;
            window.Close();
            if (editor != null) Object.DestroyImmediate(editor);
            Undo.ClearUndo(owner);
            Object.DestroyImmediate(owner);
        }
    }

    private static void DrawProbe(ContentReferenceInspectorProbeWindow window, SerializedProperty property, string context)
    {
        window.Property = property;
        int previous = window.DrawCount;
        window.SendEvent(new Event { type = EventType.Layout });
        window.SendEvent(new Event { type = EventType.Repaint });
        if (window.Failure != null) throw new InvalidOperationException(context, window.Failure);
        Require(window.DrawCount > previous, "Inspector GUI did not execute: " + context);
    }
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
            VerifyMixedContext(temporaryObjects);
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
                or ContentReferenceKind.WeightedTarget or ContentReferenceKind.Definition or ContentReferenceKind.NarrativeTarget
                or ContentReferenceKind.ExpeditionChoice or ContentReferenceKind.AuthoredEffectText or ContentReferenceKind.EncounterTarget)
            {
                continue;
            }

            IReadOnlyList<ContentReferenceSelection.Entry> entries =
                ContentReferenceSelection.Entries(kind);
            if (ContentReferenceCatalog.RequiresOwnerContext(kind)
                && kind != ContentReferenceKind.SkillCapability)
            {
                // Local IDs must not leak into a global list. Their exact
                // owner/pending-row choices are verified by the owner-scoped
                // and owner-fixed Inspector scenarios in this same batch.
                Require(entries.Count == 0, $"{kind} exposed choices without its owner.");
                continue;
            }
            Require(entries.Count > 0, $"{kind} has no catalog entries.");
            if (kind == ContentReferenceKind.WorkType)
            {
                Require(entries.Select(value => value.Id).SequenceEqual(BuiltInWorkTypeIds.All.Select(value => value.Value)),
                    "Work choices differ from the production work-type registry.");
                continue;
            }
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
            if (kind is not (ContentReferenceKind.AnatomyNode or ContentReferenceKind.GameplayValueTarget))
            {
                Require(
                    entries.Select(entry => entry.Id)
                        .Distinct(StringComparer.Ordinal).Count() == entries.Count,
                    $"{kind} contains duplicate IDs.");
                if (kind is not (ContentReferenceKind.InvasionPattern or ContentReferenceKind.SkillCapability or ContentReferenceKind.SkillModule))
                    Require(entries.Select(entry => entry.Asset).Distinct().Count() == entries.Count,
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
            ContentReferenceSelection.ResolveReference(
                new ContentReferenceAttribute(ContentReferenceKind.EffectTarget), targetProperty)?.Kind == expected,
            $"{effectKind} drawer ignored the pending serialized kind.");
        Require(
            targetProperty.stringValue == semanticTarget,
            $"{effectKind} target semantics were mutated during resolution.");
    }

    private static void VerifyMixedContext(List<Object> temporaryObjects)
    {
        var left = Create<CharacterBackgroundDefinitionSO>(temporaryObjects);
        var right = Create<CharacterBackgroundDefinitionSO>(temporaryObjects);
        foreach (var value in new[] { left, right })
        {
            using var serialized = new SerializedObject(value);
            EnsureArrayChild(serialized, "startingEffects", "kind").intValue =
                (int)(value == left ? V20ContentEffectKind.ItemGrant : V20ContentEffectKind.DiseaseExposure);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        using var mixed = new SerializedObject(new Object[] { left, right });
        var target = mixed.FindProperty("startingEffects.Array.data[0].targetId");
        var reference = new ContentReferenceAttribute(ContentReferenceKind.EffectTarget);
        Require(ContentReferenceSelection.HasMixedContext(reference, target)
            && ContentReferenceSelection.ResolveReference(reference, target) == null,
            "Mixed effect kinds must not share a target selector.");

        var narrative = Create<InGameNarrativeTextCatalogSO>(temporaryObjects);
        using var text = new SerializedObject(narrative);
        var kind = EnsureArrayChild(text, "entries", "kind");
        var id = text.FindProperty("entries.Array.data[0].stableId");
        kind.intValue = (int)InGameNarrativeTextKind.Facility;
        Require(ContentReferenceSelection.ResolveReference(
            new ContentReferenceAttribute(ContentReferenceKind.NarrativeTarget), id)?.Kind == ContentReferenceKind.Building,
            "Narrative drawer ignored the pending serialized kind.");
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

public sealed class ContentReferenceInspectorProbeData : ScriptableObject
{
    public List<AuthoredGameplayEffectRecord> effects = new();
    [SerializeReference] public AuthoredGameplayEffectRecord managed;
}

public sealed class ContentReferenceInspectorProbeWindow : EditorWindow
{
    internal SerializedProperty Property;
    internal Editor CustomEditor;
    internal int DrawCount;
    internal Exception Failure;
    private void OnGUI()
    {
        if (Property == null && CustomEditor == null || Failure != null) return;
        try
        {
            if (CustomEditor != null) CustomEditor.OnInspectorGUI();
            else EditorGUI.PropertyField(new Rect(8, 8, 600, EditorGUI.GetPropertyHeight(Property)), Property, true);
            DrawCount++;
        }
        catch (Exception exception) { Failure = exception; }
    }
}
