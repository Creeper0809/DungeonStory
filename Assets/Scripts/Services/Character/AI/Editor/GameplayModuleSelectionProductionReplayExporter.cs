#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static partial class FormulaPresentationPilotExporter
{
    private const string ReplaySourceEnvironment =
        "DUNGEONSTORY_GAMEPLAY_MODULE_REPLAY_SOURCE";
    private const string ReplayResponseEnvironment =
        "DUNGEONSTORY_GAMEPLAY_MODULE_REPLAY_RESPONSES";
    private const string ReplayOutputEnvironment =
        "DUNGEONSTORY_GAMEPLAY_MODULE_REPLAY_OUTPUT";
    private const string ReplayVersionEnvironment =
        "DUNGEONSTORY_GAMEPLAY_MODULE_REPLAY_VERSION";
    private const string ReplayRowsFileName = "resolution_proofs.jsonl";

    [MenuItem("Tools/DungeonStory/Narrative/Replay Production Module Source V8 (100)")]
    public static void ReplayProductionModuleSelection100FromEnvironment()
    {
        string sourceRoot = Path.GetFullPath(
            RequireEnvironment(ReplaySourceEnvironment));
        string responsePath = Path.GetFullPath(
            RequireEnvironment(ReplayResponseEnvironment));
        string output = Path.GetFullPath(
            RequireEnvironment(ReplayOutputEnvironment));
        string version = RequireVersion(
            RequireEnvironment(ReplayVersionEnvironment));
        JObject result = ReplayProductionModuleSelection100(
            sourceRoot,
            responsePath,
            output,
            version);
        Debug.Log(
            "Production gameplay module-selection replay PASS "
            + result.ToString(Formatting.None));
    }

    internal static JObject ReplayProductionModuleSelection100(
        string sourceRoot,
        string responsePath,
        string outputDirectory,
        string version)
    {
        if (!Directory.Exists(sourceRoot))
            throw new DirectoryNotFoundException(
                "Source-v8 package is missing: " + sourceRoot);
        if (!File.Exists(responsePath))
            throw new FileNotFoundException(
                "Authored response JSONL is missing.",
                responsePath);
        if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory))
            throw new InvalidOperationException(
                "Module replay output is create-only: " + outputDirectory);

        string sourceRowsPath = Path.Combine(
            sourceRoot,
            GameplayModuleSelectionSourceV8PackageExporter.RowsFileName);
        string sourceManifestPath = Path.Combine(sourceRoot, "manifest.json");
        if (!File.Exists(sourceRowsPath) || !File.Exists(sourceManifestPath))
            throw new FileNotFoundException(
                "Source-v8 rows and manifest are required.");
        JObject[] sourceRows = ReadJsonl(sourceRowsPath);
        JObject[] responseRows = ReadJsonl(responsePath);
        if (sourceRows.Length != RowsPerProfile * 4
            || responseRows.Length != sourceRows.Length)
        {
            throw new InvalidOperationException(
                "Production replay requires exactly 100 source and response rows.");
        }

        JObject sourceManifest = JObject.Parse(
            File.ReadAllText(sourceManifestPath, Encoding.UTF8));
        Dictionary<string, JObject> checkpointEntries =
            ((JArray)sourceManifest["checkpoints"]
                ?? throw new InvalidOperationException(
                    "Source manifest has no checkpoints."))
            .OfType<JObject>()
            .ToDictionary(
                value => RequireText(value, "checkpointId"),
                value => value,
                StringComparer.Ordinal);
        Dictionary<string, JObject> responses = responseRows.ToDictionary(
            value => RequireText(value, "logicalRequestId"),
            value => value,
            StringComparer.Ordinal);
        CatalogContext catalogs = LoadAndValidateCatalogs();
        var proofs = new List<JObject>(sourceRows.Length);
        var appliedCheckpoints = new List<(string Relative, JObject Value)>();

        for (int index = 0; index < sourceRows.Length; index++)
        {
            JObject source = sourceRows[index];
            JObject live = RequireObjectToken(source, "liveRequest");
            JObject execution = RequireObjectToken(source, "executionBinding");
            JObject authority = RequireObjectToken(source, "authoritySidecar");
            string logicalRequestId = RequireText(live, "logicalRequestId");
            if (!responses.TryGetValue(logicalRequestId, out JObject responseRecord))
                throw new InvalidOperationException(
                    "Missing authored response for " + logicalRequestId);
            ValidateReplayResponseBinding(source, responseRecord);

            string checkpointId = RequireText(execution, "ownerCheckpointId");
            if (!checkpointEntries.TryGetValue(checkpointId, out JObject entry))
                throw new InvalidOperationException(
                    "Missing source checkpoint manifest entry: " + checkpointId);
            string relativeCheckpointPath = RequireText(entry, "path")
                .Replace('/', Path.DirectorySeparatorChar);
            string checkpointPath = Path.GetFullPath(Path.Combine(
                sourceRoot,
                relativeCheckpointPath));
            if (!File.Exists(checkpointPath)
                || !string.Equals(
                    Sha256File(checkpointPath),
                    RequireText(entry, "sha256"),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Source checkpoint file/hash is invalid: " + checkpointId);
            }
            JObject checkpoint = JObject.Parse(
                File.ReadAllText(checkpointPath, Encoding.UTF8));
            if (!string.Equals(
                    GameplayModuleSelectionCanonicalJson.Hash(checkpoint),
                    RequireText(authority, "ownerSnapshotSha256"),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Source checkpoint canonical hash disagrees with its row: "
                    + checkpointId);
            }

            JToken accepted = responseRecord["acceptedResponse"]
                ?? throw new InvalidOperationException(
                    "Response record has no acceptedResponse.");
            string acceptedJson = accepted.ToString(Formatting.None);
            string profileId = RequireText(source, "profileId");
            string ownerId = RequireText(execution, "ownerId");
            GameplayModuleSelectionRuntimeReplayResult replay = profileId switch
            {
                "CharacterSkillModuleSelection" =>
                    CharacterCombatModuleSelectionSourceCapture
                        .IsCombatCheckpoint(checkpoint)
                            ? CharacterCombatModuleSelectionSourceCapture
                                .ReplaySkill(
                                    ownerId,
                                    checkpoint,
                                    catalogs.Skills,
                                    acceptedJson)
                            : PrimitiveSurvivalCausalProductionEntryDebugScenarios
                                .ReplaySkillModuleSelection(
                                    ownerId,
                                    checkpoint,
                                    catalogs.Skills,
                                    acceptedJson),
                "AcquiredTraitModuleSelection" =>
                    CharacterCombatModuleSelectionSourceCapture
                        .IsCombatCheckpoint(checkpoint)
                            ? CharacterCombatModuleSelectionSourceCapture
                                .ReplayTrait(
                                    ownerId,
                                    checkpoint,
                                    catalogs.TraitSettings,
                                    catalogs.Traits,
                                    acceptedJson)
                            : PrimitiveSurvivalCausalProductionEntryDebugScenarios
                                .ReplayTraitModuleSelection(
                                    ownerId,
                                    checkpoint,
                                    catalogs.TraitSettings,
                                    catalogs.Traits,
                                    acceptedJson),
                "EquipmentEvolutionModuleSelection" =>
                    ReplayEquipmentModuleSelection(
                        ownerId,
                        checkpoint,
                        acceptedJson,
                        index),
                "FacilityEvolutionModuleSelection" =>
                    FacilityEvolutionDebugScenarios.ReplayModuleSelection(
                        ownerId,
                        checkpoint,
                        acceptedJson),
                _ => throw new InvalidOperationException(
                    "Unsupported production replay profile: " + profileId)
            };
            JObject expectedSemantic = ReplaySemanticCheckpoint(checkpoint);
            JObject beforeSemantic = ReplaySemanticCheckpoint(
                replay.BeforeCheckpoint);
            JObject appliedSemantic = ReplaySemanticCheckpoint(
                replay.AppliedCheckpoint);
            JObject restoredSemantic = ReplaySemanticCheckpoint(
                replay.RestoredCheckpoint);
            string expectedBeforeHash = GameplayModuleSelectionCanonicalJson.Hash(
                expectedSemantic);
            string beforeHash = GameplayModuleSelectionCanonicalJson.Hash(
                beforeSemantic);
            string appliedHash = GameplayModuleSelectionCanonicalJson.Hash(
                appliedSemantic);
            string restoredHash = GameplayModuleSelectionCanonicalJson.Hash(
                restoredSemantic);
            if (!string.Equals(
                    beforeHash,
                    expectedBeforeHash,
                    StringComparison.Ordinal)
                || string.Equals(beforeHash, appliedHash, StringComparison.Ordinal)
                || !string.Equals(appliedHash, restoredHash, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Replay state hashes do not prove apply/save/restore for "
                    + logicalRequestId
                    + "; before=" + beforeHash
                    + "; expectedBefore=" + expectedBeforeHash
                    + "; applied=" + appliedHash
                    + "; restored=" + restoredHash
                    + "; beforeDiff="
                    + FirstReplayDifference(expectedSemantic, beforeSemantic)
                    + "; restoreDiff="
                    + FirstReplayDifference(
                        appliedSemantic,
                        restoredSemantic));
            }

            List<JObject> candidates = ResponseCandidates(
                profileId,
                accepted,
                (int)live["candidateCount"]);
            string[] selectedModules = candidates.SelectMany(value =>
                    ReplayRequireStringArray(value, "positiveModuleIds")
                        .Concat(ReplayRequireStringArray(value, "drawbackModuleIds")))
                .ToArray();
            if (selectedModules.Distinct(StringComparer.Ordinal).Count()
                    != selectedModules.Length)
            {
                throw new InvalidOperationException(
                    "Accepted response repeats a module identity and cannot form a closed proof: "
                    + logicalRequestId);
            }
            string[] selectedEvidence = candidates.SelectMany(value =>
                    ReplayRequireStringArray(value, "evidenceFactIds"))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (!selectedEvidence.ToHashSet(StringComparer.Ordinal).SetEquals(
                    replay.EvidenceInfluenceFactIds))
            {
                throw new InvalidOperationException(
                    "Applied evidence influence differs from the accepted response: "
                    + logicalRequestId);
            }
            if (replay.ResultFormulaIds.Count != candidates.Count)
            {
                throw new InvalidOperationException(
                    "Replay did not produce one formula result per candidate: "
                    + logicalRequestId);
            }

            proofs.Add(new JObject
            {
                ["schemaVersion"] = 1,
                ["taskId"] = "gameplay-module-selection",
                ["protocolVersion"] = 3,
                ["profileId"] = profileId,
                ["logicalRequestId"] = logicalRequestId,
                ["sourceRowHash"] = GameplayModuleSelectionCanonicalJson.Hash(source),
                ["responseSha256"] = RequireText(
                    responseRecord,
                    "acceptedResponseSha256"),
                ["authoritySnapshotSha256"] = RequireText(
                    authority,
                    "authoritySnapshotSha256"),
                ["applicationContractId"] = RequireText(
                    authority,
                    "applicationContractId"),
                ["applicationContractSha256"] = RequireText(
                    authority,
                    "applicationContractSha256"),
                ["replayAttemptId"] = "replay:source-v8:production:"
                    + index.ToString("D5", CultureInfo.InvariantCulture),
                ["requestAccepted"] = true,
                ["serviceAccepted"] = true,
                ["applicationCommitted"] = true,
                ["saveRoundTripVerified"] = true,
                ["beforeStateSha256"] = beforeHash,
                ["appliedStateSha256"] = appliedHash,
                ["restoredStateSha256"] = restoredHash,
                ["selectedModuleIds"] = new JArray(selectedModules),
                ["resultFormulaIds"] = new JArray(replay.ResultFormulaIds),
                ["evidenceInfluenceFactIds"] = new JArray(selectedEvidence),
                ["failureReasons"] = new JArray()
            });
            appliedCheckpoints.Add((
                "applied_checkpoints/"
                    + checkpointId.Replace(':', '_')
                    + ".json",
                replay.AppliedCheckpoint));
        }

        if (proofs.GroupBy(value => (string)value["profileId"],
                StringComparer.Ordinal).Any(group => group.Count() != RowsPerProfile))
        {
            throw new InvalidOperationException(
                "Production replay must contain exactly 25 proofs per profile.");
        }

        string staging = outputDirectory + ".staging-"
            + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            string proofsPath = Path.Combine(staging, ReplayRowsFileName);
            WriteCanonicalJsonl(proofsPath, proofs);
            var checkpointFiles = new JArray();
            foreach ((string relative, JObject value) in appliedCheckpoints)
            {
                string path = Path.Combine(
                    staging,
                    relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)
                    ?? throw new InvalidOperationException(
                        "Applied checkpoint parent is unavailable."));
                File.WriteAllBytes(
                    path,
                    GameplayModuleSelectionSourceV8PackageExporter
                        .CheckpointFileBytes(value));
                checkpointFiles.Add(new JObject
                {
                    ["path"] = relative,
                    ["sha256"] = Sha256File(path)
                });
            }
            JObject manifest = new()
            {
                ["schemaVersion"] = 1,
                ["version"] = version,
                ["taskId"] = "gameplay-module-selection",
                ["protocolVersion"] = 3,
                ["sourceManifestSha256"] = Sha256File(sourceManifestPath),
                ["sourceRowsSha256"] = Sha256File(sourceRowsPath),
                ["responseRowsSha256"] = Sha256File(responsePath),
                ["proofRowsSha256"] = Sha256File(proofsPath),
                ["rowCount"] = proofs.Count,
                ["profileCounts"] = new JObject(proofs
                    .GroupBy(value => (string)value["profileId"],
                        StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new JProperty(group.Key, group.Count()))),
                ["actualParserServiceApplicationReplay"] = true,
                ["saveRoundTripVerified"] = true,
                ["controlledSimulation"] = true,
                ["naturalPlayTelemetry"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["appliedCheckpoints"] = checkpointFiles
            };
            string manifestPath = Path.Combine(staging, "manifest.json");
            File.WriteAllBytes(
                manifestPath,
                AppendNewline(GameplayModuleSelectionCanonicalJson.ToUtf8(
                    manifest)));
            Directory.Move(staging, outputDirectory);
            return new JObject
            {
                ["version"] = version,
                ["rowCount"] = proofs.Count,
                ["proofRowsSha256"] = Sha256File(Path.Combine(
                    outputDirectory,
                    ReplayRowsFileName)),
                ["manifestSha256"] = Sha256File(Path.Combine(
                    outputDirectory,
                    "manifest.json")),
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            };
        }
        catch
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            throw;
        }
    }

    private static string FirstReplayDifference(
        JToken expected,
        JToken actual,
        string path = "$")
    {
        if (expected == null || actual == null)
            return expected == null && actual == null
                ? "none"
                : path + ":null-mismatch";
        if (expected.Type != actual.Type)
            return path + ":type=" + expected.Type + "/" + actual.Type;
        if (expected is JObject expectedObject
            && actual is JObject actualObject)
        {
            string[] keys = expectedObject.Properties()
                .Select(value => value.Name)
                .Union(actualObject.Properties().Select(value => value.Name))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            foreach (string key in keys)
            {
                if (expectedObject.Property(key) == null
                    || actualObject.Property(key) == null)
                    return path + "." + key + ":missing";
                string difference = FirstReplayDifference(
                    expectedObject[key], actualObject[key], path + "." + key);
                if (!string.Equals(difference, "none", StringComparison.Ordinal))
                    return difference;
            }
            return "none";
        }
        if (expected is JArray expectedArray
            && actual is JArray actualArray)
        {
            if (expectedArray.Count != actualArray.Count)
                return path + ":count=" + expectedArray.Count + "/"
                    + actualArray.Count;
            for (int index = 0; index < expectedArray.Count; index++)
            {
                string difference = FirstReplayDifference(
                    expectedArray[index], actualArray[index],
                    path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]");
                if (!string.Equals(difference, "none", StringComparison.Ordinal))
                    return difference;
            }
            return "none";
        }
        return JToken.DeepEquals(expected, actual)
            ? "none"
            : path + ":value=" + expected.ToString(Formatting.None)
                + "/" + actual.ToString(Formatting.None);
    }

    private static JObject ReplaySemanticCheckpoint(JObject checkpoint)
    {
        JObject result = checkpoint == null
            ? throw new ArgumentNullException(nameof(checkpoint))
            : (JObject)checkpoint.DeepClone();
        // A restore intentionally advances the ledger world epoch.  It is a
        // replay-fencing token, not gameplay state.  Compare and hash the
        // persistent semantic state while the profile helper independently
        // proves that a fresh composition accepted the saved checkpoint.
        NormalizeReplayWorldEpochs(result);
        return result;
    }

    private static void NormalizeReplayWorldEpochs(JToken token)
    {
        if (token is JObject value)
        {
            if (value.Property("worldEpoch") != null)
                value["worldEpoch"] = 0;
            foreach (JProperty property in value.Properties().ToArray())
                NormalizeReplayWorldEpochs(property.Value);
            return;
        }
        if (token is JArray array)
        {
            foreach (JToken item in array)
                NormalizeReplayWorldEpochs(item);
        }
    }

    private static GameplayModuleSelectionRuntimeReplayResult
        ReplayEquipmentModuleSelection(
            string ownerId,
            JObject checkpoint,
            string responseJson,
            int index)
    {
        if (!NarrativeExactKeyContract.TryValidateProfileResponse(
                LocalLlmRequestProfiles.EquipmentEvolutionModuleSelection.Id,
                responseJson,
                out _,
                out string responseError)
            || !LlmJsonResponseParser.TryParse(
                responseJson,
                out EquipmentEvolutionModuleSelectionResponseDto response,
                out responseError))
        {
            throw new InvalidOperationException(
                "Equipment replay response is invalid: " + responseError);
        }

        JObject before = (JObject)checkpoint.DeepClone();
        using EquipmentReplayState state = RestoreEquipmentReplayState(
            checkpoint,
            "run:module-replay:equipment:apply:"
                + index.ToString("D5", CultureInfo.InvariantCulture));
        EvolutionNode pending = state.Probe.Instance.evolution.evolutionNodes
            .Single(value => value != null
                && string.Equals(
                    value.moduleSelectionId,
                    response.selectionId,
                    StringComparison.Ordinal));
        string resultFormulaId = pending.nodeId;
        if (!state.Runtime.TryCommitModuleSelection(
                state.Probe.Instance.instanceId,
                response,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "The live equipment parser/service rejected the replay response: "
                + failureReason);
        }
        JObject applied = CaptureEquipmentReplayCheckpoint(checkpoint, state);
        using EquipmentReplayState restoredState = RestoreEquipmentReplayState(
            applied,
            "run:module-replay:equipment:restore:"
                + index.ToString("D5", CultureInfo.InvariantCulture));
        EvolutionNode restoredNode = restoredState.Probe.Instance.evolution
            .evolutionNodes.Single(value => value != null
                && string.Equals(
                    value.nodeId,
                    resultFormulaId,
                    StringComparison.Ordinal));
        if (restoredNode.presentationState
                != EquipmentEvolutionPresentationState.Ready
            || !restoredNode.active
            || !restoredNode.mechanicallyUnlocked
            || restoredState.Probe.Instance.evolution.presentationRequests.Any(
                value => value != null
                    && string.Equals(
                        value.presentationId,
                        response.selectionId,
                        StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Applied equipment formula did not survive save restore.");
        }
        JObject restored = CaptureEquipmentReplayCheckpoint(
            applied,
            restoredState,
            incrementRevision: false);
        return new GameplayModuleSelectionRuntimeReplayResult(
            before,
            applied,
            restored,
            new[] { resultFormulaId },
            response.evidenceFactIds.Distinct(StringComparer.Ordinal));
    }

    private static EquipmentReplayState RestoreEquipmentReplayState(
        JObject checkpoint,
        string runId)
    {
        if ((string)checkpoint?["schema"] != "dungeonstory-module-source-equipment-owner@3")
            throw new InvalidOperationException("Equipment replay checkpoint schema is unsupported.");
        JObject sourceCheckpoint = RequireObjectToken(
            checkpoint,
            "sourceCheckpoint");
        EvolutionGameplayOutcomeEditorFixture outcomes =
            EquipmentRelocationNarrativeEvidenceFixture.RestoreOutcomeFixture(
                sourceCheckpoint,
                runId);
        try
        {
            GameplayOutcomeLedger ledger = outcomes.Ledger;
            JObject componentJson = RequireObjectToken(
                checkpoint,
                "equipmentComponent");
            ItemInstanceComponentSaveData component = JsonUtility.FromJson<
                ItemInstanceComponentSaveData>(
                    componentJson.ToString(Formatting.None));
            if (!EquipmentItemStateCodec.TryDecode(
                    component,
                    out CombatEquipmentInstance instance,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Equipment replay checkpoint decode failed: "
                    + failureReason);
            }
            EquipmentModuleSelectionPublicationData[] ownerHistory = outcomes
                .ModuleSelections
                .Capture()
                .observations
                .Where(value => string.Equals(
                    value.fact.equipmentInstanceId,
                    instance.instanceId,
                    StringComparison.Ordinal))
                .ToArray();
            long journalRevision = ownerHistory.Length == 0
                ? 0L
                : ownerHistory.Max(value => value.fact.afterRevision);
            if (journalRevision != (instance.evolution?.moduleSelectionRevision ?? 0L)
                || ownerHistory.Any(value => !string.Equals(
                    value.fact.equipmentDefinitionId,
                    instance.definitionId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Equipment replay checkpoint module-selection owner join is invalid.");
            }
            ICombatEquipmentRuntime port = DispatchProxy.Create<
                ICombatEquipmentRuntime,
                ProductionEquipmentReplayProbe>();
            ProductionEquipmentReplayProbe probe =
                (ProductionEquipmentReplayProbe)(object)port;
            probe.Instance = instance;
            EquipmentEvolutionRuntime runtime =
                (EquipmentEvolutionRuntime)FormatterServices.GetUninitializedObject(
                    typeof(EquipmentEvolutionRuntime));
            SetRuntimeField(runtime, "equipment", port);
            SetRuntimeField(
                runtime,
                "ledgerCompactor",
                new UsageLedgerCompactor());
            SetRuntimeField(
                runtime,
                "aggregateRootStore",
                new DungeonRuntimeAggregateRootStore());
            SetRuntimeField(
                runtime,
                "evidenceUseTransaction",
                new GameplayOutcomeEvidenceUseTransaction(
                    ledger,
                    new GameplayOutcomeNarrativeEvidenceQuery(ledger)));
            SetRuntimeField(
                runtime,
                "moduleSelections",
                outcomes.ModuleSelections);
            SetRuntimeField(
                runtime,
                "sessionStateProvider",
                new FixedGameSessionStateProvider(41));
            return new EquipmentReplayState(probe, runtime, outcomes);
        }
        catch
        {
            outcomes.Dispose();
            throw;
        }
    }

    private static JObject CaptureEquipmentReplayCheckpoint(
        JObject template,
        EquipmentReplayState state,
        bool incrementRevision = true)
    {
        JObject result = (JObject)template.DeepClone();
        if (incrementRevision)
            result["revision"] = ((int?)result["revision"] ?? 0) + 1;
        result["equipmentComponent"] = JObject.Parse(JsonUtility.ToJson(
            EquipmentItemStateCodec.Encode(state.Probe.Instance)));
        JObject sourceCheckpoint = RequireObjectToken(
            result,
            "sourceCheckpoint");
        sourceCheckpoint["outcomeLedger"] = JObject.Parse(JsonUtility.ToJson(
            state.Ledger.CaptureGameplayOutcomes()));
        sourceCheckpoint["moduleSelectionPublications"] =
            EquipmentRelocationNarrativeEvidenceFixture
                .CaptureModuleSelectionPublications(
                    state.ModuleSelections);
        return result;
    }

    private static void SetRuntimeField(
        EquipmentEvolutionRuntime runtime,
        string name,
        object value)
    {
        FieldInfo field = typeof(EquipmentEvolutionRuntime).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(
                typeof(EquipmentEvolutionRuntime).FullName,
                name);
        field.SetValue(runtime, value);
    }

    private static void ValidateReplayResponseBinding(
        JObject source,
        JObject response)
    {
        JObject live = RequireObjectToken(source, "liveRequest");
        string sourceHash = GameplayModuleSelectionCanonicalJson.Hash(source);
        foreach ((string recordKey, string sourceValue) in new[]
        {
            ("taskId", RequireText(source, "taskId")),
            ("profileId", RequireText(source, "profileId")),
            ("logicalRequestId", RequireText(live, "logicalRequestId")),
            ("transportAttemptId", RequireText(live, "transportAttemptId")),
            ("sourceRowHash", sourceHash)
        })
        {
            if (!string.Equals(
                    RequireText(response, recordKey),
                    sourceValue,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Response binding differs at " + recordKey);
            }
        }
        if ((int?)response["schemaVersion"] != 1
            || (int?)response["protocolVersion"] != 3
            || (bool?)response["humanApprovalClaimed"] != false)
        {
            throw new InvalidOperationException(
                "Response schema/protocol/approval state is invalid.");
        }
        JToken accepted = response["acceptedResponse"]
            ?? throw new InvalidOperationException(
                "Response record has no accepted response.");
        if (!string.Equals(
                GameplayModuleSelectionCanonicalJson.Hash(accepted),
                RequireText(response, "acceptedResponseSha256"),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Accepted response canonical hash is invalid.");
        }
    }

    private static List<JObject> ResponseCandidates(
        string profileId,
        JToken accepted,
        int candidateCount)
    {
        List<JObject> values;
        if (string.Equals(
                profileId,
                LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id,
                StringComparison.Ordinal))
        {
            values = ((accepted as JObject)?["candidates"] as JArray)
                ?.OfType<JObject>()
                .ToList()
                ?? throw new InvalidOperationException(
                    "Skill response has no candidate array.");
        }
        else
        {
            values = new List<JObject>
            {
                accepted as JObject
                    ?? throw new InvalidOperationException(
                        "Single response is not an object.")
            };
        }
        if (values.Count != candidateCount)
            throw new InvalidOperationException(
                "Accepted response candidate count is invalid.");
        return values;
    }

    private static JObject RequireObjectToken(JObject owner, string key) =>
        owner?[key] as JObject
        ?? throw new InvalidOperationException(
            "Required object is missing: " + key);

    private static string RequireText(JObject owner, string key)
    {
        string value = (string)owner?[key] ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Required canonical text is missing: " + key);
        return value;
    }

    private static string[] ReplayRequireStringArray(JObject owner, string key)
    {
        string[] values = (owner?[key] as JArray)?.Values<string>().ToArray()
            ?? throw new InvalidOperationException(
                "Required string array is missing: " + key);
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new InvalidOperationException(
                "Required string array is invalid: " + key);
        return values;
    }

    private static void WriteCanonicalJsonl(
        string path,
        IEnumerable<JObject> rows)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        foreach (JObject row in rows)
        {
            byte[] bytes = GameplayModuleSelectionCanonicalJson.ToUtf8(row);
            stream.Write(bytes, 0, bytes.Length);
            stream.WriteByte((byte)'\n');
        }
    }

    private static byte[] AppendNewline(byte[] value)
    {
        byte[] result = new byte[value.Length + 1];
        Buffer.BlockCopy(value, 0, result, 0, value.Length);
        result[^1] = (byte)'\n';
        return result;
    }

    private sealed class EquipmentReplayState : IDisposable
    {
        internal EquipmentReplayState(
            ProductionEquipmentReplayProbe probe,
            EquipmentEvolutionRuntime runtime,
            EvolutionGameplayOutcomeEditorFixture outcomes)
        {
            Probe = probe ?? throw new ArgumentNullException(nameof(probe));
            Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            Outcomes = outcomes ?? throw new ArgumentNullException(nameof(outcomes));
        }

        internal ProductionEquipmentReplayProbe Probe { get; }
        internal EquipmentEvolutionRuntime Runtime { get; }
        private EvolutionGameplayOutcomeEditorFixture Outcomes { get; }
        internal GameplayOutcomeLedger Ledger => Outcomes.Ledger;
        internal EquipmentModuleSelectionPublicationRuntime ModuleSelections =>
            Outcomes.ModuleSelections;

        public void Dispose() => Outcomes.Dispose();
    }

    public class ProductionEquipmentReplayProbe : DispatchProxy
    {
        public CombatEquipmentInstance Instance { get; set; }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            if (targetMethod.Name == nameof(ICombatEquipmentRuntime.TryGetInstance))
            {
                bool found = Instance != null
                    && string.Equals(
                        arguments[0] as string,
                        Instance.instanceId,
                        StringComparison.Ordinal);
                arguments[1] = found ? Instance.Clone() : null;
                return found;
            }
            if (targetMethod.Name
                == nameof(ICombatEquipmentRuntime.TryUpdateEvolutionState))
            {
                bool found = Instance != null
                    && string.Equals(
                        arguments[0] as string,
                        Instance.instanceId,
                        StringComparison.Ordinal);
                if (found)
                    Instance.evolution = ((EquipmentEvolutionState)arguments[1])
                        .Clone();
                return found;
            }
            throw new NotSupportedException(
                "Unexpected production equipment replay call: "
                + targetMethod.Name);
        }
    }
}
#endif
