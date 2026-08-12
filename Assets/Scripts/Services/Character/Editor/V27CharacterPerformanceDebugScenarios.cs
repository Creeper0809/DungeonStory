#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

public static class V27CharacterPerformanceDebugScenarios
{
    private const string RequestedLiveAuditKey =
        "DungeonStory.V27.CharacterPerformance.LiveAuditRequested";
    private const string RequestedConsumerAuditKey =
        "DungeonStory.V27.CharacterPerformance.ConsumerAuditRequested";
    private static int liveAuditWaitFrames;
    private static int consumerAuditWaitFrames;
    private static bool consumerAuditStarted;
    private static readonly List<GameObject> consumerAuditObjects = new();
    private static readonly List<CharacterSO> consumerAuditData = new();
    private const string CatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";

    [MenuItem("DungeonStory/Debug/V27/Run Character Performance Structural Audit")]
    public static void RunStructuralAudit()
    {
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Domain catalog is missing.");
        CharacterFunctionalCapacityDefinitionSO[] capacities = catalog
            .GetAll<CharacterFunctionalCapacityDefinitionSO>()
            .ToArray();
        CharacterPerformanceFormulaDefinitionSO[] formulas = catalog
            .GetAll<CharacterPerformanceFormulaDefinitionSO>()
            .ToArray();
        Require(capacities.Length == 14,
            $"Expected 14 capacities, found {capacities.Length}.");
        Require(capacities.Select(value => value.StableId).Distinct().Count() == 14,
            "Capacity stable ids are duplicated.");
        Require(capacities.All(value => value.StableId != "capacity:resource-efficiency"),
            "Foundational resource-efficiency capacity must not exist.");
        Require(formulas.Count(value =>
                value.Domain == CharacterPerformanceFormulaDomain.Composite) == 5,
            "Exactly five composite formulas are required.");
        Require(formulas.Count(value =>
                value.Domain == CharacterPerformanceFormulaDomain.Work
                && value.ResultChannel == CharacterPerformanceResultChannel.Speed) == 30,
            "Exactly 30 non-rest work-speed formulas are required.");
        Require(formulas.Count(value =>
                value.Domain == CharacterPerformanceFormulaDomain.Work
                && value.ResultChannel == CharacterPerformanceResultChannel.AccidentRisk) == 30,
            "Exactly 30 non-rest accident formulas are required.");
        Require(formulas.Count(value =>
                value.Domain == CharacterPerformanceFormulaDomain.Work
                && value.ResultChannel == CharacterPerformanceResultChannel.Quality) == 4,
            "Exactly four work-quality formulas are required.");
        Require(formulas.Count(value =>
                value.Domain == CharacterPerformanceFormulaDomain.Work
                && value.ResultChannel == CharacterPerformanceResultChannel.Yield) == 1,
            "Exactly one work-yield formula is required.");
        Require(formulas.Count(value =>
                value.Domain == CharacterPerformanceFormulaDomain.Combat) == 8,
            "Exactly eight combat-result formulas are required.");
        Require(formulas.Count(value =>
                value.Domain == CharacterPerformanceFormulaDomain.Medical) == 10,
            "Exactly ten medical-result formulas are required.");
        Require(formulas.Count(value =>
                value.Domain == CharacterPerformanceFormulaDomain.SurvivalSocial) == 16,
            "Exactly sixteen survival/social-result formulas are required.");
        CharacterPerformanceFormulaDefinitionSO[] mappedSpeeds = formulas
            .Where(value => value.ExecutionWorkTypeId.Length > 0
                && value.ResultChannel == CharacterPerformanceResultChannel.Speed)
            .ToArray();
        CharacterPerformanceFormulaDefinitionSO[] mappedAccidents = formulas
            .Where(value => value.ExecutionWorkTypeId.Length > 0
                && value.ResultChannel == CharacterPerformanceResultChannel.AccidentRisk)
            .ToArray();
        Require(mappedSpeeds.Length == 30
                && mappedSpeeds.Select(value => value.ExecutionWorkTypeId)
                    .Distinct(StringComparer.Ordinal).Count() == 30,
            "Exactly 30 unique work-speed execution mappings are required.");
        Require(mappedAccidents.Length == 30
                && mappedAccidents.Select(value => value.ExecutionWorkTypeId)
                    .Distinct(StringComparer.Ordinal).Count() == 30,
            "Exactly 30 unique work-accident execution mappings are required.");
        Require(mappedSpeeds.Single(value =>
                    value.ExecutionWorkTypeId == BuiltInWorkTypeIds.Treat.Value)
                .FormulaId == CharacterPerformanceFormulaIds.TreatmentSpeed,
            "Treatment work is not mapped to the medical treatment-speed formula.");
        Require(mappedSpeeds.Single(value =>
                    value.ExecutionWorkTypeId == BuiltInWorkTypeIds.Surgery.Value)
                .FormulaId == CharacterPerformanceFormulaIds.SurgerySpeed,
            "Surgery work is not mapped to the medical surgery-speed formula.");
        Require(formulas.All(value => value.ValidateDefinition().Count == 0),
            "At least one performance formula is invalid.");
        Require(formulas.All(value => value.SecondaryProficiencyWeight <= .2f),
            "A performance formula exceeds the 20% secondary-proficiency limit.");

        ResourceAnatomyProfileCatalog anatomyCatalog = new(
            catalog.GetAll<AnatomyProfileSO>());
        CharacterSpeciesSO[] species = catalog.GetAll<CharacterSpeciesSO>().ToArray();
        Require(species.Length == 10,
            $"Expected 10 character species, found {species.Length}.");
        ValidateSpeciesCapacityBalance(species);
        AnatomyProfileDefinition[] characterProfiles = species
            .Select(value => anatomyCatalog.GetForSpecies(value.speciesTag))
            .Distinct()
            .ToArray();
        System.Collections.Generic.List<string> missingCapacities = new();
        foreach (AnatomyProfileDefinition profile in characterProfiles)
        {
            foreach (CharacterFunctionalCapacityId capacityId in Enum
                         .GetValues(typeof(CharacterFunctionalCapacityId))
                         .Cast<CharacterFunctionalCapacityId>())
            {
                AnatomyFunction function = ToAnatomyFunction(capacityId);
                bool hasProducer = profile.Nodes.Any(node =>
                    (node.ExpandedFunctions & function) != 0);
                bool hasNotApplicableReason = profile.TryGetNotApplicableReason(
                    capacityId,
                    out string reason);
                if (!hasProducer && !hasNotApplicableReason)
                    missingCapacities.Add(
                        $"{profile.ProfileId}={CharacterFunctionalCapacityIds.GetStableId(capacityId)}");
                if (capacityId == CharacterFunctionalCapacityId.ArcaneConduction
                    && !hasProducer)
                    missingCapacities.Add(
                        $"{profile.ProfileId}=arcane-conduction-must-be-numeric ({reason})");
            }
        }
        Require(missingCapacities.Count == 0,
            "Missing functional-capacity producers: "
            + string.Join(", ", missingCapacities));
        Require(characterProfiles.All(profile => profile.Nodes.Any(node =>
                (node.ExpandedFunctions & AnatomyFunction.PhysicalPower) != 0)),
            "Every character anatomy profile must produce physical power.");
        Require(characterProfiles.All(profile => profile.Nodes.Any(node =>
                (node.ExpandedFunctions & AnatomyFunction.ImmuneDefense) != 0)),
            "Every character anatomy profile must produce immune defense.");

        float superhuman = 2.5f;
        CharacterFunctionalCapacityValue value = new(
            CharacterFunctionalCapacityId.PrecisionManipulation,
            true,
            superhuman,
            string.Empty,
            Array.Empty<CharacterPerformanceContributionTrace>());
        Require(Mathf.Approximately(value.Value, superhuman),
            "A 250% functional capacity was capped.");
        Debug.Log(
            "V27_CHARACTER_PERFORMANCE_STRUCTURAL_AUDIT=PASS; capacities=14; "
            + $"formulas={formulas.Length}; characterSpecies=10; "
            + $"characterAnatomyProfiles={characterProfiles.Length}; superhuman={value.Value:0.0}");
    }

