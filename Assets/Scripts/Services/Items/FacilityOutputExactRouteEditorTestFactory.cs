#if UNITY_EDITOR
using UnityEngine;

public static class FacilityOutputExactRouteEditorTestFactory
{
    [GameplayInternalOnly(
        "Creates one canonical Routable exact-route custody component for isolated Editor save/runtime fixtures.",
        "Prepared-output and capacity-routing focused Editor fixtures only")]
    public static ItemInstanceComponentSaveData CreateRoutableCustody(
        string batchCommitId,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        string outputLineId,
        string lineCommitId,
        int originalStackOrdinal,
        int originalBatchStackCount,
        int originalBatchQuantity,
        long originalBatchMassGrams,
        string itemId,
        string componentSignature,
        string componentFingerprint,
        string originDestinationId,
        string targetDestinationId,
        string originStackId,
        string currentSourceStackId,
        Vector2Int originPosition,
        int sourceOffsetQuantity,
        int quantity,
        long massGrams,
        string routeOperationId,
        string requestFingerprint,
        string physicalReceiptFingerprint,
        long currentDeliveryRevision = -1L,
        string currentDeliveryRevisionFingerprint = "",
        string currentTargetDestinationId = "",
        Vector2Int currentTargetPosition = default,
        string currentTargetAuthorityFingerprint = "")
    {
        FacilityOutputExactRouteCustodyMetadata metadata = new(
            FacilityOutputExactRouteCustodyPhase.Routable,
            batchCommitId,
            outcomeFingerprint,
            plannedOutputFingerprint,
            outputLineId,
            lineCommitId,
            originalStackOrdinal,
            originalBatchStackCount,
            originalBatchQuantity,
            originalBatchMassGrams,
            originalLineStackCount: originalBatchStackCount,
            originalLineQuantity: originalBatchQuantity,
            originalLineMassGrams: originalBatchMassGrams,
            itemId,
            componentSignature,
            componentFingerprint,
            originDestinationId,
            targetDestinationId,
            originStackId,
            currentSourceStackId,
            originPosition,
            sourceOffsetQuantity,
            quantity,
            massGrams,
            routeOperationId,
            requestFingerprint,
            physicalReceiptFingerprint,
            currentDeliveryRevision,
            currentDeliveryRevisionFingerprint,
            currentDeliveryRerouteOperationId: string.Empty,
            currentTargetDestinationId,
            currentTargetPosition.x,
            currentTargetPosition.y,
            currentTargetAuthorityFingerprint);
        return FacilityOutputExactRouteCustodyCodec.Create(metadata);
    }
}
#endif
