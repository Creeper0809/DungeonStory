using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ContentReferenceIntegrityDebugScenarios
{
    public static void RunAll()
    {
        string folder = "Artifacts/QA/ContentReferenceIntegrity";
        Directory.CreateDirectory(folder);
        var identities = ContentIdentityProtection.CaptureCurrent();
        File.WriteAllText(folder + "/identities-candidate.json", ContentIdentityProtection.ToJson(identities));
        var gameTypes = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
            .Where(type => !type.Assembly.GetName().Name.EndsWith("Editor", StringComparison.Ordinal)
                && (type.Assembly.GetName().Name == "Assembly-CSharp"
                    || type.Assembly.GetName().Name.StartsWith("DungeonStory.", StringComparison.Ordinal)))
            .Select(type => type.FullName).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        File.WriteAllLines(folder + "/runtime-so-types.txt", gameTypes);
        Require(gameTypes.Length > 0 && identities.assets.Count > 0, "Empty native SO inventory.");
        if (File.Exists(ContentIdentityProtection.BaselinePath))
            RequireClean(ContentIdentityProtection.ValidateAll(), "Published identities");
        VerifyIdentityRejection(identities);
        RequireClean(BatchAArchitectureMetricsValidator.FindBrokenScriptableObjectReferences(
            identities.assets.Select(value => AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(value.guid)))),
            "All authored SO object references");
        VerifyDefinitionMetadata();

        var root = AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>("Assets/Resources/SO/GameContentCatalog.asset");
        var validationTimer = System.Diagnostics.Stopwatch.StartNew();
        var catalog = ContentReferenceSelection.CreateCatalog(root);
        RequireClean(catalog.Validate(new ScriptableObject[] { root }), "Registered content relations");
        Debug.Log($"SO_REFERENCE_ROOT_VALIDATION_MS={validationTimer.ElapsedMilliseconds}");
        RequireClean(catalog.Validate(identities.assets.Select(value =>
            AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(value.guid)))),
            "All authored SO ID references");
        var definitions = root.DomainCatalogs.OfType<GameDomainContentCatalogSO>().SelectMany(value => value.Definitions).ToArray();
        VerifyAnatomyRestore(definitions);
        string retired = Wim037GuestRequestCatalogExclusionDebugScenarios.RunAll();
        Require(retired.Contains("RESULT=PASS") && !retired.Contains("=FAIL"), retired);
        // Representative failure boundaries: real authoring fields, not a duplicate field map.
        VerifyInvalidAndRestore(catalog, definitions.OfType<EndingDefinitionSO>().First(), "landmarkBuildingId", "building:missing");
        var recipe = definitions.OfType<ProductionRecipeSO>().First(value => value.Inputs.Count > 0 && value.Outputs.Count > 0);
        VerifyInvalidAndRestore(catalog, recipe, "inputs.Array.data[0].itemId", "item:missing");
        VerifyInvalidAndRestore(catalog, recipe, "outputs.Array.data[0].itemId", "item:missing");
        VerifyInvalidAndRestore(catalog, recipe, "proficiency.primaryProficiencyId", "proficiency:missing");
        VerifyInvalidAndRestore(catalog, recipe, "spoilageItemId", "item:missing");
        var animal = definitions.OfType<WildlifeSpeciesSO>().First(value => value.PreySpeciesIds.Count > 0);
        VerifyInvalidAndRestore(catalog, animal, "preySpeciesIds.Array.data[0]", "wildlife:missing");
        VerifyInvalidListAndRestore(catalog, animal, "diseaseVectorIds", "disease:missing");
        var research = definitions.OfType<ResearchProjectSO>().First(value => value.Unlocks.OfType<BlueprintBuildingUnlock>().Any());
        VerifyResearchTarget(catalog, research);
        VerifyBossSubset(catalog, definitions);
        var seasonal = definitions.OfType<SeasonalWorldEventDefinitionSO>().First(value => value.manaLightningProfile.eligibleBuildingDefinitionIds.Count > 0);
        VerifySeasonalTarget(catalog, seasonal);
        VerifyGenericSelection(catalog, animal);
        Debug.Log($"CONTENT_REFERENCE_INTEGRITY=PASS; runtimeSoTypes={gameTypes.Length}; publishedAssets={identities.assets.Count}; invalid-target-before-play; JSON-restore; retry; immutable-ID/delete rejection");
    }

    private static void VerifyAnatomyRestore(ScriptableObject[] definitions)
    {
        var source = definitions.OfType<AnatomyProfileSO>().Single(value => value.ProfileId == "anatomy:quadruped");
        var restored = ScriptableObject.CreateInstance<AnatomyProfileSO>();
        try
        {
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), restored);
            var catalog = new ResourceAnatomyProfileCatalog(new[] { restored });
            Require(catalog.TryGet(source.ProfileId, out var profile) && profile.ProfileId == source.ProfileId,
                "Existing anatomy save profile ID could not be restored.");
            foreach (string species in source.SpeciesIds)
                Require(catalog.GetForSpecies(species).ProfileId == source.ProfileId,
                    "Registered wildlife anatomy mapping changed after restore.");
            RequireClean(catalog.Validate(), "Restored anatomy nodes");
        }
        finally { Object.DestroyImmediate(restored); }
    }

    private static void VerifyDefinitionMetadata()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(value => value.GetName().Name == "Assembly-CSharp"
            || value.GetName().Name.StartsWith("DungeonStory.", StringComparison.Ordinal)))
        foreach (var type in assembly.GetTypes())
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            var reference = field.GetCustomAttribute<ContentReferenceAttribute>();
            if (reference?.Kind != ContentReferenceKind.Definition) continue;
            Type target = ContentReferenceCatalog.ResolveType(reference.DefinitionTypeName);
            Require(typeof(ScriptableObject).IsAssignableFrom(target), "Generic reference target is not an SO.");
            bool found = false;
            for (Type current = target; current != null; current = current.BaseType)
                found |= current.GetMember(reference.IdMemberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Length > 0;
            Require(found, $"Unknown identity member {reference.DefinitionTypeName}.{reference.IdMemberName} on {type.Name}.{field.Name}");
        }
    }

    private static void VerifySeasonalTarget(ContentReferenceCatalog catalog, SeasonalWorldEventDefinitionSO source)
    {
        var clone = Object.Instantiate(source);
        try
        {
            clone.manaLightningProfile.eligibleBuildingDefinitionIds[0] = int.MaxValue;
            Require(catalog.Validate(new[] { clone }).Any(error => error.Contains(int.MaxValue.ToString())), "Unknown seasonal lightning target accepted.");
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static void VerifyIdentityRejection(ContentIdentityProtection.Snapshot snapshot)
    {
        RequireClean(ContentIdentityProtection.Validate(snapshot, snapshot), "Identity unchanged");
        string json = ContentIdentityProtection.ToJson(snapshot);
        var changed = JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(json);
        changed.assets.First(value => value.fields.Count > 0).fields[0].value += "-changed";
        Require(ContentIdentityProtection.Validate(snapshot, changed).Count > 0, "Published ID change was accepted.");
        changed = JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(json);
        changed.assets.RemoveAt(0);
        Require(ContentIdentityProtection.Validate(snapshot, changed).Count > 0, "Published asset deletion was accepted.");
        var restored = JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(json);
        RequireClean(ContentIdentityProtection.Validate(snapshot, restored), "Identity JSON restore and retry");
        VerifyNestedIdentityEdits(snapshot);
        VerifyNewPublication(snapshot);
        foreach (string prefix in new[] { "@registration:", "@resource-path" })
        {
            changed = JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(json);
            var owner = changed.assets.First(asset => asset.fields.Any(field => field.path.StartsWith(prefix, StringComparison.Ordinal)));
            owner.fields.RemoveAll(field => field.path.StartsWith(prefix, StringComparison.Ordinal));
            Require(ContentIdentityProtection.Validate(snapshot, changed).Count > 0,
                "Published registry/path protection was removed: " + prefix);
        }
        Require(ContentIdentityProtection.IsRootMove("Assets/Resources/SO", "Assets/Elsewhere")
            && ContentIdentityProtection.IsRootMove(ContentIdentityProtection.RootAssetPath,
                "Assets/Resources/SO/Renamed.asset")
            && !ContentIdentityProtection.IsRootMove("Assets/Resources/SO/Medical", "Assets/Medical"),
            "Startup catalog move boundary is incorrect.");
    }

    private static void VerifyNestedIdentityEdits(ContentIdentityProtection.Snapshot snapshot)
    {
        foreach (var spec in new[]
                 {
                     ("AnatomyProfileSO", "nodes", "nodeId"),
                     ("OffenseDecisionCardSO", "choices", "choiceId"),
                     ("ProductionRecipeSO", "outputs", "outputLineId"),
                     ("CharacterSkillSystemSettingsSO", "modules", "id"),
                     ("CharacterAcquiredTraitSettingsSO", "drawbackCapabilities", "drawbackId"),
                     ("EquipmentEvolutionFormulaCatalogSO", "capabilities", "capabilityId")
                 })
        {
            var published = snapshot.assets.First(asset => asset.typeName == spec.Item1
                && asset.fields.Any(field => field.path.StartsWith("@nested:", StringComparison.Ordinal)));
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(published.guid));
            var clone = Object.Instantiate(source);
            try
            {
                var baseline = new ContentIdentityProtection.Snapshot { assets = new() { published } };
                using var serialized = new SerializedObject(clone);
                var list = serialized.FindProperty(spec.Item2);
                Require(list.arraySize > 0, "Missing nested identity test input.");
                if (list.arraySize > 1)
                {
                    list.MoveArrayElement(0, list.arraySize - 1);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    RequireClean(ContentIdentityProtection.Validate(baseline, new ContentIdentityProtection.Snapshot
                        { assets = new() { ContentIdentityProtection.CaptureAsset(published.guid, clone) } }),
                        "Nested identity reorder " + spec.Item1);
                }
                serialized.Update();
                list = serialized.FindProperty(spec.Item2);
                SerializedProperty identity = list.GetArrayElementAtIndex(0).FindPropertyRelative(spec.Item3);
                string originalId = identity.stringValue;
                identity.stringValue += ":changed";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ContentIdentityProtection.Snapshot renamed;
                if (clone is EquipmentEvolutionFormulaCatalogSO)
                {
                    bool rejectedBeforeCapture = false;
                    try { ContentIdentityProtection.CaptureAsset(published.guid, clone); }
                    catch (InvalidOperationException error) when (error.Message ==
                        "Every equipment formula capability must be included in its catalog module authority.")
                    { rejectedBeforeCapture = true; }
                    Require(rejectedBeforeCapture, "Unregistered equipment capability reached identity capture.");
                    // 원본 캡처의 참조 거절과 게시 레코드 자체의 ID 변경 거절을 각각 확인한다.
                    renamed = JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(
                        ContentIdentityProtection.ToJson(baseline));
                    var field = renamed.assets.Single().fields.Single(value =>
                        value.path.StartsWith("@nested:capabilities[", StringComparison.Ordinal)
                        && value.path.EndsWith(".capabilityId", StringComparison.Ordinal) && value.value == originalId);
                    field.value += ":changed";
                }
                else renamed = new ContentIdentityProtection.Snapshot
                    { assets = new() { ContentIdentityProtection.CaptureAsset(published.guid, clone) } };
                Require(ContentIdentityProtection.Validate(baseline, renamed).Count > 0,
                    "Nested saved identity rename accepted: " + spec.Item1);
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), clone);
                RequireClean(ContentIdentityProtection.Validate(baseline, new ContentIdentityProtection.Snapshot
                    { assets = new() { ContentIdentityProtection.CaptureAsset(published.guid, clone) } }),
                    "Nested identity JSON restore/retry " + spec.Item1);
            }
            finally { Object.DestroyImmediate(clone); }
        }
    }

    private static void VerifyNewPublication(ContentIdentityProtection.Snapshot snapshot)
    {
        string json = ContentIdentityProtection.ToJson(snapshot);
        var candidate = snapshot.assets.First(asset => asset.typeName == nameof(AnatomyProfileSO)
            && asset.fields.Any(field => field.path == "profileId" && !string.IsNullOrWhiteSpace(field.value)));
        var beforePublication = JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(json);
        beforePublication.assets.RemoveAll(asset => asset.guid == candidate.guid);
        RequireClean(ContentIdentityProtection.ValidateNewPublication(beforePublication, snapshot),
            "Registered new definition publication");

        var draft = JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(json);
        foreach (var asset in draft.assets)
            asset.fields.RemoveAll(field => field.path.StartsWith("@registration:", StringComparison.Ordinal)
                && field.value == candidate.guid);
        Require(ContentIdentityProtection.ValidateNewPublication(beforePublication, draft)
            .Any(error => error.Contains("Unregistered draft")), "Unregistered draft publication accepted.");

        var blank = JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(json);
        blank.assets.Single(asset => asset.guid == candidate.guid).fields
            .Single(field => field.path == "profileId").value = string.Empty;
        Require(ContentIdentityProtection.ValidateNewPublication(beforePublication, blank)
            .Any(error => error.Contains("cannot be empty")), "Blank new identity publication accepted.");
        RequireClean(ContentIdentityProtection.ValidateNewPublication(beforePublication,
            JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(json)), "Publication repair and retry");
    }

    private static void VerifyInvalidAndRestore(ContentReferenceCatalog catalog, ScriptableObject source, string path, string invalid)
    {
        var clone = Object.Instantiate(source);
        try
        {
            string original = EditorJsonUtility.ToJson(clone);
            using (var serialized = new SerializedObject(clone))
            {
                var property = serialized.FindProperty(path);
                Require(property != null, $"Missing test field {source.GetType().Name}.{path}");
                property.stringValue = invalid;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Require(catalog.Validate(new[] { clone }).Any(error => error.Contains(invalid)), $"Invalid target accepted: {source.name}.{path}");
            // Restore into a fresh definition object, then retry the identical registration.
            var fresh = ScriptableObject.CreateInstance(source.GetType());
            try
            {
                EditorJsonUtility.FromJsonOverwrite(original, fresh);
                RequireClean(catalog.Validate(new[] { fresh }), "Reference JSON restore");
                RequireClean(catalog.Validate(new[] { fresh }), "Reference registration retry");
                Require(EditorJsonUtility.ToJson(fresh) == original, "Validation mutated serialized content.");
            }
            finally { Object.DestroyImmediate(fresh); }
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static void VerifyInvalidListAndRestore(ContentReferenceCatalog catalog, ScriptableObject source, string path, string invalid)
    {
        var clone = Object.Instantiate(source);
        try
        {
            using (var serialized = new SerializedObject(clone))
            {
                var list = serialized.FindProperty(path);
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).stringValue = invalid;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Require(catalog.Validate(new[] { clone }).Any(error => error.Contains(invalid)), "Invalid disease vector accepted.");
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static void VerifyResearchTarget(ContentReferenceCatalog catalog, ResearchProjectSO source)
    {
        var clone = Object.Instantiate(source);
        try
        {
            var unlock = clone.Unlocks.OfType<BlueprintBuildingUnlock>().First();
            int prior = unlock.buildingId;
            unlock.buildingId = int.MaxValue;
            Require(catalog.Validate(new[] { clone }).Any(error => error.Contains(int.MaxValue.ToString())), "Unknown research target accepted.");
            unlock.buildingId = prior;
            RequireClean(catalog.Validate(new[] { clone }), "Research repair retry");
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static void VerifyBossSubset(ContentReferenceCatalog catalog, ScriptableObject[] definitions)
    {
        var source = definitions.OfType<EnemyArchetypeDefinitionSO>().First(value => value.bossPhases.Count > 0);
        var clone = Object.Instantiate(source);
        try
        {
            string otherAbility = definitions.OfType<EnemyAbilityDefinitionSO>().First(value => !clone.abilityIds.Contains(value.stableId)).stableId;
            clone.bossPhases[0].abilityIds = new List<string> { otherAbility };
            Require(clone.ValidateDefinition().Count > 0, "Existing but unowned boss ability was accepted.");
            Require(catalog.Validate(new[] { clone }).Count > 0, "Root validator missed boss ownership.");
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static void VerifyGenericSelection(ContentReferenceCatalog catalog, WildlifeSpeciesSO source)
    {
        var reference = new ContentReferenceAttribute("WildlifeSpeciesSO", "SpeciesId");
        var clone = Object.Instantiate(source);
        try
        {
            using var serialized = new SerializedObject(clone);
            var list = serialized.FindProperty("preySpeciesIds");
            list.arraySize = 1;
            var property = list.GetArrayElementAtIndex(0);
            Require(ContentReferenceSelection.TryAssign(property, reference, source, out string error), error);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Require(clone.PreySpeciesIds[0] == source.SpeciesId, "Generic SO selection changed the serialized ID contract.");
            RequireClean(catalog.Validate(new[] { clone }), "Generic reference registration");
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static void RequireClean(IReadOnlyList<string> errors, string label) => Require(errors.Count == 0, label + ":\n" + string.Join("\n", errors));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
