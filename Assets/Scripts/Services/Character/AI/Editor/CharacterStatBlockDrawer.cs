using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomPropertyDrawer(typeof(CharacterStatBlock))]
public sealed class CharacterStatBlockDrawer : PropertyDrawer
{
    private readonly Dictionary<string, ReorderableList> lists =
        new Dictionary<string, ReorderableList>();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect foldoutRect = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
        if (property.isExpanded)
        {
            SerializedProperty entries = property.FindPropertyRelative("entries");
            if (entries != null)
            {
                ReorderableList list = GetOrCreateList(entries);
                Rect listRect = new Rect(
                    position.x,
                    foldoutRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                    position.width,
                    list.GetHeight());
                list.DoList(listRect);
            }
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return height;
        }

        SerializedProperty entries = property.FindPropertyRelative("entries");
        return entries != null
            ? height + EditorGUIUtility.standardVerticalSpacing + GetOrCreateList(entries).GetHeight()
            : height;
    }

    private ReorderableList GetOrCreateList(SerializedProperty entries)
    {
        string key = $"{entries.serializedObject.targetObject.GetInstanceID()}:{entries.propertyPath}";
        if (lists.TryGetValue(key, out ReorderableList cached))
        {
            return cached;
        }

        ReorderableList list = new ReorderableList(
            entries.serializedObject,
            entries,
            draggable: true,
            displayHeader: true,
            displayAddButton: true,
            displayRemoveButton: true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "능력치 목록");
        list.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
        list.drawElementCallback = (rect, index, active, focused) =>
            DrawElement(entries, rect, index);
        list.onAddDropdownCallback = (rect, owner) => ShowAddMenu(entries);
        lists[key] = list;
        return list;
    }

    private static void DrawElement(SerializedProperty entries, Rect rect, int index)
    {
        if (index < 0 || index >= entries.arraySize)
        {
            return;
        }

        SerializedProperty element = entries.GetArrayElementAtIndex(index);
        SerializedProperty idProperty = element.FindPropertyRelative("statId");
        SerializedProperty valueProperty = element.FindPropertyRelative("value");
        if (idProperty == null || valueProperty == null)
        {
            return;
        }

        rect.y += 2f;
        rect.height = EditorGUIUtility.singleLineHeight;
        float valueWidth = Mathf.Min(92f, rect.width * 0.3f);
        Rect idRect = new Rect(rect.x, rect.y, rect.width - valueWidth - 6f, rect.height);
        Rect valueRect = new Rect(idRect.xMax + 6f, rect.y, valueWidth, rect.height);

        string currentId = idProperty.stringValue ?? string.Empty;
        HashSet<string> usedByOthers = GetUsedIds(entries, index);
        List<CharacterStatDefinition> definitions = CharacterStatCatalog.All
            .Where(definition => string.Equals(definition.Id, currentId, StringComparison.Ordinal)
                || !usedByOthers.Contains(definition.Id))
            .ToList();
        int currentIndex = definitions.FindIndex(
            definition => string.Equals(definition.Id, currentId, StringComparison.Ordinal));
        string[] labels;
        int popupIndex;
        if (currentIndex >= 0)
        {
            labels = definitions.Select(definition => definition.DisplayName).ToArray();
            popupIndex = currentIndex;
        }
        else
        {
            labels = new[] { $"Unregistered ({currentId})" }
                .Concat(definitions.Select(definition => definition.DisplayName))
                .ToArray();
            popupIndex = 0;
        }

        int selected = EditorGUI.Popup(idRect, popupIndex, labels);
        if (currentIndex >= 0 && selected >= 0 && selected < definitions.Count)
        {
            idProperty.stringValue = definitions[selected].Id;
        }
        else if (currentIndex < 0 && selected > 0 && selected <= definitions.Count)
        {
            idProperty.stringValue = definitions[selected - 1].Id;
        }

        valueProperty.intValue = EditorGUI.IntField(valueRect, valueProperty.intValue);
    }

    private static void ShowAddMenu(SerializedProperty entries)
    {
        HashSet<string> used = GetUsedIds(entries, -1);
        CharacterStatDefinition[] available = CharacterStatCatalog.All
            .Where(definition => !used.Contains(definition.Id))
            .ToArray();
        GenericMenu menu = new GenericMenu();
        if (available.Length == 0)
        {
            menu.AddDisabledItem(new GUIContent("추가 가능한 능력치 없음"));
        }
        else
        {
            foreach (CharacterStatDefinition definition in available)
            {
                CharacterStatDefinition captured = definition;
                menu.AddItem(new GUIContent(captured.DisplayName), false, () =>
                {
                    entries.serializedObject.Update();
                    int index = entries.arraySize;
                    entries.InsertArrayElementAtIndex(index);
                    SerializedProperty element = entries.GetArrayElementAtIndex(index);
                    element.FindPropertyRelative("statId").stringValue = captured.Id;
                    element.FindPropertyRelative("value").intValue = 0;
                    entries.serializedObject.ApplyModifiedProperties();
                });
            }
        }

        menu.ShowAsContext();
    }

    private static HashSet<string> GetUsedIds(SerializedProperty entries, int ignoredIndex)
    {
        HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < entries.arraySize; index++)
        {
            if (index == ignoredIndex)
            {
                continue;
            }

            SerializedProperty id = entries
                .GetArrayElementAtIndex(index)
                .FindPropertyRelative("statId");
            if (id != null && !string.IsNullOrWhiteSpace(id.stringValue))
            {
                used.Add(id.stringValue);
            }
        }

        return used;
    }
}
