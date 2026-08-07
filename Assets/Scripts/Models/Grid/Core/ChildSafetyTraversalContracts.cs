using System;
using UnityEngine;

[Flags]
public enum WorldHazardFlags
{
    None = 0,
    Combat = 1 << 0,
    Fire = 1 << 1,
    ToxicAir = 1 << 2,
    LethalTemperature = 1 << 3,
    SevereContamination = 1 << 4,
    Industrial = 1 << 5,
    UncomfortableTemperature = 1 << 6
}

public enum WorldHazardLevel
{
    Safe = 0,
    Restricted = 1,
    Forbidden = 2
}

public readonly struct WorldHazardSnapshot
{
    public WorldHazardSnapshot(
        Vector2Int position,
        WorldHazardLevel level,
        WorldHazardFlags flags)
    {
        Position = position;
        Level = level;
        Flags = flags;
    }

    public Vector2Int Position { get; }
    public WorldHazardLevel Level { get; }
    public WorldHazardFlags Flags { get; }
}

public interface IWorldHazardZoneQuery
{
    int Version { get; }
    WorldHazardSnapshot GetHazard(CharacterId characterId, Vector2Int position);
}

public interface IWorldHazardOverlayCommand
{
    void ReplaceOverlay(
        string sourceId,
        WorldHazardFlags flags,
        System.Collections.Generic.IReadOnlyCollection<Vector2Int> cells);
    void RemoveOverlay(string sourceId);
}

public interface IChildSafetyPolicy
{
    int Version { get; }
    bool SupervisedApprenticeshipEnabled { get; }
    bool IsCharacterApprenticeshipPermitted(CharacterId characterId);

    void SetSupervisedApprenticeship(bool enabled);
    void SetCharacterApprenticeshipPermission(CharacterId characterId, bool allowed);
    bool TryAuthorizeApprenticeship(
        CharacterId characterId,
        string workOrderId,
        Vector2Int workCell,
        CharacterId supervisorId,
        bool workExplicitlyConfirmed,
        bool hasRequiredProtectiveEquipment,
        out ChildSafetyAuthorizationToken token,
        out DomainFailure failure);
    void RevokeApprenticeship(CharacterId characterId, string workOrderId);
    bool CanTraverse(
        GridTraversalContext context,
        in WorldHazardSnapshot from,
        in WorldHazardSnapshot to,
        out DomainFailure failure);
}
