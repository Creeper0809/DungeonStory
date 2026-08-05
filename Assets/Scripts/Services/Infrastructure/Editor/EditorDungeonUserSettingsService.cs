#if UNITY_EDITOR
using System;

internal sealed class EditorDungeonUserSettingsService :
    IDungeonUserSettingsService
{
    public DungeonUserSettingsData Current { get; } =
        new DungeonUserSettingsData();
    public string SettingsPath => string.Empty;
    public string LastError => string.Empty;
    public event Action Changed;

    public void Update(Action<DungeonUserSettingsData> change)
    {
        change?.Invoke(Current);
        Current.Normalize();
        Changed?.Invoke();
    }

    public void ResetDefaults()
    {
        Update(_ => { });
    }

    public void ApplyCurrent()
    {
        Changed?.Invoke();
    }
}
#endif
