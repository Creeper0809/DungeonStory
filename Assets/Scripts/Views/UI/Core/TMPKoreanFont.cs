using System;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ITmpKoreanFontProvider
{
    TMP_FontAsset GetRequiredFont();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ITmpKoreanFontService
{
    TMP_FontAsset Resolve();
    void Apply(TMP_Text text);
    void ApplyToChildren(Transform root, bool includeInactive = true);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
