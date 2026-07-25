using UnityEditor;

[CustomEditor(typeof(GridSystemManager))]
[CanEditMultipleObjects]
public class GridSystemManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
