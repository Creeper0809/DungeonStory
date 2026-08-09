#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Stopwatch = System.Diagnostics.Stopwatch;

public static class V24NarrativeLiveModelProbe
{
    private const string Endpoint = "http://127.0.0.1:11434/api/chat";
    private const string Model = "llama3.1:latest";
    private const string ReportPath = "Artifacts/QA/v24-narrative-live-probe.txt";
    private static bool running;

    [MenuItem("DungeonStory/Debug/V24/Run Live Model Smoke Probe")]
    public static void RunSmoke() => Run(1);

    [MenuItem("DungeonStory/Debug/V24/Run Live Model Acceptance Probe (20/Profile)")]
    public static void RunAcceptance() => Run(20);

    private static async void Run(int samplesPerProfile)
    {
        if (running)
        {
            Debug.LogWarning("V24 live narrative probe is already running.");
            return;
        }

        running = true;
        try
        {
            LiveProbeReport report = await ExecuteAsync(samplesPerProfile);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Artifacts/QA");
            File.WriteAllText(ReportPath, report.Format(), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            if (report.IsPassing)
            {
                Debug.Log($"V24 live narrative probe passed ({report.Accepted}/{report.Total}, report={ReportPath}).");
            }
            else
            {
                Debug.LogError($"V24 live narrative probe failed ({report.Accepted}/{report.Total}, fallback={report.Fallbacks}, parse={report.ParseFailures}, report={ReportPath}).");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("V24 live narrative probe crashed: " + exception);
        }
        finally
        {
            running = false;
        }
    }

    private static async Task<LiveProbeReport> ExecuteAsync(int samplesPerProfile)
    {
        LiveProbeReport report = new LiveProbeReport(samplesPerProfile);
        NarrativeTextQualityGate qualityGate = new NarrativeTextQualityGate();
        OllamaStructuredChatBackend backend = new OllamaStructuredChatBackend();
        using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        foreach (LlmStaticSchemaDefinition schema in LlmStaticSchemaCatalog.All)
        {
            LocalLlmRequestProfile profile = RequireProfile(schema.ProfileId);
            ProfileProbeResult profileResult = report.AddProfile(schema);
            for (int sample = 0; sample < samplesPerProfile; sample++)
            {
                NarrativeRequestContext context = NarrativeCultureStyleCatalog.Create(
                    schema.ProfileId,
                    CultureForSample(sample),
                    requireCharacterFact: schema.PersistentNarrative,
                    requireMotif: schema.PersistentNarrative);
                context.AddFact(
                    $"fact:probe:{sample:D2}",
                    "Expressed history: an aging frontier artisan who inherited a mentor's scarred tool",
                    100);
                context.AddFact(
                    $"fact:probe:{sample:D2}:origin",
                    "Background: displaced survivor from a border war",
                    90);
                context.AddFact(
                    $"fact:probe:{sample:D2}:career",
                    "Career: master smith nearing retirement",
                    80);
                context.AddFact(
                    $"fact:probe:{sample:D2}:ambition",
                    "Ambition: entrust a worthy heir with the workshop lineage",
                    70);
                string prompt = context.AppendToPrompt(BuildPrompt(schema.ProfileId, sample));
                LiveCallResult call = await CallAsync(client, backend, profile, schema, prompt);
                profileResult.RecordLatency(call.FirstTokenMilliseconds);
                if (!call.TransportSucceeded)
                {
                    profileResult.Fallbacks++;
                    profileResult.TransportFailures++;
                    profileResult.RecordError("transport: " + call.Error);
                    continue;
                }

                NarrativeQualityResult quality = qualityGate.Evaluate(profile, prompt, call.Content);
                if (quality.Verdict == NarrativeQualityVerdict.HardReject && schema.PersistentNarrative)
                {
                    profileResult.Retries++;
                    string correction = prompt
                        + "\nCorrection: the prior response was rejected: "
                        + quality.Error
                        + ". Return a corrected object using only listed Fxx and Mxx references.";
                    call = await CallAsync(client, backend, profile, schema, correction);
                    profileResult.RecordLatency(call.FirstTokenMilliseconds);
                    quality = call.TransportSucceeded
                        ? qualityGate.Evaluate(profile, prompt, call.Content)
                        : new NarrativeQualityResult(
                            NarrativeQualityVerdict.HardReject,
                            call.Error,
                            Array.Empty<string>(),
                            Array.Empty<string>());
                }

                if (!call.TransportSucceeded)
                {
                    profileResult.TransportFailures++;
                    profileResult.Fallbacks++;
                    profileResult.RecordError("correction transport: " + call.Error);
                }
                else if (quality.Verdict == NarrativeQualityVerdict.HardReject)
                {
                    profileResult.ParseFailures += LooksLikeJsonObject(call.Content) ? 0 : 1;
                    profileResult.Fallbacks++;
                    profileResult.RecordError(
                        "hard reject: " + quality.Error + " output=" + Compact(call.Content));
                }
                else
                {
                    profileResult.Accepted++;
                    if (quality.Verdict == NarrativeQualityVerdict.StrongPass)
                    {
                        profileResult.StrongPasses++;
                    }
                    else
                    {
                        profileResult.SoftPasses++;
                    }
                }
            }
        }

        return report;
    }

    private static async Task<LiveCallResult> CallAsync(
        HttpClient client,
        OllamaStructuredChatBackend backend,
        LocalLlmRequestProfile profile,
        LlmStaticSchemaDefinition schema,
        string prompt)
    {
        byte[] requestBytes;
        using (UnityWebRequest request = backend.BuildRequest(
            Endpoint,
            Model,
            profile,
            schema,
            prompt))
        {
            string nonStreaming = Encoding.UTF8.GetString(request.uploadHandler.data);
            requestBytes = Encoding.UTF8.GetBytes(nonStreaming.Replace(
                "\"stream\":false",
                "\"stream\":true"));
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        httpRequest.Content = new ByteArrayContent(requestBytes);
        httpRequest.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        try
        {
            using HttpResponseMessage response = await client.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                string raw = await response.Content.ReadAsStringAsync();
                return LiveCallResult.Failed($"HTTP {(int)response.StatusCode}: {raw}");
            }

            StringBuilder content = new StringBuilder(512);
            double firstToken = -1d;
            using Stream stream = await response.Content.ReadAsStreamAsync();
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                OllamaStreamChunk chunk = JsonUtility.FromJson<OllamaStreamChunk>(line);
                if (!string.IsNullOrEmpty(chunk?.message?.content))
                {
                    if (firstToken < 0d)
                    {
                        firstToken = stopwatch.Elapsed.TotalMilliseconds;
                    }
                    content.Append(chunk.message.content);
                }
            }
            stopwatch.Stop();

            if (content.Length == 0)
            {
                return LiveCallResult.Failed("Ollama stream contained no content.");
            }
            if (firstToken < 0d)
            {
                firstToken = stopwatch.Elapsed.TotalMilliseconds;
            }
            return LiveCallResult.Succeeded(content.ToString(), firstToken);
        }
        catch (Exception exception)
        {
            return LiveCallResult.Failed(exception.Message);
        }
    }

    private static LocalLlmRequestProfile RequireProfile(string profileId)
    {
        LocalLlmRequestProfile[] profiles =
        {
            LocalLlmRequestProfiles.CharacterSkill,
            LocalLlmRequestProfiles.Persona,
            LocalLlmRequestProfiles.MacroGoal,
            LocalLlmRequestProfiles.MoodImpulse,
            LocalLlmRequestProfiles.FacilityEvolution,
            LocalLlmRequestProfiles.EvolutionHistory,
            LocalLlmRequestProfiles.SocialRumor,
            LocalLlmRequestProfiles.CharacterRecord,
            LocalLlmRequestProfiles.BubbleLine
        };
        return profiles.First(value => string.Equals(value.Id, profileId, StringComparison.Ordinal));
    }

    private static string BuildPrompt(string profileId, int sample)
    {
        return "Return one concise schema-valid JSON object for profile " + profileId
            + ". Write player-facing narrative text in Korean fantasy or wuxia style. "
            + "Do not use markdown or invent people, events, relationships, or hidden facts. "
            + "Use conservative enum values and use F01 and M01 when reference arrays are present. "
            + "For request-bound identifiers use requestKey=req, targetPersistentId=target, nodeId=node, "
            + "parentNodeId=parent, effectId=effect, effectBudget=1 and evidenceIds=[evidence]. "
            + "For FacilityEvolution use proposalIds=[proposal-a], one matching reasons entry, "
            + "empty rejectedHints, empty mutationTagSuggestions, and a short rejectedHintText. "
            + "For skill candidates use index=0, trigger=OnTurnStart, target=Self, ultimateDomain=None, "
            + "cooldownTurns=0, combinationId=probe and an empty modules array. Sample=" + sample + ".";
    }

    private static string CultureForSample(int sample)
    {
        string[] cultures =
        {
            "orc", "vampire", "demon", "slime", "kobold",
            "myconid", "harpy", "beastkin", "golem", "adventurer"
        };
        return cultures[Math.Abs(sample) % cultures.Length];
    }

    private static bool LooksLikeJsonObject(string value)
    {
        string trimmed = value?.Trim();
        return !string.IsNullOrEmpty(trimmed)
            && trimmed[0] == '{'
            && trimmed[trimmed.Length - 1] == '}';
    }

    private static string Compact(string value)
    {
        string compact = (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
        return compact.Length <= 320 ? compact : compact.Substring(0, 320);
    }

    [Serializable]
    private sealed class OllamaStreamChunk
    {
        public OllamaStreamMessage message;
    }

    [Serializable]
    private sealed class OllamaStreamMessage
    {
        public string content;
    }

    private readonly struct LiveCallResult
    {
        private LiveCallResult(bool transportSucceeded, string content, string error, double firstTokenMilliseconds)
        {
            TransportSucceeded = transportSucceeded;
            Content = content ?? string.Empty;
            Error = error ?? string.Empty;
            FirstTokenMilliseconds = firstTokenMilliseconds;
        }

        public bool TransportSucceeded { get; }
        public string Content { get; }
        public string Error { get; }
        public double FirstTokenMilliseconds { get; }

        public static LiveCallResult Succeeded(string content, double firstTokenMilliseconds) =>
            new LiveCallResult(true, content, string.Empty, firstTokenMilliseconds);

        public static LiveCallResult Failed(string error) =>
            new LiveCallResult(false, string.Empty, error, -1d);
    }

    private sealed class LiveProbeReport
    {
        private readonly List<ProfileProbeResult> profiles = new List<ProfileProbeResult>();

        public LiveProbeReport(int samplesPerProfile) => SamplesPerProfile = samplesPerProfile;

        public int SamplesPerProfile { get; }
        public int Total => profiles.Sum(value => value.Total);
        public int Accepted => profiles.Sum(value => value.Accepted);
        public int Fallbacks => profiles.Sum(value => value.Fallbacks);
        public int ParseFailures => profiles.Sum(value => value.ParseFailures);
        public bool IsPassing => Total > 0
            && ParseFailures == 0
            && Accepted / (double)Total >= 0.98d
            && Fallbacks / (double)Total <= 0.02d;

        public ProfileProbeResult AddProfile(LlmStaticSchemaDefinition schema)
        {
            ProfileProbeResult value = new ProfileProbeResult(schema, SamplesPerProfile);
            profiles.Add(value);
            return value;
        }

        public string Format()
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("V24 Narrative Live Model Probe");
            builder.AppendLine($"endpoint={Endpoint}");
            builder.AppendLine($"model={Model}");
            builder.AppendLine($"samplesPerProfile={SamplesPerProfile}");
            builder.AppendLine($"accepted={Accepted}/{Total}");
            builder.AppendLine($"fallbacks={Fallbacks}");
            builder.AppendLine($"parseFailures={ParseFailures}");
            builder.AppendLine($"pass={IsPassing}");
            builder.AppendLine();
            foreach (ProfileProbeResult profile in profiles)
            {
                builder.AppendLine(profile.Format());
            }
            return builder.ToString();
        }
    }

    private sealed class ProfileProbeResult
    {
        private readonly List<double> firstTokenMilliseconds = new List<double>();
        private readonly List<string> errors = new List<string>();

        public ProfileProbeResult(LlmStaticSchemaDefinition schema, int total)
        {
            Schema = schema;
            Total = total;
        }

        public LlmStaticSchemaDefinition Schema { get; }
        public int Total { get; }
        public int Accepted { get; set; }
        public int StrongPasses { get; set; }
        public int SoftPasses { get; set; }
        public int Fallbacks { get; set; }
        public int ParseFailures { get; set; }
        public int TransportFailures { get; set; }
        public int Retries { get; set; }

        public void RecordLatency(double value)
        {
            if (value >= 0d)
            {
                firstTokenMilliseconds.Add(value);
            }
        }

        public void RecordError(string value)
        {
            if (errors.Count < 3 && !string.IsNullOrWhiteSpace(value))
            {
                errors.Add(value);
            }
        }

        public string Format()
        {
            firstTokenMilliseconds.Sort();
            string summary = $"{Schema.ProfileId}: schema={Schema.Version}:{Schema.Hash}, accepted={Accepted}/{Total}, "
                + $"strong={StrongPasses}, soft={SoftPasses}, retries={Retries}, fallback={Fallbacks}, "
                + $"transport={TransportFailures}, parse={ParseFailures}, "
                + $"ttftMedianMs={Percentile(0.50d):0.0}, ttftP95Ms={Percentile(0.95d):0.0}";
            return errors.Count == 0
                ? summary
                : summary + Environment.NewLine + "  errors=" + string.Join(" | ", errors);
        }

        private double Percentile(double percentile)
        {
            if (firstTokenMilliseconds.Count == 0)
            {
                return -1d;
            }
            int index = Mathf.Clamp(
                Mathf.CeilToInt((float)(firstTokenMilliseconds.Count * percentile)) - 1,
                0,
                firstTokenMilliseconds.Count - 1);
            return firstTokenMilliseconds[index];
        }
    }
}
#endif
