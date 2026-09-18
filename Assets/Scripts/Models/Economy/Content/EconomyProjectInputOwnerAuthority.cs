using System;

public static class EconomyProjectInputOwnerAuthority
{
    public const string GrandProjectDomain = "economy.grand-project";
    public const string RegionalContractDomain = "economy.regional-contract";
    public const string FactionContractDomain = "run.faction-contract";
    public const string GuestRequestDomain = "run.guest-request";
    public const string FestivalDomain = "run.festival";
    public const string StockPolicyDomain = "economy.stock-policy";
    public const long CapacitySchemaRevision = 1L;

    public const string GrandProjectCancelledReason =
        "grand-project-input-cancelled";
    public const string GrandProjectCompletedReason =
        "grand-project-input-completed";
    public const string GrandProjectFacilityLostReason =
        "grand-project-input-facility-lost";
    public const string RegionalContractTerminalReason =
        "regional-contract-input-terminal";
    public const string FactionContractTerminalReason =
        "faction-contract-input-terminal";
    public const string GuestRequestTerminalReason =
        "guest-request-input-terminal";
    public const string FestivalTerminalReason =
        "festival-input-terminal";
    public const string GuestRequestReplanReason =
        "guest-request-input-replan";
    public const string StockPolicyDisabledReason =
        "stock-policy-input-disabled";
    public const string StockPolicySaleCompletedReason =
        "stock-policy-input-sale-completed";

    public static string BuildGrandProjectDestinationId(string projectId) =>
        BuildDestinationId(GrandProjectDomain, projectId);

    public static string BuildRegionalContractDestinationId(string contractId) =>
        BuildDestinationId(RegionalContractDomain, contractId);

    public static string BuildFactionContractDestinationId(string contractId) =>
        BuildDestinationId(FactionContractDomain, contractId);

    public static string BuildGuestRequestDestinationId(string instanceId) =>
        BuildDestinationId(GuestRequestDomain, instanceId);

    public static string BuildFestivalDestinationId(string occurrenceId) =>
        BuildDestinationId(FestivalDomain, occurrenceId);

    public static string BuildStockPolicyDestinationId(string itemId) =>
        BuildDestinationId(StockPolicyDomain, itemId);

    public static string BuildDestinationId(string ownerDomain, string ownerId)
    {
        RequireSupportedDomain(ownerDomain);
        if (!IsCanonical(ownerId))
            throw new ArgumentException(
                "Economy input destination requires a canonical owner ID.",
                nameof(ownerId));
        return ExactFacilityInputDestinationIdentity.Prefix
            + ownerDomain + ":" + Uri.EscapeDataString(ownerId);
    }

    public static bool IsSupportedDomain(string ownerDomain) =>
        string.Equals(ownerDomain, GrandProjectDomain, StringComparison.Ordinal)
        || string.Equals(ownerDomain, RegionalContractDomain,
            StringComparison.Ordinal)
        || string.Equals(ownerDomain, FactionContractDomain,
            StringComparison.Ordinal)
        || string.Equals(ownerDomain, GuestRequestDomain,
            StringComparison.Ordinal)
        || string.Equals(ownerDomain, FestivalDomain,
            StringComparison.Ordinal)
        || string.Equals(ownerDomain, StockPolicyDomain,
            StringComparison.Ordinal);

    public static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    public static void RequireSupportedDomain(string ownerDomain)
    {
        if (!IsSupportedDomain(ownerDomain))
            throw new ArgumentException(
                "Unsupported economy input-owner domain.",
                nameof(ownerDomain));
    }
}
