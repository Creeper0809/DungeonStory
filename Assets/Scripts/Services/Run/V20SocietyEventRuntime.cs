using static V20CampaignIdentity;
using static V20ContentDefinitionRules;
using static V20CampaignSaveCodec;
using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class V20SocietyEventRuntime : ISocietyEventQuery, IGuestRequestDeliveryStateCommand
{
    private readonly V20SocietyEventStateStore societyState;
    private readonly V20CampaignEffectRuntime campaignEffects;
    private readonly V20ObservedIncidentRuntime observedIncidents;
    private readonly V20StoryContentCatalog catalog;

    internal V20SocietyEventRuntime(
        V20SocietyEventStateStore societyState,
        V20CampaignEffectRuntime effects,
        V20ObservedIncidentRuntime observedIncidents,
        V20StoryContentCatalog catalog)
    {
        this.societyState = societyState;
        campaignEffects = effects;
        this.observedIncidents = observedIncidents;
        this.catalog = catalog;
    }

    private SocietyEventAggregateState Society => societyState.Current;
    private SocietyEventAggregateState WritableSociety => societyState.Writable;
    public IReadOnlyList<V20ActiveEventSaveData> ActiveSocietyEvents =>
        Society.Data.activeEvents.AsReadOnly();
    public IReadOnlyList<V20ActiveEventSaveData> RecentResolvedSocietyEvents =>
        Society.Data.recentResolvedEvents.AsReadOnly();

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
            if (GuestRequestDeliveryMaterialRules.HasPhysicalDelivery(guest))
            {
                if (fulfilled && !RequirementsSatisfied(
                        GuestRequestDeliveryMaterialRules
                            .BuildNonDeliveryRequirements(guest),
                        requirements))
                {
                    failure = "손님 요청 수락 조건을 충족하지 못했습니다.";
                    return false;
                }
                GuestRequestDeliverySaveData delivery =
                    active.guestDelivery ??= new GuestRequestDeliverySaveData();
                if (delivery.phase != GuestRequestDeliveryPhase.None)
                {
                    if (fulfilled
                        && delivery.phase is
                            GuestRequestDeliveryPhase.AwaitingVenue
                            or GuestRequestDeliveryPhase.Delivering)
                    {
                        return true;
                    }
                    if (!fulfilled)
                    {
                        return TryRequestGuestRequestDeliveryRelease(
                            active.instanceId,
                            GuestRequestDeliveryOutbox.DeclineDisposition,
                            "guest-request-declined",
                            out failure);
                    }
                    failure = "이미 인도된 손님 요청은 취소하거나 다시 수락할 수 없습니다.";
                    return false;
                }
                if (fulfilled)
                {
                    return TryAcceptGuestRequestDelivery(
                        active.instanceId,
                        V21ContentAlertActionIds.Society(
                            active.instanceId,
                            guestChoice),
                        out failure);
                }
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
            if (fulfilled)
            {
                guestEffects = WithConsumedRequirements(
                    guestEffects,
                    guest.serviceRequirements);
            }
            active.selectedChoiceId = guestChoice;
            active.resolutionId = guestChoice;
            active.resolved = true;
            FreezeSocietyTerminalOutcome(
                active,
                definition,
                GuestResolutionLabel(guestChoice),
                RequireAuthoredNarrative(definition, null),
                guestEffects);
            state.activeEvents.Remove(active);
            societyState.AddResolvedSociety(state, active);
            societyState.MarkEventRecurrence(
                state,
                definition,
                active,
                active.startedAbsoluteDay);
            campaignEffects.ApplyInternalEffects(guestEffects, active.contextFactionId);
            result = CreateSocietyResolvedResult(
                active,
                definition,
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
        if (!observedIncidents.TrySelectResponseChoice(active, choice, out bool deferred, out failure))
            return false;
        if (deferred) return true;

        active.selectedChoiceId = choice.choiceId;
        active.resolutionId = choice.choiceId;
        active.resolved = true;
        observedIncidents.MarkEffectsPublished(active);
        FreezeSocietyTerminalOutcome(
            active,
            definition,
            choice.title,
            RequireAuthoredNarrative(definition, choice),
            choice.effects);
        state.activeEvents.Remove(active);
        societyState.AddResolvedSociety(state, active);
        societyState.MarkEventRecurrence(
            state,
            definition,
            active,
            active.startedAbsoluteDay);
        campaignEffects.ApplyInternalEffects(choice.effects, active.contextFactionId);
        result = CreateSocietyResolvedResult(
            active,
            definition,
            choice.effects);
        return true;
    }

    public bool TryAcceptGuestRequestDelivery(
        string instanceId,
        string actionId,
        out string failure)
    {
        failure = string.Empty;
        V20ActiveEventSaveData active = RequireWritableSocietyEvent(instanceId);
        GuestRequestDefinitionSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId)
                as GuestRequestDefinitionSO;
        if (definition == null
            || !GuestRequestDeliveryMaterialRules.HasPhysicalDelivery(definition)
            || !EconomyProjectInputOwnerAuthority.IsCanonical(actionId)
            || !string.Equals(
                actionId,
                V21ContentAlertActionIds.Society(active.instanceId, "fulfill"),
                StringComparison.Ordinal))
        {
            failure = "물리 납품 손님 요청 수락 정보가 유효하지 않습니다.";
            return false;
        }
        GuestRequestDeliverySaveData delivery =
            active.guestDelivery ??= new GuestRequestDeliverySaveData();
        if (delivery.phase != GuestRequestDeliveryPhase.None)
        {
            if (string.Equals(
                    delivery.acceptedActionId,
                    actionId,
                    StringComparison.Ordinal)
                && delivery.phase is GuestRequestDeliveryPhase.AwaitingVenue
                    or GuestRequestDeliveryPhase.Delivering)
            {
                return true;
            }
            failure = "손님 요청 납품이 이미 다른 단계에서 진행 중입니다.";
            return false;
        }
        delivery.phase = GuestRequestDeliveryPhase.AwaitingVenue;
        delivery.acceptedActionId = actionId;
        delivery.failureReason = "적격 시설을 찾는 중입니다.";
        active.selectedChoiceId = "fulfill";
        return true;
    }

    public bool TryAssignGuestRequestDeliveryVenue(
        string instanceId,
        string facilityInstanceId,
        string destinationId,
        UnityEngine.Vector2Int destination,
        EconomyProjectInputOwnerProjection projection,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        if (delivery.phase != GuestRequestDeliveryPhase.AwaitingVenue
            || !EconomyProjectInputOwnerAuthority.IsCanonical(
                facilityInstanceId)
            || !EconomyProjectInputOwnerAuthority.IsCanonical(destinationId)
            || projection.CapacityGrams <= 0L
            || projection.MassAuthorityRevision <= 0L
            || !EconomyProjectInputOwnerAuthority.IsCanonical(
                projection.Fingerprint))
        {
            failure = "손님 요청 납품 시설 배정 정보가 유효하지 않습니다.";
            return false;
        }
        delivery.phase = GuestRequestDeliveryPhase.Delivering;
        delivery.venueFacilityInstanceId = facilityInstanceId;
        delivery.destinationId = destinationId;
        delivery.destinationX = destination.x;
        delivery.destinationY = destination.y;
        delivery.inputOwnerActive = true;
        delivery.inputCapacityGrams = projection.CapacityGrams;
        delivery.inputMassAuthorityRevision = projection.MassAuthorityRevision;
        delivery.inputCapacityFingerprint = projection.Fingerprint;
        delivery.failureReason = string.Empty;
        return true;
    }

    public bool TrySetGuestRequestDeliveryFailure(
        string instanceId,
        string failureReason,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        if (delivery.phase == GuestRequestDeliveryPhase.None)
        {
            failure = "진행 중인 손님 요청 납품이 없습니다.";
            return false;
        }
        delivery.failureReason = failureReason?.Trim() ?? string.Empty;
        return true;
    }

    public bool TryRequestGuestRequestDeliveryRelease(
        string instanceId,
        string disposition,
        string reason,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        bool terminal = string.Equals(
                disposition,
                GuestRequestDeliveryOutbox.DeclineDisposition,
                StringComparison.Ordinal)
            || string.Equals(
                disposition,
                GuestRequestDeliveryOutbox.ExpiredDisposition,
                StringComparison.Ordinal);
        bool replan = string.Equals(
            disposition,
            GuestRequestDeliveryOutbox.ReplanDisposition,
            StringComparison.Ordinal);
        if (delivery.phase == GuestRequestDeliveryPhase.ReleasePending
            && string.Equals(
                delivery.terminalResolutionId,
                disposition,
                StringComparison.Ordinal))
        {
            return true;
        }
        if ((!terminal && !replan)
            || delivery.phase is not (
                GuestRequestDeliveryPhase.AwaitingVenue
                or GuestRequestDeliveryPhase.Delivering)
            || replan && delivery.phase != GuestRequestDeliveryPhase.Delivering)
        {
            failure = "현재 손님 요청 납품은 회수 단계로 전환할 수 없습니다.";
            return false;
        }
        delivery.phase = GuestRequestDeliveryPhase.ReleasePending;
        delivery.terminalResolutionId = disposition;
        delivery.failureReason = reason?.Trim() ?? string.Empty;
        return true;
    }

    public bool TryMarkGuestRequestInputOwnerRetired(
        string instanceId,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        if (!delivery.inputOwnerActive)
            return true;
        if (delivery.phase is not (
                GuestRequestDeliveryPhase.ReleasePending
                or GuestRequestDeliveryPhase.RewardPublished))
        {
            failure = "진행 중인 손님 요청 입력 목적지는 종료할 수 없습니다.";
            return false;
        }
        delivery.inputOwnerActive = false;
        delivery.inputCapacityGrams = 0L;
        delivery.inputMassAuthorityRevision = 0L;
        delivery.inputCapacityFingerprint = string.Empty;
        return true;
    }

    public bool TryResumeGuestRequestVenueSelection(
        string instanceId,
        out string failure)
    {
        failure = string.Empty;
        GuestRequestDeliverySaveData delivery =
            RequireWritableGuestDelivery(instanceId);
        if (delivery.phase != GuestRequestDeliveryPhase.ReleasePending
            || delivery.inputOwnerActive
            || !string.Equals(
                delivery.terminalResolutionId,
                GuestRequestDeliveryOutbox.ReplanDisposition,
                StringComparison.Ordinal)
            || GuestRequestDeliveryOutbox.IsCanonicalReceipt(delivery))
        {
            failure = "손님 요청 납품 목적지를 다시 찾을 수 없는 상태입니다.";
            return false;
        }
        delivery.phase = GuestRequestDeliveryPhase.AwaitingVenue;
        delivery.venueFacilityInstanceId = string.Empty;
        delivery.destinationId = string.Empty;
        delivery.destinationX = 0;
        delivery.destinationY = 0;
        delivery.terminalResolutionId = string.Empty;
        delivery.failureReason = "적격 시설을 다시 찾는 중입니다.";
        return true;
    }

    public bool TryRecordGuestRequestDeliveryReceipt(
        string instanceId,
        GuestRequestDeliveryReceipt receipt,
        out string failure)
    {
        failure = string.Empty;
        V20ActiveEventSaveData active = RequireWritableSocietyEvent(instanceId);
        GuestRequestDeliverySaveData delivery =
            active.guestDelivery ??= new GuestRequestDeliverySaveData();
        GuestRequestDefinitionSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId)
                as GuestRequestDefinitionSO;
        IReadOnlyDictionary<string, int> material =
            GuestRequestDeliveryMaterialRules.BuildMaterialRequirements(
                definition);
        if (definition == null
            || !delivery.inputOwnerActive
            || delivery.inputCapacityGrams <= 0L
            || receipt.Quantity != material.Values.Sum()
            || receipt.InputMassGrams != delivery.inputCapacityGrams)
        {
            failure = "손님 요청의 물리 인도 receipt를 기록할 수 없습니다.";
            return false;
        }
        try
        {
            GuestRequestDeliveryOutbox.Record(
                delivery,
                active.instanceId,
                receipt);
        }
        catch (InvalidOperationException exception)
        {
            failure = exception.Message;
            return false;
        }
        return true;
    }

    public bool TryPublishGuestRequestDeliveryOutcome(
        string instanceId,
        string resolutionId,
        out V20ResolvedEventResult result,
        out string failure)
    {
        result = default;
        failure = string.Empty;
        V20ActiveEventSaveData active = RequireWritableSocietyEvent(instanceId);
        GuestRequestDeliverySaveData delivery =
            active.guestDelivery ??= new GuestRequestDeliverySaveData();
        GuestRequestDefinitionSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId)
                as GuestRequestDefinitionSO;
        bool fulfilled = string.Equals(
            resolutionId,
            "fulfill",
            StringComparison.Ordinal);
        bool failed = string.Equals(
                resolutionId,
                GuestRequestDeliveryOutbox.DeclineDisposition,
                StringComparison.Ordinal)
            || string.Equals(
                resolutionId,
                GuestRequestDeliveryOutbox.ExpiredDisposition,
                StringComparison.Ordinal);
        if (definition == null
            || fulfilled && (delivery.phase !=
                    GuestRequestDeliveryPhase.PhysicalCommitted
                || !GuestRequestDeliveryOutbox.IsCanonicalReceipt(delivery))
            || failed && (delivery.phase !=
                    GuestRequestDeliveryPhase.ReleasePending
                || delivery.inputOwnerActive
                || !string.Equals(
                    delivery.terminalResolutionId,
                    resolutionId,
                    StringComparison.Ordinal))
            || !fulfilled && !failed)
        {
            failure = "손님 요청 납품 결과를 확정할 수 없는 상태입니다.";
            return false;
        }
        IReadOnlyList<V20ContentEffect> effects = fulfilled
            ? definition.successEffects
            : definition.failureEffects;
        delivery.phase = GuestRequestDeliveryPhase.RewardPublished;
        delivery.terminalResolutionId = resolutionId;
        delivery.failureReason = string.Empty;
        active.selectedChoiceId = fulfilled ? "fulfill" : resolutionId;
        active.resolutionId = resolutionId;
        active.resolved = true;
        FreezeSocietyTerminalOutcome(
            active,
            definition,
            GuestResolutionLabel(resolutionId),
            RequireAuthoredNarrative(definition, null),
            effects);
        campaignEffects.ApplyInternalEffects(effects, active.contextFactionId);
        result = CreateSocietyResolvedResult(
            active,
            definition,
            effects);
        return true;
    }

    public bool TryFinalizeGuestRequestDelivery(
        string instanceId,
        out string failure)
    {
        failure = string.Empty;
        SocietyEventWorldSaveData state = WritableSociety.Data;
        V20ActiveEventSaveData active = state.activeEvents.FirstOrDefault(value =>
            string.Equals(
                value.instanceId,
                Normalize(instanceId),
                StringComparison.Ordinal));
        GuestRequestDeliverySaveData delivery = active?.guestDelivery;
        if (active == null
            || !active.resolved
            || delivery?.phase != GuestRequestDeliveryPhase.RewardPublished
            || delivery.inputOwnerActive
            || string.IsNullOrEmpty(delivery.terminalResolutionId))
        {
            failure = "손님 요청 납품 결과의 ACK 정리를 완료할 수 없습니다.";
            return false;
        }
        V20AuthoredContentSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId);
        ResetGuestDelivery(delivery);
        state.activeEvents.Remove(active);
        societyState.AddResolvedSociety(state, active);
        societyState.MarkEventRecurrence(
            state,
            definition,
            active,
            active.startedAbsoluteDay);
        return true;
    }

    private V20ActiveEventSaveData RequireWritableSocietyEvent(
        string instanceId)
    {
        string normalized = Normalize(instanceId);
        V20ActiveEventSaveData active = WritableSociety.Data.activeEvents
            .FirstOrDefault(value => value != null && string.Equals(
                value.instanceId,
                normalized,
                StringComparison.Ordinal));
        return active ?? throw new KeyNotFoundException(
            $"Unknown active society event '{normalized}'.");
    }

    private GuestRequestDeliverySaveData RequireWritableGuestDelivery(
        string instanceId)
    {
        V20ActiveEventSaveData active = RequireWritableSocietyEvent(instanceId);
        return active.guestDelivery ??= new GuestRequestDeliverySaveData();
    }

    private static void ResetGuestDelivery(
        GuestRequestDeliverySaveData delivery)
    {
        if (delivery == null)
            throw new ArgumentNullException(nameof(delivery));
        delivery.phase = GuestRequestDeliveryPhase.None;
        delivery.acceptedActionId = string.Empty;
        delivery.venueFacilityInstanceId = string.Empty;
        delivery.destinationId = string.Empty;
        delivery.destinationX = 0;
        delivery.destinationY = 0;
        delivery.inputOwnerActive = false;
        delivery.inputCapacityGrams = 0L;
        delivery.inputMassAuthorityRevision = 0L;
        delivery.inputCapacityFingerprint = string.Empty;
        GuestRequestDeliveryOutbox.ClearReceipt(delivery);
        delivery.terminalResolutionId = string.Empty;
        delivery.failureReason = string.Empty;
    }

    internal bool TryBeginDailyEvaluation(
        V20DailyEventContext context,
        ICollection<V20ResolvedEventResult> resolved,
        out SocietyEventWorldSaveData state)
    {
        state = WritableSociety.Data;
        if (state.lastEvaluationAbsoluteDay >= context.AbsoluteDay) return false;
        state.lastEvaluationAbsoluteDay = context.AbsoluteDay;
        state.cooldowns.RemoveAll(value =>
            value == null || value.availableAbsoluteDay <= context.AbsoluteDay);

        foreach (V20ActiveEventSaveData active in state.activeEvents
                     .Where(value => value.deadlineAbsoluteDay < context.AbsoluteDay
                         && (value.guestDelivery?.phase
                                 ?? GuestRequestDeliveryPhase.None)
                             == GuestRequestDeliveryPhase.None
                         && (value.observedIncidentResponse?.phase
                                 ?? ObservedIncidentResponsePhase.None)
                             == ObservedIncidentResponsePhase.None)
                     .ToArray())
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)catalog).Require(active.definitionId);
            IReadOnlyList<V20ContentEffect> effects = ExpirationEffects(definition);
            active.resolved = true;
            active.resolutionId = "expired";
            FreezeSocietyTerminalOutcome(
                active,
                definition,
                "기한 만료",
                RequireAuthoredNarrative(definition, null),
                effects);
            state.activeEvents.Remove(active);
            societyState.AddResolvedSociety(state, active);
            societyState.MarkEventRecurrence(
                state,
                definition,
                active,
                context.AbsoluteDay);
            campaignEffects.ApplyInternalEffects(effects, active.contextFactionId);
            resolved.Add(CreateSocietyResolvedResult(
                active,
                definition,
                effects));
        }
        return true;
    }

    internal void CompleteDailyEvaluation(
        V20DailyEventContext context,
        ICollection<V20ResolvedEventResult> resolved,
        SocietyEventWorldSaveData state)
    {
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
                && CanStartSocietyDefinition(catalog,
                    value,
                    context,
                    state,
                    dailyCadenceCandidate: true))
            .OrderBy(value => WeightedDefinitionRoll(context, value))
            .ToArray();
        int automaticCount = 0;
        foreach (LifeEventDefinitionSO definition in automatic)
        {
            int requiredParticipantCount = definition.automaticEffects.Any(
                value => value != null
                    && value.kind == V20ContentEffectKind.Relationship)
                ? 2
                : 1;
            string[] selectedParticipants = participants
                .Where(value => !occupied.Contains(value)
                    && CanAssignLifeEvent(
                        definition,
                        value,
                        context,
                        state))
                .OrderBy(value => WeightedParticipantRoll(
                    context,
                    definition,
                    value))
                .Take(requiredParticipantCount)
                .ToArray();
            if (selectedParticipants.Length != requiredParticipantCount)
                continue;
            foreach (string participant in selectedParticipants)
                occupied.Add(participant);
            V20ActiveEventSaveData completed = CreateEvent(catalog,
                definition.StableId,
                context,
                0,
                selectedParticipants,
                definition);
            completed.resolved = true;
            completed.resolutionId = "automatic";
            FreezeSocietyTerminalOutcome(
                completed,
                definition,
                "자동 처리",
                RequireAuthoredNarrative(definition, null),
                definition.automaticEffects);
            societyState.AddResolvedSociety(state, completed);
            societyState.MarkEventRecurrence(
                state,
                definition,
                completed,
                context.AbsoluteDay);
            campaignEffects.ApplyInternalEffects(
                definition.automaticEffects,
                completed.contextFactionId);
            resolved.Add(CreateSocietyResolvedResult(
                completed,
                definition,
                definition.automaticEffects));
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
            .Where(value => CanStartSocietyDefinition(catalog,
                value,
                context,
                state,
                dailyCadenceCandidate: true))
            .OrderBy(value => WeightedDefinitionRoll(context, value));
        foreach (V20AuthoredContentSO definition in majorCandidates)
        {
            bool emergency = IsEmergency(definition);
            if ((emergency && currentEmergency > 0)
                || (!emergency && capacity <= 0))
                continue;
            string participant = participants
                .Where(value => !occupied.Contains(value)
                    && (definition is not LifeEventDefinitionSO life
                        || CanAssignLifeEvent(
                            life,
                            value,
                            context,
                            state)))
                .OrderBy(value => WeightedParticipantRoll(
                    context,
                    definition,
                    value))
                .FirstOrDefault();
            if (participant == null && definition is LifeEventDefinitionSO)
                continue;
            string[] selectedParticipants = participant == null
                ? Array.Empty<string>()
                : new[] { participant };
            state.activeEvents.Add(CreateEvent(catalog,
                definition.StableId,
                context,
                DeadlineDays(definition),
                selectedParticipants,
                definition));
            if (participant != null) occupied.Add(participant);
            if (emergency) currentEmergency++;
            else capacity--;
            if (capacity <= 0 && currentEmergency > 0) break;
        }
    }
}
