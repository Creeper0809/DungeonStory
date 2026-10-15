#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static GameplayModuleSelectionSourceV9ReplayInput;

/// <summary>
/// Observes one bundled-model response to an immutable public source-v9 wire.
/// No world is loaded, no gameplay effect is applied, and no response is repaired.
/// </summary>
public static class GameplayModuleSelectionActualModelAuthoringBatchRunner
{
    private const string OutputEnv = "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT";
    private const string SourceEnv = "DUNGEONSTORY_SOURCE_V9_AUTHORING_SOURCE";
    private const string SourceShaEnv = "DUNGEONSTORY_SOURCE_V9_AUTHORING_MANIFEST_SHA256";
    private const string ProfileEnv = "DUNGEONSTORY_SOURCE_V9_AUTHORING_PROFILE";

    public static void RunOrThrow()
    {
        string output = RequireOutput("DUNGEONSTORY_SOURCE_V9_ACTUAL_AUTHORING");
        JObject report = Gates("dungeonstory-source-v9-model-authoring-report@1");
        report["passed"] = false;
        report["actualModelResponsesObserved"] = 0;
        report["gameplayApplied"] = false;
        DungeonStoryLlmHostProcess host = null;
        try
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string source = RequireQaDirectory(project,
                Environment.GetEnvironmentVariable(SourceEnv), "authoring source");
            byte[] manifestBytes = File.ReadAllBytes(Path.Combine(source, "manifest.json"));
            Require(GameplayModuleSelectionCanonicalJson.Sha256Prefixed(manifestBytes)
                == Environment.GetEnvironmentVariable(SourceShaEnv), "Source manifest pin differs.");
            JObject manifest = ParseObject(manifestBytes, "source manifest");
            Require((string)manifest["schema"] == SourceSchema
                && (bool?)manifest["trainingEligible"] == false
                && (bool?)manifest["humanApprovalClaimed"] == false
                && (bool?)manifest["dpoPerformed"] == false
                && (bool?)manifest["releaseReady"] == false, "Source schema/gates differ.");
            string profile = Environment.GetEnvironmentVariable(ProfileEnv);
            Require(IsProfile(profile), "Unsupported authoring profile.");
            JObject selections = ParseObject(ReadDescriptor(source,
                RequireObject(manifest, "selectionSource"), "selection source"), "selection source");
            JObject[] rows = (selections["selections"] as JArray)?.OfType<JObject>()
                .Where(row => (string)row["profileId"] == profile).ToArray();
            Require(rows?.Length == 1 && (bool?)selections["responseDataIncluded"] == false
                && (bool?)selections["responseApplied"] == false, "Source selection is not unique/response-free.");
            JObject selection = rows[0];
            byte[] wire = ReadDescriptor(source,
                RequireObject(RequireObject(selection, "wire"), "original"), "public source wire");
            byte[] restoredWire = ReadDescriptor(source,
                RequireObject(RequireObject(selection, "wire"), "restoredReplay"), "restored public wire");
            Require(wire.SequenceEqual(restoredWire)
                && (bool?)selection["wire"]?["byteExactReplay"] == true, "Source wire restore differs.");
            JObject mechanics = ParseObject(ReadDescriptor(source,
                RequireObject(manifest, "mechanics"), "mechanics"), "mechanics");
            Require(mechanics["codeIdentity"] is JArray, "Source code identity is missing.");
            report["codeIdentity"] = GameplayModuleSelectionReplayProofV2Contract.CaptureCurrentCodeIdentity(
                project, (JArray)mechanics["codeIdentity"],
                RequireText(RequireObject(mechanics, "b1EvidenceManifest"), "sha256"),
                "sha256:" + RequireText(selection, "schemaHash"),
                new JObject { ["mode"] = "source-v9-local-model-authoring", ["gameplayApplied"] = false });
            byte[] hostManifestBytes = File.ReadAllBytes(Path.Combine(
                Application.streamingAssetsPath, "DungeonStoryLlm", "manifest.json"));
            JObject hostManifest = ParseObject(hostManifestBytes, "installed host manifest");
            string alias = Path.GetFileNameWithoutExtension(RequireText(hostManifest, "modelFile"));
            Require((string)ParseObject(wire, "wire")["model"] == alias,
                "Exact source wire model differs from the installed host alias. Recapture; do not rewrite wire.");
            Require(Process.GetProcessesByName("DungeonStoryLlmHost").Length == 0,
                "A local host already exists; it is not owned by this verification.");
            Require(DungeonStoryLlmHostProcess.TryStart(Application.streamingAssetsPath,
                out host, out string startupError), startupError);
            Require(host != null && host.IsRunning && host.BackendKind == "LlamaCppServer"
                && host.TrainingState == "base-untrained"
                && !host.ReleaseCertified, "Installed host identity/gates differ.");
            Require(Uri.TryCreate(host.Endpoint, UriKind.Absolute, out Uri endpoint)
                && endpoint.IsLoopback, "Only the owned loopback host is allowed.");

            JObject requestDescriptor = WriteBytes(output, "request-wire.json", wire);
            JObject hostDescriptor = WriteBytes(output, "host-manifest.json", hostManifestBytes);
            var stopwatch = Stopwatch.StartNew();
            using var handler = new HttpClientHandler { UseProxy = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.SessionToken);
            using var body = new ByteArrayContent(wire);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            using HttpResponseMessage http = client.PostAsync(
                host.Endpoint.TrimEnd('/') + "/v1/chat/completions", body).GetAwaiter().GetResult();
            byte[] envelopeBytes = http.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            stopwatch.Stop();
            JObject envelopeDescriptor = WriteBytes(output, "response-envelope.json", envelopeBytes);
            JObject author = new()
            {
                ["schema"] = "dungeonstory-local-model-authoring@1",
                ["author"] = "bundled-local-model", ["networkSent"] = true,
                ["model"] = alias, ["modelVersion"] = host.ModelVersion,
                ["trainingState"] = host.TrainingState, ["releaseCertified"] = host.ReleaseCertified,
                ["requestWire"] = requestDescriptor, ["responseEnvelope"] = envelopeDescriptor,
                ["hostManifest"] = hostDescriptor, ["adoptedAttempt"] = 0,
                ["attempts"] = new JArray(new JObject
                {
                    ["attempt"] = 1, ["httpStatus"] = (int)http.StatusCode, ["networkSent"] = true,
                    ["elapsedMilliseconds"] = stopwatch.ElapsedMilliseconds,
                    ["wireSha256"] = (string)requestDescriptor["sha256"],
                    ["responseEnvelopeSha256"] = (string)envelopeDescriptor["sha256"]
                })
            };
            report["authoring"] = author;
            Require(http.IsSuccessStatusCode, "Local model HTTP failure: " + (int)http.StatusCode);
            var backend = new DungeonStoryHostStructuredChatBackend();
            Require(backend.TryExtractContent(Encoding.UTF8.GetString(envelopeBytes),
                out string content, out string extractionError), extractionError);
            report["actualModelResponsesObserved"] = 1;
            JObject accepted = ParseObject(Encoding.UTF8.GetBytes(content), "model content");
            JObject resolved = profile == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
                ? ResolveObservedSkillContent(source, selection, wire, accepted)
                : profile == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id
                    ? ResolveObservedTraitContent(source, selection, wire, accepted) : accepted;
            Require(NarrativeExactKeyContract.TryValidateProfileResponse(profile, resolved.ToString(),
                out _, out string responseError), "Model response profile validation failed: " + responseError);
            JObject response = Gates(ResponseSchema);
            response["sourceManifestSha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(manifestBytes);
            response["selectionSourceId"] = RequireText(selection, "selectionSourceId");
            response["profileId"] = profile;
            response["requestId"] = RequireText(selection, "correlationId");
            response["wireSha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(wire);
            response["authoringOrigin"] = ActualModelOrigin;
            response["integrationFixture"] = false;
            author["adoptedAttempt"] = 1;
            response["authoring"] = author;
            response["acceptedResponseSha256"] = GameplayModuleSelectionCanonicalJson.Hash(accepted);
            ValidateResponseAuthoring(output, response, wire, accepted);
            response["acceptedResponse"] = WriteJson(output, "accepted-response.json", accepted);
            if (profile == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
                || profile == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id)
                response["modelReferenceResolution"] = new JObject
                {
                    ["schema"] = profile == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
                        ? "dungeonstory-skill-model-reference-resolution@1"
                        : "dungeonstory-trait-model-reference-resolution@1",
                    ["sourcePromptSha256"] = selection["capture"]["originalPrompt"]["sha256"].DeepClone(),
                    ["resolvedResponseSha256"] = GameplayModuleSelectionCanonicalJson.Hash(resolved),
                    ["resolvedResponse"] = WriteJson(output, "resolved-response.json", resolved),
                    ["choiceOrProseRepair"] = false
                };
            WriteJson(output, "manifest.json", response);
            report["passed"] = true;
            report["status"] = "OBSERVED_MODEL_RESPONSE_PACKAGED_NOT_GAMEPLAY_ACCEPTANCE";
            report["profileId"] = profile;
            report["sourceManifestSha256"] = response["sourceManifestSha256"];
            report["acceptedResponseSha256"] = response["acceptedResponseSha256"];
        }
        catch (Exception error)
        {
            if (report["authoring"] is JObject failedAuthor)
                failedAuthor["adoptedAttempt"] = 0;
            report["error"] = error.ToString();
            throw;
        }
        finally
        {
            try
            {
                if (host != null)
                    report["hostDiagnostics"] = new JObject
                    {
                        ["kind"] = "existing-mixed-stdout-stderr-bounded-tail",
                        ["capacityBytes"] = 1024 * 1024,
                        ["sessionTokenRedacted"] = true,
                        ["isAcceptedModelResponse"] = false,
                        ["file"] = WriteBytes(output, "host-diagnostic-tail.txt",
                            SnapshotHostDiagnosticTail(host))
                    };
            }
            catch (Exception diagnosticError)
            {
                report["passed"] = false;
                report["hostDiagnosticsError"] = diagnosticError.ToString();
            }
            finally
            {
                host?.Dispose();
                report["hostProcessesAfter"] = Process.GetProcessesByName("DungeonStoryLlmHost").Length;
                if ((int)report["hostProcessesAfter"] != 0)
                    report["passed"] = false;
                WriteJson(output, "report.json", report);
            }
        }
        Require((bool)report["passed"], "Model authoring or owned host diagnostics did not complete cleanly.");
    }

    // Editor observation only: read the existing bounded ring; never alter host
    // arguments, drain timing, parsing, generation, or gameplay response gates.
    private static byte[] SnapshotHostDiagnosticTail(DungeonStoryLlmHostProcess host)
    {
        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        Type type = typeof(DungeonStoryLlmHostProcess);
        object gate = type.GetField("logGate", flags)?.GetValue(host);
        var ring = type.GetField("logRing", flags)?.GetValue(host) as System.Collections.Generic.Queue<byte>;
        Require(gate != null && ring != null, "Existing host diagnostic ring is unavailable.");
        byte[] bytes;
        lock (gate) bytes = ring.ToArray();
        string tail = Encoding.UTF8.GetString(bytes);
        if (!string.IsNullOrEmpty(host.SessionToken)) tail = tail.Replace(host.SessionToken, "[SESSION_TOKEN_REDACTED]");
        return Encoding.UTF8.GetBytes(tail);
    }

    public static void RunContractChecksOrThrow()
    {
        string output = RequireOutput("DUNGEONSTORY_SOURCE_V9_AUTHORSHIP_CONTRACT");
        JObject report = Gates("dungeonstory-response-authorship-contract-check@1");
        report["actualModelResponsesObserved"] = 0;
        report["gameplayApplied"] = false;
        report["syntheticContractFixture"] = true;
        JArray checks = new();
        report["checks"] = checks;
        try
        {
            string fixture = Path.Combine(output, "provenance-contract-fixture");
            Directory.CreateDirectory(fixture);
            const string alias = "DungeonStory-Qwen3-1.7B-Q4_K_M";
            byte[] wire = Encoding.UTF8.GetBytes("{\"model\":\"" + alias + "\"}");
            JObject accepted = JObject.Parse("{\"contractFixtureOnly\":true}");
            var response = new JObject
            {
                ["authoringOrigin"] = ActualModelOrigin, ["integrationFixture"] = false,
                ["authoring"] = new JObject
                {
                    ["schema"] = "dungeonstory-local-model-authoring@1",
                    ["author"] = "bundled-local-model", ["networkSent"] = true, ["adoptedAttempt"] = 1,
                    ["model"] = alias, ["modelVersion"] = "contract-fixture-only",
                    ["trainingState"] = "base-untrained", ["releaseCertified"] = false,
                    ["requestWire"] = WriteBytes(fixture, "wire.json", wire),
                    ["responseEnvelope"] = WriteJson(fixture, "envelope.json", new JObject
                    {
                        ["syntheticContractFixture"] = true,
                        ["choices"] = new JArray(new JObject { ["message"] = new JObject
                        { ["content"] = accepted.ToString(Newtonsoft.Json.Formatting.None) } })
                    }),
                    ["hostManifest"] = WriteJson(fixture, "host.json", new JObject
                    {
                        ["modelFile"] = alias + ".gguf", ["modelVersion"] = "contract-fixture-only",
                        ["trainingState"] = "base-untrained", ["releaseCertified"] = false
                    })
                }
            };
            JObject author = (JObject)response["authoring"];
            author["attempts"] = new JArray(new JObject
            {
                ["attempt"] = 1, ["httpStatus"] = 200, ["networkSent"] = true,
                ["elapsedMilliseconds"] = 1,
                ["wireSha256"] = author["requestWire"]?["sha256"],
                ["responseEnvelopeSha256"] = author["responseEnvelope"]?["sha256"]
            });
            ValidateResponseAuthoring(fixture, response, wire, accepted);
            checks.Add("observed-origin-provenance-contract-only");
            var legacy = new JObject { ["authoringOrigin"] = "integration-fixture-explicit", ["integrationFixture"] = true };
            ValidateResponseAuthoring(fixture, legacy, wire, accepted);
            checks.Add("legacy-fixture-origin-preserved");
            Reject(checks, "unknown-origin", "origin", () =>
            { var m = (JObject)response.DeepClone(); m["authoringOrigin"] = "invented"; ValidateResponseAuthoring(fixture, m, wire, accepted); });
            Reject(checks, "fixture-cannot-claim-model", "origin", () =>
            { var m = (JObject)legacy.DeepClone(); m["integrationFixture"] = false; ValidateResponseAuthoring(fixture, m, wire, accepted); });
            Reject(checks, "model-cannot-claim-fixture", "origin", () =>
            { var m = (JObject)response.DeepClone(); m["integrationFixture"] = true; ValidateResponseAuthoring(fixture, m, wire, accepted); });
            Reject(checks, "origin-marker-type-tamper", "origin", () =>
            { var m = (JObject)response.DeepClone(); m["integrationFixture"] = "false"; ValidateResponseAuthoring(fixture, m, wire, accepted); });
            Reject(checks, "rewritten-wire", "source wire", () =>
                ValidateResponseAuthoring(fixture, response, Encoding.UTF8.GetBytes("{}"), accepted));
            Reject(checks, "replaced-accepted-content", "differs from observed", () =>
                ValidateResponseAuthoring(fixture, response, wire, JObject.Parse("{\"contractFixtureOnly\":false}")));
            Reject(checks, "envelope-hash-tamper", "bytes/hash", () =>
            { var m = (JObject)response.DeepClone(); m["authoring"]["responseEnvelope"]["sha256"] = "sha256:wrong"; ValidateResponseAuthoring(fixture, m, wire, accepted); });
            Reject(checks, "missing-attempt", "attempt metadata", () =>
            { var m = (JObject)response.DeepClone(); m["authoring"]["attempts"] = new JArray(); ValidateResponseAuthoring(fixture, m, wire, accepted); });
            Reject(checks, "unadopted-attempt", "attempt metadata", () =>
            { var m = (JObject)response.DeepClone(); m["authoring"]["adoptedAttempt"] = 0; ValidateResponseAuthoring(fixture, m, wire, accepted); });
            Reject(checks, "no-network-observation", "attempt metadata", () =>
            { var m = (JObject)response.DeepClone(); m["authoring"]["networkSent"] = false; ValidateResponseAuthoring(fixture, m, wire, accepted); });
            Reject(checks, "model-identity-tamper", "model identity", () =>
            { var m = (JObject)response.DeepClone(); m["authoring"]["modelVersion"] = "different"; ValidateResponseAuthoring(fixture, m, wire, accepted); });
            report["passed"] = true;
        }
        catch (Exception error) { report["passed"] = false; report["error"] = error.ToString(); throw; }
        finally { WriteJson(output, "report.json", report); }
    }

    private static bool IsProfile(string profile) =>
        profile == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
        || profile == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id
        || profile == LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id
        || profile == LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id;

    private static string RequireOutput(string optIn)
    {
        Require(Application.isBatchMode && Environment.GetEnvironmentVariable(optIn) == "1",
            "Dedicated isolated batch opt-in is required.");
        string output = Environment.GetEnvironmentVariable(OutputEnv);
        Require(!string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output),
            "An explicit absolute QA output is required.");
        string qa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA"))
            + Path.DirectorySeparatorChar;
        string full = Path.GetFullPath(output);
        Require(full.StartsWith(qa, StringComparison.OrdinalIgnoreCase)
            && !File.Exists(Path.Combine(full, "report.json")), "Create-only project QA output required.");
        Directory.CreateDirectory(full);
        return full;
    }

