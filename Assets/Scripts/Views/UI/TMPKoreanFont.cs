using System;
using TMPro;
using UnityEngine;

public interface ITmpKoreanFontProvider
{
    TMP_FontAsset GetRequiredFont();
}

public interface ITmpKoreanFontService
{
    TMP_FontAsset Resolve();
    void Apply(TMP_Text text);
    void ApplyToChildren(Transform root, bool includeInactive = true);
}

public sealed class TmpKoreanFontAssetProvider : ITmpKoreanFontProvider
{
    private readonly TMP_FontAsset font;

    public TmpKoreanFontAssetProvider(TMP_FontAsset font)
    {
        this.font = font
            ?? throw new ArgumentNullException(nameof(font));
    }

    public TMP_FontAsset GetRequiredFont()
    {
        return font;
    }
}

public sealed class TmpKoreanFontService : ITmpKoreanFontService
{
    private readonly ITmpKoreanFontProvider fontProvider;

    public TmpKoreanFontService(ITmpKoreanFontProvider fontProvider)
    {
        this.fontProvider = fontProvider
            ?? throw new ArgumentNullException(nameof(fontProvider));
    }

    public TMP_FontAsset Resolve()
    {
        return fontProvider.GetRequiredFont();
    }

    public void Apply(TMP_Text text)
    {
        if (text == null) return;

        TMP_FontAsset font = Resolve();
        if (font != null)
        {
            text.font = font;
        }
    }

    public void ApplyToChildren(Transform root, bool includeInactive = true)
    {
        if (root == null) return;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(includeInactive))
        {
            Apply(text);
        }
    }
}
