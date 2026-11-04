using System;
using System.Collections.Generic;

// characters.world의 값과 null 모양을 보존하는 작업별 독립 복사다.
// DTO의 보정 Clone이나 런타임 상태 변경을 호출하지 않는다.
internal static class DungeonCharacterSaveReadCopy
{
    private static global::CharacterAcquiredTraitAggregateState Copy(global::CharacterAcquiredTraitAggregateState source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitAggregateState
        {
            @formatVersion = source.@formatVersion,
            @instances = CopyList(source.@instances, Copy),
            @lastRequestInvalidation = Copy(source.@lastRequestInvalidation),
            @pendingRequests = CopyList(source.@pendingRequests, Copy),
            @processedMilestones = source.@processedMilestones == null ? null : new List<global::System.Int32>(source.@processedMilestones),
            @revision = source.@revision,
        };
    }

    private static global::CharacterAcquiredTraitEffectOverride Copy(global::CharacterAcquiredTraitEffectOverride source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitEffectOverride
        {
            @bindingId = source.@bindingId,
            @value = source.@value,
        };
    }

    private static global::CharacterAcquiredTraitFormulaCapabilityEnvelope Copy(global::CharacterAcquiredTraitFormulaCapabilityEnvelope source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitFormulaCapabilityEnvelope
        {
            @applicatorId = source.@applicatorId,
            @capabilityId = source.@capabilityId,
            @formatterId = source.@formatterId,
            @parameters = CopyList(source.@parameters, Copy),
        };
    }

    private static global::CharacterAcquiredTraitFormulaParameter Copy(global::CharacterAcquiredTraitFormulaParameter source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitFormulaParameter
        {
            @parameterId = source.@parameterId,
            @units = source.@units,
        };
    }

    private static global::CharacterAcquiredTraitInstanceState Copy(global::CharacterAcquiredTraitInstanceState source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitInstanceState
        {
            @acceptedRevision = source.@acceptedRevision,
            @benefitModuleIds = source.@benefitModuleIds == null ? null : new List<global::System.String>(source.@benefitModuleIds),
            @calculatedCost = source.@calculatedCost,
            @candidatePacketHash = source.@candidatePacketHash,
            @combinationId = source.@combinationId,
            @description = source.@description,
            @displayName = source.@displayName,
            @drawbackCapabilityIds = source.@drawbackCapabilityIds == null ? null : new List<global::System.String>(source.@drawbackCapabilityIds),
            @drawbackCredit = source.@drawbackCredit,
            @drawbackEvidenceQualified = source.@drawbackEvidenceQualified,
            @drawbackId = source.@drawbackId,
            @effectOverrides = CopyList(source.@effectOverrides, Copy),
            @erased = source.@erased,
            @erasedAt = source.@erasedAt,
            @erasedRevision = source.@erasedRevision,
            @erasureAuditId = source.@erasureAuditId,
            @evidenceBindings = CopyList(source.@evidenceBindings, Copy),
            @evidenceFactIds = source.@evidenceFactIds == null ? null : new List<global::System.String>(source.@evidenceFactIds),
            @formulaCapabilities = CopyList(source.@formulaCapabilities, Copy),
            @formulaCatalogSha256 = source.@formulaCatalogSha256,
            @formulaVersion = source.@formulaVersion,
            @instanceId = source.@instanceId,
            @manifestationMilestone = source.@manifestationMilestone,
            @mechanicalDescription = source.@mechanicalDescription,
            @moduleIds = source.@moduleIds == null ? null : new List<global::System.String>(source.@moduleIds),
            @narrativeBudget = source.@narrativeBudget,
            @narrativeFlavor = source.@narrativeFlavor,
            @narrativeReason = source.@narrativeReason,
            @originatingRequestId = source.@originatingRequestId,
            @originatingRequestKey = source.@originatingRequestKey,
            @positiveCost = source.@positiveCost,
            @presentationId = source.@presentationId,
            @presentationOrigin = Copy(source.@presentationOrigin),
            @reactionStates = CopyList(source.@reactionStates, Copy),
            @selectionAuditId = source.@selectionAuditId,
        };
    }

    private static global::CharacterAcquiredTraitModuleOfferState Copy(global::CharacterAcquiredTraitModuleOfferState source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitModuleOfferState
        {
            @moduleId = source.@moduleId,
            @polarity = source.@polarity,
            @semanticDescription = source.@semanticDescription,
        };
    }

    private static global::CharacterAcquiredTraitPendingRequestState Copy(global::CharacterAcquiredTraitPendingRequestState source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitPendingRequestState
        {
            @benefitModuleIds = source.@benefitModuleIds == null ? null : new List<global::System.String>(source.@benefitModuleIds),
            @calculatedCost = source.@calculatedCost,
            @candidatePacketHash = source.@candidatePacketHash,
            @drawbackCapabilityIds = source.@drawbackCapabilityIds == null ? null : new List<global::System.String>(source.@drawbackCapabilityIds),
            @drawbackCredit = source.@drawbackCredit,
            @drawbackEvidenceQualified = source.@drawbackEvidenceQualified,
            @drawbackId = source.@drawbackId,
            @effectOverrides = CopyList(source.@effectOverrides, Copy),
            @evidenceBindings = CopyList(source.@evidenceBindings, Copy),
            @evidenceFactIds = source.@evidenceFactIds == null ? null : new List<global::System.String>(source.@evidenceFactIds),
            @formulaCapabilities = CopyList(source.@formulaCapabilities, Copy),
            @formulaCatalogSha256 = source.@formulaCatalogSha256,
            @formulaVersion = source.@formulaVersion,
            @manifestationMilestone = source.@manifestationMilestone,
            @mechanicalDescription = source.@mechanicalDescription,
            @moduleOffers = CopyList(source.@moduleOffers, Copy),
            @moduleSelectionId = source.@moduleSelectionId,
            @moduleSelectionLocalFacts = CopyList(source.@moduleSelectionLocalFacts, Copy),
            @moduleSelectionLocalFactsHash = source.@moduleSelectionLocalFactsHash,
            @narrativeBudget = source.@narrativeBudget,
            @offeredBenefitModuleIds = source.@offeredBenefitModuleIds == null ? null : new List<global::System.String>(source.@offeredBenefitModuleIds),
            @offeredDrawbackModuleIds = source.@offeredDrawbackModuleIds == null ? null : new List<global::System.String>(source.@offeredDrawbackModuleIds),
            @ownerBinding = Copy(source.@ownerBinding),
            @positiveCost = source.@positiveCost,
            @presentationFailureCount = source.@presentationFailureCount,
            @presentationId = source.@presentationId,
            @presentationState = source.@presentationState,
            @publicContext = Copy(source.@publicContext),
            @publicContextVersion = source.@publicContextVersion,
            @registeredRevision = source.@registeredRevision,
            @requestId = source.@requestId,
            @requestKey = source.@requestKey,
            @submissionAuditId = source.@submissionAuditId,
            @targetPersistentId = source.@targetPersistentId,
        };
    }

