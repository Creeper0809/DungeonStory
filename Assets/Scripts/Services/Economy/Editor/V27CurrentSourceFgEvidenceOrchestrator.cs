#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Durable, source-bound execution owner for the Batch F/G evidence portfolio.
/// Every expensive verifier keeps ownership of its request and report. This
/// coordinator only serializes those requests, validates their terminal
/// evidence, and runs the two aggregate writers twice to prove no-op identity.
/// </summary>
[InitializeOnLoad]
public static class V27CurrentSourceFgEvidenceOrchestrator
{
    public const string RequestPath =
        "Temp/v27-current-source-fg-evidence.request";
    public const string StatePath =
        "Temp/v27-current-source-fg-evidence.state.json";
    public const string ReportPath =
        "Artifacts/QA/v27-current-source-fg-evidence-orchestration.txt";
    public const string ContractReportPath =
        "Artifacts/QA/v27-current-source-fg-orchestration-contract.txt";

    private const int SchemaVersion = 2;
    private const string RequestToken = "run";
    private const int FinalPairedSeedCount = 64;
    private const int PairedWindowsPerSeed = 16;
    private const int PairedFloorRowsPerSeed = 20;
    private const int PairedFaultArmsPerSeed = 2;
    private const int FinalPairedWindowCount =
        FinalPairedSeedCount * PairedWindowsPerSeed;
    private const int FinalPairedFloorRowCount =
        FinalPairedSeedCount * PairedFloorRowsPerSeed;
    private const int FinalPairedFaultArmCount =
        FinalPairedSeedCount * PairedFaultArmsPerSeed;
    private const string FinalPairedEvidenceStepId = "paired-clutter-final";
    private const string ConsoleJournalPath =
        "Temp/v27-current-source-fg-evidence.console.tsv";
    private const long RedispatchGraceTicks = TimeSpan.TicksPerSecond * 5L;
    private const int MaximumDispatchAttempts = 2;

    private const string DispatchPhase = "dispatch";
    private const string AwaitReportPhase = "await-report";
    private const string BatchFFirstReadyPhase = "batch-f-first-ready";
    private const string BatchFFirstRunningPhase = "batch-f-first-running";
    private const string BatchFSecondReadyPhase = "batch-f-second-ready";
    private const string BatchFSecondRunningPhase = "batch-f-second-running";
    private const string BatchGFirstReadyPhase = "batch-g-first-ready";
    private const string BatchGFirstRunningPhase = "batch-g-first-running";
    private const string BatchGSecondReadyPhase = "batch-g-second-ready";
    private const string BatchGSecondRunningPhase = "batch-g-second-running";
    private const string CompletePhase = "complete";
    private const string FailedPhase = "failed";

    private static readonly UTF8Encoding Utf8NoBom = new(false, true);
    private static readonly EvidenceStep[] Steps =
    {
        new(
            "prepared-output-p17",
            PhysicalItemLogisticsPlayModeVerifier
                .P17PreparedOutputWarehouseReportPath,
            PhysicalItemLogisticsPlayModeVerifier
                .RequestPreparedOutputWarehouseRunFromMenu,
            () => PhysicalItemLogisticsPlayModeVerifier.HasPendingDurableRun,
            ReportConsoleContract.PhysicalLogistics),
        new(
            "prepared-output-synthetic-canary",
            PhysicalItemLogisticsPlayModeVerifier
                .PreparedOutputWarehouseReportPath,
            SyntheticPreparedOutputCanaryAssetTransaction
                .QueueRunFromEditorCommand,
            () => SyntheticPreparedOutputCanaryAssetTransaction
                .HasPendingDurableRun,
            ReportConsoleContract.PhysicalLogistics),
        new(
            "prepared-output-sawmill-transport",
            PhysicalItemLogisticsPlayModeVerifier
                .SawmillPreparedOutputWarehouseReportPath,
            PhysicalItemLogisticsPlayModeVerifier
                .RequestSawmillPreparedOutputWarehouseRunFromMenu,
            () => PhysicalItemLogisticsPlayModeVerifier.HasPendingDurableRun,
            ReportConsoleContract.PhysicalLogistics),
        new(
            "production-input-mass",
            PhysicalItemLogisticsPlayModeVerifier.ProductionInputMassReportPath,
            PhysicalItemLogisticsPlayModeVerifier
                .RequestProductionInputMassRunFromMenu,
            () => PhysicalItemLogisticsPlayModeVerifier.HasPendingDurableRun,
            ReportConsoleContract.PhysicalLogistics),
        new(
            "prepared-output-destructive-drain",
            PhysicalItemLogisticsPlayModeVerifier
                .DestructiveDrainPreparedOutputReportPath,
            PhysicalItemLogisticsPlayModeVerifier
                .RequestDestructiveDrainPreparedOutputRunFromMenu,
            () => PhysicalItemLogisticsPlayModeVerifier.HasPendingDurableRun,
            ReportConsoleContract.PhysicalLogistics),
        new(
            "character-ai-cross-action-fault",
            CharacterAiCrossActionFaultPlayModeVerifier.ReportPath,
            CharacterAiCrossActionFaultPlayModeVerifier
                .QueueRunFromEditorCommand,
            () => CharacterAiCrossActionFaultPlayModeVerifier
                .HasPendingDurableRun,
            ReportConsoleContract.JournalOnly),
        new(
            "ability-haul-lifecycle-recovery",
            AbilityHaulLifecycleRecoveryPlayModeVerifier.ReportPath,
            AbilityHaulLifecycleRecoveryPlayModeVerifier
                .QueueRunFromEditorCommand,
            () => AbilityHaulLifecycleRecoveryPlayModeVerifier
                .HasPendingDurableRun,
            ReportConsoleContract.JournalOnly),
        new(
            "remaining-focused-faults",
            V27ExhaustiveClosureDenominatorDebugScenarios
                .RemainingFaultEvidenceReportPath,
            V27ExhaustiveClosureDenominatorDebugScenarios
                .QueueRemainingFaultEvidenceFromEditorCommand,
            () => V27ExhaustiveClosureDenominatorDebugScenarios
                .HasPendingDurableRun,
            ReportConsoleContract.JournalOnly),
        new(
            FinalPairedEvidenceStepId,
            V27PairedClutterPlayModeVerifier.ReportPath,
            () => V27PairedClutterPlayModeVerifier
                .QueueRunFromEditorCommand(FinalPairedSeedCount, 1),
            () => V27PairedClutterPlayModeVerifier.HasPendingDurableRun,
            ReportConsoleContract.PairedClutter)
    };

