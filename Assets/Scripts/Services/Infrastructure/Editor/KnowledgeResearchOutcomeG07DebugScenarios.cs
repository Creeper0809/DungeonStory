using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

public static partial class KnowledgeResearchOutcomeDebugScenarios
{
    public static void RunG07FromBatch()
    {
        try
        {
            G07ScenarioReport report = RunG07();
            WriteG07Report(report);
            Debug.Log("[AuditRemediation][G07] PASS " + report.operationId);
        }
        catch (Exception exception)
        {
            WriteG07Report(new G07ScenarioReport
            {
                passed = false,
                failure = exception.GetType().Name + ": " + exception.Message,
                fullAuditClosure = false,
                humanApprovalClaimed = false,
                trainingEligible = false
            });
            Debug.LogException(exception);
            throw;
        }
    }

    private static G07ScenarioReport RunG07()
    {
        using Fixture fixture = new(
            KnowledgeResidueUse.CodexAnalysis,
            ledgerRunId: "run:u0-g07-knowledge-completion",
            queueThroughRuntime: true);

        GameplayOutcomeLedgerSaveData queuedLedger =
            fixture.Ledger.CaptureGameplayOutcomes();
        Require(
            fixture.Task != null
            && fixture.Task.use == KnowledgeResidueUse.CodexAnalysis
            && fixture.Task.taskId == Fixture.TaskId
            && fixture.Runtime.Tasks.Count == 1
            && fixture.Runtime.NextTaskSequence == 2
            && queuedLedger.exactOutcomes.Count == 0
            && queuedLedger.outbox.Count == 0
            && fixture.CodexRewardLineCount == 0,
            "actual queue must not publish a completion, Codex reward, or outcome");

        fixture.Codex.ImportReferenceData();
        fixture.Events.Publish(new FacilityVisitEvent(
            (CharacterActor)null,
            fixture.Facility));
        _ = fixture.Codex.GetEntries(CodexEntryCategory.Facility);
        _ = fixture.Codex.HasInformation(
            CodexEntryCategory.Invasion,
            KnowledgeCompletionOutcomeIds.CodexMemoryResidueEntryId,
            fixture.CodexReward);
        GameplayOutcomeLedgerSaveData passiveLedger =
            fixture.Ledger.CaptureGameplayOutcomes();
        Require(
            fixture.Task != null
            && passiveLedger.exactOutcomes.Count == 0
            && passiveLedger.outbox.Count == 0
            && fixture.CodexRewardLineCount == 0,
            "observation, query, and reference import must not become completion experience");

        BlueprintResearchWorkResult completed = fixture.Runtime.ApplyApprovedWork(
            null,
            fixture.Facility,
            24f);
        GameplayOutcomeLedgerSaveData completedLedger =
            fixture.Ledger.CaptureGameplayOutcomes();
        GameplayOutcomeSnapshot completion = completedLedger.exactOutcomes.Single(
            value => string.Equals(
                value.outcomeTypeId,
                CompletionOutcomeType,
                StringComparison.Ordinal));
        Require(
            completed.Success
            && completed.Completed
            && fixture.Task == null
            && !fixture.HasSource
            && fixture.CodexRewardLineCount == 1
            && completedLedger.exactOutcomes.Count(value => string.Equals(
                value.outcomeTypeId,
                CompletionOutcomeType,
                StringComparison.Ordinal)) == 1
            && completedLedger.exactOutcomes.Count(value => string.Equals(
                value.outcomeTypeId,
                ProgressOutcomeType,
                StringComparison.Ordinal)) == 1
            && completedLedger.outbox.Count == 0,
            "actual completion must publish one reward and one exact completion");

        int exactCountBeforeLegacyReplay = completedLedger.exactOutcomes.Count;
        Require(
            fixture.Codex.TryRecordMemoryResidueClue(
                fixture.CodexReward,
                out string legacyMessage)
            && fixture.CodexRewardLineCount == 1
            && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count
                == exactCountBeforeLegacyReplay,
            "legacy Codex replay must remain idempotent and must not add an exact outcome");

        GameplayEntityId codexEntry = new(
            KnowledgeCompletionOutcomeIds.CodexEntryKind,
            KnowledgeCompletionOutcomeIds.CodexMemoryResidueEntryId);
        GameplayOutcomeNarrativeEvidenceSource exactSource =
            new GameplayOutcomeNarrativeEvidenceQuery(fixture.Ledger)
                .GetForEntity(codexEntry, 8, 0f, includeCompacted: true)
                .Single(value => string.Equals(
                    value.OutcomeTypeId,
                    CompletionOutcomeType,
                    StringComparison.Ordinal));
        GameplayOutcomeEvidenceBindingSnapshot exactBinding =
            GameplayOutcomeEvidenceFormulaProjection.Capture(
                new[] { exactSource }).Single();
        CharacterNarrativeFact legacy = new()
        {
            factId = "legacy:g07:knowledge-completion",
            lastEventContext = new GameplayNarrativeEventContext
            {
                eventInstanceId = exactBinding.operationId
            }
        };
        bool commonOrigin = GameplayOutcomeEvidenceFormulaProjection
            .HasCommonOrigin(exactBinding, legacy);
        int uniqueExperienceSourceCount = 1 + (commonOrigin ? 0 : 1);
        Require(
            commonOrigin && uniqueExperienceSourceCount == 1,
            "Codex projection, legacy fact, and exact completion must resolve to one source");

        return new G07ScenarioReport
        {
            passed = true,
            queuedTaskCount = 1,
            queuedCompletionOutcomeCount = 0,
            passiveObservationImportOutcomeCount = 0,
            completedCodexRewardLineCount = fixture.CodexRewardLineCount,
            exactCompletionOutcomeCount = 1,
            legacyReplayAddedCodexLines = 0,
            legacyReplayAddedExactOutcomes = 0,
            exactLegacyCommonOrigin = commonOrigin,
            uniqueExperienceSourceCount = uniqueExperienceSourceCount,
            taskId = Fixture.TaskId,
            operationId = exactBinding.operationId,
            outcomeRunId = completion.runId,
            outcomeSequence = completion.sequence,
            legacyMessage = legacyMessage,
            fullAuditClosure = false,
            humanApprovalClaimed = false,
            trainingEligible = false
        };
    }

    private static void WriteG07Report(G07ScenarioReport report)
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "G07 verification requires its isolated output directory.");
        }

        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "report.json");
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented));
    }

    private sealed class G07ScenarioReport
    {
        public string rowId = "G07";
        public bool passed;
        public string failure = string.Empty;
        public int queuedTaskCount;
        public int queuedCompletionOutcomeCount;
        public int passiveObservationImportOutcomeCount;
        public int completedCodexRewardLineCount;
        public int exactCompletionOutcomeCount;
        public int legacyReplayAddedCodexLines;
        public int legacyReplayAddedExactOutcomes;
        public bool exactLegacyCommonOrigin;
        public int uniqueExperienceSourceCount;
        public string taskId = string.Empty;
        public string operationId = string.Empty;
        public string outcomeRunId = string.Empty;
        public long outcomeSequence;
        public string legacyMessage = string.Empty;
        public bool fullAuditClosure;
        public bool humanApprovalClaimed;
        public bool trainingEligible;
    }
}