    private static global::CharacterAcquiredTraitPresentationOriginState Copy(global::CharacterAcquiredTraitPresentationOriginState source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitPresentationOriginState
        {
            @evidenceFactIds = source.@evidenceFactIds == null ? null : new List<global::System.String>(source.@evidenceFactIds),
            @ownerBinding = Copy(source.@ownerBinding),
            @publicContext = Copy(source.@publicContext),
            @registeredRevision = source.@registeredRevision,
            @version = source.@version,
        };
    }

    private static global::CharacterAcquiredTraitReactionState Copy(global::CharacterAcquiredTraitReactionState source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitReactionState
        {
            @lastTriggerAbsoluteDay = source.@lastTriggerAbsoluteDay,
            @reactionId = source.@reactionId,
            @totalTriggerCount = source.@totalTriggerCount,
            @triggersOnLastDay = source.@triggersOnLastDay,
        };
    }

    private static global::CharacterAcquiredTraitRequestInvalidationState Copy(global::CharacterAcquiredTraitRequestInvalidationState source)
    {
        if (source == null) return null;
        return new global::CharacterAcquiredTraitRequestInvalidationState
        {
            @invalidatedRevision = source.@invalidatedRevision,
            @reason = source.@reason,
            @registeredRevision = source.@registeredRevision,
            @requestId = source.@requestId,
            @requestKey = source.@requestKey,
        };
    }

    private static global::CharacterAutomaticSkillActivationState Copy(global::CharacterAutomaticSkillActivationState source)
    {
        if (source == null) return null;
        return new global::CharacterAutomaticSkillActivationState
        {
            @activationKind = source.@activationKind,
            @eventId = source.@eventId,
            @eventKeySha256 = source.@eventKeySha256,
            @ownerRevision = source.@ownerRevision,
            @skillId = source.@skillId,
            @useCount = source.@useCount,
        };
    }

    private static global::CharacterCarriedItemSaveData Copy(global::CharacterCarriedItemSaveData source)
    {
        if (source == null) return null;
        return new global::CharacterCarriedItemSaveData
        {
            @carriedStackId = source.@carriedStackId,
            @components = CopyList(source.@components, Copy),
            @contamination = source.@contamination,
            @itemId = source.@itemId,
            @itemInstanceId = source.@itemInstanceId,
            @ownerOperationId = source.@ownerOperationId,
            @quantity = source.@quantity,
            @sourceStackId = source.@sourceStackId,
            @wasteOrigin = source.@wasteOrigin,
        };
    }

    private static global::CharacterCarryInventorySaveData Copy(global::CharacterCarryInventorySaveData source)
    {
        if (source == null) return null;
        return new global::CharacterCarryInventorySaveData
        {
            @items = CopyList(source.@items, Copy),
        };
    }

    private static global::CharacterEventContext Copy(global::CharacterEventContext source)
    {
        if (source == null) return null;
        return new global::CharacterEventContext
        {
            @actorDisplayName = source.@actorDisplayName,
            @actorId = source.@actorId,
            @chainId = source.@chainId,
            @counterpartyDisplayName = source.@counterpartyDisplayName,
            @counterpartyId = source.@counterpartyId,
            @eventInstanceId = source.@eventInstanceId,
            @locationDisplayName = source.@locationDisplayName,
            @locationId = source.@locationId,
            @occurredDay = source.@occurredDay,
            @resultDetail = source.@resultDetail,
            @sequence = source.@sequence,
            @usedObjectDisplayName = source.@usedObjectDisplayName,
            @usedObjectId = source.@usedObjectId,
        };
    }

    private static global::CharacterExpeditionRecoveryState Copy(global::CharacterExpeditionRecoveryState source)
    {
        if (source == null) return null;
        return new global::CharacterExpeditionRecoveryState
        {
            @stress = source.@stress,
        };
    }

    private static global::CharacterGrowthState Copy(global::CharacterGrowthState source)
    {
        if (source == null) return null;
        return new global::CharacterGrowthState
        {
            @activeSkills = CopyList(source.@activeSkills, Copy),
            @autoChooseDrafts = source.@autoChooseDrafts,
            @displayName = source.@displayName,
            @drafts = CopyList(source.@drafts, Copy),
            @generationSeed = source.@generationSeed,
            @initialized = source.@initialized,
            @nextActiveDraftHasPity = source.@nextActiveDraftHasPity,
            @origin = source.@origin,
            @passiveSkills = CopyList(source.@passiveSkills, Copy),
            @pendingRequestKeys = source.@pendingRequestKeys == null ? null : new List<global::System.String>(source.@pendingRequestKeys),
            @potentialGrade = source.@potentialGrade,
            @skillAcquisitionRevision = source.@skillAcquisitionRevision,
            @skillGenerationRevision = source.@skillGenerationRevision,
            @startingProficiencies = CopyList(source.@startingProficiencies, Copy),
            @startingProfile = Copy(source.@startingProfile),
            @traitIds = source.@traitIds == null ? null : new List<global::System.Int32>(source.@traitIds),
            @traitSelectionAuthorityOrigin = source.@traitSelectionAuthorityOrigin,
            @traitSelectionAuthorityVersion = source.@traitSelectionAuthorityVersion,
            @ultimate = Copy(source.@ultimate),
            @useLimits = Copy(source.@useLimits),
        };
    }

    private static global::CharacterManualSkillBuffState Copy(global::CharacterManualSkillBuffState source)
    {
        if (source == null) return null;
        return new global::CharacterManualSkillBuffState
        {
            @expiresAbsoluteHour = source.@expiresAbsoluteHour,
            @frozenSkill = Copy(source.@frozenSkill),
            @skillId = source.@skillId,
            @sourceCharacterId = source.@sourceCharacterId,
        };
    }

    private static global::CharacterManualSkillCooldownState Copy(global::CharacterManualSkillCooldownState source)
    {
        if (source == null) return null;
        return new global::CharacterManualSkillCooldownState
        {
            @readyAbsoluteHour = source.@readyAbsoluteHour,
            @skillId = source.@skillId,
            @useCount = source.@useCount,
        };
    }

