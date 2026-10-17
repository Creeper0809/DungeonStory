using System;

public enum DungeonSpaceExpansionTrigger
{
    NewRunInitialization = 1,
    ResearchCompletion = 2
}

/// <summary>Frozen spatial transition; identity is opaque outside its observer.</summary>
[Serializable]
public sealed class DungeonSpaceExpansionPublicationData
{
    public bool active;
    public string publicationKey = string.Empty;
    public string projectId = string.Empty;
    public string projectName = string.Empty;
    public int tier;
    public int previousColumns;
    public int currentColumns;
    public int previousWidth;
    public int currentWidth;
    public int absoluteDay;
    public int entranceX;
    public int entranceY;
    public DungeonSpaceExpansionTrigger trigger;
    public string parentResearchPublicationKey = string.Empty;

    public DungeonSpaceExpansionPublicationData Clone() =>
        (DungeonSpaceExpansionPublicationData)MemberwiseClone();
}
