using System;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ResearchPrerequisiteLink))]
public sealed class ResearchPrerequisiteLinkDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;
    private static float StatusHeight => EditorGUIUtility.singleLineHeight * 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        SerializedProperty kind = property.FindPropertyRelative("kind");
        SerializedProperty reason = property.FindPropertyRelative("reason");
        height += VerticalSpacing + EditorGUIUtility.singleLineHeight;
        height += VerticalSpacing + StatusHeight;
        height += VerticalSpacing + EditorGUI.GetPropertyHeight(kind, includeChildren: true);
        height += VerticalSpacing + EditorGUI.GetPropertyHeight(reason, includeChildren: true);
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        try
        {
            Rect line = new Rect(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(
                line,
                property.isExpanded,
                label,
                toggleOnLabelClick: true);
            if (!property.isExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                SerializedProperty prerequisite = property.FindPropertyRelative("prerequisite");
                SerializedProperty prerequisiteId = property.FindPropertyRelative("prerequisiteId");
                SerializedProperty kind = property.FindPropertyRelative("kind");
                SerializedProperty reason = property.FindPropertyRelative("reason");

                line.y += line.height + VerticalSpacing;
                bool previousMixed = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = prerequisite?.hasMultipleDifferentValues == true;
                EditorGUI.BeginChangeCheck();
                ResearchProjectSO selected = EditorGUI.ObjectField(
                    line,
                    new GUIContent(
                        "Prerequisite",
                        "The selected research asset is the authoring authority. Its stable ID is synchronized automatically."),
                    prerequisite?.objectReferenceValue,
                    typeof(ResearchProjectSO),
                    allowSceneObjects: false) as ResearchProjectSO;
                bool changed = EditorGUI.EndChangeCheck();
                EditorGUI.showMixedValue = previousMixed;

                if (changed
                    && !TryAssignSelection(property, selected, out string error))
                {
                    Debug.LogWarning(error, property.serializedObject.targetObject);
                }

                line.y += line.height + VerticalSpacing;
                line.height = StatusHeight;
                DrawIdentityStatus(line, prerequisiteId, prerequisite);

                line.y += line.height + VerticalSpacing;
                line.height = EditorGUI.GetPropertyHeight(kind, includeChildren: true);
                EditorGUI.PropertyField(line, kind, includeChildren: true);

                line.y += line.height + VerticalSpacing;
                line.height = EditorGUI.GetPropertyHeight(reason, includeChildren: true);
                EditorGUI.PropertyField(line, reason, includeChildren: true);
            }
        }
        finally
        {
            EditorGUI.EndProperty();
        }
    }

    internal static bool TryAssignSelection(
        SerializedProperty linkProperty,
        ResearchProjectSO selection,
        out string error)
    {
        error = string.Empty;
        if (linkProperty?.serializedObject == null)
        {
            error = "Research prerequisite link is unavailable.";
            return false;
        }

        SerializedProperty prerequisiteId = linkProperty.FindPropertyRelative("prerequisiteId");
        SerializedProperty prerequisite = linkProperty.FindPropertyRelative("prerequisite");
        if (prerequisiteId == null || prerequisite == null)
        {
            error = "Research prerequisite link serialization is incomplete.";
            return false;
        }

        UnityEngine.Object[] targets = linkProperty.serializedObject.targetObjects;
        Undo.RecordObjects(targets, "Set Research Prerequisite");
        if (!ContentReferenceSelection.TryAssign(
                prerequisiteId,
                ContentReferenceKind.Research,
                selection,
                out error))
        {
            return false;
        }

        prerequisite.objectReferenceValue = selection;
        linkProperty.serializedObject.ApplyModifiedProperties();
        foreach (UnityEngine.Object target in targets)
        {
            if (target != null)
            {
                EditorUtility.SetDirty(target);
            }
        }

        return true;
    }

    internal static bool HasSynchronizedIdentity(SerializedProperty linkProperty)
    {
        SerializedProperty prerequisiteId = linkProperty?.FindPropertyRelative("prerequisiteId");
        SerializedProperty prerequisite = linkProperty?.FindPropertyRelative("prerequisite");
        if (prerequisiteId == null || prerequisite == null
            || prerequisiteId.hasMultipleDifferentValues
            || prerequisite.hasMultipleDifferentValues)
        {
            return false;
        }

        ResearchProjectSO project = prerequisite.objectReferenceValue as ResearchProjectSO;
        string authoredId = prerequisiteId.stringValue?.Trim() ?? string.Empty;
        string selectedId = project?.ProjectId.Value ?? string.Empty;
        return string.Equals(authoredId, selectedId, StringComparison.Ordinal);
    }

    private static void DrawIdentityStatus(
        Rect position,
        SerializedProperty prerequisiteId,
        SerializedProperty prerequisite)
    {
        if (prerequisiteId == null || prerequisite == null)
        {
            EditorGUI.HelpBox(position, "Research prerequisite serialization is incomplete.", MessageType.Error);
            return;
        }

        if (prerequisiteId.hasMultipleDifferentValues || prerequisite.hasMultipleDifferentValues)
        {
            EditorGUI.LabelField(position, "Mixed prerequisite values (selection updates all)", EditorStyles.miniLabel);
            return;
        }

        ResearchProjectSO project = prerequisite.objectReferenceValue as ResearchProjectSO;
        string authoredId = prerequisiteId.stringValue?.Trim() ?? string.Empty;
        string selectedId = project?.ProjectId.Value ?? string.Empty;
        if (!string.Equals(authoredId, selectedId, StringComparison.Ordinal))
        {
            EditorGUI.HelpBox(
                position,
                $"ID/object mismatch: '{authoredId}' / '{selectedId}'. Select the asset again or clear it to synchronize both fields.",
                MessageType.Error);
            return;
        }

        EditorGUI.LabelField(
            position,
            selectedId.Length == 0 ? "No prerequisite selected" : $"Stable ID: {selectedId}",
            EditorStyles.miniLabel);
    }
}
