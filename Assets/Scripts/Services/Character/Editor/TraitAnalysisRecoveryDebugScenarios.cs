#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;

public static class TraitAnalysisRecoveryDebugScenarios
{
    private const string CharacterIdValue = "character:qa:trait-analysis-owner";
    private const string CharacterName = "연화";
    private const string FacilityInstanceId =
        "building:qa:trait-analyzer";
    private const string FacilityDefinitionId = "building:8879";
    private const string KitItemId = "medical:trait-analysis-kit";
    private const string KitStackId = "item-stack:qa:trait-analysis-kit";
    private const long KitMassGrams = 250L;

    [MenuItem("Dungeon Story/QA/V27/Trait Analysis Recovery")]
    public static void RunFromMenu() => RunBatch();

    public static void RunBatch()
    {
        Require(RunAll(true), "Trait-analysis recovery scenarios failed.");
    }

    public static bool RunAll(bool logSuccess = false)
    {
        VerifyProductionEntryAndDuplicateNoOp();
        VerifyRevealPublicationFailureRecovers();
        VerifyOutcomePrepareFailureRoundTripsAndRejectsTamper();
        VerifyOutcomeCommitFailureRecovers();
        VerifyAcknowledgementFailureRecoversWithoutDuplicateOutcome();
        VerifyUnknownAcknowledgementRestoresAndReplaysExactFact();
        VerifyMissingObserverReconnectsAndRecovers();
        if (logSuccess)
        {
            Debug.Log(
                "TRAIT_ANALYSIS_RECOVERY=PASS entry=1 reveal-resume=1 "
                + "outcome-prepare-save-resume=1 outcome-commit-resume=1 "
                + "ack-resume=1 unknown-ack=1 observer-rebind=1 "
                + "duplicate-consume=0 duplicate-outcome=0 tamper-rejections=9");
        }
        return true;
    }

