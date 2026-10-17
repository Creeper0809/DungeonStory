using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DungeonStory.Foundation.Facts;
using static V20CampaignIdentity;
using static V20ContentDefinitionRules;
using static V20ObservedEventEvidenceRules;

internal sealed class V20EventSaveValidation
{
    private readonly V20StoryContentCatalog catalog;
    internal V20EventSaveValidation(V20StoryContentCatalog catalog)
    { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }

    internal LifeEventDefinitionSO RequireObservedLifeEventDefinition(
        string expectedDefinitionId,
        bool resolveAutomatically = true)
    {
        LifeEventDefinitionSO definition = catalog.LifeEvents.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.StableId,
                expectedDefinitionId,
                StringComparison.Ordinal));
        if (definition == null
            || definition.automatic != resolveAutomatically
            || RequireOccurrencePolicy(definition)
                != LifeEventOccurrencePolicy.ExternalObservedOnly
            || definition.frequencyRule != LifeEventFrequencyRule.OncePerCharacter
            || (resolveAutomatically
                ? definition.automaticEffects == null
                    || definition.automaticEffects.Count == 0
                : definition.choices == null
                    || definition.choices.Count < 2
                    || definition.choices.Count > 4
                    || definition.automaticEffects == null
                    || definition.automaticEffects.Count != 0)
            || !RequirementsSatisfied(
                definition.triggerRequirements,
                new RunMilestoneEvaluationSnapshot()))
        {
            throw new InvalidOperationException(
                $"Observed life-event definition '{expectedDefinitionId}' no longer matches its source receipt contract.");
        }
        return definition;
    }
    internal SocietyEventWorldSaveData ValidateSociety(SocietyEventWorldSaveData data)
    {
        if (data == null || data.version != SocietyEventWorldSaveData.CurrentVersion || data.activeEvents == null || data.recentResolvedEvents == null || data.completedOnceEventIds == null || data.recurrenceKeys == null || data.cooldowns == null || data.workDelays == null || data.successfulIncidentOperations == null || data.successfulLifeEventOperations == null || data.lastEvaluationAbsoluteDay < -1)
            throw new InvalidOperationException("Society-event save payload is invalid.");
        if (data.workDelays.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.scopeId)
                || value.untilAbsoluteDayExclusive < 0)
            || data.workDelays.GroupBy(value => value.scopeId, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            throw new InvalidOperationException("Society-event work-delay state is invalid.");
        RequireValidEvents(data.activeEvents.Concat(data.recentResolvedEvents), seasonal: false);
        HashSet<string> incidentOperations = new(StringComparer.Ordinal);
        HashSet<string> incidentOccurrences = new(StringComparer.Ordinal);
        foreach (ObservedMealIncidentEvidenceSaveData evidence in
                 data.successfulIncidentOperations)
        {
            RequireValidObservedMealEvidence(evidence);
            if (!incidentOperations.Add(evidence.operationId)
                || !incidentOccurrences.Add(evidence.occurrenceInstanceId))
            {
                throw new InvalidOperationException(
                    "Observed meal-incident history contains duplicate identities.");
            }
        }
        foreach (V20ActiveEventSaveData occurrence in data.activeEvents
                     .Concat(data.recentResolvedEvents)
                     .Where(value => value?.hasObservedMealIncident == true))
        {
            ObservedMealIncidentEvidenceSaveData recorded =
                data.successfulIncidentOperations.SingleOrDefault(value =>
                    string.Equals(
                        value.operationId,
                        occurrence.observedMealIncident.operationId,
                        StringComparison.Ordinal));
            if (!SameObservedMealEvidence(
                    occurrence.observedMealIncident,
                    recorded))
            {
                throw new InvalidOperationException(
                    $"Observed meal incident '{occurrence.instanceId}' does not match its immutable operation history.");
            }
        }
        RequireValidObservedLifeEventReceipts(data);
        foreach (V20ActiveEventSaveData value in data.activeEvents)
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)catalog).Require(value.definitionId);
            GuestRequestDeliveryOutbox.RequireValidState(
                value,
                definition as GuestRequestDefinitionSO,
                activeCollection: true);
        }
        foreach (V20ActiveEventSaveData value in data.recentResolvedEvents)
        {
            V20AuthoredContentSO definition =
                ((ISocietyEventCatalog)catalog).Require(value.definitionId);
            GuestRequestDeliveryOutbox.RequireValidState(
                value,
                definition as GuestRequestDefinitionSO,
                activeCollection: false);
        }
        HashSet<string> receiptOwnedOccurrenceIds =
            data.successfulLifeEventOperations
                .Where(value => value != null)
                .Select(value => value.occurrenceInstanceId)
                .ToHashSet(StringComparer.Ordinal);
        V20ActiveEventSaveData[] unownedActive = data.activeEvents
            .Where(value => value != null
                && !receiptOwnedOccurrenceIds.Contains(value.instanceId))
            .ToArray();
        int activeEmergency = unownedActive.Count(value =>
            IsEmergency(((ISocietyEventCatalog)catalog).Require(
                value.definitionId)));
        int activeOrdinary = unownedActive.Length - activeEmergency;
        int ordinaryCap = data.lastEvaluationAbsoluteDay is >= 0 and <= 30
            ? 1
            : 2;
        int receiptOwnedResolvedCount = data.recentResolvedEvents.Count(value =>
            value != null
            && receiptOwnedOccurrenceIds.Contains(value.instanceId));
        if (activeEmergency > 1
            || activeOrdinary > ordinaryCap
            || data.recentResolvedEvents.Count - receiptOwnedResolvedCount > 256)
            throw new InvalidOperationException(
                "Society-event history bounds are invalid: "
                + $"ordinary={activeOrdinary}/{ordinaryCap}, "
                + $"emergency={activeEmergency}/1, "
                + $"unowned-resolved={data.recentResolvedEvents.Count - receiptOwnedResolvedCount}/256.");
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

    internal void RequireValidObservedLifeEventReceipts(
        SocietyEventWorldSaveData data)
    {
        HashSet<string> sourceOperations = new(StringComparer.Ordinal);
        HashSet<string> occurrences = new(StringComparer.Ordinal);
        foreach (ObservedLifeEventReceiptSaveData receipt in
                 data.successfulLifeEventOperations)
        {
            if (receipt == null
                || receipt.version != ObservedLifeEventReceiptSaveData.CurrentVersion
                || !Enum.IsDefined(
                    typeof(ObservedLifeEventSourceKind),
                    receipt.sourceKind)
                || string.IsNullOrWhiteSpace(receipt.sourceOperationId)
                || !string.Equals(
                    receipt.sourceOperationId,
                    receipt.sourceOperationId.Trim(),
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(receipt.canonicalPayload)
                || !string.Equals(
                    receipt.occurrenceInstanceId,
                    ObservedLifeOccurrenceInstanceId(
                        receipt.sourceKind,
                        receipt.sourceOperationId),
                    StringComparison.Ordinal)
                || !sourceOperations.Add(
                    $"{(int)receipt.sourceKind}:{receipt.sourceOperationId}")
                || !occurrences.Add(receipt.occurrenceInstanceId))
            {
                throw new InvalidOperationException(
                    "Observed life-event receipt identity is invalid or duplicated.");
            }

            string definitionId;
            int absoluteDay;
            int generation;
            bool resolvedAutomatically;
            string[] eligibleParticipants;
            switch (receipt.sourceKind)
            {
                case ObservedLifeEventSourceKind.Funeral:
                    if (!ObservedFuneralLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedFuneralLifeEventReceipt funeral)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            funeral.SourceOperationId,
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Observed funeral life-event receipt payload is invalid.");
                    definitionId = GraveVisitLifeEventId;
                    absoluteDay = funeral.AbsoluteDay;
                    generation = funeral.Generation;
                    resolvedAutomatically = true;
                    eligibleParticipants = funeral.ParticipantCharacterIds
                        .Select(value => value.Value)
                        .ToArray();
                    break;
                case ObservedLifeEventSourceKind.LineageCompression:
                    if (!ObservedLineageCompressionLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedLineageCompressionLifeEventReceipt lineage)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            lineage.SourceOperationId,
                            StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            "Observed lineage-compression life-event receipt payload is invalid.");
                    definitionId = StoryCompressedLifeEventId;
                    absoluteDay = lineage.ArchiveAbsoluteDay;
                    generation = lineage.Generation;
                    resolvedAutomatically = true;
                    eligibleParticipants = new[] { lineage.CharacterId.Value };
                    break;
                case ObservedLifeEventSourceKind.ProductionDeclaredLoss:
                    if (!ObservedProductionLossLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedProductionLossLifeEventReceipt loss)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            loss.SourceOperationId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Observed production-loss life-event receipt payload is invalid.");
                    }
                    definitionId = ApprenticeMistakeLifeEventId;
                    absoluteDay = loss.AbsoluteDay;
                    generation = loss.Generation;
                    resolvedAutomatically = false;
                    eligibleParticipants = new[]
                    {
                        loss.StudentCharacterId.Value
                    };
                    break;
                case ObservedLifeEventSourceKind.LastMentorshipLesson:
                    if (!ObservedLastLessonLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedLastLessonLifeEventReceipt lesson)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            lesson.SourceOperationId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Observed last-lesson life-event receipt payload is invalid.");
                    }
                    definitionId = LastLessonLifeEventId;
                    absoluteDay = lesson.AbsoluteDay;
                    generation = lesson.Generation;
                    resolvedAutomatically = false;
                    eligibleParticipants = new[]
                    {
                        lesson.MentorCharacterId.Value
                    };
                    break;
                case ObservedLifeEventSourceKind.ProficiencyPromotion:
                    if (!ObservedQuietPromotionLifeEventReceipt.TryParse(
                            receipt.canonicalPayload,
                            out ObservedQuietPromotionLifeEventReceipt promotion)
                        || !string.Equals(
                            receipt.sourceOperationId,
                            promotion.SourceOperationId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Observed quiet-promotion life-event receipt payload is invalid.");
                    }
                    definitionId = QuietPromotionLifeEventId;
                    absoluteDay = promotion.AbsoluteDay;
                    generation = promotion.Generation;
                    resolvedAutomatically = true;
                    eligibleParticipants = new[]
                    {
                        promotion.CharacterId.Value
                    };
                    break;
                default:
                    throw new InvalidOperationException(
                        "Observed life-event receipt source kind is unsupported.");
            }

            LifeEventDefinitionSO definition =
                RequireObservedLifeEventDefinition(
                    definitionId,
                    resolvedAutomatically);
            V20ActiveEventSaveData[] joined = data.activeEvents
                .Concat(data.recentResolvedEvents)
                .Where(value => value != null
                    && string.Equals(
                        value.instanceId,
                        receipt.occurrenceInstanceId,
                        StringComparison.Ordinal))
                .ToArray();
            if (joined.Length != 1)
                throw new InvalidOperationException(
                    $"Observed life-event receipt '{receipt.sourceOperationId}' has no exact resolved occurrence.");
            V20ActiveEventSaveData occurrence = joined[0];
            bool recurrenceCommitted = data.recurrenceKeys.Contains(
                CharacterRecurrenceKey(
                    definitionId,
                    occurrence.participantCharacterIds?.SingleOrDefault()),
                StringComparer.Ordinal);
            bool expectedResolutionState = resolvedAutomatically
                ? occurrence.resolved
                    && string.Equals(
                        occurrence.resolutionId,
                        "automatic",
                        StringComparison.Ordinal)
                    && data.recentResolvedEvents.Contains(occurrence)
                : occurrence.resolved
                    ? !string.IsNullOrWhiteSpace(occurrence.resolutionId)
                        && data.recentResolvedEvents.Contains(occurrence)
                    : string.IsNullOrEmpty(occurrence.resolutionId)
                        && data.activeEvents.Contains(occurrence);
            if (!expectedResolutionState
                || !string.Equals(
                    occurrence.definitionId,
                    definitionId,
                    StringComparison.Ordinal)
                || occurrence.startedAbsoluteDay != absoluteDay
                || occurrence.deadlineAbsoluteDay != (resolvedAutomatically
                    ? absoluteDay
                    : absoluteDay + Math.Max(
                        1,
                        definition.responseDeadlineDays))
                || occurrence.generation != generation
                || occurrence.deterministicRoll
                    != PersistentEntityId.GetStableHash32(
                        receipt.canonicalPayload)
                || occurrence.participantCharacterIds.Count != 1
                || !eligibleParticipants.Contains(
                    occurrence.participantCharacterIds[0],
                    StringComparer.Ordinal)
                || recurrenceCommitted != occurrence.resolved)
            {
                throw new InvalidOperationException(
                    $"Observed life-event receipt '{receipt.sourceOperationId}' conflicts with its resolved occurrence.");
            }
        }

        HashSet<string> owned = data.successfulLifeEventOperations
            .Select(value => value.occurrenceInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        if (data.activeEvents.Any(value => value != null
                && IsObservedLifeEventDefinition(value.definitionId)
                && !owned.Contains(value.instanceId))
            || data.recentResolvedEvents.Any(value => value != null
                && IsObservedLifeEventDefinition(value.definitionId)
                && !owned.Contains(value.instanceId)))
        {
            throw new InvalidOperationException(
                "Observed life-event occurrence has no exact durable source receipt.");
        }
        bool duplicatePendingCharacterRecurrence = data.activeEvents
            .Where(value => value != null
                && IsObservedLifeEventDefinition(value.definitionId))
            .SelectMany(value => value.participantCharacterIds.Select(
                participant => (value.definitionId, participant)))
            .GroupBy(value => value)
            .Any(group => group.Count() > 1);
        if (duplicatePendingCharacterRecurrence)
        {
            throw new InvalidOperationException(
                "Observed life-event state contains duplicate pending character recurrence.");
        }
    }

    internal void RequireValidRecurrenceKey(string key)
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

    internal void RequireValidObservedMealEvidence(
        ObservedMealIncidentEvidenceSaveData evidence)
    {
        if (evidence == null
            || evidence.version
                != ObservedMealIncidentEvidenceSaveData.CurrentVersion
            || !Enum.IsDefined(
                typeof(ObservedIncidentSourceKind),
                evidence.sourceKind)
            || !new CharacterId(evidence.targetCharacterId).IsValid
            || !string.Equals(
                new CharacterId(evidence.targetCharacterId).Value,
                evidence.targetCharacterId,
                StringComparison.Ordinal)
            || !IsCanonicalRequiredId(evidence.definitionId)
            || !IsCanonicalRequiredId(evidence.occurrenceInstanceId)
            || evidence.absoluteDay < 1
            || evidence.observedDead)
        {
            throw new InvalidOperationException(
                "Observed service-incident evidence has an invalid identity or shape.");
        }

        ServiceIncidentDefinitionSO definition =
            ((ISocietyEventCatalog)catalog).Require(evidence.definitionId)
                as ServiceIncidentDefinitionSO;
        if (definition == null
            || definition.kind != evidence.incidentKind)
        {
            throw new InvalidOperationException(
                $"Observed service-incident evidence '{evidence.operationId}' is inconsistent with its definition.");
        }

        ObservedMealIncidentEvidenceSaveData expected = null;
        bool valid;
        bool culturalFieldsEmpty = string.IsNullOrEmpty(
                evidence.instigatorCharacterId)
            && string.IsNullOrEmpty(evidence.instigatorCultureId)
            && string.IsNullOrEmpty(evidence.targetCultureId);
        if (evidence.sourceKind == ObservedIncidentSourceKind.Meal)
        {
            ConsumableOperationId operationId = new(evidence.operationId);
            ItemDefinitionId itemDefinitionId = new(evidence.itemDefinitionId);
            ItemStackId itemStackId = new(evidence.itemStackId);
            valid = operationId.IsValid
                && string.Equals(
                    operationId.Value,
                    evidence.operationId,
                    StringComparison.Ordinal)
                && itemDefinitionId.IsValid
                && string.Equals(
                    itemDefinitionId.Value,
                    evidence.itemDefinitionId,
                    StringComparison.Ordinal)
                && itemStackId.IsValid
                && string.Equals(
                    itemStackId.Value,
                    evidence.itemStackId,
                    StringComparison.Ordinal)
                && string.IsNullOrEmpty(evidence.sourceOperationId)
                && evidence.saleItemId == 0
                && string.IsNullOrEmpty(evidence.itemInstanceId)
                && string.IsNullOrEmpty(evidence.sourceStackId)
                && evidence.quantity == 0
                && evidence.unitMassGrams == 0L
                && string.IsNullOrEmpty(evidence.componentFingerprint)
                && evidence.lossValue == 0
                && string.IsNullOrEmpty(
                    evidence.otherParticipantCharacterId)
                && culturalFieldsEmpty
                && !evidence.customerWasAttacker
                && evidence.actualDamage == 0f
                && evidence.fieldMeal != new BuildingInstanceId(
                    evidence.facilityInstanceId).IsValid
                && TryBuildObservedMealEvidence(
                    new ObservedMealIncidentSnapshot(
                        operationId,
                        new CharacterId(evidence.targetCharacterId),
                        itemDefinitionId,
                        itemStackId,
                        new BuildingInstanceId(evidence.facilityInstanceId),
                        evidence.fieldMeal,
                        new CoreGridCell(evidence.locationX, evidence.locationY),
                        evidence.absoluteDay,
                        evidence.observedPolicyViolation,
                        evidence.observedDowned,
                        evidence.observedDead,
                        observedContaminated:
                            evidence.incidentKind
                                == ServiceIncidentKind.Contamination),
                    evidence.definitionId,
                    evidence.occurrenceInstanceId,
                    out ServiceIncidentKind expectedKind,
                    out expected)
                && expectedKind == evidence.incidentKind;
        }
        else if (evidence.sourceKind == ObservedIncidentSourceKind.Shoplifting)
        {
            valid = string.IsNullOrEmpty(evidence.itemStackId)
                && string.IsNullOrEmpty(
                    evidence.otherParticipantCharacterId)
                && culturalFieldsEmpty
                && !evidence.customerWasAttacker
                && evidence.actualDamage == 0f
                && !evidence.fieldMeal
                && !evidence.observedPolicyViolation
                && !evidence.observedDowned
                && !evidence.observedDead
                && TryBuildObservedShopliftingEvidence(
                    new ObservedShopliftingIncidentSnapshot(
                        evidence.operationId,
                        evidence.sourceOperationId,
                        new CharacterId(evidence.targetCharacterId),
                        new ItemDefinitionId(evidence.itemDefinitionId),
                        evidence.saleItemId,
                        evidence.itemInstanceId,
                        evidence.sourceStackId,
                        evidence.quantity,
                        evidence.unitMassGrams,
                        evidence.componentFingerprint,
                        new BuildingInstanceId(evidence.facilityInstanceId),
                        new CoreGridCell(evidence.locationX, evidence.locationY),
                        evidence.absoluteDay,
                        evidence.lossValue),
                    evidence.definitionId,
                    evidence.occurrenceInstanceId,
                    out expected)
                && evidence.incidentKind == ServiceIncidentKind.Theft;
        }
        else if (evidence.sourceKind == ObservedIncidentSourceKind.Combat)
        {
            valid = string.IsNullOrEmpty(evidence.itemDefinitionId)
                && string.IsNullOrEmpty(evidence.itemStackId)
                && evidence.saleItemId == 0
                && string.IsNullOrEmpty(evidence.itemInstanceId)
                && string.IsNullOrEmpty(evidence.sourceStackId)
                && evidence.quantity == 0
                && evidence.unitMassGrams == 0L
                && string.IsNullOrEmpty(evidence.componentFingerprint)
                && evidence.lossValue == 0
                && culturalFieldsEmpty
                && !evidence.fieldMeal
                && !evidence.observedPolicyViolation
                && !evidence.observedDowned
                && !evidence.observedDead
                && TryBuildObservedBrawlEvidence(
                    new ObservedBrawlIncidentSnapshot(
                        evidence.operationId,
                        new CharacterId(evidence.targetCharacterId),
                        new CharacterId(
                            evidence.otherParticipantCharacterId),
                        evidence.customerWasAttacker,
                        evidence.actualDamage,
                        new BuildingInstanceId(evidence.facilityInstanceId),
                        new CoreGridCell(
                            evidence.locationX,
                            evidence.locationY),
                        evidence.absoluteDay),
                    evidence.definitionId,
                    evidence.occurrenceInstanceId,
                    out expected)
                && evidence.incidentKind == ServiceIncidentKind.Brawl;
        }
        else
        {
            CharacterId instigator = new(evidence.instigatorCharacterId);
            CharacterId customer = new(evidence.targetCharacterId);
            CharacterId other = new(evidence.otherParticipantCharacterId);
            CharacterId target = instigator.Equals(customer)
                ? other
                : customer;
            valid = evidence.sourceKind
                    == ObservedIncidentSourceKind.SocialConflict
                && string.IsNullOrEmpty(evidence.itemDefinitionId)
                && string.IsNullOrEmpty(evidence.itemStackId)
                && evidence.saleItemId == 0
                && string.IsNullOrEmpty(evidence.itemInstanceId)
                && string.IsNullOrEmpty(evidence.sourceStackId)
                && evidence.quantity == 0
                && evidence.unitMassGrams == 0L
                && string.IsNullOrEmpty(evidence.componentFingerprint)
                && evidence.lossValue == 0
                && !evidence.customerWasAttacker
                && evidence.actualDamage == 0f
                && !evidence.fieldMeal
                && !evidence.observedPolicyViolation
                && !evidence.observedDowned
                && !evidence.observedDead
                && TryBuildObservedCulturalConflictEvidence(
                    new ObservedCulturalConflictIncidentSnapshot(
                        evidence.operationId,
                        instigator,
                        target,
                        customer,
                        new SpeciesCultureId(evidence.instigatorCultureId),
                        new SpeciesCultureId(evidence.targetCultureId),
                        new BuildingInstanceId(evidence.facilityInstanceId),
                        new CoreGridCell(
                            evidence.locationX,
                            evidence.locationY),
                        evidence.absoluteDay),
                    evidence.definitionId,
                    evidence.occurrenceInstanceId,
                    out expected)
                && evidence.incidentKind
                    == ServiceIncidentKind.CulturalInsult;
        }
        if (!valid || !SameObservedMealEvidence(evidence, expected))
        {
            throw new InvalidOperationException(
                $"Observed service-incident evidence '{evidence.operationId}' is inconsistent with its committed facts.");
        }
    }

    internal void RequireValidEvents(
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
            if (seasonal)
            {
                if (value.hasObservedMealIncident
                    || HasObservedMealIncidentPayload(
                        value.observedMealIncident)
                    || HasObservedIncidentResponsePayload(
                        value.observedIncidentResponse))
                {
                    throw new InvalidOperationException(
                        $"Seasonal event '{value.instanceId}' contains society incident evidence.");
                }
                SeasonalWorldEventDefinitionSO seasonalDefinition =
                    ((IWorldEventCatalog)catalog).Require(
                        value.definitionId);
                SeasonalWildlifeArrivalRules.RequireValidFrozenState(
                    value.seasonalWildlifeArrival,
                    seasonalDefinition.wildlifeArrivalProfile,
                    value.instanceId);
                SeasonalDriftCargoRules.RequireValidFrozenState(
                    value.seasonalDriftCargo,
                    seasonalDefinition.driftCargoProfile,
                    value.instanceId);
                SeasonalTerminalObservation.ValidateWildlife(value);
                SeasonalTerminalObservation.ValidateCargo(value);
                SeasonalArcaneEventRules.RequireValidFrozenState(
                    value.seasonalManaLightning,
                    seasonalDefinition.manaLightningProfile,
                    value.instanceId);
                SeasonalArcaneEventRules.RequireValidFrozenState(
                    value.seasonalSpecialExpedition,
                    seasonalDefinition.specialExpeditionProfile,
                    value.instanceId);
                continue;
            }

            SeasonalWildlifeArrivalRules.RequireValidFrozenState(
                value.seasonalWildlifeArrival,
                authored: null,
                occurrenceInstanceId: value.instanceId);
            SeasonalDriftCargoRules.RequireValidFrozenState(
                value.seasonalDriftCargo,
                authored: null,
                occurrenceInstanceId: value.instanceId);
            SeasonalTerminalObservation.ValidateWildlife(value);
            SeasonalTerminalObservation.ValidateCargo(value);
            SeasonalArcaneEventRules.RequireValidFrozenState(
                value.seasonalManaLightning,
                authored: null,
                occurrenceInstanceId: value.instanceId);
            SeasonalArcaneEventRules.RequireValidFrozenState(
                value.seasonalSpecialExpedition,
                authored: null,
                occurrenceInstanceId: value.instanceId);

            V20AuthoredContentSO societyDefinition =
                ((ISocietyEventCatalog)catalog).Require(value.definitionId);
            if (societyDefinition is ServiceIncidentDefinitionSO)
            {
                if (!value.hasObservedMealIncident
                    || value.observedMealIncident == null
                    || value.participantCharacterIds.Count != 1
                    || !string.Equals(
                        value.participantCharacterIds[0],
                        value.observedMealIncident.targetCharacterId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        value.instanceId,
                        value.observedMealIncident.occurrenceInstanceId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        value.definitionId,
                        value.observedMealIncident.definitionId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Service incident '{value.instanceId}' has no exact observed-meal evidence owner.");
                }
                RequireValidObservedMealEvidence(value.observedMealIncident);
                RequireValidObservedIncidentResponse(value);
            }
            else if (value.hasObservedMealIncident
                || HasObservedMealIncidentPayload(
                    value.observedMealIncident)
                || HasObservedIncidentResponsePayload(
                    value.observedIncidentResponse))
            {
                throw new InvalidOperationException(
                    $"Non-incident society event '{value.instanceId}' contains observed-meal evidence.");
            }
            SocietyEventRiskContract.RequireValid(
                value.riskTier,
                value.riskReason,
                value.instanceId);
            if (value.terminalEffectKinds == null
                || value.terminalEffectKinds.Any(kind =>
                    kind == V20ContentEffectKind.None
                    || !Enum.IsDefined(typeof(V20ContentEffectKind), kind))
                || value.terminalEffectKinds.Distinct().Count()
                    != value.terminalEffectKinds.Count
                || !value.terminalEffectKinds.SequenceEqual(
                    value.terminalEffectKinds.OrderBy(kind => kind)))
            {
                throw new InvalidOperationException(
                    $"Society event '{value.instanceId}' has invalid terminal effect types.");
            }

            bool hasTerminalText = !string.IsNullOrEmpty(
                    value.terminalActionLabel)
                || !string.IsNullOrEmpty(value.terminalOutcomeNarrative)
                || value.terminalEffectKinds.Count > 0;
            if (value.resolved)
            {
                if (string.IsNullOrWhiteSpace(value.resolutionId)
                    || !IsCanonicalRequiredId(value.resolutionId)
                    || string.IsNullOrWhiteSpace(value.terminalActionLabel)
                    || !string.Equals(
                        value.terminalActionLabel,
                        value.terminalActionLabel.Trim(),
                        StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(
                        value.terminalOutcomeNarrative)
                    || !string.Equals(
                        value.terminalOutcomeNarrative,
                        value.terminalOutcomeNarrative.Trim(),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Resolved society event '{value.instanceId}' has no canonical terminal result.");
                }
            }
            else if (!string.IsNullOrEmpty(value.resolutionId)
                || hasTerminalText)
            {
                throw new InvalidOperationException(
                    $"Pending society event '{value.instanceId}' contains a terminal result.");
            }
        }
    }
}
