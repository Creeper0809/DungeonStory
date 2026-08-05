using System;
using System.Globalization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Strict presentation query for character-AI diagnostic text.
/// The default-assembly AI adapter selects stable keys; this boundary owns
/// locale lookup and formatting without a display-language fallback.
/// </summary>
public static class CharacterAiDiagnosticsTextQuery
{
    public const string TableName = "CharacterAI";

    public static string Get(string key, params object[] arguments)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Character AI localization key is required.",
                nameof(key));
        }

        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string template = new LocalizedString(TableName, key)
            .GetLocalizedString();
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException(
                $"Missing CharacterAI entry '{key}'.");
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
