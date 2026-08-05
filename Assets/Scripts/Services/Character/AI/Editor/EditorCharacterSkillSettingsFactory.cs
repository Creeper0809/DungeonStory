#if UNITY_EDITOR
using UnityEngine;

public static class EditorCharacterSkillSettingsFactory
{
    public static CharacterSkillSystemSettingsSO CreateTransientDefaults()
    {
        CharacterSkillSystemSettingsSO settings =
            ScriptableObject.CreateInstance<CharacterSkillSystemSettingsSO>();
        settings.hideFlags = HideFlags.HideAndDontSave;
        settings.EnsureDefaults();
        return settings;
    }
}
#endif
