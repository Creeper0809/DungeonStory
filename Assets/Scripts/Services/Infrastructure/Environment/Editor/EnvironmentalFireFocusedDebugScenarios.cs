#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static class EnvironmentalFireFocusedDebugScenarios
{
    private const string ReportPath =
        "Artifacts/QA/GameplayOutcomeLedgerPhase80FireGroup7-20260919-r6/"
        + "environmental-fire-focused.txt";

    [MenuItem("DungeonStory/QA/Run FIRE-01 Environmental Fire Focused")]
    public static void Run()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        if (File.Exists(ReportPath))
        {
            throw new InvalidOperationException(
                "Environmental-fire focused evidence is immutable and already exists: "
                + ReportPath);
        }
        try
        {
            RunAll();
            File.WriteAllText(
                ReportPath,
                "result=PASS\n"
                + "PASS electrical contact, breaker, generation and stored-power isolation gate\n"
                + "PASS exact ignition replay and 13 WU initial attack\n"
                + "PASS physical water pending receipt, current-format restore and exact acknowledgement\n"
                + "PASS v2 legacy migration without fabricated outcomes and strict malformed-v3 rejection\n"
                + "PASS deterministic direct-neighbour spread, duplicate collapse and non-combustible rejection\n"
                + "PASS process-accident receipt producer request contract, ineligible rejection and cause replay\n"
                + "PASS fire-damage exact replay, conflict rejection, save restore, false/throw retention and retry\n"
                + "scope=isolated production EnvironmentalFireRuntime and process-accident producer with controlled target/access/power/water/command authorities; registered worker selection, hauling and scene overlay require the focused Play witness\n");
            Debug.Log("FIRE01_ENVIRONMENTAL_FIRE=PASS");
        }
        catch (Exception error)
        {
            File.WriteAllText(ReportPath, "result=FAIL\n" + error + "\n");
            Debug.LogException(error);
            throw;
        }
    }

    public static void RunAll()
    {
        VerifyElectricalIsolationAndInitialAttack();
        VerifyWaterReceiptRestoreAndAcknowledgement();
        VerifyLegacyV2MigrationAndNativeValidation();
        VerifyDeterministicAdjacentSpread();
        VerifyProcessAccidentProducerContract();
        VerifyDamageOutcomeReplayRestoreAndFailureRetry();
    }

    private static void VerifyElectricalIsolationAndInitialAttack()
    {
        var fixture = new Fixture(electrical: true, intensity: 0.25f);
        EnvironmentalFireIgnitionResult ignition = fixture.Ignite("cause:electrical");
        Require(ignition.Created, "Electrical ignition was not accepted.");
        Require(fixture.PublishedOutcomeCount == 1,
            "Ignition did not publish exactly one gameplay outcome.");
        Require(
            fixture.Ignite("cause:electrical").Disposition
                == EnvironmentalFireIgnitionDisposition.PreviouslyProcessed,
            "Exact ignition replay was not idempotent.");
        Require(fixture.PublishedOutcomeCount == 1,
            "Ignition replay published a duplicate gameplay outcome.");
        EnvironmentalFireIgnitionResult conflictingIgnition =
            fixture.Runtime.TryIgnite(new EnvironmentalFireIgnitionRequest(
                "cause:electrical",
                EnvironmentalFireIgnitionKind.ElectricalFault,
                "focused-test",
                fixture.Target,
                fixture.IgnitionIntensity,
                "evidence:different",
                targetDisplayName: "전기 설비"));
        Require(
            conflictingIgnition.Disposition
                == EnvironmentalFireIgnitionDisposition.CauseConflict,
            "Same ignition cause with different evidence was not rejected as a conflict.");
        Require(fixture.PublishedOutcomeCount == 1,
            "Conflicting ignition published a gameplay outcome.");

        EnvironmentalFireSuppressionCommand command = fixture.InitialAttack(
            "suppression:electrical",
            ignition.FireId,
            13f);
        Require(
            fixture.Runtime.TryApplySuppression(command).Disposition
                == EnvironmentalFireSuppressionDisposition
                    .ElectricalIsolationRequired,
            "Live electrical contact did not block suppression.");

        fixture.Safety = new EnvironmentalFireElectricalSafetySnapshot(
            fixture.Target,
            hasElectricalContact: false,
            connectionEnabled: false,
            breakerTripped: true,
            hasLiveGeneration: false,
            hasStoredPower: true);
        Require(
            fixture.Runtime.TryApplySuppression(command).Disposition
                == EnvironmentalFireSuppressionDisposition
                    .ElectricalIsolationRequired,
            "Stored power was incorrectly ignored after disconnect.");

        fixture.Safety = new EnvironmentalFireElectricalSafetySnapshot(
            fixture.Target,
            hasElectricalContact: false,
            connectionEnabled: false,
            breakerTripped: true,
            hasLiveGeneration: false,
            hasStoredPower: false);
        EnvironmentalFireSuppressionResult applied =
            fixture.Runtime.TryApplySuppression(command);
        Require(
            applied.Disposition
                == EnvironmentalFireSuppressionDisposition.Extinguished,
            "The authored 13 WU initial attack did not extinguish intensity 0.25.");
        Require(fixture.Runtime.ActiveFires.Count == 0, "Extinguished fire remained active.");
        Require(fixture.PublishedOutcomeCount == 2,
            "Successful suppression did not publish exactly one gameplay outcome.");
        Require(
            fixture.Runtime.TryApplySuppression(command).Disposition
                == EnvironmentalFireSuppressionDisposition.PreviouslyApplied,
            "Committed suppression replay was not idempotent.");
        Require(fixture.PublishedOutcomeCount == 2,
            "Suppression replay published a duplicate gameplay outcome.");
        EnvironmentalFireSuppressionResult conflictingSuppression =
            fixture.Runtime.TryApplySuppression(fixture.InitialAttack(
                "suppression:electrical",
                ignition.FireId,
                12f));
        Require(
            conflictingSuppression.Disposition
                == EnvironmentalFireSuppressionDisposition.OperationConflict,
            "Same suppression operation with different work was not rejected as a conflict.");
        Require(fixture.PublishedOutcomeCount == 2,
            "Conflicting suppression published a gameplay outcome.");
    }

    private static void VerifyWaterReceiptRestoreAndAcknowledgement()
    {
        var fixture = new Fixture(electrical: false, intensity: 0.8f);
        fixture.Water.Acknowledge = false;
        EnvironmentalFireIgnitionResult ignition = fixture.Ignite("cause:process");
        Require(ignition.Created, "Process ignition was not accepted.");

        var command = new EnvironmentalFireSuppressionCommand(
            "suppression:water",
            ignition.FireId,
            "character:worker",
            new Vector2Int(1, 0),
            EnvironmentalFireSuppressionMode.Water,
            2.5f,
            "lease:clean-water",
            2,
            "진압 작업자");
        EnvironmentalFireSuppressionResult applied =
            fixture.Runtime.TryApplySuppression(command);
        Require(
            applied.Disposition
                == EnvironmentalFireSuppressionDisposition
                    .AppliedAwaitingWaterAcknowledgement,
            "Failed acknowledgement did not preserve a pending physical-water receipt.");
        Require(fixture.Water.CommitCount == 1, "Water was not committed exactly once.");

        DungeonEnvironmentalFireSaveData saved = fixture.Runtime.Capture();
        string json = JsonUtility.ToJson(saved);
        var restoredFixture = new Fixture(
            electrical: false,
            intensity: 0.8f,
            fixture.Water,
            outcomes: fixture.Outcomes);
        restoredFixture.Runtime.Restore(restoredFixture.Runtime.PrepareRestore(
            JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(json)));
        restoredFixture.Water.Acknowledge = true;
        IReadOnlyList<EnvironmentalFireSuppressionResult> resumed =
            restoredFixture.Runtime.ResumePendingSuppressions();
        Require(resumed.Count == 1, "Pending acknowledgement was not resumed once.");
        Require(
            restoredFixture.Water.CommitCount == 1,
            "Restore replay recommitted already consumed water.");
        Require(
            restoredFixture.Water.AcknowledgeCount == 2,
            "Physical-water receipt was not acknowledged exactly once after restore.");
    }

    private static void VerifyProcessAccidentProducerContract()
    {
        const string operationId = "work-operation:fire-01-process";
        const string facilityId = "building:fire-01-process";
        const float intensity = 0.4f;
        var command = new RecordingEnvironmentalFireCommand();
        BuildableObject facility = CreateProcessAccidentFacility(
            facilityId,
            BuiltInWorkTypeIds.Craft,
            intensity);
        CharacterActor worker = CreateWorker("character:fire-01-worker");
        try
        {
            var producer = new ProcessAccidentEnvironmentalFireProducer(command);
            var eligible = new ProcessAccidentFireReceipt(
                operationId,
                worker,
                facility,
                BuiltInWorkTypeIds.Craft,
                "body:hand",
                3.5f);

            EnvironmentalFireIgnitionResult result = producer.TryPublish(eligible);
            Require(result.Created, "Eligible process accident was not accepted.");
            Require(
                command.AcceptedRequests.Count == 1,
                "Eligible process accident did not reach the environmental-fire command once.");
            EnvironmentalFireIgnitionRequest request = command.AcceptedRequests[0];
            Require(
                request.Kind == EnvironmentalFireIgnitionKind.ProcessAccident,
                "Process accident used the wrong ignition kind.");
            Require(
                request.CauseId
                == "environmental-fire-process-accident:" + operationId,
                "Process accident used the wrong stable cause ID.");
            Require(
                request.Target.Equals(new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.Building,
                    facilityId)),
                "Process accident targeted a building other than its receipt facility.");
            Require(
                Mathf.Approximately(request.IgnitionIntensity, intensity),
                "Process accident did not preserve its authored ignition intensity.");

            int invocationsBeforeReplay = command.InvocationCount;
            EnvironmentalFireIgnitionResult replay = producer.TryPublish(eligible);
            Require(
                replay.Disposition
                    == EnvironmentalFireIgnitionDisposition.PreviouslyProcessed,
                "Repeated process accident cause was not reported as already processed.");
            Require(
                command.InvocationCount == invocationsBeforeReplay + 1,
                "Process accident replay did not reach the common cause ledger.");
            Require(
                command.AcceptedRequests.Count == 1,
                "Repeated process accident cause created a second ignition.");

            int invocationsBeforeInvalid = command.InvocationCount;
            EnvironmentalFireIgnitionResult ineligible = producer.TryPublish(
                new ProcessAccidentFireReceipt(
                    "work-operation:fire-01-ineligible",
                    worker,
                    facility,
                    BuiltInWorkTypeIds.Repair,
                    "body:hand",
                    3.5f));
            Require(
                ineligible.Disposition
                    == EnvironmentalFireIgnitionDisposition.InvalidRequest,
                "Ineligible process accident was not rejected.");
            Require(
                command.InvocationCount == invocationsBeforeInvalid,
                "Ineligible process accident reached the environmental-fire command.");

            EnvironmentalFireIgnitionResult malformed = producer.TryPublish(default);
            Require(
                malformed.Disposition
                    == EnvironmentalFireIgnitionDisposition.InvalidRequest,
                "Malformed process accident receipt was not rejected.");
            Require(
                command.InvocationCount == invocationsBeforeInvalid,
                "Malformed process accident reached the environmental-fire command.");
        }
        finally
        {
            if (worker != null)
            {
                UnityEngine.Object.DestroyImmediate(worker.gameObject);
            }

            if (facility != null)
            {
                BuildingSO definition = facility.BuildingData;
                UnityEngine.Object.DestroyImmediate(facility.gameObject);
                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }
        }
    }

    private static void VerifyLegacyV2MigrationAndNativeValidation()
    {
        var source = new Fixture(electrical: false, intensity: 0.25f);
        EnvironmentalFireIgnitionRequest ignitionRequest =
            source.CreateIgnitionRequest("cause:v2-active");
        EnvironmentalFireIgnitionResult ignition =
            source.Runtime.TryIgnite(ignitionRequest);
        Require(ignition.Created, "V2 active-fire source ignition failed.");
        EnvironmentalFireSuppressionCommand suppression = source.InitialAttack(
                "suppression:v2-active",
                ignition.FireId,
                1f);
        EnvironmentalFireSuppressionResult suppressionResult =
            source.Runtime.TryApplySuppression(suppression);
        Require(
            suppressionResult.Disposition
                == EnvironmentalFireSuppressionDisposition.Applied,
            "V2 active-fire source suppression did not remain active: "
                + suppressionResult.Disposition);

        DungeonEnvironmentalFireSaveData legacy = Clone(source.Runtime.Capture());
        legacy.version = 2;
        legacy.processedCauses[0].fingerprint = ignitionRequest.LegacyFingerprint;
        legacy.suppressionOperations[0].fingerprint = suppression.LegacyFingerprint;

        var migratedOutcomes = new OutcomeFixture();
        var migrated = new Fixture(
            electrical: false,
            intensity: 0.25f,
            outcomes: migratedOutcomes);
        migrated.Runtime.Restore(migrated.Runtime.PrepareRestore(legacy));
        DungeonEnvironmentalFireSaveData migratedCapture = migrated.Runtime.Capture();
        Require(
            migratedCapture.version == DungeonEnvironmentalFireSaveData.CurrentVersion
                && migratedCapture.activeFires.Count == 1
                && migratedCapture.history.Count == 0
                && migratedCapture.processedCauses.Count == 1
                && migratedCapture.suppressionOperations.Count == 1,
            "V2 active fire/cause/suppression did not migrate to the current schema.");
        Require(
            migratedCapture.processedCauses[0].legacyPreOutcome
                && migratedCapture.processedCauses[0].outcomeRevision == 0
                && !migratedCapture.processedCauses[0].outcomePending
                && migratedCapture.suppressionOperations[0].legacyPreOutcome
                && migratedCapture.suppressionOperations[0].outcomeRevision == 0
                && !migratedCapture.suppressionOperations[0].outcomePending,
            "V2 records were not retained as explicit pre-outcome legacy state.");
        Require(migrated.PublishedOutcomeCount == 0,
            "V2 migration fabricated gameplay outcomes.");
        Require(
            migrated.Runtime.TryIgnite(ignitionRequest).Disposition
                == EnvironmentalFireIgnitionDisposition.PreviouslyProcessed
                && migrated.Runtime.TryApplySuppression(suppression).Disposition
                    == EnvironmentalFireSuppressionDisposition.PreviouslyApplied
                && migrated.PublishedOutcomeCount == 0,
            "V2 replay did not preserve legacy identity without new outcomes.");

        var endedSource = new Fixture(electrical: false, intensity: 0.25f);
        EnvironmentalFireIgnitionRequest endedRequest =
            endedSource.CreateIgnitionRequest("cause:v2-history");
        EnvironmentalFireIgnitionResult endedIgnition =
            endedSource.Runtime.TryIgnite(endedRequest);
        EnvironmentalFireSuppressionCommand endedSuppression =
            endedSource.InitialAttack(
                "suppression:v2-history",
                endedIgnition.FireId,
                13f);
        Require(
            endedSource.Runtime.TryApplySuppression(endedSuppression).Disposition
                == EnvironmentalFireSuppressionDisposition.Extinguished,
            "V2 history source fire was not extinguished.");
        DungeonEnvironmentalFireSaveData legacyHistory =
            Clone(endedSource.Runtime.Capture());
        legacyHistory.version = 2;
        legacyHistory.processedCauses[0].fingerprint = endedRequest.LegacyFingerprint;
        legacyHistory.suppressionOperations[0].fingerprint =
            endedSuppression.LegacyFingerprint;
        var historyOutcomes = new OutcomeFixture();
        var restoredHistory = new Fixture(
            electrical: false,
            intensity: 0.25f,
            outcomes: historyOutcomes);
        restoredHistory.Runtime.Restore(
            restoredHistory.Runtime.PrepareRestore(legacyHistory));
        DungeonEnvironmentalFireSaveData historyCapture =
            restoredHistory.Runtime.Capture();
        Require(
            historyCapture.activeFires.Count == 0
                && historyCapture.history.Count == 1
                && historyCapture.history[0].fire.fireId == endedIgnition.FireId
                && historyOutcomes.Ledger.GetDiagnostics().PublishedCount == 0,
            "V2 ended-fire history did not round-trip without a fabricated ledger record.");

        DungeonEnvironmentalFireSaveData malformedLegacyMarker =
            Clone(source.Runtime.Capture());
        malformedLegacyMarker.processedCauses[0].legacyPreOutcome = true;
        RequireThrows(
            () => source.Runtime.PrepareRestore(malformedLegacyMarker),
            "Native v3 cause accepted a legacy marker with native outcome fields.");

        DungeonEnvironmentalFireSaveData malformedNativeName =
            Clone(source.Runtime.Capture());
        malformedNativeName.processedCauses[0].targetDisplayName = string.Empty;
        RequireThrows(
            () => source.Runtime.PrepareRestore(malformedNativeName),
            "Native v3 cause accepted a missing canonical target display snapshot.");

        DungeonEnvironmentalFireSaveData malformedPending =
            Clone(source.Runtime.Capture());
        malformedPending.suppressionOperations[0].outcomePending = true;
        malformedPending.suppressionOperations[0].phase =
            (int)EnvironmentalFireSuppressionPhase.Cancelled;
        RequireThrows(
            () => source.Runtime.PrepareRestore(malformedPending),
            "Native v3 suppression accepted an illegal pending phase.");
    }

    private static void VerifyDeterministicAdjacentSpread()
    {
        var fixture = new Fixture(
            electrical: false,
            intensity: 0.8f,
            spreadChancePerTick: 1f,
            minimumSpreadIntensity: 0.1f);
        EnvironmentalFireTargetSnapshot combustible = fixture.AddAdjacent(
            "building:spread-combustible",
            combustible: true,
            acceptsSpread: true);
        fixture.AdjacentTargets.Add(combustible);
        fixture.AddAdjacent(
            "building:spread-noncombustible",
            combustible: false,
            acceptsSpread: true);
        fixture.AddAdjacent(
            "building:spread-not-authored",
            combustible: true,
            acceptsSpread: false);

        EnvironmentalFireIgnitionResult ignition = fixture.Ignite(
            "cause:deterministic-spread");
        Require(ignition.Created,
            "The deterministic spread source was not ignited.");
        EnvironmentalFireTickReport first = fixture.Runtime.Advance(5f);
        Require(first.NewFires == 1
            && fixture.Runtime.ActiveFires.Count == 2
            && fixture.Runtime.ActiveFires.Count(value =>
                value.Target.TargetId == combustible.Target.TargetId) == 1,
            "Direct-neighbour spread did not create exactly one authored combustible target.");

        EnvironmentalFireTickReport second = fixture.Runtime.Advance(5f);
        Require(second.NewFires == 0
            && fixture.Runtime.ActiveFires.Count == 2
            && fixture.Runtime.ActiveFires.Select(value => value.Target)
                .Distinct()
                .Count() == 2,
            "Repeated adjacency evaluation created a duplicate fire target.");
    }

    private static void VerifyDamageOutcomeReplayRestoreAndFailureRetry()
    {
        var outcomes = new OutcomeFixture(includeDamage: true);
        var authority = new EnvironmentalFireDamageOutcomeAuthority(
            outcomes.Bridge);
        EnvironmentalFireDamageCommand command = DamageCommand(
            "fire-damage:replay");
        Require(authority.TryBegin(
                command,
                "화재 시험 시설",
                17,
                out _,
                out string beginFailure),
            "Fire-damage prepare failed: " + beginFailure);
        Require(authority.TryCommitApplied(
                command.OperationId,
                7f,
                targetRemainsCombustible: true,
                out EnvironmentalFireDamageResult committed,
                out string commitFailure)
            && committed.Committed
            && Math.Abs(committed.AppliedDamage - 7f) < 0.001f
            && outcomes.Ledger.GetDiagnostics().PublishedCount == 1,
            "Fire-damage outcome did not commit exactly once: " + commitFailure);
        Require(authority.TryBegin(
                command,
                "화재 시험 시설",
                17,
                out EnvironmentalFireDamageOutcomeSaveRecord replay,
                out string replayFailure)
            && replay.phase == EnvironmentalFireDamageOutcomePhase.OutcomeCommitted,
            "Exact fire-damage replay was not recovered: " + replayFailure);
        Require(!authority.TryBegin(
                new EnvironmentalFireDamageCommand(
                    command.OperationId,
                    command.FireId,
                    command.Target,
                    command.Position,
                    command.Intensity,
                    command.RequestedDamage + 1f),
                "화재 시험 시설",
                17,
                out _,
                out string conflictFailure)
            && conflictFailure == "environmental-fire-damage-operation-conflict",
            "Conflicting fire-damage replay was accepted.");

        var restored = new EnvironmentalFireDamageOutcomeAuthority(
            outcomes.Bridge);
        restored.Restore(authority.Capture());
        Require(restored.TryCommitApplied(
                command.OperationId,
                7f,
                targetRemainsCombustible: true,
                out _,
                out string restoreFailure)
            && outcomes.Ledger.GetDiagnostics().PublishedCount == 1,
            "Restored fire-damage replay duplicated or lost its outcome: "
            + restoreFailure);

        VerifyLethalDamageFailureRetry(
            "fire-damage:false-retry",
            ControlledCommitMode.Fail);
        VerifyLethalDamageFailureRetry(
            "fire-damage:throw-retry",
            ControlledCommitMode.Throw);
    }

    private static void VerifyLethalDamageFailureRetry(
        string operationId,
        ControlledCommitMode initialMode)
    {
        var committer = new ControlledEnvironmentCommitter
        {
            Mode = initialMode
        };
        var authority = new EnvironmentalFireDamageOutcomeAuthority(committer);
        EnvironmentalFireDamageCommand command = DamageCommand(operationId);
        Require(authority.TryBegin(
                command,
                "소실 시험 시설",
                18,
                out _,
                out string beginFailure),
            "Lethal fire-damage prepare failed: " + beginFailure);
        Require(authority.TryStageAwaitingWorldRemoval(
                operationId,
                command.RequestedDamage,
                out string stageFailure),
            "Lethal fire-damage staging failed: " + stageFailure);
        Require(!authority.TryFinalizeAfterWorldRemoval(
                operationId,
                out _)
            && authority.TryGet(operationId, out var pending)
            && pending.phase
                == EnvironmentalFireDamageOutcomePhase.AwaitingWorldRemoval,
            "Failed lethal fire-damage commit did not remain retryable.");

        var restored = new EnvironmentalFireDamageOutcomeAuthority(committer);
        restored.Restore(authority.Capture());
        committer.Mode = ControlledCommitMode.Success;
        Require(restored.TryFinalizeAfterWorldRemoval(
                operationId,
                out string retryFailure)
            && restored.TryGet(operationId, out var finalized)
            && finalized.phase
                == EnvironmentalFireDamageOutcomePhase.OutcomeCommitted
            && finalized.outcomeDigest.Length == 64,
            "Restored lethal fire-damage retry did not commit: " + retryFailure);
    }

    private static EnvironmentalFireDamageCommand DamageCommand(
        string operationId) => new(
        operationId,
        "fire:damage-focused",
        new EnvironmentalFireTargetRef(
            EnvironmentalFireTargetKind.Building,
            "building:damage-focused"),
        new Vector2Int(4, 5),
        0.75f,
        9f);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static DungeonEnvironmentalFireSaveData Clone(
        DungeonEnvironmentalFireSaveData value) =>
        JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(
            JsonUtility.ToJson(value));

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

    private static BuildableObject CreateProcessAccidentFacility(
        string persistentId,
        WorkTypeId supportedWork,
        float ignitionIntensity)
    {
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingEnvironmentalFireAbility
        {
            acceptedSources = EnvironmentalFireIgnitionSources.ProcessAccident
        });
        abilities.Add(new BuildingProcessAccidentFireSourceAbility
        {
            workTypeId = supportedWork.Value,
            ignitionIntensity = ignitionIntensity
        });
        BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.objectName = "공정 시험 시설";
        definition.ReplaceAbilities(abilities);

        var host = new GameObject("FIRE-01 Process Accident Facility")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        BuildableObject facility = host.AddComponent<BuildableObject>();
        facility.ConstructBuildableObject(
            new EmptyResearchWorkPort(),
            new EmptyFacilityStateChangePort(),
            new EmptyRoomPolicyPort(),
            combatEquipmentRuntime: null,
            worldRegistry: null,
            worldItemStackRuntime: null,
            abilityRuntimeDispatcher: null,
            gameClock: null,
            paidFacilityContracts: null,
            evolutionState: new FacilityEvolutionStateComponentFactory());
        facility.RestorePersistentIdentity((BuildingInstanceId)persistentId);
        facility.Initialization(definition, Vector2Int.zero);
        return facility;
    }

    private static CharacterActor CreateWorker(string persistentId)
    {
        var host = new GameObject("FIRE-01 Process Accident Worker")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        host.AddComponent<CharacterIdentity>();
        CharacterActor worker = host.AddComponent<CharacterActor>();
        worker.EnsureRuntimeState();
        Require(worker.Identity != null, "Process accident fixture worker has no identity.");
        worker.Identity.SetPersistentId(persistentId);
        return worker;
    }

    private sealed class RecordingEnvironmentalFireCommand :
        IEnvironmentalFireCommand
    {
        private readonly Dictionary<string, string> fingerprintByCause =
            new(StringComparer.Ordinal);

        public List<EnvironmentalFireIgnitionRequest> AcceptedRequests { get; } =
            new();
        public int InvocationCount { get; private set; }

        public EnvironmentalFireIgnitionResult TryIgnite(
            EnvironmentalFireIgnitionRequest request)
        {
            InvocationCount++;
            if (request == null || !request.IsValid)
            {
                return new EnvironmentalFireIgnitionResult(
                    EnvironmentalFireIgnitionDisposition.InvalidRequest,
                    string.Empty,
                    "recording command requires a valid request");
            }

            if (fingerprintByCause.TryGetValue(
                    request.CauseId,
                    out string fingerprint))
            {
                return new EnvironmentalFireIgnitionResult(
                    string.Equals(fingerprint, request.Fingerprint, StringComparison.Ordinal)
                        ? EnvironmentalFireIgnitionDisposition.PreviouslyProcessed
                        : EnvironmentalFireIgnitionDisposition.CauseConflict,
                    string.Empty,
                    "recording command cause ledger result");
            }

            fingerprintByCause.Add(request.CauseId, request.Fingerprint);
            AcceptedRequests.Add(request);
            return new EnvironmentalFireIgnitionResult(
                EnvironmentalFireIgnitionDisposition.Ignited,
                "fire:recording:" + AcceptedRequests.Count,
                string.Empty);
        }

        public EnvironmentalFireSuppressionResult TryApplySuppression(
            EnvironmentalFireSuppressionCommand command) =>
            throw new NotSupportedException("Ignition producer fixture does not suppress fires.");

        public EnvironmentalFireSuppressionResult TryCancelSuppression(
            string operationId) =>
            throw new NotSupportedException("Ignition producer fixture does not cancel suppression.");

        public IReadOnlyList<EnvironmentalFireSuppressionResult>
            ResumePendingSuppressions() =>
            throw new NotSupportedException("Ignition producer fixture does not resume suppression.");
    }

    private sealed class EmptyResearchWorkPort : IBuildingResearchWorkPort
    {
        public bool HasResearchWorkFor(IBuildingWorldEntryPort facility) => false;
    }

    private sealed class EmptyFacilityStateChangePort :
        IBuildingFacilityStateChangePort
    {
        public void MarkDynamicStateDirty()
        {
        }
    }

    private sealed class EmptyRoomPolicyPort : IBuildingRoomPolicyPort
    {
        public bool IsFacilityRoleAvailable(
            IBuildingWorldEntryPort building,
            FacilityRole requestedRole,
            out string rejectReason)
        {
            rejectReason = string.Empty;
            return true;
        }

        public float GetRoomUtilityScore(
            IBuildingWorldEntryPort building,
            FacilityRole role) => 0f;

        public int GetEffectiveCapacity(IBuildingWorldEntryPort building) => 0;

        public BuildingRoomOperationalSnapshot GetOperationalProfile(
            IBuildingWorldEntryPort building) =>
            new(
                Array.Empty<IBuildingWorldEntryPort>(),
                hasRoom: false,
                isUsableRoom: false,
                qualityScore: 0f,
                seatCapacity: 0,
                tableCapacity: 0,
                serviceCapacity: 0,
                retailCategory: StockCategory.General,
                storage: null);
    }

    private sealed class Fixture :
        IEnvironmentalFireTargetQuery,
        IEnvironmentalFireDamageCommand,
        IEnvironmentalFireSuppressionAccessQuery,
        IEnvironmentalFireElectricalSafetyQuery
    {
        private readonly EnvironmentalFireTargetSnapshot snapshot;

        public Fixture(
            bool electrical,
            float intensity,
            RecordingWaterSink water = null,
            float spreadChancePerTick = 0f,
            float minimumSpreadIntensity = 0.55f,
            OutcomeFixture outcomes = null)
        {
            Outcomes = outcomes ?? new OutcomeFixture();
            Target = new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.Building,
                electrical ? "building:electrical" : "building:process");
            snapshot = new EnvironmentalFireTargetSnapshot(
                Target,
                Vector2Int.zero,
                exists: true,
                combustible: true,
                hasElectricalHazard: electrical,
                profile: new EnvironmentalFireProfile(
                    fuelCapacity: 1f,
                    maximumIntensity: 1f,
                    growthPerTick: 0.05f,
                    fuelConsumedPerTick: 0.1f,
                    damagePerTick: 12f,
                    minimumSpreadIntensity: minimumSpreadIntensity,
                    spreadChancePerTick: spreadChancePerTick,
                    spreadIgnitionMultiplier: 0.5f,
                    waterSuppressionPerUnit: 0.5f),
                displayName: electrical ? "전기 설비" : "작업 시설");
            IgnitionIntensity = intensity;
            Safety = new EnvironmentalFireElectricalSafetySnapshot(
                Target,
                hasElectricalContact: electrical,
                connectionEnabled: electrical,
                breakerTripped: false,
                hasLiveGeneration: electrical,
                hasStoredPower: electrical);
            Water = water ?? new RecordingWaterSink();
            Runtime = new EnvironmentalFireRuntime(
                this,
                this,
                this,
                this,
                Water,
                new EnvironmentalFireRuntimeSettings(5f, 0.35f, 0.02f),
                new RandomStreamProvider(157181),
                Outcomes.Bridge,
                EditorFixedGameCalendar.Instance);
        }

        public EnvironmentalFireRuntime Runtime { get; }
        public OutcomeFixture Outcomes { get; }
        public int PublishedOutcomeCount =>
            Outcomes.Ledger.GetDiagnostics().PublishedCount;
        public EnvironmentalFireTargetRef Target { get; }
        public RecordingWaterSink Water { get; }
        public List<EnvironmentalFireTargetSnapshot> AdjacentTargets { get; } =
            new();
        public float IgnitionIntensity { get; }
        public EnvironmentalFireElectricalSafetySnapshot Safety { get; set; }

        public EnvironmentalFireIgnitionRequest CreateIgnitionRequest(
            string causeId) => new(
                causeId,
                Target.TargetId.EndsWith("electrical", StringComparison.Ordinal)
                    ? EnvironmentalFireIgnitionKind.ElectricalFault
                    : EnvironmentalFireIgnitionKind.ProcessAccident,
                "focused-test",
                Target,
                IgnitionIntensity,
                "evidence:" + causeId,
                targetDisplayName: snapshot.DisplayName);

        public EnvironmentalFireIgnitionResult Ignite(string causeId) =>
            Runtime.TryIgnite(CreateIgnitionRequest(causeId));

        public EnvironmentalFireSuppressionCommand InitialAttack(
            string operationId,
            string fireId,
            float work) => new(
                operationId,
                fireId,
                "character:worker",
                new Vector2Int(1, 0),
                EnvironmentalFireSuppressionMode.InitialAttack,
                work,
                workerDisplayName: "진압 작업자");

        public bool TryGetTarget(
            EnvironmentalFireTargetRef target,
            out EnvironmentalFireTargetSnapshot result)
        {
            if (target.Equals(Target))
            {
                result = snapshot;
                return true;
            }

            for (int index = 0; index < AdjacentTargets.Count; index++)
            {
                if (!AdjacentTargets[index].Target.Equals(target))
                    continue;
                result = AdjacentTargets[index];
                return true;
            }

            result = default;
            return false;
        }

        public IReadOnlyList<EnvironmentalFireTargetSnapshot> GetAdjacentTargets(
            EnvironmentalFireTargetRef target) => target.Equals(Target)
                ? AdjacentTargets
                : Array.Empty<EnvironmentalFireTargetSnapshot>();

        public EnvironmentalFireTargetSnapshot AddAdjacent(
            string targetId,
            bool combustible,
            bool acceptsSpread)
        {
            var target = new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.Building,
                targetId);
            var adjacent = new EnvironmentalFireTargetSnapshot(
                target,
                new Vector2Int(1, 0),
                exists: true,
                combustible: combustible,
                hasElectricalHazard: false,
                profile: new EnvironmentalFireProfile(
                    fuelCapacity: 1f,
                    maximumIntensity: 1f,
                    growthPerTick: 0f,
                    fuelConsumedPerTick: 0.01f,
                    damagePerTick: 1f,
                    minimumSpreadIntensity: 1f,
                    spreadChancePerTick: 0f,
                    spreadIgnitionMultiplier: 0.5f,
                    waterSuppressionPerUnit: 0.5f),
                acceptedSources: acceptsSpread
                    ? EnvironmentalFireIgnitionSources.Spread
                    : EnvironmentalFireIgnitionSources.ProcessAccident,
                displayName: "인접 시설");
            AdjacentTargets.Add(adjacent);
            return adjacent;
        }

        public bool TryApply(
            EnvironmentalFireDamageCommand command,
            out EnvironmentalFireDamageResult result)
        {
            result = new EnvironmentalFireDamageResult(
                committed: true,
                appliedDamage: command.RequestedDamage,
                targetRemainsCombustible: true);
            return true;
        }

        public bool CanSuppress(
            string workerId,
            Vector2Int standPosition,
            EnvironmentalFireTargetRef target,
            out string failureReason)
        {
            failureReason = string.Empty;
            return workerId == "character:worker"
                && standPosition == new Vector2Int(1, 0)
                && target.Equals(Target);
        }

        public bool TryGetSafety(
            EnvironmentalFireTargetRef target,
            out EnvironmentalFireElectricalSafetySnapshot result)
        {
            result = Safety;
            return target.Equals(Target);
        }
    }

    internal sealed class OutcomeFixture
    {
        internal OutcomeFixture(bool includeDamage = false)
        {
            var josa = new KoreanJosaFormatter();
            List<IGameplayOutcomeDescriptor> descriptors = new()
            {
                new FireIgnitionOutcomeDescriptor(josa),
                new FireSuppressionOutcomeDescriptor(josa)
            };
            List<IGameplayOutcomeAdapterRegistration> adapters = new()
            {
                new EnvironmentalFireIgnitionOutcomeAdapter(),
                new EnvironmentalFireSuppressionOutcomeAdapter()
            };
            if (includeDamage)
            {
                descriptors.Add(new FireDamageOutcomeDescriptor(josa));
                adapters.Add(new EnvironmentalFireDamageOutcomeAdapter());
            }
            GameplayOutcomeRegistry registry = new(
                descriptors,
                adapters);
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId("run:fire-01-focused"),
                1L);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                registry,
                new GameEventBus());
            Bridge = new EnvironmentGameplayOutcomeBridge(recorder, Ledger, Ledger);
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal EnvironmentGameplayOutcomeBridge Bridge { get; }
    }

    private enum ControlledCommitMode
    {
        Success,
        Fail,
        Throw
    }

    private sealed class ControlledEnvironmentCommitter :
        IEnvironmentGameplayOutcomeCommitter
    {
        private GameplayResultKey resultKey;

        public ControlledCommitMode Mode { get; set; }

        public bool TryReserve(
            in EnvironmentOutcomeReservationSpec spec,
            out ReservedEnvironmentOutcome reserved,
            out string failureReason)
        {
            resultKey = spec.ResultKey;
            reserved = default;
            failureReason = string.Empty;
            return true;
        }

        public bool TryWriteReserved<TReceipt>(
            in TReceipt receipt,
            in ReservedEnvironmentOutcome reserved,
            out PreparedEnvironmentOutcome prepared,
            out string failureReason)
            where TReceipt : struct, IEnvironmentOutcomeReceipt =>
            TryPrepareControlled(out prepared, out failureReason);

        public bool TryPrepare<TReceipt>(
            in TReceipt receipt,
            out PreparedEnvironmentOutcome prepared,
            out string failureReason)
            where TReceipt : struct, IEnvironmentOutcomeReceipt =>
            TryPrepareControlled(out prepared, out failureReason);

        public EnvironmentOutcomeCommitResult Commit(
            in PreparedEnvironmentOutcome prepared,
            long expectedOwnerRevision) => new(
            EnvironmentOutcomeCommitPhase.PublishedAcknowledged,
            resultKey,
            default,
            new string('a', 64),
            string.Empty);

        public EnvironmentOutcomeCommitResult Reconcile(
            GameplayResultKey key) => new(
            EnvironmentOutcomeCommitPhase.Rejected,
            key,
            default,
            string.Empty,
            "controlled-not-committed");

        public bool IsCanonicalAcknowledgedReplay<TReceipt>(
            in TReceipt receipt,
            out string failureReason)
            where TReceipt : struct, IEnvironmentOutcomeReceipt
        {
            failureReason = "controlled-not-replay";
            return false;
        }

        public void Cancel(in PreparedEnvironmentOutcome prepared)
        {
        }

        public void Cancel(in ReservedEnvironmentOutcome reserved)
        {
        }

        private bool TryPrepareControlled(
            out PreparedEnvironmentOutcome prepared,
            out string failureReason)
        {
            if (Mode == ControlledCommitMode.Throw)
                throw new InvalidOperationException("controlled-throw");
            prepared = default;
            failureReason = Mode == ControlledCommitMode.Fail
                ? "controlled-false"
                : string.Empty;
            return Mode == ControlledCommitMode.Success;
        }
    }

    private sealed class RecordingWaterSink : IEnvironmentalFireWaterSink
    {
        private readonly Dictionary<string, EnvironmentalFireWaterReceipt> pending =
            new(StringComparer.Ordinal);

        public bool Acknowledge { get; set; } = true;
        public int CommitCount { get; private set; }
        public int AcknowledgeCount { get; private set; }

        public bool TryCommitReservedWaterPending(
            string leaseId,
            int quantity,
            string operationId,
            string workerId,
            string fireId,
            Vector2Int standPosition,
            Vector2Int targetPosition,
            out EnvironmentalFireWaterReceipt receipt,
            out string failureReason)
        {
            if (!pending.TryGetValue(operationId, out receipt))
            {
                receipt = new EnvironmentalFireWaterReceipt(
                    operationId,
                    leaseId,
                    quantity,
                    "commit:" + operationId);
                pending.Add(operationId, receipt);
                CommitCount++;
            }
            failureReason = string.Empty;
            return true;
        }

        public bool TryGetPending(
            string operationId,
            string leaseId,
            out EnvironmentalFireWaterReceipt receipt) =>
            pending.TryGetValue(operationId, out receipt)
            && string.Equals(receipt.LeaseId, leaseId, StringComparison.Ordinal);

        public bool TryAcknowledge(string commitId, out string failureReason)
        {
            AcknowledgeCount++;
            failureReason = Acknowledge ? string.Empty : "controlled pending ACK";
            return Acknowledge;
        }
    }
}
#endif
