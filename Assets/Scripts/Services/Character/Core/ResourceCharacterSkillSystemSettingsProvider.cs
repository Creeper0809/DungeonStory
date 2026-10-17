using System;

public sealed class ResourceCharacterSkillSystemSettingsProvider : ICharacterSkillSystemSettingsProvider
{
    private readonly IGameContentCatalog contentCatalog;

    public ResourceCharacterSkillSystemSettingsProvider(IGameContentCatalog contentCatalog)
    {
        this.contentCatalog = contentCatalog
            ?? throw new ArgumentNullException(nameof(contentCatalog));
    }

    public CharacterSkillSystemSettingsSO Settings =>
        contentCatalog.CharacterSkillSettings
        ?? throw new InvalidOperationException(
            "Game content catalog has no character-skill settings.");
}
