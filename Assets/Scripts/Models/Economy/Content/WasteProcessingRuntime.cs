using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;
using VContainer.Unity;

internal sealed class WasteProcessingAggregateState
{
    internal Dictionary<WasteOriginKind, WastePolicyData> Policies { get; } =
        new();
    internal int Version { get; set; }
    internal float NextTickAt { get; set; }
    internal long NextPolicySequence { get; set; } = 1L;
    internal WastePolicyObservationSaveData PendingPolicyChange { get; set; }
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
    IWasteFeedCandidateQuery,
    IWasteProcessingPersistence,
    ITickable
{
    private readonly IWasteProcessingInventoryPort inventory;
    private readonly IWasteProcessingProductionPort production;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IGameClock clock;
    private readonly IWasteProcessingRules rules;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly IDomainFactStagingPublisher observations;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IGameCalendar calendar;
    private bool publishingPolicyObservation;

    private WasteProcessingAggregateState state
    {
        get => aggregateRootStore.GetOrCreate(
            () => CreateDefaultState(version: 0, nextTickAt: 0f));
        set => aggregateRootStore.Replace(value);
    }

    private Dictionary<WasteOriginKind, WastePolicyData> policies =>
        state.Policies;

    [Inject]
    public WasteProcessingRuntime(
        WasteProcessingMaterialDependencies materials,
        WasteProcessingOperationDependencies operations,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IDomainFactStagingPublisher observations = null,
        IDomainFactRecoveryPublisher recovery = null,
        IGameCalendar calendar = null)
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
        this.observations = observations;
        this.recovery = recovery;
        this.calendar = calendar;
        _ = state;
    }

    public int Version => state.Version;

    public IReadOnlyList<WastePolicyData> Policies => policies.Values
        .OrderBy(policy => policy.origin)
        .Select(policy => policy.Clone())
        .ToArray();

