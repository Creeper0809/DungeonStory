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
/// One native generation from the unchanged original r393/r63 Skill prompt.
/// This does not claim a fresh game-owner restore or apply a model response.
/// Explicit correction mode observes the existing owner's failure-feedback
/// dispatch before one native request; it never edits a reply or frozen facts.
/// </summary>
public static class OriginalSkillProseNativeBatchRunner
{
    private const string SourceRelative =
        "Artifacts/QA/AuditRemediation-20260926-r1/u3-source-v9-skill-staff-live-r63/unity-evidence/source-v9";
    private const string SourceManifestSha =
        "sha256:c3480e9428d7e6a0a98ca62087465220967acd8be5756df83c8e5805e73b0d83";
    private const string OwnerId = "character:staff:1:02";
    private const string RequestKey = "skill:character:staff:1:02:Active:5:r1:g1:l1";
    private const string PriorR407ResolvedRelative =
        "Artifacts/QA/AuditRemediation-20260926-r1/u6-original-skill-native-r407-r1/unity-evidence/original-skill-prose-native/resolved-content.json";
    private const string PriorR407ResolvedSha =
        "sha256:f05546b15b64daf1c5476b8a2f08beb8355fd89ffe807f84e48e8e64319a6b47";

    public static void RunOrThrow()
    {
        Require(Application.isBatchMode, "Explicit isolated Unity batch is required.");
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string qaOutput = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(qaOutput) && Path.IsPathRooted(qaOutput),
            "An absolute isolated QA output is required.");
        // Edit-mode runs deliberately have no gameplay persistence root.
        // Require this launcher's actual private-station probe, not player saves.
        string probePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(qaOutput)),
            "isolation-probe.txt");
        Require(File.Exists(probePath)
                && File.ReadAllText(probePath).Contains("PASS_NONINTERACTIVE_PROBE")
                && File.ReadAllText(probePath).Contains("visible=false")
                && File.ReadAllText(probePath).Contains("inJob=true"),
            "Current noninteractive launcher probe is required.");
        string replayMode = Environment.GetEnvironmentVariable("DUNGEONSTORY_ORIGINAL_SKILL_OWNER_REPLAY");
        bool canonicalCorrection = replayMode == "r514-correction";
        if (!string.IsNullOrEmpty(replayMode))
        {
            Require(replayMode == "r514" || canonicalCorrection,
                "Unsupported explicit original Skill owner-replay mode.");
            if (!canonicalCorrection)
            {
                RunOwnerReplayOrThrow(project, qaOutput);
                return;
            }
        }
        string output = Path.GetFullPath(Path.Combine(qaOutput, "original-skill-prose-native"));
        Require(output.StartsWith(Path.Combine(project, "Artifacts", "QA")
                + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !Directory.Exists(output) && !File.Exists(output),
            "Create-only project QA output required.");
        Directory.CreateDirectory(output);
        JObject report = new()
        {
            ["schema"] = "dungeonstory-original-skill-prose-native@1",
            ["passed"] = false, ["attempts"] = 0, ["networkSent"] = false,
            ["actualModelResponsesObserved"] = 0, ["gameplayApplied"] = false,
            ["freshOfficialOwnerRestore"] = false,
            ["sourcePendingOrigin"] = "historical actual production request; unchanged saved prompt",
            ["sourceIdentityRewritten"] = false, ["choiceOrProseRepair"] = false,
            ["semanticProseQualityValidated"] = false,
            ["trainingEligible"] = false, ["humanApprovalClaimed"] = false,
            ["dpoPerformed"] = false, ["releaseReady"] = false, ["fullAuditClosure"] = false
        };
        DungeonStoryLlmHostProcess host = null;
        try
        {
            // One owned Editor startup covers the changed lexical contracts
            // before the single native request; this never repairs an answer.
            V25NarrativeInferenceDebugScenarios.RunProseLexicalGrammarScenario();
            report["lexicalTransportQaPassed"] = true;
            string source = Path.GetFullPath(Path.Combine(project, SourceRelative));
            byte[] manifestBytes = File.ReadAllBytes(Path.Combine(source, "manifest.json"));
            Require(Hash(manifestBytes) == SourceManifestSha, "Original source manifest differs.");
            JObject manifest = ParseObject(manifestBytes, "original source manifest");
            JObject selectionSource = ParseObject(ReadDescriptor(source,
                RequireObject(manifest, "selectionSource"), "original selection source"),
                "original selection source");
            LocalLlmRequestProfile profile = LocalLlmRequestProfiles.CharacterSkillModuleSelection;
            JObject selection = ((JArray)selectionSource["selections"]).OfType<JObject>()
                .Single(value => (string)value["profileId"] == profile.Id);
            Require((string)selection["ownerId"] == OwnerId
                    && (string)selection["triggerId"] == RequestKey
                    && (bool?)selectionSource["responseApplied"] == false
                    && (bool?)selectionSource["responseDataIncluded"] == false,
                "The original owner/request is not the expected response-free Miru request.");
            JObject capture = RequireObject(selection, "capture");
            byte[] pendingBytes = ReadDescriptor(source, RequireObject(capture, "pending"), "original pending");
            byte[] promptBytes = ReadDescriptor(source, RequireObject(capture, "originalPrompt"), "original prompt");
            byte[] worldBytes = ReadDescriptor(source, RequireObject(capture, "officialWorldCheckpoint"), "original world");
            byte[] oldWire = ReadDescriptor(source,
                RequireObject(RequireObject(selection, "wire"), "original"), "historical wire");
            CharacterSkillDraft draft = JsonUtility.FromJson<CharacterSkillDraft>(Encoding.UTF8.GetString(pendingBytes));
            Require(draft != null && draft.requestKey == RequestKey && draft.ownerBinding?.persistentId == OwnerId
                    && !draft.isReady && !draft.permanentlyChosen && draft.chosenIndex == -1
                    && draft.nextPresentationIndex == 0 && draft.moduleSelectionOffers?.Count == 3,
                "The original draft identity/readiness/offers differ.");
            NarrativePublicContextMaterial material = draft.RestoreRequestPublicContext();
            Require(material != null && material.SubjectId == OwnerId
                    && !string.IsNullOrWhiteSpace(draft.requestContext?.characterName),
                "The original frozen request context is unavailable.");
            byte[] priorResolved = File.ReadAllBytes(Path.Combine(project, PriorR407ResolvedRelative));
            Require(Hash(priorResolved) == PriorR407ResolvedSha,
                "Preserved r407 native response differs.");
            Require(!LlmJsonResponseParser.TryParse(profile.Id, Encoding.UTF8.GetString(priorResolved),
                    out CharacterSkillModuleSelectionBatchResponseDto priorResponse, out string priorParseError)
                    && priorResponse?.candidates?.Count == 3
                    && priorParseError.Contains("repeats another candidate's skill name", StringComparison.Ordinal),
                "The real r407 batch was not rejected for duplicate skill names.");
            Require(!priorResponse.candidates[0].Validate(out string priorRejectError, draft.requestContext.characterName)
                    && priorRejectError.Contains("not only the character name", StringComparison.Ordinal),
                "The real r407 response was not rejected at the new skill-name boundary.");
            report["priorR407NameBoundaryReject"] = true;
            report["priorR407ResolvedSha256"] = Hash(priorResolved);
            report["priorR407DuplicateNameRejectReason"] = priorParseError;
            report["priorR407RejectReason"] = priorRejectError;
            UnityEngine.Debug.Log("ORIGINAL_SKILL_R407_NAME_REJECT=PASS");
            string prompt = Encoding.UTF8.GetString(promptBytes);
            if (canonicalCorrection)
            {
                JObject retry = RunOwnerReplayOrThrow(project, qaOutput, captureCanonicalRetry: true);
                Require((bool?)retry["canonicalRetryCaptured"] == true
                        && (bool?)retry["retryPromptOriginalWithoutOnlyCorrectionLine"] == true
                        && (bool?)retry["readyCandidatesOrEffectsAfterRetryCapture"] == false,
                    "The existing production owner did not capture its unchanged-context canonical retry.");
                prompt = (string)retry["canonicalRetryPrompt"];
                Require(!string.IsNullOrWhiteSpace(prompt) && prompt != Encoding.UTF8.GetString(promptBytes),
                    "Canonical correction must not resend the identical original prompt.");
                report["canonicalOwnerRetryObservation"] = WriteBytes(output,
                    "canonical-owner-retry-observation.json", GameplayModuleSelectionCanonicalJson.ToUtf8(retry));
                report["canonicalCorrectionPrompt"] = WriteBytes(output,
                    "canonical-correction-prompt.txt", Encoding.UTF8.GetBytes(prompt));
                report["mode"] = "r514-production-owner-canonical-correction-native";
                report["sourcePendingOrigin"] = "unchanged original pending -> actual bad response owner rejection -> canonical retry";
            }
            LlmStaticSchemaDefinition schema = LlmStaticSchemaCatalog.Require(profile.Id);
            Require(schema.Version == (int)selection["schemaVersion"]
                    && schema.Hash == (string)selection["schemaHash"],
                "The original static response contract changed; this is not a comparable transport test.");
            string oldModel = RequireText(ParseObject(oldWire, "historical wire"), "model");
            byte[] wire = DungeonStoryHostStructuredChatBackend.BuildRequestPayload(oldModel, profile, schema, prompt);
            Require(wire.SequenceEqual(DungeonStoryHostStructuredChatBackend.BuildRequestPayload(
                    oldModel, profile, schema, prompt)), "Current production serializer is not deterministic.");
            JObject oldRequest = ParseObject(oldWire, "historical wire");
            JObject currentRequest = ParseObject(wire, "current production wire");
            Require(JToken.DeepEquals(oldRequest["max_tokens"], currentRequest["max_tokens"])
                    && JToken.DeepEquals(oldRequest["temperature"], currentRequest["temperature"])
                    && JToken.DeepEquals(oldRequest["chat_template_kwargs"], currentRequest["chat_template_kwargs"]),
                "Model output limits, sampling or thinking mode changed.");
            report["sourceManifestSha256"] = SourceManifestSha;
            report["ownerId"] = OwnerId;
            report["requestKey"] = RequestKey;
            report["originalPendingSha256"] = Hash(pendingBytes);
            report["originalPromptSha256"] = Hash(promptBytes);
            report["originalWorldSha256"] = Hash(worldBytes);
            report["historicalWireSha256"] = Hash(oldWire);
            report["requestContextHash"] = draft.requestContextHash;
            report["publicContextSemanticHash"] = material.SemanticHash;
            report["staticSchemaHash"] = schema.Hash;
            report["currentWire"] = WriteBytes(output, "request-wire.json", wire);
            report["originalPrompt"] = WriteBytes(output, "original-prompt.txt", promptBytes);
            report["currentWireOrigin"] = canonicalCorrection
                ? "actual owner canonical correction prompt -> current production BuildRequestPayload"
                : "unchanged original prompt -> current production BuildRequestPayload";
            report["historicalWireReused"] = false;
            byte[] hostManifestBytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,
                "DungeonStoryLlm", "manifest.json"));
            JObject hostManifest = ParseObject(hostManifestBytes, "installed host manifest");
            string alias = Path.GetFileNameWithoutExtension(RequireText(hostManifest, "modelFile"));
            Require(alias == oldModel, "Installed model differs from the original request.");
            report["hostManifest"] = WriteBytes(output, "host-manifest.json", hostManifestBytes);
            Require(Process.GetProcessesByName("DungeonStoryLlmHost").Length == 0,
                "An existing host is not owned by this test.");
            Require(DungeonStoryLlmHostProcess.TryStart(Application.streamingAssetsPath,
                out host, out string startupError), startupError);
            Require(host != null && host.IsRunning && host.BackendKind == "LlamaCppServer"
                    && host.TrainingState == "base-untrained" && !host.ReleaseCertified,
                "Installed host identity/gates differ.");
            Require(Uri.TryCreate(host.Endpoint, UriKind.Absolute, out Uri endpoint) && endpoint.IsLoopback,
                "Only the owned loopback host is allowed.");
            report["modelVersion"] = host.ModelVersion;
            using var handler = new HttpClientHandler { UseProxy = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", host.SessionToken);
            using var body = new ByteArrayContent(wire);
            body.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            var stopwatch = Stopwatch.StartNew();
            report["attempts"] = 1;
            report["networkSent"] = true;
            using HttpResponseMessage http = client.PostAsync(host.Endpoint.TrimEnd('/')
                + "/v1/chat/completions", body).GetAwaiter().GetResult();
            byte[] envelopeBytes = http.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            stopwatch.Stop();
            report["elapsedMilliseconds"] = stopwatch.ElapsedMilliseconds;
            report["httpStatus"] = (int)http.StatusCode;
            report["responseEnvelope"] = WriteBytes(output, "response-envelope.json", envelopeBytes);
            Require(http.IsSuccessStatusCode, "Local model HTTP failure: " + (int)http.StatusCode);
            var backend = new DungeonStoryHostStructuredChatBackend();
            Require(backend.TryExtractContent(Encoding.UTF8.GetString(envelopeBytes),
                out string content, out string extractionError), extractionError);
            report["actualModelResponsesObserved"] = 1;
            report["observedContent"] = WriteBytes(output, "observed-content.json", Encoding.UTF8.GetBytes(content));
            Require(DungeonStoryHostStructuredChatBackend.TryResolveModelContent(profile, prompt, content,
                out string resolved, out string resolveError), resolveError);
            report["resolvedContent"] = WriteBytes(output, "resolved-content.json", Encoding.UTF8.GetBytes(resolved));
            JObject resolvedObject = ParseObject(Encoding.UTF8.GetBytes(resolved), "resolved native response");
            string[] narrativeFlavors = ((JArray)resolvedObject["candidates"]).OfType<JObject>()
                .Select(value => ((string)value["narrativeFlavor"] ?? string.Empty).Trim()).ToArray();
            report["proseShapeObservations"] = new JObject
            {
                ["narrativeCount"] = narrativeFlavors.Length,
                ["distinctNarrativeCount"] = narrativeFlavors.Distinct(StringComparer.Ordinal).Count(),
                ["terminalPeriodCount"] = narrativeFlavors.Count(value => value.EndsWith(".", StringComparison.Ordinal)),
                ["narrativeLengths"] = new JArray(narrativeFlavors.Select(value => value.Length)),
                ["semanticsOrHumanApprovalInferredFromShape"] = false
            };
            Require(LlmJsonResponseParser.TryParse(profile.Id, resolved,
                    out CharacterSkillModuleSelectionBatchResponseDto response, out string parseError), parseError);
            Require(response.Validate(out string validationError, draft.requestContext.characterName), validationError);
            Require(response.candidates.Count == draft.moduleSelectionOffers.Count,
                "Native response candidate cardinality differs from the original pending.");
            report["skillNameBoundaryValidated"] = true;
            report["passed"] = true;
            report["status"] = "PASS_ORIGINAL_PROMPT_NATIVE_LEXICAL_AND_DTO_NOT_FRESH_OWNER_OR_GAMEPLAY";
            UnityEngine.Debug.Log("ORIGINAL_SKILL_PROSE_NATIVE=PASS");
        }
        catch (Exception error)
        {
            report["error"] = error.ToString();
            throw;
        }
        finally
        {
            host?.Dispose();
            WriteBytes(output, "report.json", GameplayModuleSelectionCanonicalJson.ToUtf8(report));
        }
    }

    private static JObject RunOwnerReplayOrThrow(string project, string qaOutput,
        bool captureCanonicalRetry = false)
    {
        string output = Path.GetFullPath(Path.Combine(qaOutput, "original-skill-owner-replay"));
        Require(output.StartsWith(Path.Combine(project, "Artifacts", "QA")
                + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !Directory.Exists(output) && !File.Exists(output),
            "Create-only owner-replay QA output required.");
        Directory.CreateDirectory(output);
        JObject report = new()
        {
            ["schema"] = "dungeonstory-original-skill-native-owner-replay@1",
            ["mode"] = captureCanonicalRetry ? "r514-canonical-owner-retry-capture" : "r514-negative-owner-replay",
            ["passed"] = false,
            ["nativeModelRequests"] = 0, ["networkSent"] = false,
            ["controlledActorFixture"] = true, ["freshOfficialOwnerRestore"] = false,
            ["wholeWorldRestored"] = false, ["naturalGameplay"] = false,
            ["responseRewritten"] = false, ["semanticProseQualityValidated"] = false,
            ["trainingEligible"] = false, ["humanApprovalClaimed"] = false,
            ["dpoPerformed"] = false, ["releaseReady"] = false, ["fullAuditClosure"] = false
        };
        try
        {
            Require(Process.GetProcessesByName("DungeonStoryLlmHost").Length == 0,
                "Owner replay must not use a running model host.");
            string source = Path.GetFullPath(Path.Combine(project, SourceRelative));
            byte[] sourceManifest = File.ReadAllBytes(Path.Combine(source, "manifest.json"));
            Require(Hash(sourceManifest) == SourceManifestSha, "Original source manifest differs.");
            JObject manifest = ParseObject(sourceManifest, "original source manifest");
            JObject selections = ParseObject(ReadDescriptor(source,
                RequireObject(manifest, "selectionSource"), "original selection source"), "selection source");
            JObject selection = ((JArray)selections["selections"]).OfType<JObject>()
                .Single(value => (string)value["profileId"] == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id);
            Require((string)selection["ownerId"] == OwnerId && (string)selection["triggerId"] == RequestKey,
                "Original owner/request differs.");
            JObject capture = RequireObject(selection, "capture");
            byte[] pending = ReadDescriptor(source, RequireObject(capture, "pending"), "original pending");
            byte[] prompt = ReadDescriptor(source, RequireObject(capture, "originalPrompt"), "original prompt");
            byte[] worldBytes = ReadDescriptor(source,
                RequireObject(capture, "officialWorldCheckpoint"), "original world");
            JObject world = ParseObject(worldBytes, "original world");
            JObject actorSection = ((JArray)world["sections"]).OfType<JObject>()
                .Single(value => (string)value["sectionId"] == "characters.world");
            JObject actorWorld = JObject.Parse((string)actorSection["payloadJson"]);
            JObject sourceActor = ((JArray)actorWorld["actors"]).OfType<JObject>()
                .Single(value => (string)value["persistentId"] == OwnerId);
            var ownerSnapshot = new CharacterProgressionSnapshot(
                (int)sourceActor["level"], (int)sourceActor["currentExperience"],
                DungeonSaveJson.Deserialize<CharacterGrowthState>(sourceActor["growth"].ToString()),
                DungeonSaveJson.Deserialize<CharacterNarrativeLedger>(sourceActor["narrative"].ToString()),
                DungeonSaveJson.Deserialize<CharacterAcquiredTraitAggregateState>(sourceActor["acquiredTraits"].ToString()));
            string responsePath = Path.Combine(project,
                "Artifacts/QA/AuditRemediation-20260926-r1/u6-original-skill-native-r514-r1/unity-evidence/original-skill-prose-native/resolved-content.json");
            byte[] response = File.ReadAllBytes(responsePath);
            Require(Hash(response) == "sha256:4d2fde39f138d0036b82f821808a62f647210d637aad8a5bfe6346f73bb723f2",
                "Preserved actual r514 response differs.");
            report["sourceManifestSha256"] = SourceManifestSha;
            report["originalPendingSha256"] = Hash(pending);
            report["originalPromptSha256"] = Hash(prompt);
            report["originalWorldSha256"] = Hash(worldBytes);
            report["actualNativeResponseSha256"] = Hash(response);
            report["ownerReplay"] = CharacterProgressionDebugScenarios.ReplayOriginalNativeSkillOwner(
                DungeonSaveJson.Deserialize<CharacterSkillDraft>(Encoding.UTF8.GetString(pending)),
                ownerSnapshot, (CharacterLifecycleState)(int)sourceActor["lifecycleState"],
                (long)sourceActor["lifecycleRevision"], Encoding.UTF8.GetString(prompt), Encoding.UTF8.GetString(response),
                captureCanonicalRetry);
            Require((bool?)report["ownerReplay"]["expectedOwnerNameReject"] == true,
                "Actual response did not fail at the expected owner-name guard: "
                    + (string)report["ownerReplay"]["lastDiagnostic"]);
            Require(response.SequenceEqual(File.ReadAllBytes(responsePath)), "Actual response bytes changed during replay.");
            report["passed"] = true;
            report["status"] = "PASS_ACTUAL_RESPONSE_REJECTED_BY_PRODUCTION_OWNER_GUARD_CONTROLLED_FIXTURE";
            UnityEngine.Debug.Log("ORIGINAL_SKILL_OWNER_REPLAY=PASS");
            return (JObject)report["ownerReplay"];
        }
        catch (Exception error)
        {
            report["error"] = error.ToString();
            throw;
        }
        finally
        {
            WriteBytes(output, "report.json", GameplayModuleSelectionCanonicalJson.ToUtf8(report));
        }
    }

    private static string Hash(byte[] bytes) => GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes);

    private static JObject WriteBytes(string root, string name, byte[] bytes)
    {
        using (var stream = new FileStream(Path.Combine(root, name), FileMode.CreateNew, FileAccess.Write))
            stream.Write(bytes, 0, bytes.Length);
        return new JObject { ["path"] = name, ["bytes"] = bytes.Length, ["sha256"] = Hash(bytes) };
    }

    private static void Require(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
    }
}
#endif
