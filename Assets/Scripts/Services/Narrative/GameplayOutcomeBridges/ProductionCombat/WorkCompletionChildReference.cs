using System;
using System.Globalization;

public static class WorkCompletionChildReference
{
    public static GameplayResultKey Require(
        string operationId,
        string terminalOwnerId,
        string childKey)
    {
        string[] parts = childKey?.Split('|');
        if (parts == null
            || parts.Length != 4
            || !GameplayOutcomeStableIdSyntax.IsValid(parts[0])
            || !GameplayOutcomeStableIdSyntax.IsValid(parts[1])
            || !long.TryParse(
                parts[2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long revision)
            || !int.TryParse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int index)
            || !string.Equals(
                parts[1],
                operationId,
                StringComparison.Ordinal)
            || !GameplayOutcomeStableIdSyntax.IsValid(terminalOwnerId)
            || terminalOwnerId == WorkCompletionIdentityReferenceDefaults.None
            || terminalOwnerId
                == WorkCompletionIdentityReferenceDefaults.LegacyTerminalOwner)
        {
            throw new ArgumentException(
                "Referenced completion requires a canonical child result and its operation/terminal owner.",
                nameof(childKey));
        }

        GameplayResultKey result = new(
            parts[0],
            new GameplayOperationId(parts[1]),
            revision,
            index);
        if (!string.Equals(
                result.ToString(),
                childKey,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The child result key is not canonical.",
                nameof(childKey));
        }
        return result;
    }
}
