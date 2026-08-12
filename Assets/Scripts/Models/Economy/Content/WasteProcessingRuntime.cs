using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class WasteProcessingAggregateState
{
    internal Dictionary<WasteOriginKind, WastePolicyData> Policies { get; } =
        new();
    internal int Version { get; set; }
    internal float NextTickAt { get; set; }
}

public sealed class WasteProcessingMaterialDependencies
{
    public WasteProcessingMaterialDependencies(
        IWasteProcessingInventoryPort inventory,
        IResourceEconomyContentCatalog catalog)
    {
        Inventory = inventory
            ?? throw new ArgumentNullException(nameof(inventory));
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IWasteProcessingInventoryPort Inventory { get; }
    public IResourceEconomyContentCatalog Catalog { get; }
}

public sealed class WasteProcessingOperationDependencies
{
    public WasteProcessingOperationDependencies(
        IWasteProcessingProductionPort production,
        IGameClock clock,
        IWasteProcessingRules rules)
    {
        Production = production
            ?? throw new ArgumentNullException(nameof(production));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public IWasteProcessingProductionPort Production { get; }
    public IGameClock Clock { get; }
    public IWasteProcessingRules Rules { get; }
}

public sealed class WasteProcessingRuntime :
    IWasteProcessingQuery,
    IWastePolicyCommand,
    IWasteFeedCommand,
    IWasteProcessingPersistence,
    ITickable
{
    private readonly IWasteProcessingInventoryPort inventory;
    private readonly IWasteProcessingProductionPort production;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IGameClock clock;
    private readonly IWasteProcessingRules rules;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    private WasteProcessingAggregateState state
    {
        get => aggregateRootStore.GetOrCreate(
            () => CreateDefaultState(version: 0, nextTickAt: 0f));
        set => aggregateRootStore.Replace(value);
    }

    private Dictionary<WasteOriginKind, WastePolicyData> policies =>
        state.Policies;

    public WasteProcessingRuntime(
        WasteProcessingMaterialDependencies materials,
        WasteProcessingOperationDependencies operations,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        materials = materials ?? throw new ArgumentNullException(nameof(materials));
        operations = operations ?? throw new ArgumentNullException(nameof(operations));
        inventory = materials.Inventory;
        catalog = materials.Catalog;
        production = operations.Production;
        clock = operations.Clock;
        rules = operations.Rules;
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        _ = state;
    }

    public int Version => state.Version;

    public IReadOnlyList<WastePolicyData> Policies => policies.Values
        .OrderBy(policy => policy.origin)
        .Select(policy => policy.Clone())
        .ToArray();

    public void Tick()
    {
        if (clock.IsPaused
            || clock.DeltaTime <= 0f
            || clock.Time + 0.001f < state.NextTickAt)
        {
            return;
        }

        state.NextTickAt = clock.Time + rules.TickIntervalSeconds;
        EnsureProcessingBills();
    }

    public WastePolicyData GetPolicy(WasteOriginKind origin)
    {
        if (!policies.TryGetValue(origin, out WastePolicyData policy))
        {
            throw new KeyNotFoundException(
                $"No authored waste policy exists for '{origin}'.");
        }
        return policy.Clone();
    }

    public WastePolicyCommandResult SetPolicy(WastePolicyData policy)
    {
        if (policy == null || policy.origin == WasteOriginKind.Unknown)
        {
            return new WastePolicyCommandResult(
                false,
                new DomainFailure(FailureCode.WastePolicyInvalid));
        }
        if (!rules.IsSupported(policy.origin, policy.disposition)
            || !IsFinite(policy.maximumFeedContamination)
            || policy.maximumFeedContamination < 0f
            || policy.maximumFeedContamination >= rules.ToxicThreshold)
        {
            return new WastePolicyCommandResult(
                false,
                new DomainFailure(
                    FailureCode.WastePolicyUnsupported,
                    policy.origin.ToString(),
                    policy.disposition.ToString()));
        }

        policies[policy.origin] = policy.Clone();
        state.Version++;
        return new WastePolicyCommandResult(true, DomainFailure.None);
    }

    public WasteProcessingOverview CaptureOverview()
    {
        WasteProcessingStackSnapshot[] stacks = GetWasteStacks().ToArray();
        return new WasteProcessingOverview
        {
            PlantWaste = Sum(stacks, WasteOriginKind.Plant),
            AnimalWaste = Sum(stacks, WasteOriginKind.Animal),
            MixedWaste = Sum(stacks, WasteOriginKind.Mixed),
            ForbiddenWaste = Sum(stacks, WasteOriginKind.Forbidden),
            ToxicWaste = stacks
                .Where(stack => stack.Contamination >= rules.ToxicThreshold)
                .Sum(stack => stack.Quantity),
            ProcessingBills = production.CountBillsMatching(
                rules.IsWasteRecipe)
        };
    }

    public WasteFeedRequestResult RequestDirectFeed(
        WildlifeDietType diet,
        Vector2Int destinationPosition,
        string destinationId)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            return new WasteFeedRequestResult(
                false,
                string.Empty,
                WasteFeedOutcomeCode.None,
                new DomainFailure(FailureCode.ItemTransferDestinationMissing));
        }
        WasteProcessingStackSnapshot selected = GetWasteStacks()
            .Where(stack => CanFeed(stack, diet)
                && stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                && !stack.Forbidden
                && stack.AvailableQuantity > 0
                && (stack.State == WorldItemStackState.Stored
                    || string.IsNullOrWhiteSpace(stack.DestinationId)))
            .OrderBy(stack => Manhattan(stack.Position, destinationPosition))
            .ThenBy(stack => stack.Contamination)
            .ThenBy(stack => stack.StackId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (selected == null
            || !selected.StackId.IsValid)
        {
            return new WasteFeedRequestResult(
                false,
                string.Empty,
                WasteFeedOutcomeCode.None,
                new DomainFailure(FailureCode.WasteFeedUnavailable));
        }

        bool requested = inventory.TryRequestStackDelivery(
            selected.StackId,
            1,
            destinationPosition,
            destination,
            out int amount,
            out DomainFailure transferFailure);
        if (!requested || amount <= 0)
        {
            return new WasteFeedRequestResult(
                false,
                string.Empty,
                WasteFeedOutcomeCode.None,
                transferFailure.IsFailure
                    ? transferFailure
                    : new DomainFailure(FailureCode.WasteFeedDeliveryFailed));
        }

        return new WasteFeedRequestResult(
            true,
            selected.ItemId,
            WasteFeedOutcomeCode.FeedDeliveryRequested,
            DomainFailure.None);
    }

