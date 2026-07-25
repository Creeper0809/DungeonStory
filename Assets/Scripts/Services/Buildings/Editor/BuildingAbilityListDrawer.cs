using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomPropertyDrawer(typeof(BuildingAbilityCollection))]
public sealed class BuildingAbilityListDrawer : PropertyDrawer
{
    private readonly Dictionary<string, ReorderableList> lists = new Dictionary<string, ReorderableList>();

    private static readonly Type[] AbilityTypes = TypeCache
        .GetTypesDerivedFrom<BuildingAbility>()
        .Where(type => type.IsSerializable
            && !type.IsAbstract
            && !type.IsGenericTypeDefinition
            && type.GetConstructor(Type.EmptyTypes) != null)
        .OrderBy(GetDisplayName)
        .ToArray();

    internal static IReadOnlyList<Type> AddableTypes => AbilityTypes;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (HasMissingManagedReferences(property))
        {
            return EditorGUIUtility.singleLineHeight * 3f;
        }

        SerializedProperty items = property.FindPropertyRelative("items");
        if (items == null || !items.isArray)
        {
            return EditorGUIUtility.singleLineHeight * 2f;
        }

        return GetOrCreateList(items).GetHeight();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (HasMissingManagedReferences(property))
        {
            EditorGUI.HelpBox(
                position,
                "복구되지 않은 능력 타입이 있습니다. 타입을 복구하거나 명시적으로 삭제하기 전에는 목록을 수정할 수 없습니다.",
                MessageType.Error);
            return;
        }

        SerializedProperty items = property.FindPropertyRelative("items");
        if (items == null || !items.isArray)
        {
            EditorGUI.HelpBox(position, "능력 목록 데이터를 찾을 수 없습니다.", MessageType.Error);
            return;
        }

