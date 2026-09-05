using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Pure raw-save validation for acknowledged planned-output provenance.
/// An acknowledged generic production marker is a historical receipt, not an
/// active producer lease. Prepared batches are different: until exact-route
/// custody replaces their provenance marker, the routing journal is their one
/// durable downstream owner.
/// </summary>
public sealed class ProductionAcknowledgedOutputLifecycleSaveValidation :
    IDungeonSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator
{
    private const string PreparedBatchPrefix = "production-output-batch:";

    public void Validate(
        DungeonGameSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        try
        {
            bool hasPhysical = TryFindEnvelope(
                saveData.sections,
                PhysicalItemsSaveSection.Id,
                out DungeonSaveSectionEnvelope physicalEnvelope);
            bool hasRouting = TryFindEnvelope(
                saveData.sections,
                ProductionPreparedOutputRoutingSaveSection.Id,
                out DungeonSaveSectionEnvelope routingEnvelope);
            if (!hasPhysical && !hasRouting)
                return;
            if (!hasPhysical || !hasRouting)
            {
                throw new InvalidOperationException(
                    "Acknowledged-output lifecycle validation requires both Physical Items and Prepared Output Routing sections.");
            }

            ValidateCore(
                Parse<DungeonPhysicalItemSaveData>(
                    physicalEnvelope,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion),
                Parse<ProductionPreparedOutputRoutingSaveData>(
                    routingEnvelope,
                    ProductionPreparedOutputRoutingSaveSection.Id,
                    ProductionPreparedOutputRoutingSaveData.CurrentVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Acknowledged-output lifecycle save preflight failed: "
                + exception.Message);
        }
    }

    public void Validate(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report)
    {
        if (envelopes == null)
            throw new ArgumentNullException(nameof(envelopes));
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        try
        {
            bool hasPhysical = envelopes.TryGetValue(
                PhysicalItemsSaveSection.Id,
                out DungeonSaveSectionEnvelope physicalEnvelope);
            bool hasRouting = envelopes.TryGetValue(
                ProductionPreparedOutputRoutingSaveSection.Id,
                out DungeonSaveSectionEnvelope routingEnvelope);
            if (!hasPhysical && !hasRouting)
                return;
            if (!hasPhysical || !hasRouting)
            {
                throw new InvalidOperationException(
                    "Acknowledged-output lifecycle registry validation requires both Physical Items and Prepared Output Routing sections.");
            }

            ValidateCore(
                Parse<DungeonPhysicalItemSaveData>(
                    physicalEnvelope,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion),
                Parse<ProductionPreparedOutputRoutingSaveData>(
                    routingEnvelope,
                    ProductionPreparedOutputRoutingSaveSection.Id,
                    ProductionPreparedOutputRoutingSaveData.CurrentVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Acknowledged-output lifecycle registry preflight failed: "
                + exception.Message);
        }
    }

    internal static void ValidateCore(
        DungeonPhysicalItemSaveData physical,
        ProductionPreparedOutputRoutingSaveData routing)
    {
        if (physical?.stacks == null)
        {
            throw new InvalidOperationException(
                "Acknowledged-output lifecycle has no physical stack collection.");
        }
        if (physical.pendingExactOutputRoutes == null)
        {
            throw new InvalidOperationException(
                "Acknowledged-output lifecycle has no exact-route outbox collection.");
        }
        if (routing?.batches == null)
        {
            throw new InvalidOperationException(
                "Acknowledged-output lifecycle has no prepared routing owner collection.");
        }

        MarkerBatch[] markerBatches = CaptureMarkerBatches(physical.stacks);
        Dictionary<string, MarkerBatch> acknowledgedPrepared = markerBatches
            .Where(value => value.Acknowledged
                && IsPreparedBatchId(value.BatchCommitId))
            .ToDictionary(value => value.BatchCommitId, StringComparer.Ordinal);

        Dictionary<string, ProductionPreparedOutputRoutingBatchSaveData>
            routingOwners = new(StringComparer.Ordinal);
        foreach (ProductionPreparedOutputRoutingBatchSaveData batch in
                 routing.batches
                     .Where(value => value != null)
                     .OrderBy(value => value.batchCommitId, StringComparer.Ordinal))
        {
            string batchCommitId = batch.batchCommitId ?? string.Empty;
            if (!IsPreparedBatchId(batchCommitId))
                continue;
            if (!routingOwners.TryAdd(batchCommitId, batch))
            {
                throw new InvalidOperationException(
                    "Prepared-output routing has a duplicate batch owner: "
                    + batchCommitId);
            }
        }

        Dictionary<string, FacilityOutputExactRouteOutboxSaveData[]>
            exactRoutesByBatch = physical.pendingExactOutputRoutes
            .Where(value => value != null
                && IsPreparedBatchId(value.batchCommitId ?? string.Empty))
            .GroupBy(value => value.batchCommitId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(value => value.routeOperationId, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);

        foreach (MarkerBatch acknowledged in acknowledgedPrepared.Values
                     .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal))
        {
            if (!routingOwners.ContainsKey(acknowledged.BatchCommitId))
            {
                throw new InvalidOperationException(
                    "Acknowledged prepared-output provenance has no routing batch owner: "
                    + acknowledged.BatchCommitId);
            }
            if (exactRoutesByBatch.ContainsKey(acknowledged.BatchCommitId))
            {
                throw new InvalidOperationException(
                    "Prepared-output provenance and exact-route outbox claim dual custody: "
                    + acknowledged.BatchCommitId);
            }
        }

        foreach (string routingBatchId in routingOwners.Keys
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            bool hasProvenance = acknowledgedPrepared.ContainsKey(routingBatchId);
            bool hasExactRoute = exactRoutesByBatch.ContainsKey(routingBatchId);
            if (hasProvenance && hasExactRoute)
            {
                throw new InvalidOperationException(
                    "Prepared-output routing has both provenance and exact-route custody: "
                    + routingBatchId);
            }
            if (!hasProvenance && !hasExactRoute)
            {
                throw new InvalidOperationException(
                    "Prepared-output routing batch has no physical downstream owner: "
                    + routingBatchId);
            }
        }

        // Pending generic production-output:* batches remain the responsibility
        // of ProductionExactCapabilityOutputRestoreJoin. Acknowledged generic
        // batches are terminal historical provenance and intentionally require
        // no Production owner here. Unknown domain capability IDs are likewise
        // left to their owning semantic validators.
    }

    private static MarkerBatch[] CaptureMarkerBatches(
        IEnumerable<WorldItemStackSaveData> source)
    {
        List<MarkerRecord> marked = new();
        foreach (WorldItemStackSaveData stack in
                 (source ?? Array.Empty<WorldItemStackSaveData>())
                 .Where(value => value != null)
                 .OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            bool hasMarker = (stack.components
                    ?? new List<ItemInstanceComponentSaveData>())
                .Any(PlannedOutputPublicationComponentCodec.IsAnyMarker);
            if (!hasMarker)
                continue;
            if (!PlannedOutputPublicationComponentCodec.TryRead(
                    stack.components,
                    out PlannedOutputPublicationMetadata metadata))
            {
                throw new InvalidOperationException(
                    "Malformed planned-output marker on physical stack: "
                    + stack.stackId);
            }
            marked.Add(new MarkerRecord(stack, metadata));
        }

        List<MarkerBatch> result = new();
        foreach (IGrouping<string, MarkerRecord> group in marked
                     .GroupBy(value => value.Metadata.BatchCommitId,
                         StringComparer.Ordinal)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            MarkerRecord[] records = group
                .OrderBy(value => value.Metadata.OutputLineId, StringComparer.Ordinal)
                .ThenBy(value => value.Metadata.StackOrdinal)
                .ThenBy(value => value.Stack.stackId, StringComparer.Ordinal)
                .ToArray();
            PlannedOutputPublicationMetadata header = records[0].Metadata;
            bool consistent = records.All(value =>
                    value.Metadata.Acknowledged == header.Acknowledged
                    && string.Equals(
                        value.Metadata.OutcomeFingerprint,
                        header.OutcomeFingerprint,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.Metadata.PlannedOutputFingerprint,
                        header.PlannedOutputFingerprint,
                        StringComparison.Ordinal)
                    && value.Metadata.BatchStackCount == header.BatchStackCount
                    && value.Metadata.BatchQuantity == header.BatchQuantity
                    && value.Metadata.BatchMassGrams == header.BatchMassGrams
                    && string.Equals(
                        value.Metadata.ItemId,
                        value.Stack.itemId,
                        StringComparison.Ordinal)
                    && value.Metadata.Quantity == value.Stack.quantity
                    && value.Metadata.MassGrams > 0L
                    && (header.Acknowledged
                        || string.Equals(
                            value.Metadata.ComponentSignature,
                            FacilityBufferPlannedOutputPublicationService
                                .CreateRuntimeComponentSignature(value.Stack.components),
                            StringComparison.Ordinal)))
                && records.Length == header.BatchStackCount
                && records.Sum(value => value.Stack.quantity)
                    == header.BatchQuantity
                && records.Sum(value => value.Metadata.MassGrams)
                    == header.BatchMassGrams;
            if (!consistent)
            {
                throw new InvalidOperationException(
                    "Partial or conflicting planned-output marker batch: "
                    + group.Key);
            }

            foreach (IGrouping<string, MarkerRecord> line in records.GroupBy(
                         value => value.Metadata.OutputLineId,
                         StringComparer.Ordinal))
            {
                MarkerRecord[] lineRecords = line
                    .OrderBy(value => value.Metadata.StackOrdinal)
                    .ThenBy(value => value.Stack.stackId, StringComparer.Ordinal)
                    .ToArray();
                PlannedOutputPublicationMetadata lineHeader =
                    lineRecords[0].Metadata;
                bool lineConsistent = lineRecords.Length
                        == lineHeader.LineStackCount
                    && lineRecords.Select(value => value.Metadata.StackOrdinal)
                        .SequenceEqual(Enumerable.Range(0, lineRecords.Length))
                    && lineRecords.Sum(value => value.Stack.quantity)
                        == lineHeader.LineQuantity
                    && lineRecords.Sum(value => value.Metadata.MassGrams)
                        == lineHeader.LineMassGrams
                    && lineRecords.All(value =>
                        value.Metadata.LineStackCount == lineHeader.LineStackCount
                        && value.Metadata.LineQuantity == lineHeader.LineQuantity
                        && value.Metadata.LineMassGrams == lineHeader.LineMassGrams);
                if (!lineConsistent)
                {
                    throw new InvalidOperationException(
                        "Partial or conflicting planned-output marker line: "
                        + group.Key + ":" + line.Key);
                }
            }

            result.Add(new MarkerBatch(group.Key, header.Acknowledged));
        }
        return result.ToArray();
    }

    private static bool IsPreparedBatchId(string batchCommitId) =>
        !string.IsNullOrWhiteSpace(batchCommitId)
        && string.Equals(
            batchCommitId,
            batchCommitId.Trim(),
            StringComparison.Ordinal)
        && batchCommitId.StartsWith(PreparedBatchPrefix, StringComparison.Ordinal);

    private static bool TryFindEnvelope(
        IEnumerable<DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        out DungeonSaveSectionEnvelope envelope)
    {
        DungeonSaveSectionEnvelope[] matches = (envelopes
                ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null
                && string.Equals(
                    value.sectionId,
                    sectionId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                "Duplicate save section envelope: " + sectionId);
        }
        envelope = matches.SingleOrDefault();
        return envelope != null;
    }

    private static TPayload Parse<TPayload>(
        DungeonSaveSectionEnvelope envelope,
        string sectionId,
        int currentVersion)
        where TPayload : class
    {
        if (envelope == null
            || !string.Equals(
                envelope.sectionId,
                sectionId,
                StringComparison.Ordinal)
            || envelope.sectionVersion != currentVersion
            || string.IsNullOrWhiteSpace(envelope.payloadJson))
        {
            throw new InvalidOperationException(
                "Save section envelope is not exact current format: " + sectionId);
        }
        return JsonUtility.FromJson<TPayload>(envelope.payloadJson)
            ?? throw new InvalidOperationException(
                "Save section payload deserialized to null: " + sectionId);
    }

    private readonly struct MarkerRecord
    {
        internal MarkerRecord(
            WorldItemStackSaveData stack,
            PlannedOutputPublicationMetadata metadata)
        {
            Stack = stack;
            Metadata = metadata;
        }

        internal WorldItemStackSaveData Stack { get; }
        internal PlannedOutputPublicationMetadata Metadata { get; }
    }

    private readonly struct MarkerBatch
    {
        internal MarkerBatch(string batchCommitId, bool acknowledged)
        {
            BatchCommitId = batchCommitId ?? string.Empty;
            Acknowledged = acknowledged;
        }

        internal string BatchCommitId { get; }
        internal bool Acknowledged { get; }
    }
}

#if UNITY_EDITOR
public static class ProductionAcknowledgedOutputLifecycleSaveValidationScenarios
{
    private const string GenericCommitId =
        "production-output:bill:qa:00000001:line:qa:item:qa:00000000";
    private const string PreparedCommitId =
        "production-output-batch:bill:qa:00000001:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string UnknownDomainCommitId = "domain-output:qa:0001";

    [UnityEditor.MenuItem(
        "DungeonStory/Debug/Economy/Run Acknowledged Output Lifecycle Save Validation")]
    public static void RunAll()
    {
        RequirePass("generic-ownerless-terminal", CreateFixture(
            CreateMarkerStack(GenericCommitId, acknowledged: true),
            Array.Empty<ProductionPreparedOutputRoutingBatchSaveData>(),
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));
        RequirePass("acknowledged-runtime-component-evolved", CreateFixture(
            CreateMarkerStackWithRuntimeComponentEvolution(
                GenericCommitId + ":evolved",
                acknowledged: true),
            Array.Empty<ProductionPreparedOutputRoutingBatchSaveData>(),
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));
        RequirePass("pending-generic-delegated", CreateFixture(
            CreateMarkerStack(GenericCommitId, acknowledged: false),
            Array.Empty<ProductionPreparedOutputRoutingBatchSaveData>(),
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));
        RequirePass("unknown-domain-delegated", CreateFixture(
            CreateMarkerStack(UnknownDomainCommitId, acknowledged: true),
            Array.Empty<ProductionPreparedOutputRoutingBatchSaveData>(),
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));
        RequirePass("prepared-routing-owner", CreateFixture(
            CreateMarkerStack(PreparedCommitId, acknowledged: true),
            new[] { CreateRoutingOwner(PreparedCommitId) },
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));
        RequirePass("prepared-exact-route-owner", CreateFixture(
            null,
            new[] { CreateRoutingOwner(PreparedCommitId) },
            new[] { CreateExactRoute(PreparedCommitId) }));

        RequireFail("malformed-marker", CreateFixture(
            CreateMalformedMarkerStack(GenericCommitId),
            Array.Empty<ProductionPreparedOutputRoutingBatchSaveData>(),
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));
        RequireFail("partial-marker", CreateFixture(
            CreateMarkerStack(
                GenericCommitId,
                acknowledged: true,
                declaredStackCount: 2),
            Array.Empty<ProductionPreparedOutputRoutingBatchSaveData>(),
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));
        RequireFail("pending-runtime-component-drift", CreateFixture(
            CreateMarkerStackWithRuntimeComponentEvolution(
                GenericCommitId + ":pending-drift",
                acknowledged: false),
            Array.Empty<ProductionPreparedOutputRoutingBatchSaveData>(),
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));
        RequireFail("prepared-owner-missing", CreateFixture(
            CreateMarkerStack(PreparedCommitId, acknowledged: true),
            Array.Empty<ProductionPreparedOutputRoutingBatchSaveData>(),
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));
        RequireFail("prepared-dual-custody", CreateFixture(
            CreateMarkerStack(PreparedCommitId, acknowledged: true),
            new[] { CreateRoutingOwner(PreparedCommitId) },
            new[] { CreateExactRoute(PreparedCommitId) }));
        RequireFail("prepared-routing-orphan", CreateFixture(
            null,
            new[] { CreateRoutingOwner(PreparedCommitId) },
            Array.Empty<FacilityOutputExactRouteOutboxSaveData>()));

        Debug.Log(
            "[PASS] Acknowledged output lifecycle save/registry preflight: "
            + "ownerless generic, delegated domains, prepared routing, malformed/partial, dual custody, orphan routing, mutation=0.");
    }

    private static void RequirePass(string label, DungeonGameSaveData save)
    {
        ValidateBoth(label, save, expectedSuccess: true);
    }

    private static void RequireFail(string label, DungeonGameSaveData save)
    {
        ValidateBoth(label, save, expectedSuccess: false);
    }

    private static void ValidateBoth(
        string label,
        DungeonGameSaveData save,
        bool expectedSuccess)
    {
        ProductionAcknowledgedOutputLifecycleSaveValidation validator = new();
        string before = JsonUtility.ToJson(save);

        DungeonGameRestoreReport saveReport = new();
        validator.Validate(save, saveReport);
        Require(
            saveReport.Success == expectedSuccess,
            label + " whole-save result mismatch: "
            + string.Join(" | ", saveReport.Errors));

        Dictionary<string, DungeonSaveSectionEnvelope> envelopes = save.sections
            .ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        DungeonGameRestoreReport registryReport = new();
        validator.Validate(envelopes, registryReport);
        Require(
            registryReport.Success == expectedSuccess,
            label + " registry result mismatch: "
            + string.Join(" | ", registryReport.Errors));
        Require(
            string.Equals(before, JsonUtility.ToJson(save), StringComparison.Ordinal),
            label + " mutated its input save payload.");
    }

    private static DungeonGameSaveData CreateFixture(
        WorldItemStackSaveData stack,
        IReadOnlyList<ProductionPreparedOutputRoutingBatchSaveData> routingBatches,
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> exactRoutes)
    {
        DungeonPhysicalItemSaveData physical = new()
        {
            stacks = stack == null
                ? new List<WorldItemStackSaveData>()
                : new List<WorldItemStackSaveData> { stack },
            pendingExactOutputRoutes = exactRoutes.ToList()
        };
        ProductionPreparedOutputRoutingSaveData routing = new()
        {
            batches = routingBatches.ToList()
        };
        DungeonGameSaveData save = new();
        DungeonSaveSectionPayload.Write(
            save,
            PhysicalItemsSaveSection.Id,
            DungeonPhysicalItemSaveData.CurrentVersion,
            DungeonSaveRestorePhase.Items,
            physical);
        DungeonSaveSectionPayload.Write(
            save,
            ProductionPreparedOutputRoutingSaveSection.Id,
            ProductionPreparedOutputRoutingSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            routing);
        return save;
    }

    private static WorldItemStackSaveData CreateMarkerStack(
        string batchCommitId,
        bool acknowledged,
        int declaredStackCount = 1)
    {
        const string itemId = "item:qa:output";
        PlannedOutputPublicationMetadata metadata = new(
            batchCommitId,
            new string('a', 64),
            new string('b', 64),
            "line:qa",
            0,
            declaredStackCount,
            1,
            100L,
            declaredStackCount,
            1,
            100L,
            itemId,
            1,
            100L,
            string.Empty,
            new string('c', 64),
            acknowledged);
        ItemInstanceComponentSaveData marker = acknowledged
            ? PlannedOutputPublicationComponentCodec.CreateProvenance(metadata)
            : PlannedOutputPublicationComponentCodec.CreatePublication(
                metadata.BatchCommitId,
                metadata.OutcomeFingerprint,
                metadata.PlannedOutputFingerprint,
                metadata.OutputLineId,
                metadata.StackOrdinal,
                metadata.BatchStackCount,
                metadata.BatchQuantity,
                metadata.BatchMassGrams,
                metadata.LineStackCount,
                metadata.LineQuantity,
                metadata.LineMassGrams,
                metadata.ItemId,
                metadata.Quantity,
                metadata.MassGrams,
                metadata.ComponentSignature,
                metadata.PreparedComponentFingerprint);
        return new WorldItemStackSaveData
        {
            stackId = "stack:qa:0001",
            itemId = itemId,
            quantity = 1,
            state = acknowledged
                ? WorldItemStackState.Loose
                : WorldItemStackState.FacilityOutputBuffer,
            destinationId = acknowledged ? string.Empty : "facility-output:qa",
            components = new List<ItemInstanceComponentSaveData> { marker }
        };
    }

    private static WorldItemStackSaveData
        CreateMarkerStackWithRuntimeComponentEvolution(
            string batchCommitId,
            bool acknowledged)
    {
        WorldItemStackSaveData stack = CreateMarkerStack(
            batchCommitId,
            acknowledged);
        stack.components.Insert(0, new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.Quality,
            schemaVersion = 1,
            affectsStacking = true
        });
        return stack;
    }

    private static WorldItemStackSaveData CreateMalformedMarkerStack(
        string batchCommitId)
    {
        WorldItemStackSaveData stack = CreateMarkerStack(
            batchCommitId,
            acknowledged: true);
        stack.components[0].schemaVersion = 999;
        return stack;
    }

    private static ProductionPreparedOutputRoutingBatchSaveData
        CreateRoutingOwner(string batchCommitId) => new()
        {
            batchCommitId = batchCommitId,
            lines = new List<ProductionPreparedOutputRoutingLineSaveData>()
        };

    private static FacilityOutputExactRouteOutboxSaveData CreateExactRoute(
        string batchCommitId) => new()
    {
        phase = FacilityOutputExactRoutePhase.Routable,
        batchCommitId = batchCommitId,
        routeOperationId = "prepared-output-route:qa:0001"
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