    private static JObject Gates(string schema) => new()
    {
        ["schema"] = schema, ["trainingEligible"] = false, ["humanApprovalClaimed"] = false,
        ["dpoPerformed"] = false, ["releaseReady"] = false, ["fullAuditClosure"] = false
    };

    private static JObject WriteJson(string root, string name, JObject value) =>
        WriteBytes(root, name, GameplayModuleSelectionCanonicalJson.ToUtf8(value)
            .Concat(new[] { (byte)'\n' }).ToArray());

    private static JObject WriteBytes(string root, string name, byte[] bytes)
    {
        using (var stream = new FileStream(Path.Combine(root, name), FileMode.CreateNew, FileAccess.Write))
            stream.Write(bytes, 0, bytes.Length);
        return new JObject { ["path"] = name, ["bytes"] = bytes.Length,
            ["sha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes) };
    }

    private static void Reject(JArray checks, string name, string reason, Action action)
    {
        try { action(); }
        catch (InvalidOperationException error)
        {
            Require(error.Message.IndexOf(reason, StringComparison.OrdinalIgnoreCase) >= 0,
                name + " failed for a different reason: " + error.Message);
            checks.Add(name);
            return;
        }
        throw new InvalidOperationException(name + " was not rejected.");
    }

    private static void Require(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
    }
}
#endif
