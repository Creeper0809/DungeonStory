using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum ExteriorIncidentStage
{
    Preparing = 0,
    Active = 1,
    Interacting = 2,
    Resolved = 3,
    Failed = 4,
    TimedOut = 5
}

public enum ExteriorIncidentOutcome
{
    None = 0,
    TradeAvailable = 1,
    IntelligenceAcquired = 2,
    TheftPrevented = 3,
    ItemStolen = 4,
    RescueOrdered = 5,
    VisitorLost = 6,
    TradePurchased = 7,
    PredatorApproached = 8,
    CargoSecured = 9,
    CargoDamaged = 10
}

[Serializable]
public sealed class ExteriorIncidentRuntimeState
{
    public string incidentId = string.Empty;
    public ExteriorIncidentKind kind;
    public string zoneId = string.Empty;
    public string text = string.Empty;
    public ExteriorIncidentStage stage = ExteriorIncidentStage.Preparing;
    public ExteriorIncidentOutcome outcome;
    public float durationSeconds;
    public float remainingSeconds;
    public float progress;
    public bool receptionApplied;
    public List<string> actorIds = new List<string>();
    public List<string> wildlifeIds = new List<string>();
    public List<string> itemStackIds = new List<string>();
    public string stolenItemId = string.Empty;
    public int stolenItemQuantity;
    public int offerPrice;

    public bool IsTerminal => stage is ExteriorIncidentStage.Resolved
        or ExteriorIncidentStage.Failed
        or ExteriorIncidentStage.TimedOut;

    public ExteriorIncidentRuntimeState Clone()
    {
        return new ExteriorIncidentRuntimeState
        {
            incidentId = incidentId,
            kind = kind,
            zoneId = zoneId,
            text = text,
            stage = stage,
            outcome = outcome,
            durationSeconds = durationSeconds,
            remainingSeconds = remainingSeconds,
            progress = progress,
            receptionApplied = receptionApplied,
            actorIds = new List<string>(actorIds ?? new List<string>()),
            wildlifeIds = new List<string>(wildlifeIds ?? new List<string>()),
            itemStackIds = new List<string>(itemStackIds ?? new List<string>()),
            stolenItemId = stolenItemId,
            stolenItemQuantity = stolenItemQuantity,
            offerPrice = offerPrice
        };
    }
}

public interface IExteriorIncidentHandler
{
    ExteriorIncidentKind Kind { get; }
    string DefaultText { get; }
    float DurationSeconds { get; }
    bool TryBegin(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string failureReason);
    void Tick(ExteriorIncidentRuntimeState state, ExteriorZoneMarker zone, float deltaTime);
    void Restore(ExteriorIncidentRuntimeState state, ExteriorZoneMarker zone);
    bool TryExecutePrimaryAction(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string message);
}

public sealed class ExteriorIncidentHandlerRegistry
{
    private readonly Dictionary<ExteriorIncidentKind, IExteriorIncidentHandler> handlers =
        new Dictionary<ExteriorIncidentKind, IExteriorIncidentHandler>();

    public ExteriorIncidentHandlerRegistry(IEnumerable<IExteriorIncidentHandler> handlers)
    {
        foreach (IExteriorIncidentHandler handler in handlers
                     ?? Array.Empty<IExteriorIncidentHandler>())
        {
            if (handler == null || handler.Kind == ExteriorIncidentKind.None)
            {
                continue;
            }

            if (!this.handlers.TryAdd(handler.Kind, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate exterior incident handler '{handler.Kind}'.");
            }
        }
    }

    public IReadOnlyCollection<IExteriorIncidentHandler> All => handlers.Values;

    public bool TryGet(
        ExteriorIncidentKind kind,
        out IExteriorIncidentHandler handler)
    {
        return handlers.TryGetValue(kind, out handler);
    }
}

public interface IExteriorIncidentActorService
{
    bool TrySpawn(
        ExteriorIncidentKind kind,
        string actorId,
        Vector2Int position,
        bool downed,
        out CharacterActor actor,
        out string failureReason);
    bool TryFind(string actorId, out CharacterActor actor);
    bool HasEnteredDungeon(string actorId);
    void Despawn(string actorId);
}

public readonly struct ExteriorVisitorReceptionAppliedEvent
{
    public ExteriorVisitorReceptionAppliedEvent(
        string incidentId,
        string visitorId,
        float readiness)
    {
        IncidentId = incidentId ?? string.Empty;
        VisitorId = visitorId ?? string.Empty;
        Readiness = Mathf.Clamp(readiness, 0f, 100f);
    }

    public string IncidentId { get; }
    public string VisitorId { get; }
    public float Readiness { get; }
}

public sealed class ExteriorIncidentActorService : IExteriorIncidentActorService
{
    private readonly IGridSystemProvider gridProvider;
    private readonly ICharacterSpawnerProvider spawnerProvider;
    private readonly ICharacterSpawnObjectFactory objectFactory;
    private readonly IInvasionIntruderDataProvider characterDataProvider;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly ICharacterMedicalRuntime medicalRuntime;

