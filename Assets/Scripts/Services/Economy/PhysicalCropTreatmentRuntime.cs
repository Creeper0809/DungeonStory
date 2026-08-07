using System;
using System.Collections.Generic;
using System.Linq;

public interface IPhysicalCropTreatmentService
{
    bool TryApply(
        string plotBuildingInstanceId,
        string facilityDestinationId,
        string treatmentItemId,
        out DomainFailure failure);
}

/// <summary>
/// Applies authored crop-treatment items through the physical facility buffer.
/// The crop aggregate remains the only authority for pest and disease pressure.
/// </summary>
public sealed class PhysicalCropTreatmentRuntime : IPhysicalCropTreatmentService
{
    public const float PestLureReduction = 10f;
    public const float BotanicalPesticideReduction = 30f;
    public const float FungicideReduction = 30f;

    private readonly IItemDefinitionCatalog items;
    private readonly IItemTransferService transfers;
    private readonly ICropEcologyService ecology;

    public PhysicalCropTreatmentRuntime(
        IItemDefinitionCatalog items,
        IItemTransferService transfers,
        ICropEcologyService ecology)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.ecology = ecology ?? throw new ArgumentNullException(nameof(ecology));
    }

    public bool TryApply(
        string plotBuildingInstanceId,
        string facilityDestinationId,
        string treatmentItemId,
        out DomainFailure failure)
    {
        string plotId = plotBuildingInstanceId?.Trim() ?? string.Empty;
        if (plotId.Length == 0
            || !ecology.Plots.Any(value => value != null
                && string.Equals(value.plotId, plotId, StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.CropTreatmentPlotMissing,
                plotId);
            return false;
        }

        ItemDefinitionId definitionId = (ItemDefinitionId)(treatmentItemId?.Trim()
            ?? string.Empty);
        if (!definitionId.IsValid
            || !items.TryGet(definitionId, out ItemDefinitionSO definition)
            || definition is not ResourceItemDefinitionSO treatment
            || !treatment.TryGetCropTreatment(out CropTreatmentKind kind))
        {
            failure = new DomainFailure(
                FailureCode.CropTreatmentDefinitionMissing,
                definitionId.Value);
            return false;
        }

        if (!Enum.IsDefined(typeof(CropTreatmentKind), kind))
        {
            failure = new DomainFailure(
                FailureCode.CropTreatmentKindUnsupported,
                definitionId.Value,
                ((int)kind).ToString());
            return false;
        }

        string destinationId = facilityDestinationId?.Trim() ?? string.Empty;
        if (!transfers.TryConsumeFacilityItemBuffer(
                destinationId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [definitionId.Value] = 1
                },
                out string consumeFailure))
        {
            failure = new DomainFailure(
                FailureCode.CropTreatmentSupplyUnavailable,
                destinationId,
                definitionId.Value,
                consumeFailure ?? string.Empty);
            return false;
        }

        switch (kind)
        {
            case CropTreatmentKind.PestLure:
                ecology.ApplyPestControl(plotId, PestLureReduction);
                break;
            case CropTreatmentKind.BotanicalPesticide:
                ecology.ApplyPestControl(plotId, BotanicalPesticideReduction);
                break;
            case CropTreatmentKind.Fungicide:
                ecology.ApplyFungicide(plotId, FungicideReduction);
                break;
            default:
                throw new InvalidOperationException(
                    $"Validated crop-treatment kind '{kind}' was not handled.");
        }

        failure = DomainFailure.None;
        return true;
    }
}
