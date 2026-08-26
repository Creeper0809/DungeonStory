using UnityEditor;
[InitializeOnLoad]
public static class PlayRefreshEditor
{
    static PlayRefreshEditor()
    {
        // Unity already refreshes/imports changed assets before entering
        // PlayMode. A synchronous AssetDatabase.Refresh from the
        // ExitingEditMode callback can deadlock the state transition while an
        // import or domain reload is completing, so no editor callback is
        // registered here.
    }
}
