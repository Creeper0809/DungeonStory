using System;
using System.Collections.Generic;
using VContainer.Unity;

public sealed class WildlifeHabitatMarkerRegistry :
    IWildlifeHabitatMarkerQuery,
    IWildlifeHabitatMarkerRegistry,
    IInitializable,
    IDisposable
{
    private readonly WorldSimulationSceneReferences sceneReferences;
    private readonly List<WildlifeHabitatMarker> markers =
        new List<WildlifeHabitatMarker>();
    private IReadOnlyList<WildlifeHabitatMarker> markerView;
    private bool initialized;

    public WildlifeHabitatMarkerRegistry(
        WorldSimulationSceneReferences sceneReferences)
    {
        this.sceneReferences = sceneReferences
            ?? throw new ArgumentNullException(nameof(sceneReferences));
    }

    public int Version { get; private set; }

    public void Initialize()
    {
        EnsureInitialized();
    }

    public void Dispose()
    {
        markers.Clear();
        markerView = null;
        initialized = false;
        IncrementVersion();
    }

    public IReadOnlyList<WildlifeHabitatMarker> GetMarkers()
    {
        EnsureInitialized();
        PruneDestroyedMarkers();
        return markerView ??= ReadOnlyView.List(markers);
    }

    public bool Register(WildlifeHabitatMarker marker)
    {
        EnsureInitialized();
        if (marker == null || markers.Contains(marker))
        {
            return false;
        }

        markers.Add(marker);
        IncrementVersion();
        return true;
    }

    public bool Unregister(WildlifeHabitatMarker marker)
    {
        if (marker == null || !markers.Remove(marker))
        {
            return false;
        }

        IncrementVersion();
        return true;
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        IReadOnlyList<WildlifeHabitatMarker> sceneMarkers =
            sceneReferences.WildlifeHabitats;
        for (int index = 0; index < sceneMarkers.Count; index++)
        {
            WildlifeHabitatMarker marker = sceneMarkers[index];
            if (marker != null && !markers.Contains(marker))
            {
                markers.Add(marker);
            }
        }

        IncrementVersion();
    }

    private void PruneDestroyedMarkers()
    {
        bool changed = false;
        for (int index = markers.Count - 1; index >= 0; index--)
        {
            if (markers[index] != null)
            {
                continue;
            }

            markers.RemoveAt(index);
            changed = true;
        }

        if (changed)
        {
            IncrementVersion();
        }
    }

    private void IncrementVersion()
    {
        unchecked
        {
            Version++;
        }
    }
}
