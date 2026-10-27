using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class OwnerScopedContentReferenceDebugScenarios
{
    private const string RootPath = "Assets/Resources/SO/GameContentCatalog.asset";
    private const string CampaignPath =
        "Assets/Resources/SO/Content/OffenseCampaignCatalog.asset";
    private const string AcquiredSettingsPath =
        "Assets/Resources/SO/V25/AcquiredTraits/acquired-trait-settings.asset";
    private const string SkillSettingsPath =
        "Assets/Resources/SO/Character/CharacterSkillSystemSettings.asset";
    private const string EquipmentCatalogPath =
        "Assets/Resources/SO/EquipmentEvolutionFormulaCatalog.asset";
    private const string FacilityRecipePath =
        "Assets/Resources/SO/FacilityEvolution/P1/EV_AlchemyBench.asset";

    [MenuItem("DungeonStory/V27/Content/Validate Owner-Scoped References")]
    public static void RunAll()
    {
        GameContentCatalogSO root = RequireAsset<GameContentCatalogSO>(RootPath);
        ContentReferenceCatalog catalog = ContentReferenceSelection.CreateCatalog(root);
        ContentReferenceSelection.Invalidate();
        VerifyCampaignTargets(catalog);
        VerifyAcquiredTraitBindings(catalog);
        VerifyAcquiredFormulaCapabilities(catalog);
        VerifyEvolutionFormulaCapabilities(catalog);
        Debug.Log("OWNER_SCOPED_CONTENT_REFERENCES=PASS; pending rows; local ownership; structured rejection; repair retry");
    }

    public static void RunR28ForAutomation()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("R28 local reference verification requires isolated batch mode.");
        IGameContentCatalog content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        AnatomyProfileSO anatomySource = content.GetAll<AnatomyProfileSO>()
            .First(value => value.Nodes.Count >= 2 && value.Nodes[0] != null && value.Nodes[1] != null);
        EnemyArchetypeDefinitionSO enemySource = content.GetAll<EnemyArchetypeDefinitionSO>()
            .First(value => value.abilityIds.Count > 0);
        ScriptableObject[] sources = { anatomySource, enemySource };
        string[] sourceJson = sources.Select(EditorJsonUtility.ToJson).ToArray();
        bool[] sourceDirty = sources.Select(EditorUtility.IsDirty).ToArray();
        ScriptableObject[] clones = sources.Select(value =>
        {
            ScriptableObject clone = Object.Instantiate(value);
            clone.hideFlags = HideFlags.HideAndDontSave;
            return clone;
        }).ToArray();
        string[] cloneJson = clones.Select(EditorJsonUtility.ToJson).ToArray();
        ContentReferenceInspectorProbeWindow window =
            ScriptableObject.CreateInstance<ContentReferenceInspectorProbeWindow>();
        try
        {
            ContentReferenceSelection.Invalidate();
            window.position = new Rect(0, 0, 720, 240);
            window.ShowUtility();
            VerifyPendingAnatomy(window, (AnatomyProfileSO)clones[0]);
            VerifyPendingBossAbilities(window, (EnemyArchetypeDefinitionSO)clones[1]);
            for (int index = 0; index < clones.Length; index++)
                Require(EditorJsonUtility.ToJson(clones[index]) == cloneJson[index],
                    "R28 pending/Undo verification changed its clone.");
            Debug.Log("R28_LOCAL_REFERENCE_SELECTION=PASS; anatomy add rename delete; self parent; attachment; boss owner; mixed; undo");
        }
        finally
        {
            window.Property = null;
            window.Close();
            if (window != null) Object.DestroyImmediate(window);
            foreach (ScriptableObject clone in clones)
            {
                Undo.ClearUndo(clone);
                Object.DestroyImmediate(clone);
            }
            for (int index = 0; index < sources.Length; index++)
                Require(EditorJsonUtility.ToJson(sources[index]) == sourceJson[index]
                        && EditorUtility.IsDirty(sources[index]) == sourceDirty[index],
                    "R28 local selection changed a source asset or its dirty state.");
        }
    }

    private static void VerifyPendingAnatomy(ContentReferenceInspectorProbeWindow window, AnatomyProfileSO anatomy)
    {
        const string addedId = "r28-pending-node";
        const string renamedId = "r28-renamed-node";
        string before = EditorJsonUtility.ToJson(anatomy);
        string originalId = anatomy.Nodes[0].NodeId;
        ContentReferenceAttribute reference = new(ContentReferenceKind.AnatomyNode);
        using SerializedObject serialized = new(anatomy);
        SerializedProperty nodes = RequireProperty(serialized, "nodes");
        int addedIndex = nodes.arraySize++;
        SerializedProperty added = nodes.GetArrayElementAtIndex(addedIndex);
        added.FindPropertyRelative("nodeId").stringValue = addedId;
        added.FindPropertyRelative("parentNodeId").stringValue = addedId;
        SerializedProperty parent = nodes.GetArrayElementAtIndex(0).FindPropertyRelative("parentNodeId");
        parent.stringValue = addedId;
        var candidates = ContentReferenceSelection.Entries(reference, parent);
        Require(candidates.Any(value => value.Id == addedId)
                && candidates.All(value => value.Id != originalId),
            "Pending anatomy addition was missing or the owner was offered as its own parent.");
        Require(ContentReferenceSelection.IsCurrentValid(reference, reference, addedId, candidates),
            "Pending anatomy parent was marked invalid by the drawer.");
        DrawProbe(window, parent, "pending anatomy parent");
        SerializedProperty selfParent = added.FindPropertyRelative("parentNodeId");
        candidates = ContentReferenceSelection.Entries(reference, selfParent);
        Require(candidates.All(value => value.Id != addedId)
                && !ContentReferenceSelection.IsCurrentValid(reference, reference, addedId, candidates),
            "An anatomy node was allowed to select itself as parent.");
        DrawProbe(window, selfParent, "self-parent rejection");

        SerializedProperty attachments = RequireProperty(serialized, "apparelAttachments");
        int attachmentIndex = attachments.arraySize++;
        SerializedProperty attachment = attachments.GetArrayElementAtIndex(attachmentIndex).FindPropertyRelative("nodeId");
        attachment.stringValue = addedId;
        candidates = ContentReferenceSelection.Entries(reference, attachment);
        Require(candidates.Any(value => value.Id == addedId)
                && ContentReferenceSelection.IsCurrentValid(reference, reference, addedId, candidates),
            "A normal apparel attachment lost its pending node reference.");
        DrawProbe(window, attachment, "pending anatomy attachment");

        nodes.GetArrayElementAtIndex(addedIndex).FindPropertyRelative("nodeId").stringValue = renamedId;
        candidates = ContentReferenceSelection.Entries(reference, parent);
        Require(candidates.Any(value => value.Id == renamedId)
                && candidates.All(value => value.Id != addedId)
                && !ContentReferenceSelection.IsCurrentValid(reference, reference, parent.stringValue, candidates),
            "Pending anatomy rename retained the old candidate or hid the new one.");
        var options = ContentReferenceSelection.PopupOptions(reference, reference, candidates);
        ContentReferenceSelection.WriteId(parent, reference.Kind, options.Single(value => value.Id == renamedId).Id);
        Require(parent.stringValue == renamedId, "Anatomy popup selection did not write its pending ID.");
        DrawProbe(window, parent, "renamed anatomy candidate");
        int blankIndex = nodes.arraySize++;
        nodes.GetArrayElementAtIndex(blankIndex).FindPropertyRelative("nodeId").stringValue = string.Empty;
        candidates = ContentReferenceSelection.Entries(reference, parent);
        Require(candidates.All(value => value.Id.Length > 0)
                && ContentReferenceSelection.PopupOptions(reference, reference, candidates).Count(value => value.Id.Length == 0) == 1,
            "A blank pending anatomy row became an extra None option.");
        nodes.DeleteArrayElementAtIndex(addedIndex);
        candidates = ContentReferenceSelection.Entries(reference, parent);
        Require(candidates.All(value => value.Id != renamedId)
                && !ContentReferenceSelection.IsCurrentValid(reference, reference, parent.stringValue, candidates),
            "A deleted pending anatomy node remained selectable.");
        DrawProbe(window, parent, "deleted anatomy candidate");
        Require(parent.stringValue == renamedId && EditorJsonUtility.ToJson(anatomy) == before,
            "Anatomy selection cleared an invalid ID or applied pending changes.");
        serialized.Update();

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        RequireProperty(serialized, "nodes.Array.data[0].nodeId").stringValue = renamedId;
        Require(serialized.ApplyModifiedProperties(), "Anatomy Undo probe did not change its temporary clone.");
        Undo.CollapseUndoOperations(undoGroup);
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        serialized.Update();
        parent = RequireProperty(serialized, "nodes.Array.data[1].parentNodeId");
        candidates = ContentReferenceSelection.Entries(reference, parent);
        Require(candidates.Any(value => value.Id == originalId)
                && candidates.All(value => value.Id != renamedId)
                && EditorJsonUtility.ToJson(anatomy) == before,
            "Undo did not restore anatomy candidates and clone data.");
        DrawProbe(window, parent, "anatomy after Undo");
        VerifyMixedLocalReference(window, anatomy, reference, parent.propertyPath);
    }

    private static void VerifyPendingBossAbilities(ContentReferenceInspectorProbeWindow window, EnemyArchetypeDefinitionSO enemy)
    {
        string before = EditorJsonUtility.ToJson(enemy);
        string previous = enemy.abilityIds[0];
        ContentReferenceAttribute reference = new(ContentReferenceKind.EnemyAbility);
        ContentReferenceSelection.Entry alternate = ContentReferenceSelection.Entries(reference)
            .First(value => !enemy.abilityIds.Contains(value.Id));
        using SerializedObject serialized = new(enemy);
        SerializedProperty owned = RequireProperty(serialized, "abilityIds");
        owned.arraySize = 1;
        owned.GetArrayElementAtIndex(0).stringValue = alternate.Id;
        SerializedProperty phases = RequireProperty(serialized, "bossPhases");
        phases.arraySize = Math.Max(1, phases.arraySize);
        SerializedProperty phaseAbilities = phases.GetArrayElementAtIndex(0).FindPropertyRelative("abilityIds");
        phaseAbilities.arraySize = 1;
        SerializedProperty selected = phaseAbilities.GetArrayElementAtIndex(0);
        selected.stringValue = previous;
        var candidates = ContentReferenceSelection.Entries(reference, selected);
        Require(candidates.Count == 1 && candidates[0].Id == alternate.Id
                && !ContentReferenceSelection.IsCurrentValid(reference, reference, previous, candidates),
            "Boss phase selection did not use the pending owner ability list.");
        Require(ContentReferenceSelection.TryAssign(selected, reference, alternate.Asset, out string selectionError),
            "Pending boss ability assignment failed: " + selectionError);
        Require(selected.stringValue == alternate.Id
                && ContentReferenceSelection.IsCurrentValid(reference, reference, selected.stringValue, candidates),
            "Boss selection and the drawer's valid-state calculation disagree.");
        DrawProbe(window, selected, "pending boss ability replacement");
        owned.arraySize = 0;
        candidates = ContentReferenceSelection.Entries(reference, selected);
        Require(candidates.Count == 0
                && !ContentReferenceSelection.TryAssign(selected, reference, alternate.Asset, out _)
                && selected.stringValue == alternate.Id,
            "A removed owner ability remained selectable or rejection cleared the old ID.");
        DrawProbe(window, selected, "pending boss ability deletion");
        owned.arraySize = 1;
        owned.GetArrayElementAtIndex(0).stringValue = string.Empty;
        Require(ContentReferenceSelection.Entries(reference, selected).Count == 0,
            "A blank owner ability produced a boss phase candidate.");
        Require(EditorJsonUtility.ToJson(enemy) == before, "Boss selection applied pending data to its clone.");
        serialized.Update();

        using SerializedObject mixedSetup = new(enemy);
        phases = RequireProperty(mixedSetup, "bossPhases");
        phases.arraySize = Math.Max(1, phases.arraySize);
        phaseAbilities = phases.GetArrayElementAtIndex(0).FindPropertyRelative("abilityIds");
        phaseAbilities.arraySize = Math.Max(1, phaseAbilities.arraySize);
        // Mixed-object queries require an existing common property on both temporary objects.
        mixedSetup.ApplyModifiedPropertiesWithoutUndo();
        VerifyMixedLocalReference(window, enemy, reference, "bossPhases.Array.data[0].abilityIds.Array.data[0]");
        EditorJsonUtility.FromJsonOverwrite(before, enemy);
        Require(EditorJsonUtility.ToJson(enemy) == before, "Boss mixed-query fixture did not restore its clone.");
    }

    private static void VerifyMixedLocalReference(ContentReferenceInspectorProbeWindow window,
        ScriptableObject first, ContentReferenceAttribute reference, string propertyPath)
    {
        ScriptableObject second = Object.Instantiate(first);
        second.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            using SerializedObject mixed = new(new Object[] { first, second });
            SerializedProperty property = RequireProperty(mixed, propertyPath);
            string previous = property.stringValue;
            Require(ContentReferenceSelection.HasMixedContext(reference, property)
                    && ContentReferenceSelection.Entries(reference, property).Count == 0
                    && !ContentReferenceSelection.TryAssign(property, reference, null, out _)
                    && property.stringValue == previous,
                "Mixed local owners were not blocked.");
            DrawProbe(window, property, "mixed local owner");
        }
        finally { Object.DestroyImmediate(second); }
    }

    // Invoke only through the isolated batch-mode Inspector contract runner.
    public static void VerifyInspectorGUI()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException(
                "Owner-scoped Inspector verification requires isolated batch mode.");
        OffenseCampaignCatalogSO campaignSource =
            RequireAsset<OffenseCampaignCatalogSO>(CampaignPath);
        CharacterAcquiredTraitModuleSO acquiredSource = LoadAcquiredModules()
            .Single(value => value.DrawbackBindingIds.Count > 0);
        EquipmentEvolutionFormulaCatalogSO equipmentSource =
            RequireAsset<EquipmentEvolutionFormulaCatalogSO>(EquipmentCatalogPath);
        FacilityEvolutionRecipeSO facilitySource =
            RequireAsset<FacilityEvolutionRecipeSO>(FacilityRecipePath);
        ScriptableObject[] sources =
        {
            campaignSource, acquiredSource, equipmentSource, facilitySource
        };
        string[] sourceJson = sources.Select(value =>
            EditorJsonUtility.ToJson(value)).ToArray();
        bool[] sourceDirty = sources.Select(EditorUtility.IsDirty).ToArray();
        OffenseCampaignCatalogSO campaign = Object.Instantiate(campaignSource);
        CharacterAcquiredTraitModuleSO acquired = Object.Instantiate(acquiredSource);
        EquipmentEvolutionFormulaCatalogSO equipment =
            Object.Instantiate(equipmentSource);
        FacilityEvolutionRecipeSO facility = Object.Instantiate(facilitySource);
        ScriptableObject[] clones = { campaign, acquired, equipment, facility };
        ContentReferenceInspectorProbeWindow window =
            ScriptableObject.CreateInstance<ContentReferenceInspectorProbeWindow>();
        try
        {
            foreach (ScriptableObject clone in clones)
                clone.hideFlags = HideFlags.HideAndDontSave;
            string[] cloneJson = clones.Select(value =>
                EditorJsonUtility.ToJson(value)).ToArray();
            ContentReferenceSelection.Invalidate();
            window.position = new Rect(0, 0, 680, 220);
            window.ShowUtility();

            using (SerializedObject serialized = new(campaign))
            {
                SerializedProperty targets = RequireProperty(serialized, "targets");
                int appliedCount = campaign.Targets.Count;
                targets.arraySize++;
                SerializedProperty row = targets.GetArrayElementAtIndex(
                    targets.arraySize - 1);
                row.FindPropertyRelative("id").stringValue =
                    "fixture_inspector_pending_target";
                row.FindPropertyRelative("revealsTruth").boolValue = false;
                SerializedProperty prerequisite = row.FindPropertyRelative(
                    "prerequisiteTargetId");
                prerequisite.stringValue = campaign.Targets[0].id;
                DrawProbe(window, prerequisite,
                    "pending campaign target prerequisite");
                Require(campaign.Targets.Count == appliedCount,
                    "Drawing a campaign prerequisite applied its pending row.");
            }
            using (SerializedObject serialized = new(acquired))
            {
                SerializedProperty binding = RequireProperty(serialized,
                    "drawbackBindingIds.Array.data[0]");
                DrawProbe(window, binding, "acquired module drawback binding");
            }
            using (SerializedObject serialized = new(acquired))
            {
                SerializedProperty relations = RequireProperty(serialized,
                    "formula.forbiddenSynergies");
                int appliedCount = acquired.Formula.forbiddenSynergies.Count;
                relations.arraySize++;
                SerializedProperty pending = relations.GetArrayElementAtIndex(
                    relations.arraySize - 1);
                pending.stringValue = string.Empty;
                DrawProbe(window, pending, "acquired formula pending capability");
                Require(acquired.Formula.forbiddenSynergies.Count == appliedCount,
                    "Drawing an acquired formula relation applied its pending row.");
            }
            using (SerializedObject serialized = new(equipment))
            {
                SerializedProperty relations = RequireProperty(serialized,
                    "capabilities.Array.data[0].forbiddenSynergies");
                int appliedCount = equipment.capabilities[0]
                    .forbiddenSynergies.Count;
                relations.arraySize++;
                SerializedProperty pending = relations.GetArrayElementAtIndex(
                    relations.arraySize - 1);
                pending.stringValue = string.Empty;
                DrawProbe(window, pending, "equipment formula pending capability");
                Require(equipment.capabilities[0].forbiddenSynergies.Count
                        == appliedCount,
                    "Drawing an equipment relation applied its pending row.");
            }
            using (SerializedObject serialized = new(facility))
            {
                SerializedProperty relations = RequireProperty(serialized,
                    "formulaCapabilities.Array.data[0].forbiddenSynergies");
                int appliedCount = facility.formulaCapabilities[0]
                    .forbiddenSynergies.Count;
                relations.arraySize++;
                SerializedProperty pending = relations.GetArrayElementAtIndex(
                    relations.arraySize - 1);
                pending.stringValue = string.Empty;
                DrawProbe(window, pending, "facility formula pending capability");
                Require(facility.formulaCapabilities[0].forbiddenSynergies.Count
                        == appliedCount,
                    "Drawing a facility relation applied its pending row.");
            }

            Require(window.DrawCount >= 5,
                "Owner-scoped PropertyDrawer GUI events did not execute.");
            for (int index = 0; index < sources.Length; index++)
            {
                Require(EditorJsonUtility.ToJson(sources[index]) == sourceJson[index],
                    $"Inspector verification changed source asset '{sources[index].name}'.");
                Require(EditorUtility.IsDirty(sources[index]) == sourceDirty[index],
                    $"Inspector verification changed dirty state for '{sources[index].name}'.");
                Require(EditorJsonUtility.ToJson(clones[index]) == cloneJson[index],
                    $"Inspector drawing applied pending data to clone '{clones[index].name}'.");
            }
            Debug.Log("OWNER_SCOPED_CONTENT_REFERENCE_INSPECTOR=PASS; campaign-target; acquired-binding; acquired-formula; equipment-formula; facility-formula; unchanged-source");
        }
        finally
        {
            window.Property = null;
            window.Close();
            if (window != null) Object.DestroyImmediate(window);
            foreach (ScriptableObject clone in clones)
            {
                Undo.ClearUndo(clone);
                Object.DestroyImmediate(clone);
            }
        }
    }

    private static void VerifyCampaignTargets(ContentReferenceCatalog catalog)
    {
        OffenseCampaignCatalogSO source = RequireAsset<OffenseCampaignCatalogSO>(
            CampaignPath);
        OffenseCampaignCatalogSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(source);
        ContentReferenceAttribute reference = new(
            ContentReferenceKind.CampaignTarget);
        try
        {
            RequireClean(clone.ValidateDefinition(), "Campaign baseline");
            string[] authoredIds = clone.Targets.Select(value => value.id).ToArray();
            RequireSameIds(
                catalog.Entries(reference, clone).Select(value => value.Id),
                authoredIds,
                "Campaign direct owner entries");

            OffenseCampaignCatalogSO second = Object.Instantiate(source);
            try
            {
                using SerializedObject mixed = new(new Object[] { clone, second });
                SerializedProperty mixedPrerequisite = RequireProperty(mixed,
                    "targets.Array.data[1].prerequisiteTargetId");
                Require(ContentReferenceSelection.HasMixedContext(
                            reference, mixedPrerequisite)
                        && ContentReferenceSelection.Entries(
                            reference, mixedPrerequisite).Count == 0,
                    "A multi-root campaign selection did not require individual editing.");
            }
            finally
            {
                Object.DestroyImmediate(second);
            }

            using (SerializedObject serialized = new(clone))
            {
                SerializedProperty targets = RequireProperty(serialized, "targets");
                int appliedCount = clone.Targets.Count;
                int index = targets.arraySize;
                targets.arraySize++;
                SerializedProperty row = targets.GetArrayElementAtIndex(index);
                const string pendingId = "fixture_pending_campaign_target";
                row.FindPropertyRelative("id").stringValue = pendingId;
                row.FindPropertyRelative("title").stringValue = "Pending target";
                row.FindPropertyRelative("campaignOrder").intValue =
                    authoredIds.Length + 1;
                row.FindPropertyRelative("revealsTruth").boolValue = false;
                SerializedProperty prerequisite = row.FindPropertyRelative(
                    "prerequisiteTargetId");
                prerequisite.stringValue = authoredIds[0];
                string[] pendingEntries = ContentReferenceSelection
                    .Entries(reference, prerequisite)
                    .Select(value => value.Id).ToArray();
                Require(pendingEntries.Contains(pendingId, StringComparer.Ordinal)
                        && authoredIds.All(value => pendingEntries.Contains(
                            value, StringComparer.Ordinal)),
                    "A pending campaign row was absent from its owner-scoped dropdown: "
                    + string.Join(", ", pendingEntries));
                Require(clone.Targets.Count == appliedCount,
                    "Reading campaign dropdown entries applied the pending row.");
                serialized.Update();
            }

            SetCampaignPrerequisite(clone, 0, "fixture_missing_campaign_target");
            IReadOnlyList<string> unknownErrors = clone.ValidateDefinition();
            Require(unknownErrors.Any(value => value.Contains(
                        "fixture_missing_campaign_target", StringComparison.Ordinal)),
                "An unknown campaign prerequisite was not reported structurally.");
            Require(catalog.Validate(new ScriptableObject[] { clone }).Any(value =>
                    value.Contains("fixture_missing_campaign_target",
                        StringComparison.Ordinal)),
                "The root reference validator accepted an unknown campaign prerequisite.");

            Restore(source, clone, baseline);
            SetCampaignPrerequisite(clone, 0, clone.Targets[0].id);
            Require(clone.ValidateDefinition().Any(value => value.Contains(
                        "requires itself", StringComparison.Ordinal)),
                "A self-referencing campaign prerequisite was accepted.");

            Restore(source, clone, baseline);
            string firstId = clone.Targets[0].id;
            string secondId = clone.Targets[1].id;
            SetCampaignPrerequisites(clone, (0, secondId), (1, firstId));
            Require(clone.ValidateDefinition().Any(value => value.Contains(
                        "cycle", StringComparison.OrdinalIgnoreCase)),
                "A campaign prerequisite cycle was accepted.");

            Restore(source, clone, baseline);
            RequireClean(clone.ValidateDefinition(), "Campaign repair");
            RequireClean(clone.ValidateDefinition(), "Campaign repair retry");
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "Campaign owner-scoped repair registration");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyAcquiredTraitBindings(ContentReferenceCatalog catalog)
    {
        CharacterAcquiredTraitModuleSO[] sources = LoadAcquiredModules();
        CharacterAcquiredTraitModuleSO source = sources.Single(value =>
            value.DrawbackBindingIds.Count > 0);
        CharacterAcquiredTraitModuleSO foreign = sources.First(value => value != source);
        CharacterAcquiredTraitModuleSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(source);
        ContentReferenceAttribute reference = new(
            ContentReferenceKind.AcquiredTraitBinding);
        try
        {
            RequireClean(clone.ValidateDefinition(), "Acquired module baseline");
            string[] ownIds = clone.Effects.Select(value => value.bindingId).ToArray();
            string foreignId = foreign.Effects.First().bindingId;
            RequireSameIds(
                catalog.Entries(reference, clone).Select(value => value.Id),
                ownIds,
                "Acquired binding direct owner entries");

            using (SerializedObject serialized = new(clone))
            {
                SerializedProperty bindingIds = RequireProperty(
                    serialized, "drawbackBindingIds");
                SerializedProperty binding = bindingIds.GetArrayElementAtIndex(0);
                string[] selectionIds = ContentReferenceSelection
                    .Entries(reference, binding)
                    .Select(value => value.Id).ToArray();
                RequireSameIds(selectionIds, ownIds,
                    "Acquired binding serialized owner entries");
                Require(!selectionIds.Contains(foreignId, StringComparer.Ordinal),
                    "An acquired module dropdown leaked another module's binding.");
            }

            const string unknown = "acquired-trait:module:fixture:effect:missing";
            SetSingleStringList(clone, "drawbackBindingIds", unknown);
            IReadOnlyList<string> errors = clone.ValidateDefinition();
            Require(errors.Any(value => value.Contains(
                        "invalid drawback binding", StringComparison.OrdinalIgnoreCase)),
                "An unknown drawback binding did not return a structured definition error.");
            Require(catalog.Validate(new ScriptableObject[] { clone }).Any(value =>
                    value.Contains(unknown, StringComparison.Ordinal)
                    || value.Contains("invalid drawback binding",
                        StringComparison.OrdinalIgnoreCase)),
                "The root reference validator accepted an unknown acquired binding.");

            Restore(source, clone, baseline);
            RequireClean(clone.ValidateDefinition(), "Acquired binding repair");
            RequireClean(clone.ValidateDefinition(), "Acquired binding repair retry");
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "Acquired binding owner-scoped repair registration");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyAcquiredFormulaCapabilities(
        ContentReferenceCatalog catalog)
    {
        CharacterAcquiredTraitModuleSO[] sources = LoadAcquiredModules();
        CharacterAcquiredTraitModuleSO[] modules = sources
            .Select(value => Object.Instantiate(value)).ToArray();
        CharacterAcquiredTraitSettingsSO settingsSource =
            RequireAsset<CharacterAcquiredTraitSettingsSO>(AcquiredSettingsPath);
        CharacterAcquiredTraitSettingsSO settings = Object.Instantiate(settingsSource);
        CharacterSkillSystemSettingsSO skillSettings =
            RequireAsset<CharacterSkillSystemSettingsSO>(SkillSettingsPath);
        ContentReferenceAttribute reference = new(
            ContentReferenceKind.SkillCapability);
        try
        {
            settings.RequireFormulaRelations(modules);
            string[] acquiredIds = modules.Select(value => value.CapabilityId)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string skillId = CharacterSkillModuleCapabilityRegistry.Require(
                skillSettings.Modules.First(value => value != null)).CapabilityId;

            RequireSameIds(
                catalog.Entries(reference, modules[0]).Select(value => value.Id),
                acquiredIds,
                "Acquired benefit direct capability entries");
            RequireSameIds(
                catalog.Entries(reference, settings).Select(value => value.Id),
                acquiredIds,
                "Acquired drawback direct capability entries");
            VerifyPendingCapabilityEntries(
                modules[0],
                "formula.forbiddenSynergies",
                reference,
                acquiredIds,
                skillId,
                "Acquired benefit capability dropdown");
            VerifyPendingCapabilityEntries(
                settings,
                "drawbackCapabilities.Array.data[0].formula.forbiddenSynergies",
                reference,
                acquiredIds,
                skillId,
                "Acquired drawback capability dropdown");

            const string typo = "acquired-trait:capability:fixture-typo";
            SetSingleStringList(modules[0],
                "formula.forbiddenSynergies", typo);
            ExpectInvalidOperation(
                () => settings.RequireFormulaRelations(modules),
                typo,
                "An acquired benefit formula accepted an unknown capability.");
            SetSingleStringList(modules[0],
                "formula.forbiddenSynergies", modules[1].CapabilityId);
            settings.RequireFormulaRelations(modules);

            SetSingleStringList(settings,
                "drawbackCapabilities.Array.data[0].formula.forbiddenSynergies",
                typo);
            ExpectInvalidOperation(
                () => settings.RequireFormulaRelations(modules),
                typo,
                "An acquired drawback formula accepted an unknown capability.");
            SetSingleStringList(settings,
                "drawbackCapabilities.Array.data[0].formula.forbiddenSynergies",
                modules[1].CapabilityId);
            settings.RequireFormulaRelations(modules);
            settings.RequireFormulaRelations(modules);
        }
        finally
        {
            Object.DestroyImmediate(settings);
            foreach (CharacterAcquiredTraitModuleSO module in modules)
                Object.DestroyImmediate(module);
        }
    }

    private static void VerifyEvolutionFormulaCapabilities(
        ContentReferenceCatalog catalog)
    {
        EquipmentEvolutionFormulaCatalogSO equipmentSource =
            RequireAsset<EquipmentEvolutionFormulaCatalogSO>(EquipmentCatalogPath);
        FacilityEvolutionRecipeSO facilitySource =
            RequireAsset<FacilityEvolutionRecipeSO>(FacilityRecipePath);
        EquipmentEvolutionFormulaCatalogSO equipment =
            Object.Instantiate(equipmentSource);
        FacilityEvolutionRecipeSO facility = Object.Instantiate(facilitySource);
        ContentReferenceAttribute equipmentReference = new(
            ContentReferenceKind.EquipmentCapability);
        ContentReferenceAttribute facilityReference = new(
            ContentReferenceKind.FacilityCapability);
        try
        {
            string[] equipmentIds = equipment.ReferenceCapabilityIds().ToArray();
            string[] facilityIds = facility.ReferenceCapabilityIds().ToArray();
            Require(equipmentIds.Length > 0 && facilityIds.Length > 0,
                "Evolution formula reference catalogs are empty.");
            RequireSameIds(
                catalog.Entries(equipmentReference, equipment)
                    .Select(value => value.Id),
                equipmentIds,
                "Equipment direct capability entries");
            RequireSameIds(
                catalog.Entries(facilityReference, facility)
                    .Select(value => value.Id),
                facilityIds,
                "Facility direct capability entries");

            VerifyPendingCapabilityEntries(
                equipment,
                "capabilities.Array.data[0].forbiddenSynergies",
                equipmentReference,
                equipmentIds,
                facilityIds[0],
                "Equipment owner capability dropdown");
            VerifyPendingCapabilityEntries(
                facility,
                "formulaCapabilities.Array.data[0].forbiddenSynergies",
                facilityReference,
                facilityIds,
                equipmentIds[0],
                "Facility owner capability dropdown");
        }
        finally
        {
            Object.DestroyImmediate(equipment);
            Object.DestroyImmediate(facility);
        }
    }

    private static void VerifyPendingCapabilityEntries(
        ScriptableObject root,
        string listPath,
        ContentReferenceAttribute reference,
        IEnumerable<string> expected,
        string forbiddenForeignId,
        string label)
    {
        using SerializedObject serialized = new(root);
        SerializedProperty list = RequireProperty(serialized, listPath);
        int appliedCount = list.arraySize;
        list.arraySize++;
        SerializedProperty pending = list.GetArrayElementAtIndex(appliedCount);
        pending.stringValue = string.Empty;
        string[] ids = ContentReferenceSelection.Entries(reference, pending)
            .Select(value => value.Id).ToArray();
        RequireSameIds(ids, expected, label);
        Require(!ids.Contains(forbiddenForeignId, StringComparer.Ordinal),
            label + " exposed a foreign capability ID.");
        Require(ReadAppliedArraySize(root, listPath) == appliedCount,
            label + " applied a pending relation row while reading entries.");
        serialized.Update();
    }

    private static void DrawProbe(
        ContentReferenceInspectorProbeWindow window,
        SerializedProperty property,
        string context)
    {
        window.Property = property;
        int previous = window.DrawCount;
        window.SendEvent(new Event { type = EventType.Layout });
        window.SendEvent(new Event { type = EventType.Repaint });
        if (window.Failure != null)
            throw new InvalidOperationException(context, window.Failure);
        Require(window.DrawCount > previous,
            "Inspector GUI did not execute: " + context);
    }

    private static int ReadAppliedArraySize(
        ScriptableObject root,
        string propertyPath)
    {
        using SerializedObject applied = new(root);
        return RequireProperty(applied, propertyPath).arraySize;
    }

    private static void SetCampaignPrerequisite(
        OffenseCampaignCatalogSO target,
        int index,
        string prerequisiteId) =>
        SetCampaignPrerequisites(target, (index, prerequisiteId));

    private static void SetCampaignPrerequisites(
        OffenseCampaignCatalogSO target,
        params (int Index, string Id)[] values)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty targets = RequireProperty(serialized, "targets");
        foreach ((int index, string id) in values)
            targets.GetArrayElementAtIndex(index)
                .FindPropertyRelative("prerequisiteTargetId").stringValue = id;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSingleStringList(
        ScriptableObject target,
        string propertyPath,
        string value)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty list = RequireProperty(serialized, propertyPath);
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Restore<T>(T source, T clone, string json)
        where T : ScriptableObject
    {
        EditorJsonUtility.FromJsonOverwrite(json, clone);
        Require(EditorJsonUtility.ToJson(source) == EditorJsonUtility.ToJson(clone),
            typeof(T).Name + " JSON restoration changed the cloned definition.");
    }

    private static void ExpectInvalidOperation(
        Action action,
        string expectedText,
        string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(expectedText,
                    StringComparison.Ordinal),
                message + " Unexpected error: " + exception.Message);
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static CharacterAcquiredTraitModuleSO[] LoadAcquiredModules() =>
        AssetDatabase.FindAssets(
                "t:CharacterAcquiredTraitModuleSO",
                new[] { "Assets/Resources/SO/V25/AcquiredTraits/Modules" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterAcquiredTraitModuleSO>)
            .Where(value => value != null)
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();

    private static T RequireAsset<T>(string path) where T : Object
    {
        T value = AssetDatabase.LoadAssetAtPath<T>(path);
        return value != null
            ? value
            : throw new InvalidOperationException(
                $"Required fixture asset '{path}' is missing.");
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string path) =>
        serialized.FindProperty(path)
        ?? throw new InvalidOperationException(
            $"Missing serialized fixture property '{path}'.");

    private static void RequireSameIds(
        IEnumerable<string> actual,
        IEnumerable<string> expected,
        string label)
    {
        string[] actualIds = actual.OrderBy(value => value,
            StringComparer.Ordinal).ToArray();
        string[] expectedIds = expected.OrderBy(value => value,
            StringComparer.Ordinal).ToArray();
        Require(actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal),
            label + " mismatch. expected=" + string.Join(",", expectedIds)
            + "; actual=" + string.Join(",", actualIds));
    }

    private static void RequireClean(
        IReadOnlyList<string> errors,
        string label) =>
        Require(errors.Count == 0,
            label + ":\n" + string.Join("\n", errors));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
