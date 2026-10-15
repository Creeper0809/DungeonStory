#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class FamilyPlanningPolicyReachabilityDebugScenarios
{
    [MenuItem("Dungeon Story/QA/V27/Family Planning Policy Reachability")]
    public static void RunFromMenu() => RunOrThrow();

    public static void RunCR01FromBatch()
    {
        if (!Application.isBatchMode
            || !string.Equals(
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_U0_CR01_FAMILY_PLANNING_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "C:R01 verification requires explicit isolated batch mode.");
        }

        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "C:R01 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string reportPath = Path.Combine(directory, "report.json");
        if (File.Exists(reportPath))
        {
            throw new InvalidOperationException(
                "C:R01 report already exists: " + reportPath);
        }

        var report = new JObject
        {
            ["rowId"] = "C:R01",
            ["passed"] = false,
            ["freshDefaultPlanned"] = false,
            ["playerAiCommandReached"] = false,
            ["g03OutcomeExact"] = false,
            ["actualProposalCommandReached"] = false,
            ["actualProductionProposalReached"] = false,
            ["reproductionCoreRegressionPassed"] = false,
            ["forcedAllowedFixtureUsed"] = false,
            ["faultRollback"] = false,
            ["sameValueAndSameDayNoOp"] = false,
            ["saveRestoreRoundTrip"] = false,
            ["oldSaveMigrationAdded"] = false,
            ["fullAuditExecuted"] = false,
            ["humanApprovalRecorded"] = false,
            ["trainingExecuted"] = false,
            ["sourceFiles"] = new JArray
            {
                "Assets/Scripts/Services/Character/FamilyPlanningPolicyCommandRuntime.cs",
                "Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs",
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Infrastructure/PolicyValueChangeOutcomeDefinition.cs",
                "Assets/Scripts/Services/Character/Editor/ReproductionProcessTransitionDebugScenarios.cs",
                "Assets/Scripts/Services/Character/Editor/FamilyPlanningPolicyReachabilityDebugScenarios.cs"
            }
        };

        try
        {
            RunCore(out FamilyPlanningPolicyEvidence evidence);
            report["productionProposalEvidence"] =
                ReproductionProcessTransitionDebugScenarios
                    .VerifyAllowedPolicyProductionProposal(new FixedSessionStateProvider());
            report["actualProductionProposalReached"] = true;
            Require(ReproductionProcessTransitionDebugScenarios.RunOrThrow(),
                "Shared reproduction fixture regression failed.");
            report["reproductionCoreRegressionPassed"] = true;
            report["freshDefaultPlanned"] = true;
            report["playerAiCommandReached"] = true;
            report["g03OutcomeExact"] = true;
            report["actualProposalCommandReached"] = true;
            report["faultRollback"] = true;
            report["sameValueAndSameDayNoOp"] = true;
            report["saveRestoreRoundTrip"] = true;
            report["policyOutcomeExample"] = JObject.FromObject(
                evidence.PolicyOutcome);
            report["allowedSavedState"] = new JObject
            {
                ["policy"] = evidence.AllowedSave.familyPlanningPolicy
                    .ToString(),
                ["revision"] = evidence.AllowedSave
                    .familyPlanningPolicyRevision,
                ["lastEvaluationDay"] = evidence.AllowedSave
                    .lastAllowedPolicyEvaluationDay,
                ["processCount"] = evidence.AllowedSave.processes.Count
            };
            report["proposalSavedState"] = new JObject
            {
                ["policy"] = evidence.ProposalSave.familyPlanningPolicy
                    .ToString(),
                ["revision"] = evidence.ProposalSave
                    .familyPlanningPolicyRevision,
                ["lastEvaluationDay"] = evidence.ProposalSave
                    .lastAllowedPolicyEvaluationDay,
                ["processCount"] = evidence.ProposalSave.processes.Count,
                ["proposal"] = JObject.FromObject(
                    evidence.ProposalSave.processes.Single())
            };
            report["passed"] = true;
            WriteCreateOnlyJson(reportPath, report);
            Debug.Log(
                "[AuditRemediation][C:R01] PASS default-planned=1; "
                + "player-command=1; g03=1; actual-proposal=1; forced-allowed=0");
        }
        catch (Exception exception)
        {
            report["failure"] = exception.GetType().Name
                + ": "
                + exception.Message;
            if (!File.Exists(reportPath))
                WriteCreateOnlyJson(reportPath, report);
            Debug.LogException(exception);
            throw;
        }
    }

    public static bool RunOrThrow() => RunCore(out _);

    public static void VerifyDomainFactFocused()
    {
        using Fixture fixture = new();
        fixture.RequirePolicy(FamilyPlanningPolicy.Planned, revision: 0L);

        fixture.Outcomes.FailNextPrepare = true;
        Require(!fixture.SetPolicy(FamilyPlanningPolicy.Allowed),
            "Injected fact reservation rejection changed the policy.");
        fixture.RequirePolicy(FamilyPlanningPolicy.Planned, revision: 0L);
        fixture.RequireNoPendingPolicyChange();
        fixture.RequireOutcomeCount(0);

        fixture.Outcomes.FailNextCommit = true;
        Require(!fixture.SetPolicy(FamilyPlanningPolicy.Allowed),
            "Explicit fact publication rejection changed the policy.");
        fixture.RequirePolicy(FamilyPlanningPolicy.Planned, revision: 0L);
        fixture.RequireNoPendingPolicyChange();
        fixture.RequireOutcomeCount(0);

        fixture.SetSessionAvailable(false);
        Require(!fixture.SetPolicy(FamilyPlanningPolicy.Allowed),
            "A policy change without an authoritative session day succeeded.");
        fixture.RequirePolicy(FamilyPlanningPolicy.Planned, revision: 0L);
        fixture.RequireNoPendingPolicyChange();
        fixture.RequireOutcomeCount(0);
        fixture.SetSessionAvailable(true);

        fixture.Outcomes.HideNextDelivery = true;
        Require(!fixture.SetPolicy(FamilyPlanningPolicy.Allowed),
            "A hidden durable acknowledgement was reported as complete.");
        fixture.RequirePolicy(FamilyPlanningPolicy.Allowed, revision: 1L);
        fixture.RequirePendingPolicyChange(
            FamilyPlanningPolicy.Planned,
            FamilyPlanningPolicy.Allowed,
            beforeRevision: 0L,
            afterRevision: 1L);
        fixture.RequireOutcomeCount(1);

        ReproductionWorldSaveData pendingSave = fixture.CaptureRoundTrip();
        Require(pendingSave.pendingFamilyPlanningPolicyChanges?.Count == 1,
            "Pending family-planning fact was lost from JSON save data.");
        fixture.Restart(pendingSave);
        fixture.RequirePendingPolicyChange(
            FamilyPlanningPolicy.Planned,
            FamilyPlanningPolicy.Allowed,
            beforeRevision: 0L,
            afterRevision: 1L);
        Require(fixture.ResumePendingPolicyChange(),
            "Startup-style pending family-planning replay did not reconcile.");
        fixture.RequireNoPendingPolicyChange();
        fixture.RequirePolicy(FamilyPlanningPolicy.Allowed, revision: 1L);
        fixture.RequireOutcomeCount(1);
        _ = fixture.RequireExactPolicyOutcome();
    }

    private static bool RunCore(out FamilyPlanningPolicyEvidence evidence)
    {
        evidence = null;
        VerifyDomainFactFocused();
        using Fixture fixture = new();
        fixture.RequirePolicy(FamilyPlanningPolicy.Planned, revision: 0L);

        fixture.PublishEndedDay(9);
        fixture.RequireProposalCount(0);

        fixture.Outcomes.FailNextPrepare = true;
        Require(!fixture.SetPolicy(FamilyPlanningPolicy.Allowed),
            "Injected prepare rejection unexpectedly changed the policy.");
        fixture.RequirePolicy(FamilyPlanningPolicy.Planned, revision: 0L);
        fixture.RequireOutcomeCount(0);

        fixture.Outcomes.FailNextCommit = true;
        Require(!fixture.SetPolicy(FamilyPlanningPolicy.Allowed),
            "Injected commit rejection unexpectedly changed the policy.");
        fixture.RequirePolicy(FamilyPlanningPolicy.Planned, revision: 0L);
        fixture.RequireOutcomeCount(0);

        Require(fixture.SetPolicy(FamilyPlanningPolicy.Allowed),
            "The player policy command rejected the Allowed transition.");
        fixture.RequirePolicy(FamilyPlanningPolicy.Allowed, revision: 1L);
        GameplayOutcomeSnapshot policyOutcome =
            fixture.RequireExactPolicyOutcome();
        fixture.RequireOutcomeCount(1);

        Require(fixture.SetPolicy(FamilyPlanningPolicy.Allowed),
            "A same-value policy command should be an idempotent success.");
        fixture.RequirePolicy(FamilyPlanningPolicy.Allowed, revision: 1L);
        fixture.RequireOutcomeCount(1);
        Require(!fixture.SetPolicy((FamilyPlanningPolicy)999),
            "An undefined family-planning policy was accepted.");

        ReproductionWorldSaveData allowedSave = fixture.CaptureRoundTrip();
        Require(allowedSave.version == ReproductionWorldSaveData.CurrentVersion
                && allowedSave.familyPlanningPolicyRevision == 1L,
            "The policy revision did not round-trip through JSON save data.");
        fixture.VerifyCurrentOnlyAndInvalidSaveHandling(allowedSave);
        fixture.Restart(allowedSave);
        fixture.RequirePolicy(FamilyPlanningPolicy.Allowed, revision: 1L);

        fixture.PublishEndedDay(9);
        fixture.RequireProposalCount(1);
        fixture.RequireProposalCommandReached();
        fixture.RequireLastEvaluationDay(10);

        ReproductionWorldSaveData proposalSave = fixture.CaptureRoundTrip();
        fixture.Restart(proposalSave);
        fixture.PublishEndedDay(9);
        fixture.RequireProposalCount(1);
        fixture.RequireLastEvaluationDay(10);
        fixture.RequireOutcomeCount(1);

        evidence = new FamilyPlanningPolicyEvidence(
            policyOutcome,
            allowedSave,
            proposalSave);

        Debug.Log(
            "FAMILY_PLANNING_POLICY_REACHABILITY=PASS "
            + "fresh-default-planned=1 player-command=1 g03-outcome=1 "
            + "prepare-reject-rollback=1 commit-reject-rollback=1 "
            + "same-value-noop=1 invalid-policy-rejected=1 "
            + "save-restore=2 legacy-rejected=" + (ReproductionWorldSaveData.CurrentVersion - 2) + " "
            + "real-day-ended-entry=2 proposal-command-reached=1 "
            + "planned-proposal-persisted=1 same-day-replay-noop=1");
        return true;
    }

    private static void WriteCreateOnlyJson(string path, JObject value)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(value.ToString());
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FamilyPlanningPolicyEvidence
    {
        internal FamilyPlanningPolicyEvidence(
            GameplayOutcomeSnapshot policyOutcome,
            ReproductionWorldSaveData allowedSave,
            ReproductionWorldSaveData proposalSave)
        {
            PolicyOutcome = policyOutcome
                ?? throw new ArgumentNullException(nameof(policyOutcome));
            AllowedSave = allowedSave
                ?? throw new ArgumentNullException(nameof(allowedSave));
            ProposalSave = proposalSave
                ?? throw new ArgumentNullException(nameof(proposalSave));
        }

        internal GameplayOutcomeSnapshot PolicyOutcome { get; }
        internal ReproductionWorldSaveData AllowedSave { get; }
        internal ReproductionWorldSaveData ProposalSave { get; }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ResourceCharacterSpeciesCatalog definitions;
        private readonly CharacterSpeciesId speciesId;
        private readonly FixedLifeQuery life;
        private readonly FixedKinshipQuery kinship = new();
        private readonly FixedCharacterWorld world;
        private readonly FixedSessionStateProvider session = new();
        private readonly GameEventBus events = new();
        private readonly GameObject[] actorObjects;
        private readonly CharacterSO[] actorData;
        private readonly DomainFactDispatcher domainFacts;
        private ReproductionRuntime reproduction;
        private FamilyPlanningPolicyCommandRuntime policyCommand;
        private ProposalPersistingCommand proposalCommand;
        private ReproductionProposalAdapter proposalAdapter;

        internal Fixture()
        {
            ResourceGameContentCatalog content = new(
                new UnityGameContentRootLoader());
            definitions = new ResourceCharacterSpeciesCatalog(content);
            CharacterSO authored = content.GetAll<CharacterSO>()
                .Where(value => value != null
                    && value.species != null
                    && value.species.reproduction != null
                    && value.species.reproduction.mode
                        != ReproductionMode.GolemAssembly
                    && value.role == CharacterRole.Regular)
                .OrderBy(value => value.DefinitionId.Value, StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No authored non-golem reproductive character is available.");
            ReproductionMode mode = authored.species.reproduction.mode;
            (ReproductiveRole firstRole, ReproductiveRole secondRole) =
                Roles(mode);
            speciesId = new CharacterSpeciesId(authored.SpeciesTag);

            actorData = new[]
            {
                Clone(authored, "가족계획 검증자 하나"),
                Clone(authored, "가족계획 검증자 둘")
            };
            actorObjects = new[]
            {
                CreateActor(
                    "Family Planning Actor A",
                    actorData[0],
                    "character:qa:family-planning-a",
                    speciesId,
                    firstRole),
                CreateActor(
                    "Family Planning Actor B",
                    actorData[1],
                    "character:qa:family-planning-b",
                    speciesId,
                    secondRole)
            };
            CharacterActor[] actors = actorObjects
                .Select(value => value.GetComponent<CharacterActor>())
                .ToArray();
            world = new FixedCharacterWorld(actors);
            SpeciesLifeHistoryDefinition history =
                definitions.RequireLifeHistory(speciesId);
            int adultDays = checked(
                (history.AdultAgeYears + 1) * GameCalendarRules.DaysPerYear);
            life = new FixedLifeQuery(actors.Select(actor =>
                new CharacterLifeRecord(
                    CharacterPersistentIdentity.Require(actor),
                    speciesId,
                    adultDays,
                    adultDays,
                    birthdayDayOfYear: 1,
                    lifeHistory: history)));

            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new PolicyValueChangeOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new PolicyValueChangeOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(32, 0, 128),
                new GameplayOutcomeRunId("run:qa:family-planning-policy"),
                1L);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                registry,
                events);
            domainFacts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(recorder, Ledger);
            domainFacts.Subscribe(
                "fixture:family-planning-policy-changed",
                new GameplayDomainFactObserver<
                    FamilyPlanningPolicyChangedFact,
                    PolicyValueChangeReceipt>(
                    FamilyPlanningPolicyFactMapping.Capture,
                    registry,
                    Ledger,
                    recorder,
                    commits));
            Outcomes = new FaultingDomainFactPublisher(domainFacts);
            Restart(new ReproductionWorldSaveData());
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal FaultingDomainFactPublisher Outcomes { get; }

        internal bool SetPolicy(FamilyPlanningPolicy policy) =>
            policyCommand.TrySet(
                policy,
                "character-summary-ui",
                out _);

        internal bool ResumePendingPolicyChange()
        {
            policyCommand.Tick();
            return !reproduction.HasPendingFamilyPlanningPolicyChange;
        }

        internal void SetSessionAvailable(bool available) =>
            session.Available = available;

        internal void PublishEndedDay(int endedDay) =>
            events.Publish(new OperatingDayEndedEvent(endedDay));

        internal ReproductionWorldSaveData CaptureRoundTrip() =>
            JsonUtility.FromJson<ReproductionWorldSaveData>(
                JsonUtility.ToJson(reproduction.Capture()));

        internal void Restart(ReproductionWorldSaveData save)
        {
            proposalAdapter?.Dispose();
            reproduction = new ReproductionRuntime(
                new DungeonRuntimeAggregateRootStore(),
                definitions,
                life,
                kinship,
                world,
                new RandomStreamProvider(27031));
            reproduction.PublishRestore(reproduction.PrepareRestore(save));
            policyCommand = new FamilyPlanningPolicyCommandRuntime(
                reproduction,
                reproduction,
                Outcomes,
                session);
            proposalCommand = new ProposalPersistingCommand(
                reproduction,
                definitions,
                speciesId);
            proposalAdapter = new ReproductionProposalAdapter(
                reproduction,
                proposalCommand,
                world,
                life,
                events);
            proposalAdapter.Start();
        }

        internal void RequirePolicy(
            FamilyPlanningPolicy expected,
            long revision)
        {
            Require(reproduction.FamilyPlanningPolicy == expected,
                $"Expected policy {expected}, got {reproduction.FamilyPlanningPolicy}.");
            Require(reproduction.FamilyPlanningPolicyRevision == revision,
                "Family-planning policy revision differs from the expected value.");
        }

        internal void RequireOutcomeCount(int expected)
        {
            int actual = Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(),
                    OutcomeFilter.All)
                .Items.Count;
            Require(actual == expected,
                $"Expected {expected} policy outcomes, got {actual}.");
        }

        internal void RequirePendingPolicyChange(
            FamilyPlanningPolicy before,
            FamilyPlanningPolicy after,
            long beforeRevision,
            long afterRevision)
        {
            FamilyPlanningPolicyPendingChangeSaveData pending =
                reproduction.Capture().pendingFamilyPlanningPolicyChanges
                    .SingleOrDefault();
            Require(pending != null
                    && pending.before == before
                    && pending.after == after
                    && pending.beforeRevision == beforeRevision
                    && pending.afterRevision == afterRevision
                    && pending.source == "character-summary-ui"
                    && pending.absoluteDay == 1,
                "Pending family-planning fact does not match its exact transition.");
        }

        internal void RequireNoPendingPolicyChange() =>
            Require(reproduction.Capture().pendingFamilyPlanningPolicyChanges.Count == 0,
                "Family-planning policy retained an unexpected pending fact.");

        internal GameplayOutcomeSnapshot RequireExactPolicyOutcome()
        {
            GameplayOutcomeSnapshot row = Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(),
                    OutcomeFilter.All)
                .Items.Select(value => value.Exact)
                .Single(value => value != null
                    && value.outcomeTypeId
                        == PolicyValueChangeOutcomeIds.Changed.Value);
            Require(row.producerId == PolicyValueChangeOutcomeIds.ProducerId
                    && row.ownerRevision == 1L
                    && row.facts.Count == 6
                    && row.metrics.Count == 2,
                "The G03 family-planning policy outcome shape is invalid.");
            Require(row.facts[0].value == "family-planning"
                    && row.facts[1].value == "dungeon"
                    && row.facts[2].value == "planned"
                    && row.facts[3].value == "allowed"
                    && row.facts[4].value == "character-summary-ui"
                    && row.facts[5].value
                        == "policy-value:family-planning:1"
                    && row.operationId
                        == "policy-value:family-planning:1",
                "The G03 family-planning facts are not authoritative.");
            return row;
        }

        internal void RequireProposalCount(int expected)
        {
            Require(reproduction.Processes.Count == expected,
                $"Expected {expected} planned proposals, got {reproduction.Processes.Count}.");
            if (expected > 0)
            {
                Require(reproduction.Processes.All(value =>
                        value.Status == ReproductionProcessStatus.Planned),
                    "Allowed-policy evaluation did not persist a planned proposal.");
            }
        }

        internal void RequireProposalCommandReached() =>
            Require(proposalCommand.SuccessfulPlans == 1,
                "The real day-ended proposal entry did not reach the plan command.");

        internal void RequireLastEvaluationDay(int expected) =>
            Require(reproduction.LastAllowedPolicyEvaluationDay == expected,
                "Allowed-policy evaluation day was not persisted.");

        internal void VerifyCurrentOnlyAndInvalidSaveHandling(
            ReproductionWorldSaveData source)
        {
            for (int version = 2; version < ReproductionWorldSaveData.CurrentVersion; version++)
            {
                ReproductionWorldSaveData legacy = JsonUtility.FromJson<ReproductionWorldSaveData>(
                    JsonUtility.ToJson(source));
                legacy.version = version;
                bool rejected = false;
                try { reproduction.PrepareRestore(legacy); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "A legacy reproduction save was migrated into the current death-reference schema.");
            }

            ReproductionWorldSaveData invalid =
                JsonUtility.FromJson<ReproductionWorldSaveData>(
                    JsonUtility.ToJson(source));
            invalid.familyPlanningPolicyRevision = -1L;
            try
            {
                reproduction.PrepareRestore(invalid);
            }
            catch (InvalidOperationException)
            {
                return;
            }
            throw new InvalidOperationException(
                "A negative family-planning policy revision was restored.");
        }

        public void Dispose()
        {
            proposalAdapter?.Dispose();
            domainFacts.Dispose();
            events.Clear();
            foreach (GameObject actorObject in actorObjects)
            {
                if (actorObject != null)
                    UnityEngine.Object.DestroyImmediate(actorObject);
            }
            foreach (CharacterSO data in actorData)
            {
                if (data != null)
                    UnityEngine.Object.DestroyImmediate(data);
            }
        }

        private static CharacterSO Clone(CharacterSO authored, string name)
        {
            CharacterSO clone = UnityEngine.Object.Instantiate(authored);
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.characterType = CharacterType.NPC;
            clone.characterName = name;
            return clone;
        }

        private static GameObject CreateActor(
            string objectName,
            CharacterSO data,
            string persistentId,
            CharacterSpeciesId speciesId,
            ReproductiveRole role)
        {
            GameObject actorObject =
                CharacterAiPlanDebugFixtures.CreateActorObject(objectName);
            CharacterAiEditorTestDependencies.Inject(actorObject);
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(new CharacterId(persistentId));
            actor.Initialize(
                data,
                new CharacterSpawnRequest(
                    data.DefinitionId,
                    speciesId,
                    data.VisualVariantId,
                    role,
                    Array.Empty<CharacterTraitId>()));
            actor.characterType = CharacterType.NPC;
            return actorObject;
        }

        private static (ReproductiveRole, ReproductiveRole) Roles(
            ReproductionMode mode) => mode switch
        {
            ReproductionMode.Pregnancy =>
                (ReproductiveRole.Carrier, ReproductiveRole.Contributor),
            ReproductionMode.Egg =>
                (ReproductiveRole.Layer, ReproductiveRole.Fertilizer),
            ReproductionMode.Spore =>
                (ReproductiveRole.SporeContributor,
                    ReproductiveRole.SporeContributor),
            ReproductionMode.CoreDivision =>
                (ReproductiveRole.DivisionCore, ReproductiveRole.DivisionCore),
            _ => throw new InvalidOperationException(
                "The focused proposal fixture requires a paired reproduction mode.")
        };
    }

    private sealed class ProposalPersistingCommand : IReproductionCommand
    {
        private readonly IReproductionService reproduction;
        private readonly IReproductionDefinitionCatalog definitions;
        private readonly CharacterSpeciesId speciesId;

        internal ProposalPersistingCommand(
            IReproductionService reproduction,
            IReproductionDefinitionCatalog definitions,
            CharacterSpeciesId speciesId)
        {
            this.reproduction = reproduction;
            this.definitions = definitions;
            this.speciesId = speciesId;
        }

        internal int SuccessfulPlans { get; private set; }

        public bool TryPlan(
            ReproductionPlanRequest request,
            out string processId,
            out DomainFailure failure)
        {
            processId = string.Empty;
            failure = DomainFailure.None;
            try
            {
                CharacterId first = new(request.FirstParentId);
                CharacterId second = new(request.SecondParentId);
                ReproductionDefinition definition =
                    definitions.RequireReproduction(speciesId);
                processId = "reproduction:" + request.ActionId;
                reproduction.AddProcess(new ReproductionProcess(
                    processId,
                    first,
                    second,
                    first,
                    speciesId,
                    definition,
                    request.AbsoluteDay,
                    crossLineageIncubatorUsed: false,
                    supportFacilityInstanceId:
                        "building-instance:qa:family-planning",
                    expressedTraitIds: Array.Empty<string>(),
                    latentTraitIds: Array.Empty<string>(),
                    aptitudes: Array.Empty<InnateAptitudeSaveData>(),
                    startActive: false));
                SuccessfulPlans++;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException)
            {
                processId = string.Empty;
                failure = new DomainFailure(
                    FailureCode.ExternalInfluenceUnavailable,
                    exception.GetType().Name);
                return false;
            }
        }

        public bool TryStart(string processId, out DomainFailure failure)
        {
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable);
            return false;
        }

        public bool TryStart(
            string processId,
            bool useFertilityTreatment,
            out DomainFailure failure) => TryStart(processId, out failure);

    }

    private sealed class FixedLifeQuery : ICharacterLifeQuery
    {
        private readonly Dictionary<CharacterId, CharacterLifeRecord> records;

        internal FixedLifeQuery(IEnumerable<CharacterLifeRecord> records) =>
            this.records = (records ?? Array.Empty<CharacterLifeRecord>())
                .ToDictionary(value => value.CharacterId);

        public int Version => 1;
        public IReadOnlyCollection<CharacterLifeRecord> Records =>
            records.Values.ToArray();
        public bool TryGet(
            CharacterId characterId,
            out CharacterLifeRecord record) =>
            records.TryGetValue(characterId, out record);
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery
    {
        internal FixedCharacterWorld(IReadOnlyList<CharacterActor> actors) =>
            Characters = actors ?? Array.Empty<CharacterActor>();

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
    }

    private sealed class FixedKinshipQuery : IKinshipQuery
    {
        public IReadOnlyCollection<CharacterTombstoneSaveData> Tombstones =>
            Array.Empty<CharacterTombstoneSaveData>();
        public IReadOnlyList<CharacterId> GetParents(
            CharacterId child,
            bool includeAdoptive) => Array.Empty<CharacterId>();
        public IReadOnlyList<CharacterId> GetChildren(
            CharacterId parent,
            bool includeAdoptive) => Array.Empty<CharacterId>();
        public int GetGeneration(CharacterId characterId) => 0;
        public bool IsAncestor(
            CharacterId possibleAncestor,
            CharacterId descendant,
            int maximumDepth) => false;
        public bool IsSibling(CharacterId left, CharacterId right) => false;
        public KinshipRestriction GetPartnershipOrReproductionRestriction(
            CharacterId left,
            CharacterId right) => KinshipRestriction.None;
        public CharacterId GetPartner(CharacterId characterId) => default;
        public CharacterId GetGuardian(CharacterId child) => default;
        public bool TryGetTombstone(
            CharacterId characterId,
            out CharacterTombstoneSaveData tombstone)
        {
            tombstone = null;
            return false;
        }
    }

    private sealed class FixedSessionStateProvider : IGameSessionStateProvider
    {
        private readonly GameSessionState state = new(
            startingMoney: 0,
            startingDay: 1,
            startingGameSpeed: 1);

        internal bool Available { get; set; } = true;

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            if (!Available)
            {
                gameData = null;
                return false;
            }
            gameData = state;
            return true;
        }
    }

    internal sealed class FaultingDomainFactPublisher : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;

        internal FaultingDomainFactPublisher(IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        internal bool FailNextPrepare { get; set; }
        internal bool FailNextCommit { get; set; }
        internal bool HideNextDelivery { get; set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failureReason)
        {
            if (FailNextPrepare)
            {
                FailNextPrepare = false;
                lease = default;
                failureReason = "injected-family-planning-prepare-rejection";
                return false;
            }
            return inner.TryReserve(proposed, out lease, out failureReason);
        }

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (!FailNextCommit)
            {
                DomainFactDelivery delivered = inner.Publish(lease, actual);
                if (!HideNextDelivery || !delivered.Completed)
                    return delivered;
                HideNextDelivery = false;
                return new DomainFactDelivery(
                    DomainFactDeliveryPhase.AcceptedPendingDelivery,
                    delivered.OperationKey,
                    delivered.PublicationId,
                    delivered.Fingerprint,
                    "injected-family-planning-delivery-hidden");
            }
            FailNextCommit = false;
            inner.Cancel(lease);
            return DomainFactDelivery.Reject(
                "injected-family-planning-commit-rejection");
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            inner.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }
}
#endif
