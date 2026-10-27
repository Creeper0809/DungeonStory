using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R19ContentSelectionDebugScenarios
{
    private const string RootPath =
        "Assets/Resources/SO/GameContentCatalog.asset";
    private const string MissingId = "fixture:r19:missing";

    private static void VerifyNewIdentityField()
    {
        var previous = new ContentIdentityProtection.Snapshot();
        previous.assets.Add(new ContentIdentityProtection.AssetRecord
        {
            guid = "00000000000000000000000000000019", typeName = nameof(EndingDefinitionSO),
            fields = new List<ContentIdentityProtection.FieldRecord>
            {
                new() { path = "stableId", value = "ending:published" },
                new() { path = "legacyOptional", value = string.Empty }
            }
        });
        var current = JsonUtility.FromJson<ContentIdentityProtection.Snapshot>(JsonUtility.ToJson(previous));
        Require(ContentIdentityProtection.ValidateNewPublication(previous, current).Count == 0,
            "Existing optional legacy blank identity became invalid.");
        current.assets[0].fields.Add(new ContentIdentityProtection.FieldRecord { path = "newIdentity", value = string.Empty });
        Require(ContentIdentityProtection.ValidateNewPublication(previous, current).Any(value => value.Contains("newIdentity")),
            "A new blank identity on a published asset was accepted.");
        current.assets[0].fields.Last().value = "ending:new-published-field";
        Require(ContentIdentityProtection.ValidateNewPublication(previous, current).Count == 0,
            "Valid new identity did not pass retry after correction.");
    }

    [MenuItem("DungeonStory/V27/Content/Validate R19 Selections")]
    public static void RunAll()
    {
        VerifyNewIdentityField();
        GameContentCatalogSO root = RequireAsset<GameContentCatalogSO>(RootPath);
        ScriptableObject[] definitions = LoadDefinitions(root);
        BuildingSO[] buildings = definitions.OfType<BuildingSO>().ToArray();
        CharacterSkillSystemSettingsSO skillSource =
            root.GetCharacterSkillSettings<CharacterSkillSystemSettingsSO>()
            ?? throw new InvalidOperationException(
                "The registered character skill settings were not found.");
        GameDomainContentCatalogSO domainSource = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>().Single();
        CharacterAcquiredTraitModuleSO acquiredSource = definitions
            .OfType<CharacterAcquiredTraitModuleSO>()
            .FirstOrDefault(value => value.Formula != null)
            ?? throw new InvalidOperationException(
                "No registered acquired-trait formula owner was found.");
        FacilityEvolutionRecipeSO facilitySource = definitions
            .OfType<FacilityEvolutionRecipeSO>()
            .FirstOrDefault(value => value.formulaCapabilities?.Count > 0)
            ?? throw new InvalidOperationException(
                "No registered facility formula owner was found.");
        EquipmentEvolutionFormulaCatalogSO equipmentSource = definitions
            .OfType<EquipmentEvolutionFormulaCatalogSO>()
            .FirstOrDefault(value => value.name == "EquipmentEvolutionFormulaCatalog"
                && value.capabilities?.Count > 0)
            ?? throw new InvalidOperationException(
                "The registered equipment formula catalog was not found.");
        ProductionRecipeSO productionSource = definitions
            .OfType<ProductionRecipeSO>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No registered production recipe was found.");
        ServiceProcessSO processSource = definitions
            .OfType<ServiceProcessSO>()
            .FirstOrDefault(value => ExpectedServiceFeatures(
                buildings,
                value.OwnerHubTag).Any()
                && HasSelectableMode(value))
            ?? throw new InvalidOperationException(
                "No registered service process has a supported selectable mode.");
        BuildingSO serviceHubSource = buildings.FirstOrDefault(value =>
        {
            BuildingServiceHubAbility hub =
                value.GetAbility<BuildingServiceHubAbility>();
            return hub != null
                && ExpectedServiceFeatures(buildings, hub.ServiceHubTag).Any();
        }) ?? throw new InvalidOperationException(
            "No registered service hub has actual support features.");
        OffenseDecisionCardSO decisionSource = definitions
            .OfType<OffenseDecisionCardSO>()
            .FirstOrDefault(value => value.choices?.Count > 0)
            ?? throw new InvalidOperationException(
                "No registered offense decision card was found.");

        ScriptableObject[] sources =
        {
            skillSource,
            domainSource,
            acquiredSource,
            facilitySource,
            equipmentSource,
            productionSource,
            processSource,
            serviceHubSource,
            decisionSource
        };
        SourceSnapshot[] snapshots = sources.Distinct()
            .Select(SourceSnapshot.Capture).ToArray();
        ScriptableObject[] clones = sources.Select(Clone).ToArray();
        try
        {
            ContentReferenceCatalog catalog =
                ContentReferenceSelection.CreateCatalog(root);
            VerifyCoreCandidates(catalog);
            VerifyStartingProficiencies(catalog);
            VerifySkillAndFormulaSelections(
                catalog,
                (CharacterSkillSystemSettingsSO)clones[0],
                (CharacterAcquiredTraitModuleSO)clones[2],
                (FacilityEvolutionRecipeSO)clones[3],
                (EquipmentEvolutionFormulaCatalogSO)clones[4]);
            VerifyMetaEffectTargets(
                catalog,
                (GameDomainContentCatalogSO)clones[1]);
            VerifyServiceSelections(
                catalog,
                buildings,
                (ServiceProcessSO)clones[6],
                (BuildingSO)clones[7]);
            VerifyProductionSelections(
                catalog,
                (ProductionRecipeSO)clones[5]);
            VerifyRoomSelections(
                catalog,
                buildings,
                (FacilityEvolutionRecipeSO)clones[3]);
            VerifyDecisionSelections(
                catalog,
                definitions,
                (OffenseDecisionCardSO)clones[8]);
            Debug.Log(
                "R19_CONTENT_SELECTIONS=PASS; registry choices; owner context; rejection; repair retry; unchanged source");
        }
        finally
        {
            foreach (ScriptableObject clone in clones)
                Object.DestroyImmediate(clone);
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    private static void VerifyCoreCandidates(ContentReferenceCatalog catalog)
    {
        RequireCandidateIds(catalog, ContentReferenceKind.CoreIncident,
            new[] { "1", "2", "3", "4", "5", "6" });
        RequireCandidateIds(catalog, ContentReferenceKind.CoreServiceCategory,
            new[] { "0", "1", "2", "3", "4" });
        RequireCandidateIds(catalog, ContentReferenceKind.CoreServiceMode,
            new[] { "1", "2" });
    }

    private static void VerifyStartingProficiencies(
        ContentReferenceCatalog catalog)
    {
        string[] expected = BuiltInCharacterProficiencyIds.All
            .Select(value => value.Value).ToArray();
        Require(expected.Length == 9,
            "The built-in starting proficiency authority no longer has nine IDs.");
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.StartingProficiency,
            expected);
    }

    private static void VerifySkillAndFormulaSelections(
        ContentReferenceCatalog catalog,
        CharacterSkillSystemSettingsSO skill,
        CharacterAcquiredTraitModuleSO acquired,
        FacilityEvolutionRecipeSO facility,
        EquipmentEvolutionFormulaCatalogSO equipment)
    {
        string skillJson = EditorJsonUtility.ToJson(skill);
        CharacterSkillModuleRule[] modules = skill.Modules
            .Where(value => value != null).ToArray();
        CharacterSkillModuleRule selected = modules.FirstOrDefault(value =>
            value.formula != null)
            ?? throw new InvalidOperationException(
                "No registered skill module has formula metadata.");
        CharacterSkillModuleRule other = modules.FirstOrDefault(value =>
            !ReferenceEquals(value, selected));
        RequireClean(catalog.Validate(new ScriptableObject[] { skill }),
            "Character skill baseline");
        RequireSkillBehaviorCandidates(catalog, skill, selected);
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.FormulaMirror,
            new[] { selected.capabilityId },
            skill,
            selected.formula);

        selected.capabilityId = MissingId;
        RequireSkillBehaviorCandidates(catalog, skill, selected);
        if (other != null)
            RequireSkillBehaviorCandidates(catalog, skill, other);
        RequireRejected(catalog, skill, MissingId,
            "unknown skill behavior");
        Restore(skill, skillJson);
        RequireClean(catalog.Validate(new ScriptableObject[] { skill }),
            "Character skill repair");

        RequireCandidateIds(
            catalog,
            ContentReferenceKind.FormulaMirror,
            new[] { acquired.CapabilityId },
            acquired,
            acquired.Formula);
        FacilityEvolutionFormulaCapabilityDefinition facilityCapability =
            facility.formulaCapabilities.First(value => value != null);
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.FormulaMirror,
            new[] { facilityCapability.capabilityId },
            facility,
            facilityCapability);

        string[] parameters =
        {
            NarrativeFormulaParameterIds.Magnitude,
            NarrativeFormulaParameterIds.Duration,
            NarrativeFormulaParameterIds.Count,
            NarrativeFormulaParameterIds.TargetCount
        };
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.FormulaParameter,
            parameters);
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.EquipmentApplicator,
            EquipmentEvolutionFormulaCapabilityDefinition.ApplicatorIds);
        foreach (EquipmentEvolutionFormulaCapabilityDefinition capability in
                 equipment.capabilities.Where(value => value != null))
        {
            Require(EquipmentEvolutionFormulaCapabilityDefinition
                    .TryResolveApplicator(
                        capability.applicatorId,
                        out string statId,
                        out _),
                $"Equipment capability '{capability.capabilityId}' has no registered applicator.");
            RequireCandidateIds(
                catalog,
                ContentReferenceKind.EquipmentStat,
                new[] { statId },
                equipment,
                capability);
        }
        RequireClean(catalog.Validate(new ScriptableObject[]
            { acquired, facility, equipment }),
            "Formula owner baselines");
    }

    private static void VerifyMetaEffectTargets(
        ContentReferenceCatalog catalog,
        GameDomainContentCatalogSO domain)
    {
        string baseline = EditorJsonUtility.ToJson(domain);
        AuthoredGameplayEffectRecord integer = AllEffects(domain)
            .FirstOrDefault(value =>
                value?.kind == AuthoredGameplayEffectKind.MetaIntegerBonus)
            ?? throw new InvalidOperationException(
                "No registered integer meta effect was found.");
        AuthoredGameplayEffectRecord multiplier = AllEffects(domain)
            .FirstOrDefault(value =>
                value?.kind == AuthoredGameplayEffectKind.MetaMultiplierDelta)
            ?? throw new InvalidOperationException(
                "No registered multiplier meta effect was found.");
        AuthoredGameplayEffectRecord unrelated = AllEffects(domain)
            .FirstOrDefault(value => value != null
                && value.kind != AuthoredGameplayEffectKind.MetaIntegerBonus
                && value.kind != AuthoredGameplayEffectKind.MetaMultiplierDelta)
            ?? throw new InvalidOperationException(
                "No registered non-meta effect was found for contextual exclusion.");
        Require(MetaUpgradeEffectIds.IntegerTargets.Count == 4,
            "The integer meta target authority no longer has four IDs.");
        Require(MetaUpgradeEffectIds.MultiplierTargets.Count == 5,
            "The multiplier meta target authority no longer has five IDs.");
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.MetaEffectTarget,
            MetaUpgradeEffectIds.IntegerTargets,
            domain,
            integer);
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.MetaEffectTarget,
            MetaUpgradeEffectIds.MultiplierTargets,
            domain,
            multiplier);
        ContentReferenceAttribute metaReference =
            new(ContentReferenceKind.MetaEffectTarget);
        Require(ContentReferenceCatalog.ResolveReference(
                metaReference,
                "fixture.effects.[0].id",
                unrelated,
                domain) == null,
            "A non-meta gameplay effect retained the meta target reference.");
        Require(catalog.Entries(metaReference, domain, unrelated).Count == 0,
            "A non-meta gameplay effect received meta target candidates.");
        RequireClean(catalog.Validate(new ScriptableObject[] { domain }),
            "Meta effect baseline");
        integer.id = MissingId;
        RequireRejected(catalog, domain, MissingId,
            "unknown integer meta target");
        Restore(domain, baseline);
        RequireClean(catalog.Validate(new ScriptableObject[] { domain }),
            "Meta effect repair");
    }

    private static void VerifyServiceSelections(
        ContentReferenceCatalog catalog,
        IReadOnlyList<BuildingSO> buildings,
        ServiceProcessSO process,
        BuildingSO hubBuilding)
    {
        BuildingAbility[] abilities = buildings
            .SelectMany(value => value.Abilities)
            .Where(value => value != null).ToArray();
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.ServiceHub,
            abilities.OfType<BuildingServiceHubAbility>()
                .Select(value => value.ServiceHubTag));

        string processJson = EditorJsonUtility.ToJson(process);
        string hubJson = EditorJsonUtility.ToJson(hubBuilding);
        string[] processFeatures = ExpectedServiceFeatures(
            buildings,
            process.OwnerHubTag).ToArray();
        int modeCount = 0;
        foreach (ServiceOperationMode mode in new[]
                 { ServiceOperationMode.Managed, ServiceOperationMode.Automated })
        {
            if (!process.TryGetContract(mode, out ServiceModeProcessContract contract))
                continue;
            modeCount++;
            RequireCandidateIds(
                catalog,
                ContentReferenceKind.ServiceProcessFeature,
                processFeatures,
                process,
                contract);
            Require((contract.requiredFeatureTags ?? Array.Empty<string>())
                    .All(processFeatures.Contains),
                $"Service process '{process.ProcessId}' mode '{mode}' uses a feature outside its support hub.");
        }
        Require(modeCount > 0,
            "The selected service process has no managed or automated contract.");

        BuildingServiceHubAbility hub =
            hubBuilding.GetAbility<BuildingServiceHubAbility>()
            ?? throw new InvalidOperationException(
                "The selected service hub lost its hub ability.");
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.ServiceFeature,
            ExpectedServiceFeatures(buildings, hub.ServiceHubTag),
            hubBuilding,
            hub);
        RequireClean(catalog.Validate(new ScriptableObject[]
            { process, hubBuilding }),
            "Service owner baselines");

        SetString(process, "ownerHubTag", MissingId);
        RequireRejected(catalog, process, MissingId,
            "unregistered service process owner hub");
        Restore(process, processJson);
        Require(process.TryGetContract(
                ServiceOperationMode.Managed,
                out ServiceModeProcessContract selectedContract)
            || process.TryGetContract(
                ServiceOperationMode.Automated,
                out selectedContract),
            "The service process has no selectable contract after repair.");
        selectedContract.requiredFeatureTags = new[] { MissingId };
        RequireRejected(catalog, process, MissingId,
            "unsupported service process feature");
        Restore(process, processJson);
        Restore(hubBuilding, hubJson);
        RequireClean(catalog.Validate(new ScriptableObject[]
            { process, hubBuilding }),
            "Service owner repair");
    }

    private static void VerifyProductionSelections(
        ContentReferenceCatalog catalog,
        ProductionRecipeSO recipe)
    {
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.ProductionMassPolicy,
            ProductionMassExplanationCapabilityRegistry.CreateDefault()
                .RegisteredKeys);
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.ProductionOutputCostPolicy,
            ProductionOutputCostAllocationCapabilityRegistry.CreateDefault()
                .RegisteredKeys);

        string baseline = EditorJsonUtility.ToJson(recipe);
        RequireClean(catalog.Validate(new ScriptableObject[] { recipe }),
            "Production capability baseline");
        recipe.ConfigureMassExplanation(MissingId, 1, "fixture");
        RequireRejected(catalog, recipe, MissingId + "@1",
            "unknown production mass policy and version");
        Restore(recipe, baseline);
        recipe.ConfigureOutputCostAllocation(MissingId, 1, "fixture");
        RequireRejected(catalog, recipe, MissingId + "@1",
            "unknown production output policy and version");
        Restore(recipe, baseline);
        RequireClean(catalog.Validate(new ScriptableObject[] { recipe }),
            "Production capability repair");
    }

    private static void VerifyRoomSelections(
        ContentReferenceCatalog catalog,
        IReadOnlyList<BuildingSO> buildings,
        FacilityEvolutionRecipeSO facility)
    {
        IEnumerable<FacilityEvolutionContributionData> contributions = buildings
            .Select(value => value.Evolution)
            .Where(value => value != null);
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.RoomScore,
            RoomProfileBuilder.ProducedScoreKeys.Concat(contributions
                .SelectMany(value => value.scores
                    ?? Array.Empty<FacilityEvolutionValue>())
                .Select(value => value.key)));
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.RoomMetric,
            RoomProfileBuilder.ProducedMetricKeys
                .Concat(FacilityEvolutionRecordEventRecorder.ProducedMetricKeys)
                .Concat(contributions.SelectMany(value => value.metrics
                    ?? Array.Empty<FacilityEvolutionValue>())
                    .Select(value => value.key)));

        ContentReferenceAttribute unresolved =
            new(ContentReferenceKind.RoomProfileKey);
        Require(ContentReferenceCatalog.ResolveReference(
                    unresolved,
                    "fixture.requiredRoomScores.[0].key",
                    new FacilityEvolutionMetricRequirement(),
                    facility).Kind == ContentReferenceKind.RoomScore,
            "Room score context did not resolve to the score provider.");
        Require(ContentReferenceCatalog.ResolveReference(
                    unresolved,
                    "fixture.requiredRoomMetrics.[0].key",
                    new FacilityEvolutionMetricRequirement(),
                    facility).Kind == ContentReferenceKind.RoomMetric,
            "Room metric context did not resolve to the metric provider.");

        string baseline = EditorJsonUtility.ToJson(facility);
        RequireClean(catalog.Validate(new ScriptableObject[] { facility }),
            "Room requirement baseline");
        facility.requiredRoomMetrics = new[]
        {
            new FacilityEvolutionMetricRequirement
            {
                key = MissingId,
                requireMin = false,
                requireMax = true,
                maxValue = 1f
            }
        };
        RequireRejected(catalog, facility, MissingId,
            "unknown max-only room metric");
        Restore(facility, baseline);
        RequireClean(catalog.Validate(new ScriptableObject[] { facility }),
            "Room requirement repair");
    }

    private static void VerifyDecisionSelections(
        ContentReferenceCatalog catalog,
        IEnumerable<ScriptableObject> definitions,
        OffenseDecisionCardSO decision)
    {
        string[] expected = new[] { "tools" }
            .Concat(definitions.OfType<WeatherFrontDefinitionSO>()
                .Select(value => value.stableId))
            .ToArray();
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.DecisionContextTag,
            expected);
        string baseline = EditorJsonUtility.ToJson(decision);
        RequireClean(catalog.Validate(new ScriptableObject[] { decision }),
            "Decision context baseline");
        decision.choices[0].requiredTag = MissingId;
        RequireRejected(catalog, decision, MissingId,
            "unknown decision context tag");
        Restore(decision, baseline);
        RequireClean(catalog.Validate(new ScriptableObject[] { decision }),
            "Decision context repair");
    }

    private static void RequireSkillBehaviorCandidates(
        ContentReferenceCatalog catalog,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillModuleRule module)
    {
        CharacterSkillCapabilityDomain domain =
            module is CharacterCombatSkillModuleRule
                ? CharacterSkillCapabilityDomain.Combat
                : module is CharacterManagementSkillModuleRule
                    ? CharacterSkillCapabilityDomain.Management
                    : throw new InvalidOperationException(
                        $"Unknown skill module type '{module.GetType().Name}'.");
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.SkillBehavior,
            CharacterSkillModuleCapabilityRegistry.CapabilityIds
                .Select(CharacterSkillModuleCapabilityRegistry.Require)
                .Where(value => value.Domain == domain)
                .Select(value => value.CapabilityId),
            settings,
            module);
    }

    private static IEnumerable<AuthoredGameplayEffectRecord> AllEffects(
        GameDomainContentCatalogSO domain) =>
        domain.MetaUpgrades.SelectMany(value =>
                value?.effects ?? new List<AuthoredGameplayEffectRecord>())
            .Concat(domain.RunVariables.SelectMany(value =>
                value?.effects ?? new List<AuthoredGameplayEffectRecord>()))
            .Concat(domain.OwnerDoctrines.SelectMany(value =>
                value?.effects ?? new List<AuthoredGameplayEffectRecord>()));

    private static IEnumerable<string> ExpectedServiceFeatures(
        IEnumerable<BuildingSO> buildings,
        string hubTag) => buildings
        .SelectMany(value => value.Abilities)
        .OfType<BuildingServiceSupportAbility>()
        .Where(value => value.SupportsHub(hubTag))
        .SelectMany(value => value.featureTags ?? Array.Empty<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal);

    private static bool HasSelectableMode(ServiceProcessSO process) =>
        process.TryGetContract(ServiceOperationMode.Managed, out _)
        || process.TryGetContract(ServiceOperationMode.Automated, out _);

    private static void RequireCandidateIds(
        ContentReferenceCatalog catalog,
        ContentReferenceKind kind,
        IEnumerable<string> expected,
        ScriptableObject root = null,
        object owner = null)
    {
        ContentReferenceAttribute reference = new(kind);
        IEnumerable<string> actual = root == null
            ? catalog.Entries(reference).Select(value => value.Id)
            : catalog.Entries(reference, root, owner)
                .Select(value => value.Id);
        string[] expectedIds = Normalize(expected);
        string[] actualIds = Normalize(actual);
        Require(expectedIds.Length > 0,
            $"The independent {kind} provider returned no candidates.");
        Require(actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal),
            $"{kind} candidates disagree with their provider. Expected "
            + $"[{string.Join(", ", expectedIds)}], actual "
            + $"[{string.Join(", ", actualIds)}].");
    }

    private static void RequireRejected(
        ContentReferenceCatalog catalog,
        ScriptableObject clone,
        string expectedText,
        string context)
    {
        IReadOnlyList<string> errors =
            catalog.Validate(new[] { clone });
        Require(errors.Any(value => value.Contains(
                    expectedText,
                    StringComparison.OrdinalIgnoreCase)),
            $"Catalog accepted {context}: {string.Join(" | ", errors)}");
    }

    private static void RequireClean(
        IEnumerable<string> errors,
        string context)
    {
        string[] values = errors?.ToArray() ?? Array.Empty<string>();
        Require(values.Length == 0,
            context + " failed: " + string.Join(" | ", values));
    }

    private static string[] Normalize(IEnumerable<string> values) =>
        (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static void SetString(
        ScriptableObject target,
        string path,
        string value)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(path)
            ?? throw new InvalidOperationException(
                $"Serialized property '{path}' was not found on '{target.name}'.");
        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Restore(ScriptableObject clone, string json) =>
        EditorJsonUtility.FromJsonOverwrite(json, clone);

    private static ScriptableObject Clone(ScriptableObject source)
    {
        ScriptableObject clone = Object.Instantiate(source);
        clone.hideFlags = HideFlags.HideAndDontSave;
        return clone;
    }

    private static ScriptableObject[] LoadDefinitions(GameContentCatalogSO root) =>
        root.DomainCatalogs.OfType<GameDomainContentCatalogSO>()
            .SelectMany(value => value.Definitions)
            .Where(value => value != null)
            .Distinct()
            .ToArray();

    private static T RequireAsset<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException(
            $"Required asset '{path}' was not found.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class SourceSnapshot
    {
        private readonly ScriptableObject source;
        private readonly string json;
        private readonly bool dirty;

        private SourceSnapshot(ScriptableObject source)
        {
            this.source = source;
            json = EditorJsonUtility.ToJson(source);
            dirty = EditorUtility.IsDirty(source);
        }

        public static SourceSnapshot Capture(ScriptableObject source) =>
            new(source ?? throw new ArgumentNullException(nameof(source)));

        public void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(source) == json,
                $"R19 fixture changed source asset '{source.name}'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                $"R19 fixture changed dirty state for '{source.name}'.");
        }
    }
}
