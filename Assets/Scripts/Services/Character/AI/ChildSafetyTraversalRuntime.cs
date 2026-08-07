using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

public interface IChildSafetyEnvironmentalQuery
{
    int Version { get; }
    bool TryGetCell(
        Vector2Int position,
        out EnvironmentalCellSnapshot snapshot);
}

public sealed class ChildSafetyEnvironmentalProjection :
    IChildSafetyEnvironmentalQuery
{
    private readonly IGridSystemProvider gridProvider;
    private readonly DungeonStory.Environment.EnvironmentalFieldAggregateStateStore
        stateStore;

    public ChildSafetyEnvironmentalProjection(
        IGridSystemProvider gridProvider,
        DungeonStory.Environment.EnvironmentalFieldAggregateStateStore
            stateStore)
    {
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public int Version => stateStore.Current.Version;

    public bool TryGetCell(
        Vector2Int position,
        out EnvironmentalCellSnapshot snapshot)
    {
        DungeonStory.Environment.EnvironmentalFieldAggregateState state =
            stateStore.Current;
        if (!gridProvider.TryGetGrid(out Grid grid)
            || state.Temperature == null
            || state.Air == null
            || !grid.TryGetCellIndex(position, out int index)
            || index < 0
            || index >= state.Temperature.Length
            || index >= state.Air.Length)
        {
            snapshot = default;
            return false;
        }

        float light = state.Light != null && index < state.Light.Length
            ? state.Light[index]
            : 0f;
        snapshot = new EnvironmentalCellSnapshot(
            position,
            state.Temperature[index],
            state.Air[index],
            light);
        return true;
    }
}

public interface IChildSafetyFilthQuery
{
    int StateVersion { get; }
    IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position);
}

public sealed class ChildSafetyFilthProjection : IChildSafetyFilthQuery
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;

    public ChildSafetyFilthProjection(
        DungeonRuntimeAggregateRootStore rootStore)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
    }

    private WorldFilthAggregateState State =>
        rootStore.GetOrCreate(() => new WorldFilthAggregateState());

    public int StateVersion => State.StateVersion;

    public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position) =>
        State.Filth
            .Where(entry => entry != null
                && entry.amount > 0f
                && entry.gridX == position.x
                && entry.gridY == position.y)
            .Select(entry => new WorldFilthSnapshot(
                entry.filthId,
                entry.type,
                entry.amount,
                new Vector2Int(entry.gridX, entry.gridY),
                entry.sourceCharacterId,
                entry.infectionRisk,
                entry.wallStain))
            .ToArray();
}

