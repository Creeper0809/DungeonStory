using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

public enum CharacterAmbitionStatus
{
    None,
    Active,
    Completed,
    Failed,
    Abandoned
}

public interface ICharacterNarrativeCatalog
{
    IReadOnlyList<CharacterBackgroundDefinitionSO> Backgrounds { get; }
    IReadOnlyList<CharacterAmbitionDefinitionSO> Ambitions { get; }
    IReadOnlyList<LifeEventDefinitionSO> LifeEvents { get; }
    IReadOnlyList<SpeciesCultureDefinitionSO> Cultures { get; }
    IReadOnlyList<CulturalPracticeDefinitionSO> Practices { get; }
    IReadOnlyList<HeritableTraitDefinitionSO> HeritableTraits { get; }
    IReadOnlyList<ProficiencyDefinitionSO> Proficiencies { get; }
    CharacterBackgroundDefinitionSO Require(CharacterBackgroundId id);
    CharacterAmbitionDefinitionSO Require(CharacterAmbitionId id);
    LifeEventDefinitionSO Require(NarrativeEventId id);
    SpeciesCultureDefinitionSO Require(SpeciesCultureId id);
    SpeciesCultureDefinitionSO RequireDefaultCulture(string speciesId);
    HeritableTraitDefinitionSO RequireHeritable(string traitId);
    ProficiencyDefinitionSO Require(CharacterProficiencyId id);
}

public interface ICharacterNarrativeQuery
{
    int Version { get; }
    IReadOnlyCollection<CharacterNarrativeSnapshot> All { get; }
    bool TryGet(CharacterId characterId, out CharacterNarrativeSnapshot snapshot);
    bool CanPerformPractice(
        CharacterId characterId,
        string practiceId,
        int absoluteDay,
        out int nextAllowedAbsoluteDay);
    bool TryPreviewAmbitionProgress(
        CharacterId characterId,
        int amount,
        out AmbitionProgressPreview preview);
}

public readonly struct AmbitionProgressPreview
{
    public AmbitionProgressPreview(
        CharacterAmbitionId ambitionId,
        int currentProgress,
        int targetProgress,
        bool completes,
        IReadOnlyList<V20ContentEffect> completionRewards)
    {
        AmbitionId = ambitionId;
        CurrentProgress = currentProgress;
        TargetProgress = targetProgress;
        Completes = completes;
        CompletionRewards = completionRewards ?? Array.Empty<V20ContentEffect>();
    }

    public CharacterAmbitionId AmbitionId { get; }
    public int CurrentProgress { get; }
    public int TargetProgress { get; }
    public bool Completes { get; }
    public IReadOnlyList<V20ContentEffect> CompletionRewards { get; }
}

public interface ICharacterNarrativeCommand
{
    CharacterNarrativeSnapshot Register(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        IReadOnlyList<string> expressedHeritableTraitIds,
        IReadOnlyList<string> latentHeritableTraitIds,
        IReadOnlyList<CharacterStartingProficiencyExperience>
            startingProficiencies = null);
    CharacterNarrativeSnapshot RegisterEnemyOrigin(
        CharacterId characterId,
        CharacterSpeciesId phenotypeSpeciesId,
        IReadOnlyList<string> expressedHeritableTraitIds,
        IReadOnlyList<string> latentHeritableTraitIds,
        CharacterBackgroundId backgroundId,
        SpeciesCultureId cultureId,
        string enemyArchetypeId,
        string originFactionId,
        string militaryTrainingId,
        float loyalty,
        IReadOnlyList<CharacterStartingProficiencyExperience>
            startingProficiencies = null);
    bool TryInitializeBackground(
        CharacterId characterId,
        int absoluteDay,
        out BackgroundInitializationOutcome outcome);
    void StartAmbition(CharacterId characterId, CharacterAmbitionId ambitionId, int absoluteDay);
    void AddAmbitionProgress(CharacterId characterId, int amount, int absoluteDay);
    void FailAmbition(CharacterId characterId, int absoluteDay);
    void AbandonAmbition(CharacterId characterId, int absoluteDay);
    void BeginAssimilation(CharacterId characterId, SpeciesCultureId targetCultureId);
    void AdvanceAssimilationDay(CharacterId characterId);
    void RecordPracticeParticipation(
        CharacterId characterId,
        string practiceId,
        SpeciesCultureId practiceCultureId,
        int assimilationDays,
        int absoluteDay);
    void RecordPracticeNeglect(
        CharacterId characterId,
        string practiceId,
        int absoluteDay);
    void RecordResolvedEvent(
        CharacterId characterId,
        NarrativeEventId eventId,
        string choiceId,
        int absoluteDay);
    void MarkHeritableTraitsAnalyzed(CharacterId characterId);
}

