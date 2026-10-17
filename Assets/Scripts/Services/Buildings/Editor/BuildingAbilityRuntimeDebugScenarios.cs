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
        VerifyCurrentDefinitionBindingAndRejection();
        Debug.Log("ARCHITECTURE_BUILDING_RUNTIME_BINDINGS=PASS; definitions13; runtime capabilities; JSON identity; duplicate/missing/invalid factories; current collection/revision; readonly config; atomic reject; retry");
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
        Require(BuildingAbilityRuntimeCatalog.CreateBuiltIns(custom).Bind(new BuildingAbility[] { UnlistedWorkAbility.Create() })
            .Single() is IBuildingRuntimeStateAbility, "Explicit custom runtime was not registered.");
        ExpectFailure(() => BuildingAbilityRuntimeCatalog.CreateBuiltIns(custom, custom));
        ExpectFailure(() => BuildingAbilityRuntimeCatalog.CreateBuiltIns(
            new BuildingAbilityRuntimeFactory<UnlistedWorkAbility, object>(_ => new object()))
            .Bind(new BuildingAbility[] { UnlistedWorkAbility.Create() }));
        ExpectFailure(() => BuildingAbilityRuntimeCatalog.CreateBuiltIns(
            new BuildingAbilityRuntimeFactory<UnlistedWorkAbility, object>(_ => null))
            .Bind(new BuildingAbility[] { UnlistedWorkAbility.Create() }));
    }

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
    private static void ExpectFailure(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Expected explicit building runtime binding rejection.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
