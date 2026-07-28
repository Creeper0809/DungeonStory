using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldResourceNode : Facility
{
    private IWorldResourceRuntime runtime;

    public string NodeId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = "외부 자원";

    public void Configure(
        IWorldResourceRuntime resourceRuntime,
        string nodeId,
        string displayName)
    {
        runtime = resourceRuntime;
        NodeId = nodeId ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? "외부 자원"
            : displayName.Trim();
        gameObject.name = "WorldResource_" + NodeId;
    }

    public override bool isVisitable()
    {
        return runtime != null && !IsGridDestroyed;
    }

    internal override float GetLegacyWorkUrgency(FacilityWorkType workType)
    {
        if (runtime == null
            || !WorkTypeCatalog.TryGet(workType, out WorkTypeDefinition definition)
            || !runtime.TryGetWork(this, definition.WorkTypeId, out WorldResourceWorkSnapshot snapshot)
            || !snapshot.Available)
        {
            return 0f;
        }

        float finiteBonus = snapshot.WorkTypeId == BuiltInWorkTypeIds.Logging
            || snapshot.WorkTypeId == BuiltInWorkTypeIds.Quarry
                ? 12f
                : 0f;
        return 34f + finiteBonus + snapshot.ResourceRatio * 24f;
    }
}
