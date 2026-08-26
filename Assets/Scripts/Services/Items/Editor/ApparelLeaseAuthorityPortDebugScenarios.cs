#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;

public static class ApparelLeaseAuthorityPortDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Items/Run Apparel Lease Authority Port")]
    public static void RunAll()
    {
        string restore = VerifyRestoreStableReleaseAndReplay();
        string quantity = VerifyQuantityDriftConflictsWithoutMutation();
        string signature = VerifySignatureDriftConflictsWithoutMutation();
        UnityEngine.Debug.Log(
            "Apparel lease authority port PASS\n"
            + restore + "\n"
            + quantity + "\n"
            + signature);
    }

    private static string VerifyRestoreStableReleaseAndReplay()
    {
        const string targetOwner = "apparel-work-order:qa-target";
        const string otherOwner = "apparel-work-order:qa-other";
        MutableGameClock clock = new();
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string stackId = repository.AddEditorTestStack(
            "material:apparel-lease-authority-qa",
            3,
            WorldItemStackState.Loose);
        string signature = ItemStackSignature.Create(
            "material:apparel-lease-authority-qa",
            Array.Empty<ItemInstanceComponentSaveData>());
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            clock);
        Reserve(reservations, targetOwner, stackId, 1, signature);
        Reserve(reservations, otherOwner, stackId, 1, signature);

        ApparelLeaseAuthorityPort port = new(reservations);
        Require(port.TryCapture(
                targetOwner,
                out ApparelLeaseAuthoritySnapshot before,
                out string beforeFailure),
            "initial target capture failed: " + beforeFailure);
        Require(port.TryCapture(
                otherOwner,
                out ApparelLeaseAuthoritySnapshot otherBefore,
                out string otherFailure),
            "initial other-owner capture failed: " + otherFailure);
        Require(reservations.TryGetLeasesByOwner(
                targetOwner,
                out IReadOnlyList<ItemQuantityLease> beforeLeases),
            "initial target lease was missing");
        string beforeTransientLeaseId = beforeLeases.Single().leaseId;

        IReadOnlyList<ItemReservationIntentSaveData> intents =
            reservations.CaptureReservationIntents();
        reservations.ResetTransientLedger();
        Require(reservations.TryRestoreGrandfathered(
                intents,
                out DomainFailure restoreFailure),
            "reservation restore failed: " + restoreFailure);
        Require(reservations.TryGetLeasesByOwner(
                targetOwner,
                out IReadOnlyList<ItemQuantityLease> restoredLeases),
            "restored target lease was missing");
        Require(!string.Equals(
                beforeTransientLeaseId,
                restoredLeases.Single().leaseId,
                StringComparison.Ordinal),
            "restore unexpectedly reused the transient lease ID");
        Require(port.TryCapture(
                targetOwner,
                out ApparelLeaseAuthoritySnapshot restored,
                out string restoredFailure),
            "restored target capture failed: " + restoredFailure);
        Require(string.Equals(
                before.Fingerprint,
                restored.Fingerprint,
                StringComparison.Ordinal),
            "stable authority fingerprint changed across restore");

        IWorldItemStackRuntime unusedRuntime = DispatchProxy.Create<
            IWorldItemStackRuntime,
            NullDispatchProxy>();
        LeasedItemReservationService emptyLegacyWrapper = new(
            unusedRuntime,
            reservations,
            clock);
        FieldInfo cacheField = typeof(LeasedItemReservationService).GetField(
            "byOwner",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(cacheField?.GetValue(emptyLegacyWrapper) is IDictionary cache
                && cache.Count == 0,
            "fresh legacy wrapper did not have an empty rebuildable cache");

        ApparelLeaseAuthorityReleaseResult applied = port.TryReleaseExact(
            targetOwner,
            before.Fingerprint,
            ItemReservationReleaseReason.OwnerRemoved);
        Require(applied.Status == ApparelLeaseAuthorityReleaseStatus.Applied
                && applied.ReleasedLeaseCount == 1,
            "exact release was not applied: " + applied.FailureReason);
        Require(!reservations.TryGetLeasesByOwner(targetOwner, out _),
            "exact release left an orphan target lease");
        Require(reservations.GetReservedQuantity(new ItemStackId(stackId)) == 1,
            "exact release changed the other owner's reserved quantity");
        Require(port.TryCapture(
                otherOwner,
                out ApparelLeaseAuthoritySnapshot otherAfter,
                out string otherAfterFailure)
            && string.Equals(
                otherBefore.Fingerprint,
                otherAfter.Fingerprint,
                StringComparison.Ordinal),
            "exact release mutated the other owner: " + otherAfterFailure);

        ApparelLeaseAuthorityReleaseResult replay = port.TryReleaseExact(
            targetOwner,
            before.Fingerprint,
            ItemReservationReleaseReason.OwnerRemoved);
        Require(replay.Status == ApparelLeaseAuthorityReleaseStatus.Replay
                && replay.ReleasedLeaseCount == 0,
            "exact replay did not become a no-op");
        return "restore_fingerprint=stable; wrapper_cache=0; orphan=0; "
            + "other_owner=unchanged; replay=no-op";
    }

    private static string VerifyQuantityDriftConflictsWithoutMutation()
    {
        const string owner = "apparel-work-order:qa-quantity-drift";
        MutableReservationAuthority authority = new();
        authority.Set(owner, 1, "signature:stable");
        ApparelLeaseAuthorityPort port = new(authority);
        Require(port.TryCapture(
                owner,
                out ApparelLeaseAuthoritySnapshot frozen,
                out string captureFailure),
            "quantity baseline capture failed: " + captureFailure);
        authority.Set(owner, 2, "signature:stable");

        ApparelLeaseAuthorityReleaseResult result = port.TryReleaseExact(
            owner,
            frozen.Fingerprint,
            ItemReservationReleaseReason.Cancelled);
        Require(result.Status == ApparelLeaseAuthorityReleaseStatus.Conflict,
            "one-quantity drift was not rejected");
        Require(authority.ReleaseByOwnerCallCount == 0
                && authority.LiveQuantity == 2,
            "quantity conflict mutated live authority");
        return "quantity_drift=+1; status=conflict; mutation=0";
    }

    private static string VerifySignatureDriftConflictsWithoutMutation()
    {
        const string owner = "apparel-work-order:qa-signature-drift";
        MutableReservationAuthority authority = new();
        authority.Set(owner, 1, "signature:before");
        ApparelLeaseAuthorityPort port = new(authority);
        Require(port.TryCapture(
                owner,
                out ApparelLeaseAuthoritySnapshot frozen,
                out string captureFailure),
            "signature baseline capture failed: " + captureFailure);
        authority.Set(owner, 1, "signature:after");

        ApparelLeaseAuthorityReleaseResult result = port.TryReleaseExact(
            owner,
            frozen.Fingerprint,
            ItemReservationReleaseReason.Cancelled);
        Require(result.Status == ApparelLeaseAuthorityReleaseStatus.Conflict,
            "signature drift was not rejected");
        Require(authority.ReleaseByOwnerCallCount == 0
                && string.Equals(
                    authority.LiveSignature,
                    "signature:after",
                    StringComparison.Ordinal),
            "signature conflict mutated live authority");
        return "signature_drift=1; status=conflict; mutation=0";
    }

    private static void Reserve(
        IItemQuantityReservationService reservations,
        string owner,
        string stackId,
        int quantity,
        string signature)
    {
        Require(reservations.TryReserve(
                owner,
                string.Empty,
                ItemReservationPurpose.ProductionInput,
                "production:" + owner,
                new ItemQuantityReservationRequest(
                    new ItemStackId(stackId),
                    quantity,
                    signature),
                out _,
                out DomainFailure failure),
            "reservation setup failed: " + failure);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class MutableGameClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    public class NullDispatchProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                Type type = parameters[index].ParameterType;
                if (!type.IsByRef)
                    continue;
                Type elementType = type.GetElementType();
                args[index] = elementType != null && elementType.IsValueType
                    ? Activator.CreateInstance(elementType)
                    : null;
            }
            Type returnType = targetMethod.ReturnType;
            return returnType == typeof(void)
                ? null
                : returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
        }
    }

    private sealed class MutableReservationAuthority :
        IItemQuantityReservationService
    {
        private ItemQuantityLease live;

        public ItemReservationRestoreDiagnostics LastRestoreDiagnostics { get; } =
            new();
        public int ReleaseByOwnerCallCount { get; private set; }
        public int LiveQuantity => live?.remainingQuantity ?? 0;
        public string LiveSignature =>
            live?.slices?.SingleOrDefault()?.expectedStackSignature
            ?? string.Empty;

        public void Set(string owner, int quantity, string signature)
        {
            live = new ItemQuantityLease
            {
                leaseId = "transient:" + Guid.NewGuid().ToString("N"),
                ownerOperationId = owner,
                ownerCharacterId = string.Empty,
                purpose = ItemReservationPurpose.ProductionInput,
                aggregationCohortId = "production:" + owner,
                originalQuantity = quantity,
                remainingQuantity = quantity,
                slices = new List<ItemLeaseSlice>
                {
                    new()
                    {
                        stackId = "stack:apparel-lease-qa",
                        originStackId = "stack:apparel-lease-qa",
                        expectedStackSignature = signature,
                        quantity = quantity
                    }
                },
                createdAtGameSeconds = 1d,
                expiresAtGameSeconds = 2d,
                maximumExpiresAtGameSeconds = 3d
            };
        }

        public bool TryGetLeasesByOwner(
            string ownerOperationId,
            out IReadOnlyList<ItemQuantityLease> leases)
        {
            if (live != null && string.Equals(
                    live.ownerOperationId,
                    ownerOperationId,
                    StringComparison.Ordinal))
            {
                leases = new[] { live.Clone() };
                return true;
            }
            leases = Array.Empty<ItemQuantityLease>();
            return false;
        }

        public int ReleaseByOwner(
            string ownerOperationId,
            ItemReservationReleaseReason reason)
        {
            ReleaseByOwnerCallCount++;
            if (live == null || !string.Equals(
                    live.ownerOperationId,
                    ownerOperationId,
                    StringComparison.Ordinal))
            {
                return 0;
            }
            live = null;
            return 1;
        }

        public bool TryReserve(
            string ownerOperationId,
            string ownerCharacterId,
            ItemReservationPurpose purpose,
            string aggregationCohortId,
            ItemQuantityReservationRequest request,
            out ItemQuantityLease lease,
            out DomainFailure failure) =>
            throw new NotSupportedException();

        public bool TryReserveBatch(
            string ownerOperationId,
            string ownerCharacterId,
            ItemReservationPurpose purpose,
            string aggregationCohortId,
            IReadOnlyList<ItemQuantityReservationRequest> requests,
            out IReadOnlyList<ItemQuantityLease> leases,
            out DomainFailure failure) =>
            throw new NotSupportedException();

        public bool Revalidate(
            string leaseId,
            out ItemQuantityLease lease,
            out DomainFailure failure) =>
            throw new NotSupportedException();

        public bool Renew(
            string leaseId,
            double requestedUntilGameSeconds,
            out DomainFailure failure) =>
            throw new NotSupportedException();

        public bool Release(
            string leaseId,
            ItemReservationReleaseReason reason) =>
            throw new NotSupportedException();

        public IReadOnlyList<ItemQuantityLease> GetLeasesForStack(
            ItemStackId stackId) =>
            throw new NotSupportedException();

        public int GetReservedQuantity(ItemStackId stackId) =>
            throw new NotSupportedException();

        public int GetAvailableQuantity(ItemStackId stackId) =>
            throw new NotSupportedException();
    }
}
#endif
