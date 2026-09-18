#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// Root-owned integration witness. The existing focused scenarios retain policy,
// lifecycle and persistence coverage; this runner only closes the registered
// disease, real final-combat damage and old-terminal/new-active alert seams.
public sealed class Wim051052EndlessCrisisLiveWitness
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-051-052-live-consumers-and-alerts.txt";

    private readonly List<string> lines = new();
    private static bool running;
    private DungeonRuntimeLifetimeScope scope;
    private IGameTimeScaleController timeScale;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running,
            "Start once in a fresh disposable main Play session.");
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime is not initialized.");
        IDisposable persistence =
            scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "Cannot protect user save files.");
        persistence.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        GameManager game = FindFirstObjectByType<GameManager>();
        Require(game != null, "Missing main coroutine host.");
        game.isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try
        {
            game.StartCoroutine(new Wim051052EndlessCrisisLiveWitness().Observe());
        }
        catch
        {
            running = false;
            throw;
        }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        Stack<IEnumerator> iterators = new();
        iterators.Push(Run());
        Exception failure = null;
        while (iterators.Count > 0)
        {
            object current = null;
            bool moved;
            try
            {
                moved = iterators.Peek().MoveNext();
                if (moved) current = iterators.Peek().Current;
            }
            catch (Exception error)
            {
                failure = error;
                break;
            }

            if (!moved)
            {
                (iterators.Pop() as IDisposable)?.Dispose();
                continue;
            }
            if (current is IEnumerator nested) iterators.Push(nested);
            else yield return current;
        }

        while (iterators.Count > 0)
            (iterators.Pop() as IDisposable)?.Dispose();
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add(
            "scope=actual main registered disease exposure, invasion candidate/director/final combat and EventAlert UI; controlled EndlessAge state/day/seed in disposable Play; existing focused policy/lifecycle/save proof reused; not a natural ten-day colony or six-adult recovery-burden witness");
        lines.Add(
            "cleanup=operator stops disposable protected Play; persistence disabled before owner selection; no scene/profile/save writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
        Require(ownerManager != null, "Owner preparation is unavailable.");
        if (ownerManager.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(
                        out var expansion,
                        out string expansionFailure)
                    && expansion.CurrentInteriorColumns == 29,
                "Tier-zero preparation failed: " + expansionFailure);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(ownerManager.CurrentOwnerActor != null,
                "Normal party UI did not publish an owner.");
        }

        CharacterActor owner = ownerManager.CurrentOwnerActor;
        Require(owner != null && !owner.IsDead,
            "A live owner is required.");
        Require(CharacterPersistentIdentity.TryGet(
                owner, out CharacterId ownerId),
            "A persistent owner identity is required.");
        IDisposable runFlow =
            scope.Container.Resolve<IDungeonRunFlowRuntime>() as IDisposable;
        Require(runFlow != null,
            "Run-flow event isolation is unavailable in disposable Play.");
        runFlow.Dispose();

        V20CampaignRuntime campaign = scope.Container.Resolve<V20CampaignRuntime>();
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();
        EventAlertRuntime alerts = FindFirstObjectByType<EventAlertRuntime>();
        Require(alerts != null, "Actual event alert runtime is unavailable.");
        ResetEndlessState(campaign);

        IDiseaseDefinitionCatalog diseases =
            scope.Container.Resolve<IDiseaseDefinitionCatalog>();
        IPopulationDiseaseModifierQuery modifiers =
            scope.Container.Resolve<IPopulationDiseaseModifierQuery>();
        IPopulationHealthPersistence health =
            scope.Container.Resolve<IPopulationHealthPersistence>();
        PopulationHealthWorldSaveData healthBefore = health.Capture();
        DiseaseDefinition disease = diseases.Definitions
            .Where(value => value.Contagious
                && (value.Routes & DiseaseTransmissionRoute.Water) != 0)
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .FirstOrDefault(value => !healthBefore.pendingExposures.Any(exposure =>
                string.Equals(exposure.characterId, ownerId.Value,
                    StringComparison.Ordinal)
                && string.Equals(exposure.diseaseId, value.Id,
                    StringComparison.Ordinal)));
        Require(!string.IsNullOrEmpty(disease.Id),
            "No clean authored water-route disease is available for the owner.");
        float baselineSusceptibility =
            modifiers.Resolve(ownerId, disease).Susceptibility;

        calendar.SetDateTime(10, 8);
        campaign.ComposeNextEndlessCrisis(10, 35);
        EndlessCrisisSnapshot compound = campaign.CurrentEndlessCrisis;
        Require(compound.Phase == EndlessCrisisLifecyclePhase.Active
            && compound.Axes.Count == 2
            && compound.Axes.Any(value => value.Axis == EndlessCrisisAxis.Disease)
            && compound.Axes.Any(value => value.Axis == EndlessCrisisAxis.Combat),
            "Authored deterministic seed 35 is no longer Disease+Combat.");
        float diseaseAxis = compound.Axes.Single(value =>
            value.Axis == EndlessCrisisAxis.Disease).Multiplier;
        float pressuredSusceptibility =
            modifiers.Resolve(ownerId, disease).Susceptibility;
        Require(Approximately(
                pressuredSusceptibility / baselineSusceptibility,
                diseaseAxis),
            "Registered disease modifier did not consume the active crisis axis.");

        events.Publish(new PopulationDiseaseRouteExposureEvent(
            ownerId,
            disease.Id,
            DiseaseTransmissionRoute.Water,
            2f,
            1f));
        DiseaseExposureSaveData exposure = health.Capture().pendingExposures.SingleOrDefault(
            value => string.Equals(value.characterId, ownerId.Value,
                         StringComparison.Ordinal)
                && string.Equals(value.diseaseId, disease.Id,
                    StringComparison.Ordinal));
        Require(exposure != null
            && Approximately(exposure.weightedExposureHours, 2f)
            && Approximately(exposure.susceptibility, pressuredSusceptibility),
            "Actual route exposure did not preserve the crisis-adjusted susceptibility.");
        lines.Add("[PASS] registered disease consumer: baseline="
            + baselineSusceptibility + "; axis=" + diseaseAxis
            + "; pressured=" + pressuredSusceptibility
            + "; exposure=" + exposure.weightedExposureHours
            + "; disease=" + disease.Id);

        InvasionThreatRuntime threat = FindFirstObjectByType<InvasionThreatRuntime>();
        InvasionDirectorRuntime director =
            FindFirstObjectByType<InvasionDirectorRuntime>();
        Require(threat != null && director != null
            && !threat.IsCandidatePending
            && director.ActiveIntruders.Count == 0,
            "A fresh invasion candidate/director state is required.");
        float healthBeforeCombat = owner.CurrentHealth;
        Require(threat.ForceCandidateNow(
                "WIM-051/052 실제 전투",
                "엔드리스 전투 축의 실제 피해 소비를 확인합니다."),
            "Actual invasion candidate publication failed.");
        yield return null;
        InvasionIntruderRuntime intruder = director.ActiveIntruders.SingleOrDefault();
        Require(intruder != null,
            "Actual director did not create exactly one owned intruder.");
        InvasionThreatEndlessCrisisOwnershipState owned =
            threat.CaptureEndlessCrisisOwnership();
        Require(owned.CandidateEffectOwnerId.Length == 0
            && owned.DirectResponseOwnerId.Length > 0
            && string.Equals(owned.DirectResponseRuntimeId,
                intruder.RuntimeId, StringComparison.Ordinal),
            "Combat crisis ownership did not reach the actual intruder runtime.");
        calendar.SetDateTime(11, 8);
        intruder.ApplyFinalCombat(owner);
        // Direct invocation verifies the real damage boundary; the normal
        // coroutine would immediately finish after it. Complete that same
        // already-resolved lifecycle without publishing a second resolution.
        intruder.ResolveDefenseFailed(owner);
        yield return null;
        Require(owner.CurrentHealth < healthBeforeCombat
            && campaign.CurrentEndlessCrisis.Phase
                == EndlessCrisisLifecyclePhase.Active
            && campaign.CurrentEndlessCrisis.LastActualEndAbsoluteDay == 11
            && campaign.CurrentEndlessCrisis.DirectResponseOwnerId.Length == 0,
            "Actual final combat did not preserve damage and close exact response ownership.");
        float damagedHealth = owner.CurrentHealth;
        campaign.AdvanceEndlessCrisis(compound.PressureEndAbsoluteDay);
        Require(campaign.CurrentEndlessCrisis.Phase
                == EndlessCrisisLifecyclePhase.Recovery
            && owner.CurrentHealth == damagedHealth,
            "Pressure end restored actual combat damage or skipped recovery.");
        lines.Add("[PASS] actual threat->director->intruder final combat: ownerHP="
            + healthBeforeCombat + "->" + damagedHealth
            + "; response owner released; damage retained in Recovery");

        threat.enabled = false;
        yield return null;
        Require(director.ActiveIntruders.Count == 0,
            "Resolved intruder did not leave the active director set.");
        calendar.SetDateTime(100, 8);
        EndlessCrisisSnapshot oldActive = null;
        int alertSeed = 0;
        for (int seed = 1; seed <= 4096; seed++)
        {
            ResetEndlessState(campaign);
            campaign.ComposeNextEndlessCrisis(100, seed);
            EndlessCrisisSnapshot candidate = campaign.CurrentEndlessCrisis;
            if (candidate.Axes.Count != 1
                || candidate.Axes.Any(value =>
                    value.Axis == EndlessCrisisAxis.Combat))
                continue;
            oldActive = candidate;
            alertSeed = seed;
            break;
        }
        Require(oldActive != null,
            "No deterministic single non-combat alert-order fixture seed exists.");
        string oldSource = oldActive.InstanceId;
        events.Publish(new OperatingDayStartedEvent(100));
        EventAlertRecord oldRecord = alerts.EventLog.SingleOrDefault(value =>
            string.Equals(value.SourceId, oldSource, StringComparison.Ordinal));
        Require(oldRecord != null && !oldRecord.IsResolved,
            "Registered daily adapter did not publish the old active crisis alert.");

        campaign.AdvanceEndlessCrisis(oldActive.PressureEndAbsoluteDay);
        calendar.SetDateTime(oldActive.PressureEndAbsoluteDay, 8);
        events.Publish(new OperatingDayStartedEvent(
            oldActive.PressureEndAbsoluteDay));
        oldRecord = alerts.EventLog.Single(value =>
            string.Equals(value.SourceId, oldSource, StringComparison.Ordinal));
        Require(!oldRecord.IsResolved
            && campaign.CurrentEndlessCrisis.Phase
                == EndlessCrisisLifecyclePhase.Recovery,
            "Old crisis alert did not enter visible recovery.");

        int recoveryEnd = campaign.CurrentEndlessCrisis.RecoveryEndAbsoluteDay;
        Require(recoveryEnd % 10 == 0,
            "Fixture must end recovery on an actual evaluation day.");
        calendar.SetDateTime(recoveryEnd, 8);
        events.Publish(new OperatingDayStartedEvent(recoveryEnd));
        EndlessCrisisSnapshot next = campaign.CurrentEndlessCrisis;
        oldRecord = alerts.EventLog.Single(value =>
            string.Equals(value.SourceId, oldSource, StringComparison.Ordinal));
        EventAlertRecord nextRecord = alerts.EventLog.SingleOrDefault(value =>
            string.Equals(value.SourceId, next.InstanceId, StringComparison.Ordinal));
        Require(oldRecord.IsResolved
            && oldRecord.ResultSummary.Contains("발생한 피해와 소모는 유지")
            && next.Phase == EndlessCrisisLifecyclePhase.Active
            && !string.Equals(next.InstanceId, oldSource,
                StringComparison.Ordinal)
            && nextRecord != null && !nextRecord.IsResolved,
            "Evaluation-day rollover lost the terminal old alert or the new active alert.");
        Click("EventAlertButton_" + oldRecord.Id);
        Require(alerts.IsDetailVisible
            && alerts.SelectedRecord?.Id == oldRecord.Id,
            "Resolved old-crisis detail could not be opened from the actual alert UI.");
        alerts.CloseDetail();
        Click("EventAlertButton_" + nextRecord.Id);
        Require(alerts.IsDetailVisible
            && alerts.SelectedRecord?.Id == nextRecord.Id,
            "New active-crisis detail could not be opened from the actual alert UI.");
        alerts.CloseDetail();
        lines.Add("[PASS] registered day adapter/UI publishes old terminal before new "
            + "active on day " + recoveryEnd + "; old=" + oldSource
            + "; new=" + next.InstanceId + "; seed=" + alertSeed
            + "; both details open");
    }

    private static void ResetEndlessState(V20CampaignRuntime campaign)
    {
        RunMilestoneWorldSaveData state = campaign.CaptureMilestones();
        state.phase = RunProgressionPhase.EndlessAge;
        state.endlessCycle = 0;
        state.endlessCrisisPhase = EndlessCrisisLifecyclePhase.None;
        state.endlessCrisisInstanceId = string.Empty;
        state.endlessCrisisAxes.Clear();
        state.endlessCrisisStartedAbsoluteDay = -1;
        state.endlessCrisisPressureEndAbsoluteDay = -1;
        state.endlessCrisisLastActualEndAbsoluteDay = -1;
        state.endlessCrisisRecoveryEndAbsoluteDay = -1;
        state.lastEndlessCrisisEvaluationAbsoluteDay = -1;
        state.endlessCrisisAuthoredRecoveryDays = 0;
        state.endlessCrisisPendingInvasionSourceOwnerId = string.Empty;
        state.endlessCrisisDirectResponseOwnerId = string.Empty;
        campaign.PublishMilestones(campaign.PrepareMilestones(state));
    }

    private static void Click(string name)
    {
        Button button = Resources.FindObjectsOfTypeAll<Button>()
            .SingleOrDefault(value => value != null
                && value.gameObject.scene.isLoaded
                && value.gameObject.activeInHierarchy
                && string.Equals(value.name, name, StringComparison.Ordinal));
        Require(button != null && button.IsInteractable()
            && PlayModeVerificationFrameWait.DispatchPointerClick(
                button.gameObject, Vector2.zero),
            "Actual pointer target is unavailable: " + name);
    }

    private void Pause()
    {
        GameManager game = FindFirstObjectByType<GameManager>();
        if (game != null) game.isPause = true;
        if (timeScale != null) timeScale.Scale = 0;
    }

    private static bool Approximately(float left, float right) =>
        Math.Abs(left - right) <= 0.00001f;

    private static void Require(bool value, string failure)
    {
        if (!value) throw new InvalidOperationException(failure);
    }
}
#endif
