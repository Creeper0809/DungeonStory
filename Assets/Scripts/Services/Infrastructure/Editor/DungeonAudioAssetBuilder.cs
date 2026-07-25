using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DungeonAudioAssetBuilder
{
    private const string LibraryPath = "Assets/Resources/Audio/DungeonAudioLibrary.asset";
    private const string AudioRoot = "Assets/Resources/Audio/Kenney/";

    private static readonly IReadOnlyDictionary<DungeonAudioCue, string> ClipPaths =
        new Dictionary<DungeonAudioCue, string>
        {
            [DungeonAudioCue.UiClick] = AudioRoot + "UiClick.ogg",
            [DungeonAudioCue.Confirm] = AudioRoot + "Confirm.ogg",
            [DungeonAudioCue.Warning] = AudioRoot + "Warning.ogg",
            [DungeonAudioCue.Impact] = AudioRoot + "Impact.ogg",
            [DungeonAudioCue.Victory] = AudioRoot + "Victory.ogg",
            [DungeonAudioCue.Defeat] = AudioRoot + "Defeat.ogg"
        };

    [MenuItem("DungeonStory/Content/Build Audio Library")]
    public static void BuildFromMenu()
    {
        if (!EnsureLibrary(log: true))
        {
            throw new InvalidOperationException("DungeonStory audio library could not be built.");
        }
    }

    public static bool EnsureLibrary(bool log = false)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Dictionary<DungeonAudioCue, AudioClip> clips = new Dictionary<DungeonAudioCue, AudioClip>();
        foreach (KeyValuePair<DungeonAudioCue, string> pair in ClipPaths)
        {
            AudioClip clip = ImportClip(pair.Value);
            if (clip == null)
            {
                Debug.LogError($"Missing authored audio clip for {pair.Key}: {pair.Value}");
                return false;
            }

            clips.Add(pair.Key, clip);
        }

        DungeonAudioLibrarySO library = AssetDatabase.LoadAssetAtPath<DungeonAudioLibrarySO>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<DungeonAudioLibrarySO>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }

        library.uiClick = clips[DungeonAudioCue.UiClick];
        library.confirm = clips[DungeonAudioCue.Confirm];
        library.warning = clips[DungeonAudioCue.Warning];
        library.impact = clips[DungeonAudioCue.Impact];
        library.victory = clips[DungeonAudioCue.Victory];
        library.defeat = clips[DungeonAudioCue.Defeat];
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        bool valid = library.uiClick != null
            && library.confirm != null
            && library.warning != null
            && library.impact != null
            && library.victory != null
            && library.defeat != null;
        if (log)
        {
            Debug.Log(valid
                ? "DungeonStory authored audio library is ready with 6 CC0 cues."
                : "DungeonStory authored audio library is incomplete.");
        }

        return valid;
    }

    private static AudioClip ImportClip(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(path) is AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.72f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
