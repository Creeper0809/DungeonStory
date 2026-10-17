using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class WorldResourceTransactionFaultDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-world-resource-transaction-fault-matrix.txt";

    [MenuItem("Tools/DungeonStory/Economy/Verify World Resource Transaction Fault Matrix")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        WriteIfChanged(ReportPath, report);
        Debug.Log(report);
    }

    public static void RunDomainFactFocused()
    {
        VerifyHiddenDurableAcknowledgementRecovery();
    }

    public static void RunE10FromBatch()
    {
        // The owned launcher proves OS station/desktop isolation. Its edit mode
        // intentionally has no player-save root; do not confuse that with OS isolation.
        if (!Application.isBatchMode || Application.isPlaying)
            throw new InvalidOperationException("E10 requires the explicit edit-mode batch entry.");
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_E10_WORLD_RESOURCE_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_E10_WORLD_RESOURCE_REPORT_PATH is required.");
        }
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string rawPath = fullPath + ".raw.json";
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath) || File.Exists(rawPath))
        {
            throw new InvalidOperationException(
                "E10 world-resource report must be a create-only path under Artifacts/QA.");
        }

        JArray observations = new();
        JObject raw = new()
        {
            ["schema"] = "dungeonstory-e10-world-resource-current-raw@1",
            ["passed"] = false,
            ["originalFaultAssertionsPassed"] = false,
            ["admissionAssertionsPassed"] = false,
            ["authority"] = "WorldResourceRuntime -> real exact-source publication/repository -> real migrated outcome owner/ledger",
            ["scope"] = "Controlled topology, node host, research, clock and contributors; new resource owners preserve physical items and ledger. No Brain, natural, whole-world, cold or training claim.",
            ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false,
            ["fullAuditClosure"] = false,
            ["observations"] = observations
        };
        string report = string.Empty;
        try
        {
            FacilityBufferMassAdmissionDebugScenarios.RunAll();
            raw["admissionAssertionsPassed"] = true;
            report = RunAll().Replace("\nRESULT=PASS\n", "\nORIGINAL_FAULT_RESULT=PASS\n");
            raw["originalFaultAssertionsPassed"] = true;
            VerifyRealPhysicalBindingMatrix(observations);
            Require(observations.Count == 22, "E10 real owner observations are incomplete.");
            raw["passed"] = true;
            report += "realPhysicalBindingMatrixExact=true\n"
                + "renewableTerminalRestoreTamperExact=true\n"
                + "runtimeEntry=WorldResourceRuntime.ApplyWork,TryFinalizePendingOutput,Tick\n"
                + "branches=renewable-finite,gather-logging-quarry,partial-work,output-retry,regeneration\n"
                + "fullAuditClosure=false\nhumanApprovalClaimed=false\ntrainingEligible=false\nRESULT=PASS\n";
            WriteCreateNew(fullPath, report);
            Debug.Log("U0_E10_WORLD_RESOURCE=PASS; report=" + fullPath);
        }
        catch (Exception exception)
        {
            raw["passed"] = false;
            raw["failure"] = exception.ToString();
            WriteCreateNew(fullPath, report + "RESULT=FAIL\nU0_E10_RESULT=FAIL\n" + exception + "\n");
            throw;
        }
        finally
        {
            WriteCreateNew(rawPath, raw.ToString(Newtonsoft.Json.Formatting.Indented) + "\n");
        }
    }

    private static void WriteCreateNew(string path, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using StreamWriter writer = new(stream, new UTF8Encoding(false));
        writer.Write(contents.Replace("\r\n", "\n"));
    }

    private static void VerifyRealPhysicalBindingMatrix(JArray observations)
    {
        RealPhysicalFixture fixture = new();
        FakeNodeHost nodes = new();
        WorldResourceRuntime runtime = fixture.CreateRuntime(nodes);
        WorkTypeId[] workTypes =
        {
            BuiltInWorkTypeIds.Logging, BuiltInWorkTypeIds.Quarry, BuiltInWorkTypeIds.Gather
        };
        Dictionary<WorkTypeId, float> requiredWork = new();
        try
        {
            runtime.Tick();
            Observe("real.initial");
            foreach (WorkTypeId workType in workTypes)
            {
                WorldResourceNode node = Node(workType);
                Require(runtime.TryGetWork(node, workType, out WorldResourceWorkSnapshot work)
                        && work.Available, "Real source work is unavailable: " + workType.Value);
                requiredWork.Add(workType, work.RequiredWork);
                Require(runtime.ApplyWork(node, workType, work.RequiredWork * 0.35f,
                        "character:e10:" + workType.Value + ":a", out bool completed) && !completed,
                    "Real partial work completed the cycle: " + workType.Value);
                Require(fixture.Items.GetAllStacks().Count == 0
                        && fixture.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0,
                    "Partial labor produced an item or completion outcome.");
                Observe("real.partial." + workType.Value);
            }

            ReplaceResourceOwner("real.partial.restored");
            foreach (WorkTypeId workType in workTypes)
            {
                Require(!runtime.ApplyWork(Node(workType), workType, requiredWork[workType] * 0.65f,
                        "character:e10:" + workType.Value + ":b", out bool completed) && !completed,
                    "Injected physical prepare rejection completed the cycle: " + workType.Value);
                WorldResourcePendingOutputSaveData pending = Source(workType).pendingOutput;
                Require(!pending.IsEmpty && !pending.domainApplied
                        && pending.contributions.Count == 2
                        && fixture.Items.GetAllStacks().Count == 0
                        && fixture.Claims.CaptureClaims().Count == 0
                        && fixture.Admission.CaptureProfiles().Count == 0,
                    "Physical prepare rejection leaked output or exact destination authority.");
                Observe("real.output-wait." + workType.Value);
            }
            Require(fixture.Environment.CurrentResource == 8f
                    && runtime.Capture().nodes.SelectMany(value => value.sources)
                        .Where(value => value.workTypeId != BuiltInWorkTypeIds.Gather.Value)
                        .All(value => value.remainingCycles == 1),
                "Rejected output prepare consumed the renewable or finite source.");

            fixture.OutputFault.Enabled = false;
            ReplaceResourceOwner("real.output-wait.restored");
            foreach (WorkTypeId workType in workTypes)
            {
                Require(!runtime.TryFinalizePendingOutput(Node(workType), workType, out bool completed)
                        && !completed, "Rejected real parent completed the source cycle.");
                Observe("real.parent-wait." + workType.Value);
                WorldResourcePendingOutputSaveData pending = Source(workType).pendingOutput;
                Require(pending.domainApplied
                        && pending.parentPublicationKey.Length == 0
                        && pending.parentResultKey.Length == 0
                        && pending.outputBatchCommitId.Length > 0
                        && pending.publishedStacks.Count > 0
                        && pending.publishedStacks.Sum(value => value.massGrams)
                            == pending.physicalOutputMassGrams,
                    "Real physical output did not retain its exact receipt: prepare="
                        + fixture.Output.LastPrepareFailure + "; commit=" + fixture.Output.LastCommitFailure);
                foreach (WorldResourcePublishedStackSaveData published in pending.publishedStacks)
                {
                    WorldItemStackSnapshot actual = fixture.Items.GetAllStacks()
                        .Single(value => value.StackId == published.stackId);
                    Require(actual.ItemId == published.itemId && actual.Quantity == published.quantity
                            && actual.ItemInstanceId == published.itemInstanceId
                            && actual.State == WorldItemStackState.Loose
                            && actual.Position == NodePosition(workType)
                            && checked(fixture.Output.GetDefinitionUnitMassGrams(actual.ItemId)
                                * actual.Quantity) == published.massGrams,
                        "Actual released stack does not match its resource receipt: " + published.stackId);
                }
                Require(fixture.Outcomes.Transaction.Capture().owners.Count == 0
                        && fixture.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0,
                    "Rejected parent wrote a completion outcome.");
            }
            DungeonWorldResourceSaveData applied = runtime.Capture();
            Require(fixture.Environment.CurrentResource == 6.5f
                    && Source(BuiltInWorkTypeIds.Gather).pendingOutput.sourceBefore == 8f
                    && Source(BuiltInWorkTypeIds.Gather).pendingOutput.sourceAfter == 6.5f
                    && applied.nodes.SelectMany(value => value.sources)
                        .Where(value => value.workTypeId != BuiltInWorkTypeIds.Gather.Value)
                        .All(value => value.pendingOutput.sourceBefore == 1f
                            && value.pendingOutput.sourceAfter == 0f && value.remainingCycles == 0)
                    && fixture.Items.GetAllStacks().Count == applied.nodes.SelectMany(value => value.sources)
                        .Sum(value => value.pendingOutput.publishedStacks.Count),
                "Real finite/renewable debit or exact output census drifted.");
            string physicalAfterApplied = JsonUtility.ToJson(fixture.Items.Capture());
            fixture.Environment.Regenerate(1f); // Explicit fixture input, never a work/completion event.
            runtime.Tick();
            Require(fixture.Environment.CurrentResource == 7.5f
                    && Source(BuiltInWorkTypeIds.Gather).pendingOutput.sourceBefore == 8f
                    && Source(BuiltInWorkTypeIds.Gather).pendingOutput.sourceAfter == 6.5f
                    && physicalAfterApplied == JsonUtility.ToJson(fixture.Items.Capture())
                    && fixture.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0,
                "Regeneration changed frozen work/output evidence or emitted completion.");
            Observe("real.regeneration");

            VerifyTerminalRestoreRejection();
            Observe("real.restore-tamper-rejected");
            ReplaceResourceOwner("real.parent-wait.restored");
            fixture.ParentProbe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            Require(!runtime.TryFinalizePendingOutput(Node(workTypes[0]), workTypes[0], out bool rejected)
                    && !rejected && physicalAfterApplied == JsonUtility.ToJson(fixture.Items.Capture())
                    && fixture.Outcomes.Transaction.Capture().owners.Count == 0
                    && fixture.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0,
                "Parent commit rejection repeated output or left a durable completion owner.");
            WorldResourcePendingOutputSaveData commitRejected =
                Source(workTypes[0]).pendingOutput;
            Require(!string.IsNullOrEmpty(commitRejected.parentPublicationKey)
                    && string.IsNullOrEmpty(commitRejected.parentResultKey),
                "Parent commit rejection did not preserve its opaque publication key.");
            Observe("real.parent-commit-rejected");
            fixture.ParentProbe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            foreach (WorkTypeId workType in workTypes)
            {
                Require(runtime.TryFinalizePendingOutput(Node(workType), workType, out bool completed)
                        && completed, "Real parent-only retry failed: " + workType.Value);
                Require(Source(workType).completedCycleSequence == 1
                        && Source(workType).pendingOutput.IsEmpty
                        && Source(workType).workContributions.Count == 0
                        && physicalAfterApplied == JsonUtility.ToJson(fixture.Items.Capture()),
                    "Parent-only retry repeated physical effects or lost source retirement.");
                Observe("real.completed." + workType.Value);
            }
            Require(fixture.Outcomes.Transaction.Capture().owners.Count == 3
                    && fixture.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 3,
                "Real resource completion did not create exactly three canonical parents.");
            string completedLedger = JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes());
            string completedOwners = JsonUtility.ToJson(fixture.Outcomes.Transaction.Capture());
            ReplayCompleted("real.replay");
            ReplaceResourceOwner("real.completed.restored");
            ReplayCompleted("real.completed-replay");

            void ReplayCompleted(string stage)
            {
                string before = JsonUtility.ToJson(runtime.Capture());
                foreach (WorkTypeId workType in workTypes)
                    Require(!runtime.TryFinalizePendingOutput(Node(workType), workType, out bool completed)
                            && !completed, "Completed source accepted the same pending cycle again.");
                runtime.Tick();
                Require(before == JsonUtility.ToJson(runtime.Capture())
                        && completedLedger == JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes())
                        && completedOwners == JsonUtility.ToJson(fixture.Outcomes.Transaction.Capture())
                        && physicalAfterApplied == JsonUtility.ToJson(fixture.Items.Capture())
                        && fixture.Environment.CurrentResource == 7.5f,
                    "Completed replay changed source, output, parent identity or regeneration.");
                Observe(stage);
            }
        }
        finally
        {
            runtime.Dispose();
            nodes.Dispose();
            fixture.Dispose();
        }

        WorldResourceNode Node(WorkTypeId type) => runtime.Nodes.Single(value =>
            runtime.TryGetWork(value, type, out _));
        WorldResourceSourceSaveData Source(WorkTypeId type) => runtime.Capture().nodes
            .SelectMany(value => value.sources).Single(value => value.workTypeId == type.Value);
        Vector2Int NodePosition(WorkTypeId type)
        {
            string nodeId = Node(type).NodeId;
            WorldResourceNodeSaveData node = runtime.Capture().nodes.Single(value =>
                value.buildingInstanceId == nodeId);
            return new Vector2Int(node.gridX, node.gridY);
        }
        void Observe(string stage)
        {
            JObject record = new()
            {
                ["stage"] = stage,
                ["patchResource"] = fixture.Environment.CurrentResource,
                ["outputFaultEnabled"] = fixture.OutputFault.Enabled,
                ["parentFaultMode"] = fixture.ParentProbe.FaultMode.ToString(),
                ["outputPrepareAttempts"] = fixture.Output.PrepareAttempts,
                ["outputCommitAttempts"] = fixture.Output.CommitAttempts,
                ["outputPrepareFailure"] = fixture.Output.LastPrepareFailure,
                ["outputCommitFailure"] = fixture.Output.LastCommitFailure,
                ["outputCommitStatus"] = fixture.Output.LastCommitStatus
            };
            observations.Add(record); // Preserve diagnostics even if a transient owner rejects Capture.
            try
            {
                record["worldResources"] = JObject.Parse(JsonUtility.ToJson(runtime.Capture()));
                record["physicalItems"] = JObject.Parse(JsonUtility.ToJson(fixture.Items.Capture()));
                record["ledger"] = JObject.Parse(JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes()));
                record["parentOwners"] = JObject.Parse(JsonUtility.ToJson(fixture.Outcomes.Transaction.Capture()));
            }
            catch (Exception exception)
            {
                record["captureFailure"] = exception.ToString();
                throw;
            }
        }
        void ReplaceResourceOwner(string stage)
        {
            string canonical = JsonUtility.ToJson(runtime.Capture());
            string items = JsonUtility.ToJson(fixture.Items.Capture());
            string ledger = JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes());
            runtime.Dispose();
            nodes.Dispose();
            nodes = new FakeNodeHost();
            runtime = fixture.CreateRuntime(nodes);
            runtime.Restore(runtime.BuildRestore(JsonUtility.FromJson<DungeonWorldResourceSaveData>(canonical)));
            runtime.Tick();
            Require(canonical == JsonUtility.ToJson(runtime.Capture())
                    && items == JsonUtility.ToJson(fixture.Items.Capture())
                    && ledger == JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes()),
                "Scoped JSON restore changed resource evidence, preserved physical items or ledger.");
            Observe(stage);
        }
        void VerifyTerminalRestoreRejection()
        {
            string canonical = JsonUtility.ToJson(runtime.Capture());
            string items = JsonUtility.ToJson(fixture.Items.Capture());
            string ledger = JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes());
            string owners = JsonUtility.ToJson(fixture.Outcomes.Transaction.Capture());
            foreach (WorkTypeId workType in workTypes)
            {
                DungeonWorldResourceSaveData wrongKind = Copy();
                WorldResourcePendingOutputSaveData pending = SavedSource(wrongKind, workType).pendingOutput;
                pending.renewableSource = !pending.renewableSource;
                pending.sourcePatchId = pending.renewableSource ? MatrixEnvironment.PatchId : string.Empty;
                RequireThrows(() => runtime.BuildRestore(wrongKind),
                    "Terminal restore admitted a different registered source kind: " + workType.Value);
            }
            foreach (string invalidPatch in new[]
                     { string.Empty, "item:qa:wrong-kind", " " + MatrixEnvironment.PatchId + " " })
            {
                DungeonWorldResourceSaveData invalid = Copy();
                SavedSource(invalid, BuiltInWorkTypeIds.Gather).pendingOutput.sourcePatchId = invalidPatch;
                RequireThrows(() => runtime.BuildRestore(invalid),
                    "Terminal restore admitted an empty, wrong-kind or noncanonical patch ID.");
            }
            DungeonWorldResourceSaveData wrongPatch = Copy();
            SavedSource(wrongPatch, BuiltInWorkTypeIds.Gather).pendingOutput.sourcePatchId =
                "wildlife-habitat:qa:other-patch";
            FakeNodeHost rejectedNodes = new();
            WorldResourceRuntime rejectedRuntime = fixture.CreateRuntime(rejectedNodes);
            try
            {
                rejectedRuntime.Restore(rejectedRuntime.BuildRestore(wrongPatch));
                string candidate = JsonUtility.ToJson(rejectedRuntime.Capture());
                RequireThrows(() => rejectedRuntime.Tick(),
                    "Exact topology rebind admitted a different renewable debit patch.");
                Require(candidate == JsonUtility.ToJson(rejectedRuntime.Capture())
                        && rejectedRuntime.NodeCount == 0,
                    "Rejected patch rebind changed its candidate or published node hosts.");
            }
            finally
            {
                rejectedRuntime.Dispose();
                rejectedNodes.Dispose();
            }
            Require(canonical == JsonUtility.ToJson(runtime.Capture())
                    && items == JsonUtility.ToJson(fixture.Items.Capture())
                    && ledger == JsonUtility.ToJson(fixture.Outcomes.Ledger.CaptureGameplayOutcomes())
                    && owners == JsonUtility.ToJson(fixture.Outcomes.Transaction.Capture())
                    && fixture.Environment.CurrentResource == 7.5f,
                "Rejected terminal restore changed the live resource, output or parent owners.");

            DungeonWorldResourceSaveData Copy() =>
                JsonUtility.FromJson<DungeonWorldResourceSaveData>(canonical);
            WorldResourceSourceSaveData SavedSource(DungeonWorldResourceSaveData value, WorkTypeId workType) =>
                value.nodes.SelectMany(node => node.sources).Single(source => source.workTypeId == workType.Value);
        }
    }

    private static void VerifyHiddenDurableAcknowledgementRecovery()
    {
        RealPhysicalFixture fixture = new();
        fixture.OutputFault.Enabled = false;
        fixture.ParentProbe.FaultMode = MigratedProducerOutcomeFaultMode.None;
        fixture.DomainFacts.HideNextDeliveredAcknowledgement = true;

        string savedJson;
        string savedPublicationKey;
        string physicalAfterCommit;
        int prepareAttemptsAfterCommit;
        int commitAttemptsAfterCommit;
        FakeNodeHost firstNodes = new();
        WorldResourceRuntime first = null;
        bool firstPhaseCompleted = false;
        try
        {
            first = fixture.CreateRuntime(firstNodes);
            first.Tick();
            WorldResourceNode node = first.Nodes.Single(candidate =>
                first.TryGetWork(candidate, BuiltInWorkTypeIds.Logging, out _));
            Require(first.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot work)
                && work.Available,
                "Hidden-ack fixture has no logging work.");
            Require(!first.ApplyWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    work.RequiredWork,
                    "character:e10:hidden-ack",
                    out bool completed)
                && !completed,
                "A hidden durable acknowledgement incorrectly completed the cycle.");

            DungeonWorldResourceSaveData saved = first.Capture();
            WorldResourceSourceSaveData source = saved.nodes
                .SelectMany(value => value.sources)
                .Single(value => value.workTypeId == BuiltInWorkTypeIds.Logging.Value);
            WorldResourcePendingOutputSaveData pending = source.pendingOutput;
            savedPublicationKey = pending.parentPublicationKey;
            Require(pending.domainApplied
                    && !string.IsNullOrEmpty(savedPublicationKey)
                    && string.IsNullOrEmpty(pending.parentResultKey)
                    && source.remainingCycles == 0
                    && fixture.DomainFacts.PublishCount == 1
                    && fixture.DomainFacts.ReplayBatchCount == 0
                    && fixture.Output.PrepareAttempts == 1
                    && fixture.Output.CommitAttempts == 1
                    && first.LastFactFailure
                        == "world-resource-fact-delivery-unconfirmed:qa:hidden-durable-ack"
                    && fixture.Outcomes.Transaction.Capture().owners.Count == 1
                    && fixture.Outcomes.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == 1
                    && fixture.ParentProbe.LatestCommittedKey(
                        MigratedProducerOutcomeKind.WorldResourceCycleResult)
                        .ToString() == savedPublicationKey,
                "Hidden durable acknowledgement did not retain one exact pending parent.");

            VerifyStrictPublicationIdentityShape(first);
            savedJson = JsonUtility.ToJson(saved);
            physicalAfterCommit = JsonUtility.ToJson(fixture.Items.Capture());
            prepareAttemptsAfterCommit = fixture.Output.PrepareAttempts;
            commitAttemptsAfterCommit = fixture.Output.CommitAttempts;
            firstPhaseCompleted = true;
        }
        finally
        {
            first?.Dispose();
            firstNodes.Dispose();
            if (!firstPhaseCompleted)
                fixture.Dispose();
        }

        FakeNodeHost restoredNodes = new();
        WorldResourceRuntime restored = null;
        try
        {
            restored = fixture.CreateRuntime(restoredNodes);
            DungeonWorldResourceSaveData roundTrip =
                JsonUtility.FromJson<DungeonWorldResourceSaveData>(savedJson);
            restored.Restore(restored.BuildRestore(roundTrip));
            restored.Tick();
            WorldResourceNode node = restored.Nodes.Single(candidate =>
                restored.TryGetWork(candidate, BuiltInWorkTypeIds.Logging, out _));
            WorldResourcePendingOutputSaveData restoredPending = restored.Capture()
                .nodes.SelectMany(value => value.sources)
                .Single(value => value.workTypeId == BuiltInWorkTypeIds.Logging.Value)
                .pendingOutput;
            Require(restoredPending.parentPublicationKey == savedPublicationKey
                    && string.IsNullOrEmpty(restoredPending.parentResultKey),
                "JSON restore changed the pending publication identity.");
            Require(restored.TryFinalizePendingOutput(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out bool completed)
                && completed,
                "Fresh runtime did not replay the exact hidden acknowledgement.");

            DungeonWorldResourceSaveData completedSave = restored.Capture();
            WorldResourceSourceSaveData completedSource = completedSave.nodes
                .SelectMany(value => value.sources)
                .Single(value => value.workTypeId == BuiltInWorkTypeIds.Logging.Value);
            Require(completedSource.completedCycleSequence == 1
                    && completedSource.remainingCycles == 0
                    && completedSource.pendingOutput.IsEmpty
                    && completedSource.workContributions.Count == 0
                    && string.IsNullOrEmpty(restored.LastFactFailure)
                    && fixture.DomainFacts.PublishCount == 2
                    && fixture.DomainFacts.ReplayBatchCount == 1
                    && fixture.Output.PrepareAttempts == prepareAttemptsAfterCommit
                    && fixture.Output.CommitAttempts == commitAttemptsAfterCommit
                    && JsonUtility.ToJson(fixture.Items.Capture()) == physicalAfterCommit
                    && fixture.Outcomes.Transaction.Capture().owners.Count == 1
                    && fixture.Outcomes.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == 1
                    && fixture.ParentProbe.LatestCommittedKey(
                        MigratedProducerOutcomeKind.WorldResourceCycleResult)
                        .ToString() == savedPublicationKey,
                "Hidden-ack replay repeated source/output effects or duplicated the outcome.");

            string completedJson = JsonUtility.ToJson(completedSave);
            Require(!restored.TryFinalizePendingOutput(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out bool duplicate)
                    && !duplicate,
                "Completed hidden-ack cycle accepted a duplicate finalization.");
            restored.Tick();
            Require(JsonUtility.ToJson(restored.Capture()) == completedJson
                    && fixture.DomainFacts.ReplayBatchCount == 1
                    && fixture.Output.PrepareAttempts == prepareAttemptsAfterCommit
                    && fixture.Output.CommitAttempts == commitAttemptsAfterCommit
                    && JsonUtility.ToJson(fixture.Items.Capture()) == physicalAfterCommit
                    && fixture.Outcomes.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == 1,
                "Completed hidden-ack replay changed source, physical output or ledger.");
        }
        finally
        {
            restored?.Dispose();
            restoredNodes.Dispose();
            fixture.Dispose();
        }

        static void VerifyStrictPublicationIdentityShape(
            WorldResourceRuntime runtime)
        {
            var section = new WorldResourceSaveSection(runtime);
            string canonical = section.Capture();

            JObject missing = JObject.Parse(canonical);
            Pending(missing).Property("parentPublicationKey")?.Remove();
            RequireThrows(
                () => ((IDungeonSaveSectionPreflight)section).ValidatePayload(
                    missing.ToString(Newtonsoft.Json.Formatting.None),
                    section.SectionVersion,
                    new DungeonGameRestoreReport()),
                "World-resource save admitted a missing parent publication key.");

            JObject explicitNull = JObject.Parse(canonical);
            Pending(explicitNull)["parentPublicationKey"] = JValue.CreateNull();
            RequireThrows(
                () => ((IDungeonSaveSectionPreflight)section).ValidatePayload(
                    explicitNull.ToString(Newtonsoft.Json.Formatting.None),
                    section.SectionVersion,
                    new DungeonGameRestoreReport()),
                "World-resource save admitted a null parent publication key.");

            static JObject Pending(JObject root) => root["nodes"]
                .Children<JObject>()
                .SelectMany(value => value["sources"].Children<JObject>())
                .Select(value => value["pendingOutput"] as JObject)
                .Single(value => value != null
                    && !string.IsNullOrEmpty((string)value["operationId"]));
        }
    }

    private sealed class RealPhysicalFixture : IDisposable
    {
        private readonly CycleFactFixture facts = new(
            "run:e10-real-physical");
        private readonly IResourceEconomyContentCatalog catalog;
        private readonly WorldResourceSourceBindingCatalog bindings = CreateBuiltInBindings();
        private readonly WorldResourceOutputMaximumEnvelopeAuthority maximum;
        internal readonly MatrixEnvironment Environment = new();
        internal readonly RejectPhysicalPreparation OutputFault = new();
        internal readonly WorldItemStackRuntime Items;
        internal readonly ObservedOutputPublication Output;
        internal readonly FacilityBufferDestinationClaimRegistry Claims = new();
        internal readonly FacilityBufferMassAdmissionService Admission;
        internal MigratedProducerOutcomeEditorFixture Outcomes =>
            facts.Outcomes;
        internal MigratedProducerOutcomeProbeTransaction ParentProbe =>
            facts.Probe;
        internal ControlledCycleFactPublisher DomainFacts =>
            facts.Publisher;

        internal RealPhysicalFixture()
        {
            catalog = new ResourceEconomyContentCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            IDungeonItemCatalogProvider itemCatalog = EditorItemCatalogFactory.Create();
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(itemCatalog,
                out WorldItemRepository repository, out _, out ItemQuantityReservationService reservations,
                out _, out _, out IPhysicalItemBatchDispositionService dispositions);
            Admission = new FacilityBufferMassAdmissionService(Claims,
                new FacilityBufferPhysicalOccupancyQuery(repository, Items.MassQuery, reservations),
                Items.MassQuery);
            FacilityBufferDestinationLifecycleService lifecycle = new(Claims, Claims, Admission, Admission);
            FacilityBufferPlannedOutputPublicationService publication = new(repository, itemCatalog,
                Items.MassQuery, Admission, OutputFault);
            PhysicalItemExactSourcePublicationService exact = new(repository, Items.MassQuery, lifecycle,
                Claims, Admission, Admission, publication, new UnexpectedRetainedRelease(), dispositions,
                new PhysicalItemExactSourcePublicationDebugScenarios.MutableAcknowledgedCandidates());
            Output = new ObservedOutputPublication(
                new WorldResourceOutputPublicationPortAdapter(exact, Items.MassQuery));
            ProductionPreparedOutputComponentCodec codec = new();
            ProductionOutputMaximumMassRegistry maximumMass = new(
                new IProductionOutputMaximumMassCapability[]
                {
                    new StandardDefinitionProductionOutputCapability(catalog, codec)
                }, Items.MassQuery);
            maximum = new WorldResourceOutputMaximumEnvelopeAuthority(catalog, bindings, maximumMass);
            ParentProbe.TargetKind =
                MigratedProducerOutcomeKind.WorldResourceCycleResult;
            ParentProbe.FaultMode =
                MigratedProducerOutcomeFaultMode.RejectReservation;
        }

        internal WorldResourceRuntime CreateRuntime(FakeNodeHost nodes) => new(
            new WorldResourceEnvironmentDependencies(Environment, catalog, bindings, maximum, Output,
                DomainFacts, new UnityGameClock()),
            new WorldResourceInfrastructureDependencies(nodes),
            new WorldResourceProgressionDependencies(new RandomStreamProvider(157186),
                new FakePersistentIds(), new AlwaysResearched(), new NoGrandProjectBenefits()),
            new DungeonRuntimeAggregateRootStore());

        public void Dispose()
        {
            facts.Dispose();
            Items.Dispose();
        }
    }

    private sealed class RejectPhysicalPreparation : IFacilityBufferPlannedOutputPublicationFaultInjector
    {
        internal bool Enabled = true;
        public bool FailBeforeRepositoryAdd(int zeroBasedStackIndex) => Enabled;
    }

    private sealed class ObservedOutputPublication : IWorldResourceOutputPublicationPort
    {
        private readonly WorldResourceOutputPublicationPortAdapter inner;
        internal int PrepareAttempts { get; private set; }
        internal int CommitAttempts { get; private set; }
        internal string LastPrepareFailure { get; private set; } = string.Empty;
        internal string LastCommitFailure { get; private set; } = string.Empty;
        internal string LastCommitStatus { get; private set; } = "NOT_RUN";
        internal ObservedOutputPublication(WorldResourceOutputPublicationPortAdapter inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        public long GetDefinitionUnitMassGrams(string itemId) => inner.GetDefinitionUnitMassGrams(itemId);
        public bool TryPrepare(WorldResourcePendingOutputSaveData pending, Vector2Int position,
            out WorldResourceOutputPublicationTransaction transaction, out string failureReason)
        {
            PrepareAttempts++;
            bool prepared = inner.TryPrepare(pending, position, out transaction, out failureReason);
            LastPrepareFailure = failureReason;
            return prepared;
        }
        public WorldResourceOutputCommitStatus CommitReleased(WorldResourceOutputPublicationTransaction transaction,
            Vector2Int position, out WorldResourceOutputCommitReceipt receipt, out string failureReason)
        {
            CommitAttempts++;
            WorldResourceOutputCommitStatus status = inner.CommitReleased(transaction, position,
                out receipt, out failureReason);
            LastCommitFailure = failureReason;
            LastCommitStatus = status.ToString();
            return status;
        }
        public bool TryRollback(WorldResourceOutputPublicationTransaction transaction, string reasonCode,
            out string failureReason) => inner.TryRollback(transaction, reasonCode, out failureReason);
    }

    private sealed class UnexpectedRetainedRelease : IFacilityBufferDestinationReleaseService
    {
        public bool TryReleaseAtOwnerPosition(string destinationId, Vector2Int ownerPosition,
            string reasonCode, out int releasedQuantity, out string failureReason) =>
            throw new InvalidOperationException("E10 uses direct acknowledged release, not retained delivery.");
    }

    public static string RunAll()
    {
        ProductionRecipeSO recipe = AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
            "Assets/Resources/SO/Economy/Recipes/source_logging.asset");
        ResourceItemDefinitionSO item =
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
                "Assets/Resources/SO/Economy/Items/resource_log.asset");
        Require(recipe != null && item != null,
            "The authored logging recipe or log item is missing.");

        ResourceEconomyContentCatalog catalog = new(
            new[] { item },
            new[] { recipe },
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        FakeEnvironment environment = new();
        FakeOutputPublication output = new();
        FakeNodeHost nodes = new();
        using CycleFactFixture facts = new(
            "run:world-resource-transaction-fault");
        WorldResourceSourceBindingCatalog bindings = CreateBuiltInBindings();
        WorldResourceRuntime runtime = new(
            new WorldResourceEnvironmentDependencies(
                environment,
                catalog,
                 bindings,
                 new FakeMaximumEnvelopeAuthority(catalog, bindings, output),
                 output,
                 facts.Publisher,
                 new UnityGameClock()),
            new WorldResourceInfrastructureDependencies(nodes),
            new WorldResourceProgressionDependencies(
                new RandomStreamProvider(157181),
                new FakePersistentIds(),
                new AlwaysResearched(),
                new NoGrandProjectBenefits()),
            new DungeonRuntimeAggregateRootStore());

        try
        {
            runtime.Tick();
            Require(runtime.NodeCount == 1,
                "The focused topology did not create exactly one node.");
            WorldResourceNode node = runtime.Nodes.Single();
            Require(runtime.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot work)
                && work.Available,
                "The focused logging work was unavailable.");

            output.FailFirstCommitAfterAdmission = true;
            Require(!runtime.ApplyWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    work.RequiredWork,
                    out bool completedOnFault)
                && !completedOnFault,
                "The injected acknowledgement fault completed the cycle.");
            Require(output.PrepareCount == 1 && output.CommitCount == 1,
                "The first publication attempt did not prepare and commit once.");
            Require(runtime.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot retained)
                && retained.PendingOutputReady
                && retained.Available
                && Math.Abs(retained.CompletedWork - retained.RequiredWork) < 0.001f,
                "The frozen completed result was not retained for forward retry.");

            bool saveBlocked = false;
            try
            {
                runtime.Capture();
            }
            catch (InvalidOperationException exception)
            {
                saveBlocked = exception.Message.Contains(
                    "transient or poisoned output transaction",
                    StringComparison.Ordinal);
            }
            Require(saveBlocked,
                "Save capture was not blocked during a retained publication transaction.");

            Require(runtime.TryFinalizePendingOutput(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out bool completedOnRetry)
                && completedOnRetry,
                "The retained publication did not finalize on forward retry.");
            Require(output.PrepareCount == 1 && output.CommitCount == 2,
                "Retry prepared a second batch or skipped the retained transaction.");
            DungeonWorldResourceSaveData committed = runtime.Capture();
            WorldResourceSourceSaveData committedSource = committed.nodes
                .Single().sources.Single();
            Require(committedSource.remainingCycles == 0
                    && committedSource.completedCycleSequence == 1
                    && Math.Abs(committedSource.completedWork) < 0.001f
                    && committedSource.pendingOutput != null
                    && committedSource.pendingOutput.IsEmpty
                    && facts.Outcomes.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == 1,
                "Source debit and frozen output did not finalize exactly once.");

            WorldResourceNode retainedNode = runtime.Nodes.Single();
            string committedFingerprint = Fingerprint(committed);
            environment.SetInvalidTopology();
            bool topologyRejected = false;
            try
            {
                runtime.Tick();
            }
            catch (InvalidOperationException)
            {
                topologyRejected = true;
            }
            Require(topologyRejected,
                "An unregistered topology capability was accepted.");
            Require(runtime.NodeCount == 1
                    && ReferenceEquals(runtime.Nodes.Single(), retainedNode)
                    && string.Equals(
                        Fingerprint(runtime.Capture()),
                        committedFingerprint,
                        StringComparison.Ordinal),
                "Rejected topology validation destroyed or mutated the live resource state.");

            environment.SetValidTopology(worldRevision: 3);
            runtime.Tick();
            DungeonWorldResourceSaveData rebuilt = runtime.Capture();
            WorldResourceSourceSaveData rebuiltSource = rebuilt.nodes
                .Single().sources.Single();
            Require(runtime.NodeCount == 1
                    && rebuiltSource.remainingCycles == 0
                    && rebuiltSource.completedCycleSequence == 1
                    && rebuiltSource.pendingOutput.IsEmpty,
                "A valid topology replacement did not preserve the committed source state.");
            VerifyFrozenRootSeedTamperRejected(recipe, item);
            VerifyZeroPhysicalOutputCycle(item);
            VerifyParentFailureSaveRestore(recipe, item);
            VerifyFiniteRenewableBindingMatrix();

            return string.Join("\n", new[]
            {
                "# V27 World Resource Transaction Fault Matrix",
                "schemaVersion=1",
                "recipe=source:logging",
                "prepareCount=1",
                "commitCount=2",
                "sourceDebitCount=1",
                "saveBlockedDuringRetained=true",
                "retryReusedFrozenTransaction=true",
                "invalidTopologyLiveStateUnchanged=true",
                "validTopologyRetainedSequence=1",
                "rootSeedTamperRejected=true",
                "maximumMassTamperRejected=true",
                "maximumSourceDigestTamperRejected=true",
                "zeroPhysicalOutputCycleExact=true",
                "parentFailureSaveRestoreExact=true",
                "finiteRenewableBindingMatrixExact=true",
                "RESULT=PASS"
            }) + "\n";
        }
        finally
        {
            runtime.Dispose();
            nodes.Dispose();
        }
    }

    private static void VerifyFrozenRootSeedTamperRejected(
        ProductionRecipeSO recipe,
        ResourceItemDefinitionSO item)
    {
        FakeNodeHost nodes = new();
        FakeOutputPublication output = new() { FailPrepare = true };
        using CycleFactFixture facts = new(
            "run:world-resource-frozen-tamper");
        WorldResourceSourceBindingCatalog bindings = CreateBuiltInBindings();
        ResourceEconomyContentCatalog catalog = new(
            new[] { item },
            new[] { recipe },
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        WorldResourceRuntime runtime = new(
            new WorldResourceEnvironmentDependencies(
                new FakeEnvironment(),
                catalog,
                 bindings,
                 new FakeMaximumEnvelopeAuthority(catalog, bindings, output),
                 output,
                 facts.Publisher,
                 new UnityGameClock()),
            new WorldResourceInfrastructureDependencies(nodes),
            new WorldResourceProgressionDependencies(
                new RandomStreamProvider(157181),
                new FakePersistentIds(),
                new AlwaysResearched(),
                new NoGrandProjectBenefits()),
            new DungeonRuntimeAggregateRootStore());
        try
        {
            runtime.Tick();
            WorldResourceNode node = runtime.Nodes.Single();
            Require(runtime.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot work)
                && !runtime.ApplyWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    work.RequiredWork,
                    out bool completed)
                && !completed,
                "The root-seed fixture did not retain an unpublished frozen output.");
            string canonical = JsonUtility.ToJson(runtime.Capture());
            DungeonWorldResourceSaveData candidate = JsonUtility.FromJson<
                DungeonWorldResourceSaveData>(canonical);
            WorldResourcePendingOutputSaveData pending = candidate.nodes
                .Single().sources.Single().pendingOutput;
            Require(pending != null && !pending.IsEmpty,
                "The root-seed fixture did not capture a frozen output.");
            pending.rootSeed = checked(pending.rootSeed + 1);
            RequireThrows(
                () => runtime.BuildRestore(candidate),
                "A frozen WorldResource outcome accepted a different root seed.");

            DungeonWorldResourceSaveData massTamper = JsonUtility.FromJson<
                DungeonWorldResourceSaveData>(canonical);
            massTamper.nodes.Single().sources.Single().pendingOutput
                .maximumOutputMassGrams++;
            RequireThrows(
                () => runtime.BuildRestore(massTamper),
                "A frozen WorldResource outcome accepted a drifted maximum mass.");

            DungeonWorldResourceSaveData digestTamper = JsonUtility.FromJson<
                DungeonWorldResourceSaveData>(canonical);
            digestTamper.nodes.Single().sources.Single().pendingOutput
                .maximumOutputSourceDigest = new string('f', 64);
            RequireThrows(
                () => runtime.BuildRestore(digestTamper),
                "A frozen WorldResource outcome accepted a drifted maximum proof.");
        }
        finally
        {
            runtime.Dispose();
            nodes.Dispose();
        }
    }

    private static void VerifyZeroPhysicalOutputCycle(
        ResourceItemDefinitionSO item)
    {
        ProductionRecipeSO recipe = ScriptableObject.CreateInstance<ProductionRecipeSO>();
        FakeNodeHost nodes = new();
        using CycleFactFixture facts = new(
            "run:world-resource-zero-output");
        WorldResourceRuntime runtime = null;
        try
        {
            recipe.Configure(
                "source:qa-zero-output",
                "QA zero output source",
                "Deterministic zero-output source fixture",
                "world-resource:qa",
                BuiltInWorkTypeIds.Logging.Value,
                string.Empty,
                1f,
                Array.Empty<ItemAmountDefinition>(),
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:qa-zero-output",
                        ProductionOutputRole.Main,
                        item.ItemId,
                        1,
                        probability: 0f)
                });
            recipe.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.Fieldwork);
            recipe.ConfigureProcessClass(ProductionProcessClass.Gathering);
            recipe.ConfigureFlowRole(ProductionFlowRole.Source);
            ResourceEconomyContentCatalog catalog = new(
                new[] { item },
                new[] { recipe },
                Array.Empty<CropDefinitionSO>(),
                Array.Empty<CraftMaterialDefinitionSO>());
            FakeEnvironment environment = new();
            FakeOutputPublication output = new();
            SingleTreeSourceBindingCatalog bindings =
                new(recipe.RecipeId);
            runtime = new WorldResourceRuntime(
                new WorldResourceEnvironmentDependencies(
                    environment,
                    catalog,
                     bindings,
                     new FakeMaximumEnvelopeAuthority(catalog, bindings, output),
                     output,
                     facts.Publisher,
                     new UnityGameClock()),
                new WorldResourceInfrastructureDependencies(nodes),
                new WorldResourceProgressionDependencies(
                    new RandomStreamProvider(157182),
                    new FakePersistentIds(),
                    new AlwaysResearched(),
                    new NoGrandProjectBenefits()),
                new DungeonRuntimeAggregateRootStore());
            runtime.Tick();
            WorldResourceNode node = runtime.Nodes.Single();
            Require(runtime.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot work)
                && runtime.ApplyWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    work.RequiredWork,
                    out bool completed)
                && completed,
                "A zero-physical-output source cycle did not complete.");
            WorldResourceSourceSaveData source = runtime.Capture().nodes
                .Single().sources.Single();
            Require(source.remainingCycles == 0
                    && source.completedCycleSequence == 1
                    && source.pendingOutput.IsEmpty
                    && output.PrepareCount == 0
                    && output.CommitCount == 0
                    && facts.Outcomes.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == 1,
                "Zero-output completion published a fake item or failed exact source debit.");
        }
        finally
        {
            runtime?.Dispose();
            nodes.Dispose();
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static string Fingerprint(DungeonWorldResourceSaveData data)
    {
        StringBuilder value = new();
        foreach (WorldResourceNodeSaveData node in data.nodes
                     .OrderBy(entry => entry.buildingInstanceId,
                         StringComparer.Ordinal))
        {
            value.Append(node.buildingInstanceId).Append('@')
                .Append(node.gridX).Append(',').Append(node.gridY).Append('|');
            foreach (WorldResourceSourceSaveData source in node.sources
                         .OrderBy(entry => entry.workTypeId,
                             StringComparer.Ordinal))
            {
                value.Append(source.workTypeId).Append('|')
                    .Append(source.recipeId).Append('|')
                    .Append(source.completedWork.ToString("R",
                        System.Globalization.CultureInfo.InvariantCulture))
                    .Append('|').Append(source.remainingCycles)
                    .Append('|').Append(source.completedCycleSequence)
                    .Append('|').Append(source.pendingOutput?.outcomeFingerprint)
                    .Append(';');
            }
        }
        return value.ToString();
    }

    private static void WriteIfChanged(string path, string contents)
    {
        string normalized = contents.Replace("\r\n", "\n");
        if (File.Exists(path)
            && string.Equals(
                File.ReadAllText(path, Encoding.UTF8),
                normalized,
                StringComparison.Ordinal))
        {
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Report directory is missing."));
        File.WriteAllText(
            path,
            normalized,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class MatrixEnvironment : IWorldResourceEnvironmentPort
    {
        internal const string PatchId = "wildlife-habitat:qa:grass";
        private int revision = 1;

        internal float CurrentResource { get; private set; } = 8f;

        internal void Regenerate(float amount)
        {
            CurrentResource = Mathf.Max(0f, CurrentResource + amount);
            revision++;
        }

        public bool TryCaptureTopology(out WorldResourceTopologySnapshot value)
        {
            value = new WorldResourceTopologySnapshot(
                revision,
                revision,
                new[]
                {
                    new WorldResourceVisualSnapshot(
                        "qa:matrix:tree",
                        new Vector2Int(4, 5),
                        WorldResourceVisualKind.Tree),
                    new WorldResourceVisualSnapshot(
                        "qa:matrix:rock",
                        new Vector2Int(6, 5),
                        WorldResourceVisualKind.Rock)
                },
                new[]
                {
                    new WorldResourceRenewablePatchSnapshot(
                        (WildlifeHabitatPatchId)PatchId,
                        WildlifeHabitatType.Grass,
                        new Vector2Int(8, 5),
                        CurrentResource,
                        Mathf.Clamp01(CurrentResource / 8f))
                });
            return true;
        }

        public bool TryGetRenewablePatch(
            WildlifeHabitatPatchId patchId,
            out WorldResourceRenewablePatchSnapshot patch)
        {
            if (!string.Equals(patchId.Value, PatchId, StringComparison.Ordinal))
            {
                patch = default;
                return false;
            }
            patch = new WorldResourceRenewablePatchSnapshot(
                patchId,
                WildlifeHabitatType.Grass,
                new Vector2Int(8, 5),
                CurrentResource,
                Mathf.Clamp01(CurrentResource / 8f));
            return true;
        }

        public bool TryConsumeRenewablePatchExact(
            WildlifeHabitatPatchId patchId,
            float amount,
            out WorldResourceRenewableDebitReceipt receipt)
        {
            receipt = default;
            if (!string.Equals(patchId.Value, PatchId, StringComparison.Ordinal)
                || amount <= 0f
                || CurrentResource < amount)
            {
                return false;
            }
            float before = CurrentResource;
            CurrentResource -= amount;
            receipt = new WorldResourceRenewableDebitReceipt(
                patchId,
                amount,
                before,
                CurrentResource);
            return true;
        }

        public bool TryRollbackRenewablePatchDebit(
            WorldResourceRenewableDebitReceipt receipt)
        {
            if (!receipt.IsValid
                || !string.Equals(
                    receipt.PatchId.Value,
                    PatchId,
                    StringComparison.Ordinal)
                || !Mathf.Approximately(CurrentResource, receipt.AfterResource))
            {
                return false;
            }
            CurrentResource = receipt.BeforeResource;
            return true;
        }

        public void RefreshRenewablePatch(WildlifeHabitatPatchId patchId)
        {
        }

        public void SetResourceVisualActive(string visualId, bool active)
        {
        }
    }

    private sealed class FakeEnvironment : IWorldResourceEnvironmentPort
    {
        private WorldResourceTopologySnapshot topology;

        internal FakeEnvironment()
        {
            SetValidTopology(worldRevision: 1);
        }

        internal void SetValidTopology(int worldRevision)
        {
            topology = new WorldResourceTopologySnapshot(
                worldRevision,
                worldRevision,
                new[]
                {
                    new WorldResourceVisualSnapshot(
                        "qa:tree:1",
                        new Vector2Int(4, 5),
                        WorldResourceVisualKind.Tree)
                },
                Array.Empty<WorldResourceRenewablePatchSnapshot>());
        }

        internal void SetInvalidTopology()
        {
            topology = new WorldResourceTopologySnapshot(
                2,
                2,
                new[]
                {
                    new WorldResourceVisualSnapshot(
                        "qa:unknown:1",
                        new Vector2Int(4, 5),
                        (WorldResourceVisualKind)999)
                },
                Array.Empty<WorldResourceRenewablePatchSnapshot>());
        }

        public bool TryCaptureTopology(out WorldResourceTopologySnapshot value)
        {
            value = topology;
            return true;
        }

        public bool TryGetRenewablePatch(
            WildlifeHabitatPatchId patchId,
            out WorldResourceRenewablePatchSnapshot patch)
        {
            patch = default;
            return false;
        }

        public bool TryConsumeRenewablePatchExact(
            WildlifeHabitatPatchId patchId,
            float amount,
            out WorldResourceRenewableDebitReceipt receipt)
        {
            receipt = default;
            return false;
        }

        public bool TryRollbackRenewablePatchDebit(
            WorldResourceRenewableDebitReceipt receipt) => false;

        public void RefreshRenewablePatch(WildlifeHabitatPatchId patchId)
        {
        }

        public void SetResourceVisualActive(string visualId, bool active)
        {
        }
    }

    private sealed class FakeOutputPublication :
        IWorldResourceOutputPublicationPort
    {
        private sealed class Token : IWorldResourceOutputPublicationToken
        {
        }

        internal bool FailFirstCommitAfterAdmission { get; set; }
        internal bool FailPrepare { get; set; }
        internal int PrepareCount { get; private set; }
        internal int CommitCount { get; private set; }
        private WorldResourcePendingOutputSaveData pending;

        public long GetDefinitionUnitMassGrams(string itemId) => 1_840L;

        public bool TryPrepare(
            WorldResourcePendingOutputSaveData pending,
            Vector2Int position,
            out WorldResourceOutputPublicationTransaction transaction,
            out string failureReason)
        {
            PrepareCount++;
            this.pending = pending?.Clone();
            if (FailPrepare)
            {
                transaction = default;
                failureReason = "qa:prepare-blocked";
                return false;
            }
            transaction = new WorldResourceOutputPublicationTransaction(
                new Token());
            failureReason = string.Empty;
            return true;
        }

        public WorldResourceOutputCommitStatus CommitReleased(
            WorldResourceOutputPublicationTransaction transaction,
            Vector2Int position,
            out WorldResourceOutputCommitReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            CommitCount++;
            if (FailFirstCommitAfterAdmission && CommitCount == 1)
            {
                failureReason = "qa:acknowledgement-fault";
                return WorldResourceOutputCommitStatus.RetryableRetained;
            }
            WorldResourceResolvedOutputLineSaveData[] physical = (pending?.lines
                    ?? new List<WorldResourceResolvedOutputLineSaveData>())
                .Where(value => value != null
                    && ProductionOutputRoleRules.IsPhysical(value.role)
                    && value.resolvedQuantity > 0)
                .ToArray();
            receipt = new WorldResourceOutputCommitReceipt(
                "qa:world-resource-batch",
                "qa:world-resource-destination",
                pending?.physicalOutputMassGrams ?? 0L,
                physical.Select(value => new WorldResourcePublishedStackSaveData
                {
                    stackId = "qa:stack:" + value.outputLineId,
                    outputLineId = value.outputLineId,
                    itemId = value.itemId,
                    quantity = value.resolvedQuantity,
                    massGrams = value.resolvedMassGrams
                }).ToArray());
            failureReason = string.Empty;
            return WorldResourceOutputCommitStatus.Committed;
        }

        public bool TryRollback(
            WorldResourceOutputPublicationTransaction transaction,
            string reasonCode,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private static void VerifyParentFailureSaveRestore(
        ProductionRecipeSO recipe,
        ResourceItemDefinitionSO item)
    {
        ResourceEconomyContentCatalog catalog = new(
            new[] { item },
            new[] { recipe },
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        WorldResourceSourceBindingCatalog bindings = CreateBuiltInBindings();
        FakeEnvironment environment = new();
        FakeOutputPublication output = new();
        using CycleFactFixture facts = new(
            "run:world-resource-parent-failure");
        facts.Publisher.FailNext = true;
        string savedPublicationKey = string.Empty;
        FakeNodeHost firstNodes = new();
        WorldResourceRuntime first = new(
            new WorldResourceEnvironmentDependencies(
                environment,
                catalog,
                bindings,
                new FakeMaximumEnvelopeAuthority(catalog, bindings, output),
                output,
                facts.Publisher,
                new UnityGameClock()),
            new WorldResourceInfrastructureDependencies(firstNodes),
            new WorldResourceProgressionDependencies(
                new RandomStreamProvider(157183),
                new FakePersistentIds(),
                new AlwaysResearched(),
                new NoGrandProjectBenefits()),
            new DungeonRuntimeAggregateRootStore());
        DungeonWorldResourceSaveData saved;
        try
        {
            first.Tick();
            WorldResourceNode node = first.Nodes.Single();
            Require(first.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot work),
                "Parent-failure fixture has no logging work.");
            Require(first.ApplyWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    work.RequiredWork * 0.4f,
                    "character:w06-logger-a",
                    out bool partial)
                && !partial,
                "First logger contribution did not remain partial.");
            Require(!first.ApplyWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    work.RequiredWork * 0.6f,
                    "character:w06-logger-b",
                    out bool rejected)
                && !rejected,
                "Injected parent rejection incorrectly completed the source cycle.");
            saved = JsonUtility.FromJson<DungeonWorldResourceSaveData>(
                JsonUtility.ToJson(first.Capture()));
            WorldResourceSourceSaveData source = saved.nodes.Single().sources.Single();
            WorldResourcePendingOutputSaveData pending = source.pendingOutput;
            savedPublicationKey = pending.parentPublicationKey;
            Require(pending.domainApplied
                    && !string.IsNullOrEmpty(savedPublicationKey)
                    && string.IsNullOrEmpty(pending.parentResultKey)
                    && pending.contributions.Count == 2
                    && pending.sourceBefore == 1f
                    && pending.sourceAfter == 0f
                    && pending.publishedStacks.Sum(value => value.massGrams)
                        == pending.physicalOutputMassGrams
                    && output.CommitCount == 1
                    && facts.Publisher.PublishCount == 1,
                "Parent rejection did not preserve exact source/output/contributor evidence.");
        }
        finally
        {
            first.Dispose();
            firstNodes.Dispose();
        }

        FakeOutputPublication restoredOutput = new();
        FakeNodeHost restoredNodes = new();
        WorldResourceRuntime restored = new(
            new WorldResourceEnvironmentDependencies(
                environment,
                catalog,
                bindings,
                new FakeMaximumEnvelopeAuthority(
                    catalog,
                    bindings,
                    restoredOutput),
                restoredOutput,
                facts.Publisher,
                new UnityGameClock()),
            new WorldResourceInfrastructureDependencies(restoredNodes),
            new WorldResourceProgressionDependencies(
                new RandomStreamProvider(157183),
                new FakePersistentIds(),
                new AlwaysResearched(),
                new NoGrandProjectBenefits()),
            new DungeonRuntimeAggregateRootStore());
        try
        {
            restored.Restore(restored.BuildRestore(saved));
            restored.Tick();
            WorldResourceNode node = restored.Nodes.Single();
            WorldResourcePendingOutputSaveData restoredPending = restored.Capture()
                .nodes.Single().sources.Single().pendingOutput;
            Require(restoredPending.parentPublicationKey == savedPublicationKey
                    && string.IsNullOrEmpty(restoredPending.parentResultKey),
                "Fresh runtime changed the rejected parent publication identity.");
            Require(restored.TryFinalizePendingOutput(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out bool completed)
                && completed,
                "Fresh runtime did not resume the rejected parent outcome.");
            WorldResourceSourceSaveData source = restored.Capture().nodes
                .Single().sources.Single();
            Require(restoredOutput.CommitCount == 0
                    && facts.Publisher.PublishCount == 2
                    && facts.Publisher.ReplayBatchCount == 1
                    && source.completedCycleSequence == 1
                    && source.remainingCycles == 0
                    && source.pendingOutput.IsEmpty
                    && facts.Outcomes.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == 1,
                "Parent-only retry repeated output/source effects or failed retirement.");
        }
        finally
        {
            restored.Dispose();
            restoredNodes.Dispose();
        }
    }

    private static void VerifyFiniteRenewableBindingMatrix()
    {
        ProductionRecipeSO[] recipes =
        {
            AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                "Assets/Resources/SO/Economy/Recipes/source_logging.asset"),
            AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                "Assets/Resources/SO/Economy/Recipes/source_saltstone.asset"),
            AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(
                "Assets/Resources/SO/Economy/Recipes/source_grass.asset")
        };
        ResourceItemDefinitionSO[] items =
        {
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
                "Assets/Resources/SO/Economy/Items/resource_log.asset"),
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
                "Assets/Resources/SO/Economy/Items/resource_dark_resin.asset"),
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
                "Assets/Resources/SO/Economy/Items/resource_stone.asset"),
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
                "Assets/Resources/SO/Economy/Items/resource_saltstone.asset"),
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(
                "Assets/Resources/SO/Economy/Items/resource_grass_straw.asset")
        };
        Require(recipes.All(value => value != null)
                && items.All(value => value != null),
            "The finite/renewable authored source matrix is incomplete.");

        ResourceEconomyContentCatalog catalog = new(
            items,
            recipes,
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        WorldResourceSourceBindingCatalog bindings = CreateBuiltInBindings();
        MatrixEnvironment environment = new();
        FakeOutputPublication output = new();
        using CycleFactFixture facts = new(
            "run:world-resource-binding-matrix");
        facts.Publisher.RejectAll = true;
        FakeNodeHost nodes = new();
        WorldResourceRuntime runtime = new(
            new WorldResourceEnvironmentDependencies(
                environment,
                catalog,
                bindings,
                new FakeMaximumEnvelopeAuthority(catalog, bindings, output),
                output,
                facts.Publisher,
                new UnityGameClock()),
            new WorldResourceInfrastructureDependencies(nodes),
            new WorldResourceProgressionDependencies(
                new RandomStreamProvider(157185),
                new FakePersistentIds(),
                new AlwaysResearched(),
                new NoGrandProjectBenefits()),
            new DungeonRuntimeAggregateRootStore());
        try
        {
            runtime.Tick();
            WorkTypeId[] workTypes =
            {
                BuiltInWorkTypeIds.Logging,
                BuiltInWorkTypeIds.Quarry,
                BuiltInWorkTypeIds.Gather
            };
            foreach (WorkTypeId workType in workTypes)
            {
                WorldResourceNode node = runtime.Nodes.Single(candidate =>
                    runtime.TryGetWork(candidate, workType, out _));
                Require(runtime.TryGetWork(
                        node,
                        workType,
                        out WorldResourceWorkSnapshot work)
                    && work.Available,
                    "Authored world-resource work is unavailable: " + workType.Value);
                Require(runtime.ApplyWork(
                        node,
                        workType,
                        work.RequiredWork * 0.35f,
                        "character:w06:" + workType.Value + ":a",
                        out bool partial)
                    && !partial,
                    "Partial world-resource work did not remain partial: "
                    + workType.Value);
                Require(!runtime.ApplyWork(
                        node,
                        workType,
                        work.RequiredWork * 0.65f,
                        "character:w06:" + workType.Value + ":b",
                        out bool parentRejected)
                    && !parentRejected,
                    "Injected world-resource parent rejection completed the cycle: "
                    + workType.Value);
            }

            DungeonWorldResourceSaveData retained = runtime.Capture();
            WorldResourceSourceSaveData[] sources = retained.nodes
                .SelectMany(value => value.sources)
                .OrderBy(value => value.workTypeId, StringComparer.Ordinal)
                .ToArray();
            Require(sources.Length == 3
                    && sources.All(value => value.pendingOutput.domainApplied)
                    && sources.All(value => !string.IsNullOrEmpty(
                        value.pendingOutput.parentPublicationKey))
                    && sources.All(value => string.IsNullOrEmpty(
                        value.pendingOutput.parentResultKey))
                    && sources.All(value =>
                        value.pendingOutput.contributions.Count == 2)
                    && sources.Select(value => value.pendingOutput.operationId)
                        .Distinct(StringComparer.Ordinal).Count() == 3
                    && sources.Select(value =>
                            value.pendingOutput.completedAbsoluteDay)
                        .Distinct().Count() == 1
                    && output.CommitCount == 3,
                "Same-day resource nodes did not retain distinct exact terminal owners.");
            WorldResourcePendingOutputSaveData gather = sources.Single(value =>
                string.Equals(
                    value.workTypeId,
                    BuiltInWorkTypeIds.Gather.Value,
                    StringComparison.Ordinal)).pendingOutput;
            Require(gather.renewableSource
                    && string.Equals(
                        gather.sourcePatchId,
                        MatrixEnvironment.PatchId,
                        StringComparison.Ordinal)
                    && Mathf.Approximately(gather.sourceBefore, 8f)
                    && Mathf.Approximately(gather.sourceAfter, 6.5f)
                    && Mathf.Approximately(environment.CurrentResource, 6.5f),
                "Renewable gathering did not freeze the exact patch debit.");
            Require(sources.Where(value => !string.Equals(
                        value.workTypeId,
                        BuiltInWorkTypeIds.Gather.Value,
                        StringComparison.Ordinal))
                    .All(value => !value.pendingOutput.renewableSource
                        && value.pendingOutput.sourceBefore == 1f
                        && value.pendingOutput.sourceAfter == 0f),
                "Finite logging/quarry debit evidence is invalid.");

            int commitsBeforeRegeneration = output.CommitCount;
            environment.Regenerate(1f);
            runtime.Tick();
            DungeonWorldResourceSaveData afterRegeneration = runtime.Capture();
            WorldResourcePendingOutputSaveData retainedGather =
                afterRegeneration.nodes.SelectMany(value => value.sources)
                    .Single(value => string.Equals(
                        value.workTypeId,
                        BuiltInWorkTypeIds.Gather.Value,
                        StringComparison.Ordinal)).pendingOutput;
            Require(Mathf.Approximately(environment.CurrentResource, 7.5f)
                    && Mathf.Approximately(retainedGather.sourceBefore, 8f)
                    && Mathf.Approximately(retainedGather.sourceAfter, 6.5f)
                    && output.CommitCount == commitsBeforeRegeneration,
                "Renewable regeneration rewrote the completed gather debit or output.");

            facts.Publisher.RejectAll = false;
            foreach (WorkTypeId workType in workTypes)
            {
                WorldResourceNode node = runtime.Nodes.Single(candidate =>
                    runtime.TryGetWork(candidate, workType, out _));
                Require(runtime.TryFinalizePendingOutput(
                        node,
                        workType,
                        out bool completed)
                    && completed,
                    "Parent-only retry did not retire source work: " + workType.Value);
            }
            DungeonWorldResourceSaveData completedSave = runtime.Capture();
            Require(output.CommitCount == commitsBeforeRegeneration
                    && facts.Publisher.ReplayBatchCount == 3
                    && facts.Outcomes.Ledger.CaptureGameplayOutcomes()
                        .exactOutcomes.Count == 3
                    && completedSave.nodes.SelectMany(value => value.sources)
                        .All(value => value.completedCycleSequence == 1
                            && value.pendingOutput.IsEmpty
                            && value.workContributions.Count == 0),
                "World-resource parent retry repeated output or lost cycle retirement.");
        }
        finally
        {
            runtime.Dispose();
            nodes.Dispose();
        }
    }

    private sealed class CycleFactFixture : IDisposable
    {
        internal CycleFactFixture(string runId)
        {
            Outcomes = new MigratedProducerOutcomeEditorFixture(runId);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = MigratedProducerOutcomeKind.WorldResourceCycleResult
            };
            Dispatcher = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                Outcomes.Recorder,
                Outcomes.Ledger);
            Dispatcher.Subscribe(
                "fixture:world-resource-cycle-completed:" + runId,
                new MigratedProducerDomainFactProbeObserver<
                    WorldResourceCycleCompletedFact>(
                    WorldResourceCycleFactMapping.Capture,
                    Outcomes.Transaction,
                    commits,
                    Probe));
            Publisher = new ControlledCycleFactPublisher(Dispatcher);
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal DomainFactDispatcher Dispatcher { get; }
        internal ControlledCycleFactPublisher Publisher { get; }

        public void Dispose() => Dispatcher.Dispose();
    }

    private sealed class ControlledCycleFactPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private const string RejectedDetail = "qa:parent-rejected";
        private const string HiddenAcknowledgementDetail =
            "qa:hidden-durable-ack";
        private readonly DomainFactDispatcher inner;

        internal ControlledCycleFactPublisher(
            DomainFactDispatcher inner) => this.inner = inner
            ?? throw new ArgumentNullException(nameof(inner));

        internal bool FailNext { get; set; }
        internal bool RejectAll { get; set; }
        internal bool HideNextDeliveredAcknowledgement { get; set; }
        internal int PublishCount { get; private set; }
        internal int ReplayBatchCount { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => inner.TryReserve(
            proposed,
            out lease,
            out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            PublishCount++;
            if (ConsumeRejection())
            {
                inner.Cancel(lease);
                return DomainFactDelivery.Reject(RejectedDetail);
            }
            return HideAcknowledgement(inner.Publish(lease, actual));
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            PublishCount += facts?.Count ?? 0;
            if (ConsumeRejection())
            {
                foreach (DomainFactEmission fact in facts
                             ?? Array.Empty<DomainFactEmission>())
                {
                    if (fact != null)
                        inner.Cancel(fact.Lease);
                }
                for (int index = 0; index < (results?.Length ?? 0); index++)
                    results[index] = DomainFactDelivery.Reject(RejectedDetail);
                failure = RejectedDetail;
                return false;
            }
            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (accepted)
            {
                for (int index = 0; index < results.Length; index++)
                    results[index] = HideAcknowledgement(results[index]);
            }
            return accepted;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact)
        {
            PublishCount++;
            if (ConsumeRejection())
                return DomainFactDelivery.Reject(RejectedDetail);
            return HideAcknowledgement(inner.Replay(fact));
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            PublishCount += facts?.Count ?? 0;
            ReplayBatchCount++;
            if (RejectAll || FailNext)
            {
                FailNext = false;
                for (int index = 0; index < (results?.Length ?? 0); index++)
                    results[index] = DomainFactDelivery.Reject(RejectedDetail);
                failure = RejectedDetail;
                return false;
            }
            bool accepted = inner.ReplayBatch(facts, results, out failure);
            if (accepted)
            {
                for (int index = 0; index < results.Length; index++)
                    results[index] = HideAcknowledgement(results[index]);
            }
            return accepted;
        }

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        private bool ConsumeRejection()
        {
            if (RejectAll)
                return true;
            if (!FailNext)
                return false;
            FailNext = false;
            return true;
        }

        private DomainFactDelivery HideAcknowledgement(
            DomainFactDelivery delivery)
        {
            if (!HideNextDeliveredAcknowledgement
                || delivery.Phase is not (DomainFactDeliveryPhase.Delivered
                    or DomainFactDeliveryPhase.AcceptedPendingDelivery))
                return delivery;
            HideNextDeliveredAcknowledgement = false;
            return new DomainFactDelivery(
                DomainFactDeliveryPhase.InDoubt,
                delivery.OperationKey,
                delivery.PublicationId,
                delivery.Fingerprint,
                HiddenAcknowledgementDetail);
        }
    }

    private static WorldResourceSourceBindingCatalog CreateBuiltInBindings() =>
        new(new IWorldResourceSourceBindingContributor[]
        {
            new BuiltInWorldResourceSourceBindingContributor()
        });

    private sealed class FakeMaximumEnvelopeAuthority :
        IWorldResourceOutputMaximumEnvelopeAuthority
    {
        private readonly IReadOnlyList<WorldResourceOutputMaximumEnvelopeSnapshot>
            envelopes;
        private readonly IReadOnlyDictionary<string,
            WorldResourceOutputMaximumEnvelopeSnapshot> byRecipe;

        internal FakeMaximumEnvelopeAuthority(
            IResourceEconomyContentCatalog catalog,
            IWorldResourceSourceBindingCatalog bindings,
            IWorldResourceOutputPublicationPort output)
        {
            List<WorldResourceOutputMaximumEnvelopeSnapshot> captured = new();
            foreach (IGrouping<string, WorldResourceSourceBinding> group
                     in bindings.Bindings.GroupBy(
                             value => value.RecipeId,
                             StringComparer.Ordinal)
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                if (!catalog.TryGetRecipe(group.Key, out ProductionRecipeSO recipe))
                    continue;
                List<WorldResourceOutputMaximumLineSnapshot> lines = new();
                long total = 0L;
                foreach (ProductionOutputDefinition line in recipe
                             .CaptureCanonicalOutputs()
                             .OrderBy(value => value.OutputLineId,
                                 StringComparer.Ordinal))
                {
                    bool physical = ProductionOutputRoleRules.IsPhysical(
                            line.Role)
                        && line.Probability > 0f;
                    int quantity = physical ? line.Amount : 0;
                    long unit = physical
                        ? output.GetDefinitionUnitMassGrams(line.ItemId)
                        : 0L;
                    long mass = checked(unit * quantity);
                    total = checked(total + mass);
                    lines.Add(new WorldResourceOutputMaximumLineSnapshot(
                        line.OutputLineId,
                        line.Role,
                        line.ItemId,
                        line.Probability,
                        quantity,
                        unit,
                        mass,
                        physical ? new string('a', 64) : string.Empty));
                }
                string recipeDigest = ProductionRecipeSemanticDigest.Capture(recipe);
                CanonicalSemanticDigestBuilder digest = new();
                digest.Append("qa-world-resource-maximum@1");
                digest.Append(recipe.RecipeId);
                digest.Append(recipeDigest);
                digest.Append(total);
                captured.Add(new WorldResourceOutputMaximumEnvelopeSnapshot(
                    recipe.RecipeId,
                    group.Select(value => value.BindingId).ToArray(),
                    recipeDigest,
                    ProductionOutputFactor.One,
                    lines,
                    total,
                    1L,
                    new string('b', 64),
                    digest.ComputeSha256()));
            }
            envelopes = Array.AsReadOnly(captured.ToArray());
            byRecipe = captured.ToDictionary(
                value => value.RecipeId,
                value => value,
                StringComparer.Ordinal);
            CanonicalSemanticDigestBuilder authority = new();
            authority.Append("qa-world-resource-maximum-authority@1");
            foreach (WorldResourceOutputMaximumEnvelopeSnapshot value in captured)
                authority.Append(value.SourceDigest);
            AuthorityFingerprint = authority.ComputeSha256();
        }

        public string AuthorityFingerprint { get; }
        public IReadOnlyList<WorldResourceOutputMaximumEnvelopeSnapshot>
            Envelopes => envelopes;

        public WorldResourceOutputMaximumEnvelopeSnapshot Require(
            string recipeId) => byRecipe.TryGetValue(
            recipeId,
            out WorldResourceOutputMaximumEnvelopeSnapshot value)
                ? value
                : throw new InvalidOperationException(
                    "QA world-resource maximum proof is missing: " + recipeId);
    }

    private sealed class SingleTreeSourceBindingCatalog :
        IWorldResourceSourceBindingCatalog
    {
        private readonly WorldResourceSourceBinding binding;
        private readonly IReadOnlyList<WorldResourceSourceBinding> bindings;

        internal SingleTreeSourceBindingCatalog(string recipeId)
        {
            binding = new WorldResourceSourceBinding(
                "world-resource:qa-zero-output",
                WorldResourceSourceBindingKind.Visual,
                WorldResourceVisualKind.Tree,
                default,
                BuiltInWorkTypeIds.Logging,
                recipeId);
            bindings = Array.AsReadOnly(new[] { binding });
        }

        public IReadOnlyList<WorldResourceSourceBinding> Bindings => bindings;
        public string CatalogFingerprint { get; } = new string('c', 64);

        public WorldResourceSourceBinding RequireVisual(
            WorldResourceVisualKind kind) => kind == WorldResourceVisualKind.Tree
            ? binding
            : throw new InvalidOperationException(
                "The QA source binding supports only Tree.");

        public WorldResourceSourceBinding RequireRenewablePatch(
            WildlifeHabitatType habitatType) => throw new InvalidOperationException(
            "The QA source binding has no renewable patch.");
    }

    private sealed class FakeNodeHost : IWorldResourceNodeHostPort, IDisposable
    {
        private readonly List<GameObject> objects = new();

        public WorldResourceNode CreateNode(
            IWorldResourceRuntime runtime,
            BuildingInstanceId nodeId,
            Vector2Int position,
            string displayName)
        {
            GameObject target = new("QA_WorldResourceNode");
            objects.Add(target);
            WorldResourceNode node = target.AddComponent<WorldResourceNode>();
            node.Configure(runtime, nodeId, displayName);
            return node;
        }

        public void DestroyNode(WorldResourceNode node)
        {
            if (node == null)
                return;
            objects.Remove(node.gameObject);
            UnityEngine.Object.DestroyImmediate(node.gameObject);
        }

        public void MarkDynamicStateDirty()
        {
        }

        public void ResetCandidatesAndReplan()
        {
        }

        public void Dispose()
        {
            foreach (GameObject target in objects.ToArray())
            {
                if (target != null)
                    UnityEngine.Object.DestroyImmediate(target);
            }
            objects.Clear();
        }
    }

    private sealed class FakePersistentIds : IPersistentIdGenerator
    {
        private int next;

        public ItemInstanceId NewItemInstanceId() =>
            (ItemInstanceId)("item-instance:qa:" + ++next);

        public ItemStackId NewItemStackId() =>
            (ItemStackId)("stack:qa:" + ++next);

        public CharacterId NewCharacterId() =>
            (CharacterId)("character:qa:" + ++next);

        public BuildingInstanceId NewBuildingInstanceId() =>
            (BuildingInstanceId)("building:qa:world-resource:" + ++next);

        public WildlifeHabitatPatchId NewWildlifeHabitatPatchId() =>
            (WildlifeHabitatPatchId)("wildlife-habitat:qa:" + ++next);
    }

    private sealed class AlwaysResearched : IWorldResourceResearchPort
    {
        public bool IsCompleted(string researchId) => true;
    }

    private sealed class NoGrandProjectBenefits : IGrandProjectBenefitQuery
    {
        public bool IsCompleted(string projectId) => false;
        public float GetProductionOutputMultiplier(string facilityTag) => 1f;
        public float ContractRewardMultiplier => 1f;
        public float DefensePreparationMultiplier => 1f;
        public int ExpeditionSupplyCapacityBonus => 0;
    }
}
