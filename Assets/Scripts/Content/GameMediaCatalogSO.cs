using System;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    fileName = "GameMediaCatalog",
    menuName = "DungeonStory/Content/Game Media Catalog",
    order = -98)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class GameMediaCatalogSO : ScriptableObject
{
    [SerializeField] private DungeonAudioLibrarySO audioLibrary;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private TmpKoreanFontSettingsSO koreanFontSettings;
    [SerializeField] private Sprite titleIcon;
    [SerializeField] private Material doorSpriteMaterial;

    public DungeonAudioLibrarySO AudioLibrary => audioLibrary;
    public AudioMixer AudioMixer => audioMixer;
    public TmpKoreanFontSettingsSO KoreanFontSettings => koreanFontSettings;
    public Sprite TitleIcon => titleIcon;
    public Material DoorSpriteMaterial => doorSpriteMaterial;

    public void ValidateRequiredReferences()
    {
        Require(audioLibrary, nameof(audioLibrary));
        Require(koreanFontSettings, nameof(koreanFontSettings));
        Require(titleIcon, nameof(titleIcon));
        Require(doorSpriteMaterial, nameof(doorSpriteMaterial));
    }

    private static void Require(UnityEngine.Object value, string field)
    {
        if (value == null)
        {
            throw new InvalidOperationException(
                $"Game media catalog is missing required reference '{field}'.");
        }
    }

#if UNITY_EDITOR
    public void Configure(
        DungeonAudioLibrarySO library,
        AudioMixer mixer,
        TmpKoreanFontSettingsSO fontSettings,
        Sprite icon,
        Material doorMaterial)
    {
        audioLibrary = library;
        audioMixer = mixer;
        koreanFontSettings = fontSettings;
        titleIcon = icon;
        doorSpriteMaterial = doorMaterial;
    }
#endif
}
