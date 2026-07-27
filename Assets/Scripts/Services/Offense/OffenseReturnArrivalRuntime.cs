using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public enum OffenseReturnArrivalKind
{
    Prisoner = 0,
    SpecialWildlife = 1
}

public enum OffenseReturnArrivalStage
{
    WaitingForParty = 0,
    Ready = 1,
    AwaitingContainment = 2,
    Secured = 3,
    Escaped = 4
}

[Serializable]
public sealed class OffenseReturnArrivalState
{
    public string arrivalId = string.Empty;
    public string expeditionId = string.Empty;
    public string targetId = string.Empty;
    public OffenseReturnArrivalKind kind;
    public int requestedAmount;
    public bool returnSealed;
    public int returningMembers;
    public OffenseReturnArrivalStage stage;
    public float escapeRisk;
    public List<string> materializedIds = new List<string>();
    public List<string> escapedIds = new List<string>();
    public string lastStatus = string.Empty;

    public OffenseReturnArrivalState Clone()
    {
        return new OffenseReturnArrivalState
        {
            arrivalId = arrivalId ?? string.Empty,
            expeditionId = expeditionId ?? string.Empty,
            targetId = targetId ?? string.Empty,
            kind = kind,
            requestedAmount = Mathf.Max(0, requestedAmount),
            returnSealed = returnSealed,
            returningMembers = Mathf.Max(0, returningMembers),
            stage = stage,
            escapeRisk = Mathf.Clamp(escapeRisk, 0f, 100f),
            materializedIds = materializedIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>(),
            escapedIds = escapedIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>(),
            lastStatus = lastStatus ?? string.Empty
        };
    }
}

[Serializable]
public sealed class DungeonOffenseReturnArrivalSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int nextArrivalSequence = 1;
    public List<OffenseReturnArrivalState> arrivals = new List<OffenseReturnArrivalState>();
}

public interface IOffenseReturnArrivalRuntime
{
    IReadOnlyList<OffenseReturnArrivalState> Arrivals { get; }
    void BeginExpeditionReturn(string expeditionId);
    void RegisterReturningMember(string expeditionId);
    void CompleteReturningMember(string expeditionId);
    void SealExpeditionReturn(string expeditionId);
    int QueueArrival(
        string expeditionId,
        string targetId,
        OffenseReturnArrivalKind kind,
        int amount);
    DungeonOffenseReturnArrivalSaveData Capture();
    void Restore(
        DungeonOffenseReturnArrivalSaveData saveData,
        DungeonGameRestoreReport report = null);
}

