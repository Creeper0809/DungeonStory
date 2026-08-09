using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;
using static CircusRuntimeQueries;

public sealed class CircusRuntime :
    ICircusRuntime,
    ICircusPersistence,
    IDungeonRestoreTransactionParticipant,
    ITickable,
    IStartable,
    IDisposable
{
    private const string PerformancePropBoxItemId = "supply:performance-prop-box";
    private const float BanquetCartWearPerShow = 4f;

    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CircusRuntime.Tick");

    private readonly CircusProgramRegistry programs;
    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCommandService captivityCommands;
    private readonly IWildlifeCaptureRuntime wildlifeCapture;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IGridSystemProvider gridProvider;
    private readonly IRoomLayoutCache rooms;
    private readonly IGameMoneyAccount money;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly ICombatResolutionService combat;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ICharacterMedicalQuery medicalQuery;
    private readonly ICharacterMedicalCommand medicalCommands;
    private readonly IWorldFilthQuery filth;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly IGameEventBus events;
    private readonly IExternalInfluenceRuntime externalInfluence;
    private readonly IWorldItemStackRuntime items;
    private readonly CircusProgramForecastService forecastService;
    private readonly CircusProgramForecastProjectionAdapter forecastProjection;
    private readonly CircusStateSession stateSession;
    private readonly ICircusRestoreLifecycle restoreLifecycle;
    private readonly ICircusMovementCommands movement;
    private IDisposable invasionSubscription;

    private IReadOnlyList<CircusShowOrder> orders => stateSession.Orders;
    private int nextOrderSequence
    {
        get => stateSession.NextOrderSequence;
        set => stateSession.NextOrderSequence = value;
    }

    public CircusRuntime(
        CircusProgramContext program,
        CircusWorldContext worldContext,
        CircusCombatContext combatContext,
        CircusSessionContext session)
    {
        program = program ?? throw new ArgumentNullException(nameof(program));
        worldContext = worldContext
            ?? throw new ArgumentNullException(nameof(worldContext));
        combatContext = combatContext
            ?? throw new ArgumentNullException(nameof(combatContext));
        session = session ?? throw new ArgumentNullException(nameof(session));
        programs = program.Programs;
        captivity = program.Captivity;
        captivityCommands = program.CaptivityCommands;
        wildlifeCapture = program.WildlifeCapture;
        externalInfluence = program.ExternalInfluence;
        items = program.Items;
        world = worldContext.World;
        gridProvider = worldContext.GridProvider;
        rooms = worldContext.Rooms;
        filth = worldContext.Filth;
        bodyHealthQuery = combatContext.BodyHealthQuery;
        bodyHealthCommands = combatContext.BodyHealthCommands;
        combat = combatContext.Combat;
        equipment = combatContext.Equipment;
        medicalQuery = combatContext.MedicalQuery;
        medicalCommands = combatContext.MedicalCommands;
        money = session.Money;
        clock = session.Clock;
        random = session.RandomStreamProvider
            .Get("circus.runtime");
        events = session.Events;
        DungeonRuntimeAggregateRootStore aggregateRootStore =
            session.AggregateRootStore;
        stateSession = new CircusStateSession(
            aggregateRootStore,
            ClearOrderActorProjection,
            ClearTransientProjection);
        forecastService = new CircusProgramForecastService();
        forecastProjection = new CircusProgramForecastProjectionAdapter(
            captivity,
            wildlifeCapture,
            TryGetValidCircusRoom,
            SelectAudience);
        movement = CircusRuntimeInfrastructureFactory.CreateMovement(
            program,
            worldContext,
            session);
        restoreLifecycle = CircusRuntimeInfrastructureFactory.CreateRestore(
            program,
            worldContext,
            new CircusRestoreStateContext(
                aggregateRootStore,
                stateSession));
    }

    public IReadOnlyList<CircusProgramModule> Programs => programs.Definitions;
    public IReadOnlyList<CircusShowOrder> Orders => orders.Select(order => order.Clone()).ToArray();

    public CircusProgramForecast GetForecast(
        BuildableObject stage,
        string programId,
        CircusLethalityPolicy lethality,
        IReadOnlyList<string> performerIds,
        IReadOnlyList<string> wildlifeIds)
    {
        if (!programs.TryGet(programId, out ICircusProgramHandler handler))
        {
            return CircusProgramForecastService.Unavailable(
                "공연 프로그램을 찾을 수 없습니다.");
        }

        if (!forecastProjection.TryProject(
                stage,
                handler.Definition.publiclyCruel,
                performerIds,
                wildlifeIds,
                out CircusProgramForecastContext context,
                out string failureReason))
        {
            return CircusProgramForecastService.Unavailable(failureReason);
        }

        return forecastService.GetForecast(handler, context, lethality);
    }

    public void Start()
    {
        stateSession.EnsureProjection();
        invasionSubscription = events.Subscribe<InvasionStartedEvent>(
            _ => CancelActiveShows(
                orders,
                Cancel,
                "침공이 시작되어 공연을 취소했습니다."));
    }

    public void Dispose()
    {
        invasionSubscription?.Dispose();
        invasionSubscription = null;
        ClearTransientProjection();
    }

    private void ClearTransientProjection()
    {
        movement.Clear();
    }

    private void ClearOrderActorProjection(CircusShowOrder order)
    {
        movement.ClearOrderActorProjection(order);
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        stateSession.EnsureProjection();
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        for (int index = 0; index < orders.Count; index++)
        {
            CircusShowOrder order = orders[index];
            if (order != null && !order.IsTerminal)
            {
                TickOrder(order);
            }
        }

        movement.TickWildlifeReturns();
    }

    public bool TrySchedule(
        BuildableObject stage,
        string programId,
        CircusLethalityPolicy lethality,
        IReadOnlyList<string> performerIds,
        IReadOnlyList<string> wildlifeIds,
        out CircusShowOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        BuildingCircusStageAbility stageAbility =
            stage?.BuildingData.GetCircusStageAbility();
        if (stage == null || stageAbility == null || !stageAbility.IsValid)
        {
            failureReason = "유효한 서커스 무대가 아닙니다.";
            return false;
        }

        if (!programs.TryGet(programId, out ICircusProgramHandler program))
        {
            failureReason = "공연 프로그램을 찾을 수 없습니다.";
            return false;
        }

        if (!TryGetValidCircusRoom(stage, out RoomInstance room, out failureReason))
        {
            return false;
        }

        CircusVenueModifiers venue = CircusVenueEvaluator.Evaluate(
            room,
            program.Definition.publiclyCruel);
        List<CaptiveState> performers = (performerIds ?? Array.Empty<string>())
            .Select(id => captivity.TryGetCaptive(id, out CaptiveState captive) ? captive : null)
            .Where(captive => captive != null && captive.IsActive)
            .Take(stageAbility.performerCapacity)
            .ToList();
        foreach (CaptiveState performer in performers)
        {
            CharacterActor actor = FindActor(world, performer.captiveId);
            CharacterBodyHealthSnapshot health = bodyHealthQuery.GetSnapshot(actor);
            bool injured = actor == null
                || health.Downed
                || health.BloodLoss > 0.01f
                || health.Parts.Any(part =>
                    part != null && part.currentHealth + 0.01f < part.maxHealth);
            if (!injured)
            {
                continue;
            }

            if (actor != null)
            {
                medicalCommands.TryRequestTreatment(actor, out _, out _);
            }

            failureReason = $"{performer.displayName}은 부상 치료가 끝날 때까지 공연할 수 없습니다.";
            return false;
        }

        CircusShowOrder candidate = new CircusShowOrder
        {
            orderId = $"circus:{++nextOrderSequence}",
            stageId = GetBuildingId(stage, "stage"),
            stagePosition = stage.centerPos,
            roomId = room.Id,
            programId = program.Definition.programId,
            lethality = lethality,
            preparationWorkRequired = Mathf.Max(
                1f,
                stageAbility.preparationWork * venue.PreparationWorkMultiplier),
            showDurationSeconds = Mathf.Max(5f, stageAbility.showDurationSeconds),
            ticketPrice = Mathf.Max(
                1,
                Mathf.RoundToInt(stageAbility.baseTicketPrice * venue.RevenueMultiplier)),
            performerIds = performers.Select(item => item.captiveId).ToList(),
            wildlifeIds = (wildlifeIds ?? Array.Empty<string>())
                .Where(wildlifeCapture.IsCaptured)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            venueSatisfactionBonus = venue.SatisfactionBonus,
            venueAccidentRiskBonus = venue.AccidentRiskBonus,
            venueAccidentDamageMultiplier = venue.AccidentDamageMultiplier,
            venueFilthMultiplier = venue.FilthMultiplier,
            venueWitnessMoodPenalty = venue.WitnessMoodPenalty,
            venueGamblingVariance = venue.GamblingVariance,
            venueFlatRevenuePerAudience = venue.FlatRevenuePerAudience,
            statusMessage = "공연 준비 작업 대기"
        };
        if (!program.Validate(candidate, performers, out failureReason))
        {
            return false;
        }

        foreach (string captiveId in candidate.performerIds)
        {
            if (!captivityCommands.TryAssignPerformer(captiveId, true, out failureReason))
            {
                ReleasePerformers(
                    candidate,
                    captiveId => captivityCommands.TryAssignPerformer(
                        captiveId,
                        false,
                        out _));
                return false;
            }
        }

        foreach (string wildlifeId in candidate.wildlifeIds)
        {
            if (!wildlifeCapture.TryAssignToShow(
                    wildlifeId,
                    candidate.orderId,
                    out failureReason))
            {
                ReleasePerformers(
                    candidate,
                    captiveId => captivityCommands.TryAssignPerformer(
                        captiveId,
                        false,
                        out _));
                return false;
            }
        }

        candidate.audienceIds = SelectAudience(room).Select(GetCharacterId).ToList();
        List<Vector2Int> participantPositions = movement.ChoosePositions(
            room,
            stage.centerPos,
            candidate.performerIds.Count + candidate.wildlifeIds.Count,
            nearFirst: true);
        candidate.performerPositions = participantPositions
            .Take(candidate.performerIds.Count)
            .ToList();
        candidate.wildlifePositions = participantPositions
            .Skip(candidate.performerIds.Count)
            .Take(candidate.wildlifeIds.Count)
            .ToList();
        candidate.audiencePositions = movement.ChooseAudiencePositions(
            room,
            candidate.audienceIds.Count);
        stateSession.Add(candidate);
        order = candidate.Clone();
        return true;
    }

    public bool AdvancePreparation(
        string orderId,
        CharacterActor worker,
        float workAmount,
        out string status)
    {
        CircusShowOrder order = FindOrder(orders, orderId);
        if (order == null || order.state != CircusShowState.Composition)
        {
            status = "준비 중인 공연이 없습니다.";
            return false;
        }

        float nextCompleted = Mathf.Min(
            order.preparationWorkRequired,
            order.preparationWorkCompleted + Mathf.Max(0f, workAmount));
        float progress = nextCompleted
            / Mathf.Max(0.01f, order.preparationWorkRequired);
        if (progress >= 0.999f
            && !TryCommitShowSupplies(order, out status))
        {
            order.statusMessage = status;
            return false;
        }

        order.preparationWorkCompleted = nextCompleted;
        status = $"공연 준비 {progress:P0}";
        order.statusMessage = status;
        if (progress >= 0.999f)
        {
            order.state = CircusShowState.ParticipantEscort;
            order.phaseElapsedSeconds = 0f;
            order.statusMessage = "참가자 호송 중";
            movement.StartParticipantMovement(order);
        }

        return true;
    }

    private bool TryCommitShowSupplies(
        CircusShowOrder order,
        out string status)
    {
        status = string.Empty;
        WorldItemStackSnapshot propBox = FindUsableStageItem(
            order,
            PerformancePropBoxItemId,
            requireDurability: false);
        WorldItemStackSnapshot cart = FindUsableStageItem(
            order,
            DurableToolItemRules.BanquetCart,
            requireDurability: true);
        if (propBox == null || cart == null)
        {
            List<string> requested = new List<string>();
            if (propBox == null
                && items.TryRequestItemDelivery(
                    PerformancePropBoxItemId,
                    1,
                    order.stagePosition,
                    order.stageId,
                    out int propRequested,
                    out _)
                && propRequested > 0)
            {
                requested.Add("공연 소품 상자");
            }
            if (cart == null
                && items.TryRequestItemDelivery(
                    DurableToolItemRules.BanquetCart,
                    1,
                    order.stagePosition,
                    order.stageId,
                    out int cartRequested,
                    out _)
                && cartRequested > 0)
            {
                requested.Add("연회 운반 수레");
            }

            status = requested.Count > 0
                ? $"공연 준비품 배송 대기: {string.Join(", ", requested)}"
                : "공연 소품 상자와 사용 가능한 연회 운반 수레가 무대 버퍼에 필요합니다.";
            return false;
        }

        if (!items.TryConsumeFacilityItemBuffer(
                order.stageId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [PerformancePropBoxItemId] = 1
                },
                out string failureReason))
        {
            status = string.IsNullOrWhiteSpace(failureReason)
                ? "공연 소품 상자를 소비하지 못했습니다."
                : failureReason;
            return false;
        }

        float current = DurableToolItemRules.ReadCurrentDurability(
            cart.ItemId,
            cart.Components);
        if (!items.TrySetInstanceComponent(
                cart.StackId,
                DurableToolItemRules.CreateDurability(
                    cart.ItemId,
                    current - BanquetCartWearPerShow)))
        {
            throw new InvalidOperationException(
                $"Validated banquet cart '{cart.StackId}' disappeared during show preparation.");
        }

        return true;
    }

    private WorldItemStackSnapshot FindUsableStageItem(
        CircusShowOrder order,
        string itemId,
        bool requireDurability)
    {
        return items.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, order.stageId, StringComparison.Ordinal)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && stack.Quantity > 0
                && (!requireDurability
                    || DurableToolItemRules.ReadCurrentDurability(
                        stack.ItemId,
                        stack.Components) > 0f))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public bool Cancel(string orderId, string reason)
    {
        CircusShowOrder order = FindOrder(orders, orderId);
        if (order == null || order.IsTerminal)
        {
            return false;
        }

        order.state = CircusShowState.Cancelled;
        order.statusMessage = string.IsNullOrWhiteSpace(reason)
            ? "공연 취소"
            : reason;
        movement.ReleaseOrderActors(order);
        return true;
    }

    public CircusSaveData Capture()
    {
        return stateSession.Capture(wildlifeCapture.Capture());
    }

    public string ParticipantId => restoreLifecycle.ParticipantId;

    public CircusRestoreCandidate BuildRestore(CircusSaveData saveData) =>
        restoreLifecycle.BuildRestore(saveData);

    public void PublishRestoreCandidate(CircusRestoreCandidate candidate) =>
        restoreLifecycle.StageRestore(candidate);

    public void BeginRestoreCandidate() =>
        restoreLifecycle.BeginRestoreCandidate();

    public void PublishRestoreCandidate() =>
        restoreLifecycle.PublishRestoreCandidate();

    public void RollbackPublishedRestoreCandidate() =>
        restoreLifecycle.RollbackPublishedRestoreCandidate();

    public void CompleteRestoreCandidate() =>
        restoreLifecycle.CompleteRestoreCandidate();

    public void DiscardRestoreCandidate() =>
        restoreLifecycle.DiscardRestoreCandidate();

    private void TickOrder(CircusShowOrder order)
    {
        switch (order.state)
        {
            case CircusShowState.ParticipantEscort:
                order.phaseElapsedSeconds += clock.DeltaTime;
                if (movement.AreParticipantsAt(order))
                {
                    order.state = CircusShowState.AudienceEntering;
                    order.phaseElapsedSeconds = 0f;
                    order.statusMessage = "관객 입장 중";
                    movement.StartAudienceMovement(order);
                }
                else if (order.phaseElapsedSeconds >= 30f)
                {
                    Cancel(order.orderId, "참가자 호송 경로가 막혀 공연을 취소했습니다.");
                }
                break;
            case CircusShowState.AudienceEntering:
                order.phaseElapsedSeconds += clock.DeltaTime;
                if (movement.AreActorsAt(order.audienceIds, order.audiencePositions))
                {
                    order.state = CircusShowState.Performing;
                    order.phaseElapsedSeconds = 0f;
                    order.statusMessage = "공연 중";
                    order.nextCombatExchangeAt = 0.75f;
                    CheckPerformanceBetrayals(order);
                }
                else if (order.phaseElapsedSeconds >= 30f)
                {
                    Cancel(order.orderId, "관객 입장 경로가 막혀 공연을 취소했습니다.");
                }
                break;
            case CircusShowState.Performing:
                TickPerformance(order);
                break;
            case CircusShowState.Settlement:
                Settle(order);
                break;
            case CircusShowState.CleanupAndTreatment:
                bool cleanupPending = order.cleanupRequired
                    && filth.GetAt(order.stagePosition).Any(entry =>
                        string.Equals(
                            entry.SourceCharacterId,
                            order.orderId,
                            StringComparison.Ordinal));
                HashSet<string> performerIds = new HashSet<string>(
                    order.performerIds ?? new List<string>(),
                    StringComparer.Ordinal);
                bool treatmentPending = order.treatmentRequired
                    && medicalQuery.ActiveOrders.Any(item =>
                        item.IsActive && performerIds.Contains(item.patientId));
                if (cleanupPending || treatmentPending)
                {
                    order.statusMessage =
                        $"공연 후처리 · 청소 {(cleanupPending ? "대기" : "완료")}"
                        + $" · 치료 {(treatmentPending ? "대기" : "완료")}";
                    break;
                }

                order.state = CircusShowState.Completed;
                order.statusMessage = "공연 종료 · 청소와 치료 완료";
                movement.ReleaseOrderActors(order);
                break;
        }
    }

    private void TickPerformance(CircusShowOrder order)
    {
        order.elapsedShowSeconds += clock.DeltaTime;
        if (programs.TryGet(order.programId, out ICircusProgramHandler program)
            && program.Definition.usesCombat)
        {
            order.nextCombatExchangeAt -= clock.DeltaTime;
            if (order.nextCombatExchangeAt <= 0f)
            {
                order.nextCombatExchangeAt = 0.9f;
                ResolveCombatExchange(order);
            }
        }

        if (programs.TryGet(order.programId, out program))
        {
            TryResolvePerformanceAccident(order, program.Definition);
        }

        if (ShouldStopPerformance(order)
            || order.elapsedShowSeconds >= order.showDurationSeconds)
        {
            order.state = CircusShowState.Settlement;
            order.statusMessage = "공연 정산 중";
        }
    }

    private void CheckPerformanceBetrayals(CircusShowOrder order)
    {
        if (order == null || order.betrayalCheckCompleted)
        {
            return;
        }

        order.betrayalCheckCompleted = true;
        List<string> traitors = new List<string>();
        foreach (string captiveId in order.performerIds)
        {
            if (captivityCommands.TryTriggerBetrayal(
                    captiveId,
                    "공연장의 혼란",
                    out _))
            {
                traitors.Add(captiveId);
            }
        }

        if (traitors.Count > 0)
        {
            order.statusMessage =
                $"공연 중 포로 {traitors.Count}명이 복종을 깨고 배신했습니다.";
            order.cleanupRequired = true;
            order.treatmentRequired = true;
        }
    }

    private void Settle(CircusShowOrder order)
    {
        if (!programs.TryGet(order.programId, out ICircusProgramHandler program))
        {
            Cancel(order.orderId, "공연 프로그램이 사라졌습니다.");
            return;
        }

        List<CaptiveState> performers = order.performerIds
            .Select(id => captivity.TryGetCaptive(id, out CaptiveState state) ? state : null)
            .Where(state => state != null)
            .ToList();
        CircusProgramSettlement settlement = program.Settle(order, performers);
        float gamblingSwing = order.venueGamblingVariance <= 0f
            ? 0f
            : Mathf.Lerp(
                -order.venueGamblingVariance,
                order.venueGamblingVariance,
                random.NextFloat());
        order.satisfaction = Mathf.Clamp(
            settlement.Satisfaction
            + order.venueSatisfactionBonus
            + gamblingSwing,
            0f,
            100f);
        order.cleanupRequired = settlement.CleanupRequired;
        order.treatmentRequired |= settlement.TreatmentRequired;
        order.revenue = Mathf.Max(
            0,
            order.audienceIds.Count
            * (order.ticketPrice + order.venueFlatRevenuePerAudience));
        if (order.revenue > 0)
        {
            money.Add(
                order.revenue,
                new EconomyTransactionContext(
                    EconomyTransactionKind.CircusIncome,
                    order.stageId,
                    order.orderId,
                    $"{program.Definition.displayName} 공연 수입"));
        }

        foreach (string captiveId in order.performerIds)
        {
            captivityCommands.RecordPerformance(
                captiveId,
                settlement.Fame,
                1.5f,
                settlement.TreatmentRequired);
        }

        if (order.cleanupRequired)
        {
            float amount = program.Definition.publiclyCruel
                || program.Definition.usesCombat
                    ? 3f
                    : 1.25f;
            amount *= Mathf.Max(0.25f, order.venueFilthMultiplier);
            filth.AddFilth(
                program.Definition.usesCombat
                    ? WorldFilthType.Blood
                    : WorldFilthType.Stain,
                order.stagePosition,
                amount,
                order.orderId,
                program.Definition.publiclyCruel ? 0.55f : 0.15f);
        }

        foreach (string audienceId in order.audienceIds)
        {
            CharacterActor audience = FindActor(world, audienceId);
            if (audience == null)
            {
                continue;
            }

            float mood = Mathf.Clamp(
                (order.satisfaction - 50f) * 0.12f,
                -8f,
                6f);
            if (program.Definition.publiclyCruel)
            {
                CharacterAiPersonality personality =
                    audience.Identity?.Data?.aiPersonality;
                float riskTolerance = personality?.GetRiskTolerance01() ?? 0.4f;
                mood += Mathf.Lerp(-5f, 2.5f, riskTolerance);
            }
            audience.ApplyMoodFactor(
                $"circus:{order.orderId}",
                mood >= 0f
                    ? "인상적인 공연을 관람함"
                    : "불쾌한 공연을 목격함",
                mood,
                240f,
                1);
        }

        if (program.Definition.publiclyCruel)
        {
            ApplyCruelWitnessConsequences(order);
            externalInfluence?.AddDread(
                Mathf.Max(3f, settlement.Fame),
                order.orderId);
            externalInfluence?.AddHostileRumor(
                Mathf.Max(1f, settlement.Fame * 0.35f),
                order.orderId);
        }
        else
        {
            externalInfluence?.AddRenown(
                Mathf.Max(1f, settlement.Fame),
                order.orderId);
        }

        order.statusMessage =
            $"{settlement.Message} 수익 {order.revenue}, 만족도 {order.satisfaction:0}";
        order.state = CircusShowState.CleanupAndTreatment;
    }

    private void TryResolvePerformanceAccident(
        CircusShowOrder order,
        CircusProgramModule program)
    {
        if (order.accidentResolved
            || order.elapsedShowSeconds
                < Mathf.Max(1f, order.showDurationSeconds * 0.25f))
        {
            return;
        }

        order.accidentResolved = true;
        float chance = Mathf.Clamp01(
            program.baseAccidentRisk + order.venueAccidentRiskBonus);
        if (chance <= 0f || random.NextFloat() >= chance)
        {
            return;
        }

        List<CircusCombatant> performers = BuildCombatants(order)
            .Where(item => item.IsAlive)
            .ToList();
        if (performers.Count == 0)
        {
            return;
        }

        CircusCombatant victim = performers[random.NextInt(0, performers.Count)];
        float damage = 8f * Mathf.Clamp(
            order.venueAccidentDamageMultiplier,
            0.25f,
            1f);
        CharacterActor victimCharacter = victim.GetRuntime<CharacterActor>();
        WildlifeActor victimWildlife = victim.GetRuntime<WildlifeActor>();
        if (victimCharacter != null)
        {
            victimCharacter.ApplyBodyDamage(damage, "서커스 공연 사고");
        }
        else
        {
            victimWildlife?.ApplyDamage(Mathf.CeilToInt(damage), null);
        }

        order.treatmentRequired = true;
        order.cleanupRequired = true;
        order.statusMessage = "공연 장치 사고가 발생했습니다.";
    }

    private void ApplyCruelWitnessConsequences(CircusShowOrder order)
    {
        if (!gridProvider.TryGetGrid(out Grid grid)
            || !rooms.TryGetRoom(grid, order.stagePosition, out RoomInstance room))
        {
            return;
        }

        HashSet<string> participants = new HashSet<string>(
            (order.performerIds ?? new List<string>())
            .Concat(order.audienceIds ?? new List<string>()),
            StringComparer.Ordinal);
        foreach (CharacterActor actor in world.Characters)
        {
            string actorId = GetCharacterId(actor);
            if (actor == null
                || actor.IsDead
                || participants.Contains(actorId)
                || !CharacterWorkRoleUtility.IsWorker(actor)
                || !room.ContainsCell(actor.GetNowXY()))
            {
                continue;
            }

            CharacterAiPersonality personality = actor.Identity?.Data?.aiPersonality;
            float riskTolerance = personality?.GetRiskTolerance01() ?? 0.4f;
            float penalty = -Mathf.Max(
                1f,
                order.venueWitnessMoodPenalty * Mathf.Lerp(1.25f, 0.45f, riskTolerance));
            actor.ApplyMoodFactor(
                $"circus-witness:{order.orderId}",
                "잔혹한 공개 공연을 목격함",
                penalty,
                360f,
                1);
        }
    }

    private void ResolveCombatExchange(CircusShowOrder order)
    {
        List<CircusCombatant> fighters = BuildCombatants(order)
            .Where(combatant => combatant.IsAlive)
            .ToList();
        if (fighters.Count < 2)
        {
            return;
        }

        CircusCombatant attacker = fighters[random.NextInt(0, fighters.Count)];
        CircusCombatant defender = fighters.First(candidate =>
            !candidate.Equals(attacker));
        CharacterActor attackerCharacter = attacker.GetRuntime<CharacterActor>();
        WildlifeActor attackerWildlife = attacker.GetRuntime<WildlifeActor>();
        CharacterActor defenderCharacter = defender.GetRuntime<CharacterActor>();
        WildlifeActor defenderWildlife = defender.GetRuntime<WildlifeActor>();
        CharacterBodyHealthSnapshot attackerBody = attackerCharacter != null
            ? bodyHealthQuery.GetSnapshot(attackerCharacter)
            : default;
        CharacterBodyHealthSnapshot defenderBody = defenderCharacter != null
            ? bodyHealthQuery.GetSnapshot(defenderCharacter)
            : default;
        CombatWeaponSnapshot weapon = attackerCharacter != null
            && equipment.TryGetActiveWeapon(
                attacker.Id,
                out CombatWeaponSnapshot active)
                    ? active
                    : attackerWildlife != null
                        ? CreateWildlifeNaturalWeapon(attackerWildlife)
                        : CombatWeaponSnapshot.CreateUnarmed();
        CombatAttackResult result = combat.Resolve(new CombatAttackRequest(
            $"{order.orderId}:{clock.FrameCount}",
            attacker.Id,
            defender.Id,
            attackerCharacter != null
                ? CombatRuntimeStatFactory.Create(attackerCharacter, attackerBody)
                : CombatRuntimeStatFactory.Create(attackerWildlife),
            defenderCharacter != null
                ? CombatRuntimeStatFactory.Create(defenderCharacter, defenderBody)
                : CombatRuntimeStatFactory.Create(defenderWildlife),
            weapon,
            1,
            CombatFireMode.Aimed,
            default,
            defenderDowned: defenderCharacter != null && defenderBody.Downed,
            defenderMeleeLocked: true,
            attackerSuppression: attackerCharacter != null
                ? attackerBody.Suppression
                : 0f,
            defenderSuppression: defenderCharacter != null
                ? defenderBody.Suppression
                : 0f,
            attackPowerMultiplier: attackerCharacter != null
                ? attackerCharacter.GetCombatPowerMultiplier()
                : 1f,
            defenderArmor: defenderCharacter != null
                ? equipment.GetArmor(defender.Id)
                : null,
            defenderShield: defenderCharacter != null
                ? equipment.GetShield(defender.Id)
                : default));
        if (defenderCharacter != null)
        {
            bodyHealthCommands.ApplyCombatResult(
                defenderCharacter,
                result,
                "서커스 교전");
        }
        else if (result.Hit)
        {
            defenderWildlife?.ApplyCombatDamage(
                result,
                attackerCharacter);
        }
    }

    private bool ShouldStopPerformance(CircusShowOrder order)
    {
        List<CircusCombatant> performers = BuildCombatants(order).ToList();
        if (order.lethality == CircusLethalityPolicy.ExecuteDesignatedTarget)
        {
            CircusCombatant designated = performers.FirstOrDefault();
            CharacterActor designatedCharacter = designated.GetRuntime<CharacterActor>();
            WildlifeActor designatedWildlife = designated.GetRuntime<WildlifeActor>();
            if (designatedCharacter != null && !designatedCharacter.IsDead)
            {
                designatedCharacter.Die(
                    CharacterDeathCauseCode.Execution,
                    "captivity:designated-execution");
            }
            else if (designatedWildlife != null && designatedWildlife.IsAlive)
            {
                designatedWildlife.ApplyDamage(
                    designatedWildlife.CurrentHealth,
                    null);
            }
            return true;
        }

        if (order.lethality == CircusLethalityPolicy.StopWhenDowned)
        {
            return performers.Any(IsCombatantDowned);
        }

        if (order.lethality == CircusLethalityPolicy.FightToDeath)
        {
            return performers.Count(combatant => combatant.IsAlive) <= 1;
        }

        if (order.lethality == CircusLethalityPolicy.AllowAccidents)
        {
            return performers.Count(combatant => combatant.IsAlive) <= 1;
        }

        return false;
    }

    private IEnumerable<CircusCombatant> BuildCombatants(CircusShowOrder order)
    {
        foreach (string captiveId in order.performerIds)
        {
            if (captivity.TryGetActor(captiveId, out CharacterActor actor))
            {
                yield return new CircusCombatant(
                    new CircusCombatantIdentity(
                        CircusCombatantKind.Character,
                        actor?.Identity?.PersistentId),
                    actor,
                    () => actor != null && !actor.IsDead);
            }
        }

        foreach (string wildlifeId in order.wildlifeIds)
        {
            WildlifeActor wildlife = FindWildlife(world, wildlifeId);
            if (wildlife != null)
            {
                yield return new CircusCombatant(
                    new CircusCombatantIdentity(
                        CircusCombatantKind.Wildlife,
                        GetWildlifeCombatantId(wildlife.WildlifeId)),
                    wildlife,
                    () => wildlife != null && wildlife.IsAlive);
            }
        }
    }

    private bool IsCombatantDowned(CircusCombatant combatant)
    {
        if (!combatant.IsAlive)
        {
            return true;
        }

        CharacterActor character = combatant.GetRuntime<CharacterActor>();
        if (character != null)
        {
            return bodyHealthQuery.GetSnapshot(character).Downed;
        }

        WildlifeActor wildlife = combatant.GetRuntime<WildlifeActor>();
        return wildlife != null
            && wildlife.CurrentHealth <= wildlife.MaxHealth * 0.2f;
    }

    private static CombatWeaponSnapshot CreateWildlifeNaturalWeapon(
        WildlifeActor actor)
    {
        float damage = Mathf.Max(2f, actor?.RetaliationDamage ?? 2);
        return new CombatWeaponSnapshot(
            "combat:circus-wildlife",
            string.Empty,
            CombatEquipmentKind.MeleeWeapon,
            new MeleeStrikeVerb
            {
                attackTime = 1.05f,
                baseDamage = damage,
                penetration = damage * 0.2f,
                damageType = CombatDamageType.Pierce,
                tracking = 0.08f
            },
            new[]
            {
                new CombatRangeProfile
                {
                    band = CombatRangeBand.Contact,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                }
            },
            1,
            CombatEquipmentQuality.Normal,
            string.Empty,
            0,
            0,
            0f,
            false,
            false,
            false);
    }

    private bool TryGetValidCircusRoom(
        BuildableObject stage,
        out RoomInstance room,
        out string failureReason)
    {
        room = null;
        failureReason = string.Empty;
        if (!rooms.TryGetRoom(stage, out room) || !room.IsUsable)
        {
            failureReason = "무대가 닫힌 정식 방 안에 있어야 합니다.";
            return false;
        }

        if (!room.Furniture.Any(item =>
                item?.BuildingData.GetAudienceSeatingAbility()?.IsValid == true))
        {
            failureReason = "서커스장에 관람석이 필요합니다.";
            return false;
        }

        if (!room.Doors.OfType<Door>().Any())
        {
            failureReason = "서커스장에 공연자 출입문이 필요합니다.";
            return false;
        }

        return true;
    }

    private IEnumerable<CharacterActor> SelectAudience(RoomInstance room)
    {
        int capacity = room.Furniture.Sum(item =>
            item?.BuildingData.GetAudienceSeatingAbility()?.capacity ?? 0);
        return world.Characters
            .Where(actor =>
                actor != null
                && !actor.IsDead
                && actor.characterType == CharacterType.Customer)
            .OrderBy(actor => Manhattan(
                actor.GetNowXY(),
                new Vector2Int(
                    Mathf.RoundToInt(room.Bounds.center.x),
                    Mathf.RoundToInt(room.Bounds.center.y))))
            .Take(Mathf.Max(0, capacity))
            .ToArray();
    }

    private static CharacterActor FindActor(
        ICharacterAiWorldRegistry world,
        string persistentId) =>
        world.AllCharacters.FirstOrDefault(actor => string.Equals(
            GetCharacterId(actor),
            persistentId?.Trim(),
            StringComparison.Ordinal));

    private static WildlifeActor FindWildlife(
        ICharacterAiWorldRegistry world,
        string wildlifeId) =>
        world.Wildlife.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                actor.WildlifeId,
                wildlifeId?.Trim(),
                StringComparison.Ordinal));

    private static string GetCharacterId(CharacterActor actor) =>
        actor?.Identity?.PersistentId?.Trim() ?? string.Empty;

    private static string GetWildlifeCombatantId(string wildlifeId)
    {
        string normalizedId = wildlifeId?.Trim() ?? string.Empty;
        if (normalizedId.Length == 0)
        {
            throw new InvalidOperationException(
                "A circus wildlife combatant requires a stable wildlife ID.");
        }

        return $"wildlife:{normalizedId}";
    }

    private static string GetBuildingId(BuildableObject building, string prefix) =>
        building == null
            ? string.Empty
            : $"{prefix}:{building.RequirePersistentInstanceId().Value}";
}