    public ExteriorIncidentActorService(
        IGridSystemProvider gridProvider,
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterSpawnObjectFactory objectFactory,
        IInvasionIntruderDataProvider characterDataProvider,
        ICharacterAiWorldRegistry worldRegistry,
        ICharacterBodyHealthRuntime bodyHealth,
        ICharacterMedicalRuntime medicalRuntime)
    {
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.spawnerProvider = spawnerProvider ?? throw new ArgumentNullException(nameof(spawnerProvider));
        this.objectFactory = objectFactory ?? throw new ArgumentNullException(nameof(objectFactory));
        this.characterDataProvider = characterDataProvider
            ?? throw new ArgumentNullException(nameof(characterDataProvider));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.medicalRuntime = medicalRuntime ?? throw new ArgumentNullException(nameof(medicalRuntime));
    }

    public bool TrySpawn(
        ExteriorIncidentKind kind,
        string actorId,
        Vector2Int position,
        bool downed,
        out CharacterActor actor,
        out string failureReason)
    {
        actor = null;
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(actorId)
            || !gridProvider.TryGetGrid(out Grid grid)
            || grid == null
            || !spawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            || spawner == null
            || spawner.characterPrefab == null)
        {
            failureReason = "사건 인물을 생성할 준비가 되지 않았습니다.";
            return false;
        }

        CharacterSO data = characterDataProvider.GetRequiredIntruderData(null);
        if (data == null)
        {
            failureReason = "사건 인물 원형이 없습니다.";
            return false;
        }

        GameObject instance = objectFactory.Create(spawner.characterPrefab);
        objectFactory.Inject(instance);
        actor = instance.GetComponent<CharacterActor>();
        if (actor == null)
        {
            objectFactory.Destroy(instance);
            failureReason = "사건 인물 프리팹에 CharacterActor가 없습니다.";
            return false;
        }

        instance.name = GetDisplayName(kind);
        instance.transform.position = grid.GetWorldPos(position);
        actor.Initialize(data);
        CharacterType type = kind == ExteriorIncidentKind.Thief
            ? CharacterType.Customer
            : kind == ExteriorIncidentKind.InjuredReturnee
                ? CharacterType.NPC
                : CharacterType.Customer;
        actor.characterType = type;
        actor.Identity?.SetCharacterType(type);
        actor.Identity?.SetPersistentId(actorId);
        worldRegistry.RegisterCharacter(actor);
        worldRegistry.RegisterCharacterLifetime(actor);

        if (downed)
        {
            ApplyInjuredState(actor);
            actor.SetLifecycleState(CharacterLifecycleState.Downed);
            medicalRuntime.NotifyCharacterDowned(actor);
        }

        return true;
    }

