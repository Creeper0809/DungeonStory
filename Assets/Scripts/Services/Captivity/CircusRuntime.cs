using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class CircusRuntime :
    ICircusRuntime,
    ITickable,
    IStartable,
    IDisposable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CircusRuntime.Tick");

    private readonly CircusProgramRegistry programs;
    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCommandService captivityCommands;
    private readonly IWildlifeCaptureRuntime wildlifeCapture;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IGridSystemProvider gridProvider;
    private readonly IRoomLayoutCache rooms;
    private readonly IGameMoneyRuntime money;
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly ICombatResolutionService combat;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ICharacterMedicalRuntime medical;
    private readonly IWorldFilthQuery filth;
    private readonly IDoorAccessCommandService doorAccess;
    private readonly IGameClock clock;
    private readonly IRandomStream random;
    private readonly IGameEventBus events;
    private readonly IExternalInfluenceRuntime externalInfluence;
    private readonly List<CircusShowOrder> orders = new List<CircusShowOrder>();
    private readonly Dictionary<string, IDisposable> accessPasses =
        new Dictionary<string, IDisposable>(StringComparer.Ordinal);
    private readonly Dictionary<string, Vector2Int> wildlifeReturnTargets =
        new Dictionary<string, Vector2Int>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> wildlifeReturnOrders =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly List<string> wildlifeReturnTickIds =
        new List<string>();
    private int nextOrderSequence;
    private IDisposable invasionSubscription;

    public CircusRuntime(
        CircusProgramRegistry programs,
        ICaptivityRuntime captivity,
        ICaptivityCommandService captivityCommands,
        IWildlifeCaptureRuntime wildlifeCapture,
        ICharacterAiWorldRegistry world,
        IGridSystemProvider gridProvider,
        IRoomLayoutCache rooms,
        IGameMoneyRuntime money,
        ICharacterBodyHealthRuntime bodyHealth,
        ICombatResolutionService combat,
        ICombatEquipmentRuntime equipment,
        ICharacterMedicalRuntime medical,
        IWorldFilthQuery filth,
        IDoorAccessCommandService doorAccess,
        IGameClock clock,
        IRandomStreamProvider randomStreamProvider,
        IGameEventBus events,
        IExternalInfluenceRuntime externalInfluence = null)
    {
        this.programs = programs ?? throw new ArgumentNullException(nameof(programs));
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.captivityCommands = captivityCommands
            ?? throw new ArgumentNullException(nameof(captivityCommands));
        this.wildlifeCapture = wildlifeCapture
            ?? throw new ArgumentNullException(nameof(wildlifeCapture));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.medical = medical ?? throw new ArgumentNullException(nameof(medical));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
        this.doorAccess = doorAccess ?? throw new ArgumentNullException(nameof(doorAccess));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        random = (randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("circus.runtime");
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.externalInfluence = externalInfluence;
    }

    public IReadOnlyList<CircusProgramModule> Programs => programs.Definitions;
    public IReadOnlyList<CircusShowOrder> Orders =>
        orders.Select(order => order.Clone()).ToArray();

    public CircusProgramForecast GetForecast(
        BuildableObject stage,
        string programId,
        CircusLethalityPolicy lethality,
        IReadOnlyList<string> performerIds,
        IReadOnlyList<string> wildlifeIds)
    {
        BuildingCircusStageAbility stageAbility =
            stage?.BuildingData.GetCircusStageAbility();
        if (stage == null || stageAbility == null || !stageAbility.IsValid)
        {
            return UnavailableForecast("유효한 서커스 무대가 아닙니다.");
        }

        if (!programs.TryGet(programId, out ICircusProgramHandler handler))
        {
            return UnavailableForecast("공연 프로그램을 찾을 수 없습니다.");
        }

        if (!TryGetValidCircusRoom(
                stage,
                out RoomInstance room,
                out string roomFailure))
        {
            return UnavailableForecast(roomFailure);
        }

        CircusProgramModule definition = handler.Definition;
        CircusVenueModifiers venue = EvaluateVenue(
            room,
            definition.publiclyCruel);
        List<CaptiveState> performers = (performerIds ?? Array.Empty<string>())
            .Select(id => captivity.TryGetCaptive(id, out CaptiveState captive)
                ? captive
                : null)
            .Where(captive => captive != null && captive.IsActive)
            .Take(stageAbility.performerCapacity)
            .ToList();
        List<string> animals = (wildlifeIds ?? Array.Empty<string>())
            .Where(wildlifeCapture.IsCaptured)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        CircusShowOrder candidate = new CircusShowOrder
        {
            programId = definition.programId,
            lethality = lethality,
            performerIds = performers.Select(item => item.captiveId).ToList(),
            wildlifeIds = animals,
            venueSatisfactionBonus = venue.SatisfactionBonus,
            venueAccidentRiskBonus = venue.AccidentRiskBonus,
            venueGamblingVariance = venue.GamblingVariance
        };
        bool valid = handler.Validate(
            candidate,
            performers,
            out string failureReason);
        int audienceCount = SelectAudience(room).Count();
        int ticketPrice = Mathf.Max(
            1,
            Mathf.RoundToInt(
                stageAbility.baseTicketPrice * venue.RevenueMultiplier));
        float skill = performers
            .Select(item => item.performerSkill)
            .DefaultIfEmpty(0f)
            .Average();
        float centerSatisfaction = Mathf.Clamp(
            definition.baseAudienceSatisfaction
            + skill * 0.12f
            + venue.SatisfactionBonus,
            0f,
            100f);
        float satisfactionVariance = Mathf.Max(0f, venue.GamblingVariance);
        float accidentChance = Mathf.Clamp01(
            definition.baseAccidentRisk + venue.AccidentRiskBonus);
        float injuryChance = definition.usesCombat
            ? Mathf.Max(0.25f, accidentChance)
            : accidentChance;
        float deathChance = lethality switch
        {
            CircusLethalityPolicy.FightToDeath => 1f,
            CircusLethalityPolicy.ExecuteDesignatedTarget => 1f,
            CircusLethalityPolicy.AllowAccidents => injuryChance * 0.2f,
            _ => 0f
        };
        float fame = Mathf.Max(1f, definition.basePerformerFame);
        string requirement =
            $"포로 {(definition.requiresCaptive ? "필수" : "선택")}"
            + $" · 야생동물 {(definition.requiresWildlife ? "필수" : "선택")}"
            + $" · 현재 포로 {performers.Count}명/동물 {animals.Count}마리";
        return new CircusProgramForecast(
            audienceCount
            * (ticketPrice + venue.FlatRevenuePerAudience),
            centerSatisfaction - satisfactionVariance,
            centerSatisfaction + satisfactionVariance,
            accidentChance,
            definition.publiclyCruel ? 0f : fame,
            definition.publiclyCruel ? Mathf.Max(3f, fame) : 0f,
            definition.publiclyCruel ? Mathf.Max(1f, fame * 0.35f) : 0f,
            injuryChance,
            deathChance,
            valid,
            requirement,
            valid ? string.Empty : failureReason);
    }

    private static CircusProgramForecast UnavailableForecast(string reason)
    {
        return new CircusProgramForecast(
            0,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            false,
            string.Empty,
            reason);
    }

    public void Start()
    {
        invasionSubscription = events.Subscribe<InvasionStartedEvent>(
            _ => CancelActiveShows("침공이 시작되어 공연을 취소했습니다."));
    }

    public void Dispose()
    {
        invasionSubscription?.Dispose();
        invasionSubscription = null;
        foreach (IDisposable pass in accessPasses.Values)
        {
            pass?.Dispose();
        }
        accessPasses.Clear();
        wildlifeReturnTargets.Clear();
        wildlifeReturnOrders.Clear();
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

        TickWildlifeReturns();
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

        CircusVenueModifiers venue = EvaluateVenue(room, program.Definition.publiclyCruel);
        List<CaptiveState> performers = (performerIds ?? Array.Empty<string>())
            .Select(id => captivity.TryGetCaptive(id, out CaptiveState captive) ? captive : null)
            .Where(captive => captive != null && captive.IsActive)
            .Take(stageAbility.performerCapacity)
            .ToList();
        foreach (CaptiveState performer in performers)
        {
            CharacterActor actor = FindActor(performer.captiveId);
            CharacterBodyHealthSnapshot health = bodyHealth.GetSnapshot(actor);
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
                medical.TryRequestTreatment(actor, out _, out _);
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
                ReleasePerformers(candidate);
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
                ReleasePerformers(candidate);
                return false;
            }
        }

        candidate.audienceIds = SelectAudience(room).Select(GetCharacterId).ToList();
        List<Vector2Int> participantPositions = ChoosePositions(
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
        candidate.audiencePositions = ChooseAudiencePositions(
            room,
            candidate.audienceIds.Count);
        orders.Add(candidate);
        order = candidate.Clone();
        return true;
    }

    public bool AdvancePreparation(
        string orderId,
        CharacterActor worker,
        float workAmount,
        out string status)
    {
        CircusShowOrder order = FindOrder(orderId);
        if (order == null || order.state != CircusShowState.Composition)
        {
            status = "준비 중인 공연이 없습니다.";
            return false;
        }

        order.preparationWorkCompleted = Mathf.Min(
            order.preparationWorkRequired,
            order.preparationWorkCompleted + Mathf.Max(0f, workAmount));
        float progress = order.preparationWorkCompleted
            / Mathf.Max(0.01f, order.preparationWorkRequired);
        status = $"공연 준비 {progress:P0}";
        order.statusMessage = status;
        if (progress >= 0.999f)
        {
            order.state = CircusShowState.ParticipantEscort;
            order.phaseElapsedSeconds = 0f;
            order.statusMessage = "참가자 호송 중";
            StartParticipantMovement(order);
        }

        return true;
    }

    public bool Cancel(string orderId, string reason)
    {
        CircusShowOrder order = FindOrder(orderId);
        if (order == null || order.IsTerminal)
        {
            return false;
        }

        order.state = CircusShowState.Cancelled;
        order.statusMessage = string.IsNullOrWhiteSpace(reason)
            ? "공연 취소"
            : reason;
        ReleaseOrderActors(order);
        return true;
    }

    public CircusSaveData Capture()
    {
        return new CircusSaveData
        {
            version = CircusSaveData.CurrentVersion,
            nextOrderSequence = nextOrderSequence,
            orders = orders.Select(order => order.Clone()).ToList(),
            capturedWildlife = wildlifeCapture.Capture()
                .Select(item => item.Clone())
                .ToList()
        };
    }

    public void Restore(CircusSaveData saveData, IList<string> warnings)
    {
        foreach (CircusShowOrder order in orders)
        {
            ReleaseOrderActors(order);
        }
        orders.Clear();
        wildlifeCapture.Restore(saveData?.capturedWildlife, warnings);
        nextOrderSequence = Mathf.Max(0, saveData?.nextOrderSequence ?? 0);
        foreach (CircusShowOrder source in saveData?.orders ?? new List<CircusShowOrder>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.orderId)
                || orders.Any(item => string.Equals(
                    item.orderId,
                    source.orderId,
                    StringComparison.Ordinal)))
            {
                warnings?.Add("유효하지 않거나 중복된 공연 주문을 건너뛰었습니다.");
                continue;
            }

            CircusShowOrder restored = source.Clone();
            if (!programs.TryGet(restored.programId, out _))
            {
                restored.state = CircusShowState.Cancelled;
                restored.statusMessage = "공연 프로그램이 없어 취소됨";
                warnings?.Add($"{restored.orderId}: 공연 프로그램이 없어 취소했습니다.");
            }
            else if (!restored.IsTerminal)
            {
                restored.state = CircusShowState.Composition;
                restored.preparationWorkCompleted = Mathf.Min(
                    restored.preparationWorkCompleted,
                    restored.preparationWorkRequired);
                restored.statusMessage = "불러온 공연 준비 재개";
            }

            orders.Add(restored);
        }
    }

    private void TickOrder(CircusShowOrder order)
    {
        switch (order.state)
        {
            case CircusShowState.ParticipantEscort:
                order.phaseElapsedSeconds += clock.DeltaTime;
                if (AreParticipantsAt(order))
                {
                    order.state = CircusShowState.AudienceEntering;
                    order.phaseElapsedSeconds = 0f;
                    order.statusMessage = "관객 입장 중";
                    StartAudienceMovement(order);
                }
                else if (order.phaseElapsedSeconds >= 30f)
                {
                    Cancel(order.orderId, "참가자 호송 경로가 막혀 공연을 취소했습니다.");
                }
                break;
            case CircusShowState.AudienceEntering:
                order.phaseElapsedSeconds += clock.DeltaTime;
                if (AreActorsAt(order.audienceIds, order.audiencePositions))
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
                    && medical.ActiveOrders.Any(item =>
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
                ReleaseOrderActors(order);
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
            CharacterActor audience = FindActor(audienceId);
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
        if (victim.Character != null)
        {
            victim.Character.ApplyBodyDamage(damage, "서커스 공연 사고");
        }
        else
        {
            victim.Wildlife?.ApplyDamage(Mathf.CeilToInt(damage), null);
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

    private static CircusVenueModifiers EvaluateVenue(
        RoomInstance room,
        bool publiclyCruel)
    {
        CircusVenueModifiers result = CircusVenueModifiers.Default;
        foreach (BuildableObject part in room?.Furniture
                     ?? Array.Empty<BuildableObject>())
        {
            BuildingSO data = part?.BuildingData;
            BuildingCircusTicketBoothAbility ticket =
                data.GetCircusTicketBoothAbility();
            if (ticket != null)
            {
                result.RevenueMultiplier *= Mathf.Max(1f, ticket.revenueMultiplier);
                result.FlatRevenuePerAudience += Mathf.Max(
                    0,
                    ticket.flatRevenuePerAudience);
            }

            BuildingCircusGamblingAbility gambling =
                data.GetCircusGamblingAbility();
            if (gambling != null)
            {
                result.FlatRevenuePerAudience += Mathf.Max(
                    0,
                    gambling.revenuePerAudience);
                result.GamblingVariance += Mathf.Max(
                    0f,
                    gambling.satisfactionVariance);
            }

            BuildingCircusAnnouncerAbility announcer =
                data.GetCircusAnnouncerAbility();
            if (announcer != null)
            {
                result.SatisfactionBonus += Mathf.Max(
                    0f,
                    announcer.satisfactionBonus);
                result.PreparationWorkMultiplier *= Mathf.Clamp(
                    announcer.preparationWorkMultiplier,
                    0.5f,
                    1f);
            }

            BuildingCircusHazardAbility hazard =
                data.GetCircusHazardAbility();
            if (hazard != null)
            {
                result.AccidentRiskBonus += Mathf.Max(
                    0f,
                    hazard.accidentRiskBonus);
                result.SatisfactionBonus += Mathf.Max(
                    0f,
                    hazard.satisfactionBonus);
            }

            BuildingCircusTreatmentZoneAbility treatment =
                data.GetCircusTreatmentZoneAbility();
            if (treatment != null)
            {
                result.AccidentDamageMultiplier *= Mathf.Clamp(
                    treatment.accidentDamageMultiplier,
                    0.25f,
                    1f);
            }

            BuildingPublicPunishmentAbility punishment =
                data.GetPublicPunishmentAbility();
            if (publiclyCruel && punishment != null)
            {
                result.SatisfactionBonus += Mathf.Max(
                    0f,
                    punishment.cruelSatisfactionBonus);
                result.FilthMultiplier *= Mathf.Max(
                    1f,
                    punishment.filthMultiplier);
                result.WitnessMoodPenalty = Mathf.Max(
                    result.WitnessMoodPenalty,
                    punishment.witnessMoodPenalty);
            }
        }

        result.RevenueMultiplier = Mathf.Clamp(result.RevenueMultiplier, 1f, 2.5f);
        result.SatisfactionBonus = Mathf.Clamp(result.SatisfactionBonus, 0f, 35f);
        result.AccidentRiskBonus = Mathf.Clamp(result.AccidentRiskBonus, 0f, 0.5f);
        result.AccidentDamageMultiplier = Mathf.Clamp(
            result.AccidentDamageMultiplier,
            0.25f,
            1f);
        return result;
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
        CharacterBodyHealthSnapshot attackerBody = attacker.Character != null
            ? bodyHealth.GetSnapshot(attacker.Character)
            : default;
        CharacterBodyHealthSnapshot defenderBody = defender.Character != null
            ? bodyHealth.GetSnapshot(defender.Character)
            : default;
        CombatWeaponSnapshot weapon = attacker.Character != null
            && equipment.TryGetActiveWeapon(
                attacker.Id,
                out CombatWeaponSnapshot active)
                    ? active
                    : attacker.Wildlife != null
                        ? CreateWildlifeNaturalWeapon(attacker.Wildlife)
                        : CombatWeaponSnapshot.CreateUnarmed();
        CombatAttackResult result = combat.Resolve(new CombatAttackRequest(
            $"{order.orderId}:{clock.FrameCount}",
            attacker.Id,
            defender.Id,
            attacker.Character != null
                ? CombatRuntimeStatFactory.Create(attacker.Character, attackerBody)
                : CombatRuntimeStatFactory.Create(attacker.Wildlife),
            defender.Character != null
                ? CombatRuntimeStatFactory.Create(defender.Character, defenderBody)
                : CombatRuntimeStatFactory.Create(defender.Wildlife),
            weapon,
            1,
            CombatFireMode.Aimed,
            default,
            defenderDowned: defender.Character != null && defenderBody.Downed,
            defenderMeleeLocked: true,
            attackerSuppression: attacker.Character != null
                ? attackerBody.Suppression
                : 0f,
            defenderSuppression: defender.Character != null
                ? defenderBody.Suppression
                : 0f,
            attackPowerMultiplier: attacker.Character != null
                ? attacker.Character.GetCombatPowerMultiplier()
                : 1f,
            defenderArmor: defender.Character != null
                ? equipment.GetArmor(defender.Id)
                : null,
            defenderShield: defender.Character != null
                ? equipment.GetShield(defender.Id)
                : default));
        if (defender.Character != null)
        {
            bodyHealth.ApplyCombatResult(
                defender.Character,
                result,
                "서커스 교전");
        }
        else if (result.Hit)
        {
            defender.Wildlife?.ApplyCombatDamage(
                result,
                attacker.Character);
        }
    }

    private bool ShouldStopPerformance(CircusShowOrder order)
    {
        List<CircusCombatant> performers = BuildCombatants(order).ToList();
        if (order.lethality == CircusLethalityPolicy.ExecuteDesignatedTarget)
        {
            CircusCombatant designated = performers.FirstOrDefault();
            if (designated.Character != null && !designated.Character.IsDead)
            {
                designated.Character.Die("지정 처형");
            }
            else if (designated.Wildlife != null && designated.Wildlife.IsAlive)
            {
                designated.Wildlife.ApplyDamage(
                    designated.Wildlife.CurrentHealth,
                    null);
            }
            return true;
        }

        if (order.lethality == CircusLethalityPolicy.StopWhenDowned)
        {
            return performers.Any(combatant =>
                !combatant.IsAlive
                || (combatant.Character != null
                    && bodyHealth.GetSnapshot(combatant.Character).Downed)
                || (combatant.Wildlife != null
                    && combatant.Wildlife.CurrentHealth
                        <= combatant.Wildlife.MaxHealth * 0.2f));
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
                yield return new CircusCombatant(actor);
            }
        }

        foreach (string wildlifeId in order.wildlifeIds)
        {
            WildlifeActor wildlife = FindWildlife(wildlifeId);
            if (wildlife != null)
            {
                yield return new CircusCombatant(wildlife);
            }
        }
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

    private List<Vector2Int> ChooseAudiencePositions(RoomInstance room, int count)
    {
        List<Vector2Int> seats = room.Furniture
            .Where(item => item?.BuildingData.GetAudienceSeatingAbility()?.IsValid == true)
            .Select(item => item.centerPos)
            .Distinct()
            .ToList();
        if (seats.Count < count)
        {
            Vector2Int roomCenter = new Vector2Int(
                Mathf.RoundToInt(room.Bounds.center.x),
                Mathf.RoundToInt(room.Bounds.center.y));
            seats.AddRange(ChoosePositions(room, roomCenter, count - seats.Count, false));
        }
        return seats.Take(count).ToList();
    }

    private List<Vector2Int> ChoosePositions(
        RoomInstance room,
        Vector2Int origin,
        int count,
        bool nearFirst)
    {
        if (!gridProvider.TryGetGrid(out Grid grid))
        {
            return new List<Vector2Int>();
        }

        IEnumerable<Vector2Int> candidates = room.Cells
            .Where(cell => grid.IsWalkable(cell))
            .OrderBy(cell => nearFirst
                ? Manhattan(cell, origin)
                : -Manhattan(cell, origin));
        return candidates.Distinct().Take(Mathf.Max(0, count)).ToList();
    }

    private void StartParticipantMovement(CircusShowOrder order)
    {
        for (int index = 0;
             index < order.performerIds.Count && index < order.performerPositions.Count;
             index++)
        {
            string captiveId = order.performerIds[index];
            if (!captivity.TryGetActor(captiveId, out CharacterActor actor))
            {
                continue;
            }

            DoorAccessSubjectRef subject = new DoorAccessSubjectRef(
                captiveId,
                DoorAccessGroup.Captive,
                character: actor);
            accessPasses[captiveId] = doorAccess.BeginTemporaryOverride(
                subject,
                DoorAccessOverrideKind.EscortPass,
                order.orderId);
            actor.SetAiPaused(true);
            actor.SetLifecycleState(CharacterLifecycleState.Active);
            actor.GetAbility<AbilityMove>()?.TryStartSystemMove(
                order.performerPositions[index],
                DoorAccessOverrideKind.EscortPass,
                out _);
        }

        for (int index = 0;
             index < order.wildlifeIds.Count && index < order.wildlifePositions.Count;
             index++)
        {
            string wildlifeId = order.wildlifeIds[index];
            WildlifeActor wildlife = FindWildlife(wildlifeId);
            if (wildlife == null)
            {
                continue;
            }

            string passKey = WildlifePassKey(wildlifeId);
            accessPasses[passKey] = doorAccess.BeginTemporaryOverride(
                new DoorAccessSubjectRef(
                    wildlifeId,
                    DoorAccessGroup.CaptiveWildlife,
                    wildlife: wildlife),
                DoorAccessOverrideKind.EscortPass,
                order.orderId);
            wildlife.TrySetManagedCaptivePath(
                order.wildlifePositions[index],
                clock.Time);
        }
    }

    private void StartAudienceMovement(CircusShowOrder order)
    {
        for (int index = 0;
             index < order.audienceIds.Count && index < order.audiencePositions.Count;
             index++)
        {
            CharacterActor actor = FindActor(order.audienceIds[index]);
            if (actor == null)
            {
                continue;
            }

            actor.SetAiPaused(true);
            actor.GetAbility<AbilityMove>()?.TryStartSystemMove(
                order.audiencePositions[index],
                DoorAccessOverrideKind.None,
                out _);
        }
    }

    private bool AreActorsAt(
        IReadOnlyList<string> actorIds,
        IReadOnlyList<Vector2Int> targets)
    {
        int checkedCount = Mathf.Min(actorIds?.Count ?? 0, targets?.Count ?? 0);
        if (checkedCount == 0)
        {
            return true;
        }

        for (int index = 0; index < checkedCount; index++)
        {
            CharacterActor actor = FindActor(actorIds[index]);
            if (actor != null && actor.GetNowXY() != targets[index])
            {
                return false;
            }
        }

        return true;
    }

    private bool AreParticipantsAt(CircusShowOrder order)
    {
        if (!AreActorsAt(order.performerIds, order.performerPositions))
        {
            return false;
        }

        int checkedCount = Mathf.Min(
            order.wildlifeIds?.Count ?? 0,
            order.wildlifePositions?.Count ?? 0);
        for (int index = 0; index < checkedCount; index++)
        {
            WildlifeActor actor = FindWildlife(order.wildlifeIds[index]);
            if (actor != null && actor.GridPosition != order.wildlifePositions[index])
            {
                return false;
            }
        }

        return true;
    }

    private void ReleaseOrderActors(CircusShowOrder order)
    {
        foreach (string captiveId in order.performerIds ?? new List<string>())
        {
            if (accessPasses.Remove(captiveId, out IDisposable pass))
            {
                pass?.Dispose();
            }
            captivityCommands.TryAssignPerformer(captiveId, false, out _);
        }

        foreach (string wildlifeId in order.wildlifeIds ?? new List<string>())
        {
            WildlifeActor wildlife = FindWildlife(wildlifeId);
            if (!wildlifeCapture.TryGetCaptured(
                    wildlifeId,
                    out CapturedWildlifeState state))
            {
                ReleaseWildlifePass(wildlifeId);
                continue;
            }

            wildlifeReturnTargets[wildlifeId] = state.penPosition;
            wildlifeReturnOrders[wildlifeId] = order.orderId;
            if (wildlife != null)
            {
                wildlife.TrySetManagedCaptivePath(state.penPosition, clock.Time);
            }
        }

        foreach (string audienceId in order.audienceIds ?? new List<string>())
        {
            CharacterActor audience = FindActor(audienceId);
            audience?.SetAiPaused(false);
        }
    }

    private void TickWildlifeReturns()
    {
        if (wildlifeReturnTargets.Count == 0)
        {
            return;
        }

        wildlifeReturnTickIds.Clear();
        foreach (string wildlifeId in wildlifeReturnTargets.Keys)
        {
            wildlifeReturnTickIds.Add(wildlifeId);
        }

        for (int index = 0; index < wildlifeReturnTickIds.Count; index++)
        {
            string wildlifeId = wildlifeReturnTickIds[index];
            if (!wildlifeReturnTargets.TryGetValue(
                    wildlifeId,
                    out Vector2Int returnTarget))
            {
                continue;
            }

            WildlifeActor wildlife = FindWildlife(wildlifeId);
            if (wildlife == null)
            {
                FinishWildlifeReturn(wildlifeId);
                continue;
            }

            if (wildlife.GridPosition == returnTarget)
            {
                FinishWildlifeReturn(wildlifeId);
                continue;
            }

            if (!wildlife.IsMoving)
            {
                wildlife.TrySetManagedCaptivePath(returnTarget, clock.Time);
            }
        }
    }

    private void FinishWildlifeReturn(string wildlifeId)
    {
        wildlifeReturnOrders.Remove(wildlifeId, out string orderId);
        wildlifeReturnTargets.Remove(wildlifeId);
        ReleaseWildlifePass(wildlifeId);
        wildlifeCapture.CompleteShowAssignment(wildlifeId, orderId);
    }

    private void ReleaseWildlifePass(string wildlifeId)
    {
        string key = WildlifePassKey(wildlifeId);
        if (accessPasses.Remove(key, out IDisposable pass))
        {
            pass?.Dispose();
        }
    }

    private void ReleasePerformers(CircusShowOrder order)
    {
        foreach (string captiveId in order.performerIds)
        {
            captivityCommands.TryAssignPerformer(captiveId, false, out _);
        }
    }

    private void CancelActiveShows(string reason)
    {
        foreach (CircusShowOrder order in orders.Where(item => !item.IsTerminal).ToArray())
        {
            Cancel(order.orderId, reason);
        }
    }

    private CircusShowOrder FindOrder(string orderId)
    {
        return orders.FirstOrDefault(item => string.Equals(
            item.orderId,
            orderId?.Trim(),
            StringComparison.Ordinal));
    }

    private CharacterActor FindActor(string persistentId)
    {
        return world.AllCharacters.FirstOrDefault(actor => string.Equals(
            GetCharacterId(actor),
            persistentId?.Trim(),
            StringComparison.Ordinal));
    }

    private WildlifeActor FindWildlife(string wildlifeId)
    {
        return world.Wildlife.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                actor.WildlifeId,
                wildlifeId?.Trim(),
                StringComparison.Ordinal));
    }

    private static string WildlifePassKey(string wildlifeId)
    {
        return $"wildlife:{wildlifeId?.Trim() ?? string.Empty}";
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
    }

    private static string GetBuildingId(BuildableObject building, string prefix)
    {
        return building == null
            ? string.Empty
            : $"{prefix}:{building.id}:{building.centerPos.x}:{building.centerPos.y}";
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

    private struct CircusVenueModifiers
    {
        public float RevenueMultiplier;
        public int FlatRevenuePerAudience;
        public float SatisfactionBonus;
        public float GamblingVariance;
        public float PreparationWorkMultiplier;
        public float AccidentRiskBonus;
        public float AccidentDamageMultiplier;
        public float FilthMultiplier;
        public float WitnessMoodPenalty;

        public static CircusVenueModifiers Default => new CircusVenueModifiers
        {
            RevenueMultiplier = 1f,
            PreparationWorkMultiplier = 1f,
            AccidentDamageMultiplier = 1f,
            FilthMultiplier = 1f,
            WitnessMoodPenalty = 3f
        };
    }

    private readonly struct CircusCombatant : IEquatable<CircusCombatant>
    {
        public CircusCombatant(CharacterActor character)
        {
            Character = character;
            Wildlife = null;
        }

        public CircusCombatant(WildlifeActor wildlife)
        {
            Character = null;
            Wildlife = wildlife;
        }

        public CharacterActor Character { get; }
        public WildlifeActor Wildlife { get; }
        public string Id => Character != null
            ? GetCharacterId(Character)
            : $"wildlife:{Wildlife?.WildlifeId ?? string.Empty}";
        public bool IsAlive => Character != null
            ? !Character.IsDead
            : Wildlife != null && Wildlife.IsAlive;

        public bool Equals(CircusCombatant other)
        {
            return ReferenceEquals(Character, other.Character)
                && ReferenceEquals(Wildlife, other.Wildlife);
        }

        public override bool Equals(object obj)
        {
            return obj is CircusCombatant other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Character != null ? Character.GetInstanceID() : 0) * 397)
                    ^ (Wildlife != null ? Wildlife.GetInstanceID() : 0);
            }
        }
    }
}
