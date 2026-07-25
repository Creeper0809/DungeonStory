using System;
using System.IO;
using UnityEngine;
using VContainer.Unity;

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

[Serializable]
public sealed class DungeonUserSettingsData
{
    public const int CurrentVersion = 2;

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
    public DungeonCameraControlScheme cameraControls = DungeonCameraControlScheme.WasdAndArrows;
    public float uiScale = 1f;
    public float textScale = 1f;
    public float maxCarryMultiplier = 1.5f;
    public bool highContrast;
    public bool reducedMotion;
    public bool developerMode;

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

        resolutionWidth = Mathf.Clamp(resolutionWidth, 960, 7680);
        resolutionHeight = Mathf.Clamp(resolutionHeight, 540, 4320);
        masterVolume = Mathf.Clamp01(masterVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        effectsVolume = Mathf.Clamp01(effectsVolume);
        uiVolume = Mathf.Clamp01(uiVolume);
        cameraSpeed = Mathf.Clamp(cameraSpeed, 0.5f, 2f);
        uiScale = Mathf.Clamp(uiScale, 0.8f, 1.25f);
        textScale = Mathf.Clamp(textScale, 0.9f, 1.25f);
        maxCarryMultiplier = Mathf.Clamp(Mathf.Round(maxCarryMultiplier / 0.05f) * 0.05f, 1f, 2.5f);
    }
}

public static class DungeonUserSettingsRuntime
{
    private static DungeonUserSettingsData current = new DungeonUserSettingsData();

    public static event Action Changed;

    public static DungeonUserSettingsData Current => current;

    public static void Publish(DungeonUserSettingsData value)
    {
        current = value?.Clone() ?? new DungeonUserSettingsData();
        current.Normalize();
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntime()
    {
        current = new DungeonUserSettingsData();
        Changed = null;
    }
}

public interface IDungeonUserSettingsService
{
    DungeonUserSettingsData Current { get; }
    string SettingsPath { get; }
    string LastError { get; }
    void Update(Action<DungeonUserSettingsData> change);
    void ResetDefaults();
    void ApplyCurrent();
}

public sealed class DungeonUserSettingsService :
    IDungeonUserSettingsService,
    IStartable,
    IDisposable
{
    private const string SettingsDirectoryName = "Settings";
    private const string SettingsFileName = "user-settings.json";

    private readonly DungeonUserSettingsRuntimeTargets runtimeTargets;
    private DungeonUserSettingsData current;

    public DungeonUserSettingsService(
        DungeonUserSettingsRuntimeTargets runtimeTargets)
    {
        this.runtimeTargets = runtimeTargets
            ?? throw new ArgumentNullException(nameof(runtimeTargets));
        SettingsPath = Path.Combine(
            Application.persistentDataPath,
            SettingsDirectoryName,
            SettingsFileName);
    }

    public DungeonUserSettingsData Current => current ??= new DungeonUserSettingsData();
    public string SettingsPath { get; }
    public string LastError { get; private set; } = string.Empty;

    public void Start()
    {
        current = Load();
        DungeonUserSettingsRuntime.Publish(current);
        ApplyCurrent();
    }

    public void Dispose()
    {
        Save();
    }

    public void Update(Action<DungeonUserSettingsData> change)
    {
        DungeonUserSettingsData next = Current.Clone();
        change?.Invoke(next);
        next.Normalize();
        current = next;
        DungeonUserSettingsRuntime.Publish(current);
        ApplyCurrent();
        Save();
    }

    public void ResetDefaults()
    {
        current = new DungeonUserSettingsData();
        DungeonUserSettingsRuntime.Publish(current);
        ApplyCurrent();
        Save();
    }

    public void ApplyCurrent()
    {
        Current.Normalize();
        AudioListener.volume = Current.masterVolume;

        CameraManager cameraManager = runtimeTargets.CameraManager;
        if (cameraManager != null)
        {
            cameraManager.ApplyUserPreferences(
                Current.cameraSpeed,
                Current.edgeScroll,
                Current.cameraControls);
        }

        foreach (DungeonUiThemeRuntime theme in runtimeTargets.Themes)
        {
            theme.ApplyNow();
        }

#if !UNITY_EDITOR
        FullScreenMode fullScreenMode = Current.windowMode switch
        {
            DungeonWindowMode.Windowed => FullScreenMode.Windowed,
            DungeonWindowMode.ExclusiveFullscreen => FullScreenMode.ExclusiveFullScreen,
            _ => FullScreenMode.FullScreenWindow
        };
        Screen.SetResolution(
            Current.resolutionWidth,
            Current.resolutionHeight,
            fullScreenMode);
#endif
    }

    private DungeonUserSettingsData Load()
    {
        LastError = string.Empty;
        if (!File.Exists(SettingsPath))
        {
            return new DungeonUserSettingsData();
        }

        try
        {
            DungeonUserSettingsData loaded = JsonUtility.FromJson<DungeonUserSettingsData>(
                File.ReadAllText(SettingsPath));
            loaded ??= new DungeonUserSettingsData();
            loaded.Normalize();
            return loaded;
        }
        catch (Exception exception)
        {
            LastError = "설정을 읽지 못해 기본값으로 복구했습니다: " + exception.Message;
            PreserveCorruptFile();
            return new DungeonUserSettingsData();
        }
    }

    private void Save()
    {
        LastError = string.Empty;
        string temporaryPath = SettingsPath + ".tmp";
        try
        {
            string directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporaryPath, JsonUtility.ToJson(Current, true));
            File.Copy(temporaryPath, SettingsPath, overwrite: true);
            File.Delete(temporaryPath);
        }
        catch (Exception exception)
        {
            LastError = "설정을 저장하지 못했습니다: " + exception.Message;
            TryDelete(temporaryPath);
        }
    }

    private void PreserveCorruptFile()
    {
        try
        {
            string suffix = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            string corruptPath = SettingsPath + ".corrupt-" + suffix;
            File.Copy(SettingsPath, corruptPath, overwrite: true);
        }
        catch
        {
            // The default settings still allow the game to start.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A stale temporary settings file is harmless and can be overwritten later.
        }
    }
}
