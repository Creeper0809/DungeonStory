using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DungeonStory.Factions;
using DungeonStory.Balance;
using UnityEngine;

// Inspector selection and startup validation use exactly the same registered definitions.
// Serialized IDs remain the runtime/save contract; this catalog never rewrites them.
public sealed class ContentReferenceCatalog
{
    public readonly struct Entry
    {
        public readonly ScriptableObject Asset;
        public readonly string Id;
        public readonly string Label;
        public Entry(ScriptableObject asset, string id, string label = null) { Asset = asset; Id = id; Label = label ?? asset?.name ?? id; }
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
            .SelectMany(value => value.Definitions.Concat(new ScriptableObject[] { value })).Concat(items != null
                ? items.Definitions.Cast<ScriptableObject>() : Array.Empty<ScriptableObject>())
            .Concat(new ScriptableObject[] { root.GetCharacterSkillSettings<CharacterSkillSystemSettingsSO>() }));
    }

    public static bool IsKeyReference(ContentReferenceKind kind) => kind is
        ContentReferenceKind.WorkType or ContentReferenceKind.GameplayEffectTarget
        or ContentReferenceKind.AnatomyFamily or ContentReferenceKind.FixedTraitRule
        or ContentReferenceKind.FactionEconomicPolicy or ContentReferenceKind.StockCategory
        or ContentReferenceKind.FacilityEvolutionModule or ContentReferenceKind.EquipmentEvolutionModule
        or ContentReferenceKind.ProductionWorkstation or ContentReferenceKind.ProductionSupport
        or ContentReferenceKind.ProductionBatchSupport or ContentReferenceKind.ServiceHub
        or ContentReferenceKind.ServiceFeature or ContentReferenceKind.ServiceProcessFeature
        or ContentReferenceKind.WildlifeHabitat or ContentReferenceKind.FacilityRole
        or ContentReferenceKind.FacilityAnchorPurpose or ContentReferenceKind.CoreIncident
        or ContentReferenceKind.CoreServiceCategory or ContentReferenceKind.CoreServiceMode
        or ContentReferenceKind.WorldFlag or ContentReferenceKind.FacilityOperationCapability
        or ContentReferenceKind.BattleCounter or ContentReferenceKind.MetaEffectTarget
        or ContentReferenceKind.SkillBehavior or ContentReferenceKind.FormulaParameter
        or ContentReferenceKind.FormulaMirror or ContentReferenceKind.EquipmentApplicator
        or ContentReferenceKind.EquipmentStat or ContentReferenceKind.ProductionMassPolicy
        or ContentReferenceKind.ProductionOutputCostPolicy or ContentReferenceKind.RoomProfileKey
        or ContentReferenceKind.RoomScore or ContentReferenceKind.RoomMetric or ContentReferenceKind.DecisionContextTag
        or ContentReferenceKind.BuildingSemanticTag or ContentReferenceKind.ProductionFacilityTag
        or ContentReferenceKind.DiseaseResponse or ContentReferenceKind.CharacterNeed
        or ContentReferenceKind.IdentityRuleProtocol or ContentReferenceKind.FacilityLineageTag
        or ContentReferenceKind.RoomTag or ContentReferenceKind.FacilityMutationTag
        or ContentReferenceKind.FacilityPressure or ContentReferenceKind.FacilityValueKey
        or ContentReferenceKind.DefenseFamily or ContentReferenceKind.FacilityRecordMetric
        or ContentReferenceKind.SpeciesIncident or ContentReferenceKind.EnemyFaction
        or ContentReferenceKind.CampaignRegion or ContentReferenceKind.CampaignFaction
        or ContentReferenceKind.EnemyAbilityTarget or ContentReferenceKind.EnemyTacticalTarget
        or ContentReferenceKind.EnemyTacticalOverride;

    public static Type AssetType(ContentReferenceKind kind) => kind switch
    {
        ContentReferenceKind.Item or ContentReferenceKind.Ammunition => typeof(ItemDefinitionSO),
        ContentReferenceKind.Weapon => typeof(CombatWeaponSO),
        ContentReferenceKind.Equipment => typeof(CombatEquipmentDefinitionSO),
        ContentReferenceKind.Armor => typeof(CombatArmorSO),
        ContentReferenceKind.Shield => typeof(CombatShieldSO),
        ContentReferenceKind.Building or ContentReferenceKind.BuildingNumeric => typeof(BuildingSO),
        ContentReferenceKind.Research or ContentReferenceKind.ResearchNumeric => typeof(ResearchProjectSO),
        ContentReferenceKind.Recipe or ContentReferenceKind.SpeciesOrAnatomy or ContentReferenceKind.AnatomySpecies or ContentReferenceKind.ExpeditionSite => typeof(ScriptableObject),
        ContentReferenceKind.ExpeditionChoice => typeof(OffenseDecisionCardSO),
        ContentReferenceKind.Species => typeof(CharacterSpeciesDefinitionSO),
        ContentReferenceKind.AnatomyProfile or ContentReferenceKind.AnatomyNode => typeof(AnatomyProfileSO),
        ContentReferenceKind.Proficiency or ContentReferenceKind.StartingProficiency => typeof(ProficiencyDefinitionSO),
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
        ContentReferenceKind.InvasionPattern => typeof(GameDomainContentCatalogSO),
        ContentReferenceKind.WorkerTrait or ContentReferenceKind.WorkType => typeof(ScriptableObject),
        ContentReferenceKind.SkillCapability or ContentReferenceKind.SkillModule or ContentReferenceKind.SkillVariant => typeof(CharacterSkillSystemSettingsSO),
        ContentReferenceKind.CampaignTarget => typeof(OffenseCampaignCatalogSO),
        ContentReferenceKind.AcquiredTraitBinding => typeof(CharacterAcquiredTraitModuleSO),
        ContentReferenceKind.EquipmentCapability => typeof(EquipmentEvolutionFormulaCatalogSO),
        ContentReferenceKind.FacilityCapability => typeof(FacilityEvolutionRecipeSO),
        ContentReferenceKind.GameplayValueTarget => typeof(GameplayEffectDefinitionSO),
        _ when IsKeyReference(kind) => typeof(ScriptableObject),
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
        if (reference.Kind == ContentReferenceKind.FacilityAnchorPurpose) return BuildEntries(reference);
        string key = $"{reference.Kind}:{reference.DefinitionTypeName}:{reference.IdMemberName}";
        if (!entries.TryGetValue(key, out var found)) entries[key] = found = BuildEntries(reference);
        return found;
    }

    public static bool RequiresOwnerContext(ContentReferenceKind kind) => kind is
        ContentReferenceKind.CampaignTarget or ContentReferenceKind.AcquiredTraitBinding
        or ContentReferenceKind.SkillCapability or ContentReferenceKind.SkillVariant
        or ContentReferenceKind.EquipmentCapability or ContentReferenceKind.FacilityCapability
        or ContentReferenceKind.FacilityEvolutionModule or ContentReferenceKind.FixedTraitRule
        or ContentReferenceKind.ProductionSupport or ContentReferenceKind.ProductionBatchSupport
        or ContentReferenceKind.ServiceFeature or ContentReferenceKind.ServiceProcessFeature
        or ContentReferenceKind.SkillBehavior or ContentReferenceKind.FormulaMirror
        or ContentReferenceKind.MetaEffectTarget or ContentReferenceKind.EquipmentStat
        or ContentReferenceKind.RoomProfileKey or ContentReferenceKind.DiseaseResponse
        or ContentReferenceKind.IdentityRuleProtocol or ContentReferenceKind.FacilityValueKey
        or ContentReferenceKind.CampaignRegion or ContentReferenceKind.CampaignFaction;

    // Local relationships are scoped to their owning asset. They must never use
    // the global cache: two catalogs may intentionally publish the same local ID.
    public IReadOnlyList<Entry> Entries(ContentReferenceAttribute reference, ScriptableObject root, object owner = null)
    {
        if (reference.Kind is ContentReferenceKind.CampaignRegion or ContentReferenceKind.CampaignFaction)
        {
            if (root is not OffenseCampaignCatalogSO campaign) return Array.Empty<Entry>();
            return (campaign.Regions ?? Array.Empty<OffenseCampaignRegionDefinition>()).Where(value => value != null
                    && (reference.Kind == ContentReferenceKind.CampaignRegion
                        || owner is OffenseTargetDefinition target && target.regionId == value.id))
                .Select(value => new Entry(campaign,
                    reference.Kind == ContentReferenceKind.CampaignRegion ? value.id : value.factionId,
                    value.displayName)).ToArray();
        }
        if (reference.Kind == ContentReferenceKind.DiseaseResponse)
            return root is DiseaseDefinitionSO disease
                ? DiseaseFieldResponseRuntime.AvailableResponseIds(disease.stableId, disease.vaccineAllowed)
                    .Select(id => new Entry(null, id)).ToArray() : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.IdentityRuleProtocol)
            return CharacterIdentityRuleProtocols.Keys(owner?.GetType(), reference.ContextMember)
                .Select(id => new Entry(null, id)).ToArray();
        if (reference.Kind == ContentReferenceKind.SkillBehavior)
            return CharacterSkillModuleCapabilityRegistry.CapabilityIds.Select(CharacterSkillModuleCapabilityRegistry.Require)
                .Where(value => owner is CharacterCombatSkillModuleRule && value.Domain == CharacterSkillCapabilityDomain.Combat
                    || owner is CharacterManagementSkillModuleRule && value.Domain == CharacterSkillCapabilityDomain.Management)
                .Select(value => new Entry(null, value.CapabilityId)).ToArray();
        if (reference.Kind == ContentReferenceKind.FormulaMirror)
        {
            object parent = owner is CharacterSkillCapabilityFormulaDefinition
                ? GraphObjects(new[] { root }).FirstOrDefault(value => SerializedFields(value.GetType())
                    .Any(field => field.Name == "formula" && ReferenceEquals(field.GetValue(value), owner))) : owner;
            string id = parent == null ? string.Empty : ReadMember(parent, "capabilityId");
            return string.IsNullOrWhiteSpace(id) ? Array.Empty<Entry>() : new[] { new Entry(null, id) };
        }
        if (reference.Kind == ContentReferenceKind.EquipmentStat)
            return owner is EquipmentEvolutionFormulaCapabilityDefinition equipment
                && EquipmentEvolutionFormulaCapabilityDefinition.TryResolveApplicator(equipment.applicatorId, out string stat, out _)
                ? new[] { new Entry(null, stat) } : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.MetaEffectTarget)
            return owner is AuthoredGameplayEffectRecord effect
                ? (effect.kind == AuthoredGameplayEffectKind.MetaIntegerBonus ? MetaUpgradeEffectIds.IntegerTargets
                    : effect.kind == AuthoredGameplayEffectKind.MetaMultiplierDelta ? MetaUpgradeEffectIds.MultiplierTargets
                    : Array.Empty<string>()).Select(id => new Entry(null, id)).ToArray() : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.ServiceProcessFeature)
            return root is ServiceProcessSO process ? assets.OfType<BuildingSO>().SelectMany(value => value.Abilities)
                .OfType<BuildingServiceSupportAbility>().Where(value => value.SupportsHub(process.OwnerHubTag))
                .SelectMany(value => value.featureTags ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal)
                .Select(id => new Entry(null, id)).ToArray() : Array.Empty<Entry>();
        if (reference.Kind is ContentReferenceKind.ProductionSupport or ContentReferenceKind.ProductionBatchSupport)
            return root is ProductionRecipeSO production
                ? assets.OfType<BuildingSO>().SelectMany(value => value.Abilities)
                    .OfType<BuildingProductionSupportAbility>()
                    .Where(value => value.SupportsWorkstation(production.WorkstationTag)
                        && (reference.Kind != ContentReferenceKind.ProductionBatchSupport
                            || value.kind == ProductionSupportKind.BatchProcessor))
                    .SelectMany(value => value.featureTags ?? Array.Empty<string>())
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.Ordinal).Select(key => new Entry(null, key)).ToArray()
                : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.ServiceFeature)
            return root is BuildingSO building && building.GetAbility<BuildingServiceHubAbility>() is { } hub
                ? assets.OfType<BuildingSO>().SelectMany(value => value.Abilities)
                    .OfType<BuildingServiceSupportAbility>().Where(value => value.SupportsHub(hub.ServiceHubTag))
                    .SelectMany(value => value.featureTags ?? Array.Empty<string>())
                    .Where(key => !string.IsNullOrWhiteSpace(key))
                    .Distinct(StringComparer.Ordinal).Select(key => new Entry(null, key)).ToArray()
                : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.FacilityEvolutionModule)
            return root is FacilityEvolutionRecipeSO recipe && recipe.resultBuilding != null
                ? new EvolutionModuleRegistry().All.Where(module => module.IsPositiveModule
                    && FacilityEvolutionModifierApplicability.IsPositiveModuleEligibleForNewGeneration(
                        recipe.resultBuilding, module, out _))
                    .Select(module => new Entry(null, module.ModuleId)).ToArray()
                : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.CampaignTarget)
            return root is OffenseCampaignCatalogSO campaign
                ? (campaign.Targets ?? Array.Empty<OffenseTargetDefinition>()).Where(value => value != null)
                    .Select(value => new Entry(root, value.id)).ToArray() : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.AcquiredTraitBinding)
            return root is CharacterAcquiredTraitModuleSO acquired
                ? acquired.Effects.Where(value => value != null).Select(value => new Entry(root, value.bindingId)).ToArray()
                : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.SkillCapability)
        {
            if (root is CharacterAcquiredTraitModuleSO or CharacterAcquiredTraitSettingsSO)
                return assets.OfType<CharacterAcquiredTraitModuleSO>()
                    .Select(value => new Entry(value, value.CapabilityId)).ToArray();
            if (root is CharacterSkillSystemSettingsSO skillSettings)
                return skillSettings.Modules.Where(IsRegisteredSkillBehavior)
                    .Select(value => new Entry(root, CharacterSkillModuleCapabilityRegistry.Require(value).CapabilityId)).ToArray();
            return Array.Empty<Entry>();
        }
        if (reference.Kind == ContentReferenceKind.SkillVariant)
            return owner is CharacterSkillModuleSelection selection
                ? assets.OfType<CharacterSkillSystemSettingsSO>().SelectMany(settings =>
                    (settings.FindModule(selection.moduleId)?.variants ?? new List<CharacterSkillNumericVariant>())
                    .Where(value => value != null).Select(value => new Entry(settings, value.id))).ToArray()
                : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.EquipmentCapability)
            return root is EquipmentEvolutionFormulaCatalogSO equipment
                ? equipment.ReferenceCapabilityIds().Select(id => new Entry(root, id)).ToArray() : Array.Empty<Entry>();
        if (reference.Kind == ContentReferenceKind.FacilityCapability)
            return root is FacilityEvolutionRecipeSO facility
                ? facility.ReferenceCapabilityIds().Select(id => new Entry(root, id)).ToArray() : Array.Empty<Entry>();
        return Entries(reference);
    }

    private static bool IsRegisteredSkillBehavior(CharacterSkillModuleRule module)
    {
        if (module == null) return false;
        try { CharacterSkillModuleCapabilityRegistry.Require(module); return true; }
        catch (InvalidOperationException) { return false; } // Its own SkillBehavior field reports the invalid definition.
    }

    private static IEnumerable<object> GraphObjects(IEnumerable<ScriptableObject> roots)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>(roots.Where(value => value != null));
        while (pending.Count > 0)
        {
            object value = pending.Pop();
            if (value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum
                || !visited.Add(value) || value is UnityEngine.Object && value is not ScriptableObject) continue;
            yield return value;
            if (value is IEnumerable collection)
                foreach (object item in collection) pending.Push(item);
            else foreach (FieldInfo field in SerializedFields(value.GetType())) pending.Push(field.GetValue(value));
        }
    }

    public IReadOnlyList<Entry> AnatomyNodesFor(MedicalProcedureFamily family, bool allowsWildlife,
        IReadOnlyList<string> allowedFamilies, IReadOnlyList<string> allowedSpecies)
    {
        var characterProfiles = assets.OfType<CharacterSpeciesDefinitionSO>()
            .Select(value => value.anatomyProfileId).ToHashSet(StringComparer.Ordinal);
        return assets.OfType<AnatomyProfileSO>().Where(profile =>
                (allowsWildlife || characterProfiles.Contains(profile.ProfileId))
                && (profile.AnatomyFamily.Equals("construct", StringComparison.OrdinalIgnoreCase)
                    == (family == MedicalProcedureFamily.Construct))
                && (allowedFamilies.Count == 0 || allowedFamilies.Contains(profile.AnatomyFamily, StringComparer.OrdinalIgnoreCase))
                && (allowedSpecies.Count == 0 || profile.SpeciesIds.Any(id => allowedSpecies.Contains(id, StringComparer.OrdinalIgnoreCase))))
            .SelectMany(profile => profile.Nodes.Select(node => new Entry(profile, node.NodeId))).ToArray();
    }

    private Entry[] BuildEntries(ContentReferenceAttribute reference)
    {
        ContentReferenceKind kind = reference.Kind;
        if (RequiresOwnerContext(kind) && kind != ContentReferenceKind.SkillCapability)
            return Array.Empty<Entry>();
        if (kind == ContentReferenceKind.SkillModule)
            return assets.OfType<CharacterSkillSystemSettingsSO>().SelectMany(settings => settings.Modules
                .Where(value => value != null).Select(value => new Entry(settings, value.id))).ToArray();
        Type targetType = kind == ContentReferenceKind.Definition
            ? ResolveType(reference.DefinitionTypeName) : AssetType(kind);
        var result = new List<Entry>();
        IEnumerable<Entry> KeyEntries(IEnumerable<string> keys) => keys
            .Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal).Select(key => new Entry(null, key));
        IEnumerable<BuildingAbility> abilities = assets.OfType<BuildingSO>()
            .SelectMany(building => building.Abilities).Where(value => value != null);
        switch (kind)
        {
            case ContentReferenceKind.DefenseFamily:
                return KeyEntries(DefenseFacilityFamilyIds.Values).ToArray();
            case ContentReferenceKind.FacilityRecordMetric:
                return KeyEntries(FacilityEvolutionTerms.ProducedRecordMetricKeys).ToArray();
            case ContentReferenceKind.SpeciesIncident:
                return CharacterSpeciesIncidentIds.DeclaredIds.Select(id =>
                    new Entry(null, id, id + " / " + CharacterSpeciesIncidentIds.RequireOwner(id))).ToArray();
            case ContentReferenceKind.EnemyFaction:
                var factions = assets.OfType<EnemyArchetypeDefinitionSO>()
                    .Select(value => BackgroundFactionReactionRules.CanonicalFactionId(value.factionId))
                    .ToHashSet(StringComparer.Ordinal);
                var factionAliases = BackgroundFactionReactionRules.LegacyAliases.Where(alias =>
                    factions.Contains(BackgroundFactionReactionRules.CanonicalFactionId(alias)));
                return KeyEntries(factions.Concat(factionAliases)).ToArray();
            case ContentReferenceKind.EnemyAbilityTarget:
                return KeyEntries(EnemyAbilityTargetContract.Keys).ToArray();
            case ContentReferenceKind.EnemyTacticalTarget:
                return KeyEntries(EnemyTacticalTargetProtocols.TargetTags).ToArray();
            case ContentReferenceKind.EnemyTacticalOverride:
                return KeyEntries(new[] { EnemyTacticalTargetProtocols.Desperate }).ToArray();
            case ContentReferenceKind.BuildingSemanticTag:
            case ContentReferenceKind.ProductionFacilityTag:
                return KeyEntries(abilities.OfType<BuildingSemanticTagsAbility>().SelectMany(value => value.tags ?? Array.Empty<string>())
                    .Concat(kind == ContentReferenceKind.ProductionFacilityTag
                        ? new[] { "animal-pen", "butcher", "spoilage", "exterior-grass", "exterior-rock", "exterior-tree" }
                        : Array.Empty<string>())).ToArray();
            case ContentReferenceKind.CharacterNeed:
                return assets.OfType<GameDomainContentCatalogSO>().SelectMany(value => value.CharacterNeeds)
                    .Select(value => new Entry(null, value.id, value.displayName)).ToArray();
            case ContentReferenceKind.FacilityLineageTag:
                return KeyEntries(FacilityEvolutionUtility.ProducedLineageKeys).ToArray();
            case ContentReferenceKind.RoomTag:
                return KeyEntries(FacilityEvolutionUtility.ProducedLineageKeys.Concat(new[] { FacilityEvolutionTerms.Hygiene })
                    .Concat(GraphObjects(assets).OfType<FacilityEvolutionContributionData>()
                        .SelectMany(value => value.tags ?? Array.Empty<string>()))).ToArray();
            case ContentReferenceKind.FacilityPressure:
                return KeyEntries(FacilityIdentityPressureUtility.PressureKeys).ToArray();
            case ContentReferenceKind.FacilityMutationTag:
                return KeyEntries(FacilityIdentityPressureUtility.PressureKeys
                    .Concat(DefaultFacilityEvolutionMutationResolver.ExplicitEvidenceTags)
                    .Concat(Entries(new ContentReferenceAttribute(ContentReferenceKind.RoomScore)).Select(value => value.Id))
                    .Concat(assets.OfType<FacilityEvolutionRecordTokenDefinitionSO>().Select(value => value.TokenId))).ToArray();
            case ContentReferenceKind.CoreIncident:
                return Enum.GetValues(typeof(ExteriorIncidentKind)).Cast<ExteriorIncidentKind>()
                    .Where(value => value != ExteriorIncidentKind.None)
                    .Select(value => new Entry(null, ((int)value).ToString(CultureInfo.InvariantCulture), value.ToString())).ToArray();
            case ContentReferenceKind.CoreServiceCategory:
                return Enum.GetValues(typeof(ServiceCategory)).Cast<ServiceCategory>()
                    .Select(value => new Entry(null, ((int)value).ToString(CultureInfo.InvariantCulture), value.ToString())).ToArray();
            case ContentReferenceKind.CoreServiceMode:
                return new[] { ServiceOperationMode.Managed, ServiceOperationMode.Automated }
                    .Select(value => new Entry(null, ((int)value).ToString(CultureInfo.InvariantCulture), value.ToString())).ToArray();
            case ContentReferenceKind.WorldFlag:
                return KeyEntries(V20WorldFlagIds.Computed.Concat(GraphObjects(assets).OfType<V20ContentEffect>()
                    .Where(value => value.kind == V20ContentEffectKind.WorldFlag).Select(value => value.targetId))).ToArray();
            case ContentReferenceKind.FacilityOperationCapability:
                return KeyEntries(Enum.GetNames(typeof(FacilityCapabilityKind)).Where(value => value != nameof(FacilityCapabilityKind.None))).ToArray();
            case ContentReferenceKind.BattleCounter: return KeyEntries(OffenseBattleCounterRules.DeclaredTags).ToArray();
            case ContentReferenceKind.StartingProficiency:
                return assets.OfType<ProficiencyDefinitionSO>()
                    .Where(value => BuiltInCharacterProficiencyIds.All.Any(id => id.Value == value.StableId))
                    .Select(value => new Entry(value, value.StableId)).ToArray();
            case ContentReferenceKind.FormulaParameter:
                return KeyEntries(new[] { NarrativeFormulaParameterIds.Magnitude, NarrativeFormulaParameterIds.Duration,
                    NarrativeFormulaParameterIds.Count, NarrativeFormulaParameterIds.TargetCount }).ToArray();
            case ContentReferenceKind.EquipmentApplicator:
                return KeyEntries(EquipmentEvolutionFormulaCapabilityDefinition.ApplicatorIds).ToArray();
            case ContentReferenceKind.ProductionMassPolicy:
                return KeyEntries(ProductionMassExplanationCapabilityRegistry.CreateDefault().RegisteredKeys).ToArray();
            case ContentReferenceKind.ProductionOutputCostPolicy:
                return KeyEntries(ProductionOutputCostAllocationCapabilityRegistry.CreateDefault().RegisteredKeys).ToArray();
            case ContentReferenceKind.RoomScore:
                return KeyEntries(RoomProfileBuilder.ProducedScoreKeys.Concat(GraphObjects(assets).OfType<FacilityEvolutionContributionData>()
                    .SelectMany(value => value.scores ?? Array.Empty<FacilityEvolutionValue>()).Select(value => value.key))).ToArray();
            case ContentReferenceKind.RoomMetric:
                return KeyEntries(RoomProfileBuilder.ProducedMetricKeys.Concat(FacilityEvolutionRecordEventRecorder.ProducedMetricKeys)
                    .Concat(GraphObjects(assets).OfType<FacilityEvolutionContributionData>()
                        .SelectMany(value => value.metrics ?? Array.Empty<FacilityEvolutionValue>()).Select(value => value.key))).ToArray();
            case ContentReferenceKind.DecisionContextTag:
                return KeyEntries(new[] { "tools" }.Concat(assets.OfType<WeatherFrontDefinitionSO>().Select(value => value.stableId))).ToArray();
            case ContentReferenceKind.FixedTraitRule: return Array.Empty<Entry>();
            case ContentReferenceKind.FactionEconomicPolicy:
                return KeyEntries(FactionRouteEconomicPolicyRegistry.CreateBuiltIns(
                    new ResourceDungeonItemCatalogProvider(new ResourceItemDefinitionCatalog(assets.OfType<ItemDefinitionSO>())))
                    .Definitions.Select(value => value.capabilityId + "@" + value.capabilityVersion)).ToArray();
            case ContentReferenceKind.GameplayEffectTarget:
                return KeyEntries(GameplayEffectTargetIds.DeclaredTargets
                    .Concat(assets.OfType<CharacterFunctionalCapacityDefinitionSO>().Select(value => value.StableId))
                    .Concat(assets.OfType<ProficiencyDefinitionSO>().Select(value =>
                        GameplayEffectTargetIds.StartingProficiencyExperience(value.StableId)))).ToArray();
            case ContentReferenceKind.AnatomyFamily:
                return KeyEntries(assets.OfType<AnatomyProfileSO>().Select(value => value.AnatomyFamily)).ToArray();
            case ContentReferenceKind.StockCategory:
                return KeyEntries(Enum.GetValues(typeof(StockCategory)).Cast<StockCategory>()
                    .Select(StockCategoryPersistenceId.ToId)).ToArray();
            case ContentReferenceKind.FacilityEvolutionModule:
            case ContentReferenceKind.EquipmentEvolutionModule:
                return KeyEntries(new EvolutionModuleRegistry().All.Where(module =>
                    module.ModuleId.StartsWith(kind == ContentReferenceKind.FacilityEvolutionModule
                        ? "facility:" : "equipment:", StringComparison.Ordinal))
                    .Select(module => module.ModuleId)).ToArray();
            case ContentReferenceKind.ProductionWorkstation:
                return KeyEntries(abilities.OfType<BuildingProductionWorkstationAbility>()
                    .Select(value => value.WorkstationTag)).ToArray();
            case ContentReferenceKind.ProductionSupport:
            case ContentReferenceKind.ProductionBatchSupport:
                return KeyEntries(abilities.OfType<BuildingProductionSupportAbility>()
                    .Where(value => kind != ContentReferenceKind.ProductionBatchSupport
                        || value.kind == ProductionSupportKind.BatchProcessor)
                    .SelectMany(value => value.featureTags ?? Array.Empty<string>())).ToArray();
            case ContentReferenceKind.ServiceHub:
                return KeyEntries(abilities.OfType<BuildingServiceHubAbility>()
                    .Select(value => value.ServiceHubTag)).ToArray();
            case ContentReferenceKind.ServiceFeature:
                return KeyEntries(abilities.OfType<BuildingServiceSupportAbility>()
                    .SelectMany(value => value.featureTags ?? Array.Empty<string>())).ToArray();
            case ContentReferenceKind.WildlifeHabitat:
                return KeyEntries(Enum.GetValues(typeof(WildlifeHabitatType)).Cast<WildlifeHabitatType>()
                    .Select(value => value.ToString().ToLowerInvariant())
                    .Where(value => SeasonalWildlifeArrivalRules.TryParseHabitat(value, out _))).ToArray();
            case ContentReferenceKind.FacilityRole:
                return KeyEntries(FacilityRoleCatalog.All.Select(value => value.Id)).ToArray();
            case ContentReferenceKind.FacilityAnchorPurpose:
                return KeyEntries(FacilityAnchorPurposeCatalog.All.Select(value => value.PurposeId)).ToArray();
        }
        if (kind == ContentReferenceKind.WorkType)
            return BuiltInWorkTypeIds.All.Select(value => new Entry(null, value.Value)).ToArray();
        foreach (ScriptableObject asset in assets.Where(value => value != null).Distinct())
        {
            if (kind == ContentReferenceKind.WorkerTrait)
            {
                if (asset is CharacterTraitSO trait) result.Add(new Entry(asset, trait.DefinitionId.Value));
                else if (asset is HeritableTraitDefinitionSO heritable) result.Add(new Entry(asset, heritable.traitId));
                continue;
            }
            if (kind == ContentReferenceKind.SkillCapability)
            {
                if (asset is CharacterSkillSystemSettingsSO settings)
                    foreach (var module in settings.Modules.Where(IsRegisteredSkillBehavior))
                        result.Add(new Entry(asset, CharacterSkillModuleCapabilityRegistry.Require(module).CapabilityId));
                continue;
            }
            if (kind == ContentReferenceKind.GameplayValueTarget)
            {
                if (asset is GameplayEffectDefinitionSO effect
                    && GameplayEffectTargetIds.IsCharacterPerformanceFormulaTarget(effect.TargetId))
                    result.Add(new Entry(asset, effect.TargetId));
                continue;
            }
            if (kind == ContentReferenceKind.InvasionPattern)
            {
                if (asset is GameDomainContentCatalogSO domain)
                    foreach (var pattern in domain.InvasionPatterns)
                        result.Add(new Entry(asset, pattern.id));
                continue;
            }
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
                ContentReferenceKind.Weapon or ContentReferenceKind.Armor or ContentReferenceKind.Shield or ContentReferenceKind.Equipment => "equipmentId",
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
        if (kind == ContentReferenceKind.AuthoredEffectText)
            return owner is AuthoredGameplayEffectRecord authored ? authored.kind switch
            {
                AuthoredGameplayEffectKind.GuestDemandMultiplier => ContentReferenceKind.Species,
                AuthoredGameplayEffectKind.IntruderPattern => ContentReferenceKind.InvasionPattern,
                _ => null
            } : null;
        if (kind == ContentReferenceKind.EncounterTarget)
            return owner is OffenseEncounterSO encounter && encounter.objective == OffenseEncounterObjective.CaptureLeader
                ? ContentReferenceKind.EnemyArchetype : null;
        if (kind == ContentReferenceKind.WeightedTarget)
            return path.Contains("relatedEventWeights.") ? ContentReferenceKind.LifeEvent
                : path.Contains("otherCultureAttitudes.") ? ContentReferenceKind.Culture
                : path.Contains("factionReactions.") ? ContentReferenceKind.EnemyFaction : null;
        if (kind != ContentReferenceKind.EffectTarget) return kind;
        if (owner is not V20ContentEffect effect) return null;
        return effect.kind switch
        {
            V20ContentEffectKind.ItemGrant or V20ContentEffectKind.ItemConsume => ContentReferenceKind.Item,
            V20ContentEffectKind.DiseaseExposure => ContentReferenceKind.Disease,
            V20ContentEffectKind.ProficiencyExperience => ContentReferenceKind.Proficiency,
            V20ContentEffectKind.FactionRapport or V20ContentEffectKind.FactionGrievance
                or V20ContentEffectKind.FactionObligation => ContentReferenceKind.Faction,
            _ => null
        };
    }

    public static ContentReferenceAttribute ResolveReference(ContentReferenceAttribute reference, string path, object owner,
        ScriptableObject root = null)
    {
        if (reference.Kind == ContentReferenceKind.IdentityRuleProtocol)
            return new ContentReferenceAttribute(ContentReferenceKind.IdentityRuleProtocol)
                { ContextMember = path.Substring(path.LastIndexOf('.') + 1) };
        if (reference.Kind == ContentReferenceKind.DefenseFamily)
            return owner is DefenseFacilityData { enabled: false } ? null : reference;
        if (reference.Kind == ContentReferenceKind.FacilityRecordMetric)
            return owner is FacilityEvolutionRecordTokenDefinitionSO token
                && token.generationTrigger == FacilityEvolutionRecordTokenGenerationTrigger.MetricThresholdCrossing
                ? reference : null;
        if (reference.Kind == ContentReferenceKind.FacilityValueKey)
            return path.Contains("identityPressureWeights")
                ? new ContentReferenceAttribute(ContentReferenceKind.FacilityPressure) : null;
        if (reference.Kind == ContentReferenceKind.RoomProfileKey)
            return new ContentReferenceAttribute(path.Contains("requiredRoomScores") ? ContentReferenceKind.RoomScore : ContentReferenceKind.RoomMetric);
        if (reference.Kind == ContentReferenceKind.MetaEffectTarget)
            return owner is AuthoredGameplayEffectRecord { kind: AuthoredGameplayEffectKind.MetaIntegerBonus or AuthoredGameplayEffectKind.MetaMultiplierDelta }
                ? reference : null;
        if (reference.Kind == ContentReferenceKind.FixedTraitRule)
        {
            if (root is not CharacterTraitSO trait || owner == null) return null;
            return CharacterTraitIdentityRuleContract.TryGetExpectedKey(trait.id, owner.GetType(),
                path.Substring(path.LastIndexOf('.') + 1), out string key)
                ? new ContentReferenceAttribute(ContentReferenceKind.FixedTraitRule) { Aliases = key }
                : null;
        }
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
        var traitKeys = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
        foreach (var trait in assets.OfType<CharacterTraitSO>())
            foreach (string key in new[] { trait.DefinitionId.Value, trait.traitName }.Concat(trait.LegacyPolicyNames)
                         .Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal))
                if (traitKeys.TryGetValue(key, out var previous) && previous != trait)
                    errors.Add($"Worker trait identity/legacy alias '{key}' belongs to both '{previous.name}' and '{trait.name}'.");
                else traitKeys[key] = trait;
        foreach (var trait in assets.OfType<HeritableTraitDefinitionSO>())
            if (traitKeys.ContainsKey(trait.traitId))
                errors.Add($"Heritable trait '{trait.traitId}' conflicts with a worker trait identity/legacy alias.");
        foreach (var duplicate in assets.OfType<AIActionSet>()
                     .Where(value => value.CompositionRole == CharacterAiActionCompositionRole.RequiredDefault)
                     .GroupBy(value => (Type: value.GetType(), value.Branch))
                     .Where(group => group.Count() > 1))
            errors.Add($"Required AI default '{duplicate.Key.Type.Name}/{duplicate.Key.Branch}' occurs more than once.");
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
        // Collection elements establish their own definition scope just like
        // directly referenced SO fields; nested records retain that scope.
        if (value is ScriptableObject definition) root = definition;
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
        if (value is EnemyAbilityDefinitionSO enemyAbility)
            errors.AddRange(enemyAbility.ValidateDefinition().Select(error => path + ": " + error));
        if (value is AIActionSet aiAction
            && !Enum.IsDefined(typeof(CharacterAiActionCompositionRole), aiAction.CompositionRole))
            errors.Add(path + ": AI action composition role is not declared.");
        if (value is AIActionSet { CompositionRole: CharacterAiActionCompositionRole.RequiredDefault,
                Branch: CharacterAiBranch.None })
            errors.Add(path + ": required AI default needs an executable branch.");
        if (value is CharacterCombatAbilityDefinition combatAbility && !combatAbility.IsValid)
            errors.Add(path + ": combat ability identity, effects or targeting are invalid.");
        if (value is DefenseFacilityData defense)
            errors.AddRange(defense.ValidateDefinition().Select(error => path + ": " + error));
        if (value is FacilityEvolutionRecordTokenDefinitionSO recordToken)
            errors.AddRange(recordToken.ValidateDefinition().Select(error => path + ": " + error));
        if (value is HeritableTraitDefinitionSO heritable)
            errors.AddRange(heritable.ValidateDefinition().Select(error => path + ": " + error));
        if (value is CharacterAcquiredTraitModuleSO acquiredModule)
            errors.AddRange(acquiredModule.ValidateDefinition().Select(error => path + ": " + error));
        if (value is OffenseCampaignCatalogSO campaign)
            errors.AddRange(campaign.ValidateDefinition().Select(error => path + ": " + error));
        if (value is CharacterTraitSO authoredTrait)
            errors.AddRange(authoredTrait.ValidateDefinition().Select(error => path + ": " + error));
        if (value is StatChange needChange && string.IsNullOrWhiteSpace(needChange.needId))
            errors.Add(path + ": purchase need effect requires a character need.");
        if (value is DiseaseDefinitionSO authoredDisease)
            errors.AddRange(authoredDisease.ValidateDefinition().Select(error => path + ": " + error));
        if (value is FuneralCultureSO funeral && string.IsNullOrWhiteSpace(funeral.requiredFacilityTag))
            errors.Add(path + ": funeral culture requires a facility tag.");
        if (value is V20ContentEffect { kind: V20ContentEffectKind.ProficiencyExperience } proficiencyReward
            && !proficiencyReward.IsValid)
            errors.Add(path + ": proficiency reward requires a target and positive finite experience.");
        if (value is FacilityEvolutionRecipeSO facilityRecipe)
            errors.AddRange(facilityRecipe.ValidateDefinition().Select(error => path + ": " + error));
        if (value is RoomEnvironmentSettingsSO roomSettings)
            errors.AddRange(roomSettings.ValidateDefinition().Select(error => path + ": " + error));
        if (value is BuildingWorkAmountAbility workAmount)
            errors.AddRange(workAmount.ValidateDefaultWorkerPolicy().Select(error => path + ": " + error));
        if (value is ProductionRecipeSO production)
        {
            try
            {
                ProductionMassExplanationCapabilityRegistry.CreateDefault().ValidateRegistered(production.MassExplanation);
                ProductionOutputCostAllocationCapabilityRegistry.CreateDefault().ValidateForOutputs(production.OutputCostAllocation, production.Outputs);
            }
            catch (InvalidOperationException error) { errors.Add(path + ": " + error.Message); }
        }
        if (value is DungeonFactionDefinitionSO faction)
        {
            if (faction.tradeCargo?.Count > 0 && faction.tradeEconomicPolicy?.HasSelection != true)
                errors.Add($"{path}: trade cargo requires a selected economic policy.");
            if (faction.supplyCargo?.Count > 0 && faction.supplyEconomicPolicy?.HasSelection != true)
                errors.Add($"{path}: supply cargo requires a selected economic policy.");
        }
        if (value is CharacterSO ownerCharacter && ownerCharacter.IsOwnerCandidate)
        {
            try
            {
                CharacterOwnerFixedSkillUtility.GetSkills(ownerCharacter,
                    assets.OfType<CharacterSkillSystemSettingsSO>().SingleOrDefault()
                    ?? throw new InvalidOperationException("Owner fixed skills require registered skill settings."));
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            { errors.Add(path + ": " + exception.Message); }
        }
        if (value is CharacterSO { species: null })
            errors.Add($"{path}: a character archetype requires a registered phenotype species.");
        if (value is CharacterSO character && character.species != null)
        {
            if (!Entries(new ContentReferenceAttribute(ContentReferenceKind.Species)).Any(entry => entry.Asset == character.species)
                || !string.Equals(character.speciesTag, character.species.speciesTag, StringComparison.Ordinal))
                errors.Add($"{path}: character species object is unregistered or disagrees with its legacy species ID.");
        }
        if (value is SurgicalProcedureSO procedure)
        {
            var available = AnatomyNodesFor(procedure.Family, procedure.AllowsWildlife,
                procedure.AllowedAnatomyFamilies, procedure.AllowedSpeciesIds);
            if (procedure.AllowedAnatomyFamilies.Count > 0 && available.Count == 0)
                errors.Add($"{path}: procedure anatomy filters select no eligible profile.");
            if (!procedure.AllowsWildlife && assets.OfType<WildlifeSpeciesSO>().Any(species =>
                    procedure.AllowedSpeciesIds.Contains(species.SpeciesId, StringComparer.OrdinalIgnoreCase)))
                errors.Add($"{path}: wildlife species require a procedure that allows wildlife.");
            // An empty default is intentional for procedures whose node is
            // selected when ordering surgery. Only authored defaults are fixed.
            if (procedure.TargetNodeId.Length > 0 && !available.Any(entry => entry.Id == procedure.TargetNodeId))
                errors.Add($"{path}: target node '{procedure.TargetNodeId}' is unavailable in the procedure's eligible anatomy profiles.");
            foreach (string speciesId in procedure.AllowedSpeciesIds)
                if (!available.Any(entry => ((AnatomyProfileSO)entry.Asset).SpeciesIds.Contains(speciesId, StringComparer.OrdinalIgnoreCase)
                    && (procedure.TargetNodeId.Length == 0 || entry.Id == procedure.TargetNodeId)))
                    errors.Add($"{path}: selected species '{speciesId}' cannot use the procedure's target/anatomy family.");
        }
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
                var resolved = ResolveReference(reference, memberPath, value, root);
                if (resolved != null)
                {
                    if (member is IEnumerable collection && member is not string)
                    {
                        int index = 0;
                        foreach (object item in collection) ValidateId(item, resolved, reference.Kind, $"{memberPath}[{index++}]", errors, root, value);
                    }
                    else ValidateId(member, resolved, reference.Kind, memberPath, errors, root, value);
                }
            }
            Visit(member, memberPath, member as ScriptableObject ?? root, visited, errors);
        }
    }

    public static bool Matches(ContentReferenceAttribute reference, string left, string right) =>
        string.Equals(left, right, reference.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private void ValidateId(object value, ContentReferenceAttribute reference, ContentReferenceKind requested,
        string path, List<string> errors, ScriptableObject root, object owner)
    {
        string id = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (reference.Kind == ContentReferenceKind.FacilityAnchorPurpose)
        {
            if (!FacilityAnchorPurposeCatalog.TryGet(id, out _))
                errors.Add($"{path}: unknown facility anchor purpose '{id}'.");
            return;
        }
        if (reference.Kind == ContentReferenceKind.FactionEconomicPolicy
            && owner is FactionRouteEconomicPolicyDescriptor policy)
            id = policy.HasSelection ? id + "@" + policy.capabilityVersion.ToString(CultureInfo.InvariantCulture) : string.Empty;
        if (reference.Kind is ContentReferenceKind.ProductionMassPolicy or ContentReferenceKind.ProductionOutputCostPolicy)
        {
            int version = int.Parse(ReadMember(owner, "contractVersion"), CultureInfo.InvariantCulture);
            id = id.Length == 0 && version == 0 ? string.Empty : id + "@" + version.ToString(CultureInfo.InvariantCulture);
        }
        if (requested is ContentReferenceKind.AuthoredEffectText or ContentReferenceKind.MetaEffectTarget && string.IsNullOrWhiteSpace(id))
        {
            errors.Add($"{path}: {reference.Kind} effect requires a selected definition.");
            return;
        }
        if (id.Length == 0 || value is int number && number ==
            (reference.Kind == ContentReferenceKind.ResearchNumeric ? 0 : reference.EmptyNumericId)) return;
        if ((reference.Aliases ?? string.Empty).Split('|').Any(alias => Matches(reference, alias, id))) return;
        if (requested == ContentReferenceKind.EffectTarget && reference.Kind == ContentReferenceKind.Faction
            && (id == "affected-faction" || id == "requesting-faction")) return;
        if (RequiresOwnerContext(reference.Kind))
        {
            int localCount = Entries(reference, root, owner).Count(entry => Matches(reference, entry.Id, id));
            if (localCount != 1)
                errors.Add($"{path}: unknown or ambiguous owner-scoped {reference.Kind} reference '{id}'.");
            return;
        }
        string key = $"{reference.Kind}:{reference.DefinitionTypeName}:{reference.IdMemberName}:{reference.IgnoreCase}";
        if (!matchCounts.TryGetValue(key, out var counts))
        {
            counts = new Dictionary<string, int>(reference.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var entry in Entries(reference))
                counts[entry.Id] = counts.TryGetValue(entry.Id, out int prior) ? prior + 1 : 1;
            matchCounts[key] = counts;
        }
        counts.TryGetValue(id, out int count);
        if (count != 1 && !(reference.Kind is ContentReferenceKind.AnatomyNode or ContentReferenceKind.GameplayValueTarget && count > 0))
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
