using System;
using System.Globalization;
using System.Linq;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public interface ICharacterMedicalStatusLocalizer
{
    string Localize(CharacterMedicalOrder order);
}

/// <summary>
/// Presentation-only adapter for saved medical status codes.
/// </summary>
public sealed class CharacterMedicalStatusLocalizer :
    ICharacterMedicalStatusLocalizer
{
    private const string KeyPrefix = "CharacterMedicalStatus";

    public string Localize(CharacterMedicalOrder order)
    {
        if (order == null
            || order.statusCode == CharacterMedicalStatusCode.Unknown)
        {
            return string.Empty;
        }

        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string key = KeyPrefix + order.statusCode;
        string template = new LocalizedString(
                DomainFailureLocalizer.TableName,
                key)
            .GetLocalizedString();
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException(
                $"Missing localized medical status entry '{key}'.");
        }

        object[] arguments = (order.statusParameters ?? new System.Collections.Generic.List<string>())
            .Cast<object>()
            .ToArray();
        return arguments.Length == 0
            ? template
            : string.Format(CultureInfo.CurrentCulture, template, arguments);
    }
}