public interface ITraitAnalysisCommand
{
    bool TryAnalyze(
        CharacterId characterId,
        out IReadOnlyList<string> revealedLatentTraitIds,
        out DomainFailure failure);
}

public sealed class BackgroundInitializationOutcome
{
    public CharacterBackgroundId BackgroundId { get; internal set; }
    public string InitialMemoryCode { get; internal set; } = string.Empty;
    public IReadOnlyDictionary<string, int> SkillExperienceById
        { get; internal set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, float> FactionReactionById
        { get; internal set; } = new Dictionary<string, float>();
    public IReadOnlyList<V20ContentEffect> StartingEffects
        { get; internal set; } = Array.Empty<V20ContentEffect>();
}

public interface ICharacterNarrativePersistence
{
    CharacterNarrativeWorldSaveData Capture();
    CharacterNarrativeAggregateState PrepareRestore(CharacterNarrativeWorldSaveData data);
    void PublishRestore(CharacterNarrativeAggregateState candidate);
}

public sealed class CharacterNarrativeSnapshot
{
    public CharacterId CharacterId { get; internal set; }
    public CharacterBackgroundId BackgroundId { get; internal set; }
    public SpeciesCultureId CultureId { get; internal set; }
    public CharacterAmbitionId ActiveAmbitionId { get; internal set; }
    public CharacterAmbitionStatus AmbitionStatus { get; internal set; }
    public int AmbitionStartedAbsoluteDay { get; internal set; }
    public int AmbitionDueAbsoluteDay { get; internal set; }
    public int AmbitionProgress { get; internal set; }
    public int NextAmbitionAllowedAbsoluteDay { get; internal set; }
    public SpeciesCultureId AssimilationTargetCultureId { get; internal set; }
    public int AssimilationDays { get; internal set; }
    public IReadOnlyList<string> ExpressedHeritableTraitIds { get; internal set; }
    public IReadOnlyList<string> LatentHeritableTraitIds { get; internal set; }
    public IReadOnlyList<string> VisibleLatentHeritableTraitIds
        { get; internal set; }
    public bool HeritableTraitsAnalyzed { get; internal set; }
    public IReadOnlyList<CharacterNarrativeEventSaveData> RecentEvents { get; internal set; }
    public IReadOnlyList<CharacterNarrativeEventSummarySaveData> EventSummaries { get; internal set; }
    public string OriginEnemyArchetypeId { get; internal set; }
    public string OriginFactionId { get; internal set; }
    public string MilitaryTrainingId { get; internal set; }
    public float Loyalty { get; internal set; }
    public bool BackgroundInitialized { get; internal set; }
    public int BackgroundInitializedAbsoluteDay { get; internal set; }
    public string InitialMemoryCode { get; internal set; }
    public IReadOnlyDictionary<string, int> SkillExperienceById
        { get; internal set; }
    public IReadOnlyList<CharacterProficiencySnapshot> Proficiencies
        { get; internal set; }
    public IReadOnlyDictionary<string, float> BackgroundFactionReactionById
        { get; internal set; }
    public IReadOnlyList<CulturalPracticeParticipationSaveData>
        PracticeParticipations { get; internal set; }
    public bool HasEnemyOrigin => !string.IsNullOrWhiteSpace(OriginEnemyArchetypeId);
}

public static class BackgroundFactionReactionRules
{
    private static readonly IReadOnlyDictionary<string, string> LegacyFactionAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["faction:human-crown"] = "human:crown",
            ["faction:human-legion"] = "human:legion",
            ["faction:merchant-league"] = "human:merchant",
            ["faction:free-settlers"] = "human:settler",
            ["faction:truth-keepers"] = "human:inquisition",
            ["faction:archive-conclave"] = "truth:guardian",
            // Historical origin labels retain their political/trade meaning in
            // the active enemy faction namespace. Stored background rows and
            // already generated loyalty values are not rewritten on restore.
            ["faction:old-nobility"] = "human:crown",
            ["faction:iron-consortium"] = "human:merchant"
        };
    public static IEnumerable<string> LegacyAliases => LegacyFactionAliases.Keys;
    public static float GetMultiplier(
        CharacterBackgroundDefinitionSO background,
        string gameplayFactionId)
    {
        if (background == null || string.IsNullOrWhiteSpace(gameplayFactionId))
        {
            return 1f;
        }

        string target = CanonicalFactionId(gameplayFactionId);
        V20WeightedId reaction = (background.factionReactions
                ?? new List<V20WeightedId>())
            .FirstOrDefault(value => value != null
                && string.Equals(
                    CanonicalFactionId(value.id),
                    target,
                    StringComparison.Ordinal));
        return reaction == null ? 1f : UnityEngine.Mathf.Clamp(reaction.weight, 0.1f, 10f);
    }

