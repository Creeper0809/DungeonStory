using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R20PresentationDebugScenarios
{
    private const string PresentationCatalogPath =
        "Assets/Resources/SO/Presentation/WorldInteractionPresentationCatalog.asset";
    private const string InvalidSortingLayer = "R20 Missing Sorting Layer";

    [MenuItem("DungeonStory/Debug/R20/Run Presentation Authoring Checks")]
    public static void RunAll()
    {
        VerifyPropProfileResolution();
        VerifyLightingLayerResolution();
        VerifySortingLayerAuthoring();
        Debug.Log(
            "R20PresentationDebugScenarios passed: species/anatomy/default priority, "
            + "registered lighting layers, invalid rejection, pending values, Undo, "
            + "and source preservation.");
    }

    private static void VerifyPropProfileResolution()
    {
        WorldInteractionPresentationCatalogSO source =
            AssetDatabase.LoadAssetAtPath<WorldInteractionPresentationCatalogSO>(
                PresentationCatalogPath)
            ?? throw new InvalidOperationException(
                $"Required presentation catalog '{PresentationCatalogPath}' was not found.");
        string sourceJson = EditorJsonUtility.ToJson(source);
        bool sourceDirty = EditorUtility.IsDirty(source);
        WorldInteractionPresentationCatalogSO clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            SetPropProfileRows(
                clone,
                ("default", CharacterCarryVisualKind.Crate),
                ("anatomy:r20:fixture", CharacterCarryVisualKind.Crate),
                ("species:r20:fixture", CharacterCarryVisualKind.Crate));

            CharacterPropAttachmentProfile species = clone.ResolvePropProfile(
                "species:r20:fixture",
                "anatomy:r20:fixture",
                CharacterCarryVisualKind.Crate);
            Require(
                species.speciesOrAnatomyId == "species:r20:fixture",
                "Species-specific carry profile did not win over anatomy.");

            CharacterPropAttachmentProfile anatomy = clone.ResolvePropProfile(
                "species:r20:unknown",
                "anatomy:r20:fixture",
                CharacterCarryVisualKind.Crate);
            Require(
                anatomy.speciesOrAnatomyId == "anatomy:r20:fixture",
                "Anatomy carry profile was not selected after a species miss.");

            CharacterPropAttachmentProfile fallback = clone.ResolvePropProfile(
                "species:r20:unknown",
                "anatomy:r20:unknown",
                CharacterCarryVisualKind.Crate);
            Require(
                fallback.speciesOrAnatomyId == "default",
                "Carry profile resolution lost the intentional default.");

            CharacterPropAttachmentProfile compatible = clone.ResolvePropProfile(
                "anatomy:r20:fixture",
                CharacterCarryVisualKind.Crate);
            Require(
                compatible.speciesOrAnatomyId == "anatomy:r20:fixture",
                "The existing two-argument carry profile API changed behavior.");
        }
        finally
        {
            Object.DestroyImmediate(clone);
            Require(
                EditorJsonUtility.ToJson(source) == sourceJson,
                "Carry profile fixture changed the source catalog JSON.");
            Require(
                EditorUtility.IsDirty(source) == sourceDirty,
                "Carry profile fixture changed the source catalog dirty state.");
        }
    }

    private static void VerifyLightingLayerResolution()
    {
        string[] registeredNames = SortingLayer.layers
            .Select(layer => layer.name)
            .ToArray();
        Require(
            BuildingLightingSettingsSO.DefaultTargetSortingLayers.All(
                name => registeredNames.Contains(name, StringComparer.Ordinal)),
            "A default lighting sorting layer is not registered.");

        var ability = new BuildingLightingAbility();
        int[] noSettings = ability.GetTargetSortingLayerIds();
        RequireDefaultLayerIds(noSettings, "No-settings lighting defaults");

        BuildingLightingSettingsSO settings =
            ScriptableObject.CreateInstance<BuildingLightingSettingsSO>();
        settings.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            ability.settings = settings;
            settings.targetSortingLayers = Array.Empty<string>();
            RequireDefaultLayerIds(
                ability.GetTargetSortingLayerIds(),
                "Empty-settings lighting defaults");

            settings.targetSortingLayers = new[]
            {
                "Default",
                "DungeonHallway"
            };
            int[] valid = ability.GetTargetSortingLayerIds();
            Require(
                valid.SequenceEqual(new[]
                {
                    SortingLayer.NameToID("Default"),
                    SortingLayer.NameToID("DungeonHallway")
                }),
                "Valid lighting sorting layers did not preserve exact names and order.");

            settings.targetSortingLayers = new[] { InvalidSortingLayer };
            string invalidJson = EditorJsonUtility.ToJson(settings);
            InvalidOperationException failure = null;
            try
            {
                ability.GetTargetSortingLayerIds();
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }

            Require(
                failure != null
                    && failure.Message.Contains(
                        InvalidSortingLayer,
                        StringComparison.Ordinal),
                "An unknown lighting sorting layer was not rejected with its name.");
            Require(
                EditorJsonUtility.ToJson(settings) == invalidJson,
                "Runtime lighting validation changed the authored invalid value.");

            GameObject buildingObject = new GameObject(
                "R20 Invalid Lighting Fixture",
                typeof(BuildableObject));
            buildingObject.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                failure = null;
                try
                {
                    ModularFacilityRuntimeEffects.ConfigureLighting(
                        buildingObject.GetComponent<BuildableObject>(),
                        ability);
                }
                catch (InvalidOperationException exception)
                {
                    failure = exception;
                }

                Require(
                    failure != null
                        && buildingObject.transform.Find("RoomClippedLight") == null,
                    "Unknown sorting layer validation created a runtime light object.");
            }
            finally
            {
                Object.DestroyImmediate(buildingObject);
            }
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }

    private static void VerifySortingLayerAuthoring()
    {
        BuildingLightingSettingsSO settings =
            ScriptableObject.CreateInstance<BuildingLightingSettingsSO>();
        settings.hideFlags = HideFlags.HideAndDontSave;
        settings.targetSortingLayers = new[] { InvalidSortingLayer };
        try
        {
            Undo.ClearUndo(settings);
            string baseline = EditorJsonUtility.ToJson(settings);
            using var serialized = new SerializedObject(settings);
            serialized.Update();
            SerializedProperty layer = serialized
                .FindProperty(nameof(BuildingLightingSettingsSO.targetSortingLayers))
                ?.GetArrayElementAtIndex(0)
                ?? throw new InvalidOperationException(
                    "Lighting sorting layer fixture field was not serialized.");

            Require(
                !R20SortingLayerDrawer.TryAssignRegisteredLayer(
                    layer,
                    InvalidSortingLayer,
                    out string invalidError)
                    && invalidError.Contains("not registered", StringComparison.Ordinal)
                    && layer.stringValue == InvalidSortingLayer,
                "The sorting layer picker accepted or replaced an unknown value.");
            Require(
                EditorJsonUtility.ToJson(settings) == baseline,
                "Reading an unknown sorting layer changed source JSON.");

            const string selectedLayer = "DungeonMiddleObject";
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("R20 sorting layer selection probe");
            Require(
                R20SortingLayerDrawer.TryAssignRegisteredLayer(
                    layer,
                    selectedLayer,
                    out string selectionError),
                "A registered sorting layer could not be selected: "
                + selectionError);
            Require(
                settings.targetSortingLayers[0] == InvalidSortingLayer
                    && layer.stringValue == selectedLayer
                    && EditorJsonUtility.ToJson(settings) == baseline,
                "A pending sorting layer selection changed the object before Apply.");
            Require(
                serialized.ApplyModifiedProperties(),
                "The pending sorting layer selection was not applied.");
            Undo.CollapseUndoOperations(undoGroup);
            Require(
                settings.targetSortingLayers[0] == selectedLayer,
                "The registered sorting layer selection did not reach the fixture.");

            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            serialized.Update();
            Require(
                settings.targetSortingLayers[0] == InvalidSortingLayer
                    && EditorJsonUtility.ToJson(settings) == baseline,
                "Undo did not restore the preserved unknown sorting layer.");
        }
        finally
        {
            Undo.ClearUndo(settings);
            Object.DestroyImmediate(settings);
        }
    }

    private static void SetPropProfileRows(
        WorldInteractionPresentationCatalogSO catalog,
        params (string id, CharacterCarryVisualKind kind)[] rows)
    {
        using var serialized = new SerializedObject(catalog);
        serialized.Update();
        SerializedProperty profiles = serialized.FindProperty("propProfiles")
            ?? throw new InvalidOperationException(
                "Presentation catalog propProfiles field was not serialized.");
        profiles.arraySize = rows.Length;
        for (int index = 0; index < rows.Length; index++)
        {
            SerializedProperty row = profiles.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("speciesOrAnatomyId").stringValue =
                rows[index].id;
            row.FindPropertyRelative("carryKind").intValue =
                (int)rows[index].kind;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void RequireDefaultLayerIds(int[] actual, string context)
    {
        int[] expected = BuildingLightingSettingsSO.DefaultTargetSortingLayers
            .Select(SortingLayer.NameToID)
            .ToArray();
        Require(
            actual.SequenceEqual(expected),
            context + " changed. Expected=" + string.Join(",", expected)
            + "; Actual=" + string.Join(",", actual));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
