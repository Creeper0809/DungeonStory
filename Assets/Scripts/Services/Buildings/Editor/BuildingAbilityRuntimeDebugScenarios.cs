#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BuildingAbilityRuntimeDebugScenarios
{
    public static void RunForBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Building runtime verification requires isolated batch mode.");
        VerifyDefinitionSerializationAndFactories();
        VerifyPublishedPersistentAbilityIds();
        VerifyCurrentDefinitionBindingAndRejection();
        Debug.Log("ARCHITECTURE_BUILDING_RUNTIME_BINDINGS=PASS; definitions13; runtime capabilities; JSON identity; duplicate/missing/invalid factories; persistent declaration mismatch; published custom/built-in IDs; stateless exclusion; current collection/revision; readonly config; atomic reject; retry");
    }

    private static void VerifyDefinitionSerializationAndFactories()
    {
        BuildingAbility[] definitions =
        {
            new BuildingCoverAbility(), new BuildingStructuralIntegrityAbility(), new BuildingProductionAbility(),
            new BuildingLightingAbility(), new BuildingWorkAmountAbility(), new BuildingExpeditionRecoveryAbility(),
            new BuildingReceptionAbility(), new BuildingPatrolPostAbility(), new BuildingOutdoorRestAbility(),
            new BuildingExteriorMaintenanceAbility(), new BuildingTrainingAbility(), new BuildingSecurityAbility(),
            new BuildingCrimeRiskModifierAbility()
        };
        bool nullReceiverRejected = false;
        try { ((BuildingWorkAmountAbility)null).GetRequiredWork(null, BuiltInWorkTypeIds.Construct); }
        catch (NullReferenceException) { nullReceiverRejected = true; }
        Require(nullReceiverRejected, "Direct-call null receiver failure changed.");
        BuildingAbilityRuntimeCatalog catalog = BuildingAbilityRuntimeCatalog.CreateBuiltIns();
        IReadOnlyList<object> runtimes = catalog.Bind(definitions);
        Require(runtimes.Count == 13 && runtimes.OfType<IBuildingRuntimeStateAbility>().Count() == 4
            && runtimes.OfType<IBuildingVisualRuntimeAbility>().Count() == 3
            && runtimes.OfType<IBuildingUseCompletedRuntimeAbility>().Count() == 2
            && runtimes.OfType<IBuildingExteriorWorkRuntimeAbility>().Count() == 4
            && runtimes.OfType<IBuildingWorkAmountRuntimeAbility>().Count() == 1
            && runtimes.OfType<IBuildingCrimeRiskModifier>().Count() == 1,
            "Registered capability set differs from the original building abilities.");
        foreach (BuildingAbility definition in definitions)
        {
            Require(definition is IBuildingRuntimeAbilityDefinition
                && definition is not IBuildingRuntimeStateAbility && definition is not IBuildingVisualRuntimeAbility
                && definition is not IBuildingUseCompletedRuntimeAbility && definition is not IBuildingExteriorWorkRuntimeAbility
                && definition is not IBuildingWorkAmountRuntimeAbility && definition is not IBuildingCrimeRiskModifier,
                "A serialized definition still owns runtime behavior: " + definition.GetType().Name);
            string json = JsonUtility.ToJson(definition);
            var restored = (BuildingAbility)JsonUtility.FromJson(json, definition.GetType());
            Require(restored.GetType() == definition.GetType() && restored.AbilityId == definition.AbilityId
                && JsonUtility.ToJson(restored) == json && catalog.Bind(new[] { restored }).Count == 1,
                "Definition JSON/type/identity did not roundtrip: " + definition.GetType().Name);
        }
        Require(Mathf.Approximately(new BuildingWorkAmountAbility().GetRequiredWork(null, BuiltInWorkTypeIds.Construct), 30f),
            "Direct work-amount command lost its configured calculation.");
        ExpectFailure(() => catalog.Bind(new BuildingAbility[] { UnlistedWorkAbility.Create() }));
        var custom = new BuildingAbilityRuntimeFactory<UnlistedWorkAbility, UnlistedWorkAbilityRuntime>(d => new(d));
        Require((BuildingAbility)UnlistedWorkAbility.Create() is IBuildingPersistentAbilityDefinition,
            "The custom saved-state fixture lost its persistent identity declaration.");
        Require(BuildingAbilityRuntimeCatalog.CreateBuiltIns(custom).Bind(new BuildingAbility[] { UnlistedWorkAbility.Create() })
            .Single() is IBuildingRuntimeStateAbility, "Explicit custom runtime was not registered.");
        ExpectFailure(() => BuildingAbilityRuntimeCatalog.CreateBuiltIns(custom, custom));
        ExpectFailure(() => BuildingAbilityRuntimeCatalog.CreateBuiltIns(
            new BuildingAbilityRuntimeFactory<UnlistedWorkAbility, object>(_ => new object()))
            .Bind(new BuildingAbility[] { UnlistedWorkAbility.Create() }));
        ExpectFailure(() => BuildingAbilityRuntimeCatalog.CreateBuiltIns(
            new BuildingAbilityRuntimeFactory<UnlistedWorkAbility, object>(_ => null))
            .Bind(new BuildingAbility[] { UnlistedWorkAbility.Create() }));

        var savedOnStateless = new BuildingAbilityRuntimeCatalog(new IBuildingAbilityRuntimeFactory[]
        {
            new BuildingAbilityRuntimeFactory<BuildingLightingAbility, UnlistedWorkAbilityRuntime>(
                _ => new(UnlistedWorkAbility.Create()))
        });
        ExpectFailure(() => savedOnStateless.Bind(new BuildingAbility[] { new BuildingLightingAbility() }),
            "Saved-state capability and published identity declaration differ");
        var statelessOnSaved = new BuildingAbilityRuntimeCatalog(new IBuildingAbilityRuntimeFactory[]
        {
            new BuildingAbilityRuntimeFactory<UnlistedWorkAbility, object>(_ =>
                catalog.Bind(new BuildingAbility[] { new BuildingLightingAbility() }).Single())
        });
        ExpectFailure(() => statelessOnSaved.Bind(new BuildingAbility[] { UnlistedWorkAbility.Create() }),
            "Saved-state capability and published identity declaration differ");
    }

    public static void VerifyPublishedPersistentAbilityIds()
    {
        BuildingSO fixture = ScriptableObject.CreateInstance<BuildingSO>();
        fixture.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var custom = UnlistedWorkAbility.Create();
            var stateless = new BuildingLightingAbility();
            var abilities = new BuildingAbilityCollection();
            abilities.Add(custom);
            abilities.Add(stateless);
            fixture.ReplaceAbilities(abilities);
            const string fixtureGuid = "11111111111111111111111111111111";
            var original = ContentIdentityProtection.CaptureAsset(fixtureGuid, fixture);
            string customPath = SavedAbilityPath(custom.AbilityId, nameof(UnlistedWorkAbility));
            Require(original.fields.Any(field => field.path == customPath && field.value == custom.AbilityId),
                "The new persistent fixture was omitted from published identity capture.");
            Require(original.fields.All(field => field.path != SavedAbilityPath(
                    stateless.AbilityId, nameof(BuildingLightingAbility))),
                "A stateless runtime ability was unnecessarily included in published identity capture.");
            var baseline = new ContentIdentityProtection.Snapshot { assets = new() { original } };
            Require(ContentIdentityProtection.Validate(baseline, baseline).Count == 0,
                "The persistent fixture baseline is invalid before mutation.");
            string originalJson = EditorJsonUtility.ToJson(fixture);
            using (var serialized = new SerializedObject(fixture))
            {
                var id = serialized.FindProperty(BuildingSO.AbilityModulesFieldName)
                    .FindPropertyRelative("items").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("abilityId");
                Require(id != null, "The custom persistent ability ID is unavailable.");
                id.stringValue = custom.AbilityId + "-renamed";
                Require(serialized.ApplyModifiedPropertiesWithoutUndo(),
                    "The custom persistent ID mutation was not applied.");
            }
            var changed = new ContentIdentityProtection.Snapshot
            {
                assets = new() { ContentIdentityProtection.CaptureAsset(fixtureGuid, fixture) }
            };
            Require(ContentIdentityProtection.Validate(baseline, changed).Any(error => error.Contains(customPath)),
                "Renaming a newly declared persistent ability ID was accepted.");
            EditorJsonUtility.FromJsonOverwrite(originalJson, fixture);
            var restored = new ContentIdentityProtection.Snapshot
            {
                assets = new() { ContentIdentityProtection.CaptureAsset(fixtureGuid, fixture) }
            };
            Require(ContentIdentityProtection.Validate(baseline, restored).Count == 0,
                "Restoring the custom persistent identity did not recover the original contract.");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }

        Require(ContentIdentityProtection.TryLoadBaseline(out var published, out string baselineError), baselineError);
        string[] builtIns = { nameof(BuildingCoverAbility), nameof(BuildingStructuralIntegrityAbility),
            nameof(BuildingProductionAbility), nameof(BuildingSecurityAbility) };
        var covered = new HashSet<string>(StringComparer.Ordinal);
        foreach (var owner in published.assets.Where(asset => asset.typeName == nameof(BuildingSO)))
        {
            var expected = owner.fields.Where(field => builtIns.Any(type =>
                field.path.EndsWith(";type=" + type + "].abilityId", StringComparison.Ordinal))).ToArray();
            if (expected.Length == 0) continue;
            BuildingSO source = AssetDatabase.LoadAssetAtPath<BuildingSO>(AssetDatabase.GUIDToAssetPath(owner.guid));
            Require(source != null, "A published built-in ability owner could not be loaded.");
            var current = ContentIdentityProtection.CaptureAsset(owner.guid, source);
            foreach (var field in expected)
            {
                string type = builtIns.Single(value => field.path.EndsWith(
                    ";type=" + value + "].abilityId", StringComparison.Ordinal));
                Require(field.path == SavedAbilityPath(field.value, type)
                    && current.fields.Any(actual => actual.path == field.path && actual.value == field.value),
                    "A built-in published ability path/value changed: " + field.path);
                covered.Add(type);
            }
        }
        Require(covered.SetEquals(builtIns), "Published path preservation did not cover all four built-in saved abilities.");
    }

    private static string SavedAbilityPath(string abilityId, string typeName) =>
        "@nested:abilityModules.items[abilityId=" + Uri.EscapeDataString(abilityId)
        + ";type=" + typeName + "].abilityId";

    private static void VerifyCurrentDefinitionBindingAndRejection()
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO rejected = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject gameObject = new("BuildingRuntimeBindingFixture");
        try
        {
            data.width = data.height = rejected.width = rejected.height = 1;
            data.layer = rejected.layer = GridLayer.Building;
            data.category = rejected.category = BuildingCategory.Shop;
            data.runtimeArchetype = rejected.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
            data.ReplaceAbilities(new BuildingAbilityCollection());
            var modifier = new BuildingCrimeRiskModifierAbility { multiplier = 2f };
            data.AbilityModules.Add(modifier);
            BuildableObject building = gameObject.AddComponent<BuildableObject>();
            building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(building);
            building.Initialization(data, Vector2Int.zero);
            var first = building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().Single();
            Require(ReferenceEquals(first, building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().Single()),
                "Unchanged immutable configuration rebuilt runtime bindings.");
            modifier.multiplier = 3f;
            Require(Mathf.Approximately(first.ModifyCrimePressure(1f, default), 3f),
                "Runtime copied mutable state or cached a second configuration value.");
            int revision = data.AbilityModules.Revision;
            data.AbilityModules.Add(new BuildingTrainingAbility());
            var second = building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().Single();
            Require(data.AbilityModules.Revision == revision + 1 && !ReferenceEquals(first, second)
                && building.GetRuntimeAbilities<IBuildingUseCompletedRuntimeAbility>().Count() == 1,
                "Configuration collection revision did not rebuild its binding projection.");
            data.AbilityModules.Remove<BuildingTrainingAbility>();
            Require(!building.GetRuntimeAbilities<IBuildingUseCompletedRuntimeAbility>().Any(),
                "Removed configuration left a stale runtime binding.");
            var replacement = new BuildingAbilityCollection();
            replacement.Add(new BuildingCrimeRiskModifierAbility { multiplier = 4f });
            data.ReplaceAbilities(replacement);
            Require(Mathf.Approximately(building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().Single()
                .ModifyCrimePressure(1f, default), 4f), "New collection did not replace the old binding source.");
            Require(!JsonUtility.ToJson(replacement).Contains("\"revision\""), "Derived binding revision leaked into definition serialization.");
            SerializedObject inspector = new(data);
            SerializedProperty list = inspector.FindProperty(BuildingSO.AbilityModulesFieldName).FindPropertyRelative("items");
            list.GetArrayElementAtIndex(0).managedReferenceValue = new BuildingCrimeRiskModifierAbility { multiplier = 5f };
            inspector.ApplyModifiedPropertiesWithoutUndo();
            Require(Mathf.Approximately(building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().Single()
                .ModifyCrimePressure(1f, default), 5f), "Inspector managed-reference replacement kept a stale runtime binding.");
            replacement = data.AbilityModules;
            var beforeProviderReplacement = building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().Single();
            typeof(BuildableObject).GetField("abilityRuntimeDispatcher", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(building, new BuildingAbilityRuntimeDispatcher(Array.Empty<IBuildingAbilityWorkCompletedHandler>(),
                    Array.Empty<IBuildingWorkCompletionFallbackHandler>()));
            Require(!ReferenceEquals(beforeProviderReplacement, building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().Single()),
                "Dispatcher scope replacement retained the previous runtime binding.");
            string modules = CaptureModules(building);
            rejected.ReplaceAbilities(new BuildingAbilityCollection());
            rejected.AbilityModules.Add(UnlistedWorkAbility.Create());
            ExpectFailure(() => building.Initialization(rejected, Vector2Int.one));
            Require(ReferenceEquals(building.BuildingData, data) && CaptureModules(building) == modules
                && Mathf.Approximately(building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().Single()
                    .ModifyCrimePressure(1f, default), 5f),
                "Rejected runtime binding partially replaced live building data/modules.");
            replacement.Add(UnlistedWorkAbility.Create());
            ExpectFailure(() => building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().ToArray());
            Require(CaptureModules(building) == modules, "Failed binding rebuild changed module authority.");
            replacement.Remove<UnlistedWorkAbility>();
            Require(building.GetRuntimeAbilities<IBuildingCrimeRiskModifier>().Count() == 1,
                "Valid binding retry did not resume after removing the unregistered definition.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
            UnityEngine.Object.DestroyImmediate(data);
            UnityEngine.Object.DestroyImmediate(rejected);
        }
    }

    private static string CaptureModules(BuildableObject building) =>
        JsonUtility.ToJson(new ModuleSnapshot { modules = BuildingStateModulePersistence.Capture(building) });
    [Serializable] private sealed class ModuleSnapshot { public List<BuildingStateModuleSaveData> modules; }
    private static void ExpectFailure(Action action, string expectedMessage = null)
    {
        try { action(); }
        catch (InvalidOperationException exception)
        {
            Require(expectedMessage == null || exception.Message.Contains(expectedMessage),
                "Runtime binding failed for the wrong reason: " + exception.Message);
            return;
        }
        throw new InvalidOperationException("Expected explicit building runtime binding rejection.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
