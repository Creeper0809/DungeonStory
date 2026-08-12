using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class CharacterNarrativeRecord
{
    private const int RecentEventLimit = 12;
    private readonly List<string> expressedTraits;
    private readonly List<string> latentTraits;
    private readonly List<CharacterNarrativeEventSaveData> recentEvents;
    private readonly Dictionary<LifeEventCategory, CharacterNarrativeEventSummarySaveData> summaries;
    private readonly Dictionary<string, CulturalPracticeParticipationSaveData>
        practiceParticipations;
    private readonly Dictionary<string, NarrativeSkillExperienceSaveData>
        proficiencyById;
    private readonly Dictionary<string, float> backgroundFactionReactionById;

    private CharacterNarrativeRecord(CharacterNarrativeSaveData data)
    {
        CharacterId = new CharacterId(data.characterId);
        PhenotypeSpeciesId = new CharacterSpeciesId(data.phenotypeSpeciesId);
        BackgroundId = new CharacterBackgroundId(data.backgroundId);
        CultureId = new SpeciesCultureId(data.cultureId);
        ActiveAmbitionId = new CharacterAmbitionId(data.activeAmbitionId);
        AmbitionStatus = data.ambitionStatus;
        AmbitionProgress = data.ambitionProgress;
        NextAmbitionAllowedAbsoluteDay = data.nextAmbitionAllowedAbsoluteDay;
        AssimilationTargetCultureId = new SpeciesCultureId(data.assimilationTargetCultureId);
        AssimilationDays = data.assimilationDays;
        expressedTraits = (data.expressedHeritableTraitIds ?? new()).Distinct(StringComparer.Ordinal).ToList();
        latentTraits = (data.latentHeritableTraitIds ?? new()).Distinct(StringComparer.Ordinal).ToList();
        HeritableTraitsAnalyzed = data.heritableTraitsAnalyzed;
        recentEvents = (data.recentEvents ?? new()).Select(Clone).OrderBy(value => value.absoluteDay).ToList();
        summaries = (data.eventSummaries ?? new()).ToDictionary(value => value.category, Clone);
        practiceParticipations = (data.practiceParticipations ?? new())
            .Where(value => value != null)
            .ToDictionary(value => Normalize(value.practiceId), Clone,
                StringComparer.Ordinal);
        OriginEnemyArchetypeId = Normalize(data.originEnemyArchetypeId);
        OriginFactionId = Normalize(data.originFactionId);
        MilitaryTrainingId = Normalize(data.militaryTrainingId);
        Loyalty = Math.Max(0f, Math.Min(100f, data.loyalty));
        BackgroundInitialized = data.backgroundInitialized;
        BackgroundInitializedAbsoluteDay = Math.Max(
            0,
            data.backgroundInitializedAbsoluteDay);
        InitialMemoryCode = Normalize(data.initialMemoryCode);
        proficiencyById = (data.skillExperience ?? new())
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.proficiencyId))
            .GroupBy(
                value => Normalize(value.proficiencyId),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    NarrativeSkillExperienceSaveData latest = group.Last();
                    long current = Math.Clamp(
                        group.Sum(value => Math.Max(0L, value.currentMilliExperience)),
                        0L,
                        ProficiencyProgressionRules.MasterCurrentCap);
                    long lifetime = Math.Max(
                        current,
                        group.Sum(value => Math.Max(0L, value.lifetimeMilliExperience)));
                    return new NarrativeSkillExperienceSaveData
                    {
                        proficiencyId = group.Key,
                        learningMultiplier =
                            CharacterProficiencySpecializationRules
                                .NormalizeSerializedMultiplier(
                                    latest.learningMultiplier),
                        currentMilliExperience = current,
                        lifetimeMilliExperience = lifetime,
                        lastPracticeAbsoluteHour = Math.Max(
                            0L,
                            latest.lastPracticeAbsoluteHour),
                        lastDecaySettlementAbsoluteHour = Math.Max(
                            Math.Max(0L, latest.lastPracticeAbsoluteHour),
                            latest.lastDecaySettlementAbsoluteHour),
                        maintenancePracticeMilliExperience = Math.Max(
                            0L,
                            latest.maintenancePracticeMilliExperience),
                        practiceAbsoluteDay = latest.practiceAbsoluteDay,
                        practiceMilliExperienceToday = Math.Max(
                            0L,
                            latest.practiceMilliExperienceToday),
                        combatAwardAbsoluteDay = latest.combatAwardAbsoluteDay,
                        combatAwardMilliToday = Math.Max(
                            0L,
                            latest.combatAwardMilliToday),
                        trainingAwardMilliToday = Math.Max(
                            0L,
                            latest.trainingAwardMilliToday),
                        recentCombatAwardKeys =
                            (latest.recentCombatAwardKeys ?? new List<string>())
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .Distinct(StringComparer.Ordinal)
                            .TakeLast(64)
                            .ToList()
                    };
                },
                StringComparer.Ordinal);
        if (proficiencyById.Values.Any(value =>
                !CharacterProficiencySpecializationRules.IsCanonical(
                    value.learningMultiplier)))
        {
            throw new InvalidOperationException(
                $"Character '{CharacterId.Value}' has an invalid proficiency learning multiplier.");
        }
        backgroundFactionReactionById =
            (data.backgroundFactionReactions ?? new())
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.factionId))
            .GroupBy(value => Normalize(value.factionId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Last().reaction,
                StringComparer.Ordinal);
    }

    public CharacterId CharacterId { get; }
    public CharacterSpeciesId PhenotypeSpeciesId { get; }
    public CharacterBackgroundId BackgroundId { get; }
    public SpeciesCultureId CultureId { get; private set; }
    public CharacterAmbitionId ActiveAmbitionId { get; private set; }
    public CharacterAmbitionStatus AmbitionStatus { get; private set; }
    public int AmbitionProgress { get; private set; }
    public int NextAmbitionAllowedAbsoluteDay { get; private set; }
    public SpeciesCultureId AssimilationTargetCultureId { get; private set; }
    public int AssimilationDays { get; private set; }
    public bool HeritableTraitsAnalyzed { get; private set; }
    public string OriginEnemyArchetypeId { get; }
    public string OriginFactionId { get; }
    public string MilitaryTrainingId { get; }
    public float Loyalty { get; private set; }
    public bool BackgroundInitialized { get; private set; }
    public int BackgroundInitializedAbsoluteDay { get; private set; }
    public string InitialMemoryCode { get; private set; }

    public static CharacterNarrativeRecord Create(
        CharacterId characterId,
        CharacterSpeciesId speciesId,
        CharacterBackgroundId backgroundId,
        SpeciesCultureId cultureId,
        IReadOnlyList<string> expressed,
        IReadOnlyList<string> latent,
        string enemyArchetypeId = "",
        string originFactionId = "",
        string militaryTrainingId = "",
        float loyalty = 0f,
        IReadOnlyList<CharacterStartingProficiencyExperience>
            startingProficiencies = null)
    {
        if (!characterId.IsValid) throw new ArgumentException("A valid CharacterId is required.", nameof(characterId));
        if (!backgroundId.IsValid || !cultureId.IsValid) throw new InvalidOperationException("Narrative registration requires valid definition ids.");
        string[] expressedIds = RequireTraits(expressed, 4, "expressed");
        string[] latentIds = RequireTraits(latent, 2, "latent");
        if (expressedIds.Intersect(latentIds, StringComparer.Ordinal).Any())
            throw new InvalidOperationException("A heritable trait cannot be both expressed and latent.");
        IReadOnlyList<CharacterStartingProficiencyExperience> starts =
            startingProficiencies != null && startingProficiencies.Count > 0
                ? startingProficiencies
                : CharacterStartingProficiencyRules.Create(
                    unchecked((int)PersistentEntityId.GetStableHash32(
                        characterId.Value)));
        CharacterStartingProficiencyRules.Validate(starts);
        return new CharacterNarrativeRecord(new CharacterNarrativeSaveData
        {
            characterId = characterId.Value,
            phenotypeSpeciesId = speciesId.Value,
            backgroundId = backgroundId.Value,
            cultureId = cultureId.Value,
            expressedHeritableTraitIds = expressedIds.ToList(),
            latentHeritableTraitIds = latentIds.ToList(),
            originEnemyArchetypeId = Normalize(enemyArchetypeId),
            originFactionId = Normalize(originFactionId),
            militaryTrainingId = Normalize(militaryTrainingId),
            loyalty = Math.Max(0f, Math.Min(100f, loyalty)),
            skillExperience = starts
                .OrderBy(value => value.proficiencyId, StringComparer.Ordinal)
                .Select(value => new NarrativeSkillExperienceSaveData
                {
                    proficiencyId = value.proficiencyId,
                    learningMultiplier =
                        CharacterProficiencySpecializationRules
                            .NormalizeSerializedMultiplier(
                                value.learningMultiplier),
                    currentMilliExperience = checked(
                        (long)value.experience
                        * ProficiencyProgressionRules.MilliPerExperience),
                    lifetimeMilliExperience = checked(
                        (long)value.experience
                        * ProficiencyProgressionRules.MilliPerExperience),
                    lastPracticeAbsoluteHour = 0L,
                    lastDecaySettlementAbsoluteHour = 0L
                })
                .ToList()
        });
    }

    public static CharacterNarrativeRecord Restore(CharacterNarrativeSaveData data, ICharacterNarrativeCatalog catalog)
    {
        if (data == null) throw new InvalidOperationException("Character narrative record is null.");
        CharacterNarrativeRecord value = new(data);
        if (!value.CharacterId.IsValid) throw new InvalidOperationException($"Invalid narrative CharacterId '{data.characterId}'.");
        catalog.Require(value.BackgroundId);
        catalog.Require(value.CultureId);
        if (value.ActiveAmbitionId.IsValid) catalog.Require(value.ActiveAmbitionId);
        foreach (string traitId in value.expressedTraits.Concat(value.latentTraits))
            catalog.RequireHeritable(traitId);
        if (value.expressedTraits.Count > 4 || value.latentTraits.Count > 2)
            throw new InvalidOperationException($"'{data.characterId}' exceeds heritable trait limits.");
        if (value.recentEvents.Count > RecentEventLimit)
            throw new InvalidOperationException($"'{data.characterId}' exceeds the recent narrative-event limit.");
        bool hasAnyEnemyOrigin = value.OriginEnemyArchetypeId.Length > 0
            || value.OriginFactionId.Length > 0
            || value.MilitaryTrainingId.Length > 0;
        bool hasCompleteEnemyOrigin = value.OriginEnemyArchetypeId.StartsWith("enemy:", StringComparison.Ordinal)
            && value.OriginFactionId.Length > 0
            && value.MilitaryTrainingId.StartsWith("training:", StringComparison.Ordinal);
        if (hasAnyEnemyOrigin != hasCompleteEnemyOrigin
            || float.IsNaN(data.loyalty)
            || float.IsInfinity(data.loyalty)
            || data.loyalty < 0f
            || data.loyalty > 100f)
            throw new InvalidOperationException($"'{data.characterId}' has an invalid enemy-origin record.");
        foreach (CharacterNarrativeEventSaveData resolved in value.recentEvents)
            catalog.Require(new NarrativeEventId(resolved.eventId));
        foreach (CulturalPracticeParticipationSaveData participation in
                 value.practiceParticipations.Values)
        {
            if (!catalog.Practices.Any(practice => string.Equals(
                    practice.StableId,
                    participation.practiceId,
                    StringComparison.Ordinal))
                || participation.lastAbsoluteDay < 0)
            {
                throw new InvalidOperationException(
                    $"'{data.characterId}' has an invalid cultural-practice history.");
            }
        }
        foreach (NarrativeSkillExperienceSaveData proficiency in
                 value.proficiencyById.Values)
        {
            CharacterProficiencyId proficiencyId = new(proficiency.proficiencyId);
            catalog.Require(proficiencyId);
            if (proficiency.currentMilliExperience < 0L
                || proficiency.currentMilliExperience
                    > ProficiencyProgressionRules.MasterCurrentCap
                || proficiency.lifetimeMilliExperience
                    < proficiency.currentMilliExperience
                || proficiency.lastPracticeAbsoluteHour < 0L
                || proficiency.lastDecaySettlementAbsoluteHour
                    < proficiency.lastPracticeAbsoluteHour)
            {
                throw new InvalidOperationException(
                    $"'{data.characterId}' has invalid proficiency state for '{proficiency.proficiencyId}'.");
            }
        }
        if (BuiltInCharacterProficiencyIds.All.Any(id =>
                !value.proficiencyById.ContainsKey(id.Value)))
        {
            throw new InvalidOperationException(
                $"'{data.characterId}' predates the nine-proficiency starting authority; new game required.");
        }
        return value;
    }

    public void StartAmbition(CharacterAmbitionId id, int absoluteDay)
    {
        if (!id.IsValid) throw new ArgumentException("A valid ambition id is required.", nameof(id));
        if (AmbitionStatus == CharacterAmbitionStatus.Active) throw new InvalidOperationException("A character cannot have two active ambitions.");
        if (absoluteDay < NextAmbitionAllowedAbsoluteDay) throw new InvalidOperationException("The ambition cooldown has not elapsed.");
        ActiveAmbitionId = id;
        AmbitionStatus = CharacterAmbitionStatus.Active;
        AmbitionProgress = 0;
    }

    public bool TryInitializeBackground(
        CharacterBackgroundDefinitionSO definition,
        int absoluteDay,
        out BackgroundInitializationOutcome outcome)
    {
        outcome = null;
        if (BackgroundInitialized)
        {
            return false;
        }
        if (definition == null
            || !string.Equals(
                definition.StableId,
                BackgroundId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Background initialization definition does not match the character.");
        }

        foreach (V20SkillBonus bonus in definition.startingSkills
                     ?? new List<V20SkillBonus>())
        {
            if (bonus == null || string.IsNullOrWhiteSpace(bonus.skillId))
            {
                continue;
            }
            string skillId = Normalize(bonus.skillId);
            CharacterProficiencyId proficiencyId = new(skillId);
            if (!proficiencyId.IsValid)
            {
                throw new InvalidOperationException(
                    $"Background '{definition.StableId}' uses removed skill id '{skillId}'.");
            }
            NarrativeSkillExperienceSaveData state = RequireProficiencyState(
                proficiencyId,
                absoluteDay <= 0
                    ? 0L
                    : (long)(absoluteDay - 1) * GameCalendarRules.HoursPerDay);
            long award = Math.Max(0, bonus.experience)
                * ProficiencyProgressionRules.MilliPerExperience;
            state.currentMilliExperience = Math.Min(
                ProficiencyProgressionRules.MasterCurrentCap,
                checked(state.currentMilliExperience + award));
            state.lifetimeMilliExperience = checked(
                state.lifetimeMilliExperience + award);
        }
        foreach (V20WeightedId reaction in definition.factionReactions
                     ?? new List<V20WeightedId>())
        {
            if (reaction == null || string.IsNullOrWhiteSpace(reaction.id))
            {
                continue;
            }
            backgroundFactionReactionById[Normalize(reaction.id)] =
                Math.Clamp((reaction.weight - 1f) * 5f, -10f, 45f);
        }
        InitialMemoryCode = Normalize(definition.initialMemoryCode);
        BackgroundInitialized = true;
        BackgroundInitializedAbsoluteDay = Math.Max(0, absoluteDay);
        outcome = new BackgroundInitializationOutcome
        {
            BackgroundId = BackgroundId,
            InitialMemoryCode = InitialMemoryCode,
            SkillExperienceById = CreateExperienceProjection(),
            FactionReactionById = new Dictionary<string, float>(
                backgroundFactionReactionById,
                StringComparer.Ordinal),
            StartingEffects = (definition.startingEffects
                    ?? new List<V20ContentEffect>())
                .Where(value => value != null && value.IsValid)
                .ToArray()
        };
        return true;
    }

    public void AddAmbitionProgress(int amount, int targetProgress, int absoluteDay)
    {
        if (AmbitionStatus != CharacterAmbitionStatus.Active) throw new InvalidOperationException("No active ambition can receive progress.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        AmbitionProgress = Math.Min(targetProgress, checked(AmbitionProgress + amount));
        if (AmbitionProgress >= targetProgress) CloseAmbition(CharacterAmbitionStatus.Completed, absoluteDay);
    }

    public void CloseAmbition(CharacterAmbitionStatus status, int absoluteDay)
    {
        if (AmbitionStatus != CharacterAmbitionStatus.Active) throw new InvalidOperationException("No active ambition can be closed.");
        if (status is not (CharacterAmbitionStatus.Completed or CharacterAmbitionStatus.Failed or CharacterAmbitionStatus.Abandoned))
            throw new ArgumentOutOfRangeException(nameof(status));
        AmbitionStatus = status;
        NextAmbitionAllowedAbsoluteDay = checked(absoluteDay + 10);
        ActiveAmbitionId = default;
        AmbitionProgress = 0;
    }

    public void BeginAssimilation(SpeciesCultureId target)
    {
        if (!target.IsValid || target.Equals(CultureId)) throw new InvalidOperationException("Assimilation requires a different valid culture.");
        AssimilationTargetCultureId = target;
        AssimilationDays = 0;
    }

    public void AdvanceAssimilationDay()
    {
        if (!AssimilationTargetCultureId.IsValid) return;
        AssimilationDays++;
        if (AssimilationDays < 120) return;
        CultureId = AssimilationTargetCultureId;
        AssimilationTargetCultureId = default;
        AssimilationDays = 0;
    }

    public bool CanPerformPractice(
        string practiceId,
        int absoluteDay,
        out int nextAllowedAbsoluteDay)
    {
        string id = Normalize(practiceId);
        nextAllowedAbsoluteDay = practiceParticipations.TryGetValue(
            id,
            out CulturalPracticeParticipationSaveData previous)
                ? checked(previous.lastAbsoluteDay + 10)
                : 0;
        return id.StartsWith("practice:", StringComparison.Ordinal)
            && absoluteDay >= nextAllowedAbsoluteDay;
    }

    public void RecordPracticeParticipation(
        string practiceId,
        SpeciesCultureId practiceCultureId,
        int requiredAssimilationDays,
        int absoluteDay)
    {
        if (!CanPerformPractice(
                practiceId,
                absoluteDay,
                out int nextAllowedAbsoluteDay))
        {
            throw new InvalidOperationException(
                $"Cultural practice is on cooldown until day {nextAllowedAbsoluteDay}.");
        }

        string id = Normalize(practiceId);
        practiceParticipations[id] = new CulturalPracticeParticipationSaveData
        {
            practiceId = id,
            lastAbsoluteDay = Math.Max(0, absoluteDay),
            performed = true
        };
        if (practiceCultureId.Equals(CultureId))
        {
            return;
        }
        if (!AssimilationTargetCultureId.Equals(practiceCultureId))
        {
            BeginAssimilation(practiceCultureId);
        }
        AssimilationDays++;
        if (AssimilationDays < Math.Max(1, requiredAssimilationDays))
        {
            return;
        }
        CultureId = AssimilationTargetCultureId;
        AssimilationTargetCultureId = default;
        AssimilationDays = 0;
    }

    public void RecordPracticeNeglect(
        string practiceId,
        int absoluteDay)
    {
        if (!CanPerformPractice(
                practiceId,
                absoluteDay,
                out int nextAllowedAbsoluteDay))
        {
            throw new InvalidOperationException(
                $"Cultural practice is on cooldown until day {nextAllowedAbsoluteDay}.");
        }

        string id = Normalize(practiceId);
        practiceParticipations[id] = new CulturalPracticeParticipationSaveData
        {
            practiceId = id,
            lastAbsoluteDay = Math.Max(0, absoluteDay),
            performed = false
        };
    }

    public void RecordEvent(
        LifeEventDefinitionSO definition,
        string choiceId,
        int absoluteDay,
        Func<string, LifeEventCategory> categoryResolver)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        recentEvents.Add(new CharacterNarrativeEventSaveData
        {
            eventId = definition.StableId,
            choiceId = choiceId?.Trim() ?? string.Empty,
            absoluteDay = absoluteDay
        });
        while (recentEvents.Count > RecentEventLimit)
        {
            CharacterNarrativeEventSaveData archived = recentEvents[0];
            recentEvents.RemoveAt(0);
            LifeEventCategory category = (categoryResolver
                ?? throw new ArgumentNullException(nameof(categoryResolver)))(archived.eventId);
            if (!summaries.TryGetValue(category, out CharacterNarrativeEventSummarySaveData summary))
            {
                summary = new CharacterNarrativeEventSummarySaveData { category = category };
                summaries.Add(category, summary);
            }
            summary.count++;
            summary.lastAbsoluteDay = Math.Max(summary.lastAbsoluteDay, archived.absoluteDay);
        }
    }

    public void MarkHeritableTraitsAnalyzed()
    {
        if (HeritableTraitsAnalyzed)
            throw new InvalidOperationException(
                "Heritable traits were already analyzed for this character.");
        HeritableTraitsAnalyzed = true;
    }

    public CharacterNarrativeSnapshot Snapshot() => new()
    {
        CharacterId = CharacterId,
        BackgroundId = BackgroundId,
        CultureId = CultureId,
        ActiveAmbitionId = ActiveAmbitionId,
        AmbitionStatus = AmbitionStatus,
        AmbitionProgress = AmbitionProgress,
        NextAmbitionAllowedAbsoluteDay = NextAmbitionAllowedAbsoluteDay,
        AssimilationTargetCultureId = AssimilationTargetCultureId,
        AssimilationDays = AssimilationDays,
        ExpressedHeritableTraitIds = expressedTraits.ToArray(),
        LatentHeritableTraitIds = latentTraits.ToArray(),
        VisibleLatentHeritableTraitIds = HeritableTraitsAnalyzed
            ? latentTraits.ToArray()
            : Array.Empty<string>(),
        HeritableTraitsAnalyzed = HeritableTraitsAnalyzed,
        RecentEvents = recentEvents.Select(Clone).ToArray(),
        EventSummaries = summaries.Values.OrderBy(value => value.category).Select(Clone).ToArray(),
        OriginEnemyArchetypeId = OriginEnemyArchetypeId,
        OriginFactionId = OriginFactionId,
        MilitaryTrainingId = MilitaryTrainingId,
        Loyalty = Loyalty,
        BackgroundInitialized = BackgroundInitialized,
        BackgroundInitializedAbsoluteDay = BackgroundInitializedAbsoluteDay,
        InitialMemoryCode = InitialMemoryCode,
        SkillExperienceById = CreateExperienceProjection(),
        Proficiencies = proficiencyById.Values
            .OrderBy(value => value.proficiencyId, StringComparer.Ordinal)
            .Select(CreateSnapshot)
            .ToArray(),
        BackgroundFactionReactionById = new Dictionary<string, float>(
            backgroundFactionReactionById,
            StringComparer.Ordinal),
        PracticeParticipations = practiceParticipations.Values
            .OrderBy(value => value.practiceId, StringComparer.Ordinal)
            .Select(Clone)
            .ToArray()
    };

    public CharacterNarrativeSaveData Capture() => new()
    {
        characterId = CharacterId.Value,
        phenotypeSpeciesId = PhenotypeSpeciesId.Value,
        backgroundId = BackgroundId.Value,
        cultureId = CultureId.Value,
        activeAmbitionId = ActiveAmbitionId.Value,
        ambitionStatus = AmbitionStatus,
        ambitionProgress = AmbitionProgress,
        nextAmbitionAllowedAbsoluteDay = NextAmbitionAllowedAbsoluteDay,
        assimilationTargetCultureId = AssimilationTargetCultureId.Value,
        assimilationDays = AssimilationDays,
        expressedHeritableTraitIds = expressedTraits.ToList(),
        latentHeritableTraitIds = latentTraits.ToList(),
        heritableTraitsAnalyzed = HeritableTraitsAnalyzed,
        recentEvents = recentEvents.Select(Clone).ToList(),
        eventSummaries = summaries.Values.OrderBy(value => value.category).Select(Clone).ToList(),
        originEnemyArchetypeId = OriginEnemyArchetypeId,
        originFactionId = OriginFactionId,
        militaryTrainingId = MilitaryTrainingId,
        loyalty = Loyalty,
        backgroundInitialized = BackgroundInitialized,
        backgroundInitializedAbsoluteDay = BackgroundInitializedAbsoluteDay,
        initialMemoryCode = InitialMemoryCode,
        skillExperience = proficiencyById
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => Clone(value.Value))
            .ToList(),
        backgroundFactionReactions = backgroundFactionReactionById
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new NarrativeFactionReactionSaveData
            {
                factionId = value.Key,
                reaction = value.Value
            })
            .ToList(),
        practiceParticipations = practiceParticipations.Values
            .OrderBy(value => value.practiceId, StringComparer.Ordinal)
            .Select(Clone)
            .ToList()
    };

    public bool TryGetProficiency(
        CharacterProficiencyId proficiencyId,
        long absoluteHour,
        out CharacterProficiencySnapshot snapshot)
    {
        if (!proficiencyId.IsValid)
        {
            snapshot = default;
            return false;
        }

        NarrativeSkillExperienceSaveData state = RequireProficiencyState(
            proficiencyId,
            absoluteHour);
        SettleProficiency(state, absoluteHour);
        snapshot = CreateSnapshot(state);
        return true;
    }

    public IReadOnlyList<CharacterProficiencySnapshot> GetAllProficiencies(
        IReadOnlyList<ProficiencyDefinitionSO> definitions,
        long absoluteHour)
    {
        List<CharacterProficiencySnapshot> snapshots = new(
            definitions?.Count ?? 0);
        foreach (ProficiencyDefinitionSO definition in definitions
                     ?? Array.Empty<ProficiencyDefinitionSO>())
        {
            NarrativeSkillExperienceSaveData state = RequireProficiencyState(
                definition.ProficiencyId,
                absoluteHour);
            SettleProficiency(state, absoluteHour);
            snapshots.Add(CreateSnapshot(state));
        }
        return snapshots;
    }

    public long AddApprovedWork(
        ProficiencyWorkProfile profile,
        float approvedWork,
        float difficultyMultiplier,
        ProficiencyWorkOutcome outcome,
        float learningMultiplier,
        float repetitionMultiplier,
        long absoluteHour)
    {
        long totalAward = 0L;
        totalAward += AddAward(
            profile.Primary,
            ProficiencyProgressionRules.CalculateWorkAwardMilli(
                approvedWork * profile.PrimaryWeight,
                difficultyMultiplier,
                outcome,
                learningMultiplier * ResolveLearningMultiplier(profile.Primary),
                repetitionMultiplier),
            absoluteHour);
        if (profile.Secondary.IsValid && profile.SecondaryWeight > 0f)
        {
            totalAward += AddAward(
                profile.Secondary,
                ProficiencyProgressionRules.CalculateWorkAwardMilli(
                    approvedWork * profile.SecondaryWeight,
                    difficultyMultiplier,
                    outcome,
                    learningMultiplier * ResolveLearningMultiplier(
                        profile.Secondary),
                    repetitionMultiplier),
                absoluteHour);
        }
        return totalAward;
    }

    public long AddDirectExperience(
        CharacterProficiencyId proficiencyId,
        float experience,
        long absoluteHour,
        bool applyLearningMultiplier = true)
    {
        if (experience <= 0f) return 0L;
        float multiplier = applyLearningMultiplier
            ? ResolveLearningMultiplier(proficiencyId)
            : CharacterProficiencySpecializationRules
                .NeutralLearningMultiplier;
        long award = Math.Max(0L, checked((long)Math.Round(
            experience * multiplier
                * ProficiencyProgressionRules.MilliPerExperience,
            MidpointRounding.AwayFromZero)));
        return AddAward(proficiencyId, award, absoluteHour);
    }

    public long AddCombatExperience(
        CharacterProficiencyId proficiencyId,
        float experience,
        bool training,
        string stableAwardKey,
        long absoluteHour)
    {
        if (experience <= 0f || !proficiencyId.IsValid
            || (proficiencyId != BuiltInCharacterProficiencyIds.MeleeCombat
                && proficiencyId != BuiltInCharacterProficiencyIds.RangedCombat))
        {
            return 0L;
        }

        NarrativeSkillExperienceSaveData state = RequireProficiencyState(
            proficiencyId,
            absoluteHour);
        SettleProficiency(state, absoluteHour);
        string awardKey = Normalize(stableAwardKey);
        state.recentCombatAwardKeys ??= new List<string>();
        if (awardKey.Length > 0
            && state.recentCombatAwardKeys.Contains(
                awardKey,
                StringComparer.Ordinal))
        {
            return 0L;
        }

        int day = checked((int)Math.Min(
            int.MaxValue,
            Math.Max(0L, absoluteHour) / GameCalendarRules.HoursPerDay));
        if (state.combatAwardAbsoluteDay != day)
        {
            state.combatAwardAbsoluteDay = day;
            state.combatAwardMilliToday = 0L;
            state.trainingAwardMilliToday = 0L;
            state.recentCombatAwardKeys.Clear();
        }

        long requested = Math.Max(0L, checked((long)Math.Round(
            experience * ResolveLearningMultiplier(proficiencyId)
                * ProficiencyProgressionRules.MilliPerExperience,
            MidpointRounding.AwayFromZero)));
        long totalRemaining = Math.Max(
            0L,
            8L * ProficiencyProgressionRules.MilliPerExperience
                - state.combatAwardMilliToday);
        long trainingRemaining = training
            ? Math.Max(
                0L,
                2L * ProficiencyProgressionRules.MilliPerExperience
                    - state.trainingAwardMilliToday)
            : long.MaxValue;
        long approved = Math.Min(requested, Math.Min(totalRemaining, trainingRemaining));
        if (awardKey.Length > 0)
        {
            state.recentCombatAwardKeys.Add(awardKey);
            if (state.recentCombatAwardKeys.Count > 64)
            {
                state.recentCombatAwardKeys.RemoveRange(
                    0,
                    state.recentCombatAwardKeys.Count - 64);
            }
        }
        if (approved <= 0L) return 0L;

        state.combatAwardMilliToday += approved;
        if (training) state.trainingAwardMilliToday += approved;
        return AddAward(proficiencyId, approved, absoluteHour);
    }

    public void RecordPractice(
        CharacterProficiencyId proficiencyId,
        long absoluteHour)
    {
        NarrativeSkillExperienceSaveData state = RequireProficiencyState(
            proficiencyId,
            absoluteHour);
        SettleProficiency(state, absoluteHour);
        state.maintenancePracticeMilliExperience = 0L;
        state.lastPracticeAbsoluteHour = Math.Max(0L, absoluteHour);
        state.lastDecaySettlementAbsoluteHour = Math.Max(
            state.lastDecaySettlementAbsoluteHour,
            state.lastPracticeAbsoluteHour);
    }

    private long AddAward(
        CharacterProficiencyId proficiencyId,
        long awardMilli,
        long absoluteHour)
    {
        if (!proficiencyId.IsValid || awardMilli <= 0L) return 0L;
        NarrativeSkillExperienceSaveData state = RequireProficiencyState(
            proficiencyId,
            absoluteHour);
        SettleProficiency(state, absoluteHour);
        long before = state.currentMilliExperience;
        state.currentMilliExperience = Math.Min(
            ProficiencyProgressionRules.MasterCurrentCap,
            checked(state.currentMilliExperience + awardMilli));
        state.lifetimeMilliExperience = checked(
            state.lifetimeMilliExperience + awardMilli);
        int absoluteDay = checked((int)Math.Min(
            int.MaxValue,
            Math.Max(0L, absoluteHour) / GameCalendarRules.HoursPerDay));
        if (state.practiceAbsoluteDay != absoluteDay)
        {
            state.practiceAbsoluteDay = absoluteDay;
            state.practiceMilliExperienceToday = 0L;
        }
        state.practiceMilliExperienceToday = checked(
            state.practiceMilliExperienceToday + awardMilli);
        state.maintenancePracticeMilliExperience = checked(
            state.maintenancePracticeMilliExperience + awardMilli);
        if (state.maintenancePracticeMilliExperience
            >= ProficiencyProgressionRules.MilliPerExperience)
        {
            state.maintenancePracticeMilliExperience = 0L;
            state.lastPracticeAbsoluteHour = Math.Max(0L, absoluteHour);
            state.lastDecaySettlementAbsoluteHour = Math.Max(
                state.lastDecaySettlementAbsoluteHour,
                state.lastPracticeAbsoluteHour);
        }
        return state.currentMilliExperience - before;
    }

    private NarrativeSkillExperienceSaveData RequireProficiencyState(
        CharacterProficiencyId proficiencyId,
        long absoluteHour)
    {
        if (!proficiencyId.IsValid)
        {
            throw new ArgumentException(
                "A valid proficiency id is required.",
                nameof(proficiencyId));
        }
        if (proficiencyById.TryGetValue(
                proficiencyId.Value,
                out NarrativeSkillExperienceSaveData state))
        {
            return state;
        }

        long now = Math.Max(0L, absoluteHour);
        state = new NarrativeSkillExperienceSaveData
        {
            proficiencyId = proficiencyId.Value,
            learningMultiplier = CharacterProficiencySpecializationRules
                .NeutralLearningMultiplier,
            lastPracticeAbsoluteHour = now,
            lastDecaySettlementAbsoluteHour = now
        };
        proficiencyById.Add(proficiencyId.Value, state);
        return state;
    }

    private static void SettleProficiency(
        NarrativeSkillExperienceSaveData state,
        long absoluteHour)
    {
        long now = Math.Max(
            state.lastDecaySettlementAbsoluteHour,
            absoluteHour);
        state.currentMilliExperience = ProficiencyProgressionRules.SettleDecay(
            state.currentMilliExperience,
            state.lastPracticeAbsoluteHour,
            state.lastDecaySettlementAbsoluteHour,
            now);
        state.lastDecaySettlementAbsoluteHour = now;
    }

    private Dictionary<string, int> CreateExperienceProjection() =>
        proficiencyById.ToDictionary(
            value => value.Key,
            value => checked((int)Math.Min(
                int.MaxValue,
                value.Value.currentMilliExperience
                    / ProficiencyProgressionRules.MilliPerExperience)),
            StringComparer.Ordinal);

    private float ResolveLearningMultiplier(
        CharacterProficiencyId proficiencyId) => proficiencyById.TryGetValue(
            proficiencyId.Value,
            out NarrativeSkillExperienceSaveData state)
        ? CharacterProficiencySpecializationRules
            .NormalizeSerializedMultiplier(state.learningMultiplier)
        : CharacterProficiencySpecializationRules.NeutralLearningMultiplier;

    private static CharacterProficiencySnapshot CreateSnapshot(
        NarrativeSkillExperienceSaveData state) => new(
            new CharacterProficiencyId(state.proficiencyId),
            state.currentMilliExperience,
            state.lifetimeMilliExperience,
            state.lastPracticeAbsoluteHour,
            state.lastDecaySettlementAbsoluteHour,
            state.practiceMilliExperienceToday,
            state.learningMultiplier);

    private static NarrativeSkillExperienceSaveData Clone(
        NarrativeSkillExperienceSaveData source) => new()
    {
        proficiencyId = source.proficiencyId,
        learningMultiplier = CharacterProficiencySpecializationRules
            .NormalizeSerializedMultiplier(source.learningMultiplier),
        currentMilliExperience = source.currentMilliExperience,
        lifetimeMilliExperience = source.lifetimeMilliExperience,
        lastPracticeAbsoluteHour = source.lastPracticeAbsoluteHour,
        lastDecaySettlementAbsoluteHour = source.lastDecaySettlementAbsoluteHour,
        maintenancePracticeMilliExperience =
            source.maintenancePracticeMilliExperience,
        practiceAbsoluteDay = source.practiceAbsoluteDay,
        practiceMilliExperienceToday = source.practiceMilliExperienceToday,
        combatAwardAbsoluteDay = source.combatAwardAbsoluteDay,
        combatAwardMilliToday = source.combatAwardMilliToday,
        trainingAwardMilliToday = source.trainingAwardMilliToday,
        recentCombatAwardKeys = new List<string>(
            source.recentCombatAwardKeys ?? new List<string>())
    };

    private static string[] RequireTraits(IReadOnlyList<string> source, int maximum, string label)
    {
        string[] values = (source ?? Array.Empty<string>()).Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        if (values.Length > maximum) throw new InvalidOperationException($"Too many {label} heritable traits; maximum is {maximum}.");
        return values;
    }

    private static string Normalize(string value) => value?.Trim() ?? string.Empty;

    private static CharacterNarrativeEventSaveData Clone(CharacterNarrativeEventSaveData value) => new()
    {
        eventId = value?.eventId ?? string.Empty,
        choiceId = value?.choiceId ?? string.Empty,
        absoluteDay = value?.absoluteDay ?? 0
    };

    private static CharacterNarrativeEventSummarySaveData Clone(CharacterNarrativeEventSummarySaveData value) => new()
    {
        category = value?.category ?? default,
        count = value?.count ?? 0,
        lastAbsoluteDay = value?.lastAbsoluteDay ?? 0
    };

    private static CulturalPracticeParticipationSaveData Clone(
        CulturalPracticeParticipationSaveData value) => new()
    {
        practiceId = value?.practiceId ?? string.Empty,
        lastAbsoluteDay = value?.lastAbsoluteDay ?? 0,
        performed = value?.performed ?? false
    };
}

