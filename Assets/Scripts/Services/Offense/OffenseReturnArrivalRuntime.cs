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
    public List<EnemyIndividualSaveData> prisonerIndividuals = new();
    public string lastStatus = string.Empty;
    public int settledMaterializedAmount;
    public int settledSecuredAmount;
    public int settledEscapedAmount;

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
            prisonerIndividuals = (prisonerIndividuals
                    ?? new List<EnemyIndividualSaveData>())
                .Select(value => value?.Clone())
                .ToList(),
            lastStatus = lastStatus ?? string.Empty,
            settledMaterializedAmount = Mathf.Max(0, settledMaterializedAmount),
            settledSecuredAmount = Mathf.Max(0, settledSecuredAmount),
            settledEscapedAmount = Mathf.Max(0, settledEscapedAmount)
        };
    }
}

[Serializable]
public sealed class OffensePrisonerCandidatePoolState
{
    public string expeditionId = string.Empty;
    public List<EnemyIndividualSaveData> individuals = new();

    public OffensePrisonerCandidatePoolState Clone() => new()
    {
        expeditionId = expeditionId ?? string.Empty,
        individuals = (individuals ?? new List<EnemyIndividualSaveData>())
            .Select(value => value?.Clone())
            .ToList()
    };
}

[Serializable]
public sealed class DungeonOffenseReturnArrivalSaveData
{
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public int nextArrivalSequence = 1;
    public List<OffenseReturnArrivalState> arrivals = new List<OffenseReturnArrivalState>();
    public List<OffensePrisonerCandidatePoolState> prisonerCandidatePools = new();
}

public interface IOffenseReturnArrivalRuntime
{
    IReadOnlyList<OffenseReturnArrivalState> Arrivals { get; }
    IReadOnlyList<OffenseExpeditionArrivalReceipt> GetSettlementReceipts(
        string expeditionId);
    void BeginExpeditionReturn(string expeditionId);
    void RegisterReturningMember(string expeditionId);
    void CompleteReturningMember(string expeditionId);
    void SealExpeditionReturn(string expeditionId);
    void RegisterBattlePrisonerCandidates(
        string expeditionId,
        IEnumerable<EnemyIndividualSaveData> individuals);
    void DiscardBattlePrisonerCandidates(string expeditionId);
    int QueueArrival(
        string expeditionId,
        string targetId,
        OffenseReturnArrivalKind kind,
        int amount);
    DungeonOffenseReturnArrivalSaveData Capture();
    OffenseReturnArrivalRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseReturnArrivalSaveData saveData,
        DungeonGameRestoreReport report);
    void PublishRestoreCandidate(
        OffenseReturnArrivalRestoreCandidate candidate);
}