    private static void ValidateSpeciesCapacityBalance(
        IReadOnlyCollection<CharacterSpeciesSO> allSpecies)
    {
        Dictionary<string, (string[] Strong, string[] Weak)> expectedAptitudes =
            new(StringComparer.Ordinal)
            {
                ["Slime"] = (Ids(BuiltInWorkTypeIds.Clean, BuiltInWorkTypeIds.Gather),
                    Ids(BuiltInWorkTypeIds.Guard, BuiltInWorkTypeIds.Quarry,
                        BuiltInWorkTypeIds.Logging)),
                ["Orc"] = (Ids(BuiltInWorkTypeIds.Guard, BuiltInWorkTypeIds.Haul,
                        BuiltInWorkTypeIds.Repair),
                    Ids(BuiltInWorkTypeIds.Research)),
                ["Vampire"] = (Ids(BuiltInWorkTypeIds.Research,
                        BuiltInWorkTypeIds.ThreatMitigation),
                    Ids(BuiltInWorkTypeIds.Haul, BuiltInWorkTypeIds.Clean)),
                ["Beastkin"] = (Ids(BuiltInWorkTypeIds.Haul, BuiltInWorkTypeIds.Restock,
                        BuiltInWorkTypeIds.Hunt, BuiltInWorkTypeIds.Reception),
                    Ids(BuiltInWorkTypeIds.Research, BuiltInWorkTypeIds.Surgery)),
                ["Demon"] = (Ids(BuiltInWorkTypeIds.Research, BuiltInWorkTypeIds.Guard),
                    Ids(BuiltInWorkTypeIds.Clean, BuiltInWorkTypeIds.AnimalCare)),
                ["Kobold"] = (Ids(BuiltInWorkTypeIds.Quarry, BuiltInWorkTypeIds.Repair,
                        BuiltInWorkTypeIds.Refuel),
                    Ids(BuiltInWorkTypeIds.Reception, BuiltInWorkTypeIds.Perform)),
                ["Myconid"] = (Ids(BuiltInWorkTypeIds.Sow, BuiltInWorkTypeIds.Harvest,
                        BuiltInWorkTypeIds.Treat, BuiltInWorkTypeIds.Clean),
                    Ids(BuiltInWorkTypeIds.Guard, BuiltInWorkTypeIds.Perform)),
                ["Harpy"] = (Ids(BuiltInWorkTypeIds.Reception, BuiltInWorkTypeIds.Guard,
                        BuiltInWorkTypeIds.Hunt),
                    Ids(BuiltInWorkTypeIds.Quarry, BuiltInWorkTypeIds.Construct)),
                ["Golem"] = (Ids(BuiltInWorkTypeIds.Haul, BuiltInWorkTypeIds.Construct,
                        BuiltInWorkTypeIds.Repair, BuiltInWorkTypeIds.Plumbing),
                    Ids(BuiltInWorkTypeIds.Reception, BuiltInWorkTypeIds.Research))
            };
        string[] broadPhysiologyTargets =
        {
            GameplayEffectTargetIds.WorkSpeed,
            GameplayEffectTargetIds.ResearchSpeed,
            GameplayEffectTargetIds.CombatPower,
            GameplayEffectTargetIds.MoveSpeed,
            GameplayEffectTargetIds.AccidentChance
        };
        CharacterSpeciesSO[] dungeonSpecies = allSpecies
            .Where(value => value != null
                && !string.Equals(
                    value.speciesTag,
                    "Adventurer",
                    StringComparison.Ordinal))
            .ToArray();
        Require(dungeonSpecies.Length == 9,
            $"Expected nine dungeon species, found {dungeonSpecies.Length}.");
        foreach (CharacterSpeciesSO species in dungeonSpecies)
        {
            GameplayEffectBinding[] capacityBindings = species.Effects
                .Where(value => value?.definition != null
                    && value.definition.TargetId.StartsWith(
                        "capacity:",
                        StringComparison.Ordinal))
                .ToArray();
            Require(capacityBindings.Length == 14,
                $"Species '{species.speciesTag}' requires 14 explicit capacity bindings.");
            Require(capacityBindings
                    .Select(value => value.definition.TargetId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == 14,
                $"Species '{species.speciesTag}' has duplicate capacity bindings.");
            Require(capacityBindings.All(value =>
                    value.value >= .80f && value.value <= 1.25f),
                $"Species '{species.speciesTag}' has a capacity outside 0.80~1.25.");
            float capacityAverage = capacityBindings.Average(value => value.value);
            Require(capacityAverage <= 1.050001f,
                $"Species '{species.speciesTag}' exceeds the +5% capacity budget "
                + $"with average {capacityAverage:0.000}.");
            Require(!species.Effects.Any(value => value?.definition != null
                    && broadPhysiologyTargets.Contains(
                        value.definition.TargetId,
                        StringComparer.Ordinal)),
                $"Species '{species.speciesTag}' retains a broad physiology modifier.");
            Require(!(species.strongWorkTypeIds ?? Array.Empty<string>())
                    .Intersect(
                        species.weakWorkTypeIds ?? Array.Empty<string>(),
                        StringComparer.Ordinal)
                    .Any(),
                $"Species '{species.speciesTag}' has overlapping strong/weak work.");
            Require(expectedAptitudes.TryGetValue(
                    species.speciesTag,
                    out (string[] Strong, string[] Weak) expected)
                && SameIds(species.strongWorkTypeIds, expected.Strong)
                && SameIds(species.weakWorkTypeIds, expected.Weak),
                $"Species '{species.speciesTag}' work aptitudes differ from the approved table.");
        }
        CharacterSpeciesSO orc = dungeonSpecies.Single(value =>
            string.Equals(value.speciesTag, "Orc", StringComparison.Ordinal));
        Require(Mathf.Approximately(orc.needs.hungerRateMultiplier, 1.20f)
                && Mathf.Approximately(orc.needs.thirstRateMultiplier, 1.05f),
            "Orc upkeep must use hunger 1.20 and thirst 1.05.");
        Require(CharacterSpeciesRuntimeSaveData.CurrentVersion == 3,
            "Species runtime save schema must include Golem wear/recharge V3.");
        BuildingSO manaStorage = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/M02.asset");
        if (manaStorage == null)
            manaStorage = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/P1/P1_ManaStorage.asset");
        Require(manaStorage != null
                && manaStorage.GetAbility<BuildingGolemRechargeAbility>()
                    is BuildingGolemRechargeAbility recharge
                && Mathf.Approximately(recharge.requiredWork, 100f)
                && recharge.materialQuantity == 1
                && string.Equals(
                    recharge.materialItemId,
                    "resource:mana-crystal",
                    StringComparison.Ordinal)
                && Mathf.Approximately(recharge.restoredCharge, 50f),
            "Mana storage lacks the explicit 1 crystal / 100 WU / 50 charge capability.");
    }

    private static string[] Ids(params WorkTypeId[] ids) =>
        ids.Select(value => value.Value).ToArray();

    private static bool SameIds(IEnumerable<string> actual, IEnumerable<string> expected) =>
        (actual ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual((expected ?? Array.Empty<string>())
                .OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);

    [MenuItem("DungeonStory/Debug/V27/Begin Character Performance Live Audit")]
    public static void BeginLiveAuditFromEditMode()
    {
        if (EditorApplication.isPlaying)
        {
            RunLiveAudit();
            return;
        }
        SessionState.SetBool(RequestedLiveAuditKey, true);
        liveAuditWaitFrames = 0;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void RegisterLiveAuditObserver()
    {
        EditorApplication.playModeStateChanged -= OnLiveAuditPlayModeChanged;
        EditorApplication.playModeStateChanged += OnLiveAuditPlayModeChanged;
    }

    private static void OnLiveAuditPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode
            || !SessionState.GetBool(RequestedLiveAuditKey, false))
            return;
        liveAuditWaitFrames = 0;
        EditorApplication.update -= TryRunRequestedLiveAudit;
        EditorApplication.update += TryRunRequestedLiveAudit;
    }

    private static void TryRunRequestedLiveAudit()
    {
        liveAuditWaitFrames++;
        bool ready = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Any(value => value?.Container != null);
        if (!ready && liveAuditWaitFrames < 600)
            return;
        EditorApplication.update -= TryRunRequestedLiveAudit;
        try
        {
            if (!ready)
                throw new InvalidOperationException(
                    "Runtime scope did not become ready within 600 editor frames.");
            RunLiveAudit();
            Debug.Log("PHASE153_SPECIES_LIVE_PROJECTION=PASS");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            SessionState.SetBool(RequestedLiveAuditKey, false);
            EditorApplication.isPlaying = false;
        }
    }

    [MenuItem("DungeonStory/Debug/V27/Run Character Performance Live Audit")]
    public static void RunLiveAudit()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Live audit requires Play Mode.");
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value?.Container != null)
            ?? throw new InvalidOperationException(
                "Dungeon runtime lifetime scope is not ready.");
        CharacterActor actor = UnityEngine.Object
            .FindObjectsByType<CharacterActor>(FindObjectsSortMode.None)
            .FirstOrDefault();
        GameObject createdObject = null;
        CharacterSO createdData = null;
        try
        {
            if (actor == null)
            {
                createdObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                    "V27 Performance Live Audit Character");
                scope.Container.InjectGameObject(createdObject);
                createdData = CharacterAiEditorTestDependencies
                    .CreateCharacterFixtureData(
                        CharacterType.NPC,
                        "V27 Performance Live Audit Character",
                        "Orc");
                actor = createdObject.GetComponent<CharacterActor>();
                actor.EnsureRuntimeState();
                actor.Identity.SetPersistentId(
                    new GuidPersistentIdGenerator().NewCharacterId());
                ICharacterNarrativeCommand narrative = scope.Container
                    .Resolve<ICharacterNarrativeCommand>();
                narrative.Register(
                    new CharacterId(actor.Identity.PersistentId),
                    new CharacterSpeciesId(createdData.speciesTag),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    BuiltInCharacterProficiencyIds.All.Select(id =>
                        new CharacterStartingProficiencyExperience
                        {
                            proficiencyId = id.Value,
                            experience = 100,
                            learningMultiplier = 1f
                        }).ToArray());
                actor.RefreshAbilityCache();
                actor.Initialize(createdData);
                actor.SetLifecycleState(CharacterLifecycleState.Active);
            }

