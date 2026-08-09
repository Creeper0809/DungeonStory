using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public enum StructuredOutputCapability
{
    Unknown,
    Supported,
    Unavailable
}

public sealed class LlmStaticSchemaDefinition
{
    public LlmStaticSchemaDefinition(
        string profileId,
        int version,
        string json,
        bool persistentNarrative)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new ArgumentException("A static LLM schema requires a profile id.", nameof(profileId));
        }

        if (version <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        if (string.IsNullOrWhiteSpace(json)
            || json.IndexOf("\"type\":\"object\"", StringComparison.Ordinal) < 0
            || json.IndexOf("\"additionalProperties\":false", StringComparison.Ordinal) < 0
            || json.IndexOf("\"required\":[", StringComparison.Ordinal) < 0
            || json.IndexOf("\"properties\":{", StringComparison.Ordinal) < 0)
        {
            throw new ArgumentException(
                $"Static LLM schema '{profileId}' is not a closed object schema.",
                nameof(json));
        }

        ValidateStructuralJson(profileId, json);

        ProfileId = profileId.Trim();
        Version = version;
        Json = json;
        Utf8Bytes = Encoding.UTF8.GetBytes(json);
        Hash = ComputeHash(Utf8Bytes);
        PersistentNarrative = persistentNarrative;
    }

    public string ProfileId { get; }
    public int Version { get; }
    public string Json { get; }
    public byte[] Utf8Bytes { get; }
    public string Hash { get; }
    public bool PersistentNarrative { get; }

    private static string ComputeHash(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(bytes);
        StringBuilder builder = new StringBuilder(hash.Length * 2);
        for (int index = 0; index < hash.Length; index++)
        {
            builder.Append(hash[index].ToString("x2"));
        }
        return builder.ToString();
    }

    private static void ValidateStructuralJson(string profileId, string json)
    {
        Stack<char> stack = new Stack<char>();
        bool inString = false;
        bool escaped = false;
        for (int index = 0; index < json.Length; index++)
        {
            char value = json[index];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (value == '\\') escaped = true;
                else if (value == '"') inString = false;
                continue;
            }

            if (value == '"')
            {
                inString = true;
            }
            else if (value == '{' || value == '[')
            {
                stack.Push(value);
            }
            else if (value == '}' || value == ']')
            {
                char expected = value == '}' ? '{' : '[';
                if (stack.Count == 0 || stack.Pop() != expected)
                {
                    throw new ArgumentException(
                        $"Static LLM schema '{profileId}' has mismatched JSON delimiters.",
                        nameof(json));
                }
            }
        }

        if (inString || escaped || stack.Count != 0)
        {
            throw new ArgumentException(
                $"Static LLM schema '{profileId}' has incomplete JSON structure.",
                nameof(json));
        }
    }
}

public static class LlmStaticSchemaCatalog
{
    private const string ReferenceProperties =
        "\"usedMotifIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"pattern\":\"^M[0-9]{2}$\",\"maxLength\":3},\"maxItems\":3}," +
        "\"usedCharacterFactIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\",\"pattern\":\"^F[0-9]{2}$\",\"maxLength\":3},\"maxItems\":4}";

    private const string RequiredReferenceProperties =
        "\"usedMotifIds\":{\"type\":\"array\",\"minItems\":1,\"items\":{\"type\":\"string\",\"pattern\":\"^M[0-9]{2}$\",\"maxLength\":3},\"maxItems\":3}," +
        "\"usedCharacterFactIds\":{\"type\":\"array\",\"minItems\":1,\"items\":{\"type\":\"string\",\"pattern\":\"^F[0-9]{2}$\",\"maxLength\":3},\"maxItems\":4}";

