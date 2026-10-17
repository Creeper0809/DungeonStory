using System;

internal static class EnvironmentOutcomeReceiptFactorySupport
{
    internal static string RequireStable(string value, string parameterName) =>
        GameplayOutcomeStableIdSyntax.Require(value, parameterName);

    internal static string RequireDisplay(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(
                "A non-empty immutable display snapshot is required.",
                parameterName)
            : value.Trim();

    internal static string RequireFactValue(string value, string parameterName)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || canonical.Length
                > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length)
        {
            throw new ArgumentException(
                "A non-empty bounded gameplay outcome fact value is required.",
                parameterName);
        }
        return canonical;
    }

    internal static string RequireExactText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
        || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
        || value.IndexOf('\r') >= 0
        || value.IndexOf('\n') >= 0
            ? throw new ArgumentException(
                "A non-empty canonical text value is required.",
                parameterName)
            : value;

    internal static double PersistedFloat(float value) =>
        double.Parse(
            value.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture);

    internal static void AddMetric(
        ref EnvironmentOutcomePayloadBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId reference) => Require(builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, reference)));

    internal static void AddFact(
        ref EnvironmentOutcomePayloadBuilder builder,
        GameplayOutcomeFactId id,
        string value) => Require(builder.AddFact(
        new GameplayOutcomeFact(id, value)));

    internal static void Require(bool accepted)
    {
        if (!accepted)
            throw new InvalidOperationException(
                "Environment outcome fixed buffer capacity was exceeded.");
    }
}
