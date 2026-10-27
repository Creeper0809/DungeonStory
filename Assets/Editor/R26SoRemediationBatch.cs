using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.ServiceRooms;
using UnityEditor;
using UnityEngine;

// Focused current-source regression entry. This does not certify all gameplay.
public static class R26SoRemediationBatch
{
    public static void RunFacilityMaterialRestartAndRetry() => Execute("facility-material-restart", () =>
        FacilityEvolutionDebugScenarios.RunMaterialPendingRestartAndRetryForBatch());

    public static void RunFacilityPendingPersistence() => Execute("facility-pending-persistence", () =>
    {
        Require(FacilityEvolutionDebugScenarios.VerifyExactNestedJsonRoundTrip(),
            "Facility pending/finalized exact JSON restore and material recovery failed.");
        Debug.Log("R26_FACILITY_PENDING_PERSISTENCE=PASS; pending/finalized JSON; invalid restore unchanged; material result roundtrip");
    });

    public static void CapturePublicationCandidate() => Execute("publication-candidate", () =>
    {
        string candidate = ContentIdentityProtection.CapturePublicationCandidateJson();
        WriteNew("published-identities-candidate.json", candidate);
        Debug.Log("R26_PUBLICATION_CANDIDATE=PASS; existing records preserved; review before publication");
    });

    public static void RunAll() => Execute("so-remediation", () =>
    {
        R26CatalogGuardDebugScenarios.RunAll();
        R26LocalKeyGuardDebugScenarios.Run();
        R26EffectAndSkillTableGuardDebugScenarios.Run();
        R26BuildingConnectionGuardDebugScenarios.Run();
        R26ConveyorDestinationAuthoringDebugScenarios.RunAll();
        R26ReferenceOwnerGuardDebugScenarios.RunAll();
        VerifyMixedLocalOwners();
        R20ProductionOutputCostDebugScenarios.RunAll();
        VerifySpawnerIndexAtomicity();
        VerifyServiceSupportContracts();
        VerifyPublicationGuards();
        VerifyInspectorGUI();
        ArchitectureRefactorVerificationBatch.RunEquipmentEvolution();
        FacilityEvolutionDebugScenarios.RunFormulaV3BurdenSeparationFromBatch();
        CharacterAcquiredTraitSpecialReactionDebugScenarios.RunCurrentCatalog();
        Require(CharacterProgressionDebugScenarios.RunAcquiredTraitAutomaticProducerResumeScenario(),
            "Acquired trait pending producer did not survive JSON restore and retry.");
        Debug.Log("R26_SO_REMEDIATION=PASS; controlled production contracts and JSON restore/retry; natural UI/AI not claimed");
    });

