using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IWildlifeCarcassService
{
    IReadOnlyList<WildlifeCarcassFreshnessSaveData> CaptureFreshness();
    void RestoreFreshness(IEnumerable<WildlifeCarcassFreshnessSaveData> entries);
    bool TryGetFreshness(
        string stackId,
        out float remainingFreshnessSeconds);
    void SpawnCarcass(WildlifeActor target);
    void TickFreshness(float deltaTime);
    bool TryButcherNextCarcass(
        CharacterActor butcher,
        BuildableObject building,
        out int produced,
        out string message);
    bool HasButcherWorkAvailable(BuildableObject building);
    float GetButcherWorkUrgency();
}

public sealed class WildlifeCarcassService : IWildlifeCarcassService
{
    private const float DefaultFreshnessSeconds = 360f;

    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly ICharacterDeprivationRuntime deprivationRuntime;
    private readonly Dictionary<string, WildlifeCarcassFreshnessSaveData> freshnessByStackId =
        new Dictionary<string, WildlifeCarcassFreshnessSaveData>(StringComparer.Ordinal);

    private WorldItemStackSnapshot cachedBestButcherCarcass;
    private int cachedBestButcherCarcassVersion = -1;

    public WildlifeCarcassService(
        IWorldItemStackRuntime itemStackRuntime,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        ICharacterDeprivationRuntime deprivationRuntime = null)
    {
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.deprivationRuntime = deprivationRuntime;
    }

    public IReadOnlyList<WildlifeCarcassFreshnessSaveData> CaptureFreshness()
    {
        return freshnessByStackId.Values
            .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.stackId))
            .Select(Clone)
            .ToArray();
    }

    public void RestoreFreshness(IEnumerable<WildlifeCarcassFreshnessSaveData> entries)
    {
        freshnessByStackId.Clear();
        foreach (WildlifeCarcassFreshnessSaveData entry in entries
                     ?? Enumerable.Empty<WildlifeCarcassFreshnessSaveData>())
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.stackId))
            {
                continue;
            }

            freshnessByStackId[entry.stackId] = Clone(entry);
        }

        InvalidateButcherCache();
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
        CharacterActor butcher,
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

        butcher?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Butcher,
            produced > 0 ? CharacterActivityOutcomes.Completed : CharacterActivityOutcomes.Failed,
            produced > 0
                ? $"{species.DisplayName} 사체 손질을 마쳤다."
                : $"{species.DisplayName} 사체 손질에 실패했다.",
            building,
            reasonCode: "wildlife-butchered",
            quantity: produced,
            bubbleEligible: produced <= 0));
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
        CharacterActor butcher,
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
        CharacterActor butcher,
        WorldItemStackSnapshot carcass,
        Vector2Int outputPosition,
        int produced)
    {
        if (butcher == null)
        {
            return;
        }

        bool sameSpecies = string.Equals(
            butcher.SpeciesTag,
            carcass.SourceSpeciesTag,
            StringComparison.OrdinalIgnoreCase);
        butcher.ApplyMoodFactor(
            "survival:emergency-butchery",
            sameSpecies ? "동족의 사체를 손질함" : "인간형 사체를 손질함",
            sameSpecies ? -16f : -9f,
            900f,
            1);
        butcher.ChangesStat(CharacterCondition.HYGIENE, -18f);
        string sourceName = string.IsNullOrWhiteSpace(carcass.SourceDisplayName)
            ? "이름 모를 자"
            : carcass.SourceDisplayName;
        deprivationRuntime?.RecordTaboo(
            butcher,
            $"{sourceName}의 사체를 비상 도축했다");
        butcher.Progression?.RecordNarrative(
            CharacterNarrativeDomain.Survival,
            "survival/taboo-butchery",
            carcass.SourceCharacterId,
            sameSpecies ? "same-species" : "humanoid",
            produced,
            0);
        deprivationRuntime?.RecordTabooWitnesses(
            butcher,
            outputPosition,
            "인간형 사체의 비상 도축을 목격함",
            sameSpecies ? -10f : -7f);
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
                && !stack.IsReserved
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
            remainingFreshnessSeconds = Mathf.Max(0f, source.remainingFreshnessSeconds)
        };
    }
}
