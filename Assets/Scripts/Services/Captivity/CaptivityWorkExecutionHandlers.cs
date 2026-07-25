using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WardenWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Warden };
    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCommandService commands;
    private readonly IWorkAmountCalculator workAmount;
    private readonly IGameClock clock;

    public WardenWorkExecutionHandler(
        ICaptivityRuntime captivity,
        ICaptivityCommandService commands,
        IWorkAmountCalculator workAmount,
        IGameClock clock)
    {
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.workAmount = workAmount ?? throw new ArgumentNullException(nameof(workAmount));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        bool available = TryFindInteraction(target, out _);
        reason = available ? string.Empty : "진행할 포로 관리 작업이 없습니다.";
        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return TryFindInteraction(target, out CaptiveState state)
            ? Mathf.Clamp01(0.45f + state.escapeRisk * 0.005f)
            : 0f;
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        if (!TryFindInteraction(context.Target, out CaptiveState captive))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        while (context.CanContinue
            && captivity.TryGetCaptive(captive.captiveId, out CaptiveState current)
            && current.status == CaptivityStatus.Interaction)
        {
            float deltaWork = workAmount.CalculateWorkPerSecond(
                    context.Actor,
                    context.Target,
                    context.WorkTypeId,
                    context.Work.GetWorkEnvironmentDurationMultiplier(context.WorkTypeId))
                * clock.DeltaTime;
            if (!commands.AdvanceInteraction(
                    current.captiveId,
                    context.Actor,
                    deltaWork,
                    out string status))
            {
                result.CompletedSuccessfully = false;
                context.Actor?.Brain?.SetActionPhase(status, context.Target);
                yield break;
            }

            context.Actor?.Brain?.SetActionPhase(status, context.Target);
            yield return null;
        }

        result.CompletedSuccessfully =
            captivity.TryGetCaptive(captive.captiveId, out CaptiveState completed)
            && completed.status == CaptivityStatus.Confined;
        result.CompletionEffectsAlreadyApplied = true;
    }

    private bool TryFindInteraction(
        BuildableObject target,
        out CaptiveState captive)
    {
        captive = captivity.Captives.FirstOrDefault(state =>
            state.status == CaptivityStatus.Interaction
            && target != null
            && captivity.TryGetHousing(
                state.captiveId,
                out BuildableObject housing)
            && ReferenceEquals(housing, target));
        return captive != null;
    }
}

public sealed class PerformWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Perform };
    private readonly ICircusRuntime circus;
    private readonly IWorkAmountCalculator workAmount;
    private readonly IGameClock clock;

    public PerformWorkExecutionHandler(
        ICircusRuntime circus,
        IWorkAmountCalculator workAmount,
        IGameClock clock)
    {
        this.circus = circus ?? throw new ArgumentNullException(nameof(circus));
        this.workAmount = workAmount ?? throw new ArgumentNullException(nameof(workAmount));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Ids;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        bool available = TryGetOrder(target, out _);
        reason = available ? string.Empty : "준비할 공연이 없습니다.";
        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return TryGetOrder(target, out _) ? 0.7f : 0f;
    }

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result)
    {
        if (!TryGetOrder(context.Target, out CircusShowOrder order))
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        while (context.CanContinue
            && TryGetOrder(context.Target, out CircusShowOrder current)
            && current.state == CircusShowState.Composition)
        {
            float deltaWork = workAmount.CalculateWorkPerSecond(
                    context.Actor,
                    context.Target,
                    context.WorkTypeId,
                    context.Work.GetWorkEnvironmentDurationMultiplier(context.WorkTypeId))
                * clock.DeltaTime;
            if (!circus.AdvancePreparation(
                    order.orderId,
                    context.Actor,
                    deltaWork,
                    out string status))
            {
                result.CompletedSuccessfully = false;
                context.Actor?.Brain?.SetActionPhase(status, context.Target);
                yield break;
            }

            context.Actor?.Brain?.SetActionPhase(status, context.Target);
            yield return null;
        }

        result.CompletedSuccessfully =
            circus.Orders.Any(item =>
                string.Equals(item.orderId, order.orderId, StringComparison.Ordinal)
                && item.state != CircusShowState.Composition
                && item.state != CircusShowState.Cancelled);
        result.CompletionEffectsAlreadyApplied = true;
    }

    private bool TryGetOrder(
        BuildableObject target,
        out CircusShowOrder order)
    {
        order = circus.Orders.FirstOrDefault(item =>
            item.state == CircusShowState.Composition
            && target != null
            && item.stagePosition == target.centerPos);
        return order != null;
    }
}
