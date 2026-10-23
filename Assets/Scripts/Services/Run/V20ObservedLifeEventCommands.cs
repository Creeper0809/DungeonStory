using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;
using static V20ContentEffectEvidence;
using EffectCommitPlan = V20ContentEffectTransaction.EffectCommitPlan;
using static V20ContentResolutionJournal;
using static V20ContentEffectTransaction;

internal sealed class V20ObservedLifeEventCommands : IObservedCareerLifeEventCommand
{
    private readonly V20CampaignRuntime live;
    private readonly V20StoryContentCatalog catalog;
    private readonly ICharacterWorldQuery characters;
    private readonly ICareerService careerQuery;
    private readonly IGameCalendar calendar;
    private readonly IKinshipQuery kinship;
    private readonly ICharacterProficiencyQuery proficiencyQuery;
    private readonly ICharacterNarrativeCommand narrative;
    private readonly ISocialRitualFactPublisher factPublisher;
    private readonly IPhysicalItemBatchDispositionService physicalDispositions;
    private readonly IItemReservationService reservations;
    private readonly V20ContentEffectTransaction effectTransaction;
    private readonly V20ContentEffectRollback effectRollback;
    private readonly V20ContentResolutionJournal journal;

    internal V20ObservedLifeEventCommands(
        V20CampaignRuntime live,
        V20StoryContentCatalog catalog,
        ICharacterWorldQuery characters,
        ICareerService careerQuery,
        IGameCalendar calendar,
        IKinshipQuery kinship,
        ICharacterProficiencyQuery proficiencyQuery,
        ICharacterNarrativeCommand narrative,
        ISocialRitualFactPublisher factPublisher,
        IPhysicalItemBatchDispositionService physicalDispositions,
        IItemReservationService reservations,
        V20ContentEffectTransaction effectTransaction,
        V20ContentEffectRollback effectRollback,
        V20ContentResolutionJournal journal)
    {
        this.live = live;
        this.catalog = catalog;
        this.characters = characters;
        this.careerQuery = careerQuery;
        this.calendar = calendar;
        this.kinship = kinship;
        this.proficiencyQuery = proficiencyQuery;
        this.narrative = narrative;
        this.factPublisher = factPublisher;
        this.physicalDispositions = physicalDispositions;
        this.reservations = reservations;
        this.effectTransaction = effectTransaction;
        this.effectRollback = effectRollback;
        this.journal = journal;
    }

    internal void RequireCanCommitObservedFuneralLifeEvent(
        ObservedFuneralLifeEventReceipt receipt)
    {
        V20CampaignRuntime candidate = live.CreateContentResolutionCandidate(physicalDispositions);
        ObservedLifeEventCommitResult observed =
            candidate.RecordObservedFuneralLifeEvent(receipt);
        if (!observed.StateChanged)
            throw new InvalidOperationException("Observed funeral was already committed.");
        RequireObservedLifeEventEffectsPreflight(
            receipt.SourceOperationId,
            receipt.AbsoluteDay,
            observed.Resolution.HasValue
                ? new[] { observed.Resolution.Value }
                : Array.Empty<V20ResolvedEventResult>());
    }

    internal IReadOnlyList<V20ResolvedEventResult>
        CommitObservedFuneralLifeEvent(
            ObservedFuneralLifeEventReceipt receipt,
            DomainFactLease sourceLease) =>
        CommitObservedLifeEvents(
            receipt.SourceOperationId,
            receipt.AbsoluteDay,
            candidate => new[]
            {
                candidate.RecordObservedFuneralLifeEvent(receipt)
            }, DomainFactEmission.Create(sourceLease, receipt));