public sealed class WorldHazardZoneRuntime :
    IWorldHazardZoneQuery,
    IWorldHazardOverlayCommand
{
    private sealed class Overlay
    {
        public WorldHazardFlags Flags;
        public HashSet<Vector2Int> Cells;
    }

    private readonly IChildSafetyEnvironmentalQuery environment;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterSpeciesEnvironmentCatalog species;
    private readonly IChildSafetyFilthQuery filth;
    private readonly Dictionary<string, Overlay> overlays =
        new(StringComparer.Ordinal);
    private int overlayVersion = 1;

    public WorldHazardZoneRuntime(
        IChildSafetyEnvironmentalQuery environment,
        ICharacterLifeQuery life,
        ICharacterSpeciesEnvironmentCatalog species,
        IChildSafetyFilthQuery filth)
    {
        this.environment = environment
            ?? throw new ArgumentNullException(nameof(environment));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.species = species ?? throw new ArgumentNullException(nameof(species));
        this.filth = filth ?? throw new ArgumentNullException(nameof(filth));
    }

    public int Version => unchecked(
        (environment.Version * 397 + filth.StateVersion) * 397 + overlayVersion);

    public WorldHazardSnapshot GetHazard(
        CharacterId characterId,
        Vector2Int position)
    {
        if (!characterId.IsValid)
        {
            throw new ArgumentException(
                "A valid character id is required for hazard projection.",
                nameof(characterId));
        }

        WorldHazardFlags flags = WorldHazardFlags.None;
        foreach (Overlay overlay in overlays.Values)
        {
            if (overlay.Cells.Contains(position))
            {
                flags |= overlay.Flags;
            }
        }

        if (environment.TryGetCell(
                position,
                out EnvironmentalCellSnapshot cell))
        {
            if (cell.AirQuality < 20f)
            {
                flags |= WorldHazardFlags.ToxicAir;
            }

            if (life.TryGet(characterId, out CharacterLifeRecord record))
            {
                SpeciesThermalProfile thermal =
                    species.GetRequiredThermalProfile(record.PhenotypeSpeciesId);
                if (cell.TemperatureC <= thermal.LethalMinimum
                    || cell.TemperatureC >= thermal.LethalMaximum)
                {
                    flags |= WorldHazardFlags.LethalTemperature;
                }
                else if (cell.TemperatureC < thermal.SafeMinimum
                         || cell.TemperatureC > thermal.SafeMaximum)
                {
                    flags |= WorldHazardFlags.UncomfortableTemperature;
                }
            }
        }

        IReadOnlyList<WorldFilthSnapshot> contamination = filth.GetAt(position);
        if (contamination.Any(value =>
                value.InfectionRisk >= 0.65f || value.Amount >= 6f))
        {
            flags |= WorldHazardFlags.SevereContamination;
        }

        WorldHazardFlags forbidden = WorldHazardFlags.Combat
            | WorldHazardFlags.Fire
            | WorldHazardFlags.ToxicAir
            | WorldHazardFlags.LethalTemperature
            | WorldHazardFlags.SevereContamination;
        WorldHazardLevel level = (flags & forbidden) != 0
            ? WorldHazardLevel.Forbidden
            : (flags & (WorldHazardFlags.Industrial
                        | WorldHazardFlags.UncomfortableTemperature)) != 0
                || environment.TryGetCell(position, out cell)
                    && cell.AirQuality < 40f
                ? WorldHazardLevel.Restricted
                : WorldHazardLevel.Safe;
        return new WorldHazardSnapshot(position, level, flags);
    }

    public void ReplaceOverlay(
        string sourceId,
        WorldHazardFlags flags,
        IReadOnlyCollection<Vector2Int> cells)
    {
        string id = sourceId?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            throw new ArgumentException(
                "A stable hazard-overlay source id is required.",
                nameof(sourceId));
        }
        if (flags == WorldHazardFlags.None)
        {
            throw new ArgumentException(
                "A hazard overlay must declare at least one hazard flag.",
                nameof(flags));
        }
        if (cells == null)
        {
            throw new ArgumentNullException(nameof(cells));
        }

        overlays[id] = new Overlay
        {
            Flags = flags,
            Cells = new HashSet<Vector2Int>(cells)
        };
        overlayVersion = unchecked(overlayVersion + 1);
    }

    public void RemoveOverlay(string sourceId)
    {
        string id = sourceId?.Trim() ?? string.Empty;
        if (id.Length > 0 && overlays.Remove(id))
        {
            overlayVersion = unchecked(overlayVersion + 1);
        }
    }
}

public sealed class CombatHazardOverlayAdapter : ITickable
{
    private const string OverlayId = "combat:active-zone";
    private const int CombatZoneRadius = 6;
    private readonly ICharacterCombatCommandRuntime combat;
    private readonly ICharacterWorldQuery world;
    private readonly IWorldHazardOverlayCommand hazards;
    private readonly HashSet<Vector2Int> publishedCells = new();

    public CombatHazardOverlayAdapter(
        ICharacterCombatCommandRuntime combat,
        ICharacterWorldQuery world,
        IWorldHazardOverlayCommand hazards)
    {
        this.combat = combat ?? throw new ArgumentNullException(nameof(combat));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.hazards = hazards ?? throw new ArgumentNullException(nameof(hazards));
    }

    public void Tick()
    {
        HashSet<Vector2Int> next = new();
        foreach (CharacterActor actor in world.Characters)
        {
            if (actor == null || !combat.IsInCombatStance(actor))
            {
                continue;
            }

            Vector2Int origin = actor.GetNowXY();
            for (int x = -CombatZoneRadius; x <= CombatZoneRadius; x++)
            {
                int vertical = CombatZoneRadius - Mathf.Abs(x);
                for (int y = -vertical; y <= vertical; y++)
                {
                    next.Add(origin + new Vector2Int(x, y));
                }
            }
        }

        if (next.SetEquals(publishedCells))
        {
            return;
        }

        publishedCells.Clear();
        publishedCells.UnionWith(next);
        if (next.Count == 0)
        {
            hazards.RemoveOverlay(OverlayId);
        }
        else
        {
            hazards.ReplaceOverlay(OverlayId, WorldHazardFlags.Combat, next);
        }
    }
}

