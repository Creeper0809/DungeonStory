using System;
using System.Text.RegularExpressions;
using UnityEngine;

public static class LlmJsonResponseParser
{
    private static readonly Regex TrailingCommaRegex = new Regex(
        @",\s*(?=[}\]])",
        RegexOptions.Compiled);

    public static bool TryParse<T>(string response, out T payload, out string error)
        where T : ILlmJsonPayload
    {
        payload = default;
        error = string.Empty;

        if (!TryExtractJsonObject(response, out string json, out error))
        {
            return false;
        }

        if (typeof(T) == typeof(CustomerPersonaJsonDto)
            && !CustomerPersonaJsonDto.ValidateRawJson(json, out error))
        {
            return false;
        }

        if (typeof(T) == typeof(MacroGoalJsonDto)
            && !MacroGoalJsonDto.ValidateRawJson(json, out error))
        {
            return false;
        }

        if (typeof(T) == typeof(MoodImpulseJsonDto)
            && !MoodImpulseJsonDto.ValidateRawJson(json, out error))
        {
            return false;
        }

        if (typeof(T) == typeof(SocialRumorJsonDto)
            && !SocialRumorJsonDto.ValidateRawJson(json, out error))
        {
            return false;
        }

        try
        {
            payload = JsonUtility.FromJson<T>(json);
        }
        catch (Exception exception)
        {
            error = $"JSON parse failed: {exception.Message}";
            return false;
        }

        if (payload == null)
        {
            error = "JSON parse produced a null payload.";
            return false;
        }

        if (!payload.Validate(out error))
        {
            return false;
        }

        return true;
    }

    public static bool TryExtractJsonObject(string response, out string json, out string error)
    {
        json = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(response))
        {
            error = "LLM response is empty.";
            return false;
        }

        string trimmed = StripMarkdownFence(response.Trim());
        int firstBrace = trimmed.IndexOf('{');
        int lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace < firstBrace)
        {
            error = "LLM response does not contain a JSON object.";
            return false;
        }

        json = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
        json = TrailingCommaRegex.Replace(json, string.Empty);
        return true;
    }

    private static string StripMarkdownFence(string value)
    {
        if (!value.StartsWith("```", StringComparison.Ordinal))
        {
            return value;
        }

        int firstLineEnd = value.IndexOf('\n');
        if (firstLineEnd < 0)
        {
            return value;
        }

        int closingFence = value.LastIndexOf("```", StringComparison.Ordinal);
        if (closingFence <= firstLineEnd)
        {
            return value;
        }

        return value.Substring(firstLineEnd + 1, closingFence - firstLineEnd - 1).Trim();
    }
}

[Serializable]
public sealed class MoodImpulseJsonDto : ILlmJsonPayload
{
    private const string NumericFieldPattern = "\"{0}\"\\s*:\\s*-?\\d+(?:\\.\\d+)?";
    private const string IntegerFieldPattern = "\"{0}\"\\s*:\\s*-?\\d+(?![\\d.])";

    public string moodImpulse;
    public float strength;
    public int targetFacilityId = -1;
    public string targetFacilityTag;
    public string reason;
    public float validSeconds = 30f;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(moodImpulse))
        {
            error = "moodImpulse is required.";
            return false;
        }

        if (!Enum.TryParse(moodImpulse, ignoreCase: true, out CharacterMoodImpulseType parsed))
        {
            error = $"Unsupported moodImpulse: {moodImpulse}.";
            return false;
        }

        if (strength < 0f || strength > 1f)
        {
            error = "strength must be between 0 and 1.";
            return false;
        }

        if (validSeconds < 1f || validSeconds > 300f)
        {
            error = "validSeconds must be between 1 and 300.";
            return false;
        }

        if (parsed != CharacterMoodImpulseType.None && string.IsNullOrWhiteSpace(reason))
        {
            error = "reason is required for actionable mood impulses.";
            return false;
        }

        if ((parsed == CharacterMoodImpulseType.AvoidFacility
                || parsed == CharacterMoodImpulseType.Vandalize)
            && targetFacilityId < 0
            && string.IsNullOrWhiteSpace(targetFacilityTag))
        {
            error = $"{moodImpulse} requires targetFacilityId or targetFacilityTag.";
            return false;
        }

        return true;
    }

    public static bool ValidateRawJson(string json, out string error)
    {
        error = string.Empty;
        if (!HasRawNumber(json, nameof(strength)))
        {
            error = "strength must be a JSON number, not a string or null.";
            return false;
        }

        if (!HasRawInteger(json, nameof(targetFacilityId)))
        {
            error = "targetFacilityId must be a JSON integer, not a string or null.";
            return false;
        }

        if (!HasRawNumber(json, nameof(validSeconds)))
        {
            error = "validSeconds must be a JSON number, not a string or null.";
            return false;
        }

        return true;
    }

    public CharacterMoodImpulse ToRuntimeImpulse(string source, float now)
    {
        Enum.TryParse(moodImpulse, ignoreCase: true, out CharacterMoodImpulseType parsed);
        return new CharacterMoodImpulse
        {
            type = parsed,
            strength = Mathf.Clamp01(strength),
            targetFacilityId = targetFacilityId,
            targetFacilityTag = targetFacilityTag,
            reason = reason,
            validUntil = validSeconds > 0f ? now + validSeconds : 0f,
            source = source
        };
    }

    private static bool HasRawNumber(string json, string fieldName)
    {
        return !string.IsNullOrWhiteSpace(json)
            && Regex.IsMatch(json, string.Format(NumericFieldPattern, Regex.Escape(fieldName)));
    }

    private static bool HasRawInteger(string json, string fieldName)
    {
        return !string.IsNullOrWhiteSpace(json)
            && Regex.IsMatch(json, string.Format(IntegerFieldPattern, Regex.Escape(fieldName)));
    }
}

