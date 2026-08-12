using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface IWildlifeCarcassService
{
    IReadOnlyList<WildlifeCarcassFreshnessSaveData> CaptureFreshness();
    void ReplaceFreshnessValidated(
        IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries);
    WildlifeCarcassFreshnessRestoreTransaction ApplyFreshnessRestoreCandidate(
        IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries);
    void RollbackFreshnessRestore(
        WildlifeCarcassFreshnessRestoreTransaction transaction);
    void CompleteFreshnessRestore(
        WildlifeCarcassFreshnessRestoreTransaction transaction);
    bool TryGetFreshness(
        string stackId,
        out float remainingFreshnessSeconds);
    void SpawnCarcass(WildlifeActor target);
    void TickFreshness(float deltaTime);
    bool TryButcherNextCarcass(
        IBuildingVisitorPort butcher,
        BuildableObject building,
        out int produced,
        out string message);
    bool HasButcherWorkAvailable(BuildableObject building);
    float GetButcherWorkUrgency();
}

public sealed class WildlifeCarcassFreshnessRestoreTransaction
{
    private readonly WildlifeCarcassService owner;
    private Action rollback;
    private Action complete;

    internal WildlifeCarcassFreshnessRestoreTransaction(
        WildlifeCarcassService owner,
        Action rollback,
        Action complete)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
        this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
    }

    internal void Rollback(WildlifeCarcassService expectedOwner)
    {
        Action action = RequireActive(expectedOwner, rollback);
        action();
        rollback = null;
        complete = null;
    }

    internal void Complete(WildlifeCarcassService expectedOwner)
    {
        Action action = RequireActive(expectedOwner, complete);
        action();
        complete = null;
        rollback = null;
    }

    private Action RequireActive(
        WildlifeCarcassService expectedOwner,
        Action action)
    {
        if (!ReferenceEquals(owner, expectedOwner) || action == null)
        {
            throw new InvalidOperationException(
                "Wildlife carcass freshness restore transaction has the wrong owner or is already finished.");
        }

        return action;
    }
}

public sealed class WildlifeCarcassService : IWildlifeCarcassService
{
    private const float DefaultFreshnessSeconds = 360f;

    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IGameEventBus gameEventBus;
    private Dictionary<string, WildlifeCarcassFreshnessSaveData> freshnessByStackId =
        new Dictionary<string, WildlifeCarcassFreshnessSaveData>(StringComparer.Ordinal);

    private WorldItemStackSnapshot cachedBestButcherCarcass;
    private int cachedBestButcherCarcassVersion = -1;

    public WildlifeCarcassService(
        IWorldItemStackRuntime itemStackRuntime,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IGameEventBus gameEventBus)
    {
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public IReadOnlyList<WildlifeCarcassFreshnessSaveData> CaptureFreshness()
    {
        Dictionary<string, WorldItemStackSnapshot> stacksById =
            itemStackRuntime.GetAllStacks()
                .Where(stack => stack != null
                    && !string.IsNullOrWhiteSpace(stack.StackId))
                .GroupBy(stack => stack.StackId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.Ordinal);
        return freshnessByStackId.Values
            .Where(entry => entry != null
                && !string.IsNullOrWhiteSpace(entry.stackId)
                && stacksById.TryGetValue(
                    entry.stackId,
                    out WorldItemStackSnapshot stack)
                && WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                    stack.ItemId,
                    out string speciesId)
                && string.Equals(
                    speciesId,
                    entry.speciesId,
                    StringComparison.Ordinal))
            .Select(Clone)
            .ToArray();
    }

    public void ReplaceFreshnessValidated(
        IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries)
    {
        WildlifeCarcassFreshnessRestoreTransaction transaction =
            ApplyFreshnessRestoreCandidate(entries);
        CompleteFreshnessRestore(transaction);
    }

