using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WardenWorkExecutionUnityAdapter :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Warden };
    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCommandService commands;
    private readonly ICaptivityWorkReadinessQuery readiness;
    private readonly IWorkAmountCalculator workAmount;
    private readonly IGameClock clock;
    private readonly WardenWorkExecutionHandler flow =
        new WardenWorkExecutionHandler();

    public WardenWorkExecutionUnityAdapter(
        ICaptivityRuntime captivity,
        ICaptivityCommandService commands,
        ICaptivityWorkReadinessQuery readiness,
        IWorkAmountCalculator workAmount,
        IGameClock clock)
    {
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
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
        bool available = (TryFindInteraction(
                    captivity,
                    actor,
                    target,
                    out CaptiveState state)
                && readiness.IsInteractionReady(state.captiveId, out _))
            || TryFindRehabilitation(captivity, actor, target, out _);
        reason = available
            ? string.Empty
            : "진행할 포로 관리 또는 재사회화 작업이 없습니다.";
        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target) =>
        TryFindInteraction(captivity, actor, target, out CaptiveState state)
        && readiness.IsInteractionReady(state.captiveId, out _)
            ? CaptivityWorkExecutionRules.GetWardenUrgency(state)
            : TryFindRehabilitation(captivity, actor, target, out _)
                ? 75f
                : 0f;

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        ICaptivityWorkExecutionSession session = TryFindRehabilitation(
            captivity,
            context.Actor,
            context.Target,
            out CaptiveState rehabilitation)
                ? new RehabilitationSession(
                    context,
                    result,
                    captivity,
                    commands,
                    workAmount,
                    clock,
                    rehabilitation.captiveId)
                : new WardenSession(
                    context,
                    result,
                    captivity,
                    commands,
                    workAmount,
                    clock);
        return flow.Execute(session);
    }

    private static bool TryFindInteraction(
        ICaptivityRuntime captivity,
        CharacterActor actor,
        BuildableObject target,
        out CaptiveState captive)
    {
        string actorId = actor?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        captive = captivity.Captives.FirstOrDefault(state =>
            state.status == CaptivityStatus.Interaction
            && actorId.Length > 0
            && string.Equals(
                state.reservedWardenId,
                actorId,
                StringComparison.Ordinal)
            && target != null
            && captivity.TryGetHousing(
                state.captiveId,
                out BuildableObject housing)
            && ReferenceEquals(housing, target));
        return captive != null;
    }

    private static bool TryFindRehabilitation(
        ICaptivityRuntime captivity,
        CharacterActor actor,
        BuildableObject target,
        out CaptiveState minion)
    {
        string actorId = actor?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        minion = captivity.Captives.FirstOrDefault(state =>
            state.IsMinion
            && state.rehabilitationInProgress
            && actorId.Length > 0
            && string.Equals(
                state.reservedWardenId,
                actorId,
                StringComparison.Ordinal)
            && target != null
            && captivity.TryGetRehabilitationFacility(
                state.captiveId,
                out BuildableObject facility)
            && ReferenceEquals(facility, target));
        return minion != null;
    }

    private sealed class WardenSession : ICaptivityWorkExecutionSession
    {
        private readonly WorkExecutionContext context;
        private readonly WorkExecutionResult result;
        private readonly ICaptivityRuntime captivity;
        private readonly ICaptivityCommandService commands;
        private readonly IWorkAmountCalculator workAmount;
        private readonly IGameClock clock;
        private readonly string captiveId;

        public WardenSession(
            WorkExecutionContext context,
            WorkExecutionResult result,
            ICaptivityRuntime captivity,
            ICaptivityCommandService commands,
            IWorkAmountCalculator workAmount,
            IGameClock clock)
        {
            this.context = context;
            this.result = result;
            this.captivity = captivity;
            this.commands = commands;
            this.workAmount = workAmount;
            this.clock = clock;
            captiveId = TryFindInteraction(
                    captivity,
                    context.Actor,
                    context.Target,
                    out CaptiveState state)
                ? state.captiveId
                : string.Empty;
        }

        public bool CanContinue => context.CanContinue;
        public bool HasCurrentWork =>
            captivity.TryGetCaptive(captiveId, out CaptiveState current)
            && current.status == CaptivityStatus.Interaction;
        public bool IsCompleted =>
            captivity.TryGetCaptive(captiveId, out CaptiveState completed)
            && CaptivityWorkExecutionRules.IsWardenCompleted(completed);

        public bool TryAdvance(out string status)
        {
            if (!captivity.TryGetCaptive(captiveId, out CaptiveState before))
            {
                status = "Captive interaction state is unavailable.";
                return false;
            }

            float completedBefore = before.completedInteractionWork;
            float deltaWork = workAmount.CalculateWorkPerSecond(
                    context.Actor,
                    context.Target,
                    context.WorkTypeId,
                    context.Work.GetWorkEnvironmentDurationMultiplier(context.WorkTypeId))
                * clock.DeltaTime;
            bool succeeded = commands.AdvanceInteraction(
                captiveId,
                context.Actor,
                deltaWork,
                out status);
            if (succeeded
                && captivity.TryGetCaptive(captiveId, out CaptiveState after))
            {
                float accepted = Mathf.Max(
                    0f,
                    after.completedInteractionWork - completedBefore);
                if (accepted > 0f)
                {
                    context.RecordApprovedWork(
                        accepted,
                        Mathf.Max(
                            0f,
                            after.requiredInteractionWork
                                - after.completedInteractionWork));
                }
            }

            return succeeded;
        }

        public void SetStatus(string status) =>
            context.Actor?.Brain?.SetActionPhase(status, context.Target);

        public bool TrySuspendAtCheckpoint() =>
            context.TrySuspendAtCheckpoint();

        public void Complete(bool succeeded)
        {
            result.CompletedSuccessfully = succeeded;
            result.CompletionEffectsAlreadyApplied = true;
        }
    }

    private sealed class RehabilitationSession : ICaptivityWorkExecutionSession
    {
        private readonly WorkExecutionContext context;
        private readonly WorkExecutionResult result;
        private readonly ICaptivityRuntime captivity;
        private readonly ICaptivityCommandService commands;
        private readonly IWorkAmountCalculator workAmount;
        private readonly IGameClock clock;
        private readonly string captiveId;

        public RehabilitationSession(
            WorkExecutionContext context,
            WorkExecutionResult result,
            ICaptivityRuntime captivity,
            ICaptivityCommandService commands,
            IWorkAmountCalculator workAmount,
            IGameClock clock,
            string captiveId)
        {
            this.context = context;
            this.result = result;
            this.captivity = captivity;
            this.commands = commands;
            this.workAmount = workAmount;
            this.clock = clock;
            this.captiveId = captiveId ?? string.Empty;
        }

        public bool CanContinue => context.CanContinue;
        public bool HasCurrentWork =>
            captivity.TryGetCaptive(captiveId, out CaptiveState current)
            && current.IsMinion
            && current.rehabilitationInProgress;
        public bool IsCompleted =>
            captivity.TryGetCaptive(captiveId, out CaptiveState current)
            && current.IsMinion
            && !current.rehabilitationInProgress;

        public bool TryAdvance(out string status)
        {
            if (!captivity.TryGetCaptive(captiveId, out CaptiveState before)
                || !before.rehabilitationInProgress)
            {
                status = "재사회화 작업 상태를 찾을 수 없습니다.";
                return false;
            }

            float remainingBefore = Mathf.Max(
                0f,
                MinionIntegrationRules.RehabilitationRequiredWork
                    - before.completedRehabilitationWork);
            float deltaWork = workAmount.CalculateWorkPerSecond(
                    context.Actor,
                    context.Target,
                    context.WorkTypeId,
                    context.Work.GetWorkEnvironmentDurationMultiplier(
                        context.WorkTypeId))
                * clock.DeltaTime;
            bool succeeded = commands.AdvanceRehabilitation(
                captiveId,
                context.Actor,
                deltaWork,
                out status);
            if (succeeded)
            {
                float accepted = Mathf.Min(
                    Mathf.Max(0f, deltaWork),
                    remainingBefore);
                if (accepted > 0f)
                {
                    context.RecordApprovedWork(
                        accepted,
                        Mathf.Max(0f, remainingBefore - accepted));
                }
            }
            return succeeded;
        }

        public void SetStatus(string status) =>
            context.Actor?.Brain?.SetActionPhase(status, context.Target);

        public bool TrySuspendAtCheckpoint() =>
            context.TrySuspendAtCheckpoint();

        public void Complete(bool succeeded)
        {
            result.CompletedSuccessfully = succeeded;
            result.CompletionEffectsAlreadyApplied = true;
        }
    }
}

