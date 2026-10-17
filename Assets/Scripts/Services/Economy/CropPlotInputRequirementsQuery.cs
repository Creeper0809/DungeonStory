using System;
using System.Collections.Generic;
using static CropPlotSaveContract;

internal sealed class CropPlotInputRequirementsQuery
{
    private readonly ICropCycleInputRequirementQuery inputRequirements;

    internal CropPlotInputRequirementsQuery(
        ICropCycleInputRequirementQuery inputRequirements)
    {
        this.inputRequirements = inputRequirements;
    }

    internal static bool RequiresSowInputAuthority(CropPlotState state) =>
        state != null
        && (state.PendingSow?.phase != CropPhysicalCommitPhase.None
            || HasFrozenSowInputs(state)
            && !state.MaterialsConsumed
            && state.Phase is CropPlotPhase.Empty
                or CropPlotPhase.WaitingForMaterials);

    internal Dictionary<string, int> BuildMaterialRequirements(
        CropPlotState state,
        CropDefinitionSO crop,
        BuildingCropPlotAbility ability = null)
    {
        if (state == null || crop == null
            || state.FrozenSowInputOperationSequence
                != state.NextSowOperationSequence
            || state.FrozenSowInputs == null
            || state.FrozenSowInputs.Count == 0
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                state.FrozenSowInputSourceDigest)
            || !string.Equals(
                state.FrozenSowInputVectorDigest,
                CaptureFrozenSowInputVectorDigest(
                    state.PlotId,
                    crop.CropId,
                    state.FrozenSowInputOperationSequence,
                    state.FrozenSowInputs),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop sow input requirements are not frozen for the active operation.");
        }
        ability ??= state.Ability;
        if (ability == null)
            throw new InvalidOperationException(
                "Crop sow input requirements have no facility ability authority.");
        _ = inputRequirements.RehydrateAndValidate(
            crop,
            ability,
            state.MaterialDestinationId,
            state.FrozenSowInputWeather,
            state.FrozenSowInputConsumptionMultiplier,
            state.FrozenSowInputSelectedFuelItemId,
            state.FrozenSowInputs,
            state.FrozenSowInputSourceDigest);
        return new Dictionary<string, int>(
            state.FrozenSowInputs,
            StringComparer.Ordinal);
    }

    internal static bool HasFrozenSowInputs(CropPlotState state) =>
        state != null
        && state.FrozenSowInputOperationSequence >= 0
        && state.FrozenSowInputs != null
        && state.FrozenSowInputs.Count > 0;
}
