using System;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(menuName = "DungeonStory/UI/TMP Korean Font Settings", order = 0)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
