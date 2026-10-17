#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class ObservedCareerArcCausalProductionEntryDebugScenarios
{
    internal const string CausalOwnerRestoreKind =
        "owner-family:observed-career-arc";
    internal const string MentorshipAwardActionId =
        "action:award-career-mentorship";
    internal const string LastLessonActionId =
        "action:observe-last-lesson";
    internal const string QuietPromotionActionId =
        "action:observe-quiet-promotion";
    internal const string FuneralActionId =
        "action:observe-mentor-funeral";
    internal const string LineageCompressionActionId =
        "action:compress-mentor-lineage";

    private const string OwnerKind = "owner-kind:observed-career-arc";
    private const string DomainStateSchema =
        "dungeonstory-causal-observed-career-arc-owner-state@2";
    private const string MentorId = "character:causal-career-arc:mentor";
    private const string StudentId = "character:causal-career-arc:student";
    private const string AcademyId = "building:causal-career-arc:academy";
    private const string MemorialId = "building:causal-career-arc:memorial";
    private const string ProficiencyId = "proficiency:crafting";
    private const string CareerLedgerStackId =
        "stack:causal-career-arc:career-ledger";
    private const int MentorshipAwardDay = 42;
    private const int LastLessonDay = 42;
    private const int QuietPromotionDay = 43;
    private const int FuneralDay = 44;
    private const int LineageCompressionDay = 200;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException(
                "A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
        {
            throw new ArgumentException(
                "An observed career-arc checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));
        }

        var context = new Context(
            causalRunId,
            persistent ? ownerId : null,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(
            5,
            context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            Fixture(
                MentorshipAwardActionId,
                MigratedProducerOutcomeKind
                    .CareerMentorshipAwardCommitReceipt,
                context,
                context.AwardMentorship,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind
                        .CareerMentorshipAwardCommitReceipt,
                    expectedSequence: 1,
                    expectedDay: MentorshipAwardDay),
                lifetime),
            Fixture(
                LastLessonActionId,
                MigratedProducerOutcomeKind.ObservedLastLessonLifeEventReceipt,
                context,
                context.ObserveLastLesson,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind
                        .ObservedLastLessonLifeEventReceipt,
                    expectedSequence: 2,
                    expectedDay: LastLessonDay),
                lifetime),
            Fixture(
                QuietPromotionActionId,
                MigratedProducerOutcomeKind
                    .ObservedQuietPromotionLifeEventReceipt,
                context,
                context.ObserveQuietPromotion,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind
                        .ObservedQuietPromotionLifeEventReceipt,
                    expectedSequence: 3,
                    expectedDay: QuietPromotionDay),
                lifetime),
            Fixture(
                FuneralActionId,
                MigratedProducerOutcomeKind.ObservedFuneralLifeEventReceipt,
                context,
                context.ObserveMentorFuneral,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind
                        .ObservedFuneralLifeEventReceipt,
                    expectedSequence: 4,
                    expectedDay: FuneralDay),
                lifetime),
            Fixture(
                LineageCompressionActionId,
                MigratedProducerOutcomeKind
                    .ObservedLineageCompressionLifeEventReceipt,
                context,
                context.CompressMentorLineage,
                () => context.RequireOutcome(
                    MigratedProducerOutcomeKind
                        .ObservedLineageCompressionLifeEventReceipt,
                    expectedSequence: 5,
                    expectedDay: LineageCompressionDay),
                lifetime)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string OwnerIdValue = "owner:causal-observed-career-arc:focused";
        JObject awardCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-observed-career-arc:award",
                   OwnerIdValue)))
        {
            IProductionCausalActionFixture award = fixtures.Get(
                MentorshipAwardActionId);
            Require((int)award.CaptureState()["careerArc.stage"] == 0,
                "Observed career arc did not start empty.");
            Require(award.Execute(),
                "The real mentorship-award entry was rejected.");
            Require(award.RequireExactOutcome().HasCanonicalPayloadHash,
                "The mentorship-award result has no canonical hash.");
            IReadOnlyDictionary<string, object> after = award.CaptureState();
            Require(
                (int)after["careerArc.stage"] == 1
                && (int)after["careerArc.successfulReceiptCount"] == 0
                && (int)after["careerArc.activeEventCount"] == 0
                && (int)after["careerArc.mentorshipAwardOutcomeCount"] == 1,
                "Mentorship award did not persist its career and ledger state.");
            awardCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)award)
                .CaptureOwnerCheckpoint();
        }

        JObject lessonCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-observed-career-arc:lesson",
                   OwnerIdValue,
                   awardCheckpoint)))
        {
            IProductionCausalActionFixture award = fixtures.Get(
                MentorshipAwardActionId);
            IProductionCausalActionFixture lesson = fixtures.Get(
                LastLessonActionId);
            Require(!award.Execute(),
                "Fresh composition replayed the mentorship award.");
            Require(lesson.Execute(),
                "The real observed last-lesson entry was rejected.");
            Require(lesson.RequireExactOutcome().HasCanonicalPayloadHash,
                "The observed last-lesson result has no canonical hash.");
            IReadOnlyDictionary<string, object> after = lesson.CaptureState();
            Require(
                (int)after["careerArc.stage"] == 2
                && (int)after["careerArc.successfulReceiptCount"] == 1
                && (int)after["careerArc.activeEventCount"] == 1
                && (int)after["careerArc.resolvedEventCount"] == 0,
                "Last lesson did not persist its pending life event.");
            lessonCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)lesson)
                .CaptureOwnerCheckpoint();
        }

        JObject promotionCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-observed-career-arc:promotion",
                   OwnerIdValue,
                   lessonCheckpoint)))
        {
            IProductionCausalActionFixture lesson = fixtures.Get(
                LastLessonActionId);
            IProductionCausalActionFixture promotion = fixtures.Get(
                QuietPromotionActionId);
            Require(!lesson.Execute(),
                "Fresh composition replayed the last lesson.");
            Require(promotion.Execute(),
                "The real observed quiet-promotion entry was rejected.");
            Require(promotion.RequireExactOutcome().HasCanonicalPayloadHash,
                "The observed quiet-promotion result has no canonical hash.");
            IReadOnlyDictionary<string, object> after = promotion.CaptureState();
            Require(
                (int)after["careerArc.stage"] == 3
                && (int)after["careerArc.successfulReceiptCount"] == 2
                && (int)after["careerArc.activeEventCount"] == 1
                && (int)after["careerArc.resolvedEventCount"] == 1,
                "Student promotion did not retain the restored last lesson.");
            promotionCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)promotion)
                .CaptureOwnerCheckpoint();
        }

        JObject funeralCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-observed-career-arc:funeral",
                   OwnerIdValue,
                   promotionCheckpoint)))
        {
            IProductionCausalActionFixture promotion = fixtures.Get(
                QuietPromotionActionId);
            IProductionCausalActionFixture funeral = fixtures.Get(
                FuneralActionId);
            Require(!promotion.Execute(),
                "Fresh composition replayed the quiet promotion.");
            Require(funeral.Execute(),
                "The real observed mentor-funeral entry was rejected.");
            Require(funeral.RequireExactOutcome().HasCanonicalPayloadHash,
                "The observed funeral result has no canonical hash.");
            IReadOnlyDictionary<string, object> after = funeral.CaptureState();
            Require(
                (int)after["careerArc.stage"] == 4
                && (int)after["careerArc.successfulReceiptCount"] == 3
                && (int)after["careerArc.activeEventCount"] == 1
                && (int)after["careerArc.resolvedEventCount"] == 2
                && (int)after["careerArc.mentorTombstoneCount"] == 1,
                "Mentor funeral did not retain the student career history.");
            funeralCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)funeral)
                .CaptureOwnerCheckpoint();
        }

        JObject lineageCheckpoint;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-observed-career-arc:lineage",
                   OwnerIdValue,
                   funeralCheckpoint)))
        {
            IProductionCausalActionFixture funeral = fixtures.Get(
                FuneralActionId);
            IProductionCausalActionFixture lineage = fixtures.Get(
                LineageCompressionActionId);
            Require(!funeral.Execute(),
                "Fresh composition replayed the mentor funeral.");
            Require(lineage.Execute(),
                "The real lineage-compression entry was rejected.");
            Require(lineage.RequireExactOutcome().HasCanonicalPayloadHash,
                "The lineage-compression result has no canonical hash.");
            IReadOnlyDictionary<string, object> after = lineage.CaptureState();
            Require(
                (int)after["careerArc.stage"] == 5
                && (int)after["careerArc.successfulReceiptCount"] == 4
                && (int)after["careerArc.activeEventCount"] == 1
                && (int)after["careerArc.resolvedEventCount"] == 3
                && (int)after["careerArc.mentorTombstoneCount"] == 0
                && (int)after["careerArc.lineageSummaryCount"] == 1
                && (int)after["careerArc.lineageWorldFlagCount"] == 1,
                "Lineage compression did not preserve the mentor's career legacy.");
            lineageCheckpoint =
                ((IProductionCausalOwnerCheckpointFixture)lineage)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-observed-career-arc:terminal",
                   OwnerIdValue,
                   lineageCheckpoint)))
        {
            IProductionCausalActionFixture award = fixtures.Get(
                MentorshipAwardActionId);
            IProductionCausalActionFixture lesson = fixtures.Get(
                LastLessonActionId);
            IProductionCausalActionFixture promotion = fixtures.Get(
                QuietPromotionActionId);
            IProductionCausalActionFixture funeral = fixtures.Get(
                FuneralActionId);
            IProductionCausalActionFixture lineage = fixtures.Get(
                LineageCompressionActionId);
            JObject before =
                ((IProductionCausalOwnerCheckpointFixture)lesson)
                .CaptureOwnerCheckpoint();
            Require(!award.Execute()
                    && !lesson.Execute()
                    && !promotion.Execute()
                    && !funeral.Execute()
                    && !lineage.Execute(),
                "Terminal observed career arc accepted a replay.");
            JObject after =
                ((IProductionCausalOwnerCheckpointFixture)lesson)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected observed career-arc replay mutated its checkpoint.");
        }

        JObject campaignTamper = (JObject)promotionCheckpoint.DeepClone();
        ((JArray)((JObject)campaignTamper["domainState"])["society"]
            ["successfulLifeEventOperations"]).Clear();
        Require(RejectCheckpoint(
                OwnerIdValue,
                campaignTamper,
                "campaign-tamper"),
            "An observed career arc without source receipts was accepted.");

        JObject ledgerTamper = (JObject)promotionCheckpoint.DeepClone();
        ((JArray)((JObject)ledgerTamper["outcomeLedger"])["exactOutcomes"])
            .Clear();
        Require(RejectCheckpoint(
                OwnerIdValue,
                ledgerTamper,
                "ledger-tamper"),
            "An observed career arc without exact outcomes was accepted.");

        JObject lineageTamper = (JObject)lineageCheckpoint.DeepClone();
        ((JArray)((JObject)lineageTamper["domainState"])
            ["kinshipHousehold"]["kinship"]["lineageSummaries"]).Clear();
        Require(RejectCheckpoint(
                OwnerIdValue,
                lineageTamper,
                "lineage-tamper"),
            "A compressed career arc without its lineage summary was accepted.");

        if (log)
        {
            Debug.Log(
                "Mentorship award -> restored last lesson -> restored promotion -> restored funeral -> restored lineage compression causal owner family PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Observed career-arc causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    private static IProductionCausalActionFixture Fixture(
        string actionId,
        MigratedProducerOutcomeKind kind,
        Context context,
        Func<bool> execute,
        Func<GameplayOutcomeReplayIdentity> requireOutcome,
        SharedProductionCausalFixtureLifetime lifetime) =>
        new DelegateProductionCausalActionFixture(
            actionId,
            "character",
            MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId,
            context.CaptureState,
            execute,
            requireOutcome,
            context.CaptureOutcomeEvidence,
            lifetime.Release,
            context.PersistentOwnerId,
            context.PersistentOwnerKind,
            context.HasPersistentOwner ? context.CaptureOwnerCheckpoint : null,
            context.HasPersistentOwner ? CausalOwnerRestoreKind : null,
            context.HasPersistentOwner ? context : null);

    private static bool RejectCheckpoint(
        string ownerId,
        JObject checkpoint,
        string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-observed-career-arc:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly V20CampaignRuntime campaign;
        private readonly ObservedLifeEventOutcomeCoordinator coordinator;
        private readonly CareerRuntime careers;
        private readonly CareerEquipmentSlots careerEquipmentSlots;
        private readonly CareerDurableEquipmentAwardRuntime careerEquipment;
        private readonly KinshipHouseholdRuntime social;
        private readonly SocialRitualFactEditorFixture socialFacts;
        private readonly ObservedQuietPromotionLifeEventReceipt quietPromotion;
        private readonly ObservedFuneralLifeEventReceipt funeral;
        private CareerMentorshipAwardCommitReceipt mentorshipAward;
        private bool disposed;

        internal Context(
            string causalRunId,
            string ownerId,
            JObject checkpoint)
        {
            this.ownerId = string.IsNullOrWhiteSpace(ownerId)
                ? string.Empty
                : CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            string ledgerRunId = HasPersistentOwner
                ? ProductionCausalOwnerCheckpoint.LedgerRunId(
                    this.ownerId,
                    OwnerKind)
                : causalRunId + ":observed-career-arc-ledger";
            var aggregateRootStore = new DungeonRuntimeAggregateRootStore();
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ledgerRunId,
                aggregateRootStore);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            socialFacts = new SocialRitualFactEditorFixture(outcomes, probe);
            campaign = new V20CampaignRuntime(
                aggregateRootStore,
                new V20StoryContentCatalog(
                    new ResourceGameContentCatalog(
                        new UnityGameContentRootLoader())),
                null,
                null,
                socialFacts.Journal);
            coordinator = new ObservedLifeEventOutcomeCoordinator(probe);
            careers = new CareerRuntime(aggregateRootStore);
            careerEquipmentSlots = new CareerEquipmentSlots();
            careerEquipment = new CareerDurableEquipmentAwardRuntime(
                new DurableFacilityEquipmentPolicyRegistry(
                    new IDurableFacilityEquipmentPolicySource[]
                    {
                        new CareerDurableEquipmentPolicySource()
                    }),
                careerEquipmentSlots,
                new CareerEquipmentUse(careerEquipmentSlots),
                careers,
                socialFacts.Journal);
            social = new KinshipHouseholdRuntime(
                aggregateRootStore,
                campaign,
                new GameEventBus(),
                socialFacts.Journal);
            quietPromotion = CreateQuietPromotionReceipt();
            funeral = CreateFuneralReceipt();

            try
            {
                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                probe.ClearObservations();
                ValidateState();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool HasPersistentOwner => ownerId.Length > 0;
        internal string PersistentOwnerId => ownerId;
        internal string PersistentOwnerKind => HasPersistentOwner
            ? OwnerKind
            : string.Empty;

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            ThrowIfDisposed();
            ValidateState();
            SocietyEventWorldSaveData society = campaign.CaptureSociety();
            return new Dictionary<string, object>
            {
                ["careerArc.ownerId"] = HasPersistentOwner
                    ? ownerId
                    : "owner:ephemeral-observed-career-arc",
                ["careerArc.stage"] = Stage,
                ["careerArc.absoluteDay"] = CurrentAbsoluteDay,
                ["careerArc.mentorId"] = MentorId,
                ["careerArc.studentId"] = StudentId,
                ["careerArc.academyId"] = AcademyId,
                ["careerArc.proficiencyId"] = ProficiencyId,
                ["careerArc.successfulReceiptCount"] =
                    society.successfulLifeEventOperations.Count,
                ["careerArc.activeEventCount"] = society.activeEvents.Count,
                ["careerArc.resolvedEventCount"] =
                    society.recentResolvedEvents.Count,
                ["careerArc.mentorshipAwardOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .CareerMentorshipAwardCommitReceipt),
                ["careerArc.lastLessonOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedLastLessonLifeEventReceipt),
                ["careerArc.quietPromotionOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedQuietPromotionLifeEventReceipt),
                ["careerArc.funeralOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedFuneralLifeEventReceipt),
                ["careerArc.lineageCompressionOutcomeCount"] = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedLineageCompressionLifeEventReceipt),
                ["careerArc.mentorTombstoneCount"] = social.Tombstones.Count(
                    value => string.Equals(
                        value.characterId,
                        MentorId,
                        StringComparison.Ordinal)),
                ["careerArc.lineageSummaryCount"] = social.Capture()
                    .kinship.lineageSummaries.Count(value => value != null
                        && value.generation == 1),
                ["careerArc.lineageWorldFlagCount"] = campaign.WorldFlags.Count(
                    value => string.Equals(
                        value,
                        "life-event:story-compressed",
                        StringComparison.Ordinal)),
                ["careerArc.migratedOwnerCount"] =
                    outcomes.Transaction.Capture().owners.Count,
                ["careerArc.exactOutcomeCount"] = ExactOutcomeCount()
            };
        }

        internal bool AwardMentorship()
        {
            ThrowIfDisposed();
            if (Stage != 0)
                return false;

            CharacterId mentor = new(MentorId);
            CharacterId student = new(StudentId);
            BuildingInstanceId academy = new(AcademyId);
            CharacterProficiencyId proficiency = new(ProficiencyId);
            careers.AssignMentorship(
                mentor,
                student,
                academy,
                proficiency);
            careers.RecordMentorshipWork(
                student,
                MentorshipAwardDay,
                mentorContribution: true,
                CareerRules.MentoringWorkAmountPerParticipant);
            CareerMentorshipSnapshot ready = careers.RecordMentorshipWork(
                student,
                MentorshipAwardDay,
                mentorContribution: false,
                CareerRules.MentoringWorkAmountPerParticipant);
            bool committed = careerEquipment.TryCommitAward(
                academy,
                new Vector2Int(7, 3),
                ready,
                MentorshipAwardDay,
                () => careers.TryMarkMentoringAwarded(
                    student,
                    MentorshipAwardDay),
                out mentorshipAward);
            Require(committed && mentorshipAward.IsValid,
                "Career mentorship award production entry failed.");
            ValidateState();
            return Stage == 1;
        }

        internal bool ObserveLastLesson()
        {
            ThrowIfDisposed();
            if (Stage != 1)
                return false;
            bool accepted = coordinator.TryRecordLastLesson(
                campaign,
                CreateLastLessonReceipt(mentorshipAward),
                out bool changed,
                out string failureReason);
            Require(accepted && changed,
                "Observed last-lesson production entry failed: "
                + failureReason);
            ValidateState();
            return Stage == 2;
        }

        internal bool ObserveQuietPromotion()
        {
            ThrowIfDisposed();
            if (Stage != 2)
                return false;
            bool accepted = coordinator.TryRecordQuietPromotion(
                campaign,
                quietPromotion,
                out bool changed,
                out string failureReason);
            Require(accepted && changed,
                "Observed quiet-promotion production entry failed: "
                + failureReason);
            ValidateState();
            return Stage == 3;
        }

        internal bool ObserveMentorFuneral()
        {
            ThrowIfDisposed();
            if (Stage != 3)
                return false;
            bool accepted = coordinator.TryRecordFuneral(
                campaign,
                funeral,
                out bool changed,
                out string failureReason);
            Require(accepted && changed,
                "Observed mentor-funeral production entry failed: "
                + failureReason);
            social.ArchiveDeath(
                new CharacterId(MentorId),
                new CharacterSpeciesId("species:human"),
                birthAbsoluteDay: 1,
                deathAbsoluteDay: FuneralDay,
                famous: false,
                new HouseholdId("household:causal-career-arc:mentor"),
                generation: 1);
            ValidateState();
            return Stage == 4;
        }

        internal bool CompressMentorLineage()
        {
            ThrowIfDisposed();
            if (Stage != 4)
                return false;
            social.ArchiveColdData(
                LineageCompressionDay,
                new[] { new CharacterId(StudentId) });
            ValidateState();
            return Stage == 5;
        }

        internal GameplayOutcomeReplayIdentity RequireOutcome(
            MigratedProducerOutcomeKind kind,
            int expectedSequence,
            int expectedDay)
        {
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    kind,
                    probe);
            MigratedProducerOutcomeDefinition definition =
                MigratedProducerOutcomeCatalog.Get(kind);
            if (!identity.HasCanonicalPayloadHash
                || !outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                || exact.sequence != expectedSequence
                || exact.absoluteDay != expectedDay
                || exact.status != GameplayOutcomeStatus.Succeeded
                || exact.ownerRevision != 1L
                || exact.commitRevision != 1L
                || !string.Equals(
                    exact.producerId,
                    definition.ProducerId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                || exact.subjects == null
                || exact.subjects.Count < 1
                || exact.facts == null
                || !exact.facts.Any(value => string.Equals(
                    value.factId,
                    MigratedProducerOutcomeIds.SummaryFact.Value,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Observed career-arc entry did not publish its exact outcome: "
                    + kind);
            }
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(
            GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(
                outcomes.Ledger,
                identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            ThrowIfDisposed();
            if (!HasPersistentOwner)
            {
                throw new InvalidOperationException(
                    "Ephemeral observed career-arc fixtures have no checkpoint.");
            }
            ValidateState();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                new JObject
                {
                    ["schemaVersion"] = 2,
                    ["schema"] = DomainStateSchema,
                    ["stage"] = Stage,
                    ["mentorId"] = MentorId,
                    ["studentId"] = StudentId,
                    ["academyId"] = AcademyId,
                    ["proficiencyId"] = ProficiencyId,
                    ["mentorshipAward"] = mentorshipAward.IsValid
                        ? new JObject
                        {
                            ["academyId"] = mentorshipAward
                                .AcademyBuildingId.Value,
                            ["ledgerStackId"] = mentorshipAward.LedgerStackId,
                            ["beforeContentRevision"] = mentorshipAward
                                .BeforeContentRevision,
                            ["afterContentRevision"] = mentorshipAward
                                .AfterContentRevision,
                            ["sourceOperationId"] = mentorshipAward
                                .SourceOperationId
                        }
                        : JValue.CreateNull(),
                    ["career"] = JObject.Parse(
                        JsonUtility.ToJson(careers.Capture())),
                    ["kinshipHousehold"] = JObject.Parse(
                        JsonUtility.ToJson(social.Capture())),
                    ["milestones"] = JObject.Parse(
                        JsonUtility.ToJson(campaign.CaptureMilestones())),
                    ["society"] = JObject.Parse(
                        JsonUtility.ToJson(campaign.CaptureSociety())),
                    ["outcomeOwners"] = JObject.Parse(JsonUtility.ToJson(
                        outcomes.Transaction.Capture()))
                },
                outcomes.Ledger);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            socialFacts?.Dispose();
        }

        private int Stage
        {
            get
            {
                int awards = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .CareerMentorshipAwardCommitReceipt);
                int lessons = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedLastLessonLifeEventReceipt);
                int promotions = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedQuietPromotionLifeEventReceipt);
                int funerals = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedFuneralLifeEventReceipt);
                int lineages = OutcomeCount(
                    MigratedProducerOutcomeKind
                        .ObservedLineageCompressionLifeEventReceipt);
                int exact = ExactOutcomeCount();
                int owners = outcomes.Transaction.Capture().owners.Count;
                if (awards == 0 && lessons == 0 && promotions == 0
                    && funerals == 0 && lineages == 0
                    && exact == 0 && owners == 0)
                    return 0;
                if (awards == 1 && lessons == 0 && promotions == 0
                    && exact == 1 && owners == 1)
                    return 1;
                if (awards == 1 && lessons == 1 && promotions == 0
                    && exact == 2 && owners == 2)
                    return 2;
                if (awards == 1 && lessons == 1 && promotions == 1
                    && funerals == 0 && lineages == 0
                    && exact == 3 && owners == 3)
                    return 3;
                if (awards == 1 && lessons == 1 && promotions == 1
                    && funerals == 1 && lineages == 0
                    && exact == 4 && owners == 4)
                    return 4;
                if (awards == 1 && lessons == 1 && promotions == 1
                    && funerals == 1 && lineages == 1
                    && exact == 5 && owners == 5)
                    return 5;
                return -1;
            }
        }

        private int CurrentAbsoluteDay => Stage switch
        {
            0 => MentorshipAwardDay - 1,
            1 => MentorshipAwardDay,
            2 => LastLessonDay,
            3 => QuietPromotionDay,
            4 => FuneralDay,
            5 => LineageCompressionDay,
            _ => -1
        };

        private int OutcomeCount(MigratedProducerOutcomeKind kind)
        {
            string type = MigratedProducerOutcomeCatalog.Get(kind)
                .OutcomeTypeId.Value;
            return outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count(
                value => value != null
                    && string.Equals(
                        value.outcomeTypeId,
                        type,
                        StringComparison.Ordinal));
        }

        private int ExactOutcomeCount() => outcomes.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes.Count;

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                ownerId,
                OwnerKind,
                CausalOwnerRestoreKind,
                outcomes.Ledger);
            RequireFields(
                state,
                "observed career-arc owner state",
                "schemaVersion",
                "schema",
                "stage",
                "mentorId",
                "studentId",
                "academyId",
                "proficiencyId",
                "mentorshipAward",
                "career",
                "kinshipHousehold",
                "milestones",
                "society",
                "outcomeOwners");
            if ((int)state["schemaVersion"] != 2
                || !string.Equals(
                    (string)state["schema"],
                    DomainStateSchema,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["mentorId"], MentorId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["studentId"], StudentId,
                    StringComparison.Ordinal)
                || !string.Equals((string)state["academyId"], AcademyId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    (string)state["proficiencyId"],
                    ProficiencyId,
                    StringComparison.Ordinal)
                || state["career"] is not JObject savedCareer
                || state["kinshipHousehold"] is not JObject savedSocial
                || state["milestones"] is not JObject savedMilestones
                || state["society"] is not JObject savedSociety
                || state["outcomeOwners"] is not JObject savedOwners)
            {
                throw new InvalidOperationException(
                    "Observed career-arc checkpoint identity changed.");
            }

            careers.PublishRestore(careers.PrepareRestore(
                JsonUtility.FromJson<CharacterCareerWorldSaveData>(
                    savedCareer.ToString(Formatting.None))));
            social.PublishRestore(social.PrepareRestore(
                JsonUtility.FromJson<KinshipHouseholdWorldSaveData>(
                    savedSocial.ToString(Formatting.None))));
            campaign.PublishMilestones(campaign.PrepareMilestones(
                JsonUtility.FromJson<RunMilestoneWorldSaveData>(
                    savedMilestones.ToString(Formatting.None))));
            campaign.PublishSociety(campaign.PrepareSociety(
                JsonUtility.FromJson<SocietyEventWorldSaveData>(
                    savedSociety.ToString(Formatting.None))));
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                JsonUtility.FromJson<DungeonMigratedProducerOutcomeSaveData>(
                    savedOwners.ToString(Formatting.None))));
            if (state["mentorshipAward"] is JObject savedAward)
            {
                RequireFields(
                    savedAward,
                    "observed career-arc mentorship award",
                    "academyId",
                    "ledgerStackId",
                    "beforeContentRevision",
                    "afterContentRevision",
                    "sourceOperationId");
                mentorshipAward = CareerMentorshipAwardCommitReceipt.Restore(
                    new BuildingInstanceId((string)savedAward["academyId"]),
                    (string)savedAward["ledgerStackId"],
                    (long)savedAward["beforeContentRevision"],
                    (long)savedAward["afterContentRevision"],
                    (string)savedAward["sourceOperationId"]);
            }
            else if (state["mentorshipAward"]?.Type != JTokenType.Null)
            {
                throw new InvalidOperationException(
                    "Observed career-arc mentorship award payload changed shape.");
            }
            if ((int)state["stage"] != Stage)
            {
                throw new InvalidOperationException(
                    "Observed career-arc checkpoint stage disagrees with saved state.");
            }
        }

        private void ValidateState()
        {
            int stage = Stage;
            SocietyEventWorldSaveData society = campaign.CaptureSociety();
            int receipts = society.successfulLifeEventOperations.Count;
            int active = society.activeEvents.Count;
            int resolved = society.recentResolvedEvents.Count;
            int mentorTombstones = social.Tombstones.Count(value =>
                string.Equals(
                    value.characterId,
                    MentorId,
                    StringComparison.Ordinal));
            int lineageSummaries = social.Capture().kinship.lineageSummaries
                .Count(value => value != null && value.generation == 1);
            int lineageWorldFlags = campaign.WorldFlags.Count(value =>
                string.Equals(
                    value,
                    "life-event:story-compressed",
                    StringComparison.Ordinal));
            bool awardShape = stage == 0
                ? !mentorshipAward.IsValid && careers.Mentorships.Count == 0
                : mentorshipAward.IsValid
                    && mentorshipAward.AcademyBuildingId.Value == AcademyId
                    && mentorshipAward.LedgerStackId == CareerLedgerStackId
                    && careers.Mentorships.Count == 1;
            bool canonicalIds = society.successfulLifeEventOperations.All(value =>
                value != null
                && !string.IsNullOrWhiteSpace(value.sourceOperationId)
                && !string.IsNullOrWhiteSpace(value.canonicalPayload)
                && !string.IsNullOrWhiteSpace(value.occurrenceInstanceId));
            bool shape = stage switch
            {
                0 => receipts == 0 && active == 0 && resolved == 0
                    && mentorTombstones == 0 && lineageSummaries == 0
                    && lineageWorldFlags == 0,
                1 => receipts == 0 && active == 0 && resolved == 0
                    && mentorTombstones == 0 && lineageSummaries == 0
                    && lineageWorldFlags == 0,
                2 => receipts == 1 && active == 1 && resolved == 0
                    && lineageWorldFlags == 0
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.LastMentorshipLesson),
                3 => receipts == 2 && active == 1 && resolved == 1
                    && lineageWorldFlags == 0
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.LastMentorshipLesson)
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.ProficiencyPromotion),
                4 => receipts == 3 && active == 1 && resolved == 2
                    && mentorTombstones == 1 && lineageSummaries == 0
                    && lineageWorldFlags == 0
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.LastMentorshipLesson)
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.ProficiencyPromotion)
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.Funeral),
                5 => receipts == 4 && active == 1 && resolved == 3
                    && mentorTombstones == 0 && lineageSummaries == 1
                    && lineageWorldFlags == 1
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.LastMentorshipLesson)
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.ProficiencyPromotion)
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.Funeral)
                    && HasReceipt(
                        society,
                        ObservedLifeEventSourceKind.LineageCompression),
                _ => false
            };
            if (!shape || !awardShape || !canonicalIds)
            {
                throw new InvalidOperationException(
                    "Observed career source receipts, campaign state and exact ledger diverged: stage="
                    + stage
                    + "; receipts=" + receipts
                    + "; active=" + active
                    + "; resolved=" + resolved);
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(GetType().Name);
        }
    }

    private static bool HasReceipt(
        SocietyEventWorldSaveData society,
        ObservedLifeEventSourceKind kind) =>
        society.successfulLifeEventOperations.Count(value =>
            value != null && value.sourceKind == kind) == 1;

    private static ObservedLastLessonLifeEventReceipt
        CreateLastLessonReceipt(
            CareerMentorshipAwardCommitReceipt award)
    {
        CharacterId mentorId = new(MentorId);
        CharacterId studentId = new(StudentId);
        BuildingInstanceId academyId = new(AcademyId);
        CharacterProficiencyId proficiencyId = new(ProficiencyId);
        CareerMentorshipSnapshot mentorship = new(
            mentorId,
            studentId,
            academyId,
            proficiencyId,
            1,
            0,
            0f,
            0f);
        CharacterCareerSnapshot retirement = new(
            mentorId,
            retired: false,
            default,
            string.Empty,
            0,
            0f,
            RetirementScheduleStatus.Pending,
            "life-event:causal-career-arc:retirement-request",
            "one-more-season",
            LastLessonDay - 2,
            FuneralDay,
            0);
        CombatEquipmentInstance protectiveEquipment = new()
        {
            instanceId = "equipment:causal-career-arc:blast-coat",
            definitionId = "armor:powder-cuirass",
            ownerCharacterId = MentorId,
            worldState = CombatEquipmentWorldState.Equipped
        };
        return new ObservedLastLessonLifeEventReceipt(
            mentorship,
            retirement,
            protectiveEquipment,
            award,
            LastLessonDay,
            generation: 1);
    }

    private static ObservedQuietPromotionLifeEventReceipt
        CreateQuietPromotionReceipt()
    {
        long absoluteHour = (QuietPromotionDay - 1L)
            * GameCalendarRules.HoursPerDay;
        CharacterProficiencyAwardCommitReceipt source = new(
            CharacterProficiencyAwardKind.DirectExperience,
            new CharacterId(StudentId),
            new CharacterProficiencyId(ProficiencyId),
            beforeCurrentMilliExperience: 0L,
            afterCurrentMilliExperience: 100_000L,
            beforeLifetimeMilliExperience: 0L,
            afterLifetimeMilliExperience: 100_000L,
            absoluteHour);
        return new ObservedQuietPromotionLifeEventReceipt(
            source,
            QuietPromotionDay,
            generation: 1);
    }

    private static ObservedFuneralLifeEventReceipt CreateFuneralReceipt() =>
        new(
            "funeral-operation:causal-career-arc:mentor",
            new CharacterId(MentorId),
            MemorialId,
            FuneralDay,
            generation: 1,
            new[] { new CharacterId(StudentId) });

    private sealed class CareerEquipmentSlots :
        IDurableFacilityEquipmentSlotCommand
    {
        internal DurableFacilityEquipmentSlotSnapshot Snapshot
            { get; private set; }

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired)
        {
            const long sequence = 1L;
            Snapshot = new DurableFacilityEquipmentSlotSnapshot(
                desired,
                sequence,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    desired.Key,
                    sequence),
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    desired.Key,
                    sequence),
                DurableFacilityEquipmentFingerprint.CreateAssignment(desired),
                new DurableFacilityEquipmentCapacityProjection(
                    desired.CapacityPolicyKind,
                    new PhysicalMassGrams(1_000L),
                    1L,
                    new string('a', 64)),
                new[]
                {
                    new DurableFacilityEquipmentRequirementStatus(
                        desired.Requirements[0],
                        0,
                        1)
                });
            return new DurableFacilityEquipmentSlotResult(
                DurableFacilityEquipmentSlotStatus.Applied,
                Snapshot,
                string.Empty);
        }

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key) => new(
            DurableFacilityEquipmentSlotStatus.Replay,
            Snapshot,
            string.Empty);

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode) => throw new NotSupportedException();

        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() =>
            Array.Empty<DurableFacilityEquipmentSlotResult>();
    }

    private sealed class CareerEquipmentUse :
        IDurableFacilityEquipmentUseCommand
    {
        private readonly CareerEquipmentSlots slots;

        internal CareerEquipmentUse(CareerEquipmentSlots slots) =>
            this.slots = slots
                ?? throw new ArgumentNullException(nameof(slots));

        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect)
        {
            DurableFacilityEquipmentSlotSnapshot snapshot = slots.Snapshot
                ?? throw new InvalidOperationException(
                    "Career equipment slot was not reconciled.");
            DurableFacilityEquipmentRequirement requirement =
                snapshot.Assignment.Requirements[0];
            var before = new DurableFacilityEquipmentUseSubject(
                CareerLedgerStackId,
                1L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            if (!effect.TryPreflight(
                    snapshot,
                    requirement,
                    before,
                    wearAmount,
                    out string failureReason))
            {
                return new DurableFacilityEquipmentUseResult(
                    DurableFacilityEquipmentUseStatus.Deferred,
                    snapshot,
                    string.Empty,
                    failureReason);
            }

            var after = new DurableFacilityEquipmentUseSubject(
                before.StackId,
                2L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            if (!effect.TryCommit(
                    new DurableFacilityEquipmentUseContext(
                        snapshot,
                        requirement,
                        before,
                        after,
                        wearAmount),
                    out failureReason))
            {
                return new DurableFacilityEquipmentUseResult(
                    DurableFacilityEquipmentUseStatus.Deferred,
                    snapshot,
                    string.Empty,
                    failureReason);
            }

            return new DurableFacilityEquipmentUseResult(
                DurableFacilityEquipmentUseStatus.Applied,
                snapshot,
                before.StackId,
                string.Empty);
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IReadOnlyList<IProductionCausalActionFixture> fixtures;

        internal FixtureSet(
            IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures
                ?? throw new ArgumentNullException(nameof(fixtures));

        internal IProductionCausalActionFixture Get(string actionId) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                actionId,
                StringComparison.Ordinal));

        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static void RequireFields(
        JObject value,
        string label,
        params string[] expected)
    {
        string[] actual = value.Properties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                label + " fields mismatch. actual=" + string.Join(",", actual)
                + "; expected=" + string.Join(",", wanted));
        }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
#endif