    public static void RunObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", "Artifacts", "QA"))
            + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output)
            || !Path.IsPathRooted(output)
            || !Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Trait-analysis observer checks require new project-local batch evidence.");
        }
        string probe = File.ReadAllText(Path.Combine(
            Directory.GetParent(Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
            && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true")
            && probe.Contains("inJob=true"),
            "Trait-analysis observer checks require the noninteractive desktop probe.");
        Require(RunAll(), "Trait-analysis focused observer checks failed.");
        Debug.Log("B80_TRAIT_ANALYSIS_OBSERVER_FOCUSED=PASS");
    }

    public static void RunG14FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_G14_TRAIT_ANALYSIS_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "G14 verification requires explicit isolated batch mode.");
        }

        string reportPath = RequireG14ReportPath();
        G14Report report = new();
        try
        {
            VerifyProductionEntryAndDuplicateNoOp();
            report.preAnalysisHidden = true;
            report.kitConsumedOnce = true;
            report.latentIdsMatched = true;
            report.alreadyAnalyzedNoOp = true;
            report.parentOutcomeExact = true;

            VerifyRevealPublicationFailureRecovers();
            report.consumeThenRevealFailureRecovered = true;

            VerifyOutcomePrepareFailureRoundTripsAndRejectsTamper();
            report.saveRestoreResume = true;
            report.tamperRejected = true;

            VerifyOutcomeCommitFailureRecovers();
            report.outcomeCommitResume = true;

            VerifyAcknowledgementFailureRecoversWithoutDuplicateOutcome();
            report.ackResumeNoDuplicate = true;
            report.passed = true;
            WriteG14Report(reportPath, report);
            Debug.Log(
                "[AuditRemediation][G14] PASS kit=1; hidden-before=1; "
                + "latent-match=1; save-resume=1; duplicate=0");
        }
        catch (Exception exception)
        {
            report.failure = exception.GetType().Name
                + ": "
                + exception.Message;
            if (!File.Exists(reportPath))
                WriteG14Report(reportPath, report);
            Debug.LogException(exception);
            throw;
        }
    }

    private static void VerifyProductionEntryAndDuplicateNoOp()
    {
        using Fixture fixture = new("run:qa:trait-analysis-normal");
        RequireHidden(fixture.Narrative, fixture.CharacterId);

        Require(
            fixture.Command.TryAnalyze(
                fixture.CharacterId,
                out IReadOnlyList<string> revealed,
                out DomainFailure failure),
            "Trait-analysis production entry failed: " + failure);
        Require(revealed.SequenceEqual(
                new[] { fixture.LatentTraitId },
                StringComparer.Ordinal),
            "Trait-analysis returned a different latent trait set.");
        RequireTerminal(fixture);
        RequireOutcome(fixture, expectedCount: 1);

        int commits = fixture.Physical.CommitCalls;
        Require(
            !fixture.Command.TryAnalyze(
                fixture.CharacterId,
                out _,
                out _),
            "Already analyzed character accepted a second analysis.");
        Require(
            fixture.Physical.CommitCalls == commits,
            "Already analyzed no-op consumed another kit.");
        RequireOutcome(fixture, expectedCount: 1);
    }

    private static void VerifyRevealPublicationFailureRecovers()
    {
        using Fixture fixture = new("run:qa:trait-analysis-reveal-fault");
        fixture.Persistence.FailOnPublishCall = 2;

        Require(
            !fixture.Command.TryAnalyze(
                fixture.CharacterId,
                out _,
                out DomainFailure failure)
            && HasFailureDetail(
                failure,
                "trait-analysis-reveal-publication-failed"),
            "Injected reveal publication fault was not surfaced: " + failure);
        Require(
            fixture.Repository.GetEditorTestQuantity(fixture.StackId) == 0
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1
            && fixture.Physical.CommitCalls == 1,
            "Reveal fault did not retain one durable physical receipt.");
        RequireHidden(fixture.Narrative, fixture.CharacterId);
        RequirePhase(
            fixture.Narrative,
            TraitAnalysisCommitPhase.IntentRecorded);

        fixture.Persistence.FailOnPublishCall = 0;
        Require(
            fixture.Command.TryRecoverPending(out failure),
            "Reveal publication recovery failed: " + failure);
        RequireTerminal(fixture);
        RequireOutcome(fixture, expectedCount: 1);
    }

    private static void VerifyOutcomePrepareFailureRoundTripsAndRejectsTamper()
    {
        using Fixture fixture = new("run:qa:trait-analysis-prepare-fault");
        fixture.Outcomes.FailNextReserve = true;

        Require(
            !fixture.Command.TryAnalyze(
                fixture.CharacterId,
                out _,
                out DomainFailure failure)
            && HasFailureDetail(
                failure,
                "trait-analysis-observation-reserve-failed"),
            "Injected outcome prepare fault was not surfaced: " + failure);
        RequireVisible(fixture.Narrative, fixture.CharacterId, fixture.LatentTraitId);
        RequirePhase(
            fixture.Narrative,
            TraitAnalysisCommitPhase.RevealPublished);
        Require(
            fixture.Repository.GetEditorTestQuantity(fixture.StackId) == 0
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1
            && string.IsNullOrEmpty(fixture.Narrative.Capture()
                .pendingTraitAnalysis.publicationKey)
            && fixture.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(),
                OutcomeFilter.All).Items.Count == 0,
            "Outcome prepare fault changed the physical or ledger terminal state.");

        CharacterNarrativeWorldSaveData saved = fixture.Narrative.Capture();
        VerifyTamperRejections(fixture.Narrative, saved, fixture.CharacterId);

        CharacterNarrativeRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            fixture.Catalog);
        restored.PublishRestore(restored.PrepareRestore(Clone(saved)));
        FaultingNarrativePersistence persistence = new(restored);
        TraitAnalysisCommandRuntime command = fixture.CreateCommand(
            restored,
            persistence);
        Require(
            command.TryRecoverPending(out failure),
            "Saved RevealPublished operation did not resume: " + failure);
        RequireTerminal(fixture, restored);
        RequireOutcome(fixture, expectedCount: 1);
        Require(
            command.TryRecoverPending(out failure),
            "Terminal replay was not an idempotent no-op: " + failure);
        RequireOutcome(fixture, expectedCount: 1);
    }

    private static void VerifyOutcomeCommitFailureRecovers()
    {
        using Fixture fixture = new("run:qa:trait-analysis-commit-fault");
        fixture.Outcomes.RejectNextPublish = true;

        Require(
            !fixture.Command.TryAnalyze(
                fixture.CharacterId,
                out _,
                out DomainFailure failure)
            && HasFailureDetail(
                failure,
                "trait-analysis-observation-incomplete"),
            "Injected outcome commit fault was not surfaced: " + failure);
        RequirePhase(
            fixture.Narrative,
            TraitAnalysisCommitPhase.RevealPublished);
        RequireVisible(fixture.Narrative, fixture.CharacterId, fixture.LatentTraitId);
        TraitAnalysisCommitSaveData rejected = fixture.Narrative.Capture()
            .pendingTraitAnalysis;
        Require(
            fixture.Repository.GetEditorPendingBatchDispositionCount() == 1
            && !string.IsNullOrWhiteSpace(rejected.publicationKey)
            && string.IsNullOrEmpty(rejected.publicationId)
            && string.IsNullOrEmpty(rejected.publicationFingerprint)
            && fixture.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(),
                OutcomeFilter.All).Items.Count == 0,
            "Outcome commit fault acknowledged the physical receipt early.");
        TraitAnalysisFactPreflight.ValidateObservation(
            rejected,
            fixture.Ledger.CaptureGameplayOutcomes());

        Require(
            fixture.Command.TryRecoverPending(out failure),
            "Outcome commit recovery failed: " + failure);
        RequireTerminal(fixture);
        RequireOutcome(fixture, expectedCount: 1);
    }

    private static void VerifyAcknowledgementFailureRecoversWithoutDuplicateOutcome()
    {
        using Fixture fixture = new("run:qa:trait-analysis-ack-fault");
        fixture.Physical.FailNextAcknowledge = true;

        Require(
            !fixture.Command.TryAnalyze(
                fixture.CharacterId,
                out _,
                out DomainFailure failure)
            && HasFailureDetail(
                failure,
                "trait-analysis-physical-acknowledge-failed"),
            "Injected acknowledgement fault was not surfaced: " + failure);
        RequirePhase(
            fixture.Narrative,
            TraitAnalysisCommitPhase.OutcomePublished);
        RequireVisible(fixture.Narrative, fixture.CharacterId, fixture.LatentTraitId);
        RequireOutcome(fixture, expectedCount: 1);
        int reserveCalls = fixture.Outcomes.ReserveCalls;
        int publishCalls = fixture.Outcomes.PublishCalls;
        int replayCalls = fixture.Outcomes.ReplayBatchCalls;
        Require(
            fixture.Repository.GetEditorPendingBatchDispositionCount() == 1,
            "Acknowledgement fault removed the pending physical receipt.");

        Require(
            fixture.Command.TryRecoverPending(out failure),
            "Acknowledgement recovery failed: " + failure);
        RequireTerminal(fixture);
        Require(
            fixture.Outcomes.ReserveCalls == reserveCalls
            && fixture.Outcomes.PublishCalls == publishCalls
            && fixture.Outcomes.ReplayBatchCalls == replayCalls,
            "OutcomePublished recovery observed the parent fact twice.");
        RequireOutcome(fixture, expectedCount: 1);
    }

    private static void VerifyUnknownAcknowledgementRestoresAndReplaysExactFact()
    {
        using Fixture fixture = new("run:qa:trait-analysis-unknown-ack");
        fixture.Outcomes.HideNextCommittedAcknowledgement = true;

        Require(
            !fixture.Command.TryAnalyze(
                fixture.CharacterId,
                out _,
                out DomainFailure failure)
            && HasFailureDetail(
                failure,
                "trait-analysis-observation-incomplete"),
            "Lost trait-analysis acknowledgement was not retained: " + failure);
        RequireVisible(
            fixture.Narrative,
            fixture.CharacterId,
            fixture.LatentTraitId);
        CharacterNarrativeWorldSaveData saved = fixture.Narrative.Capture();
        TraitAnalysisCommitSaveData pending = saved.pendingTraitAnalysis;
        Require(
            (TraitAnalysisCommitPhase)pending.phase
                == TraitAnalysisCommitPhase.RevealPublished
            && !string.IsNullOrWhiteSpace(pending.publicationKey)
            && string.IsNullOrEmpty(pending.publicationId)
            && string.IsNullOrEmpty(pending.publicationFingerprint)
            && fixture.Physical.CommitCalls == 1
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1,
            "Lost acknowledgement did not preserve the exact RevealPublished owner.");
        RequireOutcome(fixture, expectedCount: 1);

        GameplayOutcomeLedgerSaveData ledger =
            fixture.Ledger.CaptureGameplayOutcomes();
        TraitAnalysisFactPreflight.ValidateObservation(pending, ledger);
        VerifyObservationJoinTamperRejections(pending, ledger);

        CharacterNarrativeWorldSaveData jsonRestored = Clone(saved);
        CharacterNarrativeRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            fixture.Catalog);
        restored.PublishRestore(restored.PrepareRestore(jsonRestored));
        FaultingNarrativePersistence persistence = new(restored);
        TraitAnalysisCommandRuntime command = fixture.CreateCommand(
            restored,
            persistence);
        Require(
            command.TryRecoverPending(out failure),
            "Lost trait-analysis acknowledgement did not replay: " + failure);
        RequireTerminal(fixture, restored);
        RequireOutcome(fixture, expectedCount: 1);
        Require(
            fixture.Physical.CommitCalls == 1
            && fixture.Outcomes.PublishCalls == 1
            && fixture.Outcomes.ReplayBatchCalls == 1,
            "Trait-analysis replay repeated a physical or observer effect.");
    }

    private static void VerifyMissingObserverReconnectsAndRecovers()
    {
        using Fixture fixture = new("run:qa:trait-analysis-observer-rebind");
        fixture.DisconnectObserver();

        Require(
            !fixture.Command.TryAnalyze(
                fixture.CharacterId,
                out _,
                out DomainFailure failure)
            && HasFailureDetail(
                failure,
                "trait-analysis-observation-reserve-failed"),
            "Missing trait-analysis observer was not rejected: " + failure);
        RequirePhase(
            fixture.Narrative,
            TraitAnalysisCommitPhase.RevealPublished);
        RequireVisible(
            fixture.Narrative,
            fixture.CharacterId,
            fixture.LatentTraitId);
        Require(
            fixture.Ledger.GetGlobal(
                OutcomeCursor.FirstPage(),
                OutcomeFilter.All).Items.Count == 0
            && fixture.Physical.CommitCalls == 1
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1,
            "Missing observer changed the terminal ledger or physical owner.");

        fixture.ConnectObserver();
        Require(
            fixture.Command.TryRecoverPending(out failure),
            "Rebound trait-analysis observer did not recover: " + failure);
        RequireTerminal(fixture);
        RequireOutcome(fixture, expectedCount: 1);
    }

    private static void VerifyObservationJoinTamperRejections(
        TraitAnalysisCommitSaveData source,
        GameplayOutcomeLedgerSaveData ledger)
    {
        TraitAnalysisCommitSaveData key = source.Clone();
        key.publicationKey += ":altered";
        RequireObservationJoinRejected(
            key,
            ledger,
            "A changed trait-analysis publication key was accepted.");

        TraitAnalysisCommitSaveData proof = source.Clone();
        proof.publicationId = "outcome:altered";
        proof.publicationFingerprint = new string('f', 64);
        RequireObservationJoinRejected(
            proof,
            ledger,
            "A changed trait-analysis publication proof was accepted.");

        TraitAnalysisCommitSaveData day = source.Clone();
        day.absoluteDay++;
        RequireObservationJoinRejected(
            day,
            ledger,
            "A changed trait-analysis day was accepted.");

        TraitAnalysisCommitSaveData name = source.Clone();
        name.characterDisplayName += " altered";
        RequireObservationJoinRejected(
            name,
            ledger,
            "A changed trait-analysis display name was accepted.");
    }

    private static void RequireObservationJoinRejected(
        TraitAnalysisCommitSaveData pending,
        GameplayOutcomeLedgerSaveData ledger,
        string message)
    {
        RequireThrows<Exception>(
            () => TraitAnalysisFactPreflight.ValidateObservation(
                pending,
                ledger),
            message);
    }

    private static void VerifyTamperRejections(
        CharacterNarrativeRuntime narrative,
        CharacterNarrativeWorldSaveData valid,
        CharacterId characterId)
    {
        CharacterNarrativeWorldSaveData legacyVersion = Clone(valid);
        legacyVersion.version = CharacterNarrativeWorldSaveData.CurrentVersion - 1;
        RequireThrows<Exception>(
            () => narrative.PrepareRestore(legacyVersion),
            "A legacy trait-analysis narrative schema was accepted.");

        CharacterNarrativeWorldSaveData missingObservationMetadata =
            Clone(valid);
        missingObservationMetadata.pendingTraitAnalysis.publicationKey = null;
        RequireThrows<Exception>(
            () => narrative.PrepareRestore(missingObservationMetadata),
            "Trait-analysis restore accepted missing observation metadata.");

        CharacterNarrativeWorldSaveData latentMismatch = Clone(valid);
        latentMismatch.pendingTraitAnalysis.latentTraitIds[0] =
            "trait:qa:forged-latent";
        RequireThrows<Exception>(
            () => narrative.PrepareRestore(latentMismatch),
            "A forged pending latent trait ID was accepted.");

        CharacterNarrativeWorldSaveData hiddenReveal = Clone(valid);
        hiddenReveal.characters.Single(value => string.Equals(
            value.characterId,
            characterId.Value,
            StringComparison.Ordinal)).heritableTraitsAnalyzed = false;
        RequireThrows<Exception>(
            () => narrative.PrepareRestore(hiddenReveal),
            "RevealPublished was accepted while the trait remained hidden.");

        CharacterNarrativeWorldSaveData missingMass = Clone(valid);
        missingMass.pendingTraitAnalysis.physicalInputMassGrams = 0L;
        RequireThrows<Exception>(
            () => narrative.PrepareRestore(missingMass),
            "RevealPublished accepted missing physical mass evidence.");
    }

    private static void RequireTerminal(
        Fixture fixture,
        CharacterNarrativeRuntime narrative = null)
    {
        CharacterNarrativeRuntime source = narrative ?? fixture.Narrative;
        RequireVisible(source, fixture.CharacterId, fixture.LatentTraitId);
        CharacterNarrativeWorldSaveData saved = source.Capture();
        Require(
            (TraitAnalysisCommitPhase)saved.pendingTraitAnalysis.phase
                == TraitAnalysisCommitPhase.None
            && saved.nextTraitAnalysisOperationSequence == 1
            && fixture.Repository.GetEditorTestQuantity(fixture.StackId) == 0
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && fixture.Physical.CommitCalls == 1,
            "Trait-analysis terminal state was not exact-once.");
    }

    private static void RequireOutcome(Fixture fixture, int expectedCount)
    {
        GameplayOutcomeQueryPage global = fixture.Ledger.GetGlobal(
            OutcomeCursor.FirstPage(),
            OutcomeFilter.All);
        Require(
            global.Items.Count == expectedCount,
            $"Expected {expectedCount} trait-analysis outcomes, got "
            + global.Items.Count + ".");
        GameplayResultKey key = new(
            HeritableTraitAnalysisOutcomeIds.ProducerId,
            new GameplayOperationId(TraitAnalysisCommandRuntime.FormatOperationId(
                fixture.CharacterId,
                0)),
            1L,
            0);
        if (!fixture.Ledger.TryGetResultIdentity(
                key,
                out GameplayOutcomeReplayIdentity identity)
            || !fixture.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact))
        {
            throw new InvalidOperationException(
                "Trait-analysis parent outcome was not durably queryable.");
        }
        string exactJson = JsonUtility.ToJson(exact, true);
        Require(
            exact.producerId == HeritableTraitAnalysisOutcomeIds.ProducerId
            && exact.outcomeTypeId
                == HeritableTraitAnalysisOutcomeIds.Completed.Value,
            "Trait-analysis parent identity mismatch: " + exactJson);
        Require(
            exact.participants.Count == 1
            && exact.participants[0].entityId == fixture.CharacterId.Value
            && exact.participants[0].displayText == CharacterName,
            "Trait-analysis real target mismatch: " + exactJson);
        Require(
            exact.metrics.Count == 4
            && exact.metrics[3].metricId
                == HeritableTraitAnalysisOutcomeIds.PhysicalInputMass.Value
            && exact.metrics[3].unitId
                == HeritableTraitAnalysisOutcomeIds.GramUnit.Value
            && exact.metrics[3].value == KitMassGrams,
            "Trait-analysis physical metric mismatch: " + exactJson);
        Require(
            exact.facts.Single(value => value.factId
                == HeritableTraitAnalysisOutcomeIds.SourceStackIds.Value).value
                == fixture.StackId
            && exact.facts.Single(value => value.factId
                == HeritableTraitAnalysisOutcomeIds.LatentTraitIds.Value).value
                == fixture.LatentTraitId,
            "Trait-analysis exact fact mismatch: " + exactJson);
        string physicalCanonical = string.Join(
            ";",
            "physical-v1",
            exact.facts.Single(value => value.factId
                == HeritableTraitAnalysisOutcomeIds.PhysicalCommitId.Value).value,
            exact.facts.Single(value => value.factId
                == HeritableTraitAnalysisOutcomeIds.KitItemId.Value).value,
            fixture.StackId,
            KitMassGrams.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Require(
            exact.anchors.Count == 1
            && exact.anchors[0].anchorId ==
                NarrativeInferenceHash.ComputeSha256Utf8(physicalCanonical),
            "Trait-analysis evidence anchor mismatch: " + exactJson);
    }

    private static void RequireHidden(
        CharacterNarrativeRuntime narrative,
        CharacterId characterId)
    {
        Require(
            narrative.TryGet(characterId, out CharacterNarrativeSnapshot snapshot)
            && !snapshot.HeritableTraitsAnalyzed
            && snapshot.VisibleLatentHeritableTraitIds.Count == 0,
            "Latent hereditary trait was visible before analysis.");
    }

    private static void RequireVisible(
        CharacterNarrativeRuntime narrative,
        CharacterId characterId,
        string latentTraitId)
    {
        Require(
            narrative.TryGet(characterId, out CharacterNarrativeSnapshot snapshot)
            && snapshot.HeritableTraitsAnalyzed
            && snapshot.VisibleLatentHeritableTraitIds.SequenceEqual(
                new[] { latentTraitId },
                StringComparer.Ordinal),
            "Exact latent hereditary trait was not visible after analysis.");
    }

    private static void RequirePhase(
        CharacterNarrativeRuntime narrative,
        TraitAnalysisCommitPhase expected)
    {
        Require(
            (TraitAnalysisCommitPhase)narrative.Capture()
                .pendingTraitAnalysis.phase == expected,
            $"Expected trait-analysis phase {expected}.");
    }

    private static CharacterNarrativeWorldSaveData Clone(
        CharacterNarrativeWorldSaveData source) =>
        JsonUtility.FromJson<CharacterNarrativeWorldSaveData>(
            JsonUtility.ToJson(source));

    private static bool HasFailureDetail(
        DomainFailure failure,
        string expected)
    {
        foreach (string parameter in failure.Parameters)
        {
            if (string.Equals(parameter, expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static void RequireThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string RequireG14ReportPath()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "G14 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "report.json");
        if (File.Exists(path))
            throw new InvalidOperationException("G14 report already exists: " + path);
        return path;
    }

    private static void WriteG14Report(string path, G14Report report)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(JsonUtility.ToJson(report, prettyPrint: true));
    }

    [Serializable]
    private sealed class G14Report
    {
        public string rowId = "G14";
        public bool passed;
        public string failure = string.Empty;
        public bool preAnalysisHidden;
        public bool kitConsumedOnce;
        public bool latentIdsMatched;
        public bool alreadyAnalyzedNoOp;
        public bool parentOutcomeExact;
        public bool consumeThenRevealFailureRecovered;
        public bool saveRestoreResume;
        public bool tamperRejected;
        public bool outcomeCommitResume;
        public bool ackResumeNoDuplicate;
        public bool oldSaveMigrationAdded;
        public bool fullAuditExecuted;
        public bool humanApprovalRecorded;
        public bool trainingExecuted;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject actorObject;
        private readonly CharacterSO actorData;
        private readonly GameObject facilityObject;
        private readonly FixedCharacterWorld characterWorld;
        private readonly FixedFacilityQuery facilityQuery;
        private readonly FixedStockQuery stock;
        private readonly FixedCalendar calendar = new(17);
        private IDisposable observerConnection;

        internal Fixture(string runId)
        {
            ResourceGameContentCatalog content = new(
                new UnityGameContentRootLoader());
            Catalog = new CharacterNarrativeCatalog(content);
            SpeciesCultureDefinitionSO culture = Catalog.Cultures[0];
            LatentTraitId = Catalog.HeritableTraits[0].traitId;
            CharacterId = new CharacterId(CharacterIdValue);
            Narrative = new CharacterNarrativeRuntime(
                new DungeonRuntimeAggregateRootStore(),
                Catalog);
            Narrative.Register(
                CharacterId,
                new CharacterSpeciesId(culture.defaultSpeciesId),
                Array.Empty<string>(),
                new[] { LatentTraitId });
            Persistence = new FaultingNarrativePersistence(Narrative);

            actorData = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    CharacterName,
                    culture.defaultSpeciesId);
            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "Trait Analysis Subject");
            CharacterAiEditorTestDependencies.Inject(actorObject);
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(CharacterId);
            actor.Initialization(actorData);
            characterWorld = new FixedCharacterWorld(actor);

            facilityObject = new GameObject("유전 특성 분석기");
            BuildableObject facility =
                facilityObject.AddComponent<BuildableObject>();
            facility.RestorePersistentIdentity(
                new BuildingInstanceId(FacilityInstanceId));
            facilityQuery = new FixedFacilityQuery(facility);

            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            StackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                KitItemId,
                1,
                WorldItemStackState.FacilityBuffer,
                position: new Vector2Int(3, 4),
                destinationId: FacilityInstanceId);
            stock = new FixedStockQuery(new WorldItemStackSnapshot
            {
                StackId = StackId,
                ItemId = KitItemId,
                Quantity = 1,
                State = WorldItemStackState.FacilityBuffer,
                DestinationId = FacilityInstanceId,
                ReservedByPersistentId = string.Empty
            });
            PhysicalItemBatchDispositionService physical = new(
                Repository,
                new FixedMassQuery(KitMassGrams),
                EditorNullItemMarkerPresenter.Instance, clock: new DungeonStory.Foundation.UnityUiClock());
            Physical = new FaultingDisposition(physical);

            Evolution = new EvolutionGameplayOutcomeEditorFixture(runId);
            Ledger = Evolution.Ledger;
            Facts = new DomainFactDispatcher();
            Outcomes = new FaultingFactPublisher(Facts);
            ConnectObserver();
            Command = CreateCommand(Narrative, Persistence);
        }

        internal CharacterNarrativeCatalog Catalog { get; }
        internal CharacterNarrativeRuntime Narrative { get; }
        internal FaultingNarrativePersistence Persistence { get; }
        internal WorldItemRepository Repository { get; }
        internal FaultingDisposition Physical { get; }
        internal DomainFactDispatcher Facts { get; }
        internal FaultingFactPublisher Outcomes { get; }
        internal EvolutionGameplayOutcomeEditorFixture Evolution { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal TraitAnalysisCommandRuntime Command { get; }
        internal CharacterId CharacterId { get; }
        internal string LatentTraitId { get; }
        internal string StackId { get; }

        internal TraitAnalysisCommandRuntime CreateCommand(
            CharacterNarrativeRuntime query,
            ICharacterNarrativePersistence persistence) => new(
            query,
            persistence,
            facilityQuery,
            stock,
            Physical,
            Outcomes,
            Outcomes,
            calendar,
            characterWorld);

        internal void ConnectObserver()
        {
            if (observerConnection != null)
                throw new InvalidOperationException(
                    "Trait-analysis observer is already connected.");
            observerConnection = HeritableTraitsAnalyzedFactMapping
                .CreateBinding(
                    Evolution.Registry,
                    Evolution.Ledger,
                    Evolution.Recorder,
                    new GameplayDomainFactCommitGroup(
                        Evolution.Recorder,
                        Evolution.Ledger))
                .Connect(Facts);
        }

        internal void DisconnectObserver()
        {
            observerConnection?.Dispose();
            observerConnection = null;
        }

        public void Dispose()
        {
            DisconnectObserver();
            Facts.Dispose();
            Evolution.Dispose();
            if (facilityObject != null)
                UnityEngine.Object.DestroyImmediate(facilityObject);
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
        }
    }

    private sealed class FaultingNarrativePersistence :
        ICharacterNarrativePersistence
    {
        private readonly CharacterNarrativeRuntime inner;

        internal FaultingNarrativePersistence(CharacterNarrativeRuntime inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal int FailOnPublishCall { get; set; }
        internal int PublishCalls { get; private set; }

        public CharacterNarrativeWorldSaveData Capture() => inner.Capture();
        public CharacterNarrativeAggregateState PrepareRestore(
            CharacterNarrativeWorldSaveData data) => inner.PrepareRestore(data);
        public void PublishRestore(CharacterNarrativeAggregateState candidate)
        {
            PublishCalls++;
            if (FailOnPublishCall == PublishCalls)
                throw new InvalidOperationException("injected-narrative-publish-fault");
            inner.PublishRestore(candidate);
        }
    }

    private sealed class FaultingFactPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        internal FaultingFactPublisher(
            DomainFactDispatcher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool FailNextReserve { get; set; }
        internal bool RejectNextPublish { get; set; }
        internal bool HideNextCommittedAcknowledgement { get; set; }
        internal int ReserveCalls { get; private set; }
        internal int PublishCalls { get; private set; }
        internal int ReplayBatchCalls { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure)
        {
            ReserveCalls++;
            if (FailNextReserve)
            {
                FailNextReserve = false;
                lease = default;
                failure = "injected-reserve-fault";
                return false;
            }
            return inner.TryReserve(proposed, out lease, out failure);
        }

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            PublishCalls++;
            if (RejectNextPublish)
            {
                RejectNextPublish = false;
                return DomainFactDelivery.Reject(
                    "injected-publication-rejection");
            }
            DomainFactDelivery delivery = inner.Publish(lease, actual);
            if (HideNextCommittedAcknowledgement)
            {
                HideNextCommittedAcknowledgement = false;
                return DomainFactDelivery.Uncertain(
                    "injected-acknowledgement-loss-after-real-commit");
            }
            return delivery;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(
                facts,
                results,
                out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayBatchCalls++;
            return inner.ReplayBatch(facts, results, out failure);
        }
    }

    private sealed class FaultingDisposition :
        IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;

        internal FaultingDisposition(
            IPhysicalItemBatchDispositionService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool FailNextAcknowledge { get; set; }
        internal int CommitCalls { get; private set; }
        internal int AcknowledgeCalls { get; private set; }

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommit(
            inputs,
            kind,
            operationId,
            reasonCode,
            out receipt,
            out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            CommitCalls++;
            return inner.TryCommitPending(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);
        }

        public bool Acknowledge(string commitId, out string failureReason)
        {
            AcknowledgeCalls++;
            if (FailNextAcknowledge)
            {
                FailNextAcknowledge = false;
                failureReason = "injected-acknowledgement-fault";
                return false;
            }
            return inner.Acknowledge(commitId, out failureReason);
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);
    }

    private sealed class FixedFacilityQuery : IFacilityCapabilityQuery
    {
        private readonly BuildableObject facility;

        internal FixedFacilityQuery(BuildableObject facility) =>
            this.facility = facility;

        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") =>
            capability == FacilityCapabilityKind.Medical
                && string.Equals(
                    buildingDefinitionId,
                    FacilityDefinitionId,
                    StringComparison.Ordinal)
                ? new[] { facility }
                : Array.Empty<BuildableObject>();

        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) =>
            Array.Empty<BuildableObject>();
    }

    private sealed class FixedStockQuery : IStockQuery
    {
        private readonly IReadOnlyList<WorldItemStackSnapshot> stacks;

        internal FixedStockQuery(params WorldItemStackSnapshot[] stacks) =>
            this.stacks = stacks ?? Array.Empty<WorldItemStackSnapshot>();

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => stacks;
        public int GetGlobalQuantity(string itemDefinitionId) => stacks
            .Where(value => string.Equals(
                value.ItemId,
                itemDefinitionId,
                StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            string itemDefinitionId) => 0;
        public int GetWarehouseQuantity(
            BuildingInstanceId warehouseId,
            StockCategory category) => 0;
        public int GetWarehouseTotal(BuildingInstanceId warehouseId) => 0;
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly PhysicalMassGrams unitMass;

        internal FixedMassQuery(long grams) =>
            unitMass = new PhysicalMassGrams(grams);

        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            unitMass;
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => unitMass;
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => unitMass;
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            unitMass.Multiply(lot.Quantity);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => unitMass.Multiply(quantity);
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery
    {
        private readonly CharacterActor[] actors;

        internal FixedCharacterWorld(params CharacterActor[] actors) =>
            this.actors = actors ?? Array.Empty<CharacterActor>();

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        internal FixedCalendar(int day) => Day = day;

        public int Day { get; private set; }
        public int Hour { get; private set; } = 9;
        public int Year => 1;
        public int DayOfYear => Day;
        public Season Season => default;
        public int DayOfSeason => Day;
        public long AbsoluteHour => checked((long)Day * 24L + Hour);
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning => true;
        public CalendarDateTime Current => default;
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) => default;
        public void Start() { }
        public void SetDateTime(int day, int hour)
        {
            Day = Math.Max(1, day);
            Hour = Math.Clamp(hour, 0, 23);
        }
    }
}
#endif
