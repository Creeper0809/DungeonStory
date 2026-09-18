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
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"presentationId\",\"displayName\",\"narrativeFlavor\"],\"properties\":{" +
        "\"presentationId\":{\"type\":\"string\",\"pattern\":\"^presentation:skill:[a-f0-9]{64}$\",\"maxLength\":96}," +
        "\"displayName\":{\"type\":\"string\",\"pattern\":\"^[^0-9０-９%％]*$\",\"minLength\":1,\"maxLength\":32}," +
        "\"narrativeFlavor\":{\"type\":\"string\",\"pattern\":\"^[^0-9０-９%％]*$\",\"minLength\":1,\"maxLength\":180}}}";

    private const string CharacterSkillLegacyV2Schema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"candidates\"],\"properties\":{" +
        "\"candidates\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":3,\"items\":{\"type\":\"object\",\"additionalProperties\":false," +
        "\"required\":[\"ruleId\",\"combinationId\",\"displayName\",\"description\",\"narrativeReason\"],\"properties\":{" +
        "\"ruleId\":{\"type\":\"string\",\"minLength\":1},\"combinationId\":{\"type\":\"string\",\"minLength\":1}," +
        "\"displayName\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":32}," +
        "\"description\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180}," +
        "\"narrativeReason\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180}}}}}}";

    private const string ModuleSelectionSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"selectionId\",\"positiveModuleIds\",\"drawbackModuleIds\",\"evidenceFactIds\",\"displayName\",\"narrativeFlavor\"],\"properties\":{" +
        "\"selectionId\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":96}," +
        "\"positiveModuleIds\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":3,\"items\":{\"type\":\"string\",\"minLength\":1}}," +
        "\"drawbackModuleIds\":{\"type\":\"array\",\"maxItems\":3,\"items\":{\"type\":\"string\",\"minLength\":1}}," +
        "\"evidenceFactIds\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":128,\"items\":{\"type\":\"string\",\"minLength\":1}}," +
        "\"displayName\":{\"type\":\"string\",\"pattern\":\"^[^0-9０-９%％]*$\",\"minLength\":1,\"maxLength\":32}," +
        "\"narrativeFlavor\":{\"type\":\"string\",\"pattern\":\"^[^0-9０-９%％]*$\",\"minLength\":1,\"maxLength\":180}}}";

    private const string PersonaSchema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"personaName\",\"flavorText\"],\"properties\":{" +
        "\"personaName\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":40},\"flavorText\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180}}}";

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

    private const string FacilityEvolutionLegacyV2Schema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"proposalIds\",\"mutationTags\",\"reasons\",\"flavorText\",\"confidence\"],\"properties\":{" +
        "\"proposalIds\":{\"type\":\"array\",\"maxItems\":16,\"items\":{\"type\":\"string\"}}," +
        "\"mutationTags\":{\"type\":\"array\",\"maxItems\":16,\"items\":{\"type\":\"string\"}}," +
        "\"reasons\":{\"type\":\"array\",\"maxItems\":16,\"items\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"proposalId\",\"reason\"],\"properties\":{\"proposalId\":{\"type\":\"string\"},\"reason\":{\"type\":\"string\",\"maxLength\":220}}}}," +
        "\"flavorText\":{\"type\":\"string\",\"maxLength\":260},\"confidence\":{\"type\":\"number\",\"minimum\":0,\"maximum\":1}}}";

    private const string EvolutionHistoryLegacyV2Schema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"requestKey\",\"targetPersistentId\",\"nodeId\",\"parentNodeId\",\"effectId\",\"effectBudget\",\"evidenceIds\",\"displayName\",\"description\",\"historyReason\"],\"properties\":{" +
        "\"requestKey\":{\"type\":\"string\"},\"targetPersistentId\":{\"type\":\"string\"},\"nodeId\":{\"type\":\"string\"},\"parentNodeId\":{\"type\":\"string\"},\"effectId\":{\"type\":\"string\"}," +
        "\"effectBudget\":{\"type\":\"integer\",\"minimum\":0},\"evidenceIds\":{\"type\":\"array\",\"maxItems\":64,\"items\":{\"type\":\"string\"}}," +
        "\"displayName\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":32},\"description\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180},\"historyReason\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180}}}";

    private const string EquipmentChoiceLegacyV2Schema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"selectedIndex\"],\"properties\":{" +
        "\"selectedIndex\":{\"type\":\"integer\",\"minimum\":0,\"maximum\":2}}}";

    private const string AcquiredTraitLegacyV2Schema =
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"combinationId\",\"displayName\",\"description\",\"narrativeReason\",\"evidenceFactIds\"],\"properties\":{" +
        "\"combinationId\":{\"type\":\"string\",\"minLength\":1},\"displayName\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":40}," +
        "\"description\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180},\"narrativeReason\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":180}," +
        "\"evidenceFactIds\":{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"maxItems\":64}}}";

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
        new("CharacterSkill", 3, CharacterSkillSchema, true),
        new("CharacterSkillModuleSelection", 4, ModuleSelectionSchema, true),
        new("CharacterSkillLegacyV2", 2, CharacterSkillLegacyV2Schema, true),
        new("Persona", 2, PersonaSchema, true),
        new("MacroGoal", 1, MacroGoalSchema, false),
        new("MoodImpulse", 1, MoodImpulseSchema, false),
        new("FacilityEvolution", 3, PresentationSchema("facility"), true),
        new("FacilityEvolutionModuleSelection", 4, ModuleSelectionSchema, true),
        new("FacilityEvolutionLegacyV2", 2, FacilityEvolutionLegacyV2Schema, true),
        new("EquipmentChoiceLegacyV2", 2, EquipmentChoiceLegacyV2Schema, false),
        new("EquipmentEvolutionModuleSelection", 4, ModuleSelectionSchema, true),
        new("EvolutionHistory", 3, PresentationSchema("equipment"), true),
        new("EvolutionHistoryLegacyV2", 2, EvolutionHistoryLegacyV2Schema, true),
        new("AcquiredTrait", 3, PresentationSchema("trait"), true),
        new("AcquiredTraitModuleSelection", 4, ModuleSelectionSchema, true),
        new("AcquiredTraitLegacyV2", 2, AcquiredTraitLegacyV2Schema, true),
        new("SocialRumor", 1, SocialRumorSchema, false),
        new("CharacterRecord", 1, CharacterRecordSchema, true),
        new("MultiPerspective", 1, MultiPerspectiveSchema, true),
        new("BubbleLine", 1, BubbleLineSchema, false)
    };

    private static readonly IReadOnlyDictionary<string, LlmStaticSchemaDefinition> ByProfile =
        Definitions.ToDictionary(value => value.ProfileId, StringComparer.Ordinal);

    public static IReadOnlyList<LlmStaticSchemaDefinition> All => Definitions;

    private static string PresentationSchema(string domain) =>
        "{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"presentationId\",\"displayName\",\"narrativeFlavor\"],\"properties\":{" +
        "\"presentationId\":{\"type\":\"string\",\"pattern\":\"^presentation:" + domain + ":[a-f0-9]{64}$\",\"maxLength\":96}," +
        "\"displayName\":{\"type\":\"string\",\"pattern\":\"^[^0-9０-９%％]*$\",\"minLength\":1,\"maxLength\":40}," +
        "\"narrativeFlavor\":{\"type\":\"string\",\"pattern\":\"^[^0-9０-９%％]*$\",\"minLength\":1,\"maxLength\":180}}}";

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
