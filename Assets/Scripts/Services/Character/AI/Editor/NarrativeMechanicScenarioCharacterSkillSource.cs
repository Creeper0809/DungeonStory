#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Captures controlled CharacterSkill fixtures through the production draft,
/// legal-combination and response-validation authorities. The fixture ledger is
/// intentionally controlled and is not evidence of natural gameplay occurrence.
/// </summary>
public sealed class NarrativeMechanicScenarioCharacterSkillSource
    : INarrativeMechanicScenarioSource
{
    // CreateDraft derives its deterministic random stream from its request key
    // (actor ID, kind, unlock level, and revision). CharacterGrowthState's
    // generationSeed is not an input once an actor is bound, so the controlled
    // capture must vary a real request-key component when it searches for a
    // legal representative draft.
    private const int MaximumRevisionSearch = 4096;
    private const string SourcePath =
        "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioCharacterSkillSource.cs";
    private const string ProducerIdentifier =
        "CharacterSkillGenerationService.CreateDraft+"
        + "CharacterSkillCombinationCatalog.Build+"
        + "CharacterSkillCombinationSemanticsFactory.Create+"
        + "CharacterSkillPromptBuilder.Build";
    private const string ValidatorIdentifier =
        "CharacterSkillGenerationService.TryValidateResponse";

    private readonly CharacterSkillSystemSettingsSO suppliedSettings;
    private readonly NarrativeMechanicScenarioSetKind scenarioSetKind;

    public NarrativeMechanicScenarioCharacterSkillSource()
        : this(NarrativeMechanicScenarioSetKind.Calibration)
    {
    }

    public NarrativeMechanicScenarioCharacterSkillSource(
        NarrativeMechanicScenarioSetKind scenarioSetKind)
    {
        this.scenarioSetKind = scenarioSetKind;
    }

    internal NarrativeMechanicScenarioCharacterSkillSource(
        CharacterSkillSystemSettingsSO settings)
        : this(settings, NarrativeMechanicScenarioSetKind.Calibration)
    {
    }

    internal NarrativeMechanicScenarioCharacterSkillSource(
        CharacterSkillSystemSettingsSO settings,
        NarrativeMechanicScenarioSetKind scenarioSetKind)
    {
        suppliedSettings = settings;
        this.scenarioSetKind = scenarioSetKind;
    }

    public string ProfileId => NarrativeMechanicScenarioProfiles.CharacterSkill;

    public NarrativeMechanicScenarioSourceCapture CaptureScenarios()
    {
        ValidateScenarioSetKind();
        CharacterSkillSystemSettingsSO settings = ResolveSettings();
        ValidateAuthoredAuthority(settings);

        ScenarioPlan[] plans = IsUnseenNamingEvaluation
            ? BuildUnseenNamingEvaluationPlans()
            : BuildPlans();
        RequirePlanCount(plans, CharacterSkillKind.Active, 8);
        RequirePlanCount(plans, CharacterSkillKind.Passive, 7);
        RequirePlanCount(plans, CharacterSkillKind.Ultimate, 5);
        RequireEvaluationMetadata(plans);

        CharacterSkillGenerationService authority = new CharacterSkillGenerationService(
            new FixedSettingsProvider(settings),
            new UnavailableRuntimeProvider(),
            new FixedUiClock());
        ScenarioMaterial[] materials = plans
            .Select((plan, index) => CapturePositive(
                plan,
                index + 1,
                settings,
                authority))
            .ToArray();

        NarrativeMechanicScenario[] negatives;
        if (IsUnseenNamingEvaluation)
        {
            // The evaluation provider merges these positives with the unchanged
            // calibration negatives. Re-capturing negatives from altered fixture
            // inputs would not be an equivalent v26 regression witness.
            negatives = Array.Empty<NarrativeMechanicScenario>();
        }
        else
        {
            ScenarioMaterial negativeBase = materials.First((material) =>
                material.Draft.kind == CharacterSkillKind.Active);
            ScenarioMaterial negativeNeedChangedPassive = materials.First((material) =>
                material.Draft.kind == CharacterSkillKind.Passive);
            negatives = BuildNegativeScenarios(
                negativeBase,
                negativeNeedChangedPassive,
                authority);
        }
        return new NarrativeMechanicScenarioSourceCapture(
            ProfileId,
            materials.Select((material) => material.Scenario),
            negatives,
            CaptureRelevantPaths(settings),
            allowEmptyNegativeScenarios: IsUnseenNamingEvaluation);
    }

    private static ScenarioMaterial CapturePositive(
        ScenarioPlan plan,
        int ordinal,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillGenerationService authority)
    {
        string scenarioId = $"character-skill-authority-{ordinal:D2}-{plan.Slug}";
        using ControlledCharacterSkillFixture fixture =
            new(scenarioId, ordinal);
        {
            CharacterProgression progression = fixture.Progression;
            CharacterActor actor = fixture.Actor;
            if (progression == null || actor == null)
            {
                throw Unsupported(
                    "character-skill-scenario-actor-missing",
                    $"Scenario '{scenarioId}' could not establish a production CharacterActor and progression.");
            }
            CharacterSkillDraft draft = null;
            List<RuleOptions> ruleOptions = null;
            int resolvedRevision = -1;
            for (int offset = 0; offset < MaximumRevisionSearch; offset++)
            {
                int candidateRevision = checked(plan.Revision + offset);
                ConfigureFixture(
                    progression,
                    plan,
                    ordinal,
                    scenarioId,
                    candidateRevision);
                CharacterSkillDraft candidate = authority.CreateDraft(
                    progression,
                    plan.Kind,
                    plan.UnlockLevel,
                    candidateRevision);
                if (!MatchesPlan(candidate, plan)
                    || !TryBuildRuleOptions(candidate, settings, out List<RuleOptions> options))
                {
                    continue;
                }

                draft = candidate;
                ruleOptions = options;
                resolvedRevision = candidateRevision;
                break;
            }

            if (draft == null || ruleOptions == null)
            {
                throw Unsupported(
                    "character-skill-scenario-production-unavailable",
                    $"Production CreateDraft could not produce scenario '{scenarioId}' "
                    + $"with kind={plan.Kind}, trigger={FormatOptional(plan.ExpectedTrigger)}, "
                    + $"target={FormatOptional(plan.ExpectedTarget)} and distinct legal candidate "
                    + $"identities in {MaximumRevisionSearch} deterministic request revisions.");
            }

            ResponseCandidate[] responseCandidates = ruleOptions
                .Select((options, candidateIndex) => new ResponseCandidate(
                    draft.moduleSelectionOffers[draft.nextPresentationIndex].selectionId,
                    options.Selected.Id,
                    draft.moduleSelectionOffers[draft.nextPresentationIndex].evidenceFactIds[0],
                    "리안의 기술",
                    "리안의 합법 조합 효과입니다.",
                    "리안의 실제 기록에서 얻은 기술입니다."))
                .ToArray();
            string responseJson = BuildResponseJson(responseCandidates);
            bool accepted = authority.TryValidateResponse(
                draft,
                responseJson,
                out List<CharacterSkillInstance> validatedSkills,
                out string validationError);
            if (!accepted)
            {
                throw Unsupported(
                    "character-skill-scenario-valid-response-rejected",
                    $"Production validator rejected scenario '{scenarioId}': {validationError}");
            }
            RequireValidatedSelections(
                scenarioId,
                ruleOptions,
                validatedSkills);

            NarrativePublicContextMaterial publicMaterial = plan.EvaluationMetadata == null
                ? CharacterSkillPromptBuilder.BuildPublicMaterial(progression)
                : CharacterSkillPromptBuilder.BuildPublicMaterial(
                    progression,
                    BuildEvaluationLedgerPublications(plan, scenarioId, actor));
            NarrativePublicPromptEnvelope envelope = CharacterSkillPromptBuilder.BuildEnvelope(
                progression,
                draft,
                settings,
                publicMaterial);
            RequireProductionEnvelope(scenarioId, publicMaterial, envelope);
            NarrativeMechanicCatalogCanonicalJsonObject request = plan.EvaluationMetadata == null
                ? ToRequestJson(
                    progression,
                    draft,
                    settings,
                    ruleOptions)
                : ToRequestJson(
                    draft,
                    settings,
                    ruleOptions,
                    envelope.Prompt);
            NarrativeMechanicCatalogCanonicalJsonArray fullLegalCandidates =
                ToFullLegalCandidatesJson(ruleOptions, settings, draft.kind);
            NarrativeMechanicScenarioPublicContext publicContext =
                NarrativeMechanicScenarioPublicContextSerializer.Serialize(publicMaterial);
            string[] effectDescriptions = BuildEffectDescriptions(
                ruleOptions,
                settings,
                draft.kind);
            NarrativeMechanicCatalogCanonicalJsonObject fixtureInput = BuildFixtureInput(
                plan,
                progression.NarrativeLedger,
                resolvedRevision);
            NarrativeMechanicCatalogCanonicalJsonObject authorityContext =
                BuildAuthorityContext(
                    settings,
                    draft,
                    responseJson,
                    accepted,
                    validationError,
                    validatedSkills,
                    ruleOptions);
            NarrativeMechanicScenario scenario = new NarrativeMechanicScenario(
                scenarioId,
                ProfileToken,
                request,
                fullLegalCandidates,
                publicContext.TargetPersistentId,
                publicContext,
                effectDescriptions,
                authorityContext,
                fixtureInput,
                ProducerIdentifier,
                ValidatorIdentifier,
                accepted: true,
                failureReason: string.Empty,
                diversityAxes: new[]
                {
                    Axis("kind", plan.Kind.ToString()),
                    Axis("ledgerDomain", plan.LedgerDomain.ToString()),
                    Axis("potential", plan.Potential.ToString()),
                    Axis("pity", plan.Pity ? "true" : "false"),
                    Axis("rarities", string.Join("+", draft.rules.Select((rule) =>
                        rule.rarity.ToString()).Distinct().OrderBy((value) => value,
                            StringComparer.Ordinal))),
                    Axis("target", draft.rules[0].target.ToString()),
                    Axis("trigger", draft.rules[0].trigger.ToString()),
                    Axis("ultimateDomain", draft.rules[0].ultimateDomain.ToString()),
                    Axis("unlockLevel", plan.UnlockLevel.ToString(CultureInfo.InvariantCulture))
                },
                evaluationMetadata: plan.EvaluationMetadata);
            return new ScenarioMaterial(
                scenario,
                draft,
                request,
                fullLegalCandidates,
                publicContext,
                effectDescriptions,
                fixtureInput,
                ruleOptions,
                responseCandidates);
        }
    }

    private static NarrativeMechanicScenario[] BuildNegativeScenarios(
        ScenarioMaterial material,
        ScenarioMaterial needChangedPassiveMaterial,
        CharacterSkillGenerationService authority)
    {
        ResponseCandidate[] forgedModule = material.ResponseCandidates
            .Select((candidate) => candidate.Copy())
            .ToArray();
        forgedModule[0].CombinationId = "skill-module:invalid";

        ResponseCandidate[] emptyPositive = material.ResponseCandidates
            .Select((candidate) => candidate.Copy())
            .ToArray();
        emptyPositive[0].IncludePositiveModule = false;

        ResponseCandidate[] staleSelection = material.ResponseCandidates
            .Select((candidate) => candidate.Copy())
            .ToArray();
        staleSelection[0].RuleId = "selection:skill:" + new string('0', 64);

        ResponseCandidate[] inventedEvidence = material.ResponseCandidates
            .Select((candidate) => candidate.Copy())
            .ToArray();
        inventedEvidence[0].EvidenceFactId = "skill-evidence:" + new string('0', 64);

        return new[]
        {
            CaptureNegative(
                "character-skill-negative-forged-module",
                "forged-module-id",
                material,
                authority,
                forgedModule),
            CaptureNegative(
                "character-skill-negative-empty-positive",
                "negative-or-empty-positive-selection",
                material,
                authority,
                emptyPositive),
            CaptureNegative(
                "character-skill-negative-stale-selection",
                "stale-selection-id",
                material,
                authority,
                staleSelection),
            CaptureNegative(
                "character-skill-negative-invented-evidence",
                "invented-evidence-id",
                material,
                authority,
                inventedEvidence)
        };
    }

    private static ResponseCandidate[] BuildFormerNeedChangedV17CleaningStockSmallResponse(
        ScenarioMaterial material)
    {
        const string V17RuleId =
            "skill-rule:sha256:c42d42782c26aed0c69fa79a1da62cb55e7dda200b9e132dac15806c095ff261";
        const string V17MechanicalIdentity =
            "Passive|Advanced|NeedChanged|Self|None|0|cleaning|small,stock|small";
        const string V17CombinationId =
            "skill-combination:sha256:80a6b140ecc1f8fbb34490423c1d0772bc2d644f0a54e937ed7a433371eb19b5";
        CharacterSkillCandidateRule rule = material?.Draft?.rules?.SingleOrDefault();
        if (rule == null
            || material.Draft.kind != CharacterSkillKind.Passive
            || rule.trigger != CharacterSkillTrigger.NeedChanged
            || !string.Equals(rule.ruleId, V17RuleId, StringComparison.Ordinal))
        {
            throw Unsupported(
                "character-skill-scenario-negative-need-changed-rule",
                "The stale v17 rejection fixture requires its exact production NeedChanged passive rule.");
        }

        string reconstructedCombinationId = "skill-combination:"
            + NarrativeInferenceHash.ComputeSha256Utf8(
                V17RuleId + "|" + V17MechanicalIdentity);
        if (!string.Equals(
                reconstructedCombinationId,
                V17CombinationId,
                StringComparison.Ordinal))
        {
            throw Unsupported(
                "character-skill-scenario-negative-v17-hash",
                "The reconstructed v17 NeedChanged cleaning/small + stock/small ID changed.");
        }
        RuleOptions currentOptions = material.RuleOptions?.SingleOrDefault();
        if (currentOptions == null
            || currentOptions.Legal.Any((option) => string.Equals(
                option.Id,
                V17CombinationId,
                StringComparison.Ordinal)))
        {
            throw Unsupported(
                "character-skill-scenario-negative-stale-combination-current",
                "Production legality still accepts the stale v17 NeedChanged cleaning/small + stock/small ID.");
        }

        ResponseCandidate[] responseCandidates = material.ResponseCandidates
            .Select((candidate) => candidate.Copy())
            .ToArray();
        int responseIndex = Array.FindIndex(responseCandidates, (candidate) =>
            string.Equals(candidate.RuleId, rule.ruleId, StringComparison.Ordinal));
        if (responseIndex < 0)
        {
            throw Unsupported(
                "character-skill-scenario-negative-response-rule",
                "The production NeedChanged response has no candidate for its authored rule.");
        }
        responseCandidates[responseIndex].CombinationId = V17CombinationId;
        return responseCandidates;
    }

    private static NarrativeMechanicScenario CaptureNegative(
        string scenarioId,
        string mutation,
        ScenarioMaterial material,
        CharacterSkillGenerationService authority,
        ResponseCandidate[] responseCandidates,
        string requiredFailureText = null)
    {
        string responseJson = BuildResponseJson(responseCandidates);
        bool accepted = authority.TryValidateResponse(
            material.Draft,
            responseJson,
            out List<CharacterSkillInstance> validatedSkills,
            out string validationError);
        if (accepted || string.IsNullOrWhiteSpace(validationError))
        {
            throw Unsupported(
                "character-skill-scenario-negative-accepted",
                $"Production validator unexpectedly accepted negative case '{scenarioId}'.");
        }
        if (!string.IsNullOrEmpty(requiredFailureText)
            && validationError.IndexOf(requiredFailureText, StringComparison.Ordinal) < 0)
        {
            throw Unsupported(
                "character-skill-scenario-negative-failure-detail",
                $"Production validator rejected negative case '{scenarioId}' without identifying "
                + $"the required stale value '{requiredFailureText}'.");
        }

        NarrativeMechanicCatalogCanonicalJsonObject authorityContext =
            NarrativeMechanicCatalogCanonicalJson.Object(
                Property("actualAccepted", Boolean(false)),
                Property("actualFailureReason", String(validationError)),
                Property("expectedAccepted", Boolean(false)),
                Property("fixtureKind", String("controlled-negative-validator-case")),
                Property("mutation", String(mutation)),
                Property("producer", String(ProducerIdentifier)),
                Property("responseJson", String(responseJson)),
                Property("validatedSkillCount", Integer(validatedSkills?.Count ?? 0)),
                Property("validator", String(ValidatorIdentifier)));
        return new NarrativeMechanicScenario(
            scenarioId,
            ProfileToken,
            material.Request,
            material.FullLegalCandidates,
            material.PublicContext.TargetPersistentId,
            material.PublicContext,
            material.EffectDescriptions,
            authorityContext,
            material.FixtureInput.With("negativeMutation", String(mutation)),
            ProducerIdentifier,
            ValidatorIdentifier,
            accepted: false,
            failureReason: validationError.Trim(),
            diversityAxes: new[]
            {
                Axis("kind", material.Draft.kind.ToString()),
                Axis("negativeCase", mutation)
            });
    }

    private static void ConfigureFixture(
        CharacterProgression progression,
        ScenarioPlan plan,
        int ordinal,
        string scenarioId,
        int requestRevision)
    {
        CharacterGrowthState growth = progression.GrowthState;
        growth.EnsureCollections();
        growth.initialized = true;
        growth.autoChooseDrafts = false;
        growth.potentialGrade = plan.Potential;
        // This remains deterministic fixture state, but CreateDraft's production
        // request key is varied through requestRevision below.
        growth.generationSeed = plan.Ordinal;
        growth.origin = $"통제 출신 {ordinal:D2}";
        growth.displayName = "리안";
        growth.nextActiveDraftHasPity = plan.Pity;
        growth.skillGenerationRevision = requestRevision;
        growth.activeSkills.Clear();
        growth.passiveSkills.Clear();
        growth.ultimate = null;
        growth.drafts.Clear();
        growth.pendingRequestKeys.Clear();

        CharacterNarrativeLedger ledger = progression.NarrativeLedger;
        ledger.facts.Clear();
        if (plan.LedgerEvents.Length > 0)
        {
            for (int eventIndex = 0; eventIndex < plan.LedgerEvents.Length; eventIndex++)
            {
                LedgerEventPlan ledgerEvent = plan.LedgerEvents[eventIndex];
                for (int repetition = 0; repetition < ledgerEvent.Repetitions; repetition++)
                {
                    ledger.Record(
                        ledgerEvent.Domain,
                        BuildLedgerEventFactId(scenarioId, eventIndex, ledgerEvent),
                        BuildLedgerEventSubjectId(scenarioId, ledgerEvent),
                        ledgerEvent.Outcome,
                        value: ledgerEvent.Value,
                        day: 200 + ordinal + ledgerEvent.DayOffset + repetition);
                }
            }
            return;
        }
        for (int repetition = 0; repetition < plan.LedgerRepetitions; repetition++)
        {
            ledger.Record(
                plan.LedgerDomain,
                $"fixture:{scenarioId}:{plan.LedgerDomain.ToString().ToLowerInvariant()}",
                $"fixture-subject:{scenarioId}",
                CharacterActivityOutcomes.Completed,
                value: 1f,
                day: 100 + ordinal);
        }
    }

    private static IReadOnlyList<NarrativeLedgerPublicationDescriptor>
        BuildEvaluationLedgerPublications(
            ScenarioPlan plan,
            string scenarioId,
            CharacterActor actor)
    {
        if (plan == null
            || plan.EvaluationMetadata == null
            || plan.LedgerEvents == null
            || plan.LedgerEvents.Length == 0)
        {
            throw Unsupported(
                "character-skill-evaluation-publication-plan-missing",
                $"Evaluation scenario '{scenarioId ?? "unknown"}' has no ledger publication plan.");
        }
        if (actor?.Identity == null
            || string.IsNullOrWhiteSpace(actor.Identity.PersistentId)
            || string.IsNullOrWhiteSpace(actor.Identity.DisplayName))
        {
            throw Unsupported(
                "character-skill-evaluation-publication-actor-missing",
                $"Evaluation scenario '{scenarioId}' has no public actor entity for ledger publications.");
        }

        List<NarrativeLedgerPublicationDescriptor> descriptors =
            new(plan.LedgerEvents.Length);
        for (int eventIndex = 0; eventIndex < plan.LedgerEvents.Length; eventIndex++)
        {
            LedgerEventPlan ledgerEvent = plan.LedgerEvents[eventIndex];
            LedgerEventPublicationPlan publication = ledgerEvent?.Publication;
            if (publication == null)
            {
                throw Unsupported(
                    "character-skill-evaluation-publication-missing",
                    $"Evaluation scenario '{scenarioId}' event {eventIndex + 1} has no public publication.");
            }

            if (!string.Equals(
                    publication.TargetSubjectToken,
                    ledgerEvent.SubjectToken,
                    StringComparison.Ordinal))
            {
                throw Unsupported(
                    "character-skill-evaluation-publication-target-mismatch",
                    $"Evaluation scenario '{scenarioId}' event {eventIndex + 1} publication target "
                    + $"'{publication.TargetSubjectToken}' does not match ledger subject "
                    + $"'{ledgerEvent.SubjectToken}'.");
            }
            string sourceSubjectId = BuildLedgerEventSubjectId(scenarioId, ledgerEvent);
            string targetName = string.Equals(
                publication.TargetSubjectToken,
                "self",
                StringComparison.Ordinal)
                ? actor.Identity.DisplayName
                : publication.TargetEntityName;
            descriptors.Add(new NarrativeLedgerPublicationDescriptor(
                ledgerEvent.Domain,
                BuildLedgerEventFactId(scenarioId, eventIndex, ledgerEvent),
                sourceSubjectId,
                publication.Sentence,
                publication.EventType,
                new NarrativePublicEntityInput(
                    sourceSubjectId,
                    publication.TargetEntityKind,
                    targetName)));
        }

        return descriptors;
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

    private static bool MatchesPlan(CharacterSkillDraft draft, ScenarioPlan plan)
    {
        int expectedCount = plan.Kind == CharacterSkillKind.Active ? 3 : 1;
        if (draft == null
            || draft.kind != plan.Kind
            || draft.unlockLevel != plan.UnlockLevel
            || draft.rules == null
            || draft.rules.Count != expectedCount
            || draft.rules.Any((rule) => rule == null))
        {
            return false;
        }
        // The controlled source records the actual production rule. It must not
        // search thousands of revisions to force a pre-authored trigger/target,
        // because that would bias the offered-module distribution.
        return plan.Kind != CharacterSkillKind.Ultimate
            || draft.rules.All((rule) =>
                rule.ultimateDomain == CharacterUltimateDomain.Offense);
    }

    private static bool TryBuildRuleOptions(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        out List<RuleOptions> ruleOptions)
    {
        ruleOptions = null;
        if (draft?.rules == null
            || draft.nextPresentationIndex < 0
            || draft.nextPresentationIndex >= draft.rules.Count
            || draft.nextPresentationIndex >= (draft.moduleSelectionOffers?.Count ?? 0))
        {
            return false;
        }
        CharacterSkillCandidateRule pendingRule = draft.rules[draft.nextPresentationIndex];
        CharacterSkillModuleOfferState offer =
            draft.moduleSelectionOffers[draft.nextPresentationIndex];
        List<CharacterSkillAllowedCombination> legal = offer.positiveModuleIds
            .Where(moduleId => !string.IsNullOrWhiteSpace(moduleId)
                && settings.FindModule(moduleId) != null)
            .Select(moduleId => new CharacterSkillAllowedCombination
            {
                Id = moduleId,
                RuleId = pendingRule.ruleId,
                Cost = 0,
                MechanicalIdentity = moduleId,
                Modules = new List<CharacterSkillModuleSelection>
                {
                    new CharacterSkillModuleSelection
                    {
                        moduleId = moduleId,
                        variantId = string.Empty
                    }
                }
            })
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToList();
        if (legal.Count == 0) return false;
        ruleOptions = new List<RuleOptions>
        {
            new RuleOptions(pendingRule, legal, legal[0])
        };
        return true;
    }

    private static bool TrySelectDistinctMechanicalOptions(
        IReadOnlyList<List<CharacterSkillAllowedCombination>> legalByRule,
        int ruleIndex,
        ISet<string> selectedMechanicalIdentities,
        CharacterSkillAllowedCombination[] selectedByRule)
    {
        if (ruleIndex >= legalByRule.Count)
        {
            return true;
        }

        foreach (CharacterSkillAllowedCombination option in legalByRule[ruleIndex])
        {
            if (option == null
                || string.IsNullOrWhiteSpace(option.MechanicalIdentity)
                || !selectedMechanicalIdentities.Add(option.MechanicalIdentity))
            {
                continue;
            }

            selectedByRule[ruleIndex] = option;
            if (TrySelectDistinctMechanicalOptions(
                    legalByRule,
                    ruleIndex + 1,
                    selectedMechanicalIdentities,
                    selectedByRule))
            {
                return true;
            }

            selectedByRule[ruleIndex] = null;
            selectedMechanicalIdentities.Remove(option.MechanicalIdentity);
        }

        return false;
    }

    private static void RequireValidatedSelections(
        string scenarioId,
        IReadOnlyList<RuleOptions> ruleOptions,
        IReadOnlyList<CharacterSkillInstance> skills)
    {
        if (skills == null || skills.Count != ruleOptions.Count)
        {
            throw Unsupported(
                "character-skill-scenario-validation-count",
                $"Scenario '{scenarioId}' validator returned {skills?.Count ?? 0} skills "
                + $"for {ruleOptions.Count} authored rules.");
        }
        for (int index = 0; index < ruleOptions.Count; index++)
        {
            if (!string.Equals(
                    skills[index].ruleId,
                    ruleOptions[index].Rule.ruleId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    skills[index].combinationId,
                    ruleOptions[index].Selected.Id,
                    StringComparison.Ordinal))
            {
                throw Unsupported(
                    "character-skill-scenario-validation-selection",
                    $"Scenario '{scenarioId}' validator output at index {index} does not "
                    + "match the response's authored rule and legal combination.");
            }
        }
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject ToRequestJson(
        CharacterProgression progression,
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        IEnumerable<RuleOptions> ruleOptions) => ToRequestJson(
        draft,
        settings,
        ruleOptions,
        CharacterSkillPromptBuilder.Build(progression, draft, settings));

    private static NarrativeMechanicCatalogCanonicalJsonObject ToRequestJson(
        CharacterSkillDraft draft,
        CharacterSkillSystemSettingsSO settings,
        IEnumerable<RuleOptions> ruleOptions,
        string publicPrompt)
    {
        if (string.IsNullOrWhiteSpace(publicPrompt))
        {
            throw Unsupported(
                "character-skill-evaluation-public-prompt-missing",
                "CharacterSkill evaluation request construction requires its production public prompt.");
        }
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("candidateCount", Integer(1)),
            Property("candidatePacket", String(
                CharacterSkillPromptBuilder.BuildModuleSelectionPacket(draft, settings))),
            Property("kind", String(draft.kind.ToString())),
            Property("prompt", String(publicPrompt)),
            Property("requestKey", String(draft.requestKey)),
            Property("requestedUltimateDomain", String(
                draft.requestedUltimateDomain.ToString())),
            Property("rules", new NarrativeMechanicCatalogCanonicalJsonArray(
                ruleOptions.Select((options) => ToRuleJson(options, settings, draft.kind)))),
            Property("unlockLevel", Integer(draft.unlockLevel)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue ToRuleJson(
        RuleOptions options,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind)
    {
        CharacterSkillCandidateRule rule = options.Rule;
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("allowedModuleIds", StringArray(rule.allowedModuleIds
                .OrderBy((value) => value, StringComparer.Ordinal))),
            Property("allowedVariantIds", StringArray(rule.allowedVariantIds
                .OrderBy((value) => value, StringComparer.Ordinal))),
            Property("budget", Integer(rule.budget)),
            Property("moduleOptions", new NarrativeMechanicCatalogCanonicalJsonArray(
                options.Legal.Select((option) => ToModuleOfferJson(option.Id, settings)))),
            Property("cooldownTurns", Integer(rule.cooldownTurns)),
            Property("mechanicalPolicySource", String(
                rule.mechanicalPolicySource.ToString())),
            Property("rarity", String(rule.rarity.ToString())),
            Property("ruleId", String(rule.ruleId)),
            Property("target", String(rule.target.ToString())),
            Property("targetPositions", String(
                CharacterSkillFormationRules.Format(rule.targetPositions))),
            Property("trigger", String(rule.trigger.ToString())),
            Property("ultimateDomain", String(rule.ultimateDomain.ToString())),
            Property("usableFrom", String(
                CharacterSkillFormationRules.Format(rule.usableFrom))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonArray ToFullLegalCandidatesJson(
        IEnumerable<RuleOptions> ruleOptions,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind)
    {
        return new NarrativeMechanicCatalogCanonicalJsonArray(ruleOptions.Select((options) =>
            NarrativeMechanicCatalogCanonicalJson.Object(
                Property("options", new NarrativeMechanicCatalogCanonicalJsonArray(
                    options.Legal.Select((option) => ToModuleOfferJson(option.Id, settings)))),
                Property("ruleId", String(options.Rule.ruleId)))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue ToModuleOfferJson(
        string moduleId,
        CharacterSkillSystemSettingsSO settings)
    {
        CharacterSkillModuleRule module = settings.FindModule(moduleId)
            ?? throw Unsupported("character-skill-scenario-module-missing",
                $"Offered module '{moduleId}' is absent from the authored catalog.");
        NarrativeFormulaCapabilityDescriptor descriptor = settings.RequireFormulaDescriptor(module);
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("capabilityId", String(descriptor.CapabilityId)),
            Property("moduleDisplayName", String(module.displayName)),
            Property("moduleId", String(module.id)),
            Property("polarity", String(NarrativeFormulaModulePolarity.Positive.ToString())));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue ToCombinationJson(
        CharacterSkillAllowedCombination option,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillSystemSettingsSO settings)
    {
        CharacterSkillCombinationSemanticsDto semantics =
            CharacterSkillCombinationSemanticsFactory.Create(option, rule, kind, settings);
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("combinationId", String(option.Id)),
            Property("cost", Integer(option.Cost)),
            Property("mechanicalIdentity", String(option.MechanicalIdentity)),
            Property("modules", new NarrativeMechanicCatalogCanonicalJsonArray(
                option.Modules.Select((selection) => ToSelectionJson(selection, settings)))),
            Property("ruleId", String(option.RuleId)),
            Property("semantics", ToSemanticsJson(semantics)),
            Property("signature", String(option.Signature)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue ToSemanticsJson(
        CharacterSkillCombinationSemanticsDto semantics)
    {
        NarrativeMechanicCatalogCanonicalJsonObject result =
            NarrativeMechanicCatalogCanonicalJson.Object(
                Property("combinationId", String(semantics.combinationId)),
                Property("descriptionKo", String(semantics.descriptionKo)),
                Property("modules", new NarrativeMechanicCatalogCanonicalJsonArray(
                    semantics.modules.Select(module =>
                        (NarrativeMechanicCatalogCanonicalJsonValue)
                        NarrativeMechanicCatalogCanonicalJson.Object(
                            Property("descriptionKo", String(module.descriptionKo)),
                            Property("effectKind", String(module.effectKind)),
                            Property("executionScope", String(module.executionScope)),
                            Property("moduleId", String(module.moduleId)),
                            Property("terms", StringArray(module.terms)),
                            Property("variantId", String(module.variantId)))))),
                Property("schemaVersion", Integer(semantics.schemaVersion)));
        string authority = CharacterSkillCombinationSemanticsFactory
            .SerializeCanonical(semantics);
        if (!string.Equals(result.ToCanonicalString(), authority, StringComparison.Ordinal))
        {
            throw Unsupported(
                "character-skill-scenario-semantics-serialization-divergence",
                "Scenario canonical JSON diverged from the production CharacterSkill semantics serializer.");
        }
        return result;
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue ToSelectionJson(
        CharacterSkillModuleSelection selection,
        CharacterSkillSystemSettingsSO settings)
    {
        CharacterSkillModuleRule module = settings.FindModule(selection.moduleId);
        CharacterSkillNumericVariant variant = module?.FindVariant(selection.variantId);
        if (module == null || variant == null)
        {
            throw Unsupported(
                "character-skill-scenario-mechanic-missing",
                $"Legal combination references missing module/variant "
                + $"'{selection?.moduleId ?? string.Empty}|{selection?.variantId ?? string.Empty}'.");
        }
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("capabilityId", String(CharacterSkillModuleCapabilityRegistry
                .Require(module)
                .CapabilityId)),
            Property("count", Integer(variant.count)),
            Property("cost", Integer(variant.cost)),
            Property("duration", Integer(variant.duration)),
            Property("moduleClass", String(module.GetType().Name)),
            Property("moduleDisplayName", String(module.displayName)),
            Property("moduleId", String(module.id)),
            Property("primaryValueDecimal", String(Decimal(variant.primaryValue))),
            Property("secondaryValueDecimal", String(Decimal(variant.secondaryValue))),
            Property("variantDisplayName", String(variant.displayName)),
            Property("variantId", String(variant.id)));
    }

    private static string[] BuildEffectDescriptions(
        IEnumerable<RuleOptions> ruleOptions,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind)
    {
        SortedSet<string> descriptions = new SortedSet<string>(StringComparer.Ordinal);
        foreach (RuleOptions options in ruleOptions)
        {
            foreach (CharacterSkillAllowedCombination option in options.Legal)
            {
                CharacterSkillModuleRule module = settings.FindModule(option.Id);
                if (module == null) continue;
                NarrativeFormulaCapabilityDescriptor descriptor =
                    settings.RequireFormulaDescriptor(module);
                descriptions.Add(string.Join(" | ", module.id,
                    descriptor.CapabilityId, module.displayName));
            }
        }
        return descriptions.ToArray();
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildAuthorityContext(
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillDraft draft,
        string responseJson,
        bool accepted,
        string validationError,
        IEnumerable<CharacterSkillInstance> validatedSkills,
        IEnumerable<RuleOptions> ruleOptions)
    {
        string settingsPath = RequireAssetPath(settings);
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("actualAccepted", Boolean(accepted)),
            Property("actualFailureReason", String(validationError)),
            Property("expectedAccepted", Boolean(true)),
            Property("fixtureKind", String("controlled-not-natural-play")),
            Property("producer", String(ProducerIdentifier)),
            Property("responseJson", String(responseJson)),
            Property("selectedCombinations", new NarrativeMechanicCatalogCanonicalJsonArray(
                ruleOptions.Select((options) => NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("combinationId", String(options.Selected.Id)),
                    Property("mechanicalIdentity", String(
                        options.Selected.MechanicalIdentity)),
                    Property("ruleId", String(options.Rule.ruleId)))))),
            Property("settingsAssetPath", String(settingsPath)),
            Property("validatedSkills", new NarrativeMechanicCatalogCanonicalJsonArray(
                (validatedSkills ?? Array.Empty<CharacterSkillInstance>())
                .Select(ToValidatedSkillJson))),
            Property("validator", String(ValidatorIdentifier)),
            Property("validatorCandidateCount", Integer(draft.rules.Count)));
    }

    private static NarrativeMechanicCatalogCanonicalJsonValue ToValidatedSkillJson(
        CharacterSkillInstance skill)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("combinationId", String(skill.combinationId)),
            Property("cooldownTurns", Integer(skill.cooldownTurns)),
            Property("description", String(skill.description)),
            Property("displayName", String(skill.displayName)),
            Property("kind", String(skill.kind.ToString())),
            Property("modules", new NarrativeMechanicCatalogCanonicalJsonArray(
                skill.modules.Select((selection) => NarrativeMechanicCatalogCanonicalJson.Object(
                    Property("moduleId", String(selection.moduleId)),
                    Property("variantId", String(selection.variantId)))))),
            Property("narrativeReason", String(skill.narrativeReason)),
            Property("rarity", String(skill.rarity.ToString())),
            Property("requestKey", String(skill.requestKey)),
            Property("ruleId", String(skill.ruleId)),
            Property("target", String(skill.target.ToString())),
            Property("targetPositions", String(
                CharacterSkillFormationRules.Format(skill.targetPositions))),
            Property("trigger", String(skill.trigger.ToString())),
            Property("ultimateDomain", String(skill.ultimateDomain.ToString())),
            Property("usableFrom", String(
                CharacterSkillFormationRules.Format(skill.usableFrom))));
    }

    private static NarrativeMechanicCatalogCanonicalJsonObject BuildFixtureInput(
        ScenarioPlan plan,
        CharacterNarrativeLedger ledger,
        int resolvedRevision)
    {
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("displayName", String("리안")),
            Property("expectedTarget", String(FormatOptional(plan.ExpectedTarget))),
            Property("expectedTrigger", String(FormatOptional(plan.ExpectedTrigger))),
            Property("fixtureKind", String("controlled")),
            Property("fixtureGenerationSeed", Integer(plan.Ordinal)),
            Property("ledgerRecordCalls", new NarrativeMechanicCatalogCanonicalJsonArray(
                ledger.Facts.OrderBy((fact) => fact.factId, StringComparer.Ordinal)
                    .Select((fact) => NarrativeMechanicCatalogCanonicalJson.Object(
                        Property("day", Integer(fact.lastDay)),
                        Property("domain", String(fact.domain.ToString())),
                        Property("factId", String(fact.factId)),
                        Property("outcome", String(fact.outcome)),
                        Property("repetitions", Integer(fact.count)),
                        Property("subjectId", String(fact.subjectId)),
                        Property("valueDecimalPerRecord", String("1")))))),
            Property("origin", String($"통제 출신 {plan.Ordinal:D2}")),
            Property("pity", Boolean(plan.Pity)),
            Property("potential", String(plan.Potential.ToString())),
            Property("initialRevision", Integer(plan.Revision)),
            Property("requestedKind", String(plan.Kind.ToString())),
            Property("requestRevision", Integer(resolvedRevision)),
            Property("revisionSearchOffset", Integer(resolvedRevision - plan.Revision)),
            Property("unlockLevel", Integer(plan.UnlockLevel)));
    }

    private static string BuildResponseJson(IEnumerable<ResponseCandidate> candidates)
    {
        ResponseCandidate candidate = (candidates ?? Array.Empty<ResponseCandidate>())
            .SingleOrDefault() ?? throw Unsupported(
                "character-skill-scenario-response-count",
                "Module-selection response requires exactly one pending selection.");
        return NarrativeMechanicCatalogCanonicalJson.Object(
            Property("displayName", String(candidate.DisplayName)),
            Property("drawbackModuleIds", NarrativeMechanicCatalogCanonicalJson.Array()),
            Property("evidenceFactIds", NarrativeMechanicCatalogCanonicalJson.Array(
                String(candidate.EvidenceFactId))),
            Property("narrativeFlavor", String(candidate.NarrativeReason)),
            Property("positiveModuleIds", candidate.IncludePositiveModule
                ? NarrativeMechanicCatalogCanonicalJson.Array(String(candidate.CombinationId))
                : NarrativeMechanicCatalogCanonicalJson.Array()),
            Property("selectionId", String(candidate.RuleId))).ToCanonicalString();
    }

    private CharacterSkillSystemSettingsSO ResolveSettings()
    {
        if (suppliedSettings != null)
        {
            return suppliedSettings;
        }
        string[] paths = AssetDatabase.FindAssets("t:CharacterSkillSystemSettingsSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where((path) => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy((path) => path, StringComparer.Ordinal)
            .ToArray();
        if (paths.Length != 1)
        {
            throw Unsupported(
                "character-skill-scenario-settings-count",
                $"Expected exactly one live CharacterSkillSystemSettingsSO asset; found {paths.Length}.");
        }
        CharacterSkillSystemSettingsSO settings =
            AssetDatabase.LoadAssetAtPath<CharacterSkillSystemSettingsSO>(paths[0]);
        if (settings == null)
        {
            throw Unsupported(
                "character-skill-scenario-settings-load",
                $"Could not load live CharacterSkillSystemSettingsSO at '{paths[0]}'.");
        }
        return settings;
    }

    private static void ValidateAuthoredAuthority(CharacterSkillSystemSettingsSO settings)
    {
        IReadOnlyList<CharacterSkillModuleRule> authoredModules = settings.Modules;
        CharacterSkillModuleRule[] modules = authoredModules?
            .Where((module) => module != null)
            .ToArray() ?? Array.Empty<CharacterSkillModuleRule>();
        if (modules.Length == 0 || modules.Length != (authoredModules?.Count ?? 0))
        {
            throw Unsupported(
                "character-skill-scenario-settings-invalid",
                "Live CharacterSkillSystemSettingsSO must contain only non-null authored modules.");
        }
        string duplicateModuleId = modules
            .Where((module) => !string.IsNullOrWhiteSpace(module.id))
            .GroupBy((module) => module.id.Trim(), StringComparer.Ordinal)
            .FirstOrDefault((group) => group.Count() > 1)?.Key;
        if (modules.Any((module) => string.IsNullOrWhiteSpace(module.id)
                || module.variants == null
                || module.variants.Count == 0
                || module.variants.Any((variant) => variant == null
                    || string.IsNullOrWhiteSpace(variant.id)
                    || variant.cost <= 0))
            || !string.IsNullOrEmpty(duplicateModuleId))
        {
            throw Unsupported(
                "character-skill-scenario-settings-invalid",
                "Live CharacterSkillSystemSettingsSO has a blank/duplicate module ID or an invalid numeric variant.");
        }
        int ultimateModules = modules.Count((module) =>
            module.allowedKinds != null
            && module.allowedKinds.Contains(CharacterSkillKind.Ultimate));
        if (ultimateModules == 0)
        {
            throw Unsupported(
                "character-skill-scenario-ultimate-candidates-empty",
                $"Live CharacterSkillSystemSettingsSO '{RequireAssetPath(settings)}' contains "
                + "zero modules that allow CharacterSkillKind.Ultimate; production "
                + "CharacterSkillGenerationService.CreateDraft cannot produce the required "
                + "5 Ultimate positive scenarios.");
        }
    }

    private static IEnumerable<string> CaptureRelevantPaths(
        CharacterSkillSystemSettingsSO settings)
    {
        string settingsPath = RequireAssetPath(settings);
        return new[]
        {
            SourcePath,
            "Assets/Scripts/Services/Character/AI/Editor/NarrativeMechanicScenarioContracts.cs",
            "Assets/Scripts/Services/Character/AI/NarrativeRequestContext.cs",
            "Assets/Scripts/Services/Character/Core/CharacterGrowthRules.cs",
            "Assets/Scripts/Services/Character/Core/CharacterProgression.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillCombinationSemantics.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs",
            "Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs",
            "Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs",
            settingsPath,
            settingsPath + ".meta"
        }.Distinct(StringComparer.Ordinal).OrderBy((path) => path, StringComparer.Ordinal);
    }

    private static string RequireAssetPath(UnityEngine.Object asset)
    {
        string path = AssetDatabase.GetAssetPath(asset)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(path))
        {
            throw Unsupported(
                "character-skill-scenario-settings-path",
                "The injected CharacterSkillSystemSettingsSO is not a live authored asset.");
        }
        return path;
    }

    private static void RequirePlanCount(
        IEnumerable<ScenarioPlan> plans,
        CharacterSkillKind kind,
        int expected)
    {
        int actual = plans.Count((plan) => plan.Kind == kind);
        if (actual != expected)
        {
            throw Unsupported(
                "character-skill-scenario-plan-count",
                $"CharacterSkill requires exactly {expected} {kind} fixtures; found {actual}.");
        }
    }

    private void ValidateScenarioSetKind()
    {
        if (scenarioSetKind != NarrativeMechanicScenarioSetKind.Calibration
            && scenarioSetKind != NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation)
        {
            throw Unsupported(
                "character-skill-scenario-set-kind",
                $"CharacterSkill does not support scenario set '{scenarioSetKind}'.");
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
                "character-skill-evaluation-metadata",
                $"Evaluation scenario '{missing?.Slug ?? "unknown"}' requires metadata, a multi-event ledger, and a publication for every ledger event.");
        }
    }

    private bool IsUnseenNamingEvaluation =>
        scenarioSetKind == NarrativeMechanicScenarioSetKind.UnseenNamingEvaluation;

    private static ScenarioPlan[] BuildUnseenNamingEvaluationPlans()
    {
        return new[]
        {
            EvaluationPlan(1, "unseen-eval-v1-active-scout-repair", CharacterSkillKind.Active,
                1, 101, CharacterPotentialGrade.Ordinary, false,
                CharacterNarrativeDomain.Combat, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Enemy, "character-skill-unseen-eval-v1-01",
                "character-skill-authority-01-active-l01-ordinary-enemy",
                "event sequence changes from one combat fact to scout, injury, and repair; domain combination expands Combat to Combat+Injury+Work; outcome adds Damaged before Completed.",
                Event(CharacterNarrativeDomain.Combat, "scout", "raider", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian scouted a raider near the outer gate.", "combat-scouting", "raider", NarrativePublicEntityKind.Character, "Raider scout")),
                Event(CharacterNarrativeDomain.Injury, "shield-hit", "raider", CharacterActivityOutcomes.Damaged,
                    publication: Publication("Rian was struck by a raider's shield while holding the line.", "combat-shield-injury", "raider", NarrativePublicEntityKind.Character, "Raider scout")),
                Event(CharacterNarrativeDomain.Work, "field-repair", "gate", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian repaired the field gate after the skirmish.", "field-repair", "gate", NarrativePublicEntityKind.Facility, "Field gate"))),
            EvaluationPlan(2, "unseen-eval-v1-active-work-evacuation", CharacterSkillKind.Active,
                1, 102, CharacterPotentialGrade.Promising, false,
                CharacterNarrativeDomain.Work, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Self, "character-skill-unseen-eval-v1-02",
                "character-skill-authority-02-active-l01-promising-self",
                "event sequence changes from one work fact to assembly, alarm, and evacuation; domain combination adds FacilityUse and Invasion; outcome changes from a single Completed result to Started, Blocked, and Returned.",
                Event(CharacterNarrativeDomain.Work, "assemble", "workbench", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian assembled emergency gear at the workbench.", "emergency-assembly", "workbench", NarrativePublicEntityKind.Facility, "Workbench")),
                Event(CharacterNarrativeDomain.FacilityUse, "alarm", "watch-post", CharacterActivityOutcomes.Started,
                    publication: Publication("Rian raised the alarm at the watch post.", "watch-alarm", "watch-post", NarrativePublicEntityKind.Facility, "Watch post")),
                Event(CharacterNarrativeDomain.Invasion, "evacuate", "shelter", CharacterActivityOutcomes.Returned,
                    publication: Publication("Rian escorted people to the shelter during the evacuation.", "shelter-evacuation", "shelter", NarrativePublicEntityKind.Facility, "Shelter"))),
            EvaluationPlan(3, "unseen-eval-v1-active-bond-counter", CharacterSkillKind.Active,
                1, 103, CharacterPotentialGrade.Excellent, false,
                CharacterNarrativeDomain.Relationship, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Ally, "character-skill-unseen-eval-v1-03",
                "character-skill-authority-03-active-l01-excellent-ally",
                "event sequence changes from one relationship fact to a plea, counterattack, and reconciliation; domain combination adds Combat; outcome adds Failed then Completed instead of one successful fact.",
                Event(CharacterNarrativeDomain.Relationship, "plea", "ally", CharacterActivityOutcomes.Changed,
                    publication: Publication("Rian answered an ally's plea for help.", "ally-plea", "ally", NarrativePublicEntityKind.Character, "Ally")),
                Event(CharacterNarrativeDomain.Combat, "counter", "raider", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian counterattacked a raider.", "counterattack", "raider", NarrativePublicEntityKind.Character, "Raider")),
                Event(CharacterNarrativeDomain.Relationship, "reconcile", "ally", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian reconciled with an ally after the fight.", "ally-reconciliation", "ally", NarrativePublicEntityKind.Character, "Ally"))),
            EvaluationPlan(4, "unseen-eval-v1-active-breach-response", CharacterSkillKind.Active,
                5, 104, CharacterPotentialGrade.Exceptional, false,
                CharacterNarrativeDomain.Invasion, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Enemy, "character-skill-unseen-eval-v1-04",
                "character-skill-authority-04-active-l05-exceptional-enemy",
                "event sequence changes from one invasion fact to breach warning, defense, and medical recovery; domain combination adds Combat and Injury; outcome includes Started and Damaged before Completed.",
                Event(CharacterNarrativeDomain.Invasion, "breach-warning", "outer-wall", CharacterActivityOutcomes.Started,
                    publication: Publication("Rian reported a breach warning at the outer wall.", "breach-warning", "outer-wall", NarrativePublicEntityKind.Facility, "Outer wall")),
                Event(CharacterNarrativeDomain.Combat, "hold-line", "intruder", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian held the line against an intruder.", "line-defense", "intruder", NarrativePublicEntityKind.Character, "Intruder")),
                Event(CharacterNarrativeDomain.Injury, "stitch", "ally", CharacterActivityOutcomes.Damaged,
                    publication: Publication("Rian stitched an ally's wound after the defense.", "field-triage", "ally", NarrativePublicEntityKind.Character, "Ally"))),
            EvaluationPlan(5, "unseen-eval-v1-active-hunger-crossing", CharacterSkillKind.Active,
                5, 105, CharacterPotentialGrade.Genius, false,
                CharacterNarrativeDomain.Survival, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Self, "character-skill-unseen-eval-v1-05",
                "character-skill-authority-05-active-l05-genius-self",
                "event sequence changes from one survival fact to rationing, a failed crossing, and return; domain combination adds Expedition; outcome adds Failed and Returned instead of only Completed.",
                Event(CharacterNarrativeDomain.Survival, "ration", "party", CharacterActivityOutcomes.Changed,
                    publication: Publication("Rian rationed supplies for the expedition party.", "rationing", "party", NarrativePublicEntityKind.Group, "Expedition party")),
                Event(CharacterNarrativeDomain.Expedition, "crossing", "ravine", CharacterActivityOutcomes.Failed,
                    publication: Publication("Rian failed to cross the ravine safely.", "ravine-crossing", "ravine", NarrativePublicEntityKind.Place, "Ravine")),
                Event(CharacterNarrativeDomain.Survival, "camp-return", "party", CharacterActivityOutcomes.Returned,
                    publication: Publication("Rian returned the expedition party to camp.", "camp-return", "party", NarrativePublicEntityKind.Group, "Expedition party"))),
            EvaluationPlan(6, "unseen-eval-v1-active-triage-rally", CharacterSkillKind.Active,
                5, 106, CharacterPotentialGrade.Ordinary, true,
                CharacterNarrativeDomain.Injury, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Ally, "character-skill-unseen-eval-v1-06",
                "character-skill-authority-06-active-l05-ordinary-pity-ally",
                "event sequence changes from one injury fact to triage, rally, and guarded retreat; domain combination adds Mood and Invasion; outcome adds Damaged, Changed, and Returned.",
                Event(CharacterNarrativeDomain.Injury, "triage", "ally", CharacterActivityOutcomes.Damaged,
                    publication: Publication("Rian treated an ally during field triage.", "field-triage", "ally", NarrativePublicEntityKind.Character, "Ally")),
                Event(CharacterNarrativeDomain.Mood, "rally", "ally", CharacterActivityOutcomes.Changed,
                    publication: Publication("Rian rallied an ally's morale before the retreat.", "morale-rally", "ally", NarrativePublicEntityKind.Character, "Ally")),
                Event(CharacterNarrativeDomain.Invasion, "retreat", "gate", CharacterActivityOutcomes.Returned,
                    publication: Publication("Rian led a guarded retreat through the gate.", "guarded-retreat", "gate", NarrativePublicEntityKind.Facility, "Gate"))),
            EvaluationPlan(7, "unseen-eval-v1-active-expedition-ambush", CharacterSkillKind.Active,
                30, 107, CharacterPotentialGrade.Genius, false,
                CharacterNarrativeDomain.Expedition, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Enemy, "character-skill-unseen-eval-v1-07",
                "character-skill-authority-07-active-l30-genius-enemy",
                "event sequence changes from one expedition fact to a route survey, ambush, and extraction; domain combination adds Combat and Relationship; outcome includes Blocked before Completed.",
                Event(CharacterNarrativeDomain.Expedition, "route-survey", "ruin", CharacterActivityOutcomes.Progress,
                    publication: Publication("Rian surveyed a route through the ruin.", "route-survey", "ruin", NarrativePublicEntityKind.Place, "Ruin")),
                Event(CharacterNarrativeDomain.Combat, "ambush", "raider", CharacterActivityOutcomes.Blocked,
                    publication: Publication("A raider ambush blocked Rian's route.", "raider-ambush", "raider", NarrativePublicEntityKind.Character, "Raider")),
                Event(CharacterNarrativeDomain.Relationship, "extraction", "ally", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian completed an ally's extraction from the ruin.", "ally-extraction", "ally", NarrativePublicEntityKind.Character, "Ally"))),
            EvaluationPlan(8, "unseen-eval-v1-active-morale-siege", CharacterSkillKind.Active,
                30, 108, CharacterPotentialGrade.Exceptional, true,
                CharacterNarrativeDomain.Mood, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Ally, "character-skill-unseen-eval-v1-08",
                "character-skill-authority-08-active-l30-exceptional-pity-ally",
                "event sequence changes from one mood fact to fear, watch duty, and a siege response; domain combination adds Work and Invasion; outcome changes through Changed, Started, and Completed.",
                Event(CharacterNarrativeDomain.Mood, "fear", "ally", CharacterActivityOutcomes.Changed,
                    publication: Publication("Rian steadied an ally shaken by the siege.", "siege-fear", "ally", NarrativePublicEntityKind.Character, "Ally")),
                Event(CharacterNarrativeDomain.Work, "watch-duty", "tower", CharacterActivityOutcomes.Started,
                    publication: Publication("Rian began watch duty at the tower.", "watch-duty", "tower", NarrativePublicEntityKind.Facility, "Tower")),
                Event(CharacterNarrativeDomain.Invasion, "siege-response", "gate", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian completed the gate's siege response.", "siege-response", "gate", NarrativePublicEntityKind.Facility, "Gate"))),

            EvaluationPlan(9, "unseen-eval-v1-passive-forge-loop", CharacterSkillKind.Passive,
                1, 109, CharacterPotentialGrade.Ordinary, false,
                CharacterNarrativeDomain.Work, CharacterSkillTrigger.WorkStarted,
                CharacterSkillTarget.Self, "character-skill-unseen-eval-v1-09",
                "character-skill-authority-09-passive-l01-work-start",
                "event sequence changes from one work fact to forge setup, blocked delivery, and resumed completion; domain combination adds FacilityUse; outcome includes Started, Blocked, and Completed.",
                Event(CharacterNarrativeDomain.Work, "forge-setup", "anvil", CharacterActivityOutcomes.Started,
                    publication: Publication("Rian began setting up the forge at the anvil.", "forge-setup", "anvil", NarrativePublicEntityKind.Facility, "Anvil")),
                Event(CharacterNarrativeDomain.FacilityUse, "delivery-block", "store", CharacterActivityOutcomes.Blocked,
                    publication: Publication("A blocked delivery delayed Rian at the store.", "delivery-block", "store", NarrativePublicEntityKind.Facility, "Store")),
                Event(CharacterNarrativeDomain.Work, "forge-resume", "anvil", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian resumed the forge work at the anvil.", "forge-resume", "anvil", NarrativePublicEntityKind.Facility, "Anvil"))),
            EvaluationPlan(10, "unseen-eval-v1-passive-facility-shift", CharacterSkillKind.Passive,
                1, 110, CharacterPotentialGrade.Promising, false,
                CharacterNarrativeDomain.FacilityUse, CharacterSkillTrigger.WorkCompleted,
                CharacterSkillTarget.Self, "character-skill-unseen-eval-v1-10",
                "character-skill-authority-10-passive-l01-facility-complete",
                "event sequence changes from one facility fact to calibration, repeated overload, and repair; domain combination adds Work and Injury; outcome adds repeated Failed and Damaged before Completed.",
                Event(CharacterNarrativeDomain.FacilityUse, "calibrate", "research-bench", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian calibrated the research bench.", "bench-calibration", "research-bench", NarrativePublicEntityKind.Facility, "Research bench")),
                Event(CharacterNarrativeDomain.Work, "overload", "research-bench", CharacterActivityOutcomes.Failed, repetitions: 3,
                    publication: Publication("The research bench repeatedly overloaded during Rian's work.", "bench-overload", "research-bench", NarrativePublicEntityKind.Facility, "Research bench")),
                Event(CharacterNarrativeDomain.Injury, "repair-burn", "worker", CharacterActivityOutcomes.Damaged,
                    publication: Publication("A worker suffered a repair burn after the overload.", "repair-burn", "worker", NarrativePublicEntityKind.Character, "Worker"))),
            EvaluationPlan(11, "unseen-eval-v1-passive-need-watch", CharacterSkillKind.Passive,
                1, 111, CharacterPotentialGrade.Excellent, false,
                CharacterNarrativeDomain.Need, CharacterSkillTrigger.NeedChanged,
                CharacterSkillTarget.Self, "character-skill-unseen-eval-v1-11",
                "character-skill-authority-11-passive-l01-need",
                "event sequence changes from one need fact to a repeated hunger warning, water recovery, and night watch; domain combination adds Survival and Work; outcome adds repeated Changed before Completed and Started.",
                Event(CharacterNarrativeDomain.Need, "hunger-warning", "self", CharacterActivityOutcomes.Changed, repetitions: 3,
                    publication: Publication("Rian noticed a persistent hunger warning.", "hunger-warning", "self", NarrativePublicEntityKind.Character, "Rian")),
                Event(CharacterNarrativeDomain.Survival, "water-recovery", "spring", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian recovered drinking water from the spring.", "water-recovery", "spring", NarrativePublicEntityKind.Place, "Spring")),
                Event(CharacterNarrativeDomain.Work, "night-watch", "gate", CharacterActivityOutcomes.Started,
                    publication: Publication("Rian began a night watch at the gate.", "night-watch", "gate", NarrativePublicEntityKind.Facility, "Gate"))),
            EvaluationPlan(12, "unseen-eval-v1-passive-mood-bond", CharacterSkillKind.Passive,
                1, 112, CharacterPotentialGrade.Exceptional, false,
                CharacterNarrativeDomain.Mood, CharacterSkillTrigger.MoodChanged,
                CharacterSkillTarget.Self, "character-skill-unseen-eval-v1-12",
                "character-skill-authority-12-passive-l01-mood",
                "event sequence changes from one mood fact to repeated despair, a companion response, and recovery work; domain combination adds Relationship and Work; outcome includes repeated Changed, Responded, and Completed.",
                Event(CharacterNarrativeDomain.Mood, "despair", "self", CharacterActivityOutcomes.Changed, repetitions: 3,
                    publication: Publication("Rian endured a period of despair.", "despair", "self", NarrativePublicEntityKind.Character, "Rian")),
                Event(CharacterNarrativeDomain.Relationship, "companion-response", "ally", CharacterActivityOutcomes.Responded,
                    publication: Publication("An ally responded to Rian's request for support.", "companion-support", "ally", NarrativePublicEntityKind.Character, "Ally")),
                Event(CharacterNarrativeDomain.Work, "recovery-work", "garden", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian completed recovery work in the garden.", "garden-recovery-work", "garden", NarrativePublicEntityKind.Facility, "Garden"))),
            EvaluationPlan(13, "unseen-eval-v1-passive-relationship-supply", CharacterSkillKind.Passive,
                25, 113, CharacterPotentialGrade.Genius, false,
                CharacterNarrativeDomain.Relationship, CharacterSkillTrigger.RelationshipChanged,
                CharacterSkillTarget.Self, "character-skill-unseen-eval-v1-13",
                "character-skill-authority-13-passive-l25-relationship",
                "event sequence changes from one relationship fact to a repeated dispute, shared supply run, and reconciliation; domain combination adds Expedition and Survival; outcome adds repeated Failed then Returned and Completed.",
                Event(CharacterNarrativeDomain.Relationship, "dispute", "ally", CharacterActivityOutcomes.Failed, repetitions: 3,
                    publication: Publication("Rian's dispute with an ally remained unresolved.", "ally-dispute", "ally", NarrativePublicEntityKind.Character, "Ally")),
                Event(CharacterNarrativeDomain.Expedition, "supply-run", "camp", CharacterActivityOutcomes.Returned,
                    publication: Publication("Rian returned to camp from a supply run.", "supply-run", "camp", NarrativePublicEntityKind.Place, "Camp")),
                Event(CharacterNarrativeDomain.Survival, "shared-ration", "ally", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian shared a ration with an ally.", "shared-ration", "ally", NarrativePublicEntityKind.Character, "Ally"))),
            EvaluationPlan(14, "unseen-eval-v1-passive-invasion-relief", CharacterSkillKind.Passive,
                25, 114, CharacterPotentialGrade.Promising, false,
                CharacterNarrativeDomain.Invasion, CharacterSkillTrigger.InvasionStarted,
                CharacterSkillTarget.Self, "character-skill-unseen-eval-v1-14",
                "character-skill-authority-14-passive-l25-invasion",
                "event sequence changes from one invasion fact to a repeated warning, barricade, and relief escort; domain combination adds Work and Relationship; outcome includes repeated Started, Completed, and Returned.",
                Event(CharacterNarrativeDomain.Invasion, "warning", "watch", CharacterActivityOutcomes.Started, repetitions: 3,
                    publication: Publication("A watch officer repeatedly warned Rian about the invasion.", "invasion-warning", "watch", NarrativePublicEntityKind.Character, "Watch officer")),
                Event(CharacterNarrativeDomain.Work, "barricade", "gate", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian completed a barricade at the gate.", "gate-barricade", "gate", NarrativePublicEntityKind.Facility, "Gate")),
                Event(CharacterNarrativeDomain.Relationship, "relief-escort", "ally", CharacterActivityOutcomes.Returned,
                    publication: Publication("Rian escorted an ally during relief duty.", "relief-escort", "ally", NarrativePublicEntityKind.Character, "Ally"))),
            EvaluationPlan(15, "unseen-eval-v1-passive-injury-return", CharacterSkillKind.Passive,
                25, 115, CharacterPotentialGrade.Excellent, false,
                CharacterNarrativeDomain.Injury, CharacterSkillTrigger.DamageTaken,
                CharacterSkillTarget.Self, "character-skill-unseen-eval-v1-15",
                "character-skill-authority-15-passive-l25-injury",
                "event sequence changes from one injury fact to repeated impact, treatment, and duty return; domain combination adds FacilityUse and Work; outcome changes through repeated Damaged, Completed, and Returned.",
                Event(CharacterNarrativeDomain.Injury, "impact", "self", CharacterActivityOutcomes.Damaged, repetitions: 3,
                    publication: Publication("Rian suffered a repeated impact injury.", "impact-injury", "self", NarrativePublicEntityKind.Character, "Rian")),
                Event(CharacterNarrativeDomain.FacilityUse, "treatment", "clinic", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian completed treatment at the clinic.", "clinic-treatment", "clinic", NarrativePublicEntityKind.Facility, "Clinic")),
                Event(CharacterNarrativeDomain.Work, "duty-return", "gate", CharacterActivityOutcomes.Returned,
                    publication: Publication("Rian returned to gate duty after treatment.", "duty-return", "gate", NarrativePublicEntityKind.Facility, "Gate"))),

            EvaluationPlan(16, "unseen-eval-v1-ultimate-scout-breach", CharacterSkillKind.Ultimate,
                50, 116, CharacterPotentialGrade.Ordinary, false,
                CharacterNarrativeDomain.Combat, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Enemy, "character-skill-unseen-eval-v1-16",
                "character-skill-authority-16-ultimate-offense-a",
                "event sequence changes from one combat fact to scout contact, breach defense, and extraction; domain combination adds Invasion and Expedition; outcome includes Observed, Completed, and Returned.",
                Event(CharacterNarrativeDomain.Combat, "scout-contact", "raider", CharacterActivityOutcomes.Observed,
                    publication: Publication("Rian observed a raider during a scouting contact.", "scout-contact", "raider", NarrativePublicEntityKind.Character, "Raider")),
                Event(CharacterNarrativeDomain.Invasion, "breach-defense", "gate", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian completed the gate's breach defense.", "breach-defense", "gate", NarrativePublicEntityKind.Facility, "Gate")),
                Event(CharacterNarrativeDomain.Expedition, "extract", "ruin", CharacterActivityOutcomes.Returned,
                    publication: Publication("Rian returned from an extraction at the ruin.", "ruin-extraction", "ruin", NarrativePublicEntityKind.Place, "Ruin"))),
            EvaluationPlan(17, "unseen-eval-v1-ultimate-siege-triage", CharacterSkillKind.Ultimate,
                50, 117, CharacterPotentialGrade.Promising, false,
                CharacterNarrativeDomain.Invasion, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Enemy, "character-skill-unseen-eval-v1-17",
                "character-skill-authority-17-ultimate-offense-b",
                "event sequence changes from one invasion fact to a siege signal, triage, and countercharge; domain combination adds Injury and Combat; outcome includes Started, Damaged, and Completed.",
                Event(CharacterNarrativeDomain.Invasion, "siege-signal", "wall", CharacterActivityOutcomes.Started,
                    publication: Publication("Rian received a siege signal from the wall.", "siege-signal", "wall", NarrativePublicEntityKind.Facility, "Wall")),
                Event(CharacterNarrativeDomain.Injury, "triage", "ally", CharacterActivityOutcomes.Damaged,
                    publication: Publication("Rian treated an ally in siege triage.", "siege-triage", "ally", NarrativePublicEntityKind.Character, "Ally")),
                Event(CharacterNarrativeDomain.Combat, "countercharge", "intruder", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian completed a countercharge against an intruder.", "countercharge", "intruder", NarrativePublicEntityKind.Character, "Intruder"))),
            EvaluationPlan(18, "unseen-eval-v1-ultimate-expedition-ration", CharacterSkillKind.Ultimate,
                50, 118, CharacterPotentialGrade.Excellent, false,
                CharacterNarrativeDomain.Expedition, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Enemy, "character-skill-unseen-eval-v1-18",
                "character-skill-authority-18-ultimate-offense-c",
                "event sequence changes from one expedition fact to a stalled route, ration choice, and ambush; domain combination adds Survival and Combat; outcome includes Blocked, Changed, and Completed.",
                Event(CharacterNarrativeDomain.Expedition, "stalled-route", "ravine", CharacterActivityOutcomes.Blocked,
                    publication: Publication("Rian's route stalled at the ravine.", "stalled-route", "ravine", NarrativePublicEntityKind.Place, "Ravine")),
                Event(CharacterNarrativeDomain.Survival, "ration-choice", "party", CharacterActivityOutcomes.Changed,
                    publication: Publication("Rian changed the ration plan for the expedition party.", "ration-choice", "party", NarrativePublicEntityKind.Group, "Expedition party")),
                Event(CharacterNarrativeDomain.Combat, "ambush", "raider", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian overcame a raider ambush.", "raider-ambush", "raider", NarrativePublicEntityKind.Character, "Raider"))),
            EvaluationPlan(19, "unseen-eval-v1-ultimate-injury-rally", CharacterSkillKind.Ultimate,
                50, 119, CharacterPotentialGrade.Exceptional, false,
                CharacterNarrativeDomain.Injury, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Enemy, "character-skill-unseen-eval-v1-19",
                "character-skill-authority-19-ultimate-offense-d",
                "event sequence changes from one injury fact to collapse, morale rally, and final defense; domain combination adds Mood and Invasion; outcome includes Damaged, Changed, and Completed.",
                Event(CharacterNarrativeDomain.Injury, "collapse", "ally", CharacterActivityOutcomes.Damaged,
                    publication: Publication("An ally collapsed after being injured.", "injury-collapse", "ally", NarrativePublicEntityKind.Character, "Ally")),
                Event(CharacterNarrativeDomain.Mood, "morale-rally", "party", CharacterActivityOutcomes.Changed,
                    publication: Publication("Rian rallied the expedition party's morale.", "morale-rally", "party", NarrativePublicEntityKind.Group, "Expedition party")),
                Event(CharacterNarrativeDomain.Invasion, "final-defense", "gate", CharacterActivityOutcomes.Completed,
                    publication: Publication("Rian completed the final defense at the gate.", "final-defense", "gate", NarrativePublicEntityKind.Facility, "Gate"))),
            EvaluationPlan(20, "unseen-eval-v1-ultimate-survival-return", CharacterSkillKind.Ultimate,
                50, 120, CharacterPotentialGrade.Genius, false,
                CharacterNarrativeDomain.Survival, CharacterSkillTrigger.ManualCombat,
                CharacterSkillTarget.Enemy, "character-skill-unseen-eval-v1-20",
                "character-skill-authority-20-ultimate-offense-e",
                "event sequence changes from one survival fact to a storm camp, lost route, and defended return; domain combination adds Expedition and Combat; outcome includes Started, Failed, and Returned.",
                Event(CharacterNarrativeDomain.Survival, "storm-camp", "party", CharacterActivityOutcomes.Started,
                    publication: Publication("Rian set a storm camp for the expedition party.", "storm-camp", "party", NarrativePublicEntityKind.Group, "Expedition party")),
                Event(CharacterNarrativeDomain.Expedition, "lost-route", "trail", CharacterActivityOutcomes.Failed,
                    publication: Publication("Rian lost the trail during the storm.", "lost-route", "trail", NarrativePublicEntityKind.Place, "Trail")),
                Event(CharacterNarrativeDomain.Combat, "defended-return", "raider", CharacterActivityOutcomes.Returned,
                    publication: Publication("Rian defended the return against a raider.", "defended-return", "raider", NarrativePublicEntityKind.Character, "Raider")))
        };
    }

    private static ScenarioPlan[] BuildPlans()
    {
        return new[]
        {
            Plan(1, "active-l01-ordinary-enemy", CharacterSkillKind.Active, 1, 0,
                CharacterPotentialGrade.Ordinary, false, CharacterNarrativeDomain.Combat,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Enemy, 1),
            Plan(2, "active-l01-promising-self", CharacterSkillKind.Active, 1, 1,
                CharacterPotentialGrade.Promising, false, CharacterNarrativeDomain.Work,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Self, 3),
            Plan(3, "active-l01-excellent-ally", CharacterSkillKind.Active, 1, 2,
                CharacterPotentialGrade.Excellent, false, CharacterNarrativeDomain.Relationship,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Ally, 8),
            Plan(4, "active-l05-exceptional-enemy", CharacterSkillKind.Active, 5, 3,
                CharacterPotentialGrade.Exceptional, false, CharacterNarrativeDomain.Invasion,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Enemy, 3),
            Plan(5, "active-l05-genius-self", CharacterSkillKind.Active, 5, 4,
                CharacterPotentialGrade.Genius, false, CharacterNarrativeDomain.Survival,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Self, 1),
            Plan(6, "active-l05-ordinary-pity-ally", CharacterSkillKind.Active, 5, 5,
                CharacterPotentialGrade.Ordinary, true, CharacterNarrativeDomain.Injury,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Ally, 3),
            Plan(7, "active-l30-genius-enemy", CharacterSkillKind.Active, 30, 6,
                CharacterPotentialGrade.Genius, false, CharacterNarrativeDomain.Expedition,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Enemy, 8),
            Plan(8, "active-l30-exceptional-pity-ally", CharacterSkillKind.Active, 30, 7,
                CharacterPotentialGrade.Exceptional, true, CharacterNarrativeDomain.Mood,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Ally, 1),

            Plan(9, "passive-l01-work-start", CharacterSkillKind.Passive, 1, 8,
                CharacterPotentialGrade.Ordinary, false, CharacterNarrativeDomain.Work,
                CharacterSkillTrigger.WorkStarted, CharacterSkillTarget.Self, 3),
            Plan(10, "passive-l01-facility-complete", CharacterSkillKind.Passive, 1, 9,
                CharacterPotentialGrade.Promising, false, CharacterNarrativeDomain.FacilityUse,
                CharacterSkillTrigger.WorkCompleted, CharacterSkillTarget.Self, 8),
            Plan(11, "passive-l01-need", CharacterSkillKind.Passive, 1, 10,
                CharacterPotentialGrade.Excellent, false, CharacterNarrativeDomain.Need,
                CharacterSkillTrigger.NeedChanged, CharacterSkillTarget.Self, 3),
            Plan(12, "passive-l01-mood", CharacterSkillKind.Passive, 1, 11,
                CharacterPotentialGrade.Exceptional, false, CharacterNarrativeDomain.Mood,
                CharacterSkillTrigger.MoodChanged, CharacterSkillTarget.Self, 8),
            Plan(13, "passive-l25-relationship", CharacterSkillKind.Passive, 25, 12,
                CharacterPotentialGrade.Genius, false, CharacterNarrativeDomain.Relationship,
                CharacterSkillTrigger.RelationshipChanged, CharacterSkillTarget.Self, 3),
            Plan(14, "passive-l25-invasion", CharacterSkillKind.Passive, 25, 13,
                CharacterPotentialGrade.Promising, false, CharacterNarrativeDomain.Invasion,
                CharacterSkillTrigger.InvasionStarted, CharacterSkillTarget.Self, 8),
            Plan(15, "passive-l25-injury", CharacterSkillKind.Passive, 25, 14,
                CharacterPotentialGrade.Excellent, false, CharacterNarrativeDomain.Injury,
                CharacterSkillTrigger.DamageTaken, CharacterSkillTarget.Self, 3),

            Plan(16, "ultimate-offense-a", CharacterSkillKind.Ultimate, 50, 15,
                CharacterPotentialGrade.Ordinary, false, CharacterNarrativeDomain.Combat,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Enemy, 1),
            Plan(17, "ultimate-offense-b", CharacterSkillKind.Ultimate, 50, 16,
                CharacterPotentialGrade.Promising, false, CharacterNarrativeDomain.Invasion,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Enemy, 3),
            Plan(18, "ultimate-offense-c", CharacterSkillKind.Ultimate, 50, 17,
                CharacterPotentialGrade.Excellent, false, CharacterNarrativeDomain.Expedition,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Enemy, 8),
            Plan(19, "ultimate-offense-d", CharacterSkillKind.Ultimate, 50, 18,
                CharacterPotentialGrade.Exceptional, false, CharacterNarrativeDomain.Injury,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Enemy, 3),
            Plan(20, "ultimate-offense-e", CharacterSkillKind.Ultimate, 50, 19,
                CharacterPotentialGrade.Genius, false, CharacterNarrativeDomain.Survival,
                CharacterSkillTrigger.ManualCombat, CharacterSkillTarget.Enemy, 8)
        };
    }

    private static ScenarioPlan Plan(
        int ordinal,
        string slug,
        CharacterSkillKind kind,
        int unlockLevel,
        int revision,
        CharacterPotentialGrade potential,
        bool pity,
        CharacterNarrativeDomain ledgerDomain,
        CharacterSkillTrigger expectedTrigger,
        CharacterSkillTarget expectedTarget,
        int ledgerRepetitions) => new ScenarioPlan(
        ordinal,
        slug,
        kind,
        unlockLevel,
        revision,
        potential,
        pity,
        ledgerDomain,
        expectedTrigger,
        expectedTarget,
        ledgerRepetitions,
        Array.Empty<LedgerEventPlan>(),
        null);

    private static ScenarioPlan EvaluationPlan(
        int ordinal,
        string slug,
        CharacterSkillKind kind,
        int unlockLevel,
        int revision,
        CharacterPotentialGrade potential,
        bool pity,
        CharacterNarrativeDomain ledgerDomain,
        CharacterSkillTrigger expectedTrigger,
        CharacterSkillTarget expectedTarget,
        string familyId,
        string nearestCalibrationScenarioId,
        string reason,
        params LedgerEventPlan[] ledgerEvents) => new ScenarioPlan(
        ordinal,
        slug,
        kind,
        unlockLevel,
        revision,
        potential,
        pity,
        ledgerDomain,
        expectedTrigger,
        expectedTarget,
        0,
        ledgerEvents,
        new NarrativeMechanicScenarioEvaluationMetadata(
            familyId,
            "unseen-evaluation",
            nearestCalibrationScenarioId,
            "distinct-context",
            reason));

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
        string sentence,
        string eventType,
        string targetSubjectToken,
        NarrativePublicEntityKind targetEntityKind,
        string targetEntityName) => new LedgerEventPublicationPlan(
        sentence,
        eventType,
        targetSubjectToken,
        targetEntityKind,
        targetEntityName);

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
                "character-skill-scenario-envelope-invalid",
                $"Scenario '{scenarioId}' did not receive its production public-context envelope.");
        }
    }

    private static string Decimal(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string FormatOptional<T>(T? value) where T : struct =>
        value.HasValue ? value.Value.ToString() : "Any";

    private static NarrativeMechanicCatalogCanonicalJsonArray StringArray(
        IEnumerable<string> values) => new NarrativeMechanicCatalogCanonicalJsonArray(
        (values ?? Array.Empty<string>()).Select((value) =>
            (NarrativeMechanicCatalogCanonicalJsonValue)String(value)));

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

    private static string ProfileToken => NarrativeMechanicScenarioProfiles.CharacterSkill;

    private sealed class FixedSettingsProvider : ICharacterSkillSystemSettingsProvider
    {
        public FixedSettingsProvider(CharacterSkillSystemSettingsSO settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public CharacterSkillSystemSettingsSO Settings { get; }
    }

    private sealed class UnavailableRuntimeProvider : ILocalLlmRuntimeProvider
    {
        public bool TryGetRuntime(out ILocalLlmRuntime runtime)
        {
            runtime = null;
            return false;
        }

        public ILocalLlmRuntime GetRequiredRuntime() => throw new InvalidOperationException(
            "CharacterSkill scenario capture validates deterministic responses and never invokes a local LLM runtime.");
    }

    private sealed class FixedUiClock : IUiClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
    }

    private sealed class ScenarioPlan
    {
        public ScenarioPlan(
            int ordinal,
            string slug,
            CharacterSkillKind kind,
            int unlockLevel,
            int revision,
            CharacterPotentialGrade potential,
            bool pity,
            CharacterNarrativeDomain ledgerDomain,
            CharacterSkillTrigger expectedTrigger,
            CharacterSkillTarget expectedTarget,
            int ledgerRepetitions,
            LedgerEventPlan[] ledgerEvents,
            NarrativeMechanicScenarioEvaluationMetadata evaluationMetadata)
        {
            Ordinal = ordinal;
            Slug = slug;
            Kind = kind;
            UnlockLevel = unlockLevel;
            Revision = revision;
            Potential = potential;
            Pity = pity;
            LedgerDomain = ledgerDomain;
            ExpectedTrigger = expectedTrigger;
            ExpectedTarget = expectedTarget;
            LedgerRepetitions = ledgerRepetitions;
            LedgerEvents = ledgerEvents ?? Array.Empty<LedgerEventPlan>();
            EvaluationMetadata = evaluationMetadata;
        }

        public int Ordinal { get; }
        public string Slug { get; }
        public CharacterSkillKind Kind { get; }
        public int UnlockLevel { get; }
        public int Revision { get; }
        public CharacterPotentialGrade Potential { get; }
        public bool Pity { get; }
        public CharacterNarrativeDomain LedgerDomain { get; }
        public CharacterSkillTrigger? ExpectedTrigger { get; }
        public CharacterSkillTarget? ExpectedTarget { get; }
        public int LedgerRepetitions { get; }
        public LedgerEventPlan[] LedgerEvents { get; }
        public NarrativeMechanicScenarioEvaluationMetadata EvaluationMetadata { get; }
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
            string targetSubjectToken,
            NarrativePublicEntityKind targetEntityKind,
            string targetEntityName)
        {
            if (string.IsNullOrWhiteSpace(sentence)
                || string.IsNullOrWhiteSpace(eventType)
                || string.IsNullOrWhiteSpace(targetSubjectToken)
                || string.IsNullOrWhiteSpace(targetEntityName)
                || eventType.IndexOf("fixture", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new ArgumentException(
                    "Narrative ledger publications require a readable sentence, semantic event type, and non-fixture public target.");
            }

            Sentence = sentence;
            EventType = eventType;
            TargetSubjectToken = targetSubjectToken;
            TargetEntityKind = targetEntityKind;
            TargetEntityName = targetEntityName;
        }

        public string Sentence { get; }
        public string EventType { get; }
        public string TargetSubjectToken { get; }
        public NarrativePublicEntityKind TargetEntityKind { get; }
        public string TargetEntityName { get; }
    }

    private sealed class RuleOptions
    {
        public RuleOptions(
            CharacterSkillCandidateRule rule,
            List<CharacterSkillAllowedCombination> legal,
            CharacterSkillAllowedCombination selected)
        {
            Rule = rule;
            Legal = legal;
            Selected = selected;
        }

        public CharacterSkillCandidateRule Rule { get; }
        public List<CharacterSkillAllowedCombination> Legal { get; }
        public CharacterSkillAllowedCombination Selected { get; }
    }

    private sealed class ResponseCandidate
    {
        public ResponseCandidate(
            string ruleId,
            string combinationId,
            string evidenceFactId,
            string displayName,
            string description,
            string narrativeReason)
        {
            RuleId = ruleId;
            CombinationId = combinationId;
            EvidenceFactId = evidenceFactId;
            DisplayName = displayName;
            Description = description;
            NarrativeReason = narrativeReason;
        }

        public string RuleId { get; set; }
        public string CombinationId { get; set; }
        public string EvidenceFactId { get; set; }
        public bool IncludePositiveModule { get; set; } = true;
        public string DisplayName { get; }
        public string Description { get; }
        public string NarrativeReason { get; }

        public ResponseCandidate Copy() => new ResponseCandidate(
            RuleId,
            CombinationId,
            EvidenceFactId,
            DisplayName,
            Description,
            NarrativeReason)
        {
            IncludePositiveModule = IncludePositiveModule
        };
    }

    private sealed class ScenarioMaterial
    {
        public ScenarioMaterial(
            NarrativeMechanicScenario scenario,
            CharacterSkillDraft draft,
            NarrativeMechanicCatalogCanonicalJsonObject request,
            NarrativeMechanicCatalogCanonicalJsonArray fullLegalCandidates,
            NarrativeMechanicScenarioPublicContext publicContext,
            string[] effectDescriptions,
            NarrativeMechanicCatalogCanonicalJsonObject fixtureInput,
            List<RuleOptions> ruleOptions,
            ResponseCandidate[] responseCandidates)
        {
            Scenario = scenario;
            Draft = draft;
            Request = request;
            FullLegalCandidates = fullLegalCandidates;
            PublicContext = publicContext;
            EffectDescriptions = effectDescriptions;
            FixtureInput = fixtureInput;
            RuleOptions = ruleOptions;
            ResponseCandidates = responseCandidates;
        }

        public NarrativeMechanicScenario Scenario { get; }
        public CharacterSkillDraft Draft { get; }
        public NarrativeMechanicCatalogCanonicalJsonObject Request { get; }
        public NarrativeMechanicCatalogCanonicalJsonArray FullLegalCandidates { get; }
        public NarrativeMechanicScenarioPublicContext PublicContext { get; }
        public string[] EffectDescriptions { get; }
        public NarrativeMechanicCatalogCanonicalJsonObject FixtureInput { get; }
        public List<RuleOptions> RuleOptions { get; }
        public ResponseCandidate[] ResponseCandidates { get; }
    }

    private sealed class ControlledCharacterSkillFixture : IDisposable
    {
        private readonly CharacterSO data;

        public ControlledCharacterSkillFixture(string scenarioId, int ordinal)
        {
            Root = CharacterAiPlanDebugFixtures.CreateActorObject(
                $"NarrativeMechanicCharacterSkill_{ordinal:D2}_{scenarioId}");
            Actor = Root.GetComponent<CharacterActor>();
            if (Actor == null)
            {
                throw Unsupported(
                    "character-skill-scenario-actor-create",
                    $"Scenario '{scenarioId}' fixture did not create a CharacterActor.");
            }
            data = CharacterAiPlanDebugFixtures.CreateCharacterData(
                CharacterType.NPC,
                "리안",
                "human");
            Actor.EnsureRuntimeState();
            Actor.Identity.SetPersistentId($"character:continuity:skill:{ordinal:D2}");
            Actor.Initialization(data);
            Progression = Actor.Progression;
            if (Progression == null)
            {
                throw Unsupported(
                    "character-skill-scenario-progression-create",
                    $"Scenario '{scenarioId}' fixture did not create a CharacterProgression.");
            }
        }

        public GameObject Root { get; }
        public CharacterActor Actor { get; }
        public CharacterProgression Progression { get; }

        public void Dispose()
        {
            if (Root != null)
            {
                UnityEngine.Object.DestroyImmediate(Root);
            }
            if (data != null)
            {
                UnityEngine.Object.DestroyImmediate(data);
            }
        }
    }
}
#endif
