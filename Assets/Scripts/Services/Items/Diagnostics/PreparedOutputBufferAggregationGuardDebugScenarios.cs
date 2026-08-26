#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class PreparedOutputBufferAggregationGuardDebugScenarios
{
    private const string ItemId = "material:lumber";
    private const string DestinationId = "production:qa:input";
    private const string CohortId = "production:qa:input:lumber";
    private static readonly Vector2Int DestinationPosition = new(7, 5);

    [MenuItem("DungeonStory/Debug/Items/Run Prepared Output Buffer Aggregation Guards")]
    public static void RunAll()
    {
        VerifyDetachedCustodyStopsDepositFallback();
        VerifyLiveCustodyStopsDepositFallback();
        VerifyCustodyTargetIsNeverMerged();
        VerifyDeferredCustodyFailsBeforeAggregation();
        Debug.Log("Prepared-output buffer aggregation guards PASS.");
    }

    private static void VerifyDetachedCustodyStopsDepositFallback()
    {
        Fixture fixture = new();
        CharacterCarriedItemSaveData incoming = CreateCarried(
            "item-stack:qa:detached-custody",
            CreateCustody());
        int beforeCount = fixture.Repository.Records.Count;

        FacilityOutputExactRouteBypassException failure = RequireThrows(
            () => fixture.Aggregation.TryDepositAndAggregate(
                incoming,
                ItemReservationPurpose.ProductionInput,
                CohortId,
                DestinationId,
                DestinationPosition,
                out _,
                out _));

        Require(failure.Code ==
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass
            && fixture.Repository.Records.Count == beforeCount
            && fixture.Aggregation.PendingAggregationCount == 0,
            "Detached custody reached generic facility deposit or its fallback.");
    }

    private static void VerifyLiveCustodyStopsDepositFallback()
    {
        Fixture fixture = new();
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            fixture.Repository,
            ItemId,
            2,
            WorldItemStackState.Carried,
            position: new Vector2Int(2, 3),
            destinationId: "character:qa:carrier",
            components: CreateCustody());
        var record = fixture.Repository.RecordsById[stackId];
        string signature = ItemStackSignature.Create(
            record.itemId,
            record.components);
        CharacterCarriedItemSaveData strippedDto = CreateCarried(
            stackId,
            Array.Empty<ItemInstanceComponentSaveData>(),
            quantity: 2);

        RequireThrows(() => fixture.Aggregation.TryDepositAndAggregate(
            strippedDto,
            ItemReservationPurpose.ProductionInput,
            CohortId,
            DestinationId,
            DestinationPosition,
            out _,
            out _));

        Require(record.quantity == 2
            && record.state == WorldItemStackState.Carried
            && record.position == new Vector2Int(2, 3)
            && record.destinationId == "character:qa:carrier"
            && ItemStackSignature.Create(
                record.itemId,
                record.components) == signature,
            "Live custody changed before the facility deposit guard rejected it.");
    }

    private static void VerifyCustodyTargetIsNeverMerged()
    {
        Fixture fixture = new();
        string protectedId = WorldItemRepositoryEditorAccess.AddStack(
            fixture.Repository,
            ItemId,
            2,
            WorldItemStackState.FacilityBuffer,
            position: DestinationPosition,
            destinationId: DestinationId,
            // Simulate a malformed stacking flag. The custody type itself must
            // still fail closed instead of becoming signature-compatible input.
            components: CreateCustody(affectsStacking: false));
        var protectedTarget =
            fixture.Repository.RecordsById[protectedId];
        protectedTarget.aggregationCohortId = CohortId;
        protectedTarget.hasDestinationPosition = true;
        protectedTarget.destinationPosition = DestinationPosition;
        fixture.Repository.MarkChanged();
        string signature = ItemStackSignature.Create(
            protectedTarget.itemId,
            protectedTarget.components);

        Require(fixture.Aggregation.TryDepositAndAggregate(
                CreateCarried("item-stack:qa:ordinary"),
                ItemReservationPurpose.ProductionInput,
                CohortId,
                DestinationId,
                DestinationPosition,
                out BufferAggregationReceipt receipt,
                out DomainFailure failure),
            "Ordinary deposit beside custody failed unexpectedly: " + failure);

        Require(protectedTarget.quantity == 2
            && ItemStackSignature.Create(
                protectedTarget.itemId,
                protectedTarget.components) == signature
            && !string.Equals(
                receipt.CanonicalStackId,
                protectedId,
                StringComparison.Ordinal)
            && fixture.Repository.Records.Count(record => record != null
                && record.state == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    record.destinationId,
                    DestinationId,
                    StringComparison.Ordinal)) == 2,
            "Generic aggregation merged ordinary input into protected custody.");
    }

    private static void VerifyDeferredCustodyFailsBeforeAggregation()
    {
        Fixture fixture = new();
        for (int index = 0; index < 64; index++)
        {
            Require(fixture.Aggregation.TryDepositAndAggregate(
                    CreateCarried($"item-stack:qa:warmup:{index:D3}"),
                    ItemReservationPurpose.ProductionInput,
                    CohortId,
                    DestinationId,
                    DestinationPosition,
                    out _,
                    out DomainFailure failure),
                $"Aggregation warmup {index} failed: {failure}");
        }

        const string pendingId = "item-stack:qa:pending-custody";
        Require(fixture.Aggregation.TryDepositAndAggregate(
                CreateCarried(pendingId),
                ItemReservationPurpose.ProductionInput,
                CohortId,
                DestinationId,
                DestinationPosition,
                out _,
                out DomainFailure pendingFailure)
            && fixture.Aggregation.PendingAggregationCount == 1,
            "Deferred aggregation fixture was not staged: " + pendingFailure);
        var pending = fixture.Repository.RecordsById[pendingId];
        pending.components = CreateCustody().Select(value => value.Clone()).ToList();
        fixture.Repository.MarkChanged();
        string signature = ItemStackSignature.Create(
            pending.itemId,
            pending.components);

        RequireThrows(() => fixture.Aggregation.ProcessPending(
            maxOperations: 64,
            beginNewTick: true));

        Require(pending.quantity == 1
            && pending.state == WorldItemStackState.FacilityBuffer
            && pending.position == DestinationPosition
            && ItemStackSignature.Create(
                pending.itemId,
                pending.components) == signature
            && fixture.Aggregation.PendingAggregationCount == 0,
            "Deferred custody was mutated by generic buffer aggregation.");
    }

    private static CharacterCarriedItemSaveData CreateCarried(
        string stackId,
        IReadOnlyList<ItemInstanceComponentSaveData> components = null,
        int quantity = 1) => new()
    {
        carriedStackId = stackId,
        sourceStackId = "item-stack:qa:source",
        ownerOperationId = "production:qa:haul",
        itemId = ItemId,
        quantity = quantity,
        components = (components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Select(value => value?.Clone())
            .Where(value => value != null)
            .ToList()
    };

    private static IReadOnlyList<ItemInstanceComponentSaveData> CreateCustody(
        bool affectsStacking = true) =>
        new[]
        {
            new ItemInstanceComponentSaveData
            {
                componentTypeId =
                    FacilityOutputExactRouteCustodyCodec.ComponentTypeId,
                schemaVersion = FacilityOutputExactRouteCustodyCodec.SchemaVersion,
                affectsStacking = affectsStacking,
                values = new List<ItemStateValueSaveData>()
            }
        };

    private static FacilityOutputExactRouteBypassException RequireThrows(
        Action action)
    {
        try
        {
            action();
        }
        catch (FacilityOutputExactRouteBypassException exception)
        {
            return exception;
        }
        throw new InvalidOperationException(
            "Prepared-output custody did not fail through its typed boundary.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal Fixture()
        {
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            ItemQuantityReservationService reservations = new(
                Repository,
                EditorNullItemMarkerPresenter.Instance,
                new FixedClock());
            Aggregation = new BufferStackAggregationService(
                EditorItemCatalogFactory.Create(),
                Repository,
                EditorNullItemMarkerPresenter.Instance,
                reservations,
                reservations);
        }

        internal WorldItemRepository Repository { get; }
        internal BufferStackAggregationService Aggregation { get; }
    }

    private sealed class FixedClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }
}
#endif