    public bool TryFind(string actorId, out CharacterActor actor)
    {
        actor = worldRegistry.Characters.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.Identity?.PersistentId,
                actorId,
                StringComparison.Ordinal));
        return actor != null;
    }

    public bool HasEnteredDungeon(string actorId)
    {
        if (!TryFind(actorId, out CharacterActor actor)
            || !gridProvider.TryGetGrid(out Grid grid)
            || grid == null)
        {
            return false;
        }

        GridCell cell = grid.GetGridCell(actor.GetNowXY());
        return cell != null
            && cell.AreaType is GridCellAreaType.Entrance
                or GridCellAreaType.DungeonInterior;
    }

    public void Despawn(string actorId)
    {
        if (!TryFind(actorId, out CharacterActor actor))
        {
            return;
        }

        worldRegistry.UnregisterCharacter(actor);
        worldRegistry.UnregisterCharacterLifetime(actor);
        objectFactory.Destroy(actor.gameObject);
    }

    private void ApplyInjuredState(CharacterActor actor)
    {
        CharacterBodyHealthSnapshot baseline = bodyHealth.GetSnapshot(actor);
        List<CharacterBodyPartHealthState> parts = baseline.Parts
            .Select(part => new CharacterBodyPartHealthState
            {
                bodyPart = part.bodyPart,
                maxHealth = part.maxHealth,
                currentHealth = part.bodyPart switch
                {
                    CombatBodyPart.Torso => part.maxHealth * 0.28f,
                    CombatBodyPart.LeftLeg => part.maxHealth * 0.18f,
                    CombatBodyPart.RightLeg => part.maxHealth * 0.18f,
                    _ => part.maxHealth * 0.62f
                },
                bleedingPerSecond = part.bodyPart == CombatBodyPart.Torso ? 0.08f : 0f
            })
            .ToList();
        bodyHealth.ApplySnapshot(
            actor,
            new CharacterBodyHealthSnapshot(
                parts,
                58f,
                0.08f,
                0.25f,
                0.62f,
                0.18f,
                true),
            "부상 귀환");
    }

    private static string GetDisplayName(ExteriorIncidentKind kind)
    {
        return kind switch
        {
            ExteriorIncidentKind.MerchantCart => "외부 상인",
            ExteriorIncidentKind.Informant => "정보상",
            ExteriorIncidentKind.Thief => "수상한 도둑",
            ExteriorIncidentKind.InjuredReturnee => "부상 귀환자",
            _ => "외부 방문자"
        };
    }
}

public abstract class ExteriorIncidentHandlerBase : IExteriorIncidentHandler
{
    protected readonly IExteriorIncidentActorService Actors;
    protected readonly IGameEventBus EventBus;

    protected ExteriorIncidentHandlerBase(
        IExteriorIncidentActorService actors,
        IGameEventBus eventBus)
    {
        Actors = actors ?? throw new ArgumentNullException(nameof(actors));
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public abstract ExteriorIncidentKind Kind { get; }
    public abstract string DefaultText { get; }
    public abstract float DurationSeconds { get; }

    public abstract bool TryBegin(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string failureReason);

    public virtual void Tick(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        float deltaTime)
    {
    }

    public virtual void Restore(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone)
    {
    }

    public virtual bool TryExecutePrimaryAction(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string message)
    {
        message = "이 사건에는 직접 실행할 행동이 없습니다.";
        return false;
    }

    protected bool SpawnVisitor(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        bool downed,
        out CharacterActor actor,
        out string failureReason)
    {
        string actorId = $"{state.incidentId}:actor";
        if (Actors.TryFind(actorId, out actor))
        {
            failureReason = string.Empty;
            return true;
        }

        if (!Actors.TrySpawn(
                Kind,
                actorId,
                zone.centerPos,
                downed,
                out actor,
                out failureReason))
        {
            return false;
        }

        if (!state.actorIds.Contains(actorId))
        {
            state.actorIds.Add(actorId);
        }

        return true;
    }

    protected void DespawnVisitors(ExteriorIncidentRuntimeState state)
    {
        foreach (string actorId in state.actorIds ?? new List<string>())
        {
            Actors.Despawn(actorId);
        }
    }

    protected bool TryApplyReceptionOnEntry(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone)
    {
        if (state == null || state.receptionApplied || state.actorIds.Count == 0)
        {
            return state?.receptionApplied == true;
        }

        string visitorId = state.actorIds[0];
        if (!Actors.HasEnteredDungeon(visitorId))
        {
            return false;
        }

        state.receptionApplied = true;
        state.progress = Mathf.Clamp(
            state.progress + Mathf.Lerp(5f, 25f, zone.ReceptionReadiness / 100f),
            0f,
            100f);
        if (Actors.TryFind(visitorId, out CharacterActor visitor))
        {
            visitor.ApplyMoodFactor(
                $"exterior:reception:{state.incidentId}",
                "입구에서 제대로 응대받음",
                Mathf.Lerp(1f, 4f, zone.ReceptionReadiness / 100f),
                180f,
                1);
        }

        EventBus.Publish(new ExteriorVisitorReceptionAppliedEvent(
            state.incidentId,
            visitorId,
            zone.ReceptionReadiness));
        return true;
    }
}

public sealed class MerchantCartExteriorIncidentHandler : ExteriorIncidentHandlerBase
{
    private readonly IWorldItemStackRuntime items;
    private readonly IGameDataProvider gameDataProvider;

