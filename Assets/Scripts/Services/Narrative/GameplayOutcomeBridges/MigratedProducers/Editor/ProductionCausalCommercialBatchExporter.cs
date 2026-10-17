#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class ProductionCausalPlanBatchExporter
{
    private const string CommercialBatchParent =
        "Artifacts/Exports/CausalGameplayCommercialBatch";
    private const string CommercialBatchSchema =
        "dungeonstory-causal-gameplay-commercial-batch@1";
    private const string PlanPackageSchema =
        "dungeonstory-causal-gameplay-plan-package@1";

    public static void RunCommercialBatchFromEnvironment()
    {
        string version = RequireVersion(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_COMMERCIAL_BATCH_VERSION"));
        string packagePath = RequireExistingDirectory(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_PLAN_PACKAGE_PATH"), "plan package");
        string path = ExecuteCommercialBatch(version, packagePath);
        Debug.Log("CAUSAL_GAMEPLAY_COMMERCIAL_BATCH=PASS; path=" + path);
    }

    public static void RunCommercialRejectionProbeFromEnvironment()
    {
        string version = RequireVersion(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_REJECTION_VERSION"));
        string packagePath = RequireExistingDirectory(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_PLAN_PACKAGE_PATH"), "plan package");
        string path = ExecuteCommercialRejectionProbe(version, packagePath);
        Debug.Log("CAUSAL_GAMEPLAY_REJECTION_PROBE=PASS; path=" + path);
    }

    public static string ExecuteCommercialBatch(
        string version,
        string packageDirectory)
    {
        string canonicalVersion = RequireVersion(version);
        string projectRoot = ProjectRoot();
        string packageRoot = Path.GetFullPath(packageDirectory);
        Require(Directory.Exists(packageRoot),
            "Causal plan package directory does not exist.");
        string gameCommit = ReadGit(projectRoot, "rev-parse HEAD");
        JObject inputManifest = StrictLoad(Path.Combine(
            packageRoot,
            "manifest.json"));
        HashSet<string> packageFiles = ValidatePlanPackageManifest(
            packageRoot,
            inputManifest,
            gameCommit);
        string catalogPath = Path.Combine(packageRoot, "action_catalog.json");
        JObject catalog = StrictLoad(catalogPath);
        ValidateCatalog(catalog, gameCommit);
        string catalogSha256 = "sha256:" + Sha256File(catalogPath);
        Require((string)inputManifest["catalogSha256"] == catalogSha256,
            "Plan package catalog hash mismatch.");
        JArray planRows = StrictLoadArray(Path.Combine(packageRoot, "plans.json"));
        Require(planRows.Count == (int)inputManifest["generated"]
            && planRows.Count == (int)inputManifest["requested"]
            && planRows.Count > 0,
            "Plan package count is incomplete or inconsistent.");

        string destination = PrepareCreateOnlyDestination(
            projectRoot,
            CommercialBatchParent,
            canonicalVersion);
        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(Path.Combine(staging, "input_plans"));
        Directory.CreateDirectory(Path.Combine(staging, "results"));
        try
        {
            File.Copy(catalogPath, Path.Combine(staging, "input_action_catalog.json"));
            File.Copy(
                Path.Combine(packageRoot, "manifest.json"),
                Path.Combine(staging, "input_package_manifest.json"));
            File.Copy(
                Path.Combine(packageRoot, "plans.json"),
                Path.Combine(staging, "input_plans.json"));
            var outputFiles = new List<string>
            {
                "input_action_catalog.json",
                "input_package_manifest.json",
                "input_plans.json"
            };
            var resultRows = new JArray();
            var planHashes = new HashSet<string>(StringComparer.Ordinal);
            var actionSequences = new HashSet<string>(StringComparer.Ordinal);
            var graphSignatures = new HashSet<string>(StringComparer.Ordinal);
            var outcomeIds = new HashSet<string>(StringComparer.Ordinal);
            var templateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var actionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int evidenceCount = 0;
            int subjectBoundEvidenceCount = 0;

            for (int index = 0; index < planRows.Count; index++)
            {
                JObject row = planRows[index] as JObject
                    ?? throw new InvalidOperationException(
                        "Plan package row must be an object.");
                RequireFields(
                    row,
                    "plan package row",
                    "path", "planId", "planSha256");
                string relativePath = (string)row["path"];
                Require(packageFiles.Contains(relativePath),
                    "Plan row path is absent from the package hash manifest: "
                    + relativePath);
                string planPath = SafePackagePath(packageRoot, relativePath);
                JObject planJson = StrictLoad(planPath);
                CausalGameplayPlan plan = ParsePlan(
                    planJson,
                    catalog,
                    catalogSha256,
                    gameCommit);
                Require((string)row["planId"] == plan.PlanId
                    && (string)row["planSha256"] == plan.PlanSha256
                    && planHashes.Add(plan.PlanSha256),
                    "Plan package row identity is mismatched or duplicated.");

                CausalGameplayExecutionResult execution;
                using (var world = new ProductionPlanWorld(plan))
                {
                    CausalGameplayActionRegistry registry = CreateRegistry(world);
                    ValidatePlanActionsMatchCatalog(plan, catalog, registry);
                    execution = new CausalGameplayPlanExecutor(registry, world)
                        .Execute(plan);
                }
                Require(execution.IsComplete && !execution.ReplanRequired,
                    "Commercial batch plan did not complete cleanly: "
                    + plan.PlanId);
                foreach (CausalGameplayNodeRecord record in execution.Records)
                {
                    Require(record.Status == CausalGameplayNodeStatus.Completed
                        && !string.IsNullOrEmpty(record.OutcomeId),
                        "Commercial batch record is incomplete or reused an outcome: "
                        + record.NodeId);
                    ValidateOutcomeEvidence(record);
                    CountCommercialOutcomeEvidence(
                        record.OutcomeEvidence,
                        outcomeIds,
                        ref evidenceCount,
                        ref subjectBoundEvidenceCount,
                        record.NodeId);
                    int companionIndex = 0;
                    foreach (JObject companion in record.CompanionOutcomeEvidence
                                 .OfType<JObject>())
                    {
                        CountCommercialOutcomeEvidence(
                            companion,
                            outcomeIds,
                            ref evidenceCount,
                            ref subjectBoundEvidenceCount,
                            record.NodeId + " companion " + companionIndex);
                        companionIndex++;
                    }
                    actionCounts[record.ActionId] =
                        actionCounts.TryGetValue(record.ActionId, out int count)
                            ? count + 1
                            : 1;
                }

                string actionSequence = string.Join(
                    ">",
                    plan.TopologicalOrder.Select(nodeId => plan.Nodes.Single(
                        node => node.NodeId == nodeId).ActionId));
                actionSequences.Add(actionSequence);
                string graphSignature = string.Join(
                    "|",
                    plan.Nodes.OrderBy(node => node.NodeId, StringComparer.Ordinal)
                        .Select(node => node.ActionId + "<-" + string.Join(
                            ",",
                            node.CausalLinks.OrderBy(
                                    link => link.ParentNodeId,
                                    StringComparer.Ordinal)
                                .Select(link => link.Kind + ":" + string.Join(
                                    ",",
                                    link.StateKeys)))));
                graphSignatures.Add(graphSignature);
                templateCounts[plan.TemplateId] =
                    templateCounts.TryGetValue(plan.TemplateId, out int templateCount)
                        ? templateCount + 1
                        : 1;

                string inputPlanRelative = "input_plans/plan-"
                    + (index + 1).ToString("D5") + ".json";
                string resultRelative = "results/result-"
                    + (index + 1).ToString("D5") + ".json";
                File.Copy(planPath, Path.Combine(staging, inputPlanRelative));
                WriteJson(
                    Path.Combine(staging, resultRelative),
                    ResultJson(plan, execution, catalogSha256));
                outputFiles.Add(inputPlanRelative);
                outputFiles.Add(resultRelative);
                resultRows.Add(new JObject
                {
                    ["planId"] = plan.PlanId,
                    ["planSha256"] = plan.PlanSha256,
                    ["templateId"] = plan.TemplateId,
                    ["seed"] = plan.Seed,
                    ["resultPath"] = resultRelative,
                    ["resultSha256"] = Sha256File(Path.Combine(
                        staging,
                        resultRelative)),
                    ["executedCount"] = execution.Records.Count
                });
            }

            Require(templateCounts.Count >= 3,
                "Commercial batch did not cover every scenario family.");
            Require(actionSequences.Count >= 4,
                "Commercial batch action-sequence diversity is too low.");
            Require(graphSignatures.Count >= 3,
                "Commercial batch graph diversity is too low.");
            Require(subjectBoundEvidenceCount == evidenceCount,
                "A durable gameplay outcome lacks participant/subject binding.");

            JObject batchResult = new()
            {
                ["schemaVersion"] = 1,
                ["schema"] = CommercialBatchSchema,
                ["status"] = "COMPLETE",
                ["catalogVersion"] = (string)catalog["catalogVersion"],
                ["catalogSha256"] = catalogSha256,
                ["gameCommit"] = gameCommit,
                ["requested"] = planRows.Count,
                ["executed"] = resultRows.Count,
                ["uniquePlanCount"] = planHashes.Count,
                ["uniqueActionSequenceCount"] = actionSequences.Count,
                ["uniqueGraphSignatureCount"] = graphSignatures.Count,
                ["exactOutcomeCount"] = outcomeIds.Count,
                ["outcomeEvidenceCount"] = evidenceCount,
                ["subjectBoundEvidenceCount"] = subjectBoundEvidenceCount,
                ["templateCounts"] = SortedCountObject(templateCounts),
                ["actionCounts"] = SortedCountObject(actionCounts),
                ["results"] = resultRows,
                ["actualGameplayProductionEntries"] = true,
                ["naturalPlayTelemetry"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            };
            string batchResultPath = Path.Combine(staging, "batch_result.json");
            WriteJson(batchResultPath, batchResult);
            outputFiles.Add("batch_result.json");
            JObject manifest = new()
            {
                ["schemaVersion"] = 1,
                ["schema"] = CommercialBatchSchema,
                ["version"] = canonicalVersion,
                ["status"] = "COMPLETE",
                ["gameCommit"] = gameCommit,
                ["workingTreeDirty"] = ReadGit(
                    projectRoot,
                    "status --porcelain").Length > 0,
                ["catalogVersion"] = (string)catalog["catalogVersion"],
                ["catalogSha256"] = catalogSha256,
                ["inputPackageManifestSha256"] = Sha256File(Path.Combine(
                    packageRoot,
                    "manifest.json")),
                ["batchResultSha256"] = Sha256File(batchResultPath),
                ["actualGameplayProductionEntries"] = true,
                ["naturalPlayTelemetry"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["sourceFiles"] = new JArray(SourceFiles(projectRoot))
            };
            WriteJson(Path.Combine(staging, "manifest.json"), manifest);
            outputFiles.Add("manifest.json");
            WriteDelivery(staging, outputFiles.ToArray());
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

    public static string ExecuteCommercialRejectionProbe(
        string version,
        string packageDirectory)
    {
        const string RejectedActionId = "action:trigger-defense-facility";
        string canonicalVersion = RequireVersion(version);
        string projectRoot = ProjectRoot();
        string packageRoot = Path.GetFullPath(packageDirectory);
        string gameCommit = ReadGit(projectRoot, "rev-parse HEAD");
        JObject packageManifest = StrictLoad(Path.Combine(
            packageRoot,
            "manifest.json"));
        HashSet<string> packageFiles = ValidatePlanPackageManifest(
            packageRoot,
            packageManifest,
            gameCommit);
        string catalogPath = Path.Combine(packageRoot, "action_catalog.json");
        JObject catalog = StrictLoad(catalogPath);
        ValidateCatalog(catalog, gameCommit);
        string catalogSha256 = "sha256:" + Sha256File(catalogPath);
        JArray rows = StrictLoadArray(Path.Combine(packageRoot, "plans.json"));
        CausalGameplayPlan selectedPlan = null;
        string selectedPlanPath = null;
        foreach (JObject row in rows.Cast<JObject>())
        {
            RequireFields(row, "plan package row", "path", "planId", "planSha256");
            string relative = (string)row["path"];
            Require(packageFiles.Contains(relative),
                "Rejection probe plan is absent from the package manifest.");
            string candidatePath = SafePackagePath(packageRoot, relative);
            CausalGameplayPlan candidate = ParsePlan(
                StrictLoad(candidatePath),
                catalog,
                catalogSha256,
                gameCommit);
            if (candidate.Nodes.Any(node => node.ActionId == RejectedActionId))
            {
                selectedPlan = candidate;
                selectedPlanPath = candidatePath;
                break;
            }
        }
        Require(selectedPlan != null,
            "No package plan exercises the controlled rejection action.");

        CausalGameplayExecutionResult execution;
        using (var world = new ProductionPlanWorld(
                   selectedPlan,
                   RejectedActionId))
        {
            CausalGameplayActionRegistry registry = CreateRegistry(world);
            ValidatePlanActionsMatchCatalog(selectedPlan, catalog, registry);
            execution = new CausalGameplayPlanExecutor(registry, world)
                .Execute(selectedPlan);
        }
        CausalGameplayNodeRecord rejected = execution.Records.Single(record =>
            record.ActionId == RejectedActionId);
        Require(execution.IsComplete
            && execution.ReplanRequired
            && rejected.Status == CausalGameplayNodeStatus.Rejected
            && rejected.FailureCode == "production-entry-rejected"
            && string.IsNullOrEmpty(rejected.OutcomeId)
            && rejected.BeforeSha256 == rejected.AfterSha256
            && execution.InvalidatedNodeIds.Count > 0,
            "Controlled production rejection did not roll back and invalidate descendants.");

        string destination = PrepareCreateOnlyDestination(
            projectRoot,
            ResultParent,
            canonicalVersion);
        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            File.Copy(selectedPlanPath, Path.Combine(staging, "input_plan.json"));
            File.Copy(catalogPath, Path.Combine(staging, "input_action_catalog.json"));
            JObject result = ResultJson(selectedPlan, execution, catalogSha256);
            string resultPath = Path.Combine(staging, "execution_result.json");
            WriteJson(resultPath, result);
            WriteJson(
                Path.Combine(staging, "replan_request.json"),
                ReplanRequest(
                    selectedPlan,
                    execution,
                    catalogSha256,
                    gameCommit));
            JObject manifest = new()
            {
                ["schemaVersion"] = ResultVersionForPlan(selectedPlan),
                ["schema"] = ResultSchemaForPlan(selectedPlan),
                ["version"] = canonicalVersion,
                ["gameCommit"] = gameCommit,
                ["workingTreeDirty"] = ReadGit(
                    projectRoot,
                    "status --porcelain").Length > 0,
                ["catalogVersion"] = selectedPlan.CatalogVersion,
                ["catalogSha256"] = catalogSha256,
                ["planId"] = selectedPlan.PlanId,
                ["planRevision"] = selectedPlan.Revision,
                ["planSha256"] = selectedPlan.PlanSha256,
                ["status"] = "replan-required",
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
            WriteDelivery(
                staging,
                "input_plan.json",
                "input_action_catalog.json",
                "execution_result.json",
                "replan_request.json",
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

    private static HashSet<string> ValidatePlanPackageManifest(
        string packageRoot,
        JObject manifest,
        string gameCommit)
    {
        RequireFields(
            manifest,
            "plan package manifest",
            "schemaVersion", "schema", "status", "catalogVersion",
            "catalogSha256", "gameCommit", "requested", "generated", "seed",
            "naturalPlayClaimed", "humanApprovalClaimed", "trainingEligible",
            "releaseGate", "files");
        Require((int)manifest["schemaVersion"] == 1
            && (string)manifest["schema"] == PlanPackageSchema
            && (string)manifest["status"] == "COMPLETE"
            && (string)manifest["gameCommit"] == gameCommit,
            "Unsupported, incomplete or stale causal plan package.");
        Require(!(bool)manifest["naturalPlayClaimed"]
            && !(bool)manifest["humanApprovalClaimed"]
            && !(bool)manifest["trainingEligible"]
            && (string)manifest["releaseGate"] == "REJECT_BEFORE_DPO",
            "Plan package scope or approval gates are invalid.");
        JArray files = manifest["files"] as JArray
            ?? throw new InvalidOperationException(
                "Plan package files must be an array.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JObject file in files.Cast<JObject>())
        {
            RequireFields(file, "plan package file", "path", "sha256");
            string relative = (string)file["path"];
            string absolute = SafePackagePath(packageRoot, relative);
            Require(seen.Add(relative)
                && File.Exists(absolute)
                && (string)file["sha256"] == "sha256:" + Sha256File(absolute),
                "Plan package file is duplicated, missing or hash-invalid: "
                + relative);
        }
        Require(seen.Contains("action_catalog.json")
            && seen.Contains("plans.json"),
            "Plan package is missing its catalog or plan index.");
        return seen;
    }

    private static string SafePackagePath(string root, string relative)
    {
        Require(!string.IsNullOrWhiteSpace(relative)
            && !Path.IsPathRooted(relative),
            "Package path must be relative.");
        string rootPrefix = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string absolute = Path.GetFullPath(Path.Combine(
            rootPrefix,
            relative.Replace('/', Path.DirectorySeparatorChar)));
        Require(absolute.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase),
            "Package path escapes its root: " + relative);
        return absolute;
    }

    private static JArray StrictLoadArray(string path)
    {
        using var reader = new JsonTextReader(File.OpenText(path))
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double
        };
        JArray result = JArray.Load(reader, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Load
        });
        if (reader.Read())
            throw new InvalidOperationException(
                "Trailing JSON content is not allowed: " + path);
        return result;
    }

    private static JObject SortedCountObject(
        IReadOnlyDictionary<string, int> counts)
    {
        var result = new JObject();
        foreach (KeyValuePair<string, int> pair in counts.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            result[pair.Key] = pair.Value;
        }
        return result;
    }

    private static void CountCommercialOutcomeEvidence(
        JObject evidence,
        ISet<string> outcomeIds,
        ref int evidenceCount,
        ref int subjectBoundEvidenceCount,
        string label)
    {
        string outcomeId = (string)evidence?["outcomeId"];
        JObject snapshot = evidence?["snapshot"] as JObject;
        Require(!string.IsNullOrEmpty(outcomeId)
            && outcomeIds.Add(outcomeId)
            && snapshot != null,
            "Commercial batch outcome evidence is missing, duplicated or invalid: "
            + label);
        evidenceCount++;
        int participants = (snapshot["participants"] as JArray)?.Count ?? 0;
        int subjects = (snapshot["subjects"] as JArray)?.Count ?? 0;
        if (participants + subjects > 0)
            subjectBoundEvidenceCount++;
    }
}
#endif
