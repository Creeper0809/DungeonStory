using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

[Serializable]
public sealed class CharacterSpeciesRuntimeState
{
    public string characterPersistentId = string.Empty;
    public string speciesTag = string.Empty;
    [Range(0f, 100f)] public float charge = 100f;
    [Range(0f, 100f)] public float integrity = 100f;
    public float nextIncidentAt;
    public string lastIncidentId = string.Empty;
    public int incidentCount;
}

[Serializable]
public sealed class CharacterSpeciesRuntimeSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<CharacterSpeciesRuntimeState> characters =
        new List<CharacterSpeciesRuntimeState>();
}

public readonly struct SpeciesIncidentContext
{
    public SpeciesIncidentContext(
        CharacterActor actor,
        CharacterSpeciesSO species,
        CharacterSpeciesRuntimeState state)
    {
        Actor = actor;
        Species = species;
        State = state;
    }

    public CharacterActor Actor { get; }
    public CharacterSpeciesSO Species { get; }
    public CharacterSpeciesRuntimeState State { get; }
}

public readonly struct SpeciesIncidentTriggeredEvent
{
    public SpeciesIncidentTriggeredEvent(
        string characterPersistentId,
        string speciesTag,
        string incidentId,
        Vector2Int position,
        string summary)
    {
        CharacterPersistentId = characterPersistentId ?? string.Empty;
        SpeciesTag = speciesTag ?? string.Empty;
        IncidentId = incidentId ?? string.Empty;
        Position = position;
        Summary = summary ?? string.Empty;
    }

    public string CharacterPersistentId { get; }
    public string SpeciesTag { get; }
    public string IncidentId { get; }
    public Vector2Int Position { get; }
    public string Summary { get; }
}

public interface ISpeciesIncidentHandler
{
    string IncidentId { get; }
    bool Execute(SpeciesIncidentContext context, out string summary);
}

public interface ISpeciesIncidentHandlerRegistry
{
    bool TryExecute(SpeciesIncidentContext context, out string summary);
}

public interface ICharacterSpeciesRuntime
{
    bool TryGet(
        string characterPersistentId,
        out CharacterSpeciesRuntimeState state);
    bool Recharge(
        string characterPersistentId,
        float amount,
        out string message);
    bool RepairIntegrity(
        string characterPersistentId,
        float amount,
        out string message);
    CharacterSpeciesRuntimeSaveData Capture();
    void Restore(CharacterSpeciesRuntimeSaveData data);
}

public sealed class SpeciesIncidentHandlerRegistry :
    ISpeciesIncidentHandlerRegistry
{
    private readonly Dictionary<string, ISpeciesIncidentHandler> handlers;

    public SpeciesIncidentHandlerRegistry(
        IWorldItemStackRuntime items,
        IWorldFilthQuery filth,
        ICharacterAiWorldRegistry world)
    {
        ISpeciesIncidentHandler[] values =
        {
            new BeastkinCommotionHandler(world),
            new DemonContractCurseHandler(world),
            new KoboldPartsHoardingHandler(items),
            new MyconidSporeBloomHandler(filth),
            new HarpyGaleCommotionHandler(items),
            new GolemCoreOverloadHandler(world)
        };
        handlers = values.ToDictionary(
            value => value.IncidentId,
            StringComparer.Ordinal);
    }

    public bool TryExecute(
        SpeciesIncidentContext context,
        out string summary)
    {
        summary = string.Empty;
        string incidentId = context.Species?.IncidentId ?? string.Empty;
        return handlers.TryGetValue(
                incidentId,
                out ISpeciesIncidentHandler handler)
            && handler.Execute(context, out summary);
    }
}

