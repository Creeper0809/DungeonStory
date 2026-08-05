using System;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public enum CharacterCombatUiTextId
{
    FireModeAimed = 0,
    FireModeRapid = 1,
    FireModeSuppressive = 2,
    RescueTargetRecovered = 3
}

public interface ICharacterCombatUiTextQuery
{
    string Get(CharacterCombatUiTextId textId);
    string GetFireModeName(CombatFireMode mode);
}

public sealed class CharacterCombatUiTextLocalizer :
    ICharacterCombatUiTextQuery
{
    public const string TableName = "CharacterCombatUI";

    public string Get(CharacterCombatUiTextId textId)
    {
        string key = textId switch
        {
            CharacterCombatUiTextId.FireModeAimed =>
                "CharacterCombat.FireMode.Aimed",
            CharacterCombatUiTextId.FireModeRapid =>
                "CharacterCombat.FireMode.Rapid",
            CharacterCombatUiTextId.FireModeSuppressive =>
                "CharacterCombat.FireMode.Suppressive",
            CharacterCombatUiTextId.RescueTargetRecovered =>
                "CharacterCombat.Command.RescueTargetRecovered",
            _ => throw new ArgumentOutOfRangeException(
                nameof(textId),
                textId,
                null)
        };

        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string localized = new LocalizedString(TableName, key).GetLocalizedString();
        if (string.IsNullOrWhiteSpace(localized))
        {
            throw new InvalidOperationException(
                $"Missing localized character-combat UI entry '{key}' "
                + $"in String Table '{TableName}'.");
        }
        return localized;
    }

    public string GetFireModeName(CombatFireMode mode) => Get(mode switch
    {
        CombatFireMode.Rapid => CharacterCombatUiTextId.FireModeRapid,
        CombatFireMode.Suppressive =>
            CharacterCombatUiTextId.FireModeSuppressive,
        _ => CharacterCombatUiTextId.FireModeAimed
    });
}