    private static global::CharacterNarrativeFact Copy(global::CharacterNarrativeFact source)
    {
        if (source == null) return null;
        return new global::CharacterNarrativeFact
        {
            @actionKey = source.@actionKey,
            @count = source.@count,
            @domain = source.@domain,
            @eventGroupKey = source.@eventGroupKey,
            @factId = source.@factId,
            @importancePoints = source.@importancePoints,
            @influenceUseCount = source.@influenceUseCount,
            @lastDay = source.@lastDay,
            @lastEventContext = Copy(source.@lastEventContext),
            @milestoneCount = source.@milestoneCount,
            @outcome = source.@outcome,
            @relationshipKey = source.@relationshipKey,
            @sourceEventIds = source.@sourceEventIds == null ? null : new List<global::System.String>(source.@sourceEventIds),
            @subjectId = source.@subjectId,
            @totalValue = source.@totalValue,
        };
    }

    private static global::CharacterNarrativeLedger Copy(global::CharacterNarrativeLedger source)
    {
        if (source == null) return null;
        return new global::CharacterNarrativeLedger
        {
            @facts = CopyList(source.@facts, Copy),
            @nextEventSequence = source.@nextEventSequence,
        };
    }

    private static global::CharacterNarrativeRequestOwnerBinding Copy(global::CharacterNarrativeRequestOwnerBinding source)
    {
        if (source == null) return null;
        return new global::CharacterNarrativeRequestOwnerBinding
        {
            @generationRevision = source.@generationRevision,
            @lifecycleRevision = source.@lifecycleRevision,
            @persistentId = source.@persistentId,
            @version = source.@version,
        };
    }

    private static global::CharacterRestAppliedFactData Copy(global::CharacterRestAppliedFactData source)
    {
        if (source == null) return null;
        return new global::CharacterRestAppliedFactData
        {
            @absoluteDay = source.@absoluteDay,
            @activeConditions = source.@activeConditions == null ? null : new List<global::System.String>(source.@activeConditions),
            @after = source.@after,
            @before = source.@before,
            @characterId = source.@characterId,
            @context = Copy(source.@context),
            @displayName = source.@displayName,
        };
    }

    private static global::CharacterRestObservationSaveData Copy(global::CharacterRestObservationSaveData source)
    {
        if (source == null) return null;
        return new global::CharacterRestObservationSaveData
        {
            @deliveryCompleted = source.@deliveryCompleted,
            @fact = Copy(source.@fact),
            @notificationsAttempted = source.@notificationsAttempted,
            @observationCommitted = source.@observationCommitted,
            @publicationKey = source.@publicationKey,
            @rollbackBlocked = source.@rollbackBlocked,
            @rollbackFailure = source.@rollbackFailure,
        };
    }

    private static global::CharacterSkillActivationCommitState Copy(global::CharacterSkillActivationCommitState source)
    {
        if (source == null) return null;
        return new global::CharacterSkillActivationCommitState
        {
            @absoluteHour = source.@absoluteHour,
            @activationKind = source.@activationKind,
            @canonicalPayloadHash = source.@canonicalPayloadHash,
            @expiresAbsoluteHour = source.@expiresAbsoluteHour,
            @mechanicsSha256 = source.@mechanicsSha256,
            @observationPending = source.@observationPending,
            @operationId = source.@operationId,
            @ownerRevision = source.@ownerRevision,
            @readyAbsoluteHourAfter = source.@readyAbsoluteHourAfter,
            @readyAbsoluteHourBefore = source.@readyAbsoluteHourBefore,
            @recipientCharacterIds = source.@recipientCharacterIds == null ? null : new List<global::System.String>(source.@recipientCharacterIds),
            @recipientStateSha256 = source.@recipientStateSha256,
            @resultKey = source.@resultKey,
            @skillId = source.@skillId,
            @triggerEventId = source.@triggerEventId,
            @triggerEventKeySha256 = source.@triggerEventKeySha256,
            @triggerSerial = source.@triggerSerial,
            @ultimateDomain = source.@ultimateDomain,
            @useCountAfter = source.@useCountAfter,
            @useCountBefore = source.@useCountBefore,
        };
    }

    private static global::CharacterSkillCandidateRule Copy(global::CharacterSkillCandidateRule source)
    {
        if (source == null) return null;
        return new global::CharacterSkillCandidateRule
        {
            @allowedModuleIds = source.@allowedModuleIds == null ? null : new List<global::System.String>(source.@allowedModuleIds),
            @allowedVariantIds = source.@allowedVariantIds == null ? null : new List<global::System.String>(source.@allowedVariantIds),
            @areaSize = source.@areaSize,
            @budget = source.@budget,
            @cooldownTurns = source.@cooldownTurns,
            @effectArea = source.@effectArea,
            @manualCooldownDays = source.@manualCooldownDays,
            @manualDurationHours = source.@manualDurationHours,
            @mechanicalPolicySource = source.@mechanicalPolicySource,
            @rarity = source.@rarity,
            @ruleId = source.@ruleId,
            @target = source.@target,
            @targetingMode = source.@targetingMode,
            @targetPositions = source.@targetPositions,
            @trigger = source.@trigger,
            @ultimateDomain = source.@ultimateDomain,
            @usableFrom = source.@usableFrom,
        };
    }

    private static global::CharacterSkillDraft Copy(global::CharacterSkillDraft source)
    {
        if (source == null) return null;
        return new global::CharacterSkillDraft
        {
            @candidates = CopyList(source.@candidates, Copy),
            @chosenIndex = source.@chosenIndex,
            @formulaBudget = source.@formulaBudget,
            @formulaCatalogSha256 = source.@formulaCatalogSha256,
            @formulaVersion = source.@formulaVersion,
            @frozenMechanics = CopyList(source.@frozenMechanics, Copy),
            @grantsUpperRarityPity = source.@grantsUpperRarityPity,
            @isReady = source.@isReady,
            @kind = source.@kind,
            @moduleSelectionOffers = CopyList(source.@moduleSelectionOffers, Copy),
            @nextPresentationIndex = source.@nextPresentationIndex,
            @outcomeEvidenceBindings = CopyList(source.@outcomeEvidenceBindings, Copy),
            @ownerBinding = Copy(source.@ownerBinding),
            @ownerInvalidationReason = source.@ownerInvalidationReason,
            @permanentlyChosen = source.@permanentlyChosen,
            @presentationCorrection = source.@presentationCorrection,
            @presentationFailureCount = source.@presentationFailureCount,
            @presentationState = source.@presentationState,
            @requestContext = Copy(source.@requestContext),
            @requestContextHash = source.@requestContextHash,
            @requestContextVersion = source.@requestContextVersion,
            @requestedUltimateDomain = source.@requestedUltimateDomain,
            @requestKey = source.@requestKey,
            @requestSubmitted = source.@requestSubmitted,
            @rules = CopyList(source.@rules, Copy),
            @unlockLevel = source.@unlockLevel,
        };
    }