    public WasteFeedResult ConsumeDirectFeed(
        WildlifeDietType diet,
        string destinationId)
    {
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            return new WasteFeedResult(
                false,
                string.Empty,
                WasteOriginKind.Unknown,
                0f,
                0f,
                0f,
                WasteFeedOutcomeCode.None,
                new DomainFailure(
                    FailureCode.ItemTransferDestinationMissing));
        }
        WasteProcessingStackSnapshot selected = GetWasteStacks()
            .Where(stack => stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destination,
                    StringComparison.Ordinal)
                && CanFeed(stack, diet))
            .OrderBy(stack => stack.Contamination)
            .ThenBy(stack => stack.StackId.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        DomainFailure transferFailure = DomainFailure.None;
        if (selected == null
            || !selected.StackId.IsValid
            || !inventory.TryConsumeStackQuantity(
                selected.StackId,
                1,
                out WasteProcessingStackSnapshot consumed,
                out transferFailure))
        {
            return new WasteFeedResult(
                false,
                string.Empty,
                WasteOriginKind.Unknown,
                0f,
                0f,
                0f,
                WasteFeedOutcomeCode.None,
                transferFailure.IsFailure
                    ? transferFailure
                    : new DomainFailure(FailureCode.WasteFeedBufferUnavailable));
        }

        if (!rules.TryGetFeedValues(
                diet,
                consumed.WasteOrigin,
                out float nutrition,
                out float diseaseChance))
        {
            return new WasteFeedResult(
                false,
                consumed.ItemId,
                consumed.WasteOrigin,
                consumed.Contamination,
                0f,
                0f,
                WasteFeedOutcomeCode.None,
                new DomainFailure(FailureCode.WasteFeedUnavailable));
        }
        return new WasteFeedResult(
            true,
            consumed.ItemId,
            consumed.WasteOrigin,
            consumed.Contamination,
            nutrition,
            diseaseChance,
            WasteFeedOutcomeCode.FeedConsumed,
            DomainFailure.None);
    }

    public DungeonWasteProcessingSaveData Capture()
    {
        return new DungeonWasteProcessingSaveData
        {
            policies = Policies.Select(policy => policy.Clone()).ToList()
        };
    }

