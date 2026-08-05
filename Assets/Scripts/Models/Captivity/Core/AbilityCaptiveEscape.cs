using System;

public static class CaptiveEscapeAbilityRules
{
    public const float RepathIntervalSeconds = 3f;
    public const float EscapeTimeoutSeconds = 35f;

    public static bool TryNormalizeId(string value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0;
    }
}
