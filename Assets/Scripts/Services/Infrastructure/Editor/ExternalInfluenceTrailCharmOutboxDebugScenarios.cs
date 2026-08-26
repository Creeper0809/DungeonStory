#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.CoreSession;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class ExternalInfluenceTrailCharmOutboxDebugScenarios
{
    private const string FirstSiteId = "expedition-site:qa-trail-charm:first";
    private const string RestoreSiteId = "expedition-site:qa-trail-charm:restore";

    [MenuItem("DungeonStory/QA/V27/Run External Influence Trail Charm Outbox")]
    public static void RunFromMenu()
    {
        string details = RunAll();
        Debug.Log("External influence trail-charm outbox PASS. " + details);
    }

    public static string RunAll()
    {
        VerifyRetryDoesNotConsumeTwice();
        VerifyItemCommittedRestoreFinalizesExactlyOnce();
        VerifyPublishedRestoreWithoutReceiptClearsSafely();
        VerifyTamperedEnvelopeFailsBeforeMutation();
        return "retry=exact; restore=item-committed; published-missing-receipt=safe; tamper=no-mutation";
    }

    private static void VerifyRetryDoesNotConsumeTwice()
    {
        using Fixture fixture = Fixture.Create();
        fixture.SeedTrailCharm(2);
        int before = fixture.TrailCharmQuantity;
        fixture.Dispositions.FailNextAcknowledgement = true;

        Require(!fixture.Runtime.TryUnlockIntel(
                FirstSiteId,
                ExpeditionIntelPaymentMethod.TrailCharm,
                out DomainFailure pendingFailure)
            && pendingFailure.Code == FailureCode.ExternalPaymentRejected,
            "forced acknowledgement failure did not leave a typed retry");
        DungeonExternalInfluenceSaveData pending = fixture.Runtime.Capture();
        Require(fixture.Runtime.IsIntelUnlocked(FirstSiteId)
                && fixture.TrailCharmQuantity == before - 1
                && pending.trailCharmCommitPhase
                    == ExternalInfluenceTrailCharmCommitPhase.IntelPublished
                && pending.pendingTrailCharmQuantity == 1
                && pending.pendingTrailCharmMassGrams > 0L,
            "trail charm debit, published intel, or pending provenance was not exact");

        Require(fixture.Runtime.TryUnlockIntel(
                FirstSiteId,
                ExpeditionIntelPaymentMethod.TrailCharm,
                out DomainFailure retryFailure)
            && !retryFailure.IsFailure
            && fixture.TrailCharmQuantity == before - 1
            && ExternalInfluenceTrailCharmOutbox.HasEmptyProvenance(
                fixture.Runtime.Capture()),
            "retry consumed twice or did not clear the receipt");
        Require(fixture.Runtime.TryUnlockIntel(
                FirstSiteId,
                ExpeditionIntelPaymentMethod.TrailCharm,
                out DomainFailure repeatFailure)
            && !repeatFailure.IsFailure
            && fixture.TrailCharmQuantity == before - 1,
            "already-unlocked site consumed a second charm");
    }

    private static void VerifyItemCommittedRestoreFinalizesExactlyOnce()
    {
        using Fixture source = Fixture.Create();
        string stackId = source.SeedTrailCharm(1);
        string operationId =
            ExternalInfluenceTrailCharmOutbox.FormatOperationId(RestoreSiteId);
        Require(source.Dispositions.TryCommitPending(
                new[] { new PhysicalItemTransformInput(stackId, 1) },
                PhysicalItemDispositionKind.Sink,
                operationId,
                ExternalInfluenceTrailCharmOutbox.ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure),
            "restore fixture could not commit the physical receipt: "
            + commitFailure);
        DungeonExternalInfluenceSaveData externalSave = source.Runtime.Capture();
        ExternalInfluenceTrailCharmOutbox.RecordPending(
            externalSave,
            RestoreSiteId,
            ExternalInfluenceRuntimeApplicationAdapter.TrailCharmItemId,
            receipt);
        DungeonPhysicalItemSaveData physicalSave = source.Items.Capture();

        using Fixture restored = Fixture.Create();
        restored.Items.Restore(physicalSave);
        restored.Runtime.PublishRestoreCandidate(
            restored.Runtime.BuildRestoreCandidate(externalSave));
        Require(restored.Runtime.IsIntelUnlocked(RestoreSiteId)
                && restored.TrailCharmQuantity == 0
                && ExternalInfluenceTrailCharmOutbox.HasEmptyProvenance(
                    restored.Runtime.Capture())
                && !restored.Dispositions.TryGetPending(operationId, out _),
            "item-committed restore did not publish and acknowledge exactly once");
    }

    private static void VerifyPublishedRestoreWithoutReceiptClearsSafely()
    {
        using Fixture source = Fixture.Create();
        string stackId = source.SeedTrailCharm(1);
        string operationId =
            ExternalInfluenceTrailCharmOutbox.FormatOperationId(RestoreSiteId);
        Require(source.Dispositions.TryCommitPending(
                new[] { new PhysicalItemTransformInput(stackId, 1) },
                PhysicalItemDispositionKind.Sink,
                operationId,
                ExternalInfluenceTrailCharmOutbox.ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure),
            "published restore fixture could not commit: " + commitFailure);
        DungeonExternalInfluenceSaveData save = source.Runtime.Capture();
        ExternalInfluenceTrailCharmOutbox.RecordPending(
            save,
            RestoreSiteId,
            ExternalInfluenceRuntimeApplicationAdapter.TrailCharmItemId,
            receipt);
        save.trailCharmCommitPhase =
            ExternalInfluenceTrailCharmCommitPhase.IntelPublished;
        save.intelUnlockedSiteIds.Add(RestoreSiteId);
        Require(source.Dispositions.Acknowledge(
                receipt.CommitId,
                out string acknowledgeFailure),
            "published restore fixture could not acknowledge: "
            + acknowledgeFailure);
        DungeonPhysicalItemSaveData physicalSave = source.Items.Capture();

        using Fixture restored = Fixture.Create();
        restored.Items.Restore(physicalSave);
        restored.Runtime.PublishRestoreCandidate(
            restored.Runtime.BuildRestoreCandidate(save));
        Require(restored.Runtime.IsIntelUnlocked(RestoreSiteId)
                && restored.TrailCharmQuantity == 0
                && ExternalInfluenceTrailCharmOutbox.HasEmptyProvenance(
                    restored.Runtime.Capture()),
            "crash-after-ack restore was not idempotent");
    }

    private static void VerifyTamperedEnvelopeFailsBeforeMutation()
    {
        using Fixture fixture = Fixture.Create();
        DungeonExternalInfluenceSaveData tampered = fixture.Runtime.Capture();
        tampered.trailCharmCommitPhase =
            ExternalInfluenceTrailCharmCommitPhase.ItemCommitted;
        tampered.pendingTrailCharmSiteId = RestoreSiteId;
        tampered.pendingTrailCharmOperationId =
            ExternalInfluenceTrailCharmOutbox.FormatOperationId(RestoreSiteId);
        tampered.pendingTrailCharmReasonCode =
            ExternalInfluenceTrailCharmOutbox.ReasonCode;
        tampered.pendingTrailCharmCommitId = "tampered";
        tampered.pendingTrailCharmSourceStackIds.Add("stack:tampered");
        tampered.pendingTrailCharmQuantity = 1;
        tampered.pendingTrailCharmMassGrams = 100L;
        tampered.pendingTrailCharmItemId =
            ExternalInfluenceRuntimeApplicationAdapter.TrailCharmItemId;

        RequireThrows<InvalidOperationException>(
            () => fixture.Runtime.BuildRestoreCandidate(tampered),
            "tampered trail-charm envelope was accepted");
        Require(!fixture.Runtime.IsIntelUnlocked(RestoreSiteId)
                && ExternalInfluenceTrailCharmOutbox.HasEmptyProvenance(
                    fixture.Runtime.Capture()),
            "tampered restore mutated the live external-influence aggregate");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireThrows<TException>(Action action, string message)
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

    private sealed class Fixture : IDisposable
    {
        private Fixture(
            WorldItemStackRuntime items,
            WorldItemRepository repository,
            FailOnceAcknowledgementDisposition dispositions,
            ExternalInfluenceRuntimeApplicationAdapter runtime)
        {
            Items = items;
            Repository = repository;
            Dispositions = dispositions;
            Runtime = runtime;
        }

        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal FailOnceAcknowledgementDisposition Dispositions { get; }
        internal ExternalInfluenceRuntimeApplicationAdapter Runtime { get; }
        internal int TrailCharmQuantity => Items.GetAllStacks()
            .Where(stack => string.Equals(
                stack.ItemId,
                ExternalInfluenceRuntimeApplicationAdapter.TrailCharmItemId,
                StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);

        internal static Fixture Create()
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
                EditorNullItemMarkerPresenter.Instance);
            FailOnceAcknowledgementDisposition dispositions = new(inner);
            ExternalInfluenceRuntimeApplicationAdapter runtime = new(
                new GameEventBus(),
                new MoneyAccount(),
                items,
                dispositions,
                new ResourceItemDefinitionCatalog(content),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IWildlifeRuntime>(),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<ISurvivalEnvironmentQuery>(),
                new ExternalInfluenceRuntimeApplicationAdapter.Dependencies(
                    new FixedGameClock(),
                    (ICoreSessionRulesProvider)content),
                new ExternalInfluenceAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()));
            return new Fixture(items, repository, dispositions, runtime);
        }

        internal string SeedTrailCharm(int quantity) =>
            WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                ExternalInfluenceRuntimeApplicationAdapter.TrailCharmItemId,
                quantity,
                WorldItemStackState.Loose,
                position: new Vector2Int(2, 2));

        public void Dispose() => Items.Dispose();
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
                failureReason = "qa-forced-trail-charm-acknowledgement-failure";
                return false;
            }
            return inner.Acknowledge(commitId, out failureReason);
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);
    }

    private sealed class FixedGameClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class MoneyAccount : IGameMoneyAccount
    {
        public int Balance { get; private set; } = 1000;
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
        public void Add(int amount) => Balance += amount;
        public void Add(int amount, EconomyTransactionContext context) => Add(amount);
        public void SetBalance(int amount, EconomyTransactionContext context) =>
            Balance = amount;
    }
}
#endif
