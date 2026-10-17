using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Save metadata and physical child joins share one validator for capture, staging, and retry.
public static class V20ContentRecoveryEvidence
{
    public static void ValidateMetadata(
        IReadOnlyList<V20ContentResolutionRecoverySaveData> pending)
    {
        HashSet<string> operations = new(StringComparer.Ordinal);
        foreach (V20ContentResolutionRecoverySaveData row in pending)
        {
            if (row == null
                || string.IsNullOrWhiteSpace(row.actionId)
                || !string.Equals(
                    row.actionId,
                    row.actionId.Trim(),
                    StringComparison.Ordinal)
                || !operations.Add(row.actionId)
                || row.absoluteDay < 1
                || !Enum.IsDefined(typeof(ContentResolutionRequestKind), row.requestKind)
                || !Enum.IsDefined(typeof(V20ContentResolutionRecoveryPhase), row.phase)
                || row.phase == V20ContentResolutionRecoveryPhase.Prepared
                || row.resolutions == null
                || row.resolutions.Count == 0
                || row.childCommitEvidenceVersion is not (1 or 2)
                || row.moneyTransactionId == null
                || row.itemConsumptionOperationId == null
                || row.itemGrantBatchCommitId == null
                || row.itemConsumptionCommitId == null
                || row.itemGrantReceiptId == null
                || row.durableEquipmentEvidenceVersion is < 0 or > 1
                || row.durableEquipmentStackId == null
                || row.durableEquipmentItemId == null
                || row.durableEquipmentContext == null
                || row.durableEquipmentEvidenceVersion == 0
                    && (row.durableEquipmentStackId.Length != 0
                        || row.durableEquipmentItemId.Length != 0
                        || row.durableEquipmentContext.Length != 0)
                || row.durableEquipmentEvidenceVersion == 1
                    && (!GameplayOutcomeStableIdSyntax.IsValid(
                            row.durableEquipmentStackId)
                        || !GameplayOutcomeStableIdSyntax.IsValid(
                            row.durableEquipmentItemId)
                        || !DungeonStory.Foundation.Facts.DomainFactValueSyntax.IsText(
                            row.durableEquipmentContext,
                            GameplayOutcomeBufferLimits
                                .MaximumFactValueUtf16Length,
                            allowEmpty: false)
                        || !row.durableEquipmentContext.StartsWith(
                            "semantics=wear;",
                            StringComparison.Ordinal))
                || row.outcomeEvidenceVersion is < 0 or > 1
                || row.stateBeforeFingerprint == null
                || row.stateAfterFingerprint == null
                || row.outcomeEvidenceVersion == 1
                    && (!IsSha256(row.stateBeforeFingerprint)
                        || row.phase
                                >= V20ContentResolutionRecoveryPhase
                                    .TypedEffectsApplied
                            && !IsSha256(row.stateAfterFingerprint)
                        || row.phase
                                < V20ContentResolutionRecoveryPhase
                                    .TypedEffectsApplied
                            && row.stateAfterFingerprint.Length > 0)
                || row.resolutions.Any(value => value == null
                    || string.IsNullOrWhiteSpace(value.definitionId)
                    || string.IsNullOrWhiteSpace(value.resolutionId)
                    || value.effects == null
                    || value.effects.Any(effect => effect == null || !effect.IsValid)
                    || value.participantCharacterIds == null
                    || value.terminalEffectKinds == null
                    || value.applications == null
                    || value.applications.Any(application =>
                        application == null
                        || application.effectIndex < 0
                        || application.effectIndex >= value.effects.Count
                        || string.IsNullOrWhiteSpace(application.targetId)
                        || !Enum.IsDefined(
                            typeof(V20EffectApplicationStatus),
                            application.status)
                        || application.beforeValue == null
                        || application.afterValue == null
                        || string.IsNullOrWhiteSpace(application.semantic)
                        || application.childCommitId == null)))
            {
                throw new InvalidOperationException(
                    "Pending content-resolution recovery is invalid or duplicated.");
            }

            V20ContentEffect[] effects = row.resolutions
                .SelectMany(value => value.effects)
                .Where(value => value != null && value.IsValid)
                .ToArray();
            int moneyDelta = effects
                .Where(value => value.kind == V20ContentEffectKind.Money)
                .Sum(value => (int)Math.Round(value.amount));
            bool consumesItems = effects.Any(value =>
                value.kind == V20ContentEffectKind.ItemConsume
                && Mathf.RoundToInt(value.amount) > 0);
            bool grantsItems = effects.Any(value =>
                value.kind == V20ContentEffectKind.ItemGrant
                && (int)Math.Round(value.amount) > 0);
            if ((moneyDelta != 0) != IsCanonicalRequiredId(
                    row.moneyTransactionId)
                || consumesItems != string.Equals(
                    row.itemConsumptionOperationId,
                    "content-resolution:" + row.actionId,
                    StringComparison.Ordinal)
                || grantsItems != string.Equals(
                    row.itemGrantBatchCommitId,
                    "physical-source-batch:run.v20-grant:content-resolution:"
                        + row.actionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Pending content-resolution child commit evidence is missing or inconsistent.");
            }
            bool committedGrant = grantsItems && row.phase >= V20ContentResolutionRecoveryPhase.ItemGrantsCommitted;
            if (row.childCommitEvidenceVersion == 2
                && (consumesItems != !string.IsNullOrEmpty(row.itemConsumptionCommitId)
                    || committedGrant != !string.IsNullOrEmpty(row.itemGrantReceiptId))
                || row.childCommitEvidenceVersion == 1
                    && (!string.IsNullOrEmpty(row.itemConsumptionCommitId)
                        || !string.IsNullOrEmpty(row.itemGrantReceiptId)))
                throw new InvalidOperationException("Content physical receipt references do not match the recovery phase.");
        }
    }

    private static bool IsSha256(string value) =>
        value != null
        && value.Length == 71
        && value.StartsWith("sha256:", StringComparison.Ordinal)
        && value.Skip("sha256:".Length).All(character =>
            character is >= '0' and <= '9'
                or >= 'a' and <= 'f');

    public static PhysicalItemExactSourcePublicationPlan CreateGrantPlan(
        string operationId, Vector2Int dropoff, IEnumerable<V20ContentEffect> effects)
    {
        var outputs = effects.Where(value => value != null
                && value.kind == V20ContentEffectKind.ItemGrant && Mathf.RoundToInt(value.amount) > 0)
            .GroupBy(value => value.targetId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select((group, index) => new FacilityBufferPlannedOutputSlice(
                $"grant:{index:D4}:{group.Key}",
                PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)group.Key),
                group.Sum(value => Math.Max(0, Mathf.RoundToInt(value.amount)))))
            .ToArray();
        return new PhysicalItemExactSourcePublicationPlan("run.v20-grant", operationId,
            dropoff, outputs, retainRecoveryReceipt: true);
    }

