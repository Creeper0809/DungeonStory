using static V20CampaignIdentity;
using static V20ContentDefinitionRules;
using static V20ObservedEventEvidenceRules;
using static V20CampaignSaveCodec;
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

internal sealed class V20ObservedIncidentRuntime :
    ISocietyObservedMealIncidentCommand,
    ISocietyObservedIncidentResponseCommand,
    ISocietyIncidentOwnedCharacterIdQuery
{
    private readonly V20SocietyEventStateStore societyState;
    private readonly V20CampaignEffectRuntime campaignEffects;
    private readonly V20StoryContentCatalog catalog;
    private readonly V20EventSaveValidation eventSaveValidation;
    private readonly ISocialRitualFactPublisher incidentFacts;

    internal V20ObservedIncidentRuntime(
        V20SocietyEventStateStore societyState,
        V20CampaignEffectRuntime effects,
        V20StoryContentCatalog catalog,
        V20EventSaveValidation eventSaveValidation,
        ISocialRitualFactPublisher incidentFacts)
    {
        this.societyState = societyState;
        campaignEffects = effects;
        this.catalog = catalog;
        this.eventSaveValidation = eventSaveValidation;
        this.incidentFacts = incidentFacts;
    }

    private SocietyEventAggregateState Society => societyState.Current;
    private SocietyEventAggregateState WritableSociety => societyState.Writable;

    internal ObservedLifeEventCommitResult RecordObservedFuneralLifeEvent(
        ObservedFuneralLifeEventReceipt receipt)
    => RecordObservedLifeEvent(
        ObservedLifeEventSourceKind.Funeral,
        receipt.SourceOperationId,
        receipt.CanonicalPayload,
        receipt.AbsoluteDay,
        receipt.Generation,
        receipt.ParticipantCharacterIds.Select(value => value.Value),
        GraveVisitLifeEventId);

    internal ObservedLifeEventCommitResult
        RecordObservedLineageCompressionLifeEvent(
            ObservedLineageCompressionLifeEventReceipt receipt)
    =>
        RecordObservedLifeEvent(
            ObservedLifeEventSourceKind.LineageCompression,
            receipt.SourceOperationId,
            receipt.CanonicalPayload,
            receipt.ArchiveAbsoluteDay,
            receipt.Generation,
            new[] { receipt.CharacterId.Value },
            StoryCompressedLifeEventId);

    internal ObservedLifeEventCommitResult
        RecordObservedProductionLossLifeEvent(
            ObservedProductionLossLifeEventReceipt receipt)
    =>
        RecordObservedLifeEvent(
            ObservedLifeEventSourceKind.ProductionDeclaredLoss,
            receipt.SourceOperationId,
            receipt.CanonicalPayload,
            receipt.AbsoluteDay,
            receipt.Generation,
            new[] { receipt.StudentCharacterId.Value },
            ApprenticeMistakeLifeEventId,
            resolveAutomatically: false);

    internal ObservedLifeEventCommitResult RecordObservedLastLessonLifeEvent(
        ObservedLastLessonLifeEventReceipt receipt)
    => RecordObservedLifeEvent(
        ObservedLifeEventSourceKind.LastMentorshipLesson,
        receipt.SourceOperationId,
        receipt.CanonicalPayload,
        receipt.AbsoluteDay,
        receipt.Generation,
        new[] { receipt.MentorCharacterId.Value },
        LastLessonLifeEventId,
        resolveAutomatically: false);

    internal ObservedLifeEventCommitResult
        RecordObservedQuietPromotionLifeEvent(
            ObservedQuietPromotionLifeEventReceipt receipt)
    =>
        RecordObservedLifeEvent(
            ObservedLifeEventSourceKind.ProficiencyPromotion,
            receipt.SourceOperationId,
            receipt.CanonicalPayload,
            receipt.AbsoluteDay,
            receipt.Generation,
            new[] { receipt.CharacterId.Value },
            QuietPromotionLifeEventId);

    internal void RequireCanRecordObservedLineageCompressionLifeEvent(
        ObservedLineageCompressionLifeEventReceipt receipt)
    {
        LifeEventDefinitionSO definition = eventSaveValidation.RequireObservedLifeEventDefinition(
            StoryCompressedLifeEventId);
        ObservedLifeEventReceiptSaveData[] matches =
            Society.Data.successfulLifeEventOperations
                .Where(value => value != null
                    && value.sourceKind
                        == ObservedLifeEventSourceKind.LineageCompression
                    && string.Equals(
                        value.sourceOperationId,
                        receipt.SourceOperationId,
                        StringComparison.Ordinal))
                .ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException(
                $"Observed lineage source '{receipt.SourceOperationId}' has duplicate receipts.");
        if (matches.Length == 1
            && !string.Equals(
                matches[0].canonicalPayload,
                receipt.CanonicalPayload,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Observed lineage source '{receipt.SourceOperationId}' conflicts with its committed receipt.");
        if (matches.Length == 1) return;

        V20DailyEventContext context = new()
        {
            AbsoluteDay = receipt.ArchiveAbsoluteDay,
            Generation = receipt.Generation
        };
        if (CanAssignLifeEvent(
                definition,
                receipt.CharacterId.Value,
                context,
                Society.Data))
        {
            CreateObservedLifeEvent(catalog,
                ObservedLifeEventSourceKind.LineageCompression,
                receipt.SourceOperationId,
                receipt.CanonicalPayload,
                receipt.ArchiveAbsoluteDay,
                receipt.Generation,
                receipt.CharacterId.Value,
                definition);
        }
    }

    public ObservedMealIncidentCaptureResult CaptureObservedMealIncident(
        ObservedMealIncidentSnapshot evidence,
        V20DailyEventContext context)
    {
        if (context == null
            || context.AbsoluteDay < 1
            || context.AbsoluteDay != evidence.AbsoluteDay
            || !TryBuildObservedMealEvidence(
                evidence,
                definitionId: string.Empty,
                occurrenceInstanceId: string.Empty,
                out _,
                out ObservedMealIncidentEvidenceSaveData proposed))
        {
            return NotEligibleObservedIncident(
                "observed-meal-incident-not-eligible");
        }
        return CaptureObservedIncident(
            proposed,
            evidence.TargetCharacterId,
            context);
    }

    public ObservedMealIncidentCaptureResult CaptureObservedShopliftingIncident(
        ObservedShopliftingIncidentSnapshot evidence,
        V20DailyEventContext context)
    {
        if (context == null
            || context.AbsoluteDay < 1
            || context.AbsoluteDay != evidence.AbsoluteDay
            || !TryBuildObservedShopliftingEvidence(
                evidence,
                definitionId: string.Empty,
                occurrenceInstanceId: string.Empty,
                out ObservedMealIncidentEvidenceSaveData proposed))
        {
            return NotEligibleObservedIncident(
                "observed-shoplifting-incident-not-eligible");
        }
        return CaptureObservedIncident(
            proposed,
            evidence.TargetCharacterId,
            context);
    }

    public ObservedMealIncidentCaptureResult CaptureObservedBrawlIncident(
        ObservedBrawlIncidentSnapshot evidence,
        V20DailyEventContext context)
    {
        if (context == null
            || context.AbsoluteDay < 1
            || context.AbsoluteDay != evidence.AbsoluteDay
            || !TryBuildObservedBrawlEvidence(
                evidence,
                definitionId: string.Empty,
                occurrenceInstanceId: string.Empty,
                out ObservedMealIncidentEvidenceSaveData proposed))
        {
            return NotEligibleObservedIncident(
                "observed-brawl-incident-not-eligible");
        }
        return CaptureObservedIncident(
            proposed,
            evidence.TargetCharacterId,
            context);
    }

    public ObservedMealIncidentCaptureResult
        CaptureObservedCulturalConflictIncident(
            ObservedCulturalConflictIncidentSnapshot evidence,
            V20DailyEventContext context)
    {
        if (context == null
            || context.AbsoluteDay < 1
            || context.AbsoluteDay != evidence.AbsoluteDay
            || !TryBuildObservedCulturalConflictEvidence(
                evidence,
                definitionId: string.Empty,
                occurrenceInstanceId: string.Empty,
                out ObservedMealIncidentEvidenceSaveData proposed))
        {
            return NotEligibleObservedIncident(
                "observed-cultural-conflict-incident-not-eligible");
        }
        return CaptureObservedIncident(
            proposed,
            evidence.CustomerCharacterId,
            context);
    }

    private ObservedMealIncidentCaptureResult CaptureObservedIncident(
        ObservedMealIncidentEvidenceSaveData proposed,
        CharacterId targetCharacterId,
        V20DailyEventContext context)
    {
        societyState.AssertIncidentIdle();
        if (context.ActiveExteriorIncidentCharacterIds.Contains(targetCharacterId.Value))
        {
            return new ObservedMealIncidentCaptureResult(
                ObservedMealIncidentCaptureDisposition.NotEligible,
                proposed.incidentKind, string.Empty,
                new DomainFailure(FailureCode.ExternalInfluenceUnavailable, proposed.operationId,
                    "observed-service-incident-target-owned-by-active-exterior"));
        }
        ServiceIncidentKind incidentKind = proposed.incidentKind;
        SocietyEventWorldSaveData societyBefore = societyState.CaptureSociety();
        SocietyEventAggregateState candidate = societyState.PrepareSociety(societyBefore);
        SocietyEventWorldSaveData state = candidate.Data;
        ObservedMealIncidentEvidenceSaveData existing =
            state.successfulIncidentOperations.FirstOrDefault(value =>
                value != null
                && string.Equals(
                    value.operationId,
                    proposed.operationId,
                    StringComparison.Ordinal));
        if (existing != null)
        {
            if (!SameObservedMealPayload(existing, proposed))
            {
                return new ObservedMealIncidentCaptureResult(
                    ObservedMealIncidentCaptureDisposition
                        .RejectedPayloadConflict,
                    existing.incidentKind,
                    existing.occurrenceInstanceId,
                    new DomainFailure(
                        FailureCode.ExternalInfluenceUnavailable,
                        proposed.operationId,
                        "observed-service-incident-payload-conflict"));
            }
            return new ObservedMealIncidentCaptureResult(
                ObservedMealIncidentCaptureDisposition.ExactReplay,
                existing.incidentKind,
                existing.occurrenceInstanceId,
                DomainFailure.None);
        }
        ServiceIncidentDefinitionSO definition = catalog.ServiceIncidents
            .Where(value => value != null && value.kind == incidentKind
                && CanStartSocietyDefinition(catalog, value, context, state))
            .OrderBy(value => WeightedDefinitionRoll(context, value))
            .ThenBy(value => value.StableId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (definition == null)
        {
            return new ObservedMealIncidentCaptureResult(
                ObservedMealIncidentCaptureDisposition.NotEligible,
                incidentKind,
                string.Empty,
                new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    incidentKind.ToString(),
                    "observed-service-incident-definition-unavailable"));
        }

        bool participantOccupied = state.activeEvents.Any(value =>
            value?.participantCharacterIds?.Contains(
                targetCharacterId.Value,
                StringComparer.Ordinal) == true);
        bool emergencyOccupied = state.activeEvents.Any(value => value != null
            && IsEmergency(((ISocietyEventCatalog)catalog).Require(
                value.definitionId)));
        if (participantOccupied
            || emergencyOccupied
            || !CanStartSocietyDefinition(catalog, definition, context, state))
        {
            return new ObservedMealIncidentCaptureResult(
                ObservedMealIncidentCaptureDisposition.NotEligible,
                incidentKind,
                string.Empty,
                new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    definition.StableId,
                    "observed-service-incident-society-policy-blocked"));
        }

        V20ActiveEventSaveData occurrence = CreateEvent(catalog,
            definition.StableId,
            context,
            DeadlineDays(definition),
            new[] { targetCharacterId.Value },
            definition);
        ObservedMealIncidentEvidenceSaveData frozen = JsonClone(proposed);
        frozen.definitionId = definition.StableId;
        frozen.occurrenceInstanceId = occurrence.instanceId;

        occurrence.hasObservedMealIncident = true;
        occurrence.observedMealIncident = frozen;
        state.activeEvents.Add(occurrence);
        state.successfulIncidentOperations.Add(JsonClone(frozen));
        candidate.Data = eventSaveValidation.ValidateSociety(Clone(state));

        if (incidentFacts == null)
            throw new InvalidOperationException("Observed service-incident fact publisher is unavailable.");
        var fact = new ObservedServiceIncidentFact(frozen);
        var rollback = societyState.PrepareSociety(societyBefore);
        DomainFactLease lease = default;
        bool mutationPublished = false;
        bool accepted = false;
        societyState.BeginObservedPublication();
        try
        {
            if (!incidentFacts.TryReserve(fact, out lease, out string reserveFailure))
                return RejectedObservedIncidentOutcome(incidentKind, frozen.operationId, "reservation-failed:" + reserveFailure);
            societyState.ReplaceObservedCandidate(candidate);
            mutationPublished = true;
            DomainFactDelivery delivery = incidentFacts.Publish(lease, fact);
            if (!delivery.MayBeCommitted)
                return RejectedObservedIncidentOutcome(incidentKind, frozen.operationId, "publication-rejected:" + delivery.Detail);
            // An uncertain acknowledgement retains the real occurrence/history.
            // The journal owns only delivery of this frozen fact, not its effects.
            accepted = true;
        }
        finally
        {
            if (!accepted && mutationPublished) societyState.ReplaceObservedCandidate(rollback);
            try { if (lease.IsValid) incidentFacts.Cancel(lease); }
            finally { societyState.EndObservedPublication(); }
        }
        return new ObservedMealIncidentCaptureResult(
            ObservedMealIncidentCaptureDisposition.Created,
            incidentKind,
            occurrence.instanceId,
            DomainFailure.None);
    }

    private static ObservedMealIncidentCaptureResult
        RejectedObservedIncidentOutcome(
            ServiceIncidentKind incidentKind,
            string operationId,
            string detail)
    => new(
        ObservedMealIncidentCaptureDisposition.NotEligible,
        incidentKind,
        string.Empty,
        new DomainFailure(
            FailureCode.ExternalInfluenceUnavailable,
            operationId ?? string.Empty,
            "observed-service-incident-outcome-" + detail));

    private static ObservedMealIncidentCaptureResult NotEligibleObservedIncident(
        string reason)
    => new(
        ObservedMealIncidentCaptureDisposition.NotEligible,
        default,
        string.Empty,
        new DomainFailure(FailureCode.ExternalInfluenceUnavailable, reason));

    public bool TryResolveObservedMealIncidentTargetDeath(
        CharacterId targetCharacterId,
        int absoluteDay,
        out V20ResolvedEventResult result,
        out DomainFailure failure)
    {
        result = default;
        failure = DomainFailure.None;
        if (!targetCharacterId.IsValid || absoluteDay < 1)
        {
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                targetCharacterId.Value,
                "observed-meal-incident-target-death-invalid");
            return false;
        }

        SocietyEventAggregateState candidate = societyState.PrepareSociety(societyState.CaptureSociety());
        SocietyEventWorldSaveData state = candidate.Data;
        V20ActiveEventSaveData occurrence = state.activeEvents
            .SingleOrDefault(value => value?.hasObservedMealIncident == true
                && string.Equals(
                    value.observedMealIncident.targetCharacterId,
                    targetCharacterId.Value,
                    StringComparison.Ordinal));
        if (occurrence == null)
        {
            return false;
        }

        V20AuthoredContentSO definition =
            ((ISocietyEventCatalog)catalog).Require(occurrence.definitionId);
        occurrence.resolved = true;
        bool cancelledResponse = false;
        if (occurrence.observedIncidentResponse?.phase is
            ObservedIncidentResponsePhase.Accepted
            or ObservedIncidentResponsePhase.ExternalOperationStarted
            or ObservedIncidentResponsePhase.ReceiptCommitted)
        {
            occurrence.observedIncidentResponse.phase =
                ObservedIncidentResponsePhase.Cancelled;
            occurrence.observedIncidentResponse.receiptId = string.Empty;
            occurrence.observedIncidentResponse.failureReason =
                "target-died-before-response-effects";
            cancelledResponse = true;
        }
        occurrence.resolutionId = cancelledResponse
            ? ObservedIncidentResponseFailureResolutionId(
                ObservedIncidentResponsePhase.Cancelled)
            : "target-death";
        FreezeSocietyTerminalOutcome(
            occurrence,
            definition,
            cancelledResponse ? "현장 대응 취소" : "대상 사망으로 종료",
            cancelledResponse
                ? FormatObservedIncidentResponseFailureNarrative(
                    occurrence.observedIncidentResponse)
                : "관측된 사건의 대상이 사망하여 현장 대응을 종료했습니다.",
            Array.Empty<V20ContentEffect>());
        state.activeEvents.Remove(occurrence);
        societyState.AddResolvedSociety(state, occurrence);
        societyState.MarkEventRecurrence(state, definition, occurrence, absoluteDay);
        candidate.Data = eventSaveValidation.ValidateSociety(Clone(state));
        societyState.PublishSociety(candidate);
        result = CreateSocietyResolvedResult(
            occurrence,
            definition,
            Array.Empty<V20ContentEffect>());
        return true;
    }

    public IReadOnlyCollection<CharacterId> GetOwnedCustomerIds()
    {
        List<CharacterId> result = new();
        foreach (V20ActiveEventSaveData occurrence in Society.Data.activeEvents
                     .Where(value => value?.hasObservedMealIncident == true)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            ObservedMealIncidentEvidenceSaveData evidence =
                occurrence.observedMealIncident;
            eventSaveValidation.RequireValidObservedMealEvidence(evidence);
            CharacterId target = new(evidence.targetCharacterId);
            if (occurrence.resolved
                || occurrence.participantCharacterIds == null
                || occurrence.participantCharacterIds.Count != 1
                || !string.Equals(
                    occurrence.participantCharacterIds[0],
                    target.Value,
                    StringComparison.Ordinal)
                || result.Contains(target))
            {
                throw new InvalidOperationException(
                    $"Society incident owner '{occurrence.instanceId}' is invalid or duplicated.");
            }
            result.Add(target);
        }
        return result.AsReadOnly();
    }

    public bool TryRecordObservedIncidentResponseStarted(
        string instanceId,
        string operationId,
        string externalOperationId,
        out string failure)
    {
        failure = string.Empty;
        if (!IsCanonicalRequiredId(operationId)
            || !IsCanonicalRequiredId(externalOperationId))
        {
            failure = "wim040-incident-response-start-identity-invalid";
            return false;
        }
        SocietyEventAggregateState candidate = societyState.PrepareSociety(societyState.CaptureSociety());
        V20ActiveEventSaveData active = FindPendingIncidentResponse(
            candidate.Data,
            instanceId,
            operationId,
            out failure);
        if (active == null)
        {
            return false;
        }
        ObservedIncidentResponseSaveData response =
            active.observedIncidentResponse;
        bool externalIdentityConflict = string.Equals(
                response.choiceId,
                "replace",
                StringComparison.Ordinal)
            && !string.Equals(
                externalOperationId,
                response.operationId,
                StringComparison.Ordinal)
            || string.Equals(
                response.choiceId,
                "transfer",
                StringComparison.Ordinal)
            && !TryClassifyObservedIncidentTransferExternalOperationId(
                active,
                externalOperationId,
                out _);
        if (externalIdentityConflict)
        {
            failure = "wim040-incident-response-external-operation-conflict";
            return false;
        }
        if (response.phase
            == ObservedIncidentResponsePhase.ExternalOperationStarted)
        {
            if (string.Equals(
                    response.externalOperationId,
                    externalOperationId,
                    StringComparison.Ordinal))
            {
                return true;
            }
            failure = "wim040-incident-response-external-operation-conflict";
            return false;
        }
        if (response.phase != ObservedIncidentResponsePhase.Accepted)
        {
            failure = "wim040-incident-response-start-phase-invalid:"
                + response.phase;
            return false;
        }
        response.phase =
            ObservedIncidentResponsePhase.ExternalOperationStarted;
        response.externalOperationId = externalOperationId;
        candidate.Data = eventSaveValidation.ValidateSociety(Clone(candidate.Data));
        societyState.PublishSociety(candidate);
        return true;
    }

    public bool TryRecordObservedIncidentResponseReceipt(
        string instanceId,
        string operationId,
        string externalOperationId,
        string receiptId,
        out string failure)
    {
        failure = string.Empty;
        if (!IsCanonicalRequiredId(operationId)
            || !IsCanonicalRequiredId(externalOperationId)
            || !IsCanonicalRequiredId(receiptId))
        {
            failure = "wim040-incident-response-receipt-identity-invalid";
            return false;
        }
        SocietyEventAggregateState candidate = societyState.PrepareSociety(societyState.CaptureSociety());
        V20ActiveEventSaveData active = FindPendingIncidentResponse(
            candidate.Data,
            instanceId,
            operationId,
            out failure);
        if (active == null)
        {
            return false;
        }
        ObservedIncidentResponseSaveData response =
            active.observedIncidentResponse;
        if (!IsExpectedObservedIncidentResponseReceipt(
                active,
                externalOperationId,
                receiptId))
        {
            failure = "wim040-incident-response-receipt-payload-invalid";
            return false;
        }
        if (response.phase == ObservedIncidentResponsePhase.ReceiptCommitted)
        {
            if (string.Equals(
                    response.externalOperationId,
                    externalOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    response.receiptId,
                    receiptId,
                    StringComparison.Ordinal))
            {
                return true;
            }
            failure = "wim040-incident-response-receipt-conflict";
            return false;
        }
        if (response.phase
                != ObservedIncidentResponsePhase.ExternalOperationStarted
            || !string.Equals(
                response.externalOperationId,
                externalOperationId,
                StringComparison.Ordinal))
        {
            failure = "wim040-incident-response-receipt-phase-invalid";
            return false;
        }
        response.phase = ObservedIncidentResponsePhase.ReceiptCommitted;
        response.receiptId = receiptId;
        candidate.Data = eventSaveValidation.ValidateSociety(Clone(candidate.Data));
        societyState.PublishSociety(candidate);
        return true;
    }

    public bool TryRecordObservedIncidentResponseFailure(
        string instanceId,
        string operationId,
        string failureReason,
        bool cancelled,
        out string failure)
    {
        failure = string.Empty;
        string normalizedReason = Normalize(failureReason);
        if (!IsCanonicalRequiredId(operationId)
            || string.IsNullOrEmpty(normalizedReason))
        {
            failure = "wim040-incident-response-failure-invalid";
            return false;
        }
        SocietyEventAggregateState candidate = societyState.PrepareSociety(societyState.CaptureSociety());
        V20ActiveEventSaveData resolved = candidate.Data.recentResolvedEvents
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.instanceId,
                    Normalize(instanceId),
                    StringComparison.Ordinal));
        if (resolved != null)
        {
            ObservedIncidentResponseSaveData resolvedResponse =
                resolved.observedIncidentResponse;
            ObservedIncidentResponsePhase expectedTerminal = cancelled
                ? ObservedIncidentResponsePhase.Cancelled
                : ObservedIncidentResponsePhase.Failed;
            if (resolved.hasObservedMealIncident
                && resolvedResponse?.phase == expectedTerminal
                && string.Equals(
                    resolvedResponse.operationId,
                    Normalize(operationId),
                    StringComparison.Ordinal)
                && string.Equals(
                    resolvedResponse.failureReason,
                    normalizedReason,
                    StringComparison.Ordinal))
            {
                return true;
            }
            failure = "wim040-incident-response-terminal-conflict";
            return false;
        }
        V20ActiveEventSaveData active = FindPendingIncidentResponse(
            candidate.Data,
            instanceId,
            operationId,
            out failure);
        if (active == null)
        {
            return false;
        }
        ObservedIncidentResponseSaveData response =
            active.observedIncidentResponse;
        ObservedIncidentResponsePhase terminal = cancelled
            ? ObservedIncidentResponsePhase.Cancelled
            : ObservedIncidentResponsePhase.Failed;
        if (response.phase is ObservedIncidentResponsePhase.Failed
            or ObservedIncidentResponsePhase.Cancelled)
        {
            if (response.phase == terminal
                && string.Equals(
                    response.failureReason,
                    normalizedReason,
                    StringComparison.Ordinal))
            {
                return true;
            }
            failure = "wim040-incident-response-terminal-conflict";
            return false;
        }
        if (response.phase is not (
            ObservedIncidentResponsePhase.Accepted
            or ObservedIncidentResponsePhase.ExternalOperationStarted))
        {
            failure = "wim040-incident-response-failure-phase-invalid";
            return false;
        }
        response.phase = terminal;
        response.failureReason = normalizedReason;
        V20AuthoredContentSO definition =
            ((ISocietyEventCatalog)catalog).Require(active.definitionId);
        active.resolved = true;
        active.resolutionId = ObservedIncidentResponseFailureResolutionId(
            terminal);
        FreezeSocietyTerminalOutcome(
            active,
            definition,
            terminal == ObservedIncidentResponsePhase.Cancelled
                ? "현장 대응 취소"
                : "현장 대응 실패",
            FormatObservedIncidentResponseFailureNarrative(response),
            Array.Empty<V20ContentEffect>());
        candidate.Data.activeEvents.Remove(active);
        societyState.AddResolvedSociety(candidate.Data, active);
        societyState.MarkEventRecurrence(
            candidate.Data,
            definition,
            active,
            active.startedAbsoluteDay);
        candidate.Data = eventSaveValidation.ValidateSociety(Clone(candidate.Data));
        societyState.PublishSociety(candidate);
        return true;
    }

    private static V20ActiveEventSaveData FindPendingIncidentResponse(
        SocietyEventWorldSaveData state,
        string instanceId,
        string operationId,
        out string failure)
    {
        V20ActiveEventSaveData active = state.activeEvents.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.instanceId,
                Normalize(instanceId),
                StringComparison.Ordinal));
        ObservedIncidentResponseSaveData response =
            active?.observedIncidentResponse;
        if (active == null
            || active.resolved
            || !active.hasObservedMealIncident
            || response == null
            || response.phase == ObservedIncidentResponsePhase.None
            || !string.Equals(
                response.operationId,
                Normalize(operationId),
                StringComparison.Ordinal))
        {
            failure = "wim040-incident-response-owner-missing";
            return null;
        }
        failure = string.Empty;
        return active;
    }

    private ObservedLifeEventCommitResult RecordObservedLifeEvent(
        ObservedLifeEventSourceKind sourceKind,
        string sourceOperationId,
        string canonicalPayload,
        int absoluteDay,
        int generation,
        IEnumerable<string> sourceParticipants,
        string expectedDefinitionId,
        bool resolveAutomatically = true)
    {
        if (!Enum.IsDefined(typeof(ObservedLifeEventSourceKind), sourceKind)
            || string.IsNullOrWhiteSpace(sourceOperationId)
            || !string.Equals(
                sourceOperationId,
                sourceOperationId.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(canonicalPayload)
            || absoluteDay < 1
            || generation < 0)
            throw new ArgumentException("Observed life-event receipt is invalid.");

        SocietyEventWorldSaveData state = WritableSociety.Data;
        ObservedLifeEventReceiptSaveData[] operationMatches =
            state.successfulLifeEventOperations
                .Where(value => value != null
                    && value.sourceKind == sourceKind
                    && string.Equals(
                        value.sourceOperationId,
                        sourceOperationId,
                        StringComparison.Ordinal))
                .ToArray();
        if (operationMatches.Length > 1)
            throw new InvalidOperationException(
                $"Observed life-event source '{sourceKind}:{sourceOperationId}' has duplicate receipts.");
        if (operationMatches.Length == 1)
        {
            if (string.Equals(
                    operationMatches[0].canonicalPayload,
                    canonicalPayload,
                    StringComparison.Ordinal))
                return new ObservedLifeEventCommitResult(false, null);
            throw new InvalidOperationException(
                $"Observed life-event source '{sourceKind}:{sourceOperationId}' conflicts with its committed receipt.");
        }

        LifeEventDefinitionSO definition = eventSaveValidation.RequireObservedLifeEventDefinition(
            expectedDefinitionId,
            resolveAutomatically);
        V20DailyEventContext assignmentContext = new()
        {
            AbsoluteDay = absoluteDay,
            Generation = generation
        };
        // The irreversible source receipt, not daily event selection, owns
        // creation. Authored requirements were already validated by the
        // observed-definition contract; only authored character recurrence
        // may make this source ineligible.
        string participant = (sourceParticipants ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(Normalize)
            .Where(value => CanAssignLifeEvent(
                definition,
                value,
                assignmentContext,
                state)
                && !state.activeEvents.Any(active => active != null
                    && string.Equals(
                        active.definitionId,
                        definition.StableId,
                        StringComparison.Ordinal)
                    && active.participantCharacterIds?.Contains(
                        value,
                        StringComparer.Ordinal) == true))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (participant == null)
            return new ObservedLifeEventCommitResult(false, null);

        V20ActiveEventSaveData occurrence = CreateObservedLifeEvent(catalog,
            sourceKind,
            sourceOperationId,
            canonicalPayload,
            absoluteDay,
            generation,
            participant,
            definition,
            resolveAutomatically);
        ObservedLifeEventReceiptSaveData durableReceipt = new()
        {
            sourceKind = sourceKind,
            sourceOperationId = sourceOperationId,
            canonicalPayload = canonicalPayload,
            occurrenceInstanceId = occurrence.instanceId
        };
        state.successfulLifeEventOperations.Add(durableReceipt);
        if (!resolveAutomatically)
        {
            state.activeEvents.Add(occurrence);
            return new ObservedLifeEventCommitResult(true, null);
        }

        societyState.AddResolvedSociety(state, occurrence);
        societyState.MarkEventRecurrence(state, definition, occurrence, absoluteDay);
        campaignEffects.ApplyInternalEffects(
            definition.automaticEffects,
            occurrence.contextFactionId);
        return new ObservedLifeEventCommitResult(
            true,
            CreateSocietyResolvedResult(
                occurrence,
                definition,
                definition.automaticEffects));
    }

    private static bool RequiresObservedIncidentOperation(
        ServiceIncidentKind incidentKind,
        string choiceId)
    =>
        incidentKind == ServiceIncidentKind.ForbiddenMeal
            && string.Equals(choiceId, "replace", StringComparison.Ordinal)
        || incidentKind == ServiceIncidentKind.MedicalCollapse
            && (string.Equals(
                    choiceId,
                    "emergency-care",
                    StringComparison.Ordinal)
                || string.Equals(
                    choiceId,
                    "transfer",
                    StringComparison.Ordinal));

    internal bool TrySelectResponseChoice(
        V20ActiveEventSaveData active,
        V20ChoiceDefinition choice,
        out bool deferred,
        out string failure)
    {
        deferred = false;
        failure = string.Empty;
        if (active.hasObservedMealIncident
            && RequiresObservedIncidentOperation(
                active.observedMealIncident?.incidentKind ?? default,
                choice.choiceId))
        {
            ObservedIncidentResponseSaveData response =
                active.observedIncidentResponse ??=
                    new ObservedIncidentResponseSaveData();
            if (response.phase == ObservedIncidentResponsePhase.None)
            {
                if (string.Equals(
                        choice.choiceId,
                        "replace",
                        StringComparison.Ordinal)
                    && (active.observedMealIncident.fieldMeal
                        || !new BuildingInstanceId(
                                active.observedMealIncident.facilityInstanceId)
                            .IsValid))
                {
                    failure = "BLOCKED_CONTRACT:wim040-replacement-meal-facility:"
                        + active.instanceId;
                    return false;
                }
                response.phase = ObservedIncidentResponsePhase.Accepted;
                response.choiceId = choice.choiceId;
                response.operationId = FormatObservedIncidentResponseOperationId(
                    active.instanceId,
                    choice.choiceId);
                active.selectedChoiceId = choice.choiceId;
                deferred = true;
                return true;
            }
            if (!string.Equals(
                    response.choiceId,
                    choice.choiceId,
                    StringComparison.Ordinal))
            {
                failure = "BLOCKED_CONTRACT:wim040-incident-response-choice-owned:"
                    + $"{active.instanceId}:{response.choiceId}";
                return false;
            }
            if (response.phase is ObservedIncidentResponsePhase.Accepted
                or ObservedIncidentResponsePhase.ExternalOperationStarted)
            {
                deferred = true;
                return true;
            }
            if (response.phase
                != ObservedIncidentResponsePhase.ReceiptCommitted)
            {
                failure = "BLOCKED_CONTRACT:wim040-incident-response-state:"
                    + $"{active.instanceId}:{response.phase}";
                return false;
            }
        }

        return true;
    }

    internal void MarkEffectsPublished(V20ActiveEventSaveData active)
    {
        if (active.observedIncidentResponse?.phase
            == ObservedIncidentResponsePhase.ReceiptCommitted)
        {
            active.observedIncidentResponse.phase =
                ObservedIncidentResponsePhase.EffectsPublished;
        }
    }
}
