using System;
using System.Collections.Generic;

/// <summary>Controlled normal and policy-boundary cases for all 29 life rules.</summary>
public static class GameplayLifeExperienceRuleCases
{
    public static IEnumerable<GameplayExperiencePolicyCase> Create()
    {
        var rows = new List<GameplayExperiencePolicyCase>();
        AddEvolution(rows);
        AddSocialLife(rows);
        AddCharacterLife(rows);
        return rows;
    }

    private static void AddEvolution(List<GameplayExperiencePolicyCase> rows)
    {
        GameplayOutcomeEvidenceBindingSnapshot fact = B(
            EvolutionOutcomeIds.ApparelChanged.Value, EvolutionOutcomeIds.WearerRole.Value);
        M(fact, EvolutionOutcomeIds.ApparelEquippedMetric.Value, 1d,
            EvolutionOutcomeIds.BooleanUnit.Value, "apparel", "apparel:test");
        M(fact, EvolutionOutcomeIds.ApparelOriginMetric.Value,
            (int)CharacterCommandOrigin.Autonomous, EvolutionOutcomeIds.EnumUnit.Value,
            "apparel", "apparel:test");
        C(rows, "apparel-change-routine", fact, GameplayExperienceCategory.Routine);

        fact = Physical(false, CraftsmanshipQualityTier.Normal, false);
        C(rows, "apparel-physical-routine", fact, GameplayExperienceCategory.Routine);
        fact = Physical(true, CraftsmanshipQualityTier.Legendary, false);
        C(rows, "apparel-physical-legendary", fact, GameplayExperienceCategory.Meaningful);
        fact = Physical(true, CraftsmanshipQualityTier.Legendary, false);
        fact.subjectKindId = "item-instance";
        fact.subjectId = "apparel-output:test";
        C(rows, "apparel-physical-output-without-participant",
            fact, GameplayExperienceCategory.Meaningful);
        fact = Physical(true, CraftsmanshipQualityTier.Mythic, true);
        C(rows, "apparel-physical-rejected", fact, GameplayExperienceCategory.NotExperience);

        fact = Quality(CraftsmanshipQualityTier.Normal, false);
        C(rows, "product-quality-normal", fact, GameplayExperienceCategory.Mild);
        fact = Quality(CraftsmanshipQualityTier.Mythic, false);
        C(rows, "product-quality-highest", fact, GameplayExperienceCategory.Meaningful);
        fact = Quality(CraftsmanshipQualityTier.Legendary, true);
        C(rows, "product-quality-rejected", fact, GameplayExperienceCategory.NotExperience);

        fact = SkillAcquired(CharacterSkillKind.Active);
        C(rows, "skill-acquired-active", fact, GameplayExperienceCategory.Meaningful);
        fact = SkillAcquired(CharacterSkillKind.Ultimate);
        C(rows, "skill-acquired-ultimate", fact, GameplayExperienceCategory.Intense);

        fact = SkillActivation(CharacterSkillActivationOutcomeIds.SourceRole.Value, "none");
        C(rows, "skill-activation-only", fact, GameplayExperienceCategory.Mild);
        fact = SkillActivation(CharacterSkillActivationOutcomeIds.RecipientRole.Value,
            "child-producer:child-operation:1:0");
        C(rows, "skill-recipient-result-without-typed-effect", fact,
            GameplayExperienceCategory.Mild);

        fact = Ambition(CharacterAmbitionStatus.None, CharacterAmbitionStatus.Active,
            CharacterAmbitionTransitionOutcomeIds.ActivationReason);
        C(rows, "ambition-start", fact, GameplayExperienceCategory.Mild);
        fact = Ambition(CharacterAmbitionStatus.Active, CharacterAmbitionStatus.Failed,
            CharacterAmbitionTransitionOutcomeIds.DeadlineReason);
        C(rows, "ambition-deadline-failure", fact, GameplayExperienceCategory.Meaningful);

        fact = AgeDamage(100d, 100d, 0d);
        C(rows, "age-condition-no-damage", fact, GameplayExperienceCategory.Routine);
        fact = AgeDamage(100d, 40d, 60d);
        C(rows, "age-condition-major-damage", fact, GameplayExperienceCategory.Intense);
        fact = AgeDamage(10d, 0d, 10d);
        C(rows, "age-condition-health-exhausted", fact, GameplayExperienceCategory.Transformative);

        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value,
            "planned", "none", "planned", "none");
        C(rows, "reproduction-planned", fact, GameplayExperienceCategory.Mild);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.CarrierRole.Value,
            "started", "planned", "active", "none");
        C(rows, "reproduction-started", fact, GameplayExperienceCategory.Mild);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value,
            "daily-transition", "active", "active", "none");
        C(rows, "reproduction-daily-stable", fact, GameplayExperienceCategory.Routine);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.CarrierRole.Value,
            "daily-transition", "active", "waiting-environment", "none");
        C(rows, "reproduction-waiting-environment", fact, GameplayExperienceCategory.Mild);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.CarrierRole.Value,
            "daily-transition", "waiting-environment", "waiting-environment", "none");
        C(rows, "reproduction-environment-wait-continues", fact,
            GameplayExperienceCategory.Routine);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.CarrierRole.Value,
            "daily-transition", "waiting-environment", "active", "none");
        C(rows, "reproduction-environment-recovered", fact, GameplayExperienceCategory.Mild);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.CarrierRole.Value,
            "daily-transition", "waiting-environment", "completed", "none");
        C(rows, "reproduction-completed-after-environment-wait", fact,
            GameplayExperienceCategory.Mild);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value,
            "daily-transition", "active", "completed", "none");
        C(rows, "reproduction-process-completed-before-birth", fact,
            GameplayExperienceCategory.Mild);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value,
            "daily-transition", "active", "failed", "miscarriage");
        C(rows, "reproduction-miscarriage", fact, GameplayExperienceCategory.Transformative);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value,
            "daily-transition", "active", "failed", "conception-failed");
        C(rows, "reproduction-conception-failed", fact, GameplayExperienceCategory.Meaningful);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value,
            "daily-transition", "waiting-environment", "failed", "conception-failed");
        C(rows, "reproduction-conception-failed-after-environment-wait", fact,
            GameplayExperienceCategory.Meaningful);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.CarrierRole.Value,
            "daily-transition", "waiting-environment", "failed",
            "incubation-environment-failed");
        C(rows, "reproduction-environment-failed", fact, GameplayExperienceCategory.Meaningful);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value,
            "daily-transition", "waiting-emergency-extraction", "failed",
            "emergency-extraction-expired");
        C(rows, "reproduction-emergency-expired", fact, GameplayExperienceCategory.Intense);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.CarrierRole.Value,
            "carrier-death", "active", "waiting-emergency-extraction", "none");
        C(rows, "reproduction-carrier-death", fact, GameplayExperienceCategory.Transformative);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value,
            "carrier-death", "active", "failed", "carrier-died");
        C(rows, "reproduction-early-carrier-death", fact,
            GameplayExperienceCategory.Transformative);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.GeneticParentRole.Value,
            "emergency-extraction", "waiting-emergency-extraction", "active", "none");
        C(rows, "reproduction-emergency-crisis", fact, GameplayExperienceCategory.Intense);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.MedicalResponderRole.Value,
            "emergency-extraction", "waiting-emergency-extraction", "active", "none");
        C(rows, "reproduction-medical-response", fact, GameplayExperienceCategory.Meaningful);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.OffspringRole.Value,
            "birth-published", "completed", "completed", "none");
        C(rows, "reproduction-birth", fact, GameplayExperienceCategory.Transformative);
        fact = Reproduction(ReproductionProcessTransitionOutcomeIds.FacilityRole.Value,
            "birth-published", "completed", "completed", "none", "facility");
        C(rows, "reproduction-facility", fact, GameplayExperienceCategory.Routine);

        fact = B(EquipmentModuleSelectionOutcomeIds.Selected.Value,
            EquipmentModuleSelectionOutcomeIds.SelectedNodeRole.Value,
            kind: "equipment-evolution-node");
        M(fact, EquipmentModuleSelectionOutcomeIds.RevisionBefore.Value, 4,
            EquipmentModuleSelectionOutcomeIds.RevisionUnit.Value);
        M(fact, EquipmentModuleSelectionOutcomeIds.RevisionAfter.Value, 5,
            EquipmentModuleSelectionOutcomeIds.RevisionUnit.Value);
        F(fact, EquipmentModuleSelectionOutcomeIds.SelectionIdFact.Value, "selection:test");
        F(fact, EquipmentModuleSelectionOutcomeIds.ModulesFact.Value, "modules-v1:test");
        C(rows, "equipment-module-selected", fact, GameplayExperienceCategory.Meaningful);

        fact = B(EvolutionOutcomeIds.TraitInferenceCompleted.Value,
            EvolutionOutcomeIds.AcquiredTraitOwnerRole.Value);
        M(fact, EvolutionOutcomeIds.TraitExpectedRevisionMetric.Value, 3,
            EvolutionOutcomeIds.RevisionUnit.Value);
        M(fact, EvolutionOutcomeIds.TraitResultingRevisionMetric.Value, 4,
            EvolutionOutcomeIds.RevisionUnit.Value);
        F(fact, EvolutionOutcomeIds.TraitInstanceFact.Value, "trait-instance:test");
        F(fact, EvolutionOutcomeIds.TraitCombinationFact.Value, "trait-combination:test");
        C(rows, "acquired-trait-completed", fact, GameplayExperienceCategory.Meaningful);

        fact = B(HeritableTraitAnalysisOutcomeIds.Completed.Value,
            HeritableTraitAnalysisOutcomeIds.SubjectRole.Value);
        M(fact, HeritableTraitAnalysisOutcomeIds.BeforeState.Value, 0,
            HeritableTraitAnalysisOutcomeIds.BooleanUnit.Value);
        M(fact, HeritableTraitAnalysisOutcomeIds.AfterState.Value, 1,
            HeritableTraitAnalysisOutcomeIds.BooleanUnit.Value);
        M(fact, HeritableTraitAnalysisOutcomeIds.ConsumedKitCount.Value, 1,
            HeritableTraitAnalysisOutcomeIds.CountUnit.Value);
        M(fact, HeritableTraitAnalysisOutcomeIds.PhysicalInputMass.Value, 250,
            HeritableTraitAnalysisOutcomeIds.GramUnit.Value);
        F(fact, HeritableTraitAnalysisOutcomeIds.FacilityInstanceId.Value, "facility:test");
        F(fact, HeritableTraitAnalysisOutcomeIds.LatentTraitIds.Value, "trait:latent");
        C(rows, "heritable-traits-analyzed", fact, GameplayExperienceCategory.Mild);

        fact = B(EvolutionOutcomeIds.TraitReactionExecuted.Value,
            EvolutionOutcomeIds.TraitOwnerRole.Value);
        M(fact, EvolutionOutcomeIds.TraitMoodMetric.Value, 12,
            EvolutionOutcomeIds.EffectPointUnit.Value, "trait-reaction", "reaction:test");
        C(rows, "trait-reaction-routine", fact, GameplayExperienceCategory.Routine);

        fact = B(EvolutionOutcomeIds.FacilityEvolutionCompleted.Value,
            EvolutionOutcomeIds.EvolvedFacilityRole.Value, kind: "facility");
        M(fact, EvolutionOutcomeIds.FacilityStarGradeMetric.Value, 3,
            EvolutionOutcomeIds.GradeUnit.Value, "facility-definition", "facility-definition:test");
        M(fact, EvolutionOutcomeIds.MaterialQuantityMetric.Value, 4,
            EvolutionOutcomeIds.CountUnit.Value, "facility-definition", "facility-definition:test");
        F(fact, EvolutionOutcomeIds.FacilityRecipeFact.Value, "recipe:test");
        F(fact, EvolutionOutcomeIds.FacilityResultDefinitionFact.Value, "facility-definition:test");
        C(rows, "facility-evolution-completed", fact, GameplayExperienceCategory.Meaningful);

        fact = MemoryErasure(MemoryErasureSealUseStatus.Succeeded, 7, 8, 1,
            GameplayOutcomeStatus.Succeeded);
        C(rows, "memory-erasure-applied", fact, GameplayExperienceCategory.Intense);
        fact = MemoryErasure(MemoryErasureSealUseStatus.AlreadyCompleted, 8, 8, 0,
            GameplayOutcomeStatus.Succeeded);
        C(rows, "memory-erasure-already-completed", fact, GameplayExperienceCategory.NotExperience);
        fact = MemoryErasure(MemoryErasureSealUseStatus.InterruptedBeforePickup, 8, 8, 0,
            GameplayOutcomeStatus.Cancelled);
        C(rows, "memory-erasure-interrupted", fact, GameplayExperienceCategory.NotExperience);

        fact = B(EvolutionOutcomeIds.MemoryErasureBossAwarded.Value,
            EvolutionOutcomeIds.AwardedRegionRole.Value, kind: "offense-region");
        M(fact, EvolutionOutcomeIds.AwardedItemCountMetric.Value, 1,
            EvolutionOutcomeIds.CountUnit.Value);
        C(rows, "boss-award-non-personal", fact, GameplayExperienceCategory.NotExperience);

        fact = Fasting(CharacterRitualFastingOutcomeIds.Started,
            CharacterRitualFastPhase.Inactive, CharacterRitualFastPhase.Fasting, -1, 5);
        C(rows, "ritual-fasting-started", fact, GameplayExperienceCategory.Mild);
        fact = Fasting(CharacterRitualFastingOutcomeIds.Completed,
            CharacterRitualFastPhase.Fasting, CharacterRitualFastPhase.AwaitingPostFastMeal, 5, 5);
        C(rows, "ritual-fasting-completed", fact, GameplayExperienceCategory.Meaningful);
        fact = Fasting(CharacterRitualFastingOutcomeIds.Interrupted,
            CharacterRitualFastPhase.Fasting, CharacterRitualFastPhase.Inactive, 5, -1);
        C(rows, "ritual-fasting-interrupted", fact, GameplayExperienceCategory.Mild);
    }

    private static void AddSocialLife(List<GameplayExperiencePolicyCase> rows)
    {
        C(rows, "conflict-zero-delta", Conflict(0d, false), GameplayExperienceCategory.Routine);
        C(rows, "conflict-nine-delta", Conflict(-9d, false), GameplayExperienceCategory.Mild);
        C(rows, "conflict-ten-delta", Conflict(-10d, false), GameplayExperienceCategory.Meaningful);
        C(rows, "conflict-venue", Conflict(-30d, true), GameplayExperienceCategory.Routine);
        C(rows, "conflict-instigator-effect-cap", ConflictInstigator(-30d),
            GameplayExperienceCategory.Mild);

        C(rows, "apology-zero-delta", Apology(0d), GameplayExperienceCategory.Routine);
        C(rows, "apology-nine-delta", Apology(9d), GameplayExperienceCategory.Mild);
        C(rows, "apology-offender-effect-cap", Apology(10d), GameplayExperienceCategory.Mild);
        C(rows, "apology-recipient-ten-delta",
            Apology(10d, SocialLifeOutcomeIds.RecipientRole.Value),
            GameplayExperienceCategory.Meaningful);

        C(rows, "idle-no-change", Idle(true, 0d, 0d), GameplayExperienceCategory.Routine);
        C(rows, "idle-minor-change", Idle(false, 9d, 0d), GameplayExperienceCategory.Mild);
        C(rows, "idle-substantial-change", Idle(false, 0d, 10d), GameplayExperienceCategory.Meaningful);

        C(rows, "staff-suppressed", StaffResponse(StaffRebellionResponseType.SuppressCommand,
            StaffDiscontentOutcomeIds.StaffRole.Value), GameplayExperienceCategory.Intense);
        C(rows, "staff-calmed", StaffResponse(StaffRebellionResponseType.Calm,
            StaffDiscontentOutcomeIds.StaffRole.Value), GameplayExperienceCategory.Mild);
        C(rows, "staff-responder", StaffResponse(StaffRebellionResponseType.Isolate,
            StaffDiscontentOutcomeIds.ResponderRole.Value), GameplayExperienceCategory.Mild);

        C(rows, "staff-warning", StaffTransition(StaffDiscontentOutcome.Warning),
            GameplayExperienceCategory.Mild);
        C(rows, "staff-efficiency-penalty", StaffTransition(StaffDiscontentOutcome.EfficiencyPenalty),
            GameplayExperienceCategory.Meaningful);
        C(rows, "staff-permanent-departure", StaffTransition(StaffDiscontentOutcome.PermanentDeparture),
            GameplayExperienceCategory.Intense);
        C(rows, "staff-local-rebellion", StaffTransition(StaffDiscontentOutcome.LocalRebellion),
            GameplayExperienceCategory.Intense);

        C(rows, "performer-milestone-50", PerformerMilestone(50), GameplayExperienceCategory.Mild);
        C(rows, "performer-milestone-75", PerformerMilestone(75), GameplayExperienceCategory.Meaningful);
        C(rows, "performer-milestone-100", PerformerMilestone(100), GameplayExperienceCategory.Intense);

        GameplayOutcomeEvidenceBindingSnapshot fact = B(CaptiveEscapeOutcomeIds.Escaped.Value,
            CaptiveEscapeOutcomeIds.CaptiveRole.Value);
        M(fact, CaptiveEscapeOutcomeIds.OwnerRevisionMetric.Value, 3,
            CaptiveEscapeOutcomeIds.RevisionUnit.Value);
        F(fact, CaptiveEscapeOutcomeIds.KindFact.Value, "physical-escape");
        F(fact, CaptiveEscapeOutcomeIds.PreviousStatusFact.Value, CaptivityStatus.EscapeAttempt.ToString());
        C(rows, "captive-escaped", fact, GameplayExperienceCategory.Transformative);

        fact = B(CaptiveRansomOutcomeIds.Ransomed.Value,
            CaptiveRansomOutcomeIds.CaptiveRole.Value);
        M(fact, CaptiveRansomOutcomeIds.AmountMetric.Value, 100,
            CaptiveRansomOutcomeIds.GoldUnit.Value);
        F(fact, CaptiveRansomOutcomeIds.PreviousStatusFact.Value, CaptivityStatus.Confined.ToString());
        C(rows, "captive-ransomed", fact, GameplayExperienceCategory.Transformative);

        C(rows, "captivity-interaction-routine", Interaction(
            CaptivityInteractionOutcomeIds.CaptiveRole.Value, 0, 0, true),
            GameplayExperienceCategory.Routine);
        C(rows, "captivity-interaction-minor", Interaction(
            CaptivityInteractionOutcomeIds.CaptiveRole.Value, 19, 0, true),
            GameplayExperienceCategory.Mild);
        C(rows, "captivity-interaction-substantial", Interaction(
            CaptivityInteractionOutcomeIds.CaptiveRole.Value, 20, 0, true),
            GameplayExperienceCategory.Meaningful);
        C(rows, "captivity-interaction-lethal", Interaction(
            CaptivityInteractionOutcomeIds.CaptiveRole.Value, 0, 100, true),
            GameplayExperienceCategory.Transformative);
        C(rows, "captivity-warden-damage-cap", Interaction(
            CaptivityInteractionOutcomeIds.WardenRole.Value, 0, 100, true),
            GameplayExperienceCategory.Meaningful);
        C(rows, "captivity-facility-routine", Interaction(
            CaptivityInteractionOutcomeIds.FacilityRole.Value, 30, 20, true, "facility"),
            GameplayExperienceCategory.Routine);
        C(rows, "captivity-interaction-failed", Interaction(
            CaptivityInteractionOutcomeIds.CaptiveRole.Value, 0, 0, false),
            GameplayExperienceCategory.NotExperience);

        C(rows, "rehabilitation-progress", Rehabilitation(
            CaptivityTransitionOutcomeIds.CaptiveRole.Value, false),
            GameplayExperienceCategory.Routine);
        C(rows, "rehabilitation-day", Rehabilitation(
            CaptivityTransitionOutcomeIds.WardenRole.Value, true),
            GameplayExperienceCategory.Mild);
        C(rows, "rehabilitation-facility", Rehabilitation(
            CaptivityTransitionOutcomeIds.FacilityRole.Value, true, "building"),
            GameplayExperienceCategory.Routine);

        C(rows, "release-captive", Release(CaptivityTransitionOutcomeIds.CaptiveRole.Value),
            GameplayExperienceCategory.Transformative);
        C(rows, "release-decider", Release(CaptivityTransitionOutcomeIds.DeciderRole.Value),
            GameplayExperienceCategory.Meaningful);
        C(rows, "release-facility", Release(CaptivityTransitionOutcomeIds.FacilityRole.Value, "building"),
            GameplayExperienceCategory.Routine);
    }

    private static void AddCharacterLife(List<GameplayExperiencePolicyCase> rows)
    {
        GameplayOutcomeEvidenceBindingSnapshot fact = B(VisitorExitOutcomeIds.TheftCommitted.Value,
            VisitorExitOutcomeIds.ThiefRole.Value);
        M(fact, VisitorExitOutcomeIds.QuantityMetric.Value, 1, VisitorExitOutcomeIds.CountUnit.Value);
        M(fact, VisitorExitOutcomeIds.MassMetric.Value, 150, VisitorExitOutcomeIds.GramsUnit.Value);
        F(fact, VisitorExitOutcomeIds.SourceQuantityBefore.Value, "3");
        F(fact, VisitorExitOutcomeIds.SourceQuantityAfter.Value, "2");
        F(fact, VisitorExitOutcomeIds.CarryQuantityBefore.Value, "0");
        F(fact, VisitorExitOutcomeIds.CarryQuantityAfter.Value, "1");
        F(fact, VisitorExitOutcomeIds.ItemInstanceId.Value, "item-instance:test");
        C(rows, "floor-theft-committed", fact, GameplayExperienceCategory.Meaningful);

        C(rows, "visitor-departed", Departure(VisitorDepartureTerminalKind.Departed,
            GameplayOutcomeStatus.Succeeded, "none"), GameplayExperienceCategory.Routine);
        C(rows, "visitor-departed-after-theft", Departure(VisitorDepartureTerminalKind.Departed,
            GameplayOutcomeStatus.Succeeded, "theft-producer:theft-operation:1:0"),
            GameplayExperienceCategory.Routine);
        C(rows, "visitor-captive-blocked", Departure(VisitorDepartureTerminalKind.CaptiveBlocked,
            GameplayOutcomeStatus.Blocked, "none"), GameplayExperienceCategory.Mild);
        C(rows, "visitor-captive-blocked-after-theft",
            Departure(VisitorDepartureTerminalKind.CaptiveBlocked,
                GameplayOutcomeStatus.PartiallySucceeded,
                "theft-producer:theft-operation:1:0"), GameplayExperienceCategory.Mild);
        C(rows, "visitor-path-failed", Departure(VisitorDepartureTerminalKind.PathUnavailable,
            GameplayOutcomeStatus.Failed, "none"), GameplayExperienceCategory.NotExperience);
        C(rows, "visitor-path-failed-after-theft", Departure(VisitorDepartureTerminalKind.PathUnavailable,
            GameplayOutcomeStatus.PartiallySucceeded, "theft-producer:theft-operation:1:0"),
            GameplayExperienceCategory.NotExperience);
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Physical(bool quality,
        CraftsmanshipQualityTier tier, bool rejected)
    {
        var fact = B(EvolutionOutcomeIds.ApparelPhysicalCompleted.Value,
            EvolutionOutcomeIds.PhysicalTransactionOwnerRole.Value, kind: "facility");
        M(fact, EvolutionOutcomeIds.PhysicalInputMassMetric.Value, 500,
            EvolutionOutcomeIds.GramUnit.Value, "apparel", "apparel:test");
        M(fact, EvolutionOutcomeIds.PhysicalOutputMassMetric.Value, 450,
            EvolutionOutcomeIds.GramUnit.Value, "apparel", "apparel:test");
        if (quality)
        {
            M(fact, EvolutionOutcomeIds.ProductQualityTierMetric.Value, (int)tier,
                EvolutionOutcomeIds.EnumUnit.Value, "apparel", "apparel:test");
            M(fact, EvolutionOutcomeIds.ProductQualityAttemptMetric.Value, 1,
                EvolutionOutcomeIds.CountUnit.Value, "apparel", "apparel:test");
            M(fact, EvolutionOutcomeIds.ProductRejectedMetric.Value, rejected ? 1 : 0,
                EvolutionOutcomeIds.BooleanUnit.Value, "apparel", "apparel:test");
        }
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Quality(
        CraftsmanshipQualityTier tier, bool rejected)
    {
        var fact = B(EvolutionOutcomeIds.ProductQualityResolved.Value,
            EvolutionOutcomeIds.ProductMakerRole.Value);
        M(fact, EvolutionOutcomeIds.ProductQualityTierMetric.Value, (int)tier,
            EvolutionOutcomeIds.EnumUnit.Value, "equipment", "equipment:test");
        M(fact, EvolutionOutcomeIds.ProductQualityAttemptMetric.Value, 1,
            EvolutionOutcomeIds.CountUnit.Value, "equipment", "equipment:test");
        M(fact, EvolutionOutcomeIds.ProductRejectedMetric.Value, rejected ? 1 : 0,
            EvolutionOutcomeIds.BooleanUnit.Value, "equipment", "equipment:test");
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot SkillAcquired(CharacterSkillKind kind)
    {
        var fact = B(CharacterSkillAcquiredOutcomeIds.Acquired.Value,
            CharacterSkillAcquiredOutcomeIds.OwnerRole.Value);
        M(fact, CharacterSkillAcquiredOutcomeIds.BeforeRevision.Value, 2,
            CharacterSkillAcquiredOutcomeIds.RevisionUnit.Value);
        M(fact, CharacterSkillAcquiredOutcomeIds.AfterRevision.Value, 3,
            CharacterSkillAcquiredOutcomeIds.RevisionUnit.Value);
        F(fact, CharacterSkillAcquiredOutcomeIds.SkillKind.Value, kind.ToString());
        F(fact, CharacterSkillAcquiredOutcomeIds.SkillId.Value, "skill:test");
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot SkillActivation(
        string subjectRole, string children)
    {
        var fact = B(CharacterSkillActivationOutcomeIds.Activated.Value, subjectRole);
        string sourceId = subjectRole == CharacterSkillActivationOutcomeIds.RecipientRole.Value
            ? "skill-source" : "subject";
        string sourceRole = subjectRole == CharacterSkillActivationOutcomeIds.SourceRecipientRole.Value
            ? CharacterSkillActivationOutcomeIds.SourceRecipientRole.Value
            : CharacterSkillActivationOutcomeIds.SourceRole.Value;
        if (sourceId != "subject")
            P(fact, sourceRole, "character", sourceId);
        M(fact, CharacterSkillActivationOutcomeIds.OwnerRevisionBefore.Value, 4,
            CharacterSkillActivationOutcomeIds.RevisionUnit.Value, "character", sourceId);
        M(fact, CharacterSkillActivationOutcomeIds.OwnerRevisionAfter.Value, 5,
            CharacterSkillActivationOutcomeIds.RevisionUnit.Value, "character", sourceId);
        F(fact, CharacterSkillActivationOutcomeIds.ActivationKind.Value,
            CharacterSkillActivationKind.ManualDungeon.ToString());
        F(fact, CharacterSkillActivationOutcomeIds.SkillId.Value, "skill:test");
        F(fact, CharacterSkillActivationOutcomeIds.ChildResultKeys.Value, children);
        F(fact, CharacterSkillActivationOutcomeIds.RecipientCount.Value, "1");
        F(fact, CharacterSkillActivationOutcomeIds.RecipientStateChunkCount.Value, "1");
        F(fact, CharacterSkillActivationOutcomeIds.RecipientStateChunk(0).Value,
            "skill-recipient-states-v1;1;state");
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Ambition(
        CharacterAmbitionStatus before, CharacterAmbitionStatus after, string reason)
    {
        var fact = B(CharacterAmbitionTransitionOutcomeIds.Transitioned.Value,
            CharacterAmbitionTransitionOutcomeIds.OwnerRole.Value);
        M(fact, CharacterAmbitionTransitionOutcomeIds.ProgressBefore.Value, 0,
            CharacterAmbitionTransitionOutcomeIds.ProgressUnit.Value);
        M(fact, CharacterAmbitionTransitionOutcomeIds.ProgressAfter.Value, 0,
            CharacterAmbitionTransitionOutcomeIds.ProgressUnit.Value);
        F(fact, CharacterAmbitionTransitionOutcomeIds.BeforeStatus.Value, before.ToString());
        F(fact, CharacterAmbitionTransitionOutcomeIds.AfterStatus.Value, after.ToString());
        F(fact, CharacterAmbitionTransitionOutcomeIds.Reason.Value, reason);
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot AgeDamage(
        double before, double after, double damage)
    {
        var fact = B(AgeConditionAnatomyOutcomeIds.Applied.Value,
            AgeConditionAnatomyOutcomeIds.PatientRole.Value);
        M(fact, AgeConditionAnatomyOutcomeIds.HealthBefore.Value, before,
            AgeConditionAnatomyOutcomeIds.HealthUnit.Value);
        M(fact, AgeConditionAnatomyOutcomeIds.HealthAfter.Value, after,
            AgeConditionAnatomyOutcomeIds.HealthUnit.Value);
        M(fact, AgeConditionAnatomyOutcomeIds.DamageApplied.Value, damage,
            AgeConditionAnatomyOutcomeIds.HealthUnit.Value);
        M(fact, AgeConditionAnatomyOutcomeIds.NodeCount.Value, 1,
            AgeConditionAnatomyOutcomeIds.CountUnit.Value);
        F(fact, AgeConditionAnatomyOutcomeIds.ConditionId.Value, "age-condition:test");
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Reproduction(string role,
        string transition, string before, string after, string failure,
        string kind = "character")
    {
        var fact = B(ReproductionProcessTransitionOutcomeIds.Transitioned.Value,
            role, after == "failed" ? GameplayOutcomeStatus.Failed
                : after.StartsWith("waiting-", StringComparison.Ordinal)
                    ? GameplayOutcomeStatus.PartiallySucceeded
                    : GameplayOutcomeStatus.Succeeded,
            kind);
        M(fact, ReproductionProcessTransitionOutcomeIds.RevisionBefore.Value, 8,
            ReproductionProcessTransitionOutcomeIds.RevisionUnit.Value,
            "character", "genetic-parent");
        M(fact, ReproductionProcessTransitionOutcomeIds.RevisionAfter.Value, 9,
            ReproductionProcessTransitionOutcomeIds.RevisionUnit.Value,
            "character", "genetic-parent");
        F(fact, ReproductionProcessTransitionOutcomeIds.TransitionKind.Value, transition);
        F(fact, ReproductionProcessTransitionOutcomeIds.StatusBefore.Value, before);
        F(fact, ReproductionProcessTransitionOutcomeIds.StatusAfter.Value, after);
        F(fact, ReproductionProcessTransitionOutcomeIds.FailureBefore.Value, "none");
        F(fact, ReproductionProcessTransitionOutcomeIds.FailureAfter.Value, failure);
        F(fact, ReproductionProcessTransitionOutcomeIds.Mode.Value, ReproductionMode.Pregnancy.ToString());
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot MemoryErasure(
        MemoryErasureSealUseStatus status, double before, double after, int consumed,
        GameplayOutcomeStatus outcomeStatus)
    {
        var fact = B(EvolutionOutcomeIds.MemoryErasureTerminal.Value,
            EvolutionOutcomeIds.ErasureTargetRole.Value, outcomeStatus);
        M(fact, EvolutionOutcomeIds.MemoryErasureStatusMetric.Value, (int)status,
            EvolutionOutcomeIds.EnumUnit.Value);
        M(fact, EvolutionOutcomeIds.PreviousRevisionMetric.Value, before,
            EvolutionOutcomeIds.RevisionUnit.Value);
        M(fact, EvolutionOutcomeIds.CommittedRevisionMetric.Value, after,
            EvolutionOutcomeIds.RevisionUnit.Value);
        M(fact, EvolutionOutcomeIds.ConsumedSealMetric.Value, consumed,
            EvolutionOutcomeIds.CountUnit.Value);
        F(fact, EvolutionOutcomeIds.TraitInstanceFact.Value, "trait-instance:test");
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Fasting(string transition,
        CharacterRitualFastPhase before, CharacterRitualFastPhase after,
        int startBefore, int startAfter)
    {
        var fact = B(CharacterRitualFastingOutcomeIds.Transitioned.Value,
            CharacterRitualFastingOutcomeIds.Faster.Value);
        M(fact, CharacterRitualFastingOutcomeIds.Metrics[0].Value, startBefore,
            CharacterRitualFastingOutcomeIds.Day.Value);
        M(fact, CharacterRitualFastingOutcomeIds.Metrics[1].Value, startAfter,
            CharacterRitualFastingOutcomeIds.Day.Value);
        M(fact, CharacterRitualFastingOutcomeIds.Metrics[2].Value, 50,
            CharacterRitualFastingOutcomeIds.Mood.Value);
        M(fact, CharacterRitualFastingOutcomeIds.Metrics[3].Value, 50,
            CharacterRitualFastingOutcomeIds.Mood.Value);
        F(fact, CharacterRitualFastingOutcomeIds.Facts[0].Value, transition);
        F(fact, CharacterRitualFastingOutcomeIds.Facts[3].Value, before.ToString());
        F(fact, CharacterRitualFastingOutcomeIds.Facts[4].Value, after.ToString());
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Conflict(double mood, bool venue)
    {
        var fact = B(SocialLifeOutcomeIds.ConflictResolved.Value,
            venue ? SocialLifeOutcomeIds.VenueRole.Value : SocialLifeOutcomeIds.TargetRole.Value,
            kind: venue ? "facility" : "character");
        string targetId = venue ? "conflict-target" : "subject";
        if (venue)
            P(fact, SocialLifeOutcomeIds.TargetRole.Value, "character", targetId);
        M(fact, SocialLifeOutcomeIds.MoodDeltaMetric.Value, mood,
            SocialLifeOutcomeIds.PointUnit.Value, "character", targetId);
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot ConflictInstigator(double mood)
    {
        var fact = B(SocialLifeOutcomeIds.ConflictResolved.Value,
            SocialLifeOutcomeIds.InstigatorRole.Value);
        P(fact, SocialLifeOutcomeIds.CustomerRole.Value, "character", "subject");
        P(fact, SocialLifeOutcomeIds.TargetRole.Value, "character", "conflict-target");
        M(fact, SocialLifeOutcomeIds.MoodDeltaMetric.Value, mood,
            SocialLifeOutcomeIds.PointUnit.Value, "character", "conflict-target");
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Apology(double mood,
        string subjectRole = null)
    {
        subjectRole ??= SocialLifeOutcomeIds.OffenderRole.Value;
        var fact = B(SocialLifeOutcomeIds.ApologyResolved.Value,
            subjectRole);
        string recipientId = subjectRole == SocialLifeOutcomeIds.RecipientRole.Value
            ? "subject" : "apology-recipient";
        if (recipientId != "subject")
            P(fact, SocialLifeOutcomeIds.RecipientRole.Value, "character", recipientId);
        M(fact, SocialLifeOutcomeIds.MoodDeltaMetric.Value, mood,
            SocialLifeOutcomeIds.PointUnit.Value, "character", recipientId);
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Idle(bool actor,
        double moodDelta, double relationshipDelta)
    {
        var fact = B(IdleSocialInteractionOutcomeIds.OutcomeType.Value,
            actor ? IdleSocialInteractionOutcomeIds.ActorRole.Value
                : IdleSocialInteractionOutcomeIds.TargetRole.Value);
        if (actor)
        {
            M(fact, IdleSocialInteractionOutcomeIds.ActorRelationshipBefore.Value, 10,
                IdleSocialInteractionOutcomeIds.PointUnit.Value);
            M(fact, IdleSocialInteractionOutcomeIds.ActorRelationshipAfter.Value,
                10 + relationshipDelta, IdleSocialInteractionOutcomeIds.PointUnit.Value);
        }
        else
        {
            M(fact, IdleSocialInteractionOutcomeIds.TargetRelationshipBefore.Value, 10,
                IdleSocialInteractionOutcomeIds.PointUnit.Value);
            M(fact, IdleSocialInteractionOutcomeIds.TargetRelationshipAfter.Value,
                10 + relationshipDelta, IdleSocialInteractionOutcomeIds.PointUnit.Value);
            M(fact, IdleSocialInteractionOutcomeIds.MoodBefore.Value, 50,
                IdleSocialInteractionOutcomeIds.PointUnit.Value);
            M(fact, IdleSocialInteractionOutcomeIds.MoodAfter.Value,
                50 + moodDelta, IdleSocialInteractionOutcomeIds.PointUnit.Value);
        }
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot StaffResponse(
        StaffRebellionResponseType response, string role)
    {
        var fact = B(StaffDiscontentOutcomeIds.ResponseResolved.Value, role);
        M(fact, StaffDiscontentOutcomeIds.ResponseTypeMetric.Value, (int)response,
            StaffDiscontentOutcomeIds.EnumUnit.Value,
            "staff-response-kind", "staff-response:test");
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot StaffTransition(
        StaffDiscontentOutcome outcome)
    {
        var fact = B(StaffDiscontentTransitionOutcomeIds.Transitioned.Value,
            StaffDiscontentTransitionOutcomeIds.StaffRole.Value);
        M(fact, StaffDiscontentTransitionOutcomeIds.OutcomeMetric.Value, (int)outcome,
            StaffDiscontentTransitionOutcomeIds.EnumUnit.Value,
            "staff-discontent-outcome", "staff-discontent:test");
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot PerformerMilestone(int threshold)
    {
        var fact = B(CaptivePerformerMilestoneOutcomeIds.Unlocked.Value,
            CaptivePerformerMilestoneOutcomeIds.CaptiveRole.Value);
        M(fact, CaptivePerformerMilestoneOutcomeIds.ThresholdMetric.Value, threshold,
            CaptivePerformerMilestoneOutcomeIds.PointUnit.Value);
        F(fact, CaptivePerformerMilestoneOutcomeIds.UnlockKindFact.Value,
            CaptivePerformerMilestoneOutcomeIds.UnlockKind(threshold));
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Interaction(string role,
        double psychologicalDelta, double damage, bool success, string kind = "character")
    {
        var fact = B(CaptivityInteractionOutcomeIds.Resolved.Value, role,
            success ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.Failed,
            kind);
        string captiveId = role == CaptivityInteractionOutcomeIds.CaptiveRole.Value
            ? "subject" : "interaction-captive";
        if (captiveId != "subject")
            P(fact, CaptivityInteractionOutcomeIds.CaptiveRole.Value, "character", captiveId);
        string[] metrics =
        {
            CaptivityInteractionOutcomeIds.WillDeltaMetric.Value,
            CaptivityInteractionOutcomeIds.FearDeltaMetric.Value,
            CaptivityInteractionOutcomeIds.TrustDeltaMetric.Value,
            CaptivityInteractionOutcomeIds.GrudgeDeltaMetric.Value,
            CaptivityInteractionOutcomeIds.CorruptionDeltaMetric.Value
        };
        for (int index = 0; index < metrics.Length; index++)
            M(fact, metrics[index], success && index == 0 ? psychologicalDelta : 0,
                CaptivityInteractionOutcomeIds.PointUnit.Value, "character", captiveId);
        double before = damage > 0 ? 100 : 100;
        double after = before - damage;
        M(fact, CaptivityInteractionOutcomeIds.BodyDamageMetric.Value,
            success ? damage : 0, CaptivityInteractionOutcomeIds.HealthUnit.Value,
            "character", captiveId);
        M(fact, CaptivityInteractionOutcomeIds.BodyHealthBeforeMetric.Value, before,
            CaptivityInteractionOutcomeIds.HealthUnit.Value, "character", captiveId);
        M(fact, CaptivityInteractionOutcomeIds.BodyHealthAfterMetric.Value,
            success ? after : before, CaptivityInteractionOutcomeIds.HealthUnit.Value,
            "character", captiveId);
        F(fact, CaptivityInteractionOutcomeIds.SuccessFact.Value, success ? "1" : "0");
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Rehabilitation(
        string role, bool completed, string kind = "character")
    {
        var fact = B(CaptivityTransitionOutcomeIds.Rehabilitation.Value, role, kind: kind);
        string captiveId = role == CaptivityTransitionOutcomeIds.CaptiveRole.Value
            ? "subject" : "rehabilitation-captive";
        string wardenId = role == CaptivityTransitionOutcomeIds.WardenRole.Value
            ? "subject" : "rehabilitation-warden";
        if (captiveId != "subject")
            P(fact, CaptivityTransitionOutcomeIds.CaptiveRole.Value, "character", captiveId);
        if (wardenId != "subject")
            P(fact, CaptivityTransitionOutcomeIds.WardenRole.Value, "character", wardenId);
        M(fact, CaptivityTransitionOutcomeIds.CompletedDay.Value, completed ? 1 : 0,
            CaptivityTransitionOutcomeIds.BooleanUnit.Value, "character", captiveId);
        M(fact, CaptivityTransitionOutcomeIds.ApprovedWork.Value, 5,
            CaptivityTransitionOutcomeIds.WorkUnit.Value, "character", wardenId);
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Release(string role,
        string kind = "character")
    {
        var fact = B(CaptivityTransitionOutcomeIds.Release.Value, role, kind: kind);
        string captiveId = role == CaptivityTransitionOutcomeIds.CaptiveRole.Value
            ? "subject" : "release-captive";
        if (captiveId != "subject")
            P(fact, CaptivityTransitionOutcomeIds.CaptiveRole.Value, "character", captiveId);
        F(fact, CaptivityTransitionOutcomeIds.StatusBefore.Value, CaptivityStatus.Confined.ToString());
        F(fact, CaptivityTransitionOutcomeIds.StatusAfter.Value, CaptivityStatus.Released.ToString());
        M(fact, CaptivityTransitionOutcomeIds.CustodyBefore.Value, 1,
            CaptivityTransitionOutcomeIds.BooleanUnit.Value, "character", captiveId);
        M(fact, CaptivityTransitionOutcomeIds.CustodyAfter.Value, 0,
            CaptivityTransitionOutcomeIds.BooleanUnit.Value, "character", captiveId);
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot Departure(
        VisitorDepartureTerminalKind terminal, GameplayOutcomeStatus status, string children)
    {
        var fact = B(VisitorExitOutcomeIds.DepartureTerminal.Value,
            VisitorExitOutcomeIds.DepartingVisitorRole.Value, status);
        F(fact, VisitorExitOutcomeIds.TerminalKind.Value, terminal.ToString());
        F(fact, VisitorExitOutcomeIds.TheftChildResultKeys.Value, children);
        return fact;
    }

    private static GameplayOutcomeEvidenceBindingSnapshot B(string type, string role,
        GameplayOutcomeStatus status = GameplayOutcomeStatus.Succeeded,
        string kind = "character") => GameplayExperienceCaseFactory.Create(
        type, role, status, kind);

    private static void P(GameplayOutcomeEvidenceBindingSnapshot fact, string role,
        string kind, string id) => GameplayExperienceCaseFactory.Participant(fact, role, kind, id);

    private static void M(GameplayOutcomeEvidenceBindingSnapshot fact, string id,
        double value, string unit, string referenceKind = null, string referenceId = null) =>
        GameplayExperienceCaseFactory.Metric(fact, id, value, unit, referenceKind, referenceId);

    private static void F(GameplayOutcomeEvidenceBindingSnapshot fact, string id, string value) =>
        GameplayExperienceCaseFactory.Fact(fact, id, value);

    private static void C(List<GameplayExperiencePolicyCase> rows, string name,
        GameplayOutcomeEvidenceBindingSnapshot fact, GameplayExperienceCategory expected) =>
        rows.Add(GameplayExperienceCaseFactory.Case(name, fact, expected));
}
