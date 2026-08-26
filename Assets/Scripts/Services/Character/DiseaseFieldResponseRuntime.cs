using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer.Unity;

public interface IDiseaseFieldResponseCommand
{
    bool TryApply(
        CharacterId characterId,
        string diseaseId,
        string responseId,
        string facilityInstanceId,
        out DomainFailure failure);
}

public interface IDiseaseFieldResponseRecovery
{
    bool TryRecoverPending(out DomainFailure failure);
}

/// <summary>
/// Executes authored disease field responses through operational medical
/// facilities and their physical input buffers. Population health is staged on
/// a detached aggregate and published only after the exact supply consumption
/// succeeds.
/// </summary>
public sealed class DiseaseFieldResponseRuntime :
    IDiseaseFieldResponseCommand,
    IDiseaseFieldResponseRecovery
{
    public const string DispositionReasonCode =
        "disease-field-response-consumed";

    private readonly struct ResponseRule
    {
        public ResponseRule(string itemId, int amount, float severityReduction)
        {
            ItemId = itemId ?? string.Empty;
            Amount = Math.Max(0, amount);
            SeverityReduction = Math.Max(1f, severityReduction);
        }
        public string ItemId { get; }
        public int Amount { get; }
        public float SeverityReduction { get; }
    }

    private static readonly IReadOnlyDictionary<string, ResponseRule> Rules =
        new Dictionary<string, ResponseRule>(StringComparer.Ordinal)
        {
            ["response:respirator"] = new("medical:isolation-care-kit", 1, 10f),
            ["response:wet-cleaning"] = new("resource:clean-water", 2, 14f),
            ["response:boil-water"] = new("resource:clean-water", 2, 12f),
            ["response:antiparasitic"] = new("medicine:antidote", 1, 26f),
            ["response:fungicide-wash"] = new("supply:fungicide", 1, 24f),
            ["response:dry-isolation"] = new("medical:isolation-care-kit", 1, 16f),
            ["response:mana-shield"] = new("medical:isolation-care-kit", 1, 12f),
            ["response:blood-filtration"] = new("medicine:blood-pack", 1, 24f),
            ["response:sealed-blood-reserve"] = new("medicine:blood-pack", 1, 18f),
            ["response:night-watch"] = new("medical:isolation-care-kit", 1, 10f),
            ["response:hot-wash"] = new("resource:clean-water", 2, 18f),
            ["response:pest-lure"] = new("supply:pest-lure", 1, 20f),
            ["response:cooling-bed"] = new("medical:isolation-care-kit", 1, 18f),
            ["response:vent-seal"] = new("component:reclaimed-water-filter", 1, 16f),
            ["response:dreamless-sedative"] = new("drug:dreamleaf-analgesic", 1, 22f),
            ["response:spore-filter"] = new("component:reclaimed-water-filter", 1, 18f)
        };

    private readonly IDiseaseDefinitionCatalog diseases;
    private readonly IPopulationHealthQuery health;
    private readonly IPopulationHealthPersistence persistence;
    private readonly IFacilityCapabilityQuery facilities;
    private readonly IItemDefinitionCatalog items;
    private readonly IPhysicalFacilityItemSinkGateway physicalItems;
    private readonly IPackagedLotTareDispositionService packagedTare;
    private readonly IPhysicalVaccinationService vaccination;
    private readonly IPopulationDiseaseModifierQuery modifiers;

    public DiseaseFieldResponseRuntime(
        IDiseaseDefinitionCatalog diseases,
        IPopulationHealthQuery health,
        IPopulationHealthPersistence persistence,
        IFacilityCapabilityQuery facilities,
        IItemDefinitionCatalog items,
        IPhysicalFacilityItemSinkGateway physicalItems,
        IPackagedLotTareDispositionService packagedTare,
        IPhysicalVaccinationService vaccination,
        IPopulationDiseaseModifierQuery modifiers)
    {
        this.diseases = diseases ?? throw new ArgumentNullException(nameof(diseases));
        this.health = health ?? throw new ArgumentNullException(nameof(health));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.packagedTare = packagedTare
            ?? throw new ArgumentNullException(nameof(packagedTare));
        this.vaccination = vaccination ?? throw new ArgumentNullException(nameof(vaccination));
        this.modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
    }

    public bool TryApply(
        CharacterId characterId,
        string diseaseId,
        string responseId,
        string facilityInstanceId,
        out DomainFailure failure)
    {
        PopulationHealthWorldSaveData beforeRecovery = persistence.Capture();
        DiseaseFieldResponseCommitSaveData prior =
            beforeRecovery.pendingFieldResponse
            ?? new DiseaseFieldResponseCommitSaveData();
        bool samePendingRequest =
            (DiseaseFieldResponseCommitPhase)prior.phase
                != DiseaseFieldResponseCommitPhase.None
            && string.Equals(
                prior.characterId,
                characterId.Value,
                StringComparison.Ordinal)
            && string.Equals(prior.diseaseId, diseaseId, StringComparison.Ordinal)
            && string.Equals(prior.responseId, responseId, StringComparison.Ordinal);
        if (!TryRecoverPendingCore(out bool recoveredOperation, out failure))
        {
            return false;
        }
        if (samePendingRequest && recoveredOperation)
        {
            failure = DomainFailure.None;
            return true;
        }

        DiseaseDefinition disease;
        try { disease = diseases.Require(diseaseId); }
        catch (Exception)
        {
            failure = new DomainFailure(FailureCode.VaccineDiseaseMismatch, diseaseId);
            return false;
        }
        string response = responseId?.Trim() ?? string.Empty;
        if (!disease.FieldResponseIds.Contains(response, StringComparer.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.VaccineDiseaseMismatch,
                disease.Id,
                response);
            return false;
        }

        string preferredFacility = response.StartsWith(
                "response:vaccine:",
                StringComparison.Ordinal)
            ? "building:8876"
            : response.StartsWith("response:isolate:", StringComparison.Ordinal)
                ? "building:8874"
                : "building:8873";
        BuildableObject facility = ResolveFacility(
            facilityInstanceId,
            preferredFacility);
        string destination = facility?.PersistentInstanceId.Value
            ?? string.Empty;
        if (facility == null || destination.Length == 0)
        {
            failure = new DomainFailure(FailureCode.ServiceFeatureMissing, response);
            return false;
        }

        if (response.StartsWith("response:vaccine:", StringComparison.Ordinal))
        {
            return vaccination.TryVaccinate(
                characterId,
                destination,
                facility.centerPos,
                "medicine:vaccine:" + disease.Id.Substring("disease:".Length),
                out failure);
        }

        if (!health.TryGetCharacterSnapshot(characterId, out PopulationCharacterHealthSnapshot state)
            || !state.ActiveDiseases.Any(value =>
                string.Equals(value.DiseaseId, disease.Id, StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.PopulationHealthCharacterMissing,
                characterId.Value,
                disease.Id);
            return false;
        }

        ResponseRule rule = ResolveRule(response);
        if (rule.ItemId.Length > 0
            && (!items.TryGet((ItemDefinitionId)rule.ItemId, out _)))
        {
            failure = new DomainFailure(FailureCode.VaccineDefinitionMissing, rule.ItemId);
            return false;
        }

        PopulationHealthWorldSaveData intent = persistence.Capture();
        int sequence = intent.nextFieldResponseOperationSequence;
        string operationId = FormatOperationId(
            characterId,
            disease.Id,
            response,
            sequence);
        intent.pendingFieldResponse = new DiseaseFieldResponseCommitSaveData
        {
            phase = (int)DiseaseFieldResponseCommitPhase.IntentRecorded,
            operationSequence = sequence,
            operationId = operationId,
            reasonCode = DispositionReasonCode,
            characterId = characterId.Value,
            diseaseId = disease.Id,
            responseId = response,
            facilityInstanceId = destination,
            outputGridX = facility.centerPos.x,
            outputGridY = facility.centerPos.y,
            itemId = rule.ItemId,
            quantity = rule.Amount,
            severityReduction = rule.SeverityReduction
        };
        try
        {
            persistence.PublishRestore(persistence.PrepareRestore(intent));
        }
        catch (Exception)
        {
            failure = new DomainFailure(
                FailureCode.PopulationHealthCharacterMissing,
                characterId.Value,
                disease.Id,
                "field-response-intent-rejected");
            return false;
        }

        if (!physicalItems.TryCommitSinkPending(
                destination,
                rule.ItemId,
                rule.Amount,
                operationId,
                DispositionReasonCode,
                out _,
                out string consumeFailure))
        {
            TryClearUncommittedIntent(operationId);
            failure = new DomainFailure(
                FailureCode.VaccineDoseUnavailable,
                destination,
                rule.ItemId,
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
        string responseId,
        int sequence) =>
        $"disease-field-response:{characterId.Value}:"
        + $"{diseaseId}:{responseId}:{sequence:D8}";

    private bool TryRecoverPendingCore(
        out bool completedOperation,
        out DomainFailure failure)
    {
        completedOperation = false;
        PopulationHealthWorldSaveData captured = persistence.Capture();
        DiseaseFieldResponseCommitSaveData pending =
            captured.pendingFieldResponse
            ?? new DiseaseFieldResponseCommitSaveData();
        DiseaseFieldResponseCommitPhase phase =
            (DiseaseFieldResponseCommitPhase)pending.phase;
        if (phase == DiseaseFieldResponseCommitPhase.None)
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
                "field-response-receipt-mismatch");
            return false;
        }

        if (phase == DiseaseFieldResponseCommitPhase.IntentRecorded)
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
                    new Vector2Int(
                        pending.outputGridX,
                        pending.outputGridY),
                    receipt.CommitId,
                    out _,
                    out string tareFailure))
            {
                failure = new DomainFailure(
                    FailureCode.VaccineDoseUnavailable,
                    pending.operationId,
                    "field-response-package-disposition-failed",
                    tareFailure ?? string.Empty);
                return false;
            }

            captured.pendingFieldResponse.phase =
                (int)DiseaseFieldResponseCommitPhase.OutcomePublished;
            captured.pendingFieldResponse.sourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            captured.pendingFieldResponse.inputMassGrams =
                receipt.InputMassGrams;
            captured.pendingFieldResponse.commitId = receipt.CommitId;
            try
            {
                PopulationHealthAggregateState candidate =
                    persistence.PrepareRestore(captured);
                DiseaseDefinition disease = diseases.Require(pending.diseaseId);
                candidate.ApplyFieldResponse(
                    new CharacterId(pending.characterId),
                    disease.Id,
                    pending.severityReduction,
                    diseases,
                    modifiers.Resolve(
                        new CharacterId(pending.characterId),
                        disease));
                persistence.PublishRestore(candidate);
                phase = DiseaseFieldResponseCommitPhase.OutcomePublished;
                completedOperation = true;
            }
            catch (Exception exception)
            {
                failure = new DomainFailure(
                    FailureCode.PopulationHealthCharacterMissing,
                    pending.characterId,
                    pending.diseaseId,
                    "field-response-outcome-publication-failed",
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
                "field-response-acknowledge-failed",
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
        if ((DiseaseFieldResponseCommitPhase)captured.pendingFieldResponse.phase
                == DiseaseFieldResponseCommitPhase.IntentRecorded
            && string.Equals(
                captured.pendingFieldResponse.operationId,
                operationId,
                StringComparison.Ordinal))
        {
            ClearPending(captured, advanceSequence: false);
            persistence.PublishRestore(persistence.PrepareRestore(captured));
        }
    }

    private static bool ReceiptMatches(
        DiseaseFieldResponseCommitSaveData pending,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            pending.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            pending.reasonCode,
            StringComparison.Ordinal)
        && receipt.Quantity == pending.quantity;

    private static void ClearPending(
        PopulationHealthWorldSaveData data,
        bool advanceSequence)
    {
        if (advanceSequence)
        {
            data.nextFieldResponseOperationSequence = checked(
                data.nextFieldResponseOperationSequence + 1);
        }
        data.pendingFieldResponse = new DiseaseFieldResponseCommitSaveData();
    }

    private BuildableObject ResolveFacility(
        string requestedId,
        string preferredDefinitionId)
    {
        string requested = requestedId?.Trim() ?? string.Empty;
        IReadOnlyList<BuildableObject> exact =
            preferredDefinitionId == "building:8873"
                ? facilities.FindOperational(
                    ResearchFacilityCommandKind.PathogenDiagnosis)
                : facilities.FindOperational(
                    FacilityCapabilityKind.Medical,
                    preferredDefinitionId);
        BuildableObject selected = requested.Length == 0
            ? exact.FirstOrDefault()
            : exact.FirstOrDefault(value => string.Equals(
                value.PersistentInstanceId.Value,
                requested,
                StringComparison.Ordinal));
        if (selected == null && preferredDefinitionId == "building:8873")
        {
            selected = facilities.FindOperational(
                    FacilityCapabilityKind.Medical,
                    "building:8874")
                .FirstOrDefault(value => requested.Length == 0 || string.Equals(
                    value.PersistentInstanceId.Value,
                    requested,
                    StringComparison.Ordinal));
        }
        return selected;
    }

    private static ResponseRule ResolveRule(string response)
    {
        if (Rules.TryGetValue(response, out ResponseRule rule)) return rule;
        if (response.StartsWith("response:isolate:", StringComparison.Ordinal))
            return new ResponseRule("medical:isolation-care-kit", 1, 16f);
        if (response.StartsWith("response:environment:", StringComparison.Ordinal))
            return new ResponseRule("medical:isolation-care-kit", 1, 20f);
        return new ResponseRule("medical:isolation-care-kit", 1, 12f);
    }
}

public sealed class DiseaseFieldResponseRecoveryAdapter : IStartable
{
    private readonly IDiseaseFieldResponseRecovery recovery;

    public DiseaseFieldResponseRecoveryAdapter(
        IDiseaseFieldResponseRecovery recovery)
    {
        this.recovery = recovery
            ?? throw new ArgumentNullException(nameof(recovery));
    }

    public void Start()
    {
        if (!recovery.TryRecoverPending(out DomainFailure failure))
        {
            throw new InvalidOperationException(
                "Disease field-response recovery failed: " + failure);
        }
    }
}
