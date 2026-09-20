using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

internal sealed class WasteProcessingAggregateState
{
    internal Dictionary<WasteOriginKind, WastePolicyData> Policies { get; } =
        new();
    internal int Version { get; set; }
    internal float NextTickAt { get; set; }
    internal long NextOutcomeSequence { get; set; } = 1L;
    internal WastePolicyOutcomeCommitSaveData PendingPolicyOutcome { get; set; }
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
    private readonly IWastePolicyGameplayOutcomeParticipant outcomeParticipant;

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
        IWastePolicyGameplayOutcomeParticipant outcomeParticipant = null)
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
        this.outcomeParticipant = outcomeParticipant;
        _ = state;
    }

    public int Version => state.Version;

    public IReadOnlyList<WastePolicyData> Policies => policies.Values
        .OrderBy(policy => policy.origin)
        .Select(policy => policy.Clone())
        .ToArray();

    public void Tick()
    {
        if (outcomeParticipant != null
            && state.PendingPolicyOutcome != null)
            TryReconcilePendingPolicyOutcome(out _);
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

        if (outcomeParticipant != null
            && !TryReconcilePendingPolicyOutcome(out string reconcileFailure))
        {
            return new WastePolicyCommandResult(
                false,
                new DomainFailure(
                    FailureCode.WastePolicyUnsupported,
                    "gameplay-outcome-pending",
                    reconcileFailure));
        }

        WastePolicyData before = policies[policy.origin].Clone();
        WastePolicyData after = policy.Clone();
        if (outcomeParticipant == null)
        {
            policies[policy.origin] = after;
            state.Version++;
            return new WastePolicyCommandResult(true, DomainFailure.None);
        }

        long ownerRevision;
        try
        {
            ownerRevision = checked(state.NextOutcomeSequence);
            if (ownerRevision <= 0L || ownerRevision == long.MaxValue)
                throw new OverflowException("Waste-policy outcome sequence exhausted.");
        }
        catch (OverflowException exception)
        {
            return new WastePolicyCommandResult(
                false,
                new DomainFailure(
                    FailureCode.WastePolicyUnsupported,
                    "outcome-sequence",
                    exception.Message));
        }
        string operationId = "waste-policy:"
            + (int)policy.origin
            + ":command:"
            + ownerRevision.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        WastePolicyGameplayOutcomePreview preview = new(
            operationId,
            ownerRevision,
            policy.origin,
            before.disposition,
            before.enabled,
            before.maximumFeedContamination,
            after.disposition,
            after.enabled,
            after.maximumFeedContamination,
            FormatWasteOrigin(policy.origin) + " 정책");
        if (!outcomeParticipant.TryPrepare(
                preview,
                out IPreparedWastePolicyGameplayOutcome prepared,
                out string prepareFailure)
            || prepared == null
            || !prepared.ResultKey.IsValid)
        {
            prepared?.Cancel();
            return new WastePolicyCommandResult(
                false,
                new DomainFailure(
                    FailureCode.WastePolicyUnsupported,
                    "gameplay-outcome-prepare",
                    prepareFailure));
        }

        state.PendingPolicyOutcome = CreatePending(preview, prepared.ResultKey);
        policies[policy.origin] = after;
        state.Version++;
        state.NextOutcomeSequence = checked(ownerRevision + 1L);

        bool commitSucceeded;
        bool canonicalCommitted = false;
        WastePolicyOutcomeAttachment attachment = default;
        string commitFailure = string.Empty;
        try
        {
            commitSucceeded = prepared.TryCommit(
                ownerRevision,
                out attachment,
                out canonicalCommitted,
                out commitFailure);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            commitSucceeded = false;
            commitFailure = "waste-policy-outcome-commit-exception:"
                + exception.Message;
        }
        if (!canonicalCommitted)
        {
            prepared.Cancel();
            policies[policy.origin] = before;
            state.Version--;
            state.NextOutcomeSequence = ownerRevision;
            state.PendingPolicyOutcome = null;
            return new WastePolicyCommandResult(
                false,
                new DomainFailure(
                    FailureCode.WastePolicyUnsupported,
                    "gameplay-outcome-commit",
                    commitFailure));
        }

        PersistCanonicalAttachment(attachment);
        if (attachment.AcknowledgementProven)
            state.PendingPolicyOutcome = null;
        return new WastePolicyCommandResult(
            true,
            DomainFailure.None,
            operationId,
            ownerRevision,
            before,
            after);
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
        return new DungeonWasteProcessingSaveData
        {
            policies = Policies.Select(policy => policy.Clone()).ToList(),
            nextOutcomeSequence = state.NextOutcomeSequence,
            pendingPolicyOutcome = state.PendingPolicyOutcome?.Clone()
        };
    }

    public WasteProcessingRestoreCandidate BuildRestore(
        DungeonWasteProcessingSaveData saveData)
    {
        ValidateSaveData(saveData);
        WastePolicyOutcomeCommitSaveData pendingPolicyOutcome =
            NormalizePendingPolicyOutcome(saveData.pendingPolicyOutcome);
        WasteProcessingAggregateState restored = new()
        {
            Version = state.Version + 1,
            NextTickAt = clock.Time + rules.TickIntervalSeconds,
            NextOutcomeSequence = saveData.nextOutcomeSequence,
            PendingPolicyOutcome = pendingPolicyOutcome?.Clone()
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
            NextTickAt = nextTickAt,
            NextOutcomeSequence = 1L
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
        if (saveData.nextOutcomeSequence <= 0L)
        {
            throw new InvalidOperationException(
                "Waste-processing next outcome sequence must be positive: "
                + saveData.nextOutcomeSequence + ".");
        }
        WastePolicyOutcomeCommitSaveData pendingPolicyOutcome =
            NormalizePendingPolicyOutcome(saveData.pendingPolicyOutcome);
        if (!IsValidPendingPolicyOutcome(
                pendingPolicyOutcome,
                saveData.nextOutcomeSequence))
        {
            throw new InvalidOperationException(
                "Waste-processing pending policy outcome is invalid for next sequence "
                + saveData.nextOutcomeSequence + ".");
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

    private bool TryReconcilePendingPolicyOutcome(out string failureReason)
    {
        failureReason = string.Empty;
        WastePolicyOutcomeCommitSaveData pending = state.PendingPolicyOutcome;
        if (pending == null)
            return true;
        WastePolicyGameplayOutcomePreview preview = PreviewFromPending(pending);
        if (!outcomeParticipant.TryPrepare(
                preview,
                out IPreparedWastePolicyGameplayOutcome prepared,
                out failureReason)
            || prepared == null
            || !prepared.ResultKey.Equals(ExpectedResultKey(pending)))
        {
            prepared?.Cancel();
            return false;
        }
        bool commitSucceeded;
        bool canonicalCommitted = false;
        WastePolicyOutcomeAttachment attachment = default;
        try
        {
            commitSucceeded = prepared.TryCommit(
                pending.ownerRevision,
                out attachment,
                out canonicalCommitted,
                out failureReason);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            failureReason = "waste-policy-outcome-reconcile-exception:"
                + exception.Message;
            return false;
        }
        if (!canonicalCommitted)
            return false;
        PersistCanonicalAttachment(attachment);
        if (attachment.AcknowledgementProven)
            state.PendingPolicyOutcome = null;
        return commitSucceeded && state.PendingPolicyOutcome == null;
    }

    private void PersistCanonicalAttachment(
        in WastePolicyOutcomeAttachment attachment)
    {
        WastePolicyOutcomeCommitSaveData pending = state.PendingPolicyOutcome;
        if (pending == null || !attachment.IsValid)
            return;
        WastePolicyOutcomeKey expected = ExpectedResultKey(pending);
        if (!attachment.ResultKey.Equals(expected))
            throw new InvalidOperationException(
                "Waste-policy gameplay outcome identity does not match its pending owner row.");
        pending.phase = (int)(attachment.AcknowledgementProven
            ? WastePolicyOutcomeCommitPhase.PublishedAcknowledged
            : WastePolicyOutcomeCommitPhase.CanonicalOutcomeCommitted);
        pending.gameplayOutcome = new WastePolicyGameplayOutcomeAttachmentSaveData
        {
            producerId = attachment.ResultKey.ProducerId,
            operationId = attachment.ResultKey.OperationId,
            commitRevision = attachment.ResultKey.CommitRevision,
            localResultIndex = attachment.ResultKey.LocalResultIndex,
            outcomeRunId = attachment.OutcomeRunId,
            outcomeSequence = attachment.OutcomeSequence,
            replayState = attachment.ReplayState,
            canonicalPayloadHash = attachment.CanonicalPayloadHash
        };
    }

    private static WastePolicyOutcomeCommitSaveData CreatePending(
        in WastePolicyGameplayOutcomePreview preview,
        WastePolicyOutcomeKey expected)
    {
        return new WastePolicyOutcomeCommitSaveData
        {
            phase = (int)WastePolicyOutcomeCommitPhase.DomainCommitted,
            operationId = preview.OperationId,
            ownerRevision = preview.OwnerRevision,
            origin = preview.Origin,
            beforeDisposition = preview.BeforeDisposition,
            beforeEnabled = preview.BeforeEnabled,
            beforeMaximumFeedContamination =
                preview.BeforeMaximumFeedContamination,
            afterDisposition = preview.AfterDisposition,
            afterEnabled = preview.AfterEnabled,
            afterMaximumFeedContamination =
                preview.AfterMaximumFeedContamination,
            displayName = preview.DisplayName,
            expectedProducerId = expected.ProducerId,
            expectedOperationId = expected.OperationId,
            expectedCommitRevision = expected.CommitRevision,
            expectedLocalResultIndex = expected.LocalResultIndex
        };
    }

    private static WastePolicyGameplayOutcomePreview PreviewFromPending(
        WastePolicyOutcomeCommitSaveData pending) => new(
        pending.operationId,
        pending.ownerRevision,
        pending.origin,
        pending.beforeDisposition,
        pending.beforeEnabled,
        pending.beforeMaximumFeedContamination,
        pending.afterDisposition,
        pending.afterEnabled,
        pending.afterMaximumFeedContamination,
        pending.displayName);

    private static WastePolicyOutcomeKey ExpectedResultKey(
        WastePolicyOutcomeCommitSaveData pending) => new(
        pending.expectedProducerId,
        pending.expectedOperationId,
        pending.expectedCommitRevision,
        pending.expectedLocalResultIndex);

    private static bool IsValidPendingPolicyOutcome(
        WastePolicyOutcomeCommitSaveData pending,
        long nextOutcomeSequence)
    {
        if (pending == null)
            return true;
        WastePolicyOutcomeCommitPhase phase =
            (WastePolicyOutcomeCommitPhase)pending.phase;
        if (phase is < WastePolicyOutcomeCommitPhase.DomainCommitted
                or > WastePolicyOutcomeCommitPhase.PublishedAcknowledged
            || pending.ownerRevision <= 0L
            || pending.ownerRevision >= nextOutcomeSequence
            || !IsCanonical(pending.operationId)
            || pending.origin is < WasteOriginKind.Plant
                or > WasteOriginKind.Forbidden
            || !Enum.IsDefined(
                typeof(WasteDispositionKind),
                pending.beforeDisposition)
            || !Enum.IsDefined(
                typeof(WasteDispositionKind),
                pending.afterDisposition)
            || !IsFinite(pending.beforeMaximumFeedContamination)
            || !IsFinite(pending.afterMaximumFeedContamination)
            || pending.beforeMaximumFeedContamination < 0f
            || pending.beforeMaximumFeedContamination > 100f
            || pending.afterMaximumFeedContamination < 0f
            || pending.afterMaximumFeedContamination > 100f
            || string.IsNullOrWhiteSpace(pending.displayName)
            || !string.Equals(
                pending.displayName,
                pending.displayName.Trim(),
                StringComparison.Ordinal))
            return false;
        WastePolicyOutcomeKey expected = ExpectedResultKey(pending);
        if (!expected.IsValid
            || !string.Equals(
                expected.OperationId,
                pending.operationId,
                StringComparison.Ordinal)
            || expected.CommitRevision != pending.ownerRevision)
            return false;
        WastePolicyGameplayOutcomeAttachmentSaveData attachment =
            pending.gameplayOutcome;
        if (phase == WastePolicyOutcomeCommitPhase.DomainCommitted)
            return attachment == null;
        if (attachment == null)
            return false;
        WastePolicyOutcomeAttachment restoredAttachment = new(
            new WastePolicyOutcomeKey(
                attachment.producerId,
                attachment.operationId,
                attachment.commitRevision,
                attachment.localResultIndex),
            attachment.outcomeRunId,
            attachment.outcomeSequence,
            attachment.replayState,
            attachment.replayState >= 6,
            attachment.canonicalPayloadHash);
        return restoredAttachment.IsValid
            && restoredAttachment.ResultKey.Equals(expected)
            && (phase != WastePolicyOutcomeCommitPhase.PublishedAcknowledged
                || restoredAttachment.AcknowledgementProven);
    }

    private static WastePolicyOutcomeCommitSaveData
        NormalizePendingPolicyOutcome(
            WastePolicyOutcomeCommitSaveData pending)
    {
        if (pending == null)
            return null;

        // Unity's JsonUtility can inflate an explicitly serialized null nested
        // reference into a field-default shell during a JSON round trip. Accept
        // only that exact shell as the absence of an owner row; any populated
        // phase or identity remains subject to the strict validation below.
        bool emptyAttachment = pending.gameplayOutcome == null
            || pending.gameplayOutcome.commitRevision == 0L
                && pending.gameplayOutcome.localResultIndex == 0
                && pending.gameplayOutcome.outcomeSequence == 0L
                && pending.gameplayOutcome.replayState == 0
                && string.IsNullOrEmpty(pending.gameplayOutcome.producerId)
                && string.IsNullOrEmpty(pending.gameplayOutcome.operationId)
                && string.IsNullOrEmpty(pending.gameplayOutcome.outcomeRunId)
                && string.IsNullOrEmpty(
                    pending.gameplayOutcome.canonicalPayloadHash);
        bool empty = pending.phase == (int)WastePolicyOutcomeCommitPhase.None
            && pending.ownerRevision == 0L
            && pending.origin == WasteOriginKind.Unknown
            && pending.beforeDisposition == default
            && !pending.beforeEnabled
            && pending.beforeMaximumFeedContamination == 0f
            && pending.afterDisposition == default
            && !pending.afterEnabled
            && pending.afterMaximumFeedContamination == 0f
            && pending.expectedCommitRevision == 0L
            && pending.expectedLocalResultIndex == 0
            && string.IsNullOrEmpty(pending.operationId)
            && string.IsNullOrEmpty(pending.displayName)
            && string.IsNullOrEmpty(pending.expectedProducerId)
            && string.IsNullOrEmpty(pending.expectedOperationId)
            && emptyAttachment;
        return empty ? null : pending;
    }

    private static string FormatWasteOrigin(WasteOriginKind origin) => origin switch
    {
        WasteOriginKind.Plant => "식물성 부패물",
        WasteOriginKind.Animal => "동물성 부패물",
        WasteOriginKind.Mixed => "혼합 부패물",
        WasteOriginKind.Forbidden => "금기 부패물",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
    };

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException
        && exception is not StackOverflowException
        && exception is not AccessViolationException;

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