    private static global::CharacterSkillDrawbackEffectEnvelope Copy(global::CharacterSkillDrawbackEffectEnvelope source)
    {
        if (source == null) return null;
        return new global::CharacterSkillDrawbackEffectEnvelope
        {
            @displayName = source.@displayName,
            @drawbackModuleId = source.@drawbackModuleId,
            @effectId = source.@effectId,
            @operation = source.@operation,
            @severityUnits = source.@severityUnits,
            @targetId = source.@targetId,
            @value = source.@value,
        };
    }

    private static global::CharacterSkillFormulaCapabilityEnvelope Copy(global::CharacterSkillFormulaCapabilityEnvelope source)
    {
        if (source == null) return null;
        return new global::CharacterSkillFormulaCapabilityEnvelope
        {
            @applicatorId = source.@applicatorId,
            @capabilityId = source.@capabilityId,
            @formatterId = source.@formatterId,
            @parameters = CopyList(source.@parameters, Copy),
        };
    }

    private static global::CharacterSkillFormulaParameter Copy(global::CharacterSkillFormulaParameter source)
    {
        if (source == null) return null;
        return new global::CharacterSkillFormulaParameter
        {
            @parameterId = source.@parameterId,
            @units = source.@units,
        };
    }

    private static global::CharacterSkillInstance Copy(global::CharacterSkillInstance source)
    {
        if (source == null) return null;
        return new global::CharacterSkillInstance
        {
            @areaSize = source.@areaSize,
            @calculatedCost = source.@calculatedCost,
            @combinationId = source.@combinationId,
            @cooldownTurns = source.@cooldownTurns,
            @description = source.@description,
            @displayName = source.@displayName,
            @drawbackCredit = source.@drawbackCredit,
            @drawbackEffect = Copy(source.@drawbackEffect),
            @drawbackEvidenceQualified = source.@drawbackEvidenceQualified,
            @drawbackId = source.@drawbackId,
            @effectArea = source.@effectArea,
            @evidenceBindings = CopyList(source.@evidenceBindings, Copy),
            @evidenceIds = source.@evidenceIds == null ? null : new List<global::System.String>(source.@evidenceIds),
            @formulaCapabilities = CopyList(source.@formulaCapabilities, Copy),
            @formulaCatalogSha256 = source.@formulaCatalogSha256,
            @formulaVersion = source.@formulaVersion,
            @id = source.@id,
            @kind = source.@kind,
            @manualCooldownDays = source.@manualCooldownDays,
            @manualDurationHours = source.@manualDurationHours,
            @mechanicalDescription = source.@mechanicalDescription,
            @modules = CopyList(source.@modules, Copy),
            @narrativeBudget = source.@narrativeBudget,
            @narrativeFlavor = source.@narrativeFlavor,
            @narrativeReason = source.@narrativeReason,
            @narrativeTrace = Copy(source.@narrativeTrace),
            @positiveCost = source.@positiveCost,
            @presentationId = source.@presentationId,
            @primaryCapabilityId = source.@primaryCapabilityId,
            @prominencePolicyVersion = source.@prominencePolicyVersion,
            @rarity = source.@rarity,
            @requestKey = source.@requestKey,
            @ruleId = source.@ruleId,
            @target = source.@target,
            @targetingMode = source.@targetingMode,
            @targetPositions = source.@targetPositions,
            @trigger = source.@trigger,
            @ultimateDomain = source.@ultimateDomain,
            @usableFrom = source.@usableFrom,
        };
    }

    private static global::CharacterSkillModuleOfferState Copy(global::CharacterSkillModuleOfferState source)
    {
        if (source == null) return null;
        return new global::CharacterSkillModuleOfferState
        {
            @drawbackModuleIds = source.@drawbackModuleIds == null ? null : new List<global::System.String>(source.@drawbackModuleIds),
            @evidenceFactIds = source.@evidenceFactIds == null ? null : new List<global::System.String>(source.@evidenceFactIds),
            @maximumDrawbackModules = source.@maximumDrawbackModules,
            @maximumPositiveModules = source.@maximumPositiveModules,
            @positiveModuleIds = source.@positiveModuleIds == null ? null : new List<global::System.String>(source.@positiveModuleIds),
            @qualifiedNegativeEvidenceFactIds = source.@qualifiedNegativeEvidenceFactIds == null ? null : new List<global::System.String>(source.@qualifiedNegativeEvidenceFactIds),
            @ruleId = source.@ruleId,
            @selectionId = source.@selectionId,
        };
    }

    private static global::CharacterSkillModuleSelection Copy(global::CharacterSkillModuleSelection source)
    {
        if (source == null) return null;
        return new global::CharacterSkillModuleSelection
        {
            @moduleId = source.@moduleId,
            @variantId = source.@variantId,
        };
    }

    private static global::CharacterSkillRequestContextSnapshot Copy(global::CharacterSkillRequestContextSnapshot source)
    {
        if (source == null) return null;
        return new global::CharacterSkillRequestContextSnapshot
        {
            @characterName = source.@characterName,
            @origin = source.@origin,
            @potential = source.@potential,
            @publicContext = Copy(source.@publicContext),
            @species = source.@species,
            @version = source.@version,
        };
    }

    private static global::CharacterSkillUseLimitState Copy(global::CharacterSkillUseLimitState source)
    {
        if (source == null) return null;
        return new global::CharacterSkillUseLimitState
        {
            @activationSequence = source.@activationSequence,
            @automaticSkillActivations = CopyList(source.@automaticSkillActivations, Copy),
            @defenseInvasionSerial = source.@defenseInvasionSerial,
            @lastSkillActivation = Copy(source.@lastSkillActivation),
            @managementOperatingDay = source.@managementOperatingDay,
            @manualSkillBuffs = CopyList(source.@manualSkillBuffs, Copy),
            @manualSkillCooldowns = CopyList(source.@manualSkillCooldowns, Copy),
            @offenseBattleSerial = source.@offenseBattleSerial,
        };
    }