    public void Tick()
    {
        AssertObservationIdle();
        if (state.PendingPolicyChange != null)
            TryReconcilePendingPolicyObservation(out _);
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
        AssertObservationIdle();
        if (policy == null || policy.origin == WasteOriginKind.Unknown)
            return new WastePolicyCommandResult(false, new DomainFailure(FailureCode.WastePolicyInvalid));
        if (!policies.ContainsKey(policy.origin) || !rules.IsSupported(policy.origin, policy.disposition)
            || !IsFinite(policy.maximumFeedContamination) || policy.maximumFeedContamination < 0f
            || policy.maximumFeedContamination >= rules.ToxicThreshold)
            return ObservationFailure("policy-unsupported", policy.origin.ToString());
        if (observations == null || recovery == null || calendar == null)
            return ObservationFailure("policy-observer-not-connected", string.Empty);

        var pending = state.PendingPolicyChange;
        if (!TryReconcilePendingPolicyObservation(out string recoveryFailure))
            return ObservationFailure("policy-observation-pending", recoveryFailure);
        // A caller retrying the same still-pending command receives its original result.
        if (pending != null)
        {
            var prior = new WastePolicyChangedFact(pending.fact);
            if (WastePolicyChangedFact.SamePolicy(prior.AfterPolicy(), policy)) return Applied(prior);
        }
        long revision = state.NextPolicySequence;
        if (revision <= 0 || revision == long.MaxValue)
            return ObservationFailure("policy-sequence-exhausted", revision.ToString());
        WastePolicyData before = policies[policy.origin].Clone();
        WastePolicyData after = policy.Clone();
        var fact = new WastePolicyChangedFact(new WastePolicyChangedFactData
        {
            operationId = WastePolicyChangedFact.OperationIdFor(policy.origin, revision),
            ownerRevision = revision, absoluteDay = Math.Max(1, calendar.Day), origin = policy.origin,
            beforeDisposition = before.disposition, beforeEnabled = before.enabled,
            beforeMaximumFeedContamination = before.maximumFeedContamination,
            afterDisposition = after.disposition, afterEnabled = after.enabled,
            afterMaximumFeedContamination = after.maximumFeedContamination
        });
        IDomainFactStage stage = null;
        publishingPolicyObservation = true;
        try
        {
            if (!observations.TryStage(fact, out stage, out string failure) || stage == null)
                return ObservationFailure("policy-observation-prepare", failure);
            if (stage.IsReplay || stage.OwnerRevision != revision
                || !DomainFactValueSyntax.IsText(stage.OperationKey, 4096, false)
                || stage.OperationKey != stage.OperationKey.Trim())
                return ObservationFailure("policy-observation-owner-conflict", stage.OperationKey);

            int versionBefore = state.Version;
            state.PendingPolicyChange = new WastePolicyObservationSaveData
            { fact = fact.Snapshot(), publicationKey = stage.OperationKey };
            policies[policy.origin] = after;
            state.Version++;
            state.NextPolicySequence = checked(revision + 1);
            DomainFactDelivery delivered;
            try { delivered = stage.Commit(); }
            catch (Exception error) when (IsRecoverable(error))
            { delivered = DomainFactDelivery.Uncertain("policy-observation-commit-exception:" + error.Message); }
            if (delivered.Phase == DomainFactDeliveryPhase.Rejected)
            {
                policies[policy.origin] = before;
                state.Version = versionBefore;
                state.NextPolicySequence = revision;
                state.PendingPolicyChange = null;
                return ObservationFailure("policy-observation-rejected", delivered.Detail);
            }
            if (AcceptProof(state.PendingPolicyChange, delivered))
            {
                if (delivered.Completed) state.PendingPolicyChange = null;
                return Applied(fact);
            }
            return ObservationFailure("policy-observation-unconfirmed", delivered.Detail);
        }
        catch (Exception error) when (IsRecoverable(error))
        { return ObservationFailure("policy-observation-failed", error.Message); }
        finally { try { stage?.Cancel(); } finally { publishingPolicyObservation = false; } }
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
        if (!HasExactWildlifeCareAuthority(
                destination,
                destinationPosition))
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

    public bool TryGetDirectFeedCandidate(
        WildlifeDietType diet,
        string destinationId,
        out WasteDirectFeedCandidate candidate,
        out DomainFailure failure)
    {
        candidate = default;
        failure = DomainFailure.None;
        string destination = destinationId?.Trim() ?? string.Empty;
        if (destination.Length == 0)
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferDestinationMissing);
            return false;
        }
        if (!inventory.TryGetExactWildlifeCareDestinationPosition(
                destination,
                out Vector2Int authorityPosition)
            || !HasExactWildlifeCareAuthority(
                destination,
                authorityPosition))
        {
            failure = new DomainFailure(
                FailureCode.ItemTransferDestinationMissing);
            return false;
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
        if (selected == null
            || !selected.StackId.IsValid
            || !rules.TryGetFeedValues(
                diet,
                selected.WasteOrigin,
                out float nutrition,
                out float diseaseChance))
        {
            failure = new DomainFailure(FailureCode.WasteFeedUnavailable);
            return false;
        }
        candidate = new WasteDirectFeedCandidate(
            selected.StackId,
            selected.ItemId,
            selected.WasteOrigin,
            selected.Contamination,
            nutrition,
            diseaseChance);
        return candidate.IsValid;
    }

    private bool HasExactWildlifeCareAuthority(
        string destinationId,
        Vector2Int destinationPosition)
    {
        return inventory.HasExactWildlifeCareDestinationAuthority(
            destinationId,
            destinationPosition);
    }

    public DungeonWasteProcessingSaveData Capture()
    {
        AssertObservationIdle();
        return new DungeonWasteProcessingSaveData
        {
            policies = Policies.Select(policy => policy.Clone()).ToList(),
            nextPolicySequence = state.NextPolicySequence,
            pendingPolicyChanges = state.PendingPolicyChange == null ? new()
                : new() { state.PendingPolicyChange.Clone() }
        };
    }

    public WasteProcessingRestoreCandidate BuildRestore(
        DungeonWasteProcessingSaveData saveData)
    {
        AssertObservationIdle();
        ValidateSaveData(saveData);
        WasteProcessingAggregateState restored = new()
        {
            Version = state.Version + 1,
            NextTickAt = clock.Time + rules.TickIntervalSeconds,
            NextPolicySequence = saveData.nextPolicySequence,
            PendingPolicyChange = saveData.pendingPolicyChanges.SingleOrDefault()?.Clone()
        };
        foreach (WastePolicyData policy in saveData.policies)
        {
            restored.Policies.Add(policy.origin, policy.Clone());
        }
        return new WasteProcessingRestoreCandidate(restored);
    }

