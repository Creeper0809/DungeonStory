using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Content.CoreSession;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R19DefinitionGuardDebugScenarios
{
    private const string CoreRulesPath =
        "Assets/Resources/SO/Content/CoreSessionRules.asset";

    [MenuItem("DungeonStory/V27/Content/Validate R19 Definition Guards")]
    public static void RunAll()
    {
        VerifyPerformanceFormulaCapacityGuards();
        VerifyCoreSessionNumericGuards();
        VerifyStartingProficiencyMetadata();
        VerifyDefaultWorkerPolicyGuards();
        VerifyWorkerPolicyInstanceIdRoundTrip();
        int wildlifeCount = VerifyWildlifeEcologyGuards();
        Debug.Log(
            "R19_DEFINITION_GUARDS=PASS; formula undefined and null inputs rejected; "
            + "core numeric kinds rejected; starting proficiency scoped; "
            + "definition worker policy guarded; runtime worker IDs preserved; "
            + "wildlife legacy metadata ignored; "
            + $"wildlifeAssets={wildlifeCount}");
    }

    private static void VerifyPerformanceFormulaCapacityGuards()
    {
        CharacterPerformanceFormulaDefinitionSO formula =
            ScriptableObject.CreateInstance<CharacterPerformanceFormulaDefinitionSO>();
        try
        {
            formula.Configure(
                "fixture:r19:formula",
                "R19 formula fixture",
                CharacterPerformanceFormulaDomain.Work,
                CharacterPerformanceResultChannel.Factor,
                1f,
                new[]
                {
                    new CharacterPerformanceCapacityInput(
                        (CharacterFunctionalCapacityId)99,
                        1f,
                        CharacterPerformanceInputRole.Contribution)
                },
                string.Empty,
                string.Empty,
                0f,
                string.Empty);
            RequireRejected(
                formula.ValidateDefinition(),
                "invalid capacity input",
                "Undefined character capacity was accepted.");

            formula.Configure(
                "fixture:r19:formula",
                "R19 formula fixture",
                CharacterPerformanceFormulaDomain.Work,
                CharacterPerformanceResultChannel.Factor,
                1f,
                new CharacterPerformanceCapacityInput[] { null },
                string.Empty,
                string.Empty,
                0f,
                string.Empty);
            RequireRejected(
                formula.ValidateDefinition(),
                "invalid capacity input",
                "Null character capacity input was accepted.");
        }
        finally
        {
            Object.DestroyImmediate(formula);
        }
    }

    private static void VerifyCoreSessionNumericGuards()
    {
        CoreSessionRulesSO source = RequireAsset<CoreSessionRulesSO>(CoreRulesPath);
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceDirty = EditorUtility.IsDirty(source);
        CoreSessionRulesSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(clone);
        try
        {
            RequireClean(source.ValidateDefinition(), "Core-session baseline");
            SetFirstNestedInteger(clone, "externalProblemBands", "allowedIncidentKinds", 99);
            RequireRejected(
                clone.ValidateDefinition(),
                "external-problem bands",
                "Undefined exterior incident kind was accepted.");

            Restore(clone, baseline);
            SetFirstNestedInteger(clone, "serviceResearch", "serviceCategory", 99);
            RequireRejected(
                clone.ValidateDefinition(),
                "service research rules",
                "Undefined service category was accepted.");

            Restore(clone, baseline);
            SetFirstNestedInteger(clone, "serviceResearch", "operationMode", 99);
            RequireRejected(
                clone.ValidateDefinition(),
                "service research rules",
                "Undefined service operation mode was accepted.");

            RequireReferenceKind(
                typeof(ExternalProblemBand),
                "allowedIncidentKinds",
                ContentReferenceKind.CoreIncident,
                0);
            RequireReferenceKind(
                typeof(ServiceResearchRule),
                "serviceCategory",
                ContentReferenceKind.CoreServiceCategory,
                -1);
            RequireReferenceKind(
                typeof(ServiceResearchRule),
                "operationMode",
                ContentReferenceKind.CoreServiceMode,
                0);
        }
        finally
        {
            Object.DestroyImmediate(clone);
            Require(EditorJsonUtility.ToJson(source) == sourceJson,
                "Core-session fixture changed the source asset.");
            Require(EditorUtility.IsDirty(source) == sourceDirty,
                "Core-session fixture changed the source dirty state.");
        }
    }

    private static int VerifyWildlifeEcologyGuards()
    {
        WildlifeSpeciesSO[] sources = AssetDatabase
            .FindAssets("t:WildlifeSpeciesSO", new[] { "Assets/Resources/SO" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<WildlifeSpeciesSO>)
            .Where(value => value != null)
            .Distinct()
            .OrderBy(value => value.SpeciesId, StringComparer.Ordinal)
            .ToArray();
        Require(sources.Length > 0, "No wildlife species assets were found.");

        string[] sourceJson = sources
            .Select(value => EditorJsonUtility.ToJson(value))
            .ToArray();
        bool[] sourceDirty = sources
            .Select(value => EditorUtility.IsDirty(value))
            .ToArray();
        try
        {
            foreach (WildlifeSpeciesSO source in sources)
                RequireClean(source.ValidateDefinition(), $"Wildlife '{source.SpeciesId}'");

            WildlifeSpeciesSO witness = sources.FirstOrDefault(value =>
                value.PreySpeciesIds.Count == 0
                && value.DiseaseVectorIds.Count > 0
                && value.ActiveSeasons.Count > 0)
                ?? throw new InvalidOperationException(
                    "No wildlife metadata witness with executable ecology relations was found.");
            WildlifeSpeciesSO clone = Object.Instantiate(witness);
            string baseline = EditorJsonUtility.ToJson(clone);
            try
            {
                SetString(clone, "nestTag", string.Empty);
                SetString(clone, "migrationPatternId", string.Empty);
                RequireClean(
                    clone.ValidateDefinition(),
                    "Wildlife empty legacy metadata");

                SetString(clone, "nestTag", "fixture:r19:nest");
                SetString(clone, "migrationPatternId", "fixture:r19:migration");
                RequireClean(
                    clone.ValidateDefinition(),
                    "Wildlife populated legacy metadata");

                ClearArray(clone, "preySpeciesIds");
                ClearArray(clone, "diseaseVectorIds");
                ClearArray(clone, "activeSeasons");
                RequireRejected(
                    clone.ValidateDefinition(),
                    "at least three authored ecology relationship groups",
                    "Legacy wildlife metadata satisfied executable ecology coverage.");

                Restore(clone, baseline);
                SetInteger(clone, "breedingSeason", 99);
                RequireRejected(
                    clone.ValidateDefinition(),
                    "invalid breeding season",
                    "Undefined wildlife breeding season was accepted.");

                Restore(clone, baseline);
                SetFirstArrayInteger(clone, "activeSeasons", 99);
                RequireRejected(
                    clone.ValidateDefinition(),
                    "invalid active season",
                    "Undefined wildlife active season was accepted.");

                RequireHiddenLegacyField("nestTag");
                RequireHiddenLegacyField("migrationPatternId");
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }
        finally
        {
            for (int index = 0; index < sources.Length; index++)
            {
                Require(EditorJsonUtility.ToJson(sources[index]) == sourceJson[index],
                    $"Wildlife fixture changed source asset '{sources[index].name}'.");
                Require(EditorUtility.IsDirty(sources[index]) == sourceDirty[index],
                    $"Wildlife fixture changed dirty state for '{sources[index].name}'.");
            }
        }

        return sources.Length;
    }

    private static void VerifyStartingProficiencyMetadata()
    {
        RequireReferenceKind(
            typeof(CharacterStartingHistorySO),
            "primaryProficiencyId",
            ContentReferenceKind.StartingProficiency,
            -1);
        RequireReferenceKind(
            typeof(CharacterStartingHistorySO),
            "secondaryProficiencyId",
            ContentReferenceKind.StartingProficiency,
            -1);
        RequireReferenceKind(
            typeof(CharacterStartingProficiencyBonus),
            "proficiencyId",
            ContentReferenceKind.StartingProficiency,
            -1);
    }

    private static void VerifyDefaultWorkerPolicyGuards()
    {
        BuildingSO[] sources = AssetDatabase
            .FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(value => value?.GetAbility<BuildingWorkAmountAbility>() != null)
            .Distinct()
            .ToArray();
        Require(sources.Length > 0,
            "No building definition with a work amount ability was found.");
        foreach (BuildingSO source in sources)
        {
            RequireClean(
                source.GetAbility<BuildingWorkAmountAbility>()
                    .ValidateDefaultWorkerPolicy(),
                $"Building '{source.name}' default worker policy");
        }

        BuildingSO witness = sources[0];
        string sourceJson = EditorJsonUtility.ToJson(witness);
        bool sourceDirty = EditorUtility.IsDirty(witness);
        BuildingSO clone = Object.Instantiate(witness);
        try
        {
            BuildingWorkAmountAbility ability =
                clone.GetAbility<BuildingWorkAmountAbility>()
                ?? throw new InvalidOperationException(
                    "Cloned building lost its work amount ability.");
            FieldInfo policyField = typeof(BuildingWorkAmountAbility).GetField(
                "defaultWorkerPolicy",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "Building default worker policy field was not found.");

            policyField.SetValue(ability, new WorkerSelectionPolicySaveData
            {
                mode = WorkerSelectionMode.SpecificCharacters,
                matchMode = WorkerRequirementMatchMode.All,
                sortMode = WorkerCandidateSortMode.SpecificThenBestExpectedQuality,
                specificCharacterIds = new List<string> { "character:r19:worker" },
                excludedCharacterIds = new List<string> { "character:r19:excluded" }
            });
            IReadOnlyList<string> initialErrors =
                ability.ValidateDefaultWorkerPolicy();
            RequireRejected(
                initialErrors,
                "cannot select specific characters",
                "Definition accepted a specific-character default worker mode.");
            RequireRejected(
                initialErrors,
                "cannot contain instance character IDs",
                "Definition accepted instance character IDs in its default worker policy.");

            policyField.SetValue(ability, new WorkerSelectionPolicySaveData
            {
                mode = WorkerSelectionMode.RuleSet,
                matchMode = WorkerRequirementMatchMode.Any,
                sortMode = WorkerCandidateSortMode.SpecificThenBestExpectedQuality,
                minimumSkillId = BuiltInCharacterProficiencyIds.Crafting.Value,
                minimumSkillExperience = 1
            });
            RequireClean(
                ability.ValidateDefaultWorkerPolicy(),
                "Corrected definition default worker policy");

            policyField.SetValue(ability, null);
            RequireClean(
                ability.ValidateDefaultWorkerPolicy(),
                "Null definition default worker policy");
            WorkerSelectionPolicySaveData nullFallback = ability.DefaultWorkerPolicy;
            Require(nullFallback.mode == WorkerSelectionMode.Anyone
                    && nullFallback.sortMode
                        == WorkerCandidateSortMode.SpecificThenBestExpectedQuality,
                "Null definition default worker policy changed its Anyone fallback.");

            policyField.SetValue(ability, new WorkerSelectionPolicySaveData
            {
                mode = (WorkerSelectionMode)99,
                matchMode = (WorkerRequirementMatchMode)99,
                sortMode = (WorkerCandidateSortMode)99
            });
            IReadOnlyList<string> enumErrors =
                ability.ValidateDefaultWorkerPolicy();
            RequireRejected(enumErrors, "invalid selection mode",
                "Definition accepted an undefined worker selection mode.");
            RequireRejected(enumErrors, "invalid requirement match mode",
                "Definition accepted an undefined worker requirement match mode.");
            RequireRejected(enumErrors, "invalid candidate sort mode",
                "Definition accepted an undefined worker candidate sort mode.");
        }
        finally
        {
            Object.DestroyImmediate(clone);
            Require(EditorJsonUtility.ToJson(witness) == sourceJson,
                "Worker policy fixture changed the source building asset.");
            Require(EditorUtility.IsDirty(witness) == sourceDirty,
                "Worker policy fixture changed the source building dirty state.");
        }
    }

    private static void VerifyWorkerPolicyInstanceIdRoundTrip()
    {
        RequireHiddenField<WorkerSelectionPolicySaveData>(
            "specificCharacterIds");
        RequireHiddenField<WorkerSelectionPolicySaveData>(
            "excludedCharacterIds");

        WorkerSelectionPolicySaveData source = new()
        {
            mode = WorkerSelectionMode.SpecificOrRuleSet,
            matchMode = WorkerRequirementMatchMode.Any,
            sortMode = WorkerCandidateSortMode.SpecificThenBestExpectedQuality,
            specificCharacterIds = new List<string>
            {
                "character:r19:specific-a",
                "character:r19:specific-b"
            },
            excludedCharacterIds = new List<string>
            {
                "character:r19:excluded"
            }
        };
        string json = JsonUtility.ToJson(source);
        WorkerSelectionPolicySaveData restored =
            JsonUtility.FromJson<WorkerSelectionPolicySaveData>(json)
            ?? throw new InvalidOperationException(
                "Worker policy JSON restore returned null.");
        Require(restored.mode == source.mode
                && restored.matchMode == source.matchMode
                && restored.sortMode == source.sortMode
                && restored.specificCharacterIds.SequenceEqual(
                    source.specificCharacterIds,
                    StringComparer.Ordinal)
                && restored.excludedCharacterIds.SequenceEqual(
                    source.excludedCharacterIds,
                    StringComparer.Ordinal),
            "Hidden worker instance IDs did not survive JSON round trip.");
    }

    private static void RequireReferenceKind(
        Type ownerType,
        string fieldName,
        ContentReferenceKind expectedKind,
        int expectedEmptyNumericId)
    {
        FieldInfo field = ownerType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Missing field '{ownerType.Name}.{fieldName}'.");
        ContentReferenceAttribute reference =
            field.GetCustomAttribute<ContentReferenceAttribute>();
        Require(reference != null
                && reference.Kind == expectedKind
                && reference.EmptyNumericId == expectedEmptyNumericId,
            $"'{ownerType.Name}.{fieldName}' has incorrect numeric reference metadata.");
    }

    private static void RequireHiddenLegacyField(string fieldName)
    {
        FieldInfo field = typeof(WildlifeSpeciesSO).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Missing wildlife field '{fieldName}'.");
        Require(field.GetCustomAttribute<SerializeField>() != null
                && field.GetCustomAttribute<HideInInspector>() != null,
            $"Wildlife legacy field '{fieldName}' is not serialized and hidden.");
    }

    private static void RequireHiddenField<TOwner>(string fieldName)
    {
        FieldInfo field = typeof(TOwner).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Missing field '{typeof(TOwner).Name}.{fieldName}'.");
        Require(field.GetCustomAttribute<HideInInspector>() != null,
            $"'{typeof(TOwner).Name}.{fieldName}' remains manually authorable.");
    }

    private static void SetFirstNestedInteger(
        ScriptableObject target,
        string arrayPath,
        string relativePath,
        int value)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty array = RequireProperty(serialized, arrayPath);
        Require(array.isArray && array.arraySize > 0,
            $"'{arrayPath}' has no fixture row.");
        for (int index = 0; index < array.arraySize; index++)
        {
            SerializedProperty nested = array.GetArrayElementAtIndex(index)
                .FindPropertyRelative(relativePath);
            if (nested == null) continue;
            if (nested.isArray)
            {
                if (nested.arraySize == 0) continue;
                nested.GetArrayElementAtIndex(0).intValue = value;
            }
            else
            {
                nested.intValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return;
        }
        throw new InvalidOperationException(
            $"'{arrayPath}.{relativePath}' has no writable fixture value.");
    }

    private static void SetFirstArrayInteger(
        ScriptableObject target,
        string path,
        int value)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty array = RequireProperty(serialized, path);
        Require(array.isArray, $"'{path}' is not an array.");
        if (array.arraySize == 0) array.arraySize = 1;
        array.GetArrayElementAtIndex(0).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInteger(
        ScriptableObject target,
        string path,
        int value)
    {
        using SerializedObject serialized = new(target);
        RequireProperty(serialized, path).intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(
        ScriptableObject target,
        string path,
        string value)
    {
        using SerializedObject serialized = new(target);
        RequireProperty(serialized, path).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ClearArray(ScriptableObject target, string path)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty array = RequireProperty(serialized, path);
        Require(array.isArray, $"'{path}' is not an array.");
        array.arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string path) => serialized.FindProperty(path)
        ?? throw new InvalidOperationException(
            $"Serialized property '{path}' was not found on '{serialized.targetObject.name}'.");

    private static void RequireRejected(
        IEnumerable<string> errors,
        string expectedText,
        string failure)
    {
        string[] values = errors?.ToArray() ?? Array.Empty<string>();
        Require(values.Any(value => value.Contains(
                    expectedText,
                    StringComparison.OrdinalIgnoreCase)),
            failure + " Errors: " + string.Join(" | ", values));
    }

    private static void RequireClean(
        IEnumerable<string> errors,
        string context)
    {
        string[] values = errors?.ToArray() ?? Array.Empty<string>();
        Require(values.Length == 0,
            context + " failed: " + string.Join(" | ", values));
    }

    private static void Restore(ScriptableObject clone, string json)
    {
        EditorJsonUtility.FromJsonOverwrite(json, clone);
    }

    private static T RequireAsset<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException(
            $"Required asset '{path}' was not found.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
