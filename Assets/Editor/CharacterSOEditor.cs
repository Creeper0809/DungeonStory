#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[CustomEditor(typeof(CharacterSO))]
[CanEditMultipleObjects]
public sealed class CharacterSOEditor : Editor
{
    private string rejectedSelection = string.Empty;

    public override void OnInspectorGUI()
    {
        serializedObject.UpdateIfRequiredOrScript();
        SerializedProperty legacyTag = serializedObject.FindProperty("speciesTag");
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(property, true);
                continue;
            }
            if (property.propertyPath == "speciesTag") continue;
            if (property.propertyPath == "ownerFixedSkills")
            {
                DrawOwnerFixedSkills(property);
                continue;
            }
            if (property.propertyPath != "species")
            {
                EditorGUILayout.PropertyField(property, true);
                continue;
            }

            DrawSpeciesReference(property, legacyTag);
        }

        serializedObject.ApplyModifiedProperties();
        if (rejectedSelection.Length > 0)
            EditorGUILayout.HelpBox(rejectedSelection, MessageType.Error);
    }

    internal static bool IsOwnerFixedInput(string field, int formulaVersion) => formulaVersion == 0 && field is
        "id" or "displayName" or "description" or "narrativeReason" or "rarity" or "trigger" or "target"
        or "targetingMode" or "effectArea" or "areaSize" or "ultimateDomain" or "cooldownTurns"
        or "manualDurationHours" or "manualCooldownDays" or "usableFrom" or "targetPositions" or "modules";

    internal static void ResizeOwnerFixedSkills(SerializedProperty list, int size)
    {
        if (list.serializedObject.isEditingMultipleObjects)
            throw new InvalidOperationException("Resize owner skills on one character at a time.");
        int before = list.arraySize;
        list.arraySize = Mathf.Clamp(size, 0, 4);
        for (int i = before; i < list.arraySize; i++)
            list.GetArrayElementAtIndex(i).boxedValue = new CharacterSkillInstance();
    }

    private static void DrawOwnerFixedSkills(SerializedProperty list)
    {
        list.isExpanded = EditorGUILayout.Foldout(list.isExpanded, list.displayName, true);
        if (!list.isExpanded) return;
        if (list.serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.HelpBox("고정 스킬은 캐릭터를 개별 선택해서 편집하세요.", MessageType.Info);
            return;
        }
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.HelpBox("최대 4개. 빈 슬롯은 기존 기본 스킬을 사용합니다. 모듈과 변형을 목록에서 선택하세요. 생성된 수식 스냅샷은 읽기 전용입니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            int size = EditorGUILayout.IntField("스킬 개수", list.arraySize);
            if (EditorGUI.EndChangeCheck()) ResizeOwnerFixedSkills(list, size);
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty skill = list.GetArrayElementAtIndex(i);
                skill.isExpanded = EditorGUILayout.Foldout(skill.isExpanded, $"고정 스킬 {i + 1}", true);
                if (!skill.isExpanded) continue;
                int version = skill.FindPropertyRelative("formulaVersion").intValue;
                SerializedProperty showSnapshot = skill.FindPropertyRelative("formulaVersion");
                using (new EditorGUI.IndentLevelScope())
                {
                    SerializedProperty child = skill.Copy();
                    SerializedProperty end = skill.GetEndProperty();
                    if (child.NextVisible(true))
                        do
                        {
                            if (SerializedProperty.EqualContents(child, end)) break;
                            if (IsOwnerFixedInput(child.name, version)) EditorGUILayout.PropertyField(child, true);
                        } while (child.NextVisible(false));
                    showSnapshot.isExpanded = EditorGUILayout.Foldout(showSnapshot.isExpanded,
                        version > 0 ? "생성된 수식 스킬 (읽기 전용)" : "생성·저장 스냅샷 (읽기 전용)", true);
                    if (!showSnapshot.isExpanded) continue;
                    using (new EditorGUI.DisabledScope(true))
                    {
                        child = skill.Copy();
                        if (child.NextVisible(true))
                            do
                            {
                                if (SerializedProperty.EqualContents(child, end)) break;
                                if (!IsOwnerFixedInput(child.name, version)) EditorGUILayout.PropertyField(child, true);
                            } while (child.NextVisible(false));
                    }
                }
            }
        }
    }

    private void DrawSpeciesReference(
        SerializedProperty speciesProperty,
        SerializedProperty legacyTag)
    {
        EditorGUI.showMixedValue = speciesProperty.hasMultipleDifferentValues;
        EditorGUI.BeginChangeCheck();
        CharacterSpeciesSO selected = EditorGUILayout.ObjectField(
            speciesProperty.displayName,
            speciesProperty.objectReferenceValue,
            typeof(CharacterSpeciesSO),
            false) as CharacterSpeciesSO;
        bool changed = EditorGUI.EndChangeCheck();
        EditorGUI.showMixedValue = false;

        if (changed)
        {
            if (TryAssignSelection(
                    speciesProperty,
                    legacyTag,
                    selected,
                    out string failure))
            {
                rejectedSelection = string.Empty;
            }
            else
            {
                rejectedSelection = failure;
            }
        }

        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(
                legacyTag,
                new GUIContent("Species Tag (Derived)"));
    }

    internal static bool TryAssignSelection(
        SerializedProperty speciesProperty,
        SerializedProperty legacyTag,
        CharacterSpeciesSO selected,
        out string failure)
    {
        if (speciesProperty == null || legacyTag == null)
        {
            failure = "Character species authoring properties are unavailable.";
            return false;
        }
        if (!TryValidateRegisteredSpecies(selected, out failure))
            return false;

        Object[] targets = speciesProperty.serializedObject.targetObjects;
        Undo.RecordObjects(targets, "Set Character Species");
        if (!ContentReferenceSelection.TryAssign(
                legacyTag,
                ContentReferenceKind.Species,
                selected,
                out failure))
        {
            return false;
        }
        speciesProperty.objectReferenceValue = selected;
        speciesProperty.serializedObject.ApplyModifiedProperties();
        foreach (Object target in targets)
        {
            if (target != null) EditorUtility.SetDirty(target);
        }
        return true;
    }

    internal static bool TryValidateRegisteredSpecies(
        CharacterSpeciesSO selected,
        out string failure)
    {
        if (selected == null)
        {
            failure = string.Empty;
            return true;
        }
        if (!ContentReferenceSelection.Entries(ContentReferenceKind.Species)
                .Any(entry => entry.Asset == selected
                    && string.Equals(
                        entry.Id,
                        selected.speciesTag,
                        StringComparison.Ordinal)))
        {
            failure = $"Species '{selected.name}' is not registered in the game content catalog.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(selected.speciesTag)
            || !string.Equals(
                selected.speciesTag,
                selected.speciesTag.Trim(),
                StringComparison.Ordinal))
        {
            failure = $"Species '{selected.name}' has no canonical species tag.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    [MenuItem("DungeonStory/Debug/Character/Run Character Species Authoring Audit")]
    public static void RunAuthoringAudit()
    {
        CharacterSpeciesSO[] registered = ContentReferenceSelection
            .Entries(ContentReferenceKind.Species)
            .Select(entry => entry.Asset as CharacterSpeciesSO)
            .Where(value => value != null)
            .Distinct()
            .OrderBy(value => value.speciesTag, StringComparer.Ordinal)
            .ToArray();
        Require(registered.Length > 0, "No character species are registered.");

        CharacterSO fixture = ScriptableObject.CreateInstance<CharacterSO>();
        CharacterSpeciesSO outsideRoot = ScriptableObject
            .CreateInstance<CharacterSpeciesSO>();
        outsideRoot.name = "OutsideRootSpecies";
        outsideRoot.speciesTag = "OutsideRoot";
        try
        {
            VerifySelectionClearAndUndo(fixture, registered[0]);
            VerifyOutsideRootRejection(fixture, outsideRoot);
            VerifyAuthoredCharacterAssets();
            Debug.Log(
                "CHARACTER_SPECIES_AUTHORING_AUDIT=PASS; species=11; characters=16; "
                + "selection=PASS; clear=PASS; undo=PASS; outsideRoot=REJECTED");
        }
        finally
        {
            Undo.ClearUndo(fixture);
            Object.DestroyImmediate(fixture);
            Object.DestroyImmediate(outsideRoot);
        }
    }

    private static void VerifySelectionClearAndUndo(
        CharacterSO fixture,
        CharacterSpeciesSO registered)
    {
        SerializedObject serialized = new(fixture);
        Require(TryAssignSelection(
                serialized.FindProperty("species"),
                serialized.FindProperty("speciesTag"),
                registered,
                out string selectionFailure),
            selectionFailure);
        serialized.Update();
        Require(fixture.species == registered
                && string.Equals(
                    fixture.speciesTag,
                    registered.speciesTag,
                    StringComparison.Ordinal),
            "Selecting a registered species did not synchronize the legacy tag.");

        Undo.ClearUndo(fixture);
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Character Species Authoring Scenario");
        Require(TryAssignSelection(
                serialized.FindProperty("species"),
                serialized.FindProperty("speciesTag"),
                null,
                out string clearFailure),
            clearFailure);
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(undoGroup);
        serialized.Update();
        Require(fixture.species == null && string.IsNullOrEmpty(fixture.speciesTag),
            "Clearing the species did not clear the legacy tag.");

        Undo.PerformUndo();
        serialized.Update();
        Require(fixture.species == registered
                && string.Equals(
                    fixture.speciesTag,
                    registered.speciesTag,
                    StringComparison.Ordinal),
            "Undo did not restore the synchronized species object and tag.");
    }

    private static void VerifyOutsideRootRejection(
        CharacterSO fixture,
        CharacterSpeciesSO outsideRoot)
    {
        CharacterSpeciesSO originalSpecies = fixture.species;
        string originalTag = fixture.speciesTag;
        SerializedObject serialized = new(fixture);
        Require(!TryAssignSelection(
                serialized.FindProperty("species"),
                serialized.FindProperty("speciesTag"),
                outsideRoot,
                out string failure)
                && failure.Contains("outside", StringComparison.OrdinalIgnoreCase),
            "An unregistered species object was not rejected explicitly.");
        serialized.Update();
        Require(fixture.species == originalSpecies
                && string.Equals(fixture.speciesTag, originalTag, StringComparison.Ordinal),
            "Rejecting an unregistered species changed the character definition.");
    }

    private static void VerifyAuthoredCharacterAssets()
    {
        CharacterSO[] authored = AssetDatabase.FindAssets("t:CharacterSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CharacterSO>)
            .Where(value => value != null)
            .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
            .ToArray();
        Require(authored.Length == 16,
            $"Expected 16 authored characters, found {authored.Length}.");
        foreach (CharacterSO character in authored)
        {
            Require(TryValidateRegisteredSpecies(character.species, out string failure),
                $"Character '{character.name}' has an invalid species reference: {failure}");
            Require(character.species != null
                    && string.Equals(
                        character.speciesTag,
                        character.species.speciesTag,
                        StringComparison.Ordinal)
                    && string.Equals(
                        character.SpeciesTag,
                        character.species.speciesTag,
                        StringComparison.Ordinal),
                $"Character '{character.name}' has an unsynchronized species tag.");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
