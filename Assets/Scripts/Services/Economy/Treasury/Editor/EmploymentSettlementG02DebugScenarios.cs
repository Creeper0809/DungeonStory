#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Infrastructure;
using DungeonStory.Operation;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class EmploymentSettlementG02DebugScenarios
{
    private const string PaidEmployeeId = "character:g02:employee-paid";
    private const string UnpaidEmployeeId = "character:g02:employee-unpaid";
    private const string RenewedMercenaryId = "character:g02:mercenary-a-renewed";
    private const string DepartingMercenaryId = "character:g02:mercenary-z-departing";
    private const MigratedProducerOutcomeKind OutcomeKind =
        MigratedProducerOutcomeKind.OperatingDayReportEvent;

    public static void RunG02FromBatch()
    {
        string reportPath = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_G02_EMPLOYMENT_SETTLEMENT_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            throw new InvalidOperationException(
                "G02 report path environment variable is required.");
        }
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "G02 report path already exists: " + reportPath);
        }

        List<CharacterActor> actors = new List<CharacterActor>();
        try
        {
            CharacterActor paid = CreateActor(
                4201,
                PaidEmployeeId,
                "G02 Paid Employee",
                actors);
            CharacterActor unpaid = CreateActor(
                4202,
                UnpaidEmployeeId,
                "G02 Unpaid Employee",
                actors);
            CharacterActor renewed = CreateActor(
                4203,
                RenewedMercenaryId,
                "G02 Renewed Mercenary",
                actors);
            CharacterActor departing = CreateActor(
                4204,
                DepartingMercenaryId,
                "G02 Departing Mercenary",
                actors);
            float unpaidMoodBefore = unpaid.Stats.Mood;

            string[] persistentIds =
            {
                PaidEmployeeId,
                UnpaidEmployeeId,
                RenewedMercenaryId,
                DepartingMercenaryId
            };
            MutableCharacterWorld firstWorld = new MutableCharacterWorld(
                new[] { paid, unpaid, renewed },
                persistentIds);
            DungeonRuntimeAggregateRootStore firstRoot =
                new DungeonRuntimeAggregateRootStore();
            GameSessionState firstSession = new GameSessionState(
                startingMoney: 96,
                startingDay: 2,
                startingGameSpeed: 1);

            using G02Scenario first = new G02Scenario(
                "first",
                firstWorld,
                firstRoot,
                firstSession,
                seedEmployment: true);
            first.Settle(2);

            OperatingDayReport firstReport = first.LatestReport;
            Require(firstReport != null, "G02 first operating-day report is missing.");
            Require(firstReport.payrollCost == 292
                && firstReport.paidOperatingCost == 96
                && firstReport.unpaidOperatingCost == 132
                && firstReport.terminatedContractCost == 64
                && firstReport.closingBalance == 0,
                "G02 aggregate employment amounts do not reconcile.");
            Require(firstReport.employmentSettlements.Count == 4,
                "G02 did not retain four individual employment lines.");

            OperatingDayEmploymentSettlementLine paidLine = RequireLine(
                firstReport,
                PaidEmployeeId);
            OperatingDayEmploymentSettlementLine unpaidLine = RequireLine(
                firstReport,
                UnpaidEmployeeId);
            OperatingDayEmploymentSettlementLine renewedLine = RequireLine(
                firstReport,
                RenewedMercenaryId);
            OperatingDayEmploymentSettlementLine departingLine = RequireLine(
                firstReport,
                DepartingMercenaryId);
            Require(paidLine.due == 32
                && paidLine.paid == 32
                && paidLine.financeSucceeded,
                "G02 paid employee line is incorrect.");
            Require(unpaidLine.due == 132
                && unpaidLine.paid == 0
                && unpaidLine.unpaidDebtAfter == 132
                && unpaidLine.moodPenaltyApplied
                && !unpaidLine.financeSucceeded
                && unpaid.Stats.Mood < unpaidMoodBefore,
                "G02 unpaid employee debt or mood consequence is incorrect.");
            Require(renewedLine.due == 64
                && renewedLine.paid == 64
                && renewedLine.financeSucceeded
                && !renewedLine.contractEnded,
                "G02 mercenary renewal line is incorrect.");
            Require(departingLine.due == 64
                && departingLine.paid == 0
                && departingLine.contractEnded
                && departingLine.departureAttempted
                && departingLine.departurePending
                && !departingLine.departureStarted
                && departingLine.departureFailureReason
                    == "employment-departure-actor-missing"
                && !departingLine.financeSucceeded,
                "G02 failed departure was not explicit and pending.");
            Require(first.LedgerRecords.Count == 4
                && first.LedgerRecords.Count(value => value.succeeded) == 2
                && first.LedgerRecords.Count(value => !value.succeeded) == 2,
                "G02 finance child ledger count is incorrect.");
            RequireFinanceJoins(firstReport, first.LedgerRecords);
            string firstSummary = first.RequireOutcomeSummary();
            Require(firstReport.employmentSettlements.All(value =>
                    firstSummary.Contains(
                        value.financeTransactionId,
                        StringComparison.Ordinal)),
                "G02 operating-day outcome lost a finance child identity.");

            int ledgerCountBeforeReplay = first.LedgerRecords.Count;
            EmploymentDailySettlement sameDayReplay =
                first.Employment.SettleDay(2);
            Require(sameDayReplay.lines.Count == 1
                && sameDayReplay.lines[0].departurePending
                && first.LedgerRecords.Count == ledgerCountBeforeReplay,
                "G02 same-day retry duplicated a finance transaction.");

            string firstSettlementJson = first.CaptureSettlementPayload();
            first.RestoreSettlementPayload(firstSettlementJson);
            Require(RequireLine(first.LatestReport, DepartingMercenaryId)
                    .financeTransactionId
                == departingLine.financeTransactionId,
                "G02 operating-day report lost its finance child after JSON restore.");

            EmploymentContractSaveData employmentSave =
                first.Employment.Capture();
            string employmentJson = JsonUtility.ToJson(employmentSave);
            EmploymentContractSaveData employmentRoundTrip =
                JsonUtility.FromJson<EmploymentContractSaveData>(employmentJson);
            EmployeeWageState savedDeparture = employmentRoundTrip.wageStates
                .Single(value => value.characterId == DepartingMercenaryId);
            Require(savedDeparture.departurePending
                && !savedDeparture.departed
                && savedDeparture.departureContractDay == 2
                && savedDeparture.departureFinanceTransactionId
                    == departingLine.financeTransactionId,
                "G02 pending departure did not survive employment JSON.");

            TreasuryEconomyAggregateState restoredTreasury =
                first.TreasurySnapshot;
            restoredTreasury.Wages.Clear();
            foreach (EmployeeWageState state in employmentRoundTrip.wageStates)
                restoredTreasury.Wages.Add(state.characterId, state.Clone());
            restoredTreasury.Mercenaries.Clear();
            foreach (MercenaryContract contract in
                     employmentRoundTrip.mercenaryContracts)
            {
                restoredTreasury.Mercenaries.Add(
                    contract.characterId,
                    contract.Clone());
            }

            AbilityMove departingMove = departing.GetComponent<AbilityMove>();
            if (departingMove != null)
                Object.DestroyImmediate(departingMove);
            departing.RefreshAbilityCache();
            MutableCharacterWorld restoredWorld = new MutableCharacterWorld(
                new[] { paid, unpaid, renewed, departing },
                persistentIds);
            DungeonRuntimeAggregateRootStore restoredRoot =
                new DungeonRuntimeAggregateRootStore();
            new TreasuryEconomyAggregateStateStore(restoredRoot)
                .Replace(restoredTreasury);
            GameSessionState restoredSession = new GameSessionState(
                startingMoney: 0,
                startingDay: 2,
                startingGameSpeed: 1);

            using G02Scenario restored = new G02Scenario(
                "restored",
                restoredWorld,
                restoredRoot,
                restoredSession,
                seedEmployment: false);
            restored.Settle(2);
            OperatingDayReport restoredReport = restored.LatestReport;
            Require(restoredReport != null
                && restoredReport.employmentSettlements.Count == 1,
                "G02 restored retry report is missing its departure line.");
            OperatingDayEmploymentSettlementLine resumed = RequireLine(
                restoredReport,
                DepartingMercenaryId);
            Require(resumed.due == 0
                && resumed.contractEnded
                && resumed.departureAttempted
                && !resumed.departureStarted
                && resumed.departurePending
                && resumed.departureFailureReason == "employment-departure-awaiting-settlement"
                && resumed.financeTransactionId
                    == departingLine.financeTransactionId
                && departing.CurrentLifecycleState
                    == CharacterLifecycleState.ExitingDungeon,
                "G02 restored pending departure did not start exactly once.");
            Require(restored.LedgerRecords.Count == ledgerCountBeforeReplay,
                "G02 restored departure retry duplicated a finance child.");
            Require(restored.RequireOutcomeSummary().Contains(
                    departingLine.financeTransactionId,
                    StringComparison.Ordinal),
                "G02 resumed departure outcome lost its original finance child.");
            EmployeeWageState finalDeparture = restored.Employment.WageStates
                .Single(value => value.characterId == DepartingMercenaryId);
            Require(finalDeparture.departed
                && !finalDeparture.departurePending
                && finalDeparture.departureFailureReason.Length == 0,
                "G02 final departure state is not terminal.");

            string restoredSettlementJson = restored.CaptureSettlementPayload();
            restored.RestoreSettlementPayload(restoredSettlementJson);
            Require(RequireLine(restored.LatestReport, DepartingMercenaryId)
                    .departurePending,
                "G02 admission-time pending report did not survive JSON restore.");

            WriteReport(reportPath, new[]
            {
                "U0_G02_EMPLOYMENT_SETTLEMENT=PASS",
                "day=2",
                "opening-balance=96",
                "closing-balance=0",
                "payroll-due=292",
                "paid=96",
                "employee-debt=132",
                "terminated-contract-unpaid=64",
                "finance-child-count=4",
                "finance-child-success=2",
                "finance-child-failure=2",
                "same-day-finance-replay=0",
                "departure-first=pending:employment-departure-actor-missing",
                "departure-restored=started",
                "departure-finance-transaction="
                    + departingLine.financeTransactionId,
                "operating-day-json-roundtrip=true",
                "employment-json-roundtrip=true",
                "human-approval-claimed=false",
                "training-claimed=false",
                "full-audit-closure-claimed=false"
            });
            Debug.Log("U0_G02_EMPLOYMENT_SETTLEMENT=PASS");
        }
        catch (Exception exception)
        {
            if (!File.Exists(reportPath))
            {
                WriteReport(reportPath, new[]
                {
                    "U0_G02_EMPLOYMENT_SETTLEMENT=FAIL",
                    "exception=" + exception.GetType().FullName,
                    "message=" + exception.Message,
                    "human-approval-claimed=false",
                    "training-claimed=false",
                    "full-audit-closure-claimed=false"
                });
            }
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            foreach (CharacterActor actor in actors)
                DestroyActor(actor);
        }
    }

    public static void RunOperatingDayEmploymentObserverOrThrow()
    {
        List<CharacterActor> actors = new List<CharacterActor>();
        try
        {
            CharacterActor paid = CreateActor(
                4211,
                PaidEmployeeId,
                "G02 Observer Paid Employee",
                actors);
            CharacterActor unpaid = CreateActor(
                4212,
                UnpaidEmployeeId,
                "G02 Observer Unpaid Employee",
                actors);
            CharacterActor renewed = CreateActor(
                4213,
                RenewedMercenaryId,
                "G02 Observer Renewed Mercenary",
                actors);
            CharacterActor departing = CreateActor(
                4214,
                DepartingMercenaryId,
                "G02 Observer Departing Mercenary",
                actors);
            float unpaidMoodBefore = unpaid.Mood.Value;
            MutableCharacterWorld world = new MutableCharacterWorld(
                new[] { paid, unpaid, renewed },
                new[]
                {
                    PaidEmployeeId,
                    UnpaidEmployeeId,
                    RenewedMercenaryId,
                    DepartingMercenaryId
                });
            var root = new DungeonRuntimeAggregateRootStore();
            var session = new GameSessionState(
                startingMoney: 96,
                startingDay: 2,
                startingGameSpeed: 1);

            using var scenario = new G02Scenario(
                "observer",
                world,
                root,
                session,
                seedEmployment: true);
            scenario.Probe.FaultMode =
                MigratedProducerOutcomeFaultMode.RejectCommit;
            bool pendingThrown = false;
            try
            {
                scenario.Settle(2);
            }
            catch (InvalidOperationException error)
            {
                pendingThrown = error.Message.Contains(
                    "Operating-day report observation pending:",
                    StringComparison.Ordinal);
                if (!pendingThrown)
                    throw;
            }

            OperatingDayReport report = scenario.LatestReport;
            OperatingDayFactJournalData pending =
                scenario.Facts.Journal.Capture();
            Require(
                pendingThrown
                && report != null
                && report.paidOperatingCost == 96
                && report.unpaidOperatingCost == 132
                && report.terminatedContractCost == 64
                && scenario.Money == 0
                && scenario.LedgerRecords.Count == 4
                && pending.pending.Count == 1
                && scenario.Probe.CommitCount(OutcomeKind) == 0,
                "G02 observer rejection did not preserve the completed settlement and one frozen observation.");
            OperatingDayEmploymentSettlementLine unpaidLine = RequireLine(
                report,
                UnpaidEmployeeId);
            OperatingDayEmploymentSettlementLine departingLine = RequireLine(
                report,
                DepartingMercenaryId);
            Require(
                unpaidLine.unpaidDebtAfter == 132
                && unpaidLine.moodPenaltyApplied
                && unpaid.Mood.Value < unpaidMoodBefore
                && unpaid.Mood.Factors.Count(value =>
                    value.Id == "economy:unpaid-wages") == 1
                && departingLine.contractEnded
                && departingLine.due == 64
                && departingLine.paid == 0,
                "G02 observer rejection lost the unpaid-mood or ended-contract effects.");
            RequireFinanceJoins(report, scenario.LedgerRecords);
            OperatingDayReportAlertObserverDebugScenarios.RunOrThrow(report);

            string pendingKey = pending.pending[0].publicationKey;
            Require(
                GameplayDomainFactDeliveryQuery.TryReadKey(
                    pendingKey,
                    out GameplayResultKey key)
                && !scenario.OutcomeLedger.TryGetResultIdentity(key, out _),
                "G02 rejected observation unexpectedly created a native outcome.");
            int moneyBeforeRetry = scenario.Money;
            int financeCountBeforeRetry = scenario.LedgerRecords.Count;
            float moodBeforeRetry = unpaid.Mood.Value;
            string employmentBeforeRetry = JsonUtility.ToJson(
                scenario.Employment.Capture());

            scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            scenario.Settle(2);

            GameplayOutcomeLedgerSaveData ledger =
                scenario.OutcomeLedger.CaptureGameplayOutcomes();
            Require(
                scenario.Facts.Journal.Capture().pending.Count == 0
                && scenario.Money == moneyBeforeRetry
                && scenario.LedgerRecords.Count == financeCountBeforeRetry
                && Mathf.Approximately(unpaid.Mood.Value, moodBeforeRetry)
                && JsonUtility.ToJson(scenario.Employment.Capture())
                    == employmentBeforeRetry
                && scenario.ReportCount == 1
                && ledger.outbox.Count == 0
                && ledger.exactOutcomes.Count == 1
                && scenario.OutcomeLedger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && scenario.OutcomeLedger.TryGetExact(
                    identity.OutcomeId,
                    out _),
                "G02 observer retry repeated owner effects or failed exact one-time recovery.");
            MigratedProducerEconomyEntryExtendedE2EDebugScenarios.VerifyOperatingDayEmploymentSeal(
                pending, ledger, scenario.OutcomeOwners,
                DungeonSaveJson.Deserialize<DungeonOperatingDaySettlementSaveData>(scenario.CaptureSettlementPayload()));
        }
        finally
        {
            foreach (CharacterActor actor in actors)
                DestroyActor(actor);
        }
    }

    private static OperatingDayEmploymentSettlementLine RequireLine(
        OperatingDayReport report,
        string characterId) => report.employmentSettlements.Single(value =>
            string.Equals(
                value.characterId,
                characterId,
                StringComparison.Ordinal));

    private static void RequireFinanceJoins(
        OperatingDayReport report,
        IReadOnlyList<EconomyTransactionRecord> records)
    {
        foreach (OperatingDayEmploymentSettlementLine line in
                 report.employmentSettlements)
        {
            EconomyTransactionRecord record = records.Single(value =>
                string.Equals(
                    value.transactionId,
                    line.financeTransactionId,
                    StringComparison.Ordinal));
            Require(record.sourceId == line.financeSourceId
                && record.targetId == line.characterId
                && record.succeeded == line.financeSucceeded
                && record.amount == -line.due,
                "G02 employment line disagrees with its finance child.");
        }
    }

    private static CharacterActor CreateActor(
        int numericId,
        string persistentId,
        string displayName,
        ICollection<CharacterActor> ownership)
    {
        CharacterSO data = CharacterAiEditorTestDependencies
            .CreateCharacterFixtureData(
                CharacterType.NPC,
                displayName,
                "Orc");
        data.id = numericId;
        data.characterType = CharacterType.NPC;
        data.role = CharacterRole.Regular;
        data.characterName = displayName;
        data.speciesTag = "Orc";
        data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

        GameObject obj = new GameObject(displayName);
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityShopping>();
        obj.AddComponent<AbilityWork>();
        AIBrain brain = obj.AddComponent<AIBrain>();
        brain.availableActions = AiDebugScenarioActionFactory
            .CreateStaffActions();
        CharacterAiEditorTestDependencies.Inject(obj);
        CharacterActor actor = obj.GetComponent<CharacterActor>();
        typeof(CharacterActor)
            .GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(actor, null);
        actor.RefreshAbilityCache();
        actor.Initialization(data);
        actor.Identity.SetPersistentId(persistentId);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        actor.stats[CharacterCondition.MOOD] = 50f;
        ownership.Add(actor);
        return actor;
    }

    private static void DestroyActor(CharacterActor actor)
    {
        if (actor == null)
            return;
        if (actor.data != null)
            Object.DestroyImmediate(actor.data);
        Object.DestroyImmediate(actor.gameObject);
    }

    private static void WriteReport(string path, IEnumerable<string> lines)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("G02 report directory is invalid.");
        Directory.CreateDirectory(directory);
        File.WriteAllLines(fullPath, lines);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class G02Scenario : IDisposable
    {
        private readonly GameObject runtimeObject;
        private readonly TestSessionStore sessionStore;
        private readonly TreasuryEconomyAggregateStateStore treasury;
        private readonly EconomyTransactionLedgerRuntime ledger;
        private readonly GameMoneyAccount money;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly OperatingDayFactEditorFixture facts;
        private readonly OperatingDaySettlementRuntime runtime;

        internal G02Scenario(
            string suffix,
            MutableCharacterWorld world,
            DungeonRuntimeAggregateRootStore root,
            GameSessionState session,
            bool seedEmployment)
        {
            sessionStore = new TestSessionStore(session);
            treasury = new TreasuryEconomyAggregateStateStore(root);
            ledger = new EconomyTransactionLedgerRuntime(
                new FixedClock(),
                treasury);
            money = new GameMoneyAccount(
                sessionStore,
                ledger);
            Events = new GameEventBus();
            Employment = new EmploymentContractRuntime(
                world,
                world,
                OffenseEditorTestDependencies
                    .CreateCombatEquipmentRuntime(),
                money,
                Events,
                treasury);
            if (seedEmployment)
                SeedEmployment(treasury.Current);

            PaidFacilityContractRuntime paidFacilities =
                new PaidFacilityContractRuntime(
                    sessionStore,
                    money,
                    treasury);
            var paidPort = PaidSettlementFaultProxy.Create(paidFacilities, out var paidFault);
            PaidFault = paidFault;
            outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:g02-employment:" + suffix + ":"
                + Guid.NewGuid().ToString("N"));
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder)
            {
                TargetKind = OutcomeKind
            };
            facts = new OperatingDayFactEditorFixture(outcomes, probe);
            runtimeObject = new GameObject(
                "G02 Employment Settlement " + suffix);
            runtime = runtimeObject.AddComponent<
                OperatingDaySettlementRuntime>();
            runtime.Construct(
                EmptyBuildingWorld.Instance,
                world,
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IFacilityShopCatalog>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios
                    .DefaultProxy.Create<IRunVariableRuntimeReader>(),
                sessionStore,
                Events,
                Employment,
                money,
                paidPort,
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                root);
            runtime.ConstructSettlementFacts(
                facts.Journal,
                sessionStore,
                treasury);
        }

        internal EmploymentContractRuntime Employment { get; }
        internal GameEventBus Events { get; }
        internal PaidSettlementFaultProxy PaidFault { get; }

        internal AutoProcurementRuntime CreateProcurement(DomainFactDispatcher dispatcher,
            IDomainFactPublisher publisher)
        {
            var commits = new GameplayDomainFactCommitGroup(outcomes.Recorder, outcomes.Ledger);
            dispatcher.Subscribe("fixture:b104-procurement",
                new MigratedProducerDomainFactProbeObserver<AutoProcurementCompletedFact>(
                    AutoProcurementFactMapping.Capture, outcomes.Transaction, commits, probe));
            var result = new AutoProcurementRuntime(sessionStore,
                new AutoProcurementFinancialDependencies(money, Employment, PaidFault.Port),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy.Create<IRunVariableRuntimeReader>(),
                new AutoProcurementStockDependencies(
                    MigratedProducerEconomyEntryExtendedE2EDebugScenarios.StatefulWorldItemStackProxy.Create(out _),
                    CharacterAiEditorTestDependencies.AuthoredGameplay),
                treasury, publisher, sessionStore);
            result.ConfigureBudget(0, 0);
            return result;
        }
        internal OperatingDayReport LatestReport => runtime.LatestReport;
        internal int ReportCount => runtime.ReportHistory.Count;
        internal int Money => money.Balance;
        internal IReadOnlyList<EconomyTransactionRecord> LedgerRecords =>
            ledger.Records;
        internal OperatingDayFactEditorFixture Facts => facts;
        internal DungeonMigratedProducerOutcomeSaveData OutcomeOwners => outcomes.Transaction.Capture();
        internal MigratedProducerOutcomeProbeTransaction Probe => probe;
        internal GameplayOutcomeLedger OutcomeLedger => outcomes.Ledger;
        internal TreasuryEconomyAggregateState TreasurySnapshot =>
            treasury.Current.Copy();

        internal void Settle(int day)
        {
            runtime.OnTriggerEvent(new OperatingDayStartedEvent(day));
            runtime.OnTriggerEvent(new OperatingDayEndedEvent(day));
        }

        internal string RequireOutcomeSummary()
        {
            GameplayResultKey key = probe.LatestCommittedKey(OutcomeKind);
            GameplayOutcomeSnapshot exact = null;
            Require(key.IsValid, "G02 operating-day result key is missing.");
            Require(outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out exact),
                "G02 operating-day exact outcome is missing.");
            return exact.facts.Single(value => string.Equals(
                value.factId,
                "summary",
                StringComparison.Ordinal)).value;
        }

        internal string CaptureSettlementPayload() =>
            CreateSaveSection().Capture();

        internal void RestoreSettlementPayload(string payload)
        {
            DungeonGameRestoreReport report = new DungeonGameRestoreReport();
            CreateSaveSection().Restore(
                payload,
                OperatingDaySettlementSaveSection.CurrentVersion,
                report);
            Require(report.Success,
                "G02 operating-day current JSON restore failed.");
        }

        private OperatingDaySettlementSaveSection CreateSaveSection()
        {
            DungeonSceneRuntimeReferences references =
                new DungeonSceneRuntimeReferences(
                    new DungeonSceneServiceReferences(
                        null,
                        runtime,
                        null,
                        null),
                    new DungeonSceneViewReferences(
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null));
            return new OperatingDaySettlementSaveSection(
                new OperatingDaySettlementSaveService(references));
        }

        public void Dispose()
        {
            facts?.Dispose();
            if (runtimeObject != null)
                Object.DestroyImmediate(runtimeObject);
        }

        private static void SeedEmployment(
            TreasuryEconomyAggregateState state)
        {
            state.Wages.Add(PaidEmployeeId, Employee(
                PaidEmployeeId,
                rolePremium: 0));
            state.Wages.Add(UnpaidEmployeeId, Employee(
                UnpaidEmployeeId,
                rolePremium: 100));
            state.Wages.Add(RenewedMercenaryId, MercenaryWage(
                RenewedMercenaryId));
            state.Wages.Add(DepartingMercenaryId, MercenaryWage(
                DepartingMercenaryId));
            state.Mercenaries.Add(
                RenewedMercenaryId,
                MercenaryContractState(RenewedMercenaryId));
            state.Mercenaries.Add(
                DepartingMercenaryId,
                MercenaryContractState(DepartingMercenaryId));
        }

        private static EmployeeWageState Employee(
            string id,
            int rolePremium) => new EmployeeWageState
        {
            characterId = id,
            contractKind = EmploymentContractKind.Employee,
            rolePremium = rolePremium,
            lastSettledDay = 1,
            active = true
        };

        private static EmployeeWageState MercenaryWage(string id) =>
            new EmployeeWageState
            {
                characterId = id,
                contractKind = EmploymentContractKind.Mercenary,
                lastSettledDay = 1,
                active = true
            };

        private static MercenaryContract MercenaryContractState(string id) =>
            new MercenaryContract
            {
                characterId = id,
                hiredDay = 1,
                lastRenewedDay = 1,
                active = true
            };
    }

    private sealed class MutableCharacterWorld :
        ICharacterWorldQuery,
        ICharacterWorldPersistenceIdentityQuery
    {
        private readonly CharacterActor[] characters;
        private readonly CharacterId[] persistentIds;

        internal MutableCharacterWorld(
            IEnumerable<CharacterActor> characters,
            IEnumerable<string> persistentIds)
        {
            this.characters = characters.ToArray();
            this.persistentIds = persistentIds
                .Select(value => (CharacterId)value)
                .ToArray();
        }

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => characters;
        public IReadOnlyCollection<CharacterId> GetPersistentCharacterIds() =>
            persistentIds;
        public IReadOnlyCollection<CharacterId> GetPersistentActorIds() =>
            persistentIds;
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        internal static readonly EmptyBuildingWorld Instance = new();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    private sealed class TestSessionStore : IGameSessionStateStore
    {
        private readonly GameSessionState state;

        internal TestSessionStore(GameSessionState state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = state;
            return true;
        }

        public void Restore(GameSessionSnapshot snapshot)
        {
            state.holdingMoney.Initialize(snapshot.Money);
            state.day.Initialize(snapshot.Day);
            state.gameSpeed.Initialize(snapshot.GameSpeed);
            state.curTime.Initialize(snapshot.ElapsedSeconds);
            state.hour.Initialize(snapshot.Hour);
            state.timeOfDay.Initialize(snapshot.TimeOfDay);
        }
    }

    private sealed class FixedClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 20f;
        public int FrameCount => 20;
        public bool IsPaused => false;
    }
}
#endif
