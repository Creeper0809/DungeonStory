using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using VContainer.Unity;

public sealed class CareerApplicationAdapter :
    ITickable
{
    private readonly ICareerService careers;
    private readonly ICharacterWorldQuery world;
    private readonly IGameCalendar calendar;
    private readonly IGameClock clock;
    private readonly IBuildingWorldQuery buildings;
    private readonly CareerDurableEquipmentAwardRuntime careerEquipment;
    private readonly ICharacterProficiencyQuery proficiencyQuery;
    private readonly ICharacterProficiencyCommand proficiencyCommands;
    private readonly ICombatEquipmentRuntime combatEquipment;
    private readonly IObservedCareerLifeEventCommand observedLifeEvents;
    private readonly CharacterMoodPolicyService moods;
    private readonly ICharacterSettlementStandingQuery settlementStandings;
    private readonly Dictionary<CharacterId, float> nextAssignmentAttemptAt = new();

    public CareerApplicationAdapter(
        ICareerService careers,
        ICharacterWorldQuery world,
        IGameCalendar calendar,
        IGameClock clock,
        IBuildingWorldQuery buildings,
        ICharacterProficiencyQuery proficiencyQuery,
        ICharacterProficiencyCommand proficiencyCommands,
        IDurableFacilityEquipmentPolicyQuery equipmentPolicies,
        IDurableFacilityEquipmentSlotCommand equipmentSlots,
        IDurableFacilityEquipmentUseCommand equipmentUse,
        ICombatEquipmentRuntime combatEquipment,
        IObservedCareerLifeEventCommand observedLifeEvents,
        ICareerPersistence careerPersistence,
        ISocialRitualFactPublisher awardFacts,
        CharacterMoodPolicyService moods = null,
        ICharacterSettlementStandingQuery settlementStandings = null)
    {
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.proficiencyQuery = proficiencyQuery
            ?? throw new ArgumentNullException(nameof(proficiencyQuery));
        this.proficiencyCommands = proficiencyCommands
            ?? throw new ArgumentNullException(nameof(proficiencyCommands));
        careerEquipment = new CareerDurableEquipmentAwardRuntime(
            equipmentPolicies,
            equipmentSlots,
            equipmentUse,
            careerPersistence,
            awardFacts);
        this.combatEquipment = combatEquipment
            ?? throw new ArgumentNullException(nameof(combatEquipment));
        this.observedLifeEvents = observedLifeEvents
            ?? throw new ArgumentNullException(nameof(observedLifeEvents));
        this.moods = moods;
        this.settlementStandings = settlementStandings;
    }

    public void Tick()
    {
        float elapsed = Math.Max(0f, clock.DeltaTime);
        if (elapsed <= 0f)
            return;
        foreach (CharacterActor actor in world.Characters.Where(value =>
                     value != null && !value.IsDead))
        {
            if (!actor.TryGetAbility(out AbilityWork work)
                || !work.isWorking
                || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
                || !careers.TryGet(id, out CharacterCareerSnapshot career)
                || !career.Retired)
            {
                continue;
            }
            careers.RecordRetiredWork(id, calendar.Day, elapsed);
        }
        ProcessMentorships(elapsed);
    }

    private void ProcessMentorships(float elapsed)
    {
        int lessonDay = Math.Max(1, calendar.Day);
        foreach (CareerMentorshipSnapshot assignment in careers.Mentorships)
        {
            CharacterActor mentor = FindLivingActor(assignment.MentorCharacterId);
            CharacterActor student = FindLivingActor(assignment.StudentCharacterId);
            BuildableObject academy = buildings.Buildings.FirstOrDefault(building =>
                building != null && !building.isDestroy
                && building.PersistentInstanceId.Equals(assignment.AcademyBuildingId)
                && building.BuildingData?.ResearchFacilityCommand ==
                    ResearchFacilityCommandKind.MentorAcademy);
            if (mentor == null || student == null || academy == null
                || settlementStandings != null
                    && (!settlementStandings.CanParticipateInMentoring(
                            mentor,
                            out _)
                        || !settlementStandings.CanParticipateInMentoring(
                            student,
                            out _))
                || !assignment.ProficiencyId.IsValid
                || !proficiencyQuery.TryGetProficiency(
                    assignment.MentorCharacterId,
                    assignment.ProficiencyId,
                    calendar.AbsoluteHour,
                    out CharacterProficiencySnapshot mentorSkill)
                || !proficiencyQuery.TryGetProficiency(
                    assignment.StudentCharacterId,
                    assignment.ProficiencyId,
                    calendar.AbsoluteHour,
                    out CharacterProficiencySnapshot studentSkill)
                || mentorSkill.Rank < CharacterProficiencyRank.Expert
                || (int)mentorSkill.Rank <= (int)studentSkill.Rank
                || mentorSkill.CurrentExperience - studentSkill.CurrentExperience < 200)
            {
                continue;
            }

            float relation = ResolveRelationshipFactor(mentor, student);
            if (relation <= 0f)
            {
                continue;
            }

            CareerMentorshipSnapshot progress = assignment;
            if (IsPerformingLessonWork(mentor, academy))
            {
                progress = careers.RecordMentorshipWork(
                    assignment.StudentCharacterId,
                    lessonDay,
                    mentorContribution: true,
                    approvedWork: elapsed * ResolveLessonWorkRate(
                        mentor,
                        academy));
            }
            else if (progress.MentorApprovedWork
                     < CareerRules.MentoringWorkAmountPerParticipant)
            {
                TryScheduleLessonWork(mentor, academy);
            }

            if (IsPerformingLessonWork(student, academy))
            {
                progress = careers.RecordMentorshipWork(
                    assignment.StudentCharacterId,
                    lessonDay,
                    mentorContribution: false,
                    approvedWork: elapsed * ResolveLessonWorkRate(
                        student,
                        academy));
            }
            else if (progress.StudentApprovedWork
                     < CareerRules.MentoringWorkAmountPerParticipant)
            {
                TryScheduleLessonWork(student, academy);
            }

            bool hasLastLessonSource = TryCaptureLastLessonSource(
                assignment,
                out CharacterCareerSnapshot retirement,
                out CombatEquipmentInstance protectiveEquipment);
            if (!progress.HasCompletedPhysicalLesson
                || progress.LastAwardAbsoluteDay >= lessonDay
                || !careerEquipment.TryCommitAward(
                    academy.RequirePersistentInstanceId(),
                    academy.centerPos,
                    assignment,
                    lessonDay,
                    () => careers.TryMarkMentoringAwarded(
                        assignment.StudentCharacterId,
                        lessonDay),
                    out CareerMentorshipAwardCommitReceipt award))
            {
                continue;
            }
            if (hasLastLessonSource)
            {
                try
                {
                    if (!observedLifeEvents.TryCaptureLastLesson(
                            assignment,
                            retirement,
                            protectiveEquipment,
                            award,
                            lessonDay,
                            out _,
                            out string observedFailure))
                    {
                        UnityEngine.Debug.LogError(
                            "Committed mentorship award observer failed: "
                            + observedFailure);
                    }
                }
                catch (Exception exception) when (
                    exception is not OutOfMemoryException
                    && exception is not StackOverflowException
                    && exception is not AccessViolationException)
                {
                    UnityEngine.Debug.LogError(
                        "Committed mentorship award observer threw: "
                        + exception.GetType().Name);
                }
            }

            float studentBonus = Math.Min(
                CareerRules.MaximumDailyMentoringXp,
                2f + studentSkill.PracticeExperienceToday * 0.35f)
                * relation;
            studentBonus *= mentor.GetDetailedStatMultiplier(
                "character:mentee-xp",
                new[] { "work:mentoring" });
            proficiencyCommands.AddDirectExperience(
                assignment.StudentCharacterId,
                assignment.ProficiencyId,
                studentBonus,
                calendar.AbsoluteHour);
            if (moods != null
                && proficiencyQuery.TryGetProficiency(
                    assignment.StudentCharacterId,
                    assignment.ProficiencyId,
                    calendar.AbsoluteHour,
                    out CharacterProficiencySnapshot promotedSkill)
                && promotedSkill.Rank > studentSkill.Rank)
            {
                moods.Apply(
                    mentor,
                    "mentee:rank-up",
                    0f,
                    2,
                    "제자의 숙련 단계 상승");
            }
            proficiencyCommands.AddDirectExperience(
                assignment.MentorCharacterId,
                assignment.ProficiencyId,
                CareerRules.MentoringWorkAmountPerParticipant
                    * ProficiencyProgressionRules.ExperiencePerApprovedWork
                    * 0.25f,
                calendar.AbsoluteHour);
            proficiencyCommands.RecordPractice(
                assignment.MentorCharacterId,
                assignment.ProficiencyId,
                calendar.AbsoluteHour);
            mentor.TryGetAbility(out AbilityWork mentorWork);
            student.TryGetAbility(out AbilityWork studentWork);
            mentorWork?.ClearPriorityWorkTarget();
            studentWork?.ClearPriorityWorkTarget();
            mentor.PublishDomainEvent(new CareerMentorshipCompletedEvent(mentor,
                student.Identity?.DisplayName ?? student.name, assignment.ProficiencyId.Value, true));
            student.PublishDomainEvent(new CareerMentorshipCompletedEvent(student,
                mentor.Identity?.DisplayName ?? mentor.name, assignment.ProficiencyId.Value, false));
        }
    }

    private static bool IsPerformingLessonWork(
        CharacterActor actor,
        BuildableObject academy)
    {
        return actor != null
            && actor.TryGetAbility(out AbilityWork work)
            && work.isWorking
            && work.assignedShop == academy
            && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Operate;
    }

    private static float ResolveLessonWorkRate(
        CharacterActor actor,
        BuildableObject academy) =>
        Math.Max(
            0.1f,
            actor?.GetWorkSpeedMultiplier(
                BuiltInWorkTypeIds.Operate,
                academy) ?? 1f);

    private void TryScheduleLessonWork(
        CharacterActor actor,
        BuildableObject academy)
    {
        if (actor == null
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            || (nextAssignmentAttemptAt.TryGetValue(id, out float next)
                && clock.Time < next)
            || !actor.TryGetAbility(out AbilityWork work)
            || work.isWorking
            || work.PriorityWorkTarget != null)
        {
            return;
        }
        nextAssignmentAttemptAt[id] = clock.Time + 1f;
        work.TrySetPriorityWorkTarget(academy, out _);
    }

    private static float ResolveRelationshipFactor(
        CharacterActor mentor,
        CharacterActor student)
    {
        float sentiment = Math.Min(
            mentor?.SocialMemory?.GetRelationshipSentiment(student) ?? 0f,
            student?.SocialMemory?.GetRelationshipSentiment(mentor) ?? 0f);
        if (sentiment < -0.20f) return 0f;
        if (sentiment < 0f) return 0.8f;
        if (sentiment < 0.5f) return 1f;
        return 1.1f;
    }

    private bool TryCaptureLastLessonSource(
        CareerMentorshipSnapshot mentorship,
        out CharacterCareerSnapshot retirement,
        out CombatEquipmentInstance protectiveEquipment)
    {
        protectiveEquipment = null;
        if (!careers.TryGet(mentorship.MentorCharacterId, out retirement)
            || retirement.Retired
            || retirement.RetirementScheduleStatus
                != RetirementScheduleStatus.Pending)
        {
            return false;
        }

        protectiveEquipment = combatEquipment.Instances
            .Where(value => value != null
                && value.worldState == CombatEquipmentWorldState.Equipped
                && string.Equals(
                    value.ownerCharacterId,
                    mentorship.MentorCharacterId.Value,
                    StringComparison.Ordinal)
                && (CombatEquipmentRoleRules.For(value.definitionId)
                    & CombatEquipmentRoleFlags.BlastAndSmokeProtection) != 0)
            .OrderBy(value => value.instanceId, StringComparer.Ordinal)
            .FirstOrDefault();
        return protectiveEquipment != null;
    }

    private CharacterActor FindLivingActor(CharacterId characterId) =>
        world.Characters.FirstOrDefault(actor => actor != null
            && !actor.IsDead
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            && id.Equals(characterId));
}

