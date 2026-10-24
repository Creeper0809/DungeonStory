using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RegisteredSortingLayerAttribute))]
public sealed class R20SortingLayerDrawer : PropertyDrawer
{
    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(
                position,
                label,
                new GUIContent("Registered sorting layers require a string field."));
            return;
        }

        string[] registeredNames = GetRegisteredLayerNames();
        string current = property.stringValue ?? string.Empty;
        bool mixed = property.hasMultipleDifferentValues;
        int registeredIndex = Array.FindIndex(
            registeredNames,
            value => string.Equals(value, current, StringComparison.Ordinal));
        bool preserveExisting = mixed || registeredIndex < 0;

        var labels = new List<string>();
        var values = new List<string>();
        if (preserveExisting)
        {
            labels.Add(mixed
                ? "Mixed values - select to replace all"
                : current.Length == 0
                    ? "Unregistered blank value - preserved"
                    : $"Unregistered: {current} - preserved");
            values.Add(null);
        }

        labels.AddRange(registeredNames);
        values.AddRange(registeredNames);

        EditorGUI.BeginProperty(position, label, property);
        bool previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = mixed;
        try
        {
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(
                position,
                label,
                preserveExisting ? 0 : registeredIndex,
                labels.Select(value => new GUIContent(value)).ToArray());
            if (EditorGUI.EndChangeCheck()
                && selected >= 0
                && selected < values.Count
                && values[selected] != null
                && !TryAssignRegisteredLayer(
                    property,
                    values[selected],
                    out string error))
            {
                Debug.LogWarning(error, property.serializedObject.targetObject);
            }
        }
        finally
        {
            EditorGUI.showMixedValue = previousMixed;
            EditorGUI.EndProperty();
        }
    }

    internal static bool TryAssignRegisteredLayer(
        SerializedProperty property,
        string layerName,
        out string error)
    {
        error = string.Empty;
        if (property == null
            || property.propertyType != SerializedPropertyType.String)
        {
            error = "Sorting layer selection requires a serialized string field.";
            return false;
        }

        if (!GetRegisteredLayerNames().Contains(layerName, StringComparer.Ordinal))
        {
            error = $"Sorting layer '{layerName ?? "<null>"}' is not registered. "
                + "The existing value was preserved.";
            return false;
        }

        property.stringValue = layerName;
        return true;
    }

    internal static string[] GetRegisteredLayerNames()
    {
        return SortingLayer.layers
            .Select(layer => layer.name)
            .ToArray();
    }
}
