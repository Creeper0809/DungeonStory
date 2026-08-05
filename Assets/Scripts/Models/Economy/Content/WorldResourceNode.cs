using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[DisallowMultipleComponent]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorldResourceNode : MonoBehaviour
{
    private IWorldResourceRuntime runtime;

    public string NodeId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = "외부 자원";
    public bool IsConfigured => runtime != null
        && ((BuildingInstanceId)NodeId).IsValid;

    public void Configure(
        IWorldResourceRuntime resourceRuntime,
        BuildingInstanceId nodeId,
        string displayName)
    {
        runtime = resourceRuntime
            ?? throw new ArgumentNullException(nameof(resourceRuntime));
        if (!nodeId.IsValid)
        {
            throw new InvalidOperationException(
                "World-resource node requires its canonical BuildingInstanceId.");
        }

        NodeId = nodeId.Value;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? "외부 자원"
            : displayName.Trim();
        gameObject.name = "WorldResource_" + NodeId;
    }

    public float GetLegacyWorkUrgency(WorkTypeId workTypeId)
    {
        if (runtime == null
            || !runtime.TryGetWork(
                this,
                workTypeId,
                out WorldResourceWorkSnapshot snapshot)
            || !snapshot.Available)
        {
            return 0f;
        }

        float finiteBonus =
            snapshot.WorkTypeId == BuiltInWorkTypeIds.Logging
            || snapshot.WorkTypeId == BuiltInWorkTypeIds.Quarry
                ? 12f
                : 0f;
        return 34f + finiteBonus + snapshot.ResourceRatio * 24f;
    }
}
