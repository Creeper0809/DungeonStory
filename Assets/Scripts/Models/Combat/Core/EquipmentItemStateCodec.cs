using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public static class EquipmentItemStateCodec
{
    public const int CurrentSchemaVersion = 4;
    private const int LegacySchemaVersion = 3;
    private const int ModuleSelectionFormulaVersion = 2;
    private const int DrawbackModuleSelectionFormulaVersion = 3;
    private const string StateJsonKey = "state-json";

    public static ItemInstanceComponentSaveData Encode(
        CombatEquipmentInstance instance,
        IEnumerable<EquipmentModuleInstance> attachedModules = null)
    {
        if (instance == null || string.IsNullOrWhiteSpace(instance.instanceId))
        {
            throw new ArgumentException(
                "A persistent combat-equipment instance is required.",
                nameof(instance));
        }

        List<EquipmentModuleInstance> moduleSnapshots =
            (attachedModules ?? Array.Empty<EquipmentModuleInstance>())
            .Where(module => module != null)
            .Select(module => module.Clone())
            .ToList();
        foreach (EquipmentModuleInstance module in moduleSnapshots)
        {
            if (!TryValidateAttachedModule(
                    instance.instanceId,
                    module,
                    out string moduleError))
            {
                throw new ArgumentException(
                    $"Attached module '{module.instanceId}' is invalid: {moduleError}",
                    nameof(attachedModules));
            }
        }

        EquipmentPhysicalStatePayload snapshot = new()
        {
            equipment = instance.Clone(),
            attachedModules = moduleSnapshots
        };
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.Equipment,
            schemaVersion = CurrentSchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new ItemStateValueSaveData
                {
                    key = StateJsonKey,
                    kind = ItemStateValueKind.String,
                    stringValue = JsonUtility.ToJson(snapshot)
                }
            }
        };
    }

    public static bool TryDecode(
        ItemInstanceComponentSaveData component,
        out CombatEquipmentInstance instance,
        out string error)
    {
        if (TryDecodeFull(component, out EquipmentPhysicalStatePayload payload, out error))
        {
            instance = payload.equipment.Clone();
            return true;
        }

        instance = null;
        return false;
    }

    public static bool TryDecodeFull(
        ItemInstanceComponentSaveData component,
        out EquipmentPhysicalStatePayload payload,
        out string error)
    {
        payload = null;
        if (component == null
            || !string.Equals(
                component.componentTypeId,
                ItemInstanceComponentIds.Equipment,
                StringComparison.Ordinal))
        {
            error = "The item component is not combat-equipment state.";
            return false;
        }
        if (component.schemaVersion != CurrentSchemaVersion
            && component.schemaVersion != LegacySchemaVersion)
        {
            error = $"Unsupported equipment item-state schema V{component.schemaVersion}.";
            return false;
        }

        string json = component.values?
            .FirstOrDefault(value => value != null
                && string.Equals(value.key, StateJsonKey, StringComparison.Ordinal)
                && value.kind == ItemStateValueKind.String)?
            .stringValue;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Equipment item-state has no state payload.";
            return false;
        }

        try
        {
            EquipmentPhysicalStatePayload restored =
                JsonUtility.FromJson<EquipmentPhysicalStatePayload>(json);
            if (restored?.equipment == null
                || string.IsNullOrWhiteSpace(restored.equipment.instanceId)
                || string.IsNullOrWhiteSpace(restored.equipment.definitionId))
            {
                error = "Equipment item-state payload has no persistent identity.";
                return false;
            }

            restored.equipment.loadedAmmunition ??=
                new LoadedAmmunitionBatch();
            restored.equipment.evolution ??= new EquipmentEvolutionState();
            if (component.schemaVersion == LegacySchemaVersion)
            {
                NormalizeLegacyEvolution(restored.equipment.evolution);
            }
            else if (!TryValidateFormulaEvolution(
                         restored.equipment.evolution,
                         out error))
            {
                return false;
            }
            if (!Enum.IsDefined(
                    typeof(CombatEquipmentWorldState),
                    restored.equipment.worldState)
                || (restored.equipment.worldState
                        == CombatEquipmentWorldState.MarketSalePending
                    && (!string.IsNullOrEmpty(
                            restored.equipment.ownerCharacterId)
                        || string.IsNullOrWhiteSpace(
                            restored.equipment.sourceStackId)
                        || !string.Equals(
                            restored.equipment.sourceStackId,
                            restored.equipment.sourceStackId.Trim(),
                            StringComparison.Ordinal)
                        || (restored.equipment.moduleSlots
                                ?? new List<EquipmentModuleSlotState>())
                            .Any(slot => slot != null
                                && !string.IsNullOrWhiteSpace(
                                    slot.moduleInstanceId)))))
            {
                error = "Equipment market-sale custody is invalid.";
                return false;
            }
            restored.equipment.powerCharge = Mathf.Clamp(
                restored.equipment.powerCharge,
                0f,
                100f);
            if (restored.equipment.loadedAmmunition.remaining <= 0)
            {
                restored.equipment.loadedAmmunition.Clear();
            }
            else if (string.IsNullOrWhiteSpace(
                         restored.equipment.loadedAmmunition.ammunitionItemId))
            {
                error = "Loaded ammunition has quantity but no physical ammunition item ID.";
                return false;
            }

            restored.attachedModules ??= new List<EquipmentModuleInstance>();
            foreach (EquipmentModuleInstance module in restored.attachedModules)
            {
                if (!TryValidateAttachedModule(
                        restored.equipment.instanceId,
                        module,
                        out string moduleError))
                {
                    error = $"Attached equipment-module state is invalid: {moduleError}";
                    return false;
                }
            }
            payload = restored;
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Equipment item-state payload is invalid: {exception.Message}";
            return false;
        }
    }

    private static bool TryValidateAttachedModule(
        string equipmentInstanceId,
        EquipmentModuleInstance module,
        out string error)
    {
        if (module == null
            || !((ItemInstanceId)module.instanceId).IsValid
            || string.IsNullOrWhiteSpace(module.definitionId)
            || module.state != EquipmentModuleProcessState.Installed
            || !string.IsNullOrWhiteSpace(module.sourceStackId)
            || !string.Equals(
                module.attachedEquipmentInstanceId,
                equipmentInstanceId,
                StringComparison.Ordinal))
        {
            error = "The attached module has invalid identity, ownership, or process state.";
            return false;
        }

        if (!EquipmentModuleItemStateCodec.TryValidateAppraisalState(
                module,
                out error))
        {
            return false;
        }

        if ((EquipmentModuleAppraisalCommitPhase)module.pendingAppraisal.phase
            != EquipmentModuleAppraisalCommitPhase.None)
        {
            error = "An attached module cannot own a pending appraisal operation.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void NormalizeLegacyEvolution(EquipmentEvolutionState state)
    {
        state.formulaEvidence = new List<EquipmentEvolutionFormulaEvidenceRecord>();
        state.presentationRequests = new List<EquipmentEvolutionPresentationRequest>();
        foreach (EvolutionNode node in state.evolutionNodes ?? new List<EvolutionNode>())
        {
            if (node == null) continue;
            node.formulaVersion = 0;
            node.formulaCatalogSha256 = string.Empty;
            node.formulaCapabilities = new List<EquipmentEvolutionFormulaCapabilityEnvelope>();
            node.formulaBudget = 0;
            node.calculatedCost = 0;
            node.mechanicalDescription = string.Empty;
            node.presentationId = string.Empty;
            node.narrativeFlavor = string.Empty;
            node.presentationState = EquipmentEvolutionPresentationState.Legacy;
            node.presentationFailureCount = 0;
        }
    }

    private static bool TryValidateFormulaEvolution(
        EquipmentEvolutionState state,
        out string error)
    {
        error = string.Empty;
        state.formulaEvidence ??= new List<EquipmentEvolutionFormulaEvidenceRecord>();
        state.presentationRequests ??= new List<EquipmentEvolutionPresentationRequest>();
        state.evolutionNodes ??= new List<EvolutionNode>();
        if (state.generation < 0
            || float.IsNaN(state.mastery)
            || float.IsInfinity(state.mastery)
            || state.mastery < 0f)
        {
            error = "Equipment formula owner progression is invalid.";
            return false;
        }
        if (state.formulaEvidence.Any(value => value == null
                || !IsCanonical(value.evidenceId)
                || !IsCanonical(value.eventGroupKey)
                || !IsCanonical(value.actionKey)
                || !IsCanonical(value.domainKey)
                || value.relationshipKey == null
                || !string.Equals(value.relationshipKey, value.relationshipKey.Trim(), StringComparison.Ordinal)
                || value.originalEvent == null
                || !string.Equals(value.evidenceId, value.originalEvent.evidenceId, StringComparison.Ordinal)
                || !string.Equals(value.eventGroupKey, value.originalEvent.eventId, StringComparison.Ordinal)
                || !string.Equals(value.actionKey, value.originalEvent.eventId, StringComparison.Ordinal)
                || !string.Equals(value.domainKey, "equipment", StringComparison.Ordinal)
                || !IsCanonical(value.originalEvent.eventId)
                || float.IsNaN(value.originalEvent.amount)
                || float.IsInfinity(value.originalEvent.amount)
                || value.originalEvent.generation < 0
                || value.originalEvent.repeatCount < 1
                || value.originalEvent.sequence < 1L
                || value.attainedMilestoneCount < 0
                || value.influenceUseCount < 0
                || double.IsNaN(value.importancePoints)
                || double.IsInfinity(value.importancePoints)
                || value.importancePoints < 0d)
            || state.formulaEvidence.Select(value => value.evidenceId)
                .Distinct(StringComparer.Ordinal).Count() != state.formulaEvidence.Count)
        {
            error = "Equipment formula evidence ledger is invalid.";
            return false;
        }
        if (state.presentationRequests.Any(value => value == null
                || !IsPresentationId(value.presentationId)
                || !IsCanonical(value.nodeId)
                || !IsCanonical(value.targetPersistentId)
                || !IsCanonical(value.historyHash)
                || value.attunementOwnerPersistentId == null
                || value.reforgeOrderId == null
                || !IsOptionalCanonical(value.attunementOwnerPersistentId)
                || !IsOptionalCanonical(value.reforgeOrderId)
                || string.IsNullOrEmpty(value.attunementOwnerPersistentId)
                    == string.IsNullOrEmpty(value.reforgeOrderId)
                || value.targetGeneration < 0
                || float.IsNaN(value.masteryCost)
                || float.IsInfinity(value.masteryCost)
                || value.masteryCost < 0f
                || value.evidenceIds == null
                || value.evidenceIds.Count == 0
                || value.evidenceIds.Any(id => !IsCanonical(id))
                || value.evidenceIds.Distinct(StringComparer.Ordinal).Count()
                    != value.evidenceIds.Count
                || !value.evidenceIds.SequenceEqual(
                    value.evidenceIds.OrderBy(id => id, StringComparer.Ordinal),
                    StringComparer.Ordinal)
                || value.failureCount < 0 || value.failureCount > 5
                || value.state is not EquipmentEvolutionPresentationState.PresentationPending
                    and not EquipmentEvolutionPresentationState.ModuleSelectionPending
                    and not EquipmentEvolutionPresentationState.AwaitingNarrativeRetry
                || (value.state is EquipmentEvolutionPresentationState.PresentationPending
                    or EquipmentEvolutionPresentationState.ModuleSelectionPending)
                    && value.failureCount >= 5
                || value.state == EquipmentEvolutionPresentationState.AwaitingNarrativeRetry
                    && value.failureCount != 5)
            || state.presentationRequests.Select(value => value.presentationId)
                .Distinct(StringComparer.Ordinal).Count() != state.presentationRequests.Count)
        {
            error = "Equipment presentation request ledger is invalid.";
            return false;
        }
        EvolutionNode[] formulaNodes = state.evolutionNodes
            .Where(value => value != null && value.formulaVersion > 0)
            .ToArray();
        if (formulaNodes.Select(value => value.nodeId).Distinct(StringComparer.Ordinal).Count()
                != formulaNodes.Length
            || formulaNodes.Select(value => value.presentationId)
                .Distinct(StringComparer.Ordinal).Count() != formulaNodes.Length
            || formulaNodes.Any(value => value.generation < 0
                || value.formulaBudget < 0
                || value.calculatedCost > value.formulaBudget
                || !value.evidenceIds.SequenceEqual(
                    value.evidenceIds.OrderBy(id => id, StringComparer.Ordinal),
                    StringComparer.Ordinal)))
        {
            error = "Equipment formula node identity or budget is invalid.";
            return false;
        }
        foreach (EvolutionNode node in state.evolutionNodes.Where(value => value != null))
        {
            if (node.formulaVersion == 0) continue;
            bool unresolvedSelection = node.formulaVersion
                    >= ModuleSelectionFormulaVersion
                && node.moduleSelectionOffers != null
                && node.moduleSelectionOffers.Count > 0
                && node.presentationState is
                    EquipmentEvolutionPresentationState.ModuleSelectionPending
                    or EquipmentEvolutionPresentationState.AwaitingNarrativeRetry;
            if (node.formulaVersion < 0
                || !IsCanonical(node.nodeId)
                || !IsOptionalCanonical(node.parentNodeId)
                || !IsSha256(node.formulaCatalogSha256)
                || !IsPresentationId(node.presentationId)
                || !unresolvedSelection && string.IsNullOrWhiteSpace(node.mechanicalDescription)
                || node.formulaBudget < 0
                || node.calculatedCost < 0
                || node.formulaCapabilities == null
                || !unresolvedSelection && node.formulaCapabilities.Count < 1
                || node.formulaCapabilities.Count > 3
                || node.formulaCapabilities.Any(capability => capability == null
                    || !IsCanonical(capability.capabilityId)
                    || !IsCanonical(capability.formatterId)
                    || !IsCanonical(capability.applicatorId)
                    || capability.parameters == null
                    || capability.parameters.Count != 4
                    || capability.parameters.Any(parameter => parameter == null
                        || !IsCanonical(parameter.parameterId))
                    || capability.parameters.Select(parameter => parameter.parameterId)
                        .Distinct(StringComparer.Ordinal).Count() != 4)
                || node.formulaCapabilities.Select(value => value.capabilityId)
                    .Distinct(StringComparer.Ordinal).Count() != node.formulaCapabilities.Count
                || unresolvedSelection && (!string.Equals(node.moduleSelectionId,
                        node.presentationId, StringComparison.Ordinal)
                    || node.moduleSelectionOffers.Any(value => value == null
                        || !IsCanonical(value.moduleId)
                        || !Enum.IsDefined(typeof(EvolutionModuleOfferPolarity),
                            value.polarity)
                        || !IsCanonical(value.semanticDescription))
                    || node.moduleSelectionOffers.Select(value => value.moduleId)
                        .Distinct(StringComparer.Ordinal).Count()
                        != node.moduleSelectionOffers.Count
                    || node.formulaCapabilities.Count != 0
                    || node.calculatedCost != 0 || node.positiveCost != 0
                    || node.drawbackCredit != 0 || !string.IsNullOrEmpty(node.drawbackId)
                    || !string.IsNullOrEmpty(node.mechanicalDescription))
                || node.evidenceIds == null || node.evidenceIds.Count == 0
                || node.evidenceIds.Any(id => !IsCanonical(id))
                || node.evidenceIds.Distinct(StringComparer.Ordinal).Count()
                    != node.evidenceIds.Count
                || !string.IsNullOrEmpty(node.effectId)
                || node.formulaVersion < DrawbackModuleSelectionFormulaVersion
                    && !string.IsNullOrEmpty(node.burdenEffectId)
                || node.formulaVersion >= DrawbackModuleSelectionFormulaVersion
                    && (!string.IsNullOrEmpty(node.burdenEffectId)
                        && (!IsCanonical(node.burdenEffectId)
                            || node.burdenPotencyMultiplier < 1f
                            || float.IsNaN(node.burdenPotencyMultiplier)
                            || float.IsInfinity(node.burdenPotencyMultiplier)))
                || (node.legalCandidateEffectIds?.Count ?? 0) != 0
                || node.selectedCandidateIndex != -1
                || node.evidenceIds.Any(id => state.formulaEvidence.All(value =>
                    !string.Equals(value.evidenceId, id, StringComparison.Ordinal))))
            {
                error = "Equipment formula node payload is invalid.";
                return false;
            }
            EquipmentEvolutionPresentationRequest request = state.presentationRequests
                .SingleOrDefault(value => string.Equals(
                    value.presentationId, node.presentationId, StringComparison.Ordinal));
            bool pending = node.presentationState is
                EquipmentEvolutionPresentationState.PresentationPending
                or EquipmentEvolutionPresentationState.ModuleSelectionPending
                or EquipmentEvolutionPresentationState.AwaitingNarrativeRetry;
            if (!Enum.IsDefined(typeof(EquipmentEvolutionPresentationState), node.presentationState)
                || node.presentationState == EquipmentEvolutionPresentationState.Legacy
                || node.presentationFailureCount < 0
                || node.presentationFailureCount > 5
                || pending != (request != null)
                || request != null && (!string.Equals(request.nodeId, node.nodeId, StringComparison.Ordinal)
                    || request.state != node.presentationState
                    || request.failureCount != node.presentationFailureCount
                    || node.historical != !string.IsNullOrEmpty(request.attunementOwnerPersistentId)
                    || node.generation != request.targetGeneration
                    || node.historical && request.masteryCost != 0f
                    || !node.historical && (request.masteryCost <= 0f
                        || request.targetGeneration != state.generation + 1
                        || state.mastery + 0.001f < request.masteryCost))
                || node.presentationState == EquipmentEvolutionPresentationState.Ready
                    && (!node.active || !node.mechanicallyUnlocked || !node.narrativeReady
                        || !node.uiVisible || !node.playerVisible
                        || !IsCanonical(node.displayName) || node.displayName.Length > 32
                        || !IsCanonical(node.narrativeFlavor) || node.narrativeFlavor.Length > 180
                        || !string.Equals(node.description, node.narrativeFlavor, StringComparison.Ordinal)
                        || ContainsMechanicalNumber(node.displayName)
                        || ContainsMechanicalNumber(node.narrativeFlavor))
                || pending && (node.active || node.mechanicallyUnlocked || node.narrativeReady
                    || node.uiVisible || node.playerVisible))
            {
                error = "Equipment formula presentation state is inconsistent.";
                return false;
            }
        }
        if (state.presentationRequests.Any(request => state.evolutionNodes.All(node => node == null
                || node.formulaVersion <= 0
                || !string.Equals(node.nodeId, request.nodeId, StringComparison.Ordinal))))
        {
            error = "Equipment presentation request has no frozen node.";
            return false;
        }
        return true;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsOptionalCanonical(string value) =>
        value != null && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsSha256(string value) =>
        IsCanonical(value)
        && value.Length == 64
        && string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal)
        && value.All(Uri.IsHexDigit);

    private static bool IsPresentationId(string value)
    {
        const string prefix = "presentation:equipment:";
        return IsCanonical(value)
            && value.StartsWith(prefix, StringComparison.Ordinal)
            && IsSha256(value.Substring(prefix.Length));
    }

    private static bool ContainsMechanicalNumber(string value) =>
        (value ?? string.Empty).Any(character =>
            char.IsDigit(character) || character is '%' or '％');
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class EquipmentPhysicalStatePayload
{
    public CombatEquipmentInstance equipment;
    public List<EquipmentModuleInstance> attachedModules = new();
}

public static class EquipmentModuleItemStateCodec
{
    public const int CurrentSchemaVersion = 2;
    private const string StateJsonKey = "state-json";

    public static ItemInstanceComponentSaveData Encode(
        EquipmentModuleInstance instance)
    {
        if (instance == null
            || !((ItemInstanceId)instance.instanceId).IsValid
            || string.IsNullOrWhiteSpace(instance.definitionId)
            || string.IsNullOrWhiteSpace(instance.sourceStackId)
            || !string.IsNullOrWhiteSpace(
                instance.attachedEquipmentInstanceId))
        {
            throw new ArgumentException(
                "A persistent unattached equipment-module instance with a physical stack is required.",
                nameof(instance));
        }

        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ItemInstanceComponentIds.EquipmentModule,
            schemaVersion = CurrentSchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new ItemStateValueSaveData
                {
                    key = StateJsonKey,
                    kind = ItemStateValueKind.String,
                    stringValue = JsonUtility.ToJson(instance.Clone())
                }
            }
        };
    }

    public static bool TryDecode(
        ItemInstanceComponentSaveData component,
        out EquipmentModuleInstance instance,
        out string error)
    {
        instance = null;
        if (component == null
            || !string.Equals(
                component.componentTypeId,
                ItemInstanceComponentIds.EquipmentModule,
                StringComparison.Ordinal))
        {
            error = "The item component is not equipment-module state.";
            return false;
        }
        if (component.schemaVersion != CurrentSchemaVersion)
        {
            error = $"Unsupported equipment-module item-state schema V{component.schemaVersion}.";
            return false;
        }

        string json = component.values?
            .FirstOrDefault(value => value != null
                && string.Equals(value.key, StateJsonKey, StringComparison.Ordinal)
                && value.kind == ItemStateValueKind.String)?
            .stringValue;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Equipment-module item-state has no state payload.";
            return false;
        }

        try
        {
            EquipmentModuleInstance restored =
                JsonUtility.FromJson<EquipmentModuleInstance>(json);
            string appraisalError = string.Empty;
            bool appraisalValid = restored != null
                && TryValidateAppraisalState(restored, out appraisalError);
            if (restored == null
                || !((ItemInstanceId)restored.instanceId).IsValid
                || string.IsNullOrWhiteSpace(restored.definitionId)
                || string.IsNullOrWhiteSpace(restored.sourceStackId)
                || !string.IsNullOrWhiteSpace(
                    restored.attachedEquipmentInstanceId)
                || !Enum.IsDefined(
                    typeof(EquipmentModuleProcessState),
                    restored.state)
                || restored.state is EquipmentModuleProcessState.Installed
                    or EquipmentModuleProcessState.Lost
                || !appraisalValid)
            {
                error = string.IsNullOrEmpty(appraisalError)
                    ? "Equipment-module item-state payload has invalid physical identity or state."
                    : appraisalError;
                return false;
            }

            instance = restored.Clone();
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = $"Equipment-module item-state payload is invalid: {exception.Message}";
            return false;
        }
    }

    public static bool TryValidateAppraisalState(
        EquipmentModuleInstance module,
        out string error)
    {
        error = string.Empty;
        if (module.nextAppraisalOperationSequence <= 0
            || module.pendingAppraisal == null)
        {
            error = "Equipment-module appraisal sequence or pending state is invalid.";
            return false;
        }

        EquipmentModuleAppraisalCommitSaveData pending = module.pendingAppraisal;
        EquipmentModuleAppraisalCommitPhase phase =
            (EquipmentModuleAppraisalCommitPhase)pending.phase;
        if (phase == EquipmentModuleAppraisalCommitPhase.None)
        {
            bool empty = pending.operationSequence == 0
                && IsEmpty(pending.operationId)
                && IsEmpty(pending.reasonCode)
                && IsEmpty(pending.moduleInstanceId)
                && IsEmpty(pending.destinationId)
                && IsEmpty(pending.couponStackId)
                && IsEmpty(pending.couponItemId)
                && pending.quantity == 0
                && !pending.moduleIdentifiedBefore
                && !pending.moduleIdentifiedAfter
                && pending.moduleStateBefore == EquipmentModuleProcessState.Unidentified
                && pending.moduleStateAfter == EquipmentModuleProcessState.Unidentified
                && IsEmpty(pending.gaugeStackId)
                && IsEmpty(pending.gaugeItemId)
                && Approximately(pending.gaugeDurabilityBefore, 0f)
                && Approximately(pending.gaugeDurabilityAfter, 0f)
                && IsEmpty(pending.lensStackId)
                && IsEmpty(pending.lensItemId)
                && Approximately(pending.lensDurabilityBefore, 0f)
                && Approximately(pending.lensDurabilityAfter, 0f)
                && (pending.sourceStackIds?.Count ?? 0) == 0
                && pending.inputMassGrams == 0L
                && IsEmpty(pending.commitId);
            if (!empty)
            {
                error = "Equipment-module empty appraisal state contains stale provenance.";
            }
            return empty;
        }

        bool common = phase is EquipmentModuleAppraisalCommitPhase.IntentRecorded
                or EquipmentModuleAppraisalCommitPhase.OutcomePublished
            && pending.operationSequence == module.nextAppraisalOperationSequence
            && pending.operationSequence > 0
            && IsCanonical(pending.operationId)
            && IsCanonical(pending.reasonCode)
            && string.Equals(
                pending.moduleInstanceId,
                module.instanceId,
                StringComparison.Ordinal)
            && IsCanonical(pending.destinationId)
            && IsCanonical(pending.couponStackId)
            && IsCanonical(pending.couponItemId)
            && pending.quantity == 1
            && !pending.moduleIdentifiedBefore
            && pending.moduleIdentifiedAfter
            && pending.moduleStateBefore == EquipmentModuleProcessState.Unidentified
            && pending.moduleStateAfter == EquipmentModuleProcessState.IdentifiedDamaged
            && IsCanonical(pending.gaugeStackId)
            && IsCanonical(pending.gaugeItemId)
            && IsCanonical(pending.lensStackId)
            && IsCanonical(pending.lensItemId)
            && !string.Equals(
                pending.gaugeStackId,
                pending.lensStackId,
                StringComparison.Ordinal)
            && IsFiniteNonNegative(pending.gaugeDurabilityBefore)
            && IsFiniteNonNegative(pending.gaugeDurabilityAfter)
            && pending.gaugeDurabilityAfter < pending.gaugeDurabilityBefore
            && IsFiniteNonNegative(pending.lensDurabilityBefore)
            && IsFiniteNonNegative(pending.lensDurabilityAfter)
            && pending.lensDurabilityAfter < pending.lensDurabilityBefore
            && pending.sourceStackIds != null;
        if (!common)
        {
            error = "Equipment-module appraisal contract is invalid.";
            return false;
        }

        if (phase == EquipmentModuleAppraisalCommitPhase.IntentRecorded)
        {
            bool validIntent = pending.sourceStackIds.Count == 0
                && pending.inputMassGrams == 0L
                && IsEmpty(pending.commitId);
            if (!validIntent)
            {
                error = "Equipment-module appraisal intent contains outcome provenance.";
            }
            return validIntent;
        }

        bool validOutcome = pending.sourceStackIds.Count > 0
            && pending.sourceStackIds.All(IsCanonical)
            && pending.sourceStackIds.SequenceEqual(
                pending.sourceStackIds.OrderBy(value => value, StringComparer.Ordinal))
            && pending.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == pending.sourceStackIds.Count
            && pending.inputMassGrams > 0L
            && IsCanonical(pending.commitId);
        if (!validOutcome)
        {
            error = "Equipment-module appraisal outcome provenance is invalid.";
        }
        return validOutcome;
    }

    private static bool IsEmpty(string value) => string.IsNullOrEmpty(value);

    private static bool IsCanonical(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsFiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;
}
