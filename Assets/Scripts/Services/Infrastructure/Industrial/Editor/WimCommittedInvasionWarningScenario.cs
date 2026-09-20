#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

internal static class WimCommittedInvasionWarningScenario
{
    public static IEnumerator Run(
        DungeonRuntimeLifetimeScope scope,
        IDungeonSaveSectionRegistry saveSections,
        IList<string> report)
    {
        RunInternal(scope, saveSections, report);
        yield break;
    }

    private static void RunInternal(
        DungeonRuntimeLifetimeScope scope,
        IDungeonSaveSectionRegistry saveSections,
        IList<string> report)
    {
        Require(scope?.Container != null,
            "WIM047 requires the loaded gameplay lifetime scope.");
        Require(saveSections != null,
            "WIM047 requires whole-registry save authority.");
        Require(report != null,
            "WIM047 requires a report sink.");

        InvasionThreatRuntime threat = UnityEngine.Object
            .FindFirstObjectByType<InvasionThreatRuntime>();
        InvasionDirectorRuntime director = UnityEngine.Object
            .FindFirstObjectByType<InvasionDirectorRuntime>();
        UITabManager tabManager = UnityEngine.Object
            .FindFirstObjectByType<UITabManager>(FindObjectsInactive.Include);
        IInvasionIntruderContext context = scope.Container
            .Resolve<IInvasionIntruderContext>();
        IInvasionCampaignRuntime campaign = scope.Container
            .Resolve<IInvasionCampaignRuntime>();
        IDefenseFeatureQueryService defenseQuery = scope.Container
            .Resolve<IDefenseFeatureQueryService>();
        IDefenseUiTextQuery defenseText = scope.Container
            .Resolve<IDefenseUiTextQuery>();
        IRandomStreamProvider random = scope.Container
            .Resolve<IRandomStreamProvider>();
        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();

        Require(threat != null && director != null,
            "WIM047 requires the loaded invasion threat and director runtimes.");
        Require(tabManager != null && EventSystem.current != null,
            "WIM047 requires the loaded main UI and EventSystem.");
        Require(director.ActiveIntruders.Count == 0,
            "WIM047 requires no invasion to be active before candidate selection; "
            + $"actual={director.ActiveIntruders.Count}.");
        Require(context.TryResolveEntry(out InvasionIntruderEntry executionEntry),
            "WIM047 requires the production invasion context to resolve an execution entry.");

        TabId? originalTab = FindActiveTopTab();
        Exception primaryFailure = null;
        List<Exception> cleanupFailures = new();
        IDisposable alertSubscription = null;
        string failureStage = "controlled-missing-entry";
        try
        {
            VerifyControlledMissingEntryFailure(
                scope,
                context,
                threat.LatestSnapshot,
                report);
            failureStage = "preselection-production-ui";
            VerifyPreselectionUnknown(
                tabManager,
                director,
                defenseQuery,
                defenseText,
                random,
                report);

            failureStage = "candidate-alert-subscription";
            List<EventAlertRequest> candidateAlerts = new();
            alertSubscription = events.Subscribe<EventAlertRequestedEvent>(
                value =>
                {
                    if (value.request != null)
                    {
                        candidateAlerts.Add(value.request);
                    }
                });

            failureStage = "candidate-force";
            bool forced = threat.ForceCandidateNow();
            InvasionIntruderRuntime intruder = director.ActiveIntruders
                .SingleOrDefault();
            InvasionCommittedWarningProjection projection = default;
            bool hasProjection = intruder != null
                && intruder.TryGetCommittedWarningProjection(
                    out projection);
            string spawnEvidence =
                $"forced={forced};active={director.ActiveIntruders.Count};"
                + $"lastFailure={director.LastSpawnFailureReason};"
                + $"projection={hasProjection}";
            RecordAndRequire(
                report,
                "wim047-candidate",
                forced
                && director.ActiveIntruders.Count == 1
                && intruder != null
                && hasProjection,
                spawnEvidence);

            failureStage = "committed-selection";
            ScheduledInvasionOperationState operation = campaign.Operations
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.operationId,
                        projection.RaidId,
                        StringComparison.Ordinal));
            EnemyIndividualSaveData individual = intruder.EnemyIndividual;
            bool selectedIdentityAndEntry =
                string.Equals(
                    projection.RaidId,
                    intruder.RaidId,
                    StringComparison.Ordinal)
                && projection.OperationKind == intruder.OperationKind
                && string.Equals(
                    projection.EnemyArchetypeId,
                    individual?.enemyArchetypeId,
                    StringComparison.Ordinal)
                && string.Equals(
                    projection.EnemyNature,
                    intruder.Pattern.title,
                    StringComparison.Ordinal)
                && projection.HasEntryGeometry
                && projection.EntryGridPosition == executionEntry.GridPosition
                && projection.OutsidePosition == executionEntry.OutsidePosition
                && projection.DoorPosition == executionEntry.DoorPosition;
            bool targetStillUnknown = intruder.CurrentPriorityTarget == null;
            bool realRallyPhase = intruder.State == InvasionIntruderState.Rallying
                && intruder.WarningRallySecondsRemaining > 0f;
            string selectionEvidence =
                $"raid={projection.RaidId};runtimeRaid={intruder.RaidId};"
                + $"archetype={projection.EnemyArchetypeId};"
                + $"individual={individual?.enemyArchetypeId};"
                + $"nature={projection.EnemyNature};pattern={intruder.Pattern.title};"
                + $"entry={projection.EntryGridPosition};"
                + $"expectedEntry={executionEntry.GridPosition};"
                + $"state={intruder.State};"
                + $"rally={intruder.WarningRallySecondsRemaining:0.###};"
                + $"targetUnknown={targetStillUnknown};"
                + $"operationMatch={operation != null};"
                + $"objective={operation?.objectiveId};"
                + $"confidence={operation?.intelligenceConfidence:0.###}";
            RecordAndRequire(
                report,
                "wim047-committed-selection",
                selectedIdentityAndEntry
                && targetStillUnknown
                && realRallyPhase
                && operation != null
                && !string.IsNullOrWhiteSpace(operation.objectiveId),
                selectionEvidence);

