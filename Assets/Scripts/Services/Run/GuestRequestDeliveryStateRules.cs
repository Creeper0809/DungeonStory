using System;
using System.Collections.Generic;
using System.Linq;


public static class GuestRequestDeliveryMaterialRules
{
    public static IReadOnlyDictionary<string, int> BuildMaterialRequirements(
        GuestRequestDefinitionSO request)
    {
        V20ItemAmountRequirement[] material =
            (request?.serviceRequirements?.items
                    ?? new List<V20ItemAmountRequirement>())
                .Where(value => value?.consume == true)
                .ToArray();
        if (material.Any(value =>
                !EconomyProjectInputOwnerAuthority.IsCanonical(
                    value.itemDefinitionId)
                || value.amount <= 0))
        {
            throw new InvalidOperationException(
                $"Guest request '{request?.StableId}' has an invalid physical "
                + "material requirement.");
        }
        return material
            .GroupBy(
                value => value.itemDefinitionId,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => checked(group.Sum(value => value.amount)),
                StringComparer.Ordinal);
    }

    public static bool HasPhysicalDelivery(GuestRequestDefinitionSO request) =>
        BuildMaterialRequirements(request).Count > 0;

    public static V20ContentRequirementSet BuildNonDeliveryRequirements(
        GuestRequestDefinitionSO request)
    {
        V20ContentRequirementSet source = request?.serviceRequirements
            ?? new V20ContentRequirementSet();
        return new V20ContentRequirementSet
        {
            items = (source.items ?? new List<V20ItemAmountRequirement>())
                .Where(value => value != null && !value.consume)
                .ToList(),
            facilities = new List<V20FacilityRequirement>(),
            research = (source.research ?? new List<V20ResearchRequirement>())
                .Where(value => value != null)
                .ToList(),
            characters = (source.characters ?? new List<V20CharacterRequirement>())
                .Where(value => value != null)
                .ToList(),
            factions = (source.factions ?? new List<V20FactionRequirement>())
                .Where(value => value != null)
                .ToList(),
            worldMetrics = (source.worldMetrics
                    ?? new List<V20WorldMetricRequirement>())
                .Where(value => value != null)
                .ToList(),
            requiredFlags = (source.requiredFlags ?? new List<string>()).ToList(),
            excludedFlags = (source.excludedFlags ?? new List<string>()).ToList()
        };
    }
}

public static class GuestRequestDeliveryOutbox
{
    public const string TransferReason = "guest-request-delivery";
    public const string ReplanDisposition = "replan";
    public const string DeclineDisposition = "decline";
    public const string ExpiredDisposition = "expired";
    private const string OperationPrefix = "guest-request-transfer:";

    public static string FormatOperationId(string instanceId)
    {
        RequireCanonical(instanceId, nameof(instanceId));
        return OperationPrefix + Uri.EscapeDataString(instanceId);
    }

    public static string FormatDestinationId(string instanceId) =>
        EconomyProjectInputOwnerAuthority.BuildGuestRequestDestinationId(
            instanceId);

    public static bool IsOwnedOperationId(string operationId) =>
        operationId != null
        && operationId.StartsWith(OperationPrefix, StringComparison.Ordinal);

