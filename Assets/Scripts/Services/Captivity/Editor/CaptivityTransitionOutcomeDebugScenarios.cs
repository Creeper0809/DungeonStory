#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static class CaptivityTransitionOutcomeDebugScenarios
{
    private const int FixtureAbsoluteDay = 21;
    private const string HousingAssetPath =
        "Assets/Resources/SO/Building/Captivity/CP01_감방구속대.asset";

    public static bool RunFocused()
    {
        VerifyDescriptorMetricWhitelists();
        VerifyRehabilitationPartialAndCompletion();
        VerifyRehabilitationSavedForwardRetry();
        VerifyReleasePhysicalFailureAndSuccess();
        VerifyReleaseSavedForwardRetry();
        Debug.Log("[D09 Captivity Transition Outcome] PASS");
        return true;
    }

    public static void RunD09FromBatch()
    {
        string reportPath = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_D09_CAPTIVITY_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(reportPath)
            || !Path.IsPathRooted(reportPath))
        {
            throw new InvalidOperationException(
                "D09 batch verification requires an absolute report path.");
        }
        if (File.Exists(reportPath))
            throw new IOException("D09 batch report already exists: " + reportPath);

        Require(RunFocused(), "D09 focused scenario returned false.");
        string directory = Path.GetDirectoryName(reportPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "D09 batch report path has no parent directory.");
        }
        Directory.CreateDirectory(directory);
        string report = string.Join(Environment.NewLine, new[]
        {
            "U0_D09_CAPTIVITY=PASS",
            "rehabilitation=partial,completed-day,food-one,relationship-deltas",
            "release=physical-failure-no-outcome,success",
            "participants=captive,warden,decider,facility,food",
            "restore=rehabilitation-pending,release-pending",
            "delivery=terminal-retry-exactly-once",
            "fullAuditClosure=false",
            "humanApprovalClaimed=false",
            "trainingEligible=false"
        }) + Environment.NewLine;
        using (FileStream stream = new(
                   reportPath,
                   FileMode.CreateNew,
                   FileAccess.Write,
                   FileShare.None))
        using (StreamWriter writer = new(stream))
            writer.Write(report);
        Debug.Log("U0_D09_CAPTIVITY=PASS; report=" + reportPath);
    }

    private static void VerifyDescriptorMetricWhitelists()
    {
        CaptivityRehabilitationOutcomeDescriptor rehabilitation = new();
        CaptivityReleaseOutcomeDescriptor release = new();
        Require(rehabilitation.IsKnownMetric(
                CaptivityTransitionOutcomeIds.ApprovedWork,
                CaptivityTransitionOutcomeIds.WorkUnit)
            && rehabilitation.IsKnownMetric(
                CaptivityTransitionOutcomeIds.TrustAfter,
                CaptivityTransitionOutcomeIds.PointUnit)
            && !rehabilitation.IsKnownMetric(
                CaptivityTransitionOutcomeIds.ApprovedWork,
                CaptivityTransitionOutcomeIds.CountUnit)
            && !rehabilitation.IsKnownMetric(
                new GameplayMetricId("captivity.rehabilitation.unknown"),
                CaptivityTransitionOutcomeIds.WorkUnit),
            "Rehabilitation descriptor accepted an unknown metric or unit.");
        Require(release.IsKnownMetric(
                CaptivityTransitionOutcomeIds.PhysicalInputsReturned,
                CaptivityTransitionOutcomeIds.BooleanUnit)
            && release.IsKnownMetric(
                CaptivityTransitionOutcomeIds.ReleaseRevision,
                CaptivityTransitionOutcomeIds.RevisionUnit)
            && !release.IsKnownMetric(
                CaptivityTransitionOutcomeIds.ReleaseRevision,
                CaptivityTransitionOutcomeIds.BooleanUnit)
            && !release.IsKnownMetric(
                new GameplayMetricId("captivity.release.unknown"),
                CaptivityTransitionOutcomeIds.BooleanUnit),
            "Release descriptor accepted an unknown metric or unit.");
    }

    private static void VerifyRehabilitationPartialAndCompletion()
    {
        using Fixture partial = new("rehab-partial", rehabilitationWork: 5f);
        Require(partial.Runtime.AdvanceRehabilitation(
                partial.State.captiveId,
                partial.Warden,
                3f,
                out string partialStatus),
            "Partial rehabilitation failed: " + partialStatus);
        Require(Mathf.Approximately(
                partial.State.completedRehabilitationWork,
                8f)
            && partial.State.rehabilitationDays == 2
            && partial.Food.Consumed == 0
            && partial.State.rehabilitationOutcomeRevision == 1L,
            "Partial rehabilitation state or food delta drifted.");
        GameplayOutcomeSnapshot partialOutcome = partial.SingleOutcome();
        Require(Metric(partialOutcome,
                    CaptivityTransitionOutcomeIds.CompletedDay.Value) == 0d
            && Metric(partialOutcome,
                    CaptivityTransitionOutcomeIds.WorkBefore.Value) == 5d
            && Metric(partialOutcome,
                    CaptivityTransitionOutcomeIds.WorkAfter.Value) == 8d
            && Metric(partialOutcome,
                    CaptivityTransitionOutcomeIds.FoodConsumed.Value) == 0d,
            "Partial rehabilitation canonical metrics drifted.");
        RequireRehabilitationParticipants(partialOutcome, partial);

        using Fixture completed = new("rehab-complete", rehabilitationWork: 17.5f);
        float trustBefore = completed.State.trust;
        float grudgeBefore = completed.State.grudge;
        float corruptionBefore = completed.State.corruption;
        Require(completed.Runtime.AdvanceRehabilitation(
                completed.State.captiveId,
                completed.Warden,
                1f,
                out string completionStatus),
            "Daily rehabilitation completion failed: " + completionStatus);
        Require(!completed.State.rehabilitationInProgress
            && completed.State.rehabilitationDays == 3
            && completed.State.completedRehabilitationWork == 0f
            && completed.Food.Consumed == 1
            && Mathf.Approximately(
                completed.State.trust,
                trustBefore + MinionIntegrationRules.RehabilitationTrustDelta)
            && Mathf.Approximately(
                completed.State.grudge,
                grudgeBefore + MinionIntegrationRules.RehabilitationGrudgeDelta)
            && Mathf.Approximately(
                completed.State.corruption,
                corruptionBefore
                    + MinionIntegrationRules.RehabilitationCorruptionDelta),
            "Daily rehabilitation did not apply the exact food/day/stat deltas.");
        GameplayOutcomeSnapshot completedOutcome = completed.SingleOutcome();
        Require(Metric(completedOutcome,
                    CaptivityTransitionOutcomeIds.CompletedDay.Value) == 1d
            && Metric(completedOutcome,
                    CaptivityTransitionOutcomeIds.FoodConsumed.Value) == 1d
            && Metric(completedOutcome,
                    CaptivityTransitionOutcomeIds.DaysBefore.Value) == 2d
            && Metric(completedOutcome,
                    CaptivityTransitionOutcomeIds.DaysAfter.Value) == 3d,
            "Completed rehabilitation canonical metrics drifted.");
        RequireRehabilitationParticipants(completedOutcome, completed);
    }

    private static void VerifyRehabilitationSavedForwardRetry()
    {
        using Fixture fixture = new(
            "rehab-retry",
            rehabilitationWork: 17.5f,
            factCommitFailures: 1);
        Require(fixture.Runtime.AdvanceRehabilitation(
                fixture.State.captiveId,
                fixture.Warden,
                1f,
                out _)
            && fixture.State.rehabilitationTerminal.outcomePending
            && fixture.State.rehabilitationOutcomeRevision == 0L
            && fixture.State.rehabilitationTerminal.publicationKey.Length > 0
            && fixture.Food.Consumed == 1
            && fixture.Publisher.RehabilitationCommitCount == 1
            && fixture.OutcomeCount == 0,
            "Rejected rehabilitation result did not remain saved-forward pending.");
        string publicationKey = fixture.State.rehabilitationTerminal.publicationKey;
        CaptiveState restored = RoundTrip(fixture.State);
        fixture.ReplaceState(restored);
        Require(fixture.Runtime.AdvanceRehabilitation(
                restored.captiveId,
                fixture.Warden,
                4f,
                out string retryStatus)
            && !restored.rehabilitationTerminal.outcomePending
            && restored.rehabilitationOutcomeRevision == 1L
            && restored.rehabilitationTerminal.publicationKey == publicationKey
            && fixture.Food.Consumed == 1
            && fixture.Publisher.RehabilitationCommitCount == 1
            && fixture.OutcomeCount == 1,
            "Saved rehabilitation result did not retry exactly once: "
            + retryStatus);
    }

    private static void VerifyReleasePhysicalFailureAndSuccess()
    {
        using Fixture rejected = new(
            "release-physical-failure",
            careFailures: 1,
            captiveStatus: CaptivityStatus.Confined);
        Require(!rejected.Runtime.TryRelease(
                rejected.State.captiveId,
                out string rejectedReason)
            && rejectedReason.Length > 0
            && rejected.State.status == CaptivityStatus.Confined
            && !rejected.State.releaseTerminal.HasOutcome
            && rejected.OutcomeCount == 0
            && rejected.ReleaseDecisionCount == 0,
            "Physical release failure leaked an observer or canonical result.");

        using Fixture success = new(
            "release-success",
            captiveStatus: CaptivityStatus.Confined);
        Require(success.Runtime.TryRelease(
                success.State.captiveId,
                out string releaseFailure)
            && string.IsNullOrEmpty(releaseFailure)
            && success.State.status == CaptivityStatus.Released
            && success.State.releaseOutcomeRevision == 1L
            && !success.State.releaseTerminal.outcomePending
            && !success.State.releaseTerminal.observersPending
            && success.State.releaseTerminal.effectsApplied
            && success.State.releaseTerminal.decisionPublished
            && success.ReleaseDecisionCount == 1
            && !success.Doors.IsCaptive(success.State.captiveId),
            "Successful release did not publish exactly one terminal result: "
            + releaseFailure);
        GameplayOutcomeSnapshot outcome = success.SingleOutcome();
        Require(outcome.outcomeTypeId
                == CaptivityTransitionOutcomeIds.Release.Value
            && outcome.absoluteDay == FixtureAbsoluteDay
            && Metric(outcome,
                    CaptivityTransitionOutcomeIds.CustodyBefore.Value) == 1d
            && Metric(outcome,
                    CaptivityTransitionOutcomeIds.CustodyAfter.Value) == 0d
            && Metric(outcome,
                    CaptivityTransitionOutcomeIds.PhysicalInputsReturned.Value)
                == 1d,
            "Release canonical before/after metrics drifted.");
        RequireReleaseParticipants(outcome, success);
    }

    private static void VerifyReleaseSavedForwardRetry()
    {
        using Fixture fixture = new(
            "release-retry",
            captiveStatus: CaptivityStatus.Confined,
            factCommitFailures: 1);
        Require(fixture.Runtime.TryRelease(
                fixture.State.captiveId,
                out _)
            && fixture.State.status == CaptivityStatus.Released
            && fixture.State.releaseTerminal.outcomePending
            && fixture.State.releaseTerminal.effectsApplied
            && !fixture.State.releaseTerminal.decisionPublished
            && fixture.State.releaseTerminal.publicationKey.Length > 0
            && fixture.ReleaseDecisionCount == 0
            && fixture.Publisher.ReleaseCommitCount == 1
            && fixture.OutcomeCount == 0,
            "Rejected release result did not suppress the decision observer.");
        string publicationKey = fixture.State.releaseTerminal.publicationKey;
        CaptiveState restored = RoundTrip(fixture.State);
        fixture.ReplaceState(restored);
        Require(fixture.Runtime.TryRelease(
                restored.captiveId,
                out string retryFailure)
            && string.IsNullOrEmpty(retryFailure)
            && restored.releaseOutcomeRevision == 1L
            && !restored.releaseTerminal.outcomePending
            && !restored.releaseTerminal.observersPending
            && restored.releaseTerminal.effectsApplied
            && restored.releaseTerminal.decisionPublished
            && restored.releaseTerminal.publicationKey == publicationKey
            && fixture.ReleaseDecisionCount == 1
            && fixture.Publisher.ReleaseCommitCount == 1
            && fixture.OutcomeCount == 1,
            "Saved release did not recover exactly once: " + retryFailure);
    }

    private static double Metric(GameplayOutcomeSnapshot outcome, string id) =>
        outcome.metrics.Single(value => value.metricId == id).value;

    private static void RequireRehabilitationParticipants(
        GameplayOutcomeSnapshot outcome,
        Fixture fixture)
    {
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.CharacterKind,
            fixture.Captive.Identity.PersistentId,
            CaptivityTransitionOutcomeIds.CaptiveRole,
            "rehabilitation captive");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.CharacterKind,
            fixture.Warden.Identity.PersistentId,
            CaptivityTransitionOutcomeIds.WardenRole,
            "rehabilitation warden");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.BuildingKind,
            fixture.Housing.RequirePersistentInstanceId().Value,
            CaptivityTransitionOutcomeIds.FacilityRole,
            "rehabilitation facility");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.StockCategoryKind,
            "food",
            CaptivityTransitionOutcomeIds.FoodRole,
            "rehabilitation food");
        Require(outcome.participants.Count == 4,
            "Rehabilitation outcome contains an unexpected participant.");
    }

    private static void RequireReleaseParticipants(
        GameplayOutcomeSnapshot outcome,
        Fixture fixture)
    {
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.CharacterKind,
            fixture.Captive.Identity.PersistentId,
            CaptivityTransitionOutcomeIds.CaptiveRole,
            "release captive");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.CharacterKind,
            fixture.Owner.Identity.PersistentId,
            CaptivityTransitionOutcomeIds.DeciderRole,
            "release decider");
        RequireParticipant(
            outcome,
            CaptivityTransitionOutcomeIds.BuildingKind,
            fixture.Housing.RequirePersistentInstanceId().Value,
            CaptivityTransitionOutcomeIds.FacilityRole,
            "release facility");
        Require(outcome.participants.Count == 3,
            "Release outcome contains an unexpected participant.");
    }

    private static void RequireParticipant(
        GameplayOutcomeSnapshot outcome,
        GameplayEntityKindId entityKind,
        string entityId,
        GameplayRoleId role,
        string context)
    {
        int count = outcome.participants.Count(value =>
            value.entityKindId == entityKind.Value
            && value.entityId == entityId
            && value.roleId == role.Value
            && value.participationKind == GameplayParticipationKind.Direct);
        Require(count == 1,
            "Missing or duplicate " + context + " participant identity.");
    }

    private static CaptiveState RoundTrip(CaptiveState source)
    {
        CaptivitySaveData payload = new()
        {
            captives = new List<CaptiveState> { source },
            policies = new List<CaptivePolicyData>
            {
                new()
                {
                    policyId = CaptivityPolicyIds.Standard,
                    displayName = "표준 수용"
                }
            }
        };
        CaptivitySaveData restored = JsonUtility.FromJson<CaptivitySaveData>(
            JsonUtility.ToJson(payload));
        DungeonGameRestoreReport report = new();
        CaptivitySaveValidation.Validate(restored, report);
        Require(report.Success,
            "D09 save round trip was rejected: "
            + string.Join(" | ", report.Errors));
        return CaptivitySaveValidation.CreateRestoredCaptive(
            restored.captives.Single());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly CaptivityActorAccess actors;
        private readonly IDisposable decisionSubscription;
        private readonly DomainFactDispatcher transitionFacts;
        private readonly IDisposable rehabilitationObservation;
        private readonly IDisposable releaseObservation;

        internal Fixture(
            string suffix,
            float rehabilitationWork = 0f,
            int careFailures = 0,
            CaptivityStatus captiveStatus = CaptivityStatus.Minion,
            int factCommitFailures = 0)
        {
            Captive = CreateActor(
                "D09 Captive " + suffix,
                "character:d09-captive-" + suffix,
                "가람",
                CharacterRole.Regular,
                CharacterType.NPC);
            Warden = CreateActor(
                "D09 Warden " + suffix,
                "character:d09-warden-" + suffix,
                "도윤",
                CharacterRole.Regular,
                CharacterType.NPC);
            Owner = CreateActor(
                "D09 Owner " + suffix,
                "character:d09-owner-" + suffix,
                "연화",
                CharacterRole.Owner,
                CharacterType.NPC);
            Housing = CreateHousing(suffix);
            State = new CaptiveState
            {
                captiveId = Captive.Identity.PersistentId,
                displayName = "가람",
                speciesTag = "human",
                status = captiveStatus,
                policyId = CaptivityPolicyIds.Standard,
                restrained = captiveStatus == CaptivityStatus.Confined,
                housingBuildingId = captiveStatus == CaptivityStatus.Confined
                    ? Housing.RequirePersistentInstanceId().Value
                    : string.Empty,
                trust = 50f,
                grudge = 30f,
                corruption = 50f,
                rehabilitationDays = 2,
                completedRehabilitationWork = rehabilitationWork,
                rehabilitationInProgress = captiveStatus == CaptivityStatus.Minion,
                reservedWardenId = captiveStatus == CaptivityStatus.Minion
                    ? Warden.Identity.PersistentId
                    : string.Empty,
                rehabilitationFacilityBuildingId =
                    captiveStatus == CaptivityStatus.Minion
                        ? Housing.RequirePersistentInstanceId().Value
                        : string.Empty,
                rehabilitationPosition = Housing.centerPos
            };
            actors = new CaptivityActorAccess(
                new DungeonRuntimeAggregateRootStore(),
                _ => { });
            actors.AddState(State);
            World = new FakeWorldRegistry(
                new[] { Captive, Warden, Owner },
                new[] { Housing });
            Food = new FakeFood(4);
            Care = new FakeCareOwner(careFailures);
            Doors = new FakeDoorSubjects();
            Doors.SetCaptive(State.captiveId, State.IsInCustody);
            Events = new GameEventBus();
            decisionSubscription = Events.Subscribe<PrisonerDecisionEvent>(
                value =>
                {
                    if (value.Prisoner.Equals(new CharacterId(State.captiveId))
                        && value.DecisionId == "release")
                    {
                        ReleaseDecisionCount++;
                    }
                });
            Clock = new FakeClock();
            Calendar = new EditorFixedGameCalendar(
                FixtureAbsoluteDay,
                hour: 0);

            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptivityRehabilitationOutcomeDescriptor(),
                    new CaptivityReleaseOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptivityRehabilitationOutcomeAdapter(),
                    new CaptivityReleaseOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                new GameplayOutcomeBufferLimits(16, 0, 128),
                new GameplayOutcomeRunId("run:d09-" + suffix),
                1L);
            GameplayOutcomeRecorder recorder = new(Ledger, Registry, Events);
            GameplayDomainFactCommitGroup commits = new(recorder, Ledger);
            transitionFacts = new DomainFactDispatcher();
            rehabilitationObservation = transitionFacts.Subscribe(
                "captivity-rehabilitation:d09-fixture",
                new GameplayDomainFactObserver<
                    CaptivityRehabilitationProgressedFact,
                    CaptivityRehabilitationOutcomeReceipt>(
                    CaptivityTransitionFactMapping.Capture,
                    Registry,
                    Ledger,
                    recorder,
                    commits));
            releaseObservation = transitionFacts.Subscribe(
                "captivity-release:d09-fixture",
                new GameplayDomainFactObserver<
                    CaptivityReleasedFact,
                    CaptivityReleaseOutcomeReceipt>(
                    CaptivityTransitionFactMapping.Capture,
                    Registry,
                    Ledger,
                    recorder,
                    commits));
            Publisher = new ConfigurablePublisher(
                transitionFacts,
                factCommitFailures);
            Runtime = CreateRuntime(Publisher, transitionFacts);
        }

        internal CharacterActor Captive { get; }
        internal CharacterActor Warden { get; }
        internal CharacterActor Owner { get; }
        internal BuildableObject Housing { get; }
        internal CaptiveState State { get; private set; }
        internal FakeWorldRegistry World { get; }
        internal FakeFood Food { get; }
        internal FakeCareOwner Care { get; }
        internal FakeDoorSubjects Doors { get; }
        internal GameEventBus Events { get; }
        internal FakeClock Clock { get; }
        internal IGameCalendar Calendar { get; }
        internal GameplayOutcomeRegistry Registry { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal ConfigurablePublisher Publisher { get; }
        internal CaptivityRuntime Runtime { get; }
        internal int ReleaseDecisionCount { get; private set; }
        internal int OutcomeCount => Ledger.GetGlobal(
            OutcomeCursor.FirstPage(32), OutcomeFilter.All).Items.Count;

        internal GameplayOutcomeSnapshot SingleOutcome() => Ledger.GetGlobal(
            OutcomeCursor.FirstPage(32), OutcomeFilter.All).Items.Single().Exact;

        internal void ReplaceState(CaptiveState restored)
        {
            actors.ClearStates();
            actors.AddState(restored);
            State = restored;
        }

        public void Dispose()
        {
            decisionSubscription?.Dispose();
            releaseObservation?.Dispose();
            rehabilitationObservation?.Dispose();
            transitionFacts?.Dispose();
            foreach (UnityEngine.Object value in cleanup.Where(value => value != null))
                UnityEngine.Object.DestroyImmediate(value);
        }

        private CaptivityRuntime CreateRuntime(
            IDomainFactStagingPublisher facts,
            IDomainFactRecoveryPublisher recovery)
        {
#pragma warning disable SYSLIB0050
            CaptivityRuntime runtime = (CaptivityRuntime)
                FormatterServices.GetUninitializedObject(typeof(CaptivityRuntime));
#pragma warning restore SYSLIB0050
            Set(runtime, "actorAccess", actors);
            Set(runtime, "worldRegistry", World);
            Set(runtime, "survivalFood", Food);
            Set(runtime, "transitionFacts", facts);
            Set(runtime, "transitionRecovery", recovery);
            Set(runtime, "gameClock", Clock);
            Set(runtime, "gameCalendar", Calendar);
            Set(runtime, "gameEventBus", Events);
            Set(runtime, "careLaborInputOwner", Care);
            Set(runtime, "doorSubjectRegistry", Doors);
            return runtime;
        }

        private CharacterActor CreateActor(
            string objectName,
            string id,
            string display,
            CharacterRole role,
            CharacterType type)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    type,
                    display,
                    "human",
                    CharacterRole.Regular);
            data.role = role;
            cleanup.Add(data);
            GameObject actorObject = new(objectName);
            cleanup.Add(actorObject);
            actorObject.AddComponent<SpriteRenderer>();
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.RefreshAbilityCache();
            actor.Initialization(data);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(id);
            actor.Identity.SetCharacterType(type);
            actor.characterType = type;
            actor.SetLifecycleState(
                role == CharacterRole.Owner
                    ? CharacterLifecycleState.Active
                    : CharacterLifecycleState.Downed);
            actor.SetAiPaused(role != CharacterRole.Owner);
            return actor;
        }

        private BuildableObject CreateHousing(string suffix)
        {
            BuildingSO data = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                HousingAssetPath);
            Require(data?.GetCaptiveHousingAbility()?.IsValid == true,
                "Authored captive housing asset is unavailable.");
            GameObject buildingObject = new("D09 Housing " + suffix);
            cleanup.Add(buildingObject);
            BuildableObject building =
                buildingObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.RestorePersistentIdentity(new BuildingInstanceId(
                "building:d09-housing-" + suffix));
            building.Initialization(data, new Vector2Int(7, 9));
            return building;
        }

        private static void Set(object target, string fieldName, object value)
        {
            FieldInfo field = typeof(CaptivityRuntime).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(
                    typeof(CaptivityRuntime).FullName,
                    fieldName);
            field.SetValue(target, value);
        }
    }

    private sealed class ConfigurablePublisher : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private int failures;

        internal ConfigurablePublisher(
            IDomainFactStagingPublisher inner,
            int failures)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.failures = Math.Max(0, failures);
        }

        internal int RehabilitationCommitCount { get; private set; }
        internal int ReleaseCommitCount { get; private set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            TransitionFactKind kind = fact switch
            {
                CaptivityRehabilitationProgressedFact _ =>
                    TransitionFactKind.Rehabilitation,
                CaptivityReleasedFact _ => TransitionFactKind.Release,
                _ => TransitionFactKind.None
            };
            if (kind == TransitionFactKind.None)
            {
                stage = null;
                failureReason = "d09-unexpected-transition-fact";
                return false;
            }
            if (!inner.TryStage(fact, out stage, out failureReason))
                return false;
            stage = new ConfigurableStage(this, stage, kind);
            return true;
        }

        private sealed class ConfigurableStage : IDomainFactStage
        {
            private readonly ConfigurablePublisher owner;
            private readonly IDomainFactStage inner;
            private readonly TransitionFactKind kind;

            internal ConfigurableStage(
                ConfigurablePublisher owner,
                IDomainFactStage inner,
                TransitionFactKind kind)
            {
                this.owner = owner;
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
                this.kind = kind;
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;

            public DomainFactDelivery Commit()
            {
                if (kind == TransitionFactKind.Rehabilitation)
                    owner.RehabilitationCommitCount++;
                else
                    owner.ReleaseCommitCount++;
                if (owner.failures > 0)
                {
                    owner.failures--;
                    inner.Cancel();
                    return DomainFactDelivery.Reject(
                        "d09-injected-commit-rejection");
                }
                return inner.Commit();
            }

            public void Cancel() => inner.Cancel();
        }

        private enum TransitionFactKind
        {
            None,
            Rehabilitation,
            Release
        }
    }

    private sealed class FakeFood : ISurvivalFoodCommand
    {
        private int available;
        internal FakeFood(int available) => this.available = available;
        internal int Consumed { get; private set; }
        public bool TryApplySurvivalWork(IBuildingVisitorPort actor,
            BuildableObject building, WorkTypeId workTypeId, out int amount,
            out DomainFailure failure)
        {
            amount = 0;
            failure = DomainFailure.None;
            return false;
        }
        public int TryConsumeStoredStock(StockCategory category, int amount)
        {
            int consumed = Math.Min(Math.Max(0, amount), available);
            available -= consumed;
            Consumed += consumed;
            return consumed;
        }
    }

    private sealed class FakeCareOwner : ICaptivityCareLaborInputOwnerRuntime
    {
        private int failures;
        internal FakeCareOwner(int failures) => this.failures = failures;
        public bool TryReconcileLive(IReadOnlyList<CaptiveState> states,
            out string failureReason)
        {
            if (failures-- > 0)
            {
                failureReason = "d09-injected-physical-cleanup-failure";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }
        public bool TryReconcileRestore(IReadOnlyList<CaptiveState> states,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FakeDoorSubjects : IDoorAccessSubjectRegistry
    {
        private readonly HashSet<string> captives = new(StringComparer.Ordinal);
        internal bool IsCaptive(string id) => captives.Contains(id);
        public void SetCaptive(string persistentId, bool captive)
        {
            if (captive) captives.Add(persistentId);
            else captives.Remove(persistentId);
        }
        public void SetCapturedWildlife(string wildlifeId, bool captured) { }
        public void ReplaceCaptiveSubjects(IEnumerable<string> persistentIds)
        {
            captives.Clear();
            foreach (string id in persistentIds ?? Array.Empty<string>())
                captives.Add(id);
        }
        public void ReplaceCapturedWildlifeSubjects(
            IEnumerable<string> wildlifeIds) { }
    }

    private sealed class FakeClock : IGameClock
    {
        public float DeltaTime => 0.02f;
        public float Time => GameCalendarRules.SecondsPerDay * 21f;
        public int FrameCount => 120;
        public bool IsPaused => false;
    }

    private sealed class FakeWorldRegistry : ICharacterAiWorldRegistry
    {
        private readonly List<CharacterActor> characters;
        private readonly List<BuildableObject> buildings;
        internal FakeWorldRegistry(IEnumerable<CharacterActor> characters,
            IEnumerable<BuildableObject> buildings)
        {
            this.characters = characters.ToList();
            this.buildings = buildings.ToList();
        }
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => characters;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters => characters;
        public int WildlifeVersion => 0;
        public IReadOnlyList<WildlifeActor> Wildlife => Array.Empty<WildlifeActor>();
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings;
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses =>
            Array.Empty<IWarehouseFacility>();
        public int RetailVersion => 0;
        public IReadOnlyList<IRetailFacility> RetailFacilities =>
            Array.Empty<IRetailFacility>();
        public int Version => 1;
        public void RegisterCharacter(CharacterActor actor) => characters.Add(actor);
        public void UnregisterCharacter(CharacterActor actor) => characters.Remove(actor);
        public void RegisterCharacterLifetime(CharacterActor actor) { }
        public void UnregisterCharacterLifetime(CharacterActor actor) { }
        public void RegisterWildlife(WildlifeActor actor) { }
        public void UnregisterWildlife(WildlifeActor actor) { }
        public void RegisterBuilding(BuildableObject building) => buildings.Add(building);
        public void UnregisterBuilding(BuildableObject building) => buildings.Remove(building);
        public int ReleaseTransientBuildingOwnership(
            IBuildingVisitorPort visitor, string reason) => 0;
        public int GetTransientBuildingOwnershipCount(CharacterId characterId) => 0;
        public void RegisterWarehouse(IWarehouseFacility warehouse) { }
        public void UnregisterWarehouse(IWarehouseFacility warehouse) { }
        public void SetGrid(Grid grid) { }
        public bool TryGetGrid(out Grid grid) { grid = null; return false; }
        public bool TryGetSessionState(out GameSessionState data)
        { data = null; return false; }
        public void Clear() { characters.Clear(); buildings.Clear(); }
    }
}
#endif
