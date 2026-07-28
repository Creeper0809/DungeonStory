using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class WasteProcessingRuntime :
    IWasteProcessingRuntime,
    ITickable
{
    private const float TickIntervalSeconds = 10f;
    private const float ToxicThreshold = 80f;

    private readonly IWorldItemStackRuntime items;
    private readonly IProductionBillRuntime production;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IGameClock clock;
    private readonly Dictionary<WasteOriginKind, WastePolicyData> policies =
        new Dictionary<WasteOriginKind, WastePolicyData>();
    private float nextTickAt;

    public WasteProcessingRuntime(
        IWorldItemStackRuntime items,
        IProductionBillRuntime production,
        IResourceEconomyContentCatalog catalog,
        ICharacterAiWorldRegistry world,
        IGameClock clock)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.production = production
            ?? throw new ArgumentNullException(nameof(production));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        ResetDefaultPolicies();
    }

    public int Version { get; private set; }

    public IReadOnlyList<WastePolicyData> Policies =>
        policies.Values
            .OrderBy(policy => policy.origin)
            .Select(policy => policy.Clone())
            .ToArray();

    public void Tick()
    {
        if (clock.IsPaused
            || clock.DeltaTime <= 0f
            || clock.Time + 0.001f < nextTickAt)
        {
            return;
        }

        nextTickAt = clock.Time + TickIntervalSeconds;
        EnsureProcessingBills();
    }

    public WastePolicyData GetPolicy(WasteOriginKind origin)
    {
        return policies.TryGetValue(origin, out WastePolicyData policy)
            ? policy.Clone()
            : CreateDefaultPolicy(origin);
    }

    public bool SetPolicy(
        WastePolicyData policy,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (policy == null
            || policy.origin == WasteOriginKind.Unknown
            || !IsSupported(policy.origin, policy.disposition))
        {
            failureReason = "이 원산지에는 선택한 폐기 방식을 사용할 수 없습니다.";
            return false;
        }

        WastePolicyData normalized = policy.Clone();
        normalized.maximumFeedContamination = Mathf.Clamp(
            normalized.maximumFeedContamination,
            0f,
            ToxicThreshold - 1f);
        policies[normalized.origin] = normalized;
        Version++;
        return true;
    }

    public WasteProcessingOverview CaptureOverview()
    {
        WorldItemStackSnapshot[] stacks = GetWasteStacks().ToArray();
        return new WasteProcessingOverview
        {
            PlantWaste = Sum(stacks, WasteOriginKind.Plant),
            AnimalWaste = Sum(stacks, WasteOriginKind.Animal),
            MixedWaste = Sum(stacks, WasteOriginKind.Mixed),
            ForbiddenWaste = Sum(stacks, WasteOriginKind.Forbidden),
            ToxicWaste = stacks
                .Where(stack => stack.Contamination >= ToxicThreshold)
                .Sum(stack => stack.Quantity),
            ProcessingBills = world.Buildings
                .Where(building => building != null)
                .Sum(building => production.GetBills(building)
                    .Count(bill => IsWasteRecipe(bill.RecipeId)))
        };
    }

    public bool TryRequestDirectFeed(
        WildlifeDietType diet,
        Vector2Int destinationPosition,
        string destinationId,
        out string itemId,
        out string failureReason)
    {
        itemId = string.Empty;
        failureReason = string.Empty;
        WorldItemStackSnapshot selected = GetWasteStacks()
            .Where(stack => CanFeed(stack, diet)
                && stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                && !stack.Forbidden
                && !stack.IsReserved
                && (stack.State == WorldItemStackState.Stored
                    || string.IsNullOrWhiteSpace(stack.DestinationId)))
            .OrderBy(stack => Manhattan(stack.Position, destinationPosition))
            .ThenBy(stack => stack.Contamination)
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected == null)
        {
            failureReason = "급여 정책에 맞는 안전한 부패물이 없습니다.";
            return false;
        }

        bool requested = items.TryRequestStackDelivery(
            selected.StackId,
            1,
            destinationPosition,
            destinationId,
            out int amount,
            out failureReason);
        if (!requested || amount <= 0)
        {
            return false;
        }

        itemId = selected.ItemId;
        return true;
    }

    public bool TryConsumeDirectFeed(
        WildlifeDietType diet,
        string destinationId,
        out WasteFeedResult result)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        WorldItemStackSnapshot selected = GetWasteStacks()
            .Where(stack => stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destination,
                    StringComparison.Ordinal)
                && CanFeed(stack, diet))
            .OrderBy(stack => stack.Contamination)
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected == null
            || !items.TryConsumeStackQuantity(
                selected.StackId,
                1,
                out WorldItemStackSnapshot consumed))
        {
            result = new WasteFeedResult(
                false,
                string.Empty,
                WasteOriginKind.Unknown,
                0f,
                0f,
                0f,
                "급여할 부패물이 도착하지 않았습니다.");
            return false;
        }

        GetFeedValues(
            diet,
            consumed.WasteOrigin,
            out float nutrition,
            out float diseaseChance);
        result = new WasteFeedResult(
            true,
            consumed.ItemId,
            consumed.WasteOrigin,
            consumed.Contamination,
            nutrition,
            diseaseChance,
            $"{FormatOrigin(consumed.WasteOrigin)} 부패물을 급여했습니다.");
        return true;
    }

    public DungeonWasteProcessingSaveData Capture()
    {
        return new DungeonWasteProcessingSaveData
        {
            policies = Policies.Select(policy => policy.Clone()).ToList()
        };
    }

    public void Restore(DungeonWasteProcessingSaveData saveData)
    {
        ResetDefaultPolicies();
        if (saveData == null
            || saveData.version != DungeonWasteProcessingSaveData.CurrentVersion)
        {
            Version++;
            return;
        }

        foreach (WastePolicyData source in saveData.policies
                     ?? new List<WastePolicyData>())
        {
            if (source == null
                || source.origin == WasteOriginKind.Unknown
                || !IsSupported(source.origin, source.disposition))
            {
                continue;
            }

            WastePolicyData normalized = source.Clone();
            normalized.maximumFeedContamination = Mathf.Clamp(
                normalized.maximumFeedContamination,
                0f,
                ToxicThreshold - 1f);
            policies[normalized.origin] = normalized;
        }

        Version++;
    }

    private void EnsureProcessingBills()
    {
        foreach (WastePolicyData policy in policies.Values
                     .Where(policy => policy.enabled
                         && policy.disposition is not (
                             WasteDispositionKind.Store
                             or WasteDispositionKind.DirectFeed)))
        {
            string recipeId = ResolveRecipeId(
                policy.origin,
                policy.disposition);
            if (recipeId.Length == 0
                || !catalog.TryGetRecipe(recipeId, out ProductionRecipeSO recipe)
                || !HasAvailableWaste(policy.origin))
            {
                continue;
            }

            BuildableObject facility = world.Buildings
                .Where(building => building != null
                    && !building.IsGridDestroyed
                    && building.HasSemanticTag(recipe.FacilityTag)
                    && building.SupportsWork(recipe.WorkTypeId))
                .OrderBy(building => production.GetBills(building).Count)
                .ThenBy(building => building.centerPos.y)
                .ThenBy(building => building.centerPos.x)
                .FirstOrDefault();
            if (facility == null
                || world.Buildings
                    .Where(building => building != null)
                    .SelectMany(building => production.GetBills(building))
                    .Any(bill => string.Equals(
                        bill.RecipeId,
                        recipeId,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            production.AddBill(
                facility,
                recipeId,
                ProductionOrderMode.RepeatCount,
                1);
        }
    }

    private IEnumerable<WorldItemStackSnapshot> GetWasteStacks()
    {
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
        {
            if (stack == null || stack.Quantity <= 0)
            {
                continue;
            }

            if (stack.IsWaste)
            {
                yield return stack;
                continue;
            }

            WasteOriginKind legacy = ResolveLegacyOrigin(stack.ItemId);
            if (legacy == WasteOriginKind.Unknown)
            {
                continue;
            }

            stack.WasteOrigin = legacy;
            stack.Contamination = 50f;
            yield return stack;
        }
    }

    private bool CanFeed(
        WorldItemStackSnapshot stack,
        WildlifeDietType diet)
    {
        if (stack == null
            || !policies.TryGetValue(
                stack.WasteOrigin,
                out WastePolicyData policy)
            || !policy.enabled
            || policy.disposition != WasteDispositionKind.DirectFeed
            || stack.Contamination >= ToxicThreshold
            || stack.Contamination > policy.maximumFeedContamination)
        {
            return false;
        }

        return GetFeedValues(
            diet,
            stack.WasteOrigin,
            out _,
            out _);
    }

    private static bool GetFeedValues(
        WildlifeDietType diet,
        WasteOriginKind origin,
        out float nutrition,
        out float diseaseChance)
    {
        nutrition = 0f;
        diseaseChance = 0f;
        switch (diet)
        {
            case WildlifeDietType.Herbivore
                when origin == WasteOriginKind.Plant:
                nutrition = 0.5f;
                diseaseChance = 0.12f;
                return true;
            case WildlifeDietType.Carnivore
                when origin is WasteOriginKind.Animal or WasteOriginKind.Mixed:
                nutrition = 0.65f;
                diseaseChance = 0.1f;
                return true;
            case WildlifeDietType.Omnivore
                when origin is WasteOriginKind.Plant
                    or WasteOriginKind.Animal
                    or WasteOriginKind.Mixed:
                nutrition = 0.6f;
                diseaseChance = 0.08f;
                return true;
            case WildlifeDietType.Scavenger:
                nutrition = 0.85f;
                diseaseChance = 0.02f;
                return true;
            default:
                return false;
        }
    }

    private bool HasAvailableWaste(WasteOriginKind origin)
    {
        return GetWasteStacks().Any(stack =>
            stack.WasteOrigin == origin
            && !stack.Forbidden
            && !stack.IsReserved
            && stack.State is WorldItemStackState.Loose
                or WorldItemStackState.Stored);
    }

    private void ResetDefaultPolicies()
    {
        policies.Clear();
        foreach (WasteOriginKind origin in new[]
                 {
                     WasteOriginKind.Plant,
                     WasteOriginKind.Animal,
                     WasteOriginKind.Mixed,
                     WasteOriginKind.Forbidden
                 })
        {
            WastePolicyData policy = CreateDefaultPolicy(origin);
            policies.Add(origin, policy);
        }
    }

    private static WastePolicyData CreateDefaultPolicy(WasteOriginKind origin)
    {
        return new WastePolicyData
        {
            origin = origin,
            disposition = origin switch
            {
                WasteOriginKind.Plant => WasteDispositionKind.Compost,
                WasteOriginKind.Animal => WasteDispositionKind.DirectFeed,
                WasteOriginKind.Mixed => WasteDispositionKind.Fuel,
                WasteOriginKind.Forbidden => WasteDispositionKind.Alchemy,
                _ => WasteDispositionKind.Store
            },
            maximumFeedContamination = 79f
        };
    }

    private static bool IsSupported(
        WasteOriginKind origin,
        WasteDispositionKind disposition)
    {
        return disposition switch
        {
            WasteDispositionKind.Store => true,
            WasteDispositionKind.DirectFeed =>
                origin is WasteOriginKind.Plant
                    or WasteOriginKind.Animal
                    or WasteOriginKind.Mixed,
            WasteDispositionKind.Compost =>
                origin is WasteOriginKind.Plant
                    or WasteOriginKind.Animal
                    or WasteOriginKind.Mixed,
            WasteDispositionKind.Fuel =>
                origin is WasteOriginKind.Plant
                    or WasteOriginKind.Animal
                    or WasteOriginKind.Mixed,
            WasteDispositionKind.Alchemy =>
                origin == WasteOriginKind.Forbidden,
            WasteDispositionKind.Incinerate => true,
            _ => false
        };
    }

    private static string ResolveRecipeId(
        WasteOriginKind origin,
        WasteDispositionKind disposition)
    {
        return (origin, disposition) switch
        {
            (WasteOriginKind.Plant, WasteDispositionKind.Compost) =>
                "recipe:compost-plant",
            (WasteOriginKind.Animal, WasteDispositionKind.Compost) =>
                "recipe:compost-animal",
            (WasteOriginKind.Mixed, WasteDispositionKind.Compost) =>
                "recipe:compost-mixed",
            (WasteOriginKind.Plant, WasteDispositionKind.Fuel) =>
                "recipe:low-fuel-plant",
            (WasteOriginKind.Animal, WasteDispositionKind.Fuel) =>
                "recipe:low-fuel-animal",
            (WasteOriginKind.Mixed, WasteDispositionKind.Fuel) =>
                "recipe:low-fuel-rot",
            (WasteOriginKind.Forbidden, WasteDispositionKind.Alchemy) =>
                "recipe:rot-toxin",
            (_, WasteDispositionKind.Incinerate) =>
                $"recipe:incinerate-{FormatOriginId(origin)}",
            _ => string.Empty
        };
    }

    private static bool IsWasteRecipe(string recipeId)
    {
        string id = recipeId?.Trim() ?? string.Empty;
        return id.StartsWith("recipe:compost-", StringComparison.Ordinal)
            || id.StartsWith("recipe:low-fuel-", StringComparison.Ordinal)
            || id.StartsWith("recipe:rot-toxin", StringComparison.Ordinal)
            || id.StartsWith("recipe:incinerate-", StringComparison.Ordinal);
    }

    private static WasteOriginKind ResolveLegacyOrigin(string itemId)
    {
        return itemId?.Trim() switch
        {
            "waste:plant-rot" => WasteOriginKind.Plant,
            "waste:animal-rot" => WasteOriginKind.Animal,
            "waste:mixed-rot" => WasteOriginKind.Mixed,
            "waste:forbidden-rot" => WasteOriginKind.Forbidden,
            "wild:rot" => WasteOriginKind.Mixed,
            _ => WasteOriginKind.Unknown
        };
    }

    private static int Sum(
        IEnumerable<WorldItemStackSnapshot> stacks,
        WasteOriginKind origin)
    {
        return stacks
            .Where(stack => stack.WasteOrigin == origin)
            .Sum(stack => stack.Quantity);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }

    private static string FormatOriginId(WasteOriginKind origin)
    {
        return origin switch
        {
            WasteOriginKind.Plant => "plant",
            WasteOriginKind.Animal => "animal",
            WasteOriginKind.Mixed => "mixed",
            WasteOriginKind.Forbidden => "forbidden",
            _ => "unknown"
        };
    }

    private static string FormatOrigin(WasteOriginKind origin)
    {
        return origin switch
        {
            WasteOriginKind.Plant => "식물성",
            WasteOriginKind.Animal => "동물성",
            WasteOriginKind.Mixed => "혼합",
            WasteOriginKind.Forbidden => "금기",
            _ => "원산지 불명"
        };
    }
}
