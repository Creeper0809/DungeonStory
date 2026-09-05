using System;
using System.Linq;

public interface ICareerMentorshipService
{
    bool CanAssign(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId,
        CharacterProficiencyId proficiencyId,
        out string failureReason);
    void Assign(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId,
        CharacterProficiencyId proficiencyId);
    bool TryAssign(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId,
        CharacterProficiencyId proficiencyId,
        out string failureReason);
    void Clear(CharacterId studentCharacterId);
}

public sealed class CareerMentorshipService : ICareerMentorshipService
{
    private readonly ICareerService careers;
    private readonly ICharacterWorldQuery characters;
    private readonly IBuildingWorldQuery buildings;
    private readonly ICharacterProficiencyQuery proficiencies;
    private readonly IGameCalendar calendar;
    private readonly ICharacterSettlementStandingQuery settlementStandings;

    public CareerMentorshipService(
        ICareerService careers,
        ICharacterWorldQuery characters,
        IBuildingWorldQuery buildings,
        ICharacterProficiencyQuery proficiencies,
        IGameCalendar calendar,
        ICharacterSettlementStandingQuery settlementStandings = null)
    {
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.proficiencies = proficiencies
            ?? throw new ArgumentNullException(nameof(proficiencies));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.settlementStandings = settlementStandings;
    }

    public void Assign(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId,
        CharacterProficiencyId proficiencyId)
    {
        if (!TryAssign(
                mentorCharacterId,
                studentCharacterId,
                academyBuildingId,
                proficiencyId,
                out string failureReason))
        {
            throw new InvalidOperationException(failureReason);
        }
    }

    public bool TryAssign(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId,
        CharacterProficiencyId proficiencyId,
        out string failureReason)
    {
        if (!CanAssign(
                mentorCharacterId,
                studentCharacterId,
                academyBuildingId,
                proficiencyId,
                out failureReason))
        {
            return false;
        }

        careers.AssignMentorship(
            mentorCharacterId,
            studentCharacterId,
            academyBuildingId,
            proficiencyId);
        return true;
    }

    public bool CanAssign(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId,
        CharacterProficiencyId proficiencyId,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterActor mentor = FindLivingActor(mentorCharacterId);
        CharacterActor student = FindLivingActor(studentCharacterId);
        if (mentor == null || student == null)
        {
            failureReason = "멘토와 학생은 살아 있고 현재 던전에 있어야 합니다.";
            return false;
        }
        if (settlementStandings != null
            && (!settlementStandings.CanParticipateInMentoring(
                    mentor,
                    out failureReason)
                || !settlementStandings.CanParticipateInMentoring(
                    student,
                    out failureReason)))
        {
            return false;
        }
        if (mentorCharacterId.Equals(studentCharacterId))
        {
            failureReason = "자기 자신을 멘토로 지정할 수 없습니다.";
            return false;
        }
        if (!proficiencyId.IsValid)
        {
            failureReason = "가르칠 숙련을 선택해야 합니다.";
            return false;
        }
        if (FindAcademy(academyBuildingId) == null)
        {
            failureReason = "가동 중인 멘토 학원이 필요합니다.";
            return false;
        }
        if (!proficiencies.TryGetProficiency(
                mentorCharacterId,
                proficiencyId,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot mentorSkill)
            || !proficiencies.TryGetProficiency(
                studentCharacterId,
                proficiencyId,
                calendar.AbsoluteHour,
                out CharacterProficiencySnapshot studentSkill))
        {
            failureReason = "멘토 또는 학생의 숙련 기록을 찾을 수 없습니다.";
            return false;
        }
        if (mentorSkill.Rank < CharacterProficiencyRank.Expert)
        {
            failureReason = "멘토는 선택 숙련의 전문가 또는 대가여야 합니다.";
            return false;
        }
        if ((int)mentorSkill.Rank <= (int)studentSkill.Rank
            || mentorSkill.CurrentExperience - studentSkill.CurrentExperience < 200)
        {
            failureReason = "멘토는 학생보다 최소 1등급, 200 XP 이상 높아야 합니다.";
            return false;
        }
        if ((mentor.SocialMemory?.GetRelationshipSentiment(student) ?? 0f) < -0.20f
            || (student.SocialMemory?.GetRelationshipSentiment(mentor) ?? 0f) < -0.20f)
        {
            failureReason = "서로에 대한 관계 감정이 -0.20 미만이면 멘토링을 시작할 수 없습니다.";
            return false;
        }

        if (careers.Mentorships.Count(value =>
                value.MentorCharacterId.Equals(mentorCharacterId)
                && !value.StudentCharacterId.Equals(studentCharacterId)) >= 3)
        {
            failureReason = "한 멘토는 동시에 최대 3명의 학생만 담당할 수 있습니다.";
            return false;
        }

        return true;
    }

    public void Clear(CharacterId studentCharacterId) =>
        careers.ClearMentorship(studentCharacterId);

    private CharacterActor FindLivingActor(CharacterId characterId) =>
        characters.Characters.FirstOrDefault(actor => actor != null
            && !actor.IsDead
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            && id.Equals(characterId))
        ;

    private BuildableObject FindAcademy(BuildingInstanceId academyBuildingId) =>
        buildings.Buildings.FirstOrDefault(building => building != null
            && !building.isDestroy
            && !building.IsDamaged
            && building.PersistentInstanceId.Equals(academyBuildingId)
            && building.BuildingData?.ResearchFacilityCommand ==
                ResearchFacilityCommandKind.MentorAcademy);
}
