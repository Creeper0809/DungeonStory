using System;
using System.Collections.Generic;

public sealed class WorldSimulationSceneReferences
{
    public WorldSimulationSceneReferences(
        IReadOnlyList<WildlifeHabitatMarker> wildlifeHabitats = null,
        IReadOnlyList<ExteriorZoneMarker> exteriorZones = null)
    {
        WildlifeHabitats = wildlifeHabitats
            ?? Array.Empty<WildlifeHabitatMarker>();
        ExteriorZones = exteriorZones
            ?? Array.Empty<ExteriorZoneMarker>();
    }

    public IReadOnlyList<WildlifeHabitatMarker> WildlifeHabitats { get; }
    public IReadOnlyList<ExteriorZoneMarker> ExteriorZones { get; }
}