    public WildlifeCarcassFreshnessRestoreTransaction ApplyFreshnessRestoreCandidate(
        IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries)
    {
        if (entries == null)
        {
            throw new ArgumentNullException(nameof(entries));
        }

        Dictionary<string, WildlifeCarcassFreshnessSaveData> restored =
            BuildFreshnessState(entries);
        Dictionary<string, WildlifeCarcassFreshnessSaveData> previous =
            freshnessByStackId;
        WorldItemStackSnapshot previousCachedBest = cachedBestButcherCarcass;
        int previousCachedVersion = cachedBestButcherCarcassVersion;
        WildlifeCarcassFreshnessRestoreTransaction transaction =
            new WildlifeCarcassFreshnessRestoreTransaction(
                this,
                rollback: () =>
                {
                    RequireAppliedFreshness(restored);
                    freshnessByStackId = previous;
                    cachedBestButcherCarcass = previousCachedBest;
                    cachedBestButcherCarcassVersion = previousCachedVersion;
                },
                complete: () => RequireAppliedFreshness(restored));

        freshnessByStackId = restored;
        InvalidateButcherCache();
        return transaction;
    }

    public void RollbackFreshnessRestore(
        WildlifeCarcassFreshnessRestoreTransaction transaction)
    {
        (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Rollback(this);
    }

    public void CompleteFreshnessRestore(
        WildlifeCarcassFreshnessRestoreTransaction transaction)
    {
        (transaction ?? throw new ArgumentNullException(nameof(transaction)))
            .Complete(this);
    }

    private static Dictionary<string, WildlifeCarcassFreshnessSaveData>
        BuildFreshnessState(
            IReadOnlyList<WildlifeCarcassFreshnessSaveData> entries)
    {
        Dictionary<string, WildlifeCarcassFreshnessSaveData> restored =
            new Dictionary<string, WildlifeCarcassFreshnessSaveData>(
                StringComparer.Ordinal);
        foreach (WildlifeCarcassFreshnessSaveData entry in entries)
        {
            if (entry == null
                || !((ItemStackId)entry.stackId).IsValid
                || string.IsNullOrWhiteSpace(entry.speciesId)
                || float.IsNaN(entry.remainingFreshnessSeconds)
                || float.IsInfinity(entry.remainingFreshnessSeconds)
                || entry.remainingFreshnessSeconds < 0f
                || restored.ContainsKey(entry.stackId))
            {
                throw new InvalidOperationException(
                    "Validated wildlife carcass candidate contains invalid state.");
            }

            restored.Add(entry.stackId, Clone(entry));
        }

        return restored;
    }

    private void RequireAppliedFreshness(
        Dictionary<string, WildlifeCarcassFreshnessSaveData> expected)
    {
        if (!ReferenceEquals(freshnessByStackId, expected))
        {
            throw new InvalidOperationException(
                "Wildlife carcass freshness restore transaction is no longer the active state.");
        }
    }

    public bool TryGetFreshness(
        string stackId,
        out float remainingFreshnessSeconds)
    {
        remainingFreshnessSeconds = 0f;
        if (string.IsNullOrWhiteSpace(stackId)
            || !freshnessByStackId.TryGetValue(
                stackId.Trim(),
                out WildlifeCarcassFreshnessSaveData freshness))
        {
            return false;
        }

        remainingFreshnessSeconds = Mathf.Max(
            0f,
            freshness.remainingFreshnessSeconds);
        return true;
    }

    public void SpawnCarcass(WildlifeActor target)
    {
        if (target == null || target.Species == null)
        {
            return;
        }

        Vector2Int position = target.GridPosition;
        string itemId = target.Species.CarcassItemId;
        if (!itemStackRuntime.SpawnItemAt(
                itemId,
                1,
                position,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned <= 0)
        {
            return;
        }

        WorldItemStackSnapshot stack = itemStackRuntime
            .GetStacksAt(position, includeStored: true)
            .LastOrDefault(candidate => candidate != null
                && string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal)
                && !freshnessByStackId.ContainsKey(candidate.StackId));
        if (stack == null)
        {
            return;
        }

        freshnessByStackId[stack.StackId] = new WildlifeCarcassFreshnessSaveData
        {
            stackId = stack.StackId,
            speciesId = target.SpeciesId,
            remainingFreshnessSeconds = DefaultFreshnessSeconds
        };
        InvalidateButcherCache();
    }

    public void TickFreshness(float deltaTime)
    {
        if (freshnessByStackId.Count == 0 || deltaTime <= 0f)
        {
            return;
        }

        List<string> expired = null;
        foreach (WildlifeCarcassFreshnessSaveData entry in freshnessByStackId.Values)
        {
            entry.remainingFreshnessSeconds -= deltaTime;
            if (entry.remainingFreshnessSeconds <= 0f)
            {
                expired ??= new List<string>();
                expired.Add(entry.stackId);
            }
        }

        if (expired == null)
        {
            return;
        }

        IReadOnlyList<WorldItemStackSnapshot> stacks = itemStackRuntime.GetAllStacks();
        foreach (string stackId in expired)
        {
            WorldItemStackSnapshot stack = stacks.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(candidate.StackId, stackId, StringComparison.Ordinal));
            freshnessByStackId.Remove(stackId);
            if (stack == null)
            {
                continue;
            }

            itemStackRuntime.DeleteStack(stackId);
            itemStackRuntime.SpawnItemAt(
                WildlifeItemDefinitions.RotItemId,
                1,
                stack.Position,
                WorldItemStackState.Loose,
                string.Empty,
                out _);
        }

        InvalidateButcherCache();
    }

