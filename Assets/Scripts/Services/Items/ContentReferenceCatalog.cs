using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Inspector selection and startup validation use exactly the same registered definitions.
// Serialized IDs remain the runtime/save contract; this catalog never rewrites them.
public sealed class ContentReferenceCatalog
{
    public readonly struct Entry
    {
        public readonly ScriptableObject Asset;
        public readonly string Id;
        public Entry(ScriptableObject asset, string id) { Asset = asset; Id = id; }
    }
    private readonly ScriptableObject[] assets;
    private readonly Dictionary<string, Entry[]> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, int>> matchCounts = new(StringComparer.Ordinal);
    private static readonly Dictionary<Type, FieldInfo[]> fields = new();
    private static readonly Dictionary<FieldInfo, ContentReferenceAttribute> references = new();

    public ContentReferenceCatalog(IEnumerable<ScriptableObject> definitions) =>
        assets = (definitions ?? throw new ArgumentNullException(nameof(definitions)))
            .Where(value => value != null).Distinct().ToArray();

    public static ContentReferenceCatalog FromRoot(GameContentCatalogSO root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        var items = root.GetItemDefinitions<ItemDefinitionCatalogSO>();
        return new ContentReferenceCatalog(root.DomainCatalogs.OfType<GameDomainContentCatalogSO>()
            .SelectMany(value => value.Definitions).Concat(items != null
                ? items.Definitions.Cast<ScriptableObject>() : Array.Empty<ScriptableObject>()));
    }

