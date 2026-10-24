#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class WorkTypeReferenceIntegrityDebugScenarios
{
    private const BindingFlags InstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Tools/Dungeon Story/QA/N01 Work Type Reference Integrity")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log(
            "N01_WORK_TYPE_REFERENCE_INTEGRITY=PASS; "
            + "pickers=7; owners=6; schema2=true; atomic-migration-retry=true");
    }

    public static void RunAll()
    {
        VerifyPickerCoverage();
        VerifyCatalogAndProductionRecipe();
        VerifyBuildingAbilityReferences();
        VerifyOffenseUrgentSiteReference();
        VerifySpeciesAptitudes();
        VerifyPriorityJsonAndMigration();
        VerifyPriorityValidationCacheInvalidation();
    }

    private static void VerifyPickerCoverage()
    {
        RequireWorkTypePicker(typeof(ProductionRecipeSO), "workTypeId");
        RequireWorkTypePicker(
            typeof(BuildingProcessAccidentFireSourceAbility),
            "workTypeId");
        RequireWorkTypePicker(typeof(BuildingProcessFluidAbility), "workTypeIds");
        RequireWorkTypePicker(
            typeof(OffenseUrgentSiteDefinitionSO),
            "mitigationWorkTypeId");
        RequireWorkTypePicker(typeof(WorkPriorityEntry), "workTypeId");
        RequireWorkTypePicker(
            typeof(CharacterSpeciesDefinitionSO),
            "strongWorkTypeIds");
        RequireWorkTypePicker(
            typeof(CharacterSpeciesDefinitionSO),
            "weakWorkTypeIds");
    }

    private static void VerifyCatalogAndProductionRecipe()
    {
        Require(
            WorkTypeCatalog.RequireRegistered(
                BuiltInWorkTypeIds.Craft,
                "N01 known catalog reference").WorkTypeId
            == BuiltInWorkTypeIds.Craft,
            "The registered craft work type did not resolve.");
        RequireThrows<InvalidOperationException>(
            () => WorkTypeCatalog.RequireRegistered(
                "work:qa-typo",
                "N01 unknown catalog reference"),
            "The catalog accepted an unknown work type ID.");

        ProductionRecipeSO recipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        try
        {
            ConfigureRecipe(recipe, "recipe:n01-legacy", string.Empty);
            Require(recipe.WorkTypeId == BuiltInWorkTypeIds.Craft,
                "A legacy blank recipe did not retain the craft default.");
            ConfigureRecipe(recipe, "recipe:n01-before", BuiltInWorkTypeIds.Cook.Value);
            Require(recipe.WorkTypeId == BuiltInWorkTypeIds.Cook,
                "A registered recipe work type changed behavior.");

            RequireThrows<InvalidOperationException>(
                () => ConfigureRecipe(
                    recipe,
                    "recipe:n01-after",
                    "work:qa-typo"),
                "A recipe accepted an unknown work type ID.");
            Require(
                string.Equals(
                    recipe.RecipeId,
                    "recipe:n01-before",
                    StringComparison.Ordinal)
                && recipe.WorkTypeId == BuiltInWorkTypeIds.Cook,
                "A rejected recipe configuration partially mutated the asset.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyBuildingAbilityReferences()
    {
        var fire = new BuildingProcessAccidentFireSourceAbility
        {
            workTypeId = BuiltInWorkTypeIds.Craft.Value,
            ignitionIntensity = 0.25f
        };
        fire.ValidateOrThrow();
        Require(fire.Supports(BuiltInWorkTypeIds.Craft.Value)
                && !fire.Supports(BuiltInWorkTypeIds.Cook.Value),
            "The registered fire-source work type changed behavior.");
        fire.workTypeId = "work:qa-typo";
        RequireThrows<InvalidOperationException>(
            fire.ValidateOrThrow,
            "A fire-source ability accepted an unknown work type ID.");

        var fluid = new BuildingProcessFluidAbility
        {
            workTypeIds = new[]
            {
                BuiltInWorkTypeIds.Cook.Value,
                BuiltInWorkTypeIds.Surgery.Value
            }
        };
        fluid.ValidateOrThrow();
        Require(fluid.Supports(BuiltInWorkTypeIds.Cook)
                && !fluid.Supports(BuiltInWorkTypeIds.Craft),
            "Registered fluid-process work types changed behavior.");
        fluid.workTypeIds = new[] { "work:qa-typo" };
        RequireThrows<InvalidOperationException>(
            fluid.ValidateOrThrow,
            "A fluid-process ability accepted an unknown work type ID.");
        fluid.workTypeIds = new[]
        {
            BuiltInWorkTypeIds.Cook.Value,
            BuiltInWorkTypeIds.Cook.Value
        };
        RequireThrows<InvalidOperationException>(
            fluid.ValidateOrThrow,
            "A fluid-process ability accepted duplicate work type rows.");
    }

    private static void VerifyOffenseUrgentSiteReference()
    {
        OffenseUrgentSiteDefinitionSO definition =
            ScriptableObject.CreateInstance<OffenseUrgentSiteDefinitionSO>();
        try
        {
            definition.urgentSiteId = "urgent-site:n01";
            definition.ValidateWorkTypeOrThrow();
            Require(!definition.HasMitigationWorkType,
                "An optional blank mitigation work type became required.");

            definition.mitigationWorkTypeId =
                BuiltInWorkTypeIds.ThreatMitigation.Value;
            definition.ValidateWorkTypeOrThrow();
            Require(
                definition.MitigationWorkType
                == BuiltInWorkTypeIds.ThreatMitigation,
                "The registered mitigation work type changed behavior.");

            definition.mitigationWorkTypeId = "work:qa-typo";
            RequireThrows<InvalidOperationException>(
                definition.ValidateWorkTypeOrThrow,
                "An urgent site accepted an unknown mitigation work type ID.");
            definition.mitigationWorkTypeId = " ";
            RequireThrows<InvalidOperationException>(
                definition.ValidateWorkTypeOrThrow,
                "An urgent site accepted a noncanonical blank work type ID.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void VerifySpeciesAptitudes()
    {
        CharacterSpeciesSO species =
            ScriptableObject.CreateInstance<CharacterSpeciesSO>();
        try
        {
            species.speciesTag = "species:n01";
            species.strongWorkTypeIds =
                new[] { BuiltInWorkTypeIds.Craft.Value };
            species.weakWorkTypeIds =
                new[] { BuiltInWorkTypeIds.Cook.Value };
            species.ValidateWorkTypeAptitudesOrThrow();
            Require(
                CharacterSpeciesWorkAptitudeRules.GetDisposition(
                    species,
                    BuiltInWorkTypeIds.Craft) == 1
                && CharacterSpeciesWorkAptitudeRules.GetDisposition(
                    species,
                    BuiltInWorkTypeIds.Cook) == -1
                && CharacterSpeciesWorkAptitudeRules.GetDisposition(
                    species,
                    BuiltInWorkTypeIds.Research) == 0,
                "Registered species aptitude results changed.");
            RequireThrows<InvalidOperationException>(
                () => CharacterSpeciesWorkAptitudeRules.GetDisposition(
                    species,
                    new WorkTypeId("work:qa-typo")),
                "An unknown species aptitude query was treated as neutral.");

            species.strongWorkTypeIds = new[] { "work:qa-typo" };
            RequireThrows<InvalidOperationException>(
                species.ValidateWorkTypeAptitudesOrThrow,
                "A species accepted an unknown aptitude work type ID.");
            species.strongWorkTypeIds = new[]
            {
                BuiltInWorkTypeIds.Craft.Value,
                BuiltInWorkTypeIds.Craft.Value
            };
            RequireThrows<InvalidOperationException>(
                species.ValidateWorkTypeAptitudesOrThrow,
                "A species accepted duplicate aptitude rows.");
            species.strongWorkTypeIds =
                new[] { BuiltInWorkTypeIds.Craft.Value };
            species.weakWorkTypeIds =
                new[] { BuiltInWorkTypeIds.Craft.Value };
            RequireThrows<InvalidOperationException>(
                species.ValidateWorkTypeAptitudesOrThrow,
                "A species accepted the same strong and weak aptitude.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(species);
        }
    }

    private static void VerifyPriorityJsonAndMigration()
    {
        WorkPriorityProfile schema2 = JsonUtility.FromJson<WorkPriorityProfile>(
            "{\"schemaVersion\":2,\"priorities\":["
            + "{\"workTypeId\":\"work:craft\",\"priority\":1}]}");
        Require(schema2.Entries.Count == 1
                && schema2.GetPriority(BuiltInWorkTypeIds.Craft)
                    == WorkPriorityLevel.Priority1
                && schema2.GetPriority(BuiltInWorkTypeIds.Research)
                    == WorkTypeCatalog.RequireRegistered(
                        BuiltInWorkTypeIds.Research,
                        "N01 missing priority default").DefaultPriority,
            "A valid schema-2 priority profile or its missing-row default changed.");
        RequireThrows<InvalidOperationException>(
            () => schema2.GetPriority(new WorkTypeId("work:qa-typo")),
            "An unknown priority query silently returned a default.");
        RequireThrows<InvalidOperationException>(
            () => _ = new WorkPriorityEntry(
                "work:qa-typo",
                WorkPriorityLevel.Priority1),
            "A priority row constructor accepted an unknown work type ID.");
        WorkPriorityProfile schema2RoundTrip =
            JsonUtility.FromJson<WorkPriorityProfile>(
                JsonUtility.ToJson(schema2));
        Require(schema2RoundTrip.Entries.Count == 1
                && schema2RoundTrip.GetPriority(BuiltInWorkTypeIds.Craft)
                    == WorkPriorityLevel.Priority1,
            "A valid schema-2 priority profile did not round-trip.");

        WorkPriorityProfile invalidMigration =
            JsonUtility.FromJson<WorkPriorityProfile>(
                "{\"schemaVersion\":1,\"priorities\":["
                + "{\"workTypeId\":\"work:qa-typo\",\"priority\":1},"
                + "{\"workTypeId\":\"work:craft\",\"priority\":3}]}");
        List<WorkPriorityEntry> sourceRows = GetPriorityRows(invalidMigration);
        RequireThrows<InvalidOperationException>(
            () => _ = invalidMigration.Entries,
            "A legacy priority profile silently migrated an unknown row.");
        Require(GetSchemaVersion(invalidMigration) == 1
                && ReferenceEquals(sourceRows, GetPriorityRows(invalidMigration))
                && sourceRows.Count == 2
                && string.Equals(
                    sourceRows[0].WorkTypeId,
                    "work:qa-typo",
                    StringComparison.Ordinal),
            "A rejected priority migration partially mutated the profile.");

        GetRequiredField(typeof(WorkPriorityEntry), "workTypeId")
            .SetValue(sourceRows[0], BuiltInWorkTypeIds.Cook.Value);
        Require(invalidMigration.Entries.Count == WorkTypeCatalog.All.Count
                && GetSchemaVersion(invalidMigration) == 2
                && invalidMigration.GetPriority(BuiltInWorkTypeIds.Cook)
                    == WorkPriorityLevel.Priority1
                && invalidMigration.GetPriority(BuiltInWorkTypeIds.Craft)
                    == WorkPriorityLevel.Priority3,
            "A corrected legacy priority profile did not migrate on retry.");
        WorkPriorityProfile migratedRoundTrip =
            JsonUtility.FromJson<WorkPriorityProfile>(
                JsonUtility.ToJson(invalidMigration));
        Require(migratedRoundTrip.Entries.Count == WorkTypeCatalog.All.Count
                && migratedRoundTrip.GetPriority(BuiltInWorkTypeIds.Cook)
                    == WorkPriorityLevel.Priority1
                && migratedRoundTrip.GetPriority(BuiltInWorkTypeIds.Craft)
                    == WorkPriorityLevel.Priority3,
            "A corrected migrated priority profile did not round-trip.");

        WorkPriorityProfile duplicate =
            JsonUtility.FromJson<WorkPriorityProfile>(
                "{\"schemaVersion\":2,\"priorities\":["
                + "{\"workTypeId\":\"work:craft\",\"priority\":1},"
                + "{\"workTypeId\":\"work:craft\",\"priority\":2}]}");
        RequireThrows<InvalidOperationException>(
            () => _ = duplicate.Entries,
            "A schema-2 priority profile accepted duplicate rows.");
    }

    private static void VerifyPriorityValidationCacheInvalidation()
    {
        const string validJson =
            "{\"schemaVersion\":2,\"priorities\":["
            + "{\"workTypeId\":\"work:craft\",\"priority\":1}]}";
        const string invalidJson =
            "{\"schemaVersion\":2,\"priorities\":["
            + "{\"workTypeId\":\"work:qa-typo\",\"priority\":1}]}";
        WorkPriorityProfile jsonProfile =
            JsonUtility.FromJson<WorkPriorityProfile>(validJson);
        Require(jsonProfile.Entries.Count == 1,
            "The JSON cache fixture did not enter a validated state.");
        JsonUtility.FromJsonOverwrite(invalidJson, jsonProfile);
        RequireThrows<InvalidOperationException>(
            () => _ = jsonProfile.Entries,
            "JSON overwrite bypassed cached priority-row validation.");
        JsonUtility.FromJsonOverwrite(validJson, jsonProfile);
        Require(jsonProfile.Entries.Count == 1,
            "A corrected JSON overwrite could not retry validation.");

        CharacterSO character = ScriptableObject.CreateInstance<CharacterSO>();
        try
        {
            WorkPriorityProfile inspectorProfile =
                character.defaultWorkPriorities;
            Require(
                inspectorProfile.Entries.Count == WorkTypeCatalog.All.Count,
                "The SerializedObject cache fixture was not validated.");
            var serialized = new SerializedObject(character);
            serialized.Update();
            SerializedProperty rows = serialized.FindProperty(
                "defaultWorkPriorities.priorities");
            Require(rows != null && rows.arraySize > 0,
                "The serialized priority rows were not found.");
            SerializedProperty firstId = rows.GetArrayElementAtIndex(0)
                .FindPropertyRelative("workTypeId");
            Require(firstId != null,
                "The serialized priority work type ID was not found.");
            string originalId = firstId.stringValue;
            firstId.stringValue = "work:qa-typo";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            RequireThrows<InvalidOperationException>(
                inspectorProfile.OnBeforeSerialize,
                "SerializedObject editing bypassed pre-serialize validation.");
            RequireThrows<InvalidOperationException>(
                () => _ = inspectorProfile.Entries,
                "A failed pre-serialize validation left a usable stale cache.");

            firstId.stringValue = originalId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            inspectorProfile.OnBeforeSerialize();
            Require(
                inspectorProfile.Entries.Count == WorkTypeCatalog.All.Count,
                "A corrected SerializedObject edit could not retry validation.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(character);
        }
    }

    private static void ConfigureRecipe(
        ProductionRecipeSO recipe,
        string recipeId,
        string workTypeId)
    {
        recipe.Configure(
            recipeId,
            "N01 recipe",
            string.Empty,
            string.Empty,
            workTypeId,
            string.Empty,
            1f,
            Array.Empty<ItemAmountDefinition>(),
            Array.Empty<ProductionOutputDefinition>());
    }

    private static void RequireWorkTypePicker(Type ownerType, string fieldName)
    {
        ContentReferenceAttribute reference = GetRequiredField(ownerType, fieldName)
            .GetCustomAttribute<ContentReferenceAttribute>();
        Require(reference != null
                && reference.Kind == ContentReferenceKind.WorkType,
            ownerType.Name + "." + fieldName
            + " is missing the WorkType content picker.");
    }

    private static List<WorkPriorityEntry> GetPriorityRows(
        WorkPriorityProfile profile) =>
        (List<WorkPriorityEntry>)GetRequiredField(
            typeof(WorkPriorityProfile),
            "priorities").GetValue(profile);

    private static int GetSchemaVersion(WorkPriorityProfile profile) =>
        (int)GetRequiredField(
            typeof(WorkPriorityProfile),
            "schemaVersion").GetValue(profile);

    private static FieldInfo GetRequiredField(Type ownerType, string fieldName) =>
        ownerType.GetField(fieldName, InstanceFields)
        ?? throw new InvalidOperationException(
            ownerType.Name + "." + fieldName + " was not found.");

    private static void RequireThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
