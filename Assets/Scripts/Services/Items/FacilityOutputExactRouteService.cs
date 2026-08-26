using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class FacilityOutputExactRouteService :
    IFacilityOutputExactRoutePort,
    IFacilityOutputExactRouteOutboxPersistence,
    IFacilityOutputExactRouteDeliveryOverlayParticipant,
    IFacilityOutputExactRouteRestoreReconciler,
    IFacilityOutputExactRouteDestructiveRetirePort,
    IPreparedOutputCheckpointGcParticipant,
    IDungeonRestoreTransactionParticipant
{
    private sealed class SourceSegment
    {
        internal WorldItemStackRecord Record;
        internal FacilityOutputExactRouteCustodyMetadata Metadata;
    }

    private sealed class SelectedRange
    {
        internal SourceSegment Source;
        internal FacilityOutputExactRouteSliceRequest Request;
        internal int Start;
        internal int Quantity;
        internal long MassGrams;
        internal string RoutedStackId;
    }

    private sealed class RecordPart
    {
        internal SourceSegment Source;
        internal int Start;
        internal int Quantity;
        internal long MassGrams;
        internal bool Routed;
        internal string StackId;
        internal FacilityOutputExactRouteSliceRequest Request;
    }

    private sealed class RestoredCustodyPart
    {
        internal WorldItemStackSaveData Stack;
        internal FacilityOutputExactRouteCustodyMetadata Metadata;
    }

    private sealed class PreparedRestoreState :
        IFacilityOutputExactRoutePreparedRestoreState
    {
        internal PreparedRestoreState(
            Dictionary<string, FacilityOutputExactRoutePendingSnapshot> routes,
            long checkpointSequence,
            string checkpointDigest)
        {
            Routes = routes ?? throw new ArgumentNullException(nameof(routes));
            CheckpointSequence = checkpointSequence;
            CheckpointDigest = checkpointDigest ?? string.Empty;
        }

        internal Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
            Routes { get; }
        internal long CheckpointSequence { get; }
        internal string CheckpointDigest { get; }
    }

    private sealed class CheckpointGcCandidate :
        IPreparedOutputCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            PreparedOutputCheckpointGcContext context,
            int sourceRepositoryVersion,
            long sourceOutboxRevision,
            Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
                previousRoutes,
            long previousSequence,
            string previousDigest,
            Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
                nextRoutes,
            IReadOnlyDictionary<string,
                IReadOnlyList<ItemInstanceComponentSaveData>> originals,
            IReadOnlyDictionary<string,
                IReadOnlyList<ItemInstanceComponentSaveData>> stripped,
            IReadOnlyDictionary<string, GcPhysicalInvariant> invariants,
            IReadOnlyList<string> batchCommitIds,
            IReadOnlyList<string> routeOperationIds)
        {
            Context = context;
            SourceRepositoryVersion = sourceRepositoryVersion;
            SourceOutboxRevision = sourceOutboxRevision;
            PreviousRoutes = previousRoutes
                ?? throw new ArgumentNullException(nameof(previousRoutes));
            PreviousSequence = previousSequence;
            PreviousDigest = previousDigest ?? string.Empty;
            NextRoutes = nextRoutes
                ?? throw new ArgumentNullException(nameof(nextRoutes));
            Originals = originals
                ?? throw new ArgumentNullException(nameof(originals));
            Stripped = stripped
                ?? throw new ArgumentNullException(nameof(stripped));
            Invariants = invariants
                ?? throw new ArgumentNullException(nameof(invariants));
            BatchCommitIds = batchCommitIds ?? Array.Empty<string>();
            RouteOperationIds = routeOperationIds ?? Array.Empty<string>();
        }

        public string ParticipantId =>
            "998.world.facility-output-exact-route-outbox";
        public PreparedOutputCheckpointGcParticipantKind ParticipantKind =>
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority;
        public long CheckpointSequence => Context.CheckpointSequence;
        public string SerializedByteDigest => Context.SerializedByteDigest;
        public IReadOnlyList<string> BatchCommitIds { get; }
        public IReadOnlyList<string> RouteOperationIds { get; }
        internal PreparedOutputCheckpointGcContext Context { get; }
        internal int SourceRepositoryVersion { get; }
        internal long SourceOutboxRevision { get; }
        internal Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
            PreviousRoutes { get; }
        internal long PreviousSequence { get; }
        internal string PreviousDigest { get; }
        internal Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
            NextRoutes { get; }
        internal IReadOnlyDictionary<string,
            IReadOnlyList<ItemInstanceComponentSaveData>> Originals { get; }
        internal IReadOnlyDictionary<string,
            IReadOnlyList<ItemInstanceComponentSaveData>> Stripped { get; }
        internal IReadOnlyDictionary<string, GcPhysicalInvariant> Invariants { get; }
        internal int PublishedRepositoryVersion { get; set; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }

    private sealed class DestructiveRetireCandidate :
        IFacilityOutputExactRouteDestructiveRetireCandidate
    {
        internal DestructiveRetireCandidate(
            string sourceDestinationId,
            string batchCommitId,
            string candidateFingerprint,
            int sourceRepositoryVersion,
            long sourceOutboxRevision,
            Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
                previousRoutes,
            Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
                nextRoutes,
            IReadOnlyDictionary<string,
                IReadOnlyList<ItemInstanceComponentSaveData>> originals,
            IReadOnlyDictionary<string,
                IReadOnlyList<ItemInstanceComponentSaveData>> stripped,
            IReadOnlyDictionary<string, GcPhysicalInvariant> invariants,
            IReadOnlyList<string> routeOperationIds,
            IReadOnlyList<string> physicalStackIds)
        {
            SourceDestinationId = sourceDestinationId;
            BatchCommitId = batchCommitId;
            CandidateFingerprint = candidateFingerprint;
            SourceRepositoryVersion = sourceRepositoryVersion;
            SourceOutboxRevision = sourceOutboxRevision;
            PreviousRoutes = previousRoutes;
            NextRoutes = nextRoutes;
            Originals = originals;
            Stripped = stripped;
            Invariants = invariants;
            RouteOperationIds = Array.AsReadOnly(routeOperationIds.ToArray());
            PhysicalStackIds = Array.AsReadOnly(physicalStackIds.ToArray());
        }

        public string SourceDestinationId { get; }
        public string BatchCommitId { get; }
        public string CandidateFingerprint { get; }
        public IReadOnlyList<string> RouteOperationIds { get; }
        public IReadOnlyList<string> PhysicalStackIds { get; }
        internal int SourceRepositoryVersion { get; }
        internal long SourceOutboxRevision { get; }
        internal Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
            PreviousRoutes { get; }
        internal Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
            NextRoutes { get; }
        internal IReadOnlyDictionary<string,
            IReadOnlyList<ItemInstanceComponentSaveData>> Originals { get; }
        internal IReadOnlyDictionary<string,
            IReadOnlyList<ItemInstanceComponentSaveData>> Stripped { get; }
        internal IReadOnlyDictionary<string, GcPhysicalInvariant> Invariants { get; }
        internal int PublishedRepositoryVersion { get; set; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }

    private sealed class GcPhysicalInvariant
    {
        internal GcPhysicalInvariant(WorldItemStackRecord record)
        {
            StackId = record.stackId;
            ItemInstanceId = record.itemInstanceId;
            ItemId = record.itemId;
            Quantity = record.quantity;
            State = record.state;
            Position = record.position;
            ReservedByPersistentId = record.reservedByPersistentId;
            ReservedQuantity = record.reservedQuantity;
            ReservationRevision = record.reservationRevision;
            DestinationId = record.destinationId;
            AggregationCohortId = record.aggregationCohortId;
            SourceStorageDestinationId = record.sourceStorageDestinationId;
            HasDestinationPosition = record.hasDestinationPosition;
            DestinationPosition = record.destinationPosition;
            DropDisposition = record.dropDisposition;
            RecoveryOwnerOperationId = record.recoveryOwnerOperationId;
            RecoverySourceStackId = record.recoverySourceStackId;
            RecoveryCarrierPersistentId = record.recoveryCarrierPersistentId;
            RecoveryInterruptionKind = record.recoveryInterruptionKind;
            DroppedAtGameTime = record.droppedAtGameTime;
            RecoveryDeadlineGameTime = record.recoveryDeadlineGameTime;
            Forbidden = record.forbidden;
            SourceCharacterId = record.sourceCharacterId;
            SourceDisplayName = record.sourceDisplayName;
            SourceSpeciesTag = record.sourceSpeciesTag;
            SourceDeathReason = record.sourceDeathReason;
            EmergencyButcheryAllowed = record.emergencyButcheryAllowed;
            WasteOrigin = record.wasteOrigin;
            Contamination = record.contamination;
        }

        internal string StackId { get; }
        internal string ItemInstanceId { get; }
        internal string ItemId { get; }
        internal int Quantity { get; }
        internal WorldItemStackState State { get; }
        internal Vector2Int Position { get; }
        internal string ReservedByPersistentId { get; }
        internal int ReservedQuantity { get; }
        internal long ReservationRevision { get; }
        internal string DestinationId { get; }
        internal string AggregationCohortId { get; }
        internal string SourceStorageDestinationId { get; }
        internal bool HasDestinationPosition { get; }
        internal Vector2Int DestinationPosition { get; }
        internal WorldItemDropDisposition DropDisposition { get; }
        internal string RecoveryOwnerOperationId { get; }
        internal string RecoverySourceStackId { get; }
        internal string RecoveryCarrierPersistentId { get; }
        internal WorldItemCarryInterruptionKind RecoveryInterruptionKind { get; }
        internal double DroppedAtGameTime { get; }
        internal double RecoveryDeadlineGameTime { get; }
        internal bool Forbidden { get; }
        internal string SourceCharacterId { get; }
        internal string SourceDisplayName { get; }
        internal string SourceSpeciesTag { get; }
        internal string SourceDeathReason { get; }
        internal bool EmergencyButcheryAllowed { get; }
        internal WasteOriginKind WasteOrigin { get; }
        internal float Contamination { get; }

        internal bool Matches(WorldItemStackRecord record) => record != null
            && string.Equals(StackId, record.stackId, StringComparison.Ordinal)
            && string.Equals(ItemInstanceId, record.itemInstanceId,
                StringComparison.Ordinal)
            && string.Equals(ItemId, record.itemId, StringComparison.Ordinal)
            && Quantity == record.quantity
            && State == record.state
            && Position == record.position
            && string.Equals(ReservedByPersistentId,
                record.reservedByPersistentId, StringComparison.Ordinal)
            && ReservedQuantity == record.reservedQuantity
            && ReservationRevision == record.reservationRevision
            && string.Equals(DestinationId, record.destinationId,
                StringComparison.Ordinal)
            && string.Equals(AggregationCohortId,
                record.aggregationCohortId, StringComparison.Ordinal)
            && string.Equals(SourceStorageDestinationId,
                record.sourceStorageDestinationId, StringComparison.Ordinal)
            && HasDestinationPosition == record.hasDestinationPosition
            && DestinationPosition == record.destinationPosition
            && DropDisposition == record.dropDisposition
            && string.Equals(RecoveryOwnerOperationId,
                record.recoveryOwnerOperationId, StringComparison.Ordinal)
            && string.Equals(RecoverySourceStackId,
                record.recoverySourceStackId, StringComparison.Ordinal)
            && string.Equals(RecoveryCarrierPersistentId,
                record.recoveryCarrierPersistentId, StringComparison.Ordinal)
            && RecoveryInterruptionKind == record.recoveryInterruptionKind
            && DroppedAtGameTime.Equals(record.droppedAtGameTime)
            && RecoveryDeadlineGameTime.Equals(record.recoveryDeadlineGameTime)
            && Forbidden == record.forbidden
            && string.Equals(SourceCharacterId,
                record.sourceCharacterId, StringComparison.Ordinal)
            && string.Equals(SourceDisplayName,
                record.sourceDisplayName, StringComparison.Ordinal)
            && string.Equals(SourceSpeciesTag,
                record.sourceSpeciesTag, StringComparison.Ordinal)
            && string.Equals(SourceDeathReason,
                record.sourceDeathReason, StringComparison.Ordinal)
            && EmergencyButcheryAllowed == record.emergencyButcheryAllowed
            && WasteOrigin == record.wasteOrigin
            && Contamination.Equals(record.contamination);
    }

    private sealed class DeliveryOverlayCandidate :
        IFacilityOutputExactRouteDeliveryOverlayCandidate
    {
        internal DeliveryOverlayCandidate(
            FacilityOutputExactRouteDeliveryOverlayStatus status,
            FacilityOutputExactRouteDeliveryOverlayReason reason,
            string message,
            string routeOperationId,
            long expectedCurrentRevision,
            string expectedCurrentRevisionFingerprint,
            FacilityOutputExactRouteDeliveryRevisionSnapshot next,
            IReadOnlyList<FacilityOutputExactRouteDeliverySubjectSnapshot>
                deliverySubjects = null,
            int sourceRepositoryVersion = 0,
            long sourceOutboxRevision = 0L,
            Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
                previousRoutes = null,
            Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
                nextRoutes = null,
            IReadOnlyDictionary<string,
                IReadOnlyList<ItemInstanceComponentSaveData>> originals = null,
            IReadOnlyDictionary<string,
                IReadOnlyList<ItemInstanceComponentSaveData>> replacements = null,
            IReadOnlyDictionary<string, DeliveryPhysicalOriginal>
                physicalOriginals = null)
        {
            Status = status;
            Reason = reason;
            Message = message ?? string.Empty;
            RouteOperationId = routeOperationId ?? string.Empty;
            ExpectedCurrentRevision = expectedCurrentRevision;
            ExpectedCurrentRevisionFingerprint =
                expectedCurrentRevisionFingerprint ?? string.Empty;
            Next = next;
            DeliverySubjects = Array.AsReadOnly((deliverySubjects
                    ?? Array.Empty<FacilityOutputExactRouteDeliverySubjectSnapshot>())
                .ToArray());
            SourceRepositoryVersion = sourceRepositoryVersion;
            SourceOutboxRevision = sourceOutboxRevision;
            PreviousRoutes = previousRoutes;
            NextRoutes = nextRoutes;
            Originals = originals;
            Replacements = replacements;
            PhysicalOriginals = physicalOriginals;
        }

        public FacilityOutputExactRouteDeliveryOverlayStatus Status { get; }
        public FacilityOutputExactRouteDeliveryOverlayReason Reason { get; }
        public string Message { get; }
        public string RouteOperationId { get; }
        public long ExpectedCurrentRevision { get; }
        public string ExpectedCurrentRevisionFingerprint { get; }
        public FacilityOutputExactRouteDeliveryRevisionSnapshot Next { get; }
        public IReadOnlyList<FacilityOutputExactRouteDeliverySubjectSnapshot>
            DeliverySubjects { get; }
        internal int SourceRepositoryVersion { get; }
        internal long SourceOutboxRevision { get; }
        internal Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
            PreviousRoutes { get; }
        internal Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
            NextRoutes { get; }
        internal IReadOnlyDictionary<string,
            IReadOnlyList<ItemInstanceComponentSaveData>> Originals { get; }
        internal IReadOnlyDictionary<string,
            IReadOnlyList<ItemInstanceComponentSaveData>> Replacements { get; }
        internal IReadOnlyDictionary<string, DeliveryPhysicalOriginal>
            PhysicalOriginals { get; }
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
        internal int PublishedRepositoryVersion { get; set; }
    }

    private sealed class DeliveryPhysicalOriginal
    {
        internal DeliveryPhysicalOriginal(WorldItemStackRecord record)
        {
            DestinationId = record.destinationId;
            HasDestinationPosition = record.hasDestinationPosition;
            DestinationPosition = record.destinationPosition;
        }

        internal string DestinationId { get; }
        internal bool HasDestinationPosition { get; }
        internal Vector2Int DestinationPosition { get; }

        internal void Restore(WorldItemStackRecord record)
        {
            record.destinationId = DestinationId;
            record.hasDestinationPosition = HasDestinationPosition;
            record.destinationPosition = DestinationPosition;
        }
    }

    private readonly WorldItemRepository repository;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IItemMarkerPresenter markers;
    private Dictionary<string, FacilityOutputExactRoutePendingSnapshot> routes =
        new(StringComparer.Ordinal);
    private Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
        stagedRoutes;
    private Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
        previousRoutes;
    private readonly Dictionary<string,
        IReadOnlyList<ItemInstanceComponentSaveData>> restoreComponentOriginals =
        new(StringComparer.Ordinal);
    private bool restoreActive;
    private bool restorePublished;
    private long previousRestoreOutboxRevision;
    private long stagedCheckpointSequence;
    private string stagedCheckpointDigest = string.Empty;
    private long previousRestoreCheckpointSequence;
    private string previousRestoreCheckpointDigest = string.Empty;
    private long outboxRevision;
    private long checkpointSequence;
    private string checkpointDigest = string.Empty;
    private DeliveryOverlayCandidate activeDeliveryOverlayCandidate;
    private CheckpointGcCandidate activeCheckpointGcCandidate;
    private DestructiveRetireCandidate activeDestructiveRetireCandidate;
#if UNITY_EDITOR
    private bool failNextCheckpointGcAfterCustodyStrip;
    private bool failNextDeliveryOverlayAfterCustodySwap;
    private bool failNextDestructiveRetireAfterCustodyStrip;
#endif

    public string ParticipantId =>
        "998.world.facility-output-exact-route-outbox";

    public string CheckpointGcParticipantId => ParticipantId;
    public PreparedOutputCheckpointGcParticipantKind
        CheckpointGcParticipantKind =>
        PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority;
    public long LastConfirmedCheckpointSequence => checkpointSequence;
    public string LastConfirmedCheckpointDigest => checkpointDigest;
    public string LastConfirmedSerializedByteDigest => checkpointDigest;

#if UNITY_EDITOR
    public void FailNextCheckpointGcAfterCustodyStripForEditorTest() =>
        failNextCheckpointGcAfterCustodyStrip = true;

    public void FailNextDeliveryOverlayAfterCustodySwapForEditorTest() =>
        failNextDeliveryOverlayAfterCustodySwap = true;

    internal void FailNextDestructiveRetireAfterCustodyStripForEditorTest() =>
        failNextDestructiveRetireAfterCustodyStrip = true;
#endif

    public FacilityOutputExactRouteService(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        IItemMarkerPresenter markers)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.markers = markers
            ?? throw new ArgumentNullException(nameof(markers));
    }

    public bool TryRoute(
        FacilityOutputExactRouteRequest request,
        out FacilityOutputExactRouteReceipt receipt,
        out FacilityOutputExactRouteFailure failure)
    {
        receipt = null;
        failure = FacilityOutputExactRouteFailure.None;
        if (request == null)
            return Fail(
                FacilityOutputExactRouteFailureCode.InvalidRequest,
                "Exact route request is missing.",
                out failure);
        if (routes.TryGetValue(
                request.RouteOperationId,
                out FacilityOutputExactRoutePendingSnapshot replay))
        {
            if (!string.Equals(
                    replay.Receipt.RequestFingerprint,
                    request.RequestFingerprint,
                    StringComparison.Ordinal))
            {
                return Fail(
                    FacilityOutputExactRouteFailureCode.OperationConflict,
                    $"Route operation '{request.RouteOperationId}' was replayed with different input.",
                    out failure);
            }
            receipt = replay.Receipt;
            return true;
        }
        if (repository.TryGetActiveProductionCustodyDrainForDestination(
                request.SourceDestinationId,
                out ProductionPhysicalCustodyDrainSaveData draining))
        {
            return Fail(
                FacilityOutputExactRouteFailureCode.ProtectedRouteBypass,
                "Exact-route source is fenced by destructive drain '"
                + draining.stepOperationId + "'.",
                out failure);
        }

        IReadOnlyList<SourceSegment> sources;
        IReadOnlyList<SelectedRange> selected;
        IReadOnlyList<RecordPart> parts;
        try
        {
            if (!TryCaptureSourceSegments(
                    request,
                    out sources,
                    out failure)
                || !TrySelectRanges(
                    request,
                    sources,
                    out selected,
                    out failure)
                || !TryBuildParts(
                    sources,
                    selected,
                    out parts,
                    out failure))
            {
                return false;
            }
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            return Fail(
                FacilityOutputExactRouteFailureCode.PublicationAuthorityInvalid,
                "Exact-route source validation failed: " + exception.Message,
                out failure);
        }

        FacilityOutputExactRouteSliceReceipt[] sliceReceipts = parts
            .Where(value => value.Routed)
            .OrderBy(value => value.Start)
            .ThenBy(value => value.Source.Record.stackId, StringComparer.Ordinal)
            .Select(value => new FacilityOutputExactRouteSliceReceipt(
                value.Source.Record.stackId,
                value.StackId,
                value.Source.Metadata.OutputLineId,
                value.Request.LineCommitId,
                value.Source.Metadata.ItemId,
                value.Start,
                routedOffsetQuantity: 0,
                value.Quantity,
                value.MassGrams,
                value.Source.Metadata.ComponentFingerprint))
            .ToArray();
        string physicalFingerprint =
            FacilityOutputExactRouteFingerprint.CreatePhysicalReceipt(
                request,
                sliceReceipts);
        receipt = new FacilityOutputExactRouteReceipt(
            request.RouteOperationId,
            request.RequestFingerprint,
            physicalFingerprint,
            request.BatchCommitId,
            request.SourceDestinationId,
            request.TargetDestinationId,
            request.TargetPosition,
            request.TotalQuantity,
            request.TotalMassGrams,
            sliceReceipts);

        if (!TryCommit(
                request,
                sources,
                parts,
                receipt,
                out failure))
        {
            receipt = null;
            return false;
        }
        return true;
    }

    public bool TryAcknowledge(
        string routeOperationId,
        string physicalReceiptFingerprint,
        out FacilityOutputExactRouteReceipt receipt,
        out FacilityOutputExactRouteFailure failure)
    {
        receipt = null;
        failure = FacilityOutputExactRouteFailure.None;
        string operationId = routeOperationId ?? string.Empty;
        string fingerprint = physicalReceiptFingerprint ?? string.Empty;
        if (!IsCanonicalRequired(operationId)
            || !IsCanonicalRequired(fingerprint)
            || !routes.TryGetValue(
                operationId,
                out FacilityOutputExactRoutePendingSnapshot pending))
        {
            return Fail(
                FacilityOutputExactRouteFailureCode.PendingRouteMissing,
                $"Pending exact route '{operationId}' is unavailable.",
                out failure);
        }
        receipt = pending.Receipt;
        if (!string.Equals(
                pending.Receipt.PhysicalReceiptFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            receipt = null;
            return Fail(
                FacilityOutputExactRouteFailureCode.ReceiptMismatch,
                $"Exact route '{operationId}' acknowledgement fingerprint mismatched.",
                out failure);
        }
        if (pending.Phase == FacilityOutputExactRoutePhase.Routable)
            return true;
        if (pending.Phase != FacilityOutputExactRoutePhase.PhysicalPending)
        {
            receipt = null;
            return Fail(
                FacilityOutputExactRouteFailureCode.PhaseMismatch,
                $"Exact route '{operationId}' is not awaiting acknowledgement.",
                out failure);
        }

        Dictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>>
            replacements = new(StringComparer.Ordinal);
        foreach (FacilityOutputExactRouteSliceReceipt slice in
                 pending.Receipt.Slices)
        {
            if (!repository.RecordsById.TryGetValue(
                    slice.RoutedStackId,
                    out WorldItemStackRecord record)
                || record == null
                || record.state != WorldItemStackState.Loose
                || record.quantity != slice.RoutedQuantity
                || !FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                || record.position != metadata.OriginPosition
                || !DestinationIntentMatches(record, pending.Receipt)
                || metadata.Phase !=
                    FacilityOutputExactRouteCustodyPhase.PhysicalPending
                || !CustodyMatchesReceipt(metadata, pending.Receipt, slice)
                || !CustodyMatchesDeliveryRevision(
                    metadata,
                    pending.DeliveryRevision))
            {
                receipt = null;
                return Fail(
                    FacilityOutputExactRouteFailureCode.ReceiptMismatch,
                    $"Routed stack '{slice.RoutedStackId}' no longer matches exact-route custody.",
                    out failure);
            }
            FacilityOutputExactRouteCustodyMetadata routable = metadata.WithSlice(
                FacilityOutputExactRouteCustodyPhase.Routable,
                metadata.TargetDestinationId,
                metadata.CurrentSourceStackId,
                metadata.SourceOffsetQuantity,
                metadata.Quantity,
                metadata.MassGrams,
                metadata.RouteOperationId,
                metadata.RequestFingerprint,
                metadata.PhysicalReceiptFingerprint);
            replacements.Add(
                record.stackId,
                FacilityOutputExactRouteCustodyCodec.ReplaceAuthority(
                    record.components,
                    routable));
        }
        if (!repository.TryReplaceBatchComponentsAtomically(
                replacements,
                out string repositoryFailure))
        {
            receipt = null;
            return Fail(
                FacilityOutputExactRouteFailureCode.RepositoryTransactionFailed,
                repositoryFailure,
                out failure);
        }
        routes[operationId] = new FacilityOutputExactRoutePendingSnapshot(
            FacilityOutputExactRoutePhase.Routable,
            pending.Receipt,
            pending.DeliveryRevision);
        outboxRevision = checked(outboxRevision + 1L);
        foreach (FacilityOutputExactRouteSliceReceipt slice in pending.Receipt.Slices)
        {
            if (repository.RecordsById.TryGetValue(
                    slice.RoutedStackId,
                    out WorldItemStackRecord record))
                markers.RefreshAt(record.position);
        }
        return true;
    }

    public bool TryForgetRoutable(
        string routeOperationId,
        string physicalReceiptFingerprint,
        out FacilityOutputExactRouteFailure failure)
    {
        return Fail(
            FacilityOutputExactRouteFailureCode.ProtectedRouteBypass,
            "Routable exact-route authority can only be forgotten by durable checkpoint GC.",
            out failure);
    }

    FacilityOutputExactRouteDestructiveRetireResult
        IFacilityOutputExactRouteDestructiveRetirePort
            .PrepareDestructiveRetire(
        string sourceDestinationId,
        string batchCommitId,
        out IFacilityOutputExactRouteDestructiveRetireCandidate candidate)
    {
        candidate = null;
        string source = sourceDestinationId ?? string.Empty;
        string batch = batchCommitId ?? string.Empty;
        if (!IsCanonicalRequired(source) || !IsCanonicalRequired(batch))
        {
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                source,
                string.Empty,
                0,
                0,
                "Exact-route destructive retire source destination is invalid.");
        }
        if (restoreActive
            || activeDeliveryOverlayCandidate != null
            || activeCheckpointGcCandidate != null
            || activeDestructiveRetireCandidate != null)
        {
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Deferred,
                source,
                string.Empty,
                0,
                0,
                "Items exact-route authority is serving another transaction.");
        }

        FacilityOutputExactRoutePendingSnapshot[] batchRoutes = routes.Values
            .Where(value => string.Equals(
                value.Receipt.BatchCommitId,
                batch,
                StringComparison.Ordinal))
            .OrderBy(value => value.Receipt.RouteOperationId,
                StringComparer.Ordinal)
            .ToArray();
        if (batchRoutes.Any(value => !string.Equals(
                value.Receipt.SourceDestinationId,
                source,
                StringComparison.Ordinal)))
        {
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                source,
                string.Empty,
                batchRoutes.Length,
                0,
                "Routing batch belongs to a different source destination.");
        }
        FacilityOutputExactRoutePendingSnapshot[] selectedRoutes = batchRoutes;
        bool hasOrphanRoutablePhysical = repository.Records.Any(record =>
            record != null
            && FacilityOutputExactRouteCustodyCodec.TryRead(
                record.components,
                out FacilityOutputExactRouteCustodyMetadata metadata)
            && metadata.Phase == FacilityOutputExactRouteCustodyPhase.Routable
            && string.Equals(metadata.BatchCommitId, batch,
                StringComparison.Ordinal)
            && string.Equals(metadata.OriginDestinationId, source,
                StringComparison.Ordinal));
        if (selectedRoutes.Length == 0)
        {
            if (hasOrphanRoutablePhysical)
            {
                return DestructiveRetireResult(
                    FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                    source,
                    string.Empty,
                    0,
                    0,
                    "Routable physical custody exists without batch outbox authority.");
            }
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Empty,
                source,
                CreateDestructiveRetireFingerprint(
                    source,
                    batch,
                    outboxRevision,
                    Array.Empty<FacilityOutputExactRoutePendingSnapshot>(),
                    Array.Empty<RestoredCustodyPart>()),
                0,
                0,
                "No exact-route authority belongs to the source destination.");
        }
        if (selectedRoutes.Any(value => value.Phase !=
                FacilityOutputExactRoutePhase.Routable))
        {
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Deferred,
                source,
                string.Empty,
                selectedRoutes.Length,
                0,
                "Source destination has an exact route awaiting acknowledgement.");
        }

        HashSet<string> selectedOperationIds = selectedRoutes
            .Select(value => value.Receipt.RouteOperationId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, List<RestoredCustodyPart>> descendantsByOperation =
            selectedOperationIds.ToDictionary(
                value => value,
                _ => new List<RestoredCustodyPart>(),
                StringComparer.Ordinal);
        Dictionary<string, WorldItemStackRecord> recordsById =
            new(StringComparer.Ordinal);
        foreach (WorldItemStackRecord record in repository.Records
                     .Where(value => value != null)
                     .OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            if (!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    record.components))
            {
                continue;
            }
            if (!FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata))
            {
                return DestructiveRetireResult(
                    FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                    source,
                    string.Empty,
                    selectedRoutes.Length,
                    recordsById.Count,
                    "Malformed exact-route custody blocks destructive retire: "
                    + record.stackId);
            }
            if (!string.Equals(
                    metadata.BatchCommitId,
                    batch,
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (!string.Equals(
                    metadata.OriginDestinationId,
                    source,
                    StringComparison.Ordinal))
            {
                return DestructiveRetireResult(
                    FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                    source,
                    string.Empty,
                    selectedRoutes.Length,
                    recordsById.Count,
                    "Physical routing batch belongs to a different source destination: "
                    + record.stackId);
            }
            if (metadata.Phase ==
                FacilityOutputExactRouteCustodyPhase.OriginBuffered)
            {
                // The downstream physical-custody participant owns the origin
                // buffer. Capacity-routing retires only this batch's routed
                // descendants and must not deadlock on an unrouted remainder.
                continue;
            }
            if (metadata.Phase != FacilityOutputExactRouteCustodyPhase.Routable
                || !selectedOperationIds.Contains(metadata.RouteOperationId))
            {
                return DestructiveRetireResult(
                    FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                    source,
                    string.Empty,
                    selectedRoutes.Length,
                    recordsById.Count,
                    "Physical exact-route custody is not covered by the selected outbox: "
                    + record.stackId);
            }
            bool hasCommittedHaulIntent = repository.HaulDeliveryIntents
                .CaptureCommitted()
                .Where(value => value?.commitments != null)
                .SelectMany(value => value.commitments)
                .Any(value => value != null
                    && string.Equals(
                        value.carriedStackId,
                        record.stackId,
                        StringComparison.Ordinal));
            if (record.state != WorldItemStackState.Loose
                || record.dropDisposition != WorldItemDropDisposition.None
                || !string.IsNullOrEmpty(record.reservedByPersistentId)
                || record.reservedQuantity != 0
                || repository.PrioritizedHaulStackIds.Contains(record.stackId)
                || hasCommittedHaulIntent)
            {
                return DestructiveRetireResult(
                    FacilityOutputExactRouteDestructiveRetireStatus.Deferred,
                    source,
                    string.Empty,
                    selectedRoutes.Length,
                    recordsById.Count,
                    "Exact-route cargo must be unreserved and quiesced as an ordinary Loose stack: "
                    + record.stackId);
            }
            descendantsByOperation[metadata.RouteOperationId].Add(
                new RestoredCustodyPart
                {
                    Stack = CaptureSaveData(record),
                    Metadata = metadata
                });
            recordsById.Add(record.stackId, record);
        }

        HashSet<string> ownedDescendants = new(StringComparer.Ordinal);
        try
        {
            foreach (FacilityOutputExactRoutePendingSnapshot route in
                     selectedRoutes)
            {
                List<RestoredCustodyPart> descendants =
                    descendantsByOperation[route.Receipt.RouteOperationId];
                if (descendants.Count == 0)
                {
                    throw new InvalidOperationException(
                        "Routable outbox has no physical custody descendants: "
                        + route.Receipt.RouteOperationId);
                }
                ValidateRestoredRoutablePartition(
                    route.Receipt,
                    route.DeliveryRevision,
                    descendants,
                    ownedDescendants);
            }
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or OverflowException)
        {
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                source,
                string.Empty,
                selectedRoutes.Length,
                recordsById.Count,
                "Exact-route destructive retire coverage is invalid: "
                + exception.Message);
        }

        Dictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>>
            originals = new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>>
            stripped = new(StringComparer.Ordinal);
        Dictionary<string, GcPhysicalInvariant> invariants =
            new(StringComparer.Ordinal);
        foreach (WorldItemStackRecord record in recordsById.Values
                     .OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            ItemInstanceComponentSaveData[] original = record.components
                .Select(value => value.Clone())
                .ToArray();
            ItemInstanceComponentSaveData[] withoutCustody = record.components
                .Where(value => !FacilityOutputExactRouteCustodyCodec
                    .IsCustody(value))
                .Select(value => value.Clone())
                .ToArray();
            if (original.Length != withoutCustody.Length + 1)
            {
                return DestructiveRetireResult(
                    FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                    source,
                    string.Empty,
                    selectedRoutes.Length,
                    recordsById.Count,
                    "Exact-route destructive retire found ambiguous custody: "
                    + record.stackId);
            }
            originals.Add(record.stackId, original);
            stripped.Add(record.stackId, withoutCustody);
            invariants.Add(record.stackId, new GcPhysicalInvariant(record));
        }

        Dictionary<string, FacilityOutputExactRoutePendingSnapshot> nextRoutes =
            routes.Values
                .Where(value => !selectedOperationIds.Contains(
                    value.Receipt.RouteOperationId))
                .OrderBy(value => value.Receipt.RouteOperationId,
                    StringComparer.Ordinal)
                .ToDictionary(
                    value => value.Receipt.RouteOperationId,
                    CloneSnapshot,
                    StringComparer.Ordinal);
        RestoredCustodyPart[] allDescendants = descendantsByOperation.Values
            .SelectMany(value => value)
            .OrderBy(value => value.Stack.stackId, StringComparer.Ordinal)
            .ToArray();
        string fingerprint = CreateDestructiveRetireFingerprint(
            source,
            batch,
            outboxRevision,
            selectedRoutes,
            allDescendants);
        DestructiveRetireCandidate prepared = new(
            source,
            batch,
            fingerprint,
            repository.ItemStackVersion,
            outboxRevision,
            routes,
            nextRoutes,
            originals,
            stripped,
            invariants,
            selectedOperationIds.OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            recordsById.Keys.OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        activeDestructiveRetireCandidate = prepared;
        candidate = prepared;
        return DestructiveRetireResult(
            FacilityOutputExactRouteDestructiveRetireStatus.Ready,
            source,
            fingerprint,
            selectedRoutes.Length,
            recordsById.Count,
            "Exact-route destructive retire candidate is detached and ready.");
    }

    FacilityOutputExactRouteDestructiveRetireResult
        IFacilityOutputExactRouteDestructiveRetirePort.PublishDestructiveRetire(
        IFacilityOutputExactRouteDestructiveRetireCandidate candidate)
    {
        DestructiveRetireCandidate exact = RequireDestructiveRetireCandidate(
            candidate);
        if (exact.Completed)
        {
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                exact.SourceDestinationId,
                exact.CandidateFingerprint,
                exact.RouteOperationIds.Count,
                exact.PhysicalStackIds.Count,
                "Exact-route destructive retire candidate is completed.");
        }
        if (exact.Published)
        {
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.AlreadyApplied,
                exact.SourceDestinationId,
                exact.CandidateFingerprint,
                exact.RouteOperationIds.Count,
                exact.PhysicalStackIds.Count,
                "Exact-route destructive retire candidate is already published.");
        }
        if (repository.ItemStackVersion != exact.SourceRepositoryVersion
            || outboxRevision != exact.SourceOutboxRevision
            || !ReferenceEquals(routes, exact.PreviousRoutes))
        {
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Deferred,
                exact.SourceDestinationId,
                exact.CandidateFingerprint,
                exact.RouteOperationIds.Count,
                exact.PhysicalStackIds.Count,
                "Exact-route physical or outbox authority changed after preparation.");
        }
        if (exact.Stripped.Count > 0
            && !repository.TryReplaceBatchComponentsAtomically(
                exact.Stripped,
                out string repositoryFailure))
        {
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Deferred,
                exact.SourceDestinationId,
                exact.CandidateFingerprint,
                exact.RouteOperationIds.Count,
                exact.PhysicalStackIds.Count,
                "Exact-route destructive custody strip failed: "
                + repositoryFailure);
        }
