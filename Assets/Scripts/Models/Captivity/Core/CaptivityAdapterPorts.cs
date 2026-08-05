using UnityEngine;

public interface ICaptivityActorEffectsPort
{
    bool IsAvailable(string captiveId);
    void ConfineLaborer(string captiveId);
    void SetPerformerAssignment(string captiveId, bool assigned);
}

public interface ICaptivityPerformerSupplyPort
{
    bool TryRequestFacilityDelivery(
        CaptivityFacilitySupplyKind supplyKind,
        int amount,
        Vector2Int destination,
        string destinationId,
        out int requested,
        out string failureReason);
}

public interface ICaptivityEventEffectsPort
{
    void Publish(CaptivePerformerMilestoneEvent gameEvent);
    void RaiseAlert(
        string title,
        string message,
        CaptivityMilestoneImportance importance,
        string category);
}

public interface ICaptivityRetaliationEffectsPort
{
    bool TryGetCaptive(string captiveId, out CaptiveState captive);
    void ApplyThreat(float amount, bool forceCandidate);
    void RaiseAlert(
        string title,
        string message,
        CaptivityMilestoneImportance importance,
        string category);
}