    public MerchantCartExteriorIncidentHandler(
        IExteriorIncidentActorService actors,
        IWorldItemStackRuntime items,
        IGameEventBus eventBus,
        IGameDataProvider gameDataProvider)
        : base(actors, eventBus)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.gameDataProvider = gameDataProvider
            ?? throw new ArgumentNullException(nameof(gameDataProvider));
    }

    public override ExteriorIncidentKind Kind => ExteriorIncidentKind.MerchantCart;
    public override string DefaultText => "상인 마차가 실제 물품을 싣고 입구에 도착했습니다.";
    public override float DurationSeconds => 180f;

    public override bool TryBegin(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string failureReason)
    {
        if (!SpawnVisitor(state, zone, false, out _, out failureReason))
        {
            return false;
        }

        foreach ((StockCategory category, int amount) in new[]
                 {
                     (StockCategory.Food, 4),
                     (StockCategory.Medicine, 2),
                     (StockCategory.General, 3)
                 })
        {
            string itemId = DungeonItemCatalogSO.StockItemId(category);
            items.SpawnItemAt(
                itemId,
                amount,
                zone.centerPos,
                WorldItemStackState.FacilityBuffer,
                state.incidentId,
                out _);
        }

        state.itemStackIds = items.GetStacksAt(zone.centerPos, true)
            .Where(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    state.incidentId,
                    StringComparison.Ordinal))
            .Select(stack => stack.StackId)
            .ToList();
        state.offerPrice = Mathf.Max(
            1,
            Mathf.RoundToInt(items.GetStacksAt(zone.centerPos, true)
                .Where(stack => stack != null
                    && string.Equals(
                        stack.DestinationId,
                        state.incidentId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.UnitPrice * stack.Quantity)
                * 0.8f));
        state.stage = ExteriorIncidentStage.Active;
        state.outcome = ExteriorIncidentOutcome.TradeAvailable;
        EventBus.RaiseAlert(
            "상인 마차 도착",
            "상인이 입구에서 거래를 기다립니다. 응대가 늦으면 물품을 싣고 떠납니다.",
            EventAlertImportance.Medium,
            "외부");
        return true;
    }

    public override void Tick(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        float deltaTime)
    {
        if (state.IsTerminal)
        {
            return;
        }

        if (!TryApplyReceptionOnEntry(state, zone))
        {
            if (state.remainingSeconds <= 0f)
            {
                state.stage = ExteriorIncidentStage.TimedOut;
                items.RemoveStacksByStateAndDestination(
                    WorldItemStackState.FacilityBuffer,
                    state.incidentId);
                DespawnVisitors(state);
            }
            return;
        }

        state.stage = ExteriorIncidentStage.Interacting;
        if (state.remainingSeconds <= 0f)
        {
            state.stage = ExteriorIncidentStage.TimedOut;
            items.RemoveStacksByStateAndDestination(
                WorldItemStackState.FacilityBuffer,
                state.incidentId);
            DespawnVisitors(state);
        }
    }

    public override bool TryExecutePrimaryAction(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string message)
    {
        if (state == null
            || state.IsTerminal
            || state.outcome != ExteriorIncidentOutcome.TradeAvailable)
        {
            message = "거래할 수 있는 상인 마차가 아닙니다.";
            return false;
        }

        if (!state.receptionApplied)
        {
            message = "상인이 입구에 들어와 응대를 받을 때까지 기다려야 합니다.";
            return false;
        }

        if (!gameDataProvider.TryGetGameData(out GameData data)
            || data?.holdingMoney == null)
        {
            message = "자금 정보를 불러오지 못했습니다.";
            return false;
        }

        int price = Mathf.Max(1, state.offerPrice);
        if (!DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.NoMoneyOrItemCost)
            && data.holdingMoney.Value < price)
        {
            message = $"자금이 부족합니다. 필요 {price} / 보유 {data.holdingMoney.Value}";
            return false;
        }

