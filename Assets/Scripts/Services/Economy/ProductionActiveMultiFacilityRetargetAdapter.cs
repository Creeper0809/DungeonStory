using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Transaction-scoped snapshot of the mutable authorities that must move
/// together when active generic production is retargeted.  The snapshot is
/// not save state; every value comes from, and is restored through, the
/// existing production/physical authorities.
/// </summary>
public sealed class ProductionActiveFacilityRetargetSnapshot
{
    public ProductionActiveFacilityRetargetSnapshot(
        DungeonProductionBillSaveData bills,
        DungeonPhysicalItemSaveData physicalItems,
        IReadOnlyList<HaulDeliveryIntentSaveData> haulIntents)
    {
        Bills = ProductionActiveFacilityRetargetSnapshotProjector.Clone(bills);
        PhysicalItems = ProductionActiveFacilityRetargetSnapshotProjector.Clone(
            physicalItems);
        HaulIntents = (haulIntents ?? Array.Empty<HaulDeliveryIntentSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.operationId, StringComparer.Ordinal)
            .Select(HaulDeliveryIntentRuntime.CloneForProjection)
            .ToArray();
        Fingerprint = ProductionActiveFacilityRetargetSnapshotProjector
            .CaptureFingerprint(this);
    }

    public DungeonProductionBillSaveData Bills { get; }
    public DungeonPhysicalItemSaveData PhysicalItems { get; }
    public IReadOnlyList<HaulDeliveryIntentSaveData> HaulIntents { get; }
    public string Fingerprint { get; }
}

public interface IProductionActiveFacilityRetargetStateStore
{
    bool TryCapture(
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        out ProductionActiveFacilityRetargetSnapshot snapshot,
        out string failureReason);

    bool TryApply(
        ProductionActiveFacilityRetargetSnapshot source,
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
        out ProductionActiveFacilityRetargetSnapshot published,
        out string failureReason);

    bool TryRestore(
        ProductionActiveFacilityRetargetSnapshot source,
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        out string restoredFingerprint,
        out string failureReason);

    bool TryCaptureCurrentFingerprint(
        out string fingerprint,
        out string failureReason);
}