    private const string CharacterSkillSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"candidates\",\"usedMotifIds\",\"usedCharacterFactIds\"],\"properties\":{" +
        "\"candidates\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":3,\"items\":{\"type\":\"object\",\"additionalProperties\":false," +
        "\"required\":[\"index\",\"name\",\"description\",\"narrativeReason\",\"trigger\",\"target\",\"ultimateDomain\",\"cooldownTurns\",\"combinationId\"],\"properties\":{" +
        "\"index\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":2},\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":32}," +
        "\"description\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180},\"narrativeReason\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180}," +
        "\"trigger\":{\"type\":\"string\"},\"target\":{\"type\":\"string\"},\"ultimateDomain\":{\"type\":\"string\",\"enum\":[\"None\",\"Offense\",\"Defense\",\"Management\"]}," +
        "\"cooldownTurns\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":999},\"combinationId\":{\"type\":\"string\"}," +
        "\"modules\":{\"type\":\"array\",\"maxItems\":8,\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"pairId\",\"moduleId\",\"variantId\"],\"properties\":{" +
        "\"pairId\":{\"type\":\"string\"},\"moduleId\":{\"type\":\"string\"},\"variantId\":{\"type\":\"string\"}}}}}}}," + RequiredReferenceProperties + "}}";

    private const string PersonaSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"traitName\",\"flavorText\",\"selfCareMultiplier\",\"curiosityMultiplier\",\"shoppingMultiplier\",\"patienceMultiplier\",\"hungerCurveMultiplier\",\"funCurveMultiplier\",\"moodCurveMultiplier\",\"preferredFacilityTags\",\"usedMotifIds\",\"usedCharacterFactIds\"],\"properties\":{" +
        "\"traitName\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":40},\"flavorText\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180}," +
        "\"selfCareMultiplier\":{\"type\":\"number\",\"minimum\":0.25,\"maximum\":2},\"curiosityMultiplier\":{\"type\":\"number\",\"minimum\":0.25,\"maximum\":2}," +
        "\"shoppingMultiplier\":{\"type\":\"number\",\"minimum\":0.25,\"maximum\":2},\"patienceMultiplier\":{\"type\":\"number\",\"minimum\":0.25,\"maximum\":2}," +
        "\"hungerCurveMultiplier\":{\"type\":\"number\",\"minimum\":0.25,\"maximum\":2},\"funCurveMultiplier\":{\"type\":\"number\",\"minimum\":0.25,\"maximum\":2}," +
        "\"moodCurveMultiplier\":{\"type\":\"number\",\"minimum\":0.25,\"maximum\":2},\"preferredFacilityTags\":{\"type\":\"array\",\"maxItems\":12,\"items\":{\"type\":\"string\"}}," + RequiredReferenceProperties + "}}";

    private const string MacroGoalSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"macroGoal\",\"reason\",\"targetFacilityId\",\"targetFacilityTag\",\"validSeconds\"],\"properties\":{" +
        "\"macroGoal\":{\"type\":\"string\",\"enum\":[\"Continue\",\"SeekFood\",\"SeekToilet\",\"SeekHygiene\",\"SeekFun\",\"AvoidFacility\",\"Complain\",\"ExitDungeon\",\"Vandalize\"]}," +
        "\"reason\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180},\"targetFacilityId\":{\"type\":\"integer\",\"minimum\":-1},\"targetFacilityTag\":{\"type\":\"string\"}," +
        "\"validSeconds\":{\"type\":\"number\",\"minimum\":1,\"maximum\":600}," + ReferenceProperties + "}}";

    private const string MoodImpulseSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"moodImpulse\",\"strength\",\"targetFacilityId\",\"targetFacilityTag\",\"reason\",\"validSeconds\"],\"properties\":{" +
        "\"moodImpulse\":{\"type\":\"string\",\"enum\":[\"None\",\"FollowRoutine\",\"SeekFood\",\"SeekRest\",\"SeekToilet\",\"SeekHygiene\",\"SeekFun\",\"ImpulseShopping\",\"Wander\",\"Wait\",\"IgnoreDuty\",\"AvoidFacility\",\"Complain\",\"ExitDungeon\",\"Vandalize\"]}," +
        "\"strength\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1},\"targetFacilityId\":{\"type\":\"integer\",\"minimum\":-1},\"targetFacilityTag\":{\"type\":\"string\"}," +
        "\"reason\":{\"type\":\"string\",\"maxLength\":180},\"validSeconds\":{\"type\":\"number\",\"minimum\":1,\"maximum\":300}," + ReferenceProperties + "}}";

    private const string SocialRumorSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"rumorType\",\"targetType\",\"targetFacilityId\",\"targetFacilityTag\",\"targetCharacterId\",\"targetCharacterName\",\"sentiment\",\"summary\",\"spreadChance\",\"trustImpact\",\"validSeconds\"],\"properties\":{" +
        "\"rumorType\":{\"type\":\"string\",\"enum\":[\"None\",\"Complaint\",\"Recommendation\",\"Warning\",\"Praise\"]},\"targetType\":{\"type\":\"string\",\"enum\":[\"None\",\"Facility\",\"Character\"]}," +
        "\"targetFacilityId\":{\"type\":\"integer\",\"minimum\":-1},\"targetFacilityTag\":{\"type\":\"string\"},\"targetCharacterId\":{\"type\":\"string\"},\"targetCharacterName\":{\"type\":\"string\"}," +
        "\"sentiment\":{\"type\":\"number\",\"minimum\":-1,\"maximum\":1},\"summary\":{\"type\":\"string\",\"maxLength\":160},\"spreadChance\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1}," +
        "\"trustImpact\":{\"type\":\"number\",\"minimum\":-1,\"maximum\":1},\"validSeconds\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1800}," + ReferenceProperties + "}}";

    private const string FacilityEvolutionSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"facilityIdentitySummary\",\"proposalIds\",\"reasons\",\"rejectedHints\",\"rejectedHintText\",\"mutationTagSuggestions\",\"flavorText\",\"confidence\",\"usedMotifIds\",\"usedCharacterFactIds\"],\"properties\":{" +
        "\"facilityIdentitySummary\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":160},\"proposalIds\":{\"type\":\"array\",\"maxItems\":16,\"items\":{\"type\":\"string\"}}," +
        "\"reasons\":{\"type\":\"array\",\"maxItems\":16,\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"id\",\"reason\"],\"properties\":{\"id\":{\"type\":\"string\"},\"reason\":{\"type\":\"string\",\"maxLength\":220}}}}," +
        "\"rejectedHints\":{\"type\":\"array\",\"maxItems\":16,\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"id\",\"reason\"],\"properties\":{\"id\":{\"type\":\"string\"},\"reason\":{\"type\":\"string\",\"maxLength\":220}}}}," +
        "\"rejectedHintText\":{\"type\":\"string\",\"maxLength\":220},\"mutationTagSuggestions\":{\"type\":\"array\",\"maxItems\":16,\"items\":{\"type\":\"string\"}}," +
        "\"flavorText\":{\"type\":\"string\",\"maxLength\":260},\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1}," + ReferenceProperties + "}}";

    private const string EvolutionHistorySchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"requestKey\",\"targetPersistentId\",\"nodeId\",\"parentNodeId\",\"effectId\",\"effectBudget\",\"evidenceIds\",\"displayName\",\"description\",\"historyReason\",\"usedMotifIds\",\"usedCharacterFactIds\"],\"properties\":{" +
        "\"requestKey\":{\"type\":\"string\"},\"targetPersistentId\":{\"type\":\"string\"},\"nodeId\":{\"type\":\"string\"},\"parentNodeId\":{\"type\":\"string\"},\"effectId\":{\"type\":\"string\"}," +
        "\"effectBudget\":{\"type\":\"integer\",\"minimum\":0},\"evidenceIds\":{\"type\":\"array\",\"maxItems\":64,\"items\":{\"type\":\"string\"}}," +
        "\"displayName\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":32},\"description\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180},\"historyReason\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180}," + ReferenceProperties + "}}";

    private const string CharacterRecordSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"line\",\"usedMotifIds\",\"usedCharacterFactIds\"],\"properties\":{\"line\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":60}," + RequiredReferenceProperties + "}}";

    private const string BubbleLineSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"line\"],\"properties\":{\"line\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":80}}}";

    private const string MultiPerspectiveSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"eventId\",\"perspectives\",\"usedMotifIds\",\"usedCharacterFactIds\"],\"properties\":{" +
        "\"eventId\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":96},\"perspectives\":{\"type\":\"array\",\"minItems\":2,\"maxItems\":4,\"items\":{\"type\":\"object\",\"additionalProperties\":false," +
        "\"required\":[\"viewpointCharacterId\",\"line\"],\"properties\":{\"viewpointCharacterId\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":96},\"line\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":320}}}}," + ReferenceProperties + "}}";

    private static readonly LlmStaticSchemaDefinition[] Definitions =
    {
        new("CharacterSkill", 1, CharacterSkillSchema, true),
        new("Persona", 1, PersonaSchema, true),
        new("MacroGoal", 1, MacroGoalSchema, false),
        new("MoodImpulse", 1, MoodImpulseSchema, false),
        new("FacilityEvolution", 1, FacilityEvolutionSchema, true),
        new("EvolutionHistory", 1, EvolutionHistorySchema, true),
        new("SocialRumor", 1, SocialRumorSchema, false),
        new("CharacterRecord", 1, CharacterRecordSchema, true),
        new("MultiPerspective", 1, MultiPerspectiveSchema, true),
        new("BubbleLine", 1, BubbleLineSchema, false)
    };

    private static readonly IReadOnlyDictionary<string, LlmStaticSchemaDefinition> ByProfile =
        Definitions.ToDictionary(value => value.ProfileId, StringComparer.Ordinal);

    public static IReadOnlyList<LlmStaticSchemaDefinition> All => Definitions;

    public static LlmStaticSchemaDefinition Require(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId)
            || !ByProfile.TryGetValue(profileId.Trim(), out LlmStaticSchemaDefinition definition))
        {
            throw new InvalidOperationException(
                $"Local LLM profile '{profileId ?? string.Empty}' has no static schema.");
        }

        return definition;
    }
}