    public static Type AssetType(ContentReferenceKind kind) => kind switch
    {
        ContentReferenceKind.Item or ContentReferenceKind.Ammunition => typeof(ItemDefinitionSO),
        ContentReferenceKind.Weapon => typeof(CombatWeaponSO),
        ContentReferenceKind.Armor => typeof(CombatArmorSO),
        ContentReferenceKind.Shield => typeof(CombatShieldSO),
        ContentReferenceKind.Building or ContentReferenceKind.BuildingNumeric => typeof(BuildingSO),
        ContentReferenceKind.Research or ContentReferenceKind.ResearchNumeric => typeof(ResearchProjectSO),
        ContentReferenceKind.Recipe or ContentReferenceKind.SpeciesOrAnatomy or ContentReferenceKind.AnatomySpecies or ContentReferenceKind.ExpeditionSite => typeof(ScriptableObject),
        ContentReferenceKind.ExpeditionChoice => typeof(OffenseDecisionCardSO),
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


    public static Type ResolveType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name ?? string.Empty, false)).FirstOrDefault(type => type != null)
        ?? throw new InvalidOperationException($"Unknown content definition type '{name}'.");

    public static string ReadMember(object value, string name)
    {
        for (Type type = value.GetType(); type != null; type = type.BaseType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var field = type.GetField(name, flags);
            var property = type.GetProperty(name, flags);
            if (field == null && property == null) continue;
            object id = field != null ? field.GetValue(value) : property.GetValue(value);
            return Convert.ToString(id, CultureInfo.InvariantCulture) ?? string.Empty;
        }
        throw new InvalidOperationException($"{value.GetType().Name} has no identity member '{name}'.");
    }

    public IReadOnlyList<Entry> Entries(ContentReferenceAttribute reference)
    {
        string key = $"{reference.Kind}:{reference.DefinitionTypeName}:{reference.IdMemberName}";
        if (!entries.TryGetValue(key, out var found)) entries[key] = found = BuildEntries(reference);
        return found;
    }

    private Entry[] BuildEntries(ContentReferenceAttribute reference)
    {
        ContentReferenceKind kind = reference.Kind;
        Type targetType = kind == ContentReferenceKind.Definition
            ? ResolveType(reference.DefinitionTypeName) : AssetType(kind);
        var result = new List<Entry>();
        foreach (ScriptableObject asset in assets.Where(value => value != null).Distinct())
        {
            if (kind == ContentReferenceKind.ExpeditionSite)
            {
                if (asset is OffenseSiteArchetypeSO site) result.Add(new Entry(asset, site.siteTypeId));
                else if (asset is OffenseUrgentSiteDefinitionSO urgent) result.Add(new Entry(asset, urgent.urgentSiteId));
                continue;
            }
            if (kind == ContentReferenceKind.AnatomySpecies)
            {
                if (asset is CharacterSpeciesDefinitionSO species) result.Add(new Entry(asset, species.speciesTag));
                else if (asset is WildlifeSpeciesSO wildlife) result.Add(new Entry(asset, wildlife.SpeciesId));
                continue;
            }
            if (kind == ContentReferenceKind.SpeciesOrAnatomy)
            {
                if (asset is CharacterSpeciesDefinitionSO species) result.Add(new Entry(asset, ReadMember(species, "speciesTag")));
                else if (asset is AnatomyProfileSO anatomy) result.Add(new Entry(asset, anatomy.ProfileId));
                continue;
            }
            if (kind == ContentReferenceKind.ExpeditionChoice)
            {
                if (asset is OffenseDecisionCardSO card)
                    foreach (var choice in card.choices.Where(value => value != null))
                        result.Add(new Entry(asset, InGameNarrativeTextCatalogSO.ComposeExpeditionChoiceStableId(card.cardId, choice.choiceId)));
                continue;
            }
            if (kind == ContentReferenceKind.Recipe)
            {
                if (asset is ProductionRecipeSO production) result.Add(new Entry(asset, production.RecipeId));
                else if (asset is FacilitySynthesisRecipeSO synthesis) result.Add(new Entry(asset, synthesis.recipeId));
                continue;
            }
            if (!targetType.IsInstanceOfType(asset)) continue;
            if (kind == ContentReferenceKind.Definition)
            {
                result.Add(new Entry(asset, ReadMember(asset, reference.IdMemberName)));
                continue;
            }
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
            result.Add(new Entry(asset, ReadMember(asset, field)));
        }
        return result.Where(value => !string.IsNullOrWhiteSpace(value.Id))
            .OrderBy(value => value.Asset.name, StringComparer.Ordinal).ToArray();

    }

    public static IReadOnlyList<FieldInfo> SerializedFields(Type type)
    {
        if (fields.TryGetValue(type, out var found)) return found;
        var result = new List<FieldInfo>();
        for (Type current = type; current != null && current != typeof(ScriptableObject)
            && current != typeof(UnityEngine.Object); current = current.BaseType)
            result.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(field => !field.IsStatic && !field.IsInitOnly && !field.IsNotSerialized
                    && (field.IsPublic || field.IsDefined(typeof(SerializeField)) || field.IsDefined(typeof(SerializeReference)))));
        return fields[type] = result.ToArray();
    }

    public static ContentReferenceKind? ResolveKind(ContentReferenceKind kind, string path, object owner)
    {
        if (kind == ContentReferenceKind.WeightedTarget)
            return path.Contains("relatedEventWeights.") ? ContentReferenceKind.LifeEvent
                : path.Contains("otherCultureAttitudes.") ? ContentReferenceKind.Culture : null;
        if (kind != ContentReferenceKind.EffectTarget) return kind;
        if (owner is not V20ContentEffect effect) return null;
        return effect.kind switch
        {
            V20ContentEffectKind.ItemGrant or V20ContentEffectKind.ItemConsume => ContentReferenceKind.Item,
            V20ContentEffectKind.DiseaseExposure => ContentReferenceKind.Disease,
            V20ContentEffectKind.FactionRapport or V20ContentEffectKind.FactionGrievance
                or V20ContentEffectKind.FactionObligation => ContentReferenceKind.Faction,
            _ => null
        };
    }

    public static ContentReferenceAttribute ResolveReference(ContentReferenceAttribute reference, string path, object owner)
    {
        if (reference.Kind == ContentReferenceKind.NarrativeTarget && owner is InGameNarrativeTextEntry text)
            return text.Kind switch
            {
                InGameNarrativeTextKind.Item => new ContentReferenceAttribute(ContentReferenceKind.Item),
                InGameNarrativeTextKind.ProductionRecipe => new ContentReferenceAttribute("ProductionRecipeSO", "RecipeId"),
                InGameNarrativeTextKind.Facility => new ContentReferenceAttribute(ContentReferenceKind.Building),
                InGameNarrativeTextKind.ExpeditionCard => new ContentReferenceAttribute("OffenseDecisionCardSO", "cardId"),
                InGameNarrativeTextKind.ExpeditionSite => new ContentReferenceAttribute(ContentReferenceKind.ExpeditionSite),
                InGameNarrativeTextKind.ExpeditionChoice => new ContentReferenceAttribute(ContentReferenceKind.ExpeditionChoice),
                _ => throw new InvalidOperationException($"Unknown narrative kind at {path}.")
            };
        var kind = ResolveKind(reference.Kind, path, owner);
        return kind == null ? null : kind == reference.Kind ? reference
            : new ContentReferenceAttribute(kind.Value, reference.EmptyNumericId) { Aliases = reference.Aliases, IgnoreCase = reference.IgnoreCase };
    }

    public IReadOnlyList<string> Validate(IEnumerable<ScriptableObject> roots)
    {
        var errors = new List<string>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var root in roots.Where(value => value != null)) Visit(root, root.name, root, visited, errors);
        return errors;
    }

    private void Visit(object value, string path, ScriptableObject root, HashSet<object> visited, List<string> errors)
    {
        if (value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum) return;
        if (!visited.Add(value)) return;
        if (value is UnityEngine.Object unity && unity == null) return;
        if (value is UnityEngine.Object && value is not ScriptableObject) return;
        if (value is IEnumerable sequence)
        {
            Type element = value.GetType().IsArray ? value.GetType().GetElementType()
                : value.GetType().IsGenericType ? value.GetType().GetGenericArguments().FirstOrDefault() : null;
            if (element != null && (element.IsPrimitive || element.IsEnum || element == typeof(string))) return;
            int index = 0;
            foreach (object item in sequence) Visit(item, $"{path}.[{index++}]", root, visited, errors);
            return;
        }
        if (value is EnemyArchetypeDefinitionSO enemy)
            errors.AddRange(enemy.ValidateDefinition().Select(error => path + ": " + error));
        if (value is AnatomyProfileSO anatomy)
        {
            var nodeIds = anatomy.Nodes.Where(node => node != null).Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
            foreach (var node in anatomy.Nodes.Where(node => node != null && node.ParentNodeId.Length > 0))
                if (!nodeIds.Contains(node.ParentNodeId) || node.ParentNodeId == node.NodeId)
                    errors.Add($"{path}: invalid local anatomy parent '{node.ParentNodeId}' for '{node.NodeId}'.");
            foreach (var attachment in anatomy.ApparelAttachments)
                if (attachment == null || !nodeIds.Contains(attachment.nodeId ?? string.Empty))
                    errors.Add($"{path}: unknown local apparel anatomy node '{attachment?.nodeId}'.");
        }
        foreach (var field in SerializedFields(value.GetType()))
        {
            object member = field.GetValue(value);
            string memberPath = path + "." + field.Name;
            if (!references.TryGetValue(field, out var reference))
                references[field] = reference = field.GetCustomAttribute<ContentReferenceAttribute>();
            // Owner-selectable species intentionally have no authored home faction.
            bool inactiveHome = value is CharacterSpeciesDefinitionSO species && species.ownerSelectable
                && field.Name == "homeFactionId";
            if (reference != null && !inactiveHome)
            {
                var resolved = ResolveReference(reference, memberPath, value);
                if (resolved != null)
                {
                    if (member is IEnumerable collection && member is not string)
                    {
                        int index = 0;
                        foreach (object item in collection) ValidateId(item, resolved, reference.Kind, $"{memberPath}[{index++}]", errors);
                    }
                    else ValidateId(member, resolved, reference.Kind, memberPath, errors);
                }
            }
            Visit(member, memberPath, member as ScriptableObject ?? root, visited, errors);
        }
    }

    public static bool Matches(ContentReferenceAttribute reference, string left, string right) =>
        string.Equals(left, right, reference.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private void ValidateId(object value, ContentReferenceAttribute reference, ContentReferenceKind requested,
        string path, List<string> errors)
    {
        string id = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (id.Length == 0 || value is int number && number ==
            (reference.Kind == ContentReferenceKind.ResearchNumeric ? 0 : reference.EmptyNumericId)) return;
        if ((reference.Aliases ?? string.Empty).Split('|').Any(alias => Matches(reference, alias, id))) return;
        if (requested == ContentReferenceKind.EffectTarget && reference.Kind == ContentReferenceKind.Faction
            && (id == "affected-faction" || id == "requesting-faction")) return;
        string key = $"{reference.Kind}:{reference.DefinitionTypeName}:{reference.IdMemberName}:{reference.IgnoreCase}";
        if (!matchCounts.TryGetValue(key, out var counts))
        {
            counts = new Dictionary<string, int>(reference.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var entry in Entries(reference))
                counts[entry.Id] = counts.TryGetValue(entry.Id, out int prior) ? prior + 1 : 1;
            matchCounts[key] = counts;
        }
        counts.TryGetValue(id, out int count);
        if (count != 1 && !(reference.Kind == ContentReferenceKind.AnatomyNode && count > 0))
            errors.Add($"{path}: unknown or ambiguous {reference.Kind} reference '{id}'.");
    }

    // Unity's target profile does not provide the newer BCL reference comparer.
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}