    public void Restore(WasteProcessingRestoreCandidate candidate)
    {
        AssertObservationIdle();
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
            NextTickAt = nextTickAt,
            NextPolicySequence = 1L
        };
        foreach (WasteOriginKind origin in rules.Origins)
        {
            created.Policies.Add(origin, rules.CreateDefaultPolicy(origin));
        }
        return created;
    }

    private void ValidateSaveData(DungeonWasteProcessingSaveData saveData)
    {
        if (saveData == null)
        {
            throw new InvalidOperationException(
                "Waste-processing payload is missing.");
        }
        if (saveData.version != DungeonWasteProcessingSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                "Waste-processing payload version is unsupported: "
                + $"actual={saveData.version}; "
                + $"expected={DungeonWasteProcessingSaveData.CurrentVersion}.");
        }
        if (saveData.policies == null
            || saveData.policies.Count != rules.Origins.Count)
        {
            throw new InvalidOperationException(
                "Waste-processing payload has a missing or incomplete policy set: "
                + $"actual={saveData.policies?.Count ?? -1}; "
                + $"expected={rules.Origins.Count}.");
        }
        if (saveData.nextPolicySequence <= 0L || saveData.pendingPolicyChanges == null
            || saveData.pendingPolicyChanges.Count > 1)
            throw new InvalidOperationException("Waste-processing policy observation sequence/list is invalid.");
        foreach (var pending in saveData.pendingPolicyChanges)
        {
            if (pending == null) throw new InvalidOperationException("Waste-policy pending fact is null.");
            pending.Validate(saveData.nextPolicySequence);
            var fact = new WastePolicyChangedFact(pending.fact);
            if (!rules.IsSupported(fact.Origin, fact.BeforeDisposition)
                || !rules.IsSupported(fact.Origin, fact.AfterDisposition)
                || fact.BeforeMaximumFeedContamination >= rules.ToxicThreshold
                || fact.AfterMaximumFeedContamination >= rules.ToxicThreshold
                || !WastePolicyChangedFact.SamePolicy(fact.AfterPolicy(),
                    saveData.policies.SingleOrDefault(policy => policy?.origin == fact.Origin)))
                throw new InvalidOperationException("Waste-policy pending fact conflicts with its applied owner.");
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

    private bool TryReconcilePendingPolicyObservation(out string failure)
    {
        AssertObservationIdle();
        failure = string.Empty;
        var pending = state.PendingPolicyChange;
        if (pending == null) return true;
        if (recovery == null) { failure = "policy-observer-not-connected"; return false; }
        publishingPolicyObservation = true;
        try
        {
            var result = new DomainFactDelivery[1];
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey,
                    new WastePolicyChangedFact(pending.fact)) }, result, out failure)) return false;
            if (!AcceptProof(pending, result[0]))
            { failure = "policy-observation-unconfirmed:" + result[0].Detail; return false; }
            if (!result[0].Completed)
            { failure = "policy-observation-awaiting-delivery:" + result[0].Detail; return false; }
            state.PendingPolicyChange = null;
            return true;
        }
        catch (Exception error) when (IsRecoverable(error))
        { failure = "policy-observation-recovery-exception:" + error.Message; return false; }
        finally { publishingPolicyObservation = false; }
    }

    private static bool AcceptProof(WastePolicyObservationSaveData pending, DomainFactDelivery delivery)
    {
        if (delivery.Phase is not (DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery)
            || delivery.OperationKey != pending.publicationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 4096, false)
            || delivery.PublicationId != delivery.PublicationId.Trim()
            || !WastePolicyObservationSaveData.IsFingerprint(delivery.Fingerprint)
            || pending.publicationId.Length > 0 && (pending.publicationId != delivery.PublicationId
                || pending.publicationFingerprint != delivery.Fingerprint))
            return false;
        pending.publicationId = delivery.PublicationId;
        pending.publicationFingerprint = delivery.Fingerprint;
        return true;
    }

    private static WastePolicyCommandResult Applied(WastePolicyChangedFact fact) => new(true, DomainFailure.None,
        fact.OperationId, fact.OwnerRevision, fact.BeforePolicy(), fact.AfterPolicy());
    private static WastePolicyCommandResult ObservationFailure(string reason, string detail) => new(false,
        new DomainFailure(FailureCode.WastePolicyUnsupported, reason, detail));
    private void AssertObservationIdle()
    {
        if (publishingPolicyObservation)
            throw new InvalidOperationException("waste-policy-observation-in-progress");
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
