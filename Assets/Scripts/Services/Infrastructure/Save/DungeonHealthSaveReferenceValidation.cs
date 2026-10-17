using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DungeonSaveReferenceRules;

/// <summary>Validates detached health and medical joins.</summary>
internal sealed class DungeonHealthSaveReferenceValidation
{
    private readonly DungeonSaveReferenceRules references;
    private readonly IDiseaseDefinitionCatalog diseaseDefinitions;

    internal DungeonHealthSaveReferenceValidation(DungeonSaveReferenceRules references, IDiseaseDefinitionCatalog diseaseDefinitions)
    {
        this.references = references ?? throw new ArgumentNullException(nameof(references));
        this.diseaseDefinitions = diseaseDefinitions ?? throw new ArgumentNullException(nameof(diseaseDefinitions));
    }

    internal void ValidatePopulationHealth(
        PopulationHealthWorldSaveData source,
        GameplayOutcomeLedgerSaveData outcomeLedger,
        ISet<string> livingCharacterIds,
        ISet<string> knownCharacterIds,
        ISet<string> buildingInstanceIds,
        DungeonSavePhysicalReferenceIndex physical,
        DungeonGameRestoreReport report)
    {
        try
        {
            PopulationHealthSaveSection.RequireExposureLedgerRevision(source, outcomeLedger);
        }
        catch (InvalidOperationException exception)
        {
            report.AddError(exception.Message);
        }
        HashSet<string> recordIds = new(StringComparer.Ordinal);
        foreach (CharacterPopulationHealthSaveData health in source?.characters
                     ?? new List<CharacterPopulationHealthSaveData>())
        {
            if (health == null)
            {
                report.AddError("Population-health aggregate contains a null character record.");
                continue;
            }
            RequireUniqueId(health.characterId, "population-health character", recordIds, report);
            RequireReference(health.characterId, livingCharacterIds, "Population-health character", report);
            foreach (string diseaseId in (health.immunity ?? new List<DiseaseImmunitySaveData>())
                         .Where(value => value != null).Select(value => value.diseaseId)
                         .Concat((health.activeDiseases ?? new List<ActiveDiseaseSaveData>())
                             .Where(value => value != null).Select(value => value.diseaseId)))
            {
                RequireDiseaseDefinition(diseaseId, report);
            }
        }
        foreach (DiseaseExposureSaveData exposure in source?.pendingExposures
                     ?? new List<DiseaseExposureSaveData>())
        {
            if (exposure == null)
            {
                report.AddError("Population-health aggregate contains a null exposure.");
                continue;
            }
            RequireReference(exposure.characterId, livingCharacterIds, "Disease exposure character", report);
            RequireDiseaseDefinition(exposure.diseaseId, report);
        }
        foreach (PopulationDiseaseExposurePendingFactSaveData fact in source?.pendingExposureFacts
                     ?? new List<PopulationDiseaseExposurePendingFactSaveData>())
        {
            if (fact == null)
            {
                report.AddError("Population-health aggregate contains a null pending exposure fact.");
                continue;
            }
            // This is an already-applied fact, not a new health target. Its
            // actor may have died while delivery was pending.
            RequireReference(fact.characterId, knownCharacterIds, "Pending disease-exposure fact character", report);
            RequireDiseaseDefinition(fact.diseaseId, report);
        }
        foreach (var projection in source?.pendingAnatomyBurdenProjections
            ?? new List<DiseaseAnatomyBurdenProjectionSaveData>())
        {
            if (projection == null)
            { report.AddError("Population-health aggregate contains a null body projection."); continue; }
            // Already-applied observations remain replayable after the actor dies.
            RequireReference(projection.characterId,
                projection.phase == (int)DiseaseAnatomyBurdenProjectionPhase.Pending ? livingCharacterIds : knownCharacterIds,
                "Disease body projection character", report);
            RequireDiseaseDefinition(projection.diseaseId, report);
        }
        foreach (EpidemicStateSaveData epidemic in source?.epidemics
                     ?? new List<EpidemicStateSaveData>())
        {
            if (epidemic == null)
            {
                report.AddError("Population-health aggregate contains a null epidemic state.");
                continue;
            }
            RequireDiseaseDefinition(epidemic.diseaseId, report);
        }
        DiseaseFieldResponseCommitSaveData pending =
            source?.pendingFieldResponse;
        if (pending != null
            && (DiseaseFieldResponseCommitPhase)pending.phase
                != DiseaseFieldResponseCommitPhase.None)
        {
            RequireReference(
                pending.characterId,
                livingCharacterIds,
                "Disease field-response character",
                report);
            RequireReference(
                pending.facilityInstanceId,
                buildingInstanceIds,
                "Disease field-response facility",
                report);
            RequireDiseaseDefinition(pending.diseaseId, report);
            references.RequireItemDefinition(pending.itemId, report);
            ValidatePopulationHealthPhysicalReceipt(
                "Disease field-response",
                pending.operationId,
                pending.reasonCode,
                pending.quantity,
                (DiseaseFieldResponseCommitPhase)pending.phase
                    == DiseaseFieldResponseCommitPhase.OutcomePublished,
                pending.commitId,
                pending.inputMassGrams,
                physical,
                report);
        }

        VaccinationCommitSaveData vaccination = source?.pendingVaccination;
        if (vaccination != null
            && (VaccinationCommitPhase)vaccination.phase
                != VaccinationCommitPhase.None)
        {
            RequireReference(
                vaccination.characterId,
                livingCharacterIds,
                "Vaccination character",
                report);
            RequireReference(
                vaccination.facilityInstanceId,
                buildingInstanceIds,
                "Vaccination facility",
                report);
            RequireDiseaseDefinition(vaccination.diseaseId, report);
            references.RequireItemDefinition(vaccination.itemId, report);
            ValidatePopulationHealthPhysicalReceipt(
                "Vaccination",
                vaccination.operationId,
                vaccination.reasonCode,
                vaccination.quantity,
                (VaccinationCommitPhase)vaccination.phase
                    == VaccinationCommitPhase.OutcomePublished,
                vaccination.commitId,
                vaccination.inputMassGrams,
                physical,
                report);
        }
    }

