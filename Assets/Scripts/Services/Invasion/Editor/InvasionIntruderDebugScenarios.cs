using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

public static class InvasionIntruderDebugScenarios
{
    private const string CampaignPublicationCheckpoint = "campaign";
    private const string IntruderPublicationCheckpoint = "intruders";
    private const string OwnerEvacuationPublicationCheckpoint =
        "owner-evacuation";
    private const string EngagementPublicationCheckpoint = "engagements";
    private const string EngagementRetiredCompletionCheckpoint =
        "engagements-retired";
    private const string OwnerEvacuationRetiredCompletionCheckpoint =
        "owner-evacuation-retired";
    private const string IntruderActivatedCompletionCheckpoint =
        "intruders-activated";
    private const string OwnerEvacuationActivatedCompletionCheckpoint =
        "owner-evacuation-activated";
    private const string EngagementActivatedCompletionCheckpoint =
        "engagements-activated";

    private static IInvasionIntruderPatternDefinitionCatalog CreatePatternCatalog()
    {
        return new AuthoredGameplayCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
    }

    [MenuItem("DungeonStory/Debug/Invasion/Run P1 Intruder Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 intruder scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("침입자 에셋", VerifyIntruderAsset, errors);
        RunScenario("탐색성과 목표 보정", VerifyExplorationBias, errors);
        RunScenario("패턴별 경로와 시설 우선순위", VerifyPatternRouting, errors);
        RunScenario("패턴별 근접 파손 대상 엄수", VerifyPatternDamagePreference, errors);
        RunScenario("구조물 돌파 경로와 내구도", VerifyStructuralBreachPlanning, errors);
        RunScenario("침입 위험 지식과 설정 마이그레이션", VerifyRaidAwarenessBuildingIds, errors);
        RunScenario("침입 배속 반응과 수동 우선권", VerifyDefenseTimeResponses, errors);
        RunScenario("시설 파손 보조 목표", VerifyFacilityDamage, errors);
        RunScenario("최종 교전과 런 종료", VerifyFinalCombatEndsRun, errors);
        RunScenario("Regular and boss owner damage tuning", VerifyOwnerDamageTuning, errors);
        RunScenario("Final invasion withdraws stale regular intruders", VerifyFinalInvasionWithdrawal, errors);
        RunScenario("Final defense rally uses the shared entrance floor", VerifyFinalDefenseRallyPlan, errors);
        RunScenario("Intruder factory failure cleanup", VerifyFactoryFailureCleanup, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 intruder scenarios passed.");
        }

        return true;
    }

    public static bool RunV5SaveValidationContracts()
    {
        try
        {
            DungeonInvasionSaveData baseline = CaptureLiveInvasionState();
            IInvasionIntruderPatternDefinitionCatalog patterns =
                CreatePatternCatalog();

            DungeonGameRestoreReport valid = new DungeonGameRestoreReport();
            InvasionSaveValidation.Validate(baseline, patterns, valid);
            if (!valid.Success)
            {
                Debug.LogError(
                    "INVASION_V5_VALIDATION fresh capture rejected: "
                    + string.Join(" | ", valid.Errors));
                return false;
            }

            DungeonInvasionSaveData duplicate = Clone(baseline);
            duplicate.responsePolicies.policies.Add(
                duplicate.responsePolicies.policies[0].Clone());
            DungeonGameRestoreReport duplicateReport =
                new DungeonGameRestoreReport();
            InvasionSaveValidation.Validate(duplicate, patterns, duplicateReport);

            DungeonInvasionSaveData nan = Clone(baseline);
            nan.threat.currentThreat = float.NaN;
            DungeonGameRestoreReport nanReport = new DungeonGameRestoreReport();
            InvasionSaveValidation.Validate(nan, patterns, nanReport);

            DungeonInvasionSaveData broken = Clone(baseline);
            broken.engagements.engagements.Add(new DefenseEngagementSaveData
            {
                id = "defense-engagement:1",
                intruderId = "missing-intruder",
                leadGuardId = "missing-guard",
                state = DefenseEngagementState.Engaged
            });
            DungeonGameRestoreReport brokenReport =
                new DungeonGameRestoreReport();
            InvasionSaveValidation.Validate(broken, patterns, brokenReport);

            DungeonInvasionSaveData legacyV4 = Clone(baseline);
            legacyV4.version = 4;
            DungeonGameRestoreReport legacyV4Report =
                new DungeonGameRestoreReport();
            InvasionSaveValidation.Validate(
                legacyV4,
                patterns,
                legacyV4Report);

            bool passed = !duplicateReport.Success
                && !nanReport.Success
                && !brokenReport.Success
                && !legacyV4Report.Success
                && VerifyDamagedFacilityIdCodecRoundTrip();
            if (passed)
            {
                Debug.Log(
                    $"INVASION_V5_VALIDATION=PASS valid={valid.Errors.Count} "
                    + $"duplicateErrors={duplicateReport.Errors.Count} "
                    + $"nanErrors={nanReport.Errors.Count} "
                    + $"brokenErrors={brokenReport.Errors.Count} "
                    + $"legacyV4Errors={legacyV4Report.Errors.Count}");
            }
            else
            {
                Debug.LogError(
                    "INVASION_V5_VALIDATION expected corrupt or V4 payload rejection failed.");
            }
            return passed;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    private static bool VerifyDamagedFacilityIdCodecRoundTrip()
    {
        BuildingInstanceId[] sourceIds =
        {
            new BuildingInstanceId("building:debug-z"),
            new BuildingInstanceId("building:debug-a")
        };
        InvasionIntruderPersistenceState source =
            CreateDamageCodecState(sourceIds);
        MethodInfo encoder = typeof(InvasionSaveRuntimeAdapter).GetMethod(
            "ToIntruderSaveData",
            BindingFlags.Static | BindingFlags.NonPublic);
        if (encoder?.Invoke(null, new object[] { source })
            is not DungeonInvasionIntruderSaveData first)
        {
            return false;
        }

        InvasionIntruderPersistenceState restored = CreateDamageCodecState(
            first.damagedFacilityBuildingInstanceIds.Select(
                value => new BuildingInstanceId(value)));
        if (encoder.Invoke(null, new object[] { restored })
            is not DungeonInvasionIntruderSaveData second)
        {
            return false;
        }

        return first.facilityDamageCount == sourceIds.Length
            && first.damagedFacilityBuildingInstanceIds.SequenceEqual(
                new[] { "building:debug-a", "building:debug-z" })
            && string.Equals(
                JsonUtility.ToJson(first),
                JsonUtility.ToJson(second),
                StringComparison.Ordinal);
    }

    private static InvasionIntruderPersistenceState CreateDamageCodecState(
        IEnumerable<BuildingInstanceId> damagedFacilityIds)
    {
        BuildingInstanceId[] ids = damagedFacilityIds.ToArray();
        return new InvasionIntruderPersistenceState(
            2001,
            Vector3.zero,
            Vector2Int.zero,
            InvasionIntruderState.Engaged,
            2f,
            0.4f,
            ids.Length,
            80f,
            0f,
            50f,
            new Dictionary<CharacterCondition, float>(),
            new InvasionIntruderSettings(),
            Array.Empty<DefenseStatusSnapshot>(),
            "invasion:damage-codec",
            damagedFacilityIds: ids);
    }

    public static bool RunAtomicInvasionRestoreContracts()
    {
        try
        {
            DungeonRuntimeLifetimeScope scope =
                Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include);
            if (scope == null)
            {
                throw new InvalidOperationException(
                    "Atomic invasion validation requires a live runtime scope.");
            }

            IInvasionSaveService service =
                scope.Container.Resolve<IInvasionSaveService>();
            InvasionSaveRuntimeAdapter runtimeAdapter =
                scope.Container.Resolve<IInvasionSaveRuntimePort>()
                    as InvasionSaveRuntimeAdapter
                ?? throw new InvalidOperationException(
                    "Atomic invasion validation requires the production runtime adapter.");
            IDungeonRestoreTransactionParticipant participant =
                (IDungeonRestoreTransactionParticipant)service;
            DungeonRuntimeAggregateRootStore rootStore =
                scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
            IsolatedInvasionSaveSection invasionSection = new(service);
            FailOnceSaveSection failOnce = new();
            DungeonSaveSectionRegistry registry = new(
                new IDungeonSaveSection[] { invasionSection, failOnce },
                rootStore,
                new[] { participant });

            List<DungeonSaveSectionEnvelope> baseline = registry.CaptureAll();
            DungeonSaveSectionEnvelope invasionEnvelope = baseline.Single(
                envelope => envelope.sectionId == invasionSection.SectionId);
            DungeonInvasionSaveData payload =
                JsonUtility.FromJson<DungeonInvasionSaveData>(
                    invasionEnvelope.payloadJson);
            if (invasionEnvelope.sectionVersion
                    != DungeonInvasionSaveData.CurrentVersion
                || payload.version != DungeonInvasionSaveData.CurrentVersion)
            {
                Debug.LogError(
                    "INVASION_ATOMIC_RESTORE section/payload version mismatch.");
                return false;
            }

            DungeonGameRestoreReport valid = new();
            if (!registry.RestoreAll(baseline, valid) || !valid.Success)
            {
                Debug.LogError(
                    "INVASION_ATOMIC_RESTORE valid round trip failed: "
                    + string.Join(" | ", valid.Errors));
                return false;
            }

            string stateBeforeFailure = JsonUtility.ToJson(service.Capture());
            int revisionBeforeFailure = rootStore.PublishedRestoreRevision;
            List<DungeonSaveSectionEnvelope> failing = registry.CaptureAll();
            DungeonSaveSectionEnvelope changed = failing.Single(
                envelope => envelope.sectionId == invasionSection.SectionId);
            DungeonInvasionSaveData changedPayload =
                JsonUtility.FromJson<DungeonInvasionSaveData>(changed.payloadJson);
            changedPayload.threat.currentThreat += 17f;
            changed.payloadJson = JsonUtility.ToJson(changedPayload);
            failOnce.FailNextCommit = true;
            DungeonGameRestoreReport failed = new();
            bool failureAccepted = registry.RestoreAll(failing, failed);
            string stateAfterFailure = JsonUtility.ToJson(service.Capture());
            if (failureAccepted
                || failed.Success
                || !string.Equals(
                    stateBeforeFailure,
                    stateAfterFailure,
                    StringComparison.Ordinal)
                || rootStore.PublishedRestoreRevision != revisionBeforeFailure)
            {
                Debug.LogError(
                    "INVASION_ATOMIC_RESTORE failed commit changed live state.");
                return false;
            }

            string[] publicationCheckpoints =
            {
                CampaignPublicationCheckpoint,
                IntruderPublicationCheckpoint,
                OwnerEvacuationPublicationCheckpoint,
                EngagementPublicationCheckpoint
            };
            foreach (string checkpoint in publicationCheckpoints)
            {
                InvasionLiveProjectionSnapshot beforeCheckpoint =
                    CaptureLiveProjection(scope, service, rootStore);
                SetRestoreHook(
                    runtimeAdapter,
                    "RestorePublicationCheckpoint",
                    reached =>
                    {
                        if (string.Equals(
                                reached,
                                checkpoint,
                                StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException(
                                $"Injected invasion publication failure after {reached}.");
                        }
                    });
                DungeonGameRestoreReport checkpointReport = new();
                bool checkpointAccepted;
                try
                {
                    checkpointAccepted = registry.RestoreAll(
                        registry.CaptureAll(),
                        checkpointReport);
                }
                finally
                {
                    SetRestoreHook(
                        runtimeAdapter,
                        "RestorePublicationCheckpoint",
                        null);
                }

                if (checkpointAccepted
                    || checkpointReport.Success
                    || !MatchesLiveProjection(
                        beforeCheckpoint,
                        scope,
                        service,
                        rootStore)
                    || CountDetachedRestoreCandidates()
                        != beforeCheckpoint.DetachedCandidateCount)
                {
                    Debug.LogError(
                        $"INVASION_ATOMIC_RESTORE checkpoint '{checkpoint}' changed a live projection.");
                    return false;
                }
            }

            LateFailingRestoreParticipant lateParticipant = new();
            DungeonSaveSectionRegistry lateFailureRegistry = new(
                new IDungeonSaveSection[] { invasionSection },
                rootStore,
                new IDungeonRestoreTransactionParticipant[]
                {
                    participant,
                    lateParticipant
                });

            List<string> completionOrder = new();
            string[] expectedCompletionOrder =
            {
                EngagementRetiredCompletionCheckpoint,
                OwnerEvacuationRetiredCompletionCheckpoint,
                IntruderActivatedCompletionCheckpoint,
                OwnerEvacuationActivatedCompletionCheckpoint,
                EngagementActivatedCompletionCheckpoint
            };
            SetRestoreHook(
                runtimeAdapter,
                "RestoreCompletionCheckpoint",
                completionOrder.Add);
            DungeonGameRestoreReport orderingReport = new();
            bool orderingAccepted;
            try
            {
                orderingAccepted = registry.RestoreAll(
                    registry.CaptureAll(),
                    orderingReport);
            }
            finally
            {
                SetRestoreHook(
                    runtimeAdapter,
                    "RestoreCompletionCheckpoint",
                    null);
            }
            if (!orderingAccepted
                || !orderingReport.Success
                || !completionOrder.SequenceEqual(expectedCompletionOrder))
            {
                Debug.LogError(
                    "INVASION_ATOMIC_RESTORE completion dependency order was incorrect: "
                    + string.Join(" -> ", completionOrder));
                return false;
            }

            InvasionLiveProjectionSnapshot beforeLateFailure =
                CaptureLiveProjection(scope, service, rootStore);
            List<string> rollbackOrder = new();
            string[] expectedRollbackOrder =
            {
                EngagementPublicationCheckpoint,
                OwnerEvacuationPublicationCheckpoint,
                IntruderPublicationCheckpoint,
                CampaignPublicationCheckpoint
            };
            SetRestoreHook(
                runtimeAdapter,
                "RestoreRollbackCheckpoint",
                checkpoint =>
                {
                    rollbackOrder.Add(checkpoint);
                    if (string.Equals(
                            checkpoint,
                            EngagementPublicationCheckpoint,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Injected engagement rollback checkpoint failure.");
                    }
                });
            lateParticipant.FailNextPublication = true;
            DungeonGameRestoreReport lateFailureReport = new();
            bool lateFailureAccepted;
            try
            {
                lateFailureAccepted = lateFailureRegistry.RestoreAll(
                    lateFailureRegistry.CaptureAll(),
                    lateFailureReport);
            }
            finally
            {
                SetRestoreHook(
                    runtimeAdapter,
                    "RestoreRollbackCheckpoint",
                    null);
            }
            if (lateFailureAccepted
                || lateFailureReport.Success
                || !rollbackOrder.SequenceEqual(expectedRollbackOrder)
                || !MatchesLiveProjection(
                    beforeLateFailure,
                    scope,
                    service,
                    rootStore)
                || CountDetachedRestoreCandidates()
                    != beforeLateFailure.DetachedCandidateCount)
            {
                Debug.LogError(
                    "INVASION_ATOMIC_RESTORE late participant failure changed a live projection.");
                return false;
            }

            List<DungeonSaveSectionEnvelope> legacy = registry.CaptureAll();
            legacy.Single(envelope =>
                envelope.sectionId == invasionSection.SectionId)
                .sectionVersion = DungeonInvasionSaveData.CurrentVersion - 1;
            DungeonGameRestoreReport legacyReport = new();
            if (registry.RestoreAll(legacy, legacyReport)
                || legacyReport.Success)
            {
                Debug.LogError(
                    "INVASION_ATOMIC_RESTORE accepted a legacy section version.");
                return false;
            }

            List<DungeonSaveSectionEnvelope> invalid = registry.CaptureAll();
            DungeonSaveSectionEnvelope invalidEnvelope = invalid.Single(
                envelope => envelope.sectionId == invasionSection.SectionId);
            DungeonInvasionSaveData invalidPayload =
                JsonUtility.FromJson<DungeonInvasionSaveData>(
                    invalidEnvelope.payloadJson);
            invalidPayload.activeIntruders = null;
            invalidEnvelope.payloadJson = JsonUtility.ToJson(invalidPayload);
            string beforeInvalid = JsonUtility.ToJson(service.Capture());
            DungeonGameRestoreReport invalidReport = new();
            if (registry.RestoreAll(invalid, invalidReport)
                || invalidReport.Success
                || !string.Equals(
                    beforeInvalid,
                    JsonUtility.ToJson(service.Capture()),
                    StringComparison.Ordinal)
                || CountDetachedRestoreCandidates() != 0)
            {
                Debug.LogError(
                    "INVASION_ATOMIC_RESTORE accepted or leaked an invalid required collection.");
                return false;
            }

            List<DungeonSaveSectionEnvelope> empty = registry.CaptureAll();
            empty.Single(envelope =>
                    envelope.sectionId == invasionSection.SectionId)
                .payloadJson = string.Empty;
            DungeonGameRestoreReport emptyReport = new();
            if (registry.RestoreAll(empty, emptyReport)
                || emptyReport.Success
                || !string.Equals(
                    beforeInvalid,
                    JsonUtility.ToJson(service.Capture()),
                    StringComparison.Ordinal)
                || CountDetachedRestoreCandidates() != 0)
            {
                Debug.LogError(
                    "INVASION_ATOMIC_RESTORE accepted or leaked an empty payload.");
                return false;
            }

            Debug.Log(
                "INVASION_ATOMIC_RESTORE=PASS "
                + $"rollbackErrors={failed.Errors.Count} "
                + $"checkpointFaults={publicationCheckpoints.Length} "
                + $"completionPhases={completionOrder.Count} "
                + $"rollbackPhases={rollbackOrder.Count} "
                + $"lateFailureErrors={lateFailureReport.Errors.Count} "
                + $"legacyErrors={legacyReport.Errors.Count}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    public static int CountDetachedRestoreCandidates()
    {
        return Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Count(actor => actor != null && actor.IsDetachedRestoreCandidate);
    }

    private static void SetRestoreHook(
        InvasionSaveRuntimeAdapter adapter,
        string propertyName,
        Action<string> hook)
    {
        PropertyInfo property = typeof(InvasionSaveRuntimeAdapter).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Invasion restore hook '{propertyName}' is unavailable.");
        property.SetValue(
            adapter ?? throw new ArgumentNullException(nameof(adapter)),
            hook);
    }

    private static InvasionLiveProjectionSnapshot CaptureLiveProjection(
        DungeonRuntimeLifetimeScope scope,
        IInvasionSaveService service,
        DungeonRuntimeAggregateRootStore rootStore)
    {
        InvasionSceneRuntimeReferences invasion =
            scope.Container.Resolve<InvasionSceneRuntimeReferences>();
        IInvasionCampaignRuntime campaign =
            scope.Container.Resolve<IInvasionCampaignRuntime>();
        IDefenseEngagementRuntime engagements =
            scope.Container.Resolve<IDefenseEngagementRuntime>();
        IInvasionOwnerEvacuationService evacuation =
            scope.Container.Resolve<IInvasionOwnerEvacuationService>();
        return new InvasionLiveProjectionSnapshot(
            JsonUtility.ToJson(service.Capture()),
            rootStore.PublishedRestoreRevision,
            campaign.Branches.ToArray(),
            campaign.SupportSites.ToArray(),
            campaign.Operations.ToArray(),
            invasion.Director.ActiveIntruders.ToArray(),
            engagements.ActiveEngagements.ToArray(),
            evacuation.Owner,
            evacuation.IsEvacuating,
            evacuation.TargetCell,
            evacuation.StatusText,
            CountDetachedRestoreCandidates());
    }

    private static bool MatchesLiveProjection(
        InvasionLiveProjectionSnapshot expected,
        DungeonRuntimeLifetimeScope scope,
        IInvasionSaveService service,
        DungeonRuntimeAggregateRootStore rootStore)
    {
        InvasionLiveProjectionSnapshot actual = CaptureLiveProjection(
            scope,
            service,
            rootStore);
        return string.Equals(
                expected.SerializedState,
                actual.SerializedState,
                StringComparison.Ordinal)
            && expected.RootRevision == actual.RootRevision
            && ReferenceSequenceEqual(expected.Branches, actual.Branches)
            && ReferenceSequenceEqual(expected.SupportSites, actual.SupportSites)
            && ReferenceSequenceEqual(expected.Operations, actual.Operations)
            && ReferenceSequenceEqual(expected.Intruders, actual.Intruders)
            && ReferenceSequenceEqual(expected.Engagements, actual.Engagements)
            && ReferenceEquals(expected.Owner, actual.Owner)
            && expected.OwnerEvacuating == actual.OwnerEvacuating
            && expected.OwnerTarget == actual.OwnerTarget
            && string.Equals(
                expected.OwnerStatus,
                actual.OwnerStatus,
                StringComparison.Ordinal);
    }

    private static bool ReferenceSequenceEqual<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual)
        where T : class
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.Count; index++)
        {
            if (!ReferenceEquals(expected[index], actual[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static DungeonInvasionSaveData CaptureLiveInvasionState()
    {
        DungeonRuntimeLifetimeScope scope =
            Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
        if (scope == null)
        {
            throw new InvalidOperationException(
                "Invasion V5 validation requires a live dungeon runtime scope.");
        }
        return scope.Container.Resolve<IInvasionSaveService>().Capture();
    }

    private static DungeonInvasionSaveData Clone(DungeonInvasionSaveData source)
    {
        return JsonUtility.FromJson<DungeonInvasionSaveData>(
            JsonUtility.ToJson(source));
    }

    private sealed class IsolatedInvasionSaveSection :
        DungeonStrictJsonSaveSection<
            DungeonInvasionSaveData,
            InvasionRestoreCandidate>,
        IDungeonRollbackFreeSaveSection
    {
        private readonly IInvasionSaveService service;

        internal IsolatedInvasionSaveSection(IInvasionSaveService service)
        {
            this.service = service;
        }

        public override string SectionId => "invasion.atomic-contract";
        public override int SectionVersion =>
            DungeonInvasionSaveData.CurrentVersion;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.LateRuntimeState;
        public override IReadOnlyList<string> DependsOn => Array.Empty<string>();

        protected override DungeonInvasionSaveData CapturePayload() =>
            service.Capture();

        protected override InvasionRestoreCandidate BuildRestoreCandidate(
            DungeonInvasionSaveData payload) =>
            service.PrepareRestore(payload);

        protected override void PublishRestoreCandidate(
            InvasionRestoreCandidate candidate) =>
            service.PublishRestore(candidate);
    }

    private sealed class FailOnceSaveSection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        internal bool FailNextCommit;

        public override string SectionId => "zz.invasion-atomic-failure";
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.LateRuntimeState;

        protected override void CommitMarker(
            DungeonGameRestoreReport report)
        {
            if (!FailNextCommit)
            {
                return;
            }
            FailNextCommit = false;
            report.AddError("Injected post-invasion commit failure.");
        }
    }

    private sealed class LateFailingRestoreParticipant :
        IDungeonRestoreTransactionParticipant
    {
        internal bool FailNextPublication;

        public string ParticipantId => "999.test.invasion-late-failure";

        public void BeginRestoreCandidate()
        {
        }

        public void PublishRestoreCandidate()
        {
            if (!FailNextPublication)
            {
                return;
            }

            FailNextPublication = false;
            throw new InvalidOperationException(
                "Injected participant failure after invasion publication.");
        }

        public void RollbackPublishedRestoreCandidate()
        {
        }

        public void DiscardRestoreCandidate()
        {
        }
    }

    private sealed class InvasionLiveProjectionSnapshot
    {
        internal InvasionLiveProjectionSnapshot(
            string serializedState,
            int rootRevision,
            IReadOnlyList<HumanInvasionBranchState> branches,
            IReadOnlyList<HumanSupportSiteState> supportSites,
            IReadOnlyList<ScheduledInvasionOperationState> operations,
            IReadOnlyList<InvasionIntruderRuntime> intruders,
            IReadOnlyList<DefenseEngagement> engagements,
            CharacterActor owner,
            bool ownerEvacuating,
            Vector2Int ownerTarget,
            string ownerStatus,
            int detachedCandidateCount)
        {
            SerializedState = serializedState;
            RootRevision = rootRevision;
            Branches = branches;
            SupportSites = supportSites;
            Operations = operations;
            Intruders = intruders;
            Engagements = engagements;
            Owner = owner;
            OwnerEvacuating = ownerEvacuating;
            OwnerTarget = ownerTarget;
            OwnerStatus = ownerStatus;
            DetachedCandidateCount = detachedCandidateCount;
        }

        internal string SerializedState { get; }
        internal int RootRevision { get; }
        internal IReadOnlyList<HumanInvasionBranchState> Branches { get; }
        internal IReadOnlyList<HumanSupportSiteState> SupportSites { get; }
        internal IReadOnlyList<ScheduledInvasionOperationState> Operations
        {
            get;
        }
        internal IReadOnlyList<InvasionIntruderRuntime> Intruders { get; }
        internal IReadOnlyList<DefenseEngagement> Engagements { get; }
        internal CharacterActor Owner { get; }
        internal bool OwnerEvacuating { get; }
        internal Vector2Int OwnerTarget { get; }
        internal string OwnerStatus { get; }
        internal int DetachedCandidateCount { get; }
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyIntruderAsset()
    {
        CharacterSO intruder = LoadIntruder();
        return intruder != null
            && intruder.characterType == CharacterType.Intruder
            && intruder.role == CharacterRole.Regular
            && intruder.id == 2001
            && intruder.characterSprite != null
            && intruder.moveSpeed > 0f;
    }

    private static bool VerifyExplorationBias()
    {
        Grid grid = new Grid(8, 1);
        for (int x = 0; x < grid.width; x++)
        {
            AddHallway(grid, new Vector2Int(x, 0));
        }

        Vector2Int start = new Vector2Int(0, 0);
        Vector2Int ownerPosition = new Vector2Int(7, 0);
        IGridPathSearchBroker pathSearchBroker = CreatePathSearchBroker();
        IRandomStream pathRandom =
            new RandomStreamProvider(71).Get("exploration-bias");
        IInvasionIntruderPatternDefinitionCatalog patterns = CreatePatternCatalog();
        Queue<GridMoveStep> earlyPath = InvasionIntruderPlanner.GetNextPath(
            grid,
            start,
            ownerPosition,
            0f,
            pathSearchBroker,
            pathRandom,
            patterns.Default,
            out bool earlyDirect,
            out _);
        Queue<GridMoveStep> latePath = InvasionIntruderPlanner.GetNextPath(
            grid,
            start,
            ownerPosition,
            1f,
            pathSearchBroker,
            pathRandom,
            patterns.Default,
            out bool lateDirect,
            out _);

        bool earlyExplores = !earlyDirect
            && earlyPath.Count > 0
            && earlyPath.Last().To != ownerPosition;
        bool lateTargetsOwner = lateDirect
            && latePath.Count > 0
            && latePath.Last().To == ownerPosition;

        return earlyExplores && lateTargetsOwner;
    }

    private static bool VerifyFactoryFailureCleanup()
    {
        foreach (bool detached in new[] { false, true })
        {
            foreach (bool prefabless in new[] { false, true })
            {
                TrackingCharacterSpawnObjectFactory characterFactory =
                    new TrackingCharacterSpawnObjectFactory();
                InvasionIntruderRuntimeFactory factory = CreateCleanupTestFactory(
                    prefabless
                        ? (ICharacterVisualRootFactory)new ThrowingVisualRootFactory()
                        : new CharacterVisualRootFactory(),
                    characterFactory);
                GameObject prefab = prefabless
                    ? null
                    : new GameObject("Failed Invasion Intruder Prefab");
                prefab?.SetActive(false);
                try
                {
                    try
                    {
                        if (detached)
                        {
                            factory.CreateDetached(prefab, Vector3.zero);
                        }
                        else
                        {
                            factory.Create(prefab, Vector3.zero);
                        }
                        return false;
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    if (characterFactory.DestroyCalls != 1
                        || characterFactory.LastCreated != null)
                    {
                        return false;
                    }
                }
                finally
                {
                    if (prefab != null)
                    {
                        Object.DestroyImmediate(prefab);
                    }
                }
            }

            TrackingCharacterSpawnObjectFactory childCleanupFactory =
                new TrackingCharacterSpawnObjectFactory
                {
                    ThrowDuringCreate = true
                };
            InvasionIntruderRuntimeFactory childCleanupSubject =
                CreateCleanupTestFactory(
                    new CharacterVisualRootFactory(),
                    childCleanupFactory);
            GameObject childCleanupPrefab = new GameObject(
                "Child Factory Cleanup Invasion Intruder Prefab");
            childCleanupPrefab.SetActive(false);
            try
            {
                try
                {
                    if (detached)
                    {
                        childCleanupSubject.CreateDetached(
                            childCleanupPrefab,
                            Vector3.zero);
                    }
                    else
                    {
                        childCleanupSubject.Create(
                            childCleanupPrefab,
                            Vector3.zero);
                    }
                    return false;
                }
                catch (InvalidOperationException)
                {
                }

                if (childCleanupFactory.DestroyCalls != 1
                    || childCleanupFactory.LastCreated != null)
                {
                    return false;
                }
            }
            finally
            {
                Object.DestroyImmediate(childCleanupPrefab);
            }
        }

        return true;
    }

    private static InvasionIntruderRuntimeFactory CreateCleanupTestFactory(
        ICharacterVisualRootFactory visualRootFactory,
        ICharacterSpawnObjectFactory characterFactory)
    {
        return new InvasionIntruderRuntimeFactory(
            visualRootFactory,
            characterFactory,
            InterfaceStub<IDefenseEngagementRuntime>(),
            InterfaceStub<IDefenseBreachPlanner>(),
            InterfaceStub<IBuildingStructuralIntegrityRuntime>(),
            InterfaceStub<IDefenseRaidAwarenessRuntime>(),
            InterfaceStub<IDefenseFacilityNetworkRuntime>(),
            InterfaceStub<IInvasionIntruderPatternDefinitionCatalog>());
    }

    private static T InterfaceStub<T>() where T : class
    {
        return DispatchProxy.Create<T, PassiveInterfaceProxy>();
    }

    private static bool VerifyPatternRouting()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(14);
        BuildableObject food = world.Place("D01_간이화덕", new Vector2Int(3, 0));
        BuildableObject research = world.Place("Q01_연구책상", new Vector2Int(7, 0));
        BuildableObject defense = world.Place("P1_SpikeTrap", new Vector2Int(10, 0));
        Vector2Int start = Vector2Int.zero;
        Vector2Int ownerPosition = new Vector2Int(13, 0);
        IGridPathSearchBroker pathSearchBroker = CreatePathSearchBroker();
        IRandomStream pathRandom =
            new RandomStreamProvider(83).Get("pattern-routing");
        IInvasionIntruderPatternDefinitionCatalog patterns = CreatePatternCatalog();

        Queue<GridMoveStep> breakerPath = InvasionIntruderPlanner.GetNextPath(
            world.Grid,
            start,
            ownerPosition,
            0f,
            pathSearchBroker,
            pathRandom,
            patterns.Get(InvasionIntruderPatternIds.Breaker),
            out bool breakerDirect,
            out BuildableObject breakerTarget);
        Queue<GridMoveStep> plundererPath = InvasionIntruderPlanner.GetNextPath(
            world.Grid,
            start,
            ownerPosition,
            0f,
            pathSearchBroker,
            pathRandom,
            patterns.Get(InvasionIntruderPatternIds.Plunderer),
            out bool plundererDirect,
            out BuildableObject plundererTarget);
        Queue<GridMoveStep> ambusherPath = InvasionIntruderPlanner.GetNextPath(
            world.Grid,
            start,
            ownerPosition,
            0.4f,
            pathSearchBroker,
            pathRandom,
            patterns.Get(InvasionIntruderPatternIds.Ambusher),
            out bool ambusherDirect,
            out BuildableObject ambusherTarget);
        Queue<GridMoveStep> stragglerPath = InvasionIntruderPlanner.GetNextPath(
            world.Grid,
            start,
            ownerPosition,
            0.4f,
            pathSearchBroker,
            pathRandom,
            patterns.Get(InvasionIntruderPatternIds.Straggler),
            out bool stragglerDirect,
            out BuildableObject stragglerTarget);

        BuildableObject expectedValuable = new[] { food, research }
            .OrderByDescending(candidate => candidate.GetConstructionCost())
            .First();
        bool valid = !breakerDirect
            && breakerTarget == defense
            && breakerPath.Count > 0
            && !plundererDirect
            && plundererTarget == expectedValuable
            && plundererPath.Count > 0
            && ambusherDirect
            && ambusherTarget == null
            && ambusherPath.Count > 0
            && ambusherPath.Last().To == ownerPosition
            && !stragglerDirect
            && stragglerTarget == null
            && stragglerPath.Count > 0
            && stragglerPath.Last().To != ownerPosition;
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Pattern routing mismatch: breaker={breakerTarget?.name}:{breakerPath.Count}:{breakerDirect}; "
                + $"plunderer={plundererTarget?.name}/{expectedValuable?.name}:{plundererPath.Count}:{plundererDirect}; "
                + $"ambusher={ambusherTarget?.name}:{ambusherPath.Count}:{ambusherDirect}; "
                + $"straggler={stragglerTarget?.name}:{stragglerPath.Count}:{stragglerDirect}; "
                + $"costs={food.GetConstructionCost()}/{research.GetConstructionCost()}.");
        }

        return true;
    }

    private static bool VerifyFacilityDamage()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(10);
        BuildableObject facility = world.Place("P1_LowFoodShop", new Vector2Int(2, 0));
        BuildableObject secondFacility = world.Place("Q01_연구책상", new Vector2Int(5, 0));
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        InvasionIntruderRuntime runtime = intruder.gameObject.AddComponent<InvasionIntruderRuntime>();
        SetPrivateField(runtime, "intruderActor", CharacterActor.From(intruder));
        runtime.ConfigureContent(CreatePatternCatalog());

        GameEventBus eventBus = new GameEventBus();
        SetPrivateField(runtime, "gameEventBus", eventBus);
        CountingFacilityDamageListener listener = new CountingFacilityDamageListener(eventBus);
        bool damaged = runtime.TryDamageNearbyFacility(world.Grid);
        facility.SetDamaged(false);
        intruder.transform.position = world.Grid.GetWorldPos(new Vector2Int(4, 0));
        bool damagedAgain = runtime.TryDamageNearbyFacility(world.Grid);
        bool valid = damaged
            && !damagedAgain
            && !facility.IsDamaged
            && !secondFacility.IsDamaged
            && runtime.FacilityDamageCount == 1
            && listener.Count == 1
            && listener.LastFacility == facility;

        listener.Dispose();
        return valid;
    }

    private static bool VerifyPatternDamagePreference()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(10);
        BuildableObject facility = world.Place("P1_LowFoodShop", new Vector2Int(2, 0));
        BuildableObject defense = world.Place("P1_SpikeTrap", new Vector2Int(6, 0));

        bool breakerIgnoredFacility = !InvasionFacilityDamageResolver.TryFindDamageTarget(
            world.Grid,
            new Vector2Int(1, 0),
            InvasionIntruderTargetPreference.DefenseFacility,
            null,
            out _);
        bool plundererIgnoredDefense = !InvasionFacilityDamageResolver.TryFindDamageTarget(
            world.Grid,
            new Vector2Int(5, 0),
            InvasionIntruderTargetPreference.ValuableFacility,
            null,
            out _);
        bool plundererFoundFacility = InvasionFacilityDamageResolver.TryFindDamageTarget(
            world.Grid,
            new Vector2Int(1, 0),
            InvasionIntruderTargetPreference.ValuableFacility,
            null,
            out BuildableObject valuableTarget)
            && valuableTarget == facility;
        bool breakerFoundDefense = InvasionFacilityDamageResolver.TryFindDamageTarget(
            world.Grid,
            new Vector2Int(5, 0),
            InvasionIntruderTargetPreference.DefenseFacility,
            null,
            out BuildableObject defenseTarget)
            && defenseTarget == defense;

        return breakerIgnoredFacility
            && plundererIgnoredDefense
            && plundererFoundFacility
            && breakerFoundDefense;
    }

    private static bool VerifyStructuralBreachPlanning()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(9);
        IGridPathSearchBroker pathSearch = CreatePathSearchBroker();
        DefenseBreachPlanner planner = new DefenseBreachPlanner();
        BuildingStructuralIntegrityRuntime integrity =
            new BuildingStructuralIntegrityRuntime();
        Vector2Int start = new Vector2Int(0, 0);
        Vector2Int destination = new Vector2Int(8, 0);

        bool openRouteDoesNotBreach = !planner.TryPlan(
            "open-route",
            world.Grid,
            start,
            destination,
            pathSearch,
            integrity,
            new Dictionary<Vector2Int, float>(),
            0.5f,
            20f,
            out _);

        BuildableObject wall = world.Place(
            "Wall",
            new Vector2Int(4, 0));
        bool planned = planner.TryPlan(
            "breacher-a",
            world.Grid,
            start,
            destination,
            pathSearch,
            integrity,
            new Dictionary<Vector2Int, float>(),
            0.5f,
            20f,
            out DefenseBreachPlan first);
        planner.ReleaseReservation("breacher-a");
        bool plannedAgain = planner.TryPlan(
            "breacher-a",
            world.Grid,
            start,
            destination,
            pathSearch,
            integrity,
            new Dictionary<Vector2Int, float>(),
            0.5f,
            20f,
            out DefenseBreachPlan second);

        if (!integrity.TryGet(
                wall,
                out BuildingStructuralIntegritySnapshot initial))
        {
            return false;
        }

        BuildingStructuralDamageResult damage =
            integrity.ApplyDamage(wall, 170f);
        BuildingStructuralIntegrity state =
            wall.GetComponent<BuildingStructuralIntegrity>();
        string saved = state.CaptureState();
        state.TryRestoreState(
            state.CurrentVersion,
            saved,
            out string restoreError);
        integrity.TryApplyRepairWork(
            wall,
            10f,
            out bool completed,
            out BuildingStructuralIntegritySnapshot repaired);

        return openRouteDoesNotBreach
            && planned
            && plannedAgain
            && first.Target == wall
            && second.Target == wall
            && first.AttackCell == second.AttackCell
            && first.VirtualPath.SequenceEqual(second.VirtualPath)
            && Mathf.Approximately(
                initial.MaxHitPoints,
                BuildingStructuralIntegrityDefaults.WallHitPoints)
            && damage.Applied
            && !damage.Destroyed
            && damage.Snapshot.CrackStage
                == BuildingCrackStage.Cracked
            && string.IsNullOrEmpty(restoreError)
            && !completed
            && repaired.CurrentHitPoints
                > damage.Snapshot.CurrentHitPoints;
    }

    private static bool VerifyRaidAwarenessAndSettings()
    {
        DefenseRaidAwarenessRuntime awareness =
            new DefenseRaidAwarenessRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry);
        const string raidId = "debug:raid";
        DefenseRaidAwarenessSnapshot hidden =
            awareness.GetSnapshot(raidId);
        awareness.IdentifyOperation(raidId, 2);
        awareness.SetExpectedPath(
            raidId,
            new[] { Vector2Int.zero, Vector2Int.right },
            "새 위험 발견");
        DefenseRaidAwarenessSaveData save = awareness.Capture(raidId);
        DefenseRaidAwarenessRuntime restored =
            new DefenseRaidAwarenessRuntime(
                CharacterAiEditorTestDependencies.WorldRegistry);
        restored.Restore(save);
        DefenseRaidAwarenessSnapshot snapshot =
            restored.GetSnapshot(raidId);

        DungeonUserSettingsData settings =
            JsonUtility.FromJson<DungeonUserSettingsData>(
                "{\"version\":3}");
        settings.Normalize();
        return hidden.KnownRisks.Count == 0
            && snapshot.IdentificationStage == 2
            && snapshot.ExpectedPath.Count == 2
            && snapshot.RouteChangeReason == "새 위험 발견"
            && settings.version == DungeonUserSettingsData.CurrentVersion
            && settings.defenseTimeResponse
                == DungeonDefenseTimeResponse.SlowToX1
            && DungeonUserSettingsData.CurrentVersion == 4;
    }

    private static bool VerifyRaidAwarenessBuildingIds()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(10);
        BuildableObject riskBuilding = world.Place(
            "P1_SpikeTrap",
            new Vector2Int(2, 0));
        BuildableObject breachTarget = world.Place(
            "Wall",
            new Vector2Int(6, 0));
        DefenseFacility riskFacility =
            riskBuilding.GetComponent<DefenseFacility>();
        if (riskFacility == null)
        {
            return false;
        }

        FixedBuildingWorldQuery buildingWorld =
            new FixedBuildingWorldQuery(riskBuilding, breachTarget);
        DefenseRaidAwarenessRuntime awareness =
            new DefenseRaidAwarenessRuntime(buildingWorld);
        const string raidId = "debug:raid-building-ids";
        DefenseRaidAwarenessSnapshot hidden = awareness.GetSnapshot(raidId);
        awareness.IdentifyOperation(raidId, 2);
        awareness.RecordObservedFacility(raidId, riskFacility);
        awareness.SetExpectedPath(
            raidId,
            new[] { Vector2Int.zero, Vector2Int.right },
            "새 위험 발견");
        awareness.SetBreachTarget(raidId, breachTarget, "구조물 돌파");

        DefenseRaidAwarenessSaveData save = awareness.Capture(raidId);
        DefenseRaidAwarenessRuntime restored =
            new DefenseRaidAwarenessRuntime(buildingWorld);
        restored.Restore(save);
        DefenseRaidAwarenessSaveData roundTrip = restored.Capture(raidId);
        string stablePayload = JsonUtility.ToJson(roundTrip);
        DefenseRaidAwarenessSnapshot snapshot = restored.GetSnapshot(raidId);

        DefenseRaidAwarenessSaveData unknown = CloneAwareness(roundTrip);
        unknown.breachTargetBuildingInstanceId = "building:missing";
        bool unknownRejected = RestoreAwarenessFailsWithoutMutation(
            restored,
            unknown,
            stablePayload);

        DefenseRaidAwarenessSaveData nonCanonical = CloneAwareness(roundTrip);
        nonCanonical.knownRisks[0].facilityBuildingInstanceId =
            "  " + riskBuilding.RequirePersistentInstanceId().Value;
        bool nonCanonicalRejected = RestoreAwarenessFailsWithoutMutation(
            restored,
            nonCanonical,
            stablePayload);

        return hidden.KnownRisks.Count == 0
            && snapshot.IdentificationStage == 2
            && snapshot.ExpectedPath.Count == 2
            && snapshot.KnownRisks.Count > 0
            && snapshot.BreachTarget == breachTarget
            && snapshot.RouteChangeReason == "구조물 돌파"
            && save.knownRisks.All(risk => string.Equals(
                risk.facilityBuildingInstanceId,
                riskBuilding.RequirePersistentInstanceId().Value,
                StringComparison.Ordinal))
            && string.Equals(
                save.breachTargetBuildingInstanceId,
                breachTarget.RequirePersistentInstanceId().Value,
                StringComparison.Ordinal)
            && string.Equals(
                JsonUtility.ToJson(save),
                stablePayload,
                StringComparison.Ordinal)
            && unknownRejected
            && nonCanonicalRejected;
    }

    private static DefenseRaidAwarenessSaveData CloneAwareness(
        DefenseRaidAwarenessSaveData source)
    {
        return JsonUtility.FromJson<DefenseRaidAwarenessSaveData>(
            JsonUtility.ToJson(source));
    }

    private static bool RestoreAwarenessFailsWithoutMutation(
        DefenseRaidAwarenessRuntime runtime,
        DefenseRaidAwarenessSaveData invalid,
        string expectedPayload)
    {
        try
        {
            runtime.Restore(invalid);
            return false;
        }
        catch (InvalidOperationException)
        {
            return string.Equals(
                JsonUtility.ToJson(runtime.Capture(invalid.raidId)),
                expectedPayload,
                StringComparison.Ordinal);
        }
    }

    private static bool VerifyDefenseTimeResponses()
    {
        bool slowRestores = ExerciseTimeResponse(
                DungeonDefenseTimeResponse.SlowToX1,
                5f,
                manualScale: null,
                expectedAfterTrigger: 1f,
                expectedAfterResolve: 5f);
            bool manualWins = ExerciseTimeResponse(
                DungeonDefenseTimeResponse.SlowToX1,
                5f,
                manualScale: 2f,
                expectedAfterTrigger: 1f,
                expectedAfterResolve: 2f);
            bool criticalPauses = ExerciseTimeResponse(
                DungeonDefenseTimeResponse.PauseOnCritical,
                5f,
                manualScale: 1f,
                expectedAfterTrigger: 0f,
                expectedAfterResolve: 1f);
            bool keepCurrent = ExerciseTimeResponse(
                DungeonDefenseTimeResponse.KeepCurrent,
                5f,
                manualScale: null,
                expectedAfterTrigger: 5f,
                expectedAfterResolve: 5f);
        return slowRestores
            && manualWins
            && criticalPauses
            && keepCurrent
            && new InvasionSaveSection(
                new NoopInvasionSaveService()).SectionVersion
                == DungeonInvasionSaveData.CurrentVersion;
    }

    private static bool ExerciseTimeResponse(
        DungeonDefenseTimeResponse response,
        float initialScale,
        float? manualScale,
        float expectedAfterTrigger,
        float expectedAfterResolve)
    {
        EditorDungeonUserSettingsService settings =
            new EditorDungeonUserSettingsService();
        settings.Update(current => current.defenseTimeResponse = response);
        GameEventBus eventBus = new GameEventBus();
        ScenarioTimeScaleController timeScale =
            new ScenarioTimeScaleController(initialScale);
        DungeonDefenseTimeResponseRuntime runtime =
            new DungeonDefenseTimeResponseRuntime(
                eventBus,
                timeScale,
                settings);
        runtime.Initialize();
        eventBus.Publish(new InvasionDungeonBreachedEvent(
            null,
            null,
            default));
        bool triggerValid = Mathf.Approximately(
            timeScale.Scale,
            expectedAfterTrigger);
        if (manualScale.HasValue)
        {
            timeScale.Scale = manualScale.Value;
            runtime.Tick();
        }

        eventBus.Publish(new InvasionResolvedEvent(true, 0f));
        bool resolveValid = Mathf.Approximately(
            timeScale.Scale,
            expectedAfterResolve);
        runtime.Dispose();
        return triggerValid && resolveValid;
    }

    private static bool VerifyFinalCombatEndsRun()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(10);
        CharacterSO ownerData = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Owners/Owner_Orc.asset");
        if (ownerData == null)
        {
            return false;
        }

        GameObject managerObject = new GameObject("Intruder Final Combat OwnerRunManager");
        world.Track(managerObject);
        OwnerRunManager manager = managerObject.AddComponent<OwnerRunManager>();
        manager.ConstructOwnerRunManager(
            new FixedOwnerCandidateCatalog(ownerData),
            new ScenarioOwnerCharacterFactory(world),
            CharacterAiEditorTestDependencies.GameEvents);
        manager.SelectOwner(ownerData);

        CharacterActor owner = manager.CurrentOwnerActor;
        if (owner == null)
        {
            return false;
        }

        SetPrivateField(
            owner.GetComponent<CharacterStats>(),
            "ownerRunLifecycleService",
            new ScenarioOwnerRunLifecycleService(manager));

        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        InvasionIntruderRuntime runtime = intruder.gameObject.AddComponent<InvasionIntruderRuntime>();
        SetPrivateField(runtime, "intruderActor", CharacterActor.From(intruder));
        SetPrivateField(runtime, "gameEventBus", new GameEventBus());
        SetPrivateField(runtime, "settings", new InvasionIntruderSettings
        {
            finalCombatDamage = owner.MaxHealth + 10f,
            finalCombatWindupSeconds = 0f
        });

        runtime.ApplyFinalCombat(CharacterActor.From(owner));

        bool valid = owner.IsDead
            && manager.IsRunEnded
            && runtime.State == InvasionIntruderState.FinalCombat;
        if (!valid)
        {
            throw new InvalidOperationException(
                $"Final combat mismatch: ownerType={owner.characterType}, "
                + $"ownerRole={owner.Role}, health={owner.CurrentHealth}/{owner.MaxHealth}, "
                + $"dead={owner.IsDead}, runEnded={manager.IsRunEnded}, state={runtime.State}.");
        }

        return true;
    }

    private static bool VerifyOwnerDamageTuning()
    {
        float normal = InvasionOwnerDamageTuning.Resolve(45f, 45f, false, 0f, 0f);
        float boss = InvasionOwnerDamageTuning.Resolve(45f, 45f, true, 0f, 0f);
        float armedNormal = InvasionOwnerDamageTuning.Resolve(45f, 60.75f, false, 0f, 0f);
        float armedBoss = InvasionOwnerDamageTuning.Resolve(45f, 60.75f, true, 0f, 0f);

        return Mathf.Approximately(normal, 10f)
            && Mathf.Approximately(boss, 90f)
            && Mathf.Approximately(armedNormal, 13.5f)
            && Mathf.Approximately(armedBoss, 121.5f);
    }

    private static bool VerifyFinalInvasionWithdrawal()
    {
        using IntruderScenarioWorld world = new IntruderScenarioWorld(10);
        GameObject directorObject = new GameObject("Final Invasion Withdrawal Director");
        world.Track(directorObject);
        InvasionDirectorRuntime director = directorObject.AddComponent<InvasionDirectorRuntime>();
        CharacterActor intruder = world.CreateIntruder(new Vector2Int(1, 0));
        InvasionIntruderRuntime runtime = intruder.gameObject.AddComponent<InvasionIntruderRuntime>();
        SetPrivateField(runtime, "intruderActor", CharacterActor.From(intruder));

        FieldInfo activeField = typeof(InvasionDirectorRuntime).GetField(
            "activeIntruders",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (activeField?.GetValue(director) is not List<InvasionIntruderRuntime> active)
        {
            return false;
        }

        active.Add(runtime);
        int withdrawn = director.WithdrawActiveIntrudersForFinalInvasion();
        return withdrawn == 1
            && director.ActiveIntruders.Count == 0
            && runtime == null;
    }

    public static bool VerifyFinalDefenseRallyPlan()
    {
        Grid grid = new Grid(12, 1);
        for (int x = 0; x < 10; x++)
        {
            AddHallway(grid, new Vector2Int(x, 0));
        }

        Vector2Int entry = Vector2Int.zero;
        Vector2Int owner = new Vector2Int(10, 0);
        bool planned = FinalDefenseRallyPlanner.TryCreate(
            grid,
            entry,
            owner,
            CreatePathSearchBroker(),
            out FinalDefenseRallyPlan plan);
        return planned
            && !grid.IsWalkable(owner)
            && plan.Target == new Vector2Int(9, 0)
            && plan.Target.y == entry.y
            && plan.IntruderSteps.Count == 9
            && plan.OwnerSteps.Count == 1
            && plan.IntruderSteps.All(step => step.IsValid && step.To.y == entry.y)
            && plan.OwnerSteps.All(step => step.IsValid && step.To.y == entry.y);
    }

    private static IGridPathSearchBroker CreatePathSearchBroker()
    {
        GridPathSearchBroker broker = new GridPathSearchBroker(new UnityGameClock(), doorAccessQuery: null, performanceRecorder: null, costPolicy: null);
        broker.BeginFrame(128, enforceBudget: false);
        return broker;
    }

    private static CharacterSO LoadIntruder()
    {
        return AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset");
    }

    private static void AddHallway(Grid grid, Vector2Int position)
    {
        grid.RegisterOccupant(
            new TestHallwayOccupant(),
            GridLayer.Hallway,
            new List<Vector2Int> { position },
            false);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }

    private sealed class IntruderScenarioWorld : IDisposable
    {
        private static readonly FieldInfo GridSystemInstanceField =
            typeof(GridSystemManager).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo GridField =
            typeof(GridSystemManager).GetField("<grid>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CharacterAwakeMethod =
            typeof(CharacterActor).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GridSystemManager previousGridSystem;
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly GameObject gridSystemObject;

        public IntruderScenarioWorld(int width)
        {
            previousGridSystem = GridSystemInstanceField?.GetValue(null) as GridSystemManager;

            Grid = new Grid(width, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                AddHallway(Grid, new Vector2Int(x, 0));
            }

            gridSystemObject = new GameObject("Intruder Scenario GridSystemManager");
            objects.Add(gridSystemObject);
            GridSystemManager manager = gridSystemObject.AddComponent<GridSystemManager>();
            GridField?.SetValue(manager, Grid);
            GridSystemInstanceField?.SetValue(null, manager);
        }

        public Grid Grid { get; }

        public void Track(GameObject obj)
        {
            if (obj != null && !objects.Contains(obj))
            {
                objects.Add(obj);
            }
        }

        public BuildableObject Place(string assetName, Vector2Int position)
        {
            BuildingSO buildingData = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                $"Assets/Resources/SO/Building/P1/{assetName}.asset");
            buildingData = buildingData != null
                ? buildingData
                : AssetDatabase.LoadAssetAtPath<BuildingSO>(
                    $"Assets/Resources/SO/Building/Modular/{assetName}.asset");
            buildingData = buildingData != null
                ? buildingData
                : AssetDatabase.LoadAssetAtPath<BuildingSO>(
                    $"Assets/Resources/SO/Building/{assetName}.asset");
            if (buildingData == null)
            {
                throw new InvalidOperationException($"{assetName} asset not found.");
            }

            GridBuildingFactory factory = new GridBuildingFactory();
            BuildableObject building = factory.Create(Grid, buildingData, position);
            if (building == null)
            {
                throw new InvalidOperationException($"{assetName} could not be created.");
            }

            objects.Add(building.gameObject);
            building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            building.SetGrid(Grid);
            CharacterAiEditorTestDependencies.Inject(building);
            CharacterAiEditorTestDependencies.InjectShop(building.GetComponent<Shop>());
            building.Initialization(buildingData, position);
            bool registered = Grid.RegisterOccupant(
                building,
                buildingData.Placement.Layer,
                buildingData.GetGridPosList(position),
                buildingData.Placement.IsMovement);
            if (!registered)
            {
                throw new InvalidOperationException($"{assetName} could not be registered.");
            }

            return building;
        }

        public CharacterActor CreateIntruder(Vector2Int position)
        {
            return CreateCharacter(LoadIntruder(), position, "Intruder Scenario Character");
        }

        public CharacterActor CreateCharacter(
            CharacterSO characterData,
            Vector2Int position,
            string objectName)
        {
            if (characterData == null)
            {
                throw new ArgumentNullException(nameof(characterData));
            }

            GameObject obj = new GameObject(objectName);
            objects.Add(obj);
            obj.AddComponent<SpriteRenderer>();
            obj.AddComponent<AbilityMove>();
            CharacterActor character = obj.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(obj);
            CharacterAwakeMethod?.Invoke(character, null);
            character.Identity.SetPersistentId(
                new GuidPersistentIdGenerator().NewCharacterId());
            character.RefreshAbilityCache();
            character.Initialization(characterData);
            character.SetLifecycleState(CharacterLifecycleState.Active);
            obj.transform.position = Grid.GetWorldPos(position);
            return character;
        }

        public void Dispose()
        {
            GridSystemInstanceField?.SetValue(null, previousGridSystem);
            foreach (GameObject obj in objects.Where((obj) => obj != null))
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private sealed class FixedBuildingWorldQuery : IBuildingWorldQuery
    {
        private readonly IReadOnlyList<BuildableObject> buildings;

        internal FixedBuildingWorldQuery(params BuildableObject[] buildings)
        {
            this.buildings = Array.AsReadOnly(
                (buildings ?? Array.Empty<BuildableObject>())
                .Where(building => building != null)
                .ToArray());
        }

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings;
    }

    private sealed class FixedOwnerCandidateCatalog : IOwnerCandidateCatalog
    {
        private readonly IReadOnlyCollection<CharacterSO> candidates;

        public FixedOwnerCandidateCatalog(CharacterSO ownerData)
        {
            candidates = new[] { ownerData };
        }

        public IReadOnlyCollection<CharacterSO> OwnerCandidates => candidates;
    }

    private sealed class ScenarioOwnerCharacterFactory : IOwnerCharacterFactory
    {
        private readonly IntruderScenarioWorld world;

        public ScenarioOwnerCharacterFactory(IntruderScenarioWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public CharacterActor CreateOwner(
            CharacterSO ownerData,
            GameObject ownerPrefab,
            Transform ownerSpawnPoint,
            Vector2Int ownerSpawnGridPosition)
        {
            CharacterActor owner = world.CreateCharacter(
                ownerData,
                ownerSpawnGridPosition,
                "Intruder Scenario Owner");
            if (ownerSpawnPoint != null)
            {
                owner.transform.position = ownerSpawnPoint.position;
            }

            return owner;
        }

        public CharacterActor CreateOwnerDetached(
            CharacterSO ownerData,
            GameObject ownerPrefab)
        {
            throw new NotSupportedException(
                "This focused invasion fixture does not restore character worlds.");
        }
    }

    private sealed class ScenarioOwnerRunLifecycleService : IOwnerRunLifecycleService
    {
        private readonly OwnerRunManager manager;

        public ScenarioOwnerRunLifecycleService(OwnerRunManager manager)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public void HandleOwnerDeath(CharacterActor owner, string reason)
        {
            manager.HandleOwnerDeath(owner, reason);
        }
    }

    private sealed class TestHallwayOccupant : IGridOccupant
    {
        public int GridId => 0;
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => true;
    }

    private sealed class ScenarioTimeScaleController :
        IGameTimeScaleController
    {
        public ScenarioTimeScaleController(float scale)
        {
            Scale = scale;
        }

        public float Scale { get; set; }
    }

    private sealed class NoopInvasionSaveService :
        IInvasionSaveService
    {
        public DungeonInvasionSaveData Capture()
        {
            return new DungeonInvasionSaveData();
        }

        public InvasionRestoreCandidate PrepareRestore(
            DungeonInvasionSaveData saveData)
        {
            throw new NotSupportedException();
        }

        public void PublishRestore(InvasionRestoreCandidate candidate)
        {
        }
    }

    public class PassiveInterfaceProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            Type returnType = targetMethod?.ReturnType;
            return returnType != null && returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private sealed class ThrowingVisualRootFactory : ICharacterVisualRootFactory
    {
        public SpriteRenderer EnsureVisualRoot(GameObject characterObject)
        {
            throw new InvalidOperationException(
                "Injected visual-root composition failure.");
        }
    }

    private sealed class TrackingCharacterSpawnObjectFactory :
        ICharacterSpawnObjectFactory
    {
        public bool ThrowDuringCreate { get; set; }
        public int DestroyCalls { get; private set; }
        public GameObject LastCreated { get; private set; }

        public GameObject CreateInactive(GameObject characterPrefab) =>
            CreateCandidate();

        public GameObject CreateInactive(
            GameObject characterPrefab,
            Action<GameObject> compose) => CreateCandidate();

        public GameObject CreateDetached(GameObject characterPrefab) =>
            CreateCandidate();

        public GameObject CreateDetached(
            GameObject characterPrefab,
            Action<GameObject> compose) => CreateCandidate();

        public void ComposeDetached(GameObject characterObject) =>
            throw new NotSupportedException();

        public void Inject(GameObject characterObject) =>
            throw new NotSupportedException();

        public void InjectAddedAbility(CharacterAbility ability) =>
            throw new NotSupportedException();

        public void Publish(GameObject characterObject) =>
            throw new NotSupportedException();

        public void PublishDetached(GameObject characterObject) =>
            throw new NotSupportedException();

        public DetachedCharacterPublication PublishDetachedInactive(
            GameObject characterObject) =>
            throw new NotSupportedException();

        public void ValidateDetachedPublication(
            DetachedCharacterPublication publication) =>
            throw new NotSupportedException();

        public void CompleteDetachedPublication(
            DetachedCharacterPublication publication) =>
            throw new NotSupportedException();

        public void RollbackDetachedPublication(
            DetachedCharacterPublication publication) =>
            throw new NotSupportedException();

        public void Destroy(GameObject characterObject)
        {
            DestroyCalls++;
            if (characterObject != null)
            {
                Object.DestroyImmediate(characterObject);
            }
        }

        private GameObject CreateCandidate()
        {
            LastCreated = new GameObject("Failed Invasion Intruder Candidate");
            LastCreated.SetActive(false);
            if (ThrowDuringCreate)
            {
                Destroy(LastCreated);
                throw new InvalidOperationException(
                    "Injected child-factory creation failure.");
            }

            return LastCreated;
        }
    }

    private sealed class CountingFacilityDamageListener : IDisposable
    {
        private readonly IDisposable subscription;

        public int Count { get; private set; }
        public BuildableObject LastFacility { get; private set; }

        public CountingFacilityDamageListener(IGameEventBus eventBus)
        {
            subscription = (eventBus ?? throw new ArgumentNullException(nameof(eventBus)))
                .Subscribe<InvasionFacilityDamagedEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)
        {
            Count++;
            LastFacility = eventType.facility;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}
