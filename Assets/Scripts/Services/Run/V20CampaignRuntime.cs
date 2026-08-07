using System;
using System.Collections.Generic;
using System.Linq;

public enum RunProgressionPhase
{
    Founding,
    LegacyAge,
    EndlessAge
}

[Serializable]
public sealed class V20ActiveEventSaveData
{
    public string instanceId = string.Empty;
    public string definitionId = string.Empty;
    public int startedAbsoluteDay;
    public int deadlineAbsoluteDay;
    public int generation;
    public string selectedChoiceId = string.Empty;
    public bool resolved;
    public uint deterministicRoll;
    public string resolutionId = string.Empty;
    public string contextFactionId = string.Empty;
    public List<string> participantCharacterIds = new();
}

[Serializable]
public sealed class V20EventCooldownSaveData
{
    public string definitionId = string.Empty;
    public int availableAbsoluteDay;
}

[Serializable]
public sealed class SeasonalEventWorldSaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<V20ActiveEventSaveData> activeEvents = new();
    public List<string> completedEventIds = new();
    public int cycle;
    public int lastEvaluationAbsoluteDay = -1;
}

[Serializable]
public sealed class SocietyEventWorldSaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<V20ActiveEventSaveData> activeEvents = new();
    public List<V20ActiveEventSaveData> recentResolvedEvents = new();
    public List<string> completedOnceEventIds = new();
    public List<string> recurrenceKeys = new();
    public List<V20EventCooldownSaveData> cooldowns = new();
    public int lastEvaluationAbsoluteDay = -1;
}

[Serializable]
public sealed class FactionCampaignStateSaveData
{
    public string factionId = string.Empty;
    public int rapport;
    public int grievance;
    public int obligationTokens;
    public int currentChapter;
    public string activeContractId = string.Empty;
    public int activeContractDeadlineAbsoluteDay;
    public List<string> completedContractIds = new();
    public List<string> failedContractIds = new();
    public List<string> majorChoiceFlags = new();
}

[Serializable]
public sealed class FactionCampaignWorldSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<FactionCampaignStateSaveData> factions = new();
}

[Serializable]
public sealed class RunMilestoneWorldSaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public RunProgressionPhase phase;
    public int endlessCycle;
    public List<string> completedMilestoneIds = new();
    public List<string> grantedRewardIds = new();
    public List<string> unlockedLandmarkIds = new();
    public List<string> activePressureIds = new();
    public List<string> activeEndlessCrisisIds = new();
    public List<string> worldFlags = new();
    public int selfSufficiencyStreakDays;
    public int lastMilestoneEvaluationAbsoluteDay = -1;
}

public interface ISocietyEventCatalog
{
    IReadOnlyList<LifeEventDefinitionSO> LifeEvents { get; }
    IReadOnlyList<GuestRequestDefinitionSO> GuestRequests { get; }
    IReadOnlyList<ServiceIncidentDefinitionSO> ServiceIncidents { get; }
    V20AuthoredContentSO Require(string id);
}

public interface IFactionStoryCatalog
{
    IReadOnlyList<FactionArcDefinitionSO> Arcs { get; }
    IReadOnlyList<FactionChapterDefinitionSO> Chapters { get; }
    IReadOnlyList<FactionContractDefinitionSO> Contracts { get; }
    V20AuthoredContentSO Require(string id);
}

public interface IWorldEventCatalog
{
    IReadOnlyList<SeasonalWorldEventDefinitionSO> SeasonalEvents { get; }
    SeasonalWorldEventDefinitionSO Require(string id);
}

public interface IEndingCatalog
{
    IReadOnlyList<EndingDefinitionSO> All { get; }
    bool TryGet(string id, out EndingDefinitionSO definition);
    EndingDefinitionSO Require(string id);
}

public sealed class V20StoryContentCatalog :
    ISocietyEventCatalog,
    IFactionStoryCatalog,
    IWorldEventCatalog,
    IEndingCatalog
{
    private readonly Dictionary<string, V20AuthoredContentSO> byId;
    public V20StoryContentCatalog(IGameContentDefinitionSource content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        LifeEvents = Exact(content.GetAll<LifeEventDefinitionSO>(), 32, "life events");
        GuestRequests = Exact(content.GetAll<GuestRequestDefinitionSO>(), 14, "guest requests");
        ServiceIncidents = Exact(content.GetAll<ServiceIncidentDefinitionSO>(), 8, "service incidents");
        Arcs = Exact(content.GetAll<FactionArcDefinitionSO>(), 6, "faction arcs");
        Chapters = Exact(content.GetAll<FactionChapterDefinitionSO>(), 36, "faction chapters");
        Contracts = Exact(content.GetAll<FactionContractDefinitionSO>(), 18, "faction contracts");
        SeasonalEvents = Exact(content.GetAll<SeasonalWorldEventDefinitionSO>(), 28, "seasonal events");
        All = Exact(content.GetAll<EndingDefinitionSO>(), 9, "milestones");
        Encounters = content.GetAll<OffenseEncounterSO>()
            .Where(value => value != null)
            .OrderBy(value => value.encounterId, StringComparer.Ordinal)
            .ToArray();
        Diseases = content.GetAll<DiseaseDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        Wildlife = content.GetAll<WildlifeSpeciesSO>()
            .Where(value => value != null)
            .OrderBy(value => value.SpeciesId, StringComparer.Ordinal)
            .ToArray();
        BattlefieldModifiers = content.GetAll<BattlefieldModifierDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        V20AuthoredContentSO[] definitions = LifeEvents.Cast<V20AuthoredContentSO>()
            .Concat(GuestRequests).Concat(ServiceIncidents).Concat(Arcs).Concat(Chapters)
            .Concat(Contracts).Concat(SeasonalEvents).Concat(All).ToArray();
        byId = definitions.ToDictionary(value => value.StableId, StringComparer.Ordinal);
        HashSet<string> chapterIds = Chapters.Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> contractIds = Contracts.Select(value => value.StableId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (FactionArcDefinitionSO arc in Arcs)
        {
            if (arc.chapterIds.Any(id => !chapterIds.Contains(id))
                || arc.contractIds.Any(id => !contractIds.Contains(id)))
                throw new InvalidOperationException(
                    $"Faction arc '{arc.StableId}' has a broken chapter or contract reference.");
        }
        ValidateFactionEffectTargets(definitions, Arcs);
    }

    public IReadOnlyList<LifeEventDefinitionSO> LifeEvents { get; }
    public IReadOnlyList<GuestRequestDefinitionSO> GuestRequests { get; }
    public IReadOnlyList<ServiceIncidentDefinitionSO> ServiceIncidents { get; }
    public IReadOnlyList<FactionArcDefinitionSO> Arcs { get; }
    public IReadOnlyList<FactionChapterDefinitionSO> Chapters { get; }
    public IReadOnlyList<FactionContractDefinitionSO> Contracts { get; }
    public IReadOnlyList<SeasonalWorldEventDefinitionSO> SeasonalEvents { get; }
    public IReadOnlyList<EndingDefinitionSO> All { get; }
    public IReadOnlyList<OffenseEncounterSO> Encounters { get; }
    public IReadOnlyList<DiseaseDefinitionSO> Diseases { get; }
    public IReadOnlyList<WildlifeSpeciesSO> Wildlife { get; }
    public IReadOnlyList<BattlefieldModifierDefinitionSO> BattlefieldModifiers { get; }

    V20AuthoredContentSO ISocietyEventCatalog.Require(string id) => RequireAny(id);
    V20AuthoredContentSO IFactionStoryCatalog.Require(string id) => RequireAny(id);
    SeasonalWorldEventDefinitionSO IWorldEventCatalog.Require(string id) =>
        RequireAny(id) as SeasonalWorldEventDefinitionSO
        ?? throw new KeyNotFoundException($"Unknown seasonal event '{id}'.");
    public bool TryGet(string id, out EndingDefinitionSO definition)
    {
        definition = All.FirstOrDefault(value => string.Equals(value.StableId, Normalize(id), StringComparison.Ordinal));
        return definition != null;
    }
    public EndingDefinitionSO Require(string id) => TryGet(id, out EndingDefinitionSO value)
        ? value
        : throw new KeyNotFoundException($"Unknown milestone '{id}'.");

    private V20AuthoredContentSO RequireAny(string id) => byId.TryGetValue(Normalize(id), out V20AuthoredContentSO value)
        ? value
        : throw new KeyNotFoundException($"Unknown V20 story definition '{id}'.");
    private static IReadOnlyList<T> Exact<T>(IEnumerable<T> source, int count, string label) where T : V20AuthoredContentSO
    {
        T[] values = (source ?? Array.Empty<T>()).Where(value => value != null).OrderBy(value => value.StableId, StringComparer.Ordinal).ToArray();
        List<string> errors = values.SelectMany(value => value.ValidateDefinition()).ToList();
        if (values.Length != count) errors.Add($"Expected {count} {label}, found {values.Length}.");
        if (values.Select(value => value.StableId).Distinct(StringComparer.Ordinal).Count() != values.Length) errors.Add($"Duplicate {label} ids.");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" | ", errors));
        return values;
    }

    private static void ValidateFactionEffectTargets(
        IEnumerable<V20AuthoredContentSO> definitions,
        IEnumerable<FactionArcDefinitionSO> arcs)
    {
        HashSet<string> factionIds = arcs.Select(value => value.factionId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> contextualTargets = new(StringComparer.Ordinal)
        {
            "requesting-faction",
            "affected-faction"
        };
        foreach ((string ownerId, V20ContentEffect effect) in EnumerateEffects(definitions))
        {
            if (effect == null || effect.kind is not (
                    V20ContentEffectKind.FactionRapport
                    or V20ContentEffectKind.FactionGrievance
                    or V20ContentEffectKind.FactionObligation))
                continue;
            if (!factionIds.Contains(effect.targetId)
                && !contextualTargets.Contains(effect.targetId))
                throw new InvalidOperationException(
                    $"V20 definition '{ownerId}' references unknown faction effect target '{effect.targetId}'.");
        }
    }

    private static IEnumerable<(string OwnerId, V20ContentEffect Effect)> EnumerateEffects(
        IEnumerable<V20AuthoredContentSO> definitions)
    {
        foreach (V20AuthoredContentSO definition in definitions)
        {
            IEnumerable<V20ContentEffect> effects = definition switch
            {
                LifeEventDefinitionSO value => (value.automaticEffects ?? new())
                    .Concat((value.choices ?? new()).SelectMany(choice => choice.effects ?? new())),
                GuestRequestDefinitionSO value => (value.successEffects ?? new())
                    .Concat(value.failureEffects ?? new()),
                ServiceIncidentDefinitionSO value => (value.responses ?? new())
                    .SelectMany(choice => choice.effects ?? new()),
                FactionChapterDefinitionSO value => (value.choices ?? new())
                    .SelectMany(choice => choice.effects ?? new()),
                FactionContractDefinitionSO value => (value.successEffects ?? new())
                    .Concat(value.failureEffects ?? new()),
                SeasonalWorldEventDefinitionSO value => (value.startEffects ?? new())
                    .Concat(value.dailyEffects ?? new())
                    .Concat(value.endEffects ?? new()),
                EndingDefinitionSO value => (value.permanentRewards ?? new())
                    .Concat(value.counterPressures ?? new()),
                _ => Array.Empty<V20ContentEffect>()
            };
            foreach (V20ContentEffect effect in effects)
                yield return (definition.StableId, effect);
        }
    }
    private static string Normalize(string value) => value?.Trim() ?? string.Empty;
}

