using Sirenix.OdinInspector;
using UnityEngine.Scripting.APIUpdating;

[DrawWithUnity]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class DataScriptableObject : SerializedScriptableObject
{
    [ContentIdentity]
    public int id;
}