    [GameplayInternalOnly(
        "Commits the source-owned apprentice-mistake occurrence.",
        "ProductionRecipeExecutionReceiptAuthority only")]
    bool IObservedCareerLifeEventCommand.TryCaptureProductionDeclaredLoss(
        ProductionDeclaredLossCycleReceipt source,
        out bool stateChanged,
        out string failureReason)
    {
        stateChanged = false;
        failureReason = string.Empty;
        try
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            CharacterId studentId = new(source.WorkerPersistentId);
            if (!studentId.IsValid)
                throw new ArgumentException(
                    "Production-loss actor has no valid character identity.");
            CareerMentorshipSnapshot[] matches = careerQuery.Mentorships
                .Where(value => value.StudentCharacterId.Equals(studentId))
                .ToArray();
            if (matches.Length == 0)
                return true;
            if (matches.Length != 1)
                throw new InvalidOperationException(
                    $"Production-loss student '{studentId.Value}' has duplicate active mentorships.");

            int absoluteDay = Math.Max(1, calendar.Day);
            ObservedProductionLossLifeEventReceipt receipt = new(
                source,
                matches[0],
                absoluteDay,
                ResolveGeneration());
            SocietyEventWorldSaveData societyBefore = live.CaptureSociety();
            if (!factPublisher.TryReserve(
                    receipt,
                    out DomainFactLease prepared,
                    out string reservationFailure))
            {
                failureReason =
                    "observed-production-loss-outcome-reservation-failed:"
                    + reservationFailure;
                return false;
            }

            bool mutationAttempted = false;
            try
            {
                mutationAttempted = true;
                stateChanged = CommitObservedPendingLifeEvent(
                    receipt.SourceOperationId,
                    receipt.AbsoluteDay,
                    DomainFactEmission.Create(prepared, receipt),
                    candidate => candidate.RecordObservedProductionLossLifeEvent(
                        receipt));
                if (!stateChanged)
                {
                    factPublisher.Cancel(prepared);
                    return true;
                }

                mutationAttempted = false;
            }
            catch
            {
                factPublisher.Cancel(prepared);
                if (mutationAttempted)
                    RestoreObservedSociety(societyBefore);
                throw;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or OverflowException)
        {
            failureReason = "observed-production-loss-life-event-failed:"
                + exception.Message;
            return false;
        }
    }

