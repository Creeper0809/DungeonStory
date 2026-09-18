#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using DungeonStory.Operation;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// Main-owned important state/UI witness. Authored assets are never changed.
public sealed class Wim039SocietyNoticeLiveRunner
{
    public const string StateReportPath = "Artifacts/QA/wim-implementation/wim-039-risk-state-focused.txt";
    public const string LiveReportPath = "Artifacts/QA/wim-implementation/wim-039-failure-result-live.txt";
    private const string IncidentId = "service-incident:forbiddenmeal";
    private static bool running;
    private readonly List<string> lines = new();
    private IGameTimeScaleController scale;

    public static string RunRiskStateFocused()
    {
        var lines = new List<string>();
        LifeEventDefinitionSO clone = null;
        try
        {
            var source = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
            var original = source.GetAll<LifeEventDefinitionSO>().Single(x => x.StableId == "life-event:last-lesson");
            string originalJson = JsonUtility.ToJson(original);
            clone = Instantiate(original);
            Require(clone.riskTier == ExperienceEventRiskTier.Serious && !string.IsNullOrWhiteSpace(clone.riskReason),
                "Last-lesson authored nonlethal injury risk changed; inspect expectations.");
            var catalog = new V20StoryContentCatalog(new CloneDefinitionSource(source, original, clone));
            var campaign = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
            var occurrence = CreateOccurrence(campaign, clone.StableId, 42, clone);
            string frozenReason = occurrence.riskReason;
            Require(occurrence.riskTier == ExperienceEventRiskTier.Serious && frozenReason == clone.riskReason,
                "Actual CreateEvent did not freeze authored risk.");
            var society = new SocietyEventWorldSaveData { lastEvaluationAbsoluteDay = 42 };
            society.activeEvents.Add(occurrence);
            campaign.PublishSociety(campaign.PrepareSociety(society));
            // Change only an unregistered disposable SO clone, not the real asset.
            clone.riskTier = ExperienceEventRiskTier.None;
            clone.riskReason = "검증 전용: 이후 작성 변경";
            var roundTrip = JsonUtility.FromJson<SocietyEventWorldSaveData>(JsonUtility.ToJson(campaign.CaptureSociety()));
            campaign.PublishSociety(campaign.PrepareSociety(roundTrip));
            Require(campaign.ActiveSocietyEvents.Single().riskTier == ExperienceEventRiskTier.Serious
                && campaign.ActiveSocietyEvents.Single().riskReason == frozenReason
                && JsonUtility.ToJson(original) == originalJson,
                "Restore recomputed frozen risk from current definition or changed authored asset.");
            lines.Add("PASS actual CreateEvent freezes Serious/nonlethal risk; cloned later authoring does not change saved occurrence; source SO unchanged");

            string before = JsonUtility.ToJson(campaign.CaptureSociety());
            var invalid = JsonUtility.FromJson<SocietyEventWorldSaveData>(before);
            invalid.activeEvents[0].riskTier = (ExperienceEventRiskTier)999;
            MustReject(() => campaign.PrepareSociety(invalid), "Undefined risk accepted.");
            invalid = JsonUtility.FromJson<SocietyEventWorldSaveData>(before);
            invalid.activeEvents[0].riskReason = "";
            MustReject(() => campaign.PrepareSociety(invalid), "Missing risk reason accepted.");
            Require(JsonUtility.ToJson(campaign.CaptureSociety()) == before,
                "Rejected risk candidate mutated live society authority.");
            var seasonal = new SeasonalEventWorldSaveData();
            seasonal.activeEvents.Add(CreateOccurrence(campaign, catalog.SeasonalEvents.First().StableId, 42));
            campaign.PrepareSeasonal(seasonal);
            lines.Add("PASS invalid enum/missing reason rejected without publication; shared seasonal payload not forced to carry society risk");
            lines.Insert(0, "result=PASS");
        }
        catch (Exception error) { lines.Insert(0, "result=FAIL"); lines.Add(error.ToString()); }
        finally { if (clone != null) DestroyImmediate(clone); }
        lines.Add("scope=local production campaign/current-format state and cloned definition; not natural event eligibility, external effects, whole-world restore or six-adult play");
        Directory.CreateDirectory(Path.GetDirectoryName(StateReportPath));
        File.WriteAllLines(StateReportPath, lines);
        return string.Join("\n", lines);
    }

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running, "Fresh disposable main Play required.");
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main scope missing.");
        var saves = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(saves != null, "Disk persistence isolation unavailable.");
        saves.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        var host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Main coroutine host unavailable.");
        var runner = new Wim039SocietyNoticeLiveRunner { scale = scope.Container.Resolve<IGameTimeScaleController>() };
        runner.Pause();
        Directory.CreateDirectory(Path.GetDirectoryName(LiveReportPath));
        File.WriteAllText(LiveReportPath, "result=RUNNING\n");
        running = true;
        try { host.StartCoroutine(runner.Observe()); }
        catch { running = false; throw; }
        return "RUNNING " + LiveReportPath;
    }

    private IEnumerator Observe()
    {
        var pending = new Stack<IEnumerator>();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object value = null;
            bool moved;
            try { moved = pending.Peek().MoveNext(); if (moved) value = pending.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
            if (value is IEnumerator nested) pending.Push(nested); else yield return value;
        }
        try { while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose(); }
        finally { Pause(); }
        lines.Insert(0, failure == null ? "result=PASS" : "result=FAIL");
        if (failure != null) lines.Add(failure.ToString());
        lines.Add("scope=controlled authored occurrence and initial funds; actual main notice pointer input, real dispatcher/content commit and persisted result. Local risk restore separately verified; not natural accident eligibility, every social effect or six-adult balance.");
        lines.Add("cleanup=paused disposable main Play; disk saves disabled before owner UI; operator stops without saving scene/profile/settings");
        File.WriteAllLines(LiveReportPath, lines);
        Debug.Log("WIM039 failure/result witness " + (failure == null ? "PASS" : "FAIL: " + failure.Message));
        running = false;
    }

    private IEnumerator Run()
    {
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        var owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner setup missing.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>().TryReconcileNewRunTierZero(
                out _, out string reason), "Tier zero: " + reason);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Owner UI did not publish normal run.");
        }
        var campaign = scope.Container.Resolve<V20CampaignRuntime>();
        var catalog = scope.Container.Resolve<V20StoryContentCatalog>();
        var definition = catalog.ServiceIncidents.Single(x => x.StableId == IncidentId);
        var choice = definition.responses.Single(x => x.choiceId == "compensate");
        Require(choice.effects.Count == 1 && choice.effects[0].kind == V20ContentEffectKind.Money
            && choice.effects[0].amount == -60 && definition.riskTier == ExperienceEventRiskTier.Recoverable,
            "Authored compensate fixture changed; inspect real effects before updating expectations.");
        var current = campaign.CaptureSociety();
        campaign.PrepareSociety(current);
        var startupIncidents = current.activeEvents.Where(x =>
            ((ISocietyEventCatalog)catalog).Require(x.definitionId) is ServiceIncidentDefinitionSO
            or LifeEventDefinitionSO { emergency: true }).ToArray();
        Require(startupIncidents.All(x => !x.resolved && string.IsNullOrEmpty(x.selectedChoiceId)
            && (x.guestDelivery?.phase ?? GuestRequestDeliveryPhase.None) == GuestRequestDeliveryPhase.None),
            "Cannot replace accepted or terminal startup incident in disposable fixture.");
        foreach (var startup in startupIncidents) current.activeEvents.Remove(startup);
        campaign.PublishSociety(campaign.PrepareSociety(current));
        // Controlled incident preparation for this notice witness; actual
        // Customer meal provenance is exercised by the WIM040 live runner.
        var captured = campaign.CaptureObservedMealIncident(new ObservedMealIncidentSnapshot(
            new ConsumableOperationId("consumable-operation:wim039:notice"),
            CharacterPersistentIdentity.Require(owner.CurrentOwnerActor),
            new ItemDefinitionId("food:lavish-meat"), new ItemStackId("stack:wim039:notice"),
            default, true, new CoreGridCell(0, 0), 2, true, false, false),
            new V20DailyEventContext { AbsoluteDay = 2, RunSeed = 39039, Season = Season.Spring });
        Require(captured.Created && captured.IncidentKind == ServiceIncidentKind.ForbiddenMeal,
            "Controlled observed incident preparation failed: " + captured.Failure);
        var occurrence = campaign.ActiveSocietyEvents.Single(x => x.instanceId == captured.OccurrenceInstanceId);
        scope.Container.Resolve<IGameCalendar>().SetDateTime(2, 8);
        var adapter = (V20CampaignApplicationAdapter)scope.Container.Resolve<IV20DailyEvaluationDiagnostic>();
        var publish = typeof(V20CampaignApplicationAdapter).GetMethod("PublishActionableAlerts", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(publish != null, "Existing alert producer method missing.");
        publish.Invoke(adapter, null);
        var notices = FindFirstObjectByType<EventAlertRuntime>();
        EventAlertRecord Notice() => notices.EventLog.Single(x => x.SourceId == occurrence.instanceId);
        var initialNotice = Notice();
        var staleRequest = new EventAlertRequest(initialNotice.Title, initialNotice.Detail,
            initialNotice.Importance, initialNotice.Category, initialNotice.Choices.ToArray(), initialNotice.SourceId);
        int initialCount = initialNotice.Count;
        Require(initialNotice.Importance == EventAlertImportance.Medium
            && initialNotice.Detail.Contains(occurrence.riskReason, StringComparison.Ordinal),
            "Main alert ignored frozen recoverable risk or reason.");
        var money = scope.Container.Resolve<IGameMoneyAccount>();
        Require(money.Balance >= 0 && money.TrySpend(money.Balance, out _), "Could not prepare exact zero funds through money command.");
        var ledger = scope.Container.Resolve<IEconomyTransactionLedger>();
        var physical = scope.Container.Resolve<IWorldItemStackRuntime>();
        string societyBefore = JsonUtility.ToJson(campaign.CaptureSociety());
        string factionsBefore = JsonUtility.ToJson(campaign.CaptureFactions());
        string physicalBefore = JsonUtility.ToJson(physical.Capture());
        string ledgerBefore = JsonUtility.ToJson(ledger.Capture());
        int choiceIndex = initialNotice.Choices.ToList().FindIndex(x => x.ActionId ==
            V21ContentAlertActionIds.Society(occurrence.instanceId, "compensate"));
        Require(choiceIndex >= 0, "Real compensate action missing.");
        Click("EventAlertButton_" + initialNotice.Id);
        Click("EventChoice_" + (choiceIndex + 1));
        string failureDetail = Notice().ChoiceFailureDetail;
        Require(!string.IsNullOrWhiteSpace(failureDetail)
            && failureDetail.Contains("보유 0", StringComparison.Ordinal)
            && failureDetail.Contains("필요 60", StringComparison.Ordinal)
            && !Notice().IsResolved && !notices.IsDismissed(Notice()) && notices.IsDetailVisible
            && notices.SelectedRecord.Id == initialNotice.Id && Notice().Choices.Count == 3
            && HasVisibleText(failureDetail),
            "Failed real choice hid reason, dismissed source or removed retry.");
        Click("EventChoice_" + (choiceIndex + 1));
        Require(Notice().ChoiceFailureDetail == failureDetail && Notice().Count == initialCount
            && JsonUtility.ToJson(campaign.CaptureSociety()) == societyBefore
            && JsonUtility.ToJson(campaign.CaptureFactions()) == factionsBefore
            && JsonUtility.ToJson(physical.Capture()) == physicalBefore
            && JsonUtility.ToJson(ledger.Capture()) == ledgerBefore && money.Balance == 0,
            "Repeated rejected choice appended failure, duplicated source or mutated a domain.");
        lines.Add("PASS actual pointer choice twice with zero funds: localized failure='" + failureDetail
            + "'; retry/source preserved; society/factions/physical/money ledger unchanged");

        var alertSaves = scope.Container.Resolve<IEventAlertSaveService>();
        var failedSave = JsonUtility.FromJson<DungeonEventAlertSaveData>(JsonUtility.ToJson(alertSaves.Capture()));
        alertSaves.PublishRestore(alertSaves.PrepareRestore(failedSave));
        Require(Notice().ChoiceFailureDetail == failureDetail && !Notice().IsResolved && Notice().Choices.Count == 3,
            "Current alert save lost failure or pending actions.");
        money.Add(60);
        var transactionIds = ledger.Records.Select(x => x.transactionId).ToHashSet(StringComparer.Ordinal);
        Click("EventAlertButton_" + Notice().Id);
        Click("EventChoice_" + (choiceIndex + 1));
        var completed = campaign.RecentResolvedSocietyEvents.Single(x => x.instanceId == occurrence.instanceId);
        Require(completed.resolved && completed.selectedChoiceId == "compensate"
            && completed.terminalActionLabel == choice.title && completed.terminalOutcomeNarrative == choice.outcomeText
            && completed.terminalEffectKinds.SequenceEqual(new[] { V20ContentEffectKind.Money })
            && completed.riskTier == ExperienceEventRiskTier.Recoverable,
            "Committed society result lost actual choice, effect kind or frozen risk.");
        var resultNotice = Notice();
        Require(money.Balance == 0 && resultNotice.IsResolved && resultNotice.Choices.Count == 0
            && resultNotice.ChoiceFailureDetail == "" && resultNotice.ResultSummary.Contains(choice.title, StringComparison.Ordinal)
            && notices.IsDetailVisible && !notices.IsDismissed(resultNotice)
            && notices.SelectedRecord.Id == initialNotice.Id && HasVisibleText(resultNotice.ResultSummary),
            "Successful retry did not replace failure with visible read-only committed result.");
        var newTransactions = ledger.Records.Where(x => !transactionIds.Contains(x.transactionId)).ToArray();
        Require(newTransactions.Length == 1 && newTransactions[0].succeeded && newTransactions[0].amount == -60
            && newTransactions[0].sourceId == "content:" + V21ContentAlertActionIds.Society(occurrence.instanceId, "compensate"),
            "Terminal result did not correspond to exact actual money debit.");
        string resultJson = JsonUtility.ToJson(campaign.CaptureSociety());
        string resultLedger = JsonUtility.ToJson(ledger.Capture());
        Require(!notices.ExecuteChoice(choiceIndex), "Read-only result still executed an action.");
        scope.Container.Resolve<IEventAlertChoiceActionDispatcher>().TryDispatch(
            V21ContentAlertActionIds.Society(occurrence.instanceId, "compensate"), out _);
        Require(resultJson == JsonUtility.ToJson(campaign.CaptureSociety())
            && resultLedger == JsonUtility.ToJson(ledger.Capture()) && money.Balance == 0,
            "Terminal replay mutated society or paid twice.");
        var resultSave = JsonUtility.FromJson<DungeonEventAlertSaveData>(JsonUtility.ToJson(alertSaves.Capture()));
        alertSaves.PublishRestore(alertSaves.PrepareRestore(resultSave));
        Require(Notice().IsResolved && Notice().Choices.Count == 0 && Notice().ResultSummary == resultNotice.ResultSummary,
            "Current saved result became actionable or lost summary.");
        Click("EventAlertButton_" + Notice().Id);
        Require(notices.IsDetailVisible && notices.SelectedRecord.IsResolved,
            "Restored read-only result cannot be reopened through its actual button.");
        // Simulate a late producer replay of the pre-completion source content.
        notices.OnTriggerEvent(new EventAlertRequestedEvent(staleRequest));
        Require(Notice().IsResolved && Notice().Choices.Count == 0 && Notice().ResultSummary == resultNotice.ResultSummary,
            "Late source refresh revived a completed action.");
        lines.Add("PASS current alert JSON failure/result roundtrip; funded retry commits one -60 transaction; actual open result remains read-only; action/source replay changes nothing");
        lines.Add("result summary=" + Notice().ResultSummary);

        // Only the changed acceptance/result boundary, not another haul replay.
        var guest = catalog.GuestRequests.Single(x => x.StableId == "guest-request:sealed-archive");
        Require(scope.Container.Resolve<IFacilityCapabilityQuery>()
            .FindOperational(FacilityCapabilityKind.None, "building:8825").Count == 0,
            "Pending-boundary fixture needs a fresh run without RF25.");
        current = campaign.CaptureSociety();
        var ordinary = current.activeEvents.Where(x =>
            ((ISocietyEventCatalog)catalog).Require(x.definitionId) is not ServiceIncidentDefinitionSO
            and not LifeEventDefinitionSO { emergency: true }).ToArray();
        Require(ordinary.All(x => !x.resolved && string.IsNullOrEmpty(x.selectedChoiceId)
            && (x.guestDelivery?.phase ?? GuestRequestDeliveryPhase.None) == GuestRequestDeliveryPhase.None
            && !(x.guestDelivery?.inputOwnerActive ?? false)),
            "Cannot replace already accepted startup ordinary event for pending-boundary fixture.");
        foreach (var startup in ordinary) current.activeEvents.Remove(startup);
        var guestOccurrence = CreateOccurrence(campaign, guest.StableId, 2, guest);
        current.activeEvents.Add(guestOccurrence);
        campaign.PublishSociety(campaign.PrepareSociety(current));
        publish.Invoke(adapter, null);
        var guestNotice = notices.EventLog.Single(x => x.SourceId == guestOccurrence.instanceId);
        int fulfillIndex = guestNotice.Choices.ToList().FindIndex(x => x.ActionId ==
            V21ContentAlertActionIds.Society(guestOccurrence.instanceId, "fulfill"));
        Require(fulfillIndex >= 0, "Actual guest acceptance action missing.");
        string pendingMoney = JsonUtility.ToJson(ledger.Capture());
        string pendingPhysical = JsonUtility.ToJson(physical.Capture());
        Click("EventAlertButton_" + guestNotice.Id);
        Click("EventChoice_" + (fulfillIndex + 1));
        var pendingGuest = campaign.ActiveSocietyEvents.Single(x => x.instanceId == guestOccurrence.instanceId);
        guestNotice = notices.EventLog.Single(x => x.SourceId == guestOccurrence.instanceId);
        Require(!pendingGuest.resolved && pendingGuest.guestDelivery.phase == GuestRequestDeliveryPhase.AwaitingVenue
            && pendingGuest.terminalActionLabel == "" && pendingGuest.terminalOutcomeNarrative == ""
            && pendingGuest.terminalEffectKinds.Count == 0 && !guestNotice.IsResolved
            && guestNotice.ResultSummary == "" && !notices.IsDismissed(guestNotice)
            && guestNotice.Importance == EventAlertImportance.Medium
            && JsonUtility.ToJson(ledger.Capture()) == pendingMoney
            && JsonUtility.ToJson(physical.Capture()) == pendingPhysical,
            "Accepted-pending without venue produced terminal notice/metadata, hid risk or mutated reward/items.");
        lines.Add("PASS actual sealed-archive acceptance without RF25 remains AwaitingVenue with frozen risk, no terminal result/reward/item mutation; no haul replay");
    }

    private static V20ActiveEventSaveData CreateOccurrence(V20CampaignRuntime campaign, string id, int day,
        V20AuthoredContentSO societyDefinition = null)
    {
        var create = typeof(V20CampaignRuntime).GetMethod("CreateEvent", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(create != null, "Existing occurrence creation contract missing.");
        return (V20ActiveEventSaveData)create.Invoke(campaign,
            new object[] { id, new V20DailyEventContext { AbsoluteDay = day, RunSeed = 39039, Generation = 0 }, 3, Array.Empty<string>(), societyDefinition });
    }

    private void Pause()
    {
        var host = FindFirstObjectByType<GameManager>();
        if (host != null) host.isPause = true;
        if (scale != null) scale.Scale = 0;
    }
    private static void Click(string name)
    {
        var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(x => x != null
            && x.gameObject.scene.isLoaded && x.gameObject.activeInHierarchy && x.name == name);
        Require(button != null && button.IsInteractable()
            && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual UI button unavailable: " + name);
    }
    private static bool HasVisibleText(string expected) => Resources.FindObjectsOfTypeAll<TMPro.TMP_Text>()
        .Any(x => x != null && x.gameObject.scene.isLoaded && x.gameObject.activeInHierarchy
            && x.text != null && x.text.Contains(expected, StringComparison.Ordinal));
    private static void MustReject(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private sealed class CloneDefinitionSource : IGameContentDefinitionSource
    {
        private readonly IGameContentDefinitionSource source;
        private readonly LifeEventDefinitionSO original;
        private readonly LifeEventDefinitionSO clone;
        public CloneDefinitionSource(IGameContentDefinitionSource source, LifeEventDefinitionSO original, LifeEventDefinitionSO clone)
        { this.source = source; this.original = original; this.clone = clone; }
        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject => source.GetAll<T>()
            .Select(x => ReferenceEquals(x, original) ? (T)(ScriptableObject)clone : x).ToArray();
        public T RequireSingle<T>() where T : ScriptableObject => GetAll<T>().Single();
    }
}
#endif
