using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class CharacterNarrativeRuntime :
    ICharacterNarrativeQuery,
    ICharacterNarrativeCommand,
    ICharacterNarrativePersistence
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly ICharacterNarrativeCatalog catalog;
    private int version = 1;

    public CharacterNarrativeRuntime(DungeonRuntimeAggregateRootStore rootStore, ICharacterNarrativeCatalog catalog)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public int Version => version;
    public IReadOnlyCollection<CharacterNarrativeSnapshot> All => Current.Characters.Values.Select(value => value.Snapshot()).ToArray();
    public bool TryGet(CharacterId characterId, out CharacterNarrativeSnapshot snapshot)
    {
        if (Current.Characters.TryGetValue(characterId, out CharacterNarrativeRecord value))
        {
            snapshot = value.Snapshot();
            return true;
        }
        snapshot = null;
        return false;
    }

    public CharacterNarrativeSnapshot Register(CharacterId characterId, CharacterSpeciesId phenotypeSpeciesId, IReadOnlyList<string> expressed, IReadOnlyList<string> latent)
    {
        if (Writable.Characters.ContainsKey(characterId)) throw new InvalidOperationException($"Narrative '{characterId.Value}' is already registered.");
        int index = (int)(PersistentEntityId.GetStableHash32(characterId) % (uint)catalog.Backgrounds.Count);
        CharacterBackgroundId background = new(catalog.Backgrounds[index].StableId);
        SpeciesCultureId culture = new(catalog.RequireDefaultCulture(phenotypeSpeciesId.Value).StableId);
        CharacterNarrativeRecord record = CharacterNarrativeRecord.Create(characterId, phenotypeSpeciesId, background, culture, expressed, latent);
        Writable.Characters.Add(characterId, record);
        version = unchecked(version + 1);
        return record.Snapshot();
    }

    public CharacterNarrativeSnapshot RegisterEnemyOrigin(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        IReadOnlyList<string> expressed,
        IReadOnlyList<string> latent,
        CharacterBackgroundId backgroundId,
        SpeciesCultureId cultureId,
        string enemyArchetypeId,
        string originFactionId,
        string militaryTrainingId,
        float loyalty)
    {
        if (Writable.Characters.ContainsKey(characterId))
            throw new InvalidOperationException($"Narrative '{characterId.Value}' is already registered.");
        catalog.Require(backgroundId);
        catalog.Require(cultureId);
        CharacterNarrativeRecord record = CharacterNarrativeRecord.Create(
            characterId,
            phenotypeSpeciesId,
            backgroundId,
            cultureId,
            expressed,
            latent,
            enemyArchetypeId,
            originFactionId,
            militaryTrainingId,
            loyalty);
        Writable.Characters.Add(characterId, record);
        version = unchecked(version + 1);
        return record.Snapshot();
    }

    public void StartAmbition(CharacterId characterId, CharacterAmbitionId ambitionId, int absoluteDay)
    {
        catalog.Require(ambitionId);
        RequireWritable(characterId).StartAmbition(ambitionId, absoluteDay);
        version = unchecked(version + 1);
    }

    public void AddAmbitionProgress(CharacterId characterId, int amount, int absoluteDay)
    {
        CharacterNarrativeRecord record = RequireWritable(characterId);
        CharacterAmbitionDefinitionSO definition = catalog.Require(record.Snapshot().ActiveAmbitionId);
        record.AddAmbitionProgress(amount, definition.targetProgress, absoluteDay);
        version = unchecked(version + 1);
    }

    public void FailAmbition(CharacterId characterId, int absoluteDay) { RequireWritable(characterId).CloseAmbition(CharacterAmbitionStatus.Failed, absoluteDay); version = unchecked(version + 1); }
    public void AbandonAmbition(CharacterId characterId, int absoluteDay) { RequireWritable(characterId).CloseAmbition(CharacterAmbitionStatus.Abandoned, absoluteDay); version = unchecked(version + 1); }
    public void BeginAssimilation(CharacterId characterId, SpeciesCultureId targetCultureId) { catalog.Require(targetCultureId); RequireWritable(characterId).BeginAssimilation(targetCultureId); version = unchecked(version + 1); }
    public void AdvanceAssimilationDay(CharacterId characterId) { RequireWritable(characterId).AdvanceAssimilationDay(); version = unchecked(version + 1); }
    public void RecordResolvedEvent(CharacterId characterId, NarrativeEventId eventId, string choiceId, int absoluteDay)
    {
        RequireWritable(characterId).RecordEvent(
            catalog.Require(eventId),
            choiceId,
            absoluteDay,
            archivedId => catalog.Require(new NarrativeEventId(archivedId)).category);
        version = unchecked(version + 1);
    }

    public CharacterNarrativeWorldSaveData Capture() => Current.Capture();
    public CharacterNarrativeAggregateState PrepareRestore(CharacterNarrativeWorldSaveData data) => CharacterNarrativeAggregateState.Restore(data, catalog);
    public void PublishRestore(CharacterNarrativeAggregateState candidate) { rootStore.Replace(candidate ?? throw new ArgumentNullException(nameof(candidate))); version = unchecked(version + 1); }

    private CharacterNarrativeAggregateState Current => rootStore.GetOrCreate(CreateEmpty);
    private CharacterNarrativeAggregateState Writable => rootStore.GetOrCreateWritable(CreateEmpty, value => CharacterNarrativeAggregateState.Restore(value.Capture(), catalog));
    private static CharacterNarrativeAggregateState CreateEmpty() => new();
    private CharacterNarrativeRecord RequireWritable(CharacterId id) => Writable.Characters.TryGetValue(id, out CharacterNarrativeRecord value)
        ? value
        : throw new KeyNotFoundException($"Unknown narrative character '{id.Value}'.");
}

public sealed class CharacterNarrativeApplicationAdapter : IStartable, IDisposable
{
    private readonly ICharacterNarrativeQuery query;
    private readonly ICharacterNarrativeCommand commands;
    private readonly ICharacterNarrativeCatalog catalog;
    private readonly ICharacterLifeQuery life;
    private readonly IGameEventBus events;
    private IDisposable dayEndedSubscription;

    public CharacterNarrativeApplicationAdapter(ICharacterNarrativeQuery query, ICharacterNarrativeCommand commands, ICharacterNarrativeCatalog catalog, ICharacterLifeQuery life, IGameEventBus events)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start() => dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
    public void Dispose() { dayEndedSubscription?.Dispose(); dayEndedSubscription = null; }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        int nextDay = ended.day + 1;
        foreach (CharacterLifeRecord record in life.Records.OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal))
        {
            if (!query.TryGet(record.CharacterId, out CharacterNarrativeSnapshot narrative))
            {
                narrative = commands.Register(record.CharacterId, record.PhenotypeSpeciesId, Array.Empty<string>(), Array.Empty<string>());
            }
            commands.AdvanceAssimilationDay(record.CharacterId);
            if (record.LifeStage >= CharacterLifeStage.Adult
                && narrative.AmbitionStatus != CharacterAmbitionStatus.Active
                && nextDay >= narrative.NextAmbitionAllowedAbsoluteDay)
            {
                uint seed = PersistentEntityId.GetStableHash32(record.CharacterId.Value + ":" + nextDay);
                CharacterAmbitionDefinitionSO selected = catalog.Ambitions[(int)(seed % (uint)catalog.Ambitions.Count)];
                commands.StartAmbition(record.CharacterId, new CharacterAmbitionId(selected.StableId), nextDay);
            }
        }
    }
}
