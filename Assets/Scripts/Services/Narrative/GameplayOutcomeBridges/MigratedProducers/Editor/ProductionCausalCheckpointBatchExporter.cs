#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class ProductionCausalPlanBatchExporter
{
    private const string CheckpointSchema =
        "dungeonstory-causal-gameplay-checkpoint@2";
    private const string LegacyCheckpointSchema =
        "dungeonstory-causal-gameplay-checkpoint@1";
    private const string ResumeAttestationSchema =
        "dungeonstory-causal-gameplay-resume-attestation@1";
    private const string CheckpointParent =
        "Artifacts/Exports/CausalGameplayCheckpoint";

    public static void RunCheckpointBatchFromEnvironment()
    {
        string version = RequireVersion(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_CHECKPOINT_VERSION"));
        string planPath = RequireExistingPath(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_PLAN_PATH"), "plan");
        string catalogPath = RequireExistingPath(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_CATALOG_PATH"), "catalog");
        if (!int.TryParse(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_CAUSAL_CHECKPOINT_AFTER"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int checkpointAfter))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_CAUSAL_CHECKPOINT_AFTER must be a positive integer.");
        }
        string path = CreateCheckpoint(
            version,
            planPath,
            catalogPath,
            checkpointAfter);
        Debug.Log("CAUSAL_GAMEPLAY_CHECKPOINT=PASS; path=" + path);
    }

    public static void RunResumeBatchFromEnvironment()
    {
        string version = RequireVersion(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_EXECUTION_VERSION"));
        string checkpointPath = RequireExistingDirectory(
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_CAUSAL_CHECKPOINT_PATH"),
            "checkpoint directory");
        string path = ResumeCheckpoint(version, checkpointPath);
        Debug.Log("CAUSAL_GAMEPLAY_RESUME=PASS; path=" + path);
    }

    private static string CreateCheckpoint(
        string version,
        string planPath,
        string catalogPath,
        int checkpointAfter)
    {
        string projectRoot = ProjectRoot();
        string destination = PrepareCreateOnlyDestination(
            projectRoot,
            CheckpointParent,
            version);
        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            (CausalGameplayPlan plan, JObject catalog, string catalogSha256) =
                LoadPlanAndCatalog(projectRoot, planPath, catalogPath);
            Require(checkpointAfter > 0 && checkpointAfter < plan.Nodes.Count,
                "Checkpoint position must leave at least one unexecuted plan node.");

            CausalGameplayExecutionResult prefix;
            using (var world = new ProductionPlanWorld(plan))
            {
                CausalGameplayActionRegistry registry = CreateRegistry(world);
                ValidatePlanActionsMatchCatalog(plan, catalog, registry);
                prefix = new CausalGameplayPlanExecutor(registry, world)
                    .ExecuteSegment(
                        plan,
                        Array.Empty<CausalGameplayNodeRecord>(),
                        checkpointAfter);
            }
            Require(!prefix.IsComplete
                && !prefix.ReplanRequired
                && prefix.NextTopologicalIndex == checkpointAfter
                && prefix.Records.All(record =>
                    record.Status == CausalGameplayNodeStatus.Completed),
                "Checkpoint prefix did not stop at a clean committed boundary.");

            File.Copy(planPath, Path.Combine(staging, "input_plan.json"));
            File.Copy(catalogPath, Path.Combine(staging, "input_action_catalog.json"));
            JArray records = new(prefix.Records.Select(record =>
                RecordJson(record, relationshipAware: HasOutcomeRelations(plan))));
            string prefixSha256 = CausalGameplayCanonicalJson.Sha256(records);
            JObject checkpoint = new()
            {
                ["schemaVersion"] = 2,
                ["schema"] = CheckpointSchema,
                ["kind"] = "isolated-production-replay-checkpoint",
                ["planId"] = plan.PlanId,
                ["planRevision"] = plan.Revision,
                ["planSha256"] = plan.PlanSha256,
                ["catalogVersion"] = plan.CatalogVersion,
                ["catalogSha256"] = catalogSha256,
                ["gameCommit"] = (string)catalog["gameCommit"],
                ["runId"] = RunIdForPlan(plan),
                ["nextTopologicalIndex"] = prefix.NextTopologicalIndex,
                ["records"] = records,
                ["recordsSha256"] = prefixSha256,
                ["observedState"] = prefix.FinalState.DeepClone(),
                ["observedStateSha256"] = prefix.FinalStateSha256,
                ["resumeMode"] = "deterministic-production-replay",
                ["isolatedBatchOnly"] = true,
                ["naturalPlayClaimed"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            };
            string checkpointFile = Path.Combine(staging, "checkpoint.json");
            WriteJson(checkpointFile, checkpoint);
            JObject manifest = new()
            {
                ["schemaVersion"] = 2,
                ["schema"] = CheckpointSchema,
                ["version"] = version,
                ["status"] = "checkpoint-ready",
                ["planId"] = plan.PlanId,
                ["planRevision"] = plan.Revision,
                ["planSha256"] = plan.PlanSha256,
                ["catalogVersion"] = plan.CatalogVersion,
                ["catalogSha256"] = catalogSha256,
                ["gameCommit"] = (string)catalog["gameCommit"],
                ["nextTopologicalIndex"] = prefix.NextTopologicalIndex,
                ["checkpointSha256"] = Sha256File(checkpointFile),
                ["actualGameplayProductionEntries"] = true,
                ["naturalPlayTelemetry"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["sourceFiles"] = new JArray(SourceFiles(projectRoot))
            };
            WriteJson(Path.Combine(staging, "manifest.json"), manifest);
            WriteDelivery(
                staging,
                "input_plan.json",
                "input_action_catalog.json",
                "checkpoint.json",
                "manifest.json");
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            throw;
        }
    }

    private static string ResumeCheckpoint(
        string version,
        string checkpointDirectory)
    {
        string projectRoot = ProjectRoot();
        string canonicalCheckpointDirectory = Path.GetFullPath(checkpointDirectory);
        ValidateCheckpointDelivery(canonicalCheckpointDirectory);
        string planPath = Path.Combine(
            canonicalCheckpointDirectory,
            "input_plan.json");
        string catalogPath = Path.Combine(
            canonicalCheckpointDirectory,
            "input_action_catalog.json");
        (CausalGameplayPlan plan, JObject catalog, string catalogSha256) =
            LoadPlanAndCatalog(projectRoot, planPath, catalogPath);
        JObject checkpoint = StrictLoad(Path.Combine(
            canonicalCheckpointDirectory,
            "checkpoint.json"));
        CausalGameplayNodeRecord[] expectedPrefix = ParseCheckpoint(
            checkpoint,
            plan,
            catalog,
            catalogSha256);
        bool checkpointIncludesEvidence =
            (int)checkpoint["schemaVersion"] >= 2;

        CausalGameplayExecutionResult rebuilt;
        CausalGameplayExecutionResult execution;
        using (var world = new ProductionPlanWorld(plan))
        {
            CausalGameplayActionRegistry registry = CreateRegistry(world);
            ValidatePlanActionsMatchCatalog(plan, catalog, registry);
            var executor = new CausalGameplayPlanExecutor(registry, world);
            rebuilt = executor.ExecuteSegment(
                plan,
                Array.Empty<CausalGameplayNodeRecord>(),
                expectedPrefix.Length);
            Require(
                rebuilt.Records.Count == expectedPrefix.Length
                && !rebuilt.IsComplete
                && !rebuilt.ReplanRequired,
                "Checkpoint replay did not rebuild a clean prefix.");
            string expectedJson = CausalGameplayCanonicalJson.Sha256(
                new JArray(expectedPrefix.Select(record =>
                    RecordJson(record, checkpointIncludesEvidence, HasOutcomeRelations(plan)))));
            string rebuiltJson = CausalGameplayCanonicalJson.Sha256(
                new JArray(rebuilt.Records.Select(record =>
                    RecordJson(record, checkpointIncludesEvidence, HasOutcomeRelations(plan)))));
            Require(string.Equals(expectedJson, rebuiltJson, StringComparison.Ordinal),
                "Checkpoint replay changed prefix state or outcome identity.");
            Require(string.Equals(
                    (string)checkpoint["observedStateSha256"],
                    rebuilt.FinalStateSha256,
                    StringComparison.Ordinal),
                "Checkpoint replay changed the terminal prefix state.");
            execution = executor.ExecuteSegment(
                plan,
                rebuilt.Records,
                int.MaxValue);
        }
        Require(execution.IsComplete, "Resumed causal execution ended early.");

        string destination = PrepareCreateOnlyDestination(
            projectRoot,
            ResultParent,
            version);
        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            File.Copy(planPath, Path.Combine(staging, "input_plan.json"));
            File.Copy(catalogPath, Path.Combine(staging, "input_action_catalog.json"));
            File.Copy(
                Path.Combine(canonicalCheckpointDirectory, "checkpoint.json"),
                Path.Combine(staging, "input_checkpoint.json"));
            JObject result = ResultJson(plan, execution, catalogSha256);
            string resultPath = Path.Combine(staging, "execution_result.json");
            WriteJson(resultPath, result);

            var files = new List<string>
            {
                "input_plan.json",
                "input_action_catalog.json",
                "input_checkpoint.json",
                "execution_result.json"
            };
            string replanFile = string.Empty;
            if (execution.ReplanRequired)
            {
                replanFile = "replan_request.json";
                WriteJson(
                    Path.Combine(staging, replanFile),
                    ReplanRequest(
                        plan,
                        execution,
                        catalogSha256,
                        (string)catalog["gameCommit"]));
                files.Add(replanFile);
            }
            JObject attestation = new()
            {
                ["schemaVersion"] = 1,
                ["schema"] = ResumeAttestationSchema,
                ["planId"] = plan.PlanId,
                ["planRevision"] = plan.Revision,
                ["planSha256"] = plan.PlanSha256,
                ["catalogSha256"] = catalogSha256,
                ["checkpointSha256"] = Sha256File(Path.Combine(
                    canonicalCheckpointDirectory,
                    "checkpoint.json")),
                ["checkpointRecordsSha256"] = (string)checkpoint["recordsSha256"],
                ["rebuiltRecordsSha256"] = CausalGameplayCanonicalJson.Sha256(
                    new JArray(rebuilt.Records.Select(record =>
                        RecordJson(record, checkpointIncludesEvidence, HasOutcomeRelations(plan))))),
                ["replayedPrefixCount"] = rebuilt.Records.Count,
                ["rebuiltStateSha256"] = rebuilt.FinalStateSha256,
                ["identityPreserved"] = true,
                ["duplicateOutcomeCount"] = execution.Records
                    .Where(record => !string.IsNullOrEmpty(record.OutcomeId))
                    .GroupBy(record => record.OutcomeId, StringComparer.Ordinal)
                    .Count(group => group.Count() > 1),
                ["resumeMode"] = "deterministic-production-replay",
                ["isolatedBatchOnly"] = true,
                ["naturalPlayClaimed"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            };
            Require((int)attestation["duplicateOutcomeCount"] == 0,
                "Resumed execution contains duplicate outcome identities.");
            WriteJson(Path.Combine(staging, "resume_attestation.json"), attestation);
            files.Add("resume_attestation.json");

            JObject manifest = new()
            {
                ["schemaVersion"] = ResultVersionForPlan(plan),
                ["schema"] = ResultSchemaForPlan(plan),
                ["version"] = version,
                ["gameCommit"] = (string)catalog["gameCommit"],
                ["workingTreeDirty"] = ReadGit(
                    projectRoot,
                    "status --porcelain").Length > 0,
                ["catalogVersion"] = plan.CatalogVersion,
                ["catalogSha256"] = catalogSha256,
                ["planId"] = plan.PlanId,
                ["planRevision"] = plan.Revision,
                ["planSha256"] = plan.PlanSha256,
                ["status"] = execution.ReplanRequired
                    ? "replan-required"
                    : "completed",
                ["actualGameplayProductionEntries"] = true,
                ["naturalPlayTelemetry"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["executedCount"] = execution.Records.Count(record =>
                    !string.IsNullOrEmpty(record.OutcomeId)),
                ["invalidatedCount"] = execution.InvalidatedNodeIds.Count,
                ["resultSha256"] = Sha256File(resultPath),
                ["sourceFiles"] = new JArray(SourceFiles(projectRoot))
            };
            WriteJson(Path.Combine(staging, "manifest.json"), manifest);
            files.Add("manifest.json");
            WriteDelivery(staging, files.ToArray());
            Directory.Move(staging, destination);
            return destination;
        }
        catch
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            throw;
        }
    }

    private static string RequireExistingDirectory(string value, string label)
    {
        string path = value?.Trim() ?? string.Empty;
        if (path.Length == 0)
            throw new ArgumentException("A " + label + " path is required.");
        string absolute = Path.GetFullPath(path);
        if (!Directory.Exists(absolute))
            throw new DirectoryNotFoundException(
                "The " + label + " is missing: " + absolute);
        return absolute;
    }

    private static (
        CausalGameplayPlan plan,
        JObject catalog,
        string catalogSha256) LoadPlanAndCatalog(
        string projectRoot,
        string planPath,
        string catalogPath)
    {
        string gameCommit = ReadGit(projectRoot, "rev-parse HEAD");
        JObject catalog = StrictLoad(catalogPath);
        ValidateCatalog(catalog, gameCommit);
        string catalogSha256 = "sha256:" + Sha256File(catalogPath);
        CausalGameplayPlan plan = ParsePlan(
            StrictLoad(planPath),
            catalog,
            catalogSha256,
            gameCommit);
        return (plan, catalog, catalogSha256);
    }

    private static CausalGameplayNodeRecord[] ParseCheckpoint(
        JObject checkpoint,
        CausalGameplayPlan plan,
        JObject catalog,
        string catalogSha256)
    {
        RequireFields(
            checkpoint,
            "causal checkpoint",
            "schemaVersion", "schema", "kind", "planId", "planRevision",
            "planSha256", "catalogVersion", "catalogSha256", "gameCommit",
            "runId", "nextTopologicalIndex", "records", "recordsSha256",
            "observedState", "observedStateSha256", "resumeMode",
            "isolatedBatchOnly", "naturalPlayClaimed", "humanApprovalClaimed",
            "trainingEligible");
        int checkpointSchemaVersion = (int)checkpoint["schemaVersion"];
        bool checkpointIncludesEvidence = checkpointSchemaVersion >= 2;
        bool relationshipAware = (int)catalog["schemaVersion"] == 4;
        Require(!relationshipAware || checkpointIncludesEvidence,
            "Relation-aware checkpoint requires exact evidence.");
        Require(((checkpointSchemaVersion == 2
                    && (string)checkpoint["schema"] == CheckpointSchema)
                || (checkpointSchemaVersion == 1
                    && (string)checkpoint["schema"] == LegacyCheckpointSchema))
            && (string)checkpoint["kind"]
                == "isolated-production-replay-checkpoint",
            "Unsupported causal checkpoint schema.");
        Require((string)checkpoint["planId"] == plan.PlanId
            && (int)checkpoint["planRevision"] == plan.Revision
            && (string)checkpoint["planSha256"] == plan.PlanSha256,
            "Checkpoint plan identity mismatch.");
        Require((string)checkpoint["catalogVersion"] == plan.CatalogVersion
            && (string)checkpoint["catalogSha256"] == catalogSha256
            && (string)checkpoint["gameCommit"] == (string)catalog["gameCommit"],
            "Checkpoint catalog or game source mismatch.");
        Require((string)checkpoint["runId"] == RunIdForPlan(plan),
            "Checkpoint run identity mismatch.");
        Require((string)checkpoint["resumeMode"]
                == "deterministic-production-replay"
            && (bool)checkpoint["isolatedBatchOnly"]
            && !(bool)checkpoint["naturalPlayClaimed"]
            && !(bool)checkpoint["humanApprovalClaimed"]
            && !(bool)checkpoint["trainingEligible"],
            "Checkpoint scope or approval gates are invalid.");

        JArray recordsJson = checkpoint["records"] as JArray
            ?? throw new InvalidOperationException(
                "Checkpoint records must be an array.");
        int nextIndex = (int)checkpoint["nextTopologicalIndex"];
        Require(nextIndex > 0
            && nextIndex < plan.Nodes.Count
            && recordsJson.Count == nextIndex,
            "Checkpoint prefix length is invalid.");
        Require((string)checkpoint["recordsSha256"]
                == CausalGameplayCanonicalJson.Sha256(recordsJson),
            "Checkpoint prefix hash mismatch.");
        JObject observedState = checkpoint["observedState"] as JObject
            ?? throw new InvalidOperationException(
                "Checkpoint observedState must be an object.");
        Require((string)checkpoint["observedStateSha256"]
                == CausalGameplayCanonicalJson.Sha256(observedState),
            "Checkpoint observed-state hash mismatch.");

        Dictionary<string, CausalGameplayPlanNode> nodes = plan.Nodes.ToDictionary(
            node => node.NodeId,
            StringComparer.Ordinal);
        Dictionary<string, string> outcomeTypes = ((JArray)catalog["actions"])
            .Cast<JObject>()
            .ToDictionary(
                action => (string)action["actionId"],
                action => (string)action["outcomeTypeId"],
                StringComparer.Ordinal);
        var records = new List<CausalGameplayNodeRecord>(recordsJson.Count);
        for (int index = 0; index < recordsJson.Count; index++)
        {
            JObject value = recordsJson[index] as JObject
                ?? throw new InvalidOperationException(
                    "Checkpoint record must be an object.");
            bool checkpointHasCompanionEvidence =
                value["companionOutcomeEvidence"] != null;
            string[] checkpointRecordFields = checkpointIncludesEvidence
                ? new[]
                {
                    "nodeId", "actionId", "status", "preState",
                    "preStateSha256", "postState", "postStateSha256",
                    "outcomeId", "outcomeTypeId", "outcomeEvidence",
                    "failureCode", "failureReason", "mismatches"
                }
                : new[]
                {
                    "nodeId", "actionId", "status", "preState",
                    "preStateSha256", "postState", "postStateSha256",
                    "outcomeId", "outcomeTypeId", "failureCode",
                    "failureReason", "mismatches"
                };
            if (checkpointIncludesEvidence && checkpointHasCompanionEvidence)
            {
                checkpointRecordFields = checkpointRecordFields
                    .Concat(new[] { "companionOutcomeEvidence" })
                    .ToArray();
            }
            if (relationshipAware)
            {
                Require(checkpointHasCompanionEvidence, "Missing companion evidence in relation-aware checkpoint.");
                checkpointRecordFields = checkpointRecordFields.Concat(new[] { "companionOutcomeRelations" }).ToArray();
            }
            RequireFields(value, "checkpoint record", checkpointRecordFields);
            string nodeId = (string)value["nodeId"];
            Require(nodes.TryGetValue(nodeId, out CausalGameplayPlanNode node),
                "Checkpoint record references an unknown plan node.");
            Require(nodeId == plan.TopologicalOrder[index]
                && (string)value["actionId"] == node.ActionId
                && (string)value["status"] == "Completed",
                "Checkpoint record does not match the clean plan prefix.");
            JObject before = value["preState"] as JObject;
            JObject after = value["postState"] as JObject;
            Require(before != null
                && after != null
                && (string)value["preStateSha256"]
                    == CausalGameplayCanonicalJson.Sha256(before)
                && (string)value["postStateSha256"]
                    == CausalGameplayCanonicalJson.Sha256(after),
                "Checkpoint record state hash mismatch.");
            string outcomeId = (string)value["outcomeId"];
            string outcomeTypeId = (string)value["outcomeTypeId"];
            JObject outcomeEvidence = checkpointIncludesEvidence
                ? value["outcomeEvidence"] as JObject
                : null;
            JArray companionOutcomeEvidence = checkpointHasCompanionEvidence
                ? value["companionOutcomeEvidence"] as JArray
                    ?? throw new InvalidOperationException(
                        "Checkpoint companion outcome evidence must be an array.")
                : new JArray();
            Require(!string.IsNullOrWhiteSpace(outcomeId)
                && outcomeTypeId == outcomeTypes[node.ActionId]
                && string.IsNullOrEmpty((string)value["failureCode"])
                && string.IsNullOrEmpty((string)value["failureReason"])
                && RequireStringArray(value["mismatches"], "mismatches").Length == 0,
                "Checkpoint record contains invalid outcome or failure data.");
            records.Add(new CausalGameplayNodeRecord(
                node,
                CausalGameplayNodeStatus.Completed,
                before,
                after,
                new CausalGameplayActionResult(
                    true,
                    outcomeId,
                    outcomeTypeId,
                    string.Empty,
                    string.Empty,
                    outcomeEvidence,
                    companionOutcomeEvidence,
                    relationshipAware ? value["companionOutcomeRelations"] as JArray
                        ?? throw new InvalidOperationException("Checkpoint companion relations must be an array.") : null),
                Array.Empty<string>()));
            if (checkpointIncludesEvidence)
                ValidateOutcomeEvidence(records[^1]);
            if (relationshipAware)
                ValidateOutcomeRelations(records[^1], (JArray)((JArray)catalog["actions"]).Cast<JObject>()
                    .Single(action => (string)action["actionId"] == node.ActionId)["companionOutcomes"]);
        }
        Require(records[^1].AfterSha256
                == (string)checkpoint["observedStateSha256"],
            "Checkpoint terminal record and observed state do not match.");
        return records.ToArray();
    }

    private static void ValidateCheckpointDelivery(string directory)
    {
        JObject delivery = StrictLoad(Path.Combine(
            directory,
            "delivery_manifest.json"));
        RequireFields(delivery, "checkpoint delivery", "schemaVersion", "files");
        Require((int)delivery["schemaVersion"] == 1,
            "Unsupported checkpoint delivery schema.");
        JArray files = delivery["files"] as JArray
            ?? throw new InvalidOperationException(
                "Checkpoint delivery files must be an array.");
        string root = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JObject file in files.Cast<JObject>())
        {
            RequireFields(file, "checkpoint delivery file", "path", "sha256");
            string relative = (string)file["path"];
            Require(!string.IsNullOrWhiteSpace(relative) && seen.Add(relative),
                "Checkpoint delivery path is empty or duplicated.");
            string absolute = Path.GetFullPath(Path.Combine(directory, relative));
            Require(absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                && File.Exists(absolute)
                && (string)file["sha256"] == Sha256File(absolute),
                "Checkpoint delivery file is missing, unsafe or hash-invalid: "
                + relative);
        }
        string[] required =
        {
            "input_plan.json",
            "input_action_catalog.json",
            "checkpoint.json",
            "manifest.json"
        };
        Require(required.All(seen.Contains),
            "Checkpoint delivery is missing a required file.");
        JObject manifest = StrictLoad(Path.Combine(directory, "manifest.json"));
        RequireFields(
            manifest,
            "checkpoint manifest",
            "schemaVersion", "schema", "version", "status", "planId",
            "planRevision", "planSha256", "catalogVersion", "catalogSha256",
            "gameCommit", "nextTopologicalIndex", "checkpointSha256",
            "actualGameplayProductionEntries", "naturalPlayTelemetry",
            "humanApprovalClaimed", "trainingEligible", "sourceFiles");
        int manifestSchemaVersion = (int)manifest["schemaVersion"];
        Require(((manifestSchemaVersion == 2
                    && (string)manifest["schema"] == CheckpointSchema)
                || (manifestSchemaVersion == 1
                    && (string)manifest["schema"] == LegacyCheckpointSchema))
            && (string)manifest["status"] == "checkpoint-ready"
            && (bool)manifest["actualGameplayProductionEntries"]
            && !(bool)manifest["naturalPlayTelemetry"]
            && !(bool)manifest["humanApprovalClaimed"]
            && !(bool)manifest["trainingEligible"],
            "Checkpoint manifest schema, status or gates are invalid.");
        Require((string)manifest["checkpointSha256"] == Sha256File(Path.Combine(
                directory,
                "checkpoint.json")),
            "Checkpoint manifest hash mismatch.");
    }
}
#endif
