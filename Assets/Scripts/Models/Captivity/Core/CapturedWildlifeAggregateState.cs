using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class CapturedWildlifeAggregateState
    : IWildlifeGameplayOutcomeOwner
{
    internal readonly Dictionary<string, CapturedWildlifeState> Captured =
        new(StringComparer.Ordinal);

    public List<WildlifeGameplayOutcomeSaveData> GameplayOutcomes { get; } =
        new();

    public int NextGameplayOutcomeSequence { get; set; }
}