        if (!DungeonDebugRuntimeRules.IsEnabled(DungeonDebugCheat.NoMoneyOrItemCost))
        {
            data.holdingMoney.Value -= price;
        }

        int released = items.ReleaseStacksByDestination(
            state.incidentId,
            zone.centerPos);
        state.stage = ExteriorIncidentStage.Resolved;
        state.outcome = ExteriorIncidentOutcome.TradePurchased;
        DespawnVisitors(state);
        message = $"상인 화물 {released}개 스택 구매 완료 / 지불 {price}";
        EventBus.RaiseAlert(
            "상인 거래 완료",
            "구매한 화물이 하차장에 놓였습니다. 직원이 창고로 운반합니다.",
            EventAlertImportance.Medium,
            "외부");
        return true;
    }
}

public sealed class InformantExteriorIncidentHandler : ExteriorIncidentHandlerBase
{
    private readonly IOffenseRegionRuntime regions;

    public InformantExteriorIncidentHandler(
        IExteriorIncidentActorService actors,
        IOffenseRegionRuntime regions,
        IGameEventBus eventBus)
        : base(actors, eventBus)
    {
        this.regions = regions ?? throw new ArgumentNullException(nameof(regions));
    }

    public override ExteriorIncidentKind Kind => ExteriorIncidentKind.Informant;
    public override string DefaultText => "정보상이 변경 교역권의 움직임을 전하러 왔습니다.";
    public override float DurationSeconds => 150f;

    public override bool TryBegin(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string failureReason)
    {
        if (!SpawnVisitor(state, zone, false, out _, out failureReason))
        {
            return false;
        }

        state.stage = ExteriorIncidentStage.Active;
        return true;
    }

    public override void Tick(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        float deltaTime)
    {
        if (state.IsTerminal)
        {
            return;
        }

        if (!TryApplyReceptionOnEntry(state, zone))
        {
            if (state.remainingSeconds <= 0f)
            {
                state.stage = ExteriorIncidentStage.TimedOut;
                DespawnVisitors(state);
            }
            return;
        }

        state.progress = Mathf.Clamp(
            state.progress + (zone.ReceptionReadiness * 0.004f * deltaTime),
            0f,
            100f);
        if (state.progress >= 100f)
        {
            regions.TryApplyReconnaissance(
                OffenseRegionRuntime.BorderTradeRegionId,
                8f,
                out _);
            state.outcome = ExteriorIncidentOutcome.IntelligenceAcquired;
            state.stage = ExteriorIncidentStage.Resolved;
            DespawnVisitors(state);
            EventBus.RaiseAlert(
                "지역 정보 확보",
                "정보상의 증언으로 변경 교역권의 정보망이 약해졌습니다.",
                EventAlertImportance.Medium,
                "외부");
        }
        else if (state.remainingSeconds <= 0f)
        {
            state.stage = ExteriorIncidentStage.TimedOut;
            DespawnVisitors(state);
        }
    }
}

public sealed class ThiefExteriorIncidentHandler : ExteriorIncidentHandlerBase
{
    private readonly IWorldItemStackRuntime items;

    public ThiefExteriorIncidentHandler(
        IExteriorIncidentActorService actors,
        IWorldItemStackRuntime items,
        IGameEventBus eventBus)
        : base(actors, eventBus)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public override ExteriorIncidentKind Kind => ExteriorIncidentKind.Thief;
    public override string DefaultText => "도둑이 하차장의 실물 화물을 노리고 있습니다.";
    public override float DurationSeconds => 120f;

    public override bool TryBegin(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string failureReason)
    {
        if (!SpawnVisitor(state, zone, false, out _, out failureReason))
        {
            return false;
        }

        state.stage = ExteriorIncidentStage.Preparing;
        return true;
    }

