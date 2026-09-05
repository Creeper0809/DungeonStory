using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class CircusProgramContext
{
    public CircusProgramContext(
        CircusProgramRegistry programs,
        ICaptivityRuntime captivity,
        ICaptivityCommandService captivityCommands,
        IWildlifeCaptureRuntime wildlifeCapture,
        IExternalInfluenceRuntime externalInfluence,
        CircusPerformanceSupplyRuntime performanceSupplies)
    {
        Programs = programs ?? throw new ArgumentNullException(nameof(programs));
        Captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        CaptivityCommands = captivityCommands
            ?? throw new ArgumentNullException(nameof(captivityCommands));
        WildlifeCapture = wildlifeCapture
            ?? throw new ArgumentNullException(nameof(wildlifeCapture));
        ExternalInfluence = externalInfluence
            ?? throw new ArgumentNullException(nameof(externalInfluence));
        PerformanceSupplies = performanceSupplies
            ?? throw new ArgumentNullException(nameof(performanceSupplies));
    }

    public CircusProgramRegistry Programs { get; }
    public ICaptivityRuntime Captivity { get; }
    public ICaptivityCommandService CaptivityCommands { get; }
    public IWildlifeCaptureRuntime WildlifeCapture { get; }
    public IExternalInfluenceRuntime ExternalInfluence { get; }
    public CircusPerformanceSupplyRuntime PerformanceSupplies { get; }
}

public sealed class CircusWorldContext
{
    public CircusWorldContext(
        ICharacterAiWorldRegistry world,
        IGridSystemProvider gridProvider,
        IRoomLayoutCache rooms,
        IDoorAccessCommandService doorAccess,
        IWorldFilthQuery filth)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        GridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        DoorAccess = doorAccess
            ?? throw new ArgumentNullException(nameof(doorAccess));
        Filth = filth ?? throw new ArgumentNullException(nameof(filth));
    }

    public ICharacterAiWorldRegistry World { get; }
    public IGridSystemProvider GridProvider { get; }
    public IRoomLayoutCache Rooms { get; }
    public IDoorAccessCommandService DoorAccess { get; }
    public IWorldFilthQuery Filth { get; }
}

public sealed class CircusCombatContext
{
    public CircusCombatContext(
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthCommand bodyHealthCommands,
        ICombatResolutionService combat,
        ICombatEquipmentRuntime equipment,
        ICharacterMedicalQuery medicalQuery,
        ICharacterMedicalCommand medicalCommands,
        ICharacterPerformanceQuery performance)
    {
        BodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        BodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        Combat = combat ?? throw new ArgumentNullException(nameof(combat));
        Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        MedicalQuery = medicalQuery
            ?? throw new ArgumentNullException(nameof(medicalQuery));
        MedicalCommands = medicalCommands
            ?? throw new ArgumentNullException(nameof(medicalCommands));
        Performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
    }

    public ICharacterBodyHealthQuery BodyHealthQuery { get; }
    public ICharacterBodyHealthCommand BodyHealthCommands { get; }
    public ICombatResolutionService Combat { get; }
    public ICombatEquipmentRuntime Equipment { get; }
    public ICharacterMedicalQuery MedicalQuery { get; }
    public ICharacterMedicalCommand MedicalCommands { get; }
    public ICharacterPerformanceQuery Performance { get; }
}

public sealed class CircusSessionContext
{
    public CircusSessionContext(
        IGameMoneyAccount money,
        IGameClock clock,
        IRandomStreamProvider randomStreamProvider,
        IGameEventBus events,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        Money = money ?? throw new ArgumentNullException(nameof(money));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        RandomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
        Events = events ?? throw new ArgumentNullException(nameof(events));
        AggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IGameMoneyAccount Money { get; }
    public IGameClock Clock { get; }
    public IRandomStreamProvider RandomStreamProvider { get; }
    public IGameEventBus Events { get; }
    public DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
}

internal sealed class CircusRestoreStateContext
{
    internal CircusRestoreStateContext(
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CircusStateSession stateSession)
    {
        AggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        StateSession = stateSession
            ?? throw new ArgumentNullException(nameof(stateSession));
    }

    internal DungeonRuntimeAggregateRootStore AggregateRootStore { get; }
    internal CircusStateSession StateSession { get; }
}

internal interface ICircusMovementCommands
{
    void Clear();
    void ClearOrderActorProjection(CircusShowOrder order);
    List<Vector2Int> ChooseAudiencePositions(RoomInstance room, int count);
    List<Vector2Int> ChoosePositions(
        RoomInstance room,
        Vector2Int origin,
        int count,
        bool nearFirst);
    void StartParticipantMovement(CircusShowOrder order);
    void StartAudienceMovement(CircusShowOrder order);
    bool AreActorsAt(
        IReadOnlyList<string> actorIds,
        IReadOnlyList<Vector2Int> targets);
    bool AreParticipantsAt(CircusShowOrder order);
    void ReleaseOrderActors(CircusShowOrder order);
    void TickWildlifeReturns();
}