public sealed class ChildSafetyPolicyRuntime : IChildSafetyPolicy
{
    private sealed class ApprenticeshipGrant
    {
        public CharacterId CharacterId;
        public string WorkOrderId;
        public Vector2Int WorkCell;
        public CharacterId SupervisorId;
        public bool HasRequiredProtectiveEquipment;
    }

    private readonly ICharacterLifeQuery life;
    private readonly ICharacterWorldQuery world;
    private readonly HashSet<CharacterId> permittedCharacters = new();
    private readonly Dictionary<string, ApprenticeshipGrant> grants =
        new(StringComparer.Ordinal);
    private int version = 1;

    public ChildSafetyPolicyRuntime(
        ICharacterLifeQuery life,
        ICharacterWorldQuery world)
    {
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public int Version => version;
    public bool SupervisedApprenticeshipEnabled { get; private set; }

    public bool IsCharacterApprenticeshipPermitted(
        CharacterId characterId)
    {
        RequireCharacterId(characterId);
        return permittedCharacters.Contains(characterId);
    }

    public void SetSupervisedApprenticeship(bool enabled)
    {
        if (SupervisedApprenticeshipEnabled == enabled)
        {
            return;
        }

        SupervisedApprenticeshipEnabled = enabled;
        if (!enabled)
        {
            grants.Clear();
        }
        Touch();
    }

    public void SetCharacterApprenticeshipPermission(
        CharacterId characterId,
        bool allowed)
    {
        RequireCharacterId(characterId);
        bool changed = allowed
            ? permittedCharacters.Add(characterId)
            : permittedCharacters.Remove(characterId);
        if (!allowed)
        {
            foreach (string key in grants
                         .Where(pair => pair.Value.CharacterId.Equals(characterId))
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                grants.Remove(key);
            }
        }
        if (changed)
        {
            Touch();
        }
    }

    public bool TryAuthorizeApprenticeship(
        CharacterId characterId,
        string workOrderId,
        Vector2Int workCell,
        CharacterId supervisorId,
        bool workExplicitlyConfirmed,
        bool hasRequiredProtectiveEquipment,
        out ChildSafetyAuthorizationToken token,
        out DomainFailure failure)
    {
        token = default;
        failure = DomainFailure.None;
        string normalizedWorkOrderId = workOrderId?.Trim() ?? string.Empty;
        if (!life.TryGet(characterId, out CharacterLifeRecord apprentice)
            || apprentice.LifeStage != CharacterLifeStage.Adolescent)
        {
            failure = new DomainFailure(
                FailureCode.ChildSafetyWorkForbidden,
                characterId.Value);
            return false;
        }
        if (!SupervisedApprenticeshipEnabled)
        {
            failure = new DomainFailure(
                FailureCode.ChildSafetyApprenticeshipDisabled,
                characterId.Value);
            return false;
        }
        if (!permittedCharacters.Contains(characterId))
        {
            failure = new DomainFailure(
                FailureCode.ChildSafetyCharacterPermissionRequired,
                characterId.Value);
            return false;
        }
        if (!workExplicitlyConfirmed || normalizedWorkOrderId.Length == 0)
        {
            failure = new DomainFailure(
                FailureCode.ChildSafetyWorkConfirmationRequired,
                characterId.Value);
            return false;
        }
        if (!hasRequiredProtectiveEquipment)
        {
            failure = new DomainFailure(
                FailureCode.ChildSafetyProtectiveEquipmentRequired,
                characterId.Value);
            return false;
        }
        if (!TryGetAdultActor(supervisorId, out CharacterActor supervisor))
        {
            failure = new DomainFailure(
                FailureCode.ChildSafetySupervisorUnavailable,
                supervisorId.Value);
            return false;
        }
        if (Manhattan(supervisor.GetNowXY(), workCell) > 6)
        {
            failure = new DomainFailure(
                FailureCode.ChildSafetySupervisorTooFar,
                supervisorId.Value);
            return false;
        }

        Touch();
        ApprenticeshipGrant grant = new()
        {
            CharacterId = characterId,
            WorkOrderId = normalizedWorkOrderId,
            WorkCell = workCell,
            SupervisorId = supervisorId,
            HasRequiredProtectiveEquipment = true
        };
        grants[GrantKey(characterId, normalizedWorkOrderId)] = grant;
        token = new ChildSafetyAuthorizationToken(
            characterId,
            normalizedWorkOrderId,
            version);
        return true;
    }

    public void RevokeApprenticeship(
        CharacterId characterId,
        string workOrderId)
    {
        if (grants.Remove(GrantKey(characterId, workOrderId)))
        {
            Touch();
        }
    }

    public bool CanTraverse(
        GridTraversalContext context,
        in WorldHazardSnapshot from,
        in WorldHazardSnapshot to,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (context.SubjectKind != GridTraversalSubjectKind.Character)
        {
            return true;
        }
        if (!life.TryGet(context.CharacterId, out CharacterLifeRecord record))
        {
            failure = new DomainFailure(
                FailureCode.ChildSafetyLifeStateUnavailable,
                context.CharacterId.Value);
            return false;
        }
        bool authorizationValid = context.MovementIntent
                == GridMovementIntent.Apprenticeship
            && TryGetValidGrant(context, out _);
        bool allowed = ChildSafetyTraversalRules.CanTraverse(
            record.LifeStage,
            context.MovementIntent,
            authorizationValid,
            from.Level,
            to.Level,
            out FailureCode failureCode);
        failure = allowed
            ? DomainFailure.None
            : new DomainFailure(failureCode, context.CharacterId.Value);
        return allowed;
    }

    private bool TryGetValidGrant(
        GridTraversalContext context,
        out ApprenticeshipGrant grant)
    {
        ChildSafetyAuthorizationToken token = context.SafetyAuthorization;
        if (!token.IsValid
            || !token.CharacterId.Equals(context.CharacterId)
            || token.PolicyVersion != version
            || !grants.TryGetValue(
                GrantKey(token.CharacterId, token.WorkOrderId),
                out grant)
            || !grant.HasRequiredProtectiveEquipment
            || !TryGetAdultActor(grant.SupervisorId, out CharacterActor supervisor)
            || Manhattan(supervisor.GetNowXY(), grant.WorkCell) > 6)
        {
            grant = null;
            return false;
        }

        return true;
    }

    private bool TryGetAdultActor(CharacterId id, out CharacterActor actor)
    {
        actor = world.Characters.FirstOrDefault(candidate =>
            CharacterPersistentIdentity.TryGet(candidate, out CharacterId candidateId)
            && candidateId.Equals(id));
        return actor != null
            && life.TryGet(id, out CharacterLifeRecord record)
            && record.LifeStage >= CharacterLifeStage.Adult;
    }

    private void Touch() => version = unchecked(version + 1);

    private static string GrantKey(CharacterId id, string workOrderId)
    {
        RequireCharacterId(id);
        string work = workOrderId?.Trim() ?? string.Empty;
        if (work.Length == 0)
        {
            throw new ArgumentException(
                "A stable work-order id is required.",
                nameof(workOrderId));
        }
        return id.Value + "\n" + work;
    }

    private static void RequireCharacterId(CharacterId id)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException("A valid character id is required.", nameof(id));
        }
    }

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
}

