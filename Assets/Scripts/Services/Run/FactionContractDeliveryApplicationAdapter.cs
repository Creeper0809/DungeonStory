using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

public sealed class FactionContractWorldMapServices
{
    public FactionContractWorldMapServices(
        IFactionContractDeliveryQuery contracts,
        IEventAlertChoiceActionDispatcher actions)
    {
        Contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        Actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    public IFactionContractDeliveryQuery Contracts { get; }
    public IEventAlertChoiceActionDispatcher Actions { get; }
}

public sealed class FactionContractDeliveryApplicationAdapter :
    IStartable,
    ITickable
{
    private const float EvaluationIntervalSeconds = 0.5f;
    private readonly IFactionContractDeliveryResolutionCommand command;
    private readonly IV20MilestoneWorldSnapshotQuery world;
    private readonly IGameCalendar calendar;
    private float nextEvaluationTime;

    public FactionContractDeliveryApplicationAdapter(
        IFactionContractDeliveryResolutionCommand command,
        IV20MilestoneWorldSnapshotQuery world,
        IGameCalendar calendar)
    {
        this.command = command ?? throw new ArgumentNullException(nameof(command));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public void Start()
    {
        Advance();
        nextEvaluationTime = Time.time + EvaluationIntervalSeconds;
    }

    public void Tick()
    {
        if (Time.time < nextEvaluationTime)
            return;
        nextEvaluationTime = Time.time + EvaluationIntervalSeconds;
        Advance();
    }

    private void Advance()
    {
        if (!command.RequiresAdvance)
            return;
        int absoluteDay = Math.Max(1, calendar.Day);
        command.AdvanceFactionContractDeliveries(
            absoluteDay,
            world.Build(absoluteDay));
    }
}