    private static global::CharacterSocialMemorySnapshot Copy(global::CharacterSocialMemorySnapshot source)
    {
        if (source == null) return null;
        return new global::CharacterSocialMemorySnapshot
        {
            @characterSentiments = CopyList(source.@characterSentiments, Copy),
            @facilitySentiments = CopyList(source.@facilitySentiments, Copy),
            @recentRumors = CopyList(source.@recentRumors, Copy),
            @sourceTrust = CopyList(source.@sourceTrust, Copy),
        };
    }

    private static global::CharacterStartingProficiencyExperience Copy(global::CharacterStartingProficiencyExperience source)
    {
        if (source == null) return null;
        return new global::CharacterStartingProficiencyExperience
        {
            @experience = source.@experience,
            @learningMultiplier = source.@learningMultiplier,
            @proficiencyId = source.@proficiencyId,
        };
    }

    private static global::CharacterStartingProfileState Copy(global::CharacterStartingProfileState source)
    {
        if (source == null) return null;
        return new global::CharacterStartingProfileState
        {
            @ageBand = source.@ageBand,
            @biologicalAgeYears = source.@biologicalAgeYears,
            @historyDisplayName = source.@historyDisplayName,
            @historyId = source.@historyId,
            @initialAgeConditionIds = source.@initialAgeConditionIds == null ? null : new List<global::System.String>(source.@initialAgeConditionIds),
            @originDisplayName = source.@originDisplayName,
            @originId = source.@originId,
            @prepared = source.@prepared,
            @primaryProficiencyId = source.@primaryProficiencyId,
            @proficiencyCap = source.@proficiencyCap,
            @secondaryProficiencyId = source.@secondaryProficiencyId,
        };
    }

    private static global::CharacterVisitorExitOperationSaveData Copy(global::CharacterVisitorExitOperationSaveData source)
    {
        if (source == null) return null;
        return new global::CharacterVisitorExitOperationSaveData
        {
            @absoluteDay = source.@absoluteDay,
            @departurePublicationKey = source.@departurePublicationKey,
            @equipmentCountBefore = source.@equipmentCountBefore,
            @failureReason = source.@failureReason,
            @lifecycleBefore = source.@lifecycleBefore,
            @operationId = source.@operationId,
            @ownerRevision = source.@ownerRevision,
            @phase = source.@phase,
            @sourceMacro = source.@sourceMacro,
            @terminalKind = source.@terminalKind,
            @theftPublicationKey = source.@theftPublicationKey,
            @visitCountBefore = source.@visitCountBefore,
            @visitingBefore = source.@visitingBefore,
        };
    }

    private static global::DungeonCharacterConditionSaveData Copy(global::DungeonCharacterConditionSaveData source)
    {
        if (source == null) return null;
        return new global::DungeonCharacterConditionSaveData
        {
            @condition = source.@condition,
            @value = source.@value,
        };
    }

    private static global::DungeonCharacterMoodFactorSaveData Copy(global::DungeonCharacterMoodFactorSaveData source)
    {
        if (source == null) return null;
        return new global::DungeonCharacterMoodFactorSaveData
        {
            @id = source.@id,
            @label = source.@label,
            @remainingSeconds = source.@remainingSeconds,
            @value = source.@value,
        };
    }

    private static global::DungeonCharacterPersonaNarrativeSaveData Copy(global::DungeonCharacterPersonaNarrativeSaveData source)
    {
        if (source == null) return null;
        return new global::DungeonCharacterPersonaNarrativeSaveData
        {
            @flavorText = source.@flavorText,
            @narrativeTrace = Copy(source.@narrativeTrace),
            @personaName = source.@personaName,
        };
    }

    private static global::DungeonCharacterSaveData Copy(global::DungeonCharacterSaveData source)
    {
        if (source == null) return null;
        return new global::DungeonCharacterSaveData
        {
            @acquiredTraits = Copy(source.@acquiredTraits),
            @baseMood = source.@baseMood,
            @carryInventory = Copy(source.@carryInventory),
            @characterType = source.@characterType,
            @conditions = CopyList(source.@conditions, Copy),
            @currentExperience = source.@currentExperience,
            @currentHealth = source.@currentHealth,
            @dataId = source.@dataId,
            @displayName = source.@displayName,
            @dutyState = source.@dutyState,
            @equippedSkillIds = source.@equippedSkillIds == null ? null : new List<global::System.String>(source.@equippedSkillIds),
            @expeditionRecovery = Copy(source.@expeditionRecovery),
            @gridX = source.@gridX,
            @gridY = source.@gridY,
            @growth = Copy(source.@growth),
            @haulDeliveryIntent = Copy(source.@haulDeliveryIntent),
            @holdingMoney = source.@holdingMoney,
            @injurySeverity = source.@injurySeverity,
            @isOwner = source.@isOwner,
            @learnedSkillIds = source.@learnedSkillIds == null ? null : new List<global::System.String>(source.@learnedSkillIds),
            @level = source.@level,
            @lifecycleRevision = source.@lifecycleRevision,
            @lifecycleState = source.@lifecycleState,
            @lookAroundCount = source.@lookAroundCount,
            @moodFactors = CopyList(source.@moodFactors, Copy),
            @narrative = Copy(source.@narrative),
            @pendingRetailPurchases = CopyList(source.@pendingRetailPurchases, Copy),
            @persistentId = source.@persistentId,
            @personaNarrative = Copy(source.@personaNarrative),
            @recentLogEntries = source.@recentLogEntries == null ? null : new List<global::System.String>(source.@recentLogEntries),
            @restObservation = Copy(source.@restObservation),
            @role = source.@role,
            @socialMemory = Copy(source.@socialMemory),
            @societyResponseMovementDestinationX = source.@societyResponseMovementDestinationX,
            @societyResponseMovementDestinationY = source.@societyResponseMovementDestinationY,
            @societyResponseMovementExternalOperationId = source.@societyResponseMovementExternalOperationId,
            @societyResponseMovementFacilityId = source.@societyResponseMovementFacilityId,
            @societyResponseMovementOperationId = source.@societyResponseMovementOperationId,
            @societyResponseMovementReceiptId = source.@societyResponseMovementReceiptId,
            @visitCount = source.@visitCount,
            @workPriorities = CopyList(source.@workPriorities, Copy),
        };
    }