public sealed class OffenseReturnArrivalRuntime :
    IOffenseReturnArrivalRuntime,
    ITickable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("OffenseReturnArrivalRuntime.Tick");

    private const float RetryIntervalSeconds = 2f;
    private const float EscapeRiskPerSecond = 0.75f;
    private const string SpecialWildlifeSpeciesId = "rune_deer";

    private readonly IGridSystemProvider gridProvider;
    private readonly IWorldDropZoneQuery dropZoneQuery;
    private readonly ICharacterSpawnerProvider spawnerProvider;
    private readonly ICharacterSpawnObjectFactory characterFactory;
    private readonly IInvasionIntruderDataProvider intruderDataProvider;
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCommandService captivityCommands;
    private readonly IWildlifeRuntime wildlife;
    private readonly IWildlifeCaptureRuntime wildlifeCapture;
    private readonly IEnemyArchetypeCatalog enemyArchetypes;
    private readonly IEnemyIndividualFactory enemyIndividuals;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IGameClock clock;
    private readonly IGameEventBus eventBus;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    private OffenseReturnArrivalAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(
            () => new OffenseReturnArrivalAggregateState());
    private OffenseReturnArrivalAggregateState writableAggregateState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new OffenseReturnArrivalAggregateState(),
            state => state.Clone());
    private List<OffenseReturnArrivalState> arrivals =>
        writableAggregateState.Arrivals;
    private Dictionary<string, OffenseReturnBarrier> barriers =>
        writableAggregateState.Barriers;
    private Dictionary<string, List<EnemyIndividualSaveData>>
        prisonerCandidatePools => writableAggregateState.PrisonerCandidatePools;
    private int nextArrivalSequence
    {
        get => aggregateState.NextArrivalSequence;
        set => writableAggregateState.NextArrivalSequence = value;
    }
    private float nextRetryAt
    {
        get => aggregateState.NextRetryAt;
        set => writableAggregateState.NextRetryAt = value;
    }

    public OffenseReturnArrivalRuntime(
        OffenseReturnArrivalWorldServices world,
        OffenseReturnArrivalDomainServices domain)
    {
        OffenseReturnArrivalWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        OffenseReturnArrivalDomainServices requiredDomain = domain
            ?? throw new ArgumentNullException(nameof(domain));
        gridProvider = requiredWorld.Grid;
        dropZoneQuery = requiredWorld.DropZones;
        spawnerProvider = requiredWorld.Spawners;
        characterFactory = requiredWorld.CharacterFactory;
        intruderDataProvider = requiredWorld.IntruderData;
        worldRegistry = requiredWorld.WorldRegistry;
        buildingWorld = requiredWorld.Buildings;
        aggregateRootStore = requiredWorld.AggregateRoots;
        bodyHealthQuery = requiredDomain.BodyHealthQuery;
        bodyHealthCommands = requiredDomain.BodyHealthCommands;
        captivity = requiredDomain.Captivity;
        captivityCommands = requiredDomain.CaptivityCommands;
        wildlife = requiredDomain.Wildlife;
        wildlifeCapture = requiredDomain.WildlifeCapture;
        enemyArchetypes = requiredDomain.EnemyArchetypes;
        enemyIndividuals = requiredDomain.EnemyIndividuals;
        equipment = requiredDomain.Equipment;
        clock = requiredDomain.Clock;
        eventBus = requiredDomain.EventBus;
    }

    public IReadOnlyList<OffenseReturnArrivalState> Arrivals => arrivals;
    public IReadOnlyList<OffenseExpeditionArrivalReceipt> GetSettlementReceipts(
        string expeditionId)
    {
        string normalized = Normalize(expeditionId);
        return aggregateState.Arrivals
            .Where(value => string.Equals(
                value.expeditionId,
                normalized,
                StringComparison.Ordinal))
            .OrderBy(value => value.arrivalId, StringComparer.Ordinal)
            .Select(CreateSettlementReceipt)
            .ToArray();
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

        barriers[normalized] = new OffenseReturnBarrier();
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

        OffenseReturnBarrier barrier = GetOrCreateBarrier(normalized);
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

        OffenseReturnBarrier barrier = GetOrCreateBarrier(normalized);
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

        OffenseReturnBarrier barrier = GetOrCreateBarrier(normalized);
        barrier.Sealed = true;
        ForEachWaiting(normalized, state => state.returnSealed = true);
        MaterializeReadyArrivals();
    }

    public void RegisterBattlePrisonerCandidates(
        string expeditionId,
        IEnumerable<EnemyIndividualSaveData> candidates)
    {
        string normalizedExpeditionId = Normalize(expeditionId);
        if (normalizedExpeditionId.Length == 0)
        {
            throw new ArgumentException(
                "A canonical expedition ID is required.",
                nameof(expeditionId));
        }

        if (!prisonerCandidatePools.TryGetValue(
                normalizedExpeditionId,
                out List<EnemyIndividualSaveData> pool))
        {
            if (prisonerCandidatePools.Count >=
                OffenseReturnArrivalSaveValidation.MaximumCandidatePools)
            {
                throw new InvalidOperationException(
                    "The offense prisoner candidate-pool limit has been reached.");
            }
            pool = new List<EnemyIndividualSaveData>();
            prisonerCandidatePools.Add(normalizedExpeditionId, pool);
        }

        HashSet<string> existing = pool
            .Select(value => value.characterId)
            .ToHashSet(StringComparer.Ordinal);
        int totalCandidateCount = prisonerCandidatePools.Values.Sum(
            values => values.Count);
        foreach (EnemyIndividualSaveData candidate in
            candidates ?? Array.Empty<EnemyIndividualSaveData>())
        {
            EnemyIndividualBlueprint blueprint =
                enemyIndividuals.RequireBlueprint(candidate);
            if (!blueprint.Archetype.individualGeneration.recruitable
                || !existing.Add(blueprint.SaveData.characterId))
            {
                continue;
            }

            if (pool.Count >=
                    OffenseReturnArrivalSaveValidation.MaximumCandidatesPerPool
                || totalCandidateCount >=
                    OffenseReturnArrivalSaveValidation.MaximumCandidateIndividuals)
            {
                break;
            }

            pool.Add(blueprint.SaveData.Clone());
            totalCandidateCount++;
        }

        pool.Sort((left, right) => string.CompareOrdinal(
            left.characterId,
            right.characterId));
        if (pool.Count == 0)
        {
            prisonerCandidatePools.Remove(normalizedExpeditionId);
        }
    }

    public void DiscardBattlePrisonerCandidates(string expeditionId)
    {
        string normalized = Normalize(expeditionId);
        if (normalized.Length > 0)
        {
            prisonerCandidatePools.Remove(normalized);
        }
    }

    public int QueueArrival(
        string expeditionId,
        string targetId,
        OffenseReturnArrivalKind kind,
        int amount)
    {
        int safeAmount = Mathf.Clamp(
            amount,
            0,
            OffenseReturnArrivalSaveValidation.MaximumArrivalSize);
        string normalizedTargetId = Normalize(targetId);
        if (safeAmount <= 0
            || normalizedTargetId.Length == 0
            || !Enum.IsDefined(typeof(OffenseReturnArrivalKind), kind))
        {
            return 0;
        }

        string normalizedExpeditionId = Normalize(expeditionId);
        barriers.TryGetValue(
            normalizedExpeditionId,
            out OffenseReturnBarrier barrier);
        OffenseReturnArrivalState state = new OffenseReturnArrivalState
        {
            arrivalId = $"return:{nextArrivalSequence++}",
            expeditionId = normalizedExpeditionId,
            targetId = normalizedTargetId,
            kind = kind,
            requestedAmount = safeAmount,
            returnSealed = normalizedExpeditionId.Length == 0
                || (barrier?.Sealed ?? false),
            returningMembers = barrier?.ReturningMembers ?? 0,
            stage = OffenseReturnArrivalStage.WaitingForParty,
            lastStatus = "원정대 귀환을 기다리는 중입니다."
        };
        PopulatePrisonerIndividuals(state);
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
            arrivals = arrivals.Select(state => state.Clone()).ToList(),
            prisonerCandidatePools = prisonerCandidatePools
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new OffensePrisonerCandidatePoolState
                {
                    expeditionId = pair.Key,
                    individuals = pair.Value
                        .OrderBy(value => value.characterId, StringComparer.Ordinal)
                        .Select(value => value.Clone())
                        .ToList()
                })
                .ToList()
        };
    }

    private void PopulatePrisonerIndividuals(OffenseReturnArrivalState state)
    {
        if (state.kind != OffenseReturnArrivalKind.Prisoner)
        {
            return;
        }

        if (prisonerCandidatePools.TryGetValue(
                state.expeditionId,
                out List<EnemyIndividualSaveData> candidates))
        {
            int take = Math.Min(state.requestedAmount, candidates.Count);
            state.prisonerIndividuals.AddRange(candidates
                .Take(take)
                .Select(value => value.Clone()));
            candidates.RemoveRange(0, take);
            if (candidates.Count == 0)
            {
                prisonerCandidatePools.Remove(state.expeditionId);
            }
        }

        EnemyArchetypeDefinitionSO[] recruitable = enemyArchetypes.All
            .Where(value => value.individualGeneration?.recruitable == true)
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        if (recruitable.Length == 0)
        {
            throw new InvalidOperationException(
                "V20 prisoner rewards require at least one recruitable enemy archetype.");
        }

        for (int index = state.prisonerIndividuals.Count;
             index < state.requestedAmount;
             index++)
        {
            CharacterId characterId = CharacterId.FromStableSuffix(
                $"{state.arrivalId}:prisoner:{index + 1}");
            uint selector = PersistentEntityId.GetStableHash32(
                $"{state.targetId}:{state.arrivalId}:{index + 1}");
            EnemyArchetypeDefinitionSO archetype = recruitable[
                (int)(selector % (uint)recruitable.Length)];
            state.prisonerIndividuals.Add(enemyIndividuals.Create(
                archetype.stableId,
                characterId,
                $"return:{state.expeditionId}:{state.targetId}:{index + 1}"));
        }
    }

    public OffenseReturnArrivalRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseReturnArrivalSaveData saveData,
        DungeonGameRestoreReport report)
    {
        OffenseReturnArrivalSaveValidation.Validate(saveData, report);
        if (report.Success)
        {
            foreach (OffenseReturnArrivalState arrival in saveData.arrivals)
            {
                foreach (EnemyIndividualSaveData individual in
                    arrival.prisonerIndividuals ?? new List<EnemyIndividualSaveData>())
                {
                    try
                    {
                        enemyIndividuals.RequireBlueprint(individual);
                    }
                    catch (Exception exception)
                    {
                        report.AddError(
                            $"Offense return-arrival '{arrival.arrivalId}' has invalid prisoner individual: {exception.Message}");
                    }
                }
            }
            foreach (OffensePrisonerCandidatePoolState pool in
                saveData.prisonerCandidatePools)
            {
                foreach (EnemyIndividualSaveData individual in pool.individuals)
                {
                    try
                    {
                        EnemyIndividualBlueprint blueprint =
                            enemyIndividuals.RequireBlueprint(individual);
                        if (!blueprint.Archetype.individualGeneration.recruitable)
                        {
                            report.AddError(
                                $"Offense candidate pool '{pool.expeditionId}' contains a non-recruitable individual.");
                        }
                    }
                    catch (Exception exception)
                    {
                        report.AddError(
                            $"Offense candidate pool '{pool.expeditionId}' has an invalid individual: {exception.Message}");
                    }
                }
            }
        }
        return report.Success
            ? new OffenseReturnArrivalRestoreCandidate(
                OffenseReturnArrivalSaveValidation.CreateStrictState(
                    saveData,
                    clock.Time + RetryIntervalSeconds))
            : null;
    }

    public void PublishRestoreCandidate(
        OffenseReturnArrivalRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
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

        GameObject characterObject = characterFactory.CreateInactive(
            spawner.characterPrefab);
        CharacterActor actor = characterObject.GetComponent<CharacterActor>();
        if (actor == null)
        {
            characterFactory.Destroy(characterObject);
            return false;
        }

        int individualIndex = arrival.materializedIds.Count;
        if (individualIndex < 0
            || individualIndex >= arrival.prisonerIndividuals.Count)
        {
            characterFactory.Destroy(characterObject);
            return false;
        }
        EnemyIndividualBlueprint blueprint = enemyIndividuals.RequireBlueprint(
            arrival.prisonerIndividuals[individualIndex]);
        CharacterId characterId = blueprint.CharacterId;
        actorId = characterId.Value;
        characterObject.name = $"귀환 포로 {arrival.materializedIds.Count + 1}";
        characterObject.transform.position = grid.GetWorldPos(position);
        characterObject.name = blueprint.SaveData.displayName;
        actor.Initialize(data, blueprint.SpawnRequest);
        actor.characterType = CharacterType.Intruder;
        actor.Identity?.SetCharacterType(CharacterType.Intruder);
        actor.Identity?.SetPersistentId(characterId);
        enemyIndividuals.EnsureCharacterDomains(blueprint);
        ApplyDownedArrivalHealth(actor);
        actor.SetLifecycleState(CharacterLifecycleState.Downed);
        characterFactory.Publish(characterObject);
        return true;
    }

    private void ApplyDownedArrivalHealth(CharacterActor actor)
    {
        CharacterBodyHealthSnapshot baseline = bodyHealthQuery.GetSnapshot(actor);
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
        bodyHealthCommands.ApplySnapshot(
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
                if (captivity.TryGetCaptive(id, out CaptiveState captive)
                    && (HasCompletedPrisonerContainment(captive)
                        || IsPrisonerCaptureInFlight(captive)))
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

        int secured = CountCurrentlySecuredArrivalSubjects(arrival);
        int escaped = arrival.escapedIds.Count(id =>
            arrival.materializedIds.Contains(id, StringComparer.Ordinal));
        if (secured + escaped < arrival.materializedIds.Count)
        {
            return false;
        }

        arrival.stage = escaped > 0
            ? OffenseReturnArrivalStage.Escaped
            : OffenseReturnArrivalStage.Secured;
        arrival.settledMaterializedAmount = arrival.materializedIds.Count;
        arrival.settledSecuredAmount = secured;
        arrival.settledEscapedAmount = escaped;
        arrival.escapeRisk = 0f;
        arrival.lastStatus = escaped > 0
            ? $"{escaped}개 대상이 수용 전에 달아났습니다."
            : "수용 절차가 완료되었습니다.";
        eventBus.Publish(new OffenseExpeditionArrivalResolvedEvent(
            arrival.expeditionId,
            GetSettlementReceipts(arrival.expeditionId)));
        return true;
    }

    private OffenseExpeditionArrivalReceipt CreateSettlementReceipt(
        OffenseReturnArrivalState arrival)
    {
        OffenseExpeditionArrivalResolution resolution = arrival.stage switch
        {
            OffenseReturnArrivalStage.Secured =>
                OffenseExpeditionArrivalResolution.Secured,
            OffenseReturnArrivalStage.Escaped =>
                OffenseExpeditionArrivalResolution.Escaped,
            _ => OffenseExpeditionArrivalResolution.Pending
        };
        bool terminal = resolution != OffenseExpeditionArrivalResolution.Pending;
        int materialized = terminal
            ? arrival.settledMaterializedAmount
            : arrival.materializedIds.Count;
        int secured = terminal
            ? arrival.settledSecuredAmount
            : CountCurrentlySecuredArrivalSubjects(arrival);
        int escaped = terminal
            ? arrival.settledEscapedAmount
            : arrival.escapedIds.Count(id =>
                arrival.materializedIds.Contains(id, StringComparer.Ordinal));
        return new OffenseExpeditionArrivalReceipt(
            arrival.arrivalId,
            arrival.kind.ToString(),
            arrival.requestedAmount,
            materialized,
            secured,
            escaped,
            resolution);
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
                ? HasCompletedPrisonerContainment(id)
                : wildlifeCapture.IsCaptured(id);
            if (secured)
            {
                continue;
            }

            if (arrival.kind == OffenseReturnArrivalKind.Prisoner)
            {
                CharacterActor actor = FindCharacter(id);
                if (!TryReleasePrisonerCaptureForArrivalEscape(
                        id,
                        actor,
                        out string failureReason))
                {
                    arrival.lastStatus = failureReason;
                    continue;
                }
                equipment.HandleCharacterDeath(id);
                if (actor != null)
                {
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

    private bool HasCompletedPrisonerContainment(string captiveId)
    {
        return captivity.TryGetCaptive(captiveId, out CaptiveState captive)
            && HasCompletedPrisonerContainment(captive);
    }

    private int CountCurrentlySecuredArrivalSubjects(
        OffenseReturnArrivalState arrival)
    {
        return arrival.materializedIds.Count(id =>
            !arrival.escapedIds.Contains(id, StringComparer.Ordinal)
            && (arrival.kind == OffenseReturnArrivalKind.Prisoner
                ? HasCompletedPrisonerContainment(id)
                : wildlifeCapture.IsCaptured(id)));
    }

    private static bool HasCompletedPrisonerContainment(CaptiveState captive)
    {
        return captive != null
            && captive.status is CaptivityStatus.Confined
                or CaptivityStatus.Labor
                or CaptivityStatus.Interaction
                or CaptivityStatus.Performer
                or CaptivityStatus.EscapeAttempt;
    }

    private static bool IsPrisonerCaptureInFlight(CaptiveState captive)
    {
        return captive?.status is CaptivityStatus.Stabilizing
            or CaptivityStatus.AwaitingEscort
            or CaptivityStatus.Escorting;
    }

    private bool TryReleasePrisonerCaptureForArrivalEscape(
        string captiveId,
        CharacterActor actor,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!captivity.TryGetCaptive(captiveId, out CaptiveState captive))
        {
            return true;
        }

        if (HasCompletedPrisonerContainment(captive))
        {
            failureReason = "이미 수용을 완료한 포로는 귀환 이탈로 처리할 수 없습니다.";
            return false;
        }
        if (actor == null)
        {
            failureReason = "귀환 포로의 포획 소유권을 해제할 실제 대상을 찾지 못했습니다.";
            return false;
        }

        CharacterActor carrier = FindCharacter(captive.reservedCarrierId);
        if (captive.IsInCustody
            && !captivityCommands.CancelCapture(
                captiveId,
                "귀환 수용 전 이탈"))
        {
            failureReason = "귀환 포로의 진행 중인 포획 소유권을 해제하지 못했습니다.";
            return false;
        }

        AbilityCaptiveEscort escort = carrier != null
            ? carrier.GetComponent<AbilityCaptiveEscort>()
            : null;
        if (escort?.IsEscorting == true)
        {
            escort.StopEscort("귀환 수용 전 이탈");
        }
        if (escort?.IsEscorting == true
            || !captivity.TryGetCaptive(
                captiveId,
                out CaptiveState releasedCapture)
            || HasPrisonerCaptureOwnership(releasedCapture))
        {
            failureReason = "귀환 포로의 운반·구속구 소유권을 완전히 해제하지 못했습니다.";
            return false;
        }

        ICaptivityEscapeRuntime escapeRuntime =
            captivityCommands as ICaptivityEscapeRuntime
            ?? captivity as ICaptivityEscapeRuntime;
        if (escapeRuntime == null)
        {
            failureReason = "귀환 포로의 이탈 상태를 확정할 포로 명령을 찾지 못했습니다.";
            return false;
        }

        escapeRuntime.CompleteEscape(captiveId, actor);
        if (!captivity.TryGetCaptive(captiveId, out CaptiveState escaped)
            || escaped.status != CaptivityStatus.Escaped
            || escaped.IsInCustody
            || escaped.restrained
            || HasPrisonerCaptureOwnership(escaped))
        {
            failureReason = "귀환 포로의 이탈 상태가 포로 권위에 확정되지 않았습니다.";
            return false;
        }

        return true;
    }

    private static bool HasPrisonerCaptureOwnership(CaptiveState captive)
    {
        return captive == null
            || !string.IsNullOrWhiteSpace(captive.reservedCarrierId)
            || !string.IsNullOrWhiteSpace(captive.housingBuildingId)
            || !string.IsNullOrWhiteSpace(captive.restraintStackId)
            || !string.IsNullOrWhiteSpace(captive.restraintItemId)
            || captive.restraintQuantity > 0
            || !string.IsNullOrWhiteSpace(captive.assignedRestraintItemId)
            || !string.IsNullOrWhiteSpace(captive.assignedRestraintInstanceId)
            || captive.assignedRestraintDurability > 0f
            || captive.assignedRestraintMaximumDurability > 0f;
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

    private OffenseReturnBarrier GetOrCreateBarrier(string expeditionId)
    {
        if (!barriers.TryGetValue(
                expeditionId,
                out OffenseReturnBarrier barrier))
        {
            barrier = new OffenseReturnBarrier();
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

public sealed class OffenseReturnArrivalRestoreCandidate
{
    internal OffenseReturnArrivalRestoreCandidate(
        OffenseReturnArrivalAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal OffenseReturnArrivalAggregateState State { get; }
}
