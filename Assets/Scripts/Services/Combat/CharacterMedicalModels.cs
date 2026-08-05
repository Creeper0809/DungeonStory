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

public enum CharacterMedicalStatusCode
{
    Unknown = 0,
    AwaitingStabilization,
    PreparingStabilization,
    Stabilizing,
    StabilizedWithInfectionRisk,
    AwaitingRescue,
    PreparingTransfer,
    Carrying,
    AwaitingBed,
    Treating,
    TreatingWithExtractedBlood,
    AdditionalTreatmentRequired,
    TreatmentCompleted,
    TreatmentRequested,
    SupplyUnavailable,
    AwaitingMedicineDelivery,
    AwaitingExtractedBloodDelivery,
    MedicineReady,
    ReservationReleased,
    Cancelled,
    PatientMissing,
    RescuerMissing,
    PatientDied,
    RescuerDied,
    RescueInterrupted,
    StabilizationInterrupted,
    TreatmentInterrupted,
    PatientPathUnavailable,
    TreatmentPathUnavailable,
    Restarted,
    ManualRescueAssigned
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
    public CharacterMedicalStatusCode statusCode;
    public List<string> statusParameters = new List<string>();

    public void SetStatus(
        CharacterMedicalStatusCode code,
        params string[] parameters)
    {
        statusCode = code;
        statusParameters.Clear();
        if (parameters == null)
        {
            return;
        }

        foreach (string parameter in parameters)
        {
            statusParameters.Add(parameter ?? string.Empty);
        }
    }

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
    public const int CurrentVersion = 3;

    public int version;
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

public interface ICharacterMedicalQuery
{
    IReadOnlyList<CharacterMedicalOrder> ActiveOrders { get; }
    bool HasAvailableRescueOrder(CharacterActor rescuer);
    bool TryGetOrder(string orderId, out CharacterMedicalOrder order);
    bool TryGetPatient(CharacterMedicalOrder order, out CharacterActor patient);
    bool TryGetTreatmentFacility(CharacterMedicalOrder order, out BuildableObject facility);
}

public interface ICharacterMedicalCommand
{
    bool TryReserveBestOrder(
        CharacterActor rescuer,
        out CharacterMedicalOrder order,
        out DomainFailure failure);
    bool TryReserveOrderForPatient(
        CharacterActor rescuer,
        CharacterActor patient,
        out CharacterMedicalOrder order,
        out DomainFailure failure);
    bool TryRequestTreatment(
        CharacterActor patient,
        out CharacterMedicalOrder order,
        out DomainFailure failure);
    bool TryAssignSpecificTreatmentFacility(
        string orderId,
        BuildableObject facility,
        out DomainFailure failure);
    float AdvanceStabilization(string orderId, CharacterActor rescuer, float work);
    bool TryBeginCarrying(string orderId, CharacterActor rescuer, out DomainFailure failure);
    bool TryPlaceAtTreatmentDestination(
        string orderId,
        CharacterActor rescuer,
        out DomainFailure failure);
    float AdvanceTreatment(string orderId, CharacterActor rescuer, float work);
    bool TryReleaseReservation(
        string orderId,
        CharacterActor rescuer,
        CharacterMedicalStatusCode releaseStatus,
        out DomainFailure failure);
    void NotifyCharacterDowned(CharacterActor actor);
    void NotifyCharacterRecovered(CharacterActor actor);
}

public interface ICharacterMedicalPersistence
{
    DungeonCharacterMedicalSaveData Capture();
    CharacterMedicalRestoreCandidate PrepareRestore(
        DungeonCharacterMedicalSaveData saveData);
    void PublishRestore(CharacterMedicalRestoreCandidate candidate);
}

public interface ICharacterCarePriorityQuery
{
    bool IsCareSubject(string persistentCharacterId);
    int GetCarePriority(string persistentCharacterId);
}

public sealed class CharacterPhysicalCapacityQuery :
    ICharacterPhysicalCapacityQuery
{
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly IAnatomyEffectRuntime anatomyEffects;

    public CharacterPhysicalCapacityQuery(
        ICharacterBodyHealthQuery bodyHealthQuery,
        IAnatomyEffectRuntime anatomyEffects)
    {
        this.bodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        this.anatomyEffects = anatomyEffects
            ?? throw new ArgumentNullException(nameof(anatomyEffects));
    }

    public CharacterPhysicalCapacitySnapshot GetSnapshot(CharacterActor actor)
    {
        CharacterBodyHealthSnapshot snapshot = bodyHealthQuery.GetSnapshot(actor);
        return new CharacterPhysicalCapacitySnapshot(
            snapshot.Consciousness,
            snapshot.Manipulation,
            snapshot.Mobility);
    }

    public float GetMoveMultiplier(CharacterActor actor)
    {
        return anatomyEffects
            .GetActivityFactor(actor, AnatomyActivityId.Movement)
            .AppliedFactor;
    }

    public float GetWorkMultiplier(CharacterActor actor, WorkTypeId workTypeId)
    {
        AnatomyActivityId activity = workTypeId == BuiltInWorkTypeIds.Haul
            || workTypeId == BuiltInWorkTypeIds.Rescue
                ? AnatomyActivityId.Carry
                : workTypeId == BuiltInWorkTypeIds.Treat
                    || workTypeId == BuiltInWorkTypeIds.Surgery
                        ? AnatomyActivityId.Treatment
                        : AnatomyActivityId.Work;
        return anatomyEffects.GetActivityFactor(actor, activity).AppliedFactor;
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
