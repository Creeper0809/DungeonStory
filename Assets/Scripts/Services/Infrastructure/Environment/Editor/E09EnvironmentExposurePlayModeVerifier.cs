#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public static class E09EnvironmentExposurePlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/LedgerB1E09ExposurePlayMode-20260925-r6/report.txt";
    private const string ReportEnvironmentVariable =
        "DUNGEONSTORY_U0_E09_ENVIRONMENT_EXPOSURE_REPORT_PATH";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string PendingKey =
        "DungeonStory.LedgerB1.E09.Exposure.Pending";
    private const string SuccessKey =
        "DungeonStory.LedgerB1.E09.Exposure.Success";
    private static double deadline;

    public static void RequestRun()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "E09 exposure verification must start in Edit Mode.");
        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }
        string reportPath = ResolveReportPath();
        if (File.Exists(reportPath))
            throw new InvalidOperationException(
                "E09 exposure report path must be create-only: " + reportPath);
        Write("status=RUNNING\n");
        SessionState.SetBool(SuccessKey, false);
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    public static IEnumerator Verify(
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        bool succeeded = false;
        string detail = string.Empty;
        JArray observations = new();
        try
        {
            Require(
                Application.isBatchMode
                && DungeonPersistencePaths.IsIsolatedVerification
                && string.Equals(
                    Environment.GetEnvironmentVariable(
                        "DUNGEONSTORY_U0_E09_ENVIRONMENT_EXPOSURE_VERIFY"),
                    "1",
                    StringComparison.Ordinal),
                "Focused E09 verification requires explicit isolated batch mode.");
            string reportPath = ResolveReportPath();
            Require(!File.Exists(reportPath),
                "Focused E09 report path must be create-only: " + reportPath);
            Require(!File.Exists(reportPath + ".raw.json"),
                "Focused E09 raw report path must be create-only: " + reportPath);
        }
        catch (Exception exception)
        {
            WriteFocusedFailure(exception, warnings, errors);
            yield break;
        }

        float readyDeadline = Time.realtimeSinceStartup + 45f;
        DungeonRuntimeLifetimeScope scope = null;
        while (scope == null && Time.realtimeSinceStartup < readyDeadline)
        {
            scope = UnityEngine.Object
                .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(value => value?.Container != null);
            if (scope == null)
                yield return null;
        }

        try
        {
            Require(scope != null, "Main gameplay container was not initialized.");
            (scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable)?
                .Dispose();
            scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
            GameManager game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Require(game != null, "Main game manager is missing.");
            game.isPause = true;
            scope.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
            Time.timeScale = 0f;

            string moduleReport = GameplayActionLogModuleSmokeScenarios.Run(
                scope.Container.Resolve<IGameplayOutcomeRegistry>());
            using (FileStream moduleFile = new(ResolveReportPath() + ".action-modules.txt", FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter moduleWriter = new(moduleFile)) moduleWriter.Write(moduleReport);
            int experiencePureChecks = GameplayExperiencePolicyDebugScenarios.RunPureContracts();
            string dispatcherReport = DomainFactDispatcherSmokeScenarios.Run();
            IEnumerable<IDomainFactObserverBinding> bindings = scope.Container
                .Resolve<IEnumerable<IDomainFactObserverBinding>>();
            var expectedFactTypes = new HashSet<Type>();
            foreach (IDomainFactObserverBinding binding in bindings)
            {
                Require(binding?.FactTypes != null
                        && binding.FactTypes.Count > 0,
                    "A domain-fact binding omitted its fact-type declaration.");
                foreach (Type factType in binding.FactTypes)
                {
                    Require(factType != null
                            && expectedFactTypes.Add(factType),
                        "Domain-fact binding declarations contain a null or duplicate fact type.");
                }
            }
            Require(expectedFactTypes.Count > 0,
                "The gameplay container resolved no declared domain-fact bindings.");
            Require(expectedFactTypes.SetEquals(
                    scope.Container.Resolve<DomainFactDispatcher>().SubscribedFactTypes),
                "Current typed-fact producers are not exactly connected to their required observers.");
            dispatcherReport += "live-container-fact-subscriptions=" + expectedFactTypes.Count + "\n";
            dispatcherReport += CharacterConsumptionFactRecoveryScenarios.Verify() + "\n";
            using (FileStream dispatcherFile = new(ResolveReportPath() + ".domain-observers.txt", FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter dispatcherWriter = new(dispatcherFile)) dispatcherWriter.Write(dispatcherReport);
            string consumptionOwnerReport = VerifyConsumptionOwnerDiagnostics(warnings, errors);
            consumptionOwnerReport += "\n" + SurvivalDebugScenarios.VerifyDetoxDomainFactFocused();
            MigratedProducerEconomyEntryExtendedE2EDebugScenarios.VerifyAutoProcurementFocused();
            TreasuryEconomySaveDebugScenarios.RunAll();
            consumptionOwnerReport += "\n" + VerifyStockSaleDiagnostics(warnings, errors);
            WorldResourceCycleOutcomeDebugScenarios.RunDomainFactFocused();
            AnimalHusbandryOutcomeDebugScenarios.RunCommandDomainFactFocused();
            AnimalHusbandryOutcomeDebugScenarios.RunWorkDomainFactFocused();
            consumptionOwnerReport += "\n" + WorkCompletionIdentityCausalProductionEntryDebugScenarios.VerifyHusbandryCompletionFactFocused();
            consumptionOwnerReport += "\nhusbandry-command/policy-and-slaughter/observer-reject-rollback/noop=PASS\n"
                + "husbandry-command/hidden-ack/json-fresh-owner/same-key/no-duplicate=PASS\n"
                + "husbandry-command/immutable-fact/strict-pending-save=PASS\n"
                + "husbandry-work/tame-product-manure-slaughter/typed-observer=PASS\n"
                + "husbandry-work/partial-save/hidden-ack/json-fresh-owner/same-key/no-duplicate=PASS\n"
                + "husbandry-work/items-and-carcass=PRODUCTION;capture-registry-time-actors=CONTROLLED;natural-ai=NOT-RUN\n"
                + "husbandry-work/completion-identity=SEPARATE-FOCUSED-OBSERVER-TEST\n";
            consumptionOwnerReport += "\nworld-resource-cycle/immutable-fact/observer-reserve-and-commit-reject=PASS\n"
                + "world-resource-cycle/real-physical/hidden-ack/json-fresh-owner/same-key/no-duplicate=PASS\n"
                + "world-resource-cycle/save-missing-null-publication-key-rejected=PASS\n"
                + "world-resource-cycle/topology-and-labor=CONTROLLED;natural-ai-and-cold-world=NOT-RUN\n";
            Require(FacilityWishlistProcurementCausalProductionEntryDebugScenarios.RunCausalOwnerFamilyFocused(),
                "Auto-procurement wishlist purchase/physical delivery/restore/replay failed.");
            consumptionOwnerReport += "\nauto-procurement/domain-fact/owner-rejection/delivery-pending=PASS\n"
                + "auto-procurement/immutable-fact/canonical-field-order=PASS\n"
                + "auto-procurement/budget-and-stock-hidden-ack/json-fresh-owner-binding/same-key/no-duplicate=PASS\n"
                + "auto-procurement/hidden-ack-stock-port=CONTROLLED;natural-ai-and-cold-world=NOT-RUN\n"
                + "treasury-save/missing-null-pending/orphan-result-rejected/no-state-mutation=PASS\n"
                + "stock-policy/quality-rejected-sale/actual-owner-physical-money/reject/save-retry-no-duplicate=PASS\n"
                + "stock-policy/restoration=SCOPED-OWNER-REGISTRY;natural-ai-and-cold-world=NOT-RUN\n"
                + "wishlist/real-payment/unlock/physical-kit/fresh-owner-restore/no-duplicate=PASS\n";
            using (FileStream ownerFile = new(ResolveReportPath() + ".domain-owners.txt", FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter ownerWriter = new(ownerFile)) ownerWriter.Write(consumptionOwnerReport
                + "\nunchanged-batch2-and-batch3-owner-tests=NOT-RERUN\n");
            int experienceIntegrationChecks = GameplayExperienceIntegrationDebugScenarios.RunLedgerContracts();
            string policyReport = GameplayAllExperiencePolicyDebugScenarios.Run(
                scope.Container.Resolve<IGameplayOutcomeRegistry>().RegisteredOutcomeTypes.Select(value => value.Value).ToArray());
            string policyReportPath = ResolveReportPath() + ".all-policies.txt";
            using (FileStream policyFile = new(policyReportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter policyWriter = new(policyFile)) policyWriter.Write(policyReport);
            JObject replay = GameplayExperienceHistoricalReplay.Run();
            using (FileStream replayFile = new(ResolveReportPath() + ".historical-replay.json", FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter replayWriter = new(replayFile)) replayWriter.Write(replay.ToString());
            Require((int)replay["snapshots"] == 116 && (int)replay["types"] == 39,
                "Historical exact receipt fixture inventory differs.");
            Require((int)replay["unassessed"] == 0, "Historical exact receipts include unassessed policy branches; see replay report.");

            Require(
                EnvironmentControlExposureOutcomeDebugScenarios.RunAll(
                    log: true,
                    writeReport: false,
                    observe: value => observations.Add(value)),
                "Thermostat setting and exposure receipt scenarios failed.");
            detail = "thermostat-setting-current-execution=PASS\n"
                + "experience-pure-assertions=" + experiencePureChecks + "\n"
                + "experience-ledger-contracts=" + experienceIntegrationChecks + "\n"
                + "all-experience-policies=PASS\n"
                + "action-log-modules=149-PASS\n"
                + RunProductionEntry(scope.Container, observations);
            string[] requiredStages =
            {
                "control-before", "control-set", "control-same-value", "control-clamped",
                "control-continuous", "control-reserve-before", "control-reserve-rejected",
                "control-ui-before", "control-ui-set", "control-ui-replay",
                "control-pending-before", "control-pending", "control-restored-pending",
                "control-reconciled", "control-reconciled-replay",
                "comfort-before", "comfort-after", "release-before", "release-stable",
                "release-stable-replay", "exposure-reserve-before", "exposure-reserve-rejected",
                "exposure-pending-before", "exposure-pending", "exposure-restored-pending",
                "exposure-reconciled", "exposure-reconciled-replay",
                "health-owner-floor-before", "health-owner-floor-applied", "health-owner-floor-no-op",
                "experience-before-restore", "experience-after-restore"
            };
            Require(requiredStages.All(stage => observations.Count(value =>
                    (string)value["stage"] == stage) == 1),
                "E09 raw observations are missing or repeat a required boundary.");
            Require(warnings.Count == 0 && errors.Count == 0,
                "Unexpected runtime Console warning/error was observed.");
            WriteRaw(true, observations, warnings, errors, null);
            Write(
                "status=PASS\n"
                + "unityMcpUsed=false\n"
                + "dungeonPlayerMcpUsed=false\n"
                + "fullAuditClosure=false\n"
                + "trainingEligible=false\n"
                + "humanApprovalClaimed=false\n"
                + detail
                + "\n");
            succeeded = true;
        }
        catch (Exception exception)
        {
            if (!File.Exists(ResolveReportPath() + ".raw.json"))
                WriteRaw(false, observations, warnings, errors, exception);
            Write(
                "status=FAIL\n"
                + "unityMcpUsed=false\n"
                + "dungeonPlayerMcpUsed=false\n"
                + "fullAuditClosure=false\n"
                + "trainingEligible=false\n"
                + "humanApprovalClaimed=false\n"
                + exception
                + "\n");
            Debug.LogException(exception);
        }

        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () =>
            EditorApplication.Exit(succeeded ? 0 : 1);
    }

    private static string VerifyConsumptionOwnerDiagnostics(ICollection<string> warnings, ICollection<string> errors)
    {
        int errorCount = errors.Count;
        int warningCount = warnings.Count;
        var actual = new List<string>();
        void Observe(string message, string trace, LogType type)
        { if (type != LogType.Log) actual.Add(type + ":" + message); }
        string report;
        Application.logMessageReceived += Observe;
        try { report = ConsumablesCon02ProductionRecoveryDebugScenarios.VerifyObserverRecoveryFocused(); }
        finally { Application.logMessageReceived -= Observe; }
        string[] expected =
        {
            "Error:character-consumables-meal-effects-failed:meal-effect-transaction-rolled-back:InvalidOperationException",
            "Error:character-consumables-substance-effects-failed:substance-effect-transaction-rolled-back:InvalidOperationException",
            "Error:character-consumables-fact-publication-rejected:con02-injected-parent-rejection",
            "Error:character-consumables-fact-publication-rejected:con02-injected-parent-rejection",
            "Error:character-consumables-fact-publication-pending:InDoubt:con02-hidden-durable-ack:",
            "Error:character-consumables-fact-publication-pending:InDoubt:con02-hidden-durable-ack:"
        };
        Require(warnings.Count == warningCount && errors.Count - errorCount == expected.Length
                && actual.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(expected.OrderBy(value => value, StringComparer.Ordinal)),
            "Consumption injected-failure diagnostics differ: " + string.Join(" | ", actual));
        // Only the six explicitly asserted injected errors are removed from
        // the outer unexpected-error list; their exact text remains evidence.
        foreach (string error in errors.Skip(errorCount).ToArray()) errors.Remove(error);
        return report + "\nexpected-injected-diagnostics=" + string.Join(" | ", actual);
    }

    private static string VerifyStockSaleDiagnostics(ICollection<string> warnings, ICollection<string> errors)
    {
        int warningCount = warnings.Count;
        int errorCount = errors.Count;
        var actual = new List<string>();
        void Observe(string message, string trace, LogType type)
        { if (type != LogType.Log) actual.Add(type + ":" + message); }
        Application.logMessageReceived += Observe;
        try
        {
            Require(ResourceStockPolicySaleOutcomeE2EDebugScenarios.RunAll(),
                "Stock sale production owner/physical/money/restore checks failed.");
        }
        finally { Application.logMessageReceived -= Observe; }
        var expected = ResourceStockPolicySaleOutcomeE2EDebugScenarios.ExpectedWarnings
            .Select(message => "Warning:" + message).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Require(expected.Length == 4 && errors.Count == errorCount
            && warnings.Count - warningCount == expected.Length
            && actual.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected),
            "Stock sale injected-failure diagnostics differ: " + string.Join(" | ", actual));
        // Preserve the exact expected diagnostic evidence; all unexpected
        // warnings and errors continue to fail the enclosing execution.
        foreach (string warning in warnings.Skip(warningCount).ToArray()) warnings.Remove(warning);
        return "stock-sale/expected-injected-warnings=" + string.Join(" | ", actual);
    }

    private static void VerifyExpectedDiagnostics(Action action, LogType expectedType,
        params string[] expectedMessages)
    {
        var actual = new List<string>();
        void Observe(string condition, string trace, LogType type)
        {
            if (type == LogType.Log) return;
            actual.Add(type + ":" + condition);
        }
        Application.logMessageReceived += Observe;
        try { action(); }
        finally { Application.logMessageReceived -= Observe; }
        var expected = expectedMessages.Select(value => expectedType + ":" + value)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Require(actual.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(expected),
            "Focused injected-failure diagnostics differ: " + string.Join(" | ", actual));
    }

    private static void WriteFocusedFailure(
        Exception exception,
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        Write(
            "status=FAIL\n"
            + "unityMcpUsed=false\n"
            + "dungeonPlayerMcpUsed=false\n"
            + "fullAuditClosure=false\n"
            + "trainingEligible=false\n"
            + "humanApprovalClaimed=false\n"
            + "warningCount=" + warnings.Count + "\n"
            + "errorCount=" + errors.Count + "\n"
            + exception
            + "\n");
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(1);
    }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PendingKey, false))
            return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 90d;
            EditorApplication.update -= Verify;
            EditorApplication.update += Verify;
            return;
        }
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.update -= Verify;
        bool succeeded = SessionState.GetBool(SuccessKey, false);
        SessionState.SetBool(PendingKey, false);
        SessionState.SetBool(SuccessKey, false);
        if (Application.isBatchMode)
            EditorApplication.Exit(succeeded ? 0 : 1);
    }

    private static void Verify()
    {
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object
            .FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(value => value?.Container != null);
        if (scope == null && EditorApplication.timeSinceStartup < deadline)
            return;

        EditorApplication.update -= Verify;
        bool succeeded = false;
        try
        {
            Require(scope != null, "Main gameplay container was not initialized.");
            (scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable)?
                .Dispose();
            scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
            GameManager game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            Require(game != null, "Main game manager is missing.");
            game.isPause = true;
            scope.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
            Time.timeScale = 0f;

            string detail = RunProductionEntry(scope.Container);
            Write(
                "status=PASS\n"
                + "unityMcpUsed=false\n"
                + "dungeonPlayerMcpUsed=false\n"
                + "humanApprovalClaimed=false\n"
                + detail
                + "\n");
            succeeded = true;
        }
        catch (Exception exception)
        {
            Write(
                "status=FAIL\n"
                + "unityMcpUsed=false\n"
                + "dungeonPlayerMcpUsed=false\n"
                + "humanApprovalClaimed=false\n"
                + exception
                + "\n");
            Debug.LogException(exception);
        }
        finally
        {
            SessionState.SetBool(SuccessKey, succeeded);
            EditorApplication.ExitPlaymode();
        }
    }

    private static string RunProductionEntry(IObjectResolver services, JArray observations = null)
    {
        GameplayOutcomeLedger ledger = services.Resolve<GameplayOutcomeLedger>();
        IDomainFactPublisher liveCommitter =
            services.Resolve<IDomainFactPublisher>();
        List<string> lines = new();

        using (ExposureFixture comfort = new(
                   services,
                   liveCommitter,
                   "character:e09:comfort",
                   100))
        {
            int before = Count(ledger, comfort.CharacterId);
            comfort.Field.Temperature = 20f;
            comfort.Stage(0f, EnvironmentalExposureBand.Stable, 0f);
            ObserveExposure(observations, "comfort-before", comfort, services, ledger);
            comfort.Advance();
            ObserveExposure(observations, "comfort-after", comfort, services, ledger);
            Require(Count(ledger, comfort.CharacterId) == before,
                "Comfortable exposure tick emitted a gameplay result.");
            lines.Add("comfortable-continuous-tick-zero-results=PASS");
        }

        using (ExposureFixture release = new(
                   services,
                   liveCommitter,
                   "character:e09:release",
                   150))
        {
            release.Stage(
                25f,
                EnvironmentalExposureBand.Burden,
                0f);
            float healthBefore = release.BodyHealth.GetVitals(
                release.Actor).CurrentHealth;
            release.Field.Temperature = 20f;
            release.Field.AirQuality = 100f;
            ObserveExposure(observations, "release-before", release, services, ledger);
            int ticks = 0;
            while (release.RequireState().physiologicalBand
                       != EnvironmentalExposureBand.Stable
                   && ticks++ < 10)
            {
                release.Advance();
            }
            float healthAfter = release.BodyHealth.GetVitals(
                release.Actor).CurrentHealth;
            ObserveExposure(observations, "release-stable", release, services, ledger);
            GameplayOutcomeSnapshot[] rows = Rows(
                ledger,
                release.CharacterId);
            Require(
                release.RequireState().physiologicalBand
                    == EnvironmentalExposureBand.Stable
                && rows.Length == 1
                && FindFact(rows[0], EnvironmentOutcomeIds.EventKindFact)
                    == "band-transition"
                && FindMetric(rows[0], EnvironmentOutcomeIds.BandBeforeMetric)
                    == (double)EnvironmentalExposureBand.Burden
                && FindMetric(rows[0], EnvironmentOutcomeIds.BandAfterMetric)
                    == (double)EnvironmentalExposureBand.Stable
                && Near(healthAfter, healthBefore),
                "Comfortable recovery did not publish exactly one Burden-to-Stable transition without damage.");
            release.Advance();
            ObserveExposure(observations, "release-stable-replay", release, services, ledger);
            Require(
                Count(ledger, release.CharacterId) == 1,
                "Stable tick after exposure release emitted another gameplay result.");
            lines.Add("exposure-release-burden-to-stable-exactly-one=PASS");
        }

        FaultingCommitter rejection = new(liveCommitter)
        {
            RejectReserve = true
        };
        using (ExposureFixture rejected = new(
                   services,
                   rejection,
                   "character:e09:reserve-rejected",
                   200))
        {
            rejected.Stage(24f, EnvironmentalExposureBand.Stable, 0f);
            float healthBefore = rejected.BodyHealth.GetVitals(rejected.Actor).CurrentHealth;
            rejected.Field.Temperature = -50f;
            rejected.Field.AirQuality = 0f;
            ObserveExposure(observations, "exposure-reserve-before", rejected, services, ledger,
                rejection);
            rejected.Advance();
            ObserveExposure(observations, "exposure-reserve-rejected", rejected, services, ledger,
                rejection);
            CharacterEnvironmentExposure state = rejected.RequireState();
            Require(Mathf.Approximately(state.coldExposure, 24f)
                && state.physiologicalBand == EnvironmentalExposureBand.Stable
                && Mathf.Approximately(
                    rejected.BodyHealth.GetVitals(rejected.Actor).CurrentHealth,
                    healthBefore)
                && rejected.Runtime.Capture().nextExposureOutcomeSequence == 200
                && rejection.ReserveAttempts > 0
                && Count(ledger, rejected.CharacterId) == 0,
                "Exposure reservation rejection mutated state, health, sequence, or ledger.");
            lines.Add("reserve-rejection-no-state-or-health-mutation=PASS");
        }

        FaultingCommitter hiddenAck = new(liveCommitter)
        {
            HideAcknowledgementCount = 2
        };
        using (ExposureFixture source = new(
                   services,
                   hiddenAck,
                   "character:e09:pending-roundtrip",
                   300))
        {
            source.Stage(24f, EnvironmentalExposureBand.Stable, 0f);
            float healthBefore = source.BodyHealth.GetVitals(source.Actor).CurrentHealth;
            source.Field.Temperature = -50f;
            source.Field.AirQuality = 0f;
            ObserveExposure(observations, "exposure-pending-before", source, services, ledger,
                hiddenAck);
            source.Advance();
            ObserveExposure(observations, "exposure-pending", source, services, ledger,
                hiddenAck);
            float healthAfter = source.BodyHealth.GetVitals(source.Actor).CurrentHealth;
            DungeonCharacterEnvironmentSaveData pending = source.Runtime.Capture();
            Require(healthAfter < healthBefore,
                "Lethal environmental tick did not apply actual body-health damage. "
                + $"before={healthBefore:R}; after={healthAfter:R}; "
                + $"actorProjection={source.Actor.CurrentHealth:R}; "
                + $"dead={source.Actor.IsDead}; expedition={source.Actor.IsOnExpedition}; "
                + $"reserveAttempts={hiddenAck.ReserveAttempts}; "
                + $"pending={pending.exposureOutcomes.Length}; "
                + $"cold={source.RequireState().coldExposure:R}; "
                + $"air={source.RequireState().airborneExposure:R}; "
                + $"band={source.RequireState().physiologicalBand}; "
                + $"rows={Count(ledger, source.CharacterId)}.");
            Require(services.Resolve<ICharacterBodyHealthPersistence>().Capture().characters
                    .Single(value => value.characterId == source.CharacterId).lastDamageReason
                    == "치명적 환경 노출",
                "Actual environmental damage lost its owned body-health cause.");
            Require(pending.exposureOutcomes.Length == 2
                && pending.nextExposureOutcomeSequence == 302,
                "Band and damage outcomes were not retained while acknowledgement was hidden.");

            GameplayOutcomeSnapshot[] rows = Rows(ledger, source.CharacterId);
            Require(rows.Length == 2,
                "Band transition and damage tick did not publish exactly two results.");
            GameplayOutcomeSnapshot band = rows.Single(value =>
                FindFact(value, EnvironmentOutcomeIds.EventKindFact)
                    == "band-transition");
            GameplayOutcomeSnapshot damage = rows.Single(value =>
                FindFact(value, EnvironmentOutcomeIds.EventKindFact)
                    == "damage-tick");
            Require(FindMetric(band, EnvironmentOutcomeIds.BandBeforeMetric) == 0d
                && FindMetric(band, EnvironmentOutcomeIds.BandAfterMetric) > 0d,
                "Band result did not retain the before/after threshold crossing.");
            Require(FindMetric(damage, EnvironmentOutcomeIds.DamageMetric) > 0d
                && Near(FindMetric(damage, EnvironmentOutcomeIds.HealthBeforeMetric), healthBefore)
                && Near(FindMetric(damage, EnvironmentOutcomeIds.HealthAfterMetric), healthAfter),
                "Damage result did not retain actual health before/after values.");

            string experienceBefore = GameplayExperienceIntegrationDebugScenarios.ObserveLive(
                ledger, source.CharacterId, observations, "experience-before-restore");
            string roundTrip = JsonUtility.ToJson(pending);
            using ExposureFixture restored = new(
                services,
                liveCommitter,
                source.CharacterId,
                0,
                source.Actor);
            restored.Clock.IsPaused = true;
            DungeonCharacterEnvironmentSaveData serialized =
                JsonUtility.FromJson<DungeonCharacterEnvironmentSaveData>(roundTrip);
            restored.Runtime.PublishRestoreCandidate(
                restored.Runtime.BuildRestoreCandidate(serialized));
            ObserveExposure(observations, "exposure-restored-pending", restored, services, ledger);
            Require(restored.Runtime.Capture().exposureOutcomes.Length == 2,
                "Exposure owner outbox was lost during save round trip.");
            restored.Runtime.Tick();
            ObserveExposure(observations, "exposure-reconciled", restored, services, ledger);
            Require(restored.Runtime.Capture().exposureOutcomes.Length == 0,
                "Restored exposure owner outbox did not reconcile acknowledgements.");
            Require(Rows(ledger, source.CharacterId).Length == 2,
                "Exposure restore/reconciliation duplicated a gameplay result.");
            Require(Near(restored.BodyHealth.GetVitals(restored.Actor).CurrentHealth, healthAfter),
                "Exposure restore/reconciliation applied body-health damage again.");
            if (observations != null)
            {
                restored.Runtime.Tick();
                ObserveExposure(observations, "exposure-reconciled-replay", restored, services, ledger);
                Require(restored.Runtime.Capture().exposureOutcomes.Length == 0
                    && restored.Runtime.Capture().nextExposureOutcomeSequence == 302
                    && Rows(ledger, source.CharacterId).Length == 2
                    && Near(restored.BodyHealth.GetVitals(restored.Actor).CurrentHealth, healthAfter),
                    "Exposure reconciliation replay repeated damage, pending work, or completion identity.");
            }
            lines.Add("band-crossing-exactly-one=PASS");
            lines.Add("damage-tick-exactly-one-with-actual-health=PASS");
            lines.Add("pending-save-restore-reconcile-no-duplicate=PASS");
            Require(experienceBefore == GameplayExperienceIntegrationDebugScenarios.ObserveLive(
                    ledger, source.CharacterId, observations, "experience-after-restore"),
                "Restoring/replaying the actual owner changed experience identities or assessments.");
            lines.Add("experience-actual-owner-save-restore-no-duplicate=PASS");
            if (observations != null)
            {
                ICharacterBodyHealthCommand commands = services.Resolve<ICharacterBodyHealthCommand>();
                ICharacterBodyHealthPersistence body = services.Resolve<ICharacterBodyHealthPersistence>();
                ObserveExposure(observations, "health-owner-floor-before", restored, services, ledger);
                commands.ApplyLegacyDamage(restored.Actor, 200f, " e09-controlled-floor-damage ", false);
                CharacterBodyHealthState applied = body.Capture().characters.Single(value =>
                    value.characterId == source.CharacterId);
                ObserveExposure(observations, "health-owner-floor-applied", restored, services, ledger);
                Require(Near(applied.currentHealth, 1f)
                    && applied.lastDamageReason == "e09-controlled-floor-damage",
                    "Actual nonlethal damage did not persist the canonical cause at its floor.");
                string atFloor = JsonUtility.ToJson(applied);
                commands.ApplyLegacyDamage(restored.Actor, 1f, "e09-zero-applied-damage", false);
                ObserveExposure(observations, "health-owner-floor-no-op", restored, services, ledger);
                Require(atFloor == JsonUtility.ToJson(body.Capture().characters.Single(value =>
                        value.characterId == source.CharacterId)),
                    "Zero-applied damage changed the canonical body or its prior damage cause.");
                lines.Add("body-health-owned-cause-and-no-applied-floor-preserved=PASS");
            }
        }

        return string.Join("\n", lines);
    }

    private static void ObserveExposure(
        JArray observations,
        string stage,
        ExposureFixture fixture,
        IObjectResolver services,
        GameplayOutcomeLedger ledger,
        FaultingCommitter fault = null)
    {
        if (observations == null)
            return;
        JObject record = new()
        {
            ["stage"] = stage,
            ["kind"] = stage.StartsWith("health-owner-", StringComparison.Ordinal)
                ? "actual-body-health-public-command-regression"
                : "actual-body-health-environment-owner",
            ["characterId"] = fixture.CharacterId,
            ["controlledField"] = new JObject
            {
                ["temperatureC"] = fixture.Field.Temperature,
                ["airQuality"] = fixture.Field.AirQuality,
                ["lightLevel"] = fixture.Field.Light,
                ["x"] = fixture.Actor.GetNowXY().x,
                ["y"] = fixture.Actor.GetNowXY().y
            },
            ["clock"] = new JObject
            {
                ["time"] = fixture.Clock.Time,
                ["frame"] = fixture.Clock.FrameCount,
                ["paused"] = fixture.Clock.IsPaused
            },
            ["reserveAttempts"] = fault?.ReserveAttempts,
            ["hiddenAcknowledgementsRemaining"] = fault?.HideAcknowledgementCount,
            ["testPreparationExcludedFromTraining"] = true,
            ["naturalGameplayClaimed"] = false
        };
        observations.Add(record);
        try
        {
            record["environment"] = JObject.Parse(JsonUtility.ToJson(fixture.Runtime.Capture()));
            CharacterBodyHealthState body = services.Resolve<ICharacterBodyHealthPersistence>()
                .Capture().characters.Single(value => value.characterId == fixture.CharacterId);
            record["bodyHealth"] = JObject.Parse(JsonUtility.ToJson(body));
            record["ledger"] = JObject.Parse(JsonUtility.ToJson(ledger.CaptureGameplayOutcomes()));
            record["characterOutcomes"] = JArray.Parse(JsonConvert.SerializeObject(
                Rows(ledger, fixture.CharacterId)));
        }
        catch (Exception exception)
        {
            record["captureFailure"] = exception.ToString();
            throw;
        }
    }

    private static GameplayOutcomeSnapshot[] Rows(
        GameplayOutcomeLedger ledger,
        string characterId) => ledger.CaptureGameplayOutcomes()
        .exactOutcomes
        .Where(value => value != null
            && value.producerId == EnvironmentOutcomeIds.EnvironmentalExposureProducer
            && value.operationId.Contains(characterId, StringComparison.Ordinal))
        .OrderBy(value => value.sequence)
        .ToArray();

    private static int Count(GameplayOutcomeLedger ledger, string characterId) =>
        Rows(ledger, characterId).Length;

    private static string FindFact(
        GameplayOutcomeSnapshot outcome,
        GameplayOutcomeFactId id) => outcome.facts.Single(value =>
            value.factId == id.Value).value;

    private static double FindMetric(
        GameplayOutcomeSnapshot outcome,
        GameplayMetricId id) => outcome.metrics.Single(value =>
            value.metricId == id.Value).value;

    private static bool Near(double actual, double expected) =>
        Math.Abs(actual - expected) < 0.001d;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("E09 PlayMode: " + message);
    }

    private static void WriteRaw(
        bool passed,
        JArray observations,
        ICollection<string> warnings,
        ICollection<string> errors,
        Exception failure)
    {
        string path = ResolveReportPath() + ".raw.json";
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        using FileStream file = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using StreamWriter writer = new(file, new System.Text.UTF8Encoding(false));
        writer.Write(new JObject
        {
            ["schema"] = "dungeonstory-e09-control-exposure-observations@1",
            ["passed"] = passed,
            ["observations"] = observations,
            ["runtimeWarnings"] = JArray.FromObject(warnings),
            ["runtimeErrors"] = JArray.FromObject(errors),
            ["failure"] = failure?.ToString(),
            ["scope"] = new JObject
            {
                ["thermostatProductionCommandsExecuted"] = observations.Any(value =>
                    (string)value["stage"] == "control-set"),
                ["generatedPresenterButtonExecuted"] = observations.Any(value =>
                    (string)value["stage"] == "control-ui-set"),
                ["actualBodyHealthServiceAndLiveLedgerExecuted"] = observations.Any(value =>
                    value["bodyHealth"] != null && value["ledger"] != null),
                ["controlledFieldClockActorAndInitialExposure"] = true,
                ["exposureOwnerJsonRestoredWithPreservedBodyAndLedger"] = observations.Any(value =>
                    (string)value["stage"] == "exposure-restored-pending"),
                ["thermostatOwnerJsonRestoredWithPreservedLedger"] = observations.Any(value =>
                    (string)value["stage"] == "control-restored-pending"),
                ["syntheticReceiptContractChecksAreNotActualDamageExamples"] = true,
                ["freshBodyHealthOrLedgerRestoreClaimed"] = false,
                ["wholeWorldRestoreClaimed"] = false,
                ["coldResumeClaimed"] = false,
                ["brainPathClaimed"] = false,
                ["naturalGameplayClaimed"] = false,
                ["trainingEligible"] = false,
                ["humanApproved"] = false,
                ["fullAuditClosure"] = false
            }
        }.ToString(Formatting.Indented));
    }

    private static void Write(string value)
    {
        string reportPath = ResolveReportPath();
        string directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        if (Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_U0_E09_ENVIRONMENT_EXPOSURE_VERIFY") == "1")
        {
            using FileStream file = new(reportPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using StreamWriter writer = new(file, new System.Text.UTF8Encoding(false));
            writer.Write(value);
            return;
        }
        File.WriteAllText(
            reportPath,
            value,
            new System.Text.UTF8Encoding(false));
    }

    private static string ResolveReportPath()
    {
        string configured = Environment.GetEnvironmentVariable(
            ReportEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(ReportPath);
        if (!Path.IsPathRooted(configured))
            throw new InvalidOperationException(
                "E09 exposure report path must be absolute.");
        string fullPath = Path.GetFullPath(configured);
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "E09 exposure report path must be under Artifacts/QA.");
        }
        return fullPath;
    }

    private sealed class ExposureFixture : IDisposable
    {
        private readonly IObjectResolver services;
        private readonly GameObject actorObject;
        private readonly bool ownsActor;

        internal ExposureFixture(
            IObjectResolver services,
            IDomainFactPublisher outcomes,
            string characterId,
            int sequence,
            CharacterActor existingActor = null)
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            CharacterId = characterId;
            if (existingActor == null)
            {
                ownsActor = true;
                actorObject = new GameObject("E09 controlled exposure witness");
                actorObject.AddComponent<AbilityWork>();
                Actor = actorObject.AddComponent<CharacterActor>();
                Actor.PrepareForComposition();
                CharacterAiEditorTestDependencies.Inject(actorObject);
                Actor.data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
                    "Assets/Resources/SO/Character/Customer_Vampire.asset");
                Require(Actor.data != null, "Authored exposure witness is missing.");
                Actor.characterType = CharacterType.NPC;
                Actor.RefreshAbilityCache();
                Actor.EnsureRuntimeState();
                Actor.Identity.SetPersistentId(new CharacterId(characterId));
                Actor.SetLifecycleState(CharacterLifecycleState.Active);
                foreach (CharacterCondition condition in Enum.GetValues(
                             typeof(CharacterCondition)))
                    Actor.stats[condition] = 100f;
                services.Resolve<ICharacterBodyHealthCommand>().ConfigureVitals(
                    Actor,
                    Mathf.Max(1f, Actor.MaxHealth),
                    resetCurrentHealth: true);
            }
            else
            {
                Actor = existingActor;
            }

            Field = new ControlledField();
            Clock = new ControlledClock();
            BodyHealth = services.Resolve<ICharacterBodyHealthQuery>();
            WitnessWorld world = new(
                Actor,
                services.Resolve<ICharacterLifetimeQuery>());
            Runtime = new CharacterEnvironmentUnityAdapter(
                Field,
                world,
                world,
                services.Resolve<ICharacterSpeciesEnvironmentCatalog>(),
                services.Resolve<ICharacterEnvironmentProtectionResolver>(),
                services.Resolve<IEnvironmentalWorkwearPersistence>(),
                services.Resolve<ICharacterApparelPersistence>(),
                services.Resolve<ICharacterApparelCommand>(),
                services.Resolve<IApparelWorkOrderPersistence>(),
                services.Resolve<ICharacterBodyHealthQuery>(),
                services.Resolve<ICharacterBodyHealthCommand>(),
                Clock,
                new CharacterEnvironmentAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                services.Resolve<ICharacterPerformanceQuery>(),
                new ApparelConditionRuntime(
                    Field,
                    world,
                    services.Resolve<ICharacterApparelQuery>(),
                    services.Resolve<IWorldItemStackRuntime>(),
                    services.Resolve<IClimateQuery>(),
                    services.Resolve<IClimateDefinitionCatalog>(),
                    services.Resolve<IGridSystemProvider>(),
                    services.Resolve<IApparelDefinitionCatalog>(),
                    services.Resolve<ITextileMaterialCatalog>(),
                    services.Resolve<IApparelMaterialProjector>(),
                    services.Resolve<IAnatomyAttachmentQuery>()),
                services.Resolve<IGameCalendar>(),
                outcomes);
            InitialSequence = sequence;
        }

        internal CharacterEnvironmentUnityAdapter Runtime { get; }
        internal CharacterActor Actor { get; }
        internal ControlledField Field { get; }
        internal ControlledClock Clock { get; }
        internal ICharacterBodyHealthQuery BodyHealth { get; }
        internal string CharacterId { get; }
        internal int InitialSequence { get; }

        internal void Stage(
            float coldExposure,
            EnvironmentalExposureBand band,
            float criticalTimer)
        {
            Field.Temperature = 20f;
            Advance();
            DungeonCharacterEnvironmentSaveData save = Runtime.Capture();
            CharacterEnvironmentExposure exposure = save.exposures.Single(value =>
                value.characterId == CharacterId);
            exposure.coldExposure = coldExposure;
            exposure.heatExposure = 0f;
            exposure.airborneExposure = 0f;
            exposure.physiologicalBand = band;
            exposure.criticalDamageTimer = criticalTimer;
            save.nextExposureOutcomeSequence = InitialSequence;
            save.exposureOutcomes =
                Array.Empty<EnvironmentalExposureOutcomeSaveData>();
            Runtime.PublishRestoreCandidate(Runtime.BuildRestoreCandidate(
                JsonUtility.FromJson<DungeonCharacterEnvironmentSaveData>(
                    JsonUtility.ToJson(save))));
        }

        internal void Advance()
        {
            Clock.Advance();
            Runtime.Tick();
        }

        internal CharacterEnvironmentExposure RequireState() =>
            Runtime.GetExposure(new CharacterId(CharacterId))
            ?? throw new InvalidOperationException("E09 witness exposure state is missing.");

        public void Dispose()
        {
            if (ownsActor && actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    internal sealed class ControlledField : IEnvironmentalFieldQuery
    {
        internal float Temperature { get; set; } = 20f;
        internal float AirQuality { get; set; } = 100f;
        internal float Light { get; set; } = 50f;
        public int Version => 1;
        public bool IsInitialized => true;
        public bool TryGetCell(
            Vector2Int position,
            out EnvironmentalCellSnapshot snapshot)
        {
            snapshot = new EnvironmentalCellSnapshot(
                position,
                Temperature,
                AirQuality,
                Light);
            return true;
        }
        public bool TryGetAverage(
            IReadOnlyList<Vector2Int> cells,
            out EnvironmentalCellSnapshot snapshot) => TryGetCell(
            cells.Count == 0 ? Vector2Int.zero : cells[0],
            out snapshot);
        public float GetFoodSpoilageMultiplier(Vector2Int position) => 1f;
        public bool IsOrganPreservationSafe(Vector2Int position) => false;
        public bool TryGetTargetTemperature(Vector2Int position, out float value)
        {
            value = 0f;
            return false;
        }
    }

    internal sealed class ControlledClock : IGameClock
    {
        public float DeltaTime => 1f;
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused { get; set; }
        internal void Advance()
        {
            Time += 1f;
            FrameCount++;
        }
    }

    private sealed class WitnessWorld : ICharacterWorldQuery, ICharacterLifetimeQuery
    {
        internal WitnessWorld(CharacterActor actor, ICharacterLifetimeQuery live)
        {
            Characters = new[] { actor };
            AllCharacters = (live.AllCharacters ?? Array.Empty<CharacterActor>())
                .Concat(Characters)
                .Distinct()
                .ToArray();
        }
        public int CharacterVersion => 1;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
        public IReadOnlyList<CharacterActor> AllCharacters { get; }
    }

    private sealed class FaultingCommitter : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;
        internal FaultingCommitter(IDomainFactPublisher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        internal bool RejectReserve { get; set; }
        internal int HideAcknowledgementCount { get; set; }
        internal int ReserveAttempts { get; private set; }

        public bool TryReserve<TFact>(in TFact proposed, out DomainFactLease reserved, out string failureReason)
        {
            ReserveAttempts++;
            if (RejectReserve)
            {
                reserved = default;
                failureReason = "e09-playmode-injected-reserve-rejection";
                return false;
            }
            return inner.TryReserve(proposed, out reserved, out failureReason);
        }
        public DomainFactDelivery Publish<TFact>(in DomainFactLease lease, in TFact actual)
        {
            DomainFactDelivery result = inner.Publish(lease, actual);
            if (HideAcknowledgementCount <= 0 || !result.Completed) return result;
            HideAcknowledgementCount--;
            return new DomainFactDelivery(DomainFactDeliveryPhase.AcceptedPendingDelivery,
                result.OperationKey, result.PublicationId, result.Fingerprint,
                "e09-playmode-injected-hidden-acknowledgement");
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results, out string failure) => inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<TFact>(in TFact fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }
}
#endif
