using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public interface ICharacterNarrativeTextQuery
{
    string Get(string key, params object[] arguments);
    IReadOnlyList<string> GetVariants(string key);
    string ApplySubjectParticle(string value);
    string ApplyObjectParticle(string value);
}

public sealed class CharacterNarrativeTextQuery : ICharacterNarrativeTextQuery
{
    public const string TableName = "CharacterNarrative";

    public string Get(string key, params object[] arguments)
    {
        string value = Require(key);
        return arguments == null || arguments.Length == 0
            ? value
            : string.Format(
                CultureInfo.CurrentCulture,
                value,
                arguments);
    }

    public IReadOnlyList<string> GetVariants(string key)
    {
        string[] variants = Require(key).Split(
            new[] { '\n' },
            StringSplitOptions.RemoveEmptyEntries);
        for (int index = 0; index < variants.Length; index++)
        {
            variants[index] = variants[index].Trim();
        }

        if (variants.Length == 0)
        {
            throw new InvalidOperationException(
                $"Character narrative entry '{key}' has no variants.");
        }
        return variants;
    }

    public string ApplySubjectParticle(string value) =>
        ApplyKoreanParticle(value, "이", "가");

    public string ApplyObjectParticle(string value) =>
        ApplyKoreanParticle(value, "을", "를");

    private static string ApplyKoreanParticle(
        string value,
        string withFinalConsonant,
        string withoutFinalConsonant)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        LocalizationSettings.InitializationOperation.WaitForCompletion();
        if (!string.Equals(
                LocalizationSettings.SelectedLocale?.Identifier.Code,
                "ko",
                StringComparison.OrdinalIgnoreCase))
        {
            return value.Trim();
        }

        string trimmed = value.Trim();
        char last = trimmed[trimmed.Length - 1];
        bool hasFinalConsonant = last >= '\uAC00'
            && last <= '\uD7A3'
            && (last - '\uAC00') % 28 != 0;
        return trimmed + (hasFinalConsonant
            ? withFinalConsonant
            : withoutFinalConsonant);
    }

    private static string Require(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Character narrative localization key is required.",
                nameof(key));
        }

        LocalizationSettings.InitializationOperation.WaitForCompletion();
        string value = new LocalizedString(TableName, key)
            .GetLocalizedString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Missing CharacterNarrative entry '{key}'.");
        }
        return value;
    }
}
