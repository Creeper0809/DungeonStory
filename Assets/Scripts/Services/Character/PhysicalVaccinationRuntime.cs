using System;
using System.Collections.Generic;

public interface IPhysicalVaccinationService
{
    bool TryVaccinate(
        CharacterId characterId,
        string medicalFacilityDestinationId,
        string vaccineItemId,
        out DomainFailure failure);
}

/// <summary>
/// The only player-facing vaccination command. It validates authored vaccine
/// content and the target before consuming one physical dose from the medical
/// facility buffer, then delegates immunity state to the population-health
/// aggregate.
/// </summary>
public sealed class PhysicalVaccinationRuntime : IPhysicalVaccinationService
{
    private readonly IItemDefinitionCatalog items;
    private readonly IItemTransferService transfers;
    private readonly ICharacterLifeQuery life;
    private readonly IDiseaseDefinitionCatalog diseases;
    private readonly IPopulationHealthService populationHealth;

    public PhysicalVaccinationRuntime(
        IItemDefinitionCatalog items,
        IItemTransferService transfers,
        ICharacterLifeQuery life,
        IDiseaseDefinitionCatalog diseases,
        IPopulationHealthService populationHealth)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.transfers = transfers ?? throw new ArgumentNullException(nameof(transfers));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
        this.populationHealth = populationHealth
            ?? throw new ArgumentNullException(nameof(populationHealth));
    }

    public bool TryVaccinate(
        CharacterId characterId,
        string medicalFacilityDestinationId,
        string vaccineItemId,
        out DomainFailure failure)
    {
        if (!characterId.IsValid || !life.TryGet(characterId, out _))
        {
            failure = new DomainFailure(
                FailureCode.PopulationHealthCharacterMissing,
                characterId.Value);
            return false;
        }

        ItemDefinitionId definitionId = (ItemDefinitionId)(vaccineItemId?.Trim()
            ?? string.Empty);
        if (!definitionId.IsValid
            || !items.TryGet(definitionId, out ItemDefinitionSO definition)
            || definition is not ResourceItemDefinitionSO vaccine
            || string.IsNullOrWhiteSpace(vaccine.VaccineDiseaseId)
            || vaccine.VaccineDoses < 1)
        {
            failure = new DomainFailure(
                FailureCode.VaccineDefinitionMissing,
                definitionId.Value);
            return false;
        }

        try
        {
            diseases.Require(vaccine.VaccineDiseaseId);
        }
        catch (Exception)
        {
            failure = new DomainFailure(
                FailureCode.VaccineDiseaseMismatch,
                definitionId.Value,
                vaccine.VaccineDiseaseId);
            return false;
        }

        string destinationId = medicalFacilityDestinationId?.Trim()
            ?? string.Empty;
        if (!transfers.TryConsumeFacilityItemBuffer(
                destinationId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [definitionId.Value] = 1
                },
                out string consumeFailure))
        {
            failure = new DomainFailure(
                FailureCode.VaccineDoseUnavailable,
                destinationId,
                definitionId.Value,
                consumeFailure ?? string.Empty);
            return false;
        }

        // All fallible authored-reference validation is complete before the
        // physical item is consumed. PopulationHealthRuntime owns the sole
        // immunity write and cannot fail for this validated tuple.
        populationHealth.Vaccinate(characterId, vaccine.VaccineDiseaseId);
        failure = DomainFailure.None;
        return true;
    }
}