    private static void Execute(string name, Action action)
    {
        if (!Application.isBatchMode || Application.isPlaying || !DungeonPersistencePaths.IsIsolatedVerification)
            throw new InvalidOperationException("R26 requires isolated edit-mode persistence.");
        string before = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        var errors = new List<string>();
        void Observe(string text, string stack, LogType type)
        { if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(text); }
        Application.logMessageReceived += Observe;
        try
        {
            action();
            Require(errors.Count == 0, "Console errors: " + string.Join(" | ", errors));
            string after = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
            Require(before == after, "Source changed during verification.");
            WriteNew(name + ".txt", "result=PASS\nsourceBefore=" + before + "\nsourceAfter=" + after
                + "\nconsoleErrors=0\nboundary=controlled production contracts; no natural-play certification\n");
        }
        catch (Exception error)
        {
            WriteNew(name + "-failure.txt", "result=FAIL\nsource=" + before + "\n" + error);
            throw;
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    private static void VerifySpawnerIndexAtomicity()
    {
        var first = ScriptableObject.CreateInstance<CharacterSO>();
        var second = ScriptableObject.CreateInstance<CharacterSO>();
        try
        {
            first.id = 11; second.id = 11;
            bool catalogRejected = false;
            try { _ = new ResourceRunCharacterCatalog(new[] { first, second }); }
            catch (InvalidOperationException) { catalogRejected = true; }
            Require(catalogRejected, "The full run character catalog accepted a numeric ID collision.");
            var adapter = new CharacterSpawnerSceneApplicationAdapter();
            adapter.RebuildCharacterIndex(new[] { first, first });
            bool rejected = false;
            try { adapter.RebuildCharacterIndex(new[] { first, second }); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && adapter.TryGetCharacter(11, out var retained) && retained == first,
                "A rejected numeric owner collision changed the spawner index.");
            second.id = 12;
            Require(new ResourceRunCharacterCatalog(new[] { first, first, second }).Characters.Count == 2,
                "Corrected character catalog did not retain one owner per numeric ID.");
            adapter.RebuildCharacterIndex(new[] { first, second });
            Require(adapter.TryGetCharacter(12, out var retried) && retried == second,
                "Spawner index could not retry after correction.");
            foreach (string scene in new[] { "GameplayScene", "SampleScene" })
                Require(!File.ReadAllText("Assets/Scenes/" + scene + ".unity")
                    .Contains("e61f47e09c6cd9449aeeee72493b9fdc"), "Test character remains in " + scene);
        }
        finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
    }

    private static void VerifyMixedLocalOwners()
    {
        var anatomyA = ScriptableObject.CreateInstance<AnatomyProfileSO>();
        var anatomyB = ScriptableObject.CreateInstance<AnatomyProfileSO>();
        var enemyA = ScriptableObject.CreateInstance<EnemyArchetypeDefinitionSO>();
        var enemyB = ScriptableObject.CreateInstance<EnemyArchetypeDefinitionSO>();
        try
        {
            foreach (var anatomy in new[] { anatomyA, anatomyB })
            {
                using var source = new SerializedObject(anatomy);
                var nodes = source.FindProperty("nodes"); nodes.arraySize = 1;
                nodes.GetArrayElementAtIndex(0).FindPropertyRelative("nodeId").stringValue = "node:shared";
                nodes.GetArrayElementAtIndex(0).FindPropertyRelative("parentNodeId").stringValue = "node:parent";
                source.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var enemy in new[] { enemyA, enemyB })
                enemy.bossPhases.Add(new EnemyBossPhaseRecord { abilityIds = new List<string> { "ability:shared" } });
            Check(new UnityEngine.Object[] { anatomyA, anatomyB }, "nodes.Array.data[0].parentNodeId", ContentReferenceKind.AnatomyNode);
            Check(new UnityEngine.Object[] { enemyA, enemyB }, "bossPhases.Array.data[0].abilityIds.Array.data[0]", ContentReferenceKind.EnemyAbility);
        }
        finally
        {
            foreach (var asset in new ScriptableObject[] { anatomyA, anatomyB, enemyA, enemyB }) UnityEngine.Object.DestroyImmediate(asset);
        }

        static void Check(UnityEngine.Object[] owners, string path, ContentReferenceKind kind)
        {
            using var source = new SerializedObject(owners);
            SerializedProperty property = source.FindProperty(path);
            Require(property != null, "Missing mixed-owner field " + path);
            string before = property.stringValue;
            var reference = new ContentReferenceAttribute(kind);
            Require(ContentReferenceSelection.HasMixedContext(reference, property)
                && ContentReferenceSelection.Entries(reference, property).Count == 0
                && !ContentReferenceSelection.TryAssign(property, reference, null, out _)
                && property.stringValue == before,
                "A mixed local-owner selection exposed candidates or cleared its value: " + path);
        }
    }

    private static void VerifyServiceSupportContracts()
    {
        var process = ScriptableObject.CreateInstance<ServiceProcessSO>();
        try
        {
            // No dependency is read for ID projection; linked instance modifiers
            // remain the normal builder's responsibility and are not deduplicated.
            var runtime = (ServiceSessionRuntime)FormatterServices.GetUninitializedObject(typeof(ServiceSessionRuntime));
            MethodInfo builder = typeof(ServiceSessionRuntime).GetMethod("CreateContract", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (string[] ids in new[] { new[] { "support:r26" }, new[] { "support:r26", "support:r26" },
                         new[] { "support:r26-a", "support:r26-b" } })
            {
                var hub = new ServiceHubSnapshot { HubId = "hub:r26", Mode = ServiceOperationMode.Managed,
                    Capacity = 2, Supports = ids.Select(id => new ServiceSupportLinkSnapshot { SupportId = id }).ToArray() };
                var contract = (ServiceSessionContractSnapshot)builder.Invoke(runtime, new object[]
                { hub, new BuildingServiceHubAbility { serviceCategory = ServiceCategory.Dining }, process,
                    new ServiceModeProcessContract { activeStages = ServiceProcessStageMask.Service, serviceSeconds = 5 }, false });
                Require(contract.supportIds.SequenceEqual(ids.Distinct(StringComparer.Ordinal)), "Support definition set is wrong.");
                var authority = new ServiceSessionAggregate();
                authority.SetMode(hub.HubId, hub.Mode, out _);
                Require(authority.TryBegin(new ServiceSessionBeginCommand { HubId = hub.HubId, ActorId = "actor:r26",
                    ProcessId = "process:r26", Category = ServiceCategory.Dining, Capacity = 2, StartedAt = 0,
                    Contract = contract }, out var session, out _), "Service builder contract rejected.");
                string json = JsonUtility.ToJson(authority.Capture(0));
                var saved = JsonUtility.FromJson<ServiceRoomsSaveData>(json);
                var restored = ServiceSessionAggregate.CreateRestored(saved, authority.Version);
                Require(restored.ActiveSessions.Single().Contract.supportIds.SequenceEqual(contract.supportIds), "Service JSON lost support definitions.");
                saved.sessions[0].contract.supportIds = new[] { "support:r26", "support:r26" };
                bool rejected = false;
                try { ServiceSessionAggregate.CreateRestored(saved, authority.Version); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && restored.ActiveSessions.Count == 1, "Malformed restore changed accepted authority.");
                restored = ServiceSessionAggregate.CreateRestored(JsonUtility.FromJson<ServiceRoomsSaveData>(json), authority.Version);
                Require(restored.TryComplete(session.SessionId, 10, out _, out _), "Restored service could not complete.");
                int committed = restored.Version;
                Require(!restored.TryComplete(session.SessionId, 11, out _, out _) && restored.Version == committed,
                    "Service completion retry duplicated the transition.");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(process); }
    }

    private static void VerifyPublicationGuards()
    {
        Require(ContentIdentityProtection.TryLoadBaseline(out var baseline, out string loadError), loadError);
        Require(ContentIdentityProtection.ValidateAll().Count == 0, "Published identity inventory is not current.");
        var crop = AssetDatabase.FindAssets("t:CropDefinitionSO").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CropDefinitionSO>).First();
        WithRestoredAsset(crop, source =>
        {
            SerializedProperty id = source.FindProperty("harvestItemId");
            Require(ContentIdentityProtection.IsPublished(id, out _), "Published physical identity is editable.");
            id.stringValue += ":changed";
            source.ApplyModifiedPropertiesWithoutUndo();
            Require(ContentIdentityProtection.ValidateAssetForSave(AssetDatabase.GetAssetPath(crop), baseline).Count > 0,
                "Changed published physical identity can be saved.");
        });
        var anatomy = AssetDatabase.FindAssets("t:AnatomyProfileSO").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AnatomyProfileSO>).First(value => value.Nodes.Count > 1);
        WithRestoredAsset(anatomy, source =>
        {
            SerializedProperty nodes = source.FindProperty("nodes");
            Require(ContentIdentityProtection.IsPublished(nodes.GetArrayElementAtIndex(0).FindPropertyRelative("pairedGroupId"), out _),
                "Published pair identity is editable.");
            nodes.MoveArrayElement(0, nodes.arraySize - 1);
            Require(ContentIdentityProtection.IsPublished(nodes.GetArrayElementAtIndex(nodes.arraySize - 1).FindPropertyRelative("pairedGroupId"), out _),
                "Reordering unlocked a published node.");
        });
        foreach (string type in new[] { "EquipmentEvolutionFormulaCatalogSO", "FacilityEvolutionRecipeSO", "CharacterAcquiredTraitSettingsSO" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:" + type).First()));
            WithRestoredAsset(asset, source =>
            {
                SerializedProperty policy = source.FindProperty("formulaPolicy");
                Require(ContentIdentityProtection.IsPublished(policy, out _), "Published formula policy is editable.");
                policy.FindPropertyRelative("baseBudget").intValue++;
                source.ApplyModifiedPropertiesWithoutUndo();
                Require(ContentIdentityProtection.ValidatePublishedFormulaContracts(baseline).Count > 0,
                    "Changed formula can silently replace saved pending authority: " + type);
            });
        }
        var root = AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>(ContentIdentityProtection.RootAssetPath);
        Require(typeof(BuildingAbility).GetField("abilityId", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetCustomAttribute<ContentIdentityAttribute>() != null,
            "Published building ability identities have no read-only Inspector drawer.");
        BuildingSO structural = root.DomainCatalogs.OfType<GameDomainContentCatalogSO>()
            .SelectMany(domain => domain.Definitions).OfType<BuildingSO>()
            .First(value => value.GetAbility<BuildingStructuralIntegrityAbility>() != null);
        WithRestoredAsset(structural, source =>
        {
            SerializedProperty list = source.FindProperty("abilityModules.items");
            SerializedProperty id = Enumerable.Range(0, list.arraySize).Select(list.GetArrayElementAtIndex)
                .First(element => element.managedReferenceFullTypename.EndsWith("BuildingStructuralIntegrityAbility", StringComparison.Ordinal))
                .FindPropertyRelative("abilityId");
            Require(ContentIdentityProtection.IsPublished(id, out _), "Structural ability ID is not protected.");
            id.stringValue += ":changed"; source.ApplyModifiedPropertiesWithoutUndo();
            Require(ContentIdentityProtection.ValidateAssetForSave(AssetDatabase.GetAssetPath(structural), baseline).Count > 0,
                "Changed structural identity can be saved.");
        });
        CharacterAcquiredTraitModuleSO module = root.DomainCatalogs.OfType<GameDomainContentCatalogSO>()
            .SelectMany(domain => domain.Definitions).OfType<CharacterAcquiredTraitModuleSO>()
            .First(value => value.Effects.Count > 0);
        WithRestoredAsset(module, source =>
        {
            SerializedProperty amount = source.FindProperty("effects").GetArrayElementAtIndex(0).FindPropertyRelative("value");
            amount.floatValue += 1;
            source.ApplyModifiedPropertiesWithoutUndo();
            Require(ContentIdentityProtection.ValidateAssetForSave(AssetDatabase.GetAssetPath(module), baseline)
                    .Any(error => error.Contains("Published formula", StringComparison.Ordinal)),
                "A dependency save bypassed its published acquired-trait formula contract.");
        });
        Require(ContentIdentityProtection.ValidateAll().Count == 0, "Publication mutation fixture was not restored.");
    }

    private static void WithRestoredAsset(ScriptableObject asset, Action<SerializedObject> action)
    {
        string before = EditorJsonUtility.ToJson(asset);
        bool dirty = EditorUtility.IsDirty(asset);
        try { action(new SerializedObject(asset)); }
        finally
        {
            EditorJsonUtility.FromJsonOverwrite(before, asset);
            if (!dirty) EditorUtility.ClearDirty(asset);
        }
    }

    private static void VerifyInspectorGUI()
    {
        var window = ScriptableObject.CreateInstance<ContentReferenceInspectorProbeWindow>();
        try
        {
            window.position = new Rect(0, 0, 900, 800);
            window.ShowUtility();
            foreach (var target in new[]
            {
                (Type: "CropDefinitionSO", Path: "harvestItemId"),
                (Type: "StockInfo", Path: "shopId"),
                (Type: "AnatomyProfileSO", Path: "nodes.Array.data[0].pairedGroupId"),
                (Type: "EquipmentEvolutionFormulaCatalogSO", Path: "formulaPolicy"),
                (Type: "FacilityEvolutionRecipeSO", Path: "formulaPolicy"),
                (Type: "CharacterAcquiredTraitSettingsSO", Path: "formulaPolicy")
            })
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:" + target.Type).First()));
                string before = EditorJsonUtility.ToJson(asset);
                bool dirty = EditorUtility.IsDirty(asset);
                using var source = new SerializedObject(asset);
                window.Property = source.FindProperty(target.Path);
                window.Property.isExpanded = true;
                int drawn = window.DrawCount;
                window.SendEvent(new Event { type = EventType.Layout });
                window.SendEvent(new Event { type = EventType.Repaint });
                Require(window.Failure == null && window.DrawCount > drawn,
                    "Inspector drawer did not render " + target.Type + "." + target.Path + ": " + window.Failure);
                Require(before == EditorJsonUtility.ToJson(asset) && dirty == EditorUtility.IsDirty(asset),
                    "Inspector drawing changed published data: " + target.Type);
                window.Property = null;
            }
            Debug.Log("R26_INSPECTOR=PASS; six published surfaces; Layout/Repaint; source unchanged");
        }
        finally
        {
            window.Property = null;
            window.Close();
            UnityEngine.Object.DestroyImmediate(window);
        }
    }

    private static void WriteNew(string name, string text)
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Missing evidence directory.");
        Directory.CreateDirectory(output);
        using var writer = new StreamWriter(new FileStream(Path.Combine(output, name), FileMode.CreateNew));
        writer.Write(text);
    }

    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }
}