            ICharacterPerformanceQuery query = scope.Container
                .Resolve<ICharacterPerformanceQuery>();
            VerifyAllSpeciesLiveProjection(scope, query);
            CharacterFunctionalCapacitySnapshot capacities = query
                .GetFunctionalCapacities(actor);
            Require(capacities.Values.Count == 14,
                $"Expected 14 live capacities, found {capacities.Values.Count}.");
            Require(capacities.Values.All(value => !value.IsApplicable
                    || (!float.IsNaN(value.Value)
                        && !float.IsInfinity(value.Value)
                        && value.Value >= 0f)),
                "A live functional capacity is negative or non-finite.");
            Require(capacities.Get(CharacterFunctionalCapacityId.ArcaneConduction)
                    .IsApplicable,
                "Arcane conduction must be numeric for every live character.");

            GameDomainContentCatalogSO catalog = AssetDatabase
                .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
                ?? throw new InvalidOperationException("Domain catalog is missing.");
            CharacterPerformanceFormulaDefinitionSO[] formulas = catalog
                .GetAll<CharacterPerformanceFormulaDefinitionSO>()
                .ToArray();
            int evaluated = 0;
            foreach (CharacterPerformanceFormulaDefinitionSO formula in formulas)
            {
                CharacterPerformanceEvaluationContext context = new()
                {
                    PrimaryProficiencyOverride =
                        BuiltInCharacterProficiencyIds.Fieldwork.Value,
                    SecondaryProficiencyOverride =
                        BuiltInCharacterProficiencyIds.Fieldwork.Value
                };
                CharacterPerformanceSnapshot snapshot = query.Evaluate(
                    actor,
                    formula.FormulaId,
                    context);
                Require(snapshot.IsApplicable,
                    $"Live formula '{formula.FormulaId}' is unavailable: "
                    + snapshot.Failure?.Message);
                Require(!float.IsNaN(snapshot.Value)
                    && !float.IsInfinity(snapshot.Value)
                    && snapshot.Value >= 0f,
                    $"Live formula '{formula.FormulaId}' returned {snapshot.Value}.");
                evaluated++;
            }