        GetOrCreateList(items).DoList(position);
    }

    internal ReorderableList GetOrCreateList(SerializedProperty property)
    {
        UnityEngine.Object target = property.serializedObject.targetObject;
        string key = $"{target.GetInstanceID()}:{property.propertyPath}";
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

        list.drawHeaderCallback = rect =>
        {
            string header = $"능력 목록 ({property.arraySize})";
            const float buttonSize = 18f;
            GUIContent headerContent = new GUIContent(header, "추가된 능력을 위에서부터 조회합니다.");
            float controlsWidth = buttonSize * 2f + 2f;
            float preferredLabelWidth = EditorStyles.label.CalcSize(headerContent).x + 6f;
            float labelWidth = Mathf.Min(preferredLabelWidth, Mathf.Max(0f, rect.width - controlsWidth));
            Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
            Rect addRect = new Rect(labelRect.xMax, rect.y, buttonSize, rect.height);
            Rect removeRect = new Rect(addRect.xMax + 2f, rect.y, buttonSize, rect.height);
            EditorGUI.LabelField(labelRect, headerContent);

            GUIContent addContent = new GUIContent("+", "능력 추가");
            if (GUI.Button(addRect, addContent, EditorStyles.miniButton))
            {
                ShowAddMenu(property);
            }

            bool previousEnabled = GUI.enabled;
            GUI.enabled = list.index >= 0 && list.index < property.arraySize;
            GUIContent removeContent = new GUIContent("-", "선택한 능력 삭제");
            if (GUI.Button(removeRect, removeContent, EditorStyles.miniButton))
            {
                RemoveSelected(property, list.index);
            }

            GUI.enabled = previousEnabled;
        };
        list.elementHeightCallback = index => GetElementHeight(property, index);
        list.drawElementCallback = (rect, index, active, focused) => DrawElement(property, rect, index);
        list.drawNoneElementCallback = rect => EditorGUI.LabelField(rect, "등록된 능력이 없습니다. + 버튼으로 추가하세요.");
        return list;
    }

    private static float GetElementHeight(SerializedProperty listProperty, int index)
    {
        if (index < 0 || index >= listProperty.arraySize)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
        GUIContent label = GetElementLabel(element);
        return EditorGUI.GetPropertyHeight(element, label, includeChildren: true) + 4f;
    }

    private static void DrawElement(SerializedProperty listProperty, Rect rect, int index)
    {
        if (index < 0 || index >= listProperty.arraySize)
        {
            return;
        }

        rect.y += 2f;
        rect.height -= 4f;
        SerializedProperty element = listProperty.GetArrayElementAtIndex(index);
        EditorGUI.PropertyField(rect, element, GetElementLabel(element), includeChildren: true);
    }

    private static GUIContent GetElementLabel(SerializedProperty element)
    {
        object value = element.managedReferenceValue;
        if (value == null)
        {
            return new GUIContent("비어 있는 능력", "삭제한 뒤 + 버튼으로 올바른 능력을 추가하세요.");
        }

        Type type = value.GetType();
        return new GUIContent(GetDisplayName(type), type.FullName);
    }

    private static void ShowAddMenu(SerializedProperty listProperty)
    {
        if (listProperty.serializedObject.isEditingMultipleObjects)
        {
            Debug.LogWarning("능력 추가는 BuildingSO 하나를 선택했을 때만 사용할 수 있습니다.");
            return;
        }

        HashSet<Type> existingTypes = new HashSet<Type>();
        for (int index = 0; index < listProperty.arraySize; index++)
        {
            object value = listProperty.GetArrayElementAtIndex(index).managedReferenceValue;
            if (value != null)
            {
                existingTypes.Add(value.GetType());
            }
        }

        UnityEngine.Object target = listProperty.serializedObject.targetObject;
        if (target != null && SerializationUtility.HasManagedReferencesWithMissingTypes(target))
        {
            Debug.LogError($"Cannot modify building abilities on '{target.name}' while managed-reference types are missing.", target);
            return;
        }

        string propertyPath = listProperty.propertyPath;
        GenericMenu menu = new GenericMenu();
        foreach (Type abilityType in AbilityTypes)
        {
            GUIContent content = new GUIContent(GetDisplayName(abilityType));
            if (existingTypes.Contains(abilityType))
            {
                menu.AddDisabledItem(content, true);
                continue;
            }

            Type capturedType = abilityType;
            menu.AddItem(content, false, () => AddAbility(target, propertyPath, capturedType));
        }

        if (AbilityTypes.Length == 0)
        {
            menu.AddDisabledItem(new GUIContent("추가 가능한 능력이 없습니다."));
        }

        menu.ShowAsContext();
    }

    internal static bool AddAbility(UnityEngine.Object target, string propertyPath, Type abilityType)
    {
        if (target == null || abilityType == null || !AbilityTypes.Contains(abilityType))
        {
            return false;
        }

        if (SerializationUtility.HasManagedReferencesWithMissingTypes(target))
        {
            Debug.LogError($"Cannot add a building ability to '{target.name}' while managed-reference types are missing.", target);
            return false;
        }

        Undo.RecordObject(target, "Add Building Ability");
        SerializedObject serializedObject = new SerializedObject(target);
        SerializedProperty list = serializedObject.FindProperty(propertyPath);
        if (list == null || !list.isArray)
        {
            return false;
        }

        for (int existingIndex = 0; existingIndex < list.arraySize; existingIndex++)
        {
            object existing = list.GetArrayElementAtIndex(existingIndex).managedReferenceValue;
            if (existing != null && existing.GetType() == abilityType)
            {
                return false;
            }
        }

        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        SerializedProperty element = list.GetArrayElementAtIndex(index);
        element.managedReferenceValue = Activator.CreateInstance(abilityType);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        return true;
    }

    internal static bool RemoveSelected(SerializedProperty listProperty, int index)
    {
        if (listProperty == null || index < 0 || index >= listProperty.arraySize)
        {
            return false;
        }

        if (listProperty.serializedObject.targetObjects.Any(
                SerializationUtility.HasManagedReferencesWithMissingTypes))
        {
            Debug.LogError("Cannot remove building abilities while a selected asset has missing managed-reference types.");
            return false;
        }

        Undo.RecordObjects(listProperty.serializedObject.targetObjects, "Remove Building Ability");
        listProperty.DeleteArrayElementAtIndex(index);
        listProperty.serializedObject.ApplyModifiedProperties();
        foreach (UnityEngine.Object target in listProperty.serializedObject.targetObjects)
        {
            EditorUtility.SetDirty(target);
        }

        return true;
    }

    internal static string GetDisplayName(Type abilityType)
    {
        BuildingAbilityDisplayNameAttribute attribute = abilityType
            .GetCustomAttribute<BuildingAbilityDisplayNameAttribute>();
        if (attribute != null && !string.IsNullOrWhiteSpace(attribute.DisplayName))
        {
            return attribute.DisplayName;
        }

        string name = abilityType.Name;
        if (name.StartsWith("Building", StringComparison.Ordinal))
        {
            name = name.Substring("Building".Length);
        }

        if (name.EndsWith("Ability", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - "Ability".Length);
        }

        return ObjectNames.NicifyVariableName(name);
    }

    private static bool HasMissingManagedReferences(SerializedProperty property)
    {
        return property?.serializedObject?.targetObjects != null
            && property.serializedObject.targetObjects.Any(
                target => target != null && SerializationUtility.HasManagedReferencesWithMissingTypes(target));
    }
}