    internal static void ValidatePopulationHealthPhysicalReceipt(
        string label,
        string operationId,
        string reasonCode,
        int quantity,
        bool outcomePublished,
        string commitId,
        long inputMassGrams,
        DungeonSavePhysicalReferenceIndex physical,
        DungeonGameRestoreReport report)
    {
        bool hasPending = physical.PendingBatchByOperation.TryGetValue(
            operationId ?? string.Empty,
            out PhysicalItemBatchDispositionSaveData receipt);
        if (!hasPending)
        {
            // Intent may be captured before its physical commit. An outcome may
            // be captured after acknowledgement but before its terminal clear.
            return;
        }

        bool exact = receipt != null
            && receipt.kind == (int)PhysicalItemDispositionKind.Sink
            && string.Equals(
                receipt.operationId,
                operationId,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.reasonCode,
                reasonCode,
                StringComparison.Ordinal)
            && receipt.quantity == quantity;
        if (outcomePublished)
        {
            exact = exact
                && string.Equals(
                    receipt.commitId,
                    commitId,
                    StringComparison.Ordinal)
                && receipt.inputMassGrams == inputMassGrams;
        }
        if (!exact)
        {
            report.AddError(
                $"{label} '{operationId}' does not join its exact pending physical Sink receipt.");
        }
    }

    internal void ValidateCharacterMedicalPhysicalReceipts(
        DungeonCharacterMedicalSaveData source,
        ISet<string> livingCharacterIds,
        ISet<string> buildingInstanceIds,
        DungeonSavePhysicalReferenceIndex physical,
        DungeonGameRestoreReport report)
    {
        foreach (CharacterMedicalOrder order in source?.orders
                     ?? new List<CharacterMedicalOrder>())
        {
            if (order == null)
            {
                continue;
            }
            if (!CharacterMedicalSaveValidation.IsCharacterId(order.patientId))
            {
                report.AddError(
                    $"Medical order '{order.orderId}' has an invalid patient ID.");
            }
            else if (CharacterMedicalSaveValidation
                     .RequiresLivingPatientReference(order))
            {
                RequireReference(
                    order.patientId,
                    livingCharacterIds,
                    $"Medical order '{order.orderId}' patient",
                    report);
            }
            if (!string.IsNullOrEmpty(order.treatmentFacilityId))
            {
                RequireReference(
                    order.treatmentFacilityId,
                    buildingInstanceIds,
                    $"Medical order '{order.orderId}' facility",
                    report);
            }

            CharacterMedicalSupplyCommitPhase phase =
                (CharacterMedicalSupplyCommitPhase)
                    order.treatmentSupplyCommitPhase;
            if (phase == CharacterMedicalSupplyCommitPhase.None)
            {
                continue;
            }
            references.RequireItemDefinition(order.treatmentPhysicalItemId, report);
            bool hasPending = physical.PendingBatchByOperation.TryGetValue(
                order.treatmentSupplyOperationId ?? string.Empty,
                out PhysicalItemBatchDispositionSaveData receipt);
            if (!hasPending)
            {
                // Intent may precede the physical commit. SupplyPublished may
                // follow acknowledgement and precede terminal provenance clear.
                continue;
            }

            bool exact = receipt != null
                && receipt.kind == (int)PhysicalItemDispositionKind.Sink
                && string.Equals(
                    receipt.operationId,
                    order.treatmentSupplyOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    receipt.reasonCode,
                    order.treatmentSupplyReasonCode,
                    StringComparison.Ordinal)
                && receipt.quantity == order.treatmentPhysicalQuantity;
            if (phase == CharacterMedicalSupplyCommitPhase.SupplyPublished)
            {
                exact = exact
                    && string.Equals(
                        receipt.commitId,
                        order.treatmentPhysicalCommitId,
                        StringComparison.Ordinal)
                    && receipt.inputMassGrams == order.treatmentInputMassGrams;
            }
            if (!exact)
            {
                report.AddError(
                    $"Medical order '{order.orderId}' does not join its exact pending physical Sink receipt.");
            }
        }
    }

    internal void RequireDiseaseDefinition(
        string diseaseId,
        DungeonGameRestoreReport report)
    {
        try
        {
            diseaseDefinitions.Require(diseaseId);
        }
        catch (Exception)
        {
            report.AddError(
                $"Save references unknown disease definition '{diseaseId ?? string.Empty}'.");
        }
    }
}
