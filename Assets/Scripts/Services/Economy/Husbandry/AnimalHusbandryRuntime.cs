using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed class AnimalHusbandryRuntime :
    IAnimalHusbandryRuntime,
    ITickable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("AnimalHusbandryRuntime.Tick");
    private const float SecondsPerGameDay = 180f;
    private const float TickIntervalSeconds = 5f;
    private const string ManureItemId = "resource:manure";

    private readonly IWildlifeCaptureRuntime captureRuntime;
    private readonly IWildlifeRuntime wildlifeRuntime;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWildlifeCarcassService carcassService;
    private readonly IGameClock clock;
    private readonly Dictionary<string, HusbandryAnimalState> animals =
        new Dictionary<string, HusbandryAnimalState>(StringComparer.Ordinal);
    private readonly Dictionary<string, AnimalPenPolicyData> policies =
        new Dictionary<string, AnimalPenPolicyData>(StringComparer.Ordinal);
    private readonly HashSet<string> synchronizedAnimalIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<string> staleAnimalIds = new List<string>();
    private readonly Dictionary<string, CapturedWildlifeState> capturedById =
        new Dictionary<string, CapturedWildlifeState>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<HusbandryAnimalState>> animalsByPen =
        new Dictionary<string, List<HusbandryAnimalState>>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> compatibilityRiskByPen =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly List<HusbandryAnimalState> animalIterationBuffer =
        new List<HusbandryAnimalState>();
    private readonly List<CapturedWildlifeState> capturedAnimalBuffer =
        new List<CapturedWildlifeState>();
    private readonly Dictionary<(string PenId, string SpeciesId), List<HusbandryAnimalState>>
        slaughterGroups =
            new Dictionary<(string PenId, string SpeciesId), List<HusbandryAnimalState>>();
    private readonly List<HusbandryAnimalState> femaleSlaughterCandidates =
        new List<HusbandryAnimalState>();
    private readonly List<HusbandryAnimalState> maleSlaughterCandidates =
        new List<HusbandryAnimalState>();
    private readonly List<HusbandryAnimalState> juvenileSlaughterCandidates =
        new List<HusbandryAnimalState>();
    private static readonly Comparison<HusbandryAnimalState> OldestFirst =
        CompareOldestFirst;
    private float nextTickAt;

    public AnimalHusbandryRuntime(
        IWildlifeCaptureRuntime captureRuntime,
        IWildlifeRuntime wildlifeRuntime,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IWorldItemStackRuntime itemRuntime,
        IWildlifeCarcassService carcassService,
        IGameClock clock)
    {
        this.captureRuntime = captureRuntime
            ?? throw new ArgumentNullException(nameof(captureRuntime));
        this.wildlifeRuntime = wildlifeRuntime
            ?? throw new ArgumentNullException(nameof(wildlifeRuntime));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.itemRuntime = itemRuntime
            ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.carcassService = carcassService
            ?? throw new ArgumentNullException(nameof(carcassService));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public IReadOnlyList<HusbandryAnimalState> Animals =>
        animals.Values
            .OrderBy(state => state.penId, StringComparer.Ordinal)
            .ThenBy(state => state.speciesId, StringComparer.Ordinal)
            .ThenBy(state => state.wildlifeId, StringComparer.Ordinal)
            .Select(state => state.Clone())
            .ToArray();

    public IReadOnlyList<AnimalPenPolicyData> PenPolicies =>
        policies.Values
            .OrderBy(policy => policy.penId, StringComparer.Ordinal)
            .Select(policy => policy.Clone())
            .ToArray();

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
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
            RefreshAutoSlaughterDesignations();
        }
    }

    public bool TryGetAnimal(
        string wildlifeId,
        out HusbandryAnimalState state)
    {
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (animals.TryGetValue(id, out HusbandryAnimalState found))
        {
            state = found.Clone();
            return true;
        }

        state = null;
        return false;
    }

    public AnimalPenPolicyData GetOrCreatePenPolicy(string penId)
    {
        string id = penId?.Trim() ?? string.Empty;
        if (id.Length == 0)
        {
            return new AnimalPenPolicyData();
        }

        if (!policies.TryGetValue(id, out AnimalPenPolicyData policy))
        {
            policy = new AnimalPenPolicyData { penId = id };
            policies.Add(id, policy);
        }

        return policy.Clone();
    }

    public int GetEffectivePenCapacity(string penId)
    {
        AnimalPenPolicyData policy = GetPolicyInternal(penId);
        return captureRuntime.TryGetPenCapacity(penId, out int physicalCapacity)
            ? Mathf.Min(policy.maximumAnimals, physicalCapacity)
            : policy.maximumAnimals;
    }

    public bool SetPenPolicy(
        AnimalPenPolicyData policy,
        out string failureReason)
    {
        failureReason = string.Empty;
        string penId = policy?.penId?.Trim() ?? string.Empty;
        if (penId.Length == 0)
        {
            failureReason = "우리 정책에는 유효한 우리 ID가 필요합니다.";
            return false;
        }

        AnimalPenPolicyData normalized = policy.Clone();
        normalized.penId = penId;
        normalized.maximumAnimals = Mathf.Max(1, normalized.maximumAnimals);
        if (captureRuntime.TryGetPenCapacity(penId, out int physicalCapacity))
        {
            normalized.maximumAnimals = Mathf.Min(
                normalized.maximumAnimals,
                physicalCapacity);
        }
        normalized.adultFemaleLimit = Mathf.Max(0, normalized.adultFemaleLimit);
        normalized.adultMaleLimit = Mathf.Max(0, normalized.adultMaleLimit);
        normalized.juvenileLimit = Mathf.Max(0, normalized.juvenileLimit);
        normalized.minimumBreedingFemales = Mathf.Max(
            0,
            normalized.minimumBreedingFemales);
        normalized.minimumBreedingMales = Mathf.Max(
            0,
            normalized.minimumBreedingMales);
        normalized.allowedSpeciesIds = (normalized.allowedSpeciesIds
                ?? new List<string>())
            .Select(value => value?.Trim() ?? string.Empty)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        policies[penId] = normalized;
        RefreshAutoSlaughterDesignations();
        return true;
    }

    public bool DesignateSlaughter(
        string wildlifeId,
        bool designated,
        out string failureReason)
    {
        failureReason = string.Empty;
        string id = wildlifeId?.Trim() ?? string.Empty;
        if (!animals.TryGetValue(id, out HusbandryAnimalState state))
        {
            failureReason = "축산 개체를 찾을 수 없습니다.";
            return false;
        }

        if (designated && state.pregnant)
        {
            AnimalPenPolicyData policy = GetPolicyInternal(state.penId);
            if (policy.protectPregnant)
            {
                failureReason = "현재 우리 정책이 임신 개체를 보호합니다.";
                return false;
            }
        }

        state.slaughterDesignated = designated;
        if (!designated)
        {
            state.autoSlaughterDesignated = false;
        }
        state.lastStatus = designated ? "도축 지정" : "도축 지정 해제";
        return true;
    }

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
            work = Unavailable("동물 돌봄 능력이 있는 우리가 아닙니다.");
            return false;
        }

        string penId = GetPenId(pen);
        HusbandryAnimalState selected = animals.Values
            .Where(state => string.Equals(
                state.penId,
                penId,
                StringComparison.Ordinal))
            .OrderByDescending(GetWorkPriority)
            .ThenBy(state => state.wildlifeId, StringComparer.Ordinal)
            .FirstOrDefault(state => GetWorkPriority(state) > 0);
        if (selected == null)
        {
            work = Unavailable("현재 돌볼 동물이 없습니다.");
            return false;
        }

        AnimalHusbandryWorkKind kind = ResolveWorkKind(selected);
        float required = GetRequiredWork(selected, kind, ability);
        PreparePendingWork(selected, kind);
        float completed = GetCompletedWork(selected, kind, required);
        work = new AnimalHusbandryWorkSnapshot(
            true,
            selected.wildlifeId,
            kind,
            GetWorkLabel(selected, kind),
            required,
            completed,
            string.Empty);
        return true;
    }

    public bool ApplyWork(
        BuildableObject pen,
        CharacterActor worker,
        string wildlifeId,
        AnimalHusbandryWorkKind kind,
        float amount,
        out bool completed)
    {
        completed = false;
        if (pen == null
            || amount <= 0f
            || !animals.TryGetValue(
                wildlifeId?.Trim() ?? string.Empty,
                out HusbandryAnimalState state)
            || !string.Equals(
                state.penId,
                GetPenId(pen),
                StringComparison.Ordinal))
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
                state.tamingProgress = Mathf.Clamp01(
                    state.tamingProgress + amount / required);
                completed = state.tamingProgress >= 0.999f;
                if (completed)
                {
                    state.tamed = true;
                    state.tamingProgress = 1f;
                    state.lastStatus = "길들임 완료";
                    captureRuntime.TrySetTamed(
                        state.wildlifeId,
                        true,
                        out _);
                    ResetPendingWork(state);
                }
                return true;

            case AnimalHusbandryWorkKind.CollectProduct:
                AnimalProductProgressState product = state.products
                    .FirstOrDefault(item => item != null && item.readyCycles > 0);
                if (product == null
                    || !TryGetProductDefinition(
                        state,
                        product.itemId,
                        out WildlifeHusbandryProductDefinition definition))
                {
                    return false;
                }

                state.pendingWorkCompleted = Mathf.Min(
                    required,
                    state.pendingWorkCompleted + amount);
                if (state.pendingWorkCompleted + 0.001f < required)
                {
                    return true;
                }

                product.readyCycles = Mathf.Max(0, product.readyCycles - 1);
                completed = itemRuntime.SpawnItemAt(
                    definition.ItemId,
                    definition.Amount,
                    pen.centerPos,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                    && spawned > 0;
                state.lastStatus = completed
                    ? $"{definition.ItemId} 산출물 수거"
                    : "산출물을 놓을 수 없음";
                if (completed)
                {
                    ResetPendingWork(state);
                }
                return completed;

            case AnimalHusbandryWorkKind.CollectManure:
                state.pendingWorkCompleted = Mathf.Min(
                    required,
                    state.pendingWorkCompleted + amount);
                if (state.pendingWorkCompleted + 0.001f < required)
                {
                    return true;
                }

                state.readyManureCycles = Mathf.Max(
                    0,
                    state.readyManureCycles - 1);
                completed = itemRuntime.SpawnItemAt(
                    ManureItemId,
                    1,
                    pen.centerPos,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int manureSpawned)
                    && manureSpawned > 0;
                state.lastStatus = completed ? "분뇨 수거" : "분뇨를 놓을 수 없음";
                if (completed)
                {
                    ResetPendingWork(state);
                }
                return completed;

            case AnimalHusbandryWorkKind.Slaughter:
                state.pendingWorkCompleted = Mathf.Min(
                    required,
                    state.pendingWorkCompleted + amount);
                if (state.pendingWorkCompleted + 0.001f < required)
                {
                    return true;
                }

                WildlifeActor actor = FindActor(state.wildlifeId);
                if (actor == null || !actor.IsAlive)
                {
                    return false;
                }

                actor.ApplyDamage(actor.CurrentHealth, worker);
                carcassService.SpawnCarcass(actor);
                captureRuntime.TryRelease(state.wildlifeId, out _);
                wildlifeRuntime.TryRemoveArrival(state.wildlifeId);
                animals.Remove(state.wildlifeId);
                completed = true;
                return true;

            default:
                return false;
        }
    }

    public AnimalPenCompatibilityResult EvaluatePen(string penId)
    {
        string id = penId?.Trim() ?? string.Empty;
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
            if (!string.Equals(state.penId, id, StringComparison.Ordinal))
            {
                continue;
            }

            occupants.Add(state);
            if (!TryGetSpecies(state, out WildlifeSpeciesDefinition species))
            {
                continue;
            }

            string speciesId = state.speciesId ?? string.Empty;
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
                $"수용 정책보다 {occupants.Count - policy.maximumAnimals}마리 많음"));
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
                        $"{left.DisplayName}과 {right.DisplayName}의 포식 위험"));
                    weightedSeverity += severity * pairCount;
                }

                float aggression = Mathf.Max(left.Aggression, right.Aggression);
                if (aggression >= 0.5f)
                {
                    float severity = Mathf.Clamp01(aggression * 0.6f);
                    issues.Add(new AnimalPenCompatibilityIssue(
                        AnimalPenCompatibilityIssueKind.Aggression,
                        severity,
                        $"{left.DisplayName}과 {right.DisplayName}의 공격성 충돌"));
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
                        $"{left.DisplayName}과 {right.DisplayName}의 체급 차이"));
                    weightedSeverity += severity * pairCount;
                }
            }
        }

        if (hasPlantEater && hasMeatEater)
        {
            const float severity = 0.55f;
            issues.Add(new AnimalPenCompatibilityIssue(
                AnimalPenCompatibilityIssueKind.FeedConflict,
                severity,
                "초식과 육식 사료를 같은 우리에서 관리 중"));
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
        return new DungeonAnimalHusbandrySaveData
        {
            animals = animals.Values.Select(state => state.Clone()).ToList(),
            penPolicies = policies.Values.Select(policy => policy.Clone()).ToList()
        };
    }

    public void Restore(DungeonAnimalHusbandrySaveData saveData)
    {
        animals.Clear();
        policies.Clear();
        DungeonAnimalHusbandrySaveData source =
            saveData ?? new DungeonAnimalHusbandrySaveData();
        foreach (HusbandryAnimalState state in source.animals
                     ?? new List<HusbandryAnimalState>())
        {
            string id = state?.wildlifeId?.Trim() ?? string.Empty;
            if (id.Length > 0 && !animals.ContainsKey(id))
            {
                animals.Add(id, state.Clone());
            }
        }

        foreach (AnimalPenPolicyData policy in source.penPolicies
                     ?? new List<AnimalPenPolicyData>())
        {
            string id = policy?.penId?.Trim() ?? string.Empty;
            if (id.Length > 0 && !policies.ContainsKey(id))
            {
                policies.Add(id, policy.Clone());
            }
        }

        SynchronizeCapturedAnimals();
        nextTickAt = clock.Time + TickIntervalSeconds;
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

            string id = captured.wildlifeId?.Trim() ?? string.Empty;
            if (id.Length == 0)
            {
                continue;
            }

            synchronizedAnimalIds.Add(id);
            if (!animals.TryGetValue(id, out HusbandryAnimalState state))
            {
                state = CreateAnimalState(captured);
                animals.Add(id, state);
            }

            state.penId = captured.penId?.Trim() ?? string.Empty;
            state.tamed |= captured.isTamed;
            EnsureProductStates(state);
            GetPolicyInternal(state.penId);
        }

        staleAnimalIds.Clear();
        foreach (string id in animals.Keys)
        {
            if (!synchronizedAnimalIds.Contains(id))
            {
                staleAnimalIds.Add(id);
            }
        }

        for (int index = 0; index < staleAnimalIds.Count; index++)
        {
            animals.Remove(staleAnimalIds[index]);
        }
    }

    private HusbandryAnimalState CreateAnimalState(
        CapturedWildlifeState captured)
    {
        uint hash = StableHash(captured.wildlifeId);
        float ageRatio = 0.25f + ((hash >> 1) % 70) / 100f;
        WildlifeHusbandryProfile profile = speciesCatalog.TryGetSpecies(
                captured.speciesId,
                out WildlifeSpeciesDefinition species)
            ? species.Husbandry
            : WildlifeHusbandryProfile.CreateDefault(0f, 8f);
        HusbandryAnimalState state = new HusbandryAnimalState
        {
            wildlifeId = captured.wildlifeId,
            speciesId = captured.speciesId,
            penId = captured.penId,
            sex = (hash & 1) == 0 ? AnimalSex.Female : AnimalSex.Male,
            ageDays = Mathf.Max(0.1f, profile.AdultAgeDays * ageRatio),
            tamed = captured.isTamed,
            tamingProgress = captured.isTamed ? 1f : 0f,
            lastStatus = captured.isTamed ? "길들인 가축" : "길들이기 대기"
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
                capturedById[captured.wildlifeId] = captured;
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
            string penId = state.penId ?? string.Empty;
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
            state.ageDays += elapsedDays;
            state.breedingCooldownDays = Mathf.Max(
                0f,
                state.breedingCooldownDays - elapsedDays);
            string penId = state.penId ?? string.Empty;
            if (!compatibilityRiskByPen.TryGetValue(
                    penId,
                    out float compatibilityRisk))
            {
                animalsByPen.TryGetValue(
                    penId,
                    out List<HusbandryAnimalState> penAnimals);
                compatibilityRisk = CalculatePenCompatibilityRisk(
                    penId,
                    penAnimals);
                compatibilityRiskByPen.Add(penId, compatibilityRisk);
            }
            float comfortMultiplier = Mathf.Lerp(
                1f,
                0.45f,
                compatibilityRisk);
            if (capturedById.TryGetValue(
                    state.wildlifeId,
                    out CapturedWildlifeState care))
            {
                comfortMultiplier *= Mathf.Lerp(
                    1f,
                    0.35f,
                    Mathf.Clamp01(care.feedSicknessSeverity / 100f));
            }

            if (state.tamed && GetGrowthStage(state, profile) == AnimalGrowthStage.Adult)
            {
                AdvanceProducts(
                    state,
                    profile,
                    elapsedDays * comfortMultiplier);
                state.manureProgressDays += elapsedDays;
                while (state.manureProgressDays >= profile.ManureIntervalDays)
                {
                    state.manureProgressDays -= profile.ManureIntervalDays;
                    state.readyManureCycles = Mathf.Min(
                        4,
                        state.readyManureCycles + 1);
                }
            }

            if (state.pregnant)
            {
                state.pregnancyProgressDays += elapsedDays * comfortMultiplier;
                if (state.pregnancyProgressDays >= profile.GestationDays)
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
            if (definition.FemaleOnly && state.sex != AnimalSex.Female)
            {
                continue;
            }

            AnimalProductProgressState progress = null;
            for (int progressIndex = 0;
                 progressIndex < state.products.Count;
                 progressIndex++)
            {
                AnimalProductProgressState candidate =
                    state.products[progressIndex];
                if (candidate != null
                    && string.Equals(
                        candidate.itemId,
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

            progress.progressDays += elapsedDays;
            while (progress.progressDays >= definition.IntervalDays)
            {
                progress.progressDays -= definition.IntervalDays;
                progress.readyCycles = Mathf.Min(4, progress.readyCycles + 1);
            }
        }
    }

    private void TryBeginPregnancy(
        HusbandryAnimalState female,
        WildlifeHusbandryProfile profile,
        float compatibilityRisk)
    {
        if (!female.tamed
            || female.sex != AnimalSex.Female
            || female.breedingCooldownDays > 0f
            || compatibilityRisk >= 0.8f
            || GetGrowthStage(female, profile) != AnimalGrowthStage.Adult)
        {
            return;
        }

        AnimalPenPolicyData policy = GetPolicyInternal(female.penId);
        if (!policy.breedingAllowed
            || !IsAllowedByPolicy(female, profile, policy))
        {
            return;
        }

        HusbandryAnimalState male = null;
        int population = 0;
        if (animalsByPen.TryGetValue(
                female.penId ?? string.Empty,
                out List<HusbandryAnimalState> penAnimals))
        {
            population = penAnimals.Count;
            for (int index = 0; index < penAnimals.Count; index++)
            {
                HusbandryAnimalState candidate = penAnimals[index];
                if (candidate.tamed
                    && candidate.sex == AnimalSex.Male
                    && string.Equals(
                        candidate.speciesId,
                        female.speciesId,
                        StringComparison.Ordinal)
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

        if (population >= GetEffectivePenCapacity(female.penId))
        {
            return;
        }

        female.pregnant = true;
        female.pregnancyProgressDays = 0f;
        female.otherParentId = male.wildlifeId;
        female.breedingCooldownDays = profile.GestationDays + 2f;
        female.lastStatus = profile.LaysEggs ? "알을 품는 중" : "임신 중";
    }

    private void TryCompleteBirth(
        HusbandryAnimalState mother,
        WildlifeHusbandryProfile profile)
    {
        capturedById.TryGetValue(
            mother.wildlifeId ?? string.Empty,
            out CapturedWildlifeState captured);
        WildlifeActor newborn = null;
        if (captured == null
            || !wildlifeRuntime.TrySpawnDomesticBirth(
                mother.speciesId,
                captured.penPosition,
                out newborn,
                out _)
            || !captureRuntime.TryRegisterPenBorn(
                newborn,
                mother.penId,
                captured.penPosition,
                out _))
        {
            if (newborn != null)
            {
                wildlifeRuntime.TryRemoveArrival(newborn.WildlifeId);
            }
            mother.lastStatus = "새끼를 수용할 자리가 없어 출산 대기";
            return;
        }

        HusbandryAnimalState child = new HusbandryAnimalState
        {
            wildlifeId = newborn.WildlifeId,
            speciesId = mother.speciesId,
            penId = mother.penId,
            sex = (StableHash(newborn.WildlifeId) & 1) == 0
                ? AnimalSex.Female
                : AnimalSex.Male,
            ageDays = 0f,
            tamed = true,
            tamingProgress = 1f,
            lastStatus = profile.LaysEggs ? "부화한 새끼" : "막 태어난 새끼"
        };
        EnsureProductStates(child);
        animals[child.wildlifeId] = child;
        mother.pregnant = false;
        mother.pregnancyProgressDays = 0f;
        mother.otherParentId = string.Empty;
        mother.lastStatus = profile.LaysEggs ? "부화 완료" : "출산 완료";
    }

    private void RefreshAutoSlaughterDesignations()
    {
        foreach (HusbandryAnimalState state in animals.Values)
        {
            if (state.autoSlaughterDesignated)
            {
                state.slaughterDesignated = false;
                state.autoSlaughterDesignated = false;
            }
        }

        foreach (List<HusbandryAnimalState> group in slaughterGroups.Values)
        {
            group.Clear();
        }

        foreach (HusbandryAnimalState state in animals.Values)
        {
            var key = (state.penId ?? string.Empty, state.speciesId ?? string.Empty);
            if (!slaughterGroups.TryGetValue(
                    key,
                    out List<HusbandryAnimalState> group))
            {
                group = new List<HusbandryAnimalState>();
                slaughterGroups.Add(key, group);
            }

            group.Add(state);
        }

        foreach (KeyValuePair<(string PenId, string SpeciesId), List<HusbandryAnimalState>>
                 entry in slaughterGroups)
        {
            List<HusbandryAnimalState> members = entry.Value;
            if (members.Count == 0)
            {
                continue;
            }

            AnimalPenPolicyData policy = GetPolicyInternal(entry.Key.PenId);
            femaleSlaughterCandidates.Clear();
            maleSlaughterCandidates.Clear();
            juvenileSlaughterCandidates.Clear();
            for (int index = 0; index < members.Count; index++)
            {
                HusbandryAnimalState member = members[index];
                if (!IsAdult(member))
                {
                    juvenileSlaughterCandidates.Add(member);
                }
                else if (member.sex == AnimalSex.Female)
                {
                    femaleSlaughterCandidates.Add(member);
                }
                else if (member.sex == AnimalSex.Male)
                {
                    maleSlaughterCandidates.Add(member);
                }
            }

            femaleSlaughterCandidates.Sort(OldestFirst);
            maleSlaughterCandidates.Sort(OldestFirst);
            juvenileSlaughterCandidates.Sort(OldestFirst);
            MarkExcess(
                femaleSlaughterCandidates,
                policy.adultFemaleLimit,
                policy.minimumBreedingFemales,
                policy.protectPregnant);
            MarkExcess(
                maleSlaughterCandidates,
                policy.adultMaleLimit,
                policy.minimumBreedingMales,
                false);
            MarkExcess(
                juvenileSlaughterCandidates,
                policy.juvenileLimit,
                0,
                false);
        }
    }

    private float CalculatePenCompatibilityRisk(
        string penId,
        IReadOnlyList<HusbandryAnimalState> occupants)
    {
        int occupantCount = occupants?.Count ?? 0;
        if (occupantCount == 0)
        {
            return 0f;
        }

        AnimalPenPolicyData policy = GetPolicyInternal(penId);
        float weightedSeverity = 0f;
        float maximumSeverity = 0f;
        bool hasPlantEater = false;
        bool hasMeatEater = false;
        if (occupantCount > policy.maximumAnimals)
        {
            float severity = Mathf.Clamp01(
                (occupantCount - policy.maximumAnimals)
                / (float)Mathf.Max(1, policy.maximumAnimals));
            weightedSeverity += severity;
            maximumSeverity = Mathf.Max(maximumSeverity, severity);
        }

        for (int leftIndex = 0; leftIndex < occupantCount; leftIndex++)
        {
            HusbandryAnimalState leftState = occupants[leftIndex];
            if (!TryGetSpecies(
                    leftState,
                    out WildlifeSpeciesDefinition left))
            {
                continue;
            }

            hasPlantEater |= left.Diet == WildlifeDietType.Herbivore;
            hasMeatEater |= left.Diet == WildlifeDietType.Carnivore;
            for (int rightIndex = leftIndex + 1;
                 rightIndex < occupantCount;
                 rightIndex++)
            {
                if (!TryGetSpecies(
                        occupants[rightIndex],
                        out WildlifeSpeciesDefinition right))
                {
                    continue;
                }

                bool predatorPrey =
                    left.Diet == WildlifeDietType.Carnivore
                    && right.Diet == WildlifeDietType.Herbivore
                    || right.Diet == WildlifeDietType.Carnivore
                    && left.Diet == WildlifeDietType.Herbivore;
                if (predatorPrey)
                {
                    const float severity = 0.9f;
                    weightedSeverity += severity;
                    maximumSeverity = Mathf.Max(maximumSeverity, severity);
                }

                float aggression = Mathf.Max(
                    left.Aggression,
                    right.Aggression);
                if (aggression >= 0.5f)
                {
                    float severity = Mathf.Clamp01(aggression * 0.6f);
                    weightedSeverity += severity;
                    maximumSeverity = Mathf.Max(maximumSeverity, severity);
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
                    weightedSeverity += severity;
                    maximumSeverity = Mathf.Max(maximumSeverity, severity);
                }
            }
        }

        if (hasPlantEater && hasMeatEater)
        {
            const float severity = 0.55f;
            weightedSeverity += severity;
            maximumSeverity = Mathf.Max(maximumSeverity, severity);
        }

        float risk = maximumSeverity <= 0f
            ? 0f
            : Mathf.Clamp01(maximumSeverity + weightedSeverity * 0.08f);
        return policy.allowRiskyMixing ? risk * 0.75f : risk;
    }

    private static int CompareOldestFirst(
        HusbandryAnimalState left,
        HusbandryAnimalState right)
    {
        return (right?.ageDays ?? 0f).CompareTo(left?.ageDays ?? 0f);
    }

    private static void MarkExcess(
        IReadOnlyList<HusbandryAnimalState> candidates,
        int limit,
        int protectedMinimum,
        bool protectPregnant)
    {
        int maximum = Mathf.Max(limit, protectedMinimum);
        int excess = Mathf.Max(0, candidates.Count - maximum);
        for (int index = 0; index < candidates.Count && excess > 0; index++)
        {
            HusbandryAnimalState state = candidates[index];
            if (protectPregnant && state.pregnant)
            {
                continue;
            }

            state.slaughterDesignated = true;
            state.autoSlaughterDesignated = true;
            state.lastStatus = "자동 도축 정책 대상";
            excess--;
        }
    }

    private void EnsureProductStates(HusbandryAnimalState state)
    {
        if (!TryGetSpecies(state, out WildlifeSpeciesDefinition species))
        {
            return;
        }

        state.products ??= new List<AnimalProductProgressState>();
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
                 productIndex < state.products.Count;
                 productIndex++)
            {
                AnimalProductProgressState product =
                    state.products[productIndex];
                if (product != null
                    && string.Equals(
                        product.itemId,
                        definition.ItemId,
                        StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                state.products.Add(
                    new AnimalProductProgressState
                    {
                        itemId = definition.ItemId
                    });
            }
        }
    }

    private bool IsAllowedByPolicy(
        HusbandryAnimalState state,
        WildlifeHusbandryProfile profile,
        AnimalPenPolicyData policy)
    {
        if (policy.allowedSpeciesIds.Count > 0
            && !policy.allowedSpeciesIds.Contains(
                state.speciesId,
                StringComparer.Ordinal))
        {
            return false;
        }

        if (state.sex == AnimalSex.Female && !policy.allowFemales
            || state.sex == AnimalSex.Male && !policy.allowMales)
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

    private AnimalPenPolicyData GetPolicyInternal(string penId)
    {
        string id = penId?.Trim() ?? string.Empty;
        if (!policies.TryGetValue(id, out AnimalPenPolicyData policy))
        {
            policy = new AnimalPenPolicyData { penId = id };
            policies[id] = policy;
        }

        return policy;
    }

    private bool TryGetSpecies(
        HusbandryAnimalState state,
        out WildlifeSpeciesDefinition species)
    {
        return speciesCatalog.TryGetSpecies(state?.speciesId, out species);
    }

    private bool TryGetProductDefinition(
        HusbandryAnimalState state,
        string itemId,
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
                    itemId,
                    StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }

    private WildlifeActor FindActor(string wildlifeId)
    {
        return wildlifeRuntime.Wildlife.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                actor.WildlifeId,
                wildlifeId,
                StringComparison.Ordinal));
    }

    private bool IsAdult(HusbandryAnimalState state)
    {
        return TryGetSpecies(state, out WildlifeSpeciesDefinition species)
            && GetGrowthStage(state, species.Husbandry) != AnimalGrowthStage.Juvenile;
    }

    private static AnimalGrowthStage GetGrowthStage(
        HusbandryAnimalState state,
        WildlifeHusbandryProfile profile)
    {
        if (state.ageDays < profile.AdultAgeDays)
        {
            return AnimalGrowthStage.Juvenile;
        }

        return state.ageDays >= profile.MaximumAgeDays * 0.8f
            ? AnimalGrowthStage.Elder
            : AnimalGrowthStage.Adult;
    }

    private int GetWorkPriority(HusbandryAnimalState state)
    {
        return ResolveWorkKind(state) switch
        {
            AnimalHusbandryWorkKind.Slaughter => 100,
            AnimalHusbandryWorkKind.CollectProduct => 80,
            AnimalHusbandryWorkKind.CollectManure => 70,
            AnimalHusbandryWorkKind.Tame => 60,
            _ => 0
        };
    }

    private static AnimalHusbandryWorkKind ResolveWorkKind(
        HusbandryAnimalState state)
    {
        if (state.slaughterDesignated)
        {
            return AnimalHusbandryWorkKind.Slaughter;
        }

        if (state.products?.Any(product =>
                product != null && product.readyCycles > 0) == true)
        {
            return AnimalHusbandryWorkKind.CollectProduct;
        }

        if (state.readyManureCycles > 0)
        {
            return AnimalHusbandryWorkKind.CollectManure;
        }

        return !state.tamed
            ? AnimalHusbandryWorkKind.Tame
            : AnimalHusbandryWorkKind.None;
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

    private static float GetCompletedWork(
        HusbandryAnimalState state,
        AnimalHusbandryWorkKind kind,
        float required)
    {
        return kind switch
        {
            AnimalHusbandryWorkKind.Tame =>
                state.tamingProgress * required,
            _ => Mathf.Clamp(state.pendingWorkCompleted, 0f, required)
        };
    }

    private static void PreparePendingWork(
        HusbandryAnimalState state,
        AnimalHusbandryWorkKind kind)
    {
        string productId = kind == AnimalHusbandryWorkKind.CollectProduct
            ? state.products?.FirstOrDefault(product =>
                product != null && product.readyCycles > 0)?.itemId ?? string.Empty
            : string.Empty;
        if (state.pendingWorkKind == kind
            && string.Equals(
                state.pendingProductItemId,
                productId,
                StringComparison.Ordinal))
        {
            return;
        }

        state.pendingWorkKind = kind;
        state.pendingProductItemId = productId;
        state.pendingWorkCompleted = 0f;
    }

    private static void ResetPendingWork(HusbandryAnimalState state)
    {
        state.pendingWorkKind = AnimalHusbandryWorkKind.None;
        state.pendingProductItemId = string.Empty;
        state.pendingWorkCompleted = 0f;
    }

    private string GetWorkLabel(
        HusbandryAnimalState state,
        AnimalHusbandryWorkKind kind)
    {
        string name = TryGetSpecies(
            state,
            out WildlifeSpeciesDefinition species)
                ? species.DisplayName
                : state.speciesId;
        return kind switch
        {
            AnimalHusbandryWorkKind.Tame => $"{name} 길들이기",
            AnimalHusbandryWorkKind.CollectProduct => $"{name} 산출물 수거",
            AnimalHusbandryWorkKind.CollectManure => $"{name} 분뇨 수거",
            AnimalHusbandryWorkKind.Slaughter => $"{name} 도축",
            _ => "동물 돌봄"
        };
    }

    private static AnimalHusbandryWorkSnapshot Unavailable(string reason)
    {
        return new AnimalHusbandryWorkSnapshot(
            false,
            string.Empty,
            AnimalHusbandryWorkKind.None,
            "동물 돌봄",
            1f,
            0f,
            reason);
    }

    private static string GetPenId(BuildableObject pen)
    {
        return pen == null
            ? string.Empty
            : $"pen:{pen.id}:{pen.centerPos.x}:{pen.centerPos.y}";
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            string source = value ?? string.Empty;
            for (int index = 0; index < source.Length; index++)
            {
                hash ^= source[index];
                hash *= 16777619;
            }

            return hash;
        }
    }
}
