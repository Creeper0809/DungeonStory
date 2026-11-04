#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

// Bounded real model integration, not a synthetic response or training-data run.
public static class TrainedNarrativeModelPlayModeVerifier
{
    private const string Pending = "DungeonStory.TrainedModel.Pending";
    private const string ExitPending = "DungeonStory.TrainedModel.ExitPending";
    private const string ExitCode = "DungeonStory.TrainedModel.ExitCode";
    private const string Started = "DungeonStory.TrainedModel.Started";
    private static LocalLlmRequestQueue queue;
    private static IStartPartyPreparationService preparation;
    private static bool begun;
    private static int completions;
    private static int sequence;
    private static int lastStatusSecond = -1;
    private static bool partyVerified;
    private static bool macroRequested;
    private static bool macroCompleted;
    private static LocalLlmResult macroResult;
    private static AiDirectorRuntime director;
    private static JArray verifiedChoices;
    private static CharacterSO preparedOwner;
    private static string previewJournalBaseline;
    private static int previewLifecyclePhase;
    private static JArray runtimeFailures = new JArray();
    private static HashSet<string> successfulProfiles = new HashSet<string>(StringComparer.Ordinal);

    public static void CompileForBatch()
    {
        Require(Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode,
            "Compilation check requires isolated Editor batch mode.");
        DungeonStoryLlmHostProcess.VerifyNativeCacheDiagnosticScopeForBatch();
        DungeonStoryLlmHostProcess.VerifyNativeSingleModelSpeculativeScopeForBatch();
        V25NarrativeInferenceDebugScenarios.VerifyContextAwareScheduling();
        V25NarrativeInferenceDebugScenarios.RunCompactEquipmentTransportScenario();
        V25NarrativeInferenceDebugScenarios.RunEvolutionHistoryAdmissionRetryScenario();
        ResponsiveLlmLaneDebugScenarios.VerifyForBatch();
        CharacterProgressionDebugScenarios.VerifyRejectedInputRetryForBatch();
        CharacterProgressionDebugScenarios.VerifyStaleTraitEvidenceForBatch();
        CharacterProgressionDebugScenarios.VerifyAcquiredTraitNoOfferDiagnosticForBatch();
        Require(CharacterProgressionDebugScenarios.RunAcquiredTraitBoundedEvidenceWindowScenario(),
            "Acquired-trait exact/legacy domain capacity regression failed.");
        Require(CharacterProgressionDebugScenarios.RunSkillAcquiredOutcomeAtomicityScenario(),
            "Skill acquisition and immutable producer/dynamic retention anchor contracts failed.");
        PersonaNarrativePersistenceDebugScenarios.VerifyForBatch();
        AcquiredTraitNumericNameDebugScenarios.VerifyForBatch();
        AcquiredTraitSummaryDebugScenarios.VerifyForBatch();
        AcquiredTraitEffectValidationDebugScenarios.VerifyForBatch();
        PerformanceReadBatchDebugScenarios.VerifyForBatch();
        SaveSimulationHoldDebugScenarios.VerifyForBatch();
        SavePayloadReadMemoDebugScenarios.VerifyForBatch();
        StrictSaveScalarStreamingDebugScenarios.VerifyForBatch();
        RetailPurchaseReadShapeDebugScenarios.VerifyForBatch();
        ScheduledDungeonSaveDebugScenarios.VerifyForBatch();
        GameplayOutcomeReadCopyDebugScenarios.VerifyForBatch();
        CharacterSaveReadCopyDebugScenarios.VerifyForBatch();
        EvolutionInitialAnchorShapeDebugScenarios.VerifyForBatch();
        V20ContentResolutionRecoveryDebugScenarios.VerifyLargeContextForBatch();
        EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios.VerifySpeciesPreflightForBatch();
        R80ExtendedPlayModeVerifier.VerifyDecimalRecordContract();
        R80ExtendedPlayModeVerifier.VerifyFacilityDamageSourceContract();
        SocialRumorSingleFlightDebugScenarios.VerifyForBatch();
        CharacterVitalsActivityObserver.VerifySmallDamageProjectionForBatch();
        CharacterFeedbackDebugScenarios.VerifyNarrativeContractForBatch();
        CharacterFeedbackDebugScenarios.VerifyRecordPlaceParticleForBatch();
        CharacterRecordVisibilityDebugScenarios.VerifyForBatch();
        MigratedProducerCombatDefenseEntryE2EDebugScenarios.VerifyRepeatedInvasionFrontCollapseForBatch();
        WriteNew("compile-result.json", new JObject {
            ["status"] = "CURRENT_SOURCE_COMPILED",
            ["nativeCachePrivateDiagnosticScopeFixture"] = "PASS",
            ["nativeSingleModelSpeculativeScopeFixture"] = "PASS",
            ["contextAwareSchedulingFixture"] = "PASS",
            ["historyAdmissionRetryAndLosslessTransportFixture"] = "PASS",
            ["responsiveLifetimeAndRejectionFixture"] = "PASS",
            ["unchangedRejectedInputAndLegacyTimeoutFixture"] = "PASS",
            ["staleExactTraitEvidenceInvalidationFixture"] = "PASS",
            ["acquiredTraitNoOfferReasonAndUnconsumedStateFixture"] = "PASS",
            ["acquiredTraitBalancedEvidenceCapacityFixture"] = "PASS",
            ["skillAcquisitionDynamicAnchorRestoreFixture"] = "PASS",
            ["personaPersistenceCodecAndValidationFixture"] = "PASS",
            ["acquiredTraitNumericNameAuthorityFixture"] = "PASS",
            ["acquiredTraitPlayerSummaryFixture"] = "PASS",
            ["acquiredTraitEffectValidationCompatibilityFixture"] = "PASS",
            ["performanceReadBatchInputLifetimeFixture"] = "PASS",
            ["saveSimulationHoldIntentAndReleaseFixture"] = "PASS",
            ["savePayloadReadMemoIsolationFixture"] = "PASS",
            ["strictScalarStreamingCompatibilityFixture"] = "PASS",
            ["retailEmptyReadShapeAndPendingRecoveryFixture"] = "PASS",
            ["scheduledSaveCompletionFailureAndReadLifetimeFixture"] = "PASS",
            ["gameplayOutcomeReadCopyCompletenessFixture"] = "PASS",
            ["characterSaveReadCopyCompletenessFixture"] = "PASS",
            ["evolutionInitialAnchorShapeFixture"] = "PASS",
            ["largeCampaignContextLedgerAndRecoveryFixture"] = "PASS",
            ["speciesPreflightUsesOwnerDefinitionsFixture"] = "PASS",
            ["decimalRecordFixture"] = "PASS",
            ["facilityDamageSourceContractFixture"] = "PASS",
            ["socialSingleFlightFixture"] = "PASS",
            ["smallDamageProjectionFixture"] = "PASS",
            ["recordFactualContractFixture"] = "PASS",
            ["recordPlaceObjectParticleFixture"] = "PASS",
            ["recordRejectedDisplayVisibilityFixture"] = "PASS",
            ["repeatedInvasionFrontCollapseLineageFixture"] = "PASS",
            ["hostAssemblyMvid"] = typeof(DungeonStoryLlmHostProcess).Module.ModuleVersionId.ToString(),
            ["verifierAssemblyMvid"] = typeof(TrainedNarrativeModelPlayModeVerifier).Module.ModuleVersionId.ToString(),
            ["modelInferenceRun"] = false
        }.ToString());
    }

