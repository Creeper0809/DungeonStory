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
        ReferenceHeight((ContentReferenceAttribute)attribute, property);

    public static float ReferenceHeight(ContentReferenceAttribute reference, SerializedProperty property) =>
        ContentReferenceSelection.ResolveReference(reference, property) == null
            ? EditorGUIUtility.singleLineHeight
            : EditorGUIUtility.singleLineHeight * 2 + 2;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        DrawReference(position, property, label, (ContentReferenceAttribute)attribute);
    }

    public static void DrawReference(Rect position, SerializedProperty property, GUIContent label,
        ContentReferenceAttribute reference)
    {
        using var protection = new EditorGUI.DisabledScope(
            ContentIdentityProtection.IsPublished(property, out _));
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
            var aliases = (resolved.Aliases ?? string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            string current = ContentReferenceSelection.ReadId(property, kind.Value, reference.EmptyNumericId);
            var matches = entries.Where(value => ContentReferenceCatalog.Matches(resolved, value.Id, current)).ToArray();
            bool contextual = requested == ContentReferenceKind.EffectTarget && kind == ContentReferenceKind.Faction;
            bool valid = ContentReferenceSelection.IsCurrentValid(reference, resolved, current, entries);
            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            if (kind == ContentReferenceKind.AnatomyNode || kind == ContentReferenceKind.ExpeditionChoice
                || kind is ContentReferenceKind.InvasionPattern or ContentReferenceKind.SkillCapability
                    or ContentReferenceKind.GameplayValueTarget or ContentReferenceKind.WorkType
                    or ContentReferenceKind.SkillModule or ContentReferenceKind.SkillVariant
                    or ContentReferenceKind.CampaignTarget or ContentReferenceKind.AcquiredTraitBinding
                    or ContentReferenceKind.EquipmentCapability or ContentReferenceKind.FacilityCapability
                || ContentReferenceCatalog.IsKeyReference(kind.Value)
                || contextual || aliases.Length > 0)
            {
                var options = ContentReferenceSelection.PopupOptions(reference, resolved, entries);
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
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            // An incomplete owning catalog is still editable. Surface the real
            // failure without clearing the stored ID or inventing candidates.
            EditorGUI.LabelField(position, label.text, "참조 목록 오류: " + exception.Message);
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
        public readonly string Label;
        public Entry(ScriptableObject asset, string id, string label = null) { Asset = asset; Id = id; Label = label ?? asset?.name ?? id; }
    }

    internal static List<(string Id, string Label)> PopupOptions(
        ContentReferenceAttribute requested, ContentReferenceAttribute resolved,
        IReadOnlyList<Entry> candidates)
    {
        var options = new List<(string Id, string Label)> { ("", "None") };
        if (requested.Kind == ContentReferenceKind.EffectTarget && resolved.Kind == ContentReferenceKind.Faction)
        {
            options.Add(("affected-faction", "발생한 사건의 세력 (affected-faction)"));
            options.Add(("requesting-faction", "요청한 세력 (requesting-faction)"));
        }
        options.AddRange((resolved.Aliases ?? string.Empty)
            .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => (value, "호환/문맥 키: " + value)));
        options.AddRange(candidates.Select(value => (value.Id, value.Label + " / " + value.Id)));
        return options;
    }

    internal static bool IsCurrentValid(ContentReferenceAttribute requested,
        ContentReferenceAttribute resolved, string current, IReadOnlyList<Entry> candidates)
    {
        int matches = candidates.Count(value => ContentReferenceCatalog.Matches(resolved, value.Id, current));
        bool contextId = requested.Kind == ContentReferenceKind.EffectTarget && resolved.Kind == ContentReferenceKind.Faction
            && (current == "affected-faction" || current == "requesting-faction");
        return current.Length == 0 || contextId
            || (resolved.Aliases ?? string.Empty).Split('|')
                .Any(value => ContentReferenceCatalog.Matches(resolved, value, current))
            || matches == 1
            || resolved.Kind is ContentReferenceKind.AnatomyNode or ContentReferenceKind.GameplayValueTarget && matches > 0;
    }

    public static ContentReferenceKind? ResolveKind(ContentReferenceKind kind, SerializedProperty property)
    {
        if (kind == ContentReferenceKind.WeightedTarget)
            return ContentReferenceCatalog.ResolveWeightedTargetKind(property.propertyPath);
        if (kind != ContentReferenceKind.EffectTarget) return kind;
        int dot = property.propertyPath.LastIndexOf('.');
        var effectKind = property.serializedObject.FindProperty(property.propertyPath.Substring(0, dot + 1) + "kind");
        if (effectKind == null || effectKind.hasMultipleDifferentValues) return null;
        return ContentReferenceCatalog.ResolveEffectTargetKind((V20ContentEffectKind)effectKind.intValue);
    }

    public static ContentReferenceAttribute ResolveReference(ContentReferenceAttribute reference, SerializedProperty property)
    {
        if (reference.Kind == ContentReferenceKind.DefenseFamily)
            return Sibling(property, "enabled")?.boolValue == false ? null : reference;
        if (reference.Kind == ContentReferenceKind.FacilityRecordMetric)
        {
            var trigger = property.serializedObject.FindProperty("generationTrigger");
            return trigger == null || trigger.hasMultipleDifferentValues
                || (FacilityEvolutionRecordTokenGenerationTrigger)trigger.intValue
                    != FacilityEvolutionRecordTokenGenerationTrigger.MetricThresholdCrossing
                ? null : reference;
        }
        if (reference.Kind == ContentReferenceKind.MetaEffectTarget)
        {
            var kind = ContextKind(property);
            return kind == null || kind.hasMultipleDifferentValues ? null
                : ContentReferenceCatalog.ResolveReference(reference, property.propertyPath,
                    new AuthoredGameplayEffectRecord { kind = (AuthoredGameplayEffectKind)kind.intValue });
        }
        if (reference.Kind == ContentReferenceKind.FixedTraitRule)
        {
            if (property.serializedObject.targetObject is not CharacterTraitSO) return null;
            if (property.serializedObject.isEditingMultipleObjects) return reference;
            string path = property.propertyPath;
            var rule = property.serializedObject.FindProperty(path.Substring(0, path.LastIndexOf('.')));
            int traitId = property.serializedObject.FindProperty("id").intValue;
            return rule?.managedReferenceValue != null && CharacterTraitIdentityRuleContract.TryGetExpectedKey(
                traitId, rule.managedReferenceValue.GetType(), property.name, out string key)
                ? new ContentReferenceAttribute(ContentReferenceKind.FixedTraitRule) { Aliases = key }
                : null;
        }
        if (reference.Kind == ContentReferenceKind.EncounterTarget)
        {
            var objective = property.serializedObject.FindProperty("objective");
            return objective == null || objective.hasMultipleDifferentValues
                || (OffenseEncounterObjective)objective.intValue != OffenseEncounterObjective.CaptureLeader
                ? null : new ContentReferenceAttribute(ContentReferenceKind.EnemyArchetype);
        }
        if (reference.Kind == ContentReferenceKind.AuthoredEffectText)
        {
            var kind = ContextKind(property);
            if (kind == null || kind.hasMultipleDifferentValues) return null;
            return ContentReferenceCatalog.ResolveReference(reference, property.propertyPath,
                new AuthoredGameplayEffectRecord { kind = (AuthoredGameplayEffectKind)kind.intValue });
        }
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
        // Read pending SerializedProperty values above. The CLR object graph can
        // still contain the old list/managed reference until ApplyModifiedProperties.
        // Non-contextual references never need that graph, including new list elements.
        return ContentReferenceCatalog.ResolveReference(reference, property.propertyPath, null);
    }

    private static SerializedProperty ContextKind(SerializedProperty property)
    {
        int dot = property.propertyPath.LastIndexOf('.');
        return property.serializedObject.FindProperty(property.propertyPath.Substring(0, dot + 1) + "kind");
    }

    public static bool HasMixedContext(ContentReferenceAttribute reference, SerializedProperty property) =>
        ContentReferenceCatalog.RequiresOwnerContext(reference.Kind) && property.serializedObject.isEditingMultipleObjects
            || property.serializedObject.isEditingMultipleObjects
                && (reference.Kind == ContentReferenceKind.AnatomyNode
                        && property.serializedObject.targetObject is AnatomyProfileSO
                    || reference.Kind == ContentReferenceKind.EnemyAbility
                        && property.serializedObject.targetObject is EnemyArchetypeDefinitionSO
                        && property.propertyPath.Contains("bossPhases."))
            || reference.Kind == ContentReferenceKind.FacilityRecordMetric
                && property.serializedObject.FindProperty("generationTrigger")?.hasMultipleDifferentValues == true
            || reference.Kind == ContentReferenceKind.DefenseFamily && Sibling(property, "enabled")?.hasMultipleDifferentValues == true
            || reference.Kind == ContentReferenceKind.SkillVariant && Sibling(property, "moduleId")?.hasMultipleDifferentValues == true
            || (reference.Kind == ContentReferenceKind.EncounterTarget
            ? property.serializedObject.FindProperty("objective")?.hasMultipleDifferentValues == true
            : reference.Kind is ContentReferenceKind.EffectTarget or ContentReferenceKind.NarrativeTarget or ContentReferenceKind.AuthoredEffectText or ContentReferenceKind.MetaEffectTarget
                && ContextKind(property)?.hasMultipleDifferentValues == true);

    private static SerializedProperty Sibling(SerializedProperty property, string name) =>
        property.serializedObject.FindProperty(property.propertyPath.Substring(0, property.propertyPath.LastIndexOf('.') + 1) + name);

    public static IReadOnlyList<Entry> Entries(ContentReferenceAttribute reference, SerializedProperty property)
    {
        if (HasMixedContext(reference, property)) return Array.Empty<Entry>();
        var all = Entries(reference);
        if (catalog == null) return all;
        if (property.serializedObject.targetObject is StockInfo && reference.Kind == ContentReferenceKind.BuildingNumeric)
            return catalog.Entries(reference, (ScriptableObject)property.serializedObject.targetObject)
                .Select(entry => new Entry(entry.Asset, entry.Id)).ToArray();
        int processAt = property.propertyPath.IndexOf(".supportedProcessIds", StringComparison.Ordinal);
        if (processAt >= 0 && reference.DefinitionTypeName == nameof(ServiceProcessSO))
        {
            if (property.serializedObject.isEditingMultipleObjects) return Array.Empty<Entry>();
            SerializedProperty hub = property.serializedObject.FindProperty(property.propertyPath.Substring(0, processAt));
            var pending = new BuildingServiceHubAbility
            {
                serviceHubTag = hub.FindPropertyRelative("serviceHubTag").stringValue,
                serviceCategory = (ServiceCategory)hub.FindPropertyRelative("serviceCategory").intValue
            };
            return catalog.Entries(reference, property.serializedObject.targetObject as ScriptableObject, pending)
                .Select(entry => new Entry(entry.Asset, entry.Id)).ToArray();
        }
        if (ContentReferenceCatalog.RequiresOwnerContext(reference.Kind))
        {
            if (HasMixedContext(reference, property)) return Array.Empty<Entry>();
            if (reference.Kind == ContentReferenceKind.SkillVariant)
                return catalog.Entries(reference, property.serializedObject.targetObject as ScriptableObject,
                    new CharacterSkillModuleSelection { moduleId = Sibling(property, "moduleId")?.stringValue })
                    .Select(entry => new Entry(entry.Asset, entry.Id)).ToArray();
            return PendingOwnerEntries(reference, property);
        }
        if (reference.Kind == ContentReferenceKind.AnatomyNode
            && property.serializedObject.targetObject is SurgicalProcedureSO)
        {
            var source = property.serializedObject;
            if (source.FindProperty("family").hasMultipleDifferentValues
                || source.FindProperty("allowsWildlife").hasMultipleDifferentValues
                || source.FindProperty("allowedAnatomyFamilies").hasMultipleDifferentValues
                || source.FindProperty("allowedSpeciesIds").hasMultipleDifferentValues)
                return Array.Empty<Entry>();
            return catalog.AnatomyNodesFor((MedicalProcedureFamily)source.FindProperty("family").intValue,
                source.FindProperty("allowsWildlife").boolValue,
                ReadStrings(source.FindProperty("allowedAnatomyFamilies")),
                ReadStrings(source.FindProperty("allowedSpeciesIds")))
                .Select(entry => new Entry(entry.Asset, entry.Id)).ToArray();
        }
        if (reference.Kind == ContentReferenceKind.AnatomyNode
            && property.serializedObject.targetObject is AgeConditionDefinitionSO
            && property.propertyPath.StartsWith("anatomyTargets.Array.data[", StringComparison.Ordinal))
        {
            int end = property.propertyPath.IndexOf("].", StringComparison.Ordinal);
            var profileId = property.serializedObject.FindProperty(property.propertyPath.Substring(0, end + 2) + "anatomyProfileId");
            if (profileId == null || profileId.hasMultipleDifferentValues) return Array.Empty<Entry>();
            return all.Where(entry => ((AnatomyProfileSO)entry.Asset).ProfileId == profileId.stringValue).ToArray();
        }
        if (reference.Kind == ContentReferenceKind.AnatomyNode
            && property.serializedObject.targetObject is AnatomyProfileSO)
            return PendingOwnerEntries(reference, property);
        if (reference.Kind == ContentReferenceKind.EnemyAbility && property.propertyPath.Contains("bossPhases.")
            && property.serializedObject.targetObject is EnemyArchetypeDefinitionSO)
            return PendingOwnerEntries(reference, property);
        if (reference.Kind == ContentReferenceKind.EnemyArchetype && property.name == "objectiveTargetId"
            && property.serializedObject.targetObject is OffenseEncounterSO)
        {
            SerializedProperty enemies = property.serializedObject.FindProperty("enemies");
            if (enemies == null || enemies.hasMultipleDifferentValues) return Array.Empty<Entry>();
            var selected = Enumerable.Range(0, enemies.arraySize)
                .Select(index => enemies.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("enemyArchetypeId")?.stringValue)
                .Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
            return all.Where(value => selected.Contains(value.Id)).ToArray();
        }
        return all;
    }

    // Copy pending serialized values to a disposable object, never Apply to the
    // user's asset just to populate a picker. New list rows and Undo therefore
    // see the same local catalog that validation will see after Apply.
    private static IReadOnlyList<Entry> PendingOwnerEntries(ContentReferenceAttribute reference, SerializedProperty property)
    {
        SerializedObject source = property.serializedObject;
        if (source.targetObject is not ScriptableObject original) return Array.Empty<Entry>();
        ScriptableObject pending = ScriptableObject.CreateInstance(original.GetType());
        pending.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            using var copy = new SerializedObject(pending);
            SerializedProperty field = source.GetIterator();
            bool enter = true;
            while (field.Next(enter))
            {
                enter = false;
                if (field.name != "m_Script")
                    CopyPendingValue(field, copy.FindProperty(field.propertyPath));
            }
            copy.ApplyModifiedPropertiesWithoutUndo();
            object owner = ReadOwner(pending, property.propertyPath);
            IEnumerable<ContentReferenceCatalog.Entry> candidates;
            if (reference.Kind == ContentReferenceKind.AnatomyNode && pending is AnatomyProfileSO profile)
            {
                string selfParent = property.name == "parentNodeId" && owner is AnatomyNodeDefinition node
                    ? node.NodeId : null;
                candidates = profile.Nodes.Where(value => value != null
                        && !string.IsNullOrWhiteSpace(value.NodeId) && value.NodeId != selfParent)
                    .Select(value => new ContentReferenceCatalog.Entry(profile, value.NodeId));
            }
            else if (reference.Kind == ContentReferenceKind.EnemyAbility && pending is EnemyArchetypeDefinitionSO enemy
                && property.propertyPath.Contains("bossPhases."))
                candidates = catalog.Entries(reference).Where(value => enemy.abilityIds?.Contains(value.Id) == true);
            else
                candidates = catalog.Entries(reference, pending, owner);
            return candidates.Select(entry =>
                new Entry(entry.Asset == pending ? original : entry.Asset, entry.Id, entry.Label)).ToArray();
        }
        finally { UnityEngine.Object.DestroyImmediate(pending); }
    }

    private static object ReadOwner(object root, string propertyPath)
    {
        string[] parts = propertyPath.Replace(".Array.data[", "[").Split('.');
        object value = root;
        foreach (string part in parts.Take(parts.Length - 1))
        {
            int bracket = part.IndexOf('[');
            string name = bracket < 0 ? part : part.Substring(0, bracket);
            var field = ContentReferenceCatalog.SerializedFields(value.GetType()).FirstOrDefault(item => item.Name == name);
            value = field?.GetValue(value);
            if (value == null) return null;
            if (bracket >= 0)
                value = ((System.Collections.IList)value)[int.Parse(part.Substring(bracket + 1, part.Length - bracket - 2), CultureInfo.InvariantCulture)];
        }
        return value;
    }

    private static void CopyPendingValue(SerializedProperty source, SerializedProperty target)
    {
        if (target == null)
            throw new InvalidOperationException("Pending reference property is missing: " + source.propertyPath);
        if (source.isArray && source.propertyType != SerializedPropertyType.String)
        {
            target.arraySize = source.arraySize;
            for (int i = 0; i < source.arraySize; i++)
                CopyPendingValue(source.GetArrayElementAtIndex(i), target.GetArrayElementAtIndex(i));
        }
        else if (source.propertyType is SerializedPropertyType.Generic or SerializedPropertyType.ManagedReference)
        {
            if (source.propertyType == SerializedPropertyType.ManagedReference)
            {
                object managed = source.managedReferenceValue;
                target.managedReferenceValue = managed == null ? null
                    : JsonUtility.FromJson(JsonUtility.ToJson(managed), managed.GetType());
                if (managed == null) return;
            }
            var child = source.Copy();
            var end = source.GetEndProperty();
            if (child.Next(true))
                do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    CopyPendingValue(child, target.FindPropertyRelative(child.name));
                } while (child.Next(false));
        }
        else target.boxedValue = source.boxedValue;
    }

    private static string[] ReadStrings(SerializedProperty list) =>
        Enumerable.Range(0, list.arraySize).Select(index => list.GetArrayElementAtIndex(index).stringValue).ToArray();

    public static Type AssetType(ContentReferenceKind kind) => ContentReferenceCatalog.AssetType(kind);
    public static Type AssetType(ContentReferenceAttribute reference) => reference.Kind == ContentReferenceKind.Definition
        ? ContentReferenceCatalog.ResolveType(reference.DefinitionTypeName) : AssetType(reference.Kind);

    public static IReadOnlyList<Entry> Entries(ContentReferenceKind kind) => Entries(new ContentReferenceAttribute(kind));

    public static ContentReferenceCatalog CreateCatalog(GameContentCatalogSO root)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        return ContentReferenceCatalog.FromCatalogs(
            root.DomainCatalogs.OfType<GameDomainContentCatalogSO>(),
            root.GetItemDefinitions<ItemDefinitionCatalogSO>(),
            root.GetCharacterSkillSettings<CharacterSkillSystemSettingsSO>());
    }

    public static IReadOnlyList<Entry> Entries(ContentReferenceAttribute reference)
    {
        string key = $"{reference.Kind}:{reference.DefinitionTypeName}:{reference.IdMemberName}";
        if (reference.Kind != ContentReferenceKind.FacilityAnchorPurpose
            && entries.TryGetValue(key, out var found)) return found;
        if (catalog == null)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>("Assets/Resources/SO/GameContentCatalog.asset");
            if (root == null) return Array.Empty<Entry>();
            catalog = CreateCatalog(root);
        }
        return entries[key] = catalog.Entries(reference).Select(value => new Entry(value.Asset, value.Id, value.Label)).ToArray();
    }

    public static string ReadId(SerializedProperty property, ContentReferenceKind kind, int emptyNumericId = -1)
    {
        if (kind is ContentReferenceKind.FactionEconomicPolicy or ContentReferenceKind.ProductionMassPolicy or ContentReferenceKind.ProductionOutputCostPolicy)
        {
            int version = Sibling(property, kind == ContentReferenceKind.FactionEconomicPolicy ? "capabilityVersion" : "contractVersion").intValue;
            return string.IsNullOrEmpty(property.stringValue) && version == 0
                ? string.Empty : (property.stringValue ?? string.Empty) + "@" + version;
        }
        if (property.propertyType == SerializedPropertyType.Integer)
            return property.intValue == (kind == ContentReferenceKind.ResearchNumeric ? 0 : emptyNumericId)
                ? string.Empty : property.intValue.ToString(CultureInfo.InvariantCulture);
        return property.stringValue ?? string.Empty;
    }

    public static void WriteId(SerializedProperty property, ContentReferenceKind kind, string id, int emptyNumericId = -1)
    {
        if (kind is ContentReferenceKind.FactionEconomicPolicy or ContentReferenceKind.ProductionMassPolicy or ContentReferenceKind.ProductionOutputCostPolicy)
        {
            int split = id.LastIndexOf('@');
            property.stringValue = split < 0 ? string.Empty : id.Substring(0, split);
            Sibling(property, kind == ContentReferenceKind.FactionEconomicPolicy ? "capabilityVersion" : "contractVersion").intValue = split < 0 ? 0 : int.Parse(id.Substring(split + 1), CultureInfo.InvariantCulture);
            if (split < 0 && kind != ContentReferenceKind.FactionEconomicPolicy)
                Sibling(property, "canonicalPayload").stringValue = string.Empty;
            return;
        }
        if (property.propertyType == SerializedPropertyType.Integer)
            property.intValue = id.Length == 0 ? (kind == ContentReferenceKind.ResearchNumeric ? 0 : emptyNumericId)
                : int.Parse(id, CultureInfo.InvariantCulture);
        else property.stringValue = id;
        if (kind == ContentReferenceKind.SkillBehavior)
        {
            var formula = Sibling(property, "formula");
            if (formula != null)
            {
                formula.FindPropertyRelative("formatterId").stringValue = id;
                formula.FindPropertyRelative("applicatorId").stringValue = id;
            }
        }
        if (kind == ContentReferenceKind.EquipmentApplicator
            && EquipmentEvolutionFormulaCapabilityDefinition.TryResolveApplicator(id, out string stat, out var modifier))
        {
            Sibling(property, "statId").stringValue = stat;
            Sibling(property, "modifierKind").intValue = (int)modifier;
        }
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
        if (HasMixedContext(reference, property))
        {
            error = "Select one owning definition before assigning its local reference. Existing ID preserved.";
            return false;
        }
        if (ContentIdentityProtection.IsPublished(property, out error)) return false;
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

[CustomPropertyDrawer(typeof(HeritableTraitConsequence))]
public sealed class HeritableTraitConsequenceDrawer : PropertyDrawer
{
    private const float Spacing = 2f;

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label) =>
        EditorGUIUtility.singleLineHeight * 3f + Spacing * 2f;

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        SerializedProperty kind = property.FindPropertyRelative("kind");
        SerializedProperty target = property.FindPropertyRelative("targetId");
        SerializedProperty multiplier = property.FindPropertyRelative("multiplierDelta");
        if (kind == null || target == null || multiplier == null)
        {
            EditorGUI.LabelField(position, label, new GUIContent("상속 특성 결과 필드가 없습니다."));
            return;
        }

        EditorGUI.BeginProperty(position, label, property);
        try
        {
            Rect line = new(position.x, position.y, position.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, kind,
                new GUIContent(label.text + " / 종류"));

            line.y += EditorGUIUtility.singleLineHeight + Spacing;
            DrawTarget(line, property, kind, target);

            line.y += EditorGUIUtility.singleLineHeight + Spacing;
            EditorGUI.PropertyField(line, multiplier, new GUIContent("배율 변화"));
        }
        finally
        {
            EditorGUI.EndProperty();
        }
    }

    internal static bool TryAssignTarget(
        SerializedProperty property,
        string nextTargetId,
        out string error)
    {
        error = string.Empty;
        SerializedProperty kind = property?.FindPropertyRelative("kind");
        SerializedProperty target = property?.FindPropertyRelative("targetId");
        if (kind == null || target == null)
        {
            error = "상속 특성 결과의 종류 또는 대상 필드가 없습니다.";
            return false;
        }
        if (kind.hasMultipleDifferentValues)
        {
            error = "서로 다른 결과 종류에는 하나의 대상을 함께 지정할 수 없습니다.";
            return false;
        }

        HeritableTraitConsequenceKind pendingKind =
            (HeritableTraitConsequenceKind)kind.intValue;
        if (!HeritableTraitConsequence.TargetsFor(pendingKind)
                .Contains(nextTargetId, StringComparer.Ordinal))
        {
            error = $"'{nextTargetId ?? string.Empty}'은(는) {pendingKind}의 등록 대상이 아닙니다.";
            return false;
        }

        target.stringValue = nextTargetId;
        return true;
    }

    private static void DrawTarget(
        Rect position,
        SerializedProperty property,
        SerializedProperty kind,
        SerializedProperty target)
    {
        if (kind.hasMultipleDifferentValues)
        {
            EditorGUI.LabelField(position, "대상",
                "서로 다른 종류 — 종류를 맞춘 뒤 선택");
            return;
        }

        HeritableTraitConsequenceKind pendingKind =
            (HeritableTraitConsequenceKind)kind.intValue;
        string[] targets = HeritableTraitConsequence.TargetsFor(pendingKind)
            .ToArray();
        string current = target.stringValue ?? string.Empty;
        bool mixedTargets = target.hasMultipleDifferentValues;
        int currentIndex = Array.FindIndex(targets,
            value => string.Equals(value, current, StringComparison.Ordinal));
        bool preserveExisting = mixedTargets || currentIndex < 0;

        var labels = new List<string>();
        var values = new List<string>();
        if (preserveExisting)
        {
            labels.Add(mixedTargets
                ? "서로 다른 값 — 선택 시 모두 변경"
                : current.Length == 0
                    ? "미지정 — 대상 선택 필요"
                    : "알 수 없는 기존 값: " + current + " — 기존 값 보존");
            values.Add(null);
        }
        labels.AddRange(targets);
        values.AddRange(targets);

        if (labels.Count == 0)
        {
            EditorGUI.LabelField(position, "대상",
                "선택 가능한 대상 없음 — 기존 값 보존");
            return;
        }

        int selected = preserveExisting ? 0 : currentIndex;
        bool previousMixed = EditorGUI.showMixedValue;
        EditorGUI.showMixedValue = mixedTargets;
        try
        {
            using (new EditorGUI.DisabledScope(targets.Length == 0))
            {
                EditorGUI.BeginChangeCheck();
                int next = EditorGUI.Popup(position, "대상", selected,
                    labels.ToArray());
                if (EditorGUI.EndChangeCheck()
                    && next >= 0
                    && next < values.Count
                    && values[next] != null
                    && !TryAssignTarget(property, values[next], out string error))
                {
                    Debug.LogWarning(error,
                        property.serializedObject.targetObject);
                }
            }
        }
        finally
        {
            EditorGUI.showMixedValue = previousMixed;
        }
    }
}
