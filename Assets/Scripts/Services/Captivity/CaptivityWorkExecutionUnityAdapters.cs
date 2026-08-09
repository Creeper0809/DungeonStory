using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public sealed class WardenWorkExecutionUnityAdapter :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Ids = { BuiltInWorkTypeIds.Warden };
    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCommandService commands;
    private readonly IWorkAmountCalculator workAmount;
    private readonly IGameClock clock;
    private readonly WardenWorkExecutionHandler flow =
        new WardenWorkExecutionHandler();

    public WardenWorkExecutionUnityAdapter(
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
        bool available = TryFindInteraction(captivity, target, out _);
        reason = available ? string.Empty : "진행할 포로 관리 작업이 없습니다.";
        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target) =>
        TryFindInteraction(captivity, target, out CaptiveState state)
            ? CaptivityWorkExecutionRules.GetWardenUrgency(state)
            : 0f;

    public IEnumerator Execute(WorkExecutionContext context, WorkExecutionResult result) =>
        flow.Execute(new WardenSession(
            context,
            result,
            captivity,
            commands,
            workAmount,
            clock));

    private static bool TryFindInteraction(
        ICaptivityRuntime captivity,
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
            captiveId = TryFindInteraction(captivity, context.Target, out CaptiveState state)
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
            float deltaWork = workAmount.CalculateWorkPerSecond(
                    context.Actor,
                    context.Target,
                    context.WorkTypeId,
                    context.Work.GetWorkEnvironmentDurationMultiplier(context.WorkTypeId))
                * clock.DeltaTime;
            return commands.AdvanceInteraction(
                captiveId,
                context.Actor,
                deltaWork,
                out status);
        }

        public void SetStatus(string status) =>
            context.Actor?.Brain?.SetActionPhase(status, context.Target);

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
            float deltaWork = workAmount.CalculateWorkPerSecond(
                    context.Actor,
                    context.Target,
                    context.WorkTypeId,
                    context.Work.GetWorkEnvironmentDurationMultiplier(context.WorkTypeId))
                * clock.DeltaTime;
            return circus.AdvancePreparation(
                orderId,
                context.Actor,
                deltaWork,
                out status);
        }

        public void SetStatus(string status) =>
            context.Actor?.Brain?.SetActionPhase(status, context.Target);

        public void Complete(bool succeeded)
        {
            result.CompletedSuccessfully = succeeded;
            result.CompletionEffectsAlreadyApplied = true;
        }
    }
}
