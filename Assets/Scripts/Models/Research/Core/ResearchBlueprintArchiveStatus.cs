using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ResearchBlueprintArchiveStatus
{
    public ResearchBlueprintArchiveStatus(
        bool archived,
        bool inTransit,
        string location,
        string blocker)
    {
        IsArchived = archived;
        IsInTransit = inTransit;
        Location = location ?? string.Empty;
        Blocker = blocker ?? string.Empty;
    }

    public bool IsArchived { get; }
    public bool IsInTransit { get; }
    public string Location { get; }
    public string Blocker { get; }
}
