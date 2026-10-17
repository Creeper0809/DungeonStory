#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Regression coverage for future facility-generation eligibility. It does not
/// rewrite assets or assert a new balance value; committed nodes are explicitly
/// checked through the unchanged projection path.
/// </summary>
public static class FacilityEvolutionSameAxisEligibilityDebugScenarios
{
    [MenuItem("DungeonStory/V27/Facility/Validate Formula Same-Axis Eligibility")]
    public static void Validate()
    {
        VerifyDerivedSameAxisPredicate();
        VerifyTargetConsumerPolicy();
        VerifyFreshOffersAndPendingTamperFailClosed();
        VerifyFourEvidenceLowCreditPairIsNotExposed();
        VerifyPhase77TargetCapabilitiesAndSources();
        VerifyCommittedSameAxisNodeRetainsProjection();
        VerifyReadyV3NodeWithHistoricalOffersRestoresExactly();
        Debug.Log("[FacilityEvolutionSameAxisEligibility] focused scenarios passed.");
    }

    private static void VerifyDerivedSameAxisPredicate()
    {
        EvolutionModuleDefinition sameAxis = Module(
            "facility:qa-same-axis",
            Benefit("service.speed", 1.1f),
            Burden("service.speed", 0.95f));
        EvolutionModuleDefinition distinctAxis = Module(
            "facility:qa-distinct-axis",
            Benefit("service.speed", 1.1f),
            Burden("work.output", 0.95f));

        Require(sameAxis.HasInternallySelfCancellingPositiveAxis
                && !distinctAxis.HasInternallySelfCancellingPositiveAxis,
            "Same-axis eligibility did not distinguish overlapping benefit/burden stats.");
    }

