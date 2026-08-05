using System;
using TMPro;

public sealed class ResourceTmpKoreanFontProvider : ITmpKoreanFontProvider
{
    private readonly TmpKoreanFontSettingsSO settings;
    private TMP_FontAsset cachedFont;

    public ResourceTmpKoreanFontProvider(IGameContentCatalog content)
    {
        settings = (content ?? throw new ArgumentNullException(nameof(content)))
            .Media.KoreanFontSettings;
    }

    public TMP_FontAsset GetRequiredFont()
    {
        if (cachedFont != null)
        {
            return cachedFont;
        }

        cachedFont = settings.GetRequiredFont();
        return cachedFont;
    }
}