            failureStage = "committed-alert";
            string expectedAlert =
                InvasionThreatCalculator.BuildCommittedCandidateDetail(
                    projection,
                    intruder.WarningRallySecondsRemaining,
                    operation.objectiveId,
                    operation.intelligenceConfidence);
            bool exactCommittedAlert = candidateAlerts.Any(value =>
                string.Equals(value.Detail, expectedAlert, StringComparison.Ordinal));
            RecordAndRequire(
                report,
                "wim047-alert",
                exactCommittedAlert,
                $"captured={candidateAlerts.Count};exact={exactCommittedAlert};"
                + $"detail={Flatten(expectedAlert)}");

            failureStage = "live-query-render";
            VerifyCommittedQueryAndRender(
                "wim047-live",
                tabManager,
                intruder,
                projection,
                operation,
                defenseQuery,
                defenseText,
                random,
                report);

            failureStage = "whole-registry-capture-restore";
            List<DungeonSaveSectionEnvelope> checkpoint =
                saveSections.CaptureAll();
            DungeonGameRestoreReport restoreReport = new();
            bool restoredAll = saveSections.RestoreAll(
                    checkpoint,
                    restoreReport)
                && restoreReport.Success;
            RecordAndRequire(
                report,
                "wim047-whole-registry-restore",
                restoredAll,
                $"sections={checkpoint.Count};success={restoreReport.Success};"
                + $"errors={Flatten(string.Join(" | ", restoreReport.Errors))}");

