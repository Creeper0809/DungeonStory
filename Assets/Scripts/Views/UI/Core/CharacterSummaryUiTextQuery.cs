using System;
using System.Globalization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Strict presentation lookup for formatter-owned character-summary text.
/// Domain enum fallbacks, numeric values, and log payloads remain caller-owned.
/// </summary>
public static class CharacterSummaryUiTextQuery
{
    public const string TableName = "CharacterSummaryUI";

    public static string Get(string key, params object[] arguments)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Character summary localization key is required.",
                nameof(key));
        }

        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string template = new LocalizedString(TableName, key)
            .GetLocalizedString();
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException(
                $"Missing CharacterSummaryUI entry '{key}'.");
        }

        object[] resolvedArguments = arguments ?? Array.Empty<object>();
        return resolvedArguments.Length == 0
            ? template
            : string.Format(
                CultureInfo.CurrentCulture,
                template,
                resolvedArguments);
    }
}