    private static bool captureConsole;
    private static string captureOwner = string.Empty;
    private static string infrastructureFailure = string.Empty;

    static V27CurrentSourceFgEvidenceOrchestrator()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLogMessageReceived;
        Application.logMessageReceived += OnLogMessageReceived;
        TryRestoreConsoleCapture();
    }

    [MenuItem("DungeonStory/V27/Physical Mass/Queue Current-Source F-G Evidence")]
    public static void QueueRunFromEditorCommand()
    {
        if (File.Exists(RequestPath))
            throw new InvalidOperationException(
                "A current-source F/G orchestration request is already queued.");
        if (File.Exists(StatePath))
        {
            OrchestrationState existing = ReadState();
            if (!IsTerminal(existing.phase))
            {
                throw new InvalidOperationException(
                    "A current-source F/G orchestration run is already active: "
                    + existing.phase + ".");
            }
        }

        Directory.CreateDirectory("Temp");
        File.Delete(ReportPath);
        WriteAtomicText(RequestPath, RequestToken);
    }

    [MenuItem("DungeonStory/V27/Physical Mass/Verify F-G Orchestration Contract")]
    public static void RunContractFocusedFromMenu()
    {
        RequireOfficialScene(
            V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest());
        Require(Steps.Length == 9,
            "The F/G orchestration evidence-owner denominator drifted from 9.");
        Require(Steps.Select(value => value.Id)
                .Distinct(StringComparer.Ordinal).Count() == Steps.Length,
            "The F/G orchestration contains duplicate evidence-owner IDs.");
        Require(Steps.Select(value => value.ReportPath)
                .Distinct(StringComparer.Ordinal).Count() == Steps.Length,
            "The F/G orchestration contains duplicate terminal report paths.");
        EvidenceStep[] pairedSteps = Steps.Where(value =>
                value.ConsoleContract == ReportConsoleContract.PairedClutter)
            .ToArray();
        Require(FinalPairedSeedCount == 64
                && pairedSteps.Length == 1
                && string.Equals(
                    pairedSteps[0].Id,
                    FinalPairedEvidenceStepId,
                    StringComparison.Ordinal),
            "The F/G orchestration final paired evidence contract drifted from 64 seeds.");
        VerifyFinalPairedEvidenceContractFocused();
        Require(V27DomainClusterClosureDebugScenarios.CaptureRowIds().Count == 6,
            "The Batch F aggregate denominator drifted from 6.");
        Require(V27ExhaustiveClosureDenominatorDebugScenarios
                .CaptureFaultRowIds().Count == 19,
            "The Batch G aggregate denominator drifted from 19.");

        OrchestrationState state = new()
        {
            schemaVersion = SchemaVersion,
            phase = DispatchPhase,
            stepIndex = 0,
            dispatchAttempts = 0,
            transitionUtcTicks = 1L,
            allScriptsDigest = new string('a', 64),
            gameplaySceneSha256 = new string('b', 64),
            evidence = new List<EvidenceRecord>(),
            failure = string.Empty
        };
        string first = ComputeStateFingerprint(state);
        string second = ComputeStateFingerprint(state);
        Require(string.Equals(first, second, StringComparison.Ordinal),
            "The F/G orchestration state fingerprint is nondeterministic.");
        state.fingerprint = first;
        OrchestrationState roundTrip = JsonUtility.FromJson<OrchestrationState>(
            JsonUtility.ToJson(state));
        Require(roundTrip != null
                && string.Equals(
                    roundTrip.fingerprint,
                    ComputeStateFingerprint(roundTrip),
                    StringComparison.Ordinal),
            "The F/G orchestration state did not survive its JSON boundary.");
        state.stepIndex = 1;
        Require(!string.Equals(
                first,
                ComputeStateFingerprint(state),
                StringComparison.Ordinal),
            "The F/G orchestration state fingerprint ignored a step mutation.");

        ArtifactStamp left = new()
        {
            path = "qa",
            sha256 = new string('c', 64),
            length = 10L,
            lastWriteUtcTicks = 20L
        };
        ArtifactStamp right = new()
        {
            path = left.path,
            sha256 = left.sha256,
            length = left.length,
            lastWriteUtcTicks = left.lastWriteUtcTicks
        };
        Require(left.EqualsStamp(right),
            "Equal aggregate artifact stamps were rejected.");
        right.lastWriteUtcTicks++;
        Require(!left.EqualsStamp(right),
            "The aggregate no-op gate ignored mtime churn.");

        string report =
            "RESULT=PASS\n"
            + "schema=v27-current-source-fg-orchestration-contract@2\n"
            + "evidenceOwners=9/9\n"
            + "batchF=6/6\n"
            + "batchG=19/19\n"
            + "pairedSeeds=" + FinalPairedSeedCount + "\n"
            + "pairedWindows=" + FinalPairedWindowCount + "\n"
            + "pairedFloorRows=" + FinalPairedFloorRowCount + "\n"
            + "pairedFaultArms=" + FinalPairedFaultArmCount + "\n"
            + "pairedPreviousSampleRejected=PASS\n"
            + "pairedExactMarkerTamperRejected=PASS\n"
            + "atomicStateFingerprint=PASS\n"
            + "tamperRejection=PASS\n"
            + "byteLengthMtimeNoOpContract=PASS\n"
            + "currentSourceDigest="
            + V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest() + "\n"
            + "gameplaySceneSha256="
            + V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest() + "\n";
        V27BalanceArtifactWriter.WriteIfDifferent(
            ContractReportPath,
            stream =>
            {
                byte[] bytes = Utf8NoBom.GetBytes(report);
                stream.Write(bytes, 0, bytes.Length);
            });
        Debug.Log("V27_CURRENT_SOURCE_FG_ORCHESTRATION_CONTRACT=PASS");
    }

    internal static bool HasActiveRun
    {
        get
        {
            if (!File.Exists(StatePath))
                return File.Exists(RequestPath);
            OrchestrationState state = ReadState();
            return !IsTerminal(state.phase) || File.Exists(RequestPath);
        }
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        OrchestrationState state = null;
        try
        {
            if (File.Exists(RequestPath))
            {
                if (!CanStartNewRun())
                    return;
                state = StartNewRun();
            }
            else if (File.Exists(StatePath))
            {
                state = ReadState();
            }

            if (state == null || IsTerminal(state.phase))
            {
                SetConsoleCapture(false, string.Empty);
                return;
            }

            ValidateAuthority(state);
            switch (state.phase)
            {
                case DispatchPhase:
                    DispatchCurrentStep(state);
                    break;
                case AwaitReportPhase:
                    PollCurrentStep(state);
                    break;
                case BatchFFirstReadyPhase:
                case BatchFFirstRunningPhase:
                    RunAggregateFirst(state, true);
                    break;
                case BatchFSecondReadyPhase:
                case BatchFSecondRunningPhase:
                    RunAggregateSecond(state, true);
                    break;
                case BatchGFirstReadyPhase:
                case BatchGFirstRunningPhase:
                    RunAggregateFirst(state, false);
                    break;
                case BatchGSecondReadyPhase:
                case BatchGSecondRunningPhase:
                    RunAggregateSecond(state, false);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Unknown current-source F/G orchestration phase: "
                        + state.phase);
            }
        }
        catch (Exception exception)
        {
            if (state != null && !IsTerminal(state.phase))
                Fail(state, exception.Message);
            else if (!string.Equals(
                         infrastructureFailure,
                         exception.ToString(),
                         StringComparison.Ordinal))
            {
                infrastructureFailure = exception.ToString();
                Debug.LogError(
                    "V27 current-source F/G orchestration infrastructure failure: "
                    + exception);
            }
        }
    }

    private static bool CanStartNewRun()
    {
        if (File.Exists(StatePath))
        {
            OrchestrationState existing = ReadState();
            if (!IsTerminal(existing.phase))
                throw new InvalidOperationException(
                    "A non-terminal F/G orchestration state already exists.");
        }
        return !PhysicalItemLogisticsPlayModeVerifier.HasPendingDurableRun
            && !SyntheticPreparedOutputCanaryAssetTransaction.HasPendingDurableRun
            && !CharacterAiCrossActionFaultPlayModeVerifier.HasPendingDurableRun
            && !AbilityHaulLifecycleRecoveryPlayModeVerifier.HasPendingDurableRun
            && !V27ExhaustiveClosureDenominatorDebugScenarios.HasPendingDurableRun
            && !V27PairedClutterPlayModeVerifier.HasPendingDurableRun;
    }

    private static OrchestrationState StartNewRun()
    {
        string request = File.ReadAllText(RequestPath).Trim();
        if (!string.Equals(request, RequestToken, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "The current-source F/G request token is invalid.");

        string source = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        string scene = V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        RequireOfficialScene(scene);
        File.Delete(ReportPath);
        File.Delete(ConsoleJournalPath);
        OrchestrationState state = new()
        {
            schemaVersion = SchemaVersion,
            phase = DispatchPhase,
            stepIndex = 0,
            dispatchAttempts = 0,
            transitionUtcTicks = DateTime.UtcNow.Ticks,
            allScriptsDigest = source,
            gameplaySceneSha256 = scene,
            evidence = new List<EvidenceRecord>(),
            failure = string.Empty
        };
        WriteState(state);
        File.Delete(RequestPath);
        infrastructureFailure = string.Empty;
        return state;
    }

    private static void DispatchCurrentStep(OrchestrationState state)
    {
        if (state.stepIndex < 0 || state.stepIndex >= Steps.Length)
            throw new InvalidOperationException(
                "The F/G orchestration step index is outside the exact portfolio.");
        EvidenceStep step = Steps[state.stepIndex];
        if (step.HasPending())
            return;

        File.Delete(step.ReportPath);
        File.Delete(ConsoleJournalPath);
        state.phase = AwaitReportPhase;
        state.dispatchAttempts++;
        state.transitionUtcTicks = DateTime.UtcNow.Ticks;
        WriteState(state);
        SetConsoleCapture(true, step.Id);
        step.Queue();
    }

    private static void PollCurrentStep(OrchestrationState state)
    {
        EvidenceStep step = Steps[state.stepIndex];
        SetConsoleCapture(true, step.Id);
        if (string.Equals(step.Id, FinalPairedEvidenceStepId, StringComparison.Ordinal)
            && V27PairedClutterPlayModeVerifier.HasDurableInterruption)
        {
            throw new InvalidOperationException(
                "The final paired-clutter verifier was interrupted by an assembly reload.");
        }
        if (!File.Exists(step.ReportPath))
        {
            if (step.HasPending())
                return;
            if (DateTime.UtcNow.Ticks - state.transitionUtcTicks
                < RedispatchGraceTicks)
            {
                return;
            }
            if (HasConsoleIssues())
                throw new InvalidOperationException(
                    "Verifier terminated without a report and emitted console issues: "
                    + step.Id + ".");
            if (state.dispatchAttempts >= MaximumDispatchAttempts)
                throw new InvalidOperationException(
                    "Verifier terminated without a terminal report after durable retry: "
                    + step.Id + ".");

            state.phase = DispatchPhase;
            state.transitionUtcTicks = DateTime.UtcNow.Ticks;
            WriteState(state);
            SetConsoleCapture(false, string.Empty);
            return;
        }
        if (step.HasPending())
            return;

        ArtifactStamp stamp = ValidateTerminalReport(
            step.ReportPath,
            state,
            step.ConsoleContract);
        RequireConsoleJournalClean(step.Id);
        state.evidence.Add(new EvidenceRecord
        {
            stepId = step.Id,
            reportPath = step.ReportPath,
            reportSha256 = stamp.sha256,
            reportLength = stamp.length
        });
        state.stepIndex++;
        state.dispatchAttempts = 0;
        state.transitionUtcTicks = DateTime.UtcNow.Ticks;
        state.phase = state.stepIndex == Steps.Length
            ? BatchFFirstReadyPhase
            : DispatchPhase;
        File.Delete(ConsoleJournalPath);
        WriteState(state);
        SetConsoleCapture(false, string.Empty);
    }

    private static void RunAggregateFirst(
        OrchestrationState state,
        bool batchF)
    {
        string owner = batchF ? "batch-f-first" : "batch-g-first";
        ValidateEvidencePortfolio(state);
        string runningPhase = batchF
            ? BatchFFirstRunningPhase
            : BatchGFirstRunningPhase;
        if (!string.Equals(state.phase, runningPhase, StringComparison.Ordinal))
        {
            state.phase = runningPhase;
            state.transitionUtcTicks = DateTime.UtcNow.Ticks;
            File.Delete(ConsoleJournalPath);
            WriteState(state);
        }
        SetConsoleCapture(true, owner);

        if (batchF)
            V27DomainClusterClosureDebugScenarios.RunIntegratedFromMenu();
        else
            V27ExhaustiveClosureDenominatorDebugScenarios.RunFromMenu();

        AggregateStamp stamp = ValidateAggregate(state, batchF);
        RequireConsoleJournalClean(owner);
        if (batchF)
        {
            state.batchFFirst = stamp;
            state.phase = BatchFSecondReadyPhase;
        }
        else
        {
            state.batchGFirst = stamp;
            state.phase = BatchGSecondReadyPhase;
        }
        state.transitionUtcTicks = DateTime.UtcNow.Ticks;
        File.Delete(ConsoleJournalPath);
        WriteState(state);
        SetConsoleCapture(false, string.Empty);
    }

    private static void RunAggregateSecond(
        OrchestrationState state,
        bool batchF)
    {
        string owner = batchF ? "batch-f-second" : "batch-g-second";
        ValidateEvidencePortfolio(state);
        AggregateStamp first = batchF ? state.batchFFirst : state.batchGFirst;
        RequireValidAggregateStamp(first, batchF ? "Batch F" : "Batch G");
        string runningPhase = batchF
            ? BatchFSecondRunningPhase
            : BatchGSecondRunningPhase;
        if (!string.Equals(state.phase, runningPhase, StringComparison.Ordinal))
        {
            state.phase = runningPhase;
            state.transitionUtcTicks = DateTime.UtcNow.Ticks;
            File.Delete(ConsoleJournalPath);
            WriteState(state);
        }
        SetConsoleCapture(true, owner);

        if (batchF)
            V27DomainClusterClosureDebugScenarios.RunIntegratedFromMenu();
        else
            V27ExhaustiveClosureDenominatorDebugScenarios.RunFromMenu();

        AggregateStamp second = ValidateAggregate(state, batchF);
        RequireConsoleJournalClean(owner);
        if (!first.ByteAndMtimeEquals(second))
            throw new InvalidOperationException(
                (batchF ? "Batch F" : "Batch G")
                + " second aggregate capture changed bytes, length, or mtime.");

        File.Delete(ConsoleJournalPath);
        SetConsoleCapture(false, string.Empty);
        if (batchF)
        {
            state.phase = BatchGFirstReadyPhase;
            state.transitionUtcTicks = DateTime.UtcNow.Ticks;
            WriteState(state);
            return;
        }

        state.batchGSecond = second;
        Complete(state);
    }

    private static AggregateStamp ValidateAggregate(
        OrchestrationState state,
        bool batchF)
    {
        string csvPath = batchF
            ? V27DomainClusterClosureDebugScenarios.CsvPath
            : V27ExhaustiveClosureDenominatorDebugScenarios.FaultCsvPath;
        string reportPath = batchF
            ? V27DomainClusterClosureDebugScenarios.ReportPath
            : V27ExhaustiveClosureDenominatorDebugScenarios.FaultReportPath;
        ArtifactStamp report = ValidateTerminalReport(
            reportPath,
            state,
            ReportConsoleContract.JournalOnly);
        string reportText = File.ReadAllText(reportPath)
            .Replace("\r\n", "\n");
        if (batchF)
        {
            RequireExactHeaderValue(reportText, "structural", "6/6", reportPath);
            RequireExactHeaderValue(reportText, "closed", "6/6", reportPath);
            RequireExactHeaderValue(reportText, "open", "0", reportPath);
        }
        else
        {
            RequireExactHeaderValue(reportText, "closed", "19", reportPath);
            RequireExactHeaderValue(reportText, "total", "19", reportPath);
            RequireExactHeaderValue(reportText, "open", "0", reportPath);
            RequireExactReportValue(
                reportText, "structurallyVerified", "19", reportPath);
            RequireExactReportValue(
                reportText, "structuralOpen", "0", reportPath);
            RequireExactReportValue(
                reportText, "unityExecutionRequired", "0", reportPath);
        }
        ValidateAggregateCsv(csvPath, batchF);
        ArtifactStamp csv = CaptureStamp(csvPath);
        if (csv.length <= 0L)
            throw new InvalidOperationException(
                (batchF ? "Batch F" : "Batch G")
                + " aggregate CSV is empty.");
        return new AggregateStamp
        {
            csv = csv,
            report = report
        };
    }

    private static void ValidateAggregateCsv(string path, bool batchF)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException("Aggregate CSV is missing: " + path);
        string[] lines = File.ReadAllText(path)
            .Replace("\r\n", "\n")
            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int expectedRows = batchF ? 6 : 19;
        if (lines.Length != expectedRows + 1)
            throw new InvalidOperationException(
                (batchF ? "Batch F" : "Batch G")
                + " aggregate CSV row denominator drifted: "
                + Math.Max(0, lines.Length - 1) + "/" + expectedRows + ".");
        string closureToken = batchF
            ? "PASS:current-source-integrated-fixture"
            : "PASS:current-source-integrated-evidence";
        int closed = lines.Skip(1).Count(value =>
            value.Contains(closureToken, StringComparison.Ordinal));
        if (closed != expectedRows)
            throw new InvalidOperationException(
                (batchF ? "Batch F" : "Batch G")
                + " aggregate CSV closure drifted: "
                + closed + "/" + expectedRows + ".");
    }

    private static ArtifactStamp ValidateTerminalReport(
        string path,
        OrchestrationState state,
        ReportConsoleContract consoleContract)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException(
                "Required terminal evidence is missing: " + path);
        string text = File.ReadAllText(path).Replace("\r\n", "\n");
        if (!string.Equals(
                RequireUniqueReportValue(text, "RESULT", path),
                "PASS",
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Terminal evidence is not PASS: " + path);
        if (!string.Equals(
                RequireUniqueReportValue(
                    text,
                    "currentSourceDigest",
                    path),
                state.allScriptsDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Terminal evidence is stale or mixed-source: " + path);
        }
        if (!string.Equals(
                RequireUniqueReportValue(
                    text,
                    "gameplaySceneSha256",
                    path),
                state.gameplaySceneSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Terminal evidence has a stale or mixed scene digest: " + path);
        }
        if (consoleContract == ReportConsoleContract.PhysicalLogistics
            && (!string.Equals(
                    RequireUniqueReportValue(text, "capturedErrors", path),
                    "0",
                    StringComparison.Ordinal)
                || !string.Equals(
                    RequireUniqueReportValue(text, "capturedWarnings", path),
                    "0",
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Physical-logistics evidence does not prove Console 0/0: "
                + path);
        }
        if (consoleContract == ReportConsoleContract.PairedClutter)
        {
            if (!string.Equals(
                    RequireUniqueReportValue(text, "consoleIssues", path),
                    "0",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Paired-clutter evidence does not prove Console 0/0: " + path);
            }
            RequireFinalPairedEvidence(text, path);
        }
        return CaptureStamp(path);
    }

    private static void RequireFinalPairedEvidence(string text, string path)
    {
        int newline = text.IndexOf('\n');
        string header = newline < 0 ? text : text.Substring(0, newline);
        RequireExactReportValue(
            header,
            "seeds",
            FinalPairedSeedCount.ToString(CultureInfo.InvariantCulture),
            path + "#header");
        RequireExactReportValue(
            header,
            "windows",
            FinalPairedWindowCount.ToString(CultureInfo.InvariantCulture),
            path + "#header");
        RequireExactReportValue(
            header,
            "floorRows",
            FinalPairedFloorRowCount.ToString(CultureInfo.InvariantCulture),
            path + "#header");
        RequireExactReportValue(header, "failures", "0", path + "#header");
        RequireExactReportValue(header, "consoleIssues", "0", path + "#header");

        string[] exactMarkers =
        {
            "PASS\tPAIRED_RUN_CLEAN_REPEATABILITY_EXACT\tseeds="
                + FinalPairedSeedCount,
            "PASS\tPAIRED_KEYED_PRODUCTION_BURST_APPLIED\tarms="
                + FinalPairedFaultArmCount,
            "PASS\tPAIRED_PRODUCTION_BURST_HAUL_PRIORITY\tarms="
                + FinalPairedFaultArmCount,
            "PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\tarms="
                + FinalPairedFaultArmCount
        };
        foreach (string marker in exactMarkers)
        {
            if (!text.Split('\n').Any(line => string.Equals(
                    line.TrimEnd('\r'),
                    marker,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Final paired-clutter evidence marker is missing: "
                    + marker + ": " + path);
            }
        }
    }

    private static void VerifyFinalPairedEvidenceContractFocused()
    {
        string exact =
            "RESULT=PASS; seeds=" + FinalPairedSeedCount
            + "; windows=" + FinalPairedWindowCount
            + "; floorRows=" + FinalPairedFloorRowCount
            + "; failures=0; consoleIssues=0;\n"
            + "PASS\tPAIRED_RUN_CLEAN_REPEATABILITY_EXACT\tseeds="
            + FinalPairedSeedCount + "\n"
            + "PASS\tPAIRED_KEYED_PRODUCTION_BURST_APPLIED\tarms="
            + FinalPairedFaultArmCount + "\n"
            + "PASS\tPAIRED_PRODUCTION_BURST_HAUL_PRIORITY\tarms="
            + FinalPairedFaultArmCount + "\n"
            + "PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\tarms="
            + FinalPairedFaultArmCount + "\n";
        RequireFinalPairedEvidence(exact, "focused-final-paired");

        int previousSeedCount = FinalPairedSeedCount / 2;
        string stale = exact.Replace(
            "seeds=" + FinalPairedSeedCount,
            "seeds=" + previousSeedCount);
        bool staleRejected = false;
        try
        {
            RequireFinalPairedEvidence(stale, "focused-stale-paired");
        }
        catch (InvalidOperationException)
        {
            staleRejected = true;
        }
        Require(staleRejected,
            "The F/G paired evidence contract accepted the previous sample size.");

        string requiredMarker =
            "PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\tarms="
            + FinalPairedFaultArmCount + "\n";
        string markerTampered = exact.Replace(requiredMarker, string.Empty);
        bool markerTamperRejected = false;
        try
        {
            RequireFinalPairedEvidence(
                markerTampered,
                "focused-marker-tampered-paired");
        }
        catch (InvalidOperationException)
        {
            markerTamperRejected = true;
        }
        Require(markerTamperRejected,
            "The F/G paired evidence contract accepted a missing exact fault marker.");
    }

    private static void RequireExactReportValue(
        string text,
        string key,
        string expected,
        string path)
    {
        string actual = RequireUniqueReportValue(text, key, path);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Terminal evidence field " + key + " is " + actual
                + " instead of " + expected + ": " + path);
        }
    }

    private static void RequireExactHeaderValue(
        string text,
        string key,
        string expected,
        string path)
    {
        int newline = text.IndexOf('\n');
        string header = newline < 0 ? text : text.Substring(0, newline);
        RequireExactReportValue(header, key, expected, path + "#header");
    }

    private static string RequireUniqueReportValue(
        string text,
        string key,
        string path)
    {
        List<string> matches = new();
        foreach (string line in text.Split('\n'))
        {
            foreach (string field in line.Split(';'))
            {
                string candidate = field.Trim();
                int separator = candidate.IndexOf('=');
                if (separator <= 0
                    || !string.Equals(
                        candidate.Substring(0, separator).Trim(),
                        key,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                matches.Add(candidate.Substring(separator + 1).Trim());
            }
        }
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                "Terminal evidence must contain exactly one " + key
                + " field, but found " + matches.Count + ": " + path);
        }
        return matches[0];
    }

    private static void Complete(OrchestrationState state)
    {
        ValidateEvidencePortfolio(state);
        Require(state.evidence != null && state.evidence.Count == Steps.Length,
            "The F/G orchestration cannot complete with an incomplete evidence portfolio.");
        state.phase = CompletePhase;
        state.failure = string.Empty;
        state.transitionUtcTicks = DateTime.UtcNow.Ticks;
        WriteState(state);
        WriteFinalReport(state, passed: true);
        if (WriteFinalReport(state, passed: true))
        {
            File.Delete(ReportPath);
            throw new InvalidOperationException(
                "The F/G orchestration terminal report changed during its "
                + "second deterministic generation.");
        }
        Debug.Log(
            "V27_CURRENT_SOURCE_FG_ORCHESTRATION=PASS; steps=9/9; F=6/6; G=19/19; pairedSeeds="
            + FinalPairedSeedCount + "; noOp=PASS");
    }

    private static void Fail(OrchestrationState state, string reason)
    {
        SetConsoleCapture(false, string.Empty);
        state.phase = FailedPhase;
        state.failure = CanonicalSingleLine(reason);
        state.transitionUtcTicks = DateTime.UtcNow.Ticks;
        WriteFinalReport(state, passed: false);
        WriteState(state);
        Debug.LogError(
            "V27_CURRENT_SOURCE_FG_ORCHESTRATION=FAIL: " + state.failure);
    }

    private static bool WriteFinalReport(
        OrchestrationState state,
        bool passed)
    {
        StringBuilder builder = new();
        builder.Append("RESULT=").Append(passed ? "PASS" : "FAIL").Append('\n')
            .Append("schema=v27-current-source-fg-orchestration@2\n")
            .Append("currentSourceDigest=").Append(state.allScriptsDigest).Append('\n')
            .Append("gameplaySceneSha256=").Append(state.gameplaySceneSha256).Append('\n')
            .Append("evidenceSteps=").Append(state.evidence?.Count ?? 0)
            .Append('/').Append(Steps.Length).Append('\n')
            .Append("pairedSeeds=").Append(FinalPairedSeedCount).Append('\n')
            .Append("pairedWindows=").Append(FinalPairedWindowCount).Append('\n')
            .Append("pairedFloorRows=").Append(FinalPairedFloorRowCount).Append('\n')
            .Append("pairedFaultArms=").Append(FinalPairedFaultArmCount).Append('\n');
        foreach (EvidenceRecord evidence in (state.evidence
                     ?? new List<EvidenceRecord>())
                 .OrderBy(value => value.stepId, StringComparer.Ordinal))
        {
            builder.Append("evidence=").Append(evidence.stepId)
                .Append("; path=").Append(evidence.reportPath)
                .Append("; sha256=").Append(evidence.reportSha256)
                .Append("; bytes=").Append(evidence.reportLength).Append('\n');
        }
        AppendAggregate(builder, "F", state.batchFFirst);
        AppendAggregate(builder, "G", state.batchGSecond);
        if (passed)
        {
            builder.Append("consoleWarnings=0\n")
                .Append("consoleErrors=0\n")
                .Append("aggregateSecondRunByteDiff=0\n")
                .Append("aggregateSecondRunLengthDiff=0\n")
                .Append("aggregateSecondRunMtimeDiff=0\n")
                .Append("orchestrationSecondRunByteDiff=0\n")
                .Append("orchestrationSecondRunLengthDiff=0\n")
                .Append("orchestrationSecondRunMtimeDiff=0\n");
        }
        else
        {
            builder.Append("consoleIssues=").Append(CountConsoleIssues())
                .Append('\n')
                .Append("aggregateSecondRun=NOT_PROVEN\n");
            builder.Append("failure=").Append(state.failure).Append('\n');
        }
        return V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream =>
            {
                byte[] bytes = Utf8NoBom.GetBytes(builder.ToString());
                stream.Write(bytes, 0, bytes.Length);
            });
    }

    private static void AppendAggregate(
        StringBuilder builder,
        string batch,
        AggregateStamp stamp)
    {
        if (stamp?.csv == null || stamp.report == null)
            return;
        builder.Append("batch=").Append(batch)
            .Append("; csvSha256=").Append(stamp.csv.sha256)
            .Append("; reportSha256=").Append(stamp.report.sha256)
            .Append("; noOp=PASS\n");
    }

    private static void ValidateAuthority(OrchestrationState state)
    {
        Require(state.schemaVersion == SchemaVersion,
            "The F/G orchestration state schema is unsupported.");
        string source = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        string scene = V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        RequireOfficialScene(scene);
        if (!string.Equals(
                source,
                state.allScriptsDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "CURRENT_SOURCE_CHANGED_DURING_FG_ORCHESTRATION");
        }
        if (!string.Equals(
                scene,
                state.gameplaySceneSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "GAMEPLAY_SCENE_CHANGED_DURING_FG_ORCHESTRATION");
        }
    }

    private static void ValidateEvidencePortfolio(OrchestrationState state)
    {
        Require(state.evidence != null && state.evidence.Count == Steps.Length,
            "The F/G evidence portfolio is incomplete.");
        for (int index = 0; index < Steps.Length; index++)
        {
            EvidenceStep step = Steps[index];
            EvidenceRecord record = state.evidence[index];
            Require(record != null
                    && string.Equals(record.stepId, step.Id, StringComparison.Ordinal)
                    && string.Equals(
                        record.reportPath,
                        step.ReportPath,
                        StringComparison.Ordinal),
                "The F/G evidence portfolio order or owner identity drifted at "
                + index + ".");
            ArtifactStamp current = ValidateTerminalReport(
                step.ReportPath,
                state,
                step.ConsoleContract);
            Require(string.Equals(
                        record.reportSha256,
                        current.sha256,
                        StringComparison.Ordinal)
                    && record.reportLength == current.length,
                "Terminal evidence changed after acceptance: " + step.Id + ".");
        }
    }

    private static void RequireOfficialScene(string scene)
    {
        if (!string.Equals(
                scene,
                V27CurrentSourceEvidenceDigest.OfficialGameplaySceneSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "OFFICIAL_GAMEPLAY_SCENE_DIGEST_MISMATCH:" + scene);
        }
    }

    private static ArtifactStamp CaptureStamp(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException("Artifact is missing: " + path);
        FileInfo info = new(path);
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        return new ArtifactStamp
        {
            path = path,
            sha256 = Hex(sha.ComputeHash(stream)),
            length = info.Length,
            lastWriteUtcTicks = info.LastWriteTimeUtc.Ticks
        };
    }

    private static void RequireValidAggregateStamp(
        AggregateStamp stamp,
        string owner)
    {
        if (stamp?.csv == null
            || stamp.report == null
            || string.IsNullOrEmpty(stamp.csv.sha256)
            || string.IsNullOrEmpty(stamp.report.sha256))
        {
            throw new InvalidOperationException(
                owner + " first aggregate stamp is missing or corrupt.");
        }
    }

    private static void OnLogMessageReceived(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (!captureConsole
            || type is not (LogType.Warning or LogType.Error
                or LogType.Exception or LogType.Assert))
        {
            return;
        }
        try
        {
            Directory.CreateDirectory("Temp");
            File.AppendAllText(
                ConsoleJournalPath,
                captureOwner + "\t" + type + "\t"
                + CanonicalSingleLine(condition) + "\n",
                Utf8NoBom);
        }
        catch (Exception exception)
        {
            infrastructureFailure =
                "Console journal write failed: " + exception.Message;
        }
    }

    private static bool HasConsoleIssues()
    {
        if (!string.IsNullOrEmpty(infrastructureFailure))
            return true;
        return File.Exists(ConsoleJournalPath)
            && new FileInfo(ConsoleJournalPath).Length > 0L;
    }

    private static int CountConsoleIssues()
    {
        int count = string.IsNullOrEmpty(infrastructureFailure) ? 0 : 1;
        if (!File.Exists(ConsoleJournalPath))
            return count;
        return count + File.ReadAllLines(ConsoleJournalPath)
            .Count(value => !string.IsNullOrWhiteSpace(value));
    }

    private static void RequireConsoleJournalClean(string owner)
    {
        if (!string.IsNullOrEmpty(infrastructureFailure))
            throw new InvalidOperationException(infrastructureFailure);
        if (!HasConsoleIssues())
            return;
        string issues = CanonicalSingleLine(File.ReadAllText(ConsoleJournalPath));
        throw new InvalidOperationException(
            owner + " emitted Unity Console Warning/Error: " + issues);
    }

    private static void SetConsoleCapture(bool enabled, string owner)
    {
        captureConsole = enabled;
        captureOwner = enabled ? owner ?? string.Empty : string.Empty;
    }

    private static void TryRestoreConsoleCapture()
    {
        try
        {
            if (!File.Exists(StatePath))
                return;
            OrchestrationState state = ReadState();
            if (string.Equals(state.phase, AwaitReportPhase, StringComparison.Ordinal)
                && state.stepIndex >= 0
                && state.stepIndex < Steps.Length)
            {
                SetConsoleCapture(true, Steps[state.stepIndex].Id);
            }
            else if (state.phase.EndsWith("-running", StringComparison.Ordinal))
            {
                SetConsoleCapture(true, state.phase);
            }
        }
        catch (Exception exception)
        {
            infrastructureFailure = exception.ToString();
        }
    }

    private static OrchestrationState ReadState()
    {
        string json = File.ReadAllText(StatePath);
        OrchestrationState state = JsonUtility.FromJson<OrchestrationState>(json)
            ?? throw new InvalidOperationException(
                "The F/G orchestration state is empty or malformed.");
        string expected = ComputeStateFingerprint(state);
        if (!string.Equals(
                state.fingerprint,
                expected,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The F/G orchestration state fingerprint is invalid.");
        }
        state.evidence ??= new List<EvidenceRecord>();
        return state;
    }

    private static void WriteState(OrchestrationState state)
    {
        state.fingerprint = ComputeStateFingerprint(state);
        WriteAtomicText(StatePath, JsonUtility.ToJson(state, true) + "\n");
    }

    private static string ComputeStateFingerprint(OrchestrationState state)
    {
        StringBuilder builder = new();
        builder.Append(state.schemaVersion).Append('|')
            .Append(state.phase).Append('|')
            .Append(state.stepIndex).Append('|')
            .Append(state.dispatchAttempts).Append('|')
            .Append(state.transitionUtcTicks).Append('|')
            .Append(state.allScriptsDigest).Append('|')
            .Append(state.gameplaySceneSha256).Append('|')
            .Append(CanonicalSingleLine(state.failure)).Append('\n');
        foreach (EvidenceRecord value in state.evidence
                     ?? new List<EvidenceRecord>())
        {
            builder.Append(value.stepId).Append('|')
                .Append(value.reportPath).Append('|')
                .Append(value.reportSha256).Append('|')
                .Append(value.reportLength).Append('\n');
        }
        AppendFingerprint(builder, state.batchFFirst);
        AppendFingerprint(builder, state.batchGFirst);
        AppendFingerprint(builder, state.batchGSecond);
        using SHA256 sha = SHA256.Create();
        return Hex(sha.ComputeHash(Utf8NoBom.GetBytes(builder.ToString())));
    }

    private static void AppendFingerprint(
        StringBuilder builder,
        AggregateStamp stamp)
    {
        if (stamp == null
            || IsEmptyStamp(stamp.csv) && IsEmptyStamp(stamp.report))
        {
            builder.Append("<null>\n");
            return;
        }
        AppendFingerprint(builder, stamp.csv);
        AppendFingerprint(builder, stamp.report);
    }

    private static bool IsEmptyStamp(ArtifactStamp stamp) =>
        stamp == null
        || (string.IsNullOrEmpty(stamp.path)
            && string.IsNullOrEmpty(stamp.sha256)
            && stamp.length == 0L
            && stamp.lastWriteUtcTicks == 0L);

    private static void AppendFingerprint(
        StringBuilder builder,
        ArtifactStamp stamp)
    {
        if (stamp == null)
        {
            builder.Append("<null>\n");
            return;
        }
        builder.Append(stamp.path).Append('|')
            .Append(stamp.sha256).Append('|')
            .Append(stamp.length).Append('|')
            .Append(stamp.lastWriteUtcTicks).Append('\n');
    }

    private static void WriteAtomicText(string path, string text)
    {
        string directory = Path.GetDirectoryName(path) ?? "Temp";
        Directory.CreateDirectory(directory);
        string temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, Utf8NoBom);
            using (FileStream stream = new(
                       temporary,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }

    private static bool IsTerminal(string phase) =>
        string.Equals(phase, CompletePhase, StringComparison.Ordinal)
        || string.Equals(phase, FailedPhase, StringComparison.Ordinal);

    private static string CanonicalSingleLine(string value) =>
        (value ?? string.Empty)
        .Replace("\r\n", " ")
        .Replace('\r', ' ')
        .Replace('\n', ' ')
        .Trim();

    private static string Hex(byte[] bytes)
    {
        const string alphabet = "0123456789abcdef";
        char[] result = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            result[index * 2] = alphabet[bytes[index] >> 4];
            result[index * 2 + 1] = alphabet[bytes[index] & 0x0f];
        }
        return new string(result);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private enum ReportConsoleContract
    {
        JournalOnly,
        PairedClutter,
        PhysicalLogistics
    }

    private sealed class EvidenceStep
    {
        public EvidenceStep(
            string id,
            string reportPath,
            Action queue,
            Func<bool> hasPending,
            ReportConsoleContract consoleContract)
        {
            Id = id;
            ReportPath = reportPath;
            Queue = queue;
            HasPending = hasPending;
            ConsoleContract = consoleContract;
        }

        public string Id { get; }
        public string ReportPath { get; }
        public Action Queue { get; }
        public Func<bool> HasPending { get; }
        public ReportConsoleContract ConsoleContract { get; }
    }

    [Serializable]
    private sealed class OrchestrationState
    {
        public int schemaVersion;
        public string phase;
        public int stepIndex;
        public int dispatchAttempts;
        public long transitionUtcTicks;
        public string allScriptsDigest;
        public string gameplaySceneSha256;
        public List<EvidenceRecord> evidence;
        public AggregateStamp batchFFirst;
        public AggregateStamp batchGFirst;
        public AggregateStamp batchGSecond;
        public string failure;
        public string fingerprint;
    }

    [Serializable]
    private sealed class EvidenceRecord
    {
        public string stepId;
        public string reportPath;
        public string reportSha256;
        public long reportLength;
    }

    [Serializable]
    private sealed class AggregateStamp
    {
        public ArtifactStamp csv;
        public ArtifactStamp report;

        public bool ByteAndMtimeEquals(AggregateStamp other) =>
            other != null
            && csv != null
            && report != null
            && csv.EqualsStamp(other.csv)
            && report.EqualsStamp(other.report);
    }

    [Serializable]
    private sealed class ArtifactStamp
    {
        public string path;
        public string sha256;
        public long length;
        public long lastWriteUtcTicks;

        public bool EqualsStamp(ArtifactStamp other) =>
            other != null
            && string.Equals(path, other.path, StringComparison.Ordinal)
            && string.Equals(sha256, other.sha256, StringComparison.Ordinal)
            && length == other.length
            && lastWriteUtcTicks == other.lastWriteUtcTicks;
    }
}
#endif
