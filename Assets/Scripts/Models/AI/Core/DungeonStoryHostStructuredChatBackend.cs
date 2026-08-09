using System;
using System.Globalization;
using System.IO;
using System.Text;
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
        "markdown, internal ids other than supplied Fxx/Mxx references, or extra keys.";

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

        string modelPrompt = NarrativeRequestContext.ToModelPrompt(prompt);
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
                   prefixBytes.Length + schema.Utf8Bytes.Length + suffixBytes.Length))
        {
            stream.Write(prefixBytes, 0, prefixBytes.Length);
            stream.Write(schema.Utf8Bytes, 0, schema.Utf8Bytes.Length);
            stream.Write(suffixBytes, 0, suffixBytes.Length);
            payload = stream.ToArray();
        }

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
        body.Append(",\"temperature\":0,\"n_predict\":4,\"cache_prompt\":true,\"stream\":false}");

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
            error = "DungeonStory choice response parse failed: " + exception.Message;
            return false;
        }

        if (response == null
            || !EquipmentChoiceResultParser.TryParse(
                response.content,
                candidateCount,
                out selectedIndex))
        {
            error = response?.error?.message ?? "Bundled llama.cpp host returned an invalid choice.";
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