    public WasteProcessingRestoreCandidate BuildRestore(
        DungeonWasteProcessingSaveData saveData)
    {
        ValidateSaveData(saveData);
        WasteProcessingAggregateState restored = new()
        {
            Version = state.Version + 1,
            NextTickAt = clock.Time + rules.TickIntervalSeconds
        };
        foreach (WastePolicyData policy in saveData.policies)
        {
            restored.Policies.Add(policy.origin, policy.Clone());
        }
        return new WasteProcessingRestoreCandidate(restored);
    }

    public void Restore(WasteProcessingRestoreCandidate candidate)
    {
        state = (candidate
            ?? throw new ArgumentNullException(nameof(candidate))).State;
    }

    private void EnsureProcessingBills()
    {
        foreach (WastePolicyData policy in policies.Values
                     .Where(policy => policy.enabled
                         && policy.disposition is not (
                             WasteDispositionKind.Store
                             or WasteDispositionKind.DirectFeed)))
        {
            if (!rules.TryGetRecipeId(
                    policy.origin,
                    policy.disposition,
                    out string recipeId)
                || !catalog.TryGetRecipe(recipeId, out ProductionRecipeSO recipe)
                || !HasAvailableWaste(policy.origin))
            {
                continue;
            }

            production.EnsureSingleBill(recipe);
        }
    }

    private IEnumerable<WasteProcessingStackSnapshot> GetWasteStacks()
    {
        foreach (WasteProcessingStackSnapshot stack in inventory.GetAllStacks())
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
            if (!rules.TryGetLegacyWaste(
                    stack.ItemId,
                    out WasteOriginKind origin,
                    out float contamination))
            {
                continue;
            }
            stack.WasteOrigin = origin;
            stack.Contamination = contamination;
            yield return stack;
        }
    }

    private bool CanFeed(
        WasteProcessingStackSnapshot stack,
        WildlifeDietType diet)
    {
        return stack != null
            && policies.TryGetValue(stack.WasteOrigin, out WastePolicyData policy)
            && policy.enabled
            && policy.disposition == WasteDispositionKind.DirectFeed
            && stack.Contamination < rules.ToxicThreshold
            && stack.Contamination <= policy.maximumFeedContamination
            && rules.TryGetFeedValues(
                diet,
                stack.WasteOrigin,
                out _,
                out _);
    }

    private bool HasAvailableWaste(WasteOriginKind origin) =>
        GetWasteStacks().Any(stack => stack.WasteOrigin == origin
            && !stack.Forbidden
            && stack.AvailableQuantity > 0
            && stack.State is WorldItemStackState.Loose
                or WorldItemStackState.Stored);

    private WasteProcessingAggregateState CreateDefaultState(
        int version,
        float nextTickAt)
    {
        WasteProcessingAggregateState created = new()
        {
            Version = version,
            NextTickAt = nextTickAt
        };
        foreach (WasteOriginKind origin in rules.Origins)
        {
            created.Policies.Add(origin, rules.CreateDefaultPolicy(origin));
        }
        return created;
    }

    private void ValidateSaveData(DungeonWasteProcessingSaveData saveData)
    {
        if (saveData == null
            || saveData.version != DungeonWasteProcessingSaveData.CurrentVersion
            || saveData.policies == null
            || saveData.policies.Count != rules.Origins.Count)
        {
            throw new InvalidOperationException(
                "Waste-processing payload has an unsupported version or missing policy set.");
        }
        WasteOriginKind[] expected = rules.Origins.OrderBy(origin => origin).ToArray();
        for (int index = 0; index < expected.Length; index++)
        {
            WastePolicyData policy = saveData.policies[index];
            if (policy == null
                || policy.origin != expected[index]
                || !rules.IsSupported(policy.origin, policy.disposition)
                || !IsFinite(policy.maximumFeedContamination)
                || policy.maximumFeedContamination < 0f
                || policy.maximumFeedContamination >= rules.ToxicThreshold)
            {
                throw new InvalidOperationException(
                    $"Waste-processing policy {index} is invalid or non-canonical.");
            }
        }
    }

    private static int Sum(
        IEnumerable<WasteProcessingStackSnapshot> stacks,
        WasteOriginKind origin) => stacks
            .Where(stack => stack.WasteOrigin == origin)
            .Sum(stack => stack.Quantity);

    private static int Manhattan(Vector2Int left, Vector2Int right) =>
        Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
