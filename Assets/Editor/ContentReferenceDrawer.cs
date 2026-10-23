using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Select definitions in the Inspector without changing the serialized ID contract.
[CustomPropertyDrawer(typeof(ContentReferenceAttribute))]
public sealed class ContentReferenceDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
        ContentReferenceSelection.ResolveReference((ContentReferenceAttribute)attribute, property) == null
            ? EditorGUIUtility.singleLineHeight
            : EditorGUIUtility.singleLineHeight * 2 + 2;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var reference = (ContentReferenceAttribute)attribute;
        if (ContentReferenceSelection.HasMixedContext(reference, property))
        {
            EditorGUI.LabelField(position, label.text, "서로 다른 대상 종류 — 개별 선택 필요");
            return;
        }
        if (property.name == "homeFactionId" && property.serializedObject.targetObject is CharacterSpeciesDefinitionSO species
            && species.ownerSelectable)
        {
            EditorGUI.LabelField(position, label.text, "플레이어가 선택하는 세력 (기존 ID 보존)");
            return;
        }
        ContentReferenceKind requested = reference.Kind;
        var resolved = ContentReferenceSelection.ResolveReference(reference, property);
        ContentReferenceKind? kind = resolved?.Kind;
        if (kind == null)
        {
            // Free-form effect labels and non-SO faction reaction keys are not references.
            EditorGUI.PropertyField(position, property, label);
            return;
        }
        EditorGUI.BeginProperty(position, label, property);
        bool previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
        try
        {
            var entries = ContentReferenceSelection.Entries(resolved, property);
            var aliases = (reference.Aliases ?? string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            string current = ContentReferenceSelection.ReadId(property, kind.Value, reference.EmptyNumericId);
            var matches = entries.Where(value => ContentReferenceCatalog.Matches(resolved, value.Id, current)).ToArray();
            bool contextual = requested == ContentReferenceKind.EffectTarget && kind == ContentReferenceKind.Faction;
            bool contextId = contextual && (current == "affected-faction" || current == "requesting-faction");
            bool valid = current.Length == 0 || contextId || aliases.Any(value => ContentReferenceCatalog.Matches(resolved, value, current)) || matches.Length == 1
                || kind == ContentReferenceKind.AnatomyNode && matches.Length > 0;
            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            if (kind == ContentReferenceKind.AnatomyNode || kind == ContentReferenceKind.ExpeditionChoice || contextual || aliases.Length > 0)
            {
                var options = new List<(string Id, string Label)> { ("", "None") };
                if (contextual)
                {
                    options.Add(("affected-faction", "발생한 사건의 세력 (affected-faction)"));
                    options.Add(("requesting-faction", "요청한 세력 (requesting-faction)"));
                }
                options.AddRange(aliases.Select(value => (value, "호환/문맥 키: " + value)));
                options.AddRange(entries.Select(value => (value.Id, value.Asset.name + " / " + value.Id)));
                int selected = options.FindIndex(value => ContentReferenceCatalog.Matches(resolved, value.Id, current));
                if (selected < 0)
                {
                    options.Insert(0, (current, "끊어진 참조: " + current));
                    selected = 0;
                }
                EditorGUI.BeginChangeCheck();
                int next = EditorGUI.Popup(line, label.text, selected, options.Select(value => value.Label).ToArray());
                if (EditorGUI.EndChangeCheck()) ContentReferenceSelection.WriteId(property, kind.Value, options[next].Id, reference.EmptyNumericId);
            }
            else
            {
                ScriptableObject selected = matches.Length == 1 ? matches[0].Asset : null;
                EditorGUI.BeginChangeCheck();
                var next = EditorGUI.ObjectField(line, label, selected,
                    ContentReferenceSelection.AssetType(resolved), false) as ScriptableObject;
                if (EditorGUI.EndChangeCheck())
                {
                    if (!ContentReferenceSelection.TryAssign(property, resolved, next, out string error))
                        Debug.LogWarning(error, property.serializedObject.targetObject);
                }
            }
            line.y += EditorGUIUtility.singleLineHeight + 2;
            string status = property.hasMultipleDifferentValues ? "서로 다른 ID (선택 시 함께 변경)"
                : valid ? (current.Length == 0 ? "선택 없음" : current)
                : "끊어졌거나 중복된 ID: " + current + " — 기존 값 보존";
            EditorGUI.LabelField(line, status, valid ? EditorStyles.miniLabel : EditorStyles.boldLabel);
        }
        finally
        {
            EditorGUI.showMixedValue = previousMixed;
            EditorGUI.EndProperty();
        }
    }
}

