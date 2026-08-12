using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterNarrativeRuntime :
    ICharacterNarrativeQuery,
    ICharacterNarrativeCommand,
    ICharacterNarrativePersistence,
    ICharacterProficiencyQuery,
    ICharacterProficiencyCommand
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly ICharacterNarrativeCatalog catalog;
    private readonly CharacterIdentityStateStore identityStates;
    private readonly IGameContentDefinitionSource content;
    private int version = 1;

    public CharacterNarrativeRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        ICharacterNarrativeCatalog catalog,
        CharacterIdentityStateStore identityStates = null,
        IGameContentDefinitionSource content = null)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.identityStates = identityStates ?? new CharacterIdentityStateStore();
        this.content = content;
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

    public bool TryGetProficiency(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        long absoluteHour,
        out CharacterProficiencySnapshot snapshot)
    {
        catalog.Require(proficiencyId);
        if (!Current.Characters.ContainsKey(characterId))
        {
            snapshot = default;
            return false;
        }
        CharacterNarrativeRecord record = RequireWritable(characterId);
        bool found = record.TryGetProficiency(
            proficiencyId,
            absoluteHour,
            out snapshot);
        if (found) version = unchecked(version + 1);
        return found;
    }

    public IReadOnlyList<CharacterProficiencySnapshot> GetAllProficiencies(
        CharacterId characterId,
        long absoluteHour)
    {
        IReadOnlyList<CharacterProficiencySnapshot> snapshots =
            RequireWritable(characterId).GetAllProficiencies(
                catalog.Proficiencies,
                absoluteHour);
        version = unchecked(version + 1);
        return snapshots;
    }

    public long AddApprovedWork(
        CharacterId characterId,
        ProficiencyWorkProfile profile,
        float approvedWork,
        float difficultyMultiplier,
        ProficiencyWorkOutcome outcome,
        float learningMultiplier,
        float repetitionMultiplier,
        long absoluteHour)
    {
        catalog.Require(profile.Primary);
        if (profile.Secondary.IsValid) catalog.Require(profile.Secondary);
        long awarded = RequireWritable(characterId).AddApprovedWork(
            profile,
            approvedWork,
            difficultyMultiplier,
            outcome,
            learningMultiplier,
            repetitionMultiplier,
            absoluteHour);
        if (awarded > 0L) version = unchecked(version + 1);
        return awarded;
    }

    public long AddDirectExperience(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        float experience,
        long absoluteHour,
        bool applyLearningMultiplier = true)
    {
        catalog.Require(proficiencyId);
        long awarded = RequireWritable(characterId).AddDirectExperience(
            proficiencyId,
            experience,
            absoluteHour,
            applyLearningMultiplier);
        if (awarded > 0L) version = unchecked(version + 1);
        return awarded;
    }

    public long AddCombatExperience(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        float experience,
        bool training,
        string stableAwardKey,
        long absoluteHour)
    {
        catalog.Require(proficiencyId);
        long awarded = RequireWritable(characterId).AddCombatExperience(
            proficiencyId,
            experience,
            training,
            stableAwardKey,
            absoluteHour);
        if (awarded > 0L) version = unchecked(version + 1);
        return awarded;
    }

    public void RecordPractice(
        CharacterId characterId,
        CharacterProficiencyId proficiencyId,
        long absoluteHour)
    {
        catalog.Require(proficiencyId);
        RequireWritable(characterId).RecordPractice(
            proficiencyId,
            absoluteHour);
        version = unchecked(version + 1);
    }

    public bool CanPerformPractice(
        CharacterId characterId,
        string practiceId,
        int absoluteDay,
        out int nextAllowedAbsoluteDay)
    {
        nextAllowedAbsoluteDay = int.MaxValue;
        CulturalPracticeDefinitionSO practice = catalog.Practices.FirstOrDefault(
            value => string.Equals(
                value.StableId,
                practiceId?.Trim(),
                StringComparison.Ordinal));
        return practice != null
            && Current.Characters.TryGetValue(
                characterId,
                out CharacterNarrativeRecord record)
            && record.CanPerformPractice(
                practice.StableId,
                Math.Max(0, absoluteDay),
                out nextAllowedAbsoluteDay);
    }

    public bool TryPreviewAmbitionProgress(
        CharacterId characterId,
        int amount,
        out AmbitionProgressPreview preview)
    {
        preview = default;
        if (amount <= 0
            || !TryGet(characterId, out CharacterNarrativeSnapshot narrative)
            || narrative.AmbitionStatus != CharacterAmbitionStatus.Active
            || !narrative.ActiveAmbitionId.IsValid)
        {
            return false;
        }
        CharacterAmbitionDefinitionSO definition = catalog.Require(
            narrative.ActiveAmbitionId);
        preview = new AmbitionProgressPreview(
            narrative.ActiveAmbitionId,
            narrative.AmbitionProgress,
            definition.targetProgress,
            narrative.AmbitionProgress + amount >= definition.targetProgress,
            definition.completionRewards);
        return true;
    }

    public CharacterNarrativeSnapshot Register(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        IReadOnlyList<string> expressed,
        IReadOnlyList<string> latent,
        IReadOnlyList<CharacterStartingProficiencyExperience>
            startingProficiencies = null)
    {
        if (Writable.Characters.ContainsKey(characterId)) throw new InvalidOperationException($"Narrative '{characterId.Value}' is already registered.");
        int index = (int)(PersistentEntityId.GetStableHash32(characterId) % (uint)catalog.Backgrounds.Count);
        CharacterBackgroundId background = new(catalog.Backgrounds[index].StableId);
        SpeciesCultureId culture = new(catalog.RequireDefaultCulture(phenotypeSpeciesId.Value).StableId);
        ResolveHeritableTraits(
            characterId,
            phenotypeSpeciesId,
            expressed,
            latent,
            out IReadOnlyList<string> resolvedExpressed,
            out IReadOnlyList<string> resolvedLatent);
        CharacterNarrativeRecord record = CharacterNarrativeRecord.Create(
            characterId,
            phenotypeSpeciesId,
            background,
            culture,
            resolvedExpressed,
            resolvedLatent,
            startingProficiencies: startingProficiencies);
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
        float loyalty,
        IReadOnlyList<CharacterStartingProficiencyExperience>
            startingProficiencies = null)
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
            loyalty,
            startingProficiencies);
        Writable.Characters.Add(characterId, record);
        version = unchecked(version + 1);
        return record.Snapshot();
    }

    public bool TryInitializeBackground(
        CharacterId characterId,
        int absoluteDay,
        out BackgroundInitializationOutcome outcome)
    {
        CharacterNarrativeRecord record = RequireWritable(characterId);
        bool initialized = record.TryInitializeBackground(
            catalog.Require(record.Snapshot().BackgroundId),
            absoluteDay,
            out outcome);
        if (initialized)
        {
            version = unchecked(version + 1);
        }
        return initialized;
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
    public void RecordPracticeParticipation(
        CharacterId characterId,
        string practiceId,
        SpeciesCultureId practiceCultureId,
        int assimilationDays,
        int absoluteDay)
    {
        CulturalPracticeDefinitionSO practice = catalog.Practices.Single(value =>
            string.Equals(
                value.StableId,
                practiceId?.Trim(),
                StringComparison.Ordinal));
        if (!string.Equals(
                practice.cultureId,
                practiceCultureId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Practice culture does not match its authored definition.");
        }
        catalog.Require(practiceCultureId);
        RequireWritable(characterId).RecordPracticeParticipation(
            practice.StableId,
            practiceCultureId,
            assimilationDays,
            absoluteDay);
        version = unchecked(version + 1);
    }
    public void RecordPracticeNeglect(
        CharacterId characterId,
        string practiceId,
        int absoluteDay)
    {
        CulturalPracticeDefinitionSO practice = catalog.Practices.Single(value =>
            string.Equals(
                value.StableId,
                practiceId?.Trim(),
                StringComparison.Ordinal));
        RequireWritable(characterId).RecordPracticeNeglect(
            practice.StableId,
            absoluteDay);
        version = unchecked(version + 1);
    }
    public void RecordResolvedEvent(CharacterId characterId, NarrativeEventId eventId, string choiceId, int absoluteDay)
    {
        RequireWritable(characterId).RecordEvent(
            catalog.Require(eventId),
            choiceId,
            absoluteDay,
            archivedId => catalog.Require(new NarrativeEventId(archivedId)).category);
        version = unchecked(version + 1);
    }
    public void MarkHeritableTraitsAnalyzed(CharacterId characterId)
    {
        RequireWritable(characterId).MarkHeritableTraitsAnalyzed();
        version = unchecked(version + 1);
    }

    public CharacterNarrativeWorldSaveData Capture()
    {
        CharacterNarrativeWorldSaveData data = Current.Capture();
        data.identityStates = identityStates.Capture()
            .Select(value => value.Clone()).ToList();
        return data;
    }
    public CharacterNarrativeAggregateState PrepareRestore(CharacterNarrativeWorldSaveData data) => CharacterNarrativeAggregateState.Restore(data, catalog);
    public void PublishRestore(CharacterNarrativeAggregateState candidate)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        if (candidate.IdentityStates.Count > 0)
        {
            if (content == null)
                throw new InvalidOperationException(
                    "Identity rule state restore requires the content definition source.");
            identityStates.Restore(
                candidate.IdentityStates,
                content.GetAll<CharacterTraitSO>());
        }
        else
        {
            identityStates.Restore(
                Array.Empty<CharacterIdentityRuntimeStateSaveData>(),
                Array.Empty<CharacterTraitSO>());
        }
        rootStore.Replace(candidate);
        version = unchecked(version + 1);
    }

    private void ResolveHeritableTraits(
        CharacterId characterId,
        CharacterSpeciesId speciesId,
        IReadOnlyList<string> expressed,
        IReadOnlyList<string> latent,
        out IReadOnlyList<string> resolvedExpressed,
        out IReadOnlyList<string> resolvedLatent)
    {
        string[] authored = (expressed ?? Array.Empty<string>())
            .Concat(latent ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (authored.Length > 0)
        {
            foreach (string id in authored) catalog.RequireHeritable(id);
            string[] expressedSelection = (expressed ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .Take(4)
                .ToArray();
            resolvedExpressed = expressedSelection;
            resolvedLatent = (latent ?? Array.Empty<string>())
                .Where(value => !expressedSelection.Contains(value, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToArray();
            return;
        }

        HeritableTraitDefinitionSO[] candidates = catalog.HeritableTraits
            .Where(value => value.compatibleSpeciesTags == null
                || value.compatibleSpeciesTags.Count == 0
                || value.compatibleSpeciesTags.Contains(
                    speciesId.Value,
                    StringComparer.Ordinal))
            .OrderBy(value => PersistentEntityId.GetStableHash32(
                characterId.Value + ":" + value.traitId))
            .ThenBy(value => value.traitId, StringComparer.Ordinal)
            .ToArray();
        List<HeritableTraitDefinitionSO> selected = new();
        foreach (HeritableTraitDefinitionSO candidate in candidates)
        {
            if (selected.Any(value => !string.IsNullOrWhiteSpace(value.incompatibilityGroup)
                && string.Equals(
                    value.incompatibilityGroup,
                    candidate.incompatibilityGroup,
                    StringComparison.Ordinal)))
                continue;
            selected.Add(candidate);
            if (selected.Count == 3) break;
        }
        resolvedExpressed = selected.Take(2).Select(value => value.traitId).ToArray();
        resolvedLatent = selected.Skip(2).Take(1).Select(value => value.traitId).ToArray();
    }

    private CharacterNarrativeAggregateState Current => rootStore.GetOrCreate(CreateEmpty);
    private CharacterNarrativeAggregateState Writable => rootStore.GetOrCreateWritable(CreateEmpty, value => CharacterNarrativeAggregateState.Restore(value.Capture(), catalog));
    private static CharacterNarrativeAggregateState CreateEmpty() => new();
    private CharacterNarrativeRecord RequireWritable(CharacterId id) => Writable.Characters.TryGetValue(id, out CharacterNarrativeRecord value)
        ? value
        : throw new KeyNotFoundException($"Unknown narrative character '{id.Value}'.");
}

/// <summary>
/// Converts one physical trait-analysis kit at an operational trait analyzer
/// into a persistent player-facing reveal of the subject's latent hereditary
/// traits. Internal inheritance rules retain the latent traits before analysis;
/// only their visible projection changes.
/// </summary>
public sealed class TraitAnalysisCommandRuntime : ITraitAnalysisCommand
{
    private const string TraitAnalyzerBuildingId = "building:8879";
    private const string TraitAnalysisKitId = "medical:trait-analysis-kit";

    private readonly ICharacterNarrativeQuery query;
    private readonly ICharacterNarrativePersistence persistence;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IStockQuery stock;
    private readonly IItemReservationService reservations;
    private readonly IAtomicItemConsumptionService atomicItems;

    public TraitAnalysisCommandRuntime(
        ICharacterNarrativeQuery query,
        ICharacterNarrativePersistence persistence,
        IFacilityCapabilityQuery facilities,
        IStockQuery stock,
        IItemReservationService reservations,
        IAtomicItemConsumptionService atomicItems)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.reservations = reservations
            ?? throw new ArgumentNullException(nameof(reservations));
        this.atomicItems = atomicItems
            ?? throw new ArgumentNullException(nameof(atomicItems));
    }

    public bool TryAnalyze(
        CharacterId characterId,
        out IReadOnlyList<string> revealedLatentTraitIds,
        out DomainFailure failure)
    {
        revealedLatentTraitIds = Array.Empty<string>();
        failure = DomainFailure.None;
        if (!characterId.IsValid
            || !query.TryGet(characterId, out CharacterNarrativeSnapshot current)
            || current.HeritableTraitsAnalyzed
            || current.LatentHeritableTraitIds.Count == 0)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }
        if (!facilities.FindOperational(
                FacilityCapabilityKind.Medical,
                TraitAnalyzerBuildingId).Any())
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing);
            return false;
        }

        WorldItemStackSnapshot kit = stock.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && !value.Forbidden
                && value.AvailableQuantity > 0
                && string.Equals(
                    value.ItemId,
                    TraitAnalysisKitId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (kit == null)
        {
            failure = new DomainFailure(FailureCode.ProductionMaterialsMissing);
            return false;
        }

        string owner = $"trait-analysis:{characterId.Value}";
        if (!reservations.TryReserveQuantities(
                new[] { new ReservedItemConsumption(kit.StackId, 1) },
                owner,
                ItemReservationPurpose.Medical,
                $"medical:{TraitAnalyzerBuildingId}:trait-analysis-kit"))
        {
            failure = new DomainFailure(FailureCode.ItemTransferStackUnavailable);
            return false;
        }

        CharacterNarrativeAggregateState candidate;
        CharacterNarrativeRecord candidateRecord;
        try
        {
            candidate = persistence.PrepareRestore(persistence.Capture());
            if (!candidate.Characters.TryGetValue(characterId, out candidateRecord))
                throw new InvalidOperationException();
            candidateRecord.MarkHeritableTraitsAnalyzed();
        }
        catch (InvalidOperationException)
        {
            reservations.Release(kit.StackId, owner);
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        ReservedItemConsumption[] cost =
        {
            new(kit.StackId, 1)
        };
        if (!atomicItems.TryConsumeReserved(cost, owner, out failure))
        {
            reservations.Release(kit.StackId, owner);
            return false;
        }

        persistence.PublishRestore(candidate);
        revealedLatentTraitIds = candidateRecord.Snapshot()
            .VisibleLatentHeritableTraitIds;
        return true;
    }
}

public sealed class CharacterNarrativeApplicationAdapter : IStartable, IDisposable
{
    private readonly ICharacterNarrativeQuery query;
    private readonly ICharacterNarrativeCommand commands;
    private readonly ICharacterNarrativeCatalog catalog;
    private readonly ICharacterLifeQuery life;
    private readonly IGameEventBus events;
    private readonly IContentRequirementEvaluator requirements;
    private readonly V20MilestoneWorldSnapshotProjector world;
    private readonly ICharacterWorldQuery characters;
    private IDisposable dayEndedSubscription;

    public CharacterNarrativeApplicationAdapter(
        ICharacterNarrativeQuery query,
        ICharacterNarrativeCommand commands,
        ICharacterNarrativeCatalog catalog,
        ICharacterLifeQuery life,
        IGameEventBus events,
        IContentRequirementEvaluator requirements,
        V20MilestoneWorldSnapshotProjector world,
        ICharacterWorldQuery characters)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.requirements = requirements
            ?? throw new ArgumentNullException(nameof(requirements));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
    }

    public void Start() => dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
    public void Dispose() { dayEndedSubscription?.Dispose(); dayEndedSubscription = null; }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        int nextDay = ended.day + 1;
        RunMilestoneEvaluationSnapshot snapshot = world.Build(nextDay);
        foreach (CharacterLifeRecord record in life.Records.OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal))
        {
            if (!query.TryGet(record.CharacterId, out CharacterNarrativeSnapshot narrative))
            {
                narrative = commands.Register(record.CharacterId, record.PhenotypeSpeciesId, Array.Empty<string>(), Array.Empty<string>());
            }
            if (!narrative.BackgroundInitialized
                && commands.TryInitializeBackground(
                    record.CharacterId,
                    nextDay,
                    out BackgroundInitializationOutcome background))
            {
                ApplyBackgroundInitialization(record.CharacterId, background);
                query.TryGet(record.CharacterId, out narrative);
                ApplyInitialCultureAttitudes(record.CharacterId, narrative);
            }
            if (narrative.AmbitionStatus == CharacterAmbitionStatus.Active)
            {
                CharacterAmbitionDefinitionSO active = catalog.Require(
                    narrative.ActiveAmbitionId);
                if (HasRequirements(active.failureConditions)
                    && requirements.TryEvaluate(
                        active.failureConditions,
                        snapshot,
                        new[] { record.CharacterId.Value },
                        out _))
                {
                    commands.FailAmbition(record.CharacterId, nextDay);
                }
                continue;
            }
            if (record.LifeStage >= CharacterLifeStage.Adult
                && nextDay >= narrative.NextAmbitionAllowedAbsoluteDay)
            {
                uint seed = PersistentEntityId.GetStableHash32(record.CharacterId.Value + ":" + nextDay);
                CharacterAmbitionDefinitionSO[] eligible = catalog.Ambitions
                    .Where(value => requirements.TryEvaluate(
                        value.activationRequirements,
                        snapshot,
                        new[] { record.CharacterId.Value },
                        out _))
                    .OrderBy(value => value.StableId, StringComparer.Ordinal)
                    .ToArray();
                if (eligible.Length == 0)
                {
                    continue;
                }
                CharacterAmbitionDefinitionSO selected = eligible[
                    (int)(seed % (uint)eligible.Length)];
                commands.StartAmbition(
                    record.CharacterId,
                    new CharacterAmbitionId(selected.StableId),
                    nextDay);
            }
        }
    }

    private static bool HasRequirements(V20ContentRequirementSet value) =>
        value != null
        && ((value.items?.Count ?? 0) > 0
            || (value.facilities?.Count ?? 0) > 0
            || (value.research?.Count ?? 0) > 0
            || (value.characters?.Count ?? 0) > 0
            || (value.factions?.Count ?? 0) > 0
            || (value.worldMetrics?.Count ?? 0) > 0
            || (value.requiredFlags?.Count ?? 0) > 0
            || (value.excludedFlags?.Count ?? 0) > 0);

    private void ApplyBackgroundInitialization(
        CharacterId characterId,
        BackgroundInitializationOutcome outcome)
    {
        CharacterActor actor = characters.Characters.FirstOrDefault(value =>
            value != null
            && value.Identity != null
            && string.Equals(
                value.Identity.PersistentId,
                characterId.Value,
                StringComparison.Ordinal));
        if (actor == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(outcome.InitialMemoryCode))
        {
            actor.AiMemory?.RecordDecision(
                CharacterAiBranch.ContinueCurrent,
                CharacterAiIntentionType.None,
                outcome.InitialMemoryCode,
                0.1f);
        }
        foreach (V20ContentEffect effect in outcome.StartingEffects)
        {
            switch (effect.kind)
            {
                case V20ContentEffectKind.Mood:
                    actor.ApplyMoodFactor(
                        $"background:{outcome.BackgroundId.Value}:{effect.targetId}",
                        effect.targetId,
                        effect.amount,
                        Math.Max(1, effect.durationDays) * 180f);
                    break;
                case V20ContentEffectKind.SkillExperience:
                    throw new InvalidOperationException(
                        $"Background '{outcome.BackgroundId.Value}' must author starting proficiency XP through startingSkills, not a generic SkillExperience effect.");
                default:
                    throw new InvalidOperationException(
                        $"Background '{outcome.BackgroundId.Value}' has unsupported starting effect '{effect.kind}'.");
            }
        }
    }

    private void ApplyInitialCultureAttitudes(
        CharacterId newcomerId,
        CharacterNarrativeSnapshot newcomerNarrative)
    {
        if (newcomerNarrative == null || !newcomerNarrative.CultureId.IsValid)
        {
            return;
        }
        CharacterActor newcomer = characters.Characters.FirstOrDefault(value =>
            value != null
            && value.Identity != null
            && string.Equals(
                value.Identity.PersistentId,
                newcomerId.Value,
                StringComparison.Ordinal));
        if (newcomer == null)
        {
            return;
        }

        SpeciesCultureDefinitionSO newcomerCulture = catalog.Require(
            newcomerNarrative.CultureId);
        foreach (CharacterActor resident in characters.Characters
                     .Where(value => value != null
                         && value.Identity != null
                         && !string.Equals(
                             value.Identity.PersistentId,
                             newcomerId.Value,
                             StringComparison.Ordinal))
                     .OrderBy(
                         value => value.Identity.PersistentId,
                         StringComparer.Ordinal))
        {
            CharacterId residentId = CharacterPersistentIdentity.Require(resident);
            if (!query.TryGet(residentId, out CharacterNarrativeSnapshot residentNarrative)
                || !residentNarrative.BackgroundInitialized
                || !residentNarrative.CultureId.IsValid
                || residentNarrative.CultureId.Equals(newcomerNarrative.CultureId))
            {
                continue;
            }

            SpeciesCultureDefinitionSO residentCulture = catalog.Require(
                residentNarrative.CultureId);
            float newcomerSentiment = ResolveCultureAttitude(
                newcomerCulture,
                residentCulture.StableId);
            float residentSentiment = ResolveCultureAttitude(
                residentCulture,
                newcomerCulture.StableId);
            newcomer.SocialMemory.RememberCharacterExperience(
                resident,
                newcomerSentiment,
                "culture:initial-attitude");
            resident.SocialMemory.RememberCharacterExperience(
                newcomer,
                residentSentiment,
                "culture:initial-attitude");
        }
    }

    private static float ResolveCultureAttitude(
        SpeciesCultureDefinitionSO source,
        string targetCultureId)
    {
        V20WeightedId authored = (source?.otherCultureAttitudes
                ?? new List<V20WeightedId>())
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.id,
                    targetCultureId,
                    StringComparison.Ordinal));
        return Mathf.Clamp((authored?.weight ?? 1f) - 1f, -1f, 1f);
    }
}
