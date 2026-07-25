using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomPropertyDrawer(typeof(BlueprintUnlockCollection))]
public sealed class BlueprintUnlockListDrawer : PropertyDrawer
{
    private readonly Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();

    private static readonly Type[] UnlockTypes = TypeCache
        .GetTypesDerivedFrom<BlueprintUnlock>()
        .Where(type => type.Assembly == typeof(BlueprintUnlock).Assembly
            && !type.IsAbstract
            && !type.IsGenericTypeDefinition
            && type.GetConstructor(Type.EmptyTypes) != null)
        .OrderBy(GetDisplayName)
        .ToArray();

    internal static IReadOnlyList<Type> AddableTypes => UnlockTypes;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty items = property.FindPropertyRelative("items");
        return items != null && items.isArray
            ? GetOrCreateList(items).GetHeight()
            : EditorGUIUtility.singleLineHeight * 2f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty items = property.FindPropertyRelative("items");
        if (items == null || !items.isArray)
        {
            EditorGUI.HelpBox(position, "해금 목록 데이터를 찾을 수 없습니다.", MessageType.Error);
            return;
        }

        GetOrCreateList(items).DoList(position);
    }

    internal ReorderableList GetOrCreateList(SerializedProperty property)
    {
        string key = $"{property.serializedObject.targetObject.GetInstanceID()}:{property.propertyPath}";
        if (!lists.TryGetValue(key, out ReorderableList list))
        {
            list = CreateList(property);
            lists.Add(key, list);
        }

        return list;
    }

    private static ReorderableList CreateList(SerializedProperty property)
    {
        ReorderableList list = new ReorderableList(
            property.serializedObject,
            property,
            draggable: true,
            displayHeader: true,
            displayAddButton: false,
            displayRemoveButton: false);

        list.drawHeaderCallback = rect => DrawHeader(rect, property, list);
        list.elementHeightCallback = index => GetElementHeight(property, index);
        list.drawElementCallback = (rect, index, active, focused) => DrawElement(property, rect, index);
        list.drawNoneElementCallback = rect => EditorGUI.LabelField(rect, "등록된 해금이 없습니다. + 버튼으로 추가하세요.");
        return list;
    }

    private static void DrawHeader(Rect rect, SerializedProperty property, ReorderableList list)
    {
        const float buttonSize = 18f;
        GUIContent header = new GUIContent($"해금 목록 ({property.arraySize})", "연구 완료 시 위에서부터 적용합니다.");
        float labelWidth = Mathf.Min(
            EditorStyles.label.CalcSize(header).x + 6f,
            Mathf.Max(0f, rect.width - buttonSize * 2f - 2f));
        Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
        Rect addRect = new Rect(labelRect.xMax, rect.y, buttonSize, rect.height);
        Rect removeRect = new Rect(addRect.xMax + 2f, rect.y, buttonSize, rect.height);
        EditorGUI.LabelField(labelRect, header);

        if (GUI.Button(addRect, new GUIContent("+", "해금 추가"), EditorStyles.miniButton))
        {
            ShowAddMenu(property);
        }

        bool previousEnabled = GUI.enabled;
        GUI.enabled = list.index >= 0 && list.index < property.arraySize;
        if (GUI.Button(removeRect, new GUIContent("-", "선택한 해금 삭제"), EditorStyles.miniButton))
        {
            RemoveSelected(property, list.index);
        }

        GUI.enabled = previousEnabled;
    }

    private static float GetElementHeight(SerializedProperty property, int index)
    {
        if (index < 0 || index >= property.arraySize)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        SerializedProperty element = property.GetArrayElementAtIndex(index);
        return EditorGUI.GetPropertyHeight(element, GetElementLabel(element), true) + 4f;
    }

    private static void DrawElement(SerializedProperty property, Rect rect, int index)
    {
        if (index < 0 || index >= property.arraySize)
        {
            return;
        }

        rect.y += 2f;
        rect.height -= 4f;
        SerializedProperty element = property.GetArrayElementAtIndex(index);
        EditorGUI.PropertyField(rect, element, GetElementLabel(element), true);
    }

    private static GUIContent GetElementLabel(SerializedProperty element)
    {
        object value = element.managedReferenceValue;
        return value == null
            ? new GUIContent("비어 있는 해금")
            : new GUIContent(GetDisplayName(value.GetType()), value.GetType().FullName);
    }

    private static void ShowAddMenu(SerializedProperty property)
    {
        if (property.serializedObject.isEditingMultipleObjects)
        {
            Debug.LogWarning("해금 추가는 설계도 하나를 선택했을 때만 사용할 수 있습니다.");
            return;
        }

        UnityEngine.Object target = property.serializedObject.targetObject;
        string propertyPath = property.propertyPath;
        GenericMenu menu = new GenericMenu();
        foreach (Type unlockType in UnlockTypes)
        {
            Type capturedType = unlockType;
            menu.AddItem(
                new GUIContent(GetDisplayName(unlockType)),
                false,
                () => AddUnlock(target, propertyPath, capturedType));
        }

        if (UnlockTypes.Length == 0)
        {
            menu.AddDisabledItem(new GUIContent("추가 가능한 해금이 없습니다."));
        }

        menu.ShowAsContext();
    }

    internal static bool AddUnlock(UnityEngine.Object target, string propertyPath, Type unlockType)
    {
        if (target == null || unlockType == null || !UnlockTypes.Contains(unlockType))
        {
            return false;
        }

        Undo.RecordObject(target, "Add Blueprint Unlock");
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty list = serializedObject.FindProperty(propertyPath);
        if (list == null || !list.isArray)
        {
            return false;
        }

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).managedReferenceValue = Activator.CreateInstance(unlockType);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        return true;
    }

    internal static bool RemoveSelected(SerializedProperty property, int index)
    {
        if (property == null || index < 0 || index >= property.arraySize)
        {
            return false;
        }

        Undo.RecordObjects(property.serializedObject.targetObjects, "Remove Blueprint Unlock");
        property.DeleteArrayElementAtIndex(index);
        property.serializedObject.ApplyModifiedProperties();
        foreach (UnityEngine.Object target in property.serializedObject.targetObjects)
        {
            EditorUtility.SetDirty(target);
        }

        return true;
    }

    private static string GetDisplayName(Type type)
    {
        return type.GetCustomAttributes(typeof(BlueprintUnlockDisplayNameAttribute), false)
            .OfType<BlueprintUnlockDisplayNameAttribute>()
            .FirstOrDefault()?.DisplayName
            ?? ObjectNames.NicifyVariableName(type.Name);
    }
}