/// <summary>
/// Active bill/WIP/physical-custody participant.  Source ordering is inherited
/// from ProductionFacilityRetargetTransaction.  This adapter performs exactly
/// one authority-set publication and can restore the byte-semantic source
/// snapshot after its own failure or a later participant failure.
/// </summary>
public sealed class ProductionActiveMultiFacilityRetargetAdapter :
    IProductionFacilityRetargetAuthorityAdapter
{
    private const string CanonicalVersion =
        "active-multi-facility-retarget@1";
    private readonly IProductionActiveFacilityRetargetStateStore store;

    public ProductionActiveMultiFacilityRetargetAdapter(
        IProductionActiveFacilityRetargetStateStore store)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public string AdapterId => "active-multi-facility-authority";

    public IReadOnlyList<string> OwnedLifecycleContributorIds { get; } =
        Array.AsReadOnly(new[]
        {
            ProductionFacilityDestructiveDrainParticipantIds
                .GenericProductionBills
        });

    public bool TryStage(
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        string operationId,
        out ProductionFacilityRetargetAuthorityPlan plan,
        out string failureReason)
    {
        plan = null;
        failureReason = string.Empty;
        ProductionFacilityRetargetRequest[] requests = (orderedRequests
                ?? Array.Empty<ProductionFacilityRetargetRequest>())
            .OrderBy(value => value?.SourceFacilityId.Value,
                StringComparer.Ordinal)
            .ToArray();
        if (requests.Length == 0
            || requests.Any(value => value == null)
            || !store.TryCapture(requests, out var source, out failureReason))
        {
            failureReason = "active-retarget-stage-failed:" + failureReason;
            return false;
        }

        State state = new(requests, source);
        plan = ProductionFacilityRetargetAuthorityPlan.Create(
            AdapterId,
            CapturePlanFingerprint("staged", source.Fingerprint, requests, null),
            state);
        failureReason = string.Empty;
        return true;
    }

    public bool TryPublish(
        ProductionFacilityRetargetAuthorityPlan plan,
        IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
        out string publishedFingerprint,
        out string failureReason)
    {
        publishedFingerprint = string.Empty;
        if (!TryState(plan, out State state, out failureReason)
            || state.IsPublished)
        {
            return false;
        }

        ProductionFacilityRetargetBinding[] bindings = (orderedBindings
                ?? Array.Empty<ProductionFacilityRetargetBinding>())
            .OrderBy(value => value?.SourceFacilityId.Value,
                StringComparer.Ordinal)
            .ToArray();
        if (bindings.Length != state.Requests.Count
            || bindings.Where((value, index) => value == null
                || !value.SourceFacilityId.Equals(
                    state.Requests[index].SourceFacilityId)).Any())
        {
            failureReason = "active-retarget-binding-coverage-invalid";
            return false;
        }

        if (!store.TryApply(
                state.Source,
                state.Requests,
                bindings,
                out ProductionActiveFacilityRetargetSnapshot published,
                out failureReason))
        {
            return false;
        }

        state.Bindings = bindings;
        state.Published = published;
        state.IsPublished = true;
        publishedFingerprint = CapturePlanFingerprint(
            "published",
            published.Fingerprint,
            state.Requests,
            bindings);
        state.PublishedPlanFingerprint = publishedFingerprint;
        return true;
    }

    public bool TryRollback(
        ProductionFacilityRetargetAuthorityPlan plan,
        out string rolledBackFingerprint,
        out string failureReason)
    {
        rolledBackFingerprint = string.Empty;
        if (!TryState(plan, out State state, out failureReason)
            || !store.TryRestore(
                state.Source,
                state.Requests,
                out string restored,
                out failureReason)
            || !string.Equals(
                restored,
                state.Source.Fingerprint,
                StringComparison.Ordinal))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "active-retarget-rollback-fingerprint-mismatch"
                : failureReason;
            return false;
        }

        state.Bindings = Array.Empty<ProductionFacilityRetargetBinding>();
        state.Published = null;
        state.IsPublished = false;
        state.PublishedPlanFingerprint = string.Empty;
        rolledBackFingerprint = CapturePlanFingerprint(
            "staged",
            restored,
            state.Requests,
            null);
        return true;
    }

    public bool TryCaptureCurrentFingerprint(
        ProductionFacilityRetargetAuthorityPlan plan,
        out string currentFingerprint,
        out string failureReason)
    {
        currentFingerprint = string.Empty;
        if (!TryState(plan, out State state, out failureReason)
            || !store.TryCaptureCurrentFingerprint(
                out string authorityFingerprint,
                out failureReason))
        {
            return false;
        }

        string expected = state.IsPublished
            ? state.Published?.Fingerprint
            : state.Source.Fingerprint;
        if (!string.Equals(
                authorityFingerprint,
                expected,
                StringComparison.Ordinal))
        {
            failureReason = "active-retarget-authority-drift";
            return false;
        }
        currentFingerprint = CapturePlanFingerprint(
            state.IsPublished ? "published" : "staged",
            authorityFingerprint,
            state.Requests,
            state.IsPublished ? state.Bindings : null);
        return true;
    }

    private static bool TryState(
        ProductionFacilityRetargetAuthorityPlan plan,
        out State state,
        out string failureReason)
    {
        state = plan?.AdapterState as State;
        if (state == null
            || !string.Equals(
                plan.AdapterId,
                "active-multi-facility-authority",
                StringComparison.Ordinal))
        {
            failureReason = "active-retarget-plan-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    private static string CapturePlanFingerprint(
        string phase,
        string authorityFingerprint,
        IReadOnlyList<ProductionFacilityRetargetRequest> requests,
        IReadOnlyList<ProductionFacilityRetargetBinding> bindings)
    {
        StringBuilder canonical = new StringBuilder(CanonicalVersion)
            .Append('|').Append(phase).Append('|')
            .Append(authorityFingerprint).Append('|');
        foreach (ProductionFacilityRetargetRequest request in requests)
            canonical.Append(request.SourceFacilityId.Value).Append(';');
        foreach (ProductionFacilityRetargetBinding binding in bindings
                     ?? Array.Empty<ProductionFacilityRetargetBinding>())
        {
            canonical.Append(binding.SourceFacilityId.Value).Append("->")
                .Append(binding.TargetFacilityId.Value).Append(';');
        }
        return ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            canonical.ToString());
    }

    private sealed class State
    {
        public State(
            IReadOnlyList<ProductionFacilityRetargetRequest> requests,
            ProductionActiveFacilityRetargetSnapshot source)
        {
            Requests = requests;
            Source = source;
        }

        public IReadOnlyList<ProductionFacilityRetargetRequest> Requests { get; }
        public ProductionActiveFacilityRetargetSnapshot Source { get; }
        public ProductionActiveFacilityRetargetSnapshot Published { get; set; }
        public IReadOnlyList<ProductionFacilityRetargetBinding> Bindings { get; set; } =
            Array.Empty<ProductionFacilityRetargetBinding>();
        public bool IsPublished { get; set; }
        public string PublishedPlanFingerprint { get; set; } = string.Empty;
    }
}