    private static void VerifyTargetConsumerPolicy()
    {
        BuildingSO guardTarget = Target(
            "qa-guard-target",
            FacilityRole.Security,
            true,
            BuiltInWorkTypeIds.Operate,
            BuiltInWorkTypeIds.Guard);
        BuildingSO guardOnlyProductionTarget = Target(
            "qa-guard-only-production-target",
            FacilityRole.Security,
            true,
            BuiltInWorkTypeIds.Guard);
        BuildingSO noProductionTarget = Target(
            "qa-guard-without-production-target",
            FacilityRole.Security,
            false,
            BuiltInWorkTypeIds.Guard);
        try
        {
            EvolutionModuleRegistry registry = new();
            bool hasDefense = registry.TryGet("facility:defense", out EvolutionModuleDefinition defense);
            bool hasRoomSynergy = registry.TryGet("facility:room-synergy",
                out EvolutionModuleDefinition roomSynergy);
            Require(hasDefense && hasRoomSynergy,
                "Facility candidate modules are missing from the registry.");
            Require(FacilityEvolutionModifierApplicability.IsStatReachable(
                        guardTarget, "defense.output", out _)
                    && FacilityEvolutionModifierApplicability.IsStatReachable(
                        guardTarget, "work.output", out _)
                    && !FacilityEvolutionModifierApplicability.IsStatReachable(
                        noProductionTarget, "defense.output", out string noProductionReason)
                    && noProductionReason.Contains(
                        "production ability", StringComparison.Ordinal)
                    && !FacilityEvolutionModifierApplicability.IsStatReachable(
                        guardOnlyProductionTarget,
                        "defense.output",
                        out string guardOnlyReason)
                    && guardOnlyReason.Contains(
                        "operate or research", StringComparison.Ordinal)
                    && !FacilityEvolutionModifierApplicability.IsStatReachable(
                        guardTarget, "service.speed", out string serviceReason)
                    && serviceReason.Contains("role/work", StringComparison.Ordinal)
                    && FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            guardTarget, defense, out _)
                    && !FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            guardTarget, roomSynergy, out string roomSynergyReason)
                    && roomSynergyReason.Contains(
                        "self-cancelling", StringComparison.Ordinal),
                "Target stat applicability did not require a reachable matching consumer.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(guardTarget);
            UnityEngine.Object.DestroyImmediate(noProductionTarget);
            UnityEngine.Object.DestroyImmediate(guardOnlyProductionTarget);
        }
    }

    private static void VerifyFreshOffersAndPendingTamperFailClosed()
    {
        BuildingSO target = Target(
            "qa-formula-target",
            FacilityRole.Security | FacilityRole.Meal,
            true,
            BuiltInWorkTypeIds.Operate,
            BuiltInWorkTypeIds.Guard);
        BuildingSO outputUnavailableTarget = Target(
            "qa-formula-output-unavailable",
            FacilityRole.Security,
            false,
            BuiltInWorkTypeIds.Guard);
        BuildingSO optionalUnavailableTarget = Target(
            "qa-formula-optional-unavailable",
            FacilityRole.Meal,
            true,
            BuiltInWorkTypeIds.Operate);
        FacilityEvolutionRecipeSO mixed = Recipe(
            "qa-formula-mixed",
            target,
            "facility:defense",
            "facility:room-synergy",
            "facility:service");
        FacilityEvolutionRecipeSO selfCancellingOnly = Recipe(
            "qa-formula-self-cancelling-only",
            target,
            "facility:room-synergy");
        FacilityEvolutionRecipeSO outputUnavailable = Recipe(
            "qa-formula-output-unavailable",
            outputUnavailableTarget,
            "facility:defense");
        FacilityEvolutionRecipeSO optionalUnavailable = Recipe(
            "qa-formula-optional-unavailable",
            optionalUnavailableTarget,
            "facility:service");
        FacilityEvolutionRecipeSO resolvableOptional = Recipe(
            "qa-formula-resolvable-optional",
            target,
            "facility:defense");
        // The positive costs two units; its optional burden can legally grant
        // two units of local credit, so this is a real resolvable pair rather
        // than a mere structural offer.
        resolvableOptional.formulaCapabilities[0].baseCost = 2;
        try
        {
            FacilityEvolutionState state = State("building:qa-formula-target");
            Require(FacilityFormulaEvolutionAuthority.TryPrepare(
                        state, mixed,
                        out FacilityEvolutionFormulaPresentationPendingSnapshot pending,
                        out string preparationFailure)
                    && pending?.node != null,
                "Fresh facility formula preparation failed: " + preparationFailure);

            NarrativeFormulaModuleSelectionRequest request =
                FacilityFormulaEvolutionAuthority.BuildModuleSelectionRequest(
                    pending.node, mixed);
            string[] positiveIds = request.Offers
                .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Positive)
                .Select(value => value.ModuleId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Require(positiveIds.SequenceEqual(new[] { "facility:defense", "facility:service" })
                    && !request.Offers.Any(value => string.Equals(
                        value.ModuleId,
                        "facility:room-synergy",
                        StringComparison.Ordinal))
                    && !request.Offers.Any(value => string.Equals(
                        value.ModuleId,
                        "facility:drawback-accident",
                        StringComparison.Ordinal)),
                "Fresh module offers retained an ineligible or numerically infeasible module.");

            VerifyExposedPositiveDrawbackPairsResolve(state, mixed, pending.node);

            FacilityEvolutionState resolvableState = StateWithFourEvidence(
                "building:qa-formula-resolvable-optional");
            Require(FacilityFormulaEvolutionAuthority.TryPrepare(
                        resolvableState,
                        resolvableOptional,
                        out FacilityEvolutionFormulaPresentationPendingSnapshot resolvablePending,
                        out string resolvableFailure)
                    && resolvablePending?.node != null
                    && resolvablePending.node.moduleSelectionOffers.Any(
                        offer => offer != null
                            && offer.polarity
                                == EvolutionModuleOfferPolarity.Drawback),
                "A numerically resolvable optional drawback was incorrectly omitted: "
                + resolvableFailure);
            VerifyExposedPositiveDrawbackPairsResolve(
                resolvableState,
                resolvableOptional,
                resolvablePending.node);

            string evidenceId = request.EvidenceFactIds.Single();
            Require(!NarrativeFormulaModuleSelectionValidator.TryValidate(
                        request,
                        new NarrativeFormulaModuleSelectionChoice(
                            request.SelectionId,
                            new[] { "facility:room-synergy" },
                            new[] { "facility:drawback-accident" },
                            new[] { evidenceId }),
                        out _,
                        out _),
                "An optional-drawback combination selected a module absent from the legal offer.");

            pending.node.moduleSelectionOffers.Add(new EquipmentEvolutionModuleOfferState
            {
                moduleId = "facility:room-synergy",
                polarity = EvolutionModuleOfferPolarity.Positive,
                semanticDescription = "tampered"
            });
            RequireThrows<InvalidOperationException>(
                () => FacilityFormulaEvolutionAuthority.BuildModuleSelectionRequest(
                    pending.node, mixed),
                "A tampered same-axis positive offer bypassed the pending resolution gate.");
            RequireThrows<InvalidOperationException>(
                () => FacilityFormulaEvolutionAuthority.FreezeSelectedModule(
                    state,
                    mixed,
                    pending.node,
                    new NarrativeFormulaModuleSelectionChoice(
                        request.SelectionId,
                        new[] { "facility:defense" },
                        Array.Empty<string>(),
                        new[] { evidenceId })),
                "Direct module selection bypassed the pending same-axis gate.");

            RequireThrows<InvalidOperationException>(
                () => FacilityFormulaEvolutionAuthority.BuildModuleSelectionRequest(
                    pending.node, outputUnavailable),
                "A stored output positive bypassed the target-consumer pending gate.");
            RequireThrows<InvalidOperationException>(
                () => FacilityFormulaEvolutionAuthority.FreezeSelectedModule(
                    state,
                    outputUnavailable,
                    pending.node,
                    new NarrativeFormulaModuleSelectionChoice(
                        request.SelectionId,
                        new[] { "facility:defense" },
                        Array.Empty<string>(),
                        new[] { evidenceId })),
                "Direct output selection bypassed the target-consumer gate.");

            EvolutionNode optionalTamper = pending.node.Clone();
            optionalTamper.moduleSelectionOffers = new List<EquipmentEvolutionModuleOfferState>
            {
                new()
                {
                    moduleId = "facility:drawback-defense-gap",
                    polarity = EvolutionModuleOfferPolarity.Drawback,
                    semanticDescription = "tampered unreachable optional drawback"
                }
            };
            RequireThrows<InvalidOperationException>(
                () => FacilityFormulaEvolutionAuthority.BuildModuleSelectionRequest(
                    optionalTamper, optionalUnavailable),
                "An optional drawback without a target consumer bypassed the pending gate.");
            RequireThrows<InvalidOperationException>(
                () => FacilityFormulaEvolutionAuthority.FreezeSelectedModule(
                    state,
                    optionalUnavailable,
                    optionalTamper,
                    new NarrativeFormulaModuleSelectionChoice(
                        request.SelectionId,
                        new[] { "facility:service" },
                        new[] { "facility:drawback-defense-gap" },
                        new[] { evidenceId })),
                "A direct optional-drawback selection bypassed target-consumer eligibility.");

            Require(!FacilityFormulaEvolutionAuthority.TryPrepare(
                        State("building:qa-formula-only"),
                        selfCancellingOnly,
                        out _,
                        out string selfCancellingFailure)
                    && selfCancellingFailure.Contains(
                        "self-cancelling", StringComparison.Ordinal),
                "A recipe with no eligible positive module did not fail closed.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(mixed);
            UnityEngine.Object.DestroyImmediate(selfCancellingOnly);
            UnityEngine.Object.DestroyImmediate(outputUnavailable);
            UnityEngine.Object.DestroyImmediate(optionalUnavailable);
            UnityEngine.Object.DestroyImmediate(resolvableOptional);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(outputUnavailableTarget);
            UnityEngine.Object.DestroyImmediate(optionalUnavailableTarget);
        }
    }

    private static void VerifyFourEvidenceLowCreditPairIsNotExposed()
    {
        BuildingSO researchTarget = Target(
            "qa-four-evidence-research-target",
            FacilityRole.Research,
            true,
            BuiltInWorkTypeIds.Research);
        FacilityEvolutionRecipeSO lowCreditRecipe = Recipe(
            "qa-four-evidence-low-credit",
            researchTarget,
            "facility:research");
        // This data-owned capability has a fixed zero-cost positive. The
        // four-evidence policy can grant one optional credit, but the exact
        // composition rejects a selected drawback when the positive cannot
        // consume that credit; the positive-only selection remains legal.
        lowCreditRecipe.formulaPolicy.baseBudget = 1;
        lowCreditRecipe.formulaPolicy.powerScale = 4;
        lowCreditRecipe.formulaPolicy.softCapK = 8f;
        lowCreditRecipe.formulaCapabilities[0].baseCost = 0;
        try
        {
            FacilityEvolutionState state = StateWithFourEvidence(
                "building:qa-four-evidence-low-credit");
            Require(FacilityFormulaEvolutionAuthority.TryPrepare(
                        state,
                        lowCreditRecipe,
                        out FacilityEvolutionFormulaPresentationPendingSnapshot pending,
                        out string failureReason)
                    && pending?.node != null,
                "The four-evidence low-credit fixture failed preparation: " + failureReason);
            NarrativeFormulaModuleSelectionRequest request =
                FacilityFormulaEvolutionAuthority.BuildModuleSelectionRequest(
                    pending.node, lowCreditRecipe);
            EvolutionNode allEvidenceOptional = pending.node.Clone();
            allEvidenceOptional.moduleSelectionOffers.Add(
                new EquipmentEvolutionModuleOfferState
                {
                    moduleId = "facility:drawback-maintenance",
                    polarity = EvolutionModuleOfferPolarity.Drawback,
                    semanticDescription = "누적 성능 저하: 선택 시 work.output 부담; "
                        + "부정 원장 근거가 있는 경우에만 예산 보너스; 함께 선택 금지 이로운 기능="
                        + "facility:output,facility:risky-overdrive"
                });
            NarrativeFormulaModuleSelectionRequest allEvidenceRequest =
                FacilityFormulaEvolutionAuthority.BuildModuleSelectionRequest(
                    allEvidenceOptional, lowCreditRecipe);
            NarrativeFormulaModuleSelectionChoice allEvidenceChoice = new(
                allEvidenceRequest.SelectionId,
                new[] { "facility:research" },
                new[] { "facility:drawback-maintenance" },
                allEvidenceRequest.EvidenceFactIds);
            Require(NarrativeFormulaModuleSelectionValidator.TryValidate(
                        allEvidenceRequest,
                        allEvidenceChoice,
                        out _,
                        out _),
                "The four-evidence low-credit fixture did not construct a contract-valid optional selection.");
            RequireThrowsContaining(
                () => FacilityFormulaEvolutionAuthority.FreezeSelectedModule(
                    state,
                    lowCreditRecipe,
                    allEvidenceOptional,
                    allEvidenceChoice),
                "numerically infeasible",
                "The four-evidence all-selected optional burden unexpectedly resolved.");
            Require(request.Offers.Any(value => string.Equals(
                        value.ModuleId,
                        "facility:research",
                        StringComparison.Ordinal)
                    && value.Polarity == NarrativeFormulaModulePolarity.Positive)
                    && !request.Offers.Any(value => value.Polarity
                        == NarrativeFormulaModulePolarity.Drawback),
                "A four-evidence optional burden survived despite failing the exact final freeze path.");
            EvolutionNode resolvedPositive = FacilityFormulaEvolutionAuthority.FreezeSelectedModule(
                state,
                lowCreditRecipe,
                pending.node,
                new NarrativeFormulaModuleSelectionChoice(
                    request.SelectionId,
                    new[] { "facility:research" },
                    Array.Empty<string>(),
                    request.EvidenceFactIds));
            Require(string.Equals(resolvedPositive.effectId, "facility:research",
                        StringComparison.Ordinal)
                    && string.IsNullOrEmpty(resolvedPositive.burdenEffectId),
                "The low-credit fixture removed its resolvable positive along with the optional burden.");
            VerifyExposedPositiveDrawbackPairsResolve(state, lowCreditRecipe, pending.node);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(lowCreditRecipe);
            UnityEngine.Object.DestroyImmediate(researchTarget);
        }
    }

    private static void VerifyCommittedSameAxisNodeRetainsProjection()
    {
        GameObject fixture = new("FacilityEvolutionCommittedSameAxisFixture");
        try
        {
            BuildableObject facility = fixture.AddComponent<BuildableObject>();
            FacilityEvolutionStateComponent component =
                fixture.AddComponent<FacilityEvolutionStateComponent>();
            component.ReplaceInstanceEvolution(new FacilityEvolutionState
            {
                evolutionNodes = new List<EvolutionNode>
                {
                    new()
                    {
                        nodeId = "facility:qa-committed-room-synergy",
                        effectId = "facility:room-synergy",
                        active = true,
                        formulaVersion =
                            FacilityFormulaEvolutionAuthority.DrawbackModuleSelectionFormulaVersion,
                        potencyMultiplier = 1f
                    }
                },
                activeNodeIds = new List<string>
                {
                    "facility:qa-committed-room-synergy"
                }
            });

            EvolutionModuleRegistry registry = new();
            Require(registry.TryGet(
                        "facility:room-synergy",
                        out EvolutionModuleDefinition committedModule),
                "Committed same-axis fixture module is missing from the registry.");
            float expected = committedModule.Benefits.Single().multiplier
                * committedModule.Burdens.Single().multiplier;
            FacilityEvolutionModifierQuery query = new(registry);
            float beforeClone = query.GetMultiplier(facility, "service.speed");
            component.ReplaceInstanceEvolution(component.InstanceEvolution);
            float afterClone = query.GetMultiplier(facility, "service.speed");
            Require(Mathf.Approximately(beforeClone, expected)
                    && Mathf.Approximately(afterClone, beforeClone),
                "The eligibility gate changed projection of a committed same-axis node.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    private static void VerifyPhase77TargetCapabilitiesAndSources()
    {
        BuildingSO training = Target(
            "qa-phase77-training",
            FacilityRole.Training,
            false,
            BuiltInWorkTypeIds.Operate);
        BuildingSO security = Target(
            "qa-phase77-security",
            FacilityRole.Security,
            false,
            BuiltInWorkTypeIds.Guard);
        BuildingSO support = ScriptableObject.CreateInstance<BuildingSO>();
        support.objectName = "qa-phase77-service-support";
        support.AbilityModules.Add(new BuildingServiceSupportAbility
        {
            supportId = "service:qa-phase77",
            featureTags = new[] { "service:display" },
            compatibleHubTags = new[] { "service:retail" },
            modifierType = ServiceSupportModifierType.Security,
            workSpeedMultiplier = 1f
        });
        security.AbilityModules.Add(new BuildingSecurityAbility
        {
            maxAlarmCharges = 3,
            chargesPerGuardWork = 1
        });
        FacilityEvolutionRecipeSO trainingRecipe = Recipe(
            "qa-phase77-training-recipe", training,
            "facility:training-operations");
        FacilityEvolutionRecipeSO securityRecipe = Recipe(
            "qa-phase77-security-recipe", security,
            "facility:security-operations");
        FacilityEvolutionRecipeSO supportRecipe = Recipe(
            "qa-phase77-support-recipe", support,
            "facility:service-support");
        try
        {
            EvolutionModuleRegistry registry = new();
            bool hasTraining = registry.TryGet(
                "facility:training-operations",
                out EvolutionModuleDefinition trainingModule);
            bool hasSecurity = registry.TryGet(
                "facility:security-operations",
                out EvolutionModuleDefinition securityModule);
            bool hasSupport = registry.TryGet(
                "facility:service-support",
                out EvolutionModuleDefinition supportModule);
            Require(hasTraining && hasSecurity && hasSupport,
                "Phase77 facility modules are absent from the registered authority.");
            Require(!trainingModule.HasInternallySelfCancellingPositiveAxis
                    && !securityModule.HasInternallySelfCancellingPositiveAxis
                    && !supportModule.HasInternallySelfCancellingPositiveAxis
                    && FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            training, trainingModule, out _)
                    && FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            security, securityModule, out _)
                    && FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            support, supportModule, out _)
                    && !FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            training, securityModule, out _)
                    && !FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            security, trainingModule, out _)
                    && !FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            support, trainingModule, out _),
                "Phase77 module eligibility did not stay bound to a matching real consumer.");

            FacilityEvolutionState trainingState = State(
                "building:qa-phase77-training");
            Require(FacilityFormulaEvolutionAuthority.TryPrepare(
                        trainingState,
                        trainingRecipe,
                        out FacilityEvolutionFormulaPresentationPendingSnapshot trainingPending,
                        out string trainingFailure)
                    && trainingPending?.node != null
                    && trainingPending.node.moduleSelectionOffers
                        .Where(offer => offer != null
                            && offer.polarity
                                == EvolutionModuleOfferPolarity.Positive)
                        .Select(offer => offer.moduleId)
                        .SequenceEqual(new[] { "facility:training-operations" },
                            StringComparer.Ordinal)
                    && !trainingPending.node.moduleSelectionOffers.Any(
                        offer => offer != null
                            && offer.polarity
                                == EvolutionModuleOfferPolarity.Drawback),
                "The training target retained an infeasible optional drawback instead of its authored positive offer: "
                + trainingFailure);
            VerifyExposedPositiveDrawbackPairsResolve(
                trainingState,
                trainingRecipe,
                trainingPending.node);
            trainingPending.node.moduleSelectionOffers.Add(
                new EquipmentEvolutionModuleOfferState
                {
                    moduleId = "facility:security-operations",
                    polarity = EvolutionModuleOfferPolarity.Positive,
                    semanticDescription = "tampered wrong target"
                });
            RequireThrows<InvalidOperationException>(
                () => FacilityFormulaEvolutionAuthority.BuildModuleSelectionRequest(
                    trainingPending.node,
                    trainingRecipe),
                "A Phase77 pending request accepted a positive module for a different consumer family.");
            bool securityPrepared = FacilityFormulaEvolutionAuthority.TryPrepare(
                State("building:qa-phase77-security"), securityRecipe,
                out _, out string securityFailure);
            bool supportPrepared = FacilityFormulaEvolutionAuthority.TryPrepare(
                State("building:qa-phase77-support"), supportRecipe,
                out _, out string supportFailure);
            Require(securityPrepared && supportPrepared,
                "A Phase77 security or service-support target could not prepare: "
                + securityFailure + "; " + supportFailure);

            BuildingSO trainingSource = LoadModularBuilding("T01_훈련허수아비");
            BuildingSO displaySource = LoadModularBuilding("S02_잡화진열선반");
            BuildingSO securitySource = LoadModularBuilding("G01_경비초소책상");
            BuildingSO result = LoadModularBuilding("S03_잠금진열장");
            BuildingSO archery = LoadModularBuilding("T02_사격과녁");
            BuildingSO tactical = LoadModularBuilding("G04_전술지도탁자");
            FacilityEvolutionRecipeSO[] p1 = AssetDatabase.FindAssets(
                    "t:FacilityEvolutionRecipeSO",
                    new[] { "Assets/Resources/SO/FacilityEvolution/P1" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>)
                .Where(value => value != null)
                .ToArray();
            FacilityEvolutionRecipeSO archeryRecipe = p1.Single(value =>
                value.EffectiveId == "evolve_training_dummy_to_archery_target");
            FacilityEvolutionRecipeSO displayRecipe = p1.Single(value =>
                value.EffectiveId == "evolve_shop_display_to_secure_display");
            FacilityEvolutionRecipeSO tacticalRecipe = p1.Single(value =>
                value.EffectiveId == "evolve_guard_desk_to_tactical_table");
            FacilityEvolutionState authoredTrainingState = State(
                "building:qa-phase77-authored-training");
            Require(FacilityFormulaEvolutionAuthority.TryPrepare(
                        authoredTrainingState,
                        archeryRecipe,
                        out FacilityEvolutionFormulaPresentationPendingSnapshot authoredTrainingPending,
                        out string authoredTrainingFailure)
                    && authoredTrainingPending?.node != null
                    && authoredTrainingPending.node.moduleSelectionOffers
                        .Where(offer => offer != null
                            && offer.polarity
                                == EvolutionModuleOfferPolarity.Positive)
                        .Select(offer => offer.moduleId)
                        .SequenceEqual(new[] { "facility:training-operations" },
                            StringComparer.Ordinal),
                "The authored training recipe did not retain its legal positive offer: "
                + authoredTrainingFailure);
            VerifyExposedPositiveDrawbackPairsResolve(
                authoredTrainingState,
                archeryRecipe,
                authoredTrainingPending.node);
            FacilityGameplayUsageSource visit = GameplayNarrativeSourceCatalog
                .RequireFacility("facility:visit");
            Require(trainingSource?.Facility?.IsVisitorFacility == true
                    && displaySource?.Facility?.IsVisitorFacility == true
                    && securitySource?.Facility?.IsVisitorFacility == true
                    && result?.Facility?.IsVisitorFacility == true
                    && displaySource.runtimeArchetype
                        == BuildingRuntimeArchetypeKind.Facility
                    && result.runtimeArchetype
                        == BuildingRuntimeArchetypeKind.Facility
                    && result.GetAbility<BuildingServiceSupportAbility>()?.IsValid == true
                    && result.GetAbility<BuildingServiceSupportAbility>()?.SupportId
                        == "service-retail-secure-display"
                    && visit.AppliesTo(trainingSource, out _)
                    && visit.AppliesTo(displaySource, out _)
                    && visit.AppliesTo(securitySource, out _)
                    && visit.IsDirectLedgerReplaySupported(out _)
                    && FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            archery, trainingModule, out _)
                    && FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            tactical, securityModule, out _)
                    && FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            result, supportModule, out _)
                    && HasExactCapability(
                        archeryRecipe, "facility:training-operations")
                    && HasExactCapability(
                        displayRecipe, "facility:service-support")
                    && HasExactCapability(
                        tacticalRecipe, "facility:security-operations")
                    && HasExactSource(archeryRecipe, trainingSource)
                    && HasExactSource(displayRecipe, displaySource)
                    && HasExactSource(tacticalRecipe, securitySource),
                "Phase77 authored assets do not close all three visit sources, real consumers, and explicit offers.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(trainingRecipe);
            UnityEngine.Object.DestroyImmediate(securityRecipe);
            UnityEngine.Object.DestroyImmediate(supportRecipe);
            UnityEngine.Object.DestroyImmediate(training);
            UnityEngine.Object.DestroyImmediate(security);
            UnityEngine.Object.DestroyImmediate(support);
        }
    }

    private static bool HasExactCapability(
        FacilityEvolutionRecipeSO recipe,
        string moduleId) => recipe != null
        && recipe.formulaCapabilities != null
        && recipe.formulaCapabilities.Count == 1
        && string.Equals(
            recipe.formulaCapabilities[0]?.evolutionModuleId,
            moduleId,
            StringComparison.Ordinal);

    private static bool HasExactSource(
        FacilityEvolutionRecipeSO recipe,
        BuildingSO source) => recipe?.fromFacilities?.Length == 1
        && recipe.fromFacilities[0] == source;

    private static BuildingSO LoadModularBuilding(string assetName) =>
        AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/" + assetName + ".asset");

    private static void VerifyExposedPositiveDrawbackPairsResolve(
        FacilityEvolutionState state,
        FacilityEvolutionRecipeSO recipe,
        EvolutionNode pending)
    {
        NarrativeFormulaModuleSelectionRequest request =
            FacilityFormulaEvolutionAuthority.BuildModuleSelectionRequest(
                pending, recipe);
        string[] positiveIds = request.Offers
            .Where(offer => offer.Polarity == NarrativeFormulaModulePolarity.Positive)
            .Select(offer => offer.ModuleId)
            .ToArray();
        string[] drawbackIds = request.Offers
            .Where(offer => offer.Polarity == NarrativeFormulaModulePolarity.Drawback)
            .Select(offer => offer.ModuleId)
            .ToArray();
        foreach (string positiveId in positiveIds)
        foreach (string drawbackId in drawbackIds)
        {
            foreach (string[] selectedEvidenceIds in
                     EnumerateNonEmptyEvidenceSubsets(request.EvidenceFactIds))
            {
                NarrativeFormulaModuleSelectionChoice choice = new(
                    request.SelectionId,
                    new[] { positiveId },
                    new[] { drawbackId },
                    selectedEvidenceIds);
                if (!NarrativeFormulaModuleSelectionValidator.TryValidate(
                        request,
                        choice,
                        out _,
                        out _))
                {
                    // A forbidden-synergy pair cannot be selected, so it is not a
                    // resolution offer. Every valid subset below must resolve.
                    continue;
                }

                try
                {
                    EvolutionNode resolved = FacilityFormulaEvolutionAuthority
                        .FreezeSelectedModule(state, recipe, pending, choice);
                    Require(resolved != null
                            && string.Equals(resolved.effectId, positiveId,
                                StringComparison.Ordinal)
                            && string.Equals(resolved.burdenEffectId, drawbackId,
                                StringComparison.Ordinal),
                        "An exposed facility positive/drawback pair did not retain its selected modules.");
                }
                catch (InvalidOperationException exception)
                {
                    throw new InvalidOperationException(
                        "An exposed facility positive/drawback evidence subset was numerically infeasible: "
                        + positiveId + "+" + drawbackId + "; evidence="
                        + string.Join(",", selectedEvidenceIds) + "; " + exception.Message,
                        exception);
                }
            }
        }
    }

    private static IEnumerable<string[]> EnumerateNonEmptyEvidenceSubsets(
        IReadOnlyList<string> evidenceIds)
    {
        if (evidenceIds == null || evidenceIds.Count == 0 || evidenceIds.Count > 4)
            throw new InvalidOperationException(
                "The focused optional-drawback test requires one to four request evidence IDs.");
        int subsetCount = 1 << evidenceIds.Count;
        for (int mask = 1; mask < subsetCount; mask++)
        {
            List<string> subset = new();
            for (int index = 0; index < evidenceIds.Count; index++)
            {
                if ((mask & (1 << index)) != 0)
                    subset.Add(evidenceIds[index]);
            }
            yield return subset.ToArray();
        }
    }

    private static void VerifyReadyV3NodeWithHistoricalOffersRestoresExactly()
    {
        GameObject sourceObject = new("FacilityEvolutionReadyV3SourceFixture");
        GameObject restoredObject = new("FacilityEvolutionReadyV3RestoreFixture");
        GameObject tamperedObject = new("FacilityEvolutionReadyV3TamperedFixture");
        try
        {
            FacilityEvolutionState readyState = new()
            {
                facilityPersistentId = "building:qa-ready-v3",
                usageLedger = new UsageLedger { nextSequence = 1 },
                evolutionNodes = new List<EvolutionNode>
                {
                    new()
                    {
                        nodeId = "facility:qa-ready-v3-node",
                        effectId = "facility:service",
                        active = true,
                        formulaVersion =
                            FacilityFormulaEvolutionAuthority.DrawbackModuleSelectionFormulaVersion,
                        presentationState = EquipmentEvolutionPresentationState.Ready,
                        potencyMultiplier = 1f,
                        moduleSelectionOffers = new List<EquipmentEvolutionModuleOfferState>
                        {
                            new()
                            {
                                moduleId = "facility:room-synergy",
                                polarity = EvolutionModuleOfferPolarity.Positive,
                                semanticDescription = "historical same-axis offer"
                            },
                            new()
                            {
                                moduleId = "facility:service",
                                polarity = EvolutionModuleOfferPolarity.Positive,
                                semanticDescription = "historical selected offer"
                            }
                        }
                    }
                },
                activeNodeIds = new List<string> { "facility:qa-ready-v3-node" }
            };
            FacilityEvolutionStateComponent source =
                sourceObject.AddComponent<FacilityEvolutionStateComponent>();
            FacilityEvolutionStateSnapshot snapshot = source.CreateSnapshot();
            snapshot.baseFacilityId = "fixture:facility-base";
            snapshot.currentFacilityId = "fixture:facility-current";
            snapshot.instanceEvolution = readyState;
            source.ApplySnapshot(snapshot);
            string payload = source.CaptureState();

            FacilityEvolutionStateComponent restored =
                restoredObject.AddComponent<FacilityEvolutionStateComponent>();
            Require(restored.TryRestoreState(restored.CurrentVersion, payload, out string error),
                "A ready v3 node with historical offers did not restore: " + error);
            EvolutionNode restoredNode = restored.InstanceEvolution.evolutionNodes.Single();
            Require(restoredNode.presentationState == EquipmentEvolutionPresentationState.Ready
                    && restoredNode.effectId == "facility:service"
                    && restored.PendingFormulaPresentation == null
                    && restoredNode.moduleSelectionOffers.Select(value => value.moduleId)
                        .SequenceEqual(new[] { "facility:room-synergy", "facility:service" }),
                "The new-generation gate rewrote a committed v3 node or its historical offers.");

            FacilityEvolutionStateSnapshot partialEnvelope =
                JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(payload);
            Require(partialEnvelope != null,
                "The ready v3 fixture could not be deserialized for tamper coverage.");
            FacilityEvolutionStateComponent tampered =
                tamperedObject.AddComponent<FacilityEvolutionStateComponent>();
            Require(tampered.TryRestoreState(tampered.CurrentVersion, payload, out string baselineError),
                "The tamper fixture could not establish a valid baseline: " + baselineError);
            string beforeTamperPayload = tampered.CaptureState();

            partialEnvelope.pendingFormulaPresentation =
                new FacilityEvolutionFormulaPresentationPendingSnapshot
                {
                    recipeId = "recipe:partial-envelope"
                };
            Require(!tampered.TryRestoreState(
                    tampered.CurrentVersion,
                    JsonUtility.ToJson(partialEnvelope),
                    out string tamperError)
                && tamperError.Contains("identity is invalid", StringComparison.Ordinal)
                && string.Equals(beforeTamperPayload, tampered.CaptureState(),
                    StringComparison.Ordinal),
                "A partially populated pending formula envelope was accepted as a null sentinel.");

            FacilityEvolutionStateSnapshot negativeFailureEnvelope =
                JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(payload);
            negativeFailureEnvelope.pendingFormulaPresentation =
                new FacilityEvolutionFormulaPresentationPendingSnapshot
                {
                    failureCount = -1
                };
            string negativeFailurePayload = JsonUtility.ToJson(negativeFailureEnvelope);
            bool rawApplyRejected = false;
            try
            {
                tampered.ApplySnapshot(negativeFailureEnvelope);
            }
            catch (InvalidOperationException)
            {
                rawApplyRejected = true;
            }
            Require(!tampered.TryRestoreState(
                    tampered.CurrentVersion,
                    negativeFailurePayload,
                    out string negativeFailureError)
                && negativeFailureError.Contains("identity is invalid", StringComparison.Ordinal)
                && rawApplyRejected
                && string.Equals(beforeTamperPayload, tampered.CaptureState(),
                    StringComparison.Ordinal),
                "A negative pending failure count was clamped into an empty sentinel.");

            FacilityEvolutionStateSnapshot nullEntryEnvelope =
                JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(payload);
            nullEntryEnvelope.pendingFormulaPresentation =
                new FacilityEvolutionFormulaPresentationPendingSnapshot
                {
                    node = new EvolutionNode
                    {
                        evidenceIds = new List<string> { null }
                    }
                };
            Require(!tampered.TryRestoreState(
                    tampered.CurrentVersion,
                    JsonUtility.ToJson(nullEntryEnvelope),
                    out string nullEntryError)
                && nullEntryError.Contains("identity is invalid", StringComparison.Ordinal)
                && string.Equals(beforeTamperPayload, tampered.CaptureState(),
                    StringComparison.Ordinal),
                "A pending node with a null evidence entry was pruned into an empty sentinel.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceObject);
            UnityEngine.Object.DestroyImmediate(restoredObject);
            UnityEngine.Object.DestroyImmediate(tamperedObject);
        }
    }

    private static FacilityEvolutionState State(string persistentId) => new()
    {
        facilityPersistentId = persistentId,
        usageLedger = new UsageLedger
        {
            nextSequence = 2,
            currentGenerationEvents = new List<UsageLedgerEvent>
            {
                new()
                {
                    evidenceId = "qa-same-axis-evidence",
                    eventId = "facility-operation",
                    actorId = "character:qa",
                    targetId = persistentId,
                    outcomeId = "failed-operation",
                    amount = 2f,
                    repeatCount = 1,
                    sequence = 1
                }
            }
        }
    };

    private static FacilityEvolutionState StateWithFourEvidence(string persistentId)
    {
        FacilityEvolutionState state = State(persistentId);
        state.usageLedger.nextSequence = 5;
        state.usageLedger.currentGenerationEvents = Enumerable.Range(0, 4)
            .Select(index => new UsageLedgerEvent
            {
                evidenceId = "qa-four-evidence-" + index,
                eventId = index == 3 ? "facility:invasion-damage" : "facility:visit",
                actorId = "character:qa-four-evidence-" + index,
                targetId = persistentId,
                outcomeId = index == 3 ? "damaged" : "completed",
                amount = 1f,
                repeatCount = 1,
                sequence = index + 1
            })
            .ToList();
        return state;
    }

    private static FacilityEvolutionRecipeSO Recipe(
        string id,
        BuildingSO target,
        params string[] moduleIds)
    {
        FacilityEvolutionRecipeSO recipe = ScriptableObject.CreateInstance<FacilityEvolutionRecipeSO>();
        recipe.evolutionId = id;
        recipe.displayName = id;
        recipe.resultBuilding = target;
        recipe.formulaPolicy = new FacilityEvolutionFormulaPolicyDefinition
        {
            formulaVersion = FacilityFormulaEvolutionAuthority.DrawbackModuleSelectionFormulaVersion,
            catalogSha256 = new string('d', 64),
            baseBudget = 4,
            powerScale = 0,
            softCapK = 1f,
            minimumImportance = 0f,
            maximumImportance = 4f,
            milestoneWeights = new List<float> { 1f },
            drawbackCredit = new NarrativeFormulaDrawbackCreditPolicyDefinition
            {
                playerChoiceMaximumBudgetFraction = 0.5f,
                automaticMaximumBudgetFraction = 0.25f,
                absoluteMaximumCredit = 3,
                requireNegativeEvidenceForAutomatic = true
            },
            guaranteedProc = true,
            targetCount = 1
        };
        recipe.formulaCapabilities = moduleIds.Select(Capability).ToList();
        return recipe;
    }

    private static FacilityEvolutionFormulaCapabilityDefinition Capability(string moduleId)
    {
        string capabilityId = "qa:facility:" + moduleId.Replace(':', '-');
        return new FacilityEvolutionFormulaCapabilityDefinition
        {
            capabilityId = capabilityId,
            evolutionModuleId = moduleId,
            baseCost = 0,
            narrativeAffinity = 1f,
            affinityKeys = new List<string> { "facility" },
            conflictGroups = new List<string> { "facility-module" },
            forbiddenSynergies = new List<string>(),
            formatterId = capabilityId,
            applicatorId = capabilityId,
            parameterRanges = new List<FacilityEvolutionFormulaRangeDefinition>
            {
                new()
                {
                    parameterId = NarrativeFormulaParameterIds.Magnitude,
                    minimumUnits = 10000,
                    maximumUnits = 10000,
                    quantumUnits = 1,
                    decimalPlaces = 4,
                    costPerQuantum = 1
                },
                new()
                {
                    parameterId = NarrativeFormulaParameterIds.Duration,
                    minimumUnits = 0,
                    maximumUnits = 0,
                    quantumUnits = 1,
                    decimalPlaces = 0,
                    costPerQuantum = 1
                },
                new()
                {
                    parameterId = NarrativeFormulaParameterIds.Count,
                    minimumUnits = 1,
                    maximumUnits = 1,
                    quantumUnits = 1,
                    decimalPlaces = 0,
                    costPerQuantum = 1
                },
                new()
                {
                    parameterId = NarrativeFormulaParameterIds.TargetCount,
                    minimumUnits = 1,
                    maximumUnits = 1,
                    quantumUnits = 1,
                    decimalPlaces = 0,
                    costPerQuantum = 1
                }
            }
        };
    }

    private static BuildingSO Target(
        string name,
        FacilityRole roles,
        bool includeProductionConsumer,
        params WorkTypeId[] workTypes)
    {
        BuildingSO target = ScriptableObject.CreateInstance<BuildingSO>();
        target.objectName = name;
        FacilityData facility = new()
        {
            roles = roles,
            capacity = 1,
            requiredWorkers = 1
        };
        facility.SetSupportedWorkTypeIds(workTypes);
        target.Facility = facility;
        if (includeProductionConsumer)
        {
            target.AbilityModules.Add(new BuildingProductionAbility
            {
                outputCategory = StockCategory.General,
                amount = 1
            });
        }
        return target;
    }

    private static EvolutionModuleDefinition Module(
        string id,
        EvolutionEffectModifier benefit,
        EvolutionEffectModifier burden) => new(
        id,
        id,
        "qa",
        new[] { benefit },
        new[] { burden },
        burdenKind: EvolutionModuleBurdenKind.OperatingCost);

    private static EvolutionEffectModifier Benefit(string statId, float multiplier) => new()
    {
        statId = statId,
        multiplier = multiplier
    };

    private static EvolutionEffectModifier Burden(string statId, float multiplier) => new()
    {
        statId = statId,
        multiplier = multiplier
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void RequireThrowsContaining(
        Action action,
        string expectedMessageFragment,
        string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains(
            expectedMessageFragment,
            StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(message);
    }
}
#endif
