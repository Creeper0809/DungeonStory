using System;
using TMPro;

public sealed class ResourceTmpKoreanFontProvider : ITmpKoreanFontProvider
{
    private const string SettingsResourcePath = "Config/TMPKoreanFontSettings";

    private readonly IResourcesAssetLoader resourcesAssetLoader;
    private TmpKoreanFontSettingsSO cachedSettings;
    private TMP_FontAsset cachedFont;

    public ResourceTmpKoreanFontProvider(IResourcesAssetLoader resourcesAssetLoader)
    {
        this.resourcesAssetLoader = resourcesAssetLoader
            ?? throw new ArgumentNullException(nameof(resourcesAssetLoader));
    }

    public TMP_FontAsset GetRequiredFont()
    {
        if (cachedFont != null)
        {
            return cachedFont;
        }

        if (cachedSettings == null)
        {
            cachedSettings = resourcesAssetLoader.LoadRequired<TmpKoreanFontSettingsSO>(SettingsResourcePath);
        }

        cachedFont = cachedSettings.GetRequiredFont();
        return cachedFont;
    }
}