[InitializeOnLoad]
public static class ContentReferenceSelection
{
    private static ContentReferenceCatalog catalog;
    private static readonly Dictionary<string, Entry[]> entries = new(StringComparer.Ordinal);

    static ContentReferenceSelection()
    {
        EditorApplication.projectChanged += Invalidate;
        Undo.undoRedoPerformed += Invalidate;
        Undo.postprocessModifications += modifications => { Invalidate(); return modifications; };
    }

    public static void Invalidate()
    {
        catalog = null;
        entries.Clear();
    }

    public readonly struct Entry
    {
        public readonly ScriptableObject Asset;
        public readonly string Id;
        public Entry(ScriptableObject asset, string id) { Asset = asset; Id = id; }
    }

    public static ContentReferenceKind? ResolveKind(ContentReferenceKind kind, SerializedProperty property)
    {
        if (kind == ContentReferenceKind.WeightedTarget)
        {
            if (property.propertyPath.Contains("relatedEventWeights.")) return ContentReferenceKind.LifeEvent;
            if (property.propertyPath.Contains("otherCultureAttitudes.")) return ContentReferenceKind.Culture;
            return null;
        }
        if (kind != ContentReferenceKind.EffectTarget) return kind;
        int dot = property.propertyPath.LastIndexOf('.');
        var effectKind = property.serializedObject.FindProperty(property.propertyPath.Substring(0, dot + 1) + "kind");
        if (effectKind == null || effectKind.hasMultipleDifferentValues) return null;
        return (V20ContentEffectKind)effectKind.intValue switch
        {
            V20ContentEffectKind.ItemGrant or V20ContentEffectKind.ItemConsume => ContentReferenceKind.Item,
            V20ContentEffectKind.DiseaseExposure => ContentReferenceKind.Disease,
            V20ContentEffectKind.FactionRapport or V20ContentEffectKind.FactionGrievance
                or V20ContentEffectKind.FactionObligation => ContentReferenceKind.Faction,
            _ => null
        };
    }

    public static object Owner(SerializedProperty property)
    {
        object current = property.serializedObject.targetObject;
        string[] segments = property.propertyPath.Replace(".Array.data[", "[").Split('.');
        foreach (string segment in segments.Take(segments.Length - 1))
        {
            int bracket = segment.IndexOf('[');
            string fieldName = bracket < 0 ? segment : segment.Substring(0, bracket);
            var field = ContentReferenceCatalog.SerializedFields(current.GetType()).FirstOrDefault(value => value.Name == fieldName);
            if (field == null) return null;
            current = field.GetValue(current);
            if (bracket >= 0 && current is System.Collections.IList list)
                current = list[int.Parse(segment.Substring(bracket + 1, segment.Length - bracket - 2))];
            if (current == null) return null;
        }
        return current;
    }

    public static ContentReferenceAttribute ResolveReference(ContentReferenceAttribute reference, SerializedProperty property)
    {
        if (reference.Kind == ContentReferenceKind.EffectTarget)
        {
            var kind = ResolveKind(reference.Kind, property);
            return kind == null ? null : new ContentReferenceAttribute(kind.Value, reference.EmptyNumericId)
                { Aliases = reference.Aliases, IgnoreCase = reference.IgnoreCase };
        }
        if (reference.Kind == ContentReferenceKind.NarrativeTarget)
        {
            var kind = ContextKind(property);
            if (kind == null || kind.hasMultipleDifferentValues) return null;
            var entry = new InGameNarrativeTextEntry();
            entry.Configure((InGameNarrativeTextKind)kind.intValue, string.Empty, string.Empty);
            return ContentReferenceCatalog.ResolveReference(reference, property.propertyPath, entry);
        }
        // A list element carries its containing field's metadata.
        return ContentReferenceCatalog.ResolveReference(reference, property.propertyPath, Owner(property));
    }

    private static SerializedProperty ContextKind(SerializedProperty property)
    {
        int dot = property.propertyPath.LastIndexOf('.');
        return property.serializedObject.FindProperty(property.propertyPath.Substring(0, dot + 1) + "kind");
    }