    public static float ApplyToLoyalty(float baseLoyalty, float multiplier)
    {
        return UnityEngine.Mathf.Clamp(
            baseLoyalty + ((UnityEngine.Mathf.Clamp(multiplier, 0.1f, 10f) - 1f) * 12f),
            0f,
            100f);
    }

    public static string CanonicalFactionId(string id)
    {
        string normalized = (id ?? string.Empty).Trim().ToLowerInvariant();
        if (LegacyFactionAliases.TryGetValue(normalized, out string canonical)) return canonical;
        return normalized.StartsWith("faction:", StringComparison.Ordinal)
            ? normalized.Substring("faction:".Length) : normalized;
    }
}

[Serializable]
public sealed class CharacterNarrativeWorldSaveData
{
    public const int CurrentVersion = 14;
    public int version = CurrentVersion;
    public List<CharacterNarrativeSaveData> characters = new();
    public List<CharacterIdentityRuntimeStateSaveData> identityStates = new();
    public List<WorkCompletionIdentityDeliveryCursorSaveData>
        workCompletionDeliveries = new();
    public int nextTraitAnalysisOperationSequence;
    public TraitAnalysisCommitSaveData pendingTraitAnalysis = new();
    public List<CharacterAmbitionTransitionObservationSaveData> ambitionTransitionObservations = new();
}

public enum TraitAnalysisCommitPhase
{
    None = 0,
    IntentRecorded = 1,
    RevealPublished = 2,
    OutcomePublished = 3
}

[Serializable]
public sealed class TraitAnalysisCommitSaveData
{
    public int phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string characterId = string.Empty;
    public string characterDisplayName = string.Empty;
    public string facilityInstanceId = string.Empty;
    public string facilityDefinitionId = string.Empty;
    public string facilityDisplayName = string.Empty;
    public string itemId = string.Empty;
    public string sourceStackId = string.Empty;
    public int absoluteDay;
    public List<string> latentTraitIds = new();
    public string physicalCommitId = string.Empty;
    public List<string> physicalSourceStackIds = new();
    public long physicalInputMassGrams;
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string publicationFingerprint = string.Empty;

    public HeritableTraitsAnalyzedFact CreateCompletedFact() => new(new HeritableTraitsAnalyzedFactData
    {
        operationId = operationId, operationSequence = operationSequence,
        characterId = characterId, characterDisplayName = characterDisplayName,
        facilityInstanceId = facilityInstanceId, facilityDefinitionId = facilityDefinitionId,
        kitItemId = itemId, physicalCommitId = physicalCommitId,
        sourceStackIds = physicalSourceStackIds?.ToArray(), inputMassGrams = physicalInputMassGrams,
        latentTraitIds = latentTraitIds?.ToArray(), absoluteDay = absoluteDay
    });

    public void ValidateObservationState()
    {
        bool Text(string value) => DomainFactValueSyntax.IsText(value, 4096, true)
            && value == value.Trim();
        if (!Text(publicationKey) || !Text(publicationId) || !Text(publicationFingerprint))
            throw new InvalidOperationException("Trait-analysis observation metadata is invalid.");
        bool key = publicationKey.Length > 0;
        bool proof = publicationId.Length > 0;
        if (proof != (publicationFingerprint.Length > 0)
            || proof && !key
            || phase < (int)TraitAnalysisCommitPhase.RevealPublished && (key || proof)
            || phase == (int)TraitAnalysisCommitPhase.OutcomePublished && (!key || !proof))
            throw new InvalidOperationException("Trait-analysis observation phase is inconsistent.");
    }

    public TraitAnalysisCommitSaveData Clone() => new()
    {
        phase = phase,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        characterId = characterId ?? string.Empty,
        characterDisplayName = characterDisplayName ?? string.Empty,
        facilityInstanceId = facilityInstanceId ?? string.Empty,
        facilityDefinitionId = facilityDefinitionId ?? string.Empty,
        facilityDisplayName = facilityDisplayName ?? string.Empty,
        itemId = itemId ?? string.Empty,
        sourceStackId = sourceStackId ?? string.Empty,
        absoluteDay = absoluteDay,
        latentTraitIds = (latentTraitIds ?? new List<string>()).ToList(),
        physicalCommitId = physicalCommitId ?? string.Empty,
        physicalSourceStackIds = (physicalSourceStackIds
                ?? new List<string>())
            .ToList(),
        physicalInputMassGrams = physicalInputMassGrams,
        publicationKey = publicationKey,
        publicationId = publicationId,
        publicationFingerprint = publicationFingerprint
    };
}

