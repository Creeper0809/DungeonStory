using TMPro;
using UnityEditor;

public static class TMPKoreanFontEditorResolver
{
    private const string EditorFontPath = "Assets/Font/Maplestory Light SDF.asset";

    public static ITmpKoreanFontService CreateService()
    {
        return new TmpKoreanFontService(
            new TmpKoreanFontAssetProvider(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(EditorFontPath)));
    }
}
