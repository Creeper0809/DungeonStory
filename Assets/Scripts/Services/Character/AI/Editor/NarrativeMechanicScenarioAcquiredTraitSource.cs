#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Captures controlled acquired-trait fixtures from the authored Unity assets.
/// Every positive packet is produced and revalidated by the gameplay authority;
/// the fixture does not claim that its ledger was observed in natural play.
/// </summary>
public sealed class NarrativeMechanicScenarioAcquiredTraitSource
    : INarrativeMechanicScenarioSource
{
    private const string AuthoredRoot = "Assets/Resources/SO/V25/AcquiredTraits";
    private const string SourcePath =
        "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioAcquiredTraitSource.cs";
    private const string ProducerIdentifier =
        "CharacterAcquiredTraitRequestPacketAuthority.TryBuild";
    private const string ValidatorIdentifier =
        "CharacterAcquiredTraitStateValidator.ValidateWithDefinitions+"
        + "CharacterAcquiredTraitRequestPacketAuthority.TryValidate+"
        + "CharacterAcquiredTraitResponseAuthority.TryValidate";

    private readonly CharacterAcquiredTraitSettingsSO suppliedSettings;
    private readonly CharacterAcquiredTraitModuleSO[] suppliedModules;
    private readonly NarrativeMechanicScenarioSetKind scenarioSetKind;

    public NarrativeMechanicScenarioAcquiredTraitSource()
        : this(NarrativeMechanicScenarioSetKind.Calibration)
    {
    }

    public NarrativeMechanicScenarioAcquiredTraitSource(
        NarrativeMechanicScenarioSetKind scenarioSetKind)
    {
        this.scenarioSetKind = scenarioSetKind;
    }

    internal NarrativeMechanicScenarioAcquiredTraitSource(
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> modules)
        : this(settings, modules, NarrativeMechanicScenarioSetKind.Calibration)
    {
    }

    internal NarrativeMechanicScenarioAcquiredTraitSource(
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> modules,
        NarrativeMechanicScenarioSetKind scenarioSetKind)
    {
        suppliedSettings = settings;
        suppliedModules = modules?.ToArray();
        this.scenarioSetKind = scenarioSetKind;
    }

    public string ProfileId => NarrativeMechanicScenarioProfiles.AcquiredTrait;

    public NarrativeMechanicScenarioSourceCapture CaptureScenarios()
    {
        ValidateScenarioSetKind();
        ResolveAuthority(
            out CharacterAcquiredTraitSettingsSO settings,
            out CharacterAcquiredTraitModuleSO[] modules);
        ValidateAuthoredAuthority(settings, modules);

        Dictionary<string, CharacterAcquiredTraitModuleSO> modulesById = modules
            .ToDictionary((module) => module.ModuleId, StringComparer.Ordinal);
        ScenarioPlan[] plans = IsUnseenNamingEvaluation
            ? BuildUnseenNamingEvaluationPlans()
            : BuildPlans();
        if (plans.Length != 20)
        {
            throw Unsupported(
                "acquired-trait-scenario-count",
                $"AcquiredTrait requires exactly 20 positive fixtures; found {plans.Length}.");
        }
        RequireEvaluationMetadata(plans);

        NarrativeMechanicScenario[] positives = plans
            .Select((plan, index) => CapturePositive(
                plan,
                index + 1,
                settings,
                modules,
                modulesById))
            .ToArray();
        NarrativeMechanicScenario[] negatives = IsUnseenNamingEvaluation
            ? Array.Empty<NarrativeMechanicScenario>()
            : CaptureNegativeScenarios(
                settings,
                modules,
                modulesById);
        return new NarrativeMechanicScenarioSourceCapture(
            ProfileId,
            positives,
            negatives,
            CaptureRelevantPaths(settings, modules),
            allowEmptyNegativeScenarios: IsUnseenNamingEvaluation);
    }

    private static NarrativeMechanicScenario CapturePositive(
        ScenarioPlan plan,
        int ordinal,
        CharacterAcquiredTraitSettingsSO settings,
        CharacterAcquiredTraitModuleSO[] modules,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById)
    {
        string scenarioId = $"acquired-trait-authority-{ordinal:D2}-{plan.Slug}";
        string targetId = $"character:fixture:acquired-trait:{ordinal:D2}";
        using ControlledActorFixture actor = new(
            850000 + ordinal,
            $"Acquired trait fixture {ordinal:D2}",
            targetId);
        CharacterProgression progression = actor.Actor.Progression;
        CharacterNarrativeLedger ledger = progression.NarrativeLedger;
        if (plan.LedgerEvents.Length > 0)
        {
            for (int eventIndex = 0; eventIndex < plan.LedgerEvents.Length; eventIndex++)
            {
                LedgerEventPlan ledgerEvent = plan.LedgerEvents[eventIndex];
                for (int repetition = 0; repetition < ledgerEvent.Repetitions; repetition++)
                {
                    progression.RecordNarrative(
                        ledgerEvent.Domain,
                        BuildLedgerEventFactId(scenarioId, eventIndex, ledgerEvent),
                        BuildLedgerEventSubjectId(scenarioId, ledgerEvent),
                        ledgerEvent.Outcome,
                        value: ledgerEvent.Value,
                        day: 200 + ordinal + ledgerEvent.DayOffset + repetition);
                }
            }
        }
        else
        {
            for (int factOrdinal = 1; factOrdinal <= plan.Milestone; factOrdinal++)
            {
                CharacterNarrativeDomain domain = plan.Domains[(factOrdinal - 1) % plan.Domains.Length];
                progression.RecordNarrative(
                    domain,
                    BuildFactId(scenarioId, domain, factOrdinal),
                    $"fixture-subject:{scenarioId}:{factOrdinal:D2}",
                    CharacterActivityOutcomes.Completed,
                    value: 1f,
                    day: 100 + ordinal);
            }
        }

        int score = CharacterAcquiredTraitExperienceScore.Require(ledger);
        if (score != plan.Milestone)
        {
            throw Unsupported(
                "acquired-trait-fixture-score",
                $"Scenario '{scenarioId}' expected score {plan.Milestone}, but produced {score}.");
        }

        CharacterAcquiredTraitAggregateState state = BuildPriorState(
            scenarioId,
            plan,
            ledger,
            modulesById);
        IReadOnlyList<CharacterAcquiredTraitValidationIssue> stateIssues =
            CharacterAcquiredTraitStateValidator.ValidateWithDefinitions(
                state,
                ledger,
                settings,
                modules);
        if (stateIssues.Count > 0)
        {
            throw Unsupported(
                "acquired-trait-fixture-state-rejected",
                $"Scenario '{scenarioId}' state was rejected: {string.Join(" | ", stateIssues)}");
        }

        string requestId = $"request:fixture:acquired-trait:{ordinal:D2}";
        string requestKey = $"request-key:fixture:acquired-trait:{ordinal:D2}";
        string[] rawEvidenceFactIds = ledger.Facts
            .Select((fact) => fact.factId)
            .OrderBy((factId) => factId, StringComparer.Ordinal)
            .ToArray();
        IReadOnlyList<NarrativeLedgerPublicationDescriptor> evaluationPublications =
            plan.EvaluationMetadata == null
                ? null
                : BuildEvaluationLedgerPublications(plan, scenarioId, ledger);
        NarrativePublicContextMaterial publicMaterial = evaluationPublications == null
            ? CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(
                progression,
                rawEvidenceFactIds)
            : CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(
                progression,
                rawEvidenceFactIds,
                evaluationPublications);
        IReadOnlyList<string> evidenceFactIds =
            CharacterAcquiredTraitPromptBuilder.BuildProjectedEvidenceFactIds(
                publicMaterial,
                rawEvidenceFactIds);
        CharacterAcquiredTraitSubmissionCommand command =
            new CharacterAcquiredTraitSubmissionCommand(
                targetId,
                requestId,
                requestKey,
                plan.Milestone,
                evidenceFactIds,
                state.revision,
                NarrativeInferenceTimestamp.FromGameTick(1000L + ordinal));

        if (!CharacterAcquiredTraitRequestPacketAuthority.TryBuild(
                command,
                ledger,
                state,
                settings,
                modules,
                out CharacterAcquiredTraitRequestPacketDto packet,
                out CharacterAcquiredTraitInferenceIssueCode buildIssue,
                out string buildError))
        {
            throw Unsupported(
                "acquired-trait-fixture-build-rejected",
                $"Scenario '{scenarioId}' producer rejected the fixture as {buildIssue}: {buildError}");
        }
        if (!CharacterAcquiredTraitRequestPacketAuthority.TryValidate(
                packet,
                ledger,
                state,
                settings,
                modules,
                out CharacterAcquiredTraitInferenceIssueCode requestIssue,
                out string requestError))
        {
            throw Unsupported(
                "acquired-trait-fixture-request-rejected",
                $"Scenario '{scenarioId}' validator rejected its produced packet as {requestIssue}: {requestError}");
        }

        CharacterAcquiredTraitCombinationPacketDto selected = packet.combinationOptions.First();
        string responseJson = BuildAcceptedResponseJson(
            selected.combinationId,
            packet.evidenceFactIds[0]);
        if (!CharacterAcquiredTraitResponseAuthority.TryValidate(
                packet,
                responseJson,
                ledger,
                state,
                settings,
                modules,
                out _,
                out CharacterAcquiredTraitCombinationPacketDto validatedSelection,
                out CharacterAcquiredTraitInferenceIssueCode responseIssue,
                out string responseError))
        {
            throw Unsupported(
                "acquired-trait-fixture-response-rejected",
                $"Scenario '{scenarioId}' response validator rejected the fixture as {responseIssue}: {responseError}");
        }
        RequireProductionEnvelope(
            scenarioId,
            publicMaterial,
            CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
                progression,
                packet,
                publicMaterial));
        NarrativeMechanicScenarioPublicContext publicContext =
            NarrativeMechanicScenarioPublicContextSerializer.Serialize(publicMaterial);

        return new NarrativeMechanicScenario(
            scenarioId,
            NarrativeMechanicScenarioProfiles.AcquiredTrait,
            ToRequestJson(packet),
            new NarrativeMechanicCatalogCanonicalJsonArray(
                packet.combinationOptions.Select(ToCombinationJson)),
            publicContext.TargetPersistentId,
            publicContext,
            BuildEffectDescriptions(packet, modulesById),
            BuildAuthorityContext(
                settings,
                state,
                score,
                packet,
                validatedSelection,
                responseJson),
            BuildFixtureInput(plan, command, ledger, state),
            ProducerIdentifier,
            ValidatorIdentifier,
            accepted: true,
            failureReason: string.Empty,
            diversityAxes: new[]
            {
                Axis("manifestationMilestone", plan.Milestone.ToString(CultureInfo.InvariantCulture)),
                Axis("evidenceDomains", string.Join("+", plan.Domains.Select((value) => value.ToString()))),
                Axis("priorState", plan.StateAxis)
            },
            evaluationMetadata: plan.EvaluationMetadata);
    }

    private static NarrativeMechanicScenario[] CaptureNegativeScenarios(
        CharacterAcquiredTraitSettingsSO settings,
        CharacterAcquiredTraitModuleSO[] modules,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById)
    {
        const string battle = "acquired-trait:module:battle-temper";
        const string steady = "acquired-trait:module:steady-hands";
        const string survival = "acquired-trait:module:survival-instinct";
        const string ritual = "acquired-trait:module:ritual-attunement";
        const string rhythm = "acquired-trait:module:work-rhythm";

        List<NarrativeMechanicScenario> negatives = new List<NarrativeMechanicScenario>();

        PacketFixture identity = BuildPacketFixture(
            "acquired-trait-negative-combination-identity",
            Plan("negative-combination-identity", 3,
                Domains(CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Combat,
                    CharacterNarrativeDomain.Survival)),
            101,
            settings,
            modules,
            modulesById);
        CharacterAcquiredTraitRequestPacketDto invalidIdentity = identity.Packet.Clone();
        invalidIdentity.combinationOptions[0].combinationId =
            CharacterAcquiredTraitCombinationIdentity.Prefix + "invalid";
        invalidIdentity.candidatePacketHash =
            CharacterAcquiredTraitRequestPacketAuthority.ComputeHash(invalidIdentity);
        negatives.Add(CaptureRejectedPacket(
            identity,
            invalidIdentity,
            "forged-combination-identity",
            CharacterAcquiredTraitInferenceIssueCode.InvalidCombination,
            settings,
            modules,
            modulesById));

        PacketFixture budget = BuildPacketFixture(
            "acquired-trait-negative-budget",
            Plan("negative-budget", 3,
                Domains(CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Combat,
                    CharacterNarrativeDomain.Survival)),
            102,
            settings,
            modules,
            modulesById);
        CharacterAcquiredTraitRequestPacketDto overBudget = budget.Packet.Clone();
        overBudget.combinationOptions = new List<CharacterAcquiredTraitCombinationPacketDto>
        {
            BuildCombination(
                new[]
                {
                    RequireModule(modulesById, steady),
                    RequireModule(modulesById, ritual),
                    RequireModule(modulesById, rhythm)
                },
                overBudget.eligibleDomains)
        };
        overBudget.candidatePacketHash =
            CharacterAcquiredTraitRequestPacketAuthority.ComputeHash(overBudget);
        negatives.Add(CaptureRejectedPacket(
            budget,
            overBudget,
            "gate-budget-exceeded",
            CharacterAcquiredTraitInferenceIssueCode.BudgetExceeded,
            settings,
            modules,
            modulesById));

        PacketFixture conflict = BuildPacketFixture(
            "acquired-trait-negative-conflict",
            Plan("negative-conflict", 20,
                Domains(CharacterNarrativeDomain.Combat, CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Survival)),
            103,
            settings,
            modules,
            modulesById);
        CharacterAcquiredTraitRequestPacketDto conflicting = conflict.Packet.Clone();
        conflicting.combinationOptions[0] = BuildCombination(
            new[] { RequireModule(modulesById, battle), RequireModule(modulesById, survival) },
            conflicting.eligibleDomains);
        conflicting.candidatePacketHash =
            CharacterAcquiredTraitRequestPacketAuthority.ComputeHash(conflicting);
        negatives.Add(CaptureRejectedPacket(
            conflict,
            conflicting,
            "conflict-group",
            CharacterAcquiredTraitInferenceIssueCode.Conflict,
            settings,
            modules,
            modulesById));

        PacketFixture active = BuildPacketFixture(
            "acquired-trait-negative-active-module",
            Plan("negative-active-module", 8, Domains(CharacterNarrativeDomain.Work),
                Prior(3, steady, false)),
            104,
            settings,
            modules,
            modulesById);
        CharacterAcquiredTraitRequestPacketDto activeModule = active.Packet.Clone();
        activeModule.combinationOptions[0] = BuildCombination(
            new[] { RequireModule(modulesById, steady) },
            activeModule.eligibleDomains);
        activeModule.candidatePacketHash =
            CharacterAcquiredTraitRequestPacketAuthority.ComputeHash(activeModule);
        negatives.Add(CaptureRejectedPacket(
            active,
            activeModule,
            "active-module-already-present",
            CharacterAcquiredTraitInferenceIssueCode.ActiveModuleAlreadyPresent,
            settings,
            modules,
            modulesById));

        PacketFixture forgedEvidence = BuildPacketFixture(
            "acquired-trait-negative-evidence-forgery",
            Plan("negative-evidence-forgery", 3, Domains(CharacterNarrativeDomain.Work)),
            105,
            settings,
            modules,
            modulesById);
        CharacterAcquiredTraitRequestPacketDto forged = forgedEvidence.Packet.Clone();
        forged.evidenceFactIds[0] =
            "fixture:acquired-trait-negative-evidence-forgery:not-in-ledger";
        forged.evidenceFactIds = forged.evidenceFactIds
            .OrderBy((value) => value, StringComparer.Ordinal)
            .ToList();
        forged.candidatePacketHash =
            CharacterAcquiredTraitRequestPacketAuthority.ComputeHash(forged);
        negatives.Add(CaptureRejectedPacket(
            forgedEvidence,
            forged,
            "evidence-forgery",
            CharacterAcquiredTraitInferenceIssueCode.EvidenceForgery,
            settings,
            modules,
            modulesById));

        PacketFixture duplicateEvidence = BuildPacketFixture(
            "acquired-trait-negative-duplicate-evidence",
            Plan("negative-duplicate-evidence", 3, Domains(CharacterNarrativeDomain.Work)),
            106,
            settings,
            modules,
            modulesById);
        CharacterAcquiredTraitRequestPacketDto duplicate = duplicateEvidence.Packet.Clone();
        duplicate.evidenceFactIds.Add(duplicate.evidenceFactIds[0]);
        duplicate.candidatePacketHash =
            CharacterAcquiredTraitRequestPacketAuthority.ComputeHash(duplicate);
        negatives.Add(CaptureRejectedPacket(
            duplicateEvidence,
            duplicate,
            "duplicate-evidence",
            CharacterAcquiredTraitInferenceIssueCode.DuplicateEvidence,
            settings,
            modules,
            modulesById));

        negatives.Add(CaptureConsumedMilestoneAfterErasure(
            settings,
            modules,
            modulesById));

        return negatives.ToArray();
    }

    private static PacketFixture BuildPacketFixture(
        string scenarioId,
        ScenarioPlan plan,
        int ordinal,
        CharacterAcquiredTraitSettingsSO settings,
        CharacterAcquiredTraitModuleSO[] modules,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById)
    {
        string targetId = $"character:fixture:negative-acquired-trait:{ordinal}";
        using ControlledActorFixture actor = new(
            860000 + ordinal,
            $"Negative acquired trait fixture {ordinal}",
            targetId);
        CharacterProgression progression = actor.Actor.Progression;
        CharacterNarrativeLedger ledger = progression.NarrativeLedger;
        for (int factOrdinal = 1; factOrdinal <= plan.Milestone; factOrdinal++)
        {
            CharacterNarrativeDomain domain = plan.Domains[(factOrdinal - 1) % plan.Domains.Length];
            progression.RecordNarrative(
                domain,
                BuildFactId(scenarioId, domain, factOrdinal),
                $"fixture-subject:{scenarioId}:{factOrdinal:D2}",
                CharacterActivityOutcomes.Completed,
                1f,
                100 + ordinal);
        }
        if (CharacterAcquiredTraitExperienceScore.Require(ledger) != plan.Milestone)
        {
            throw Unsupported(
                "acquired-trait-negative-score",
                $"Negative scenario '{scenarioId}' did not reach milestone {plan.Milestone} exactly.");
        }
        CharacterAcquiredTraitAggregateState state = BuildPriorState(
            scenarioId,
            plan,
            ledger,
            modulesById);
        IReadOnlyList<CharacterAcquiredTraitValidationIssue> stateIssues =
            CharacterAcquiredTraitStateValidator.ValidateWithDefinitions(
                state,
                ledger,
                settings,
                modules);
        if (stateIssues.Count > 0)
        {
            throw Unsupported(
                "acquired-trait-negative-base-state",
                $"Negative scenario '{scenarioId}' base state was rejected: {string.Join(" | ", stateIssues)}");
        }

        NarrativePublicContextMaterial publicMaterial =
            CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(
                progression,
                ledger.Facts.Select((fact) => fact.factId));
        IReadOnlyList<string> projectedEvidenceFactIds =
            CharacterAcquiredTraitPromptBuilder.BuildProjectedEvidenceFactIds(
                publicMaterial,
                ledger.Facts.Select((fact) => fact.factId));
        CharacterAcquiredTraitSubmissionCommand command =
            new CharacterAcquiredTraitSubmissionCommand(
                targetId,
                $"request:fixture:negative-acquired-trait:{ordinal}",
                $"request-key:fixture:negative-acquired-trait:{ordinal}",
                plan.Milestone,
                projectedEvidenceFactIds
                    .OrderBy((factId) => factId, StringComparer.Ordinal),
                state.revision,
                NarrativeInferenceTimestamp.FromGameTick(2000L + ordinal));
        if (!CharacterAcquiredTraitRequestPacketAuthority.TryBuild(
                command,
                ledger,
                state,
                settings,
                modules,
                out CharacterAcquiredTraitRequestPacketDto packet,
                out CharacterAcquiredTraitInferenceIssueCode issue,
                out string error))
        {
            throw Unsupported(
                "acquired-trait-negative-base-packet",
                $"Negative scenario '{scenarioId}' base producer failed as {issue}: {error}");
        }
        RequireProductionEnvelope(
            scenarioId,
            publicMaterial,
            CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
                progression,
                packet,
                publicMaterial));
        return new PacketFixture(
            scenarioId,
            plan,
            ledger,
            state,
            command,
            packet,
            NarrativeMechanicScenarioPublicContextSerializer.Serialize(publicMaterial));
    }

    private static NarrativeMechanicScenario CaptureRejectedPacket(
        PacketFixture fixture,
        CharacterAcquiredTraitRequestPacketDto rejectedPacket,
        string mutation,
        CharacterAcquiredTraitInferenceIssueCode expectedIssue,
        CharacterAcquiredTraitSettingsSO settings,
        CharacterAcquiredTraitModuleSO[] modules,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById)
    {
        bool accepted = CharacterAcquiredTraitRequestPacketAuthority.TryValidate(
            rejectedPacket,
            fixture.Ledger,
            fixture.State,
            settings,
            modules,
            out CharacterAcquiredTraitInferenceIssueCode issue,
            out string error);
        if (accepted || issue != expectedIssue || string.IsNullOrWhiteSpace(error))
        {
            throw Unsupported(
                "acquired-trait-negative-validator-drift",
                $"Negative scenario '{fixture.ScenarioId}' expected {expectedIssue}, "
                + $"but validator returned accepted={accepted}, issue={issue}, error='{error}'.");
        }

        CharacterAcquiredTraitCombinationPacketDto selected = fixture.Packet.combinationOptions[0];
        return new NarrativeMechanicScenario(
            fixture.ScenarioId,
            NarrativeMechanicScenarioProfiles.AcquiredTrait,
            ToRequestJson(rejectedPacket),
            new NarrativeMechanicCatalogCanonicalJsonArray(
                fixture.Packet.combinationOptions.Select(ToCombinationJson)),
            fixture.PublicContext.TargetPersistentId,
            fixture.PublicContext,
            BuildEffectDescriptions(fixture.Packet, modulesById),
            NarrativeMechanicCatalogCanonicalJson.Object(
                Property("actualAccepted", Boolean(false)),
                Property("actualIssueCode", String(issue.ToString())),
                Property("baselineCandidatePacketHash", String(
                    fixture.Packet.candidatePacketHash)),
                Property("expectedAccepted", Boolean(false)),
                Property("expectedIssueCode", String(expectedIssue.ToString())),
                Property("failureReason", String(error)),
                Property("fixtureKind", String("controlled-negative-not-natural-play")),
                Property("instances", new NarrativeMechanicCatalogCanonicalJsonArray(
                    fixture.State.instances.Select(ToInstanceJson))),
                Property("mutation", String(mutation)),
                Property("producer", String(ProducerIdentifier)),
                Property("selectedLegalBaselineCombinationId", String(
                    selected.combinationId)),
                Property("stateRevision", Integer(fixture.State.revision)),
                Property("validator", String(
                    "CharacterAcquiredTraitRequestPacketAuthority.TryValidate"))),
            BuildFixtureInput(
                fixture.Plan,
                fixture.Command,
                fixture.Ledger,
                fixture.State).With(
                    "mutation",
                    String(mutation)),
            ProducerIdentifier + "+controlled-negative-mutation:" + mutation,
            "CharacterAcquiredTraitRequestPacketAuthority.TryValidate",
            accepted: false,
            failureReason: issue + ": " + error,
            diversityAxes: new[]
            {
                Axis("validationBoundary", mutation),
                Axis("expectedIssueCode", expectedIssue.ToString())
            });
    }

    private static NarrativeMechanicScenario CaptureConsumedMilestoneAfterErasure(
        CharacterAcquiredTraitSettingsSO settings,
        CharacterAcquiredTraitModuleSO[] modules,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById)
    {
        const string scenarioId =
            "acquired-trait-negative-consumed-milestone-after-erasure";
        const string targetId =
            "character:fixture:acquired-trait:consumed-after-erasure";
        const string carriedStackId =
            "carried:fixture:acquired-trait:memory-erasure";
        const string erasureOperationId =
            "operation:fixture:acquired-trait:erase-milestone-3";
        const string submissionProducer =
            "CharacterAcquiredTraitInferenceService.SubmitMilestone";
        const string completionProducer =
            "CharacterAcquiredTraitInferenceService.CompleteMilestone";
        const string erasureProducer =
            "MemoryErasureSealTransactionService.TryErase";
        const string rejectionValidator =
            "CharacterAcquiredTraitInferenceService.SubmitMilestone:HasProcessedMilestone";

        using ControlledActorFixture actor = new(
            912514,
            "Consumed milestone export fixture",
            targetId);
        CharacterProgression progression = actor.Actor.Progression;
        for (int ordinal = 1; ordinal <= 8; ordinal++)
        {
            progression.RecordNarrative(
                CharacterNarrativeDomain.Work,
                $"fixture:{scenarioId}:work:{ordinal:D2}",
                $"fixture-subject:{scenarioId}:{ordinal:D2}",
                CharacterActivityOutcomes.Completed,
                value: 1f,
                day: 214);
        }

        CharacterNarrativeLedger ledger = progression.NarrativeLedger;
        if (CharacterAcquiredTraitExperienceScore.Require(ledger) != 8)
        {
            throw Unsupported(
                "acquired-trait-consumed-milestone-score",
                $"Scenario '{scenarioId}' did not reach the exact score 8.");
        }
        string[] rawEvidenceFactIds = ledger.Facts
            .Select((fact) => fact.factId)
            .OrderBy((factId) => factId, StringComparer.Ordinal)
            .ToArray();
        NarrativePublicContextMaterial publicMaterial =
            CharacterAcquiredTraitPromptBuilder.BuildPublicMaterialForLedgerEvidence(
                progression,
                rawEvidenceFactIds);
        IReadOnlyList<string> evidenceFactIds =
            CharacterAcquiredTraitPromptBuilder.BuildProjectedEvidenceFactIds(
                publicMaterial,
                rawEvidenceFactIds);
        NarrativeMechanicScenarioPublicContext publicContext =
            NarrativeMechanicScenarioPublicContextSerializer.Serialize(publicMaterial);
        string ledgerBefore = ToLedgerAuditJson(ledger, scenarioId).ToCanonicalString();

        CharacterAcquiredTraitInferenceService inference = new(settings, modules);
        CharacterAcquiredTraitSubmissionCommand firstCommand =
            BuildLifecycleSubmission(
                targetId,
                "manifest-milestone-3",
                milestone: 3,
                expectedRevision: 0,
                gameTick: 3100,
                evidenceFactIds);
        CharacterAcquiredTraitInferenceCommandResult firstSubmission =
            inference.SubmitMilestone(progression, firstCommand);
        if (!firstSubmission.Succeeded || firstSubmission.Packet == null)
        {
            throw Unsupported(
                "acquired-trait-consumed-milestone-first-submission",
                $"Scenario '{scenarioId}' first submission failed as "
                + $"{firstSubmission.Audit.IssueCode}: "
                + firstSubmission.Audit.ValidationError);
        }
        RequireProductionEnvelope(
            scenarioId,
            publicMaterial,
            CharacterAcquiredTraitPromptBuilder.BuildEnvelope(
                progression,
                firstSubmission.Packet,
                publicMaterial));
        CharacterAcquiredTraitInferenceCommandResult firstCompletion =
            CompleteLifecycleManifestation(
                inference,
                progression,
                targetId,
                firstSubmission,
                gameTick: 3101);
        if (!firstCompletion.Succeeded)
        {
            throw Unsupported(
                "acquired-trait-consumed-milestone-first-completion",
                $"Scenario '{scenarioId}' first completion failed as "
                + $"{firstCompletion.Audit.IssueCode}: "
                + firstCompletion.Audit.ValidationError);
        }

        CharacterAcquiredTraitAggregateState manifested =
            progression.CaptureAcquiredTraitState();
        CharacterAcquiredTraitInstanceState manifestedInstance = manifested
            .CaptureActiveInstances()
            .Single();
        int manifestedEffectCount = CharacterAcquiredTraitEffectSourceProjection
            .Project(manifested, ledger, settings, modules)
            .Count;
        if (!manifested.processedMilestones.SequenceEqual(new[] { 3 })
            || manifestedEffectCount != 1)
        {
            throw Unsupported(
                "acquired-trait-consumed-milestone-manifested-state",
                $"Scenario '{scenarioId}' did not establish one manifested milestone-3 effect.");
        }

        CharacterCarryInventory carry = actor.Actor.gameObject
            .GetComponent<CharacterCarryInventory>()
            ?? actor.Actor.gameObject.AddComponent<CharacterCarryInventory>();
        carry.Restore(new CharacterCarryInventorySaveData
        {
            items = new List<CharacterCarriedItemSaveData>
            {
                new()
                {
                    carriedStackId = carriedStackId,
                    sourceStackId =
                        "stack:fixture:acquired-trait:memory-erasure",
                    ownerOperationId = erasureOperationId,
                    itemId = MemoryErasureSealItemRules.ItemId,
                    quantity = MemoryErasureSealItemRules.UseQuantity,
                    components = new List<ItemInstanceComponentSaveData>()
                }
            }
        });
        ControlledReversibleCarriedDisposition physicalDisposition = new(
            carriedStackId);
        MemoryErasureSealUseResult erasure =
            new MemoryErasureSealTransactionService(
                new ControlledDefinitionSource(settings, modules),
                new ControlledGameCalendar(day: 214, hour: 9),
                physicalDisposition)
            .TryErase(
                actor.Actor,
                manifestedInstance.instanceId,
                carry,
                carriedStackId,
                erasureOperationId);
        if (erasure.Status != MemoryErasureSealUseStatus.Succeeded)
        {
            throw Unsupported(
                "acquired-trait-consumed-milestone-erasure",
                $"Scenario '{scenarioId}' erasure failed as {erasure.Status}: "
                + erasure.Detail);
        }

        CharacterAcquiredTraitAggregateState erased =
            progression.CaptureAcquiredTraitState();
        int erasedEffectCount = CharacterAcquiredTraitEffectSourceProjection
            .Project(erased, ledger, settings, modules)
            .Count;
        CharacterAcquiredTraitInstanceState erasedInstance = null;
        bool erasedStateEstablished = erased.ActiveCount == 0
            && erased.processedMilestones.SequenceEqual(new[] { 3 })
            && erased.TryGetInstance(
                manifestedInstance.instanceId,
                out erasedInstance)
            && erasedInstance.erased
            && erasedEffectCount == 0;
        if (!erasedStateEstablished)
        {
            throw Unsupported(
                "acquired-trait-consumed-milestone-erased-state",
                $"Scenario '{scenarioId}' did not preserve the consumed milestone after erasure.");
        }

        string erasedStateBeforeRetry = BuildStateAuditJson(erased)
            .ToCanonicalString();
        CharacterAcquiredTraitSubmissionCommand retryCommand =
            BuildLifecycleSubmission(
                targetId,
                "retry-consumed-milestone-3",
                milestone: 3,
                expectedRevision: erased.revision,
                gameTick: 3102,
                evidenceFactIds);
        CharacterAcquiredTraitInferenceCommandResult retry =
            inference.SubmitMilestone(progression, retryCommand);
        CharacterAcquiredTraitAggregateState afterRejectedRetry =
            progression.CaptureAcquiredTraitState();
        string rejectedState = BuildStateAuditJson(afterRejectedRetry)
            .ToCanonicalString();
        int rejectedEffectCount = CharacterAcquiredTraitEffectSourceProjection
            .Project(afterRejectedRetry, ledger, settings, modules)
            .Count;
        bool stateUnchanged = string.Equals(
            erasedStateBeforeRetry,
            rejectedState,
            StringComparison.Ordinal);
        bool ledgerPreserved = string.Equals(
            ledgerBefore,
            ToLedgerAuditJson(ledger, scenarioId).ToCanonicalString(),
            StringComparison.Ordinal);
        bool erasedStatePreserved = afterRejectedRetry.TryGetInstance(
                manifestedInstance.instanceId,
                out CharacterAcquiredTraitInstanceState rejectedInstance)
            && rejectedInstance.erased
            && rejectedInstance.erasedRevision == erasedInstance.erasedRevision
            && string.Equals(
                rejectedInstance.erasureAuditId,
                erasedInstance.erasureAuditId,
                StringComparison.Ordinal);
        if (retry.Succeeded
            || retry.Packet != null
            || retry.Audit.IssueCode
                != CharacterAcquiredTraitInferenceIssueCode.MilestoneAlreadyProcessed
            || string.IsNullOrWhiteSpace(retry.Audit.ValidationError)
            || !stateUnchanged
            || !ledgerPreserved
            || !erasedStatePreserved
            || rejectedEffectCount != 0)
        {
            throw Unsupported(
                "acquired-trait-consumed-milestone-rejection-drift",
                $"Scenario '{scenarioId}' expected an immutable MilestoneAlreadyProcessed "
                + $"rejection, but received succeeded={retry.Succeeded}, "
                + $"packet={retry.Packet != null}, issue={retry.Audit.IssueCode}, "
                + $"stateUnchanged={stateUnchanged}, ledgerPreserved={ledgerPreserved}, "
                + $"erasedStatePreserved={erasedStatePreserved}, effects={rejectedEffectCount}.");
        }

        CharacterAcquiredTraitSubmissionCommand nextCommand =
            BuildLifecycleSubmission(
                targetId,
                "manifest-next-milestone-8",
                milestone: 8,
                expectedRevision: afterRejectedRetry.revision,
                gameTick: 3103,
                evidenceFactIds);
        CharacterAcquiredTraitInferenceCommandResult nextSubmission =
            inference.SubmitMilestone(progression, nextCommand);
        CharacterAcquiredTraitInferenceCommandResult nextCompletion = default;
        if (nextSubmission.Succeeded && nextSubmission.Packet != null)
        {
            nextCompletion = CompleteLifecycleManifestation(
                inference,
                progression,
                targetId,
                nextSubmission,
                gameTick: 3104);
        }
        CharacterAcquiredTraitAggregateState afterNext =
            progression.CaptureAcquiredTraitState();
        int afterNextEffectCount = CharacterAcquiredTraitEffectSourceProjection
            .Project(afterNext, ledger, settings, modules)
            .Count;
        bool nextAccepted = nextSubmission.Succeeded
            && nextCompletion.Succeeded
            && afterNext.ActiveCount == 1
            && afterNext.instances.Count == 2
            && afterNext.processedMilestones.SequenceEqual(new[] { 3, 8 })
            && afterNext.TryGetInstance(
                manifestedInstance.instanceId,
                out CharacterAcquiredTraitInstanceState originalAfterNext)
            && originalAfterNext.erased
            && afterNextEffectCount == 1
            && string.Equals(
                ledgerBefore,
                ToLedgerAuditJson(ledger, scenarioId).ToCanonicalString(),
                StringComparison.Ordinal);
        if (!nextAccepted)
        {
            throw Unsupported(
                "acquired-trait-consumed-milestone-next-unused",
                $"Scenario '{scenarioId}' blocked normal manifestation at unused milestone 8.");
        }

        return new NarrativeMechanicScenario(
            scenarioId,
            NarrativeMechanicScenarioProfiles.AcquiredTrait,
            ToSubmissionCommandJson(retryCommand),
            new NarrativeMechanicCatalogCanonicalJsonArray(
                Array.Empty<NarrativeMechanicCatalogCanonicalJsonValue>()),
            publicContext.TargetPersistentId,
            publicContext,
            new[] { manifestedInstance.mechanicalDescription },
            NarrativeMechanicCatalogCanonicalJson.Object(
                Property("actualAccepted", Boolean(false)),
                Property("actualFailureReason", String(
                    retry.Audit.ValidationError)),
                Property("actualIssueCode", String(
                    retry.Audit.IssueCode.ToString())),
                Property("candidatePacketGenerated", Boolean(false)),
                Property("consumedManifestationMilestonesAfterErasure",
                    IntegerArray(erased.processedMilestones)),
                Property("erasedState", BuildStateAuditJson(erased)),
                Property("erasedStatePreserved", Boolean(erasedStatePreserved)),
                Property("erasure", NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("auditId", String(erasure.AuditId)),
                    Property("committedRevision", Integer(
                        erasure.CommittedRevision)),
                    Property("detail", String(erasure.Detail)),
                    Property("operationId", String(erasure.OperationId)),
                    Property("producer", String(erasureProducer)),
                    Property("status", String(erasure.Status.ToString())),
                    Property("traitInstanceId", String(
                        erasure.TraitInstanceId)))),
                Property("firstManifestation", NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("completionIssueCode", String(
                        firstCompletion.Audit.IssueCode.ToString())),
                    Property("completionProducer", String(completionProducer)),
                    Property("instance", ToInstanceJson(manifestedInstance)),
                    Property("projectedEffectCount", Integer(
                        manifestedEffectCount)),
                    Property("submissionIssueCode", String(
                        firstSubmission.Audit.IssueCode.ToString())),
                    Property("submissionProducer", String(submissionProducer)))),
                Property("ledgerPreserved", Boolean(ledgerPreserved)),
                Property("nextUnusedMilestone", Integer(8)),
                Property("nextUnusedMilestoneAccepted", Boolean(nextAccepted)),
                Property("nextUnusedMilestoneState", BuildStateAuditJson(afterNext)),
                Property("producer", String(submissionProducer)),
                Property("rejectedRequestAddedEffectCount", Integer(
                    rejectedEffectCount)),
                Property("stateAfterRejectedRequest",
                    BuildStateAuditJson(afterRejectedRetry)),
                Property("stateUnchangedAfterRejectedRequest", Boolean(
                    stateUnchanged)),
                Property("validator", String(rejectionValidator))),
            NarrativeMechanicCatalogCanonicalJson.Object(
                Property("actualExecution", NarrativeMechanicCatalogCanonicalJson.Array(
                    String(submissionProducer),
                    String(completionProducer),
                    String(erasureProducer),
                    String(submissionProducer))),
                Property("controlledPreparation", NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("actorFixture", String(
                        "transient CharacterActor initialized through CharacterAiEditorTestDependencies")),
                    Property("carriedSealFixture", String(
                        "one exact carried memory-erasure seal owned by the erasure operation")),
                    Property("ledgerRecordCalls", new NarrativeMechanicCatalogCanonicalJsonArray(
                        ledger.Facts.Select((fact) => NarrativeMechanicCatalogCanonicalJson.Object(
                            Property("day", Integer(fact.lastDay)),
                            Property("domain", String(fact.domain.ToString())),
                            Property("factId", String(fact.factId)),
                            Property("outcome", String(fact.outcome)),
                            Property("subjectId", String(fact.subjectId)))))))),
                Property("erasureOperationId", String(erasureOperationId)),
                Property("firstSubmission", ToSubmissionCommandJson(firstCommand)),
                Property("nextUnusedSubmission", ToSubmissionCommandJson(nextCommand)),
                Property("retrySubmission", ToSubmissionCommandJson(retryCommand))),
            submissionProducer,
            rejectionValidator,
            accepted: false,
            failureReason: retry.Audit.IssueCode + ": "
                + retry.Audit.ValidationError,
            diversityAxes: new[]
            {
                Axis("expectedIssueCode", retry.Audit.IssueCode.ToString()),
                Axis("lifecycle", "manifested+erased+same-milestone-rejected"),
                Axis("nextUnusedMilestone", "8"),
                Axis("validationBoundary", "production-submission-command")
            });
    }

    private static CharacterAcquiredTraitSubmissionCommand BuildLifecycleSubmission(
        string targetId,
        string suffix,
        int milestone,
        int expectedRevision,
        long gameTick,
        IEnumerable<string> evidenceFactIds) =>
        new(
            targetId,
            $"request:fixture:acquired-trait:{suffix}",
            $"request-key:fixture:acquired-trait:{suffix}",
            milestone,
            evidenceFactIds,
            expectedRevision,
            NarrativeInferenceTimestamp.FromGameTick(gameTick));

    private static CharacterAcquiredTraitInferenceCommandResult
        CompleteLifecycleManifestation(
            CharacterAcquiredTraitInferenceService inference,
            CharacterProgression progression,
            string targetId,
            CharacterAcquiredTraitInferenceCommandResult submission,
            long gameTick)
    {
        CharacterAcquiredTraitRequestPacketDto packet = submission.Packet;
        CharacterAcquiredTraitPendingRequestState pending = progression
            .CaptureAcquiredTraitState()
            .pendingRequests
            .Single(value => value != null
                && string.Equals(value.requestId, packet.requestId, StringComparison.Ordinal));
        string responseJson;
        if (pending.formulaVersion
            >= CharacterAcquiredTraitFormulaGeneration.ModuleSelectionFormulaVersion)
        {
            string positiveModuleId = (pending.offeredBenefitModuleIds
                    ?? new List<string>())
                .FirstOrDefault()
                ?? throw Unsupported(
                    "acquired-trait-lifecycle-module-offer-missing",
                    "Lifecycle fixture received no positive module offer.");
            string evidenceFactId = (pending.evidenceFactIds
                    ?? new List<string>())
                .FirstOrDefault()
                ?? throw Unsupported(
                    "acquired-trait-lifecycle-selection-evidence-missing",
                    "Lifecycle fixture received no selectable evidence fact.");
            responseJson = NarrativeMechanicCatalogCanonicalJson.Object(
                Property("displayName", String("기록의 결실")),
                Property("drawbackModuleIds",
                    NarrativeMechanicCatalogCanonicalJson.Array()),
                Property("evidenceFactIds",
                    NarrativeMechanicCatalogCanonicalJson.Array(
                        String(evidenceFactId))),
                Property("narrativeFlavor", String(
                    "통제된 의미 있는 경험이 새로운 성향으로 피어났습니다.")),
                Property("positiveModuleIds",
                    NarrativeMechanicCatalogCanonicalJson.Array(
                        String(positiveModuleId))),
                Property("selectionId", String(pending.moduleSelectionId)))
                .ToCanonicalString();
        }
        else if (pending.formulaVersion > 0)
        {
            responseJson = NarrativeMechanicCatalogCanonicalJson.Object(
                Property("displayName", String("기록의 결실")),
                Property("narrativeFlavor", String(
                    "통제된 의미 있는 경험이 새로운 성향으로 피어났습니다.")),
                Property("presentationId", String(pending.presentationId)))
                .ToCanonicalString();
        }
        else
        {
            CharacterAcquiredTraitCombinationPacketDto selected =
                packet.combinationOptions[0];
            responseJson = BuildAcceptedResponseJson(
                selected.combinationId,
                packet.evidenceFactIds[0]);
        }
        return inference.CompleteMilestone(
            progression,
            new CharacterAcquiredTraitCompletionCommand(
                targetId,
                packet.requestId,
                packet.requestKey,
                packet.candidatePacketHash,
                submission.Audit.ResultingRevision,
                submission.Audit.ResultingRevision,
                responseJson,
                NarrativeInferenceTimestamp.FromGameTick(gameTick)),
            packet);
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject
        ToSubmissionCommandJson(CharacterAcquiredTraitSubmissionCommand command) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            Property("evidenceFactIds", StringArray(command.EvidenceFactIds)),
            Property("expectedRevision", Integer(command.ExpectedRevision)),
            Property("gameTick", Integer(command.Timestamp.GameTick)),
            Property("manifestationMilestone", Integer(
                command.ManifestationMilestone)),
            Property("requestId", String(command.RequestId)),
            Property("requestKey", String(command.RequestKey)),
            Property("targetPersistentId", String(command.TargetPersistentId)),
            Property("timeAuthority", String(
                command.Timestamp.Authority.ToString())));

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildStateAuditJson(
        CharacterAcquiredTraitAggregateState state) =>
        NarrativeMechanicCatalogCanonicalJson.Object(
            Property("activeCount", Integer(state.ActiveCount)),
            Property("formatVersion", Integer(state.formatVersion)),
            Property("instances", new NarrativeMechanicCatalogCanonicalJsonArray(
                state.instances.Select(ToInstanceJson))),
            Property("pendingRequestCount", Integer(state.pendingRequests.Count)),
            Property("processedMilestones", IntegerArray(
                state.processedMilestones)),
            Property("revision", Integer(state.revision)));

    private static CharacterAcquiredTraitCombinationPacketDto BuildCombination(
        IEnumerable<CharacterAcquiredTraitModuleSO> source,
        IEnumerable<string> eligibleDomains)
    {
        CharacterAcquiredTraitModuleSO[] modules = source
            .OrderBy((module) => module.ModuleId, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> domains = new HashSet<string>(
            eligibleDomains ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        string[] moduleIds = modules.Select((module) => module.ModuleId).ToArray();
        return new CharacterAcquiredTraitCombinationPacketDto
        {
            combinationId = CharacterAcquiredTraitCombinationIdentity.Build(moduleIds),
            moduleIds = moduleIds.ToList(),
            totalCost = modules.Sum((module) => module.Cost),
            domainAffinities = modules
                .SelectMany((module) => module.DomainAffinities)
                .Select((domain) => domain.ToString())
                .Where(domains.Contains)
                .Distinct(StringComparer.Ordinal)
                .OrderBy((domain) => domain, StringComparer.Ordinal)
                .ToList(),
            conflictGroups = modules
                .SelectMany((module) => module.ConflictGroups)
                .Select((group) => group.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy((group) => group, StringComparer.Ordinal)
                .ToList()
        };
    }

    private static CharacterAcquiredTraitModuleSO RequireModule(
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById,
        string moduleId)
    {
        if (!modulesById.TryGetValue(moduleId, out CharacterAcquiredTraitModuleSO module))
        {
            throw Unsupported(
                "acquired-trait-negative-module-missing",
                $"Negative fixture requires authored module '{moduleId}'.");
        }
        return module;
    }

    private static CharacterAcquiredTraitAggregateState BuildPriorState(
        string scenarioId,
        ScenarioPlan plan,
        CharacterNarrativeLedger ledger,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById)
    {
        CharacterAcquiredTraitAggregateState state =
            new CharacterAcquiredTraitAggregateState();
        int completedRevision = plan.Priors.Length * 2;
        int erasedOrdinal = 0;
        foreach ((PriorPlan prior, int index) in plan.Priors.Select(
                     (value, index) => (value, index)))
        {
            if (!modulesById.TryGetValue(prior.ModuleId, out CharacterAcquiredTraitModuleSO module))
            {
                throw Unsupported(
                    "acquired-trait-fixture-module-missing",
                    $"Scenario '{scenarioId}' requires authored module '{prior.ModuleId}'.");
            }
            CharacterNarrativeFact evidence = ledger.Facts.FirstOrDefault((fact) =>
                fact != null
                && fact.milestoneCount > 0
                && module.DomainAffinities.Contains(fact.domain));
            if (evidence == null)
            {
                throw Unsupported(
                    "acquired-trait-fixture-prior-domain",
                    $"Scenario '{scenarioId}' has no evidence domain for prior module '{prior.ModuleId}'.");
            }

            int acceptedRevision = (index + 1) * 2;
            int erasedRevision = prior.Erased
                ? completedRevision + ++erasedOrdinal
                : 0;
            state.instances.Add(new CharacterAcquiredTraitInstanceState
            {
                instanceId = $"acquired-trait-instance:fixture:{scenarioId}:{prior.Milestone}",
                combinationId = CharacterAcquiredTraitCombinationIdentity.Build(
                    new[] { prior.ModuleId }),
                moduleIds = new List<string> { prior.ModuleId },
                displayName = $"통제 fixture {module.DisplayName}",
                description = module.Description,
                narrativeReason = $"{evidence.factId} 기록으로 발현된 통제 fixture입니다.",
                evidenceFactIds = new List<string> { evidence.factId },
                manifestationMilestone = prior.Milestone,
                originatingRequestId = $"request:fixture:prior:{scenarioId}:{prior.Milestone}",
                originatingRequestKey = $"request-key:fixture:prior:{scenarioId}:{prior.Milestone}",
                candidatePacketHash = NarrativeInferenceHash.ComputeSha256Utf8(
                    $"fixture-packet|{scenarioId}|{prior.Milestone}|{prior.ModuleId}"),
                selectionAuditId = $"audit:fixture:selection:{scenarioId}:{prior.Milestone}",
                acceptedRevision = acceptedRevision,
                erased = prior.Erased,
                erasedAt = prior.Erased
                    ? 2000L + prior.Milestone
                    : CharacterAcquiredTraitInstanceState.NotErasedAtAbsoluteHour,
                erasureAuditId = prior.Erased
                    ? $"audit:fixture:erasure:{scenarioId}:{prior.Milestone}"
                    : string.Empty,
                erasedRevision = erasedRevision
            });
        }
        state.instances = state.instances
            .OrderBy((instance) => instance.manifestationMilestone)
            .ThenBy((instance) => instance.instanceId, StringComparer.Ordinal)
            .ToList();
        state.processedMilestones = state.instances
            .Select((instance) => instance.manifestationMilestone)
            .OrderBy((milestone) => milestone)
            .ToList();
        state.revision = completedRevision + erasedOrdinal;
        return state;
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildAuthorityContext(
        CharacterAcquiredTraitSettingsSO settings,
        CharacterAcquiredTraitAggregateState state,
        int score,
        CharacterAcquiredTraitRequestPacketDto packet,
        CharacterAcquiredTraitCombinationPacketDto selected,
        string responseJson)
    {
        settings.TryGetGate(
            packet.manifestationMilestone,
            out CharacterAcquiredTraitManifestationGateDefinition gate);
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("activeCount", Integer(state.ActiveCount)),
            Property("budget", Integer(gate.Budget)),
            Property("candidatePacketHash", String(packet.candidatePacketHash)),
            Property("consumedManifestationMilestones", IntegerArray(state.processedMilestones)),
            Property("expectedAccepted", Boolean(true)),
            Property("expectedIssueCode", String(CharacterAcquiredTraitInferenceIssueCode.None.ToString())),
            Property("expectedResponseJson", String(responseJson)),
            Property("experienceScore", Integer(score)),
            Property("fixtureKind", String("controlled-not-natural-play")),
            Property("instances", new NarrativeMechanicCatalogCanonicalJsonArray(
                state.instances.Select(ToInstanceJson))),
            Property("maximumActiveTraits", Integer(settings.MaximumActiveTraits)),
            Property("producer", String(ProducerIdentifier)),
            Property("rarity", String(gate.Rarity.ToString())),
            Property("selectedCombinationId", String(selected.combinationId)),
            Property("settingsId", String(settings.SettingsId)),
            Property("stateFormatVersion", Integer(state.formatVersion)),
            Property("stateRevision", Integer(state.revision)),
            Property("stateValidation", String("accepted")),
            Property("requestValidation", String("accepted:None")),
            Property("responseValidation", String("accepted:None")),
            Property("validator", String(ValidatorIdentifier)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildFixtureInput(
        ScenarioPlan plan,
        CharacterAcquiredTraitSubmissionCommand command,
        CharacterNarrativeLedger ledger,
        CharacterAcquiredTraitAggregateState state)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("fixtureKind", String("controlled")),
            Property("ledgerRecordCalls", new NarrativeMechanicCatalogCanonicalJsonArray(
                ledger.Facts.Select((fact) => NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("day", Integer(fact.lastDay)),
                    Property("domain", String(fact.domain.ToString())),
                    Property("factId", String(fact.factId)),
                    Property("outcome", String(fact.outcome)),
                    Property("repetitions", Integer(fact.count)),
                    Property("subjectId", String(fact.subjectId)),
                    Property("valueDecimalPerRecord", String("1")))))),
            Property("priorManifestations", new NarrativeMechanicCatalogCanonicalJsonArray(
                state.instances.Select(ToInstanceJson))),
            Property("requestCommand", NarrativeMechanicCatalogCanonicalJson.Object(
                Property("evidenceFactIds", StringArray(command.EvidenceFactIds)),
                Property("expectedRevision", Integer(command.ExpectedRevision)),
                Property("gameTick", Integer(command.Timestamp.GameTick)),
                Property("manifestationMilestone", Integer(command.ManifestationMilestone)),
                Property("requestId", String(command.RequestId)),
                Property("requestKey", String(command.RequestKey)),
                Property("targetPersistentId", String(command.TargetPersistentId)),
                Property("timeAuthority", String(command.Timestamp.Authority.ToString())))),
            Property("stateAxis", String(plan.StateAxis)));
    }

    // Local lifecycle invariant only.  It is never scenario publicFacts or
    // publicNarrativeContext, and no adapter is permitted to consume it.
    private static NarrativeMechanicCatalogCanonicalJsonArray ToLedgerAuditJson(
        CharacterNarrativeLedger ledger,
        string scenarioId)
    {
        return new NarrativeMechanicCatalogCanonicalJsonArray(ledger.Facts
            .OrderBy((fact) => fact.factId, StringComparer.Ordinal)
            .Select((fact, ordinal) => NarrativeMechanicCatalogCanonicalJson.Object(
                Property("count", Integer(fact.count)),
                Property("domain", String(fact.domain.ToString())),
                Property("factId", String(fact.factId)),
                Property("lastDay", Integer(fact.lastDay)),
                Property("milestoneCount", Integer(fact.milestoneCount)),
                Property("outcome", String(fact.outcome)),
                Property("producerSymbol", String("CharacterNarrativeLedger.Record")),
                Property("subjectId", String(fact.subjectId)),
                Property("text", String(BuildFactText(
                    scenarioId,
                    fact.domain,
                    ordinal + 1))),
                Property("totalValueDecimal", String(
                    fact.totalValue.ToString("R", CultureInfo.InvariantCulture))))));
    }

    private static IEnumerable<string> BuildEffectDescriptions(
        CharacterAcquiredTraitRequestPacketDto packet,
        IReadOnlyDictionary<string, CharacterAcquiredTraitModuleSO> modulesById)
    {
        foreach (CharacterAcquiredTraitModulePacketDto packetModule in packet.modules
                     .OrderBy((module) => module.moduleId, StringComparer.Ordinal))
        {
            if (!modulesById.TryGetValue(
                    packetModule.moduleId,
                    out CharacterAcquiredTraitModuleSO module))
            {
                throw Unsupported(
                    "acquired-trait-fixture-effect-module",
                    $"Produced packet references unknown authored module '{packetModule.moduleId}'.");
            }
            foreach (GameplayEffectBinding binding in module.Effects
                         .OrderBy((effect) => effect.bindingId, StringComparer.Ordinal))
            {
                GameplayEffectDefinitionSO definition = binding.definition;
                if (definition == null)
                {
                    throw Unsupported(
                        "acquired-trait-fixture-effect-definition",
                        $"Module '{module.ModuleId}' binding '{binding.bindingId}' has no definition.");
                }
                yield return string.Join(
                    " | ",
                    module.ModuleId,
                    module.DisplayName,
                    module.Description,
                    binding.bindingId.Trim(),
                    definition.EffectId,
                    definition.TargetId,
                    definition.Operation.ToString(),
                    binding.value.ToString("R", CultureInfo.InvariantCulture),
                    binding.condition?.ConditionId ?? "unconditional");
            }
        }
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject ToRequestJson(
        CharacterAcquiredTraitRequestPacketDto packet)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("budget", Integer(packet.budget)),
            Property("candidatePacketHash", String(packet.candidatePacketHash)),
            Property("combinationOptions", new NarrativeMechanicCatalogCanonicalJsonArray(
                packet.combinationOptions.Select(ToCombinationJson))),
            Property("eligibleDomains", StringArray(packet.eligibleDomains)),
            Property("evidenceFactIds", StringArray(packet.evidenceFactIds)),
            Property("manifestationMilestone", Integer(packet.manifestationMilestone)),
            Property("maximumActiveTraits", Integer(packet.maximumActiveTraits)),
            Property("modules", new NarrativeMechanicCatalogCanonicalJsonArray(
                packet.modules.Select(ToModuleJson))),
            Property("rarity", String(packet.rarity)),
            Property("requestId", String(packet.requestId)),
            Property("requestKey", String(packet.requestKey)),
            Property("settingsId", String(packet.settingsId)),
            Property("targetPersistentId", String(packet.targetPersistentId)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue ToModuleJson(
        CharacterAcquiredTraitModulePacketDto module)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("conflictGroups", StringArray(module.conflictGroups)),
            Property("cost", Integer(module.cost)),
            Property("description", String(module.description)),
            Property("displayName", String(module.displayName)),
            Property("domainAffinities", StringArray(module.domainAffinities)),
            Property("moduleId", String(module.moduleId)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue ToCombinationJson(
        CharacterAcquiredTraitCombinationPacketDto option)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("combinationId", String(option.combinationId)),
            Property("conflictGroups", StringArray(option.conflictGroups)),
            Property("domainAffinities", StringArray(option.domainAffinities)),
            Property("moduleIds", StringArray(option.moduleIds)),
            Property("totalCost", Integer(option.totalCost)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue ToInstanceJson(
        CharacterAcquiredTraitInstanceState instance)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("acceptedRevision", Integer(instance.acceptedRevision)),
            Property("candidatePacketHash", String(instance.candidatePacketHash)),
            Property("combinationId", String(instance.combinationId)),
            Property("description", String(instance.description)),
            Property("displayName", String(instance.displayName)),
            Property("erased", Boolean(instance.erased)),
            Property("erasedAtAbsoluteHour", Integer(instance.erasedAt)),
            Property("erasedRevision", Integer(instance.erasedRevision)),
            Property("erasureAuditId", String(instance.erasureAuditId)),
            Property("evidenceFactIds", StringArray(instance.evidenceFactIds)),
            Property("instanceId", String(instance.instanceId)),
            Property("manifestationMilestone", Integer(instance.manifestationMilestone)),
            Property("moduleIds", StringArray(instance.moduleIds)),
            Property("narrativeReason", String(instance.narrativeReason)),
            Property("originatingRequestId", String(instance.originatingRequestId)),
            Property("originatingRequestKey", String(instance.originatingRequestKey)),
            Property("selectionAuditId", String(instance.selectionAuditId)));
    }

    private static string BuildAcceptedResponseJson(
        string combinationId,
        string evidenceFactId)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("combinationId", String(combinationId)),
            Property("description", String("실제 원장 근거와 법적 조합이 확인된 후천 특성입니다.")),
            Property("displayName", String("기록의 결실")),
            Property("evidenceFactIds", StringArray(new[] { evidenceFactId })),
            Property("narrativeReason", String("통제 fixture의 의미 있는 경험 기록이 발현 근거가 되었습니다.")))
            .ToCanonicalString();
    }

    private static void RequireProductionEnvelope(
        string scenarioId,
        NarrativePublicContextMaterial material,
        NarrativePublicPromptEnvelope envelope)
    {
        if (envelope == null
            || !ReferenceEquals(envelope.Material, material)
            || string.IsNullOrWhiteSpace(envelope.Prompt))
        {
            throw Unsupported(
                "acquired-trait-scenario-envelope-invalid",
                $"Scenario '{scenarioId}' did not receive its production public-context envelope.");
        }
    }

    private void ResolveAuthority(
        out CharacterAcquiredTraitSettingsSO settings,
        out CharacterAcquiredTraitModuleSO[] modules)
    {
        if (suppliedSettings != null || suppliedModules != null)
        {
            if (suppliedSettings == null || suppliedModules == null)
            {
                throw Unsupported(
                    "acquired-trait-fixture-partial-authority",
                    "Injected acquired-trait authority must supply both settings and modules.");
            }
            settings = suppliedSettings;
            modules = suppliedModules.Where((module) => module != null).ToArray();
            if (modules.Length != suppliedModules.Length)
            {
                throw Unsupported(
                    "acquired-trait-fixture-null-module",
                    "Injected acquired-trait authority contains a null module.");
            }
            return;
        }

        CharacterAcquiredTraitSettingsSO[] settingsAssets =
            LoadAssets<CharacterAcquiredTraitSettingsSO>();
        if (settingsAssets.Length != 1)
        {
            throw Unsupported(
                "acquired-trait-fixture-settings-count",
                $"'{AuthoredRoot}' must contain exactly one acquired-trait settings asset; "
                + $"found {settingsAssets.Length}.");
        }
        settings = settingsAssets[0];
        modules = LoadAssets<CharacterAcquiredTraitModuleSO>();
        if (modules.Length == 0)
        {
            throw Unsupported(
                "acquired-trait-fixture-module-count",
                $"'{AuthoredRoot}' contains no acquired-trait modules.");
        }
    }

    private static T[] LoadAssets<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { AuthoredRoot });
        T[] assets = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy((path) => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where((asset) => asset != null)
            .ToArray();
        if (assets.Length != guids.Distinct(StringComparer.Ordinal).Count())
        {
            throw Unsupported(
                "acquired-trait-fixture-asset-load",
                $"One or more authored {typeof(T).Name} assets could not be loaded from '{AuthoredRoot}'.");
        }
        return assets;
    }

    private static void ValidateAuthoredAuthority(
        CharacterAcquiredTraitSettingsSO settings,
        CharacterAcquiredTraitModuleSO[] modules)
    {
        string[] settingsErrors = settings.ValidateDefinition().ToArray();
        if (settingsErrors.Length > 0)
        {
            throw Unsupported(
                "acquired-trait-fixture-settings-invalid",
                string.Join(" | ", settingsErrors));
        }
        string[] moduleErrors = modules
            .SelectMany((module) => module.ValidateDefinition()
                .Select((error) => $"{module.ModuleId}: {error}"))
            .ToArray();
        if (moduleErrors.Length > 0)
        {
            throw Unsupported(
                "acquired-trait-fixture-modules-invalid",
                string.Join(" | ", moduleErrors));
        }
        string[] effectErrors = modules
            .SelectMany((module) => module.Effects
                .Where((binding) => binding?.definition != null)
                .SelectMany((binding) => binding.definition.ValidateDefinition()
                    .Select((error) =>
                        $"{module.ModuleId}/{binding.bindingId}: {error}")))
            .Concat(modules.SelectMany((module) => module.Effects
                .Where((binding) => binding?.condition != null
                    && string.IsNullOrWhiteSpace(binding.condition.ConditionId))
                .Select((binding) =>
                    $"{module.ModuleId}/{binding.bindingId}: condition ID is blank.")))
            .ToArray();
        if (effectErrors.Length > 0)
        {
            throw Unsupported(
                "acquired-trait-fixture-effects-invalid",
                string.Join(" | ", effectErrors));
        }
        string duplicateId = modules
            .GroupBy((module) => module.ModuleId, StringComparer.Ordinal)
            .FirstOrDefault((group) => group.Count() > 1)?.Key;
        if (!string.IsNullOrEmpty(duplicateId))
        {
            throw Unsupported(
                "acquired-trait-fixture-module-duplicate",
                $"Authored module ID '{duplicateId}' is duplicated.");
        }
        foreach (int milestone in new[] { 3, 8, 20 })
        {
            if (!settings.TryGetGate(milestone, out _))
            {
                throw Unsupported(
                    "acquired-trait-fixture-gate-missing",
                    $"Authored acquired-trait settings lack required milestone {milestone}.");
            }
        }
    }

    private static IEnumerable<string> CaptureRelevantPaths(
        CharacterAcquiredTraitSettingsSO settings,
        IEnumerable<CharacterAcquiredTraitModuleSO> modules)
    {
        HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal)
        {
            SourcePath,
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioContracts.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitEffectSource.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitInferenceContracts.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitInferenceService.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitValidation.cs",
            "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitState.cs",
            "Assets/Scripts/Services/Character/Core/CharacterProgression.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs",
            "Assets/Scripts/Services/Character/Core/MemoryErasureSealContracts.cs",
            "Assets/Scripts/Services/Character/Core/MemoryErasureSealTransactionService.cs",
            "Assets/Scripts/Services/Items/CharacterCarryInventory.cs",
            "Assets/Scripts/Services/Items/PhysicalItemBatchDispositionService.cs",
            "Assets/Scripts/Models/Items/Core/ItemPrimitives.cs"
        };
        AddAssetPath(paths, settings);
        foreach (CharacterAcquiredTraitModuleSO module in modules)
        {
            AddAssetPath(paths, module);
            foreach (GameplayEffectBinding binding in module.Effects)
            {
                AddAssetPath(paths, binding.definition);
                AddAssetPath(paths, binding.condition);
            }
        }
        return paths.OrderBy((path) => path, StringComparer.Ordinal).ToArray();
    }

    private static void AddAssetPath(ISet<string> paths, UnityEngine.Object asset)
    {
        if (asset == null)
            return;
        string path = AssetDatabase.GetAssetPath(asset)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(path))
        {
            throw Unsupported(
                "acquired-trait-fixture-asset-path",
                $"Authored dependency '{asset.name}' has no AssetDatabase path.");
        }
        paths.Add(path);
        paths.Add(path + ".meta");
    }

    private void ValidateScenarioSetKind()
    {
        if (scenarioSetKind != NarrativeMechanicScenarioSetKind.Calibration
            && scenarioSetKind != NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation)
        {
            throw Unsupported(
                "acquired-trait-scenario-set-kind",
                $"AcquiredTrait does not support scenario set '{scenarioSetKind}'.");
        }
    }

    private void RequireEvaluationMetadata(IEnumerable<ScenarioPlan> plans)
    {
        if (!IsUnseenNamingEvaluation)
        {
            return;
        }

        ScenarioPlan missing = plans.FirstOrDefault((plan) =>
            plan.EvaluationMetadata == null
            || plan.LedgerEvents.Length < 2
            || plan.LedgerEvents.Any((ledgerEvent) => ledgerEvent?.Publication == null));
        if (missing != null)
        {
            throw Unsupported(
                "acquired-trait-evaluation-metadata",
                $"Evaluation scenario '{missing?.Slug ?? "unknown"}' requires metadata, a multi-event ledger, and one public publication per event.");
        }
    }

    private bool IsUnseenNamingEvaluation =>
        scenarioSetKind == NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation;

    private static ScenarioPlan[] BuildUnseenNamingEvaluationPlans()
    {
        const string battle = "acquired-trait:module:battle-temper";
        const string steady = "acquired-trait:module:steady-hands";
        const string scar = "acquired-trait:module:scar-memory";
        const string night = "acquired-trait:module:night-vigil";
        const string rhythm = "acquired-trait:module:work-rhythm";
        const string social = "acquired-trait:module:social-bridge";
        const string ritual = "acquired-trait:module:ritual-attunement";

        return new[]
        {
            EvaluationPlan("unseen-eval-v1-m03-work-supply", 3,
                "acquired-trait-unseen-eval-v1-01",
                "acquired-trait-authority-01-m03-work-empty",
                "event sequence changes from one work fact to setup, blocked supply, and completed repair; domain combination adds FacilityUse; outcome changes through Started, Blocked, and Completed.",
                CuratedEvents("work-supply", Domains(CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.FacilityUse, CharacterNarrativeDomain.Work),
                    Outcomes(CharacterActivityOutcomes.Started, CharacterActivityOutcomes.Blocked,
                        CharacterActivityOutcomes.Completed))),
            EvaluationPlan("unseen-eval-v1-m03-facility-clinic", 3,
                "acquired-trait-unseen-eval-v1-02",
                "acquired-trait-authority-02-m03-facility-empty",
                "event sequence changes from one facility fact to calibration, treatment, and duty return; domain combination adds Injury and Work; outcome adds Failed and Returned beside Completed.",
                CuratedEvents("facility-clinic", Domains(CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Work),
                    Outcomes(CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Damaged,
                        CharacterActivityOutcomes.Returned))),
            EvaluationPlan("unseen-eval-v1-m03-injury-escort", 3,
                "acquired-trait-unseen-eval-v1-03",
                "acquired-trait-authority-03-m03-injury-empty",
                "event sequence changes from one injury fact to impact, escort, and recovery; domain combination adds Relationship; outcome changes from one Completed result to Damaged, Responded, and Completed.",
                CuratedEvents("injury-escort", Domains(CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Relationship, CharacterNarrativeDomain.Injury),
                    Outcomes(CharacterActivityOutcomes.Damaged, CharacterActivityOutcomes.Responded,
                        CharacterActivityOutcomes.Completed))),
            EvaluationPlan("unseen-eval-v1-m03-mood-watch", 3,
                "acquired-trait-unseen-eval-v1-04",
                "acquired-trait-authority-04-m03-mood-empty",
                "event sequence changes from one mood fact to alarm, watch, and relief; domain combination adds Invasion and Work; outcome changes through Changed, Started, and Returned.",
                CuratedEvents("mood-watch", Domains(CharacterNarrativeDomain.Mood,
                    CharacterNarrativeDomain.Invasion, CharacterNarrativeDomain.Work),
                    Outcomes(CharacterActivityOutcomes.Changed, CharacterActivityOutcomes.Started,
                        CharacterActivityOutcomes.Returned))),
            EvaluationPlan("unseen-eval-v1-m03-relationship-crossing", 3,
                "acquired-trait-unseen-eval-v1-05",
                "acquired-trait-authority-05-m03-relationship-empty",
                "event sequence changes from one relationship fact to dispute, crossing, and reconciliation; domain combination adds Expedition; outcome adds Failed before Returned and Completed.",
                CuratedEvents("relationship-crossing", Domains(CharacterNarrativeDomain.Relationship,
                    CharacterNarrativeDomain.Expedition, CharacterNarrativeDomain.Relationship),
                    Outcomes(CharacterActivityOutcomes.Failed, CharacterActivityOutcomes.Returned,
                        CharacterActivityOutcomes.Completed))),
            EvaluationPlan("unseen-eval-v1-m03-survival-ration", 3,
                "acquired-trait-unseen-eval-v1-06",
                "acquired-trait-authority-06-m03-survival-empty",
                "event sequence changes from one survival fact to rationing, a water search, and a camp recovery; domain combination adds Need; outcome changes through Changed, Blocked, and Completed.",
                CuratedEvents("survival-ration", Domains(CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Need, CharacterNarrativeDomain.Survival),
                    Outcomes(CharacterActivityOutcomes.Changed, CharacterActivityOutcomes.Blocked,
                        CharacterActivityOutcomes.Completed))),
            EvaluationPlan("unseen-eval-v1-m03-combat-guard", 3,
                "acquired-trait-unseen-eval-v1-07",
                "acquired-trait-authority-07-m03-combat-empty",
                "event sequence changes from one combat fact to a guard warning, damaged defense, and counterattack; domain combination adds Invasion and Injury; outcome adds Started and Damaged before Completed.",
                CuratedEvents("combat-guard", Domains(CharacterNarrativeDomain.Combat,
                    CharacterNarrativeDomain.Invasion, CharacterNarrativeDomain.Injury),
                    Outcomes(CharacterActivityOutcomes.Started, CharacterActivityOutcomes.Damaged,
                        CharacterActivityOutcomes.Completed))),

            EvaluationPlan("unseen-eval-v1-m08-work-steady", 8,
                "acquired-trait-unseen-eval-v1-08",
                "acquired-trait-authority-08-m08-work-active-steady",
                "event sequence changes from repeated work facts to a forge, delivery, repair, and watch history; domain combination expands Work with FacilityUse, Survival, and Mood; outcome cycles through Started, Blocked, Completed, and Changed; prior active/erased state keeps Steady Hands active with a distinct evidence trail.",
                CuratedEvents("work-steady", Domains(CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.FacilityUse, CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Mood,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Work), Outcomes(CharacterActivityOutcomes.Started,
                    CharacterActivityOutcomes.Blocked, CharacterActivityOutcomes.Completed,
                    CharacterActivityOutcomes.Changed, CharacterActivityOutcomes.Damaged,
                    CharacterActivityOutcomes.Returned, CharacterActivityOutcomes.Progress,
                    CharacterActivityOutcomes.Completed)), Prior(3, steady, false)),
            EvaluationPlan("unseen-eval-v1-m08-work-night-erased", 8,
                "acquired-trait-unseen-eval-v1-09",
                "acquired-trait-authority-09-m08-work-active-night",
                "event sequence changes from a single work stream to night patrol, rationing, repair, and return; domain combination adds Survival, Invasion, and FacilityUse; outcome includes Started, Failed, Damaged, and Returned; prior active/erased state changes Night Vigil from active to erased.",
                CuratedEvents("work-night-erased", Domains(CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Invasion,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Invasion), Outcomes(CharacterActivityOutcomes.Started,
                    CharacterActivityOutcomes.Changed, CharacterActivityOutcomes.Failed,
                    CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Damaged,
                    CharacterActivityOutcomes.Returned, CharacterActivityOutcomes.Progress,
                    CharacterActivityOutcomes.Completed)), Prior(3, night, true)),
            EvaluationPlan("unseen-eval-v1-m08-work-survival-night-active", 8,
                "acquired-trait-unseen-eval-v1-10",
                "acquired-trait-authority-10-m08-work-survival-erased-night",
                "event sequence changes from alternating work and survival facts to shelter setup, blocked water, escort, and recovery; domain combination adds Relationship and FacilityUse; outcome adds Blocked, Responded, and Returned; prior active/erased state changes Night Vigil from erased to active.",
                CuratedEvents("work-survival-night", Domains(CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Need, CharacterNarrativeDomain.Relationship,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.FacilityUse), Outcomes(CharacterActivityOutcomes.Started,
                    CharacterActivityOutcomes.Blocked, CharacterActivityOutcomes.Completed,
                    CharacterActivityOutcomes.Changed, CharacterActivityOutcomes.Responded,
                    CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Returned,
                    CharacterActivityOutcomes.Completed)), Prior(3, night, false)),
            EvaluationPlan("unseen-eval-v1-m08-combat-survival-battle", 8,
                "acquired-trait-unseen-eval-v1-11",
                "acquired-trait-authority-11-m08-combat-survival-active-battle",
                "event sequence changes from combat-survival pairs to a scout, ambush, triage, ration, and countercharge sequence; domain combination adds Injury and Expedition; outcome adds Observed, Damaged, Failed, and Completed; prior active/erased state retains Battle Temper active with different evidence.",
                CuratedEvents("combat-survival-battle", Domains(CharacterNarrativeDomain.Combat,
                    CharacterNarrativeDomain.Expedition, CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Combat,
                    CharacterNarrativeDomain.Invasion, CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Combat), Outcomes(CharacterActivityOutcomes.Observed,
                    CharacterActivityOutcomes.Failed, CharacterActivityOutcomes.Damaged,
                    CharacterActivityOutcomes.Changed, CharacterActivityOutcomes.Completed,
                    CharacterActivityOutcomes.Started, CharacterActivityOutcomes.Returned,
                    CharacterActivityOutcomes.Completed)), Prior(3, battle, false)),
            EvaluationPlan("unseen-eval-v1-m08-relationship-work-social-erased", 8,
                "acquired-trait-unseen-eval-v1-12",
                "acquired-trait-authority-12-m08-relationship-work-active-social",
                "event sequence changes from relationship-work pairs to dispute, mediation, delivery, and reconciliation; domain combination adds FacilityUse and Mood; outcome adds Failed, Responded, Blocked, and Completed; prior active/erased state changes Social Bridge from active to erased.",
                CuratedEvents("relationship-work-social", Domains(CharacterNarrativeDomain.Relationship,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Mood,
                    CharacterNarrativeDomain.FacilityUse, CharacterNarrativeDomain.Relationship,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Relationship,
                    CharacterNarrativeDomain.Work), Outcomes(CharacterActivityOutcomes.Failed,
                    CharacterActivityOutcomes.Started, CharacterActivityOutcomes.Changed,
                    CharacterActivityOutcomes.Blocked, CharacterActivityOutcomes.Responded,
                    CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Returned,
                    CharacterActivityOutcomes.Completed)), Prior(3, social, true)),
            EvaluationPlan("unseen-eval-v1-m08-injury-scar-active", 8,
                "acquired-trait-unseen-eval-v1-13",
                "acquired-trait-authority-13-m08-injury-mood-work-active-scar",
                "event sequence changes from injury-mood-work facts to impact, treatment, morale, and repaired duty; domain combination adds FacilityUse and Relationship; outcome adds Damaged, Changed, Responded, and Returned; prior active/erased state retains Scar Memory active with a new sequence.",
                CuratedEvents("injury-scar", Domains(CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.FacilityUse, CharacterNarrativeDomain.Mood,
                    CharacterNarrativeDomain.Relationship, CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Mood), Outcomes(CharacterActivityOutcomes.Damaged,
                    CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Changed,
                    CharacterActivityOutcomes.Responded, CharacterActivityOutcomes.Returned,
                    CharacterActivityOutcomes.Damaged, CharacterActivityOutcomes.Completed,
                    CharacterActivityOutcomes.Changed)), Prior(3, scar, false)),
            EvaluationPlan("unseen-eval-v1-m08-facility-ritual-erased", 8,
                "acquired-trait-unseen-eval-v1-14",
                "acquired-trait-authority-14-m08-facility-work-active-ritual",
                "event sequence changes from facility-work facts to ritual setup, failed calibration, repair, and night closure; domain combination adds Mood and Survival; outcome adds Started, Failed, Changed, and Returned; prior active/erased state changes Ritual Attunement from active to erased.",
                CuratedEvents("facility-ritual", Domains(CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Mood,
                    CharacterNarrativeDomain.FacilityUse, CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Work), Outcomes(CharacterActivityOutcomes.Started,
                    CharacterActivityOutcomes.Failed, CharacterActivityOutcomes.Changed,
                    CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Returned,
                    CharacterActivityOutcomes.Progress, CharacterActivityOutcomes.Completed,
                    CharacterActivityOutcomes.Completed)), Prior(3, ritual, true)),

            EvaluationPlan("unseen-eval-v1-m20-work-social-mixed", 20,
                "acquired-trait-unseen-eval-v1-15",
                "acquired-trait-authority-15-m20-work-relationship-two-active",
                "event sequence changes from repeated work-relationship facts to a 20-event build, dispute, patrol, and repair history; domain combination adds FacilityUse, Survival, Invasion, and Mood; outcome includes Started, Blocked, Damaged, Failed, Returned, and Completed; prior active/erased state changes the second prior Social Bridge from active to erased.",
                CuratedCycle("m20-work-social", 20, Domains(CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Relationship, CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Invasion,
                    CharacterNarrativeDomain.Mood), Outcomes(CharacterActivityOutcomes.Started,
                    CharacterActivityOutcomes.Responded, CharacterActivityOutcomes.Blocked,
                    CharacterActivityOutcomes.Changed, CharacterActivityOutcomes.Damaged,
                    CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Failed,
                    CharacterActivityOutcomes.Returned)), Prior(3, steady, false), Prior(8, social, true)),
            EvaluationPlan("unseen-eval-v1-m20-work-night-switch", 20,
                "acquired-trait-unseen-eval-v1-16",
                "acquired-trait-authority-16-m20-work-survival-active-erased",
                "event sequence changes from work-survival facts to a 20-event ration, watch, crossing, and repair history; domain combination adds Expedition, Need, and FacilityUse; outcome adds Started, Failed, Blocked, Damaged, Returned, and Completed; prior active/erased state changes Steady Hands to erased and Night Vigil to active.",
                CuratedCycle("m20-work-night", 20, Domains(CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Survival, CharacterNarrativeDomain.Need,
                    CharacterNarrativeDomain.Expedition, CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Work), Outcomes(CharacterActivityOutcomes.Changed,
                    CharacterActivityOutcomes.Started, CharacterActivityOutcomes.Blocked,
                    CharacterActivityOutcomes.Failed, CharacterActivityOutcomes.Damaged,
                    CharacterActivityOutcomes.Returned, CharacterActivityOutcomes.Completed)), Prior(3, steady, true), Prior(8, night, false)),
            EvaluationPlan("unseen-eval-v1-m20-work-rhythm-split", 20,
                "acquired-trait-unseen-eval-v1-17",
                "acquired-trait-authority-17-m20-work-two-erased",
                "event sequence changes from one repeated work type to a 20-event forge, delivery, alarm, and recovery history; domain combination adds FacilityUse, Invasion, Injury, and Mood; outcome includes Started, Blocked, Damaged, Changed, Returned, and Completed; prior active/erased state changes Steady Hands to active while Work Rhythm remains erased.",
                CuratedCycle("m20-work-rhythm", 20, Domains(CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.FacilityUse, CharacterNarrativeDomain.Invasion,
                    CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Mood,
                    CharacterNarrativeDomain.Work), Outcomes(CharacterActivityOutcomes.Started,
                    CharacterActivityOutcomes.Blocked, CharacterActivityOutcomes.Damaged,
                    CharacterActivityOutcomes.Changed, CharacterActivityOutcomes.Returned,
                    CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Failed)), Prior(3, steady, false), Prior(8, rhythm, true)),
            EvaluationPlan("unseen-eval-v1-m20-combat-battle-erased", 20,
                "acquired-trait-unseen-eval-v1-18",
                "acquired-trait-authority-18-m20-combat-work-survival-two-active",
                "event sequence changes from combat-work-survival facts to a 20-event scout, ambush, triage, and evacuation history; domain combination adds Expedition, Invasion, Injury, and Relationship; outcome adds Observed, Started, Damaged, Failed, Returned, and Completed; prior active/erased state changes Battle Temper to erased while Steady Hands remains active.",
                CuratedCycle("m20-combat-battle", 20, Domains(CharacterNarrativeDomain.Combat,
                    CharacterNarrativeDomain.Expedition, CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Invasion, CharacterNarrativeDomain.Relationship),
                    Outcomes(CharacterActivityOutcomes.Observed, CharacterActivityOutcomes.Started,
                    CharacterActivityOutcomes.Damaged, CharacterActivityOutcomes.Failed,
                    CharacterActivityOutcomes.Returned, CharacterActivityOutcomes.Completed,
                    CharacterActivityOutcomes.Responded)), Prior(3, battle, true), Prior(8, steady, false)),
            EvaluationPlan("unseen-eval-v1-m20-social-scar-mixed", 20,
                "acquired-trait-unseen-eval-v1-19",
                "acquired-trait-authority-19-m20-relationship-injury-mood-work-two-active",
                "event sequence changes from relationship-injury-mood-work facts to a 20-event dispute, treatment, vigil, and repair history; domain combination adds FacilityUse and Survival; outcome adds Failed, Damaged, Changed, Responded, Returned, and Completed; prior active/erased state changes Scar Memory from active to erased while Social Bridge remains active.",
                CuratedCycle("m20-social-scar", 20, Domains(CharacterNarrativeDomain.Relationship,
                    CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Mood,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Survival), Outcomes(CharacterActivityOutcomes.Failed,
                    CharacterActivityOutcomes.Damaged, CharacterActivityOutcomes.Changed,
                    CharacterActivityOutcomes.Responded, CharacterActivityOutcomes.Returned,
                    CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Blocked)), Prior(3, social, false), Prior(8, scar, true)),
            EvaluationPlan("unseen-eval-v1-m20-facility-ritual-night", 20,
                "acquired-trait-unseen-eval-v1-20",
                "acquired-trait-authority-20-m20-facility-work-survival-two-active",
                "event sequence changes from facility-work-survival facts to a 20-event ritual, shelter, delivery, and defense history; domain combination adds Invasion, Mood, and Relationship; outcome includes Started, Blocked, Changed, Damaged, Returned, and Completed; prior active/erased state changes Night Vigil from active to erased while Ritual Attunement remains active.",
                CuratedCycle("m20-facility-ritual", 20, Domains(CharacterNarrativeDomain.FacilityUse,
                    CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Invasion, CharacterNarrativeDomain.Mood,
                    CharacterNarrativeDomain.Relationship), Outcomes(CharacterActivityOutcomes.Started,
                    CharacterActivityOutcomes.Blocked, CharacterActivityOutcomes.Changed,
                    CharacterActivityOutcomes.Damaged, CharacterActivityOutcomes.Returned,
                    CharacterActivityOutcomes.Completed, CharacterActivityOutcomes.Failed)), Prior(3, ritual, false), Prior(8, night, true))
        };
    }

    private static ScenarioPlan[] BuildPlans()
    {
        const string battle = "acquired-trait:module:battle-temper";
        const string steady = "acquired-trait:module:steady-hands";
        const string scar = "acquired-trait:module:scar-memory";
        const string night = "acquired-trait:module:night-vigil";
        const string rhythm = "acquired-trait:module:work-rhythm";
        const string social = "acquired-trait:module:social-bridge";
        const string ritual = "acquired-trait:module:ritual-attunement";

        return new[]
        {
            Plan("m03-work-empty", 3, Domains(CharacterNarrativeDomain.Work)),
            Plan("m03-facility-empty", 3, Domains(CharacterNarrativeDomain.FacilityUse)),
            Plan("m03-injury-empty", 3, Domains(CharacterNarrativeDomain.Injury)),
            Plan("m03-mood-empty", 3, Domains(CharacterNarrativeDomain.Mood)),
            Plan("m03-relationship-empty", 3, Domains(CharacterNarrativeDomain.Relationship)),
            Plan("m03-survival-empty", 3, Domains(CharacterNarrativeDomain.Survival)),
            Plan("m03-combat-empty", 3, Domains(CharacterNarrativeDomain.Combat)),
            Plan("m08-work-active-steady", 8, Domains(CharacterNarrativeDomain.Work),
                Prior(3, steady, false)),
            Plan("m08-work-active-night", 8, Domains(CharacterNarrativeDomain.Work),
                Prior(3, night, false)),
            Plan("m08-work-survival-erased-night", 8,
                Domains(CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Survival),
                Prior(3, night, true)),
            Plan("m08-combat-survival-active-battle", 8,
                Domains(CharacterNarrativeDomain.Combat, CharacterNarrativeDomain.Survival),
                Prior(3, battle, false)),
            Plan("m08-relationship-work-active-social", 8,
                Domains(CharacterNarrativeDomain.Relationship, CharacterNarrativeDomain.Work),
                Prior(3, social, false)),
            Plan("m08-injury-mood-work-active-scar", 8,
                Domains(CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Mood,
                    CharacterNarrativeDomain.Work),
                Prior(3, scar, false)),
            Plan("m08-facility-work-active-ritual", 8,
                Domains(CharacterNarrativeDomain.FacilityUse, CharacterNarrativeDomain.Work),
                Prior(3, ritual, false)),
            Plan("m20-work-relationship-two-active", 20,
                Domains(CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Relationship),
                Prior(3, steady, false), Prior(8, social, false)),
            Plan("m20-work-survival-active-erased", 20,
                Domains(CharacterNarrativeDomain.Work, CharacterNarrativeDomain.Survival),
                Prior(3, steady, false), Prior(8, night, true)),
            Plan("m20-work-two-erased", 20, Domains(CharacterNarrativeDomain.Work),
                Prior(3, steady, true), Prior(8, rhythm, true)),
            Plan("m20-combat-work-survival-two-active", 20,
                Domains(CharacterNarrativeDomain.Combat, CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Survival),
                Prior(3, battle, false), Prior(8, steady, false)),
            Plan("m20-relationship-injury-mood-work-two-active", 20,
                Domains(CharacterNarrativeDomain.Relationship, CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Mood, CharacterNarrativeDomain.Work),
                Prior(3, social, false), Prior(8, scar, false)),
            Plan("m20-facility-work-survival-two-active", 20,
                Domains(CharacterNarrativeDomain.FacilityUse, CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.Survival),
                Prior(3, ritual, false), Prior(8, night, false))
        };
    }

    private static ScenarioPlan Plan(
        string slug,
        int milestone,
        CharacterNarrativeDomain[] domains,
        params PriorPlan[] priors) => new ScenarioPlan(
        slug,
        milestone,
        domains,
        priors,
        Array.Empty<LedgerEventPlan>(),
        null);

    private static ScenarioPlan EvaluationPlan(
        string slug,
        int milestone,
        string familyId,
        string nearestCalibrationScenarioId,
        string reason,
        LedgerEventPlan[] ledgerEvents,
        params PriorPlan[] priors) => new ScenarioPlan(
        slug,
        milestone,
        ledgerEvents.Select((ledgerEvent) => ledgerEvent.Domain)
            .Distinct()
            .ToArray(),
        priors,
        ledgerEvents,
        new NarrativeMechanicScenarioEvaluationMetadata(
            familyId,
            "unseen-evaluation",
            nearestCalibrationScenarioId,
            "distinct-context",
            reason));

    private static PriorPlan Prior(int milestone, string moduleId, bool erased) =>
        new PriorPlan(milestone, moduleId, erased);

    private static CharacterNarrativeDomain[] Domains(
        params CharacterNarrativeDomain[] domains) => domains;

    private static string[] Outcomes(params string[] outcomes) => outcomes;

    private static LedgerEventPlan[] CuratedEvents(
        string storyline,
        CharacterNarrativeDomain[] domains,
        string[] outcomes)
    {
        if (domains == null || outcomes == null || domains.Length < 2
            || domains.Length != outcomes.Length)
        {
            throw new ArgumentException(
                "Curated evaluation events require matching multi-event domain and outcome sequences.");
        }

        return domains.Select((domain, index) => Event(
            domain,
            $"{storyline}-{index + 1:D2}",
            $"{storyline}-subject-{index + 1:D2}",
            outcomes[index],
            dayOffset: index,
            publication: Publication(
                storyline,
                index + 1,
                domain,
                outcomes[index]))).ToArray();
    }

    private static LedgerEventPlan[] CuratedCycle(
        string storyline,
        int count,
        CharacterNarrativeDomain[] domains,
        string[] outcomes)
    {
        if (count < 2 || domains == null || outcomes == null
            || domains.Length < 2 || outcomes.Length < 2)
        {
            throw new ArgumentException(
                "Curated evaluation cycles require multi-event domain and outcome patterns.");
        }

        return Enumerable.Range(0, count).Select((index) => Event(
            domains[index % domains.Length],
            $"{storyline}-{index + 1:D2}",
            $"{storyline}-subject-{index + 1:D2}",
            outcomes[index % outcomes.Length],
            dayOffset: index,
            publication: Publication(
                storyline,
                index + 1,
                domains[index % domains.Length],
                outcomes[index % outcomes.Length]))).ToArray();
    }

    private static LedgerEventPlan Event(
        CharacterNarrativeDomain domain,
        string factToken,
        string subjectToken,
        string outcome,
        int repetitions = 1,
        float value = 1f,
        int dayOffset = 0,
        LedgerEventPublicationPlan publication = null) => new LedgerEventPlan(
        domain,
        factToken,
        subjectToken,
        outcome,
        repetitions,
        value,
        dayOffset,
        publication);

    private static LedgerEventPublicationPlan Publication(
        string storyline,
        int eventOrdinal,
        CharacterNarrativeDomain domain,
        string outcome)
    {
        if (eventOrdinal <= 0)
            throw new ArgumentOutOfRangeException(nameof(eventOrdinal));

        string activity = StorylineActivity(storyline);
        string semanticStoryline = SemanticStoryline(storyline);
        string targetName = TargetNameFor(domain);
        return new LedgerEventPublicationPlan(
            $"During stage {eventOrdinal:D2} of {activity}, {NarrativeAction(domain, outcome)} at {targetName}.",
            $"{semanticStoryline}-{SemanticDomain(domain)}-{SemanticOutcome(outcome)}",
            TargetKindFor(domain),
            targetName);
    }

    private static string StorylineActivity(string storyline) => storyline switch
    {
        "work-supply" => "repairing the interrupted supply line",
        "facility-clinic" => "reopening the clinic after treatment",
        "injury-escort" => "bringing an injured escort back to safety",
        "mood-watch" => "maintaining an overnight watch after an alarm",
        "relationship-crossing" => "crossing the river after a dispute",
        "survival-ration" => "stretching rations during a water search",
        "combat-guard" => "holding the outer gate against raiders",
        "work-steady" => "keeping the forge and delivery route running",
        "work-night-erased" => "covering the night patrol and repair route",
        "work-survival-night" => "setting shelter and restoring the water route",
        "combat-survival-battle" => "recovering from an ambush at field camp",
        "relationship-work-social" => "mediating a dispute while restoring deliveries",
        "injury-scar" => "treating an injury and returning to duty",
        "facility-ritual" => "calibrating and closing the ritual chamber",
        "m20-work-social" => "rebuilding the settlement through work and support",
        "m20-work-night" => "balancing rations, watch duty, and repairs",
        "m20-work-rhythm" => "recovering the forge rhythm after an alarm",
        "m20-combat-battle" => "evacuating the camp after a scout ambush",
        "m20-social-scar" => "resolving a dispute while caring for the injured",
        "m20-facility-ritual" => "securing the ritual shelter through the night",
        _ => throw Unsupported(
            "acquired-trait-evaluation-publication-storyline",
            $"Unknown acquired-trait evaluation storyline '{storyline ?? "null"}'.")
    };

    private static string SemanticStoryline(string storyline) => storyline switch
    {
        "work-supply" => "supply-line-repair",
        "facility-clinic" => "clinic-duty-return",
        "injury-escort" => "escort-recovery",
        "mood-watch" => "watch-morale-response",
        "relationship-crossing" => "river-reconciliation",
        "survival-ration" => "ration-water-search",
        "combat-guard" => "outer-gate-defense",
        "work-steady" => "forge-delivery-watch",
        "work-night-erased" => "night-patrol-repair",
        "work-survival-night" => "shelter-water-route",
        "combat-survival-battle" => "scout-ambush-triage",
        "relationship-work-social" => "mediation-delivery",
        "injury-scar" => "injury-duty-recovery",
        "facility-ritual" => "ritual-calibration",
        "m20-work-social" => "settlement-work-support",
        "m20-work-night" => "ration-watch-repair",
        "m20-work-rhythm" => "forge-alarm-recovery",
        "m20-combat-battle" => "scout-ambush-evacuation",
        "m20-social-scar" => "dispute-treatment-vigil",
        "m20-facility-ritual" => "ritual-shelter-defense",
        _ => throw Unsupported(
            "acquired-trait-evaluation-publication-storyline",
            $"Unknown acquired-trait evaluation storyline '{storyline ?? "null"}'.")
    };

    private static string NarrativeAction(
        CharacterNarrativeDomain domain,
        string outcome)
    {
        string participant = domain switch
        {
            CharacterNarrativeDomain.Work => "the work crew",
            CharacterNarrativeDomain.FacilityUse => "the facility operator",
            CharacterNarrativeDomain.Need => "the expedition group",
            CharacterNarrativeDomain.Mood => "the watch team",
            CharacterNarrativeDomain.Relationship => "the two partners",
            CharacterNarrativeDomain.Injury => "the field medic",
            CharacterNarrativeDomain.Survival => "the expedition party",
            CharacterNarrativeDomain.Invasion => "the gate guard",
            CharacterNarrativeDomain.Expedition => "the route scout",
            CharacterNarrativeDomain.Combat => "the defender",
            _ => throw Unsupported(
                "acquired-trait-evaluation-publication-domain",
                $"Unsupported acquired-trait evaluation domain '{domain}'.")
        };
        string task = domain switch
        {
            CharacterNarrativeDomain.Work => "the assigned repair",
            CharacterNarrativeDomain.FacilityUse => "the operating procedure",
            CharacterNarrativeDomain.Need => "the supply plan",
            CharacterNarrativeDomain.Mood => "the morale response",
            CharacterNarrativeDomain.Relationship => "the reconciliation effort",
            CharacterNarrativeDomain.Injury => "the treatment",
            CharacterNarrativeDomain.Survival => "the camp plan",
            CharacterNarrativeDomain.Invasion => "the defensive response",
            CharacterNarrativeDomain.Expedition => "the route survey",
            CharacterNarrativeDomain.Combat => "the engagement",
            _ => throw Unsupported(
                "acquired-trait-evaluation-publication-domain",
                $"Unsupported acquired-trait evaluation domain '{domain}'.")
        };
        return $"{participant} {NarrativeOutcomeVerb(outcome)} {task}";
    }

    private static string NarrativeOutcomeVerb(string outcome) => outcome switch
    {
        CharacterActivityOutcomes.Started => "began",
        CharacterActivityOutcomes.Blocked => "was blocked from completing",
        CharacterActivityOutcomes.Completed => "completed",
        CharacterActivityOutcomes.Changed => "adjusted",
        CharacterActivityOutcomes.Damaged => "continued after damage to",
        CharacterActivityOutcomes.Returned => "returned to",
        CharacterActivityOutcomes.Progress => "made progress on",
        CharacterActivityOutcomes.Failed => "could not complete",
        CharacterActivityOutcomes.Responded => "responded to",
        CharacterActivityOutcomes.Observed => "observed",
        _ => throw Unsupported(
            "acquired-trait-evaluation-publication-outcome",
            $"Unsupported acquired-trait evaluation outcome '{outcome ?? "null"}'.")
    };

    private static string SemanticDomain(CharacterNarrativeDomain domain) => domain switch
    {
        CharacterNarrativeDomain.Work => "work",
        CharacterNarrativeDomain.FacilityUse => "facility-use",
        CharacterNarrativeDomain.Need => "need",
        CharacterNarrativeDomain.Mood => "mood",
        CharacterNarrativeDomain.Relationship => "relationship",
        CharacterNarrativeDomain.Injury => "injury",
        CharacterNarrativeDomain.Survival => "survival",
        CharacterNarrativeDomain.Invasion => "invasion",
        CharacterNarrativeDomain.Expedition => "expedition",
        CharacterNarrativeDomain.Combat => "combat",
        _ => throw Unsupported(
            "acquired-trait-evaluation-publication-domain",
            $"Unsupported acquired-trait evaluation domain '{domain}'.")
    };

    private static string SemanticOutcome(string outcome) => outcome switch
    {
        CharacterActivityOutcomes.Started => "started",
        CharacterActivityOutcomes.Blocked => "blocked",
        CharacterActivityOutcomes.Completed => "completed",
        CharacterActivityOutcomes.Changed => "changed",
        CharacterActivityOutcomes.Damaged => "damaged",
        CharacterActivityOutcomes.Returned => "returned",
        CharacterActivityOutcomes.Progress => "progressed",
        CharacterActivityOutcomes.Failed => "failed",
        CharacterActivityOutcomes.Responded => "responded",
        CharacterActivityOutcomes.Observed => "observed",
        _ => throw Unsupported(
            "acquired-trait-evaluation-publication-outcome",
            $"Unsupported acquired-trait evaluation outcome '{outcome ?? "null"}'.")
    };

    private static NarrativePublicEntityKind TargetKindFor(
        CharacterNarrativeDomain domain) => domain switch
    {
        CharacterNarrativeDomain.Work => NarrativePublicEntityKind.Facility,
        CharacterNarrativeDomain.FacilityUse => NarrativePublicEntityKind.Facility,
        CharacterNarrativeDomain.Need => NarrativePublicEntityKind.Group,
        CharacterNarrativeDomain.Mood => NarrativePublicEntityKind.Group,
        CharacterNarrativeDomain.Relationship => NarrativePublicEntityKind.Character,
        CharacterNarrativeDomain.Injury => NarrativePublicEntityKind.Character,
        CharacterNarrativeDomain.Survival => NarrativePublicEntityKind.Group,
        CharacterNarrativeDomain.Invasion => NarrativePublicEntityKind.Facility,
        CharacterNarrativeDomain.Expedition => NarrativePublicEntityKind.Place,
        CharacterNarrativeDomain.Combat => NarrativePublicEntityKind.Character,
        _ => throw Unsupported(
            "acquired-trait-evaluation-publication-domain",
            $"Unsupported acquired-trait evaluation domain '{domain}'.")
    };

    private static string TargetNameFor(CharacterNarrativeDomain domain) => domain switch
    {
        CharacterNarrativeDomain.Work => "Workshop",
        CharacterNarrativeDomain.FacilityUse => "Facility station",
        CharacterNarrativeDomain.Need => "Supply party",
        CharacterNarrativeDomain.Mood => "Watch team",
        CharacterNarrativeDomain.Relationship => "Field partner",
        CharacterNarrativeDomain.Injury => "Injured escort",
        CharacterNarrativeDomain.Survival => "Expedition camp",
        CharacterNarrativeDomain.Invasion => "Outer gate",
        CharacterNarrativeDomain.Expedition => "Expedition route",
        CharacterNarrativeDomain.Combat => "Raider line",
        _ => throw Unsupported(
            "acquired-trait-evaluation-publication-domain",
            $"Unsupported acquired-trait evaluation domain '{domain}'.")
    };

    private static string BuildFactId(
        string scenarioId,
        CharacterNarrativeDomain domain,
        int ordinal) =>
        $"fixture:{scenarioId}:{domain.ToString().ToLowerInvariant()}:{ordinal:D2}";

    private static IReadOnlyList<NarrativeLedgerPublicationDescriptor>
        BuildEvaluationLedgerPublications(
            ScenarioPlan plan,
            string scenarioId,
            CharacterNarrativeLedger ledger)
    {
        if (plan == null
            || plan.EvaluationMetadata == null
            || plan.LedgerEvents == null
            || plan.LedgerEvents.Length == 0)
        {
            throw Unsupported(
                "acquired-trait-evaluation-publication-plan-missing",
                $"Evaluation scenario '{scenarioId ?? "unknown"}' has no ledger publication plan.");
        }
        if (ledger == null)
        {
            throw Unsupported(
                "acquired-trait-evaluation-publication-ledger-missing",
                $"Evaluation scenario '{scenarioId}' has no narrative ledger.");
        }

        CharacterNarrativeFact[] ledgerFacts = ledger.Facts
            .Where((fact) => fact != null)
            .ToArray();
        if (ledgerFacts.Length != plan.LedgerEvents.Length)
        {
            throw Unsupported(
                "acquired-trait-evaluation-publication-count-mismatch",
                $"Evaluation scenario '{scenarioId}' recorded {ledgerFacts.Length} ledger tuples for {plan.LedgerEvents.Length} publication plans.");
        }

        HashSet<string> expectedTuples = new(StringComparer.Ordinal);
        List<NarrativeLedgerPublicationDescriptor> descriptors =
            new(plan.LedgerEvents.Length);
        for (int eventIndex = 0; eventIndex < plan.LedgerEvents.Length; eventIndex++)
        {
            LedgerEventPlan ledgerEvent = plan.LedgerEvents[eventIndex];
            LedgerEventPublicationPlan publication = ledgerEvent?.Publication;
            if (publication == null)
            {
                throw Unsupported(
                    "acquired-trait-evaluation-publication-missing",
                    $"Evaluation scenario '{scenarioId}' event {eventIndex + 1} has no public publication.");
            }

            string originalFactId = BuildLedgerEventFactId(
                scenarioId,
                eventIndex,
                ledgerEvent);
            string sourceSubjectId = BuildLedgerEventSubjectId(scenarioId, ledgerEvent);
            string tuple = BuildLedgerPublicationTuple(
                ledgerEvent.Domain,
                originalFactId,
                sourceSubjectId);
            if (!expectedTuples.Add(tuple))
            {
                throw Unsupported(
                    "acquired-trait-evaluation-publication-duplicate",
                    $"Evaluation scenario '{scenarioId}' repeats ledger publication tuple '{tuple}'.");
            }

            CharacterNarrativeFact[] matchingFacts = ledgerFacts.Where((fact) =>
                fact.domain == ledgerEvent.Domain
                && string.Equals(fact.factId, originalFactId, StringComparison.Ordinal)
                && string.Equals(fact.subjectId, sourceSubjectId, StringComparison.Ordinal))
                .ToArray();
            if (matchingFacts.Length != 1)
            {
                RequireExactLedgerTuple(
                    scenarioId,
                    eventIndex,
                    ledgerEvent,
                    originalFactId,
                    sourceSubjectId,
                    ledgerFacts,
                    matchingFacts.Length);
            }

            NarrativePublicEntityInput targetEntity = new(
                sourceSubjectId,
                publication.TargetEntityKind,
                publication.TargetEntityName);
            if (!string.Equals(
                    targetEntity.EntityId,
                    sourceSubjectId,
                    StringComparison.Ordinal))
            {
                throw Unsupported(
                    "acquired-trait-evaluation-publication-target-mismatch",
                    $"Evaluation scenario '{scenarioId}' event {eventIndex + 1} target does not equal its source subject.");
            }
            descriptors.Add(new NarrativeLedgerPublicationDescriptor(
                ledgerEvent.Domain,
                originalFactId,
                sourceSubjectId,
                publication.Sentence,
                publication.EventType,
                targetEntity));
        }

        foreach (CharacterNarrativeFact fact in ledgerFacts)
        {
            string actualTuple = BuildLedgerPublicationTuple(
                fact.domain,
                fact.factId,
                fact.subjectId);
            if (!expectedTuples.Contains(actualTuple))
            {
                throw Unsupported(
                    "acquired-trait-evaluation-publication-extra-ledger",
                    $"Evaluation scenario '{scenarioId}' recorded unplanned ledger tuple '{actualTuple}'.");
            }
        }
        if (descriptors.Count != expectedTuples.Count)
        {
            throw Unsupported(
                "acquired-trait-evaluation-publication-descriptor-count",
                $"Evaluation scenario '{scenarioId}' produced {descriptors.Count} descriptors for {expectedTuples.Count} ledger tuples.");
        }

        return descriptors;
    }

    private static void RequireExactLedgerTuple(
        string scenarioId,
        int eventIndex,
        LedgerEventPlan ledgerEvent,
        string originalFactId,
        string sourceSubjectId,
        IReadOnlyList<CharacterNarrativeFact> ledgerFacts,
        int matchingFactCount)
    {
        if (ledgerEvent == null)
        {
            throw Unsupported(
                "acquired-trait-evaluation-publication-event-missing",
                $"Evaluation scenario '{scenarioId}' event {eventIndex + 1} is null.");
        }
        CharacterNarrativeFact domainMismatch = ledgerFacts.FirstOrDefault((fact) =>
            string.Equals(fact.factId, originalFactId, StringComparison.Ordinal)
            && string.Equals(fact.subjectId, sourceSubjectId, StringComparison.Ordinal)
            && fact.domain != ledgerEvent.Domain);
        if (domainMismatch != null)
        {
            throw Unsupported(
                "acquired-trait-evaluation-publication-domain-mismatch",
                $"Evaluation scenario '{scenarioId}' event {eventIndex + 1} ledger domain is '{domainMismatch.domain}', not '{ledgerEvent.Domain}'.");
        }
        CharacterNarrativeFact subjectMismatch = ledgerFacts.FirstOrDefault((fact) =>
            fact.domain == ledgerEvent.Domain
            && string.Equals(fact.factId, originalFactId, StringComparison.Ordinal)
            && !string.Equals(fact.subjectId, sourceSubjectId, StringComparison.Ordinal));
        if (subjectMismatch != null)
        {
            throw Unsupported(
                "acquired-trait-evaluation-publication-subject-mismatch",
                $"Evaluation scenario '{scenarioId}' event {eventIndex + 1} ledger subject is '{subjectMismatch.subjectId}', not '{sourceSubjectId}'.");
        }
        throw Unsupported(
            matchingFactCount == 0
                ? "acquired-trait-evaluation-publication-tuple-missing"
                : "acquired-trait-evaluation-publication-tuple-duplicate",
            $"Evaluation scenario '{scenarioId}' event {eventIndex + 1} requires exactly one {ledgerEvent.Domain} ledger tuple for fact '{originalFactId}' and subject '{sourceSubjectId}', but found {matchingFactCount}.");
    }

    private static string BuildLedgerEventFactId(
        string scenarioId,
        int eventIndex,
        LedgerEventPlan ledgerEvent) =>
        $"fixture:{scenarioId}:event:{eventIndex + 1:D2}:{ledgerEvent.FactToken}";

    private static string BuildLedgerEventSubjectId(
        string scenarioId,
        LedgerEventPlan ledgerEvent) =>
        $"fixture-subject:{scenarioId}:{ledgerEvent.SubjectToken}";

    private static string BuildLedgerPublicationTuple(
        CharacterNarrativeDomain domain,
        string originalFactId,
        string sourceSubjectId)
    {
        string factId = originalFactId?.Trim() ?? string.Empty;
        string subjectId = sourceSubjectId?.Trim() ?? string.Empty;
        return ((int)domain).ToString(CultureInfo.InvariantCulture)
            + ":" + factId.Length.ToString(CultureInfo.InvariantCulture)
            + ":" + factId
            + ":" + subjectId.Length.ToString(CultureInfo.InvariantCulture)
            + ":" + subjectId;
    }

    private static string BuildFactText(
        string scenarioId,
        CharacterNarrativeDomain domain,
        int ordinal)
    {
        string eventText = domain switch
        {
            CharacterNarrativeDomain.Work => "제작과 정비 작업을 완료했다",
            CharacterNarrativeDomain.FacilityUse => "시설을 사용해 연구 절차를 마쳤다",
            CharacterNarrativeDomain.Need => "생존 욕구를 해결했다",
            CharacterNarrativeDomain.Mood => "강한 감정의 변화를 견뎠다",
            CharacterNarrativeDomain.Relationship => "동료와 갈등을 풀고 관계를 회복했다",
            CharacterNarrativeDomain.Injury => "전투에서 입은 부상을 버텼다",
            CharacterNarrativeDomain.Survival => "위험한 생존 상황을 넘겼다",
            CharacterNarrativeDomain.Invasion => "침입 상황에서 거점을 지켰다",
            CharacterNarrativeDomain.Expedition => "원정 목표를 해결하고 돌아왔다",
            CharacterNarrativeDomain.Combat => "전투 조우를 끝까지 치렀다",
            _ => throw Unsupported(
                "acquired-trait-fixture-domain",
                $"Scenario '{scenarioId}' uses unsupported domain '{domain}'.")
        };
        return $"{eventText} — 통제 기록 {ordinal:D2}";
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray StringArray(
        IEnumerable<string> values) => new NarrativeMechanicCatalogCanonicalJsonArray(
        (values ?? Array.Empty<string>()).Select((value) =>
            (NarrativeMechanicCatalogCanonicalJsonValue)String(value)));

    private static NarrativeMechanicCatalogCanonicalJsonArray IntegerArray(
        IEnumerable<int> values) => new NarrativeMechanicCatalogCanonicalJsonArray(
        (values ?? Array.Empty<int>()).Select((value) =>
            (NarrativeMechanicCatalogCanonicalJsonValue)Integer(value)));

    private static KeyValuePair<string, string> Axis(string key, string value) =>
        new KeyValuePair<string, string>(key, value);

    private static KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue> Property(
        string key,
        NarrativeMechanicCatalogCanonicalJsonValue value) =>
        NarrativeMechanicCatalogCanonicalJson.Property(key, value);

    private static NarrativeMechanicCatalogCanonicalJsonString String(string value) =>
        NarrativeMechanicCatalogCanonicalJson.String(value ?? string.Empty);

    private static NarrativeMechanicCatalogCanonicalJsonInt64 Integer(long value) =>
        NarrativeMechanicCatalogCanonicalJson.Integer(value);

    private static NarrativeMechanicCatalogCanonicalJsonBoolean Boolean(bool value) =>
        NarrativeMechanicCatalogCanonicalJson.Boolean(value);

    private static NarrativeMechanicCatalogUnsupportedSourceException Unsupported(
        string code,
        string message) => new NarrativeMechanicCatalogUnsupportedSourceException(code, message);

    private sealed class ScenarioPlan
    {
        public ScenarioPlan(
            string slug,
            int milestone,
            CharacterNarrativeDomain[] domains,
            PriorPlan[] priors,
            LedgerEventPlan[] ledgerEvents,
            NarrativeMechanicScenarioEvaluationMetadata evaluationMetadata)
        {
            Slug = slug;
            Milestone = milestone;
            Domains = domains ?? Array.Empty<CharacterNarrativeDomain>();
            Priors = priors ?? Array.Empty<PriorPlan>();
            LedgerEvents = ledgerEvents ?? Array.Empty<LedgerEventPlan>();
            EvaluationMetadata = evaluationMetadata;
            if (Domains.Length == 0)
                throw new ArgumentException("Scenario plan requires at least one domain.");
        }

        public string Slug { get; }
        public int Milestone { get; }
        public CharacterNarrativeDomain[] Domains { get; }
        public PriorPlan[] Priors { get; }
        public LedgerEventPlan[] LedgerEvents { get; }
        public NarrativeMechanicScenarioEvaluationMetadata EvaluationMetadata { get; }
        public string StateAxis => Priors.Length == 0
            ? "empty"
            : string.Join("+", Priors.Select((prior) =>
                $"m{prior.Milestone}-{(prior.Erased ? "erased" : "active")}-{prior.ModuleId}"));
    }

    private sealed class LedgerEventPlan
    {
        public LedgerEventPlan(
            CharacterNarrativeDomain domain,
            string factToken,
            string subjectToken,
            string outcome,
            int repetitions,
            float value,
            int dayOffset,
            LedgerEventPublicationPlan publication)
        {
            if (string.IsNullOrWhiteSpace(factToken)
                || string.IsNullOrWhiteSpace(subjectToken)
                || string.IsNullOrWhiteSpace(outcome)
                || repetitions <= 0
                || publication == null)
            {
                throw new ArgumentException(
                    "Narrative ledger event plans require canonical values, a positive repetition count, and a public publication.");
            }

            Domain = domain;
            FactToken = factToken;
            SubjectToken = subjectToken;
            Outcome = outcome;
            Repetitions = repetitions;
            Value = value;
            DayOffset = dayOffset;
            Publication = publication;
        }

        public CharacterNarrativeDomain Domain { get; }
        public string FactToken { get; }
        public string SubjectToken { get; }
        public string Outcome { get; }
        public int Repetitions { get; }
        public float Value { get; }
        public int DayOffset { get; }
        public LedgerEventPublicationPlan Publication { get; }
    }

    private sealed class LedgerEventPublicationPlan
    {
        public LedgerEventPublicationPlan(
            string sentence,
            string eventType,
            NarrativePublicEntityKind targetEntityKind,
            string targetEntityName)
        {
            if (string.IsNullOrWhiteSpace(sentence)
                || string.IsNullOrWhiteSpace(eventType)
                || string.IsNullOrWhiteSpace(targetEntityName)
                || eventType.Any(char.IsWhiteSpace)
                || eventType.IndexOf("fixture", StringComparison.OrdinalIgnoreCase) >= 0
                || sentence.IndexOf("fixture", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new ArgumentException(
                    "Narrative ledger publications require a readable sentence, semantic non-fixture event type, and public target.");
            }

            Sentence = sentence;
            EventType = eventType;
            TargetEntityKind = targetEntityKind;
            TargetEntityName = targetEntityName;
        }

        public string Sentence { get; }
        public string EventType { get; }
        public NarrativePublicEntityKind TargetEntityKind { get; }
        public string TargetEntityName { get; }
    }

    private sealed class PriorPlan
    {
        public PriorPlan(int milestone, string moduleId, bool erased)
        {
            Milestone = milestone;
            ModuleId = moduleId;
            Erased = erased;
        }

        public int Milestone { get; }
        public string ModuleId { get; }
        public bool Erased { get; }
    }

    private sealed class PacketFixture
    {
        public PacketFixture(
            string scenarioId,
            ScenarioPlan plan,
            CharacterNarrativeLedger ledger,
            CharacterAcquiredTraitAggregateState state,
            CharacterAcquiredTraitSubmissionCommand command,
            CharacterAcquiredTraitRequestPacketDto packet,
            NarrativeMechanicScenarioPublicContext publicContext)
        {
            ScenarioId = scenarioId;
            Plan = plan;
            Ledger = ledger;
            State = state;
            Command = command;
            Packet = packet;
            PublicContext = publicContext;
        }

        public string ScenarioId { get; }
        public ScenarioPlan Plan { get; }
        public CharacterNarrativeLedger Ledger { get; }
        public CharacterAcquiredTraitAggregateState State { get; }
        public CharacterAcquiredTraitSubmissionCommand Command { get; }
        public CharacterAcquiredTraitRequestPacketDto Packet { get; }
        public NarrativeMechanicScenarioPublicContext PublicContext { get; }
    }

    private sealed class ControlledActorFixture : IDisposable
    {
        private readonly CharacterSO data;
        private readonly CharacterSkillSystemSettingsSO skillSettings;

        public ControlledActorFixture(
            int seed,
            string displayName,
            string persistentId)
        {
            skillSettings = EditorCharacterSkillSettingsFactory
                .CreateTransientDefaults();
            data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                CharacterType.NPC,
                displayName,
                "human");
            data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

            GameObject actorObject = new($"NarrativeMechanicActor_{seed}");
            actorObject.AddComponent<SpriteRenderer>();
            Actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            actorObject.AddComponent<AbilityWork>();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            Actor.RefreshAbilityCache();
            Actor.Identity.SetPersistentId(persistentId);
            Actor.Initialization(data);
            Actor.Progression.ApplyPreparedIdentity(
                displayName,
                "controlled acquired-trait lifecycle export fixture",
                Array.Empty<int>(),
                CharacterGrowthRules.RollPotential(
                    skillSettings,
                    new System.Random(seed)),
                seed,
                autoChooseDrafts: false);
            Actor.SetLifecycleState(CharacterLifecycleState.Active);
        }

        public CharacterActor Actor { get; }

        public void Dispose()
        {
            if (Actor != null)
            {
                UnityEngine.Object.DestroyImmediate(Actor.gameObject);
            }
            if (data != null)
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
            if (skillSettings != null)
            {
                UnityEngine.Object.DestroyImmediate(skillSettings);
            }
        }
    }

    private sealed class ControlledDefinitionSource :
        IGameContentDefinitionSource
    {
        private readonly CharacterAcquiredTraitSettingsSO settings;
        private readonly CharacterAcquiredTraitModuleSO[] modules;

        public ControlledDefinitionSource(
            CharacterAcquiredTraitSettingsSO settings,
            IEnumerable<CharacterAcquiredTraitModuleSO> modules)
        {
            this.settings = settings
                ?? throw new ArgumentNullException(nameof(settings));
            this.modules = (modules
                    ?? throw new ArgumentNullException(nameof(modules)))
                .Where((value) => value != null)
                .ToArray();
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            if (typeof(T) == typeof(CharacterAcquiredTraitSettingsSO))
            {
                return new[] { settings }.Cast<T>().ToArray();
            }
            if (typeof(T) == typeof(CharacterAcquiredTraitModuleSO))
            {
                return modules.Cast<T>().ToArray();
            }
            return Array.Empty<T>();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> values = GetAll<T>();
            if (values.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Controlled source expected one {typeof(T).Name}, found {values.Count}.");
            }
            return values[0];
        }
    }

    private sealed class ControlledGameCalendar : IGameCalendar
    {
        private int day;
        private int hour;

        public ControlledGameCalendar(int day, int hour)
        {
            this.day = Math.Max(1, day);
            this.hour = Math.Clamp(hour, 0, 23);
        }

        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => hour / 24f * GameCalendarRules.SecondsPerDay;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, 23);
        }
    }

    private sealed class ControlledReversibleCarriedDisposition :
        IReversibleCarriedPhysicalItemBatchDispositionService
    {
        private readonly string expectedStackId;

        public ControlledReversibleCarriedDisposition(string expectedStackId)
        {
            this.expectedStackId = expectedStackId;
        }

        public bool TryCommitCarriedSinkReversible(
            string stackId,
            int quantity,
            string operationId,
            string reasonCode,
            out IReversiblePhysicalItemDisposition transaction,
            out string failureReason)
        {
            bool exact = string.Equals(
                    stackId,
                    expectedStackId,
                    StringComparison.Ordinal)
                && quantity == MemoryErasureSealItemRules.UseQuantity
                && !string.IsNullOrWhiteSpace(operationId)
                && string.Equals(
                    reasonCode,
                    "memory-erasure-seal-consume",
                    StringComparison.Ordinal);
            if (!exact)
            {
                transaction = null;
                failureReason = "controlled-physical-request-mismatch";
                return false;
            }

            transaction = new ControlledReversibleDisposition();
            failureReason = string.Empty;
            return true;
        }

        private sealed class ControlledReversibleDisposition :
            IReversiblePhysicalItemDisposition
        {
            private bool terminal;

            public PhysicalItemBatchDispositionReceipt Receipt => default;

            public bool TryRollback(out string failureReason)
            {
                if (terminal)
                {
                    failureReason = "controlled-physical-already-terminal";
                    return false;
                }
                terminal = true;
                failureReason = string.Empty;
                return true;
            }

            public bool TryAcknowledge(out string failureReason)
            {
                if (terminal)
                {
                    failureReason = "controlled-physical-already-terminal";
                    return false;
                }
                terminal = true;
                failureReason = string.Empty;
                return true;
            }
        }
    }
}
#endif
