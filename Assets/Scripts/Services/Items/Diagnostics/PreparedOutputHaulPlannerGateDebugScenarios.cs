#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class PreparedOutputHaulPlannerGateDebugScenarios
{
    private const string ItemId = "material:lumber";
    private const string TargetId = "warehouse:building:qa:prepared-output";
    private static readonly Vector2Int TargetPosition = new(13, 8);

    [MenuItem("DungeonStory/Debug/Items/Run Prepared Output Haul Planner Gates")]
    public static void RunAll()
    {
        VerifyOrdinaryHaulIsIndependent();
        VerifyAcknowledgedPublicationProvenanceIsHaulable();
        VerifyAcknowledgedProvenanceAggregationIsolated();
        VerifyWarehouseSelectionPendingIsExcluded();
        VerifyUnconfirmedRevisionIsExcluded();
        VerifyExactTargetRequiresMatchingOverlay();
        VerifyExactTargetRequiresCurrentWarehouseAuthority();
        VerifyExactWarehouseAdmissionProjection();
        VerifyPickupBoundaryRejectsInjectedInvalidation();
        VerifyCrossRouteOpportunisticAggregationIsExcluded();
        Debug.Log("Prepared-output haul planner gates PASS.");
    }

    private static void VerifyAcknowledgedPublicationProvenanceIsHaulable()
    {
        ItemInstanceComponentSaveData pending =
            PlannedOutputPublicationComponentCodec.CreatePublication(
                "batch:qa:acknowledged-provenance",
                "outcome:qa:acknowledged-provenance",
                "planned:qa:acknowledged-provenance",
                "output:qa:acknowledged-provenance",
                stackOrdinal: 0,
                batchStackCount: 1,
                batchQuantity: 1,
                batchMassGrams: 1800L,
                lineStackCount: 1,
                lineQuantity: 1,
                lineMassGrams: 1800L,
                itemId: "surgery:prosthetic:arm:left",
                quantity: 1,
                massGrams: 1800L,
                componentSignature: "",
                preparedComponentFingerprint: "prepared:qa");
        Require(PlannedOutputPublicationComponentCodec.TryRead(
                new[] { pending },
                out PlannedOutputPublicationMetadata metadata),
            "Valid planned-output publication marker did not parse.");
        ItemInstanceComponentSaveData provenance =
            PlannedOutputPublicationComponentCodec.CreateProvenance(metadata);

        Require(FacilityOutputExactRouteCustodyCodec.IsRouteBlocked(
                new[] { pending }),
            "Unacknowledged planned-output publication became haulable.");
        Require(!FacilityOutputExactRouteCustodyCodec.IsRouteBlocked(
                new[] { provenance }),
            "Acknowledged planned-output provenance remained a haul lock.");

        provenance.schemaVersion++;
        Require(FacilityOutputExactRouteCustodyCodec.IsRouteBlocked(
                new[] { provenance }),
            "Malformed acknowledged provenance became haulable.");
    }

    private static void VerifyAcknowledgedProvenanceAggregationIsolated()
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        ProvenanceAggregationCatalogProvider catalog = new();
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new UnityGameClock());
        BufferStackAggregationService aggregation = new(
            catalog,
            repository,
            EditorNullItemMarkerPresenter.Instance,
            reservations,
            reservations);
        const string destination = "facility:test-provenance-buffer";
        const string cohort = "production:test:provenance";
        CharacterCarriedItemSaveData ordinary = new()
        {
            carriedStackId = "item-stack:ordinary",
            sourceStackId = "item-stack:ordinary-source",
            ownerOperationId = "production:test:ordinary",
            itemId = "item:buffer",
            quantity = 5,
            components = new List<ItemInstanceComponentSaveData>()
        };
        Require(aggregation.TryDepositAndAggregate(
                ordinary,
                ItemReservationPurpose.ProductionInput,
                cohort,
                destination,
                new Vector2Int(8, 4),
                out BufferAggregationReceipt ordinaryReceipt,
                out DomainFailure ordinaryFailure),
            $"ordinary provenance-isolation seed failed: {ordinaryFailure}");

        ItemInstanceComponentSaveData pending =
            PlannedOutputPublicationComponentCodec.CreatePublication(
                "batch:test:provenance",
                "outcome:test:provenance",
                "plan:test:provenance",
                "line:test:provenance",
                0,
                1,
                1,
                100L,
                1,
                1,
                100L,
                "item:buffer",
                1,
                100L,
                string.Empty,
                string.Empty);
        Require(PlannedOutputPublicationComponentCodec.TryRead(
                new[] { pending },
                out PlannedOutputPublicationMetadata metadata),
            "planned-output publication fixture did not decode");
        CharacterCarriedItemSaveData provenance = new()
        {
            carriedStackId = "item-stack:provenance",
            sourceStackId = "item-stack:provenance-source",
            ownerOperationId = "production:test:provenance",
            itemId = "item:buffer",
            quantity = 1,
            components = new List<ItemInstanceComponentSaveData>
            {
                PlannedOutputPublicationComponentCodec.CreateProvenance(metadata)
            }
        };
        Require(aggregation.TryDepositAndAggregate(
                provenance,
                ItemReservationPurpose.ProductionInput,
                cohort,
                destination,
                new Vector2Int(8, 4),
                out BufferAggregationReceipt provenanceReceipt,
                out DomainFailure provenanceFailure),
            $"provenance-preserving deposit failed: {provenanceFailure}");

        WorldItemStackRecord[] stored = repository.Records
            .Where(value => value != null
                && value.state == WorldItemStackState.FacilityBuffer
                && string.Equals(value.destinationId, destination,
                    StringComparison.Ordinal))
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
        Require(stored.Length == 2
            && stored.Sum(value => value.quantity) == 6
            && !string.Equals(
                ordinaryReceipt.CanonicalStackId,
                provenanceReceipt.CanonicalStackId,
                StringComparison.Ordinal)
            && stored.Count(value =>
                (value.components ?? new List<ItemInstanceComponentSaveData>())
                .Any(PlannedOutputPublicationComponentCodec.IsAnyMarker)) == 1,
            "Acknowledged planned-output provenance was merged into an ordinary buffer stack.");
    }

    private static void VerifyOrdinaryHaulIsIndependent()
    {
        WorldItemStackRecord ordinary = new()
        {
            stackId = "item-stack:qa:ordinary",
            itemId = ItemId,
            quantity = 2,
            state = WorldItemStackState.Loose,
            components = new List<ItemInstanceComponentSaveData>()
        };
        Require(WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                ordinary,
                query: null,
                out FacilityOutputExactRouteFailure failure)
            && !failure.IsFailure,
            "Ordinary hauling acquired an exact-route query dependency.");
        Require(WorldItemHaulPlanningService.CanShareOpportunisticRoute(
                ordinary,
                CloneOrdinary(ordinary, "item-stack:qa:ordinary:2")),
            "Ordinary opportunistic hauling changed.");
    }

    private static void VerifyWarehouseSelectionPendingIsExcluded()
    {
        RouteFixture pending = CreateRoute(
            "route:qa:selection-pending",
            string.Empty,
            TargetPosition);
        Require(!WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                pending.Stack,
                new FixedQuery(pending.Snapshot),
                out FacilityOutputExactRouteFailure failure)
            && failure.Code ==
                FacilityOutputExactRouteFailureCode.PendingRouteMissing,
            "Target-empty custody entered generic warehouse selection.");
    }

    private static void VerifyUnconfirmedRevisionIsExcluded()
    {
        RouteFixture valid = CreateRoute(
            "route:qa:unconfirmed",
            TargetId,
            TargetPosition);
        FacilityOutputExactRouteCustodyMetadata invalid =
            CreateBaseCustody(valid, FacilityOutputExactRouteCustodyPhase.Routable)
            .WithSlice(
                FacilityOutputExactRouteCustodyPhase.Routable,
                TargetId,
                valid.Stack.stackId,
                0,
                2,
                2000L,
                valid.Snapshot.Receipt.RouteOperationId,
                valid.Snapshot.Receipt.RequestFingerprint,
                valid.Snapshot.Receipt.PhysicalReceiptFingerprint,
                deliveryRevision: null);
        valid.Stack.components = new List<ItemInstanceComponentSaveData>
        {
            FacilityOutputExactRouteCustodyCodec.Create(invalid)
        };
        Require(!WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                valid.Stack,
                new FixedQuery(valid.Snapshot),
                out FacilityOutputExactRouteFailure failure)
            && failure.IsFailure,
            "Revision-unconfirmed custody became haulable.");
    }

    private static void VerifyExactTargetRequiresMatchingOverlay()
    {
        RouteFixture valid = CreateRoute(
            "route:qa:exact-target",
            TargetId,
            TargetPosition);
        FixedQuery query = new(valid.Snapshot);
        FixedAdmissionAuthority authority = FixedAdmissionAuthority.Matching(
            TargetId,
            TargetPosition,
            Sha('8'));
        Require(WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                valid.Stack,
                query,
                out FacilityOutputExactRouteFailure success,
                authority)
            && !success.IsFailure,
            "Concrete exact target with matching overlay was rejected.");
        Require(!WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                valid.Stack,
                query: null,
                out FacilityOutputExactRouteFailure missingQuery)
            && missingQuery.Code ==
                FacilityOutputExactRouteFailureCode.PendingRouteMissing,
            "Exact custody was accepted without its live delivery query.");

        valid.Stack.destinationId = "warehouse:qa:wrong";
        Require(!WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                valid.Stack,
                query,
                out FacilityOutputExactRouteFailure mismatch)
            && mismatch.Code ==
                FacilityOutputExactRouteFailureCode.ReceiptMismatch,
            "Physical current-target mismatch was accepted.");

        RouteFixture otherOverlay = CreateRoute(
            valid.Snapshot.Receipt.RouteOperationId,
            "warehouse:qa:other",
            new Vector2Int(19, 8));
        valid.Stack.destinationId = TargetId;
        Require(!WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                valid.Stack,
                new FixedQuery(otherOverlay.Snapshot),
                out FacilityOutputExactRouteFailure overlayMismatch)
            && overlayMismatch.Code ==
                FacilityOutputExactRouteFailureCode.ReceiptMismatch,
            "Outbox current-delivery mismatch was accepted.");
    }

    private static void VerifyExactTargetRequiresCurrentWarehouseAuthority()
    {
        RouteFixture valid = CreateRoute(
            "route:qa:authority",
            TargetId,
            TargetPosition);
        FixedQuery query = new(valid.Snapshot);
        Require(!WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                valid.Stack,
                query,
                out FacilityOutputExactRouteFailure missing,
                destinationAdmission: null)
            && missing.Code == FacilityOutputExactRouteFailureCode.ReceiptMismatch,
            "Concrete warehouse custody was accepted without live authority.");
        Require(WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                valid.Stack,
                query,
                out FacilityOutputExactRouteFailure advancedRevision,
                FixedAdmissionAuthority.Matching(
                    TargetId,
                    TargetPosition,
                    Sha('9')))
            && !advancedRevision.IsFailure,
            "A live warehouse whose admission revision advanced was rejected. "
                + advancedRevision);
        Require(!WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                valid.Stack,
                query,
                out FacilityOutputExactRouteFailure malformed,
                FixedAdmissionAuthority.Matching(
                    TargetId,
                    TargetPosition,
                    " "))
            && malformed.Code == FacilityOutputExactRouteFailureCode.ReceiptMismatch,
            "A non-canonical live warehouse authority was accepted.");
        Require(!WorldItemHaulPlanningService.IsExactRouteDeliveryCandidate(
                valid.Stack,
                query,
                out FacilityOutputExactRouteFailure wrongPosition,
                FixedAdmissionAuthority.Matching(
                    TargetId,
                    TargetPosition + Vector2Int.right,
                    Sha('8')))
            && wrongPosition.Code == FacilityOutputExactRouteFailureCode.ReceiptMismatch,
            "Warehouse authority at a different physical position was accepted.");
    }

    private static void VerifyCrossRouteOpportunisticAggregationIsExcluded()
    {
        RouteFixture first = CreateRoute("route:qa:first", TargetId, TargetPosition);
        RouteFixture second = CreateRoute("route:qa:second", TargetId, TargetPosition);
        RouteFixture sameRoute = CreateRoute("route:qa:first", TargetId, TargetPosition,
            "item-stack:qa:first:descendant");
        WorldItemStackRecord ordinary = CloneOrdinary(
            first.Stack,
            "item-stack:qa:ordinary-peer");
        ordinary.components.Clear();

        Require(!WorldItemHaulPlanningService.CanShareOpportunisticRoute(
                first.Stack,
                second.Stack)
            && !WorldItemHaulPlanningService.CanShareOpportunisticRoute(
                first.Stack,
                ordinary)
            && WorldItemHaulPlanningService.CanShareOpportunisticRoute(
                first.Stack,
                sameRoute.Stack),
            "Cross-route or ordinary/exact opportunistic aggregation leaked.");
    }

    private static void VerifyExactWarehouseAdmissionProjection()
    {
        RouteFixture valid = CreateRoute(
            "route:qa:admission",
            TargetId,
            TargetPosition);
        WorldItemStackRecord ordinary = CloneOrdinary(
            valid.Stack,
            "item-stack:qa:ordinary-admission-order");
        ordinary.components.Clear();
        Require(WorldItemHaulPlanningService.IsExactWarehouseCustody(valid.Stack)
            && !WorldItemHaulPlanningService.IsExactWarehouseCustody(ordinary)
            && WorldItemHaulPlanningService
                .RequiresExactWarehouseAdmissionsBeforeLease(
                    new[] { valid.Stack })
            && !WorldItemHaulPlanningService
                .RequiresExactWarehouseAdmissionsBeforeLease(
                    new[] { valid.Stack, ordinary }),
            "Exact custody did not select admission-before-lease ordering.");
        string lotFingerprint = ItemReservationSignature.Create(
            valid.Stack.itemId,
            valid.Stack.components);
        WarehouseMassAdmissionRequest request = new(
            new BuildingInstanceId("building:qa:prepared-output"),
            "haul-delivery:qa:warehouse-admission:00",
            (ItemDefinitionId)valid.Stack.itemId,
            valid.Stack.itemInstanceId,
            lotFingerprint,
            requestedQuantity: 2,
            expectedWarehouseCapacityRevision: 17L,
            expectedCatalogRevision: 19L,
            expectedSourceRevision: 23L,
            massSubject: PhysicalItemMassSubject.ForDefinition(
                (ItemDefinitionId)valid.Stack.itemId));
        WarehouseMassAdmissionToken exact = new(
            "warehouse-mass-token:qa:exact",
            request,
            acceptedQuantity: 2,
            reservedMassGrams: 2000L,
            warehouseCapacityRevision: 18L,
            expiresAtGameSeconds: 120d);
        Require(WorldItemHaulPlanningService.ExactWarehouseAdmissionMatches(
                valid.Stack,
                exactQuantity: 2,
                token: exact),
            "Exact current lot admission projection was rejected.");
        WarehouseHaulAdmissionSaveData projection =
            WorldItemHaulPlanningService.CreateWarehouseAdmissionProjection(
                valid.Stack,
                request.OwnerOperationId,
                exact);
        Require(string.Equals(
                    projection.tokenId,
                    exact.TokenId,
                    StringComparison.Ordinal)
                && string.Equals(
                    projection.ownerAdmissionOperationId,
                    request.OwnerOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    projection.warehouseId,
                    exact.WarehouseId.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    projection.sourceStackId,
                    valid.Stack.stackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    projection.lotFingerprint,
                    lotFingerprint,
                    StringComparison.Ordinal)
                && projection.quantity == valid.Stack.quantity
                && projection.reservedMassGrams == 2000L
                && projection.catalogRevision == exact.CatalogRevision
                && projection.sourceRevision == exact.SourceRevision,
            "Exact admission was not durably projected for the haul intent.");

        WarehouseMassAdmissionToken wrongMass = new(
            "warehouse-mass-token:qa:wrong-mass",
            request,
            acceptedQuantity: 2,
            reservedMassGrams: 1999L,
            warehouseCapacityRevision: 18L,
            expiresAtGameSeconds: 120d);
        Require(!WorldItemHaulPlanningService.ExactWarehouseAdmissionMatches(
                valid.Stack,
                exactQuantity: 2,
                token: wrongMass)
            && !WorldItemHaulPlanningService.ExactWarehouseAdmissionMatches(
                valid.Stack,
                exactQuantity: 1,
                token: exact),
            "Partial quantity or gram-mismatched admission was accepted.");

        WarehouseMassAdmissionRequest wrongTargetRequest = new(
            new BuildingInstanceId("building:qa:other"),
            "haul-delivery:qa:warehouse-admission:00",
            (ItemDefinitionId)valid.Stack.itemId,
            valid.Stack.itemInstanceId,
            lotFingerprint,
            requestedQuantity: 2,
            expectedWarehouseCapacityRevision: 17L,
            expectedCatalogRevision: 19L,
            expectedSourceRevision: 23L,
            massSubject: PhysicalItemMassSubject.ForDefinition(
                (ItemDefinitionId)valid.Stack.itemId));
        WarehouseMassAdmissionToken wrongTarget = new(
            "warehouse-mass-token:qa:wrong-target",
            wrongTargetRequest,
            acceptedQuantity: 2,
            reservedMassGrams: 2000L,
            warehouseCapacityRevision: 18L,
            expiresAtGameSeconds: 120d);
        Require(!WorldItemHaulPlanningService.ExactWarehouseAdmissionMatches(
                valid.Stack,
                exactQuantity: 2,
                token: wrongTarget),
            "Admission for a different warehouse was accepted.");
    }

    private static void VerifyPickupBoundaryRejectsInjectedInvalidation()
    {
        const string haulOperationId = "haul:qa-actor:000000000001";
        const string actorId = "qa-actor";
        const string admissionOperationId =
            "haul:qa-actor:000000000001:warehouse-admission:00";
        RouteFixture route = CreateRoute(
            "route:qa:pickup-toctou",
            TargetId,
            TargetPosition);
        Require(FacilityOutputExactRouteCustodyCodec.TryRead(
                route.Stack.components,
                out FacilityOutputExactRouteCustodyMetadata custody),
            "Pickup boundary fixture custody was malformed.");
        string signature = ItemReservationSignature.Create(
            route.Stack.itemId,
            route.Stack.components);
        WorldItemReservedStackQuantity reservation = new(
            route.Stack.stackId,
            route.Stack.itemId,
            route.Stack.quantity,
            route.Stack.position,
            WorldItemHaulDestinationKind.Warehouse,
            TargetId,
            "lease:qa:pickup-toctou",
            haulOperationId);
        ItemQuantityLease lease = new()
        {
            leaseId = reservation.LeaseId,
            ownerOperationId = haulOperationId,
            ownerCharacterId = actorId,
            purpose = ItemReservationPurpose.Hauling,
            aggregationCohortId = "haul:Warehouse:" + TargetId,
            originalQuantity = route.Stack.quantity,
            remainingQuantity = route.Stack.quantity,
            slices = new List<ItemLeaseSlice>
            {
                new()
                {
                    stackId = route.Stack.stackId,
                    originStackId = route.Stack.stackId,
                    expectedStackSignature = signature,
                    quantity = route.Stack.quantity
                }
            }
        };
        WarehouseMassAdmissionRequest request = new(
            new BuildingInstanceId("building:qa:prepared-output"),
            admissionOperationId,
            (ItemDefinitionId)route.Stack.itemId,
            route.Stack.itemInstanceId,
            signature,
            route.Stack.quantity,
            expectedWarehouseCapacityRevision: 11L,
            expectedCatalogRevision: 12L,
            expectedSourceRevision: 13L,
            massSubject: PhysicalItemMassSubject.ForDefinition(
                (ItemDefinitionId)route.Stack.itemId));
        WarehouseMassAdmissionToken token = new(
            "warehouse-mass-token:qa:pickup-toctou",
            request,
            route.Stack.quantity,
            custody.MassGrams,
            warehouseCapacityRevision: 11L,
            expiresAtGameSeconds: 120d);
        WarehouseHaulAdmissionSaveData admission =
            WorldItemHaulPlanningService.CreateWarehouseAdmissionProjection(
                route.Stack,
                admissionOperationId,
                token);
        HaulDeliveryIntentSaveData intent = new()
        {
            operationId = haulOperationId,
            ownerCharacterId = actorId,
            destinationKind = WorldItemHaulDestinationKind.Warehouse,
            destinationId = TargetId,
            deliveryGridX = TargetPosition.x + 1,
            deliveryGridY = TargetPosition.y,
            dropGridX = TargetPosition.x + 1,
            dropGridY = TargetPosition.y,
            warehouseAdmissions = new List<WarehouseHaulAdmissionSaveData>
            {
                admission
            },
            commitments = new List<HaulDeliveryItemCommitmentSaveData>()
        };
        PreparedOutputExactDestinationAuthoritySnapshot authority =
            new(
                PreparedOutputExactDestinationTargetKind.Warehouse,
                TargetId,
                TargetPosition,
                custody.CurrentTargetAuthorityFingerprint,
                capacityRevision: 11L,
                massAuthorityRevision: 12L,
                maxMassGrams: 100000L,
                reservedMassGrams: custody.MassGrams);

        PreparedOutputPickupBoundaryResult valid =
            WorldItemWarehouseService.ValidatePreparedOutputPickupBoundarySnapshot(
                reservation,
                lease,
                route.Stack,
                route.Stack.quantity,
                custody.MassGrams,
                custody,
                intent,
                admission,
                new WarehouseMassAdmissionStatusSnapshot(
                    token,
                    WarehouseMassAdmissionTokenStatus.Reserved,
                    default),
                hasCurrentDeliveryProjection: true,
                currentDeliveryPosition: new Vector2Int(
                    TargetPosition.x + 1,
                    TargetPosition.y),
                hasCurrentAuthority: true,
                authority);
        Require(!valid.IsFailure,
            "Fresh exact pickup authority was rejected: " + valid);

        int sourceQuantityBefore = route.Stack.quantity;
        WorldItemStackState sourceStateBefore = route.Stack.state;
        string componentSignatureBefore = ItemReservationSignature.Create(
            route.Stack.itemId,
            route.Stack.components);
        int leaseQuantityBefore = lease.remainingQuantity;

        // Models invalidation after AbilityHaul renewed its leases but before
        // ItemTransferService entered its source-mutation boundary.
        PreparedOutputPickupBoundaryResult invalidated =
            WorldItemWarehouseService.ValidatePreparedOutputPickupBoundarySnapshot(
                reservation,
                lease,
                route.Stack,
                route.Stack.quantity,
                custody.MassGrams,
                custody,
                intent,
                admission,
                new WarehouseMassAdmissionStatusSnapshot(
                    token,
                    WarehouseMassAdmissionTokenStatus.Invalidated,
                    WarehouseMassAdmissionReleaseReason.DestinationInvalidated),
                hasCurrentDeliveryProjection: true,
                currentDeliveryPosition: new Vector2Int(
                    TargetPosition.x + 1,
                    TargetPosition.y),
                hasCurrentAuthority: true,
                authority);
        Require(invalidated.Code ==
                PreparedOutputPickupBoundaryFailureCode.AdmissionNotReserved,
            "Injected destination invalidation did not fail typed at pickup: "
            + invalidated);
        Require(route.Stack.quantity == sourceQuantityBefore
                && route.Stack.state == sourceStateBefore
                && lease.remainingQuantity == leaseQuantityBefore
                && string.Equals(
                    ItemReservationSignature.Create(
                        route.Stack.itemId,
                        route.Stack.components),
                    componentSignatureBefore,
                    StringComparison.Ordinal),
            "Failed pickup boundary changed source, components or lease.");

        PreparedOutputExactDestinationAuthoritySnapshot staleAuthority = new(
            PreparedOutputExactDestinationTargetKind.Warehouse,
            TargetId,
            TargetPosition,
            Sha('9'),
            capacityRevision: 12L,
            massAuthorityRevision: 12L,
            maxMassGrams: 100000L,
            reservedMassGrams: custody.MassGrams);
        PreparedOutputPickupBoundaryResult authorityInvalidated =
            WorldItemWarehouseService.ValidatePreparedOutputPickupBoundarySnapshot(
                reservation,
                lease,
                route.Stack,
                route.Stack.quantity,
                custody.MassGrams,
                custody,
                intent,
                admission,
                new WarehouseMassAdmissionStatusSnapshot(
                    token,
                    WarehouseMassAdmissionTokenStatus.Reserved,
                    default),
                hasCurrentDeliveryProjection: true,
                currentDeliveryPosition: new Vector2Int(
                    TargetPosition.x + 1,
                    TargetPosition.y),
                hasCurrentAuthority: true,
                staleAuthority);
        Require(authorityInvalidated.Code ==
                PreparedOutputPickupBoundaryFailureCode
                    .DestinationAuthorityStale,
            "Stale live destination authority crossed the pickup boundary.");
    }

    private static RouteFixture CreateRoute(
        string routeOperationId,
        string targetDestinationId,
        Vector2Int targetPosition,
        string stackId = null)
    {
        string routedStackId = stackId
            ?? $"item-stack:qa:{routeOperationId.Replace(':', '-')}";
        string requestFingerprint = Sha('1');
        string physicalFingerprint = Sha(
            routeOperationId.EndsWith("second", StringComparison.Ordinal)
                ? '3'
                : '2');
        FacilityOutputExactRouteSliceReceipt slice = new(
            "item-stack:qa:source",
            routedStackId,
            "output:main",
            "line-commit:qa",
            ItemId,
            0,
            0,
            2,
            2000L,
            Sha('4'));
        FacilityOutputExactRouteReceipt receipt = new(
            routeOperationId,
            requestFingerprint,
            physicalFingerprint,
            "batch-commit:qa",
            "production:qa:output",
            targetDestinationId,
            targetPosition,
            2,
            2000L,
            new[] { slice });
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery =
            targetDestinationId.Length == 0
                ? FacilityOutputExactRouteDeliveryRevisionSnapshot.CreateInitial(
                    routeOperationId,
                    requestFingerprint,
                    physicalFingerprint,
                    targetDestinationId,
                    targetPosition.x,
                    targetPosition.y)
                : new FacilityOutputExactRouteDeliveryRevisionSnapshot(
                    routeOperationId,
                    physicalFingerprint,
                    revision: 1L,
                    revisionFingerprint: Sha('7'),
                    rerouteOperationId: "production-output-delivery-reroute:qa",
                    targetDestinationId: targetDestinationId,
                    targetPositionX: targetPosition.x,
                    targetPositionY: targetPosition.y,
                    targetAuthorityFingerprint: Sha('8'));
        FacilityOutputExactRoutePendingSnapshot snapshot = new(
            FacilityOutputExactRoutePhase.Routable,
            receipt,
            delivery);
        RouteFixture result = new(
            new WorldItemStackRecord
            {
                stackId = routedStackId,
                itemId = ItemId,
                quantity = 2,
                state = WorldItemStackState.Loose,
                destinationId = targetDestinationId,
                hasDestinationPosition = targetDestinationId.Length > 0,
                destinationPosition = targetPosition,
                components = new List<ItemInstanceComponentSaveData>()
            },
            snapshot);
        FacilityOutputExactRouteCustodyMetadata custody =
            CreateBaseCustody(result, FacilityOutputExactRouteCustodyPhase.OriginBuffered)
            .WithSlice(
                FacilityOutputExactRouteCustodyPhase.Routable,
                targetDestinationId,
                routedStackId,
                0,
                2,
                2000L,
                routeOperationId,
                requestFingerprint,
                physicalFingerprint,
                delivery);
        result.Stack.components.Add(
            FacilityOutputExactRouteCustodyCodec.Create(custody));
        return result;
    }

    private static FacilityOutputExactRouteCustodyMetadata CreateBaseCustody(
        RouteFixture fixture,
        FacilityOutputExactRouteCustodyPhase phase) => new(
        phase,
        "batch-commit:qa",
        Sha('5'),
        Sha('6'),
        "output:main",
        "line-commit:qa",
        0,
        1,
        2,
        2000L,
        1,
        2,
        2000L,
        ItemId,
        string.Empty,
        Sha('4'),
        "production:qa:output",
        fixture.Snapshot.Receipt.TargetDestinationId,
        "item-stack:qa:origin",
        fixture.Stack.stackId,
        new Vector2Int(3, 4),
        0,
        2,
        2000L,
        string.Empty,
        string.Empty,
        string.Empty);

    private static WorldItemStackRecord CloneOrdinary(
        WorldItemStackRecord source,
        string stackId) => new()
    {
        stackId = stackId,
        itemId = source.itemId,
        quantity = source.quantity,
        state = source.state,
        destinationId = source.destinationId,
        hasDestinationPosition = source.hasDestinationPosition,
        destinationPosition = source.destinationPosition,
        components = (source.components ?? new List<ItemInstanceComponentSaveData>())
            .Select(value => value.Clone())
            .ToList()
    };

    private static string Sha(char value) => new string(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RouteFixture
    {
        internal RouteFixture(
            WorldItemStackRecord stack,
            FacilityOutputExactRoutePendingSnapshot snapshot)
        {
            Stack = stack;
            Snapshot = snapshot;
        }

        internal WorldItemStackRecord Stack { get; }
        internal FacilityOutputExactRoutePendingSnapshot Snapshot { get; }
    }

    private sealed class ProvenanceAggregationCatalogProvider :
        IDungeonItemCatalogProvider
    {
        private readonly DungeonItemDefinition definition = new(
            "item:buffer",
            "Buffer Item",
            "Prepared-output provenance aggregation fixture",
            StockCategory.General,
            5,
            null,
            1f,
            75);

        public IReadOnlyList<DungeonItemDefinition> All =>
            new[] { definition };

        public DungeonItemDefinition GetDefinition(string itemId) =>
            TryGetDefinition(itemId, out DungeonItemDefinition resolved)
                ? resolved
                : throw new KeyNotFoundException(
                    $"Unknown provenance aggregation item '{itemId}'.");

        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition resolved)
        {
            resolved = string.Equals(
                itemId,
                definition.ItemId,
                StringComparison.Ordinal)
                ? definition
                : null;
            return resolved != null;
        }
    }

    private sealed class FixedQuery : IFacilityOutputExactRouteOutboxQuery
    {
        private readonly IReadOnlyList<FacilityOutputExactRoutePendingSnapshot>
            routes;

        internal FixedQuery(params FacilityOutputExactRoutePendingSnapshot[] routes)
        {
            this.routes = routes ?? Array.Empty<FacilityOutputExactRoutePendingSnapshot>();
        }

        public IReadOnlyList<FacilityOutputExactRoutePendingSnapshot>
            CapturePendingRoutes() => routes;
    }

    private sealed class FixedAdmissionAuthority :
        IPreparedOutputExactDestinationAdmissionParticipant
    {
        private readonly PreparedOutputExactDestinationAuthoritySnapshot snapshot;

        private FixedAdmissionAuthority(
            PreparedOutputExactDestinationAuthoritySnapshot snapshot)
        {
            this.snapshot = snapshot;
        }

        internal static FixedAdmissionAuthority Matching(
            string destinationId,
            Vector2Int position,
            string fingerprint) => new(
            new PreparedOutputExactDestinationAuthoritySnapshot(
                PreparedOutputExactDestinationTargetKind.Warehouse,
                destinationId,
                position,
                fingerprint,
                capacityRevision: 11L,
                massAuthorityRevision: 12L,
                maxMassGrams: 100000L,
                reservedMassGrams: 2000L));

        public string ParticipantId => "items.qa.fixed-admission-authority";

        public bool TryCaptureTargetAuthority(
            PreparedOutputExactDestinationTargetKind kind,
            string destinationId,
            Vector2Int position,
            out PreparedOutputExactDestinationAuthoritySnapshot captured,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason)
        {
            captured = snapshot;
            failureCode = PreparedOutputExactDestinationAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }

        public bool TryPrepare(
            PreparedOutputExactDestinationAdmissionRequest request,
            out PreparedOutputExactDestinationAdmissionCandidate candidate,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason) => Unsupported(
            out candidate,
            out failureCode,
            out failureReason);

        public bool TryPublish(
            PreparedOutputExactDestinationAdmissionCandidate candidate,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason) => Unsupported(
            out failureCode,
            out failureReason);

        public bool TryRollback(
            PreparedOutputExactDestinationAdmissionCandidate candidate,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason) => Unsupported(
            out failureCode,
            out failureReason);

        public bool TryComplete(
            PreparedOutputExactDestinationAdmissionCandidate candidate,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason) => Unsupported(
            out failureCode,
            out failureReason);

        private static bool Unsupported(
            out PreparedOutputExactDestinationAdmissionCandidate candidate,
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason)
        {
            candidate = null;
            return Unsupported(out failureCode, out failureReason);
        }

        private static bool Unsupported(
            out PreparedOutputExactDestinationAdmissionFailureCode failureCode,
            out string failureReason)
        {
            failureCode =
                PreparedOutputExactDestinationAdmissionFailureCode.InvalidRequest;
            failureReason = "focused authority fixture does not mutate admission";
            return false;
        }
    }
}
#endif
