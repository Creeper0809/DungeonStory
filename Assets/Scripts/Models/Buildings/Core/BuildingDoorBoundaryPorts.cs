using UnityEngine;

public readonly struct BuildingDoorTraversalSubjects
{
    public BuildingDoorTraversalSubjects(object first, object second)
    {
        First = first;
        Second = second;
    }

    public object First { get; }
    public object Second { get; }
}

public interface IBuildingDoorTraversalSubjectPort
{
    BuildingDoorTraversalSubjects ResolveTraversalSubjects(Collider2D collision);
    bool IsTraversalSubjectAvailable(object subject);
    void ChangeTraversalSortingLayer(object subject, string layerName);
}

public enum BuildingDoorAccessSubjectKind
{
    None = 0,
    Owner = 1,
    Staff = 2,
    Customer = 3,
    Intruder = 4,
    Wildlife = 5
}

public readonly struct BuildingDoorAccessSubjectSnapshot
{
    public BuildingDoorAccessSubjectSnapshot(
        string persistentId,
        string displayName,
        BuildingDoorAccessSubjectKind kind,
        Object runtimeSubject)
    {
        PersistentId = persistentId?.Trim() ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Kind = kind;
        RuntimeSubject = runtimeSubject;
    }

    public string PersistentId { get; }
    public string DisplayName { get; }
    public BuildingDoorAccessSubjectKind Kind { get; }
    public Object RuntimeSubject { get; }
    public bool IsValid => Kind != BuildingDoorAccessSubjectKind.None;
}

public interface IBuildingDoorAccessSubjectPort
{
    bool TryResolveDoorAccessSubject(
        Object subject,
        out BuildingDoorAccessSubjectSnapshot snapshot);
}

public interface IBuildingDoorPolicyInvalidationPort
{
    void InvalidateDoorPolicyPaths();
}
