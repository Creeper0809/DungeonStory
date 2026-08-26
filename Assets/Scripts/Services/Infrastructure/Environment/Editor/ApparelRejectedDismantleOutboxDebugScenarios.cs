#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused durable Transform tests for quality-rejected apparel. These
/// scenarios use the real Items repository and pending-disposition authority;
/// they never call the apparel runtime's private resolver directly.
/// </summary>
public static class ApparelRejectedDismantleOutboxDebugScenarios
{
    private const string SourceItemId = "tool:hauling-harness";
    private const string RecoveryItemId = "material:mending-scrap";
    private const string DestinationId =
        "production-output:building:qa-apparel-dismantle";
    private static readonly Vector2Int RecoveryPosition = new(7, 3);

    [MenuItem(
        "DungeonStory/Debug/Infrastructure/Run Apparel Rejected Dismantle Focused")]
    public static void RunFocused()
    {
        VerifyNormalTransferRecoveryAcknowledgement();
        VerifyRowFirstPendingReceiptAdoption();
        VerifyPhysicalAheadRecoveryReplay();
        VerifyAcknowledgementCrashAheadAdoption();
        VerifyOneGramMassCreationRejected();
        VerifySourceInstanceDriftRejected();
        VerifyAttemptIdsAreIndependent();
        VerifyZeroRecovery();
        VerifySaveJsonRoundTrip();
        VerifyResolverContainsNoRawDeleteOrSpawn();
        Debug.Log(
            "Apparel rejected dismantle focused PASS: normal=1; "
            + "row-first=1; physical-ahead=1; ack-crash-ahead=1; "
            + "mass-creation-rejected=1; source-drift-rejected=1; "
            + "attempt-isolation=1; zero-recovery=1; json-round-trip=1; "
            + "raw-delete-spawn=0.");
    }

    private static void VerifyNormalTransferRecoveryAcknowledgement()
    {
        using Fixture fixture = Fixture.Create();
        ApparelWorkOrderSaveData order = fixture.CreateOrder("normal", 0, 1);
        string sourceStackId = order.rejectedOutputStackId;

        Require(ApparelRejectedDismantleOutbox.TryCommitOrResume(
                    order,
                    fixture.Items,
                    fixture.Dispositions,
                    out string commitFailure),
            "Normal rejected input Transfer failed: " + commitFailure);
        Require(fixture.Quantity(sourceStackId) == 0
                && order.rejectedOutputConsumed
                && fixture.Dispositions.TryGetPending(
                    order.rejectedDismantleOperationId,
                    out PhysicalItemBatchDispositionReceipt receipt)
                && string.Equals(
                    receipt.CommitId,
                    order.rejectedDismantleCommitId,
                    StringComparison.Ordinal),
            "Normal rejected input was not durably transferred to pending WIP.");

        Require(ApparelRejectedDismantleOutbox.TryEnsureRecovery(
                    order,
                    fixture.Items,
                    RecoveryPosition,
                    DestinationId,
                    out string recoveryFailure),
            "Normal rejected recovery publication failed: " + recoveryFailure);
        Require(order.rejectedRecoveryPublished
                && fixture.CommittedRecoveryQuantity(
                    order.rejectedRecoveryCommitId) == 1,
            "Normal rejected recovery was not published exactly once.");

        Require(ApparelRejectedDismantleOutbox.TryAcknowledge(
                    order,
                    fixture.Dispositions,
                    out string acknowledgeFailure),
            "Normal rejected input acknowledgement failed: "
            + acknowledgeFailure);
        Require(order.rejectedDismantleAcknowledged
                && !fixture.Dispositions.TryGetPending(
                    order.rejectedDismantleOperationId,
                    out _)
                && fixture.CommittedRecoveryQuantity(
                    order.rejectedRecoveryCommitId) == 1,
            "Normal acknowledgement retained WIP or duplicated recovery.");
    }