public sealed class CharacterSpeciesRuntime :
    ICharacterSpeciesRuntime,
    ITickable
{
    private const float TickInterval = 1f;
    private const float IncidentCooldown = 300f;
    private const float IncidentMoodThreshold = 30f;

    private readonly ICharacterAiWorldRegistry world;
    private readonly ICharacterSpeciesCatalog speciesCatalog;
    private readonly ISpeciesIncidentHandlerRegistry incidents;
    private readonly IGameClock clock;
    private readonly IGameEventBus events;
    private readonly Dictionary<string, CharacterSpeciesRuntimeState> states =
        new Dictionary<string, CharacterSpeciesRuntimeState>(
            StringComparer.Ordinal);
    private float nextTickAt;

    public CharacterSpeciesRuntime(
        ICharacterAiWorldRegistry world,
        ICharacterSpeciesCatalog speciesCatalog,
        ISpeciesIncidentHandlerRegistry incidents,
        IGameClock clock,
        IGameEventBus events)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.incidents = incidents
            ?? throw new ArgumentNullException(nameof(incidents));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Tick()
    {
        if (clock.IsPaused || clock.Time < nextTickAt)
        {
            return;
        }

        float elapsed = nextTickAt <= 0f
            ? TickInterval
            : Mathf.Max(TickInterval, clock.Time - nextTickAt + TickInterval);
        nextTickAt = clock.Time + TickInterval;
        foreach (CharacterActor actor in world.Characters)
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            CharacterSpeciesSO species = actor.profile?.Species;
            if (species == null
                && !speciesCatalog.TryGet(actor.SpeciesTag, out species))
            {
                continue;
            }

            CharacterSpeciesRuntimeState state = GetOrCreate(actor, species);
            TickPhysiology(actor, species, state, elapsed);
            TryTriggerIncident(actor, species, state);
        }
    }

    public bool TryGet(
        string characterPersistentId,
        out CharacterSpeciesRuntimeState state)
    {
        return states.TryGetValue(
            characterPersistentId?.Trim() ?? string.Empty,
            out state);
    }

    public bool Recharge(
        string characterPersistentId,
        float amount,
        out string message)
    {
        message = string.Empty;
        if (!TryGet(characterPersistentId, out CharacterSpeciesRuntimeState state)
            || !string.Equals(state.speciesTag, "Golem", StringComparison.OrdinalIgnoreCase))
        {
            message = "충전 가능한 골렘 상태를 찾지 못했습니다.";
            return false;
        }

        state.charge = Mathf.Clamp(state.charge + amount, 0f, 100f);
        message = $"충전 {state.charge:0}/100";
        return true;
    }

    public bool RepairIntegrity(
        string characterPersistentId,
        float amount,
        out string message)
    {
        message = string.Empty;
        if (!TryGet(characterPersistentId, out CharacterSpeciesRuntimeState state)
            || !string.Equals(state.speciesTag, "Golem", StringComparison.OrdinalIgnoreCase))
        {
            message = "정비 가능한 골렘 상태를 찾지 못했습니다.";
            return false;
        }

        state.integrity = Mathf.Clamp(state.integrity + amount, 0f, 100f);
        message = $"건전도 {state.integrity:0}/100";
        return true;
    }

    public CharacterSpeciesRuntimeSaveData Capture()
    {
        return new CharacterSpeciesRuntimeSaveData
        {
            characters = states.Values
                .OrderBy(
                    value => value.characterPersistentId,
                    StringComparer.Ordinal)
                .Select(Clone)
                .ToList()
        };
    }

    public void Restore(CharacterSpeciesRuntimeSaveData data)
    {
        states.Clear();
        foreach (CharacterSpeciesRuntimeState source in data?.characters
                     ?? Enumerable.Empty<CharacterSpeciesRuntimeState>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.characterPersistentId))
            {
                continue;
            }

            CharacterSpeciesRuntimeState clone = Clone(source);
            clone.charge = Mathf.Clamp(clone.charge, 0f, 100f);
            clone.integrity = Mathf.Clamp(clone.integrity, 0f, 100f);
            states[clone.characterPersistentId] = clone;
        }
    }

    private void TickPhysiology(
        CharacterActor actor,
        CharacterSpeciesSO species,
        CharacterSpeciesRuntimeState state,
        float elapsed)
    {
        if (species.needs?.UsesChargeInsteadOfFood != true)
        {
            return;
        }

        state.charge = Mathf.Clamp(
            state.charge
            - 0.035f
            * Mathf.Max(0f, species.needs.chargeRateMultiplier)
            * elapsed,
            0f,
            100f);
        state.integrity = actor.Stats != null
            ? Mathf.Min(
                state.integrity,
                actor.Stats.CurrentHealth
                / Mathf.Max(1f, actor.Stats.MaxHealth)
                * 100f)
            : state.integrity;
        if (state.charge < 25f)
        {
            actor.ApplyMoodFactor(
                "species:golem-low-charge",
                "동력핵 충전 부족",
                -10f,
                5f,
                1);
        }

        if (state.charge <= 0f)
        {
            actor.Stats?.ApplyNonLethalDamage(
                Mathf.Max(0.1f, actor.Stats.MaxHealth * 0.0025f * elapsed),
                "동력핵 방전");
        }
    }

    private void TryTriggerIncident(
        CharacterActor actor,
        CharacterSpeciesSO species,
        CharacterSpeciesRuntimeState state)
    {
        string incidentId = species.IncidentId;
        if (string.IsNullOrWhiteSpace(incidentId)
            || incidentId is CharacterSpeciesIncidentIds.SlimeContamination
                or CharacterSpeciesIncidentIds.OrcRampage
                or CharacterSpeciesIncidentIds.VampireFear
            || state.nextIncidentAt > clock.Time)
        {
            return;
        }

        bool forcedGolemOverload =
            incidentId == CharacterSpeciesIncidentIds.GolemCoreOverload
            && state.charge <= 5f;
        if (!forcedGolemOverload && actor.Mood.Value > IncidentMoodThreshold)
        {
            return;
        }

        int sample = CharacterGrowthRules.StableHash(
            state.characterPersistentId
            + "|"
            + incidentId
            + "|"
            + state.incidentCount);
        if (!forcedGolemOverload
            && (sample & 0x7fffffff) / (float)int.MaxValue > 0.25f)
        {
            state.nextIncidentAt = clock.Time + 30f;
            return;
        }

        SpeciesIncidentContext context =
            new SpeciesIncidentContext(actor, species, state);
        if (!incidents.TryExecute(context, out string summary))
        {
            return;
        }

        state.lastIncidentId = incidentId;
        state.incidentCount++;
        state.nextIncidentAt = clock.Time + IncidentCooldown;
        actor.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.Social,
            CharacterActivityOutcomes.Failed,
            summary,
            null,
            actionId: incidentId,
            reasonCode: "species-discontent",
            bubbleEligible: true));
        events.Publish(new SpeciesIncidentTriggeredEvent(
            state.characterPersistentId,
            species.speciesTag,
            incidentId,
            actor.GetNowXY(),
            summary));
    }

    private CharacterSpeciesRuntimeState GetOrCreate(
        CharacterActor actor,
        CharacterSpeciesSO species)
    {
        string id = actor.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            id = $"runtime-character:{actor.GetInstanceID()}";
        }

        if (states.TryGetValue(id, out CharacterSpeciesRuntimeState state))
        {
            state.speciesTag = species.speciesTag;
            return state;
        }

        state = new CharacterSpeciesRuntimeState
        {
            characterPersistentId = id,
            speciesTag = species.speciesTag,
            charge = 100f,
            integrity = 100f,
            nextIncidentAt = clock.Time + 30f
        };
        states.Add(id, state);
        return state;
    }

    private static CharacterSpeciesRuntimeState Clone(
        CharacterSpeciesRuntimeState source)
    {
        return new CharacterSpeciesRuntimeState
        {
            characterPersistentId = source.characterPersistentId ?? string.Empty,
            speciesTag = source.speciesTag ?? string.Empty,
            charge = source.charge,
            integrity = source.integrity,
            nextIncidentAt = source.nextIncidentAt,
            lastIncidentId = source.lastIncidentId ?? string.Empty,
            incidentCount = source.incidentCount
        };
    }
}