[Serializable]
public sealed class CustomerPersonaJsonDto : ILlmJsonPayload
{
    private const string NumericFieldPattern = "\"{0}\"\\s*:\\s*-?\\d+(?:\\.\\d+)?";

    public string traitName;
    public string flavorText;
    public float selfCareMultiplier;
    public float curiosityMultiplier;
    public float shoppingMultiplier;
    public float patienceMultiplier;
    public float hungerCurveMultiplier;
    public float funCurveMultiplier;
    public float moodCurveMultiplier;
    public string[] preferredFacilityTags;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(traitName))
        {
            error = "traitName is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(flavorText))
        {
            error = "flavorText is required.";
            return false;
        }

        if (preferredFacilityTags == null)
        {
            error = "preferredFacilityTags is required.";
            return false;
        }

        return ValidateMultiplier(selfCareMultiplier, nameof(selfCareMultiplier), out error)
            && ValidateMultiplier(curiosityMultiplier, nameof(curiosityMultiplier), out error)
            && ValidateMultiplier(shoppingMultiplier, nameof(shoppingMultiplier), out error)
            && ValidateMultiplier(patienceMultiplier, nameof(patienceMultiplier), out error)
            && ValidateMultiplier(hungerCurveMultiplier, nameof(hungerCurveMultiplier), out error)
            && ValidateMultiplier(funCurveMultiplier, nameof(funCurveMultiplier), out error)
            && ValidateMultiplier(moodCurveMultiplier, nameof(moodCurveMultiplier), out error);
    }

    public CustomerPersonaData ToRuntimeData()
    {
        return new CustomerPersonaData
        {
            traitName = traitName,
            flavorText = flavorText,
            selfCareMultiplier = selfCareMultiplier,
            curiosityMultiplier = curiosityMultiplier,
            shoppingMultiplier = shoppingMultiplier,
            patienceMultiplier = patienceMultiplier,
            hungerCurveMultiplier = hungerCurveMultiplier,
            funCurveMultiplier = funCurveMultiplier,
            moodCurveMultiplier = moodCurveMultiplier,
            preferredFacilityTags = preferredFacilityTags ?? Array.Empty<string>()
        };
    }

    public static bool ValidateRawJson(string json, out string error)
    {
        error = string.Empty;
        string[] numericFields =
        {
            nameof(selfCareMultiplier),
            nameof(curiosityMultiplier),
            nameof(shoppingMultiplier),
            nameof(patienceMultiplier),
            nameof(hungerCurveMultiplier),
            nameof(funCurveMultiplier),
            nameof(moodCurveMultiplier)
        };

        foreach (string field in numericFields)
        {
            if (!HasRawNumber(json, field))
            {
                error = $"{field} must be a JSON number, not a string or null.";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(json)
            || !Regex.IsMatch(json, $"\"{nameof(preferredFacilityTags)}\"\\s*:\\s*\\["))
        {
            error = "preferredFacilityTags must be a JSON array.";
            return false;
        }

        return true;
    }

    private static bool ValidateMultiplier(float value, string fieldName, out string error)
    {
        error = string.Empty;
        if (value < 0.25f || value > 2f)
        {
            error = $"{fieldName} must be between 0.25 and 2.0.";
            return false;
        }

        return true;
    }

    private static bool HasRawNumber(string json, string fieldName)
    {
        return !string.IsNullOrWhiteSpace(json)
            && Regex.IsMatch(json, string.Format(NumericFieldPattern, Regex.Escape(fieldName)));
    }
}

