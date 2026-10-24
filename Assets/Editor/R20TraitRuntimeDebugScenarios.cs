#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class R20TraitRuntimeDebugScenarios
{
    private const string BetrayalOrAssault = "betrayal-or-assault";
    private const string PrefixedBetrayalOrAssault = "social:betrayal-or-assault";
    private const string LightSleeperPath =
        "Assets/Resources/SO/V20/Traits/General/Trait_201_light-sleeper.asset";
    private const string GrudgeHolderPath =
        "Assets/Resources/SO/V20/Traits/General/Trait_218_grudge-holder.asset";
    private const string ForgivingPath =
        "Assets/Resources/SO/V20/Traits/General/Trait_219_forgiving.asset";

    [MenuItem("DungeonStory/R20/Run Trait Runtime Scenarios")]
    public static void RunAll()
    {
        CharacterTraitSO lightSleeper = LoadTrait(LightSleeperPath);
        CharacterTraitSO grudgeHolder = LoadTrait(GrudgeHolderPath);
        CharacterTraitSO forgiving = LoadTrait(ForgivingPath);
        AssetSnapshot[] sources =
        {
            AssetSnapshot.Capture(LightSleeperPath, lightSleeper),
            AssetSnapshot.Capture(GrudgeHolderPath, grudgeHolder),
            AssetSnapshot.Capture(ForgivingPath, forgiving)
        };

        Exception failure = null;
        try
        {
            VerifyAuthoredRelationshipRules(grudgeHolder, forgiving);
            VerifyRelationshipMemoryAndRestore(forgiving);
            VerifyCommittedAmberDecision(lightSleeper);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        try
        {
            foreach (AssetSnapshot source in sources)
                source.RequireUnchanged();
        }
        catch (Exception sourceFailure)
        {
            if (failure == null)
                failure = sourceFailure;
            else
                failure = new AggregateException(failure, sourceFailure);
        }

        if (failure != null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();

        Debug.Log(
            "R20_TRAIT_RUNTIME=PASS; relationship=exact-memory-restore-retry; "
            + "amber=light-sleeper-decision-current-action-retained; "
            + "source-assets=unchanged");
    }

    private static void VerifyAuthoredRelationshipRules(
        CharacterTraitSO grudgeHolder,
        CharacterTraitSO forgiving)
    {
        RelationshipMemoryRule grudge = RelationshipRule(grudgeHolder);
        RelationshipMemoryRule forgiveness = RelationshipRule(forgiving);

        Require(
            string.Equals(grudge.eventId, BetrayalOrAssault, StringComparison.Ordinal),
            "Trait 218 must remember the raw betrayal-or-assault protocol ID.");
        Require(
            string.Equals(forgiveness.eventId, BetrayalOrAssault, StringComparison.Ordinal),
            "Trait 219 must remember the raw betrayal-or-assault protocol ID.");
        Require(
            Mathf.Approximately(forgiveness.relationshipDelta, -3f),
            "Trait 219 must retain the authored small negative relationship delta.");
        Require(
            forgiveness.apologyCanClear && !forgiveness.restitutionRequired,
            "Trait 219 must accept a real apology without mandatory restitution.");
    }

    private static void VerifyRelationshipMemoryAndRestore(
        CharacterTraitSO forgiving)
    {
        RelationshipMemoryRule rule = RelationshipRule(forgiving);
        using ActorFixture offender = new(
            "character:r20-trait-offender",
            "R20 offender",
            20021);
        using ActorFixture recipient = new(
            "character:r20-trait-forgiving",
            "R20 forgiving recipient",
            20022,
            forgiving.id);

        CharacterIdentityStateStore states = new();
        CharacterRelationshipMemoryService memories = new(
            new CharacterIdentityRuleRouter(),
            states);
        string recipientId = recipient.Actor.Identity.PersistentId;
        string offenderId = offender.Actor.Identity.PersistentId;
        string traitDefinitionId = forgiving.DefinitionId.Value;
        float baseline = recipient.Actor.SocialMemory.GetRelationshipSentiment(
            offender.Actor);

        memories.Remember(
            recipient.Actor,
            offender.Actor,
            PrefixedBetrayalOrAssault,
            severity: 4f,
            absoluteDay: 20);
        Require(
            !states.TryGet(
                recipientId,
                traitDefinitionId,
                rule.ruleId,
                out _),
            "A prefixed relationship event must not create an exact-protocol memory.");
        Require(
            Approximately(
                recipient.Actor.SocialMemory.GetRelationshipSentiment(offender.Actor),
                baseline),
            "A rejected prefixed event must not change social sentiment.");

        memories.Remember(
            recipient.Actor,
            offender.Actor,
            BetrayalOrAssault,
            severity: 4f,
            absoluteDay: 20);
        CharacterRelationshipMemoryRuleState remembered = ReadState(
            states,
            recipientId,
            traitDefinitionId,
            rule.ruleId);
        Require(
            remembered.entries.Count == 1
            && string.Equals(
                remembered.entries[0].offenderCharacterId,
                offenderId,
                StringComparison.Ordinal)
            && string.Equals(
                remembered.entries[0].eventId,
                BetrayalOrAssault,
                StringComparison.Ordinal),
            "The valid event must persist one raw relationship memory entry.");
        float harmed = recipient.Actor.SocialMemory.GetRelationshipSentiment(
            offender.Actor);
        Require(
            harmed < baseline,
            "Trait 219 harm memory must apply a negative relationship sentiment.");

        CharacterSocialMemorySnapshot legacySocial =
            recipient.Actor.SocialMemory.CaptureSnapshot();

        bool prefixedForgiven = memories.TryForgive(
            recipient.Actor,
            offender.Actor,
            PrefixedBetrayalOrAssault,
            restitutionProvided: false);
        Require(
            !prefixedForgiven
            && ReadState(
                states,
                recipientId,
                traitDefinitionId,
                rule.ruleId).entries.Count == 1
            && Approximately(
                recipient.Actor.SocialMemory.GetRelationshipSentiment(offender.Actor),
                harmed),
            "A failed prefixed apology must preserve both memory and sentiment.");

        bool forgiven = memories.TryForgive(
            recipient.Actor,
            offender.Actor,
            BetrayalOrAssault,
            restitutionProvided: false);
        float restored = recipient.Actor.SocialMemory.GetRelationshipSentiment(
            offender.Actor);
        Require(
            forgiven
            && ReadState(
                states,
                recipientId,
                traitDefinitionId,
                rule.ruleId).entries.Count == 0
            && restored > harmed,
            "Repairing the apology ID must clear the memory and restore sentiment.");
        Require(
            !memories.TryForgive(
                recipient.Actor,
                offender.Actor,
                BetrayalOrAssault,
                restitutionProvided: false)
            && Approximately(
                recipient.Actor.SocialMemory.GetRelationshipSentiment(offender.Actor),
                restored),
            "The same apology must not restore relationship sentiment twice.");

        using ActorFixture restoredRecipient = new(
            "character:r20-trait-forgiving-restored",
            "R20 restored forgiving recipient",
            20023,
            forgiving.id);
        restoredRecipient.Actor.SocialMemory.RestoreSnapshot(legacySocial.Clone());
        CharacterIdentityStateStore restoredStates = new();
        restoredStates.Restore(
            new[]
            {
                CreateLegacyRawMemory(
                    restoredRecipient.Actor.Identity.PersistentId,
                    traitDefinitionId,
                    rule.ruleId,
                    offenderId)
            },
            new[] { forgiving });
        CharacterRelationshipMemoryService restoredMemories = new(
            new CharacterIdentityRuleRouter(),
            restoredStates);
        float restoredHarm = restoredRecipient.Actor.SocialMemory
            .GetRelationshipSentiment(offender.Actor);
        Require(
            restoredHarm < baseline
            && restoredMemories.CanForgive(
                restoredRecipient.Actor,
                offender.Actor,
                restitutionProvided: false),
            "A legacy raw-ID memory must restore as an active forgivable memory.");
        Require(
            restoredMemories.TryForgive(
                restoredRecipient.Actor,
                offender.Actor,
                BetrayalOrAssault,
                restitutionProvided: false),
            "A restored legacy raw-ID memory must accept the same apology.");
        float legacyRestored = restoredRecipient.Actor.SocialMemory
            .GetRelationshipSentiment(offender.Actor);
        Require(
            legacyRestored > restoredHarm
            && ReadState(
                restoredStates,
                restoredRecipient.Actor.Identity.PersistentId,
                traitDefinitionId,
                rule.ruleId).entries.Count == 0,
            "The restored legacy memory must clear and restore sentiment.");
        Require(
            !restoredMemories.TryForgive(
                restoredRecipient.Actor,
                offender.Actor,
                BetrayalOrAssault,
                restitutionProvided: false)
            && Approximately(
                restoredRecipient.Actor.SocialMemory
                    .GetRelationshipSentiment(offender.Actor),
                legacyRestored),
            "A restored legacy memory must also restore sentiment only once.");
    }

    private static void VerifyCommittedAmberDecision(CharacterTraitSO lightSleeper)
    {
        using ActorFixture responsive = new(
            "character:r20-trait-light-sleeper",
            "R20 light sleeper",
            20024,
            lightSleeper.id);
        using ActorFixture control = new(
            "character:r20-trait-alert-control",
            "R20 alert control",
            20025);
        AIHunt retainedActionSet = ScriptableObject.CreateInstance<AIHunt>();
        retainedActionSet.name = "R20 retained current work action";
        AIAction retainedAction = new(
            retainedActionSet,
            AIActionPlan.WithoutDestination);
        responsive.Actor.Brain.bestAction = retainedAction;
        responsive.Actor.Brain.isBestActionEnd = false;

        GameEventBus events = new();
        FixedAlertService alerts = new();
        using CharacterAlarmResponseRuntime runtime = new(
            events,
            new FixedCharacterWorld(responsive.Actor, control.Actor),
            EmptyBuildingWorld.Instance,
            FixedGameClock.Instance,
            new FixedCalendar(day: 20, hour: 8),
            alerts,
            EmptyReserveTarget.Instance,
            EmptyForecastRegistry.Instance);
        try
        {
            runtime.Start();
            Require(
                responsive.Actor.CanRunAi
                && responsive.Actor.Progression.GetEffectiveRuntimeProfile()
                    .GetBehaviorUtilityMultiplier(
                        new[] { CharacterIdentityRuleProtocols.MinorAlert }) > 1f,
                "The light-sleeper fixture must be a live AI actor with alert preference.");

            int responsiveBefore =
                responsive.Actor.Brain.ImmediateDecisionRequestCount;
            int controlBefore = control.Actor.Brain.ImmediateDecisionRequestCount;
            CharacterLifecycleState lifecycleBefore =
                responsive.Actor.CurrentLifecycleState;
            float sleepBefore =
                responsive.Actor.Stats.Stats[CharacterCondition.SLEEP];

            alerts.SetCommitted(SettlementThreatAlertLevel.Amber, epochId: 2L);
            events.Publish(new SettlementCommittedAlertChangedEvent(
                SettlementThreatAlertLevel.Green,
                SettlementThreatAlertLevel.Amber,
                epochId: 2L,
                absoluteHour: 20L * GameCalendarRules.HoursPerDay + 8L));

            Require(
                responsive.Actor.Brain.ImmediateDecisionRequestCount
                    == responsiveBefore + 1
                && string.Equals(
                    responsive.Actor.Brain.LastImmediateDecisionReason,
                    CharacterIdentityRuleProtocols.MinorAlert,
                    StringComparison.Ordinal),
                "Committed Amber must request one immediate light-sleeper decision.");
            Require(
                control.Actor.Brain.ImmediateDecisionRequestCount == controlBefore,
                "Committed Amber must not request the trait-specific decision for a control actor.");
            Require(
                ReferenceEquals(responsive.Actor.Brain.bestAction, retainedAction)
                && !responsive.Actor.Brain.isBestActionEnd
                && responsive.Actor.CurrentLifecycleState == lifecycleBefore
                && Approximately(
                    responsive.Actor.Stats.Stats[CharacterCondition.SLEEP],
                    sleepBefore),
                "The Amber signal must retain current work without forcing a sleep wake transition.");
        }
        finally
        {
            responsive.Actor.Brain.bestAction = null;
            UnityEngine.Object.DestroyImmediate(retainedActionSet);
        }
    }

    private static CharacterIdentityRuntimeStateSaveData CreateLegacyRawMemory(
        string characterId,
        string traitDefinitionId,
        string ruleId,
        string offenderId)
    {
        CharacterRelationshipMemoryRuleState payload = new();
        payload.entries.Add(new CharacterRelationshipMemoryEntrySaveData
        {
            offenderCharacterId = offenderId,
            eventId = BetrayalOrAssault,
            severity = 4f,
            createdDay = 20
        });
        return new CharacterIdentityRuntimeStateSaveData
        {
            characterId = characterId,
            rules = new List<CharacterIdentityRuleStateSaveData>
            {
                new()
                {
                    traitDefinitionId = traitDefinitionId,
                    ruleId = ruleId,
                    revision = 1,
                    statePayload = JsonUtility.ToJson(payload)
                }
            }
        };
    }

    private static CharacterRelationshipMemoryRuleState ReadState(
        CharacterIdentityStateStore states,
        string characterId,
        string traitDefinitionId,
        string ruleId)
    {
        Require(
            states.TryGet(
                characterId,
                traitDefinitionId,
                ruleId,
                out CharacterIdentityRuleStateSaveData saved),
            $"Missing relationship memory state '{characterId}:{traitDefinitionId}:{ruleId}'.");
        CharacterRelationshipMemoryRuleState state =
            JsonUtility.FromJson<CharacterRelationshipMemoryRuleState>(
                saved.statePayload);
        Require(state?.entries != null, "Relationship memory payload did not decode.");
        return state;
    }

    private static RelationshipMemoryRule RelationshipRule(CharacterTraitSO trait)
    {
        RelationshipMemoryRule[] rules = (trait.identityRules
                ?? new List<CharacterIdentityRule>())
            .OfType<RelationshipMemoryRule>()
            .ToArray();
        Require(
            rules.Length == 1,
            $"Trait {trait.id} must expose one relationship memory rule.");
        return rules[0];
    }

    private static CharacterTraitSO LoadTrait(string path)
    {
        CharacterTraitSO trait = AssetDatabase.LoadAssetAtPath<CharacterTraitSO>(path);
        Require(trait != null, $"Missing trait fixture asset '{path}'.");
        return trait;
    }

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class AssetSnapshot
    {
        private readonly string path;
        private readonly string fullPath;
        private readonly CharacterTraitSO asset;
        private readonly byte[] sourceBytes;
        private readonly string sourceJson;
        private readonly bool dirty;

        private AssetSnapshot(string path, CharacterTraitSO asset)
        {
            this.path = path;
            this.asset = asset;
            fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            sourceBytes = File.ReadAllBytes(fullPath);
            sourceJson = EditorJsonUtility.ToJson(asset);
            dirty = EditorUtility.IsDirty(asset);
        }

        public static AssetSnapshot Capture(string path, CharacterTraitSO asset) =>
            new(path, asset);

        public void RequireUnchanged()
        {
            Require(
                sourceBytes.SequenceEqual(
                    File.ReadAllBytes(fullPath)),
                $"Trait source bytes changed during runtime fixture '{path}'.");
            Require(
                string.Equals(
                    sourceJson,
                    EditorJsonUtility.ToJson(asset),
                    StringComparison.Ordinal),
                $"Trait source JSON changed during runtime fixture '{path}'.");
            Require(
                dirty == EditorUtility.IsDirty(asset),
                $"Trait dirty state changed during runtime fixture '{path}'.");
        }
    }

    private sealed class ActorFixture : IDisposable
    {
        private readonly GameObject actorObject;
        private readonly CharacterSO data;

        public ActorFixture(
            string persistentId,
            string displayName,
            int seed,
            params int[] traitIds)
        {
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                displayName,
                "human");
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();
            actorObject = new GameObject("R20TraitRuntime_" + seed);
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            AIBrain brain = actorObject.AddComponent<AIBrain>();
            AIActionSet wait = Resources.Load<AIActionSet>("SO/AI/Action/Wait")
                ?? throw new InvalidOperationException("R20 trait fixture requires the authored Wait action.");
            brain.availableActions = new[] { new AIAction(wait, AIActionPlan.WithoutDestination) };
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.Identity.SetPersistentId(persistentId);
            Actor.Initialization(data);
            Actor.Progression.ApplyPreparedIdentity(
                displayName,
                "r20-trait-runtime-fixture",
                traitIds ?? Array.Empty<int>(),
                CharacterPotentialGrade.Ordinary,
                seed,
                autoChooseDrafts: false,
                maximumTraitCount: 4,
                ensureInitialDrafts: false);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
            if (Actor.IsUnpublishedComposition) Actor.PublishComposition();
            Actor.Stats.Stats[CharacterCondition.MOOD] = 70f;
        }

        public CharacterActor Actor { get; }

        public void Dispose()
        {
            if (actorObject != null)
                UnityEngine.Object.DestroyImmediate(actorObject);
            if (data != null)
                UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery
    {
        private readonly CharacterActor[] actors;

        public FixedCharacterWorld(params CharacterActor[] actors) =>
            this.actors = actors?
                .Where(actor => actor != null)
                .ToArray()
                ?? Array.Empty<CharacterActor>();

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        public static EmptyBuildingWorld Instance { get; } = new();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    private sealed class FixedGameClock : IGameClock
    {
        public static FixedGameClock Instance { get; } = new();
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        public FixedCalendar(int day, int hour)
        {
            Day = day;
            Hour = hour;
        }

        public int Day { get; private set; }
        public int Hour { get; private set; }
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int day, int hour)
        {
            Day = day;
            Hour = hour;
        }
    }

    private sealed class FixedAlertService : ISettlementAlertService
    {
        private SettlementAlertSnapshot snapshot = Snapshot(
            SettlementThreatAlertLevel.Green,
            epochId: 1L);

        public SettlementAlertSnapshot Capture() => snapshot;

        public void SetCommitted(SettlementThreatAlertLevel level, long epochId) =>
            snapshot = Snapshot(level, epochId);

        public EmergencyAccountingResult PublishIncidentSignal(
            SettlementIncidentSignal signal) => Unexpected();
        public EmergencyAccountingResult ResolveIncident(
            string incidentId,
            long revision) => Unexpected();
        public long GetNextIncidentRevision(string incidentId) =>
            throw new InvalidOperationException("Unexpected alert revision request.");
        public EmergencyAccountingResult UpdateReserveCoverage(
            long currentMilliWu,
            long targetMilliWu) => Unexpected();
        public bool TryClaimContextTransition(
            string characterId,
            long epochId,
            bool toEmergency) =>
            throw new InvalidOperationException("Unexpected alert context claim.");
        public EmergencyAccountingResult RecordSuspendedWork(
            SettlementSuspendedWorkSnapshot suspendedWork) => Unexpected();
        public bool TryGetSuspendedWork(
            string characterId,
            out SettlementSuspendedWorkSnapshot suspendedWork)
        {
            suspendedWork = default;
            return false;
        }
        public EmergencyAccountingResult MarkSuspendedWorkResumed(
            string characterId,
            long epochId) => Unexpected();
        public EmergencyAccountingResult MarkSuspendedWorkAbandoned(
            string characterId,
            long epochId,
            string reasonCode) => Unexpected();

        private static SettlementAlertSnapshot Snapshot(
            SettlementThreatAlertLevel level,
            long epochId) => new(
                level,
                level,
                epochId,
                levelEnteredAbsoluteHour: 0L,
                downgradeStableSinceAbsoluteHour: 0L,
                amberIncidentCount: level == SettlementThreatAlertLevel.Amber ? 1 : 0,
                redIncidentCount: 0,
                activeIncidentIds: Array.Empty<string>(),
                suspendedWork: Array.Empty<SettlementSuspendedWorkSnapshot>(),
                reserveCoverageBand: default,
                reserveCoverage: 1f,
                coverageStableSinceAbsoluteHour: 0L);

        private static EmergencyAccountingResult Unexpected() =>
            throw new InvalidOperationException("Unexpected alert mutation.");
    }

    private sealed class EmptyReserveTarget : ISettlementEmergencyReserveTargetQuery
    {
        public static EmptyReserveTarget Instance { get; } = new();
        public SettlementEmergencyReserveTargetSnapshot CaptureTarget() => default;
    }

    private sealed class EmptyForecastRegistry : IEmergencyRiskForecastRegistry
    {
        public static EmptyForecastRegistry Instance { get; } = new();
        public EmergencyAccountingResult SetP90Requirement(
            string sourceId,
            long requiredMilliWu) =>
            throw new InvalidOperationException("Unexpected forecast mutation.");
        public EmergencyAccountingResult Remove(string sourceId) =>
            throw new InvalidOperationException("Unexpected forecast mutation.");
        public EmergencyRiskForecastSnapshot Capture() => default;
    }
}
#endif
