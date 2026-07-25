using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "FacilityCrimeSettings",
    menuName = "DungeonStory/Buildings/Facility Crime Settings",
    order = 0)]
public sealed class FacilityCrimeSettingsSO : ScriptableObject
{
    [Header("Shared Crime Pressure")]
    [SerializeField, Range(0f, 1f)] private float baseCrimePressure = 0.01f;
    [SerializeField, Range(0f, 1f)] private float unstaffedSupervisionRisk = 0.07f;
    [SerializeField, Range(0f, 1f)] private float staffedSupervisionReduction = 0.03f;
    [SerializeField, Range(0f, 1f)] private float lowMoodCrimeRiskWeight = 0.08f;
    [SerializeField, Range(0f, 1f)] private float unmetNeedCrimeRiskWeight = 0.05f;
    [SerializeField, Range(0f, 1f)] private float crowdCrimeRiskWeight = 0.04f;
    [SerializeField, Range(0f, 1f)] private float highValueCrimeRiskWeight = 0.05f;
    [SerializeField, Range(0f, 1f)] private float damagedFacilityCrimeRiskWeight = 0.05f;

    [Header("World Threat")]
    [SerializeField, Min(0f)] private float operationalRiskScale = 10f;

    public float BaseCrimePressure => Mathf.Max(0f, baseCrimePressure);
    public float UnstaffedSupervisionRisk => Mathf.Max(0f, unstaffedSupervisionRisk);
    public float StaffedSupervisionReduction => Mathf.Max(0f, staffedSupervisionReduction);
    public float LowMoodCrimeRiskWeight => Mathf.Max(0f, lowMoodCrimeRiskWeight);
    public float UnmetNeedCrimeRiskWeight => Mathf.Max(0f, unmetNeedCrimeRiskWeight);
    public float CrowdCrimeRiskWeight => Mathf.Max(0f, crowdCrimeRiskWeight);
    public float HighValueCrimeRiskWeight => Mathf.Max(0f, highValueCrimeRiskWeight);
    public float DamagedFacilityCrimeRiskWeight => Mathf.Max(0f, damagedFacilityCrimeRiskWeight);
    public float OperationalRiskScale => Mathf.Max(0f, operationalRiskScale);
}

public interface IFacilityCrimeSettingsProvider
{
    FacilityCrimeSettingsSO Settings { get; }
}

public sealed class ResourceFacilityCrimeSettingsProvider : IFacilityCrimeSettingsProvider
{
    public const string ResourcePath = "Config/FacilityCrimeSettings";

    private readonly IResourcesAssetLoader assetLoader;
    private FacilityCrimeSettingsSO settings;

    public ResourceFacilityCrimeSettingsProvider(IResourcesAssetLoader assetLoader)
    {
        this.assetLoader = assetLoader
            ?? throw new ArgumentNullException(nameof(assetLoader));
    }

    public FacilityCrimeSettingsSO Settings
    {
        get
        {
            if (settings == null)
            {
                settings = assetLoader.LoadRequired<FacilityCrimeSettingsSO>(ResourcePath);
            }

            return settings;
        }
    }
}