[Serializable]
public sealed class MacroGoalJsonDto : ILlmJsonPayload
{
    private const string NumericFieldPattern = "\"{0}\"\\s*:\\s*-?\\d+(?:\\.\\d+)?";
    private const string IntegerFieldPattern = "\"{0}\"\\s*:\\s*-?\\d+(?![\\d.])";

    public string macroGoal;
    public string reason;
    public int targetFacilityId = -1;
    public string targetFacilityTag;
    public float validSeconds = 30f;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(macroGoal))
        {
            error = "macroGoal is required.";
            return false;
        }

        if (!Enum.TryParse(macroGoal, ignoreCase: true, out CharacterMacroGoalType parsed)
            || parsed == CharacterMacroGoalType.None)
        {
            error = $"Unsupported macroGoal: {macroGoal}.";
            return false;
        }

        if (validSeconds < 1f || validSeconds > 600f)
        {
            error = "validSeconds must be between 1 and 600.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            error = "reason is required.";
            return false;
        }

        if ((parsed == CharacterMacroGoalType.AvoidFacility
                || parsed == CharacterMacroGoalType.Vandalize)
            && targetFacilityId < 0
            && string.IsNullOrWhiteSpace(targetFacilityTag))
        {
            error = $"{macroGoal} requires targetFacilityId or targetFacilityTag.";
            return false;
        }

        return true;
    }

    public static bool ValidateRawJson(string json, out string error)
    {
        error = string.Empty;
        if (!HasRawInteger(json, nameof(targetFacilityId)))
        {
            error = "targetFacilityId must be a JSON integer, not a string or null.";
            return false;
        }

        if (!HasRawNumber(json, nameof(validSeconds)))
        {
            error = "validSeconds must be a JSON number, not a string or null.";
            return false;
        }

        return true;
    }

    public CharacterMacroGoal ToRuntimeGoal(string source, float now)
    {
        Enum.TryParse(macroGoal, ignoreCase: true, out CharacterMacroGoalType parsed);
        return new CharacterMacroGoal
        {
            type = parsed,
            reason = reason,
            targetFacilityId = targetFacilityId,
            targetFacilityTag = targetFacilityTag,
            validUntil = validSeconds > 0f ? now + validSeconds : 0f,
            source = source
        };
    }

    private static bool HasRawNumber(string json, string fieldName)
    {
        return !string.IsNullOrWhiteSpace(json)
            && Regex.IsMatch(json, string.Format(NumericFieldPattern, Regex.Escape(fieldName)));
    }

    private static bool HasRawInteger(string json, string fieldName)
    {
        return !string.IsNullOrWhiteSpace(json)
            && Regex.IsMatch(json, string.Format(IntegerFieldPattern, Regex.Escape(fieldName)));
    }
}

[Serializable]
public sealed class SocialRumorJsonDto : ILlmJsonPayload
{
    private const string NumericFieldPattern = "\"{0}\"\\s*:\\s*-?\\d+(?:\\.\\d+)?";
    private const string IntegerFieldPattern = "\"{0}\"\\s*:\\s*-?\\d+(?![\\d.])";

