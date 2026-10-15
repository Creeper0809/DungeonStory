#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class VisitorExitOutcomeDebugScenarios
{
    public static void RunVisitorExitObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        Require(Application.isBatchMode && !string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output)
            && Path.GetFullPath(output).StartsWith(qa, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(output),
            "Requires fresh project-local isolated batch evidence.");
        string probe = File.ReadAllText(Path.Combine(Directory.GetParent(Path.GetFullPath(output)).FullName, "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE") && probe.Contains("visible=false")
            && probe.Contains("windowCreatedAndDestroyed=true") && probe.Contains("inJob=true"), "Isolation probe missing.");
        Require(RunAll(true), "Visitor exit observer checks failed.");
        Debug.Log("B91_VISITOR_EXIT_OBSERVER_FOCUSED=PASS");
    }

    public static bool RunAll(bool log = false)
    {
        try
        {
            VerifyNoItemAndDisconnected();
            VerifyTheftRejectionAndRetry(1);
            VerifyTheftRejectionAndRetry(2);
            VerifyCarryCallbackSourceDrift();
            VerifyDepartureRejectionAndAckLoss();
            VerifyTheftRecoveryThenTerminal(false, false);
            VerifyTheftRecoveryThenTerminal(true, false);
            VerifyTheftRecoveryThenTerminal(true, true);
            if (log) Debug.Log("B91_VISITOR_EXIT_RECOVERY=PASS");
            return true;
        }
        catch (Exception exception) { if (log) Debug.LogError(exception); return false; }
    }

    private static void VerifyNoItemAndDisconnected()
    {
        using var f = new TheftFixture("character:b91-empty", 0);
        using var o = new OutcomeFixture("run:b91-empty");
        var population = new FakePopulation(f.Actor, Profile(f.Actor, 2));
        var runtime = Owner(f, population, o.Journal, o.Events);
        Require(!runtime.BeginExit(f.Actor, new string('x', 2049), out _)
            && population.Current(f.Actor).nextVisitorExitSequence == 1
            && population.Current(f.Actor).visitorExitOperation.phase == CharacterVisitorExitPhase.None,
            "Invalid macro consumed the exit owner.");
        Require(runtime.BeginExit(f.Actor, "macro:exit", out var failure), failure);
        string before = JsonUtility.ToJson(f.Items.Capture());
        Require(!runtime.TryStealLooseItemBeforeExit(f.Actor, 0, out var stolen, out failure)
            && stolen == null && failure == "items.theft.no_loose_item"
            && o.Journal.Capture().observations.Count == 0 && o.Rows.Count == 0
            && before == JsonUtility.ToJson(f.Items.Capture()), "No-item attempt mutated or published.");
        Require(runtime.TryFailExit(f.Actor, VisitorDepartureTerminalKind.CaptiveBlocked, "captivity-active")
            && o.Rows.Single().status == GameplayOutcomeStatus.Blocked, "Blocked departure status changed.");
        Validate(o, population);
        using var noObserver = new DomainFactDispatcher();
        using var physical = new TheftFixture("character:b91-disconnected", 1);
        var p = new FakePopulation(physical.Actor, Profile(physical.Actor, 0));
        var journal = new VisitorExitPublicationRuntime(noObserver, noObserver);
        var disconnected = Owner(physical, p, journal, o.Events);
        Require(disconnected.BeginExit(physical.Actor, "macro:exit", out failure), failure);
        Require(!disconnected.TryStealLooseItemBeforeExit(physical.Actor, 0, out _, out _)
            && physical.Items.GetAllStacks().Single().Quantity == 1 && physical.Carry.Capture().items.Count == 0
            && journal.Capture().observations.Count == 0, "Missing observer allowed an unowned transfer.");
    }

    private static void VerifyTheftRejectionAndRetry(int quantity)
    {
        using var f = new TheftFixture("character:b91-theft-" + quantity, quantity);
        using var o = new OutcomeFixture("run:b91-theft-" + quantity);
        var p = new FakePopulation(f.Actor, Profile(f.Actor, 2));
        var runtime = Owner(f, p, o.Journal, o.Events);
        Require(runtime.BeginExit(f.Actor, "macro:exit", out var failure), failure);
        string world = JsonUtility.ToJson(f.Items.Capture()), carry = JsonUtility.ToJson(f.Carry.Capture());
        o.Fault.RejectNext = true;
        Require(!runtime.TryStealLooseItemBeforeExit(f.Actor, 0, out _, out failure)
            && failure.StartsWith("items.theft.observation_rejected:", StringComparison.Ordinal)
            && world == JsonUtility.ToJson(f.Items.Capture()) && carry == JsonUtility.ToJson(f.Carry.Capture())
            && o.Rows.Count == 0 && o.Journal.Capture().observations.Count == 0
            && p.Current(f.Actor).visitorExitOperation.phase == CharacterVisitorExitPhase.ExitPending,
            "Theft rejection failed exact rollback, especially a removed quantity-one stack: " + failure);
        Require(runtime.TryStealLooseItemBeforeExit(f.Actor, 0, out var stolen, out failure), failure);
        var row = o.Journal.Capture().observations.Single();
        var carried = f.Carry.Capture().items.Single();
        Require(stolen.StackId == f.SourceStackId && stolen.Quantity == 1
            && f.Items.GetAllStacks().Sum(value => value.Quantity) == quantity - 1
            && carried.quantity == 1 && carried.sourceStackId == f.SourceStackId
            && carried.carriedStackId == row.theft.carriedStackId
            && carried.ownerOperationId == row.theft.parentOperationId
            && row.theft.sourceQuantityBefore == quantity && row.theft.sourceQuantityAfter == quantity - 1
            && row.publicationKey == FloorItemStolenFactMapping.ResultKey(row.theft).ToString()
            && !runtime.TryStealLooseItemBeforeExit(f.Actor, 0, out _, out _)
            && o.Rows.Count == 1, "Theft exact identity/custody/dedup changed.");
        Validate(o, p);
    }

    private static void VerifyCarryCallbackSourceDrift()
    {
        for (int kind = 0; kind < 3; kind++)
        {
            using var f = new TheftFixture("character:b91-drift-" + kind, 1);
            using var o = new OutcomeFixture("run:b91-drift-" + kind);
            var p = new FakePopulation(f.Actor, Profile(f.Actor, 2));
            var runtime = Owner(f, p, o.Journal, o.Events);
            Require(runtime.BeginExit(f.Actor, "macro:exit", out var failure), failure);
            var source = f.Repository.Records.Single();
            bool changed = false;
            void Drift()
            {
                if (changed) return;
                changed = true;
                ExpectInvalid(() => o.Journal.Capture(), "Save interleaved an in-flight physical transfer.");
                if (kind == 0) source.forbidden = true;
                else if (kind == 1) source.reservedQuantity = 1;
                else source.itemId = "item:changed-by-other-owner";
            }
            f.Carry.Changed += Drift;
            try
            {
                Require(!runtime.TryStealLooseItemBeforeExit(f.Actor, 0, out _, out failure)
                    && failure == "items.theft.source_changed_before_commit" && source.quantity == 1
                    && f.Carry.Capture().items.Count == 0 && o.Rows.Count == 0
                    && o.Journal.Capture().observations.Count == 0,
                    "Carry callback source drift was consumed or published: " + failure);
            }
            finally { f.Carry.Changed -= Drift; }
        }
    }

    private static void VerifyDepartureRejectionAndAckLoss()
    {
        using var f = new TheftFixture("character:b91-depart", 0);
        using var o = new OutcomeFixture("run:b91-depart");
        var p = new FakePopulation(f.Actor, Profile(f.Actor, 4));
        var runtime = Owner(f, p, o.Journal, o.Events);
        var world = new LifetimeWorld(f.Actor);
        using var activity = new VisitorDepartureActivityObserver(o.Events, world);
        activity.Initialize(); activity.Initialize();
        Require(runtime.BeginExit(f.Actor, "macro:exit", out var failure), failure);
        f.Actor.SetLifecycleState(CharacterLifecycleState.ExitingDungeon);
        o.Fault.RejectNext = true;
        Require(!runtime.TryCompleteDeparture(f.Actor, "spawner-pool-handoff", out failure)
            && p.Current(f.Actor).isVisiting && p.Current(f.Actor).visitCount == 4
            && p.Current(f.Actor).visitorExitOperation.phase == CharacterVisitorExitPhase.ExitPending
            && f.Actor.CurrentLifecycleState == CharacterLifecycleState.ExitingDungeon
            && o.Rows.Count == 0 && o.Journal.Capture().observations.Count == 0 && world.Observations == 0,
            "Departure rejection failed rollback: " + failure);
        o.Fault.HideNext = true;
        Require(runtime.TryCompleteDeparture(f.Actor, "spawner-pool-handoff", out failure), failure);
        Require(!p.Current(f.Actor).isVisiting && p.Current(f.Actor).visitCount == 5
            && f.Actor.CurrentLifecycleState == CharacterLifecycleState.Despawned
            && o.Journal.Capture().observations.Single().pending && o.Rows.Count == 1 && world.Observations == 1,
            "Real commit hidden ACK rolled back departure or duplicate activity subscription.");
        Validate(o, p);
        var saved = RoundTrip(o.Journal);
        using var restored = new OutcomeFixture("run:b91-depart-restored");
        restored.RestoreLedger(o.Ledger.CaptureGameplayOutcomes()); restored.Journal.Restore(saved);
        var profiles = DungeonSaveJson.Deserialize<ProfileEnvelope>(
            DungeonCharacterWorldSaveCodec.SerializeFields(new ProfileEnvelope { profiles = p.CaptureProfiles() }));
        p.RestoreProfiles(profiles.profiles);
        var resumed = Owner(f, p, restored.Journal, restored.Events, new EditorFixedGameCalendar(20));
        Require(restored.Journal.RecoverPending(out failure)
            && !resumed.TryCompleteDeparture(f.Actor, "spawner-pool-handoff", out _)
            && p.Current(f.Actor).visitCount == 5 && restored.Rows.Count == 1
            && !restored.Journal.Capture().observations.Single().pending, "Departure recovery repeated mechanics: " + failure);
        activity.Dispose();
        o.Events.Publish(new VisitorDepartureAppliedEvent(f.Actor.Identity.PersistentId, f.Actor.Identity.DisplayName, 1));
        Require(world.Observations == 1, "Disposed activity observer remained subscribed.");
        activity.Initialize();
        o.Events.Publish(new VisitorDepartureAppliedEvent(f.Actor.Identity.PersistentId, f.Actor.Identity.DisplayName, 1));
        Require(world.Observations == 2, "Activity observer failed to rebind.");
        Validate(restored, p);
    }

    private static void VerifyTheftRecoveryThenTerminal(bool depart, bool noCanonicalAck)
    {
        string suffix = depart ? noCanonicalAck ? "delayed" : "depart" : "failed";
        using var f = new TheftFixture("character:b91-restore-" + suffix, 1);
        using var o = new OutcomeFixture("run:b91-restore-" + suffix);
        var p = new FakePopulation(f.Actor, Profile(f.Actor, 6));
        var runtime = Owner(f, p, o.Journal, o.Events);
        Require(runtime.BeginExit(f.Actor, "macro:exit", out var failure), failure);
        o.Fault.HideNext = !noCanonicalAck; o.Fault.DeferNext = noCanonicalAck;
        Require(runtime.TryStealLooseItemBeforeExit(f.Actor, 0, out _, out failure), failure);
        Require(o.Journal.Capture().observations.Single().pending && o.Rows.Count == (noCanonicalAck ? 0 : 1),
            "Unknown ACK fixture did not retain real transfer.");
        // The departure may complete while observation of its child is pending, without duplicating the theft.
        if (depart) Require(runtime.TryCompleteDeparture(f.Actor, "spawner-pool-handoff", out failure), failure);
        else Require(runtime.TryFailExit(f.Actor, VisitorDepartureTerminalKind.DoorPassageFailed, "door-passage-failed"),
            "Partial terminal failed.");
        var pending = RoundTrip(o.Journal);
        Require(pending.observations.Count == 2 && pending.observations.All(row => row.pending)
            && o.Rows.Count == (noCanonicalAck ? 0 : 1), "Parent committed ahead of its pending child.");
        Validate(o, p);
        string physical = JsonUtility.ToJson(f.Items.Capture()), carried = JsonUtility.ToJson(f.Carry.Capture());
        var savedProfiles = DungeonSaveJson.Deserialize<ProfileEnvelope>(
            DungeonCharacterWorldSaveCodec.SerializeFields(new ProfileEnvelope { profiles = p.CaptureProfiles() }));
        var ledger = o.Ledger.CaptureGameplayOutcomes();
        using var restored = new OutcomeFixture("run:b91-new-owner-" + suffix);
        restored.RestoreLedger(ledger); restored.Journal.Restore(pending);
        p.RestoreProfiles(savedProfiles.profiles);
        var resumed = Owner(f, p, restored.Journal, restored.Events, new EditorFixedGameCalendar(30));
        Require(restored.Journal.RecoverPending(out failure) && restored.Journal.RecoverPending(out failure)
            && restored.Rows.Count == 2 && physical == JsonUtility.ToJson(f.Items.Capture())
            && carried == JsonUtility.ToJson(f.Carry.Capture())
            && !resumed.TryStealLooseItemBeforeExit(f.Actor, 0, out _, out _)
            && !resumed.TryCompleteDeparture(f.Actor, "spawner-pool-handoff", out _)
            && restored.Journal.Capture().observations.All(row => !row.pending && (row.theft?.absoluteDay ?? row.departure.absoluteDay) == 3),
            "New owner/ledger journal recovery changed mechanics, day or identity: " + failure);
        var parent = restored.Rows.Single(row => row.producerId == VisitorExitOutcomeIds.DepartureProducerId);
        Require(parent.status == (depart ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.PartiallySucceeded),
            "Partial/success policy changed.");
        Validate(restored, p);
        var canonical = restored.Ledger.CaptureGameplayOutcomes();
        ExpectInvalid(() => VisitorExitFactPreflight.ValidateJournal(new(), canonical), "Missing journal history accepted.");
        var altered = VisitorExitPublicationRuntime.ValidateAndClone(restored.Journal.Capture());
        altered.observations[0].theft.positionX++;
        ExpectInvalid(() => VisitorExitFactPreflight.ValidateJournal(altered, canonical), "Tampered fact accepted.");
        var regressed = p.CaptureProfiles();
        regressed[0].visitorExitOperation = new CharacterVisitorExitOperationSaveData();
        regressed[0].nextVisitorExitSequence = 1;
        ExpectInvalid(() => VisitorExitFactPreflight.ValidateOwners(pending, regressed), "Regressed owner accepted.");
        using var detached = new DomainFactDispatcher();
        var recover = new VisitorExitPublicationRuntime(detached, detached); recover.Restore(pending);
        Require(!recover.RecoverPending(out _), "Unconnected journal recovered.");
        var group = new GameplayDomainFactCommitGroup(restored.Recorder, restored.Ledger);
        var binding = FloorItemStolenFactMapping.CreateBinding(restored.Registry, restored.Ledger, restored.Recorder, group);
        using (var connection = binding.Connect(detached))
            ExpectInvalid(() => { using var duplicate = binding.Connect(detached); }, "Duplicate observer accepted.");
        Require(!recover.RecoverPending(out _), "Disposed observer recovered.");
        using var rebound = binding.Connect(detached);
        using var parentBinding = VisitorDepartureFactMapping.CreateBinding(restored.Registry, restored.Ledger, restored.Recorder, group).Connect(detached);
        Require(recover.RecoverPending(out failure) && restored.Rows.Count == 2
            && physical == JsonUtility.ToJson(f.Items.Capture()) && carried == JsonUtility.ToJson(f.Carry.Capture()),
            "Rebound observer altered mechanics or duplicated native history: " + failure);
    }

    private static VisitorExitPublicationSaveData RoundTrip(VisitorExitPublicationRuntime journal)
    {
        string json = new VisitorExitPublicationSaveSection(journal).Capture();
        VisitorExitPublicationSaveSection.ValidateJson(json);
        var raw = Newtonsoft.Json.Linq.JObject.Parse(json);
        ((Newtonsoft.Json.Linq.JObject)raw["observations"][0]).Remove("publicationKey");
        ExpectInvalid(() => VisitorExitPublicationSaveSection.ValidateJson(raw.ToString()), "Missing raw opaque key accepted.");
        return DungeonSaveJson.Deserialize<VisitorExitPublicationSaveData>(json);
    }
    private static void Validate(OutcomeFixture o, FakePopulation p)
    {
        VisitorExitFactPreflight.ValidateJournal(o.Journal.Capture(), o.Ledger.CaptureGameplayOutcomes());
        VisitorExitFactPreflight.ValidateOwners(o.Journal.Capture(), p.CaptureProfiles());
    }
    private static CharacterVisitorExitOutcomeRuntime Owner(TheftFixture f, FakePopulation p,
        VisitorExitPublicationRuntime journal, IGameEventBus events, IGameCalendar calendar = null) =>
        new(p, f.Equipment, f.Theft, journal, calendar ?? new EditorFixedGameCalendar(3), events);
    private static WorldCharacterProfile Profile(CharacterActor actor, int visitCount) => new()
    {
        persistentId = actor.Identity.PersistentId, characterDataId = actor.Identity.Data.id,
        displayName = actor.Identity.DisplayName, isAlive = true, isVisiting = true,
        settlementStanding = CharacterSettlementStanding.Visitor, visitCount = visitCount
    };
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("B91: " + message); }
    private static void ExpectInvalid(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (Exception error) when (error is ArgumentException or InvalidOperationException) { rejected = true; }
        Require(rejected, message);
    }
    [Serializable] private sealed class ProfileEnvelope { public List<WorldCharacterProfile> profiles = new(); }
    private sealed class LifetimeWorld : ICharacterLifetimeQuery
    {
        private readonly CharacterActor actor;
        internal int Observations;
        internal LifetimeWorld(CharacterActor actor) => this.actor = actor;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters { get { Observations++; return new[] { actor }; } }
    }

    private sealed class OutcomeFixture : IDisposable
    {
        internal readonly GameplayOutcomeRegistry Registry;
        internal readonly GameplayOutcomeLedger Ledger;
        internal readonly GameplayOutcomeRecorder Recorder;
        internal readonly GameEventBus Events = new();
        internal readonly DomainFactDispatcher Facts = new();
        internal readonly FaultingStagePublisher Fault;
        internal readonly VisitorExitPublicationRuntime Journal;
        private readonly IDisposable theft, departure;
        internal OutcomeFixture(string run)
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[] { new FloorTheftOutcomeDescriptor(), new VisitorDepartureOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new FloorTheftOutcomeAdapter(), new VisitorDepartureOutcomeAdapter() });
            Ledger = new GameplayOutcomeLedger(Registry, GameplayOutcomeBufferLimits.Default, new GameplayOutcomeRunId(run), 1L);
            Recorder = new GameplayOutcomeRecorder(Ledger, Registry, Events);
            var group = new GameplayDomainFactCommitGroup(Recorder, Ledger);
            theft = FloorItemStolenFactMapping.CreateBinding(Registry, Ledger, Recorder, group).Connect(Facts);
            departure = VisitorDepartureFactMapping.CreateBinding(Registry, Ledger, Recorder, group).Connect(Facts);
            Fault = new FaultingStagePublisher(Facts);
            Journal = new VisitorExitPublicationRuntime(Fault, Facts);
        }
        internal List<GameplayOutcomeSnapshot> Rows => Ledger.CaptureGameplayOutcomes().exactOutcomes;
        internal void RestoreLedger(GameplayOutcomeLedgerSaveData saved)
        {
            var candidate = Ledger.PrepareGameplayOutcomeRestore(saved);
            Ledger.BeginRestoreCandidate(); Ledger.PublishGameplayOutcomeRestore(candidate);
            Ledger.PublishRestoreCandidate(); Ledger.CompleteRestoreCandidate();
        }
        public void Dispose() { theft.Dispose(); departure.Dispose(); Facts.Dispose(); }
    }

    private sealed class FaultingStagePublisher : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        internal bool RejectNext, HideNext, DeferNext;
        internal FaultingStagePublisher(IDomainFactStagingPublisher inner) => this.inner = inner;
        public bool TryStage<T>(in T fact, out IDomainFactStage stage, out string failure)
        {
            if (!inner.TryStage(fact, out stage, out failure)) return false;
            stage = new Stage(this, stage); return true;
        }
        private sealed class Stage : IDomainFactStage
        {
            private readonly FaultingStagePublisher owner;
            private readonly IDomainFactStage inner;
            internal Stage(FaultingStagePublisher owner, IDomainFactStage inner) { this.owner = owner; this.inner = inner; }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public DomainFactDelivery Commit()
            {
                if (owner.RejectNext) { owner.RejectNext = false; inner.Cancel(); return DomainFactDelivery.Reject("qa:rejected"); }
                if (owner.DeferNext) { owner.DeferNext = false; inner.Cancel(); return DomainFactDelivery.Uncertain("qa:not-yet-published"); }
                var result = inner.Commit();
                if (owner.HideNext) { owner.HideNext = false; Require(result.Completed, "ACK fixture requires real commit."); return DomainFactDelivery.Uncertain("qa:lost-ack"); }
                return result;
            }
            public void Cancel() => inner.Cancel();
        }
    }

    private sealed class TheftFixture : IDisposable
    {
        private readonly GameObject actorObject;
        private readonly CharacterSO characterData;

        internal TheftFixture(string characterId, int sourceQuantity)
        {
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository repository,
                out CombatEquipmentRuntime equipment);
            Repository = repository;
            Equipment = equipment;
            SourceStackId = "stack:" + characterId.Replace(':', '-');
            Items.Restore(new DungeonPhysicalItemSaveData
            {
                version = DungeonPhysicalItemSaveData.CurrentVersion,
                haulingSettings = new ItemHaulingSettingsSnapshot
                {
                    maxCarryMultiplier = 1.5f
                },
                stacks = sourceQuantity <= 0
                    ? new List<WorldItemStackSaveData>()
                    : new List<WorldItemStackSaveData>
                    {
                        new()
                        {
                            stackId = SourceStackId,
                            itemId = "item:rich",
                            quantity = sourceQuantity,
                            state = WorldItemStackState.Loose,
                            gridX = 0,
                            gridY = 0
                        }
                    }
            });
            Items.Start();

            actorObject = new GameObject("A03 Visitor Exit Fixture");
            Actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<CharacterAiMemoryRuntime>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.EnsureRuntimeState();
            characterData = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.Customer,
                    "A03 방문객",
                    "Adventurer");
            Actor.Initialization(characterData);
            Actor.Identity.SetPersistentId(characterId);
            Actor.characterType = CharacterType.Customer;
            Actor.Identity.SetCharacterType(CharacterType.Customer);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            Carry = CharacterCarryInventory.Ensure(Actor)
                ?? actorObject.AddComponent<CharacterCarryInventory>();
            Carry.Configure(
                Items.CatalogProvider,
                Items.MassQuery,
                Items.HaulingSettingsProvider,
                new CharacterCarryInventoryRegistry());
            var query = new WorldItemQueryService(
                Items.CatalogProvider,
                Items.MassQuery,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            Theft = new WorldItemTheftService(
                new NoGridProvider(),
                Items.CatalogProvider,
                Items.HaulingSettingsProvider,
                new UnityGameClock(),
                Repository,
                query,
                EditorNullItemMarkerPresenter.Instance);
        }

        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal CombatEquipmentRuntime Equipment { get; }
        internal CharacterActor Actor { get; }
        internal CharacterCarryInventory Carry { get; }
        internal WorldItemTheftService Theft { get; }
        internal string SourceStackId { get; }

        public void Dispose()
        {
            Items?.Dispose();
            if (characterData != null)
                UnityEngine.Object.DestroyImmediate(characterData);
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    private sealed class FakePopulation : ICharacterPopulationService
    {
        private readonly CharacterActor actor;
        private readonly List<WorldCharacterProfile> profiles = new();
        private bool bound;

        internal FakePopulation(CharacterActor actor, WorldCharacterProfile profile)
        {
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
            profiles.Add(profile ?? throw new ArgumentNullException(nameof(profile)));
            bound = true;
        }

        public IReadOnlyList<WorldCharacterProfile> Profiles => profiles;
        public WorldCharacterProfile AcquireVisitor(
            CharacterSO characterData,
            IEnumerable<string> unavailableProfileIds = null) => null;
        public bool TryCreateRecruitCandidate(
            out WorldCharacterProfile profile,
            out CharacterSO sourceData)
        {
            profile = null;
            sourceData = null;
            return false;
        }
        public void BindActor(WorldCharacterProfile profile, CharacterActor value)
        {
            Require(value == actor, "Fake population received an unknown actor.");
            bound = true;
        }
        public void RefreshProfile(CharacterActor value)
        {
        }
        public void ReleaseVisitor(CharacterActor value)
        {
            WorldCharacterProfile profile = Current(value);
            profile.isVisiting = false;
            if (!profile.isStaff) profile.visitCount++;
            bound = false;
        }
        public void PromoteToStaff(CharacterActor value)
        {
            WorldCharacterProfile profile = Current(value);
            profile.isStaff = true;
            profile.isVisiting = false;
        }
        public void PromoteToMinion(CharacterActor value)
        {
            WorldCharacterProfile profile = Current(value);
            profile.isStaff = true;
            profile.isVisiting = false;
            profile.settlementStanding = CharacterSettlementStanding.Minion;
        }
        public CharacterSettlementStanding SetSettlementStanding(
            CharacterActor value,
            CharacterSettlementStanding standing)
        {
            WorldCharacterProfile profile = Current(value);
            CharacterSettlementStanding previous = profile.settlementStanding;
            profile.settlementStanding = standing;
            return previous;
        }
        public void RestoreSettlementStanding(
            CharacterActor value,
            CharacterSettlementStanding standing) =>
            Current(value).settlementStanding = standing;
        public CharacterSettlementStandingTransaction BeginSettlementStandingTransition(
            CharacterActor value,
            CharacterSettlementStanding standing) => default;
        public void RollbackSettlementStandingTransition(
            CharacterSettlementStandingTransaction transaction)
        {
        }
        public void CompleteSettlementStandingTransition(
            CharacterSettlementStandingTransaction transaction)
        {
        }
        public bool TryGetProfile(
            CharacterActor value,
            out WorldCharacterProfile profile)
        {
            profile = null;
            if (!bound || value != actor) return false;
            profile = profiles.FirstOrDefault(candidate => candidate != null
                && candidate.persistentId == actor.Identity.PersistentId);
            return profile != null;
        }
        public List<WorldCharacterProfile> CaptureProfiles() => profiles
            .Where(value => value != null)
            .Select(value => value.Clone())
            .ToList();
        public CharacterPopulationRestoreCandidate BuildRestoreCandidate(
            IEnumerable<WorldCharacterProfile> values) =>
            throw new NotSupportedException();
        public CharacterPopulationRestoreTransaction ApplyRestoreCandidate(
            CharacterPopulationRestoreCandidate candidate) =>
            throw new NotSupportedException();
        public void RollbackRestore(CharacterPopulationRestoreTransaction transaction) =>
            throw new NotSupportedException();
        public void CompleteRestore(CharacterPopulationRestoreTransaction transaction) =>
            throw new NotSupportedException();
        public void RestoreProfiles(IEnumerable<WorldCharacterProfile> values)
        {
            profiles.Clear();
            profiles.AddRange((values ?? Array.Empty<WorldCharacterProfile>())
                .Where(value => value != null)
                .Select(value => value.Clone()));
            bound = profiles.Any(value => value.persistentId
                == actor.Identity.PersistentId);
        }

        internal WorldCharacterProfile Current(CharacterActor value)
        {
            Require(value == actor, "Fake population received an unknown actor.");
            WorldCharacterProfile profile = profiles.SingleOrDefault(candidate =>
                candidate != null
                && candidate.persistentId == actor.Identity.PersistentId);
            return profile ?? throw new InvalidOperationException(
                "A03 fake population lost its visitor profile.");
        }
    }

    private sealed class NoGridProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }
        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
    }
}
#endif
