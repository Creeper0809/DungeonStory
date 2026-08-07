using System;
using System.Collections.Generic;
using UnityEngine;

public enum CareerPositionScopeKind
{
    Global = 0,
    Facility = 1
}

[CreateAssetMenu(
    fileName = "CareerPosition",
    menuName = "DungeonStory/Population/Career Position")]
public sealed class CareerPositionDefinitionSO : ScriptableObject
{
    public string definitionId = string.Empty;
    public string displayName = string.Empty;
    public CareerPositionKind position;
    public CareerPositionScopeKind scope;
    [Min(1)] public int maximumOccupants = 1;
    public string requiredFacilityTag = string.Empty;

    public string StableId => definitionId?.Trim() ?? string.Empty;
}

public interface ICareerPositionDefinitionCatalog
{
    IReadOnlyList<CareerPositionDefinitionSO> All { get; }
    CareerPositionDefinitionSO Require(CareerPositionKind position);
}