    private static void VerifyRowFirstPendingReceiptAdoption()
    {
        using Fixture fixture = Fixture.Create();
        ApparelWorkOrderSaveData order = fixture.CreateOrder("row-first", 0, 1);
        string operationId = ApparelRejectedDismantleOutbox.FormatOperationId(
            order.orderId,
            order.qualityAttemptIndex);
        Require(fixture.Dispositions.TryCommitPending(
                    new[]
                    {
                        new PhysicalItemTransformInput(
                            order.rejectedOutputStackId,
                            1)
                    },
                    PhysicalItemDispositionKind.Transfer,
                    operationId,
                    ApparelRejectedDismantleOutbox.ReasonCode,
                    out PhysicalItemBatchDispositionReceipt row,
                    out string rowFailure),
            "Row-first pending receipt could not be committed: " + rowFailure);
        Require(string.IsNullOrEmpty(order.rejectedDismantleOperationId),
            "Row-first fixture unexpectedly pre-populated its owner row.");

        Require(ApparelRejectedDismantleOutbox.TryCommitOrResume(
                    order,
                    fixture.Items,
                    fixture.Dispositions,
                    out string adoptFailure),
            "Row-first pending receipt was not adopted: " + adoptFailure);
        Require(string.Equals(
                    order.rejectedDismantleOperationId,
                    row.OperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    order.rejectedDismantleCommitId,
                    row.CommitId,
                    StringComparison.Ordinal)
                && string.Equals(
                    order.rejectedDismantleRequestFingerprint,
                    row.RequestFingerprint,
                    StringComparison.Ordinal)
                && order.rejectedDismantleInputMassGrams
                    == row.InputMassGrams
                && order.rejectedOutputConsumed,
            "Row-first adoption did not copy the exact Items-owned receipt.");
    }

    private static void VerifyPhysicalAheadRecoveryReplay()
    {
        using Fixture fixture = Fixture.Create();
        ApparelWorkOrderSaveData order = fixture.CreateOrder(
            "physical-ahead",
            0,
            1);
        CommitInput(fixture, order);
        string operationId = ApparelRejectedDismantleOutbox
            .FormatRecoveryOperationId(order.orderId, order.qualityAttemptIndex);
        string commitId = ApparelRejectedDismantleOutbox
            .FormatRecoveryCommitId(operationId, RecoveryItemId, 1);
        Require(fixture.Items.SpawnItemAtWithComponents(
                    RecoveryItemId,
                    1,
                    RecoveryPosition,
                    WorldItemStackState.FacilityOutputBuffer,
                    DestinationId,
                    new[]
                    {
                        ProductionOutputCommitComponentCodec.Create(commitId)
                    },
                    out int spawned)
                && spawned == 1,
            "Physical-ahead recovery fixture could not publish its output.");
        Require(!order.rejectedRecoveryPublished
                && fixture.CommittedRecoveryQuantity(commitId) == 1,
            "Physical-ahead fixture did not stop before owner publication.");

        Require(ApparelRejectedDismantleOutbox.TryEnsureRecovery(
                    order,
                    fixture.Items,
                    RecoveryPosition,
                    DestinationId,
                    out string replayFailure),
            "Physical-ahead recovery was not adopted: " + replayFailure);
        Require(order.rejectedRecoveryPublished
                && string.Equals(
                    order.rejectedRecoveryCommitId,
                    commitId,
                    StringComparison.Ordinal)
                && fixture.CommittedRecoveryQuantity(commitId) == 1,
            "Physical-ahead recovery replay duplicated or changed output.");
    }

