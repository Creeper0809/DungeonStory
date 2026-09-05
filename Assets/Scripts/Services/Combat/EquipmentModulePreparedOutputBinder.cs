using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Frozen, pre-publication representation of a detached equipment module. The
/// physical stack ID is deliberately absent until the common publication service
/// allocates it and invokes the registered binder.
/// </summary>
public static class EquipmentModulePreparedOutputCodec
{
    public const string CapabilityId = "combat.equipment-module-stack-binding@1";
    public const string ComponentTypeId = "combat.equipment-module-prepared";
    public const int SchemaVersion = 1;
    private const string StateJsonKey = "state-json";

    public static ItemInstanceComponentSaveData Encode(
        EquipmentModuleInstance desired)
    {
        EquipmentModuleInstance canonical = RequireDesired(desired);
        return new ItemInstanceComponentSaveData
        {
            componentTypeId = ComponentTypeId,
            schemaVersion = SchemaVersion,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>
            {
                new()
                {
                    key = StateJsonKey,
                    kind = ItemStateValueKind.String,
                    stringValue = JsonUtility.ToJson(canonical)
                }
            }
        };
    }

    public static bool TryDecode(
        ItemInstanceComponentSaveData component,
        out EquipmentModuleInstance desired,
        out string failureReason)
    {
        desired = null;
        failureReason = string.Empty;
        if (component == null
            || !string.Equals(
                component.componentTypeId,
                ComponentTypeId,
                StringComparison.Ordinal)
            || component.schemaVersion != SchemaVersion
            || !component.affectsStacking)
        {
            failureReason = "equipment-module-prepared-component-invalid";
            return false;
        }
        string json = component.values?
            .SingleOrDefault(value => value != null
                && string.Equals(value.key, StateJsonKey, StringComparison.Ordinal)
                && value.kind == ItemStateValueKind.String)?
            .stringValue;
        if (string.IsNullOrWhiteSpace(json))
        {
            failureReason = "equipment-module-prepared-payload-missing";
            return false;
        }
        try
        {
            desired = RequireDesired(
                JsonUtility.FromJson<EquipmentModuleInstance>(json));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException)
        {
            desired = null;
            failureReason = "equipment-module-prepared-payload-invalid:"
                + exception.Message;
            return false;
        }
    }

    public static string CreateFingerprint(
        ItemInstanceComponentSaveData component)
    {
        if (!TryDecode(component, out _, out string failureReason))
            throw new ArgumentException(failureReason, nameof(component));
        string canonical = CapabilityId + "|" + component.ToCanonicalString();
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(
                sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static EquipmentModuleInstance RequireDesired(
        EquipmentModuleInstance source)
    {
        EquipmentModuleInstance desired = source?.Clone()
            ?? throw new ArgumentNullException(nameof(source));
        string appraisalFailure = string.Empty;
        if (!((ItemInstanceId)desired.instanceId).IsValid
            || string.IsNullOrWhiteSpace(desired.definitionId)
            || !string.Equals(
                desired.definitionId,
                desired.definitionId.Trim(),
                StringComparison.Ordinal)
            || !string.IsNullOrEmpty(desired.sourceStackId)
            || !string.IsNullOrEmpty(desired.attachedEquipmentInstanceId)
            || desired.state is EquipmentModuleProcessState.Installed
                or EquipmentModuleProcessState.Lost
            || !EquipmentModuleItemStateCodec.TryValidateAppraisalState(
                desired,
                out appraisalFailure)
            || (EquipmentModuleAppraisalCommitPhase)desired.pendingAppraisal.phase
                != EquipmentModuleAppraisalCommitPhase.None)
        {
            throw new ArgumentException(
                "A canonical detached module without physical stack identity is required: "
                + appraisalFailure,
                nameof(source));
        }
        return desired;
    }
}

public sealed class EquipmentModulePreparedOutputBinder :
    IFacilityBufferPlannedUniqueOutputBinder
{
    private readonly IItemInstanceRepository aggregates;

    public EquipmentModulePreparedOutputBinder(
        IItemInstanceRepository aggregates)
    {
        this.aggregates = aggregates
            ?? throw new ArgumentNullException(nameof(aggregates));
    }

    public string CapabilityId => EquipmentModulePreparedOutputCodec.CapabilityId;

    public bool TryBind(
        FacilityBufferPlannedOutputSliceSnapshot line,
        string allocatedStackId,
        out IReadOnlyList<ItemInstanceComponentSaveData> boundComponents,
        out string failureReason)
    {
        boundComponents = Array.Empty<ItemInstanceComponentSaveData>();
        failureReason = string.Empty;
        ItemInstanceComponentSaveData[] prepared = line.Source
            .MaterializeRuntimeComponents()
            .Where(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    EquipmentModulePreparedOutputCodec.ComponentTypeId,
                    StringComparison.Ordinal))
            .ToArray();
        if (prepared.Length != 1
            || line.Source.RuntimeComponents.Count != 1
            || !EquipmentModulePreparedOutputCodec.TryDecode(
                prepared[0],
                out EquipmentModuleInstance desired,
                out failureReason)
            || !string.Equals(
                desired.instanceId,
                line.Source.Subject.ItemInstanceId,
                StringComparison.Ordinal))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "equipment-module-prepared-line-invalid"
                : failureReason;
            return false;
        }

        if (string.IsNullOrEmpty(allocatedStackId))
        {
            boundComponents = Array.AsReadOnly(new[] { prepared[0].Clone() });
            return true;
        }
        if (!string.Equals(
                allocatedStackId,
                allocatedStackId.Trim(),
                StringComparison.Ordinal))
        {
            failureReason = "equipment-module-allocated-stack-id-noncanonical";
            return false;
        }
        desired.sourceStackId = allocatedStackId;
        boundComponents = Array.AsReadOnly(new[]
        {
            EquipmentModuleItemStateCodec.Encode(desired)
        });
        return true;
    }

