using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class WildlifeHaulCargoSaveValidation :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator
{
    public void Validate(
        DungeonGameSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (saveData == null) throw new ArgumentNullException(nameof(saveData));
        ValidateCore(
            ReadRequired<DungeonPhysicalItemSaveData>(
                saveData.sections,
                PhysicalItemsSaveSection.Id,
                DungeonPhysicalItemSaveData.CurrentVersion),
            ReadRequired<CircusSaveData>(
                saveData.sections,
                CircusSaveSection.Id,
                CircusSaveData.CurrentVersion),
            report);
    }

    public void Validate(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report)
    {
        if (envelopes == null) throw new ArgumentNullException(nameof(envelopes));
        ValidateCore(
            ReadRequired<DungeonPhysicalItemSaveData>(
                envelopes,
                PhysicalItemsSaveSection.Id,
                DungeonPhysicalItemSaveData.CurrentVersion),
            ReadRequired<CircusSaveData>(
                envelopes,
                CircusSaveSection.Id,
                CircusSaveData.CurrentVersion),
            report);
    }

    internal static void ValidateCore(
        DungeonPhysicalItemSaveData physical,
        CircusSaveData circus,
        DungeonGameRestoreReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (physical?.stacks == null || circus?.capturedWildlife == null)
        {
            report.AddError(
                "Wildlife haul custody requires physical and circus collections.");
            return;
        }

        Dictionary<string, WildlifeHaulAssignmentSnapshot> roles =
            new(StringComparer.Ordinal);
        foreach (CapturedWildlifeState state in circus.capturedWildlife
                     .Where(value => value != null))
        {
            if (!CapturedWildlifeCapabilityStateCodec.TryRead(
                    state.capabilityState,
                    out CapturedWildlifeRoleId roleId,
                    out _)
                || !roleId.Equals(CapturedWildlifeRoleIds.Haul))
            {
                continue;
            }
            if (!CapturedWildlifeCapabilityStateCodec.TryReadHaul(
                    state.capabilityState,
                    out CapturedWildlifeHaulPayloadSnapshot decoded,
                    out string roleFailure)
                || !roles.TryAdd(
                    state.wildlifeId,
                    WildlifeHaulAssignmentSnapshot.FromPayload(
                        decoded,
                        state.wildlifeId,
                        state.penPosition)))
            {
                report.AddError(
                    $"Wildlife haul role '{state.wildlifeId}' is invalid or duplicated: {roleFailure}");
            }
        }

        Dictionary<string, (WorldItemStackSaveData stack,
            WildlifeHaulCargoCustody custody)> cargoByOperation =
            new(StringComparer.Ordinal);
        HashSet<string> cargoWildlifeIds = new(StringComparer.Ordinal);
        foreach (WorldItemStackSaveData stack in physical.stacks
                     .Where(value => value != null
                         && WildlifeHaulCargoCustodyCodec.HasAny(value.components)))
        {
            if (!WildlifeHaulCargoCustodyCodec.TryRead(
                    stack.components,
                    out WildlifeHaulCargoCustody custody)
                || stack.state != WorldItemStackState.InTransit
                || !string.Equals(stack.destinationId, custody.OperationId,
                    StringComparison.Ordinal)
                || !HaulDeliveryOperationIdentity.TryParse(
                    custody.OperationId,
                    custody.WildlifeId,
                    out long cargoSequence)
                || cargoSequence >= physical.nextHaulOperationSequence
                || stack.quantity != custody.Quantity
                || !string.Equals(stack.itemId, custody.ItemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    ItemStackSignature.Create(stack.itemId, stack.components),
                    custody.ExpectedStackSignature,
                    StringComparison.Ordinal)
                || !cargoByOperation.TryAdd(custody.OperationId, (stack, custody))
                || !cargoWildlifeIds.Add(custody.WildlifeId))
            {
                report.AddError(
                    $"Physical wildlife-haul cargo '{stack.stackId}' has invalid or duplicate custody.");
                continue;
            }

            if (custody.Phase == WildlifeHaulCargoCustodyPhase.RecoveryPending)
            {
                if (stack.gridX != custody.InterruptionPosition.x
                    || stack.gridY != custody.InterruptionPosition.y)
                {
                    report.AddError(
                        $"Recovery-pending wildlife cargo '{stack.stackId}' is not at its interruption cell.");
                }
                if (roles.ContainsKey(custody.WildlifeId))
                {
                    report.AddError(
                        $"Recovery-pending wildlife cargo '{stack.stackId}' is dual-owned by Circus.");
                }
                continue;
            }

            if (!roles.TryGetValue(
                    custody.WildlifeId,
                    out WildlifeHaulAssignmentSnapshot role)
                || role.Phase is not (CapturedWildlifeHaulPhase.CargoOwned
                    or CapturedWildlifeHaulPhase.ReleasePending)
                || !string.Equals(role.CargoStackId, stack.stackId,
                    StringComparison.Ordinal)
                || !WildlifeHaulItemRuntime.Matches(role, custody))
            {
                report.AddError(
                    $"Cargo-owned wildlife haul lot '{stack.stackId}' has no exact Circus role owner.");
            }
        }

        foreach (WildlifeHaulAssignmentSnapshot role in roles.Values)
        {
            if (role.OperationId.Length > 0
                && (!HaulDeliveryOperationIdentity.TryParse(
                        role.OperationId,
                        role.WildlifeId,
                        out long roleSequence)
                    || roleSequence >= physical.nextHaulOperationSequence))
            {
                report.AddError(
                    $"Circus wildlife haul role '{role.WildlifeId}' has an unallocated or future operation identity.");
            }
            bool expectsCargo = role.Phase is CapturedWildlifeHaulPhase.CargoOwned
                or CapturedWildlifeHaulPhase.ReleasePending;
            bool hasCargo = role.OperationId.Length > 0
                && cargoByOperation.TryGetValue(
                    role.OperationId,
                    out var owned)
                && owned.custody.Phase == WildlifeHaulCargoCustodyPhase.CargoOwned
                && string.Equals(owned.stack.stackId, role.CargoStackId,
                    StringComparison.Ordinal)
                && WildlifeHaulItemRuntime.Matches(role, owned.custody);
            if (expectsCargo != hasCargo)
            {
                report.AddError(
                    $"Circus wildlife haul role '{role.WildlifeId}' has an orphan or unexpected cargo join.");
            }
        }
    }

    private static T ReadRequired<T>(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        int expectedVersion)
        where T : class
    {
        DungeonSaveSectionEnvelope envelope = (envelopes
                ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .SingleOrDefault(value => value != null
                && string.Equals(value.sectionId, sectionId,
                    StringComparison.Ordinal));
        return Parse<T>(envelope, sectionId, expectedVersion);
    }

    private static T ReadRequired<T>(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        int expectedVersion)
        where T : class =>
        envelopes.TryGetValue(sectionId, out DungeonSaveSectionEnvelope envelope)
            ? Parse<T>(envelope, sectionId, expectedVersion)
            : throw new InvalidOperationException(
                $"Required save section '{sectionId}' is missing.");

    private static T Parse<T>(
        DungeonSaveSectionEnvelope envelope,
        string sectionId,
        int expectedVersion)
        where T : class
    {
        if (envelope == null
            || envelope.sectionVersion != expectedVersion
            || string.IsNullOrEmpty(envelope.payloadJson))
        {
            throw new InvalidOperationException(
                $"Save section '{sectionId}' is missing or has an invalid version.");
        }
        return JsonUtility.FromJson<T>(envelope.payloadJson)
            ?? throw new InvalidOperationException(
                $"Save section '{sectionId}' payload is invalid.");
    }
}
