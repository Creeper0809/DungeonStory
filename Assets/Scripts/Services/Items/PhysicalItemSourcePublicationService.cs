using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct PhysicalItemSourcePublicationReceipt
{
    public PhysicalItemSourcePublicationReceipt(
        string operationId,
        string reasonCode,
        IReadOnlyList<string> outputCommitIds,
        int outputQuantity,
        long outputMassGrams)
    {
        OperationId = operationId ?? string.Empty;
        ReasonCode = reasonCode ?? string.Empty;
        OutputCommitIds = (outputCommitIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        OutputQuantity = outputQuantity;
        OutputMassGrams = outputMassGrams;
    }

    public string OperationId { get; }
    public string ReasonCode { get; }
    public IReadOnlyList<string> OutputCommitIds { get; }
    public int OutputQuantity { get; }
    public long OutputMassGrams { get; }
    public bool IsCommitted => IsCanonical(OperationId)
        && IsCanonical(ReasonCode)
        && OutputCommitIds?.Count > 0
        && OutputCommitIds.All(IsCanonical)
        && OutputCommitIds.Distinct(StringComparer.Ordinal).Count()
            == OutputCommitIds.Count
        && OutputQuantity > 0
        && OutputMassGrams > 0L;

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

/// <summary>
/// Publishes physical Source outputs from an already-authoritative abstract
/// owner. Every output is tagged with a deterministic commit marker, so a
/// restore/retry validates the existing stacks instead of minting a second
/// copy. This boundary does not authorize the abstract owner itself; callers
/// must persist that ownership before publishing.
/// </summary>
public interface IPhysicalItemSourcePublicationService
{
    bool TryEnsureLooseOutputs(
        IReadOnlyDictionary<string, int> outputs,
        Vector2Int outputPosition,
        string operationId,
        string reasonCode,
        out PhysicalItemSourcePublicationReceipt receipt,
        out string failureReason);
}

public sealed class PhysicalItemSourcePublicationService :
    IPhysicalItemSourcePublicationService
{
    private readonly IEquipmentPhysicalItemGateway items;
    private readonly IPhysicalItemMassQuery massQuery;

    public PhysicalItemSourcePublicationService(
        IEquipmentPhysicalItemGateway items,
        IPhysicalItemMassQuery massQuery)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
    }

    public bool TryEnsureLooseOutputs(
        IReadOnlyDictionary<string, int> outputs,
        Vector2Int outputPosition,
        string operationId,
        string reasonCode,
        out PhysicalItemSourcePublicationReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        string operation = operationId ?? string.Empty;
        string reason = reasonCode ?? string.Empty;
        KeyValuePair<string, int>[] canonical = (outputs
                ?? new Dictionary<string, int>())
            .Where(pair => pair.Value > 0)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        if (!IsCanonical(operation)
            || !IsCanonical(reason)
            || canonical.Length == 0
            || canonical.Any(pair => !IsCanonical(pair.Key))
            || canonical.Select(pair => pair.Key)
                .Distinct(StringComparer.Ordinal).Count() != canonical.Length)
        {
            failureReason = "physical-source-publication-invalid-request";
            return false;
        }

        List<string> commits = new(canonical.Length);
        int totalQuantity = 0;
        long totalMass = 0L;
        foreach (KeyValuePair<string, int> output in canonical)
        {
            long unitMass;
            try
            {
                unitMass = massQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)output.Key).Value;
                totalQuantity = checked(totalQuantity + output.Value);
                totalMass = checked(totalMass
                    + checked(unitMass * output.Value));
            }
            catch (Exception exception)
            {
                failureReason = "physical-source-publication-mass-invalid:"
                    + output.Key + ":" + exception.GetType().Name;
                return false;
            }

            string commitId =
                $"physical-source:{operation}:{output.Key}:{output.Value}:"
                + checked(unitMass * output.Value);
            WorldItemStackSnapshot[] existing = items.GetAllStacks()
                .Where(stack => stack != null
                    && ProductionOutputCommitComponentCodec.Matches(
                        stack.Components,
                        commitId))
                .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                .ToArray();
            if (existing.Length > 0)
            {
                if (existing.Any(stack => !string.Equals(
                            stack.ItemId,
                            output.Key,
                            StringComparison.Ordinal)
                        || stack.State != WorldItemStackState.Loose
                        || stack.Position != outputPosition)
                    || existing.Sum(stack => (long)stack.Quantity) != output.Value)
                {
                    failureReason =
                        "physical-source-publication-existing-output-conflict:"
                        + commitId;
                    return false;
                }
                commits.Add(commitId);
                continue;
            }

            if (!items.SpawnItemAtWithComponents(
                    output.Key,
                    output.Value,
                    outputPosition,
                    WorldItemStackState.Loose,
                    string.Empty,
                    new[]
                    {
                        ProductionOutputCommitComponentCodec.Create(commitId)
                    },
                    out int spawned)
                || spawned != output.Value)
            {
                failureReason = "physical-source-publication-spawn-failed:"
                    + commitId;
                return false;
            }

            WorldItemStackSnapshot[] published = items.GetAllStacks()
                .Where(stack => stack != null
                    && ProductionOutputCommitComponentCodec.Matches(
                        stack.Components,
                        commitId))
                .ToArray();
            if (published.Length == 0
                || published.Any(stack => !string.Equals(
                            stack.ItemId,
                            output.Key,
                            StringComparison.Ordinal)
                        || stack.State != WorldItemStackState.Loose
                        || stack.Position != outputPosition)
                || published.Sum(stack => (long)stack.Quantity) != output.Value)
            {
                failureReason =
                    "physical-source-publication-postcondition-failed:"
                    + commitId;
                return false;
            }
            commits.Add(commitId);
        }

        receipt = new PhysicalItemSourcePublicationReceipt(
            operation,
            reason,
            commits,
            totalQuantity,
            totalMass);
        if (!receipt.IsCommitted)
        {
            receipt = default;
            failureReason = "physical-source-publication-receipt-invalid";
            return false;
        }
        return true;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
