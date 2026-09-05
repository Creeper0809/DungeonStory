using System;
using System.Collections.Generic;

[Serializable]
public sealed class DungeonDurableFacilityEquipmentSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public long nextAssignmentSequence = 1L;
    public long revision = 1L;
    public List<DurableFacilityEquipmentSlotSaveData> slots = new();
}

[Serializable]
public sealed class DurableFacilityEquipmentSlotSaveData
{
    public string logicalOwnerDomain = string.Empty;
    public string ownerSubjectId = string.Empty;
    public string policyId = string.Empty;
    public long policyRevision;
    public string capacityPolicyKind = string.Empty;
    public string usabilityPolicyKind = string.Empty;
    public string ownerFacilityId = string.Empty;
    public int dropPositionX;
    public int dropPositionY;
    public long assignmentSequence;
    public string assignmentFingerprint = string.Empty;
    public long maximumMassGrams;
    public long sourceAuthorityRevision;
    public string sourceAuthorityFingerprint = string.Empty;
    public DurableFacilityEquipmentSlotLifecyclePhase lifecyclePhase;
    public string closeReasonCode = string.Empty;
    public bool authoritiesRevoked;
    public string drainParentOperationId = string.Empty;
    public string drainStepOperationId = string.Empty;
    public string drainOwnerStableId = string.Empty;
    public string drainOwnerSubjectId = string.Empty;
    public string drainOwnerFacilityId = string.Empty;
    public string drainSourceDestinationId = string.Empty;
    public string drainSourceAuthorityFingerprint = string.Empty;
    public string drainRequestFingerprint = string.Empty;
    public int drainOwnerGridX;
    public int drainOwnerGridY;
    public FacilityBufferDestinationCustodyDrainPhase drainPhase;
    public int drainSourceActorCount;
    public int drainCompletedActorCount;
    public int drainSourceOperationCount;
    public int drainReleasedOperationCount;
    public int drainInputQuantity;
    public long drainInputMassGrams;
    public int drainReleasedQuantity;
    public long drainReleasedMassGrams;
    public string drainCommitId = string.Empty;
    public string drainReceiptFingerprint = string.Empty;
}

public interface IDurableFacilityEquipmentSlotPersistence
{
    DungeonDurableFacilityEquipmentSaveData CaptureSaveData();

    void PublishRestoreCandidate(
        DurableFacilityEquipmentRestoreCandidate candidate);
}

public sealed class DurableFacilityEquipmentRestoreCandidate
{
    private readonly IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> slots;

    public DurableFacilityEquipmentRestoreCandidate(
        long nextAssignmentSequence,
        long revision,
        IEnumerable<DurableFacilityEquipmentSlotSnapshot> slots)
    {
        DurableFacilityEquipmentSlotSnapshot[] copied =
            System.Linq.Enumerable.ToArray(
                slots ?? throw new ArgumentNullException(nameof(slots)));
        if (nextAssignmentSequence <= 0L
            || revision <= 0L
            || System.Linq.Enumerable.Any(copied, value => value == null)
            || System.Linq.Enumerable.Count(
                System.Linq.Enumerable.Distinct(
                    System.Linq.Enumerable.Select(
                        copied,
                        value => value.AssignmentSequence)))
                != copied.Length
            || System.Linq.Enumerable.Any(
                System.Linq.Enumerable.GroupBy(
                    System.Linq.Enumerable.Where(
                        copied,
                        value => value.LifecyclePhase !=
                            DurableFacilityEquipmentSlotLifecyclePhase
                                .ClosedAwaitingCheckpointGc),
                    value => value.Key),
                group => System.Linq.Enumerable.Count(group) != 1)
            || (copied.Length > 0
                && nextAssignmentSequence <=
                    System.Linq.Enumerable.Max(
                        copied,
                        value => value.AssignmentSequence)))
        {
            throw new ArgumentException(
                "Durable facility-equipment restore candidate is invalid.");
        }
        NextAssignmentSequence = nextAssignmentSequence;
        Revision = revision;
        this.slots = Array.AsReadOnly(
            System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.OrderBy(
                    copied,
                    value => value.AssignmentSequence)));
    }

    public long NextAssignmentSequence { get; }
    public long Revision { get; }
    public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> Slots => slots;
}
