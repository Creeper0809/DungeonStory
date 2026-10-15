#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class ProductionCausalPlanBatchExporter
{
    private const string CampaignPackageSchema =
        "dungeonstory-causal-gameplay-campaign-package@2";
    private const string CampaignSchema =
        "dungeonstory-causal-gameplay-campaign@2";
    private const string CampaignBatchSchema =
        "dungeonstory-causal-gameplay-campaign-batch@5";
    private const string CampaignResultSchema =
        "dungeonstory-causal-gameplay-campaign-result@5";
    private const string WorldCheckpointDigestAlgorithm =
        "dungeonstory-jtoken-binary-v1";
    private const string CampaignBatchParent =
        "Artifacts/Exports/CausalGameplayCampaignBatch";
    private const string CampaignLinkKind = "committed-outcome-ordering";
    private const string CampaignWorldTemplate =
        "dungeon-defense-social-economy";
    private const string CropIrrigationEpisodeTemplate =
        "crop-irrigation-episode";
    private const string CropHarvestEpisodeTemplate =
        "crop-harvest-episode";
    private const string CropIrrigationAction =
        "action:environment-crop-irrigation-supplied";
    private const string CropHarvestAction =
        "action:environment-crop-plan-terminal";
    private static readonly string[] FireEpisodeTemplates =
    {
        "environment-fire-ignite-episode",
        "environment-fire-initial-attack-episode",
        "environment-fire-fuel-ignition-episode",
        "environment-fire-damage-episode",
        "environment-fire-water-suppression-episode"
    };
    private static readonly string[] FireActions =
    {
        "action:environment-fire-ignite",
        "action:environment-fire-initial-attack",
        "action:environment-fire-fuel-ignition",
        "action:environment-fire-damage-tick",
        "action:environment-fire-water-suppression"
    };
    private static readonly string[] SocialEpisodeTemplates =
    {
        "social-assault-conflict-episode",
        "social-apology-episode"
    };
    private static readonly string[] SocialActions =
    {
        SocialLifeCausalProductionEntryDebugScenarios.AssaultConflictActionId,
        SocialLifeCausalProductionEntryDebugScenarios.ApologyActionId
    };
    private static readonly string[] TraitEpisodeTemplates =
    {
        "acquired-trait-manifestation-episode",
        "acquired-trait-special-reaction-episode",
        "acquired-trait-memory-erasure-episode"
    };
    private static readonly string[] TraitActions =
    {
        AcquiredTraitMemoryCausalProductionEntryDebugScenarios.ManifestActionId,
        AcquiredTraitMemoryCausalProductionEntryDebugScenarios.ReactionActionId,
        AcquiredTraitMemoryCausalProductionEntryDebugScenarios.EraseActionId
    };
    private static readonly string[] CaptivityCommonEpisodeTemplates =
    {
        "captivity-interaction-episode",
        "captivity-performer-milestone-episode"
    };
    private const string CaptivityRansomEpisodeTemplate =
        "captivity-ransom-terminal-episode";
    private const string CaptivityEscapeEpisodeTemplate =
        "captivity-escape-terminal-episode";
    private static readonly string[] CaptivityCommonActions =
    {
        CaptivityCausalProductionEntryDebugScenarios.InteractionActionId,
        CaptivityCausalProductionEntryDebugScenarios.MilestoneActionId
    };
    private static readonly string[] CaptivityActions =
    {
        CaptivityCausalProductionEntryDebugScenarios.InteractionActionId,
        CaptivityCausalProductionEntryDebugScenarios.MilestoneActionId,
        CaptivityCausalProductionEntryDebugScenarios.RansomActionId,
        CaptivityCausalProductionEntryDebugScenarios.EscapeActionId
    };
    private static readonly string[] EquipmentApparelEpisodeTemplates =
    {
        "equipment-apparel-craft-episode",
        "equipment-apparel-equip-episode"
    };
    private static readonly string[] EquipmentApparelActions =
    {
        EquipmentApparelCausalProductionEntryDebugScenarios.CraftActionId,
        EquipmentApparelCausalProductionEntryDebugScenarios.EquipActionId
    };
    private static readonly string[] ResearchKnowledgeEpisodeTemplates =
    {
        "research-knowledge-progress-episode",
        "research-knowledge-completion-episode"
    };
    private static readonly string[] ResearchKnowledgeActions =
    {
        KnowledgeResearchOutcomeDebugScenarios.CausalResearchProgressActionId,
        KnowledgeResearchOutcomeDebugScenarios.CausalResearchCompletionActionId
    };
    private static readonly string[] ProductionBillEpisodeTemplates =
    {
        "production-bill-command-episode",
        "production-bill-completion-episode"
    };
    private static readonly string[] ProductionBillActions =
    {
        ProductionPreparedOutputFullPersistenceDebugScenarios
            .CausalProductionCommandActionId,
        ProductionPreparedOutputFullPersistenceDebugScenarios
            .CausalProductionCompletionActionId
    };
    private static readonly string[] FacilityEvolutionEpisodeTemplates =
    {
        "facility-evolution-stage-one-episode",
        "facility-evolution-stage-two-episode"
    };
    private static readonly string[] FacilityEvolutionActions =
    {
        FacilityEvolutionDebugScenarios.CausalFacilityEvolutionFirstActionId,
        FacilityEvolutionDebugScenarios.CausalFacilityEvolutionSecondActionId
    };
    private static readonly string[] FacilitySynthesisEpisodeTemplates =
    {
        "facility-synthesis-patrol-board-episode",
        "facility-synthesis-battle-banner-episode"
    };
    private static readonly string[] FacilitySynthesisActions =
    {
        FacilitySynthesisDebugScenarios.CausalSynthesisFirstActionId,
        FacilitySynthesisDebugScenarios.CausalSynthesisSecondActionId
    };
    private static readonly string[] InfrastructurePowerEpisodeTemplates =
    {
        "infrastructure-power-disconnect-episode",
        "infrastructure-power-reconnect-episode"
    };
    private static readonly string[] InfrastructurePowerActions =
    {
        InfrastructureCommandCausalProductionEntryDebugScenarios
            .DisconnectActionId,
        InfrastructureCommandCausalProductionEntryDebugScenarios
            .ReconnectActionId
    };
    private static readonly string[] DungeonSpaceEpisodeTemplates =
    {
        "dungeon-space-quarry-episode",
        "dungeon-space-stonecutting-episode"
    };
    private static readonly string[] DungeonSpaceActions =
    {
        DungeonSpaceExpansionCausalProductionEntryDebugScenarios
            .QuarryActionId,
        DungeonSpaceExpansionCausalProductionEntryDebugScenarios
            .StonecuttingActionId
    };
    private static readonly string[] RoomEnvironmentEpisodeTemplates =
    {
        "room-environment-experience-episode",
        "room-environment-condition-episode"
    };
    private static readonly string[] RoomEnvironmentActions =
    {
        RoomEnvironmentCausalProductionEntryDebugScenarios.ExperienceActionId,
        RoomEnvironmentCausalProductionEntryDebugScenarios.ConditionActionId
    };
    private static readonly string[] SpeciesIncidentEpisodeTemplates =
    {
        "species-golem-overload-first-episode",
        "species-golem-overload-repeat-episode"
    };
    private static readonly string[] SpeciesIncidentActions =
    {
        EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
            .SpeciesFirstIncidentActionId,
        EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
            .SpeciesRepeatIncidentActionId
    };
    private static readonly string[] CertifiedSeedEpisodeTemplates =
    {
        "certified-seed-first-completion-episode",
        "certified-seed-repeat-completion-episode"
    };
    private static readonly string[] CertifiedSeedActions =
    {
        EnvironmentCropProductionEntryDebugScenarios
            .CertifiedSeedFirstCompletionActionId,
        EnvironmentCropProductionEntryDebugScenarios
            .CertifiedSeedRepeatCompletionActionId
    };
    private static readonly string[] StaffResponseEpisodeTemplates =
    {
        "staff-rebellion-isolation-episode",
        "staff-discontent-calming-episode"
    };
    private static readonly string[] StaffResponseActions =
    {
        StaffRebellionResponseDebugScenarios.CausalIsolateActionId,
        StaffRebellionResponseDebugScenarios.CausalCalmActionId
    };
    private static readonly string[] EmergencySuspensionEpisodeTemplates =
    {
        "emergency-work-suspension-first-episode",
        "emergency-work-suspension-second-episode"
    };
    private static readonly string[] EmergencySuspensionActions =
    {
        EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
            .FirstSuspensionActionId,
        EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
            .SecondSuspensionActionId
    };
    private static readonly string[] BossAwardEpisodeTemplates =
    {
        "memory-erasure-first-boss-award-episode",
        "memory-erasure-second-boss-award-episode"
    };
    private static readonly string[] BossAwardActions =
    {
        MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
            .FirstAwardActionId,
        MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
            .SecondAwardActionId
    };
    private static readonly string[] TradeInventoryEpisodeTemplates =
    {
        "trade-inventory-relocation-episode",
        "trade-inventory-disposition-episode",
        "trade-inventory-waste-policy-episode"
    };
    private static readonly string[] TradeInventoryActions =
    {
        TradeInventoryCausalProductionEntryDebugScenarios.RelocateActionId,
        TradeInventoryCausalProductionEntryDebugScenarios.DisposeActionId,
        TradeInventoryCausalProductionEntryDebugScenarios.WastePolicyActionId
    };
    private static readonly string[] DiseaseRouteEpisodeTemplates =
    {
        "disease-route-first-exposure-episode",
        "disease-route-repeat-exposure-episode"
    };
    private static readonly string[] DiseaseRouteActions =
    {
        DiseaseRouteExposureCausalProductionEntryDebugScenarios
            .FirstExposureActionId,
        DiseaseRouteExposureCausalProductionEntryDebugScenarios
            .RepeatExposureActionId
    };
    private static readonly string[] OffenseTruthEpisodeTemplates =
    {
        "offense-truth-preliminary-victory-episode",
        "offense-truth-final-reveal-episode"
    };
    private static readonly string[] OffenseTruthActions =
    {
        OffenseTruthRevealCausalProductionEntryDebugScenarios
            .PreliminaryVictoryActionId,
        OffenseTruthRevealCausalProductionEntryDebugScenarios
            .RevealTruthActionId
    };
    private static readonly string[] OffenseNodeResolutionEpisodeTemplates =
    {
        "offense-node-clear-approach-episode",
        "offense-node-secure-cache-episode"
    };
    private static readonly string[] OffenseNodeResolutionActions =
    {
        OffenseNodeResolutionCausalProductionEntryDebugScenarios
            .ClearApproachActionId,
        OffenseNodeResolutionCausalProductionEntryDebugScenarios
            .SecureCacheActionId
    };
    private static readonly string[] WorkIdentityEpisodeTemplates =
    {
        "work-identity-first-harvest-episode",
        "work-identity-next-day-harvest-episode"
    };
    private static readonly string[] WorkIdentityActions =
    {
        WorkCompletionIdentityCausalProductionEntryDebugScenarios
            .FirstHarvestActionId,
        WorkCompletionIdentityCausalProductionEntryDebugScenarios
            .NextDayHarvestActionId
    };
    private static readonly string[] RunVariableEpisodeTemplates =
    {
        "run-operation-variable-activate-episode",
        "run-operation-variable-expire-episode"
    };
    private static readonly string[] RunVariableActions =
    {
        RunVariableCausalProductionEntryDebugScenarios
            .ActivateActionId,
        RunVariableCausalProductionEntryDebugScenarios
            .ExpireActionId
    };
    private static readonly string[] OwnerRunCompletionEpisodeTemplates =
    {
        "owner-run-completion-episode-0", "owner-run-completion-episode-1"
    };
    private static readonly string[] OwnerRunCompletionActions =
    {
        OwnerRunCompletionCausalProductionEntryDebugScenarios.CompleteActionId,
        OwnerRunCompletionCausalProductionEntryDebugScenarios.UpgradeActionId
    };
    private static readonly string[] VisitorReceptionCheckoutEpisodeTemplates =
    {
        "visitor-reception-checkout-episode-0",
        "visitor-reception-checkout-episode-1"
    };
    private static readonly string[] VisitorReceptionCheckoutActions =
    {
        VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.ReceptionActionId,
        VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.CheckoutActionId
    };
    private static readonly string[] TabooButcheryEpisodeTemplates =
    {
        "taboo-butchery-episode-0",
        "taboo-butchery-episode-1"
    };
    private static readonly string[] TabooButcheryActions =
    {
        TabooButcheryCausalProductionEntryDebugScenarios.ControlledDeathActionId,
        TabooButcheryCausalProductionEntryDebugScenarios.ButcherCorpseActionId
    };
    private static readonly string[] OffenseArrivalEpisodeTemplates =
    {
        "offense-special-wildlife-arrival-episode-0",
        "offense-special-wildlife-arrival-episode-1"
    };
    private static readonly string[] OffenseArrivalActions =
    {
        OffenseArrivalCausalProductionEntryDebugScenarios.FinalizeRewardActionId,
        OffenseArrivalCausalProductionEntryDebugScenarios.SettleArrivalActionId
    };
    private static readonly string[] FestivalCelebrationEpisodeTemplates =
    {
        "festival-long-night-memorial-episode-0",
        "festival-long-night-memorial-episode-1"
    };
    private static readonly string[] FestivalCelebrationActions =
    {
        FestivalCelebrationCausalProductionEntryDebugScenarios.FirstCelebrationActionId,
        FestivalCelebrationCausalProductionEntryDebugScenarios.SecondCelebrationActionId
    };
    private static readonly string[] MetaProgressionEpisodeTemplates =
    {
        "meta-run-result-ready-episode",
        "meta-upgrade-purchased-episode"
    };
    private static readonly string[] MetaProgressionActions =
    {
        MetaProgressionCausalProductionEntryDebugScenarios
            .RunResultActionId,
        MetaProgressionCausalProductionEntryDebugScenarios
            .UpgradeActionId
    };
    private static readonly string[] RegularCustomerEpisodeTemplates =
    {
        "regular-customer-first-visit-episode",
        "regular-customer-candidate-recruit-episode"
    };
    private static readonly string[] RegularCustomerActions =
    {
        RegularCustomerCausalProductionEntryDebugScenarios.FirstVisitActionId,
        RegularCustomerCausalProductionEntryDebugScenarios.CandidateVisitActionId,
        RegularCustomerCausalProductionEntryDebugScenarios.RecruitActionId
    };
    private static readonly string[] CharacterLifeEpisodeTemplates =
    {
        "character-age-condition-episode",
        "character-life-stage-episode"
    };
    private static readonly string[] CharacterLifeActions =
    {
        CharacterLifeCausalProductionEntryDebugScenarios.AgeConditionActionId,
        CharacterLifeCausalProductionEntryDebugScenarios.LifeStageActionId
    };
    private static readonly string[] FactionRouteEpisodeTemplates =
    {
        "faction-route-settlement-episode",
        "faction-route-arrival-episode",
        "faction-route-cargo-episode"
    };
    private static readonly string[] FactionRouteActions =
    {
        FactionRouteCausalProductionEntryDebugScenarios.SettlementActionId,
        FactionRouteCausalProductionEntryDebugScenarios.ArrivalActionId,
        FactionRouteCausalProductionEntryDebugScenarios.CargoActionId
    };
    private static readonly string[] ConsumablesEpisodeTemplates =
    {
        "character-consumables-meal-episode",
        "character-consumables-substance-episode"
    };
    private static readonly string[] ConsumablesActions =
    {
        ConsumablesProgressionCausalProductionEntryDebugScenarios.MealActionId,
        ConsumablesProgressionCausalProductionEntryDebugScenarios
            .SubstanceActionId
    };
    private static readonly string[] MealShortageEpisodeTemplates =
    {
        "survival-meal-shortage-first-episode",
        "survival-meal-shortage-repeat-episode"
    };
    private static readonly string[] MealShortageActions =
    {
        MealShortageCausalProductionEntryDebugScenarios.FirstMissActionId,
        MealShortageCausalProductionEntryDebugScenarios.RepeatMissActionId
    };
    private static readonly string[] PrimitiveSurvivalEpisodeTemplates =
    {
        "survival-primitive-rest-first-episode",
        "survival-primitive-rest-repeat-episode"
    };
    private static readonly string[] PrimitiveSurvivalActions =
    {
        PrimitiveSurvivalCausalProductionEntryDebugScenarios.FirstRestActionId,
        PrimitiveSurvivalCausalProductionEntryDebugScenarios.RepeatRestActionId
    };
    private static readonly string[] ServiceHubDayEpisodeTemplates =
    {
        "service-hub-visit-episode",
        "service-hub-mode-episode",
        "service-hub-close-day-episode"
    };
    private static readonly string[] ServiceHubDayActions =
    {
        ServiceHubDayCausalProductionEntryDebugScenarios.VisitActionId,
        ServiceHubDayCausalProductionEntryDebugScenarios.ModeActionId,
        ServiceHubDayCausalProductionEntryDebugScenarios.CloseDayActionId
    };
    private static readonly string[] InvasionLifecycleEpisodeTemplates =
    {
        "invasion-lifecycle-start-episode",
        "invasion-lifecycle-breach-episode",
        "invasion-lifecycle-damage-episode",
        "invasion-lifecycle-front-collapse-episode",
        "invasion-lifecycle-resolve-episode"
    };
    private static readonly string[] InvasionLifecycleActions =
    {
        MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .InvasionStartActionId,
        MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .InvasionBreachActionId,
        MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .InvasionDamageActionId,
        MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .InvasionFrontCollapseActionId,
        MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .InvasionResolveActionId
    };
    private static readonly string[] CombatLifeCycleEpisodeTemplates =
    {
        "combat-life-cycle-injury-episode",
        "combat-life-cycle-recovery-episode",
        "combat-life-cycle-fatal-episode"
    };
    private static readonly string[] CombatLifeCycleActions =
    {
        MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .NonlethalCombatInjuryActionId,
        MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .RecoverBodyHealthActionId,
        MigratedProducerCombatDefenseEntryE2EDebugScenarios
            .FatalCombatInjuryActionId
    };
    private static readonly string[] StockSupplyTransformEpisodeTemplates =
    {
        "stock-supply-purchase-episode",
        "stock-supply-transform-episode"
    };
    private static readonly string[] StockSupplyTransformActions =
    {
        StockSupplyTransformCausalProductionEntryDebugScenarios
            .SupplyActionId,
        StockSupplyTransformCausalProductionEntryDebugScenarios
            .TransformActionId
    };
    private static readonly string[] FacilityWishlistProcurementEpisodeTemplates =
    {
        "facility-wishlist-budget-blocked-episode",
        "facility-wishlist-purchase-episode"
    };
    private static readonly string[] FacilityWishlistProcurementActions =
    {
        FacilityWishlistProcurementCausalProductionEntryDebugScenarios
            .BudgetBlockedActionId,
        FacilityWishlistProcurementCausalProductionEntryDebugScenarios
            .PurchaseWishlistActionId
    };
    private static readonly string[] ObservedCareerArcEpisodeTemplates =
    {
        "observed-career-mentorship-award-episode",
        "observed-career-last-lesson-episode",
        "observed-career-promotion-episode",
        "observed-career-funeral-episode",
        "observed-career-lineage-compression-episode"
    };
    private static readonly string[] ObservedCareerArcActions =
    {
        ObservedCareerArcCausalProductionEntryDebugScenarios
            .MentorshipAwardActionId,
        ObservedCareerArcCausalProductionEntryDebugScenarios
            .LastLessonActionId,
        ObservedCareerArcCausalProductionEntryDebugScenarios
            .QuietPromotionActionId,
        ObservedCareerArcCausalProductionEntryDebugScenarios
            .FuneralActionId,
        ObservedCareerArcCausalProductionEntryDebugScenarios
            .LineageCompressionActionId
    };
    private static readonly string[] ObservedMealIncidentResolutionEpisodeTemplates =
    {
        "observed-meal-incident-episode",
        "observed-meal-resolution-episode"
    };
    private static readonly string[] ObservedMealIncidentResolutionActions =
    {
        ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
            .ObserveIncidentActionId,
        ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
            .ResolveIncidentActionId
    };
    private static readonly HashSet<string> PlainFireActionIds = new(
        FireActions.Take(2),
        StringComparer.Ordinal);

    private static readonly HashSet<string> CampaignProtagonistTemplates = new(
        new[]
        {
            "protagonist-injury-recovery-growth",
            "detox-and-recovery-routine"
        },
        StringComparer.Ordinal);

    private static readonly HashSet<string> ProtagonistActionIds = new(
        new[]
        {
            "action:receive-nonlethal-combat-injury",
            "action:recover-body-health",
            "action:drink-clean-water",
            "action:rest",
            "action:train-proficiency"
        },
        StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string>
        PersistentFixtureOwnerFamilies =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["action:trigger-defense-facility"] =
                "owner-family:defense-facility",
            ["action:adjust-faction-trust"] =
                "owner-family:faction-relationship",
            ["action:purchase-retail-item"] =
                "owner-family:retail-facility",
            ["action:environment-crop-irrigation-supplied"] =
                EnvironmentCropProductionEntryDebugScenarios
                    .CausalCropOwnerRestoreKind,
            ["action:environment-crop-plan-terminal"] =
                EnvironmentCropProductionEntryDebugScenarios
                    .CausalCropOwnerRestoreKind,
            ["action:environment-fire-ignite"] =
                EnvironmentFirePhysicalProductionEntryDebugScenarios
                    .CausalFireOwnerRestoreKind,
            ["action:environment-fire-initial-attack"] =
                EnvironmentFirePhysicalProductionEntryDebugScenarios
                    .CausalFireOwnerRestoreKind,
            ["action:environment-fire-fuel-ignition"] =
                EnvironmentFirePhysicalProductionEntryDebugScenarios
                    .CausalFireOwnerRestoreKind,
            ["action:environment-fire-damage-tick"] =
                EnvironmentFirePhysicalProductionEntryDebugScenarios
                    .CausalFireOwnerRestoreKind,
            ["action:environment-fire-water-suppression"] =
                EnvironmentFirePhysicalProductionEntryDebugScenarios
                    .CausalFireOwnerRestoreKind,
            [SocialLifeCausalProductionEntryDebugScenarios
                .AssaultConflictActionId] =
                SocialLifeCausalProductionEntryDebugScenarios
                    .CausalSocialOwnerRestoreKind,
            [SocialLifeCausalProductionEntryDebugScenarios.ApologyActionId] =
                SocialLifeCausalProductionEntryDebugScenarios
                    .CausalSocialOwnerRestoreKind,
            [AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                .ManifestActionId] =
                AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                .ReactionActionId] =
                AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                .EraseActionId] =
                AcquiredTraitMemoryCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [CaptivityCausalProductionEntryDebugScenarios.InteractionActionId] =
                CaptivityCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [CaptivityCausalProductionEntryDebugScenarios.MilestoneActionId] =
                CaptivityCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [CaptivityCausalProductionEntryDebugScenarios.RansomActionId] =
                CaptivityCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [CaptivityCausalProductionEntryDebugScenarios.EscapeActionId] =
                CaptivityCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [EquipmentApparelCausalProductionEntryDebugScenarios.CraftActionId] =
                EquipmentApparelCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [EquipmentApparelCausalProductionEntryDebugScenarios.EquipActionId] =
                EquipmentApparelCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [KnowledgeResearchOutcomeDebugScenarios
                .CausalResearchProgressActionId] =
                KnowledgeResearchOutcomeDebugScenarios
                    .CausalResearchOwnerRestoreKind,
            [KnowledgeResearchOutcomeDebugScenarios
                .CausalResearchCompletionActionId] =
                KnowledgeResearchOutcomeDebugScenarios
                    .CausalResearchOwnerRestoreKind,
            [ProductionPreparedOutputFullPersistenceDebugScenarios
                .CausalProductionCommandActionId] =
                ProductionPreparedOutputFullPersistenceDebugScenarios
                    .CausalProductionOwnerRestoreKind,
            [ProductionPreparedOutputFullPersistenceDebugScenarios
                .CausalProductionCompletionActionId] =
                ProductionPreparedOutputFullPersistenceDebugScenarios
                    .CausalProductionOwnerRestoreKind,
            [FacilityEvolutionDebugScenarios
                .CausalFacilityEvolutionFirstActionId] =
                FacilityEvolutionDebugScenarios
                    .CausalFacilityEvolutionOwnerRestoreKind,
            [FacilityEvolutionDebugScenarios
                .CausalFacilityEvolutionSecondActionId] =
                FacilityEvolutionDebugScenarios
                    .CausalFacilityEvolutionOwnerRestoreKind,
            [FacilitySynthesisDebugScenarios
                .CausalSynthesisFirstActionId] =
                FacilitySynthesisDebugScenarios
                    .CausalSynthesisOwnerRestoreKind,
            [FacilitySynthesisDebugScenarios
                .CausalSynthesisSecondActionId] =
                FacilitySynthesisDebugScenarios
                    .CausalSynthesisOwnerRestoreKind,
            [InfrastructureCommandCausalProductionEntryDebugScenarios
                .DisconnectActionId] =
                InfrastructureCommandCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [InfrastructureCommandCausalProductionEntryDebugScenarios
                .ReconnectActionId] =
                InfrastructureCommandCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                .QuarryActionId] =
                DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                .StonecuttingActionId] =
                DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [RoomEnvironmentCausalProductionEntryDebugScenarios
                .ExperienceActionId] =
                RoomEnvironmentCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [RoomEnvironmentCausalProductionEntryDebugScenarios
                .ConditionActionId] =
                RoomEnvironmentCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                .SpeciesFirstIncidentActionId] =
                EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                    .SpeciesCausalOwnerRestoreKind,
            [EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                .SpeciesRepeatIncidentActionId] =
                EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                    .SpeciesCausalOwnerRestoreKind,
            [EnvironmentCropProductionEntryDebugScenarios
                .CertifiedSeedFirstCompletionActionId] =
                EnvironmentCropProductionEntryDebugScenarios
                    .CertifiedSeedCausalOwnerRestoreKind,
            [EnvironmentCropProductionEntryDebugScenarios
                .CertifiedSeedRepeatCompletionActionId] =
                EnvironmentCropProductionEntryDebugScenarios
                    .CertifiedSeedCausalOwnerRestoreKind,
            [StaffRebellionResponseDebugScenarios.CausalIsolateActionId] =
                StaffRebellionResponseDebugScenarios.CausalOwnerRestoreKind,
            [StaffRebellionResponseDebugScenarios.CausalCalmActionId] =
                StaffRebellionResponseDebugScenarios.CausalOwnerRestoreKind,
            [EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                .FirstSuspensionActionId] =
                EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                .SecondSuspensionActionId] =
                EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                .FirstAwardActionId] =
                MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                .SecondAwardActionId] =
                MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [TradeInventoryCausalProductionEntryDebugScenarios
                .RelocateActionId] =
                TradeInventoryCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [TradeInventoryCausalProductionEntryDebugScenarios
                .DisposeActionId] =
                TradeInventoryCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [TradeInventoryCausalProductionEntryDebugScenarios
                .WastePolicyActionId] =
                TradeInventoryCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [DiseaseRouteExposureCausalProductionEntryDebugScenarios
                .FirstExposureActionId] =
                DiseaseRouteExposureCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [DiseaseRouteExposureCausalProductionEntryDebugScenarios
                .RepeatExposureActionId] =
                DiseaseRouteExposureCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [OffenseTruthRevealCausalProductionEntryDebugScenarios
                .PreliminaryVictoryActionId] =
                OffenseTruthRevealCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [OffenseTruthRevealCausalProductionEntryDebugScenarios
                .RevealTruthActionId] =
                OffenseTruthRevealCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [OffenseNodeResolutionCausalProductionEntryDebugScenarios
                .ClearApproachActionId] =
                OffenseNodeResolutionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [OffenseNodeResolutionCausalProductionEntryDebugScenarios
                .SecureCacheActionId] =
                OffenseNodeResolutionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [WorkCompletionIdentityCausalProductionEntryDebugScenarios
                .FirstHarvestActionId] =
                WorkCompletionIdentityCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [WorkCompletionIdentityCausalProductionEntryDebugScenarios
                .NextDayHarvestActionId] =
                WorkCompletionIdentityCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [RunVariableCausalProductionEntryDebugScenarios
                .ActivateActionId] =
                RunVariableCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [RunVariableCausalProductionEntryDebugScenarios
                .ExpireActionId] =
                RunVariableCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [OwnerRunCompletionCausalProductionEntryDebugScenarios.CompleteActionId] =
                OwnerRunCompletionCausalProductionEntryDebugScenarios.RestoreKind,
            [OwnerRunCompletionCausalProductionEntryDebugScenarios.UpgradeActionId] =
                OwnerRunCompletionCausalProductionEntryDebugScenarios.RestoreKind,
            [VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.ReceptionActionId] =
                VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.RestoreKind,
            [VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.CheckoutActionId] =
                VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.RestoreKind,
            [TabooButcheryCausalProductionEntryDebugScenarios.ControlledDeathActionId] =
                TabooButcheryCausalProductionEntryDebugScenarios.RestoreKind,
            [TabooButcheryCausalProductionEntryDebugScenarios.ButcherCorpseActionId] =
                TabooButcheryCausalProductionEntryDebugScenarios.RestoreKind,
            [OffenseArrivalCausalProductionEntryDebugScenarios.FinalizeRewardActionId] =
                OffenseArrivalCausalProductionEntryDebugScenarios.RestoreKind,
            [OffenseArrivalCausalProductionEntryDebugScenarios.SettleArrivalActionId] =
                OffenseArrivalCausalProductionEntryDebugScenarios.RestoreKind,
            [FestivalCelebrationCausalProductionEntryDebugScenarios.FirstCelebrationActionId] =
                FestivalCelebrationCausalProductionEntryDebugScenarios.RestoreKind,
            [FestivalCelebrationCausalProductionEntryDebugScenarios.SecondCelebrationActionId] =
                FestivalCelebrationCausalProductionEntryDebugScenarios.RestoreKind,
            [MetaProgressionCausalProductionEntryDebugScenarios
                .RunResultActionId] =
                MetaProgressionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [MetaProgressionCausalProductionEntryDebugScenarios
                .UpgradeActionId] =
                MetaProgressionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [RegularCustomerCausalProductionEntryDebugScenarios
                .FirstVisitActionId] =
                RegularCustomerCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [RegularCustomerCausalProductionEntryDebugScenarios
                .CandidateVisitActionId] =
                RegularCustomerCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [RegularCustomerCausalProductionEntryDebugScenarios
                .RecruitActionId] =
                RegularCustomerCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [CharacterLifeCausalProductionEntryDebugScenarios
                .AgeConditionActionId] =
                CharacterLifeCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [CharacterLifeCausalProductionEntryDebugScenarios
                .LifeStageActionId] =
                CharacterLifeCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [FactionRouteCausalProductionEntryDebugScenarios
                .SettlementActionId] =
                FactionRouteCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [FactionRouteCausalProductionEntryDebugScenarios
                .ArrivalActionId] =
                FactionRouteCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [FactionRouteCausalProductionEntryDebugScenarios
                .CargoActionId] =
                FactionRouteCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ConsumablesProgressionCausalProductionEntryDebugScenarios
                .MealActionId] =
                ConsumablesProgressionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ConsumablesProgressionCausalProductionEntryDebugScenarios
                .SubstanceActionId] =
                ConsumablesProgressionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [MealShortageCausalProductionEntryDebugScenarios
                .FirstMissActionId] =
                MealShortageCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [MealShortageCausalProductionEntryDebugScenarios
                .RepeatMissActionId] =
                MealShortageCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [PrimitiveSurvivalCausalProductionEntryDebugScenarios
                .FirstRestActionId] =
                PrimitiveSurvivalCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [PrimitiveSurvivalCausalProductionEntryDebugScenarios
                .RepeatRestActionId] =
                PrimitiveSurvivalCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ServiceHubDayCausalProductionEntryDebugScenarios.VisitActionId] =
                ServiceHubDayCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ServiceHubDayCausalProductionEntryDebugScenarios.ModeActionId] =
                ServiceHubDayCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ServiceHubDayCausalProductionEntryDebugScenarios.CloseDayActionId] =
                ServiceHubDayCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .InvasionStartActionId] =
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionLifecycleCausalOwnerRestoreKind,
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .InvasionBreachActionId] =
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionLifecycleCausalOwnerRestoreKind,
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .InvasionDamageActionId] =
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionLifecycleCausalOwnerRestoreKind,
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .InvasionFrontCollapseActionId] =
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionLifecycleCausalOwnerRestoreKind,
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .InvasionResolveActionId] =
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .InvasionLifecycleCausalOwnerRestoreKind,
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .NonlethalCombatInjuryActionId] =
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .CombatLifeCycleOwnerRestoreKind,
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .RecoverBodyHealthActionId] =
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .CombatLifeCycleOwnerRestoreKind,
            [MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .FatalCombatInjuryActionId] =
                MigratedProducerCombatDefenseEntryE2EDebugScenarios
                    .CombatLifeCycleOwnerRestoreKind,
            [StockSupplyTransformCausalProductionEntryDebugScenarios
                .SupplyActionId] =
                StockSupplyTransformCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [StockSupplyTransformCausalProductionEntryDebugScenarios
                .TransformActionId] =
                StockSupplyTransformCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                .BudgetBlockedActionId] =
                FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                .PurchaseWishlistActionId] =
                FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ObservedCareerArcCausalProductionEntryDebugScenarios
                .MentorshipAwardActionId] =
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ObservedCareerArcCausalProductionEntryDebugScenarios
                .LastLessonActionId] =
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ObservedCareerArcCausalProductionEntryDebugScenarios
                .QuietPromotionActionId] =
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ObservedCareerArcCausalProductionEntryDebugScenarios
                .FuneralActionId] =
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ObservedCareerArcCausalProductionEntryDebugScenarios
                .LineageCompressionActionId] =
                ObservedCareerArcCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                .ObserveIncidentActionId] =
                ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind,
            [ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                .ResolveIncidentActionId] =
                ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                    .CausalOwnerRestoreKind
        };

    private static readonly HashSet<string> PersistentFixtureActionIds = new(
        PersistentFixtureOwnerFamilies.Keys,
        StringComparer.Ordinal);

    private static readonly HashSet<string> EpisodeFixtureActionIds = new(
        new[]
        {
            "action:resolve-combat-attack",
            "action:execute-offense-battle-command",
            "action:apply-detox-treatment"
        },
        StringComparer.Ordinal);

    public static void RunCampaignBatchFromEnvironment()
    {
        string version = RequireVersion(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_CAMPAIGN_BATCH_VERSION"));
        string packagePath = RequireExistingDirectory(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_CAUSAL_CAMPAIGN_PACKAGE_PATH"), "campaign package");
        string path = ExecuteCampaignBatch(version, packagePath);
        Debug.Log("CAUSAL_GAMEPLAY_CAMPAIGN_BATCH=PASS; path=" + path);
    }

    public static string ExecuteCampaignBatch(
        string version,
        string packageDirectory)
    {
        string canonicalVersion = RequireVersion(version);
        string projectRoot = ProjectRoot();
        string packageRoot = Path.GetFullPath(packageDirectory);
        string gameCommit = ReadGit(projectRoot, "rev-parse HEAD");
        JObject inputManifest = StrictLoad(Path.Combine(
            packageRoot,
            "manifest.json"));
        HashSet<string> packageFiles = ValidateCampaignPackageManifest(
            packageRoot,
            inputManifest,
            gameCommit);
        string catalogPath = Path.Combine(packageRoot, "action_catalog.json");
        JObject catalog = StrictLoad(catalogPath);
        ValidateCatalog(catalog, gameCommit);
        Require((int)catalog["schemaVersion"] is 2 or 3 or 4,
            "Campaign execution requires a modern action catalog.");
        string catalogSha256 = "sha256:" + Sha256File(catalogPath);
        Require((string)inputManifest["catalogSha256"] == catalogSha256,
            "Campaign package catalog hash mismatch.");
        JArray campaigns = StrictLoadArray(Path.Combine(
            packageRoot,
            "campaigns.json"));
        int expectedCampaigns = (int)inputManifest["generatedCampaigns"];
        int episodesPerCampaign = (int)inputManifest["episodesPerCampaign"];
        Require(campaigns.Count == expectedCampaigns
                && expectedCampaigns == (int)inputManifest["requestedCampaigns"]
                && expectedCampaigns > 0
                && episodesPerCampaign >= 2
                && (int)inputManifest["episodeCount"]
                    == expectedCampaigns * episodesPerCampaign,
            "Campaign package counts are incomplete or inconsistent.");

        string destination = PrepareCreateOnlyDestination(
            projectRoot,
            CampaignBatchParent,
            canonicalVersion);
        string staging = destination + ".staging-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(Path.Combine(staging, "input_plans"));
        Directory.CreateDirectory(Path.Combine(staging, "results"));
        try
        {
            File.Copy(catalogPath, Path.Combine(staging, "input_action_catalog.json"));
            File.Copy(
                Path.Combine(packageRoot, "manifest.json"),
                Path.Combine(staging, "input_package_manifest.json"));
            File.Copy(
                Path.Combine(packageRoot, "campaigns.json"),
                Path.Combine(staging, "input_campaigns.json"));
            var outputFiles = new List<string>
            {
                "input_action_catalog.json",
                "input_package_manifest.json",
                "input_campaigns.json"
            };
            var batchCampaigns = new JArray();
            var globalPlanIds = new HashSet<string>(StringComparer.Ordinal);
            var globalPlanHashes = new HashSet<string>(StringComparer.Ordinal);
            var globalPlannedActionIds = new HashSet<string>(
                StringComparer.Ordinal);
            var globalOutcomeIds = new HashSet<string>(StringComparer.Ordinal);
            var globalProtagonists = new HashSet<string>(StringComparer.Ordinal);
            var actionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var templateCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            int totalEpisodes = 0;
            int totalNodes = 0;
            int totalEvidence = 0;
            int totalSubjectBoundEvidence = 0;
            int totalProtagonistEvidence = 0;
            int totalRestoreBoundaries = 0;
            int totalVerifiedRestores = 0;
            int totalWorldRestoreBoundaries = 0;
            int totalVerifiedWorldRestores = 0;
            int totalPersistentWorldOwners = 0;
            int totalRehydratedWorldOwners = 0;

            for (int campaignIndex = 0;
                 campaignIndex < campaigns.Count;
                 campaignIndex++)
            {
                JObject campaign = campaigns[campaignIndex] as JObject
                    ?? throw new InvalidOperationException(
                        "Campaign package row must be an object.");
                ValidateCampaignIdentity(
                    campaign,
                    catalog,
                    catalogSha256,
                    gameCommit,
                    episodesPerCampaign,
                    globalProtagonists);
                string campaignOutputLabel = CampaignOutputLabel(campaign);
                string campaignId = (string)campaign["campaignId"];
                string campaignSha256 = (string)campaign["campaignSha256"];
                string protagonistId = (string)campaign["protagonistId"];
                JArray episodes = (JArray)campaign["episodes"];
                var episodeRows = new JArray();
                var campaignOutcomeIds = new HashSet<string>(StringComparer.Ordinal);
                var sessionOutcomeIds = new HashSet<string>(StringComparer.Ordinal);
                var campaignTemplateCounts = new Dictionary<string, int>(
                    StringComparer.Ordinal);
                LongHorizonNarrativeExecutionExporter.LongHorizonCheckpoint
                    sessionCheckpoint = null;
                JObject previousSessionState = null;
                string previousEpisodeId = string.Empty;
                string previousTerminalOutcomeId = string.Empty;
                int changedSessionEpisodeCount = 0;
                int campaignNodeCount = 0;
                int campaignRestoreCount = 0;
                int campaignVerifiedRestoreCount = 0;
                JObject campaignInitialState = null;
                JObject campaignFinalState = null;
                var persistentOwnerActions = new Dictionary<string, string>(
                    StringComparer.Ordinal);
                var defenseOwners = new HashSet<string>(StringComparer.Ordinal);
                var retailOwners = new HashSet<string>(StringComparer.Ordinal);
                var episodeLocalOwners = new HashSet<string>(StringComparer.Ordinal);
                string relationshipOwner = string.Empty;
                int relationshipAdjustmentCount = 0;
                string cropOwner = string.Empty;
                var cropActions = new List<string>();
                string plainFireOwner = string.Empty;
                string physicalFireOwner = string.Empty;
                var fireActions = new List<string>();
                string socialOwner = string.Empty;
                var socialActions = new List<string>();
                string traitOwner = string.Empty;
                var traitActions = new List<string>();
                string captivityOwner = string.Empty;
                var captivityActions = new List<string>();
                string equipmentApparelOwner = string.Empty;
                var equipmentApparelActions = new List<string>();
                string researchKnowledgeOwner = string.Empty;
                var researchKnowledgeActions = new List<string>();
                string productionBillOwner = string.Empty;
                var productionBillActions = new List<string>();
                string facilityEvolutionOwner = string.Empty;
                var facilityEvolutionActions = new List<string>();
                string facilitySynthesisOwner = string.Empty;
                var facilitySynthesisActions = new List<string>();
                string infrastructurePowerOwner = string.Empty;
                var infrastructurePowerActions = new List<string>();
                string dungeonSpaceOwner = string.Empty;
                var dungeonSpaceActions = new List<string>();
                string roomEnvironmentOwner = string.Empty;
                var roomEnvironmentActions = new List<string>();
                string speciesIncidentOwner = string.Empty;
                var speciesIncidentActions = new List<string>();
                string certifiedSeedOwner = string.Empty;
                var certifiedSeedActions = new List<string>();
                string staffResponseOwner = string.Empty;
                var staffResponseActions = new List<string>();
                string emergencySuspensionOwner = string.Empty;
                var emergencySuspensionActions = new List<string>();
                string bossAwardOwner = string.Empty;
                var bossAwardActions = new List<string>();
                string tradeInventoryOwner = string.Empty;
                var tradeInventoryActions = new List<string>();
                string diseaseRouteOwner = string.Empty;
                var diseaseRouteActions = new List<string>();
                string offenseTruthOwner = string.Empty;
                var offenseTruthActions = new List<string>();
                string offenseNodeResolutionOwner = string.Empty;
                var offenseNodeResolutionActions = new List<string>();
                string workIdentityOwner = string.Empty;
                var workIdentityActions = new List<string>();
                string runVariableOwner = string.Empty;
                var runVariableActions = new List<string>();
                string ownerRunCompletionOwner = string.Empty;
                var ownerRunCompletionActions = new List<string>();
                string visitorReceptionCheckoutOwner = string.Empty;
                var visitorReceptionCheckoutActions = new List<string>();
                string tabooButcheryOwner = string.Empty;
                var tabooButcheryActions = new List<string>();
                string offenseArrivalOwner = string.Empty;
                var offenseArrivalActions = new List<string>();
                string festivalCelebrationOwner = string.Empty;
                var festivalCelebrationActions = new List<string>();
                string metaProgressionOwner = string.Empty;
                var metaProgressionActions = new List<string>();
                string regularCustomerOwner = string.Empty;
                var regularCustomerActions = new List<string>();
                string characterLifeOwner = string.Empty;
                var characterLifeActions = new List<string>();
                string factionRouteOwner = string.Empty;
                var factionRouteActions = new List<string>();
                string consumablesOwner = string.Empty;
                var consumablesActions = new List<string>();
                string mealShortageOwner = string.Empty;
                var mealShortageActions = new List<string>();
                string primitiveSurvivalOwner = string.Empty;
                var primitiveSurvivalActions = new List<string>();
                string serviceHubDayOwner = string.Empty;
                var serviceHubDayActions = new List<string>();
                string invasionLifecycleOwner = string.Empty;
                var invasionLifecycleActions = new List<string>();
                string combatLifeCycleOwner = string.Empty;
                var combatLifeCycleActions = new List<string>();
                string stockSupplyTransformOwner = string.Empty;
                var stockSupplyTransformActions = new List<string>();
                string facilityWishlistProcurementOwner = string.Empty;
                var facilityWishlistProcurementActions = new List<string>();
                string observedCareerArcOwner = string.Empty;
                var observedCareerArcActions = new List<string>();
                string observedMealIncidentResolutionOwner = string.Empty;
                var observedMealIncidentResolutionActions = new List<string>();
                JObject campaignWorldCheckpoint = null;
                JObject campaignFinalWorldCheckpoint = null;
                string campaignInitialWorldCheckpointPath = string.Empty;
                string campaignInitialWorldCheckpointSha256 = string.Empty;
                string campaignFinalWorldCheckpointPath = string.Empty;
                string campaignFinalWorldCheckpointSha256 = string.Empty;
                string previousWorldStateAfterPath = string.Empty;
                string previousWorldStateAfterSha256 = string.Empty;
                int campaignWorldRestoreCount = 0;
                int campaignWorldVerifiedRestoreCount = 0;
                int campaignIntroducedOwnerCount = 0;
                int campaignRehydratedOwnerCount = 0;

                for (int episodeIndex = 0;
                     episodeIndex < episodes.Count;
                     episodeIndex++)
                {
                    JObject episode = episodes[episodeIndex] as JObject
                        ?? throw new InvalidOperationException(
                            "Campaign episode must be an object.");
                    ValidateCampaignEpisodeLink(
                        episode,
                        episodeIndex + 1,
                        previousEpisodeId);
                    string relativePlanPath = (string)episode["planPath"];
                    Require(packageFiles.Contains(relativePlanPath),
                        "Campaign episode plan is absent from the package manifest: "
                        + relativePlanPath);
                    string planPath = SafePackagePath(
                        packageRoot,
                        relativePlanPath);
                    CausalGameplayPlan plan = ParsePlan(
                        StrictLoad(planPath),
                        catalog,
                        catalogSha256,
                        gameCommit);
                    Require((string)episode["planId"] == plan.PlanId
                            && (string)episode["planSha256"] == plan.PlanSha256
                            && (string)episode["templateId"] == plan.TemplateId
                            && globalPlanIds.Add(plan.PlanId)
                            && globalPlanHashes.Add(plan.PlanSha256),
                        "Campaign episode plan identity is mismatched or reused.");
                    foreach (CausalGameplayPlanNode node in plan.Nodes)
                        globalPlannedActionIds.Add(node.ActionId);
                    IReadOnlyDictionary<string, string> fixtureOwnerIds =
                        ValidateCampaignOwnerBindings(
                            episode,
                            plan,
                            persistentOwnerActions,
                            defenseOwners,
                            retailOwners,
                            episodeLocalOwners,
                            ref relationshipOwner,
                            ref relationshipAdjustmentCount,
                            ref cropOwner,
                            cropActions,
                            ref plainFireOwner,
                            ref physicalFireOwner,
                            fireActions,
                            ref socialOwner,
                            socialActions,
                            ref traitOwner,
                            traitActions,
                            ref captivityOwner,
                            captivityActions,
                            ref equipmentApparelOwner,
                            equipmentApparelActions,
                            ref researchKnowledgeOwner,
                            researchKnowledgeActions,
                            ref productionBillOwner,
                            productionBillActions,
                            ref facilityEvolutionOwner,
                            facilityEvolutionActions,
                            ref facilitySynthesisOwner,
                            facilitySynthesisActions,
                            ref infrastructurePowerOwner,
                            infrastructurePowerActions,
                            ref dungeonSpaceOwner,
                            dungeonSpaceActions,
                            ref roomEnvironmentOwner,
                            roomEnvironmentActions,
                            ref speciesIncidentOwner,
                            speciesIncidentActions,
                            ref certifiedSeedOwner,
                            certifiedSeedActions,
                            ref staffResponseOwner,
                            staffResponseActions,
                            ref emergencySuspensionOwner,
                            emergencySuspensionActions,
                            ref bossAwardOwner,
                            bossAwardActions,
                            ref tradeInventoryOwner,
                            tradeInventoryActions,
                            ref diseaseRouteOwner,
                            diseaseRouteActions,
                            ref offenseTruthOwner,
                            offenseTruthActions,
                            ref offenseNodeResolutionOwner,
                            offenseNodeResolutionActions,
                            ref workIdentityOwner,
                            workIdentityActions,
                            ref runVariableOwner,
                            runVariableActions,
                            ref ownerRunCompletionOwner,
                            ownerRunCompletionActions,
                            ref visitorReceptionCheckoutOwner,
                            visitorReceptionCheckoutActions,
                            ref tabooButcheryOwner,
                            tabooButcheryActions,
                            ref offenseArrivalOwner,
                            offenseArrivalActions,
                            ref festivalCelebrationOwner,
                            festivalCelebrationActions,
                            ref metaProgressionOwner,
                            metaProgressionActions,
                            ref regularCustomerOwner,
                            regularCustomerActions,
                            ref characterLifeOwner,
                            characterLifeActions,
                            ref factionRouteOwner,
                            factionRouteActions,
                            ref consumablesOwner,
                            consumablesActions,
                            ref mealShortageOwner,
                            mealShortageActions,
                            ref primitiveSurvivalOwner,
                            primitiveSurvivalActions,
                            ref serviceHubDayOwner,
                            serviceHubDayActions,
                            ref invasionLifecycleOwner,
                            invasionLifecycleActions,
                            ref combatLifeCycleOwner,
                            combatLifeCycleActions,
                            ref stockSupplyTransformOwner,
                            stockSupplyTransformActions,
                            ref facilityWishlistProcurementOwner,
                            facilityWishlistProcurementActions,
                            ref observedCareerArcOwner,
                            observedCareerArcActions,
                            ref observedMealIncidentResolutionOwner,
                            observedMealIncidentResolutionActions);
                    campaignTemplateCounts[plan.TemplateId] =
                        campaignTemplateCounts.TryGetValue(
                            plan.TemplateId,
                            out int localTemplateCount)
                                ? localTemplateCount + 1
                                : 1;
                    templateCounts[plan.TemplateId] =
                        templateCounts.TryGetValue(
                            plan.TemplateId,
                            out int globalTemplateCount)
                                ? globalTemplateCount + 1
                                : 1;

                    string episodeRunId = "run:causal-campaign:"
                        + campaignSha256.Substring("sha256:".Length)
                        + ":"
                        + (episodeIndex + 1).ToString("D4");
                    CausalGameplayExecutionResult execution;
                    JObject sessionBefore;
                    JObject sessionAfter;
                    bool restoreVerified;
                    JObject worldCheckpointInput = campaignWorldCheckpoint == null
                        ? ProductionCausalWorldCheckpoint.Empty()
                        : (JObject)campaignWorldCheckpoint.DeepClone();
                    JObject worldStateBefore;
                    JObject worldStateAfter;
                    bool worldRestoreVerified;
                    int restoredWorldOwners;
                    int introducedWorldOwners;
                    int changedWorldOwners;
                    using (var world = new ProductionPlanWorld(
                               plan,
                               null,
                               episodeRunId,
                               protagonistId,
                               sessionCheckpoint,
                               fixtureOwnerIds,
                               campaignWorldCheckpoint))
                    {
                        sessionBefore = world.CaptureSessionState();
                        restoreVerified = previousSessionState == null
                            || JToken.DeepEquals(
                                previousSessionState,
                                sessionBefore);
                        Require(restoreVerified,
                            "Campaign protagonist state changed across an episode restore boundary.");
                        Require((string)sessionBefore["actor.id"] == protagonistId,
                            "Campaign protagonist identity changed across an episode boundary.");
                        worldRestoreVerified = JToken.DeepEquals(
                            worldCheckpointInput,
                            world.RestoredInputWorldCheckpoint);
                        Require(worldRestoreVerified,
                            "Campaign world checkpoint changed before owner binding.");
                        worldStateBefore = world.CaptureWorldCheckpoint();
                        restoredWorldOwners = world.RestoredWorldOwnerCount;
                        introducedWorldOwners = world.IntroducedWorldOwnerCount;
                        int boundPersistentOwnerCount =
                            CountBoundPersistentOwners(fixtureOwnerIds);
                        worldRestoreVerified = worldRestoreVerified
                            && restoredWorldOwners + introducedWorldOwners
                                == boundPersistentOwnerCount;
                        Require(worldRestoreVerified,
                            "Campaign world owner restore/binding count is incomplete.");
                        if (episodeIndex > 0)
                        {
                            campaignWorldRestoreCount++;
                            if (worldRestoreVerified)
                                campaignWorldVerifiedRestoreCount++;
                        }
                        campaignIntroducedOwnerCount += introducedWorldOwners;
                        campaignRehydratedOwnerCount += restoredWorldOwners;
                        if (previousSessionState != null)
                        {
                            campaignRestoreCount++;
                            if (restoreVerified)
                                campaignVerifiedRestoreCount++;
                        }
                        if (campaignInitialState == null)
                            campaignInitialState = (JObject)sessionBefore.DeepClone();
                        if (previousEpisodeId.Length > 0)
                        {
                            Require(globalOutcomeIds.Contains(previousTerminalOutcomeId),
                                "Campaign episode dependency lacks a committed prior outcome.");
                        }

                        CausalGameplayActionRegistry registry = CreateRegistry(world);
                        ValidatePlanActionsMatchCatalog(plan, catalog, registry);
                        execution = new CausalGameplayPlanExecutor(registry, world)
                            .Execute(plan);
                        Require(execution.IsComplete && !execution.ReplanRequired,
                            "Campaign episode did not complete cleanly: "
                            + (string)episode["episodeId"] + "; records="
                            + string.Join("; ", execution.Records.Select(record =>
                                record.NodeId + ":" + record.Status + ":" + record.FailureCode
                                + ":" + record.FailureReason + ":"
                                + string.Join(",", record.MismatchReasons))));
                        foreach (CausalGameplayNodeRecord record in execution.Records)
                        {
                            Require(record.Status == CausalGameplayNodeStatus.Completed
                                    && !string.IsNullOrEmpty(record.OutcomeId),
                                "Campaign record is incomplete or reused an outcome: "
                                + record.NodeId);
                            ValidateOutcomeEvidence(record);
                            JObject catalogAction = ((JArray)catalog["actions"])
                                .Cast<JObject>()
                                .Single(value => (string)value["actionId"]
                                    == record.ActionId);
                            string[] expectedCompanionTypes =
                                // Relationship validation is separate from type/count validation.
                                (catalogAction["companionOutcomes"] as JArray
                                    ?? new JArray())
                                .Cast<JObject>()
                                .Select(value => (string)value["outcomeTypeId"])
                                .ToArray();
                            string[] actualCompanionTypes =
                                record.CompanionOutcomeEvidence
                                    .OfType<JObject>()
                                    .Select(value =>
                                        (string)value["snapshot"]?["outcomeTypeId"])
                                    .ToArray();
                            Require(expectedCompanionTypes.SequenceEqual(
                                    actualCompanionTypes,
                                    StringComparer.Ordinal),
                                "Campaign companion outcome types changed for "
                                + record.ActionId);
                            if (HasOutcomeRelations(plan))
                                ValidateOutcomeRelations(record, (JArray)catalogAction["companionOutcomes"]);
                            JObject[] allEvidence = new[]
                                {
                                    record.OutcomeEvidence
                                }
                                .Concat(record.CompanionOutcomeEvidence
                                    .OfType<JObject>())
                                .ToArray();
                            foreach (JObject evidence in allEvidence)
                            {
                                string outcomeId = (string)evidence["outcomeId"];
                                Require(campaignOutcomeIds.Add(outcomeId)
                                        && globalOutcomeIds.Add(outcomeId),
                                    "Campaign outcome was reused: " + outcomeId);
                                totalEvidence++;
                                JObject snapshot = (JObject)evidence["snapshot"];
                                int participants =
                                    (snapshot["participants"] as JArray)?.Count ?? 0;
                                int subjects =
                                    (snapshot["subjects"] as JArray)?.Count ?? 0;
                                Require(participants + subjects > 0,
                                    "Campaign outcome lacks participant/subject binding.");
                                totalSubjectBoundEvidence++;
                                if (ProtagonistActionIds.Contains(record.ActionId))
                                {
                                    Require(SnapshotBindsEntity(snapshot, protagonistId),
                                        "A protagonist action outcome is not bound to the campaign protagonist: "
                                        + record.ActionId);
                                    totalProtagonistEvidence++;
                                }
                                if (world.IsSessionOutcome(outcomeId))
                                    sessionOutcomeIds.Add(outcomeId);
                            }
                            actionCounts[record.ActionId] =
                                actionCounts.TryGetValue(record.ActionId, out int count)
                                    ? count + 1
                                    : 1;
                        }
                        sessionAfter = world.CaptureSessionState();
                        sessionCheckpoint = world.CaptureSessionCheckpoint(
                            sessionOutcomeIds);
                        worldStateAfter = world.CaptureWorldCheckpoint();
                    }

                    if (!JToken.DeepEquals(sessionBefore, sessionAfter))
                        changedSessionEpisodeCount++;
                    changedWorldOwners = CountChangedOwners(
                        worldStateBefore,
                        worldStateAfter);
                    int expectedChangedWorldOwners =
                        CountBoundPersistentOwners(fixtureOwnerIds);
                    Require(changedWorldOwners == expectedChangedWorldOwners,
                        "Campaign persistent-owner change count does not match its bound actions.");
                    previousSessionState = (JObject)sessionAfter.DeepClone();
                    campaignFinalState = (JObject)sessionAfter.DeepClone();
                    campaignWorldCheckpoint = (JObject)worldStateAfter.DeepClone();
                    campaignFinalWorldCheckpoint =
                        (JObject)worldStateAfter.DeepClone();
                    previousEpisodeId = (string)episode["episodeId"];
                    previousTerminalOutcomeId = execution.Records[^1].OutcomeId;
                    campaignNodeCount += execution.Records.Count;
                    totalNodes += execution.Records.Count;
                    totalEpisodes++;

                    string inputRelative = "input_plans/"
                        + Path.GetFileName(relativePlanPath);
                    string resultDirectory = Path.Combine(
                        staging,
                        "results",
                        campaignOutputLabel);
                    Directory.CreateDirectory(resultDirectory);
                    string checkpointDirectory = Path.Combine(
                        resultDirectory,
                        "world");
                    Directory.CreateDirectory(checkpointDirectory);
                    string resultRelative = "results/"
                        + campaignOutputLabel
                        + "/episode-"
                        + (episodeIndex + 1).ToString("D4")
                        + ".json";
                    File.Copy(planPath, Path.Combine(staging, inputRelative));
                    WriteJson(
                        Path.Combine(staging, resultRelative),
                        ResultJson(plan, execution, catalogSha256));
                    outputFiles.Add(inputRelative);
                    outputFiles.Add(resultRelative);
                    JObject persistedWorldCheckpointInput =
                        PersistedCheckpoint(worldCheckpointInput);
                    JObject persistedWorldStateBefore =
                        PersistedCheckpoint(worldStateBefore);
                    JObject persistedWorldStateAfter =
                        PersistedCheckpoint(worldStateAfter);
                    string checkpointPrefix = "results/"
                        + campaignOutputLabel
                        + "/world/episode-"
                        + (episodeIndex + 1).ToString("D4");
                    string inputWorldCheckpointRelative;
                    string inputWorldCheckpointSha256;
                    if (episodeIndex == 0)
                    {
                        inputWorldCheckpointRelative = checkpointPrefix
                            + "-input.json";
                        inputWorldCheckpointSha256 = WriteWorldCheckpointFile(
                            staging,
                            inputWorldCheckpointRelative,
                            persistedWorldCheckpointInput);
                        outputFiles.Add(inputWorldCheckpointRelative);
                    }
                    else
                    {
                        inputWorldCheckpointRelative = previousWorldStateAfterPath;
                        inputWorldCheckpointSha256 = WorldCheckpointSha256(
                            persistedWorldCheckpointInput);
                        Require(inputWorldCheckpointSha256
                                == previousWorldStateAfterSha256,
                            "Campaign input checkpoint does not reference the "
                            + "prior delivered after-state.");
                    }
                    string worldStateBeforeRelative = checkpointPrefix
                        + "-before.json";
                    string worldStateBeforeSha256 = WriteWorldCheckpointFile(
                        staging,
                        worldStateBeforeRelative,
                        persistedWorldStateBefore);
                    string worldStateAfterRelative = checkpointPrefix
                        + "-after.json";
                    string worldStateAfterSha256 = WriteWorldCheckpointFile(
                        staging,
                        worldStateAfterRelative,
                        persistedWorldStateAfter);
                    outputFiles.Add(worldStateBeforeRelative);
                    outputFiles.Add(worldStateAfterRelative);
                    previousWorldStateAfterPath = worldStateAfterRelative;
                    previousWorldStateAfterSha256 = worldStateAfterSha256;
                    if (campaignInitialWorldCheckpointPath.Length == 0)
                    {
                        campaignInitialWorldCheckpointPath =
                            worldStateBeforeRelative;
                        campaignInitialWorldCheckpointSha256 =
                            worldStateBeforeSha256;
                    }
                    campaignFinalWorldCheckpointPath = worldStateAfterRelative;
                    campaignFinalWorldCheckpointSha256 = worldStateAfterSha256;
                    episodeRows.Add(new JObject
                    {
                        ["episodeId"] = (string)episode["episodeId"],
                        ["sequence"] = episodeIndex + 1,
                        ["planId"] = plan.PlanId,
                        ["planSha256"] = plan.PlanSha256,
                        ["templateId"] = plan.TemplateId,
                        ["resultPath"] = resultRelative,
                        ["resultSha256"] = Sha256File(Path.Combine(
                            staging,
                            resultRelative)),
                        ["executedCount"] = execution.Records.Count,
                        ["terminalOutcomeId"] = previousTerminalOutcomeId,
                        ["dependsOnEpisodeIds"] = episode["dependsOnEpisodeIds"]
                            .DeepClone(),
                        ["causalLinks"] = episode["causalLinks"].DeepClone(),
                        ["worldOwnerBindings"] =
                            episode["worldOwnerBindings"].DeepClone(),
                        ["inputWorldCheckpointPath"] =
                            inputWorldCheckpointRelative,
                        ["inputWorldCheckpointSha256"] =
                            inputWorldCheckpointSha256,
                        ["worldStateBeforePath"] = worldStateBeforeRelative,
                        ["worldStateBeforeSha256"] = worldStateBeforeSha256,
                        ["worldStateAfterPath"] = worldStateAfterRelative,
                        ["worldStateAfterSha256"] = worldStateAfterSha256,
                        ["worldOwnerCount"] =
                            ((JArray)worldStateAfter["owners"]).Count,
                        ["introducedWorldOwnerCount"] = introducedWorldOwners,
                        ["rehydratedWorldOwnerCount"] = restoredWorldOwners,
                        ["changedWorldOwnerCount"] = changedWorldOwners,
                        ["worldRestoreBoundary"] = episodeIndex > 0,
                        ["worldRestoreVerified"] = worldRestoreVerified,
                        ["sessionStateBefore"] = sessionBefore.DeepClone(),
                        ["sessionStateBeforeSha256"] =
                            CausalGameplayCanonicalJson.Sha256(sessionBefore),
                        ["sessionStateAfter"] = sessionAfter.DeepClone(),
                        ["sessionStateAfterSha256"] =
                            CausalGameplayCanonicalJson.Sha256(sessionAfter),
                        ["restoreBoundary"] = episodeIndex > 0,
                        ["restoreVerified"] = restoreVerified
                    });
                    persistedWorldCheckpointInput.RemoveAll();
                    persistedWorldStateBefore.RemoveAll();
                    persistedWorldStateAfter.RemoveAll();
                    worldCheckpointInput.RemoveAll();
                    worldStateBefore.RemoveAll();
                    worldStateAfter.RemoveAll();
                }

                bool cropFamilyCampaign = campaignTemplateCounts.Count == 2
                    && campaignTemplateCounts.TryGetValue(
                        CropIrrigationEpisodeTemplate,
                        out int irrigationEpisodeCount)
                    && irrigationEpisodeCount == 1
                    && campaignTemplateCounts.TryGetValue(
                        CropHarvestEpisodeTemplate,
                        out int harvestEpisodeCount)
                    && harvestEpisodeCount == 1;
                bool fireFamilyCampaign = campaignTemplateCounts.Count
                        == FireEpisodeTemplates.Length
                    && FireEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool socialFamilyCampaign = campaignTemplateCounts.Count
                        == SocialEpisodeTemplates.Length
                    && SocialEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool traitFamilyCampaign = campaignTemplateCounts.Count
                        == TraitEpisodeTemplates.Length
                    && TraitEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool captivityRansomFamilyCampaign =
                    IsCaptivityFamilyCampaign(
                        campaignTemplateCounts,
                        CaptivityRansomEpisodeTemplate);
                bool captivityEscapeFamilyCampaign =
                    IsCaptivityFamilyCampaign(
                        campaignTemplateCounts,
                        CaptivityEscapeEpisodeTemplate);
                bool equipmentApparelFamilyCampaign = campaignTemplateCounts.Count
                        == EquipmentApparelEpisodeTemplates.Length
                    && EquipmentApparelEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool researchKnowledgeFamilyCampaign = campaignTemplateCounts.Count
                        == ResearchKnowledgeEpisodeTemplates.Length
                    && ResearchKnowledgeEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool productionBillFamilyCampaign = campaignTemplateCounts.Count
                        == ProductionBillEpisodeTemplates.Length
                    && ProductionBillEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool facilityEvolutionFamilyCampaign = campaignTemplateCounts.Count
                        == FacilityEvolutionEpisodeTemplates.Length
                    && FacilityEvolutionEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool facilitySynthesisFamilyCampaign = campaignTemplateCounts.Count
                        == FacilitySynthesisEpisodeTemplates.Length
                    && FacilitySynthesisEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool infrastructurePowerFamilyCampaign = campaignTemplateCounts.Count
                        == InfrastructurePowerEpisodeTemplates.Length
                    && InfrastructurePowerEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool dungeonSpaceFamilyCampaign = campaignTemplateCounts.Count
                        == DungeonSpaceEpisodeTemplates.Length
                    && DungeonSpaceEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool roomEnvironmentFamilyCampaign = campaignTemplateCounts.Count
                        == RoomEnvironmentEpisodeTemplates.Length
                    && RoomEnvironmentEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool speciesIncidentFamilyCampaign = campaignTemplateCounts.Count
                        == SpeciesIncidentEpisodeTemplates.Length
                    && SpeciesIncidentEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool certifiedSeedFamilyCampaign = campaignTemplateCounts.Count
                        == CertifiedSeedEpisodeTemplates.Length
                    && CertifiedSeedEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool staffResponseFamilyCampaign = campaignTemplateCounts.Count
                        == StaffResponseEpisodeTemplates.Length
                    && StaffResponseEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool emergencySuspensionFamilyCampaign =
                    campaignTemplateCounts.Count
                        == EmergencySuspensionEpisodeTemplates.Length
                    && EmergencySuspensionEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool bossAwardFamilyCampaign = campaignTemplateCounts.Count
                        == BossAwardEpisodeTemplates.Length
                    && BossAwardEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool tradeInventoryFamilyCampaign = campaignTemplateCounts.Count
                        == TradeInventoryEpisodeTemplates.Length
                    && TradeInventoryEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool diseaseRouteFamilyCampaign = campaignTemplateCounts.Count
                        == DiseaseRouteEpisodeTemplates.Length
                    && DiseaseRouteEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool offenseTruthFamilyCampaign = campaignTemplateCounts.Count
                        == OffenseTruthEpisodeTemplates.Length
                    && OffenseTruthEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool offenseNodeResolutionFamilyCampaign =
                    campaignTemplateCounts.Count
                        == OffenseNodeResolutionEpisodeTemplates.Length
                    && OffenseNodeResolutionEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool workIdentityFamilyCampaign = campaignTemplateCounts.Count
                        == WorkIdentityEpisodeTemplates.Length
                    && WorkIdentityEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool runVariableFamilyCampaign = campaignTemplateCounts.Count
                        == RunVariableEpisodeTemplates.Length
                    && RunVariableEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool ownerRunCompletionFamilyCampaign = campaignTemplateCounts.Count
                        == OwnerRunCompletionEpisodeTemplates.Length
                    && OwnerRunCompletionEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(templateId, out int count) && count == 1);
                bool visitorReceptionCheckoutFamilyCampaign = campaignTemplateCounts.Count
                        == VisitorReceptionCheckoutEpisodeTemplates.Length
                    && VisitorReceptionCheckoutEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(templateId, out int count) && count == 1);
                bool tabooButcheryFamilyCampaign = campaignTemplateCounts.Count
                        == TabooButcheryEpisodeTemplates.Length
                    && TabooButcheryEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(templateId, out int count) && count == 1);
                bool metaProgressionFamilyCampaign = campaignTemplateCounts.Count
                        == MetaProgressionEpisodeTemplates.Length
                    && MetaProgressionEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool offenseArrivalFamilyCampaign = campaignTemplateCounts.Count
                        == OffenseArrivalEpisodeTemplates.Length
                    && OffenseArrivalEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool festivalCelebrationFamilyCampaign = campaignTemplateCounts.Count
                        == FestivalCelebrationEpisodeTemplates.Length
                    && FestivalCelebrationEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool regularCustomerFamilyCampaign = campaignTemplateCounts.Count
                        == RegularCustomerEpisodeTemplates.Length
                    && RegularCustomerEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool characterLifeFamilyCampaign = campaignTemplateCounts.Count
                        == CharacterLifeEpisodeTemplates.Length
                    && CharacterLifeEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool factionRouteFamilyCampaign = campaignTemplateCounts.Count
                        == FactionRouteEpisodeTemplates.Length
                    && FactionRouteEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool consumablesFamilyCampaign = campaignTemplateCounts.Count
                        == ConsumablesEpisodeTemplates.Length
                    && ConsumablesEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool mealShortageFamilyCampaign = campaignTemplateCounts.Count
                        == MealShortageEpisodeTemplates.Length
                    && MealShortageEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool primitiveSurvivalFamilyCampaign =
                    campaignTemplateCounts.Count
                        == PrimitiveSurvivalEpisodeTemplates.Length
                    && PrimitiveSurvivalEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool serviceHubDayFamilyCampaign =
                    campaignTemplateCounts.Count
                        == ServiceHubDayEpisodeTemplates.Length
                    && ServiceHubDayEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool invasionLifecycleFamilyCampaign =
                    campaignTemplateCounts.Count
                        == InvasionLifecycleEpisodeTemplates.Length
                    && InvasionLifecycleEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool combatLifeCycleFamilyCampaign =
                    campaignTemplateCounts.Count
                        == CombatLifeCycleEpisodeTemplates.Length
                    && CombatLifeCycleEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool stockSupplyTransformFamilyCampaign =
                    campaignTemplateCounts.Count
                        == StockSupplyTransformEpisodeTemplates.Length
                    && StockSupplyTransformEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool facilityWishlistProcurementFamilyCampaign =
                    campaignTemplateCounts.Count
                        == FacilityWishlistProcurementEpisodeTemplates.Length
                    && FacilityWishlistProcurementEpisodeTemplates.All(
                        templateId => campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool observedCareerArcFamilyCampaign =
                    campaignTemplateCounts.Count
                        == ObservedCareerArcEpisodeTemplates.Length
                    && ObservedCareerArcEpisodeTemplates.All(templateId =>
                        campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                bool observedMealIncidentResolutionFamilyCampaign =
                    campaignTemplateCounts.Count
                        == ObservedMealIncidentResolutionEpisodeTemplates.Length
                    && ObservedMealIncidentResolutionEpisodeTemplates.All(
                        templateId => campaignTemplateCounts.TryGetValue(
                            templateId,
                            out int count)
                        && count == 1);
                if (cropFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && cropOwner.Length > 0
                            && cropActions.SequenceEqual(
                                new[]
                                {
                                    CropIrrigationAction,
                                    CropHarvestAction
                                },
                                StringComparer.Ordinal),
                        "Crop owner-family campaign composition changed during execution.");
                }
                else if (fireFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && plainFireOwner.Length > 0
                            && physicalFireOwner.Length > 0
                            && plainFireOwner != physicalFireOwner
                            && fireActions.SequenceEqual(
                                FireActions,
                                StringComparer.Ordinal),
                        "Environmental-fire owner-family campaign composition changed during execution.");
                }
                else if (socialFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && socialOwner.Length > 0
                            && socialActions.SequenceEqual(
                                SocialActions,
                                StringComparer.Ordinal),
                        "Social conflict-apology owner-family campaign composition changed during execution.");
                }
                else if (traitFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && traitOwner.Length > 0
                            && traitActions.SequenceEqual(
                                TraitActions,
                                StringComparer.Ordinal),
                        "Acquired-trait manifestation-reaction-erasure owner-family campaign composition changed during execution.");
                }
                else if (captivityRansomFamilyCampaign
                         || captivityEscapeFamilyCampaign)
                {
                    string terminalAction = captivityRansomFamilyCampaign
                        ? CaptivityCausalProductionEntryDebugScenarios.RansomActionId
                        : CaptivityCausalProductionEntryDebugScenarios.EscapeActionId;
                    string[] expected = CaptivityCommonActions
                        .Concat(new[] { terminalAction })
                        .ToArray();
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && captivityOwner.Length > 0
                            && captivityActions.SequenceEqual(
                                expected,
                                StringComparer.Ordinal),
                        "Captivity interaction-performer-terminal owner-family campaign composition changed during execution.");
                }
                else if (equipmentApparelFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && equipmentApparelOwner.Length > 0
                            && equipmentApparelActions.SequenceEqual(
                                EquipmentApparelActions,
                                StringComparer.Ordinal),
                        "Equipment apparel craft-equip owner-family campaign composition changed during execution.");
                }
                else if (researchKnowledgeFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && researchKnowledgeOwner.Length > 0
                            && researchKnowledgeActions.SequenceEqual(
                                ResearchKnowledgeActions,
                                StringComparer.Ordinal),
                        "Research knowledge progress-completion owner-family campaign composition changed during execution.");
                }
                else if (productionBillFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && productionBillOwner.Length > 0
                            && productionBillActions.SequenceEqual(
                                ProductionBillActions,
                                StringComparer.Ordinal),
                        "Production bill command-completion owner-family campaign composition changed during execution.");
                }
                else if (facilityEvolutionFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && facilityEvolutionOwner.Length > 0
                            && facilityEvolutionActions.SequenceEqual(
                                FacilityEvolutionActions,
                                StringComparer.Ordinal),
                        "Facility evolution two-stage owner-family campaign composition changed during execution.");
                }
                else if (facilitySynthesisFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && facilitySynthesisOwner.Length > 0
                            && facilitySynthesisActions.SequenceEqual(
                                FacilitySynthesisActions,
                                StringComparer.Ordinal),
                        "Facility synthesis two-stage owner-family campaign composition changed during execution.");
                }
                else if (infrastructurePowerFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && infrastructurePowerOwner.Length > 0
                            && infrastructurePowerActions.SequenceEqual(
                                InfrastructurePowerActions,
                                StringComparer.Ordinal),
                        "Infrastructure power disconnect-reconnect owner-family campaign composition changed during execution.");
                }
                else if (dungeonSpaceFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && dungeonSpaceOwner.Length > 0
                            && dungeonSpaceActions.SequenceEqual(
                                DungeonSpaceActions,
                                StringComparer.Ordinal),
                        "Dungeon-space quarry-stonecutting owner-family campaign composition changed during execution.");
                }
                else if (roomEnvironmentFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && roomEnvironmentOwner.Length > 0
                            && roomEnvironmentActions.SequenceEqual(
                                RoomEnvironmentActions,
                                StringComparer.Ordinal),
                        "Room experience-condition owner-family campaign composition changed during execution.");
                }
                else if (speciesIncidentFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && speciesIncidentOwner.Length > 0
                            && speciesIncidentActions.SequenceEqual(
                                SpeciesIncidentActions,
                                StringComparer.Ordinal),
                        "Species incident recurrence owner-family campaign composition changed during execution.");
                }
                else if (certifiedSeedFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && certifiedSeedOwner.Length > 0
                            && certifiedSeedActions.SequenceEqual(
                                CertifiedSeedActions,
                                StringComparer.Ordinal),
                        "Certified-seed repeated-completion owner-family campaign composition changed during execution.");
                }
                else if (staffResponseFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && staffResponseOwner.Length > 0
                            && staffResponseActions.SequenceEqual(
                                StaffResponseActions,
                                StringComparer.Ordinal),
                        "Staff isolation-calming owner-family campaign composition changed during execution.");
                }
                else if (emergencySuspensionFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && emergencySuspensionOwner.Length > 0
                            && emergencySuspensionActions.SequenceEqual(
                                EmergencySuspensionActions,
                                StringComparer.Ordinal),
                        "Emergency work-suspension owner-family campaign composition changed during execution.");
                }
                else if (bossAwardFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && bossAwardOwner.Length > 0
                            && bossAwardActions.SequenceEqual(
                                BossAwardActions,
                                StringComparer.Ordinal),
                        "Memory-erasure boss-award owner-family campaign composition changed during execution.");
                }
                else if (tradeInventoryFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && tradeInventoryOwner.Length > 0
                            && tradeInventoryActions.SequenceEqual(
                                TradeInventoryActions,
                                StringComparer.Ordinal),
                        "Trade-inventory three-producer owner-family campaign composition changed during execution.");
                }
                else if (diseaseRouteFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && diseaseRouteOwner.Length > 0
                            && diseaseRouteActions.SequenceEqual(
                                DiseaseRouteActions,
                                StringComparer.Ordinal),
                        "Disease-route repeated-exposure owner-family campaign composition changed during execution.");
                }
                else if (offenseTruthFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && offenseTruthOwner.Length > 0
                            && offenseTruthActions.SequenceEqual(
                                OffenseTruthActions,
                                StringComparer.Ordinal),
                        "Offense truth-reveal owner-family campaign composition changed during execution.");
                }
                else if (offenseNodeResolutionFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && offenseNodeResolutionOwner.Length > 0
                            && offenseNodeResolutionActions.SequenceEqual(
                                OffenseNodeResolutionActions,
                                StringComparer.Ordinal),
                        "Offense node-resolution owner-family campaign composition changed during execution.");
                }
                else if (workIdentityFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && workIdentityOwner.Length > 0
                            && workIdentityActions.SequenceEqual(
                                WorkIdentityActions,
                                StringComparer.Ordinal),
                        "Work-completion identity owner-family campaign composition changed during execution.");
                }
                else if (runVariableFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && runVariableOwner.Length > 0
                            && runVariableActions.SequenceEqual(
                                RunVariableActions,
                                StringComparer.Ordinal),
                        "Run operation-variable owner-family campaign composition changed during execution.");
                }
                else if (ownerRunCompletionFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && ownerRunCompletionOwner.Length > 0
                            && ownerRunCompletionActions.SequenceEqual(
                                OwnerRunCompletionActions, StringComparer.Ordinal),
                        "Owner completion/settlement/upgrade campaign composition changed during execution.");
                }
                else if (visitorReceptionCheckoutFamilyCampaign)
                {
                    const string campaignIdPrefix = "campaign:production-causal:";
                    Require(campaignId.StartsWith(campaignIdPrefix, StringComparison.Ordinal),
                        "Visitor reception/checkout campaign identity prefix is invalid.");
                    string campaignIdentity = campaignId.Substring(campaignIdPrefix.Length);
                    Require(campaignIdentity.Length == 64
                            && campaignIdentity.All(value => value is >= '0' and <= '9'
                                or >= 'a' and <= 'f')
                            && visitorReceptionCheckoutOwner
                                == "visitor-reception-checkout:causal-campaign:"
                                    + campaignIdentity + ":primary",
                        "Visitor reception/checkout campaign owner identity is invalid.");
                    Require(episodes.Count == VisitorReceptionCheckoutEpisodeTemplates.Length
                            && campaignNodeCount == VisitorReceptionCheckoutActions.Length
                            && episodes.Select(value => (string)value["templateId"])
                                .SequenceEqual(VisitorReceptionCheckoutEpisodeTemplates,
                                    StringComparer.Ordinal)
                            && changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && visitorReceptionCheckoutActions.SequenceEqual(
                                VisitorReceptionCheckoutActions, StringComparer.Ordinal),
                        "Visitor reception/checkout owner-family campaign composition changed during execution.");
                }
                else if (tabooButcheryFamilyCampaign)
                {
                    const string campaignIdPrefix = "campaign:production-causal:";
                    Require(campaignId.StartsWith(campaignIdPrefix, StringComparison.Ordinal),
                        "Taboo butchery campaign identity prefix is invalid.");
                    string campaignIdentity = campaignId.Substring(campaignIdPrefix.Length);
                    Require(campaignIdentity.Length == 64
                            && campaignIdentity.All(value => value is >= '0' and <= '9'
                                or >= 'a' and <= 'f')
                            && tabooButcheryOwner == "taboo-butchery:causal-campaign:"
                                + campaignIdentity + ":primary",
                        "Taboo butchery campaign owner identity is invalid.");
                    Require(episodes.Count == TabooButcheryEpisodeTemplates.Length
                            && campaignNodeCount == TabooButcheryActions.Length
                            && episodes.Select(value => (string)value["templateId"])
                                .SequenceEqual(TabooButcheryEpisodeTemplates, StringComparer.Ordinal)
                            && changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && tabooButcheryActions.SequenceEqual(TabooButcheryActions, StringComparer.Ordinal),
                        "Taboo butchery owner-family campaign composition changed during execution.");
                }
                else if (offenseArrivalFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && offenseArrivalOwner.Length > 0
                            && episodes.Count == OffenseArrivalEpisodeTemplates.Length
                            && campaignNodeCount == OffenseArrivalActions.Length
                            && episodes.Select(value => (string)value["templateId"])
                                .SequenceEqual(
                                    OffenseArrivalEpisodeTemplates,
                                    StringComparer.Ordinal)
                            && offenseArrivalActions.SequenceEqual(
                                OffenseArrivalActions,
                                StringComparer.Ordinal),
                        "Offense special-wildlife arrival owner-family campaign composition changed during execution.");
                }
                else if (festivalCelebrationFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && festivalCelebrationOwner.Length > 0
                            && episodes.Count == FestivalCelebrationEpisodeTemplates.Length
                            && campaignNodeCount == FestivalCelebrationActions.Length
                            && episodes.Select(value => (string)value["templateId"])
                                .SequenceEqual(
                                    FestivalCelebrationEpisodeTemplates,
                                    StringComparer.Ordinal)
                            && festivalCelebrationActions.SequenceEqual(
                                FestivalCelebrationActions,
                                StringComparer.Ordinal),
                        "Festival celebration owner-family campaign composition changed during execution.");
                }
                else if (metaProgressionFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && metaProgressionOwner.Length > 0
                            && metaProgressionActions.SequenceEqual(
                                MetaProgressionActions,
                                StringComparer.Ordinal),
                        "Meta run-progression owner-family campaign composition changed during execution.");
                }
                else if (regularCustomerFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && regularCustomerOwner.Length > 0
                            && regularCustomerActions.SequenceEqual(
                                RegularCustomerActions,
                                StringComparer.Ordinal),
                        "Regular-customer visit-recruit owner-family campaign composition changed during execution.");
                }
                else if (characterLifeFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && characterLifeOwner.Length > 0
                            && characterLifeActions.SequenceEqual(
                                CharacterLifeActions,
                                StringComparer.Ordinal),
                        "Character-life aging owner-family campaign composition changed during execution.");
                }
                else if (factionRouteFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && factionRouteOwner.Length > 0
                            && factionRouteActions.SequenceEqual(
                                FactionRouteActions,
                                StringComparer.Ordinal),
                        "Faction-route settlement-arrival-cargo owner-family campaign composition changed during execution.");
                }
                else if (consumablesFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && consumablesOwner.Length > 0
                            && consumablesActions.SequenceEqual(
                                ConsumablesActions,
                                StringComparer.Ordinal),
                        "Character-consumables meal-substance owner-family campaign composition changed during execution.");
                }
                else if (mealShortageFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && mealShortageOwner.Length > 0
                            && mealShortageActions.SequenceEqual(
                                MealShortageActions,
                                StringComparer.Ordinal),
                        "Survival meal-shortage owner-family campaign composition changed during execution.");
                }
                else if (primitiveSurvivalFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && primitiveSurvivalOwner.Length > 0
                            && primitiveSurvivalActions.SequenceEqual(
                                PrimitiveSurvivalActions,
                                StringComparer.Ordinal),
                        "Primitive-survival rest owner-family campaign composition changed during execution.");
                }
                else if (serviceHubDayFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && serviceHubDayOwner.Length > 0
                            && serviceHubDayActions.SequenceEqual(
                                ServiceHubDayActions,
                                StringComparer.Ordinal),
                        "Service-hub visit-mode-close owner-family campaign composition changed during execution.");
                }
                else if (invasionLifecycleFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && invasionLifecycleOwner.Length > 0
                            && invasionLifecycleActions.SequenceEqual(
                                InvasionLifecycleActions,
                                StringComparer.Ordinal),
                        "Invasion lifecycle owner-family campaign composition changed during execution.");
                }
                else if (combatLifeCycleFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && combatLifeCycleOwner.Length > 0
                            && combatLifeCycleActions.SequenceEqual(
                                CombatLifeCycleActions,
                                StringComparer.Ordinal),
                        "Combat injury-recovery-fatality owner-family campaign composition changed during execution.");
                }
                else if (stockSupplyTransformFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && stockSupplyTransformOwner.Length > 0
                            && stockSupplyTransformActions.SequenceEqual(
                                StockSupplyTransformActions,
                                StringComparer.Ordinal),
                        "Stock supply-transform owner-family campaign composition changed during execution.");
                }
                else if (facilityWishlistProcurementFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && facilityWishlistProcurementOwner.Length > 0
                            && facilityWishlistProcurementActions.SequenceEqual(
                                FacilityWishlistProcurementActions,
                                StringComparer.Ordinal),
                        "Facility wishlist procurement owner-family campaign composition changed during execution.");
                }
                else if (observedCareerArcFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && observedCareerArcOwner.Length > 0
                            && observedCareerArcActions.SequenceEqual(
                                ObservedCareerArcActions,
                                StringComparer.Ordinal),
                        "Observed career-arc owner-family campaign composition changed during execution.");
                }
                else if (observedMealIncidentResolutionFamilyCampaign)
                {
                    Require(changedSessionEpisodeCount == 0
                            && relationshipAdjustmentCount == 0
                            && observedMealIncidentResolutionOwner.Length > 0
                            && observedMealIncidentResolutionActions.SequenceEqual(
                                ObservedMealIncidentResolutionActions,
                                StringComparer.Ordinal),
                        "Observed meal-incident resolution owner-family campaign composition changed during execution.");
                }
                else
                {
                    Require(campaignTemplateCounts.TryGetValue(
                                CampaignWorldTemplate,
                                out int worldEpisodeCount)
                            && worldEpisodeCount == episodesPerCampaign - 1
                            && campaignTemplateCounts
                                .Where(pair => CampaignProtagonistTemplates.Contains(pair.Key))
                                .Sum(pair => pair.Value) == 1,
                        "Campaign template composition changed during execution.");
                    Require(changedSessionEpisodeCount == 1,
                        "Campaign protagonist state must change in exactly one protagonist-bearing episode.");
                    Require(relationshipAdjustmentCount > 0
                            && relationshipAdjustmentCount <= 9,
                        "Campaign relationship owner exceeded its state-aware trust limit.");
                }
                Require(campaignRestoreCount == episodesPerCampaign - 1
                        && campaignVerifiedRestoreCount == campaignRestoreCount,
                    "Campaign did not verify every episode restore boundary.");
                Require(campaignWorldRestoreCount == episodesPerCampaign - 1
                        && campaignWorldVerifiedRestoreCount
                            == campaignWorldRestoreCount,
                    "Campaign did not verify every world-checkpoint boundary.");
                ProductionCausalWorldCheckpoint.RequireEveryOwnerRestorable(
                    campaignFinalWorldCheckpoint,
                    "run:causal-world-audit:"
                        + campaignSha256.Substring("sha256:".Length));
                totalRestoreBoundaries += campaignRestoreCount;
                totalVerifiedRestores += campaignVerifiedRestoreCount;
                totalWorldRestoreBoundaries += campaignWorldRestoreCount;
                totalVerifiedWorldRestores +=
                    campaignWorldVerifiedRestoreCount;
                totalPersistentWorldOwners +=
                    ((JArray)campaignFinalWorldCheckpoint["owners"]).Count;
                totalRehydratedWorldOwners += campaignRehydratedOwnerCount;
                Require(campaignInitialWorldCheckpointPath.Length > 0
                        && campaignInitialWorldCheckpointSha256.Length > 0
                        && campaignFinalWorldCheckpointPath.Length > 0
                        && campaignFinalWorldCheckpointSha256.Length > 0,
                    "Campaign world-checkpoint file references are incomplete.");
                JObject campaignResult = new()
                {
                    ["schemaVersion"] = 5,
                    ["schema"] = CampaignResultSchema,
                    ["worldCheckpointDigestAlgorithm"] =
                        WorldCheckpointDigestAlgorithm,
                    ["status"] = "COMPLETE",
                    ["campaignId"] = campaignId,
                    ["campaignSha256"] = campaignSha256,
                    ["catalogVersion"] = (string)catalog["catalogVersion"],
                    ["catalogSha256"] = catalogSha256,
                    ["gameCommit"] = gameCommit,
                    ["protagonistId"] = protagonistId,
                    ["episodeCount"] = episodes.Count,
                    ["executedNodeCount"] = campaignNodeCount,
                    ["exactOutcomeCount"] = campaignOutcomeIds.Count,
                    ["restoreBoundaryCount"] = campaignRestoreCount,
                    ["verifiedRestoreCount"] = campaignVerifiedRestoreCount,
                    ["changedSessionEpisodeCount"] = changedSessionEpisodeCount,
                    ["worldRestoreBoundaryCount"] = campaignWorldRestoreCount,
                    ["verifiedWorldRestoreCount"] =
                        campaignWorldVerifiedRestoreCount,
                    ["introducedWorldOwnerCount"] =
                        campaignIntroducedOwnerCount,
                    ["rehydratedWorldOwnerCount"] =
                        campaignRehydratedOwnerCount,
                    ["persistentWorldOwnerCount"] =
                        ((JArray)campaignFinalWorldCheckpoint["owners"]).Count,
                    ["relationshipAdjustmentCount"] =
                        relationshipAdjustmentCount,
                    ["initialSessionState"] = campaignInitialState,
                    ["initialSessionStateSha256"] =
                        CausalGameplayCanonicalJson.Sha256(campaignInitialState),
                    ["finalSessionState"] = campaignFinalState,
                    ["finalSessionStateSha256"] =
                        CausalGameplayCanonicalJson.Sha256(campaignFinalState),
                    ["initialWorldCheckpointPath"] =
                        campaignInitialWorldCheckpointPath,
                    ["initialWorldCheckpointSha256"] =
                        campaignInitialWorldCheckpointSha256,
                    ["finalWorldCheckpointPath"] =
                        campaignFinalWorldCheckpointPath,
                    ["finalWorldCheckpointSha256"] =
                        campaignFinalWorldCheckpointSha256,
                    ["templateCounts"] = SortedCountObject(campaignTemplateCounts),
                    ["episodes"] = episodeRows,
                    ["actualGameplayProductionEntries"] = true,
                    ["controlledSimulation"] = true,
                    ["naturalPlayTelemetry"] = false,
                    ["humanApprovalClaimed"] = false,
                    ["trainingEligible"] = false
                };
                string campaignResultRelative = "results/"
                    + campaignOutputLabel
                    + "/campaign_result.json";
                WriteJson(
                    Path.Combine(staging, campaignResultRelative),
                    campaignResult);
                outputFiles.Add(campaignResultRelative);
                batchCampaigns.Add(new JObject
                {
                    ["campaignId"] = campaignId,
                    ["campaignSha256"] = campaignSha256,
                    ["protagonistId"] = protagonistId,
                    ["resultPath"] = campaignResultRelative,
                    ["resultSha256"] = Sha256File(Path.Combine(
                        staging,
                        campaignResultRelative)),
                    ["episodeCount"] = episodes.Count,
                    ["executedNodeCount"] = campaignNodeCount,
                    ["exactOutcomeCount"] = campaignOutcomeIds.Count,
                    ["persistentWorldOwnerCount"] =
                        ((JArray)campaignFinalWorldCheckpoint["owners"]).Count,
                    ["worldRestoreBoundaryCount"] = campaignWorldRestoreCount,
                    ["verifiedWorldRestoreCount"] =
                        campaignWorldVerifiedRestoreCount
                });
                campaignResult.RemoveAll();
                episodeRows.Clear();
                campaignFinalWorldCheckpoint.RemoveAll();
                campaignWorldCheckpoint?.RemoveAll();
                previousSessionState?.RemoveAll();
                campaignInitialState?.RemoveAll();
                campaignFinalState?.RemoveAll();
                sessionCheckpoint = null;
                campaignOutcomeIds.Clear();
                sessionOutcomeIds.Clear();
                persistentOwnerActions.Clear();
                defenseOwners.Clear();
                retailOwners.Clear();
                episodeLocalOwners.Clear();
                long retainedManagedBytes = GC.GetTotalMemory(
                    forceFullCollection: true);
                Debug.Log("Released campaign batch state "
                    + (campaignIndex + 1)
                    + "/"
                    + campaigns.Count
                    + "; retainedManagedBytes="
                    + retainedManagedBytes);
            }

            Require(globalPlannedActionIds.SetEquals(actionCounts.Keys),
                "Campaign batch did not execute every action selected by its plans.");
            Require(totalEvidence == globalOutcomeIds.Count
                    && totalSubjectBoundEvidence == totalEvidence
                    && totalVerifiedRestores == totalRestoreBoundaries
                    && totalVerifiedWorldRestores
                        == totalWorldRestoreBoundaries,
                "Campaign evidence or restore coverage is incomplete.");
            JObject batchResult = new()
            {
                ["schemaVersion"] = 5,
                ["schema"] = CampaignBatchSchema,
                ["worldCheckpointDigestAlgorithm"] =
                    WorldCheckpointDigestAlgorithm,
                ["status"] = "COMPLETE",
                ["catalogVersion"] = (string)catalog["catalogVersion"],
                ["catalogSha256"] = catalogSha256,
                ["gameCommit"] = gameCommit,
                ["campaignCount"] = campaigns.Count,
                ["protagonistCount"] = globalProtagonists.Count,
                ["episodeCount"] = totalEpisodes,
                ["executedNodeCount"] = totalNodes,
                ["exactOutcomeCount"] = globalOutcomeIds.Count,
                ["outcomeEvidenceCount"] = totalEvidence,
                ["subjectBoundEvidenceCount"] = totalSubjectBoundEvidence,
                ["protagonistBoundEvidenceCount"] = totalProtagonistEvidence,
                ["restoreBoundaryCount"] = totalRestoreBoundaries,
                ["verifiedRestoreCount"] = totalVerifiedRestores,
                ["worldRestoreBoundaryCount"] =
                    totalWorldRestoreBoundaries,
                ["verifiedWorldRestoreCount"] =
                    totalVerifiedWorldRestores,
                ["persistentWorldOwnerCount"] = totalPersistentWorldOwners,
                ["rehydratedWorldOwnerCount"] =
                    totalRehydratedWorldOwners,
                ["templateCounts"] = SortedCountObject(templateCounts),
                ["actionCounts"] = SortedCountObject(actionCounts),
                ["campaigns"] = batchCampaigns,
                ["actualGameplayProductionEntries"] = true,
                ["controlledSimulation"] = true,
                ["naturalPlayTelemetry"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            };
            string batchResultPath = Path.Combine(staging, "campaign_batch_result.json");
            WriteJson(batchResultPath, batchResult);
            outputFiles.Add("campaign_batch_result.json");
            JArray sourceFiles = new(SourceFiles(projectRoot));
            const string CampaignSource =
                "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/"
                + "MigratedProducers/Editor/ProductionCausalCampaignBatchExporter.cs";
            sourceFiles.Add(new JObject
            {
                ["path"] = CampaignSource,
                ["sha256"] = Sha256File(Path.Combine(
                    projectRoot,
                    CampaignSource.Replace('/', Path.DirectorySeparatorChar)))
            });
            JObject manifest = new()
            {
                ["schemaVersion"] = 5,
                ["schema"] = CampaignBatchSchema,
                ["worldCheckpointDigestAlgorithm"] =
                    WorldCheckpointDigestAlgorithm,
                ["version"] = canonicalVersion,
                ["status"] = "COMPLETE",
                ["gameCommit"] = gameCommit,
                ["workingTreeDirty"] = ReadGit(projectRoot, "status --porcelain").Length > 0,
                ["catalogVersion"] = (string)catalog["catalogVersion"],
                ["catalogSha256"] = catalogSha256,
                ["campaignCount"] = campaigns.Count,
                ["episodeCount"] = totalEpisodes,
                ["executedNodeCount"] = totalNodes,
                ["exactOutcomeCount"] = globalOutcomeIds.Count,
                ["restoreBoundaryCount"] = totalRestoreBoundaries,
                ["verifiedRestoreCount"] = totalVerifiedRestores,
                ["worldRestoreBoundaryCount"] =
                    totalWorldRestoreBoundaries,
                ["verifiedWorldRestoreCount"] =
                    totalVerifiedWorldRestores,
                ["persistentWorldOwnerCount"] = totalPersistentWorldOwners,
                ["rehydratedWorldOwnerCount"] =
                    totalRehydratedWorldOwners,
                ["actualGameplayProductionEntries"] = true,
                ["controlledSimulation"] = true,
                ["naturalPlayTelemetry"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false,
                ["batchResultSha256"] = Sha256File(batchResultPath),
                ["sourceFiles"] = sourceFiles
            };
            WriteJson(Path.Combine(staging, "manifest.json"), manifest);
            outputFiles.Add("manifest.json");
            WriteDelivery(staging, outputFiles.ToArray());
            Directory.Move(staging, destination);
            return destination;
        }
        catch (Exception exception)
        {
            if (Directory.Exists(staging))
            {
                WriteJson(Path.Combine(staging, "failure.json"), new JObject
                {
                    ["status"] = "FAILED", ["reason"] = exception.ToString(),
                    ["humanApprovalClaimed"] = false, ["trainingEligible"] = false,
                    ["releaseGate"] = "REJECT_BEFORE_DPO"
                });
                string failed = destination + ".failed-" + Guid.NewGuid().ToString("N");
                Directory.Move(staging, failed);
                Debug.LogError("CAUSAL_GAMEPLAY_CAMPAIGN_FAILED_EVIDENCE=" + failed);
            }
            throw;
        }
    }

    private static HashSet<string> ValidateCampaignPackageManifest(
        string packageRoot,
        JObject manifest,
        string gameCommit)
    {
        RequireFields(
            manifest,
            "campaign package manifest",
            "schemaVersion", "schema", "status", "catalogVersion",
            "catalogSha256", "gameCommit", "seed", "requestedCampaigns",
            "generatedCampaigns", "episodesPerCampaign", "episodeCount",
            "naturalPlayClaimed", "humanApprovalClaimed", "trainingEligible",
            "releaseGate", "files");
        Require((int)manifest["schemaVersion"] == 2
                && (string)manifest["schema"] == CampaignPackageSchema
                && (string)manifest["status"] == "COMPLETE"
                && (string)manifest["gameCommit"] == gameCommit,
            "Unsupported, incomplete or stale campaign package.");
        Require(!(bool)manifest["naturalPlayClaimed"]
                && !(bool)manifest["humanApprovalClaimed"]
                && !(bool)manifest["trainingEligible"]
                && (string)manifest["releaseGate"] == "REJECT_BEFORE_DPO",
            "Campaign package scope or approval gates are invalid.");
        JArray files = manifest["files"] as JArray
            ?? throw new InvalidOperationException(
                "Campaign package files must be an array.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (JObject file in files.Cast<JObject>())
        {
            RequireFields(file, "campaign package file", "path", "sha256");
            string relative = (string)file["path"];
            string absolute = SafePackagePath(packageRoot, relative);
            Require(seen.Add(relative)
                    && File.Exists(absolute)
                    && (string)file["sha256"] == "sha256:" + Sha256File(absolute),
                "Campaign package file is duplicated, missing or hash-invalid: "
                + relative);
        }
        Require(seen.Contains("action_catalog.json")
                && seen.Contains("campaigns.json"),
            "Campaign package lacks its catalog or campaign index.");
        return seen;
    }

    private static string CampaignOutputLabel(JObject campaign)
    {
        JArray episodes = campaign?["episodes"] as JArray
            ?? throw new InvalidOperationException(
                "Campaign episodes must be an array.");
        Require(episodes.Count > 0,
            "Campaign output identity requires at least one episode.");
        string label = string.Empty;
        for (int index = 0; index < episodes.Count; index++)
        {
            JObject episode = episodes[index] as JObject
                ?? throw new InvalidOperationException(
                    "Campaign episode must be an object.");
            string fileName = Path.GetFileName((string)episode["planPath"]);
            string suffix = "-episode-"
                + (index + 1).ToString("D4")
                + ".json";
            Require(fileName.EndsWith(suffix, StringComparison.Ordinal),
                "Campaign plan path does not carry its episode sequence.");
            string candidate = fileName.Substring(
                0,
                fileName.Length - suffix.Length);
            Require(candidate.StartsWith("campaign-", StringComparison.Ordinal)
                    && candidate.Length > "campaign-".Length
                    && candidate.Substring("campaign-".Length)
                        .All(char.IsDigit),
                "Campaign plan path does not carry a canonical campaign slot.");
            if (label.Length == 0)
                label = candidate;
            else
                Require(string.Equals(label, candidate, StringComparison.Ordinal),
                    "Campaign episodes do not share one output slot.");
        }
        return label;
    }

    private static void ValidateCampaignIdentity(
        JObject campaign,
        JObject catalog,
        string catalogSha256,
        string gameCommit,
        int episodesPerCampaign,
        ISet<string> protagonists)
    {
        RequireFields(
            campaign,
            "campaign",
            "schemaVersion", "schema", "kind", "campaignId", "seed",
            "catalogVersion", "catalogSha256", "gameCommit", "protagonistId",
            "humanApprovalClaimed", "trainingEligible", "episodes",
            "campaignSha256");
        Require((int)campaign["schemaVersion"] == 2
                && (string)campaign["schema"] == CampaignSchema
                && (string)campaign["kind"] == "unity-causal-gameplay-campaign",
            "Unsupported campaign schema or kind.");
        CausalGameplaySyntax.RequireId((string)campaign["campaignId"], "campaignId");
        string protagonistId = CausalGameplaySyntax.RequireId(
            (string)campaign["protagonistId"],
            "protagonistId");
        Require(protagonists.Add(protagonistId),
            "Campaign protagonist identity is duplicated.");
        Require((string)campaign["catalogVersion"] == (string)catalog["catalogVersion"]
                && (string)campaign["catalogSha256"] == catalogSha256
                && (string)campaign["gameCommit"] == gameCommit
                && !(bool)campaign["humanApprovalClaimed"]
                && !(bool)campaign["trainingEligible"],
            "Campaign catalog identity or approval gates are invalid.");
        JObject hashInput = (JObject)campaign.DeepClone();
        string declared = (string)hashInput["campaignSha256"];
        hashInput.Remove("campaignSha256");
        Require(declared == CausalGameplayCanonicalJson.Sha256(hashInput),
            "Campaign SHA-256 mismatch.");
        JArray episodes = campaign["episodes"] as JArray
            ?? throw new InvalidOperationException("Campaign episodes must be an array.");
        Require(episodes.Count == episodesPerCampaign,
            "Campaign episode count mismatch.");
    }

    private static void ValidateCampaignEpisodeLink(
        JObject episode,
        int expectedSequence,
        string previousEpisodeId)
    {
        RequireFields(
            episode,
            "campaign episode",
            "episodeId", "sequence", "planPath", "planId", "planSha256",
            "templateId", "worldOwnerBindings", "dependsOnEpisodeIds",
            "causalLinks");
        string episodeId = CausalGameplaySyntax.RequireId(
            (string)episode["episodeId"],
            "episodeId");
        Require((int)episode["sequence"] == expectedSequence,
            "Campaign episode sequence mismatch.");
        string[] dependencies = RequireStringArray(
            episode["dependsOnEpisodeIds"],
            "dependsOnEpisodeIds");
        JArray links = episode["causalLinks"] as JArray
            ?? throw new InvalidOperationException(
                "Campaign episode causalLinks must be an array.");
        if (previousEpisodeId.Length == 0)
        {
            Require(dependencies.Length == 0 && links.Count == 0,
                "First campaign episode cannot have a predecessor.");
            return;
        }
        Require(dependencies.SequenceEqual(
                    new[] { previousEpisodeId },
                    StringComparer.Ordinal)
                && links.Count == 1,
            "Campaign episode is not an exact topological chain.");
        JObject link = links[0] as JObject
            ?? throw new InvalidOperationException(
                "Campaign causal link must be an object.");
        RequireFields(link, "campaign causal link", "parentEpisodeId", "kind");
        Require((string)link["parentEpisodeId"] == previousEpisodeId
                && (string)link["kind"] == CampaignLinkKind
                && episodeId != previousEpisodeId,
            "Campaign causal link does not match the prior episode.");
    }

    private static IReadOnlyDictionary<string, string>
        ValidateCampaignOwnerBindings(
            JObject episode,
            CausalGameplayPlan plan,
            IDictionary<string, string> persistentOwnerActions,
            ISet<string> defenseOwners,
            ISet<string> retailOwners,
            ISet<string> episodeLocalOwners,
            ref string relationshipOwner,
            ref int relationshipAdjustmentCount,
            ref string cropOwner,
            IList<string> cropActions,
            ref string plainFireOwner,
            ref string physicalFireOwner,
            IList<string> fireActions,
            ref string socialOwner,
            IList<string> socialActions,
            ref string traitOwner,
            IList<string> traitActions,
            ref string captivityOwner,
            IList<string> captivityActions,
            ref string equipmentApparelOwner,
            IList<string> equipmentApparelActions,
            ref string researchKnowledgeOwner,
            IList<string> researchKnowledgeActions,
            ref string productionBillOwner,
            IList<string> productionBillActions,
            ref string facilityEvolutionOwner,
            IList<string> facilityEvolutionActions,
            ref string facilitySynthesisOwner,
            IList<string> facilitySynthesisActions,
            ref string infrastructurePowerOwner,
            IList<string> infrastructurePowerActions,
            ref string dungeonSpaceOwner,
            IList<string> dungeonSpaceActions,
            ref string roomEnvironmentOwner,
            IList<string> roomEnvironmentActions,
            ref string speciesIncidentOwner,
            IList<string> speciesIncidentActions,
            ref string certifiedSeedOwner,
            IList<string> certifiedSeedActions,
            ref string staffResponseOwner,
            IList<string> staffResponseActions,
            ref string emergencySuspensionOwner,
            IList<string> emergencySuspensionActions,
            ref string bossAwardOwner,
            IList<string> bossAwardActions,
            ref string tradeInventoryOwner,
            IList<string> tradeInventoryActions,
            ref string diseaseRouteOwner,
            IList<string> diseaseRouteActions,
            ref string offenseTruthOwner,
            IList<string> offenseTruthActions,
            ref string offenseNodeResolutionOwner,
            IList<string> offenseNodeResolutionActions,
            ref string workIdentityOwner,
            IList<string> workIdentityActions,
            ref string runVariableOwner,
            IList<string> runVariableActions,
            ref string ownerRunCompletionOwner,
            IList<string> ownerRunCompletionActions,
            ref string visitorReceptionCheckoutOwner,
            IList<string> visitorReceptionCheckoutActions,
            ref string tabooButcheryOwner,
            IList<string> tabooButcheryActions,
            ref string offenseArrivalOwner,
            IList<string> offenseArrivalActions,
            ref string festivalCelebrationOwner,
            IList<string> festivalCelebrationActions,
            ref string metaProgressionOwner,
            IList<string> metaProgressionActions,
            ref string regularCustomerOwner,
            IList<string> regularCustomerActions,
            ref string characterLifeOwner,
            IList<string> characterLifeActions,
            ref string factionRouteOwner,
            IList<string> factionRouteActions,
            ref string consumablesOwner,
            IList<string> consumablesActions,
            ref string mealShortageOwner,
            IList<string> mealShortageActions,
            ref string primitiveSurvivalOwner,
            IList<string> primitiveSurvivalActions,
            ref string serviceHubDayOwner,
            IList<string> serviceHubDayActions,
            ref string invasionLifecycleOwner,
            IList<string> invasionLifecycleActions,
            ref string combatLifeCycleOwner,
            IList<string> combatLifeCycleActions,
            ref string stockSupplyTransformOwner,
            IList<string> stockSupplyTransformActions,
            ref string facilityWishlistProcurementOwner,
            IList<string> facilityWishlistProcurementActions,
            ref string observedCareerArcOwner,
            IList<string> observedCareerArcActions,
            ref string observedMealIncidentResolutionOwner,
            IList<string> observedMealIncidentResolutionActions)
    {
        string[] expectedActions = plan.Nodes
            .Select(value => value.ActionId)
            .Where(value => PersistentFixtureActionIds.Contains(value)
                || EpisodeFixtureActionIds.Contains(value))
            .ToArray();
        Require(expectedActions.Distinct(StringComparer.Ordinal).Count()
                == expectedActions.Length,
            "Campaign fixture actions must be unique within an episode.");
        JArray bindings = episode["worldOwnerBindings"] as JArray
            ?? throw new InvalidOperationException(
                "Campaign worldOwnerBindings must be an array.");
        Require(bindings.Count == expectedActions.Length,
            "Campaign owner-binding count mismatch.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int index = 0; index < bindings.Count; index++)
        {
            JObject binding = bindings[index] as JObject
                ?? throw new InvalidOperationException(
                    "Campaign owner binding must be an object.");
            RequireFields(
                binding,
                "campaign owner binding",
                "actionId",
                "ownerId",
                "lifecycle");
            string actionId = CausalGameplaySyntax.RequireId(
                (string)binding["actionId"],
                "campaign owner binding actionId");
            string ownerId = CausalGameplaySyntax.RequireId(
                (string)binding["ownerId"],
                "campaign owner binding ownerId");
            string lifecycle = (string)binding["lifecycle"];
            Require(string.Equals(
                        actionId,
                        expectedActions[index],
                        StringComparison.Ordinal)
                    && result.TryAdd(actionId, ownerId),
                "Campaign owner bindings do not follow plan action order.");
            if (PersistentFixtureActionIds.Contains(actionId))
            {
                Require(lifecycle == "campaign-persistent",
                    "Persistent campaign owner has the wrong lifecycle.");
                string ownerFamily = PersistentFixtureOwnerFamilies[actionId];
                if (persistentOwnerActions.TryGetValue(
                        ownerId,
                        out string previousFamily))
                {
                    Require(previousFamily == ownerFamily,
                        "Persistent campaign owner was rebound to another owner family.");
                }
                else
                {
                    persistentOwnerActions.Add(ownerId, ownerFamily);
                }
                if (actionId == "action:trigger-defense-facility")
                {
                    Require(defenseOwners.Add(ownerId),
                        "A depleted defense facility owner was reused.");
                }
                else if (actionId == "action:purchase-retail-item")
                {
                    Require(retailOwners.Add(ownerId),
                        "A depleted retail owner was reused.");
                }
                else if (actionId == "action:adjust-faction-trust")
                {
                    if (relationshipOwner.Length > 0)
                    {
                        Require(relationshipOwner == ownerId,
                            "Campaign relationship owner changed.");
                    }
                    relationshipOwner = ownerId;
                    relationshipAdjustmentCount++;
                }
                else if (actionId is CropIrrigationAction or CropHarvestAction)
                {
                    if (cropOwner.Length > 0)
                    {
                        Require(cropOwner == ownerId,
                            "Campaign crop-plot owner changed.");
                    }
                    cropOwner = ownerId;
                    cropActions.Add(actionId);
                }
                else if (FireActions.Contains(actionId, StringComparer.Ordinal))
                {
                    if (PlainFireActionIds.Contains(actionId))
                    {
                        if (plainFireOwner.Length > 0)
                        {
                            Require(plainFireOwner == ownerId,
                                "Campaign plain-fire owner changed.");
                        }
                        plainFireOwner = ownerId;
                    }
                    else
                    {
                        if (physicalFireOwner.Length > 0)
                        {
                            Require(physicalFireOwner == ownerId,
                                "Campaign physical-fire owner changed.");
                        }
                        physicalFireOwner = ownerId;
                    }
                    fireActions.Add(actionId);
                }
                else if (SocialActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (socialOwner.Length > 0)
                    {
                        Require(socialOwner == ownerId,
                            "Campaign social relationship owner changed.");
                    }
                    socialOwner = ownerId;
                    socialActions.Add(actionId);
                }
                else if (TraitActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (traitOwner.Length > 0)
                    {
                        Require(traitOwner == ownerId,
                            "Campaign acquired-trait character owner changed.");
                    }
                    traitOwner = ownerId;
                    traitActions.Add(actionId);
                }
                else if (CaptivityActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (captivityOwner.Length > 0)
                    {
                        Require(captivityOwner == ownerId,
                            "Campaign captivity owner changed.");
                    }
                    captivityOwner = ownerId;
                    captivityActions.Add(actionId);
                }
                else if (EquipmentApparelActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (equipmentApparelOwner.Length > 0)
                    {
                        Require(equipmentApparelOwner == ownerId,
                            "Campaign equipment-apparel owner changed.");
                    }
                    equipmentApparelOwner = ownerId;
                    equipmentApparelActions.Add(actionId);
                }
                else if (ResearchKnowledgeActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (researchKnowledgeOwner.Length > 0)
                    {
                        Require(researchKnowledgeOwner == ownerId,
                            "Campaign research-knowledge owner changed.");
                    }
                    researchKnowledgeOwner = ownerId;
                    researchKnowledgeActions.Add(actionId);
                }
                else if (ProductionBillActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (productionBillOwner.Length > 0)
                    {
                        Require(productionBillOwner == ownerId,
                            "Campaign production-bill owner changed.");
                    }
                    productionBillOwner = ownerId;
                    productionBillActions.Add(actionId);
                }
                else if (FacilityEvolutionActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (facilityEvolutionOwner.Length > 0)
                    {
                        Require(facilityEvolutionOwner == ownerId,
                            "Campaign facility-evolution owner changed.");
                    }
                    facilityEvolutionOwner = ownerId;
                    facilityEvolutionActions.Add(actionId);
                }
                else if (FacilitySynthesisActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (facilitySynthesisOwner.Length > 0)
                    {
                        Require(facilitySynthesisOwner == ownerId,
                            "Campaign facility-synthesis owner changed.");
                    }
                    facilitySynthesisOwner = ownerId;
                    facilitySynthesisActions.Add(actionId);
                }
                else if (InfrastructurePowerActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (infrastructurePowerOwner.Length > 0)
                    {
                        Require(infrastructurePowerOwner == ownerId,
                            "Campaign infrastructure-power owner changed.");
                    }
                    infrastructurePowerOwner = ownerId;
                    infrastructurePowerActions.Add(actionId);
                }
                else if (DungeonSpaceActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (dungeonSpaceOwner.Length > 0)
                    {
                        Require(dungeonSpaceOwner == ownerId,
                            "Campaign dungeon-space owner changed.");
                    }
                    dungeonSpaceOwner = ownerId;
                    dungeonSpaceActions.Add(actionId);
                }
                else if (RoomEnvironmentActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (roomEnvironmentOwner.Length > 0)
                    {
                        Require(roomEnvironmentOwner == ownerId,
                            "Campaign room-environment owner changed.");
                    }
                    roomEnvironmentOwner = ownerId;
                    roomEnvironmentActions.Add(actionId);
                }
                else if (SpeciesIncidentActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (speciesIncidentOwner.Length > 0)
                    {
                        Require(speciesIncidentOwner == ownerId,
                            "Campaign species-incident owner changed.");
                    }
                    speciesIncidentOwner = ownerId;
                    speciesIncidentActions.Add(actionId);
                }
                else if (CertifiedSeedActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (certifiedSeedOwner.Length > 0)
                    {
                        Require(certifiedSeedOwner == ownerId,
                            "Campaign certified-seed owner changed.");
                    }
                    certifiedSeedOwner = ownerId;
                    certifiedSeedActions.Add(actionId);
                }
                else if (StaffResponseActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (staffResponseOwner.Length > 0)
                    {
                        Require(staffResponseOwner == ownerId,
                            "Campaign staff-response owner changed.");
                    }
                    staffResponseOwner = ownerId;
                    staffResponseActions.Add(actionId);
                }
                else if (EmergencySuspensionActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (emergencySuspensionOwner.Length > 0)
                    {
                        Require(emergencySuspensionOwner == ownerId,
                            "Campaign emergency-suspension owner changed.");
                    }
                    emergencySuspensionOwner = ownerId;
                    emergencySuspensionActions.Add(actionId);
                }
                else if (BossAwardActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (bossAwardOwner.Length > 0)
                    {
                        Require(bossAwardOwner == ownerId,
                            "Campaign memory-erasure boss-award owner changed.");
                    }
                    bossAwardOwner = ownerId;
                    bossAwardActions.Add(actionId);
                }
                else if (TradeInventoryActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (tradeInventoryOwner.Length > 0)
                    {
                        Require(tradeInventoryOwner == ownerId,
                            "Campaign trade-inventory owner changed.");
                    }
                    tradeInventoryOwner = ownerId;
                    tradeInventoryActions.Add(actionId);
                }
                else if (DiseaseRouteActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (diseaseRouteOwner.Length > 0)
                    {
                        Require(diseaseRouteOwner == ownerId,
                            "Campaign disease-route owner changed.");
                    }
                    diseaseRouteOwner = ownerId;
                    diseaseRouteActions.Add(actionId);
                }
                else if (OffenseTruthActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (offenseTruthOwner.Length > 0)
                    {
                        Require(offenseTruthOwner == ownerId,
                            "Campaign offense truth-reveal owner changed.");
                    }
                    offenseTruthOwner = ownerId;
                    offenseTruthActions.Add(actionId);
                }
                else if (OffenseNodeResolutionActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (offenseNodeResolutionOwner.Length > 0)
                    {
                        Require(offenseNodeResolutionOwner == ownerId,
                            "Campaign offense node-resolution owner changed.");
                    }
                    offenseNodeResolutionOwner = ownerId;
                    offenseNodeResolutionActions.Add(actionId);
                }
                else if (WorkIdentityActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (workIdentityOwner.Length > 0)
                    {
                        Require(workIdentityOwner == ownerId,
                            "Campaign work-identity owner changed.");
                    }
                    workIdentityOwner = ownerId;
                    workIdentityActions.Add(actionId);
                }
                else if (RunVariableActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (runVariableOwner.Length > 0)
                    {
                        Require(runVariableOwner == ownerId,
                            "Campaign run operation-variable owner changed.");
                    }
                    runVariableOwner = ownerId;
                    runVariableActions.Add(actionId);
                }
                else if (OwnerRunCompletionActions.Contains(actionId, StringComparer.Ordinal))
                {
                    if (ownerRunCompletionOwner.Length > 0)
                        Require(ownerRunCompletionOwner == ownerId, "Campaign run-completion owner changed.");
                    ownerRunCompletionOwner = ownerId;
                    ownerRunCompletionActions.Add(actionId);
                }
                else if (VisitorReceptionCheckoutActions.Contains(actionId, StringComparer.Ordinal))
                {
                    if (visitorReceptionCheckoutOwner.Length > 0)
                        Require(visitorReceptionCheckoutOwner == ownerId,
                            "Campaign visitor reception/checkout owner changed.");
                    visitorReceptionCheckoutOwner = ownerId;
                    visitorReceptionCheckoutActions.Add(actionId);
                }
                else if (TabooButcheryActions.Contains(actionId, StringComparer.Ordinal))
                {
                    if (tabooButcheryOwner.Length > 0)
                        Require(tabooButcheryOwner == ownerId,
                            "Campaign taboo butchery owner changed.");
                    tabooButcheryOwner = ownerId;
                    tabooButcheryActions.Add(actionId);
                }
                else if (OffenseArrivalActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (offenseArrivalOwner.Length > 0)
                    {
                        Require(offenseArrivalOwner == ownerId,
                            "Campaign offense-arrival owner changed.");
                    }
                    offenseArrivalOwner = ownerId;
                    offenseArrivalActions.Add(actionId);
                }
                else if (FestivalCelebrationActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (festivalCelebrationOwner.Length > 0)
                    {
                        Require(festivalCelebrationOwner == ownerId,
                            "Campaign festival-execution owner changed.");
                    }
                    festivalCelebrationOwner = ownerId;
                    festivalCelebrationActions.Add(actionId);
                }
                else if (MetaProgressionActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (metaProgressionOwner.Length > 0)
                    {
                        Require(metaProgressionOwner == ownerId,
                            "Campaign meta run-progression owner changed.");
                    }
                    metaProgressionOwner = ownerId;
                    metaProgressionActions.Add(actionId);
                }
                else if (RegularCustomerActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (regularCustomerOwner.Length > 0)
                    {
                        Require(regularCustomerOwner == ownerId,
                            "Campaign regular-customer owner changed.");
                    }
                    regularCustomerOwner = ownerId;
                    regularCustomerActions.Add(actionId);
                }
                else if (CharacterLifeActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (characterLifeOwner.Length > 0)
                    {
                        Require(characterLifeOwner == ownerId,
                            "Campaign character-life owner changed.");
                    }
                    characterLifeOwner = ownerId;
                    characterLifeActions.Add(actionId);
                }
                else if (FactionRouteActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (factionRouteOwner.Length > 0)
                    {
                        Require(factionRouteOwner == ownerId,
                            "Campaign faction-route owner changed.");
                    }
                    factionRouteOwner = ownerId;
                    factionRouteActions.Add(actionId);
                }
                else if (ConsumablesActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (consumablesOwner.Length > 0)
                    {
                        Require(consumablesOwner == ownerId,
                            "Campaign character-consumables owner changed.");
                    }
                    consumablesOwner = ownerId;
                    consumablesActions.Add(actionId);
                }
                else if (MealShortageActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (mealShortageOwner.Length > 0)
                    {
                        Require(mealShortageOwner == ownerId,
                            "Campaign survival meal-shortage owner changed.");
                    }
                    mealShortageOwner = ownerId;
                    mealShortageActions.Add(actionId);
                }
                else if (PrimitiveSurvivalActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (primitiveSurvivalOwner.Length > 0)
                    {
                        Require(primitiveSurvivalOwner == ownerId,
                            "Campaign primitive-survival owner changed.");
                    }
                    primitiveSurvivalOwner = ownerId;
                    primitiveSurvivalActions.Add(actionId);
                }
                else if (ServiceHubDayActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (serviceHubDayOwner.Length > 0)
                    {
                        Require(serviceHubDayOwner == ownerId,
                            "Campaign service-hub day owner changed.");
                    }
                    serviceHubDayOwner = ownerId;
                    serviceHubDayActions.Add(actionId);
                }
                else if (InvasionLifecycleActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (invasionLifecycleOwner.Length > 0)
                    {
                        Require(invasionLifecycleOwner == ownerId,
                            "Campaign invasion lifecycle owner changed.");
                    }
                    invasionLifecycleOwner = ownerId;
                    invasionLifecycleActions.Add(actionId);
                }
                else if (CombatLifeCycleActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (combatLifeCycleOwner.Length > 0)
                    {
                        Require(combatLifeCycleOwner == ownerId,
                            "Campaign combat life-cycle owner changed.");
                    }
                    combatLifeCycleOwner = ownerId;
                    combatLifeCycleActions.Add(actionId);
                }
                else if (StockSupplyTransformActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (stockSupplyTransformOwner.Length > 0)
                    {
                        Require(stockSupplyTransformOwner == ownerId,
                            "Campaign stock supply-transform owner changed.");
                    }
                    stockSupplyTransformOwner = ownerId;
                    stockSupplyTransformActions.Add(actionId);
                }
                else if (FacilityWishlistProcurementActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (facilityWishlistProcurementOwner.Length > 0)
                    {
                        Require(facilityWishlistProcurementOwner == ownerId,
                            "Campaign facility wishlist-procurement owner changed.");
                    }
                    facilityWishlistProcurementOwner = ownerId;
                    facilityWishlistProcurementActions.Add(actionId);
                }
                else if (ObservedCareerArcActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (observedCareerArcOwner.Length > 0)
                    {
                        Require(observedCareerArcOwner == ownerId,
                            "Campaign observed career-arc owner changed.");
                    }
                    observedCareerArcOwner = ownerId;
                    observedCareerArcActions.Add(actionId);
                }
                else if (ObservedMealIncidentResolutionActions.Contains(
                             actionId,
                             StringComparer.Ordinal))
                {
                    if (observedMealIncidentResolutionOwner.Length > 0)
                    {
                        Require(observedMealIncidentResolutionOwner == ownerId,
                            "Campaign observed meal-incident resolution owner changed.");
                    }
                    observedMealIncidentResolutionOwner = ownerId;
                    observedMealIncidentResolutionActions.Add(actionId);
                }
                continue;
            }

            Require(EpisodeFixtureActionIds.Contains(actionId)
                    && lifecycle == "episode-local"
                    && episodeLocalOwners.Add(ownerId),
                "Episode-local owner action, lifecycle or identity is invalid.");
        }
        return result;
    }

    private static int CountChangedOwners(JObject before, JObject after)
    {
        JArray beforeRows = before?["owners"] as JArray
            ?? throw new InvalidOperationException(
                "Before-world checkpoint has no owner array.");
        JArray afterRows = after?["owners"] as JArray
            ?? throw new InvalidOperationException(
                "After-world checkpoint has no owner array.");
        Dictionary<string, JObject> beforeOwners = beforeRows
            .Cast<JObject>()
            .ToDictionary(
                value => (string)value["ownerId"],
                value => value,
                StringComparer.Ordinal);
        Dictionary<string, JObject> afterOwners = afterRows
            .Cast<JObject>()
            .ToDictionary(
                value => (string)value["ownerId"],
                value => value,
                StringComparer.Ordinal);
        Require(beforeOwners.Count == afterOwners.Count
                && beforeOwners.Keys.All(afterOwners.ContainsKey),
            "Campaign execution added or removed a persistent owner after binding.");
        return beforeOwners.Count(pair => !JToken.DeepEquals(
            pair.Value,
            afterOwners[pair.Key]));
    }

    private static bool IsCaptivityFamilyCampaign(
        IReadOnlyDictionary<string, int> templateCounts,
        string terminalTemplate) =>
        templateCounts.Count == CaptivityCommonEpisodeTemplates.Length + 1
        && CaptivityCommonEpisodeTemplates.All(templateId =>
            templateCounts.TryGetValue(templateId, out int count)
            && count == 1)
        && templateCounts.TryGetValue(terminalTemplate, out int terminalCount)
        && terminalCount == 1;

    private static int CountBoundPersistentOwners(
        IReadOnlyDictionary<string, string> fixtureOwnerIds) =>
        (fixtureOwnerIds
            ?? throw new ArgumentNullException(nameof(fixtureOwnerIds)))
        .Where(pair => PersistentFixtureActionIds.Contains(pair.Key))
        .Select(pair => pair.Value)
        .Distinct(StringComparer.Ordinal)
        .Count();

    private static JObject PersistedCheckpoint(JToken checkpoint)
    {
        if (checkpoint == null)
            throw new ArgumentNullException(nameof(checkpoint));
        return JObject.Parse(checkpoint.ToString(
            Newtonsoft.Json.Formatting.None));
    }

    private static string WriteWorldCheckpointFile(
        string stagingRoot,
        string relativePath,
        JObject checkpoint)
    {
        if (string.IsNullOrWhiteSpace(stagingRoot))
            throw new ArgumentException("A staging root is required.", nameof(stagingRoot));
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("A checkpoint path is required.", nameof(relativePath));
        if (checkpoint == null)
            throw new ArgumentNullException(nameof(checkpoint));
        string path = Path.Combine(
            stagingRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        WriteJson(path, checkpoint);
        JObject delivered = StrictLoad(path);
        string digest = WorldCheckpointSha256(delivered);
        delivered.RemoveAll();
        return digest;
    }

    private static string WorldCheckpointSha256(JToken checkpoint)
    {
        if (checkpoint == null)
            throw new ArgumentNullException(nameof(checkpoint));
        using MemoryStream canonical = new();
        WriteWorldCheckpointToken(canonical, checkpoint);
        canonical.Position = 0;
        using SHA256 sha = SHA256.Create();
        return "sha256:" + string.Concat(sha.ComputeHash(canonical)
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static void WriteWorldCheckpointToken(Stream stream, JToken value)
    {
        switch (value.Type)
        {
            case JTokenType.Null:
                stream.WriteByte(0);
                return;
            case JTokenType.Boolean:
                stream.WriteByte((bool)value ? (byte)2 : (byte)1);
                return;
            case JTokenType.Integer:
                stream.WriteByte(3);
                WriteWorldCheckpointText(
                    stream,
                    Convert.ToString(
                        ((JValue)value).Value,
                        CultureInfo.InvariantCulture));
                return;
            case JTokenType.Float:
                stream.WriteByte(4);
                double number = Convert.ToDouble(
                    ((JValue)value).Value,
                    CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number))
                {
                    throw new InvalidOperationException(
                        "World checkpoints cannot contain non-finite numbers.");
                }
                WriteWorldCheckpointUInt64(
                    stream,
                    unchecked((ulong)BitConverter.DoubleToInt64Bits(number)));
                return;
            case JTokenType.String:
                stream.WriteByte(5);
                WriteWorldCheckpointText(stream, (string)value);
                return;
            case JTokenType.Array:
                JArray array = (JArray)value;
                stream.WriteByte(6);
                WriteWorldCheckpointUInt64(stream, (ulong)array.Count);
                foreach (JToken item in array)
                    WriteWorldCheckpointToken(stream, item);
                return;
            case JTokenType.Object:
                JObject obj = (JObject)value;
                JProperty[] properties = obj.Properties()
                    .OrderBy(property => property.Name, StringComparer.Ordinal)
                    .ToArray();
                stream.WriteByte(7);
                WriteWorldCheckpointUInt64(stream, (ulong)properties.Length);
                foreach (JProperty property in properties)
                {
                    WriteWorldCheckpointText(stream, property.Name);
                    WriteWorldCheckpointToken(stream, property.Value);
                }
                return;
            default:
                throw new InvalidOperationException(
                    "Unsupported world-checkpoint token type: " + value.Type);
        }
    }

    private static void WriteWorldCheckpointText(Stream stream, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        WriteWorldCheckpointUInt64(stream, (ulong)bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static void WriteWorldCheckpointUInt64(Stream stream, ulong value)
    {
        for (int shift = 56; shift >= 0; shift -= 8)
            stream.WriteByte((byte)(value >> shift));
    }

    private static bool SnapshotBindsEntity(JObject snapshot, string entityId)
    {
        foreach (string field in new[] { "participants", "subjects" })
        {
            if (snapshot[field] is not JArray values)
                continue;
            if (values.Cast<JObject>().Any(value =>
                    string.Equals(
                        (string)value["entityId"],
                        entityId,
                        StringComparison.Ordinal)))
            {
                return true;
            }
        }
        return false;
    }
}
#endif
