using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public enum DungeonWindowMode
{
    Windowed,
    Borderless,
    ExclusiveFullscreen
}

public enum DungeonCameraControlScheme
{
    WasdAndArrows,
    WasdOnly,
    ArrowsOnly
}

public enum DungeonDefenseTimeResponse
{
    SlowToX1,
    PauseOnCritical,
    KeepCurrent
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonUserSettingsData
{
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public DungeonWindowMode windowMode = DungeonWindowMode.Borderless;
    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public float masterVolume = 0.8f;
    public float musicVolume = 0.55f;
    public float effectsVolume = 0.8f;
    public float uiVolume = 0.8f;
    public float cameraSpeed = 1f;
    public bool edgeScroll;
    public DungeonCameraControlScheme cameraControls =
        DungeonCameraControlScheme.WasdAndArrows;
    public float uiScale = 1f;
    public float textScale = 1f;
    public float maxCarryMultiplier = 1.5f;
    public bool highContrast;
    public bool reducedMotion;
    public bool developerMode;
    public bool pauseOnResearchTree;
    public DungeonDefenseTimeResponse defenseTimeResponse =
        DungeonDefenseTimeResponse.SlowToX1;

    public DungeonUserSettingsData Clone()
    {
        return (DungeonUserSettingsData)MemberwiseClone();
    }

    public void Normalize()
    {
        version = CurrentVersion;
        if (!Enum.IsDefined(typeof(DungeonWindowMode), windowMode))
        {
            windowMode = DungeonWindowMode.Borderless;
        }

        if (!Enum.IsDefined(typeof(DungeonCameraControlScheme), cameraControls))
        {
            cameraControls = DungeonCameraControlScheme.WasdAndArrows;
        }

        if (!Enum.IsDefined(
                typeof(DungeonDefenseTimeResponse),
                defenseTimeResponse))
        {
            defenseTimeResponse = DungeonDefenseTimeResponse.SlowToX1;
        }

        resolutionWidth = Mathf.Clamp(resolutionWidth, 960, 7680);
        resolutionHeight = Mathf.Clamp(resolutionHeight, 540, 4320);
        masterVolume = Mathf.Clamp01(masterVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        effectsVolume = Mathf.Clamp01(effectsVolume);
        uiVolume = Mathf.Clamp01(uiVolume);
        cameraSpeed = Mathf.Clamp(cameraSpeed, 0.5f, 2f);
        uiScale = Mathf.Clamp(uiScale, 0.8f, 1.25f);
        textScale = Mathf.Clamp(textScale, 0.9f, 1.25f);
        maxCarryMultiplier = Mathf.Clamp(
            Mathf.Round(maxCarryMultiplier / 0.05f) * 0.05f,
            1f,
            2.5f);
    }
}

public interface IDungeonUserSettingsService
{
    DungeonUserSettingsData Current { get; }
    event Action Changed;
    string SettingsPath { get; }
    string LastError { get; }
    void Update(Action<DungeonUserSettingsData> change);
    void ResetDefaults();
    void ApplyCurrent();
}
