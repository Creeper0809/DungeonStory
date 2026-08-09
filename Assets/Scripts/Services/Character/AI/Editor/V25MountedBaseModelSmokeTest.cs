#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class V25MountedBaseModelSmokeTest
{
    [MenuItem("DungeonStory/Debug/V25/Run Mounted Base Model Smoke")]
    public static void Run()
    {
        int exitCode = 0;
        DungeonStoryLlmHostProcess host = null;
        int processCountBefore = Process.GetProcessesByName("DungeonStoryLlmHost").Length;
        Stopwatch elapsed = Stopwatch.StartNew();
        string generated = string.Empty;
        try
        {
            Require(DungeonStoryLlmHostProcess.TryStart(
                    Application.streamingAssetsPath,
                    out host,
                    out string error),
                error);
            Require(host != null && host.IsRunning, "Mounted base model host is not running.");
            Require(string.Equals(host.BackendKind, "LlamaCppServer", StringComparison.Ordinal),
                "Mounted host kind mismatch.");
            Require(string.Equals(host.TrainingState, "base-untrained", StringComparison.Ordinal),
                "Mounted model was not explicitly marked as the untrained base model.");
            Require(!host.ReleaseCertified,
                "Untrained base model must not be marked release-certified.");

            generated = GenerateStructuredRecord(host);
            RecordOutput record = JsonUtility.FromJson<RecordOutput>(generated);
            Require(record != null && !string.IsNullOrWhiteSpace(record.line),
                "Mounted model returned no structured record line.");
            Require(record.usedMotifIds != null && record.usedMotifIds.Length > 0,
                "Mounted model omitted its required motif reference.");
            Require(record.usedCharacterFactIds != null && record.usedCharacterFactIds.Length > 0,
                "Mounted model omitted its required character fact reference.");
        }
        catch (Exception exception)
        {
            exitCode = 1;
            Debug.LogError("V25 mounted base model smoke FAIL: " + exception);
        }
        finally
        {
            host?.Dispose();
        }

        Stopwatch shutdown = Stopwatch.StartNew();
        while (shutdown.Elapsed < TimeSpan.FromSeconds(5)
               && Process.GetProcessesByName("DungeonStoryLlmHost").Length > processCountBefore)
        {
            Thread.Sleep(50);
        }
        int processCountAfter = Process.GetProcessesByName("DungeonStoryLlmHost").Length;
        if (processCountAfter != processCountBefore)
        {
            exitCode = 1;
            Debug.LogError("V25 mounted base model smoke left a host process behind.");
        }

        WriteReport(exitCode == 0, elapsed.Elapsed, generated, processCountBefore, processCountAfter);
        if (exitCode == 0)
        {
            Debug.Log("V25 mounted base model smoke PASS (CPU structured generation + clean shutdown).");
        }
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
        }
    }

    private static string GenerateStructuredRecord(DungeonStoryLlmHostProcess host)
    {
        const string schema =
            "{\"type\":\"object\",\"additionalProperties\":false," +
            "\"required\":[\"line\",\"usedMotifIds\",\"usedCharacterFactIds\"]," +
            "\"properties\":{" +
            "\"line\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":60}," +
            "\"usedMotifIds\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":3," +
            "\"items\":{\"type\":\"string\",\"pattern\":\"^M[0-9]{2}$\",\"maxLength\":3}}," +
            "\"usedCharacterFactIds\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":4," +
            "\"items\":{\"type\":\"string\",\"pattern\":\"^F[0-9]{2}$\",\"maxLength\":3}}}}";
        string payload =
            "{\"model\":\"DungeonStory-Qwen3-1.7B-Q4_K_M\",\"stream\":false," +
            "\"cache_prompt\":true,\"messages\":[" +
            "{\"role\":\"system\",\"content\":\"Return exactly one compact JSON object matching the supplied schema. Write player-facing prose in Korean. No markdown.\"}," +
            "{\"role\":\"user\",\"content\":\"/no_think\\nF01 = 출신: 전쟁 난민\\nM01 = 흉터\\n오래된 대장장이의 한 줄 기록을 쓰고 F01과 M01을 사용하라.\"}]," +
            "\"chat_template_kwargs\":{\"enable_thinking\":false}," +
            "\"response_format\":{\"type\":\"json_schema\",\"json_schema\":{" +
            "\"name\":\"DungeonStory_CharacterRecord\",\"strict\":true,\"schema\":" + schema + "}}," +
            "\"temperature\":0.4,\"max_tokens\":128}";

        using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", host.SessionToken);
        using StringContent body = new StringContent(payload, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = client.PostAsync(
                host.Endpoint.TrimEnd('/') + "/v1/chat/completions",
                body)
            .GetAwaiter().GetResult();
        string responseJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        Require(response.IsSuccessStatusCode,
            "Mounted host HTTP failure: " + (int)response.StatusCode + " " + responseJson);
        ChatResponse chat = JsonUtility.FromJson<ChatResponse>(responseJson);
        Require(chat?.choices != null && chat.choices.Length > 0
                && !string.IsNullOrWhiteSpace(chat.choices[0]?.message?.content),
            "Mounted host response envelope was invalid.");
        return chat.choices[0].message.content;
    }

    private static void WriteReport(
        bool passed,
        TimeSpan elapsed,
        string generated,
        int processCountBefore,
        int processCountAfter)
    {
        string directory = Path.Combine(Directory.GetCurrentDirectory(), "Artifacts", "Validation");
        Directory.CreateDirectory(directory);
        SmokeReport report = new SmokeReport
        {
            passed = passed,
            cpuOnly = true,
            modelVersion = "Qwen3-1.7B-base-Q4_K_M@ggml-org/b10331",
            elapsedMilliseconds = (long)elapsed.TotalMilliseconds,
            generatedContent = generated ?? string.Empty,
            hostProcessesBefore = processCountBefore,
            hostProcessesAfter = processCountAfter
        };
        File.WriteAllText(
            Path.Combine(directory, "V25BaseModelRuntimeSmoke.json"),
            JsonUtility.ToJson(report, true) + Environment.NewLine,
            Encoding.UTF8);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    [Serializable]
    private sealed class ChatResponse { public ChatChoice[] choices; }
    [Serializable]
    private sealed class ChatChoice { public ChatMessage message; }
    [Serializable]
    private sealed class ChatMessage { public string content; }
    [Serializable]
    private sealed class RecordOutput
    {
        public string line;
        public string[] usedMotifIds;
        public string[] usedCharacterFactIds;
    }
    [Serializable]
    private sealed class SmokeReport
    {
        public bool passed;
        public bool cpuOnly;
        public string modelVersion;
        public long elapsedMilliseconds;
        public string generatedContent;
        public int hostProcessesBefore;
        public int hostProcessesAfter;
    }
}
#endif
