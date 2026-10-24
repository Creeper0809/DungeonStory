using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R20FacilityRelationshipDebugScenarios
{
    private const string RecipeFolder =
        "Assets/Resources/SO/FacilityEvolution/P1";
    private const string UnknownPressureKey =
        "fixture:r20:unknown-facility-pressure";

    [MenuItem("DungeonStory/Debug/R20/Run Facility Relationship Checks")]
    public static void RunAll()
    {
        VerifyReferenceMetadata();
        VerifyProducerKeys();
        VerifyCurrentRecipeKeys();
        Debug.Log(
            "R20FacilityRelationshipDebugScenarios passed: metadata, current producer "
            + "keys, unknown pressure rejection, repair retry, and source preservation.");
    }

    private static void VerifyReferenceMetadata()
    {
        RequireReferenceKind(
            typeof(FacilityEvolutionRecipeSO),
            nameof(FacilityEvolutionRecipeSO.fromLineageTags),
            ContentReferenceKind.FacilityLineageTag);
        RequireReferenceKind(
            typeof(FacilityEvolutionRecipeSO),
            nameof(FacilityEvolutionRecipeSO.requiredRoomTags),
            ContentReferenceKind.RoomTag);
        RequireReferenceKind(
            typeof(FacilityEvolutionRecipeSO),
            nameof(FacilityEvolutionRecipeSO.allowedMutationTags),
            ContentReferenceKind.FacilityMutationTag);
        RequireReferenceKind(
            typeof(FacilityEvolutionValue),
            nameof(FacilityEvolutionValue.key),
            ContentReferenceKind.FacilityValueKey);
    }

    private static void VerifyProducerKeys()
    {
        RequireSequence(
            FacilityEvolutionUtility.ProducedLineageKeys,
            new[]
            {
                FacilityEvolutionTerms.Dining,
                "Shop",
                FacilityEvolutionTerms.Rest,
                FacilityEvolutionTerms.Training,
                FacilityEvolutionTerms.Research,
                FacilityEvolutionTerms.Mana,
                FacilityEvolutionTerms.Logistics,
                FacilityEvolutionTerms.Defense,
                FacilityEvolutionTerms.Combat
            },
            "Default lineage producers");
        RequireSequence(
            FacilityIdentityPressureUtility.PressureKeys,
            new[]
            {
                FacilityEvolutionTerms.Crowd,
                FacilityEvolutionTerms.Luxury,
                FacilityEvolutionTerms.Combat,
                FacilityEvolutionTerms.Outlaw,
                FacilityEvolutionTerms.Rest,
                FacilityEvolutionTerms.Service,
                FacilityEvolutionTerms.Ritual,
                FacilityEvolutionTerms.Security,
                FacilityEvolutionTerms.Logistics
            },
            "Identity pressure producers");
        RequireSequence(
            DefaultFacilityEvolutionMutationResolver.ExplicitEvidenceTags,
            new[]
            {
                FacilityEvolutionTerms.Combat,
                FacilityEvolutionTerms.Brutal,
                FacilityEvolutionTerms.Luxury,
                FacilityEvolutionTerms.Noble,
                FacilityEvolutionTerms.Security,
                FacilityEvolutionTerms.Outlaw,
                FacilityEvolutionTerms.Service,
                FacilityEvolutionTerms.Crowd
            },
            "Explicit mutation evidence tags");
    }

    private static void VerifyCurrentRecipeKeys()
    {
        FacilityEvolutionRecipeSO[] sources = AssetDatabase
            .FindAssets("t:FacilityEvolutionRecipeSO", new[] { RecipeFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>)
            .Where(value => value != null)
            .OrderBy(value => value.EffectiveId, StringComparer.Ordinal)
            .ToArray();
        Require(sources.Length == 6,
            $"Expected 6 facility evolution recipes, found {sources.Length}.");

        string[] sourceJson = sources
            .Select(value => EditorJsonUtility.ToJson(value))
            .ToArray();
        bool[] sourceDirty = sources
            .Select(EditorUtility.IsDirty)
            .ToArray();
        try
        {
            Require(
                sources.All(source =>
                    (source.fromLineageTags?.Length ?? 0) == 0
                    && (source.requiredRoomTags?.Length ?? 0) == 0),
                "Current empty lineage or room-tag fields changed.");

            string[] mutations = sources
                .SelectMany(source =>
                    source.allowedMutationTags ?? Array.Empty<string>())
                .ToArray();
            string[] mutationKeys = mutations
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            HashSet<string> knownRelationshipKeys =
                FacilityEvolutionUtility.ProducedLineageKeys
                    .Concat(FacilityIdentityPressureUtility.PressureKeys)
                    .Concat(DefaultFacilityEvolutionMutationResolver
                        .ExplicitEvidenceTags)
                    .ToHashSet(StringComparer.Ordinal);
            Require(
                mutations.Length == 12
                    && mutationKeys.Length == 9
                    && mutationKeys.All(knownRelationshipKeys.Contains),
                "Current mutation rows are not covered by actual relationship producers. "
                + $"rows={mutations.Length}; unique={mutationKeys.Length}; unknown="
                + string.Join(",", mutationKeys.Where(
                    key => !knownRelationshipKeys.Contains(key))));

            FacilityEvolutionValue[] weights = sources
                .SelectMany(source => source.identityPressureWeights
                    ?? Array.Empty<FacilityEvolutionValue>())
                .ToArray();
            string[] weightKeys = weights
                .Select(weight => weight.key)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Require(
                weights.Length == 13
                    && weightKeys.Length == 6
                    && weightKeys.All(key =>
                        FacilityIdentityPressureUtility.PressureKeys.Contains(
                            key,
                            StringComparer.Ordinal)),
                "Current identity pressure weights are not covered by pressure producers. "
                + $"rows={weights.Length}; unique={weightKeys.Length}.");

            VerifyUnknownPressureRepair(sources[0]);
        }
        finally
        {
            for (int index = 0; index < sources.Length; index++)
            {
                Require(
                    EditorJsonUtility.ToJson(sources[index]) == sourceJson[index],
                    $"Facility relationship fixture changed source JSON for "
                    + $"'{sources[index].name}'.");
                Require(
                    EditorUtility.IsDirty(sources[index]) == sourceDirty[index],
                    $"Facility relationship fixture changed source dirty state for "
                    + $"'{sources[index].name}'.");
            }
        }
    }

    private static void VerifyUnknownPressureRepair(
        FacilityEvolutionRecipeSO source)
    {
        FacilityEvolutionRecipeSO clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        string baseline = EditorJsonUtility.ToJson(clone);
        try
        {
            using (var serialized = new SerializedObject(clone))
            {
                serialized.Update();
                SerializedProperty weights = serialized.FindProperty(
                    nameof(FacilityEvolutionRecipeSO.identityPressureWeights));
                Require(weights != null && weights.arraySize > 0,
                    "Identity pressure fixture has no weight row.");
                SerializedProperty key = weights.GetArrayElementAtIndex(0)
                    .FindPropertyRelative(nameof(FacilityEvolutionValue.key));
                Require(key != null,
                    "Identity pressure weight key was not serialized.");
                key.stringValue = UnknownPressureKey;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            InvalidOperationException failure = null;
            try
            {
                FacilityIdentityPressureUtility.ScoreRecipe(
                    new RoomProfile(null, null),
                    clone);
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            Require(
                failure != null
                    && failure.Message.Contains(
                        UnknownPressureKey,
                        StringComparison.Ordinal),
                "Unknown identity pressure weight was not rejected with its key.");

            EditorJsonUtility.FromJsonOverwrite(baseline, clone);
            FacilityEvolutionIdentityScore repaired =
                FacilityIdentityPressureUtility.ScoreRecipe(
                    new RoomProfile(null, null),
                    clone);
            FacilityEvolutionIdentityScore retried =
                FacilityIdentityPressureUtility.ScoreRecipe(
                    new RoomProfile(null, null),
                    clone);
            Require(
                repaired.UsesIdentityPressure
                    && retried.UsesIdentityPressure
                    && EditorJsonUtility.ToJson(clone) == baseline,
                "Identity pressure repair and retry did not restore stable behavior.");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void RequireReferenceKind(
        Type ownerType,
        string fieldName,
        ContentReferenceKind expected)
    {
        FieldInfo field = ownerType.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Missing field '{ownerType.Name}.{fieldName}'.");
        ContentReferenceAttribute reference =
            field.GetCustomAttribute<ContentReferenceAttribute>();
        Require(
            reference != null && reference.Kind == expected,
            $"'{ownerType.Name}.{fieldName}' has the wrong reference kind.");
    }

    private static void RequireSequence(
        IEnumerable<string> actual,
        IEnumerable<string> expected,
        string label)
    {
        string[] actualValues = actual?.ToArray() ?? Array.Empty<string>();
        string[] expectedValues = expected?.ToArray() ?? Array.Empty<string>();
        Require(
            actualValues.SequenceEqual(expectedValues, StringComparer.Ordinal),
            label + " mismatch. Expected=" + string.Join(",", expectedValues)
            + "; Actual=" + string.Join(",", actualValues));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
