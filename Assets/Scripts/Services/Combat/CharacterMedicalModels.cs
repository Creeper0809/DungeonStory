using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

public enum CharacterMedicalOrderState
{
    AwaitingStabilization = 0,
    Stabilizing = 1,
    AwaitingRescue = 2,
    Carrying = 3,
    AwaitingBed = 4,
    Treating = 5,
    Recovering = 6,
    Completed = 7,
    Cancelled = 8
}

public enum CharacterMedicalSupplyKind
{
    None = 0,
    Medicine = 1,
    ExtractedBlood = 2
}

[Serializable]
public sealed class CharacterMedicalOrder
{
    public string orderId = string.Empty;
    public string patientId = string.Empty;
    public string rescuerId = string.Empty;
    public string treatmentFacilityId = string.Empty;
    public CharacterMedicalOrderState state;
    public bool stabilized;
    public bool carried;
    public float requiredStabilizationWork;
    public float completedStabilizationWork;
    public float requiredTreatmentWork;
    public float completedTreatmentWork;
    public CharacterMedicalSupplyKind treatmentSupply;
    public bool treatmentSupplyConsumed;
    public bool treatmentSupplyDeliveryRequested;
    public string treatmentItemId = string.Empty;
    public float treatmentPotency = 1f;
    public float treatmentInfectionReduction;
    public float treatmentPainReduction;
    public string treatmentMaterialDestinationId = string.Empty;
    public int patientX;
    public int patientY;
    public int bedX;
    public int bedY;
    public string status = string.Empty;

    public Vector2Int PatientPosition
    {
        get => new Vector2Int(patientX, patientY);
        set
        {
            patientX = value.x;
            patientY = value.y;
        }
    }

    public Vector2Int BedPosition
    {
        get => new Vector2Int(bedX, bedY);
        set
        {
            bedX = value.x;
            bedY = value.y;
        }
    }

    public bool IsActive => state != CharacterMedicalOrderState.Completed
        && state != CharacterMedicalOrderState.Cancelled;
}

[Serializable]
public sealed class DungeonCharacterMedicalSaveData
{
    public List<CharacterMedicalOrder> orders = new List<CharacterMedicalOrder>();
    public int orderSequence;
}

public readonly struct CharacterPhysicalCapacitySnapshot
{
    public CharacterPhysicalCapacitySnapshot(
        float consciousness,
        float manipulation,
        float mobility)
    {
        Consciousness = Mathf.Clamp01(consciousness);
        Manipulation = Mathf.Clamp01(manipulation);
        Mobility = Mathf.Clamp01(mobility);
    }

    public float Consciousness { get; }
    public float Manipulation { get; }
    public float Mobility { get; }
}

public interface ICharacterPhysicalCapacityQuery
{
    CharacterPhysicalCapacitySnapshot GetSnapshot(CharacterActor actor);
    float GetMoveMultiplier(CharacterActor actor);
    float GetWorkMultiplier(CharacterActor actor, WorkTypeId workTypeId);
}

public interface ICharacterMedicalRuntime
{
    IReadOnlyList<CharacterMedicalOrder> ActiveOrders { get; }
    bool HasAvailableRescueOrder(CharacterActor rescuer);
    bool TryReserveBestOrder(
        CharacterActor rescuer,
        out CharacterMedicalOrder order,
        out string failureReason);
    bool TryReserveOrderForPatient(
        CharacterActor rescuer,
        CharacterActor patient,
        out CharacterMedicalOrder order,
        out string failureReason);
    bool TryRequestTreatment(
        CharacterActor patient,
        out CharacterMedicalOrder order,
        out string failureReason);
    bool TryGetOrder(string orderId, out CharacterMedicalOrder order);
    bool TryGetPatient(CharacterMedicalOrder order, out CharacterActor patient);
    bool TryGetTreatmentFacility(CharacterMedicalOrder order, out BuildableObject facility);
    bool TryAssignSpecificTreatmentFacility(
        string orderId,
        BuildableObject facility,
        out string failureReason);
    float AdvanceStabilization(string orderId, CharacterActor rescuer, float work);
    bool TryBeginCarrying(string orderId, CharacterActor rescuer, out string failureReason);
    bool TryPlaceAtTreatmentDestination(
        string orderId,
        CharacterActor rescuer,
        out string failureReason);
    float AdvanceTreatment(string orderId, CharacterActor rescuer, float work);
    void ReleaseReservation(string orderId, CharacterActor rescuer, string reason);
    void NotifyCharacterDowned(CharacterActor actor);
    void NotifyCharacterRecovered(CharacterActor actor);
    DungeonCharacterMedicalSaveData Capture();
    void Restore(DungeonCharacterMedicalSaveData saveData, IList<string> warnings);
}

public interface ICharacterCarePriorityQuery
{
    bool IsCareSubject(string persistentCharacterId);
    int GetCarePriority(string persistentCharacterId);
}

public sealed class CharacterPhysicalCapacityQuery :
    ICharacterPhysicalCapacityQuery
{
    private readonly ICharacterBodyHealthRuntime bodyHealth;

    public CharacterPhysicalCapacityQuery(ICharacterBodyHealthRuntime bodyHealth)
    {
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
    }

    public CharacterPhysicalCapacitySnapshot GetSnapshot(CharacterActor actor)
    {
        CharacterBodyHealthSnapshot snapshot = bodyHealth.GetSnapshot(actor);
        return new CharacterPhysicalCapacitySnapshot(
            snapshot.Consciousness,
            snapshot.Manipulation,
            snapshot.Mobility);
    }

    public float GetMoveMultiplier(CharacterActor actor)
    {
        CharacterPhysicalCapacitySnapshot snapshot = GetSnapshot(actor);
        return Mathf.Clamp01(Mathf.Min(snapshot.Consciousness, snapshot.Mobility));
    }

    public float GetWorkMultiplier(CharacterActor actor, WorkTypeId workTypeId)
    {
        CharacterPhysicalCapacitySnapshot snapshot = GetSnapshot(actor);
        float relevantCapacity;
        if (workTypeId == BuiltInWorkTypeIds.Haul
            || workTypeId == BuiltInWorkTypeIds.Rescue
            || workTypeId == BuiltInWorkTypeIds.Guard
            || workTypeId == BuiltInWorkTypeIds.Hunt)
        {
            relevantCapacity = Mathf.Min(snapshot.Manipulation, snapshot.Mobility);
        }
        else if (workTypeId == BuiltInWorkTypeIds.Research
            || workTypeId == BuiltInWorkTypeIds.Treat
            || workTypeId == BuiltInWorkTypeIds.Surgery)
        {
            relevantCapacity = Mathf.Min(snapshot.Consciousness, snapshot.Manipulation);
        }
        else
        {
            relevantCapacity = snapshot.Manipulation;
        }

        return Mathf.Clamp01(Mathf.Min(snapshot.Consciousness, relevantCapacity));
    }
}

public sealed class DownedCharacterGridOccupant : IGridOccupant
{
    public DownedCharacterGridOccupant(CharacterActor actor)
    {
        Actor = actor;
    }

    public CharacterActor Actor { get; }
    public int GridId => Actor != null ? Actor.GetInstanceID() : 0;
    public bool IsGridDestroyed => Actor == null || Actor.IsDead;
    public bool IsGridVisitable => true;
    public bool IsGridMovement => false;
}
