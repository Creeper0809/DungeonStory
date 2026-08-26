using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

public interface IPhysicalVaccinationService
{
    bool TryVaccinate(
        CharacterId characterId,
        string medicalFacilityDestinationId,
        Vector2Int outputPosition,
        string vaccineItemId,
        out DomainFailure failure);
}

public interface IPhysicalVaccinationRecovery
{
    bool TryRecoverPending(out DomainFailure failure);
}

/// <summary>
/// Commits one exact vaccine lot as a physical Sink, publishes immunity through
/// the population-health aggregate, and acknowledges the physical receipt only
/// after the durable health outbox contains the outcome.
/// </summary>
public sealed class PhysicalVaccinationRuntime :
    IPhysicalVaccinationService,
    IPhysicalVaccinationRecovery
{
    public const string DispositionReasonCode =
        "vaccination-dose-administered";

    private readonly IItemDefinitionCatalog items;
    private readonly ICharacterLifeQuery life;
    private readonly IDiseaseDefinitionCatalog diseases;
    private readonly IPopulationHealthPersistence persistence;
    private readonly IPopulationDiseaseModifierQuery modifiers;
    private readonly IPhysicalFacilityItemSinkGateway physicalItems;
    private readonly IPackagedLotTareDispositionService packagedTare;

    public PhysicalVaccinationRuntime(
        IItemDefinitionCatalog items,
        ICharacterLifeQuery life,
        IDiseaseDefinitionCatalog diseases,
        IPopulationHealthPersistence persistence,
        IPopulationDiseaseModifierQuery modifiers,
        IPhysicalFacilityItemSinkGateway physicalItems,
        IPackagedLotTareDispositionService packagedTare)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.packagedTare = packagedTare
            ?? throw new ArgumentNullException(nameof(packagedTare));
    }

    public bool TryVaccinate(
        CharacterId characterId,
        string medicalFacilityDestinationId,
        Vector2Int outputPosition,
        string vaccineItemId,
        out DomainFailure failure)
    {
        PopulationHealthWorldSaveData beforeRecovery = persistence.Capture();
        VaccinationCommitSaveData prior = beforeRecovery.pendingVaccination
            ?? new VaccinationCommitSaveData();
        bool samePendingRequest =
            (VaccinationCommitPhase)prior.phase != VaccinationCommitPhase.None
            && string.Equals(prior.characterId, characterId.Value, StringComparison.Ordinal)
            && string.Equals(prior.itemId, vaccineItemId, StringComparison.Ordinal)
            && string.Equals(
                prior.facilityInstanceId,
                medicalFacilityDestinationId,
                StringComparison.Ordinal);
        if (!TryRecoverPendingCore(out bool recoveredOperation, out failure))
        {
            return false;
        }
        if (samePendingRequest && recoveredOperation)
        {
            failure = DomainFailure.None;
            return true;
        }

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
            || vaccine.VaccineDoses != 1)
        {
            failure = new DomainFailure(
                FailureCode.VaccineDefinitionMissing,
                definitionId.Value);
            return false;
        }

        DiseaseDefinition disease;
        try
        {
            disease = diseases.Require(vaccine.VaccineDiseaseId);
        }
        catch (Exception)
        {
            failure = new DomainFailure(
                FailureCode.VaccineDiseaseMismatch,
                definitionId.Value,
                vaccine.VaccineDiseaseId);
            return false;
        }
        if (!disease.VaccineAllowed)
        {
            failure = new DomainFailure(
                FailureCode.VaccineDiseaseMismatch,
                definitionId.Value,
                disease.Id);
            return false;
        }

        string destinationId = medicalFacilityDestinationId?.Trim()
            ?? string.Empty;
        if (destinationId.Length == 0)
        {
            failure = new DomainFailure(
                FailureCode.VaccineDoseUnavailable,
                destinationId,
                definitionId.Value,
                "vaccination-facility-destination-missing");
            return false;
        }

        PopulationHealthWorldSaveData intent = persistence.Capture();
        int sequence = intent.nextVaccinationOperationSequence;
        string operationId = FormatOperationId(characterId, disease.Id, sequence);
        intent.pendingVaccination = new VaccinationCommitSaveData
        {
            phase = (int)VaccinationCommitPhase.IntentRecorded,
            operationSequence = sequence,
            operationId = operationId,
            reasonCode = DispositionReasonCode,
            characterId = characterId.Value,
            diseaseId = disease.Id,
            facilityInstanceId = destinationId,
            outputGridX = outputPosition.x,
            outputGridY = outputPosition.y,
            itemId = definitionId.Value,
            quantity = 1
        };
        try
        {
            persistence.PublishRestore(persistence.PrepareRestore(intent));
        }
        catch (Exception exception)
        {
            failure = new DomainFailure(
                FailureCode.VaccineDoseUnavailable,
                destinationId,
                definitionId.Value,
                "vaccination-intent-rejected",
                exception.GetType().Name);
            return false;
        }

        if (!physicalItems.TryCommitSinkPending(
                destinationId,
                definitionId.Value,
                1,
                operationId,
                DispositionReasonCode,
                out _,
                out string consumeFailure))
        {
            TryClearUncommittedIntent(operationId);
            failure = new DomainFailure(
                FailureCode.VaccineDoseUnavailable,
                destinationId,
                definitionId.Value,
                consumeFailure ?? string.Empty);
            return false;
        }

        if (!TryRecoverPendingCore(out bool completed, out failure)
            || !completed)
        {
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool TryRecoverPending(out DomainFailure failure) =>
        TryRecoverPendingCore(out _, out failure);

    public static string FormatOperationId(
        CharacterId characterId,
        string diseaseId,
        int sequence) =>
        $"vaccination:{characterId.Value}:{diseaseId}:{sequence:D8}";

    private bool TryRecoverPendingCore(
        out bool completedOperation,
        out DomainFailure failure)
    {
        completedOperation = false;
        PopulationHealthWorldSaveData captured = persistence.Capture();
        VaccinationCommitSaveData pending = captured.pendingVaccination
            ?? new VaccinationCommitSaveData();
        VaccinationCommitPhase phase = (VaccinationCommitPhase)pending.phase;
        if (phase == VaccinationCommitPhase.None)
        {
            failure = DomainFailure.None;
            return true;
        }

        bool hasReceipt = physicalItems.TryGetPending(
            pending.operationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (hasReceipt && !ReceiptMatches(pending, receipt))
        {
            failure = new DomainFailure(
                FailureCode.VaccineDoseUnavailable,
                pending.operationId,
                "vaccination-receipt-mismatch");
            return false;
        }

        if (phase == VaccinationCommitPhase.IntentRecorded)
        {
            if (!hasReceipt)
            {
                ClearPending(captured, advanceSequence: false);
                persistence.PublishRestore(persistence.PrepareRestore(captured));
                failure = DomainFailure.None;
                return true;
            }

            if (!packagedTare.EnsureTerminalSinkOutputs(
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [pending.itemId] = pending.quantity
                    },
                    new Vector2Int(pending.outputGridX, pending.outputGridY),
                    receipt.CommitId,
                    out _,
                    out string tareFailure))
            {
                failure = new DomainFailure(
                    FailureCode.VaccineDoseUnavailable,
                    pending.operationId,
                    "vaccination-package-disposition-failed",
                    tareFailure ?? string.Empty);
                return false;
            }

            captured.pendingVaccination.phase =
                (int)VaccinationCommitPhase.OutcomePublished;
            captured.pendingVaccination.sourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            captured.pendingVaccination.inputMassGrams = receipt.InputMassGrams;
            captured.pendingVaccination.commitId = receipt.CommitId;
            try
            {
                PopulationHealthAggregateState candidate =
                    persistence.PrepareRestore(captured);
                DiseaseDefinition disease = diseases.Require(pending.diseaseId);
                CharacterId characterId = new(pending.characterId);
                candidate.Vaccinate(
                    characterId,
                    disease.Id,
                    diseases,
                    modifiers.Resolve(characterId, disease));
                persistence.PublishRestore(candidate);
                completedOperation = true;
            }
            catch (Exception exception)
            {
                failure = new DomainFailure(
                    FailureCode.PopulationHealthCharacterMissing,
                    pending.characterId,
                    pending.diseaseId,
                    "vaccination-outcome-publication-failed",
                    exception.GetType().Name);
                return false;
            }
        }
        else
        {
            completedOperation = true;
        }

        if (hasReceipt
            && !physicalItems.Acknowledge(
                receipt.CommitId,
                out string acknowledgeFailure))
        {
            failure = new DomainFailure(
                FailureCode.VaccineDoseUnavailable,
                pending.operationId,
                "vaccination-acknowledge-failed",
                acknowledgeFailure ?? string.Empty);
            return false;
        }

        PopulationHealthWorldSaveData terminal = persistence.Capture();
        ClearPending(terminal, advanceSequence: true);
        persistence.PublishRestore(persistence.PrepareRestore(terminal));
        failure = DomainFailure.None;
        return true;
    }

    private void TryClearUncommittedIntent(string operationId)
    {
        if (physicalItems.TryGetPending(operationId, out _))
        {
            return;
        }
        PopulationHealthWorldSaveData captured = persistence.Capture();
        if ((VaccinationCommitPhase)captured.pendingVaccination.phase
                == VaccinationCommitPhase.IntentRecorded
            && string.Equals(
                captured.pendingVaccination.operationId,
                operationId,
                StringComparison.Ordinal))
        {
            ClearPending(captured, advanceSequence: false);
            persistence.PublishRestore(persistence.PrepareRestore(captured));
        }
    }

    private static bool ReceiptMatches(
        VaccinationCommitSaveData pending,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(receipt.OperationId, pending.operationId, StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode, pending.reasonCode, StringComparison.Ordinal)
        && receipt.Quantity == pending.quantity;

    private static void ClearPending(
        PopulationHealthWorldSaveData data,
        bool advanceSequence)
    {
        if (advanceSequence)
        {
            data.nextVaccinationOperationSequence = checked(
                data.nextVaccinationOperationSequence + 1);
        }
        data.pendingVaccination = new VaccinationCommitSaveData();
    }
}

public sealed class PhysicalVaccinationRecoveryAdapter : IStartable
{
    private readonly IPhysicalVaccinationRecovery recovery;

    public PhysicalVaccinationRecoveryAdapter(
        IPhysicalVaccinationRecovery recovery)
    {
        this.recovery = recovery
            ?? throw new ArgumentNullException(nameof(recovery));
    }

    public void Start()
    {
        if (!recovery.TryRecoverPending(out DomainFailure failure))
        {
            throw new InvalidOperationException(
                "Physical vaccination recovery failed: " + failure);
        }
    }
}