public sealed class CharacterNarrativeAggregateState
{
    internal Dictionary<CharacterId, CharacterNarrativeRecord> Characters { get; } = new();
    internal List<CharacterIdentityRuntimeStateSaveData> IdentityStates { get; } = new();

    public static CharacterNarrativeAggregateState Restore(CharacterNarrativeWorldSaveData data, ICharacterNarrativeCatalog catalog)
    {
        if (data == null || data.version != CharacterNarrativeWorldSaveData.CurrentVersion)
            throw new InvalidOperationException("Character narrative save version is invalid.");
        if (data.characters == null) throw new InvalidOperationException("Character narrative collection is required.");
        CharacterNarrativeAggregateState state = new();
        foreach (CharacterNarrativeSaveData recordData in data.characters)
        {
            CharacterNarrativeRecord record = CharacterNarrativeRecord.Restore(recordData, catalog);
            if (!state.Characters.TryAdd(record.CharacterId, record))
                throw new InvalidOperationException($"Duplicate narrative character '{record.CharacterId.Value}'.");
        }
        state.IdentityStates.AddRange((data.identityStates
                ?? new List<CharacterIdentityRuntimeStateSaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone()));
        return state;
    }

    public CharacterNarrativeWorldSaveData Capture() => new()
    {
        characters = Characters.Values.OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
            .Select(value => value.Capture()).ToList(),
        identityStates = IdentityStates.Select(value => value.Clone()).ToList()
    };
}
