using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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
            equipmentUse);
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

            if (!progress.HasCompletedPhysicalLesson
                || progress.LastAwardAbsoluteDay >= lessonDay
                || !careerEquipment.TryCommitAward(
                    academy.RequirePersistentInstanceId(),
                    academy.centerPos,
                    () => careers.TryMarkMentoringAwarded(
                        assignment.StudentCharacterId,
                        lessonDay)))
            {
                continue;
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
            mentor.AddLog($"{student.Identity?.DisplayName ?? student.name}에게 {assignment.ProficiencyId.Value} 숙련을 가르쳤다.");
            student.AddLog($"{mentor.Identity?.DisplayName ?? mentor.name}에게 {assignment.ProficiencyId.Value} 숙련을 배웠다.");
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

    private CharacterActor FindLivingActor(CharacterId characterId) =>
        world.Characters.FirstOrDefault(actor => actor != null
            && !actor.IsDead
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            && id.Equals(characterId));
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

    public CareerDurableEquipmentAwardRuntime(
        IDurableFacilityEquipmentPolicyQuery policies,
        IDurableFacilityEquipmentSlotCommand slots,
        IDurableFacilityEquipmentUseCommand use)
    {
        this.policies = policies
            ?? throw new ArgumentNullException(nameof(policies));
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.use = use ?? throw new ArgumentNullException(nameof(use));
    }

    [GameplayInternalOnly(
        "Commits one mentorship award through the registered career-ledger slot.",
        "CareerApplicationAdapter only")]
    public bool TryCommitAward(
        BuildingInstanceId academyId,
        UnityEngine.Vector2Int academyPosition,
        Func<bool> commitAward)
    {
        if (!academyId.IsValid || commitAward == null)
            throw new ArgumentException("Career equipment award input is invalid.");
        if (!policies.TryGetPolicy(
                CareerDurableEquipmentPolicySource.PolicyId,
                out DurableFacilityEquipmentPolicy policy))
        {
            throw new InvalidOperationException(
                "The career-ledger durable-equipment policy is not registered.");
        }

        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
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

        DurableFacilityEquipmentUseResult result = use.TryApplyWearAndEffect(
            assignment.Key,
            CareerDurableEquipmentPolicySource.RequirementId,
            LedgerWearPerAward,
            new CareerMentorshipAwardEffect(commitAward));
        return result.Succeeded;
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
            bool valid = slot != null
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
}