    public bool TryButcherNextCarcass(
        IBuildingVisitorPort butcher,
        BuildableObject building,
        out int produced,
        out string message)
    {
        produced = 0;
        WorldItemStackSnapshot carcass = FindBestButcherCarcass();
        if (carcass == null)
        {
            message = "손질할 사체가 없습니다.";
            return false;
        }

        if (string.Equals(
                carcass.ItemId,
                DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                StringComparison.Ordinal))
        {
            return TryButcherHumanoidCorpse(
                butcher,
                building,
                carcass,
                out produced,
                out message);
        }

        if (!WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                carcass.ItemId,
                out string speciesId)
            || !speciesCatalog.TryGetSpecies(speciesId, out WildlifeSpeciesDefinition species))
        {
            message = "알 수 없는 사체입니다.";
            return false;
        }

        if (!itemStackRuntime.DeleteStack(carcass.StackId))
        {
            message = "사체 스택을 소비하지 못했습니다.";
            return false;
        }

        freshnessByStackId.Remove(carcass.StackId);
        InvalidateButcherCache();
        Vector2Int outputPosition = building != null ? building.centerPos : carcass.Position;
        foreach (WildlifeButcherYield yieldItem in species.ButcherYields)
        {
            if (yieldItem == null || yieldItem.amount <= 0)
            {
                continue;
            }

            if (itemStackRuntime.SpawnItemAt(
                    yieldItem.itemId,
                    yieldItem.amount,
                    outputPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned))
            {
                produced += spawned;
            }
        }

