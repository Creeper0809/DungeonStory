using UnityEngine;

[CreateAssetMenu(fileName = "DungeonAudioLibrary", menuName = "DungeonStory/Audio Library")]
public sealed class DungeonAudioLibrarySO : ScriptableObject
{
    public AudioClip musicLoop;
    public AudioClip ambienceLoop;
    public AudioClip uiClick;
    public AudioClip confirm;
    public AudioClip warning;
    public AudioClip impact;
    public AudioClip victory;
    public AudioClip defeat;

    public AudioClip GetCue(DungeonAudioCue cue)
    {
        return cue switch
        {
            DungeonAudioCue.UiClick => uiClick,
            DungeonAudioCue.Confirm => confirm,
            DungeonAudioCue.Warning => warning,
            DungeonAudioCue.Impact => impact,
            DungeonAudioCue.Victory => victory,
            DungeonAudioCue.Defeat => defeat,
            _ => null
        };
    }
}