    private static global::DungeonCharacterWorkPrioritySaveData Copy(global::DungeonCharacterWorkPrioritySaveData source)
    {
        if (source == null) return null;
        return new global::DungeonCharacterWorkPrioritySaveData
        {
            @priority = source.@priority,
            @workTypeId = source.@workTypeId,
        };
    }

    internal static global::DungeonCharacterWorldSaveData Copy(global::DungeonCharacterWorldSaveData source)
    {
        if (source == null) return null;
        return new global::DungeonCharacterWorldSaveData
        {
            @actors = CopyList(source.@actors, Copy),
            @globalFacilityReputation = Copy(source.@globalFacilityReputation),
            @populationProfiles = CopyList(source.@populationProfiles, Copy),
        };
    }

    private static global::GameplayExperienceAssessmentSnapshot Copy(global::GameplayExperienceAssessmentSnapshot source)
    {
        if (source == null) return null;
        return new global::GameplayExperienceAssessmentSnapshot
        {
            @category = source.@category,
            @evidenceFactIds = source.@evidenceFactIds == null ? null : new List<global::System.String>(source.@evidenceFactIds),
            @evidenceMetricIds = source.@evidenceMetricIds == null ? null : new List<global::System.String>(source.@evidenceMetricIds),
            @evidenceRoleIds = source.@evidenceRoleIds == null ? null : new List<global::System.String>(source.@evidenceRoleIds),
            @explanationKo = source.@explanationKo,
            @importance = source.@importance,
            @observerId = source.@observerId,
            @policyVersion = source.@policyVersion,
            @reasonCode = source.@reasonCode,
        };
    }

    private static global::GameplayNarrativeEventContext Copy(global::GameplayNarrativeEventContext source)
    {
        if (source == null) return null;
        return new global::GameplayNarrativeEventContext
        {
            @actorDisplayName = source.@actorDisplayName,
            @actorId = source.@actorId,
            @chainId = source.@chainId,
            @counterpartyDisplayName = source.@counterpartyDisplayName,
            @counterpartyId = source.@counterpartyId,
            @eventInstanceId = source.@eventInstanceId,
            @locationDisplayName = source.@locationDisplayName,
            @locationId = source.@locationId,
            @occurredDay = source.@occurredDay,
            @resultDetail = source.@resultDetail,
            @sequence = source.@sequence,
            @usedObjectDisplayName = source.@usedObjectDisplayName,
            @usedObjectId = source.@usedObjectId,
        };
    }

    private static global::GameplayOutcomeEvidenceBindingSnapshot Copy(global::GameplayOutcomeEvidenceBindingSnapshot source)
    {
        if (source == null) return null;
        return new global::GameplayOutcomeEvidenceBindingSnapshot
        {
            @anchorRevision = source.@anchorRevision,
            @canonicalFactText = source.@canonicalFactText,
            @causationRelationId = source.@causationRelationId,
            @compactedMemoryId = source.@compactedMemoryId,
            @experience = Copy(source.@experience),
            @factIds = source.@factIds == null ? null : new List<global::System.String>(source.@factIds),
            @facts = CopyList(source.@facts, Copy),
            @firstDay = source.@firstDay,
            @influenceRevision = source.@influenceRevision,
            @influenceUseCount = source.@influenceUseCount,
            @lastDay = source.@lastDay,
            @locationId = source.@locationId,
            @locationX = source.@locationX,
            @locationY = source.@locationY,
            @metricIds = source.@metricIds == null ? null : new List<global::System.String>(source.@metricIds),
            @metricReferenceIds = source.@metricReferenceIds == null ? null : new List<global::System.String>(source.@metricReferenceIds),
            @metrics = CopyList(source.@metrics, Copy),
            @occurrenceCount = source.@occurrenceCount,
            @operationId = source.@operationId,
            @outcomeRunId = source.@outcomeRunId,
            @outcomeSequence = source.@outcomeSequence,
            @outcomeTypeId = source.@outcomeTypeId,
            @parentOutcomeId = source.@parentOutcomeId,
            @participants = CopyList(source.@participants, Copy),
            @perspectiveKind = source.@perspectiveKind,
            @provenance = CopyList(source.@provenance, Copy),
            @publicFactId = source.@publicFactId,
            @roleIds = source.@roleIds == null ? null : new List<global::System.String>(source.@roleIds),
            @roomId = source.@roomId,
            @rootOperationId = source.@rootOperationId,
            @schemaVersion = source.@schemaVersion,
            @semanticTags = source.@semanticTags == null ? null : new List<global::System.String>(source.@semanticTags),
            @sourceKind = source.@sourceKind,
            @status = source.@status,
            @subjectId = source.@subjectId,
            @subjectKindId = source.@subjectKindId,
            @subjectSalience = source.@subjectSalience,
        };
    }

    private static global::GameplayOutcomeEvidenceFactBindingSnapshot Copy(global::GameplayOutcomeEvidenceFactBindingSnapshot source)
    {
        if (source == null) return null;
        return new global::GameplayOutcomeEvidenceFactBindingSnapshot
        {
            @factId = source.@factId,
            @value = source.@value,
        };
    }

    private static global::GameplayOutcomeEvidenceMetricBindingSnapshot Copy(global::GameplayOutcomeEvidenceMetricBindingSnapshot source)
    {
        if (source == null) return null;
        return new global::GameplayOutcomeEvidenceMetricBindingSnapshot
        {
            @maximum = source.@maximum,
            @metricId = source.@metricId,
            @minimum = source.@minimum,
            @referenceId = source.@referenceId,
            @referenceKindId = source.@referenceKindId,
            @sampleCount = source.@sampleCount,
            @unitId = source.@unitId,
            @value = source.@value,
        };
    }

    private static global::GameplayOutcomeEvidenceParticipantBindingSnapshot Copy(global::GameplayOutcomeEvidenceParticipantBindingSnapshot source)
    {
        if (source == null) return null;
        return new global::GameplayOutcomeEvidenceParticipantBindingSnapshot
        {
            @displayRevision = source.@displayRevision,
            @displayText = source.@displayText,
            @entityId = source.@entityId,
            @entityKindId = source.@entityKindId,
            @finalConsonant = source.@finalConsonant,
            @hasPerceptionEvidence = source.@hasPerceptionEvidence,
            @locale = source.@locale,
            @participationKind = source.@participationKind,
            @pronunciationMode = source.@pronunciationMode,
            @pronunciationRevision = source.@pronunciationRevision,
            @pronunciationValue = source.@pronunciationValue,
            @roleId = source.@roleId,
        };
    }