    private static void VerifyAcknowledgementCrashAheadAdoption()
    {
        using Fixture fixture = Fixture.Create();
        ApparelWorkOrderSaveData order = fixture.CreateOrder(
            "ack-crash-ahead",
            0,
            1);
        CommitInput(fixture, order);
        EnsureRecovery(fixture, order);
        Require(fixture.Dispositions.Acknowledge(
                    order.rejectedDismantleCommitId,
                    out string directFailure),
            "Acknowledgement crash-ahead fixture could not remove pending WIP: "
            + directFailure);
        Require(!order.rejectedDismantleAcknowledged
                && !fixture.Dispositions.TryGetPending(
                    order.rejectedDismantleOperationId,
                    out _),
            "Acknowledgement crash-ahead fixture did not stop between authorities.");

        Require(ApparelRejectedDismantleOutbox.TryCommitOrResume(
                    order,
                    fixture.Items,
                    fixture.Dispositions,
                    out string adoptFailure),
            "Missing pending receipt was not adopted from exact recovery: "
            + adoptFailure);
        Require(!order.rejectedDismantleAcknowledged,
            "Crash-ahead join inferred owner acknowledgement before exact output validation.");
        Require(ApparelRejectedDismantleOutbox.TryEnsureRecovery(
                    order,
                    fixture.Items,
                    RecoveryPosition,
                    DestinationId,
                    out string recoveryFailure),
            "Acknowledgement crash-ahead recovery validation failed: "
            + recoveryFailure);
        Require(ApparelRejectedDismantleOutbox.TryAcknowledge(
                    order,
                    fixture.Dispositions,
                    out string acknowledgementFailure)
                && order.rejectedDismantleAcknowledged
                && fixture.CommittedRecoveryQuantity(
                    order.rejectedRecoveryCommitId) == 1,
            "Acknowledgement crash-ahead recovery changed physical output: "
            + acknowledgementFailure);
    }