    public override void Tick(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        float deltaTime)
    {
        if (state.IsTerminal)
        {
            return;
        }

        if (zone.PatrolReadiness >= 70f)
        {
            state.stage = ExteriorIncidentStage.Resolved;
            state.outcome = ExteriorIncidentOutcome.TheftPrevented;
            DespawnVisitors(state);
            return;
        }

        state.progress += deltaTime;
        if (state.stolenItemQuantity <= 0
            && state.progress >= 12f
            && state.actorIds.Count > 0
            && Actors.TryFind(state.actorIds[0], out CharacterActor thief)
            && items.TryStealLooseItem(thief, 12, out WorldItemStackSnapshot stolen, out _))
        {
            state.stolenItemId = stolen.ItemId;
            state.stolenItemQuantity = 1;
            state.stage = ExteriorIncidentStage.Interacting;
        }

        if (state.remainingSeconds > 0f)
        {
            return;
        }

        state.stage = ExteriorIncidentStage.TimedOut;
        state.outcome = state.stolenItemQuantity > 0
            ? ExteriorIncidentOutcome.ItemStolen
            : ExteriorIncidentOutcome.TheftPrevented;
        DespawnVisitors(state);
        if (state.outcome == ExteriorIncidentOutcome.ItemStolen)
        {
            EventBus.RaiseAlert(
                "하차장 절도",
                "경비가 늦어 도둑이 실제 화물을 들고 달아났습니다.",
                EventAlertImportance.High,
                "외부");
        }
    }
}

public sealed class InjuredReturneeExteriorIncidentHandler : ExteriorIncidentHandlerBase
{
    public InjuredReturneeExteriorIncidentHandler(
        IExteriorIncidentActorService actors,
        IGameEventBus eventBus)
        : base(actors, eventBus)
    {
    }

    public override ExteriorIncidentKind Kind => ExteriorIncidentKind.InjuredReturnee;
    public override string DefaultText => "부상 귀환자가 쓰러진 채 구조를 기다립니다.";
    public override float DurationSeconds => 120f;

    public override bool TryBegin(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string failureReason)
    {
        if (!SpawnVisitor(state, zone, true, out _, out failureReason))
        {
            return false;
        }

        state.stage = ExteriorIncidentStage.Resolved;
        state.outcome = ExteriorIncidentOutcome.RescueOrdered;
        EventBus.RaiseAlert(
            "부상 귀환자 구조 요청",
            "입구에 쓰러진 귀환자를 위한 구조·치료 작업이 생성되었습니다.",
            EventAlertImportance.High,
            "외부");
        return true;
    }
}

