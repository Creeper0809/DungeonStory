using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum WasteDispositionKind
{
    Store = 0,
    DirectFeed = 1,
    Compost = 2,
    Fuel = 3,
    Alchemy = 4,
    Incinerate = 5
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WastePolicyData
{
    public WasteOriginKind origin;
    public WasteDispositionKind disposition;
    public bool enabled = true;
    [Range(0f, 100f)] public float maximumFeedContamination = 79f;

    public WastePolicyData Clone()
    {
        return (WastePolicyData)MemberwiseClone();
    }
}