[Serializable]
public sealed class WorkCompletionIdentityDeliveryCursorSaveData
{
    public string producerStreamId = string.Empty;
    public int operationSequence;
    public string deliveryId = string.Empty;
    public string payloadFingerprint = string.Empty;
    public WorkCompletionIdentityDeliveryDisposition disposition;
    public bool notificationAttempted;
    public WorkIdentityAppliedFactData observation = new();
    public string publicationKey = string.Empty, publicationId = string.Empty, publicationFingerprint = string.Empty;

    public WorkCompletionIdentityDeliveryCursorSaveData Clone() => new()
    {
        producerStreamId = producerStreamId ?? string.Empty,
        operationSequence = operationSequence,
        deliveryId = deliveryId ?? string.Empty,
        payloadFingerprint = payloadFingerprint ?? string.Empty,
        disposition = disposition,
        notificationAttempted = notificationAttempted,
        observation = observation?.Clone(),
        publicationKey = publicationKey,
        publicationId = publicationId,
        publicationFingerprint = publicationFingerprint
    };
}

public enum WorkCompletionIdentityDeliveryDisposition
{
    EffectsApplied = 0,
    TerminalRecipientUnavailable = 1,
    EffectsAppliedWithOutcome = 2,
    EffectsAppliedPendingObservation = 3
}

[Serializable]
public sealed class CharacterNarrativeSaveData
{
    public string characterId = string.Empty;
    public string phenotypeSpeciesId = string.Empty;
    public string backgroundId = string.Empty;
    public string cultureId = string.Empty;
    public string activeAmbitionId = string.Empty;
    public CharacterAmbitionStatus ambitionStatus;
    public int ambitionStartedAbsoluteDay;
    public int ambitionDueAbsoluteDay;
    public int ambitionProgress;
    public int nextAmbitionAllowedAbsoluteDay;
    public string assimilationTargetCultureId = string.Empty;
    public int assimilationDays;
    public List<string> expressedHeritableTraitIds = new();
    public List<string> latentHeritableTraitIds = new();
    public bool heritableTraitsAnalyzed;
    public List<CharacterNarrativeEventSaveData> recentEvents = new();
    public List<CharacterNarrativeEventSummarySaveData> eventSummaries = new();
    public string originEnemyArchetypeId = string.Empty;
    public string originFactionId = string.Empty;
    public string militaryTrainingId = string.Empty;
    public float loyalty;
    public bool backgroundInitialized;
    public int backgroundInitializedAbsoluteDay;
    public string initialMemoryCode = string.Empty;
    public List<NarrativeSkillExperienceSaveData> skillExperience = new();
    public List<NarrativeFactionReactionSaveData> backgroundFactionReactions =
        new();
    public List<CulturalPracticeParticipationSaveData> practiceParticipations =
        new();
}

[Serializable]
public sealed class NarrativeSkillExperienceSaveData
{
    public string proficiencyId = string.Empty;
    public float learningMultiplier =
        CharacterProficiencySpecializationRules.NeutralLearningMultiplier;
    public long currentMilliExperience;
    public long lifetimeMilliExperience;
    public long lastPracticeAbsoluteHour;
    public long lastDecaySettlementAbsoluteHour;
    public long maintenancePracticeMilliExperience;
    public int practiceAbsoluteDay = -1;
    public long practiceMilliExperienceToday;
    public int combatAwardAbsoluteDay = -1;
    public long combatAwardMilliToday;
    public long trainingAwardMilliToday;
    public List<string> recentCombatAwardKeys = new();
}

[Serializable]
public sealed class NarrativeFactionReactionSaveData
{
    public string factionId = string.Empty;
    public float reaction;
}

[Serializable]
public sealed class CulturalPracticeParticipationSaveData
{
    public string practiceId = string.Empty;
    public int lastAbsoluteDay;
    public bool performed;
}

[Serializable]
public sealed class CharacterNarrativeEventSaveData
{
    public string eventId = string.Empty;
    public string choiceId = string.Empty;
    public int absoluteDay;
}

[Serializable]
public sealed class CharacterNarrativeEventSummarySaveData
{
    public LifeEventCategory category;
    public int count;
    public int lastAbsoluteDay;
}