public interface IV20CampaignPersistence
{
    SeasonalEventWorldSaveData CaptureSeasonal();
    SocietyEventWorldSaveData CaptureSociety();
    FactionCampaignWorldSaveData CaptureFactions();
    RunMilestoneWorldSaveData CaptureMilestones();
    SeasonalEventAggregateState PrepareSeasonal(SeasonalEventWorldSaveData data);
    SocietyEventAggregateState PrepareSociety(SocietyEventWorldSaveData data);
    FactionCampaignAggregateState PrepareFactions(FactionCampaignWorldSaveData data);
    RunMilestoneAggregateState PrepareMilestones(RunMilestoneWorldSaveData data);
    void PublishSeasonal(SeasonalEventAggregateState state);
    void PublishSociety(SocietyEventAggregateState state);
    void PublishFactions(FactionCampaignAggregateState state);
    void PublishMilestones(RunMilestoneAggregateState state);
}

public interface IRunMilestoneQuery
{
    RunProgressionPhase Phase { get; }
    int EndlessCycle { get; }
    IReadOnlyCollection<string> CompletedMilestoneIds { get; }
    IReadOnlyCollection<string> WorldFlags { get; }
    bool IsLandmarkUnlocked(string buildingDefinitionId);
}

public sealed class RunMilestoneEvaluationSnapshot
{
    public int AbsoluteDay { get; set; }
    public ISet<int> CompletedResearchIds { get; } = new HashSet<int>();
    public ISet<string> WorldFlags { get; } = new HashSet<string>(StringComparer.Ordinal);
    public IDictionary<V20WorldMetricKind, float> WorldMetrics { get; } = new Dictionary<V20WorldMetricKind, float>();
    public IDictionary<string, int> ItemQuantities { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public IDictionary<string, int> FacilityCounts { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public IDictionary<string, FactionCampaignStateSaveData> Factions { get; } =
        new Dictionary<string, FactionCampaignStateSaveData>(StringComparer.Ordinal);
    public int EligibleCharacterCount { get; set; }
}

public interface IRunMilestoneCommand
{
    IReadOnlyList<string> Evaluate(RunMilestoneEvaluationSnapshot snapshot);
    int AdvanceEndlessCycle();
}

public sealed class V20DailyEventContext
{
    public int AbsoluteDay { get; set; }
    public int RunSeed { get; set; }
    public Season Season { get; set; }
    public int Generation { get; set; }
    public List<string> ParticipantCharacterIds { get; } = new();
    public RunMilestoneEvaluationSnapshot Requirements { get; } = new();
}

public readonly struct V20ResolvedEventResult
{
    public V20ResolvedEventResult(
        string definitionId,
        string resolutionId,
        IReadOnlyList<V20ContentEffect> effects)
    {
        DefinitionId = definitionId ?? string.Empty;
        ResolutionId = resolutionId ?? string.Empty;
        Effects = effects ?? Array.Empty<V20ContentEffect>();
    }

    public string DefinitionId { get; }
    public string ResolutionId { get; }
    public IReadOnlyList<V20ContentEffect> Effects { get; }
}

public interface ISeasonalEventQuery
{
    IReadOnlyList<V20ActiveEventSaveData> ActiveSeasonalEvents { get; }
}

public interface ISocietyEventQuery
{
    IReadOnlyList<V20ActiveEventSaveData> ActiveSocietyEvents { get; }
    IReadOnlyList<V20ActiveEventSaveData> RecentResolvedSocietyEvents { get; }
}

public interface ISocietyEventCommand
{
    IReadOnlyList<V20ResolvedEventResult> EvaluateDaily(V20DailyEventContext context);
    bool TryResolveSocietyEvent(
        string instanceId,
        string choiceId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure);
}

public interface IFactionCampaignQuery
{
    IReadOnlyList<FactionCampaignStateSaveData> Factions { get; }
    bool TryGetFaction(string factionId, out FactionCampaignStateSaveData state);
}

public interface IFactionCampaignCommand
{
    bool TryResolveChapter(
        string factionId,
        string choiceId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure);
    bool TryAcceptContract(
        string factionId,
        string contractId,
        int absoluteDay,
        out string failure);
    bool TryResolveContract(
        string factionId,
        bool success,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure);
    void ApplyFactionChange(
        string factionId,
        int rapportDelta,
        int grievanceDelta,
        int obligationDelta);
}

public interface IEndlessCrisisCommand
{
    IReadOnlyList<string> ComposeNextEndlessCrisis(
        int absoluteDay,
        int runSeed);
}

public sealed class SeasonalEventAggregateState
{
    internal SeasonalEventWorldSaveData Data = new();
}
public sealed class SocietyEventAggregateState
{
    internal SocietyEventWorldSaveData Data = new();
}
public sealed class FactionCampaignAggregateState
{
    internal FactionCampaignWorldSaveData Data = new();
}
public sealed class RunMilestoneAggregateState
{
    internal RunMilestoneWorldSaveData Data = new();
}

public sealed class V20CampaignRuntime :
    IV20CampaignPersistence,
    IRunMilestoneQuery,
    IRunMilestoneCommand,
    ISeasonalEventQuery,
    ISocietyEventQuery,
    ISocietyEventCommand,
    IFactionCampaignQuery,
    IFactionCampaignCommand,
    IEndlessCrisisCommand
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly V20StoryContentCatalog catalog;
    public V20CampaignRuntime(DungeonRuntimeAggregateRootStore rootStore, V20StoryContentCatalog catalog)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        EnsureFactionStates();
    }

