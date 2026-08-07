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
        recentEvents = (data.recentEvents ?? new()).Select(Clone).OrderBy(value => value.absoluteDay).ToList();
        summaries = (data.eventSummaries ?? new()).ToDictionary(value => value.category, Clone);
        OriginEnemyArchetypeId = Normalize(data.originEnemyArchetypeId);
        OriginFactionId = Normalize(data.originFactionId);
        MilitaryTrainingId = Normalize(data.militaryTrainingId);
        Loyalty = Math.Max(0f, Math.Min(100f, data.loyalty));
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
    public string OriginEnemyArchetypeId { get; }
    public string OriginFactionId { get; }
    public string MilitaryTrainingId { get; }
    public float Loyalty { get; private set; }

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
        float loyalty = 0f)
    {
        if (!characterId.IsValid) throw new ArgumentException("A valid CharacterId is required.", nameof(characterId));
        if (!backgroundId.IsValid || !cultureId.IsValid) throw new InvalidOperationException("Narrative registration requires valid definition ids.");
        string[] expressedIds = RequireTraits(expressed, 4, "expressed");
        string[] latentIds = RequireTraits(latent, 2, "latent");
        if (expressedIds.Intersect(latentIds, StringComparer.Ordinal).Any())
            throw new InvalidOperationException("A heritable trait cannot be both expressed and latent.");
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
            loyalty = Math.Max(0f, Math.Min(100f, loyalty))
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
        RecentEvents = recentEvents.Select(Clone).ToArray(),
        EventSummaries = summaries.Values.OrderBy(value => value.category).Select(Clone).ToArray(),
        OriginEnemyArchetypeId = OriginEnemyArchetypeId,
        OriginFactionId = OriginFactionId,
        MilitaryTrainingId = MilitaryTrainingId,
        Loyalty = Loyalty
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
        recentEvents = recentEvents.Select(Clone).ToList(),
        eventSummaries = summaries.Values.OrderBy(value => value.category).Select(Clone).ToList(),
        originEnemyArchetypeId = OriginEnemyArchetypeId,
        originFactionId = OriginFactionId,
        militaryTrainingId = MilitaryTrainingId,
        loyalty = Loyalty
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
}

public sealed class CharacterNarrativeAggregateState
{
    internal Dictionary<CharacterId, CharacterNarrativeRecord> Characters { get; } = new();

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
        return state;
    }

    public CharacterNarrativeWorldSaveData Capture() => new()
    {
        characters = Characters.Values.OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
            .Select(value => value.Capture()).ToList()
    };
}
