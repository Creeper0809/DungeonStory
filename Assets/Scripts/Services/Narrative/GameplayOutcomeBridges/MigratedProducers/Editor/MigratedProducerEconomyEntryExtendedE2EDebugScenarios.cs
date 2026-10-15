#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Infrastructure;
using DungeonStory.Operation;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class MigratedProducerEconomyEntryExtendedE2EDebugScenarios
{
    private static JArray e06RawSnapshots;
    private static JArray t01RawSnapshots;

    public static void RunOperatingDayE06FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_E06_OPERATING_DAY_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_E06_OPERATING_DAY_REPORT_PATH is required.");
        }
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException(
                "Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string rawPath = Path.ChangeExtension(fullPath, "raw.json");
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath)
            || File.Exists(rawPath))
        {
            throw new InvalidOperationException(
                "E06 operating-day report must be a create-only path under Artifacts/QA.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "E06 report directory is unavailable."));
        JObject rawEvidence = new JObject
        {
            ["schema"] = "dungeonstory-e06-operating-day-raw@1",
            ["passed"] = false,
            ["moneyAuthority"] = "GameMoneyAccount/EconomyTransactionLedgerRuntime",
            ["fixtureInputs"] = "explicit day/facility contract/detailed ledger; controlled world/employment/fault ports",
            ["wholeWorldExecuted"] = false,
            ["coldProcessRestoreExecuted"] = false,
            ["brainExecuted"] = false,
            ["naturalPlay"] = false,
            ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false,
            ["fullAuditClosure"] = false
        };
        e06RawSnapshots = new JArray();
        try
        {
            OperatingDaySettlementDebugScenarios.RunE06CoreOrThrow();
            VerifyOperatingDayReportEntry();
            Require(e06RawSnapshots.Count == 13,
                "E06 did not observe all thirteen settlement/save/replay/failure boundaries.");
            rawEvidence["passed"] = true;
        }
        catch (Exception error)
        {
            rawEvidence["failure"] = error.ToString();
            throw;
        }
        finally
        {
            rawEvidence["publicEventCollection"] =
                OperatingDaySettlementDebugScenarios.LastE06RawEvidence?.DeepClone()
                ?? JValue.CreateNull();
            rawEvidence["snapshots"] = e06RawSnapshots.DeepClone();
            e06RawSnapshots = null;
            using FileStream rawStream = new(rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using StreamWriter rawWriter = new(rawStream);
            rawWriter.Write(rawEvidence.ToString(Formatting.Indented));
        }
        using FileStream report = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(report);
        writer.Write(
            "U0_E06_OPERATING_DAY=PASS\n"
            + "runtimeEntry=OperatingDaySettlementApplicationAdapter.OnTriggerEvent\n"
            + "facilityDetail=persistent-id,revenue,cost,item,quantity,visits,satisfaction\n"
            + "settlement=money-and-report-atomic,reservation-rejection-no-effect,observation-rejection-pending,retry-no-duplicate\n"
            + "persistence=report-id-and-facility-detail-round-trip\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_E06_OPERATING_DAY=PASS; report=" + fullPath);
    }

    private static void RecordE06Evidence(OperatingDayScenario scenario,
        string caseId, string boundary, string savedCheckpoint = null)
    {
        if (e06RawSnapshots == null)
            return;
        e06RawSnapshots.Add(new JObject
        {
            ["sequence"] = e06RawSnapshots.Count,
            ["caseId"] = caseId,
            ["boundary"] = boundary,
            ["money"] = scenario.Money,
            ["reportCount"] = scenario.ReportCount,
            ["checkpoint"] = JObject.Parse(scenario.CaptureCheckpoint()),
            ["savedCheckpoint"] = savedCheckpoint == null ? JValue.CreateNull() : JObject.Parse(savedCheckpoint)
        });
    }

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        new[]
        {
            MigratedProducerOutcomeKind.AutoProcurementResult,
            MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult,
            MigratedProducerOutcomeKind.FacilityCrimeEvent,
            MigratedProducerOutcomeKind.FacilityShopPurchaseResult,
            MigratedProducerOutcomeKind.OperatingDayReportEvent,
            MigratedProducerOutcomeKind.StockSupplyResult,
            MigratedProducerOutcomeKind.RegionalSupplyContractResult
        };

    internal static IProductionCausalActionFixture CreateCausalEconomyFixture(
        string causalRunId = null,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        const string ActionId = "action:purchase-retail-item";
        const string OwnerKind = "retail-facility";
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult;
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "A retail owner checkpoint requires a stable owner ID.",
                nameof(ownerCheckpoint));
        }
        string ledgerRunId = persistent
            ? ProductionCausalOwnerCheckpoint.LedgerRunId(ownerId, OwnerKind)
            : string.IsNullOrWhiteSpace(causalRunId)
                ? null
                : causalRunId + ":economy-ledger";
        var scenario = new RetailScenario(
            Kind,
            crime: false,
            faultMode: MigratedProducerOutcomeFaultMode.None,
            deliveryFault: false,
            stableRunId: ledgerRunId,
            ownerId: persistent ? ownerId : null);
        try
        {
            if (ownerCheckpoint != null)
            {
                JObject domainState = ProductionCausalOwnerCheckpoint.RestoreCommon(
                    ownerCheckpoint,
                    ownerId,
                    OwnerKind,
                    ActionId,
                    scenario.Outcomes.Ledger,
                    scenario.Outcomes.Transaction);
                scenario.RestoreOwnerCheckpoint(domainState);
            }
        }
        catch
        {
            scenario.Dispose();
            throw;
        }
        return new DelegateProductionCausalActionFixture(
            ActionId,
            "economy",
            Kind,
            () => new Dictionary<string, object>
            {
                ["economy.stockCount"] = scenario.StockCount,
                ["economy.actorMoney"] = scenario.HoldingMoney,
                ["economy.purchaseCount"] = scenario.CommittedPurchaseCount
            },
            () =>
            {
                scenario.Purchase();
                return scenario.PurchaseCommitted;
            },
            () => MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe),
            identity => ProductionCausalOutcomeEvidence.Capture(
                scenario.Outcomes.Ledger,
                identity),
            scenario.Dispose,
            persistent ? ownerId : null,
            persistent ? OwnerKind : null,
            persistent
                ? () => ProductionCausalOwnerCheckpoint.Capture(
                    ownerId,
                    OwnerKind,
                    ActionId,
                    scenario.CaptureOwnerCheckpoint(),
                    scenario.Outcomes.Ledger,
                    scenario.Outcomes.Transaction)
                : null);
    }

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyAutoProcurementEntry();
            VerifyBuildingRetailPurchaseEntry();
            VerifyRetailRevenueSkillProvenance();
            VerifyFacilityCrimeEntry();
            RunLiveCheckoutRejectionProbeBatch();
            VerifyFacilityShopPurchaseEntry();
            VerifyOperatingDayReportEntry();
            VerifyStockSupplyEntry();
            RegionalSupplyContractTransferOutboxDebugScenarios
                .VerifyGameplayOutcomeParent();
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static void VerifyAutoProcurementFocused()
    {
        VerifyAutoProcurementCompletedFactSnapshot();
        VerifyAutoProcurementEntry();
        VerifyAutoProcurementBudgetPendingRecovery();
        VerifyAutoProcurementStockPendingRecovery();
    }

    private static void VerifyAutoProcurementCompletedFactSnapshot()
    {
        var sourceStack = new AutoProcurementPhysicalStackRef
        {
            stackId = "stack:auto-procurement-fact-snapshot",
            itemInstanceId = "item-instance:auto-procurement-fact-snapshot",
            quantity = 3
        };
        var source = new AutoProcurementResult
        {
            day = 4,
            ruleId = "rule:snapshot",
            itemLabel = "item:snapshot",
            quantity = 3,
            cost = 21,
            purchased = true,
            reason = "captured",
            attemptedItemId = "item:snapshot",
            attemptedQuantity = 3,
            attemptedCost = 21,
            committedItemId = "item:snapshot",
            committedQuantity = 3,
            committedCost = 21,
            physicalStacks = new List<AutoProcurementPhysicalStackRef>
            {
                sourceStack
            }
        };
        var fact = new AutoProcurementCompletedFact(
            "stock:4:Food",
            source);

        source.day = 9;
        source.ruleId = "mutated";
        source.committedQuantity = 99;
        sourceStack.stackId = "stack:mutated";
        sourceStack.quantity = 99;
        source.physicalStacks.Clear();

        AutoProcurementResult first = fact.CaptureResult();
        Require(
            fact.OfferKey == "stock:4:Food"
            && fact.Day == 4
            && fact.RuleId == "rule:snapshot"
            && fact.CommittedQuantity == 3
            && fact.PhysicalStacks.Count == 1
            && fact.PhysicalStacks[0].StackId
                == "stack:auto-procurement-fact-snapshot"
            && fact.PhysicalStacks[0].Quantity == 3
            && first.physicalStacks.Count == 1,
            "Auto-procurement fact retained a mutable source result graph.");

        first.ruleId = "mutated-capture";
        first.physicalStacks[0].quantity = 77;
        first.physicalStacks.Clear();
        AutoProcurementResult second = fact.CaptureResult();
        Require(
            second.ruleId == "rule:snapshot"
            && second.physicalStacks.Count == 1
            && second.physicalStacks[0].quantity == 3,
            "Auto-procurement fact returned its immutable snapshot by reference.");
    }

    private static void VerifyAutoProcurementBudgetPendingRecovery()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.AutoProcurementResult;
        using var scenario = new AutoProcurementPendingScenario(
            stockPurchase: false);
        scenario.ProcessExpectingHiddenAcknowledgement();
        Require(
            scenario.LastProcessedDay == 1
            && scenario.ResultCount == 1
            && !scenario.Result.purchased
            && scenario.Result.ruleId == "budget"
            && scenario.PendingCount == 1,
            "A hidden budget-skip acknowledgement rolled back its durable owner result.");

        string savedKey = scenario.PendingPublicationKey;
        string checkpoint = scenario.CaptureCheckpoint();
        Require(
            !string.IsNullOrWhiteSpace(savedKey)
            && savedKey == scenario.CommittedResultKey,
            "Budget-skip pending DTO did not retain the canonical publication key.");

        scenario.RestoreCheckpoint(checkpoint);
        Require(
            scenario.PendingCount == 1
            && scenario.PendingPublicationKey == savedKey
            && scenario.LastProcessedDay == 1
            && scenario.ResultCount == 1,
            "Budget-skip pending DTO did not survive JSON and fresh runtime/store binding.");
        scenario.Process();

        RequireRecoveredAutoProcurement(
            scenario,
            savedKey,
            GameplayOutcomeStatus.Failed);
        Require(
            scenario.Money == scenario.InitialMoney
            && scenario.PhysicalQuantity == 0
            && scenario.EconomyRecordCount == 0,
            "Budget-skip recovery changed unrelated controlled owner state.");
    }

    private static void VerifyAutoProcurementStockPendingRecovery()
    {
        using var scenario = new AutoProcurementPendingScenario(
            stockPurchase: true);
        scenario.ProcessExpectingHiddenAcknowledgement();
        Require(
            scenario.ExpectedQuantity > 0
            && scenario.ExpectedCost > 0
            && scenario.LastProcessedDay == 1
            && scenario.ResultCount == 1
            && scenario.Result.purchased
            && scenario.Result.quantity == scenario.ExpectedQuantity
            && scenario.Result.cost == scenario.ExpectedCost
            && scenario.Money == scenario.InitialMoney - scenario.ExpectedCost
            && scenario.PhysicalQuantity == scenario.ExpectedQuantity
            && scenario.HasExactPurchaseLedgerRecord
            && scenario.PendingCount == 1,
            "A hidden stock-purchase acknowledgement rolled back money, physical output, treasury, or result state.");

        string savedKey = scenario.PendingPublicationKey;
        string checkpoint = scenario.CaptureCheckpoint();
        int money = scenario.Money;
        int quantity = scenario.PhysicalQuantity;
        int economyRecordCount = scenario.EconomyRecordCount;
        scenario.RestoreCheckpoint(checkpoint);
        Require(
            scenario.PendingPublicationKey == savedKey
            && scenario.Money == money
            && scenario.PhysicalQuantity == quantity
            && scenario.EconomyRecordCount == economyRecordCount
            && scenario.HasExactPurchaseLedgerRecord
            && scenario.ResultCount == 1,
            "Stock-purchase JSON restore changed committed money, physical output, treasury, or result state.");
        scenario.Process();

        RequireRecoveredAutoProcurement(
            scenario,
            savedKey,
            GameplayOutcomeStatus.Succeeded);
        Require(
            scenario.Money == money
            && scenario.PhysicalQuantity == quantity
            && scenario.EconomyRecordCount == economyRecordCount
            && scenario.HasExactPurchaseLedgerRecord
            && scenario.ResultCount == 1,
            "Stock-purchase recovery duplicated payment, physical output, treasury, or result state.");
    }

    private static void RequireRecoveredAutoProcurement(
        AutoProcurementPendingScenario scenario,
        string savedKey,
        GameplayOutcomeStatus expectedStatus)
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.AutoProcurementResult;
        GameplayOutcomeReplayIdentity identity =
            MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe);
        Require(
            scenario.PendingCount == 0
            && scenario.LastReplayPublicationKey == savedKey
            && scenario.ReplayBatchCount == 1
            && scenario.Probe.ReservationCount(Kind) == 1
            && scenario.Probe.CommitCount(Kind) == 1
            && identity.ResultKey.ToString() == savedKey,
            "Auto-procurement pending recovery did not reuse the exact saved result identity once.");
        Require(
            scenario.Outcomes.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact),
            "Auto-procurement pending recovery lost its exact outcome.");
        Require(
            exact.status == expectedStatus,
            "Auto-procurement pending recovery changed its exact status.");
        string[] expectedFactOrder =
        {
            MigratedProducerOutcomeIds.SourceTypeFact.Value,
            MigratedProducerOutcomeIds.SummaryFact.Value,
            MigratedProducerOutcomeIds.OperationFact.Value,
            "procurement-attempted-item-id",
            "procurement-committed-item-id",
            "procurement-child-result-key",
            "procurement-physical-stacks",
            MigratedProducerOutcomeIds.ReasonFact.Value,
            "procurement-purchased"
        };
        Require(
            exact.facts != null
            && exact.facts.Select(value => value.factId)
                .SequenceEqual(expectedFactOrder),
            "Auto-procurement replay changed canonical fact order; expected source, summary, operation, then typed details.");
    }

    private static void VerifyAutoProcurementEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.AutoProcurementResult;

        using (var scenario = new AutoProcurementScenario(Kind))
        {
            string before = "lastProcessedDay=" + scenario.LastProcessedDay
                + "|resultCount=" + scenario.ResultCount;
            scenario.Process();
            string after = "lastProcessedDay=" + scenario.LastProcessedDay
                + "|resultCount=" + scenario.ResultCount;
            Require(
                scenario.LastProcessedDay == 1
                && scenario.ResultCount == 1,
                "The auto-procurement production entry did not mutate its owner.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new AutoProcurementScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RequireThrows<InvalidOperationException>(scenario.Process);
            Require(
                scenario.LastProcessedDay == 0
                && scenario.ResultCount == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected auto-procurement result left a partial owner mutation.");
        }

        using (var scenario = new AutoProcurementScenario(
                   Kind,
                   deliveryFault: true))
        {
            scenario.Process();
            int day = scenario.LastProcessedDay;
            int count = scenario.ResultCount;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.LastProcessedDay == day
                && scenario.ResultCount == count,
                "Delivery retry duplicated the auto-procurement mutation.");
        }
    }

    private static void VerifyBuildingRetailPurchaseEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult;

        using (var scenario = new RetailScenario(Kind, crime: false))
        {
            string before = "stockCount=" + scenario.StockCount
                + "|holdingMoney=" + scenario.HoldingMoney
                + "|purchaseCount=" + scenario.CommittedPurchaseCount;
            scenario.Purchase();
            string after = "stockCount=" + scenario.StockCount
                + "|holdingMoney=" + scenario.HoldingMoney
                + "|purchaseCount=" + scenario.CommittedPurchaseCount;
            Require(
                scenario.PurchaseCommitted
                && scenario.StockCount == scenario.InitialStockCount - 1
                && scenario.HoldingMoney == scenario.InitialMoney - scenario.Cost,
                "The retail-purchase production entry did not mutate its owners.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: false,
                   faultMode: MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RequireThrows<InvalidOperationException>(scenario.Purchase);
            Require(
                scenario.StockCount == scenario.InitialStockCount - 1
                && scenario.HoldingMoney == scenario.InitialMoney - scenario.Cost
                && scenario.CommittedPurchaseCount == 0
                && scenario.PendingPurchaseCount == 1
                && scenario.PendingPurchasePhase
                    == RetailPurchaseRecoveryPhase.PhysicalCommitted
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A post-physical retail rejection did not retain its exact recovery state.");
            int stock = scenario.StockCount;
            int money = scenario.HoldingMoney;
            scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            scenario.RecoverPending();
            scenario.RecoverPending();
            Require(
                scenario.StockCount == stock
                && scenario.HoldingMoney == money
                && scenario.PendingPurchaseCount == 0
                && scenario.CommittedPurchaseCount == 1,
                "Retail recovery duplicated owners or failed to commit exactly once.");
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: false,
                   deliveryFault: true))
        {
            scenario.Purchase();
            int stock = scenario.StockCount;
            int money = scenario.HoldingMoney;
            int purchaseCount = scenario.CommittedPurchaseCount;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.StockCount == stock
                && scenario.HoldingMoney == money
                && scenario.CommittedPurchaseCount == purchaseCount,
                "Delivery retry duplicated the retail purchase.");
        }
    }

    public static bool VerifyRetailRevenueSkillProvenance()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult;

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: false,
                   revenueSkill: true))
        {
            scenario.Purchase();
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    scenario.Outcomes,
                    Kind,
                    scenario.Probe);
            Require(
                scenario.Outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact),
                "The revenue-skilled purchase lost its exact outcome.");
            RequireRevenueSkillFacts(scenario, exact);
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: false,
                   faultMode: MigratedProducerOutcomeFaultMode.RejectCommit,
                   revenueSkill: true))
        {
            RequireThrows<InvalidOperationException>(scenario.Purchase);
            RetailPurchaseRecoverySaveData captured =
                scenario.CaptureSinglePendingPurchase();
            RetailPurchaseRecoverySaveData roundTrip = JsonUtility.FromJson<
                RetailPurchaseRecoverySaveData>(JsonUtility.ToJson(captured));
            Require(
                roundTrip != null
                && string.Equals(
                    roundTrip.revenueWorkerId,
                    scenario.RevenueWorkerId,
                    StringComparison.Ordinal)
                && Mathf.Approximately(
                    roundTrip.revenuePriceMultiplier,
                    scenario.RevenuePriceMultiplier)
                && string.Equals(
                    roundTrip.revenueSkillModifierProvenance,
                    scenario.RevenueSkillModifierProvenance,
                    StringComparison.Ordinal),
                "Retail recovery serialization changed the frozen revenue skill context.");
            scenario.RestoreSinglePendingPurchase(roundTrip);
            scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            scenario.RecoverPending();
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    scenario.Outcomes,
                    Kind,
                    scenario.Probe);
            Require(
                scenario.Outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact),
                "The restored revenue-skilled purchase lost its exact outcome.");
            RequireRevenueSkillFacts(scenario, exact);
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: false,
                   faultMode: MigratedProducerOutcomeFaultMode.RejectCommit,
                   revenueSkill: true))
        {
            RequireThrows<InvalidOperationException>(scenario.Purchase);
            RetailPurchaseRecoverySaveData tampered =
                scenario.CaptureSinglePendingPurchase();
            tampered.revenueSkillModifierProvenance = string.Empty;
            RequireThrows<InvalidOperationException>(
                () => scenario.RestoreSinglePendingPurchase(tampered));
        }

        return true;
    }

    private static void RequireRevenueSkillFacts(
        RetailScenario scenario,
        GameplayOutcomeSnapshot exact)
    {
        Require(
            string.Equals(
                FactValue(exact, "retail-revenue-worker-id"),
                scenario.RevenueWorkerId,
                StringComparison.Ordinal)
            && string.Equals(
                FactValue(exact, "retail-revenue-price-multiplier"),
                scenario.RevenuePriceMultiplier.ToString(
                    "R",
                    System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            && string.Equals(
                FactValue(exact,
                    "retail-revenue-skill-modifier-provenance"),
                scenario.RevenueSkillModifierProvenance,
                StringComparison.Ordinal)
            && scenario.RevenuePriceMultiplier > 1f
            && scenario.RevenueSkillModifierProvenance.Contains(
                "passive|" + scenario.RevenueSkillId + "|revenue|",
                StringComparison.Ordinal)
            && scenario.RevenueSkillModifierProvenance.EndsWith(
                "|" + scenario.RevenueMechanicsSha256,
                StringComparison.Ordinal),
            "The retail outcome did not preserve the exact revenue skill provenance.");
    }

    private static void VerifyFacilityCrimeEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.FacilityCrimeEvent;

        using (var scenario = new RetailScenario(Kind, crime: true))
        {
            string before = "stockCount=" + scenario.StockCount;
            Require(
                scenario.CommitCrime()
                && scenario.StockCount == scenario.InitialStockCount - 1,
                "The facility-crime production entry did not remove the stolen lot.");
            string after = "stockCount=" + scenario.StockCount;
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: true,
                   faultMode: MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RequireThrows<InvalidOperationException>(() => scenario.CommitCrime());
            Require(
                scenario.StockCount == scenario.InitialStockCount
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected facility crime left the stolen lot removed.");
        }

        using (var scenario = new RetailScenario(
                   Kind,
                   crime: true,
                   deliveryFault: true))
        {
            Require(scenario.CommitCrime(), "The durably pending crime did not commit.");
            int stock = scenario.StockCount;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.StockCount == stock,
                "Delivery retry duplicated the facility crime mutation.");
        }
    }

    public static void RunCheckoutBoundaryRegressionBatch()
    {
        RunAll();
        Debug.Log("LIVE_CHECKOUT_ECONOMY_REGRESSION=PASS; existingKinds=6; liveCheckoutCases=5");
    }

    // Full checkout coroutine boundary: the older crime E2E calls the transaction
    // directly and cannot detect a leaked shop-use lease after it throws.
    public static void RunLiveCheckoutRejectionProbeBatch()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.FacilityCrimeEvent;
        foreach (MigratedProducerOutcomeFaultMode fault in new[]
                 {
                     MigratedProducerOutcomeFaultMode.None,
                     MigratedProducerOutcomeFaultMode.RejectReservation,
                     MigratedProducerOutcomeFaultMode.RejectCommit
                 })
        {
            using var scenario = new RetailScenario(Kind, crime: true, faultMode: fault);
            JObject before = scenario.CaptureOwnerCheckpoint();
            InvalidOperationException failure = null;
            try
            {
                scenario.RunLiveCheckout();
            }
            catch (InvalidOperationException exception)
            {
                failure = exception;
            }
            Debug.Log("LIVE_CHECKOUT_OBSERVATION: fault=" + fault
                + "; error=" + (failure?.Message ?? "none")
                + "; activeUsers=" + scenario.ActiveShopUsers
                + "; stock=" + scenario.StockCount
                + "; committed=" + scenario.Probe.CommitCount(Kind)
                + "; visit=" + scenario.LastVisitOutcome
                + "; activeExecutions=" + scenario.ActiveShopExecutions);
            if (fault == MigratedProducerOutcomeFaultMode.None)
            {
                Require(failure == null && scenario.Probe.CommitCount(Kind) == 1
                    && scenario.StockCount == scenario.InitialStockCount - 1
                    && scenario.LastVisitOutcome == ShoppingVisitOutcome.Completed,
                    "The full checkout did not reach its successful crime transaction: "
                    + failure?.Message);
            }
            else
            {
                string expected = fault == MigratedProducerOutcomeFaultMode.RejectReservation
                    ? "Shoplifting outcome reservation failed:"
                    : "Shoplifting outcome commit failed:";
                Require(failure != null && failure.Message.StartsWith(expected,
                        StringComparison.Ordinal)
                    && scenario.Probe.ReservationCount(Kind) == 1
                    && scenario.Probe.CommitCount(Kind) == 0
                    && scenario.Probe.CommitAttemptCount(Kind)
                        == (fault == MigratedProducerOutcomeFaultMode.RejectCommit ? 1 : 0),
                    "The requested checkout rejection was not reached.");
                Require(JToken.DeepEquals(before, scenario.CaptureOwnerCheckpoint()),
                    "A rejected live checkout changed exact stock or persistent shopping state.");
                Require(scenario.LastVisitOutcome == ShoppingVisitOutcome.Failed,
                    "A rejected live checkout did not expose a failed visit.");
            }
            Require(scenario.ActiveShopUsers == 0 && !scenario.IsActiveShopUser,
                "A terminal live checkout leaked its active shop-use lease.");
            scenario.RequireLiveCheckoutExecutionClosed();
        }
        using (var scenario = new RetailScenario(Kind, crime: true, deliveryFault: true))
        {
            scenario.RunLiveCheckout();
            Require(scenario.Probe.CommitCount(Kind) == 1
                    && scenario.StockCount == scenario.InitialStockCount - 1
                    && scenario.ActiveShopUsers == 0 && !scenario.IsActiveShopUser
                    && scenario.LastVisitOutcome == ShoppingVisitOutcome.Completed,
                "A durable pending-delivery crime did not finish the live checkout.");
            scenario.RequireLiveCheckoutExecutionClosed();
            JObject committed = scenario.CaptureOwnerCheckpoint();
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes, Kind, scenario.Probe, scenario.DeliveryFault);
            Require(JToken.DeepEquals(committed, scenario.CaptureOwnerCheckpoint())
                    && scenario.ActiveShopUsers == 0
                    && scenario.LastVisitOutcome == ShoppingVisitOutcome.Completed,
                "Delivery retry repeated the live checkout or changed its committed state.");
            scenario.RequireLiveCheckoutExecutionClosed();
        }
        using (var scenario = new RetailScenario(
                   Kind, crime: true,
                   faultMode: MigratedProducerOutcomeFaultMode.RejectReservation))
        {
            scenario.VerifyLiveCheckoutEndUseFailure();
        }
        Debug.Log("LIVE_CHECKOUT_REJECTION_PROBE=PASS; cases=5");
    }

    internal static void RunFacilityShopT01OrThrow(JArray rawSnapshots = null)
    {
        t01RawSnapshots = rawSnapshots;
        try
        {
            VerifyFacilityShopPurchaseEntry();
        }
        finally
        {
            t01RawSnapshots = null;
        }
    }

    private static void VerifyFacilityShopPurchaseEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.FacilityShopPurchaseResult;

        using (var scenario = new FacilityShopScenario(Kind))
        {
            string before = "money=" + scenario.Money;
            Require(
                scenario.Purchase(out FacilityShopPurchaseResult purchase)
                && scenario.Money < scenario.InitialMoney,
                "The facility-shop production entry did not spend money.");
            Require(
                FacilityShopFactEditorFixture.ReadKey(purchase).IsValid
                && purchase.paymentTransactionId.Length > 0
                && purchase.failureStage == FacilityShopPurchaseFailureStage.None
                && purchase.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .DomainApplied
                && purchase.balanceBefore == scenario.InitialMoney
                && purchase.balanceAfter == scenario.Money
                && scenario.PurchaseSequence == 1
                && scenario.PaymentRecords.Count == 1
                && scenario.PaymentRecords[0].transactionId
                    == purchase.paymentTransactionId,
                "The facility-shop purchase lost its payment or result identity.");
            string after = "money=" + scenario.Money;
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
            Require(
                scenario.Outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact.status == GameplayOutcomeStatus.Succeeded
                && exact.participants.Count == 2
                && exact.metrics.Count == 6
                && exact.metrics.Any(row => row.metricId == "quantity"
                    && row.value == 1d && row.unitId == "count")
                && exact.metrics.Any(row => row.metricId == "amount"
                    && row.value == purchase.offer.cost
                    && row.unitId == "currency")
                && exact.metrics.Any(row => row.metricId == "value"
                    && row.value == purchase.offer.cost
                    && row.unitId == "currency")
                && exact.metrics.Any(row => row.metricId == "previous"
                    && row.value == scenario.InitialMoney
                    && row.unitId == "currency")
                && exact.metrics.Any(row => row.metricId == "current"
                    && row.value == scenario.Money
                    && row.unitId == "currency")
                && exact.facts.Any(row => row.factId
                    == "facility-shop-payment-transaction-id"
                    && row.value == purchase.paymentTransactionId)
                && exact.facts.Any(row => row.factId
                    == "facility-shop-item-definition-id"
                    && row.value.StartsWith(
                        FacilityInstallationKitItemIds.Prefix,
                        StringComparison.Ordinal)),
                "The production purchase did not publish typed payment/offer evidence.");
            CaptureFacilityShopT01Evidence("daily-published", scenario, purchase);
        }

        using (var scenario = new FacilityShopScenario(
                   Kind,
                   unlockBasic: true))
        {
            string before = "money=" + scenario.Money;
            Require(
                scenario.PurchaseBasic(out FacilityShopPurchaseResult purchase)
                && purchase.offer?.basicPurchase == true
                && purchase.failureStage == FacilityShopPurchaseFailureStage.None
                && purchase.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .DomainApplied
                && purchase.balanceBefore == scenario.InitialMoney
                && purchase.balanceAfter == scenario.Money
                && scenario.Money < scenario.InitialMoney
                && scenario.PurchaseSequence == 1
                && scenario.PaymentRecords.Count == 1,
                "The authored basic facility purchase lost its typed durable result.");
            MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                "money=" + scenario.Money);
            CaptureFacilityShopT01Evidence("basic-published", scenario, purchase);
        }

        using (var scenario = new FacilityShopScenario(Kind, initialMoney: 0))
        {
            Require(
                !scenario.Purchase(out FacilityShopPurchaseResult rejection)
                && rejection.failureStage
                    == FacilityShopPurchaseFailureStage.InsufficientFunds
                && rejection.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .RejectedWithoutPublication
                && rejection.balanceBefore == 0
                && rejection.balanceAfter == 0
                && !FacilityShopFactEditorFixture.ReadKey(rejection).IsValid
                && rejection.paymentTransactionId.Length == 0
                && scenario.Money == 0
                && scenario.PurchaseSequence == 0
                && scenario.PaymentRecords.Count == 0
                && scenario.Probe.CommitCount(Kind) == 0,
                "An insufficient-funds facility purchase reported success or mutated authority.");
            CaptureFacilityShopT01Evidence("insufficient-funds", scenario, rejection);
            Require(
                !scenario.PurchaseDailyAt(-1, out FacilityShopPurchaseResult selection)
                && selection.failureStage
                    == FacilityShopPurchaseFailureStage.OfferSelection
                && selection.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .RejectedWithoutPublication
                && selection.balanceBefore == 0
                && selection.balanceAfter == 0,
                "An invalid facility offer selection lost its typed rejection stage.");
            CaptureFacilityShopT01Evidence("offer-selection", scenario, selection);
        }

        using (var scenario = new FacilityShopScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectReservation))
        {
            Require(
                !scenario.Purchase(out FacilityShopPurchaseResult rejection)
                && rejection.failureStage
                    == FacilityShopPurchaseFailureStage.OutcomeReservation
                && rejection.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .RejectedWithoutPublication
                && rejection.balanceBefore == scenario.InitialMoney
                && rejection.balanceAfter == scenario.InitialMoney
                && scenario.Money == scenario.InitialMoney
                && scenario.PurchaseSequence == 0
                && scenario.PaymentRecords.Count == 0,
                "A rejected purchase reservation changed payment or shop state.");
            CaptureFacilityShopT01Evidence("outcome-reservation", scenario, rejection);
        }

        using (var scenario = new FacilityShopScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            Require(
                !scenario.Purchase(out FacilityShopPurchaseResult rejection)
                && rejection.failureStage
                    == FacilityShopPurchaseFailureStage.OutcomeCommit
                && rejection.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .RejectedWithoutPublication
                && rejection.balanceBefore == scenario.InitialMoney
                && rejection.balanceAfter == scenario.InitialMoney
                && scenario.Money == scenario.InitialMoney
                && scenario.PurchaseSequence == 0
                && scenario.PaymentRecords.Count == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected facility-shop purchase left money mutation.");
            CaptureFacilityShopT01Evidence("outcome-commit", scenario, rejection);
        }

        using (var scenario = new FacilityShopScenario(
                   Kind,
                   deliveryFault: true))
        {
            Require(
                scenario.Purchase(out FacilityShopPurchaseResult pendingPurchase)
                && FacilityShopFactEditorFixture.ReadKey(pendingPurchase).IsValid
                && pendingPurchase.failureStage == FacilityShopPurchaseFailureStage.None
                && pendingPurchase.outcomePolicy == FacilityShopPurchaseOutcomePolicy
                    .DomainApplied
                && pendingPurchase.balanceBefore == scenario.InitialMoney
                && pendingPurchase.balanceAfter == scenario.Money,
                "The durably pending facility-shop purchase failed.");
            int money = scenario.Money;
            GameplayOutcomeLedgerSaveData pendingSave =
                scenario.Outcomes.Ledger.CaptureGameplayOutcomes();
            Require(
                pendingSave.outbox.Count == 1
                && pendingSave.outbox[0].operationId
                    == FacilityShopFactEditorFixture.ReadKey(pendingPurchase)
                        .OperationId.Value
                && scenario.PurchaseSequence == 1,
                "The failed outcome delivery was not saved as an exact pending result.");
            CaptureFacilityShopT01Evidence(
                "delivery-pending-before-restore", scenario, pendingPurchase);

            // Restore only the canonical ledger owner. Existing money/shop/treasury
            // owners stay live; this does not claim a whole-world or cold restore.
            GameplayOutcomeSnapshot savedPending = pendingSave.outbox.Single();
            // End the injected transient descriptor fault before restore preflight.
            // The saved delivery-fault state itself must survive the real restore.
            scenario.DeliveryFault.Enabled = false;
            RestoreFacilityShopT01Ledger(scenario.Outcomes.Ledger, pendingSave);
            GameplayOutcomeLedgerSaveData restoredPending =
                scenario.Outcomes.Ledger.CaptureGameplayOutcomes();
            Require(
                restoredPending.runId == pendingSave.runId
                && restoredPending.worldEpoch == pendingSave.worldEpoch + 1
                && restoredPending.nextSequence == pendingSave.nextSequence
                && restoredPending.outbox.Count == 1
                && restoredPending.exactOutcomes.Count == 0
                && restoredPending.outbox[0].sequence == savedPending.sequence
                && restoredPending.outbox[0].operationId == savedPending.operationId
                && restoredPending.outbox[0].immutablePayloadHash
                    == savedPending.immutablePayloadHash
                && scenario.Outcomes.Ledger.TryGetResultIdentity(
                    FacilityShopFactEditorFixture.ReadKey(pendingPurchase),
                    out GameplayOutcomeReplayIdentity restoredIdentity)
                && restoredIdentity.State
                    == GameplayOutcomeReplayState.DeliveryFaultPending
                && scenario.Money == money
                && scenario.PurchaseSequence == 1
                && scenario.PaymentRecords.Count == 1
                && scenario.PaymentRecords[0].transactionId
                    == pendingPurchase.paymentTransactionId,
                "Pending facility-shop restore changed result identity, hash or payment.");
            CaptureFacilityShopT01Evidence(
                "delivery-pending-after-restore", scenario, pendingPurchase);
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.Money == money
                && scenario.PaymentRecords.Count == 1
                && scenario.Outcomes.Ledger.CaptureGameplayOutcomes().outbox.Count == 0,
                "Delivery retry charged the facility-shop purchase twice.");
            CaptureFacilityShopT01Evidence(
                "published-after-restored-retry", scenario, pendingPurchase);

            GameplayOutcomeLedgerSaveData completedSave =
                scenario.Outcomes.Ledger.CaptureGameplayOutcomes();
            Require(
                completedSave.exactOutcomes.Count == 1
                && completedSave.exactOutcomes[0].sequence == savedPending.sequence
                && completedSave.exactOutcomes[0].immutablePayloadHash
                    == savedPending.immutablePayloadHash,
                "Restored delivery changed the committed purchase payload.");
            RestoreFacilityShopT01Ledger(scenario.Outcomes.Ledger, completedSave);
            GameplayOutcomeLedgerSaveData restoredCompleted =
                scenario.Outcomes.Ledger.CaptureGameplayOutcomes();
            Require(
                scenario.Outcomes.Recorder.RetryPendingDeliveries(16) == 0
                && scenario.Outcomes.Ledger.TryGetResultIdentity(
                    FacilityShopFactEditorFixture.ReadKey(pendingPurchase),
                    out GameplayOutcomeReplayIdentity completedIdentity)
                && completedIdentity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && restoredCompleted.runId == completedSave.runId
                && restoredCompleted.worldEpoch == completedSave.worldEpoch + 1
                && restoredCompleted.nextSequence == completedSave.nextSequence
                && restoredCompleted.outbox.Count == 0
                && restoredCompleted.exactOutcomes.Count == 1
                && restoredCompleted.exactOutcomes[0].sequence == savedPending.sequence
                && restoredCompleted.exactOutcomes[0].immutablePayloadHash
                    == savedPending.immutablePayloadHash
                && scenario.Money == money
                && scenario.PurchaseSequence == 1
                && scenario.PaymentRecords.Count == 1
                && scenario.PaymentRecords[0].transactionId
                    == pendingPurchase.paymentTransactionId,
                "Completed facility-shop restore redelivered or charged the purchase twice.");
            CaptureFacilityShopT01Evidence(
                "published-after-completed-restore-noop", scenario, pendingPurchase);
        }
    }

    private static void RestoreFacilityShopT01Ledger(
        GameplayOutcomeLedger ledger,
        GameplayOutcomeLedgerSaveData saved)
    {
        GameplayOutcomeLedgerSaveData parsed =
            DungeonSaveSectionPayload.Deserialize<GameplayOutcomeLedgerSaveData>(
                JsonUtility.ToJson(saved));
        GameplayOutcomeLedgerRestoreCandidate candidate =
            ledger.PrepareGameplayOutcomeRestore(parsed);
        ledger.BeginRestoreCandidate();
        try
        {
            ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate();
            ledger.CompleteRestoreCandidate();
        }
        catch
        {
            ledger.RollbackPublishedRestoreCandidate();
            throw;
        }
    }

    private static void CaptureFacilityShopT01Evidence(
        string stage,
        FacilityShopScenario scenario,
        FacilityShopPurchaseResult purchase)
    {
        if (t01RawSnapshots == null)
            return;
        t01RawSnapshots.Add(new JObject
        {
            ["stage"] = stage,
            ["money"] = scenario.Money,
            ["purchaseSequence"] = scenario.PurchaseSequence,
            ["deliveryFaultEnabled"] = scenario.DeliveryFault.Enabled,
            ["purchase"] = new JObject
            {
                ["cost"] = purchase.offer == null
                    ? JValue.CreateNull() : new JValue(purchase.offer.cost),
                ["basicPurchase"] = purchase.offer == null
                    ? JValue.CreateNull() : new JValue(purchase.offer.basicPurchase),
                ["balanceBefore"] = purchase.balanceBefore,
                ["balanceAfter"] = purchase.balanceAfter,
                ["paymentTransactionId"] = purchase.paymentTransactionId,
                ["resultOperationId"] = FacilityShopFactEditorFixture
                    .ReadKey(purchase).IsValid
                    ? FacilityShopFactEditorFixture.ReadKey(purchase)
                        .OperationId.Value
                    : string.Empty,
                ["failureStage"] = purchase.failureStage.ToString(),
                ["outcomePolicy"] = purchase.outcomePolicy.ToString()
            },
            ["paymentRecords"] = JArray.FromObject(scenario.PaymentRecords),
            ["ledger"] = JObject.Parse(JsonUtility.ToJson(
                scenario.Outcomes.Ledger.CaptureGameplayOutcomes()))
        });
    }

    private static void VerifyOperatingDayReportEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.OperatingDayReportEvent;

        using (var scenario = new OperatingDayScenario(Kind))
        {
            scenario.SeedDetailedLedger();
            RecordE06Evidence(scenario, "normal", "detailed-input");
            string before = "reportCount=" + scenario.ReportCount
                + "|latestReportDay=" + scenario.LatestReportDay
                + "|money=" + scenario.Money;
            scenario.Settle();
            RecordE06Evidence(scenario, "normal", "settled");
            string after = "reportCount=" + scenario.ReportCount
                + "|latestReportDay=" + scenario.LatestReportDay
                + "|money=" + scenario.Money;
            OperatingDayReport report = scenario.LatestReport;
            Require(
                scenario.ReportCount == 1
                && scenario.LatestReportDay == 1
                && report.reportId == OperatingDayReport.CreatePersistentId(1)
                && report.totalRevenue == 120
                && report.totalVisits == 2
                && Mathf.Approximately(report.averageSatisfaction, 70f)
                && report.facilityRevenues.Count == 1
                && report.facilityRevenues[0].facilityName == "test-hub"
                && report.facilityRevenues[0].revenue == 120
                && report.facilityDetails.Count == 1
                && report.facilityDetails[0].FacilityId == "test-hub"
                && report.facilityDetails[0].FacilityName == "test-hub"
                && report.facilityDetails[0].Revenue == 120
                && report.facilityDetails[0].VisitCount == 2
                && report.facilityDetails[0].OperatingCost == 7
                && Mathf.Approximately(
                    report.facilityDetails[0].AverageSatisfaction,
                    70f)
                && report.facilityDetails[0].ConsumedStock.Count == 1
                && report.facilityDetails[0].ConsumedStock[
                    (int)StockCategory.Food] == 3
                && report.stockConsumed.Count == 1
                && report.stockConsumed[0].category == StockCategory.Food
                && report.stockConsumed[0].amount == 3
                && report.maintenanceCost == 7
                && report.paidOperatingCost == 7
                && report.closingBalance == scenario.InitialMoney - 7
                && scenario.PaymentRecords.Count == 1
                && scenario.PaymentRecords[0].kind
                    == EconomyTransactionKind.PaidFacilityContract
                && scenario.PaymentRecords[0].sourceId
                    == "contract:test-hub"
                && scenario.PaymentRecords[0].targetId == "test-hub"
                && scenario.PaymentRecords[0].amount == -7
                && scenario.PaymentRecords[0].balanceBefore
                    == scenario.InitialMoney
                && scenario.PaymentRecords[0].balanceAfter
                    == report.closingBalance,
                "The operating-day production entry did not store its report.");
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
            bool exactFound = scenario.Outcomes.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact);
            Require(
                exactFound,
                "The operating-day outcome did not retain a durable join to its detailed report.");
            string outcomeSummary = exact.facts
                .Single(value => string.Equals(
                    value.factId,
                    "summary",
                    StringComparison.Ordinal))
                .value;
            Require(
                outcomeSummary.Contains("report-id=" + report.reportId)
                && outcomeSummary.Contains("revenue=120")
                && outcomeSummary.Contains("visits=2")
                && outcomeSummary.Contains("average-satisfaction=70"),
                "The operating-day outcome summary lost its detailed-report join or totals.");

            string reportBeforeReplay = scenario.CaptureSavePayload();
            string financeBeforeDirectReplay = scenario.CaptureFinancePayload();
            scenario.Settle();
            Require(scenario.ReportCount == 1
                && scenario.CaptureSavePayload() == reportBeforeReplay
                && scenario.CaptureFinancePayload() == financeBeforeDirectReplay
                && scenario.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
                "Repeating the completed operating day duplicated report, payment, or gameplay outcome.");
            string saved = scenario.CaptureSavePayload();
            string savedCheckpoint = scenario.CaptureCheckpoint();
            RecordE06Evidence(scenario, "normal", "saved", savedCheckpoint);
            using (var restored = new OperatingDayScenario(Kind))
            {
                restored.RestoreCheckpoint(savedCheckpoint);
                RecordE06Evidence(restored, "normal", "restored", savedCheckpoint);
                OperatingDayReport restoredReport = restored.LatestReport;
                Require(
                    restored.ReportCount == 1
                    && restoredReport.reportId == report.reportId
                    && restoredReport.totalRevenue == report.totalRevenue
                    && restoredReport.totalVisits == report.totalVisits
                    && Mathf.Approximately(
                        restoredReport.averageSatisfaction,
                        report.averageSatisfaction)
                    && restoredReport.facilityRevenues.Count == 1
                    && restoredReport.facilityRevenues[0].facilityName == "test-hub"
                    && restoredReport.facilityRevenues[0].revenue == 120
                    && restoredReport.facilityDetails.Count == 1
                    && restoredReport.facilityDetails[0].FacilityId == "test-hub"
                    && restoredReport.facilityDetails[0].Revenue == 120
                    && restoredReport.facilityDetails[0].VisitCount == 2
                    && restoredReport.facilityDetails[0].OperatingCost == 7
                    && Mathf.Approximately(
                        restoredReport.facilityDetails[0]
                            .AverageSatisfaction,
                        70f)
                    && restoredReport.facilityDetails[0].ConsumedStock[
                        (int)StockCategory.Food] == 3
                    && restoredReport.stockConsumed.Count == 1
                    && restoredReport.stockConsumed[0].category == StockCategory.Food
                    && restoredReport.stockConsumed[0].amount == 3
                    && restoredReport.paidOperatingCost == report.paidOperatingCost
                    && restoredReport.closingBalance == report.closingBalance,
                    "The detailed operating-day report did not survive persistence restore.");
                Require(restored.Money == report.closingBalance
                    && restored.PaymentRecords.Count == 1
                    && restored.PaymentRecords[0].transactionId == scenario.PaymentRecords[0].transactionId
                    && restored.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Single().immutablePayloadHash
                        == exact.immutablePayloadHash
                    && restored.CaptureSavePayload() == saved,
                    "Canonical settlement/finance/session/ledger restore lost the exact payment or report join.");
                string financeBeforeReplay = restored.CaptureFinancePayload();
                restored.Settle();
                RecordE06Evidence(restored, "normal", "restored-replay");
                Require(restored.ReportCount == 1
                    && restored.CaptureSavePayload() == saved
                    && restored.CaptureFinancePayload() == financeBeforeReplay
                    && restored.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
                    "Repeating the restored operating day duplicated report, payment, or gameplay outcome.");
            }
        }

        using (var scenario = new OperatingDayScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectReservation))
        {
            RecordE06Evidence(scenario, "reservation-rejected", "before");
            RequireThrows<InvalidOperationException>(scenario.Settle);
            RecordE06Evidence(scenario, "reservation-rejected", "rejected");
            Require(
                scenario.ReportCount == 0
                && scenario.Money == scenario.InitialMoney
                && scenario.PaymentRecords.Count == 0
                && scenario.Facts.Journal.Capture().pending.Count == 0
                && scenario.Probe.ReservationCount(Kind) == 1
                && scenario.Probe.CommitAttemptCount(Kind) == 0,
                "A rejected operating-day reservation changed report, payment, or fact state.");
        }

        using (var scenario = new OperatingDayScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RecordE06Evidence(scenario, "commit-rejected", "before");
            InvalidOperationException rejected = null;
            try
            {
                scenario.Settle();
            }
            catch (InvalidOperationException exception)
            {
                rejected = exception;
            }
            RecordE06Evidence(scenario, "commit-rejected", "rejected");
            Require(
                rejected?.Message.Contains(
                    "Operating-day report observation pending:",
                    StringComparison.Ordinal) == true
                && scenario.ReportCount == 1
                && scenario.LatestReport != null
                && scenario.LatestReport.paidOperatingCost == 7
                && scenario.Money == scenario.InitialMoney - 7
                && scenario.PaymentRecords.Count == 1
                && scenario.Facts.Journal.Capture().pending.Count == 1
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected operating-day observation did not retain the completed settlement and frozen fact.");

            int reportCount = scenario.ReportCount;
            int money = scenario.Money;
            int payments = scenario.PaymentRecords.Count;
            string pendingKey = scenario.Facts.Journal.Capture()
                .pending.Single().publicationKey;
            scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            scenario.Settle();
            RecordE06Evidence(scenario, "commit-rejected", "recovered");
            bool recoveredKey = GameplayDomainFactDeliveryQuery.TryReadKey(
                pendingKey,
                out GameplayResultKey resultKey);
            Require(
                scenario.ReportCount == reportCount
                && scenario.Money == money
                && scenario.PaymentRecords.Count == payments
                && scenario.Facts.Journal.Capture().pending.Count == 0
                && recoveredKey
                && scenario.Outcomes.Ledger.TryGetResultIdentity(
                    resultKey,
                    out GameplayOutcomeReplayIdentity recovered)
                && recovered.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && scenario.Outcomes.Ledger.CaptureGameplayOutcomes()
                    .exactOutcomes.Count == 1,
                "Operating-day observation recovery repeated report or payment effects.");
        }

        using (var scenario = new OperatingDayScenario(
                   Kind,
                   deliveryFault: true))
        {
            RecordE06Evidence(scenario, "delivery-pending", "before");
            scenario.Settle();
            RecordE06Evidence(scenario, "delivery-pending", "pending");
            int count = scenario.ReportCount;
            int money = scenario.Money;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            RecordE06Evidence(scenario, "delivery-pending", "delivered");
            Require(
                scenario.ReportCount == count
                && scenario.Money == money
                && scenario.PaymentRecords.Count == 1,
                "Delivery retry duplicated the operating-day settlement.");
        }
    }

    private static void VerifyStockSupplyEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.StockSupplyResult;

        using (var scenario = new StockSupplyScenario(Kind))
        {
            string before = "quantity=" + scenario.Quantity
                + "|money=" + scenario.Money;
            Require(
                scenario.Purchase().Succeeded
                && scenario.Quantity == scenario.Offer.amount
                && scenario.Money == scenario.InitialMoney - scenario.Offer.cost,
                "The stock-supply production entry did not mutate physical stock and money.");
            string after = "quantity=" + scenario.Quantity
                + "|money=" + scenario.Money;
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new StockSupplyScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            Require(
                !scenario.Purchase().Succeeded
                && scenario.Quantity == 0
                && scenario.Money == scenario.InitialMoney
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected stock supply left physical stock or money mutation.");
        }

        using (var scenario = new StockSupplyScenario(
                   Kind,
                   deliveryFault: true))
        {
            Require(scenario.Purchase().Succeeded, "The durably pending stock supply failed.");
            int quantity = scenario.Quantity;
            int money = scenario.Money;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.Quantity == quantity && scenario.Money == money,
                "Delivery retry duplicated the stock supply.");
        }
    }

    public static void RunStockSupplyObserverFocused()
    {
        VerifyStockSupplyEntry();
        Debug.Log("B116_STOCK_SUPPLY_OBSERVER=PASS");
    }

    private abstract class ScenarioBase : IDisposable
    {
        protected ScenarioBase(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode,
            bool deliveryFault,
            string label,
            string stableRunId = null)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                string.IsNullOrWhiteSpace(stableRunId)
                    ? "run:migrated-entry-" + label + "-"
                        + Guid.NewGuid().ToString("N")
                    : CausalGameplaySyntax.RequireId(
                        stableRunId,
                        nameof(stableRunId)),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        public virtual void Dispose()
        {
        }
    }

    private sealed class AutoProcurementScenario : ScenarioBase
    {
        private readonly AutoProcurementRuntime runtime;
        private readonly DomainFactDispatcher domainFacts;

        internal AutoProcurementScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
            : base(target, faultMode, deliveryFault, "auto-procurement")
        {
            var state = new GameSessionState(100, 1);
            var session = new TestGameSessionStateStore(state);
            var physical = StatefulWorldItemStackProxy.Create(out _);
            var treasury = new TreasuryEconomyAggregateStateStore(
                Outcomes.AggregateRootStore);
            domainFacts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                Outcomes.Recorder,
                Outcomes.Ledger);
            domainFacts.Subscribe(
                "fixture:auto-procurement-completed",
                new MigratedProducerDomainFactProbeObserver<
                    AutoProcurementCompletedFact>(
                    AutoProcurementFactMapping.Capture,
                    Outcomes.Transaction,
                    commits,
                    Probe));
            runtime = new AutoProcurementRuntime(
                session,
                new AutoProcurementFinancialDependencies(
                    new EditorGameMoneyAccount(state),
                    new ZeroEmploymentRuntime(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IPaidFacilityContractRuntime>()),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IRunVariableRuntimeReader>(),
                new AutoProcurementStockDependencies(
                    physical,
                    CharacterAiEditorTestDependencies.AuthoredGameplay),
                treasury,
                domainFacts,
                session);
            runtime.ConfigureBudget(0, 0);
        }

        internal int LastProcessedDay => runtime.Capture().lastProcessedDay;
        internal int ResultCount => runtime.LastResults.Count;
        internal void Process() => runtime.ProcessShopRefresh(
            1,
            Array.Empty<FacilityShopOffer>(),
            shopRuntime: null);

        public override void Dispose() => domainFacts.Dispose();
    }

    // Focused persistence evidence with controlled session/stock ports. The
    // authored stock catalog and real outcome ledger/observer remain in use;
    // this is not a whole-gameplay-world fixture.
    private sealed class AutoProcurementPendingScenario : ScenarioBase
    {
        private const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.AutoProcurementResult;

        private readonly bool stockPurchase;
        private AutoProcurementRuntime runtime;
        private HideOneDomainFactAcknowledgementPublisher publisher;
        private GameSessionState state;
        private TestGameSessionStateStore session;
        private TreasuryEconomyAggregateStateStore treasury;
        private EconomyTransactionLedgerRuntime economyLedger;
        private IWorldItemStackRuntime physicalRuntime;
        private StatefulWorldItemStackProxy physical;

        internal AutoProcurementPendingScenario(bool stockPurchase)
            : base(
                Kind,
                MigratedProducerOutcomeFaultMode.None,
                deliveryFault: false,
                label: stockPurchase
                    ? "auto-procurement-pending-stock"
                    : "auto-procurement-pending-budget")
        {
            this.stockPurchase = stockPurchase;
            InitialMoney = stockPurchase ? 10_000 : 100;
            BindFreshRuntime(
                new GameSessionState(InitialMoney, 1),
                hideFirstAcknowledgement: true);
            if (!stockPurchase)
            {
                runtime.ConfigureBudget(0, 0);
                return;
            }

            StockDeliveryOffer offer = StockSupplyService
                .CreateDailyDeliveryOffers(
                    1,
                    NeutralRunVariableReader.Instance,
                    CharacterAiEditorTestDependencies.AuthoredGameplay)
                .Single(value => value.category == StockCategory.Food);
            ExpectedQuantity = Math.Min(2, offer.amount);
            ExpectedCost = Mathf.CeilToInt(
                offer.cost
                / (float)Mathf.Max(1, offer.amount)
                * ExpectedQuantity);
            Require(
                ExpectedQuantity > 0
                && ExpectedCost > 0
                && !string.IsNullOrWhiteSpace(offer.itemId),
                "The authored food stock offer is not usable by the pending recovery fixture.");
            runtime.ConfigureBudget(ExpectedCost, 0);
            runtime.UpsertStockRule(new AutoProcurementRule
            {
                ruleId = "rule:pending-stock-food",
                category = StockCategory.Food,
                targetQuantity = ExpectedQuantity,
                maximumUnitPrice = int.MaxValue,
                dailyMaximumQuantity = ExpectedQuantity,
                priority = 1,
                enabled = true
            });
        }

        internal int InitialMoney { get; }
        internal int ExpectedQuantity { get; }
        internal int ExpectedCost { get; }
        internal int LastProcessedDay => runtime.Capture().lastProcessedDay;
        internal int ResultCount => runtime.LastResults.Count;
        internal AutoProcurementResult Result => runtime.LastResults.Single();
        internal int PendingCount => runtime.Capture().pendingFacts?.Count ?? 0;
        internal string PendingPublicationKey => PendingCount == 1
            ? runtime.Capture().pendingFacts[0].publicationKey
            : string.Empty;
        internal string CommittedResultKey =>
            Probe.LatestCommittedKey(Kind).ToString();
        internal int Money => state.holdingMoney.Value;
        internal int PhysicalQuantity => physical.Quantity;
        internal int EconomyRecordCount => economyLedger.Records.Count;
        internal int ReplayBatchCount => publisher.ReplayBatchCount;
        internal string LastReplayPublicationKey =>
            publisher.LastReplayPublicationKey;
        internal bool HasExactPurchaseLedgerRecord =>
            economyLedger.Records.Count == 1
            && economyLedger.Records[0].kind
                == EconomyTransactionKind.AutoProcurement
            && economyLedger.Records[0].sourceId
                == "rule:pending-stock-food"
            && economyLedger.Records[0].amount == -ExpectedCost
            && economyLedger.Records[0].balanceBefore == InitialMoney
            && economyLedger.Records[0].balanceAfter
                == InitialMoney - ExpectedCost
            && economyLedger.Records[0].succeeded;

        internal void Process() => runtime.ProcessShopRefresh(
            1,
            Array.Empty<FacilityShopOffer>(),
            shopRuntime: null);

        internal void ProcessExpectingHiddenAcknowledgement()
        {
            try
            {
                Process();
            }
            catch (InvalidOperationException exception)
            {
                Require(
                    exception.Message.Contains(
                        "Auto-procurement fact publication pending",
                        StringComparison.Ordinal),
                    "Hidden durable acknowledgement surfaced the wrong production failure: "
                        + exception.Message);
                return;
            }
            throw new InvalidOperationException(
                "Hidden durable acknowledgement did not surface an explicit pending result.");
        }

        internal string CaptureCheckpoint()
        {
            AutoProcurementSaveData procurement = runtime.Capture();
            Require(
                procurement.pendingFacts?.Count == 1
                && !string.IsNullOrWhiteSpace(
                    procurement.pendingFacts[0].publicationKey),
                "Pending auto-procurement capture omitted its exact publication identity.");
            return JsonUtility.ToJson(new AutoProcurementPendingCheckpoint
            {
                procurement = procurement,
                session = GameSessionSaveData.From(state.Capture()),
                economyLedger = economyLedger.Capture(),
                physical = physicalRuntime.Capture()
            });
        }

        internal void RestoreCheckpoint(string json)
        {
            AutoProcurementPendingCheckpoint checkpoint =
                JsonUtility.FromJson<AutoProcurementPendingCheckpoint>(json);
            if (checkpoint?.procurement == null
                || checkpoint.session == null
                || checkpoint.economyLedger == null
                || checkpoint.physical == null)
            {
                throw new InvalidOperationException(
                    "Auto-procurement pending checkpoint JSON is incomplete.");
            }

            publisher.Dispose();
            BindFreshRuntime(
                new GameSessionState(0, 1),
                hideFirstAcknowledgement: false);
            var restoredTreasury = new TreasuryEconomyAggregateState();
            runtime.PopulateRestoreState(
                restoredTreasury,
                checkpoint.procurement);
            economyLedger.PopulateRestoreState(
                restoredTreasury,
                checkpoint.economyLedger);
            treasury.Replace(restoredTreasury);
            session.Restore(checkpoint.session.ToSnapshot());
            physicalRuntime.Restore(checkpoint.physical);
        }

        private void BindFreshRuntime(
            GameSessionState freshState,
            bool hideFirstAcknowledgement)
        {
            state = freshState ?? throw new ArgumentNullException(nameof(freshState));
            session = new TestGameSessionStateStore(state);
            treasury = new TreasuryEconomyAggregateStateStore(
                Outcomes.AggregateRootStore);
            economyLedger = new EconomyTransactionLedgerRuntime(
                new UnityGameClock(),
                treasury);
            physicalRuntime = StatefulWorldItemStackProxy.Create(
                out physical,
                stockPurchase ? StockCategory.Food : StockCategory.General);

            var dispatcher = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                Outcomes.Recorder,
                Outcomes.Ledger);
            dispatcher.Subscribe(
                "fixture:auto-procurement-pending",
                new MigratedProducerDomainFactProbeObserver<
                    AutoProcurementCompletedFact>(
                    AutoProcurementFactMapping.Capture,
                    Outcomes.Transaction,
                    commits,
                    Probe));
            publisher = new HideOneDomainFactAcknowledgementPublisher(
                dispatcher,
                hideFirstAcknowledgement);
            runtime = new AutoProcurementRuntime(
                session,
                new AutoProcurementFinancialDependencies(
                    new EditorGameMoneyAccount(state, economyLedger),
                    new ZeroEmploymentRuntime(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios
                        .DefaultProxy.Create<IPaidFacilityContractRuntime>()),
                NeutralRunVariableReader.Instance,
                new AutoProcurementStockDependencies(
                    physicalRuntime,
                    CharacterAiEditorTestDependencies.AuthoredGameplay),
                treasury,
                publisher,
                session);
        }

        public override void Dispose() => publisher?.Dispose();
    }

    [Serializable]
    private sealed class AutoProcurementPendingCheckpoint
    {
        public AutoProcurementSaveData procurement;
        public GameSessionSaveData session;
        public EconomyTransactionLedgerSaveData economyLedger;
        public DungeonPhysicalItemSaveData physical;
    }

    private sealed class HideOneDomainFactAcknowledgementPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher,
        IDisposable
    {
        private readonly DomainFactDispatcher inner;
        private bool hideNextAcknowledgement;
        private bool disposed;

        internal HideOneDomainFactAcknowledgementPublisher(
            DomainFactDispatcher inner,
            bool hideNextAcknowledgement)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.hideNextAcknowledgement = hideNextAcknowledgement;
        }

        internal int ReplayBatchCount { get; private set; }
        internal string LastReplayPublicationKey { get; private set; } =
            string.Empty;

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual) => HideOnce(inner.Publish(lease, actual));

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            bool accepted = inner.PublishBatch(facts, results, out failure);
            for (int index = 0; index < results.Length; index++)
                results[index] = HideOnce(results[index]);
            return accepted;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayBatchCount++;
            LastReplayPublicationKey = facts?.Count == 1
                ? facts[0]?.PublicationKey ?? string.Empty
                : string.Empty;
            return inner.ReplayBatch(facts, results, out failure);
        }

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            inner.Dispose();
        }

        private DomainFactDelivery HideOnce(DomainFactDelivery delivery)
        {
            if (!hideNextAcknowledgement || !delivery.MayBeCommitted)
                return delivery;
            hideNextAcknowledgement = false;
            return new DomainFactDelivery(
                DomainFactDeliveryPhase.InDoubt,
                delivery.OperationKey,
                delivery.PublicationId,
                delivery.Fingerprint,
                "fixture-hidden-durable-acknowledgement");
        }
    }

    private sealed partial class RetailScenario : ScenarioBase
    {
        private readonly CustomerAiDebugScenarios.CustomerAiScenarioWorld world;
        private readonly Shop shop;
        private readonly CharacterActor customer;
        private readonly AbilityShopping shopping;
        private readonly RemainStock stock;
        private readonly AIAction action;
        private readonly BuildingRetailPurchaseCommitResult purchaseResult = new();
        private readonly RetailPurchaseFactEditorFixture purchaseFacts;
        private readonly ShopliftingFactEditorFixture shopliftingFacts;
        private readonly EditorFacilityUseFactFixture facilityUseFacts;
        private readonly ShopliftingActivityObserver shopliftingActivities;
        private readonly GameEventBus shopEvents;
        private readonly bool crime;
        private ShopUseExecutionState observedCheckoutExecution;
        private long checkoutSequenceBefore;
        private bool observedSelectedCart;

        internal RetailScenario(
            MigratedProducerOutcomeKind target,
            bool crime,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false,
            string stableRunId = null,
            string ownerId = null,
            bool revenueSkill = false)
            : base(target, faultMode, deliveryFault,
                crime ? "facility-crime" : "retail-purchase",
                stableRunId)
        {
            this.crime = crime;
            world = new CustomerAiDebugScenarios.CustomerAiScenarioWorld();
            shop = world.Place("S01_판매카운터", new Vector2Int(4, 0)) as Shop
                ?? throw new InvalidOperationException("The authored shop fixture is missing.");
            customer = world.CreateCustomer(
                "Slime",
                Vector2Int.zero,
                90f,
                90f,
                10f,
                20f);
            string identityScope = string.IsNullOrWhiteSpace(ownerId)
                ? stableRunId
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            if (!string.IsNullOrWhiteSpace(identityScope))
            {
                shop.RestorePersistentIdentity(new BuildingInstanceId(
                    identityScope.StartsWith("building:", StringComparison.Ordinal)
                        ? identityScope
                        : "building:causal-retail:" + identityScope));
                customer.Identity.SetPersistentId(
                    "character:causal-retail:" + identityScope);
            }
            shopping = customer.GetAbility<AbilityShopping>()
                ?? throw new InvalidOperationException("The customer shopping ability is missing.");
            var session = new FixedGameSessionStateProvider(7);
            purchaseFacts = new RetailPurchaseFactEditorFixture(Outcomes, Probe);
            shopping.ConstructRetailPurchaseFacts(
                purchaseFacts.Publisher,
                purchaseFacts.Recovery,
                session);
            shopliftingFacts = new ShopliftingFactEditorFixture(Outcomes, Probe);
            shop.ConstructShopCrimeFacts(
                shopliftingFacts.Journal,
                session);
            facilityUseFacts = new EditorFacilityUseFactFixture(
                Outcomes,
                Probe);
            shopEvents = new GameEventBus();
            shop.ConstructBuildableObjectEventBus(
                shopEvents,
                new BuildingVisitEventPublisher(
                    shopEvents,
                    session,
                    facilityUseFacts.Facts,
                    facilityUseFacts.Facts),
                new BuildingInfoPresentationAdapter(shopEvents));
            shopliftingActivities = new ShopliftingActivityObserver(shopEvents);
            shopliftingActivities.Initialize();
            // The production shop is composed with GameMoneyAccount, whose
            // source-key ledger makes revenue credit safe to resume.  The
            // older customer-AI fixture still supplies EditorGameMoneyAccount,
            // which cannot satisfy the persisted retail-recovery contract.
            // Replace only that fixture port with the production account
            // shape so this E2E exercises the real post-outcome recovery path.
            var revenueState = new GameSessionState(0, 7);
            var revenueSession = new TestGameSessionStateStore(revenueState);
            var revenueAggregate = new TreasuryEconomyAggregateStateStore(
                Outcomes.AggregateRootStore);
            var revenueAccount = new GameMoneyAccount(
                revenueSession,
                new EconomyTransactionLedgerRuntime(
                    new UnityGameClock(),
                    revenueAggregate));
            FieldInfo moneyAccountField = typeof(Shop).GetField(
                "moneyAccount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(moneyAccountField != null,
                "The production shop money-account port is unavailable.");
            moneyAccountField.SetValue(shop, revenueAccount);
            shop.ConfigureCrimeForDiagnostics(
                AlwaysCrimeRiskEvaluator.Instance,
                ZeroRandomStream.Instance);

            if (revenueSkill)
            {
                CharacterActor worker = world.StaffShop(
                    shop,
                    persistentId: "character:g13:retail-revenue-worker")
                    ?? throw new InvalidOperationException(
                        "The revenue provenance fixture could not staff the shop.");
                CharacterSkillInstance skill = new CharacterSkillInstance
                {
                    id = "skill:g13:retail-revenue",
                    combinationId = "combination:g13:retail-revenue",
                    displayName = "거래의 호흡",
                    description = "상점 판매 수익을 높인다.",
                    kind = CharacterSkillKind.Passive,
                    trigger = CharacterSkillTrigger.WorkCompleted,
                    target = CharacterSkillTarget.Facility,
                    modules = new List<CharacterSkillModuleSelection>
                    {
                        new CharacterSkillModuleSelection
                        {
                            moduleId = "revenue",
                            variantId = "small"
                        }
                    }
                };
                worker.Progression.GrowthState.passiveSkills.Add(skill);
                RetailRevenueModifierSnapshot pricing =
                    shop.CaptureRevenueModifierSnapshot();
                purchaseResult.BindPricingContext(pricing);
                RevenueWorkerId = pricing.WorkerId;
                RevenuePriceMultiplier = pricing.PriceMultiplier;
                RevenueSkillModifierProvenance =
                    pricing.SkillModifierProvenance;
                RevenueSkillId = skill.id;
                RevenueMechanicsSha256 = CharacterSkillActivationTransaction
                    .ComputeMechanicsSha256(skill);
            }

            RetailStockLotSnapshot source = shop.CreateStockSnapshot().lots
                .FirstOrDefault(lot => lot != null
                    && lot.quantity > 0
                    && string.IsNullOrEmpty(lot.itemInstanceId))
                ?.Clone()
                ?? throw new InvalidOperationException(
                    "The authored shop has no stackable exact retail lot.");
            source.quantity = 1;
            ShopStockStateSnapshot authored = shop.CreateStockSnapshot();
            shop.ApplyStockSnapshot(new ShopStockStateSnapshot
            {
                schemaVersion = ShopStockStateSnapshot.CurrentSchemaVersion,
                activatedAuthoredSaleItemIds = new List<int>(
                    authored.activatedAuthoredSaleItemIds),
                lots = new List<RetailStockLotSnapshot> { source }
            });
            RetailProductSnapshot product = shop.ProductSnapshots.Single(value =>
                value.Id == source.saleItemId);
            Cost = revenueSkill ? product.Price : 25;
            stock = new RemainStock(
                product.Id,
                product.Name,
                Cost,
                1,
                Array.Empty<OnBuyItemSO>());
            shopping.RestorePersistentState(2, 0, 500);
            AIActionSet actionSet = Resources.Load<AIActionSet>(
                "SO/AI/Action/Shopping")
                ?? throw new InvalidOperationException(
                    "The authored shopping action fixture is missing.");
            action = new AIAction(actionSet, AIActionPlan.AtDestination(shop));
            customer.ai.bestAction = action;
            customer.ai.isBestActionEnd = false;
            InitialStockCount = shop.GetStockCount();
            InitialMoney = shopping.HoldingMoney;
        }

        internal int Cost { get; }
        internal string RevenueWorkerId { get; } = string.Empty;
        internal float RevenuePriceMultiplier { get; } = 1f;
        internal string RevenueSkillModifierProvenance { get; } =
            WorkCompletionIdentityReferenceDefaults.None;
        internal string RevenueSkillId { get; } = string.Empty;
        internal string RevenueMechanicsSha256 { get; } = string.Empty;
        internal int InitialStockCount { get; }
        internal int InitialMoney { get; }
        internal int StockCount => shop.GetStockCount();
        internal int HoldingMoney => shopping.HoldingMoney;
        internal int PendingPurchaseCount =>
            shopping.CapturePendingRetailPurchases().Count;
        internal RetailPurchaseRecoveryPhase PendingPurchasePhase =>
            shopping.CapturePendingRetailPurchases().Single().phase;
        internal int CommittedPurchaseCount
        {
            get
            {
                GameplayOutcomeLedgerSaveData saved =
                    Outcomes.Ledger.CaptureGameplayOutcomes();
                return saved.outbox.Concat(saved.exactOutcomes)
                    .Count(value => string.Equals(
                    value.outcomeTypeId,
                    MigratedProducerOutcomeCatalog.Get(
                        MigratedProducerOutcomeKind
                            .BuildingRetailPurchaseCommitResult)
                        .OutcomeTypeId.Value,
                    StringComparison.Ordinal));
            }
        }
        internal bool PurchaseCommitted => purchaseResult.Committed;
        internal int ActiveShopUsers => shop.CurrentUserCount;
        internal bool IsActiveShopUser => shop.IsActiveUser(customer.BuildingVisitor.BuildingCharacterId);
        internal ShoppingVisitOutcome LastVisitOutcome => shopping.LastVisitOutcome;
        internal int ActiveShopExecutions => shop.InteractionState.activeUses.Count;

        internal void RequireLiveCheckoutExecutionClosed()
        {
            Require(observedCheckoutExecution != null && observedSelectedCart
                    && shop.InteractionState.nextSequence == checkoutSequenceBefore + 1
                    && ActiveShopExecutions == 0
                    && shop.FindActiveShopUse(customer.BuildingVisitor.BuildingCharacterId.Value) == null,
                "A terminal live checkout did not release its single observed execution/cart owner.");
        }

        private void ObserveLiveCheckoutExecution()
        {
            ShopUseExecutionState current = shop.FindActiveShopUse(
                customer.BuildingVisitor.BuildingCharacterId.Value);
            if (current == null) return;
            Require(ActiveShopExecutions == 1
                    && current.sequence == checkoutSequenceBefore
                    && current.operationId == ShopUseExecutionState.OperationFor(
                        shop.RequirePersistentInstanceId().Value, checkoutSequenceBefore)
                    && (observedCheckoutExecution == null
                        || ReferenceEquals(observedCheckoutExecution, current)),
                "Live checkout replaced or duplicated its per-visitor execution owner.");
            observedCheckoutExecution = current;
            if (current.cart.Count > 0)
            {
                Require(current.cart.Count == 1 && current.cart[0].saleItemId == stock.id,
                    "Live checkout selected a different or duplicated exact stock line.");
                observedSelectedCart = true;
            }
        }

        internal void RunLiveCheckout(bool waitForAttendant = false)
        {
            Require(ActiveShopExecutions == 0,
                "A new live checkout fixture already has an execution owner.");
            checkoutSequenceBefore = shop.InteractionState.nextSequence;
            observedCheckoutExecution = null;
            observedSelectedCart = false;
            var clock = new LiveCheckoutClock();
            FieldInfo shopClock = typeof(BuildableObject).GetField(
                "gameClock", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo moveClock = typeof(AbilityMove).GetField(
                "gameClock", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(shopClock != null && moveClock != null,
                "Live checkout controlled clock injection is unavailable.");
            shopClock.SetValue(shop, clock);
            moveClock.SetValue(customer.GetAbility<AbilityMove>(), clock);
            // Explicit fixture staffing, either initially or after the real
            // owner enters Waiting. Neither mode changes the shop's rules.
            using IDisposable attendantArrival = waitForAttendant
                ? shopEvents.Subscribe<ShopInteractionObservedEvent>(value =>
                {
                    if (value.Transition == ShopInteractionTransition.AttendantWaitingStarted)
                        world.StaffShop(shop);
                }) : null;
            if (!waitForAttendant) world.StaffShop(shop);
            Require(waitForAttendant ? !shop.HasServingWorker
                    : shop.HasServingWorker && shop.CanServeCustomer(customer.BuildingVisitor, out _),
                "The authored shop does not match the controlled staffing condition.");
            customer.transform.position = shop.GetFacilityAnchorWorldPosition(
                FacilityAnchorPurposeIds.Use, customer.transform.position);
            Require(shop.ContainsGridPosition(world.Grid.GetXY(customer.transform.position)),
                "The controlled visitor must already be inside the checkout destination.");
            shopping.BeginVisitInteraction(shop);
            Require(shopping.LastVisitOutcome == ShoppingVisitOutcome.InProgress,
                "The production shopping caller did not establish its visit target.");
            var routines = new Stack<IEnumerator>();
            routines.Push(shop.Interact(customer.BuildingVisitor));
            int steps = 0;
            try
            {
                while (routines.Count > 0)
                {
                    Require(++steps <= 10000, "The live checkout coroutine did not terminate.");
                    clock.Advance();
                    ObserveLiveCheckoutExecution();
                    IEnumerator current = routines.Peek();
                    if (!current.MoveNext())
                    {
                        routines.Pop();
                        (current as IDisposable)?.Dispose();
                    }
                    else if (current.Current is IEnumerator nested)
                    {
                        routines.Push(nested);
                    }
                }
            }
            finally
            {
                while (routines.Count > 0)
                    (routines.Pop() as IDisposable)?.Dispose();
            }
        }

        internal void VerifyLiveCheckoutEndUseFailure()
        {
            const MigratedProducerOutcomeKind Kind =
                MigratedProducerOutcomeKind.FacilityCrimeEvent;
            FieldInfo registryField = typeof(BuildableObject).GetField(
                "worldRegistry", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(registryField != null,
                "Live checkout world registry injection is unavailable.");
            var originalRegistry = registryField.GetValue(shop) as IBuildingWorldRegistryPort
                ?? throw new InvalidOperationException("The original shop world registry is missing.");
            var faultRegistry = new LiveCheckoutEndUseFailureRegistry(
                originalRegistry, shop, customer.BuildingVisitor.BuildingCharacterId);
            JObject before = CaptureOwnerCheckpoint();
            try
            {
                registryField.SetValue(shop, faultRegistry);
                Exception failure = null;
                try
                {
                    RunLiveCheckout();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                Debug.Log("LIVE_CHECKOUT_OBSERVATION: fault=RejectReservation+EndUse"
                    + "; errorType=" + failure?.GetType().Name
                    + "; error=" + failure?.Message
                    + "; endUseFailures=" + faultRegistry.FailureCount
                    + "; activeUsers=" + ActiveShopUsers
                    + "; activeExecutions=" + ActiveShopExecutions
                    + "; pendingReason=" + observedCheckoutExecution?.serviceFailureReason);
                Require(failure is AggregateException,
                    "The checkout lost its original failure when EndUse also failed.");
                var errors = ((AggregateException)failure).Flatten().InnerExceptions;
                Require(faultRegistry.FailureCount == 1
                    && errors.Count == 1 + faultRegistry.FailureCount
                    && errors.Count(error => error is InvalidOperationException
                        && error.Message.StartsWith("Shoplifting outcome reservation failed:",
                            StringComparison.Ordinal)) == 1
                    && errors.Count(error => error is InvalidOperationException
                        && error.Message == LiveCheckoutEndUseFailureRegistry.FailureMessage)
                        == faultRegistry.FailureCount,
                    "The aggregate did not preserve both the original reservation and EndUse failures.");
                Require(Probe.ReservationCount(Kind) == 1
                    && Probe.CommitAttemptCount(Kind) == 0 && Probe.CommitCount(Kind) == 0
                    && JToken.DeepEquals(before, CaptureOwnerCheckpoint())
                    && LastVisitOutcome == ShoppingVisitOutcome.Failed
                    && ActiveShopUsers == 0 && !IsActiveShopUser,
                    "The EndUse failure changed rejected checkout state or retained active occupancy.");
                Require(observedSelectedCart && ActiveShopExecutions == 1
                        && ReferenceEquals(observedCheckoutExecution, shop.FindActiveShopUse(
                            customer.BuildingVisitor.BuildingCharacterId.Value))
                        && observedCheckoutExecution.phase == ShopUseExecutionPhase.ServiceCompletion
                        && !observedCheckoutExecution.serviceSucceeded
                        && observedCheckoutExecution.serviceFailureReason.Contains(
                            "Shoplifting outcome reservation failed:"),
                    "Failed terminal cleanup lost its pending execution/cart or original failure.");
            }
            finally
            {
                registryField.SetValue(shop, originalRegistry);
                // The injected registry deliberately retains its ownership entry.
                // Recover it through the original port for teardown, not as a
                // claim that the failed runtime cleanup had already cleared it.
                shop.EndUse(customer.BuildingVisitor);
            }
            Require(ReferenceEquals(registryField.GetValue(shop), originalRegistry),
                "The original shop world registry was not restored before disposal.");
            Debug.Log("LIVE_CHECKOUT_OBSERVATION: fault=RejectReservation+EndUse"
                + "; originalFailurePreserved=true; endUseFailures=" + faultRegistry.FailureCount
                + "; registryRestoredForTeardown=true; ownershipRetriedForTeardown=true");
        }

        internal JObject CaptureOwnerCheckpoint() => new()
        {
            ["shopStock"] = JObject.Parse(
                JsonUtility.ToJson(shop.CreateStockSnapshot())),
            ["shopping"] = new JObject
            {
                ["schemaVersion"] = 1,
                ["visitCount"] = shopping.visitCount,
                ["lookAroundCount"] = shopping.lookAroundCount,
                ["holdingMoney"] = shopping.HoldingMoney
            }
        };

        internal void RestoreOwnerCheckpoint(JObject domainState)
        {
            string[] fields = domainState?.Properties()
                .Select(value => value.Name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray() ?? Array.Empty<string>();
            if (!fields.SequenceEqual(
                    new[] { "shopStock", "shopping" },
                    StringComparer.Ordinal)
                || domainState["shopStock"] is not JObject stockJson
                || domainState["shopping"] is not JObject shoppingJson
                || shoppingJson.Properties().Select(value => value.Name)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(
                        new[]
                        {
                            "holdingMoney",
                            "lookAroundCount",
                            "schemaVersion",
                            "visitCount"
                        },
                        StringComparer.Ordinal) == false
                || (int)shoppingJson["schemaVersion"] != 1)
            {
                throw new InvalidOperationException(
                    "Retail owner checkpoint shape is invalid.");
            }
            ShopStockStateSnapshot stock = JsonUtility.FromJson<
                ShopStockStateSnapshot>(stockJson.ToString(Formatting.None));
            shop.ApplyStockSnapshot(stock);
            shopping.RestorePersistentState(
                (int)shoppingJson["visitCount"],
                (int)shoppingJson["lookAroundCount"],
                (int)shoppingJson["holdingMoney"]);
        }

        internal void Purchase()
        {
            if (crime)
                throw new InvalidOperationException("This scenario is configured for crime.");
            IEnumerator routine = shopping.BuyItem(
                stock,
                Cost,
                action,
                shop,
                purchaseResult);
            Require(routine.MoveNext(), "The retail purchase did not reach its commit delay.");
            Require(!routine.MoveNext(), "The retail purchase did not reach a terminal state.");
        }

        internal void RecoverPending()
        {
            Require(
                shopping.TryRecoverPendingRetailPurchases(
                    shop,
                    out string failureReason),
                "Retail purchase recovery failed: " + failureReason);
        }

        internal RetailPurchaseRecoverySaveData CaptureSinglePendingPurchase()
        {
            return shopping.CapturePendingRetailPurchases().Single();
        }

        internal void RestoreSinglePendingPurchase(
            RetailPurchaseRecoverySaveData pending)
        {
            shopping.RestorePersistentState(
                2,
                0,
                shopping.HoldingMoney,
                new[] { pending });
        }

        internal bool CommitCrime()
        {
            if (!crime)
                throw new InvalidOperationException("This scenario is configured for purchase.");
            return shop.TryResolveCheckoutCrime(
                customer.BuildingVisitor,
                new[] { stock });
        }

        public override void Dispose()
        {
            shopliftingActivities?.Dispose();
            facilityUseFacts?.Dispose();
            shopliftingFacts?.Dispose();
            purchaseFacts?.Dispose();
            world.Dispose();
        }
    }

    private sealed class LiveCheckoutEndUseFailureRegistry : IBuildingWorldRegistryPort
    {
        internal const string FailureMessage = "injected-checkout-end-use-failure";
        private readonly IBuildingWorldRegistryPort original;
        private readonly IBuildingWorldEntryPort targetBuilding;
        private readonly CharacterId targetCharacter;

        internal LiveCheckoutEndUseFailureRegistry(
            IBuildingWorldRegistryPort original,
            IBuildingWorldEntryPort targetBuilding,
            CharacterId targetCharacter)
        {
            this.original = original ?? throw new ArgumentNullException(nameof(original));
            this.targetBuilding = targetBuilding;
            this.targetCharacter = targetCharacter;
        }

        internal int FailureCount { get; private set; }
        public int BuildingVersion => original.BuildingVersion;
        public IReadOnlyList<IBuildingWorldEntryPort> Buildings => original.Buildings;
        public void RegisterBuilding(IBuildingWorldEntryPort building) => original.RegisterBuilding(building);
        public void UnregisterBuilding(IBuildingWorldEntryPort building) => original.UnregisterBuilding(building);
        public bool TryReplaceBuilding(IBuildingWorldEntryPort expectedCurrent,
            IBuildingWorldEntryPort replacement, out string failureReason) =>
            original.TryReplaceBuilding(expectedCurrent, replacement, out failureReason);
        public bool TryRollbackBuildingReplacement(IBuildingWorldEntryPort expectedReplacement,
            IBuildingWorldEntryPort restored, out string failureReason) =>
            original.TryRollbackBuildingReplacement(expectedReplacement, restored, out failureReason);
        public void TrackTransientCharacterOwnership(IBuildingWorldEntryPort building,
            CharacterId characterId, BuildingTransientOwnershipKind kind) =>
            original.TrackTransientCharacterOwnership(building, characterId, kind);
        public void UntrackTransientCharacterOwnership(IBuildingWorldEntryPort building,
            CharacterId characterId, BuildingTransientOwnershipKind kind)
        {
            if (ReferenceEquals(building, targetBuilding)
                && characterId.Equals(targetCharacter)
                && kind == BuildingTransientOwnershipKind.ActiveUse)
            {
                FailureCount++;
                throw new InvalidOperationException(FailureMessage);
            }
            original.UntrackTransientCharacterOwnership(building, characterId, kind);
        }
        public void UntrackAllTransientCharacterOwnership(IBuildingWorldEntryPort building) =>
            original.UntrackAllTransientCharacterOwnership(building);
    }

    private sealed class LiveCheckoutClock : IGameClock
    {
        public float DeltaTime => 0.25f;
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Advance() { Time += DeltaTime; FrameCount++; }
    }

    private sealed class FacilityShopScenario : ScenarioBase
    {
        private readonly GameObject host;
        private readonly BuildingSO building;
        private readonly GameSessionState state;
        private readonly DailyFacilityShopRuntime runtime;
        private readonly TreasuryEconomyAggregateStateStore treasury;
        private readonly FacilityShopFactEditorFixture purchaseFacts;

        internal FacilityShopScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false,
            int initialMoney = 100000,
            bool unlockBasic = false,
            IFacilityShopPurchaseDelivery purchaseDelivery = null)
            : base(target, faultMode, deliveryFault, "facility-shop")
        {
            state = new GameSessionState(initialMoney, 5);
            var session = new TestGameSessionStateStore(state);
            var root = Outcomes.AggregateRootStore;
            treasury = new TreasuryEconomyAggregateStateStore(root);
            building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset")
                ?? throw new InvalidOperationException(
                    "The authored facility-shop building fixture is missing.");
            host = new GameObject("Migrated Facility Shop E2E");
            runtime = host.AddComponent<DailyFacilityShopRuntime>();
            runtime.ConstructDailyFacilityShopRuntime(
                new SingleFacilityShopCatalog(building),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IRunVariableRuntimeReader>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IMetaProgressionRuntimeReader>(),
                new DungeonStory.Foundation.GameEventBus(),
                new EditorGameMoneyAccount(
                    state,
                    new EconomyTransactionLedgerRuntime(
                        new UnityGameClock(), treasury)),
                autoProcurement: null,
                buildingCategoryCatalog:
                    CharacterAiEditorTestDependencies.AuthoredGameplay,
                aggregateRootStore: root,
                debugRules: DisabledDungeonDebugRuleQuery.Instance);
            purchaseFacts = new FacilityShopFactEditorFixture(Outcomes, Probe, purchaseDelivery);
            runtime.ConstructPurchaseFacts(
                purchaseFacts.Journal,
                session,
                treasury);
            if (unlockBasic)
            {
                Require(
                    FacilityShopService.CanEnterBasicPurchase(building)
                    && runtime.UnlockState.UnlockBasicPurchaseById(building.id),
                    "The authored facility-shop fixture cannot enter basic purchase.");
            }
            runtime.Refresh(5, FacilityShopRefreshCause.Initialization);
            Require(
                runtime.CurrentDailyOffers.Count > 0,
                "The authored facility-shop fixture produced no daily offer.");
            if (unlockBasic)
            {
                Require(
                    runtime.CurrentBasicPurchaseOffers.Count > 0,
                    "The authored facility-shop fixture produced no basic offer.");
            }
            InitialMoney = state.holdingMoney.Value;
        }

        internal int InitialMoney { get; }
        internal int Money => state.holdingMoney.Value;
        internal int PurchaseSequence => runtime.CaptureState().PurchaseSequence;
        internal FacilityShopFactEditorFixture Facts => purchaseFacts;
        internal DailyFacilityShopRuntime ShopOwner => runtime;
        internal GameSessionState Session => state;
        internal TreasuryEconomyAggregateStateStore Treasury => treasury;
        internal IReadOnlyList<EconomyTransactionRecord> PaymentRecords =>
            treasury.Current.Ledger.Records;

        internal bool Purchase() => runtime.TryPurchaseDailyOffer(
            0,
            state,
            out _);

        internal bool Purchase(out FacilityShopPurchaseResult result) =>
            runtime.TryPurchaseDailyOffer(0, state, out result);

        internal bool PurchaseDailyAt(
            int index,
            out FacilityShopPurchaseResult result) =>
            runtime.TryPurchaseDailyOffer(index, state, out result);

        internal bool PurchaseBasic(out FacilityShopPurchaseResult result) =>
            runtime.TryPurchaseBasicOffer(0, state, out result);

        public override void Dispose()
        {
            purchaseFacts?.Dispose();
            if (host != null)
                Object.DestroyImmediate(host);
        }
    }

    private sealed partial class OperatingDayScenario : ScenarioBase
    {
        private readonly GameObject host;
        private readonly GameSessionState state;
        private readonly OperatingDaySettlementRuntime runtime;
        private readonly TestGameSessionStateStore session;
        private readonly TreasuryEconomyAggregateStateStore treasury;
        private readonly EconomyTransactionLedgerRuntime transactionLedger;
        private readonly PaidFacilityContractRuntime paidFacilities;

        internal OperatingDayScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
            : base(target, faultMode, deliveryFault, "operating-day")
        {
            GameData settings = ScriptableObject.CreateInstance<GameData>();
            IGameSessionStateMutation mutation;
            try
            {
                state = GameSessionState.Create(settings, out mutation);
                mutation.Reset(100, 1, 1); // Explicit QA preparation, not earned income.
            }
            finally { Object.DestroyImmediate(settings); }
            session = new TestGameSessionStateStore(state, mutation);
            var root = Outcomes.AggregateRootStore;
            treasury = new TreasuryEconomyAggregateStateStore(root);
            transactionLedger = new EconomyTransactionLedgerRuntime(
                new DungeonStory.Foundation.UnityGameClock(),
                treasury);
            var money = new GameMoneyAccount(session, transactionLedger);
            treasury.Current.FacilityContracts.Add(
                new PaidFacilityContractState
                {
                    contractId = "contract:test-hub",
                    facilityPersistentId = "test-hub",
                    dailyCost = 7,
                    active = true,
                    lastSettledDay = 0
                });
            paidFacilities = new PaidFacilityContractRuntime(
                session,
                money,
                treasury);
            Facts = new OperatingDayFactEditorFixture(Outcomes, Probe);
            host = new GameObject("Migrated Operating Day E2E");
            runtime = host.AddComponent<OperatingDaySettlementRuntime>();
            runtime.Construct(
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IBuildingWorldQuery>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<ICharacterWorldQuery>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IFacilityShopCatalog>(),
                MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                    .Create<IRunVariableRuntimeReader>(),
                session,
                new DungeonStory.Foundation.GameEventBus(),
                new ZeroEmploymentRuntime(),
                money,
                paidFacilities,
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                CharacterAiEditorTestDependencies.AuthoredGameplay,
                root);
            runtime.ConstructSettlementFacts(
                Facts.Journal,
                session,
                treasury);
            runtime.OnTriggerEvent(new OperatingDayStartedEvent(1));
            InitialMoney = state.holdingMoney.Value;
        }

        internal int InitialMoney { get; }
        internal OperatingDayFactEditorFixture Facts { get; }
        internal int Money => state.holdingMoney.Value;
        internal int ReportCount => runtime.ReportHistory.Count;
        internal int LatestReportDay => runtime.LatestReport?.day ?? 0;
        internal OperatingDayReport LatestReport => runtime.LatestReport;
        internal IReadOnlyList<EconomyTransactionRecord> PaymentRecords =>
            Outcomes.AggregateRootStore
                .GetOrCreate(() => new TreasuryEconomyAggregateState())
                .Ledger.Records;

        internal void SeedDetailedLedger()
        {
            runtime.RestorePersistentState(
                new OperatingDaySettlementPersistenceState(
                    currentDay: 1,
                    totalRevenue: 120,
                    totalVisits: 2,
                    restockFailureCount: 1,
                    facilityDetails: new[]
                    {
                        new OperatingDayFacilityLedgerEntry(
                            "test-hub",
                            "test-hub",
                            120,
                            2,
                            0,
                            new Dictionary<int, int>
                            {
                                [(int)StockCategory.Food] = 3
                            },
                            new[] { 60f, 80f })
                    },
                    speciesVisits: new Dictionary<string, int>
                    {
                        ["Orc"] = 2
                    },
                    consumedStock: new Dictionary<StockCategory, int>
                    {
                        [StockCategory.Food] = 3
                    },
                    visitorMoodSamples: new[] { 60f, 80f },
                    stockSupplyResults: Array.Empty<StockSupplyResult>(),
                    incidents: new[] { "test-incident" },
                    eventLog: new[] { "test-log" },
                    reportHistory: Array.Empty<OperatingDayReport>()));
        }

        internal string CaptureSavePayload() => CreateSaveSection().Capture();

        internal string CaptureFinancePayload() => JsonUtility.ToJson(new OperatingDayFinanceSaveData
        {
            session = GameSessionSaveData.From(state.Capture()),
            ledger = transactionLedger.Capture(),
            contracts = paidFacilities.Capture()
        });

        // Existing capture APIs detach DTOs and do not execute settlement or delivery.
        internal string CaptureCheckpoint() => JsonUtility.ToJson(new OperatingDayCheckpoint
        {
            reportPayload = CaptureSavePayload(),
            finance = JsonUtility.FromJson<OperatingDayFinanceSaveData>(CaptureFinancePayload()),
            outcomes = Outcomes.Ledger.CaptureGameplayOutcomes(),
            outcomeOwners = Outcomes.Transaction.Capture(),
            facts = Facts.Journal.Capture()
        });

        internal void RestoreCheckpoint(string json)
        {
            OperatingDayCheckpoint saved = JsonUtility.FromJson<OperatingDayCheckpoint>(json);
            session.Restore(saved.finance.session.ToSnapshot());
            TreasuryEconomyAggregateState candidate = new TreasuryEconomyAggregateState();
            transactionLedger.PopulateRestoreState(candidate, saved.finance.ledger);
            paidFacilities.PopulateRestoreState(candidate, saved.finance.contracts);
            treasury.Replace(candidate);
            Outcomes.Ledger.BeginRestoreCandidate();
            try
            {
                Outcomes.Ledger.PublishGameplayOutcomeRestore(
                    Outcomes.Ledger.PrepareGameplayOutcomeRestore(saved.outcomes));
                Outcomes.Ledger.PublishRestoreCandidate();
                Outcomes.Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                Outcomes.Ledger.DiscardRestoreCandidate();
                throw;
            }
            Outcomes.Transaction.Restore(
                Outcomes.Transaction.PrepareRestore(saved.outcomeOwners));
            Facts.Journal.Restore(saved.facts);
            RestoreSavePayload(saved.reportPayload);
        }

        internal void RestoreSavePayload(string payload)
        {
            DungeonGameRestoreReport report = new();
            CreateSaveSection().Restore(
                payload,
                OperatingDaySettlementSaveSection.CurrentVersion,
                report);
            Require(
                report.Success,
                "The operating-day JSON save section rejected its own current payload.");
        }

        internal void Settle() => runtime.OnTriggerEvent(
            new OperatingDayEndedEvent(1));

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

        public override void Dispose()
        {
            Facts?.Dispose();
            if (host != null)
                Object.DestroyImmediate(host);
        }
    }

    private sealed class StockSupplyScenario : ScenarioBase
    {
        private readonly GameSessionState state;
        private readonly StatefulWorldItemStackProxy physical;
        private readonly WarehouseFeatureCommandService commands;
        private readonly StockSupplyFactEditorFixture facts;

        internal StockSupplyScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
            : base(target, faultMode, deliveryFault, "stock-supply")
        {
            state = new GameSessionState(100, 3);
            var session = new TestGameSessionStateStore(state);
            IWorldItemStackRuntime physicalRuntime =
                StatefulWorldItemStackProxy.Create(out physical);
            var root = Outcomes.AggregateRootStore;
            var treasury = new TreasuryEconomyAggregateStateStore(root);
            facts = new StockSupplyFactEditorFixture(
                Outcomes,
                treasury,
                Probe);
            commands = new WarehouseFeatureCommandService(
                new WarehouseCommandSessionContext(
                    session,
                    new EditorGameMoneyAccount(state),
                    new DungeonStory.Foundation.GameEventBus(),
                    DisabledDungeonDebugRuleQuery.Instance,
                    facts.Journal,
                    session,
                    treasury),
                new WarehouseCommandWorldContext(
                    physicalRuntime,
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IWarehouseWorldQuery>(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IBuildingWorldQuery>()),
                new WarehouseCommandPlanningContext(
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IResourceStockPolicyRuntime>(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IRegionalSupplyContractRuntime>(),
                    MigratedProducerEconomyServiceEntryE2EDebugScenarios.DefaultProxy
                        .Create<IGrandProjectRuntime>()));
            Offer = new StockDeliveryOffer(
                StockCategory.General,
                "material:lumber",
                3,
                12,
                "원장 검증 납품");
            InitialMoney = state.holdingMoney.Value;
        }

        internal StockDeliveryOffer Offer { get; }
        internal int InitialMoney { get; }
        internal int Money => state.holdingMoney.Value;
        internal int Quantity => physical.Quantity;
        internal WarehouseFeatureCommandResult Purchase() =>
            commands.PurchaseDelivery(Offer);

        public override void Dispose() => facts.Dispose();
    }

    private sealed class TestGameSessionStateStore : IGameSessionStateStore
    {
        private readonly GameSessionState state;
        private readonly IGameSessionStateMutation mutation;

        internal TestGameSessionStateStore(GameSessionState state, IGameSessionStateMutation mutation = null)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            this.mutation = mutation;
        }

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = state;
            return true;
        }

        public void Restore(GameSessionSnapshot snapshot)
        {
            if (mutation != null)
            {
                mutation.Restore(snapshot);
                return;
            }
            state.holdingMoney.Initialize(snapshot.Money);
            state.day.Initialize(snapshot.Day);
            state.gameSpeed.Initialize(snapshot.GameSpeed);
            state.curTime.Initialize(snapshot.ElapsedSeconds);
            state.hour.Initialize(snapshot.Hour);
            state.timeOfDay.Initialize(snapshot.TimeOfDay);
        }
    }

    [Serializable]
    private sealed class OperatingDayFinanceSaveData
    {
        public GameSessionSaveData session;
        public EconomyTransactionLedgerSaveData ledger;
        public PaidFacilityContractSaveData contracts;
    }

    [Serializable]
    private sealed class OperatingDayCheckpoint
    {
        public string reportPayload;
        public OperatingDayFinanceSaveData finance;
        public GameplayOutcomeLedgerSaveData outcomes;
        public DungeonMigratedProducerOutcomeSaveData outcomeOwners;
        public OperatingDayFactJournalData facts;
    }

    private sealed class SingleFacilityShopCatalog : IFacilityShopCatalog
    {
        private readonly BuildingSO building;

        internal SingleFacilityShopCatalog(BuildingSO building) =>
            this.building = building
                ?? throw new ArgumentNullException(nameof(building));

        public IReadOnlyCollection<BuildingSO> Buildings => new[] { building };
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints =>
            Array.Empty<FacilityBlueprintSO>();
        public BuildingSO FindBuildingById(int buildingId) =>
            building.id == buildingId ? building : null;
    }

    private sealed class ZeroEmploymentRuntime : IEmploymentContractRuntime
    {
        public IReadOnlyList<EmployeeWageState> WageStates =>
            Array.Empty<EmployeeWageState>();
        public IReadOnlyList<MercenaryContract> MercenaryContracts =>
            Array.Empty<MercenaryContract>();
        public IEmploymentSettlementScope BeginSettlement() =>
            new NoopSettlementScope();
        public int ForecastCost(int days) => 0;
        public int GetDailyCost(string characterId) => 0;
        public int QuoteMercenaryDailyCost(
            string characterId,
            int level,
            int rolePremium) => 0;
        public EmploymentDailySettlement SettleDay(int day) => new()
        {
            day = Math.Max(1, day)
        };
        public bool TryHireMercenary(
            CharacterActor actor,
            int rolePremium,
            int day,
            out string failureReason)
        {
            failureReason = "not used";
            return false;
        }
        public bool SetEmployeeRolePremium(
            string characterId,
            int premium,
            out string failureReason)
        {
            failureReason = "not used";
            return false;
        }
        public EmploymentContractSaveData Capture() => new();

        private sealed class NoopSettlementScope :
            IEmploymentSettlementScope
        {
            public void Commit() { }
            public void Dispose() { }
        }
    }

    private sealed class AlwaysCrimeRiskEvaluator : IFacilityCrimeRiskEvaluator
    {
        internal static readonly AlwaysCrimeRiskEvaluator Instance = new();
        public float CalculateShopliftingChance(FacilityCrimeRiskContext context) => 1f;
        public float CalculateOperationalRisk(FacilityCrimeRiskContext context) => 1f;
        public bool ShouldTriggerCrime(float chance, float roll) => true;
    }

    private sealed class NeutralRunVariableReader : IRunVariableRuntimeReader
    {
        internal static readonly NeutralRunVariableReader Instance = new();
        public int GetInitialShopSeed() => 0;
        public IReadOnlyList<int> GetStartingBlueprintCandidateIds() =>
            Array.Empty<int>();
        public float GetGuestDemandMultiplier(string speciesTag) => 1f;
        public float GetStockCostMultiplier(StockCategory category) => 1f;
        public float GetFacilityShopCostMultiplier(BuildingSO building) => 1f;
        public float GetBlueprintCostMultiplier(
            FacilityBlueprintSO blueprint) => 1f;
        public float GetThreatRiseMultiplier() => 1f;
        public float GetWarningThresholdMultiplier() => 1f;
        public DungeonSurvivalPressure GetSurvivalPressure() =>
            DungeonSurvivalPressure.Standard;
        public InvasionIntruderSettings ApplyInvasionSettings(
            InvasionIntruderSettings source) => source;
    }

    private sealed class ZeroRandomStream : IRandomStream
    {
        internal static readonly ZeroRandomStream Instance = new();
        public ulong State => 0UL;
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
        public float NextFloat() => 0f;
        public bool Chance(float probability) => probability > 0f;
        public void Restore(ulong state)
        {
        }
    }

    public class StatefulWorldItemStackProxy : DispatchProxy
    {
        private const string StackId = "stack:migrated-economy-e2e";
        private string itemId = string.Empty;
        private string itemInstanceId = string.Empty;
        private StockCategory stockCategory;

        internal int Quantity { get; private set; }

        internal static IWorldItemStackRuntime Create(
            out StatefulWorldItemStackProxy proxy,
            StockCategory stockCategory = StockCategory.General)
        {
            IWorldItemStackRuntime runtime = DispatchProxy.Create<
                IWorldItemStackRuntime,
                StatefulWorldItemStackProxy>();
            proxy = (StatefulWorldItemStackProxy)(object)runtime;
            proxy.stockCategory = stockCategory;
            return runtime;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] arguments)
        {
            switch (targetMethod.Name)
            {
                case nameof(IWorldItemStackRuntime.Capture):
                {
                    var snapshot = new DungeonPhysicalItemSaveData();
                    if (Quantity > 0)
                    {
                        snapshot.stacks.Add(new WorldItemStackSaveData
                        {
                            stackId = StackId,
                            itemInstanceId = itemInstanceId,
                            itemId = itemId,
                            quantity = Quantity,
                            state = WorldItemStackState.Loose
                        });
                    }
                    return snapshot;
                }
                case nameof(IWorldItemStackRuntime.Restore):
                {
                    var snapshot = arguments[0] as DungeonPhysicalItemSaveData;
                    WorldItemStackSaveData restored = snapshot?.stacks?
                        .SingleOrDefault(value => value != null
                            && string.Equals(
                                value.stackId,
                                StackId,
                                StringComparison.Ordinal));
                    Quantity = Mathf.Max(0, restored?.quantity ?? 0);
                    itemId = restored?.itemId?.Trim() ?? string.Empty;
                    itemInstanceId = restored?.itemInstanceId?.Trim()
                        ?? string.Empty;
                    return null;
                }
                case nameof(IWorldItemStackRuntime.SpawnItemAtDropoff):
                {
                    itemId = arguments[0]?.ToString()?.Trim() ?? string.Empty;
                    int amount = Math.Max(0, (int)arguments[1]);
                    Quantity = checked(Quantity + amount);
                    arguments[3] = amount;
                    return true;
                }
                case nameof(IWorldItemStackRuntime.GetAllStacks):
                {
                    if (Quantity <= 0)
                        return Array.Empty<WorldItemStackSnapshot>();
                    return new[]
                    {
                        new WorldItemStackSnapshot
                        {
                            StackId = StackId,
                            ItemInstanceId = itemInstanceId,
                            ItemId = itemId,
                            DisplayName = itemId,
                            StockCategory = stockCategory,
                            Quantity = Quantity,
                            State = WorldItemStackState.Loose
                        }
                    };
                }
            }

            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (!parameters[index].IsOut && !parameterType.IsByRef)
                    continue;
                Type valueType = parameterType.GetElementType();
                arguments[index] = valueType != null && valueType.IsValueType
                    ? Activator.CreateInstance(valueType)
                    : null;
            }
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            if (returnType.IsArray)
                return Array.CreateInstance(returnType.GetElementType(), 0);
            if (returnType.IsGenericType)
            {
                Type generic = returnType.GetGenericTypeDefinition();
                if (generic == typeof(IReadOnlyList<>)
                    || generic == typeof(IReadOnlyCollection<>)
                    || generic == typeof(IEnumerable<>))
                {
                    return Array.CreateInstance(
                        returnType.GetGenericArguments()[0],
                        0);
                }
            }
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string FactValue(
        GameplayOutcomeSnapshot exact,
        string factId) => exact?.facts?.SingleOrDefault(value => value != null
            && string.Equals(
                value.factId,
                factId,
                StringComparison.Ordinal))?.value
        ?? string.Empty;

    private static bool RejectionReachedCommit(
        MigratedProducerOutcomeProbeTransaction probe,
        MigratedProducerOutcomeKind kind) =>
        probe.ReservationCount(kind) > 0
        && probe.CommitAttemptCount(kind) > 0
        && probe.CommitCount(kind) == 0;

    private static void RequireThrows<TException>(Action action)
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
        throw new InvalidOperationException(
            "The production entry did not surface the injected "
            + typeof(TException).Name + ".");
    }
}

public static class FacilityShopPurchaseT01DebugScenarios
{
    public static void RunOrThrow() =>
        MigratedProducerEconomyEntryExtendedE2EDebugScenarios
            .RunFacilityShopT01OrThrow();

    public static void RunFromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_T01_FACILITY_SHOP_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_T01_FACILITY_SHOP_REPORT_PATH is required.");
        }
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string rawPath = Path.ChangeExtension(fullPath, "raw.json");
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath)
            || File.Exists(rawPath))
        {
            throw new InvalidOperationException(
                "T01 facility-shop report must be a create-only path under Artifacts/QA.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        JObject rawEvidence = new JObject
        {
            ["schema"] = "dungeonstory-t01-facility-shop-raw@1",
            ["passed"] = false,
            ["restoreScope"] = "same-process canonical ledger; monetary owners retained",
            ["wholeWorldRestoreExecuted"] = false,
            ["coldProcessRestoreExecuted"] = false,
            ["naturalGameplay"] = false,
            ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false,
            ["stages"] = new JArray()
        };
        try
        {
            MigratedProducerEconomyEntryExtendedE2EDebugScenarios
                .RunFacilityShopT01OrThrow((JArray)rawEvidence["stages"]);
            rawEvidence["passed"] = true;
        }
        catch (Exception exception)
        {
            rawEvidence["failure"] = exception.ToString();
            throw;
        }
        finally
        {
            using FileStream rawStream = new(
                rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using StreamWriter rawWriter = new(rawStream);
            rawWriter.Write(rawEvidence.ToString(Formatting.Indented));
        }
        using FileStream stream = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(
            "U0_T01_FACILITY_SHOP=PASS\n"
            + "success=daily,basic\n"
            + "typedSuccess=offer,cost,balance-before-after,payment-transaction,durable-result\n"
            + "rejections=offer-selection,insufficient-funds,outcome-reservation,outcome-commit\n"
            + "rejectionPolicy=typed-stage,no-gameplay-outcome,no-authority-mutation\n"
            + "deliveryRecovery=durable-pending,actual-ledger-restore,retry-no-second-payment,completed-restore-noop\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_T01_FACILITY_SHOP=PASS; report=" + fullPath);
    }
}
#endif