    public RunProgressionPhase Phase => Milestones.Data.phase;
    public int EndlessCycle => Milestones.Data.endlessCycle;
    public IReadOnlyCollection<string> CompletedMilestoneIds => Milestones.Data.completedMilestoneIds.AsReadOnly();
    public IReadOnlyCollection<string> WorldFlags =>
        Milestones.Data.worldFlags.AsReadOnly();
    public bool IsLandmarkUnlocked(string id) => Milestones.Data.unlockedLandmarkIds.Contains(Normalize(id), StringComparer.Ordinal);
    public IReadOnlyList<V20ActiveEventSaveData> ActiveSeasonalEvents =>
        Seasonal.Data.activeEvents.AsReadOnly();
    public IReadOnlyList<V20ActiveEventSaveData> ActiveSocietyEvents =>
        Society.Data.activeEvents.AsReadOnly();
    public IReadOnlyList<V20ActiveEventSaveData> RecentResolvedSocietyEvents =>
        Society.Data.recentResolvedEvents.AsReadOnly();
    IReadOnlyList<FactionCampaignStateSaveData> IFactionCampaignQuery.Factions =>
        Factions.Data.factions.AsReadOnly();

    public IReadOnlyList<string> Evaluate(RunMilestoneEvaluationSnapshot snapshot)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        foreach (string flag in state.worldFlags)
            snapshot.WorldFlags.Add(flag);
        UpdateSelfSufficiency(state, snapshot);
        List<string> completed = new();
        foreach (EndingDefinitionSO definition in catalog.All)
        {
            if (state.completedMilestoneIds.Contains(definition.StableId, StringComparer.Ordinal)
                || !RequirementsSatisfied(definition.completionRequirements, snapshot)) continue;
            state.completedMilestoneIds.Add(definition.StableId);
            state.unlockedLandmarkIds.Add(definition.landmarkBuildingId);
            state.grantedRewardIds.Add(definition.permanentRewards[0].targetId);
            state.activePressureIds.Add(definition.counterPressures[0].targetId);
            completed.Add(definition.StableId);
            if (definition.tier == RunMilestoneTier.Legacy && state.phase == RunProgressionPhase.Founding)
                state.phase = RunProgressionPhase.LegacyAge;
            if (definition.tier == RunMilestoneTier.Grand)
                state.phase = RunProgressionPhase.EndlessAge;
        }
        return completed;
    }

    public int AdvanceEndlessCycle()
    {
        if (WritableMilestones.Data.phase != RunProgressionPhase.EndlessAge)
            throw new InvalidOperationException("Endless cycles require EndlessAge.");
        return ++WritableMilestones.Data.endlessCycle;
    }

    public IReadOnlyList<V20ResolvedEventResult> EvaluateDaily(
        V20DailyEventContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (context.AbsoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(context.AbsoluteDay));

        List<V20ResolvedEventResult> resolved = new();
        EvaluateSeasonal(context, resolved);
        EvaluateSociety(context, resolved);
        return resolved;
    }

    public bool TryResolveSocietyEvent(
        string instanceId,
        string choiceId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        SocietyEventWorldSaveData state = WritableSociety.Data;
        V20ActiveEventSaveData active = state.activeEvents.FirstOrDefault(value =>
            string.Equals(value.instanceId, Normalize(instanceId), StringComparison.Ordinal));
        if (active == null)
        {
            failure = "활성 사건을 찾을 수 없습니다.";
            return false;
        }

        V20AuthoredContentSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId);
        if (definition is GuestRequestDefinitionSO guest)
        {
            string guestChoice = Normalize(choiceId);
            bool fulfilled = string.Equals(
                guestChoice,
                "fulfill",
                StringComparison.Ordinal);
            if (!fulfilled && !string.Equals(
                    guestChoice,
                    "decline",
                    StringComparison.Ordinal))
            {
                failure = "손님 요청은 fulfill 또는 decline으로 해결해야 합니다.";
                return false;
            }
            if (fulfilled && !RequirementsSatisfied(
                    guest.serviceRequirements,
                    requirements))
            {
                failure = "손님 요청의 물리 조건을 충족하지 못했습니다.";
                return false;
            }
            IReadOnlyList<V20ContentEffect> guestEffects = fulfilled
                ? guest.successEffects
                : guest.failureEffects;
            active.selectedChoiceId = guestChoice;
            active.resolutionId = guestChoice;
            active.resolved = true;
            state.activeEvents.Remove(active);
            AddResolvedSociety(state, active);
            MarkEventRecurrence(
                state,
                definition,
                active,
                active.startedAbsoluteDay);
            ApplyInternalEffects(guestEffects, active.contextFactionId);
            result = new V20ResolvedEventResult(
                definition.StableId,
                guestChoice,
                guestEffects);
            return true;
        }
        V20ChoiceDefinition choice = GetChoices(definition).FirstOrDefault(value =>
            string.Equals(value.choiceId, Normalize(choiceId), StringComparison.Ordinal));
        if (choice == null)
        {
            failure = "선택지가 이 사건에 속하지 않습니다.";
            return false;
        }
        if (!RequirementsSatisfied(choice.requirements, requirements))
        {
            failure = "선택 조건을 충족하지 못했습니다.";
            return false;
        }

        active.selectedChoiceId = choice.choiceId;
        active.resolutionId = choice.choiceId;
        active.resolved = true;
        state.activeEvents.Remove(active);
        AddResolvedSociety(state, active);
        MarkEventRecurrence(
            state,
            definition,
            active,
            active.startedAbsoluteDay);
        ApplyInternalEffects(choice.effects, active.contextFactionId);
        result = new V20ResolvedEventResult(
            definition.StableId,
            choice.choiceId,
            choice.effects.AsReadOnly());
        return true;
    }

    public bool TryGetFaction(
        string factionId,
        out FactionCampaignStateSaveData state)
    {
        state = Factions.Data.factions.FirstOrDefault(value => string.Equals(
            value.factionId,
            Normalize(factionId),
            StringComparison.Ordinal));
        return state != null;
    }

    public bool TryResolveChapter(
        string factionId,
        string choiceId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (state.currentChapter < 1 || state.currentChapter > 6)
        {
            failure = "이 세력의 장기 서사는 이미 끝났습니다.";
            return false;
        }
        FactionChapterDefinitionSO chapter = catalog.Chapters.Single(value =>
            string.Equals(value.factionId, state.factionId, StringComparison.Ordinal)
            && value.chapterNumber == state.currentChapter);
        if (!RequirementsSatisfied(chapter.triggerRequirements, requirements))
        {
            failure = "서사 장의 시작 조건을 충족하지 못했습니다.";
            return false;
        }
        V20ChoiceDefinition choice = chapter.choices.FirstOrDefault(value =>
            string.Equals(value.choiceId, Normalize(choiceId), StringComparison.Ordinal));
        if (choice == null || !RequirementsSatisfied(choice.requirements, requirements))
        {
            failure = "유효하지 않거나 조건을 충족하지 못한 선택입니다.";
            return false;
        }

        state.majorChoiceFlags.Add($"{chapter.StableId}:{choice.choiceId}");
        state.currentChapter++;
        ApplyInternalEffects(choice.effects);
        result = new V20ResolvedEventResult(
            chapter.StableId,
            choice.choiceId,
            choice.effects.AsReadOnly());
        return true;
    }

    public bool TryAcceptContract(
        string factionId,
        string contractId,
        int absoluteDay,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (!string.IsNullOrWhiteSpace(state.activeContractId))
        {
            failure = "이미 진행 중인 세력 계약이 있습니다.";
            return false;
        }
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, Normalize(contractId), StringComparison.Ordinal)
            && string.Equals(value.factionId, state.factionId, StringComparison.Ordinal));
        if (contract == null
            || state.completedContractIds.Contains(contract.StableId, StringComparer.Ordinal)
            || state.failedContractIds.Contains(contract.StableId, StringComparer.Ordinal))
        {
            failure = "이 세력에 속한 수락 가능한 계약이 아닙니다.";
            return false;
        }
        state.activeContractId = contract.StableId;
        state.activeContractDeadlineAbsoluteDay =
            Math.Max(0, absoluteDay) + contract.deadlineDays;
        return true;
    }

    public bool TryResolveContract(
        string factionId,
        bool success,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, state.activeContractId, StringComparison.Ordinal));
        if (contract == null)
        {
            failure = "진행 중인 계약이 없습니다.";
            return false;
        }
        if (success && !RequirementsSatisfied(contract.completionRequirements, requirements))
        {
            failure = "계약 완료 조건을 충족하지 못했습니다.";
            return false;
        }

        IReadOnlyList<V20ContentEffect> effects = success
            ? contract.successEffects
            : contract.failureEffects;
        (success ? state.completedContractIds : state.failedContractIds)
            .Add(contract.StableId);
        state.activeContractId = string.Empty;
        state.activeContractDeadlineAbsoluteDay = 0;
        ApplyInternalEffects(effects);
        result = new V20ResolvedEventResult(
            contract.StableId,
            success ? "success" : "failure",
            effects);
        return true;
    }

    public void ApplyFactionChange(
        string factionId,
        int rapportDelta,
        int grievanceDelta,
        int obligationDelta)
    {
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        state.rapport = Math.Clamp(state.rapport + rapportDelta, -100, 100);
        state.grievance = Math.Clamp(state.grievance + grievanceDelta, 0, 100);
        state.obligationTokens = Math.Clamp(
            state.obligationTokens + obligationDelta,
            0,
            5);
    }

    public IReadOnlyList<string> ComposeNextEndlessCrisis(
        int absoluteDay,
        int runSeed)
    {
        RunMilestoneWorldSaveData state = WritableMilestones.Data;
        if (state.phase != RunProgressionPhase.EndlessAge)
            throw new InvalidOperationException("Endless crisis composition requires EndlessAge.");
        int cycle = ++state.endlessCycle;
        string key = $"{runSeed}:endless:{absoluteDay}:{cycle}";
        state.activeEndlessCrisisIds = new List<string>
        {
            Pick(catalog.SeasonalEvents, value => value.StableId, key + ":season"),
            Pick(catalog.Encounters, value => value.encounterId, key + ":encounter"),
            Pick(catalog.BattlefieldModifiers, value => value.stableId, key + ":battlefield"),
            Pick(catalog.Diseases, value => value.stableId, key + ":disease"),
            Pick(catalog.Wildlife, value => value.SpeciesId, key + ":wildlife")
        };
        return state.activeEndlessCrisisIds.AsReadOnly();
    }

    public SeasonalEventWorldSaveData CaptureSeasonal() => Clone(Seasonal.Data);
    public SocietyEventWorldSaveData CaptureSociety() => Clone(Society.Data);
    public FactionCampaignWorldSaveData CaptureFactions() => Clone(Factions.Data);
    public RunMilestoneWorldSaveData CaptureMilestones() => Clone(Milestones.Data);
    public SeasonalEventAggregateState PrepareSeasonal(SeasonalEventWorldSaveData data) => new() { Data = ValidateSeasonal(Clone(data)) };
    public SocietyEventAggregateState PrepareSociety(SocietyEventWorldSaveData data) => new() { Data = ValidateSociety(Clone(data)) };
    public FactionCampaignAggregateState PrepareFactions(FactionCampaignWorldSaveData data) => new() { Data = ValidateFactions(Clone(data)) };
    public RunMilestoneAggregateState PrepareMilestones(RunMilestoneWorldSaveData data) => new() { Data = ValidateMilestones(Clone(data)) };
    public void PublishSeasonal(SeasonalEventAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void PublishSociety(SocietyEventAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void PublishFactions(FactionCampaignAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void PublishMilestones(RunMilestoneAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));

    private SeasonalEventAggregateState Seasonal => rootStore.GetOrCreate(() => new SeasonalEventAggregateState());
    private SocietyEventAggregateState Society => rootStore.GetOrCreate(() => new SocietyEventAggregateState());
    private FactionCampaignAggregateState Factions => rootStore.GetOrCreate(() => new FactionCampaignAggregateState());
    private RunMilestoneAggregateState Milestones => rootStore.GetOrCreate(() => new RunMilestoneAggregateState());
    private RunMilestoneAggregateState WritableMilestones => rootStore.GetOrCreateWritable(
        () => new RunMilestoneAggregateState(), value => new RunMilestoneAggregateState { Data = Clone(value.Data) });
    private SeasonalEventAggregateState WritableSeasonal => rootStore.GetOrCreateWritable(
        () => new SeasonalEventAggregateState(),
        value => new SeasonalEventAggregateState { Data = Clone(value.Data) });
    private SocietyEventAggregateState WritableSociety => rootStore.GetOrCreateWritable(
        () => new SocietyEventAggregateState(),
        value => new SocietyEventAggregateState { Data = Clone(value.Data) });
    private FactionCampaignAggregateState WritableFactions => rootStore.GetOrCreateWritable(
        () => new FactionCampaignAggregateState(),
        value => new FactionCampaignAggregateState { Data = Clone(value.Data) });

    private static bool RequirementsSatisfied(V20ContentRequirementSet requirements, RunMilestoneEvaluationSnapshot snapshot)
    {
        if (snapshot == null) return false;
        requirements ??= new V20ContentRequirementSet();
        return (requirements.research ?? new()).All(value => snapshot.CompletedResearchIds.Contains(value.researchNumericId))
            && (requirements.requiredFlags ?? new()).All(snapshot.WorldFlags.Contains)
            && (requirements.excludedFlags ?? new()).All(value => !snapshot.WorldFlags.Contains(value))
            && (requirements.worldMetrics ?? new()).All(value => snapshot.WorldMetrics.TryGetValue(value.kind, out float actual) && actual >= value.minimumValue)
            && (requirements.items ?? new()).All(value => snapshot.ItemQuantities.TryGetValue(value.itemDefinitionId, out int quantity) && quantity >= value.amount)
            && (requirements.facilities ?? new()).All(value => snapshot.FacilityCounts.TryGetValue(FacilityRequirementKey(value), out int count) && count >= value.minimumCount)
            && (requirements.factions ?? new()).All(value => snapshot.Factions.TryGetValue(value.factionId, out FactionCampaignStateSaveData faction)
                && faction.rapport >= value.minimumRapport
                && faction.grievance <= value.maximumGrievance
                && faction.obligationTokens >= value.minimumObligationTokens)
            && ((requirements.characters?.Count ?? 0) == 0
                || snapshot.EligibleCharacterCount >= requirements.characters.Count);
    }

    private void EvaluateSeasonal(
        V20DailyEventContext context,
        ICollection<V20ResolvedEventResult> resolved)
    {
        SeasonalEventWorldSaveData state = WritableSeasonal.Data;
        if (state.lastEvaluationAbsoluteDay >= context.AbsoluteDay) return;
        state.lastEvaluationAbsoluteDay = context.AbsoluteDay;
        int cycle = context.AbsoluteDay / GameCalendarRules.DaysPerYear;
        if (state.cycle != cycle)
        {
            state.cycle = cycle;
            state.completedEventIds.Clear();
        }

        foreach (V20ActiveEventSaveData active in state.activeEvents
                     .Where(value => value.deadlineAbsoluteDay < context.AbsoluteDay)
                     .ToArray())
        {
            SeasonalWorldEventDefinitionSO definition =
                ((IWorldEventCatalog)catalog).Require(active.definitionId);
            active.resolved = true;
            active.resolutionId = "completed";
            state.activeEvents.Remove(active);
            state.completedEventIds.Add(definition.StableId);
            ApplyInternalEffects(definition.endEffects, active.contextFactionId);
            resolved.Add(new V20ResolvedEventResult(
                definition.StableId,
                "completed",
                definition.endEffects.AsReadOnly()));
        }

        foreach (V20ActiveEventSaveData active in state.activeEvents.ToArray())
        {
            SeasonalWorldEventDefinitionSO definition =
                ((IWorldEventCatalog)catalog).Require(active.definitionId);
            ApplyInternalEffects(
                definition.dailyEffects,
                active.contextFactionId);
            if ((definition.dailyEffects?.Count ?? 0) > 0)
            {
                resolved.Add(new V20ResolvedEventResult(
                    definition.StableId,
                    "daily",
                    definition.dailyEffects.AsReadOnly()));
            }
        }

        if (state.activeEvents.Count > 0) return;
        SeasonalWorldEventDefinitionSO[] eligible = catalog.SeasonalEvents
            .Where(value => value.season == context.Season
                && !state.completedEventIds.Contains(value.StableId, StringComparer.Ordinal)
                && RequirementsSatisfied(value.triggerRequirements, context.Requirements))
            .ToArray();
        if (eligible.Length == 0) return;
        SeasonalWorldEventDefinitionSO selected = SelectDeterministic(
            eligible,
            value => value.StableId,
            context,
            "seasonal");
        uint roll = EventRoll(context, selected.StableId);
        int duration = selected.minimumDurationDays
            + (int)(roll % (uint)(selected.maximumDurationDays
                - selected.minimumDurationDays + 1));
        V20ActiveEventSaveData created = CreateEvent(
            selected.StableId,
            context,
            duration,
            Array.Empty<string>());
        state.activeEvents.Add(created);
        ApplyInternalEffects(selected.startEffects, created.contextFactionId);
        resolved.Add(new V20ResolvedEventResult(
            selected.StableId,
            "started",
            selected.startEffects.AsReadOnly()));
    }

    private void EvaluateSociety(
        V20DailyEventContext context,
        ICollection<V20ResolvedEventResult> resolved)
    {
        SocietyEventWorldSaveData state = WritableSociety.Data;
        if (state.lastEvaluationAbsoluteDay >= context.AbsoluteDay) return;
        state.lastEvaluationAbsoluteDay = context.AbsoluteDay;
        state.cooldowns.RemoveAll(value =>
            value == null || value.availableAbsoluteDay <= context.AbsoluteDay);

        foreach (V20ActiveEventSaveData active in state.activeEvents
                     .Where(value => value.deadlineAbsoluteDay < context.AbsoluteDay)
                     .ToArray())
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)catalog).Require(active.definitionId);
            IReadOnlyList<V20ContentEffect> effects = ExpirationEffects(definition);
            active.resolved = true;
            active.resolutionId = "expired";
            state.activeEvents.Remove(active);
            AddResolvedSociety(state, active);
            MarkEventRecurrence(
                state,
                definition,
                active,
                context.AbsoluteDay);
            ApplyInternalEffects(effects, active.contextFactionId);
            resolved.Add(new V20ResolvedEventResult(
                definition.StableId,
                "expired",
                effects));
        }

        foreach (FactionCampaignStateSaveData faction in WritableFactions.Data.factions)
        {
            if (string.IsNullOrWhiteSpace(faction.activeContractId)
                || faction.activeContractDeadlineAbsoluteDay >= context.AbsoluteDay)
                continue;
            FactionContractDefinitionSO contract = catalog.Contracts.Single(value =>
                string.Equals(value.StableId, faction.activeContractId, StringComparison.Ordinal));
            faction.failedContractIds.Add(contract.StableId);
            faction.activeContractId = string.Empty;
            faction.activeContractDeadlineAbsoluteDay = 0;
            ApplyInternalEffects(contract.failureEffects);
            resolved.Add(new V20ResolvedEventResult(
                contract.StableId,
                "expired",
                contract.failureEffects.AsReadOnly()));
        }

        string[] participants = context.ParticipantCharacterIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> occupied = state.activeEvents
            .SelectMany(value => value.participantCharacterIds)
            .ToHashSet(StringComparer.Ordinal);

        LifeEventDefinitionSO[] automatic = catalog.LifeEvents
            .Where(value => value.automatic
                && CanStartSocietyDefinition(value, context, state))
            .OrderBy(value => EventRoll(context, value.StableId))
            .ToArray();
        int automaticCount = 0;
        foreach (LifeEventDefinitionSO definition in automatic)
        {
            string participant = participants.FirstOrDefault(value =>
                !occupied.Contains(value)
                && CanAssignLifeEvent(
                    definition,
                    value,
                    context.Generation,
                    state));
            if (participant == null) continue;
            occupied.Add(participant);
            V20ActiveEventSaveData completed = CreateEvent(
                definition.StableId,
                context,
                0,
                new[] { participant });
            completed.resolved = true;
            completed.resolutionId = "automatic";
            AddResolvedSociety(state, completed);
            MarkEventRecurrence(
                state,
                definition,
                completed,
                context.AbsoluteDay);
            ApplyInternalEffects(
                definition.automaticEffects,
                completed.contextFactionId);
            resolved.Add(new V20ResolvedEventResult(
                definition.StableId,
                "automatic",
                definition.automaticEffects.AsReadOnly()));
            automaticCount++;
            if (automaticCount >= 6) break;
        }

        int ordinaryCap = context.AbsoluteDay <= 30 ? 1 : 2;
        int currentEmergency = state.activeEvents.Count(value =>
            IsEmergency(((ISocietyEventCatalog)catalog).Require(value.definitionId)));
        int currentOrdinary = state.activeEvents.Count - currentEmergency;
        int capacity = Math.Max(0, ordinaryCap - currentOrdinary);
        IEnumerable<V20AuthoredContentSO> majorCandidates = catalog.LifeEvents
            .Where(value => !value.automatic)
            .Cast<V20AuthoredContentSO>()
            .Concat(catalog.GuestRequests)
            .Concat(catalog.ServiceIncidents)
            .Where(value => CanStartSocietyDefinition(value, context, state))
            .OrderBy(value => EventRoll(context, value.StableId));
        foreach (V20AuthoredContentSO definition in majorCandidates)
        {
            bool emergency = IsEmergency(definition);
            if ((emergency && currentEmergency > 0)
                || (!emergency && capacity <= 0))
                continue;
            string participant = participants.FirstOrDefault(value =>
                !occupied.Contains(value)
                && (definition is not LifeEventDefinitionSO life
                    || CanAssignLifeEvent(
                        life,
                        value,
                        context.Generation,
                        state)));
            if (participant == null && definition is LifeEventDefinitionSO)
                continue;
            string[] selectedParticipants = participant == null
                ? Array.Empty<string>()
                : new[] { participant };
            state.activeEvents.Add(CreateEvent(
                definition.StableId,
                context,
                DeadlineDays(definition),
                selectedParticipants));
            if (participant != null) occupied.Add(participant);
            if (emergency) currentEmergency++;
            else capacity--;
            if (capacity <= 0 && currentEmergency > 0) break;
        }
    }

    private bool CanStartSocietyDefinition(
        V20AuthoredContentSO definition,
        V20DailyEventContext context,
        SocietyEventWorldSaveData state)
    {
        if (state.activeEvents.Any(value => string.Equals(
                value.definitionId,
                definition.StableId,
                StringComparison.Ordinal))
            || state.cooldowns.Any(value => string.Equals(
                value.definitionId,
                definition.StableId,
                StringComparison.Ordinal))
            || state.activeEvents.Any(value => string.Equals(
                CategoryKey(((ISocietyEventCatalog)catalog).Require(
                    value.definitionId)),
                CategoryKey(definition),
                StringComparison.Ordinal))
            || state.cooldowns.Any(value => string.Equals(
                value.definitionId,
                CategoryKey(definition),
                StringComparison.Ordinal)))
            return false;
        return RequirementsSatisfied(TriggerRequirements(definition), context.Requirements);
    }

    private static bool CanAssignLifeEvent(
        LifeEventDefinitionSO definition,
        string participantCharacterId,
        int generation,
        SocietyEventWorldSaveData state)
    {
        if (definition == null || string.IsNullOrWhiteSpace(participantCharacterId))
            return false;
        return definition.frequencyRule switch
        {
            LifeEventFrequencyRule.Repeatable => true,
            LifeEventFrequencyRule.OncePerRun =>
                !state.completedOnceEventIds.Contains(
                    definition.StableId,
                    StringComparer.Ordinal),
            LifeEventFrequencyRule.OncePerGeneration =>
                !state.recurrenceKeys.Contains(
                    GenerationRecurrenceKey(definition.StableId, generation),
                    StringComparer.Ordinal),
            LifeEventFrequencyRule.OncePerCharacter =>
                !state.recurrenceKeys.Contains(
                    CharacterRecurrenceKey(
                        definition.StableId,
                        participantCharacterId),
                    StringComparer.Ordinal),
            _ => false
        };
    }

    private void ApplyInternalEffects(
        IEnumerable<V20ContentEffect> source,
        string contextFactionId = "")
    {
        foreach (V20ContentEffect effect in source ?? Array.Empty<V20ContentEffect>())
        {
            if (effect == null || !effect.IsValid) continue;
            int amount = (int)Math.Round(effect.amount);
            switch (effect.kind)
            {
                case V20ContentEffectKind.FactionRapport:
                    ApplyFactionChange(
                        ResolveFactionTarget(effect.targetId, contextFactionId),
                        amount,
                        0,
                        0);
                    break;
                case V20ContentEffectKind.FactionGrievance:
                    ApplyFactionChange(
                        ResolveFactionTarget(effect.targetId, contextFactionId),
                        0,
                        amount,
                        0);
                    break;
                case V20ContentEffectKind.FactionObligation:
                    ApplyFactionChange(
                        ResolveFactionTarget(effect.targetId, contextFactionId),
                        0,
                        0,
                        amount);
                    break;
                case V20ContentEffectKind.WorldFlag:
                    if (!WritableMilestones.Data.worldFlags.Contains(
                            effect.targetId,
                            StringComparer.Ordinal))
                        WritableMilestones.Data.worldFlags.Add(effect.targetId);
                    break;
                case V20ContentEffectKind.MilestonePressure:
                case V20ContentEffectKind.Threat:
                    if (!WritableMilestones.Data.activePressureIds.Contains(
                            effect.targetId,
                            StringComparer.Ordinal))
                        WritableMilestones.Data.activePressureIds.Add(effect.targetId);
                    break;
            }
        }
    }

    private string ResolveFactionTarget(
        string authoredTargetId,
        string contextFactionId)
    {
        string target = Normalize(authoredTargetId);
        if (target is "requesting-faction" or "affected-faction")
            target = Normalize(contextFactionId);
        if (WritableFactions.Data.factions.Any(value => string.Equals(
                value.factionId,
                target,
                StringComparison.Ordinal)))
            return target;
        throw new InvalidOperationException(
            $"Faction effect target '{authoredTargetId}' has no valid campaign faction context.");
    }

    private void EnsureFactionStates()
    {
        FactionCampaignWorldSaveData state = WritableFactions.Data;
        foreach (FactionArcDefinitionSO arc in catalog.Arcs)
        {
            if (state.factions.All(value => !string.Equals(
                    value.factionId,
                    arc.factionId,
                    StringComparison.Ordinal)))
            {
                state.factions.Add(new FactionCampaignStateSaveData
                {
                    factionId = arc.factionId,
                    currentChapter = 1
                });
            }
        }
        state.factions = state.factions
            .OrderBy(value => value.factionId, StringComparer.Ordinal)
            .ToList();
    }

    private FactionCampaignStateSaveData RequireWritableFaction(string factionId)
    {
        string normalized = Normalize(factionId);
        FactionCampaignStateSaveData state = WritableFactions.Data.factions
            .FirstOrDefault(value => string.Equals(
                value.factionId,
                normalized,
                StringComparison.Ordinal));
        return state ?? throw new KeyNotFoundException(
            $"Unknown V20 faction campaign '{normalized}'.");
    }

    private V20ActiveEventSaveData CreateEvent(
        string definitionId,
        V20DailyEventContext context,
        int durationDays,
        IEnumerable<string> participants)
    {
        string[] sorted = (participants ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        uint roll = EventRoll(context, definitionId, sorted);
        string[] factionIds = catalog.Arcs.Select(value => value.factionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (factionIds.Length == 0)
            throw new InvalidOperationException(
                "V20 event creation requires at least one faction campaign.");
        return new V20ActiveEventSaveData
        {
            instanceId = $"event:{context.AbsoluteDay}:{definitionId}:{roll:X8}",
            definitionId = definitionId,
            startedAbsoluteDay = context.AbsoluteDay,
            deadlineAbsoluteDay = context.AbsoluteDay + Math.Max(0, durationDays),
            generation = Math.Max(0, context.Generation),
            deterministicRoll = roll,
            contextFactionId = factionIds[(int)(roll % (uint)factionIds.Length)],
            participantCharacterIds = sorted.ToList()
        };
    }

    private static uint EventRoll(
        V20DailyEventContext context,
        string definitionId,
        IEnumerable<string> participants = null) =>
        PersistentEntityId.GetStableHash32(
            $"{context.RunSeed}:{definitionId}:{context.AbsoluteDay}:"
            + string.Join(",", (participants ?? context.ParticipantCharacterIds)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .OrderBy(value => value, StringComparer.Ordinal)));

    private static T SelectDeterministic<T>(
        IReadOnlyList<T> values,
        Func<T, string> id,
        V20DailyEventContext context,
        string salt) => values
        .OrderBy(value => PersistentEntityId.GetStableHash32(
            $"{context.RunSeed}:{salt}:{id(value)}:{context.AbsoluteDay}"))
        .ThenBy(id, StringComparer.Ordinal)
        .First();

    private static string Pick<T>(
        IReadOnlyList<T> values,
        Func<T, string> id,
        string key)
    {
        if (values == null || values.Count == 0)
            throw new InvalidOperationException("Endless crisis content pool is empty.");
        int index = (int)(PersistentEntityId.GetStableHash32(key)
            % (uint)values.Count);
        return id(values[index]);
    }

    private static V20ContentRequirementSet TriggerRequirements(
        V20AuthoredContentSO definition) => definition switch
        {
            LifeEventDefinitionSO value => value.triggerRequirements,
            GuestRequestDefinitionSO value => value.serviceRequirements,
            ServiceIncidentDefinitionSO value => value.triggerRequirements,
            _ => new V20ContentRequirementSet()
        };

    private static IReadOnlyList<V20ChoiceDefinition> GetChoices(
        V20AuthoredContentSO definition) => definition switch
        {
            LifeEventDefinitionSO value => value.choices,
            ServiceIncidentDefinitionSO value => value.responses,
            _ => Array.Empty<V20ChoiceDefinition>()
        };

    private static IReadOnlyList<V20ContentEffect> ExpirationEffects(
        V20AuthoredContentSO definition) => definition switch
        {
            GuestRequestDefinitionSO value => value.failureEffects,
            _ => Array.Empty<V20ContentEffect>()
        };

    private static int DeadlineDays(V20AuthoredContentSO definition) =>
        definition switch
        {
            LifeEventDefinitionSO value => value.responseDeadlineDays,
            GuestRequestDefinitionSO value => value.deadlineDays,
            _ => 3
        };

    private static bool IsEmergency(V20AuthoredContentSO definition) =>
        definition is LifeEventDefinitionSO { emergency: true }
        || definition is ServiceIncidentDefinitionSO;

    private static void AddResolvedSociety(
        SocietyEventWorldSaveData state,
        V20ActiveEventSaveData value)
    {
        state.recentResolvedEvents.Add(value);
        if (state.recentResolvedEvents.Count > 256)
            state.recentResolvedEvents.RemoveRange(
                0,
                state.recentResolvedEvents.Count - 256);
    }

    private static void MarkEventRecurrence(
        SocietyEventWorldSaveData state,
        V20AuthoredContentSO definition,
        V20ActiveEventSaveData resolvedEvent,
        int absoluteDay)
    {
        int cooldown = definition is LifeEventDefinitionSO life
            ? life.cooldownDays
            : 30;
        state.cooldowns.RemoveAll(value => string.Equals(
            value.definitionId,
            definition.StableId,
            StringComparison.Ordinal));
        state.cooldowns.Add(new V20EventCooldownSaveData
        {
            definitionId = definition.StableId,
            availableAbsoluteDay = absoluteDay + Math.Max(3, cooldown)
        });
        string categoryKey = CategoryKey(definition);
        state.cooldowns.RemoveAll(value => string.Equals(
            value.definitionId,
            categoryKey,
            StringComparison.Ordinal));
        state.cooldowns.Add(new V20EventCooldownSaveData
        {
            definitionId = categoryKey,
            availableAbsoluteDay = absoluteDay + 3
        });
        if (definition is not LifeEventDefinitionSO life) return;
        switch (life.frequencyRule)
        {
            case LifeEventFrequencyRule.OncePerRun:
                if (!state.completedOnceEventIds.Contains(
                        life.StableId,
                        StringComparer.Ordinal))
                    state.completedOnceEventIds.Add(life.StableId);
                break;
            case LifeEventFrequencyRule.OncePerGeneration:
                AddUnique(
                    state.recurrenceKeys,
                    GenerationRecurrenceKey(
                        life.StableId,
                        resolvedEvent?.generation ?? 0));
                break;
            case LifeEventFrequencyRule.OncePerCharacter:
                foreach (string participant in
                         resolvedEvent?.participantCharacterIds
                         ?? new List<string>())
                    AddUnique(
                        state.recurrenceKeys,
                        CharacterRecurrenceKey(life.StableId, participant));
                break;
        }
    }

    private static void AddUnique(ICollection<string> target, string value)
    {
        if (!target.Contains(value, StringComparer.Ordinal)) target.Add(value);
    }

    private static string CategoryKey(V20AuthoredContentSO definition) =>
        definition switch
        {
            LifeEventDefinitionSO value =>
                $"event-category:life:{value.category}",
            GuestRequestDefinitionSO value =>
                $"event-category:guest:{value.kind}",
            ServiceIncidentDefinitionSO value =>
                $"event-category:incident:{value.kind}",
            _ => $"event-category:definition:{definition?.StableId}"
        };

    private static string GenerationRecurrenceKey(
        string definitionId,
        int generation) =>
        $"{definitionId}:generation:{Math.Max(0, generation)}";

    private static string CharacterRecurrenceKey(
        string definitionId,
        string characterId) =>
        $"{definitionId}:character:{Normalize(characterId)}";

    private static string FacilityRequirementKey(V20FacilityRequirement value) =>
        !string.IsNullOrWhiteSpace(value.buildingDefinitionId)
            ? value.buildingDefinitionId
            : $"capability:{value.capabilityId}";

    private SeasonalEventWorldSaveData ValidateSeasonal(SeasonalEventWorldSaveData data)
    {
        if (data == null || data.version != SeasonalEventWorldSaveData.CurrentVersion || data.activeEvents == null || data.completedEventIds == null || data.lastEvaluationAbsoluteDay < -1)
            throw new InvalidOperationException("Seasonal-event save payload is invalid.");
        RequireValidEvents(data.activeEvents, seasonal: true);
        foreach (string id in data.completedEventIds) ((IWorldEventCatalog)catalog).Require(id);
        RequireUniqueIds(data.completedEventIds, "seasonal completion");
        return data;
    }
    private SocietyEventWorldSaveData ValidateSociety(SocietyEventWorldSaveData data)
    {
        if (data == null || data.version != SocietyEventWorldSaveData.CurrentVersion || data.activeEvents == null || data.recentResolvedEvents == null || data.completedOnceEventIds == null || data.recurrenceKeys == null || data.cooldowns == null || data.lastEvaluationAbsoluteDay < -1)
            throw new InvalidOperationException("Society-event save payload is invalid.");
        RequireValidEvents(data.activeEvents.Concat(data.recentResolvedEvents), seasonal: false);
        int activeEmergency = data.activeEvents.Count(value =>
            IsEmergency(((ISocietyEventCatalog)catalog).Require(
                value.definitionId)));
        int activeOrdinary = data.activeEvents.Count - activeEmergency;
        int ordinaryCap = data.lastEvaluationAbsoluteDay is >= 0 and <= 30
            ? 1
            : 2;
        if (activeEmergency > 1
            || activeOrdinary > ordinaryCap
            || data.recentResolvedEvents.Count > 256)
            throw new InvalidOperationException(
                "Society-event history bounds are invalid: "
                + $"ordinary={activeOrdinary}/{ordinaryCap}, "
                + $"emergency={activeEmergency}/1, "
                + $"resolved={data.recentResolvedEvents.Count}/256.");
        foreach (string id in data.completedOnceEventIds) ((ISocietyEventCatalog)catalog).Require(id);
        foreach (string key in data.recurrenceKeys)
            RequireValidRecurrenceKey(key);
        HashSet<string> categoryKeys = catalog.LifeEvents
            .Cast<V20AuthoredContentSO>()
            .Concat(catalog.GuestRequests)
            .Concat(catalog.ServiceIncidents)
            .Select(CategoryKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (V20EventCooldownSaveData cooldown in data.cooldowns)
        {
            if (cooldown == null || cooldown.availableAbsoluteDay < 0)
                throw new InvalidOperationException("Society-event cooldown is invalid.");
            if (!categoryKeys.Contains(cooldown.definitionId))
                ((ISocietyEventCatalog)catalog).Require(cooldown.definitionId);
        }
        RequireUniqueIds(data.completedOnceEventIds, "society once-only completion");
        RequireUniqueIds(data.recurrenceKeys, "society scoped recurrence");
        RequireUniqueIds(data.cooldowns.Select(value => value.definitionId), "society cooldown");
        return data;
    }

    private void RequireValidRecurrenceKey(string key)
    {
        string normalized = Normalize(key);
        int marker = normalized.IndexOf(":generation:", StringComparison.Ordinal);
        if (marker > 0)
        {
            ((ISocietyEventCatalog)catalog).Require(normalized.Substring(0, marker));
            if (!int.TryParse(
                    normalized.Substring(marker + ":generation:".Length),
                    out int generation)
                || generation < 0)
                throw new InvalidOperationException(
                    $"Society generation recurrence key '{key}' is invalid.");
            return;
        }
        marker = normalized.IndexOf(":character:", StringComparison.Ordinal);
        if (marker > 0)
        {
            ((ISocietyEventCatalog)catalog).Require(normalized.Substring(0, marker));
            if (!new CharacterId(normalized.Substring(
                    marker + ":character:".Length)).IsValid)
                throw new InvalidOperationException(
                    $"Society character recurrence key '{key}' is invalid.");
            return;
        }
        throw new InvalidOperationException(
            $"Society recurrence key '{key}' is invalid.");
    }
    private FactionCampaignWorldSaveData ValidateFactions(FactionCampaignWorldSaveData data)
    {
        if (data == null || data.version != FactionCampaignWorldSaveData.CurrentVersion || data.factions == null)
            throw new InvalidOperationException("Faction-campaign save payload is invalid.");
        if (data.factions.Count != catalog.Arcs.Count)
            throw new InvalidOperationException("Faction-campaign save must contain all six authored factions.");
        foreach (FactionCampaignStateSaveData value in data.factions)
        {
            FactionArcDefinitionSO arc = catalog.Arcs.FirstOrDefault(candidate =>
                value != null && string.Equals(
                    candidate.factionId,
                    value.factionId,
                    StringComparison.Ordinal));
            if (value == null || arc == null || value.rapport < -100 || value.rapport > 100 || value.grievance < 0 || value.grievance > 100 || value.obligationTokens < 0 || value.obligationTokens > 5 || value.currentChapter < 1 || value.currentChapter > 7)
                throw new InvalidOperationException("Faction-campaign state is invalid.");
            foreach (string id in (value.completedContractIds ?? new()).Concat(value.failedContractIds ?? new())) ((IFactionStoryCatalog)catalog).Require(id);
            if (!string.IsNullOrWhiteSpace(value.activeContractId))
            {
                FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(candidate =>
                    string.Equals(candidate.StableId, value.activeContractId, StringComparison.Ordinal)
                    && string.Equals(candidate.factionId, value.factionId, StringComparison.Ordinal));
                if (contract == null || value.activeContractDeadlineAbsoluteDay < 0)
                    throw new InvalidOperationException("Faction active contract is invalid.");
            }
            RequireUniqueIds(value.completedContractIds, "completed faction contract");
            RequireUniqueIds(value.failedContractIds, "failed faction contract");
            RequireUniqueIds(value.majorChoiceFlags, "faction choice flag");
        }
        RequireUniqueIds(data.factions.Select(value => value.factionId), "faction campaign");
        return data;
    }
    private RunMilestoneWorldSaveData ValidateMilestones(RunMilestoneWorldSaveData data)
    {
        if (data == null || data.version != RunMilestoneWorldSaveData.CurrentVersion || data.completedMilestoneIds == null || data.grantedRewardIds == null || data.unlockedLandmarkIds == null || data.activePressureIds == null || data.activeEndlessCrisisIds == null || data.worldFlags == null || data.endlessCycle < 0 || data.selfSufficiencyStreakDays < 0 || data.lastMilestoneEvaluationAbsoluteDay < -1)
            throw new InvalidOperationException("Milestone save payload is invalid.");
        foreach (string id in data.completedMilestoneIds) catalog.Require(id);
        if (data.completedMilestoneIds.Distinct(StringComparer.Ordinal).Count() != data.completedMilestoneIds.Count)
            throw new InvalidOperationException("Milestone completion ids are duplicated.");
        if (data.activeEndlessCrisisIds.Count is not (0 or 5))
            throw new InvalidOperationException("Endless crisis must contain exactly five authored components.");
        HashSet<string> endlessIds = catalog.SeasonalEvents.Select(value => value.StableId)
            .Concat(catalog.Encounters.Select(value => value.encounterId))
            .Concat(catalog.BattlefieldModifiers.Select(value => value.stableId))
            .Concat(catalog.Diseases.Select(value => value.stableId))
            .Concat(catalog.Wildlife.Select(value => value.SpeciesId))
            .ToHashSet(StringComparer.Ordinal);
        if (data.activeEndlessCrisisIds.Any(id => !endlessIds.Contains(id)))
            throw new InvalidOperationException("Endless crisis references unknown authored content.");
        RequireUniqueIds(data.grantedRewardIds, "milestone reward");
        RequireUniqueIds(data.unlockedLandmarkIds, "milestone landmark");
        RequireUniqueIds(data.activePressureIds, "milestone pressure");
        RequireUniqueIds(data.worldFlags, "milestone world flag");
        return data;
    }

    private static void UpdateSelfSufficiency(
        RunMilestoneWorldSaveData state,
        RunMilestoneEvaluationSnapshot snapshot)
    {
        if (snapshot.AbsoluteDay < 0
            || state.lastMilestoneEvaluationAbsoluteDay >= snapshot.AbsoluteDay)
        {
            snapshot.WorldMetrics[V20WorldMetricKind.SelfSufficiencyDays] =
                state.selfSufficiencyStreakDays;
            if (state.selfSufficiencyStreakDays >= 120)
                snapshot.WorldFlags.Add("ecology:closed-cycle");
            return;
        }

        bool consecutive = state.lastMilestoneEvaluationAbsoluteDay < 0
            || snapshot.AbsoluteDay
                == state.lastMilestoneEvaluationAbsoluteDay + 1;
        bool sufficientToday = snapshot.WorldFlags.Contains(
            "ecology:self-sufficient-today");
        state.selfSufficiencyStreakDays = sufficientToday
            ? (consecutive ? state.selfSufficiencyStreakDays + 1 : 1)
            : 0;
        state.lastMilestoneEvaluationAbsoluteDay = snapshot.AbsoluteDay;
        snapshot.WorldMetrics[V20WorldMetricKind.SelfSufficiencyDays] =
            state.selfSufficiencyStreakDays;
        if (state.selfSufficiencyStreakDays >= 120)
            snapshot.WorldFlags.Add("ecology:closed-cycle");
    }

    private void RequireValidEvents(
        IEnumerable<V20ActiveEventSaveData> source,
        bool seasonal)
    {
        HashSet<string> instances = new(StringComparer.Ordinal);
        foreach (V20ActiveEventSaveData value in source ?? Array.Empty<V20ActiveEventSaveData>())
        {
            if (value == null || string.IsNullOrWhiteSpace(value.instanceId)
                || !instances.Add(value.instanceId)
                || value.startedAbsoluteDay < 0
                || value.deadlineAbsoluteDay < value.startedAbsoluteDay
                || value.generation < 0
                || !catalog.Arcs.Any(arc => string.Equals(
                    arc.factionId,
                    value.contextFactionId,
                    StringComparison.Ordinal))
                || value.participantCharacterIds == null
                || value.participantCharacterIds.Any(string.IsNullOrWhiteSpace)
                || value.participantCharacterIds.Distinct(StringComparer.Ordinal).Count()
                    != value.participantCharacterIds.Count)
                throw new InvalidOperationException("V20 event instance is invalid.");
            if (seasonal) ((IWorldEventCatalog)catalog).Require(value.definitionId);
            else ((ISocietyEventCatalog)catalog).Require(value.definitionId);
        }
    }

    private static void RequireUniqueIds(
        IEnumerable<string> source,
        string label)
    {
        string[] values = (source ?? Array.Empty<string>()).ToArray();
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException($"{label} ids are invalid.");
    }

    private static SeasonalEventWorldSaveData Clone(SeasonalEventWorldSaveData value) => JsonClone(value);
    private static SocietyEventWorldSaveData Clone(SocietyEventWorldSaveData value) => JsonClone(value);
    private static FactionCampaignWorldSaveData Clone(FactionCampaignWorldSaveData value) => JsonClone(value);
    private static RunMilestoneWorldSaveData Clone(RunMilestoneWorldSaveData value) => JsonClone(value);
    private static T JsonClone<T>(T value) where T : class =>
        UnityEngine.JsonUtility.FromJson<T>(UnityEngine.JsonUtility.ToJson(value ?? throw new ArgumentNullException(nameof(value))));
    private static string Normalize(string value) => value?.Trim() ?? string.Empty;
}