    public static void RequireValidState(
        V20ActiveEventSaveData active,
        GuestRequestDefinitionSO definition,
        bool activeCollection)
    {
        GuestRequestDeliverySaveData state = active?.guestDelivery;
        if (active == null || state == null || state.sourceStackIds == null)
            throw new InvalidOperationException(
                $"Guest-request delivery save state for '{active?.instanceId}' is missing.");
        if (state.phase == GuestRequestDeliveryPhase.None)
        {
            if (!IsCanonicalEmpty(state))
                throw new InvalidOperationException(
                    $"Empty guest-request delivery state for "
                    + $"'{active.instanceId}' is not canonical.");
            return;
        }
        if (!activeCollection
            || definition == null
            || !GuestRequestDeliveryMaterialRules.HasPhysicalDelivery(definition)
            || (state.phase == GuestRequestDeliveryPhase.RewardPublished
                ? !string.Equals(
                    active.selectedChoiceId,
                    state.terminalResolutionId,
                    StringComparison.Ordinal)
                : !string.Equals(
                    active.selectedChoiceId,
                    "fulfill",
                    StringComparison.Ordinal))
            || (state.phase == GuestRequestDeliveryPhase.RewardPublished
                ? !string.Equals(
                    active.resolutionId,
                    state.terminalResolutionId,
                    StringComparison.Ordinal)
                : !string.IsNullOrEmpty(active.resolutionId))
            || !EconomyProjectInputOwnerAuthority.IsCanonical(
                state.acceptedActionId)
            || !string.Equals(
                state.acceptedActionId,
                V21ContentAlertActionIds.Society(active.instanceId, "fulfill"),
                StringComparison.Ordinal)
            || !IsOptionalCanonicalText(state.failureReason))
        {
            throw new InvalidOperationException(
                $"Guest-request delivery owner identity for '{active.instanceId}' is invalid.");
        }

        bool hasAssignment =
            EconomyProjectInputOwnerAuthority.IsCanonical(
                state.venueFacilityInstanceId)
            && EconomyProjectInputOwnerAuthority.IsCanonical(
                state.destinationId)
            && string.Equals(
                state.destinationId,
                FormatDestinationId(active.instanceId),
                StringComparison.Ordinal);
        bool hasProjection = state.inputCapacityGrams > 0L
            && state.inputMassAuthorityRevision > 0L
            && EconomyProjectInputOwnerAuthority.IsCanonical(
                state.inputCapacityFingerprint);
        bool emptyAssignment = string.IsNullOrEmpty(
                state.venueFacilityInstanceId)
            && string.IsNullOrEmpty(state.destinationId)
            && state.destinationX == 0
            && state.destinationY == 0;
        bool emptyProjection = state.inputCapacityGrams == 0L
            && state.inputMassAuthorityRevision == 0L
            && string.IsNullOrEmpty(state.inputCapacityFingerprint);
        bool emptyReceipt = IsEmptyReceipt(state);
        bool canonicalOwnedReceipt = IsCanonicalReceipt(state)
            && string.Equals(
                state.operationId,
                FormatOperationId(active.instanceId),
                StringComparison.Ordinal)
            && state.committedQuantity == GuestRequestDeliveryMaterialRules
                .BuildMaterialRequirements(definition)
                .Values.Sum()
            && (!state.inputOwnerActive
                || state.committedMassGrams == state.inputCapacityGrams);
        bool valid = state.phase switch
        {
            GuestRequestDeliveryPhase.AwaitingVenue =>
                !active.resolved
                && emptyAssignment
                && !state.inputOwnerActive
                && emptyProjection
                && emptyReceipt
                && string.IsNullOrEmpty(state.terminalResolutionId),
            GuestRequestDeliveryPhase.Delivering =>
                !active.resolved
                && hasAssignment
                && state.inputOwnerActive
                && hasProjection
                && emptyReceipt
                && string.IsNullOrEmpty(state.terminalResolutionId),
            GuestRequestDeliveryPhase.ReleasePending =>
                !active.resolved
                && emptyReceipt
                && (state.inputOwnerActive
                    ? hasAssignment && hasProjection
                    : emptyProjection)
                && IsReleaseDisposition(state.terminalResolutionId),
            GuestRequestDeliveryPhase.PhysicalCommitted =>
                !active.resolved
                && hasAssignment
                && state.inputOwnerActive
                && hasProjection
                && string.IsNullOrEmpty(state.terminalResolutionId)
                && canonicalOwnedReceipt,
            GuestRequestDeliveryPhase.RewardPublished =>
                active.resolved
                && hasAssignmentOrTerminalWithoutVenue(
                    hasAssignment,
                    state.terminalResolutionId)
                && (state.inputOwnerActive ? hasProjection : emptyProjection)
                && (string.Equals(
                        state.terminalResolutionId,
                        "fulfill",
                        StringComparison.Ordinal)
                    ? canonicalOwnedReceipt
                    : IsTerminalFailure(state.terminalResolutionId)
                        && !state.inputOwnerActive
                        && emptyReceipt),
            _ => false
        };
        if (!valid)
            throw new InvalidOperationException(
                $"Guest-request delivery occurrence '{active.instanceId}' "
                + $"has invalid phase '{state.phase}'.");

        static bool hasAssignmentOrTerminalWithoutVenue(
            bool assignment,
            string resolutionId) =>
            assignment || IsTerminalFailure(resolutionId);
    }

    public static bool IsCanonicalEmpty(GuestRequestDeliverySaveData state) =>
        state != null
        && state.phase == GuestRequestDeliveryPhase.None
        && string.IsNullOrEmpty(state.acceptedActionId)
        && string.IsNullOrEmpty(state.venueFacilityInstanceId)
        && string.IsNullOrEmpty(state.destinationId)
        && state.destinationX == 0
        && state.destinationY == 0
        && !state.inputOwnerActive
        && state.inputCapacityGrams == 0L
        && state.inputMassAuthorityRevision == 0L
        && string.IsNullOrEmpty(state.inputCapacityFingerprint)
        && IsEmptyReceipt(state)
        && string.IsNullOrEmpty(state.terminalResolutionId)
        && string.IsNullOrEmpty(state.failureReason);

