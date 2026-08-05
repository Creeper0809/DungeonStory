using System;
using System.IO;
using UnityEngine;
using VContainer.Unity;

public sealed class DungeonUserSettingsService :
    IDungeonUserSettingsService,
    IBuildingPresentationSettingsPort,
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
    bool IBuildingPresentationSettingsPort.ReducedMotion => Current.reducedMotion;
    public event Action Changed;
    public string SettingsPath { get; }
    public string LastError { get; private set; } = string.Empty;

    public void Start()
    {
        current = Load();
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
        Changed?.Invoke();
        ApplyCurrent();
        Save();
    }

    public void ResetDefaults()
    {
        current = new DungeonUserSettingsData();
        Changed?.Invoke();
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
        if (!IsAutomationLaunch())
        {
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
        }
#endif
    }

    private static bool IsAutomationLaunch()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length; index++)
        {
            if (string.Equals(
                    arguments[index],
                    "-automation",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