public sealed class ChildSafetyGridTraversalCostPolicy : IGridTraversalCostPolicy
{
    private readonly DefaultGridTraversalCostPolicy terrain;
    private readonly IWorldHazardZoneQuery hazards;
    private readonly IChildSafetyPolicy safety;
    private readonly ICharacterLifeQuery life;

    public ChildSafetyGridTraversalCostPolicy(
        DefaultGridTraversalCostPolicy terrain,
        IWorldHazardZoneQuery hazards,
        IChildSafetyPolicy safety,
        ICharacterLifeQuery life)
    {
        this.terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        this.hazards = hazards ?? throw new ArgumentNullException(nameof(hazards));
        this.safety = safety ?? throw new ArgumentNullException(nameof(safety));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
    }

    public int Version => unchecked(
        ((terrain.Version * 397 + hazards.Version) * 397 + safety.Version) * 397
        + life.Version);
    public int MinimumHorizontalCost => terrain.MinimumHorizontalCost;

    public int GetTraversalCost(
        Grid grid,
        in GridTraversalStepData step,
        GridTraversalContext traversalContext)
    {
        int terrainCost = terrain.GetTraversalCost(grid, step, traversalContext);
        if (terrainCost == int.MaxValue
            || traversalContext.SubjectKind != GridTraversalSubjectKind.Character)
        {
            return terrainCost;
        }

        WorldHazardSnapshot from = hazards.GetHazard(
            traversalContext.CharacterId,
            step.From);
        WorldHazardSnapshot to = hazards.GetHazard(
            traversalContext.CharacterId,
            step.To);
        return safety.CanTraverse(traversalContext, from, to, out _)
            ? terrainCost
            : int.MaxValue;
    }
}
