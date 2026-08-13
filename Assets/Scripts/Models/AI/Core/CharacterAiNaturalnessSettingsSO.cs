using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(menuName = "DungeonStory/AI/Naturalness Settings", order = 10)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterAiNaturalnessSettingsSO : ScriptableObject
{
    [Header("Soft Lock")]
    [SerializeField, Min(0f)] private float softLockMinimumSeconds = 1.15f;
    [SerializeField, Min(0f)] private float softLockMaximumSeconds = 4.5f;
    [SerializeField, Range(0f, 0.5f)] private float softLockScoreBonus = 0.14f;

    [Header("Signals")]
    [SerializeField, Min(1f)] private float nearbyCharacterRadius = 4f;
    [SerializeField, Min(1f)] private float wildlifeThreatRadius = 7f;
    [SerializeField, Min(0f)] private float signalCacheSeconds = 0.25f;

    [Header("Micro Behavior")]
    [SerializeField, Range(0f, 1f)] private float queueWaitThreshold = 0.35f;
    [SerializeField, Range(0f, 1f)] private float shelterWeatherThreshold = 0.55f;
    [SerializeField, Range(0f, 1f)] private float stepAsideFailureThreshold = 0.35f;

    [Header("Facility Selection")]
    [SerializeField, Min(0f)] private float freeFacilityTravelCells = 4f;
    [SerializeField, Min(0f)] private float facilityTravelUtilityCostPerCell = 0.015f;
    [SerializeField, Range(0f, 1f)] private float maximumFacilityTravelUtilityPenalty = 0.35f;
    [SerializeField, Range(0f, 0.5f)] private float equivalentFacilityUtilityTolerance = 0.12f;
    [SerializeField, Min(0f)] private float equivalentFacilityMinimumTravelSavingsCells = 4f;

    public float SoftLockMinimumSeconds => Mathf.Max(0f, softLockMinimumSeconds);
    public float SoftLockMaximumSeconds => Mathf.Max(SoftLockMinimumSeconds, softLockMaximumSeconds);
    public float SoftLockScoreBonus => Mathf.Clamp(softLockScoreBonus, 0f, 0.5f);
    public float NearbyCharacterRadius => Mathf.Max(1f, nearbyCharacterRadius);
    public float WildlifeThreatRadius => Mathf.Max(1f, wildlifeThreatRadius);
    public float SignalCacheSeconds => Mathf.Max(0f, signalCacheSeconds);
    public float QueueWaitThreshold => Mathf.Clamp01(queueWaitThreshold);
    public float ShelterWeatherThreshold => Mathf.Clamp01(shelterWeatherThreshold);
    public float StepAsideFailureThreshold => Mathf.Clamp01(stepAsideFailureThreshold);
    public float FreeFacilityTravelCells => Mathf.Max(0f, freeFacilityTravelCells);
    public float FacilityTravelUtilityCostPerCell =>
        Mathf.Max(0f, facilityTravelUtilityCostPerCell);
    public float MaximumFacilityTravelUtilityPenalty =>
        Mathf.Clamp01(maximumFacilityTravelUtilityPenalty);
    public float EquivalentFacilityUtilityTolerance =>
        Mathf.Clamp(equivalentFacilityUtilityTolerance, 0f, 0.5f);
    public float EquivalentFacilityMinimumTravelSavingsCells =>
        Mathf.Max(0f, equivalentFacilityMinimumTravelSavingsCells);
}
