using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;
using static CropPlotInputRequirementsQuery;

internal sealed class CropPlotPhysicalInputRuntime
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionItemGateway items;
    private readonly ICropPlotInputOwnerRuntime inputOwners;
    private readonly CropPlotInputRequirementsQuery inputRequirementsQuery;

    internal CropPlotPhysicalInputRuntime(
        CropPlotWorldDependencies world,
        CropPlotInputRequirementsQuery inputRequirementsQuery)
    {
        catalog = world.Catalog;
        items = world.Items;
        inputOwners = world.InputOwners;
        this.inputRequirementsQuery = inputRequirementsQuery;
    }
    internal IReadOnlyList<CropPlotInputOwnerDescriptor>
        BuildInputOwnerDescriptors(
            CropPlotRestoreCandidate candidate,
            IReadOnlyList<BuildableObject> detachedBuildings)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        BuildableObject[] buildings = (detachedBuildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null && !value.IsBuildingDestroyed)
            .OrderBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
        List<CropPlotInputOwnerDescriptor> descriptors = new();
        foreach (CropPlotState state in candidate.State.States.Values
                     .Where(RequiresAnyInputAuthority)
                     .OrderBy(value => value.PlotId.Value,
                         StringComparer.Ordinal))
        {
            BuildableObject[] matches = buildings.Where(value =>
                    value.PersistentInstanceId.Equals(state.PlotId))
                .ToArray();
            BuildingCropPlotAbility ability = matches.Length == 1
                ? matches[0].BuildingData?
                    .GetAbility<BuildingCropPlotAbility>()
                : null;
            if (matches.Length != 1 || ability == null)
            {
                throw new InvalidOperationException(
                    "Crop-plot input restore requires one exact live facility: "
                    + state.PlotId.Value);
            }
            descriptors.AddRange(BuildInputOwnerDescriptors(
                state,
                matches[0],
                ability));
        }
        return descriptors
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();
    }

    internal IReadOnlyList<CropPlotInputOwnerDescriptor>
        BuildInputOwnerDescriptors(
            CropPlotState state,
            BuildableObject building,
            BuildingCropPlotAbility ability)
    {
        if (state == null
            || building == null
            || building.IsBuildingDestroyed
            || ability == null
            || !building.PersistentInstanceId.Equals(state.PlotId)
            || building.centerPos != state.LastKnownPosition
                && state.Building == null)
        {
            throw new InvalidOperationException(
                "Crop-plot input owner source is not attached to its exact live facility.");
        }

        List<CropPlotInputOwnerDescriptor> descriptors = new();
        if (RequiresSowInputAuthority(state))
        {
            if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop)
                || crop == null)
                throw new InvalidOperationException(
                    "Crop-plot input owner references an unknown crop: "
                    + state.CropId);
            IReadOnlyDictionary<string, int> requirements =
                inputRequirementsQuery.BuildMaterialRequirements(state, crop, ability);
            descriptors.Add(CreateSowInputOwnerDescriptor(
                state,
                requirements,
                building.centerPos));
        }
        if (state.Treatment?.phase != CropTreatmentOrderPhase.None)
            descriptors.Add(CreateTreatmentInputOwnerDescriptor(
                state,
                state.Treatment,
                building.centerPos));
        if (state.WaterRefill?.phase != CropWaterRefillPhase.None)
            descriptors.Add(CreateWaterRefillInputOwnerDescriptor(
                state,
                state.WaterRefill,
                building.centerPos));
        return descriptors;
    }

    internal CropPlotInputOwnerDescriptor CreateSowInputOwnerDescriptor(
        CropPlotState state,
        IReadOnlyDictionary<string, int> requirements,
        Vector2Int? position = null) => new(
        state.PlotId.Value,
        position ?? state.Building?.centerPos ?? state.LastKnownPosition,
        state.MaterialDestinationId,
        CropPhysicalTransactionOutbox.FormatSowOperationId(
            state.PlotId.Value,
            state.NextSowOperationSequence),
        requirements);

    internal static CropPlotInputOwnerDescriptor
        CreateTreatmentInputOwnerDescriptor(
            CropPlotState state,
            CropTreatmentOrderSaveData treatment,
            Vector2Int? position = null)
    {
        if (state == null || treatment == null
            || treatment.phase == CropTreatmentOrderPhase.None)
            throw new ArgumentException(
                "A live crop-treatment input owner is required.");
        return new CropPlotInputOwnerDescriptor(
            state.PlotId.Value,
            position ?? state.Building?.centerPos ?? state.LastKnownPosition,
            treatment.destinationId,
            treatment.operationId,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [treatment.itemId] = treatment.quantity
            });
    }

    internal static CropPlotInputOwnerDescriptor
        CreateWaterRefillInputOwnerDescriptor(
            CropPlotState state,
            CropWaterRefillSaveData refill,
            Vector2Int? position = null)
    {
        if (state == null || refill == null
            || refill.phase == CropWaterRefillPhase.None)
            throw new ArgumentException(
                "A live crop water-refill input owner is required.");
        return new CropPlotInputOwnerDescriptor(
            state.PlotId.Value,
            position ?? state.Building?.centerPos ?? state.LastKnownPosition,
            refill.destinationId,
            refill.operationId,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [refill.itemId] = refill.quantity
            });
    }

    internal static bool RequiresAnyInputAuthority(CropPlotState state) =>
        RequiresSowInputAuthority(state)
        || state?.Treatment?.phase != CropTreatmentOrderPhase.None
        || state?.WaterRefill?.phase != CropWaterRefillPhase.None;

    internal bool TryRetireDestination(
        CropPlotState state,
        string destinationId,
        string reasonCode,
        out string failureReason)
    {
        Vector2Int position = state?.Building != null
            ? state.Building.centerPos
            : state?.LastKnownPosition ?? default;
        return inputOwners.TryRetireDestination(
            destinationId,
            position,
            reasonCode,
            out failureReason);
    }

    internal bool HasDelivered(
        IReadOnlyDictionary<string, int> requirements,
        string destinationId)
    {
        return requirements.All(requirement =>
            items.CountDelivered(requirement.Key, destinationId)
            >= requirement.Value);
    }

    internal string BuildMaterialWaitReason(
        IReadOnlyDictionary<string, int> requirements,
        string destinationId)
    {
        string missing = requirements
            .Where(requirement =>
                items.CountDelivered(requirement.Key, destinationId)
                < requirement.Value)
            .Select(requirement =>
                $"{requirement.Key} "
                + $"{items.CountDelivered(requirement.Key, destinationId)}"
                + $"/{requirement.Value}")
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(missing)
            ? "파종 재료 확인 중"
            : $"파종 재료 운반 대기: {missing}";
    }
}