    public static void RequirePhysicalReceipts(V20ContentResolutionRecoverySaveData recovery,
        IPhysicalEffectReceiptQuery query) => RequirePhysicalReceipts(recovery,
            (kind, operation) => query != null && query.TryGetPhysicalEffectReceipt(kind, operation, out var value)
                ? value : null);

    public static void ValidateIncoming(IReadOnlyList<V20ContentResolutionRecoverySaveData> pending,
        IPhysicalEffectReceiptRestoreQuery query)
    {
        if (pending == null || query == null || !query.IsPhysicalEffectReceiptCandidateAvailable)
            throw new InvalidOperationException("Content recovery requires the incoming physical receipt candidate.");
        ValidateMetadata(pending);
        foreach (var row in pending)
            RequirePhysicalReceipts(row, (kind, operation) =>
                query.TryGetPhysicalEffectReceiptCandidate(kind, operation, out var value) ? value : null);
    }

    private static void RequirePhysicalReceipts(V20ContentResolutionRecoverySaveData row,
        Func<PhysicalEffectReceiptKind, string, PhysicalEffectReceiptSaveData> lookup)
    {
        ValidateMetadata(new[] { row });
        var effects = row.resolutions.SelectMany(value => value.effects).ToArray();
        var costs = Quantities(effects, V20ContentEffectKind.ItemConsume);
        var grants = Quantities(effects, V20ContentEffectKind.ItemGrant);
        if (row.childCommitEvidenceVersion < 2 && (costs.Count > 0 || grants.Count > 0))
            throw new InvalidOperationException("Legacy content recovery has no durable physical commit evidence; automatic replay is unsafe.");
        if (costs.Count > 0)
        {
            var receipt = Require(PhysicalEffectReceiptKind.ReservedConsumption,
                row.itemConsumptionOperationId, row.itemConsumptionCommitId, lookup);
            RequireQuantities(costs, receipt);
        }
        if (grants.Count > 0)
        {
            var receipt = lookup(PhysicalEffectReceiptKind.ReleasedPublication, row.itemGrantBatchCommitId);
            // A committed child may precede its parent phase when the commit reply was lost.
            if (receipt == null && row.phase < V20ContentResolutionRecoveryPhase.ItemGrantsCommitted) return;
            receipt = Require(PhysicalEffectReceiptKind.ReleasedPublication, row.itemGrantBatchCommitId,
                row.phase >= V20ContentResolutionRecoveryPhase.ItemGrantsCommitted ? row.itemGrantReceiptId : null, lookup);
            var plan = CreateGrantPlan("content-resolution:" + row.actionId,
                new Vector2Int(row.grantDropoffX, row.grantDropoffY), effects);
            if (receipt.requestFingerprint != PhysicalEffectReceiptRules.PublicationFingerprint(
                    plan.DestinationId, plan.OutcomeFingerprint, plan.DropPosition.x, plan.DropPosition.y))
                throw new InvalidOperationException("Content grant receipt does not match the exact publication plan.");
            RequireQuantities(grants, receipt);
            foreach (var output in plan.Outputs)
                if (receipt.lines.Where(value => value.outputLineId == output.OutputLineId)
                        .Sum(value => value.quantity) != output.Quantity
                    || receipt.lines.Any(value => value.outputLineId == output.OutputLineId
                        && (value.itemId != output.ItemDefinitionId.Value || value.itemInstanceId.Length != 0)))
                    throw new InvalidOperationException("Content grant receipt output vector mismatched.");
        }
    }

