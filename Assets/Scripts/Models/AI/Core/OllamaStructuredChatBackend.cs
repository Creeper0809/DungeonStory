using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public interface ILocalLlmStructuredBackend
{
    string ResolveEndpoint(string configuredEndpoint);

    UnityWebRequest BuildRequest(
        string configuredEndpoint,
        string model,
        LocalLlmRequestProfile profile,
        LlmStaticSchemaDefinition schema,
        string prompt);

    bool TryExtractContent(
        string responseJson,
        out string content,
        out string error);
}

#if UNITY_EDITOR
public sealed class OllamaStructuredChatBackend : ILocalLlmStructuredBackend
{
    private const string SystemInstruction =
        "Return one compact JSON object matching the supplied schema. " +
        "Write player-facing prose in Korean. Never add markdown or unrequested keys.";

    public string ResolveEndpoint(string configuredEndpoint)
    {
        string value = (configuredEndpoint ?? string.Empty).Trim().TrimEnd('/');
        if (value.EndsWith("/v1/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(0, value.Length - "/v1/chat/completions".Length);
        }
        else if (value.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return string.IsNullOrWhiteSpace(value) ? string.Empty : value + "/api/chat";
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
            throw new InvalidOperationException("Ollama structured endpoint is not configured.");
        }

        string modelPrompt = NarrativeRequestContext.ToModelPrompt(prompt);
        StringBuilder prefix = new StringBuilder((modelPrompt?.Length ?? 0) + 512);
        prefix.Append("{\"model\":");
        AppendJsonString(prefix, model ?? string.Empty);
        prefix.Append(",\"stream\":false,\"messages\":[{\"role\":\"system\",\"content\":");
        AppendJsonString(prefix, SystemInstruction);
        prefix.Append("},{\"role\":\"user\",\"content\":");
        AppendJsonString(prefix, modelPrompt ?? string.Empty);
        prefix.Append("}],\"format\":");

        StringBuilder suffix = new StringBuilder(128);
        suffix.Append(",\"options\":{\"temperature\":");
        suffix.Append(profile.Temperature.ToString(
            "0.###",
            System.Globalization.CultureInfo.InvariantCulture));
        suffix.Append(",\"num_predict\":");
        suffix.Append(profile.MaxOutputTokens);
        suffix.Append("}}");

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

        UnityWebRequest request = new UnityWebRequest(endpoint, "POST");
        request.uploadHandler = new UploadHandlerRaw(payload);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
        return request;
    }

    public bool TryExtractContent(
        string responseJson,
        out string content,
        out string error)
    {
        content = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            error = "Ollama HTTP response is empty.";
            return false;
        }

        OllamaChatResponse response;
        try
        {
            response = JsonUtility.FromJson<OllamaChatResponse>(responseJson);
        }
        catch (Exception exception)
        {
            error = $"Ollama HTTP response parse failed: {exception.Message}";
            return false;
        }

        content = response?.message?.content;
        if (string.IsNullOrWhiteSpace(content))
        {
            error = "Ollama HTTP response has no message content.";
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
    private sealed class OllamaChatResponse
    {
        public OllamaChatMessage message;
    }

    [Serializable]
    private sealed class OllamaChatMessage
    {
        public string role;
        public string content;
    }
}
#endif
