public static class WildlifeCaptureTransportAbilityRules
{
    public static bool TryNormalizeId(string value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0;
    }
}
