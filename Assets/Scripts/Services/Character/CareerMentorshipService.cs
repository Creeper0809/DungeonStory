using System;
using System.Linq;

public interface ICareerMentorshipService
{
    void Assign(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId);
    void Clear(CharacterId studentCharacterId);
}

public sealed class CareerMentorshipService : ICareerMentorshipService
{
    private readonly ICareerService careers;
    private readonly ICharacterWorldQuery characters;
    private readonly IBuildingWorldQuery buildings;

    public CareerMentorshipService(
        ICareerService careers,
        ICharacterWorldQuery characters,
        IBuildingWorldQuery buildings)
    {
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
    }

    public void Assign(
        CharacterId mentorCharacterId,
        CharacterId studentCharacterId,
        BuildingInstanceId academyBuildingId)
    {
        RequireLivingActor(mentorCharacterId);
        RequireLivingActor(studentCharacterId);
        if (!careers.TryGet(
                mentorCharacterId,
                out CharacterCareerSnapshot mentorCareer)
            || mentorCareer.Position != CareerPositionKind.Mentor
            || !string.Equals(
                mentorCareer.PositionScopeId,
                academyBuildingId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The mentor must hold the academy's unique mentor position.");
        }
        RequireAcademy(academyBuildingId);
        careers.AssignMentorship(
            mentorCharacterId,
            studentCharacterId,
            academyBuildingId);
    }

    public void Clear(CharacterId studentCharacterId) =>
        careers.ClearMentorship(studentCharacterId);

    private CharacterActor RequireLivingActor(CharacterId characterId) =>
        characters.Characters.FirstOrDefault(actor => actor != null
            && !actor.IsDead
            && CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
            && id.Equals(characterId))
        ?? throw new InvalidOperationException(
            $"Mentorship character '{characterId.Value}' is not living in the world.");

    private BuildableObject RequireAcademy(BuildingInstanceId academyBuildingId) =>
        buildings.Buildings.FirstOrDefault(building => building != null
            && !building.isDestroy
            && building.PersistentInstanceId.Equals(academyBuildingId)
            && building.BuildingData?.ResearchFacilityCommand ==
                ResearchFacilityCommandKind.MentorAcademy)
        ?? throw new InvalidOperationException(
            $"Mentorship academy '{academyBuildingId.Value}' is not available.");
}