/// <summary>
/// Live adapter for the existing production and physical save authorities.
/// Restore is deliberately used as the mutation primitive: its validators
/// rejoin active WIP receipts, input claims, stack identity, reservations and
/// carried haul commitments before the candidate becomes authoritative.
/// </summary>
public sealed class ProductionActiveFacilityRetargetStateStore :
    IProductionActiveFacilityRetargetStateStore
{
    private readonly IProductionBillDetachedFacilityPersistence bills;
    private readonly IWorldItemStackRuntime physicalItems;
    private readonly IHaulDeliveryIntentCommand haulIntents;
    private readonly IProductionAssemblyBridge facilities;

    public ProductionActiveFacilityRetargetStateStore(
        IProductionBillDetachedFacilityPersistence bills,
        IWorldItemStackRuntime physicalItems,
        IHaulDeliveryIntentCommand haulIntents,
        IProductionAssemblyBridge facilities)
    {
        this.bills = bills ?? throw new ArgumentNullException(nameof(bills));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.haulIntents = haulIntents
            ?? throw new ArgumentNullException(nameof(haulIntents));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
    }

    public bool TryCapture(
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        out ProductionActiveFacilityRetargetSnapshot snapshot,
        out string failureReason)
    {
        snapshot = null;
        try
        {
            ProductionActiveFacilityRetargetSnapshot captured = Capture();
            if (!ProductionActiveFacilityRetargetSnapshotProjector.TryValidateScope(
                    captured,
                    orderedRequests,
                    out failureReason))
            {
                return false;
            }
            snapshot = captured;
            return true;
        }
        catch (Exception exception)
        {
            failureReason = "active-retarget-capture-exception:"
                + exception.GetType().Name;
            return false;
        }
    }

    public bool TryApply(
        ProductionActiveFacilityRetargetSnapshot source,
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
        out ProductionActiveFacilityRetargetSnapshot published,
        out string failureReason)
    {
        published = null;
        if (!ProductionActiveFacilityRetargetSnapshotProjector.TryProject(
                source,
                orderedRequests,
                orderedBindings,
                out ProductionActiveFacilityRetargetSnapshot candidate,
                out failureReason))
        {
            return false;
        }

        try
        {
            Restore(candidate, CandidateFacilities(orderedBindings));
            published = Capture();
            if (!string.Equals(
                    published.Fingerprint,
                    candidate.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "active-retarget-published-fingerprint-mismatch");
            }
            return true;
        }
        catch (Exception exception)
        {
            try
            {
                Restore(source, SourceFacilities(orderedRequests));
            }
            catch (Exception rollbackException)
            {
                throw new InvalidOperationException(
                    "Active retarget apply and exact rollback both failed.",
                    new AggregateException(exception, rollbackException));
            }
            failureReason = "active-retarget-apply-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    public bool TryRestore(
        ProductionActiveFacilityRetargetSnapshot source,
        IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
        out string restoredFingerprint,
        out string failureReason)
    {
        restoredFingerprint = string.Empty;
        try
        {
            Restore(source, SourceFacilities(orderedRequests));
            restoredFingerprint = Capture().Fingerprint;
            failureReason = string.Empty;
            return string.Equals(
                restoredFingerprint,
                source.Fingerprint,
                StringComparison.Ordinal);
        }
        catch (Exception exception)
        {
            failureReason = "active-retarget-restore-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    public bool TryCaptureCurrentFingerprint(
        out string fingerprint,
        out string failureReason)
    {
        try
        {
            fingerprint = Capture().Fingerprint;
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            fingerprint = string.Empty;
            failureReason = "active-retarget-current-capture-failed:"
                + exception.GetType().Name;
            return false;
        }
    }

    private ProductionActiveFacilityRetargetSnapshot Capture() => new(
        bills.Capture(),
        physicalItems.Capture(),
        haulIntents.CaptureRuntimeState());

    private void Restore(
        ProductionActiveFacilityRetargetSnapshot snapshot,
        IReadOnlyList<ProductionFacilityHandle> detachedFacilities)
    {
        // Physical stacks first make retiring source-buffer occupancy zero.
        // The bill restore can then atomically replace its claim/profile pairs.
        physicalItems.Restore(
            ProductionActiveFacilityRetargetSnapshotProjector.Clone(
                snapshot.PhysicalItems));
        haulIntents.ReplaceRuntimeState(snapshot.HaulIntents
            .Select(HaulDeliveryIntentRuntime.CloneForProjection)
            .ToArray());
        DungeonProductionBillSaveData billData =
            ProductionActiveFacilityRetargetSnapshotProjector.Clone(
                snapshot.Bills);
        bills.Restore(bills.BuildRestore(billData), detachedFacilities);
    }

    private IReadOnlyList<ProductionFacilityHandle> CandidateFacilities(
        IReadOnlyList<ProductionFacilityRetargetBinding> bindings) =>
        MergeFacilities(
            bindings.Select(value => value.TargetFacility),
            bindings.Select(value => value.SourceFacilityId));

    private IReadOnlyList<ProductionFacilityHandle> SourceFacilities(
        IReadOnlyList<ProductionFacilityRetargetRequest> requests) =>
        MergeFacilities(
            requests.Select(value => value.SourceFacility),
            requests.Select(value => value.SourceFacilityId));

    private IReadOnlyList<ProductionFacilityHandle> MergeFacilities(
        IEnumerable<ProductionFacilityHandle> preferred,
        IEnumerable<BuildingInstanceId> replacedIds)
    {
        HashSet<BuildingInstanceId> replaced = replacedIds.ToHashSet();
        Dictionary<BuildingInstanceId, ProductionFacilityHandle> merged = new();
        foreach (ProductionFacilityHandle facility in preferred
                     .Where(value => value != null)
                     .OrderBy(value => value.InstanceId.Value,
                         StringComparer.Ordinal))
        {
            merged[facility.InstanceId] = facility;
        }
        foreach (ProductionFacilityHandle facility in facilities.Facilities
                     .Where(value => value != null)
                     .OrderBy(value => value.InstanceId.Value,
                         StringComparer.Ordinal))
        {
            if (!replaced.Contains(facility.InstanceId))
                merged.TryAdd(facility.InstanceId, facility);
        }
        return merged.Values
            .OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal)
            .ToArray();
    }
}

public static class ProductionActiveFacilityRetargetSnapshotProjector
{
    private const string CanonicalVersion =
        "active-facility-authority-snapshot@1";

    public static bool TryValidateScope(
        ProductionActiveFacilityRetargetSnapshot source,
        IReadOnlyList<ProductionFacilityRetargetRequest> requests,
        out string failureReason)
    {
        HashSet<string> sourceIds = (requests
                ?? Array.Empty<ProductionFacilityRetargetRequest>())
            .Where(value => value != null)
            .Select(value => value.SourceFacilityId.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (source == null || sourceIds.Count == 0)
        {
            failureReason = "active-retarget-snapshot-invalid";
            return false;
        }

        ProductionBillSaveData[] owned = (source.Bills.bills
                ?? new List<ProductionBillSaveData>())
            .Where(value => value != null
                && sourceIds.Contains(value.buildingInstanceId))
            .ToArray();
        if (owned.Any(HasTerminalOutputAuthority))
        {
            failureReason =
                "active-retarget-terminal-output-must-converge-first";
            return false;
        }
        HashSet<string> sourceOutputDestinations = sourceIds
            .Select(value => ProductionBillRuntime.OutputDestinationPrefix + value)
            .ToHashSet(StringComparer.Ordinal);
        if ((source.PhysicalItems.pendingExactOutputRoutes
                ?? new List<FacilityOutputExactRouteOutboxSaveData>())
                .Any(value => value != null
                    && sourceOutputDestinations.Contains(
                        value.sourceDestinationId))
            || (source.PhysicalItems.pendingProductionCustodyDrains
                ?? new List<ProductionPhysicalCustodyDrainSaveData>())
                .Any(value => value != null
                    && sourceOutputDestinations.Contains(
                        value.sourceDestinationId))
            || (source.PhysicalItems.pendingProductionInputDestinationDrains
                ?? new List<ProductionInputDestinationCustodyDrainSaveData>())
                .Any(value => value != null
                    && sourceIds.Contains(value.facilityId))
            || (source.PhysicalItems.pendingCapacityRoutingDrains
                ?? new List<ProductionCapacityRoutingDrainSaveData>())
                .Any(value => value != null
                    && sourceIds.Contains(value.facilityId)))
        {
            failureReason = "active-retarget-terminal-outbox-open";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public static bool TryProject(
        ProductionActiveFacilityRetargetSnapshot source,
        IReadOnlyList<ProductionFacilityRetargetRequest> requests,
        IReadOnlyList<ProductionFacilityRetargetBinding> bindings,
        out ProductionActiveFacilityRetargetSnapshot candidate,
        out string failureReason)
    {
        candidate = null;
        if (!TryValidateScope(source, requests, out failureReason))
            return false;

        ProductionFacilityRetargetBinding[] ordered = (bindings
                ?? Array.Empty<ProductionFacilityRetargetBinding>())
            .OrderBy(value => value?.SourceFacilityId.Value,
                StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0 || ordered.Any(value => value == null))
        {
            failureReason = "active-retarget-projection-binding-invalid";
            return false;
        }

        Dictionary<string, ProductionFacilityRetargetBinding> map = ordered
            .ToDictionary(value => value.SourceFacilityId.Value,
                StringComparer.Ordinal);
        DungeonProductionBillSaveData projectedBills = Clone(source.Bills);
        Dictionary<string, ProductionFacilityRetargetBinding> billTargets = new(
            StringComparer.Ordinal);
        foreach (ProductionBillSaveData bill in projectedBills.bills
                     ?? new List<ProductionBillSaveData>())
        {
            if (bill == null
                || !map.TryGetValue(
                    bill.buildingInstanceId,
                    out ProductionFacilityRetargetBinding binding))
            {
                continue;
            }
            bill.buildingInstanceId = binding.TargetFacilityId.Value;
            bill.outputDestinationId = ProductionBillRuntime.OutputDestinationPrefix
                + binding.TargetFacilityId.Value;
            billTargets.Add(bill.billId, binding);
        }

        DungeonPhysicalItemSaveData projectedPhysical = Clone(
            source.PhysicalItems);
        Dictionary<string, DestinationProjection> destinations =
            BuildDestinations(source.Bills, map, billTargets);
        foreach (WorldItemStackSaveData stack in projectedPhysical.stacks
                     ?? new List<WorldItemStackSaveData>())
        {
            if (stack == null
                || !destinations.TryGetValue(
                    stack.destinationId ?? string.Empty,
                    out DestinationProjection projection))
            {
                continue;
            }
            stack.destinationId = projection.TargetDestinationId;
            stack.hasDestinationPosition = true;
            stack.destinationGridX = projection.TargetPosition.x;
            stack.destinationGridY = projection.TargetPosition.y;
            if (stack.state is WorldItemStackState.FacilityBuffer
                or WorldItemStackState.FacilityOutputBuffer)
            {
                stack.gridX = projection.TargetPosition.x;
                stack.gridY = projection.TargetPosition.y;
            }
        }

        HaulDeliveryIntentSaveData[] projectedIntents = source.HaulIntents
            .Select(HaulDeliveryIntentRuntime.CloneForProjection)
            .ToArray();
        foreach (HaulDeliveryIntentSaveData intent in projectedIntents)
        {
            if (!destinations.TryGetValue(
                    intent.destinationId ?? string.Empty,
                    out DestinationProjection projection))
            {
                continue;
            }
            intent.destinationId = projection.TargetDestinationId;
            intent.deliveryGridX = projection.TargetPosition.x;
            intent.deliveryGridY = projection.TargetPosition.y;
            intent.dropGridX = projection.TargetPosition.x;
            intent.dropGridY = projection.TargetPosition.y;
        }

        candidate = new ProductionActiveFacilityRetargetSnapshot(
            projectedBills,
            projectedPhysical,
            projectedIntents);
        failureReason = string.Empty;
        return true;
    }

    internal static T Clone<T>(T source) where T : class
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        return JsonUtility.FromJson<T>(JsonUtility.ToJson(source));
    }

    internal static string CaptureFingerprint(
        ProductionActiveFacilityRetargetSnapshot snapshot)
    {
        StringBuilder canonical = new StringBuilder(CanonicalVersion)
            .Append('|').Append(JsonUtility.ToJson(snapshot.Bills)).Append('|')
            .Append(JsonUtility.ToJson(snapshot.PhysicalItems)).Append('|');
        foreach (HaulDeliveryIntentSaveData intent in snapshot.HaulIntents
                     .OrderBy(value => value.operationId,
                         StringComparer.Ordinal))
        {
            canonical.Append(JsonUtility.ToJson(intent)).Append(';');
        }
        return ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            canonical.ToString());
    }

    private static Dictionary<string, DestinationProjection> BuildDestinations(
        DungeonProductionBillSaveData originalBills,
        IReadOnlyDictionary<string, ProductionFacilityRetargetBinding> map,
        IReadOnlyDictionary<string, ProductionFacilityRetargetBinding> billTargets)
    {
        Dictionary<string, DestinationProjection> result = new(
            StringComparer.Ordinal);
        foreach (KeyValuePair<string, ProductionFacilityRetargetBinding> pair in
                 map.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            string sourceOutput = ProductionBillRuntime.OutputDestinationPrefix
                + pair.Key;
            string targetOutput = ProductionBillRuntime.OutputDestinationPrefix
                + pair.Value.TargetFacilityId.Value;
            AddOrRequireSame(result, sourceOutput, new(
                targetOutput,
                pair.Value.TargetFacility.Position));
        }
        foreach (ProductionBillSaveData bill in originalBills.bills
                     ?? new List<ProductionBillSaveData>())
        {
            if (bill == null
                || !billTargets.TryGetValue(
                    bill.billId,
                    out ProductionFacilityRetargetBinding binding))
            {
                continue;
            }
            AddOrRequireSame(result, bill.materialDestinationId, new(
                bill.materialDestinationId,
                binding.TargetFacility.Position));
        }
        return result;
    }

    private static void AddOrRequireSame(
        IDictionary<string, DestinationProjection> values,
        string source,
        DestinationProjection projection)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;
        if (values.TryGetValue(source, out DestinationProjection existing)
            && (!string.Equals(
                    existing.TargetDestinationId,
                    projection.TargetDestinationId,
                    StringComparison.Ordinal)
                || existing.TargetPosition != projection.TargetPosition))
        {
            throw new InvalidOperationException(
                "Active retarget destination projection is ambiguous: " + source);
        }
        values[source] = projection;
    }

    private static bool HasTerminalOutputAuthority(ProductionBillSaveData bill) =>
        bill.preparedOutput != null
            && bill.preparedOutput.phase != ProductionPreparedOutputPhase.Unresolved
        || (bill.resolvedOutputs ?? new List<ProductionResolvedOutputSaveData>())
            .Any(value => value != null
                && (!string.IsNullOrEmpty(value.pendingCommitId)
                    || value.pendingOutputPublication?.phase ==
                        ProductionExactOutputPublicationPhase.Published));

    private readonly struct DestinationProjection
    {
        public DestinationProjection(
            string targetDestinationId,
            Vector2Int targetPosition)
        {
            TargetDestinationId = targetDestinationId;
            TargetPosition = targetPosition;
        }

        public string TargetDestinationId { get; }
        public Vector2Int TargetPosition { get; }
    }
}