#if UNITY_EDITOR
        if (failNextDestructiveRetireAfterCustodyStrip)
        {
            failNextDestructiveRetireAfterCustodyStrip = false;
            if (exact.Originals.Count > 0
                && !repository.TryReplaceBatchComponentsAtomically(
                    exact.Originals,
                    out string injectedRollbackFailure))
            {
                throw new InvalidOperationException(
                    "Injected exact-route destructive retire rollback failed: "
                    + injectedRollbackFailure);
            }
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Deferred,
                exact.SourceDestinationId,
                exact.CandidateFingerprint,
                exact.RouteOperationIds.Count,
                exact.PhysicalStackIds.Count,
                "Injected destructive retire fault rolled back custody strip.");
        }
#endif
        if (exact.Invariants.Any(pair =>
                !repository.RecordsById.TryGetValue(
                    pair.Key,
                    out WorldItemStackRecord record)
                || !pair.Value.Matches(record)
                || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    record.components)
                || !ComponentsEqual(record.components, exact.Stripped[pair.Key])))
        {
            if (exact.Originals.Count > 0
                && !repository.TryReplaceBatchComponentsAtomically(
                    exact.Originals,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Exact-route destructive retire invariant rollback failed: "
                    + rollbackFailure);
            }
            return DestructiveRetireResult(
                FacilityOutputExactRouteDestructiveRetireStatus.Conflict,
                exact.SourceDestinationId,
                exact.CandidateFingerprint,
                exact.RouteOperationIds.Count,
                exact.PhysicalStackIds.Count,
                "Exact-route destructive retire changed business payload or physical identity.");
        }

        routes = exact.NextRoutes;
        outboxRevision = checked(exact.SourceOutboxRevision + 1L);
        exact.PublishedRepositoryVersion = repository.ItemStackVersion;
        exact.Published = true;
        foreach (string stackId in exact.PhysicalStackIds)
        {
            if (repository.RecordsById.TryGetValue(
                    stackId,
                    out WorldItemStackRecord record))
            {
                markers.RefreshAt(record.position);
            }
        }
        return DestructiveRetireResult(
            FacilityOutputExactRouteDestructiveRetireStatus.Applied,
            exact.SourceDestinationId,
            exact.CandidateFingerprint,
            exact.RouteOperationIds.Count,
            exact.PhysicalStackIds.Count,
            "Exact-route custody and outbox were retired atomically.");
    }

    void IFacilityOutputExactRouteDestructiveRetirePort
        .RollbackDestructiveRetire(
        IFacilityOutputExactRouteDestructiveRetireCandidate candidate)
    {
        DestructiveRetireCandidate exact = RequireDestructiveRetireCandidate(
            candidate);
        if (exact.Completed)
            return;
        if (exact.Published)
        {
            if (!ReferenceEquals(routes, exact.NextRoutes)
                || outboxRevision != checked(exact.SourceOutboxRevision + 1L)
                || repository.ItemStackVersion != exact.PublishedRepositoryVersion)
            {
                throw new InvalidOperationException(
                    "Exact-route destructive retire rollback authority changed.");
            }
            if (exact.Originals.Count > 0
                && !repository.TryReplaceBatchComponentsAtomically(
                    exact.Originals,
                    out string repositoryFailure))
            {
                throw new InvalidOperationException(
                    "Exact-route destructive retire rollback failed: "
                    + repositoryFailure);
            }
            routes = exact.PreviousRoutes;
            outboxRevision = exact.SourceOutboxRevision;
        }
        exact.Completed = true;
        activeDestructiveRetireCandidate = null;
    }

    void IFacilityOutputExactRouteDestructiveRetirePort
        .CompleteDestructiveRetire(
        IFacilityOutputExactRouteDestructiveRetireCandidate candidate)
    {
        DestructiveRetireCandidate exact = RequireDestructiveRetireCandidate(
            candidate);
        if (exact.Completed)
            return;
        if (!exact.Published)
            throw new InvalidOperationException(
                "Unpublished exact-route destructive retire cannot complete.");
        exact.Completed = true;
        activeDestructiveRetireCandidate = null;
    }

    FacilityOutputExactRouteDeliveryRevisionSnapshot
        IFacilityOutputExactRouteDeliveryOverlayParticipant
            .CaptureCurrentDelivery(string routeOperationId)
    {
        string operationId = routeOperationId ?? string.Empty;
        if (!routes.TryGetValue(operationId, out var route))
            throw new InvalidOperationException(
                $"Items exact route '{operationId}' is unavailable.");
        return route.DeliveryRevision;
    }

    IFacilityOutputExactRouteDeliveryOverlayCandidate
        IFacilityOutputExactRouteDeliveryOverlayParticipant
            .PrepareDeliveryOverlay(
        string routeOperationId,
        long expectedCurrentRevision,
        string expectedCurrentRevisionFingerprint,
        string originalPhysicalReceiptFingerprint,
        long nextRevision,
        string nextRevisionFingerprint,
        string rerouteOperationId,
        string targetDestinationId,
        int targetPositionX,
        int targetPositionY,
        string targetAuthorityFingerprint)
    {
        string operationId = routeOperationId ?? string.Empty;
        FacilityOutputExactRouteDeliveryRevisionSnapshot next = new(
            operationId,
            originalPhysicalReceiptFingerprint,
            nextRevision,
            nextRevisionFingerprint,
            rerouteOperationId,
            targetDestinationId,
            targetPositionX,
            targetPositionY,
            targetAuthorityFingerprint);
        if (restoreActive
            || activeDeliveryOverlayCandidate != null
            || activeCheckpointGcCandidate != null)
        {
            return new DeliveryOverlayCandidate(
                FacilityOutputExactRouteDeliveryOverlayStatus.Deferred,
                FacilityOutputExactRouteDeliveryOverlayReason.AuthorityBusy,
                "Items exact-route authority is serving another transaction.",
                operationId,
                expectedCurrentRevision,
                expectedCurrentRevisionFingerprint,
                next);
        }
        if (!routes.TryGetValue(operationId, out var currentRoute)
            || currentRoute.Phase != FacilityOutputExactRoutePhase.Routable)
        {
            throw new InvalidOperationException(
                $"Items exact route '{operationId}' is not Routable.");
        }
        if (!string.Equals(
                currentRoute.Receipt.PhysicalReceiptFingerprint,
                originalPhysicalReceiptFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Items delivery reroute changed the original physical receipt.");
        }
        bool isReplay = SameDeliveryRevision(
            currentRoute.DeliveryRevision,
            next);
        if (!isReplay
            && (currentRoute.DeliveryRevision.Revision != expectedCurrentRevision
            || !string.Equals(
                currentRoute.DeliveryRevision.RevisionFingerprint,
                expectedCurrentRevisionFingerprint,
                StringComparison.Ordinal)
            || nextRevision != checked(expectedCurrentRevision + 1L)))
        {
            throw new InvalidOperationException(
                "Items delivery reroute expected revision conflicts with live authority.");
        }

        List<RestoredCustodyPart> descendants = repository.Records
            .Where(record => record != null
                && FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                && metadata.Phase == FacilityOutputExactRouteCustodyPhase.Routable
                && string.Equals(metadata.RouteOperationId,
                    operationId, StringComparison.Ordinal))
            .OrderBy(record => record.stackId, StringComparer.Ordinal)
            .Select(record =>
            {
                FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata);
                return new RestoredCustodyPart
                {
                    Stack = CaptureSaveData(record),
                    Metadata = metadata
                };
            })
            .ToList();
        ValidateRestoredRoutablePartition(
            currentRoute.Receipt,
            currentRoute.DeliveryRevision,
            descendants,
            new HashSet<string>(StringComparer.Ordinal));

        HashSet<string> activeHaulStackIds = repository.HaulDeliveryIntents
            .CaptureCommitted()
            .Where(value => value?.commitments != null)
            .SelectMany(value => value.commitments)
            .Where(value => value != null)
            .Select(value => value.carriedStackId ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        activeHaulStackIds.UnionWith(repository.PrioritizedHaulStackIds);
        WorldItemStackRecord[] records = descendants
            .Select(value => repository.RecordsById[value.Stack.stackId])
            .ToArray();
        if (records.Any(record => record.state != WorldItemStackState.Loose
                || record.dropDisposition != WorldItemDropDisposition.None
                || record.reservedQuantity != 0
                || !string.IsNullOrEmpty(record.reservedByPersistentId)
                || !string.IsNullOrEmpty(record.recoveryOwnerOperationId)
                || activeHaulStackIds.Contains(record.stackId)))
        {
            return new DeliveryOverlayCandidate(
                FacilityOutputExactRouteDeliveryOverlayStatus.Deferred,
                FacilityOutputExactRouteDeliveryOverlayReason
                    .PhysicalStateNotStable,
                "Items delivery overlay supports only unreserved stable Loose custody.",
                operationId,
                expectedCurrentRevision,
                expectedCurrentRevisionFingerprint,
                next);
        }

        FacilityOutputExactRouteDeliverySubjectSnapshot[] deliverySubjects =
            records.OrderBy(record => record.stackId, StringComparer.Ordinal)
                .Select(record =>
                {
                    FacilityOutputExactRouteCustodyCodec.TryRead(
                        record.components,
                        out FacilityOutputExactRouteCustodyMetadata metadata);
                    return new FacilityOutputExactRouteDeliverySubjectSnapshot(
                        record.stackId,
                        record.quantity,
                        record.reservationRevision,
                        metadata.ComponentFingerprint,
                        metadata.MassGrams,
                        metadata.RouteOperationId,
                        metadata.PhysicalReceiptFingerprint);
                })
                .ToArray();
        if (isReplay)
        {
            DeliveryOverlayCandidate replay = new(
                FacilityOutputExactRouteDeliveryOverlayStatus.Replay,
                FacilityOutputExactRouteDeliveryOverlayReason.None,
                "Items delivery overlay was already published.",
                operationId,
                expectedCurrentRevision,
                expectedCurrentRevisionFingerprint,
                next,
                deliverySubjects);
            activeDeliveryOverlayCandidate = replay;
            return replay;
        }

        Dictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>> originals =
            new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>> replacements =
            new(StringComparer.Ordinal);
        Dictionary<string, DeliveryPhysicalOriginal> physicalOriginals =
            new(StringComparer.Ordinal);
        foreach (WorldItemStackRecord record in records)
        {
            FacilityOutputExactRouteCustodyCodec.TryRead(
                record.components,
                out FacilityOutputExactRouteCustodyMetadata metadata);
            originals.Add(record.stackId,
                record.components.Select(value => value.Clone()).ToArray());
            replacements.Add(record.stackId,
                FacilityOutputExactRouteCustodyCodec.ReplaceAuthority(
                    record.components,
                    metadata.WithDeliveryRevision(next)));
            physicalOriginals.Add(
                record.stackId,
                new DeliveryPhysicalOriginal(record));
        }
        Dictionary<string, FacilityOutputExactRoutePendingSnapshot> nextRoutes =
            routes.ToDictionary(
                pair => pair.Key,
                pair => CloneSnapshot(pair.Value),
                StringComparer.Ordinal);
        nextRoutes[operationId] = new FacilityOutputExactRoutePendingSnapshot(
            currentRoute.Phase,
            currentRoute.Receipt,
            next);
        DeliveryOverlayCandidate prepared = new(
            FacilityOutputExactRouteDeliveryOverlayStatus.Prepared,
            FacilityOutputExactRouteDeliveryOverlayReason.None,
            "Items delivery overlay candidate is detached.",
            operationId,
            expectedCurrentRevision,
            expectedCurrentRevisionFingerprint,
            next,
            deliverySubjects,
            repository.ItemStackVersion,
            outboxRevision,
            routes,
            nextRoutes,
            originals,
            replacements,
            physicalOriginals);
        activeDeliveryOverlayCandidate = prepared;
        return prepared;
    }

    void IFacilityOutputExactRouteDeliveryOverlayParticipant
        .PublishDeliveryOverlay(
            IFacilityOutputExactRouteDeliveryOverlayCandidate candidate)
    {
        DeliveryOverlayCandidate exact = RequireDeliveryOverlayCandidate(candidate);
        if (exact.Completed)
            throw new InvalidOperationException("Items delivery overlay is completed.");
        if (exact.Published)
            return;
        if (exact.Status == FacilityOutputExactRouteDeliveryOverlayStatus.Deferred)
            throw new InvalidOperationException(
                "A deferred Items delivery overlay cannot publish.");
        if (exact.Status == FacilityOutputExactRouteDeliveryOverlayStatus.Replay)
        {
            exact.Published = true;
            return;
        }
        if (repository.ItemStackVersion != exact.SourceRepositoryVersion
            || outboxRevision != exact.SourceOutboxRevision
            || !ReferenceEquals(routes, exact.PreviousRoutes))
        {
            throw new InvalidOperationException(
                "Items delivery overlay authority changed after preparation.");
        }
        if (!repository.TryReplaceBatchComponentsAtomically(
                exact.Replacements,
                out string componentFailure))
        {
            throw new InvalidOperationException(
                "Items delivery custody swap failed: " + componentFailure);
        }
        try
        {
            foreach (string stackId in exact.PhysicalOriginals.Keys)
            {
                WorldItemStackRecord record = repository.RecordsById[stackId];
                record.destinationId = exact.Next.TargetDestinationId;
                record.hasDestinationPosition = true;
                record.destinationPosition = new Vector2Int(
                    exact.Next.TargetPositionX,
                    exact.Next.TargetPositionY);
            }
#if UNITY_EDITOR
            if (failNextDeliveryOverlayAfterCustodySwap)
            {
                failNextDeliveryOverlayAfterCustodySwap = false;
                throw new InvalidOperationException(
                    "Injected Items delivery overlay publish fault.");
            }
#endif
            routes = exact.NextRoutes;
            outboxRevision = checked(exact.SourceOutboxRevision + 1L);
            repository.MarkChanged();
            exact.PublishedRepositoryVersion = repository.ItemStackVersion;
            exact.Published = true;
        }
        catch
        {
            RestoreDeliveryOverlayPhysical(exact);
            throw;
        }
    }

    void IFacilityOutputExactRouteDeliveryOverlayParticipant
        .RollbackDeliveryOverlay(
            IFacilityOutputExactRouteDeliveryOverlayCandidate candidate)
    {
        DeliveryOverlayCandidate exact = RequireDeliveryOverlayCandidate(candidate);
        if (exact.Completed)
            return;
        if (exact.Published
            && exact.Status == FacilityOutputExactRouteDeliveryOverlayStatus.Prepared)
        {
            if (!ReferenceEquals(routes, exact.NextRoutes)
                || repository.ItemStackVersion != exact.PublishedRepositoryVersion)
            {
                throw new InvalidOperationException(
                    "Items delivery overlay rollback authority changed.");
            }
            RestoreDeliveryOverlayPhysical(exact);
        }
        exact.Published = false;
        activeDeliveryOverlayCandidate = null;
    }

    void IFacilityOutputExactRouteDeliveryOverlayParticipant
        .CompleteDeliveryOverlay(
            IFacilityOutputExactRouteDeliveryOverlayCandidate candidate)
    {
        DeliveryOverlayCandidate exact = RequireDeliveryOverlayCandidate(candidate);
        if (!exact.Published)
            throw new InvalidOperationException(
                "Items delivery overlay must publish before completion.");
        exact.Completed = true;
        activeDeliveryOverlayCandidate = null;
    }

    PreparedOutputCheckpointGcResult IPreparedOutputCheckpointGcParticipant
        .PrepareCheckpointGarbageCollection(
        PreparedOutputCheckpointGcContext context,
        out IPreparedOutputCheckpointGcCandidate candidate)
    {
        candidate = null;
        if (context.CheckpointSequence < checkpointSequence)
        {
            return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.StaleCheckpoint,
                context,
                "Items exact-route checkpoint sequence moved backwards.");
        }
        if (context.CheckpointSequence == checkpointSequence)
        {
            if (!string.Equals(checkpointDigest,
                    context.SerializedByteDigest, StringComparison.Ordinal))
            {
                return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                    PreparedOutputCheckpointGcReason.ReplayDigestMismatch,
                    context,
                    "Items exact-route checkpoint replay changed serialized bytes.");
            }
            return GcResult(PreparedOutputCheckpointGcStatus.AlreadyApplied,
                PreparedOutputCheckpointGcReason.None,
                context,
                "Items exact-route checkpoint was already applied.");
        }
        if (context.CheckpointSequence != checked(checkpointSequence + 1L))
        {
            return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.StaleCheckpoint,
                context,
                "Items exact-route checkpoint sequence is not contiguous.");
        }
        if (activeDeliveryOverlayCandidate != null
            || activeCheckpointGcCandidate != null)
        {
            return GcResult(PreparedOutputCheckpointGcStatus.Deferred,
                PreparedOutputCheckpointGcReason.LiveAuthorityChanged,
                context,
                "Items exact-route authority is serving another transaction.");
        }

        Dictionary<string, List<RestoredCustodyPart>> routableByOperation =
            new(StringComparer.Ordinal);
        HashSet<string> batchesWithOriginCustody = new(StringComparer.Ordinal);
        Dictionary<string, WorldItemStackRecord> routableRecords =
            new(StringComparer.Ordinal);
        foreach (WorldItemStackRecord record in repository.Records
                     .Where(value => value != null)
                     .OrderBy(value => value.stackId, StringComparer.Ordinal))
        {
            if (!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    record.components))
            {
                continue;
            }
            if (!FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata))
            {
                return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                    PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                    context,
                    $"Exact-route custody '{record.stackId}' is malformed.");
            }
            if (metadata.Phase ==
                FacilityOutputExactRouteCustodyPhase.OriginBuffered)
            {
                batchesWithOriginCustody.Add(metadata.BatchCommitId);
                continue;
            }
            if (metadata.Phase !=
                FacilityOutputExactRouteCustodyPhase.Routable)
            {
                continue;
            }
            if (!routableByOperation.TryGetValue(
                    metadata.RouteOperationId,
                    out List<RestoredCustodyPart> descendants))
            {
                descendants = new List<RestoredCustodyPart>();
                routableByOperation.Add(metadata.RouteOperationId, descendants);
            }
            descendants.Add(new RestoredCustodyPart
            {
                Stack = CaptureSaveData(record),
                Metadata = metadata
            });
            routableRecords.Add(record.stackId, record);
        }

        foreach (string operationId in routableByOperation.Keys)
        {
            if (!routes.TryGetValue(operationId, out var route)
                || route.Phase != FacilityOutputExactRoutePhase.Routable)
            {
                return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                    PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                    context,
                    $"Routable physical custody '{operationId}' has no Routable outbox authority.");
            }
        }

        HashSet<string> activeHaulStackIds = repository.HaulDeliveryIntents
            .CaptureCommitted()
            .Where(value => value?.commitments != null)
            .SelectMany(value => value.commitments)
            .Where(value => value != null)
            .Select(value => value.carriedStackId ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        activeHaulStackIds.UnionWith(repository.PrioritizedHaulStackIds);
        List<string> collectedBatches = new();
        List<string> collectedOperations = new();
        Dictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>> originals =
            new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>> stripped =
            new(StringComparer.Ordinal);
        Dictionary<string, GcPhysicalInvariant> invariants =
            new(StringComparer.Ordinal);
        HashSet<string> ownedDescendants = new(StringComparer.Ordinal);

        foreach (IGrouping<string, FacilityOutputExactRoutePendingSnapshot> batch
                 in routes.Values
                     .GroupBy(value => value.Receipt.BatchCommitId,
                         StringComparer.Ordinal)
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            FacilityOutputExactRoutePendingSnapshot[] batchRoutes = batch
                .OrderBy(value => value.Receipt.RouteOperationId,
                    StringComparer.Ordinal)
                .ToArray();
            if (batchesWithOriginCustody.Contains(batch.Key)
                || batchRoutes.Any(value => value.Phase !=
                    FacilityOutputExactRoutePhase.Routable))
            {
                continue;
            }

            List<WorldItemStackRecord> batchRecords = new();
            foreach (FacilityOutputExactRoutePendingSnapshot pending in batchRoutes)
            {
                string operationId = pending.Receipt.RouteOperationId;
                if (!routableByOperation.TryGetValue(
                        operationId,
                        out List<RestoredCustodyPart> descendants)
                    || descendants.Count == 0)
                {
                    return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                        PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                        context,
                        $"Routable outbox '{operationId}' has no physical custody descendants.");
                }
                foreach (RestoredCustodyPart descendant in descendants)
                    batchRecords.Add(routableRecords[descendant.Stack.stackId]);
            }

            if (batchRecords.Any(record =>
                    !IsCheckpointGcStable(record, activeHaulStackIds)))
            {
                return GcResult(PreparedOutputCheckpointGcStatus.Deferred,
                    PreparedOutputCheckpointGcReason.PhysicalStateNotStable,
                    context,
                    $"Prepared-output batch '{batch.Key}' has moving, reserved, or recovery custody.");
            }

            foreach (FacilityOutputExactRoutePendingSnapshot pending in batchRoutes)
            {
                string operationId = pending.Receipt.RouteOperationId;
                List<RestoredCustodyPart> descendants =
                    routableByOperation[operationId];
                try
                {
                    ValidateRestoredRoutablePartition(
                        pending.Receipt,
                        pending.DeliveryRevision,
                        descendants,
                        ownedDescendants);
                }
                catch (Exception exception) when (exception is
                    InvalidOperationException or OverflowException)
                {
                    return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                        PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                        context,
                        $"Routable outbox '{operationId}' coverage is invalid: {exception.Message}");
                }
            }

            collectedBatches.Add(batch.Key);
            foreach (FacilityOutputExactRoutePendingSnapshot pending in batchRoutes)
                collectedOperations.Add(pending.Receipt.RouteOperationId);
            foreach (WorldItemStackRecord record in batchRecords
                         .OrderBy(value => value.stackId, StringComparer.Ordinal))
            {
                ItemInstanceComponentSaveData[] original = record.components
                    .Select(value => value.Clone())
                    .ToArray();
                ItemInstanceComponentSaveData[] withoutCustody = record.components
                    .Where(value => !FacilityOutputExactRouteCustodyCodec
                        .IsCustody(value))
                    .Select(value => value.Clone())
                    .ToArray();
                if (original.Length != withoutCustody.Length + 1)
                {
                    return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                        PreparedOutputCheckpointGcReason.PartialAuthorityCoverage,
                        context,
                        $"Prepared-output stack '{record.stackId}' has ambiguous custody components.");
                }
                originals.Add(record.stackId, original);
                stripped.Add(record.stackId, withoutCustody);
                invariants.Add(record.stackId, new GcPhysicalInvariant(record));
            }
        }

        Dictionary<string, FacilityOutputExactRoutePendingSnapshot> nextRoutes =
            routes.Values
                .Where(value => !collectedOperations.Contains(
                    value.Receipt.RouteOperationId, StringComparer.Ordinal))
                .OrderBy(value => value.Receipt.RouteOperationId,
                    StringComparer.Ordinal)
                .ToDictionary(
                    value => value.Receipt.RouteOperationId,
                    CloneSnapshot,
                    StringComparer.Ordinal);
        candidate = new CheckpointGcCandidate(
            context,
            repository.ItemStackVersion,
            outboxRevision,
            routes,
            checkpointSequence,
            checkpointDigest,
            nextRoutes,
            originals,
            stripped,
            invariants,
            collectedBatches.ToArray(),
            collectedOperations.OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
        activeCheckpointGcCandidate = (CheckpointGcCandidate)candidate;
        return GcResult(PreparedOutputCheckpointGcStatus.Applied,
            collectedBatches.Count == 0
                ? PreparedOutputCheckpointGcReason.NoEligibleWholeBatch
                : PreparedOutputCheckpointGcReason.None,
            context,
            collectedBatches.Count == 0
                ? "Checkpoint advances without an eligible whole Items batch."
                : "Items whole-batch GC candidate is detached.",
            collectedBatches.Count);
    }

    PreparedOutputCheckpointGcResult IPreparedOutputCheckpointGcParticipant
        .PublishCheckpointGarbageCollection(
        IPreparedOutputCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireGcCandidate(candidate);
        if (!ReferenceEquals(activeCheckpointGcCandidate, exact))
            throw new InvalidOperationException(
                "Items checkpoint GC candidate is not active.");
        if (exact.Completed)
            throw new InvalidOperationException("Checkpoint GC candidate is completed.");
        if (exact.Published)
        {
            return GcResult(PreparedOutputCheckpointGcStatus.AlreadyApplied,
                PreparedOutputCheckpointGcReason.None,
                exact.Context,
                "Items checkpoint candidate is already published.",
                exact.BatchCommitIds.Count);
        }
        if (repository.ItemStackVersion != exact.SourceRepositoryVersion
            || outboxRevision != exact.SourceOutboxRevision
            || !ReferenceEquals(routes, exact.PreviousRoutes)
            || checkpointSequence != exact.PreviousSequence
            || !string.Equals(checkpointDigest, exact.PreviousDigest,
                StringComparison.Ordinal))
        {
            return GcResult(PreparedOutputCheckpointGcStatus.Deferred,
                PreparedOutputCheckpointGcReason.LiveAuthorityChanged,
                exact.Context,
                "Items physical or outbox authority changed after GC preparation.");
        }
        long nextOutboxRevision = checked(exact.SourceOutboxRevision + 1L);

        if (exact.Stripped.Count > 0
            && !repository.TryReplaceBatchComponentsAtomically(
                exact.Stripped,
                out string repositoryFailure))
        {
            return GcResult(PreparedOutputCheckpointGcStatus.Deferred,
                PreparedOutputCheckpointGcReason.ParticipantPublishFailed,
                exact.Context,
                "Items custody strip failed: " + repositoryFailure);
        }
#if UNITY_EDITOR
        if (failNextCheckpointGcAfterCustodyStrip)
        {
            failNextCheckpointGcAfterCustodyStrip = false;
            if (exact.Originals.Count > 0
                && !repository.TryReplaceBatchComponentsAtomically(
                    exact.Originals,
                    out string injectedRollbackFailure))
            {
                throw new InvalidOperationException(
                    "Injected Items custody-strip rollback failed: "
                    + injectedRollbackFailure);
            }
            return GcResult(PreparedOutputCheckpointGcStatus.Deferred,
                PreparedOutputCheckpointGcReason.ParticipantPublishFailed,
                exact.Context,
                "Injected Items custody-strip publish fault rolled back.");
        }
#endif
        if (exact.Invariants.Any(pair =>
                !repository.RecordsById.TryGetValue(
                    pair.Key, out WorldItemStackRecord record)
                || !pair.Value.Matches(record)
                || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    record.components)
                || !ComponentsEqual(record.components, exact.Stripped[pair.Key])))
        {
            if (exact.Originals.Count > 0
                && !repository.TryReplaceBatchComponentsAtomically(
                    exact.Originals,
                    out string rollbackFailure))
            {
                throw new InvalidOperationException(
                    "Items custody-strip invariant rollback failed: "
                    + rollbackFailure);
            }
            return GcResult(PreparedOutputCheckpointGcStatus.Corruption,
                PreparedOutputCheckpointGcReason.ParticipantPublishFailed,
                exact.Context,
                "Items custody strip changed business payload or physical identity.");
        }

        routes = exact.NextRoutes;
        checkpointSequence = exact.Context.CheckpointSequence;
        checkpointDigest = exact.Context.SerializedByteDigest;
        outboxRevision = nextOutboxRevision;
        exact.PublishedRepositoryVersion = repository.ItemStackVersion;
        exact.Published = true;
        return GcResult(PreparedOutputCheckpointGcStatus.Applied,
            PreparedOutputCheckpointGcReason.None,
            exact.Context,
            "Items whole-batch GC candidate was published.",
            exact.BatchCommitIds.Count);
    }

    void IPreparedOutputCheckpointGcParticipant
        .RollbackCheckpointGarbageCollection(
        IPreparedOutputCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireGcCandidate(candidate);
        if (!ReferenceEquals(activeCheckpointGcCandidate, exact))
            throw new InvalidOperationException(
                "Items checkpoint GC candidate is not active.");
        if (exact.Completed || !exact.Published)
            return;
        if (!ReferenceEquals(routes, exact.NextRoutes)
            || checkpointSequence != exact.Context.CheckpointSequence
            || !string.Equals(checkpointDigest,
                exact.Context.SerializedByteDigest, StringComparison.Ordinal)
            || repository.ItemStackVersion != exact.PublishedRepositoryVersion)
        {
            throw new InvalidOperationException(
                "Items checkpoint rollback authority changed.");
        }
        if (exact.Originals.Count > 0
            && !repository.TryReplaceBatchComponentsAtomically(
                exact.Originals,
                out string repositoryFailure))
        {
            throw new InvalidOperationException(
                "Items checkpoint component rollback failed: "
                + repositoryFailure);
        }
        routes = exact.PreviousRoutes;
        checkpointSequence = exact.PreviousSequence;
        checkpointDigest = exact.PreviousDigest;
        outboxRevision = exact.SourceOutboxRevision;
        exact.Published = false;
    }

    void IPreparedOutputCheckpointGcParticipant
        .CompleteCheckpointGarbageCollection(
        IPreparedOutputCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireGcCandidate(candidate);
        if (!ReferenceEquals(activeCheckpointGcCandidate, exact))
            throw new InvalidOperationException(
                "Items checkpoint GC candidate is not active.");
        exact.Completed = true;
        activeCheckpointGcCandidate = null;
    }

    public IReadOnlyList<FacilityOutputExactRoutePendingSnapshot>
        CapturePendingRoutes() => routes.Values
        .OrderBy(value => value.Receipt.RouteOperationId, StringComparer.Ordinal)
        .Select(CloneSnapshot)
        .ToArray();

    public IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> CaptureOutbox() =>
        CapturePendingRoutes().Select(ToSaveData).ToArray();

    public FacilityOutputExactRouteRestoreCandidate BuildRestoreCandidate(
        IReadOnlyList<FacilityOutputExactRouteOutboxSaveData> restoredRoutes,
        IReadOnlyList<WorldItemStackSaveData> physicalStacks,
        long restoredCheckpointSequence = 0L,
        string restoredCheckpointDigest = "")
    {
        ValidateCheckpointState(
            restoredCheckpointSequence,
            restoredCheckpointDigest);
        FacilityOutputExactRoutePendingSnapshot[] snapshots = (restoredRoutes
                ?? Array.Empty<FacilityOutputExactRouteOutboxSaveData>())
            .Select(FromSaveData)
            .OrderBy(value => value.Receipt.RouteOperationId, StringComparer.Ordinal)
            .ToArray();
        if (snapshots.Select(value => value.Receipt.RouteOperationId)
                .Distinct(StringComparer.Ordinal).Count() != snapshots.Length)
            throw new InvalidOperationException(
                "Exact-route restore candidate contains duplicate operations.");
        Dictionary<string, FacilityOutputExactRoutePendingSnapshot> validated =
            ValidateRestorePhysicalJoin(snapshots, physicalStacks);
        return new FacilityOutputExactRouteRestoreCandidate(
            snapshots.Select(ToSaveData).ToArray(),
            new PreparedRestoreState(
                validated,
                restoredCheckpointSequence,
                restoredCheckpointDigest),
            restoredCheckpointSequence,
            restoredCheckpointDigest);
    }

    public void RestoreCandidate(
        FacilityOutputExactRouteRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        if (!restoreActive
            || restorePublished
            || stagedRoutes != null
            || candidate.PreparedState is not PreparedRestoreState prepared)
        {
            throw new InvalidOperationException(
                "Exact-route restore candidate was not prevalidated for this transaction.");
        }
        stagedRoutes = prepared.Routes;
        stagedCheckpointSequence = prepared.CheckpointSequence;
        stagedCheckpointDigest = prepared.CheckpointDigest;
    }

    public void AcknowledgeRestoredRoute(
        string routeOperationId,
        string physicalReceiptFingerprint)
    {
        if (!restoreActive || restorePublished || stagedRoutes == null)
            throw new InvalidOperationException(
                "Exact-route restore acknowledgement requires an active staged candidate.");
        string operationId = routeOperationId ?? string.Empty;
        string fingerprint = physicalReceiptFingerprint ?? string.Empty;
        if (!stagedRoutes.TryGetValue(operationId, out var pending)
            || !string.Equals(
                pending.Receipt.PhysicalReceiptFingerprint,
                fingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Exact-route restore acknowledgement '{operationId}' conflicts.");
        }
        if (pending.Phase == FacilityOutputExactRoutePhase.Routable)
            return;
        if (pending.Phase != FacilityOutputExactRoutePhase.PhysicalPending)
            throw new InvalidOperationException(
                $"Exact-route restore acknowledgement '{operationId}' has an invalid phase.");

        Dictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>>
            replacements = new(StringComparer.Ordinal);
        foreach (FacilityOutputExactRouteSliceReceipt slice in
                 pending.Receipt.Slices)
        {
            if (!repository.RecordsById.TryGetValue(
                    slice.RoutedStackId,
                    out WorldItemStackRecord record)
                || record == null
                || !FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata)
                || metadata.Phase !=
                    FacilityOutputExactRouteCustodyPhase.PhysicalPending
                || !CustodyMatchesReceipt(metadata, pending.Receipt, slice)
                || !CustodyMatchesDeliveryRevision(
                    metadata,
                    pending.DeliveryRevision)
                || record.position != metadata.OriginPosition
                || !DestinationIntentMatches(record, pending.Receipt))
            {
                throw new InvalidOperationException(
                    $"Exact-route restored stack '{slice.RoutedStackId}' conflicts before acknowledgement.");
            }
            if (!restoreComponentOriginals.ContainsKey(record.stackId))
            {
                restoreComponentOriginals.Add(
                    record.stackId,
                    record.components.Select(value => value.Clone()).ToArray());
            }
            replacements.Add(
                record.stackId,
                FacilityOutputExactRouteCustodyCodec.ReplaceAuthority(
                    record.components,
                    metadata.WithSlice(
                        FacilityOutputExactRouteCustodyPhase.Routable,
                        metadata.TargetDestinationId,
                        metadata.CurrentSourceStackId,
                        metadata.SourceOffsetQuantity,
                        metadata.Quantity,
                        metadata.MassGrams,
                        metadata.RouteOperationId,
                        metadata.RequestFingerprint,
                        metadata.PhysicalReceiptFingerprint)));
        }
        if (!repository.TryReplaceBatchComponentsAtomically(
                replacements,
                out string repositoryFailure))
        {
            throw new InvalidOperationException(
                "Exact-route restore acknowledgement failed: "
                + repositoryFailure);
        }
        stagedRoutes[operationId] = new FacilityOutputExactRoutePendingSnapshot(
            FacilityOutputExactRoutePhase.Routable,
            pending.Receipt,
            pending.DeliveryRevision);
    }

    public void BeginRestoreCandidate()
    {
        if (restoreActive)
            throw new InvalidOperationException(
                "Exact-route restore transaction is already active.");
        restoreActive = true;
        restorePublished = false;
        stagedRoutes = null;
        previousRoutes = null;
        stagedCheckpointSequence = 0L;
        stagedCheckpointDigest = string.Empty;
        previousRestoreCheckpointSequence = 0L;
        previousRestoreCheckpointDigest = string.Empty;
        restoreComponentOriginals.Clear();
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreActive || restorePublished || stagedRoutes == null)
            throw new InvalidOperationException(
                "Exact-route restore candidate is not ready to publish.");
        long nextOutboxRevision = checked(outboxRevision + 1L);
        previousRoutes = routes;
        previousRestoreOutboxRevision = outboxRevision;
        previousRestoreCheckpointSequence = checkpointSequence;
        previousRestoreCheckpointDigest = checkpointDigest;
        routes = stagedRoutes;
        checkpointSequence = stagedCheckpointSequence;
        checkpointDigest = stagedCheckpointDigest;
        outboxRevision = nextOutboxRevision;
        restorePublished = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        RestoreCandidateComponents();
        if (restorePublished && previousRoutes != null)
        {
            routes = previousRoutes;
            outboxRevision = previousRestoreOutboxRevision;
            checkpointSequence = previousRestoreCheckpointSequence;
            checkpointDigest = previousRestoreCheckpointDigest;
        }
        ResetRestoreTransaction();
    }

    public void CompleteRestoreCandidate() =>
        ResetRestoreTransaction();

    public void DiscardRestoreCandidate()
    {
        RestoreCandidateComponents();
        ResetRestoreTransaction();
    }

    private Dictionary<string, FacilityOutputExactRoutePendingSnapshot>
        ValidateRestorePhysicalJoin(
            IReadOnlyList<FacilityOutputExactRoutePendingSnapshot> snapshots,
            IReadOnlyList<WorldItemStackSaveData> physicalStacks)
    {
        WorldItemStackSaveData[] stacks = (physicalStacks
                ?? Array.Empty<WorldItemStackSaveData>())
            .Select(value => value ?? throw new InvalidOperationException(
                "Exact-route restore contains a null physical stack."))
            .ToArray();
        Dictionary<string, WorldItemStackSaveData> physicalById = new(
            StringComparer.Ordinal);
        foreach (WorldItemStackSaveData stack in stacks)
        {
            if (!physicalById.TryAdd(stack.stackId ?? string.Empty, stack))
                throw new InvalidOperationException(
                    $"Exact-route restore has duplicate physical stack '{stack.stackId}'.");
        }

        Dictionary<string, List<RestoredCustodyPart>> routedByOperation =
            new(StringComparer.Ordinal);
        foreach (WorldItemStackSaveData stack in stacks)
        {
            if (!FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    stack.components))
            {
                continue;
            }
            if (!FacilityOutputExactRouteCustodyCodec.TryRead(
                    stack.components,
                    out FacilityOutputExactRouteCustodyMetadata metadata))
            {
                throw new InvalidOperationException(
                    $"Exact-route restore stack '{stack.stackId}' has malformed custody.");
            }
            if (metadata.Phase ==
                FacilityOutputExactRouteCustodyPhase.OriginBuffered)
            {
                ValidateRestoredOriginStack(stack, metadata);
                continue;
            }
            if (!routedByOperation.TryGetValue(
                    metadata.RouteOperationId,
                    out List<RestoredCustodyPart> operationParts))
            {
                operationParts = new List<RestoredCustodyPart>();
                routedByOperation.Add(metadata.RouteOperationId, operationParts);
            }
            operationParts.Add(new RestoredCustodyPart
            {
                Stack = stack,
                Metadata = metadata
            });
        }

        Dictionary<string, FacilityOutputExactRoutePendingSnapshot> result =
            new(StringComparer.Ordinal);
        HashSet<string> ownedRoutedStacks = new(StringComparer.Ordinal);
        foreach (FacilityOutputExactRoutePendingSnapshot snapshot in snapshots)
        {
            FacilityOutputExactRouteReceipt receipt = snapshot.Receipt;
            FacilityOutputExactRouteRequest logicalRequest =
                RebuildLogicalRequest(receipt);
            if (!string.Equals(
                    logicalRequest.RequestFingerprint,
                    receipt.RequestFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    FacilityOutputExactRouteFingerprint.CreatePhysicalReceipt(
                        logicalRequest,
                        receipt.Slices),
                    receipt.PhysicalReceiptFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Exact-route restore fingerprints conflict for '{receipt.RouteOperationId}'.");
            }
            ValidateRestoredDeliveryRevision(
                receipt,
                snapshot.DeliveryRevision);

            if (snapshot.Phase == FacilityOutputExactRoutePhase.PhysicalPending)
            {
                foreach (IGrouping<string, FacilityOutputExactRouteSliceReceipt> group
                         in receipt.Slices.GroupBy(
                             value => value.RoutedStackId,
                             StringComparer.Ordinal))
                {
                    FacilityOutputExactRouteSliceReceipt[] slices = group.ToArray();
                    if (slices.Length != 1
                        || slices[0].RoutedOffsetQuantity != 0)
                    {
                        throw new InvalidOperationException(
                            $"Exact-route restore stack '{group.Key}' has unsupported pending custody ranges.");
                    }
                    FacilityOutputExactRouteSliceReceipt slice = slices[0];
                    if (!ownedRoutedStacks.Add(slice.RoutedStackId)
                        || !physicalById.TryGetValue(
                            slice.RoutedStackId,
                            out WorldItemStackSaveData stack))
                    {
                        throw new InvalidOperationException(
                            $"Exact-route restore stack '{slice.RoutedStackId}' is missing or multiply owned.");
                    }
                    ValidateRestoredRoutedStack(snapshot, receipt, slice, stack);
                }
            }
            else if (snapshot.Phase == FacilityOutputExactRoutePhase.Routable)
            {
                if (!routedByOperation.TryGetValue(
                        receipt.RouteOperationId,
                        out List<RestoredCustodyPart> descendants)
                    || descendants.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Exact-route restore operation '{receipt.RouteOperationId}' has no physical descendants.");
                }
                ValidateRestoredRoutablePartition(
                    receipt,
                    snapshot.DeliveryRevision,
                    descendants,
                    ownedRoutedStacks);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Exact-route restore operation '{receipt.RouteOperationId}' has an invalid phase.");
            }
            result.Add(receipt.RouteOperationId, CloneSnapshot(snapshot));
        }

        foreach (KeyValuePair<string, List<RestoredCustodyPart>> operation in
                 routedByOperation)
        {
            foreach (RestoredCustodyPart part in operation.Value)
            {
                if (!ownedRoutedStacks.Contains(part.Stack.stackId))
                {
                    throw new InvalidOperationException(
                        $"Exact-route restore stack '{part.Stack.stackId}' has orphan routed custody.");
                }
            }
        }
        return result;
    }

    private void ValidateRestoredOriginStack(
        WorldItemStackSaveData stack,
        FacilityOutputExactRouteCustodyMetadata metadata)
    {
        List<ItemInstanceComponentSaveData> business =
            CaptureBusinessComponents(stack.components);
        string signature = FacilityBufferPlannedOutputPublicationService
            .CreateRuntimeComponentSignature(business);
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)stack.itemId,
            stack.itemInstanceId,
            business);
        long mass = massQuery.GetQuantityMass(
            (ItemDefinitionId)stack.itemId,
            subject,
            stack.quantity).Value;
        if (stack.state != WorldItemStackState.FacilityOutputBuffer
            || stack.quantity != metadata.Quantity
            || !string.Equals(stack.itemId, metadata.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.stackId,
                metadata.CurrentSourceStackId,
                StringComparison.Ordinal)
            || new Vector2Int(stack.gridX, stack.gridY) !=
                metadata.OriginPosition
            || !string.Equals(stack.destinationId,
                metadata.OriginDestinationId, StringComparison.Ordinal)
            || stack.hasDestinationPosition
            || !string.Equals(signature, metadata.ComponentSignature,
                StringComparison.Ordinal)
            || mass != metadata.MassGrams)
        {
            throw new InvalidOperationException(
                $"Exact-route origin custody '{stack.stackId}' conflicts with its physical stack.");
        }
    }

    private FacilityOutputExactRouteRequest RebuildLogicalRequest(
        FacilityOutputExactRouteReceipt receipt)
    {
        FacilityOutputExactRouteSliceReceipt[] physical = receipt.Slices
            .OrderBy(value => value.SourceOffsetQuantity)
            .ThenBy(value => value.SourceStackId, StringComparer.Ordinal)
            .ToArray();
        FacilityOutputExactRouteSliceReceipt first = physical[0];
        int nextOffset = first.SourceOffsetQuantity;
        long mass = 0L;
        foreach (FacilityOutputExactRouteSliceReceipt slice in physical)
        {
            if (!string.Equals(slice.OutputLineId, first.OutputLineId,
                    StringComparison.Ordinal)
                || !string.Equals(slice.LineCommitId, first.LineCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(slice.ItemId, first.ItemId,
                    StringComparison.Ordinal)
                || !string.Equals(slice.ComponentFingerprint,
                    first.ComponentFingerprint, StringComparison.Ordinal)
                || slice.SourceOffsetQuantity != nextOffset)
            {
                throw new InvalidOperationException(
                    $"Exact-route restore operation '{receipt.RouteOperationId}' is not one canonical line range.");
            }
            nextOffset = checked(nextOffset + slice.RoutedQuantity);
            mass = checked(mass + slice.RoutedMassGrams);
        }
        FacilityOutputExactRouteSliceRequest logical = new(
            first.OutputLineId,
            first.LineCommitId,
            first.ItemId,
            first.SourceOffsetQuantity,
            receipt.TotalQuantity,
            mass,
            first.ComponentFingerprint);
        return new FacilityOutputExactRouteRequest(
            receipt.RouteOperationId,
            receipt.BatchCommitId,
            receipt.SourceDestinationId,
            receipt.TargetDestinationId,
            receipt.TargetPosition,
            new[] { logical });
    }

    private void ValidateRestoredRoutablePartition(
        FacilityOutputExactRouteReceipt receipt,
        FacilityOutputExactRouteDeliveryRevisionSnapshot deliveryRevision,
        IReadOnlyList<RestoredCustodyPart> descendants,
        ISet<string> ownedRoutedStacks)
    {
        FacilityOutputExactRouteSliceReceipt[] expectedSlices = receipt.Slices
            .OrderBy(value => value.SourceOffsetQuantity)
            .ThenBy(value => value.RoutedStackId, StringComparer.Ordinal)
            .ToArray();
        if (expectedSlices.Length == 0
            || expectedSlices.Any(value => value.RoutedOffsetQuantity != 0))
        {
            throw new InvalidOperationException(
                $"Exact-route restore operation '{receipt.RouteOperationId}' has unsupported routed offsets.");
        }
        Dictionary<FacilityOutputExactRouteSliceReceipt,
            List<RestoredCustodyPart>> assigned = new();
        foreach (FacilityOutputExactRouteSliceReceipt slice in expectedSlices)
            assigned.Add(slice, new List<RestoredCustodyPart>());

        foreach (RestoredCustodyPart descendant in descendants)
        {
            if (descendant?.Stack == null
                || descendant.Metadata.Phase !=
                    FacilityOutputExactRouteCustodyPhase.Routable
                || !ownedRoutedStacks.Add(descendant.Stack.stackId))
            {
                throw new InvalidOperationException(
                    $"Exact-route restore operation '{receipt.RouteOperationId}' has malformed or multiply owned descendants.");
            }

            FacilityOutputExactRouteSliceReceipt[] matches = expectedSlices
                .Where(slice => CustodyBelongsToReceiptSlice(
                    descendant.Metadata,
                    receipt,
                    slice))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Exact-route restore descendant '{descendant.Stack.stackId}' does not belong to one receipt range.");
            }
            ValidateRestoredRoutableDescendant(
                receipt,
                deliveryRevision,
                descendant.Stack,
                descendant.Metadata);
            assigned[matches[0]].Add(descendant);
        }

        foreach (FacilityOutputExactRouteSliceReceipt slice in expectedSlices)
        {
            RestoredCustodyPart[] parts = assigned[slice]
                .OrderBy(value => value.Metadata.SourceOffsetQuantity)
                .ThenBy(value => value.Stack.stackId, StringComparer.Ordinal)
                .ToArray();
            int nextOffset = slice.SourceOffsetQuantity;
            long totalMass = 0L;
            string originStackId = string.Empty;
            string originDestinationId = string.Empty;
            Vector2Int originPosition = default;
            int originalStackOrdinal = -1;
            foreach (RestoredCustodyPart part in parts)
            {
                FacilityOutputExactRouteCustodyMetadata metadata = part.Metadata;
                if (metadata.SourceOffsetQuantity != nextOffset)
                {
                    throw new InvalidOperationException(
                        $"Exact-route restore receipt range '{slice.RoutedStackId}' has a gap or overlap at {nextOffset}.");
                }
                if (originStackId.Length == 0)
                {
                    originStackId = metadata.OriginStackId;
                    originDestinationId = metadata.OriginDestinationId;
                    originPosition = metadata.OriginPosition;
                    originalStackOrdinal = metadata.OriginalStackOrdinal;
                }
                else if (!string.Equals(
                             originStackId,
                             metadata.OriginStackId,
                             StringComparison.Ordinal)
                         || !string.Equals(
                             originDestinationId,
                             metadata.OriginDestinationId,
                             StringComparison.Ordinal)
                         || originPosition != metadata.OriginPosition
                         || originalStackOrdinal != metadata.OriginalStackOrdinal)
                {
                    throw new InvalidOperationException(
                        $"Exact-route restore receipt range '{slice.RoutedStackId}' changed immutable origin authority.");
                }
                nextOffset = checked(nextOffset + metadata.Quantity);
                totalMass = checked(totalMass + metadata.MassGrams);
            }
            int expectedEnd = checked(
                slice.SourceOffsetQuantity + slice.RoutedQuantity);
            if (parts.Length == 0
                || nextOffset != expectedEnd
                || totalMass != slice.RoutedMassGrams)
            {
                throw new InvalidOperationException(
                    $"Exact-route restore receipt range '{slice.RoutedStackId}' is not exactly conserved.");
            }
        }
    }

    private void ValidateRestoredRoutableDescendant(
        FacilityOutputExactRouteReceipt receipt,
        FacilityOutputExactRouteDeliveryRevisionSnapshot deliveryRevision,
        WorldItemStackSaveData stack,
        FacilityOutputExactRouteCustodyMetadata metadata)
    {
        if (stack.quantity != metadata.Quantity
            || !string.Equals(stack.itemId, metadata.ItemId,
                StringComparison.Ordinal)
            || !CustodyMatchesDeliveryRevision(metadata, deliveryRevision)
            || !IsAllowedRoutablePhysicalState(
                stack,
                deliveryRevision))
        {
            throw new InvalidOperationException(
                $"Exact-route restore descendant '{stack.stackId}' has invalid physical state or quantity.");
        }
        List<ItemInstanceComponentSaveData> business =
            CaptureBusinessComponents(stack.components);
        string signature = FacilityBufferPlannedOutputPublicationService
            .CreateRuntimeComponentSignature(business);
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)stack.itemId,
            stack.itemInstanceId,
            business);
        long mass = massQuery.GetQuantityMass(
            (ItemDefinitionId)stack.itemId,
            subject,
            stack.quantity).Value;
        if (!string.Equals(
                signature,
                metadata.ComponentSignature,
                StringComparison.Ordinal)
            || mass != metadata.MassGrams)
        {
            throw new InvalidOperationException(
                $"Exact-route restore descendant '{stack.stackId}' physical payload changed.");
        }
    }

    private static bool IsAllowedRoutablePhysicalState(
        WorldItemStackSaveData stack,
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery)
    {
        switch (stack.state)
        {
            case WorldItemStackState.Loose:
                if (stack.dropDisposition ==
                    WorldItemDropDisposition.TransientCarryRecoveryDrop)
                {
                    return !string.IsNullOrWhiteSpace(
                            stack.recoveryOwnerOperationId)
                        && !string.IsNullOrWhiteSpace(stack.recoverySourceStackId)
                        && !string.IsNullOrWhiteSpace(
                            stack.recoveryCarrierPersistentId)
                        && stack.recoveryInterruptionKind is
                            WorldItemCarryInterruptionKind.Downed
                            or WorldItemCarryInterruptionKind.Dead
                        && RecoveryDestinationIntentMatches(stack, delivery);
                }
                return stack.dropDisposition == WorldItemDropDisposition.None
                    && DestinationIntentMatches(stack, delivery);
            case WorldItemStackState.Carried:
            case WorldItemStackState.InTransit:
                return stack.dropDisposition == WorldItemDropDisposition.None
                    && !string.IsNullOrWhiteSpace(stack.destinationId)
                    && stack.hasDestinationPosition
                    && !string.Equals(
                        stack.destinationId,
                        delivery.TargetDestinationId,
                        StringComparison.Ordinal);
            case WorldItemStackState.Stored:
                return stack.dropDisposition == WorldItemDropDisposition.None
                    && (string.Equals(
                            stack.destinationId,
                            delivery.TargetDestinationId,
                            StringComparison.Ordinal)
                        || (delivery.TargetDestinationId.Length == 0
                            && IsCanonicalWarehouseDestination(
                                stack.destinationId)))
                    && !stack.hasDestinationPosition;
            case WorldItemStackState.FacilityBuffer:
                return stack.dropDisposition == WorldItemDropDisposition.None
                    && DestinationIntentMatches(stack, delivery);
            default:
                return false;
        }
    }

    private static bool RecoveryDestinationIntentMatches(
        WorldItemStackSaveData stack,
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery)
    {
        if (delivery.TargetDestinationId.Length > 0)
            return DestinationIntentMatches(stack, delivery);
        return IsCanonicalWarehouseDestination(stack.destinationId)
            && stack.hasDestinationPosition;
    }

    private static bool DestinationIntentMatches(
        WorldItemStackSaveData stack,
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery)
    {
        if (delivery.TargetDestinationId.Length == 0)
        {
            return string.IsNullOrEmpty(stack.destinationId)
                && !stack.hasDestinationPosition;
        }
        return string.Equals(
                stack.destinationId,
                delivery.TargetDestinationId,
                StringComparison.Ordinal)
            && stack.hasDestinationPosition
            && stack.destinationGridX == delivery.TargetPositionX
            && stack.destinationGridY == delivery.TargetPositionY;
    }

    private static bool IsCanonicalWarehouseDestination(string destinationId)
    {
        string value = destinationId ?? string.Empty;
        return string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && value.StartsWith(
                WarehouseStorageIdentity.DestinationPrefix,
                StringComparison.Ordinal)
            && value.Length > WarehouseStorageIdentity.DestinationPrefix.Length;
    }

    private static bool CustodyBelongsToReceiptSlice(
        FacilityOutputExactRouteCustodyMetadata metadata,
        FacilityOutputExactRouteReceipt receipt,
        FacilityOutputExactRouteSliceReceipt slice)
    {
        int metadataEnd = checked(
            metadata.SourceOffsetQuantity + metadata.Quantity);
        int sliceEnd = checked(
            slice.SourceOffsetQuantity + slice.RoutedQuantity);
        return string.Equals(metadata.RouteOperationId, receipt.RouteOperationId,
                StringComparison.Ordinal)
            && string.Equals(metadata.RequestFingerprint,
                receipt.RequestFingerprint, StringComparison.Ordinal)
            && string.Equals(metadata.PhysicalReceiptFingerprint,
                receipt.PhysicalReceiptFingerprint, StringComparison.Ordinal)
            && string.Equals(metadata.BatchCommitId, receipt.BatchCommitId,
                StringComparison.Ordinal)
            && string.Equals(metadata.TargetDestinationId,
                receipt.TargetDestinationId, StringComparison.Ordinal)
            && string.Equals(metadata.OutputLineId, slice.OutputLineId,
                StringComparison.Ordinal)
            && string.Equals(metadata.LineCommitId, slice.LineCommitId,
                StringComparison.Ordinal)
            && string.Equals(metadata.ItemId, slice.ItemId,
                StringComparison.Ordinal)
            && string.Equals(metadata.ComponentFingerprint,
                slice.ComponentFingerprint, StringComparison.Ordinal)
            && metadata.SourceOffsetQuantity >= slice.SourceOffsetQuantity
            && metadataEnd <= sliceEnd;
    }

    private void ValidateRestoredRoutedStack(
        FacilityOutputExactRoutePendingSnapshot snapshot,
        FacilityOutputExactRouteReceipt receipt,
        FacilityOutputExactRouteSliceReceipt slice,
        WorldItemStackSaveData stack)
    {
        FacilityOutputExactRouteCustodyPhase expectedPhase = snapshot.Phase ==
            FacilityOutputExactRoutePhase.PhysicalPending
                ? FacilityOutputExactRouteCustodyPhase.PhysicalPending
                : FacilityOutputExactRouteCustodyPhase.Routable;
        if (stack.state != WorldItemStackState.Loose
            || stack.quantity != slice.RoutedQuantity
            || !string.Equals(stack.itemId, slice.ItemId, StringComparison.Ordinal)
            || !FacilityOutputExactRouteCustodyCodec.TryRead(
                stack.components,
                out FacilityOutputExactRouteCustodyMetadata metadata)
            || metadata.Phase != expectedPhase
            || metadata.OriginPosition != new Vector2Int(stack.gridX, stack.gridY)
            || !CustodyMatchesReceipt(metadata, receipt, slice)
            || !CustodyMatchesDeliveryRevision(
                metadata,
                snapshot.DeliveryRevision)
            || !string.Equals(metadata.CurrentSourceStackId, slice.SourceStackId,
                StringComparison.Ordinal)
            || !DestinationIntentMatches(stack, receipt))
        {
            throw new InvalidOperationException(
                $"Exact-route restore stack '{slice.RoutedStackId}' conflicts with its custody receipt.");
        }
        List<ItemInstanceComponentSaveData> business =
            CaptureBusinessComponents(stack.components);
        string signature = FacilityBufferPlannedOutputPublicationService
            .CreateRuntimeComponentSignature(business);
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)stack.itemId,
            stack.itemInstanceId,
            business);
        long mass = massQuery.GetQuantityMass(
            (ItemDefinitionId)stack.itemId,
            subject,
            stack.quantity).Value;
        if (!string.Equals(signature, metadata.ComponentSignature,
                StringComparison.Ordinal)
            || mass != slice.RoutedMassGrams)
        {
            throw new InvalidOperationException(
                $"Exact-route restore stack '{slice.RoutedStackId}' physical payload changed.");
        }
    }

    private static bool DestinationIntentMatches(
        WorldItemStackSaveData stack,
        FacilityOutputExactRouteReceipt receipt)
    {
        if (receipt.TargetDestinationId.Length == 0)
        {
            return string.IsNullOrEmpty(stack.destinationId)
                && !stack.hasDestinationPosition;
        }
        return string.Equals(
                stack.destinationId,
                receipt.TargetDestinationId,
                StringComparison.Ordinal)
            && stack.hasDestinationPosition
            && stack.destinationGridX == receipt.TargetPosition.x
            && stack.destinationGridY == receipt.TargetPosition.y;
    }

    private void RestoreCandidateComponents()
    {
        if (restoreComponentOriginals.Count == 0)
            return;
        if (!repository.TryReplaceBatchComponentsAtomically(
            restoreComponentOriginals,
            out string repositoryFailure))
        {
            throw new InvalidOperationException(
                "Exact-route restore component rollback failed: "
                + repositoryFailure);
        }
    }

    private void ResetRestoreTransaction()
    {
        restoreActive = false;
        restorePublished = false;
        stagedRoutes = null;
        previousRoutes = null;
        previousRestoreOutboxRevision = 0L;
        stagedCheckpointSequence = 0L;
        stagedCheckpointDigest = string.Empty;
        previousRestoreCheckpointSequence = 0L;
        previousRestoreCheckpointDigest = string.Empty;
        restoreComponentOriginals.Clear();
    }

    private bool TryCaptureSourceSegments(
        FacilityOutputExactRouteRequest request,
        out IReadOnlyList<SourceSegment> sources,
        out FacilityOutputExactRouteFailure failure)
    {
        sources = Array.Empty<SourceSegment>();
        failure = FacilityOutputExactRouteFailure.None;
        WorldItemStackRecord[] destinationRecords = repository.Records
            .Where(value => value != null
                && value.state == WorldItemStackState.FacilityOutputBuffer
                && string.Equals(
                    value.destinationId,
                    request.SourceDestinationId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
        if (destinationRecords.Length == 0)
            return Fail(
                FacilityOutputExactRouteFailureCode.SourceUnavailable,
                $"Output destination '{request.SourceDestinationId}' has no physical stack.",
                out failure);
        foreach (WorldItemStackRecord record in destinationRecords)
        {
            if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(record.components)
                && !FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out _))
            {
                return Fail(
                    FacilityOutputExactRouteFailureCode.PublicationAuthorityInvalid,
                    $"Output stack '{record.stackId}' has malformed route custody.",
                    out failure);
            }
        }

        List<SourceSegment> captured = new();
        bool foundPublication = false;
        bool foundCustody = false;
        foreach (WorldItemStackRecord record in destinationRecords)
        {
            if (PlannedOutputPublicationComponentCodec.TryRead(
                    record.components,
                    out PlannedOutputPublicationMetadata publication)
                && string.Equals(
                    publication.BatchCommitId,
                    request.BatchCommitId,
                    StringComparison.Ordinal))
            {
                foundPublication = true;
                if (!publication.Acknowledged
                    || record.quantity != publication.Quantity
                    || record.reservedQuantity != 0
                    || !string.IsNullOrEmpty(record.reservedByPersistentId))
                {
                    return Fail(
                        FacilityOutputExactRouteFailureCode.PublicationAuthorityInvalid,
                        $"Published stack '{record.stackId}' is not acknowledged and unreserved.",
                        out failure);
                }
                continue;
            }
            if (FacilityOutputExactRouteCustodyCodec.TryRead(
                    record.components,
                    out FacilityOutputExactRouteCustodyMetadata custody)
                && string.Equals(
                    custody.BatchCommitId,
                    request.BatchCommitId,
                    StringComparison.Ordinal))
            {
                foundCustody = true;
                if (custody.Phase !=
                        FacilityOutputExactRouteCustodyPhase.OriginBuffered
                    || record.quantity != custody.Quantity
                    || !string.Equals(
                        record.stackId,
                        custody.CurrentSourceStackId,
                        StringComparison.Ordinal)
                    || record.position != custody.OriginPosition
                    || !string.Equals(
                        record.destinationId,
                        custody.OriginDestinationId,
                        StringComparison.Ordinal)
                    || record.hasDestinationPosition
                    || !string.Equals(record.itemId, custody.ItemId,
                        StringComparison.Ordinal)
                    || record.reservedQuantity != 0
                    || !string.IsNullOrEmpty(record.reservedByPersistentId))
                {
                    return Fail(
                        FacilityOutputExactRouteFailureCode.PublicationAuthorityInvalid,
                        $"Custody stack '{record.stackId}' is not an available origin range.",
                        out failure);
                }
                List<ItemInstanceComponentSaveData> business =
                    CaptureBusinessComponents(record.components);
                string signature = FacilityBufferPlannedOutputPublicationService
                    .CreateRuntimeComponentSignature(business);
                long mass = GetMass(record, record.quantity);
                if (!string.Equals(signature, custody.ComponentSignature,
                        StringComparison.Ordinal)
                    || mass != custody.MassGrams)
                {
                    return Fail(
                        FacilityOutputExactRouteFailureCode.PublicationAuthorityInvalid,
                        $"Custody stack '{record.stackId}' physical payload changed.",
                        out failure);
                }
                captured.Add(new SourceSegment
                {
                    Record = record,
                    Metadata = custody
                });
            }
        }
        if (foundPublication && foundCustody)
            return Fail(
                FacilityOutputExactRouteFailureCode.PublicationAuthorityInvalid,
                $"Batch '{request.BatchCommitId}' mixes publication and custody authority.",
                out failure);
        if (foundPublication)
        {
            WorldItemStackRecord[] batch = destinationRecords
                .Where(value => PlannedOutputPublicationComponentCodec.TryRead(
                    value.components,
                    out PlannedOutputPublicationMetadata metadata)
                    && string.Equals(
                        metadata.BatchCommitId,
                        request.BatchCommitId,
                        StringComparison.Ordinal))
                .ToArray();
            foreach (IGrouping<string, WorldItemStackRecord> line in batch.GroupBy(
                         value =>
                         {
                             PlannedOutputPublicationComponentCodec.TryRead(
                                 value.components,
                                 out PlannedOutputPublicationMetadata metadata);
                             return metadata.OutputLineId;
                         },
                         StringComparer.Ordinal))
            {
                int offset = 0;
                foreach (WorldItemStackRecord record in line.OrderBy(value =>
                         {
                             PlannedOutputPublicationComponentCodec.TryRead(
                                 value.components,
                                 out PlannedOutputPublicationMetadata metadata);
                             return metadata.StackOrdinal;
                         }))
                {
                    PlannedOutputPublicationComponentCodec.TryRead(
                        record.components,
                        out PlannedOutputPublicationMetadata publication);
                    List<ItemInstanceComponentSaveData> business =
                        CaptureBusinessComponents(record.components);
                    string componentSignature =
                        FacilityBufferPlannedOutputPublicationService
                            .CreateRuntimeComponentSignature(business);
                    PhysicalItemMassSubject subject =
                        PhysicalItemMassSubjectAdapter.Create(
                            massQuery,
                            (ItemDefinitionId)record.itemId,
                            record.itemInstanceId,
                            business);
                    long mass = massQuery.GetQuantityMass(
                        (ItemDefinitionId)record.itemId,
                        subject,
                        record.quantity).Value;
                    if (!string.Equals(
                            componentSignature,
                            publication.ComponentSignature,
                            StringComparison.Ordinal)
                        || mass != publication.MassGrams
                        || !IsLowercaseSha256(
                            publication.PreparedComponentFingerprint))
                    {
                        return Fail(
                            FacilityOutputExactRouteFailureCode.PublicationAuthorityInvalid,
                            $"Published stack '{record.stackId}' changed before routing.",
                            out failure);
                    }
                    FacilityOutputExactRouteCustodyMetadata custody = new(
                        FacilityOutputExactRouteCustodyPhase.OriginBuffered,
                        publication.BatchCommitId,
                        publication.OutcomeFingerprint,
                        publication.PlannedOutputFingerprint,
                        publication.OutputLineId,
                        BuildLineCommitId(
                            publication.BatchCommitId,
                            publication.OutputLineId),
                        publication.StackOrdinal,
                        publication.BatchStackCount,
                        publication.BatchQuantity,
                        publication.BatchMassGrams,
                        publication.LineStackCount,
                        publication.LineQuantity,
                        publication.LineMassGrams,
                        publication.ItemId,
                        componentSignature,
                        publication.PreparedComponentFingerprint,
                        request.SourceDestinationId,
                        string.Empty,
                        record.stackId,
                        record.stackId,
                        record.position,
                        offset,
                        record.quantity,
                        mass,
                        string.Empty,
                        string.Empty,
                        string.Empty);
                    captured.Add(new SourceSegment
                    {
                        Record = record,
                        Metadata = custody
                    });
                    offset = checked(offset + record.quantity);
                }
            }
        }
        if (captured.Count == 0)
            return Fail(
                FacilityOutputExactRouteFailureCode.SourceUnavailable,
                $"Batch '{request.BatchCommitId}' has no routable physical output.",
                out failure);
        sources = captured
            .OrderBy(value => value.Metadata.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.Metadata.SourceOffsetQuantity)
            .ThenBy(value => value.Record.stackId, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private bool TrySelectRanges(
        FacilityOutputExactRouteRequest request,
        IReadOnlyList<SourceSegment> sources,
        out IReadOnlyList<SelectedRange> selected,
        out FacilityOutputExactRouteFailure failure)
    {
        List<SelectedRange> result = new();
        failure = FacilityOutputExactRouteFailure.None;
        foreach (FacilityOutputExactRouteSliceRequest requested in request.Slices)
        {
            if (!string.Equals(
                    requested.LineCommitId,
                    BuildLineCommitId(
                        request.BatchCommitId,
                        requested.OutputLineId),
                    StringComparison.Ordinal))
            {
                selected = Array.Empty<SelectedRange>();
                return Fail(
                    FacilityOutputExactRouteFailureCode.InvalidRequest,
                    $"Line commit '{requested.LineCommitId}' is not canonical for its batch.",
                    out failure);
            }
            int covered = 0;
            long mass = 0L;
            foreach (SourceSegment source in sources.Where(value => string.Equals(
                         value.Metadata.OutputLineId,
                         requested.OutputLineId,
                         StringComparison.Ordinal)))
            {
                int start = Math.Max(
                    requested.SourceOffsetQuantity,
                    source.Metadata.SourceOffsetQuantity);
                int end = Math.Min(
                    requested.EndOffsetQuantity,
                    checked(source.Metadata.SourceOffsetQuantity
                        + source.Metadata.Quantity));
                if (start >= end)
                    continue;
                if (!string.Equals(
                        source.Metadata.ItemId,
                        requested.ItemId,
                        StringComparison.Ordinal))
                {
                    selected = Array.Empty<SelectedRange>();
                    return Fail(
                        FacilityOutputExactRouteFailureCode.ItemMismatch,
                        $"Line '{requested.OutputLineId}' item changed before routing.",
                        out failure);
                }
                if (!string.Equals(
                        source.Metadata.ComponentFingerprint,
                        requested.ComponentFingerprint,
                        StringComparison.Ordinal))
                {
                    selected = Array.Empty<SelectedRange>();
                    return Fail(
                        FacilityOutputExactRouteFailureCode.ComponentMismatch,
                        $"Line '{requested.OutputLineId}' component fingerprint changed.",
                        out failure);
                }
                int quantity = end - start;
                long routedMass = GetMass(source.Record, quantity);
                result.Add(new SelectedRange
                {
                    Source = source,
                    Request = requested,
                    Start = start,
                    Quantity = quantity,
                    MassGrams = routedMass
                });
                covered = checked(covered + quantity);
                mass = checked(mass + routedMass);
            }
            if (covered != requested.Quantity)
            {
                selected = Array.Empty<SelectedRange>();
                return Fail(
                    FacilityOutputExactRouteFailureCode.RangeUnavailable,
                    $"Line '{requested.OutputLineId}' range is missing, duplicated, or already routed.",
                    out failure);
            }
            if (mass != requested.ExactMassGrams)
            {
                selected = Array.Empty<SelectedRange>();
                return Fail(
                    FacilityOutputExactRouteFailureCode.MassMismatch,
                    $"Line '{requested.OutputLineId}' route mass changed from {requested.ExactMassGrams}g to {mass}g.",
                    out failure);
            }
        }
        if (result.Sum(value => value.Quantity) != request.TotalQuantity
            || result.Sum(value => value.MassGrams) != request.TotalMassGrams)
        {
            selected = Array.Empty<SelectedRange>();
            return Fail(
                FacilityOutputExactRouteFailureCode.MassMismatch,
                "Exact route aggregate totals changed during physical selection.",
                out failure);
        }
        selected = result
            .OrderBy(value => value.Start)
            .ThenBy(value => value.Source.Record.stackId, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private bool TryBuildParts(
        IReadOnlyList<SourceSegment> sources,
        IReadOnlyList<SelectedRange> selected,
        out IReadOnlyList<RecordPart> parts,
        out FacilityOutputExactRouteFailure failure)
    {
        List<RecordPart> result = new();
        failure = FacilityOutputExactRouteFailure.None;
        foreach (SourceSegment source in sources)
        {
            SelectedRange[] selectedForSource = selected
                .Where(value => ReferenceEquals(value.Source, source))
                .OrderBy(value => value.Start)
                .ToArray();
            if (selectedForSource.Length == 0)
            {
                result.Add(CreatePart(
                    source,
                    source.Metadata.SourceOffsetQuantity,
                    source.Metadata.Quantity,
                    routed: false,
                    source.Record.stackId,
                    null));
                continue;
            }
            int cursor = source.Metadata.SourceOffsetQuantity;
            int end = checked(cursor + source.Metadata.Quantity);
            List<RecordPart> sourceParts = new();
            foreach (SelectedRange range in selectedForSource)
            {
                if (range.Start < cursor
                    || range.Start + range.Quantity > end)
                {
                    parts = Array.Empty<RecordPart>();
                    return Fail(
                        FacilityOutputExactRouteFailureCode.RangeUnavailable,
                        $"Source stack '{source.Record.stackId}' has overlapping route ranges.",
                        out failure);
                }
                if (range.Start > cursor)
                {
                    sourceParts.Add(CreatePart(
                        source,
                        cursor,
                        range.Start - cursor,
                        routed: false,
                        string.Empty,
                        null));
                }
                RecordPart routed = CreatePart(
                    source,
                    range.Start,
                    range.Quantity,
                    routed: true,
                    string.Empty,
                    range.Request);
                sourceParts.Add(routed);
                range.RoutedStackId = routed.StackId;
                cursor = checked(range.Start + range.Quantity);
            }
            if (cursor < end)
            {
                sourceParts.Add(CreatePart(
                    source,
                    cursor,
                    end - cursor,
                    routed: false,
                    string.Empty,
                    null));
            }
            if (!string.IsNullOrEmpty(source.Record.itemInstanceId)
                && (sourceParts.Count != 1 || !sourceParts[0].Routed))
            {
                parts = Array.Empty<RecordPart>();
                return Fail(
                    FacilityOutputExactRouteFailureCode.UniquePartialForbidden,
                    $"Unique output stack '{source.Record.stackId}' cannot be split.",
                    out failure);
            }

            RecordPart keeper = sourceParts.Count == 1
                ? sourceParts[0]
                : sourceParts.FirstOrDefault(value => !value.Routed)
                    ?? sourceParts[0];
            keeper.StackId = source.Record.stackId;
            foreach (RecordPart part in sourceParts)
            {
                if (!ReferenceEquals(part, keeper))
                    part.StackId = repository.AllocateStackId();
            }
            result.AddRange(sourceParts);
        }
        parts = result
            .OrderBy(value => value.Source.Metadata.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.Start)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        return true;
    }

    private RecordPart CreatePart(
        SourceSegment source,
        int start,
        int quantity,
        bool routed,
        string stackId,
        FacilityOutputExactRouteSliceRequest request) => new()
    {
        Source = source,
        Start = start,
        Quantity = quantity,
        MassGrams = GetMass(source.Record, quantity),
        Routed = routed,
        StackId = stackId ?? string.Empty,
        Request = request
    };

    private bool TryCommit(
        FacilityOutputExactRouteRequest request,
        IReadOnlyList<SourceSegment> sources,
        IReadOnlyList<RecordPart> parts,
        FacilityOutputExactRouteReceipt receipt,
        out FacilityOutputExactRouteFailure failure)
    {
        failure = FacilityOutputExactRouteFailure.None;
        Dictionary<string, WorldItemStackRecord> originals = sources
            .ToDictionary(
                value => value.Record.stackId,
                value => CloneRecord(value.Record),
                StringComparer.Ordinal);
        Dictionary<string, RecordPart> keepers = parts
            .Where(value => originals.ContainsKey(value.StackId))
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        WorldItemStackRecord[] additions = parts
            .Where(value => !originals.ContainsKey(value.StackId))
            .Select(value => BuildPartRecord(
                value,
                request,
                receipt.PhysicalReceiptFingerprint))
            .ToArray();
        try
        {
            foreach (SourceSegment source in sources)
            {
                RecordPart keeper = keepers[source.Record.stackId];
                ApplyPart(
                    source.Record,
                    keeper,
                    request,
                    receipt.PhysicalReceiptFingerprint);
            }
            if (additions.Length > 0
                && !repository.TryAddBatchAtomically(
                    additions,
                    failBeforeAdd: null,
                    out string addFailure))
            {
                RestoreOriginals(originals);
                return Fail(
                    FacilityOutputExactRouteFailureCode.RepositoryTransactionFailed,
                    addFailure,
                    out failure);
            }
            if (additions.Length == 0)
                repository.MarkChanged();
            routes.Add(
                request.RouteOperationId,
                new FacilityOutputExactRoutePendingSnapshot(
                    FacilityOutputExactRoutePhase.PhysicalPending,
                    receipt));
            outboxRevision = checked(outboxRevision + 1L);
        }
        catch (Exception exception)
        {
            if (additions.Length > 0)
            {
                WorldItemStackRecord[] liveAdditions = additions
                    .Where(value => repository.RecordsById.ContainsKey(value.stackId))
                    .ToArray();
                if (liveAdditions.Length > 0)
                    repository.TryRemoveBatchAtomically(liveAdditions, out _);
            }
            RestoreOriginals(originals);
            routes.Remove(request.RouteOperationId);
            return Fail(
                FacilityOutputExactRouteFailureCode.RepositoryTransactionFailed,
                "Exact-route rollback: " + exception.Message,
                out failure);
        }
        foreach (WorldItemStackRecord original in originals.Values)
            markers.RefreshAt(original.position);
        foreach (RecordPart part in parts)
        {
            Vector2Int position = part.Source.Metadata.OriginPosition;
            markers.RefreshAt(position);
        }
        return true;
    }

    private WorldItemStackRecord BuildPartRecord(
        RecordPart part,
        FacilityOutputExactRouteRequest request,
        string physicalFingerprint)
    {
        WorldItemStackRecord record = CloneRecord(part.Source.Record);
        record.stackId = part.StackId;
        record.itemInstanceId = string.Empty;
        ApplyPart(record, part, request, physicalFingerprint, relocate: false);
        return record;
    }

    private void ApplyPart(
        WorldItemStackRecord record,
        RecordPart part,
        FacilityOutputExactRouteRequest request,
        string physicalFingerprint,
        bool relocate = true)
    {
        Vector2Int targetPosition = part.Source.Metadata.OriginPosition;
        if (relocate)
            repository.Relocate(record, targetPosition);
        else
            record.position = targetPosition;
        record.quantity = part.Quantity;
        record.state = part.Routed
            ? WorldItemStackState.Loose
            : WorldItemStackState.FacilityOutputBuffer;
        record.destinationId = part.Routed
            ? request.TargetDestinationId
            : request.SourceDestinationId;
        record.sourceStorageDestinationId = string.Empty;
        record.hasDestinationPosition = part.Routed
            && request.TargetDestinationId.Length > 0;
        record.destinationPosition = record.hasDestinationPosition
            ? request.TargetPosition
            : default;
        record.aggregationCohortId = string.Empty;
        record.reservedByPersistentId = string.Empty;
        record.reservedQuantity = 0;
        record.reservationRevision = checked(record.reservationRevision + 1L);
        FacilityOutputExactRouteCustodyPhase phase = part.Routed
            ? FacilityOutputExactRouteCustodyPhase.PhysicalPending
            : FacilityOutputExactRouteCustodyPhase.OriginBuffered;
        FacilityOutputExactRouteCustodyMetadata custody =
            part.Source.Metadata.WithSlice(
                phase,
                part.Routed ? request.TargetDestinationId : string.Empty,
                part.Routed ? part.Source.Record.stackId : part.StackId,
                part.Start,
                part.Quantity,
                part.MassGrams,
                part.Routed ? request.RouteOperationId : string.Empty,
                part.Routed ? request.RequestFingerprint : string.Empty,
                part.Routed ? physicalFingerprint : string.Empty,
                part.Routed
                    ? FacilityOutputExactRouteDeliveryRevisionSnapshot
                        .CreateInitial(
                            request.RouteOperationId,
                            request.RequestFingerprint,
                            physicalFingerprint,
                            request.TargetDestinationId,
                            request.TargetPosition.x,
                            request.TargetPosition.y)
                    : null);
        record.components = FacilityOutputExactRouteCustodyCodec.ReplaceAuthority(
            record.components,
            custody);
    }

    private void RestoreOriginals(
        IReadOnlyDictionary<string, WorldItemStackRecord> originals)
    {
        foreach (KeyValuePair<string, WorldItemStackRecord> pair in originals)
        {
            if (!repository.RecordsById.TryGetValue(
                    pair.Key,
                    out WorldItemStackRecord live))
                continue;
            repository.Relocate(live, pair.Value.position);
            CopyRecordState(pair.Value, live);
        }
        repository.MarkChanged();
    }

    private long GetMass(WorldItemStackRecord record, int quantity)
    {
        List<ItemInstanceComponentSaveData> business =
            CaptureBusinessComponents(record.components);
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)record.itemId,
            record.itemInstanceId,
            business);
        return massQuery.GetQuantityMass(
            (ItemDefinitionId)record.itemId,
            subject,
            quantity).Value;
    }

    private static bool IsCheckpointGcStable(
        WorldItemStackRecord record,
        ISet<string> activeHaulStackIds) => record != null
        && record.state is WorldItemStackState.Loose
            or WorldItemStackState.Stored
            or WorldItemStackState.FacilityBuffer
        && record.dropDisposition == WorldItemDropDisposition.None
        && record.reservedQuantity == 0
        && string.IsNullOrEmpty(record.reservedByPersistentId)
        && string.IsNullOrEmpty(record.recoveryOwnerOperationId)
        && string.IsNullOrEmpty(record.recoverySourceStackId)
        && string.IsNullOrEmpty(record.recoveryCarrierPersistentId)
        && record.recoveryInterruptionKind == WorldItemCarryInterruptionKind.None
        && !activeHaulStackIds.Contains(record.stackId);

    private static WorldItemStackSaveData CaptureSaveData(
        WorldItemStackRecord record) => new()
    {
        stackId = record.stackId,
        itemInstanceId = record.itemInstanceId,
        itemId = record.itemId,
        quantity = record.quantity,
        state = record.state,
        gridX = record.position.x,
        gridY = record.position.y,
        reservedByPersistentId = record.reservedByPersistentId,
        destinationId = record.destinationId,
        aggregationCohortId = record.aggregationCohortId,
        sourceStorageDestinationId = record.sourceStorageDestinationId,
        hasDestinationPosition = record.hasDestinationPosition,
        destinationGridX = record.destinationPosition.x,
        destinationGridY = record.destinationPosition.y,
        forbidden = record.forbidden,
        sourceCharacterId = record.sourceCharacterId,
        sourceDisplayName = record.sourceDisplayName,
        sourceSpeciesTag = record.sourceSpeciesTag,
        sourceDeathReason = record.sourceDeathReason,
        emergencyButcheryAllowed = record.emergencyButcheryAllowed,
        wasteOrigin = record.wasteOrigin,
        contamination = record.contamination,
        components = (record.components
                ?? new List<ItemInstanceComponentSaveData>())
            .Select(value => value.Clone())
            .ToList(),
        dropDisposition = record.dropDisposition,
        recoveryOwnerOperationId = record.recoveryOwnerOperationId,
        recoverySourceStackId = record.recoverySourceStackId,
        recoveryCarrierPersistentId = record.recoveryCarrierPersistentId,
        recoveryInterruptionKind = record.recoveryInterruptionKind,
        droppedAtGameTime = record.droppedAtGameTime,
        recoveryDeadlineGameTime = record.recoveryDeadlineGameTime
    };

    private static bool ComponentsEqual(
        IEnumerable<ItemInstanceComponentSaveData> left,
        IEnumerable<ItemInstanceComponentSaveData> right)
    {
        ItemInstanceComponentSaveData[] first = (left
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .ToArray();
        ItemInstanceComponentSaveData[] second = (right
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .ToArray();
        if (first.Length != second.Length)
            return false;
        for (int index = 0; index < first.Length; index++)
        {
            ItemInstanceComponentSaveData a = first[index];
            ItemInstanceComponentSaveData b = second[index];
            if (a == null || b == null
                || !string.Equals(a.componentTypeId, b.componentTypeId,
                    StringComparison.Ordinal)
                || a.schemaVersion != b.schemaVersion
                || a.affectsStacking != b.affectsStacking)
            {
                return false;
            }
            ItemStateValueSaveData[] aValues = (a.values
                    ?? new List<ItemStateValueSaveData>()).ToArray();
            ItemStateValueSaveData[] bValues = (b.values
                    ?? new List<ItemStateValueSaveData>()).ToArray();
            if (aValues.Length != bValues.Length)
                return false;
            for (int valueIndex = 0; valueIndex < aValues.Length; valueIndex++)
            {
                ItemStateValueSaveData av = aValues[valueIndex];
                ItemStateValueSaveData bv = bValues[valueIndex];
                if (av == null || bv == null
                    || !string.Equals(av.key, bv.key, StringComparison.Ordinal)
                    || av.kind != bv.kind
                    || !string.Equals(av.stringValue, bv.stringValue,
                        StringComparison.Ordinal)
                    || av.integerValue != bv.integerValue
                    || av.decimalValue != bv.decimalValue
                    || av.booleanValue != bv.booleanValue)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private PreparedOutputCheckpointGcResult GcResult(
        PreparedOutputCheckpointGcStatus status,
        PreparedOutputCheckpointGcReason reason,
        PreparedOutputCheckpointGcContext context,
        string message,
        int collectedBatchCount = 0) => new(
        status,
        reason,
        context.CheckpointSequence,
        message,
        collectedBatchCount);

    private static void ValidateCheckpointState(
        long sequence,
        string digest)
    {
        string value = digest ?? string.Empty;
        if (sequence < 0L
            || (sequence == 0L && value.Length != 0)
            || (sequence > 0L && !IsLowerSha256(value)))
        {
            throw new InvalidOperationException(
                "Items exact-route checkpoint sequence/digest authority is invalid.");
        }
    }

    private static bool IsLowerSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!((character >= '0' && character <= '9')
                  || (character >= 'a' && character <= 'f')))
            {
                return false;
            }
        }
        return true;
    }

    private static FacilityOutputExactRouteDestructiveRetireResult
        DestructiveRetireResult(
        FacilityOutputExactRouteDestructiveRetireStatus status,
        string sourceDestinationId,
        string candidateFingerprint,
        int routeCount,
        int stackCount,
        string reason) => new(
            status,
            sourceDestinationId,
            candidateFingerprint,
            routeCount,
            stackCount,
            reason);

    private static string CreateDestructiveRetireFingerprint(
        string sourceDestinationId,
        string batchCommitId,
        long sourceOutboxRevision,
        IReadOnlyList<FacilityOutputExactRoutePendingSnapshot> selectedRoutes,
        IReadOnlyList<RestoredCustodyPart> descendants)
    {
        StringBuilder canonical = new();
        canonical.Append("facility-output-exact-route-destructive-retire@1\n")
            .Append(sourceDestinationId).Append('\n')
            .Append(batchCommitId).Append('\n')
            .Append(sourceOutboxRevision).Append('\n');
        foreach (FacilityOutputExactRoutePendingSnapshot route in
                 (selectedRoutes
                     ?? Array.Empty<FacilityOutputExactRoutePendingSnapshot>())
                 .OrderBy(value => value.Receipt.RouteOperationId,
                     StringComparer.Ordinal))
        {
            canonical.Append("route|")
                .Append(route.Receipt.RouteOperationId).Append('|')
                .Append((int)route.Phase).Append('|')
                .Append(route.Receipt.RequestFingerprint).Append('|')
                .Append(route.Receipt.PhysicalReceiptFingerprint).Append('|')
                .Append(route.DeliveryRevision.Revision).Append('|')
                .Append(route.DeliveryRevision.RevisionFingerprint).Append('\n');
        }
        foreach (RestoredCustodyPart descendant in (descendants
                     ?? Array.Empty<RestoredCustodyPart>())
                 .OrderBy(value => value.Stack.stackId, StringComparer.Ordinal))
        {
            WorldItemStackSaveData stack = descendant.Stack;
            FacilityOutputExactRouteCustodyMetadata metadata =
                descendant.Metadata;
            canonical.Append("stack|")
                .Append(stack.stackId).Append('|')
                .Append(stack.itemInstanceId).Append('|')
                .Append(stack.itemId).Append('|')
                .Append(stack.quantity).Append('|')
                .Append((int)stack.state).Append('|')
                .Append(stack.gridX).Append('|').Append(stack.gridY).Append('|')
                .Append(stack.destinationId).Append('|')
                .Append(stack.hasDestinationPosition ? '1' : '0').Append('|')
                .Append(stack.destinationGridX).Append('|')
                .Append(stack.destinationGridY).Append('|')
                .Append(metadata.RouteOperationId).Append('|')
                .Append(metadata.SourceOffsetQuantity).Append('|')
                .Append(metadata.Quantity).Append('|')
                .Append(metadata.MassGrams).Append('|')
                .Append(metadata.ComponentFingerprint).Append('\n');
        }
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical.ToString()));
        StringBuilder fingerprint = new(digest.Length * 2);
        foreach (byte value in digest)
            fingerprint.Append(value.ToString("x2"));
        return fingerprint.ToString();
    }

    private DestructiveRetireCandidate RequireDestructiveRetireCandidate(
        IFacilityOutputExactRouteDestructiveRetireCandidate candidate)
    {
        if (candidate is not DestructiveRetireCandidate exact
            || !ReferenceEquals(activeDestructiveRetireCandidate, exact))
        {
            throw new InvalidOperationException(
                "Exact-route destructive retire candidate owner conflicts.");
        }
        return exact;
    }

    private CheckpointGcCandidate RequireGcCandidate(
        IPreparedOutputCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || !string.Equals(exact.ParticipantId,
                CheckpointGcParticipantId, StringComparison.Ordinal)
            || exact.ParticipantKind != CheckpointGcParticipantKind)
        {
            throw new InvalidOperationException(
                "Items checkpoint candidate owner conflicts.");
        }
        return exact;
    }

    private DeliveryOverlayCandidate RequireDeliveryOverlayCandidate(
        IFacilityOutputExactRouteDeliveryOverlayCandidate candidate)
    {
        if (candidate is not DeliveryOverlayCandidate exact
            || !ReferenceEquals(activeDeliveryOverlayCandidate, exact))
        {
            throw new InvalidOperationException(
                "Items delivery overlay candidate owner conflicts.");
        }
        return exact;
    }

    private void RestoreDeliveryOverlayPhysical(
        DeliveryOverlayCandidate exact)
    {
        if (exact.Originals.Count > 0
            && !repository.TryReplaceBatchComponentsAtomically(
                exact.Originals,
                out string componentFailure))
        {
            throw new InvalidOperationException(
                "Items delivery overlay rollback failed: " + componentFailure);
        }
        foreach (KeyValuePair<string, DeliveryPhysicalOriginal> pair in
                 exact.PhysicalOriginals)
        {
            if (!repository.RecordsById.TryGetValue(
                    pair.Key,
                    out WorldItemStackRecord record))
            {
                throw new InvalidOperationException(
                    $"Items delivery overlay rollback lost '{pair.Key}'.");
            }
            pair.Value.Restore(record);
        }
        routes = exact.PreviousRoutes;
        outboxRevision = exact.SourceOutboxRevision;
        repository.MarkChanged();
    }

    private static bool SameDeliveryRevision(
        FacilityOutputExactRouteDeliveryRevisionSnapshot left,
        FacilityOutputExactRouteDeliveryRevisionSnapshot right) =>
        left != null && right != null
        && left.Revision == right.Revision
        && string.Equals(left.RevisionFingerprint, right.RevisionFingerprint,
            StringComparison.Ordinal)
        && string.Equals(left.RerouteOperationId, right.RerouteOperationId,
            StringComparison.Ordinal)
        && string.Equals(left.OriginalPhysicalReceiptFingerprint,
            right.OriginalPhysicalReceiptFingerprint, StringComparison.Ordinal)
        && string.Equals(left.TargetDestinationId, right.TargetDestinationId,
            StringComparison.Ordinal)
        && left.TargetPositionX == right.TargetPositionX
        && left.TargetPositionY == right.TargetPositionY
        && string.Equals(left.TargetAuthorityFingerprint,
            right.TargetAuthorityFingerprint, StringComparison.Ordinal);

    private static List<ItemInstanceComponentSaveData> CaptureBusinessComponents(
        IEnumerable<ItemInstanceComponentSaveData> components) => (components
            ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Where(value => value != null
            && !PlannedOutputPublicationComponentCodec.IsAnyMarker(value)
            && !FacilityOutputExactRouteCustodyCodec.IsCustody(value))
        .Select(value => value.Clone())
        .ToList();

    private static string BuildLineCommitId(
        string batchCommitId,
        string outputLineId) =>
        $"{batchCommitId}:line:{outputLineId}";

    private static bool CustodyMatchesReceipt(
        FacilityOutputExactRouteCustodyMetadata metadata,
        FacilityOutputExactRouteReceipt receipt,
        FacilityOutputExactRouteSliceReceipt slice) =>
        string.Equals(metadata.RouteOperationId, receipt.RouteOperationId,
            StringComparison.Ordinal)
        && string.Equals(metadata.RequestFingerprint, receipt.RequestFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            metadata.PhysicalReceiptFingerprint,
            receipt.PhysicalReceiptFingerprint,
            StringComparison.Ordinal)
        && string.Equals(metadata.BatchCommitId, receipt.BatchCommitId,
            StringComparison.Ordinal)
        && string.Equals(
            metadata.TargetDestinationId,
            receipt.TargetDestinationId,
            StringComparison.Ordinal)
        && string.Equals(metadata.OutputLineId, slice.OutputLineId,
            StringComparison.Ordinal)
        && string.Equals(metadata.LineCommitId, slice.LineCommitId,
            StringComparison.Ordinal)
        && string.Equals(metadata.ItemId, slice.ItemId, StringComparison.Ordinal)
        && string.Equals(
            metadata.CurrentSourceStackId,
            slice.SourceStackId,
            StringComparison.Ordinal)
        && string.Equals(
            metadata.ComponentFingerprint,
            slice.ComponentFingerprint,
            StringComparison.Ordinal)
        && metadata.SourceOffsetQuantity == slice.SourceOffsetQuantity
        && metadata.Quantity == slice.RoutedQuantity
        && metadata.MassGrams == slice.RoutedMassGrams;

    private static bool DestinationIntentMatches(
        WorldItemStackRecord record,
        FacilityOutputExactRouteReceipt receipt)
    {
        if (receipt.TargetDestinationId.Length == 0)
        {
            return string.IsNullOrEmpty(record.destinationId)
                && !record.hasDestinationPosition;
        }
        return string.Equals(
                record.destinationId,
                receipt.TargetDestinationId,
                StringComparison.Ordinal)
            && record.hasDestinationPosition
            && record.destinationPosition == receipt.TargetPosition;
    }

    private static FacilityOutputExactRoutePendingSnapshot CloneSnapshot(
        FacilityOutputExactRoutePendingSnapshot source) =>
        FromSaveData(ToSaveData(source));

    private static FacilityOutputExactRouteOutboxSaveData ToSaveData(
        FacilityOutputExactRoutePendingSnapshot source) => new()
    {
        phase = source.Phase,
        routeOperationId = source.Receipt.RouteOperationId,
        requestFingerprint = source.Receipt.RequestFingerprint,
        physicalReceiptFingerprint = source.Receipt.PhysicalReceiptFingerprint,
        batchCommitId = source.Receipt.BatchCommitId,
        sourceDestinationId = source.Receipt.SourceDestinationId,
        targetDestinationId = source.Receipt.TargetDestinationId,
        targetPositionX = source.Receipt.TargetPosition.x,
        targetPositionY = source.Receipt.TargetPosition.y,
        totalQuantity = source.Receipt.TotalQuantity,
        totalMassGrams = source.Receipt.TotalMassGrams,
        currentDeliveryRevision = source.DeliveryRevision.Revision,
        currentDeliveryRevisionFingerprint =
            source.DeliveryRevision.RevisionFingerprint,
        currentDeliveryRerouteOperationId =
            source.DeliveryRevision.RerouteOperationId,
        currentTargetDestinationId =
            source.DeliveryRevision.TargetDestinationId,
        currentTargetPositionX = source.DeliveryRevision.TargetPositionX,
        currentTargetPositionY = source.DeliveryRevision.TargetPositionY,
        currentTargetAuthorityFingerprint =
            source.DeliveryRevision.TargetAuthorityFingerprint,
        slices = source.Receipt.Slices.Select(value =>
            new FacilityOutputExactRouteSliceSaveData
            {
                sourceStackId = value.SourceStackId,
                routedStackId = value.RoutedStackId,
                outputLineId = value.OutputLineId,
                lineCommitId = value.LineCommitId,
                itemId = value.ItemId,
                sourceOffsetQuantity = value.SourceOffsetQuantity,
                routedOffsetQuantity = value.RoutedOffsetQuantity,
                routedQuantity = value.RoutedQuantity,
                routedMassGrams = value.RoutedMassGrams,
                componentFingerprint = value.ComponentFingerprint
            }).ToList()
    };

    private static FacilityOutputExactRoutePendingSnapshot FromSaveData(
        FacilityOutputExactRouteOutboxSaveData source)
    {
        if (source == null)
            throw new InvalidOperationException("Exact-route save entry is null.");
        FacilityOutputExactRouteSliceReceipt[] slices = (source.slices
                ?? new List<FacilityOutputExactRouteSliceSaveData>())
            .Select(value => value ?? throw new InvalidOperationException(
                "Exact-route save slice is null."))
            .Select(value => new FacilityOutputExactRouteSliceReceipt(
                value.sourceStackId,
                value.routedStackId,
                value.outputLineId,
                value.lineCommitId,
                value.itemId,
                value.sourceOffsetQuantity,
                value.routedOffsetQuantity,
                value.routedQuantity,
                value.routedMassGrams,
                value.componentFingerprint))
            .ToArray();
        FacilityOutputExactRouteReceipt receipt = new(
            source.routeOperationId,
            source.requestFingerprint,
            source.physicalReceiptFingerprint,
            source.batchCommitId,
            source.sourceDestinationId,
            source.targetDestinationId,
            new Vector2Int(source.targetPositionX, source.targetPositionY),
            source.totalQuantity,
            source.totalMassGrams,
            slices);
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery = new(
            source.routeOperationId,
            source.physicalReceiptFingerprint,
            source.currentDeliveryRevision,
            source.currentDeliveryRevisionFingerprint,
            source.currentDeliveryRerouteOperationId,
            source.currentTargetDestinationId,
            source.currentTargetPositionX,
            source.currentTargetPositionY,
            source.currentTargetAuthorityFingerprint);
        return new FacilityOutputExactRoutePendingSnapshot(
            source.phase,
            receipt,
            delivery);
    }

    private static void ValidateRestoredDeliveryRevision(
        FacilityOutputExactRouteReceipt receipt,
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery)
    {
        if (delivery == null
            || !string.Equals(delivery.RouteOperationId,
                receipt.RouteOperationId, StringComparison.Ordinal)
            || !string.Equals(delivery.OriginalPhysicalReceiptFingerprint,
                receipt.PhysicalReceiptFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Exact-route delivery revision conflicts for '{receipt.RouteOperationId}'.");
        }
        if (delivery.Revision == 0L)
        {
            FacilityOutputExactRouteDeliveryRevisionSnapshot expected =
                FacilityOutputExactRouteDeliveryRevisionSnapshot.CreateInitial(
                    receipt.RouteOperationId,
                    receipt.RequestFingerprint,
                    receipt.PhysicalReceiptFingerprint,
                    receipt.TargetDestinationId,
                    receipt.TargetPosition.x,
                    receipt.TargetPosition.y);
            if (!string.Equals(delivery.RevisionFingerprint,
                    expected.RevisionFingerprint, StringComparison.Ordinal)
                || !string.Equals(delivery.TargetDestinationId,
                    expected.TargetDestinationId, StringComparison.Ordinal)
                || delivery.TargetPositionX != expected.TargetPositionX
                || delivery.TargetPositionY != expected.TargetPositionY)
            {
                throw new InvalidOperationException(
                    $"Exact-route initial delivery revision drifted for '{receipt.RouteOperationId}'.");
            }
        }
    }

    private static bool CustodyMatchesDeliveryRevision(
        FacilityOutputExactRouteCustodyMetadata metadata,
        FacilityOutputExactRouteDeliveryRevisionSnapshot delivery) =>
        delivery != null
        && metadata.CurrentDeliveryRevision == delivery.Revision
        && string.Equals(metadata.CurrentDeliveryRevisionFingerprint,
            delivery.RevisionFingerprint, StringComparison.Ordinal)
        && string.Equals(metadata.CurrentDeliveryRerouteOperationId,
            delivery.RerouteOperationId, StringComparison.Ordinal)
        && string.Equals(metadata.CurrentTargetDestinationId,
            delivery.TargetDestinationId, StringComparison.Ordinal)
        && metadata.CurrentTargetPosition == new Vector2Int(
            delivery.TargetPositionX,
            delivery.TargetPositionY)
        && string.Equals(metadata.CurrentTargetAuthorityFingerprint,
            delivery.TargetAuthorityFingerprint, StringComparison.Ordinal);

    private static WorldItemStackRecord CloneRecord(
        WorldItemStackRecord source)
    {
        WorldItemStackRecord result = new();
        CopyRecordState(source, result);
        return result;
    }

    private static void CopyRecordState(
        WorldItemStackRecord source,
        WorldItemStackRecord target)
    {
        target.stackId = source.stackId;
        target.itemInstanceId = source.itemInstanceId;
        target.itemId = source.itemId;
        target.quantity = source.quantity;
        target.state = source.state;
        target.position = source.position;
        target.reservedByPersistentId = source.reservedByPersistentId;
        target.reservedQuantity = source.reservedQuantity;
        target.reservationRevision = source.reservationRevision;
        target.destinationId = source.destinationId;
        target.aggregationCohortId = source.aggregationCohortId;
        target.sourceStorageDestinationId = source.sourceStorageDestinationId;
        target.hasDestinationPosition = source.hasDestinationPosition;
        target.destinationPosition = source.destinationPosition;
        target.forbidden = source.forbidden;
        target.sourceCharacterId = source.sourceCharacterId;
        target.sourceDisplayName = source.sourceDisplayName;
        target.sourceSpeciesTag = source.sourceSpeciesTag;
        target.sourceDeathReason = source.sourceDeathReason;
        target.emergencyButcheryAllowed = source.emergencyButcheryAllowed;
        target.wasteOrigin = source.wasteOrigin;
        target.contamination = source.contamination;
        target.components = (source.components
                ?? new List<ItemInstanceComponentSaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .ToList();
        target.dropDisposition = source.dropDisposition;
        target.recoveryOwnerOperationId = source.recoveryOwnerOperationId;
        target.recoverySourceStackId = source.recoverySourceStackId;
        target.recoveryCarrierPersistentId = source.recoveryCarrierPersistentId;
        target.recoveryInterruptionKind = source.recoveryInterruptionKind;
        target.droppedAtGameTime = source.droppedAtGameTime;
        target.recoveryDeadlineGameTime = source.recoveryDeadlineGameTime;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        foreach (char character in value)
        {
            if (!(character is >= '0' and <= '9')
                && !(character is >= 'a' and <= 'f'))
                return false;
        }
        return true;
    }

    private static bool Fail(
        FacilityOutputExactRouteFailureCode code,
        string reason,
        out FacilityOutputExactRouteFailure failure)
    {
        failure = new FacilityOutputExactRouteFailure(code, reason);
        return false;
    }
}