    private static global::GameplayOutcomeEvidenceProvenanceBindingSnapshot Copy(global::GameplayOutcomeEvidenceProvenanceBindingSnapshot source)
    {
        if (source == null) return null;
        return new global::GameplayOutcomeEvidenceProvenanceBindingSnapshot
        {
            @kindId = source.@kindId,
            @value = source.@value,
        };
    }

    private static global::GlobalFacilityReputationSnapshot Copy(global::GlobalFacilityReputationSnapshot source)
    {
        if (source == null) return null;
        return new global::GlobalFacilityReputationSnapshot
        {
            @reputation = CopyList(source.@reputation, Copy),
            @rumors = CopyList(source.@rumors, Copy),
        };
    }

    private static global::HaulDeliveryIntentSaveData Copy(global::HaulDeliveryIntentSaveData source)
    {
        if (source == null) return null;
        return new global::HaulDeliveryIntentSaveData
        {
            @commitments = CopyList(source.@commitments, Copy),
            @deliveryGridX = source.@deliveryGridX,
            @deliveryGridY = source.@deliveryGridY,
            @destinationId = source.@destinationId,
            @destinationKind = source.@destinationKind,
            @dropGridX = source.@dropGridX,
            @dropGridY = source.@dropGridY,
            @operationId = source.@operationId,
            @ownerCharacterId = source.@ownerCharacterId,
            @warehouseAdmissions = CopyList(source.@warehouseAdmissions, Copy),
        };
    }

    private static global::HaulDeliveryItemCommitmentSaveData Copy(global::HaulDeliveryItemCommitmentSaveData source)
    {
        if (source == null) return null;
        return new global::HaulDeliveryItemCommitmentSaveData
        {
            @carriedStackId = source.@carriedStackId,
            @expectedStackSignature = source.@expectedStackSignature,
            @itemId = source.@itemId,
            @quantity = source.@quantity,
            @sourceStackId = source.@sourceStackId,
        };
    }

    private static global::ItemInstanceComponentSaveData Copy(global::ItemInstanceComponentSaveData source)
    {
        if (source == null) return null;
        return new global::ItemInstanceComponentSaveData
        {
            @affectsStacking = source.@affectsStacking,
            @componentTypeId = source.@componentTypeId,
            @schemaVersion = source.@schemaVersion,
            @values = CopyList(source.@values, Copy),
        };
    }

    private static global::ItemStateValueSaveData Copy(global::ItemStateValueSaveData source)
    {
        if (source == null) return null;
        return new global::ItemStateValueSaveData
        {
            @booleanValue = source.@booleanValue,
            @decimalValue = source.@decimalValue,
            @integerValue = source.@integerValue,
            @key = source.@key,
            @kind = source.@kind,
            @stringValue = source.@stringValue,
        };
    }

    private static global::NarrativeGenerationTrace Copy(global::NarrativeGenerationTrace source)
    {
        if (source == null) return null;
        return new global::NarrativeGenerationTrace
        {
            @cultureStyleId = source.@cultureStyleId,
            @retryCount = source.@retryCount,
            @schemaHash = source.@schemaHash,
            @schemaId = source.@schemaId,
            @schemaVersion = source.@schemaVersion,
            @usedCharacterFactIds = source.@usedCharacterFactIds == null ? null : (string[])source.@usedCharacterFactIds.Clone(),
            @usedFallback = source.@usedFallback,
            @usedMotifIds = source.@usedMotifIds == null ? null : (string[])source.@usedMotifIds.Clone(),
            @verdict = source.@verdict,
        };
    }

    private static global::NarrativePublicContextMaterialSnapshot Copy(global::NarrativePublicContextMaterialSnapshot source)
    {
        if (source == null) return null;
        return new global::NarrativePublicContextMaterialSnapshot
        {
            @availableFactCount = source.@availableFactCount,
            @backgroundFactIds = source.@backgroundFactIds == null ? null : new List<global::System.String>(source.@backgroundFactIds),
            @cultureStyleId = source.@cultureStyleId,
            @entities = CopyList(source.@entities, Copy),
            @events = CopyList(source.@events, Copy),
            @memoryFactIds = source.@memoryFactIds == null ? null : new List<global::System.String>(source.@memoryFactIds),
            @motifs = CopyList(source.@motifs, Copy),
            @omittedFactCount = source.@omittedFactCount,
            @priorHistoryFactIds = source.@priorHistoryFactIds == null ? null : new List<global::System.String>(source.@priorHistoryFactIds),
            @profileId = source.@profileId,
            @publicFacts = CopyList(source.@publicFacts, Copy),
            @relationshipFactIds = source.@relationshipFactIds == null ? null : new List<global::System.String>(source.@relationshipFactIds),
            @requireCharacterFact = source.@requireCharacterFact,
            @requireMotif = source.@requireMotif,
            @schemaVersion = source.@schemaVersion,
            @selectionPolicyId = source.@selectionPolicyId,
            @semanticHash = source.@semanticHash,
            @subjectId = source.@subjectId,
            @subjectKind = source.@subjectKind,
        };
    }

    private static global::NarrativePublicEntitySnapshot Copy(global::NarrativePublicEntitySnapshot source)
    {
        if (source == null) return null;
        return new global::NarrativePublicEntitySnapshot
        {
            @displayName = source.@displayName,
            @entityId = source.@entityId,
            @kind = source.@kind,
        };
    }

    private static global::NarrativePublicEventSnapshot Copy(global::NarrativePublicEventSnapshot source)
    {
        if (source == null) return null;
        return new global::NarrativePublicEventSnapshot
        {
            @actorId = source.@actorId,
            @count = source.@count,
            @day = source.@day,
            @eventType = source.@eventType,
            @evidenceId = source.@evidenceId,
            @factId = source.@factId,
            @generation = source.@generation,
            @hasDay = source.@hasDay,
            @hasGeneration = source.@hasGeneration,
            @outcome = source.@outcome,
            @targetId = source.@targetId,
        };
    }

    private static global::NarrativePublicFactSnapshot Copy(global::NarrativePublicFactSnapshot source)
    {
        if (source == null) return null;
        return new global::NarrativePublicFactSnapshot
        {
            @category = source.@category,
            @count = source.@count,
            @domain = source.@domain,
            @factId = source.@factId,
            @hasCount = source.@hasCount,
            @hasLastDay = source.@hasLastDay,
            @hasMilestoneCount = source.@hasMilestoneCount,
            @lastDay = source.@lastDay,
            @milestoneCount = source.@milestoneCount,
            @originalFactId = source.@originalFactId,
            @outcome = source.@outcome,
            @priority = source.@priority,
            @sourceSubjectId = source.@sourceSubjectId,
            @subjectId = source.@subjectId,
            @text = source.@text,
            @totalValueDecimal = source.@totalValueDecimal,
        };
    }

