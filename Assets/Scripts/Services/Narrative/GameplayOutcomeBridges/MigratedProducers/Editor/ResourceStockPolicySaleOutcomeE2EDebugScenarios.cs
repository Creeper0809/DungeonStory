#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class ResourceStockPolicySaleOutcomeE2EDebugScenarios
{
    private const string LumberId = "material:lumber";
    private const string InjectedReservationFailure =
        "migrated-producer-e2e-injected-reservation-rejection";
    private const string OverflowCreditFailure =
        "골드 입금 결과가 정수 범위를 초과합니다.";
    private static readonly Vector2Int MarketPosition = new(9, 4);
    private static readonly List<string> expectedWarnings = new();
    private static readonly IReadOnlyList<string> expectedWarningsView =
        expectedWarnings.AsReadOnly();
    private static List<JObject> rawSnapshots;

    public static IReadOnlyList<string> ExpectedWarnings =>
        expectedWarningsView;

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        new[]
        {
            MigratedProducerOutcomeKind.ResourceStockPolicySaleResult,
            MigratedProducerOutcomeKind.QualityRejectedSaleResult
        };

    [MenuItem("DungeonStory/Debug/Narrative/Run Stock Policy Sale Outcome E2E")]
    public static void RunFromMenu() => Debug.Log(
        RunAll() ? "RESOURCE_STOCK_POLICY_SALE_OUTCOME_E2E_PASS" : "FAIL");

    public static void RunE07FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_E07_STOCK_POLICY_SALE_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_E07_STOCK_POLICY_SALE_REPORT_PATH is required.");
        }
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException(
                "Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string rawPath = Path.Combine(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("E07 report directory is unavailable."),
            "stock-policy-sale-report.raw.json");
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath) || File.Exists(rawPath))
        {
            throw new InvalidOperationException(
                "E07 stock-policy sale report must be a create-only path under Artifacts/QA.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "E07 report directory is unavailable."));
        rawSnapshots = new List<JObject>();
        string failure = string.Empty;
        try
        {
            Require(RunAll(), "E07 stock-policy sale scenarios failed.");
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            throw;
        }
        finally
        {
            using FileStream raw = new(rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using StreamWriter rawWriter = new(raw);
            rawWriter.Write(new JObject
            {
                ["schemaVersion"] = "dungeonstory-e07-sale-raw@1",
                ["status"] = failure.Length == 0 ? "PASS" : "FAIL",
                ["failure"] = failure,
                ["runtimeEntry"] = "ResourceStockPolicyRuntime.Tick",
                ["scope"] = "Controlled production-owner integration; fresh scoped save registry plus canonical finance/session owners, not whole-world or cold-process restore.",
                ["fixturePreparation"] = "Explicit initial stock/equipment/money, fixed clock/calendar/drop zones, empty world/production ports and injected outcome faults. Overflow recovery uses an explicit test-only DebugAdjustment debit, not an earned purchase or natural narrative.",
                ["brainExecuted"] = false,
                ["wholeWorldRestore"] = false,
                ["coldProcessRestore"] = false,
                ["naturalNarrativeEligible"] = false,
                ["trainingEligible"] = false,
                ["humanApprovalClaimed"] = false,
                ["fullAuditClosure"] = false,
                ["snapshots"] = new JArray(rawSnapshots)
            }.ToString());
            rawSnapshots = null;
        }
        using FileStream report = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(report);
        writer.Write(
            "U0_E07_STOCK_POLICY_SALE=PASS\n"
            + "runtimeEntry=ResourceStockPolicyRuntime.Tick\n"
            + "sales=surplus-stock,quality-rejected\n"
            + "branches=pre-sale-rejection,disposed-income-pending,restore-retry-no-duplicate\n"
            + "joins=physical-child,economy-transaction,parent-outcome\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_E07_STOCK_POLICY_SALE=PASS; report=" + fullPath);
    }

    public static bool RunAll(bool throwOnFailure = true)
    {
        expectedWarnings.Clear();
        try
        {
            _ = ResourceStockPolicySaleOutcomeDetails.Preview(
                "stock-policy-sale:00000001:material:lumber",
                LumberId,
                ResourceStockPolicySaleOutbox.FormatDestinationId(LumberId),
                2,
                1L,
                7,
                10,
                7000,
                qualityRejected: false);
            VerifyNormalSaleReservationGuardAndRecovery();
            VerifyBufferedSaleWithoutFreeSurplus();
            VerifyRejectedSaleRecoveryAndAuthorityRelease();
            VerifyDisposedIncomePendingRecovery(qualityRejected: false);
            VerifyDisposedIncomePendingRecovery(qualityRejected: true);
            Require(expectedWarnings.Count == 4,
                "Stock-policy sale fixture did not declare its four injected warnings exactly.");
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    private static void VerifyNormalSaleReservationGuardAndRecovery()
    {
        using Scenario scenario = new("run:stock-policy-sale-e07-normal");
        ResourceStockPolicyData policy = scenario.Runtime.GetOrCreate(LumberId);
        policy.enabled = true;
        policy.minimumStock = 0;
        policy.targetStock = 0;
        policy.maximumStock = 0;
        policy.surplusDisposition = StockSurplusDisposition.Sell;
        Require(scenario.Runtime.SetPolicy(policy, out string policyFailure),
            "Normal sale policy failed: " + policyFailure);

        string destination = ResourceStockPolicySaleOutbox.FormatDestinationId(
            LumberId);
        string delivered = WorldItemRepositoryEditorAccess.AddStack(
            scenario.Repository,
            LumberId,
            2,
            WorldItemStackState.FacilityBuffer,
            destination,
            position: MarketPosition);
        _ = WorldItemRepositoryEditorAccess.AddStack(
            scenario.Repository,
            LumberId,
            2,
            WorldItemStackState.Loose,
            position: new Vector2Int(2, 2));

        scenario.Probe.TargetKind =
            MigratedProducerOutcomeKind.ResourceStockPolicySaleResult;
        scenario.Observe("normal/before");
        scenario.Probe.FaultMode =
            MigratedProducerOutcomeFaultMode.RejectReservation;
        scenario.Runtime.Tick();
        Require(scenario.Repository.GetEditorTestQuantity(delivered) == 2
                && scenario.Money.Balance == 100
                && scenario.Runtime.Capture().pendingSales.Count == 0,
            "Parent reservation rejection mutated physical stock or money.");
        scenario.Observe("normal/reservation-rejected");

        string beforeState = JsonUtility.ToJson(scenario.Runtime.Capture());
        scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
        scenario.AdvanceClock();
        scenario.Runtime.Tick();
        DungeonResourceStockPolicySaveData interrupted = scenario.Runtime.Capture();
        Require(interrupted.pendingSales.Count == 1
                && interrupted.pendingSales[0].phase
                    == ResourceStockPolicySaleCommitPhase.IncomePublished
                && scenario.Money.Balance > 100
                && scenario.Repository.GetEditorTestQuantity(delivered) == 0,
            "Normal sale commit fault did not retain the paid physical owner: "
                + $"pending={interrupted.pendingSales.Count}; "
                + $"phase={(interrupted.pendingSales.Count == 0 ? "none" : interrupted.pendingSales[0].phase.ToString())}; "
                + $"balance={scenario.Money.Balance}; "
                + $"delivered={scenario.Repository.GetEditorTestQuantity(delivered)}; "
                + $"reserve={scenario.Probe.ReservationCount(MigratedProducerOutcomeKind.ResourceStockPolicySaleResult)}; "
                + $"attempt={scenario.Probe.CommitAttemptCount(MigratedProducerOutcomeKind.ResourceStockPolicySaleResult)}; "
                + $"status={scenario.Runtime.Policies.Single(value => value.itemId == LumberId).lastStatus}");
        int creditedBalance = scenario.Money.Balance;
        ResourceStockPolicyPendingSale saved = interrupted.pendingSales[0].Clone();
        VerifyReservationOnlyFactRejection(scenario, saved);
        scenario.Observe("normal/income-published");

        DungeonResourceStockPolicySaveData roundTrip =
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                JsonUtility.ToJson(interrupted));
        scenario.Runtime.PublishRestoreCandidate(
            scenario.Runtime.PrepareRestoreCandidate(roundTrip));
        scenario.Observe("normal/same-runtime-restored");
        scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
        scenario.AdvanceClock();
        scenario.Runtime.Tick();

        Require(scenario.Runtime.Capture().pendingSales.Count == 0
                && scenario.Money.Balance == creditedBalance
                && scenario.LedgerRuntime.Records.Count(record =>
                    record != null
                    && record.succeeded
                    && record.kind == EconomyTransactionKind.SaleIncome
                    && record.sourceId == saved.operationId) == 1,
            "Restored normal sale duplicated income or retained its owner.");
        RequireParentCausation(
            scenario,
            MigratedProducerOutcomeKind.ResourceStockPolicySaleResult,
            saved.operationId,
            saved.physicalOutcomeOwnerRevision,
            saved.economyTransactionId,
            saved.sourceStackIds,
            "stock-policy-sale-from-physical-transfer",
            beforeState,
            JsonUtility.ToJson(scenario.Runtime.Capture()));
        scenario.Observe("normal/completed");
        VerifyFreshCompletedReplay(scenario, "normal");
    }

    private static void VerifyBufferedSaleWithoutFreeSurplus()
    {
        using Scenario scenario = new("run:stock-policy-sale-e07-buffered-only");
        ResourceStockPolicyData policy = scenario.Runtime.GetOrCreate(LumberId);
        policy.enabled = true;
        policy.minimumStock = 0;
        policy.targetStock = 0;
        policy.maximumStock = 0;
        policy.surplusDisposition = StockSurplusDisposition.Sell;
        Require(scenario.Runtime.SetPolicy(policy, out string policyFailure),
            "Buffered-only policy failed: " + policyFailure);
        string destination = ResourceStockPolicySaleOutbox.FormatDestinationId(
            LumberId);
        string firstStack = WorldItemRepositoryEditorAccess.AddStack(
            scenario.Repository,
            LumberId,
            1,
            WorldItemStackState.FacilityBuffer,
            destination,
            position: MarketPosition);
        string secondStack = WorldItemRepositoryEditorAccess.AddStack(
            scenario.Repository,
            LumberId,
            2,
            WorldItemStackState.FacilityBuffer,
            destination,
            position: MarketPosition);
        Require(firstStack != secondStack
                && scenario.Runtime.CountOwned(LumberId) == 0,
            "Buffered-only regression must have no free surplus, not an unrelated source stack.");
        scenario.Observe("buffered/before");
        scenario.Runtime.Tick();
        Require(scenario.Repository.GetEditorTestQuantity(firstStack) == 0
            && scenario.Repository.GetEditorTestQuantity(secondStack) == 0
            && scenario.Money.Balance > 100
            && scenario.Runtime.Capture().pendingSales.Count == 0
            && scenario.LedgerRuntime.Records.Count(value => value.succeeded && value.kind == EconomyTransactionKind.SaleIncome) == 1,
            "Fully delivered sale input was retired before export when free surplus reached zero.");
        RequireParentSourceStacks(
            scenario,
            new[] { firstStack, secondStack });
        scenario.Observe("buffered/completed");
        int after = scenario.Money.Balance;
        scenario.AdvanceClock();
        scenario.Runtime.Tick();
        Require(scenario.Money.Balance == after
            && scenario.LedgerRuntime.Records.Count(value => value.succeeded && value.kind == EconomyTransactionKind.SaleIncome) == 1,
            "Buffered-only sale duplicated its acknowledged income on the next tick.");
        scenario.Observe("buffered/replay");
        VerifyFreshCompletedReplay(scenario, "buffered");
    }

    private static void VerifyRejectedSaleRecoveryAndAuthorityRelease()
    {
        using Scenario scenario = new("run:stock-policy-sale-e07-rejected");
        CombatEquipmentInstance equipment = scenario.CombatEquipment.CreateInstance(
            "weapon:greatsword",
            CombatEquipmentQuality.Good,
            CombatEquipmentWorldState.Loose);
        string itemId = PhysicalItemIds.ForEquipment(equipment.definitionId);
        string routeFailure = string.Empty;
        Require(scenario.Items.SpawnExistingUniqueItemAt(
                itemId,
                (ItemInstanceId)equipment.instanceId,
                MarketPosition,
                WorldItemStackState.FacilityBuffer,
                QualityRejectedOutputRules.MarketDestinationId,
                out string stackId)
            && scenario.CombatEquipment.TryLinkToWorldStack(
                equipment.instanceId,
                stackId,
                CombatEquipmentWorldState.Loose)
            && scenario.Items.TryRouteStackToDestination(
                stackId,
                WorldItemStackState.FacilityBuffer,
                QualityRejectedOutputRules.MarketDestinationId,
                MarketPosition,
                out routeFailure),
            "Rejected equipment could not enter the exact market buffer: "
                + routeFailure);
        WorldItemStackSnapshot warningStack = scenario.Items.GetAllStacks()
            .Single(value => value.StackId == stackId);

        scenario.Probe.TargetKind =
            MigratedProducerOutcomeKind.QualityRejectedSaleResult;
        scenario.Observe("rejected/before");
        scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectReservation;
        ExpectRejectedSaleWarning(
            warningStack.DisplayName,
            InjectedReservationFailure);
        scenario.Runtime.Tick();
        Require(scenario.Repository.GetEditorTestQuantity(stackId) == 1
                && scenario.Money.Balance == 100
                && scenario.Runtime.Capture().pendingRejectedSales.Count == 0
                && scenario.CombatEquipment.TryGetInstance(equipment.instanceId, out CombatEquipmentInstance untouched)
                && untouched.worldState == CombatEquipmentWorldState.Loose,
            "Rejected-quality reservation failure changed physical, money or unique authority.");
        scenario.Observe("rejected/reservation-rejected");
        scenario.AdvanceClock();
        string beforeState = JsonUtility.ToJson(scenario.Runtime.Capture());
        scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
        scenario.Runtime.Tick();
        DungeonResourceStockPolicySaveData interrupted = scenario.Runtime.Capture();
        Require(interrupted.pendingRejectedSales.Count == 1
                && interrupted.pendingRejectedSales[0].phase
                    == QualityRejectedSaleCommitPhase.IncomePublished
                && scenario.CombatEquipment.TryGetInstance(
                    equipment.instanceId,
                    out CombatEquipmentInstance pendingAuthority)
                && pendingAuthority.worldState
                    == CombatEquipmentWorldState.MarketSalePending,
            "Rejected sale fault lost its paid owner or unique authority.");
        int creditedBalance = scenario.Money.Balance;
        QualityRejectedSalePending saved =
            interrupted.pendingRejectedSales[0].Clone();
        VerifyReservationOnlyFactRejection(scenario, saved);
        scenario.Observe("rejected/income-published");

        DungeonResourceStockPolicySaveData roundTrip =
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                JsonUtility.ToJson(interrupted));
        scenario.Runtime.PublishRestoreCandidate(
            scenario.Runtime.PrepareRestoreCandidate(roundTrip));
        scenario.Observe("rejected/same-runtime-restored");
        scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
        scenario.AdvanceClock();
        scenario.Runtime.Tick();

        Require(scenario.Runtime.Capture().pendingRejectedSales.Count == 0
                && scenario.Money.Balance == creditedBalance
                && !scenario.CombatEquipment.TryGetInstance(
                    equipment.instanceId,
                    out _)
                && scenario.LedgerRuntime.Records.Count(record =>
                    record != null
                    && record.succeeded
                    && record.kind == EconomyTransactionKind.SaleIncome
                    && record.sourceId == saved.operationId) == 1,
            "Restored rejected sale duplicated income or retained authority.");
        RequireParentCausation(
            scenario,
            MigratedProducerOutcomeKind.QualityRejectedSaleResult,
            saved.operationId,
            saved.physicalOutcomeOwnerRevision,
            saved.economyTransactionId,
            new[] { saved.sourceStackId },
            "quality-rejected-sale-from-physical-transfer",
            beforeState,
            JsonUtility.ToJson(scenario.Runtime.Capture()));
        scenario.Observe("rejected/completed");
        VerifyFreshCompletedReplay(scenario, "rejected");
    }

    private static void VerifyDisposedIncomePendingRecovery(bool qualityRejected)
    {
        string profile = qualityRejected ? "rejected-overflow" : "normal-overflow";
        using Scenario scenario = new("run:stock-policy-sale-e07-" + profile, int.MaxValue);
        string stackId;
        if (qualityRejected)
        {
            CombatEquipmentInstance equipment = scenario.CombatEquipment.CreateInstance(
                "weapon:greatsword", CombatEquipmentQuality.Good, CombatEquipmentWorldState.Loose);
            Require(scenario.Items.SpawnExistingUniqueItemAt(
                    PhysicalItemIds.ForEquipment(equipment.definitionId),
                    (ItemInstanceId)equipment.instanceId, MarketPosition,
                    WorldItemStackState.FacilityBuffer, QualityRejectedOutputRules.MarketDestinationId,
                    out stackId)
                    && scenario.CombatEquipment.TryLinkToWorldStack(equipment.instanceId, stackId, CombatEquipmentWorldState.Loose),
                "Overflow rejected-sale preparation could not link its exact unique source.");
            Require(scenario.Items.TryRouteStackToDestination(stackId,
                    WorldItemStackState.FacilityBuffer, QualityRejectedOutputRules.MarketDestinationId,
                    MarketPosition, out string routeFailure),
                "Overflow rejected-sale preparation could not bind its exact market target: " + routeFailure);
            WorldItemStackSnapshot prepared = scenario.Items.GetAllStacks().Single(value => value.StackId == stackId);
            Require(prepared.HasDestinationPosition && prepared.DestinationPosition == MarketPosition
                    && prepared.State == WorldItemStackState.FacilityBuffer,
                "Overflow rejected-sale preparation lacks the exact public market route.");
            ExpectRejectedSaleWarning(
                prepared.DisplayName,
                OverflowCreditFailure);
        }
        else
        {
            ResourceStockPolicyData policy = scenario.Runtime.GetOrCreate(LumberId);
            policy.enabled = true;
            policy.minimumStock = policy.targetStock = policy.maximumStock = 0;
            policy.surplusDisposition = StockSurplusDisposition.Sell;
            Require(scenario.Runtime.SetPolicy(policy, out string policyFailure), policyFailure);
            // Prepare the input destination through the real request authority
            // before seeding explicit test-only delivered cargo. Directly adding
            // a buffer alone is not a valid durable pending-sale checkpoint.
            _ = WorldItemRepositoryEditorAccess.AddStack(scenario.Repository,
                LumberId, 2, WorldItemStackState.Loose, position: new Vector2Int(2, 2));
            scenario.Runtime.Tick();
            ResourceStockPolicyData claimed = scenario.Runtime.GetOrCreate(LumberId);
            Require(claimed.inputDestinationId == ResourceStockPolicySaleOutbox.FormatDestinationId(LumberId)
                    && claimed.inputCapacityGrams > 0L && claimed.inputMassAuthorityRevision > 0L
                    && !string.IsNullOrEmpty(claimed.inputCapacityFingerprint),
                "Overflow normal-sale preparation did not acquire its official exact input owner.");
            scenario.AdvanceClock();
            stackId = WorldItemRepositoryEditorAccess.AddStack(scenario.Repository,
                LumberId, 2, WorldItemStackState.FacilityBuffer,
                ResourceStockPolicySaleOutbox.FormatDestinationId(LumberId), position: MarketPosition);
        }
        scenario.Observe(profile + "/before");
        scenario.Runtime.Tick();
        DungeonResourceStockPolicySaveData pending = scenario.Runtime.Capture();
        string operationId;
        int proceeds;
        if (qualityRejected)
        {
            QualityRejectedSalePending sale = pending.pendingRejectedSales.Single();
            Require(sale.phase == QualityRejectedSaleCommitPhase.PhysicalCommitted
                    && scenario.CombatEquipment.TryGetInstance(sale.itemInstanceId, out CombatEquipmentInstance owned)
                    && owned.worldState == CombatEquipmentWorldState.MarketSalePending,
                "Actual overflow did not retain rejected-sale physical and unique custody.");
            operationId = sale.operationId;
            proceeds = sale.proceeds;
        }
        else
        {
            ResourceStockPolicyPendingSale sale = pending.pendingSales.Single();
            Require(sale.phase == ResourceStockPolicySaleCommitPhase.PhysicalCommitted,
                "Actual overflow did not retain the surplus-sale physical owner.");
            operationId = sale.operationId;
            proceeds = sale.proceeds;
        }
        Require(scenario.Repository.GetEditorTestQuantity(stackId) == 0
                && scenario.Money.Balance == int.MaxValue
                && scenario.LedgerRuntime.Records.Count == 0
                && scenario.Repository.GetEditorPendingBatchDispositionCount() == 1,
            "Actual credit rejection changed money or lost the single physical receipt.");
        scenario.Observe(profile + "/physical-pending");
        scenario.AdvanceClock();
        if (qualityRejected)
        {
            ExpectRejectedSaleRecoveryWarning(
                pending.pendingRejectedSales.Single().itemId,
                OverflowCreditFailure);
        }
        scenario.Runtime.Tick();
        scenario.Observe(profile + "/pending-retry");
        string checkpoint = scenario.CaptureCheckpoint();
        using Scenario restored = new("run:stock-policy-sale-e07-" + profile + "-fresh");
        restored.RestoreCheckpoint(checkpoint);
        RequireOwnedCheckpointEqual(checkpoint, restored.CaptureCheckpoint(), profile + " fresh pending restore");
        restored.Observe(profile + "/fresh-pending");
        restored.AdvanceClock();
        if (qualityRejected)
        {
            ExpectRejectedSaleRecoveryWarning(
                pending.pendingRejectedSales.Single().itemId,
                OverflowCreditFailure);
        }
        restored.Runtime.Tick();
        restored.Observe(profile + "/fresh-pending-retry");
        Require(restored.Money.Balance == int.MaxValue
                && restored.LedgerRuntime.Records.Count == 0
                && restored.Repository.GetEditorPendingBatchDispositionCount() == 1,
            "Restored overflow retry duplicated disposal or published income.");
        Require(restored.Money.TrySpend(proceeds,
                new EconomyTransactionContext(EconomyTransactionKind.DebugAdjustment,
                    "qa:e07:overflow-headroom:" + profile, operationId,
                    "Explicit test-only credit headroom; excluded from training"), out string debitFailure),
            "Test-only headroom debit failed: " + debitFailure);
        restored.Observe(profile + "/test-only-headroom");
        restored.AdvanceClock();
        restored.Runtime.Tick();
        Require(restored.Money.Balance == int.MaxValue
                && restored.Runtime.Capture().pendingSales.Count == 0
                && restored.Runtime.Capture().pendingRejectedSales.Count == 0
                && restored.Repository.GetEditorPendingBatchDispositionCount() == 0
                && restored.LedgerRuntime.Records.Count(value => value.succeeded
                    && value.kind == EconomyTransactionKind.SaleIncome
                    && value.sourceId == operationId && value.amount == proceeds) == 1,
            "Credit headroom recovery did not complete exactly once through the production owner.");
        if (qualityRejected)
        {
            QualityRejectedSalePending sale = pending.pendingRejectedSales.Single();
            RequireParentCausation(restored, MigratedProducerOutcomeKind.QualityRejectedSaleResult,
                operationId, sale.physicalOutcomeOwnerRevision,
                restored.LedgerRuntime.Records.Single(value => value.succeeded
                    && value.kind == EconomyTransactionKind.SaleIncome && value.sourceId == operationId).transactionId,
                new[] { sale.sourceStackId }, "quality-rejected-sale-from-physical-transfer",
                JsonUtility.ToJson(pending), JsonUtility.ToJson(restored.Runtime.Capture()));
        }
        else
        {
            ResourceStockPolicyPendingSale sale = pending.pendingSales.Single();
            RequireParentCausation(restored, MigratedProducerOutcomeKind.ResourceStockPolicySaleResult,
                operationId, sale.physicalOutcomeOwnerRevision,
                restored.LedgerRuntime.Records.Single(value => value.succeeded
                    && value.kind == EconomyTransactionKind.SaleIncome && value.sourceId == operationId).transactionId,
                sale.sourceStackIds, "stock-policy-sale-from-physical-transfer",
                JsonUtility.ToJson(pending), JsonUtility.ToJson(restored.Runtime.Capture()));
        }
        restored.Observe(profile + "/completed");
        string completed = restored.CaptureCheckpoint();
        restored.AdvanceClock();
        restored.Runtime.Tick();
        RequireNoEffectReplay(completed, restored.CaptureCheckpoint(), profile + " completed replay");
        restored.Observe(profile + "/replay");
        VerifyFreshCompletedReplay(restored, profile);
    }

    private static void VerifyFreshCompletedReplay(Scenario scenario, string profile)
    {
        string saved = scenario.CaptureCheckpoint();
        using Scenario restored = new("run:stock-policy-sale-e07-" + profile + "-completed-fresh");
        restored.RestoreCheckpoint(saved);
        RequireOwnedCheckpointEqual(saved, restored.CaptureCheckpoint(), profile + " completed fresh restore");
        restored.Observe(profile + "/fresh-restored");
        string before = restored.CaptureCheckpoint();
        restored.AdvanceClock();
        restored.Runtime.Tick();
        // Public evaluation may update its status string, but must not replay any
        // effect, receipt, sequence, exact source stack or persisted unique owner.
        RequireNoEffectReplay(before, restored.CaptureCheckpoint(), profile + " fresh completed replay");
        restored.Observe(profile + "/fresh-replay");
    }

    private static void VerifyReservationOnlyFactRejection(
        Scenario scenario,
        ResourceStockPolicyPendingSale pending)
    {
        ResourceStockPolicyPendingSale mutable = pending.Clone();
        mutable.phase = default;
        mutable.commitId = string.Empty;
        mutable.inputMassGrams = 0L;
        mutable.physicalOutcomeOwnerRevision = 0L;
        mutable.economyTransactionId = string.Empty;
        mutable.balanceBefore = 0;
        mutable.balanceAfter = 0;
        var proposed = new ResourceStockSaleFact(mutable, completed: false);
        string immutableState = JsonUtility.ToJson(
            proposed.CaptureDomainState());
        mutable.sourceStackIds.Add("qa:added-source-stack");
        Require(JsonUtility.ToJson(proposed.CaptureDomainState())
                == immutableState,
            "Reserved sale fact retained the caller-owned source stack list.");
        ResourceStockPolicyPendingSale returned = proposed.CaptureDomainState();
        returned.sourceStackIds.Add("qa:mutated-returned-source-stack");
        Require(JsonUtility.ToJson(proposed.CaptureDomainState())
                == immutableState,
            "Reserved sale fact exposed its internal source stack list.");
        VerifyReservationOnlyFactRejection(
            scenario,
            proposed,
            "resource stock sale");
    }

    private static void VerifyReservationOnlyFactRejection(
        Scenario scenario,
        QualityRejectedSalePending pending)
    {
        QualityRejectedSalePending mutable = pending.Clone();
        mutable.phase = QualityRejectedSaleCommitPhase.Prepared;
        mutable.commitId = string.Empty;
        mutable.inputMassGrams = 0L;
        mutable.physicalOutcomeOwnerRevision = 0L;
        mutable.economyTransactionId = string.Empty;
        mutable.balanceBefore = 0;
        mutable.balanceAfter = 0;
        VerifyReservationOnlyFactRejection(
            scenario,
            new QualityRejectedSaleFact(mutable, completed: false),
            "quality-rejected sale");
    }

    private static void VerifyReservationOnlyFactRejection<TFact>(
        Scenario scenario,
        TFact proposed,
        string label)
    {
        string before = JsonUtility.ToJson(
            scenario.Ledger.CaptureGameplayOutcomes());
        Require(scenario.domainFacts.TryReserve(
                proposed,
                out DomainFactLease lease,
                out string reserveFailure)
            && !string.IsNullOrWhiteSpace(lease.PublicationKey),
            label + " proposal reservation failed: " + reserveFailure);
        string publicationKey = lease.PublicationKey;
        DomainFactDelivery delivery = scenario.domainFacts.Publish(
            lease,
            proposed);
        Require(!delivery.MayBeCommitted
                && delivery.Detail
                    == "fact-observer-proposal-cannot-be-published"
                && scenario.domainFacts.ActiveLeaseCount == 0
                && JsonUtility.ToJson(
                    scenario.Ledger.CaptureGameplayOutcomes()) == before,
            label + " proposal was accepted as a completed publication.");

        var replayResults = new DomainFactDelivery[1];
        bool replayed = scenario.domainFacts.ReplayBatch(
            new[] { DomainFactReplay.Create(publicationKey, proposed) },
            replayResults,
            out string replayFailure);
        Require(!replayed
                && !replayResults[0].MayBeCommitted
                && replayFailure
                    == "fact-observer-proposal-cannot-be-replayed"
                && replayResults[0].Detail == replayFailure
                && JsonUtility.ToJson(
                    scenario.Ledger.CaptureGameplayOutcomes()) == before,
            label + " proposal was accepted as a completed recovery replay.");
    }

    private static void RequireOwnedCheckpointEqual(string before, string after, string label)
    {
        JObject left = JObject.Parse(before);
        JObject right = JObject.Parse(after);
        // The official ledger restore advances only its world epoch. Never erase
        // result keys, hashes, sequence, receipts or content to force equality.
        Require((long)right["outcomes"]["worldEpoch"] == checked((long)left["outcomes"]["worldEpoch"] + 1L),
            label + " did not advance the official ledger restore epoch exactly once.");
        left["outcomes"]["worldEpoch"] = right["outcomes"]["worldEpoch"];
        Require(JToken.DeepEquals(left, right), label + " changed canonical saved owner state.");
    }

    private static void RequireNoEffectReplay(string before, string after, string label)
    {
        JObject left = JObject.Parse(before);
        JObject right = JObject.Parse(after);
        foreach (JToken policy in left["stock"]["policies"])
            ((JObject)policy).Remove("lastStatus");
        foreach (JToken policy in right["stock"]["policies"])
            ((JObject)policy).Remove("lastStatus");
        Require(JToken.DeepEquals(left, right), label + " changed a canonical effect owner, receipt or result identity.");
    }

    private static void RequireParentSourceStacks(
        Scenario scenario,
        IReadOnlyList<string> expectedSourceStackIds)
    {
        string[] expected = expectedSourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        GameplayResultKey parentKey = scenario.Probe.LatestCommittedKey(
            MigratedProducerOutcomeKind.ResourceStockPolicySaleResult);
        Require(expected.Length == 2
                && expected.Distinct(StringComparer.Ordinal).Count() == 2
                && parentKey.IsValid
                && scenario.Ledger.TryGetResultIdentity(
                    parentKey,
                    out GameplayOutcomeReplayIdentity parentIdentity)
                && scenario.Ledger.TryGetExact(
                    parentIdentity.OutcomeId,
                    out GameplayOutcomeSnapshot parent)
                && parent.facts.Count(fact =>
                    fact.factId == ResourceStockPolicySaleOutcomeDetails
                        .SourceStackIdsFact.Value) == 1
                && parent.facts.Any(fact =>
                    fact.factId == ResourceStockPolicySaleOutcomeDetails
                        .SourceStackIdsFact.Value
                    && fact.value == string.Join(";", expected))
                && parent.participants
                    .Where(participant => participant.entityKindId == "item-stack")
                    .Select(participant => participant.entityId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(expected, StringComparer.Ordinal),
            "Buffered sale parent did not preserve both exact source stacks.");
    }

    private static void ExpectRejectedSaleWarning(
        string displayName,
        string failureReason) => expectedWarnings.Add(
        $"품질 미달 완제품 판매 대기 · {displayName} · {failureReason}");

    private static void ExpectRejectedSaleRecoveryWarning(
        string itemId,
        string failureReason) => expectedWarnings.Add(
        $"품질 미달 완제품 판매 정산 복구 대기 · {itemId} · {failureReason}");

    [Serializable]
    private sealed class SaleCheckpoint
    {
        public DungeonResourceStockPolicySaveData stock;
        public DungeonPhysicalItemSaveData physical;
        public DungeonCombatEquipmentSaveData combat;
        public GameSessionSaveData session;
        public EconomyTransactionLedgerSaveData economy;
        public GameplayOutcomeLedgerSaveData outcomes;
    }

    private static void RequireParentCausation(
        Scenario scenario,
        MigratedProducerOutcomeKind kind,
        string operationId,
        long physicalOwnerRevision,
        string economyTransactionId,
        IReadOnlyList<string> sourceStackIds,
        string relation,
        string beforeState,
        string afterState)
    {
        GameplayResultKey parentKey = scenario.Probe.LatestCommittedKey(kind);
        GameplayResultKey physicalKey =
            ResourceStockPolicySaleOutcomeDetails.PhysicalResultKey(
                operationId,
                physicalOwnerRevision);
        string helperPhysicalResultKey = physicalKey.ToString();
        GameplayOutcomeReplayIdentity parentIdentity = default;
        GameplayOutcomeSnapshot parent = null;
        Require(parentKey.IsValid
                && scenario.Ledger.TryGetResultIdentity(
                    parentKey,
                    out parentIdentity)
                && scenario.Ledger.TryGetExact(
                    parentIdentity.OutcomeId,
                    out parent)
                && scenario.Ledger.TryGetResultIdentity(
                    physicalKey,
                    out GameplayOutcomeReplayIdentity physicalIdentity)
                && parent.parentRunId
                    == physicalIdentity.OutcomeId.RunId.Value
                && parent.parentSequence == physicalIdentity.OutcomeId.Sequence
                && parent.rootOperationId == operationId
                && parent.causationRelationId == relation
                && parent.facts.Any(fact =>
                    fact.factId == ResourceStockPolicySaleOutcomeDetails
                        .PhysicalResultKeyFact.Value
                    && fact.value == helperPhysicalResultKey)
                && parent.facts.Any(fact =>
                    fact.factId == ResourceStockPolicySaleOutcomeDetails
                        .EconomyTransactionFact.Value
                    && fact.value == economyTransactionId)
                && parent.facts.Any(fact =>
                    fact.factId == ResourceStockPolicySaleOutcomeDetails
                        .SourceStackIdsFact.Value
                    && fact.value == string.Join(
                        ";",
                        sourceStackIds.OrderBy(
                            value => value,
                            StringComparer.Ordinal)))
                && sourceStackIds.All(sourceStackId =>
                    parent.participants.Any(participant =>
                        participant.entityKindId == "item-stack"
                        && participant.entityId == sourceStackId)),
            "Sale parent did not preserve physical, payment and source-stack evidence: "
                + kind);
        MigratedProducerProductionTransitionEvidenceCapture.Record(
            kind, parentIdentity, parent, beforeState, afterState);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Scenario : IDisposable
    {
        private readonly FixedClock clock = new();
        private readonly FixedCalendar calendar = new(17, 9);
        private readonly EconomyProjectInputOwnerFixtureAuthority inputOwners;
        private readonly IResourceEconomyContentCatalog economyCatalog;
        private readonly IDungeonRestoreTransactionParticipant exactRoute;
        private readonly TreasuryEconomyAggregateStateStore treasury;
        private readonly SessionProvider session;
        internal readonly DomainFactDispatcher domainFacts;

        internal Scenario(string runId, int initialMoney = 100)
        {
            Root = new DungeonRuntimeAggregateRootStore();
            IKoreanJosaFormatter josa = new KoreanJosaFormatter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new TradeInventoryOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new TradeInventoryOutcomeAdapter(),
                    new MigratedProducerOutcomeAdapter()
                },
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    new MigratedProducerOutcomeDescriptorCatalog(josa)
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 64,
                    largePageCount: 8,
                    knownResultKeyCapacity: 256,
                    retainedSmallPageCount: 64,
                    retainedLargePageCount: 8),
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            Migrated = new MigratedProducerOutcomeRuntime(
                Root,
                Ledger,
                Recorder,
                Ledger);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Migrated,
                Recorder);
            domainFacts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            var saleFactMapping = new ResourceStockSaleFactMapping(Ledger);
            domainFacts.Subscribe(
                "fixture:resource-stock-policy-sale",
                new MigratedProducerDomainFactProbeObserver<
                    ResourceStockSaleFact>(
                    saleFactMapping.Capture,
                    Migrated,
                    commits,
                    Probe));
            domainFacts.Subscribe(
                "fixture:quality-rejected-sale",
                new MigratedProducerDomainFactProbeObserver<
                    QualityRejectedSaleFact>(
                    saleFactMapping.Capture,
                    Migrated,
                    commits,
                    Probe));
            domainFacts.Subscribe(
                "physical-disposition",
                new GameplayDomainFactObserver<
                    PhysicalItemDisposedFact,
                    TradeInventoryOutcomeReceipt>(
                    PhysicalItemDispositionFactObservation.Capture,
                    Registry,
                    Ledger,
                    Recorder,
                    commits));

            IDungeonItemCatalogProvider itemCatalog =
                EditorItemCatalogFactory.Create();
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                itemCatalog,
                Root,
                out WorldItemRepository repository,
                out CombatEquipmentRuntime combatEquipment,
                out ItemQuantityReservationService reservations,
                out _,
                out _,
                out _,
                out IDungeonRestoreTransactionParticipant exactRouteParticipant);
            exactRoute = exactRouteParticipant;
            Repository = repository;
            CombatEquipment = combatEquipment;
            var dispositions = new PhysicalItemBatchDispositionService(
                Repository,
                Items.MassQuery,
                EditorNullItemMarkerPresenter.Instance,
                reservations,
                Items.CatalogProvider,
                domainFacts,
                domainFacts,
                new GameplayDomainFactDeliveryQuery(Recorder, Ledger),
                calendar);
            IWorldItemStackRuntime routedItems = RoutedWorldItems.Create(
                Items,
                dispositions);
            var stock = new PhysicalStockQuery(
                Repository,
                Items.CatalogProvider,
                Items.MassQuery);
            var gateway = new PhysicalFacilityItemSinkGateway(
                stock,
                dispositions);

            treasury = new TreasuryEconomyAggregateStateStore(Root);
            LedgerRuntime = new EconomyTransactionLedgerRuntime(
                clock,
                treasury);
            session = new SessionProvider(initialMoney);
            Money = new GameMoneyAccount(
                session,
                LedgerRuntime);
            inputOwners = new EconomyProjectInputOwnerFixtureAuthority(
                Items.MassQuery);
            economyCatalog =
                new ResourceEconomyContentCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader()));
            Runtime = new ResourceStockPolicyRuntime(
                economyCatalog,
                new ResourceStockPolicyLogisticsDependencies(
                    routedItems,
                    gateway,
                    new FixedDropZones(MarketPosition),
                    new FixedRejectedDestination(MarketPosition),
                    DefaultProxy.Create<IWorkforceReplanService>()),
                new ResourceStockPolicyProductionDependencies(
                    DefaultProxy.Create<IProductionBillQuery>(),
                    DefaultProxy.Create<IProductionBillOrderCommand>(),
                    DefaultProxy.Create<IBuildingWorldQuery>()),
                CombatEquipment,
                Money,
                clock,
                calendar,
                Root,
                inputOwners.Runtime,
                domainFacts);
            Runtime.Initialize();
        }

        internal DungeonRuntimeAggregateRootStore Root { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal MigratedProducerOutcomeRuntime Migrated { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal CombatEquipmentRuntime CombatEquipment { get; }
        internal EconomyTransactionLedgerRuntime LedgerRuntime { get; }
        internal GameMoneyAccount Money { get; }
        internal ResourceStockPolicyRuntime Runtime { get; }

        internal void AdvanceClock() => clock.Advance(3f);

        internal string CaptureCheckpoint() => JsonUtility.ToJson(new SaleCheckpoint
        {
            stock = Runtime.Capture(), physical = Items.Capture(),
            combat = CombatEquipment.Capture(), session = session.Capture(),
            economy = LedgerRuntime.Capture(), outcomes = Ledger.CaptureGameplayOutcomes()
        });

        internal void Observe(string stage)
        {
            if (rawSnapshots == null) return;
            JObject snapshot = new()
            {
                ["stage"] = stage, ["gameTime"] = clock.Time,
                ["checkpoint"] = new JObject
                {
                    ["stock"] = JObject.Parse(JsonUtility.ToJson(Runtime.Capture())),
                    ["physical"] = JObject.Parse(JsonUtility.ToJson(Items.Capture())),
                    ["combat"] = JObject.Parse(JsonUtility.ToJson(CombatEquipment.Capture())),
                    ["session"] = JObject.Parse(JsonUtility.ToJson(session.Capture())),
                    ["economy"] = JObject.Parse(JsonUtility.ToJson(LedgerRuntime.Capture()))
                }
            };
            rawSnapshots.Add(snapshot);
            try
            {
                snapshot["checkpoint"]["outcomes"] = JObject.Parse(JsonUtility.ToJson(Ledger.CaptureGameplayOutcomes()));
            }
            catch (Exception exception)
            {
                snapshot["outcomeCaptureFailure"] = exception.ToString();
                throw;
            }
        }

        internal void RestoreCheckpoint(string json)
        {
            SaleCheckpoint saved = JsonUtility.FromJson<SaleCheckpoint>(json);
            Require(saved.combat?.craftOrders != null && saved.combat.craftOrders.Count == 0,
                "E07 sale restore fixture cannot stand in for a production craft-output restore.");
            // These are the canonical money/session owners, not a fake credit port.
            session.Restore(saved.session);
            TreasuryEconomyAggregateState finance = new();
            LedgerRuntime.PopulateRestoreState(finance, saved.economy);
            treasury.Replace(finance);
            IDungeonSaveSection[] sections = CreateSections();
            List<DungeonSaveSectionEnvelope> envelopes = sections.Select(section => new DungeonSaveSectionEnvelope
            {
                sectionId = section.SectionId, sectionVersion = section.SectionVersion,
                restorePhase = section.RestorePhase, optional = false,
                payloadJson = section.SectionId switch
                {
                    PhysicalItemsSaveSection.Id => JsonUtility.ToJson(saved.physical),
                    CombatEquipmentSaveSection.Id => CombatEquipmentSaveJson.Serialize(saved.combat),
                    ResourceStockPolicySaveSection.Id => JsonUtility.ToJson(saved.stock),
                    GameplayOutcomeLedgerSaveSection.Id => JsonUtility.ToJson(saved.outcomes),
                    _ => section.Capture()
                }
            }).ToList();
            DungeonSaveSectionRegistry registry = new(sections, Root,
                new IDungeonRestoreTransactionParticipant[] { Items, exactRoute, Ledger }
                    .Concat(inputOwners.RestoreParticipants).ToArray());
            DungeonGameRestoreReport report = new();
            Require(registry.RestoreAll(envelopes, report) && report.Success,
                "Fresh E07 scoped production save registry rejected its own checkpoint: "
                + string.Join(" | ", report.Errors));
        }

        private IDungeonSaveSection[] CreateSections() => new IDungeonSaveSection[]
        {
            new RegistryDependencyStubSection(ModularFacilityWorldSaveSection.Id, DungeonSaveRestorePhase.World),
            new RegistryDependencyStubSection(CharacterWorldSaveSection.Id, DungeonSaveRestorePhase.Characters),
            new PhysicalItemsSaveSection(Items, Items, EmptyWorldCandidates.Instance,
                ProductionOutputLifecycleRestoreCandidatePublisher.IsolatedSectionFixtureOnly),
            new CombatEquipmentSaveSection(CombatEquipment,
                ProductionOutputLifecycleRestoreCandidatePublisher.IsolatedSectionFixtureOnly,
                NoCraftOutputRestoreProxy.Create<IProductionDomainOutputRestoreJoin>(),
                NoCraftOutputRestoreProxy.Create<IProductionOutputMaximumMassRegistry>(),
                NoCraftOutputRestoreProxy.Create<IProductionOutputDetachedFacilityCapacityRestoreGuard>()),
            new RegistryDependencyStubSection(ProductionBillsSaveSection.Id, DungeonSaveRestorePhase.RuntimeState),
            new ResourceStockPolicySaveSection(Runtime, economyCatalog, Items, inputOwners.RestoreRuntime),
            new GameplayOutcomeLedgerSaveSection(Ledger)
        };

        public void Dispose()
        {
            domainFacts.Dispose();
            Items.Dispose();
            session.Dispose();
        }
    }

    public class RoutedWorldItems : DispatchProxy
    {
        private IWorldItemStackRuntime inner;
        private IPhysicalItemBatchDispositionService dispositions;

        internal static IWorldItemStackRuntime Create(
            IWorldItemStackRuntime inner,
            IPhysicalItemBatchDispositionService dispositions)
        {
            IWorldItemStackRuntime proxy = DispatchProxy.Create<
                IWorldItemStackRuntime,
                RoutedWorldItems>();
            var routed = (RoutedWorldItems)(object)proxy;
            routed.inner = inner;
            routed.dispositions = dispositions;
            return proxy;
        }

        protected override object Invoke(MethodInfo method, object[] args)
        {
            switch (method.Name)
            {
                case nameof(IWorldItemStackRuntime.TryCommitPendingBatchPhysicalDisposition):
                {
                    bool committed = dispositions.TryCommitPending(
                        (IReadOnlyList<PhysicalItemTransformInput>)args[0],
                        (PhysicalItemDispositionKind)args[1],
                        (string)args[2],
                        (string)args[3],
                        out PhysicalItemBatchDispositionReceipt receipt,
                        out string failure);
                    args[4] = receipt;
                    args[5] = failure;
                    return committed;
                }
                case nameof(IWorldItemStackRuntime.TryGetPendingBatchPhysicalDisposition):
                {
                    bool found = dispositions.TryGetPending(
                        (string)args[0],
                        out PhysicalItemBatchDispositionReceipt receipt);
                    args[1] = receipt;
                    return found;
                }
                case nameof(IWorldItemStackRuntime.AcknowledgeBatchPhysicalDisposition):
                {
                    bool acknowledged = dispositions.Acknowledge(
                        (string)args[0],
                        out string failure);
                    args[1] = failure;
                    return acknowledged;
                }
            }
            try
            {
                return method.Invoke(inner, args);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }
    }

    private sealed class FixedClock : IGameClock
    {
        public float DeltaTime => 1f;
        public float Time { get; private set; } = 1f;
        public int FrameCount => Mathf.FloorToInt(Time);
        public bool IsPaused => false;
        internal void Advance(float amount) => Time += Mathf.Max(0f, amount);
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        internal FixedCalendar(int day, int hour)
        {
            Day = day;
            Hour = hour;
        }

        public int Day { get; private set; }
        public int Hour { get; private set; }
        public int Year => 1;
        public int DayOfYear => Day;
        public Season Season => Season.Spring;
        public int DayOfSeason => Day;
        public long AbsoluteHour => ((long)Day - 1L) * 24L + Hour;
        public float ElapsedSeconds => AbsoluteHour * 60f;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning => true;
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() { }
        public void SetDateTime(int day, int hour)
        {
            Day = Math.Max(0, day);
            Hour = Mathf.Clamp(hour, 0, 23);
        }
    }

    private sealed class FixedDropZones : IWorldDropZoneQuery
    {
        private readonly Vector2Int position;
        internal FixedDropZones(Vector2Int position) => this.position = position;
        public bool TryGetDeliveryDropoff(out Vector2Int value)
        {
            value = position;
            return true;
        }
        public bool TryGetExpeditionLootDropoff(out Vector2Int value) =>
            TryGetDeliveryDropoff(out value);
        public bool TryGetVisitorEntryPoint(out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class FixedRejectedDestination :
        IQualityRejectedSaleDestinationAuthority
    {
        private readonly Vector2Int position;
        internal FixedRejectedDestination(Vector2Int position) =>
            this.position = position;
        public bool TryEnsureTarget(
            out FacilityBufferAcknowledgedOutputReleaseTarget target,
            out string failureReason)
        {
            target = new FacilityBufferAcknowledgedOutputReleaseTarget(
                QualityRejectedOutputRules.MarketDestinationId,
                position);
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class SessionProvider : IGameSessionStateProvider, IDisposable
    {
        private readonly GameSessionState state;
        private readonly IGameSessionStateMutation mutation;
        private readonly GameData settings;
        internal SessionProvider(int money)
        {
            settings = ScriptableObject.CreateInstance<GameData>();
            state = GameSessionState.Create(settings, out mutation);
            mutation.Reset(money, 1, 1);
        }
        internal GameSessionSaveData Capture() => GameSessionSaveData.From(state.Capture());
        internal void Restore(GameSessionSaveData saved) => mutation.Restore(saved.ToSnapshot());
        public void Dispose() => UnityEngine.Object.DestroyImmediate(settings);
        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = state;
            return true;
        }
    }

    private sealed class RegistryDependencyStubSection : IDungeonSaveSection,
        IDungeonSaveSectionPreflight, IDungeonStagedSaveSection, IDungeonRollbackFreeSaveSection
    {
        internal RegistryDependencyStubSection(string id, DungeonSaveRestorePhase phase)
        { SectionId = id; RestorePhase = phase; }
        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{\"fixture\":true}";
        public void ValidatePayload(string json, int version, DungeonGameRestoreReport report)
        {
            if (version != SectionVersion || json != Capture())
                report.AddError("E07 controlled dependency payload mismatch.");
        }
        public void Restore(string json, int version, DungeonGameRestoreReport report) =>
            StageRestore(json, version, report).Commit(report);
        public IDungeonSaveRestoreStage StageRestore(string json, int version, DungeonGameRestoreReport report) =>
            new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
    }

    private sealed class EmptyWorldCandidates : IRestoreWorldCandidateQuery
    {
        internal static readonly EmptyWorldCandidates Instance = new();
        public int Revision => 0;
        public bool TryGetGrid(out Grid grid) { grid = null; return false; }
        public bool TryGetBuildings(out IReadOnlyList<BuildableObject> values)
        { values = Array.Empty<BuildableObject>(); return true; }
        public bool TryGetCharacters(out IReadOnlyList<CharacterActor> values)
        { values = Array.Empty<CharacterActor>(); return true; }
        public bool TryGetWildlife(out IReadOnlyList<WildlifeActor> values)
        { values = Array.Empty<WildlifeActor>(); return true; }
        public bool TryGetExteriorZones(out IReadOnlyList<ExteriorZoneMarker> values)
        { values = Array.Empty<ExteriorZoneMarker>(); return true; }
    }

    public class DefaultProxy : DispatchProxy
    {
        internal static T Create<T>() where T : class =>
            DispatchProxy.Create<T, DefaultProxy>();

        protected override object Invoke(MethodInfo method, object[] args)
        {
            ParameterInfo[] parameters = method.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                if (!parameters[index].ParameterType.IsByRef)
                    continue;
                Type type = parameters[index].ParameterType.GetElementType();
                args[index] = type != null && type.IsValueType
                    ? Activator.CreateInstance(type)
                    : null;
            }
            Type result = method.ReturnType;
            if (result == typeof(void))
                return null;
            if (result.IsGenericType
                && result.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                return Array.CreateInstance(result.GetGenericArguments()[0], 0);
            }
            return result.IsValueType ? Activator.CreateInstance(result) : null;
        }
    }

    // E07 owns sales, not crafting. The empty craft-owner set still goes through
    // the real save-section preflight. Any attempted craft projection/adoption is
    // a loud fixture scope error, never a fabricated mass/capacity/result.
    public class NoCraftOutputRestoreProxy : DispatchProxy
    {
        internal static T Create<T>() where T : class =>
            DispatchProxy.Create<T, NoCraftOutputRestoreProxy>();

        protected override object Invoke(MethodInfo method, object[] args)
        {
            if (method.Name == nameof(IProductionDomainOutputRestoreJoin.Acknowledge)
                && args.Length == 1
                && args[0] is IReadOnlyList<ProductionDomainOutputRestoreAcknowledgement> { Count: 0 })
                return null;
            throw new InvalidOperationException("E07 has no craft-output owner: " + method.Name);
        }
    }
}
#endif