internal abstract class SpeciesIncidentHandlerBase : ISpeciesIncidentHandler
{
    public abstract string IncidentId { get; }
    public abstract bool Execute(
        SpeciesIncidentContext context,
        out string summary);

    protected static IEnumerable<CharacterActor> Nearby(
        ICharacterAiWorldRegistry world,
        CharacterActor source,
        int radius)
    {
        Vector2Int origin = source.GetNowXY();
        return world.Characters.Where(actor => actor != null
            && actor != source
            && !actor.IsDead
            && Mathf.Abs(actor.GetNowXY().x - origin.x)
                + Mathf.Abs(actor.GetNowXY().y - origin.y)
                <= radius);
    }
}

internal sealed class BeastkinCommotionHandler :
    SpeciesIncidentHandlerBase
{
    private readonly ICharacterAiWorldRegistry world;
    public BeastkinCommotionHandler(ICharacterAiWorldRegistry world) =>
        this.world = world;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.BeastkinCommotion;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        foreach (CharacterActor actor in Nearby(world, context.Actor, 3))
        {
            actor.ApplyMoodFactor(
                IncidentId,
                "수인 소동의 소음",
                -4f,
                90f,
                2);
        }

        summary = "무리 불만이 수인 소동으로 번져 주변의 휴식과 작업을 방해했습니다.";
        return true;
    }
}

internal sealed class DemonContractCurseHandler :
    SpeciesIncidentHandlerBase
{
    private readonly ICharacterAiWorldRegistry world;
    public DemonContractCurseHandler(ICharacterAiWorldRegistry world) =>
        this.world = world;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.DemonContractCurse;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        foreach (CharacterActor actor in Nearby(world, context.Actor, 4))
        {
            actor.ApplyMoodFactor(
                IncidentId,
                "계약 저주의 압박",
                -6f,
                120f,
                1);
        }

        summary = "불이행된 대가를 요구하는 계약 저주가 주변 인원에게 남았습니다.";
        return true;
    }
}