public sealed class PerformWorkExecutionUnityAdapter :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Perform };
    private readonly Func<ICircusRuntime> circusProvider;
    private readonly IWorkAmountCalculator workAmount;
    private readonly IGameClock clock;
    private readonly PerformWorkExecutionHandler flow =
        new PerformWorkExecutionHandler();

    public PerformWorkExecutionUnityAdapter(
        Func<ICircusRuntime> circusProvider,
        IWorkAmountCalculator workAmount,
        IGameClock clock)
    {
        this.circusProvider = circusProvider ?? throw new ArgumentNullException(nameof(circusProvider));
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
        bool available = TryGetOrder(circusProvider(), target, out _);
        reason = available ? string.Empty : "준비할 공연이 없습니다.";
        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target) =>
        CaptivityWorkExecutionRules.GetPerformUrgency(
            TryGetOrder(circusProvider(), target, out _));

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result) =>
        flow.Execute(new PerformSession(context, result, circusProvider(), workAmount, clock));

    private static bool TryGetOrder(
        ICircusRuntime circus,
        BuildableObject target,
        out CircusShowOrder order)
    {
        order = circus.Orders.FirstOrDefault(item =>
            item.state == CircusShowState.Composition
            && target != null
            && item.stagePosition == target.centerPos);
        return order != null;
    }

    private sealed class PerformSession : ICaptivityWorkExecutionSession
    {
        private readonly WorkExecutionContext context;
        private readonly WorkExecutionResult result;
        private readonly ICircusRuntime circus;
        private readonly IWorkAmountCalculator workAmount;
        private readonly IGameClock clock;
        private readonly string orderId;

        public PerformSession(
            WorkExecutionContext context,
            WorkExecutionResult result,
            ICircusRuntime circus,
            IWorkAmountCalculator workAmount,
            IGameClock clock)
        {
            this.context = context;
            this.result = result;
            this.circus = circus;
            this.workAmount = workAmount;
            this.clock = clock;
            orderId = TryGetOrder(circus, context.Target, out CircusShowOrder order)
                ? order.orderId
                : string.Empty;
        }

        public bool CanContinue => context.CanContinue;
        public bool HasCurrentWork => TryGetOrder(circus, context.Target, out _);
        public bool IsCompleted => circus.Orders.Any(item =>
            string.Equals(item.orderId, orderId, StringComparison.Ordinal)
            && CaptivityWorkExecutionRules.IsPerformancePreparationCompleted(item));

        public bool TryAdvance(out string status)
        {
            CircusShowOrder before = circus.Orders.FirstOrDefault(item =>
                string.Equals(item.orderId, orderId, StringComparison.Ordinal));
            if (before == null)
            {
                status = "Performance preparation state is unavailable.";
                return false;
            }

            float completedBefore = before.preparationWorkCompleted;
            float deltaWork = workAmount.CalculateWorkPerSecond(
                    context.Actor,
                    context.Target,
                    context.WorkTypeId,
                    context.Work.GetWorkEnvironmentDurationMultiplier(context.WorkTypeId))
                * clock.DeltaTime;
            bool succeeded = circus.AdvancePreparation(
                orderId,
                context.Actor,
                deltaWork,
                out status);
            if (succeeded)
            {
                CircusShowOrder after = circus.Orders.FirstOrDefault(item =>
                    string.Equals(item.orderId, orderId, StringComparison.Ordinal));
                if (after != null)
                {
                    float accepted = Mathf.Max(
                        0f,
                        after.preparationWorkCompleted - completedBefore);
                    if (accepted > 0f)
                    {
                        context.RecordApprovedWork(
                            accepted,
                            Mathf.Max(
                                0f,
                                after.preparationWorkRequired
                                    - after.preparationWorkCompleted));
                    }
                }
            }

            return succeeded;
        }

        public void SetStatus(string status) =>
            context.Actor?.Brain?.SetActionPhase(status, context.Target);

        public bool TrySuspendAtCheckpoint() =>
            context.TrySuspendAtCheckpoint();

        public void Complete(bool succeeded)
        {
            result.CompletedSuccessfully = succeeded;
            result.CompletionEffectsAlreadyApplied = true;
        }
    }
}
