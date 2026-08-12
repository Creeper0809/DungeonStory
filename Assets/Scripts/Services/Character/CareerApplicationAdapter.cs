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
    private readonly IWorldItemStackRuntime items;
    private readonly ICharacterProficiencyQuery proficiencyQuery;
    private readonly ICharacterProficiencyCommand proficiencyCommands;
    private readonly CharacterMoodPolicyService moods;
    private readonly Dictionary<CharacterId, float> nextAssignmentAttemptAt = new();

    public CareerApplicationAdapter(
        ICareerService careers,
        ICharacterWorldQuery world,
        IGameCalendar calendar,
        IGameClock clock,
        IBuildingWorldQuery buildings,
        IWorldItemStackRuntime items,
        ICharacterProficiencyQuery proficiencyQuery,
        ICharacterProficiencyCommand proficiencyCommands,
        CharacterMoodPolicyService moods = null)
    {
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.proficiencyQuery = proficiencyQuery
            ?? throw new ArgumentNullException(nameof(proficiencyQuery));
        this.proficiencyCommands = proficiencyCommands
            ?? throw new ArgumentNullException(nameof(proficiencyCommands));
        this.moods = moods;
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
                || !TryUseCareerLedger(academy)
                || !careers.TryMarkMentoringAwarded(
                    assignment.StudentCharacterId,
                    lessonDay))
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

    private bool TryUseCareerLedger(BuildableObject academy)
    {
        string destinationId = academy.PersistentInstanceId.Value;
        WorldItemStackSnapshot ledger = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    DurableToolItemRules.CareerLedger,
                    StringComparison.Ordinal)
                && DurableToolItemRules.ReadCurrentDurability(
                    stack.ItemId,
                    stack.Components) > 0f)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (ledger == null)
        {
            if (!items.GetAllStacks().Any(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        DurableToolItemRules.CareerLedger,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)))
            {
                items.TryRequestItemDelivery(
                    DurableToolItemRules.CareerLedger,
                    1,
                    academy.centerPos,
                    destinationId,
                    out _,
                    out _);
            }
            return false;
        }

        float current = DurableToolItemRules.ReadCurrentDurability(
            ledger.ItemId,
            ledger.Components);
        return items.TrySetInstanceComponent(
            ledger.StackId,
            DurableToolItemRules.CreateDurability(ledger.ItemId, current - 0.5f));
    }

    private CharacterActor FindLivingActor(CharacterId characterId) =>
        world.Characters.FirstOrDefault(actor => actor != null
            && !actor.IsDead
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            && id.Equals(characterId));
}
