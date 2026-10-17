#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using UnityEngine;

public static class WorldItemDestinationAdmissionDebugScenarios
{
    private const string ItemId = "material:lumber";
    private const string Destination = "production-input:building:qa:destination-admission";
    private const string PreviousDestination = "warehouse:building:qa:previous-output";
    private const string RouteOperation = "route:qa:destination-admission";

    public static void RunForBatch()
    {
        Require(Application.isBatchMode, "Destination admission validation requires isolated batch execution.");
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(catalog);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(), new DungeonRuntimeAggregateRootStore());
        ItemQuantityReservationService reservations = new(
            repository, EditorNullItemMarkerPresenter.Instance, new UnityGameClock());
        PhysicalStockQuery stock = new(repository, catalog, mass);
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        WarehouseMassAdmissionService warehouseMass = new(
            catalog, mass, stock, world, new UnityGameClock(), repository);
        FacilityBufferPhysicalOccupancyQuery occupancy = new(repository, mass, reservations);
        FacilityBufferDestinationClaimRegistry claims = new();
        FacilityBufferMassAdmissionService facilityMass = new(claims, occupancy);
        long exactMass = mass.GetQuantityMass(
            (ItemDefinitionId)ItemId,
            PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)ItemId), 2).Value;
        Vector2Int position = new(13, 7);
        const string owner = "economy.production-input";
        FacilityBufferCapacityProfile profile = new(
            Destination, position, owner, Destination,
            "building:qa:destination-admission", new PhysicalMassGrams(exactMass),
            capacityRevision: 1L);
        Require(claims.TryClaim(new FacilityBufferDestinationClaim(
                Destination, position, owner, Destination,
                "building:qa:destination-admission",
                FacilityBufferDestinationAnchorKind.LiveBuilding), out _, out _)
            && facilityMass.TryReplaceOwnedProfiles(owner, new[] { profile }, out _, out _),
            "Destination admission fixture could not publish its real capacity authority.");
        WorldItemDestinationAdmissionService service = new(
            catalog, repository, world, new NoGrid(), warehouseMass, facilityMass);
        Require(service.TryCapturePreparedOutputAuthority(
                PreparedOutputExactDestinationTargetKind.FacilityBuffer,
                Destination, position, out var authority, out _, out _),
            "Destination capacity authority capture failed.");
        string stackId = repository.AddEditorTestStack(
            ItemId, 2, WorldItemStackState.Loose, PreviousDestination);
        WorldItemStackRecord source = repository.RecordsById[stackId];
        SetPublication(published: false);
        PreparedOutputExactDestinationAdmissionRequest request = new(
            "admission:qa:destination-admission", RouteOperation, Sha('1'), Sha('3'),
            new[] { new PreparedOutputExactDestinationLotSlice(
                stackId, 2, source.reservationRevision, Sha('4'), exactMass) }, authority);
        string blocker = repository.AddEditorTestStack(
            ItemId, 1, WorldItemStackState.FacilityBuffer, Destination, position: position);
        string before = Signature();
        Require(before.Contains(stackId, StringComparison.Ordinal),
            "Physical comparison did not capture the actual stack fields.");
        Require(!service.TryPreparePreparedOutputAdmission(request, out _,
                out var capacityFailure, out _)
            && capacityFailure == PreparedOutputExactDestinationAdmissionFailureCode.CapacityUnavailable
            && Signature() == before && Reserved() == 0,
            "Capacity rejection changed physical source or reserved grams.");
        repository.RemoveEditorTestStack(blocker);
        repository.SetEditorTestQuantity(stackId, 1);
        before = Signature();
        Require(!service.TryPreparePreparedOutputAdmission(request, out _,
                out var sourceFailure, out _)
            && sourceFailure == PreparedOutputExactDestinationAdmissionFailureCode.SourceChanged
            && Signature() == before && Reserved() == 0,
            "Mismatched source entered admission or changed live stock.");
        repository.SetEditorTestQuantity(stackId, 2);
        before = Signature();
        PreparedOutputExactDestinationAdmissionRequest cancellationRequest = new(
            "admission:qa:destination-admission:cancelled", request.ExpectedRouteOperationId,
            request.ExpectedPhysicalReceiptFingerprint,
            request.ExpectedNextDeliveryRevisionFingerprint,
            request.ExactLotSlices, request.TargetAuthority);
        Require(service.TryPreparePreparedOutputAdmission(cancellationRequest, out var cancelled, out _, out _)
            && Reserved() == exactMass && Signature() == before
            && service.TryRollbackPreparedOutputAdmission(cancelled, false, out _)
            && Reserved() == 0 && Signature() == before,
            "Prepared admission cancellation did not release grams without changing source.");
        Require(!service.TryPreparePreparedOutputAdmission(cancellationRequest, out _,
                out var cancelledFailure, out _)
            && cancelledFailure == PreparedOutputExactDestinationAdmissionFailureCode.ReplayConflict
            && Reserved() == 0 && Signature() == before,
            "Released transfer operation was reused or changed physical source.");
        Require(service.TryPreparePreparedOutputAdmission(request, out var handle, out _, out _),
            "Same-request retry after capacity/source rejection failed.");
        Require(service.TryPreparePreparedOutputAdmission(request, out var repeated, out _, out _)
            && repeated.FacilityToken.TokenId == handle.FacilityToken.TokenId
            && repeated.Fingerprint == handle.Fingerprint
            && Reserved() == exactMass && Signature() == before,
            "Repeated prepare duplicated the active reservation.");
        Require(!service.TryPublishPreparedOutputAdmission(request, handle, out _,
                out var unpublishedFailure, out _)
            && unpublishedFailure == PreparedOutputExactDestinationAdmissionFailureCode.SourceChanged
            && Reserved() == exactMass && Signature() == before,
            "Admission published before the source carried the target delivery revision.");
        SetPublication(published: true);
        before = Signature();
        Require(service.TryPublishPreparedOutputAdmission(request, handle,
                out long committedMass, out _, out _)
            && committedMass == exactMass && Reserved() == 0 && Signature() == before
            && service.TryPublishPreparedOutputAdmission(request, handle,
                out long replayMass, out _, out _)
            && replayMass == exactMass && Signature() == before
            && service.TryRollbackPreparedOutputAdmission(handle, true, out _)
            && !facilityMass.TryGetReceipt(handle.FacilityToken.TokenId, out _),
            "Admission publish/replay/routed rollback changed source or lost its exact receipt.");
        // The surrounding route owner restores its own publication after admission rollback.
        SetPublication(published: false);
        Require(service.TryPreparePreparedOutputAdmission(request, out var retried, out _, out _),
            "Same-request retry after routed rollback failed.");
        SetPublication(published: true);
        Require(service.TryPublishPreparedOutputAdmission(request, retried,
                out committedMass, out _, out _)
            && committedMass == exactMass && Reserved() == 0
            && occupancy.Capture(Destination).NonCarriedMassGrams == exactMass
            && repository.RecordsById.Count == 1 && source.quantity == 2,
            "Admission retry left duplicate, missing, or unaccounted physical quantity.");
        Debug.Log("ARCHITECTURE_DESTINATION_ADMISSION=PASS; real repository/mass/capacity; source and capacity rejection unchanged; prepublication rejection; cancel and terminal ID rejection; prepare replay; publish/replay; routed rollback and same-request retry");

        string Signature() => string.Join("\n", repository.RecordsById.Values
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .Select(value => JsonConvert.SerializeObject(value)));

        long Reserved()
        {
            Require(facilityMass.TryGetCapacity(Destination, position, out var snapshot),
                "Fixture destination capacity disappeared.");
            return snapshot.ReservedMassGrams;
        }

        void SetPublication(bool published)
        {
            FacilityOutputExactRouteCustodyMetadata custody = new(
                FacilityOutputExactRouteCustodyPhase.Routable,
                "batch:qa:destination-admission", Sha('5'), Sha('6'),
                "line:qa:destination-admission", "line-commit:qa:destination-admission",
                0, 1, 2, exactMass, 1, 2, exactMass, ItemId,
                FacilityBufferPlannedOutputPublicationService.CreateRuntimeComponentSignature(
                    Array.Empty<ItemInstanceComponentSaveData>()),
                Sha('4'), "production-output:building:qa:origin", PreviousDestination,
                stackId, stackId, Vector2Int.zero, 0, 2, exactMass,
                RouteOperation, Sha('7'), Sha('1'),
                currentDeliveryRevision: published ? 1L : 0L,
                currentDeliveryRevisionFingerprint: published ? Sha('3') : Sha('2'),
                currentDeliveryRerouteOperationId: published ? "reroute:qa:destination-admission" : "",
                currentTargetDestinationId: published ? Destination : PreviousDestination,
                currentTargetPositionX: published ? position.x : 0,
                currentTargetPositionY: published ? position.y : 0,
                currentTargetAuthorityFingerprint: published ? authority.Fingerprint : "");
            Require(repository.SetEditorTestComponent(stackId,
                    FacilityOutputExactRouteCustodyCodec.Create(custody))
                && FacilityOutputExactRouteCustodyCodec.TryRead(source.components, out _),
                "Controlled route publication produced malformed custody.");
            source.destinationId = published ? Destination : PreviousDestination;
            source.hasDestinationPosition = published;
            source.destinationPosition = published ? position : default;
            repository.MarkChanged();
        }
    }

    private static string Sha(char value) => new(value, 64);
    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class NoGrid : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;
        public bool TryGetManager(out GridSystemManager manager) { manager = null; return false; }
        public bool TryGetGrid(out Grid grid) { grid = null; return false; }
    }
}
#endif
