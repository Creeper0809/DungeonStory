#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Captures the exact production structured-chat body for a module-selection
/// request. Exporters must use this adapter instead of rebuilding system/user
/// messages or the response schema in Python.
/// </summary>
internal static class GameplayModuleSelectionLiveWireCapture
{
    internal const int ProtocolVersion = 3;

    internal static JObject Capture(
        string profileId,
        string logicalRequestId,
        string transportAttemptId,
        string model,
        string prompt,
        IReadOnlyList<JObject> authorityRequests)
    {
        LocalLlmRequestProfile profile = RequireProfile(profileId);
        LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(profile.Id);
        string logical = RequireText(logicalRequestId, nameof(logicalRequestId));
        string attempt = RequireText(transportAttemptId, nameof(transportAttemptId));
        string requestPrompt = RequirePrompt(prompt, nameof(prompt));
        JObject[] requests = (authorityRequests
                ?? throw new ArgumentNullException(nameof(authorityRequests)))
            .Select((value, index) => RequireAuthorityRequest(value, index))
            .ToArray();
        bool batch = string.Equals(
            profile.Id,
            LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
            StringComparison.Ordinal);
        if (requests.Length < 1 || requests.Length > 3 || !batch && requests.Length != 1)
        {
            throw new InvalidOperationException(
                "The live request candidate count does not match its production profile.");
        }
        if (batch && requests.Any(value =>
                ((JArray)value["evidenceFactIds"]).Count > 2))
        {
            throw new InvalidOperationException(
                "A live character-skill request may expose at most two evidence facts.");
        }

        byte[] bodyBytes = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
            model ?? string.Empty,
            profile,
            schema,
            requestPrompt);
        string bodyJson = new UTF8Encoding(false, true).GetString(bodyBytes);
        JObject body;
        try
        {
            body = JObject.Parse(bodyJson);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The production structured-chat serializer emitted invalid JSON.",
                exception);
        }

        JObject responseSchema = body["response_format"]?["json_schema"]?["schema"]
            as JObject ?? throw new InvalidOperationException(
                "The production request body lost its closed response schema.");
        int maximumPositive = requests.Max(value => (int)value["maximumPositiveModules"]);
        int maximumDrawback = requests.Max(value => (int)value["maximumDrawbackModules"]);
        int maximumEvidence = requests.Max(value =>
            ((JArray)value["evidenceFactIds"]).Count);
        int nameMaximum = batch ? 14 : 32;
        int flavorMaximum = batch ? 48 : 180;

        return new JObject
        {
            ["profileId"] = profile.Id,
            ["mode"] = batch ? "batch" : "single",
            ["logicalRequestId"] = logical,
            ["transportAttemptId"] = attempt,
            ["candidateCount"] = requests.Length,
            ["selectionIds"] = new JArray(requests.Select(value =>
                (string)value["selectionId"])),
            ["prompt"] = requestPrompt,
            ["promptSha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(
                Encoding.UTF8.GetBytes(requestPrompt)),
            ["wireBodyJson"] = bodyJson,
            ["wireBodySha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bodyBytes),
            ["schemaVersion"] = schema.Version,
            ["schemaSha256"] = GameplayModuleSelectionCanonicalJson.Hash(
                responseSchema),
            ["constraints"] = new JObject
            {
                ["minimumPositiveModules"] = 1,
                ["maximumPositiveModules"] = maximumPositive,
                ["maximumDrawbackModules"] = maximumDrawback,
                ["minimumEvidenceFacts"] = 1,
                ["maximumEvidenceFacts"] = maximumEvidence,
                ["nameMaxUtf16"] = nameMaximum,
                ["flavorMaxUtf16"] = flavorMaximum
            }
        };
    }

    private static JObject RequireAuthorityRequest(JObject source, int index)
    {
        JObject value = source == null
            ? throw new ArgumentException(
                "An authority request cannot be null.", nameof(source))
            : (JObject)source.DeepClone();
        string[] required =
        {
            "selectionId", "targetDisplayName", "moduleOffers",
            "maximumPositiveModules", "maximumDrawbackModules",
            "evidenceFactIds", "publicFacts", "style"
        };
        foreach (string property in required)
        {
            if (value[property] == null)
            {
                throw new InvalidOperationException(
                    $"Authority request {index} is missing '{property}'.");
            }
        }
        RequireText((string)value["selectionId"], "selectionId");
        int maximumPositive = value["maximumPositiveModules"].Value<int>();
        int maximumDrawback = value["maximumDrawbackModules"].Value<int>();
        if (maximumPositive < 1 || maximumPositive > 3
            || maximumDrawback < 0 || maximumDrawback > 3
            || value["evidenceFactIds"] is not JArray evidence
            || evidence.Count == 0)
        {
            throw new InvalidOperationException(
                $"Authority request {index} has invalid live cardinality.");
        }
        return value;
    }

    private static LocalLlmRequestProfile RequireProfile(string profileId)
    {
        string value = RequireText(profileId, nameof(profileId));
        if (string.Equals(value,
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                StringComparison.Ordinal))
            return LocalLlmRequestProfiles.CharacterSkillModuleSelection;
        if (string.Equals(value,
                LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id,
                StringComparison.Ordinal))
            return LocalLlmRequestProfiles.AcquiredTraitModuleSelection;
        if (string.Equals(value,
                LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                StringComparison.Ordinal))
            return LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection;
        if (string.Equals(value,
                LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id,
                StringComparison.Ordinal))
            return LocalLlmRequestProfiles.FacilityEvolutionModuleSelection;
        throw new InvalidOperationException(
            "Unsupported gameplay module-selection profile: " + value);
    }

    private static string RequireText(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException(
                "A canonical non-empty value is required.", name);
        return canonical;
    }

    private static string RequirePrompt(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "A non-empty prompt is required.",
                name);
        return value;
    }
}
#endif
