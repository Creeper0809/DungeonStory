using System;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/UI/TMP Korean Font Settings", order = 0)]
public sealed class TmpKoreanFontSettingsSO : ScriptableObject
{
    [SerializeField] private TMP_FontAsset font;

    public TMP_FontAsset Font => font;

    public TMP_FontAsset GetRequiredFont()
    {
        return font != null
            ? font
            : throw new InvalidOperationException($"{nameof(TmpKoreanFontSettingsSO)} requires a TMP font reference.");
    }
}