internal sealed class KoboldPartsHoardingHandler :
    SpeciesIncidentHandlerBase
{
    private readonly IWorldItemStackRuntime items;
    public KoboldPartsHoardingHandler(IWorldItemStackRuntime items) =>
        this.items = items;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.KoboldPartsHoarding;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        Vector2Int origin = context.Actor.GetNowXY();
        WorldItemStackSnapshot source = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && !stack.HasUniqueMetadata
                && stack.StockCategory == StockCategory.General
                && stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored)
            .OrderBy(stack =>
                Mathf.Abs(stack.Position.x - origin.x)
                + Mathf.Abs(stack.Position.y - origin.y))
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (source == null
            || !items.TryConsumeStackQuantity(
                source.StackId,
                1,
                out WorldItemStackSnapshot consumed))
        {
            summary = "숨길 부품을 찾지 못해 코볼트의 사재기가 미수에 그쳤습니다.";
            return true;
        }

        Vector2Int hidePosition = origin + Vector2Int.right;
        items.SpawnItemAt(
            consumed.ItemId,
            1,
            hidePosition,
            WorldItemStackState.Loose,
            string.Empty,
            out _);
        WorldItemStackSnapshot hidden = items.GetStacksAt(hidePosition)
            .Where(stack => stack.ItemId == consumed.ItemId)
            .OrderByDescending(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (hidden != null)
        {
            items.SetForbidden(hidden.StackId, true);
        }

        summary = $"{consumed.DisplayName} 1개를 인접 칸에 숨기고 금지 표시했습니다.";
        return true;
    }
}

internal sealed class MyconidSporeBloomHandler :
    SpeciesIncidentHandlerBase
{
    private readonly IWorldFilthQuery filth;
    public MyconidSporeBloomHandler(IWorldFilthQuery filth) =>
        this.filth = filth;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.MyconidSporeBloom;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        string sourceId = context.Actor.Identity?.PersistentId ?? string.Empty;
        filth.AddFilth(
            WorldFilthType.Stain,
            context.Actor.GetNowXY(),
            18f,
            sourceId,
            0.35f);
        summary = "건조 스트레스로 포자 개화가 발생해 실제 오염이 남았습니다.";
        return true;
    }
}

internal sealed class HarpyGaleCommotionHandler :
    SpeciesIncidentHandlerBase
{
    private readonly IWorldItemStackRuntime items;
    public HarpyGaleCommotionHandler(IWorldItemStackRuntime items) =>
        this.items = items;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.HarpyGaleCommotion;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        Vector2Int origin = context.Actor.GetNowXY();
        WorldItemStackSnapshot source = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && stack.Quantity > 0
                && !stack.HasUniqueMetadata
                && Mathf.Abs(stack.Position.x - origin.x)
                    + Mathf.Abs(stack.Position.y - origin.y)
                    <= 3)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (source == null
            || !items.TryConsumeStackQuantity(
                source.StackId,
                1,
                out WorldItemStackSnapshot consumed))
        {
            summary = "돌풍이 불었지만 흩어질 loose stack이 없었습니다.";
            return true;
        }

        Vector2Int destination = source.Position
            + ((CharacterGrowthRules.StableHash(source.StackId) & 1) == 0
                ? Vector2Int.left
                : Vector2Int.up);
        items.SpawnItemAt(
            consumed.ItemId,
            1,
            destination,
            WorldItemStackState.Loose,
            string.Empty,
            out _);
        summary = $"{consumed.DisplayName} 1개가 돌풍에 인접 칸으로 흩어졌습니다.";
        return true;
    }
}

internal sealed class GolemCoreOverloadHandler :
    SpeciesIncidentHandlerBase
{
    private readonly ICharacterAiWorldRegistry world;
    public GolemCoreOverloadHandler(ICharacterAiWorldRegistry world) =>
        this.world = world;
    public override string IncidentId =>
        CharacterSpeciesIncidentIds.GolemCoreOverload;

    public override bool Execute(
        SpeciesIncidentContext context,
        out string summary)
    {
        context.Actor.Stats?.ApplyNonLethalDamage(
            context.Actor.Stats.MaxHealth * 0.08f,
            "핵 과부하");
        Vector2Int origin = context.Actor.GetNowXY();
        foreach (BuildableObject building in world.Buildings.Where(
                     building => building != null
                         && Mathf.Abs(building.centerPos.x - origin.x)
                             + Mathf.Abs(building.centerPos.y - origin.y)
                             <= 1))
        {
            building.SetDamaged(true);
        }

        summary = "방전된 동력핵이 과부하되어 골렘과 인접 시설이 실제 피해를 입었습니다.";
        return true;
    }
}