    public string rumorType;
    public string targetType;
    public int targetFacilityId = -1;
    public string targetFacilityTag;
    public string targetCharacterId = string.Empty;
    public string targetCharacterName;
    public float sentiment;
    public string summary;
    public float spreadChance;
    public float trustImpact;
    public float validSeconds = 600f;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(rumorType))
        {
            error = "rumorType is required.";
            return false;
        }

        if (!Enum.TryParse(rumorType, ignoreCase: true, out SocialRumorType parsedRumorType))
        {
            error = $"Unsupported rumorType: {rumorType}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetType))
        {
            error = "targetType is required.";
            return false;
        }

        if (!Enum.TryParse(targetType, ignoreCase: true, out SocialRumorTargetType parsedTargetType))
        {
            error = $"Unsupported targetType: {targetType}.";
            return false;
        }

        if (sentiment < -1f || sentiment > 1f)
        {
            error = "sentiment must be between -1 and 1.";
            return false;
        }

        if (spreadChance < 0f || spreadChance > 1f)
        {
            error = "spreadChance must be between 0 and 1.";
            return false;
        }

        if (trustImpact < -1f || trustImpact > 1f)
        {
            error = "trustImpact must be between -1 and 1.";
            return false;
        }

        if (validSeconds < 0f || validSeconds > 1800f)
        {
            error = "validSeconds must be between 0 and 1800.";
            return false;
        }

        if (parsedRumorType == SocialRumorType.None)
        {
            if (parsedTargetType != SocialRumorTargetType.None)
            {
                error = "rumorType None requires targetType None.";
                return false;
            }

            return true;
        }

        if (parsedTargetType == SocialRumorTargetType.None)
        {
            error = "Actionable rumors require a targetType.";
            return false;
        }

        if (validSeconds <= 0f)
        {
            error = "Actionable rumors require validSeconds greater than 0.";
            return false;
        }

        if (spreadChance < 0.35f)
        {
            error = "Actionable rumors require spreadChance between 0.35 and 1.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            error = "Actionable rumors require summary.";
            return false;
        }

        if (summary.Length > 160)
        {
            error = "summary must be 160 characters or shorter.";
            return false;
        }

        if (parsedTargetType == SocialRumorTargetType.Facility
            && targetFacilityId < 0
            && string.IsNullOrWhiteSpace(targetFacilityTag))
        {
            error = "Facility rumors require targetFacilityId or targetFacilityTag.";
            return false;
        }

        if (parsedTargetType == SocialRumorTargetType.Character
            && string.IsNullOrWhiteSpace(targetCharacterId)
            && string.IsNullOrWhiteSpace(targetCharacterName))
        {
            error = "Character rumors require targetCharacterId or targetCharacterName.";
            return false;
        }

        return true;
    }

    public static bool ValidateRawJson(string json, out string error)
    {
        error = string.Empty;
        if (!HasRawInteger(json, nameof(targetFacilityId)))
        {
            error = "targetFacilityId must be a JSON number, not a string or null.";
            return false;
        }

        if (!Regex.IsMatch(
                json ?? string.Empty,
                $"\"{Regex.Escape(nameof(targetCharacterId))}\"\\s*:\\s*\"[^\"]*\""))
        {
            error = "targetCharacterId must be a JSON string, not a number or null.";
            return false;
        }

        if (!HasRawNumber(json, nameof(sentiment))
            || !HasRawNumber(json, nameof(spreadChance))
            || !HasRawNumber(json, nameof(trustImpact))
            || !HasRawNumber(json, nameof(validSeconds)))
        {
            error = "sentiment, spreadChance, trustImpact, and validSeconds must be JSON numbers, not strings or null.";
            return false;
        }

        return true;
    }

    public SocialRumor ToRuntimeRumor(string source, CharacterActor speaker, float now)
    {
        Enum.TryParse(rumorType, ignoreCase: true, out SocialRumorType parsedRumorType);
        Enum.TryParse(targetType, ignoreCase: true, out SocialRumorTargetType parsedTargetType);
        return new SocialRumor
        {
            type = parsedRumorType,
            targetType = parsedTargetType,
            sourceActorId = speaker != null && speaker.Identity != null
                ? speaker.Identity.PersistentId
                : string.Empty,
            sourceActorName = SocialRumorUtility.GetActorLabel(speaker),
            targetFacilityId = targetFacilityId,
            targetFacilityTag = targetFacilityTag,
            targetCharacterId = targetCharacterId,
            targetCharacterName = targetCharacterName,
            sentiment = Mathf.Clamp(sentiment, -1f, 1f),
            spreadChance = Mathf.Clamp01(spreadChance),
            trustImpact = Mathf.Clamp(trustImpact, -1f, 1f),
            validUntil = validSeconds > 0f ? now + validSeconds : 0f,
            summary = summary,
            source = source
        };
    }

    private static bool HasRawNumber(string json, string fieldName)
    {
        return !string.IsNullOrWhiteSpace(json)
            && Regex.IsMatch(json, string.Format(NumericFieldPattern, Regex.Escape(fieldName)));
    }

    private static bool HasRawInteger(string json, string fieldName)
    {
        return !string.IsNullOrWhiteSpace(json)
            && Regex.IsMatch(json, string.Format(IntegerFieldPattern, Regex.Escape(fieldName)));
    }
}

[Serializable]
public sealed class BubbleLineJsonDto : ILlmJsonPayload
{
    public string line;

    public bool Validate(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            error = "line is required.";
            return false;
        }

        if (line.Length > 80)
        {
            error = "line must be 80 characters or shorter.";
            return false;
        }

        return true;
    }
}
