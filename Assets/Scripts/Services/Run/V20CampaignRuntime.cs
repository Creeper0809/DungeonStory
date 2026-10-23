using static V20CampaignIdentity;
using static V20ContentDefinitionRules;
using static V20CampaignSaveCodec;
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

public sealed class V20CampaignRuntime :
    IV20CampaignPersistence,
    IRunMilestoneQuery,
    ICommittedRunResultQuery,
    IMilestoneGameplayModifierQuery,
    IRunMilestoneCommand,
    ISeasonalEventQuery,
    ISeasonalWildlifeArrivalCommand,
    ISocietyEventQuery,
    IContentWorkDelayQuery,
    IContentWorkDelayCommand,
    ISocietyEventCommand,
    ISocietyObservedMealIncidentCommand,
    ISocietyObservedIncidentResponseCommand,
    ISocietyIncidentOwnedCharacterIdQuery,
    IGuestRequestDeliveryStateCommand,
    IFactionCampaignQuery,
    IFactionCampaignCommand,
    IFactionCampaignDeliveryCommand,
    IEndlessCrisisCommand,
    IEndlessCrisisQuery,
    ISeasonalRestoreCandidateOwner
{
    public const string SocietyChoiceOwnerId = V20CampaignIdentity.SocietyChoiceOwnerId;
    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly V20StoryContentCatalog catalog;
    private readonly V20CampaignSaveCodec saveCodec;
    private readonly V20MilestoneRuntime milestoneRuntime;
    private readonly V20SeasonalOutcomeRuntime seasonalOutcomes;
    private readonly ISeasonalFeedSelfHeatingTargetQuery
        seasonalFeedSelfHeatingTargets;
    private readonly V20SocietyEventStateStore societyState;
    private readonly V20CampaignEffectRuntime campaignEffects;
    internal V21ContentEffectExecutionRegistry EffectHandlers { get; }
    private readonly V20ObservedIncidentRuntime observedIncidents;
    private readonly V20SocietyEventRuntime societyEvents;
    private SeasonalEventAggregateState preparedSeasonalRestore;
    public V20CampaignRuntime(DungeonRuntimeAggregateRootStore rootStore, V20StoryContentCatalog catalog)
        : this(rootStore, catalog, null, null, null) { }
    public V20CampaignRuntime(DungeonRuntimeAggregateRootStore rootStore, V20StoryContentCatalog catalog, IPhysicalItemBatchDispositionService physicalDispositions)
        : this(rootStore, catalog, physicalDispositions, null, null) { }
    public V20CampaignRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        V20StoryContentCatalog catalog,
        IPhysicalItemBatchDispositionService physicalDispositions,
        ISeasonalFeedSelfHeatingTargetQuery seasonalFeedSelfHeatingTargets)
        : this(
            rootStore,
            catalog,
            physicalDispositions,
            seasonalFeedSelfHeatingTargets,
            null)
    {
    }

    public V20CampaignRuntime(
        DungeonRuntimeAggregateRootStore rootStore,
        V20StoryContentCatalog catalog,
        IPhysicalItemBatchDispositionService physicalDispositions,
        ISeasonalFeedSelfHeatingTargetQuery seasonalFeedSelfHeatingTargets,
        ISocialRitualFactPublisher incidentFacts,
        IDomainFactPublisher terminalObservations = null,
        IDomainFactRecoveryPublisher terminalRecovery = null,
        V21ContentEffectExecutionRegistry effectHandlers = null)
    {
        this.rootStore = rootStore ?? throw new ArgumentNullException(nameof(rootStore));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        EffectHandlers = effectHandlers ?? V21ContentEffectExecutionRegistry.Default;
        V20EventSaveValidation eventSaveValidation = new(this.catalog);
        saveCodec = new V20CampaignSaveCodec(this.catalog, eventSaveValidation);
        milestoneRuntime = new V20MilestoneRuntime(this.rootStore, this.catalog, saveCodec, physicalDispositions);
        seasonalOutcomes = new V20SeasonalOutcomeRuntime(this.rootStore, this.catalog, physicalDispositions, terminalObservations, terminalRecovery);
        this.seasonalFeedSelfHeatingTargets =
            seasonalFeedSelfHeatingTargets;
        societyState = new V20SocietyEventStateStore(this.rootStore, eventSaveValidation);
        campaignEffects = new V20CampaignEffectRuntime(this.rootStore, societyState, milestoneRuntime, EffectHandlers,
            () => new V20ContentEffectOwnerSnapshot
            {
                Seasonal = CaptureSeasonal(), Society = CaptureSociety(),
                Factions = CaptureFactions(), Milestones = CaptureMilestones()
            });
        observedIncidents = new V20ObservedIncidentRuntime(societyState, campaignEffects, this.catalog, eventSaveValidation, incidentFacts);
        societyEvents = new V20SocietyEventRuntime(societyState, campaignEffects, observedIncidents, this.catalog);
        EnsureFactionStates();
    }

    internal ISeasonalFeedSelfHeatingTargetQuery
        SeasonalFeedSelfHeatingTargets => seasonalFeedSelfHeatingTargets
        ?? throw new InvalidOperationException(
            "Seasonal feed self-heating evaluation requires its typed physical-target query.");

    public RunProgressionPhase Phase => milestoneRuntime.Phase;

    public int EndlessCycle => milestoneRuntime.EndlessCycle;

    public IReadOnlyCollection<string> CompletedMilestoneIds => milestoneRuntime.CompletedMilestoneIds;

    public IReadOnlyCollection<string> GrantedRewardIds => milestoneRuntime.GrantedRewardIds;

    public IReadOnlyCollection<string> ActivePressureIds => milestoneRuntime.ActivePressureIds;

    public IReadOnlyCollection<string> WorldFlags => milestoneRuntime.WorldFlags;

    public EndlessCrisisSnapshot CurrentEndlessCrisis => milestoneRuntime.CurrentEndlessCrisis;

    public float GetEndlessCrisisMultiplier(EndlessCrisisAxis axis) =>
        milestoneRuntime.GetEndlessCrisisMultiplier(axis);

    public bool TryGetActiveEndlessCrisisEffectOwner(
        EndlessCrisisAxis axis,
        out string effectOwnerId) =>
        milestoneRuntime.TryGetActiveEndlessCrisisEffectOwner(axis, out effectOwnerId);

    public CommittedRunResultSnapshot CaptureCommittedRunResult() =>
        milestoneRuntime.CaptureCommittedRunResult();

    public bool IsLandmarkBuilding(string id) =>
        milestoneRuntime.IsLandmarkBuilding(id);

    public bool IsLandmarkUnlocked(string id) =>
        milestoneRuntime.IsLandmarkUnlocked(id);

    public bool EnemyCounterIntelVisible => milestoneRuntime.EnemyCounterIntelVisible;

    public float ExpeditionTravelTimeMultiplier => milestoneRuntime.ExpeditionTravelTimeMultiplier;

    public float FacilityMaintenanceGoldMultiplier => milestoneRuntime.FacilityMaintenanceGoldMultiplier;

    public float WaterAndFertilizerConsumptionMultiplier => milestoneRuntime.WaterAndFertilizerConsumptionMultiplier;

    public int MentorshipDailyXpCap => milestoneRuntime.MentorshipDailyXpCap;

    public int TemporalStasisWarningDays => milestoneRuntime.TemporalStasisWarningDays;

    public float ManaTransferLossMultiplier => milestoneRuntime.ManaTransferLossMultiplier;

    public float AutomaticMaintenanceWorkMultiplier => milestoneRuntime.AutomaticMaintenanceWorkMultiplier;

    public bool IsAccordSignalSupportDay(int absoluteDay) =>
        milestoneRuntime.IsAccordSignalSupportDay(absoluteDay);

    public bool IsAccordSignalSupportActive(int absoluteDay) =>
        milestoneRuntime.IsAccordSignalSupportActive(absoluteDay);

    public bool TryActivateAccordSignalSupport(int absoluteDay, string sourceStackId) =>
        milestoneRuntime.TryActivateAccordSignalSupport(absoluteDay, sourceStackId);

    public bool HasReward(string milestoneId) =>
        milestoneRuntime.HasReward(milestoneId);

    public bool HasPressure(string milestoneId) =>
        milestoneRuntime.HasPressure(milestoneId);
    public IReadOnlyList<V20ActiveEventSaveData> ActiveSeasonalEvents =>
        Seasonal.Data.activeEvents.AsReadOnly();

    [GameplayInternalOnly(
        "Commits one immutable exact-count seasonal wildlife arrival plan.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalWildlifeArrivalPlan(
        string occurrenceInstanceId,
        IReadOnlyList<SeasonalWildlifeArrivalMemberSaveData> members,
        out string failureReason) =>
        seasonalOutcomes.TryRecordSeasonalWildlifeArrivalPlan(occurrenceInstanceId, members, out failureReason);

    [GameplayInternalOnly(
        "Persists retry-visible failure state without changing a frozen seasonal arrival plan.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalWildlifeArrivalFailure(
        string occurrenceInstanceId,
        string failureReason) =>
        seasonalOutcomes.TryRecordSeasonalWildlifeArrivalFailure(occurrenceInstanceId, failureReason);

    [GameplayInternalOnly(
        "Observes already materialized seasonal visitors; retries publish only the frozen fact.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryObserveSeasonalWildlifeArrival(
        string occurrenceInstanceId,
        string speciesDisplayName,
        int absoluteDay,
        out string failureReason) =>
        seasonalOutcomes.TryObserveSeasonalWildlifeArrival(occurrenceInstanceId, speciesDisplayName, absoluteDay, out failureReason);

    [GameplayInternalOnly(
        "Marks one exact planned visitor as materialized after wildlife authority confirms it.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryMarkSeasonalWildlifeArrivalSpawned(
        string occurrenceInstanceId,
        string wildlifeId,
        out string failureReason) =>
        seasonalOutcomes.TryMarkSeasonalWildlifeArrivalSpawned(occurrenceInstanceId, wildlifeId, out failureReason);

    [GameplayInternalOnly(
        "Freezes one legal exterior source cell before seasonal drift cargo is materialized.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalDriftCargoPlan(
        string occurrenceInstanceId,
        int positionX,
        int positionY,
        out string failureReason) =>
        seasonalOutcomes.TryRecordSeasonalDriftCargoPlan(occurrenceInstanceId, positionX, positionY, out failureReason);

    [GameplayInternalOnly(
        "Records the exact persistent source stack IDs after physical loose publication succeeds.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryMarkSeasonalDriftCargoSpawned(
        string occurrenceInstanceId,
        IReadOnlyList<string> sourceStackIds,
        out string failureReason) =>
        seasonalOutcomes.TryMarkSeasonalDriftCargoSpawned(occurrenceInstanceId, sourceStackIds, out failureReason);

    [GameplayInternalOnly(
        "Persists monotonic actual-pickup progress derived from event-tagged physical lots.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalDriftCargoSecured(
        string occurrenceInstanceId,
        int securedQuantity,
        out string failureReason) =>
        seasonalOutcomes.TryRecordSeasonalDriftCargoSecured(occurrenceInstanceId, securedQuantity, out failureReason);

    [GameplayInternalOnly(
        "Commits only the supplied unpicked event source lots to the typed world-exit Sink outbox.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryExpireSeasonalDriftCargo(
        string occurrenceInstanceId,
        IReadOnlyList<PhysicalItemTransformInput> unpickedSourceLots,
        out string failureReason) =>
        seasonalOutcomes.TryExpireSeasonalDriftCargo(occurrenceInstanceId, unpickedSourceLots, out failureReason);

    [GameplayInternalOnly(
        "Persists retry-visible drift-cargo failure without changing frozen source facts.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryRecordSeasonalDriftCargoFailure(
        string occurrenceInstanceId,
        string failureReason) =>
        seasonalOutcomes.TryRecordSeasonalDriftCargoFailure(occurrenceInstanceId, failureReason);

    [GameplayInternalOnly(
        "Observes settled seasonal cargo; retries publish only the frozen fact.",
        "SeasonalWildlifeVisitorApplicationAdapter only")]
    public bool TryObserveSeasonalDriftCargo(
        string occurrenceInstanceId,
        string itemDisplayName,
        long unitMassGrams,
        int absoluteDay,
        out string failureReason) =>
        seasonalOutcomes.TryObserveSeasonalDriftCargo(occurrenceInstanceId, itemDisplayName, unitMassGrams, absoluteDay, out failureReason);

    public float GetWorldWaterRegenerationMultiplier()
    {
        float multiplier = GetEndlessCrisisMultiplier(
            EndlessCrisisAxis.Climate);
        List<V20ActiveEventSaveData> activeEvents = Seasonal.Data.activeEvents;
        for (int index = 0; index < activeEvents.Count; index++)
        {
            V20ActiveEventSaveData occurrence = activeEvents[index]
                ?? throw new InvalidOperationException(
                    "Seasonal active-event state contains a null occurrence.");
            if (occurrence.resolved)
            {
                continue;
            }

            SeasonalWorldEventDefinitionSO definition =
                ((IWorldEventCatalog)catalog).Require(occurrence.definitionId);
            float authored = definition.worldWaterRegenerationMultiplier;
            if (float.IsNaN(authored)
                || float.IsInfinity(authored)
                || authored <= 0f
                || authored > 1f)
            {
                throw new InvalidOperationException(
                    $"Seasonal event '{definition.StableId}' has an invalid "
                    + "world-water regeneration multiplier.");
            }
            multiplier = Math.Min(multiplier, authored);
        }
        return multiplier;
    }
    public SeasonalPowerCapacityContribution GetPowerCapacityContribution()
    {
        SeasonalPowerCapacityContribution selected =
            SeasonalPowerCapacityContribution.None;
        foreach ((V20ActiveEventSaveData occurrence,
                     SeasonalWorldEventDefinitionSO definition) in
                 ActiveSeasonalDefinitions())
        {
            float multiplier = definition
                .powerAvailableSupplyCapacityMultiplier;
            if (!ValidSeasonalMultiplier(multiplier))
                throw new InvalidOperationException(
                    $"Seasonal event '{definition.StableId}' has an invalid power available-supply multiplier.");
            if (multiplier >= 1f
                || selected.IsActive
                && (multiplier > selected.AvailableSupplyMultiplier
                    || multiplier == selected.AvailableSupplyMultiplier
                    && string.CompareOrdinal(
                        occurrence.instanceId,
                        selected.OccurrenceInstanceId) >= 0))
            {
                continue;
            }
            selected = new SeasonalPowerCapacityContribution(
                occurrence.instanceId,
                definition.StableId,
                definition.DisplayName,
                RemainingSeasonalDays(occurrence),
                multiplier);
        }
        return selected;
    }

    public SeasonalPipedWaterContribution GetPipedWaterContribution()
    {
        SeasonalPipedWaterContribution selected =
            SeasonalPipedWaterContribution.None;
        foreach ((V20ActiveEventSaveData occurrence,
                     SeasonalWorldEventDefinitionSO definition) in
                 ActiveSeasonalDefinitions())
        {
            float multiplier = definition.pipedWaterThroughputMultiplier;
            if (!ValidSeasonalMultiplier(multiplier))
                throw new InvalidOperationException(
                    $"Seasonal event '{definition.StableId}' has an invalid piped-water throughput multiplier.");
            if (multiplier >= 1f
                || selected.IsActive
                && (multiplier > selected.ThroughputMultiplier
                    || multiplier == selected.ThroughputMultiplier
                    && string.CompareOrdinal(
                        occurrence.instanceId,
                        selected.OccurrenceInstanceId) >= 0))
            {
                continue;
            }
            selected = new SeasonalPipedWaterContribution(
                occurrence.instanceId,
                definition.StableId,
                definition.DisplayName,
                RemainingSeasonalDays(occurrence),
                multiplier,
                definition.pipedWaterFreezeThresholdC,
                definition.pipedWaterRecoveryThresholdC);
        }
        return selected;
    }

    private IEnumerable<(V20ActiveEventSaveData Occurrence,
        SeasonalWorldEventDefinitionSO Definition)> ActiveSeasonalDefinitions()
    {
        foreach (V20ActiveEventSaveData occurrence in Seasonal.Data.activeEvents)
        {
            if (occurrence == null)
                throw new InvalidOperationException(
                    "Seasonal active-event state contains a null occurrence.");
            if (occurrence.resolved)
                continue;
            if (string.IsNullOrWhiteSpace(occurrence.instanceId)
                || !string.Equals(
                    occurrence.instanceId,
                    occurrence.instanceId.Trim(),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Seasonal active-event state has a non-canonical occurrence ID.");
            yield return (
                occurrence,
                ((IWorldEventCatalog)catalog).Require(occurrence.definitionId));
        }
    }

    private int RemainingSeasonalDays(V20ActiveEventSaveData occurrence) =>
        Math.Max(
            0,
            occurrence.deadlineAbsoluteDay
            - Math.Max(0, Seasonal.Data.lastEvaluationAbsoluteDay));
    private static bool ValidSeasonalMultiplier(float value) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value > 0f
        && value <= 1f;

    public IReadOnlyList<V20ActiveEventSaveData> ActiveSocietyEvents => societyEvents.ActiveSocietyEvents;

    public IReadOnlyList<V20ActiveEventSaveData> RecentResolvedSocietyEvents => societyEvents.RecentResolvedSocietyEvents;

    internal ObservedLifeEventCommitResult RecordObservedFuneralLifeEvent(
        ObservedFuneralLifeEventReceipt receipt)
    { return observedIncidents.RecordObservedFuneralLifeEvent(receipt); }

    internal ObservedLifeEventCommitResult
        RecordObservedLineageCompressionLifeEvent(
            ObservedLineageCompressionLifeEventReceipt receipt)
    { return observedIncidents.RecordObservedLineageCompressionLifeEvent(receipt); }

    internal ObservedLifeEventCommitResult
        RecordObservedProductionLossLifeEvent(
            ObservedProductionLossLifeEventReceipt receipt)
    { return observedIncidents.RecordObservedProductionLossLifeEvent(receipt); }

    internal ObservedLifeEventCommitResult RecordObservedLastLessonLifeEvent(
        ObservedLastLessonLifeEventReceipt receipt)
    { return observedIncidents.RecordObservedLastLessonLifeEvent(receipt); }

    internal ObservedLifeEventCommitResult
        RecordObservedQuietPromotionLifeEvent(
            ObservedQuietPromotionLifeEventReceipt receipt)
    { return observedIncidents.RecordObservedQuietPromotionLifeEvent(receipt); }

    internal void RequireCanRecordObservedLineageCompressionLifeEvent(
        ObservedLineageCompressionLifeEventReceipt receipt)
    { observedIncidents.RequireCanRecordObservedLineageCompressionLifeEvent(receipt); }
    public int GetRemainingDays(string scopeId = "")
    { return campaignEffects.GetRemainingDays(scopeId); }

    public float GetWorkSpeedMultiplier(WorkTypeId workTypeId)
    { return campaignEffects.GetWorkSpeedMultiplier(workTypeId); }

    public void ApplyWorkDelay(
        string scopeId,
        int days,
        int absoluteDay)
    { campaignEffects.ApplyWorkDelay(scopeId, days, absoluteDay); }
    IReadOnlyList<FactionCampaignStateSaveData> IFactionCampaignQuery.Factions =>
        Factions.Data.factions.AsReadOnly();

    public IReadOnlyList<string> Evaluate(RunMilestoneEvaluationSnapshot snapshot) =>
        milestoneRuntime.Evaluate(snapshot);

    public int AdvanceEndlessCycle() =>
        milestoneRuntime.AdvanceEndlessCycle();

    public IReadOnlyList<V20ResolvedEventResult> EvaluateDaily(
        V20DailyEventContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        if (context.AbsoluteDay < 0)
            throw new ArgumentOutOfRangeException(nameof(context.AbsoluteDay));
        if (context.WeatherFrontId == null
            || context.WeatherFrontId.Length > 0
            && (string.IsNullOrWhiteSpace(context.WeatherFrontId)
                || !string.Equals(
                    context.WeatherFrontId,
                    context.WeatherFrontId.Trim(),
                    StringComparison.Ordinal)))
            throw new ArgumentException(
                "Daily event context weather-front ID must be optional and canonical.",
                nameof(context));

        List<V20ResolvedEventResult> resolved = new();
        campaignEffects.BeginDailyEvaluation(context.AbsoluteDay);
        try
        {
            EvaluateSeasonal(context, resolved);
            if (societyEvents.TryBeginDailyEvaluation(context, resolved, out SocietyEventWorldSaveData state))
            {
                ExpireFactionContracts(context, resolved);
                societyEvents.CompleteDailyEvaluation(context, resolved, state);
            }
            return resolved;
        }
        finally
        {
            campaignEffects.EndDailyEvaluation();
        }
    }

    public ObservedMealIncidentCaptureResult CaptureObservedMealIncident(
        ObservedMealIncidentSnapshot evidence,
        V20DailyEventContext context)
    { return observedIncidents.CaptureObservedMealIncident(evidence, context); }

    public ObservedMealIncidentCaptureResult CaptureObservedShopliftingIncident(
        ObservedShopliftingIncidentSnapshot evidence,
        V20DailyEventContext context)
    { return observedIncidents.CaptureObservedShopliftingIncident(evidence, context); }

    public ObservedMealIncidentCaptureResult CaptureObservedBrawlIncident(
        ObservedBrawlIncidentSnapshot evidence,
        V20DailyEventContext context)
    { return observedIncidents.CaptureObservedBrawlIncident(evidence, context); }

    public ObservedMealIncidentCaptureResult
        CaptureObservedCulturalConflictIncident(
            ObservedCulturalConflictIncidentSnapshot evidence,
            V20DailyEventContext context)
    { return observedIncidents.CaptureObservedCulturalConflictIncident(evidence, context); }

    private void AssertIncidentIdle()
    { societyState.AssertIncidentIdle(); }

    public bool TryResolveObservedMealIncidentTargetDeath(
        CharacterId targetCharacterId,
        int absoluteDay,
        out V20ResolvedEventResult result,
        out DomainFailure failure)
    { return observedIncidents.TryResolveObservedMealIncidentTargetDeath(targetCharacterId, absoluteDay, out result, out failure); }

    public IReadOnlyCollection<CharacterId> GetOwnedCustomerIds()
    { return observedIncidents.GetOwnedCustomerIds(); }

    public bool TryResolveSocietyEvent(
        string instanceId,
        string choiceId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure)
    { return societyEvents.TryResolveSocietyEvent(instanceId, choiceId, requirements, out result, out failure); }

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

        IReadOnlyList<V20ContentEffect> resolvedEffects = WithConsumedRequirements(
            WithConsumedRequirements(choice.effects, chapter.triggerRequirements),
            choice.requirements);
        state.majorChoiceFlags.Add($"{chapter.StableId}:{choice.choiceId}");
        state.currentChapter++;
        ApplyInternalEffects(resolvedEffects);
        result = new V20ResolvedEventResult(
            chapter.StableId,
            choice.choiceId,
            resolvedEffects,
            Array.Empty<string>(),
            state.factionId);
        return true;
    }

    public bool TryAcceptContract(
        string factionId,
        string contractId,
        int absoluteDay,
        out string failure)
    {
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, Normalize(contractId), StringComparison.Ordinal)
            && string.Equals(value.factionId, Normalize(factionId), StringComparison.Ordinal));
        if (contract == null || !string.IsNullOrEmpty(contract.seasonalEventId))
        {
            failure = "활성 계절 사건에 속한 계약은 사건 제안으로만 수락할 수 있습니다.";
            return false;
        }
        if (FactionContractMaterialRules.HasMaterialRequirements(contract))
        {
            failure = "물자 계약은 유효한 물리 납품 목적지로만 수락할 수 있습니다.";
            return false;
        }
        return TryAcceptContractCore(
            factionId,
            contractId,
            absoluteDay,
            default,
            material: false,
            occurrenceId: string.Empty,
            deadlineAbsoluteDay: Math.Max(0, absoluteDay) + contract.deadlineDays,
            out failure);
    }

    public bool TryAcceptContract(
        string factionId,
        string contractId,
        int absoluteDay,
        FactionContractDeliveryTarget target,
        out string failure)
    {
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, Normalize(contractId), StringComparison.Ordinal)
            && string.Equals(value.factionId, Normalize(factionId), StringComparison.Ordinal));
        if (contract == null || !string.IsNullOrEmpty(contract.seasonalEventId))
        {
            failure = "활성 계절 사건에 속한 계약은 사건 제안으로만 수락할 수 있습니다.";
            return false;
        }
        bool material = FactionContractMaterialRules
            .HasMaterialRequirements(contract);
        if (material
            && (!target.IsValid
                || !string.Equals(
                    target.DestinationId,
                    FactionContractDeliveryOutbox.FormatDestinationId(
                        contract.StableId),
                    StringComparison.Ordinal)))
        {
            failure = "물자 계약 납품 목적지가 유효하지 않습니다.";
            return false;
        }
        return TryAcceptContractCore(
            factionId,
            contractId,
            absoluteDay,
            target,
            material,
            occurrenceId: string.Empty,
            deadlineAbsoluteDay: Math.Max(0, absoluteDay) + contract.deadlineDays,
            out failure);
    }

    public bool TryAcceptSeasonalContract(
        string occurrenceId,
        string contractId,
        FactionContractDeliveryTarget target,
        out string failure)
    {
        failure = string.Empty;
        string normalizedOccurrenceId = Normalize(occurrenceId);
        V20ActiveEventSaveData occurrence = Seasonal.Data.activeEvents
            .FirstOrDefault(value => value != null
                && !value.resolved
                && value.deadlineAbsoluteDay >=
                    Seasonal.Data.lastEvaluationAbsoluteDay
                && string.Equals(
                    value.instanceId,
                    normalizedOccurrenceId,
                    StringComparison.Ordinal));
        FactionContractDefinitionSO contract = catalog.Contracts
            .FirstOrDefault(value => occurrence != null
                && string.Equals(
                    value.StableId,
                    Normalize(contractId),
                    StringComparison.Ordinal)
                && string.Equals(
                    value.factionId,
                    occurrence.contextFactionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.seasonalEventId,
                    occurrence.definitionId,
                    StringComparison.Ordinal));
        if (contract == null
            || !FactionContractMaterialRules.HasMaterialRequirements(contract)
            || !target.IsValid
            || !string.Equals(
                target.DestinationId,
                FactionContractDeliveryOutbox.FormatDestinationId(
                    contract.StableId,
                    normalizedOccurrenceId),
                StringComparison.Ordinal))
        {
            failure = "활성 계절 사건과 세력에 맞는 물자 계약이 아닙니다.";
            return false;
        }
        return TryAcceptContractCore(
            contract.factionId,
            contract.StableId,
            Math.Max(
                occurrence.startedAbsoluteDay,
                Seasonal.Data.lastEvaluationAbsoluteDay),
            target,
            material: true,
            occurrenceId: normalizedOccurrenceId,
            deadlineAbsoluteDay: occurrence.deadlineAbsoluteDay,
            out failure);
    }

    private bool TryAcceptContractCore(
        string factionId,
        string contractId,
        int absoluteDay,
        FactionContractDeliveryTarget target,
        bool material,
        string occurrenceId,
        int deadlineAbsoluteDay,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (!string.IsNullOrWhiteSpace(state.activeContractId)
            || FactionContractDeliveryOutbox.HasPending(state)
            || state.activeContractInputOwnerActive
            || state.deliveryTerminalCleanupPending)
        {
            failure = "이미 진행 중인 세력 계약이 있습니다.";
            return false;
        }
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, Normalize(contractId), StringComparison.Ordinal)
            && string.Equals(value.factionId, state.factionId, StringComparison.Ordinal));
        bool seasonal = !string.IsNullOrEmpty(occurrenceId);
        bool alreadyTerminal = seasonal
            ? string.Equals(
                    state.lastSeasonalContractId,
                    contract?.StableId,
                    StringComparison.Ordinal)
                && string.Equals(
                    state.lastSeasonalOccurrenceId,
                    occurrenceId,
                    StringComparison.Ordinal)
            : contract != null
                && (state.completedContractIds.Contains(
                        contract.StableId,
                        StringComparer.Ordinal)
                    || state.failedContractIds.Contains(
                        contract.StableId,
                        StringComparer.Ordinal));
        if (contract == null
            || seasonal != !string.IsNullOrEmpty(contract.seasonalEventId)
            || alreadyTerminal
            || deadlineAbsoluteDay < Math.Max(0, absoluteDay)
            || material
                && (!target.IsValid
                    || !string.Equals(
                        target.DestinationId,
                        FactionContractDeliveryOutbox.FormatDestinationId(
                            contract.StableId,
                            occurrenceId),
                        StringComparison.Ordinal)))
        {
            failure = "이 세력에 속한 수락 가능한 계약이 아닙니다.";
            return false;
        }
        if (seasonal)
        {
            state.completedContractIds.RemoveAll(value => string.Equals(
                value,
                contract.StableId,
                StringComparison.Ordinal));
            state.failedContractIds.RemoveAll(value => string.Equals(
                value,
                contract.StableId,
                StringComparison.Ordinal));
            state.lastSeasonalContractId = contract.StableId;
            state.lastSeasonalOccurrenceId = occurrenceId;
        }
        state.activeContractId = contract.StableId;
        state.activeContractOccurrenceId = occurrenceId;
        state.activeContractDeadlineAbsoluteDay = deadlineAbsoluteDay;
        state.deliveryFailureReason = string.Empty;
        if (material)
        {
            state.activeContractDestinationOwnerId =
                FactionContractDeliveryOutbox.FormatOwnerId(
                    contract.StableId,
                    occurrenceId);
            state.activeContractDestinationId = target.DestinationId;
            state.activeContractDestinationX = target.Position.x;
            state.activeContractDestinationY = target.Position.y;
            state.activeContractInputOwnerActive = true;
        }
        return true;
    }

    public bool TryRecordObservedIncidentResponseStarted(
        string instanceId,
        string operationId,
        string externalOperationId,
        out string failure)
    { return observedIncidents.TryRecordObservedIncidentResponseStarted(instanceId, operationId, externalOperationId, out failure); }

    public bool TryRecordObservedIncidentResponseReceipt(
        string instanceId,
        string operationId,
        string externalOperationId,
        string receiptId,
        out string failure)
    { return observedIncidents.TryRecordObservedIncidentResponseReceipt(instanceId, operationId, externalOperationId, receiptId, out failure); }

    public bool TryRecordObservedIncidentResponseFailure(
        string instanceId,
        string operationId,
        string failureReason,
        bool cancelled,
        out string failure)
    { return observedIncidents.TryRecordObservedIncidentResponseFailure(instanceId, operationId, failureReason, cancelled, out failure); }

    public bool TryAcceptGuestRequestDelivery(
        string instanceId,
        string actionId,
        out string failure)
    { return societyEvents.TryAcceptGuestRequestDelivery(instanceId, actionId, out failure); }

    public bool TryAssignGuestRequestDeliveryVenue(
        string instanceId,
        string facilityInstanceId,
        string destinationId,
        UnityEngine.Vector2Int destination,
        EconomyProjectInputOwnerProjection projection,
        out string failure)
    { return societyEvents.TryAssignGuestRequestDeliveryVenue(instanceId, facilityInstanceId, destinationId, destination, projection, out failure); }

    public bool TrySetGuestRequestDeliveryFailure(
        string instanceId,
        string failureReason,
        out string failure)
    { return societyEvents.TrySetGuestRequestDeliveryFailure(instanceId, failureReason, out failure); }

    public bool TryRequestGuestRequestDeliveryRelease(
        string instanceId,
        string disposition,
        string reason,
        out string failure)
    { return societyEvents.TryRequestGuestRequestDeliveryRelease(instanceId, disposition, reason, out failure); }

    public bool TryMarkGuestRequestInputOwnerRetired(
        string instanceId,
        out string failure)
    { return societyEvents.TryMarkGuestRequestInputOwnerRetired(instanceId, out failure); }

    public bool TryResumeGuestRequestVenueSelection(
        string instanceId,
        out string failure)
    { return societyEvents.TryResumeGuestRequestVenueSelection(instanceId, out failure); }

    public bool TryRecordGuestRequestDeliveryReceipt(
        string instanceId,
        GuestRequestDeliveryReceipt receipt,
        out string failure)
    { return societyEvents.TryRecordGuestRequestDeliveryReceipt(instanceId, receipt, out failure); }

    public bool TryPublishGuestRequestDeliveryOutcome(
        string instanceId,
        string resolutionId,
        out V20ResolvedEventResult result,
        out string failure)
    { return societyEvents.TryPublishGuestRequestDeliveryOutcome(instanceId, resolutionId, out result, out failure); }

    public bool TryFinalizeGuestRequestDelivery(
        string instanceId,
        out string failure)
    { return societyEvents.TryFinalizeGuestRequestDelivery(instanceId, out failure); }

    public bool TrySetInputOwnerProjection(
        string factionId,
        long capacityGrams,
        long massAuthorityRevision,
        string capacityFingerprint,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (string.IsNullOrWhiteSpace(state.activeContractId)
            || !state.activeContractInputOwnerActive
            || capacityGrams <= 0L
            || massAuthorityRevision <= 0L
            || string.IsNullOrWhiteSpace(capacityFingerprint))
        {
            failure = "세력 계약 물리 목적지 projection이 유효하지 않습니다.";
            return false;
        }
        state.activeContractInputCapacityGrams = capacityGrams;
        state.activeContractInputMassAuthorityRevision =
            massAuthorityRevision;
        state.activeContractInputCapacityFingerprint =
            capacityFingerprint.Trim();
        return true;
    }

    public bool TryRecordDeliveryReceipt(
        string factionId,
        FactionContractDeliveryReceipt receipt,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, state.activeContractId,
                StringComparison.Ordinal));
        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        if (contract == null
            || material.Count == 0
            || !state.activeContractInputOwnerActive
            || state.activeContractInputCapacityGrams <= 0L
            || receipt.Quantity != material.Values.Sum()
            || receipt.InputMassGrams !=
                state.activeContractInputCapacityGrams
            || FactionContractDeliveryOutbox.HasPending(state))
        {
            failure = "세력 계약 인도 receipt를 기록할 수 없는 상태입니다.";
            return false;
        }
        try
        {
            FactionContractDeliveryOutbox.RecordPending(
                state,
                contract.StableId,
                state.activeContractOccurrenceId,
                receipt);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception.Message;
            return false;
        }
        return true;
    }

    public bool TryResolveDeliveredContract(
        string factionId,
        RunMilestoneEvaluationSnapshot requirements,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        FactionContractDefinitionSO contract = catalog.Contracts.FirstOrDefault(value =>
            string.Equals(value.StableId, state.activeContractId,
                StringComparison.Ordinal));
        if (contract == null
            || !FactionContractMaterialRules.HasMaterialRequirements(contract)
            || state.deliveryCommitPhase !=
                FactionContractDeliveryCommitPhase.PhysicalCommitted
            || !string.Equals(
                state.deliveryContractId,
                contract.StableId,
                StringComparison.Ordinal)
            || !FactionContractDeliveryOutbox.HasCanonicalPending(state))
        {
            failure = "실제 인도가 확정된 진행 중 계약이 없습니다.";
            return false;
        }
        state.completedContractIds.Add(contract.StableId);
        state.activeContractId = string.Empty;
        state.activeContractDeadlineAbsoluteDay = 0;
        state.deliveryTerminalCleanupPending =
            state.activeContractInputOwnerActive;
        FactionContractDeliveryOutbox.MarkRewardPublished(state);
        ApplyInternalEffects(contract.successEffects, state.factionId);
        result = new V20ResolvedEventResult(
            contract.StableId,
            "success",
            contract.successEffects.AsReadOnly(),
            Array.Empty<string>(),
            state.factionId);
        return true;
    }

    public bool TrySetDeliveryFailure(
        string factionId,
        string failureReason,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (string.IsNullOrWhiteSpace(state.activeContractId)
            && !FactionContractDeliveryOutbox.HasPending(state)
            && !state.deliveryTerminalCleanupPending)
        {
            failure = "진행 중인 세력 계약 납품이 없습니다.";
            return false;
        }
        state.deliveryFailureReason = failureReason?.Trim() ?? string.Empty;
        return true;
    }

    public bool TryMarkInputOwnerRetired(
        string factionId,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (!state.activeContractInputOwnerActive)
            return true;
        if (!state.deliveryTerminalCleanupPending)
        {
            failure = "진행 중인 세력 계약의 입력 목적지는 종료할 수 없습니다.";
            return false;
        }
        state.activeContractInputOwnerActive = false;
        state.activeContractInputCapacityGrams = 0L;
        state.activeContractInputMassAuthorityRevision = 0L;
        state.activeContractInputCapacityFingerprint = string.Empty;
        state.deliveryTerminalCleanupPending = false;
        if (!FactionContractDeliveryOutbox.HasPending(state))
            ClearContractDestination(state);
        return true;
    }

    public bool TryClearDeliveryOutbox(
        string factionId,
        out string failure)
    {
        failure = string.Empty;
        FactionCampaignStateSaveData state = RequireWritableFaction(factionId);
        if (state.deliveryCommitPhase !=
                FactionContractDeliveryCommitPhase.RewardPublished
            || state.activeContractInputOwnerActive
            || state.deliveryTerminalCleanupPending)
        {
            failure = "세력 계약 인도 outbox를 종료할 수 없는 상태입니다.";
            return false;
        }
        FactionContractDeliveryOutbox.Clear(state);
        ClearContractDestination(state);
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
        if (FactionContractMaterialRules.HasMaterialRequirements(contract))
        {
            failure = success
                ? "물자 계약은 실제 인도 receipt 이후에만 완료할 수 있습니다."
                : "물자 계약은 공개 포기 명령으로 실패시킬 수 없습니다.";
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
        if (success)
        {
            effects = WithConsumedRequirements(
                effects,
                contract.completionRequirements);
        }
        (success ? state.completedContractIds : state.failedContractIds)
            .Add(contract.StableId);
        state.activeContractId = string.Empty;
        state.activeContractDeadlineAbsoluteDay = 0;
        ApplyInternalEffects(effects, state.factionId);
        result = new V20ResolvedEventResult(
            contract.StableId,
            success ? "success" : "failure",
            effects,
            Array.Empty<string>(),
            state.factionId);
        return true;
    }

    public void ApplyFactionChange(
        string factionId,
        int rapportDelta,
        int grievanceDelta,
        int obligationDelta)
    { campaignEffects.ApplyFactionChange(factionId, rapportDelta, grievanceDelta, obligationDelta); }

    public void ApplyResolvedEffects(
        IEnumerable<V20ContentEffect> effects,
        string contextFactionId = "") =>
        ApplyInternalEffects(effects, contextFactionId);

    public IReadOnlyList<string> ComposeNextEndlessCrisis(
        int absoluteDay,
        int runSeed) =>
        milestoneRuntime.ComposeNextEndlessCrisis(absoluteDay, runSeed);

    public EndlessCrisisSnapshot AdvanceEndlessCrisis(int absoluteDay) =>
        milestoneRuntime.AdvanceEndlessCrisis(absoluteDay);

    public bool TryOwnInvasionCandidate(string effectOwnerId) =>
        milestoneRuntime.TryOwnInvasionCandidate(effectOwnerId);

    public bool TryBeginOwnedInvasion(
        string effectOwnerId,
        int absoluteDay,
        out string directResponseOwnerId) =>
        milestoneRuntime.TryBeginOwnedInvasion(effectOwnerId, absoluteDay, out directResponseOwnerId);

    public bool TryResolveOwnedInvasion(
        string directResponseOwnerId,
        int absoluteDay) =>
        milestoneRuntime.TryResolveOwnedInvasion(directResponseOwnerId, absoluteDay);

    public SeasonalEventWorldSaveData CaptureSeasonal()
    { AssertSeasonalObservationIdle(); return Clone(Seasonal.Data); }
    public SocietyEventWorldSaveData CaptureSociety()
    { return societyState.CaptureSociety(); }
    public FactionCampaignWorldSaveData CaptureFactions() => Clone(Factions.Data);
    public RunMilestoneWorldSaveData CaptureMilestones() => Clone(Milestones.Data);
    public SeasonalEventAggregateState PrepareSeasonal(SeasonalEventWorldSaveData data)
    { AssertSeasonalObservationIdle(); return new() { Data = saveCodec.ValidateSeasonal(Clone(data)) }; }
    public SeasonalEventAggregateState PrepareSeasonalRestore(
        SeasonalEventWorldSaveData data)
    {
        if (preparedSeasonalRestore != null)
            throw new InvalidOperationException(
                "A seasonal restore candidate is already staged.");
        SeasonalEventAggregateState candidate = PrepareSeasonal(data);
        candidate.AttachRestoreOwner(this);
        preparedSeasonalRestore = candidate;
        return candidate;
    }
    public SocietyEventAggregateState PrepareSociety(SocietyEventWorldSaveData data)
    { return societyState.PrepareSociety(data); }
    public FactionCampaignAggregateState PrepareFactions(FactionCampaignWorldSaveData data) => new() { Data = saveCodec.ValidateFactions(Clone(data), Seasonal.Data) };
    public FactionCampaignAggregateState PrepareFactions(
        FactionCampaignWorldSaveData data,
        SeasonalEventAggregateState seasonalCandidate)
    {
        if (seasonalCandidate == null)
            throw new ArgumentNullException(nameof(seasonalCandidate));
        return new FactionCampaignAggregateState
        {
            Data = saveCodec.ValidateFactions(Clone(data), seasonalCandidate.Data)
        };
    }
    public FactionCampaignAggregateState PrepareFactionsPreflight(
        FactionCampaignWorldSaveData data) => new()
    {
        Data = saveCodec.ValidateFactions(Clone(data), seasonalData: null)
    };
    public FactionCampaignAggregateState PrepareFactionsRestore(
        FactionCampaignWorldSaveData data)
    {
        if (preparedSeasonalRestore == null)
            throw new InvalidOperationException(
                "Faction campaign restore requires the staged seasonal candidate.");
        return new FactionCampaignAggregateState
        {
            Data = saveCodec.ValidateFactions(
                Clone(data),
                preparedSeasonalRestore.Data)
        };
    }
    public RunMilestoneAggregateState PrepareMilestones(RunMilestoneWorldSaveData data) => new() { Data = saveCodec.ValidateMilestones(Clone(data)) };
    public void PublishSeasonal(SeasonalEventAggregateState state)
    { AssertSeasonalObservationIdle(); rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state))); }
    public void PublishSeasonalRestore(SeasonalEventAggregateState state)
    {
        AssertSeasonalObservationIdle();
        if (!ReferenceEquals(preparedSeasonalRestore, state))
            throw new InvalidOperationException(
                "Seasonal restore candidate does not match the staged candidate.");
        state.ReleaseRestoreOwner(this);
        preparedSeasonalRestore = null;
        rootStore.Replace(state);
    }

    internal void DiscardSeasonalRestore(SeasonalEventAggregateState state)
    {
        if (ReferenceEquals(preparedSeasonalRestore, state))
            preparedSeasonalRestore = null;
    }
    public void PublishSociety(SocietyEventAggregateState state)
    { societyState.PublishSociety(state); }
    public void PublishFactions(FactionCampaignAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void PublishMilestones(RunMilestoneAggregateState state) => rootStore.Replace(state ?? throw new ArgumentNullException(nameof(state)));
    public void PublishContentResolution(
        SeasonalEventAggregateState seasonal,
        SocietyEventAggregateState society,
        FactionCampaignAggregateState factions,
        RunMilestoneAggregateState milestones)
    {
        AssertIncidentIdle();
        AssertSeasonalObservationIdle();
        ValidateContentResolutionJoin(seasonal, factions);
        rootStore.PublishPreparedReplacements(
            seasonal ?? throw new ArgumentNullException(nameof(seasonal)),
            society ?? throw new ArgumentNullException(nameof(society)),
            factions ?? throw new ArgumentNullException(nameof(factions)),
            milestones ?? throw new ArgumentNullException(nameof(milestones)));
    }

    public void PublishContentResolution(
        SeasonalEventAggregateState seasonal,
        SocietyEventAggregateState society,
        FactionCampaignAggregateState factions,
        RunMilestoneAggregateState milestones,
        CharacterCareerAggregate careers)
    {
        AssertIncidentIdle();
        AssertSeasonalObservationIdle();
        ValidateContentResolutionJoin(seasonal, factions);
        rootStore.PublishPreparedReplacements(
            seasonal ?? throw new ArgumentNullException(nameof(seasonal)),
            society ?? throw new ArgumentNullException(nameof(society)),
            factions ?? throw new ArgumentNullException(nameof(factions)),
            milestones ?? throw new ArgumentNullException(nameof(milestones)),
            careers ?? throw new ArgumentNullException(nameof(careers)));
    }

    private void ValidateContentResolutionJoin(
        SeasonalEventAggregateState seasonal,
        FactionCampaignAggregateState factions)
    {
        if (seasonal == null)
            throw new ArgumentNullException(nameof(seasonal));
        if (factions == null)
            throw new ArgumentNullException(nameof(factions));
        // Validate the pair being published, including callers that prepared
        // factions against the live season. Validation normalizes legacy fields,
        // so inspect a clone without changing either candidate on failure.
        saveCodec.ValidateFactions(Clone(factions.Data), seasonal.Data);
    }

    private SeasonalEventAggregateState Seasonal => rootStore.GetOrCreate(() => new SeasonalEventAggregateState());
    private FactionCampaignAggregateState Factions => rootStore.GetOrCreate(() => new FactionCampaignAggregateState());
    private RunMilestoneAggregateState Milestones => rootStore.GetOrCreate(() => new RunMilestoneAggregateState());
    private SeasonalEventAggregateState WritableSeasonal
    {
        get
        {
            AssertSeasonalObservationIdle();
            return rootStore.GetOrCreateWritable(() => new SeasonalEventAggregateState(),
                value => new SeasonalEventAggregateState { Data = Clone(value.Data) });
        }
    }
    private void AssertSeasonalObservationIdle() => seasonalOutcomes.AssertIdle();
    private FactionCampaignAggregateState WritableFactions => rootStore.GetOrCreateWritable(
        () => new FactionCampaignAggregateState(),
        value => new FactionCampaignAggregateState { Data = Clone(value.Data) });

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
            SeasonalWildlifeArrivalSaveData wildlifeArrival =
                active.seasonalWildlifeArrival;
            if (wildlifeArrival?.configured == true
                && wildlifeArrival.members.Count == wildlifeArrival.exactCount
                && wildlifeArrival.members.All(value => value?.spawned == true)
                && string.IsNullOrEmpty(wildlifeArrival.outcomeResultKey))
            {
                continue;
            }
            if (active.seasonalDriftCargo?.configured == true
                && (!active.seasonalDriftCargo.expirationCompleted
                    || active.seasonalDriftCargo.spawned
                        && string.IsNullOrEmpty(
                            active.seasonalDriftCargo.outcomeResultKey)))
            {
                continue;
            }
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
                definition.endEffects.AsReadOnly(),
                context.ParticipantCharacterIds.AsReadOnly(),
                active.contextFactionId,
                occurrenceInstanceId: active.instanceId));
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
                    definition.dailyEffects.AsReadOnly(),
                    context.ParticipantCharacterIds.AsReadOnly(),
                    active.contextFactionId));
            }
        }

        if (state.activeEvents.Count > 0) return;
        SeasonalWorldEventDefinitionSO[] eligible = catalog.SeasonalEvents
            .Where(value => value.season == context.Season
                && !state.completedEventIds.Contains(value.StableId, StringComparer.Ordinal)
                && (value.requiredWeatherFrontId.Length == 0
                    || string.Equals(
                        value.requiredWeatherFrontId,
                        context.WeatherFrontId,
                        StringComparison.Ordinal))
                && HasSeasonalFeedSelfHeatingTarget(value)
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
        created.seasonalWildlifeArrival =
            SeasonalWildlifeArrivalSaveData.FromProfile(
                selected.wildlifeArrivalProfile);
        created.seasonalDriftCargo = SeasonalDriftCargoSaveData.FromProfile(
            selected.driftCargoProfile);
        created.seasonalManaLightning =
            SeasonalManaLightningSaveData.FromProfile(
                selected.manaLightningProfile);
        created.seasonalSpecialExpedition =
            SeasonalSpecialExpeditionSaveData.FromProfile(
                selected.specialExpeditionProfile);
        state.activeEvents.Add(created);
        ApplyInternalEffects(selected.startEffects, created.contextFactionId);
        resolved.Add(new V20ResolvedEventResult(
            selected.StableId,
            "started",
            selected.startEffects.AsReadOnly(),
            context.ParticipantCharacterIds.AsReadOnly(),
            created.contextFactionId,
            occurrenceInstanceId: created.instanceId));
    }

    private bool HasSeasonalFeedSelfHeatingTarget(
        SeasonalWorldEventDefinitionSO definition)
    {
        SeasonalFeedSelfHeatingFireProfile profile =
            definition.feedSelfHeatingFireProfile;
        if (profile?.IsConfigured != true)
        {
            return true;
        }

        if (SeasonalFeedSelfHeatingTargets.TrySelect(
                profile,
                out _,
                out SeasonalFeedSelfHeatingTargetFailure reason))
        {
            return true;
        }
        if (reason.Code == SeasonalFeedSelfHeatingTargetFailureCode
                .NoEligiblePhysicalStock)
        {
            return false;
        }

        throw new InvalidOperationException(
            $"Seasonal event '{definition.StableId}' target query "
            + $"failed with {reason.Code}: {reason.Reason}");
    }

    private void ExpireFactionContracts(
        V20DailyEventContext context,
        ICollection<V20ResolvedEventResult> resolved)
    {
        foreach (FactionCampaignStateSaveData faction in WritableFactions.Data.factions)
        {
            if (string.IsNullOrWhiteSpace(faction.activeContractId)
                || faction.activeContractDeadlineAbsoluteDay >= context.AbsoluteDay
                || FactionContractDeliveryOutbox.HasPending(faction))
                continue;
            FactionContractDefinitionSO contract = catalog.Contracts.Single(value =>
                string.Equals(value.StableId, faction.activeContractId, StringComparison.Ordinal));
            faction.failedContractIds.Add(contract.StableId);
            faction.activeContractId = string.Empty;
            faction.activeContractDeadlineAbsoluteDay = 0;
            faction.deliveryTerminalCleanupPending =
                faction.activeContractInputOwnerActive;
            faction.deliveryFailureReason = "faction-contract-deadline-expired";
            ApplyInternalEffects(contract.failureEffects, faction.factionId);
            resolved.Add(new V20ResolvedEventResult(
                contract.StableId,
                "expired",
                contract.failureEffects.AsReadOnly(),
                Array.Empty<string>(),
                faction.factionId));
        }
    }

    private void ApplyInternalEffects(
        IEnumerable<V20ContentEffect> source,
        string contextFactionId = "")
    { campaignEffects.ApplyInternalEffects(source, contextFactionId); }

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

    private static void ClearContractDestination(
        FactionCampaignStateSaveData state)
    {
        state.activeContractOccurrenceId = string.Empty;
        state.activeContractDestinationId = string.Empty;
        state.activeContractDestinationOwnerId = string.Empty;
        state.activeContractDestinationX = 0;
        state.activeContractDestinationY = 0;
        state.activeContractInputCapacityGrams = 0L;
        state.activeContractInputMassAuthorityRevision = 0L;
        state.activeContractInputCapacityFingerprint = string.Empty;
    }

    private V20ActiveEventSaveData CreateEvent(
        string definitionId,
        V20DailyEventContext context,
        int durationDays,
        IEnumerable<string> participants,
        V20AuthoredContentSO societyDefinition = null)
    { return V20ContentDefinitionRules.CreateEvent(catalog, definitionId, context, durationDays, participants, societyDefinition); }
    internal void RecordCommittedChoice(
        CommittedRunChoiceKind kind,
        string ownerId,
        string definitionId,
        string instanceId,
        string choiceId,
        string operationId) =>
        milestoneRuntime.RecordCommittedChoice(kind, ownerId, definitionId, instanceId, choiceId, operationId);

    public static string FacilityRequirementKey(V20FacilityRequirement value) =>
        V20ContentDefinitionRules.FacilityRequirementKey(value);

    void ISeasonalRestoreCandidateOwner.DiscardSeasonalRestore(SeasonalEventAggregateState candidate) =>
        DiscardSeasonalRestore(candidate);

    internal V20CampaignRuntime CreateContentResolutionCandidate(
        IPhysicalItemBatchDispositionService physicalDispositions)
    {
        V20CampaignRuntime candidate = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog,
            physicalDispositions,
            this.SeasonalFeedSelfHeatingTargets,
            null, null, null, EffectHandlers);
        candidate.PublishSeasonal(candidate.PrepareSeasonal(this.CaptureSeasonal()));
        candidate.PublishSociety(candidate.PrepareSociety(this.CaptureSociety()));
        candidate.PublishFactions(candidate.PrepareFactions(this.CaptureFactions()));
        candidate.PublishMilestones(candidate.PrepareMilestones(this.CaptureMilestones()));
        return candidate;
    }

    internal void ObserveCampaignEffects(Action<V20ContentEffect, V20ContentEffectOwnerSnapshot,
        V20ContentEffectOwnerSnapshot> observer) => campaignEffects.Observer = observer;
}
