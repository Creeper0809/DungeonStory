using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CharacterNeedIdAttribute))]
public sealed class CharacterNeedIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        CharacterNeedDefinition[] definitions = CharacterNeedCatalog.All.ToArray();
        string currentId = property.stringValue ?? string.Empty;
        int selectedIndex = Array.FindIndex(
            definitions,
            definition => string.Equals(definition.Id, currentId, StringComparison.Ordinal));

        string[] options;
        int popupIndex;
        if (selectedIndex >= 0)
        {
            options = definitions
                .Select(definition => $"{definition.DisplayName} ({definition.Id})")
                .ToArray();
            popupIndex = selectedIndex;
        }
        else
        {
            options = new[] { $"Unregistered ({currentId})" }
                .Concat(definitions.Select(
                    definition => $"{definition.DisplayName} ({definition.Id})"))
                .ToArray();
            popupIndex = 0;
        }

        EditorGUI.BeginProperty(position, label, property);
        int nextIndex = EditorGUI.Popup(position, label.text, popupIndex, options);
        if (selectedIndex >= 0 && nextIndex >= 0 && nextIndex < definitions.Length)
        {
            property.stringValue = definitions[nextIndex].Id;
        }
        else if (selectedIndex < 0 && nextIndex > 0 && nextIndex <= definitions.Length)
        {
            property.stringValue = definitions[nextIndex - 1].Id;
        }

        EditorGUI.EndProperty();
    }
}