    private static void VerifyOneGramMassCreationRejected()
    {
        using Fixture fixture = Fixture.Create();
        ApparelWorkOrderSaveData order = fixture.CreateOrder(
            "mass-plus-one",
            0,
            1);
        CommitInput(fixture, order);
        long recoveryMass = fixture.Items.MassQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)RecoveryItemId).Value;
        Require(recoveryMass > 1L,
            "Mass creation fixture requires a recovery item heavier than 1g.");
        order.rejectedDismantleInputMassGrams = recoveryMass - 1L;

        Require(!ApparelRejectedDismantleOutbox.TryEnsureRecovery(
                    order,
                    fixture.Items,
                    RecoveryPosition,
                    DestinationId,
                    out string failure)
                && string.Equals(
                    failure,
                    "apparel-rejected-recovery-mass-exceeds-input",
                    StringComparison.Ordinal)
                && fixture.CommittedRecoveryQuantityByPrefix() == 0,
            "A recovery output creating exactly 1g was not rejected fail-loud.");
    }

    private static void VerifySourceInstanceDriftRejected()
    {
        using Fixture fixture = Fixture.Create();
        ApparelWorkOrderSaveData order = fixture.CreateOrder(
            "source-drift",
            0,
            1);
        string sourceStackId = order.rejectedOutputStackId;
        order.rejectedOutputInstanceId += ":drift";

        Require(!ApparelRejectedDismantleOutbox.TryCommitOrResume(
                    order,
                    fixture.Items,
                    fixture.Dispositions,
                    out string failure)
                && string.Equals(
                    failure,
                    "apparel-rejected-dismantle-source-missing",
                    StringComparison.Ordinal)
                && fixture.Quantity(sourceStackId) == 1
                && !fixture.Dispositions.TryGetPending(
                    ApparelRejectedDismantleOutbox.FormatOperationId(
                        order.orderId,
                        order.qualityAttemptIndex),
                    out _),
            "Source instance drift removed or adopted a non-exact source.");
    }

    private static void VerifyAttemptIdsAreIndependent()
    {
        using Fixture fixture = Fixture.Create();
        const string sharedOrderId = "apparel-order:qa:dismantle:attempts";
        ApparelWorkOrderSaveData first = fixture.CreateOrder(
            "attempt-zero",
            0,
            1,
            sharedOrderId);
        ApparelWorkOrderSaveData second = fixture.CreateOrder(
            "attempt-one",
            1,
            1,
            sharedOrderId);
        CommitInput(fixture, first);
        CommitInput(fixture, second);

        Require(!string.Equals(
                    first.rejectedDismantleOperationId,
                    second.rejectedDismantleOperationId,
                    StringComparison.Ordinal)
                && !string.Equals(
                    first.rejectedDismantleCommitId,
                    second.rejectedDismantleCommitId,
                    StringComparison.Ordinal)
                && fixture.Dispositions.TryGetPending(
                    first.rejectedDismantleOperationId,
                    out _)
                && fixture.Dispositions.TryGetPending(
                    second.rejectedDismantleOperationId,
                    out _),
            "Two quality attempts aliased the same durable operation or commit ID.");
    }

    private static void VerifyZeroRecovery()
    {
        using Fixture fixture = Fixture.Create();
        ApparelWorkOrderSaveData order = fixture.CreateOrder(
            "zero-recovery",
            0,
            0);
        CommitInput(fixture, order);
        Require(ApparelRejectedDismantleOutbox.TryEnsureRecovery(
                    order,
                    fixture.Items,
                    RecoveryPosition,
                    DestinationId,
                    out string recoveryFailure),
            "Zero recovery was not published: " + recoveryFailure);
        Require(order.rejectedRecoveryPublished
                && order.rejectedMaterialSpawned == 0
                && string.IsNullOrEmpty(order.rejectedRecoveryCommitId)
                && order.rejectedRecoveryOutputMassGrams == 0L
                && fixture.CommittedRecoveryQuantityByPrefix() == 0,
            "Zero recovery created a physical output or non-zero mass receipt.");
        Require(ApparelRejectedDismantleOutbox.TryAcknowledge(
                    order,
                    fixture.Dispositions,
                    out string acknowledgeFailure)
                && order.rejectedDismantleAcknowledged,
            "Zero recovery did not acknowledge its input: "
            + acknowledgeFailure);
    }

    private static void VerifySaveJsonRoundTrip()
    {
        using Fixture fixture = Fixture.Create();
        ApparelWorkOrderSaveData order = fixture.CreateOrder(
            "json-round-trip",
            3,
            1);
        CommitInput(fixture, order);
        EnsureRecovery(fixture, order);
        string json = JsonUtility.ToJson(order);
        ApparelWorkOrderSaveData restored =
            JsonUtility.FromJson<ApparelWorkOrderSaveData>(json);
        string validationFailure = string.Empty;

        Require(restored != null
                && string.Equals(
                    restored.rejectedOutputStackId,
                    order.rejectedOutputStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    restored.rejectedOutputInstanceId,
                    order.rejectedOutputInstanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    restored.rejectedDismantleOperationId,
                    order.rejectedDismantleOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    restored.rejectedDismantleCommitId,
                    order.rejectedDismantleCommitId,
                    StringComparison.Ordinal)
                && string.Equals(
                    restored.rejectedDismantleRequestFingerprint,
                    order.rejectedDismantleRequestFingerprint,
                    StringComparison.Ordinal)
                && restored.rejectedDismantleInputMassGrams
                    == order.rejectedDismantleInputMassGrams
                && string.Equals(
                    restored.rejectedRecoveryOperationId,
                    order.rejectedRecoveryOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    restored.rejectedRecoveryCommitId,
                    order.rejectedRecoveryCommitId,
                    StringComparison.Ordinal)
                && restored.rejectedRecoveryOutputMassGrams
                    == order.rejectedRecoveryOutputMassGrams
                && restored.rejectedRecoveryPublished
                && !restored.rejectedDismantleAcknowledged
                && ApparelRejectedDismantleOutbox.ValidateOwnerShape(
                    restored,
                    out validationFailure),
            "Apparel rejected-dismantle save DTO lost durable fields: "
            + validationFailure);
    }

    private static void VerifyResolverContainsNoRawDeleteOrSpawn()
    {
        string path = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "Scripts/Services/Infrastructure/Environment/"
            + "ApparelWorkOrderRuntime.cs"));
        string source = File.ReadAllText(path);
        const string methodName =
            "private bool ResolveRejectedApparelDismantle(";
        const string nextMethod = "private bool PrepareNextCraftAttempt(";
        int start = source.IndexOf(methodName, StringComparison.Ordinal);
        int end = source.IndexOf(nextMethod, start, StringComparison.Ordinal);
        Require(start >= 0 && end > start,
            "Could not isolate ResolveRejectedApparelDismantle for static audit.");
        string body = source.Substring(start, end - start);
        Require(!Regex.IsMatch(body, @"\bDeleteStack\s*\(")
                && !Regex.IsMatch(body, @"\bSpawnItemAt\s*\("),
            "ResolveRejectedApparelDismantle retained raw DeleteStack/SpawnItemAt mutation.");
    }

    private static void CommitInput(
        Fixture fixture,
        ApparelWorkOrderSaveData order)
    {
        Require(ApparelRejectedDismantleOutbox.TryCommitOrResume(
                    order,
                    fixture.Items,
                    fixture.Dispositions,
                    out string failure),
            "Rejected apparel input Transfer failed: " + failure);
    }

    private static void EnsureRecovery(
        Fixture fixture,
        ApparelWorkOrderSaveData order)
    {
        Require(ApparelRejectedDismantleOutbox.TryEnsureRecovery(
                    order,
                    fixture.Items,
                    RecoveryPosition,
                    DestinationId,
                    out string failure),
            "Rejected apparel recovery publication failed: " + failure);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private Fixture(
            WorldItemStackRuntime items,
            WorldItemRepository repository,
            IPhysicalItemBatchDispositionService dispositions)
        {
            Items = items;
            Repository = repository;
            Dispositions = dispositions;
        }

        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal IPhysicalItemBatchDispositionService Dispositions { get; }

        internal static Fixture Create()
        {
            WorldItemStackRuntime items =
                PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                    EditorItemCatalogFactory.Create(),
                    out WorldItemRepository repository,
                    out _,
                    out _,
                    out _,
                    out _,
                    out IPhysicalItemBatchDispositionService dispositions);
            return new Fixture(items, repository, dispositions);
        }

        internal ApparelWorkOrderSaveData CreateOrder(
            string suffix,
            int attemptIndex,
            int recoveryQuantity,
            string explicitOrderId = null)
        {
            string instanceId = "apparel-instance:qa:dismantle:"
                + suffix;
            string stackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                SourceItemId,
                1,
                WorldItemStackState.FacilityOutputBuffer,
                destinationId: DestinationId,
                position: RecoveryPosition,
                itemInstanceId: instanceId);
            Require(!string.IsNullOrEmpty(stackId),
                "Rejected apparel source could not be created: " + suffix);
            return new ApparelWorkOrderSaveData
            {
                orderId = explicitOrderId
                    ?? "apparel-order:qa:dismantle:" + suffix,
                kind = ApparelWorkOrderKind.Craft,
                state = ApparelWorkOrderState.InProgress,
                qualityAttemptIndex = attemptIndex,
                dismantlingRejectedOutput = true,
                rejectedOutputStackId = stackId,
                rejectedOutputInstanceId = instanceId,
                rejectedMaterialAmount = recoveryQuantity,
                rejectedRecoveryItemId = RecoveryItemId
            };
        }

        internal int Quantity(string stackId) =>
            Repository.GetEditorTestQuantity(stackId);

        internal int CommittedRecoveryQuantity(string commitId) =>
            Items.GetAllStacks()
                .Where(stack => stack != null
                    && ProductionOutputCommitComponentCodec.Matches(
                        stack.Components,
                        commitId))
                .Sum(stack => stack.Quantity);

        internal int CommittedRecoveryQuantityByPrefix() =>
            Items.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        RecoveryItemId,
                        StringComparison.Ordinal)
                    && stack.State
                        == WorldItemStackState.FacilityOutputBuffer
                    && string.Equals(
                        stack.DestinationId,
                        DestinationId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);

        public void Dispose() => Items.Dispose();
    }
}
#endif
