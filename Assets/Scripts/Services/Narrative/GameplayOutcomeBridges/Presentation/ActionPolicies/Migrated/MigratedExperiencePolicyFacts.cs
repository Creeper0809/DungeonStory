using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal static class MigratedExperiencePolicyFacts
{
    internal const string Point = "point";
    internal const string Count = "count";
    internal const string Gram = "gram";
    internal const string MilliExperience = "milli-experience";
    internal const double CriticalNeedValue = 20d;
    internal const double SatisfiedNeedValue = 80d;
    internal const double MajorHealthRecoveryPoints = 20d;

    internal static GameplayExperienceJudgment CharacterRole(
        GameplayExperienceFacts facts,
        GameplayExperienceCategory category,
        string reason,
        string explanation,
        params string[] roles)
    {
        if (!facts.Character)
            return facts.Technical("non-character-subject");
        if (!facts.HasRole(roles))
            GameplayExperienceFacts.Fail("participant-role");
        return CompletedAction(facts, category, reason, explanation);
    }

    internal static GameplayExperienceJudgment RoleAction(
        GameplayExperienceFacts facts,
        GameplayExperienceCategory category,
        string reason,
        string explanation,
        params string[] roles)
    {
        if (!facts.HasRole(roles))
            return facts.Technical("non-experiencing-role");
        return CompletedAction(facts, category, reason, explanation);
    }

    internal static GameplayExperienceJudgment CompletedAction(
        GameplayExperienceFacts facts,
        GameplayExperienceCategory category,
        string reason,
        string explanation,
        bool partialCounts = false)
    {
        if (facts.Success || (partialCounts && facts.Partial))
            return facts.Judge(category, reason, explanation);
        return facts.Technical("non-successful-action");
    }

    internal static Dictionary<string, string> StrictDetail(
        GameplayExperienceFacts facts,
        params string[] keys)
    {
        Dictionary<string, string> values = ParseCanonicalPairs(facts.Fact("detail"));
        RequireKeys(values, keys);
        return values;
    }

    internal static bool HasFact(GameplayExperienceFacts facts, string id) =>
        facts.Binding.facts?.Any(value => value != null
            && string.Equals(value.factId, id, StringComparison.Ordinal)) == true;

    internal static Dictionary<string, string> ParseCanonicalPairs(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string segment in text.Split(';'))
        {
            int separator = segment.IndexOf('=');
            if (separator <= 0 || separator != segment.LastIndexOf('='))
                GameplayExperienceFacts.Fail("detail-key-value");
            string key = segment.Substring(0, separator);
            string value = segment.Substring(separator + 1);
            if (key.Length == 0 || value.Length == 0 || !result.TryAdd(key, value))
                GameplayExperienceFacts.Fail("detail-key-value");
        }
        return result;
    }

    internal static void RequireKeys(
        IReadOnlyDictionary<string, string> values,
        params string[] keys)
    {
        if (values.Count != keys.Length
            || keys.Any(key => !values.ContainsKey(key)))
            GameplayExperienceFacts.Fail("detail-key-set");
    }

    internal static bool StrictBoolean(
        IReadOnlyDictionary<string, string> values,
        string key)
    {
        if (!values.TryGetValue(key, out string text))
            GameplayExperienceFacts.Fail("detail-" + key);
        if (text == bool.TrueString)
            return true;
        if (text == bool.FalseString)
            return false;
        GameplayExperienceFacts.Fail("detail-" + key);
        return false;
    }

    internal static double StrictNumber(
        IReadOnlyDictionary<string, string> values,
        string key,
        double minimum = double.MinValue,
        double maximum = double.MaxValue)
    {
        double value = 0d;
        if (!values.TryGetValue(key, out string text)
            || !double.TryParse(text, NumberStyles.Float,
                CultureInfo.InvariantCulture, out value)
            || double.IsNaN(value) || double.IsInfinity(value)
            || value < minimum || value > maximum)
        {
            GameplayExperienceFacts.Fail("detail-" + key);
        }
        return value;
    }
}