            failureStage = "restored-selection";
            InvasionIntruderRuntime restoredIntruder = director.ActiveIntruders
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.RaidId,
                        projection.RaidId,
                        StringComparison.Ordinal));
            InvasionCommittedWarningProjection restoredProjection = default;
            bool restoredProjectionAvailable = restoredIntruder != null
                && restoredIntruder.TryGetCommittedWarningProjection(
                    out restoredProjection);
            ScheduledInvasionOperationState restoredOperation = campaign.Operations
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.operationId,
                        projection.RaidId,
                        StringComparison.Ordinal));
            string restoreEvidence =
                $"active={director.ActiveIntruders.Count};"
                + $"intruder={restoredIntruder != null};"
                + $"projection={restoredProjectionAvailable};"
                + $"same={restoredProjectionAvailable && SameProjection(projection, restoredProjection)};"
                + $"operation={restoredOperation?.operationId};"
                + $"objective={restoredOperation?.objectiveId};"
                + $"targetUnknown={restoredIntruder?.CurrentPriorityTarget == null}";
            RecordAndRequire(
                report,
                "wim047-restored-selection",
                director.ActiveIntruders.Count == 1
                && restoredProjectionAvailable
                && SameProjection(projection, restoredProjection)
                && restoredOperation != null
                && string.Equals(
                    restoredOperation.objectiveId,
                    operation.objectiveId,
                    StringComparison.Ordinal)
                && restoredIntruder.CurrentPriorityTarget == null,
                restoreEvidence);

            failureStage = "restored-query-render";
            VerifyCommittedQueryAndRender(
                "wim047-restored",
                tabManager,
                restoredIntruder,
                restoredProjection,
                restoredOperation,
                defenseQuery,
                defenseText,
                random,
                report);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
            report.Add(
                "wim047-exception=FAIL;"
                + $"stage={failureStage};type={exception.GetType().Name};"
                + $"message={Flatten(exception.Message)};"
                + $"stack={Flatten(exception.StackTrace)}");
        }
        finally
        {
            alertSubscription?.Dispose();
            try
            {
                RestoreTopTab(originalTab);
            }
            catch (Exception exception)
            {
                cleanupFailures.Add(new InvalidOperationException(
                    "WIM047 failed to restore the original top-tab state.",
                    exception));
            }
        }

        if (cleanupFailures.Count > 0)
        {
            if (primaryFailure != null)
            {
                cleanupFailures.Insert(0, primaryFailure);
            }
            throw new AggregateException(
                "WIM047 verification and UI cleanup did not both complete.",
                cleanupFailures);
        }
        if (primaryFailure != null)
        {
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }
    }

    private static void VerifyControlledMissingEntryFailure(
        DungeonRuntimeLifetimeScope scope,
        IInvasionIntruderContext liveContext,
        InvasionThreatSnapshot snapshot,
        IList<string> report)
    {
        GameObject host = new("WIM047 Missing Entry Director");
        host.SetActive(false);
        try
        {
            InvasionDirectorRuntime isolated =
                host.AddComponent<InvasionDirectorRuntime>();
            isolated.Construct(
                new MissingEntryContext(liveContext),
                scope.Container.Resolve<IInvasionIntruderDataProvider>(),
                scope.Container.Resolve<IInvasionIntruderFactory>(),
                scope.Container.Resolve<IDefenseStatusRuntimeService>(),
                scope.Container.Resolve<IGameClock>(),
                scope.Container.Resolve<IRandomStreamProvider>(),
                new GameEventBus(),
                scope.Container.Resolve<IOffenseRegionRuntime>(),
                scope.Container.Resolve<ITreasuryDefenseRuntime>(),
                scope.Container.Resolve<IExternalInfluenceRuntime>(),
                scope.Container.Resolve<IInvasionCampaignRuntime>(),
                scope.Container.Resolve<IFacilityCapabilityQuery>(),
                scope.Container.Resolve<
                    InvasionSignalHornDurableEquipmentRuntime>(),
                scope.Container.Resolve<ICharacterPerformanceQuery>(),
                scope.Container.Resolve<IMigratedProducerOutcomeTransaction>());
            bool spawned = isolated.TrySpawnIntruder(snapshot, out CharacterActor actor);
            bool committedDetail = isolated.ActiveIntruders.Any(value =>
                value != null && value.TryGetCommittedWarningProjection(out _));
            bool pass = !spawned
                && actor == null
                && isolated.ActiveIntruders.Count == 0
                && !committedDetail
                && string.Equals(
                    isolated.LastSpawnFailureReason,
                    "missing-invasion-entry",
                    StringComparison.Ordinal);
            RecordAndRequire(
                report,
                "wim047-missing-entry",
                pass,
                $"spawned={spawned};actor={actor != null};"
                + $"active={isolated.ActiveIntruders.Count};"
                + $"committedDetail={committedDetail};"
                + $"failure={isolated.LastSpawnFailureReason}");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    private static void VerifyPreselectionUnknown(
        UITabManager tabManager,
        InvasionDirectorRuntime director,
        IDefenseFeatureQueryService defenseQuery,
        IDefenseUiTextQuery defenseText,
        IRandomStreamProvider random,
        IList<string> report)
    {
        IReadOnlyList<RandomStreamStateSnapshot> before =
            random.CaptureStates();

        // The production Defense surface creates/injects its feature panel and
        // performs the first query while the real top-tab click is handled.
        // Open it before issuing the fixture's independent query so this check
        // follows the same UI lifetime contract as normal gameplay.
        OpenTopTab(tabManager, TabId.Defense);
        DefenseFeatureSurfaceModel model = defenseQuery.Capture(string.Empty);
        IReadOnlyList<RandomStreamStateSnapshot> after =
            random.CaptureStates();
        string unknown = defenseText.Get("CommittedWarningUnknown");
        Require(!string.IsNullOrWhiteSpace(unknown),
            "WIM047 production Defense text returned no unknown-warning content.");
        Require(model != null,
            "WIM047 production Defense query returned no preselection model.");
        Require(model.Intruders != null,
            "WIM047 production Defense query returned no intruder collection.");
        Require(model.ThreatSummary != null,
            "WIM047 production Defense query returned no threat summary.");
        bool queryUnknown = model.Intruders.Count == 0
            && model.ThreatSummary.Contains(unknown, StringComparison.Ordinal);
        string threatSectionName = "Section_"
            + defenseText.Get("Section.InvasionThreat");
        bool renderUnknown = HasVisibleTextContaining(
            threatSectionName,
            unknown);
        bool randomUnchanged = SameRandomStates(before, after);
        RecordAndRequire(
            report,
            "wim047-preselection",
            director.ActiveIntruders.Count == 0
            && queryUnknown
            && renderUnknown
            && randomUnchanged,
            $"active={director.ActiveIntruders.Count};"
            + $"queryUnknown={queryUnknown};renderUnknown={renderUnknown};"
            + $"queryRandomUnchanged={randomUnchanged}");
    }

    private static void VerifyCommittedQueryAndRender(
        string rowName,
        UITabManager tabManager,
        InvasionIntruderRuntime intruder,
        InvasionCommittedWarningProjection projection,
        ScheduledInvasionOperationState operation,
        IDefenseFeatureQueryService defenseQuery,
        IDefenseUiTextQuery defenseText,
        IRandomStreamProvider random,
        IList<string> report)
    {
        Require(intruder != null && operation != null,
            rowName + " requires a committed intruder and matched operation.");
        Require(intruder.State == InvasionIntruderState.Rallying
                && intruder.WarningRallySecondsRemaining > 0f,
            rowName + " expected the real rally state/time before target selection; "
            + $"state={intruder.State};"
            + $"remaining={intruder.WarningRallySecondsRemaining:0.###}.");

        string expectedPhase = defenseText.Get(
            "CommittedWarningRallyEstimate",
            Mathf.CeilToInt(intruder.WarningRallySecondsRemaining));
        string expectedWarning = defenseText.Get(
            "CommittedWarningDetail",
            projection.EntryGridPosition.ToString(),
            defenseText.Get(
                "CommittedWarningDirection." + projection.ApproachDirection),
            expectedPhase,
            projection.EnemyNature,
            defenseText.Get("Operation." + projection.OperationKind),
            operation.objectiveId,
            defenseText.Get("CommittedWarningUnknownValue"));
        string expectedState = defenseText.Get(
            "IntruderState." + intruder.State);

        IReadOnlyList<RandomStreamStateSnapshot> before =
            random.CaptureStates();
        DefenseFeatureSurfaceModel model = defenseQuery.Capture(string.Empty);
        DefenseFeatureIntruderRow row = model.Intruders.SingleOrDefault();
        OpenTopTab(tabManager, TabId.Defense);
        IReadOnlyList<RandomStreamStateSnapshot> after =
            random.CaptureStates();

        bool exactQuery = row != null
            && !string.IsNullOrEmpty(row.Detail)
            && row.Detail.Contains(expectedState, StringComparison.Ordinal)
            && row.Detail.Contains(expectedWarning, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(model.ThreatSummary)
            && model.ThreatSummary.Contains(
                expectedWarning,
                StringComparison.Ordinal);
        string threatSectionName = "Section_"
            + defenseText.Get("Section.InvasionThreat");
        const string intruderCardName = "P1Action_IntruderTrack_0_Card";
        bool exactRender = HasVisibleTextContaining(
                threatSectionName,
                expectedWarning)
            && HasVisibleTextContaining(intruderCardName, expectedWarning)
            && HasVisibleTextContaining(intruderCardName, expectedState);
        bool randomUnchanged = SameRandomStates(before, after);
        RecordAndRequire(
            report,
            rowName,
            exactQuery && exactRender && randomUnchanged,
            $"query={exactQuery};render={exactRender};"
            + $"queryRandomUnchanged={randomUnchanged};"
            + $"state={intruder.State};phase={Flatten(expectedPhase)};"
            + $"warning={Flatten(expectedWarning)}");
    }

    private static void OpenTopTab(UITabManager tabManager, TabId id)
    {
        Require(tabManager != null,
            "WIM047 cannot open a top tab without UITabManager.");
        if (FindActiveTopTab() == id)
        {
            ClickTopTab(id);
        }
        ClickTopTab(id);
        Canvas.ForceUpdateCanvases();
        Require(FindActiveTopTab() == id,
            $"WIM047 EventSystem route did not open the {id} tab.");
    }

    private static void RestoreTopTab(TabId? originalTab)
    {
        TabId? current = FindActiveTopTab();
        if (current == originalTab)
        {
            return;
        }
        if (current.HasValue)
        {
            ClickTopTab(current.Value);
        }
        if (originalTab.HasValue)
        {
            ClickTopTab(originalTab.Value);
        }
        Canvas.ForceUpdateCanvases();
        Require(FindActiveTopTab() == originalTab,
            "WIM047 top-tab cleanup did not restore the original selection.");
    }

    private static void ClickTopTab(TabId id)
    {
        Button button = Resources.FindObjectsOfTypeAll<UITabButtonBinding>()
            .Where(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy
                && value.Id == id)
            .Select(value => value.GetComponent<Button>())
            .SingleOrDefault(value => value != null && value.IsInteractable());
        Require(button != null && EventSystem.current != null,
            $"WIM047 requires one interactable {id} top-tab button and EventSystem.");
        PointerEventData pointer = new(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        };
        ExecuteEvents.ExecuteHierarchy(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerClickHandler);
    }

    private static TabId? FindActiveTopTab()
    {
        UITabIdentity[] active = Resources.FindObjectsOfTypeAll<UITabIdentity>()
            .Where(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy)
            .ToArray();
        if (active.Length == 0)
        {
            return null;
        }
        if (active.Length != 1)
        {
            throw new InvalidOperationException(
                "WIM047 expected at most one active top tab; actual="
                + string.Join(",", active.Select(value => value.Id)) + ".");
        }
        return active[0].Id;
    }

    private static bool HasVisibleTextContaining(
        string rootName,
        string expected)
    {
        if (string.IsNullOrWhiteSpace(rootName)
            || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        RectTransform[] roots = Resources.FindObjectsOfTypeAll<RectTransform>()
            .Where(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy
                && string.Equals(
                    value.name,
                    rootName,
                    StringComparison.Ordinal))
            .ToArray();
        return roots.Length == 1
            && roots[0].GetComponentsInChildren<TMP_Text>(false)
                .Any(value => value != null
                    && value.gameObject.activeInHierarchy
                    && !string.IsNullOrEmpty(value.text)
                    && value.text.Contains(expected, StringComparison.Ordinal));
    }

    private static bool SameProjection(
        InvasionCommittedWarningProjection first,
        InvasionCommittedWarningProjection second) =>
        string.Equals(first.RaidId, second.RaidId, StringComparison.Ordinal)
        && first.OperationKind == second.OperationKind
        && string.Equals(
            first.EnemyArchetypeId,
            second.EnemyArchetypeId,
            StringComparison.Ordinal)
        && string.Equals(
            first.EnemyNature,
            second.EnemyNature,
            StringComparison.Ordinal)
        && first.HasEntryGeometry == second.HasEntryGeometry
        && first.EntryGridPosition == second.EntryGridPosition
        && first.OutsidePosition == second.OutsidePosition
        && first.DoorPosition == second.DoorPosition;

    private static bool SameRandomStates(
        IReadOnlyList<RandomStreamStateSnapshot> first,
        IReadOnlyList<RandomStreamStateSnapshot> second)
    {
        if (first == null || second == null || first.Count != second.Count)
        {
            return false;
        }
        for (int index = 0; index < first.Count; index++)
        {
            if (!string.Equals(
                    first[index]?.StreamId,
                    second[index]?.StreamId,
                    StringComparison.Ordinal)
                || first[index]?.State != second[index]?.State)
            {
                return false;
            }
        }
        return true;
    }

    private static void RecordAndRequire(
        IList<string> report,
        string rowName,
        bool passed,
        string evidence)
    {
        string line = rowName + "=" + (passed ? "PASS" : "FAIL")
            + ";" + evidence;
        report.Add(line);
        Require(passed, line);
    }

    private static string Flatten(string value) =>
        (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class MissingEntryContext : IInvasionIntruderContext
    {
        private readonly IInvasionIntruderContext live;

        public MissingEntryContext(IInvasionIntruderContext live)
        {
            this.live = live ?? throw new ArgumentNullException(nameof(live));
        }

        public bool TryGetGrid(out Grid grid) => live.TryGetGrid(out grid);

        public bool TryGetOwner(out CharacterActor owner) =>
            live.TryGetOwner(out owner);

        public bool TryResolveBuilding(
            BuildingInstanceId id,
            out BuildableObject building) =>
            live.TryResolveBuilding(id, out building);

        public bool TryResolveEntry(out InvasionIntruderEntry entry)
        {
            entry = default;
            return false;
        }

        public InvasionIntruderSettings ApplyRunVariables(
            InvasionIntruderSettings source) =>
            live.ApplyRunVariables(source);
    }
}
#endif