    private static PhysicalEffectReceiptSaveData Require(PhysicalEffectReceiptKind kind,
        string operation, string commitId,
        Func<PhysicalEffectReceiptKind, string, PhysicalEffectReceiptSaveData> lookup)
    {
        var receipt = lookup(kind, operation);
        PhysicalEffectReceiptRules.Validate(receipt);
        if (receipt.kind != (int)kind || receipt.operationId != operation
            || commitId != null && receipt.commitId != commitId)
            throw new InvalidOperationException("Content physical child commit is missing or mismatched.");
        return receipt;
    }

    private static Dictionary<string, int> Quantities(IEnumerable<V20ContentEffect> effects,
        V20ContentEffectKind kind) => effects.Where(value => value.kind == kind
                && Mathf.RoundToInt(value.amount) > 0)
            .GroupBy(value => value.targetId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key,
                group => group.Sum(value => Mathf.RoundToInt(value.amount)), StringComparer.Ordinal);

    private static void RequireQuantities(Dictionary<string, int> expected, PhysicalEffectReceiptSaveData receipt)
    {
        var actual = receipt.lines.GroupBy(value => value.itemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Sum(value => value.quantity), StringComparer.Ordinal);
        if (actual.Count != expected.Count || expected.Any(value =>
                !actual.TryGetValue(value.Key, out int quantity) || quantity != value.Value))
            throw new InvalidOperationException("Content physical receipt quantities do not match the resolved effects.");
    }

    private static bool IsCanonicalRequiredId(string value) => !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
