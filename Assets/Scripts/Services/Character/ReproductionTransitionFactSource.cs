using System;
using System.Linq;
using DungeonStory.Foundation;

public interface IReproductionFactNameQuery
{
    bool TryCharacter(CharacterId id, out string name);
    bool TryFacility(string id, out string name);
}

public sealed class ReproductionFactNameQuery : IReproductionFactNameQuery
{
    private readonly ICharacterWorldQuery world;
    private readonly ICharacterLifetimeQuery lifetime;
    private readonly IBuildingWorldQuery buildings;
    public ReproductionFactNameQuery(ICharacterWorldQuery world, ICharacterLifetimeQuery lifetime, IBuildingWorldQuery buildings)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
    }
    public bool TryCharacter(CharacterId id, out string name)
    {
        var actor = (world.Characters ?? Array.Empty<CharacterActor>()).Concat(lifetime.AllCharacters ?? Array.Empty<CharacterActor>())
            .FirstOrDefault(value => value?.Identity != null && value.Identity.PersistentId == id.Value);
        name = actor?.Identity?.DisplayName?.Trim() ?? string.Empty;
        return name.Length > 0;
    }
    public bool TryFacility(string id, out string name)
    {
        var facility = (buildings.Buildings ?? Array.Empty<BuildableObject>())
            .FirstOrDefault(value => value != null && value.PersistentInstanceId.Value == id);
        name = facility?.BuildingData?.objectName?.Trim() ?? string.Empty;
        return name.Length > 0;
    }
}

public sealed class ReproductionTransitionFactSource
{
    private readonly IReproductionFactNameQuery names;
    public ReproductionTransitionFactSource(IReproductionFactNameQuery names) =>
        this.names = names ?? throw new ArgumentNullException(nameof(names));

    public ReproductionTransitionFact Create(ReproductionProcessSaveData before, ReproductionProcess after,
        string kind, string cause, string costs, int day, ReproductionFactLocation location,
        string parentPublicationKey = "", CharacterId responder = default, int deadline = 0, CharacterId offspring = default)
    {
        if (after == null) throw new ArgumentNullException(nameof(after));
        if (!names.TryFacility(after.SupportFacilityInstanceId, out string facilityName))
            throw new InvalidOperationException("reproduction-facility-name-missing:" + after.SupportFacilityInstanceId);
        var data = new ReproductionTransitionFactData
        {
            processId = after.ProcessId, transitionKind = kind, mode = after.Mode,
            phenotypeSpeciesId = after.PhenotypeSpeciesId.Value, hasBefore = before != null,
            statusBefore = before?.status ?? default, statusAfter = after.Status,
            failureBefore = before?.failure ?? default, failureAfter = after.Failure,
            cause = cause, costs = costs, facilityId = after.SupportFacilityInstanceId, facilityName = facilityName,
            absoluteDay = day, revisionBefore = before?.transitionRevision ?? checked(after.TransitionRevision - 1),
            revisionAfter = after.TransitionRevision, phaseBefore = before?.currentPhaseIndex ?? 0,
            phaseAfter = after.CurrentPhaseIndex, phaseElapsedBefore = before?.currentPhaseElapsedDays ?? 0,
            phaseElapsedAfter = after.CurrentPhaseElapsedDays, progressBefore = before?.totalProgressDays ?? 0,
            progressAfter = after.TotalProgressDays, emergencyDeadlineDay = deadline,
            locationInstanceId = location.InstanceId ?? string.Empty, locationRegionId = location.RegionId ?? string.Empty,
            locationX = location.X, locationY = location.Y, parentPublicationKey = parentPublicationKey ?? string.Empty,
            previousPublicationKey = before?.lastPublicationKey ?? string.Empty
        };
        Add(after.FirstParentId, "genetic-parent");
        if (after.SecondParentId.IsValid) Add(after.SecondParentId, "genetic-parent");
        Add(after.CarrierId, "reproduction-carrier");
        if (responder.IsValid) Add(responder, "medical-responder");
        if (offspring.IsValid) Add(offspring, "offspring");
        return new ReproductionTransitionFact(data);

        void Add(CharacterId id, string role)
        {
            if (!names.TryCharacter(id, out string name))
                throw new InvalidOperationException("reproduction-character-name-missing:" + id.Value);
            data.people.Add(new ReproductionTransitionPersonData { characterId = id.Value, name = name, role = role });
        }
    }
}