    public static bool HasMixedContext(ContentReferenceAttribute reference, SerializedProperty property) =>
        reference.Kind is ContentReferenceKind.EffectTarget or ContentReferenceKind.NarrativeTarget
            && ContextKind(property)?.hasMultipleDifferentValues == true;

    public static IReadOnlyList<Entry> Entries(ContentReferenceAttribute reference, SerializedProperty property)
    {
        var all = Entries(reference);
        if (reference.Kind == ContentReferenceKind.AnatomyNode
            && property.serializedObject.targetObject is AnatomyProfileSO profile)
            return profile.Nodes.Where(node => node != null)
                .Select(node => new Entry(profile, node.NodeId)).ToArray();
        if (reference.Kind == ContentReferenceKind.EnemyAbility && property.propertyPath.Contains("bossPhases.")
            && property.serializedObject.targetObject is EnemyArchetypeDefinitionSO enemy)
            return all.Where(value => enemy.abilityIds.Contains(value.Id)).ToArray();
        return all;
    }

    public static Type AssetType(ContentReferenceKind kind) => ContentReferenceCatalog.AssetType(kind);
    public static Type AssetType(ContentReferenceAttribute reference) => reference.Kind == ContentReferenceKind.Definition
        ? ContentReferenceCatalog.ResolveType(reference.DefinitionTypeName) : AssetType(reference.Kind);

    public static IReadOnlyList<Entry> Entries(ContentReferenceKind kind) => Entries(new ContentReferenceAttribute(kind));
    public static IReadOnlyList<Entry> Entries(ContentReferenceAttribute reference)
    {
        string key = $"{reference.Kind}:{reference.DefinitionTypeName}:{reference.IdMemberName}";
        if (entries.TryGetValue(key, out var found)) return found;
        if (catalog == null)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>("Assets/Resources/SO/GameContentCatalog.asset");
            if (root == null) return Array.Empty<Entry>();
            catalog = ContentReferenceCatalog.FromRoot(root);
        }
        return entries[key] = catalog.Entries(reference).Select(value => new Entry(value.Asset, value.Id)).ToArray();
    }

    public static string ReadId(SerializedProperty property, ContentReferenceKind kind, int emptyNumericId = -1)
    {
        if (property.propertyType == SerializedPropertyType.Integer)
            return property.intValue == (kind == ContentReferenceKind.ResearchNumeric ? 0 : emptyNumericId)
                ? string.Empty : property.intValue.ToString(CultureInfo.InvariantCulture);
        return property.stringValue ?? string.Empty;
    }

    public static void WriteId(SerializedProperty property, ContentReferenceKind kind, string id, int emptyNumericId = -1)
    {
        if (property.propertyType == SerializedPropertyType.Integer)
            property.intValue = id.Length == 0 ? (kind == ContentReferenceKind.ResearchNumeric ? 0 : emptyNumericId)
                : int.Parse(id, CultureInfo.InvariantCulture);
        else property.stringValue = id;
    }

    public static bool TryAssign(SerializedProperty property, ContentReferenceKind kind,
        ScriptableObject asset, out string error, int emptyNumericId = -1)
        => TryAssign(property, new ContentReferenceAttribute(kind, emptyNumericId), asset, out error);

    public static bool TryAssign(SerializedProperty property, ContentReferenceAttribute reference,
        ScriptableObject asset, out string error)
    {
        ContentReferenceKind kind = reference.Kind;
        int emptyNumericId = reference.EmptyNumericId;
        error = string.Empty;
        if (asset == null) { WriteId(property, kind, string.Empty, emptyNumericId); return true; }
        Entry[] entries = Entries(reference, property).Where(value => value.Asset == asset).ToArray();
        if (entries.Length != 1 || Entries(reference).Count(value => ContentReferenceCatalog.Matches(reference, value.Id, entries[0].Id)) != 1)
        {
            error = $"'{asset.name}' is not a uniquely registered {kind} definition. Existing ID preserved.";
            return false;
        }
        if (property.propertyType == SerializedPropertyType.Integer
            && entries[0].Id == (kind == ContentReferenceKind.ResearchNumeric ? 0 : emptyNumericId).ToString(CultureInfo.InvariantCulture))
        {
            error = $"'{asset.name}' uses this field's empty ID. Existing ID preserved.";
            return false;
        }
        WriteId(property, kind, entries[0].Id, emptyNumericId);
        return true;
    }
}
