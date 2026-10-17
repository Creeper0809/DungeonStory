using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Protocol adapter for the bundled CPU-only llama.cpp server. It intentionally
/// does not implement Ollama's API; the game only talks to its authenticated
/// loopback process and keeps all gameplay authority in C#.
/// </summary>
public interface IEquipmentChoiceStructuredBackend
{
    UnityWebRequest BuildChoiceRequest(
        string configuredEndpoint,
        string requestKey,
        ChoicePromptDiagnostic prompt,
        int candidateCount);

    bool TryExtractChoice(
        string responseJson,
        int candidateCount,
        out int selectedIndex,
        out string error);
}

public sealed class DungeonStoryHostStructuredChatBackend :
    ILocalLlmStructuredBackend,
    IEquipmentChoiceStructuredBackend
{
    private const string SystemInstruction =
        "Return exactly one compact JSON object matching the supplied schema. " +
        "Write player-facing prose in Korean. Do not reveal hidden reasoning, " +
        "markdown, invented ids, or extra keys. Copy only ids explicitly supplied by the request.";

    private readonly Func<string> sessionTokenProvider;

    public DungeonStoryHostStructuredChatBackend(Func<string> sessionTokenProvider = null)
    {
        this.sessionTokenProvider = sessionTokenProvider;
    }

    public string ResolveEndpoint(string configuredEndpoint)
    {
        string value = (configuredEndpoint ?? string.Empty).Trim().TrimEnd('/');
        if (value.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value + "/v1/chat/completions";
    }

    public UnityWebRequest BuildRequest(
        string configuredEndpoint,
        string model,
        LocalLlmRequestProfile profile,
        LlmStaticSchemaDefinition schema,
        string prompt)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }
        if (schema == null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        string endpoint = ResolveEndpoint(configuredEndpoint);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("DungeonStory local host endpoint is not available.");
        }

        byte[] payload = BuildRequestPayload(model, profile, schema, prompt);

        UnityWebRequest request = new UnityWebRequest(endpoint, "POST")
        {
            uploadHandler = new UploadHandlerRaw(payload),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        string token = sessionTokenProvider?.Invoke() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
        }
        return request;
    }

    /// <summary>
    /// Builds the exact production request body without performing I/O. Export
    /// and replay tools must call this same serializer instead of reconstructing
    /// prompts or JSON schema envelopes independently.
    /// </summary>
    public static byte[] BuildRequestPayload(
        string model,
        LocalLlmRequestProfile profile,
        LlmStaticSchemaDefinition schema,
        string prompt)
    {
        if (profile == null)
            throw new ArgumentNullException(nameof(profile));
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        // CharacterSkill selection already carries the same validated facts as
        // request-local Fxx/Mxx lines and as canonical public JSON. Keep the
        // original queue prompt for audit, but do not pay the model context cost
        // twice for this latency-sensitive persistent profile.
        bool suppressThinking = string.Equals(
                profile.Id,
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                StringComparison.Ordinal);
        bool compactPublicContext = string.Equals(profile.Id,
            LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id, StringComparison.Ordinal);
        bool compactEquipmentContext = string.Equals(profile.Id,
            LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id, StringComparison.Ordinal);
        bool facilityModuleSelection = string.Equals(profile.Id,
            LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id, StringComparison.Ordinal);
        JObject skillTable = null;
        string modelPrompt = compactEquipmentContext
            ? NarrativeRequestContext.ToCompactEvolutionModelPrompt(prompt)
            : compactPublicContext
            ? NarrativeRequestContext.ToCompactTraitModelPrompt(prompt)
            : suppressThinking
            ? CharacterSkillCompactModelPrompt.Build(prompt, out skillTable)
            : NarrativeRequestContext.ToModelPrompt(prompt);
        if (suppressThinking || compactPublicContext || compactEquipmentContext)
        {
            modelPrompt = "/no_think\n" + modelPrompt;
        }
        byte[] requestSchema = compactPublicContext
            ? BuildTraitSelectionRequestSchema(schema, prompt)
            : facilityModuleSelection ? BuildFacilitySelectionRequestSchema(schema, prompt)
            : compactEquipmentContext ? BuildEquipmentSelectionRequestSchema(schema)
            : suppressThinking ? BuildSkillSelectionRequestSchema(schema, skillTable, prompt) : schema.Utf8Bytes;
        StringBuilder prefix = new StringBuilder((modelPrompt?.Length ?? 0) + 640);
        prefix.Append("{\"model\":");
        AppendJsonString(prefix, model ?? string.Empty);
        prefix.Append(",\"stream\":false,\"cache_prompt\":true,\"messages\":[{\"role\":\"system\",\"content\":");
        AppendJsonString(prefix, SystemInstruction);
        prefix.Append("},{\"role\":\"user\",\"content\":");
        AppendJsonString(prefix, modelPrompt ?? string.Empty);
        prefix.Append("}],\"chat_template_kwargs\":{\"enable_thinking\":false},");
        prefix.Append("\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{\"name\":");
        AppendJsonString(prefix, "DungeonStory_" + schema.ProfileId);
        prefix.Append(",\"strict\":true,\"schema\":");

        StringBuilder suffix = new StringBuilder(192);
        suffix.Append("}},\"temperature\":");
        suffix.Append(profile.Temperature.ToString("0.###", CultureInfo.InvariantCulture));
        suffix.Append(",\"max_tokens\":");
        suffix.Append(profile.MaxOutputTokens);
        suffix.Append("}");

        byte[] prefixBytes = Encoding.UTF8.GetBytes(prefix.ToString());
        byte[] suffixBytes = Encoding.UTF8.GetBytes(suffix.ToString());
        byte[] payload;
        using (MemoryStream stream = new MemoryStream(
                   prefixBytes.Length + requestSchema.Length + suffixBytes.Length))
        {
            stream.Write(prefixBytes, 0, prefixBytes.Length);
            stream.Write(requestSchema, 0, requestSchema.Length);
            stream.Write(suffixBytes, 0, suffixBytes.Length);
            payload = stream.ToArray();
        }

        return payload;
    }

    public static bool TryResolveModelContent(LocalLlmRequestProfile profile, string prompt,
        string content, out string resolved, out string error)
    {
        resolved = string.Empty; error = string.Empty;
        try
        {
            resolved = profile.Id == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
                ? CharacterSkillCompactModelPrompt.Resolve(prompt, content)
                : profile.Id == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id
                    ? NarrativeRequestContext.ResolveTraitEvidenceReferences(prompt, content) : content;
            return true;
        }
        catch (Exception failure) { error = failure.Message; return false; }
    }

    private static byte[] BuildSkillSelectionRequestSchema(LlmStaticSchemaDefinition schema, JObject table, string prompt)
    {
        // The bundled converter supports fixed tuple items, not contains/if.
        // Keep every existing offer; never choose a module or repair a response.
        JObject narrowed = JObject.Parse(Encoding.UTF8.GetString(schema.Utf8Bytes));
        JObject candidates = (JObject)narrowed["properties"]["candidates"];
        JObject template = (JObject)candidates["items"];
        UseJsonSafeProseGrammar((JObject)template["properties"],
            ReadSkillCharacterName(prompt, table != null), singleLine: true,
            excludeUnicodeNumbers: true, requireTerminalPeriod: true);
        if (table == null)
            return Encoding.UTF8.GetBytes(narrowed.ToString(Formatting.None));
        ((JArray)narrowed["required"]).Add("requestRef");
        narrowed["properties"]["requestRef"] = new JObject
        {
            ["type"] = "string", ["enum"] = new JArray(CharacterSkillCompactModelPrompt.RequestRef(prompt))
        };
        var tuple = new JArray();
        int candidateIndex = 0;
        foreach (JArray row in (JArray)table["C"])
        {
            JObject item = (JObject)template.DeepClone();
            JObject properties = (JObject)item["properties"];
            properties["selectionId"]["enum"] = new JArray("S" + candidateIndex++);
            properties["positiveModuleIds"]["items"]["enum"] = new JArray(
                ((JArray)row[3]).Values<int>().Select(index => (string)table["M"][index][0]));
            if (((JArray)row[4]).Count == 0) properties["drawbackModuleIds"]["maxItems"] = 0;
            else properties["drawbackModuleIds"]["items"]["enum"] = new JArray(
                ((JArray)row[4]).Values<int>().Select(index => (string)table["M"][index][0]));
            properties["evidenceFactIds"]["items"]["enum"] = new JArray(
                ((JArray)row[5]).Values<int>().Select(index => "E" + index));
            tuple.Add(item);
        }
        candidates["minItems"] = tuple.Count;
        candidates["maxItems"] = tuple.Count;
        candidates["items"] = tuple;
        candidates["additionalItems"] = false;
        return Encoding.UTF8.GetBytes(narrowed.ToString(Formatting.None));
    }

    private static byte[] BuildEquipmentSelectionRequestSchema(LlmStaticSchemaDefinition schema)
    {
        JObject narrowed = JObject.Parse(Encoding.UTF8.GetString(schema.Utf8Bytes));
        UseJsonSafeProseGrammar((JObject)narrowed["properties"]);
        return Encoding.UTF8.GetBytes(narrowed.ToString(Formatting.None));
    }

    private static byte[] BuildFacilitySelectionRequestSchema(
        LlmStaticSchemaDefinition schema, string prompt)
    {
        // Constrain identity at generation, not by repairing the response. All
        // offered modules/evidence and the production commit guards stay intact.
        int contextStart = (prompt ?? string.Empty).IndexOf(
            NarrativeRequestContext.BeginMarker, StringComparison.Ordinal);
        string header = contextStart < 0 ? prompt ?? string.Empty : prompt.Substring(0, contextStart);
        const string prefix = "selectionId=";
        string[] matches = header.Split('\n').Select(value => value.TrimEnd('\r'))
            .Where(value => value.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException("Facility request requires one exact selectionId header.");
        string selectionId = matches[0].Substring(prefix.Length);
        if (string.IsNullOrWhiteSpace(selectionId)
            || !string.Equals(selectionId, selectionId.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Facility request requires a nonempty canonical selectionId.");
        JObject narrowed = JObject.Parse(Encoding.UTF8.GetString(schema.Utf8Bytes));
        UseJsonSafeProseGrammar((JObject)narrowed["properties"]);
        narrowed["properties"]["selectionId"]["enum"] = new JArray(selectionId);
        return Encoding.UTF8.GetBytes(narrowed.ToString(Formatting.None));
    }

    private static byte[] BuildTraitSelectionRequestSchema(
        LlmStaticSchemaDefinition schema, string prompt)
    {
        // Narrow generation to the existing request's offers, not a preferred
        // answer. C# still validates budgets, evidence binding and every commit.
        int contextStart = (prompt ?? string.Empty).IndexOf(
            NarrativeRequestContext.BeginMarker, StringComparison.Ordinal);
        string header = contextStart < 0 ? prompt ?? string.Empty : prompt.Substring(0, contextStart);
        string[] lines = header.Split('\n').Select(value => value.Trim()).ToArray();
        string ExactValue(string key)
        {
            string[] matches = lines.Where(value => value.StartsWith(key + "=", StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1 || matches[0].Length == key.Length + 1)
                throw new InvalidOperationException("Trait request requires one nonempty " + key + " header.");
            return matches[0].Substring(key.Length + 1);
        }
        string selectionId = ExactValue("selectionId");
        string[] evidence = ExactValue("evidenceFactIds").Split(',').Select(value => value.Trim()).ToArray();
        string[][] offers = lines.Where(value => value.StartsWith("- moduleId=", StringComparison.Ordinal))
            .Select(value => value.Split(new[] { "; polarity=", "; meaning=" }, StringSplitOptions.None))
            .ToArray();
        if (evidence.Any(string.IsNullOrWhiteSpace)
            || evidence.Distinct(StringComparer.Ordinal).Count() != evidence.Length
            || offers.Length == 0
            || offers.Any(value => value.Length != 3 || value[0].Length <= 11
                || (value[1] != "Positive" && value[1] != "Drawback"))
            || offers.Select(value => value[0]).Distinct(StringComparer.Ordinal).Count() != offers.Length)
            throw new InvalidOperationException("Trait request has invalid or duplicate evidence/module offers.");
        string[] positive = offers.Where(value => value[1] == "Positive")
            .Select(value => value[0].Substring("- moduleId=".Length)).ToArray();
        string[] drawback = offers.Where(value => value[1] == "Drawback")
            .Select(value => value[0].Substring("- moduleId=".Length)).ToArray();
        if (positive.Length == 0)
            throw new InvalidOperationException("Trait request has no positive module offer.");

        JObject narrowed = JObject.Parse(Encoding.UTF8.GetString(schema.Utf8Bytes));
        JObject properties = (JObject)narrowed["properties"];
        // Trait currently rejects only ASCII/fullwidth digits in its DTO.
        // Do not silently change that gameplay acceptance contract here.
        UseJsonSafeProseGrammar(properties, excludeUnicodeDigits: false);
        properties["selectionId"]["enum"] = new JArray(selectionId);
        properties["positiveModuleIds"]["minItems"] = 1;
        properties["positiveModuleIds"]["maxItems"] = 1;
        properties["positiveModuleIds"]["items"]["enum"] = new JArray(positive);
        properties["drawbackModuleIds"]["maxItems"] = drawback.Length == 0 ? 0 : 1;
        if (drawback.Length > 0)
            properties["drawbackModuleIds"]["items"]["enum"] = new JArray(drawback);
        properties["evidenceFactIds"]["maxItems"] = evidence.Length;
        properties["evidenceFactIds"]["items"]["enum"] = new JArray(evidence);
        string[] gameplayHeaders = lines.Where(value => value.StartsWith(
            "gameplayEvidenceFactIds=", StringComparison.Ordinal)).ToArray();
        if (gameplayHeaders.Length > 0)
        {
            string[] gameplay = ExactValue("gameplayEvidenceFactIds").Split(',');
            if (gameplay.Any(value => string.IsNullOrWhiteSpace(value) || value != value.Trim())
                || gameplay.Distinct(StringComparer.Ordinal).Count() != gameplay.Length
                || gameplay.Any(value => !evidence.Contains(value, StringComparer.Ordinal)))
                throw new InvalidOperationException("Trait gameplay evidence is invalid, duplicated or not offered.");
            JObject references = NarrativeRequestContext.TraitEvidenceReferences(prompt);
            string ReferenceFor(string id) => references.Properties()
                .Single(value => (string)value.Value == id).Name;
            string[] evidenceReferences = evidence.Select(ReferenceFor).ToArray();
            properties["evidenceFactIds"]["items"]["enum"] = new JArray(evidenceReferences);
            ((JArray)narrowed["required"]).Add("requestRef");
            properties["requestRef"] = new JObject
            {
                ["type"] = "string", ["enum"] = new JArray(CharacterSkillCompactModelPrompt.RequestRef(prompt))
            };
            RequireTraitGameplayEvidence(narrowed, evidenceReferences, gameplay.Select(ReferenceFor).ToArray());
        }
        return Encoding.UTF8.GetBytes(narrowed.ToString(Formatting.None));
    }

    private static string ReadSkillCharacterName(string prompt, bool required)
    {
        int contextStart = (prompt ?? string.Empty).IndexOf(
            NarrativeRequestContext.BeginMarker, StringComparison.Ordinal);
        string header = contextStart < 0 ? prompt ?? string.Empty : prompt.Substring(0, contextStart);
        string[] names = header.Split('\n').Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("character=", StringComparison.Ordinal))
            .Select(line => line.Substring("character=".Length)).ToArray();
        if (names.Length > 1 || required && (names.Length != 1 || string.IsNullOrWhiteSpace(names[0])))
            throw new InvalidOperationException("Skill request requires one nonempty character header.");
        return names.Length == 1 ? names[0] : null;
    }

    private static void UseJsonSafeProseGrammar(
        JObject properties, string authoritativeName = null, bool singleLine = false,
        bool excludeUnicodeDigits = true, bool excludeUnicodeNumbers = false,
        bool requireTerminalPeriod = false)
    {
        // b10331 inserts pattern between JSON quotes, not over decoded text.
        // Its request-local lexical dialect must therefore admit JSON escapes
        // explicitly and must not admit bare quotes, backslashes or controls.
        // Exclude the profile's existing C# digit/percent ranges even when
        // unicode-escaped. Decoded C# guards (including canonical text,
        // exact name boundaries and character lengths) remain
        // the sole acceptance authority; no reply or static schema is repaired.
        string lowUnicode = singleLine
            ? "00([14-9A-Fa-f][0-9A-Fa-f]|0[0-9BCEFbcef])"
            : "00[014-9A-Fa-f][0-9A-Fa-f]";
        string unicode = @"\\u([1-9A-Ea-e][0-9A-Fa-f]{3}|0[1-9A-Fa-f][0-9A-Fa-f]{2}|"
            + lowUnicode + @"|002[0-46-9A-Fa-f]|003[A-Fa-f]|"
            + @"[Ff][0-9A-Ea-e][0-9A-Fa-f]{2}|[Ff][Ff][2-9A-Fa-f][0-9A-Fa-f]|"
            + @"[Ff][Ff]0[0-46-9A-Fa-f]|[Ff][Ff]1[A-Fa-f])";
        string digits = "0-9０-９";
        if (excludeUnicodeDigits || excludeUnicodeNumbers)
        {
            // Skill prose also excludes BMP NumberLetter/OtherNumber bypasses.
            // Other profiles retain their decimal-digit policy. Supplementary
            // numbers and exact name boundaries still use the decoded C# guard.
            digits = BuildBmpDigitExclusions(excludeUnicodeNumbers);
            unicode = @"\\u" + BuildProseUnicodeEscapeBody(0, 4, singleLine, excludeUnicodeNumbers);
        }
        string unit = @"[^""\\\x7F\x00-\x1F" + digits + @"%％]|"
            + (singleLine ? @"\\[""\\/bft]" : @"\\[""\\/bfnrt]") + "|" + unicode;
        if (!string.IsNullOrWhiteSpace(authoritativeName)
            && authoritativeName.Any(character => excludeUnicodeNumbers
                ? char.IsNumber(character) : char.IsDigit(character))
            && authoritativeName.Any(char.IsLetter)
            && authoritativeName.IndexOf('%') < 0 && authoritativeName.IndexOf('％') < 0)
        {
            // Preserve the already-authorized numeric-name exception, not a
            // general digit allowance. C# still checks its exact word boundary.
            string encoded = JsonConvert.ToString(authoritativeName);
            StringBuilder literal = new StringBuilder();
            foreach (char character in encoded.Substring(1, encoded.Length - 2))
            {
                if ("\\.^$|?*+()[]{}".IndexOf(character) >= 0) literal.Append('\\');
                literal.Append(character);
            }
            unit += "|" + literal;
        }
        foreach (string field in new[] { "displayName", "narrativeFlavor" })
        {
            JObject definition = (JObject)properties[field];
            if (requireTerminalPeriod && field == "narrativeFlavor")
            {
                // Reserve one decoded character for the literal/escaped period;
                // no reply rewriting or change to the static length contract.
                definition["pattern"] = "^(" + unit + "){"
                    + Math.Max(0, (int)definition["minLength"] - 1) + ","
                    + ((int)definition["maxLength"] - 1) + @"}(\.|\\u002[Ee])$";
                continue;
            }
            definition["pattern"] = "^(" + unit + "){"
                + (int)definition["minLength"] + "," + (int)definition["maxLength"] + "}$";
        }
    }

    private static string BuildBmpDigitExclusions(bool includeOtherNumbers)
    {
        var result = new StringBuilder();
        for (int value = 0; value <= char.MaxValue; value++)
        {
            if (!(includeOtherNumbers ? char.IsNumber((char)value) : char.IsDigit((char)value))) continue;
            int first = value;
            while (value < char.MaxValue && (includeOtherNumbers
                ? char.IsNumber((char)(value + 1)) : char.IsDigit((char)(value + 1)))) value++;
            result.Append((char)first);
            if (value != first) result.Append('-').Append((char)value);
        }
        return result.ToString();
    }

    // b10331 has no lookahead. Build the complement as a compact four-nibble
    // trie so escaped BMP digits/numbers cannot bypass the profile's guard.
    private static string BuildProseUnicodeEscapeBody(
        int prefix, int remaining, bool singleLine, bool includeOtherNumbers)
    {
        int first = prefix << (remaining * 4);
        int count = 1 << (remaining * 4);
        int forbidden = 0;
        for (int value = first; value < first + count; value++)
        {
            char character = (char)value;
            if ((includeOtherNumbers ? char.IsNumber(character) : char.IsDigit(character))
                || character == '%' || character == '％'
                || singleLine && (character == '\r' || character == '\n')) forbidden++;
        }
        if (forbidden == count) return null;
        if (remaining == 0) return string.Empty;
        if (forbidden == 0)
            return "[0-9A-Fa-f]" + (remaining == 1 ? string.Empty : "{" + remaining + "}");
        var groups = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int nibble = 0; nibble < 16; nibble++)
        {
            string suffix = BuildProseUnicodeEscapeBody(
                (prefix << 4) | nibble, remaining - 1, singleLine, includeOtherNumbers);
            if (suffix == null) continue;
            groups.TryGetValue(suffix, out int mask);
            groups[suffix] = mask | (1 << nibble);
        }
        string[] alternatives = groups.OrderBy(group => group.Value)
            .Select(group => ProseHexChoices(group.Value) + group.Key).ToArray();
        return alternatives.Length == 1 ? alternatives[0] : "(" + string.Join("|", alternatives) + ")";
    }

    private static string ProseHexChoices(int mask)
    {
        const string Upper = "0123456789ABCDEF";
        const string Lower = "0123456789abcdef";
        var characters = new StringBuilder();
        for (int nibble = 0; nibble < 16; nibble++)
        {
            if ((mask & (1 << nibble)) == 0) continue;
            int first = nibble;
            // Split at 9/A: a regex character range must not span punctuation.
            while (nibble + 1 < (first < 10 ? 10 : 16)
                && (mask & (1 << (nibble + 1))) != 0) nibble++;
            characters.Append(Upper[first]);
            if (first != nibble) characters.Append('-').Append(Upper[nibble]);
            if (first >= 10)
            {
                characters.Append(Lower[first]);
                if (first != nibble) characters.Append('-').Append(Lower[nibble]);
            }
        }
        return characters.Length == 1 ? characters.ToString() : "[" + characters + "]";
    }

    private static void RequireTraitGameplayEvidence(
        JObject requestSchema, string[] evidence, string[] gameplay)
    {
        // b10331 ignores contains. Its supported fixed tuples instead describe
        // every possible first gameplay-evidence position, without choosing a
        // module, a particular fact or a response. C# remains commit authority.
        string[] background = evidence.Except(gameplay, StringComparer.Ordinal).ToArray();
        JObject EvidenceDefinition(string[] ids) => new()
            { ["type"] = "string", ["minLength"] = 1, ["enum"] = new JArray(ids) };
        JObject Reference(string name) => new() { ["$ref"] = "#/$defs/" + name };
        var definitions = new JObject
        {
            ["trait-evidence-all"] = EvidenceDefinition(evidence),
            ["trait-evidence-gameplay"] = EvidenceDefinition(gameplay)
        };
        if (background.Length > 0)
            definitions["trait-evidence-nongameplay"] = EvidenceDefinition(background);
        requestSchema["$defs"] = definitions;
        var alternatives = new JArray();
        for (int length = 1; length <= evidence.Length; length++)
        {
            int positions = background.Length == 0 ? 1 : length;
            for (int firstGameplay = 0; firstGameplay < positions; firstGameplay++)
            {
                var tuple = new JArray();
                for (int index = 0; index < length; index++)
                    tuple.Add(Reference(index < firstGameplay
                        ? "trait-evidence-nongameplay"
                        : index == firstGameplay ? "trait-evidence-gameplay" : "trait-evidence-all"));
                alternatives.Add(new JObject
                {
                    ["type"] = "array", ["minItems"] = length, ["maxItems"] = length,
                    ["items"] = tuple, ["additionalItems"] = false
                });
            }
        }
        requestSchema["properties"]["evidenceFactIds"]["anyOf"] = alternatives;
    }

    public bool TryExtractContent(string responseJson, out string content, out string error)
    {
        content = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            error = "DungeonStory host response is empty.";
            return false;
        }

        LlamaCppChatResponse response;
        try
        {
            response = JsonUtility.FromJson<LlamaCppChatResponse>(responseJson);
        }
        catch (Exception exception)
        {
            error = "DungeonStory host response parse failed: " + exception.Message;
            return false;
        }

        if (response?.choices == null
            || response.choices.Length == 0
            || string.IsNullOrWhiteSpace(response.choices[0]?.message?.content))
        {
            error = response?.error?.message ?? "Bundled llama.cpp host returned no content.";
            return false;
        }

        content = response.choices[0].message.content;
        return true;
    }

    public UnityWebRequest BuildChoiceRequest(
        string configuredEndpoint,
        string requestKey,
        ChoicePromptDiagnostic prompt,
        int candidateCount)
    {
        string baseEndpoint = (configuredEndpoint ?? string.Empty).Trim().TrimEnd('/');
        if (baseEndpoint.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            baseEndpoint = baseEndpoint.Substring(
                0,
                baseEndpoint.Length - "/v1/chat/completions".Length);
        }
        if (string.IsNullOrWhiteSpace(baseEndpoint))
        {
            throw new InvalidOperationException("DungeonStory local host endpoint is not available.");
        }

        string grammar = EquipmentChoiceGrammarCatalog.Require(candidateCount);
        StringBuilder body = new StringBuilder(prompt.Prompt.Length + grammar.Length + 256);
        body.Append("{\"prompt\":");
        AppendJsonString(body, prompt.Prompt);
        body.Append(",\"grammar\":");
        AppendJsonString(body, grammar);
        body.Append(",\"temperature\":0,\"n_predict\":32,\"cache_prompt\":true,\"stream\":false}");

        UnityWebRequest request = new UnityWebRequest(
            baseEndpoint + "/completion",
            "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString())),
            downloadHandler = new DownloadHandlerBuffer()
        };
        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        string token = sessionTokenProvider?.Invoke() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
        }
        return request;
    }

    public bool TryExtractChoice(
        string responseJson,
        int candidateCount,
        out int selectedIndex,
        out string error)
    {
        selectedIndex = -1;
        error = string.Empty;
        LlamaCppCompletionResponse response;
        try
        {
            response = JsonUtility.FromJson<LlamaCppCompletionResponse>(responseJson ?? string.Empty);
        }
        catch (Exception exception)
        {
            error = "EquipmentChoice.TransportEnvelopeInvalid: " + exception.Message;
            return false;
        }

        if (response == null)
        {
            error = response?.error?.message ?? "EquipmentChoice.TransportEnvelopeInvalid";
            return false;
        }
        if (!EquipmentChoiceResultParser.TryParse(
                response.content,
                candidateCount,
                out selectedIndex,
                out error))
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                error = response.error?.message ?? "EquipmentChoice.SchemaInvalid";
            }
            return false;
        }
        return true;
    }

    private static void AppendJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value ?? string.Empty)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append('"');
    }

    [Serializable]
    private sealed class LlamaCppChatResponse
    {
        public LlamaCppChatChoice[] choices;
        public LlamaCppError error;
    }

    [Serializable]
    private sealed class LlamaCppChatChoice
    {
        public LlamaCppChatMessage message;
    }

    [Serializable]
    private sealed class LlamaCppChatMessage
    {
        public string content;
    }

    [Serializable]
    private sealed class LlamaCppCompletionResponse
    {
        public string content;
        public LlamaCppError error;
    }

    [Serializable]
    private sealed class LlamaCppError
    {
        public string message;
    }
}
