using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class SurgeryLogisticsRuntime
{
    private const float AdmissionRetryInterval = 1.5f;
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.left,
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.down
    };

    private readonly ISurgicalPartRuntime parts;
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly ICaptivityRuntime captivity;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IPhysicalItemMassQuery physicalMass;
    private readonly IPackagedLotTareDispositionService tareDispositions;
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ISurgicalPatientTransportRuntime patientTransport;
    private readonly ICharacterMedicalCommand medicalCommands;
    private readonly IWorkforceReplanService workforce;
    private readonly IGameClock clock;

    public SurgeryLogisticsRuntime(
        SurgeryContentServices content,
        SurgeryWorldServices world,
        SurgeryResourceServices resources,
        SurgeryExecutionServices execution)
    {
        parts = (content ?? throw new ArgumentNullException(nameof(content))).Parts;
        SurgeryWorldServices requiredWorld = world
            ?? throw new ArgumentNullException(nameof(world));
        characters = requiredWorld.Characters;
        wildlife = requiredWorld.Wildlife;
        captivity = requiredWorld.Captivity;
        bodyHealth = requiredWorld.BodyHealthQuery;
        patientTransport = requiredWorld.PatientTransport;
        medicalCommands = requiredWorld.MedicalCommands;
        SurgeryResourceServices requiredResources = resources
            ?? throw new ArgumentNullException(nameof(resources));
        items = requiredResources.Items;
        batchDispositions = requiredResources.BatchDispositions;
        physicalMass = requiredResources.PhysicalMass;
        tareDispositions = requiredResources.TareDispositions;
        destinationClaims = requiredResources.DestinationClaims;
        workforce = requiredResources.Workforce;
        clock = (execution ?? throw new ArgumentNullException(nameof(execution))).Clock;
    }

    public void PrepareAdmission(SurgeryOrder order, BuildableObject facility)
    {
        if (order.subject.kind != SurgicalSubjectKind.Character)
        {
            return;
        }

        CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        if (patient == null || patient.IsDead)
        {
            return;
        }

        if (bodyHealth.GetSnapshot(patient).Downed)
        {
            if (medicalCommands.TryRequestTreatment(
                    patient,
                    out CharacterMedicalOrder medicalOrder,
                    out _))
            {
                medicalCommands.TryAssignSpecificTreatmentFacility(
                    medicalOrder.orderId,
                    facility,
                    out _);
            }

            return;
        }

        order.subjectAiWasPaused = patient.IsAiPaused();
        patient.SetAiPaused(true);
        patient.Brain?.SetActionPhase(
            SurgeryStatusCode.PatientAdmissionWaiting.ToString(),
            facility);
    }

    public bool EnsureAdmission(SurgeryOrder order, BuildableObject facility)
    {
        if (order.subject.kind is SurgicalSubjectKind.HumanoidCorpse
            or SurgicalSubjectKind.WildlifeCorpse)
        {
            WorldItemStackSnapshot corpse = items.GetAllStacks().FirstOrDefault(stack =>
                stack != null
                && string.Equals(stack.StackId, order.subject.subjectId, StringComparison.Ordinal));
            bool ready = corpse != null
                && corpse.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    corpse.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal);
            order.statusData.Set(
                ready
                    ? SurgeryStatusCode.CorpseReady
                    : SurgeryStatusCode.CorpseTransportPending,
                order.subject.subjectId);
            return ready;
        }

        if (order.subject.kind == SurgicalSubjectKind.Wildlife)
        {
            WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
                wildlife,
                order.subject.subjectId);
            if (animal == null || !animal.IsAlive)
            {
                order.statusData.Set(
                    SurgeryStatusCode.WildlifePatientMissing,
                    order.subject.subjectId);
                return false;
            }

            if (ManhattanToFacility(animal.GridPosition, facility) <= 1)
            {
                order.patientAdmitted = true;
                order.statusData.Set(
                    SurgeryStatusCode.WildlifePatientReady,
                    order.subject.subjectId);
                return true;
            }

            if (!TryFindAdmissionCell(facility, animal.GridPosition, out Vector2Int admission))
            {
                order.statusData.Set(
                    SurgeryStatusCode.PatientAdmissionCellMissing,
                    order.facilityId);
                return false;
            }

            bool ready = patientTransport.EnsureWildlifeAdmission(
                order,
                animal,
                admission,
                out SurgeryStatusData transportStatus);
            order.statusData = transportStatus?.Clone()
                ?? new SurgeryStatusData
                {
                    code = ready
                        ? SurgeryStatusCode.WildlifePatientReady
                        : SurgeryStatusCode.WildlifePatientTransporting,
                    primaryId = order.subject.subjectId
                };
            return ready;
        }

        CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        if (patient == null || patient.IsDead)
        {
            order.statusData.Set(
                SurgeryStatusCode.PatientMissing,
                order.subject.subjectId);
            return false;
        }

        if (order.patientAdmitted)
        {
            return true;
        }

        if (ManhattanToFacility(patient.GetNowXY(), facility) <= 1
            || facility.ContainsGridPosition(patient.GetNowXY()))
        {
            order.patientAdmitted = true;
            patient.SetAiPaused(true);
            order.statusData.Set(
                SurgeryStatusCode.PatientAdmitted,
                order.subject.subjectId);
            patient.Brain?.SetActionPhase(
                order.statusData.code.ToString(),
                facility);
            return true;
        }

        if (bodyHealth.GetSnapshot(patient).Downed)
        {
            order.statusData.Set(
                SurgeryStatusCode.PatientTransportByRescuer,
                order.subject.subjectId);
            return false;
        }

        if (!order.subject.willing
            && (!captivity.TryGetCaptive(order.subject.subjectId, out CaptiveState captive)
                || !captive.restrained))
        {
            order.statusData.Set(
                SurgeryStatusCode.PatientRestraintRequired,
                order.subject.subjectId);
            return false;
        }

        AbilityMove move = patient.GetAbility<AbilityMove>();
        Vector2Int requestedAdmission = new(order.admissionX, order.admissionY);
        if (order.admissionMoveRequested)
        {
            if (move != null && move.IsSystemMoveInProgressTo(requestedAdmission))
            {
                order.statusData.Set(
                    SurgeryStatusCode.PatientMovingToSurgery,
                    order.subject.subjectId);
                return false;
            }

            order.admissionMoveRequested = false;
        }

        if (move != null && move.IsSystemMoveInProgress)
        {
            order.statusData.Set(
                SurgeryStatusCode.PatientCurrentMovePending,
                order.subject.subjectId);
            return false;
        }

        if (clock.Time < order.nextAdmissionRetryAt)
        {
            order.statusData.Set(
                SurgeryStatusCode.PatientMovingToSurgery,
                order.subject.subjectId);
            return false;
        }

        if (!TryFindAdmissionCell(facility, patient.GetNowXY(), out Vector2Int admissionCell))
        {
            order.statusData.Set(
                SurgeryStatusCode.PatientAdmissionCellMissing,
                order.facilityId);
            return false;
        }

        if (!order.admissionMoveRequested)
        {
            Vector2Int origin = patient.GetNowXY();
            order.patientOriginX = origin.x;
            order.patientOriginY = origin.y;
        }

        order.admissionX = admissionCell.x;
        order.admissionY = admissionCell.y;
        order.nextAdmissionRetryAt = clock.Time + AdmissionRetryInterval;
        order.admissionMoveRequested = move != null
            && move.TryStartSystemMove(
                admissionCell,
                order.subject.willing
                    ? DoorAccessOverrideKind.None
                    : DoorAccessOverrideKind.EscortPass,
                out _);
        order.statusData.Set(
            order.admissionMoveRequested
                ? SurgeryStatusCode.PatientMovingToSurgery
                : SurgeryStatusCode.PatientCurrentMovePending,
            order.subject.subjectId);
        return false;
    }

    public void RequestMissingMaterials(
        SurgeryOrder order,
        BuildableObject facility)
    {
        if (order == null || facility == null)
        {
            return;
        }

        bool deliveryCreated = false;
        foreach (SurgicalMaterialRequirement requirement in order.materials)
        {
            if (requirement == null || requirement.optional)
            {
                continue;
            }

            int missing = Mathf.Max(
                0,
                requirement.quantity - CountRoutedItem(order, requirement.itemId));
            if (missing <= 0)
            {
                continue;
            }

            bool created = items.TryRequestItemDelivery(
                requirement.itemId,
                missing,
                facility.centerPos,
                order.materialDestinationId,
                out int requested,
                out _);
            deliveryCreated |= created && requested > 0;
        }

        if (order.subject.kind is SurgicalSubjectKind.HumanoidCorpse
            or SurgicalSubjectKind.WildlifeCorpse)
        {
            WorldItemStackSnapshot corpse = items.GetAllStacks().FirstOrDefault(stack =>
                stack != null
                && string.Equals(stack.StackId, order.subject.subjectId, StringComparison.Ordinal));
            if (corpse != null
                && !string.Equals(
                    corpse.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal))
            {
                bool created = items.TryRequestStackDelivery(
                    corpse.StackId,
                    1,
                    facility.centerPos,
                    order.materialDestinationId,
                    out int requested,
                    out _);
                deliveryCreated |= created && requested > 0;
            }
        }

        if (!string.IsNullOrWhiteSpace(order.selectedPartInstanceId)
            && parts.TryGet(order.selectedPartInstanceId, out SurgicalPartInstance part)
            && !string.IsNullOrWhiteSpace(part.worldStackId))
        {
            bool created = items.TryRequestStackDelivery(
                part.worldStackId,
                1,
                facility.centerPos,
                order.materialDestinationId,
                out int requested,
                out _);
            deliveryCreated |= created && requested > 0;
        }

        if (deliveryCreated)
        {
            foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                         .Where(stack => stack != null
                             && string.Equals(
                                 stack.DestinationId,
                                 order.materialDestinationId,
                                 StringComparison.Ordinal)
                             && stack.State is WorldItemStackState.Loose
                                 or WorldItemStackState.Stored))
            {
                items.PrioritizeHaul(stack.StackId);
            }

            workforce.RequestOneHaulerToReplan(forceInterrupt: true);
        }

        order.materialsRequested = true;
    }

    public bool AreRequiredMaterialsReady(SurgeryOrder order)
    {
        foreach (SurgicalMaterialRequirement requirement in order.materials)
        {
            if (requirement == null || requirement.optional)
            {
                continue;
            }

            int buffered = items.GetAllStacks()
                .Where(stack => stack != null
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(
                        stack.DestinationId,
                        order.materialDestinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        requirement.itemId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            if (buffered < requirement.quantity)
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(order.selectedPartInstanceId)
            || !parts.TryGet(order.selectedPartInstanceId, out SurgicalPartInstance selected))
        {
            return true;
        }

        WorldItemStackSnapshot selectedStack = items.GetAllStacks().FirstOrDefault(
            candidate => candidate != null
                && string.Equals(
                    candidate.StackId,
                    selected.worldStackId,
                    StringComparison.Ordinal));
        return selectedStack != null
            && selectedStack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(
                selectedStack.DestinationId,
                order.materialDestinationId,
                StringComparison.Ordinal);
    }

    public bool TryConsumeMaterials(SurgeryOrder order, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        Dictionary<string, int> costs = order.materials
            .Where(requirement => requirement != null && !requirement.optional)
            .GroupBy(requirement => requirement.itemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => Mathf.Max(1, item.quantity)),
                StringComparer.Ordinal);
        if (costs.Count > 0
            && !TryCommitMaterialSinkWithTare(
                order,
                costs,
                out _))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryMaterialUnavailable,
                order.materialDestinationId);
            return false;
        }

        order.materialsConsumed = true;
        order.anesthesiaConsumed = order.materials.Any(requirement =>
            requirement != null
            && string.Equals(
                requirement.itemId,
                SurgeryItemDefinitions.AnestheticId,
                StringComparison.Ordinal));
        return true;
    }

    public bool TryFinalizeConsumedMaterials(
        SurgeryOrder order,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order == null || !order.materialsConsumed)
        {
            return true;
        }

        string operationId =
            SurgeryMaterialSinkIdentity.FormatOperationId(order.orderId);
        if (order.materialSinkAcknowledged)
        {
            if (batchDispositions.TryGetPending(operationId, out _))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryMaterialUnavailable,
                    "surgery-material-sink-acknowledged-but-pending");
                return false;
            }
            return true;
        }
        if (!batchDispositions.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt pending))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryMaterialUnavailable,
                "surgery-material-sink-receipt-missing");
            return false;
        }
        if (!string.Equals(
                pending.CommitId,
                order.materialSinkCommitId,
                StringComparison.Ordinal)
            || pending.InputMassGrams != order.materialSinkInputMassGrams)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryMaterialUnavailable,
                "surgery-material-sink-receipt-join-mismatch");
            return false;
        }
        if (!batchDispositions.Acknowledge(
                pending.CommitId,
                out string acknowledgementFailure))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryMaterialUnavailable,
                acknowledgementFailure);
            return false;
        }
        order.materialSinkAcknowledged = true;
        return true;
    }

    private bool TryCommitMaterialSinkWithTare(
        SurgeryOrder order,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason)
    {
        failureReason = string.Empty;
        string operationId =
            SurgeryMaterialSinkIdentity.FormatOperationId(order.orderId);
        PhysicalItemBatchDispositionReceipt receipt;
        if (!batchDispositions.TryGetPending(operationId, out receipt))
        {
            if (!TrySelectExactMaterialInputs(
                    order.materialDestinationId,
                    costs,
                    out PhysicalItemTransformInput[] inputs,
                    out failureReason)
                || !batchDispositions.TryCommitPending(
                    inputs,
                    PhysicalItemDispositionKind.Sink,
                    operationId,
                    "surgery-materials-consumed",
                    out receipt,
                    out failureReason))
            {
                return false;
            }
        }

        long expectedMass = 0L;
        int expectedQuantity = 0;
        foreach (KeyValuePair<string, int> cost in costs)
        {
            expectedQuantity = checked(expectedQuantity + cost.Value);
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                physicalMass,
                (ItemDefinitionId)cost.Key,
                string.Empty,
                Array.Empty<ItemInstanceComponentSaveData>());
            expectedMass = checked(expectedMass
                + physicalMass.GetQuantityMass(
                    (ItemDefinitionId)cost.Key,
                    subject,
                    cost.Value).Value);
        }
        if (!receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || receipt.Quantity != expectedQuantity
            || receipt.InputMassGrams != expectedMass)
        {
            failureReason = "surgery-material-disposition-receipt-mismatch";
            return false;
        }
        if (!SurgeryMaterialDestinationAuthority.TryGetOwnedClaim(
                destinationClaims,
                order,
                out FacilityBufferDestinationClaim claim))
        {
            failureReason = "surgery-material-destination-claim-missing";
            return false;
        }
        if (!tareDispositions.EnsureTerminalSinkOutputs(
                costs,
                claim.DropPosition,
                receipt.CommitId,
                out _,
                out failureReason))
        {
            return false;
        }
        if ((!string.IsNullOrEmpty(order.materialSinkOperationId)
                && !string.Equals(
                    order.materialSinkOperationId,
                    operationId,
                    StringComparison.Ordinal))
            || (!string.IsNullOrEmpty(order.materialSinkCommitId)
                && !string.Equals(
                    order.materialSinkCommitId,
                    receipt.CommitId,
                    StringComparison.Ordinal))
            || order.materialSinkInputMassGrams != 0L
                && order.materialSinkInputMassGrams != receipt.InputMassGrams)
        {
            failureReason = "surgery-material-sink-order-join-mismatch";
            return false;
        }
        order.materialSinkOperationId = operationId;
        order.materialSinkCommitId = receipt.CommitId;
        order.materialSinkInputMassGrams = receipt.InputMassGrams;
        order.materialSinkAcknowledged = false;
        return true;
    }

    private bool TrySelectExactMaterialInputs(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out PhysicalItemTransformInput[] inputs,
        out string failureReason)
    {
        List<PhysicalItemTransformInput> selected = new();
        foreach (KeyValuePair<string, int> cost in costs
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            int remaining = cost.Value;
            foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                         .Where(candidate => candidate != null
                             && candidate.State == WorldItemStackState.FacilityBuffer
                             && candidate.ReservedQuantity == 0
                             && string.Equals(
                                 candidate.DestinationId,
                                 destinationId,
                                 StringComparison.Ordinal)
                             && string.Equals(
                                 candidate.ItemId,
                                 cost.Key,
                                 StringComparison.Ordinal))
                         .OrderBy(candidate => candidate.StackId, StringComparer.Ordinal))
            {
                if (remaining <= 0)
                    break;
                int take = Mathf.Min(remaining, stack.AvailableQuantity);
                if (take <= 0)
                    continue;
                selected.Add(new PhysicalItemTransformInput(stack.StackId, take));
                remaining -= take;
            }
            if (remaining > 0)
            {
                inputs = Array.Empty<PhysicalItemTransformInput>();
                failureReason = $"facility item missing: {cost.Key}";
                return false;
            }
        }

        inputs = selected.ToArray();
        failureReason = string.Empty;
        return inputs.Length > 0;
    }

    private int CountRoutedItem(SurgeryOrder order, string itemId)
    {
        int worldQuantity = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        int carriedQuantity = characters.Characters
            .Where(actor => actor != null)
            .Select(actor => actor.GetComponent<AbilityHaul>())
            .Where(haul => haul != null)
            .Sum(haul => haul.GetInTransitQuantity(order.materialDestinationId, itemId));
        return worldQuantity + carriedQuantity;
    }

    private static bool TryFindAdmissionCell(
        BuildableObject facility,
        Vector2Int origin,
        out Vector2Int admission)
    {
        admission = default;
        if (facility?.Grid == null)
        {
            return false;
        }

        List<Vector2Int> candidates = new();
        foreach (Vector2Int occupied in facility.buildPoses)
        {
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int cell = occupied + direction;
                if (!facility.ContainsGridPosition(cell)
                    && facility.Grid.IsValidGridPos(cell)
                    && facility.Grid.IsWalkable(cell)
                    && !candidates.Contains(cell))
                {
                    candidates.Add(cell);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        admission = candidates
            .OrderBy(cell => Mathf.Abs(cell.x - origin.x) + Mathf.Abs(cell.y - origin.y))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .First();
        return true;
    }

    private static int ManhattanToFacility(
        Vector2Int position,
        BuildableObject facility)
    {
        return facility?.buildPoses?
            .Select(cell => Mathf.Abs(cell.x - position.x) + Mathf.Abs(cell.y - position.y))
            .DefaultIfEmpty(int.MaxValue)
            .Min() ?? int.MaxValue;
    }
}
