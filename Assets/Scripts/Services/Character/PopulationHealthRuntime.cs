using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class ResourceDiseaseDefinitionCatalog : IDiseaseDefinitionCatalog
{
    private readonly Dictionary<string, DiseaseDefinition> byId;

    public ResourceDiseaseDefinitionCatalog(IGameContentCatalog content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        DiseaseDefinitionSO[] assets = content.GetAll<DiseaseDefinitionSO>().ToArray();
        if (assets.Length != 16)
            throw new InvalidOperationException($"V20 requires exactly 16 disease definitions, found {assets.Length}.");
        IReadOnlyList<string> authoredErrors = assets
            .SelectMany(value => value.ValidateDefinition())
            .ToArray();
        if (authoredErrors.Count > 0)
            throw new InvalidOperationException(
                "V20 disease content is incomplete: "
                + string.Join(" | ", authoredErrors));
        byId = assets.Select(value => value.CreateRuntimeDefinition())
            .ToDictionary(value => value.Id, StringComparer.Ordinal);
        if (byId.Values.Any(value => !value.IsValid))
            throw new InvalidOperationException("V20 disease content contains an invalid definition.");
        Definitions = byId.Values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<DiseaseDefinition> Definitions { get; }
    public DiseaseDefinition Require(string diseaseId) =>
        byId.TryGetValue(diseaseId?.Trim() ?? string.Empty, out DiseaseDefinition value)
            ? value
            : throw new KeyNotFoundException($"Unknown disease '{diseaseId}'.");
}

public sealed class PopulationHealthRuntime :
    IPopulationHealthService,
    IPopulationHealthQuery,
    IDiseaseSymptomEffectQuery,
    IPopulationHealthPersistence
{
    private const string InfectionRandomStreamId = "population:infection";
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly IDiseaseDefinitionCatalog definitions;
    private readonly IGameCalendar calendar;
    private readonly IRandomStream random;
    private int version = 1;

    public PopulationHealthRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        IDiseaseDefinitionCatalog definitions,
        IGameCalendar calendar,
        IRandomStreamProvider randomStreams)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        random = (randomStreams ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get(InfectionRandomStreamId);
    }

    public int Version => version;

    public void RecordExposure(
        string diseaseId,
        IReadOnlyList<PopulationExposureTarget> targets,
        float exposureHours,
        float environmentCoefficient)
    {
        Writable.RecordExposure(
            diseaseId,
            targets,
            exposureHours,
            environmentCoefficient,
            definitions);
        version = unchecked(version + 1);
    }

    public IReadOnlyList<PopulationHealthChange> AdvanceToDay(int absoluteDay)
    {
        IReadOnlyList<PopulationHealthChange> changes = Writable.AdvanceToDay(
            absoluteDay,
            definitions,
            () => random.NextFloat());
        version = unchecked(version + 1);
        return changes;
    }

    public void Vaccinate(CharacterId characterId, string diseaseId)
    {
        Writable.Vaccinate(characterId, diseaseId, definitions);
        version = unchecked(version + 1);
    }

    public PopulationHealthChange ApplyEnvironmentalCondition(
        CharacterId characterId,
        string diseaseId)
    {
        PopulationHealthChange change = Writable.ApplyEnvironmentalCondition(
            characterId,
            diseaseId,
            definitions);
        version = unchecked(version + 1);
        return change;
    }

    public void RemoveEnvironmentalCondition(CharacterId characterId, string diseaseId)
    {
        Writable.RemoveEnvironmentalCondition(characterId, diseaseId, definitions);
        version = unchecked(version + 1);
    }

    public float GetImmunity(CharacterId characterId, string diseaseId) =>
        Current.GetImmunity(characterId, diseaseId);
    public bool IsEpidemicDeclared(string diseaseId) =>
        Current.IsEpidemicDeclared(diseaseId);
    public IReadOnlyList<ContagiousDiseaseSnapshot> GetContagious() =>
        Current.GetContagious(definitions);
    public bool TryGetCharacterSnapshot(
        CharacterId characterId,
        out PopulationCharacterHealthSnapshot snapshot) =>
        Current.TryGetCharacterSnapshot(characterId, out snapshot);
    public IReadOnlyList<EpidemicSnapshot> GetEpidemics(bool declaredOnly) =>
        Current.GetEpidemics(declaredOnly);
    public IReadOnlyList<DiseaseSymptomEffectSnapshot> GetActiveSymptoms(
        CharacterId characterId)
    {
        if (!Current.TryGetCharacterSnapshot(characterId, out PopulationCharacterHealthSnapshot state))
            return Array.Empty<DiseaseSymptomEffectSnapshot>();
        return state.ActiveDiseases
            .Where(value => Current.CurrentAbsoluteDay >= value.SymptomDay
                && Current.CurrentAbsoluteDay < value.RecoveryDay)
            .Select(CreateSymptomEffect)
            .OrderBy(value => value.DiseaseId, StringComparer.Ordinal)
            .ToArray();
    }
    public float GetWorkSpeedMultiplier(CharacterId characterId) =>
        GetActiveSymptoms(characterId).Aggregate(
            1f,
            (current, value) => Math.Max(0.2f, current * value.WorkSpeedMultiplier));
    public float GetMoveSpeedMultiplier(CharacterId characterId) =>
        GetActiveSymptoms(characterId).Aggregate(
            1f,
            (current, value) => Math.Max(0.2f, current * value.MoveSpeedMultiplier));
    public PopulationHealthWorldSaveData Capture() => Current.Capture();
    public PopulationHealthAggregateState PrepareRestore(PopulationHealthWorldSaveData data) =>
        PopulationHealthAggregateState.Restore(data, definitions);
    public void PublishRestore(PopulationHealthAggregateState candidate)
    {
        rootStore.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate)));
        version = unchecked(version + 1);
    }

    private PopulationHealthAggregateState Current => rootStore.GetOrCreate(CreateEmpty);
    private PopulationHealthAggregateState Writable => rootStore.GetOrCreateWritable(
        CreateEmpty,
        value => PopulationHealthAggregateState.Restore(value.Capture(), definitions));
    private PopulationHealthAggregateState CreateEmpty() =>
        PopulationHealthAggregateState.Restore(
            new PopulationHealthWorldSaveData { currentAbsoluteDay = calendar.Day },
            definitions);

    private DiseaseSymptomEffectSnapshot CreateSymptomEffect(
        ActiveDiseaseSnapshot active)
    {
        DiseaseDefinition disease = definitions.Require(active.DiseaseId);
        float normalized = Math.Clamp(active.Severity / 100f, 0f, 1f);
        float workBurden = disease.TargetSystem switch
        {
            DiseaseTargetSystem.Consciousness => 0.55f,
            DiseaseTargetSystem.Breathing => 0.5f,
            DiseaseTargetSystem.Digestion => 0.38f,
            DiseaseTargetSystem.Filtration => 0.42f,
            _ => 0.45f
        };
        float moveBurden = disease.TargetSystem switch
        {
            DiseaseTargetSystem.Breathing => 0.48f,
            DiseaseTargetSystem.Consciousness => 0.35f,
            DiseaseTargetSystem.Core => 0.4f,
            _ => 0.25f
        };
        return new DiseaseSymptomEffectSnapshot(
            disease.Id,
            disease.SymptomProfileId,
            disease.TargetSystem,
            active.Severity,
            1f - normalized * workBurden,
            1f - normalized * moveBurden,
            -Math.Max(1f, normalized * 10f));
    }
}

