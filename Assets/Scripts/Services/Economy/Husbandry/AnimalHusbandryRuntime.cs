using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;
using static AnimalHusbandryWorkRules;

public sealed class AnimalHusbandryRuntime :
    IAnimalHusbandryQuery,
    IAnimalHusbandryCommand,
    IAnimalHusbandryPersistence,
    IAnimalHusbandryCommandState,
    ITickable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("AnimalHusbandryRuntime.Tick");
    private const float SecondsPerGameDay = 180f;
    private const float TickIntervalSeconds = 5f;
    private static readonly ItemDefinitionId ManureItemId =
        new("resource:manure");

    private readonly IWildlifeCaptureRuntime captureRuntime;
    private readonly IWildlifeRuntime wildlifeRuntime;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IItemDefinitionCatalog itemCatalog;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWildlifeCarcassService carcassService;
    private readonly IGameClock clock;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly HashSet<WildlifeInstanceId> synchronizedAnimalIds = new();
    private readonly List<WildlifeInstanceId> staleAnimalIds = new();
    private readonly Dictionary<WildlifeInstanceId, CapturedWildlifeState> capturedById =
        new();
    private readonly Dictionary<BuildingInstanceId, List<HusbandryAnimalState>> animalsByPen =
        new();
    private readonly Dictionary<BuildingInstanceId, float> compatibilityRiskByPen =
        new();
    private readonly List<HusbandryAnimalState> animalIterationBuffer =
        new List<HusbandryAnimalState>();
    private readonly List<CapturedWildlifeState> capturedAnimalBuffer =
        new List<CapturedWildlifeState>();
    private readonly AnimalHusbandryPolicyEvaluator policyEvaluator;
    private readonly AnimalHusbandryCommandService commandService;
    private int projectedRestoreRevision;

    private AnimalHusbandryAggregateState State =>
        aggregateRootStore.GetOrCreate(() => new AnimalHusbandryAggregateState());
    private Dictionary<WildlifeInstanceId, HusbandryAnimalState> animals => State.Animals;
    private Dictionary<BuildingInstanceId, AnimalPenPolicyData> policies => State.Policies;
    private float nextTickAt
    {
        get => State.NextTickAt;
        set => State.NextTickAt = value;
    }

    public AnimalHusbandryRuntime(
        IWildlifeCaptureRuntime captureRuntime,
        IWildlifeRuntime wildlifeRuntime,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IItemDefinitionCatalog itemCatalog,
        IWorldItemStackRuntime itemRuntime,
        IWildlifeCarcassService carcassService,
        IGameClock clock,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.captureRuntime = captureRuntime
            ?? throw new ArgumentNullException(nameof(captureRuntime));
        this.wildlifeRuntime = wildlifeRuntime
            ?? throw new ArgumentNullException(nameof(wildlifeRuntime));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.itemCatalog = itemCatalog
            ?? throw new ArgumentNullException(nameof(itemCatalog));
        this.itemCatalog.GetRequired(ManureItemId);
        policyEvaluator = new AnimalHusbandryPolicyEvaluator(this.speciesCatalog);
        commandService = new AnimalHusbandryCommandService(
            this,
            this.captureRuntime,
            this.speciesCatalog);
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.carcassService = carcassService
            ?? throw new ArgumentNullException(nameof(carcassService));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public IReadOnlyList<HusbandryAnimalState> Animals =>
        animals.Values
            .OrderBy(state => state.PenId.Value, StringComparer.Ordinal)
            .ThenBy(state => state.SpeciesId.Value, StringComparer.Ordinal)
            .ThenBy(state => state.AnimalId.Value, StringComparer.Ordinal)
            .Select(state => state.Clone())
            .ToArray();

    public IReadOnlyList<AnimalPenPolicyData> PenPolicies =>
        policies.Values
            .OrderBy(policy => policy.PenId.Value, StringComparer.Ordinal)
            .Select(policy => policy.Clone())
            .ToArray();

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            EnsureRestoreProjectionCurrent();
            if (clock.IsPaused
                || clock.DeltaTime <= 0f
                || clock.Time + 0.001f < nextTickAt)
            {
                return;
            }

            float elapsed = nextTickAt <= 0f
                ? TickIntervalSeconds
                : Mathf.Max(
                    0.01f,
                    clock.Time - (nextTickAt - TickIntervalSeconds));
            nextTickAt = clock.Time + TickIntervalSeconds;
            captureRuntime.CopyCapturedAnimalReferences(capturedAnimalBuffer);
            SynchronizeCapturedAnimals(capturedAnimalBuffer);
            AdvanceAnimals(
                elapsed / SecondsPerGameDay,
                capturedAnimalBuffer);
            policyEvaluator.RefreshAutoSlaughterDesignations(
                animals.Values,
                GetPolicyInternal,
                IsAdult);
        }
    }

    public bool TryGetAnimal(
        WildlifeInstanceId animalId,
        out HusbandryAnimalState state)
    {
        if (animalId.IsValid
            && animals.TryGetValue(animalId, out HusbandryAnimalState found))
        {
            state = found.Clone();
            return true;
        }

        state = null;
        return false;
    }

    public AnimalPenPolicyData GetPenPolicy(BuildingInstanceId penId)
    {
        if (!penId.IsValid)
        {
            return new AnimalPenPolicyData();
        }

        if (!policies.TryGetValue(penId, out AnimalPenPolicyData policy))
        {
            policy = new AnimalPenPolicyData { PenId = penId };
            policies.Add(penId, policy);
        }

        return policy.Clone();
    }

    public int GetEffectivePenCapacity(BuildingInstanceId penId)
    {
        AnimalPenPolicyData policy = GetPolicyInternal(penId);
        return captureRuntime.TryGetPenCapacity(penId.Value, out int physicalCapacity)
            ? Mathf.Min(policy.maximumAnimals, physicalCapacity)
            : policy.maximumAnimals;
    }

    public bool SetPenPolicy(
        AnimalPenPolicyData policy,
        out AnimalHusbandryFailure failure) =>
        commandService.SetPenPolicy(policy, out failure);

    public bool DesignateSlaughter(
        WildlifeInstanceId animalId,
        bool designated,
        out AnimalHusbandryFailure failure) =>
        commandService.DesignateSlaughter(animalId, designated, out failure);

    public bool TryGetWork(
        BuildableObject pen,
        CharacterActor worker,
        out AnimalHusbandryWorkSnapshot work)
    {
        work = default;
        BuildingBeastPenAbility ability =
            pen?.BuildingData.GetBeastPenAbility();
        if (pen == null || ability == null || !ability.IsValid)
        {
            work = Unavailable(
                new AnimalHusbandryFailure(
                    AnimalHusbandryFailureCode.InvalidPen));
            return false;
        }

        BuildingInstanceId penId = GetPenId(pen);
        HusbandryAnimalState selected = animals.Values
            .Where(state => state.PenId.Equals(penId))
            .OrderByDescending(GetWorkPriority)
            .ThenBy(state => state.AnimalId.Value, StringComparer.Ordinal)
            .FirstOrDefault(state => GetWorkPriority(state) > 0);
        if (selected == null)
        {
            work = Unavailable(
                new AnimalHusbandryFailure(
                    AnimalHusbandryFailureCode.NoPendingWork,
                    penId.Value));
            return false;
        }

        AnimalHusbandryWorkKind kind = ResolveWorkKind(selected);
        float required = GetRequiredWork(selected, kind, ability);
        PreparePendingWork(selected, kind);
        float completed = GetCompletedWork(selected, kind, required);
        work = new AnimalHusbandryWorkSnapshot(
            true,
            selected.AnimalId,
            kind,
            required,
            completed,
            AnimalHusbandryFailure.None);
        return true;
    }

    public bool ApplyWork(
        BuildableObject pen,
        CharacterActor worker,
        WildlifeInstanceId animalId,
        AnimalHusbandryWorkKind kind,
        float amount,
        out bool completed)
    {
        completed = false;
        if (pen == null
            || amount <= 0f
            || !animalId.IsValid
            || !animals.TryGetValue(animalId, out HusbandryAnimalState state)
            || !state.PenId.Equals(GetPenId(pen)))
        {
            return false;
        }

        BuildingBeastPenAbility ability =
            pen.BuildingData.GetBeastPenAbility();
        if (ability == null || kind != ResolveWorkKind(state))
        {
            return false;
        }

        float required = GetRequiredWork(state, kind, ability);
        PreparePendingWork(state, kind);
        switch (kind)
        {
            case AnimalHusbandryWorkKind.Tame:
                state.TamingProgress = Mathf.Clamp01(
                    state.TamingProgress + amount / required);
                completed = state.TamingProgress >= 0.999f;
                if (completed)
                {
                    state.Tamed = true;
                    state.TamingProgress = 1f;
                    SetStatus(state, AnimalHusbandryStatusCode.TamingCompleted);
                    captureRuntime.TrySetTamed(
                        state.AnimalId.Value,
                        true,
                        out _);
                    ResetPendingWork(state);
                }
                return true;

            case AnimalHusbandryWorkKind.CollectProduct:
                AnimalProductProgressState product = state.Products
                    .FirstOrDefault(item => item != null && item.ReadyCycles > 0);
                if (product == null
                    || !TryGetProductDefinition(
                        state,
                        product.ItemId,
                        out WildlifeHusbandryProductDefinition definition))
                {
                    return false;
                }

                state.PendingWorkCompleted = Mathf.Min(
                    required,
                    state.PendingWorkCompleted + amount);
                if (state.PendingWorkCompleted + 0.001f < required)
                {
                    return true;
                }

                product.ReadyCycles = Mathf.Max(0, product.ReadyCycles - 1);
                completed = itemRuntime.SpawnItemAt(
                    new ItemDefinitionId(definition.ItemId).Value,
                    definition.Amount,
                    pen.centerPos,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                    && spawned > 0;
                SetStatus(
                    state,
                    completed
                        ? AnimalHusbandryStatusCode.ProductCollected
                        : AnimalHusbandryStatusCode.ProductStorageUnavailable,
                    definition.ItemId);
                if (completed)
                {
                    ResetPendingWork(state);
                }
                return completed;

            case AnimalHusbandryWorkKind.CollectManure:
                state.PendingWorkCompleted = Mathf.Min(
                    required,
                    state.PendingWorkCompleted + amount);
                if (state.PendingWorkCompleted + 0.001f < required)
                {
                    return true;
                }

                state.ReadyManureCycles = Mathf.Max(
                    0,
                    state.ReadyManureCycles - 1);
                completed = itemRuntime.SpawnItemAt(
                    ManureItemId.Value,
                    1,
                    pen.centerPos,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int manureSpawned)
                    && manureSpawned > 0;
                SetStatus(
                    state,
                    completed
                        ? AnimalHusbandryStatusCode.ManureCollected
                        : AnimalHusbandryStatusCode.ManureStorageUnavailable);
                if (completed)
                {
                    ResetPendingWork(state);
                }
                return completed;

            case AnimalHusbandryWorkKind.Slaughter:
                state.PendingWorkCompleted = Mathf.Min(
                    required,
                    state.PendingWorkCompleted + amount);
                if (state.PendingWorkCompleted + 0.001f < required)
                {
                    return true;
                }

                WildlifeActor actor = FindActor(state.AnimalId);
                if (actor == null || !actor.IsAlive)
                {
                    return false;
                }

                actor.ApplyDamage(actor.CurrentHealth, worker);
                carcassService.SpawnCarcass(actor);
                captureRuntime.TryRelease(state.AnimalId.Value, out _);
                wildlifeRuntime.TryRemoveArrival(state.AnimalId.Value);
                RemoveAnimal(state.AnimalId);
                completed = true;
                return true;

            default:
                return false;
        }
    }

    public AnimalPenCompatibilityResult EvaluatePen(BuildingInstanceId penId)
    {
        BuildingInstanceId id = penId;
        List<HusbandryAnimalState> occupants =
            new List<HusbandryAnimalState>();
        Dictionary<string, WildlifeSpeciesDefinition> speciesById =
            new Dictionary<string, WildlifeSpeciesDefinition>(
                StringComparer.Ordinal);
        Dictionary<string, int> speciesCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        bool hasPlantEater = false;
        bool hasMeatEater = false;
        foreach (HusbandryAnimalState state in animals.Values)
        {
            if (!state.PenId.Equals(id))
            {
                continue;
            }

            occupants.Add(state);
            if (!TryGetSpecies(state, out WildlifeSpeciesDefinition species))
            {
                continue;
            }

            string speciesId = state.SpeciesId.Value;
            speciesById[speciesId] = species;
            speciesCounts.TryGetValue(speciesId, out int count);
            speciesCounts[speciesId] = count + 1;
            hasPlantEater |= species.Diet == WildlifeDietType.Herbivore;
            hasMeatEater |= species.Diet == WildlifeDietType.Carnivore;
        }

        List<AnimalPenCompatibilityIssue> issues =
            new List<AnimalPenCompatibilityIssue>();
        AnimalPenPolicyData policy = GetPolicyInternal(id);
        float weightedSeverity = 0f;

        if (occupants.Count > policy.maximumAnimals)
        {
            float severity = Mathf.Clamp01(
                (occupants.Count - policy.maximumAnimals)
                / (float)Mathf.Max(1, policy.maximumAnimals));
            issues.Add(new AnimalPenCompatibilityIssue(
                AnimalPenCompatibilityIssueKind.Overcrowding,
                severity,
                (occupants.Count - policy.maximumAnimals).ToString()));
            weightedSeverity += severity;
        }

        List<string> speciesIds = speciesById.Keys
            .OrderBy(speciesId => speciesId, StringComparer.Ordinal)
            .ToList();
        for (int leftIndex = 0; leftIndex < speciesIds.Count; leftIndex++)
        {
            string leftSpeciesId = speciesIds[leftIndex];
            WildlifeSpeciesDefinition left = speciesById[leftSpeciesId];
            for (int rightIndex = leftIndex;
                 rightIndex < speciesIds.Count;
                 rightIndex++)
            {
                string rightSpeciesId = speciesIds[rightIndex];
                int leftCount = speciesCounts[leftSpeciesId];
                int rightCount = speciesCounts[rightSpeciesId];
                int pairCount = leftIndex == rightIndex
                    ? leftCount * Mathf.Max(0, leftCount - 1) / 2
                    : leftCount * rightCount;
                if (pairCount <= 0)
                {
                    continue;
                }

                WildlifeSpeciesDefinition right = speciesById[rightSpeciesId];
                bool predatorPrey =
                    left.Diet == WildlifeDietType.Carnivore
                    && right.Diet == WildlifeDietType.Herbivore
                    || right.Diet == WildlifeDietType.Carnivore
                    && left.Diet == WildlifeDietType.Herbivore;
                if (predatorPrey)
                {
                    const float severity = 0.9f;
                    issues.Add(new AnimalPenCompatibilityIssue(
                        AnimalPenCompatibilityIssueKind.PredatorPrey,
                        severity,
                        left.SpeciesId,
                        right.SpeciesId));
                    weightedSeverity += severity * pairCount;
                }

                float aggression = Mathf.Max(left.Aggression, right.Aggression);
                if (aggression >= 0.5f)
                {
                    float severity = Mathf.Clamp01(aggression * 0.6f);
                    issues.Add(new AnimalPenCompatibilityIssue(
                        AnimalPenCompatibilityIssueKind.Aggression,
                        severity,
                        left.SpeciesId,
                        right.SpeciesId));
                    weightedSeverity += severity * pairCount;
                }

                float sizeRatio = Mathf.Max(
                    left.Husbandry.BodySize,
                    right.Husbandry.BodySize)
                    / Mathf.Max(
                        0.1f,
                        Mathf.Min(
                            left.Husbandry.BodySize,
                            right.Husbandry.BodySize));
                if (sizeRatio >= 3f)
                {
                    float severity = Mathf.InverseLerp(3f, 8f, sizeRatio);
                    issues.Add(new AnimalPenCompatibilityIssue(
                        AnimalPenCompatibilityIssueKind.BodySize,
                        severity,
                        left.SpeciesId,
                        right.SpeciesId));
                    weightedSeverity += severity * pairCount;
                }
            }
        }

        if (hasPlantEater && hasMeatEater)
        {
            const float severity = 0.55f;
            issues.Add(new AnimalPenCompatibilityIssue(
                AnimalPenCompatibilityIssueKind.FeedConflict,
                severity));
            weightedSeverity += severity;
        }

        float risk = issues.Count == 0
            ? 0f
            : Mathf.Clamp01(
                issues.Max(issue => issue.Severity)
                + weightedSeverity * 0.08f);
        return new AnimalPenCompatibilityResult
        {
            PenId = id,
            Risk = policy.allowRiskyMixing ? risk * 0.75f : risk,
            Issues = issues
        };
    }

    public DungeonAnimalHusbandrySaveData Capture()
    {
        return AnimalHusbandryStateCodec.Capture(State);
    }

    public AnimalHusbandryRestoreCandidate BuildRestore(
        DungeonAnimalHusbandrySaveData saveData)
    {
        return AnimalHusbandryStateCodec.BuildRestore(
            saveData,
            clock.Time + TickIntervalSeconds,
            id => speciesCatalog.TryGetSpecies(
                id.Value,
                out WildlifeSpeciesDefinition species)
                    ? species.Husbandry.Products
                        .Select(product => new ItemDefinitionId(product.ItemId))
                        .ToArray()
                    : null,
            id => itemCatalog.TryGet(id, out _));
    }

    public void Restore(AnimalHusbandryRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate))).State);
        if (!aggregateRootStore.IsRestoreStaging)
        {
            SynchronizeCapturedAnimals();
        }
    }

    private void EnsureRestoreProjectionCurrent()
    {
        int publishedRevision = aggregateRootStore.PublishedRestoreRevision;
        if (projectedRestoreRevision == publishedRevision)
        {
            return;
        }

        projectedRestoreRevision = publishedRevision;
        SynchronizeCapturedAnimals();
    }

    private void SynchronizeCapturedAnimals()
    {
        captureRuntime.CopyCapturedAnimalReferences(capturedAnimalBuffer);
        SynchronizeCapturedAnimals(capturedAnimalBuffer);
    }

    private void SynchronizeCapturedAnimals(
        IReadOnlyList<CapturedWildlifeState> capturedAnimals)
    {
        synchronizedAnimalIds.Clear();
        int capturedCount = capturedAnimals?.Count ?? 0;
        for (int index = 0; index < capturedCount; index++)
        {
            CapturedWildlifeState captured = capturedAnimals[index];
            if (captured == null
                || captured.escaped
                || captured.transportState != CapturedWildlifeTransportState.Penned)
            {
                continue;
            }

            WildlifeInstanceId id = new(captured.wildlifeId);
            WildlifeSpeciesId speciesId = new(captured.speciesId);
            BuildingInstanceId penId = new(captured.penId);
            if (!id.IsValid || !speciesId.IsValid || !penId.IsValid
                || !speciesCatalog.TryGetSpecies(speciesId.Value, out _))
            {
                throw new InvalidOperationException(
                    $"Penned wildlife '{captured.wildlifeId}' has an invalid instance, species, or pen reference.");
            }

            synchronizedAnimalIds.Add(id);
            if (!animals.TryGetValue(id, out HusbandryAnimalState state))
            {
                state = CreateAnimalState(captured);
                animals.Add(id, state);
            }

            state.PenId = penId;
            state.SpeciesId = speciesId;
            state.Tamed |= captured.isTamed;
            EnsureProductStates(state);
            GetPolicyInternal(state.PenId);
        }

        staleAnimalIds.Clear();
        foreach (WildlifeInstanceId id in animals.Keys)
        {
            if (!synchronizedAnimalIds.Contains(id))
            {
                staleAnimalIds.Add(id);
            }
        }

        for (int index = 0; index < staleAnimalIds.Count; index++)
        {
            RemoveAnimal(staleAnimalIds[index]);
        }
    }

    private HusbandryAnimalState CreateAnimalState(
        CapturedWildlifeState captured)
    {
        uint hash = StableHash(captured.wildlifeId);
        float ageRatio = 0.25f + ((hash >> 1) % 70) / 100f;
        WildlifeSpeciesId speciesId = new(captured.speciesId);
        BuildingInstanceId penId = new(captured.penId);
        if (!speciesCatalog.TryGetSpecies(
                speciesId.Value,
                out WildlifeSpeciesDefinition species))
        {
            throw new InvalidOperationException(
                $"Captured animal '{captured.wildlifeId}' references unknown authored species '{speciesId.Value}'.");
        }
        WildlifeHusbandryProfile profile = species.Husbandry;
        HusbandryAnimalState state = new HusbandryAnimalState
        {
            AnimalId = new WildlifeInstanceId(captured.wildlifeId),
            SpeciesId = speciesId,
            PenId = penId,
            Sex = (hash & 1) == 0 ? AnimalSex.Female : AnimalSex.Male,
            AgeDays = Mathf.Max(0.1f, profile.AdultAgeDays * ageRatio),
            Tamed = captured.isTamed,
            TamingProgress = captured.isTamed ? 1f : 0f,
            StatusCode = captured.isTamed
                ? AnimalHusbandryStatusCode.TamedAnimal
                : AnimalHusbandryStatusCode.AwaitingTaming
        };
        EnsureProductStates(state);
        return state;
    }

    private void AdvanceAnimals(
        float elapsedDays,
        IReadOnlyList<CapturedWildlifeState> capturedAnimals)
    {
        if (elapsedDays <= 0f)
        {
            return;
        }

        capturedById.Clear();
        int capturedCount = capturedAnimals?.Count ?? 0;
        for (int index = 0; index < capturedCount; index++)
        {
            CapturedWildlifeState captured = capturedAnimals[index];
            if (captured != null
                && !string.IsNullOrWhiteSpace(captured.wildlifeId))
            {
                WildlifeInstanceId animalId = new(captured.wildlifeId);
                if (animalId.IsValid)
                {
                    capturedById[animalId] = captured;
                }
            }
        }

        foreach (List<HusbandryAnimalState> penAnimals in animalsByPen.Values)
        {
            penAnimals.Clear();
        }

        animalIterationBuffer.Clear();
        foreach (HusbandryAnimalState state in animals.Values)
        {
            animalIterationBuffer.Add(state);
            BuildingInstanceId penId = state.PenId;
            if (!animalsByPen.TryGetValue(
                    penId,
                    out List<HusbandryAnimalState> penAnimals))
            {
                penAnimals = new List<HusbandryAnimalState>();
                animalsByPen.Add(penId, penAnimals);
            }

            penAnimals.Add(state);
        }

        compatibilityRiskByPen.Clear();
        for (int index = 0; index < animalIterationBuffer.Count; index++)
        {
            HusbandryAnimalState state = animalIterationBuffer[index];
            if (!TryGetSpecies(state, out WildlifeSpeciesDefinition species))
            {
                continue;
            }

            WildlifeHusbandryProfile profile = species.Husbandry;
            state.AgeDays += elapsedDays;
            state.BreedingCooldownDays = Mathf.Max(
                0f,
                state.BreedingCooldownDays - elapsedDays);
            BuildingInstanceId penId = state.PenId;
            if (!compatibilityRiskByPen.TryGetValue(
                    penId,
                    out float compatibilityRisk))
            {
                animalsByPen.TryGetValue(
                    penId,
                    out List<HusbandryAnimalState> penAnimals);
                compatibilityRisk = policyEvaluator.CalculatePenCompatibilityRisk(
                    penAnimals,
                    GetPolicyInternal(penId));
                compatibilityRiskByPen.Add(penId, compatibilityRisk);
            }
            float comfortMultiplier = Mathf.Lerp(
                1f,
                0.45f,
                compatibilityRisk);
            if (capturedById.TryGetValue(
                    state.AnimalId,
                    out CapturedWildlifeState care))
            {
                comfortMultiplier *= Mathf.Lerp(
                    1f,
                    0.35f,
                    Mathf.Clamp01(care.feedSicknessSeverity / 100f));
            }

            if (state.Tamed && GetGrowthStage(state, profile) == AnimalGrowthStage.Adult)
            {
                AdvanceProducts(
                    state,
                    profile,
                    elapsedDays * comfortMultiplier);
                state.ManureProgressDays += elapsedDays;
                while (state.ManureProgressDays >= profile.ManureIntervalDays)
                {
                    state.ManureProgressDays -= profile.ManureIntervalDays;
                    state.ReadyManureCycles = Mathf.Min(
                        4,
                        state.ReadyManureCycles + 1);
                }
            }

            if (state.Pregnant)
            {
                state.PregnancyProgressDays += elapsedDays * comfortMultiplier;
                if (state.PregnancyProgressDays >= profile.GestationDays)
                {
                    TryCompleteBirth(state, profile);
                }
                continue;
            }

            TryBeginPregnancy(state, profile, compatibilityRisk);
        }
    }

    private void AdvanceProducts(
        HusbandryAnimalState state,
        WildlifeHusbandryProfile profile,
        float elapsedDays)
    {
        IReadOnlyList<WildlifeHusbandryProductDefinition> definitions =
            profile.Products;
        for (int definitionIndex = 0;
             definitionIndex < definitions.Count;
             definitionIndex++)
        {
            WildlifeHusbandryProductDefinition definition =
                definitions[definitionIndex];
            if (definition.FemaleOnly && state.Sex != AnimalSex.Female)
            {
                continue;
            }

            AnimalProductProgressState progress = null;
            for (int progressIndex = 0;
                 progressIndex < state.Products.Count;
                 progressIndex++)
            {
                AnimalProductProgressState candidate =
                    state.Products[progressIndex];
                if (candidate != null
                    && string.Equals(
                        candidate.ItemId.Value,
                        definition.ItemId,
                        StringComparison.Ordinal))
                {
                    progress = candidate;
                    break;
                }
            }

            if (progress == null)
            {
                continue;
            }

            progress.ProgressDays += elapsedDays;
            while (progress.ProgressDays >= definition.IntervalDays)
            {
                progress.ProgressDays -= definition.IntervalDays;
                progress.ReadyCycles = Mathf.Min(4, progress.ReadyCycles + 1);
            }
        }
    }

    private void TryBeginPregnancy(
        HusbandryAnimalState female,
        WildlifeHusbandryProfile profile,
        float compatibilityRisk)
    {
        if (!female.Tamed
            || female.Sex != AnimalSex.Female
            || female.BreedingCooldownDays > 0f
            || compatibilityRisk >= 0.8f
            || GetGrowthStage(female, profile) != AnimalGrowthStage.Adult)
        {
            return;
        }

        AnimalPenPolicyData policy = GetPolicyInternal(female.PenId);
        if (!policy.breedingAllowed
            || !IsAllowedByPolicy(female, profile, policy))
        {
            return;
        }

        HusbandryAnimalState male = null;
        int population = 0;
        if (animalsByPen.TryGetValue(
                female.PenId,
                out List<HusbandryAnimalState> penAnimals))
        {
            population = penAnimals.Count;
            for (int index = 0; index < penAnimals.Count; index++)
            {
                HusbandryAnimalState candidate = penAnimals[index];
                if (candidate.Tamed
                    && candidate.Sex == AnimalSex.Male
                    && candidate.SpeciesId.Equals(female.SpeciesId)
                    && TryGetSpecies(
                        candidate,
                        out WildlifeSpeciesDefinition maleSpecies)
                    && GetGrowthStage(
                        candidate,
                        maleSpecies.Husbandry) == AnimalGrowthStage.Adult)
                {
                    male = candidate;
                    break;
                }
            }
        }

        if (male == null)
        {
            return;
        }

        if (population >= GetEffectivePenCapacity(female.PenId))
        {
            return;
        }

        female.Pregnant = true;
        female.PregnancyProgressDays = 0f;
        female.OtherParentId = male.AnimalId;
        female.BreedingCooldownDays = profile.GestationDays + 2f;
        SetStatus(
            female,
            profile.LaysEggs
                ? AnimalHusbandryStatusCode.Brooding
                : AnimalHusbandryStatusCode.Pregnant);
    }

    private void TryCompleteBirth(
        HusbandryAnimalState mother,
        WildlifeHusbandryProfile profile)
    {
        capturedById.TryGetValue(
            mother.AnimalId,
            out CapturedWildlifeState captured);
        WildlifeActor newborn = null;
        if (captured == null
            || !wildlifeRuntime.TrySpawnDomesticBirth(
                mother.SpeciesId.Value,
                captured.penPosition,
                out newborn,
                out _)
            || !captureRuntime.TryRegisterPenBorn(
                newborn,
                mother.PenId.Value,
                captured.penPosition,
                out _))
        {
            if (newborn != null)
            {
                wildlifeRuntime.TryRemoveArrival(newborn.WildlifeId);
            }
            SetStatus(
                mother,
                AnimalHusbandryStatusCode.BirthWaitingForPenCapacity,
                mother.PenId.Value);
            return;
        }

        HusbandryAnimalState child = new HusbandryAnimalState
        {
            AnimalId = new WildlifeInstanceId(newborn.WildlifeId),
            SpeciesId = mother.SpeciesId,
            PenId = mother.PenId,
            Sex = (StableHash(newborn.WildlifeId) & 1) == 0
                ? AnimalSex.Female
                : AnimalSex.Male,
            AgeDays = 0f,
            Tamed = true,
            TamingProgress = 1f,
            StatusCode = profile.LaysEggs
                ? AnimalHusbandryStatusCode.HatchedJuvenile
                : AnimalHusbandryStatusCode.NewbornJuvenile
        };
        EnsureProductStates(child);
        animals[child.AnimalId] = child;
        mother.Pregnant = false;
        mother.PregnancyProgressDays = 0f;
        mother.OtherParentId = default;
        SetStatus(
            mother,
            profile.LaysEggs
                ? AnimalHusbandryStatusCode.HatchingCompleted
                : AnimalHusbandryStatusCode.BirthCompleted);
    }

    private void EnsureProductStates(HusbandryAnimalState state)
    {
        if (!TryGetSpecies(state, out WildlifeSpeciesDefinition species))
        {
            return;
        }

        state.Products ??= new List<AnimalProductProgressState>();
        IReadOnlyList<WildlifeHusbandryProductDefinition> definitions =
            species.Husbandry.Products;
        for (int definitionIndex = 0;
             definitionIndex < definitions.Count;
             definitionIndex++)
        {
            WildlifeHusbandryProductDefinition definition =
                definitions[definitionIndex];
            bool found = false;
            for (int productIndex = 0;
                 productIndex < state.Products.Count;
                 productIndex++)
            {
                AnimalProductProgressState product =
                    state.Products[productIndex];
                if (product != null
                    && string.Equals(
                        product.ItemId.Value,
                        definition.ItemId,
                        StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                state.Products.Add(
                    new AnimalProductProgressState
                    {
                        ItemId = new ItemDefinitionId(definition.ItemId)
                    });
            }
        }
    }

    private bool IsAllowedByPolicy(
        HusbandryAnimalState state,
        WildlifeHusbandryProfile profile,
        AnimalPenPolicyData policy)
    {
        if (policy.AllowedSpeciesIds.Count > 0
            && !policy.AllowedSpeciesIds.Contains(state.SpeciesId))
        {
            return false;
        }

        if (state.Sex == AnimalSex.Female && !policy.allowFemales
            || state.Sex == AnimalSex.Male && !policy.allowMales)
        {
            return false;
        }

        if (GetGrowthStage(state, profile) == AnimalGrowthStage.Juvenile
            && !policy.allowJuveniles)
        {
            return false;
        }

        if (!TryGetSpecies(state, out WildlifeSpeciesDefinition species))
        {
            return false;
        }

        return species.Diet switch
        {
            WildlifeDietType.Herbivore => policy.allowHerbivores,
            WildlifeDietType.Omnivore => policy.allowOmnivores,
            WildlifeDietType.Carnivore => policy.allowCarnivores,
            WildlifeDietType.Scavenger => policy.allowScavengers,
            _ => false
        };
    }

    private AnimalPenPolicyData GetPolicyInternal(BuildingInstanceId penId)
    {
        if (!penId.IsValid)
        {
            throw new InvalidOperationException(
                "Animal husbandry requires a valid BuildingInstanceId.");
        }
        if (!policies.TryGetValue(penId, out AnimalPenPolicyData policy))
        {
            policy = new AnimalPenPolicyData { PenId = penId };
            policies[penId] = policy;
        }

        return policy;
    }

    private bool TryGetSpecies(
        HusbandryAnimalState state,
        out WildlifeSpeciesDefinition species)
    {
        species = null;
        return state != null
            && speciesCatalog.TryGetSpecies(state.SpeciesId.Value, out species);
    }

    private bool TryGetProductDefinition(
        HusbandryAnimalState state,
        ItemDefinitionId itemId,
        out WildlifeHusbandryProductDefinition definition)
    {
        definition = null;
        if (!TryGetSpecies(state, out WildlifeSpeciesDefinition species))
        {
            return false;
        }

        IReadOnlyList<WildlifeHusbandryProductDefinition> products =
            species.Husbandry.Products;
        for (int index = 0; index < products.Count; index++)
        {
            WildlifeHusbandryProductDefinition candidate = products[index];
            if (string.Equals(
                    candidate.ItemId,
                    itemId.Value,
                    StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }

    private WildlifeActor FindActor(WildlifeInstanceId animalId)
    {
        return wildlifeRuntime.Wildlife.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                actor.WildlifeId,
                animalId.Value,
                StringComparison.Ordinal));
    }

    private bool IsAdult(HusbandryAnimalState state)
    {
        return TryGetSpecies(state, out WildlifeSpeciesDefinition species)
            && GetGrowthStage(state, species.Husbandry) != AnimalGrowthStage.Juvenile;
    }

    bool IAnimalHusbandryCommandState.TryGetMutableAnimal(
        WildlifeInstanceId animalId,
        out HusbandryAnimalState state) =>
        animals.TryGetValue(animalId, out state);

    AnimalPenPolicyData IAnimalHusbandryCommandState.GetMutablePolicy(
        BuildingInstanceId penId) =>
        GetPolicyInternal(penId);

    void IAnimalHusbandryCommandState.StorePolicy(
        AnimalPenPolicyData policy)
    {
        if (policy == null || !policy.PenId.IsValid)
        {
            throw new ArgumentException(
                "Animal husbandry policy requires a valid pen.",
                nameof(policy));
        }
        policies[policy.PenId] = policy;
    }

    void IAnimalHusbandryCommandState.RefreshAutoSlaughterDesignations()
    {
        policyEvaluator.RefreshAutoSlaughterDesignations(
            animals.Values,
            GetPolicyInternal,
            IsAdult);
    }

    private void RemoveAnimal(WildlifeInstanceId animalId)
    {
        animals.Remove(animalId);
        foreach (HusbandryAnimalState remaining in animals.Values)
        {
            if (remaining.OtherParentId.Equals(animalId))
            {
                remaining.OtherParentId = default;
            }
        }
    }

    private float GetRequiredWork(
        HusbandryAnimalState state,
        AnimalHusbandryWorkKind kind,
        BuildingBeastPenAbility ability)
    {
        TryGetSpecies(state, out WildlifeSpeciesDefinition species);
        float difficulty = species?.Husbandry.TamingDifficulty ?? 0.5f;
        float bodySize = species?.Husbandry.BodySize ?? 1f;
        return kind switch
        {
            AnimalHusbandryWorkKind.Tame =>
                ability.tamingWork * (1f + difficulty),
            AnimalHusbandryWorkKind.CollectProduct =>
                ability.productCollectionWork,
            AnimalHusbandryWorkKind.CollectManure =>
                Mathf.Max(4f, ability.productCollectionWork * 0.65f),
            AnimalHusbandryWorkKind.Slaughter =>
                10f + bodySize * 8f,
            _ => 1f
        };
    }

    private static BuildingInstanceId GetPenId(BuildableObject pen)
    {
        return pen == null
            ? default
            : pen.RequirePersistentInstanceId();
    }
}
