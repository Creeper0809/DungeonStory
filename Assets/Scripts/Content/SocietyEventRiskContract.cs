using System;
using System.Collections.Generic;
using System.Linq;

public static class SocietyEventRiskContract
{
    public static IEnumerable<string> Validate(
        string ownerId,
        ExperienceEventRiskTier riskTier,
        string riskReason)
    {
        if (!Enum.IsDefined(typeof(ExperienceEventRiskTier), riskTier))
        {
            yield return $"'{ownerId}' has an invalid society-event risk tier.";
        }

        if (string.IsNullOrWhiteSpace(riskReason)
            || !string.Equals(
                riskReason,
                riskReason.Trim(),
                StringComparison.Ordinal))
        {
            yield return $"'{ownerId}' requires a canonical society-event risk reason.";
        }
    }

    public static void RequireValid(
        ExperienceEventRiskTier riskTier,
        string riskReason,
        string ownerId)
    {
        string[] errors = new List<string>(Validate(
            ownerId,
            riskTier,
            riskReason)).ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join(" | ", errors));
        }
    }
}