    public static void RequestForBatch()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Trained model test requires isolated Editor batch mode.");
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "PlayMode already active.");
        string root = Environment.GetEnvironmentVariable("DUNGEONSTORY_LLM_STREAMING_ROOT");
        Require(!string.IsNullOrWhiteSpace(root) && Path.IsPathRooted(root), "Explicit test model root missing.");
        var manifest = new DungeonStoryLlmHostManifest();
        JsonUtility.FromJsonOverwrite(File.ReadAllText(Path.Combine(root, "DungeonStoryLlm", "manifest.json")), manifest);
        Require(IsTrainedPrototype(manifest) && !manifest.releaseCertified,
            "Test requires an exact supported trained prototype bundle.");
        if (manifest.hostKind == DungeonStoryLlmHostProcess.R80BackendKind)
            Require(Path.GetFullPath(DungeonStoryLlmHostProcess.ResolveDefaultRoot(Application.streamingAssetsPath))
                == Path.GetFullPath(root), "R80 test must match the game's activated default bundle.");
        Require(Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE") != "1",
            "Capture-only mode cannot prove model inference.");
        WriteNew("test-scope.json", new JObject {
            ["kind"] = "actual-start-party-model-integration", ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false, ["injectedModelResponses"] = 0,
            ["modelVersion"] = manifest.modelVersion, ["modelSha256"] = manifest.modelSha256,
            ["adapterSha256"] = manifest.adapterSha256,
            ["scope"] = "Actual party skill lifecycle, commit/save/restore, MacroGoal and automatically produced world trait/social responses; not all narrative profiles, combat or long gameplay."
        }.ToString());
        SessionState.SetBool(Pending, true);
        SessionState.SetString(Started, EditorApplication.timeSinceStartup.ToString(CultureInfo.InvariantCulture));
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayScene.unity", OpenSceneMode.Single);
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (SessionState.GetBool(ExitPending, false))
        {
            EditorApplication.playModeStateChanged += ExitAfterStop;
            EditorApplication.delayCall += CompleteExit;
        }
        if (SessionState.GetBool(Pending, false))
        {
            queue = null; preparation = null; begun = false;
            completions = 0; sequence = 0; lastStatusSecond = -1;
            partyVerified = false; macroRequested = false; macroCompleted = false;
            director = null; verifiedChoices = null;
            preparedOwner = null; previewJournalBaseline = null; previewLifecyclePhase = 0;
            runtimeFailures = new JArray(); successfulProfiles = new HashSet<string>(StringComparer.Ordinal);
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
        }
    }

    private static void Pump()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        try
        {
            double elapsed = EditorApplication.timeSinceStartup
                - double.Parse(SessionState.GetString(Started, "0"), CultureInfo.InvariantCulture);
            Require(elapsed <= 300, "Bounded integration did not complete within 300 seconds. "
                + (queue != null ? queue.GetRequestDiagnosticsForDebug() : "No queue"));
            if (!EditorApplication.isPlaying) return;
            var scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value.Container != null);
            if (scope == null) return;
            if (queue == null)
            {
                queue = UnityEngine.Object.FindFirstObjectByType<LocalLlmRequestQueue>();
                if (queue == null) return;
                queue.ActualRequestCompletedForDiagnostics += RecordCompletion;
            }
            int second = (int)elapsed;
            if (second / 15 != lastStatusSecond / 15)
            {
                lastStatusSecond = second;
                WriteNew("status-" + second.ToString("D3") + ".json", new JObject {
                    ["elapsedSeconds"] = elapsed, ["frame"] = Time.frameCount,
                    ["hostStarting"] = queue.IsBundledModelStarting, ["hostRunning"] = queue.IsBundledModelRunning,
                    ["previewLifecyclePhase"] = previewLifecyclePhase,
                    ["members"] = preparation == null ? new JArray() : new JArray(preparation.Members.Select(member => new JObject {
                        ["name"] = member.Progression?.GrowthState?.displayName,
                        ["activeReady"] = member.HasReadyFirstActive, ["activeChosen"] = member.HasSelectedFirstActive,
                        ["passiveReady"] = member.HasFirstPassive,
                        ["drafts"] = member.Progression == null ? new JArray() : new JArray(member.Progression.Drafts.Select(draft => new JObject {
                            ["kind"] = draft.kind.ToString(), ["ready"] = draft.isReady,
                            ["state"] = draft.presentationState.ToString(), ["correction"] = draft.presentationCorrection
                        }))
                    })),
                    ["diagnostic"] = queue.GetRequestDiagnosticsForDebug(), ["lastError"] = queue.LastError
                }.ToString());
            }
            if (queue.IsBundledModelStarting) return;
            Require(queue.IsBundledModelRunning
                && (queue.BundledModelTrainingState == "sft-v10-local-4bit-test"
                    || queue.BundledModelTrainingState == "r80-single-checkpoint-prototype"),
                "Trained host unavailable: " + queue.LastError);
            if (partyVerified)
            {
                if (runtimeFailures.Count > 0)
                {
                    WriteNew("runtime-request-failures.json", runtimeFailures.ToString());
                    throw new InvalidOperationException("Actual gameplay emitted failed model requests; inspect runtime-request-failures.json.");
                }
                if (!macroRequested)
                {
                    director = UnityEngine.Object.FindFirstObjectByType<AiDirectorRuntime>();
                    Require(director != null, "Actual scene AI director missing.");
                    var actor = scope.Container.Resolve<ICharacterAiWorldRegistry>().Characters
                        .FirstOrDefault(value => value != null && value.Blackboard != null && value.CanRunAi);
                    Require(actor != null, "Restored party has no active AI actor.");
                    macroRequested = true;
                    Require(director.RequestMacroGoal(actor), "Actual macro-goal producer rejected request: " + director.LastError);
                    return;
                }
                if (!macroCompleted) return;
                if (!successfulProfiles.Contains(LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id)
                    || !successfulProfiles.Contains(LocalLlmRequestProfiles.SocialRumor.Id)) return;
                Require(macroResult.IsSuccess && string.IsNullOrEmpty(director.LastError)
                    && director.LastAppliedMacroGoalDebug.StartsWith("narrative-only suggested=", StringComparison.Ordinal),
                    "Actual macro-goal consumer rejected R80 response: " + director.LastError + " " + macroResult.Error);
                Require(director.LastRequestDebug.Contains("\nfun: "), "Actual macro producer omitted fun satisfaction.");
                WriteNew("macro-goal-producer-result.json", new JObject {
                    ["status"] = "PASS", ["producerPrompt"] = director.LastRequestDebug,
                    ["acceptedContent"] = macroResult.Content,
                    ["consumer"] = director.LastAppliedMacroGoalDebug
                }.ToString());
                WriteNew("runtime-request-failures.json", runtimeFailures.ToString());
                Require(runtimeFailures.Count == 0, "Actual gameplay emitted failed model requests; inspect runtime-request-failures.json.");
                WriteNew("integration-result.json", new JObject {
                    ["status"] = "PASS", ["elapsedSeconds"] = elapsed,
                    ["actualSuccessfulCompletions"] = completions, ["choices"] = verifiedChoices,
                    ["modelVersion"] = queue.BundledModelVersion, ["saveRestore"] = true,
                    ["actualMacroGoalConsumer"] = director.LastAppliedMacroGoalDebug,
                    ["actualSuccessfulProfiles"] = new JArray(successfulProfiles.OrderBy(value => value, StringComparer.Ordinal)),
                    ["actualFailedRequests"] = runtimeFailures.Count,
                    ["injectedModelResponses"] = 0, ["trainingEligible"] = false,
                    ["allNarrativeProfilesVerified"] = false
                }.ToString());
                Finish(0);
                return;
            }
            if (!begun)
            {
                scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
                Time.timeScale = 0;
                Require(StartPartyPreparationPlayModeVerifier.TryReconcileTierZeroForDirectGameplayFixture(
                    scope, out string layoutDetail), "Normal new-run space initialization failed: " + layoutDetail);
                WriteNew("new-run-space-initialization.json", new JObject {
                    ["status"] = "PASS", ["productionExpansionCommand"] = true, ["detail"] = layoutDetail
                }.ToString());
                var provider = scope.Container.Resolve<IOwnerRunManagerProvider>();
                Require(provider.TryGetManager(out var manager), "Owner manager missing.");
                var owner = manager.OwnerCandidates.FirstOrDefault();
                Require(owner != null, "No authored owner candidate.");
                preparedOwner = owner;
                previewJournalBaseline = CaptureSkillJournal(scope);
                preparation = scope.Container.Resolve<IStartPartyPreparationService>();
                Require(preparation.Begin(owner, out string message), "Normal preparation rejected: " + message);
                begun = true;
                return;
            }
            var staff = preparation.Members.Where(member => !member.IsOwner && !member.IsReserve).ToArray();
            Require(staff.Length > 0, "Normal preparation produced no staff.");
            foreach (var member in staff)
            {
                var rejected = member.Progression.Drafts.FirstOrDefault(draft =>
                    !draft.isReady && draft.presentationState == CharacterSkillPresentationState.AwaitingNarrativeRetry);
                Require(rejected == null, "Actual skill presentation requires manual retry: " + rejected?.presentationCorrection);
                if (member.HasReadyFirstActive && !member.HasSelectedFirstActive)
                    Require(preparation.TryChooseFirstActive(member.Index, 0, out string message),
                        "Normal skill choice rejected: " + message);
                Require(member.Progression.GrowthState.skillAcquisitionRevision == 0,
                    "Disposable initial choices must not claim world acquisition revisions.");
            }
            Require(CaptureSkillJournal(scope) == previewJournalBaseline,
                "Disposable preview choices polluted the world acquisition journal.");
            if (!staff.All(member => member.IsReadyToStart)) return;
            Require(completions > 0, "No actual successful model completion observed.");
            if (previewLifecyclePhase == 0)
            {
                Require(preparation.TryPartialReroll(staff[0].Index, StartPartyRerollGroup.Skill, out string message),
                    "Normal skill reroll rejected: " + message);
                previewLifecyclePhase = 1;
                return;
            }
            if (previewLifecyclePhase == 1)
            {
                Require(preparation.TryFullReroll(staff[0].Index, out string message),
                    "Normal full reroll rejected: " + message);
                previewLifecyclePhase = 2;
                return;
            }
            if (previewLifecyclePhase == 2)
            {
                preparation.Cancel();
                Require(!preparation.IsPreparing && CaptureSkillJournal(scope) == previewJournalBaseline,
                    "Cancel retained provisional party or world acquisition facts.");
                Require(preparation.Begin(preparedOwner, out string message),
                    "Preparation restart after cancel rejected: " + message);
                previewLifecyclePhase = 3;
                WriteNew("preview-lifecycle-result.json", new JObject {
                    ["status"] = "PASS", ["skillReroll"] = true, ["fullReroll"] = true,
                    ["cancelRestart"] = true, ["worldAcquisitionJournalUnchanged"] = true
                }.ToString());
                return;
            }
            var choices = new JArray(staff.Select(member => new JObject {
                ["name"] = member.Progression.GrowthState.displayName,
                ["role"] = member.RoleLabel,
                ["activeSkills"] = Skills(member.Progression.ActiveSkills),
                ["passiveSkills"] = Skills(member.Progression.PassiveSkills)
            }));
            Require(scope.Container.Resolve<IPreparedStartPartyCommitService>().TryCommit(out string commitMessage),
                "Normal gameplay commit rejected: " + commitMessage);
            Require(CaptureSkillJournal(scope) == previewJournalBaseline,
                "Initial-state publication fabricated world skill acquisition events.");
            var saves = scope.Container.Resolve<IDungeonGameSaveService>();
            var world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
            var committed = new JArray(world.Characters.Where(actor => actor != null && actor.Progression != null)
                .Select(actor => new JObject {
                    ["characterId"] = actor.Identity.PersistentId,
                    ["activeSkills"] = Skills(actor.Progression.ActiveSkills),
                    ["passiveSkills"] = Skills(actor.Progression.PassiveSkills)
                }));
            Require(committed.Count >= staff.Length, "Selected party was not published to the actual world registry.");
            string saved = saves.ToJson(saves.Capture(), true);
            WriteNew("committed-world.json", saved);
            Require(saves.TryRestore(saves.FromJson(saved), out var restore),
                "Official save roundtrip rejected: " + string.Join(";", restore.Errors));
            string restored = saves.ToJson(saves.Capture(), true);
            WriteNew("restored-world.json", restored);
            foreach (JObject member in committed)
            {
                string id = (string)member["characterId"];
                var matches = world.Characters.Where(actor => actor.Identity.PersistentId == id).ToArray();
                Require(matches.Length == 1, "Restored character identity is missing or ambiguous: " + id);
                Require(JToken.DeepEquals(member["activeSkills"], Skills(matches[0].Progression.ActiveSkills))
                    && JToken.DeepEquals(member["passiveSkills"], Skills(matches[0].Progression.PassiveSkills)),
                    "Restored model-authored skills changed: " + id);
            }
            WriteNew("party-roundtrip-result.json", new JObject {
                ["status"] = "PASS", ["elapsedSeconds"] = elapsed,
                ["actualSuccessfulCompletions"] = completions, ["choices"] = choices,
                ["modelVersion"] = queue.BundledModelVersion, ["saveRestore"] = true,
                ["injectedModelResponses"] = 0, ["trainingEligible"] = false,
                ["allNarrativeProfilesVerified"] = false
            }.ToString());
            verifiedChoices = choices;
            partyVerified = true;
        }
        catch (Exception exception)
        {
            WriteNew("integration-failure.txt", exception.ToString());
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static JArray Skills(IEnumerable<CharacterSkillInstance> skills) =>
        new JArray(skills.Select(skill => JObject.Parse(JsonUtility.ToJson(skill))));

    private static string CaptureSkillJournal(DungeonRuntimeLifetimeScope scope) =>
        JsonUtility.ToJson(scope.Container.Resolve<CharacterSkillAcquisitionPublicationRuntime>().Capture());

    private static bool IsTrainedPrototype(DungeonStoryLlmHostManifest manifest) =>
        !string.IsNullOrWhiteSpace(manifest.adapterFile) && manifest.trainingState == "sft-v10-local-4bit-test"
        || manifest.hostKind == DungeonStoryLlmHostProcess.R80BackendKind
        && manifest.trainingState == "r80-single-checkpoint-prototype" && string.IsNullOrEmpty(manifest.adapterFile)
        && manifest.modelSha256 == "2894ec5261b3a8352a375eb6319bbc8dd8c68c9b7b64fba255d51ce1bc2eb9b2";

    private static void RecordCompletion(string profile, string correlation, LocalLlmResult result)
    {
        WriteNew("response-" + (++sequence).ToString("D3") + ".json", new JObject {
            ["profile"] = profile, ["correlationId"] = correlation,
            ["status"] = result.Status.ToString(), ["rawModelContent"] = result.RawModelContent,
            ["acceptedTransportContent"] = result.Content, ["error"] = result.Error,
            ["originalText"] = result.OriginalText, ["frame"] = Time.frameCount
        }.ToString());
        if (result.IsSuccess)
        {
            completions++;
            successfulProfiles.Add(profile);
        }
        if (result.Status == LocalLlmRequestStatus.Failed)
            runtimeFailures.Add(new JObject { ["profile"] = profile, ["correlationId"] = correlation, ["error"] = result.Error });
        if (macroRequested && profile == LocalLlmRequestProfiles.MacroGoal.Id)
        {
            macroResult = result;
            macroCompleted = true;
        }
    }

    private static void Finish(int code)
    {
        if (queue != null) queue.ActualRequestCompletedForDiagnostics -= RecordCompletion;
        SessionState.SetBool(Pending, false);
        SessionState.SetBool(ExitPending, true);
        SessionState.SetInt(ExitCode, code);
        EditorApplication.update -= Pump;
        EditorApplication.playModeStateChanged -= ExitAfterStop;
        EditorApplication.playModeStateChanged += ExitAfterStop;
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
        else EditorApplication.delayCall += CompleteExit;
    }
    private static void ExitAfterStop(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += CompleteExit;
    }
    private static void CompleteExit()
    {
        if (!SessionState.GetBool(ExitPending, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        SessionState.SetBool(ExitPending, false);
        EditorApplication.playModeStateChanged -= ExitAfterStop;
        EditorApplication.Exit(SessionState.GetInt(ExitCode, 1));
    }
    private static void WriteNew(string name, string value)
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-logFile");
        Require(index >= 0 && index + 1 < args.Length, "Isolated log argument missing.");
        string root = Path.GetDirectoryName(Path.GetFullPath(args[index + 1]));
        Require(root.StartsWith(Path.GetFullPath("Artifacts/QA") + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Evidence outside repository QA root.");
        using var writer = new StreamWriter(new FileStream(Path.Combine(root, name), FileMode.CreateNew), new UTF8Encoding(false));
        writer.Write(value);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