public readonly struct CareerMentorshipAwardCommitReceipt
{
    private CareerMentorshipAwardCommitReceipt(
        BuildingInstanceId academyBuildingId,
        string ledgerStackId,
        long beforeContentRevision,
        long afterContentRevision,
        string sourceOperationId)
    {
        AcademyBuildingId = academyBuildingId;
        LedgerStackId = ledgerStackId?.Trim() ?? string.Empty;
        BeforeContentRevision = beforeContentRevision;
        AfterContentRevision = afterContentRevision;
        SourceOperationId = sourceOperationId?.Trim() ?? string.Empty;
        if (!AcademyBuildingId.IsValid
            || string.IsNullOrWhiteSpace(LedgerStackId)
            || BeforeContentRevision < 0L
            || AfterContentRevision <= BeforeContentRevision
            || !string.Equals(
                SourceOperationId,
                FormatOperationId(LedgerStackId, AfterContentRevision),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Career mentorship award receipt is invalid.");
        }
    }

    internal BuildingInstanceId AcademyBuildingId { get; }
    internal string LedgerStackId { get; }
    internal long BeforeContentRevision { get; }
    internal long AfterContentRevision { get; }
    internal string SourceOperationId { get; }
    internal bool IsValid => AcademyBuildingId.IsValid
        && !string.IsNullOrWhiteSpace(LedgerStackId)
        && BeforeContentRevision >= 0L
        && AfterContentRevision > BeforeContentRevision
        && string.Equals(
            SourceOperationId,
            FormatOperationId(LedgerStackId, AfterContentRevision),
            StringComparison.Ordinal);

    internal static CareerMentorshipAwardCommitReceipt FromCommittedUse(
        BuildingInstanceId academyBuildingId,
        DurableFacilityEquipmentUseContext context) => new(
        academyBuildingId,
        context?.After?.StackId,
        context?.Before?.ContentRevision ?? -1L,
        context?.After?.ContentRevision ?? -1L,
        context == null
            ? string.Empty
            : FormatOperationId(
                context.After.StackId,
                context.After.ContentRevision));

    internal static CareerMentorshipAwardCommitReceipt FromPreflight(
        BuildingInstanceId academyBuildingId,
        DurableFacilityEquipmentUseSubject subject)
    {
        if (subject == null)
            throw new ArgumentNullException(nameof(subject));
        long afterContentRevision = checked(subject.ContentRevision + 1L);
        return new CareerMentorshipAwardCommitReceipt(
            academyBuildingId,
            subject.StackId,
            subject.ContentRevision,
            afterContentRevision,
            FormatOperationId(subject.StackId, afterContentRevision));
    }

    internal static CareerMentorshipAwardCommitReceipt Restore(
        BuildingInstanceId academyBuildingId,
        string ledgerStackId,
        long beforeContentRevision,
        long afterContentRevision,
        string sourceOperationId) => new(
        academyBuildingId,
        ledgerStackId,
        beforeContentRevision,
        afterContentRevision,
        sourceOperationId);

    private static string FormatOperationId(
        string ledgerStackId,
        long afterContentRevision) =>
        $"career-mentorship-award:{ledgerStackId}:"
        + afterContentRevision.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Career-owned adapter over the common durable facility-equipment authority.
/// The common slot owns exact delivery, positive gram capacity, persistence and
/// terminal custody. The career aggregate mutation is committed as the effect
/// of the same wear transaction so a rejected award restores ledger wear.
/// </summary>
public sealed class CareerDurableEquipmentAwardRuntime
{
    public const string AwardEffectKind = "career-mentorship-award";
    public const double LedgerWearPerAward = 0.5d;

    private readonly IDurableFacilityEquipmentPolicyQuery policies;
    private readonly IDurableFacilityEquipmentSlotCommand slots;
    private readonly IDurableFacilityEquipmentUseCommand use;
    private readonly ICareerPersistence careerPersistence;
    private readonly ISocialRitualFactPublisher awardFacts;
    private bool awarding;

    public CareerDurableEquipmentAwardRuntime(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentSlotCommand slots,
        IDurableFacilityEquipmentUseCommand use)
        : this(policies, slots, use, null, null)
    {
    }

    internal CareerDurableEquipmentAwardRuntime(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentSlotCommand slots,
        IDurableFacilityEquipmentUseCommand use,
        ICareerPersistence careerPersistence,
        ISocialRitualFactPublisher awardFacts)
    {
        this.policies = policies
            ?? throw new ArgumentNullException(nameof(policies));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.use = use ?? throw new ArgumentNullException(nameof(use));
        this.careerPersistence = careerPersistence;
        this.awardFacts = awardFacts;
    }

    [GameplayInternalOnly(
        "Commits one mentorship award through the registered career-ledger slot.",
        "CareerApplicationAdapter only")]
    public bool TryCommitAward(
        BuildingInstanceId academyId,
        UnityEngine.Vector2Int academyPosition,
        Func<bool> commitAward)
    {
        AssertIdle();
        if (!academyId.IsValid || commitAward == null)
            throw new ArgumentException("Career equipment award input is invalid.");
        if (!TryPrepareAssignment(
                academyId,
                academyPosition,
                out DurableFacilityEquipmentAssignment assignment))
        {
            return false;
        }
        return use.TryApplyWearAndEffect(
            assignment.Key,
            CareerDurableEquipmentPolicySource.RequirementId,
            LedgerWearPerAward,
            new CareerMentorshipAwardEffect(commitAward)).Succeeded;
    }

    [GameplayInternalOnly(
        "Commits one mentorship award and exposes its exact durable-equipment receipt.",
        "CareerApplicationAdapter only")]
    internal bool TryCommitAward(
        BuildingInstanceId academyId,
        UnityEngine.Vector2Int academyPosition,
        CareerMentorshipSnapshot mentorship,
        int absoluteDay,
        Func<bool> commitAward,
        out CareerMentorshipAwardCommitReceipt receipt)
    {
        AssertIdle();
        receipt = default;
        if (!academyId.IsValid
            || !mentorship.MentorCharacterId.IsValid
            || !mentorship.StudentCharacterId.IsValid
            || !mentorship.ProficiencyId.IsValid
            || !mentorship.AcademyBuildingId.Equals(academyId)
            || absoluteDay < 1
            || commitAward == null)
            throw new ArgumentException("Career equipment award input is invalid.");
        if (careerPersistence == null || awardFacts == null)
            throw new InvalidOperationException(
                "Career mentorship award fact publication is unavailable.");
        if (!TryPrepareAssignment(
                academyId,
                academyPosition,
                out DurableFacilityEquipmentAssignment assignment))
        {
            return false;
        }

        CareerMentorshipAwardFactEffect effect = new(
            commitAward,
            academyId,
            mentorship,
            absoluteDay,
            careerPersistence,
            awardFacts);
        DurableFacilityEquipmentUseResult result;
        awarding = true;
        try
        {
            result = use.TryApplyWearAndEffect(
                assignment.Key,
                CareerDurableEquipmentPolicySource.RequirementId,
                LedgerWearPerAward,
                effect);
        }
        catch
        {
            effect.CancelUncommitted();
            throw;
        }
        finally { awarding = false; }
        if (result.Succeeded)
        {
            receipt = effect.CommittedReceipt;
            if (!receipt.IsValid)
                throw new InvalidOperationException(
                    "Career mentorship award committed without an exact durable-equipment receipt.");
        }
        else
        {
            effect.CancelUncommitted();
        }
        return result.Succeeded;
    }

    private void AssertIdle()
    {
        if (awarding) throw new InvalidOperationException("career-mentorship-award-active");
    }

    private bool TryPrepareAssignment(
        BuildingInstanceId academyId,
        UnityEngine.Vector2Int academyPosition,
        out DurableFacilityEquipmentAssignment assignment)
    {
        if (!policies.TryGetPolicy(
                CareerDurableEquipmentPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "The career-ledger durable-equipment policy is not registered.");
        }

        assignment = policy.CreateAssignment(
            academyId.Value,
            academyId,
            academyPosition);
        DurableFacilityEquipmentSlotResult reconciled = slots.TryReconcile(
            assignment);
        if (reconciled.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            throw new InvalidOperationException(
                "Career-ledger slot reconciliation conflicted: "
                + reconciled.FailureReason);
        }
        if (!reconciled.Succeeded)
            return false;

        DurableFacilityEquipmentSlotResult supplied = slots.TryEnsureSupply(
            assignment.Key);
        if (supplied.Status == DurableFacilityEquipmentSlotStatus.Conflict)
        {
            throw new InvalidOperationException(
                "Career-ledger supply reconciliation conflicted: "
                + supplied.FailureReason);
        }
        return true;
    }

    private sealed class CareerMentorshipAwardEffect :
        IDurableFacilityEquipmentEffectCommit
    {
        private readonly Func<bool> commit;

        internal CareerMentorshipAwardEffect(Func<bool> commit)
        {
            this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
        }

        public string EffectKind => AwardEffectKind;

        public bool TryPreflight(
            DurableFacilityEquipmentSlotSnapshot slot,
            DurableFacilityEquipmentRequirement requirement,
            DurableFacilityEquipmentUseSubject subject,
            double wearAmount,
            out string failureReason)
        {
            bool valid = ValidPreflight(
                slot,
                requirement,
                subject,
                wearAmount);
            failureReason = valid
                ? string.Empty
                : "career-mentorship-award-preflight-mismatch";
            return valid;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            if (!commit())
            {
                failureReason = "career-mentorship-award-rejected";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class CareerMentorshipAwardFactEffect :
        IDurableFacilityEquipmentEffectCommit
    {
        private readonly Func<bool> commit;
        private readonly BuildingInstanceId academyId;
        private readonly CareerMentorshipSnapshot mentorship;
        private readonly int absoluteDay;
        private readonly ICareerPersistence careerPersistence;
        private readonly ISocialRitualFactPublisher awardFacts;
        private CharacterCareerWorldSaveData careerBefore;
        private DomainFactLease prepared;
        private CareerMentorshipAwardFact fact;
        private CareerMentorshipAwardCommitReceipt projectedReceipt;
        private bool reserved;
        private bool mutationAttempted;
        private bool durablyCommitted;

        internal CareerMentorshipAwardFactEffect(
            Func<bool> commit,
            BuildingInstanceId academyId,
            CareerMentorshipSnapshot mentorship,
            int absoluteDay,
            ICareerPersistence careerPersistence,
            ISocialRitualFactPublisher awardFacts)
        {
            this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
            this.academyId = academyId;
            this.mentorship = mentorship;
            this.absoluteDay = absoluteDay;
            this.careerPersistence = careerPersistence
                ?? throw new ArgumentNullException(nameof(careerPersistence));
            this.awardFacts = awardFacts
                ?? throw new ArgumentNullException(nameof(awardFacts));
        }

        internal CareerMentorshipAwardCommitReceipt CommittedReceipt
            { get; private set; }

        public string EffectKind => AwardEffectKind;

        public bool TryPreflight(
            DurableFacilityEquipmentSlotSnapshot slot,
            DurableFacilityEquipmentRequirement requirement,
            DurableFacilityEquipmentUseSubject subject,
            double wearAmount,
            out string failureReason)
        {
            if (reserved
                || !ValidPreflight(slot, requirement, subject, wearAmount))
            {
                failureReason =
                    "career-mentorship-award-outcome-preflight-mismatch";
                return false;
            }

            projectedReceipt = CareerMentorshipAwardCommitReceipt
                .FromPreflight(academyId, subject);
            careerBefore = careerPersistence.Capture();
            fact = new CareerMentorshipAwardFact(mentorship, projectedReceipt, absoluteDay);
            if (!awardFacts.TryReserve(
                    fact,
                    out prepared,
                    out failureReason))
            {
                failureReason =
                    "career-mentorship-award-outcome-reservation-failed:"
                    + failureReason;
                return false;
            }

            reserved = true;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCommit(
            DurableFacilityEquipmentUseContext context,
            out string failureReason)
        {
            if (!reserved || context == null)
            {
                failureReason =
                    "career-mentorship-award-outcome-reservation-missing";
                return false;
            }

            CareerMentorshipAwardCommitReceipt actual =
                CareerMentorshipAwardCommitReceipt.FromCommittedUse(
                    academyId,
                    context);
            if (!SameReceipt(projectedReceipt, actual))
            {
                CancelUncommitted();
                failureReason =
                    "career-mentorship-award-outcome-revision-drift";
                return false;
            }

            try
            {
                mutationAttempted = true;
                if (!commit())
                {
                    CancelUncommitted();
                    failureReason = "career-mentorship-award-rejected";
                    return false;
                }

                DomainFactDelivery committed = awardFacts.Publish(prepared, fact);
                if (!committed.MayBeCommitted)
                {
                    reserved = false;
                    RestoreCareer();
                    failureReason =
                        "career-mentorship-award-outcome-commit-rejected:"
                        + committed.Detail;
                    return false;
                }

                // Unknown acknowledgement keeps the actual wear and award. The journal
                // retries only this frozen receipt, never XP, mood, work or equipment use.
                durablyCommitted = true;
                reserved = false;
                mutationAttempted = false;
                CommittedReceipt = actual;
                failureReason = string.Empty;
                return true;
            }
            catch
            {
                CancelUncommitted();
                throw;
            }
        }

        internal void CancelUncommitted()
        {
            if (durablyCommitted)
                return;
            if (reserved)
            {
                awardFacts.Cancel(prepared);
                reserved = false;
            }
            if (mutationAttempted)
                RestoreCareer();
        }

        private void RestoreCareer()
        {
            CharacterCareerWorldSaveData snapshot = careerBefore
                ?? throw new InvalidOperationException(
                    "Career mentorship award rollback snapshot is unavailable.");
            careerPersistence.PublishRestore(
                careerPersistence.PrepareRestore(snapshot));
            mutationAttempted = false;
        }

        private static bool SameReceipt(
            in CareerMentorshipAwardCommitReceipt expected,
            in CareerMentorshipAwardCommitReceipt actual) =>
            expected.AcademyBuildingId.Equals(actual.AcademyBuildingId)
            && string.Equals(
                expected.LedgerStackId,
                actual.LedgerStackId,
                StringComparison.Ordinal)
            && expected.BeforeContentRevision == actual.BeforeContentRevision
            && expected.AfterContentRevision == actual.AfterContentRevision
            && string.Equals(
                expected.SourceOperationId,
                actual.SourceOperationId,
                StringComparison.Ordinal);
    }

    private static bool ValidPreflight(
        DurableFacilityEquipmentSlotSnapshot slot,
        DurableFacilityEquipmentRequirement requirement,
        DurableFacilityEquipmentUseSubject subject,
        double wearAmount) =>
        slot != null
        && requirement != null
        && subject != null
        && string.Equals(
            slot.PolicyId,
            CareerDurableEquipmentPolicySource.PolicyId,
            StringComparison.Ordinal)
        && string.Equals(
            requirement.RequirementId,
            CareerDurableEquipmentPolicySource.RequirementId,
            StringComparison.Ordinal)
        && requirement.ItemId.Equals(
            (ItemDefinitionId)DurableToolItemRules.CareerLedger)
        && Math.Abs(wearAmount - LedgerWearPerAward) <= 0.000001d;
}
