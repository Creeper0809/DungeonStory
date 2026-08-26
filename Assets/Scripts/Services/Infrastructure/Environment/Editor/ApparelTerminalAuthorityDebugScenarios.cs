#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused same-aggregate authority and V9 persistence checks for apparel
/// terminal effects. The fixture publishes only through production restore and
/// terminal ports; it does not mutate world-item or lease authorities.
/// </summary>
public static class ApparelTerminalAuthorityDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Infrastructure/Run Apparel Terminal Authority Focused")]
    public static void RunFocused()
    {
        VerifyEffectRowFirst();
        VerifyExactSourceRemovalAndReceipt();
        VerifySourceDriftRejected();
        VerifyV9TerminalStateRoundTrip();
        VerifyTerminalJoinTamperRejected();
        VerifyOrderOnlyRestoreCannotOverwriteTerminalAuthority();
        Debug.Log(
            "Apparel terminal authority focused PASS: effect-row-first=1; "
            + "exact-source-removal=1; source-drift-rejected=1; "
            + "v7-round-trip=1; invalid-joins-rejected=2; "
            + "order-only-overwrite-rejected=1.");
    }

    private static void VerifyEffectRowFirst()
    {
        ApparelWorkOrderRuntime runtime = CreateRuntime();
        ApparelWorkOrderSaveData source = SeedOrder(runtime, "row-first");
        TerminalFixture terminal = CommitEffect(runtime, source, "row-first");

        ApparelWorkOrderSaveData[] live = runtime.CaptureOrders();
        ApparelWorkOrderTerminalStateSaveData[] states =
            runtime.CaptureTerminalStates();
        Require(live.Length == 1
                && SameOrder(live[0], source)
                && states.Length == 1
                && SameOrder(states[0].sourceOrder, source)
                && states[0].sourceTerminalReceipt == null
                && ProductionApparelOrderTerminalDrainCanonical
                    .EffectReceiptEquals(
                        states[0].terminalEffectReceipt,
                        terminal.EffectReceipt),
            "Apparel terminal effect was not published before source removal.");
        Require(runtime.TryCaptureTerminalEffectReceipt(
                    terminal.EffectReceipt.commitId,
                    out ProductionApparelOrderTerminalEffectReceipt captured)
                && ProductionApparelOrderTerminalDrainCanonical
                    .EffectReceiptEquals(captured, terminal.EffectReceipt),
            "Apparel effect receipt query did not return the exact durable row.");
    }

    private static void VerifyExactSourceRemovalAndReceipt()
    {
        ApparelWorkOrderRuntime runtime = CreateRuntime();
        ApparelWorkOrderSaveData source = SeedOrder(runtime, "exact-removal");
        TerminalFixture terminal = CommitEffect(runtime, source, "exact-removal");
        ProductionApparelOrderSourceTerminalReceipt expected =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceTerminalReceipt(
                    terminal.StepOperationId,
                    source,
                    terminal.SourceFingerprint,
                    terminal.EffectReceipt.receiptFingerprint);

        ProductionApparelOrderSourceTerminalApplyResult applied =
            runtime.TryCommitSourceTerminal(expected);
        Require(applied.Status ==
                    ProductionApparelOrderTerminalDrainStatus.Applied
                && ProductionApparelOrderTerminalDrainCanonical
                    .SourceReceiptEquals(applied.Receipt, expected)
                && runtime.CaptureOrders().Length == 0,
            "Exact apparel source removal did not atomically publish its receipt.");
        Require(runtime.TryCaptureSourceTerminalReceipt(
                    expected.commitId,
                    out ProductionApparelOrderSourceTerminalReceipt captured)
                && ProductionApparelOrderTerminalDrainCanonical
                    .SourceReceiptEquals(captured, expected)
                && runtime.CaptureTerminalStates().Single()
                    .sourceTerminalReceipt != null,
            "Apparel source-terminal receipt was not durably queryable.");

        ProductionApparelOrderSourceTerminalApplyResult replay =
            runtime.TryCommitSourceTerminal(expected);
        Require(replay.Status ==
                    ProductionApparelOrderTerminalDrainStatus.Replay
                && runtime.CaptureOrders().Length == 0,
            "Exact apparel source-terminal replay was not idempotent.");
    }

    private static void VerifySourceDriftRejected()
    {
        ApparelWorkOrderRuntime runtime = CreateRuntime();
        ApparelWorkOrderSaveData source = SeedOrder(runtime, "source-drift");
        TerminalFixture terminal = CommitEffect(runtime, source, "source-drift");
        ProductionApparelOrderSourceTerminalReceipt expected =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceTerminalReceipt(
                    terminal.StepOperationId,
                    source,
                    terminal.SourceFingerprint,
                    terminal.EffectReceipt.receiptFingerprint);

        runtime.Orders.Single().completedWork += 1f;
        ProductionApparelOrderSourceTerminalApplyResult rejected =
            runtime.TryCommitSourceTerminal(expected);
        Require(rejected.Status ==
                    ProductionApparelOrderTerminalDrainStatus.Conflict
                && rejected.FailureReason.Contains(
                    "live-source-conflict",
                    StringComparison.Ordinal)
                && runtime.CaptureOrders().Length == 1
                && runtime.CaptureTerminalStates().Single()
                    .sourceTerminalReceipt == null,
            "A drifted apparel source was removed or partially terminalized.");
    }

    private static void VerifyV9TerminalStateRoundTrip()
    {
        ApparelWorkOrderRuntime runtime = CreateRuntime();
        ApparelWorkOrderSaveData effectSource = SeedOrder(runtime, "v7-effect");
        CommitEffect(runtime, effectSource, "v7-effect");
        ApparelWorkOrderSaveData removedSource = SeedOrder(
            runtime,
            "v7-removed");
        TerminalFixture removed = CommitEffect(
            runtime,
            removedSource,
            "v7-removed");
        ProductionApparelOrderSourceTerminalReceipt sourceReceipt =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceTerminalReceipt(
                    removed.StepOperationId,
                    removedSource,
                    removed.SourceFingerprint,
                    removed.EffectReceipt.receiptFingerprint);
        Require(runtime.TryCommitSourceTerminal(sourceReceipt).Status ==
                ProductionApparelOrderTerminalDrainStatus.Applied,
            "V9 fixture could not reach the source-terminal phase.");

        DungeonCharacterEnvironmentSaveData captured = new()
        {
            version = DungeonCharacterEnvironmentSaveData.CurrentVersion,
            exposures = Array.Empty<CharacterEnvironmentExposure>(),
            equippedWorkwear = Array.Empty<EnvironmentalWorkwearSaveData>(),
            equippedApparel = Array.Empty<EquippedApparelSaveData>(),
            apparelWorkOrders = runtime.CaptureOrders(),
            apparelWorkOrderTerminalStates = runtime.CaptureTerminalStates()
        };
        DungeonGameRestoreReport captureReport = new();
        CharacterEnvironmentSaveValidation.Validate(captured, captureReport);
        Require(captureReport.Success,
            "V9 apparel terminal payload failed section validation: "
            + string.Join(" | ", captureReport.Errors));

        string json = JsonUtility.ToJson(captured);
        DungeonCharacterEnvironmentSaveData roundTrip =
            JsonUtility.FromJson<DungeonCharacterEnvironmentSaveData>(json);
        Require(roundTrip != null
                && roundTrip.version ==
                    DungeonCharacterEnvironmentSaveData.CurrentVersion
                && roundTrip.apparelWorkOrders != null
                && roundTrip.apparelWorkOrderTerminalStates != null,
            "V9 apparel terminal payload lost a required collection in JSON.");
        DungeonGameRestoreReport roundTripReport = new();
        CharacterEnvironmentSaveValidation.Validate(roundTrip, roundTripReport);
        Require(roundTripReport.Success,
            "Round-tripped V9 apparel terminal payload failed validation: "
            + string.Join(" | ", roundTripReport.Errors));

        ApparelWorkOrderRuntime restored = CreateRuntime();
        restored.PublishRestoreState(restored.PrepareRestoreState(
            roundTrip.apparelWorkOrders,
            roundTrip.apparelWorkOrderTerminalStates));
        Require(SameOrders(runtime.CaptureOrders(), restored.CaptureOrders())
                && SameTerminalStates(
                    runtime.CaptureTerminalStates(),
                    restored.CaptureTerminalStates()),
            "V9 apparel terminal authority changed during save/restore round-trip.");
    }

    private static void VerifyTerminalJoinTamperRejected()
    {
        ApparelWorkOrderRuntime effectRuntime = CreateRuntime();
        ApparelWorkOrderSaveData effectSource = SeedOrder(
            effectRuntime,
            "join-effect");
        CommitEffect(effectRuntime, effectSource, "join-effect");
        ApparelWorkOrderTerminalStateSaveData[] effectRows =
            effectRuntime.CaptureTerminalStates();
        ExpectInvalidOperation(
            () => CreateRuntime().PrepareRestoreState(
                Array.Empty<ApparelWorkOrderSaveData>(),
                effectRows),
            "An effect receipt without its exact live source joined restore.");

        ApparelWorkOrderRuntime sourceRuntime = CreateRuntime();
        ApparelWorkOrderSaveData source = SeedOrder(
            sourceRuntime,
            "join-source");
        TerminalFixture terminal = CommitEffect(
            sourceRuntime,
            source,
            "join-source");
        ProductionApparelOrderSourceTerminalReceipt sourceReceipt =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceTerminalReceipt(
                    terminal.StepOperationId,
                    source,
                    terminal.SourceFingerprint,
                    terminal.EffectReceipt.receiptFingerprint);
        Require(sourceRuntime.TryCommitSourceTerminal(sourceReceipt).Status ==
                ProductionApparelOrderTerminalDrainStatus.Applied,
            "Source-terminal join fixture could not remove its source.");
        ApparelWorkOrderTerminalStateSaveData[] sourceRows =
            sourceRuntime.CaptureTerminalStates();
        ExpectInvalidOperation(
            () => CreateRuntime().PrepareRestoreState(
                new[] { sourceRows.Single().sourceOrder },
                sourceRows),
            "A source-terminal receipt joined restore while its live source remained.");
    }

    private static void VerifyOrderOnlyRestoreCannotOverwriteTerminalAuthority()
    {
        ApparelWorkOrderRuntime runtime = CreateRuntime();
        ApparelWorkOrderSaveData source = SeedOrder(
            runtime,
            "order-only-overwrite");
        CommitEffect(runtime, source, "order-only-overwrite");
        string ordersBefore = CanonicalOrders(runtime.CaptureOrders());
        string terminalsBefore = CanonicalTerminals(
            runtime.CaptureTerminalStates());

        ExpectInvalidOperation(
            () => runtime.PublishRestoreOrders(
                runtime.PrepareRestoreOrders(
                    Array.Empty<ApparelWorkOrderSaveData>())),
            "Order-only restore overwrote an existing apparel terminal authority.");
        Require(string.Equals(
                    CanonicalOrders(runtime.CaptureOrders()),
                    ordersBefore,
                    StringComparison.Ordinal)
                && string.Equals(
                    CanonicalTerminals(runtime.CaptureTerminalStates()),
                    terminalsBefore,
                    StringComparison.Ordinal),
            "Rejected order-only restore mutated apparel authority.");
    }

    private static ApparelWorkOrderRuntime CreateRuntime() => new(
        Proxy<IApparelDefinitionCatalog>(),
        Proxy<ITextileMaterialCatalog>(),
        Proxy<IWorldItemStackRuntime>(),
        Proxy<ILeasedItemReservationService>(),
        Proxy<IFacilityCapabilityQuery>(),
        Proxy<IGameClock>(),
        Proxy<IPhysicalItemBatchDispositionService>(),
        Proxy<IApparelPhysicalTransaction>(),
        performance: Proxy<ICharacterPerformanceQuery>());

    private static T Proxy<T>() where T : class =>
        BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<T>();

    private static ApparelWorkOrderSaveData SeedOrder(
        ApparelWorkOrderRuntime runtime,
        string suffix)
    {
        ApparelWorkOrderSaveData authored = new()
        {
            orderId = "apparel-order:qa:terminal:" + suffix,
            kind = ApparelWorkOrderKind.Laundry,
            state = ApparelWorkOrderState.Ready,
            facilityInstanceId = "facility:qa:apparel-terminal:" + suffix,
            requiredWork = 12f,
            completedWork = 3f,
            consumedWork = 2f
        };
        ApparelWorkOrderSaveData[] existingOrders = runtime.CaptureOrders();
        ApparelWorkOrderTerminalStateSaveData[] existingTerminals =
            runtime.CaptureTerminalStates();
        runtime.PublishRestoreState(runtime.PrepareRestoreState(
            existingOrders.Concat(new[] { authored }),
            existingTerminals));
        return runtime.CaptureOrders().Single(value => string.Equals(
            value.orderId,
            authored.orderId,
            StringComparison.Ordinal));
    }

    private static TerminalFixture CommitEffect(
        ApparelWorkOrderRuntime runtime,
        ApparelWorkOrderSaveData source,
        string suffix)
    {
        Require(ProductionApparelOrderTerminalDrainCanonical
                .TryCreatePendingEffectIdentity(
                    source,
                    out ProductionApparelOrderPendingEffectIdentity pending,
                    out string failureReason)
                && pending == null,
            "Apparel terminal source could not produce its canonical effect: "
            + failureReason);
        string sourceFingerprint =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceOrderFingerprint(source);
        string stepOperationId =
            "production-apparel-terminal-step:qa:" + suffix;
        ProductionApparelOrderTerminalEffectReceipt expected =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateTerminalEffectReceipt(
                    stepOperationId,
                    source,
                    sourceFingerprint,
                    pending);
        ProductionApparelOrderTerminalEffectApplyResult result =
            runtime.TryCommitTerminalEffect(expected, pending);
        Require(result.Status ==
                    ProductionApparelOrderTerminalDrainStatus.Applied
                && ProductionApparelOrderTerminalDrainCanonical
                    .EffectReceiptEquals(result.Receipt, expected),
            "Apparel terminal effect commit failed: " + result.FailureReason);
        return new TerminalFixture(
            stepOperationId,
            sourceFingerprint,
            expected);
    }

    private static bool SameOrders(
        IReadOnlyList<ApparelWorkOrderSaveData> left,
        IReadOnlyList<ApparelWorkOrderSaveData> right) => string.Equals(
        CanonicalOrders(left),
        CanonicalOrders(right),
        StringComparison.Ordinal);

    private static bool SameTerminalStates(
        IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> left,
        IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> right) =>
        string.Equals(
            CanonicalTerminals(left),
            CanonicalTerminals(right),
            StringComparison.Ordinal);

    private static string CanonicalOrders(
        IEnumerable<ApparelWorkOrderSaveData> values) => string.Join(
        "\n",
        (values ?? Array.Empty<ApparelWorkOrderSaveData>())
        .OrderBy(value => value.orderId, StringComparer.Ordinal)
        .Select(JsonUtility.ToJson));

    private static string CanonicalTerminals(
        IEnumerable<ApparelWorkOrderTerminalStateSaveData> values) =>
        string.Join(
            "\n",
            (values ?? Array.Empty<ApparelWorkOrderTerminalStateSaveData>())
            .OrderBy(
                value => value.sourceOrder.orderId,
                StringComparer.Ordinal)
            .Select(JsonUtility.ToJson));

    private static bool SameOrder(
        ApparelWorkOrderSaveData left,
        ApparelWorkOrderSaveData right) => left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private static void ExpectInvalidOperation(Action action, string message)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct TerminalFixture
    {
        internal TerminalFixture(
            string stepOperationId,
            string sourceFingerprint,
            ProductionApparelOrderTerminalEffectReceipt effectReceipt)
        {
            StepOperationId = stepOperationId;
            SourceFingerprint = sourceFingerprint;
            EffectReceipt = effectReceipt;
        }

        internal string StepOperationId { get; }
        internal string SourceFingerprint { get; }
        internal ProductionApparelOrderTerminalEffectReceipt EffectReceipt
        {
            get;
        }
    }
}
#endif
