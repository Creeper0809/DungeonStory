#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonStory.Content.CoreSession;
using DungeonStory.CoreSession;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ExternalInfluenceG09DebugScenarios
{
    private const string IntelSiteId = "food_farm";
    private const string TrailSiteId = "expedition-site:g09:trail";

    public static void RunG09FromBatch()
    {
        try
        {
            G09Report report = Run();
            WriteReport(report);
            Debug.Log("[AuditRemediation][G09] PASS operations="
                + report.operationCount);
        }
        catch (Exception exception)
        {
            WriteReport(new G09Report
            {
                passed = false,
                failure = exception.GetType().Name + ": " + exception.Message
            });
            Debug.LogException(exception);
            throw;
        }
    }

    private static G09Report Run()
    {
        using RuntimeFixture source = RuntimeFixture.Create();
        source.Runtime.Start();
        source.Events.Publish(new OperatingDayStartedEvent(7));

        source.Runtime.AddRenown(200f, "circus-show:g09:renown");
        source.Runtime.AddDread(100f, "circus-show:g09:dread");
        source.Runtime.AddHostileRumor(20f, "circus-show:g09:rumor");
        int rumorMoneyBefore = source.Money.Balance;
        Require(source.Runtime.TryMitigateHostileRumor(
                HostileRumorMitigationMethod.Gold,
                out float reducedRumor,
                out int rumorCost,
                out DomainFailure rumorFailure)
            && !rumorFailure.IsFailure,
            "gold rumor mitigation failed");

        ExternalInfluenceOperationSaveData rumorOperation = source.Runtime
            .Capture().operations.Single(operation =>
                operation.kind == ExternalInfluenceOperationKind.RumorMitigation);
        Require(rumorOperation.phase == ExternalInfluenceOperationPhase.Completed
                && rumorOperation.costKind == ExternalInfluenceCostKind.Gold
                && Mathf.Approximately(rumorOperation.costAmount, rumorCost)
                && rumorOperation.financeTransactionId.Length > 0
                && source.Money.Balance == rumorMoneyBefore - rumorCost
                && Mathf.Approximately(
                    rumorOperation.appliedAmount,
                    reducedRumor),
            "rumor source, cost, child, or result phase was not exact");

        using WorldMapFixture worldMap = new(source.Events, source.Runtime);
        int visibleBefore = worldMap.Runtime.VisibleTargets.Count;
        Require(worldMap.Runtime.TryUpgradeRecon(out _),
            "actual world-map recon upgrade failed");
        int newlyRevealed = worldMap.Runtime.VisibleTargets.Count - visibleBefore;
        ExternalInfluenceOperationSaveData reconOperation = source.Runtime
            .Capture().operations.Single(operation =>
                operation.kind == ExternalInfluenceOperationKind.ReconUpgrade);
        Require(reconOperation.phase == ExternalInfluenceOperationPhase.Completed
                && reconOperation.sourceId == nameof(OffenseWorldMapRuntime)
                && reconOperation.targetId == "recon:1"
                && reconOperation.newlyRevealed == newlyRevealed
                && newlyRevealed > 0,
            "actual recon did not preserve level and newly-revealed count");

        int intelMoneyBefore = source.Money.Balance;
        Require(worldMap.Runtime.TryUnlockTargetIntel(
                IntelSiteId,
                ExpeditionIntelPaymentMethod.Gold,
                out DomainFailure intelFailure)
            && !intelFailure.IsFailure,
            "actual world-map intel unlock failed");
        DungeonExternalInfluenceSaveData afterIntel = source.Runtime.Capture();
        ExternalInfluenceOperationSaveData intelOperation = afterIntel.operations
            .Single(operation => operation.kind
                == ExternalInfluenceOperationKind.IntelUnlock
                && operation.targetId == IntelSiteId);
        int operationCountBeforeNoOp = afterIntel.operations.Count;
        int moneyBeforeNoOp = source.Money.Balance;
        Require(intelOperation.phase == ExternalInfluenceOperationPhase.Completed
                && intelOperation.costKind == ExternalInfluenceCostKind.Gold
                && intelOperation.costAmount > 0f
                && intelOperation.financeTransactionId.Length > 0
                && source.Money.Balance
                    == intelMoneyBefore - (int)intelOperation.costAmount,
            "intel source, site, gold cost, or finance child was not exact");
        Require(worldMap.Runtime.TryUnlockTargetIntel(
                IntelSiteId,
                ExpeditionIntelPaymentMethod.Gold,
                out DomainFailure repeatFailure)
            && !repeatFailure.IsFailure
            && source.Money.Balance == moneyBeforeNoOp
            && source.Runtime.Capture().operations.Count
                == operationCountBeforeNoOp,
            "already-unlocked intel was not a zero-cost, zero-record no-op");

        source.SeedTrailCharm(1);
        int charmBefore = source.TrailCharmQuantity;
        source.Dispositions.FailNextAcknowledgement = true;
        Require(!source.Runtime.TryUnlockIntel(
                TrailSiteId,
                ExpeditionIntelPaymentMethod.TrailCharm,
                out DomainFailure pendingFailure)
            && pendingFailure.Code == FailureCode.ExternalPaymentRejected,
            "forced post-payment failure was not surfaced");
        DungeonExternalInfluenceSaveData pendingExternal = source.Runtime.Capture();
        DungeonPhysicalItemSaveData pendingPhysical = source.Items.Capture();
        ExternalInfluenceOperationSaveData pendingIntel = pendingExternal.operations
            .Single(operation => operation.targetId == TrailSiteId);
        Require(source.TrailCharmQuantity == charmBefore - 1
                && source.Runtime.IsIntelUnlocked(TrailSiteId)
                && pendingIntel.phase
                    == ExternalInfluenceOperationPhase.ResultPublished
                && pendingIntel.costKind
                    == ExternalInfluenceCostKind.TrailCharm
                && pendingIntel.physicalCommitId.Length > 0
                && pendingIntel.failureReason.Length > 0,
            "post-payment failure lost its physical child or published phase");

        using RuntimeFixture restored = RuntimeFixture.Create();
        restored.Runtime.Start();
        restored.Items.Restore(pendingPhysical);
        restored.Runtime.PublishRestoreCandidate(
            restored.Runtime.BuildRestoreCandidate(pendingExternal));
        DungeonExternalInfluenceSaveData resumed = restored.Runtime.Capture();
        ExternalInfluenceOperationSaveData resumedIntel = resumed.operations
            .Single(operation => operation.targetId == TrailSiteId);
        Require(resumedIntel.phase == ExternalInfluenceOperationPhase.Completed
                && resumedIntel.physicalCommitId
                    == pendingIntel.physicalCommitId
                && resumedIntel.failureReason.Length == 0
                && ExternalInfluenceTrailCharmOutbox.HasEmptyProvenance(resumed)
                && restored.Runtime.IsIntelUnlocked(TrailSiteId),
            "save/restore did not resume only the pending trail-charm phase");

        Require(restored.Runtime.TryArmDreadDefense(out DomainFailure dreadFailure)
                && !dreadFailure.IsFailure
                && restored.Runtime.BeginInvasionDread(boss: false),
            "dread defense could not enter the invasion phase");
        GameObject intruderObject = new("G09_Intruder");
        try
        {
            CharacterActor intruder = intruderObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(intruderObject);
            intruder.SetLifecycleState(CharacterLifecycleState.Active);
            string intruderId = intruder.Identity?.PersistentId;
            Require(!string.IsNullOrWhiteSpace(intruderId),
                "dread fixture intruder has no persistent identity");
            restored.Events.Publish(new InvasionSpawnedEvent(
                CharacterActor.From(intruder),
                default));
            restored.Events.Publish(new InvasionResolvedEvent(true, 0f));
            ExternalInfluenceOperationSaveData dreadOperation = restored.Runtime
                .Capture().operations.Last(operation => operation.kind
                    == ExternalInfluenceOperationKind.DreadDefense);
            Require(dreadOperation.phase
                        == ExternalInfluenceOperationPhase.Completed
                    && dreadOperation.targetId == intruderId
                    && dreadOperation.costKind
                        == ExternalInfluenceCostKind.Dread
                    && dreadOperation.costAmount > 0f
                    && dreadOperation.resultCount == 1,
                "dread cost, intruder, or completed phase was not linked");
        }
        finally
        {
            Object.DestroyImmediate(intruderObject);
        }

        restored.Wildlife.FailNextRaid = true;
        restored.Runtime.AddEcologyPressure(100f, "weather:g09:first");
        restored.Runtime.Tick();
        restored.Runtime.AddEcologyPressure(100f, "weather:g09:second");
        restored.Runtime.Tick();
        DungeonExternalInfluenceSaveData finalSave = restored.Runtime.Capture();
        ExternalInfluenceOperationSaveData failedRaid = finalSave.operations
            .Single(operation => operation.operationId == "ecology:1");
        ExternalInfluenceOperationSaveData completedRaid = finalSave.operations
            .Single(operation => operation.operationId == "ecology:2");
        Require(failedRaid.phase == ExternalInfluenceOperationPhase.Failed
                && failedRaid.sourceId == "weather:g09:first"
                && failedRaid.failureReason.Length > 0
                && completedRaid.phase
                    == ExternalInfluenceOperationPhase.Completed
                && completedRaid.sourceId == "weather:g09:second"
                && completedRaid.targetId == "ecology:2"
                && completedRaid.resultCount == 1
                && restored.Runtime.GetEcologyRaidSnapshot().Phase
                    == EcologyRaidPhase.Resolved,
            "ecology raid source, raid ID, failure, or resolved phase was not exact");

        string finalJson = JsonUtility.ToJson(finalSave);
        using RuntimeFixture roundTrip = RuntimeFixture.Create();
        roundTrip.Runtime.PublishRestoreCandidate(
            roundTrip.Runtime.BuildRestoreCandidate(
                JsonUtility.FromJson<DungeonExternalInfluenceSaveData>(finalJson)));
        string restoredJson = JsonUtility.ToJson(roundTrip.Runtime.Capture());
        Require(string.Equals(finalJson, restoredJson, StringComparison.Ordinal),
            "external-influence operation history did not round-trip exactly");

        return new G09Report
        {
            passed = true,
            operationCount = finalSave.operations.Count,
            valueSourceCount = finalSave.operations.Count(operation =>
                operation.kind is ExternalInfluenceOperationKind.RenownChange
                    or ExternalInfluenceOperationKind.DreadChange
                    or ExternalInfluenceOperationKind.HostileRumorChange
                    or ExternalInfluenceOperationKind.EcologyPressureChange),
            newlyRevealed = reconOperation.newlyRevealed,
            intelSiteId = intelOperation.targetId,
            intelFinanceTransactionId = intelOperation.financeTransactionId,
            alreadyUnlockedAddedOperations = 0,
            postPaymentFailurePhase = pendingIntel.phase.ToString(),
            resumedTrailCharmPhase = resumedIntel.phase.ToString(),
            trailCharmPhysicalCommitId = resumedIntel.physicalCommitId,
            dreadIntruderCount = 1,
            failedRaidId = failedRaid.targetId,
            completedRaidId = completedRaid.targetId,
            exactJsonRoundTrip = true
        };
    }

    private static void WriteReport(G09Report report)
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "G09 verification requires its isolated output directory.");
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class G09Report
    {
        public string rowId = "G09";
        public bool passed;
        public string failure = string.Empty;
        public int operationCount;
        public int valueSourceCount;
        public int newlyRevealed;
        public string intelSiteId = string.Empty;
        public string intelFinanceTransactionId = string.Empty;
        public int alreadyUnlockedAddedOperations;
        public string postPaymentFailurePhase = string.Empty;
        public string resumedTrailCharmPhase = string.Empty;
        public string trailCharmPhysicalCommitId = string.Empty;
        public int dreadIntruderCount;
        public string failedRaidId = string.Empty;
        public string completedRaidId = string.Empty;
        public bool exactJsonRoundTrip;
        public bool fullAuditClosure;
        public bool humanApprovalClaimed;
        public bool trainingEligible;
    }

    private sealed class RuntimeFixture : IDisposable
    {
        private RuntimeFixture(
            GameEventBus events,
            WorldItemStackRuntime items,
            WorldItemRepository repository,
            FailOnceAcknowledgementDisposition dispositions,
            IdempotentMoneyAccount money,
            WildlifeProxy wildlife,
            ExternalInfluenceRuntimeApplicationAdapter runtime)
        {
            Events = events;
            Items = items;
            Repository = repository;
            Dispositions = dispositions;
            Money = money;
            Wildlife = wildlife;
            Runtime = runtime;
        }

        internal GameEventBus Events { get; }
        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal FailOnceAcknowledgementDisposition Dispositions { get; }
        internal IdempotentMoneyAccount Money { get; }
        internal WildlifeProxy Wildlife { get; }
        internal ExternalInfluenceRuntimeApplicationAdapter Runtime { get; }
        internal int TrailCharmQuantity => Items.GetAllStacks()
            .Where(stack => string.Equals(
                stack.ItemId,
                ExternalInfluenceRuntimeApplicationAdapter.TrailCharmItemId,
                StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);

        internal static RuntimeFixture Create()
        {
            WorldItemStackRuntime items =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    out WorldItemRepository repository,
                    out _);
            IGameContentCatalog content = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            PhysicalItemBatchDispositionService inner = new(
                repository,
                new PhysicalItemMassQuery(items.CatalogProvider),
                EditorNullItemMarkerPresenter.Instance, clock: new DungeonStory.Foundation.UnityUiClock());
            FailOnceAcknowledgementDisposition dispositions = new(inner);
            IdempotentMoneyAccount money = new(10000);
            GameEventBus events = new();
            IWildlifeRuntime wildlifeRuntime = WildlifeProxy.Create(
                out WildlifeProxy wildlife);
            ExternalInfluenceRuntimeApplicationAdapter runtime = new(
                events,
                money,
                items,
                dispositions,
                new ResourceItemDefinitionCatalog(content),
                wildlifeRuntime,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<ISurvivalEnvironmentQuery>(),
                new ExternalInfluenceRuntimeApplicationAdapter.Dependencies(
                    new FixedGameClock(),
                    (ICoreSessionRulesProvider)content),
                new ExternalInfluenceAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()));
            return new RuntimeFixture(
                events,
                items,
                repository,
                dispositions,
                money,
                wildlife,
                runtime);
        }

        internal string SeedTrailCharm(int quantity) =>
            WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                ExternalInfluenceRuntimeApplicationAdapter.TrailCharmItemId,
                quantity,
                WorldItemStackState.Loose,
                position: new Vector2Int(2, 2));

        public void Dispose()
        {
            Runtime.Dispose();
            Items.Dispose();
        }
    }

    private sealed class WorldMapFixture : IDisposable
    {
        private readonly GameObject host;
        private readonly EditorOffenseTruthFactFixture truthFacts;

        internal WorldMapFixture(
            IGameEventBus events,
            IExternalInfluenceRuntime externalInfluence)
        {
            host = new GameObject("G09_OffenseWorldMap");
            Runtime = host.AddComponent<OffenseWorldMapRuntime>();
            OffenseCampaignRuntime campaign = new();
            truthFacts = new EditorOffenseTruthFactFixture(
                events,
                "run:g09-offense-truth");
            Runtime.Construct(
                new DungeonStory.Foundation.UnityUiClock(),
                events,
                externalInfluence,
                campaign,
                campaign,
                OffenseEditorTestDependencies.CreateCampaignCatalog(),
                truthFacts.Facts,
                truthFacts.Facts,
                new EditorFixedGameCalendar(7, 0));
        }

        internal OffenseWorldMapRuntime Runtime { get; }

        public void Dispose()
        {
            truthFacts.Dispose();
            Object.DestroyImmediate(host);
        }
    }

    private sealed class FixedGameClock : IGameClock
    {
        public float DeltaTime => 120f;
        public float Time => 120f;
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private sealed class IdempotentMoneyAccount : IIdempotentGameMoneyAccount
    {
        private readonly Dictionary<string, EconomyTransactionRecord> bySource =
            new(StringComparer.Ordinal);
        private int nextSequence = 1;

        internal IdempotentMoneyAccount(int balance) => Balance = balance;

        public int Balance { get; private set; }
        public bool CanSpend(int amount) => amount >= 0 && Balance >= amount;
        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (!CanSpend(amount))
            {
                reason = "insufficient";
                return false;
            }
            Balance -= amount;
            reason = string.Empty;
            return true;
        }

        public bool TrySpendOnce(
            int amount,
            EconomyTransactionContext context,
            out EconomyTransactionRecord receipt,
            out string reason)
        {
            string sourceId = context.sourceId ?? string.Empty;
            if (bySource.TryGetValue(sourceId, out EconomyTransactionRecord existing))
            {
                receipt = existing.Clone();
                reason = existing.amount == -amount
                    && existing.targetId == (context.targetId ?? string.Empty)
                        ? string.Empty
                        : "source-conflict";
                return reason.Length == 0;
            }
            if (!CanSpend(amount))
            {
                receipt = null;
                reason = "insufficient";
                return false;
            }

            int before = Balance;
            Balance -= amount;
            receipt = new EconomyTransactionRecord
            {
                transactionId = $"economy:g09:{nextSequence++:D8}",
                kind = context.kind,
                sourceId = sourceId,
                targetId = context.targetId ?? string.Empty,
                description = context.description ?? string.Empty,
                amount = -amount,
                balanceBefore = before,
                balanceAfter = Balance,
                succeeded = true
            };
            bySource.Add(sourceId, receipt.Clone());
            reason = string.Empty;
            return true;
        }

        public bool TryCreditOnce(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (amount <= 0)
            {
                reason = "invalid";
                return false;
            }
            Add(amount, context);
            reason = string.Empty;
            return true;
        }

        public void Add(int amount) => Balance += Math.Max(0, amount);
        public void Add(int amount, EconomyTransactionContext context) => Add(amount);
        public void SetBalance(int amount, EconomyTransactionContext context) =>
            Balance = Math.Max(0, amount);
    }

    private sealed class FailOnceAcknowledgementDisposition :
        IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;

        internal FailOnceAcknowledgementDisposition(
            IPhysicalItemBatchDispositionService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool FailNextAcknowledgement { get; set; }

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommit(
                inputs, kind, operationId, reasonCode, out receipt, out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommitPending(
                inputs, kind, operationId, reasonCode, out receipt, out failureReason);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "qa-forced-g09-acknowledgement-failure";
                return false;
            }
            return inner.Acknowledge(commitId, out failureReason);
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);
    }

    public class WildlifeProxy : DispatchProxy
    {
        private IReadOnlyList<WildlifeFoodRaidOrderSnapshot> orders =
            Array.Empty<WildlifeFoodRaidOrderSnapshot>();

        internal bool FailNextRaid { get; set; }

        internal static IWildlifeRuntime Create(out WildlifeProxy controller)
        {
            IWildlifeRuntime result =
                DispatchProxy.Create<IWildlifeRuntime, WildlifeProxy>();
            controller = (WildlifeProxy)(object)result;
            return result;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            switch (targetMethod.Name)
            {
                case "get_Wildlife":
                    return Array.Empty<WildlifeActor>();
                case nameof(IWildlifeRuntime.GetReachableFoodRaidTargets):
                    return Array.Empty<WorldItemStackSnapshot>();
                case nameof(IWildlifeRuntime.GetFoodRaidOrders):
                    return orders;
                case nameof(IWildlifeRuntime.TryBeginFoodRaid):
                {
                    string raidId = (string)args[0];
                    if (FailNextRaid)
                    {
                        FailNextRaid = false;
                        orders = Array.Empty<WildlifeFoodRaidOrderSnapshot>();
                        args[2] = orders;
                        args[3] = "qa-wildlife-spawn-rejected";
                        return false;
                    }
                    orders = new[]
                    {
                        new WildlifeFoodRaidOrderSnapshot(
                            raidId,
                            "wildlife:g09:wolf:1",
                            "stack:g09:food:1",
                            WildlifeFoodRaidOrderState.Stolen,
                            1,
                            "stolen"),
                        new WildlifeFoodRaidOrderSnapshot(
                            raidId,
                            "wildlife:g09:wolf:2",
                            "stack:g09:food:2",
                            WildlifeFoodRaidOrderState.Cancelled,
                            0,
                            "cancelled")
                    };
                    args[2] = orders;
                    args[3] = string.Empty;
                    return true;
                }
                default:
                    return targetMethod.ReturnType == typeof(void)
                        ? null
                        : targetMethod.ReturnType.IsValueType
                            ? Activator.CreateInstance(targetMethod.ReturnType)
                            : null;
            }
        }
    }
}
#endif