public sealed class PredatorApproachExteriorIncidentHandler :
    ExteriorIncidentHandlerBase
{
    private const string PredatorSpeciesId = "shadow_wolf";
    private IWildlifeRuntime wildlife;

    public PredatorApproachExteriorIncidentHandler(
        IExteriorIncidentActorService actors,
        IGameEventBus eventBus)
        : base(actors, eventBus)
    {
    }

    internal void BindWildlifeRuntime(IWildlifeRuntime runtime)
    {
        if (runtime == null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        if (wildlife != null && !ReferenceEquals(wildlife, runtime))
        {
            throw new InvalidOperationException(
                "Predator incident wildlife runtime is already bound.");
        }

        wildlife = runtime;
    }

    public override ExteriorIncidentKind Kind =>
        ExteriorIncidentKind.PredatorApproach;
    public override string DefaultText =>
        "어둠 속 포식자가 입구 쪽으로 접근하고 있습니다.";
    public override float DurationSeconds => 45f;

    public override bool TryBegin(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string failureReason)
    {
        if (wildlife == null)
        {
            failureReason = "야생동물 런타임 준비가 끝나지 않았습니다.";
            return false;
        }

        if (!wildlife.TrySpawnArrival(
                PredatorSpeciesId,
                zone.centerPos,
                out WildlifeActor predator,
                out failureReason))
        {
            return false;
        }

        predator.SetPredatorStalking();
        state.wildlifeIds.Add(predator.WildlifeId);
        state.stage = ExteriorIncidentStage.Active;
        EventBus.RaiseAlert(
            "외부 포식자 접근",
            "순찰대가 입구 근처의 그림자늑대를 발견했습니다. 실제 외부 개체로 진입 중입니다.",
            EventAlertImportance.High,
            "외부");
        return true;
    }

    public override void Tick(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        float deltaTime)
    {
        if (state.IsTerminal)
        {
            return;
        }

        state.progress += Mathf.Max(0f, deltaTime);
        if (state.progress < 8f)
        {
            return;
        }

        state.stage = ExteriorIncidentStage.Resolved;
        state.outcome = ExteriorIncidentOutcome.PredatorApproached;
    }
}

public sealed class CargoDamageExteriorIncidentHandler :
    ExteriorIncidentHandlerBase
{
    private const string RuinedCargoItemId = "wild:rot";
    private readonly IWorldItemStackRuntime items;

    public CargoDamageExteriorIncidentHandler(
        IExteriorIncidentActorService actors,
        IWorldItemStackRuntime items,
        IGameEventBus eventBus)
        : base(actors, eventBus)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public override ExteriorIncidentKind Kind => ExteriorIncidentKind.CargoDamage;
    public override string DefaultText =>
        "하차장에 노출된 화물이 날씨와 야간 위험에 훼손될 수 있습니다.";
    public override float DurationSeconds => 50f;

    public override bool TryBegin(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        out string failureReason)
    {
        WorldItemStackSnapshot target = FindExposedCargo(zone);
        if (target == null)
        {
            failureReason = "하차장 주변에 노출된 실물 화물이 없습니다.";
            return false;
        }

        state.itemStackIds.Add(target.StackId);
        state.stage = ExteriorIncidentStage.Preparing;
        items.PrioritizeHaul(target.StackId);
        EventBus.RaiseAlert(
            "하차장 화물 훼손 위험",
            $"{target.DisplayName}이 노출되어 있습니다. 운반 우선 작업이 발행되었으며 제때 옮기면 피해를 막을 수 있습니다.",
            EventAlertImportance.High,
            "외부");
        failureReason = string.Empty;
        return true;
    }

    public override void Tick(
        ExteriorIncidentRuntimeState state,
        ExteriorZoneMarker zone,
        float deltaTime)
    {
        if (state.IsTerminal || state.itemStackIds.Count == 0)
        {
            return;
        }

        WorldItemStackSnapshot target = FindStack(state.itemStackIds[0]);
        if (target == null
            || target.State != WorldItemStackState.Loose
            || GridDistance(target.Position, zone.centerPos) > 3)
        {
            state.stage = ExteriorIncidentStage.Resolved;
            state.outcome = ExteriorIncidentOutcome.CargoSecured;
            EventBus.RaiseAlert(
                "하차장 화물 확보",
                "직원이 위험해지기 전에 노출 화물을 옮겼습니다.",
                EventAlertImportance.Low,
                "외부");
            return;
        }

        state.progress += Mathf.Max(0f, deltaTime);
        float damageDelay = Mathf.Lerp(
            10f,
            24f,
            Mathf.Clamp01(zone.PatrolReadiness / 100f));
        if (state.progress < damageDelay)
        {
            state.stage = ExteriorIncidentStage.Active;
            return;
        }

        if (!items.TryConsumeStackQuantity(
                target.StackId,
                1,
                out WorldItemStackSnapshot consumed))
        {
            state.stage = ExteriorIncidentStage.Failed;
            return;
        }

        items.SpawnItemAt(
            RuinedCargoItemId,
            1,
            consumed.Position,
            WorldItemStackState.Loose,
            string.Empty,
            out _);
        state.stage = ExteriorIncidentStage.Resolved;
        state.outcome = ExteriorIncidentOutcome.CargoDamaged;
        EventBus.RaiseAlert(
            "하차장 화물 훼손",
            $"{consumed.DisplayName} 1개가 망가져 부패 잔해가 되었습니다. 재고는 경고 없이 사라지지 않습니다.",
            EventAlertImportance.High,
            "외부");
    }

    private WorldItemStackSnapshot FindExposedCargo(ExteriorZoneMarker zone)
    {
        return items.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && !stack.IsReserved
                && !stack.HasUniqueMetadata
                && !string.Equals(
                    stack.ItemId,
                    RuinedCargoItemId,
                    StringComparison.Ordinal)
                && GridDistance(stack.Position, zone.centerPos) <= 3)
            .OrderByDescending(stack => stack.TotalValue)
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private WorldItemStackSnapshot FindStack(string stackId)
    {
        return items.GetAllStacks().FirstOrDefault(stack =>
            stack != null
            && string.Equals(
                stack.StackId,
                stackId,
                StringComparison.Ordinal));
    }

    private static int GridDistance(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }
}