public sealed class PopulationHealthApplicationAdapter : IStartable, IDisposable
{
    private readonly IPopulationHealthService health;
    private readonly IDiseaseDefinitionCatalog definitions;
    private readonly ICharacterWorldQuery world;
    private readonly ICharacterAiWorldRegistry gridWorld;
    private readonly IRoomLayoutCache rooms;
    private readonly IEnvironmentalFieldQuery environment;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly IAnatomyHealthRuntime anatomyHealth;
    private readonly IGameEventBus events;
    private readonly IHeritableTraitEffectQuery heritableTraits;
    private readonly IDiseaseSymptomEffectQuery symptoms;
    private IDisposable dayEndedSubscription;
    private IDisposable mealConsumedSubscription;
    private IDisposable waterConsumedSubscription;
    private IDisposable routeExposureSubscription;
    private IDisposable medicalBloodContactSubscription;

    public PopulationHealthApplicationAdapter(
        IPopulationHealthService health,
        IDiseaseDefinitionCatalog definitions,
        ICharacterWorldQuery world,
        ICharacterAiWorldRegistry gridWorld,
        IRoomLayoutCache rooms,
        IEnvironmentalFieldQuery environment,
        IAnatomyProfileCatalog anatomyProfiles,
        IAnatomyHealthRuntime anatomyHealth,
        IGameEventBus events,
        IHeritableTraitEffectQuery heritableTraits,
        IDiseaseSymptomEffectQuery symptoms)
    {
        this.health = health ?? throw new ArgumentNullException(nameof(health));
        this.definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.gridWorld = gridWorld ?? throw new ArgumentNullException(nameof(gridWorld));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.anatomyProfiles = anatomyProfiles ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        this.anatomyHealth = anatomyHealth ?? throw new ArgumentNullException(nameof(anatomyHealth));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.heritableTraits = heritableTraits
            ?? throw new ArgumentNullException(nameof(heritableTraits));
        this.symptoms = symptoms ?? throw new ArgumentNullException(nameof(symptoms));
    }