        butcher?.RecordActivity(
            building,
            new BuildingActivitySnapshot(
                BuildingActivityKinds.Work,
                produced > 0 ? BuildingActivityOutcomes.Completed : BuildingActivityOutcomes.Failed,
                produced > 0
                    ? $"{species.DisplayName} 사체 손질을 마쳤다."
                    : $"{species.DisplayName} 사체 손질에 실패했다.",
                BuiltInWorkTypeIds.Butcher.Value,
                string.Empty,
                "wildlife-butchered",
                0f,
                produced,
                produced <= 0));
        message = produced > 0 ? "손질 완료" : "손질 산출물 없음";
        return produced > 0;
    }

    public bool HasButcherWorkAvailable(BuildableObject building)
    {
        return building != null && FindBestButcherCarcass() != null;
    }

    public float GetButcherWorkUrgency()
    {
        int carcasses = itemStackRuntime.GetAllStacks().Count(stack =>
            stack != null
            && (WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(stack.ItemId, out _)
                || (string.Equals(
                        stack.ItemId,
                        DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                        StringComparison.Ordinal)
                    && stack.EmergencyButcheryAllowed)));
        return Mathf.Clamp(carcasses * 22f, 0f, 90f);
    }

    private bool TryButcherHumanoidCorpse(
        IBuildingVisitorPort butcher,
        BuildableObject building,
        WorldItemStackSnapshot carcass,
        out int produced,
        out string message)
    {
        produced = 0;
        if (carcass == null || !carcass.EmergencyButcheryAllowed)
        {
            message = "비상 도축이 허용되지 않은 사체입니다.";
            return false;
        }

        if (!itemStackRuntime.DeleteStack(carcass.StackId))
        {
            message = "사체를 소비하지 못했습니다.";
            return false;
        }

        freshnessByStackId.Remove(carcass.StackId);
        InvalidateButcherCache();
        Vector2Int outputPosition = building != null ? building.centerPos : carcass.Position;
        if (itemStackRuntime.SpawnItemAt(
                DarkSurvivalItemDefinitions.HumanoidMeatItemId,
                4,
                outputPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int meat))
        {
            produced += meat;
        }

        if (itemStackRuntime.SpawnItemAt(
                DarkSurvivalItemDefinitions.BoneItemId,
                2,
                outputPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int bone))
        {
            produced += bone;
        }

        ApplyHumanoidButcheryConsequences(butcher, carcass, outputPosition, produced);
        message = produced > 0 ? "비상 도축 완료" : "도축 산출물 없음";
        return produced > 0;
    }

    private void ApplyHumanoidButcheryConsequences(
        IBuildingVisitorPort butcherPort,
        WorldItemStackSnapshot carcass,
        Vector2Int outputPosition,
        int produced)
    {
        if (!CharacterBuildingVisitorAdapter.TryGetActor(
                butcherPort,
                out CharacterActor butcher))
        {
            return;
        }

        bool sameSpecies = string.Equals(
            butcher.SpeciesTag,
            carcass.SourceSpeciesTag,
            StringComparison.OrdinalIgnoreCase);
        butcher.ApplyMoodFactor(
            "mood:butchery-guilt",
            sameSpecies ? "동족의 사체를 손질함" : "인간형 사체를 손질함",
            sameSpecies ? -16f : -9f,
            900f,
            1);
        butcher.ChangesStat(CharacterCondition.HYGIENE, -18f);
        string sourceName = string.IsNullOrWhiteSpace(carcass.SourceDisplayName)
            ? "이름 모를 자"
            : carcass.SourceDisplayName;
        gameEventBus.Publish(new CharacterTabooIncidentEvent<CharacterActor>(
            butcher,
            outputPosition,
            $"{sourceName}의 사체를 비상 도축했다",
            "인간형 사체의 비상 도축을 목격함",
            sameSpecies ? -10f : -7f));
        butcher.Progression?.RecordNarrative(
            CharacterNarrativeDomain.Survival,
            "survival/taboo-butchery",
            carcass.SourceCharacterId,
            sameSpecies ? "same-species" : "humanoid",
            produced,
            0);
    }

    private WorldItemStackSnapshot FindBestButcherCarcass()
    {
        if (cachedBestButcherCarcassVersion == itemStackRuntime.ItemStackVersion)
        {
            return cachedBestButcherCarcass;
        }

        cachedBestButcherCarcassVersion = itemStackRuntime.ItemStackVersion;
        cachedBestButcherCarcass = itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null
                && !stack.Forbidden
                && stack.AvailableQuantity > 0
                && (WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(stack.ItemId, out _)
                    || (string.Equals(
                            stack.ItemId,
                            DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
                            StringComparison.Ordinal)
                        && stack.EmergencyButcheryAllowed))
                && (stack.State == WorldItemStackState.Stored
                    || stack.State == WorldItemStackState.Loose
                    || stack.State == WorldItemStackState.FacilityBuffer))
            .OrderBy(stack => stack.State == WorldItemStackState.Stored ? 0 : 1)
            .ThenBy(stack => freshnessByStackId.TryGetValue(
                    stack.StackId,
                    out WildlifeCarcassFreshnessSaveData freshness)
                ? freshness.remainingFreshnessSeconds
                : DefaultFreshnessSeconds)
            .FirstOrDefault();
        return cachedBestButcherCarcass;
    }

    private void InvalidateButcherCache()
    {
        cachedBestButcherCarcass = null;
        cachedBestButcherCarcassVersion = -1;
    }

    private static WildlifeCarcassFreshnessSaveData Clone(
        WildlifeCarcassFreshnessSaveData source)
    {
        return new WildlifeCarcassFreshnessSaveData
        {
            stackId = source.stackId,
            speciesId = source.speciesId,
            remainingFreshnessSeconds = source.remainingFreshnessSeconds
        };
    }
}