    private static global::NarrativePublicMotifSnapshot Copy(global::NarrativePublicMotifSnapshot source)
    {
        if (source == null) return null;
        return new global::NarrativePublicMotifSnapshot
        {
            @label = source.@label,
            @priority = source.@priority,
            @stableId = source.@stableId,
        };
    }

    private static global::RetailPurchaseRecoverySaveData Copy(global::RetailPurchaseRecoverySaveData source)
    {
        if (source == null) return null;
        return new global::RetailPurchaseRecoverySaveData
        {
            @absoluteDay = source.@absoluteDay,
            @buyerId = source.@buyerId,
            @buyerMoneyAfter = source.@buyerMoneyAfter,
            @buyerMoneyBefore = source.@buyerMoneyBefore,
            @facilityId = source.@facilityId,
            @lot = Copy(source.@lot),
            @operationId = source.@operationId,
            @phase = source.@phase,
            @publicationFingerprint = source.@publicationFingerprint,
            @publicationId = source.@publicationId,
            @publicationKey = source.@publicationKey,
            @purchaseCost = source.@purchaseCost,
            @revenuePriceMultiplier = source.@revenuePriceMultiplier,
            @revenueSkillModifierProvenance = source.@revenueSkillModifierProvenance,
            @revenueWorkerId = source.@revenueWorkerId,
            @shopUseCartIndex = source.@shopUseCartIndex,
            @shopUseOperationId = source.@shopUseOperationId,
        };
    }

    private static global::RetailStockComponentSnapshot Copy(global::RetailStockComponentSnapshot source)
    {
        if (source == null) return null;
        return new global::RetailStockComponentSnapshot
        {
            @affectsStacking = source.@affectsStacking,
            @componentTypeId = source.@componentTypeId,
            @schemaVersion = source.@schemaVersion,
            @values = CopyList(source.@values, Copy),
        };
    }

    private static global::RetailStockComponentValueSnapshot Copy(global::RetailStockComponentValueSnapshot source)
    {
        if (source == null) return null;
        return new global::RetailStockComponentValueSnapshot
        {
            @booleanValue = source.@booleanValue,
            @decimalValue = source.@decimalValue,
            @integerValue = source.@integerValue,
            @key = source.@key,
            @kind = source.@kind,
            @stringValue = source.@stringValue,
        };
    }

    private static global::RetailStockLotSnapshot Copy(global::RetailStockLotSnapshot source)
    {
        if (source == null) return null;
        return new global::RetailStockLotSnapshot
        {
            @componentFingerprint = source.@componentFingerprint,
            @components = CopyList(source.@components, Copy),
            @itemDefinitionId = source.@itemDefinitionId,
            @itemInstanceId = source.@itemInstanceId,
            @quantity = source.@quantity,
            @saleItemId = source.@saleItemId,
            @sourceOperationId = source.@sourceOperationId,
            @sourceStackId = source.@sourceStackId,
            @unitMassGrams = source.@unitMassGrams,
        };
    }

    private static global::SocialMemoryFloat Copy(global::SocialMemoryFloat source)
    {
        if (source == null) return null;
        return new global::SocialMemoryFloat(source.@key, source.@value);
    }

    private static global::SocialRumorSnapshot Copy(global::SocialRumorSnapshot source)
    {
        if (source == null) return null;
        return new global::SocialRumorSnapshot
        {
            @baseSentiment = source.@baseSentiment,
            @relationshipSkillModifierProvenance = source.@relationshipSkillModifierProvenance,
            @remainingSeconds = source.@remainingSeconds,
            @sentiment = source.@sentiment,
            @source = source.@source,
            @sourceActorId = source.@sourceActorId,
            @sourceActorName = source.@sourceActorName,
            @spreadChance = source.@spreadChance,
            @summary = source.@summary,
            @targetCharacterId = source.@targetCharacterId,
            @targetCharacterName = source.@targetCharacterName,
            @targetFacilityId = source.@targetFacilityId,
            @targetFacilityTag = source.@targetFacilityTag,
            @targetType = source.@targetType,
            @trustImpact = source.@trustImpact,
            @type = source.@type,
        };
    }

    private static global::WarehouseHaulAdmissionSaveData Copy(global::WarehouseHaulAdmissionSaveData source)
    {
        if (source == null) return null;
        return new global::WarehouseHaulAdmissionSaveData
        {
            @catalogRevision = source.@catalogRevision,
            @itemId = source.@itemId,
            @itemInstanceId = source.@itemInstanceId,
            @lotFingerprint = source.@lotFingerprint,
            @ownerAdmissionOperationId = source.@ownerAdmissionOperationId,
            @quantity = source.@quantity,
            @reservedMassGrams = source.@reservedMassGrams,
            @sourceRevision = source.@sourceRevision,
            @sourceStackId = source.@sourceStackId,
            @sourceWarehouseId = source.@sourceWarehouseId,
            @tokenId = source.@tokenId,
            @warehouseId = source.@warehouseId,
        };
    }

    private static global::WorldCharacterProfile Copy(global::WorldCharacterProfile source)
    {
        if (source == null) return null;
        return new global::WorldCharacterProfile
        {
            @acquiredTraits = Copy(source.@acquiredTraits),
            @characterDataId = source.@characterDataId,
            @currentExperience = source.@currentExperience,
            @displayName = source.@displayName,
            @growth = Copy(source.@growth),
            @isAlive = source.@isAlive,
            @isOwner = source.@isOwner,
            @isStaff = source.@isStaff,
            @isVisiting = source.@isVisiting,
            @level = source.@level,
            @narrative = Copy(source.@narrative),
            @nextVisitorExitSequence = source.@nextVisitorExitSequence,
            @origin = source.@origin,
            @persistentId = source.@persistentId,
            @settlementStanding = source.@settlementStanding,
            @socialMemory = Copy(source.@socialMemory),
            @visitCount = source.@visitCount,
            @visitorExitOperation = Copy(source.@visitorExitOperation),
        };
    }

    private static List<T> CopyList<T>(List<T> source, Func<T, T> copy)
    {
        if (source == null) return null;
        var result = new List<T>(source.Count);
        foreach (T value in source) result.Add(copy(value));
        return result;
    }
}
