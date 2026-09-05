using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DurableFacilityEquipmentUseRuntime :
    IDurableFacilityEquipmentUseCommand
{
    private readonly IDurableFacilityEquipmentSlotQuery slots;
    private readonly IDurableFacilityEquipmentSlotCommand slotCommands;
    private readonly IDurableFacilityEquipmentPhysicalPort physical;
    private readonly IDurableFacilityEquipmentComponentMutationPort mutations;
    private readonly IDurableFacilityEquipmentUsabilityQuery usability;
    private readonly IDurableFacilityEquipmentWearQuery wear;

    public DurableFacilityEquipmentUseRuntime(
        IDurableFacilityEquipmentSlotQuery slots,
        IDurableFacilityEquipmentSlotCommand slotCommands,
        IDurableFacilityEquipmentPhysicalPort physical,
        IDurableFacilityEquipmentComponentMutationPort mutations,
        IDurableFacilityEquipmentUsabilityQuery usability,
        IDurableFacilityEquipmentWearQuery wear)
    {
        this.slots = slots ?? throw new ArgumentNullException(nameof(slots));
        this.slotCommands = slotCommands
            ?? throw new ArgumentNullException(nameof(slotCommands));
        this.physical = physical
            ?? throw new ArgumentNullException(nameof(physical));
        this.mutations = mutations
            ?? throw new ArgumentNullException(nameof(mutations));
        this.usability = usability
            ?? throw new ArgumentNullException(nameof(usability));
        this.wear = wear ?? throw new ArgumentNullException(nameof(wear));
    }

    public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
        DurableFacilityEquipmentSlotKey key,
        string requirementId,
        double wearAmount,
        IDurableFacilityEquipmentEffectCommit effect)
    {
        if (!key.IsValid
            || !Canonical(requirementId)
            || double.IsNaN(wearAmount)
            || double.IsInfinity(wearAmount)
            || wearAmount <= 0d
            || effect == null
            || !Canonical(effect.EffectKind))
        {
            return Failure(
                DurableFacilityEquipmentUseStatus.Conflict,
                RequireSlotOrSyntheticFailure(key),
                "durable-equipment-use-input-invalid");
        }
        if (!slots.TryCapture(key, out DurableFacilityEquipmentSlotSnapshot slot))
        {
            throw new InvalidOperationException(
                "Durable equipment use requires an existing slot: " + key);
        }
        if (slot.LifecyclePhase !=
            DurableFacilityEquipmentSlotLifecyclePhase.Active)
        {
            return Failure(
                DurableFacilityEquipmentUseStatus.Deferred,
                slot,
                "durable-equipment-use-slot-draining");
        }

        DurableFacilityEquipmentRequirement[] requirements = slot.Assignment
            .Requirements.Where(value => string.Equals(
                value.RequirementId,
                requirementId,
                StringComparison.Ordinal))
            .ToArray();
        if (requirements.Length != 1)
        {
            return Failure(
                DurableFacilityEquipmentUseStatus.Conflict,
                slot,
                "durable-equipment-use-requirement-missing-or-duplicate");
        }
        DurableFacilityEquipmentRequirement requirement = requirements[0];
        WorldItemStackSnapshot[] candidates = (physical
                .CaptureDestinationStacks(slot.DestinationId)
                ?? Array.Empty<WorldItemStackSnapshot>())
            .Where(value => value != null
                && value.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    value.ItemId,
                    requirement.ItemId.Value,
                    StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();

        List<(WorldItemStackSnapshot Stack,
            DurableFacilityEquipmentUseSubject Subject)> usable = new();
        bool exhausted = false;
        foreach (WorldItemStackSnapshot candidate in candidates)
        {
            DurableFacilityEquipmentUseSubject subject =
                DurableFacilityEquipmentUseSubjectCapture.Capture(candidate);
            if (!usability.TryEvaluate(
                    slot.UsabilityPolicyKind,
                    requirement,
                    subject,
                    out DurableFacilityEquipmentUsabilityResult disposition,
                    out string usabilityFailure))
            {
                return Failure(
                    DurableFacilityEquipmentUseStatus.Conflict,
                    slot,
                    Canonical(usabilityFailure)
                        ? usabilityFailure
                        : "durable-equipment-use-usability-failed");
            }
            if (disposition.Disposition ==
                DurableFacilityEquipmentUsabilityDisposition.Incompatible)
            {
                return Failure(
                    DurableFacilityEquipmentUseStatus.Conflict,
                    slot,
                    "durable-equipment-use-incompatible-buffered-item");
            }
            if (disposition.IsUsable)
                usable.Add((candidate, subject));
            else
                exhausted = true;
        }

        if (usable.Count == 0)
        {
            if (exhausted)
                slotCommands.TryClose(key, "equipment-exhausted");
            return Failure(
                DurableFacilityEquipmentUseStatus.Unavailable,
                CaptureLatest(key, slot),
                exhausted
                    ? "durable-equipment-use-exhausted"
                    : "durable-equipment-use-not-ready");
        }

        WorldItemStackSnapshot selected = usable[0].Stack;
        DurableFacilityEquipmentUseSubject before = usable[0].Subject;
        ItemInstanceComponentSaveData[] originalComponents =
            (selected.Components ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.Durability,
                    StringComparison.Ordinal))
            .ToArray();
        if (originalComponents.Length != 1)
        {
            return Failure(
                DurableFacilityEquipmentUseStatus.Conflict,
                slot,
                "durable-equipment-use-durability-component-invalid");
        }
        ItemInstanceComponentSaveData original = originalComponents[0].Clone();

        if (!wear.TryProject(
                slot.UsabilityPolicyKind,
                requirement,
                before,
                wearAmount,
                out DurableFacilityEquipmentWearProjection projection,
                out string wearFailure))
        {
            return Failure(
                DurableFacilityEquipmentUseStatus.Conflict,
                slot,
                Canonical(wearFailure)
                    ? wearFailure
                    : "durable-equipment-use-wear-projection-failed");
        }
        if (!effect.TryPreflight(
                slot,
                requirement,
                before,
                wearAmount,
                out string preflightFailure))
        {
            return Failure(
                DurableFacilityEquipmentUseStatus.Deferred,
                slot,
                Canonical(preflightFailure)
                    ? preflightFailure
                    : "durable-equipment-effect-preflight-failed");
        }
        if (!mutations.TryReplaceComponentExact(
                selected.StackId,
                before.ContentRevision,
                projection.ReplacementComponent,
                out WorldItemStackSnapshot afterStack,
                out string mutationFailure))
        {
            return Failure(
                DurableFacilityEquipmentUseStatus.Deferred,
                slot,
                Canonical(mutationFailure)
                    ? mutationFailure
                    : "durable-equipment-use-wear-mutation-failed");
        }

        DurableFacilityEquipmentUseSubject after =
            DurableFacilityEquipmentUseSubjectCapture.Capture(afterStack);
        ItemInstanceComponentSaveData actual = (afterStack.Components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    projection.ReplacementComponent.componentTypeId,
                    StringComparison.Ordinal));
        if (actual == null
            || !string.Equals(
                actual.ToCanonicalString(),
                projection.ReplacementComponent.ToCanonicalString(),
                StringComparison.Ordinal))
        {
            RollbackOrThrow(
                selected.StackId,
                projection.ReplacementComponent,
                original);
            return Failure(
                DurableFacilityEquipmentUseStatus.Conflict,
                slot,
                "durable-equipment-use-wear-publication-mismatch");
        }

        DurableFacilityEquipmentUseContext context = new(
            slot,
            requirement,
            before,
            after,
            wearAmount);
        bool effectCommitted;
        string effectFailure;
        try
        {
            effectCommitted = effect.TryCommit(context, out effectFailure);
        }
        catch (Exception exception)
        {
            RollbackOrThrow(
                selected.StackId,
                projection.ReplacementComponent,
                original);
            return Failure(
                DurableFacilityEquipmentUseStatus.Conflict,
                slot,
                "durable-equipment-effect-threw:"
                + exception.GetType().Name);
        }
        if (!effectCommitted)
        {
            RollbackOrThrow(
                selected.StackId,
                projection.ReplacementComponent,
                original);
            return Failure(
                DurableFacilityEquipmentUseStatus.Deferred,
                slot,
                Canonical(effectFailure)
                    ? effectFailure
                    : "durable-equipment-effect-commit-failed");
        }

        if (projection.ExhaustedAfter)
        {
            DurableFacilityEquipmentSlotResult close = slotCommands.TryClose(
                key,
                "equipment-exhausted");
            return Applied(
                DurableFacilityEquipmentUseStatus.AppliedDrainPending,
                close.Snapshot ?? CaptureLatest(key, slot),
                selected.StackId);
        }
        return Applied(
            DurableFacilityEquipmentUseStatus.Applied,
            CaptureLatest(key, slot),
            selected.StackId);
    }

    private void RollbackOrThrow(
        string stackId,
        ItemInstanceComponentSaveData expectedCurrent,
        ItemInstanceComponentSaveData original)
    {
        if (!mutations.TryRestoreComponentExact(
                stackId,
                expectedCurrent,
                original,
                out _,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Durable equipment wear rollback failed: " + failureReason);
        }
    }

    private DurableFacilityEquipmentSlotSnapshot CaptureLatest(
        DurableFacilityEquipmentSlotKey key,
        DurableFacilityEquipmentSlotSnapshot fallback) =>
        slots.TryCapture(key, out DurableFacilityEquipmentSlotSnapshot latest)
            ? latest
            : fallback;

    private DurableFacilityEquipmentSlotSnapshot RequireSlotOrSyntheticFailure(
        DurableFacilityEquipmentSlotKey key)
    {
        if (key.IsValid
            && slots.TryCapture(key, out DurableFacilityEquipmentSlotSnapshot slot))
        {
            return slot;
        }
        throw new ArgumentException(
            "Durable equipment use input is invalid and has no slot context.");
    }

    private static DurableFacilityEquipmentUseResult Applied(
        DurableFacilityEquipmentUseStatus status,
        DurableFacilityEquipmentSlotSnapshot slot,
        string stackId) => new(status, slot, stackId, string.Empty);

    private static DurableFacilityEquipmentUseResult Failure(
        DurableFacilityEquipmentUseStatus status,
        DurableFacilityEquipmentSlotSnapshot slot,
        string reason) => new(status, slot, string.Empty, reason);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