    public void Start()
    {
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
        mealConsumedSubscription ??= events.Subscribe<PhysicalMealConsumedEvent>(OnMealConsumed);
        waterConsumedSubscription ??= events.Subscribe<CharacterWaterConsumedEvent>(OnWaterConsumed);
        routeExposureSubscription ??= events.Subscribe<PopulationDiseaseRouteExposureEvent>(
            OnRouteExposure);
        medicalBloodContactSubscription ??= events.Subscribe<CharacterMedicalBloodContactEvent>(
            OnMedicalBloodContact);
    }
    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        mealConsumedSubscription?.Dispose();
        waterConsumedSubscription?.Dispose();
        routeExposureSubscription?.Dispose();
        medicalBloodContactSubscription?.Dispose();
        dayEndedSubscription = null;
        mealConsumedSubscription = null;
        waterConsumedSubscription = null;
        routeExposureSubscription = null;
        medicalBloodContactSubscription = null;
    }

    private void OnMealConsumed(PhysicalMealConsumedEvent consumed)
    {
        if (!consumed.Result.Success
            || !consumed.Result.Contaminated
            || consumed.Actor == null
            || consumed.Actor.IsDead
            || !CharacterPersistentIdentity.TryGet(consumed.Actor, out CharacterId characterId))
        {
            return;
        }

        RecordPhysicalExposure(
            characterId,
            "disease:gut-rot",
            DiseaseTransmissionRoute.Food,
            24f,
            1f);
    }

    private void OnWaterConsumed(CharacterWaterConsumedEvent consumed)
    {
        if (!consumed.CharacterId.IsValid || consumed.Amount <= 0f)
            return;

        HashSet<string> recorded = new(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(consumed.PathogenDiseaseId))
        {
            RecordPhysicalExposure(
                consumed.CharacterId,
                consumed.PathogenDiseaseId,
                DiseaseTransmissionRoute.Water,
                24f,
                1f);
            recorded.Add(consumed.PathogenDiseaseId);
        }

        if (consumed.Quality != WorldWaterQuality.Clean
            && recorded.Add("disease:gut-rot"))
        {
            RecordPhysicalExposure(
                consumed.CharacterId,
                "disease:gut-rot",
                DiseaseTransmissionRoute.Water,
                24f,
                consumed.Quality == WorldWaterQuality.Foul ? 1f : 0.5f);
        }
    }

    private void OnRouteExposure(PopulationDiseaseRouteExposureEvent exposure)
    {
        RecordPhysicalExposure(
            exposure.CharacterId,
            exposure.DiseaseId,
            exposure.Route,
            exposure.ExposureHours,
            exposure.EnvironmentCoefficient);
    }

    private void OnMedicalBloodContact(CharacterMedicalBloodContactEvent contact)
    {
        if (!contact.PatientId.IsValid || !contact.ClinicianId.IsValid)
            return;

        if (contact.UsedExtractedBlood)
        {
            RecordPhysicalExposure(
                contact.PatientId,
                "disease:blood-wasting",
                DiseaseTransmissionRoute.Blood,
                24f,
                1f);
        }

        foreach (string diseaseId in health.GetContagious()
                     .Where(value => value.CharacterId.Equals(contact.PatientId))
                     .Select(value => value.DiseaseId)
                     .Distinct(StringComparer.Ordinal))
        {
            DiseaseDefinition disease = definitions.Require(diseaseId);
            if ((disease.Routes & DiseaseTransmissionRoute.Blood) == 0)
                continue;
            RecordPhysicalExposure(
                contact.ClinicianId,
                disease.Id,
                DiseaseTransmissionRoute.Blood,
                24f,
                1f);
        }
    }

    private void RecordPhysicalExposure(
        CharacterId characterId,
        string diseaseId,
        DiseaseTransmissionRoute route,
        float exposureHours,
        float environmentCoefficient)
    {
        if (!characterId.IsValid || exposureHours <= 0f || environmentCoefficient <= 0f)
            return;
        DiseaseDefinition disease = definitions.Require(diseaseId);
        int routeBits = (int)route;
        if (route == DiseaseTransmissionRoute.None
            || (routeBits & (routeBits - 1)) != 0
            || (disease.Routes & route) == 0)
        {
            throw new InvalidOperationException(
                $"Disease '{disease.Id}' does not permit exposure route '{route}'.");
        }

        CharacterActor actor = world.Characters.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.IsDead
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId candidateId)
            && candidateId.Equals(characterId));
        if (actor == null)
            return;
        health.RecordExposure(
            disease.Id,
            new[] { new PopulationExposureTarget(characterId, ResolveSusceptibility(actor, disease)) },
            exposureHours,
            environmentCoefficient);
    }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        AggregateAmbientExposure();
        IReadOnlyList<PopulationHealthChange> changes = health.AdvanceToDay(ended.day + 1);
        foreach (PopulationHealthChange change in changes)
        {
            if (change.Kind == PopulationHealthChangeKind.DailyBodyBurden)
                ProjectBodyBurden(change);
        }
        ProjectSymptomMood();
    }

    private void ProjectSymptomMood()
    {
        foreach (CharacterActor actor in world.Characters)
        {
            if (actor == null || actor.IsDead
                || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId))
                continue;
            foreach (DiseaseSymptomEffectSnapshot symptom in
                     symptoms.GetActiveSymptoms(characterId))
            {
                actor.ApplyMoodFactor(
                    "mood:disease:" + symptom.DiseaseId,
                    symptom.SymptomProfileId,
                    symptom.MoodDelta,
                    360f,
                    1);
            }
        }
    }

    private void AggregateAmbientExposure()
    {
        if (!gridWorld.TryGetGrid(out Grid grid)) return;
        Dictionary<string, List<CharacterActor>> groups = new(StringComparer.Ordinal);
        foreach (CharacterActor actor in world.Characters)
        {
            if (actor == null || actor.IsDead
                || !CharacterPersistentIdentity.TryGet(actor, out _)) continue;
            Vector2Int position = actor.GetNowXY();
            string key = rooms.TryGetRoom(grid, position, out RoomInstance room)
                ? "room:" + room.Id
                : $"cell:{position.x}:{position.y}";
            if (!groups.TryGetValue(key, out List<CharacterActor> members))
                groups.Add(key, members = new List<CharacterActor>());
            members.Add(actor);
        }

        Dictionary<CharacterId, string[]> contagious = health.GetContagious()
            .GroupBy(value => value.CharacterId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.DiseaseId).Distinct().ToArray());
        foreach (List<CharacterActor> members in groups.Values)
        {
            Vector2Int[] positions = members.Select(value => value.GetNowXY()).ToArray();
            float environmentCoefficient = environment.TryGetAverage(
                    positions,
                    out EnvironmentalCellSnapshot average)
                ? Mathf.Lerp(1.5f, 0.75f, Mathf.Clamp01(average.AirQuality / 100f))
                : 1f;
            foreach (IGrouping<string, CharacterActor> sources in members
                         .Where(actor => contagious.ContainsKey(CharacterPersistentIdentity.Require(actor)))
                         .SelectMany(actor => contagious[CharacterPersistentIdentity.Require(actor)]
                             .Select(diseaseId => (actor, diseaseId)))
                         .GroupBy(value => value.diseaseId, value => value.actor))
            {
                DiseaseDefinition disease = definitions.Require(sources.Key);
                if ((disease.Routes & (DiseaseTransmissionRoute.Air
                                       | DiseaseTransmissionRoute.Droplet)) == 0)
                    continue;
                HashSet<CharacterId> sourceIds = sources
                    .Select(CharacterPersistentIdentity.Require)
                    .ToHashSet();
                PopulationExposureTarget[] targets = members
                    .Where(actor => !sourceIds.Contains(CharacterPersistentIdentity.Require(actor)))
                    .Select(actor => new PopulationExposureTarget(
                        CharacterPersistentIdentity.Require(actor),
                        ResolveSusceptibility(actor, disease)))
                    .ToArray();
                health.RecordExposure(
                    disease.Id,
                    targets,
                    24f,
                    environmentCoefficient * Mathf.Min(2f, 1f + 0.15f * (sourceIds.Count - 1)));
            }
        }
    }

    private void ProjectBodyBurden(PopulationHealthChange change)
    {
        CharacterActor actor = world.Characters.FirstOrDefault(candidate =>
            CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && id.Equals(change.CharacterId));
        if (actor == null || actor.IsDead) return;
        AnatomyProfileDefinition profile = anatomyProfiles.GetForSpecies(actor.SpeciesTag);
        AnatomyFunction target = change.TargetSystem switch
        {
            DiseaseTargetSystem.Consciousness => AnatomyFunction.Consciousness,
            DiseaseTargetSystem.Breathing => AnatomyFunction.Breathing,
            DiseaseTargetSystem.Digestion => AnatomyFunction.Digestion,
            DiseaseTargetSystem.Filtration => AnatomyFunction.Filtration,
            _ => AnatomyFunction.Core
        };
        AnatomyNodeDefinition node = profile.Nodes
            .Where(value => (value.Functions & target) != 0)
            .OrderByDescending(value => value.Vital)
            .ThenByDescending(value => value.CapacityWeight)
            .ThenBy(value => value.NodeId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (node == null) return;
        DiseaseDefinition disease = definitions.Require(change.DiseaseId);
        float dailyBurden = change.Severity /
            Mathf.Max(1, disease.Chronic ? 30 : disease.ContagiousDays);
        anatomyHealth.TryAddNodeBurden(
            actor,
            node.NodeId,
            rejection: 0f,
            mutation: 0f,
            infection: dailyBurden,
            out _);
    }

    private float ResolveSusceptibility(
        CharacterActor actor,
        DiseaseDefinition disease)
    {
        if (actor?.profile == null) return 1f;
        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        float route = (disease.Routes & DiseaseTransmissionRoute.Air) != 0
            ? Mathf.Max(0.05f, actor.profile.GetEnvironmentProfile().airborneExposureMultiplier)
            : 1f;
        float broad = heritableTraits.GetMultiplier(
            characterId,
            HeritableTraitConsequenceKind.DiseaseResistance,
            "all");
        float toxin = (disease.Routes & (DiseaseTransmissionRoute.Food
                                          | DiseaseTransmissionRoute.Water)) != 0
            ? heritableTraits.GetMultiplier(
                characterId,
                HeritableTraitConsequenceKind.DiseaseResistance,
                "toxin")
            : 1f;
        return Mathf.Clamp(route / Mathf.Max(0.1f, broad * toxin), 0.05f, 3f);
    }
}