    [GameplayInternalOnly(
        "Commits the source-owned last-lesson occurrence.",
        "CareerApplicationAdapter only")]
    bool IObservedCareerLifeEventCommand.TryCaptureLastLesson(
        CareerMentorshipSnapshot mentorship,
        CharacterCareerSnapshot retirement,
        CombatEquipmentInstance protectiveEquipment,
        CareerMentorshipAwardCommitReceipt award,
        int absoluteDay,
        out bool stateChanged,
        out string failureReason)
    {
        stateChanged = false;
        failureReason = string.Empty;
        try
        {
            if (absoluteDay != Math.Max(1, calendar.Day)
                || !careerQuery.TryGet(
                    mentorship.MentorCharacterId,
                    out CharacterCareerSnapshot currentRetirement)
                || currentRetirement.Retired != retirement.Retired
                || currentRetirement.RetirementScheduleStatus
                    != retirement.RetirementScheduleStatus
                || !string.Equals(
                    currentRetirement.RetirementEventId,
                    retirement.RetirementEventId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    currentRetirement.RetirementChoiceId,
                    retirement.RetirementChoiceId,
                    StringComparison.Ordinal)
                || currentRetirement.RetirementDecisionAbsoluteDay
                    != retirement.RetirementDecisionAbsoluteDay
                || currentRetirement.RetirementDueAbsoluteDay
                    != retirement.RetirementDueAbsoluteDay
                || currentRetirement.RetirementTerminalAbsoluteDay
                    != retirement.RetirementTerminalAbsoluteDay
                || !careerQuery.Mentorships.Any(value =>
                    value.MentorCharacterId.Equals(
                        mentorship.MentorCharacterId)
                    && value.StudentCharacterId.Equals(
                        mentorship.StudentCharacterId)
                    && value.AcademyBuildingId.Equals(
                        mentorship.AcademyBuildingId)
                    && value.ProficiencyId.Equals(mentorship.ProficiencyId)))
            {
                throw new InvalidOperationException(
                    "Last-lesson source no longer matches the active mentorship day.");
            }

            ObservedLastLessonLifeEventReceipt receipt = new(
                mentorship,
                retirement,
                protectiveEquipment,
                award,
                absoluteDay,
                ResolveGeneration());
            SocietyEventWorldSaveData societyBefore = live.CaptureSociety();
            if (!factPublisher.TryReserve(
                    receipt,
                    out DomainFactLease prepared,
                    out string reservationFailure))
            {
                failureReason =
                    "observed-last-lesson-outcome-reservation-failed:"
                    + reservationFailure;
                return false;
            }

            bool mutationAttempted = false;
            try
            {
                mutationAttempted = true;
                stateChanged = CommitObservedPendingLifeEvent(
                    receipt.SourceOperationId,
                    receipt.AbsoluteDay,
                    DomainFactEmission.Create(prepared, receipt),
                    candidate => candidate.RecordObservedLastLessonLifeEvent(
                        receipt));
                if (!stateChanged)
                {
                    factPublisher.Cancel(prepared);
                    return true;
                }

                mutationAttempted = false;
            }
            catch
            {
                factPublisher.Cancel(prepared);
                if (mutationAttempted)
                    RestoreObservedSociety(societyBefore);
                throw;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or OverflowException)
        {
            failureReason = "observed-last-lesson-life-event-failed:"
                + exception.Message;
            return false;
        }
    }

    [GameplayInternalOnly(
        "Commits the source-owned quiet-promotion occurrence.",
        "ObservedProficiencyPromotionApplicationAdapter only")]
    bool IObservedCareerLifeEventCommand.TryCaptureQuietPromotion(
        CharacterProficiencyAwardCommitReceipt source,
        out bool stateChanged,
        out string failureReason)
    {
        stateChanged = false;
        failureReason = string.Empty;
        try
        {
            ObservedLifeEventReceiptSaveData[] existing = live.CaptureSociety()
                .successfulLifeEventOperations
                .Where(value => value != null
                    && value.sourceKind
                        == ObservedLifeEventSourceKind.ProficiencyPromotion
                    && string.Equals(
                        value.sourceOperationId,
                        source.SourceOperationId,
                        StringComparison.Ordinal))
                .ToArray();
            if (existing.Length > 1)
                throw new InvalidOperationException(
                    $"Quiet-promotion source '{source.SourceOperationId}' has duplicate receipts.");
            if (existing.Length == 1)
            {
                if (!ObservedQuietPromotionLifeEventReceipt.TryParse(
                        existing[0].canonicalPayload,
                        out ObservedQuietPromotionLifeEventReceipt committed)
                    || !string.Equals(
                        committed.SourceOperationId,
                        source.SourceOperationId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Quiet-promotion source '{source.SourceOperationId}' conflicts with its committed receipt.");
                }
                return true;
            }

            if (source.AbsoluteHour != Math.Max(0L, calendar.AbsoluteHour)
                || !proficiencyQuery.TryGetProficiency(
                    source.CharacterId,
                    source.ProficiencyId,
                    source.AbsoluteHour,
                    out CharacterProficiencySnapshot current)
                || current.CurrentMilliExperience
                    != source.AfterCurrentMilliExperience
                || current.LifetimeMilliExperience
                    != source.AfterLifetimeMilliExperience)
            {
                throw new InvalidOperationException(
                    "Quiet-promotion source no longer matches the committed proficiency state.");
            }

            int absoluteDay = Math.Max(1, calendar.Day);
            ObservedQuietPromotionLifeEventReceipt receipt = new(
                source,
                absoluteDay,
                ResolveGeneration());
            LifeEventDefinitionSO definition = catalog.LifeEvents.Single(
                value => string.Equals(
                    value.StableId,
                    "life-event:quiet-promotion",
                    StringComparison.Ordinal));
            if (!definition.automatic
                || definition.automaticEffects == null
                || definition.automaticEffects.Count == 0
                || definition.automaticEffects.Any(value => value == null
                    || !value.IsValid
                    || value.kind != V20ContentEffectKind.Mood))
            {
                throw new InvalidOperationException(
                    "Quiet-promotion transactional effects must remain mood-only.");
            }

            SocietyEventWorldSaveData societyBefore = live.CaptureSociety();
            CharacterActor owner = characters.Characters.SingleOrDefault(
                value => value != null
                    && value.Identity != null
                    && string.Equals(
                        value.Identity.PersistentId,
                        receipt.CharacterId.Value,
                        StringComparison.Ordinal));
            if (owner?.Stats == null)
            {
                failureReason =
                    "observed-quiet-promotion-owner-unavailable";
                return false;
            }
            CharacterMoodDeliveryTransactionSnapshot moodBefore =
                owner.Stats.CaptureMoodDeliveryTransactionState();
            ICharacterNarrativePersistence narrativePersistence =
                narrative as ICharacterNarrativePersistence;
            if (narrativePersistence == null)
            {
                failureReason =
                    "observed-quiet-promotion-narrative-owner-unavailable";
                return false;
            }
            CharacterNarrativeWorldSaveData narrativeBefore =
                narrativePersistence.Capture();
            if (!factPublisher.TryReserve(
                    receipt,
                    out DomainFactLease prepared,
                    out string reservationFailure))
            {
                failureReason =
                    "observed-quiet-promotion-outcome-reservation-failed:"
                    + reservationFailure;
                return false;
            }

            bool mutationAttempted = false;
            try
            {
                mutationAttempted = true;
                stateChanged = CommitObservedPendingLifeEvent(
                    receipt.SourceOperationId,
                    receipt.AbsoluteDay,
                    DomainFactEmission.Create(prepared, receipt),
                    candidate => candidate
                        .RecordObservedQuietPromotionLifeEvent(receipt));
                if (!stateChanged)
                {
                    factPublisher.Cancel(prepared);
                    return true;
                }

                mutationAttempted = false;
            }
            catch
            {
                factPublisher.Cancel(prepared);
                if (mutationAttempted)
                {
                    RestoreQuietPromotionOwner(
                        societyBefore,
                        owner,
                        moodBefore,
                        narrativePersistence,
                        narrativeBefore);
                }
                throw;
            }
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException or OverflowException)
        {
            failureReason = "observed-quiet-promotion-life-event-failed:"
                + exception.Message;
            return false;
        }
    }

    [GameplayInternalOnly(
        "Publishes one prepared observed life-event candidate.",
        "IObservedCareerLifeEventCommand implementations only")]
    private bool CommitObservedPendingLifeEvent(
        string operationId,
        int absoluteDay,
        DomainFactEmission sourceFact,
        Func<V20CampaignRuntime, ObservedLifeEventCommitResult> observe)
    {
        bool stateChanged = false;
        CommitObservedLifeEvents(
            operationId,
            absoluteDay,
            candidate =>
            {
                ObservedLifeEventCommitResult result = observe(candidate);
                stateChanged = result.StateChanged;
                return new[] { result };
            }, sourceFact);
        return stateChanged;
    }

    private void RestoreObservedSociety(SocietyEventWorldSaveData snapshot) =>
        live.PublishSociety(live.PrepareSociety(
            snapshot ?? throw new ArgumentNullException(nameof(snapshot))));

    private void RestoreQuietPromotionOwner(
        SocietyEventWorldSaveData societyBefore,
        CharacterActor owner,
        CharacterMoodDeliveryTransactionSnapshot moodBefore,
        ICharacterNarrativePersistence narrativePersistence,
        CharacterNarrativeWorldSaveData narrativeBefore)
    {
        RestoreObservedSociety(societyBefore);
        owner.Stats.RestoreMoodDeliveryTransactionState(
            moodBefore ?? throw new ArgumentNullException(nameof(moodBefore)));
        narrativePersistence.PublishRestore(
            narrativePersistence.PrepareRestore(
                narrativeBefore
                ?? throw new ArgumentNullException(nameof(narrativeBefore))));
    }

    private int ResolveGeneration()
    {
        // Life-event receipts need only the living population's generation.
        // A full milestone projection also constructs offense, wildlife and
        // production services, creating a dependency back to the producer.
        return Mathf.Max(0, characters.Characters
            .Where(value => value != null
                && value.CurrentHealth > 0f
                && value.Identity != null)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .Select(value => kinship.GetGeneration(
                CharacterPersistentIdentity.Require(value)))
            .DefaultIfEmpty(0)
            .Max());
    }

    private IReadOnlyList<V20ResolvedEventResult> CommitObservedLifeEvents(
        string operationId,
        int absoluteDay,
        Func<V20CampaignRuntime, IReadOnlyList<ObservedLifeEventCommitResult>>
            observe,
        DomainFactEmission sourceFact = null)
    {
        V20CampaignRuntime candidate = live.CreateContentResolutionCandidate(physicalDispositions);
        IReadOnlyList<ObservedLifeEventCommitResult> observed =
            observe?.Invoke(candidate)
            ?? throw new ArgumentNullException(nameof(observe));
        V20ResolvedEventResult[] resolutions = observed
            .Where(value => value.Resolution.HasValue)
            .Select(value => value.Resolution.Value)
            .ToArray();
        if (!observed.Any(value => value.StateChanged))
            return resolutions;

        ContentResolutionRequest request = new()
        {
            ActionId = operationId,
            AbsoluteDay = Math.Max(1, absoluteDay)
        };
        if (!effectTransaction.TryPreflightEffects(
                request,
                resolutions,
                out EffectCommitPlan plan,
                out DomainFailure preflightFailure))
            throw new InvalidOperationException(
                $"Observed life-event effect preflight failed: {preflightFailure.Code}.");

        SeasonalEventAggregateState seasonal =
            live.PrepareSeasonal(candidate.CaptureSeasonal());
        SocietyEventAggregateState society =
            live.PrepareSociety(candidate.CaptureSociety());
        FactionCampaignAggregateState factions =
            live.PrepareFactions(candidate.CaptureFactions(), seasonal);
        RunMilestoneAggregateState milestones =
            live.PrepareMilestones(candidate.CaptureMilestones());
        V20ContentEffectOwnerSnapshot ownersBefore = effectRollback.CaptureEffectOwners(resolutions);
        string stateBeforeFingerprint =
            CaptureEffectOwnerFingerprint(ownersBefore);
        string stateAfterFingerprint = string.Empty;
        // Observed parent and child facts are one completion boundary. Pure typed
        // effects have captured rollback-capable owners, so a definite rejection
        // must not leave them in forward recovery without their parent receipt.
        // Monetary/physical commits still retain the existing forward-only boundary.
        bool requiresForwardRecovery = RequiresForwardRecovery(
            plan,
            resolutions, effectTransaction.Handlers) && (sourceFact == null || plan.MoneyDelta != 0
                || plan.ItemCosts.Count > 0 || plan.Grants.Count > 0);
        V20ContentResolutionRecoveryPhase effectPhase =
            V20ContentResolutionRecoveryPhase.Prepared;
        V20ContentEffectCommitReferences childCommits = new();
        bool recoveryPublished = false;
        try
        {
            if (!effectTransaction.TryCommitEffects(
                    request,
                    resolutions,
                    plan,
                    out DomainFailure effectFailure,
                    out effectPhase,
                    out childCommits))
            {
                throw new InvalidOperationException(
                    $"Observed life-event effect commit failed: {effectFailure.Code}.");
            }
            stateAfterFingerprint = CaptureEffectOwnerFingerprint(
                effectRollback.CaptureEffectOwners(resolutions));
            if (requiresForwardRecovery)
            {
                UpsertRecovery(
                    milestones,
                    CreateRecovery(
                        request,
                        resolutions,
                        plan.Dropoff,
                        V20ContentResolutionRecoveryPhase.TypedEffectsApplied,
                        childCommits,
                        stateBeforeFingerprint,
                        stateAfterFingerprint));
            }
            live.PublishContentResolution(
                seasonal,
                society,
                factions,
                milestones);
            recoveryPublished = requiresForwardRecovery;
            journal.PublishResolvedFacts(
                operationId,
                absoluteDay,
                resolutions,
                childCommits,
                stateBeforeFingerprint,
                stateAfterFingerprint,
                sourceFact);
            if (requiresForwardRecovery)
            {
                journal.AdvanceContentResolutionRecovery(
                    operationId,
                    V20ContentResolutionRecoveryPhase.OutcomesCommitted);
                journal.RemoveContentResolutionRecovery(operationId);
            }
        }
        catch
        {
            if (requiresForwardRecovery
                && effectPhase
                    >= V20ContentResolutionRecoveryPhase.ItemCostsCommitted)
            {
                if (effectPhase
                    == V20ContentResolutionRecoveryPhase.ItemGrantsCommitted)
                {
                    effectRollback.RestoreTypedEffectOwners(ownersBefore);
                }
                if (!recoveryPublished)
                {
                    UpsertRecovery(
                        milestones,
                        CreateRecovery(
                            request,
                            resolutions,
                            plan.Dropoff,
                            effectPhase,
                            childCommits,
                            stateBeforeFingerprint,
                            string.Empty));
                    live.PublishContentResolution(
                        seasonal,
                        society,
                        factions,
                        milestones);
                }
            }
            else
            {
                effectRollback.RestoreEffectOwners(ownersBefore, operationId);
            }
            plan.Release(reservations);
            throw;
        }
        return resolutions;
    }

    private void RequireObservedLifeEventEffectsPreflight(
        string operationId,
        int absoluteDay,
        IReadOnlyList<V20ResolvedEventResult> resolutions)
    {
        ContentResolutionRequest request = new()
        {
            ActionId = operationId,
            AbsoluteDay = Math.Max(1, absoluteDay)
        };
        if (!effectTransaction.TryPreflightEffects(
                request,
                resolutions,
                out EffectCommitPlan plan,
                out DomainFailure failure))
            throw new InvalidOperationException(
                $"Observed life-event effect preflight failed: {failure.Code}.");
        plan.Release(reservations);
    }
}