public sealed class OffenseReturnArrivalRuntime :
    IOffenseReturnArrivalRuntime,
    ITickable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("OffenseReturnArrivalRuntime.Tick");

    private sealed class ReturnBarrier
    {
        public int ReturningMembers;
        public bool Sealed;
    }

    private const float RetryIntervalSeconds = 2f;
    private const float EscapeRiskPerSecond = 0.75f;
    private const string SpecialWildlifeSpeciesId = "rune_deer";

    private readonly IGridSystemProvider gridProvider;
    private readonly IWorldDropZoneQuery dropZoneQuery;
    private readonly ICharacterSpawnerProvider spawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterFactory;
    private readonly IInvasionIntruderDataProvider intruderDataProvider;
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCommandService captivityCommands;
    private readonly IWildlifeRuntime wildlife;
    private readonly IWildlifeCaptureRuntime wildlifeCapture;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IGameClock clock;
    private readonly IGameEventBus eventBus;
    private readonly List<OffenseReturnArrivalState> arrivals =
        new List<OffenseReturnArrivalState>();
    private readonly Dictionary<string, ReturnBarrier> barriers =
        new Dictionary<string, ReturnBarrier>(StringComparer.Ordinal);
    private int nextArrivalSequence = 1;
    private float nextRetryAt;

    public OffenseReturnArrivalRuntime(
        IGridSystemProvider gridProvider,
        IWorldDropZoneQuery dropZoneQuery,
        ICharacterSpawnerProvider spawnerProvider,
        ICharacterSpawnObjectFactory characterFactory,
        IInvasionIntruderDataProvider intruderDataProvider,
        ICharacterBodyHealthRuntime bodyHealth,
        ICharacterAiWorldRegistry worldRegistry,
        ICaptivityRuntime captivity,
        ICaptivityCommandService captivityCommands,
        IWildlifeRuntime wildlife,
        IWildlifeCaptureRuntime wildlifeCapture,
        IBuildingWorldQuery buildingWorld,
        IGameClock clock,
        IGameEventBus eventBus)
    {
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.dropZoneQuery = dropZoneQuery ?? throw new ArgumentNullException(nameof(dropZoneQuery));
        this.spawnerProvider = spawnerProvider ?? throw new ArgumentNullException(nameof(spawnerProvider));
        this.characterFactory = characterFactory ?? throw new ArgumentNullException(nameof(characterFactory));
        this.intruderDataProvider = intruderDataProvider ?? throw new ArgumentNullException(nameof(intruderDataProvider));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.captivityCommands = captivityCommands ?? throw new ArgumentNullException(nameof(captivityCommands));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.wildlifeCapture = wildlifeCapture ?? throw new ArgumentNullException(nameof(wildlifeCapture));
        this.buildingWorld = buildingWorld ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public IReadOnlyList<OffenseReturnArrivalState> Arrivals => arrivals;

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        if (clock.DeltaTime <= 0f)
        {
            return;
        }

        foreach (OffenseReturnArrivalState arrival in arrivals)
        {
            if (arrival.stage != OffenseReturnArrivalStage.AwaitingContainment)
            {
                continue;
            }

            if (TryFinalizeResolvedArrival(arrival))
            {
                continue;
            }

            arrival.escapeRisk = Mathf.Clamp(
                arrival.escapeRisk + EscapeRiskPerSecond * clock.DeltaTime,
                0f,
                100f);
            if (arrival.escapeRisk >= 100f)
            {
                ResolveUncontainedEscapes(arrival);
                TryFinalizeResolvedArrival(arrival);
            }
        }

        if (clock.Time < nextRetryAt)
        {
            return;
        }

        nextRetryAt = clock.Time + RetryIntervalSeconds;
        MaterializeReadyArrivals();
        RetryAutomaticContainment();
    }

    public void BeginExpeditionReturn(string expeditionId)
    {
        string normalized = Normalize(expeditionId);
        if (normalized.Length == 0)
        {
            return;
        }

        barriers[normalized] = new ReturnBarrier();
        ForEachWaiting(normalized, state =>
        {
            state.returnSealed = false;
            state.returningMembers = 0;
        });
    }

    public void RegisterReturningMember(string expeditionId)
    {
        string normalized = Normalize(expeditionId);
        if (normalized.Length == 0)
        {
            return;
        }

        ReturnBarrier barrier = GetOrCreateBarrier(normalized);
        barrier.ReturningMembers++;
        ForEachWaiting(normalized, state => state.returningMembers++);
    }

    public void CompleteReturningMember(string expeditionId)
    {
        string normalized = Normalize(expeditionId);
        if (normalized.Length == 0)
        {
            return;
        }

        ReturnBarrier barrier = GetOrCreateBarrier(normalized);
        barrier.ReturningMembers = Mathf.Max(0, barrier.ReturningMembers - 1);
        ForEachWaiting(
            normalized,
            state => state.returningMembers = Mathf.Max(0, state.returningMembers - 1));
        MaterializeReadyArrivals();
    }

    public void SealExpeditionReturn(string expeditionId)
    {
        string normalized = Normalize(expeditionId);
        if (normalized.Length == 0)
        {
            return;
        }

        ReturnBarrier barrier = GetOrCreateBarrier(normalized);
        barrier.Sealed = true;
        ForEachWaiting(normalized, state => state.returnSealed = true);
        MaterializeReadyArrivals();
    }

    public int QueueArrival(
        string expeditionId,
        string targetId,
        OffenseReturnArrivalKind kind,
        int amount)
    {
        int safeAmount = Mathf.Max(0, amount);
        if (safeAmount <= 0)
        {
            return 0;
        }

        string normalizedExpeditionId = Normalize(expeditionId);
        barriers.TryGetValue(normalizedExpeditionId, out ReturnBarrier barrier);
        OffenseReturnArrivalState state = new OffenseReturnArrivalState
        {
            arrivalId = $"return:{nextArrivalSequence++}",
            expeditionId = normalizedExpeditionId,
            targetId = Normalize(targetId),
            kind = kind,
            requestedAmount = safeAmount,
            returnSealed = normalizedExpeditionId.Length == 0
                || (barrier?.Sealed ?? false),
            returningMembers = barrier?.ReturningMembers ?? 0,
            stage = OffenseReturnArrivalStage.WaitingForParty,
            lastStatus = "원정대 귀환을 기다리는 중입니다."
        };
        arrivals.Add(state);
        MaterializeReadyArrivals();
        return safeAmount;
    }

    public DungeonOffenseReturnArrivalSaveData Capture()
    {
        return new DungeonOffenseReturnArrivalSaveData
        {
            version = DungeonOffenseReturnArrivalSaveData.CurrentVersion,
            nextArrivalSequence = Mathf.Max(1, nextArrivalSequence),
            arrivals = arrivals.Select(state => state.Clone()).ToList()
        };
    }

    public void Restore(
        DungeonOffenseReturnArrivalSaveData saveData,
        DungeonGameRestoreReport report = null)
    {
        arrivals.Clear();
        barriers.Clear();
        if (saveData == null
            || saveData.version != DungeonOffenseReturnArrivalSaveData.CurrentVersion)
        {
            if (saveData != null)
            {
                report?.AddWarning(
                    $"지원하지 않는 귀환 대상 저장 버전 {saveData.version}입니다.");
            }
            nextArrivalSequence = 1;
            return;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (OffenseReturnArrivalState source in saveData.arrivals
                     ?? new List<OffenseReturnArrivalState>())
        {
            if (source == null || string.IsNullOrWhiteSpace(source.arrivalId))
            {
                continue;
            }

            OffenseReturnArrivalState restored = source.Clone();
            if (!ids.Add(restored.arrivalId))
            {
                report?.AddWarning($"중복 귀환 대상 ID를 건너뛰었습니다: {restored.arrivalId}");
                continue;
            }

            if (restored.stage == OffenseReturnArrivalStage.WaitingForParty)
            {
                restored.returnSealed = true;
                restored.returningMembers = 0;
                restored.lastStatus = "불러오기 후 하차장 도착을 재개합니다.";
            }
            arrivals.Add(restored);
        }

        nextArrivalSequence = Mathf.Max(1, saveData.nextArrivalSequence);
        MaterializeReadyArrivals();
    }

    private void MaterializeReadyArrivals()
    {
        foreach (OffenseReturnArrivalState arrival in arrivals.Where(state =>
                     state.stage == OffenseReturnArrivalStage.WaitingForParty
                     && state.returnSealed
                     && state.returningMembers <= 0))
        {
            arrival.stage = OffenseReturnArrivalStage.Ready;
            if (!TryMaterialize(arrival, out string status))
            {
                arrival.stage = OffenseReturnArrivalStage.WaitingForParty;
                arrival.returnSealed = true;
                arrival.lastStatus = status;
                continue;
            }

            arrival.stage = OffenseReturnArrivalStage.AwaitingContainment;
            arrival.lastStatus = status;
        }
    }

    private bool TryMaterialize(
        OffenseReturnArrivalState arrival,
        out string status)
    {
        status = string.Empty;
        if (!gridProvider.TryGetGrid(out Grid grid)
            || !dropZoneQuery.TryGetExpeditionLootDropoff(out Vector2Int dropoff))
        {
            status = "하차장 위치를 찾지 못해 귀환 대상을 대기시켰습니다.";
            return false;
        }

        int remaining = Mathf.Max(0, arrival.requestedAmount - arrival.materializedIds.Count);
        for (int index = 0; index < remaining; index++)
        {
            Vector2Int spawnCell = FindNearbyDropCell(grid, dropoff, index);
            if (arrival.kind == OffenseReturnArrivalKind.Prisoner)
            {
                if (!TrySpawnPrisoner(arrival, grid, spawnCell, out string actorId))
                {
                    status = "귀환 포로를 생성하지 못해 다음 갱신 때 다시 시도합니다.";
                    return false;
                }
                arrival.materializedIds.Add(actorId);
            }
            else
            {
                if (!wildlife.TrySpawnArrival(
                        SpecialWildlifeSpeciesId,
                        spawnCell,
                        out WildlifeActor actor,
                        out string message))
                {
                    status = string.IsNullOrWhiteSpace(message)
                        ? "특수 동물을 하차장에 내리지 못했습니다."
                        : message;
                    return false;
                }

                actor.ApplyDamage(
                    Mathf.Max(1, Mathf.CeilToInt(actor.MaxHealth * 0.7f)),
                    null);
                arrival.materializedIds.Add(actor.WildlifeId);
            }
        }

        status = arrival.kind == OffenseReturnArrivalKind.Prisoner
            ? $"포로 {arrival.materializedIds.Count}명이 하차장에 도착했습니다."
            : $"특수 동물 {arrival.materializedIds.Count}마리가 하차장에 도착했습니다.";
        eventBus.RaiseAlert(
            "원정 귀환 대상 도착",
            status,
            EventAlertImportance.High,
            "오펜스");
        TryAutomaticContainment(arrival);
        return true;
    }

    private bool TrySpawnPrisoner(
        OffenseReturnArrivalState arrival,
        Grid grid,
        Vector2Int position,
        out string actorId)
    {
        actorId = string.Empty;
        if (!spawnerProvider.TryGetSpawner(out CharacterSpawner spawner)
            || spawner == null
            || spawner.characterPrefab == null)
        {
            return false;
        }

        CharacterSO data = intruderDataProvider.GetRequiredIntruderData(null);
        if (data == null)
        {
            return false;
        }

        GameObject characterObject = characterFactory.Create(spawner.characterPrefab);
        characterFactory.Inject(characterObject);
        CharacterActor actor = characterObject.GetComponent<CharacterActor>();
        if (actor == null)
        {
            characterFactory.Destroy(characterObject);
            return false;
        }

        actorId = $"{arrival.arrivalId}:prisoner:{arrival.materializedIds.Count + 1}";
        characterObject.name = $"귀환 포로 {arrival.materializedIds.Count + 1}";
        characterObject.transform.position = grid.GetWorldPos(position);
        actor.Initialize(data);
        actor.characterType = CharacterType.Intruder;
        actor.Identity?.SetCharacterType(CharacterType.Intruder);
        actor.Identity?.SetPersistentId(actorId);
        worldRegistry.RegisterCharacter(actor);
        worldRegistry.RegisterCharacterLifetime(actor);
        ApplyDownedArrivalHealth(actor);
        actor.SetLifecycleState(CharacterLifecycleState.Downed);
        return true;
    }

    private void ApplyDownedArrivalHealth(CharacterActor actor)
    {
        CharacterBodyHealthSnapshot baseline = bodyHealth.GetSnapshot(actor);
        List<CharacterBodyPartHealthState> parts = baseline.Parts
            .Select(part => new CharacterBodyPartHealthState
            {
                bodyPart = part.bodyPart,
                maxHealth = part.maxHealth,
                currentHealth = part.bodyPart switch
                {
                    CombatBodyPart.Torso => part.maxHealth * 0.22f,
                    CombatBodyPart.LeftLeg => part.maxHealth * 0.12f,
                    CombatBodyPart.RightLeg => part.maxHealth * 0.12f,
                    _ => part.maxHealth * 0.55f
                },
                bleedingPerSecond = 0f
            })
            .ToList();
        bodyHealth.ApplySnapshot(
            actor,
            new CharacterBodyHealthSnapshot(
                parts,
                45f,
                0f,
                0.2f,
                0.55f,
                0.12f,
                true),
            "원정 포로 귀환");
    }

    private void RetryAutomaticContainment()
    {
        foreach (OffenseReturnArrivalState arrival in arrivals.Where(state =>
                     state.stage == OffenseReturnArrivalStage.AwaitingContainment))
        {
            TryAutomaticContainment(arrival);
        }
    }

    private void TryAutomaticContainment(OffenseReturnArrivalState arrival)
    {
        if (arrival.kind == OffenseReturnArrivalKind.Prisoner)
        {
            foreach (string id in arrival.materializedIds)
            {
                if (captivity.IsCaptive(id))
                {
                    continue;
                }

                CharacterActor subject = FindCharacter(id);
                CharacterActor carrier = FindNearestCarrier(subject != null
                    ? subject.GetNowXY()
                    : Vector2Int.zero);
                if (subject != null
                    && subject.CurrentLifecycleState == CharacterLifecycleState.Downed
                    && carrier != null)
                {
                    captivityCommands.TryOrderCapture(subject, carrier, out _);
                }
            }
            return;
        }

        BuildableObject pen = buildingWorld.Buildings.FirstOrDefault(building =>
            building != null
            && !building.isDestroy
            && building.BuildingData.GetBeastPenAbility() != null);
        if (pen == null)
        {
            return;
        }

        foreach (string id in arrival.materializedIds)
        {
            if (wildlifeCapture.IsCaptured(id))
            {
                continue;
            }

            WildlifeActor animal = wildlife.Wildlife.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.WildlifeId, id, StringComparison.Ordinal));
            CharacterActor carrier = FindNearestCarrier(animal != null
                ? animal.GridPosition
                : pen.centerPos);
            if (animal != null && carrier != null)
            {
                wildlifeCapture.TryOrderCapture(animal, carrier, pen, out _);
            }
        }
    }

    private bool TryFinalizeResolvedArrival(OffenseReturnArrivalState arrival)
    {
        if (arrival.materializedIds.Count < arrival.requestedAmount)
        {
            return false;
        }

        int secured = arrival.kind == OffenseReturnArrivalKind.Prisoner
            ? arrival.materializedIds.Count(captivity.IsCaptive)
            : arrival.materializedIds.Count(wildlifeCapture.IsCaptured);
        int escaped = arrival.escapedIds.Count(id =>
            arrival.materializedIds.Contains(id, StringComparer.Ordinal));
        if (secured + escaped < arrival.materializedIds.Count)
        {
            return false;
        }

        arrival.stage = escaped > 0
            ? OffenseReturnArrivalStage.Escaped
            : OffenseReturnArrivalStage.Secured;
        arrival.escapeRisk = 0f;
        arrival.lastStatus = escaped > 0
            ? $"{escaped}개 대상이 수용 전에 달아났습니다."
            : "수용 절차가 완료되었습니다.";
        return true;
    }

    private void ResolveUncontainedEscapes(OffenseReturnArrivalState arrival)
    {
        int escaped = 0;
        foreach (string id in arrival.materializedIds)
        {
            if (arrival.escapedIds.Contains(id, StringComparer.Ordinal))
            {
                continue;
            }

            bool secured = arrival.kind == OffenseReturnArrivalKind.Prisoner
                ? captivity.IsCaptive(id)
                : wildlifeCapture.IsCaptured(id);
            if (secured)
            {
                continue;
            }

            if (arrival.kind == OffenseReturnArrivalKind.Prisoner)
            {
                CharacterActor actor = FindCharacter(id);
                if (actor != null)
                {
                    actor.SetLifecycleState(CharacterLifecycleState.Active);
                    worldRegistry.UnregisterCharacter(actor);
                    worldRegistry.UnregisterCharacterLifetime(actor);
                    characterFactory.Destroy(actor.gameObject);
                }
            }
            else
            {
                wildlife.TryRemoveArrival(id);
            }

            arrival.escapedIds.Add(id);
            escaped++;
        }

        if (escaped <= 0)
        {
            return;
        }

        string subject = arrival.kind == OffenseReturnArrivalKind.Prisoner
            ? "귀환 포로"
            : "특수 동물";
        string countedSubject = arrival.kind == OffenseReturnArrivalKind.Prisoner
            ? $"{escaped}명이"
            : $"{escaped}마리가";
        eventBus.RaiseAlert(
            $"{subject} 이탈",
            $"수용 준비가 늦어 {subject} {countedSubject} 하차장에서 달아났습니다.",
            EventAlertImportance.High,
            "오펜스");
    }

    private CharacterActor FindCharacter(string id)
    {
        return worldRegistry.AllCharacters.FirstOrDefault(actor =>
            actor != null
            && actor.Identity != null
            && string.Equals(actor.Identity.PersistentId, id, StringComparison.Ordinal));
    }

    private CharacterActor FindNearestCarrier(Vector2Int position)
    {
        return worldRegistry.Characters
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.characterType == CharacterType.NPC
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
            .OrderBy(actor => Manhattan(actor.GetNowXY(), position))
            .FirstOrDefault();
    }

    private static Vector2Int FindNearbyDropCell(Grid grid, Vector2Int anchor, int index)
    {
        if (index <= 0 && grid.IsWalkable(anchor))
        {
            return anchor;
        }

        for (int radius = 1; radius <= 5; radius++)
        {
            foreach (Vector2Int candidate in EnumerateRing(anchor, radius))
            {
                GridCell cell = grid.GetGridCell(candidate);
                if (cell != null && cell.AllowsItemDrop && grid.IsWalkable(candidate))
                {
                    return candidate;
                }
            }
        }

        return anchor;
    }

    private static IEnumerable<Vector2Int> EnumerateRing(Vector2Int center, int radius)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius)
                {
                    continue;
                }
                yield return center + new Vector2Int(x, y);
            }
        }
    }

    private void ForEachWaiting(
        string expeditionId,
        Action<OffenseReturnArrivalState> action)
    {
        string normalized = Normalize(expeditionId);
        if (normalized.Length == 0 || action == null)
        {
            return;
        }

        foreach (OffenseReturnArrivalState state in arrivals.Where(item =>
                     string.Equals(item.expeditionId, normalized, StringComparison.Ordinal)
                     && item.stage == OffenseReturnArrivalStage.WaitingForParty))
        {
            action(state);
        }
    }

    private ReturnBarrier GetOrCreateBarrier(string expeditionId)
    {
        if (!barriers.TryGetValue(expeditionId, out ReturnBarrier barrier))
        {
            barrier = new ReturnBarrier();
            barriers.Add(expeditionId, barrier);
        }

        return barrier;
    }

    private static string Normalize(string value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }
}

public sealed class OffenseReturnArrivalSaveSection : IDungeonSaveSection
{
    public const string Id = "offense.return-arrivals";
    private readonly IOffenseReturnArrivalRuntime runtime;

    public OffenseReturnArrivalSaveSection(IOffenseReturnArrivalRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        PhysicalItemsSaveSection.Id,
        CharacterWorldSaveSection.Id,
        WildlifeSaveSection.Id,
        CaptivitySaveSection.Id,
        OffenseSaveSection.Id
    };

    public string Capture()
    {
        return JsonUtility.ToJson(runtime.Capture());
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {sectionVersion}.");
        }

        runtime.Restore(
            JsonUtility.FromJson<DungeonOffenseReturnArrivalSaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonOffenseReturnArrivalSaveData(),
            report);
    }
}
