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
        ContentReferenceSelection.ResolveKind(((ContentReferenceAttribute)attribute).Kind, property) == null
            ? EditorGUIUtility.singleLineHeight
            : EditorGUIUtility.singleLineHeight * 2 + 2;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var reference = (ContentReferenceAttribute)attribute;
        ContentReferenceKind requested = reference.Kind;
        ContentReferenceKind? kind = ContentReferenceSelection.ResolveKind(requested, property);
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
            var entries = ContentReferenceSelection.Entries(kind.Value);
            string current = ContentReferenceSelection.ReadId(property, kind.Value, reference.EmptyNumericId);
            var matches = entries.Where(value => value.Id == current).ToArray();
            bool contextual = requested == ContentReferenceKind.EffectTarget && kind == ContentReferenceKind.Faction;
            bool contextId = contextual && (current == "affected-faction" || current == "requesting-faction");
            bool valid = current.Length == 0 || contextId || matches.Length == 1
                || kind == ContentReferenceKind.AnatomyNode && matches.Length > 0;
            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            if (kind == ContentReferenceKind.AnatomyNode || contextual)
            {
                var options = new List<(string Id, string Label)> { ("", "None") };
                if (contextual)
                {
                    options.Add(("affected-faction", "발생한 사건의 세력 (affected-faction)"));
                    options.Add(("requesting-faction", "요청한 세력 (requesting-faction)"));
                }
                options.AddRange(entries.Select(value => (value.Id, value.Asset.name + " / " + value.Id)));
                int selected = options.FindIndex(value => value.Id == current);
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
                    ContentReferenceSelection.AssetType(kind.Value), false) as ScriptableObject;
                if (EditorGUI.EndChangeCheck())
                {
                    if (!ContentReferenceSelection.TryAssign(property, kind.Value, next, out string error, reference.EmptyNumericId))
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

public static class ContentReferenceSelection
{
    public readonly struct Entry
    {
        public readonly ScriptableObject Asset;
        public readonly string Id;
        public Entry(ScriptableObject asset, string id) { Asset = asset; Id = id; }
    }
    private static readonly Dictionary<ContentReferenceKind, Entry[]> cache = new();
    static ContentReferenceSelection() => EditorApplication.projectChanged += cache.Clear;

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

    public static Type AssetType(ContentReferenceKind kind) => kind switch
    {
        ContentReferenceKind.Item or ContentReferenceKind.Ammunition => typeof(ItemDefinitionSO),
        ContentReferenceKind.Weapon => typeof(CombatWeaponSO),
        ContentReferenceKind.Armor => typeof(CombatArmorSO),
        ContentReferenceKind.Shield => typeof(CombatShieldSO),
        ContentReferenceKind.Building or ContentReferenceKind.BuildingNumeric => typeof(BuildingSO),
        ContentReferenceKind.Research or ContentReferenceKind.ResearchNumeric => typeof(ResearchProjectSO),
        ContentReferenceKind.Recipe => typeof(ScriptableObject),
        ContentReferenceKind.Species => typeof(CharacterSpeciesDefinitionSO),
        ContentReferenceKind.AnatomyProfile or ContentReferenceKind.AnatomyNode => typeof(AnatomyProfileSO),
        ContentReferenceKind.Proficiency => typeof(ProficiencyDefinitionSO),
        ContentReferenceKind.Culture => typeof(SpeciesCultureDefinitionSO),
        ContentReferenceKind.Practice => typeof(CulturalPracticeDefinitionSO),
        ContentReferenceKind.Ambition => typeof(CharacterAmbitionDefinitionSO),
        ContentReferenceKind.LifeEvent => typeof(LifeEventDefinitionSO),
        ContentReferenceKind.Faction => typeof(DungeonFactionDefinitionSO),
        ContentReferenceKind.FactionChapter => typeof(FactionChapterDefinitionSO),
        ContentReferenceKind.FactionContract => typeof(FactionContractDefinitionSO),
        ContentReferenceKind.EnemyAbility => typeof(EnemyAbilityDefinitionSO),
        ContentReferenceKind.EnemyArchetype => typeof(EnemyArchetypeDefinitionSO),
        ContentReferenceKind.BattlefieldModifier => typeof(BattlefieldModifierDefinitionSO),
        ContentReferenceKind.Disease => typeof(DiseaseDefinitionSO),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static IReadOnlyList<Entry> Entries(ContentReferenceKind kind)
    {
        if (cache.TryGetValue(kind, out Entry[] found)) return found;
        var root = AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>("Assets/Resources/SO/GameContentCatalog.asset");
        if (root == null) return Array.Empty<Entry>();
        var items = root.GetItemDefinitions<ItemDefinitionCatalogSO>();
        IEnumerable<ScriptableObject> assets = root.DomainCatalogs.OfType<GameDomainContentCatalogSO>()
            .SelectMany(value => value.Definitions).Concat(items != null
                ? items.Definitions.Cast<ScriptableObject>() : Array.Empty<ScriptableObject>());
        var result = new List<Entry>();
        foreach (ScriptableObject asset in assets.Where(value => value != null).Distinct())
        {
            if (kind == ContentReferenceKind.Recipe)
            {
                if (asset is ProductionRecipeSO production) result.Add(new Entry(asset, production.RecipeId));
                else if (asset is FacilitySynthesisRecipeSO synthesis) result.Add(new Entry(asset, synthesis.recipeId));
                continue;
            }
            if (!AssetType(kind).IsInstanceOfType(asset)) continue;
            if (kind == ContentReferenceKind.AnatomyNode)
            {
                foreach (var node in ((AnatomyProfileSO)asset).Nodes) result.Add(new Entry(asset, node.NodeId));
                continue;
            }
            if (kind == ContentReferenceKind.Ammunition && asset is ItemDefinitionSO item
                && !item.TryGetFeature<AmmunitionItemFeature>(out _)) continue;
            if (kind == ContentReferenceKind.Building)
            {
                var building = (BuildingSO)asset;
                result.Add(new Entry(asset, string.IsNullOrWhiteSpace(building.ContentDefinitionId)
                    ? $"building:{building.id}" : building.ContentDefinitionId));
                continue;
            }
            if (kind == ContentReferenceKind.BuildingNumeric && ((BuildingSO)asset).id < 0) continue;
            string field = kind switch
            {
                ContentReferenceKind.Item or ContentReferenceKind.Ammunition => "itemId",
                ContentReferenceKind.Weapon or ContentReferenceKind.Armor or ContentReferenceKind.Shield => "equipmentId",
                ContentReferenceKind.BuildingNumeric or ContentReferenceKind.ResearchNumeric => "id",
                ContentReferenceKind.Research => "projectId",
                ContentReferenceKind.Species => "speciesTag",
                ContentReferenceKind.AnatomyProfile => "profileId",
                ContentReferenceKind.Faction => "factionId",
                _ => "stableId"
            };
            using var serialized = new SerializedObject(asset);
            var id = serialized.FindProperty(field);
            if (id == null) throw new InvalidOperationException($"{asset.GetType().Name} has no serialized {field}.");
            result.Add(new Entry(asset, id.propertyType == SerializedPropertyType.Integer
                ? id.intValue.ToString(CultureInfo.InvariantCulture) : id.stringValue));
        }
        return cache[kind] = result.Where(value => !string.IsNullOrWhiteSpace(value.Id))
            .OrderBy(value => value.Asset.name, StringComparer.Ordinal).ToArray();
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
    {
        error = string.Empty;
        if (asset == null) { WriteId(property, kind, string.Empty, emptyNumericId); return true; }
        Entry[] entries = Entries(kind).Where(value => value.Asset == asset).ToArray();
        if (entries.Length != 1 || Entries(kind).Count(value => value.Id == entries[0].Id) != 1)
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
