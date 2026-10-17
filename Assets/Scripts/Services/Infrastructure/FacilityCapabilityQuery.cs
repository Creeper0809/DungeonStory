using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;

public sealed class FacilityCapabilityQuery : IFacilityCapabilityQuery
{
    private readonly IBuildingWorldQuery world;

    public FacilityCapabilityQuery(IBuildingWorldQuery world) =>
        this.world = world ?? throw new ArgumentNullException(nameof(world));

    public IReadOnlyList<BuildableObject> FindOperational(
        FacilityCapabilityKind capability,
        string buildingDefinitionId = "")
    {
        string definitionId = buildingDefinitionId?.Trim() ?? string.Empty;
        bool requiresIsolationRecovery = capability
            == FacilityCapabilityKind.IsolationRecovery;
        FacilityRole role = requiresIsolationRecovery
            ? FacilityRole.Medical
            : ToRole(capability);
        return world.Buildings
            .Where(value => value != null
                && !value.IsBuildingDestroyed
                && value.BuildingData != null
                && (definitionId.Length == 0
                    || string.Equals(
                        BuildingDefinitionId(value.BuildingData),
                        definitionId,
                        StringComparison.Ordinal))
                && (role == FacilityRole.None || value.SupportsFacilityRole(role))
                && (!requiresIsolationRecovery
                    || value.BuildingData.Abilities
                        .OfType<ISurgicalFacilityAbility>()
                        .Any(ability =>
                            (ability.FacilityTags
                                & SurgeryFacilityTag.IsolationRecovery)
                            == SurgeryFacilityTag.IsolationRecovery))
                && IsOperational(value))
            .OrderBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<BuildableObject> FindOperational(
        ResearchFacilityCommandKind command)
    {
        if (command == ResearchFacilityCommandKind.None)
        {
            return Array.Empty<BuildableObject>();
        }

        return world.Buildings
            .Where(value => value != null
                && !value.IsBuildingDestroyed
                && value.BuildingData != null
                && value.BuildingData.ResearchFacilityCommand == command
                && IsOperational(value))
            .OrderBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsOperational(BuildableObject building)
    {
        if (building?.BuildingData == null)
        {
            return false;
        }

        if (!building.BuildingData.RequiresRoomRole())
        {
            return true;
        }

        BuildingRoomOperationalSnapshot profile =
            building.GetRoomOperationalProfile();
        return profile?.IsUsableRoom == true;
    }

    private static string BuildingDefinitionId(BuildingSO building) =>
        !string.IsNullOrWhiteSpace(building?.ContentDefinitionId)
            ? building.ContentDefinitionId
            : building == null
                ? string.Empty
                : $"building:{building.id}";

    private static FacilityRole ToRole(FacilityCapabilityKind capability) =>
        capability switch
        {
            FacilityCapabilityKind.Meal => FacilityRole.Meal,
            FacilityCapabilityKind.Purchase => FacilityRole.Purchase,
            FacilityCapabilityKind.Rest => FacilityRole.Rest,
            FacilityCapabilityKind.Training => FacilityRole.Training,
            FacilityCapabilityKind.Research => FacilityRole.Research,
            FacilityCapabilityKind.Mana => FacilityRole.Mana,
            FacilityCapabilityKind.Logistics => FacilityRole.Logistics,
            FacilityCapabilityKind.Toilet => FacilityRole.Toilet,
            FacilityCapabilityKind.Hygiene => FacilityRole.Hygiene,
            FacilityCapabilityKind.Administration => FacilityRole.Administration,
            FacilityCapabilityKind.Security => FacilityRole.Security,
            FacilityCapabilityKind.Entertainment => FacilityRole.Entertainment,
            FacilityCapabilityKind.Medical => FacilityRole.Medical,
            _ => FacilityRole.None
        };
}

