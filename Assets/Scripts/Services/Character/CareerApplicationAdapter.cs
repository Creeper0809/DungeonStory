using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class CareerApplicationAdapter :
    IStartable,
    ITickable,
    IDisposable
{
    private readonly ICareerService careers;
    private readonly ICharacterWorldQuery world;
    private readonly IGameCalendar calendar;
    private readonly IGameClock clock;
    private readonly IBuildingWorldQuery buildings;
    private readonly IGameEventBus events;
    private readonly IWorldItemStackRuntime items;
    private IDisposable dayEndedSubscription;

    public CareerApplicationAdapter(
        ICareerService careers,
        ICharacterWorldQuery world,
        IGameCalendar calendar,
        IGameClock clock,
        IBuildingWorldQuery buildings,
        IGameEventBus events,
        IWorldItemStackRuntime items)
    {
        this.careers = careers ?? throw new ArgumentNullException(nameof(careers));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public void Start() => dayEndedSubscription ??=
        events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
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
    }

    private void OnDayEnded(OperatingDayEndedEvent ended)
    {
        foreach (CareerMentorshipSnapshot assignment in careers.Mentorships)
        {
            CharacterActor mentor = FindLivingActor(assignment.MentorCharacterId);
            CharacterActor student = FindLivingActor(assignment.StudentCharacterId);
            BuildableObject academy = buildings.Buildings.FirstOrDefault(building =>
                building != null && !building.isDestroy
                && building.PersistentInstanceId.Equals(assignment.AcademyBuildingId)
                && building.BuildingData?.ResearchFacilityCommand ==
                    ResearchFacilityCommandKind.MentorAcademy);
            if (mentor == null || student?.Progression == null || academy == null
                || !careers.TryGet(
                    assignment.MentorCharacterId,
                    out CharacterCareerSnapshot mentorCareer)
                || mentorCareer.Position != CareerPositionKind.Mentor
                || !string.Equals(
                    mentorCareer.PositionScopeId,
                    assignment.AcademyBuildingId.Value,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryUseCareerLedger(academy))
            {
                continue;
            }

            if (careers.TryMarkMentoringAwarded(
                    assignment.StudentCharacterId,
                    ended.day))
            {
                student.Progression.AddExperience(
                    careers.ResolveMentoringXp(int.MaxValue));
            }
        }
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