    public bool CanValidate(
        IReadOnlyList<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Count(value => value != null
            && string.Equals(
                value.componentTypeId,
                ItemInstanceComponentIds.EquipmentModule,
                StringComparison.Ordinal)) == 1;

    public bool MatchesPrepared(
        FacilityBufferPlannedOutputSliceSnapshot line,
        FacilityBufferPublishedUniqueOutputSnapshot output,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (output == null
            || line.Quantity != 1
            || output.Quantity != 1
            || !string.Equals(
                output.ItemDefinitionId.Value,
                line.ItemDefinitionId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                output.ItemInstanceId,
                line.Source.Subject.ItemInstanceId,
                StringComparison.Ordinal)
            || !TryBind(
                line,
                output.StackId,
                out IReadOnlyList<ItemInstanceComponentSaveData> expected,
                out failureReason))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "equipment-module-prepared-record-identity-mismatch"
                : failureReason;
            return false;
        }
        ItemInstanceComponentSaveData actual = output.Components
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.EquipmentModule,
                    StringComparison.Ordinal));
        bool matches = actual != null
            && string.Equals(
                actual.ToCanonicalString(),
                expected.Single().ToCanonicalString(),
                StringComparison.Ordinal);
        failureReason = matches
            ? string.Empty
            : "equipment-module-prepared-record-component-mismatch";
        return matches;
    }

    public bool MatchesCommitted(
        FacilityBufferPublishedUniqueOutputSnapshot output,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (output == null
            || output.Quantity != 1
            || string.IsNullOrWhiteSpace(output.StackId)
            || string.IsNullOrWhiteSpace(output.ItemInstanceId)
            || !aggregates.EquipmentModules.TryGetValue(
                output.ItemInstanceId,
                out EquipmentModuleInstance aggregate)
            || !string.Equals(
                aggregate.sourceStackId,
                output.StackId,
                StringComparison.Ordinal)
            || !string.IsNullOrEmpty(aggregate.attachedEquipmentInstanceId))
        {
            failureReason = "equipment-module-committed-aggregate-mismatch";
            return false;
        }
        ItemInstanceComponentSaveData actual = output.Components
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.componentTypeId,
                    ItemInstanceComponentIds.EquipmentModule,
                    StringComparison.Ordinal));
        bool matches = actual != null
            && string.Equals(
                actual.ToCanonicalString(),
                EquipmentModuleItemStateCodec.Encode(aggregate)
                    .ToCanonicalString(),
                StringComparison.Ordinal);
        failureReason = matches
            ? string.Empty
            : "equipment-module-committed-component-mismatch";
        return matches;
    }
}
