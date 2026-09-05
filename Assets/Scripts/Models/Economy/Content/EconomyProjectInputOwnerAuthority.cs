using System;

public static class EconomyProjectInputOwnerAuthority
{
    public const string GrandProjectDomain = "economy.grand-project";
    public const string RegionalContractDomain = "economy.regional-contract";
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
    public const string StockPolicyDisabledReason =
        "stock-policy-input-disabled";
    public const string StockPolicySaleCompletedReason =
        "stock-policy-input-sale-completed";

    public static string BuildGrandProjectDestinationId(string projectId) =>
        BuildDestinationId(GrandProjectDomain, projectId);

    public static string BuildRegionalContractDestinationId(string contractId) =>
        BuildDestinationId(RegionalContractDomain, contractId);

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