    public static bool IsCanonicalReceipt(GuestRequestDeliverySaveData state) =>
        state != null
        && EconomyProjectInputOwnerAuthority.IsCanonical(state.operationId)
        && EconomyProjectInputOwnerAuthority.IsCanonical(state.commitId)
        && EconomyProjectInputOwnerAuthority.IsCanonical(
            state.requestFingerprint)
        && state.sourceStackIds != null
        && state.sourceStackIds.Count > 0
        && state.sourceStackIds.All(
            EconomyProjectInputOwnerAuthority.IsCanonical)
        && state.sourceStackIds.SequenceEqual(
            state.sourceStackIds.OrderBy(value => value, StringComparer.Ordinal),
            StringComparer.Ordinal)
        && state.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
            == state.sourceStackIds.Count
        && state.committedQuantity > 0
        && state.committedMassGrams > 0L
        && string.Equals(
            state.commitId,
            FormatCommitId(state),
            StringComparison.Ordinal);

    public static bool MatchesSaved(
        GuestRequestDeliverySaveData state,
        PhysicalItemBatchDispositionReceipt receipt) =>
        IsCanonicalReceipt(state)
        && receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(receipt.OperationId, state.operationId,
            StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode, TransferReason,
            StringComparison.Ordinal)
        && string.Equals(receipt.CommitId, state.commitId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.RequestFingerprint,
            state.requestFingerprint,
            StringComparison.Ordinal)
        && receipt.Quantity == state.committedQuantity
        && receipt.InputMassGrams == state.committedMassGrams
        && (receipt.SourceStackIds ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(state.sourceStackIds, StringComparer.Ordinal);

    public static void Record(
        GuestRequestDeliverySaveData state,
        string instanceId,
        GuestRequestDeliveryReceipt receipt)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));
        if (state.phase != GuestRequestDeliveryPhase.Delivering
            || !receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(
                receipt.OperationId,
                FormatOperationId(instanceId),
                StringComparison.Ordinal)
            || !string.Equals(receipt.ReasonCode, TransferReason,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Guest-request delivery receipt is not canonical.");
        }
        state.phase = GuestRequestDeliveryPhase.PhysicalCommitted;
        state.operationId = receipt.OperationId;
        state.commitId = receipt.CommitId;
        state.requestFingerprint = receipt.RequestFingerprint;
        state.sourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        state.committedQuantity = receipt.Quantity;
        state.committedMassGrams = receipt.InputMassGrams;
        state.failureReason = string.Empty;
    }

    public static void ClearReceipt(GuestRequestDeliverySaveData state)
    {
        state.operationId = string.Empty;
        state.commitId = string.Empty;
        state.requestFingerprint = string.Empty;
        state.sourceStackIds.Clear();
        state.committedQuantity = 0;
        state.committedMassGrams = 0L;
    }

    private static string FormatCommitId(GuestRequestDeliverySaveData state) =>
        $"physical-batch-disposition:1:{state.operationId}:"
        + $"{state.committedQuantity}:{state.committedMassGrams}";

    private static bool IsEmptyReceipt(GuestRequestDeliverySaveData state) =>
        state != null
        && string.IsNullOrEmpty(state.operationId)
        && string.IsNullOrEmpty(state.commitId)
        && string.IsNullOrEmpty(state.requestFingerprint)
        && (state.sourceStackIds?.Count ?? 0) == 0
        && state.committedQuantity == 0
        && state.committedMassGrams == 0L;

    private static bool IsReleaseDisposition(string value) =>
        string.Equals(value, ReplanDisposition, StringComparison.Ordinal)
        || IsTerminalFailure(value);

    private static bool IsTerminalFailure(string value) =>
        string.Equals(value, DeclineDisposition, StringComparison.Ordinal)
        || string.Equals(value, ExpiredDisposition, StringComparison.Ordinal);

    private static bool IsOptionalCanonicalText(string value) =>
        value != null
        && (value.Length == 0
            || string.Equals(value, value.Trim(), StringComparison.Ordinal));

    private static void RequireCanonical(string value, string parameterName)
    {
        if (!EconomyProjectInputOwnerAuthority.IsCanonical(value))
            throw new ArgumentException(
                "Guest-request delivery requires a canonical ID.",
                parameterName);
    }
}
