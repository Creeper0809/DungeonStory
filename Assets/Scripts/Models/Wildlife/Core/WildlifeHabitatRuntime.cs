using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IWildlifeHabitatMarkerQuery
{
    int Version { get; }
    IReadOnlyList<WildlifeHabitatMarker> GetMarkers();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IWildlifeHabitatMarkerRegistry : IWildlifeHabitatMarkerQuery
{
    bool Register(WildlifeHabitatMarker marker);
    bool Unregister(WildlifeHabitatMarker marker);
}

[DisallowMultipleComponent]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WildlifeHabitatMarker : MonoBehaviour
{
    [SerializeField] private WildlifeHabitatType habitatType = WildlifeHabitatType.Grass;
    [SerializeField, Min(0)] private int radius = 4;
    [SerializeField, Min(0.1f)] private float resourceCapacity = 8f;
    [SerializeField, Min(0f)] private float regenPerSecond = 0.025f;
    [SerializeField, Range(0f, 1f)] private float danger;
    [SerializeField] private List<string> preferredSpeciesTags = new List<string>();

    public WildlifeHabitatType HabitatType => habitatType;

    public WildlifeHabitatPatch ToPatch(
        IWildlifeGridPort grid,
        WildlifeHabitatPatchId patchId)
    {
        if (!patchId.IsValid)
        {
            throw new ArgumentException(
                "A persistent wildlife habitat patch id is required.",
                nameof(patchId));
        }
        Vector2Int center = grid != null
            ? grid.GetCellPosition(transform.position)
            : Vector2Int.zero;
        float capacity = resourceCapacity;
        float regen = regenPerSecond;
        if (habitatType is WildlifeHabitatType.Burrow or WildlifeHabitatType.Brush or WildlifeHabitatType.Lair)
        {
            capacity = Mathf.Max(1f, capacity);
            regen = Mathf.Max(0.005f, regen);
        }

        return new WildlifeHabitatPatch(
            patchId.Value,
            habitatType,
            center,
            radius,
            capacity,
            capacity,
            regen,
            danger,
            preferredSpeciesTags);
    }
}