            float movement = actor.GetMoveSpeed();
            float haulSpeed = actor.GetWorkSpeedMultiplier(
                BuiltInWorkTypeIds.Haul);
            float consumption = actor.GetConsumptionMultiplier();
            float combat = actor.GetCombatPowerMultiplier();
            Require(new[] { movement, haulSpeed, consumption, combat }.All(value =>
                    !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f),
                "At least one live consumer returned a negative or non-finite value.");
            Debug.Log(
                $"V27_CHARACTER_PERFORMANCE_LIVE_AUDIT=PASS; "
                + $"character={actor.Identity?.PersistentId}; capacities=14; "
                + $"formulas={evaluated}; movement={movement:0.###}; "
                + $"haul={haulSpeed:0.###}; food={consumption:0.###}; "
                + $"combat={combat:0.###}");
        }
        finally
        {
            if (createdObject != null)
                UnityEngine.Object.Destroy(createdObject);
            if (createdData != null)
                UnityEngine.Object.Destroy(createdData);
        }
    }

    private static void VerifyAllSpeciesLiveProjection(
        DungeonRuntimeLifetimeScope scope,
        ICharacterPerformanceQuery query)
    {
        string[] speciesTags =
        {
            "Adventurer", "Slime", "Orc", "Vampire", "Beastkin",
            "Demon", "Kobold", "Myconid", "Harpy", "Golem"
        };
        List<GameObject> createdObjects = new();
        List<CharacterSO> createdData = new();
        Dictionary<string, Dictionary<string, float>> formulaValues =
            new(StringComparer.Ordinal);
        Dictionary<string, CharacterActor> actorsBySpecies =
            new(StringComparer.Ordinal);
        Dictionary<string, float> normalizedSleepLoss =
            new(StringComparer.Ordinal);
        try
        {
            ICharacterNarrativeCommand narrative = scope.Container
                .Resolve<ICharacterNarrativeCommand>();
            ICharacterDetailedStatsRuntime details = scope.Container
                .Resolve<ICharacterDetailedStatsRuntime>();
            GameDomainContentCatalogSO catalog = AssetDatabase
                .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
                ?? throw new InvalidOperationException("Domain catalog is missing.");
            CharacterPerformanceFormulaDefinitionSO[] formulas = catalog
                .GetAll<CharacterPerformanceFormulaDefinitionSO>()
                .ToArray();
            foreach (string speciesTag in speciesTags)
            {
                CharacterActor auditActor = CreateSpeciesAuditActor(
                    scope,
                    narrative,
                    speciesTag,
                    createdObjects,
                    createdData);
                actorsBySpecies[speciesTag] = auditActor;
                VerifySpeciesAptitude(auditActor, speciesTag);
                CharacterFunctionalCapacitySnapshot capacities = query
                    .GetFunctionalCapacities(auditActor);
                float[] expected = speciesTag == "Adventurer"
                    ? Enumerable.Repeat(1f, 14).ToArray()
                    : V27CharacterPerformanceContentAssetBuilder
                        .TryGetSpeciesCapacityMultipliers(speciesTag, out float[] row)
                            ? row
                            : throw new InvalidOperationException(
                                $"Missing capacity baseline for '{speciesTag}'.");
                CharacterFunctionalCapacityId[] capacityIds = Enum
                    .GetValues(typeof(CharacterFunctionalCapacityId))
                    .Cast<CharacterFunctionalCapacityId>()
                    .ToArray();
                for (int index = 0; index < capacityIds.Length; index++)
                {
                    CharacterFunctionalCapacityValue value = capacities.Get(
                        capacityIds[index]);
                    Require(value.IsApplicable,
                        $"Species '{speciesTag}' capacity '{value.StableId}' is N/A.");
                    Require(Mathf.Abs(value.Value - expected[index]) <= .001f,
                        $"Species '{speciesTag}' capacity '{value.StableId}' expected "
                        + $"{expected[index]:0.###}, got {value.Value:0.###}.");
                    Require(value.Contributions.Any(contribution =>
                            string.Equals(
                                contribution.SourceKind,
                                "Species",
                                StringComparison.OrdinalIgnoreCase))
                        || speciesTag == "Adventurer",
                        $"Species '{speciesTag}' capacity '{value.StableId}' lacks "
                        + "a species contribution trace.");
                }

                CharacterDetailedStatsSnapshot detailSnapshot = details.GetSnapshot(
                    auditActor);
                IReadOnlyList<CharacterDetailedStatRow> healthRows = detailSnapshot
                    .GetRows(CharacterDetailedStatsTab.HealthAnatomy);
                Require(capacityIds.All(id => healthRows.Any(row => string.Equals(
                        row.Id,
                        CharacterFunctionalCapacityIds.GetStableId(id),
                        StringComparison.Ordinal))),
                    $"Species '{speciesTag}' detail UI does not expose all 14 capacities.");
                if (speciesTag != "Adventurer")
                    Require(healthRows.Where(row => row.Id.StartsWith(
                            "capacity:", StringComparison.Ordinal))
                        .All(row => row.Detail.Contains(
                            "Species",
                            StringComparison.OrdinalIgnoreCase)),
                        $"Species '{speciesTag}' UI capacity trace omits its species source.");
                Require(detailSnapshot.GetRows(CharacterDetailedStatsTab.Work)
                        .Count(row => row.Id.StartsWith("performance:", StringComparison.Ordinal))
                    == formulas.Count(value =>
                        value.Domain == CharacterPerformanceFormulaDomain.Work),
                    $"Species '{speciesTag}' work UI formula row count is incomplete.");
                Require(detailSnapshot.GetRows(CharacterDetailedStatsTab.CombatEquipment)
                        .Count(row => row.Id.StartsWith("performance:", StringComparison.Ordinal))
                    == 8,
                    $"Species '{speciesTag}' combat UI formula row count is incomplete.");
                Require(detailSnapshot.GetRows(CharacterDetailedStatsTab.Modifiers)
                        .Count(row => row.Id.StartsWith("performance:", StringComparison.Ordinal))
                    == 21,
                    $"Species '{speciesTag}' composite/survival UI formula rows are incomplete.");

                Dictionary<string, float> evaluated = new(StringComparer.Ordinal);
                foreach (CharacterPerformanceFormulaDefinitionSO formula in formulas)
                {
                    CharacterPerformanceSnapshot snapshot = query.Evaluate(
                        auditActor,
                        formula.FormulaId,
                        new CharacterPerformanceEvaluationContext
                        {
                            PrimaryProficiencyOverride =
                                BuiltInCharacterProficiencyIds.Fieldwork.Value,
                            SecondaryProficiencyOverride =
                                BuiltInCharacterProficiencyIds.Fieldwork.Value
                        });
                    Require(snapshot.IsApplicable,
                        $"Species '{speciesTag}' formula '{formula.FormulaId}' is unavailable: "
                        + snapshot.Failure?.Message);
                    Require(!float.IsNaN(snapshot.Value)
                        && !float.IsInfinity(snapshot.Value)
                        && snapshot.Value >= 0f,
                        $"Species '{speciesTag}' formula '{formula.FormulaId}' returned "
                        + snapshot.Value);
                    evaluated[formula.FormulaId] = snapshot.Value;
                }
                formulaValues[speciesTag] = evaluated;

                float sleepBefore = auditActor.Stats.GetConditionValue(
                    CharacterCondition.SLEEP,
                    100f);
                auditActor.Stats.ChangesStat(
                    CharacterCondition.SLEEP,
                    100f - sleepBefore);
                float fatigue = query.Evaluate(
                    auditActor,
                    "performance:survival:fatigue-rate").Value;
                float sleepRate = auditActor.Identity.Data.species.needs
                    .sleepRateMultiplier;
                auditActor.Stats.ApplyWorkNeedDepletion(100f);
                float observedLoss = 100f - auditActor.Stats.GetConditionValue(
                    CharacterCondition.SLEEP,
                    100f);
                Require(fatigue > 0f && sleepRate > 0f && observedLoss > 0f,
                    $"Species '{speciesTag}' produced no measurable work-sleep loss.");
                normalizedSleepLoss[speciesTag] = observedLoss
                    / (fatigue * sleepRate);
                auditActor.Stats.ChangesStat(
                    CharacterCondition.SLEEP,
                    sleepBefore - auditActor.Stats.GetConditionValue(
                        CharacterCondition.SLEEP,
                        sleepBefore));
            }

            Require(normalizedSleepLoss.Values.Max()
                    - normalizedSleepLoss.Values.Min() <= .001f,
                "sleepRateMultiplier is not applied exactly once after fatigue projection: "
                + string.Join(",", normalizedSleepLoss.Select(pair =>
                    $"{pair.Key}={pair.Value:0.0000}")));
            VerifyGolemWearAndRecharge(
                scope,
                actorsBySpecies["Golem"],
                createdObjects,
                query,
                formulas);

            Dictionary<string, float> baseline = formulaValues["Adventurer"];
            string[] representativeRoles =
            {
                "performance:work:gather:speed",
                "performance:work:construct:speed",
                "performance:work:craft:speed",
                "performance:work:research:speed",
                "performance:work:reception:speed",
                CharacterPerformanceFormulaIds.MeleePower,
                "performance:combat:ranged-hit",
                CharacterPerformanceFormulaIds.ArcanePower,
                CharacterPerformanceFormulaIds.TreatmentEfficiency
            };
            Dictionary<string, int> wins = speciesTags
                .Skip(1)
                .ToDictionary(value => value, _ => 0, StringComparer.Ordinal);
            foreach (string role in representativeRoles)
            {
                Require(baseline.TryGetValue(role, out float baselineValue)
                    && baselineValue > 0f,
                    $"Representative role '{role}' lacks an Adventurer baseline.");
                float best = formulaValues
                    .Where(pair => pair.Key != "Adventurer")
                    .Max(pair => pair.Value[role] / baselineValue);
                foreach (string winner in wins.Keys.ToArray())
                {
                    if (formulaValues[winner][role] / baselineValue >= best - .0001f)
                        wins[winner]++;
                }
            }
            Require(wins.Values.All(value => value <= 3),
                "A species is the sole or tied leader in more than three representative roles: "
                + string.Join(", ", wins.Select(pair => $"{pair.Key}={pair.Value}")));

            foreach (string speciesTag in speciesTags.Skip(1))
            {
                float[] ratios = representativeRoles
                    .Select(role => formulaValues[speciesTag][role] / baseline[role])
                    .ToArray();
                string[] beneficialFormulaIds = formulas
                    .Where(formula => formula.ResultChannel
                            is not CharacterPerformanceResultChannel.AccidentRisk
                            and not CharacterPerformanceResultChannel.Consumption
                            and not CharacterPerformanceResultChannel.Exposure
                            and not CharacterPerformanceResultChannel.MoodDuration)
                    .Select(formula => formula.FormulaId)
                    .Where(id => baseline.TryGetValue(id, out float value) && value > 0f)
                    .ToArray();
                float[] beneficialRatios = beneficialFormulaIds
                    .Select(id => formulaValues[speciesTag][id] / baseline[id])
                    .ToArray();
                Require(beneficialRatios.Count(value =>
                        value >= 1.05f && value <= 1.1251f) >= 2,
                    $"Species '{speciesTag}' has fewer than two +5~+12.5% roles.");
                Require(beneficialRatios.Any(value =>
                        value >= .8499f && value <= .95f),
                    $"Species '{speciesTag}' has no -5~-15% role weakness.");
                Debug.Log(
                    $"PHASE153_SPECIES_ROLE_BANDS; species={speciesTag}; "
                    + string.Join(";", representativeRoles.Select((role, index) =>
                        $"{role}={ratios[index]:0.000}")));
            }
            Debug.Log(
                "PHASE153_SPECIES_MATRIX_LIVE=PASS; species=10; capacities=140; "
                + $"formulasPerSpecies={baseline.Count}; roleWinners="
                + string.Join(",", wins.Select(pair => $"{pair.Key}:{pair.Value}")));
        }
        finally
        {
            foreach (GameObject value in createdObjects)
                if (value != null) UnityEngine.Object.Destroy(value);
            foreach (CharacterSO value in createdData)
                if (value != null) UnityEngine.Object.Destroy(value);
        }
    }

    private static void VerifySpeciesAptitude(
        CharacterActor actor,
        string speciesTag)
    {
        if (speciesTag == "Adventurer") return;
        CharacterSpeciesSO species = actor.Identity?.Data?.species
            ?? throw new InvalidOperationException(
                $"Species audit actor '{speciesTag}' has no species definition.");
        string[] strong = species.strongWorkTypeIds ?? Array.Empty<string>();
        string[] weak = species.weakWorkTypeIds ?? Array.Empty<string>();
        Require(strong.Length > 0 && weak.Length > 0,
            $"Species '{speciesTag}' requires both strong and weak work aptitudes.");
        ProficiencyWorkProfile profile = new(
            BuiltInCharacterProficiencyIds.Fieldwork);
        WorkTypeId neutral = WorkTypeCatalog.All
            .Select(value => value.WorkTypeId)
            .First(value => !strong.Contains(value.Value, StringComparer.Ordinal)
                && !weak.Contains(value.Value, StringComparer.Ordinal));
        float neutralLearning = CharacterProficiencyLearningRules.Resolve(
            actor,
            profile,
            neutral);
        foreach (string workTypeId in strong)
        {
            WorkTypeId work = new(workTypeId);
            Require(Mathf.Approximately(
                    CharacterSpeciesWorkAptitudeRules.GetLearningMultiplier(actor, work),
                    1.10f)
                && Mathf.Approximately(
                    CharacterSpeciesWorkAptitudeRules
                        .GetAutonomousUtilityAdjustment(actor, work),
                    10f),
                $"Species '{speciesTag}' strong work '{workTypeId}' is not 1.10/+10.");
            Require(Mathf.Abs(
                    CharacterProficiencyLearningRules.Resolve(actor, profile, work)
                        / neutralLearning - 1.10f) <= .0001f,
                $"Species '{speciesTag}' strong XP consumer did not apply ×1.10.");
        }
        foreach (string workTypeId in weak)
        {
            WorkTypeId work = new(workTypeId);
            Require(Mathf.Approximately(
                    CharacterSpeciesWorkAptitudeRules.GetLearningMultiplier(actor, work),
                    .90f)
                && Mathf.Approximately(
                    CharacterSpeciesWorkAptitudeRules
                        .GetAutonomousUtilityAdjustment(actor, work),
                    -10f),
                $"Species '{speciesTag}' weak work '{workTypeId}' is not .90/-10.");
            Require(Mathf.Abs(
                    CharacterProficiencyLearningRules.Resolve(actor, profile, work)
                        / neutralLearning - .90f) <= .0001f,
                $"Species '{speciesTag}' weak XP consumer did not apply ×0.90.");
        }
    }

    private static void VerifyGolemWearAndRecharge(
        DungeonRuntimeLifetimeScope scope,
        CharacterActor golem,
        ICollection<GameObject> createdObjects,
        ICharacterPerformanceQuery performance,
        IReadOnlyList<CharacterPerformanceFormulaDefinitionSO> formulas)
    {
        CharacterId golemId = CharacterPersistentIdentity.Require(golem);
        ICharacterSpeciesCommand command = scope.Container
            .Resolve<ICharacterSpeciesCommand>();
        ICharacterSpeciesQuery query = scope.Container
            .Resolve<ICharacterSpeciesQuery>();
        IAnatomyHealthRuntime anatomy = scope.Container
            .Resolve<IAnatomyHealthRuntime>();
        Dictionary<string, float> burdenBefore = anatomy.GetAnatomySnapshot(golem)
            .Nodes.ToDictionary(
                value => value.nodeId,
                value => value.rejectionBurden,
                StringComparer.Ordinal);
        Require(command.RecordCompletedWork(
                golemId,
                BuiltInWorkTypeIds.Haul.Value,
                99f,
                out DomainFailure firstFailure),
            "Golem 99 WU wear record failed: " + firstFailure.Code);
        Require(query.TryGet(golemId, out CharacterSpeciesRuntimeState at99)
                && at99.CompletedWorkIndex == 0
                && Mathf.Abs(at99.WearWorkRemainder - 99f) <= .001f
                && Mathf.Abs(at99.Integrity - 100f) <= .001f,
            "Golem wear was applied before a completed 100 WU block.");
        Require(command.RecordCompletedWork(
                golemId,
                BuiltInWorkTypeIds.Haul.Value,
                1f,
                out DomainFailure secondFailure),
            "Golem 100th WU wear record failed: " + secondFailure.Code);
        Require(query.TryGet(golemId, out CharacterSpeciesRuntimeState at100)
                && at100.CompletedWorkIndex == 1
                && at100.WearWorkRemainder <= .001f
                && Mathf.Abs(at100.Integrity - 97.5f) <= .001f,
            "Golem wear is not exactly 2.5 per completed 100 WU.");
        Require(anatomy.GetAnatomySnapshot(golem).Nodes.Any(value =>
                burdenBefore.TryGetValue(value.nodeId, out float before)
                && value.rejectionBurden > before),
            "Golem wear did not burden a formula-producing anatomy node.");

        ICharacterSpeciesPersistence persistence = scope.Container
            .Resolve<ICharacterSpeciesPersistence>();
        CharacterSpeciesRuntimeSaveData loweredCharge = persistence.Capture();
        CharacterSpeciesRuntimeRecordSaveData golemRecord = loweredCharge.characters
            .Single(value => string.Equals(
                value.characterInstanceId,
                golemId.Value,
                StringComparison.Ordinal));
        golemRecord.charge = 20f;
        persistence.Restore(persistence.BuildRestore(loweredCharge));

        BuildingSO manaStorage = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/M02.asset")
            ?? AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/P1/P1_ManaStorage.asset")
            ?? throw new InvalidOperationException("Mana storage asset is missing.");
        GameObject facilityObject = new("Phase153 Golem Recharge Facility");
        createdObjects.Add(facilityObject);
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        scope.Container.InjectGameObject(facilityObject);
        facility.Initialization(manaStorage, new Vector2Int(997, 997));

        WorldItemRepository items = scope.Container.Resolve<WorldItemRepository>();
        IStockQuery stock = scope.Container.Resolve<IStockQuery>();
        string crystalStackId = items.AddEditorTestStack(
            "resource:mana-crystal",
            1,
            WorldItemStackState.Loose);
        ICharacterSpeciesRechargeService recharge = scope.Container
            .Resolve<ICharacterSpeciesRechargeService>();
        try
        {
            Require(recharge.IsRechargeAvailable(golem, facility, out string reason),
                "Golem recharge was unavailable at charge 20: " + reason);
            Require(recharge.GetRechargeUrgency(golem, facility) >= 65f,
                "Golem recharge urgency was not raised below charge 35.");
            Require(recharge.TryBeginRecharge(
                    golem,
                    facility,
                    out float startedWork,
                    out DomainFailure beginFailure)
                && startedWork <= .001f,
                "Golem recharge reservation failed: " + beginFailure.Code);
            Require(stock.GetAllStacks().Single(value =>
                    string.Equals(value.StackId, crystalStackId, StringComparison.Ordinal))
                    .HasReservations,
                "Golem recharge did not reserve its mana crystal.");
            recharge.CancelRecharge(golemId);
            Require(stock.GetAllStacks().Single(value =>
                    string.Equals(value.StackId, crystalStackId, StringComparison.Ordinal))
                    .HasReservations == false,
                "Cancelling Golem recharge did not release its crystal.");

            Require(recharge.TryBeginRecharge(
                    golem,
                    facility,
                    out _,
                    out beginFailure),
                "Golem recharge restart failed: " + beginFailure.Code);
            Require(recharge.TryApplyRechargeWork(
                    golem,
                    facility,
                    50f,
                    out bool halfCompleted,
                    out DomainFailure halfFailure)
                && !halfCompleted,
                "Golem recharge completed before 100 WU: " + halfFailure.Code);
            CharacterSpeciesRuntimeSaveData midOrder = persistence.Capture();
            CharacterSpeciesRuntimeRecordSaveData midRecord = midOrder.characters
                .Single(value => string.Equals(
                    value.characterInstanceId,
                    golemId.Value,
                    StringComparison.Ordinal));
            Require(midRecord.rechargeProgressWork > 49f
                    && midRecord.rechargeProgressWork < 51f,
                "Golem recharge progress was not saved at 50 WU.");
            persistence.Restore(persistence.BuildRestore(midOrder));
            Require(recharge.TryApplyRechargeWork(
                    golem,
                    facility,
                    50f,
                    out bool completed,
                    out DomainFailure completeFailure)
                && completed,
                "Golem recharge did not resume and complete: " + completeFailure.Code);
            Require(query.TryGet(golemId, out CharacterSpeciesRuntimeState charged)
                    && Mathf.Abs(charged.Charge - 70f) <= .001f
                    && charged.RechargeProgressWork <= .001f
                    && string.IsNullOrEmpty(charged.RechargeMaterialStackId),
                "Golem recharge did not restore exactly 50 charge and clear order state.");
            Require(!stock.GetAllStacks().Any(value => string.Equals(
                    value.StackId,
                    crystalStackId,
                    StringComparison.Ordinal)),
                "Golem recharge did not atomically consume its reserved crystal.");
        }
        finally
        {
            recharge.CancelRecharge(golemId);
            if (stock.GetAllStacks().Any(value => string.Equals(
                    value.StackId,
                    crystalStackId,
                    StringComparison.Ordinal)))
                items.RemoveEditorTestStack(crystalStackId);
        }
        string beforeRestore = BuildDerivedSignature(performance, golem, formulas);
        CharacterSpeciesRuntimeSaveData saved = persistence.Capture();
        string payload = JsonUtility.ToJson(saved);
        CharacterSpeciesRuntimeSaveData decoded = JsonUtility
            .FromJson<CharacterSpeciesRuntimeSaveData>(payload);
        persistence.Restore(persistence.BuildRestore(decoded));
        Require(query.TryGet(golemId, out CharacterSpeciesRuntimeState roundTrip)
                && roundTrip.CompletedWorkIndex == 1
                && Mathf.Abs(roundTrip.Integrity - 97.5f) <= .001f
                && Mathf.Abs(roundTrip.Charge - 70f) <= .001f
                && roundTrip.RechargeProgressWork <= .001f,
            "Golem V3 species state changed across JSON save round trip.");
        Require(string.Equals(
                beforeRestore,
                BuildDerivedSignature(performance, golem, formulas),
                StringComparison.Ordinal),
            "Golem capacities, formulas, or contribution traces changed after restore.");
        Require(command.RecordCompletedWork(
                golemId,
                BuiltInWorkTypeIds.Haul.Value,
                1900f,
                out DomainFailure maintenanceWearFailure),
            "Golem maintenance-threshold wear failed: "
            + maintenanceWearFailure.Code);
        Require(query.TryGet(golemId, out CharacterSpeciesRuntimeState maintenanceState)
                && Mathf.Abs(maintenanceState.Integrity - 50f) <= .001f,
            "Golem did not reach the exact maintenance suggestion threshold.");
        Require(scope.Container.Resolve<ISurgeryQuery>()
                .TryGetAutomaticMaintenanceSuggestion(
                    golem,
                    out string maintenanceProcedureId,
                    out string maintenanceNodeId)
                && string.Equals(
                    maintenanceProcedureId,
                    "procedure:golem-power-core",
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(maintenanceNodeId),
            "Golem integrity <= 50 did not produce a power-core maintenance suggestion.");
        SurgicalProcedureSO maintenanceProcedure = Resources
            .LoadAll<SurgicalProcedureSO>(SurgicalProcedureSO.ResourcePath)
            .Single(value => string.Equals(
                value.ProcedureId,
                "procedure:golem-power-core",
                StringComparison.Ordinal));
        Require(Mathf.Approximately(maintenanceProcedure.RequiredWork, 26f)
                && maintenanceProcedure.Materials.Count == 1
                && string.Equals(
                    maintenanceProcedure.Materials[0].itemId,
                    "material:lumber",
                    StringComparison.Ordinal)
                && maintenanceProcedure.Materials[0].quantity == 1
                && maintenanceProcedure.Effects
                    .OfType<MaintainSurgicalPartEffect>()
                    .Any(value => Mathf.Approximately(value.durability, 30f)),
            "Power-core maintenance must consume 26 WU and one lumber to repair 30.");
        Debug.Log(
            "PHASE153_GOLEM_UPKEEP_LIVE=PASS; wear=2.5/100WU; "
            + "recharge=1crystal+100WU+50charge; maintenance=26WU+1lumber+30; "
            + "cancel=rollback; save=resume");
        Debug.Log(
            "PHASE153_SPECIES_SAVE_FOCUSED=PASS; schema=V3; capacities=14; "
            + $"formulas={formulas.Count}; wear=1; charge=70");
    }

    private static CharacterActor CreateSpeciesAuditActor(
        DungeonRuntimeLifetimeScope scope,
        ICharacterNarrativeCommand narrative,
        string speciesTag,
        ICollection<GameObject> createdObjects,
        ICollection<CharacterSO> createdData)
    {
        GameObject actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
            $"Phase153 {speciesTag} Audit Character");
        createdObjects.Add(actorObject);
        actorObject.SetActive(false);
        if (actorObject.GetComponent<AbilityWork>() == null)
            actorObject.AddComponent<AbilityWork>();
        scope.Container.InjectGameObject(actorObject);
        actorObject.SetActive(true);
        CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
            CharacterType.NPC,
            $"Phase153 {speciesTag} Audit Character",
            speciesTag);
        createdData.Add(data);
        CharacterActor actor = actorObject.GetComponent<CharacterActor>();
        actor.EnsureRuntimeState();
        actor.Identity.SetPersistentId(new GuidPersistentIdGenerator().NewCharacterId());
        narrative.Register(
            new CharacterId(actor.Identity.PersistentId),
            new CharacterSpeciesId(speciesTag),
            Array.Empty<string>(),
            Array.Empty<string>(),
            BuiltInCharacterProficiencyIds.All.Select(id =>
                new CharacterStartingProficiencyExperience
                {
                    proficiencyId = id.Value,
                    experience = 100,
                    learningMultiplier = 1f
                }).ToArray());
        actor.RefreshAbilityCache();
        actor.Initialize(data);
        actor.Progression.GrowthState.traitIds = new List<int>();
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        // The fixture is already active before the runtime scope is injected, so its
        // first OnEnable cannot publish into the live registries. Reconcile through the
        // bridge after the authoritative container and identity are present.
        CharacterActorRuntimeBridge bridge = actorObject
            .GetComponent<CharacterActorRuntimeBridge>();
        MethodInfo reconcile = typeof(CharacterActorRuntimeBridge).GetMethod(
            "ReconcilePublishedRegistration",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(
                nameof(CharacterActorRuntimeBridge),
                "ReconcilePublishedRegistration");
        reconcile.Invoke(bridge, null);
        return actor;
    }

    [MenuItem("DungeonStory/Debug/V27/Begin Character Performance Consumer Audit")]
    public static void BeginConsumerExecutionAuditFromEditMode()
    {
        if (EditorApplication.isPlaying)
        {
            BeginConsumerExecutionAudit();
            return;
        }
        SessionState.SetBool(RequestedConsumerAuditKey, true);
        consumerAuditWaitFrames = 0;
        consumerAuditStarted = false;
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void RegisterConsumerAuditObserver()
    {
        EditorApplication.playModeStateChanged -= OnConsumerAuditPlayModeChanged;
        EditorApplication.playModeStateChanged += OnConsumerAuditPlayModeChanged;
    }

    private static void OnConsumerAuditPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode
            || !SessionState.GetBool(RequestedConsumerAuditKey, false))
            return;
        consumerAuditWaitFrames = 0;
        consumerAuditStarted = false;
        EditorApplication.update -= TryRunRequestedConsumerAudit;
        EditorApplication.update += TryRunRequestedConsumerAudit;
    }

    private static void TryRunRequestedConsumerAudit()
    {
        consumerAuditWaitFrames++;
        bool finished = false;
        bool ready = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Any(value => value?.Container != null);
        if (!ready && consumerAuditWaitFrames < 600)
            return;
        try
        {
            if (!ready)
                throw new InvalidOperationException(
                    "Runtime scope did not become ready within 600 editor frames.");
            if (!consumerAuditStarted)
            {
                BeginConsumerExecutionAudit();
                consumerAuditStarted = true;
                consumerAuditWaitFrames = 0;
                return;
            }
            if (consumerAuditWaitFrames < 180)
                return;
            CompleteConsumerExecutionAudit();
            Debug.Log("PHASE153_SPECIES_CONSUMERS=PASS");
            finished = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            CleanupConsumerAuditFixtures();
            finished = true;
        }
        finally
        {
            if (finished)
            {
                EditorApplication.update -= TryRunRequestedConsumerAudit;
                SessionState.SetBool(RequestedConsumerAuditKey, false);
                consumerAuditStarted = false;
                EditorApplication.isPlaying = false;
            }
        }
    }

    public static void BeginConsumerExecutionAudit()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException(
                "Consumer execution audit requires Play Mode.");
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value?.Container != null)
            ?? throw new InvalidOperationException(
                "Dungeon runtime lifetime scope is not ready.");
        ICharacterLifetimeQuery lifetime = scope.Container
            .Resolve<ICharacterLifetimeQuery>();
        CharacterActor[] actors = lifetime.AllCharacters
            .Where(value => value != null
                && value.gameObject.activeInHierarchy
                && !value.IsDead
                && value.Stats != null)
            .Take(2)
            .ToArray();
        if (actors.Length < 2)
        {
            CleanupConsumerAuditFixtures();
            ICharacterNarrativeCommand narrative = scope.Container
                .Resolve<ICharacterNarrativeCommand>();
            CreateSpeciesAuditActor(
                scope,
                narrative,
                "Adventurer",
                consumerAuditObjects,
                consumerAuditData);
            CreateSpeciesAuditActor(
                scope,
                narrative,
                "Slime",
                consumerAuditObjects,
                consumerAuditData);
            actors = lifetime.AllCharacters
                .Where(value => value != null
                    && value.gameObject.activeInHierarchy
                    && !value.IsDead
                    && value.Stats != null)
                .Take(2)
                .ToArray();
        }
        Require(actors.Length >= 2,
            "Consumer execution audit requires two active characters.");
        CharacterActor actor = actors[0];
        CharacterActor other = actors[1];
        CharacterId actorId = CharacterPersistentIdentity.Require(actor);
        CharacterPerformanceExecutionTrace.Clear();

        float originalHunger = actor.Stats.GetConditionValue(
            CharacterCondition.HUNGER,
            100f);
        string moodEventId = "audit:negative-mood-duration";
        List<int> originalTraits = actor.Progression.GrowthState.traitIds?
            .ToList() ?? new List<int>();
        try
        {
            actor.Stats.ChangesStat(
                CharacterCondition.HUNGER,
                40f - originalHunger);
            float hungerBefore = actor.Stats.GetConditionValue(
                CharacterCondition.HUNGER,
                0f);
            scope.Container.Resolve<ICharacterConsumablesWorldPort>()
                .RecoverHunger(actorId, 2f);
            float hungerAfter = actor.Stats.GetConditionValue(
                CharacterCondition.HUNGER,
                0f);
            Require(hungerAfter > hungerBefore,
                "Nutrition consumer did not change authoritative hunger state.");

            ICharacterPerformanceQuery performance = scope.Container
                .Resolve<ICharacterPerformanceQuery>();
            VerifyPowerAndImmuneCapacityCausality(
                scope,
                actor,
                performance);
            CharacterPerformanceSnapshot moodDuration = performance.Evaluate(
                actor,
                CharacterPerformanceFormulaIds.NegativeMoodDuration);
            scope.Container.Resolve<CharacterMoodPolicyService>().ApplySeconds(
                actor,
                moodEventId,
                -1f,
                60f,
                "V27 audit",
                1);
            CharacterMoodFactorSnapshot factor = actor.Mood.Factors
                .LastOrDefault(value => string.Equals(
                    value.Id,
                    moodEventId,
                    StringComparison.Ordinal));
            Require(factor != null,
                "Negative mood consumer did not create a mood factor.");
            Require(Mathf.Abs(
                    factor.RemainingSeconds - 60f * moodDuration.Value) < 1.5f,
                "Negative mood duration did not use the performance result.");

            IWorkAmountCalculator work = scope.Container
                .Resolve<IWorkAmountCalculator>();
            Require(work.CalculateWorkPerSecond(
                    actor,
                    null,
                    BuiltInWorkTypeIds.Treat,
                    1f) > 0f,
                "Treatment speed consumer returned no work.");
            Require(work.CalculateWorkPerSecond(
                    actor,
                    null,
                    BuiltInWorkTypeIds.Surgery,
                    1f) > 0f,
                "Surgery speed consumer returned no work.");

            SurgicalProcedureSO procedure = AssetDatabase
                .FindAssets("t:SurgicalProcedureSO")
                .Select(guid => AssetDatabase.LoadAssetAtPath<SurgicalProcedureSO>(
                    AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(value => value != null)
                ?? throw new InvalidOperationException(
                    "No surgical procedure asset exists.");
            SurgeryRiskBreakdown surgery = scope.Container
                .Resolve<ISurgeryRiskEvaluator>()
                .Evaluate(
                    actor,
                    new SurgicalSubjectRef
                    {
                        kind = SurgicalSubjectKind.Character,
                        subjectId = actorId.Value,
                        displayName = actor.Identity?.DisplayName ?? actor.name,
                        speciesId = actor.Identity?.SpeciesTag ?? string.Empty
                    },
                    procedure,
                    new SurgicalFacilitySnapshot(
                        null,
                        SurgeryFacilityTag.None,
                        .8f,
                        1f,
                        0f,
                        .8f,
                        Array.Empty<BuildableObject>(),
                        default),
                    .1f,
                    0f);
            Require(surgery.complicationRiskMultiplier >= 0f,
                "Surgery complication consumer returned an invalid multiplier.");

            scope.Container.Resolve<ICharacterAiWorldSignalQuery>()
                .Capture(actor, CharacterAiBranch.Idle);
            scope.Container.Resolve<IGameEventBus>().Publish(
                new EventAlertLoggedEvent(
                    new EventAlertRecordSnapshot(
                        -27001,
                        "V27 audit",
                        "Performance alarm audit",
                        EventAlertImportance.High,
                        "combat",
                        1,
                        Array.Empty<EventAlertChoice>())));

            actor.Progression.GrowthState.traitIds = new List<int> { 218 };
            CharacterRelationshipMemoryService relationships = scope.Container
                .Resolve<CharacterRelationshipMemoryService>();
            relationships.Remember(
                actor,
                other,
                "betrayal-or-assault",
                1f,
                0);
            Require(relationships.TryForgive(
                    actor,
                    other,
                    "betrayal-or-assault",
                    restitutionProvided: true),
                "Relationship recovery consumer did not clear its audit memory.");

            CombatWeaponSnapshot arcaneWeapon = new(
                "weapon:rune-blade",
                "audit:arcane-weapon",
                CombatEquipmentKind.MeleeWeapon,
                new MeleeStrikeVerb(),
                Array.Empty<CombatRangeProfile>(),
                1,
                CombatEquipmentQuality.Normal,
                string.Empty,
                0,
                0,
                0f,
                false,
                false,
                false);
            CombatResolutionService combat = scope.Container
                .Resolve<ICombatResolutionService>() as CombatResolutionService
                ?? throw new InvalidOperationException(
                    "Combat resolution implementation is unavailable.");
            MethodInfo arcaneMethod = typeof(CombatResolutionService).GetMethod(
                "ResolveArcanePowerMultiplier",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(
                    nameof(CombatResolutionService),
                    "ResolveArcanePowerMultiplier");
            float arcane = (float)arcaneMethod.Invoke(
                combat,
                new object[] { actorId.Value, arcaneWeapon });
            Require(arcane >= 0f,
                "Arcane power consumer returned a negative multiplier.");

            AbilityWork abilityWork = actor.GetComponent<AbilityWork>()
                ?? throw new InvalidOperationException(
                    "Work accident audit actor has no AbilityWork component.");
            MethodInfo ensureWorkModules = typeof(AbilityWork).GetMethod(
                "EnsureWorkModules",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(
                    nameof(AbilityWork),
                    "EnsureWorkModules");
            FieldInfo assignedWorkType = typeof(AbilityWork).GetField(
                "assignedWorkType",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(
                    nameof(AbilityWork),
                    "assignedWorkType");
            FieldInfo taskExecutorField = typeof(AbilityWork).GetField(
                "taskExecutor",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(
                    nameof(AbilityWork),
                    "taskExecutor");
            FacilityWorkType originalWorkType =
                (FacilityWorkType)assignedWorkType.GetValue(abilityWork);
            bool originalWorking = abilityWork.isWorking;
            IAnatomyHealthRuntime anatomy = scope.Container
                .Resolve<IAnatomyHealthRuntime>();
            try
            {
                assignedWorkType.SetValue(abilityWork, FacilityWorkType.Haul);
                ensureWorkModules.Invoke(abilityWork, null);
                WorkTaskExecutor executor = taskExecutorField.GetValue(abilityWork)
                    as WorkTaskExecutor
                    ?? throw new InvalidOperationException(
                        "Work accident executor is unavailable.");
                MethodInfo accidentMethod = typeof(WorkTaskExecutor).GetMethod(
                    "TryTriggerWorkAccident",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new MissingMethodException(
                        nameof(WorkTaskExecutor),
                        "TryTriggerWorkAccident");
                Dictionary<string, float> nodeHealthBefore = anatomy
                    .GetAnatomySnapshot(actor)
                    .Nodes
                    .Where(value => value != null)
                    .ToDictionary(
                        value => value.nodeId,
                        value => value.currentHealth,
                        StringComparer.Ordinal);
                bool triggered = (bool)accidentMethod.Invoke(
                    executor,
                    new object[] { actor, 100_000f });
                Require(triggered,
                    "Forced work accident did not trigger.");
                AnatomyNodeHealthState damaged = anatomy
                    .GetAnatomySnapshot(actor)
                    .Nodes
                    .FirstOrDefault(value => value != null
                        && nodeHealthBefore.TryGetValue(
                            value.nodeId,
                            out float previousHealth)
                        && value.currentHealth < previousHealth);
                Require(damaged != null,
                    "Work accident did not damage an anatomy node.");
                Require(anatomy.TryHealNode(
                        actor,
                        damaged.nodeId,
                        2f,
                        infectionReduction: 0f),
                    "Work accident audit could not restore the damaged node.");
            }
            finally
            {
                assignedWorkType.SetValue(abilityWork, originalWorkType);
                abilityWork.isWorking = originalWorking;
            }

            ICharacterManaCommand mana = scope.Container
                .Resolve<ICharacterManaCommand>();
            Require(mana.TrySpendMana(actor, 5f, out string manaFailure),
                "Mana audit spend failed: " + manaFailure);
            Time.timeScale = 1f;
        }
        finally
        {
            actor.Stats.RemoveMoodFactor(moodEventId);
            actor.Stats.ChangesStat(
                CharacterCondition.HUNGER,
                originalHunger - actor.Stats.GetConditionValue(
                    CharacterCondition.HUNGER,
                    originalHunger));
            actor.Progression.GrowthState.traitIds = originalTraits;
        }
        Debug.Log(
            "V27_CHARACTER_PERFORMANCE_CONSUMER_AUDIT=RUNNING; "
            + "mana recovery evidence will be checked after live ticks.");
    }

    public static void CompleteConsumerExecutionAudit()
    {
        string[] expected =
        {
            CharacterPerformanceFormulaIds.ArcanePower,
            CharacterPerformanceFormulaIds.ManaRecovery,
            CharacterPerformanceFormulaIds.TreatmentSpeed,
            CharacterPerformanceFormulaIds.SurgerySpeed,
            CharacterPerformanceFormulaIds.ComplicationRisk,
            CharacterPerformanceFormulaIds.NutritionEfficiency,
            CharacterPerformanceFormulaIds.AlarmResponse,
            CharacterPerformanceFormulaIds.RiskDetection,
            CharacterPerformanceFormulaIds.NegativeMoodDuration,
            CharacterPerformanceFormulaIds.RelationshipRecovery,
            "performance:work:haul:accident"
        };
        IReadOnlyList<CharacterPerformanceExecutionTraceEntry> trace =
            CharacterPerformanceExecutionTrace.Snapshot();
        string[] missing = expected
            .Where(formulaId => !trace.Any(value =>
                string.Equals(
                    value.FormulaId,
                    formulaId,
                    StringComparison.Ordinal)
                && value.Count > 0))
            .ToArray();
        Require(missing.Length == 0,
            "Missing live consumer execution evidence: "
            + string.Join(", ", missing));
        Require(trace.Where(value => expected.Contains(
                    value.FormulaId,
                    StringComparer.Ordinal))
                .All(value => !float.IsNaN(value.OutputValue)
                    && !float.IsInfinity(value.OutputValue)
                    && value.OutputValue >= 0f),
            "A live consumer trace contains an invalid output.");
        Debug.Log(
            "V27_CHARACTER_PERFORMANCE_CONSUMER_AUDIT=PASS; "
            + $"formulas={expected.Length}; consumers="
            + trace.Count(value => expected.Contains(
                value.FormulaId,
                StringComparer.Ordinal)));
        CleanupConsumerAuditFixtures();
    }

    private static void CleanupConsumerAuditFixtures()
    {
        foreach (GameObject value in consumerAuditObjects)
            if (value != null) UnityEngine.Object.Destroy(value);
        foreach (CharacterSO value in consumerAuditData)
            if (value != null) UnityEngine.Object.Destroy(value);
        consumerAuditObjects.Clear();
        consumerAuditData.Clear();
    }

    [MenuItem("DungeonStory/Debug/V27/Run Character Performance Save Audit")]
    public static void RunSaveRoundTripAudit()
    {
        if (!EditorApplication.isPlaying)
            throw new InvalidOperationException("Save audit requires Play Mode.");
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value?.Container != null)
            ?? throw new InvalidOperationException(
                "Dungeon runtime lifetime scope is not ready.");
        ICharacterLifetimeQuery lifetime = scope.Container
            .Resolve<ICharacterLifetimeQuery>();
        CharacterActor actor = lifetime.AllCharacters.FirstOrDefault(value =>
            value != null
            && value.gameObject.activeInHierarchy
            && !value.IsDead)
            ?? throw new InvalidOperationException(
                "Save audit requires an active character.");
        string characterId = actor.Identity?.PersistentId
            ?? throw new InvalidOperationException(
                "Save audit character has no persistent id.");
        ICharacterPerformanceQuery query = scope.Container
            .Resolve<ICharacterPerformanceQuery>();
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Domain catalog is missing.");
        CharacterPerformanceFormulaDefinitionSO[] formulas = catalog
            .GetAll<CharacterPerformanceFormulaDefinitionSO>()
            .OrderBy(value => value.FormulaId, StringComparer.Ordinal)
            .ToArray();
        string before = BuildDerivedSignature(query, actor, formulas);

        IDungeonGameSaveService saves = scope.Container
            .Resolve<IDungeonGameSaveService>();
        DungeonGameSaveData captured = saves.Capture();
        Require(captured.version == DungeonGameSaveData.CurrentVersion,
            $"Captured save is V{captured.version}, expected V{DungeonGameSaveData.CurrentVersion}.");
        DungeonGameSaveData legacy = saves.FromJson(saves.ToJson(captured));
        legacy.version = DungeonGameSaveData.CurrentVersion - 1;
        Require(!saves.TryRestore(legacy, out DungeonGameRestoreReport legacyReport),
            "A pre-V24 save was accepted.");
        Require(legacyReport.Errors.Any(error => error.Contains(
                DungeonSaveCompatibility.LegacyCharacterStatSchema,
                StringComparison.Ordinal)),
            "Pre-V24 rejection did not report LegacyCharacterStatSchema.");

        DungeonGameSaveData roundTrip = saves.FromJson(
            saves.ToJson(captured, prettyPrint: true));
        Require(saves.TryRestore(roundTrip, out DungeonGameRestoreReport report),
            "V24 restore failed: " + string.Join(" | ", report.Errors));
        CharacterActor restored = lifetime.AllCharacters.FirstOrDefault(value =>
            value != null
            && value.gameObject.activeInHierarchy
            && string.Equals(
                value.Identity?.PersistentId,
                characterId,
                StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Restored character '{characterId}' was not published.");
        string after = BuildDerivedSignature(query, restored, formulas);
        Require(string.Equals(before, after, StringComparison.Ordinal),
            "V24 round trip changed derived capacity, performance, or contribution trace.");
        Debug.Log(
            "V27_CHARACTER_PERFORMANCE_SAVE_AUDIT=PASS; "
            + $"character={characterId}; version=24; capacities=14; "
            + $"formulas={formulas.Length}; legacy={DungeonSaveCompatibility.LegacyCharacterStatSchema}");
    }

    private static string BuildDerivedSignature(
        ICharacterPerformanceQuery query,
        CharacterActor actor,
        IReadOnlyList<CharacterPerformanceFormulaDefinitionSO> formulas)
    {
        CharacterFunctionalCapacitySnapshot capacities = query
            .GetFunctionalCapacities(actor);
        IEnumerable<string> capacityRows = capacities.Values
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .Select(value => string.Join("~",
                value.StableId,
                value.IsApplicable,
                Stable(value.Value),
                value.NonApplicableReason,
                TraceSignature(value.Contributions)));
        CharacterPerformanceEvaluationContext context = new()
        {
            PrimaryProficiencyOverride =
                BuiltInCharacterProficiencyIds.Fieldwork.Value,
            SecondaryProficiencyOverride =
                BuiltInCharacterProficiencyIds.Fieldwork.Value
        };
        IEnumerable<string> performanceRows = formulas.Select(formula =>
        {
            CharacterPerformanceSnapshot snapshot = query.Evaluate(
                actor,
                formula.FormulaId,
                context);
            return string.Join("~",
                snapshot.FormulaId,
                snapshot.IsApplicable,
                Stable(snapshot.Value),
                Stable(snapshot.FunctionalCapacityFactor),
                Stable(snapshot.ProficiencyFactor),
                Stable(snapshot.GameplayEffectFactor),
                Stable(snapshot.ContextFactor),
                snapshot.Failure?.Code ?? string.Empty,
                TraceSignature(snapshot.Contributions));
        });
        return string.Join("\n", capacityRows.Concat(performanceRows));
    }

    private static string TraceSignature(
        IEnumerable<CharacterPerformanceContributionTrace> trace) =>
        string.Join(";", (trace
                ?? Array.Empty<CharacterPerformanceContributionTrace>())
            .Select(value => string.Join("|",
                value.SourceKind,
                value.SourceId,
                value.TargetId,
                Stable(value.AuthoredValue),
                Stable(value.AppliedValue),
                value.Detail)));

    private static string Stable(float value) => value.ToString(
        "R",
        CultureInfo.InvariantCulture);

    private static void VerifyPowerAndImmuneCapacityCausality(
        DungeonRuntimeLifetimeScope scope,
        CharacterActor actor,
        ICharacterPerformanceQuery performance)
    {
        IAnatomyHealthRuntime anatomy = scope.Container
            .Resolve<IAnatomyHealthRuntime>();
        GameDomainContentCatalogSO catalog = AssetDatabase
            .LoadAssetAtPath<GameDomainContentCatalogSO>(CatalogPath)
            ?? throw new InvalidOperationException("Domain catalog is missing.");
        ResourceAnatomyProfileCatalog profiles = new(
            catalog.GetAll<AnatomyProfileSO>());
        AnatomyProfileDefinition profile = profiles.GetForSpecies(
            actor.Identity?.SpeciesTag ?? string.Empty);

        VerifyCapacityBurdenChangesResults(
            actor,
            anatomy,
            performance,
            profile,
            CharacterFunctionalCapacityId.PhysicalPower,
            AnatomyFunction.PhysicalPower,
            CharacterPerformanceFormulaIds.HaulCapacity,
            CharacterPerformanceFormulaIds.MeleePower);
        VerifyCapacityBurdenChangesResults(
            actor,
            anatomy,
            performance,
            profile,
            CharacterFunctionalCapacityId.ImmuneDefense,
            AnatomyFunction.ImmuneDefense,
            CharacterPerformanceFormulaIds.DiseaseResistance,
            CharacterPerformanceFormulaIds.ImmunityGain);
    }

    private static void VerifyCapacityBurdenChangesResults(
        CharacterActor actor,
        IAnatomyHealthRuntime anatomy,
        ICharacterPerformanceQuery performance,
        AnatomyProfileDefinition profile,
        CharacterFunctionalCapacityId capacityId,
        AnatomyFunction function,
        params string[] formulaIds)
    {
        AnatomyNodeDefinition producer = profile.Nodes
            .Where(value => (value.ExpandedFunctions & function) != 0)
            .OrderBy(value => value.Vital)
            .ThenByDescending(value => value.CapacityWeight)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No producer for {CharacterFunctionalCapacityIds.GetStableId(capacityId)}.");
        AnatomyNodeHealthState node = anatomy.GetAnatomySnapshot(actor).Nodes
            .FirstOrDefault(value => value != null
                && string.Equals(value.nodeId, producer.NodeId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Live anatomy node '{producer.NodeId}' is missing.");
        const float burden = 20f;

        float beforeCapacity = performance.GetFunctionalCapacities(actor)
            .Get(capacityId).Value;
        Dictionary<string, float> beforeResults = formulaIds.ToDictionary(
            id => id,
            id => performance.Evaluate(actor, id).Value,
            StringComparer.Ordinal);
        Require(anatomy.TryAddNodeBurden(
                actor,
                producer.NodeId,
                0f,
                0f,
                burden,
                out DomainFailure addFailure),
            $"Could not burden capacity producer '{producer.NodeId}': "
            + addFailure.Code);
        try
        {
            float afterCapacity = performance.GetFunctionalCapacities(actor)
                .Get(capacityId).Value;
            Require(afterCapacity < beforeCapacity,
                $"Burdening '{producer.NodeId}' did not reduce "
                + CharacterFunctionalCapacityIds.GetStableId(capacityId));
            foreach (string formulaId in formulaIds)
            {
                float afterResult = performance.Evaluate(actor, formulaId).Value;
                Require(afterResult < beforeResults[formulaId],
                    $"Burdening {CharacterFunctionalCapacityIds.GetStableId(capacityId)} "
                    + $"did not reduce '{formulaId}'.");
            }
        }
        finally
        {
            Require(anatomy.TryReduceNodeBurden(
                    actor,
                    producer.NodeId,
                    0f,
                    0f,
                    burden,
                    out DomainFailure reduceFailure),
                $"Could not clear capacity burden '{producer.NodeId}': "
                + reduceFailure.Code);
        }
    }

    private static AnatomyFunction ToAnatomyFunction(
        CharacterFunctionalCapacityId capacityId) => capacityId switch
    {
        CharacterFunctionalCapacityId.MentalMaintenance => AnatomyFunction.MentalMaintenance,
        CharacterFunctionalCapacityId.VisualDiscernment => AnatomyFunction.VisualDiscernment,
        CharacterFunctionalCapacityId.AuditorySensing => AnatomyFunction.AuditorySensing,
        CharacterFunctionalCapacityId.RespiratoryExchange => AnatomyFunction.RespiratoryExchange,
        CharacterFunctionalCapacityId.PowerCirculation => AnatomyFunction.PowerCirculation,
        CharacterFunctionalCapacityId.IntakeProcessing => AnatomyFunction.IntakeProcessing,
        CharacterFunctionalCapacityId.PurificationProcessing => AnatomyFunction.PurificationProcessing,
        CharacterFunctionalCapacityId.VitalityResponse => AnatomyFunction.VitalityResponse,
        CharacterFunctionalCapacityId.PhysicalPower => AnatomyFunction.PhysicalPower,
        CharacterFunctionalCapacityId.PrecisionManipulation => AnatomyFunction.PrecisionManipulation,
        CharacterFunctionalCapacityId.PhysicalMobility => AnatomyFunction.PhysicalMobility,
        CharacterFunctionalCapacityId.Communication => AnatomyFunction.Communication,
        CharacterFunctionalCapacityId.ArcaneConduction => AnatomyFunction.ArcaneConduction,
        CharacterFunctionalCapacityId.ImmuneDefense => AnatomyFunction.ImmuneDefense,
        _ => throw new ArgumentOutOfRangeException(nameof(capacityId),capacityId,null)
    };

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
