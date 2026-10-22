#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ScriptableObjectModularityDebugScenarios
{
    public static string RunCatalogExtensionsForBatch()
    {
        if (!Application.isBatchMode || Application.isPlaying)
            throw new InvalidOperationException("Catalog verification requires isolated edit batch execution.");
        VerifyCatalogExtensions();
        return "SO_CATALOG=PASS; typed snapshots; SetDefinitions/OnValidate invalidation; read-only; extensions";
    }

    public static string RunForBatch()
    {
        if (!Application.isBatchMode || Application.isPlaying)
            throw new InvalidOperationException("SO composition verification requires isolated edit batch execution.");
        VerifyCatalogExtensions();
        VerifyClimateDefinitions();
        VerifyCropAndDiseaseExtensions();
        VerifyRecordTokenExtensions();
        VerifyActorComposition();
        return "SO_MODULARITY=PASS; real root clones; additive catalogs; enemy factory/encounter; climate; crop seed/sow/harvest; disease exposure; record tokens; JSON/fresh owners; malformed/duplicate/reference rejection";
    }

    private static void VerifyCatalogExtensions()
    {
        using var fixture = new ContentFixture();
        GameDomainContentCatalogSO cachedDomain = fixture.Own(
            ScriptableObject.CreateInstance<GameDomainContentCatalogSO>());
        BuildingSO[] cacheBuildings = fixture.Baseline.GetAll<BuildingSO>().Take(2).ToArray();
        CharacterSO cacheOtherType = fixture.Baseline.GetAll<CharacterSO>()[0];
        cachedDomain.SetDefinitions(new ScriptableObject[] { cacheBuildings[0] });
        IReadOnlyList<BuildingSO> firstView = cachedDomain.GetAll<BuildingSO>();
        Require(ReferenceEquals(firstView, cachedDomain.GetAll<BuildingSO>()),
            "Repeated domain typed reads did not reuse their read-only view.");
        cachedDomain.SetDefinitions(new ScriptableObject[] { cacheBuildings[0], cacheBuildings[1] });
        IReadOnlyList<BuildingSO> expandedView = cachedDomain.GetAll<BuildingSO>();
        Require(firstView.Count == 1 && firstView[0] == cacheBuildings[0]
            && expandedView.Count == 2
            && expandedView.Contains(cacheBuildings[0])
            && expandedView.Contains(cacheBuildings[1]),
            "SetDefinitions did not invalidate the domain typed view without mutating an existing view.");
        bool externalWriteRejected = false;
        try
        {
            ((IList<BuildingSO>)expandedView).Add(cacheBuildings[0]);
        }
        catch (NotSupportedException)
        {
            externalWriteRejected = true;
        }
        Require(externalWriteRejected,
            "Domain typed views allowed callers to mutate a cached result.");
        var definitionsField = typeof(GameDomainContentCatalogSO).GetField(
            "definitions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var onValidate = typeof(GameDomainContentCatalogSO).GetMethod(
            "OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Require(definitionsField != null && onValidate != null,
            "Domain catalog validation hooks were unavailable to the regression fixture.");
        definitionsField.SetValue(cachedDomain, new List<ScriptableObject>
        {
            cacheBuildings[1], null, cacheOtherType, cacheBuildings[0], cacheBuildings[1]
        });
        onValidate.Invoke(cachedDomain, null);
        IReadOnlyList<BuildingSO> inspectedView = cachedDomain.GetAll<BuildingSO>();
        Require(inspectedView.Count == 2
            && inspectedView[0] == cacheBuildings[1]
            && inspectedView[1] == cacheBuildings[0]
            && expandedView.Count == 2,
            "OnValidate did not refresh the ordered, distinct, null-filtered domain typed view.");
        fixture.AddV20<LifeEventDefinitionSO>();
        fixture.AddV20<GuestRequestDefinitionSO>();
        ServiceIncidentDefinitionSO incident = fixture.AddV20<ServiceIncidentDefinitionSO>(
            value => value.kind == ServiceIncidentKind.Contamination);
        fixture.AddV20<CharacterBackgroundDefinitionSO>();
        fixture.AddV20<CharacterAmbitionDefinitionSO>();
        fixture.AddV20<CulturalPracticeDefinitionSO>();
        fixture.AddV20<ProficiencyDefinitionSO>();
        HeritableTraitDefinitionSO trait = fixture.AddClone<HeritableTraitDefinitionSO>();
        trait.traitId += "-qa-so-extension";
        fixture.AddClone<CharacterTraitSO>();
        FestivalDefinitionSO festival = fixture.AddClone<FestivalDefinitionSO>();
        festival.festivalId += "-qa-so-extension";
        EnemyAbilityDefinitionSO ability = fixture.AddClone<EnemyAbilityDefinitionSO>();
        ability.stableId += "-qa-so-extension";
        BattlefieldModifierDefinitionSO modifier = fixture.AddClone<BattlefieldModifierDefinitionSO>();
        modifier.stableId += "-qa-so-extension";
        EnemyArchetypeDefinitionSO enemy = fixture.AddClone<EnemyArchetypeDefinitionSO>();
        enemy.stableId += "-qa-so-extension";
        enemy.abilityIds = new List<string> { ability.stableId };
        OffenseEncounterSO encounter = fixture.AddClone<OffenseEncounterSO>();
        encounter.encounterId += "-qa-so-extension";
        encounter.enemies[0].enemyArchetypeId = enemy.stableId;
        encounter.battlefieldModifierIds = new List<string> { modifier.stableId };
        IGameContentCatalog content = fixture.Build();
        var stories = new V20StoryContentCatalog(content);
        var narratives = new CharacterNarrativeCatalog(content);
        Require(stories.LifeEvents.Count == fixture.Baseline.GetAll<LifeEventDefinitionSO>().Count + 1
            && stories.ServiceIncidents.Contains(incident)
            && narratives.HeritableTraits.Contains(trait), "Additional authored definitions were not cataloged.");
        Require(new FestivalDefinitionCatalog(content).Require(festival.StableId) == festival,
            "Additional festival was not available through its production catalog.");
        Require(content.GetAll<ResourceItemDefinitionSO>().Count == content.Items.Definitions.OfType<ResourceItemDefinitionSO>().Count()
            && content.GetAll<GenericItemDefinitionSO>().Count == content.Items.Definitions.OfType<GenericItemDefinitionSO>().Count()
            && content.GetAll<ResourceItemDefinitionSO>().Count > 0,
            "Typed item queries lost the authoritative item catalog.");
        Debug.Log(V20EnemyIndividualDebugScenarios.RunSoDefinitionExtensionsForBatch(
            content, enemy.stableId, encounter.encounterId));
        Debug.Log(ObservedServiceIncidentFactDebugScenarios.RunSoIncidentVariantsForBatch(content));
        encounter.enemies[0].enemyArchetypeId = "enemy:qa-missing-reference";
        Reject(() => _ = new EnemyCombatContentCatalog(content));
        encounter.enemies[0].enemyArchetypeId = enemy.stableId;
        ability.stableId = fixture.Baseline.GetAll<EnemyAbilityDefinitionSO>()[0].stableId;
        Reject(() => _ = new EnemyCombatContentCatalog(content));
        incident.ConfigureMetadata(fixture.Baseline.GetAll<ServiceIncidentDefinitionSO>()[0].StableId,
            "Duplicate", "Invalid duplicate", 1, "QA");
        Reject(() => _ = new V20StoryContentCatalog(content));
        Debug.Log("SO_ADDITIVE_CATALOGS=PASS; registered variants; subtype queries; real enemy/incident consumers; duplicate/broken reference reject");
    }

    private static void VerifyClimateDefinitions()
    {
        using var fixture = new ContentFixture();
        ClimateZoneDefinitionSO zone = fixture.AddClone<ClimateZoneDefinitionSO>();
        zone.stableId = "climate:qa-so-extension";
        WeatherFrontDefinitionSO original = fixture.Baseline.GetAll<WeatherFrontDefinitionSO>()[0];
        WeatherFrontDefinitionSO reduced = fixture.Replace(original);
        WeatherFrontDefinitionSO added = fixture.AddClone<WeatherFrontDefinitionSO>(value => value == original);
        added.stableId = "weather:qa-so-extension";
        reduced.springWeight *= .5f; reduced.summerWeight *= .5f;
        reduced.autumnWeight *= .5f; reduced.winterWeight *= .5f;
        added.springWeight = reduced.springWeight; added.summerWeight = reduced.summerWeight;
        added.autumnWeight = reduced.autumnWeight; added.winterWeight = reduced.winterWeight;
        var catalog = new ResourceClimateDefinitionCatalog(fixture.Build());
        ClimateAggregateState state = ClimateAggregateState.Restore(new ClimateWorldSaveData
        {
            absoluteDay = 8, climateZoneId = zone.stableId, weatherFrontId = added.stableId,
            frontRemainingDays = 2, dailyNoiseC = 0f
        }, catalog);
        Require(float.IsFinite(state.GetOutdoorTemperature(catalog)), "Additional climate was not consumed.");
        string saved = JsonUtility.ToJson(state.Capture());
        ClimateAggregateState restored = ClimateAggregateState.Restore(
            JsonUtility.FromJson<ClimateWorldSaveData>(saved), catalog);
        Require(JsonUtility.ToJson(restored.Capture()) == saved, "Additional climate ID did not restore exactly.");
        restored.AdvanceToDay(10, catalog, () => .5d);
        Require(restored.AbsoluteDay == 10, "Restored climate did not advance.");
        added.springWeight += 1f;
        Reject(() => _ = new ResourceClimateDefinitionCatalog(fixture.Build()));
        Debug.Log("SO_CLIMATE_DEFINITIONS=PASS; 6 zones/7 fronts; normalized weights; temperature; JSON/advance; invalid total reject");
    }

    private static void VerifyCropAndDiseaseExtensions()
    {
        using var fixture = new ContentFixture();
        CropDefinitionSO crop = fixture.AddClone<CropDefinitionSO>();
        SetString(crop, "cropId", "crop:qa-so-extension");
        CropGenomeDefinitionSO genome = fixture.AddClone<CropGenomeDefinitionSO>();
        genome.ConfigureCultivar("genome:qa-so-extension:base", crop.CropId, "QA cultivar", "QA extension",
            Array.Empty<string>(), genome.CreateRuntimeDefinition().loci);
        crop.ConfigureEcology(crop.SeedItemId, genome, crop.FamilyGroup, crop.EndemicDisease);
        DiseaseDefinitionSO disease = fixture.AddClone<DiseaseDefinitionSO>(value => value.CreateRuntimeDefinition().Contagious);
        disease.stableId = "disease:qa-so-extension";
        disease.baseInfectionProbability = 1f;
        disease.incubationDays = 0;
        disease.contagiousDays = 10;
        disease.chronic = false;
        IGameContentCatalog content = fixture.Build();
        CropEcologyRuntime CreateCrop() => new(new DungeonRuntimeAggregateRootStore(), content,
            new RandomStreamProvider(19837), CharacterAiEditorTestDependencies.PhysicalStock);
        CropEcologyRuntime crops = CreateCrop();
        Require(crops.TryClaim(out IReadOnlyList<SeedLotState> grant) && grant.Count == 13
            && !crops.TryClaim(out _), "Extended crop seed grant was not exactly once.");
        SeedLotState seed = grant.Single(value => value.cropId == crop.CropId);
        crops.Sow("plot:qa-so-extension", crop.FamilyGroup, seed);
        string cropJson = JsonUtility.ToJson(crops.Capture());
        CropEcologyRuntime restoredCrops = CreateCrop();
        restoredCrops.PublishRestore(restoredCrops.PrepareRestore(JsonUtility.FromJson<CropEcologyWorldSaveData>(cropJson)));
        Require(!restoredCrops.TryClaim(out _) && restoredCrops.Plots.Single().cultivarGenomeId == genome.GenomeId,
            "Crop restore lost the new cultivar or replayed its seed grant.");
        restoredCrops.PrepareHarvest("crop-harvest:qa-so-extension", "plot:qa-so-extension");
        string pending = JsonUtility.ToJson(restoredCrops.Capture());
        CropEcologyRuntime retry = CreateCrop();
        retry.PublishRestore(retry.PrepareRestore(JsonUtility.FromJson<CropEcologyWorldSaveData>(pending)));
        retry.CommitPreparedHarvest("crop-harvest:qa-so-extension");
        string committed = JsonUtility.ToJson(retry.Capture());
        retry.CommitPreparedHarvest("crop-harvest:qa-so-extension");
        Require(JsonUtility.ToJson(retry.Capture()) == committed
            && retry.AcknowledgePreparedHarvest("crop-harvest:qa-so-extension"), "Extended crop harvest replay changed state.");
        var diseases = new ResourceDiseaseDefinitionCatalog(content);
        PopulationHealthRuntime CreateHealth() => new(new DungeonRuntimeAggregateRootStore(), diseases,
            new NeutralDiseaseModifiers(), CharacterAiEditorTestDependencies.GameCalendar, new RandomStreamProvider(19837));
        var character = CharacterId.FromStableSuffix("so-extension-disease");
        PopulationHealthRuntime health = CreateHealth();
        health.RecordExposure(disease.stableId, new[] { new PopulationExposureTarget(character, 3f) }, 24f, 3f);
        health.AdvanceToDay(CharacterAiEditorTestDependencies.GameCalendar.Day + 1);
        Require(health.TryGetCharacterSnapshot(character, out var sick)
            && sick.ActiveDiseases.Any(value => value.DiseaseId == disease.stableId), "Additional disease never infected its target.");
        string healthJson = JsonUtility.ToJson(health.Capture());
        PopulationHealthRuntime restoredHealth = CreateHealth();
        restoredHealth.PublishRestore(restoredHealth.PrepareRestore(JsonUtility.FromJson<PopulationHealthWorldSaveData>(healthJson)));
        Require(JsonUtility.ToJson(restoredHealth.Capture()) == healthJson, "Additional disease did not restore exactly.");
        disease.stableId = string.Empty;
        Reject(() => _ = new ResourceDiseaseDefinitionCatalog(content));
        Debug.Log("SO_CROP_DISEASE=PASS; 13th crop seed grant/sow; fresh pending harvest/commit/replay; 17th disease exposure/infection/JSON; invalid definition reject");
    }

    private static void VerifyRecordTokenExtensions()
    {
        using var fixture = new ContentFixture();
        FacilityEvolutionRecordTokenDefinitionSO source = fixture.Baseline.GetAll<FacilityEvolutionRecordTokenDefinitionSO>()
            .Single(value => value.generationTrigger == FacilityEvolutionRecordTokenGenerationTrigger.MealFoodConsumed);
        var extra = fixture.AddClone<FacilityEvolutionRecordTokenDefinitionSO>(value => value == source);
        extra.tokenId = "qa:food-history";
        var threshold = fixture.AddClone<FacilityEvolutionRecordTokenDefinitionSO>(value => value == source);
        threshold.tokenId = "qa:visit-threshold";
        threshold.generationTrigger = FacilityEvolutionRecordTokenGenerationTrigger.MetricThresholdCrossing;
        threshold.sourceMetric = FacilityEvolutionTerms.VisitCount;
        threshold.threshold = 2f;
        threshold.decayPolicy = "long-term";
        var provider = new TokenDefinitions(fixture.Build().GetAll<FacilityEvolutionRecordTokenDefinitionSO>());
        var records = new FacilityEvolutionRecordComponentService(new FacilityEvolutionRecordComponentFactory());
        var cache = new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, null);
        FacilityEvolutionRecordEventRecorder CreateRecorder() => new(cache, records, null, provider);
        BuildingSO meal = fixture.Baseline.GetAll<BuildingSO>().First(value => value.Facility != null
            && value.Facility.SupportsRole(FacilityRole.Meal));
        GameObject firstObject = new("SO record source"), restoredObject = new("SO record restored");
        try
        {
            BuildableObject CreateFacility(GameObject root)
            {
                var facility = root.AddComponent<BuildableObject>();
                facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
                CharacterAiEditorTestDependencies.Inject(facility);
                facility.Initialization(meal, Vector2Int.zero);
                return facility;
            }
            BuildableObject first = CreateFacility(firstObject);
            FacilityEvolutionRecordEventRecorder recorder = CreateRecorder();
            recorder.RecordVisit(new FacilityVisitEvent(null, first), 5);
            recorder.RecordVisit(new FacilityVisitEvent(null, first), 5);
            recorder.RecordStockConsumed(new FacilityStockConsumedEvent(null, first, StockCategory.Food, 3));
            FacilityEvolutionRecord record = records.GetRecord(first);
            Require(record.GetToken(source.EffectiveId) == 3 && record.GetToken(extra.EffectiveId) == 3
                && record.GetToken(threshold.EffectiveId) == 1, "Authored token generation did not reach record authority.");
            var saved = new ModularFacilityBuildingSaveData { stateModules = BuildingStateModulePersistence.Capture(first) };
            BuildableObject restored = CreateFacility(restoredObject);
            records.GetOrAdd(restored).GetRecord(restored);
            var modules = JsonUtility.FromJson<ModularFacilityBuildingSaveData>(JsonUtility.ToJson(saved));
            var result = BuildingStateModulePersistence.Restore(restored, modules.stateModules);
            Require(result.Success, string.Join(" | ", result.errors));
            Require(records.GetRecord(restored).GetToken(extra.EffectiveId) == 3,
                "Building state JSON did not restore the additional record token.");
            FacilityEvolutionRecordEventRecorder fresh = CreateRecorder();
            fresh.RecordVisit(new FacilityVisitEvent(null, restored), 5);
            fresh.RecordStockConsumed(new FacilityStockConsumedEvent(null, restored, StockCategory.Food, 2));
            FacilityEvolutionRecord after = records.GetRecord(restored);
            Require(after.GetToken(extra.EffectiveId) == 5 && after.GetToken(threshold.EffectiveId) == 1,
                "Restored tokens duplicated a crossed threshold or lost new token output.");
            threshold.sourceMetric = "qa:unknown-metric";
            Reject(() => _ = CreateRecorder());
            threshold.sourceMetric = FacilityEvolutionTerms.VisitCount;
            threshold.decayPolicy = "qa:unsupported-decay";
            Reject(() => _ = CreateRecorder());
        }
        finally { Object.DestroyImmediate(firstObject); Object.DestroyImmediate(restoredObject); }
        Debug.Log("SO_RECORD_TOKENS=PASS; existing event amounts; additional token/metric crossing; fresh owner; unsupported metric/decay reject");
    }

    private sealed class NeutralDiseaseModifiers : IPopulationDiseaseModifierQuery
    {
        public PopulationDiseaseStatModifiers Resolve(CharacterId id, DiseaseDefinition definition) => PopulationDiseaseStatModifiers.Neutral;
    }

    private static void VerifyActorComposition()
    {
        using var fixture = new ContentFixture();
        CharacterSO template = fixture.Baseline.GetAll<CharacterSO>().First(value => value.species != null
            && value.role != CharacterRole.Owner);
        CharacterSpeciesSO speciesSource = fixture.Baseline.GetAll<CharacterSpeciesSO>()
            .First(value => value.speciesTag != template.SpeciesTag);
        CharacterSpeciesSO species = fixture.Replace(speciesSource);
        var anatomy = fixture.AddClone<AnatomyProfileSO>();
        anatomy.Configure("anatomy:qa-so-extension", "QA authored anatomy", "construct",
            Array.Empty<string>(), new[]
            {
                new AnatomyNodeDefinition("qa:core", "Core", "", AnatomyNodeKind.Core,
                    AnatomyFunction.Consciousness | AnatomyFunction.Core | AnatomyFunction.Breathing
                    | AnatomyFunction.Digestion | AnatomyFunction.Filtration | AnatomyFunction.Manipulation
                    | AnatomyFunction.Mobility | AnatomyFunction.Sight, 100f, 1f, true, false, legacyBodyPart: CombatBodyPart.Head, mapsToLegacyBodyPart: true),
                new AnatomyNodeDefinition("qa:apparel-surface", "Surface", "qa:core", AnatomyNodeKind.BodyPart,
                    AnatomyFunction.Manipulation, 30f, .1f, false, true, legacyBodyPart: CombatBodyPart.Torso, mapsToLegacyBodyPart: true)
            });
        Reject(() => _ = new AnatomyProfileDefinition("anatomy:qa-ambiguous", "QA invalid", "QA",
            Array.Empty<string>(), new[]
            {
                new AnatomyNodeDefinition("qa:a", "A", "", AnatomyNodeKind.BodyPart,
                    AnatomyFunction.None, 20f, 1f, false, true, legacyBodyPart: CombatBodyPart.Head, mapsToLegacyBodyPart: true),
                new AnatomyNodeDefinition("qa:b", "B", "", AnatomyNodeKind.BodyPart,
                    AnatomyFunction.None, 20f, 1f, false, true, legacyBodyPart: CombatBodyPart.Head, mapsToLegacyBodyPart: true)
            }));
        anatomy.ConfigureApparelFit(ApparelBodyForm.Construct, ApparelSizeClass.Large,
            AnatomyAttachmentPoint.None, new[] { new AnatomyApparelAttachment
            {
                nodeId = "qa:apparel-surface", points = AnatomyAttachmentPoint.Torso,
                mode = AnatomyApparelAttachmentMode.AddWhenPresent
            } });
        species.anatomyProfileId = anatomy.ProfileId;
        var firstAbility = new CharacterCombatAbilityDefinition("qa:species-first", "First", "QA", 0,
            OffenseBattleTargetRule.Enemy, new OffenseDamageEffect(.5f));
        var secondAbility = new CharacterCombatAbilityDefinition("qa:species-second", "Second", "QA", 0,
            OffenseBattleTargetRule.Enemy, new OffenseDamageEffect(.7f));
        species.combatAbilities.SetAbilities(new[] { firstAbility, secondAbility });
        CharacterTraitSO trait = fixture.AddClone<CharacterTraitSO>();
        trait.eligibleSpeciesTags = new List<string> { species.speciesTag };
        var traitAbility = new CharacterCombatAbilityDefinition("qa:trait-composition", "Trait", "QA", 0,
            OffenseBattleTargetRule.Enemy, new OffenseDamageEffect(.8f));
        trait.combatAbilities.SetAbilities(new[] { traitAbility });
        ApparelDefinitionSO garment = fixture.AddClone<ApparelDefinitionSO>();
        var garmentFields = new SerializedObject(garment);
        garmentFields.FindProperty("bodyForm").enumValueIndex = (int)ApparelBodyForm.Construct;
        garmentFields.FindProperty("fitMode").enumValueIndex = (int)ApparelFitMode.Sized;
        garmentFields.FindProperty("requiredPoints").longValue = (long)AnatomyAttachmentPoint.Torso;
        garmentFields.FindProperty("sealedOptionalPoints").longValue = 0;
        garmentFields.ApplyModifiedPropertiesWithoutUndo();
        IGameContentCatalog content = fixture.Build();
        var factory = new CharacterRuntimeProfileFactory(content);
        var profiles = new ResourceAnatomyProfileCatalog(content);
        var events = new GameEventBus();
        var clock = new UnityGameClock();
        CharacterBodyHealthRuntime CreateBody() => new(new UnityRealtimeClock(),
            CharacterAiEditorTestDependencies.WorldRegistry,
            clock, events, new DynamicFrameWorkBudget(clock, new UnityUiClock()), profiles,
            new DungeonRuntimeAggregateRootStore(), CharacterAiEditorTestDependencies.GameCalendar);
        CharacterBodyHealthRuntime body = CreateBody();
        using var activities = new CharacterVitalsActivityObserver(events, CharacterAiEditorTestDependencies.WorldRegistry);
        activities.Initialize();
        GameObject actorObject = new("SO phenotype/combat/apparel fixture");
        try
        {
            var actor = actorObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.Identity.SetPersistentId(CharacterId.FromStableSuffix("so-modularity-actor"));
            actor.Stats.ConstructCharacterVitals(new CharacterStatsVitalsService(body, body, events,
                new CharacterDeathEventFactory(CharacterAiEditorTestDependencies.WorldRegistry,
                    CharacterAiEditorTestDependencies.GameCalendar), new NoOwnerLifecycle()));
            body.GetAnatomySnapshot(actor);
            Require(body.Capture().characters.Count == 0,
                "Pre-profile injection/query created a provisional anatomy authority.");
            actor.Identity.SetData(template, factory.Create(new CharacterSpawnRequest(template.DefinitionId,
                species.DefinitionId, template.VisualVariantId, ReproductiveRole.None, new[] { trait.DefinitionId })));
            actor.Progression.ConfigurePreview(new EditorCharacterSkillGenerationService(),
                new ResourceCharacterSkillSystemSettingsProvider(content),
                new CharacterProgressionProfileProjector(content, factory));
            actor.Progression.Bind(actor);
            body.ConfigureVitals(actor, 100f, true);
            Require(actor.profile.PhenotypeSpeciesId.Equals(species.DefinitionId)
                && actor.profile.GetAnatomyProfileId() == anatomy.ProfileId
                && actor.Progression.ResolveSelectedTraits().Single().DefinitionId.Equals(trait.DefinitionId),
                "Progression replaced the chosen phenotype or expressed trait with archetype defaults.");
            Require(CharacterCombatAbilityCatalog.GetSpeciesAbilities(actor).Count == 2,
                "A second authored species ability was truncated.");
            CharacterProgressionSnapshot beforeLevel = actor.Progression.CapturePersistentState();
            actor.Progression.RestorePersistentState(new CharacterProgressionSnapshot(3, 0,
                beforeLevel.GrowthState, beforeLevel.NarrativeLedger, beforeLevel.AcquiredTraitState));
            OffenseBattleCombatant ally = OffenseEncounterCatalog.CreateAlly(actor,
                actor.Identity.PersistentId, CharacterAiEditorTestDependencies.NeutralPerformance);
            Require(ally.Abilities.Any(value => value.Id == firstAbility.Id)
                && ally.Abilities.Any(value => value.Id == secondAbility.Id)
                && ally.Abilities.Any(value => value.Id == traitAbility.Id),
                "Species/trait composition did not reach the production battle projection.");
            var world = new ActorWorld(actor);
            var attachments = new AnatomyAttachmentQuery(world, body, profiles);
            Require(body.GetAnatomySnapshot(actor).ProfileId == anatomy.ProfileId
                && attachments.GetBodyForm(actor.Identity.TypedPersistentId) == ApparelBodyForm.Construct
                && attachments.GetSize(actor.Identity.TypedPersistentId) == ApparelSizeClass.Large
                && attachments.GetAvailablePoints(actor.Identity.TypedPersistentId) == AnatomyAttachmentPoint.Torso,
                "The explicit profile was replaced by species-name or humanoid attachment defaults.");
            body.ApplyLocalizedDamage(actor, CombatBodyPart.Torso, 5f, "QA authored surface", allowDeath: false);
            Require(Mathf.Approximately(body.GetAnatomySnapshot(actor).Nodes
                .Single(value => value.nodeId == "qa:apparel-surface").currentHealth, 25f),
                "Authored legacy surface mapping did not receive localized damage.");
            var apparel = new ApparelInstanceState { size = ApparelSizeClass.Large };
            Require(attachments.CanEquip(actor.Identity.TypedPersistentId, garment, apparel, out _, out _),
                "An existing garment mechanic could not use the authored new anatomy surface.");
            Require(body.TryRemoveNode(actor, "qa:apparel-surface", out _, out DomainFailure failure), failure.Code.ToString());
            Require(!attachments.CanEquip(actor.Identity.TypedPersistentId, garment, apparel, out _, out _),
                "A removed authored attachment still admitted the garment.");
            string bodyJson = JsonUtility.ToJson(body.Capture());
            CharacterBodyHealthRuntime restored = CreateBody();
            restored.PublishRestore(restored.PrepareRestore(JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(bodyJson)));
            var restoredFit = new AnatomyAttachmentQuery(world, restored, profiles);
            Require(restored.GetAnatomySnapshot(actor).ProfileId == anatomy.ProfileId
                && restoredFit.GetAvailablePoints(actor.Identity.TypedPersistentId) == AnatomyAttachmentPoint.None,
                "Fresh body restore lost the custom anatomy or missing attachment state.");
            var invalid = JsonUtility.FromJson<DungeonCharacterBodyHealthSaveData>(bodyJson);
            invalid.characters[0].anatomyProfileId = "anatomy:missing-qa";
            Reject(() => restored.PrepareRestore(invalid));
            Require(JsonUtility.ToJson(restored.Capture()) == bodyJson, "Rejected anatomy restore changed live state.");
        }
        finally { Object.DestroyImmediate(actorObject); }
        Debug.Log("SO_ACTOR_COMPOSITION=PASS; phenotype/expressed traits preserved; two species+trait abilities in real battle projection; authored damage surface/fit/node removal; JSON/fresh body; bad profile atomic reject");
    }

    private sealed class ActorWorld : ICharacterWorldQuery
    {
        internal ActorWorld(CharacterActor actor) { Characters = new[] { actor }; }
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
    }
    private sealed class NoOwnerLifecycle : IOwnerRunLifecycleService
    {
        public void HandleOwnerDeath(CharacterActor actor, string reason) { }
    }

    private sealed class TokenDefinitions : IFacilityEvolutionRecordTokenDefinitionProvider
    {
        private readonly IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> values;
        internal TokenDefinitions(IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> values) { this.values = values; }
        public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions() => values;
        public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string id) => values.SingleOrDefault(value => value.EffectiveId == id);
    }

    private sealed class ContentFixture : IDisposable
    {
        internal readonly ResourceGameContentCatalog Baseline = new(new UnityGameContentRootLoader());
        private readonly List<ScriptableObject> extras = new();
        private readonly Dictionary<ScriptableObject, ScriptableObject> replacements = new();
        private readonly List<ScriptableObject> owned = new();
        private readonly GameContentCatalogSO root;
        private readonly GameDomainContentCatalogSO domain;
        internal ContentFixture()
        {
            root = Own(Object.Instantiate(Baseline.Root));
            domain = Own(Object.Instantiate(Baseline.Domain));
        }
        internal T Own<T>(T value) where T : ScriptableObject { owned.Add(value); return value; }
        internal T AddClone<T>(Func<T, bool> predicate = null) where T : ScriptableObject
        {
            T clone = Own(Object.Instantiate(Baseline.GetAll<T>().First(value => predicate == null || predicate(value))));
            if (clone is DataScriptableObject data) data.id = 1_800_000_000 + owned.Count;
            extras.Add(clone);
            return clone;
        }
        internal T AddV20<T>(Func<T, bool> predicate = null) where T : V20AuthoredContentSO
        {
            T clone = AddClone(predicate);
            clone.ConfigureMetadata(clone.StableId + "-qa-so-extension", clone.DisplayName,
                clone.Description, clone.AuthoringRevision, "SO extension verification");
            return clone;
        }
        internal T Replace<T>(T original) where T : ScriptableObject
        {
            T clone = Own(Object.Instantiate(original));
            replacements.Add(original, clone);
            return clone;
        }
        internal ResourceGameContentCatalog Build()
        {
            domain.SetDefinitions(Baseline.Domain.Definitions.Select(value =>
                replacements.TryGetValue(value, out ScriptableObject replacement) ? replacement : value).Concat(extras));
            root.Configure(Baseline.Items, Baseline.WorldPresentation, Baseline.CharacterSkillSettings,
                Baseline.Media, new[] { domain });
            return new ResourceGameContentCatalog(new RootLoader(root));
        }
        public void Dispose() { for (int index = owned.Count - 1; index >= 0; index--) Object.DestroyImmediate(owned[index]); }
    }

    private sealed class RootLoader : IGameContentRootLoader
    {
        private readonly ScriptableObject root;
        internal RootLoader(ScriptableObject root) { this.root = root; }
        public ScriptableObject LoadRequiredRoot() => root;
    }
    private static void SetString(Object target, string field, string value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException or ArgumentException) { return; }
        throw new InvalidOperationException("Expected invalid SO composition to be rejected.");
    }
}
#endif
