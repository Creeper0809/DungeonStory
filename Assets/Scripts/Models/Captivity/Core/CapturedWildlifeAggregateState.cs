using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class CapturedWildlifeAggregateState
{
    internal readonly Dictionary<string, CapturedWildlifeState> Captured =
        new(StringComparer.Ordinal);
}
