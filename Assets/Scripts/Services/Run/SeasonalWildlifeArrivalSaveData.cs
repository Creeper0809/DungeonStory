using System;
using System.Collections.Generic;


[Serializable]
public sealed class SeasonalWildlifeArrivalMemberSaveData
{
    public string wildlifeId = string.Empty;
    public int positionX;
    public int positionY;
    public bool spawned;
}

[Serializable]
public sealed class SeasonalWildlifeArrivalSaveData
{
    public bool configured;
    public string speciesId = string.Empty;
    public int exactCount;
    public string requiredHabitatId = string.Empty;
    public SeasonalWildlifeArrivalQualification qualification;
    public List<SeasonalWildlifeArrivalMemberSaveData> members = new();
    public string lastFailureReason = string.Empty;
    public string outcomeResultKey = string.Empty;
    public SeasonalWildlifeArrivalFactData observation = new();
    public SeasonalTerminalPublicationSaveData publication = new();

    public static SeasonalWildlifeArrivalSaveData FromProfile(
        SeasonalWildlifeArrivalProfile profile)
    {
        if (profile?.IsConfigured != true)
        {
            return new SeasonalWildlifeArrivalSaveData();
        }

        return new SeasonalWildlifeArrivalSaveData
        {
            configured = true,
            speciesId = profile.speciesId,
            exactCount = profile.exactCount,
            requiredHabitatId = profile.requiredHabitatId,
            qualification = profile.qualification
        };
    }
}
