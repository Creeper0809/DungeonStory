#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Characters;
using DungeonStory.Foundation;
using DungeonStory.Factions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using VContainer;

/// <summary>
/// Controlled production commands in the authored world, never fabricated XP or
/// outcome rows. This is a request/checkpoint witness, not approved training data.
/// </summary>
public sealed class DungeonLiveNarrativeCampaignRunner
{
    [Serializable]
    private sealed class CommandWitness
    {
        public string command = "facility.interact";
        public string actorId;
        public string facilityId;
        public int levelBefore, experienceBefore, levelAfter, experienceAfter;
        public int usesBefore, usesAfter;
        public int traitScoreBefore, traitScoreAfter;
        public long hourBefore, hourAfter;
        public string executionPath = "controlled-facility-interact";
        public bool activeUseObserved, activeUseOfficialSaveCaptured;
        public bool publicOffDutyVisitCycleStarted;
        public bool publicTrainingVisitIntentRequested;
        public string activeUseCheckpointFile, actionType, useOperationId;
        public bool exactUseParentBound;
        public bool retryableMealSupplyCancelled, exactCancelledUseParentBound, actualMealSupersededUse;
        public string visitTerminalStatus, visitTerminalReason;
        public int actionBranch;
        public long useOperationSequence;
        public float registeredCandidateScore;
        public List<GameplayOutcomeSnapshot> publishedOutcomes = new();
        public List<GameplayOutcomeSnapshot> supersedingPhysicalMealOutcomes = new();
        public LiveNarrativeActivityWitness observedActivityLedger = new();
    }

    [Serializable]
    private sealed class DispatchWitness
    {
        public GameplayModuleDispatchEligibilityWitness dispatchEligibility;
        public string profileId, correlationId, model, schemaHash, wireSha256, checkpointSha256;
        public int schemaVersion, frame;
        public long absoluteHour;
        public bool batch, captureOnly = true, networkSent, responseApplied;
        public string ownerId;
        public CharacterSkillDraft pending;
        public List<GameplayOutcomeSnapshot> evidence = new();
    }

    [Serializable]
    private sealed class DispatchObservation
    {
        public string profileId, correlationId;
        public int attemptIndex;
        public bool replaying;
        public bool duringControlledAction;
    }

    [Serializable]
    private sealed class TraitDispatchWitness
    {
        public GameplayModuleDispatchEligibilityWitness dispatchEligibility;
        public string profileId, correlationId, model, schemaHash, wireSha256, checkpointSha256;
        public int schemaVersion, frame, experienceScore;
        public long absoluteHour;
        public bool captureOnly = true, networkSent, responseApplied;
        public string ownerId;
        public CharacterAcquiredTraitPendingRequestState pending;
        public List<GameplayOutcomeSnapshot> evidence = new();
        public List<AggregateEvidenceBinding> aggregateEvidence = new();
    }

    [Serializable]
    private sealed class AggregateEvidenceBinding
    {
        public string commandFile, publicFactId, evidenceKind;
    }

    [Serializable]
    private sealed class FacilityDispatchWitness
    {
        public GameplayModuleDispatchEligibilityWitness dispatchEligibility;
        public string profileId, correlationId, model, schemaHash, wireSha256, checkpointSha256;
        public int schemaVersion, frame;
        public long absoluteHour;
        public string facilityId;
        public bool captureOnly = true, networkSent, responseApplied;
        public FacilityEvolutionFormulaPresentationPendingSnapshot pending;
        public List<GameplayOutcomeSnapshot> evidence = new();
        public List<FacilityUsageEvidenceBinding> facilityUsageEvidence = new();
    }

    [Serializable]
    private sealed class EquipmentDispatchWitness
    {
        public GameplayModuleDispatchEligibilityWitness dispatchEligibility;
        public string profileId, correlationId, model, schemaHash, wireSha256,
            checkpointSha256;
        public int schemaVersion, frame;
        public long absoluteHour;
        public bool captureOnly = true, networkSent, responseApplied;
        public string ownerId, equipmentInstanceId, targetId;
        public EvolutionNode node;
        public EquipmentEvolutionPresentationRequest pending;
        public List<GameplayOutcomeSnapshot> evidence = new();
        public List<EquipmentEvolutionFormulaEvidenceRecord> formulaEvidence = new();
    }

    [Serializable]
    private sealed class EquipmentCombatWitness
    {
        public string command = "combat.force-fire-target";
        public string actorId, targetId, equipmentInstanceId,
            equipmentDefinitionId = "weapon:shortbow", failure;
        public bool publicCommandIssued, controlledEquipmentFixture = true,
            controlledDurableTargetFixture = true, controlledTargetAiPaused,
            transportQueueResetAtThreshold;
        public bool fabricatedExperience, fabricatedOutcome, naturalPlayClaimed;
        public int externalAmmunitionLoads, affinityBefore, affinityAfter,
            committedAttackOutcomes, evolutionNodeCount,
            presentationRequestCount, queuedBeforeThresholdReset,
            runningBeforeThresholdReset, queuedAfterThresholdReset,
            runningAfterThresholdReset;
        public string lastNodeGenerationFailure, queueBeforeThresholdReset,
            queueAfterThresholdReset;
        public Vector2Int actorCell, targetCellBefore, targetCell;
        public float observedRealSeconds, observedGameSeconds;
        public int observedFrames;
        public List<GameplayOutcomeSnapshot> outcomes = new();
    }

    [Serializable]
    private sealed class EquipmentMechanicWitness
    {
        public string consumerPath =
            "CharacterCombatCommandRuntime.TryIssueAttack -> CombatResolutionService.ResolveDetached -> CombatCommandResultApplier.Apply";
        public string ownerId, equipmentInstanceId, definitionId;
        public string benefitStatId, burdenStatId;
        public float accuracyBefore, accuracyApplied, accuracyRestored;
        public float damageBefore, damageApplied, damageRestored;
        public bool activeWeaponBound, accuracyBenefitApplied,
            damageBurdenApplied, officialProjectionRoundTrip;
        public bool damageBenefitApplied, accuracyBurdenApplied,
            selectedBenefitApplied, selectedBurdenApplied;
        public EquipmentAttackWitness attackApplied, attackRestored;
        public bool actualAttackExecuted, actualAttackRoundTrip;
    }

    [Serializable]
    private sealed class EquipmentAttackWitness
    {
        public string actorId, targetId, equipmentInstanceId, commandId;
        public string rawDamageOperationId;
        public float consumedCombatPower, calculatedRawDamage;
        public int rawDamageConsumerCount;
        public float targetHealthBefore, targetHealthAfter, accuracyMultiplier, damageMultiplier;
        public Vector2Int actorCell, targetCell;
        public int distance;
        public float rangeAccuracyMultiplier, actorLightLevel;
        public string lastCommandState, lastCommandStatus;
        public int lastCommandRevision;
        public int ammunitionBefore, ammunitionAfter, ammunitionLoads, ammunitionConsumed;
        public bool publicCommandIssued, exactEquipmentBound, actualDamageApplied, noDuplicateOutcomes;
        public List<GameplayOutcomeSnapshot> outcomes = new();
        public List<GameplayOutcomeSnapshot> damageOutcomes = new();
    }

    [Serializable]
    private sealed class FacilityMechanicWitness
    {
        public string consumerPath =
            "ServiceSessionRuntime.TryBeginSession/CreateContract/GetSupportSpeedMultiplier/TryCompleteSession";
        public string facilityId, formulaId, moduleId,
            statId = "service.support-speed";
        public float multiplierBefore, multiplierApplied, multiplierRestored;
        public bool currentFacilityBound, supportBenefitApplied,
            officialProjectionRoundTrip;
        public FacilityServiceWitness serviceBefore, serviceApplied, serviceRestored;
        public bool actualServiceBenefitApplied, actualServiceRoundTrip;
    }

    [Serializable]
    private sealed class FacilityServiceWitness
    {
        public string facilityId, hubId, supportId, processId, sessionId, terminalOutcomeId, terminalResultKey;
        public float serviceSeconds, elapsedGameSeconds, providedServiceSeconds;
        public bool completed, terminalAcknowledged, exactOutcome, duplicateRejected;
    }

    [Serializable]
    private sealed class SkillSelectionWitness
    {
        public string actorId, skillId, requestKey;
        public string selectionOrigin = "simulated-player-explicit-index-public-command";
        public int candidateIndex, unlockLevel, activeSkillsBefore, activeSkillsAfter;
        public int acquisitionOutcomesBefore, acquisitionOutcomesAfter, restoredSections;
        public long acquisitionRevisionBefore, acquisitionRevisionAfter;
        public bool unconfirmedAtomic, invalidIndexAtomic, committed,
            duplicateStable, differentChoiceRejected, officialFreshRestore,
            officialStateExact, officialEvidenceExact, restoredDuplicateStable;
        public List<GameplayOutcomeSnapshot> acquisitionOutcomes = new();
        public string requiredResearchId, researchQueueMessage;
        public bool expeditionUnlocked, researchQueued, researchRejectionAtomic;
        public List<string> queuedResearchProjects = new();
    }

    [Serializable]
    private sealed class SkillMechanicWitness
    {
        public string consumerPath = "public shop purchase -> real Haul -> simulated-player public Research commands -> Brain/AIWork/AbilityWork/WorkTaskExecutor/Research -> registered expedition -> public battle Ability command";
        public string actorId, skillId, researchId, expeditionId, battleId, targetId;
        public bool controlledNeutralPrerequisiteFixture = true;
        public bool preparationInstantWorkFixture, naturalPreparationWorkPassClaimed;
        public int startingDay, blueprintWaitStartingDay, purchaseDay, observedFrames, stabilizationPasses, restoredSections;
        public int unchangedApprovedNeedWritesSkipped;
        public int publicResearchCommands;
        public float elapsedGameSeconds, elapsedRealSeconds,
            calendarMaximumDeltaTime = 0.6f, physicalMaximumDeltaTime = 0.1f;
        public float researchGameTimeBound;
        public long approvedResearchTicks;
        public bool blueprintPurchased, blueprintArchived, researchCompleted, departureCompleted,
            actualEffect, officialFreshRestore, battleExact, expeditionExact, skillExact, evidenceExact,
            restoredDuplicateStable, restoredActualEffect, environmentExact, restoredWorkContextClearStable;
        public List<string> completedResearchProjects = new();
        public List<GameplayOutcomeSnapshot> researchOutcomes = new();
        public List<SkillBlueprintPurchaseWitness> purchases = new();
        public List<string> combatOwnerRejections = new();
        public List<string> publicPreparationCommands = new();
        public bool combatOwnerGuardsAtomic;
        public bool resumedExistingBattle;
        public int initiativeGuardChecks;
        public List<string> initiativeStateChecks = new();
        public bool researchPrerequisitesAlreadyComplete, researchQueueAlreadyOwned;
        public string formationControlOrigin = "simulated-player-public-expedition-formation-command";
        public bool legalSelectedSkillFormation, publicFormationSwap;
        public List<string> partyActorIds = new();
        public SkillRecruitmentWitness recruitment;
        public SkillNutritionWitness nutrition;
        public SkillBattleCommandWitness applied, reexecuted;
        public SkillReuseRouteWitness restoredReuseRoute;
    }

    [Serializable]
    private sealed class SkillReuseRouteWitness
    {
        public string origin = "simulated-player public formation/route/battle commands after finite battle completion; no damage, health, enemy or outcome injection";
        public string completedBattleId, expeditionId, actorId;
        public bool actualBattleInjuryObserved, terminalOfficialFreshRestore,
            legalFormationRestored, humanApprovalClaimed, trainingEligible;
        public float actorHealthBefore, actorHealthAfterBattle,
            injuryBattleStartingHealth, lowestObservedBattleHealth;
        public int publicBattleCommands, publicRouteCommands;
        public List<string> publicCommands = new();
        public List<string> battleIds = new();
        public List<string> terminalWorldArtifacts = new();
        public List<string> noSelfHealRecipientBattleIds = new();
    }

    [Serializable]
    private sealed class SkillNutritionWitness
    {
        public string origin = "controlled registered meal commands, actual food payment/consumption and calendar recovery; no health/meal-ledger injection";
        public string haulProofScope = "public duty/priority assignment and restoration, not a uniquely asserted carrier; actual Haul proof is separate";
        public int startingDay, endingDay, quantityBeforePurchase, quantityAfterPurchase,
            moneyBeforePurchase, moneyAfterPurchase;
        public bool foodPurchased, publicHaulAssigned, haulActivityExact, haulPriorityRestored,
            haulOnDutyApplied, haulDutyRestored, pressureReleased, restoredRecoveryAlreadySafe,
            nonTargetPreparationAlreadySafe,
            humanApprovalClaimed, trainingEligible;
        public DungeonSurvivalSaveData before, after;
        public List<CommandWitness> meals = new();
    }

    [Serializable]
    private sealed class SkillRecruitmentWitness
    {
        public string origin = "controlled public construction/visitor/registered facility-use/Recruit commands; explicit preparation response fixture, not model or training data";
        public string actorId, policyBefore, policyAfter, recruitMessage;
        public int residentCountBefore, sleepingSlotsBefore, sleepingSlotsAfter, preparationResponses;
        public bool preparedThroughProduction, enteredDungeon, admissionAccepted, recruited, deliveryApplied;
        public bool controlledHistoryCapacityFixture;
        public bool controlledOneClothPreparationFixture, clothFixtureCreatedNoGameplayOutcome;
        public string haulAssignmentActorId, originalHaulPriority;
        public bool publicHaulAssignmentIssued, haulAssignmentActivityExact, haulPriorityRestored, restoredHaulAssignmentRetained;
        public bool publicHaulDutyCommandIssued, haulOnDutyApplied, haulDutyRestored;
        public int clothBeforeFixture, clothAfterFixture;
        public List<int> housingRoomIds = new();
        public bool housingUsableRoomsSelected;
        public List<Vector2Int> plannedBedPositions = new();
        public bool duplicateRecruitStable, fabricatedVisits, fabricatedMood, fabricatedStaff, humanApprovalClaimed, trainingEligible;
        public List<ConstructionWitness> housingConstruction = new();
        public List<CommandWitness> visits = new();
        public List<float> satisfactionSamples = new();
        public List<GameplayOutcomeSnapshot> outcomes = new();
    }

    [Serializable]
    private sealed class SkillBlueprintPurchaseWitness
    {
        public int blueprintId, day, cost, moneyBefore, moneyAfter;
        public string purchaseKey, message, archiveDestination;
        public bool purchased, duplicateStable, archived;
        public int haulFrames;
        public float haulGameSeconds;
    }

    [Serializable]
    private sealed class SkillHaulDiagnostics
    {
        public string actorId, blueprintItemInstanceId, archiveDestination;
        public bool completed, itemIdentityPreserved, controlledNeutralFixture = true;
        public bool observedExistingCarrierWithoutCommands;
        public float gameSeconds, realSeconds, maximumDeltaTime;
        public int observedFrames;
        public List<SkillHaulFrame> frames = new();
    }

    [Serializable]
    private sealed class SkillHaulFrame
    {
        public int frame, x, y, heartbeat, updateHeartbeat, carryQuantity;
        public float gameSeconds, realSeconds, deltaTime;
        public long starts, terminals, replans, pathFailures;
        public bool hauling, blocked, archived;
        public string brain, brainPhase, stage, path, failure, terminal,
            moveFailure, cancellation, movementOwner;
    }

    [Serializable]
    private sealed class SkillResearchDiagnostics
    {
        public string actorId;
        public string researchControlOrigin = "simulated-player-public-work-commands";
        public int publicResearchCommands;
        public bool completed, controlledNeutralFixture = true;
        public bool preparationInstantWorkFixture, naturalWorkPassClaimed;
        public float gameSeconds, realSeconds, maximumDeltaTime = 0.1f, initialWork, finalWork;
        public float stationaryMaximumDeltaTime = 0.6f, researchGameTimeBound;
        public long approvedTicks, approvedResearchTicks;
        public List<string> completedProjects = new();
        public List<GameplayOutcomeSnapshot> completionOutcomes = new();
        public List<SkillResearchFrame> frames = new();
    }

    [Serializable]
    private sealed class SkillResearchFrame
    {
        public int frame, x, y, workRunId, completedProjects;
        public float gameSeconds, realSeconds, deltaTime, activeWork, totalWork;
        public float maximumDeltaTime, genericCompletedWork, genericRequiredWork;
        public long approvedTicks, approvedResearchTicks;
        public bool working, priorityTarget, offDuty, blocked, moving;
        public string activeProject, assignedWork, brain, brainPhase, preWuExit, cancellation, moveFailure;
    }

    [Serializable]
    private sealed class SkillBattleCommandWitness
    {
        public string battleId, actorId, targetId, abilityId, message, effectKind, targetRule, targetTeam;
        public long commandId;
        public int cooldownAfter, expectedCooldown, duplicateOutcomeCount;
        public float targetHealthBefore, targetHealthAfter, targetHealthAfterFollowingTurns, amount, appliedHealthChange;
        public bool accepted, exactOutcome, duplicateStable, commandBoundaryObserved, healthOutcomeExact, cooldownExact;
        public List<GameplayOutcomeSnapshot> outcomes = new();
    }

    [Serializable]
    private sealed class TraitMechanicWitness
    {
        public string consumerPath =
            "WorkCommandHandler -> AIBrain -> AIWork -> AbilityWork -> WorkTaskExecutor -> WorkAmountCalculator -> CleanWorkExecutionHandler";
        public float workRateBefore, workRateApplied;
        public bool workRateBenefitApplied, actualWorkExecuted, actualWorkRoundTrip;
        public TraitWorkWitness workApplied, workRestored;
        public string targetId, bindingId, controlledWeaponInstanceId;
        public float consumerValueBefore, consumerValueApplied;
        public bool consumerBenefitApplied, actualConsumerExecuted, actualConsumerRoundTrip;
        public EquipmentAttackWitness attackApplied, attackRestored;
    }

    [Serializable]
    private sealed class TraitWorkWitness
    {
        public string actorId, targetId, filthId, failure, brainAction, brainDetail,
            performanceFormulaId;
        public Vector2Int cell;
        public int workRunId, observedFrames, stabilizationPasses;
        public long approvedTicks;
        public float requiredWork, completedWork, elapsedGameSeconds, elapsedRealSeconds;
        public int actualConsumerTraceCount;
        public float performanceValue;
        public bool publicCommandIssued, actualProgressObserved, completed,
            exactOutcome, noDuplicateOutcomes, controlledNeutralFixture = true;
        public List<TraitWorkProgressWitness> progress = new();
        public List<GameplayOutcomeSnapshot> outcomes = new();
    }

    [Serializable]
    private sealed class TraitWorkProgressWitness
    {
        public float before, after, gameDelta, actualDelta, observedWorkPerGameSecond;
        public long revision;
        public bool singleApprovedTickObserved;
    }

    [Serializable]
    private sealed class FacilityCandidateWitness
    {
        public string facilityId, recipeId, reason;
        public bool approved;
    }

    [Serializable]
    private sealed class FacilityCandidateReport
    {
        public string eligibility;
        public List<FacilityCandidateWitness> candidates;
    }

    [Serializable]
    private sealed class MaterialTradeWitness
    {
        public string command = "faction.goodwill + faction.request-trade + actual route ticks";
        public string factionId, routeId, failure;
        public int trustBefore, trustAfter, gifts, ironBefore, ironAfter;
        public bool accepted, delivered, fabricatedResources;
        public long hourBefore, hourAfter;
        public List<GameplayOutcomeSnapshot> outcomes = new();
        public FactionRouteState route;
        public bool resumedExistingRoute;
        public float realSeconds, gameSeconds;
        public int frames, publicSpeedMultiplier;
    }

    [Serializable]
    private sealed class ClockWitness
    {
        public string phase;
        public float realSeconds, accumulatedGameSeconds, maximumDeltaTime;
        public float minScale = float.MaxValue, maxScale;
        public int frames, pausedFrames, deltaLimitFrames;
        public bool runInBackground;
    }

    [Serializable]
    private sealed class ContinuationWitness
    {
        public string sourcePath, sourceSha256, routeId, failure;
        public bool officialRestore, freshOwner, sessionExact, factionExact, outcomesPreserved, workOrdersExact;
        public bool consolidationEpochsAdvanced;
        public bool corruptRouteIdentityRejected, rejectedRestoreUnchanged;
        public bool sourceRouteDelivered, resumedRouteAdvanced;
        public long sourceLedgerEpoch, restoredLedgerEpoch;
        public string identityRejectionReason;
        public List<string> rejectionChangedSections = new();
        public int sourceRouteIndex;
        public float sourceRouteProgress;
        public int sections;
    }

    [Serializable]
    private sealed class ConstructionWitness
    {
        public string command = "building.place-construction-site + worker.priority-target";
        public string orderId, siteId, actorId, failure;
        public int buildingDefinitionId, x, y, publicSpeedMultiplier;
        public bool completed, fabricatedWork, resumedExistingOrder, sourceOrderExact, priorityCommandIssued;
        public bool preparationInstantWorkFixture, naturalWorkPassClaimed;
        public List<string> commandedWorkerIds = new();
        public List<string> priorityOneWorkerIds = new();
        public string materialAcquisitionOrigin = "public work-order request + observed autonomous hauling";
        public bool materialsReady;
        public float materialWaitRealSeconds, simulationMaximumDeltaTime;
        public float authoredWorkRate, workGameSecondsBudget, workGameSeconds;
        public int verifiedSelfCareProgressObservations;
        public int routineNeedSuspendedActivityCount, falseCompletedActivityCount;
        public int primitivePathSearchPendingActivityCount;
        public int materialWaitFrames, actorStabilizationPasses;
        public int environmentStabilizationPasses;
        public int environmentCandidatePreparations, environmentCandidateReuses;
        public double environmentPrepareMilliseconds, environmentRestoreMilliseconds;
        public int terminalLedgerPolls, activeOrderTerminalPollsSkipped;
        public int unchangedApprovedNeedWritesSkipped;
        public double approvedNeedRestoreMilliseconds;
        public bool neutralEnvironmentApplied, neutralEnvironmentCandidateHasNoControls;
        public WorkOrderSaveData before, after;
        public GameplayOutcomeSnapshot terminalOutcome;
        public List<GameplayOutcomeSnapshot> outcomes = new();
    }

    [Serializable]
    private sealed class HaulingWaitWitness
    {
        public string scope = "construction wait: published state and non-reserving planner diagnostics; path-query cache may be populated; no action or priority commands";
        public string observationPhase;
        public float realSeconds, gameTime;
        public int frame;
        public float deltaTime, unscaledDeltaTime, scale, maximumDeltaTime;
        public double schedulerMilliseconds;
        public string lastHaulReplan;
        public WorkOrderSaveData workOrder;
        public ServiceSessionSaveData[] activeServiceSessions;
        public List<HaulingActorWitness> actors = new();
    }

    [Serializable]
    private sealed class HaulingActorWitness
    {
        public string actorId, action, phase, detail, destination, brainSummary;
        public Vector3 worldPosition;
        public bool hasBrain, hasHaul, isHauling, hasHaulRoutine, hasWork, isWorking;
        public string haulStage, haulPlan, haulFailure, haulPath, haulTerminal, workDetail;
        public long haulStarts, haulTerminals;
        public int haulHeartbeat;
        public List<CharacterActivityEvent> recentActivity = new();
        public string haulPriority, priorityWorkType, priorityWorkTarget, emergencyWorkType;
        public bool emergencyWorkGate;
        public bool selectedAction, runningAction, decisionPending, actionExecuted, shoppingRoutine;
        public long gameplayProgressRevision, runtimeActionEpoch, externalIntentEpoch;
        public bool externalIntentActive;
        public string externalIntentOwner, externalIntentKind;
        public bool canRunAi, scoringPending, pathDeferred, preferredDeferred, decisionScheduled;
        public long schedulerProcessCount;
        public long immediateReplanCount, candidateResetCount, candidatePendingResetCount;
        public string lastImmediateReplanCaller, candidateLastResetCaller, candidateLastPendingResetDetail;
        public string candidatePendingDetail, preferredActionType;
        public string decisionTask, decisionStatus, decisionTrace;
        public float preferredActionUntil;
        public float decisionDelay;
        public string retryKind, lastActionFailure;
        public string selectedActionType, selectedDestinationId, runtimePhase, shoppingTargetId;
        public float hunger, sleep, hygiene, excretion, health;
        public List<HaulingCandidateWitness> materialCandidates = new();
        public List<CachedActionWitness> cachedActions = new();
    }

    [Serializable]
    private sealed class ConstructionHaulDiagnostics
    {
        public string orderId, siteId, destinationId, retryDetail;
        public bool sourceOrderExact, materialsReady, publicRetryIssued, completedBeforeRetry;
        public float realSeconds, gameSeconds;
        public double processCpuSeconds;
        public int frames;
        public WorkOrderSaveData before, after;
        public string assignedActorId, originalHaulPriority;
        public bool publicAssignmentIssued, assignmentActivityExact, priorityRestored;
    }

    [Serializable]
    private sealed class HaulingCandidateWitness
    {
        public string stackId, itemId, destinationId, reason;
        public Vector2Int position;
        public int quantity, availableQuantity;
        public bool evaluated, eligible;
    }

    [Serializable]
    private sealed class CachedActionWitness
    {
        public string action, failure, destinationId;
        public float score;
    }

    [Serializable]
    private sealed class CampaignReport
    {
        public string scope = "U3 skill, acquired-trait and facility controlled production-command request/checkpoint witnesses";
        public string executionOrigin = "controlled-production-command";
        public string initialSetup = "authored GameplayScene and controlled start-party preparation";
        public bool pilotInitialSeedConfigured;
        public int pilotInitialSeed;
        public string requestScheduling = "capture-only: cancellation drains the instantaneous queue, not producer retries; observe during-action dispatches but bind checkpoint witnesses only at completed command boundaries; cancel observed non-target skill and trait transports through public correlated cancellation to release response-less slots; target trait dispatch/retry/restore uses registered production ticks; resume the existing skill draft unchanged";
        public int suspendedSkillDrafts;
        public int cancelledQueuedRequests, cancelledRunningRequests;
        public int cancelledNonTargetSkillTransports,
            cancelledNonTargetTraitTransports;
        public bool passed, officialWorldRestore, freshPublishedOwner, pendingExact, wireReplayExact;
        public bool skillLaterContextChanged, skillLaterOutcomePreserved;
        public bool skillTriggerCommandBound, skillSelectedEvidenceContainsFacilityUse;
        public int skillTriggerCommandIndex;
        public string skillLaterCheckpointSha256;
        public List<GameplayOutcomeSnapshot> skillLaterOutcomes = new();
        public bool traitOfficialWorldRestore, traitFreshPublishedOwner, traitPendingExact, traitWireReplayExact;
        public bool traitCommandExactEvidenceBound, traitCommandActivityEvidenceBound;
        public bool resumedExistingTraitRequest, resumedTraitPendingExact, resumedTraitEvidenceBound;
        public List<string> resumedTraitOutcomeKeys = new();
        public int activityEvidenceContractChecks;
        public List<string> traitCommandActivityEvidence = new();
        public bool facilityOfficialWorldRestore, facilityFreshPublishedOwner, facilityPendingExact, facilityWireReplayExact;
        public bool equipmentOfficialWorldRestore, equipmentStateExact,
            equipmentPendingExact, equipmentWireReplayExact;
        public int equipmentCheckpointSections;
        public EquipmentCombatWitness equipmentCombat;
        public EquipmentMechanicWitness equipmentMechanic;
        public FacilityMechanicWitness facilityMechanic;
        public TraitMechanicWitness traitMechanic;
        public SkillSelectionWitness skillSelection;
        public SkillMechanicWitness skillMechanic;
        public SkillHaulDiagnostics skillHaulDiagnostics;
        public SkillResearchDiagnostics skillResearchDiagnostics;
        public ConstructionHaulDiagnostics constructionHaulDiagnostics;
        public bool originalSkillPendingDiagnosticsVerified;
        public int facilityCheckpointSections;
        public string facilitySelectionOrigin = "simulated-player-public-command";
        public string facilityEligibilityStatus = "not-evaluated";
        public bool facilityNonTargetPreparationFixture, facilityTradeSkippedAsPreparation;
        public List<FacilityCandidateWitness> facilityCandidates = new();
        public MaterialTradeWitness facilityMaterialTrade;
        public ConstructionWitness facilityConstruction;
        public string facilityUseOrigin;
        public List<GameplayOutcomeSnapshot> facilityObservedUseOutcomes = new();
        public string facilityVisitorActorId, facilityVisitorLastSpawnRejection;
        public int facilityVisitorSkillFixtureResponses;
        public bool facilityVisitorExternal, facilityVisitorFixtureApplied;
        public bool facilityVisitorPoolPreparationRequested;
        public int facilityVisitorProfilesBeforePreparation, facilityVisitorProfilesAfterPreparation;
        public string facilityVisitorPoolPreparationRejection;
        public string incompleteCheckpointSha256, incompleteCheckpointFailure;
        public ContinuationWitness continuation;
        public ClockWitness clock = new();
        public int initialTraitScore, traitCheckpointSections;
        public bool fullAuditClosure, humanApprovalClaimed, trainingEligible;
        public bool autonomousPolicyClaimed, fabricatedExperience, fabricatedOutcome, networkSent, responseApplied;
        public bool sourceV9SkillIntake, sourceV9EquipmentIntake,
            sourceV9FacilityIntake,
            sourceV9EquipmentReplay, sourceV9SkillReplay,
            sourceV9TraitReplay, sourceV9FacilityReplay,
            sourceV9PackageWritten, u4ProofWritten,
            equipmentMechanicExecuted, facilityMechanicExecuted;
        public bool skillSelectionCommitted, skillMechanicExecuted;
        public bool naturalInvasionHoldApplied, naturalInvasionDirectorRestored;
        public int naturalCandidateEventsObservedWhileHeld;
        public int newInvasionStartEventsWhileHeld;
        public string sourceV9ManifestSha256, u4ProofManifestSha256;
        public string gameOwnerId, skillRequestActorId;
        public bool skillExpeditionActor, skillAutoChoiceBefore;
        public int registeredSections, checkpointSections, dispatchCount;
        public string failure = string.Empty;
        public List<CommandWitness> commands = new();
        public List<DispatchObservation> dispatches = new();
        public List<string> warnings = new();
        public List<string> errors = new();
    }

    private readonly CampaignReport report = new();
    private DungeonRuntimeLifetimeScope scope;
    private IDungeonGameSaveService saves;
    private IGameCalendar calendar;
    private IGameTimeScaleController timeScale;
    private IGameSpeedController gameSpeed;
    private IGameplayOutcomeQuery outcomes;
    private IGameplayOutcomeNarrativeEvidenceQuery outcomeEvidence;
    private CharacterDerivedStatsSnapshotProjector derivedStats;
    private CharacterSkillGenerationService generation;
    private CharacterAcquiredTraitManifestationRuntime traitRuntime;
    private LocalLlmRequestQueue queue;
    private CharacterActor owner;
    private CharacterActor leasedActor;
    private CharacterSpawner pausedSpawner;
    private InvasionDirectorRuntime heldInvasionDirector;
    private bool invasionDirectorWasEnabled;
    private IDisposable heldInvasionCandidateObservation;
    private IDisposable heldInvasionStartedObservation;
    private HashSet<string> heldInitialInvasionIds;
    private bool controlledFacilityCommandActive;
    private bool spawnerWasPaused, ownsLease, replaying, traitPhase,
        facilityPhase, equipmentPhase, facilityVisitorResponseProviderConfigured;
    private CharacterActionIntentLease lease;
    private float originalScale, originalMaximumDeltaTime;
    private int originalGameSpeed;
    private bool originalGamePaused;
    private int targetUnlock;
    private bool skillSubmittedPromptCaptured;
    private string output, checkpointJson, capturedPrompt, capturedWire;
    private DispatchWitness firstDispatch;
    private readonly HashSet<string> intakeInitialSkillRequestKeys = new(StringComparer.Ordinal);
    private TraitDispatchWitness traitDispatch;
    private string traitCheckpointJson, traitPrompt, traitWire;
    private string resumedTraitPendingJson;
    private FacilityFormulaEvolutionPresentationRuntime facilityPresentation;
    private BuildableObject facilityOwner;
    private FacilityDispatchWitness facilityDispatch;
    private string facilityCheckpointJson, facilityPrompt, facilityWire;
    private EvolutionHistoryNarrativeRuntime equipmentPresentation;
    private ICombatEquipmentRuntime combatEquipment;
    private IEquipmentEvolutionRuntime equipmentEvolution;
    private ICharacterCombatCommandRuntime combatCommands;
    private CharacterActor equipmentTarget;
    private EquipmentDispatchWitness equipmentDispatch;
    private string equipmentCheckpointJson, equipmentPrompt, equipmentWire;
    private Exception callbackFailure;
    private readonly HashSet<string> nonTargetSkillTransports = new(StringComparer.Ordinal);
    private readonly HashSet<string> nonTargetTraitTransports = new(StringComparer.Ordinal);
    private string resumedTradeRouteId, phase = "initialization";
    private string facilityVisitorProfileName;
    private string facilityVisitorPreparationOwnerId;
    private readonly HashSet<string> facilityVisitorPreparationCaptures = new(StringComparer.Ordinal);
    private int facilityVisitorDefinitionId;
    private List<WorkOrderSaveData> sourceConstructionOrders = new();
    private GameplayModuleSelectionEquipmentReplayInput equipmentReplayInput;
    private LocalLlmCapturedRequest equipmentReplayCapture;
    private int equipmentReplayResponseCount;
    private GameplayModuleSelectionSkillReplayInput skillReplayInput;
    private LocalLlmCapturedRequest skillReplayCapture;
    private int skillReplayResponseCount;
    private bool skillAutomaticDispatchDeferred;
    private string skillRecruitmentPreparationOwnerId;
    private readonly HashSet<string> skillRecruitmentPreparationCaptures = new(StringComparer.Ordinal);
    private JObject skillSelectionBeforeArtifact, skillSelectionAppliedArtifact,
        skillSelectionRestoredArtifact;
    private JObject skillMechanicBeforeArtifact, skillMechanicAppliedArtifact,
        skillMechanicRestoredArtifact;
    private JObject[] skillSelectionInfluenceDeltas;
    private string[] skillSelectionModules, skillSelectionFacts;
    private string skillSelectionFormulaId;
    private GameplayModuleSelectionTraitReplayInput traitReplayInput;
    private LocalLlmCapturedRequest traitReplayCapture;
    private int traitReplayResponseCount;
    private GameplayModuleSelectionFacilityReplayInput facilityReplayInput;
    private LocalLlmCapturedRequest facilityReplayCapture;
    private int facilityReplayResponseCount;
    private readonly bool sourceV9SkillIntake = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_SKILL_INTAKE"),
        "1",
        StringComparison.Ordinal);
    private readonly bool sourceV9SkillExpeditionActor = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_SKILL_EXPEDITION_ACTOR"),
        "1",
        StringComparison.Ordinal);
    private readonly bool sourceV9EquipmentIntake = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_EQUIPMENT_INTAKE"),
        "1",
        StringComparison.Ordinal);
    private readonly bool sourceV9FacilityIntake = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_FACILITY_INTAKE"),
        "1",
        StringComparison.Ordinal);
    private readonly bool sourceV9EquipmentReplay = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_EQUIPMENT_REPLAY"),
        "1",
        StringComparison.Ordinal);
    private readonly bool sourceV9SkillReplay = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_SKILL_REPLAY"),
        "1",
        StringComparison.Ordinal);
    private readonly bool skillHaulDiagnostics = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_BLUEPRINT_HAUL_DIAGNOSTICS"),
        "1", StringComparison.Ordinal);
    private readonly bool skillReplayHoldNaturalInvasion = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_REPLAY_HOLD_NATURAL_INVASION"),
        "1", StringComparison.Ordinal);
    private readonly bool skillNormalWeaponPreparation = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_NORMAL_WEAPON_PREPARATION"),
        "1", StringComparison.Ordinal);
    private readonly bool skillOneCraftingBenchFixture = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_ONE_CRAFTING_BENCH_FIXTURE"),
        "1", StringComparison.Ordinal);
    private readonly bool skillNonTargetHungerFixture = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_NON_TARGET_HUNGER_FIXTURE"),
        "1", StringComparison.Ordinal);
    private readonly bool skillEquipmentPreparationFixture = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_EQUIPMENT_PREPARATION_FIXTURE"),
        "1", StringComparison.Ordinal);
    private readonly bool skillPreparationEnvironmentFixture = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_PREPARATION_ENVIRONMENT_FIXTURE"),
        "1", StringComparison.Ordinal);
    private readonly bool skillPreparationInstantWorkFixture = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_PREPARATION_INSTANT_WORK_FIXTURE"),
        "1", StringComparison.Ordinal);
    private CharacterEnvironmentUnityAdapter nonTargetEnvironmentFixtureRuntime;
    private IDisposable nonTargetExposureHold;
    private DungeonDebugRuleRuntime nonTargetInstantWorkFixtureRuntime;
    private IDisposable nonTargetInstantWorkHold;
    private bool nonTargetEnvironmentFixtureScopeWritten;
    private float nextNonTargetHungerFixtureGameTime = float.NegativeInfinity;
    private bool nonTargetHungerFixtureScopeWritten;
    private string[] nonTargetHungerResidentIds;
    private SurvivalFoodRuntime nonTargetNutritionFixtureRuntime;
    private IDisposable nonTargetDailyNutritionHold;
    private bool nonTargetPreparationFixturesClosed;
    private readonly bool skillThirtyFoodPreparationFixture = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_THIRTY_FOOD_PREPARATION_FIXTURE"),
        "1", StringComparison.Ordinal);
    private bool skillThirtyFoodPreparationApplied;
    private readonly bool skillRecruitmentHistoryCapacityFixture = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_RECRUITMENT_HISTORY_CAPACITY_FIXTURE"),
        "1", StringComparison.Ordinal);
    private RegularCustomerRuntime historyCapacityFixtureOwner;
    private ISettlementPopulationCapacityQuery historyCapacityFixtureOriginal;
    private SupportHistoryCapacityFixture historyCapacityFixture;
    private bool skillCraftingBenchFixtureCreated;
    private bool skillResidentWeaponsPreparedBeforeAdmission;
    private int supportWorkFunVisitSequence;
    private int supportWorkMealVisitSequence;
    private readonly bool skillResearchDiagnostics = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_RESEARCH_DIAGNOSTICS"),
        "1", StringComparison.Ordinal);
    private readonly bool skillBattleDiagnostics = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_BATTLE_DIAGNOSTICS"),
        "1", StringComparison.Ordinal);
    private readonly bool completedCraftDeliveryDiagnostics = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_COMPLETED_CRAFT_DELIVERY_DIAGNOSTICS"),
        "1", StringComparison.Ordinal);
    private bool completedCraftDeliveryVerified;
    private readonly bool originalSkillPendingDiagnostics = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_ORIGINAL_SKILL_PENDING_DIAGNOSTICS"),
        "1", StringComparison.Ordinal);
    private LocalLlmCapturedRequest originalSkillPendingCapture;
    private int originalSkillPendingAttempt;
    private const string OriginalSkillPendingOwner = "character:staff:1:02";
    private const string OriginalSkillPendingKey = "skill:character:staff:1:02:Active:5:r1:g1:l1";
    private const string OriginalSkillPendingNativeWireSha256 =
        "sha256:4fc97bca5affa212c78ffb7b54bbf41191be5976f2c92b6ee52acf3e97902527";
    private readonly string skillBattleActorId = Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_BATTLE_ACTOR_ID");
    private readonly string skillBattleSkillId = Environment.GetEnvironmentVariable("DUNGEONSTORY_SKILL_BATTLE_SKILL_ID");
    private bool SkillSupportContinuation => skillBattleDiagnostics && !string.IsNullOrEmpty(skillBattleActorId);
    private readonly bool constructionHaulDiagnostics = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_CONSTRUCTION_HAUL_DIAGNOSTICS"),
        "1", StringComparison.Ordinal);
    private bool SkillAdmissionDiagnostics => skillHaulDiagnostics || skillResearchDiagnostics || skillBattleDiagnostics || constructionHaulDiagnostics || completedCraftDeliveryDiagnostics || originalSkillPendingDiagnostics;
    private readonly bool sourceV9TraitReplay = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_TRAIT_REPLAY"),
        "1",
        StringComparison.Ordinal);
    private readonly bool sourceV9FacilityReplay = string.Equals(
        Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_FACILITY_REPLAY"),
        "1",
        StringComparison.Ordinal);

    public DungeonLiveNarrativeCampaignRunner(IEnumerable<string> earlyWarnings, IEnumerable<string> earlyErrors)
    {
        report.warnings.AddRange(earlyWarnings);
        report.errors.AddRange(earlyErrors);
        Application.logMessageReceived += CaptureLog;
        DungeonFullWorldRoundTripPlayModeFacade.StopEarlyCaptureAndDrain(report.warnings, report.errors);
    }

    public static void RequestSkillBlueprintHaulDiagnostics()
        => RequestSkillAdmissionDiagnostics("DUNGEONSTORY_SKILL_BLUEPRINT_HAUL_DIAGNOSTICS");

    public static void RequestSkillResearchDiagnostics()
        => RequestSkillAdmissionDiagnostics("DUNGEONSTORY_SKILL_RESEARCH_DIAGNOSTICS");

    public static void RequestSkillBattleDiagnostics()
        => RequestSkillAdmissionDiagnostics("DUNGEONSTORY_SKILL_BATTLE_DIAGNOSTICS");

    public static void RequestConstructionHaulDiagnostics()
        => RequestSkillAdmissionDiagnostics("DUNGEONSTORY_CONSTRUCTION_HAUL_DIAGNOSTICS");

    public static void RequestCompletedCraftDeliveryDiagnostics()
        => RequestSkillAdmissionDiagnostics("DUNGEONSTORY_COMPLETED_CRAFT_DELIVERY_DIAGNOSTICS");

    public static void RequestOriginalSkillPendingDiagnostics()
        => RequestSkillAdmissionDiagnostics("DUNGEONSTORY_ORIGINAL_SKILL_PENDING_DIAGNOSTICS");

    private static void RequestSkillAdmissionDiagnostics(string diagnosticVariable)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE") == "1"
                && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD")),
            "Skill admission diagnostics require an explicitly hashed world and isolated campaign batch.");
        if (diagnosticVariable == "DUNGEONSTORY_SKILL_BATTLE_DIAGNOSTICS")
        {
            OffenseAggregateSaveV18DebugScenarios.Run();
            OffenseBattleDebugScenarios.RunTreatmentMembershipRegression();
            OffenseAggregateSaveV18DebugScenarios.RunTreatmentParticipantSaveRegression();
        }
        Environment.SetEnvironmentVariable(diagnosticVariable, "1");
        DungeonFullWorldRoundTripPlayModeFacade.RequestIsolatedBatch();
    }

    public IEnumerator Run()
    {
        File.Delete(DungeonFullWorldRoundTripPlayModeFacade.RequestPath);
        output = Path.GetDirectoryName(DungeonPersistencePaths.Root);
        originalMaximumDeltaTime = Time.maximumDeltaTime;
        // Drive nested iterators ourselves so exceptions in movement/interaction
        // terminate this run with a durable failure report rather than a hung editor.
        var stack = new Stack<IEnumerator>();
        stack.Push(RunCampaign());
        float started = Time.realtimeSinceStartup;
        // A skill replay includes up to four real 180-game-second midnights,
        // Haul/research/departure and battle, not just one facility interaction.
        // r4 measured 172 game seconds / 165 real seconds on the isolated editor.
        // With the established 0.6-second frame cap, budget the complete planned
        // simulation here; phase game-time bounds and actual-effect gates remain.
        // Actual r6 shop/haul took about 450s; r5's remaining 112/164 authored
        // Research WU took 485s. The whole replay also includes departure/travel
        // and combat restore. Keep independent phase/no-progress guards intact.
        float deadline = started + (sourceV9SkillReplay ? 1800f : SkillSupportContinuation ? 900f : skillResearchDiagnostics ? 660f : 240f);
        float nextClockWrite = started + 15f;
        int lastClockFrame = -1, clockWrite = 0;
        while (stack.Count > 0)
        {
            object next = null;
            bool moved = false;
            try
            {
                if (lastClockFrame != Time.frameCount)
                {
                    lastClockFrame = Time.frameCount;
                    report.clock.phase = phase;
                    report.clock.realSeconds = Time.realtimeSinceStartup - started;
                    report.clock.accumulatedGameSeconds += Time.deltaTime;
                    report.clock.frames++;
                    report.clock.pausedFrames += Time.timeScale <= 0f ? 1 : 0;
                    report.clock.deltaLimitFrames += Mathf.Approximately(Time.deltaTime, Time.maximumDeltaTime) ? 1 : 0;
                    report.clock.maximumDeltaTime = Time.maximumDeltaTime;
                    report.clock.minScale = Mathf.Min(report.clock.minScale, Time.timeScale);
                    report.clock.maxScale = Mathf.Max(report.clock.maxScale, Time.timeScale);
                    report.clock.runInBackground = Application.runInBackground;
                    if (Time.realtimeSinceStartup >= nextClockWrite)
                    {
                        WriteNew($"clock-progress-{++clockWrite:D3}.json", JsonUtility.ToJson(report.clock, true));
                        nextClockWrite = Time.realtimeSinceStartup + 15f;
                    }
                }
                Require(Time.realtimeSinceStartup < deadline,
                    "Live execution slice reached its real-time bound; incomplete world is preserved, not a gameplay failure or completion.");
                if (callbackFailure != null) throw callbackFailure;
                // Defer cancellation until outside the queue's capture callback.
                // No fabricated model reply or producer state replacement.
                foreach (string key in nonTargetSkillTransports)
                {
                    queue.CancelCharacterSkillRequest(key);
                    report.cancelledNonTargetSkillTransports++;
                }
                nonTargetSkillTransports.Clear();
                foreach (string key in nonTargetTraitTransports)
                {
                    queue.CancelAcquiredTraitRequest(key);
                    report.cancelledNonTargetTraitTransports++;
                }
                nonTargetTraitTransports.Clear();
                if (sourceV9FacilityIntake && facilityVisitorResponseProviderConfigured)
                {
                    generation.Tick();
                    foreach (string id in generation.CapturePendingModuleDispatchIdsForOwner(
                                 facilityVisitorPreparationOwnerId))
                    {
                        if (facilityVisitorPreparationCaptures.Add(id))
                            Require(queue.TryDispatchQueuedRequestForIsolatedCapture(id),
                                "Selected facility visitor's actual preparation request could not dispatch.");
                    }
                }
                moved = stack.Peek().MoveNext();
                if (moved) next = stack.Peek().Current;
            }
            catch (Exception exception)
            {
                report.failure = exception.ToString();
                break;
            }
            if (!moved)
            {
                (stack.Pop() as IDisposable)?.Dispose();
                continue;
            }
            if (next is IEnumerator child) stack.Push(child);
            else yield return next;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        try { EndSupportHistoryCapacityFixture(); }
        catch (Exception error)
        {
            report.failure = (report.failure ?? string.Empty) + "\nHistorical-capacity fixture cleanup failed: " + error;
        }
        if (saves != null && !string.IsNullOrEmpty(report.failure) && (facilityPhase || sourceV9SkillIntake || sourceV9SkillReplay || SkillAdmissionDiagnostics) && !ownsLease
            && (report.continuation == null || report.continuation.officialRestore))
        {
            // Natural route/work state is durable. Preserve the partial actual
            // world on a bounded failure, without asserting resume validation.
            try
            {
                timeScale.Scale = 0;
                string incomplete = saves.ToJson(saves.Capture());
                WriteNew("incomplete-world.json", incomplete);
                report.incompleteCheckpointSha256 = Hash(Encoding.UTF8.GetBytes(incomplete));
                if (report.skillMechanic != null)
                    WriteNew("u4-skill-mechanic-incomplete-witness.json",
                        JsonUtility.ToJson(report.skillMechanic, true));
                if (report.skillHaulDiagnostics != null)
                    WriteNew("skill-haul-incomplete-diagnostics.json", JsonUtility.ToJson(report.skillHaulDiagnostics, true));
                if (report.skillResearchDiagnostics != null)
                    WriteNew("skill-research-incomplete-diagnostics.json", JsonUtility.ToJson(report.skillResearchDiagnostics, true));
            }
            catch (Exception error)
            {
                report.incompleteCheckpointFailure = error.ToString();
                // An invalid join cannot be a partial restore world. Retain only
                // raw owner observations for diagnosis, without rewriting DTOs
                // or relaxing the strict save boundary.
                try
                {
                    CharacterActor[] actors = scope.Container.Resolve<ICharacterLifetimeQuery>()
                        .AllCharacters.Where(value => value != null && value.Identity != null).ToArray();
                    var bindings = new JArray(scope.Container.Resolve<ICharacterPopulationService>()
                        .CaptureProfiles().Where(profile => profile != null).Select(profile => new JObject
                        {
                            ["persistentId"] = profile.persistentId, ["dataId"] = profile.characterDataId,
                            ["isAlive"] = profile.isAlive, ["isVisiting"] = profile.isVisiting,
                            ["isStaff"] = profile.isStaff, ["standing"] = profile.settlementStanding.ToString(),
                            ["actors"] = new JArray(actors.Where(actor =>
                                actor.Identity.PersistentId == profile.persistentId).Select(actor => new JObject
                                {
                                    ["dataId"] = actor.Identity.Data != null ? actor.Identity.Data.id : -1,
                                    ["characterType"] = actor.Identity.CharacterType.ToString(),
                                    ["lifecycle"] = actor.CurrentLifecycleState.ToString(),
                                    ["isDead"] = actor.IsDead,
                                    ["activeInHierarchy"] = actor.gameObject.activeInHierarchy,
                                    ["normallyPersistent"] = CharacterWorldPersistenceRules.IsPersistentActor(actor)
                                }))
                        }));
                    WriteNew("failed-character-world-bindings.json", new JObject
                    {
                        ["origin"] = "raw live owner observations after strict save capture rejection",
                        ["captureFailure"] = error.ToString(), ["bindings"] = bindings,
                        ["restorableWorld"] = false, ["stateChangedToRepairSave"] = false,
                        ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
                    }.ToString());
                }
                catch (Exception diagnosticError)
                {
                    WriteNew("failed-character-world-bindings-diagnostic-error.txt", diagnosticError.ToString());
                }
            }
        }
        if (ownsLease && leasedActor != null)
            leasedActor.Brain.CancelExternallyDrivenAction(lease);
        if (queue != null && facilityVisitorResponseProviderConfigured)
        {
            queue.ClearIsolatedFacilityVisitorTestResponses();
            facilityVisitorResponseProviderConfigured = false;
        }
        if (queue != null && skillAutomaticDispatchDeferred)
        {
            queue.ConfigureIsolatedAutomaticDispatch(true);
            skillAutomaticDispatchDeferred = false;
        }
        if (gameSpeed != null)
        {
            gameSpeed.SetSpeed(originalGameSpeed);
            gameSpeed.SetPaused(originalGamePaused);
        }
        CloseNonTargetSupportPreparationFixtures("campaign-cleanup");
        EndNaturalInvasionHoldFixture();
        if (timeScale != null) timeScale.Scale = originalScale;
        Time.maximumDeltaTime = originalMaximumDeltaTime;
        if (pausedSpawner != null)
            pausedSpawner.ConfigureDeterministicSimulationForDiagnostics(paused: spawnerWasPaused);
        bool baseEntryVerified = (sourceV9SkillIntake && report.continuation != null)
            || sourceV9EquipmentIntake || sourceV9FacilityIntake
            || sourceV9EquipmentReplay || sourceV9SkillReplay
            || sourceV9TraitReplay || sourceV9FacilityReplay || SkillAdmissionDiagnostics
            ? report.continuation != null
                && report.continuation.officialRestore
                && report.continuation.freshOwner
                && report.continuation.sessionExact
                && report.continuation.factionExact
                && report.continuation.outcomesPreserved
                && report.continuation.workOrdersExact
            : report.continuation != null
            ? report.continuation.officialRestore && report.continuation.freshOwner
                && report.continuation.sessionExact && report.continuation.factionExact && report.continuation.outcomesPreserved
                && report.continuation.workOrdersExact
                && report.continuation.corruptRouteIdentityRejected && report.continuation.rejectedRestoreUnchanged
                && (report.continuation.sourceRouteDelivered || report.continuation.resumedRouteAdvanced)
            : report.wireReplayExact && report.pendingExact && report.officialWorldRestore && report.freshPublishedOwner
            && report.traitWireReplayExact && report.traitPendingExact
            && report.traitOfficialWorldRestore && report.traitFreshPublishedOwner
            && report.skillLaterContextChanged && report.skillLaterOutcomePreserved;
        report.sourceV9SkillIntake = sourceV9SkillIntake;
        report.sourceV9EquipmentIntake = sourceV9EquipmentIntake;
        report.sourceV9FacilityIntake = sourceV9FacilityIntake;
        report.sourceV9EquipmentReplay = sourceV9EquipmentReplay;
        report.sourceV9SkillReplay = sourceV9SkillReplay;
        report.sourceV9TraitReplay = sourceV9TraitReplay;
        report.sourceV9FacilityReplay = sourceV9FacilityReplay;
        if ((sourceV9SkillIntake || sourceV9EquipmentIntake
                || sourceV9FacilityIntake)
            && string.IsNullOrEmpty(report.failure)
            && report.errors.Count == 0 && baseEntryVerified)
        {
            try
            {
                Newtonsoft.Json.Linq.JObject exported =
                    sourceV9EquipmentIntake
                        ? GameplayModuleSelectionContinuousSourceV9Exporter
                            .ExportEquipment(output, report.registeredSections)
                        : sourceV9FacilityIntake
                        ? GameplayModuleSelectionContinuousSourceV9Exporter
                            .ExportFacility(output, report.registeredSections)
                        : GameplayModuleSelectionContinuousSourceV9Exporter
                            .Export(output, report.registeredSections);
                report.sourceV9PackageWritten = true;
                report.sourceV9ManifestSha256 = (string)exported["manifestSha256"];
            }
            catch (Exception exception)
            {
                report.failure = exception.ToString();
            }
        }
        bool profileVerified = originalSkillPendingDiagnostics
            ? report.originalSkillPendingDiagnosticsVerified && !report.networkSent
            : completedCraftDeliveryDiagnostics
            ? completedCraftDeliveryVerified && !report.networkSent
            : constructionHaulDiagnostics
            ? report.constructionHaulDiagnostics != null && report.constructionHaulDiagnostics.sourceOrderExact
                && report.constructionHaulDiagnostics.materialsReady && !report.networkSent
            : skillBattleDiagnostics
            ? report.skillMechanicExecuted && report.skillMechanic != null
                && report.skillMechanic.actualEffect && report.skillMechanic.officialFreshRestore
                && report.skillMechanic.combatOwnerGuardsAtomic
                && report.skillMechanic.initiativeGuardChecks == 5
                && report.skillMechanic.initiativeStateChecks.Count == 5
                && report.skillMechanic.battleExact && report.skillMechanic.skillExact
                && report.skillMechanic.evidenceExact && report.skillMechanic.restoredDuplicateStable
                && report.skillMechanic.restoredActualEffect && !report.networkSent
            : skillResearchDiagnostics
            ? report.skillResearchDiagnostics != null && report.skillResearchDiagnostics.completed
                && report.skillResearchDiagnostics.approvedResearchTicks > 0
                && report.skillResearchDiagnostics.completionOutcomes.Count > 0
            : skillHaulDiagnostics
            ? report.skillHaulDiagnostics != null && report.skillHaulDiagnostics.completed
                && report.skillHaulDiagnostics.itemIdentityPreserved
            : sourceV9EquipmentReplay || sourceV9SkillReplay
            || sourceV9TraitReplay || sourceV9FacilityReplay
            ? report.u4ProofWritten && report.responseApplied
                && !report.networkSent
                && (!sourceV9SkillReplay || (report.skillSelectionCommitted && report.skillMechanicExecuted))
            : sourceV9EquipmentIntake
            ? report.equipmentOfficialWorldRestore
                && report.equipmentStateExact
                && report.equipmentPendingExact
                && report.equipmentWireReplayExact
                && report.sourceV9PackageWritten
            : sourceV9FacilityIntake
            ? report.facilityOfficialWorldRestore
                && report.facilityFreshPublishedOwner
                && report.facilityPendingExact
                && report.facilityWireReplayExact
                && report.sourceV9PackageWritten
            : sourceV9SkillIntake
            ? report.sourceV9PackageWritten && report.skillTriggerCommandBound && report.officialWorldRestore
                && report.freshPublishedOwner && report.pendingExact && report.wireReplayExact
                && report.traitOfficialWorldRestore && report.traitFreshPublishedOwner
                && report.traitPendingExact && report.traitWireReplayExact
                && (!report.resumedExistingTraitRequest
                    || (report.resumedTraitPendingExact && report.resumedTraitEvidenceBound))
            : report.facilityOfficialWorldRestore && report.facilityFreshPublishedOwner
                && report.facilityPendingExact && report.facilityWireReplayExact;
        PrepareControlledPlayExit();
        report.passed = string.IsNullOrEmpty(report.failure) && report.errors.Count == 0
            && baseEntryVerified && profileVerified
            && (!skillReplayHoldNaturalInvasion
                || (report.naturalInvasionHoldApplied && report.naturalInvasionDirectorRestored));
        WriteNew("report.json", JsonUtility.ToJson(report, true));
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        Application.logMessageReceived -= CaptureLog;
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(report.passed ? 0 : 1);
    }

    private void PrepareControlledPlayExit()
    {
        try
        {
            Require(scope != null && scope.Container != null,
                "Pre-exit lifecycle cleanup requires the live restored scope.");
            if (this.outcomes == null)
                this.outcomes = scope.Container.Resolve<IGameplayOutcomeQuery>();
            IServiceSessionRuntime sessions = scope.Container.Resolve<IServiceSessionRuntime>();
            string[] activeIds = sessions.ActiveSessions.Select(value => value.SessionId).ToArray();
            CharacterActor[] actors = UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(actor => actor != null && actor.gameObject.scene == scope.gameObject.scene).ToArray();
            // ExitPlaymode resets Unity's source-clock epoch before some
            // OnDisable callbacks. Release through the real actor owners now;
            // never clamp or rebind the restored service aggregate's clock.
            foreach (CharacterActor actor in actors)
                actor.ReleaseTransientAiOwnership("isolated-verification-before-play-exit");
            var cancelledSessions = new JArray();
            foreach (string id in activeIds)
            {
                Require(sessions.TryGetSessionSnapshot(id, out var cancelled) && !cancelled.IsActive
                        && cancelled.Stage == ServiceSessionStage.Cancelled
                        && cancelled.TerminalPhase == ServiceSessionTerminalPhase.Acknowledged,
                    "Pre-exit actor cleanup did not cancel the exact active service session: " + id);
                cancelledSessions.Add(JObject.FromObject(cancelled));
            }
            Require(sessions.ActiveSessions.Count == 0,
                "An active service session remains after pre-exit actor cleanup.");
            string first = JsonUtility.ToJson(sessions.Capture());
            int version = sessions.Version;
            string[] outcomes = ReadAllOutcomes().Select(OutcomeKey).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            foreach (CharacterActor actor in actors)
                actor.ReleaseTransientAiOwnership("isolated-verification-repeat-pre-exit");
            bool duplicateStable = sessions.Version == version
                && JsonUtility.ToJson(sessions.Capture()) == first
                && outcomes.SequenceEqual(ReadAllOutcomes().Select(OutcomeKey).OrderBy(value => value, StringComparer.Ordinal));
            WriteNew("controlled-play-exit-witness.json", new JObject
            {
                ["origin"] = "explicit verifier termination; real lifecycle cancellation before Unity source-clock reset, not natural play",
                ["activeServiceSessionIdsBefore"] = new JArray(activeIds),
                ["cancelledSessions"] = cancelledSessions,
                ["activeServiceSessionCountAfter"] = sessions.ActiveSessions.Count,
                ["actorCount"] = actors.Length, ["duplicateStable"] = duplicateStable,
                ["clockClampedOrRebound"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
            }.ToString(Newtonsoft.Json.Formatting.Indented));
            Require(duplicateStable, "Repeated pre-exit actor cleanup changed service state or outcome identities.");
        }
        catch (Exception error)
        {
            report.failure = (report.failure ?? string.Empty) + "\nControlled Play exit cleanup failed: " + error;
        }
    }

    private IEnumerator RunCampaign()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
            && Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE") == "1",
            "Live campaign requires explicit isolated capture-only batch mode.");
        Require(!LiveNarrativeCausalPlan.Requested || sourceV9EquipmentIntake || sourceV9FacilityIntake
                || (sourceV9SkillIntake && string.IsNullOrEmpty(
                    Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD"))),
            "Live causal plans require fresh skill/trait intake or a new equipment/facility request.");
        Require(!sourceV9SkillExpeditionActor || sourceV9SkillIntake,
            "The expedition-eligible skill actor option requires fresh skill intake.");
        Require(!skillReplayHoldNaturalInvasion || sourceV9SkillReplay || SkillSupportContinuation,
            "Natural invasion hold is an explicit isolated skill replay/selected-support fixture only.");
        Require(!skillNonTargetHungerFixture || (Application.isBatchMode
                && DungeonPersistencePaths.IsIsolatedVerification && skillNormalWeaponPreparation
                && skillReplayHoldNaturalInvasion && (sourceV9SkillReplay || SkillSupportContinuation)),
            "Non-target hunger fixture requires isolated selected-skill preparation only.");
        Require(!skillEquipmentPreparationFixture || (Application.isBatchMode
                && DungeonPersistencePaths.IsIsolatedVerification && (SkillSupportContinuation || sourceV9SkillReplay)
                && skillNormalWeaponPreparation
                && skillReplayHoldNaturalInvasion && skillNonTargetHungerFixture),
            "Equipment preparation fixture requires an explicit isolated selected-skill continuation or source-v9 replay.");
        Require(!skillPreparationEnvironmentFixture || skillEquipmentPreparationFixture,
            "Preparation exposure fixture requires the separate explicit equipment/support test opt-in.");
        Require(!skillPreparationInstantWorkFixture || (skillEquipmentPreparationFixture && skillPreparationEnvironmentFixture),
            "Instant work preparation requires the separate explicit equipment and exposure fixture opt-ins.");
        if (originalSkillPendingDiagnostics)
        {
            report.scope = "Original Miru Active5 pending: official fresh restore, current exact wire, preserved native negative response through production callback, retry save/restore and no application";
            report.executionOrigin = "original actual production checkpoint + preserved actual native negative response in isolated test branch";
        }
        if (sourceV9SkillIntake)
        {
            // These request-only contracts do not send model calls or modify the world.
            // Run them in the same owned batch so a fresh-source capture needs no second Editor startup.
            V25NarrativeInferenceDebugScenarios.RunCompactPublicTransportScenario();
            V25NarrativeInferenceDebugScenarios.RunCompactTraitDomainEvidenceScenario();
            V25NarrativeInferenceDebugScenarios.RunTraitGameplayEvidenceGrammarScenario();
            V25NarrativeInferenceDebugScenarios.RunTraitEvidenceReferenceTransportScenario();
            V25NarrativeInferenceDebugScenarios.RunTraitProseJsonBoundaryScenario();
            V25NarrativeInferenceDebugScenarios.RunModuleSelectionProseJsonBoundaryScenario();
            V25NarrativeInferenceDebugScenarios.RunSkillProseAuthorityInstructionScenario();
            report.scope = "U3 controlled skill/acquired-trait execution to official checkpoint, exact wire and source-v9 intake";
            report.requestScheduling += "; source-v9 mode stops after both restored exact-wire witnesses and does not run the separate facility campaign";
        }
        else if (sourceV9EquipmentIntake)
        {
            V25NarrativeInferenceDebugScenarios.RunCompactPublicTransportScenario();
            V25NarrativeInferenceDebugScenarios.RunCompactEquipmentTransportScenario();
            report.scope = "U3 resumed official world, actual combat equipment trigger, exact wire and source-v9 equipment intake";
            report.executionOrigin = "saved-world + public combat command";
            report.requestScheduling += "; equipment-only source-v9 mode restores an existing official checkpoint and does not repeat skill/trait commands";
        }
        else if (sourceV9FacilityIntake)
        {
            V25NarrativeInferenceDebugScenarios.RunFacilitySelectionIdentityTransportScenario();
            FacilityUsagePublicProjectionDebugScenarios.RunOrThrow();
            report.scope = "U3 resumed official world, actual facility-use trigger, exact wire and source-v9 facility intake";
            report.executionOrigin = "saved-world + public facility command";
            report.requestScheduling += "; facility-only source-v9 mode restores an existing official checkpoint and does not repeat skill/trait commands";
        }
        else if (sourceV9EquipmentReplay)
        {
            report.scope = "U4 source-v9 equipment response through production queue, commit and official save/restore";
            report.executionOrigin = "saved-world + explicit integration response fixture";
            report.requestScheduling += "; replay mode applies one externally fixed response through the production callback path without network access";
        }
        else if (sourceV9SkillReplay)
        {
            report.scope = "U4 source-v9 skill response, explicit simulated-player selection, physical blueprint/haul/research admission, actual registered expedition Ability effect and official save/restore/reexecution";
            report.executionOrigin = "saved-world + explicit integration response fixture";
            report.requestScheduling += "; replay stores three candidates then uses the explicitly supplied simulated-player index through the public selection command; expedition research admission uses public commands and physical inputs"
                + (skillEquipmentPreparationFixture
                    ? "; explicit non-target equipment/exposure/work preparation flags are recorded separately and end before actual expedition effects; no natural preparation claim"
                    : "; normal preparation without injected research completion");
        }
        else if (sourceV9TraitReplay)
        {
            report.scope = "U4 source-v9 acquired-trait response through production queue, commit, gameplay effect and official save/restore";
            report.executionOrigin = "saved-world + explicit integration response fixture";
            report.requestScheduling += "; replay mode commits one unchanged provenance-validated trait selection through the production callback and executes the actual selected effect consumer";
        }
        else if (sourceV9FacilityReplay)
        {
            report.scope = "U4 source-v9 facility response through production queue, commit, gameplay effect and official save/restore";
            report.executionOrigin = "saved-world + explicit integration response fixture";
            report.requestScheduling += "; replay mode commits one fixed facility selection through the production callback and executes its production service-support projection";
        }
        report.activityEvidenceContractChecks = LiveNarrativeActivityEvidence.VerifyContract();
        float readyUntil = Time.realtimeSinceStartup + 45f;
        while (scope == null && Time.realtimeSinceStartup < readyUntil)
        {
            scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(value => value != null && value.Container != null);
            if (scope == null) yield return null;
        }
        Require(scope != null, "Official world scope did not initialize.");
        if (scope.Container.Resolve<ICharacterSpawnerProvider>().TryGetSpawner(out pausedSpawner))
        {
            spawnerWasPaused = pausedSpawner.DeterministicSimulationPausedForDiagnostics;
            pausedSpawner.ConfigureDeterministicSimulationForDiagnostics(paused: true);
        }
        string pilotSeedText = Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_INITIAL_SEED");
        if (!string.IsNullOrEmpty(pilotSeedText))
        {
            Require(sourceV9SkillIntake
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD"))
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_SHA256")),
                "Pilot seed preparation cannot alter a resumed world or another capture mode.");
            Require(int.TryParse(pilotSeedText, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int pilotSeed) && pilotSeed > 0,
                "Pilot initial seed must be an explicit positive integer.");
            Require(!scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out OwnerRunManager existingManager)
                || existingManager.CurrentOwnerActor == null,
                "Pilot initial seed must be applied before the starting party exists.");
            IRandomStreamProvider randomStreams = scope.Container.Resolve<IRandomStreamProvider>();
            int priorSeed = randomStreams.RootSeed;
            RunVariableRuntime pilotRun = scope.Container.Resolve<DungeonSceneRuntimeReferences>().RunVariables;
            InvasionThreatRuntime pilotThreat = scope.Container.Resolve<InvasionSceneRuntimeReferences>().Threat;
            Require(pilotThreat != null && pilotThreat.Settings != null && existingManager != null,
                "Pilot seed preparation requires the authored difficulty and owner catalogue.");
            CharacterSO pilotOwnerDefinition = existingManager.OwnerCandidates.FirstOrDefault();
            Require(pilotOwnerDefinition != null, "Pilot owner definition is missing.");
            // Match PreparedStartPartyCommitService's authored difficulty and
            // its standard-pressure overload before that first commit exists.
            pilotRun.StartRun(pilotSeed, pilotOwnerDefinition,
                DungeonDifficultyRules.FromLegacy(pilotThreat.Settings.difficulty), DungeonSurvivalPressure.Standard);
            Require(pilotRun.RunSeed == pilotSeed && randomStreams.RootSeed == pilotSeed,
                "Pilot run and random-stream seed commands differ.");
            report.pilotInitialSeedConfigured = true;
            report.pilotInitialSeed = pilotSeed;
            report.initialSetup += "; explicit test-only random seed before party creation: " + pilotSeed;
            WriteNew("pilot-initial-seed.json", new JObject
            {
                ["schema"] = "dungeonstory-pilot-initial-seed@1",
                ["priorSeed"] = priorSeed,
                ["initialSeed"] = pilotSeed,
                ["boundary"] = "before-start-party-creation",
                ["command"] = "RunVariableRuntime.StartRun",
                ["testPreparationOnly"] = true,
                ["naturalPlayClaimed"] = false,
                ["trainingEligible"] = false
            }.ToString());
        }
        if (!scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out OwnerRunManager manager)
            || manager.CurrentOwnerActor == null)
        {
            StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            for (int frame = 0; frame < 10; frame++) yield return null;
        }
        Require(StartPartyPreparationPlayModeVerifier.TryReconcileTierZeroForDirectGameplayFixture(scope, out string setupDetail),
            "Initial world preparation failed: " + setupDetail);
        if (report.pilotInitialSeedConfigured)
            Require(scope.Container.Resolve<DungeonSceneRuntimeReferences>().RunVariables.RunSeed == report.pilotInitialSeed
                && scope.Container.Resolve<IRandomStreamProvider>().RootSeed == report.pilotInitialSeed,
                "Starting-party commit replaced the explicit pilot seed.");
        Require(scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out manager)
            && manager.CurrentOwnerActor != null, "Official owner is missing.");
        owner = manager.CurrentOwnerActor;
        report.gameOwnerId = owner.Identity.PersistentId;
        Require(!owner.IsDead && owner.Brain != null, "Owner is not a living command recipient.");
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        calendar = scope.Container.Resolve<IGameCalendar>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        gameSpeed = scope.Container.Resolve<IGameSpeedController>();
        originalScale = timeScale.Scale;
        originalGameSpeed = gameSpeed.Speed;
        originalGamePaused = gameSpeed.IsPaused;
        Require(originalScale > 0 && !originalGamePaused,
            "Initial world is paused; no implicit time override is allowed.");
        outcomes = scope.Container.Resolve<IGameplayOutcomeQuery>();
        outcomeEvidence = scope.Container.Resolve<IGameplayOutcomeNarrativeEvidenceQuery>();
        derivedStats = scope.Container.Resolve<CharacterDerivedStatsSnapshotProjector>();
        generation = scope.Container.Resolve<ICharacterSkillGenerationService>() as CharacterSkillGenerationService;
        traitRuntime = scope.Container.Resolve<ICharacterAcquiredTraitManifestationDiagnostics>() as CharacterAcquiredTraitManifestationRuntime;
        queue = scope.Container.Resolve<ILocalLlmRuntimeProvider>().GetRequiredRuntime() as LocalLlmRequestQueue;
        Require(generation != null && traitRuntime != null && queue != null, "Actual skill/trait services and request queue are required.");
        report.registeredSections = scope.Container.Resolve<IDungeonSaveSectionRegistry>().OrderedSections.Count;
        string resumePath = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD");
        string resumeHash = Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_RESUME_SHA256");
        Require(string.IsNullOrEmpty(resumePath) == string.IsNullOrEmpty(resumeHash), "Continuation path and hash must be specified together.");
        if (originalSkillPendingDiagnostics)
        {
            string originalWorld = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
                "Artifacts/QA/AuditRemediation-20260926-r1/u3-source-v9-skill-staff-live-r63/unity-evidence/source-v9/selection/skill/world.json"));
            Require(!string.IsNullOrEmpty(resumePath)
                    && string.Equals(Path.GetFullPath(resumePath), originalWorld, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(resumeHash?.Replace("sha256:", string.Empty),
                        "382867d8599b8279fe829c7ed48f978782a9146f8f7b3852857ee4cb0b090d3a",
                        StringComparison.OrdinalIgnoreCase),
                "Original skill diagnostics require the exact preserved r63 world, not a new request.");
        }
        Require(!sourceV9SkillIntake || string.IsNullOrEmpty(resumePath) || sourceV9SkillExpeditionActor,
            "Resumed skill intake requires explicit normal expedition staff selection.");
        Require((skillHaulDiagnostics ? 1 : 0) + (skillResearchDiagnostics ? 1 : 0)
                + (skillBattleDiagnostics ? 1 : 0) + (constructionHaulDiagnostics ? 1 : 0)
                + (completedCraftDeliveryDiagnostics ? 1 : 0) + (originalSkillPendingDiagnostics ? 1 : 0) <= 1,
            "Focused admission diagnostics must run separately.");
        Require(!SkillAdmissionDiagnostics || (!string.IsNullOrEmpty(resumePath)
                && !sourceV9SkillReplay && !sourceV9SkillIntake && !sourceV9EquipmentReplay
                && !sourceV9EquipmentIntake && !sourceV9TraitReplay && !sourceV9FacilityReplay
                && !sourceV9FacilityIntake), "Admission diagnostics cannot substitute for a source-v9 intake/replay.");
        Require(string.IsNullOrEmpty(skillBattleActorId) == string.IsNullOrEmpty(skillBattleSkillId)
                && (string.IsNullOrEmpty(skillBattleActorId) || skillBattleDiagnostics),
            "Selected support continuation requires an explicit actor/skill pair and its focused battle entry.");
        Require(!skillOneCraftingBenchFixture || (skillNormalWeaponPreparation
                && (sourceV9SkillReplay || SkillSupportContinuation)),
            "One approved crafting bench is isolated support preparation only, not a general intake fixture.");
        Require(!skillThirtyFoodPreparationFixture || (SkillSupportContinuation && skillNormalWeaponPreparation
                && skillReplayHoldNaturalInvasion
                && string.Equals(resumeHash?.Replace("sha256:", string.Empty),
                    "54ffb75b344e07ff0dccc45c0901ff604e0414633f2992dd14ba054756422ada",
                    StringComparison.OrdinalIgnoreCase)),
            "Thirty approved food inputs require the exact r26 support checkpoint, not another intake or continuation.");
        Require(!skillRecruitmentHistoryCapacityFixture || (SkillSupportContinuation && skillNormalWeaponPreparation
                && skillReplayHoldNaturalInvasion && !skillThirtyFoodPreparationFixture
                && !skillOneCraftingBenchFixture && !string.IsNullOrEmpty(resumePath)),
            "Historical-capacity preparation is an explicit isolated support test, never another intake or a repeated food preparation.");
        if (!string.IsNullOrEmpty(resumePath))
        {
            if (sourceV9SkillReplay)
            {
                // A source request owner can be a staff character. Resolve its
                // fixed identity from the package, never substitute the boss.
                skillReplayInput = GameplayModuleSelectionSourceV9ReplayInput.LoadSkill(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                    Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE"),
                    Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE"),
                    resumePath);
            }
            if (sourceV9TraitReplay)
            {
                // Trait requests also belong to their captured actor, not
                // necessarily the boss restored from the official world.
                traitReplayInput = GameplayModuleSelectionSourceV9ReplayInput.LoadTrait(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                    Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE"),
                    Environment.GetEnvironmentVariable("DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE"),
                    resumePath);
            }
            if (sourceV9SkillIntake || sourceV9EquipmentIntake || sourceV9FacilityIntake
                || sourceV9EquipmentReplay || sourceV9SkillReplay
                || sourceV9TraitReplay || sourceV9FacilityReplay || SkillAdmissionDiagnostics)
                ResumeSourceV9World(
                    resumePath,
                    resumeHash,
                    sourceV9SkillIntake ? "skill-intake"
                        : originalSkillPendingDiagnostics ? "original-skill-pending-diagnostics"
                        : completedCraftDeliveryDiagnostics ? "completed-craft-delivery-diagnostics"
                        : constructionHaulDiagnostics ? "construction-haul-diagnostics"
                        : skillBattleDiagnostics ? "skill-battle-diagnostics"
                        : skillResearchDiagnostics ? "skill-research-diagnostics"
                        : skillHaulDiagnostics ? "skill-haul-diagnostics" : sourceV9TraitReplay
                        ? "trait-replay"
                        : sourceV9SkillReplay
                        ? "skill-replay"
                        : sourceV9FacilityReplay
                        ? "facility-replay"
                        : sourceV9EquipmentReplay
                        ? "equipment-replay"
                        : sourceV9EquipmentIntake ? "equipment" : "facility",
                    originalSkillPendingDiagnostics ? OriginalSkillPendingOwner
                        : sourceV9SkillReplay ? skillReplayInput.OwnerId
                        : sourceV9TraitReplay ? traitReplayInput.OwnerId
                        : SkillSupportContinuation ? skillBattleActorId
                        : SkillAdmissionDiagnostics && !constructionHaulDiagnostics ? "character:staff:1:01" : null);
            else
                ResumeOfficialWorld(resumePath, resumeHash);
            if (skillThirtyFoodPreparationFixture)
                ApplyApprovedThirtyFoodPreparation();
            if (!sourceV9SkillIntake)
            {
                if (skillReplayHoldNaturalInvasion || completedCraftDeliveryDiagnostics) BeginNaturalInvasionHoldFixture();
                queue.ConfigureIsolatedRequestCapture(CaptureDispatch);
                if (originalSkillPendingDiagnostics)
                    yield return RunOriginalSkillPendingDiagnostics();
                else if (completedCraftDeliveryDiagnostics)
                    yield return RunCompletedCraftDeliveryDiagnostics();
                else if (constructionHaulDiagnostics)
                    yield return RunConstructionHaulDiagnostics();
                else if (skillBattleDiagnostics)
                    yield return RunSkillBattleDiagnostics();
                else if (skillResearchDiagnostics)
                    yield return RunSkillResearchDiagnostics();
                else if (skillHaulDiagnostics)
                    yield return RunSkillBlueprintHaulDiagnostics();
                else if (sourceV9TraitReplay)
                    yield return RunTraitReplay();
                else if (sourceV9SkillReplay)
                    yield return RunSkillReplay();
                else if (sourceV9FacilityReplay)
                    yield return RunFacilityReplay();
                else if (sourceV9EquipmentReplay)
                    yield return RunEquipmentReplay();
                else if (sourceV9EquipmentIntake)
                    yield return RunEquipmentWitness();
                else
                    yield return RunFacilityWitness();
                yield break;
            }
        }
        if (sourceV9SkillExpeditionActor)
            ConfigureSkillExpeditionActor();
        report.skillRequestActorId = owner.Identity.PersistentId;
        report.initialTraitScore = ReadTraitScore();
        if (sourceV9SkillIntake && report.continuation != null)
        {
            CharacterAcquiredTraitAggregateState traits = owner.Progression.CaptureAcquiredTraitState();
            Require(report.initialTraitScore >= 3 && traits.pendingRequests.Count == 1
                    && traits.pendingRequests[0].manifestationMilestone == 3
                    && traits.pendingRequests[0].ownerBinding != null
                    && traits.pendingRequests[0].ownerBinding.Matches(owner.Progression, out _),
                "Resumed intake requires one existing authoritative milestone-3 trait request; no request is synthesized.");
            report.resumedExistingTraitRequest = true;
            resumedTraitPendingJson = JsonUtility.ToJson(traits.pendingRequests[0]);
            List<GameplayOutcomeSnapshot> evidence = ReadBoundEvidence(
                traits.pendingRequests[0].evidenceBindings, requireFacilityCompletion: false);
            report.resumedTraitOutcomeKeys = evidence.Select(OutcomeKey).ToList();
            WriteNew("resumed-trait-pending.json", JsonUtility.ToJson(traits.pendingRequests[0], true));
            WriteNew("resumed-trait-evidence.json", new JArray(evidence.Select(
                value => JObject.Parse(JsonUtility.ToJson(value)))).ToString());
            report.executionOrigin = "saved-world + controlled-production-command";
            report.initialSetup = "official hashed full-world continuation; existing institution research and trait request preserved; new skill through actual training";
        }
        else
            Require(report.initialTraitScore < 3 && owner.Progression.CaptureAcquiredTraitState().pendingRequests.Count == 0,
                "Initial world already has an eligible/pending trait; it is not a new gameplay-trigger witness.");
        targetUnlock = owner.Progression.SkillSettings.activeUnlockLevels.Where(value => value > owner.Progression.Level)
            .DefaultIfEmpty(-1).Min();
        Require(targetUnlock > owner.Progression.Level, "No upcoming authored active unlock.");
        foreach (CharacterSkillDraft existing in owner.Progression.Drafts)
            intakeInitialSkillRequestKeys.Add(existing.requestKey);
        scope.Container.Resolve<ICharacterBodyHealthQuery>().GetSnapshot(owner);
        DungeonGameSaveData baseline = saves.Capture();
        Require(baseline.sections.Count == report.registeredSections, "Incomplete initial official world save.");
        if (report.pilotInitialSeedConfigured)
        {
            int storedRunSeed = JObject.Parse(baseline.sections.Single(value => value.sectionId == "run.variables").payloadJson)["runSeed"].Value<int>();
            int storedRandomSeed = JObject.Parse(baseline.sections.Single(value => value.sectionId == "foundation.random-streams").payloadJson)["rootSeed"].Value<int>();
            Require(storedRunSeed == report.pilotInitialSeed && storedRandomSeed == report.pilotInitialSeed,
                "Official initial world did not preserve the explicit pilot seed.");
        }
        WriteNew("initial-world.json", saves.ToJson(baseline));

        if (sourceV9SkillIntake && report.continuation != null)
        {
            // Restore preserves the input's paused state; starting new public
            // training is an explicit resume command after the exact checkpoint.
            gameSpeed.SetPaused(false);
            timeScale.Scale = originalScale;
        }

        LiveNarrativeCausalPlan livePlan = LiveNarrativeCausalPlan.Requested
            ? LiveNarrativeCausalPlan.Begin(output, CaptureLivePlanState()) : null;
        yield return RunTrainingRequests(livePlan?.SelectedActionId);
        // Seal before the independent later-world regression. That later visit
        // must never be presented as a cause of an already frozen request.
        livePlan?.Complete(CaptureLivePlanState(), owner.Identity.PersistentId, report.commands.Count,
            new JArray(LiveRequestReceipt(firstDispatch.dispatchEligibility,
                    firstDispatch.wireSha256, firstDispatch.checkpointSha256),
                LiveRequestReceipt(traitDispatch.dispatchEligibility,
                    traitDispatch.wireSha256, traitDispatch.checkpointSha256)));
        // Continue real gameplay after dispatch. Preserve the original request-time
        // checkpoint separately; restore the later official world, not a rewritten past.
        yield return CompleteTraining();
        report.skillLaterOutcomes = report.commands.Last().publishedOutcomes.ToList();
        CharacterSkillDraft laterPending = owner.Progression.Drafts.Single(value => value.requestKey == firstDispatch.pending.requestKey);
        var currentEvidence = GameplayOutcomeEvidenceFormulaProjection.Capture(
            outcomeEvidence.GetForCharacter(owner.Identity.PersistentId, 24, 0f, includeCompacted: true));
        var laterMaterial = GameplayOutcomeEvidencePublicMaterialComposer.AddExactEvidence(
            CharacterSkillPromptBuilder.BuildPublicMaterial(scope.Container.Resolve<NarrativeRequestContextBuilder>(), owner.Progression), currentEvidence);
        report.skillLaterContextChanged = laterMaterial.SemanticHash != laterPending.RestoreRequestPublicContext().SemanticHash;
        WriteNew("post-dispatch-current-context.json", JsonUtility.ToJson(laterMaterial.CaptureSnapshot(), true));
        Require(report.skillLaterContextChanged && report.skillLaterOutcomes.Count > 0,
            "Later gameplay did not exercise a changed public context.");
        checkpointJson = saves.ToJson(saves.Capture());
        report.skillLaterCheckpointSha256 = Hash(Encoding.UTF8.GetBytes(checkpointJson));
        WriteNew("post-dispatch-world.json", checkpointJson);
        // Freeze only after gameplay produced the captured request; UI/request time
        // remains independent. Restore is a real aggregate restore, not a DTO replay.
        timeScale.Scale = 0;
        CharacterActor previousOwner = owner;
        replaying = true;
        Require(saves.TryRestore(saves.FromJson(checkpointJson), out DungeonGameRestoreReport restored),
            "Official checkpoint restore failed: " + string.Join(" | ", restored.Errors));
        report.officialWorldRestore = true;
        Require(scope.Container.Resolve<ICharacterWorldSaveService>()
            .TryGetRestoredActor(firstDispatch.ownerId, out owner), "Restored actor index omitted the captured owner.");
        report.freshPublishedOwner = !ReferenceEquals(previousOwner, owner);
        Require(report.freshPublishedOwner && !owner.IsDead, "Restore did not publish a fresh living owner.");
        Require(string.Equals(manager.CurrentOwnerActor?.Identity?.PersistentId,
                report.gameOwnerId, StringComparison.Ordinal),
            "Restoring the skill request actor changed the boss identity.");
        var restoredOutcomeKeys = ReadOwnerOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        report.skillLaterOutcomePreserved = report.skillLaterOutcomes.All(value => restoredOutcomeKeys.Contains(OutcomeKey(value)));
        Require(report.skillLaterOutcomePreserved, "Restoring a pending skill discarded later gameplay outcomes.");
        CharacterSkillDraft pending = owner.Progression.Drafts.Single(value => value.requestKey == firstDispatch.pending.requestKey);
        CharacterSkillDraft normalized = pending.Clone();
        normalized.requestSubmitted = firstDispatch.pending.requestSubmitted;
        // Preserve both actual projections before the equality gate, including
        // on failure. Do not normalize away identity, evidence or retry state.
        WriteNew("restored-pending.json", JsonUtility.ToJson(normalized, true));
        WriteNew("restored-owner-binding.json", JsonUtility.ToJson(
            CharacterNarrativeRequestOwnerBinding.Capture(owner.Progression), true));
        Require(pending.ownerBinding != null && pending.ownerBinding.Matches(owner.Progression, out _)
            && JsonUtility.ToJson(normalized) == JsonUtility.ToJson(firstDispatch.pending),
            "Official restore changed pending ownership, evidence or offers.");
        report.pendingExact = true;
        WriteNew("restored-world.json", saves.ToJson(saves.Capture()));
        ResumeOnlyExistingTarget();
        float dispatchUntil = Time.realtimeSinceStartup + 30f;
        while (!report.wireReplayExact && Time.realtimeSinceStartup < dispatchUntil)
        {
            if (sourceV9SkillIntake) DispatchExistingSkillTargetForIsolatedWitness();
            yield return null;
        }
        Require(report.wireReplayExact, "Restored pending did not dispatch the identical actual wire request: " + generation.LastDiagnostic);
        if (sourceV9SkillIntake) yield break;
        yield return RunFacilityWitness();
    }

    private void ConfigureSkillExpeditionActor()
    {
        bool TraitGateCanBeWitnessed(CharacterActor candidate)
        {
            CharacterAcquiredTraitAggregateState traits = candidate.Progression.CaptureAcquiredTraitState();
            int score = CharacterAcquiredTraitExperienceScore.Require(candidate.Progression.NarrativeLedger);
            return report.continuation != null
                ? score >= 3 && traits.pendingRequests.Count == 1
                    && traits.pendingRequests[0].manifestationMilestone == 3
                    && traits.pendingRequests[0].ownerBinding != null
                    && traits.pendingRequests[0].ownerBinding.Matches(candidate.Progression, out _)
                : score < 3 && traits.pendingRequests.Count == 0;
        }
        Facility[] trainingFacilities = UnityEngine.Object.FindObjectsByType<Facility>(
                FindObjectsSortMode.None)
            .Where(value => value != null && value.isActiveAndEnabled
                && !value.isDestroy && value.SupportsFacilityRole(FacilityRole.Training))
            .ToArray();
        CharacterActor actor = UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsSortMode.None)
            .Where(value => value != null && !value.IsOwner && !value.IsDead
                && value.Identity?.CharacterType == CharacterType.NPC
                && value.Brain != null && value.Progression != null
                && OffenseExpeditionService.CanJoinExpedition(value, out _)
                && value.Progression.SkillSettings.activeUnlockLevels.Any(
                    level => level > value.Progression.Level)
                && TraitGateCanBeWitnessed(value)
                && trainingFacilities.Any(facility => facility.SupportsFacilityRole(
                        CharacterVisitPolicy.GetInterestRoles(value))
                    && CharacterVisitPolicy.CanVisitBuilding(
                        value, facility, alreadyVisited: false, out _)))
            .OrderBy(value => value.Progression.Level)
            .ThenBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(actor != null,
            "No actual expedition-eligible staff actor can reach a new skill through authored training; no owner or synthetic actor is substituted.");
        report.skillExpeditionActor = true;
        report.skillAutoChoiceBefore = actor.Progression.GrowthState.autoChooseDrafts;
        // This is the existing public player policy command. The isolated
        // branch must retain candidates for an explicit simulated-player pick.
        actor.Progression.SetAutoChooseSkillDrafts(false);
        owner = actor;
        report.initialSetup += "; actual staff request owner; public auto-choice disabled in this isolated branch; boss identity and expedition admission unchanged";
    }

    private void ResumeSourceV9World(
        string path,
        string expectedHash,
        string profileLabel,
        string requestActorId = null)
    {
        string label = profileLabel?.Trim() ?? string.Empty;
        Require(label is "skill-intake" or "equipment" or "equipment-replay" or "skill-replay"
                or "trait-replay" or "facility" or "facility-replay"
                or "skill-haul-diagnostics" or "skill-research-diagnostics" or "skill-battle-diagnostics"
                or "construction-haul-diagnostics" or "completed-craft-delivery-diagnostics"
                or "original-skill-pending-diagnostics" or "original-skill-pending-reject1"
                or "original-skill-pending-reject2",
            "Source-v9 continuation profile is invalid.");
        phase = label + "-continuation-restore";
        var witness = new ContinuationWitness
        {
            sourcePath = Path.GetFullPath(path),
            sourceSha256 = expectedHash
        };
        report.continuation = witness;
        string qaRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Artifacts",
            "QA")) + Path.DirectorySeparatorChar;
        Require(Path.IsPathRooted(path)
                && witness.sourcePath.StartsWith(
                    qaRoot,
                    StringComparison.OrdinalIgnoreCase),
            "Source-v9 continuation must reference this project's QA evidence.");
        byte[] bytes = File.ReadAllBytes(witness.sourcePath);
        Require(string.Equals(
                Hash(bytes),
                expectedHash,
                StringComparison.OrdinalIgnoreCase),
            "Source-v9 continuation checkpoint hash differs.");
        DungeonGameSaveData checkpoint = saves.FromJson(
            Encoding.UTF8.GetString(bytes));
        witness.sections = checkpoint.sections.Count;
        Require(witness.sections == report.registeredSections,
            "Source-v9 continuation is not a complete official world.");
        if (constructionHaulDiagnostics)
            VerifyConstructionEngagementSaveContract(checkpoint);
        if (label == "facility")
        {
            sourceConstructionOrders = DungeonSaveJson.Deserialize<
                DungeonWorkOrderSaveData>(checkpoint.sections.Single(value =>
                    value.sectionId == "work.orders").payloadJson).orders
                .Where(value => value.workTypeId
                    == BuiltInWorkTypeIds.Construct.Value)
                .ToList();
            DungeonFactionSaveData faction = DungeonSaveJson.Deserialize<
                DungeonFactionSaveData>(checkpoint.sections.Single(value =>
                    value.sectionId == "world.factions").payloadJson);
            FactionRouteState route = faction.routes.SingleOrDefault(value =>
                value.factionId == DungeonFactionIds.Kobold
                && value.kind == FactionRouteKind.TradeCaravan);
            if (route != null)
            {
                resumedTradeRouteId = witness.routeId = route.routeId;
                witness.sourceRouteIndex = route.pathIndex;
                witness.sourceRouteProgress = route.segmentProgress;
                witness.sourceRouteDelivered = route.cargoDelivery.state
                    == FactionRouteCargoDeliveryState.Delivered;
            }
        }

        CharacterActor previousOwner = requestActorId == null ? owner
            : UnityEngine.Object.FindObjectsByType<CharacterActor>(FindObjectsSortMode.None)
                .SingleOrDefault(value => value != null && string.Equals(
                    value.Identity?.PersistentId, requestActorId, StringComparison.Ordinal));
        string ownerId = requestActorId ?? owner.Identity.PersistentId;
        timeScale.Scale = 0;
        Require(saves.TryRestore(
                checkpoint,
                out DungeonGameRestoreReport restored),
            "Official source-v9 continuation restore failed: "
                + string.Join(" | ", restored.Errors));
        witness.officialRestore = true;
        Require(scope.Container.Resolve<ICharacterWorldSaveService>()
                .TryGetRestoredActor(ownerId, out owner),
            "Source-v9 continuation omitted its published owner.");
        witness.freshOwner = owner != null
            && !owner.IsDead
            && !ReferenceEquals(previousOwner, owner);
        if (label is "skill-replay" or "skill-haul-diagnostics" or "skill-research-diagnostics" or "skill-battle-diagnostics"
            or "original-skill-pending-diagnostics" or "original-skill-pending-reject1" or "original-skill-pending-reject2")
        {
            report.skillRequestActorId = ownerId;
            Require(owner != null && owner.Brain != null
                    && scope.Container.Resolve<IOwnerRunManagerProvider>()
                        .TryGetManager(out var restoredManager)
                    && string.Equals(restoredManager.CurrentOwnerActor?.Identity?.PersistentId,
                        report.gameOwnerId, StringComparison.Ordinal),
                "Skill restore changed the boss identity or omitted its request actor.");
        }
        if (label == "trait-replay")
        {
            Require(owner != null && owner.Brain != null
                    && scope.Container.Resolve<IOwnerRunManagerProvider>()
                        .TryGetManager(out var traitRestoredManager)
                    && string.Equals(traitRestoredManager.CurrentOwnerActor?.Identity?.PersistentId,
                        report.gameOwnerId, StringComparison.Ordinal),
                "Trait restore changed the boss identity or omitted its request actor.");
        }

        DungeonGameSaveData actual = saves.Capture();
        if (label == "skill-battle-diagnostics" && SkillSupportContinuation)
        {
            DungeonOffenseAggregateSaveData expectedOffense = DungeonSaveJson.Deserialize<DungeonOffenseAggregateSaveData>(
                checkpoint.sections.Single(value => value.sectionId == OffenseAggregateSaveSection.Id).payloadJson);
            DungeonOffenseAggregateSaveData actualOffense = DungeonSaveJson.Deserialize<DungeonOffenseAggregateSaveData>(
                actual.sections.Single(value => value.sectionId == OffenseAggregateSaveSection.Id).payloadJson);
            if (expectedOffense.expedition.hasActiveBattle)
            {
                bool expeditionExact = JsonUtility.ToJson(expectedOffense.expedition)
                    == JsonUtility.ToJson(actualOffense.expedition);
                WriteNew("skill-support-restored-expedition-exact.json", new JObject
                {
                    ["passed"] = expeditionExact,
                    ["scope"] = "exact original active-battle expedition DTO including departure, journey, members and battle",
                    ["trainingEligible"] = false, ["humanApprovalClaimed"] = false
                }.ToString());
                Require(expeditionExact, "Official support continuation changed the original expedition DTO.");
            }
        }
        bool SameSection(string id) => checkpoint.sections
            .Single(value => value.sectionId == id).payloadJson
            == actual.sections.Single(value => value.sectionId == id).payloadJson;
        witness.sessionExact = SameSection("foundation.session");
        witness.factionExact = SameSection("world.factions");
        witness.workOrdersExact = SameSection("work.orders");
        var sourceLedger = DungeonSaveJson.Deserialize<
            GameplayOutcomeLedgerSaveData>(checkpoint.sections.Single(
                value => value.sectionId
                    == "world.gameplay-outcome-ledger").payloadJson);
        var restoredLedger = DungeonSaveJson.Deserialize<
            GameplayOutcomeLedgerSaveData>(actual.sections.Single(
                value => value.sectionId
                    == "world.gameplay-outcome-ledger").payloadJson);
        witness.sourceLedgerEpoch = sourceLedger.worldEpoch;
        witness.restoredLedgerEpoch = restoredLedger.worldEpoch;
        bool epochAdvanced = restoredLedger.worldEpoch
            == checked(sourceLedger.worldEpoch + 1);
        witness.consolidationEpochsAdvanced =
            sourceLedger.consolidationJobs != null
            && restoredLedger.consolidationJobs != null
            && sourceLedger.consolidationJobs.Count
                == restoredLedger.consolidationJobs.Count;
        if (witness.consolidationEpochsAdvanced)
        {
            for (int index = 0;
                 index < sourceLedger.consolidationJobs.Count;
                 index++)
            {
                GameplayOutcomeConsolidationJobSnapshot sourceJob =
                    sourceLedger.consolidationJobs[index];
                GameplayOutcomeConsolidationJobSnapshot restoredJob =
                    restoredLedger.consolidationJobs[index];
                witness.consolidationEpochsAdvanced &= sourceJob != null
                    && restoredJob != null
                    && sourceJob.worldEpoch == sourceLedger.worldEpoch
                    && restoredJob.worldEpoch == restoredLedger.worldEpoch;
                if (sourceJob != null)
                    sourceJob.worldEpoch = restoredLedger.worldEpoch;
            }
        }
        sourceLedger.worldEpoch = restoredLedger.worldEpoch;
        witness.outcomesPreserved = epochAdvanced
            && witness.consolidationEpochsAdvanced
            && JsonUtility.ToJson(sourceLedger)
                == JsonUtility.ToJson(restoredLedger)
            && SameSection("narrative.migrated-producer-outcomes");
        WriteNew(
            label + "-continuation-restored-world.json",
            saves.ToJson(actual));
        WriteNew(
            label + "-continuation-witness.json",
            JsonUtility.ToJson(witness, true));
        Require(witness.freshOwner
                && witness.sessionExact
                && witness.factionExact
                && witness.outcomesPreserved
                && witness.workOrdersExact,
            "Source-v9 continuation changed owner, session, faction, outcomes or work orders.");
        if (SkillSupportContinuation)
            CaptureRestoredTraitOfferDiagnostics();
    }

    private void CaptureRestoredTraitOfferDiagnostics()
    {
        IGameContentDefinitionSource content = scope.Container.Resolve<IGameContentDefinitionSource>();
        CharacterAcquiredTraitSettingsSO settings = content.RequireSingle<CharacterAcquiredTraitSettingsSO>();
        CharacterAcquiredTraitModuleSO[] modules = content.GetAll<CharacterAcquiredTraitModuleSO>().ToArray();
        var rows = new JArray();
        foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>().Characters)
        {
            if (actor?.Progression == null) continue;
            foreach (CharacterAcquiredTraitPendingRequestState pending in
                     actor.Progression.CaptureAcquiredTraitState().pendingRequests)
            {
                var row = new JObject { ["actorId"] = actor.Identity.PersistentId,
                    ["requestId"] = pending.requestId, ["frozenSelectionId"] = pending.moduleSelectionId,
                    ["frozenBenefitIds"] = new JArray(pending.offeredBenefitModuleIds),
                    ["frozenDrawbackIds"] = new JArray(pending.offeredDrawbackModuleIds),
                    ["frozenEvidenceIds"] = new JArray(pending.evidenceFactIds) };
                rows.Add(row);
                try
                {
                    var current = CharacterAcquiredTraitFormulaGeneration.PrepareModuleSelection(
                        actor.Progression, pending.requestId, pending.requestKey, pending.manifestationMilestone,
                        settings, modules, pending.evidenceFactIds, pending.evidenceBindings);
                    row["currentSelectionId"] = current.SelectionId;
                    row["currentBenefitIds"] = new JArray(current.Offers
                        .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Positive)
                        .Select(value => value.ModuleId));
                    row["currentDrawbackIds"] = new JArray(current.Offers
                        .Where(value => value.Polarity == NarrativeFormulaModulePolarity.Drawback)
                        .Select(value => value.ModuleId));
                    row["currentEvidenceIds"] = new JArray(current.EvidenceFactIds);
                    row["selectionSame"] = current.SelectionId == pending.moduleSelectionId;
                }
                catch (InvalidOperationException exception)
                {
                    row["rebuildFailure"] = exception.Message;
                }
            }
        }
        WriteNew("skill-restored-trait-offer-diagnostics.json", new JObject
        {
            ["origin"] = "read-only frozen/current comparison; no pending, evidence or policy modification",
            ["rows"] = rows, ["trainingEligible"] = false, ["passClaimed"] = false
        }.ToString());
    }

    private void ResumeOfficialWorld(string path, string expectedHash)
    {
        phase = "continuation-restore";
        report.scope = "U3 official saved-world continuation and facility request/checkpoint witness; prior skill/trait checks are not repeated";
        report.executionOrigin = "saved-production-command-continuation";
        report.initialSetup = "authored scene initializes services, then official restore replaces it with the explicitly hashed actual checkpoint";
        var witness = new ContinuationWitness { sourcePath = Path.GetFullPath(path), sourceSha256 = expectedHash };
        report.continuation = witness;
        string qaRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Artifacts", "QA")) + Path.DirectorySeparatorChar;
        Require(Path.IsPathRooted(path) && witness.sourcePath.StartsWith(qaRoot, StringComparison.OrdinalIgnoreCase),
            "Continuation must explicitly reference this project's QA evidence.");
        byte[] bytes = File.ReadAllBytes(witness.sourcePath);
        Require(string.Equals(Hash(bytes), expectedHash, StringComparison.OrdinalIgnoreCase),
            "Continuation checkpoint hash does not match; original evidence is not changed.");
        DungeonGameSaveData checkpoint = saves.FromJson(Encoding.UTF8.GetString(bytes));
        sourceConstructionOrders = DungeonSaveJson.Deserialize<DungeonWorkOrderSaveData>(
            checkpoint.sections.Single(value => value.sectionId == "work.orders").payloadJson).orders
            .Where(value => value.workTypeId == BuiltInWorkTypeIds.Construct.Value).ToList();
        witness.sections = checkpoint.sections.Count;
        Require(witness.sections == report.registeredSections, "Continuation is not a complete official world.");
        var factionSave = DungeonSaveJson.Deserialize<DungeonFactionSaveData>(
            checkpoint.sections.Single(value => value.sectionId == "world.factions").payloadJson);
        FactionRouteState route = factionSave.routes.Single(value => value.factionId == DungeonFactionIds.Kobold
            && value.kind == FactionRouteKind.TradeCaravan);
        resumedTradeRouteId = witness.routeId = route.routeId;
        witness.sourceRouteIndex = route.pathIndex;
        witness.sourceRouteProgress = route.segmentProgress;
        witness.sourceRouteDelivered = route.cargoDelivery.state == FactionRouteCargoDeliveryState.Delivered;
        CharacterActor previousOwner = owner;
        string ownerId = owner.Identity.PersistentId;
        timeScale.Scale = 0;
        // Challenge semantic identity, not merely a stale file checksum. The
        // corrupt copy gets a valid manifest; the original bytes stay untouched.
        DungeonGameSaveData baseline = saves.Capture();
        WriteNew("continuation-rejection-before-world.json", saves.ToJson(baseline));
        DungeonGameSaveData corrupt = saves.FromJson(Encoding.UTF8.GetString(bytes));
        DungeonSaveSectionEnvelope corruptedSection = corrupt.sections.Single(value => value.sectionId == "world.factions");
        var corruptedFaction = DungeonSaveJson.Deserialize<DungeonFactionSaveData>(corruptedSection.payloadJson);
        corruptedFaction.routes.Single(value => value.routeId == route.routeId)
            .settlementPublicationId =
            "run:faction-route-settlement:conflicting/1";
        corruptedSection.payloadJson = JsonUtility.ToJson(corruptedFaction);
        corrupt.manifest = DungeonSaveManifest.Capture(corrupt.sections);
        witness.corruptRouteIdentityRejected = !saves.TryRestore(corrupt, out DungeonGameRestoreReport rejected);
        witness.identityRejectionReason = string.Join(" | ", rejected.Errors);
        DungeonGameSaveData afterRejected = saves.Capture();
        WriteNew("continuation-rejection-after-world.json", saves.ToJson(afterRejected));
        witness.rejectionChangedSections = baseline.sections.Where(before => !afterRejected.sections.Any(after =>
                after.sectionId == before.sectionId && after.sectionVersion == before.sectionVersion
                && after.payloadJson == before.payloadJson))
            .Select(value => value.sectionId).ToList();
        witness.rejectedRestoreUnchanged = baseline.sections.Count == afterRejected.sections.Count
            && witness.rejectionChangedSections.Count == 0;
        WriteNew("continuation-rejection-witness.json", JsonUtility.ToJson(witness, true));
        // Still diagnose the valid restore after a rejected-input discrepancy,
        // but do not advance gameplay or claim PASS until BOTH checks pass.
        Require(saves.TryRestore(checkpoint, out DungeonGameRestoreReport restored),
            "Official continuation restore failed: " + string.Join(" | ", restored.Errors));
        witness.officialRestore = true;
        Require(scope.Container.Resolve<ICharacterWorldSaveService>().TryGetRestoredActor(ownerId, out owner),
            "Continuation omitted its published owner.");
        witness.freshOwner = owner != null && !owner.IsDead && !ReferenceEquals(previousOwner, owner);
        DungeonGameSaveData actual = saves.Capture();
        bool SameSection(string id) => checkpoint.sections.Single(value => value.sectionId == id).payloadJson
            == actual.sections.Single(value => value.sectionId == id).payloadJson;
        witness.sessionExact = SameSection("foundation.session");
        witness.factionExact = SameSection("world.factions");
        witness.workOrdersExact = SameSection("work.orders");
        var sourceLedger = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(
            checkpoint.sections.Single(value => value.sectionId == "world.gameplay-outcome-ledger").payloadJson);
        var restoredLedger = DungeonSaveJson.Deserialize<GameplayOutcomeLedgerSaveData>(
            actual.sections.Single(value => value.sectionId == "world.gameplay-outcome-ledger").payloadJson);
        witness.sourceLedgerEpoch = sourceLedger.worldEpoch;
        witness.restoredLedgerEpoch = restoredLedger.worldEpoch;
        // Official restore intentionally advances the transient token epoch.
        // Verify that exact contract, then compare every other saved field.
        bool epochAdvanced = restoredLedger.worldEpoch == checked(sourceLedger.worldEpoch + 1);
        sourceLedger.worldEpoch = restoredLedger.worldEpoch;
        witness.outcomesPreserved = epochAdvanced
            && JsonUtility.ToJson(sourceLedger) == JsonUtility.ToJson(restoredLedger)
            && SameSection("narrative.migrated-producer-outcomes");
        WriteNew("continuation-restored-world.json", saves.ToJson(actual));
        WriteNew("continuation-witness.json", JsonUtility.ToJson(witness, true));
        Require(witness.corruptRouteIdentityRejected && witness.rejectedRestoreUnchanged
            && witness.identityRejectionReason.Contains("conflicting settlement outcome identity", StringComparison.Ordinal),
            "Corrupt paid-route identity was accepted or rejection changed the official world: "
                + string.Join(", ", witness.rejectionChangedSections));
        Require(witness.freshOwner && witness.sessionExact && witness.factionExact && witness.outcomesPreserved
                && witness.workOrdersExact,
            "Continuation changed owner, session, faction/route/payment, published outcomes or saved work at restore.");
    }

    private IEnumerator RunEquipmentReplay()
    {
        phase = "equipment-source-v9-replay";
        equipmentPhase = true;
        replaying = true;
        CancelOutstandingTransport();
        combatEquipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        equipmentEvolution = scope.Container.Resolve<IEquipmentEvolutionRuntime>();
        equipmentPresentation = scope.Container
            .Resolve<IEvolutionHistoryNarrativeRuntime>()
            as EvolutionHistoryNarrativeRuntime;
        Require(combatEquipment != null
                && equipmentEvolution != null
                && equipmentPresentation != null,
            "Equipment replay requires the production equipment and presentation runtimes.");

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string sourceRoot = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE");
        string responseRoot = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE");
        string resumePath = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD");
        equipmentReplayInput = GameplayModuleSelectionSourceV9ReplayInput
            .LoadEquipment(projectRoot, sourceRoot, responseRoot, resumePath);
        Require(string.Equals(
                    owner.Identity.PersistentId,
                    equipmentReplayInput.OwnerId,
                    StringComparison.Ordinal),
            "Restored official owner differs from the source-v9 response owner.");
        Require(combatEquipment.TryGetInstance(
                equipmentReplayInput.EquipmentInstanceId,
                out CombatEquipmentInstance beforeInstance),
            "Source-v9 replay equipment is absent after official restore.");
        EquipmentEvolutionState beforeState = beforeInstance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        EquipmentEvolutionPresentationRequest pending = beforeState
            .presentationRequests?.SingleOrDefault(value => value != null
                && string.Equals(
                    value.presentationId,
                    equipmentReplayInput.RequestId,
                    StringComparison.Ordinal));
        EvolutionNode pendingNode = beforeState.evolutionNodes?
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.nodeId,
                    equipmentReplayInput.FormulaId,
                    StringComparison.Ordinal));
        Require(pending != null && pendingNode != null
                && pending.state
                    == EquipmentEvolutionPresentationState.ModuleSelectionPending
                && pendingNode.presentationState
                    == EquipmentEvolutionPresentationState.ModuleSelectionPending,
            "Source-v9 replay pending request or node is not commit-ready.");
        Require(combatEquipment.TryGetActiveWeapon(
                    equipmentReplayInput.OwnerId,
                    out CombatWeaponSnapshot beforeWeapon)
                && string.Equals(
                    beforeWeapon.InstanceId,
                    equipmentReplayInput.EquipmentInstanceId,
                    StringComparison.Ordinal),
            "Source-v9 replay equipment is not the owner's active production weapon before commit.");

        Require(equipmentReplayInput.Response.positiveModuleIds.Count == 1
                && equipmentReplayInput.Response.drawbackModuleIds.Count == 0,
            "This equipment consumer witness requires the offered single positive module with its inseparable burden.");
        IEvolutionModuleRegistry moduleRegistry = scope.Container.Resolve<IEvolutionModuleRegistry>();
        Require(moduleRegistry.TryGet(equipmentReplayInput.Response.positiveModuleIds[0],
                    out EvolutionModuleDefinition selectedModule)
                && selectedModule.Benefits.Count == 1 && selectedModule.Burdens.Count == 1,
            "Selected equipment module has no single authoritative benefit/burden definition.");
        string benefitStatId = selectedModule.Benefits[0].statId;
        string burdenStatId = selectedModule.Burdens[0].statId;
        Require((benefitStatId == "combat.accuracy" && burdenStatId == "combat.damage")
                || (benefitStatId == "combat.damage" && burdenStatId == "combat.accuracy"),
            "Selected equipment module has an unsupported consumer axis: "
                + benefitStatId + " / " + burdenStatId);

        var beforeEvidenceUse = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string evidenceId in equipmentReplayInput.SelectedEvidenceFactIds)
        {
            EquipmentEvolutionFormulaEvidenceRecord evidence = beforeState
                .formulaEvidence?.SingleOrDefault(value => value != null
                    && string.Equals(
                        value.evidenceId,
                        evidenceId,
                        StringComparison.Ordinal));
            Require(evidence != null,
                "The explicit replay fixture currently requires legacy formula evidence: "
                    + evidenceId);
            beforeEvidenceUse.Add(evidenceId, evidence.influenceUseCount);
        }

        string beforeWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-before-world.json", beforeWorld);
        WriteNew("u4-before-equipment.json", JsonUtility.ToJson(beforeState, true));

        queue.ConfigureIsolatedModuleReplayTestResponses(captured =>
        {
            Require(equipmentReplayCapture != null
                    && ReferenceEquals(captured, equipmentReplayCapture),
                "The replay response provider did not receive the observed request.");
            Require(equipmentReplayResponseCount == 0,
                "The explicit source-v9 response was requested more than once.");
            equipmentReplayResponseCount++;
            return equipmentReplayInput.AcceptedResponseJson;
        });
        Require(equipmentPresentation
                .TrySubmitNextFormulaPresentationForIsolatedCapture(),
            "Production equipment presentation did not submit the restored pending request.");
        Require(queue.TryDispatchQueuedRequestForIsolatedCapture(
                equipmentReplayInput.RequestId),
            "The restored source-v9 request was not uniquely queued for replay: "
                + queue.GetRequestDiagnosticsForDebug());

        float deadline = Time.realtimeSinceStartup + 30f;
        EquipmentEvolutionState appliedState = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            appliedState = equipmentEvolution.GetState(
                equipmentReplayInput.EquipmentInstanceId);
            bool pendingRemoved = appliedState?.presentationRequests?.All(value =>
                    value == null || !string.Equals(
                        value.presentationId,
                        equipmentReplayInput.RequestId,
                        StringComparison.Ordinal)) == true;
            if (equipmentReplayResponseCount == 1
                && appliedState != null
                && appliedState.moduleSelectionRevision
                    == beforeState.moduleSelectionRevision + 1L
                && pendingRemoved)
            {
                break;
            }
            yield return null;
        }
        queue.ClearIsolatedModuleReplayTestResponses();
        Require(equipmentReplayCapture != null
                && equipmentReplayResponseCount == 1
                && appliedState != null
                && appliedState.moduleSelectionRevision
                    == beforeState.moduleSelectionRevision + 1L,
            "The fixed source-v9 response did not commit exactly once through the production callback.");
        EvolutionNode appliedNode = appliedState.evolutionNodes.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.nodeId,
                equipmentReplayInput.FormulaId,
                StringComparison.Ordinal));
        Require(appliedNode != null
                && appliedNode.active
                && appliedNode.mechanicallyUnlocked
                && appliedNode.narrativeReady
                && appliedNode.uiVisible
                && appliedNode.playerVisible
                && appliedNode.presentationState
                    == EquipmentEvolutionPresentationState.Ready
                && string.Equals(
                    appliedNode.displayName,
                    equipmentReplayInput.Response.displayName,
                    StringComparison.Ordinal)
                && string.Equals(
                    appliedNode.narrativeFlavor,
                    equipmentReplayInput.Response.narrativeFlavor,
                    StringComparison.Ordinal)
                && equipmentReplayInput.SelectedEvidenceFactIds.All(id =>
                    appliedNode.evidenceIds.Contains(id, StringComparer.Ordinal)),
            "Committed equipment node does not match the fixed response.");
        Require(combatEquipment.TryGetActiveWeapon(
                    equipmentReplayInput.OwnerId,
                    out CombatWeaponSnapshot appliedWeapon)
                && string.Equals(
                    appliedWeapon.InstanceId,
                    equipmentReplayInput.EquipmentInstanceId,
                    StringComparison.Ordinal),
            "Committed equipment is not the owner's active production weapon.");

        var afterEvidenceUse = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, int> before in beforeEvidenceUse)
        {
            EquipmentEvolutionFormulaEvidenceRecord after = appliedState
                .formulaEvidence.Single(value => value != null
                    && string.Equals(
                        value.evidenceId,
                        before.Key,
                        StringComparison.Ordinal));
            afterEvidenceUse.Add(before.Key, after.influenceUseCount);
            Require(after.influenceUseCount == before.Value + 1,
                "Selected legacy formula evidence was not consumed exactly once: "
                    + before.Key);
        }

        string committedStateJson = JsonUtility.ToJson(appliedState);
        bool duplicateRejected = !equipmentEvolution.TryCommitModuleSelection(
            equipmentReplayInput.EquipmentInstanceId,
            equipmentReplayInput.Response,
            out string duplicateFailure);
        EquipmentEvolutionState afterDuplicate = equipmentEvolution.GetState(
            equipmentReplayInput.EquipmentInstanceId);
        Require(duplicateRejected
                && !string.IsNullOrWhiteSpace(duplicateFailure)
                && string.Equals(
                    committedStateJson,
                    JsonUtility.ToJson(afterDuplicate),
                    StringComparison.Ordinal),
            "Duplicate module-selection commit was accepted or changed state.");

        EquipmentAttackWitness attackApplied = null;
        yield return RunEquipmentAttackWitness("applied", value => attackApplied = value);
        afterDuplicate = equipmentEvolution.GetState(equipmentReplayInput.EquipmentInstanceId);
        committedStateJson = JsonUtility.ToJson(afterDuplicate);
        string appliedWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-applied-world.json", appliedWorld);
        WriteNew("u4-applied-equipment.json", JsonUtility.ToJson(afterDuplicate, true));
        CharacterActor previousOwner = owner;
        Require(saves.TryRestore(
                saves.FromJson(appliedWorld),
                out DungeonGameRestoreReport restored),
            "Official replay result restore failed: "
                + string.Join(" | ", restored.Errors));
        Require(scope.Container.Resolve<ICharacterWorldSaveService>()
                .TryGetRestoredActor(equipmentReplayInput.OwnerId, out owner)
                && !ReferenceEquals(previousOwner, owner),
            "Official replay restore did not publish a fresh owner.");
        Require(combatEquipment.TryGetInstance(
                equipmentReplayInput.EquipmentInstanceId,
                out CombatEquipmentInstance restoredInstance),
            "Official replay restore omitted the committed equipment.");
        EquipmentEvolutionState restoredState = restoredInstance.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        Require(string.Equals(
                    committedStateJson,
                    JsonUtility.ToJson(restoredState),
                    StringComparison.Ordinal),
            "Equipment module-selection state changed across official save/restore.");
        Require(combatEquipment.TryGetActiveWeapon(
                    equipmentReplayInput.OwnerId,
                    out CombatWeaponSnapshot restoredWeapon)
                && string.Equals(
                    restoredWeapon.InstanceId,
                    equipmentReplayInput.EquipmentInstanceId,
                    StringComparison.Ordinal),
            "Official replay restore did not publish the evolved active weapon.");
        EquipmentMechanicWitness mechanicWitness = new()
        {
            ownerId = equipmentReplayInput.OwnerId,
            equipmentInstanceId = equipmentReplayInput.EquipmentInstanceId,
            definitionId = appliedWeapon.DefinitionId,
            benefitStatId = benefitStatId,
            burdenStatId = burdenStatId,
            accuracyBefore = beforeWeapon.EvolutionAccuracyMultiplier,
            accuracyApplied = appliedWeapon.EvolutionAccuracyMultiplier,
            accuracyRestored = restoredWeapon.EvolutionAccuracyMultiplier,
            damageBefore = beforeWeapon.MaterialDamageMultiplier,
            damageApplied = appliedWeapon.MaterialDamageMultiplier,
            damageRestored = restoredWeapon.MaterialDamageMultiplier
        };
        mechanicWitness.activeWeaponBound = string.Equals(
                beforeWeapon.InstanceId,
                mechanicWitness.equipmentInstanceId,
                StringComparison.Ordinal)
            && string.Equals(
                appliedWeapon.InstanceId,
                mechanicWitness.equipmentInstanceId,
                StringComparison.Ordinal)
            && string.Equals(
                restoredWeapon.InstanceId,
                mechanicWitness.equipmentInstanceId,
                StringComparison.Ordinal);
        mechanicWitness.accuracyBenefitApplied =
            mechanicWitness.accuracyApplied > mechanicWitness.accuracyBefore;
        mechanicWitness.damageBurdenApplied =
            mechanicWitness.damageApplied < mechanicWitness.damageBefore;
        mechanicWitness.damageBenefitApplied =
            mechanicWitness.damageApplied > mechanicWitness.damageBefore;
        mechanicWitness.accuracyBurdenApplied =
            mechanicWitness.accuracyApplied < mechanicWitness.accuracyBefore;
        mechanicWitness.selectedBenefitApplied = benefitStatId == "combat.accuracy"
            ? mechanicWitness.accuracyBenefitApplied : mechanicWitness.damageBenefitApplied;
        mechanicWitness.selectedBurdenApplied = burdenStatId == "combat.damage"
            ? mechanicWitness.damageBurdenApplied : mechanicWitness.accuracyBurdenApplied;
        mechanicWitness.officialProjectionRoundTrip = Mathf.Approximately(
                mechanicWitness.accuracyApplied,
                mechanicWitness.accuracyRestored)
            && Mathf.Approximately(
                mechanicWitness.damageApplied,
                mechanicWitness.damageRestored);
        Require(mechanicWitness.activeWeaponBound
                && mechanicWitness.selectedBenefitApplied
                && mechanicWitness.selectedBurdenApplied
                && mechanicWitness.officialProjectionRoundTrip,
            "The selected module did not preserve its authoritative production benefit/burden axes across official restore.");
        string restoredWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-restored-world.json", restoredWorld);
        WriteNew("u4-restored-equipment.json", JsonUtility.ToJson(restoredState, true));
        EquipmentAttackWitness attackRestored = null;
        yield return RunEquipmentAttackWitness("restored", value => attackRestored = value);
        mechanicWitness.attackApplied = attackApplied;
        mechanicWitness.attackRestored = attackRestored;
        mechanicWitness.actualAttackExecuted = attackApplied.actualDamageApplied
            && attackApplied.exactEquipmentBound && attackApplied.noDuplicateOutcomes;
        mechanicWitness.actualAttackRoundTrip = attackRestored.actualDamageApplied
            && attackRestored.exactEquipmentBound && attackRestored.noDuplicateOutcomes
            && attackRestored.targetId == attackApplied.targetId
            && Mathf.Approximately(attackRestored.accuracyMultiplier, attackApplied.accuracyMultiplier)
            && Mathf.Approximately(attackRestored.damageMultiplier, attackApplied.damageMultiplier);
        Require(mechanicWitness.actualAttackExecuted && mechanicWitness.actualAttackRoundTrip,
            "The selected equipment effect was not consumed by an actual attack before and after official restore.");
        report.equipmentMechanic = mechanicWitness;
        report.equipmentMechanicExecuted = true;
        WriteNew("u4-restored-attack-world.json", saves.ToJson(saves.Capture()));
        WriteNew("u4-equipment-mechanic-witness.json",
            JsonUtility.ToJson(mechanicWitness, true));

        WriteEquipmentReplayProof(
            beforeState,
            afterDuplicate,
            restoredState,
            beforeEvidenceUse,
            afterEvidenceUse,
            beforeWorld,
            appliedWorld,
            restoredWorld,
            mechanicWitness,
            duplicateFailure);
        report.responseApplied = true;
        report.networkSent = false;
    }

    private IEnumerator RunEquipmentAttackWitness(
        string label, Action<EquipmentAttackWitness> completedWitness,
        string explicitOwnerId = null, string explicitWeaponId = null)
    {
        string attackOwnerId = explicitOwnerId ?? equipmentReplayInput.OwnerId;
        string attackWeaponId = explicitWeaponId ?? equipmentReplayInput.EquipmentInstanceId;
        string artifactPrefix = explicitOwnerId == null ? "u4-equipment-attack-" : "u4-trait-attack-";
        phase = artifactPrefix + label;
        combatCommands = scope.Container.Resolve<ICharacterCombatCommandRuntime>();
        ICharacterWorldQuery characters = scope.Container.Resolve<ICharacterWorldQuery>();
        CharacterActor target = characters.Characters.Where(value => value != null
                && !value.IsDead && value.CurrentLifecycleState == CharacterLifecycleState.Active
                && value.Identity != null && value.Identity.PersistentId != owner.Identity.PersistentId)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Require(target != null, "Selected-equipment attack requires a living published target.");
        target.Brain?.StopCurrentActionForReplan("qa:selected-equipment-attack-target");
        target.GetComponent<AbilityMove>()?.CancelActiveMovement();
        target.SetAiPaused(true);
        Grid grid = scope.Container.Resolve<IGridSystemProvider>().Grid;
        Vector2Int actorCell = owner.GetNowXY();
        // A contact shot is an authored shortbow penalty, not a neutral test
        // of the selected accuracy effect. Use its ordinary near-range band;
        // retain the real environment, random stream and damage resolution.
        Vector2Int? targetCell = new[] { 2, -2, 3, -3, 4, -4, 5, -5 }
            .Select(offset => new Vector2Int(actorCell.x + offset, actorCell.y))
            .Where(cell => grid.IsValidGridPos(cell) && grid.IsWalkable(cell)
                && !characters.Characters.Any(value => value != null && value != owner
                    && value != target && !value.IsDead && value.GetNowXY() == cell))
            .Select(cell => (Vector2Int?)cell).FirstOrDefault();
        Require(targetCell.HasValue, "Selected-equipment attack requires a clear same-floor near-range fixture cell.");
        target.transform.position = grid.GetWorldPos(targetCell.Value);
        Physics2D.SyncTransforms();
        Require(target.GetNowXY() == targetCell.Value, "Selected-equipment target cell changed.");
        ICharacterBodyHealthQuery health = scope.Container.Resolve<ICharacterBodyHealthQuery>();
        scope.Container.Resolve<ICharacterBodyHealthCommand>().ConfigureVitals(target, 10000f,
            resetCurrentHealth: true);
        Require(combatEquipment.TryGetActiveWeapon(attackOwnerId, out CombatWeaponSnapshot weapon)
                && weapon.InstanceId == attackWeaponId && weapon.RequiresAmmo,
            "Selected-equipment attack is not bound to the evolved ranged weapon.");
        int distance = Mathf.Abs(actorCell.x - targetCell.Value.x)
            + Mathf.Abs(actorCell.y - targetCell.Value.y);
        Require(CombatRangeRules.GetBand(distance) == CombatRangeBand.Near,
            "Selected-equipment fixture must use the authored near-range band.");
        Require(scope.Container.Resolve<IEnvironmentalFieldQuery>()
                .TryGetCell(actorCell, out EnvironmentalCellSnapshot actorEnvironment),
            "Selected-equipment fixture requires its actual environmental cell.");
        EquipmentAttackWitness witness = new()
        {
            actorId = attackOwnerId, targetId = target.Identity.PersistentId,
            equipmentInstanceId = weapon.InstanceId, ammunitionBefore = weapon.LoadedAmmo,
            targetHealthBefore = health.GetVitals(target).CurrentHealth,
            accuracyMultiplier = weapon.EvolutionAccuracyMultiplier,
            damageMultiplier = weapon.MaterialDamageMultiplier,
            actorCell = actorCell, targetCell = targetCell.Value, distance = distance,
            rangeAccuracyMultiplier = weapon.GetAccuracyMultiplier(CombatRangeBand.Near),
            actorLightLevel = actorEnvironment.LightLevel
        };
        HashSet<string> beforeKeys = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        float previousScale = timeScale.Scale;
        float previousMaximumDeltaTime = Time.maximumDeltaTime;
        string Fact(GameplayOutcomeSnapshot outcome, string id) => outcome.facts
            .Single(value => value.factId == id).value;
        List<GameplayOutcomeSnapshot> NewAttacks() => ReadAllOutcomes().Where(value =>
                !beforeKeys.Contains(OutcomeKey(value)) && value.outcomeTypeId == "combat.attack-resolved"
                && value.participants.Any(participant => participant.entityId == witness.actorId
                    && participant.roleId == "actor"))
            .ToList();
        List<GameplayOutcomeSnapshot> NewDamage() => ReadAllOutcomes().Where(value =>
                !beforeKeys.Contains(OutcomeKey(value)) && value.outcomeTypeId == "combat.damage-resolved"
                && value.participants.Any(participant => participant.entityId == witness.actorId
                    && participant.roleId == "attacker")
                && value.participants.Any(participant => participant.entityId == witness.targetId
                    && participant.roleId == "victim"))
            .ToList();
        try
        {
            CharacterPerformanceExecutionTrace.Clear();
            Require(combatCommands.SetCombatStance(owner, true, out string stanceFailure),
                "Selected-equipment combat stance rejected: " + stanceFailure);
            Require(combatCommands.TrySetFireMode(owner, CombatFireMode.Aimed, out string modeFailure),
                "Selected-equipment aimed mode rejected: " + modeFailure);
            witness.publicCommandIssued = combatCommands.TryIssueAttack(owner, new CombatParticipantRef(target),
                forceFire: true, out string attackFailure);
            Require(witness.publicCommandIssued, "Selected-equipment public attack rejected: " + attackFailure);
            Require(combatCommands.TryGetCommand(owner, out CharacterCombatCommand command),
                "Selected-equipment public attack did not create a command.");
            witness.commandId = command.commandId;
            gameSpeed.SetPaused(false);
            gameSpeed.SetSpeed(5);
            Time.maximumDeltaTime = 0.6f;
            timeScale.Scale = originalScale;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < deadline)
            {
                Require(combatCommands.TryGetCommand(owner, out CharacterCombatCommand observedCommand)
                        && observedCommand.commandId == witness.commandId,
                    "Selected-equipment command disappeared or changed during the bounded attack.");
                witness.lastCommandState = observedCommand.state.ToString();
                witness.lastCommandStatus = observedCommand.status;
                witness.lastCommandRevision = observedCommand.revision;
                Require(observedCommand.state != CharacterCombatCommandState.Blocked,
                    "Selected-equipment production command blocked: " + observedCommand.status);
                witness.outcomes = NewAttacks();
                witness.targetHealthAfter = health.GetVitals(target).CurrentHealth;
                witness.actualDamageApplied = witness.targetHealthAfter < witness.targetHealthBefore
                    && witness.outcomes.Any(value => Fact(value, "result-flags").Contains("hit=1")
                        && float.Parse(Fact(value, "detail").Split(';').Single(part =>
                            part.StartsWith("damage=", StringComparison.Ordinal)).Substring(7),
                            System.Globalization.CultureInfo.InvariantCulture) > 0f);
                if (witness.actualDamageApplied) break;
                Require(combatEquipment.TryGetActiveWeapon(witness.actorId, out CombatWeaponSnapshot current)
                        && current.InstanceId == witness.equipmentInstanceId,
                    "Selected-equipment attack switched to a different weapon.");
                if (current.LoadedAmmo <= 0)
                {
                    Require(witness.ammunitionLoads < 8,
                        "Selected-equipment attack exhausted eight bounded shots without actual damage.");
                    Require(combatEquipment.TryLoadExternalAmmunition(current.InstanceId, "ammo:arrow", 1),
                        "Selected-equipment controlled arrow load rejected.");
                    witness.ammunitionLoads++;
                }
                yield return null;
            }
            timeScale.Scale = 0;
            Require(combatCommands.SetCombatStance(owner, false, out string releaseFailure),
                "Selected-equipment command cleanup rejected: " + releaseFailure);
            witness.outcomes = NewAttacks();
            witness.targetHealthAfter = health.GetVitals(target).CurrentHealth;
            Require(combatEquipment.TryGetActiveWeapon(witness.actorId, out CombatWeaponSnapshot after),
                "Selected-equipment weapon disappeared after actual attack.");
            witness.ammunitionAfter = after.LoadedAmmo;
            witness.ammunitionConsumed = witness.outcomes.Sum(value => int.Parse(Fact(value, "ammunition-consumed")));
            witness.exactEquipmentBound = witness.outcomes.Count > 0 && witness.outcomes.All(value =>
                Fact(value, "weapon-instance-id") == witness.equipmentInstanceId
                && Fact(value, "weapon-definition-id") == weapon.DefinitionId
                && Fact(value, "fire-mode") == "Aimed"
                && Fact(value, "result-flags").StartsWith("executed=1;", StringComparison.Ordinal)
                && value.status == GameplayOutcomeStatus.Succeeded
                && value.participants.Any(participant => participant.entityId == witness.targetId
                    && participant.roleId == "target"));
            int committedCount = witness.outcomes.Count;
            witness.damageOutcomes = NewDamage();
            for (int frame = 0; frame < 5; frame++) yield return null;
            witness.noDuplicateOutcomes = NewAttacks().Count == committedCount
                && witness.outcomes.Select(OutcomeKey).Distinct(StringComparer.Ordinal).Count() == committedCount
                && NewDamage().Count == witness.damageOutcomes.Count
                && witness.damageOutcomes.Select(OutcomeKey).Distinct(StringComparer.Ordinal).Count()
                    == witness.damageOutcomes.Count
                && witness.ammunitionConsumed == witness.ammunitionBefore + witness.ammunitionLoads - witness.ammunitionAfter
                && witness.ammunitionConsumed == committedCount;
            CharacterPerformanceExecutionTraceEntry rawDamage = CharacterPerformanceExecutionTrace.Snapshot()
                .SingleOrDefault(value => value.ConsumerId == "CombatResolutionService.CalculateRawDamage");
            witness.rawDamageConsumerCount = rawDamage?.Count ?? 0;
            witness.consumedCombatPower = rawDamage?.InputValue ?? 0f;
            witness.calculatedRawDamage = rawDamage?.OutputValue ?? 0f;
            witness.rawDamageOperationId = rawDamage?.Detail;
            Require(witness.actualDamageApplied && witness.exactEquipmentBound && witness.noDuplicateOutcomes,
                "Selected-equipment actual attack, target damage, exact ammunition or outcome uniqueness failed.");
            completedWitness(witness);
        }
        finally
        {
            if (!witness.actualDamageApplied)
            {
                timeScale.Scale = 0;
                WriteNew(artifactPrefix + label + "-failed-world.json",
                    saves.ToJson(saves.Capture()));
            }
            timeScale.Scale = previousScale;
            Time.maximumDeltaTime = previousMaximumDeltaTime;
            gameSpeed.SetSpeed(originalGameSpeed);
            gameSpeed.SetPaused(originalGamePaused);
            combatCommands.SetCombatStance(owner, false, out _);
            WriteNew(artifactPrefix + label + ".json", JsonUtility.ToJson(witness, true));
        }
    }

    private IEnumerator RunFacilityReplay()
    {
        phase = "facility-source-v9-replay";
        facilityPhase = true;
        replaying = true;
        CancelOutstandingTransport();
        facilityPresentation = scope.Container
            .Resolve<FacilityFormulaEvolutionPresentationRuntime>();
        FacilityEvolutionRuntime evolution = scope.Container
            .Resolve<FacilityFeatureSceneRuntimeReferences>().Evolution;
        IBuildingWorldQuery buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        IFacilityEvolutionModifierQuery modifiers = scope.Container
            .Resolve<IFacilityEvolutionModifierQuery>();
        Require(facilityPresentation != null && evolution != null
                && buildings != null && modifiers != null,
            "Facility replay requires the production presentation, evolution, building and modifier runtimes.");

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        facilityReplayInput = GameplayModuleSelectionSourceV9ReplayInput
            .LoadFacility(
                projectRoot,
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE"),
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE"),
                Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD"));
        facilityOwner = buildings.Buildings.SingleOrDefault(value => value != null
            && !value.isDestroy
            && string.Equals(value.PersistentInstanceId.Value,
                facilityReplayInput.FacilityId, StringComparison.Ordinal));
        Require(facilityOwner != null,
            "Source-v9 replay facility is absent after official restore.");
        FacilityEvolutionStateComponent beforeComponent = facilityOwner
            .GetComponent<FacilityEvolutionStateComponent>();
        FacilityEvolutionFormulaPresentationPendingSnapshot livePending =
            beforeComponent?.PendingFormulaPresentation;
        Require(beforeComponent != null && livePending != null
                && string.Equals(JsonUtility.ToJson(livePending),
                    JsonUtility.ToJson(facilityReplayInput.Pending),
                    StringComparison.Ordinal),
            "Restored facility pending state differs from the source-v9 package.");
        FacilityEvolutionStateSnapshot beforeState = beforeComponent.CreateSnapshot();
        Require(beforeState.instanceEvolution.evolutionNodes.All(value => value == null
                    || !string.Equals(value.nodeId,
                        facilityReplayInput.FormulaId, StringComparison.Ordinal)),
            "Source-v9 facility formula was already committed before replay.");
        Dictionary<string, GameplayOutcomeNarrativeEvidenceSource> beforeEvidence =
            CurrentFacilityEvidence(facilityReplayInput.SelectedEvidenceBindings);
        float multiplierBefore = modifiers.GetMultiplier(
            facilityOwner, "service.support-speed");
        FacilityServiceWitness serviceBefore = null;
        yield return RunFacilityServiceWitness(facilityOwner, false, value => serviceBefore = value);
        WriteNew("u4-service-before.json", JsonUtility.ToJson(serviceBefore, true));
        string beforeWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-before-world.json", beforeWorld);
        WriteNew("u4-before-facility.json", JsonUtility.ToJson(beforeState, true));

        queue.ConfigureIsolatedModuleReplayTestResponses(captured =>
        {
            Require(facilityReplayCapture != null
                    && ReferenceEquals(captured, facilityReplayCapture),
                "The facility replay response provider did not receive the observed request.");
            Require(facilityReplayResponseCount == 0,
                "The explicit facility source-v9 response was requested more than once.");
            facilityReplayResponseCount++;
            return facilityReplayInput.AcceptedResponseJson;
        });

        float deadline = Time.realtimeSinceStartup + 30f;
        BuildableObject appliedFacility = null;
        FacilityEvolutionStateSnapshot appliedState = null;
        bool committed = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (facilityReplayCapture == null)
            {
                queue.TryDispatchQueuedRequestForIsolatedCapture(
                    facilityReplayInput.RequestId);
            }
            appliedFacility = buildings.Buildings.SingleOrDefault(value => value != null
                && !value.isDestroy
                && string.Equals(value.PersistentInstanceId.Value,
                    facilityReplayInput.FacilityId, StringComparison.Ordinal));
            appliedState = appliedFacility?
                .GetComponent<FacilityEvolutionStateComponent>()?.CreateSnapshot();
            committed = appliedState != null
                && appliedState.pendingFormulaPresentation == null
                && appliedState.instanceEvolution.evolutionNodes.Count(value => value != null
                    && string.Equals(value.nodeId,
                        facilityReplayInput.FormulaId, StringComparison.Ordinal)) == 1;
            if (facilityReplayResponseCount == 1 && committed)
                break;
            yield return null;
        }
        queue.ClearIsolatedModuleReplayTestResponses();
        Require(facilityReplayCapture != null
                && facilityReplayResponseCount == 1
                && appliedFacility != null
                && appliedState != null,
            "The fixed facility source-v9 response did not commit exactly once through the production callback.");
        EvolutionNode committedNode = appliedState.instanceEvolution.evolutionNodes
            .SingleOrDefault(value => value != null
                && string.Equals(value.nodeId,
                    facilityReplayInput.FormulaId, StringComparison.Ordinal));
        WriteNew("u4-committed-facility-pre-validation.json",
            JsonUtility.ToJson(appliedState, true));
        WriteNew("u4-committed-node-pre-validation.json",
            JsonUtility.ToJson(committedNode, true));
        Require(committed,
            "The facility response was delivered, but its formula node was not committed. "
            + "Inspect the facility response rejection log and pre-validation state.");
        Require(committedNode != null
                && committedNode.active
                && committedNode.mechanicallyUnlocked
                && committedNode.narrativeReady
                && committedNode.uiVisible
                && committedNode.playerVisible
                && committedNode.presentationState
                    == EquipmentEvolutionPresentationState.Ready
                && string.Equals(committedNode.effectId,
                    facilityReplayInput.SelectedModuleIds.Single(),
                    StringComparison.Ordinal)
                && string.IsNullOrEmpty(committedNode.burdenEffectId)
                && string.Equals(committedNode.displayName,
                    facilityReplayInput.Response.displayName,
                    StringComparison.Ordinal)
                && string.Equals(committedNode.narrativeFlavor,
                    facilityReplayInput.Response.narrativeFlavor,
                    StringComparison.Ordinal)
                && facilityReplayInput.SelectedEvidenceFactIds.All(id =>
                    committedNode.evidenceIds.Contains(id,
                        StringComparer.Ordinal)),
            "Committed facility node does not match the fixed response.");
        Require(!string.Equals(beforeState.currentFacilityId,
                    appliedState.currentFacilityId, StringComparison.Ordinal)
                && !ReferenceEquals(facilityOwner, appliedFacility),
            "Facility module selection did not publish the evolved replacement building.");

        Dictionary<string, GameplayOutcomeNarrativeEvidenceSource> afterEvidence =
            CurrentFacilityEvidence(facilityReplayInput.SelectedEvidenceBindings);
        foreach (KeyValuePair<string, GameplayOutcomeNarrativeEvidenceSource> pair
                 in beforeEvidence)
        {
            GameplayOutcomeNarrativeEvidenceSource after = afterEvidence[pair.Key];
            Require(after.InfluenceUseCount == pair.Value.InfluenceUseCount + 1
                    && after.InfluenceRevision == pair.Value.InfluenceRevision + 1,
                "Selected exact facility evidence was not consumed exactly once: "
                    + pair.Key);
        }
        float multiplierApplied = modifiers.GetMultiplier(
            appliedFacility, "service.support-speed");
        FacilityServiceWitness serviceApplied = null;
        yield return RunFacilityServiceWitness(appliedFacility, true, value => serviceApplied = value);
        WriteNew("u4-service-applied.json", JsonUtility.ToJson(serviceApplied, true));
        appliedState = appliedFacility.GetComponent<FacilityEvolutionStateComponent>().CreateSnapshot();

        string committedStateJson = JsonUtility.ToJson(appliedState);
        string worldBeforeDuplicate = saves.ToJson(saves.Capture());
        bool duplicateRejected = !evolution.TryCommitFormulaModuleSelection(
            appliedFacility,
            facilityReplayInput.Response,
            out _,
            out string duplicateFailure);
        string worldAfterDuplicate = saves.ToJson(saves.Capture());
        WriteNew("u4-duplicate-before-world.json", worldBeforeDuplicate);
        WriteNew("u4-duplicate-after-world.json", worldAfterDuplicate);
        JObject beforeDuplicateSemantic = JObject.Parse(worldBeforeDuplicate);
        JObject afterDuplicateSemantic = JObject.Parse(worldAfterDuplicate);
        // Capture always stamps DateTime.UtcNow. Preserve those source bytes,
        // but compare every game/save field independently of that envelope time.
        beforeDuplicateSemantic.Remove("savedAtUtc");
        afterDuplicateSemantic.Remove("savedAtUtc");
        Require(duplicateRejected
                && !string.IsNullOrWhiteSpace(duplicateFailure)
                && JToken.DeepEquals(beforeDuplicateSemantic, afterDuplicateSemantic),
            "Duplicate facility module-selection commit was accepted or changed world state.");

        string appliedWorld = worldAfterDuplicate;
        WriteNew("u4-applied-world.json", appliedWorld);
        WriteNew("u4-applied-facility.json", JsonUtility.ToJson(appliedState, true));
        BuildableObject previousApplied = appliedFacility;
        Require(saves.TryRestore(
                saves.FromJson(appliedWorld),
                out DungeonGameRestoreReport restored),
            "Official facility replay result restore failed: "
                + string.Join(" | ", restored.Errors));
        BuildableObject restoredFacility = buildings.Buildings.SingleOrDefault(value =>
            value != null && !value.isDestroy
            && string.Equals(value.PersistentInstanceId.Value,
                facilityReplayInput.FacilityId, StringComparison.Ordinal));
        Require(restoredFacility != null
                && !ReferenceEquals(previousApplied, restoredFacility),
            "Official facility replay restore did not publish a fresh evolved building.");
        FacilityEvolutionStateSnapshot restoredState = restoredFacility
            .GetComponent<FacilityEvolutionStateComponent>()?.CreateSnapshot();
        Require(restoredState != null
                && string.Equals(committedStateJson,
                    JsonUtility.ToJson(restoredState), StringComparison.Ordinal),
            "Facility module-selection state changed across official save/restore.");
        Dictionary<string, GameplayOutcomeNarrativeEvidenceSource> restoredEvidence =
            CurrentFacilityEvidence(facilityReplayInput.SelectedEvidenceBindings);
        Require(afterEvidence.All(pair => restoredEvidence.TryGetValue(pair.Key,
                    out GameplayOutcomeNarrativeEvidenceSource restoredSource)
                && restoredSource.InfluenceUseCount == pair.Value.InfluenceUseCount
                && restoredSource.InfluenceRevision == pair.Value.InfluenceRevision),
            "Facility evidence influence changed across official save/restore.");
        float multiplierRestored = modifiers.GetMultiplier(
            restoredFacility, "service.support-speed");
        string restoredWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-restored-world.json", restoredWorld);
        WriteNew("u4-restored-facility.json", JsonUtility.ToJson(restoredState, true));
        FacilityServiceWitness serviceRestored = null;
        yield return RunFacilityServiceWitness(restoredFacility, true, value => serviceRestored = value);
        WriteNew("u4-service-restored.json", JsonUtility.ToJson(serviceRestored, true));
        WriteNew("u4-restored-service-world.json", saves.ToJson(saves.Capture()));
        FacilityMechanicWitness mechanicWitness = new()
        {
            facilityId = facilityReplayInput.FacilityId,
            formulaId = facilityReplayInput.FormulaId,
            moduleId = facilityReplayInput.SelectedModuleIds.Single(),
            multiplierBefore = multiplierBefore,
            multiplierApplied = multiplierApplied,
            multiplierRestored = multiplierRestored,
            currentFacilityBound = string.Equals(
                restoredFacility.PersistentInstanceId.Value,
                facilityReplayInput.FacilityId,
                StringComparison.Ordinal),
            supportBenefitApplied = multiplierApplied > multiplierBefore,
            officialProjectionRoundTrip = Mathf.Approximately(
                multiplierApplied, multiplierRestored),
            serviceBefore = serviceBefore,
            serviceApplied = serviceApplied,
            serviceRestored = serviceRestored,
            actualServiceBenefitApplied = serviceApplied.completed
                && serviceApplied.serviceSeconds < serviceBefore.serviceSeconds,
            actualServiceRoundTrip = serviceRestored.completed
                && Mathf.Approximately(serviceApplied.serviceSeconds, serviceRestored.serviceSeconds)
                && serviceApplied.hubId == serviceRestored.hubId
                && serviceApplied.processId == serviceRestored.processId
        };
        Require(mechanicWitness.currentFacilityBound
                && mechanicWitness.supportBenefitApplied
                && mechanicWitness.officialProjectionRoundTrip
                && mechanicWitness.actualServiceBenefitApplied
                && mechanicWitness.actualServiceRoundTrip,
            "The selected facility support did not shorten an actual service and retain that effect after official restore.");
        report.facilityMechanic = mechanicWitness;
        report.facilityMechanicExecuted = true;
        WriteNew("u4-facility-mechanic-witness.json",
            JsonUtility.ToJson(mechanicWitness, true));
        WriteFacilityReplayProof(
            beforeState,
            appliedState,
            restoredState,
            beforeEvidence,
            afterEvidence,
            beforeWorld,
            appliedWorld,
            restoredWorld,
            mechanicWitness,
            duplicateFailure);
        report.responseApplied = true;
        report.networkSent = false;
    }

    private IEnumerator RunFacilityServiceWitness(BuildableObject support, bool execute,
        Action<FacilityServiceWitness> completedWitness)
    {
        IServiceRoomLinkRuntime links = scope.Container.Resolve<IServiceRoomLinkRuntime>();
        IServiceSessionRuntime sessions = scope.Container.Resolve<IServiceSessionRuntime>();
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        Require(links.TryGetLinkForSupport(support, out ServiceSupportLinkSnapshot link)
                && link.Hub != null,
            "The evolved support has no actual operational service hub link.");
        Require(scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out var manager)
                && manager.CurrentOwnerActor != null && !manager.CurrentOwnerActor.IsDead,
            "The service witness requires the current living owner.");
        Require(sessions.SwitchToDirect(link.Hub).Succeeded,
            "The actual service hub could not enter Direct mode.");
        string processId = link.Hub.GetServiceHubAbility().supportedProcessIds.Single();
        Require(sessions.TryBeginSession(new ServiceSessionRequest
            {
                Hub = link.Hub, Actor = manager.CurrentOwnerActor,
                ProcessId = processId, IsInternalActor = true, AdvertisedDemand = false
            }, out ServiceSessionSnapshot session, out DomainFailure beginFailure),
            "Actual facility support service entry failed: " + beginFailure.Code);
        FacilityServiceWitness witness = new()
        {
            facilityId = support.PersistentInstanceId.Value,
            hubId = session.HubId, supportId = link.SupportId,
            processId = session.ProcessId, sessionId = session.SessionId,
            serviceSeconds = session.Contract.serviceSeconds
        };
        Require(session.Contract.supportIds.Contains(link.SupportId, StringComparer.Ordinal),
            "The actual service contract did not consume the selected support.");
        try
        {
            if (execute)
            {
                Require(sessions.TrySetStage(session.SessionId, ServiceSessionStage.Service,
                    out DomainFailure stageFailure), "Service stage rejected: " + stageFailure.Code);
                float start = clock.Time;
                float deadline = Time.realtimeSinceStartup + 60f;
                float priorMaximumDeltaTime = Time.maximumDeltaTime;
                gameSpeed.SetPaused(false);
                gameSpeed.SetSpeed(5);
                timeScale.Scale = originalScale;
                try
                {
                    Time.maximumDeltaTime = 0.6f;
                    while (clock.Time - start < witness.serviceSeconds
                           && Time.realtimeSinceStartup < deadline)
                        yield return null;
                }
                finally { Time.maximumDeltaTime = priorMaximumDeltaTime; }
                witness.elapsedGameSeconds = clock.Time - start;
                Require(witness.elapsedGameSeconds >= witness.serviceSeconds,
                    "The actual service did not advance through its contracted game duration.");
                Require(sessions.TryCompleteSession(session.SessionId, out ServiceSessionSnapshot terminal,
                    out DomainFailure terminalFailure), "Service completion rejected: " + terminalFailure.Code);
                witness.completed = terminal.Stage == ServiceSessionStage.Completed;
                witness.providedServiceSeconds = terminal.ProvidedServiceSeconds;
                witness.terminalAcknowledged = terminal.TerminalPhase == ServiceSessionTerminalPhase.Acknowledged;
                witness.terminalResultKey = terminal.TerminalPublicationKey;
                witness.terminalOutcomeId = terminal.TerminalPublicationId;
                witness.duplicateRejected = !sessions.TryCompleteSession(session.SessionId, out _, out _);
                Require(Mathf.Approximately(witness.providedServiceSeconds, witness.serviceSeconds),
                    "The actual terminal lost the contracted service duration.");
                Require(witness.terminalResultKey == ServiceSessionOutcomeKey.ResultKeyText(session.SessionId),
                    "The actual service terminal has a different completion identity.");
            }
            else
                Require(sessions.CancelSession(session.SessionId, "qa:baseline-contract-observation"),
                    "Baseline service contract could not be cancelled.");
            GameplayOutcomeSnapshot[] exact = ReadAllOutcomes().Where(value =>
                value.producerId == ServiceSessionOutcomeKey.ProducerId
                && value.operationId == session.SessionId).ToArray();
            witness.exactOutcome = exact.Length == 1
                && exact[0].status == (execute ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.Cancelled)
                && exact[0].ownerRevision == ServiceSessionTerminalIdentity.OwnerRevision
                && Math.Abs(exact[0].metrics.Single(value => value.metricId
                    == ServiceSessionOutcomeIds.ContractedServiceSecondsMetric.Value).value
                    - witness.serviceSeconds) < 0.001d;
            Require(witness.exactOutcome && (!execute
                || (witness.completed && witness.terminalAcknowledged && witness.duplicateRejected)),
                "Actual service terminal identity/outcome or duplicate rejection failed.");
            completedWitness(witness);
        }
        finally
        {
            if (sessions.TryGetSessionSnapshot(session.SessionId, out ServiceSessionSnapshot active)
                && active.IsActive)
                sessions.CancelSession(session.SessionId, "qa:service-witness-failed");
            gameSpeed.SetSpeed(originalGameSpeed);
            gameSpeed.SetPaused(originalGamePaused);
            timeScale.Scale = originalScale;
        }
    }

    private Dictionary<string, GameplayOutcomeNarrativeEvidenceSource>
        CurrentFacilityEvidence(
            IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings)
    {
        GameplayOutcomeEvidenceBindingSnapshot[] required = (bindings
                ?? Array.Empty<GameplayOutcomeEvidenceBindingSnapshot>())
            .Where(value => value != null)
            .ToArray();
        var result = new Dictionary<string, GameplayOutcomeNarrativeEvidenceSource>(
            StringComparer.Ordinal);
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in required)
        {
            GameplayEntityId subject = new(
                new GameplayEntityKindId(binding.subjectKindId),
                binding.subjectId);
            GameplayOutcomeNarrativeEvidenceSource source = outcomeEvidence
                .GetForEntity(subject, 256, 0f, includeCompacted: false)
                .SingleOrDefault(value => string.Equals(
                    value.SourceId, binding.publicFactId, StringComparison.Ordinal));
            Require(!string.IsNullOrEmpty(source.SourceId),
                "Current facility evidence authority is missing selected fact: "
                    + binding.publicFactId);
            result.Add(binding.publicFactId, source);
        }
        Require(result.Count == required.Length && result.Count > 0,
            "Facility replay requires unique selected exact evidence.");
        return result;
    }

    private IEnumerator RunOriginalSkillPendingDiagnostics()
    {
        phase = "original-skill-pending-production-negative";
        gameSpeed.SetPaused(true);
        timeScale.Scale = 0;
        queue.ConfigureIsolatedAutomaticDispatch(false);
        skillAutomaticDispatchDeferred = true;
        string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string source = Path.Combine(project,
            "Artifacts/QA/AuditRemediation-20260926-r1/u3-source-v9-skill-staff-live-r63/unity-evidence/source-v9");
        byte[] manifest = File.ReadAllBytes(Path.Combine(source, "manifest.json"));
        byte[] pending = File.ReadAllBytes(Path.Combine(source, "selection/skill/pending.json"));
        byte[] originalPrompt = File.ReadAllBytes(Path.Combine(source, "selection/skill/original-prompt.txt"));
        byte[] rejected = File.ReadAllBytes(Path.Combine(project,
            "Artifacts/QA/AuditRemediation-20260926-r1/u6-original-skill-native-r407-r1/unity-evidence/original-skill-prose-native/resolved-content.json"));
        string Sha(byte[] bytes) => GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes);
        Require(Sha(manifest) == "sha256:c3480e9428d7e6a0a98ca62087465220967acd8be5756df83c8e5805e73b0d83"
                && Sha(pending) == "sha256:8703c6ba593ca5004971bd45e7733ecbec0fe58aea44723519833b88dd2d86cc"
                && Sha(originalPrompt) == "sha256:52a21903fba519f8759270caf539cf7ec3923360fdecde0ab64515864b1e2b56"
                && Sha(rejected) == "sha256:f05546b15b64daf1c5476b8a2f08beb8355fd89ffe807f84e48e8e64319a6b47",
            "Original request or preserved actual negative response bytes differ.");
        CharacterSkillDraft sidecar = Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterSkillDraft>(
            Encoding.UTF8.GetString(pending));
        DungeonGameSaveData originalWorld = saves.FromJson(File.ReadAllText(Path.Combine(source, "selection/skill/world.json")));
        DungeonCharacterWorldSaveData originalCharacters = DungeonSaveJson.Deserialize<DungeonCharacterWorldSaveData>(
            originalWorld.sections.Single(value => value.sectionId == "characters.world").payloadJson);
        CharacterSkillDraft expected = originalCharacters.actors.Single(value => value.persistentId == OriginalSkillPendingOwner)
            .growth.drafts.Single(value => value.requestKey == OriginalSkillPendingKey);
        CharacterSkillDraft OriginalDraft() => owner.Progression.Drafts.Single(value => value != null
            && value.requestKey == OriginalSkillPendingKey);
        string ComparableDraft(CharacterSkillDraft value, CharacterSkillDraft transportReference)
        {
            CharacterSkillDraft clone = value.Clone();
            // A recreated transport has no surviving in-flight network handle.
            // Compare every persisted domain value; report this one transport flag separately.
            clone.requestSubmitted = transportReference.requestSubmitted;
            return JsonUtility.ToJson(clone);
        }
        CharacterSkillDraft actual = OriginalDraft();
        bool sidecarMatchesOfficialSavedDraft = sidecar != null && expected != null
            && JsonUtility.ToJson(sidecar) == JsonUtility.ToJson(expected);
        bool freshMatchesOfficialSavedDraft = expected != null
            && ComparableDraft(actual, expected) == JsonUtility.ToJson(expected);
        WriteNew("original-skill-initial-pending-comparison.json", new JObject
        {
            ["sidecarParser"] = "Newtonsoft.Json typed single-precision fields",
            ["officialSavedDraftParser"] = "DungeonSaveJson official characters.world DTO",
            ["sidecarMatchesOfficialSavedDraft"] = sidecarMatchesOfficialSavedDraft,
            ["freshMatchesOfficialSavedDraft"] = freshMatchesOfficialSavedDraft,
            ["sourceSubmittedTransportFlag"] = expected?.requestSubmitted,
            ["freshSubmittedTransportFlag"] = actual.requestSubmitted,
            ["numericToleranceApplied"] = false,
            ["persistedDomainFieldsOmitted"] = false
        }.ToString());
        Require(expected != null && owner.Identity.PersistentId == OriginalSkillPendingOwner
                && actual.kind == CharacterSkillKind.Active && actual.unlockLevel == 5
                && !actual.isReady && !actual.permanentlyChosen && actual.chosenIndex == -1
                && actual.nextPresentationIndex == 0 && actual.candidates.Count == 0
                && actual.moduleSelectionOffers.Count == 3
                && sidecarMatchesOfficialSavedDraft && freshMatchesOfficialSavedDraft,
            "Official fresh restore changed the original Miru pending request.");
        var proof = new JObject
        {
            ["schema"] = "dungeonstory-original-skill-pending-production-negative@1",
            ["passed"] = false, ["ownerId"] = OriginalSkillPendingOwner,
            ["requestKey"] = OriginalSkillPendingKey, ["originalPendingSha256"] = Sha(pending),
            ["originalPromptSha256"] = Sha(originalPrompt), ["actualNegativeResponseSha256"] = Sha(rejected),
            ["currentNativeWireOrigin"] = "r412 actual native input; qualitative response remains FAIL",
            ["currentNativeWireSha256"] = OriginalSkillPendingNativeWireSha256,
            ["initialSubmittedTransportFlag"] = actual.requestSubmitted,
            ["sourceSubmittedTransportFlag"] = expected.requestSubmitted,
            ["newTriggerSynthesized"] = false, ["nativeModelReexecuted"] = false,
            ["semanticQualityPassed"] = false, ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false, ["steps"] = new JArray()
        };
        string response = Encoding.UTF8.GetString(rejected);
        string GrowthDomain()
        {
            CharacterGrowthState clone = owner.Progression.GrowthState.Clone();
            CharacterSkillDraft target = clone.drafts.Single(value => value.requestKey == OriginalSkillPendingKey);
            target.presentationFailureCount = 0;
            target.presentationCorrection = string.Empty;
            return JsonUtility.ToJson(clone);
        }
        for (int attempt = 0; attempt < 2; attempt++)
        {
            originalSkillPendingAttempt = attempt;
            originalSkillPendingCapture = null;
            CancelOutstandingTransport();
            actual = OriginalDraft();
            CharacterSkillDraft before = actual.Clone();
            int responses = 0;
            generation.RequestDraft(owner.Progression, actual);
            generation.Tick();
            string beforeGrowth = GrowthDomain();
            DungeonGameSaveData beforeWorld = saves.Capture();
            WriteNew("original-skill-attempt" + attempt + "-before-world.json", saves.ToJson(beforeWorld));
            queue.ConfigureIsolatedModuleReplayTestResponses(captured =>
            {
                if (!ReferenceEquals(captured, originalSkillPendingCapture)) return null;
                Require(responses == 0, "The same explicit negative response was delivered twice in one attempt.");
                responses++;
                return response;
            });
            try
            {
                Require(queue.TryDispatchQueuedRequestForIsolatedCapture(
                        "narrative-context:sha256:ac38389c89395d528cb9655de08eee57a9fc0dd38f741ce3d3035650b8b7ee8b"),
                    "The original pending did not dispatch through the actual production queue.");
                float until = Time.realtimeSinceStartup + 10f;
                while (responses != 1 && Time.realtimeSinceStartup < until) yield return null;
            }
            finally { queue.ClearIsolatedModuleReplayTestResponses(); }
            Require(originalSkillPendingCapture != null && responses == 1
                    && actual.presentationFailureCount == before.presentationFailureCount + 1
                    && actual.presentationCorrection.Contains("skill name", StringComparison.Ordinal)
                    && !actual.isReady && !actual.permanentlyChosen && actual.chosenIndex == -1
                    && actual.nextPresentationIndex == 0 && actual.candidates.Count == 0
                    && actual.frozenMechanics.Count == 0 && GrowthDomain() == beforeGrowth,
                "The real production callback did not reject the response atomically with only failure metadata.");
            DungeonGameSaveData failedWorld = saves.Capture();
            string[] unchangedSections = { "items.physical", "characters.life", "characters.narrative",
                "world.gameplay-outcome-ledger", "narrative.migrated-producer-outcomes" };
            foreach (string section in unchangedSections)
                Require(beforeWorld.sections.Single(value => value.sectionId == section).payloadJson
                        == failedWorld.sections.Single(value => value.sectionId == section).payloadJson,
                    "Rejected skill response changed authority section " + section);
            string failedJson = saves.ToJson(failedWorld);
            string failedHash = SkillWorldStateHash(failedJson);
            Require(!queue.TryDispatchQueuedRequestForIsolatedCapture(originalSkillPendingCapture.CorrelationId)
                    && SkillWorldStateHash(saves.ToJson(saves.Capture())) == failedHash,
                "Immediate duplicate dispatch changed the failed world.");
            string failedName = "original-skill-attempt" + attempt + "-failed-world.json";
            WriteNew(failedName, failedJson);
            CharacterSkillDraft failedDraft = actual.Clone();
            var step = new JObject
            {
                ["attempt"] = attempt, ["responseDeliveries"] = responses,
                ["failureCountBefore"] = before.presentationFailureCount,
                ["failureCountAfter"] = actual.presentationFailureCount,
                ["rejectReason"] = actual.presentationCorrection,
                ["unchangedAuthoritySections"] = new JArray(unchangedSections),
                ["immediateDuplicateDispatchRejected"] = true,
                ["failedWorldSha256"] = Sha(Encoding.UTF8.GetBytes(failedJson)),
                ["candidateCount"] = 0, ["appliedSkillCountDelta"] = 0
            };
            ResumeSourceV9World(Path.Combine(output, failedName), Hash(Encoding.UTF8.GetBytes(failedJson)),
                attempt == 0 ? "original-skill-pending-reject1" : "original-skill-pending-reject2",
                OriginalSkillPendingOwner);
            CharacterSkillDraft restored = OriginalDraft();
            Require(ComparableDraft(restored, failedDraft) == JsonUtility.ToJson(failedDraft),
                "Official fresh restore lost the exact failed pending or changed its evidence/offers.");
            step["freshOwnerAndFailedPendingRestored"] = true;
            step["restoredSubmittedTransportFlag"] = restored.requestSubmitted;
            ((JArray)proof["steps"]).Add(step);
        }
        proof["passed"] = true;
        report.originalSkillPendingDiagnosticsVerified = true;
        WriteNew("original-skill-pending-production-negative.json", proof.ToString());
    }

    private void CaptureOriginalSkillPendingDispatch(LocalLlmCapturedRequest captured)
    {
        Require(originalSkillPendingCapture == null && captured.CaptureOnly && !captured.NetworkSent
                && captured.ProfileId == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
                && captured.CorrelationId == "narrative-context:sha256:ac38389c89395d528cb9655de08eee57a9fc0dd38f741ce3d3035650b8b7ee8b"
                && generation.TryCapturePendingModuleDispatch(captured.CorrelationId,
                    out CharacterProgression progression, out CharacterSkillDraft draft,
                    out string prompt, out bool batch)
                && batch && ReferenceEquals(progression, owner.Progression)
                && progression.Actor.Identity.PersistentId == OriginalSkillPendingOwner
                && draft.requestKey == OriginalSkillPendingKey && prompt == captured.Prompt,
            "Original pending dispatch changed its exact owner, request or public context.");
        if (originalSkillPendingAttempt == 0)
            Require(GameplayModuleSelectionCanonicalJson.Sha256Prefixed(Encoding.UTF8.GetBytes(captured.Prompt))
                    == "sha256:52a21903fba519f8759270caf539cf7ec3923360fdecde0ab64515864b1e2b56"
                    && GameplayModuleSelectionCanonicalJson.Sha256Prefixed(captured.CopyWireBodyBytes())
                    == OriginalSkillPendingNativeWireSha256,
                "Fresh original owner did not reproduce the original prompt/r412 current native wire.");
        originalSkillPendingCapture = captured;
        WriteNew("original-skill-attempt" + originalSkillPendingAttempt + "-prompt.txt", captured.Prompt);
        WriteNew("original-skill-attempt" + originalSkillPendingAttempt + "-wire.json", captured.WireBodyJson);
    }

    private IEnumerator RunSkillReplay()
    {
        phase = "skill-source-v9-replay";
        replaying = true;
        gameSpeed.SetPaused(true);
        timeScale.Scale = 0f;
        CancelOutstandingTransport();
        Require(int.TryParse(Environment.GetEnvironmentVariable(
                    "DUNGEONSTORY_SOURCE_V9_SKILL_CANDIDATE_INDEX"),
                out int selectedIndex) && selectedIndex is >= 0 and <= 2,
            "Skill replay requires an explicit simulated-player candidate index.");
        queue.ConfigureIsolatedAutomaticDispatch(false);
        skillAutomaticDispatchDeferred = true;
        report.requestScheduling += "; unrelated AI transport remains queued during this isolated mechanic witness, with no fabricated response/cancellation-retry loop";
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        skillReplayInput = GameplayModuleSelectionSourceV9ReplayInput.LoadSkill(
            projectRoot,
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE"),
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE"),
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD"));
        Require(string.Equals(owner.Identity.PersistentId,
                skillReplayInput.OwnerId, StringComparison.Ordinal),
            "Restored official owner differs from the source-v9 skill owner.");
        CharacterSkillDraft beforeDraft = owner.Progression.Drafts
            .Single(value => value != null
                && string.Equals(value.requestKey,
                    skillReplayInput.RequestKey, StringComparison.Ordinal));
        Require(!beforeDraft.isReady && !beforeDraft.permanentlyChosen
                && beforeDraft.chosenIndex == -1
                && beforeDraft.nextPresentationIndex == 0
                && (beforeDraft.candidates?.Count ?? 0) == 0
                && (beforeDraft.moduleSelectionOffers?.Count ?? 0) == 3,
            "Restored skill draft is not candidate-store ready.");
        CharacterSkillDraft beforeSnapshot = beforeDraft.Clone();
        string beforeWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-before-world.json", beforeWorld);
        WriteNew("u4-before-skill-draft.json",
            JsonUtility.ToJson(beforeSnapshot, true));

        queue.ConfigureIsolatedModuleReplayTestResponses(captured =>
        {
            if (skillReplayCapture == null
                || !ReferenceEquals(captured, skillReplayCapture))
                return null;
            Require(skillReplayResponseCount == 0,
                "The explicit skill response was requested more than once.");
            skillReplayResponseCount++;
            return skillReplayInput.AcceptedResponseJson;
        });
        generation.RequestDraft(owner.Progression, beforeDraft);
        generation.Tick();
        Require(queue.TryDispatchQueuedRequestForIsolatedCapture(
                skillReplayInput.RequestId),
            "The restored skill request was not uniquely queued for replay: "
                + queue.GetRequestDiagnosticsForDebug());
        float deadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < deadline
            && (!beforeDraft.isReady || skillReplayResponseCount != 1))
            yield return null;
        queue.ClearIsolatedModuleReplayTestResponses();
        Require(skillReplayCapture != null && skillReplayResponseCount == 1
                && beforeDraft.isReady
                && beforeDraft.presentationState
                    == CharacterSkillPresentationState.Ready
                && !beforeDraft.requestSubmitted
                && !beforeDraft.permanentlyChosen
                && beforeDraft.chosenIndex == -1
                && beforeDraft.nextPresentationIndex == 3
                && beforeDraft.candidates.Count == 3
                && beforeDraft.frozenMechanics.Count == 3
                && generation.PendingRequestCount == 0,
            "The fixed skill response did not store exactly three unchosen candidates through the production callback.");
        for (int index = 0; index < skillReplayInput.Response.candidates.Count;
             index++)
        {
            CharacterSkillModuleSelectionResponseDto expected =
                skillReplayInput.Response.candidates[index];
            CharacterSkillInstance actual = beforeDraft.candidates[index];
            string[] actualModules = actual.modules
                .Select(value => value.moduleId).ToArray();
            string[] expectedModules = expected.positiveModuleIds
                .Concat(expected.drawbackModuleIds).ToArray();
            Require(actual.presentationId.StartsWith(
                        "presentation:skill:", StringComparison.Ordinal)
                    && string.Equals(actual.displayName,
                        expected.displayName, StringComparison.Ordinal)
                    && string.Equals(actual.narrativeFlavor,
                        expected.narrativeFlavor, StringComparison.Ordinal)
                    && actualModules.SequenceEqual(expectedModules,
                        StringComparer.Ordinal)
                    && actual.evidenceIds.SequenceEqual(
                        expected.evidenceFactIds, StringComparer.Ordinal),
                "Stored skill candidate differs from the explicit response at index "
                    + index + ".");
        }
        Require(beforeSnapshot.outcomeEvidenceBindings.Count
                == beforeDraft.outcomeEvidenceBindings.Count
                && beforeSnapshot.outcomeEvidenceBindings.Zip(
                    beforeDraft.outcomeEvidenceBindings,
                    (before, after) => JsonUtility.ToJson(before)
                        == JsonUtility.ToJson(after)).All(value => value),
            "Candidate storage consumed or rewrote request evidence.");
        string appliedDraftJson = JsonUtility.ToJson(beforeDraft);
        string appliedWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-applied-world.json", appliedWorld);
        WriteNew("u4-applied-skill-draft.json",
            JsonUtility.ToJson(beforeDraft, true));

        CharacterActor previousOwner = owner;
        Require(saves.TryRestore(saves.FromJson(appliedWorld),
                out DungeonGameRestoreReport restored),
            "Official skill replay result restore failed: "
                + string.Join(" | ", restored.Errors));
        Require(scope.Container.Resolve<ICharacterWorldSaveService>()
                .TryGetRestoredActor(skillReplayInput.OwnerId, out owner)
                && !ReferenceEquals(previousOwner, owner),
            "Official skill replay restore did not publish a fresh owner.");
        CharacterSkillDraft restoredDraft = owner.Progression.Drafts
            .Single(value => value != null
                && string.Equals(value.requestKey,
                    skillReplayInput.RequestKey, StringComparison.Ordinal));
        Require(string.Equals(appliedDraftJson,
                JsonUtility.ToJson(restoredDraft), StringComparison.Ordinal)
                && restoredDraft.isReady
                && !restoredDraft.permanentlyChosen
                && restoredDraft.chosenIndex == -1,
            "Stored skill candidates changed or became selected across official save/restore.");
        string restoredWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-restored-world.json", restoredWorld);
        WriteNew("u4-restored-skill-draft.json",
            JsonUtility.ToJson(restoredDraft, true));
        CharacterSkillDraft candidateRestoredSnapshot = restoredDraft.Clone();
        CommitAndRestoreSkillSelection(restoredDraft, selectedIndex);
        yield return RunSkillMechanicWitness();
        WriteSkillReplayProof(
            beforeSnapshot,
            beforeDraft,
            candidateRestoredSnapshot,
            beforeWorld,
            appliedWorld,
            restoredWorld);
        report.responseApplied = true;
        report.networkSent = false;
    }

    private Dictionary<string, GameplayOutcomeNarrativeEvidenceSource>
        CurrentSkillEvidence(IEnumerable<string> factIds, string characterId = null)
    {
        HashSet<string> required = factIds.ToHashSet(StringComparer.Ordinal);
        var current = outcomeEvidence.GetForCharacter(characterId ?? skillReplayInput.OwnerId,
                256, 0f, includeCompacted: true)
            .Where(value => required.Contains(value.SourceId))
            .ToDictionary(value => value.SourceId, StringComparer.Ordinal);
        Require(required.Count > 0 && current.Count == required.Count,
            "Selected skill requires current exact gameplay evidence.");
        return current;
    }

    private static string SkillEvidenceAnchor(
        GameplayOutcomeNarrativeEvidenceSource source) =>
        GameplayModuleSelectionCanonicalJson.Hash(new JObject
        {
            ["sourceId"] = source.SourceId,
            ["exactOutcomeId"] = source.ExactOutcomeId.ToString(),
            ["subjectId"] = source.SubjectId.Value,
            ["anchorRevision"] = source.AnchorRevision,
            ["influenceUseCount"] = source.InfluenceUseCount,
            ["influenceRevision"] = source.InfluenceRevision
        });

    private static string SkillWorldStateHash(string world)
    {
        JObject captured = JObject.Parse(world);
        // Capture stamps wall time even while paused. Preserve every game section,
        // version and integrity seal; exclude only this non-domain capture label.
        captured.Remove(nameof(DungeonGameSaveData.savedAtUtc));
        return GameplayModuleSelectionCanonicalJson.Hash(captured);
    }

    private void CommitAndRestoreSkillSelection(
        CharacterSkillDraft draft, int selectedIndex)
    {
        phase = "skill-public-selection-and-restore";
        CharacterSkillInstance selected = draft.candidates[selectedIndex].Clone();
        skillSelectionModules = selected.modules.Select(value => value.moduleId).ToArray();
        skillSelectionFacts = selected.evidenceIds.ToArray();
        skillSelectionFormulaId = skillReplayInput.FormulaIds[selectedIndex];
        Require(selected.kind == CharacterSkillKind.Active
                && selected.evidenceBindings.Count == skillSelectionFacts.Length
                && selected.evidenceBindings.All(value => value.sourceKind == "exact")
                && selected.evidenceBindings.Select(value => value.publicFactId)
                    .SequenceEqual(skillSelectionFacts, StringComparer.Ordinal),
            "This representative selection requires offered exact evidence only.");
        var beforeEvidence = CurrentSkillEvidence(skillSelectionFacts);
        List<GameplayOutcomeSnapshot> AcquisitionRows() => ReadOwnerOutcomes()
            .Where(value => value.outcomeTypeId
                == CharacterSkillAcquiredOutcomeIds.Acquired.Value).ToList();
        report.skillSelection = new SkillSelectionWitness
        {
            actorId = owner.Identity.PersistentId,
            requestKey = draft.requestKey,
            candidateIndex = selectedIndex,
            unlockLevel = draft.unlockLevel,
            skillId = selected.id,
            activeSkillsBefore = owner.Progression.ActiveSkills.Count,
            acquisitionRevisionBefore = owner.Progression.GrowthState.skillAcquisitionRevision,
            acquisitionOutcomesBefore = AcquisitionRows().Count
        };
        SkillSelectionWitness witness = report.skillSelection;
        string beforeWorld = saves.ToJson(saves.Capture());
        string beforeStateHash = SkillWorldStateHash(beforeWorld);
        string beforeGrowth = JsonUtility.ToJson(owner.Progression.GrowthState);
        WriteNew("u4-selection-before-world.json", beforeWorld);
        skillSelectionBeforeArtifact = GameplayModuleSelectionReplayProofV2Contract.Artifact(
            "u4-selection-before-world.json", Encoding.UTF8.GetBytes(beforeWorld),
            JObject.Parse(beforeGrowth));
        bool unconfirmedRejected = !owner.Progression.TryChooseActiveSkill(
            draft.unlockLevel, selectedIndex, false, out _);
        string unconfirmedWorld = saves.ToJson(saves.Capture());
        witness.unconfirmedAtomic = unconfirmedRejected
            && SkillWorldStateHash(unconfirmedWorld) == beforeStateHash;
        bool invalidIndexRejected = !owner.Progression.TryChooseActiveSkill(
            draft.unlockLevel, draft.candidates.Count, true, out _);
        string invalidWorld = saves.ToJson(saves.Capture());
        witness.invalidIndexAtomic = invalidIndexRejected
            && SkillWorldStateHash(invalidWorld) == beforeStateHash;
        if (!witness.unconfirmedAtomic)
            WriteNew("u4-selection-unconfirmed-failed-world.json", unconfirmedWorld);
        if (!witness.invalidIndexAtomic)
            WriteNew("u4-selection-invalid-failed-world.json", invalidWorld);
        Require(witness.unconfirmedAtomic && witness.invalidIndexAtomic,
            "Rejected skill selections changed the official world.");
        Require(owner.Progression.TryChooseActiveSkill(draft.unlockLevel,
                selectedIndex, true, out string commitMessage),
            "Public skill selection failed: " + commitMessage);
        witness.activeSkillsAfter = owner.Progression.ActiveSkills.Count;
        witness.acquisitionRevisionAfter = owner.Progression.GrowthState.skillAcquisitionRevision;
        witness.acquisitionOutcomes = AcquisitionRows();
        witness.acquisitionOutcomesAfter = witness.acquisitionOutcomes.Count;
        var afterEvidence = CurrentSkillEvidence(skillSelectionFacts);
        witness.committed = draft.permanentlyChosen && draft.chosenIndex == selectedIndex
            && JsonUtility.ToJson(draft.ChosenSkill) == JsonUtility.ToJson(selected)
            && owner.Progression.ActiveSkills.Count(value => value.id == selected.id) == 1
            && JsonUtility.ToJson(owner.Progression.ActiveSkills.Single(value => value.id == selected.id))
                == JsonUtility.ToJson(selected)
            && witness.activeSkillsAfter == witness.activeSkillsBefore + 1
            && witness.acquisitionRevisionAfter == witness.acquisitionRevisionBefore + 1
            && witness.acquisitionOutcomesAfter == witness.acquisitionOutcomesBefore + 1
            && beforeEvidence.All(pair =>
                afterEvidence[pair.Key].ExactOutcomeId.Equals(pair.Value.ExactOutcomeId)
                && afterEvidence[pair.Key].SubjectId.Equals(pair.Value.SubjectId)
                && afterEvidence[pair.Key].InfluenceUseCount == pair.Value.InfluenceUseCount + 1
                && afterEvidence[pair.Key].InfluenceRevision == pair.Value.InfluenceRevision + 1);
        Require(witness.committed,
            "Skill selection did not commit exactly one skill, acquisition outcome and evidence use.");
        skillSelectionInfluenceDeltas = beforeEvidence.OrderBy(value => value.Key,
                StringComparer.Ordinal)
            .Select(pair => GameplayModuleSelectionReplayProofV2Contract.InfluenceDelta(
                "exact", pair.Key, pair.Value.InfluenceUseCount,
                afterEvidence[pair.Key].InfluenceUseCount, pair.Value.InfluenceRevision,
                afterEvidence[pair.Key].InfluenceRevision, SkillEvidenceAnchor(pair.Value),
                SkillEvidenceAnchor(afterEvidence[pair.Key]))).ToArray();
        string appliedWorld = saves.ToJson(saves.Capture());
        string appliedStateHash = SkillWorldStateHash(appliedWorld);
        string appliedGrowth = JsonUtility.ToJson(owner.Progression.GrowthState);
        WriteNew("u4-selection-applied-world.json", appliedWorld);
        WriteNew("u4-selection-applied-growth.json", appliedGrowth);
        skillSelectionAppliedArtifact = GameplayModuleSelectionReplayProofV2Contract.Artifact(
            "u4-selection-applied-world.json", Encoding.UTF8.GetBytes(appliedWorld),
            JObject.Parse(appliedGrowth));
        witness.duplicateStable = owner.Progression.TryChooseActiveSkill(
                draft.unlockLevel, selectedIndex, true, out _)
            && SkillWorldStateHash(saves.ToJson(saves.Capture())) == appliedStateHash;
        witness.differentChoiceRejected = !owner.Progression.TryChooseActiveSkill(
                draft.unlockLevel, (selectedIndex + 1) % draft.candidates.Count, true, out _)
            && SkillWorldStateHash(saves.ToJson(saves.Capture())) == appliedStateHash;
        Require(witness.duplicateStable && witness.differentChoiceRejected,
            "Repeated or different skill choices changed a committed world.");
        CharacterActor previous = owner;
        var envelope = saves.FromJson(appliedWorld);
        witness.restoredSections = envelope.sections.Count;
        Require(saves.TryRestore(envelope, out DungeonGameRestoreReport restore),
            "Official chosen skill restore failed: " + string.Join(" | ", restore.Errors));
        witness.officialFreshRestore = scope.Container.Resolve<ICharacterWorldSaveService>()
            .TryGetRestoredActor(skillReplayInput.OwnerId, out owner)
            && !ReferenceEquals(previous, owner);
        Require(witness.officialFreshRestore,
            "Chosen skill restore did not publish a fresh request actor.");
        witness.officialStateExact = appliedGrowth == JsonUtility.ToJson(owner.Progression.GrowthState);
        var restoredEvidence = CurrentSkillEvidence(skillSelectionFacts);
        witness.officialEvidenceExact = afterEvidence.All(pair =>
            SkillEvidenceAnchor(pair.Value) == SkillEvidenceAnchor(restoredEvidence[pair.Key]));
        Require(witness.officialStateExact && witness.officialEvidenceExact,
            "Official restore changed the chosen skill, acquisition revision or exact evidence use.");
        string restoredWorld = saves.ToJson(saves.Capture());
        string restoredStateHash = SkillWorldStateHash(restoredWorld);
        WriteNew("u4-selection-restored-world.json", restoredWorld);
        WriteNew("u4-selection-restored-growth.json", JsonUtility.ToJson(owner.Progression.GrowthState));
        skillSelectionRestoredArtifact = GameplayModuleSelectionReplayProofV2Contract.Artifact(
            "u4-selection-restored-world.json", Encoding.UTF8.GetBytes(restoredWorld),
            JObject.Parse(JsonUtility.ToJson(owner.Progression.GrowthState)));
        witness.restoredDuplicateStable = owner.Progression.TryChooseActiveSkill(
                witness.unlockLevel, selectedIndex, true, out _)
            && !owner.Progression.TryChooseActiveSkill(witness.unlockLevel,
                (selectedIndex + 1) % draft.candidates.Count, true, out _)
            && SkillWorldStateHash(saves.ToJson(saves.Capture())) == restoredStateHash;
        Require(witness.restoredDuplicateStable,
            "Repeated choices after official restore changed skill state or evidence.");
        report.skillSelectionCommitted = true;

        // Observe the real admission prerequisite; never inject completed research.
        BlueprintResearchRuntime research = UnityEngine.Object
            .FindFirstObjectByType<BlueprintResearchRuntime>();
        Require(research != null, "The authored world has no research authority.");
        witness.requiredResearchId = OffenseExpeditionAccessRules.RequiredResearchId;
        witness.expeditionUnlocked = OffenseExpeditionAccessRules.IsUnlocked(research.State);
        if (!witness.expeditionUnlocked)
        {
            string beforeQueueWorld = saves.ToJson(saves.Capture());
            ResearchQueueCommandResult queued = research.EnqueueProject(
                new ResearchProjectId(witness.requiredResearchId));
            witness.researchQueued = queued.Succeeded;
            witness.researchQueueMessage = queued.Message;
            witness.queuedResearchProjects = queued.AffectedProjects
                .Select(value => value.Value).ToList();
            witness.researchRejectionAtomic = queued.Succeeded
                || SkillWorldStateHash(beforeQueueWorld)
                    == SkillWorldStateHash(saves.ToJson(saves.Capture()));
            Require(witness.researchRejectionAtomic,
                "Rejected research prerequisites partially changed the world.");
        }
        WriteNew("u4-skill-prerequisite-world.json", saves.ToJson(saves.Capture()));
        WriteNew("u4-skill-selection-witness.json", JsonUtility.ToJson(witness, true));
    }

    private void VerifyConstructionEngagementSaveContract(DungeonGameSaveData checkpoint)
    {
        DungeonInvasionSaveData payload = DungeonSaveJson.Deserialize<DungeonInvasionSaveData>(
            checkpoint.sections.Single(value => value.sectionId == "invasion.state").payloadJson);
        var patterns = scope.Container.Resolve<IInvasionIntruderPatternDefinitionCatalog>();
        var baseline = new DungeonGameRestoreReport();
        InvasionSaveValidation.Validate(payload, patterns, baseline);
        Require(baseline.Success, "Actual saved invasion failed its corrected contract: " + string.Join(" | ", baseline.Errors));
        Require(payload.engagements.engagements.Count == 1,
            "Focused saved engagement contract requires exactly one active engagement.");
        DefenseEngagementSaveData engagement = payload.engagements.engagements[0];
        Require(engagement.hasReserveCell && string.IsNullOrEmpty(engagement.reserveGuardId),
            "Focused checkpoint does not witness a real vacant planned reserve position.");
        var evidence = new JObject { ["actualSavedPayloadAccepted"] = true, ["vacantPlannedReserveAccepted"] = true };
        void Reject(string name, Action<DungeonInvasionSaveData> corrupt, string expectedReason)
        {
            var copy = DungeonSaveJson.Deserialize<DungeonInvasionSaveData>(JsonUtility.ToJson(payload));
            corrupt(copy);
            var report = new DungeonGameRestoreReport();
            InvasionSaveValidation.Validate(copy, patterns, report);
            Require(!report.Success && report.Errors.Any(value => value.Contains(expectedReason)),
                "Malformed engagement contract was not rejected at its expected boundary: " + name);
            evidence[name] = new JObject { ["rejected"] = true, ["errors"] = JArray.FromObject(report.Errors) };
        }
        Reject("runtimeIdIsNotActorId", copy => copy.engagements.engagements[0].intruderId = copy.activeIntruders[0].runtimeId,
            "invalid participants or state");
        Reject("assignedReserveRequiresCell", copy =>
        {
            copy.engagements.engagements[0].reserveGuardId = "character:qa-reserve-contract";
            copy.engagements.engagements[0].hasReserveCell = false;
        }, "inconsistent reserve state");
        Reject("plannedReserveCannotOverlapLead", copy =>
        {
            copy.engagements.engagements[0].reserveX = engagement.guardX;
            copy.engagements.engagements[0].reserveY = engagement.guardY;
        }, "inconsistent reserve state");
        Reject("duplicateGuardRejected", copy => copy.engagements.engagements[0].reserveGuardId = engagement.leadGuardId,
            "invalid or duplicate guard");
        evidence["trainingEligible"] = false;
        evidence["humanApprovalClaimed"] = false;
        WriteNew("construction-engagement-save-contract.json", evidence.ToString());
    }

    private IEnumerator RunConstructionHaulDiagnostics()
    {
        phase = "construction-haul-diagnostics";
        report.scope = "Focused restored construction material Haul diagnosis; not U4 closure or training data";
        gameSpeed.SetPaused(true);
        var capacityEvidence = new JArray();
        foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>().Characters
                     .Where(value => value != null).OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal))
            capacityEvidence.Add(CharacterCapacityBatchProjectionVerifier.Verify(actor,
                scope.Container.Resolve<IAnatomyHealthRuntime>(), scope.Container.Resolve<IAnatomyProfileCatalog>(),
                derivedStats, scope.Container.Resolve<ICharacterPerformanceQuery>()));
        WriteNew("capacity-batch-projection-parity.json", capacityEvidence.ToString());
        queue.ConfigureIsolatedAutomaticDispatch(false);
        skillAutomaticDispatchDeferred = true;
        var workOrders = scope.Container.Resolve<IWorkOrderRuntime>();
        var buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        ConstructionSite[] sites = buildings.Buildings.OfType<ConstructionSite>()
            .Where(value => value != null && !value.isDestroy).ToArray();
        Require(sites.Length == 1, "Diagnostic checkpoint must contain exactly one actual construction site.");
        ConstructionSite site = sites[0];
        WorkOrderSaveData before = workOrders.Capture().orders.Single(value => value.workOrderId == site.WorkOrderId);
        Require(before.workTypeId == BuiltInWorkTypeIds.Construct.Value && before.completedWork == 0f
                && before.itemMaterials.Any(value => value.delivered < value.required)
                && before.constructionSitePersistentId == site.PersistentInstanceId.Value,
            "Focused diagnostic requires the exact unfinished, material-waiting saved site.");
        var witness = new ConstructionHaulDiagnostics
        {
            orderId = before.workOrderId, siteId = site.PersistentInstanceId.Value,
            destinationId = before.materialDestinationId, before = before,
            sourceOrderExact = report.continuation.workOrdersExact
        };
        report.constructionHaulDiagnostics = witness;
        report.facilityConstruction = new ConstructionWitness
        {
            orderId = witness.orderId, siteId = witness.siteId, before = before,
            actorId = owner.Identity.PersistentId, buildingDefinitionId = before.targetBuildingId
        };
        CharacterActor assignedHauler = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .SingleOrDefault(value => value != null && value.Identity.PersistentId == "character:staff:1:01");
        Require(assignedHauler != null && assignedHauler.CanRunAi
                && CharacterWorkRoleUtility.TryGetWork(assignedHauler, out AbilityWork assignedWork)
                && !assignedWork.isWorking && assignedHauler.Brain != null,
            "Public Haul assignment requires the existing available staff actor, not a fabricated worker.");
        AbilityWork haulWork = assignedHauler.GetAbility<AbilityWork>();
        WorkPriorityLevel originalPriority = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul);
        Require(originalPriority == WorkPriorityLevel.Off,
            "Focused public assignment requires the actual saved Haul-Off condition.");
        CharacterLog activityLog = assignedHauler.LogComponent;
        Require(activityLog != null, "Public Haul assignment requires the actual character activity log.");
        CharacterActivityEvent previousActivity = activityLog.ActivityEntries.LastOrDefault();
        witness.assignedActorId = assignedHauler.Identity.PersistentId;
        witness.originalHaulPriority = originalPriority.ToString();
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        using var measuredProcess = System.Diagnostics.Process.GetCurrentProcess();
        double cpuStarted = measuredProcess.TotalProcessorTime.TotalSeconds;
        float started = Time.realtimeSinceStartup, startedGame = clock.Time;
        float nextObservation = started, nextNeutral = startedGame;
        int startedFrame = Time.frameCount, observation = 0, profilerObservation = 0;
        bool originalProfiler = ProfilerDriver.enabled;
        bool originalEditorProfile = ProfilerDriver.profileEditor;
        bool profilerWindowOpen = true;
        ProfilerDriver.profileEditor = false;
        ProfilerDriver.enabled = true;
        try
        {
            try
            {
                haulWork.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
            }
            finally
            {
                // Activity observers run after the owner changes; their failure must not skip cleanup.
                witness.publicAssignmentIssued = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul)
                    == WorkPriorityLevel.Priority1;
            }
            CharacterActivityEvent changed = activityLog.ActivityEntries.LastOrDefault();
            witness.assignmentActivityExact = changed != null && !ReferenceEquals(changed, previousActivity)
                && changed.ActorId == witness.assignedActorId && changed.ActionId == BuiltInWorkTypeIds.Haul.Value
                && changed.OutcomeId == CharacterActivityOutcomes.Changed
                && changed.ReasonCode == $"priority:{(int)WorkPriorityLevel.Priority1}"
                && changed.Value.Equals((float)(int)WorkPriorityLevel.Priority1);
            Require(witness.assignmentActivityExact, "Public Haul priority command did not produce its exact activity.");
            WriteNew("construction-haul-public-assignment.json", new JObject
            {
                ["origin"] = "simulated-player-public-work-priority-command",
                ["actorId"] = witness.assignedActorId, ["before"] = witness.originalHaulPriority,
                ["after"] = WorkPriorityLevel.Priority1.ToString(), ["activity"] = JObject.FromObject(changed),
                ["forcedAiChoice"] = false, ["naturalInvasionSuppressed"] = false,
                ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
            }.ToString());
            gameSpeed.SetSpeed(5);
            Time.maximumDeltaTime = 0.1f;
            gameSpeed.SetPaused(false);
            while (Time.realtimeSinceStartup - started < 120f)
            {
                if (clock.Time >= nextNeutral)
                {
                    StabilizeTraitWorkFixture();
                    nextNeutral = clock.Time + 5f;
                }
                witness.realSeconds = Time.realtimeSinceStartup - started;
                witness.processCpuSeconds = measuredProcess.TotalProcessorTime.TotalSeconds - cpuStarted;
                witness.gameSeconds = clock.Time - startedGame;
                witness.frames = Time.frameCount - startedFrame;
                if (profilerWindowOpen && (witness.frames >= 64 || witness.realSeconds >= 20f))
                {
                    CaptureConstructionProfiler(++profilerObservation);
                    ProfilerDriver.enabled = false;
                    profilerWindowOpen = false;
                    WriteNew("construction-profiler-window-ended.json", new JObject
                    {
                        ["frames"] = witness.frames, ["realSeconds"] = witness.realSeconds,
                        ["maximumFrames"] = 64, ["maximumRealSeconds"] = 20,
                        ["originalEnabled"] = originalProfiler, ["originalProfileEditor"] = originalEditorProfile,
                        ["reason"] = "Bounded diagnostic capture; gameplay material wait continues unchanged",
                        ["trainingEligible"] = false
                    }.ToString());
                }
                witness.after = workOrders.Capture().orders.Single(value => value.workOrderId == witness.orderId);
                Require(witness.after.constructionSitePersistentId == witness.siteId
                        && witness.after.materialDestinationId == witness.destinationId
                        && witness.after.targetBuildingId == before.targetBuildingId
                        && witness.after.requiredWork == before.requiredWork,
                    "Diagnostic changed the saved construction identity.");
                witness.materialsReady = workOrders.RefreshMaterialsReady(site);
                if (Time.realtimeSinceStartup >= nextObservation || witness.materialsReady)
                {
                    CaptureHaulingWait(++observation, started, "construction-haul-diagnostics");
                    if (profilerWindowOpen) CaptureConstructionProfiler(++profilerObservation);
                    nextObservation = Time.realtimeSinceStartup + 20f;
                }
                if (witness.materialsReady)
                {
                    witness.completedBeforeRetry = !witness.publicRetryIssued;
                    break;
                }
                yield return null;
            }
        }
        finally
        {
            ProfilerDriver.enabled = originalProfiler;
            ProfilerDriver.profileEditor = originalEditorProfile;
            gameSpeed.SetPaused(true);
            if (witness.publicAssignmentIssued)
            {
                if (assignedHauler.Brain.HasRunningAction && assignedHauler.Brain.bestAction?.actionset is AIHaul)
                    Require(assignedHauler.Brain.StopCurrentActionForReplan("End focused public Haul assignment"),
                        "Could not release the actual Haul operation before priority restoration.");
                haulWork.SetWorkPriority(BuiltInWorkTypeIds.Haul, originalPriority);
                witness.priorityRestored = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul) == originalPriority;
                Require(witness.priorityRestored, "Public Haul priority restoration failed.");
                WriteNew("construction-haul-public-assignment-restored.json", JsonUtility.ToJson(witness, true));
            }
        }
        witness.after = workOrders.Capture().orders.Single(value => value.workOrderId == witness.orderId);
        witness.processCpuSeconds = measuredProcess.TotalProcessorTime.TotalSeconds - cpuStarted;
        WriteNew("construction-haul-diagnostics.json", JsonUtility.ToJson(witness, true));
        WriteNew("construction-haul-observed-world.json", saves.ToJson(saves.Capture()));
        Require(witness.materialsReady, "Focused actual construction material Haul remains incomplete; inspect scheduler/path/profiler evidence.");
    }

    private void CaptureConstructionProfiler(int observation)
    {
        int frameIndex = ProfilerDriver.lastFrameIndex;
        if (frameIndex < 0)
        {
            WriteNew($"construction-haul-profiler-{observation:D3}.json", new JObject
            {
                ["available"] = false, ["reason"] = "No completed profiler frame yet", ["trainingEligible"] = false
            }.ToString());
            return;
        }
        using RawFrameDataView view = ProfilerDriver.GetRawFrameDataView(frameIndex, 0);
        var samples = new List<SlowFrameSample>();
        var exclusiveSamples = new List<SlowFrameSample>();
        var selectedMarkers = new Dictionary<string, (int Count, float Milliseconds)>(StringComparer.Ordinal)
        {
            ["DefenseEngagementRuntime.SupportFill"] = default,
            ["DefenseEngagementRuntime.CombatExchange"] = default,
            ["CharacterPerformance.FunctionalCapacities"] = default
        };
        if (view.valid)
        {
            for (int index = 0; index < view.sampleCount; index++)
            {
                float milliseconds = view.GetSampleTimeMs(index);
                string name = view.GetSampleName(index);
                if (selectedMarkers.TryGetValue(name, out var observed))
                    selectedMarkers[name] = (observed.Count + 1, observed.Milliseconds + milliseconds);
                if (milliseconds >= 1f)
                    samples.Add(new SlowFrameSample { name = name, milliseconds = milliseconds });
            }
            int cursor = 0;
            while (cursor < view.sampleCount)
                CollectExclusiveProfilerCosts(view, ref cursor, exclusiveSamples);
        }
        var evidence = new JObject
        {
            ["available"] = view.valid,
            ["profilerFrameIndex"] = frameIndex,
            ["frameMilliseconds"] = view.valid ? view.frameTimeMs : 0f,
            ["samples"] = JArray.FromObject(samples.OrderByDescending(value => value.milliseconds).Take(16)),
            ["selectedMarkers"] = JArray.FromObject(selectedMarkers.Select(value => new
                { name = value.Key, count = value.Value.Count, milliseconds = value.Value.Milliseconds })),
            ["exclusiveSamples"] = JArray.FromObject(exclusiveSamples.GroupBy(value => value.name)
                .Select(group => new SlowFrameSample { name = group.Key, milliseconds = group.Sum(value => value.milliseconds) })
                .OrderByDescending(value => value.milliseconds).Take(16)),
            ["trainingEligible"] = false
        };
        WriteNew($"construction-haul-profiler-{observation:D3}.json", evidence.ToString());
    }

    private static void CollectExclusiveProfilerCosts(RawFrameDataView view, ref int cursor, List<SlowFrameSample> samples)
    {
        int current = cursor++;
        float total = view.GetSampleTimeMs(current), children = 0f;
        int childCount = view.GetSampleChildrenCount(current);
        for (int child = 0; child < childCount && cursor < view.sampleCount; child++)
        {
            children += view.GetSampleTimeMs(cursor);
            CollectExclusiveProfilerCosts(view, ref cursor, samples);
        }
        float exclusive = Mathf.Max(0f, total - children);
        if (exclusive >= 1f)
            samples.Add(new SlowFrameSample { name = view.GetSampleName(current), milliseconds = exclusive });
    }

    private IEnumerator RunSkillBlueprintHaulDiagnostics()
    {
        phase = "skill-haul-focused-continuation";
        report.scope = "Focused actual Haul continuation from the preserved failed world; no request replay, research, combat or U4 proof claim";
        report.executionOrigin = "official hashed partial-world restore -> ordinary Brain/AIHaul/AbilityHaul/AbilityMove";
        CancelOutstandingTransport();
        queue.ConfigureIsolatedAutomaticDispatch(false);
        skillAutomaticDispatchDeferred = true;
        BlueprintResearchRuntime research = UnityEngine.Object.FindFirstObjectByType<BlueprintResearchRuntime>();
        Require(research != null, "Restored world has no physical blueprint authority.");
        FacilityBlueprintSO blueprint = research.ProjectCatalog.Projects.Where(value => value?.Blueprint != null)
            .Select(value => value.Blueprint).FirstOrDefault(value => value.id == 6103);
        Require(blueprint != null, "Authored support blueprint is missing.");
        IResearchBlueprintArchiveQuery archives = scope.Container.Resolve<IResearchBlueprintArchiveQuery>();
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        WorldItemStackSnapshot[] physical = items.GetAllStacks().Where(value => value != null
            && value.ItemId == blueprint.PhysicalItemId && value.Quantity > 0).ToArray();
        Require(physical.Length == 1 && physical[0].Quantity == 1 && !archives.GetStatus(blueprint).IsArchived,
            "Diagnostic checkpoint must contain exactly one actually purchased unarchived blueprint.");
        bool observeExistingCarrier = physical[0].State == WorldItemStackState.Carried;
        CharacterActor observedActor = observeExistingCarrier
            ? scope.Container.Resolve<ICharacterWorldQuery>().Characters.Single(value => value != null
                && value.Identity.PersistentId == physical[0].DestinationId)
            : owner;
        Require(CharacterWorkRoleUtility.TryGetWork(observedActor, out AbilityWork worker),
            "Actual blueprint carrier has no work authority.");
        Require(observedActor.TryGetAbility(out AbilityMove mover),
            "Actual blueprint carrier has no movement authority.");
        AbilityHaul haul = observedActor.GetComponent<AbilityHaul>();
        Require(haul != null, "Actual blueprint carrier Haul authority is missing.");
        Require(archives.TryGetPreferredArchive(blueprint, out _, out string archiveDestination),
            "Actual blueprint archive authority is missing.");
        SkillHaulDiagnostics witness = new()
        {
            actorId = observedActor.Identity.PersistentId,
            blueprintItemInstanceId = physical[0].ItemInstanceId,
            archiveDestination = archiveDestination,
            observedExistingCarrierWithoutCommands = observeExistingCarrier,
            controlledNeutralFixture = !observeExistingCarrier,
            maximumDeltaTime = 0.1f
        };
        report.skillHaulDiagnostics = witness;
        if (!observeExistingCarrier)
        {
            worker.ClearPriorityWorkTarget();
            worker.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
            StabilizeTraitWorkFixture();
            observedActor.Brain.ClearPathSearchCache();
        }
        // The failed r5 allowed three game seconds per observed isolated frame.
        // Observe movement with the established fine work-tick cap instead of
        // raising its 90-game-second completion bound or injecting a pickup.
        IGameClock gameClock = scope.Container.Resolve<IGameClock>();
        float startedGame = gameClock.Time, startedReal = Time.realtimeSinceStartup;
        float nextNeutral = startedGame + 5f;
        gameSpeed.SetSpeed(5);
        // At public speed five Unity's scaled fixed timestep is 0.1 seconds;
        // r2 observed that as the effective cap even when 0.05 was requested.
        Time.maximumDeltaTime = witness.maximumDeltaTime;
        witness.maximumDeltaTime = Time.maximumDeltaTime;
        gameSpeed.SetPaused(false);
        while (Time.realtimeSinceStartup - startedReal < 180f && gameClock.Time - startedGame < 90f)
        {
            yield return null;
            witness.observedFrames++;
            witness.gameSeconds = gameClock.Time - startedGame;
            witness.realSeconds = Time.realtimeSinceStartup - startedReal;
            Vector2Int position = observedActor.GetNowXY();
            SkillHaulFrame sample = new()
            {
                frame = witness.observedFrames, x = position.x, y = position.y,
                gameSeconds = witness.gameSeconds, realSeconds = witness.realSeconds,
                deltaTime = gameClock.DeltaTime, hauling = haul.IsHauling,
                heartbeat = haul.RoutineHeartbeat, updateHeartbeat = haul.UpdateHeartbeatForDiagnostics,
                starts = haul.RuntimeHaulStartCount, terminals = haul.RuntimeHaulTerminalCount,
                stage = haul.CurrentExecutionStage, path = haul.ActivePathDebug,
                failure = haul.LastFailureReason, terminal = haul.LastTerminalDiagnostics,
                brain = observedActor.Brain.CurrentActionDebugLabel, brainPhase = observedActor.Brain.CurrentActionPhaseDetail,
                moveFailure = mover.LastGridMoveFailureReason.ToString(),
                blocked = mover.LastGridMoveWasBlocked, cancellation = mover.LastMovementCancellationSourceForDiagnostics,
                movementOwner = mover.ActiveMovementOperationOwnerForDiagnostics,
                replans = mover.RuntimeActionPathReplanCount, pathFailures = mover.RuntimeActionPathFailureCount,
                archived = archives.GetStatus(blueprint).IsArchived,
                carryQuantity = observedActor.CarryInventory.Capture().items.Sum(value => value.quantity)
            };
            witness.frames.Add(sample);
            if (sample.archived) { witness.completed = true; break; }
            if (gameClock.Time >= nextNeutral)
            {
                if (!observeExistingCarrier) StabilizeTraitWorkFixture();
                nextNeutral = gameClock.Time + 5f;
            }
        }
        gameSpeed.SetPaused(true);
        WorldItemStackSnapshot[] after = items.GetAllStacks().Where(value => value != null
            && value.ItemId == blueprint.PhysicalItemId && value.Quantity > 0).ToArray();
        witness.itemIdentityPreserved = after.Length == 1 && after[0].Quantity == 1
            && after[0].ItemInstanceId == witness.blueprintItemInstanceId
            && after[0].DestinationId == witness.archiveDestination;
        WriteNew("skill-haul-diagnostics.json", JsonUtility.ToJson(witness, true));
        WriteNew("skill-haul-observed-world.json", saves.ToJson(saves.Capture()));
        Require(witness.completed && witness.itemIdentityPreserved,
            "Focused actual Haul did not complete; inspect per-frame path/position/lease/movement diagnostics.");
    }

    private IEnumerator RunSkillResearchDiagnostics()
    {
        phase = "skill-research-focused-continuation";
        report.scope = "Focused actual Research from the preserved failed world; no request replay, combat or U4 proof claim";
        report.executionOrigin = "official hashed partial-world restore -> simulated-player public Research commands -> Brain/AIWork/AbilityWork/WorkTaskExecutor";
        CancelOutstandingTransport();
        queue.ConfigureIsolatedAutomaticDispatch(false);
        skillAutomaticDispatchDeferred = true;
        Require(CharacterWorkRoleUtility.TryGetWork(owner, out AbilityWork worker),
            "Restored staff has no actual research commands.");
        Require(owner.TryGetAbility(out AbilityMove mover), "Restored staff has no actual movement commands.");
        BlueprintResearchRuntime research = UnityEngine.Object.FindFirstObjectByType<BlueprintResearchRuntime>();
        Require(research != null && !OffenseExpeditionAccessRules.IsUnlocked(research.State)
                && research.State.Projects.Queue.Count > 0, "Research diagnostics require an unfinished restored queue.");
        IBlueprintResearchWorkService researchWork = scope.Container.Resolve<IBlueprintResearchWorkService>();
        BuildableObject desk = scope.Container.Resolve<IBuildingWorldQuery>().Buildings
            .FirstOrDefault(value => value != null && !value.isDestroy && researchWork.HasResearchWorkFor(value));
        Require(desk != null, "Restored research queue has no admitted actual facility.");
        SkillResearchDiagnostics witness = new()
        {
            actorId = owner.Identity.PersistentId,
            initialWork = research.State.Projects.ProgressById.Values.Sum(value => value.Progress)
        };
        report.skillResearchDiagnostics = witness;
        witness.researchGameTimeBound = CalculateSkillResearchGameTimeBound(research);
        HashSet<string> beforeKeys = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        worker.ClearPriorityWorkTarget();
        HashSet<string> beforeCompletedProjects = research.State.Projects.CompletedProjectIds.ToHashSet(StringComparer.Ordinal);
        string[] eligibleResearcherIds = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .Where(value => value != null && !value.IsDead && CharacterWorkRoleUtility.TryGetWork(value, out _))
            .Select(value => value.Identity.PersistentId).ToArray();
        ConfigureSkillResearchRole(worker);
        StabilizeTraitWorkFixture();
        Require(worker.TrySetPriorityWorkTarget(desk, BuiltInWorkTypeIds.Research, null, out string rejection),
            "Restored public Research command rejected: " + rejection);
        witness.publicResearchCommands++;
        owner.Brain.ClearPathSearchCache();
        IGameClock gameClock = scope.Container.Resolve<IGameClock>();
        float startedGame = gameClock.Time, startedReal = Time.realtimeSinceStartup;
        float nextNeutral = startedGame + 5f;
        long revision = worker.ApprovedWorkProgressRevisionForDiagnostics;
        float lastResearchApproval = startedGame;
        float lastGameProgress = startedGame, lastGameProgressReal = startedReal;
        gameSpeed.SetSpeed(5);
        Time.maximumDeltaTime = witness.maximumDeltaTime;
        witness.maximumDeltaTime = Time.maximumDeltaTime;
        gameSpeed.SetPaused(false);
        while (!OffenseExpeditionAccessRules.IsUnlocked(research.State)
            && gameClock.Time - startedGame < witness.researchGameTimeBound)
        {
            if (IssueNextSkillResearchCommandWhenIdle(worker, desk))
                witness.publicResearchCommands++;
            bool researching = worker.AssignedWorkTypeId == BuiltInWorkTypeIds.Research;
            long beforeTick = worker.ApprovedWorkProgressRevisionForDiagnostics;
            SetSkillResearchClock(worker, mover, witness.maximumDeltaTime, witness.stationaryMaximumDeltaTime);
            yield return null;
            if (gameClock.Time > lastGameProgress)
            {
                lastGameProgress = gameClock.Time;
                lastGameProgressReal = Time.realtimeSinceStartup;
            }
            Require(Time.realtimeSinceStartup - lastGameProgressReal < 30f,
                "Focused research has no game-clock progress for 30 real seconds.");
            witness.gameSeconds = gameClock.Time - startedGame;
            witness.realSeconds = Time.realtimeSinceStartup - startedReal;
            witness.approvedTicks = worker.ApprovedWorkProgressRevisionForDiagnostics - revision;
            if (researching)
            {
                witness.approvedResearchTicks += worker.ApprovedWorkProgressRevisionForDiagnostics - beforeTick;
                if (worker.ApprovedWorkProgressRevisionForDiagnostics > beforeTick)
                    lastResearchApproval = gameClock.Time;
            }
            witness.finalWork = research.State.Projects.ProgressById.Values.Sum(value => value.Progress);
            string active = research.State.Projects.ActiveProjectId.Value;
            Vector2Int position = owner.GetNowXY();
            witness.frames.Add(new SkillResearchFrame
            {
                frame = witness.frames.Count + 1, x = position.x, y = position.y,
                gameSeconds = witness.gameSeconds, realSeconds = witness.realSeconds, deltaTime = gameClock.DeltaTime,
                maximumDeltaTime = Time.maximumDeltaTime, moving = mover.HasActiveMovementRoutineForDiagnostics,
                genericCompletedWork = worker.GenericCompletedWorkForDiagnostics,
                genericRequiredWork = worker.GenericRequiredWorkForDiagnostics,
                activeProject = active, totalWork = witness.finalWork,
                activeWork = research.State.Projects.ProgressById.TryGetValue(active ?? string.Empty, out var progress)
                    ? progress.Progress : 0f,
                completedProjects = research.State.Projects.CompletedProjectIds.Count,
                approvedTicks = witness.approvedTicks, approvedResearchTicks = witness.approvedResearchTicks,
                workRunId = worker.ActiveWorkRunIdForDiagnostics,
                working = worker.isWorking, priorityTarget = worker.PriorityWorkTarget != null,
                assignedWork = worker.AssignedWorkTypeId.Value, offDuty = worker.IsOffDuty,
                brain = owner.Brain.CurrentActionDebugLabel, brainPhase = owner.Brain.CurrentActionPhaseDetail,
                preWuExit = worker.LastPreWuExitKindForDiagnostics + ": " + worker.LastPreWuExitDetailForDiagnostics,
                cancellation = worker.LastActiveWorkCancellationReasonForDiagnostics,
                moveFailure = mover.LastGridMoveFailureReason.ToString(), blocked = mover.LastGridMoveWasBlocked
            });
            if (gameClock.Time >= nextNeutral)
            {
                StabilizeTraitWorkFixture();
                nextNeutral = gameClock.Time + 5f;
            }
            Require(gameClock.Time - lastResearchApproval < 90f,
                "Focused research has no approved Research work for 90 game seconds; inspect preserved actual frames.");
        }
        gameSpeed.SetPaused(true);
        witness.completed = OffenseExpeditionAccessRules.IsUnlocked(research.State);
        witness.completedProjects = research.State.Projects.CompletedProjectIds.OrderBy(value => value, StringComparer.Ordinal).ToList();
        witness.completionOutcomes = ReadAllOutcomes().Where(value => !beforeKeys.Contains(OutcomeKey(value))
            && IsCompletedResearchWorkOutcome(value)).ToList();
        WriteNew("skill-research-diagnostics.json", JsonUtility.ToJson(witness, true));
        WriteNew("skill-research-observed-world.json", saves.ToJson(saves.Capture()));
        Require(witness.completed && witness.approvedResearchTicks > 0,
            "Focused actual Research did not unlock expedition; inspect work assignment/progress/cancellation frames.");
        RequireResearchCompletionOutcomes(witness.completedProjects.Where(value => !beforeCompletedProjects.Contains(value)),
            witness.completionOutcomes, eligibleResearcherIds);
    }

    private static bool IsCompletedResearchWorkOutcome(GameplayOutcomeSnapshot value) =>
        value.outcomeTypeId == ResearchWorkOutcomeIds.WorkApplied.Value
        && value.status == GameplayOutcomeStatus.Succeeded
        && value.facts.Any(fact => fact.factId == ResearchWorkOutcomeIds.CompletedFact.Value && fact.value == "true");

    private static void RequireResearchCompletionOutcomes(IEnumerable<string> projectIds,
        List<GameplayOutcomeSnapshot> rows, IReadOnlyCollection<string> eligibleResearcherIds)
    {
        string[] expected = projectIds.Distinct(StringComparer.Ordinal).ToArray();
        Require(expected.Length > 0 && rows.Count == expected.Length
            && rows.Select(OutcomeKey).Distinct(StringComparer.Ordinal).Count() == rows.Count,
            "Actual prerequisite research completion outcome count differs from newly completed projects.");
        foreach (string projectId in expected)
        {
            GameplayOutcomeSnapshot[] exact = rows.Where(value => value.participants.Any(participant =>
                participant.entityKindId == ResearchWorkOutcomeIds.ProjectKind.Value
                && participant.roleId == ResearchWorkOutcomeIds.ProjectRole.Value && participant.entityId == projectId)).ToArray();
            Require(exact.Length == 1 && exact[0].producerId == ResearchWorkOutcomeIds.ProducerId
                && IsCompletedResearchWorkOutcome(exact[0])
                && exact[0].participants.Count(participant => participant.entityKindId == ResearchWorkOutcomeIds.ProjectKind.Value
                    && participant.roleId == ResearchWorkOutcomeIds.ProjectRole.Value) == 1,
                "Actual research lacks one exact successful completion for project: " + projectId);
            GameplayOutcomeParticipantSnapshot[] researchers = exact[0].participants.Where(participant =>
                participant.entityKindId == ResearchWorkOutcomeIds.CharacterKind.Value
                && participant.roleId == ResearchWorkOutcomeIds.ResearcherRole.Value).ToArray();
            // Autonomous eligible colleagues can finish prerequisite projects.
            // Keep their actual credit instead of requiring the selected skill owner.
            Require(researchers.Length == 1 && !string.IsNullOrWhiteSpace(researchers[0].entityId)
                && eligibleResearcherIds.Contains(researchers[0].entityId),
                "Actual research lacks one exact successful completion for project/staff: " + projectId);
        }
    }

    private static bool IsSkillPreparationNutritionSafe(int healthConditions, float diseaseRisk,
        int untreated, int shortageDays) => healthConditions == 0 && diseaseRisk >= 0f
        && diseaseRisk <= 50f && untreated == 0 && shortageDays == 0;

    public static void RunSkillPreparationContractsBatch()
    {
        // Controlled QA inputs only; these are never published as gameplay outcomes.
        GameplayOutcomeSnapshot Row(long sequence, string project, string researcher) => new()
        {
            runId = "run:qa-skill-preparation", sequence = sequence,
            producerId = ResearchWorkOutcomeIds.ProducerId,
            outcomeTypeId = ResearchWorkOutcomeIds.WorkApplied.Value,
            status = GameplayOutcomeStatus.Succeeded,
            participants = new()
            {
                new() { entityKindId = ResearchWorkOutcomeIds.ProjectKind.Value,
                    roleId = ResearchWorkOutcomeIds.ProjectRole.Value, entityId = project },
                new() { entityKindId = ResearchWorkOutcomeIds.CharacterKind.Value,
                    roleId = ResearchWorkOutcomeIds.ResearcherRole.Value, entityId = researcher }
            },
            facts = new() { new() { factId = ResearchWorkOutcomeIds.CompletedFact.Value, value = "true" } }
        };
        string[] projects = { "qa:project-a", "qa:project-b" };
        string[] researchers = { "qa:selected", "qa:colleague" };
        List<GameplayOutcomeSnapshot> Rows() => new()
            { Row(1, projects[0], researchers[0]), Row(2, projects[1], researchers[1]) };
        List<GameplayOutcomeSnapshot> valid = Rows();
        string before = JsonUtility.ToJson(valid[1]);
        RequireResearchCompletionOutcomes(projects, valid, researchers);
        Require(before == JsonUtility.ToJson(valid[1]), "Research validation rewrote the colleague's credit.");
        int rejected = 0;
        void Reject(List<GameplayOutcomeSnapshot> rows, string label)
        {
            try { RequireResearchCompletionOutcomes(projects, rows, researchers); }
            catch (InvalidOperationException) { rejected++; return; }
            throw new InvalidOperationException("Research preparation accepted " + label);
        }
        Reject(new() { valid[0] }, "missing completion");
        Reject(new() { valid[0], valid[0] }, "duplicate completion identity");
        Reject(new() { valid[0], Row(2, projects[1], "qa:unknown") }, "unknown researcher");
        List<GameplayOutcomeSnapshot> failed = Rows(); failed[1].status = GameplayOutcomeStatus.Failed;
        Reject(failed, "failed research");
        List<GameplayOutcomeSnapshot> unfinished = Rows(); unfinished[1].facts[0].value = "false";
        Reject(unfinished, "unfinished research");
        List<GameplayOutcomeSnapshot> ambiguous = Rows(); ambiguous[1].participants.Add(ambiguous[1].participants[1]);
        Reject(ambiguous, "ambiguous researcher");
        List<GameplayOutcomeSnapshot> wrongProducer = Rows(); wrongProducer[1].producerId = "qa:wrong";
        Reject(wrongProducer, "wrong producer");
        Require(rejected == 7, "Research negative contract count changed.");
        Require(IsSkillPreparationNutritionSafe(0, 0f, 0, 0)
            && IsSkillPreparationNutritionSafe(0, 50f, 0, 0)
            && !IsSkillPreparationNutritionSafe(1, 0f, 0, 0)
            && !IsSkillPreparationNutritionSafe(0, 51f, 0, 0)
            && !IsSkillPreparationNutritionSafe(0, 0f, 1, 0)
            && !IsSkillPreparationNutritionSafe(0, 0f, 0, 1)
            && !IsSkillPreparationNutritionSafe(0, float.NaN, 0, 0)
            && !IsSkillPreparationNutritionSafe(0, -1f, 0, 0),
            "Non-target nutrition preparation accepted unsafe state or rejected a healthy boundary.");
        Debug.Log("PASS SkillPreparationContracts: colleague credit preserved; research negatives=7; nutrition boundaries=8.");
    }

    private static void ConfigureSkillResearchRole(AbilityWork worker)
    {
        // Public player job assignment, never progress/completion injection.
        // Operate and Reception are distinct; r1 also selected autonomous Repair
        // after finishing a research run. r2 then chose unrelated Haul=P2.
        // Purchased blueprints are already archived before this phase, so
        // keep only Research enabled; do not cancel bound deliveries or fake them.
        foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
            worker.SetWorkPriority(definition.WorkTypeId,
                definition.WorkTypeId == BuiltInWorkTypeIds.Research ? WorkPriorityLevel.Priority1
                : WorkPriorityLevel.Off);
    }

    private static bool IssueNextSkillResearchCommandWhenIdle(AbilityWork worker, BuildableObject desk)
    {
        if (worker.isWorking || worker.HasActiveWorkRoutineForDiagnostics
            || worker.AssignedWorkTypeId.IsValid || worker.PriorityWorkTarget != null)
            return false;
        // A public order owns one normal work run. r3 confirmed that completed
        // six-WU sessions clear it, after which autonomous utility may wander.
        // This controlled integration scenario explicitly issues another player
        // order; it does not claim continuous autonomous research or bypass an
        // admitted command, movement, work, resource or outcome authority.
        Require(worker.TrySetPriorityWorkTarget(desk, BuiltInWorkTypeIds.Research, null, out string rejection),
            "Next explicit public Research command rejected: " + rejection);
        return true;
    }

    private static float CalculateSkillResearchGameTimeBound(BlueprintResearchRuntime research)
    {
        float remainingWork = 0f;
        foreach (ResearchQueueEntry entry in research.State.Projects.Queue)
        {
            Require(research.ProjectCatalog.TryGet(entry.ProjectId, out ResearchProjectSO project),
                "Restored research queue refers to a missing authored project.");
            float completedWork = research.State.Projects.ProgressById.TryGetValue(entry.ProjectId.Value, out var progress)
                ? progress.Progress : 0f;
            remainingWork += Mathf.Max(0f, project.RequiredWork - completedWork);
        }
        // r1: two real work sessions applied 11 project WU in 17 research
        // game seconds, excluding travel. The remaining 147 authored WU cannot
        // fit the former 180-second whole-chain bound even without movement.
        // Budget from unchanged authored work; independently reject 90 seconds
        // without Research approval instead of disguising a stalled worker.
        return Mathf.Max(180f, remainingWork * 3f + 90f);
    }

    private static void SetSkillResearchClock(AbilityWork worker, AbilityMove mover, float physical, float stationary)
    {
        // Re-evaluate every frame. A first approved tick is not sufficient:
        // completed sessions release the target and start actual replanning.
        Time.maximumDeltaTime = worker.isWorking
            && worker.AssignedWorkTypeId == BuiltInWorkTypeIds.Research
            && worker.HasActiveGenericProgressForDiagnostics
            && !mover.HasActiveMovementRoutineForDiagnostics ? stationary : physical;
    }

    private IEnumerator RunSkillBattleDiagnostics()
    {
        report.executionOrigin = "official hashed admission-world restore -> registered expedition -> public battle Ability command";
        CancelOutstandingTransport();
        queue.ConfigureIsolatedAutomaticDispatch(false);
        skillAutomaticDispatchDeferred = true;
        BlueprintResearchRuntime research = UnityEngine.Object.FindFirstObjectByType<BlueprintResearchRuntime>();
        Require(research != null && (OffenseExpeditionAccessRules.IsUnlocked(research.State)
                || (SkillSupportContinuation && skillNormalWeaponPreparation)),
            "Battle diagnostics require completed admission research or explicit normal support preparation.");
        CharacterSkillInstance[] selected = owner.Progression.ActiveSkills.Where(value => value != null
            && value.kind == CharacterSkillKind.Active && value.formulaVersion > 0
            && value.evidenceIds.Count > 0).ToArray();
        Require(selected.Length == 1 && selected[0].IsReady
                && selected[0].evidenceBindings.Count == selected[0].evidenceIds.Count
                && selected[0].evidenceBindings.All(value => value.sourceKind == "exact")
                && selected[0].evidenceBindings.Select(value => value.publicFactId)
                    .SequenceEqual(selected[0].evidenceIds, StringComparer.Ordinal),
            "Battle diagnostics require one existing ready generated active skill with exact evidence.");
        Require(!SkillSupportContinuation || (owner.Identity.PersistentId == skillBattleActorId
                && selected[0].id == skillBattleSkillId),
            "Selected support continuation must retain the exact previously committed actor and skill.");
        skillSelectionFacts = selected[0].evidenceIds.ToArray();
        if (!OffenseExpeditionAccessRules.IsUnlocked(research.State))
        {
            // Continue the already chosen exact skill from its official partial
            // world. Reuse the normal purchase/archive/Research commands; never
            // bypass the expedition's production admission gate or re-author
            // the old source-v9 request as a current request.
            Require(!scope.Container.Resolve<IOffenseBattleRuntime>().HasActiveBattle,
                "Unfinished admission cannot coexist with an active support battle.");
            yield return RunSkillMechanicWitness(selected[0].id);
            yield break;
        }
        SkillMechanicWitness witness = new()
        {
            consumerPath = report.executionOrigin,
            actorId = owner.Identity.PersistentId,
            skillId = selected[0].id,
            researchId = OffenseExpeditionAccessRules.RequiredResearchId,
            researchCompleted = true
        };
        report.skillMechanic = witness;
        Require(CharacterWorkRoleUtility.TryGetWork(owner, out AbilityWork worker),
            "Restored selected actor has no normal departure command recipient.");
        worker.ClearPriorityWorkTarget();
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        IOffenseBattleRuntime restoredBattle = scope.Container.Resolve<IOffenseBattleRuntime>();
        bool prepareSupportPrerequisites = SkillSupportContinuation && !restoredBattle.HasActiveBattle;
        CharacterActor[] neutralNeedResidents = prepareSupportPrerequisites
            ? scope.Container.Resolve<ICharacterWorldQuery>().Characters
                .Where(value => value != null && !value.IsDead && (value.Role == CharacterRole.Owner
                    || CharacterWorkRoleUtility.TryGetWork(value, out _)))
                .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal).ToArray()
            : Array.Empty<CharacterActor>();
        CharacterCondition[] approvedNeutralNeeds =
        {
            CharacterCondition.THIRST, CharacterCondition.SLEEP,
            CharacterCondition.EXCRETION, CharacterCondition.HYGIENE
        };
        if (prepareSupportPrerequisites)
        {
            Require(neutralNeedResidents.Length == 3,
                "Approved support neutral-needs fixture requires exactly the three restored residents.");
            WriteNew("skill-support-neutral-needs-fixture.json", new JObject
            {
                ["actorIds"] = new JArray(neutralNeedResidents.Select(value => value.Identity.PersistentId)),
                ["conditions"] = new JArray(approvedNeutralNeeds.Select(value => value.ToString())
                    .Concat(skillRecruitmentHistoryCapacityFixture ? new[] { "HUNGER", "FUN" } : Array.Empty<string>())),
                ["intervalGameSeconds"] = 5,
                ["scope"] = "explicit isolated preparation; existing selected-actor environment fixture unchanged",
                ["hungerOrHealthInjected"] = skillRecruitmentHistoryCapacityFixture,
                ["healthInjected"] = false, ["foodOrMoneyInjected"] = false,
                ["funInjected"] = skillRecruitmentHistoryCapacityFixture,
                ["applyToNewRecruitedResidents"] = skillRecruitmentHistoryCapacityFixture,
                ["breakdownOrExposureReset"] = false,
                ["naturalPlayClaimed"] = false, ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            }.ToString());
        }
        float startedGame = clock.Time, startedReal = Time.realtimeSinceStartup;
        float lastProgressGame = startedGame, lastProgressReal = startedReal, nextNeutral = startedGame;
        void Observe()
        {
            if (prepareSupportPrerequisites)
                Require(!report.warnings.Any(message => message.StartsWith(
                        "Autosave failed after operating day ", StringComparison.Ordinal)),
                    "Support preparation stopped at the first actual autosave failure; invalid world is not a reuse proof.");
            witness.observedFrames++;
            witness.elapsedGameSeconds = clock.Time - startedGame;
            witness.elapsedRealSeconds = Time.realtimeSinceStartup - startedReal;
            if (clock.Time > lastProgressGame)
            {
                lastProgressGame = clock.Time;
                lastProgressReal = Time.realtimeSinceStartup;
            }
            Require(Time.realtimeSinceStartup - lastProgressReal < 30f,
                "Battle diagnostics simulation stopped despite observed frames.");
            if (clock.Time >= nextNeutral)
            {
                StabilizeTraitWorkFixture(restoreHunger: !SkillSupportContinuation);
                // Without the broad test-preparation opt-in, retain the original
                // four-need scope. Never reuse construction's health/exposure or
                // breakdown preparation for this support-effect witness.
                foreach (CharacterActor resident in neutralNeedResidents)
                {
                    Require(resident != null && !resident.IsDead && resident.Stats != null,
                        "A restored resident became unavailable to the approved neutral-needs fixture.");
                    foreach (CharacterCondition need in approvedNeutralNeeds)
                    {
                        bool available = resident.Stats.TryGetConditionValue(need, out float current);
                        Require(available, "Approved neutral need is missing: " + need);
                        if (current != 100f)
                            resident.ChangesStat(need, 100f - current);
                        else
                            witness.unchangedApprovedNeedWritesSkipped++;
                    }
                }
                if (skillRecruitmentHistoryCapacityFixture)
                    MaintainControlledSupportPreparationNeeds();
                if (skillNonTargetHungerFixture)
                    MaintainNonTargetSkillPreparationHunger();
                witness.stabilizationPasses++;
                nextNeutral = clock.Time + 5f;
            }
        }
        Observe();
        gameSpeed.SetSpeed(5);
        if (prepareSupportPrerequisites)
        {
            bool restoredPaused = gameSpeed.IsPaused;
            // Official partial saves retain pause. Speed selection does not
            // resume that state; issue the normal public resume once before
            // any timed physical meal/haul preparation, not in the watchdog.
            gameSpeed.SetPaused(false);
            Require(!gameSpeed.IsPaused && timeScale.Scale == gameSpeed.Speed,
                "Public support preparation resume did not publish its running clock.");
            WriteNew("skill-normal-preparation-resume-command.json", new JObject
            {
                ["origin"] = "explicit public resume after exact official restore",
                ["restoredPaused"] = restoredPaused, ["isPausedAfter"] = gameSpeed.IsPaused,
                ["speedAfter"] = gameSpeed.Speed, ["clockScaleAfter"] = timeScale.Scale,
                ["watchdogOrTimeBoundChanged"] = false, ["trainingEligible"] = false
            }.ToString());
            Time.maximumDeltaTime = witness.physicalMaximumDeltaTime;
            // The disposable exterior regression has dedicated preserved
            // evidence. Do not install its non-natural empty preview profiles
            // into the actual recruitment world's persistent guest pool.
            WriteNew("skill-exterior-spawn-regression.json", new JObject
            {
                ["status"] = "NOT_REEXECUTED",
                ["reason"] = "unchanged exterior implementation; disposable regression runs separately from persistent recruitment gameplay",
                ["historicalFocusedEvidence"] = "Artifacts/QA/AuditRemediation-20260926-r1/injured-returnee-save-handoff-r3",
                ["historicalFourKindEvidence"] = "Artifacts/QA/AuditRemediation-20260926-r1/skill-support-continuation-r38/unity-evidence/skill-exterior-spawn-regression.json",
                ["currentRegressionPassClaimed"] = false,
                ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
            }.ToString());
            yield return PrepareSkillRecruitmentNutrition(witness, Observe);
            WriteNew("skill-support-admission-world.json", saves.ToJson(saves.Capture()));
        }
        else if (SkillSupportContinuation)
        {
            Require(restoredBattle.HasActiveBattle && restoredBattle.Session != null,
                "Saved support prerequisites require the exact restored active battle.");
            WriteNew("skill-support-saved-battle-prerequisites.json", new JObject
            {
                ["origin"] = "official hashed active-battle checkpoint; prior actual recruitment/departure retained, not reexecuted",
                ["battleId"] = restoredBattle.Session.BattleId, ["actorId"] = witness.actorId,
                ["skillId"] = witness.skillId, ["threeResidentNeutralNeedsFixtureApplied"] = false,
                ["existingSelectedActorStabilizationApplied"] = true,
                ["foodOrHealthInjected"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
            }.ToString(Newtonsoft.Json.Formatting.Indented));
        }
        yield return RunSkillBattleMechanicWitness(witness, Observe);
    }

    private void ApplyApprovedThirtyFoodPreparation()
    {
        Require(skillThirtyFoodPreparationFixture && !skillThirtyFoodPreparationApplied
                && SkillSupportContinuation && skillNormalWeaponPreparation
                && report.continuation?.officialRestore == true
                && Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && !scope.Container.Resolve<IOffenseBattleRuntime>().HasActiveBattle,
            "The approved food preparation must run once, after exact isolated support restore and before gameplay.");
        const string mealItem = "food:preserved-ration";
        const int approvedQuantity = 30;
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        IWarehouseFeatureQueryService supply = scope.Container.Resolve<IWarehouseFeatureQueryService>();
        ISurvivalFoodPersistence survival = scope.Container.Resolve<ISurvivalFoodPersistence>();
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        int Quantity() => items.GetAllStacks().Where(value => value != null && value.ItemId == mealItem)
            .Sum(value => value.Quantity);
        int beforeQuantity = Quantity(), beforeMoney = supply.Capture().CurrentMoney;
        Require(beforeQuantity == 20 && beforeMoney == 4,
            "The explicitly approved r26 resource state differs; do not top up a changed continuation.");
        string[] beforeOutcomes = ReadAllOutcomes().Select(value => JsonUtility.ToJson(value)).ToArray();
        string beforeSurvival = JsonUtility.ToJson(survival.Capture());
        float beforeGameTime = clock.Time, beforeHealth = owner.CurrentHealth;
        int beforeDay = calendar.Day;
        bool wasPaused = gameSpeed.IsPaused;
        gameSpeed.SetPaused(true);
        try
        {
            WriteNew("skill-thirty-food-preparation-before-world.json", saves.ToJson(saves.Capture()));
            WriteNew("skill-thirty-food-preparation-reservation.json", new JObject
            {
                ["origin"] = "human-authorized isolated test preparation only",
                ["itemId"] = mealItem, ["authorizedQuantity"] = approvedQuantity,
                ["sourceCheckpointSha256"] = "54ffb75b344e07ff0dccc45c0901ff604e0414633f2992dd14ba054756422ada",
                ["actualSpawnClaimed"] = false, ["purchaseOrProductionClaimed"] = false,
                ["trainingEligible"] = false, ["humanApprovalClaimed"] = false
            }.ToString(Newtonsoft.Json.Formatting.Indented));
            Require(items.SpawnItemAtDropoff(mealItem, approvedQuantity,
                    "qa:explicit-thirty-food-preparation-input:not-purchase-or-production", out int spawned)
                    && spawned == approvedQuantity && Quantity() == checked(beforeQuantity + approvedQuantity),
                "Approved food preparation failed to create exactly thirty physical inputs at the normal dropoff.");
            skillThirtyFoodPreparationApplied = true;
            bool unchanged = beforeMoney == supply.Capture().CurrentMoney
                && beforeOutcomes.SequenceEqual(ReadAllOutcomes().Select(value => JsonUtility.ToJson(value)))
                && beforeSurvival == JsonUtility.ToJson(survival.Capture())
                && beforeGameTime == clock.Time && beforeDay == calendar.Day
                && beforeHealth == owner.CurrentHealth;
            WriteNew("skill-thirty-food-preparation-input.json", new JObject
            {
                ["origin"] = "explicit thirty-food preparation, not a production/purchase or natural-play event",
                ["itemId"] = mealItem, ["quantityBefore"] = beforeQuantity,
                ["quantityAdded"] = approvedQuantity, ["quantityAfter"] = Quantity(),
                ["moneyBefore"] = beforeMoney, ["moneyAfter"] = supply.Capture().CurrentMoney,
                ["physicalState"] = "normal dropoff Loose; later haul/consumption must be real",
                ["preparationApplicationCount"] = 1, ["originalCanonicalOutcomesUnchanged"] = unchanged,
                ["survivalHealthClockAndMoneyUnchanged"] = unchanged,
                ["reuseInstruction"] = "Resume the prepared checkpoint without the food opt-in; never repeat this top-up.",
                ["purchaseOrProductionClaimed"] = false, ["naturalPlayClaimed"] = false,
                ["trainingEligible"] = false, ["humanApprovalClaimed"] = false
            }.ToString(Newtonsoft.Json.Formatting.Indented));
            Require(unchanged, "Food test preparation changed canonical results, payment, survival, health or time.");
            WriteNew("skill-thirty-food-preparation-applied-world.json", saves.ToJson(saves.Capture()));
        }
        finally
        {
            gameSpeed.SetPaused(wasPaused);
        }
    }

    private IEnumerator PrepareSkillRecruitmentNutrition(SkillMechanicWitness mechanic, Action observe)
    {
        // The construction fixture restores needs, not meals. r12 therefore
        // owned eleven genuine missed-meal days and two sick residents. Repair
        // this isolated preparation through actual meals and real midnights;
        // never reset the survival DTO, health, calendar or recruitment gate.
        phase = "skill-normal-nutrition-preparation";
        var witness = new SkillNutritionWitness { startingDay = calendar.Day };
        mechanic.nutrition = witness;
        ISurvivalFoodPersistence survival = scope.Container.Resolve<ISurvivalFoodPersistence>();
        ISurvivalFoodQuery food = scope.Container.Resolve<ISurvivalFoodQuery>();
        ICharacterNutritionRuntime nutrition = scope.Container.Resolve<ICharacterNutritionRuntime>();
        ICharacterNeedBalanceRuntime needBalance = scope.Container.Resolve<ICharacterNeedBalanceRuntime>();
        CharacterNeedStateService needDecay = scope.Container.Resolve<CharacterNeedStateService>();
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        CharacterActor[] residents = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .Where(value => value != null && !value.IsDead && (value.Role == CharacterRole.Owner
                || CharacterWorkRoleUtility.TryGetWork(value, out _)))
            .OrderByDescending(value => value.Role == CharacterRole.Owner)
            .ThenBy(value => value.Identity.PersistentId, StringComparer.Ordinal).ToArray();
        witness.before = survival.Capture();
        Require(residents.Length == 3 && witness.before.health.All(value => value.remainingSeconds <= 360f),
            "The bounded saved support preparation requires exactly three residents and at most two recovery days.");
        WriteNew("skill-nutrition-before.json", JsonUtility.ToJson(witness, true));
        IWarehouseFeatureQueryService supply = scope.Container.Resolve<IWarehouseFeatureQueryService>();
        IWarehouseFeatureCommandService purchases = scope.Container.Resolve<IWarehouseFeatureCommandService>();
        const string mealItem = "food:preserved-ration";
        int Quantity() => items.GetAllStacks().Where(value => value != null && value.ItemId == mealItem).Sum(value => value.Quantity);
        witness.quantityBeforePurchase = Quantity();
        witness.moneyBeforePurchase = supply.Capture().CurrentMoney;
        witness.moneyAfterPurchase = witness.moneyBeforePurchase;
        int deficit = checked(residents.Length * (7 + 3)) - witness.quantityBeforePurchase;
        SurvivalFoodOverview restoredOverview = food.GetOverview();
        bool recoveryAlreadySafe = IsSkillPreparationNutritionSafe(witness.before.health.Count,
            restoredOverview.DiseaseRisk, restoredOverview.UntreatedCount, witness.before.consecutiveFoodShortageDays);
        Require(!skillNonTargetHungerFixture || recoveryAlreadySafe,
            "NonTargetNutritionFixtureRequiresSafeState: resolve actual illness/shortage with the nutrition fixture disabled; no health reset or recovery claim is permitted.");
        if (deficit > 0)
        {
            WarehouseFeatureSurfaceModel current = supply.Capture();
            WriteNew("skill-nutrition-supply-preflight.json", new JObject
            {
                ["origin"] = "read-only actual public supply quote before any purchase or preparation input",
                ["physicalRations"] = witness.quantityBeforePurchase, ["requiredRationDeficit"] = deficit,
                ["currentMoney"] = current.CurrentMoney,
                ["authoredFoodOffers"] = JArray.FromObject(current.DeliveryOffers
                    .Where(value => value.Offer.itemId == mealItem).Select(value => value.Offer)),
                ["moneyOrFoodInjected"] = false, ["trainingEligible"] = false
            }.ToString(Newtonsoft.Json.Formatting.Indented));
            StockDeliveryOffer offer = current.DeliveryOffers.Where(value => value.Offer.itemId == mealItem
                    && value.Offer.amount >= deficit && value.Offer.cost <= current.CurrentMoney)
                .OrderBy(value => value.Offer.cost).Select(value => value.Offer).FirstOrDefault();
            Require(offer.itemId == mealItem && offer.amount >= deficit && offer.cost <= current.CurrentMoney,
                "NutritionSupplyUnavailable: no adequate affordable authored meal delivery offer.");
            witness.moneyBeforePurchase = current.CurrentMoney;
            WarehouseFeatureCommandResult purchase = purchases.PurchaseDelivery(offer);
            witness.moneyAfterPurchase = supply.Capture().CurrentMoney;
            witness.quantityAfterPurchase = Quantity();
            witness.foodPurchased = purchase.Succeeded
                && witness.moneyAfterPurchase == witness.moneyBeforePurchase - offer.cost
                && witness.quantityAfterPurchase == witness.quantityBeforePurchase + offer.amount;
            Require(witness.foodPurchased, "Actual nutrition payment/delivery failed: " + purchase.Message);
        }
        else witness.quantityAfterPurchase = witness.quantityBeforePurchase;
        if ((SkillSupportContinuation || skillNonTargetHungerFixture) && recoveryAlreadySafe
            && witness.quantityAfterPurchase >= checked(residents.Length * (7 + 3)))
        {
            // The explicit non-target fixture holds hunger and daily nutrition.
            // Waiting for natural meal admission under that hold cannot progress.
            // Reuse only healthy, adequately supplied state; never invent meals.
            witness.origin = skillNonTargetHungerFixture
                ? "explicit non-target nutrition preparation; healthy state and actual ration supply verified; no meal or recovery execution claimed"
                : "restored healthy state and actual ration supply verified; no new meal or recovery execution claimed";
            witness.nonTargetPreparationAlreadySafe = skillNonTargetHungerFixture;
            witness.restoredRecoveryAlreadySafe = SkillSupportContinuation && !skillNonTargetHungerFixture;
            witness.pressureReleased = true;
            witness.endingDay = calendar.Day;
            witness.after = survival.Capture();
            WriteNew("skill-nutrition-witness.json", JsonUtility.ToJson(witness, true));
            yield break;
        }
        CharacterActor hauler = residents.Single(value => value.Identity.PersistentId == "character:staff:1:01");
        AbilityWork haulWork = hauler.GetAbility<AbilityWork>();
        WorkPriorityLevel oldHaul = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul);
        bool haulerWasOffDuty = haulWork.IsOffDuty;
        CharacterActivityEvent previous = hauler.LogComponent.ActivityEntries.LastOrDefault();
        try
        {
            // A priority does not make an off-duty saved worker available.
            // Use the ordinary duty command and restore it on every exit.
            haulWork.SetDutyState(AbilityWork.DutyState.OnDuty);
            witness.haulOnDutyApplied = !haulWork.IsOffDuty;
            Require(witness.haulOnDutyApplied, "Nutrition public Haul duty command did not apply.");
            if (oldHaul != WorkPriorityLevel.Priority1)
            {
                try { haulWork.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1); }
                finally { witness.publicHaulAssigned = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul) == WorkPriorityLevel.Priority1; }
                CharacterActivityEvent changed = hauler.LogComponent.ActivityEntries.LastOrDefault();
                witness.haulActivityExact = changed != null && !ReferenceEquals(changed, previous)
                    && changed.ActorId == hauler.Identity.PersistentId && changed.ActionId == BuiltInWorkTypeIds.Haul.Value
                    && changed.OutcomeId == CharacterActivityOutcomes.Changed
                    && changed.ReasonCode == $"priority:{(int)WorkPriorityLevel.Priority1}"
                    && changed.Value.Equals((float)(int)WorkPriorityLevel.Priority1);
                Require(witness.haulActivityExact, "Nutrition Haul assignment lacks its exact public-command activity.");
            }
            IGameClock clock = scope.Container.Resolve<IGameClock>();
            int needWaitSequence = 0;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                int day = calendar.Day;
                foreach (CharacterActor actor in residents)
                {
                    if (nutrition.GetMealsConsumed(actor.Identity.PersistentId, calendar.Day) > 0) continue;
                    int residentMealStartDay = calendar.Day;
                    bool ResidentMealConsumed() => nutrition.GetRecentMeals().Any(value => value.characterId == actor.Identity.PersistentId
                        && value.day >= residentMealStartDay && value.amount > 0);
                    int supplyRaceRetries = 0;
                    while (!ResidentMealConsumed())
                    {
                        // Do not create hunger or use a manual consumables shortcut.
                        // Excluding hunger from the neutral work fixture lets the
                        // normal need owner reach automatic meal admission itself.
                        float hungerBefore = actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
                        float hungerRate = needDecay.CalculateTimedDecay(actor, 1f).Hunger;
                        float routineStart = needBalance.GetResponse(CharacterCondition.HUNGER).routineStart;
                        Require(hungerRate > 0f, "This resident has no real automatic hunger progression.");
                        float needBound = Math.Max(0f, hungerBefore - routineStart + 0.1f) / hungerRate
                            + needBalance.DayLengthSeconds;
                        float needStartedGame = clock.Time, needStartedReal = Time.realtimeSinceStartup;
                        Time.maximumDeltaTime = mechanic.calendarMaximumDeltaTime;
                        while (!ResidentMealConsumed()
                               && actor.Stats.GetConditionValue(CharacterCondition.HUNGER) >= routineStart
                               && clock.Time - needStartedGame < needBound
                               && Time.realtimeSinceStartup - needStartedReal < 240f)
                        {
                            observe();
                            yield return null;
                        }
                        bool alreadyConsumed = ResidentMealConsumed();
                        float hungerAfter = actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
                        WriteNew($"skill-nutrition-natural-need-{++needWaitSequence:D3}.json", new JObject
                        {
                            ["actorId"] = actor.Identity.PersistentId, ["hungerBefore"] = hungerBefore,
                            ["hungerAfter"] = hungerAfter, ["routineStart"] = routineStart,
                            ["initialNaturalDepletionPerSecond"] = hungerRate, ["derivedGameTimeBound"] = needBound,
                            ["elapsedGameSeconds"] = clock.Time - needStartedGame,
                            ["elapsedRealSeconds"] = Time.realtimeSinceStartup - needStartedReal,
                            ["actualMealAlreadyConsumed"] = alreadyConsumed,
                            ["hungerInjected"] = false, ["healthInjected"] = false, ["trainingEligible"] = false
                        }.ToString());
                        Require(alreadyConsumed || hungerAfter < routineStart,
                            "Natural hunger did not reach the authored meal admission threshold.");
                        if (alreadyConsumed) continue;
                        Time.maximumDeltaTime = mechanic.physicalMaximumDeltaTime;
                        Facility[] mealCandidates = scope.Container.Resolve<IBuildingWorldQuery>().Buildings.OfType<Facility>()
                            .Where(value => value != null && !value.isDestroy && value.SupportsFacilityRole(FacilityRole.Meal)
                                && CharacterVisitPolicy.CanVisitBuilding(actor, value, false, out _))
                            .OrderBy(value => Vector2.Distance(actor.transform.position, value.transform.position)).ToArray();
                        Require(mealCandidates.Length > 0, "NutritionFacilityUnavailable: the resident has no legitimate authored dining destination.");
                        Facility meal = mealCandidates[0];
                        string initialMealFacilityId = meal.RequirePersistentInstanceId().Value;
                        // Normal AIEat deliberately excludes DeliveryPending: a
                        // warehouse item (or an in-flight haul) is not food at the
                        // facility yet. Let the existing consumables Tick author
                        // the delivery and production haulers deposit it first.
                        // Use the established primary-meal delivery bound, without
                        // setting hunger, creating a buffer or weakening scoring.
                        phase = "skill-normal-meal-delivery";
                        CharacterConsumablesRuntime consumables = scope.Container.Resolve<CharacterConsumablesRuntime>();
                        float deliveryStartedGame = clock.Time, deliveryStartedReal = Time.realtimeSinceStartup;
                        long probeCountBefore = consumables.MealDeliveryProbeCount;
                        CharacterConsumablesFailure mealFailure = default;
                        bool ProbePhysicalMeal()
                        {
                            // Demand routing and other residents may supply a
                            // different legitimate dining fixture. Do not pin an
                            // empty nearest fixture while real food arrives in
                            // another. Registered AIEat still owns admission,
                            // scoring, reservation, execution and consumption.
                            foreach (Facility candidate in mealCandidates)
                            {
                                if (candidate.HasMealAvailableFor(actor, out CharacterConsumablesFailure available)
                                    && !available.IsFailure)
                                {
                                    meal = candidate;
                                    mealFailure = available;
                                    return true;
                                }
                            }
                            meal.HasMealAvailableFor(actor, out mealFailure);
                            return false;
                        }
                        bool mealReady = ProbePhysicalMeal();
                        while (!mealReady
                               && !ResidentMealConsumed()
                               && clock.Time - deliveryStartedGame < needBalance.DayLengthSeconds * 2f
                               && Time.realtimeSinceStartup - deliveryStartedReal < 60f)
                        {
                            Require(mealFailure.Code == CharacterConsumablesFailureCode.DeliveryPending,
                                "NutritionMealUnavailable: " + mealFailure.Code + ": " + string.Join(",", mealFailure.Parameters));
                            observe();
                            yield return null;
                            mealReady = ProbePhysicalMeal();
                        }
                        alreadyConsumed = ResidentMealConsumed();
                        string mealDestination = CharacterConsumablesInputDestinationIdentity.Build(
                            CharacterConsumablesInputKind.Meal, meal.RequirePersistentInstanceId(),
                            new ConsumableItemDefinitionId(mealItem));
                        WriteNew($"skill-nutrition-delivery-{needWaitSequence:D3}.json", new JObject
                        {
                            ["actorId"] = actor.Identity.PersistentId, ["facilityId"] = meal.RequirePersistentInstanceId().Value,
                            ["initialNearestFacilityId"] = initialMealFacilityId,
                            ["destinationSelection"] = "nearest legitimate facility with actual physical food; no pending-delivery score bypass",
                            ["physicalMealReady"] = mealReady, ["actualMealAlreadyConsumed"] = alreadyConsumed,
                            ["elapsedGameSeconds"] = clock.Time - deliveryStartedGame,
                            ["elapsedRealSeconds"] = Time.realtimeSinceStartup - deliveryStartedReal,
                            ["probeCountBefore"] = probeCountBefore, ["probeCountAfter"] = consumables.MealDeliveryProbeCount,
                            ["lastProbe"] = consumables.LastMealDeliveryProbeDetail,
                            ["lastRequestFailure"] = consumables.LastMealDeliveryRequestFailure,
                            ["availabilityFailure"] = mealFailure.Code.ToString(),
                            ["bufferQuantity"] = items.GetAllStacks().Where(value => value != null
                                && value.State == WorldItemStackState.FacilityBuffer && value.DestinationId == mealDestination)
                                .Sum(value => value.Quantity),
                            ["bufferInjected"] = false, ["hungerInjected"] = false, ["trainingEligible"] = false
                        }.ToString());
                        Require(mealReady || alreadyConsumed,
                            "NutritionMealDeliveryIncomplete: " + consumables.LastMealDeliveryProbeDetail
                            + ";request=" + consumables.LastMealDeliveryRequestFailure);
                        if (alreadyConsumed) continue;
                        // A current primitive self-care owner may be finishing
                        // a real meal. Do not commit another selected owner beside
                        // it. Observe within the existing delivery bound and then
                        // recheck real nutrition before staging a registered visit.
                        while (!ResidentMealConsumed() && actor.Brain.IsExternallyDrivenActionActive
                               && clock.Time - deliveryStartedGame < needBalance.DayLengthSeconds * 2f
                               && Time.realtimeSinceStartup - deliveryStartedReal < 60f)
                        {
                            observe();
                            yield return null;
                        }
                        if (ResidentMealConsumed()) continue;
                        Require(!actor.Brain.IsExternallyDrivenActionActive,
                            "Nutrition resident's actual external self-care did not finish within the existing delivery bound.");
                        phase = "skill-normal-registered-meal-use";
                        var visit = new CommandWitness
                        {
                            command = "controlled-public-off-duty-cycle->registered-meal-use",
                            actorId = actor.Identity.PersistentId, facilityId = meal.RequirePersistentInstanceId().Value,
                            usesBefore = meal.FacilityState.completedUses,
                            activeUseCheckpointFile = $"skill-nutrition-active-world-{witness.meals.Count + 1:D3}.json"
                        };
                        witness.meals.Add(visit);
                        bool wasOffDuty = actor.GetAbility<AbilityWork>().IsOffDuty;
                        try
                        {
                            yield return CompleteTrainingThroughRegisteredAi(actor, meal, visit, observe: observe,
                                mealVisit: true, recordRetryableMealSupplyCancellation: true);
                            Require(visit.retryableMealSupplyCancelled || visit.actualMealSupersededUse && ResidentMealConsumed()
                                || nutrition.GetRecentMeals().Any(value => value.characterId == visit.actorId
                                    && value.facilityId == visit.facilityId && value.day >= day && value.amount > 0),
                                "Completed dining use did not record this resident's actual consumed meal.");
                        }
                        finally
                        {
                            actor.GetAbility<AbilityWork>().SetDutyState(wasOffDuty ? AbilityWork.DutyState.OffDuty : AbilityWork.DutyState.OnDuty);
                        }
                        if (visit.retryableMealSupplyCancelled)
                        {
                            Require(visit.exactCancelledUseParentBound && ++supplyRaceRetries <= 2,
                                "Normal meal supply race did not resolve within three separately recorded attempts.");
                            // The producer explicitly retired this operation with
                            // meal-supply-retry. Preserve it as Cancelled, then use
                            // ordinary supply/admission to author a NEW attempt.
                            // Never promote its empty physical meal to completion.
                            continue;
                        }
                        break;
                    }
                }
                Time.maximumDeltaTime = mechanic.calendarMaximumDeltaTime;
                // Meal staging can lawfully cross midnight. Recovery belongs
                // to the feeding day just reached, not the day captured before
                // physical deliveries and automatic hunger progression.
                int recoveryDay = calendar.Day;
                float startedGame = clock.Time, startedReal = Time.realtimeSinceStartup;
                while (calendar.Day == recoveryDay && clock.Time - startedGame < 181f
                       && Time.realtimeSinceStartup - startedReal < 120f)
                {
                    observe();
                    yield return null;
                }
                Require(calendar.Day > recoveryDay, "Actual calendar did not complete the bounded nutrition recovery day.");
                SurvivalFoodOverview overview = food.GetOverview();
                witness.after = survival.Capture();
                witness.endingDay = calendar.Day;
                witness.pressureReleased = overview.DiseaseRisk <= 50f && overview.UntreatedCount == 0
                    && witness.after.consecutiveFoodShortageDays == 0;
                WriteNew($"skill-nutrition-recovery-{cycle + 1:D3}.json", JsonUtility.ToJson(witness, true));
                if (witness.pressureReleased) break;
                Time.maximumDeltaTime = mechanic.physicalMaximumDeltaTime;
            }
            Require(witness.pressureReleased, "Actual meals/calendar did not release the saved medical admission pressure.");
        }
        finally
        {
            Time.maximumDeltaTime = mechanic.physicalMaximumDeltaTime;
            if (witness.publicHaulAssigned)
            {
                if (hauler.Brain.HasRunningAction && hauler.Brain.bestAction?.actionset is AIHaul)
                    Require(hauler.Brain.StopCurrentActionForReplan("End nutrition public Haul assignment"),
                        "Could not release actual nutrition Haul before priority restoration.");
                haulWork.SetWorkPriority(BuiltInWorkTypeIds.Haul, oldHaul);
            }
            witness.haulPriorityRestored = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul) == oldHaul;
            Require(witness.haulPriorityRestored, "Nutrition public Haul priority restoration failed.");
            haulWork.SetDutyState(haulerWasOffDuty ? AbilityWork.DutyState.OffDuty : AbilityWork.DutyState.OnDuty);
            witness.haulDutyRestored = haulWork.IsOffDuty == haulerWasOffDuty;
            Require(witness.haulDutyRestored, "Nutrition public Haul duty restoration failed.");
            WriteNew("skill-nutrition-witness.json", JsonUtility.ToJson(witness, true));
        }
    }

    private IEnumerator RunSkillMechanicWitness(string restoredChosenSkillId = null)
    {
        SkillMechanicWitness witness = new()
        {
            actorId = owner.Identity.PersistentId,
            skillId = restoredChosenSkillId ?? report.skillSelection.skillId,
            researchId = OffenseExpeditionAccessRules.RequiredResearchId,
            preparationInstantWorkFixture = skillPreparationInstantWorkFixture,
            naturalPreparationWorkPassClaimed = false
        };
        report.skillMechanic = witness;
        BlueprintResearchRuntime research = UnityEngine.Object.FindFirstObjectByType<BlueprintResearchRuntime>();
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        IGameClock gameClock = scope.Container.Resolve<IGameClock>();
        IShopFeatureQueryService shopQuery = scope.Container.Resolve<IShopFeatureQueryService>();
        IShopFeatureCommandService shopCommands = scope.Container.Resolve<IShopFeatureCommandService>();
        IResearchBlueprintArchiveQuery archives = scope.Container.Resolve<IResearchBlueprintArchiveQuery>();
        IBlueprintResearchWorkService researchWork = scope.Container.Resolve<IBlueprintResearchWorkService>();
        Require(research != null, "Normal expedition admission research authority is missing.");
        Require(calendar.IsRunning, "Normal shop progression requires the running production calendar.");
        Require(research.ProjectCatalog.TryGet(witness.researchId, out ResearchProjectSO rootProject),
            "Normal expedition admission research definition is missing.");
        Require(CharacterWorkRoleUtility.TryGetWork(owner, out AbilityWork worker),
            "Selected skill actor has no actual research/haul command recipient.");
        List<ResearchProjectSO> required = new();
        void Visit(ResearchProjectSO project)
        {
            if (required.Contains(project)) return;
            foreach (ResearchProjectSO dependency in project.Prerequisites) Visit(dependency);
            required.Add(project);
        }
        Visit(rootProject);
        FacilityBlueprintSO[] blueprints = required
            .Where(value => value.BlueprintRule == ResearchBlueprintRule.Required)
            .Select(value => value.Blueprint).Distinct().ToArray();
        Require(blueprints.All(value => value != null), "Required physical blueprint definition is missing.");
        witness.startingDay = calendar.Day;
        float startedReal = Time.realtimeSinceStartup, startedGame = gameClock.Time;
        float nextStabilize = startedGame;
        float lastProgressGame = startedGame, lastProgressReal = startedReal;
        string[] neutralResidentIds = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .Where(value => value != null && !value.IsDead
                && (value.Role == CharacterRole.Owner || CharacterWorkRoleUtility.IsWorker(value)))
            .Select(value => value.Identity.PersistentId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        Require(neutralResidentIds.Length == 3,
            "Skill admission requires the original three residents for its approved non-target needs fixture.");
        IEnvironmentalFieldPersistence fields = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        DungeonEnvironmentalFieldSaveData neutral = fields.Capture();
        neutral.cells.Clear();
        for (int y = 0; y < neutral.height; y++)
        for (int x = 0; x < neutral.width; x++)
            neutral.cells.Add(new EnvironmentalCellSaveData
            { x = x, y = y, temperatureC = 22f, airQuality = 100f, lightLevel = 100f });
        void Stabilize()
        {
            CharacterActor[] actors = scope.Container.Resolve<ICharacterWorldQuery>()
                .Characters.Where(value => value != null && !value.IsDead
                    && neutralResidentIds.Contains(value.Identity.PersistentId)).ToArray();
            Require(actors.Length == neutralResidentIds.Length,
                "The approved skill admission fixture lost an original resident.");
            Require(!owner.IsDead, "Selected staff died during the normal admission prerequisite.");
            StabilizeConstructionActors(actors, scope.Container.Resolve<ICharacterDeprivationCommand>(),
                fields, neutral, scope.Container.Resolve<ICharacterEnvironmentPersistence>(),
                scope.Container.Resolve<ICharacterEnvironmentWorkContext>(),
                restoreHunger: false, approvedSupportNeedsOnly: true);
            witness.stabilizationPasses++;
            nextStabilize = gameClock.Time + 5f;
        }
        void Observe()
        {
            witness.observedFrames++;
            witness.elapsedGameSeconds = gameClock.Time - startedGame;
            witness.elapsedRealSeconds = Time.realtimeSinceStartup - startedReal;
            if (gameClock.Time > lastProgressGame)
            {
                lastProgressGame = gameClock.Time;
                lastProgressReal = Time.realtimeSinceStartup;
            }
            Require(Time.realtimeSinceStartup - lastProgressReal < 30f,
                "Normal admission simulation is not advancing despite observed frames.");
            if (gameClock.Time >= nextStabilize) Stabilize();
            if (skillRecruitmentHistoryCapacityFixture)
                MaintainControlledSupportPreparationNeeds();
            if (skillNonTargetHungerFixture)
                MaintainNonTargetSkillPreparationHunger();
        }
        worker.ClearPriorityWorkTarget();
        worker.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
        worker.SetWorkPriority(BuiltInWorkTypeIds.Research, WorkPriorityLevel.Off);
        Stabilize();
        if (skillNonTargetHungerFixture) MaintainNonTargetSkillPreparationHunger();
        WriteNew("skill-support-neutral-needs-scope.json", new JObject
        {
            ["origin"] = "approved isolated prerequisite fixture; not natural or training evidence",
            ["residentIds"] = new JArray(neutralResidentIds),
            ["maintainedNeeds"] = new JArray("THIRST", "SLEEP", "EXCRETION", "HYGIENE"),
            ["hungerInjected"] = skillRecruitmentHistoryCapacityFixture || skillNonTargetHungerFixture, ["healthInjected"] = false,
            ["funOrBreakdownReset"] = skillRecruitmentHistoryCapacityFixture || skillNonTargetHungerFixture, ["actorExposureReset"] = false,
            ["breakdownReset"] = false,
            ["additionalPreparationNeeds"] = skillRecruitmentHistoryCapacityFixture
                ? new JArray("HUNGER", "FUN") : skillNonTargetHungerFixture ? new JArray("HUNGER", "FUN") : new JArray(),
            ["dailyNutritionCalculationHeldDuringNonTargetPreparation"] = skillNonTargetHungerFixture,
            ["additionalPreparationScope"] = "human-authorized non-target support test prerequisites; not meal consumption, mood recovery, completed labor or skill healing",
            ["neutralFieldInputRetained"] = true,
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
        }.ToString());
        // Default replay retains actual meal care. The separately declared
        // non-target hunger opt-in is test preparation, never meal evidence.
        // Nutrition still owns stock/purchases/deliveries and admission inputs.
        // This opt-in explicitly holds the unrelated daily meal calculation
        // while preparing; no meal, shortage, health, labor or admission DTO is
        // fabricated/reset, and the hold ends before the battle witness.
        gameSpeed.SetSpeed(5);
        gameSpeed.SetPaused(false);
        Require(timeScale.Scale == gameSpeed.Speed && !gameSpeed.IsPaused,
            "Nutrition preparation must retain the public game-speed clock authority.");
        Time.maximumDeltaTime = witness.calendarMaximumDeltaTime;
        yield return PrepareSkillRecruitmentNutrition(witness, Observe);
        // This selected resident owns Craft and Research. Other residents keep
        // ordinary autonomous care; do not force their unrelated leisure visits.
        yield return RecoverSupportWorkFunThroughNormalVisits(new[] { owner }, Observe);
        if (skillNormalWeaponPreparation)
        {
            // Wooden spears have no research prerequisite. Prepare their real
            // craft/haul/equip path first; a separate admission failure must not
            // prevent observing the explicitly approved bench and weapon path.
            CharacterActor[] residents = scope.Container.Resolve<ICharacterWorldQuery>().Characters
                .Where(actor => actor != null && !actor.IsDead
                    && neutralResidentIds.Contains(actor.Identity.PersistentId)).ToArray();
            Require(residents.Length == 3, "Normal pre-admission weapons require the original three residents.");
            yield return PrepareNormalSupportWeapons(residents, Observe, neutralResidentIds,
                "skill-normal-resident-weapons");
            skillResidentWeaponsPreparedBeforeAdmission = true;
        }
        if (OffenseExpeditionAccessRules.IsUnlocked(research.State))
        {
            // A complete official input can already own the institution's
            // admission research. Do not repurchase/research or claim new work.
            witness.researchPrerequisitesAlreadyComplete = true;
            witness.researchCompleted = true;
            witness.completedResearchProjects = required.Where(value => research.State.Projects.IsCompleted(value.ProjectId))
                .Select(value => value.ProjectId.Value).ToList();
            Require(witness.completedResearchProjects.Count == required.Count,
                "Previously admitted expedition lacks authoritative prerequisite research.");
            witness.consumerPath = "existing authoritative research admission -> registered expedition -> public battle Ability command";
            gameSpeed.SetSpeed(5);
            worker.ClearPriorityWorkTarget();
            yield return RunSkillBattleMechanicWitness(witness, Observe);
            yield break;
        }
        // Speed is the same public 1..5 command as the player. Midnight and shop
        // refresh must be driven by the actual calendar, not SetDateTime/events.
        // Existing isolated WorkTaskExecutor fixtures use this bounded cap:
        // public speed five advances at most three field ticks per frame.
        gameSpeed.SetSpeed(5);
        Time.maximumDeltaTime = witness.calendarMaximumDeltaTime;
        gameSpeed.SetPaused(false);
        // Normal meals and weapon preparation can span several days. The
        // existing four-transition bound belongs to actual blueprint waiting,
        // not those already witnessed preparation phases.
        witness.blueprintWaitStartingDay = calendar.Day;
        foreach (FacilityBlueprintSO blueprint in blueprints)
        {
            if (archives.GetStatus(blueprint).IsArchived) continue;
            phase = "skill-normal-blueprint-shop-wait";
            ShopFeatureOfferRow offer = null;
            while (calendar.Day <= witness.blueprintWaitStartingDay + 4)
            {
                offer = shopQuery.Capture().DailyOffers.FirstOrDefault(value =>
                    value.OfferTypeId == FacilityShopOfferTypeIds.Blueprint
                    && value.DataId == blueprint.id && !value.IsPurchased);
                if (offer != null) break;
                Observe();
                yield return null;
            }
            Require(offer != null, "Required blueprint was not offered within four real calendar transitions: " + blueprint.id);
            gameSpeed.SetPaused(true);
            ShopFeatureSurfaceModel beforePurchase = shopQuery.Capture();
            SkillBlueprintPurchaseWitness purchase = new()
            {
                blueprintId = blueprint.id, day = calendar.Day, cost = offer.Cost,
                purchaseKey = offer.PurchaseKey, moneyBefore = beforePurchase.CurrentMoney
            };
            witness.purchases.Add(purchase);
            ShopFeatureCommandResult bought = shopCommands.PurchaseDaily(offer.Index, offer.PurchaseKey);
            purchase.purchased = bought.Succeeded;
            witness.blueprintPurchased = witness.purchases.All(value => value.purchased);
            purchase.message = bought.Message;
            purchase.moneyAfter = shopQuery.Capture().CurrentMoney;
            Require(purchase.purchased && purchase.moneyBefore - purchase.moneyAfter == purchase.cost,
                "Public physical blueprint purchase/payment failed: " + purchase.message);
            string afterPurchaseHash = SkillWorldStateHash(saves.ToJson(saves.Capture()));
            // The UI command may report an idempotent success; domain state and
            // payment must remain unchanged on a repeated purchase key.
            shopCommands.PurchaseDaily(offer.Index, offer.PurchaseKey);
            purchase.duplicateStable = afterPurchaseHash == SkillWorldStateHash(saves.ToJson(saves.Capture()));
            Require(purchase.duplicateStable, "Repeated blueprint purchase changed payment/delivery/world state.");
            Require(archives.TryGetPreferredArchive(blueprint, out _, out string archiveDestination),
                "Authored world has no eligible physical blueprint archive.");
            purchase.archiveDestination = archiveDestination;
            witness.purchaseDay = purchase.day;
            phase = "skill-normal-blueprint-haul";
            owner.Brain.ClearPathSearchCache();
            // r5's three game seconds/frame exhausted a movement bound before
            // enough coroutine steps. r2 completed the same item in 67 actual
            // frames / 33.38 game seconds with this 0.5-game-second frame step.
            // Keep the real AI path and 90-game-second bound unchanged.
            Time.maximumDeltaTime = witness.physicalMaximumDeltaTime;
            gameSpeed.SetPaused(false);
            float haulStarted = gameClock.Time;
            while (!archives.GetStatus(blueprint).IsArchived && gameClock.Time - haulStarted < 90f)
            {
                Observe();
                yield return null;
                purchase.haulFrames++;
                purchase.haulGameSeconds = gameClock.Time - haulStarted;
            }
            purchase.archived = archives.GetStatus(blueprint).IsArchived;
            Require(purchase.archived, "Purchased blueprint did not reach its real archive via Haul: "
                + archives.GetStatus(blueprint).Blocker + "; brain=" + owner.Brain.CurrentActionDebugLabel
                + "/" + owner.Brain.CurrentActionPhaseDetail);
            Time.maximumDeltaTime = witness.calendarMaximumDeltaTime;
        }
        witness.blueprintPurchased = witness.purchases.Count > 0 && witness.purchases.All(value => value.purchased);
        witness.blueprintArchived = blueprints.All(value => archives.GetStatus(value).IsArchived);
        gameSpeed.SetPaused(true);
        HashSet<string> beforeResearchKeys = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        string[] pendingResearchProjects = required.Where(value => !research.State.Projects.IsCompleted(value.ProjectId))
            .Select(value => value.ProjectId.Value).ToArray();
        string[] eligibleResearcherIds = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .Where(value => value != null && !value.IsDead && CharacterWorkRoleUtility.TryGetWork(value, out _))
            .Select(value => value.Identity.PersistentId).ToArray();
        if (!OffenseExpeditionAccessRules.IsUnlocked(research.State))
        {
            phase = "skill-normal-research";
            bool restoredQueue = !string.IsNullOrEmpty(restoredChosenSkillId)
                && research.State.Projects.Queue.Any(entry => entry.ProjectId.Value == rootProject.ProjectId.Value);
            if (restoredQueue)
            {
                Require(research.State.Projects.Queue.Count(entry =>
                            entry.ProjectId.Value == rootProject.ProjectId.Value) == 1
                        && research.State.Projects.Queue.All(entry => pendingResearchProjects.Contains(entry.ProjectId.Value))
                        && (!research.State.Projects.ActiveProjectId.IsValid
                            || pendingResearchProjects.Contains(research.State.Projects.ActiveProjectId.Value)),
                    "Official resumed research queue has duplicated or unrelated project owners.");
                witness.researchQueueAlreadyOwned = true;
            }
            else
            {
                ResearchQueueCommandResult queued = research.EnqueueProject(rootProject.ProjectId);
                Require(queued.Succeeded, "Normal physical-blueprint research queue rejected: " + queued.Message);
            }
            ConfigureSkillResearchRole(worker);
            Require(owner.TryGetAbility(out AbilityMove researchMover), "Selected research actor has no movement authority.");
            BuildableObject desk = scope.Container.Resolve<IBuildingWorldQuery>().Buildings
                .FirstOrDefault(value => value != null && !value.isDestroy && researchWork.HasResearchWorkFor(value));
            Require(desk != null, "Authored research facility has no admitted actual work.");
            Require(worker.TrySetPriorityWorkTarget(desk, BuiltInWorkTypeIds.Research, null, out string rejection),
                "Public Research work command rejected: " + rejection);
            witness.publicResearchCommands++;
            owner.Brain.ClearPathSearchCache();
            float researchStarted = gameClock.Time;
            float researchStartedReal = Time.realtimeSinceStartup;
            long researchInitialRevision = worker.ApprovedWorkProgressRevisionForDiagnostics;
            float lastResearchApproval = researchStarted;
            witness.researchGameTimeBound = CalculateSkillResearchGameTimeBound(research);
            // Preserve actual work/intent observations in the one final report.
            // A saved treatment log alone cannot explain r16's stalled work.
            var researchTrace = new SkillResearchDiagnostics
            {
                actorId = owner.Identity.PersistentId,
                preparationInstantWorkFixture = nonTargetInstantWorkHold != null,
                naturalWorkPassClaimed = false,
                researchControlOrigin = nonTargetInstantWorkHold != null
                    ? "isolated InstantWork preparation through public commands; not natural work proof"
                    : "simulated-player-public-work-commands",
                initialWork = research.State.Projects.ProgressById.Values.Sum(value => value.Progress),
                researchGameTimeBound = witness.researchGameTimeBound,
                controlledNeutralFixture = witness.controlledNeutralPrerequisiteFixture
            };
            report.skillResearchDiagnostics = researchTrace;
            Time.maximumDeltaTime = witness.physicalMaximumDeltaTime;
            gameSpeed.SetPaused(false);
            while (!OffenseExpeditionAccessRules.IsUnlocked(research.State)
                && gameClock.Time - researchStarted < witness.researchGameTimeBound)
            {
                Observe();
                if (CharacterNeedAiThresholds.GetRoutineUtility(owner, CharacterCondition.HUNGER) > 0f
                    || worker.RequiresRoutineNeedRecovery(CharacterCondition.HUNGER))
                {
                    int mealAttemptsBeforeCare = supportWorkMealVisitSequence;
                    yield return RecoverSupportWorkHungerThroughNormalMeals(owner, Observe);
                    // Only a separately evidenced actual meal is care progress;
                    // an empty helper return cannot hide stalled Research.
                    if (supportWorkMealVisitSequence > mealAttemptsBeforeCare)
                        lastResearchApproval = gameClock.Time;
                }
                if (owner.Stats.TryGetConditionValue(CharacterCondition.FUN, out float fun)
                    && fun < owner.Stats.GetNeedResponse(CharacterCondition.FUN).routineStart)
                {
                    yield return RecoverSupportWorkFunThroughNormalVisits(new[] { owner }, Observe);
                    // A separately evidenced real care phase is not stalled
                    // research. The overall finite research budget is unchanged.
                    lastResearchApproval = gameClock.Time;
                }
                if (IssueNextSkillResearchCommandWhenIdle(worker, desk))
                    witness.publicResearchCommands++;
                bool researching = worker.AssignedWorkTypeId == BuiltInWorkTypeIds.Research;
                long beforeTick = worker.ApprovedWorkProgressRevisionForDiagnostics;
                SetSkillResearchClock(worker, researchMover, witness.physicalMaximumDeltaTime, witness.calendarMaximumDeltaTime);
                yield return null;
                if (researching)
                {
                    witness.approvedResearchTicks += worker.ApprovedWorkProgressRevisionForDiagnostics - beforeTick;
                    if (worker.ApprovedWorkProgressRevisionForDiagnostics > beforeTick)
                        lastResearchApproval = gameClock.Time;
                }
                researchTrace.gameSeconds = gameClock.Time - researchStarted;
                researchTrace.realSeconds = Time.realtimeSinceStartup - researchStartedReal;
                researchTrace.approvedTicks = worker.ApprovedWorkProgressRevisionForDiagnostics - researchInitialRevision;
                researchTrace.approvedResearchTicks = witness.approvedResearchTicks;
                researchTrace.publicResearchCommands = witness.publicResearchCommands;
                researchTrace.finalWork = research.State.Projects.ProgressById.Values.Sum(value => value.Progress);
                Vector2Int researchPosition = owner.GetNowXY();
                string activeResearchProject = research.State.Projects.ActiveProjectId.Value;
                researchTrace.frames.Add(new SkillResearchFrame
                {
                    frame = researchTrace.frames.Count + 1, x = researchPosition.x, y = researchPosition.y,
                    gameSeconds = researchTrace.gameSeconds, realSeconds = researchTrace.realSeconds,
                    deltaTime = gameClock.DeltaTime,
                    maximumDeltaTime = Time.maximumDeltaTime,
                    moving = researchMover.HasActiveMovementRoutineForDiagnostics,
                    totalWork = researchTrace.finalWork, activeProject = activeResearchProject,
                    activeWork = research.State.Projects.ProgressById.TryGetValue(activeResearchProject ?? string.Empty,
                        out var observedProgress) ? observedProgress.Progress : 0f,
                    genericCompletedWork = worker.GenericCompletedWorkForDiagnostics,
                    genericRequiredWork = worker.GenericRequiredWorkForDiagnostics,
                    completedProjects = research.State.Projects.CompletedProjectIds.Count,
                    approvedTicks = researchTrace.approvedTicks,
                    approvedResearchTicks = witness.approvedResearchTicks,
                    workRunId = worker.ActiveWorkRunIdForDiagnostics, working = worker.isWorking,
                    priorityTarget = worker.PriorityWorkTarget != null,
                    assignedWork = worker.AssignedWorkTypeId.Value, offDuty = worker.IsOffDuty,
                    brain = owner.Brain.CurrentActionDebugLabel, brainPhase = owner.Brain.CurrentActionPhaseDetail,
                    preWuExit = worker.LastPreWuExitKindForDiagnostics + ": " + worker.LastPreWuExitDetailForDiagnostics,
                    cancellation = worker.LastActiveWorkCancellationReasonForDiagnostics,
                    moveFailure = researchMover.LastGridMoveFailureReason.ToString(),
                    blocked = researchMover.LastGridMoveWasBlocked
                });
                Require(gameClock.Time - lastResearchApproval < 90f,
                    "Actual research has no approved Research work for 90 game seconds.");
            }
            researchTrace.completed = OffenseExpeditionAccessRules.IsUnlocked(research.State);
            researchTrace.completedProjects = research.State.Projects.CompletedProjectIds
                .OrderBy(value => value, StringComparer.Ordinal).ToList();
            Require(OffenseExpeditionAccessRules.IsUnlocked(research.State) && witness.approvedResearchTicks > 0,
                "Actual research did not unlock expedition: " + owner.Brain.CurrentActionDebugLabel
                + "/" + owner.Brain.CurrentActionPhaseDetail + "; active=" + research.State.Projects.ActiveProjectId.Value
                + "; worker=" + worker.LastPreWuExitDetailForDiagnostics);
        }
        gameSpeed.SetPaused(true);
        witness.researchCompleted = OffenseExpeditionAccessRules.IsUnlocked(research.State);
        witness.completedResearchProjects = required.Where(value => research.State.Projects.IsCompleted(value.ProjectId))
            .Select(value => value.ProjectId.Value).ToList();
        witness.researchOutcomes = ReadAllOutcomes().Where(value => !beforeResearchKeys.Contains(OutcomeKey(value))
            && IsCompletedResearchWorkOutcome(value)).ToList();
        Require(witness.completedResearchProjects.Count == required.Count,
            "Normal prerequisite research lacks authoritative completions.");
        RequireResearchCompletionOutcomes(pendingResearchProjects, witness.researchOutcomes, eligibleResearcherIds);
        WriteNew("u4-skill-admission-world.json", saves.ToJson(saves.Capture()));
        WriteNew("u4-skill-admission-witness.json", JsonUtility.ToJson(witness, true));
        worker.ClearPriorityWorkTarget();

        yield return RunSkillBattleMechanicWitness(witness, Observe);
    }

    private void BeginNaturalInvasionHoldFixture()
    {
        Require(((sourceV9SkillReplay || SkillSupportContinuation) && skillReplayHoldNaturalInvasion
                || completedCraftDeliveryDiagnostics) && Application.isBatchMode
                && DungeonPersistencePaths.IsIsolatedVerification,
            "Natural invasion hold requires its explicit isolated replay option.");
        heldInvasionDirector = UnityEngine.Object.FindObjectsByType<InvasionDirectorRuntime>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).SingleOrDefault();
        Require(heldInvasionDirector != null && heldInvasionDirector.isActiveAndEnabled,
            "Natural invasion hold requires exactly one active production spawn consumer.");
        invasionDirectorWasEnabled = heldInvasionDirector.enabled;
        heldInitialInvasionIds = heldInvasionDirector.ActiveIntruders.Select(value => value.RuntimeId)
            .ToHashSet(StringComparer.Ordinal);
        heldInvasionCandidateObservation = scope.Container.Resolve<IGameEventBus>()
            .Subscribe<InvasionCandidateEvent>(_ => report.naturalCandidateEventsObservedWhileHeld++);
        heldInvasionStartedObservation = scope.Container.Resolve<IGameEventBus>()
            .Subscribe<InvasionStartedEvent>(_ => report.newInvasionStartEventsWhileHeld++);
        try { heldInvasionDirector.enabled = false; }
        finally { report.naturalInvasionHoldApplied = !heldInvasionDirector.enabled; }
        Require(report.naturalInvasionHoldApplied && heldInitialInvasionIds.SetEquals(
                heldInvasionDirector.ActiveIntruders.Select(value => value.RuntimeId)),
            "Natural invasion fixture must not remove or replace any existing intruder.");
        report.initialSetup += "; explicit controlled fixture holds new natural candidate spawns, not natural-play evidence";
        WriteNew("skill-natural-invasion-hold-start.json", new JObject
        {
            ["origin"] = "explicit-isolated-test-fixture-new-natural-candidate-spawns-held",
            ["directorWasEnabled"] = invasionDirectorWasEnabled,
            ["directorEnabled"] = heldInvasionDirector.enabled,
            ["existingInvasionIds"] = new JArray(heldInitialInvasionIds.OrderBy(value => value, StringComparer.Ordinal)),
            ["existingIntrudersChanged"] = false, ["threatCalculationPaused"] = false,
            ["candidateEventsReplayedAfterFixture"] = false,
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
        }.ToString());
    }

    private void EndNaturalInvasionHoldFixture()
    {
        if (heldInvasionDirector == null) return;
        try
        {
            string[] finalIds = heldInvasionDirector.ActiveIntruders.Select(value => value.RuntimeId)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            string[] unexpected = finalIds.Where(value => !heldInitialInvasionIds.Contains(value)).ToArray();
            heldInvasionDirector.enabled = invasionDirectorWasEnabled;
            report.naturalInvasionDirectorRestored = heldInvasionDirector.enabled == invasionDirectorWasEnabled;
            Require(report.naturalInvasionDirectorRestored && unexpected.Length == 0
                    && report.newInvasionStartEventsWhileHeld == 0,
                "Natural invasion fixture restoration failed or a new invasion bypassed the held consumer.");
            WriteNew("skill-natural-invasion-hold-ended.json", new JObject
            {
                ["origin"] = "controlled-test-fixture-ended-not-natural-play-evidence",
                ["directorRestored"] = report.naturalInvasionDirectorRestored,
                ["candidateEventsObservedWhileHeld"] = report.naturalCandidateEventsObservedWhileHeld,
                ["newInvasionStartEventsWhileHeld"] = report.newInvasionStartEventsWhileHeld,
                ["remainingExistingInvasionIds"] = new JArray(finalIds),
                ["unexpectedNewInvasionIds"] = new JArray(unexpected),
                ["candidateEventsReplayedAfterFixture"] = false,
                ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
            }.ToString());
        }
        catch (Exception exception)
        {
            report.errors.Add("Natural invasion fixture cleanup failed: " + exception);
        }
        finally
        {
            heldInvasionCandidateObservation?.Dispose();
            heldInvasionCandidateObservation = null;
            heldInvasionStartedObservation?.Dispose();
            heldInvasionStartedObservation = null;
        }
    }

    private void EnsureSkillRecruitmentFoodSupply(RegularCustomerRuntime customers)
    {
        // Construction consumes real meals. Recheck its physical supply after
        // the last bed, not only before construction. This is a public purchase
        // fixture, never a changed admission threshold or invented food.
        Require(customers.ImmigrationPolicy == SettlementImmigrationPolicy.Open,
            "Support recruitment food preparation requires the declared Open policy.");
        const string mealItem = "food:preserved-ration";
        const int preparationSupplyDays = 10; // Open requires seven; retain a real admission/travel margin.
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        IWarehouseFeatureQueryService supply = scope.Container.Resolve<IWarehouseFeatureQueryService>();
        SettlementPopulationCapacitySnapshot before = customers.CapturePopulationCapacity();
        int Quantity() => items.GetAllStacks().Where(value => value != null && value.ItemId == mealItem)
            .Sum(value => value.Quantity);
        int quantityBefore = Quantity();
        WarehouseFeatureSurfaceModel current = supply.Capture();
        int moneyBefore = current.CurrentMoney;
        int purchasedAmount = 0, paidAmount = 0;
        GameplayOutcomeSnapshot purchaseOutcome = null;
        string purchaseMessage = "Existing physical food coverage is adequate; no purchase executed.";
        if (before.FoodSupplyDays < preparationSupplyDays)
        {
            // The public forecast includes all authored food, not just rations.
            // Its rounded-down days give a conservative shortage for this one
            // authored daily offer; never fabricate a larger offer or quantity.
            int deficit = checked(before.ResidentCount * (preparationSupplyDays - before.FoodSupplyDays));
            Require(deficit > 0, "Post-housing resident count and food forecast disagree.");
            StockDeliveryOffer offer = current.DeliveryOffers.Where(value => value.Offer.itemId == mealItem
                    && value.Offer.amount >= deficit && value.Offer.cost <= moneyBefore)
                .OrderBy(value => value.Offer.cost).Select(value => value.Offer).FirstOrDefault();
            Require(offer.itemId == mealItem && offer.amount >= deficit && offer.cost <= moneyBefore,
                "RecruitmentFoodSupplyUnavailable: no adequate affordable authored delivery offer.");
            string ownerIdentity = "stock-supply:warehouse:day=" + calendar.Day + ":category=" + offer.category
                + ":item=" + offer.itemId.Trim() + ":amount=" + offer.amount + ":source=" + (offer.sourceLabel?.Trim() ?? string.Empty);
            string producerId = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.StockSupplyResult).ProducerId;
            string ownerKey = producerId + "|" + ownerIdentity;
            long previousRevision = scope.Container.Resolve<IMigratedProducerOutcomePersistence>().Capture().owners
                .SingleOrDefault(value => value.ownerKey == ownerKey)?.revision ?? 0L;
            long expectedRevision = checked(previousRevision + 1L);
            string operationId = MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.StockSupplyResult, ownerIdentity, expectedRevision).OperationId.Value;
            var beforeKeys = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
            WarehouseFeatureCommandResult purchase = scope.Container.Resolve<IWarehouseFeatureCommandService>()
                .PurchaseDelivery(offer);
            purchaseMessage = purchase.Message;
            Require(purchase.Succeeded && Quantity() == quantityBefore + offer.amount
                && supply.Capture().CurrentMoney == moneyBefore - offer.cost,
                "Post-housing actual food payment/physical delivery failed: " + purchase.Message);
            GameplayOutcomeSnapshot[] exact = ReadAllOutcomes().Where(value => !beforeKeys.Contains(OutcomeKey(value))
                && MatchesStockSupplyOutcome(value, producerId, operationId, expectedRevision)).ToArray();
            WriteNew("skill-recruitment-food-purchase-identity.json", new JObject
            {
                ["ownerIdentity"] = ownerIdentity, ["ownerKey"] = ownerKey,
                ["previousRevision"] = previousRevision, ["expectedRevision"] = expectedRevision,
                ["canonicalOperationId"] = operationId, ["exactCount"] = exact.Length,
                ["quantityBefore"] = quantityBefore, ["quantityAfter"] = Quantity(),
                ["moneyBefore"] = moneyBefore, ["moneyAfter"] = supply.Capture().CurrentMoney,
                ["outcomes"] = JArray.FromObject(exact), ["trainingEligible"] = false
            }.ToString(Newtonsoft.Json.Formatting.Indented));
            Require(exact.Length == 1 && exact[0].status == GameplayOutcomeStatus.Succeeded
                && exact[0].publishedRevision > 0, "Actual recruitment food purchase lacks one exact committed outcome.");
            purchaseOutcome = exact[0];
            purchasedAmount = offer.amount;
            paidAmount = offer.cost;
        }
        SettlementPopulationCapacitySnapshot after = customers.CapturePopulationCapacity();
        WriteNew("skill-recruitment-food-supply.json", new JObject
        {
            ["origin"] = "post-housing public warehouse PurchaseDelivery and physical stack observation",
            ["policy"] = customers.ImmigrationPolicy.ToString(), ["preparationSupplyDays"] = preparationSupplyDays,
            ["coverageBefore"] = before.FoodSupplyDays, ["coverageAfter"] = after.FoodSupplyDays,
            ["quantityBefore"] = quantityBefore, ["quantityAfter"] = Quantity(),
            ["moneyBefore"] = moneyBefore, ["moneyAfter"] = supply.Capture().CurrentMoney,
            ["purchasedAmount"] = purchasedAmount, ["paidAmount"] = paidAmount, ["message"] = purchaseMessage,
            ["outcome"] = purchaseOutcome == null ? JValue.CreateNull() : JObject.Parse(JsonUtility.ToJson(purchaseOutcome)),
            ["foodInjected"] = false, ["thresholdChanged"] = false, ["trainingEligible"] = false
        }.ToString(Newtonsoft.Json.Formatting.Indented));
        Require(after.FoodSupplyDays >= preparationSupplyDays,
            "Post-housing food forecast remains below its declared preparation supply.");
        WriteNew("skill-recruitment-food-supply-world.json", saves.ToJson(saves.Capture()));
    }

    private static bool MatchesStockSupplyOutcome(GameplayOutcomeSnapshot value, string producerId,
        string operationId, long revision) => value.outcomeTypeId == "inventory.stock-supplied"
        && value.producerId == producerId && value.operationId == operationId
        && value.commitRevision == revision && value.ownerRevision == revision
        && value.localResultIndex == 0 && value.participants.Count == 1
        && value.participants.Any(participant => participant.entityId == operationId
            && participant.entityKindId == MigratedProducerOutcomeIds.OperationKind.Value
            && participant.roleId == MigratedProducerOutcomeIds.OperationRole.Value);

    // Replays the immutable failed-run record, not a fabricated purchase or
    // a replacement PASS for that run. Shares the actual campaign predicate.
    public static void VerifyRecruitmentFoodIdentityFromBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrEmpty(output), "Isolated evidence output is required.");
        string input = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Artifacts/QA/AuditRemediation-20260926-r1/u4-skill-staff-replay-r18/unity-evidence/incomplete-world.json"));
        const string inputSha = "6ddb9870871dcae1b4cfa09fc527bad1dd09bb38ad7664e5bd01f250a8a0e7a1";
        byte[] bytes = File.ReadAllBytes(input);
        Require(Hash(bytes) == inputSha, "Original failed-run purchase record changed.");
        JObject world = JObject.Parse(Encoding.UTF8.GetString(bytes));
        JObject ledger = JObject.Parse(world["sections"].Single(value =>
            (string)value["sectionId"] == "world.gameplay-outcome-ledger")["payloadJson"].Value<string>());
        GameplayOutcomeSnapshot[] outcomes = ledger["exactOutcomes"].ToObject<GameplayOutcomeSnapshot[]>();
        const string ownerIdentity = "stock-supply:warehouse:day=17:category=Food:item=food:preserved-ration:amount=25:source=운영일 납품";
        const long revision = 1;
        string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.StockSupplyResult).ProducerId;
        string operation = MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.StockSupplyResult, ownerIdentity, revision).OperationId.Value;
        GameplayOutcomeSnapshot[] exact = outcomes.Where(value =>
            MatchesStockSupplyOutcome(value, producer, operation, revision)).ToArray();
        Require(exact.Length == 1 && exact[0].sequence == 586
            && exact[0].status == GameplayOutcomeStatus.Succeeded && exact[0].publishedRevision > 0,
            "Production canonical identity must match the one actual committed purchase.");
        Require(!outcomes.Any(value => MatchesStockSupplyOutcome(value, producer, ownerIdentity, revision)),
            "Raw payment owner identity must not match a canonical ledger operation.");
        Require(!outcomes.Any(value => MatchesStockSupplyOutcome(value, producer, operation, revision + 1)),
            "Wrong owner revision must not match.");
        GameplayOutcomeSnapshot duplicatedParticipant = JsonUtility.FromJson<GameplayOutcomeSnapshot>(
            JsonUtility.ToJson(exact[0]));
        duplicatedParticipant.participants.Add(duplicatedParticipant.participants[0]);
        Require(!MatchesStockSupplyOutcome(duplicatedParticipant, producer, operation, revision),
            "Duplicated typed participant must be rejected.");
        Require(new[] { exact[0], exact[0] }.Count(value =>
            MatchesStockSupplyOutcome(value, producer, operation, revision)) != 1,
            "Duplicated exact outcome must not satisfy the single-result contract.");
        Directory.CreateDirectory(output);
        using (var stream = new FileStream(Path.Combine(output, "recruitment-food-identity-report.json"), FileMode.CreateNew))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            writer.Write(new JObject { ["passed"] = true, ["input"] = input, ["inputSha256"] = inputSha,
                ["operationId"] = operation, ["sequence"] = exact[0].sequence, ["checks"] = 5,
                ["scope"] = "saved-record identity regression only; original replay remains FAIL",
                ["trainingEligible"] = false, ["humanApprovalClaimed"] = false }.ToString());
        Debug.Log("RECRUITMENT_FOOD_CANONICAL_IDENTITY=PASS checks=5");
    }

    // Editor-only prerequisite control. No production rule, settled labor,
    // serialized world section or project asset is changed. The real capacity
    // provider still supplies every physical/health/reserve/onboarding input.
    private sealed class SupportHistoryCapacityFixture : ISettlementPopulationCapacityQuery
    {
        private readonly ISettlementPopulationCapacityQuery original;
        public int QueryCount { get; private set; }

        public SupportHistoryCapacityFixture(ISettlementPopulationCapacityQuery original)
            => this.original = original ?? throw new ArgumentNullException(nameof(original));

        public SettlementPopulationCapacitySnapshot CapturePopulationCapacity()
        {
            SettlementPopulationCapacitySnapshot value = original.CapturePopulationCapacity();
            QueryCount++;
            return new SettlementPopulationCapacitySnapshot(value.ResidentCount, value.SleepingSlotCount,
                value.VacantSleepingSlotCount, value.FoodSupplyDays, value.WaterSupplyDays,
                value.SanitationRisk, value.DiseaseRisk, value.UntreatedCount, value.AlertLevel,
                value.EmergencyReserveCoverage, Math.Max(1f, value.RollingPerCapitaNetWuIndex),
                value.LatestGuaranteedGrowthMilliWu);
        }

        public SettlementPopulationAcceptance EvaluateImmigration(SettlementImmigrationPolicy policy)
        {
            SettlementPopulationCapacitySnapshot prepared = CapturePopulationCapacity();
            return SettlementPopulationAcceptanceRules.Evaluate(in prepared, policy);
        }
    }

    private static FieldInfo HistoryCapacityField => typeof(RegularCustomerRuntime).GetField(
        "populationCapacity", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Exact recruitment capacity binding is unavailable.");

    private void BeginSupportHistoryCapacityFixture(RegularCustomerRuntime customers)
    {
        if (!skillRecruitmentHistoryCapacityFixture) return;
        Require(SkillSupportContinuation && Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && report.continuation?.officialRestore == true && customers != null,
            "Historical recruitment capacity must remain an explicitly isolated restored test.");
        if (historyCapacityFixture != null)
        {
            Require(ReferenceEquals(historyCapacityFixtureOwner, customers)
                    && ReferenceEquals(HistoryCapacityField.GetValue(customers), historyCapacityFixture),
                "Historical-capacity fixture was rebound to a different customer authority.");
            return;
        }
        ISettlementLaborPersistence labor = scope.Container.Resolve<ISettlementLaborPersistence>();
        VerifyActiveExteriorSocietyOwnerBoundary();
        string laborBefore = JsonUtility.ToJson(labor.CaptureLaborSaveData());
        string[] outcomesBefore = ReadAllOutcomes().Select(value => JsonUtility.ToJson(value)).ToArray();
        historyCapacityFixtureOwner = customers;
        historyCapacityFixtureOriginal = HistoryCapacityField.GetValue(customers) as ISettlementPopulationCapacityQuery;
        Require(historyCapacityFixtureOriginal != null, "The actual recruitment capacity provider is missing.");
        SettlementPopulationCapacitySnapshot actual = historyCapacityFixtureOriginal.CapturePopulationCapacity();
        SettlementPopulationAcceptance originalAdmission = historyCapacityFixtureOriginal.EvaluateImmigration(customers.ImmigrationPolicy);
        historyCapacityFixture = new SupportHistoryCapacityFixture(historyCapacityFixtureOriginal);
        HistoryCapacityField.SetValue(customers, historyCapacityFixture);
        SettlementPopulationCapacitySnapshot prepared = customers.CapturePopulationCapacity();
        Require(actual.ResidentCount == prepared.ResidentCount && actual.SleepingSlotCount == prepared.SleepingSlotCount
                && actual.VacantSleepingSlotCount == prepared.VacantSleepingSlotCount
                && actual.FoodSupplyDays == prepared.FoodSupplyDays && actual.WaterSupplyDays == prepared.WaterSupplyDays
                && actual.SanitationRisk == prepared.SanitationRisk && actual.DiseaseRisk == prepared.DiseaseRisk
                && actual.UntreatedCount == prepared.UntreatedCount && actual.AlertLevel == prepared.AlertLevel
                && actual.EmergencyReserveCoverage == prepared.EmergencyReserveCoverage
                && actual.LatestGuaranteedGrowthMilliWu == prepared.LatestGuaranteedGrowthMilliWu
                && laborBefore == JsonUtility.ToJson(labor.CaptureLaborSaveData())
                && outcomesBefore.SequenceEqual(ReadAllOutcomes().Select(value => JsonUtility.ToJson(value))),
            "Test history projection changed a physical prerequisite, settled labor or canonical outcome.");
        WriteNew("skill-recruitment-history-capacity-fixture-start.json", new JObject
        {
            ["origin"] = "human-authorized Editor-only test prerequisite; not natural admission or completed labor",
            ["actualCapacity"] = JObject.FromObject(actual), ["preparedCapacity"] = JObject.FromObject(prepared),
            ["unmodifiedAdmission"] = JObject.FromObject(originalAdmission),
            ["laborAndCanonicalUnchanged"] = true, ["productionRulesChanged"] = false,
            ["serializedWorldChangedByProjection"] = false, ["trainingEligible"] = false,
            ["naturalPlayClaimed"] = false, ["humanApprovalClaimed"] = false
        }.ToString(Newtonsoft.Json.Formatting.Indented));
    }

    private void EndSupportHistoryCapacityFixture()
    {
        if (historyCapacityFixture == null) return;
        object current = HistoryCapacityField.GetValue(historyCapacityFixtureOwner);
        Require(ReferenceEquals(current, historyCapacityFixture) || ReferenceEquals(current, historyCapacityFixtureOriginal),
            "A foreign capacity provider replaced the owned test fixture.");
        HistoryCapacityField.SetValue(historyCapacityFixtureOwner, historyCapacityFixtureOriginal);
        Require(ReferenceEquals(HistoryCapacityField.GetValue(historyCapacityFixtureOwner), historyCapacityFixtureOriginal),
            "The original recruitment provider was not restored.");
        WriteNew("skill-recruitment-history-capacity-fixture-ended.json", new JObject
        {
            ["origin"] = "restore exact original production provider after explicit test prerequisite",
            ["queryCount"] = historyCapacityFixture.QueryCount, ["originalProviderRestored"] = true,
            ["unmodifiedAdmission"] = JObject.FromObject(historyCapacityFixtureOriginal.EvaluateImmigration(
                historyCapacityFixtureOwner.ImmigrationPolicy)),
            ["productionRulesChanged"] = false, ["trainingEligible"] = false, ["naturalPlayClaimed"] = false
        }.ToString(Newtonsoft.Json.Formatting.Indented));
        historyCapacityFixture = null;
        historyCapacityFixtureOwner = null;
        historyCapacityFixtureOriginal = null;
    }

    private void VerifyActiveExteriorSocietyOwnerBoundary()
    {
        var isolated = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(),
            scope.Container.Resolve<V20StoryContentCatalog>());
        var context = new V20DailyEventContext { AbsoluteDay = 42, RunSeed = 157181, Season = Season.Spring };
        const string targetId = "character:incident:Thief:7:actor";
        context.ActiveExteriorIncidentCharacterIds.Add(targetId);
        var evidence = new ObservedBrawlIncidentSnapshot("combat-command:exterior-owner-boundary:7",
            new CharacterId(targetId), new CharacterId("character:staff:1:01"), true, 2.5f,
            new BuildingInstanceId("building:exterior-owner-boundary:facility"), new CoreGridCell(5, 3), 42);
        string before = JsonUtility.ToJson(isolated.CaptureSociety());
        for (int attempt = 0; attempt < 2; attempt++)
        {
            ObservedMealIncidentCaptureResult result = isolated.CaptureObservedBrawlIncident(evidence, context);
            Require(result.Disposition == ObservedMealIncidentCaptureDisposition.NotEligible
                    && result.IncidentKind == ServiceIncidentKind.Brawl
                    && result.Failure.Parameters.Length == 2
                    && result.Failure.Parameters[1] == "observed-service-incident-target-owned-by-active-exterior"
                    && JsonUtility.ToJson(isolated.CaptureSociety()) == before
                    && isolated.GetOwnedCustomerIds().Count == 0,
                "Active Exterior actor acquired a second Society owner or mutated Society state.");
        }
        WriteNew("active-exterior-society-owner-boundary.json", new JObject
        {
            ["origin"] = "controlled pure admission boundary; not an actual combat callback or whole-world restore",
            ["targetCharacterId"] = targetId, ["attempts"] = 2, ["societyUnchanged"] = true,
            ["secondOwnerCount"] = 0, ["strictCaptureRestoreGuardsChanged"] = false,
            ["trainingEligible"] = false, ["passed"] = true
        }.ToString());
    }

    private void MaintainControlledSupportPreparationNeeds()
    {
        Require(SkillSupportContinuation && Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
            "Non-target support preparation needs must remain isolated test input.");
        foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>().Characters
                     .Where(value => value != null && !value.IsDead
                         && (value.Role == CharacterRole.Owner || CharacterWorkRoleUtility.IsWorker(value))))
        foreach (CharacterCondition need in new[] { CharacterCondition.HUNGER, CharacterCondition.FUN,
                     CharacterCondition.THIRST, CharacterCondition.SLEEP, CharacterCondition.EXCRETION, CharacterCondition.HYGIENE })
        {
            float current = actor.Stats.GetConditionValue(need);
            if (current < 95f) actor.Stats.ChangesStat(need, 100f - current);
        }
    }

    private void MaintainNonTargetSkillPreparationHunger()
    {
        Require(skillNonTargetHungerFixture && Application.isBatchMode
                && DungeonPersistencePaths.IsIsolatedVerification && skillNormalWeaponPreparation
                && skillReplayHoldNaturalInvasion && (sourceV9SkillReplay || SkillSupportContinuation),
            "Non-target hunger fixture escaped its isolated selected-skill preparation.");
        if (nonTargetPreparationFixturesClosed) return;
        // Do not modify even this non-target input during the effect witness.
        if (scope.Container.Resolve<IOffenseBattleRuntime>().HasActiveBattle) return;
        if (nonTargetDailyNutritionHold == null)
        {
            nonTargetNutritionFixtureRuntime = scope.Container.Resolve<ISurvivalFoodPersistence>() as SurvivalFoodRuntime;
            Require(nonTargetNutritionFixtureRuntime != null,
                "Non-target preparation has no daily nutrition owner.");
            nonTargetDailyNutritionHold = nonTargetNutritionFixtureRuntime.DebugHoldDailyNutritionForIsolatedPreparation();
            Require(nonTargetNutritionFixtureRuntime.DebugIsolatedDailyNutritionHeld,
                "Non-target preparation failed to acquire its explicit daily nutrition hold.");
        }
        float gameTime = scope.Container.Resolve<IGameClock>().Time;
        if (gameTime < nextNonTargetHungerFixtureGameTime) return;
        ICharacterWorldQuery world = scope.Container.Resolve<ICharacterWorldQuery>();
        if (nonTargetHungerResidentIds == null)
        {
            nonTargetHungerResidentIds = world.Characters
                .Where(actor => actor != null && !actor.IsDead
                    && (actor.Role == CharacterRole.Owner || CharacterWorkRoleUtility.IsWorker(actor)))
                .Select(actor => actor.Identity.PersistentId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            Require(nonTargetHungerResidentIds.Length == 3,
                "Non-target hunger fixture requires the original three residents.");
        }
        CharacterActor[] residents = world.Characters.Where(actor => actor != null && !actor.IsDead
            && nonTargetHungerResidentIds.Contains(actor.Identity.PersistentId)).ToArray();
        Require(residents.Length == nonTargetHungerResidentIds.Length,
            "Non-target hunger fixture lost an original resident.");
        if (skillPreparationEnvironmentFixture)
            NeutralizeSupportPreparationExposure(residents);
        if (skillPreparationInstantWorkFixture && nonTargetInstantWorkHold == null)
        {
            nonTargetInstantWorkFixtureRuntime = scope.Container.Resolve<IDungeonDebugRuleRuntime>() as DungeonDebugRuleRuntime;
            Require(nonTargetInstantWorkFixtureRuntime != null, "Preparation has no debug rule owner.");
            IDungeonDebugModeService mode = scope.Container.Resolve<IDungeonDebugModeService>();
            string debugStateBefore = JsonUtility.ToJson(mode.Capture());
            bool developerModeBefore = mode.IsDeveloperModeEnabled;
            bool[] cheatsBefore = Enum.GetValues(typeof(DungeonDebugCheat)).Cast<DungeonDebugCheat>()
                .Select(mode.IsCheatEnabled).ToArray();
            nonTargetInstantWorkHold = nonTargetInstantWorkFixtureRuntime.DebugHoldInstantWorkForIsolatedPreparation();
            Require(nonTargetInstantWorkFixtureRuntime.DebugIsolatedPreparationInstantWorkHeld
                    && nonTargetInstantWorkFixtureRuntime.IsEnabled(DungeonDebugCheat.InstantWork)
                    && developerModeBefore == mode.IsDeveloperModeEnabled
                    && debugStateBefore == JsonUtility.ToJson(mode.Capture())
                    && cheatsBefore.SequenceEqual(Enum.GetValues(typeof(DungeonDebugCheat)).Cast<DungeonDebugCheat>()
                        .Select(mode.IsCheatEnabled)),
                "Instant work preparation changed saved debug state, mode or user cheats.");
            WriteNew("skill-preparation-instant-work-fixture.json", new JObject
            {
                ["origin"] = "explicit isolated non-target preparation speed fixture",
                ["rule"] = "existing InstantWork for all non-target world work during isolated preparation; commands and material costs retained",
                ["affectedConsumers"] = "work executor, work orders, blueprint research and knowledge residue before expedition",
                ["workSpeedAltered"] = true, ["naturalWorkProofClaimed"] = false,
                ["developerModeWritten"] = false, ["userCheatsWritten"] = false,
                ["savedDebugStateWritten"] = false, ["healthWritten"] = false,
                ["excludedDuringActualExpeditionAndBattle"] = true, ["trainingEligible"] = false
            }.ToString());
        }
        var inputs = new JArray();
        foreach (CharacterActor actor in residents)
        {
            Require(actor.Stats.TryGetConditionValue(CharacterCondition.HUNGER, out float before),
                "Non-target hunger fixture has no hunger authority.");
            float health = actor.Stats.CurrentHealth;
            float funBefore = actor.Stats.GetConditionValue(CharacterCondition.FUN);
            if (before != 100f) actor.ChangesStat(CharacterCondition.HUNGER, 100f - before);
            if (funBefore != 100f) actor.ChangesStat(CharacterCondition.FUN, 100f - funBefore);
            Require(actor.Stats.GetConditionValue(CharacterCondition.HUNGER) == 100f
                    && actor.Stats.GetConditionValue(CharacterCondition.FUN) == 100f
                    && actor.Stats.CurrentHealth == health,
                "Non-target hunger fixture changed health or failed its declared input.");
            inputs.Add(new JObject { ["actorId"] = actor.Identity.PersistentId,
                ["hungerBefore"] = before, ["hungerAfter"] = 100f,
                ["funBefore"] = funBefore, ["funAfter"] = 100f,
                ["healthBefore"] = health, ["healthAfter"] = actor.Stats.CurrentHealth });
        }
        nextNonTargetHungerFixtureGameTime = gameTime + 5f;
        if (!nonTargetHungerFixtureScopeWritten)
        {
            WriteNew("skill-non-target-hunger-fixture.json", new JObject
            {
                ["origin"] = "explicit isolated non-target preparation fixture, not meal or recovery evidence",
                ["inputs"] = inputs, ["cadenceGameSeconds"] = 5f, ["excludedDuringActiveBattle"] = true,
                ["dailyNutritionCalculationHeldDuringPreparation"] = true,
                ["dailyNutritionSavedFrontierWritten"] = false, ["mealsFabricated"] = false,
                ["permanentlyReleasedBeforeExpeditionOrActiveBattleWitness"] = true,
                ["healthInjected"] = false, ["laborInjectedByThisNeedFixture"] = false,
                ["separatePreparationInstantWorkFixture"] = skillPreparationInstantWorkFixture, ["resourcesInjected"] = false,
                ["ledgerInjected"] = false, ["naturalPlayClaimed"] = false, ["trainingEligible"] = false
            }.ToString());
            nonTargetHungerFixtureScopeWritten = true;
        }
    }

    private void NeutralizeSupportPreparationExposure(CharacterActor[] residents)
    {
        Require(skillPreparationEnvironmentFixture && skillEquipmentPreparationFixture
                && Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && !nonTargetPreparationFixturesClosed
                && !scope.Container.Resolve<IOffenseBattleRuntime>().HasActiveBattle
                && residents.Length == 3,
            "Exposure input fixture is only for the original three residents before the actual battle.");
        ICharacterEnvironmentPersistence environment = scope.Container.Resolve<ICharacterEnvironmentPersistence>();
        if (nonTargetExposureHold == null)
        {
            nonTargetEnvironmentFixtureRuntime = environment as CharacterEnvironmentUnityAdapter;
            Require(nonTargetEnvironmentFixtureRuntime != null, "Preparation has no exposure step owner.");
            nonTargetExposureHold = nonTargetEnvironmentFixtureRuntime.DebugHoldExposureForIsolatedPreparation(
                residents.Select(actor => actor.Identity.PersistentId).ToArray());
            Require(nonTargetEnvironmentFixtureRuntime.DebugIsolatedPreparationExposureHeld,
                "Preparation exposure step hold was not acquired.");
        }
        DungeonCharacterEnvironmentSaveData state = environment.Capture();
        HashSet<string> ids = residents.Select(actor => actor.Identity.PersistentId).ToHashSet(StringComparer.Ordinal);
        var health = residents.ToDictionary(actor => actor.Identity.PersistentId, actor => actor.Stats.CurrentHealth);
        string outcomeOwnerBefore = JsonUtility.ToJson(new DungeonCharacterEnvironmentSaveData
        { nextExposureOutcomeSequence = state.nextExposureOutcomeSequence, exposureOutcomes = state.exposureOutcomes });
        int outcomesBefore = ReadAllOutcomes().Count();
        var before = new JArray((state.exposures ?? Array.Empty<CharacterEnvironmentExposure>())
            .Where(value => value != null && ids.Contains(value.characterId))
            .Select(value => JObject.Parse(JsonUtility.ToJson(value))));
        nonTargetEnvironmentFixtureRuntime.DebugNeutralizeHeldPreparationExposure();
        DungeonCharacterEnvironmentSaveData after = environment.Capture();
        string outcomeOwnerAfter = JsonUtility.ToJson(new DungeonCharacterEnvironmentSaveData
        { nextExposureOutcomeSequence = after.nextExposureOutcomeSequence, exposureOutcomes = after.exposureOutcomes });
        Require(residents.All(actor => actor.Stats.CurrentHealth == health[actor.Identity.PersistentId])
                && outcomeOwnerBefore == outcomeOwnerAfter && outcomesBefore == ReadAllOutcomes().Count(),
            "Preparation exposure input changed health or environment/canonical outcomes.");
        if (!nonTargetEnvironmentFixtureScopeWritten)
        {
            WriteNew("skill-non-target-environment-fixture.json", new JObject
            {
                ["origin"] = "explicit user-authorized non-target test exposure input",
                ["actorIds"] = new JArray(ids.OrderBy(id => id, StringComparer.Ordinal)),
                ["before"] = before, ["cadenceGameSeconds"] = 5,
                ["input"] = "neutral cold/heat/airborne/visual exposure bands and damage accumulator; work context cleared",
                ["originalResidentsExposureStepsHeldDuringPreparation"] = true,
                ["globalAggregateRestoreUsed"] = false, ["otherActorWorkContextsAndTickAccumulatorPreserved"] = true,
                ["existingExposureOutcomesConvergeNormally"] = true,
                ["healthWritten"] = false, ["environmentOutcomeOwnerUnchanged"] = true,
                ["canonicalOutcomesUnchanged"] = true, ["recruitsIncluded"] = false,
                ["excludedDuringActiveBattle"] = true, ["permanentlyEndedBeforeExpedition"] = true,
                ["naturalRecoveryClaimed"] = false, ["trainingEligible"] = false
            }.ToString());
            nonTargetEnvironmentFixtureScopeWritten = true;
        }
    }

    private void CloseNonTargetSupportPreparationFixtures(string boundary)
    {
        EndSupportHistoryCapacityFixture();
        if (nonTargetDailyNutritionHold == null && nonTargetExposureHold == null && nonTargetInstantWorkHold == null) return;
        nonTargetDailyNutritionHold?.Dispose();
        nonTargetDailyNutritionHold = null;
        nonTargetExposureHold?.Dispose();
        nonTargetExposureHold = null;
        nonTargetInstantWorkHold?.Dispose();
        nonTargetInstantWorkHold = null;
        nonTargetPreparationFixturesClosed = true;
        Require(nonTargetNutritionFixtureRuntime != null
                && !nonTargetNutritionFixtureRuntime.DebugIsolatedDailyNutritionHeld,
            "Non-target daily nutrition hold was not released before gameplay verification.");
        Require(nonTargetEnvironmentFixtureRuntime == null || !nonTargetEnvironmentFixtureRuntime.DebugIsolatedPreparationExposureHeld,
            "Non-target exposure step hold was not released before gameplay verification.");
        Require(nonTargetInstantWorkFixtureRuntime == null || (!nonTargetInstantWorkFixtureRuntime.DebugIsolatedPreparationInstantWorkHeld
                && !nonTargetInstantWorkFixtureRuntime.IsEnabled(DungeonDebugCheat.InstantWork)),
            "Preparation InstantWork was not released before gameplay verification.");
        WriteNew("skill-non-target-preparation-fixture-ended.json", new JObject
        {
            ["boundary"] = boundary, ["dailyNutritionHoldReleased"] = true,
            ["heldDailyNutritionChecks"] = nonTargetNutritionFixtureRuntime.DebugHeldDailyNutritionChecks,
            ["calendarDayAtRelease"] = calendar.Day,
            ["savedNutritionFrontierAtRelease"] = nonTargetNutritionFixtureRuntime.Capture().lastProcessedDay,
            ["needInputMaintenancePermanentlyEnded"] = true,
            ["environmentExposureInputMaintenancePermanentlyEnded"] = skillPreparationEnvironmentFixture,
            ["exposureStepHoldReleased"] = nonTargetEnvironmentFixtureRuntime != null
                && !nonTargetEnvironmentFixtureRuntime.DebugIsolatedPreparationExposureHeld,
            ["heldOriginalResidentExposureSteps"] = nonTargetEnvironmentFixtureRuntime?.DebugHeldPreparationExposureSteps ?? 0,
            ["instantWorkHoldReleased"] = nonTargetInstantWorkFixtureRuntime != null
                && !nonTargetInstantWorkFixtureRuntime.DebugIsolatedPreparationInstantWorkHeld,
            ["preparationInstantWorkQueries"] = nonTargetInstantWorkFixtureRuntime?.DebugPreparationInstantWorkQueries ?? 0,
            ["healthWritten"] = false, ["stockWritten"] = false, ["mealsFabricated"] = false,
            ["trainingEligible"] = false
        }.ToString());
    }

    private IEnumerator RecruitSkillExpeditionParticipant(SkillMechanicWitness mechanic, Action observe)
    {
        phase = "skill-normal-recruitment-capacity";
        var witness = new SkillRecruitmentWitness();
        mechanic.recruitment = witness;
        RegularCustomerRuntime customers = UnityEngine.Object.FindFirstObjectByType<RegularCustomerRuntime>();
        Require(customers != null && pausedSpawner != null,
            "Normal recruitment requires its loaded customer authority and production spawner.");
        BeginSupportHistoryCapacityFixture(customers);
        if (skillRecruitmentHistoryCapacityFixture)
        {
            witness.controlledHistoryCapacityFixture = true;
            witness.origin = "actual public visit/recruit commands with explicit history-capacity test prerequisite; not natural eligibility proof or training data";
        }
        IOperationsFeatureCommandService commands = scope.Container.Resolve<IOperationsFeatureCommandService>();
        SettlementPopulationCapacitySnapshot capacity = customers.CapturePopulationCapacity();
        witness.residentCountBefore = capacity.ResidentCount;
        witness.sleepingSlotsBefore = capacity.SleepingSlotCount;
        witness.policyBefore = customers.ImmigrationPolicy.ToString();
        if (customers.ImmigrationPolicy == SettlementImmigrationPolicy.Balanced)
        {
            OperationsFeatureCommandResult policy = commands.CycleImmigrationPolicy();
            Require(policy.Succeeded && customers.ImmigrationPolicy == SettlementImmigrationPolicy.Open,
                "Public immigration policy command failed: " + policy.Message);
        }
        witness.policyAfter = customers.ImmigrationPolicy.ToString();
        yield return PrepareSkillRecruitmentHousing(witness, customers, observe);
        EnsureSkillRecruitmentFoodSupply(customers);
        // Let the normal planner project restored work/population on its own
        // tick; the verifier never writes reserve, alert or labor accounting.
        yield return null;
        observe();
        if (SkillSupportContinuation)
        {
            JObject reserveObservation = null;
            var reserveFrameObservations = new JArray();
            bool projectionExact = false, accountingUnchanged = false, actualWuUnchanged = false;
            // Planner.Tick can precede a routine's terminal in this frame.
            // Observe up to four normal frames; do not force the planner, write
            // capacity, or compare a previous-frame projection to a new actor state.
            for (int reserveFrame = 0; reserveFrame < 4; reserveFrame++)
            {
                IEmergencyWorkAccountingService accounting =
                    scope.Container.Resolve<IEmergencyWorkAccountingService>();
                EmergencyReserveSnapshot accountingBefore = accounting.CaptureSnapshot();
                long actualWuBefore = scope.Container.Resolve<ISettlementLaborAccountingService>()
                    .Capture().ActualLaborMilliWu;
                SettlementEmergencyReserveTargetSnapshot reserve =
                    scope.Container.Resolve<ISettlementEmergencyReserveTargetQuery>().CaptureTarget();
                ICharacterLifeQuery lives = scope.Container.Resolve<ICharacterLifeQuery>();
                ICharacterBodyHealthQuery health = scope.Container.Resolve<ICharacterBodyHealthQuery>();
                string[] idleIds = scope.Container.Resolve<ICharacterWorldQuery>().Characters
                    .Where(actor => actor != null && !actor.IsDead
                        && CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
                        && lives.TryGet(id, out CharacterLifeRecord lifeRecord)
                        && lifeRecord.LifeStage >= CharacterLifeStage.Adult
                        && !health.GetSnapshot(actor).Downed
                        && (actor.Role == CharacterRole.Owner || CharacterWorkRoleUtility.IsWorker(actor))
                        && (!actor.TryGetAbility(out AbilityWork ability) || !ability.isWorking)
                        && !accounting.TryGetWorkerOperation(id.Value, out _))
                    .Select(actor => actor.Identity.PersistentId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                EmergencyReserveSnapshot accountingAfter = accounting.CaptureSnapshot();
                accountingUnchanged = accountingBefore.AccountingRevision == accountingAfter.AccountingRevision
                    && accountingBefore.GroundTruthHash == accountingAfter.GroundTruthHash
                    && accountingBefore.ReserveEligibleMilliWu == accountingAfter.ReserveEligibleMilliWu
                    && accountingBefore.ActiveOperationCount == accountingAfter.ActiveOperationCount;
                actualWuUnchanged = actualWuBefore == scope.Container.Resolve<ISettlementLaborAccountingService>()
                    .Capture().ActualLaborMilliWu;
                projectionExact = reserve.IdleResidentCount == idleIds.Length
                    && reserve.ActiveReserveMilliWu == accountingBefore.ReserveEligibleMilliWu
                    && reserve.AvailableMilliWu == SettlementEmergencyReservePlanner.CalculateAvailableReserveMilliWu(
                        accountingBefore.ReserveEligibleMilliWu, idleIds.Length)
                    && Mathf.Approximately(reserve.Coverage,
                        customers.CapturePopulationCapacity().EmergencyReserveCoverage);
                reserveObservation = new JObject
                {
                    ["origin"] = "live planner potential idle-resident reserve; not completed labor or a fixture ledger entry",
                    ["frameAttempt"] = reserveFrame + 1,
                    ["idleResidentIds"] = new JArray(idleIds), ["idleResidentCount"] = reserve.IdleResidentCount,
                    ["idleReserveMilliWu"] = reserve.IdleReserveMilliWu,
                    ["activeReserveMilliWu"] = reserve.ActiveReserveMilliWu,
                    ["targetMilliWu"] = reserve.TargetMilliWu, ["availableMilliWu"] = reserve.AvailableMilliWu,
                    ["coverage"] = reserve.Coverage, ["projectionExact"] = projectionExact,
                    ["accountingUnchanged"] = accountingUnchanged, ["actualWuUnchanged"] = actualWuUnchanged,
                    ["naturalPlay"] = false, ["trainingEligible"] = false, ["humanApprovalClaimed"] = false
                };
                reserveFrameObservations.Add(reserveObservation.DeepClone());
                if (projectionExact && accountingUnchanged && actualWuUnchanged) break;
                if (reserveFrame < 3) { yield return null; observe(); }
            }
            reserveObservation["coherentFrameObservations"] = reserveFrameObservations;
            WriteNew("skill-recruitment-reserve-witness.json",
                reserveObservation.ToString(Newtonsoft.Json.Formatting.Indented));
            Require(projectionExact && accountingUnchanged && actualWuUnchanged,
                "Live idle-resident reserve projection or readonly accounting boundary differs.");
        }
        SettlementPopulationAcceptance admission = customers.EvaluateImmigration();
        witness.sleepingSlotsAfter = customers.CapturePopulationCapacity().SleepingSlotCount;
        witness.admissionAccepted = admission.Accepted;
        Require(admission.Accepted, "Normal recruitment capacity rejected: " + admission.FailureCode + ": " + admission.Message);

        phase = "skill-normal-visitor-preparation";
        // Nutrition can legitimately author unrelated resident requests. Drain
        // only the existing verifier transport boundary before selecting the
        // production preparation owner; inactive population previews remain
        // owned by the population service and are never cancelled here.
        CancelOutstandingTransport();
        ICharacterPopulationService population = scope.Container.Resolve<ICharacterPopulationService>();
        CharacterSO template = scope.Container.Resolve<IRunCharacterCatalog>().Characters
            .Single(value => value != null && value.id == 1 && value.characterType == CharacterType.Customer);
        var beforeVisitors = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .Where(value => value != null).Select(value => value.Identity.PersistentId).ToHashSet(StringComparer.Ordinal);
        timeScale.Scale = originalScale;
        Time.maximumDeltaTime = mechanic.physicalMaximumDeltaTime;
        gameSpeed.SetPaused(false);
        float preparationDeadline = Time.realtimeSinceStartup + 90f;
        bool spawned = pausedSpawner.TrySpawnCharacter(template.id, out CharacterSpawnRejection rejection);
        Require(spawned || rejection is CharacterSpawnRejection.PopulationProfilePreparing
                or CharacterSpawnRejection.PopulationProfileNotCreated,
            "Production visitor creation rejected: " + rejection);
        int requestedTemplateId = template.id;
        WorldCharacterProfile profile = spawned ? population.Profiles.Where(value => value != null && value.isAlive
                && !value.isOwner && !value.isStaff && value.characterDataId == template.id
                && value.isVisiting && !beforeVisitors.Contains(value.persistentId))
            .OrderBy(value => value.persistentId, StringComparer.Ordinal).SingleOrDefault() : null;
        // The population authority prepares its global pool in order, not one
        // template's lexicographic subset. Bind the response fixture to a real
        // submitted owner occupying a service slot instead of waiting behind
        // another response-less capture owner. Its actual template is retained.
        while (!spawned && profile == null && Time.realtimeSinceStartup < preparationDeadline)
        {
            observe();
            generation.Tick();
            profile = population.Profiles.Where(value => value != null && value.isAlive
                    && !value.isOwner && !value.isStaff && !value.isVisiting
                    && generation.CapturePendingModuleDispatchIdsForOwner(value.persistentId).Count > 0)
                .OrderBy(value => value.persistentId, StringComparer.Ordinal).FirstOrDefault();
            if (profile == null) yield return null;
        }
        Require(profile != null, "Production visitor preparation supplied no submitted owner: " + generation.LastDiagnostic);
        template = scope.Container.Resolve<IRunCharacterCatalog>().Characters.Single(value => value != null
            && value.id == profile.characterDataId && value.characterType == CharacterType.Customer);
        WriteNew("skill-recruitment-preparation-owner.json", new JObject
        {
            ["origin"] = "actual production preparation transport owner and authored template; no readiness/skill/evidence mutation",
            ["requestedTemplateId"] = requestedTemplateId, ["initialSpawned"] = spawned,
            ["initialSpawnRejection"] = rejection.ToString(), ["actorId"] = profile.persistentId,
            ["actualTemplateId"] = template.id,
            ["submittedTransportIds"] = new JArray(generation.CapturePendingModuleDispatchIdsForOwner(profile.persistentId)),
            ["trainingEligible"] = false, ["humanApprovalClaimed"] = false
        }.ToString());
        witness.actorId = profile.persistentId;
        skillRecruitmentPreparationOwnerId = profile.persistentId;
        Func<LocalLlmCapturedRequest, string> prepareVisitor = captured =>
        {
            Require(captured.ProfileId == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
                    && generation.TryCapturePendingModuleDispatch(captured.CorrelationId,
                        out CharacterProgression requestOwner, out _, out string prompt, out _)
                    && requestOwner.Actor.Identity.PersistentId == witness.actorId && prompt == captured.Prompt
                    && skillRecruitmentPreparationCaptures.Contains(captured.CorrelationId),
                "Preparation response is not bound to the one legitimate visitor.");
            Require(witness.preparationResponses < 8, "Visitor preparation exceeded its bounded actual requests.");
            string response = ShopContinuationSkillResponseFixture.Build(captured);
            Require(!string.IsNullOrWhiteSpace(response), "Offered visitor preparation response could not be built.");
            witness.preparationResponses++;
            WriteNew($"skill-recruitment-preparation-{witness.preparationResponses:D3}-wire.json", captured.WireBodyJson);
            WriteNew($"skill-recruitment-preparation-{witness.preparationResponses:D3}-response.json", response);
            return response;
        };
        if (SkillSupportContinuation)
            queue.ConfigureIsolatedSkillVisitorTestResponses(prepareVisitor);
        else
            queue.ConfigureIsolatedModuleReplayTestResponses(prepareVisitor);
        try
        {
            while (!profile.IsReady && Time.realtimeSinceStartup < preparationDeadline)
            {
                observe();
                generation.Tick();
                foreach (string id in generation.CapturePendingModuleDispatchIdsForOwner(witness.actorId))
                    if (!skillRecruitmentPreparationCaptures.Contains(id))
                        Require(queue.TryDispatchQueuedRequestForIsolatedCapture(id),
                            "Exact visitor preparation transport was not uniquely queued: " + id);
                yield return null;
            }
            witness.preparedThroughProduction = profile.IsReady;
            Require(profile.IsReady, "Actual visitor did not complete active/passive preparation through production callbacks.");
        }
        finally
        {
            if (SkillSupportContinuation)
                queue.ClearIsolatedSkillVisitorTestResponses();
            else
                queue.ClearIsolatedModuleReplayTestResponses();
            skillRecruitmentPreparationOwnerId = null;
        }
        if (!spawned)
            Require(pausedSpawner.TrySpawnCharacter(template.id, out rejection),
                "Prepared visitor spawn rejected: " + rejection);
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        float entryStart = clock.Time, entryReal = Time.realtimeSinceStartup;
        CharacterActor visitor = null;
        while (clock.Time - entryStart < 90f && Time.realtimeSinceStartup - entryReal < 120f)
        {
            observe();
            visitor = scope.Container.Resolve<ICharacterWorldQuery>().Characters
                .SingleOrDefault(value => value != null && value.Identity.PersistentId == witness.actorId);
            if (visitor != null && visitor.CurrentLifecycleState == CharacterLifecycleState.Active) break;
            yield return null;
        }
        witness.enteredDungeon = visitor != null && visitor.CurrentLifecycleState == CharacterLifecycleState.Active;
        Require(witness.enteredDungeon && !visitor.IsDead,
            "Prepared production visitor did not enter the actual dungeon.");
        var outcomeKeys = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        Facility training = scope.Container.Resolve<IBuildingWorldQuery>().Buildings.OfType<Facility>()
            .Where(value => value != null && !value.isDestroy && value.SupportsFacilityRole(FacilityRole.Training)
                && CharacterVisitPolicy.CanVisitBuilding(visitor, value, false, out _))
            .OrderBy(value => Vector2.Distance(value.transform.position, visitor.transform.position)).FirstOrDefault();
        Require(training != null && training.BuildingData.GetNeedRecovery().mood > 0f,
            "The actual visitor has no authored mood-recovering training facility.");
        // Satisfaction is sampled by RegularCustomerRuntime from the real
        // completed facility effect. Do not set mood, visit count or candidacy.
        for (int index = 0; index < 8; index++)
        {
            phase = "skill-normal-recruitment-visit-" + (index + 1);
            var visit = new CommandWitness
            {
                command = "controlled-public-visitor-cycle->registered-training-facility.interact",
                actorId = witness.actorId, facilityId = training.RequirePersistentInstanceId().Value,
                usesBefore = training.FacilityState.completedUses,
                levelBefore = visitor.Progression.Level, experienceBefore = visitor.Progression.CurrentExperience,
                activeUseCheckpointFile = $"skill-recruitment-active-world-{index + 1:D3}.json"
            };
            witness.visits.Add(visit);
            yield return CompleteTrainingThroughRegisteredAi(visitor, training, visit, requireStaffDuty: false, observe: observe);
            visit.usesAfter = training.FacilityState.completedUses;
            visit.levelAfter = visitor.Progression.Level;
            visit.experienceAfter = visitor.Progression.CurrentExperience;
            witness.satisfactionSamples.Add(RegularCustomerService.GetSatisfaction(visitor));
            Require(customers.State.TryGetRecord(witness.actorId, out RegularCustomerRecord record)
                    && record.VisitCount == witness.visits.Count,
                "Real facility completions did not publish exactly one regular-customer visit each.");
            if (record.IsRecruitCandidate) break;
        }
        gameSpeed.SetPaused(true);
        Require(customers.State.TryGetRecord(witness.actorId, out RegularCustomerRecord candidate)
                && candidate.VisitCount >= customers.Rules.recruitCandidateVisitThreshold
                && candidate.AverageSatisfaction >= customers.Rules.recruitCandidateAverageSatisfactionThreshold
                && candidate.IsRecruitCandidate,
            "Real visits did not satisfy authored recruitment thresholds; no candidacy is injected.");
        OperationsFeatureCommandResult recruited = commands.Recruit(witness.actorId);
        Require(customers.State.TryGetRecord(witness.actorId, out candidate),
            "Public recruitment lost its authoritative customer record.");
        witness.recruitMessage = recruited.Message;
        witness.recruited = recruited.Succeeded;
        witness.deliveryApplied = candidate.RecruitDeliveryApplied
            && candidate.RecruitDeliveryActorId == witness.actorId;
        Require(witness.recruited && witness.deliveryApplied && candidate.IsRecruited
                && CharacterWorkRoleUtility.TryGetWork(visitor, out _)
                && UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>()
                    .GetAvailableMemberActors().Any(value => ReferenceEquals(value, visitor)),
            "Public recruitment did not activate the same legitimate expedition participant: " + recruited.Message);
        string recruitedState = SkillWorldStateHash(saves.ToJson(saves.Capture()));
        Require(!commands.Recruit(witness.actorId).Succeeded, "Duplicate recruit unexpectedly succeeded.");
        witness.duplicateRecruitStable = recruitedState == SkillWorldStateHash(saves.ToJson(saves.Capture()));
        witness.outcomes = ReadAllOutcomes().Where(value => !outcomeKeys.Contains(OutcomeKey(value))).ToList();
        Require(witness.duplicateRecruitStable && witness.outcomes.Count(value =>
                value.outcomeTypeId == "service.regular-customer-recruited") == 1
                && !string.IsNullOrWhiteSpace(candidate.RecruitDeliveryOutcomeResultKey),
            "Recruitment lacks a single committed delivery result or duplicate world stability.");
        WriteNew("skill-recruitment-world.json", saves.ToJson(saves.Capture()));
        WriteNew("skill-recruitment-witness.json", JsonUtility.ToJson(witness, true));
        Time.maximumDeltaTime = mechanic.calendarMaximumDeltaTime;
    }

    private IEnumerator PrepareSkillRecruitmentHousing(SkillRecruitmentWitness witness, RegularCustomerRuntime customers,
        Action observe)
    {
        SettlementPopulationCapacitySnapshot before = customers.CapturePopulationCapacity();
        int bedsNeeded = Math.Max(0, before.ResidentCount + 1 - before.SleepingSlotCount);
        Require(bedsNeeded <= 2, "This representative source requires more than two additional sleeping slots.");
        if (bedsNeeded == 0) yield break;
        BuildingSO bed = scope.Container.Resolve<IRunStartVariableCatalog>().Buildings
            .Single(value => value != null && value.id == 1021);
        Require(bed.GetFacilityCode() == "R02" && bed.Facility.capacity == 1,
            "Authored sleeping capacity changed; do not substitute a fabricated bed.");
        BuildingWorkAmountAbility work = bed.GetAbility<BuildingWorkAmountAbility>();
        Require(work != null && work.ConstructionMaterials.Count > 0,
            "Authored bed has no authoritative construction material plan.");
        var controller = scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller;
        IBuildingWorldQuery buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        var bedRooms = new List<RoomInstance>();
        var resumedBedOrders = new Dictionary<Vector2Int, WorkOrderSaveData>();
        GridMode previousMode = controller.GridSystem.Mode;
        int previousSelection = controller.SelectedBuilding != null ? controller.SelectedBuilding.id : -1;
        try
        {
            // Query the same placement validator used by the player before
            // spending or preparing inputs. A bed's first saved room may be
            // open; it is not authority for where new usable beds can go.
            controller.SetGridModeBuild();
            controller.SelectBuildingById(bed.id);
            Require(controller.SelectedBuilding == bed, "Public bed selection did not resolve its authored definition.");
            RoomInstance[] candidates = scope.Container.Resolve<IRoomLayoutCache>()
                .GetLayout(controller.GridSystem.grid).Rooms
                .Where(value => value != null && value.IsUsable && !value.IsSelfContained)
                .OrderByDescending(value => value.SupportsFacilityRole(FacilityRole.Rest))
                .ThenByDescending(value => value.Cells.Count).ThenBy(value => value.Id).ToArray();
            var occupiedPlan = new HashSet<Vector2Int>();
            ConstructionSite[] restoredSites = buildings.Buildings.OfType<ConstructionSite>()
                .Where(value => value != null && !value.isDestroy && value.TargetBuilding == bed)
                .OrderBy(value => value.GridPosition.y).ThenBy(value => value.GridPosition.x).ToArray();
            Require(restoredSites.Length <= bedsNeeded,
                "Existing bed construction exceeds the exact remaining sleeping-capacity plan.");
            foreach (ConstructionSite site in restoredSites)
            {
                Require(SkillSupportContinuation && report.continuation?.officialRestore == true,
                    "Existing housing construction requires the exact officially restored continuation.");
                WorkOrderSaveData currentOrder = scope.Container.Resolve<IWorkOrderRuntime>().Capture().orders
                    .SingleOrDefault(value => value.workOrderId == site.WorkOrderId);
                WorkOrderSaveData sourceOrder = sourceConstructionOrders
                    .SingleOrDefault(value => value.workOrderId == site.WorkOrderId);
                Require(currentOrder != null && sourceOrder != null
                    && JsonUtility.ToJson(currentOrder) == JsonUtility.ToJson(sourceOrder)
                    && currentOrder.targetBuildingId == bed.id
                    && currentOrder.workTypeId == BuiltInWorkTypeIds.Construct.Value
                    && currentOrder.constructionSitePersistentId == site.PersistentInstanceId.Value
                    && currentOrder.gridX == site.GridPosition.x && currentOrder.gridY == site.GridPosition.y
                    && (currentOrder.status is WorkOrderStatus.Ready or WorkOrderStatus.InProgress)
                    && currentOrder.completedWork > 0f && currentOrder.completedWork < currentOrder.requiredWork
                    && currentOrder.materialTransfer?.phase == WorkOrderMaterialTransferPhase.Acknowledged
                    && currentOrder.itemMaterials.Count == work.ConstructionMaterials.Count
                    && work.ConstructionMaterials.All(material => currentOrder.itemMaterials.Count(value =>
                        value.itemId == material.ItemId && value.required == material.Amount
                        && value.delivered == material.Amount) == 1),
                    "Restored housing must retain its exact incomplete owner and already acknowledged authored BOM.");
                RoomInstance room = candidates.SingleOrDefault(value => site.buildPoses.All(value.ContainsCell));
                Require(room != null, "Restored housing construction no longer belongs to a usable actual room.");
                witness.plannedBedPositions.Add(site.GridPosition);
                witness.housingRoomIds.Add(room.Id);
                bedRooms.Add(room);
                resumedBedOrders.Add(site.GridPosition, currentOrder);
                occupiedPlan.UnionWith(site.buildPoses);
            }
            foreach (RoomInstance candidate in candidates)
            {
                if (bedRooms.Count == bedsNeeded) break;
                foreach (Vector2Int cell in candidate.Cells.OrderBy(value => value.y).ThenBy(value => value.x))
                {
                    List<Vector2Int> footprint = bed.GetGridPosList(cell);
                    if (!footprint.All(candidate.ContainsCell)
                        || footprint.Any(occupiedPlan.Contains) || !controller.IsBuildableAt(cell)) continue;
                    witness.plannedBedPositions.Add(cell);
                    witness.housingRoomIds.Add(candidate.Id);
                    bedRooms.Add(candidate);
                    occupiedPlan.UnionWith(footprint);
                    if (bedRooms.Count == bedsNeeded) break;
                }
                if (bedRooms.Count == bedsNeeded) break;
            }
            witness.housingUsableRoomsSelected = bedRooms.Count == bedsNeeded;
        }
        finally
        {
            controller.GridSystem.SetGridMode(previousMode);
            controller.SelectBuildingById(previousSelection);
        }
        Require(witness.housingUsableRoomsSelected,
            "HousingPlacementUnavailable: actual usable rooms have insufficient legal space for the required beds.");
        WriteNew("skill-recruitment-housing-placement-plan.json", JsonUtility.ToJson(witness, true));
        WriteNew("skill-recruitment-housing-resume-plan.json", new JObject
        {
            ["origin"] = "exact official construction restore; acknowledged WIP inputs are not purchased or injected again",
            ["restoredOrders"] = JArray.FromObject(resumedBedOrders.Values),
            ["newBedCount"] = bedsNeeded - resumedBedOrders.Count,
            ["workInjected"] = false, ["trainingEligible"] = false
        }.ToString(Newtonsoft.Json.Formatting.Indented));
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        int Quantity(string itemId) => items.GetAllStacks().Where(value => value != null && value.ItemId == itemId)
            .Sum(value => value.Quantity);
        IWarehouseFeatureQueryService supplyQuery = scope.Container.Resolve<IWarehouseFeatureQueryService>();
        IWarehouseFeatureCommandService supplyCommands = scope.Container.Resolve<IWarehouseFeatureCommandService>();
        int requiredCloth = work.ConstructionMaterials.Where(value => value.ItemId == "material:cloth")
            .Sum(value => checked(value.Amount * (bedsNeeded - resumedBedOrders.Count)));
        witness.clothBeforeFixture = Quantity("material:cloth");
        int clothDeficit = requiredCloth - witness.clothBeforeFixture;
        Require(clothDeficit <= 1, "The authorized one-cloth fixture cannot cover a larger material deficit.");
        if (clothDeficit == 1)
        {
            // Explicitly authorized QA setup, before any supply command, not a
            // failed-command fallback or evidence of a purchase/production.
            // All hauling, material consumption, work and recruitment remain real.
            Require((sourceV9SkillReplay || SkillSupportContinuation)
                    && Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification,
                "The one-cloth preparation input is isolated skill QA only.");
            var outcomesBefore = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
            int moneyBefore = supplyQuery.Capture().CurrentMoney;
            Require(items.SpawnItemAtDropoff("material:cloth", 1,
                    "qa:explicit-one-cloth-preparation-input:not-purchase-or-production", out int spawned)
                    && spawned == 1 && Quantity("material:cloth") == witness.clothBeforeFixture + 1,
                "Explicit one-cloth preparation did not create exactly one physical input at the normal dropoff.");
            witness.controlledOneClothPreparationFixture = true;
            witness.clothFixtureCreatedNoGameplayOutcome = outcomesBefore.SetEquals(ReadAllOutcomes().Select(OutcomeKey))
                && supplyQuery.Capture().CurrentMoney == moneyBefore;
            Require(witness.clothFixtureCreatedNoGameplayOutcome,
                "A test preparation input must not manufacture supply/production outcomes or payment.");
        }
        witness.clothAfterFixture = Quantity("material:cloth");
        WriteNew("skill-recruitment-cloth-preparation-input.json", JsonUtility.ToJson(witness, true));
        var deficits = work.ConstructionMaterials.Select(value => new
        {
            itemId = value.ItemId, required = checked(value.Amount * (bedsNeeded - resumedBedOrders.Count)),
            available = Quantity(value.ItemId)
        }).Where(value => value.available < value.required).ToArray();
        WarehouseFeatureSurfaceModel supply = supplyQuery.Capture();
        // Validate the entire supply plan before spending. A permissive command
        // can accept a custom StockDeliveryOffer; constructing one here would
        // invent a cloth offer that the authored game does not actually expose.
        foreach (var deficit in deficits)
            Require(supply.DeliveryOffers.Any(value => value.Offer.itemId == deficit.itemId
                    && value.Offer.amount >= deficit.required - deficit.available),
                "HousingSupplyUnavailable: " + deficit.itemId + "; required=" + deficit.required
                + "; actual=" + deficit.available + "; no adequate authored delivery offer."
                + " Obtain this material through its real production/recovery path before recruitment.");
        int housingCost = deficits.Sum(deficit => supply.DeliveryOffers
            .Where(value => value.Offer.itemId == deficit.itemId
                && value.Offer.amount >= deficit.required - deficit.available)
            .Min(value => value.Offer.cost));
        yield return PrepareSkillHousingFundsThroughNormalSurplusSale(housingCost, observe);
        foreach (var deficit in deficits)
        {
            WarehouseFeatureSurfaceModel current = supplyQuery.Capture();
            StockDeliveryOffer offer = current.DeliveryOffers.Where(value => value.Offer.itemId == deficit.itemId
                    && value.Offer.amount >= deficit.required - deficit.available)
                .OrderBy(value => value.Offer.cost).First().Offer;
            int quantityBefore = Quantity(deficit.itemId);
            WarehouseFeatureCommandResult purchase = supplyCommands.PurchaseDelivery(offer);
            Require(purchase.Succeeded && supplyQuery.Capture().CurrentMoney == current.CurrentMoney - offer.cost
                    && Quantity(deficit.itemId) == quantityBefore + offer.amount,
                "Actual housing supply payment/physical delivery failed: " + purchase.Message);
        }
        CharacterActor hauler = scope.Container.Resolve<ICharacterWorldQuery>().Characters
            .SingleOrDefault(value => value != null && value.Identity.PersistentId == "character:staff:1:01");
        Require(hauler != null && hauler.CanRunAi && CharacterWorkRoleUtility.TryGetWork(hauler, out _)
                && hauler.Brain != null && hauler.LogComponent != null,
            "Housing material supply requires its actual available staff hauler and activity authority.");
        AbilityWork haulWork = hauler.GetAbility<AbilityWork>();
        WorkPriorityLevel originalPriority = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul);
        bool haulerWasOffDuty = haulWork.IsOffDuty;
        witness.restoredHaulAssignmentRetained = SkillSupportContinuation
            && report.continuation?.officialRestore == true && originalPriority == WorkPriorityLevel.Priority1;
        Require(Enum.IsDefined(typeof(WorkPriorityLevel), originalPriority)
                && (originalPriority != WorkPriorityLevel.Priority1 || witness.restoredHaulAssignmentRetained),
            "Housing Haul preparation requires a defined saved priority; a Priority1 no-op is not a new command.");
        witness.haulAssignmentActorId = hauler.Identity.PersistentId;
        witness.originalHaulPriority = originalPriority.ToString();
        CharacterActivityEvent previousActivity = hauler.LogComponent.ActivityEntries.LastOrDefault();
        try
        {
            if (haulerWasOffDuty)
            {
                haulWork.SetDutyState(AbilityWork.DutyState.OnDuty);
                witness.publicHaulDutyCommandIssued = true;
            }
            witness.haulOnDutyApplied = !haulWork.IsOffDuty;
            Require(witness.haulOnDutyApplied, "Housing public Haul duty command did not apply.");
            if (witness.restoredHaulAssignmentRetained)
            {
                // The first-bed checkpoint was captured while the original
                // public assignment was active. Retain the saved priority;
                // a no-op is not a new player command or activity witness.
                WriteNew("skill-recruitment-public-haul-assignment.json", new JObject
                {
                    ["origin"] = "exact officially restored support Haul assignment; no new priority command",
                    ["actorId"] = witness.haulAssignmentActorId, ["before"] = witness.originalHaulPriority,
                    ["after"] = witness.originalHaulPriority, ["restoredAssignmentRetained"] = true,
                    ["publicCommandIssued"] = false, ["activity"] = null,
                    ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
                }.ToString());
            }
            else
            {
                try { haulWork.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1); }
                finally
                {
                    witness.publicHaulAssignmentIssued = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul)
                        == WorkPriorityLevel.Priority1;
                }
                CharacterActivityEvent changed = hauler.LogComponent.ActivityEntries.LastOrDefault();
                witness.haulAssignmentActivityExact = changed != null && !ReferenceEquals(changed, previousActivity)
                    && changed.ActorId == witness.haulAssignmentActorId && changed.ActionId == BuiltInWorkTypeIds.Haul.Value
                    && changed.OutcomeId == CharacterActivityOutcomes.Changed
                    && changed.ReasonCode == $"priority:{(int)WorkPriorityLevel.Priority1}"
                    && changed.Value.Equals((float)(int)WorkPriorityLevel.Priority1);
                Require(witness.haulAssignmentActivityExact, "Housing Haul priority command lacks its exact activity.");
                WriteNew("skill-recruitment-public-haul-assignment.json", new JObject
                {
                    ["origin"] = "simulated-player-public-work-priority-command",
                    ["actorId"] = witness.haulAssignmentActorId, ["before"] = witness.originalHaulPriority,
                    ["after"] = WorkPriorityLevel.Priority1.ToString(), ["activity"] = JObject.FromObject(changed),
                    ["forcedAiChoice"] = false, ["naturalInvasionSuppressedByThisCommand"] = false,
                    ["naturalInvasionHoldApplied"] = report.naturalInvasionHoldApplied,
                    ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
                }.ToString());
            }
            for (int index = 0; index < bedsNeeded; index++)
            {
                int slotsBefore = customers.CapturePopulationCapacity().SleepingSlotCount;
                yield return ConstructFacilityFixture(bed, bedRooms[index], $"skill-recruitment-housing-{index + 1:D3}",
                    useAuthoredWorkBudget: true, observe: observe, plannedPosition: witness.plannedBedPositions[index],
                    approvedSupportNeedsOnly: true);
                witness.housingConstruction.Add(report.facilityConstruction);
                BuildableObject installedBed = buildings.Buildings.Single(value => value != null && !value.isDestroy
                    && value is not ConstructionSite && value.BuildingData == bed
                    && value.Grid == controller.GridSystem.grid
                    && value.centerPos == witness.plannedBedPositions[index]);
                Require(scope.Container.Resolve<IRoomFacilityPolicy>().IsFacilityRoleAvailable(installedBed,
                        FacilityRole.Rest, out string roomFailure),
                    "Completed bed is not actually usable for rest: " + roomFailure);
                Require(customers.CapturePopulationCapacity().SleepingSlotCount == slotsBefore + 1,
                    "Completed actual bed did not publish exactly one sleeping slot.");
            }
            foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>().Characters)
                if (CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork duty)) duty.ClearPriorityWorkTarget();
        }
        finally
        {
            if (witness.publicHaulAssignmentIssued)
            {
                if (hauler.Brain.HasRunningAction && hauler.Brain.bestAction?.actionset is AIHaul)
                    Require(hauler.Brain.StopCurrentActionForReplan("End housing public Haul assignment"),
                        "Could not release actual housing Haul before priority restoration.");
                haulWork.SetWorkPriority(BuiltInWorkTypeIds.Haul, originalPriority);
                witness.haulPriorityRestored = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul) == originalPriority;
                Require(witness.haulPriorityRestored, "Housing public Haul priority restoration failed.");
                WriteNew("skill-recruitment-public-haul-assignment-restored.json", JsonUtility.ToJson(witness, true));
            }
            else if (witness.restoredHaulAssignmentRetained)
            {
                witness.haulPriorityRestored = haulWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul) == originalPriority;
                Require(witness.haulPriorityRestored, "Officially restored housing Haul priority changed unexpectedly.");
                WriteNew("skill-recruitment-public-haul-assignment-restored.json", JsonUtility.ToJson(witness, true));
            }
            if (witness.publicHaulDutyCommandIssued)
                haulWork.SetDutyState(AbilityWork.DutyState.OffDuty);
            witness.haulDutyRestored = haulWork.IsOffDuty == haulerWasOffDuty;
            Require(witness.haulDutyRestored, "Housing public Haul duty restoration failed.");
            WriteNew("skill-recruitment-public-haul-duty-restored.json", JsonUtility.ToJson(witness, true));
        }
    }

    private IEnumerator PrepareSkillHousingFundsThroughNormalSurplusSale(int requiredMoney, Action observe)
    {
        IWarehouseFeatureQueryService query = scope.Container.Resolve<IWarehouseFeatureQueryService>();
        if (query.Capture().CurrentMoney >= requiredMoney) yield break;
        Require(skillNormalWeaponPreparation && (sourceV9SkillReplay || SkillSupportContinuation),
            "Normal surplus procurement is limited to the declared support preparation.");
        Require(ResourceStockPolicySaleOutcomeE2EDebugScenarios.RunAll(),
            "Stock sale domain regressions failed before the actual housing procurement.");
        WriteNew("skill-housing-sale-domain-regression.json", new JObject
        {
            ["entry"] = "ResourceStockPolicySaleOutcomeE2EDebugScenarios.RunAll",
            ["passed"] = true,
            ["scope"] = new JArray("buffered-only without free surplus", "reservation rejection",
                "physical export/income pending restore", "no duplicate acknowledged income", "quality-rejected authority release"),
            ["controlledDomainFixture"] = true, ["actualHaulClaimed"] = false, ["trainingEligible"] = false
        }.ToString());
        IResourceStockPolicyRuntime policies = scope.Container.Resolve<IResourceStockPolicyRuntime>();
        IWarehouseFeatureCommandService commands = scope.Container.Resolve<IWarehouseFeatureCommandService>();
        IResourceEconomyContentCatalog catalog = scope.Container.Resolve<IResourceEconomyContentCatalog>();
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        IEconomyTransactionLedger ledger = scope.Container.Resolve<IEconomyTransactionLedger>();
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        ICharacterNeedBalanceRuntime needBalance = scope.Container.Resolve<ICharacterNeedBalanceRuntime>();
        // Finite non-BOM/non-food surplus only. Retain clothing for four future
        // residents plus one spare, and two candles. No item or price is authored here.
        var reserves = new[]
        {
            (itemId: "craft:candle", reserve: 2),
            (itemId: "apparel:lower-underwear", reserve: 5),
            (itemId: "apparel:undershirt", reserve: 5)
        };
        int Owned(string id) => items.GetAllStacks().Where(value => value != null && value.ItemId == id)
            .Sum(value => value.Quantity);
        int Available(string id) => items.GetAllStacks().Where(value => value != null && value.ItemId == id
                && (value.State == WorldItemStackState.Stored || value.State == WorldItemStackState.Loose))
            .Sum(value => value.AvailableQuantity);
        int conservativeProceeds = 0;
        var plan = new JArray();
        foreach (var candidate in reserves)
        {
            Require(catalog.TryGetItem(candidate.itemId, out ResourceItemDefinitionSO item) && item.CanSellToMarket,
                "Housing funding has no authored market price: " + candidate.itemId);
            ResourceStockPolicyData policy = policies.GetOrCreate(candidate.itemId);
            Require(!policy.enabled && !policy.isEmergencyReserve && policy.surplusDisposition == StockSurplusDisposition.Hold
                    && string.IsNullOrEmpty(policy.inputDestinationId)
                    && !policies.Capture().pendingSales.Any(value => value.itemId == candidate.itemId),
                "Housing funding must not replace an existing sale/reserve: " + candidate.itemId);
            int surplus = Math.Min(Available(candidate.itemId), Math.Max(0, Owned(candidate.itemId) - candidate.reserve));
            int perUnit = Mathf.FloorToInt(item.UnitPrice * item.MarketSaleRate);
            Require(perUnit > 0, "Housing funding has no positive conservative authored sale proceeds.");
            conservativeProceeds = checked(conservativeProceeds + surplus * perUnit);
            plan.Add(new JObject { ["itemId"] = candidate.itemId, ["owned"] = Owned(candidate.itemId),
                ["available"] = Available(candidate.itemId), ["reserve"] = candidate.reserve, ["surplus"] = surplus,
                ["unitPrice"] = item.UnitPrice, ["saleRate"] = item.MarketSaleRate, ["perUnitLowerBound"] = perUnit });
        }
        WriteNew("skill-housing-normal-sale-plan.json", new JObject
        {
            ["origin"] = "public warehouse surplus policy -> real Haul -> physical export -> actual sale income",
            ["requiredMoney"] = requiredMoney, ["moneyBefore"] = query.Capture().CurrentMoney,
            ["conservativeProceeds"] = conservativeProceeds, ["candidates"] = plan,
            ["protected"] = new JArray("food", "water", "cloth", "lumber", "weapons", "seeds", "medicine"),
            ["trainingEligible"] = false, ["moneyOrItemInjected"] = false
        }.ToString(Newtonsoft.Json.Formatting.Indented));
        Require(query.Capture().CurrentMoney + conservativeProceeds >= requiredMoney,
            "HousingFundingUnavailable: real sellable surplus cannot cover the authored housing offer while retaining reserves.");
        Require(CharacterWorkRoleUtility.TryGetWork(owner, out AbilityWork work),
            "Normal housing sale requires the actual selected resident's work authority.");
        bool wasOffDuty = work.IsOffDuty;
        if (wasOffDuty) work.SetDutyState(AbilityWork.DutyState.OnDuty);
        bool prePickupCancellationVerified = false;
        try
        {
            for (int index = 0; index < reserves.Length && query.Capture().CurrentMoney < requiredMoney; index++)
            {
                var candidate = reserves[index];
                catalog.TryGetItem(candidate.itemId, out ResourceItemDefinitionSO item);
                int before = Owned(candidate.itemId);
                int quantity = Math.Min(Math.Min(Available(candidate.itemId), Math.Max(0, before - candidate.reserve)),
                    Mathf.CeilToInt((requiredMoney - query.Capture().CurrentMoney)
                        / (float)Mathf.FloorToInt(item.UnitPrice * item.MarketSaleRate)));
                if (quantity == 0) continue;
                ResourceStockPolicyData original = policies.GetOrCreate(candidate.itemId);
                int retained = before - quantity;
                var beforeKeys = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
                string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.ResourceStockPolicySaleResult).ProducerId;
                List<GameplayOutcomeSnapshot> Sales() => ReadAllOutcomes().Where(value => !beforeKeys.Contains(OutcomeKey(value))
                    && value.producerId == producer && value.facts.Any(fact => fact.factId == "item-definition-id"
                        && fact.value == candidate.itemId)).ToList();
                int Sold() => checked((int)Sales().Sum(value => value.metrics.Single(metric => metric.metricId == "quantity").value));
                Require(commands.AdjustStockPolicy(candidate.itemId, ResourceStockThreshold.Minimum,
                        retained - original.minimumStock).Succeeded
                    && commands.AdjustStockPolicy(candidate.itemId, ResourceStockThreshold.Target,
                        retained - original.targetStock).Succeeded
                    && commands.AdjustStockPolicy(candidate.itemId, ResourceStockThreshold.Maximum,
                        retained - original.maximumStock).Succeeded
                    && commands.CycleStockPolicy(candidate.itemId).Succeeded
                    && commands.ToggleStockPolicy(candidate.itemId).Succeeded,
                    "Public housing surplus policy commands failed.");
                ResourceStockPolicyData applied = policies.GetOrCreate(candidate.itemId);
                Require(applied.enabled && applied.surplusDisposition == StockSurplusDisposition.Sell
                    && applied.minimumStock == retained && applied.targetStock == retained && applied.maximumStock == retained,
                    "Public housing surplus policy did not retain the exact physical reserve.");
                phase = "skill-housing-normal-surplus-sale-" + candidate.itemId;
                float startedGame = clock.Time, startedReal = Time.realtimeSinceStartup;
                var liveFrames = new JArray();
                float nextSample = startedGame;
                void SampleSale()
                {
                    string destination = ResourceStockPolicySaleOutbox.FormatDestinationId(candidate.itemId);
                    liveFrames.Add(new JObject
                    {
                        ["gameTime"] = clock.Time, ["realTime"] = Time.realtimeSinceStartup,
                        ["ownedExcludingOutbound"] = policies.CountOwned(candidate.itemId),
                        ["physicalQuantity"] = Owned(candidate.itemId),
                        ["policy"] = JObject.FromObject(policies.GetOrCreate(candidate.itemId)),
                        ["destinationStacks"] = JArray.FromObject(items.GetAllStacks().Where(value => value.DestinationId == destination)
                            .Select(value => new { value.StackId, value.ItemId, value.Quantity, value.ReservedQuantity,
                                value.State, value.Position, value.ReservedByPersistentId })),
                        ["destinationIntents"] = JArray.FromObject(items.CaptureHaulDeliveryIntentsByDestination(destination)),
                        ["residents"] = JArray.FromObject(scope.Container.Resolve<ICharacterWorldQuery>().Characters
                            .Where(value => value != null && (value.Role == CharacterRole.Owner || CharacterWorkRoleUtility.IsWorker(value)))
                            .Select(value =>
                            {
                                AbilityHaul haul = value.GetComponent<AbilityHaul>();
                                return new { actorId = value.Identity.PersistentId, position = value.GetNowXY(),
                                    brain = value.Brain?.CurrentActionDebugLabel, brainPhase = value.Brain?.CurrentActionPhaseDetail,
                                    haulStage = haul?.CurrentExecutionStage, haulPlan = haul?.CurrentPlanSummary,
                                    hauling = haul?.IsHauling, lastFailure = haul?.LastFailureReason,
                                    operations = haul?.CaptureActiveHaulOperationIds().Select(operationId => new
                                    {
                                        operationId,
                                        hasIntent = items.TryCaptureHaulDeliveryIntent(operationId, out _)
                                    }).ToArray(),
                                    cargo = value.CarryInventory?.Items.Select(cargo => new
                                    { cargo.itemId, cargo.quantity, cargo.carriedStackId, cargo.ownerOperationId }).ToArray() };
                            })),
                        ["money"] = query.Capture().CurrentMoney
                    });
                    nextSample = clock.Time + 2f;
                }
                float oldMaximumDelta = Time.maximumDeltaTime;
                var priorHooks = new Dictionary<AbilityHaul, Action<WorldItemHaulPlan>>();
                if (!prePickupCancellationVerified)
                {
                    string saleDestination = ResourceStockPolicySaleOutbox.FormatDestinationId(candidate.itemId);
                    foreach (CharacterActor resident in scope.Container.Resolve<ICharacterWorldQuery>().Characters
                        .Where(value => value != null && (value.Role == CharacterRole.Owner || CharacterWorkRoleUtility.IsWorker(value))))
                    {
                        AbilityHaul haul = resident.GetComponent<AbilityHaul>();
                        if (haul == null) continue;
                        Action<WorldItemHaulPlan> previousHook = haul.DebugBeforeHaulRoutineStart;
                        priorHooks.Add(haul, previousHook);
                        haul.DebugBeforeHaulRoutineStart = plan =>
                        {
                            previousHook?.Invoke(plan);
                            if (prePickupCancellationVerified || plan?.PrimaryDestinationId != saleDestination) return;
                            string operation = plan.ReservedStackQuantities.Select(value => value.OwnerOperationId).Distinct().Single();
                            Require(items.TryCaptureHaulDeliveryIntent(operation, out HaulDeliveryIntentSaveData intent)
                                && !intent.HasCommittedPickup && resident.CarryInventory?.HasItems != true,
                                "Sale cancellation regression requires the real reserved plan before its first physical pickup.");
                            int moneyBefore = query.Capture().CurrentMoney;
                            Require(commands.ToggleStockPolicy(candidate.itemId).Succeeded,
                                "Public sale policy disable failed at the actual unpicked movement boundary.");
                            ResourceStockPolicyData disabled = policies.GetOrCreate(candidate.itemId);
                            Require(!disabled.enabled && string.IsNullOrEmpty(disabled.inputDestinationId)
                                && !haul.IsHauling && haul.CaptureActiveHaulOperationIds().Count == 0
                                && !items.TryCaptureHaulDeliveryIntent(operation, out _)
                                && items.CaptureHaulDeliveryIntentsByDestination(saleDestination).Count == 0
                                && items.GetAllStacks().Where(value => value.ItemId == candidate.itemId).All(value => value.ReservedQuantity == 0)
                                && haul.CaptureDeliveryIntentForSave() == null
                                && Owned(candidate.itemId) == before && query.Capture().CurrentMoney == moneyBefore
                                && Sales().Count == 0,
                                "Public sale disable retained dangling plan/intent/lease/input owner or changed physical stock/income.");
                            string cancelledWorld = saves.ToJson(saves.Capture());
                            Require(commands.ToggleStockPolicy(candidate.itemId).Succeeded,
                                "Public sale policy re-enable failed after exact pre-pickup cancellation.");
                            prePickupCancellationVerified = true;
                            WriteNew("skill-housing-sale-prepickup-cancel-world.json", cancelledWorld);
                            WriteNew("skill-housing-sale-prepickup-cancel-witness.json", new JObject
                            {
                                ["origin"] = "controlled public policy disable at real AbilityHaul reserved-before-pickup hook",
                                ["actorId"] = resident.Identity.PersistentId, ["operationId"] = operation,
                                ["destinationId"] = saleDestination, ["physicalQuantity"] = before,
                                ["moneyBefore"] = moneyBefore, ["moneyAfter"] = query.Capture().CurrentMoney,
                                ["disabledPolicy"] = JObject.FromObject(disabled),
                                ["reEnabledPolicy"] = JObject.FromObject(policies.GetOrCreate(candidate.itemId)),
                                ["planIntentLeaseRetired"] = true, ["strictOfficialCaptureSucceeded"] = true,
                                ["physicalPickupClaimed"] = false, ["saleOutcomeCreated"] = false, ["trainingEligible"] = false
                            }.ToString(Newtonsoft.Json.Formatting.Indented));
                        };
                    }
                }
                Time.maximumDeltaTime = 0.1f;
                gameSpeed.SetPaused(false);
                try
                {
                    SampleSale();
                    while (Owned(candidate.itemId) > retained && clock.Time - startedGame < needBalance.DayLengthSeconds * 2f
                        && Time.realtimeSinceStartup - startedReal < 180f)
                    {
                        yield return null;
                        if (clock.Time >= nextSample) SampleSale();
                        observe?.Invoke();
                    }
                }
                finally
                {
                    gameSpeed.SetPaused(true);
                    Time.maximumDeltaTime = oldMaximumDelta;
                    foreach (var hook in priorHooks) hook.Key.DebugBeforeHaulRoutineStart = hook.Value;
                    SampleSale();
                    WriteNew($"skill-housing-normal-sale-{index + 1:D3}-live-trace.json", liveFrames.ToString(Newtonsoft.Json.Formatting.Indented));
                }
                List<GameplayOutcomeSnapshot> sales = Sales();
                WriteNew($"skill-housing-normal-sale-{index + 1:D3}-observed.json", new JObject
                {
                    ["itemId"] = candidate.itemId, ["plannedQuantity"] = quantity, ["soldQuantity"] = Sold(),
                    ["before"] = before, ["after"] = Owned(candidate.itemId), ["retained"] = retained,
                    ["elapsedGameSeconds"] = clock.Time - startedGame, ["elapsedRealSeconds"] = Time.realtimeSinceStartup - startedReal,
                    ["policy"] = JObject.FromObject(policies.GetOrCreate(candidate.itemId)), ["sales"] = JArray.FromObject(sales),
                    ["pendingSales"] = JArray.FromObject(policies.Capture().pendingSales.Where(value => value.itemId == candidate.itemId)),
                    ["physicalStacks"] = JArray.FromObject(items.GetAllStacks().Where(value => value.ItemId == candidate.itemId)
                        .Select(value => new { value.StackId, value.ItemInstanceId, value.ItemId, value.Quantity,
                            value.AvailableQuantity, value.State, value.Position, value.DestinationId, value.ReservedByPersistentId })),
                    ["brain"] = owner.Brain.CurrentActionDebugLabel, ["brainPhase"] = owner.Brain.CurrentActionPhaseDetail,
                    ["moneyAfter"] = query.Capture().CurrentMoney, ["trainingEligible"] = false
                }.ToString(Newtonsoft.Json.Formatting.Indented));
                Require(Sold() == quantity && Owned(candidate.itemId) == retained
                    && !policies.Capture().pendingSales.Any(value => value.itemId == candidate.itemId)
                    && prePickupCancellationVerified,
                    "Actual housing surplus sale did not complete the exact physical quantity and acknowledged income.");
                var all = ReadAllOutcomes();
                foreach (GameplayOutcomeSnapshot sale in sales)
                {
                    string physicalKey = sale.facts.Single(value => value.factId == "physical-result-key").value;
                    GameplayOutcomeSnapshot physical = all.Single(value => new GameplayResultKey(value.producerId,
                        new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex).ToString() == physicalKey);
                    string transactionId = sale.facts.Single(value => value.factId == "economy-transaction-id").value;
                    EconomyTransactionRecord receipt = ledger.Records.Single(value => value.transactionId == transactionId);
                    string ownerOperation = sale.facts.Single(value => value.factId == "operation-id").value;
                    int proceeds = checked((int)sale.metrics.Single(value => value.metricId == "amount").value);
                    int sold = checked((int)sale.metrics.Single(value => value.metricId == "quantity").value);
                    Require(sale.status == GameplayOutcomeStatus.Succeeded && sale.publishedRevision > 0
                        && physical.status == GameplayOutcomeStatus.Succeeded && physical.publishedRevision > 0
                        && physical.operationId == ownerOperation && sale.rootOperationId == ownerOperation
                        && sale.causationRelationId == "stock-policy-sale-from-physical-transfer"
                        && sale.parentRunId == physical.runId && sale.parentSequence == physical.sequence
                        && all.Count(value => value.producerId == sale.producerId && value.operationId == sale.operationId
                            && value.commitRevision == sale.commitRevision && value.localResultIndex == sale.localResultIndex) == 1
                        && receipt.succeeded && receipt.kind == EconomyTransactionKind.SaleIncome
                        && receipt.sourceId == ownerOperation && receipt.targetId == candidate.itemId
                        && ledger.Records.Count(value => value.kind == EconomyTransactionKind.SaleIncome
                            && value.sourceId == ownerOperation && value.succeeded) == 1
                        && proceeds == Mathf.FloorToInt(sold * item.UnitPrice * item.MarketSaleRate)
                        && receipt.amount == proceeds && receipt.balanceAfter - receipt.balanceBefore == proceeds
                        && sale.metrics.Single(value => value.metricId == "previous").value == receipt.balanceBefore
                        && sale.metrics.Single(value => value.metricId == "current").value == receipt.balanceAfter,
                        "Actual housing sale lacks its single canonical physical child or exact real income receipt.");
                }
                Require(policies.SetPolicy(original, out string restoreFailure),
                    "Housing sale completed but original stock policy restoration failed: " + restoreFailure);
                WriteNew($"skill-housing-normal-sale-{index + 1:D3}-verified.json", new JObject
                {
                    ["sales"] = JArray.FromObject(sales),
                    ["physicalChildren"] = JArray.FromObject(all.Where(value => sales.Any(sale =>
                        sale.parentRunId == value.runId && sale.parentSequence == value.sequence))),
                    ["incomeReceipts"] = JArray.FromObject(ledger.Records.Where(value => value.kind == EconomyTransactionKind.SaleIncome
                        && sales.Any(sale => sale.facts.Any(fact => fact.factId == "operation-id" && fact.value == value.sourceId)))),
                    ["restoredPolicy"] = JObject.FromObject(policies.GetOrCreate(candidate.itemId)),
                    ["moneyAfter"] = query.Capture().CurrentMoney, ["trainingEligible"] = false
                }.ToString(Newtonsoft.Json.Formatting.Indented));
                WriteNew($"skill-housing-normal-sale-{index + 1:D3}-world.json", saves.ToJson(saves.Capture()));
            }
            Require(query.Capture().CurrentMoney >= requiredMoney,
                "HousingFundingUnavailable: actual completed surplus income does not cover the housing offer.");
        }
        finally
        {
            if (wasOffDuty) work.SetDutyState(AbilityWork.DutyState.OffDuty);
        }
    }


    private IEnumerator RunSkillBattleMechanicWitness(SkillMechanicWitness witness, Action Observe)
    {
        IGameClock gameClock = scope.Container.Resolve<IGameClock>();
        IOffenseBattleRuntime battle = scope.Container.Resolve<IOffenseBattleRuntime>();
        if (skillBattleDiagnostics && battle.HasActiveBattle)
        {
            CloseNonTargetSupportPreparationFixtures("before-restored-active-battle-witness");
            witness.resumedExistingBattle = true;
            witness.expeditionId = battle.Session.ExpeditionId;
            witness.targetId = battle.Session.TargetId;
            witness.consumerPath = report.executionOrigin =
                "official hashed active-battle restore -> public battle Ability command";
        }
        else
        {
            phase = "skill-normal-expedition-departure";
            OffenseWorldMapRuntime worldMap = UnityEngine.Object.FindFirstObjectByType<OffenseWorldMapRuntime>();
            OffenseExpeditionRuntime expeditions = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
            Require(worldMap != null && expeditions != null, "Normal expedition authority is missing.");
            CharacterCombatAbilityDefinition selectedAbility = CharacterCombatAbilityCatalog.GetAbilities(owner)
                .Single(value => value.Id == witness.skillId);
            int minimumFormationMembers = (selectedAbility.UsableFrom & OffenseFormationMask.Front) != 0 ? 1
                : (selectedAbility.UsableFrom & OffenseFormationMask.Middle) != 0 ? 3 : 5;
            string[] weaponPreparationResidentIds = skillNormalWeaponPreparation
                ? scope.Container.Resolve<ICharacterWorldQuery>().Characters
                    .Where(actor => actor != null && !actor.IsDead
                        && (actor.Role == CharacterRole.Owner || CharacterWorkRoleUtility.IsWorker(actor)))
                    .Select(actor => actor.Identity.PersistentId).ToArray()
                : Array.Empty<string>();
            Require(!skillNormalWeaponPreparation || weaponPreparationResidentIds.Length == 3,
                "Normal weapon preparation requires the original three approved residents before recruitment.");
            if (skillNormalWeaponPreparation && !skillResidentWeaponsPreparedBeforeAdmission
                && (sourceV9SkillReplay || SkillSupportContinuation))
            {
                // Prepare existing residents through actual production before
                // admission evaluates settled productivity. A front-row skill's
                // smaller party must not silently skip an explicitly requested
                // unfinished resident weapon from the restored checkpoint.
                CharacterActor[] residents = scope.Container.Resolve<ICharacterWorldQuery>().Characters
                    .Where(actor => actor != null
                        && weaponPreparationResidentIds.Contains(actor.Identity.PersistentId)).ToArray();
                yield return PrepareNormalSupportWeapons(residents, Observe, weaponPreparationResidentIds,
                    "skill-normal-resident-weapons");
                skillResidentWeaponsPreparedBeforeAdmission = true;
            }
            if ((sourceV9SkillReplay || SkillSupportContinuation) && expeditions.GetAvailableMemberActors().Count() < minimumFormationMembers)
                yield return RecruitSkillExpeditionParticipant(witness, Observe);
            phase = "skill-normal-expedition-departure";
            CharacterActor[] eligible = expeditions.GetAvailableMemberActors()
                .Where(value => value != null && !ReferenceEquals(value, owner)).ToArray();
            OffenseTargetDefinition target = worldMap.TargetDefinitions.Where(value => value != null
                    && worldMap.State.KnowTarget(value.id) && value.requiredMembers <= eligible.Length + 1)
                .OrderBy(value => value.campaignOrder).FirstOrDefault();
            Require(target != null, "No discovered authored expedition target admits this actual party.");
            int requiredPartySize = Math.Max(target.requiredMembers, minimumFormationMembers);
            Require(eligible.Length + 1 >= requiredPartySize,
                "Selected skill requires a legal authored formation with at least " + requiredPartySize
                + " eligible non-owner participants; actual available=" + (eligible.Length + 1));
            CharacterActor[] party = new[] { owner }.Concat(eligible.Take(requiredPartySize - 1)).ToArray();
            if (skillNormalWeaponPreparation)
                yield return PrepareNormalSupportWeapons(party, Observe, weaponPreparationResidentIds);
            CloseNonTargetSupportPreparationFixtures("before-registered-expedition-and-battle-witness");
            Require(expeditions.TryStartExpedition(target.id, party, out OffenseExpeditionRun expedition, out string launchFailure),
                "Normal expedition launch rejected: " + launchFailure);
            witness.partyActorIds = party.Select(value => value.Identity.PersistentId).ToList();
            int selectedMemberIndex = expedition.MemberStates.ToList().FindIndex(value => ReferenceEquals(value.Actor, owner));
            int legalMemberIndex = expedition.MemberStates.ToList().FindIndex(value =>
                (selectedAbility.UsableFrom & OffenseFormationUtility.ToMask(value.Formation)) != 0);
            Require(selectedMemberIndex >= 0 && legalMemberIndex >= 0,
                "Registered expedition has no legal formation slot for the selected skill.");
            if ((selectedAbility.UsableFrom & OffenseFormationUtility.ToMask(
                    expedition.MemberStates[selectedMemberIndex].Formation)) == 0)
            {
                Require(expeditions.TrySwapFormation(expedition.ExpeditionId, selectedMemberIndex, legalMemberIndex,
                        out string formationFailure), "Public expedition formation command rejected: " + formationFailure);
                witness.publicFormationSwap = true;
            }
            witness.legalSelectedSkillFormation = (selectedAbility.UsableFrom & OffenseFormationUtility.ToMask(
                expedition.MemberStates.Single(value => ReferenceEquals(value.Actor, owner)).Formation)) != 0;
            Require(witness.legalSelectedSkillFormation, "Public formation command did not publish the selected skill's legal slot.");
            witness.expeditionId = expedition.ExpeditionId;
            witness.targetId = target.id;
            Time.maximumDeltaTime = witness.physicalMaximumDeltaTime;
            gameSpeed.SetPaused(false);
            float departureStarted = gameClock.Time;
            while (!expedition.DepartureCompleted && gameClock.Time - departureStarted < 90f)
            {
                Observe();
                yield return null;
            }
            witness.departureCompleted = expedition.DepartureCompleted;
            Require(witness.departureCompleted, "Registered expedition did not complete actual physical departure.");
            Time.maximumDeltaTime = witness.calendarMaximumDeltaTime;
            if (!expedition.UsesWorldTravel)
            {
                for (int step = 0; step < 12 && !battle.HasActiveBattle; step++)
                {
                    OffenseRouteNode node = expedition.GetAvailableRouteNodes()
                        .OrderByDescending(value => value.StartsBattle).FirstOrDefault();
                    Require(node != null, "Normal authored route offered no reachable next node.");
                    Require(expeditions.TryChooseRouteNode(expedition.ExpeditionId, node.Id, out string routeFailure),
                        "Normal route command rejected: " + routeFailure);
                    if (!node.StartsBattle)
                        Require(expeditions.TryResolveCurrentNode(expedition.ExpeditionId, false, out _, out string nodeFailure),
                            "Normal route resolution rejected: " + nodeFailure);
                }
            }
            else
            {
                float travelStarted = gameClock.Time;
                while (!battle.HasActiveBattle && !expedition.IsComplete && gameClock.Time - travelStarted < 180f)
                {
                    Observe();
                    yield return null;
                }
            }
        }
        gameSpeed.SetPaused(true);
        Require(battle.HasActiveBattle, "Normal registered expedition reached no actual battle.");
        witness.battleId = battle.Session.BattleId;
        phase = "skill-actual-ability-and-restore";
        Require(PrepareSkillBattleTurn(battle, witness.skillId, witness.publicPreparationCommands),
            "Normal battle completed before selected skill's first usable turn.");
        string selectedSkillJson = JsonUtility.ToJson(owner.Progression.ActiveSkills.Single(value => value.id == witness.skillId));
        Dictionary<string, string> evidenceBefore = CurrentSkillEvidence(skillSelectionFacts, witness.actorId)
            .ToDictionary(value => value.Key, value => SkillEvidenceAnchor(value.Value), StringComparer.Ordinal);
        string beforeWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-skill-mechanic-before-world.json", beforeWorld);
        VerifySkillBattleCombatOwnerGuards(beforeWorld, witness);
        skillMechanicBeforeArtifact = GameplayModuleSelectionReplayProofV2Contract.Artifact(
            "u4-skill-mechanic-before-world.json", Encoding.UTF8.GetBytes(beforeWorld),
            JObject.Parse(JsonUtility.ToJson(battle.CapturePersistentState())));
        witness.applied = ExecuteSkillBattleCommand(battle, witness.skillId);
        witness.actualEffect = HasActualSkillHealthEffect(witness.applied);
        Require(witness.actualEffect && battle.HasActiveBattle, "Selected Ability produced no exact actual health effect or battle ended before round trip.");
        string battleJson = JsonUtility.ToJson(battle.CapturePersistentState());
        string appliedWorld = saves.ToJson(saves.Capture());
        string expeditionJson = JsonUtility.ToJson(DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(
            saves.FromJson(appliedWorld), OffenseAggregateSaveSection.Id).expedition);
        WriteNew("u4-skill-mechanic-applied-world.json", appliedWorld);
        skillMechanicAppliedArtifact = GameplayModuleSelectionReplayProofV2Contract.Artifact(
            "u4-skill-mechanic-applied-world.json", Encoding.UTF8.GetBytes(appliedWorld), JObject.Parse(battleJson));
        CharacterActor previous = owner;
        var combatEnvelope = saves.FromJson(appliedWorld);
        ICharacterEnvironmentPersistence environment = scope.Container.Resolve<ICharacterEnvironmentPersistence>();
        DungeonCharacterEnvironmentSaveData environmentBefore = environment.Capture();
        string environmentBeforeJson = JsonUtility.ToJson(environmentBefore);
        witness.restoredSections = combatEnvelope.sections.Count;
        Require(saves.TryRestore(combatEnvelope, out DungeonGameRestoreReport restored),
            "Official skill combat full-world restore failed: " + string.Join(" | ", restored.Errors));
        Require(scope.Container.Resolve<ICharacterWorldSaveService>().TryGetRestoredActor(witness.actorId, out owner),
            "Official skill battle restore did not publish the selected actor.");
        witness.officialFreshRestore = !ReferenceEquals(previous, owner);
        witness.battleExact = battle.HasActiveBattle && battleJson == JsonUtility.ToJson(battle.CapturePersistentState());
        witness.expeditionExact = expeditionJson == JsonUtility.ToJson(DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(
            saves.Capture(), OffenseAggregateSaveSection.Id).expedition);
        witness.skillExact = selectedSkillJson == JsonUtility.ToJson(owner.Progression.ActiveSkills.Single(value => value.id == witness.skillId));
        var restoredEvidence = CurrentSkillEvidence(skillSelectionFacts, witness.actorId);
        witness.evidenceExact = evidenceBefore.All(value => restoredEvidence.TryGetValue(value.Key, out var source)
            && value.Value == SkillEvidenceAnchor(source));
        witness.environmentExact = environmentBeforeJson == JsonUtility.ToJson(environment.Capture());
        Require(witness.officialFreshRestore && witness.battleExact && witness.expeditionExact && witness.skillExact && witness.evidenceExact,
            "Official combat restore changed actual battle/skill/exact evidence state.");
        Require(witness.environmentExact, "Official combat restore changed saved apparel/environment state.");
        // Controlled late-cleanup probe, not a simulated gameplay work completion.
        // Restored contexts are empty; cancelling an old routine must be a no-op.
        ICharacterEnvironmentWorkContext workEnvironment = scope.Container.Resolve<ICharacterEnvironmentWorkContext>();
        foreach (CharacterApparelPolicySaveData policy in environmentBefore.apparelPolicies)
        {
            workEnvironment.ClearWorkContext(new CharacterId(policy.characterId));
            workEnvironment.ClearWorkContext(new CharacterId(policy.characterId));
        }
        witness.restoredWorkContextClearStable = environmentBeforeJson == JsonUtility.ToJson(environment.Capture());
        Require(witness.restoredWorkContextClearStable, "Retired/repeated work cleanup reset restored apparel policy.");
        string restoredWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-skill-mechanic-restored-world.json", restoredWorld);
        skillMechanicRestoredArtifact = GameplayModuleSelectionReplayProofV2Contract.Artifact(
            "u4-skill-mechanic-restored-world.json", Encoding.UTF8.GetBytes(restoredWorld),
            JObject.Parse(JsonUtility.ToJson(battle.CapturePersistentState())));
        string restoredHash = SkillWorldStateHash(restoredWorld);
        var repeated = new OffenseBattleCommand(witness.applied.commandId, witness.actorId,
            OffenseBattleActionType.Ability, witness.applied.targetId, witness.skillId);
        witness.restoredDuplicateStable = !battle.TryExecuteCommand(repeated, out _)
            && restoredHash == SkillWorldStateHash(saves.ToJson(saves.Capture()));
        Require(witness.restoredDuplicateStable, "Restored duplicate Ability command changed world or outcomes.");
        if (!PrepareSkillBattleTurn(battle, witness.skillId, witness.publicPreparationCommands))
            yield return PrepareRestoredSkillReuseRoute(witness, battle, Observe);
        witness.reexecuted = ExecuteSkillBattleCommand(battle, witness.skillId);
        witness.restoredActualEffect = HasActualSkillHealthEffect(witness.reexecuted)
            && witness.reexecuted.effectKind == witness.applied.effectKind;
        Require(witness.restoredActualEffect, "Selected skill did not execute a second actual effect after official restore/next legal turn.");
        WriteNew("u4-skill-mechanic-reexecuted-world.json", saves.ToJson(saves.Capture()));
        WriteNew("u4-skill-mechanic-witness.json", JsonUtility.ToJson(witness, true));
        report.skillMechanicExecuted = true;
    }

    private IEnumerator RecoverSupportWorkFunThroughNormalVisits(CharacterActor[] actors, Action observe)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && (sourceV9SkillReplay || SkillSupportContinuation),
            "Normal support leisure preparation requires the explicit isolated support path.");
        Facility training = scope.Container.Resolve<IBuildingWorldQuery>().Buildings.OfType<Facility>()
            .FirstOrDefault(value => value != null && !value.isDestroy
                && value.SupportsFacilityRole(FacilityRole.Training)
                && value.BuildingData.GetAbility<BuildingNeedRecoveryAbility>()?.recovery.fun > 0f);
        Require(training != null, "Normal support leisure lacks an existing positive-FUN facility.");
        foreach (CharacterActor actor in actors)
        {
            Require(actor != null && !actor.IsDead,
                "Normal support leisure requires a live resident.");
            Require(CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work),
                "Normal support leisure requires a live resident's work owner.");
            Require(actor.Stats.TryGetConditionValue(CharacterCondition.FUN, out float initialFun),
                "Normal support leisure cannot read the resident's FUN authority.");
            float recovery = training.BuildingData.GetAbility<BuildingNeedRecoveryAbility>().recovery.fun;
            int maximumVisits = Mathf.CeilToInt(Mathf.Max(0f,
                actor.Stats.GetNeedResponse(CharacterCondition.FUN).resumeTarget - initialFun) / recovery) + 2;
            int attempts = 0;
            bool wasOffDuty = work.IsOffDuty;
            try
            {
                while (!CharacterNeedAiThresholds.IsSatisfied(actor, CharacterCondition.FUN))
                {
                    Require(++attempts <= maximumVisits,
                        "Normal support leisure did not recover FUN within its authored-effect visit budget.");
                    string prefix = $"skill-normal-fun-use-{++supportWorkFunVisitSequence:D3}";
                    // Fine physical ticks are required for actual meal/training
                    // approach coroutines too, not only craft and blueprint Haul.
                    Time.maximumDeltaTime = report.skillMechanic.physicalMaximumDeltaTime;
                    // A non-survival training visit must not race the next
                    // normal hunger response (r13 began at50.77, then eating
                    // correctly preempted it at46.29). Use the existing authored
                    // recovery target as preparation headroom, never lower hunger
                    // or force an ineligible meal. Wait for natural eligibility
                    // with the same timed-decay/day and240s bounds as nutrition.
                    IGameClock leisureClock = scope.Container.Resolve<IGameClock>();
                    ICharacterNeedBalanceRuntime leisureBalance = scope.Container.Resolve<ICharacterNeedBalanceRuntime>();
                    CharacterNeedResponseProfile hungerResponse = actor.Stats.GetNeedResponse(CharacterCondition.HUNGER);
                    float hungerAtPreparation = actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
                    int mealAttemptsAtPreparation = supportWorkMealVisitSequence;
                    int mealPreparationRounds = 0;
                    while (hungerAtPreparation < hungerResponse.resumeTarget)
                    {
                        // An ordinary unlatched meal may legitimately finish
                        // above routineStart but below this preparation target.
                        // Wait naturally and use the same shared three-attempt
                        // budget instead of forcing an ineligible second meal.
                        int remainingMealAttempts = 3 - (supportWorkMealVisitSequence - mealAttemptsAtPreparation);
                        Require(remainingMealAttempts > 0,
                            "Actual leisure meal preparation exhausted its existing three-attempt budget.");
                        ++mealPreparationRounds;
                        float hungerRate = scope.Container.Resolve<CharacterNeedStateService>()
                            .CalculateTimedDecay(actor, 1f).Hunger;
                        Require(hungerRate > 0f, "Leisure meal preparation has no natural hunger progression.");
                        float needBound = Mathf.Max(0f, hungerAtPreparation - hungerResponse.routineStart + 0.1f)
                            / hungerRate + leisureBalance.DayLengthSeconds;
                        float needStartedGame = leisureClock.Time, needStartedReal = Time.realtimeSinceStartup;
                        gameSpeed.SetSpeed(5);
                        gameSpeed.SetPaused(false);
                        phase = "skill-normal-fun-natural-meal-eligibility";
                        bool NeedsMeal() => CharacterNeedAiThresholds.GetRoutineUtility(actor, CharacterCondition.HUNGER) > 0f
                            || work.RequiresRoutineNeedRecovery(CharacterCondition.HUNGER);
                        while (!NeedsMeal() && actor.Stats.GetConditionValue(CharacterCondition.HUNGER) < hungerResponse.resumeTarget
                            && leisureClock.Time - needStartedGame < needBound
                            && Time.realtimeSinceStartup - needStartedReal < 240f)
                        {
                            observe();
                            yield return null;
                        }
                        Require(NeedsMeal() || actor.Stats.GetConditionValue(CharacterCondition.HUNGER) >= hungerResponse.resumeTarget,
                            "Leisure natural meal eligibility did not progress within the authored decay bound.");
                        WriteNew(prefix + $"-natural-meal-eligibility-{mealPreparationRounds:D2}.json", new JObject
                        {
                            ["actorId"] = actor.Identity.PersistentId, ["hungerBefore"] = hungerAtPreparation,
                            ["hungerAfter"] = actor.Stats.GetConditionValue(CharacterCondition.HUNGER),
                            ["routineStart"] = hungerResponse.routineStart, ["resumeTarget"] = hungerResponse.resumeTarget,
                            ["timedDecayRate"] = hungerRate, ["gameBound"] = needBound,
                            ["remainingMealAttemptBudget"] = remainingMealAttempts,
                            ["gameSeconds"] = leisureClock.Time - needStartedGame,
                            ["hungerInjected"] = false, ["trainingEligible"] = false
                        }.ToString());
                        yield return RecoverSupportWorkHungerThroughNormalMeals(actor, observe, remainingMealAttempts);
                        hungerAtPreparation = actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
                    }
                    Require(hungerAtPreparation >= hungerResponse.resumeTarget,
                        "Actual leisure meal preparation did not provide its authored hunger headroom.");
                    if (CharacterNeedAiThresholds.IsSatisfied(actor, CharacterCondition.FUN)) break;
                    Require(actor.Stats.TryGetConditionValue(CharacterCondition.FUN, out float before),
                        "Normal support leisure lost its FUN authority.");
                    var command = new CommandWitness
                    {
                        command = "public-off-duty-cycle->registered-training-for-fun",
                        actorId = actor.Identity.PersistentId,
                        facilityId = training.RequirePersistentInstanceId().Value,
                        usesBefore = training.FacilityState.completedUses,
                        activeUseCheckpointFile = prefix + "-active-world.json"
                    };
                    WriteNew(prefix + "-planned.json", new JObject
                    {
                        ["actorId"] = command.actorId, ["facilityId"] = command.facilityId,
                        ["funBefore"] = before,
                        ["hungerBefore"] = actor.Stats.TryGetConditionValue(CharacterCondition.HUNGER, out float hunger)
                            ? (JToken)hunger : JValue.CreateNull(),
                        ["status"] = "PLANNED_NOT_COMPLETED", ["trainingEligible"] = false
                    }.ToString());
                    gameSpeed.SetSpeed(5);
                    gameSpeed.SetPaused(false);
                    yield return CompleteTrainingThroughRegisteredAi(actor, training, command,
                        observe: observe, requireExactUseParent: true);
                    Require(actor.Stats.TryGetConditionValue(CharacterCondition.FUN, out float after)
                            && after > before && command.exactUseParentBound,
                        "Normal support leisure lacks actual FUN recovery and one exact completed use parent.");
                    WriteNew(prefix + "-witness.json", new JObject
                    {
                        ["origin"] = "real registered facility use; no FUN injection or need-policy change",
                        ["funBefore"] = before, ["funAfter"] = after,
                        ["authoredResumeTarget"] = actor.Stats.GetNeedResponse(CharacterCondition.FUN).resumeTarget,
                        ["visit"] = JObject.Parse(JsonUtility.ToJson(command)),
                        ["trainingEligible"] = false, ["naturalPlayClaimed"] = false
                    }.ToString());
                }
            }
            finally
            {
                work.SetDutyState(wasOffDuty ? AbilityWork.DutyState.OffDuty : AbilityWork.DutyState.OnDuty);
            }
        }
    }

    private IEnumerator RecoverSupportWorkHungerThroughNormalMeals(CharacterActor actor, Action observe,
        int maximumAttempts = 3)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && (sourceV9SkillReplay || SkillSupportContinuation),
            "Normal work meal care requires the explicit isolated support path.");
        Require(actor != null && !actor.IsDead,
            "Normal work meal care requires a live actor.");
        Require(CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work),
            "Normal work meal care requires the actual live work owner.");
        Require(maximumAttempts is > 0 and <= 3,
            "Normal work meal care requires its existing bounded attempt budget.");
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        ICharacterNeedBalanceRuntime balance = scope.Container.Resolve<ICharacterNeedBalanceRuntime>();
        Facility[] dining = scope.Container.Resolve<IBuildingWorldQuery>().Buildings.OfType<Facility>()
            .Where(value => value != null && !value.isDestroy && value.SupportsFacilityRole(FacilityRole.Meal)
                && CharacterVisitPolicy.CanVisitBuilding(actor, value, false, out _))
            .OrderBy(value => Vector2.Distance(actor.transform.position, value.transform.position)).ToArray();
        Require(dining.Length > 0, "Normal work meal care has no legitimate authored dining facility.");
        bool wasOffDuty = work.IsOffDuty;
        int attempts = 0;
        // An unlatched worker may resume above routineStart after one real meal.
        // Only a work interruption's existing hunger latch requires resumeTarget.
        bool NeedsMealCare() => CharacterNeedAiThresholds.GetRoutineUtility(actor, CharacterCondition.HUNGER) > 0f
            || work.RequiresRoutineNeedRecovery(CharacterCondition.HUNGER);
        try
        {
            while (NeedsMealCare())
            {
                Require(++attempts <= maximumAttempts, "Normal work meal care exceeded its remaining three-attempt budget.");
                string prefix = $"skill-normal-work-meal-{++supportWorkMealVisitSequence:D3}";
                float before = actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
                bool latchedBefore = work.RequiresRoutineNeedRecovery(CharacterCondition.HUNGER);
                HashSet<string> beforeKeys = ReadEntityOutcomes("character", actor.Identity.PersistentId)
                    .Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
                List<GameplayOutcomeSnapshot> NewMeals() => ReadEntityOutcomes("character", actor.Identity.PersistentId)
                    .Where(value => !beforeKeys.Contains(OutcomeKey(value))
                        && IsPhysicalMealConsumptionWitness(value, actor.Identity.PersistentId)).ToList();
                float startedGame = clock.Time, startedReal = Time.realtimeSinceStartup;
                Facility meal = null;
                CharacterConsumablesFailure unavailable = default;
                bool FindPhysicalMeal()
                {
                    foreach (Facility candidate in dining)
                        if (candidate.HasMealAvailableFor(actor, out unavailable) && !unavailable.IsFailure)
                        {
                            meal = candidate;
                            return true;
                        }
                    return false;
                }
                gameSpeed.SetSpeed(5);
                gameSpeed.SetPaused(false);
                phase = "skill-normal-work-meal-delivery";
                bool ready = FindPhysicalMeal();
                while ((!ready || actor.Brain.IsExternallyDrivenActionActive) && NewMeals().Count == 0
                       && clock.Time - startedGame < balance.DayLengthSeconds * 2f
                       && Time.realtimeSinceStartup - startedReal < 60f)
                {
                    Require(ready || unavailable.Code == CharacterConsumablesFailureCode.DeliveryPending,
                        "Normal work meal unavailable: " + unavailable.Code);
                    observe();
                    yield return null;
                    ready = FindPhysicalMeal();
                }
                var visit = new CommandWitness();
                if (NewMeals().Count == 0)
                {
                    Require(ready && !actor.Brain.IsExternallyDrivenActionActive,
                        "Normal work meal did not physically arrive or existing self-care did not finish.");
                    visit.command = "public-off-duty-cycle->registered-work-meal-use";
                    visit.actorId = actor.Identity.PersistentId;
                    visit.facilityId = meal.RequirePersistentInstanceId().Value;
                    visit.usesBefore = meal.FacilityState.completedUses;
                    visit.activeUseCheckpointFile = prefix + "-active-world.json";
                    yield return CompleteTrainingThroughRegisteredAi(actor, meal, visit, observe: observe,
                        mealVisit: true, recordRetryableMealSupplyCancellation: true);
                }
                List<GameplayOutcomeSnapshot> physicalMeals = NewMeals();
                float after = actor.Stats.GetConditionValue(CharacterCondition.HUNGER);
                WriteNew(prefix + "-witness.json", new JObject
                {
                    ["origin"] = "actual registered or already-running meal; no hunger, food, HP or ledger injection",
                    ["actorId"] = actor.Identity.PersistentId, ["hungerBefore"] = before, ["hungerAfter"] = after,
                    ["routineStart"] = actor.Stats.GetNeedResponse(CharacterCondition.HUNGER).routineStart,
                    ["resumeTarget"] = actor.Stats.GetNeedResponse(CharacterCondition.HUNGER).resumeTarget,
                    ["workHungerRecoveryLatchedBefore"] = latchedBefore,
                    ["workHungerRecoveryLatchedAfter"] = work.RequiresRoutineNeedRecovery(CharacterCondition.HUNGER),
                    ["routineUtilityAfter"] = CharacterNeedAiThresholds.GetRoutineUtility(actor, CharacterCondition.HUNGER),
                    ["visit"] = JObject.Parse(JsonUtility.ToJson(visit)),
                    ["newPhysicalMeals"] = new JArray(physicalMeals.Select(value => JObject.Parse(JsonUtility.ToJson(value)))),
                    ["elapsedGameSeconds"] = clock.Time - startedGame,
                    ["trainingEligible"] = false, ["naturalPlayClaimed"] = false
                }.ToString());
                if (visit.retryableMealSupplyCancelled)
                {
                    Require(visit.exactCancelledUseParentBound && physicalMeals.Count == 0,
                        "Normal work meal supply cancellation is not exact or also claims consumption.");
                    continue;
                }
                Require(physicalMeals.Count == 1 && after > before
                        && (string.IsNullOrEmpty(visit.actorId) || visit.exactUseParentBound
                            || visit.actualMealSupersededUse && visit.exactCancelledUseParentBound),
                    "Normal work meal has no unique physical consumption/recovery and exact visit terminal.");
                while (actor.Brain.IsExternallyDrivenActionActive
                       && clock.Time - startedGame < balance.DayLengthSeconds * 2f
                       && Time.realtimeSinceStartup - startedReal < 60f)
                {
                    observe();
                    yield return null;
                }
                Require(!actor.Brain.IsExternallyDrivenActionActive,
                    "Actual physical meal committed but its natural self-care owner did not finish.");
            }
        }
        finally
        {
            work.SetDutyState(wasOffDuty ? AbilityWork.DutyState.OffDuty : AbilityWork.DutyState.OnDuty);
        }
    }

    private IEnumerator RunCompletedCraftDeliveryDiagnostics()
    {
        Require(completedCraftDeliveryDiagnostics && Application.isBatchMode
                && DungeonPersistencePaths.IsIsolatedVerification
                && string.Equals(report.continuation?.sourceSha256,
                    "55c23a4eb50221c22a0f7477022051c6032ad54009c24e9bec0d26932bc04e48",
                    StringComparison.OrdinalIgnoreCase),
            "Completed craft delivery requires the exact preserved r374 official checkpoint.");
        CancelOutstandingTransport();
        queue.ConfigureIsolatedAutomaticDispatch(false);
        skillAutomaticDispatchDeferred = true;
        const string actorId = "character:staff:1:02";
        const string recipientId = "character:staff:1:01";
        const string instanceId = "item-instance:93a4d4f91e8343d380bcdacf20b9d888";
        const string stackId = "stack:ad142249cf414f11ae545fc907e0c047";
        const string orderId = "combat-craft:00000001";
        ICombatEquipmentRuntime equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        ICharacterWorldSaveService characters = scope.Container.Resolve<ICharacterWorldSaveService>();
        CharacterActor carrier = null;
        AbilityHaul haul = null;
        Require(characters.TryGetRestoredActor(actorId, out carrier)
                && (haul = carrier.GetComponent<AbilityHaul>()) != null && haul.HasBoundDeliveryIntent,
            "Exact crafted cargo did not restore its bound real Haul recipient.");
        Require(characters.TryGetRestoredActor(recipientId, out CharacterActor recipient)
                && (!equipment.TryGetActiveWeapon(recipientId, out CombatWeaponSnapshot priorWeapon)
                    || priorWeapon == null || string.IsNullOrWhiteSpace(priorWeapon.InstanceId)),
            "Original unarmed craft recipient differs; the already armed carrier is not substituted.");
        bool ExactState(WorldItemStackState state, CombatEquipmentWorldState equipmentState) =>
            equipment.TryGetInstance(instanceId, out CombatEquipmentInstance current)
            && current.worldState == equipmentState && current.sourceStackId == stackId
            && items.GetAllStacks().Count(stack => stack.StackId == stackId
                && stack.ItemInstanceId == instanceId && stack.Quantity == 1 && stack.State == state) == 1;
        GameplayOutcomeSnapshot[] Parents() => ReadAllOutcomes().Where(value =>
            value.producerId == "ledger.equipment.combat-craft-completed"
            && value.status == GameplayOutcomeStatus.Succeeded
            && value.facts.Any(fact => fact.factId == "output-stacks"
                && fact.value == stackId + "=equipment-item:weapon:spear=1=" + instanceId)).ToArray();
        Require(ExactState(WorldItemStackState.Carried, CombatEquipmentWorldState.Carried)
                && Parents().Length == 1 && !equipment.CraftQueue.Any(value => value.orderId == orderId),
            "Preserved r374 craft/pickup/unique completion evidence differs; no new craft is substituted.");
        string parentKey = OutcomeKey(Parents()[0]);
        int Lumber() => items.GetAllStacks().Where(stack => stack.ItemId == "material:lumber")
            .Sum(stack => stack.Quantity);
        int lumberBefore = Lumber();
        var trace = new JObject
        {
            ["scope"] = "same historically completed craft output; current real bound AIHaul, public equipment pickup and official fresh restore",
            ["actorId"] = actorId, ["instanceId"] = instanceId, ["stackId"] = stackId,
            ["recipientId"] = recipientId,
            ["orderId"] = orderId, ["completionOutcomeKey"] = parentKey,
            ["status"] = "INCOMPLETE_NO_PASS_CLAIMED", ["trainingEligible"] = false,
            ["naturalPlayClaimed"] = false, ["humanApprovalClaimed"] = false,
            ["newCraftQueued"] = false, ["itemsInjected"] = false,
            ["fixture"] = "human-authorized isolated initial carrier health and maintained non-target needs; no healing/meal outcome claimed",
            ["initialHealth"] = carrier.CurrentHealth, ["initialInjury"] = carrier.InjurySeverity,
            ["frames"] = new JArray()
        };
        try
        {
            gameSpeed.SetPaused(true);
            WriteNew("completed-craft-delivery-before-world.json", saves.ToJson(saves.Capture()));
            scope.Container.Resolve<ICharacterBodyHealthCommand>().ConfigureVitals(carrier, carrier.MaxHealth, true);
            Require(carrier.InjurySeverity < 0.1f,
                "Explicit initial health preparation did not remove unrelated recovery need.");
            trace["preparedHealth"] = carrier.CurrentHealth;
            trace["preparedInjury"] = carrier.InjurySeverity;
            Require(CharacterWorkRoleUtility.TryGetWork(carrier, out AbilityWork worker),
                "Exact delivery recipient has no work role.");
            worker.ClearPriorityWorkTarget();
            worker.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
            float started = clock.Time, nextSample = started;
            float lastAdvancingGameTime = started, lastAdvancingRealTime = Time.realtimeSinceStartup;
            void Observe()
            {
                if (clock.Time > lastAdvancingGameTime)
                {
                    lastAdvancingGameTime = clock.Time;
                    lastAdvancingRealTime = Time.realtimeSinceStartup;
                }
                Require(Time.realtimeSinceStartup - lastAdvancingRealTime < 30f,
                    "Delivery/pickup simulation is not advancing; paused=" + gameSpeed.IsPaused
                        + "; clockScale=" + timeScale.Scale + "; phase=" + phase);
                foreach (CharacterActor resident in new[] { carrier, recipient })
                foreach (CharacterCondition need in new[] { CharacterCondition.HUNGER, CharacterCondition.FUN,
                    CharacterCondition.THIRST, CharacterCondition.SLEEP, CharacterCondition.EXCRETION, CharacterCondition.HYGIENE })
                {
                    float current = resident.Stats.GetConditionValue(need);
                    if (current < 95f) resident.Stats.ChangesStat(need, 100f - current);
                }
                if (clock.Time < nextSample) return;
                ((JArray)trace["frames"]).Add(new JObject
                {
                    ["elapsedGameSeconds"] = clock.Time - started,
                    ["brain"] = carrier.Brain.CurrentActionDebugLabel,
                    ["position"] = carrier.transform.position.ToString("F3"),
                    ["health"] = carrier.CurrentHealth, ["injury"] = carrier.InjurySeverity,
                    ["boundIntent"] = haul.HasBoundDeliveryIntent, ["isHauling"] = haul.IsHauling,
                    ["publicGamePaused"] = gameSpeed.IsPaused, ["clockScale"] = timeScale.Scale,
                    ["stage"] = haul.CurrentExecutionStage, ["plan"] = haul.CurrentPlanSummary,
                    ["failure"] = haul.LastFailureReason
                });
                nextSample = clock.Time + 5f;
            }
            phase = "completed-craft-bound-delivery";
            Observe();
            Time.maximumDeltaTime = 0.1f;
            gameSpeed.SetSpeed(5);
            gameSpeed.SetPaused(false);
            carrier.Brain.RequestImmediateReplan(clearFailures: false);
            while (!ExactState(WorldItemStackState.Stored, CombatEquipmentWorldState.Stored)
                && clock.Time - started < 90f)
            { Observe(); yield return null; }
            Require(ExactState(WorldItemStackState.Stored, CombatEquipmentWorldState.Stored),
                "Same completed craft output did not reach exact Stored custody within the unchanged 90-game-second boundary.");
            gameSpeed.SetPaused(true);
            trace["deliveryGameSeconds"] = clock.Time - started;
            WriteNew("completed-craft-delivery-stored-world.json", saves.ToJson(saves.Capture()));
            OffenseExpeditionRuntime expeditions = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
            Require(expeditions != null && expeditions.TryEquipEquipment(recipient, "weapon:spear", out _),
                "Public same-output equipment pickup was rejected.");
            bool Equipped() => equipment.TryGetActiveWeapon(recipientId, out CombatWeaponSnapshot active)
                && active != null && active.InstanceId == instanceId;
            float pickupStarted = clock.Time;
            phase = "completed-craft-public-pickup";
            gameSpeed.SetPaused(false);
            while (!Equipped() && clock.Time - pickupStarted < 90f)
            { Observe(); yield return null; }
            Require(Equipped(), "Public movement/pickup did not equip the same crafted instance.");
            gameSpeed.SetPaused(true);
            string equippedWorld = saves.ToJson(saves.Capture());
            WriteNew("completed-craft-delivery-equipped-world.json", equippedWorld);
            CharacterActor previous = carrier;
            CharacterActor previousRecipient = recipient;
            Require(saves.TryRestore(saves.FromJson(equippedWorld), out DungeonGameRestoreReport restored),
                "Completed craft delivery official fresh restore failed: " + string.Join(" | ", restored.Errors));
            Require(characters.TryGetRestoredActor(actorId, out carrier) && !ReferenceEquals(previous, carrier)
                    && (haul = carrier.GetComponent<AbilityHaul>()) != null && !haul.HasBoundDeliveryIntent
                    && characters.TryGetRestoredActor(recipientId, out recipient)
                    && !ReferenceEquals(previousRecipient, recipient)
                    && Equipped() && Parents().Length == 1 && OutcomeKey(Parents()[0]) == parentKey
                    && Lumber() == lumberBefore && !equipment.CraftQueue.Any(value => value.orderId == orderId),
                "Fresh restore changed output identity, retained completed haul, re-consumed BOM or duplicated craft completion.");
            WriteNew("completed-craft-delivery-restored-world.json", saves.ToJson(saves.Capture()));
            trace["freshOwner"] = true;
            trace["completedIntentRetired"] = true;
            trace["additionalLumberConsumption"] = 0;
            trace["completionOutcomeCount"] = Parents().Length;
            trace["status"] = "PASS_CURRENT_DELIVERY_PICKUP_FRESH_RESTORE_OF_HISTORICAL_CRAFT";
            completedCraftDeliveryVerified = true;
        }
        finally
        {
            gameSpeed.SetPaused(true);
            trace["lastObservedPhase"] = phase;
            WriteNew("completed-craft-delivery-witness.json", trace.ToString());
        }
    }

    private IEnumerator PrepareNormalSupportWeapons(CharacterActor[] party, Action observe,
        IReadOnlyCollection<string> approvedResidentIds, string artifactPrefix = "skill-normal-weapons")
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && (sourceV9SkillReplay || SkillSupportContinuation),
            "Normal support weapon preparation is an explicitly selected isolated game path.");
        if (skillEquipmentPreparationFixture)
        {
            // Keep the same public preparation scheduling as the normal
            // craft/haul branch. Supplying only equipment must not skip Haul
            // priority for the other residents who can claim the blueprint.
            foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>().Characters)
                if (actor != null && !actor.IsDead && CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork hauler))
                    hauler.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
            PrepareSupportEquipmentTestFixture(party, artifactPrefix);
            yield break;
        }
        const string weaponId = "weapon:spear", materialId = "material:wood";
        ICombatEquipmentRuntime equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        IBuildingWorldQuery buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        OffenseExpeditionRuntime expeditions = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
        var trace = new JObject
        {
            ["origin"] = "explicit normal public shop/construction/craft/haul/equipment pickup commands",
            ["weaponId"] = weaponId, ["materialId"] = materialId,
            ["partyActorIds"] = new JArray(party.Select(actor => actor.Identity.PersistentId)),
            ["status"] = "PREPARING", ["itemsOrHealthInjected"] = false,
            ["enemyOrCombatResultChanged"] = false, ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false, ["naturalPlayClaimed"] = false,
            ["crafts"] = new JArray(), ["publicCommands"] = new JArray()
        };
        var crafts = (JArray)trace["crafts"];
        var commands = (JArray)trace["publicCommands"];
        int Quantity(string itemId) => items.GetAllStacks()
            .Where(stack => stack != null && stack.ItemId == itemId && stack.Quantity > 0)
            .Sum(stack => stack.Quantity);
        bool Supports(BuildableObject facility) => facility != null && !facility.isDestroy
            && facility.BuildingData != null
            && facility.BuildingData.GetAbility<BuildingEquipmentCraftingAbility>()
                ?.CraftableEquipmentIds.Contains(weaponId) == true;
        IEnumerator AwaitStoredCraftOutput(string instanceId, JObject sample)
        {
            var frames = new JArray();
            sample["deliveryFrames"] = frames;
            float started = clock.Time, nextSample = started;
            bool Stored() => equipment.TryGetInstance(instanceId, out CombatEquipmentInstance current)
                && current.worldState == CombatEquipmentWorldState.Stored;
            void ObserveDelivery()
            {
                observe();
                if (clock.Time < nextSample) return;
                var actors = new JArray();
                foreach (CharacterActor actor in party)
                {
                    AbilityHaul haul = actor.GetComponent<AbilityHaul>();
                    actors.Add(new JObject
                    {
                        ["actorId"] = actor.Identity.PersistentId,
                        ["position"] = actor.transform.position.ToString("F3"),
                        ["brain"] = actor.Brain.CurrentActionDebugLabel,
                        ["health"] = actor.CurrentHealth, ["injury"] = actor.InjurySeverity,
                        ["boundIntent"] = haul != null && haul.HasBoundDeliveryIntent,
                        ["isHauling"] = haul != null && haul.IsHauling,
                        ["stage"] = haul?.CurrentExecutionStage,
                        ["plan"] = haul?.CurrentPlanSummary, ["failure"] = haul?.LastFailureReason
                    });
                }
                frames.Add(new JObject { ["gameSeconds"] = clock.Time - started,
                    ["actors"] = actors });
                nextSample = clock.Time + 5f;
            }
            Time.maximumDeltaTime = 0.1f;
            gameSpeed.SetSpeed(5);
            gameSpeed.SetPaused(false);
            while (!Stored() && clock.Time - started < 90f)
            { ObserveDelivery(); yield return null; }
            sample["deliveryGameSeconds"] = clock.Time - started;
            ObserveDelivery();
            Require(Stored(), "Crafted support weapon did not reach real stored custody via Haul: " + instanceId);
            gameSpeed.SetPaused(true);
        }
        WriteNew(artifactPrefix + "-before-world.json", saves.ToJson(saves.Capture()));
        try
        {
            phase = "skill-normal-weapon-facility-preparation";
            Require(equipment.IsDefinitionUnlocked(weaponId, out string unlockFailure),
                "Normal support weapon is not unlocked: " + unlockFailure);
            Require(equipment.TryGetDefinition(weaponId, out CombatEquipmentDefinitionSO definition),
                "Authored normal support weapon is missing.");
            Require(scope.Container.Resolve<IResourceEconomyContentCatalog>()
                    .TryGetMaterial(materialId, out CraftMaterialDefinitionSO material),
                "Authored normal support material is missing.");
            Require(CombatCraftConcreteInputProjection.TryCapture(definition, weaponId, material,
                    out var bom, out string bomFailure), "Normal weapon BOM unavailable: " + bomFailure);
            var pendingActors = party.Where(actor => !equipment.TryGetActiveWeapon(
                actor.Identity.PersistentId, out CombatWeaponSnapshot active)
                || active == null || string.IsNullOrWhiteSpace(active.InstanceId)).ToArray();
            // A resumed world may already contain the previous attempt's real
            // crafted output, including a restored bound Haul still in flight.
            // Finish that exact delivery/pickup; do not queue/consume another BOM.
            foreach (CombatEquipmentInstance stored in equipment.Instances.Where(value =>
                value.definitionId == weaponId && value.materialId == materialId
                && (value.worldState == CombatEquipmentWorldState.Stored
                    || value.worldState == CombatEquipmentWorldState.Carried
                    || value.worldState == CombatEquipmentWorldState.Loose)))
            {
                if (pendingActors.Length == 0) break;
                string exactOutput = stored.sourceStackId + "=equipment-item:" + weaponId + "=1=" + stored.instanceId;
                GameplayOutcomeSnapshot[] priorCompletions = ReadAllOutcomes().Where(value =>
                    value.producerId == "ledger.equipment.combat-craft-completed"
                    && value.status == GameplayOutcomeStatus.Succeeded
                    && value.facts.Any(fact => fact.factId == "output-stacks" && fact.value == exactOutput)).ToArray();
                if (priorCompletions.Length == 0) continue;
                WorldItemStackState expectedState = stored.worldState == CombatEquipmentWorldState.Stored
                    ? WorldItemStackState.Stored : stored.worldState == CombatEquipmentWorldState.Carried
                    ? WorldItemStackState.Carried : WorldItemStackState.Loose;
                Require(priorCompletions.Length == 1 && items.GetAllStacks().Count(stack =>
                    stack.StackId == stored.sourceStackId && stack.ItemInstanceId == stored.instanceId
                    && stack.State == expectedState && stack.Quantity == 1) == 1,
                    "Previously crafted support weapon lacks one exact physical stack and completion parent.");
                CharacterActor recipient = pendingActors[0];
                int[] inputBeforeReuse = bom.Inputs.Select(input => Quantity(input.ItemId)).ToArray();
                var reusedCraft = new JObject { ["recipientId"] = recipient.Identity.PersistentId,
                    ["instanceId"] = stored.instanceId, ["sourceStackId"] = stored.sourceStackId,
                    ["initialWorldState"] = stored.worldState.ToString(),
                    ["completionOutcome"] = JObject.Parse(JsonUtility.ToJson(priorCompletions[0])),
                    ["newCraftQueued"] = false, ["status"] = "AWAITING_PRIOR_CRAFT_DELIVERY" };
                crafts.Add(reusedCraft);
                if (stored.worldState != CombatEquipmentWorldState.Stored)
                {
                    phase = "skill-normal-existing-crafted-weapon-delivery";
                    yield return AwaitStoredCraftOutput(stored.instanceId, reusedCraft);
                }
                Require(items.GetAllStacks().Count(stack => stack.StackId == stored.sourceStackId
                        && stack.ItemInstanceId == stored.instanceId && stack.Quantity == 1
                        && stack.State == WorldItemStackState.Stored) == 1,
                    "Previously crafted weapon delivery did not retain the exact physical stored stack.");
                reusedCraft["storedSourceStackId"] = stored.sourceStackId;
                phase = "skill-normal-existing-crafted-weapon-pickup";
                Require(expeditions.TryEquipEquipment(recipient, weaponId, out string reuseFailure),
                    "Public previously crafted weapon pickup rejected: " + reuseFailure);
                gameSpeed.SetPaused(false);
                float pickupStarted = clock.Time;
                while ((!equipment.TryGetActiveWeapon(recipient.Identity.PersistentId, out CombatWeaponSnapshot activeReuse)
                        || activeReuse == null || activeReuse.InstanceId != stored.instanceId)
                    && clock.Time - pickupStarted < 90f)
                { observe(); yield return null; }
                Require(equipment.TryGetActiveWeapon(recipient.Identity.PersistentId, out CombatWeaponSnapshot equippedReuse)
                        && equippedReuse.InstanceId == stored.instanceId
                        && inputBeforeReuse.SequenceEqual(bom.Inputs.Select(input => Quantity(input.ItemId)))
                        && ReadAllOutcomes().Count(value => OutcomeKey(value) == OutcomeKey(priorCompletions[0])) == 1,
                    "Previously crafted weapon pickup changed identity, consumed another BOM or duplicated its completion.");
                commands.Add("equipment-pickup-existing-crafted:" + recipient.Identity.PersistentId + ":" + stored.instanceId);
                reusedCraft["additionalCraftInputConsumption"] = 0;
                reusedCraft["activeInstanceId"] = equippedReuse.InstanceId;
                reusedCraft["status"] = "ACTUAL_PRIOR_CRAFT_STORED_PICKUP_EQUIPPED";
                gameSpeed.SetPaused(true);
                pendingActors = pendingActors.Skip(1).ToArray();
            }
            trace["unarmedActorIds"] = new JArray(pendingActors.Select(actor => actor.Identity.PersistentId));
            if (pendingActors.Length == 0)
            {
                trace["status"] = crafts.Count == 0 ? "ALREADY_EQUIPPED_NO_NEW_CRAFT_CLAIMED"
                    : "PASS_PRIOR_CRAFT_DELIVERY_PICKUP_NO_NEW_CRAFT_CLAIMED";
                WriteNew(artifactPrefix + "-equipped-world.json", saves.ToJson(saves.Capture()));
                yield break;
            }
            foreach (var input in bom.Inputs)
                Require(Quantity(input.ItemId) >= checked(input.Amount * pendingActors.Length),
                    "NormalWeaponMaterialUnavailable: " + input.ItemId + "; required="
                    + input.Amount * pendingActors.Length + "; actual=" + Quantity(input.ItemId));
            BuildableObject forge = buildings.Buildings.FirstOrDefault(Supports);
            if (forge == null && skillOneCraftingBenchFixture)
                forge = PrepareApprovedSingleSupportCraftingBench(trace);
            if (forge == null)
            {
                // The real shop delivers a paid installation kit. Construction
                // consumes that exact kit through WorkAmountSystem, not a QA
                // material grant or a forged delivery offer.
                IShopFeatureQueryService shop = scope.Container.Resolve<IShopFeatureQueryService>();
                ShopFeatureSurfaceModel model = shop.Capture();
                BuildingSO forgeDefinition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                    "Assets/Resources/SO/Building/Modular/S08_대장작업대.asset");
                Require(forgeDefinition != null, "Authored support forge is missing.");
                ShopFeatureOfferRow FindForgeOffer(ShopFeatureSurfaceModel surface, out bool fromBasic)
                {
                    ShopFeatureOfferRow current = surface.BasicOffers.FirstOrDefault(row =>
                        row.OfferTypeId == FacilityShopOfferTypeIds.Building
                        && row.DataId == forgeDefinition.id && !row.IsPurchased);
                    fromBasic = current != null;
                    return current ?? surface.DailyOffers.FirstOrDefault(row =>
                        row.OfferTypeId == FacilityShopOfferTypeIds.Building
                        && row.DataId == forgeDefinition.id && !row.IsPurchased);
                }
                var offerObservations = new JArray();
                trace["shopOfferObservations"] = offerObservations;
                void ObserveShop()
                {
                    var offers = new JArray(model.BasicOffers.Concat(model.DailyOffers)
                        .Where(row => row.OfferTypeId == FacilityShopOfferTypeIds.Building)
                        .Select(row => new JObject { ["buildingId"] = row.DataId, ["cost"] = row.Cost,
                            ["purchaseKey"] = row.PurchaseKey, ["purchased"] = row.IsPurchased }));
                    trace["availableBuildingOffers"] = offers;
                    var sample = new JObject { ["offerDay"] = model.OfferDay,
                        ["calendarDay"] = calendar.Day, ["buildingOffers"] = offers.DeepClone() };
                    offerObservations.Add(sample);
                    WriteNew(artifactPrefix + "-shop-day-" + model.OfferDay + ".json", sample.ToString());
                }
                ObserveShop();
                ShopFeatureOfferRow offer = FindForgeOffer(model, out bool basic);
                // A bounded normal procurement scenario observes seven actual
                // daily refreshes. It does not choose another seed, manufacture
                // stock, call Refresh, or change the calendar. Absence still fails.
                const int maximumProcurementDays = 7;
                int procurementStartDay = calendar.Day;
                int lastObservedCalendarDay = calendar.Day;
                trace["initialForgeOfferAvailable"] = offer != null;
                trace["maximumProcurementDays"] = maximumProcurementDays;
                trace["procurementStartDay"] = procurementStartDay;
                if (offer == null)
                {
                    phase = "skill-normal-forge-procurement-wait";
                    Time.maximumDeltaTime = report.skillMechanic.calendarMaximumDeltaTime;
                    gameSpeed.SetSpeed(5);
                    gameSpeed.SetPaused(false);
                    while (offer == null && calendar.Day < procurementStartDay + maximumProcurementDays)
                    {
                        observe();
                        yield return null;
                        if (calendar.Day == lastObservedCalendarDay) continue;
                        Require(calendar.Day > lastObservedCalendarDay,
                            "Normal forge procurement calendar moved backwards.");
                        lastObservedCalendarDay = calendar.Day;
                        model = shop.Capture();
                        ObserveShop();
                        offer = FindForgeOffer(model, out basic);
                    }
                    gameSpeed.SetPaused(true);
                }
                trace["procurementEndDay"] = calendar.Day;
                Require(offer != null,
                    "NormalWeaponForgeUnavailable: no existing forge or public forge kit offer within the finite procurement window;"
                    + " normal facility procurement/production is still required, no injected fallback.");
                Require(model.CurrentMoney >= offer.Cost, "Normal support forge purchase lacks actual funds.");
                var controller = scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller;
                GridMode previousMode = controller.GridSystem.Mode;
                int previousSelection = controller.SelectedBuilding != null ? controller.SelectedBuilding.id : -1;
                RoomInstance room = null;
                Vector2Int position = default;
                try
                {
                    controller.SetGridModeBuild();
                    controller.SelectBuildingById(forgeDefinition.id);
                    Require(controller.SelectedBuilding == forgeDefinition,
                        "Public support forge selection did not resolve its authored definition.");
                    foreach (RoomInstance candidate in scope.Container.Resolve<IRoomLayoutCache>()
                        .GetLayout(controller.GridSystem.grid).Rooms.Where(value =>
                            value != null && value.IsUsable && !value.IsSelfContained).OrderBy(value => value.Id))
                    {
                        foreach (Vector2Int cell in candidate.Cells.OrderBy(value => value.y).ThenBy(value => value.x))
                        {
                            if (!forgeDefinition.GetGridPosList(cell).All(candidate.ContainsCell)
                                || !controller.IsBuildableAt(cell)) continue;
                            room = candidate; position = cell; break;
                        }
                        if (room != null) break;
                    }
                }
                finally
                {
                    controller.GridSystem.SetGridMode(previousMode);
                    if (previousSelection >= 0) controller.SelectBuildingById(previousSelection);
                }
                Require(room != null, "Normal support forge has no legal usable construction space.");
                gameSpeed.SetPaused(true);
                IShopFeatureCommandService purchases = scope.Container.Resolve<IShopFeatureCommandService>();
                ShopFeatureCommandResult purchased = basic ? purchases.PurchaseBasic(offer.Index, offer.PurchaseKey)
                    : purchases.PurchaseDaily(offer.Index, offer.PurchaseKey);
                Require(purchased.Succeeded && shop.Capture().CurrentMoney == model.CurrentMoney - offer.Cost,
                    "Normal support forge payment failed: " + purchased.Message);
                commands.Add("shop:" + offer.PurchaseKey);
                trace["forgeCost"] = offer.Cost;
                trace["forgeMoneyBefore"] = model.CurrentMoney;
                trace["forgeMoneyAfter"] = shop.Capture().CurrentMoney;
                string kitId = FacilityInstallationKitItemIds.ForBuilding(forgeDefinition);
                gameSpeed.SetPaused(false);
                float deliveryStarted = clock.Time;
                while (Quantity(kitId) < 1 && clock.Time - deliveryStarted < 90f)
                { observe(); yield return null; }
                Require(Quantity(kitId) == 1, "Paid normal support forge kit did not arrive exactly once.");
                foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>().Characters)
                    if (actor != null && !actor.IsDead && CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork hauler))
                        hauler.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
                yield return ConstructFacilityFixture(forgeDefinition, room, "skill-normal-weapon-forge",
                    useAuthoredWorkBudget: true, observe: observe, plannedPosition: position,
                    approvedSupportNeedsOnly: true, approvedSupportActorIds: approvedResidentIds);
                forge = buildings.Buildings.SingleOrDefault(value => Supports(value) && value.centerPos == position);
                Require(forge != null && Quantity(kitId) == 0,
                    "Normal support forge construction did not complete with one real kit consumption.");
                commands.Add("construct:" + forge.PersistentInstanceId.Value);
            }
            trace["forgeId"] = forge.PersistentInstanceId.Value;
            Require(CharacterWorkRoleUtility.TryGetWork(owner, out AbilityWork worker),
                "Selected support actor cannot receive an actual Craft command.");
            foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>().Characters)
                if (actor != null && !actor.IsDead && CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork hauler))
                    hauler.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
            for (int index = 0; index < pendingActors.Length; index++)
            {
                if (owner.Stats.TryGetConditionValue(CharacterCondition.FUN, out float fun)
                    && fun < owner.Stats.GetNeedResponse(CharacterCondition.FUN).routineStart)
                    yield return RecoverSupportWorkFunThroughNormalVisits(new[] { owner }, observe);
                phase = "skill-normal-weapon-craft";
                gameSpeed.SetPaused(true);
                HashSet<string> beforeInstances = equipment.Instances.Select(value => value.instanceId)
                    .ToHashSet(StringComparer.Ordinal);
                var inputBefore = bom.Inputs.ToDictionary(input => input.ItemId, input => Quantity(input.ItemId));
                CombatEquipmentCraftOrderSaveData[] ownedOrders = equipment.CraftQueue.Where(value =>
                    value.definitionId == weaponId && value.materialId == materialId
                    && value.facilityPersistentId == forge.PersistentInstanceId.Value).ToArray();
                Require(ownedOrders.Length <= 1,
                    "Normal support crafting has multiple ambiguous owned orders; no extra order is queued.");
                bool resumedOrder = ownedOrders.Length == 1;
                CombatEquipmentCraftOrderSaveData order;
                if (resumedOrder) order = ownedOrders[0];
                else
                {
                    Require(equipment.TryQueueCraft(weaponId, materialId, forge, out string craftFailure),
                        "Public normal support weapon craft rejected: " + craftFailure);
                    order = equipment.CraftQueue.Last();
                }
                Require(!resumedOrder || order.completedWork >= 0f && order.completedWork < order.requiredWork
                        && !order.materialTransferAcknowledged && !order.outputPublished
                        && !order.completionEffectsPublished && order.qualityAttemptIndex == 0,
                    "Owned craft continuation is not the supported unfinished first physical crafting attempt.");
                var sample = new JObject { ["orderId"] = order.orderId,
                    ["recipientId"] = pendingActors[index].Identity.PersistentId,
                    ["orderAlreadyOwned"] = resumedOrder, ["queueCommandIssued"] = !resumedOrder,
                    ["initialOrder"] = JObject.Parse(JsonUtility.ToJson(order)), ["frames"] = new JArray() };
                crafts.Add(sample);
                commands.Add((resumedOrder ? "resume-owned-craft:" : "craft:") + order.orderId);
                bool resumedWorkInProgress = resumedOrder && order.materialsReady;
                sample["materialsPreviouslyTransferredToWip"] = resumedWorkInProgress;
                phase = "skill-normal-weapon-material-haul";
                Time.maximumDeltaTime = 0.1f;
                gameSpeed.SetSpeed(5);
                gameSpeed.SetPaused(false);
                float deliveryStarted = clock.Time;
                int deliveryFrames = 0;
                while (!forge.HasPendingEquipmentCraftWork() && clock.Time - deliveryStarted < 90f)
                { observe(); deliveryFrames++; yield return null; }
                gameSpeed.SetPaused(true);
                sample["materialHaulGameSeconds"] = clock.Time - deliveryStarted;
                sample["materialHaulFrames"] = deliveryFrames;
                sample["deliveredInputs"] = new JArray(bom.Inputs.Select(input => new JObject
                {
                    ["itemId"] = input.ItemId, ["required"] = input.Amount,
                    ["actual"] = items.GetAllStacks().Where(stack => stack != null
                        && stack.State == WorldItemStackState.FacilityBuffer
                        && stack.DestinationId == order.materialDestinationId && stack.ItemId == input.ItemId)
                        .Sum(stack => stack.Quantity)
                }));
                bool exactMaterialAuthority = resumedWorkInProgress
                    ? CombatEquipmentCraftMaterialOutbox.ValidateProvenance(order,
                        bom.Inputs.ToDictionary(input => input.ItemId, input => input.Amount), out _)
                    : bom.Inputs.All(input => items.GetAllStacks().Where(stack => stack != null
                        && stack.State == WorldItemStackState.FacilityBuffer
                        && stack.DestinationId == order.materialDestinationId && stack.ItemId == input.ItemId)
                        .Sum(stack => stack.Quantity) == input.Amount);
                Require(forge.HasPendingEquipmentCraftWork() && exactMaterialAuthority,
                    "Normal craft inputs did not arrive via real Haul at this order's exact buffer.");
                if (resumedWorkInProgress)
                    sample["restoredMaterialTransferCommitId"] = order.materialTransferCommitId;
                if (owner.Stats.GetConditionValue(CharacterCondition.HUNGER)
                    < owner.Stats.GetNeedResponse(CharacterCondition.HUNGER).routineStart)
                    yield return RecoverSupportWorkHungerThroughNormalMeals(owner, observe);
                JObject retargetContract = null;
                if (SkillSupportContinuation && resumedOrder && order.completedWork == 0f)
                {
                    // Recreate the observed Operate -> Craft ownership boundary
                    // with real public commands, not by seeding a coroutine or
                    // relying on a cold restore to accidentally clear the bug.
                    phase = "skill-normal-weapon-retarget-precondition";
                    worker.ClearPriorityWorkTarget();
                    worker.SetWorkPriority(BuiltInWorkTypeIds.Operate, WorkPriorityLevel.Priority1);
                    // This tests a direct player command, not whether the
                    // autonomous scorer currently offers an Operate candidate.
                    // Use the authored facility observed in the original stall.
                    BuildingSO trainingDefinition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                        "Assets/Resources/SO/Building/Modular/T01_훈련허수아비.asset");
                    BuildableObject previousTarget = buildings.Buildings
                        .Where(value => value != null && !value.isDestroy && value.BuildingData == trainingDefinition)
                        .OrderBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal).FirstOrDefault();
                    Require(trainingDefinition != null && previousTarget != null && previousTarget != forge,
                        "Retarget regression lacks its authored prior training facility.");
                    Require(worker.TrySetPriorityWorkTarget(previousTarget, BuiltInWorkTypeIds.Operate, null,
                            out string previousRejection),
                        "Retarget regression Operate command rejected: " + previousRejection);
                    long approvedBeforeOperate = worker.ApprovedWorkProgressRevisionForDiagnostics;
                    float operateStarted = clock.Time;
                    gameSpeed.SetPaused(false);
                    while ((!owner.Brain.HasRunningWorkAction || !worker.HasActiveWorkRoutineForDiagnostics
                            || worker.assignedShop != previousTarget || !worker.IsAssignedWork(BuiltInWorkTypeIds.Operate)
                            || worker.ApprovedWorkProgressRevisionForDiagnostics <= approvedBeforeOperate)
                        && clock.Time - operateStarted < 90f)
                    { observe(); yield return null; }
                    gameSpeed.SetPaused(true);
                    Require(owner.Brain.HasRunningWorkAction && worker.HasActiveWorkRoutineForDiagnostics
                            && worker.assignedShop == previousTarget && worker.IsAssignedWork(BuiltInWorkTypeIds.Operate)
                            && worker.ApprovedWorkProgressRevisionForDiagnostics > approvedBeforeOperate,
                        "Retarget regression did not reach a real running Operate task.");
                    int priorRun = worker.ActiveWorkRunIdForDiagnostics;
                    AIAction priorAction = owner.Brain.bestAction;
                    long priorCancellations = worker.ActiveWorkCancellationCountForDiagnostics;
                    Require(!worker.TrySetPriorityWorkTarget(null, BuiltInWorkTypeIds.Craft, null, out _)
                            && worker.ActiveWorkRunIdForDiagnostics == priorRun
                            && worker.assignedShop == previousTarget && ReferenceEquals(owner.Brain.bestAction, priorAction),
                        "Rejected work command changed the running task.");
                    Require(worker.TrySetPriorityWorkTarget(previousTarget, BuiltInWorkTypeIds.Operate, null, out _)
                            && worker.ActiveWorkRunIdForDiagnostics == priorRun
                            && worker.ActiveWorkCancellationCountForDiagnostics == priorCancellations
                            && ReferenceEquals(owner.Brain.bestAction, priorAction),
                        "Repeated same-target work command restarted the running task.");
                    retargetContract = new JObject
                    {
                        ["origin"] = "public Operate command followed by public Craft command; isolated regression",
                        ["previousFacilityId"] = previousTarget.PersistentInstanceId.Value,
                        ["previousRunId"] = priorRun, ["cancellationsBefore"] = priorCancellations,
                        ["rejectedCommandPreservedRun"] = true, ["sameTargetCommandPreservedRun"] = true,
                        ["priorOperateApprovedTicks"] = worker.ApprovedWorkProgressRevisionForDiagnostics - approvedBeforeOperate,
                        ["stateOrOutcomeInjected"] = false, ["trainingEligible"] = false
                    };
                    sample["retargetContract"] = retargetContract;
                }
                phase = "skill-normal-weapon-craft";
                worker.ClearPriorityWorkTarget();
                worker.SetWorkPriority(BuiltInWorkTypeIds.Craft, WorkPriorityLevel.Priority1);
                Require(worker.TrySetPriorityWorkTarget(forge, BuiltInWorkTypeIds.Craft, null, out string rejection),
                    "Public normal support Craft work rejected: " + rejection);
                if (retargetContract != null)
                {
                    Require(worker.ActiveWorkRunIdForDiagnostics > (int)retargetContract["previousRunId"]
                            && worker.ActiveWorkCancellationCountForDiagnostics == (long)retargetContract["cancellationsBefore"] + 1
                            && !worker.HasActiveWorkRoutineForDiagnostics && !owner.Brain.HasRunningAction
                            && worker.assignedShop == forge && worker.IsAssignedWork(BuiltInWorkTypeIds.Craft),
                        "Accepted Craft retarget did not cancel exactly the previous work run before assignment.");
                    retargetContract["previousRunCancelledExactlyOnce"] = true;
                    retargetContract["assignedFacilityId"] = forge.PersistentInstanceId.Value;
                }
                long initialApproved = worker.ApprovedWorkProgressRevisionForDiagnostics;
                float started = clock.Time;
                float lastCraftProgressGame = started, lastCraftWork = order.completedWork;
                int craftProgressSequence = 0;
                float initialCraftWork = order.completedWork;
                var executionStates = new JArray();
                sample["executionStates"] = executionStates;
                JObject CaptureCraftExecutionState()
                {
                    bool eligible = equipment.TryGetNextCraftMaterialContext(
                        forge.BuildingData.GetAbility<BuildingEquipmentCraftingAbility>().CraftableEquipmentIds,
                        owner, out string nextDefinition, out string nextMaterial, out bool substitute);
                    return new JObject
                    {
                        ["gameSeconds"] = clock.Time - started,
                        ["orderId"] = order.orderId, ["work"] = order.completedWork,
                        ["materialsReady"] = order.materialsReady,
                        ["materialCommitId"] = order.materialTransferCommitId,
                        ["pendingCraft"] = forge.HasPendingEquipmentCraftWork(),
                        ["workerEligibleForNextCraft"] = eligible,
                        ["nextDefinitionId"] = nextDefinition, ["nextMaterialId"] = nextMaterial,
                        ["usesSubstituteMaterial"] = substitute,
                        ["brain"] = owner.Brain.CurrentActionDebugLabel,
                        ["actorPosition"] = owner.transform.position.ToString("F3"),
                        ["isWorking"] = worker.isWorking,
                        ["hasRoutine"] = worker.HasActiveWorkRoutineForDiagnostics,
                        ["runId"] = worker.ActiveWorkRunIdForDiagnostics,
                        ["assignedFacilityId"] = worker.assignedShop?.PersistentInstanceId.Value ?? string.Empty,
                        ["assignedWorkTypeId"] = worker.AssignedWorkTypeId.Value ?? string.Empty,
                        ["priorityFacilityId"] = worker.PriorityWorkTarget?.PersistentInstanceId.Value ?? string.Empty,
                        ["priorityWorkTypeId"] = worker.PriorityWorkTypeId.Value ?? string.Empty,
                        ["duty"] = worker.CurrentDutyState.ToString(),
                        ["routineNeedBlocked"] = worker.HasRoutineNeedWorkBlockForDiagnostics,
                        ["genericProgressActive"] = worker.HasActiveGenericProgressForDiagnostics,
                        ["genericWork"] = worker.GenericCompletedWorkForDiagnostics,
                        ["genericRequiredWork"] = worker.GenericRequiredWorkForDiagnostics,
                        ["approvedTicks"] = worker.ApprovedWorkProgressRevisionForDiagnostics - initialApproved,
                        ["preWuExitKind"] = worker.LastPreWuExitKindForDiagnostics.ToString(),
                        ["preWuExitDetail"] = worker.LastPreWuExitDetailForDiagnostics,
                        ["workOrderDetail"] = worker.LastWorkOrderExecutionDetailForDiagnostics,
                        ["cancellationCount"] = worker.ActiveWorkCancellationCountForDiagnostics,
                        ["lastCancellationReason"] = worker.LastActiveWorkCancellationReasonForDiagnostics
                    };
                }
                executionStates.Add(CaptureCraftExecutionState());
                WriteNew(artifactPrefix + "-" + index + "-execution-state-0.json", executionStates.Last.ToString());
                float nextInitialExecutionState = 15f;
                // This is a new finite preparation budget from the authored WU,
                // not a timeout increase for an existing failed test.
                float bound = order.requiredWork * 20f + 90f;
                Time.maximumDeltaTime = 0.1f;
                gameSpeed.SetSpeed(5);
                gameSpeed.SetPaused(false);
                while (equipment.CraftQueue.Any(value => value.orderId == order.orderId)
                    && clock.Time - started < bound)
                {
                    observe();
                    if (owner.Stats.GetConditionValue(CharacterCondition.HUNGER)
                        < owner.Stats.GetNeedResponse(CharacterCondition.HUNGER).routineStart)
                    {
                        yield return RecoverSupportWorkHungerThroughNormalMeals(owner, observe);
                        lastCraftProgressGame = clock.Time;
                        phase = "skill-normal-weapon-craft";
                        Time.maximumDeltaTime = 0.1f;
                    }
                    if (owner.Stats.TryGetConditionValue(CharacterCondition.FUN, out float currentFun)
                        && currentFun < owner.Stats.GetNeedResponse(CharacterCondition.FUN).routineStart)
                    {
                        yield return RecoverSupportWorkFunThroughNormalVisits(new[] { owner }, observe);
                        lastCraftProgressGame = clock.Time;
                        phase = "skill-normal-weapon-craft";
                        Time.maximumDeltaTime = 0.1f;
                    }
                    if (!worker.isWorking && !worker.HasActiveWorkRoutineForDiagnostics
                        && !worker.AssignedWorkTypeId.IsValid && worker.PriorityWorkTarget == null)
                        Require(worker.TrySetPriorityWorkTarget(forge, BuiltInWorkTypeIds.Craft, null, out rejection),
                            "Public idle Craft retry rejected: " + rejection);
                    yield return null;
                    // Read-only, bounded diagnostics: these observations never
                    // reset the WU watchdog or claim material/actor motion as work.
                    if (order.completedWork <= initialCraftWork
                        && clock.Time - started >= nextInitialExecutionState)
                    {
                        executionStates.Add(CaptureCraftExecutionState());
                        WriteNew(artifactPrefix + "-" + index + "-execution-state-" + executionStates.Count + ".json",
                            executionStates.Last.ToString());
                        nextInitialExecutionState += 15f;
                    }
                    if (order.completedWork > lastCraftWork)
                    {
                        lastCraftWork = order.completedWork;
                        lastCraftProgressGame = clock.Time;
                        var progress = new JObject { ["orderId"] = order.orderId,
                            ["gameSeconds"] = clock.Time - started, ["work"] = order.completedWork,
                            ["requiredWork"] = order.requiredWork, ["stage"] = order.qualityStage.ToString(),
                            ["brain"] = owner.Brain.CurrentActionDebugLabel,
                            ["blocker"] = worker.LastPreWuExitDetailForDiagnostics };
                        ((JArray)sample["frames"]).Add(progress);
                        // Sparse create-only evidence, not one file per frame.
                        if (++craftProgressSequence == 1 || craftProgressSequence % 8 == 0)
                            WriteNew(artifactPrefix + "-" + index + "-progress-" + craftProgressSequence + ".json",
                                progress.ToString());
                    }
                    if (clock.Time - lastCraftProgressGame >= 90f)
                    {
                        sample["noWuTerminalExecutionState"] = CaptureCraftExecutionState();
                        WriteNew(artifactPrefix + "-" + index + "-no-wu-terminal.json",
                            sample["noWuTerminalExecutionState"].ToString());
                    }
                    Require(clock.Time - lastCraftProgressGame < 90f,
                        "Actual Craft has no order WU progress for 90 game seconds: order=" + order.orderId
                            + "; work=" + order.completedWork + "/" + order.requiredWork
                            + "; brain=" + owner.Brain.CurrentActionDebugLabel
                            + "; blocker=" + worker.LastPreWuExitDetailForDiagnostics);
                }
                gameSpeed.SetPaused(true);
                sample["terminalOrder"] = JObject.Parse(JsonUtility.ToJson(order));
                sample["gameSeconds"] = clock.Time - started;
                sample["approvedTicks"] = worker.ApprovedWorkProgressRevisionForDiagnostics - initialApproved;
                CombatEquipmentInstance[] created = equipment.Instances.Where(value =>
                    !beforeInstances.Contains(value.instanceId)).ToArray();
                Require(!equipment.CraftQueue.Any(value => value.orderId == order.orderId)
                        && created.Length == 1 && created[0].definitionId == weaponId
                        && created[0].materialId == materialId && order.materialTransferAcknowledged
                        && !string.IsNullOrWhiteSpace(order.materialTransferCommitId)
                        && order.completionFact?.IsEmpty == false
                        && order.outputPublication?.observationDelivered == true
                        && !string.IsNullOrWhiteSpace(
                            order.outputPublication.observationKey)
                        && worker.ApprovedWorkProgressRevisionForDiagnostics > initialApproved,
                    "Actual support Craft did not complete one exact physically backed weapon/outcome.");
                foreach (var input in bom.Inputs)
                    Require(Quantity(input.ItemId) == inputBefore[input.ItemId]
                            - (resumedWorkInProgress ? 0 : input.Amount),
                        "Actual support weapon input consumption differs from its exact BOM: " + input.ItemId);
                sample["instanceId"] = created[0].instanceId;
                sample["completionResultKey"] =
                    order.outputPublication.observationKey;
                string ResultKey(GameplayOutcomeSnapshot value) => new GameplayResultKey(value.producerId,
                    new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex).ToString();
                GameplayOutcomeSnapshot[] completions = ReadAllOutcomes().Where(value =>
                    ResultKey(value) == order.outputPublication.observationKey)
                    .ToArray();
                Require(completions.Length == 1 && completions[0].status == GameplayOutcomeStatus.Succeeded
                        && completions[0].facts.Any(value => value.factId == MigratedProducerOutcomeIds.WorkOrderFact.Value
                            && value.value == order.orderId),
                    "Craft completion ledger identity is missing, duplicated, or bound to another order.");
                sample["completionOutcome"] = JObject.Parse(JsonUtility.ToJson(completions[0]));
                phase = "skill-normal-weapon-haul-and-equip";
                worker.ClearPriorityWorkTarget();
                yield return AwaitStoredCraftOutput(created[0].instanceId, sample);
                // Both query APIs return detached snapshots. Refresh by exact ID
                // after Haul instead of polling the original pre-Haul clone.
                Require(equipment.TryGetInstance(created[0].instanceId, out CombatEquipmentInstance storedOutput)
                        && items.GetAllStacks().Count(stack => stack.StackId == storedOutput.sourceStackId
                            && stack.ItemInstanceId == storedOutput.instanceId
                            && stack.State == WorldItemStackState.Stored && stack.Quantity == 1) == 1,
                    "Crafted support weapon lacks one exact current stored physical stack.");
                sample["storedSourceStackId"] = storedOutput.sourceStackId;
                sample["storedWorldState"] = storedOutput.worldState.ToString();
                Require(expeditions.TryEquipEquipment(pendingActors[index], weaponId, out string equipFailure),
                    "Public support weapon pickup rejected: " + equipFailure);
                commands.Add("equipment-pickup:" + pendingActors[index].Identity.PersistentId + ":" + weaponId);
                float pickupStarted = clock.Time;
                gameSpeed.SetPaused(false);
                while ((!equipment.TryGetActiveWeapon(pendingActors[index].Identity.PersistentId,
                        out CombatWeaponSnapshot activePickup)
                        || activePickup == null || activePickup.InstanceId != created[0].instanceId)
                    && clock.Time - pickupStarted < 90f)
                { observe(); yield return null; }
                Require(equipment.TryGetActiveWeapon(pendingActors[index].Identity.PersistentId, out CombatWeaponSnapshot weapon)
                        && weapon.InstanceId == created[0].instanceId,
                    "Actual movement/pickup did not equip the exact crafted support weapon.");
                sample["activeInstanceId"] = weapon.InstanceId;
                Require(ReadAllOutcomes().Count(value => ResultKey(value)
                            == order.outputPublication.observationKey) == 1,
                    "Haul/pickup republished the craft completion ledger identity.");
                sample["status"] = "ACTUAL_CRAFT_HAUL_PICKUP_EQUIPPED";
            }
            gameSpeed.SetPaused(true);
            trace["status"] = "PASS_NORMAL_CRAFT_HAUL_EQUIPMENT";
            WriteNew(artifactPrefix + "-equipped-world.json", saves.ToJson(saves.Capture()));
        }
        finally
        {
            gameSpeed.SetPaused(true);
            trace["lastObservedPhase"] = phase;
            if ((string)trace["status"] == "PREPARING")
                trace["status"] = "INCOMPLETE_NORMAL_PATH_NO_PASS_CLAIMED";
            WriteNew(artifactPrefix + "-witness.json", trace.ToString());
        }
    }

    private void PrepareSupportEquipmentTestFixture(CharacterActor[] party, string artifactPrefix)
    {
        Require(skillEquipmentPreparationFixture && (SkillSupportContinuation || sourceV9SkillReplay)
                && Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && report.continuation?.officialRestore == true
                && !scope.Container.Resolve<IOffenseBattleRuntime>().HasActiveBattle,
            "Test equipment provisioning must precede the actual skill battle after exact official restore.");
        Require(party != null && party.Length > 0 && party.All(actor => actor != null && !actor.IsDead)
                && party.Select(actor => actor.Identity.PersistentId).Distinct(StringComparer.Ordinal).Count() == party.Length,
            "Equipment test fixture requires a finite unique live party.");
        phase = "skill-explicit-test-equipment-preparation";
        bool wasPaused = gameSpeed.IsPaused;
        gameSpeed.SetPaused(true);
        ICombatEquipmentRuntime equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        var healthBefore = party.ToDictionary(actor => actor.Identity.PersistentId, actor => actor.Stats.CurrentHealth);
        string ordersBefore = new JArray(equipment.CraftQueue.Select(order => JObject.Parse(JsonUtility.ToJson(order)))).ToString();
        int outcomesBefore = ReadAllOutcomes().Count();
        int stacksBefore = scope.Container.Resolve<IWorldItemStackRuntime>().GetAllStacks().Count();
        var supplied = new JArray();
        foreach (CharacterActor actor in party)
        {
            if (equipment.TryGetActiveWeapon(actor.Identity.PersistentId, out CombatWeaponSnapshot active)
                && active != null && !string.IsNullOrWhiteSpace(active.InstanceId)) continue;
            CombatEquipmentInstance fixture = equipment.CreateInstance("weapon:spear",
                CombatEquipmentQuality.Normal, CombatEquipmentWorldState.Stored, "material:wood");
            Require(equipment.TryAssignToCharacter(actor.Identity.PersistentId, fixture.instanceId, out string failure),
                "Explicit test equipment assignment rejected: " + failure);
            Require(equipment.TryGetActiveWeapon(actor.Identity.PersistentId, out CombatWeaponSnapshot equipped)
                    && equipped.InstanceId == fixture.instanceId,
                "Explicit test equipment did not reach the same owner's active loadout.");
            supplied.Add(new JObject { ["actorId"] = actor.Identity.PersistentId,
                ["instanceId"] = fixture.instanceId, ["definitionId"] = fixture.definitionId,
                ["materialId"] = fixture.materialId, ["quality"] = fixture.quality.ToString(),
                ["origin"] = "explicit isolated test provisioning; not crafted, purchased or hauled" });
        }
        Require(party.All(actor => actor.Stats.CurrentHealth == healthBefore[actor.Identity.PersistentId])
                && ordersBefore == new JArray(equipment.CraftQueue.Select(order => JObject.Parse(JsonUtility.ToJson(order)))).ToString()
                && outcomesBefore == ReadAllOutcomes().Count()
                && stacksBefore == scope.Container.Resolve<IWorldItemStackRuntime>().GetAllStacks().Count(),
            "Equipment test preparation changed health, existing craft orders, physical stacks or outcomes.");
        WriteNew(artifactPrefix + "-test-equipment-fixture.json", new JObject
        {
            ["origin"] = "user-authorized isolated non-target equipment preparation",
            ["supplied"] = supplied, ["suppliedCount"] = supplied.Count,
            ["existingCraftOrdersPreserved"] = true, ["existingPhysicalItemsPreserved"] = true,
            ["healthOrEnemyOrSkillEffectInjected"] = false, ["outcomesUnchanged"] = true,
            ["normalCraftHaulPickupPassClaimed"] = false, ["normalCraftHaulFailureStillOpen"] = true,
            ["naturalPlayClaimed"] = false, ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
        }.ToString());
        WriteNew(artifactPrefix + "-test-equipped-world.json", saves.ToJson(saves.Capture()));
        gameSpeed.SetPaused(wasPaused);
    }

    private void VerifySkillBattleCombatOwnerGuards(string world, SkillMechanicWitness witness)
    {
        var validator = new DungeonAggregateReferencePreflight(
            scope.Container.Resolve<IItemDefinitionCatalog>(),
            scope.Container.Resolve<IBuildingDefinitionLookup>(),
            scope.Container.Resolve<ICombatEquipmentCatalog>(),
            scope.Container.Resolve<IResourceEconomyContentCatalog>(),
            scope.Container.Resolve<ICharacterLifeDefinitionCatalog>(),
            scope.Container.Resolve<IDiseaseDefinitionCatalog>());
        string unchanged = SkillWorldStateHash(saves.ToJson(saves.Capture()));
        void Reject(string label, Action<DungeonCombatEquipmentSaveData, OffenseBattlePersistenceState> corrupt,
            string expectedError)
        {
            DungeonGameSaveData detached = saves.FromJson(world);
            DungeonOffenseAggregateSaveData offense = DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(
                detached, OffenseAggregateSaveSection.Id);
            DungeonCombatEquipmentSaveData equipment = DungeonSaveSectionPayload.ReadOrNew<DungeonCombatEquipmentSaveData>(
                detached, CombatEquipmentSaveSection.Id);
            corrupt(equipment, offense.expedition.activeBattle);
            detached.sections.Single(value => value.sectionId == OffenseAggregateSaveSection.Id).payloadJson = JsonUtility.ToJson(offense);
            detached.sections.Single(value => value.sectionId == CombatEquipmentSaveSection.Id).payloadJson = JsonUtility.ToJson(equipment);
            DungeonGameRestoreReport rejected = new();
            // This exercises the aggregate validator on detached payloads, not
            // section integrity or a claimed successful/failed world publication.
            validator.Validate(detached, rejected);
            Require(rejected.Errors.Any(value => value.Contains(expectedError))
                    && unchanged == SkillWorldStateHash(saves.ToJson(saves.Capture())),
                "Combat owner aggregate guard failed or changed live state: " + label);
            witness.combatOwnerRejections.Add(label);
        }
        Reject("orphan-loadout", (equipment, _) =>
            equipment.loadouts[0].characterId = "character:combat-owner-orphan", "Combat loadout references missing character");
        Reject("duplicate-enemy", (_, battle) =>
            battle.enemyIndividuals.Add(battle.enemyIndividuals[0].Clone()), "duplicate enemy individual");
        Reject("resident-collision", (_, battle) =>
            battle.enemyIndividuals[0].characterId = witness.actorId, "collides with a world character");
        Reject("unmatched-enemy", (_, battle) =>
            battle.combatants.RemoveAll(value => value.persistentId == battle.enemyIndividuals[0].characterId),
            "has no matching combatant");
        witness.combatOwnerGuardsAtomic = witness.combatOwnerRejections.Count == 4;
        void RejectInitiative(Action<OffenseBattlePersistenceState> corrupt, string expectedError)
        {
            DungeonOffenseAggregateSaveData detached = DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(
                saves.FromJson(world), OffenseAggregateSaveSection.Id);
            corrupt(detached.expedition.activeBattle);
            bool rejected = false;
            try { OffenseAggregateSaveValidation.BuildRestorePlan(detached); }
            catch (InvalidOperationException exception)
            { rejected = exception.Message.Contains(expectedError); }
            Require(rejected, "Offense initiative membership guard did not reject malformed participation.");
            witness.initiativeGuardChecks++;
        }
        RejectInitiative(battle => battle.initiativeOrder[0] = "character:initiative-unknown",
            "initiative references an unknown or non-participating combatant");
        RejectInitiative(battle => battle.combatants.Single(value =>
            value.persistentId == battle.initiativeOrder[0]).participatesInInitiative = false,
            "initiative references an unknown or non-participating combatant");
        RejectInitiative(battle => battle.initiativeOrder.Add(battle.initiativeOrder[0]),
            "Duplicate offense battle");
        RejectInitiative(battle => battle.initiativeOrder.Clear(), "invalid turn state");
        RejectInitiative(battle => battle.currentOrderIndex = battle.initiativeOrder.Count,
            "invalid turn state");
        VerifySkillBattleRoundSchedule(world, witness);
        Require(unchanged == SkillWorldStateHash(saves.ToJson(saves.Capture())),
            "Detached initiative preflight tests changed live state.");
    }

    private void VerifySkillBattleRoundSchedule(string world, SkillMechanicWitness witness)
    {
        // Detached model fixtures only: no actor, damage, status or command is
        // published to the real world or its gameplay ledger. The actual attack
        // and official full-world restore are verified separately below.
        DungeonOffenseAggregateSaveData baseline = DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(
            saves.FromJson(world), OffenseAggregateSaveSection.Id);
        OffenseBattlePersistenceState source = baseline.expedition.activeBattle;
        OffenseBattleSnapshot live = scope.Container.Resolve<IOffenseBattleRuntime>().Session;
        ICombatResolutionService resolution = scope.Container.Resolve<ICombatResolutionService>();
        ICombatEquipmentRuntime equipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        const string reserveId = "qa:round-schedule:reserve";
        OffenseBattleCombatant[] Configure(IEnumerable<OffenseBattleCombatant> combatants) => combatants.Select(value =>
            new OffenseBattleCombatant(value.PersistentId, value.DisplayName, value.SpeciesTag, value.Team,
                value.Stats, value.CurrentHealth, value.Abilities, value.PortraitDataId, value.Formation,
                value.ParticipatesInInitiative)).ToArray();
        OffenseBattleSession Create(bool downed)
        {
            var reserve = new OffenseBattleCombatant(reserveId, "Round schedule fixture", string.Empty,
                OffenseBattleTeam.Allies, new OffenseBattleStats(100f, 1f, 1f, 1f, 0f, 0f), 100f);
            if (downed)
                reserve.AddStatus(new OffenseBattleStatus("qa:sedation", OffenseBattleStatusType.Sedated,
                    1f, 3, reserveId));
            return new OffenseBattleSession(source.battleId, source.expeditionId, source.targetId,
                source.targetTitle, source.difficulty, Configure(live.Combatants).Append(reserve), resolution, equipment);
        }
        OffenseBattleSession CheckAndRestore(OffenseBattleSession session, string label)
        {
            DungeonOffenseAggregateSaveData detached = DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(
                saves.FromJson(world), OffenseAggregateSaveSection.Id);
            OffenseBattlePersistenceState snapshot = session.CapturePersistentState();
            snapshot.encounterId = source.encounterId;
            snapshot.enemyIndividuals = detached.expedition.activeBattle.enemyIndividuals;
            detached.expedition.activeBattle = snapshot;
            OffenseBattlePersistenceState validated = OffenseAggregateSaveValidation.BuildRestorePlan(detached)
                .Payload.expedition.activeBattle;
            Require(JsonUtility.ToJson(validated) == JsonUtility.ToJson(snapshot),
                "Round schedule preflight rewrote its input: " + label);
            OffenseBattleSession restored = OffenseBattleSession.Restore(
                validated,
                Configure(session.Combatants),
                resolution,
                equipment);
            Require(JsonUtility.ToJson(restored.CapturePersistentState()) == JsonUtility.ToJson(session.CapturePersistentState())
                    && restored.Combatants.All(value => value.IsDowned == session.FindCombatant(value.PersistentId).IsDowned
                        && value.IsDead == session.FindCombatant(value.PersistentId).IsDead),
                "Round schedule restore changed its snapshot or derived availability: " + label);
            witness.initiativeStateChecks.Add(label);
            return restored;
        }
        void NextRound(OffenseBattleSession session)
        {
            int initial = session.RoundNumber;
            for (int i = 0; i < session.Combatants.Count * 2 && session.RoundNumber == initial; i++)
            {
                Require(!session.IsComplete && session.CurrentActor != null,
                    "Detached round schedule fixture ended before the round boundary.");
                Require(session.TryExecuteCommand(new OffenseBattleCommand(session.LastProcessedCommandId + 1,
                        session.CurrentActor.PersistentId, OffenseBattleActionType.Guard), out var result),
                    "Detached round schedule guard failed: " + result.Message);
            }
            Require(session.RoundNumber == initial + 1, "Detached round schedule did not advance exactly one round.");
        }

        OffenseBattleSession death = Create(false);
        Require(death.CurrentActor.PersistentId != reserveId, "Death fixture requires a different current actor.");
        death.FindCombatant(reserveId).RestoreHealth(0f, 100f);
        Require(death.InitiativeOrder.Contains(reserveId), "Mid-round death unexpectedly rebuilt the schedule.");
        death = CheckAndRestore(death, "mid-round-dead-retained");
        NextRound(death);
        Require(!death.InitiativeOrder.Contains(reserveId), "Next round retained the dead participant.");
        CheckAndRestore(death, "next-round-dead-excluded");

        OffenseBattleSession recovery = Create(true);
        Require(recovery.FindCombatant(reserveId).IsDowned && !recovery.InitiativeOrder.Contains(reserveId),
            "Round construction did not exclude the downed fixture.");
        recovery = CheckAndRestore(recovery, "downed-excluded-restored");
        OffenseBattleCombatant recovering = recovery.FindCombatant(reserveId);
        // The same status-owner command used by the production Cleanse effect.
        Require(recovering.RemoveStatuses(value => value.Type == OffenseBattleStatusType.Sedated, 1) == 1
                && !recovering.IsDowned && !recovery.InitiativeOrder.Contains(reserveId),
            "Status recovery did not preserve the current round's schedule.");
        recovery = CheckAndRestore(recovery, "recovered-alive-waits-for-next-round");
        NextRound(recovery);
        Require(recovery.InitiativeOrder.Contains(reserveId)
                && recovery.FindCombatant(reserveId).ParticipatesInInitiative,
            "Restored recovered combatant lost next-round eligibility.");
        CheckAndRestore(recovery, "restored-recovery-rejoins-next-round");
    }

    private bool PrepareSkillBattleTurn(IOffenseBattleRuntime battle, string skillId, ICollection<string> commands = null)
    {
        for (int turn = 0; turn < 40; turn++)
        {
            if (!battle.HasActiveBattle) return false;
            battle.AdvanceToPlayerDecision();
            if (!battle.HasActiveBattle) return false;
            OffenseBattleSnapshot session = battle.Session;
            OffenseBattleCombatant acting = session.CurrentActor;
            Require(acting != null && acting.Team == OffenseBattleTeam.Allies, "Normal battle did not offer a player turn.");
            if (acting.PersistentId == owner.Identity.PersistentId && acting.GetCooldown(skillId) == 0)
            {
                CharacterCombatAbilityDefinition ability = acting.Abilities.SingleOrDefault(value => value.Id == skillId);
                Require(ability != null, "Selected skill is not in the actual battle ability catalog.");
                Require((ability.UsableFrom & OffenseFormationUtility.ToMask(acting.Formation)) != 0,
                    "Actual selected skill actor is outside its frozen legal source formation.");
                // A support skill must have a real injured legal recipient.
                // When nobody is injured, normal player/enemy turns may create
                // that condition; never seed damage or bypass target rules.
                if (FindSkillBattleHealthTarget(session, ability) != null) return true;
            }
            IssueSkillPreparationTurn(battle, commands);
        }
        throw new InvalidOperationException("Selected skill received no usable turn within 40 normal player commands.");
    }

    private static void IssueSkillPreparationTurn(IOffenseBattleRuntime battle, ICollection<string> commands)
    {
        OffenseBattleSnapshot session = battle.Session;
        OffenseBattleCombatant actor = session.CurrentActor;
        Require(actor != null && actor.Team == OffenseBattleTeam.Allies, "Preparation requires an actual public player turn.");
        Require(battle.TryGetActor(actor.PersistentId, out CharacterActor actingOwner),
            "Preparation player turn lost its registered actor.");
        var generatedSkillIds = new HashSet<string>(actingOwner.Progression.ActiveSkills
            .Where(value => value.formulaVersion > 0).Select(value => value.id), StringComparer.Ordinal);
        OffenseBattleCombatant[] enemies = session.Combatants.Where(value => value.Team != actor.Team
            && !value.IsDead && !value.IsDowned).OrderBy(value => value.CurrentHealth)
            .ThenBy(value => value.PersistentId, StringComparer.Ordinal).ToArray();
        CharacterCombatAbilityDefinition attack = actor.Abilities.Where(value => value.TargetRule == OffenseBattleTargetRule.Enemy
            && actor.GetCooldown(value.Id) == 0
            && (value.UsableFrom & OffenseFormationUtility.ToMask(actor.Formation)) != 0
            && value.Effects.Any(effect => effect is OffenseDamageEffect or OffenseDamageOverTimeEffect
                or OffenseConditionalAmplifyEffect or OffenseMultiTargetEffect)
            && enemies.Any(target => (value.TargetPositions & OffenseFormationUtility.ToMask(target.Formation)) != 0))
            // Use an already learned production formula before the old quick
            // diagnostic draft; selection does not grant or upgrade a skill.
            .OrderByDescending(value => generatedSkillIds.Contains(value.Id))
            .ThenBy(value => value.Id, StringComparer.Ordinal).FirstOrDefault();
        OffenseBattleActionType action = OffenseBattleActionType.Guard;
        string abilityId = string.Empty, targetId = string.Empty;
        if (attack != null)
        {
            action = OffenseBattleActionType.Ability;
            abilityId = attack.Id;
            targetId = enemies.First(value => (attack.TargetPositions & OffenseFormationUtility.ToMask(value.Formation)) != 0).PersistentId;
        }
        else
        {
            bool hasForwardEnemy = enemies.Any(value => value.Formation != OffenseFormationSlot.Rear);
            OffenseBattleCombatant target = enemies.FirstOrDefault(value => battle.PreviewBasicAttack(
                    actor.PersistentId,
                    value.PersistentId).Valid
                && (actor.Weapon?.IsRanged == true || !hasForwardEnemy || value.Formation != OffenseFormationSlot.Rear));
            if (target != null)
            {
                action = OffenseBattleActionType.BasicAttack;
                targetId = target.PersistentId;
            }
        }
        string actorId = actor.PersistentId;
        Require(battle.TryIssuePlayerCommand(action, targetId, abilityId, out var result),
            "Normal preparation player command rejected: " + result.Message);
        commands?.Add("battle:" + action + ":actor=" + actorId + ":target=" + targetId + ":ability=" + abilityId);
    }

    private IEnumerator PrepareRestoredSkillReuseRoute(
        SkillMechanicWitness witness, IOffenseBattleRuntime battle, Action Observe)
    {
        phase = "skill-normal-reuse-route";
        CharacterCombatAbilityDefinition ability = CharacterCombatAbilityCatalog.GetAbilities(owner)
            .Single(value => value.Id == witness.skillId);
        Require(witness.applied.effectKind == "heal"
                && ability.TargetRule.ToString() == "Self",
            "Finite battle continuation is only defined for this actual Self-heal reuse scenario.");
        OffenseExpeditionRuntime expeditions = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>();
        Require(expeditions != null && !battle.HasActiveBattle,
            "Self-heal reuse requires a completed battle and the registered expedition authority.");
        var route = witness.restoredReuseRoute = new SkillReuseRouteWitness
        {
            completedBattleId = witness.applied.battleId,
            expeditionId = witness.expeditionId,
            actorId = witness.actorId,
            actorHealthBefore = owner.CurrentHealth
        };
        OffenseExpeditionRun FindRun() => expeditions.ActiveExpeditions.Single(value =>
            value.ExpeditionId == witness.expeditionId);
        OffenseExpeditionRun expedition = FindRun();
        Require(!expedition.UsesWorldTravel && expedition.Phase == OffenseExpeditionPhase.ChoosingRoute,
            "Completed Self-heal battle offered no normal authored route continuation.");
        OffenseFormationSlot originalFormation = expedition.MemberStates.Single(value =>
            value.Actor?.Identity?.PersistentId == witness.actorId).Formation;
        Require((ability.UsableFrom & OffenseFormationUtility.ToMask(originalFormation)) != 0,
            "Saved support actor did not retain its actual legal skill formation.");

        void SwapTo(OffenseFormationSlot formation)
        {
            var members = expedition.MemberStates.ToList();
            int selected = members.FindIndex(value => value.Actor?.Identity?.PersistentId == witness.actorId);
            Require(selected >= 0, "Reuse expedition lost the selected actor.");
            if (members[selected].Formation == formation) return;
            int destination = members.FindIndex(value => value.Formation == formation && value.IsAlive);
            Require(destination >= 0 && expeditions.TrySwapFormation(expedition.ExpeditionId,
                    selected, destination, out _),
                "Public formation swap for the next actual encounter was rejected.");
            Require(expedition.MemberStates.Single(value =>
                    value.Actor?.Identity?.PersistentId == witness.actorId).Formation == formation,
                "Public reuse formation command did not change the authoritative formation.");
            route.publicCommands.Add("formation:" + formation);
        }

        for (int nextBattle = 0; nextBattle < 3; nextBattle++)
        {
            Require(!owner.IsDead && !expedition.IsComplete
                    && expedition.Phase == OffenseExpeditionPhase.ChoosingRoute,
                "Actual support reuse has no living actor/continuing authored expedition.");
            bool needsInjury = owner.CurrentHealth >= owner.MaxHealth - 0.001f;
            SwapTo(needsInjury ? OffenseFormationSlot.Front : originalFormation);
            for (int nodeStep = 0; nodeStep < 4 && route.publicRouteCommands < 4
                    && !battle.HasActiveBattle; nodeStep++)
            {
                OffenseRouteNode node = expedition.GetAvailableRouteNodes()
                    .OrderByDescending(value => value.StartsBattle)
                    .ThenByDescending(value => value.Kind == OffenseRouteNodeKind.Cache)
                    .ThenBy(value => value.Id, StringComparer.Ordinal).FirstOrDefault();
                Require(node != null && expeditions.TryChooseRouteNode(expedition.ExpeditionId, node.Id, out _),
                    "Public reuse route command found no accepted authored next node.");
                route.publicCommands.Add("route:" + node.Id);
                route.publicRouteCommands++;
                if (!node.StartsBattle)
                {
                    Require(expeditions.TryResolveCurrentNode(expedition.ExpeditionId, false, out _, out _),
                        "Public reuse non-battle node resolution was rejected.");
                    route.publicCommands.Add("resolve-without-supply:" + node.Id);
                }
            }
            Require(battle.HasActiveBattle && battle.Session.ExpeditionId == witness.expeditionId,
                "Normal reuse route did not start this same expedition's authored battle.");
            route.battleIds.Add(battle.Session.BattleId);
            if (!needsInjury)
            {
                route.legalFormationRestored = true;
                if (PrepareSkillBattleTurn(battle, witness.skillId, route.publicCommands))
                {
                    WriteNew("u4-skill-reuse-route-witness.json", JsonUtility.ToJson(route, true));
                    yield break;
                }
            }
            else
            {
                phase = "skill-normal-reuse-injury-battle";
                OffenseBattleEncounterRules rules = battle.Session.EncounterRules;
                Require(rules.RoundLimit > 0 && rules.Objective is
                        OffenseEncounterObjective.SurviveRounds or
                        OffenseEncounterObjective.ProtectTarget or OffenseEncounterObjective.Escape,
                    "Actual injury scenario requires a bounded survival/protection/escape encounter.");
                float healthAtStart = battle.Session.FindCombatant(witness.actorId).CurrentHealth;
                route.injuryBattleStartingHealth = route.lowestObservedBattleHealth = healthAtStart;
                bool injured = false;
                for (int command = 0; command < 64 && route.publicBattleCommands < 64
                        && battle.HasActiveBattle; command++)
                {
                    battle.AdvanceToPlayerDecision();
                    if (!battle.HasActiveBattle) break;
                    OffenseBattleCombatant selected = battle.Session.FindCombatant(witness.actorId);
                    Require(selected != null && !selected.IsDead,
                        "The selected support actor died in the actual injury encounter.");
                    injured |= selected.CurrentHealth < healthAtStart - 0.001f;
                    route.lowestObservedBattleHealth = Math.Min(route.lowestObservedBattleHealth, selected.CurrentHealth);
                    route.actualBattleInjuryObserved |= injured;
                    if (rules.Objective == OffenseEncounterObjective.Escape && battle.Session.RoundNumber >= rules.RoundLimit)
                    {
                        Require(battle.TryIssuePlayerCommand(OffenseBattleActionType.Retreat, string.Empty, string.Empty, out _),
                            "Public retreat command was rejected in the actual support injury encounter.");
                        route.publicCommands.Add("battle:Retreat");
                    }
                    else IssueSkillPreparationTurn(battle, route.publicCommands);
                    route.publicBattleCommands++;
                    Observe();
                    yield return null;
                }
                Require(!battle.HasActiveBattle,
                    "Actual injury encounter did not complete within its bounded public-command scenario.");
            }
            Require(!owner.IsDead && expedition.Phase == OffenseExpeditionPhase.ChoosingRoute,
                "Actual reuse encounter ended without a surviving actor and next route.");
            route.actorHealthAfterBattle = owner.CurrentHealth;
            string terminalName = "u4-skill-reuse-route-terminal-" + nextBattle + "-world.json";
            string terminalWorld = saves.ToJson(saves.Capture());
            WriteNew(terminalName, terminalWorld);
            route.terminalWorldArtifacts.Add(terminalName);
            string selectedSkill = JsonUtility.ToJson(owner.Progression.ActiveSkills.Single(value => value.id == witness.skillId));
            CharacterActor previous = owner;
            phase = "skill-normal-reuse-terminal-restore";
            Require(saves.TryRestore(saves.FromJson(terminalWorld), out DungeonGameRestoreReport restored),
                "Official terminal battle restore failed: " + string.Join(" | ", restored.Errors));
            Require(scope.Container.Resolve<ICharacterWorldSaveService>().TryGetRestoredActor(witness.actorId, out owner)
                    && owner != null && !ReferenceEquals(previous, owner) && !battle.HasActiveBattle
                    && selectedSkill == JsonUtility.ToJson(owner.Progression.ActiveSkills.Single(value => value.id == witness.skillId)),
                "Terminal battle restore lost the fresh support actor, exact skill or completed battle state.");
            route.terminalOfficialFreshRestore = true;
            expedition = FindRun();
            // An authored encounter can legitimately damage a different ally
            // or nobody. It is not an effect PASS: retain its exact terminal
            // save and use only the already bounded next-route attempts. The
            // caller still requires an actual restored heal, not just a turn.
            bool needsAnotherEncounter = NeedsFurtherSelfHealInjury(
                route.actualBattleInjuryObserved, owner.CurrentHealth, owner.MaxHealth);
            if (needsAnotherEncounter) route.noSelfHealRecipientBattleIds.Add(route.battleIds.Last());
            WriteNew("u4-skill-reuse-route-stage-" + nextBattle + "-witness.json", JsonUtility.ToJson(route, true));
            if (needsAnotherEncounter) continue;
        }
        throw new InvalidOperationException("No second actual Self-heal recipient was available along the bounded normal authored route.");
    }

    private static bool NeedsFurtherSelfHealInjury(bool actualBattleInjuryObserved, float health, float maxHealth)
        => !actualBattleInjuryObserved || health >= maxHealth - 0.001f;

    public static void VerifyUninjuredReusePreparationFromBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrEmpty(output), "Isolated evidence output is required.");
        string input = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Artifacts/QA/AuditRemediation-20260926-r1/u4-skill-staff-replay-r19/unity-evidence/u4-skill-reuse-route-stage-0-witness.json"));
        byte[] bytes = File.ReadAllBytes(input);
        const string inputSha = "d435d1472cf27b57aa478cf3ced45a2ea89d223c733ab4fb362fb54d0589a588";
        Require(Hash(bytes) == inputSha, "Original failed-run no-injury record changed.");
        SkillReuseRouteWitness route = JsonUtility.FromJson<SkillReuseRouteWitness>(Encoding.UTF8.GetString(bytes));
        Require(route.actorId == "character:staff:1:02" && route.terminalOfficialFreshRestore
            && route.actorHealthBefore == 100f && route.actorHealthAfterBattle == 100f
            && route.lowestObservedBattleHealth == 100f && !route.actualBattleInjuryObserved,
            "Original failed-run record must show the actual uninjured surviving selected actor.");
        Require(NeedsFurtherSelfHealInjury(route.actualBattleInjuryObserved, route.actorHealthAfterBattle, 100f),
            "Actual uninjured encounter must require another attempt, never an effect PASS.");
        Require(NeedsFurtherSelfHealInjury(false, 80f, 100f)
            && NeedsFurtherSelfHealInjury(true, 100f, 100f)
            && !NeedsFurtherSelfHealInjury(true, 83.833954f, 100f),
            "Missing injury observation/full health must not satisfy an injured-recipient boundary.");
        Directory.CreateDirectory(output);
        using (var stream = new FileStream(Path.Combine(output, "uninjured-reuse-preparation-report.json"), FileMode.CreateNew))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            writer.Write(new JObject { ["passed"] = true, ["input"] = input,
                ["inputSha256"] = inputSha, ["checks"] = 4,
                ["scope"] = "preparation classification only; actual restored reexecution remains NOT_RUN",
                ["trainingEligible"] = false, ["humanApprovalClaimed"] = false }.ToString());
        Debug.Log("UNINJURED_REUSE_PREPARATION=PASS checks=4");
    }

    private SkillBattleCommandWitness ExecuteSkillBattleCommand(IOffenseBattleRuntime battle, string skillId)
    {
        OffenseBattleSession session = (battle as OffenseBattleRuntime)?.OwnedSession
            ?? throw new InvalidOperationException(
                "Skill battle command witness requires the concrete runtime owned session.");
        CharacterCombatAbilityDefinition ability = session.CurrentActor.Abilities.Single(value => value.Id == skillId);
        OffenseBattleCombatant target = FindSkillBattleHealthTarget(session, ability);
        Require(target != null, "Normal battle has no legal recipient for the selected health effect.");
        SkillBattleCommandWitness witness = new()
        {
            battleId = session.BattleId, actorId = session.CurrentActor.PersistentId,
            targetId = target.PersistentId, abilityId = skillId,
            commandId = session.LastProcessedCommandId + 1, targetHealthBefore = target.CurrentHealth,
            effectKind = SkillBattleHealthEffectKind(ability), targetRule = ability.TargetRule.ToString(),
            targetTeam = target.Team.ToString(), expectedCooldown = ability.CooldownTurns
        };
        var command = new OffenseBattleCommand(witness.commandId, witness.actorId,
            OffenseBattleActionType.Ability, witness.targetId, skillId);
        // TryIssuePlayerCommand runs subsequent enemy turns synchronously.
        // Observe the committed command event first, so later enemy damage
        // cannot conceal an actual heal or be misreported as its outcome.
        void ObserveCommandBoundary()
        {
            if (witness.commandBoundaryObserved || session.LastProcessedCommandId != witness.commandId) return;
            witness.targetHealthAfter = target.CurrentHealth;
            witness.cooldownAfter = session.FindCombatant(witness.actorId).GetCooldown(skillId);
            witness.commandBoundaryObserved = true;
        }
        OffenseBattleCommandResult result;
        battle.StateChanged += ObserveCommandBoundary;
        try
        {
            witness.accepted = battle.TryIssuePlayerCommand(command.ActionType, command.TargetId, command.AbilityId, out result);
        }
        finally { battle.StateChanged -= ObserveCommandBoundary; }
        witness.message = result.Message;
        // Production Amount is damage dealt, not healing. Retain it verbatim;
        // healing is proven by the target's authoritative HP and ledger metrics.
        witness.amount = result.Amount;
        witness.targetHealthAfterFollowingTurns = target.CurrentHealth;
        witness.appliedHealthChange = witness.effectKind == "heal"
            ? witness.targetHealthAfter - witness.targetHealthBefore
            : witness.targetHealthBefore - witness.targetHealthAfter;
        witness.cooldownExact = witness.commandBoundaryObserved && witness.cooldownAfter == witness.expectedCooldown;
        string operation = witness.battleId + ":command:" + witness.commandId;
        List<GameplayOutcomeSnapshot> ExactRows() => ReadAllOutcomes().Where(value => value.facts.Any(fact =>
            fact.factId == MigratedProducerOutcomeIds.OperationFact.Value && fact.value == operation)).ToList();
        witness.outcomes = ExactRows();
        witness.exactOutcome = witness.outcomes.Count == 1
            && witness.outcomes[0].status == GameplayOutcomeStatus.Succeeded
            && witness.outcomes[0].rootOperationId == session.ExpeditionId
            && witness.outcomes[0].causationRelationId == "expedition-battle-command"
            && witness.outcomes[0].participants.Any(subject => subject.entityKindId == MigratedProducerOutcomeIds.AbilityKind.Value
                && subject.entityId == skillId)
            && witness.outcomes[0].participants.Any(subject => subject.entityKindId == MigratedProducerOutcomeIds.CharacterKind.Value
                && subject.entityId == witness.actorId && subject.roleId == MigratedProducerOutcomeIds.ActorRole.Value)
            && (witness.targetId == witness.actorId || witness.outcomes[0].participants.Any(subject =>
                subject.entityKindId == MigratedProducerOutcomeIds.CharacterKind.Value
                && subject.entityId == witness.targetId && subject.roleId == MigratedProducerOutcomeIds.TargetRole.Value))
            && witness.outcomes[0].facts.Any(fact => fact.factId == MigratedProducerOutcomeIds.TargetFact.Value
                && fact.value == witness.targetId);
        bool ExactHealthMetric(string id, float expected) => witness.outcomes.Count == 1
            && witness.outcomes[0].metrics.Count(value => value.metricId == id
                && value.referenceKindId == MigratedProducerOutcomeIds.CharacterKind.Value
                && value.referenceId == witness.targetId && value.value == (double)expected) == 1;
        witness.healthOutcomeExact = witness.commandBoundaryObserved
            && ExactHealthMetric(MigratedProducerOutcomeIds.HealthBeforeMetric.Value, witness.targetHealthBefore)
            && ExactHealthMetric(MigratedProducerOutcomeIds.HealthAfterMetric.Value, witness.targetHealthAfter);
        string appliedHash = SkillWorldStateHash(saves.ToJson(saves.Capture()));
        bool repeatedAccepted = battle.TryExecuteCommand(command, out _);
        witness.duplicateOutcomeCount = ExactRows().Count;
        witness.duplicateStable = !repeatedAccepted && witness.duplicateOutcomeCount == 1
            && appliedHash == SkillWorldStateHash(saves.ToJson(saves.Capture()));
        return witness;
    }

    private static bool HasActualSkillHealthEffect(SkillBattleCommandWitness witness) =>
        witness.accepted && witness.commandBoundaryObserved && witness.appliedHealthChange > 0f
        && (witness.effectKind == "heal" || (witness.effectKind == "damage" && witness.amount > 0f))
        && witness.cooldownExact && witness.exactOutcome && witness.healthOutcomeExact && witness.duplicateStable;

    private static string SkillBattleHealthEffectKind(CharacterCombatAbilityDefinition ability)
    {
        bool heal = ability.Effects.OfType<OffenseHealEffect>().Any(value => value.FlatAmount > 0f);
        bool damage = ability.Effects.Any(value => value is OffenseDamageEffect
            or OffenseDamageOverTimeEffect or OffenseConditionalAmplifyEffect or OffenseMultiTargetEffect);
        Require(heal != damage, "Health-effect witness requires either healing or damage, not an unsupported/mixed effect.");
        Require(!heal || ability.TargetRule != OffenseBattleTargetRule.Enemy,
            "Offered healing skill requires a legal self/ally target rule.");
        return heal ? "heal" : "damage";
    }

    private static OffenseBattleCombatant FindSkillBattleHealthTarget(OffenseBattleSession session,
        CharacterCombatAbilityDefinition ability)
        => FindSkillBattleHealthTarget(session?.Combatants, session?.CurrentActor, ability);

    private static OffenseBattleCombatant FindSkillBattleHealthTarget(OffenseBattleSnapshot session,
        CharacterCombatAbilityDefinition ability)
        => FindSkillBattleHealthTarget(session?.Combatants, session?.CurrentActor, ability);

    private static OffenseBattleCombatant FindSkillBattleHealthTarget(
        IReadOnlyList<OffenseBattleCombatant> combatants,
        OffenseBattleCombatant actor,
        CharacterCombatAbilityDefinition ability)
    {
        bool heal = SkillBattleHealthEffectKind(ability) == "heal";
        return (combatants ?? Array.Empty<OffenseBattleCombatant>()).Where(value => !value.IsDead
                && (ability.TargetPositions & OffenseFormationUtility.ToMask(value.Formation)) != 0
                && (ability.TargetRule == OffenseBattleTargetRule.Self ? ReferenceEquals(actor, value)
                    : ability.TargetRule == OffenseBattleTargetRule.Ally ? value.Team == actor.Team
                    : value.Team != actor.Team && !value.IsDowned)
                && (!heal || value.CurrentHealth < value.Stats.MaxHealth))
            .OrderByDescending(value => heal ? value.Stats.MaxHealth - value.CurrentHealth : value.CurrentHealth)
            .ThenBy(value => value.PersistentId, StringComparer.Ordinal).FirstOrDefault();
    }

    private void WriteSkillReplayProof(
        CharacterSkillDraft beforeDraft,
        CharacterSkillDraft appliedDraft,
        CharacterSkillDraft restoredDraft,
        string beforeWorld,
        string appliedWorld,
        string restoredWorld)
    {
        JObject Artifact(string path, string world, CharacterSkillDraft draft) =>
            GameplayModuleSelectionReplayProofV2Contract.Artifact(
                path,
                Encoding.UTF8.GetBytes(world),
                JObject.Parse(JsonUtility.ToJson(draft)));
        JObject beforeArtifact = Artifact(
            "u4-before-world.json", beforeWorld, beforeDraft);
        JObject appliedArtifact = Artifact(
            "u4-applied-world.json", appliedWorld, appliedDraft);
        JObject restoredArtifact = Artifact(
            "u4-restored-world.json", restoredWorld, restoredDraft);
        JObject Assertion(string id, bool passed) =>
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                id, passed, true, passed);
        JObject request = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                skillReplayInput.ProfileId,
                skillReplayInput.SourceManifestSha256,
                skillReplayInput.ResponseManifestSha256,
                skillReplayInput.RequestId,
                skillReplayInput.ResponseSha256,
                skillReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.RequestAcceptedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "The exact source-v9 batch request accepted one explicit integration response without network access.",
                beforeArtifact, appliedArtifact, restoredArtifact,
                Array.Empty<string>(), Array.Empty<string>(),
                skillReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                new[]
                {
                    Assertion("response-requested-exactly-once",
                        skillReplayResponseCount == 1),
                    Assertion("wire-byte-exact",
                        skillReplayCapture != null)
                },
                Array.Empty<string>(), Array.Empty<string>());
        JObject candidate = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                skillReplayInput.ProfileId,
                skillReplayInput.SourceManifestSha256,
                skillReplayInput.ResponseManifestSha256,
                skillReplayInput.RequestId,
                skillReplayInput.ResponseSha256,
                skillReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.CandidateStoredStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "The production skill service stored three unchosen candidates and official restore reproduced them exactly.",
                beforeArtifact, appliedArtifact, restoredArtifact,
                skillReplayInput.SelectedModuleIds,
                skillReplayInput.FormulaIds,
                skillReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                new[]
                {
                    Assertion("candidate-count-three",
                        appliedDraft.candidates.Count == 3),
                    Assertion("candidate-ready-unselected",
                        appliedDraft.isReady
                        && !appliedDraft.permanentlyChosen
                        && appliedDraft.chosenIndex == -1),
                    Assertion("official-round-trip-exact",
                        JsonUtility.ToJson(appliedDraft)
                            == JsonUtility.ToJson(restoredDraft))
                },
                Array.Empty<string>(), Array.Empty<string>());
        JObject committed = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                skillReplayInput.ProfileId,
                skillReplayInput.SourceManifestSha256,
                skillReplayInput.ResponseManifestSha256,
                skillReplayInput.RequestId,
                skillReplayInput.ResponseSha256,
                skillReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.SelectionCommittedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "An explicit simulated-player index committed one skill and one exact evidence use through the public command; official full-world restore and repeated choices preserve it.",
                skillSelectionBeforeArtifact, skillSelectionAppliedArtifact,
                skillSelectionRestoredArtifact,
                skillSelectionModules, new[] { skillSelectionFormulaId }, skillSelectionFacts,
                skillSelectionInfluenceDeltas,
                new[]
                {
                    Assertion("public-selection-committed-once", report.skillSelection.committed),
                    Assertion("unconfirmed-and-invalid-atomic", report.skillSelection.unconfirmedAtomic
                        && report.skillSelection.invalidIndexAtomic),
                    Assertion("duplicate-and-different-choice-stable", report.skillSelection.duplicateStable
                        && report.skillSelection.differentChoiceRejected),
                    Assertion("official-fresh-state-and-evidence-exact", report.skillSelection.officialFreshRestore
                        && report.skillSelection.officialStateExact && report.skillSelection.officialEvidenceExact),
                    Assertion("restored-duplicate-stable", report.skillSelection.restoredDuplicateStable)
                },
                Array.Empty<string>(), Array.Empty<string>());
        JObject mechanic = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                skillReplayInput.ProfileId,
                skillReplayInput.SourceManifestSha256,
                skillReplayInput.ResponseManifestSha256,
                skillReplayInput.RequestId,
                skillReplayInput.ResponseSha256,
                skillReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.MechanicExecutedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "Authoritative research admission and registered physical departure reach an actual battle; the selected Ability applies its offered health effect to a legal target, records one exact command/health outcome, rejects duplicate commands and repeats the effect after official full-world restore, exact authored cooldown state and the next legal turn."
                    + (skillEquipmentPreparationFixture
                        ? " Explicit non-target equipment, environment and optional accelerated-work preparation is not natural-work evidence and ends before expedition/battle."
                        : string.Empty),
                skillMechanicBeforeArtifact, skillMechanicAppliedArtifact, skillMechanicRestoredArtifact,
                skillSelectionModules, new[] { skillSelectionFormulaId }, skillSelectionFacts,
                Array.Empty<JObject>(),
                new[]
                {
                    Assertion("normal-physical-admission", (report.skillMechanic.blueprintArchived
                        || report.skillMechanic.researchPrerequisitesAlreadyComplete)
                        && report.skillMechanic.researchCompleted && report.skillMechanic.departureCompleted),
                    Assertion("actual-selected-ability-exact-outcome", report.skillMechanic.actualEffect),
                    Assertion("official-fresh-combat-skill-evidence-exact", report.skillMechanic.officialFreshRestore
                        && report.skillMechanic.battleExact && report.skillMechanic.skillExact && report.skillMechanic.evidenceExact),
                    Assertion("restored-command-duplicate-stable", report.skillMechanic.restoredDuplicateStable),
                    Assertion("actual-reexecution-after-restore", report.skillMechanic.restoredActualEffect),
                    Assertion("preparation-fixtures-ended-before-effect", !skillEquipmentPreparationFixture
                        || (nonTargetPreparationFixturesClosed && nonTargetDailyNutritionHold == null
                            && nonTargetExposureHold == null && nonTargetInstantWorkHold == null))
                },
                new[] { "u4-skill-mechanic-witness.json", "u4-skill-admission-witness.json" }, Array.Empty<string>());
        JArray proof = GameplayModuleSelectionReplayProofV2Contract
            .ValidateProofSet(new[] { request, candidate, committed, mechanic });
        byte[] proofBytes = GameplayModuleSelectionCanonicalJson.ToUtf8(proof);
        WriteNew("u4-replay-proof-v2.json", Encoding.UTF8.GetString(proofBytes));
        JObject manifest = new()
        {
            ["schema"] = "dungeonstory-gameplay-module-selection-replay-proof@2",
            ["profileId"] = skillReplayInput.ProfileId,
            ["requestId"] = skillReplayInput.RequestId,
            ["sourceManifestSha256"] = skillReplayInput.SourceManifestSha256,
            ["responseManifestSha256"] = skillReplayInput.ResponseManifestSha256,
            ["proof"] = new JObject
            {
                ["path"] = "u4-replay-proof-v2.json",
                ["bytes"] = proofBytes.Length,
                ["sha256"] = GameplayModuleSelectionCanonicalJson
                    .Sha256Prefixed(proofBytes)
            },
            ["stageCount"] = proof.Count,
            ["networkSent"] = false,
            ["responseApplied"] = true,
            ["integrationFixture"] = true,
            ["selectionCommitted"] = report.skillSelectionCommitted,
            ["mechanicExecuted"] = report.skillMechanicExecuted,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["dpoPerformed"] = false,
            ["releaseReady"] = false
        };
        byte[] manifestBytes = GameplayModuleSelectionCanonicalJson.ToUtf8(manifest);
        WriteNew("u4-replay-proof-v2-manifest.json",
            Encoding.UTF8.GetString(manifestBytes));
        report.u4ProofWritten = true;
        report.u4ProofManifestSha256 = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(manifestBytes);
    }

    private IEnumerator RunTraitReplay()
    {
        phase = "trait-source-v9-replay";
        replaying = true;
        gameSpeed.SetPaused(true);
        timeScale.Scale = 0f;
        CancelOutstandingTransport();
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        traitReplayInput = GameplayModuleSelectionSourceV9ReplayInput.LoadTrait(
            projectRoot,
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_SOURCE_V9_REPLAY_PACKAGE"),
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_SOURCE_V9_REPLAY_RESPONSE"),
            Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_LIVE_NARRATIVE_RESUME_WORLD"));
        report.executionOrigin = "official source-v9 world + unchanged "
            + traitReplayInput.AuthoringOrigin + " response + controlled gameplay fixture";
        JArray transportDiagnostics = new();
        using IDisposable transportObservation = scope.Container.Resolve<IGameEventBus>()
            .Subscribe<CharacterAcquiredTraitManifestationDiagnosticEvent>(value =>
            {
                var diagnostic = value.Diagnostic;
                if (diagnostic.TargetPersistentId != traitReplayInput.OwnerId) return;
                transportDiagnostics.Add(JObject.FromObject(new
                {
                    status = diagnostic.Status.ToString(), diagnostic.RequestId, diagnostic.RequestKey,
                    diagnostic.Attempt, diagnostic.NextRetryAt, issue = diagnostic.IssueCode.ToString(),
                    transportStatus = diagnostic.TransportStatus?.ToString(), diagnostic.Detail,
                    queue = queue.GetRequestDiagnosticsForDebug(), queueError = queue.LastError
                }));
            });
        Require(string.Equals(owner.Identity.PersistentId,
                traitReplayInput.OwnerId, StringComparison.Ordinal),
            "Restored official owner differs from the source-v9 trait owner.");
        // Prepare the same real work target on both sides of the response while
        // the game is frozen. This isolates the trait's consumer-rate change
        // from proficiency gains, elapsed needs and a different target.
        bool combatTrait = traitReplayInput.MechanicTargetId == GameplayEffectTargetIds.CombatPower;
        bool workTrait = traitReplayInput.MechanicTargetId == GameplayEffectTargetIds.WorkSpeed;
        if (traitReplayInput.SelectedModuleIds.Contains("acquired-trait:module:night-vigil", StringComparer.Ordinal))
        {
            int hourBefore = calendar.Hour;
            if (calendar.Hour >= 6 && calendar.Hour < 18)
                calendar.SetDateTime(calendar.Day, 18);
            WriteNew("u4-trait-night-test-preparation.json", JObject.FromObject(new
            {
                testOnly = true, trainingEligible = false, naturalGameplay = false,
                hourBefore, hourAfter = calendar.Hour, changedOnlyCalendarFixture = true,
                effectConditionInjected = false, responseChanged = false
            }).ToString());
        }
        GameplayEffectContext EffectContext() => workTrait
            ? new GameplayEffectContext(new[] { calendar.Hour >= 18 || calendar.Hour < 6 ? "shift:night" : "shift:day" })
            : null;
        WorldFilthWorkTarget workTarget = workTrait ? PrepareTraitWorkFixture() : null;
        string controlledWeaponId = combatTrait ? PrepareTraitCombatFixture() : null;
        IWorkAmountCalculator workCalculator = scope.Container.Resolve<IWorkAmountCalculator>();
        IRoomEnvironmentQuery workEnvironment = scope.Container.Resolve<IRoomEnvironmentQuery>();
        float CleaningRate() => workCalculator.CalculateWorkPerSecond(
                owner, workTarget, BuiltInWorkTypeIds.Clean,
                workEnvironment.GetWorkDurationMultiplier(workTarget, BuiltInWorkTypeIds.Clean))
            * Mathf.Max(0.05f, CharacterSkillRuntimeEffects.GetCleaningSpeedMultiplier(owner));
        float ConsumerValue() => combatTrait ? owner.GetCombatPowerMultiplier() : workTrait ? CleaningRate() : 0f;
        TraitMechanicWitness mechanicWitness = new()
        {
            targetId = traitReplayInput.MechanicTargetId,
            bindingId = traitReplayInput.MechanicBindingId,
            controlledWeaponInstanceId = controlledWeaponId,
            consumerValueBefore = ConsumerValue()
        };
        if (combatTrait)
            mechanicWitness.consumerPath = "CharacterCombatCommandRuntime.TryIssueAttack -> CombatResolutionService.CalculateRawDamage -> CombatCommandResultApplier.Apply";
        else
            mechanicWitness.workRateBefore = mechanicWitness.consumerValueBefore;
        report.traitMechanic = mechanicWitness;
        CharacterAcquiredTraitAggregateState beforeState = owner.Progression
            .CaptureAcquiredTraitState();
        CharacterAcquiredTraitPendingRequestState beforePending = beforeState
            .pendingRequests.Single(value => value != null
                && string.Equals(value.requestId,
                    traitReplayInput.TraitRequestId, StringComparison.Ordinal));
        Require(beforePending.presentationState
                    == CharacterAcquiredTraitPresentationState.ModuleSelectionPending
                && string.Equals(beforePending.requestKey,
                    traitReplayInput.RequestId, StringComparison.Ordinal)
                && string.Equals(beforePending.moduleSelectionId,
                    traitReplayInput.FormulaIds.Single(), StringComparison.Ordinal),
            "Restored trait pending is not the fixed module-selection request.");
        Dictionary<string, GameplayOutcomeNarrativeEvidenceSource> beforeEvidence =
            new(StringComparer.Ordinal);
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in
                 traitReplayInput.SelectedEvidenceBindings)
        {
            Require(outcomeEvidence.TryResolveExactBinding(binding, out var source),
                "Selected trait evidence does not resolve exactly before replay: "
                    + binding.publicFactId);
            beforeEvidence.Add(binding.publicFactId, source);
        }
        CharacterDerivedStatsSnapshotProjector projector = derivedStats;
        float beforeWorkSpeed = projector.ProjectIncrementalMultiplier(
            owner, traitReplayInput.MechanicTargetId, EffectContext());
        List<GameplayOutcomeSnapshot> beforeOutcomes = ReadAllOutcomes();
        HashSet<string> beforeOutcomeIds = beforeOutcomes.Select(value =>
                value.runId + ":" + value.sequence)
            .ToHashSet(StringComparer.Ordinal);
        int beforeTraitOutcomeCount = beforeOutcomes.Count(value =>
            value.outcomeTypeId == EvolutionOutcomeIds.TraitInferenceCompleted.Value);
        string beforeWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-before-world.json", beforeWorld);
        WriteNew("u4-before-trait-state.json",
            JsonUtility.ToJson(beforeState, true));

        queue.ConfigureIsolatedModuleReplayTestResponses(captured =>
        {
            if (traitReplayCapture == null
                || !ReferenceEquals(captured, traitReplayCapture))
            {
                return null;
            }
            Require(traitReplayResponseCount == 0,
                "The explicit trait response was requested more than once.");
            traitReplayResponseCount++;
            return traitReplayInput.AcceptedResponseJson;
        });
        bool dispatched = false;
        float dispatchDeadline = Time.realtimeSinceStartup + 15f;
        while (!dispatched && Time.realtimeSinceStartup < dispatchDeadline)
        {
            traitRuntime.Tick();
            dispatched = queue.TryDispatchQueuedRequestForIsolatedCapture(
                traitReplayInput.RequestId);
            if (!dispatched)
                yield return null;
        }
        Require(dispatched,
            "The restored trait request was not uniquely queued for replay: "
                + queue.GetRequestDiagnosticsForDebug());
        float completionDeadline = Time.realtimeSinceStartup + 30f;
        CharacterAcquiredTraitAggregateState appliedState = owner.Progression
            .CaptureAcquiredTraitState();
        while (Time.realtimeSinceStartup < completionDeadline
            && (appliedState.revision == beforeState.revision
                || traitReplayResponseCount != 1))
        {
            yield return null;
            if (traitReplayResponseCount == 1
                && traitRuntime.TryGetLatestDiagnostic(
                    traitReplayInput.OwnerId, out var diagnostic)
                && diagnostic.IsFailure)
            {
                // Preserve the completion chain before a later dispatch status
                // can obscure the original callback rejection. No guard or
                // response is changed by this observation.
                WriteNew("u4-trait-transport-diagnostics.json", transportDiagnostics.ToString());
                WriteNew("u4-trait-response-failed-state.json", JsonUtility.ToJson(owner.Progression.CaptureAcquiredTraitState(), true));
                WriteNew("u4-trait-response-failed-world.json", saves.ToJson(saves.Capture()));
                queue.ClearIsolatedModuleReplayTestResponses();
                throw new InvalidOperationException(
                    "The fixed trait response was rejected: "
                    + diagnostic.Status + "/" + diagnostic.IssueCode
                    + ": " + diagnostic.Detail + "; queue=" + queue.GetRequestDiagnosticsForDebug()
                    + "; queueError=" + queue.LastError);
            }
            appliedState = owner.Progression.CaptureAcquiredTraitState();
        }
        queue.ClearIsolatedModuleReplayTestResponses();
        WriteNew("u4-trait-transport-diagnostics.json", transportDiagnostics.ToString());
        Require(traitReplayCapture != null && traitReplayResponseCount == 1
                && appliedState.revision == beforeState.revision + 1
                && appliedState.pendingRequests.All(value => value == null
                    || !string.Equals(value.requestId,
                        traitReplayInput.TraitRequestId,
                        StringComparison.Ordinal)),
            "The fixed trait response did not commit exactly once through the production callback.");
        CharacterAcquiredTraitInstanceState appliedInstance = appliedState.instances
            .Single(value => value != null
                && string.Equals(value.originatingRequestId,
                    traitReplayInput.TraitRequestId, StringComparison.Ordinal));
        Require(appliedInstance.acceptedRevision == appliedState.revision
                && appliedInstance.IsActive
                && appliedInstance.moduleIds.SequenceEqual(
                    traitReplayInput.SelectedModuleIds, StringComparer.Ordinal)
                && string.Equals(appliedInstance.displayName,
                    traitReplayInput.Response.displayName,
                    StringComparison.Ordinal)
                && string.Equals(appliedInstance.narrativeFlavor,
                    traitReplayInput.Response.narrativeFlavor,
                    StringComparison.Ordinal),
            "Committed trait identity or presentation differs from the fixed response.");
        report.responseApplied = true;
        WriteNew("u4-trait-callback-accepted-state.json", JsonUtility.ToJson(appliedState, true));
        WriteNew("u4-trait-callback-accepted-world.json", saves.ToJson(saves.Capture()));
        Require((workTrait || combatTrait) && traitReplayInput.Response.drawbackModuleIds.Count == 0,
            "Trait response was committed, but its selected consumer is not yet witnessed: "
                + traitReplayInput.MechanicTargetId + "; selected drawbacks="
                + traitReplayInput.Response.drawbackModuleIds.Count);
        float expectedWorkSpeedFactor = appliedInstance.effectOverrides
            .Single(value => value != null
                && string.Equals(value.bindingId,
                    traitReplayInput.MechanicBindingId,
                    StringComparison.Ordinal))
            .value;
        Require(expectedWorkSpeedFactor > 1f,
            "The committed selected trait formula override is not beneficial.");
        Dictionary<string, GameplayOutcomeNarrativeEvidenceSource> afterEvidence =
            CurrentTraitEvidence(traitReplayInput.SelectedEvidenceBindings
                .Select(value => value.publicFactId));
        Require(beforeEvidence.All(pair => afterEvidence.TryGetValue(
                    pair.Key, out GameplayOutcomeNarrativeEvidenceSource after)
                && after.InfluenceUseCount == pair.Value.InfluenceUseCount + 1
                && after.InfluenceRevision == pair.Value.InfluenceRevision + 1),
            "Trait commit did not consume each selected exact evidence authority once.");
        float appliedWorkSpeed = projector.ProjectIncrementalMultiplier(
            owner, traitReplayInput.MechanicTargetId, EffectContext());
        Require(appliedWorkSpeed > beforeWorkSpeed
                && Mathf.Abs(appliedWorkSpeed
                    - beforeWorkSpeed * expectedWorkSpeedFactor) < 0.0002f,
            "The committed selected trait did not execute its frozen formula "
                + traitReplayInput.MechanicTargetId + " multiplier. before=" + beforeWorkSpeed
                + ", expectedFactor=" + expectedWorkSpeedFactor
                + ", applied=" + appliedWorkSpeed);
        mechanicWitness.consumerValueApplied = ConsumerValue();
        mechanicWitness.consumerBenefitApplied = mechanicWitness.consumerValueApplied > mechanicWitness.consumerValueBefore
            && Mathf.Abs(mechanicWitness.consumerValueApplied
                - mechanicWitness.consumerValueBefore * expectedWorkSpeedFactor) < 0.0002f;
        if (!combatTrait)
        {
            mechanicWitness.workRateApplied = mechanicWitness.consumerValueApplied;
            mechanicWitness.workRateBenefitApplied = mechanicWitness.consumerBenefitApplied;
        }
        Require(mechanicWitness.consumerBenefitApplied,
            "The exact selected trait did not increase its frozen production consumer value by the formula factor.");
        List<GameplayOutcomeSnapshot> appliedOutcomes = ReadAllOutcomes();
        List<GameplayOutcomeSnapshot> newTraitOutcomes = appliedOutcomes.Where(value =>
                !beforeOutcomeIds.Contains(value.runId + ":" + value.sequence)
                && value.outcomeTypeId
                    == EvolutionOutcomeIds.TraitInferenceCompleted.Value)
            .ToList();
        Require(newTraitOutcomes.Count == 1
                && appliedOutcomes.Count(value => value.outcomeTypeId
                    == EvolutionOutcomeIds.TraitInferenceCompleted.Value)
                    == beforeTraitOutcomeCount + 1,
            "Trait completion did not publish exactly one authoritative outcome.");

        int committedRevision = appliedState.revision;
        int committedInstanceCount = appliedState.instances.Count;
        int committedOutcomeCount = appliedOutcomes.Count;
        CharacterAcquiredTraitAggregateState responseCommittedState = appliedState;
        WriteNew("u4-response-committed-trait-state.json", JsonUtility.ToJson(responseCommittedState, true));
        HashSet<string> committedOutcomeKeys = appliedOutcomes.Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        HashSet<string> nonTargetCancellationCandidates = new(StringComparer.Ordinal);
        WriteNew("u4-trait-duplicate-before-world.json", saves.ToJson(saves.Capture()));
        for (int frame = 0; frame < 5; frame++)
        {
            traitRuntime.Tick();
            yield return null;
            // A queued request may not have reached RequestCaptured yet.
            // Read other published owners' actual pending keys, never infer
            // them from dispatch history or cancel the target to empty the queue.
            foreach (CharacterActor other in scope.Container.Resolve<ICharacterWorldQuery>().Characters
                         .Where(value => value != null && value != owner && value.Progression != null))
            foreach (CharacterAcquiredTraitPendingRequestState pending in other.Progression
                         .CaptureAcquiredTraitState().pendingRequests.Where(value => value != null))
            {
                Require(!string.IsNullOrWhiteSpace(pending.requestKey)
                        && pending.requestKey != traitReplayInput.RequestId,
                    "Non-target trait cancellation would affect the fixed target request.");
                nonTargetCancellationCandidates.Add(pending.requestKey);
                queue.CancelAcquiredTraitRequest(pending.requestKey);
            }
        }
        CharacterAcquiredTraitAggregateState duplicateCheck = owner.Progression
            .CaptureAcquiredTraitState();
        List<GameplayOutcomeSnapshot> duplicateOutcomes = ReadAllOutcomes();
        WriteNew("u4-trait-duplicate-after-world.json", saves.ToJson(saves.Capture()));
        WriteNew("u4-trait-duplicate-witness.json", JObject.FromObject(new
        {
            responseCount = traitReplayResponseCount,
            revisionBefore = committedRevision, revisionAfter = duplicateCheck.revision,
            instancesBefore = committedInstanceCount, instancesAfter = duplicateCheck.instances.Count,
            outcomesBefore = committedOutcomeCount, outcomesAfter = duplicateOutcomes.Count,
            newOutcomes = duplicateOutcomes.Where(value => !committedOutcomeKeys.Contains(OutcomeKey(value))).ToArray(),
            queued = queue.QueuedCount, running = queue.RunningCount,
            targetCaptureCount = report.dispatches.Count(value =>
                value.profileId == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id
                && value.correlationId == traitReplayInput.RequestId),
            nonTargetCancellationCandidates = nonTargetCancellationCandidates.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            cancelledNonTargetCorrelations = report.dispatches.Where(value =>
                    value.profileId == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id
                    && value.correlationId != traitReplayInput.RequestId)
                .Select(value => value.correlationId).Distinct(StringComparer.Ordinal).ToArray(),
            queueDiagnostics = queue.GetRequestDiagnosticsForDebug(),
            before = appliedState, after = duplicateCheck,
            gamePaused = gameSpeed.IsPaused, scale = timeScale.Scale
        }).ToString());
        Require(traitReplayResponseCount == 1
                && report.dispatches.Count(value =>
                    value.profileId == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id
                    && value.correlationId == traitReplayInput.RequestId) == 1
                && duplicateCheck.revision == committedRevision
                && duplicateCheck.instances.Count == committedInstanceCount
                && JsonUtility.ToJson(duplicateCheck) == JsonUtility.ToJson(responseCommittedState)
                && duplicateOutcomes.Count == committedOutcomeCount
                && queue.QueuedCount == 0 && queue.RunningCount == 0,
            "Post-commit ticks duplicated the trait request, instance or ledger result.");
        if (combatTrait)
        {
            yield return RunEquipmentAttackWitness("applied", value => mechanicWitness.attackApplied = value,
                traitReplayInput.OwnerId, controlledWeaponId);
            mechanicWitness.actualConsumerExecuted = TraitAttackConsumedEffect(mechanicWitness.attackApplied);
        }
        else
        {
            yield return RunTraitWorkWitness("applied", workTarget, value => mechanicWitness.workApplied = value);
            mechanicWitness.actualWorkExecuted = mechanicWitness.workApplied.completed
                && mechanicWitness.workApplied.actualProgressObserved
                && mechanicWitness.workApplied.exactOutcome && mechanicWitness.workApplied.noDuplicateOutcomes;
            mechanicWitness.actualConsumerExecuted = mechanicWitness.actualWorkExecuted;
        }
        Require(mechanicWitness.actualConsumerExecuted,
            "Selected trait did not execute its actual consumer with an exact unique outcome.");
        appliedState = owner.Progression.CaptureAcquiredTraitState();
        string appliedWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-applied-world.json", appliedWorld);
        WriteNew("u4-applied-trait-state.json",
            JsonUtility.ToJson(appliedState, true));

        CharacterActor previousOwner = owner;
        Require(saves.TryRestore(saves.FromJson(appliedWorld),
                out DungeonGameRestoreReport restored),
            "Official trait replay result restore failed: "
                + string.Join(" | ", restored.Errors));
        Require(scope.Container.Resolve<ICharacterWorldSaveService>()
                .TryGetRestoredActor(traitReplayInput.OwnerId, out owner)
                && !ReferenceEquals(previousOwner, owner),
            "Official trait replay restore did not publish a fresh owner.");
        CharacterAcquiredTraitAggregateState restoredState = owner.Progression
            .CaptureAcquiredTraitState();
        float restoredWorkSpeed = projector.ProjectIncrementalMultiplier(
            owner, traitReplayInput.MechanicTargetId, EffectContext());
        Dictionary<string, GameplayOutcomeNarrativeEvidenceSource> restoredEvidence =
            CurrentTraitEvidence(traitReplayInput.SelectedEvidenceBindings
                .Select(value => value.publicFactId));
        Require(string.Equals(JsonUtility.ToJson(appliedState),
                    JsonUtility.ToJson(restoredState), StringComparison.Ordinal)
                && Mathf.Abs(restoredWorkSpeed - appliedWorkSpeed) < 0.0001f
                && afterEvidence.All(pair => restoredEvidence.TryGetValue(
                        pair.Key, out GameplayOutcomeNarrativeEvidenceSource restoredSource)
                    && restoredSource.InfluenceUseCount
                        == pair.Value.InfluenceUseCount
                    && restoredSource.InfluenceRevision
                        == pair.Value.InfluenceRevision)
                && ReadAllOutcomes().Count(value => value.outcomeTypeId
                    == EvolutionOutcomeIds.TraitInferenceCompleted.Value)
                    == beforeTraitOutcomeCount + 1,
            "Committed trait authority, evidence use, outcome or mechanic changed across official restore.");
        string restoredWorld = saves.ToJson(saves.Capture());
        WriteNew("u4-restored-world.json", restoredWorld);
        WriteNew("u4-restored-trait-state.json",
            JsonUtility.ToJson(restoredState, true));
        if (combatTrait)
        {
            yield return RunEquipmentAttackWitness("restored", value => mechanicWitness.attackRestored = value,
                traitReplayInput.OwnerId, controlledWeaponId);
            mechanicWitness.actualConsumerRoundTrip = TraitAttackConsumedEffect(mechanicWitness.attackRestored)
                && mechanicWitness.attackRestored.targetId == mechanicWitness.attackApplied.targetId;
        }
        else
        {
            yield return RunTraitWorkWitness("restored", PrepareTraitWorkFixture(), value => mechanicWitness.workRestored = value);
            mechanicWitness.actualWorkRoundTrip = mechanicWitness.workRestored.completed
                && mechanicWitness.workRestored.actualProgressObserved
                && mechanicWitness.workRestored.exactOutcome && mechanicWitness.workRestored.noDuplicateOutcomes;
            mechanicWitness.actualConsumerRoundTrip = mechanicWitness.actualWorkRoundTrip;
        }
        Require(mechanicWitness.actualConsumerRoundTrip,
            "Officially restored trait did not execute its actual consumer with an exact unique outcome.");
        WriteNew("u4-trait-mechanic-witness.json", JsonUtility.ToJson(mechanicWitness, true));
        WriteNew(combatTrait ? "u4-restored-attack-world.json" : "u4-restored-work-world.json", saves.ToJson(saves.Capture()));
        WriteTraitReplayProof(
            beforeState,
            responseCommittedState,
            appliedState,
            restoredState,
            beforeEvidence,
            afterEvidence,
            beforeWorld,
            appliedWorld,
            restoredWorld,
            beforeWorkSpeed,
            appliedWorkSpeed,
            restoredWorkSpeed,
            expectedWorkSpeedFactor,
            beforeTraitOutcomeCount,
            duplicateCheck.revision == committedRevision,
            mechanicWitness);
        report.responseApplied = true;
        report.networkSent = false;
    }

    private string PrepareTraitCombatFixture()
    {
        // Test-only authored equipment/target preparation, not a crafted item,
        // generated model result, injected attack, damage or ledger outcome.
        foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>().Characters
                     .Where(value => value != null && !value.IsDead))
        {
            actor.Brain?.StopCurrentActionForReplan("qa:trait-combat-fixture");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
            actor.SetAiPaused(true);
        }
        combatEquipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        CombatEquipmentInstance weapon = combatEquipment.CreateExternalInstance(
            "weapon:shortbow", CombatEquipmentQuality.Normal, string.Empty);
        Require(weapon != null && combatEquipment.TryAssignToCharacter(owner.Identity.PersistentId,
                weapon.instanceId, out _) && combatEquipment.TrySetActiveWeapon(owner.Identity.PersistentId,
                weapon.instanceId, out _), "Trait combat authored shortbow fixture could not be assigned/activated.");
        WriteNew("u4-trait-test-preparation.json", JObject.FromObject(new
        {
            testOnly = true, trainingEligible = false, naturalGameplay = false,
            weaponDefinitionId = "weapon:shortbow", weaponInstanceId = weapon.instanceId,
            preparation = "authored external bow; bounded arrows; living target AI paused/near-range/vitals 10000",
            responseChanged = false, attackOrDamageInjected = false, ledgerInjected = false
        }).ToString());
        return weapon.instanceId;
    }

    private bool TraitAttackConsumedEffect(EquipmentAttackWitness witness) => witness != null
        && witness.publicCommandIssued && witness.actualDamageApplied && witness.exactEquipmentBound
        && witness.noDuplicateOutcomes && witness.rawDamageConsumerCount > 0
        && witness.calculatedRawDamage > 0f
        && Mathf.Abs(witness.consumedCombatPower - owner.GetCombatPowerMultiplier()) < 0.0002f
        // The migrated attack receipt has its own result identity. Its atomic
        // sibling root identifies the primary damage operation observed above.
        && witness.outcomes.Count(value => value.rootOperationId == witness.rawDamageOperationId
            && value.causationRelationId == "atomic-sibling") == 1
        && witness.damageOutcomes.Count == 1
        && witness.damageOutcomes[0].operationId == witness.rawDamageOperationId
        && witness.damageOutcomes[0].status == GameplayOutcomeStatus.Succeeded
        && Math.Abs(witness.damageOutcomes[0].metrics.Single(value =>
                value.metricId == "health.damage").value
            - (witness.targetHealthBefore - witness.targetHealthAfter)) < 0.002;

    private WorldFilthWorkTarget PrepareTraitWorkFixture()
    {
        // A controlled filth/environment fixture, not fabricated work units,
        // proficiency, a completion event or a successful handler result.
        foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>()
                     .Characters.Where(value => value != null && !value.IsDead))
        {
            actor.Brain?.StopCurrentActionForReplan("qa:trait-work-fixture");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
            actor.SetAiPaused(!ReferenceEquals(actor, owner));
        }
        StabilizeTraitWorkFixture();
        IWorldFilthQuery filth = scope.Container.Resolve<IWorldFilthQuery>();
        Vector2Int cell = owner.GetNowXY();
        // Six real work units are sufficient for multiple approved ticks at
        // the observed sub-one-WU/s rate; this is not a throughput benchmark.
        filth.AddFilth(WorldFilthType.Stain, cell, 0.5f, owner.Identity.PersistentId, 0f);
        WorldFilthWorkTarget target = scope.Container.Resolve<IBuildingWorldQuery>()
            .Buildings.OfType<WorldFilthWorkTarget>()
            .SingleOrDefault(value => value != null && !value.isDestroy && value.centerPos == cell);
        Require(target != null && target.RequiredCleaningWork >= 5f,
            "Actual filth runtime did not publish its real cleaning target.");
        target.SetPriorityCleaning(true);
        return target;
    }

    private void StabilizeTraitWorkFixture(bool restoreHunger = true)
    {
        IEnvironmentalFieldPersistence fields = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        DungeonEnvironmentalFieldSaveData neutral = fields.Capture();
        neutral.cells.Clear();
        for (int y = 0; y < neutral.height; y++)
        for (int x = 0; x < neutral.width; x++)
            neutral.cells.Add(new EnvironmentalCellSaveData
            {
                x = x, y = y, temperatureC = 22f, airQuality = 100f, lightLevel = 100f
            });
        StabilizeConstructionActors(new[] { owner },
            scope.Container.Resolve<ICharacterDeprivationCommand>(), fields, neutral,
            scope.Container.Resolve<ICharacterEnvironmentPersistence>(),
            scope.Container.Resolve<ICharacterEnvironmentWorkContext>(), restoreHunger);
    }

    private IEnumerator RunTraitWorkWitness(
        string label, WorldFilthWorkTarget target, Action<TraitWorkWitness> completedWitness)
    {
        phase = "trait-selected-effect-work-" + label;
        Require(CharacterWorkRoleUtility.TryGetWork(owner, out AbilityWork worker),
            "Trait owner has no real work command recipient.");
        IWorldFilthQuery filth = scope.Container.Resolve<IWorldFilthQuery>();
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        TraitWorkWitness witness = new()
        {
            actorId = owner.Identity.PersistentId,
            targetId = target.PersistentInstanceId.Value,
            cell = target.centerPos,
            filthId = filth.GetAt(target.centerPos)
                .Single(value => value.Type == WorldFilthType.Stain
                    && value.SourceCharacterId == owner.Identity.PersistentId).FilthId,
            requiredWork = target.RequiredCleaningWork
        };
        completedWitness(witness);
        HashSet<string> beforeKeys = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        long beforeRevision = worker.ApprovedWorkProgressRevisionForDiagnostics;
        float previousScale = timeScale.Scale;
        int previousGameSpeed = gameSpeed.Speed;
        bool previousGamePaused = gameSpeed.IsPaused;
        float previousMaximumDelta = Time.maximumDeltaTime;
        float startedReal = Time.realtimeSinceStartup;
        float startedGame = clock.Time;
        float nextStabilizeGame = startedGame + 5f;
        List<GameplayOutcomeSnapshot> CompletionRows() => ReadAllOutcomes()
            .Where(value => !beforeKeys.Contains(OutcomeKey(value))
                && value.outcomeTypeId == WorkCompletionIdentityOutcomeIds.Applied.Value
                && value.facts.Any(fact => fact.factId == WorkCompletionIdentityOutcomeIds.WorkIdFact.Value
                    && fact.value == BuiltInWorkTypeIds.Clean.Value)
                && value.facts.Any(fact => fact.factId == WorkCompletionIdentityOutcomeIds.OperationIdFact.Value
                    && fact.value == $"work-run:{witness.actorId}:{witness.workRunId}"))
            .ToList();
        try
        {
            worker.SetWorkPriority(BuiltInWorkTypeIds.Clean, WorkPriorityLevel.Priority1);
            Require(worker.TrySetPriorityWorkTarget(target, BuiltInWorkTypeIds.Clean, null, out string failure),
                "Actual trait cleaning command rejected: " + failure);
            witness.publicCommandIssued = true;
            owner.Brain.ClearPathSearchCache();
            CharacterPerformanceExecutionTrace.Clear();
            Time.maximumDeltaTime = 0.05f;
            timeScale.Scale = 1f;
            gameSpeed.SetSpeed(5);
            gameSpeed.SetPaused(false);
            long lastRevision = beforeRevision;
            float lastApprovedGame = clock.Time;
            while (Time.realtimeSinceStartup - startedReal < 240f && clock.Time - startedGame < 180f)
            {
                bool hadProgress = worker.HasActiveGenericProgressForDiagnostics;
                float progressBefore = worker.GenericCompletedWorkForDiagnostics;
                long revisionBefore = worker.ApprovedWorkProgressRevisionForDiagnostics;
                yield return null;
                witness.observedFrames++;
                witness.brainAction = owner.Brain.CurrentActionDebugLabel;
                witness.brainDetail = owner.Brain.CurrentActionPhaseDetail;
                if (worker.HasActiveGenericProgressForDiagnostics && worker.assignedShop == target)
                {
                    int runId = worker.ActiveWorkRunIdForDiagnostics;
                    if (witness.workRunId == 0) witness.workRunId = runId;
                    Require(runId > 0 && runId == witness.workRunId
                            && worker.AssignedWorkTypeId == BuiltInWorkTypeIds.Clean,
                        "Actual cleaning progress switched work run or work type.");
                    long revisionAfter = worker.ApprovedWorkProgressRevisionForDiagnostics;
                    float progressAfter = worker.GenericCompletedWorkForDiagnostics;
                    if (hadProgress && revisionAfter > revisionBefore && witness.progress.Count < 12)
                    {
                        float delta = progressAfter - progressBefore;
                        TraitWorkProgressWitness sample = new()
                        {
                            before = progressBefore, after = progressAfter,
                            actualDelta = delta, gameDelta = clock.DeltaTime, revision = revisionAfter,
                            observedWorkPerGameSecond = clock.DeltaTime > 0f ? delta / clock.DeltaTime : 0f,
                            singleApprovedTickObserved = revisionAfter == revisionBefore + 1 && delta > 0f
                        };
                        Require(sample.singleApprovedTickObserved && sample.gameDelta > 0f,
                            "Cleaning approved-work tick did not advance actual generic progress.");
                        witness.progress.Add(sample);
                    }
                }
                long revision = worker.ApprovedWorkProgressRevisionForDiagnostics;
                if (revision > lastRevision) { lastRevision = revision; lastApprovedGame = clock.Time; }
                witness.outcomes = CompletionRows();
                if (witness.outcomes.Count > 0 && filth.GetAt(witness.cell).Count == 0)
                    break;
                Require(clock.Time - lastApprovedGame < 20f || witness.observedFrames < 120,
                    "Trait actual work made no approved progress: " + witness.brainAction
                        + "/" + witness.brainDetail + "/" + worker.LastPreWuExitDetailForDiagnostics);
                if (clock.Time >= nextStabilizeGame)
                {
                    StabilizeTraitWorkFixture();
                    witness.stabilizationPasses++;
                    nextStabilizeGame = clock.Time + 5f;
                }
            }
            timeScale.Scale = 0f;
            witness.approvedTicks = worker.ApprovedWorkProgressRevisionForDiagnostics - beforeRevision;
            CharacterPerformanceExecutionTraceEntry consumer = CharacterPerformanceExecutionTrace.Snapshot()
                .SingleOrDefault(value => value.ConsumerId == "WorkAmountCalculator.CalculateWorkPerSecond"
                    && value.Detail == BuiltInWorkTypeIds.Clean.Value);
            witness.actualConsumerTraceCount = consumer?.Count ?? 0;
            witness.performanceFormulaId = consumer?.FormulaId;
            witness.performanceValue = consumer?.OutputValue ?? 0f;
            witness.actualProgressObserved = witness.approvedTicks >= 2 && witness.progress.Count >= 2
                && witness.actualConsumerTraceCount >= 2;
            witness.outcomes = CompletionRows();
            witness.exactOutcome = witness.outcomes.Count == 1
                && witness.outcomes[0].facts.Any(fact => fact.factId == WorkCompletionIdentityOutcomeIds.ReactionFact.Value
                    && fact.value == "completed-work")
                && witness.outcomes[0].facts.Any(fact => fact.factId == WorkCompletionIdentityOutcomeIds.OriginFact.Value
                    && fact.value == "direct-player-order");
            witness.completed = filth.GetAt(witness.cell).Count == 0 && witness.exactOutcome;
            Require(witness.completed && witness.actualProgressObserved,
                "Actual trait cleaning did not complete with sampled progress and one exact completion identity.");
            for (int frame = 0; frame < 5; frame++) yield return null;
            witness.noDuplicateOutcomes = CompletionRows().Count == 1;
            Require(witness.noDuplicateOutcomes, "Actual trait cleaning duplicated its completion outcome.");
            worker.ClearPriorityWorkTarget();
        }
        finally
        {
            witness.elapsedGameSeconds = clock.Time - startedGame;
            witness.elapsedRealSeconds = Time.realtimeSinceStartup - startedReal;
            witness.approvedTicks = worker.ApprovedWorkProgressRevisionForDiagnostics - beforeRevision;
            // The outer campaign bound can dispose this iterator before its
            // terminal assertions. Preserve actual partial observations too;
            // unassigned defaults must not imply a missing production consumer.
            CharacterPerformanceExecutionTraceEntry partialConsumer = CharacterPerformanceExecutionTrace.Snapshot()
                .SingleOrDefault(value => value.ConsumerId == "WorkAmountCalculator.CalculateWorkPerSecond"
                    && value.Detail == BuiltInWorkTypeIds.Clean.Value);
            witness.actualConsumerTraceCount = partialConsumer?.Count ?? 0;
            witness.performanceFormulaId = partialConsumer?.FormulaId;
            witness.performanceValue = partialConsumer?.OutputValue ?? 0f;
            witness.actualProgressObserved = witness.approvedTicks >= 2 && witness.progress.Count >= 2
                && witness.actualConsumerTraceCount >= 2;
            witness.completedWork = witness.completed ? witness.requiredWork
                : worker.GenericCompletedWorkForDiagnostics;
            if (!witness.completed)
            {
                witness.failure = "Actual work failed; inspect progress/brain/filth/outcome witness and preserved world.";
                timeScale.Scale = 0f;
                WriteNew("u4-trait-work-" + label + "-failed-world.json", saves.ToJson(saves.Capture()));
            }
            Time.maximumDeltaTime = previousMaximumDelta;
            gameSpeed.SetSpeed(previousGameSpeed);
            gameSpeed.SetPaused(previousGamePaused);
            timeScale.Scale = previousScale;
            WriteNew("u4-trait-work-" + label + ".json", JsonUtility.ToJson(witness, true));
        }
    }

    private Dictionary<string, GameplayOutcomeNarrativeEvidenceSource>
        CurrentTraitEvidence(IEnumerable<string> factIds)
    {
        HashSet<string> required = (factIds ?? Array.Empty<string>())
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, GameplayOutcomeNarrativeEvidenceSource> result =
            outcomeEvidence.GetForCharacter(
                    traitReplayInput.OwnerId,
                    256,
                    0f,
                    includeCompacted: true)
                .Where(value => required.Contains(value.SourceId))
                .ToDictionary(value => value.SourceId, StringComparer.Ordinal);
        Require(result.Count == required.Count,
            "Current trait evidence authority is missing a selected fact.");
        return result;
    }

    private void WriteTraitReplayProof(
        CharacterAcquiredTraitAggregateState beforeState,
        CharacterAcquiredTraitAggregateState responseCommittedState,
        CharacterAcquiredTraitAggregateState appliedState,
        CharacterAcquiredTraitAggregateState restoredState,
        IReadOnlyDictionary<string, GameplayOutcomeNarrativeEvidenceSource>
            beforeEvidence,
        IReadOnlyDictionary<string, GameplayOutcomeNarrativeEvidenceSource>
            afterEvidence,
        string beforeWorld,
        string appliedWorld,
        string restoredWorld,
        float beforeWorkSpeed,
        float appliedWorkSpeed,
        float restoredWorkSpeed,
        float expectedWorkSpeedFactor,
        int beforeTraitOutcomeCount,
        bool duplicateStable,
        TraitMechanicWitness mechanicWitness)
    {
        JObject Semantic(
            CharacterAcquiredTraitAggregateState state,
            float workSpeed,
            int traitOutcomeCount) => new()
        {
            ["traitState"] = JObject.Parse(JsonUtility.ToJson(state)),
            ["effectTargetId"] = traitReplayInput.MechanicTargetId,
            ["effectMultiplier"] = workSpeed,
            ["traitOutcomeCount"] = traitOutcomeCount
        };
        JObject Artifact(
            string path,
            string world,
            CharacterAcquiredTraitAggregateState state,
            float workSpeed,
            int traitOutcomeCount) =>
            GameplayModuleSelectionReplayProofV2Contract.Artifact(
                path,
                Encoding.UTF8.GetBytes(world),
                Semantic(state, workSpeed, traitOutcomeCount));
        JObject beforeArtifact = Artifact(
            "u4-before-world.json",
            beforeWorld,
            beforeState,
            beforeWorkSpeed,
            beforeTraitOutcomeCount);
        JObject appliedArtifact = Artifact(
            "u4-applied-world.json",
            appliedWorld,
            appliedState,
            appliedWorkSpeed,
            beforeTraitOutcomeCount + 1);
        JObject restoredArtifact = Artifact(
            "u4-restored-world.json",
            restoredWorld,
            restoredState,
            restoredWorkSpeed,
            beforeTraitOutcomeCount + 1);
        JObject Assertion(string id, bool passed, JToken expected, JToken actual) =>
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                id, passed, expected, actual);
        string EvidenceAnchor(GameplayOutcomeNarrativeEvidenceSource source) =>
            GameplayModuleSelectionCanonicalJson.Hash(new JObject
            {
                ["sourceId"] = source.SourceId,
                ["exactOutcomeId"] = source.ExactOutcomeId.ToString(),
                ["subjectId"] = source.SubjectId.Value,
                ["anchorRevision"] = source.AnchorRevision,
                ["influenceUseCount"] = source.InfluenceUseCount,
                ["influenceRevision"] = source.InfluenceRevision
            });
        JObject[] influenceDeltas = beforeEvidence.OrderBy(value => value.Key,
                StringComparer.Ordinal)
            .Select(pair =>
            {
                GameplayOutcomeNarrativeEvidenceSource after =
                    afterEvidence[pair.Key];
                return GameplayModuleSelectionReplayProofV2Contract
                    .InfluenceDelta(
                        "exact",
                        pair.Key,
                        pair.Value.InfluenceUseCount,
                        after.InfluenceUseCount,
                        pair.Value.InfluenceRevision,
                        after.InfluenceRevision,
                        EvidenceAnchor(pair.Value),
                        EvidenceAnchor(after));
            })
            .ToArray();
        JObject request = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                traitReplayInput.ProfileId,
                traitReplayInput.SourceManifestSha256,
                traitReplayInput.ResponseManifestSha256,
                traitReplayInput.RequestId,
                traitReplayInput.ResponseSha256,
                traitReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.RequestAcceptedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "The exact source-v9 trait request accepted its unchanged, provenance-validated response through the production callback; replay itself sends no network request.",
                beforeArtifact, appliedArtifact, restoredArtifact,
                traitReplayInput.SelectedModuleIds,
                traitReplayInput.FormulaIds,
                traitReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                new[]
                {
                    Assertion("response-requested-exactly-once",
                        traitReplayResponseCount == 1, 1,
                        traitReplayResponseCount),
                    Assertion("wire-byte-exact",
                        traitReplayCapture != null, true,
                        traitReplayCapture != null)
                },
                Array.Empty<string>(), Array.Empty<string>());
        JObject candidate = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                traitReplayInput.ProfileId,
                traitReplayInput.SourceManifestSha256,
                traitReplayInput.ResponseManifestSha256,
                traitReplayInput.RequestId,
                traitReplayInput.ResponseSha256,
                traitReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.CandidateStoredStage,
                GameplayModuleSelectionReplayProofV2Contract.NotApplicableStatus,
                "Acquired-trait module selection commits one response directly and has no separate candidate store.",
                beforeArtifact, null, null,
                traitReplayInput.SelectedModuleIds,
                traitReplayInput.FormulaIds,
                traitReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(), Array.Empty<JObject>(),
                Array.Empty<string>(), Array.Empty<string>());
        JObject committed = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                traitReplayInput.ProfileId,
                traitReplayInput.SourceManifestSha256,
                traitReplayInput.ResponseManifestSha256,
                traitReplayInput.RequestId,
                traitReplayInput.ResponseSha256,
                traitReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.SelectionCommittedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "The production callback committed one exact acquired-trait identity, consumed selected evidence once, published one outcome and survived official restore.",
                beforeArtifact, appliedArtifact, restoredArtifact,
                traitReplayInput.SelectedModuleIds,
                traitReplayInput.FormulaIds,
                traitReplayInput.SelectedEvidenceFactIds,
                influenceDeltas,
                new[]
                {
                    Assertion("trait-revision-once",
                        responseCommittedState.revision == beforeState.revision + 1,
                        beforeState.revision + 1, responseCommittedState.revision),
                    Assertion("trait-pending-consumed",
                        appliedState.pendingRequests.All(value => value == null
                            || value.requestId
                                != traitReplayInput.TraitRequestId),
                        0,
                        appliedState.pendingRequests.Count(value => value != null
                            && value.requestId
                                == traitReplayInput.TraitRequestId)),
                    Assertion("trait-instance-added-once",
                        responseCommittedState.instances.Count
                            == beforeState.instances.Count + 1,
                        beforeState.instances.Count + 1,
                        responseCommittedState.instances.Count),
                    Assertion("target-trait-instance-stable-after-real-work",
                        appliedState.instances.Count(value => value.originatingRequestId == traitReplayInput.TraitRequestId) == 1
                            && restoredState.instances.Count(value => value.originatingRequestId == traitReplayInput.TraitRequestId) == 1,
                        1,
                        appliedState.instances.Count(value => value.originatingRequestId == traitReplayInput.TraitRequestId)),
                    Assertion("trait-outcome-added-once", true,
                        beforeTraitOutcomeCount + 1,
                        beforeTraitOutcomeCount + 1),
                    Assertion("duplicate-post-commit-ticks-stable",
                        duplicateStable, true, duplicateStable),
                    Assertion("official-trait-state-round-trip",
                        JsonUtility.ToJson(appliedState)
                            == JsonUtility.ToJson(restoredState),
                        GameplayModuleSelectionCanonicalJson.Hash(
                            JObject.Parse(JsonUtility.ToJson(appliedState))),
                        GameplayModuleSelectionCanonicalJson.Hash(
                            JObject.Parse(JsonUtility.ToJson(restoredState))))
                },
                Array.Empty<string>(), Array.Empty<string>());
        JObject mechanic = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                traitReplayInput.ProfileId,
                traitReplayInput.SourceManifestSha256,
                traitReplayInput.ResponseManifestSha256,
                traitReplayInput.RequestId,
                traitReplayInput.ResponseSha256,
                traitReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.MechanicExecutedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "The selected trait increased its frozen production consumer value by the exact formula factor; public commands executed that actual consumer and unique outcomes before and after official restore.",
                beforeArtifact, appliedArtifact, restoredArtifact,
                traitReplayInput.SelectedModuleIds,
                traitReplayInput.FormulaIds,
                traitReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                new[]
                {
                    Assertion("selected-effect-increased",
                        appliedWorkSpeed > beforeWorkSpeed,
                        "applied > before",
                        new JObject
                        {
                            ["before"] = beforeWorkSpeed,
                            ["applied"] = appliedWorkSpeed
                        }),
                    Assertion("selected-effect-multiplier-exact",
                        Mathf.Abs(appliedWorkSpeed
                            - beforeWorkSpeed * expectedWorkSpeedFactor)
                            < 0.0002f,
                        beforeWorkSpeed * expectedWorkSpeedFactor,
                        appliedWorkSpeed),
                    Assertion("selected-frozen-formula-factor-beneficial",
                        expectedWorkSpeedFactor > 1f,
                        "factor > 1",
                        expectedWorkSpeedFactor),
                    Assertion("official-mechanic-round-trip",
                        Mathf.Abs(restoredWorkSpeed
                            - appliedWorkSpeed) < 0.0001f,
                        appliedWorkSpeed,
                        restoredWorkSpeed),
                    Assertion("actual-consumer-rate-benefit",
                        mechanicWitness.consumerBenefitApplied, "applied == before * frozen factor",
                        JObject.FromObject(new
                        {
                            before = mechanicWitness.consumerValueBefore,
                            applied = mechanicWitness.consumerValueApplied
                        })),
                    Assertion("actual-selected-consumer-executed",
                        mechanicWitness.actualConsumerExecuted, true,
                        mechanicWitness.actualConsumerExecuted),
                    Assertion("actual-selected-consumer-executed-after-official-restore",
                        mechanicWitness.actualConsumerRoundTrip, true,
                        mechanicWitness.actualConsumerRoundTrip)
                },
                effectCoverageRefs: new[]
                {
                    "CharacterAcquiredTraitEffectSourceProjection.Project",
                    "CharacterDerivedStatsSnapshotProjector.ProjectIncrementalMultiplier:" + traitReplayInput.MechanicTargetId,
                    mechanicWitness.consumerPath,
                    traitReplayInput.MechanicTargetId == GameplayEffectTargetIds.CombatPower
                        ? "u4-trait-attack-applied.json" : "u4-trait-work-applied.json",
                    traitReplayInput.MechanicTargetId == GameplayEffectTargetIds.CombatPower
                        ? "u4-trait-attack-restored.json" : "u4-trait-work-restored.json"
                },
                failures: Array.Empty<string>());
        JArray proof = GameplayModuleSelectionReplayProofV2Contract
            .ValidateProofSet(new[] { request, candidate, committed, mechanic });
        byte[] proofBytes = GameplayModuleSelectionCanonicalJson.ToUtf8(proof);
        WriteNew("u4-replay-proof-v2.json", Encoding.UTF8.GetString(proofBytes));
        JObject manifest = new()
        {
            ["schema"] = "dungeonstory-gameplay-module-selection-replay-proof@2",
            ["profileId"] = traitReplayInput.ProfileId,
            ["requestId"] = traitReplayInput.RequestId,
            ["sourceManifestSha256"] = traitReplayInput.SourceManifestSha256,
            ["responseManifestSha256"] = traitReplayInput.ResponseManifestSha256,
            ["proof"] = new JObject
            {
                ["path"] = "u4-replay-proof-v2.json",
                ["bytes"] = proofBytes.Length,
                ["sha256"] = GameplayModuleSelectionCanonicalJson
                    .Sha256Prefixed(proofBytes)
            },
            ["stageCount"] = proof.Count,
            ["mechanicTargetId"] = traitReplayInput.MechanicTargetId,
            ["mechanicWitnesses"] = new JArray(new[]
            {
                traitReplayInput.MechanicTargetId == GameplayEffectTargetIds.CombatPower
                    ? "u4-trait-attack-applied.json" : "u4-trait-work-applied.json",
                traitReplayInput.MechanicTargetId == GameplayEffectTargetIds.CombatPower
                    ? "u4-trait-attack-restored.json" : "u4-trait-work-restored.json",
                "u4-trait-mechanic-witness.json",
                traitReplayInput.MechanicTargetId == GameplayEffectTargetIds.CombatPower
                    ? "u4-restored-attack-world.json" : "u4-restored-work-world.json",
                "u4-response-committed-trait-state.json", "u4-trait-duplicate-witness.json",
                "u4-trait-duplicate-before-world.json", "u4-trait-duplicate-after-world.json"
            }.Select(path =>
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(output, path));
                return new JObject
                {
                    ["path"] = path, ["bytes"] = bytes.Length,
                    ["sha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes)
                };
            })),
            ["networkSent"] = false,
            ["responseApplied"] = true,
            ["integrationFixture"] = traitReplayInput.IntegrationFixture,
            ["authoringOrigin"] = traitReplayInput.AuthoringOrigin,
            ["controlledGameplayFixture"] = true,
            ["mechanicExecuted"] = true,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["dpoPerformed"] = false,
            ["releaseReady"] = false
        };
        byte[] manifestBytes = GameplayModuleSelectionCanonicalJson.ToUtf8(manifest);
        WriteNew("u4-replay-proof-v2-manifest.json",
            Encoding.UTF8.GetString(manifestBytes));
        report.u4ProofWritten = true;
        report.u4ProofManifestSha256 = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(manifestBytes);
    }

    private void WriteFacilityReplayProof(
        FacilityEvolutionStateSnapshot beforeState,
        FacilityEvolutionStateSnapshot appliedState,
        FacilityEvolutionStateSnapshot restoredState,
        IReadOnlyDictionary<string, GameplayOutcomeNarrativeEvidenceSource>
            beforeEvidence,
        IReadOnlyDictionary<string, GameplayOutcomeNarrativeEvidenceSource>
            afterEvidence,
        string beforeWorld,
        string appliedWorld,
        string restoredWorld,
        FacilityMechanicWitness mechanicWitness,
        string duplicateFailure)
    {
        JObject Semantic(FacilityEvolutionStateSnapshot state, float multiplier) => new()
        {
            ["facilityId"] = facilityReplayInput.FacilityId,
            ["facilityEvolution"] = JObject.Parse(JsonUtility.ToJson(state)),
            ["serviceSupportMultiplier"] = multiplier
        };
        JObject beforeSemantic = Semantic(beforeState,
            mechanicWitness.multiplierBefore);
        JObject appliedSemantic = Semantic(appliedState,
            mechanicWitness.multiplierApplied);
        JObject restoredSemantic = Semantic(restoredState,
            mechanicWitness.multiplierRestored);
        JObject beforeArtifact = GameplayModuleSelectionReplayProofV2Contract
            .Artifact("u4-before-world.json", Encoding.UTF8.GetBytes(beforeWorld),
                beforeSemantic);
        JObject appliedArtifact = GameplayModuleSelectionReplayProofV2Contract
            .Artifact("u4-applied-world.json", Encoding.UTF8.GetBytes(appliedWorld),
                appliedSemantic);
        JObject restoredArtifact = GameplayModuleSelectionReplayProofV2Contract
            .Artifact("u4-restored-world.json", Encoding.UTF8.GetBytes(restoredWorld),
                restoredSemantic);
        JObject Assertion(string id, bool passed, JToken expected, JToken actual) =>
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                id, passed, expected, actual);
        string EvidenceAnchor(GameplayOutcomeNarrativeEvidenceSource source) =>
            GameplayModuleSelectionCanonicalJson.Hash(new JObject
            {
                ["sourceId"] = source.SourceId,
                ["exactOutcomeId"] = source.ExactOutcomeId.ToString(),
                ["subjectId"] = source.SubjectId.Value,
                ["anchorRevision"] = source.AnchorRevision,
                ["influenceUseCount"] = source.InfluenceUseCount,
                ["influenceRevision"] = source.InfluenceRevision
            });
        JObject[] influenceDeltas = beforeEvidence
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                GameplayOutcomeNarrativeEvidenceSource after =
                    afterEvidence[pair.Key];
                return GameplayModuleSelectionReplayProofV2Contract
                    .InfluenceDelta(
                        "exact",
                        pair.Key,
                        pair.Value.InfluenceUseCount,
                        after.InfluenceUseCount,
                        pair.Value.InfluenceRevision,
                        after.InfluenceRevision,
                        EvidenceAnchor(pair.Value),
                        EvidenceAnchor(after));
            })
            .ToArray();
        string[] formulaIds = { facilityReplayInput.FormulaId };
        string actualWireSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(facilityReplayCapture.CopyWireBodyBytes());
        JObject request = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                facilityReplayInput.ProfileId,
                facilityReplayInput.SourceManifestSha256,
                facilityReplayInput.ResponseManifestSha256,
                facilityReplayInput.RequestId,
                facilityReplayInput.ResponseSha256,
                facilityReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.RequestAcceptedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "The exact source-v9 facility request accepted one explicit integration response without network access.",
                beforeArtifact,
                appliedArtifact,
                restoredArtifact,
                facilityReplayInput.SelectedModuleIds,
                formulaIds,
                facilityReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                new[]
                {
                    Assertion("source-wire-byte-exact",
                        string.Equals(actualWireSha,
                            facilityReplayInput.SourceWireSha256,
                            StringComparison.Ordinal),
                        facilityReplayInput.SourceWireSha256,
                        actualWireSha),
                    Assertion("explicit-response-consumed-once",
                        facilityReplayResponseCount == 1,
                        1,
                        facilityReplayResponseCount)
                },
                Array.Empty<string>(),
                Array.Empty<string>());
        JObject candidate = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                facilityReplayInput.ProfileId,
                facilityReplayInput.SourceManifestSha256,
                facilityReplayInput.ResponseManifestSha256,
                facilityReplayInput.RequestId,
                facilityReplayInput.ResponseSha256,
                facilityReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.CandidateStoredStage,
                GameplayModuleSelectionReplayProofV2Contract.NotApplicableStatus,
                "Facility module selection commits one response directly and has no separate candidate store.",
                beforeArtifact,
                null,
                null,
                facilityReplayInput.SelectedModuleIds,
                formulaIds,
                facilityReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                Array.Empty<JObject>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        EvolutionNode appliedNode = appliedState.instanceEvolution.evolutionNodes
            .Single(value => value != null
                && string.Equals(value.nodeId,
                    facilityReplayInput.FormulaId, StringComparison.Ordinal));
        JObject committed = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                facilityReplayInput.ProfileId,
                facilityReplayInput.SourceManifestSha256,
                facilityReplayInput.ResponseManifestSha256,
                facilityReplayInput.RequestId,
                facilityReplayInput.ResponseSha256,
                facilityReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.SelectionCommittedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "The production callback committed one facility formula, consumed exact evidence once, rejected a duplicate and survived official restore.",
                beforeArtifact,
                appliedArtifact,
                restoredArtifact,
                facilityReplayInput.SelectedModuleIds,
                formulaIds,
                facilityReplayInput.SelectedEvidenceFactIds,
                influenceDeltas,
                new[]
                {
                    Assertion("facility-formula-added-once",
                        appliedState.instanceEvolution.evolutionNodes.Count
                            == beforeState.instanceEvolution.evolutionNodes.Count + 1,
                        beforeState.instanceEvolution.evolutionNodes.Count + 1,
                        appliedState.instanceEvolution.evolutionNodes.Count),
                    Assertion("facility-pending-consumed",
                        appliedState.pendingFormulaPresentation == null,
                        JValue.CreateNull(),
                        appliedState.pendingFormulaPresentation == null
                            ? JValue.CreateNull()
                            : JToken.FromObject(
                                appliedState.pendingFormulaPresentation)),
                    Assertion("selected-positive-module-active",
                        appliedNode.active
                            && string.Equals(appliedNode.effectId,
                                facilityReplayInput.SelectedModuleIds.Single(),
                                StringComparison.Ordinal)
                            && string.IsNullOrEmpty(appliedNode.burdenEffectId),
                        facilityReplayInput.SelectedModuleIds.Single(),
                        appliedNode.effectId),
                    Assertion("exact-evidence-consumed-once",
                        influenceDeltas.All(value =>
                            (int)value["useCountDelta"] == 1
                            && (int)value["revisionDelta"] == 1),
                        "all deltas equal one",
                        new JArray(influenceDeltas.Select(value => new JObject
                        {
                            ["evidenceFactId"] = (string)value["evidenceFactId"],
                            ["useCountDelta"] = (int)value["useCountDelta"],
                            ["revisionDelta"] = (int)value["revisionDelta"]
                        }))),
                    Assertion("duplicate-commit-rejected",
                        !string.IsNullOrWhiteSpace(duplicateFailure),
                        "rejected-with-reason",
                        string.IsNullOrWhiteSpace(duplicateFailure)
                            ? "accepted-or-silent"
                            : "rejected-with-reason"),
                    Assertion("official-facility-state-round-trip",
                        JToken.DeepEquals(appliedSemantic, restoredSemantic),
                        GameplayModuleSelectionCanonicalJson.Hash(appliedSemantic),
                        GameplayModuleSelectionCanonicalJson.Hash(restoredSemantic))
                },
                Array.Empty<string>(),
                Array.Empty<string>());
        JObject mechanic = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                facilityReplayInput.ProfileId,
                facilityReplayInput.SourceManifestSha256,
                facilityReplayInput.ResponseManifestSha256,
                facilityReplayInput.RequestId,
                facilityReplayInput.ResponseSha256,
                facilityReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.MechanicExecutedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "Actual service contracts consumed the selected support, completed after their game duration, rejected duplicate completion, and retained the shorter duration after official restore.",
                beforeArtifact,
                appliedArtifact,
                restoredArtifact,
                facilityReplayInput.SelectedModuleIds,
                formulaIds,
                facilityReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                new[]
                {
                    Assertion("current-facility-bound",
                        mechanicWitness.currentFacilityBound,
                        facilityReplayInput.FacilityId,
                        mechanicWitness.facilityId),
                    Assertion("service-support-benefit-applied",
                        mechanicWitness.supportBenefitApplied,
                        "applied > before",
                        new JObject
                        {
                            ["before"] = mechanicWitness.multiplierBefore,
                            ["applied"] = mechanicWitness.multiplierApplied
                        }),
                    Assertion("official-mechanic-projection-round-trip",
                        mechanicWitness.officialProjectionRoundTrip,
                        mechanicWitness.multiplierApplied,
                        mechanicWitness.multiplierRestored),
                    Assertion("actual-service-duration-benefit",
                        mechanicWitness.actualServiceBenefitApplied,
                        "completed service duration < baseline contract",
                        new JObject
                        {
                            ["before"] = mechanicWitness.serviceBefore.serviceSeconds,
                            ["applied"] = mechanicWitness.serviceApplied.serviceSeconds
                        }),
                    Assertion("actual-service-duration-official-round-trip",
                        mechanicWitness.actualServiceRoundTrip,
                        mechanicWitness.serviceApplied.serviceSeconds,
                        mechanicWitness.serviceRestored.serviceSeconds),
                    Assertion("actual-service-terminal-identity-and-duplicate",
                        mechanicWitness.serviceApplied.exactOutcome
                            && mechanicWitness.serviceRestored.exactOutcome
                            && mechanicWitness.serviceApplied.duplicateRejected
                            && mechanicWitness.serviceRestored.duplicateRejected,
                        "one terminal per session; duplicate rejected",
                        "one terminal per session; duplicate rejected")
                },
                effectCoverageRefs: new[] { mechanicWitness.consumerPath },
                failures: Array.Empty<string>());
        JArray proof = GameplayModuleSelectionReplayProofV2Contract
            .ValidateProofSet(new[] { request, candidate, committed, mechanic });
        byte[] proofBytes = GameplayModuleSelectionCanonicalJson.ToUtf8(proof);
        WriteNew("u4-replay-proof-v2.json", Encoding.UTF8.GetString(proofBytes));
        JObject manifest = new()
        {
            ["schema"] = "dungeonstory-gameplay-module-selection-replay-proof@2",
            ["profileId"] = facilityReplayInput.ProfileId,
            ["requestId"] = facilityReplayInput.RequestId,
            ["sourceManifestSha256"] = facilityReplayInput.SourceManifestSha256,
            ["responseManifestSha256"] = facilityReplayInput.ResponseManifestSha256,
            ["proof"] = new JObject
            {
                ["path"] = "u4-replay-proof-v2.json",
                ["bytes"] = proofBytes.Length,
                ["sha256"] = GameplayModuleSelectionCanonicalJson
                    .Sha256Prefixed(proofBytes)
            },
            ["stageCount"] = proof.Count,
            ["serviceWitnesses"] = new JArray(new[]
            {
                "u4-service-before.json", "u4-service-applied.json", "u4-service-restored.json",
                "u4-facility-mechanic-witness.json", "u4-restored-service-world.json"
            }.Select(path =>
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(output, path));
                return new JObject
                {
                    ["path"] = path, ["bytes"] = bytes.Length,
                    ["sha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes)
                };
            })),
            ["networkSent"] = false,
            ["responseApplied"] = true,
            ["integrationFixture"] = true,
            ["mechanicExecuted"] = true,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["dpoPerformed"] = false,
            ["releaseReady"] = false
        };
        byte[] manifestBytes = GameplayModuleSelectionCanonicalJson.ToUtf8(manifest);
        WriteNew("u4-replay-proof-v2-manifest.json",
            Encoding.UTF8.GetString(manifestBytes));
        report.u4ProofWritten = true;
        report.u4ProofManifestSha256 = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(manifestBytes);
    }

    private void WriteEquipmentReplayProof(
        EquipmentEvolutionState beforeState,
        EquipmentEvolutionState appliedState,
        EquipmentEvolutionState restoredState,
        IReadOnlyDictionary<string, int> beforeEvidenceUse,
        IReadOnlyDictionary<string, int> afterEvidenceUse,
        string beforeWorld,
        string appliedWorld,
        string restoredWorld,
        EquipmentMechanicWitness mechanicWitness,
        string duplicateFailure)
    {
        JObject BeforeSemantic() => new()
        {
            ["equipmentInstanceId"] = equipmentReplayInput.EquipmentInstanceId,
            ["evolution"] = JObject.Parse(JsonUtility.ToJson(beforeState)),
            ["mechanicProjection"] = new JObject
            {
                ["accuracyMultiplier"] = mechanicWitness.accuracyBefore,
                ["damageMultiplier"] = mechanicWitness.damageBefore
            }
        };
        JObject AppliedSemantic() => new()
        {
            ["equipmentInstanceId"] = equipmentReplayInput.EquipmentInstanceId,
            ["evolution"] = JObject.Parse(JsonUtility.ToJson(appliedState)),
            ["mechanicProjection"] = new JObject
            {
                ["accuracyMultiplier"] = mechanicWitness.accuracyApplied,
                ["damageMultiplier"] = mechanicWitness.damageApplied
            }
        };
        JObject RestoredSemantic() => new()
        {
            ["equipmentInstanceId"] = equipmentReplayInput.EquipmentInstanceId,
            ["evolution"] = JObject.Parse(JsonUtility.ToJson(restoredState)),
            ["mechanicProjection"] = new JObject
            {
                ["accuracyMultiplier"] = mechanicWitness.accuracyRestored,
                ["damageMultiplier"] = mechanicWitness.damageRestored
            }
        };

        byte[] beforeBytes = Encoding.UTF8.GetBytes(beforeWorld);
        byte[] appliedBytes = Encoding.UTF8.GetBytes(appliedWorld);
        byte[] restoredBytes = Encoding.UTF8.GetBytes(restoredWorld);
        JObject beforeArtifact = GameplayModuleSelectionReplayProofV2Contract
            .Artifact("u4-before-world.json", beforeBytes, BeforeSemantic());
        JObject appliedArtifact = GameplayModuleSelectionReplayProofV2Contract
            .Artifact("u4-applied-world.json", appliedBytes, AppliedSemantic());
        JObject restoredArtifact = GameplayModuleSelectionReplayProofV2Contract
            .Artifact("u4-restored-world.json", restoredBytes, RestoredSemantic());

        string actualWireSha = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(equipmentReplayCapture.CopyWireBodyBytes());
        JObject[] requestAssertions =
        {
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "source-wire-byte-exact",
                string.Equals(
                    equipmentReplayInput.SourceWireSha256,
                    actualWireSha,
                    StringComparison.Ordinal),
                equipmentReplayInput.SourceWireSha256,
                actualWireSha),
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "explicit-response-consumed-once",
                equipmentReplayResponseCount == 1,
                1,
                equipmentReplayResponseCount)
        };
        JObject expectedEvidenceUse = new(beforeEvidenceUse
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new JProperty(value.Key, value.Value + 1)));
        JObject actualEvidenceUse = new(afterEvidenceUse
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new JProperty(value.Key, value.Value)));
        string appliedSemanticSha = GameplayModuleSelectionCanonicalJson.Hash(
            AppliedSemantic());
        string restoredSemanticSha = GameplayModuleSelectionCanonicalJson.Hash(
            RestoredSemantic());
        JObject[] commitAssertions =
        {
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "module-selection-revision-once",
                appliedState.moduleSelectionRevision
                    == beforeState.moduleSelectionRevision + 1L,
                beforeState.moduleSelectionRevision + 1L,
                appliedState.moduleSelectionRevision),
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "selected-legacy-evidence-consumed-once",
                JToken.DeepEquals(expectedEvidenceUse, actualEvidenceUse),
                expectedEvidenceUse,
                actualEvidenceUse),
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "duplicate-commit-rejected",
                !string.IsNullOrWhiteSpace(duplicateFailure),
                "rejected-with-reason",
                string.IsNullOrWhiteSpace(duplicateFailure)
                    ? "accepted-or-silent"
                    : "rejected-with-reason"),
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "official-equipment-state-round-trip",
                string.Equals(
                    appliedSemanticSha,
                    restoredSemanticSha,
                    StringComparison.Ordinal),
                appliedSemanticSha,
                restoredSemanticSha)
        };
        string[] formulaIds = { equipmentReplayInput.FormulaId };
        JObject request = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                equipmentReplayInput.ProfileId,
                equipmentReplayInput.SourceManifestSha256,
                equipmentReplayInput.ResponseManifestSha256,
                equipmentReplayInput.RequestId,
                equipmentReplayInput.ResponseSha256,
                equipmentReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.RequestAcceptedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "The restored source-v9 request accepted one explicit integration response without network access.",
                beforeArtifact,
                appliedArtifact,
                restoredArtifact,
                equipmentReplayInput.SelectedModuleIds,
                formulaIds,
                equipmentReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                requestAssertions,
                Array.Empty<string>(),
                Array.Empty<string>());
        JObject candidate = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                equipmentReplayInput.ProfileId,
                equipmentReplayInput.SourceManifestSha256,
                equipmentReplayInput.ResponseManifestSha256,
                equipmentReplayInput.RequestId,
                equipmentReplayInput.ResponseSha256,
                equipmentReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.CandidateStoredStage,
                GameplayModuleSelectionReplayProofV2Contract.NotApplicableStatus,
                "Equipment module selection is a single response commit and has no separate candidate-store authority.",
                beforeArtifact,
                null,
                null,
                equipmentReplayInput.SelectedModuleIds,
                formulaIds,
                equipmentReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                Array.Empty<JObject>(),
                Array.Empty<string>(),
                Array.Empty<string>());
        JObject committed = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                equipmentReplayInput.ProfileId,
                equipmentReplayInput.SourceManifestSha256,
                equipmentReplayInput.ResponseManifestSha256,
                equipmentReplayInput.RequestId,
                equipmentReplayInput.ResponseSha256,
                equipmentReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.SelectionCommittedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "The production callback committed the selected equipment module exactly once and survived official restore.",
                beforeArtifact,
                appliedArtifact,
                restoredArtifact,
                equipmentReplayInput.SelectedModuleIds,
                formulaIds,
                equipmentReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                commitAssertions,
                Array.Empty<string>(),
                Array.Empty<string>());
        JObject[] mechanicAssertions =
        {
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "active-production-weapon-bound",
                mechanicWitness.activeWeaponBound,
                equipmentReplayInput.EquipmentInstanceId,
                mechanicWitness.equipmentInstanceId),
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "precision-accuracy-benefit-applied",
                mechanicWitness.accuracyBenefitApplied,
                "applied > before",
                new JObject
                {
                    ["before"] = mechanicWitness.accuracyBefore,
                    ["applied"] = mechanicWitness.accuracyApplied
                }),
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "precision-damage-burden-applied",
                mechanicWitness.damageBurdenApplied,
                "applied < before",
                new JObject
                {
                    ["before"] = mechanicWitness.damageBefore,
                    ["applied"] = mechanicWitness.damageApplied
                }),
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "official-mechanic-projection-round-trip",
                mechanicWitness.officialProjectionRoundTrip,
                new JObject
                {
                    ["accuracy"] = mechanicWitness.accuracyApplied,
                    ["damage"] = mechanicWitness.damageApplied
                },
                new JObject
                {
                    ["accuracy"] = mechanicWitness.accuracyRestored,
                    ["damage"] = mechanicWitness.damageRestored
                }),
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "actual-selected-weapon-attack-executed",
                mechanicWitness.actualAttackExecuted,
                "exact selected weapon, target damage, one arrow per unique attack",
                JObject.Parse(JsonUtility.ToJson(mechanicWitness.attackApplied))),
            GameplayModuleSelectionReplayProofV2Contract.StateAssertion(
                "actual-selected-weapon-attack-after-official-restore",
                mechanicWitness.actualAttackRoundTrip,
                "same selected weapon effect, target damage, one arrow per unique attack",
                JObject.Parse(JsonUtility.ToJson(mechanicWitness.attackRestored)))
        };
        JObject mechanic = GameplayModuleSelectionReplayProofV2Contract
            .CreateStageProof(
                equipmentReplayInput.ProfileId,
                equipmentReplayInput.SourceManifestSha256,
                equipmentReplayInput.ResponseManifestSha256,
                equipmentReplayInput.RequestId,
                equipmentReplayInput.ResponseSha256,
                equipmentReplayInput.ReplayCodeIdentity,
                GameplayModuleSelectionReplayProofV2Contract.MechanicExecutedStage,
                GameplayModuleSelectionReplayProofV2Contract.PassedStatus,
                "Public production attacks consumed the selected precision benefit and damage burden, changed the real target and ammunition, and executed again after official restore.",
                beforeArtifact,
                appliedArtifact,
                restoredArtifact,
                equipmentReplayInput.SelectedModuleIds,
                formulaIds,
                equipmentReplayInput.SelectedEvidenceFactIds,
                Array.Empty<JObject>(),
                mechanicAssertions,
                effectCoverageRefs: new[]
                {
                    "CombatEquipmentRuntime.TryGetActiveWeapon",
                    "CombatEquipmentLoadoutRuntime.TryGetActiveWeapon",
                    "CombatEquipmentStatProjector.GetEvolutionMultiplier:combat.accuracy",
                    "CombatEquipmentStatProjector.GetEvolutionMultiplier:combat.damage",
                    mechanicWitness.consumerPath
                },
                failures: Array.Empty<string>());
        JArray proof = GameplayModuleSelectionReplayProofV2Contract
            .ValidateProofSet(new[] { request, candidate, committed, mechanic });
        byte[] proofBytes = GameplayModuleSelectionCanonicalJson.ToUtf8(proof);
        WriteNew("u4-replay-proof-v2.json", Encoding.UTF8.GetString(proofBytes));
        JObject manifest = new()
        {
            ["schema"] = "dungeonstory-gameplay-module-selection-replay-proof@2",
            ["profileId"] = equipmentReplayInput.ProfileId,
            ["requestId"] = equipmentReplayInput.RequestId,
            ["sourceManifestSha256"] = equipmentReplayInput.SourceManifestSha256,
            ["responseManifestSha256"] = equipmentReplayInput.ResponseManifestSha256,
            ["proof"] = new JObject
            {
                ["path"] = "u4-replay-proof-v2.json",
                ["bytes"] = proofBytes.Length,
                ["sha256"] = GameplayModuleSelectionCanonicalJson
                    .Sha256Prefixed(proofBytes)
            },
            ["stageCount"] = proof.Count,
            ["attackWitnesses"] = new JArray(new[]
            {
                "u4-equipment-attack-applied.json", "u4-equipment-attack-restored.json",
                "u4-equipment-mechanic-witness.json", "u4-restored-attack-world.json"
            }.Select(path =>
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(output, path));
                return new JObject
                {
                    ["path"] = path, ["bytes"] = bytes.Length,
                    ["sha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes)
                };
            })),
            ["networkSent"] = false,
            ["responseApplied"] = true,
            ["integrationFixture"] = true,
            ["mechanicExecuted"] = true,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["dpoPerformed"] = false,
            ["releaseReady"] = false
        };
        byte[] manifestBytes = GameplayModuleSelectionCanonicalJson.ToUtf8(manifest);
        WriteNew("u4-replay-proof-v2-manifest.json",
            Encoding.UTF8.GetString(manifestBytes));
        report.u4ProofWritten = true;
        report.u4ProofManifestSha256 = GameplayModuleSelectionCanonicalJson
            .Sha256Prefixed(manifestBytes);
    }

    private IEnumerator RunEquipmentWitness()
    {
        phase = "equipment-actual-combat";
        equipmentPhase = true;
        replaying = false;
        CancelOutstandingTransport();
        combatEquipment = scope.Container.Resolve<ICombatEquipmentRuntime>();
        equipmentEvolution = scope.Container.Resolve<IEquipmentEvolutionRuntime>();
        combatCommands = scope.Container.Resolve<ICharacterCombatCommandRuntime>();
        equipmentPresentation = scope.Container
            .Resolve<IEvolutionHistoryNarrativeRuntime>()
            as EvolutionHistoryNarrativeRuntime;
        Require(combatEquipment != null
                && equipmentEvolution != null
                && combatCommands != null
                && equipmentPresentation != null,
            "Actual combat equipment, evolution, command and presentation runtimes are required.");

        string ownerId = owner.Identity.PersistentId;
        ICharacterWorldQuery characters = scope.Container.Resolve<ICharacterWorldQuery>();
        equipmentTarget = characters.Characters
            .Where(value => value != null
                && !value.IsDead
                && value.CurrentLifecycleState == CharacterLifecycleState.Active
                && value.Identity != null
                && !string.Equals(
                    value.Identity.PersistentId,
                    ownerId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(equipmentTarget != null,
            "Actual combat witness requires a second living published actor.");
        string targetId = equipmentTarget.Identity.PersistentId;
        Vector2Int targetCellBefore = equipmentTarget.GetNowXY();
        equipmentTarget.Brain?.StopCurrentActionForReplan(
            "qa:source-v9-equipment-controlled-target");
        equipmentTarget.GetComponent<AbilityMove>()?.CancelActiveMovement();
        equipmentTarget.SetAiPaused(true);
        Grid grid = scope.Container.Resolve<IGridSystemProvider>().Grid;
        Vector2Int actorCell = owner.GetNowXY();
        bool foundTargetCell = false;
        Vector2Int targetCell = default;
        foreach (int offset in new[] { 1, -1, 2, -2, 3, -3 })
        {
            Vector2Int candidate = new(actorCell.x + offset, actorCell.y);
            if (!grid.IsValidGridPos(candidate)
                || !grid.IsWalkable(candidate)
                || characters.Characters.Any(value => value != null
                    && value != owner
                    && value != equipmentTarget
                    && !value.IsDead
                    && value.GetNowXY() == candidate))
            {
                continue;
            }
            targetCell = candidate;
            foundTargetCell = true;
            break;
        }
        Require(foundTargetCell,
            "Actual combat witness requires a clear same-floor target cell.");
        equipmentTarget.transform.position = grid.GetWorldPos(targetCell);
        Physics2D.SyncTransforms();
        Require(equipmentTarget.GetNowXY() == targetCell,
            "Controlled combat target did not remain on its same-floor fixture cell.");
        scope.Container.Resolve<ICharacterBodyHealthCommand>()
            .ConfigureVitals(
                equipmentTarget,
                10000f,
                resetCurrentHealth: true);

        CombatEquipmentInstance equipmentInstance = combatEquipment.CreateExternalInstance(
            "weapon:shortbow",
            CombatEquipmentQuality.Normal,
            string.Empty);
        Require(equipmentInstance != null
                && !string.IsNullOrWhiteSpace(equipmentInstance.instanceId),
            "Controlled authored shortbow fixture was not created.");
        Require(combatEquipment.TryAssignToCharacter(
                ownerId,
                equipmentInstance.instanceId,
                out string assignFailure),
            "Actual owner could not receive the authored shortbow: "
                + assignFailure);
        Require(combatEquipment.TrySetActiveWeapon(
                ownerId,
                equipmentInstance.instanceId,
                out string activeFailure),
            "Actual owner could not activate the authored shortbow: "
                + activeFailure);

        var witness = new EquipmentCombatWitness
        {
            actorId = ownerId,
            targetId = targetId,
            equipmentInstanceId = equipmentInstance.instanceId,
            controlledTargetAiPaused = !equipmentTarget.CanRunAi,
            actorCell = actorCell,
            targetCellBefore = targetCellBefore,
            targetCell = targetCell
        };
        report.equipmentCombat = witness;
        EquipmentEvolutionState beforeState = equipmentEvolution.GetState(
            equipmentInstance.instanceId);
        witness.affinityBefore = ReadAffinity(beforeState, ownerId);
        Require(witness.affinityBefore == 0,
            "New controlled equipment already has attunement progress.");
        // Bind the plan after explicit test preparation but before issuing any
        // combat command. Preparation is not represented as earned experience.
        LiveNarrativeCausalPlan livePlan = LiveNarrativeCausalPlan.Requested
            ? LiveNarrativeCausalPlan.Begin(output, CaptureEquipmentLivePlanState(equipmentInstance.instanceId),
                LiveNarrativeCausalPlan.EquipmentActionId) : null;
        var beforeOutcomeKeys = ReadAllOutcomes()
            .Select(OutcomeKey)
            .ToHashSet(StringComparer.Ordinal);

        Require(combatEquipment.TryLoadExternalAmmunition(
                equipmentInstance.instanceId,
                "ammo:arrow",
                1),
            "Controlled authored shortbow could not receive compatible ammunition.");
        witness.externalAmmunitionLoads++;
        Require(combatCommands.SetCombatStance(
                owner,
                true,
                out string stanceMessage),
            "Actual owner could not enter combat stance: " + stanceMessage);
        Require(combatCommands.TrySetFireMode(
                owner,
                CombatFireMode.Rapid,
                out string fireModeMessage),
            "Actual owner could not select the authored rapid-fire mode: "
                + fireModeMessage);
        witness.publicCommandIssued = combatCommands.TryIssueAttack(
            owner,
            new CombatParticipantRef(equipmentTarget),
            forceFire: true,
            out string commandMessage);
        Require(witness.publicCommandIssued,
            "Actual force-fire command was rejected: " + commandMessage);
        WriteNew(
            "equipment-pre-dispatch-world.json",
            saves.ToJson(saves.Capture()));

        // The exact official continuation may intentionally be paused. Once
        // that restore has been checked, start this controlled combat slice
        // with the production speed/pause commands, not a private scale that
        // leaves the game clock paused.
        timeScale.Scale = originalScale;
        gameSpeed.SetSpeed(5);
        gameSpeed.SetPaused(false);
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        float observationStartedReal = Time.realtimeSinceStartup;
        float observationStartedGame = clock.Time;
        int observationStartedFrame = Time.frameCount;
        float deadline = observationStartedReal + 60f;
        float hardDeadline = observationStartedReal + 240f;
        // A restored large world can spend several wall seconds per frame.
        // Thirty public attacks require real simulation ticks, not only a
        // wall-clock budget. Still fail if no request appears within the hard
        // bound; neither attunement nor attacks are synthesized by this guard.
        bool HasObservationBudget() => Time.realtimeSinceStartup < hardDeadline
            && (Time.realtimeSinceStartup < deadline
                || clock.Time - observationStartedGame < 120f
                || Time.frameCount - observationStartedFrame < 60);
        bool preparedEquipmentTransport = false;
        bool startedEquipmentTransport = false;
        Action prepareEquipmentTransport = () =>
        {
            if (preparedEquipmentTransport) return;
            // Capture-only requests have no model response. A same-priority
            // trait retry can remain ahead of the newly accepted equipment
            // request even after its previously dispatched transport was
            // cancelled. Clear transient transport work exactly once at the
            // real affinity threshold; persistent producer facts remain owned
            // by their runtimes and are neither completed nor fabricated.
            witness.queuedBeforeThresholdReset = queue.QueuedCount;
            witness.runningBeforeThresholdReset = queue.RunningCount;
            witness.queueBeforeThresholdReset =
                queue.GetRequestDiagnosticsForDebug();
            queue.AbortAllForDebug();
            witness.transportQueueResetAtThreshold = true;
            witness.queuedAfterThresholdReset = queue.QueuedCount;
            witness.runningAfterThresholdReset = queue.RunningCount;
            witness.queueAfterThresholdReset =
                queue.GetRequestDiagnosticsForDebug();
            preparedEquipmentTransport = true;
        };
        Action startEquipmentTransport = () =>
        {
            if (startedEquipmentTransport) return;
            Require(combatCommands.SetCombatStance(
                    owner,
                    false,
                    out string releaseMessage),
                "Actual owner could not leave combat stance after reaching the attunement threshold: "
                    + releaseMessage);
            prepareEquipmentTransport();
            EquipmentEvolutionPresentationRequest pending =
                equipmentEvolution.GetState(equipmentInstance.instanceId)
                    .presentationRequests?
                    .SingleOrDefault(value => value != null
                        && value.state
                            == EquipmentEvolutionPresentationState
                                .ModuleSelectionPending);
            Require(pending != null,
                "Attunement threshold produced no module-selection pending request.");
            Require(equipmentPresentation
                    .TrySubmitNextFormulaPresentationForIsolatedCapture(),
                "Production equipment presentation did not submit the persisted pending request.");
            Require(queue.TryDispatchQueuedRequestForIsolatedCapture(
                    pending.presentationId),
                "Submitted equipment request was not uniquely queued for isolated capture: "
                    + queue.GetRequestDiagnosticsForDebug());
            startedEquipmentTransport = true;
        };
        while (equipmentDispatch == null
            && HasObservationBudget())
        {
            if (combatEquipment.TryGetActiveWeapon(
                    ownerId,
                    out CombatWeaponSnapshot weapon)
                && weapon != null
                && weapon.LoadedAmmo <= 0)
            {
                Require(combatEquipment.TryLoadExternalAmmunition(
                        equipmentInstance.instanceId,
                        "ammo:arrow",
                        1),
                    "Controlled ammunition reload failed before the real command reached attunement.");
                witness.externalAmmunitionLoads++;
            }
            EquipmentEvolutionState liveState = equipmentEvolution.GetState(
                equipmentInstance.instanceId);
            witness.affinityAfter = ReadAffinity(liveState, ownerId);
            if (witness.affinityAfter >= 30)
            {
                startEquipmentTransport();
            }
            yield return null;
        }
        EquipmentEvolutionState thresholdState = equipmentEvolution.GetState(
            equipmentInstance.instanceId);
        witness.affinityAfter = ReadAffinity(thresholdState, ownerId);
        if (equipmentDispatch == null && witness.affinityAfter >= 30)
        {
            startEquipmentTransport();
            float dispatchDeadline = Time.realtimeSinceStartup + 5f;
            while (equipmentDispatch == null
                && Time.realtimeSinceStartup < dispatchDeadline)
            {
                yield return null;
            }
        }
        timeScale.Scale = 0;

        witness.outcomes = ReadAllOutcomes()
            .Where(value => !beforeOutcomeKeys.Contains(OutcomeKey(value))
                && value.outcomeTypeId == "combat.attack-resolved")
            .ToList();
        witness.committedAttackOutcomes = witness.outcomes.Count;
        EquipmentEvolutionState capturedState = equipmentEvolution.GetState(
            equipmentInstance.instanceId);
        witness.affinityAfter = ReadAffinity(capturedState, ownerId);
        witness.evolutionNodeCount = capturedState.evolutionNodes?.Count ?? 0;
        witness.presentationRequestCount =
            capturedState.presentationRequests?.Count ?? 0;
        witness.lastNodeGenerationFailure =
            equipmentEvolution is EquipmentEvolutionRuntime concreteEvolution
                ? concreteEvolution
                    .LastAttunementNodeGenerationFailureForDiagnostics
                : string.Empty;
        witness.observedRealSeconds = Time.realtimeSinceStartup - observationStartedReal;
        witness.observedGameSeconds = clock.Time - observationStartedGame;
        witness.observedFrames = Time.frameCount - observationStartedFrame;
        WriteNew(
            "equipment-command-witness.json",
            JsonUtility.ToJson(witness, true));
        Require(equipmentDispatch != null,
            "Actual combat did not dispatch an equipment module request within the bounded run: "
                + $"affinity={witness.affinityAfter}; nodes={witness.evolutionNodeCount}; "
                + $"presentations={witness.presentationRequestCount}; "
                + $"nodeFailure={witness.lastNodeGenerationFailure}");
        Require(witness.affinityAfter >= 30
                && witness.committedAttackOutcomes >= 30,
            "Equipment request was not backed by thirty committed public attacks.");
        livePlan?.CompleteRequest(CaptureEquipmentLivePlanState(equipmentInstance.instanceId),
            LiveTargetRequestReceipt(equipmentDispatch.dispatchEligibility,
                equipmentDispatch.wireSha256, equipmentDispatch.checkpointSha256),
            "equipment-command-witness.json");

        Require(combatEquipment.TryGetInstance(
                equipmentInstance.instanceId,
                out CombatEquipmentInstance beforeRestore),
            "Captured equipment instance disappeared before restore.");
        CharacterActor previousOwner = owner;
        replaying = true;
        Require(saves.TryRestore(
                saves.FromJson(equipmentCheckpointJson),
                out DungeonGameRestoreReport restored),
            "Official equipment checkpoint restore failed: "
                + string.Join(" | ", restored.Errors));
        report.equipmentOfficialWorldRestore = true;
        Require(scope.Container.Resolve<ICharacterWorldSaveService>()
                .TryGetRestoredActor(ownerId, out owner)
                && !ReferenceEquals(previousOwner, owner),
            "Equipment checkpoint did not publish a fresh owner actor.");
        Require(combatEquipment.TryGetInstance(
                equipmentInstance.instanceId,
                out CombatEquipmentInstance afterRestore),
            "Equipment checkpoint omitted the captured instance.");
        report.equipmentStateExact = JsonUtility.ToJson(beforeRestore.evolution)
            == JsonUtility.ToJson(afterRestore.evolution);
        EquipmentEvolutionPresentationRequest restoredPending =
            afterRestore.evolution.presentationRequests.SingleOrDefault(value =>
                value != null
                && string.Equals(
                    value.presentationId,
                    equipmentDispatch.correlationId,
                    StringComparison.Ordinal));
        WriteNew(
            "equipment-restored-pending.json",
            JsonUtility.ToJson(restoredPending, true));
        report.equipmentPendingExact = restoredPending != null
            && JsonUtility.ToJson(restoredPending)
                == JsonUtility.ToJson(equipmentDispatch.pending);
        Require(report.equipmentStateExact && report.equipmentPendingExact,
            "Official restore changed equipment evolution state or pending request.");
        WriteNew(
            "equipment-restored-world.json",
            saves.ToJson(saves.Capture()));

        CancelOutstandingTransport();
        // As at the original threshold, schedule this exact response-less
        // transport explicitly. Unrelated retries are not model responses and
        // must not consume the replay observation window's shared slots.
        queue.ConfigureIsolatedAutomaticDispatch(false);
        WriteNew("equipment-replay-queue-before.json", new JObject
        {
            ["correlationId"] = equipmentDispatch.correlationId,
            ["diagnostics"] = queue.GetRequestDiagnosticsForDebug()
        }.ToString());
        bool replaySubmitted = false;
        bool replayDispatched = false;
        deadline = Time.realtimeSinceStartup + 30f;
        while (!report.equipmentWireReplayExact
            && Time.realtimeSinceStartup < deadline)
        {
            replaySubmitted |= equipmentPresentation.TrySubmitNextFormulaPresentationForIsolatedCapture();
            replayDispatched |= queue.TryDispatchQueuedRequestForIsolatedCapture(equipmentDispatch.correlationId);
            yield return null;
        }
        WriteNew("equipment-replay-queue-after.json", new JObject
        {
            ["correlationId"] = equipmentDispatch.correlationId,
            ["submissionObserved"] = replaySubmitted, ["dispatchObserved"] = replayDispatched,
            ["sameWireObserved"] = report.equipmentWireReplayExact,
            ["diagnostics"] = queue.GetRequestDiagnosticsForDebug()
        }.ToString());
        Require(report.equipmentWireReplayExact,
            "Restored equipment pending did not dispatch the identical wire request.");
    }

    private static int ReadAffinity(
        EquipmentEvolutionState state,
        string ownerId) => state?.attunements?
        .SingleOrDefault(value => value != null
            && string.Equals(
                value.ownerPersistentId,
                ownerId,
                StringComparison.Ordinal))?.affinityScore ?? 0;

    private IEnumerator RunFacilityWitness()
    {
        phase = "facility-prerequisites";
        facilityPhase = true;
        replaying = false;
        CancelOutstandingTransport();
        facilityPresentation = scope.Container.Resolve<FacilityFormulaEvolutionPresentationRuntime>();
        FacilityEvolutionRuntime evolution = scope.Container.Resolve<FacilityFeatureSceneRuntimeReferences>().Evolution;
        Require(evolution != null, "Authored world has no facility evolution owner.");
        IBuildingWorldQuery buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        var usedIds = report.commands.Select(value => value.facilityId).ToHashSet(StringComparer.Ordinal);
        FacilityEvolutionRecipeSO selectedRecipe = null;
        foreach (BuildableObject building in buildings.Buildings
                     .Where(value => value is Facility && !value.isDestroy && value.isActiveAndEnabled)
                     .OrderByDescending(value => usedIds.Contains(value.PersistentInstanceId.Value))
                     .ThenBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal))
        foreach (FacilityEvolutionCandidate candidate in evolution.GetCandidates(building, includeRejected: true, requestLlmProposal: false))
        {
            report.facilityCandidates.Add(new FacilityCandidateWitness
            {
                facilityId = building.PersistentInstanceId.Value, recipeId = candidate.Recipe.EffectiveId,
                approved = candidate.Approved, reason = candidate.Validation.ToMessage()
            });
            if (candidate.Approved && selectedRecipe == null)
            {
                facilityOwner = building;
                selectedRecipe = candidate.Recipe;
            }
        }
        report.facilityEligibilityStatus = selectedRecipe == null ? "not-eligible" : "eligible";
        WriteNew("facility-candidates.json", JsonUtility.ToJson(new FacilityCandidateReport
            { eligibility = report.facilityEligibilityStatus, candidates = report.facilityCandidates }, true));
        bool prepareNonTargetFixture = sourceV9FacilityIntake && Application.isBatchMode
            && DungeonPersistencePaths.IsIsolatedVerification;
        report.facilityTradeSkippedAsPreparation = prepareNonTargetFixture;
        if (!prepareNonTargetFixture && report.continuation != null && !report.continuation.sourceRouteDelivered)
            yield return RunFacilityMaterialTrade();
        if (selectedRecipe == null)
        {
            // Iron production needs an iron-built furnace in this authored
            // starting world. Use the real goods/rapport/paid-trade bootstrap.
            if (!prepareNonTargetFixture && report.facilityMaterialTrade == null
                && report.continuation?.sourceRouteDelivered != true)
                yield return RunFacilityMaterialTrade();
            var target = buildings.Buildings.Where(value => value != null && !value.isDestroy)
                .SelectMany(building => evolution.GetCandidates(building, includeRejected: true, requestLlmProposal: false)
                    .Where(candidate => candidate.Recipe.EffectiveId == "evolve_shop_display_to_secure_display")
                    .Select(candidate => (building, candidate.Recipe))).Single();
            facilityOwner = target.building;
            RoomProfile profile = scope.Container.Resolve<IRoomProfileProvider>().Build(facilityOwner);
            BuildingSO[] missing = target.Recipe.requiredUniqueFixtures
                .Where(definition => !profile.Fixtures.Any(value => value.BuildingData == definition)).ToArray();
            Require(missing.Length == 1 && missing[0].id == 1070,
                "Authored shop prerequisite changed; the controlled candle construction plan is no longer applicable.");
            if (prepareNonTargetFixture)
                PrepareIsolatedFacilityUniqueFixture(missing[0], profile.Room);
            else
                yield return ConstructFacilityFixture(missing[0], profile.Room);
            FacilityEvolutionCandidate after = evolution.GetCandidates(facilityOwner, includeRejected: true, requestLlmProposal: false)
                .Single(candidate => candidate.Recipe == target.Recipe);
            report.facilityEligibilityStatus = prepareNonTargetFixture
                ? after.Approved ? "eligible-after-explicit-preparation" : "not-eligible-after-explicit-preparation"
                : after.Approved ? "eligible-after-real-construction" : "not-eligible-after-real-construction";
            WriteNew("facility-candidate-after-construction.json", JsonUtility.ToJson(new FacilityCandidateWitness
            {
                facilityId = facilityOwner.PersistentInstanceId.Value, recipeId = after.Recipe.EffectiveId,
                approved = after.Approved, reason = after.Validation.ToMessage()
            }, true));
            if (after.Approved) selectedRecipe = after.Recipe;
        }
        Require(selectedRecipe != null,
            "No authored usable facility is eligible for evolution. Actual paid-trade prefix is preserved; construction and an eligible visitor are still required.");
        // Non-target furnishings may be explicit isolated preparation. Usage,
        // evolution commands, evidence and the effect under test are still real.
        string selectedFacilityId = facilityOwner.PersistentInstanceId.Value;
        LiveNarrativeCausalPlan livePlan = LiveNarrativeCausalPlan.Requested
            ? LiveNarrativeCausalPlan.Begin(output, CaptureFacilityLivePlanState(selectedFacilityId),
                LiveNarrativeCausalPlan.FacilityActionId) : null;
        if (!usedIds.Contains(selectedFacilityId))
        {
            timeScale.Scale = originalScale;
            if (facilityOwner.SupportsFacilityRole(CharacterVisitPolicy.GetInterestRoles(owner)))
            {
                report.facilityUseOrigin = "controlled-eligible-actor-command";
                yield return CompleteFacilityUse((Facility)facilityOwner, requireTrainingGain: false);
            }
            else yield return ObserveFacilityVisitor();
            CancelOutstandingTransport();
        }
        // Visitor admission and interaction may publish a fresh building object
        // for the same persistent facility. Submit the evolution command through
        // the current world-query owner so the pending request and capture owner
        // are the same live instance.
        facilityOwner = buildings.Buildings.SingleOrDefault(value => value != null
            && !value.isDestroy
            && value.PersistentInstanceId.Value == selectedFacilityId);
        Require(facilityOwner != null,
            "Selected facility disappeared before the evolution command.");
        WriteNew("facility-pre-command-world.json", saves.ToJson(saves.Capture()));
        Require(evolution.TryEvolve(facilityOwner, selectedRecipe, out FacilityEvolutionResult selected),
            "Actual facility player selection was rejected: " + selected.Message);
        Require(facilityOwner.GetComponent<FacilityEvolutionStateComponent>()?.PendingFormulaPresentation != null,
            "Actual player selection did not create a persisted facility request.");
        timeScale.Scale = originalScale;
        float deadline = Time.realtimeSinceStartup + 30f;
        string facilityRequestId = facilityOwner.GetComponent<FacilityEvolutionStateComponent>()
            .PendingFormulaPresentation.presentationId;
        string[] liveUsageCommands = report.commands.Select((command, index) => new { command, index })
            .Where(value => value.command.facilityId == selectedFacilityId)
            .Select(value => $"command-{value.index + 1:D3}.json").ToArray();
        if (livePlan != null)
            WriteNew("live-facility-evolution-command.json", new JObject
            {
                ["command"] = "facility.evolve", ["facilityId"] = selectedFacilityId,
                ["recipeId"] = selectedRecipe.EffectiveId, ["accepted"] = true,
                ["presentationId"] = facilityRequestId, ["usageCommandFiles"] = new JArray(liveUsageCommands),
                ["preCommandWorldSha256"] = LiveNarrativeCausalPlan.FileHash(Path.Combine(output, "facility-pre-command-world.json"))
            }.ToString());
        while (facilityDispatch == null && Time.realtimeSinceStartup < deadline)
        {
            facilityPresentation.Tick();
            queue.TryDispatchQueuedRequestForIsolatedCapture(facilityRequestId);
            yield return null;
        }
        Require(facilityDispatch != null, "Actual facility evolution did not dispatch its module request.");
        livePlan?.CompleteRequest(CaptureFacilityLivePlanState(selectedFacilityId),
            LiveTargetRequestReceipt(facilityDispatch.dispatchEligibility,
                facilityDispatch.wireSha256, facilityDispatch.checkpointSha256),
            liveUsageCommands.Concat(new[] { "live-facility-evolution-command.json" }).ToArray());
        timeScale.Scale = 0;
        BuildableObject oldFacility = facilityOwner;
        replaying = true;
        Require(saves.TryRestore(saves.FromJson(facilityCheckpointJson), out DungeonGameRestoreReport restored),
            "Official facility checkpoint restore failed: " + string.Join(" | ", restored.Errors));
        report.facilityOfficialWorldRestore = true;
        facilityOwner = buildings.Buildings.SingleOrDefault(value => value != null && !value.isDestroy
            && value.PersistentInstanceId.Value == facilityDispatch.facilityId);
        report.facilityFreshPublishedOwner = facilityOwner != null && !ReferenceEquals(oldFacility, facilityOwner);
        Require(report.facilityFreshPublishedOwner, "Facility restore did not publish a fresh owner for the captured ID.");
        var pending = facilityOwner.GetComponent<FacilityEvolutionStateComponent>()?.PendingFormulaPresentation;
        WriteNew("facility-restored-pending.json", JsonUtility.ToJson(pending, true));
        Require(pending != null && JsonUtility.ToJson(pending) == JsonUtility.ToJson(facilityDispatch.pending),
            "Official restore changed the facility's pending evidence, offers, or retry state.");
        report.facilityPendingExact = true;
        WriteNew("facility-restored-world.json", saves.ToJson(saves.Capture()));
        CancelOutstandingTransport();
        deadline = Time.realtimeSinceStartup + 30f;
        while (!report.facilityWireReplayExact && Time.realtimeSinceStartup < deadline)
        {
            facilityPresentation.Tick();
            queue.TryDispatchQueuedRequestForIsolatedCapture(facilityRequestId);
            yield return null;
        }
        Require(report.facilityWireReplayExact, "Restored facility did not dispatch the identical actual wire request.");
    }

    private void PrepareIsolatedFacilityUniqueFixture(BuildingSO definition, RoomInstance room)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && sourceV9FacilityIntake && !report.facilityNonTargetPreparationFixture
                && definition != null && definition.id == 1070 && room != null && room.IsUsable,
            "Facility-only preparation requires exactly one missing authored candle in the selected usable room.");
        Grid grid = scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller.GridSystem.grid;
        IBuildingWorldQuery buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        Require(!buildings.Buildings.Any(value => value != null && !value.isDestroy
                && value.BuildingData == definition && value.buildPoses.Any(room.ContainsCell)),
            "Existing room furnishing must be reused; no duplicate preparation is permitted.");
        Vector2Int? position = room.Cells.OrderBy(value => value.y).ThenBy(value => value.x)
            .Where(cell => definition.GetGridPosList(cell).All(footprint => room.ContainsCell(footprint)
                && grid.GetGridCell(footprint) is GridCell actual
                && actual.CanBuildInArea(definition) && actual.CanOccupy(definition.Placement.Layer)))
            .Select(cell => (Vector2Int?)cell).FirstOrDefault();
        Require(position.HasValue, "Prepared furnishing has no legal footprint in the selected room.");
        int moneyBefore = scope.Container.Resolve<IShopFeatureQueryService>().Capture().CurrentMoney;
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        string[] PhysicalStocks() => items.GetAllStacks().Where(value => value != null)
            .Select(value => new JObject { ["id"] = value.StackId, ["item"] = value.ItemId,
                ["quantity"] = value.Quantity, ["state"] = value.State.ToString(),
                ["destination"] = value.DestinationId }.ToString(Newtonsoft.Json.Formatting.None))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] stocksBefore = PhysicalStocks();
        string[] outcomesBefore = ReadAllOutcomes().Select(OutcomeKey).OrderBy(value => value).ToArray();
        BuildableObject furnishing = scope.Container.Resolve<IGridBuildingObjectFactory>()
            .Create(grid, definition, position.Value);
        Require(furnishing != null, "Production building factory rejected isolated furnishing preparation.");
        foreach (MonoBehaviour component in furnishing.GetComponentsInChildren<MonoBehaviour>(true))
            if (component != null) scope.Container.Inject(component);
        furnishing.SetGrid(grid);
        furnishing.Initialization(definition, position.Value);
        Require(grid.RegisterOccupant(furnishing, definition.Placement.Layer,
                definition.GetGridPosList(position.Value), definition.Placement.IsMovement)
                && buildings.Buildings.Contains(furnishing),
            "Prepared furnishing is absent from the actual grid/building authorities.");
        report.facilityNonTargetPreparationFixture = true;
        Require(scope.Container.Resolve<IShopFeatureQueryService>().Capture().CurrentMoney == moneyBefore
                && PhysicalStocks().SequenceEqual(stocksBefore)
                && ReadAllOutcomes().Select(OutcomeKey).OrderBy(value => value).SequenceEqual(outcomesBefore),
            "Non-target furnishing preparation changed money, physical resources or gameplay outcomes.");
        WriteNew("facility-explicit-nontarget-preparation.json", new JObject
        {
            ["authorization"] = "human: isolated non-production test preparation broadly permitted",
            ["fixtureKind"] = "one authored candle; not purchase, delivery, hauling or construction gameplay",
            ["buildingDefinitionId"] = definition.id,
            ["buildingInstanceId"] = furnishing.PersistentInstanceId.Value,
            ["x"] = position.Value.x, ["y"] = position.Value.y,
            ["moneyUnchanged"] = true, ["physicalResourcesUnchanged"] = true,
            ["gameplayOutcomesUnchanged"] = true, ["paidTradePerformed"] = false,
            ["deliveredRouteFabricated"] = false, ["workUnitsFabricated"] = false,
            ["facilityUsageFabricated"] = false, ["trainingEligible"] = false,
            ["naturalPlayClaimed"] = false, ["humanApprovalClaimed"] = false
        }.ToString());
    }

    private BuildableObject PrepareApprovedSingleSupportCraftingBench(JObject trace)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
                && skillOneCraftingBenchFixture && skillNormalWeaponPreparation
                && (sourceV9SkillReplay || SkillSupportContinuation)
                && !skillCraftingBenchFixtureCreated,
            "Exactly one explicitly approved support bench may be prepared per isolated run.");
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/S08_대장작업대.asset");
        Require(definition != null, "Approved authored crafting bench definition is missing.");
        var controller = scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller;
        Grid grid = controller.GridSystem.grid;
        IBuildingWorldQuery buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        Require(!buildings.Buildings.Any(value => value != null && !value.isDestroy
                && value.BuildingData == definition),
            "An existing bench must be reused; approved preparation cannot add a second bench.");
        Vector2Int? position = null;
        foreach (RoomInstance room in scope.Container.Resolve<IRoomLayoutCache>().GetLayout(grid).Rooms
            .Where(value => value != null && value.IsUsable && !value.IsSelfContained).OrderBy(value => value.Id))
        {
            position = room.Cells.OrderBy(value => value.y).ThenBy(value => value.x)
                .Where(cell => definition.GetGridPosList(cell).All(footprint => room.ContainsCell(footprint)
                    && grid.GetGridCell(footprint) is GridCell actual
                    && actual.CanBuildInArea(definition) && actual.CanOccupy(definition.Placement.Layer)))
                .Select(cell => (Vector2Int?)cell).FirstOrDefault();
            if (position.HasValue) break;
        }
        Require(position.HasValue, "Approved bench has no legal footprint in an existing usable room.");
        int moneyBefore = scope.Container.Resolve<IShopFeatureQueryService>().Capture().CurrentMoney;
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        string[] PhysicalStocks() => items.GetAllStacks().Where(value => value != null)
            .Select(value => new JObject { ["stackId"] = value.StackId, ["itemId"] = value.ItemId,
                ["quantity"] = value.Quantity, ["state"] = value.State.ToString(),
                ["destinationId"] = value.DestinationId }.ToString(Newtonsoft.Json.Formatting.None))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] stocksBefore = PhysicalStocks();
        string[] outcomesBefore = ReadAllOutcomes().Select(OutcomeKey).OrderBy(value => value).ToArray();
        BuildableObject bench = scope.Container.Resolve<IGridBuildingObjectFactory>()
            .Create(grid, definition, position.Value);
        Require(bench != null, "Actual building factory rejected approved bench preparation.");
        foreach (MonoBehaviour component in bench.GetComponentsInChildren<MonoBehaviour>(true))
            if (component != null) scope.Container.Inject(component);
        bench.SetGrid(grid);
        bench.Initialization(definition, position.Value);
        Require(grid.RegisterOccupant(bench, definition.Placement.Layer,
                definition.GetGridPosList(position.Value), definition.Placement.IsMovement)
                && buildings.Buildings.Contains(bench),
            "Approved bench is not registered in the actual grid and building authority.");
        skillCraftingBenchFixtureCreated = true;
        Require(buildings.Buildings.Count(value => value != null && !value.isDestroy
                && value.BuildingData == definition) == 1
                && scope.Container.Resolve<IShopFeatureQueryService>().Capture().CurrentMoney == moneyBefore
                && PhysicalStocks().SequenceEqual(stocksBefore)
                && ReadAllOutcomes().Select(OutcomeKey).OrderBy(value => value).SequenceEqual(outcomesBefore),
            "Approved bench preparation changed money, crafting material, outcomes or bench count.");
        var preparation = new JObject
        {
            ["authorization"] = "human: 제작대 1개는 테스트 준비물로 허용함",
            ["fixtureKind"] = "one authored crafting bench, not purchase or construction gameplay",
            ["count"] = 1, ["buildingDefinitionId"] = definition.id,
            ["buildingInstanceId"] = bench.PersistentInstanceId.Value,
            ["x"] = position.Value.x, ["y"] = position.Value.y,
            ["moneyBefore"] = moneyBefore, ["moneyAfter"] = moneyBefore,
            ["physicalStocksBefore"] = new JArray(stocksBefore.Select(JObject.Parse)),
            ["physicalStocksAfter"] = new JArray(PhysicalStocks().Select(JObject.Parse)),
            ["allPhysicalStocksUnchanged"] = true,
            ["gameplayOutcomeCreated"] = false, ["weaponOrHealthInjected"] = false,
            ["trainingEligible"] = false, ["naturalPlayClaimed"] = false,
            ["learningApprovalClaimed"] = false
        };
        trace["craftingBenchPreparation"] = preparation.DeepClone();
        WriteNew("skill-approved-one-crafting-bench-preparation.json", preparation.ToString());
        return bench;
    }

    private IEnumerator ConstructFacilityFixture(BuildingSO definition, RoomInstance room,
        string artifactPrefix = "facility-construction", bool useAuthoredWorkBudget = false, Action observe = null,
        Vector2Int? plannedPosition = null, bool approvedSupportNeedsOnly = false,
        IReadOnlyCollection<string> approvedSupportActorIds = null)
    {
        phase = "construction";
        Require(room != null && room.IsUsable, "Construction prerequisite requires its actual usable room.");
        var controller = scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller;
        IWorkOrderRuntime workOrders = scope.Container.Resolve<IWorkOrderRuntime>();
        IBuildingWorldQuery buildings = scope.Container.Resolve<IBuildingWorldQuery>();
        ICharacterDeprivationCommand deprivation =
            scope.Container.Resolve<ICharacterDeprivationCommand>();
        IEnvironmentalFieldPersistence environmentFields =
            scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        ICharacterEnvironmentPersistence characterEnvironment =
            scope.Container.Resolve<ICharacterEnvironmentPersistence>();
        ICharacterEnvironmentWorkContext environmentWorkContext =
            scope.Container.Resolve<ICharacterEnvironmentWorkContext>();
        DungeonEnvironmentalFieldSaveData safeConstructionEnvironment =
            JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(
                JsonUtility.ToJson(environmentFields.Capture()));
        safeConstructionEnvironment.cells.Clear();
        for (int y = 0; y < safeConstructionEnvironment.height; y++)
        {
            for (int x = 0; x < safeConstructionEnvironment.width; x++)
            {
                safeConstructionEnvironment.cells.Add(
                    new EnvironmentalCellSaveData
                    {
                        x = x,
                        y = y,
                        temperatureC = 22f,
                        airQuality = 100f,
                        lightLevel = 100f
                    });
            }
        }
        var beforeKeys = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        var activeOrders = workOrders.Capture().orders;
        var existingOrders = activeOrders.Select(value => value.workOrderId).ToHashSet(StringComparer.Ordinal);
        var witness = new ConstructionWitness
        {
            actorId = owner.Identity.PersistentId, buildingDefinitionId = definition.id,
            preparationInstantWorkFixture = nonTargetInstantWorkHold != null,
            naturalWorkPassClaimed = false
        };
        report.facilityConstruction = witness;
        WriteNew(artifactPrefix + "-before-world.json", saves.ToJson(saves.Capture()));
        ConstructionSite[] matchingSites = buildings.Buildings.OfType<ConstructionSite>()
            .Where(value => value != null && !value.isDestroy && value.TargetBuilding == definition
                && value.buildPoses.Any(room.ContainsCell)).ToArray();
        WorkOrderSaveData[] expectedOrders = sourceConstructionOrders.Where(value => value.targetBuildingId == definition.id
            && room.ContainsCell(new Vector2Int(value.gridX, value.gridY))).ToArray();
        Require(matchingSites.Length <= 1 && expectedOrders.Length <= 1,
            "Multiple matching construction owners are ambiguous; no replacement site is created.");
        ConstructionSite site;
        if (matchingSites.Length == 1)
        {
            site = matchingSites[0];
            witness.before = activeOrders.SingleOrDefault(value => value.workOrderId == site.WorkOrderId
                && value.workTypeId == BuiltInWorkTypeIds.Construct.Value && value.targetBuildingId == definition.id
                && value.gridX == site.GridPosition.x && value.gridY == site.GridPosition.y
                && value.constructionSitePersistentId == site.PersistentInstanceId.Value);
            Require(witness.before != null && expectedOrders.Length == 1
                && JsonUtility.ToJson(witness.before) == JsonUtility.ToJson(expectedOrders[0]),
                "Existing construction does not exactly match its incoming saved order.");
            witness.resumedExistingOrder = witness.sourceOrderExact = true;
            witness.x = site.GridPosition.x; witness.y = site.GridPosition.y;
        }
        else
        {
            Require(expectedOrders.Length == 0,
                "Saved construction owner is missing after restore; refusing to create a replacement.");
            bool placed = false;
            IEnumerable<Vector2Int> placementCells = plannedPosition.HasValue
                ? new[] { plannedPosition.Value }
                : room.Cells.OrderBy(value => value.y).ThenBy(value => value.x);
            foreach (Vector2Int cell in placementCells.Where(value => definition.GetGridPosList(value).All(room.ContainsCell)))
            {
                if (!controller.TryPlaceConstructionSite(definition, cell, out string failure))
                {
                    witness.failure = failure;
                    continue;
                }
                witness.x = cell.x; witness.y = cell.y;
                placed = true;
                break;
            }
            Require(placed, "Real room cannot accept the required construction site: " + witness.failure);
            witness.before = workOrders.Capture().orders.Single(value => !existingOrders.Contains(value.workOrderId));
            site = buildings.Buildings.OfType<ConstructionSite>()
                .Single(value => value.WorkOrderId == witness.before.workOrderId);
        }
        witness.orderId = witness.before.workOrderId;
        witness.siteId = site.PersistentInstanceId.Value;
        Require(workOrders.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out WorkOrderProgressState currentOrder)
            && currentOrder.WorkOrderId == witness.orderId && currentOrder.TargetBuildingId == definition.id
            && currentOrder.Position == new Vector2Int(witness.x, witness.y)
            && currentOrder.RequiredWork == witness.before.requiredWork && currentOrder.CompletedWork == witness.before.completedWork,
            "Live construction query is not bound to the captured site/order/progress.");
        WriteNew(artifactPrefix + (witness.resumedExistingOrder ? "-resumed-world.json" : "-placed-world.json"),
            saves.ToJson(saves.Capture()));
        Require(CharacterWorkRoleUtility.TryGetWork(owner, out AbilityWork worker), "Actual owner has no work command recipient.");
        CharacterActor[] constructionActors = scope.Container.Resolve<ICharacterWorldQuery>()
            .Characters.Where(value => value != null && !value.IsDead
                && (!approvedSupportNeedsOnly || value.Role == CharacterRole.Owner
                    || CharacterWorkRoleUtility.IsWorker(value))
                && (approvedSupportActorIds == null || approvedSupportActorIds.Contains(value.Identity.PersistentId)))
            .OrderBy(value => ReferenceEquals(value, owner) ? 0 : 1)
            .ThenBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToArray();
        Require(!approvedSupportNeedsOnly || constructionActors.Length == 3,
            "Approved support construction fixture requires the original three residents.");
        long environmentPrepareStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        // The immutable neutral input is identical throughout this construction
        // scope. Validate it once; Restore still checks grid dimensions, copies
        // into a new authority state and runs at exactly the former cadence.
        witness.neutralEnvironmentCandidateHasNoControls = safeConstructionEnvironment.thermostats.Count == 0
            && safeConstructionEnvironment.controlOutcomes.Count == 0;
        // Thermostat-bearing inputs still need the original per-restore live
        // owner/range validation. Reuse only fields with no dynamic controls.
        var safeConstructionCandidate = witness.neutralEnvironmentCandidateHasNoControls
            ? environmentFields.PrepareRestore(safeConstructionEnvironment) : null;
        witness.environmentCandidatePreparations = safeConstructionCandidate == null ? 0 : 1;
        witness.environmentPrepareMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - environmentPrepareStarted)
            * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        StabilizeConstructionActors(
            constructionActors,
            deprivation,
            environmentFields,
            safeConstructionEnvironment,
            characterEnvironment,
            environmentWorkContext, restoreHunger: !approvedSupportNeedsOnly,
            approvedSupportNeedsOnly: approvedSupportNeedsOnly, telemetry: witness,
            preparedEnvironment: safeConstructionCandidate);
        witness.actorStabilizationPasses++;
        witness.environmentStabilizationPasses++;
        witness.neutralEnvironmentApplied = true;
        float deadline = Time.realtimeSinceStartup + 120f;
        float materialsStarted = Time.realtimeSinceStartup;
        int materialsStartFrame = Time.frameCount;
        int materialObservation = 0;
        float nextMaterialObservation = Time.realtimeSinceStartup;
        float nextActorStabilization = Time.realtimeSinceStartup + 5f;
        phase = "construction-materials";
        timeScale.Scale = originalScale;
        gameSpeed.SetPaused(false);
        gameSpeed.SetSpeed(5);
        witness.publicSpeedMultiplier = gameSpeed.Speed;
        if (useAuthoredWorkBudget) Time.maximumDeltaTime = 0.1f;
        // A delivered caravan is not a delivered construction input. This public
        // request routes existing physical items and wakes the ordinary hauler;
        // only actual pickup/delivery can make the work order ready.
        while (Time.realtimeSinceStartup < deadline)
        {
            observe?.Invoke();
            if (Time.realtimeSinceStartup >= nextMaterialObservation)
            {
                CaptureHaulingWait(++materialObservation, materialsStarted, artifactPrefix);
                nextMaterialObservation = Time.realtimeSinceStartup + 20f;
            }
            if (Time.realtimeSinceStartup >= nextActorStabilization)
            {
                StabilizeConstructionActors(
                    constructionActors,
                    deprivation,
                    environmentFields,
                    safeConstructionEnvironment,
                    characterEnvironment,
                    environmentWorkContext, restoreHunger: !approvedSupportNeedsOnly,
                    approvedSupportNeedsOnly: approvedSupportNeedsOnly, telemetry: witness,
                    preparedEnvironment: safeConstructionCandidate);
                witness.actorStabilizationPasses++;
                witness.environmentStabilizationPasses++;
                nextActorStabilization = Time.realtimeSinceStartup + 5f;
            }
            witness.after = workOrders.Capture().orders.SingleOrDefault(value => value.workOrderId == witness.orderId);
            witness.terminalOutcome = ObserveConstructionTerminal(witness, beforeKeys);
            if (witness.after == null && witness.terminalOutcome != null) break;
            Require(witness.after != null, "Construction order disappeared before a published terminal result.");
            if (workOrders.RefreshMaterialsReady(site))
            {
                witness.materialsReady = true;
                break;
            }
            yield return new WaitForSecondsRealtime(0.25f);
        }
        timeScale.Scale = 0;
        CaptureHaulingWait(++materialObservation, materialsStarted, artifactPrefix);
        witness.materialWaitRealSeconds = Time.realtimeSinceStartup - materialsStarted;
        witness.materialWaitFrames = Time.frameCount - materialsStartFrame;
        witness.after = workOrders.Capture().orders.SingleOrDefault(value => value.workOrderId == witness.orderId);
        witness.terminalOutcome = ObserveConstructionTerminal(witness, beforeKeys);
        WriteNew(artifactPrefix + "-materials.json", JsonUtility.ToJson(witness, true));
        WriteNew(artifactPrefix + "-materials-world.json", saves.ToJson(saves.Capture()));
        if (!witness.materialsReady && witness.terminalOutcome == null)
        {
            witness.failure = "Actual construction materials have not reached the site in the bounded observation.";
            witness.outcomes = ReadAllOutcomes().Where(value => !beforeKeys.Contains(OutcomeKey(value))).ToList();
            WriteNew(artifactPrefix + "-result.json", JsonUtility.ToJson(witness, true));
            throw new InvalidOperationException(witness.failure);
        }
        if (witness.terminalOutcome == null)
        {
            StabilizeConstructionActors(
                constructionActors,
                deprivation,
                environmentFields,
                safeConstructionEnvironment,
                characterEnvironment,
                environmentWorkContext, restoreHunger: !approvedSupportNeedsOnly,
                approvedSupportNeedsOnly: approvedSupportNeedsOnly, telemetry: witness,
                preparedEnvironment: safeConstructionCandidate);
            witness.actorStabilizationPasses++;
            witness.environmentStabilizationPasses++;
            foreach (CharacterActor actor in constructionActors)
            {
                if (!CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork candidate)) continue;
                candidate.SetWorkPriority(
                    BuiltInWorkTypeIds.Construct,
                    WorkPriorityLevel.Priority1);
                witness.priorityOneWorkerIds.Add(actor.Identity.PersistentId);
                // A restored command target is persisted state, not proof that
                // the newly published Brain has been woken for that command.
                // Reissuing the same public command is intentionally idempotent
                // at the target boundary and restores OnDuty/AssignWork/replan
                // through WorkCommandHandler without granting any work units.
                Require(candidate.TrySetPriorityWorkTarget(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        null,
                        out string commandFailure),
                    $"Actual construction assignment rejected for {actor.Identity.PersistentId}: {commandFailure}");
                actor.Brain?.ClearPathSearchCache();
                witness.priorityCommandIssued = true;
                witness.commandedWorkerIds.Add(actor.Identity.PersistentId);
            }
            Require(witness.commandedWorkerIds.Contains(owner.Identity.PersistentId),
                "Actual owner was not included in the public construction assignments.");
        }
        witness.failure = string.Empty;
        WriteNew(artifactPrefix + "-command.json", JsonUtility.ToJson(witness, true));
        phase = "construction";
        float nextWorkObservation = Time.realtimeSinceStartup;
        IGameClock constructionClock = scope.Container.Resolve<IGameClock>();
        float workStartedGame = constructionClock.Time;
        float lastWorkProgressGame = workStartedGame;
        float lastCompletedWork = witness.after?.completedWork ?? witness.before.completedWork;
        var selfCareProgress = new Dictionary<CharacterActor, (AIAction action, long epoch, long revision, float gameTime)>();
        if (useAuthoredWorkBudget && witness.terminalOutcome == null)
        {
            IWorkAmountCalculator calculator = scope.Container.Resolve<IWorkAmountCalculator>();
            IRoomEnvironmentQuery roomEnvironment = scope.Container.Resolve<IRoomEnvironmentQuery>();
            float environmentMultiplier = roomEnvironment.GetWorkDurationMultiplier(site, BuiltInWorkTypeIds.Construct);
            float[] rates = constructionActors.Where(value => witness.commandedWorkerIds.Contains(value.Identity.PersistentId))
                .Select(value => calculator.CalculateWorkPerSecond(value, site, BuiltInWorkTypeIds.Construct, environmentMultiplier))
                .Where(value => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value)).ToArray();
            Require(rates.Length > 0, "Actual construction has no positive authoritative worker rate.");
            witness.authoredWorkRate = rates.Min();
            // Allow travel plus two single-worker work durations, never lower
            // the authored WU. Game/work progress and the outer real-time bound
            // remain independent failure gates for this larger housing fixture.
            witness.workGameSecondsBudget = 90f + 2f * Mathf.Max(0f,
                witness.before.requiredWork - lastCompletedWork) / witness.authoredWorkRate;
        }
        gameSpeed.SetPaused(false);
        gameSpeed.SetSpeed(5);
        // The isolated editor can deliver fewer than one frame per real second.
        // Preserve a bounded portion of those gaps so ordinary WorkTaskExecutor
        // ticks advance. Exterior cells exchange 35% with outdoors per game
        // second; at speed five a 0.6-second cap limits one frame to three field
        // ticks and keeps a 22C fixture above the Slime lethal minimum. No work
        // units or production environment decisions are written here.
        Time.maximumDeltaTime = 0.6f;
        witness.simulationMaximumDeltaTime = Time.maximumDeltaTime;
        witness.publicSpeedMultiplier = gameSpeed.Speed;
        while (useAuthoredWorkBudget ? constructionClock.Time - workStartedGame < witness.workGameSecondsBudget
            : Time.realtimeSinceStartup < deadline)
        {
            observe?.Invoke();
            witness.after = workOrders.Capture().orders.SingleOrDefault(value => value.workOrderId == witness.orderId);
            witness.workGameSeconds = constructionClock.Time - workStartedGame;
            if (approvedSupportNeedsOnly && witness.after != null)
            {
                witness.primitivePathSearchPendingActivityCount = constructionActors
                    .SelectMany(value => value.LogComponent.ActivityEntries)
                    .Count(value => value.ReasonCode == "primitive-path-search-pending");
                CharacterActivityEvent[] siteActivities = constructionActors.SelectMany(value => value.LogComponent.ActivityEntries)
                    .Where(value => value.KindId == CharacterActivityKinds.Work
                        && value.ActionId == BuiltInWorkTypeIds.Construct.Value
                        && value.TargetId == witness.siteId).ToArray();
                witness.routineNeedSuspendedActivityCount = siteActivities.Count(value =>
                    value.OutcomeId == CharacterActivityOutcomes.Changed && value.ReasonCode == "routine-need-suspended");
                witness.falseCompletedActivityCount = witness.after.completedWork < witness.after.requiredWork
                    ? siteActivities.Count(value => value.OutcomeId == CharacterActivityOutcomes.Completed) : 0;
                Require(witness.falseCompletedActivityCount == 0,
                    "Incomplete housing construction published a false Completed activity.");
            }
            if (useAuthoredWorkBudget && witness.after != null)
            {
                if (witness.after.completedWork > lastCompletedWork)
                {
                    lastCompletedWork = witness.after.completedWork;
                    lastWorkProgressGame = constructionClock.Time;
                }
                if (approvedSupportNeedsOnly)
                {
                    // Normal self-care may lawfully pause every builder. Only
                    // progress of the SAME real action/intent counts, not merely
                    // selecting, switching actions or reporting a phase string.
                    // The unchanged outer authored budget still expires and a
                    // real WU-complete construction terminal remains mandatory.
                    CharacterActor[] commanded = constructionActors.Where(value =>
                        witness.commandedWorkerIds.Contains(value.Identity.PersistentId)).ToArray();
                    bool allInSelfCare = commanded.Length > 0;
                    float oldestProgressGame = constructionClock.Time;
                    foreach (CharacterActor actor in commanded)
                    {
                        AIBrain brain = actor.Brain;
                        Require(!actor.IsDead && brain != null,
                            "A commanded housing worker became unavailable before construction completed.");
                        bool external = brain.IsExternallyDrivenActionActive;
                        long executionEpoch = external ? brain.ExternalIntentEpoch : brain.RuntimeActionEpoch;
                        bool actualSelfCare = external
                            ? brain.ExternalIntentOwnerId.StartsWith("survival:primitive:", StringComparison.Ordinal)
                                && brain.ExternalIntentKind >= CharacterActionIntentKind.RoutineNeed
                                && brain.ExternalIntentKind <= CharacterActionIntentKind.EmergencyPhysicalCritical
                            : brain.HasRunningAction && brain.bestAction?.actionset != null
                                && brain.bestAction.actionset.HasSemanticTag(CharacterAiActionTags.SelfCare);
                        actualSelfCare &= actor.TryGetAbility(out AbilityWork assigned)
                            && assigned.PriorityWorkTarget == site
                            && assigned.PriorityWorkTypeId == BuiltInWorkTypeIds.Construct;
                        float progressGame = lastWorkProgressGame;
                        if (actualSelfCare && selfCareProgress.TryGetValue(actor, out var previous)
                            && ReferenceEquals(previous.action, brain.bestAction)
                            && previous.epoch == executionEpoch)
                        {
                            progressGame = brain.GameplayProgressRevision > previous.revision
                                ? constructionClock.Time : previous.gameTime;
                        }
                        selfCareProgress[actor] = (brain.bestAction, executionEpoch,
                            brain.GameplayProgressRevision, progressGame);
                        allInSelfCare &= actualSelfCare;
                        oldestProgressGame = Mathf.Min(oldestProgressGame, progressGame);
                    }
                    if (allInSelfCare && oldestProgressGame > lastWorkProgressGame)
                    {
                        lastWorkProgressGame = oldestProgressGame;
                        witness.verifiedSelfCareProgressObservations++;
                    }
                }
                if (constructionClock.Time - lastWorkProgressGame >= 90f)
                {
                    // Preserve the exact failure boundary, not only a periodic
                    // snapshot that may precede a different action/continuation.
                    CaptureHaulingWait(++materialObservation, materialsStarted, artifactPrefix);
                }
                Require(constructionClock.Time - lastWorkProgressGame < 90f,
                    "Actual housing construction made no work or verified all-builder self-care progress for 90 game seconds.");
                // Shopping traverses MoveByPath as a nested iterator, so it
                // owns a movement operation without an action-routine handle.
                // Keep the existing physical clock cap for those real journeys;
                // otherwise stationary acceleration exhausts the unchanged
                // no-work bound while residents are still reaching their needs.
                Time.maximumDeltaTime = constructionActors.Any(value => value.TryGetComponent(out AbilityMove movement)
                    && (movement.HasActiveMovementRoutineForDiagnostics
                        || !string.IsNullOrEmpty(movement.ActiveMovementOperationOwnerForDiagnostics))) ? 0.1f : 0.6f;
            }
            if (Time.realtimeSinceStartup >= nextWorkObservation)
            {
                CaptureHaulingWait(++materialObservation, materialsStarted, artifactPrefix);
                nextWorkObservation = Time.realtimeSinceStartup + 20f;
            }
            // Environment and character exposure tick before this coroutine.
            // Restore the controlled construction fixture after every observed
            // frame so the next frame never inherits multiple accelerated-frame
            // gaps. WorkTaskExecutor and its production environment policy still
            // run first and remain the only authority that can grant work units.
            StabilizeConstructionActors(
                constructionActors,
                deprivation,
                environmentFields,
                safeConstructionEnvironment,
                characterEnvironment,
                environmentWorkContext, restoreHunger: !approvedSupportNeedsOnly,
                approvedSupportNeedsOnly: approvedSupportNeedsOnly, telemetry: witness,
                preparedEnvironment: safeConstructionCandidate);
            witness.actorStabilizationPasses++;
            witness.environmentStabilizationPasses++;
            // Completed orders intentionally leave the active save aggregate.
            // Absence alone is not success: bind its committed terminal result.
            witness.terminalOutcome = ObserveConstructionTerminal(witness, beforeKeys);
            if (witness.after == null && witness.terminalOutcome != null) break;
            yield return new WaitForSecondsRealtime(0.25f);
        }
        Time.maximumDeltaTime = originalMaximumDeltaTime;
        gameSpeed.SetSpeed(originalGameSpeed);
        timeScale.Scale = 0;
        CaptureHaulingWait(++materialObservation, materialsStarted, artifactPrefix);
        witness.completed = witness.after == null && witness.terminalOutcome?.publishedRevision > 0;
        witness.outcomes = ReadAllOutcomes().Where(value => !beforeKeys.Contains(OutcomeKey(value))).ToList();
        witness.failure = witness.completed ? string.Empty
            : "Actual construction remains incomplete in this bounded observation: " + worker.LastWorkOrderExecutionDetailForDiagnostics;
        WriteNew(artifactPrefix + "-result.json", JsonUtility.ToJson(witness, true));
        WriteNew(artifactPrefix + "-after-world.json", saves.ToJson(saves.Capture()));
        Require(witness.completed, witness.failure);
        double completed = witness.terminalOutcome.metrics.Single(value => value.metricId == MigratedProducerOutcomeIds.CompletedWorkMetric.Value).value;
        double required = witness.terminalOutcome.metrics.Single(value => value.metricId == MigratedProducerOutcomeIds.RequiredWorkMetric.Value).value;
        Require(required > 0 && completed >= required, "Construction terminal lacks the required real work amount.");
        Require(buildings.Buildings.Any(value => value != null && !(value is ConstructionSite) && !value.isDestroy
                && value.BuildingData == definition && value.centerPos == new Vector2Int(witness.x, witness.y)),
            "Completed construction did not publish the actual authored building.");
    }

    private static void StabilizeConstructionActors(
        IReadOnlyList<CharacterActor> actors,
        ICharacterDeprivationCommand deprivation,
        IEnvironmentalFieldPersistence environmentFields,
        DungeonEnvironmentalFieldSaveData safeEnvironment,
        ICharacterEnvironmentPersistence characterEnvironment,
        ICharacterEnvironmentWorkContext environmentWorkContext,
        bool restoreHunger = true, bool approvedSupportNeedsOnly = false,
        ConstructionWitness telemetry = null,
        DungeonStory.Environment.EnvironmentalFieldRestoreCandidate preparedEnvironment = null)
    {
        var candidate = preparedEnvironment;
        if (candidate == null)
        {
            long prepareStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            candidate = environmentFields.PrepareRestore(safeEnvironment);
            if (telemetry != null)
            {
                telemetry.environmentCandidatePreparations++;
                telemetry.environmentPrepareMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - prepareStarted)
                    * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            }
        }
        long environmentRestoreStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        environmentFields.Restore(candidate);
        if (telemetry != null)
        {
            telemetry.environmentRestoreMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - environmentRestoreStarted)
                * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (preparedEnvironment != null) telemetry.environmentCandidateReuses++;
        }
        if (approvedSupportNeedsOnly)
        {
            long needRestoreStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            // The user authorized these four needs only for the original
            // residents. Preserve hunger, fun, breakdown and actor exposure;
            // neutral field input remains the declared construction fixture.
            CharacterCondition[] approvedNeeds =
            {
                CharacterCondition.THIRST, CharacterCondition.SLEEP,
                CharacterCondition.EXCRETION, CharacterCondition.HYGIENE
            };
            foreach (CharacterActor actor in actors)
            {
                Require(actor != null && !actor.IsDead && actor.Stats != null,
                    "An approved support-needs resident is unavailable.");
                foreach (CharacterCondition need in approvedNeeds)
                {
                    Require(actor.Stats.TryGetConditionValue(need, out float current),
                        "Approved support need is missing: " + need);
                    // A zero-delta ChangesStat still publishes stats and mood.
                    // Keep the exact authorized target, but do not publish a
                    // second change when the fixture has already reached it.
                    if (current == 100f)
                    {
                        if (telemetry != null) telemetry.unchangedApprovedNeedWritesSkipped++;
                        continue;
                    }
                    actor.ChangesStat(need, 100f - current);
                }
            }
            if (telemetry != null)
                telemetry.approvedNeedRestoreMilliseconds +=
                    (System.Diagnostics.Stopwatch.GetTimestamp() - needRestoreStarted)
                    * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return;
        }
        HashSet<string> actorIds = actors
            .Where(actor => actor?.Identity != null && !actor.IsDead)
            .Select(actor => actor.Identity.PersistentId)
            .ToHashSet(StringComparer.Ordinal);
        DungeonCharacterEnvironmentSaveData environmentState =
            characterEnvironment.Capture();
        List<CharacterEnvironmentExposure> exposures =
            (environmentState.exposures
                ?? Array.Empty<CharacterEnvironmentExposure>())
            .Where(value => value != null
                && !actorIds.Contains(value.characterId))
            .ToList();
        foreach (string actorId in actorIds)
        {
            exposures.Add(new CharacterEnvironmentExposure
            {
                characterId = actorId,
                coldExposure = 0f,
                heatExposure = 0f,
                airborneExposure = 0f,
                visualStrain = 0f,
                physiologicalBand = EnvironmentalExposureBand.Stable,
                visualBand = EnvironmentalExposureBand.Stable,
                criticalDamageTimer = 0f,
                coldWorkCooldownActive = false
            });
        }
        environmentState.exposures = exposures
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ToArray();
        characterEnvironment.PublishRestoreCandidate(
            characterEnvironment.BuildRestoreCandidate(environmentState));
        foreach (string actorId in actorIds)
        {
            environmentWorkContext.ClearWorkContext(
                new CharacterId(actorId));
        }

        CharacterCondition[] needs =
        {
            CharacterCondition.HUNGER,
            CharacterCondition.THIRST,
            CharacterCondition.SLEEP,
            CharacterCondition.EXCRETION,
            CharacterCondition.HYGIENE,
            CharacterCondition.FUN
        };
        foreach (CharacterActor actor in actors)
        {
            if (actor == null || actor.IsDead || actor.Stats == null) continue;
            deprivation.DebugClearBreakdown(actor);
            foreach (CharacterCondition need in needs)
            {
                if (need == CharacterCondition.HUNGER && !restoreHunger) continue;
                if (actor.Stats.TryGetConditionValue(need, out float current))
                    actor.ChangesStat(need, 100f - current);
            }
        }
    }

    private GameplayOutcomeSnapshot ObserveConstructionTerminal(ConstructionWitness witness, ISet<string> beforeKeys)
    {
        if (witness.after != null)
        {
            Require(witness.after.terminalKind != WorkOrderTerminalKind.Cancelled,
                "Actual construction entered a cancelled terminal state.");
            Require(witness.after.terminalKind != WorkOrderTerminalKind.Completed
                || witness.after.completedWork >= witness.after.requiredWork,
                "Active construction completed before its required actual work.");
            witness.activeOrderTerminalPollsSkipped++;
            return null;
        }
        // WorkAmountSystem removes an order only after durable terminal commit,
        // in the same synchronous command. Query on that very observation, not
        // on every active-work frame; pending commit remains an active order.
        witness.terminalLedgerPolls++;
        GameplayOutcomeSnapshot terminal = ReadConstructionTerminal(witness.orderId, beforeKeys);
        Require(terminal != null && terminal.status == GameplayOutcomeStatus.Succeeded
            && terminal.facts.Any(fact => fact.factId == MigratedProducerOutcomeIds.TerminalKindFact.Value
                && fact.value == "completed"),
            "Construction disappeared without one successful completed terminal: " + witness.orderId);
        return terminal;
    }

    private GameplayOutcomeSnapshot ReadConstructionTerminal(string orderId, ISet<string> beforeKeys) =>
        ReadAllOutcomes().SingleOrDefault(value => !beforeKeys.Contains(OutcomeKey(value))
            && value.outcomeTypeId == "work.order-terminal"
            && value.facts.Any(fact => fact.factId == MigratedProducerOutcomeIds.WorkOrderFact.Value && fact.value == orderId));

    private void CaptureHaulingWait(int index, float started, string artifactPrefix = "facility-construction")
    {
        var observation = new HaulingWaitWitness
        {
            observationPhase = phase,
            realSeconds = Time.realtimeSinceStartup - started,
            gameTime = scope.Container.Resolve<IGameClock>().Time,
            frame = Time.frameCount, deltaTime = Time.deltaTime,
            unscaledDeltaTime = Time.unscaledDeltaTime, scale = Time.timeScale,
            maximumDeltaTime = Time.maximumDeltaTime,
            schedulerMilliseconds = scope.Container.Resolve<ICharacterAiDiagnosticsQuery>().LastProcessingMilliseconds,
            lastHaulReplan = (scope.Container.Resolve<IWorkforceReplanService>() as DungeonWorkforceReplanService)?.LastHaulReplanDetail,
            workOrder = scope.Container.Resolve<IWorkOrderRuntime>().Capture().orders
                .SingleOrDefault(value => value.workOrderId == report.facilityConstruction.orderId),
            activeServiceSessions = scope.Container.Resolve<IServiceSessionRuntime>().ActiveSessions
                .Select(ServiceRoomsSaveData.FromSnapshot).ToArray()
        };
        string materialDestination = report.facilityConstruction.before.materialDestinationId;
        WorldItemStackSnapshot[] materials = scope.Container.Resolve<IWorldItemStackRuntime>()
            .GetAllStacks().Where(stack => stack != null && string.Equals(
                stack.DestinationId, materialDestination, StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal).ToArray();
        var planning = scope.Container.Resolve<IWorldItemHaulPlanningService>()
            as WorldItemHaulPlanningService;
        Require(planning != null, "Actual construction candidate diagnostics require the production planner.");
        // Candidate explanation may populate the ordinary path-query cache but
        // never reserves a plan, changes priorities, or starts an action. Require
        // an existing carry component so the diagnostic cannot create one.
        foreach (CharacterActor actor in scope.Container.Resolve<ICharacterWorldQuery>()
                     .Characters.Where(value => value != null)
                     .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal))
        {
            AIBrain brain = actor.Brain;
            AbilityHaul haul = actor.GetComponent<AbilityHaul>();
            AbilityWork work = actor.GetComponent<AbilityWork>();
            AbilityShopping shopping = actor.GetComponent<AbilityShopping>();
            BuildingVisitorSnapshot needs = actor.BuildingVisitor.VisitorSnapshot;
            var value = new HaulingActorWitness
            {
                actorId = actor.Identity.PersistentId,
                worldPosition = actor.transform.position,
                hasBrain = brain != null,
                hasHaul = haul != null,
                hasWork = work != null,
                hunger = needs.Hunger, sleep = needs.Sleep, hygiene = needs.Hygiene,
                excretion = needs.Excretion, health = needs.CurrentHealth,
                shoppingRoutine = shopping != null && shopping.HasActiveShoppingRoutineForDiagnostics,
                shoppingTargetId = shopping != null && shopping.CurrentVisitTargetForDiagnostics != null
                    ? shopping.CurrentVisitTargetForDiagnostics.PersistentInstanceId.Value : string.Empty
            };
            if (brain != null)
            {
                CharacterAiDecisionScheduleObservation schedule = scope.Container.Resolve<ICharacterAiDiagnosticsQuery>().CaptureDecisionSchedule(actor);
                value.canRunAi = actor.CanRunAi;
                value.scoringPending = brain.IsActionScoringPending;
                value.pathDeferred = brain.IsPathSearchDeferred;
                value.preferredDeferred = brain.IsPreferredActionDeferred;
                value.schedulerProcessCount = brain.RuntimeSchedulerProcessCount;
                value.immediateReplanCount = brain.RuntimeImmediateReplanCount;
                value.lastImmediateReplanCaller = brain.LastImmediateReplanCallerForDiagnostics;
                value.candidateResetCount = brain.CandidateResetCountForDiagnostics;
                value.candidatePendingResetCount = brain.CandidatePendingResetCountForDiagnostics;
                value.candidateLastResetCaller = brain.CandidateLastResetCallerForDiagnostics;
                value.candidateLastPendingResetDetail = brain.CandidateLastPendingResetDetailForDiagnostics;
                value.candidatePendingDetail = brain.CandidatePendingDetailForDiagnostics;
                value.preferredActionType = brain.PreferredActionTypeForDiagnostics;
                value.preferredActionUntil = brain.PreferredActionUntilForDiagnostics;
                value.decisionTask = actor.Blackboard?.CurrentTask ?? string.Empty;
                value.decisionStatus = actor.Blackboard?.CurrentStatus ?? string.Empty;
                value.decisionTrace = actor.Blackboard?.LastDecisionTrace ?? string.Empty;
                value.decisionScheduled = schedule.IsScheduled;
                value.decisionDelay = schedule.RemainingSeconds;
                value.retryKind = schedule.RetryKind.ToString();
                value.lastActionFailure = brain.LastActionFailure.ToString();
                value.action = brain.CurrentActionDebugLabel;
                value.phase = brain.CurrentActionPhase;
                value.detail = brain.CurrentActionPhaseDetail;
                value.destination = brain.CurrentDestinationDebugLabel;
                value.brainSummary = brain.GetDebugSummary(8);
                value.selectedAction = brain.bestAction != null;
                value.runningAction = brain.HasRunningAction;
                value.gameplayProgressRevision = brain.GameplayProgressRevision;
                value.runtimeActionEpoch = brain.RuntimeActionEpoch;
                value.externalIntentActive = brain.IsExternallyDrivenActionActive;
                value.externalIntentOwner = brain.ExternalIntentOwnerId;
                value.externalIntentKind = brain.ExternalIntentKind.ToString();
                value.externalIntentEpoch = brain.ExternalIntentEpoch;
                value.decisionPending = brain.isBestActionEnd;
                value.actionExecuted = brain.isExecuted;
                value.selectedActionType = brain.bestAction?.actionset != null
                    ? brain.bestAction.actionset.GetType().Name : string.Empty;
                value.selectedDestinationId = brain.bestAction?.destination != null
                    ? brain.bestAction.destination.PersistentInstanceId.Value : string.Empty;
                value.runtimePhase = brain.CurrentRuntimePhase.ToString();
                value.cachedActions = brain.LastCandidateScores.Select(candidate => new CachedActionWitness
                {
                    action = candidate.ActionLabel,
                    score = candidate.Score,
                    failure = candidate.Failure.ToString(),
                    destinationId = candidate.Destination != null
                        ? candidate.Destination.PersistentInstanceId.Value : string.Empty
                }).ToList();
            }
            if (haul != null)
            {
                value.isHauling = haul.IsHauling;
                value.hasHaulRoutine = haul.HasHaulingRoutineForDiagnostics;
                value.haulStage = haul.CurrentExecutionStage;
                value.haulPlan = haul.CurrentPlanSummary;
                value.haulFailure = haul.LastFailureReason;
                value.haulPath = haul.ActivePathDebug;
                value.haulTerminal = haul.LastTerminalDiagnostics;
                value.haulStarts = haul.RuntimeHaulStartCount;
                value.haulTerminals = haul.RuntimeHaulTerminalCount;
                value.haulHeartbeat = haul.RoutineHeartbeat;
            }
            if (work != null)
            {
                value.isWorking = work.isWorking;
                value.workDetail = work.LastWorkOrderExecutionDetailForDiagnostics;
                value.haulPriority = work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul).ToString();
                value.priorityWorkType = work.PriorityWorkTypeId.Value;
                value.priorityWorkTarget = work.PriorityWorkTarget != null
                    ? work.PriorityWorkTarget.PersistentInstanceId.Value : string.Empty;
                value.emergencyWorkGate = work.HasEmergencyResponseWorkGateForDiagnostics;
                value.emergencyWorkType = work.EmergencyResponseOnlyWorkTypeForDiagnostics.Value;
            }
            foreach (WorldItemStackSnapshot material in materials)
            {
                var candidate = new HaulingCandidateWitness
                {
                    stackId = material.StackId, itemId = material.ItemId,
                    destinationId = material.DestinationId, position = material.Position,
                    quantity = material.Quantity, availableQuantity = material.AvailableQuantity
                };
                if (actor.GetComponent<CharacterCarryInventory>() != null)
                {
                    candidate.evaluated = true;
                    candidate.eligible = planning.TryExplainCandidateForEditorTest(
                        actor, material.StackId, out candidate.reason);
                }
                else
                    candidate.reason = "not-evaluated:existing-carry-component-missing";
                value.materialCandidates.Add(candidate);
            }
            CharacterLog activityLog = actor.GetComponent<CharacterLog>();
            if (activityLog != null)
                value.recentActivity = activityLog.ActivityEntries.TakeLast(8).ToList();
            observation.actors.Add(value);
        }
        string observationPrefix = artifactPrefix == "facility-construction" ? "facility-hauling" : artifactPrefix + "-hauling";
        WriteNew($"{observationPrefix}-observation-{index:D3}.json", JsonUtility.ToJson(observation, true));
    }

    private IEnumerator ObserveFacilityVisitor()
    {
        phase = "controlled-external-visitor";
        // Prepare only after RunFacilityWitness has drained the old transport.
        // Otherwise that drain cancels the newly created preview's requests.
        if (sourceV9FacilityIntake)
            ConfigureFacilityVisitorPreparationFixture();
        report.facilityUseOrigin = "production-spawner + controlled external visitor command";
        string id = facilityOwner.PersistentInstanceId.Value;
        ICharacterWorldQuery characters = scope.Container.Resolve<ICharacterWorldQuery>();
        var beforeKeys = ReadEntityOutcomes("facility", id).Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        // Keep autonomous admission paused so this witness owns exactly one
        // external visitor. The production spawner still owns population
        // admission, object creation, identity binding and entrance traversal.
        Require(pausedSpawner != null && !spawnerWasPaused,
            "Actual visitor command cannot override a pre-existing spawn pause.");
        Require(facilityVisitorDefinitionId > 0
                && !string.IsNullOrWhiteSpace(facilityVisitorProfileName),
            "Facility visitor preparation fixture has no selected population identity.");
        HashSet<string> existingIds = characters.Characters
            .Where(value => value != null)
            .Select(value => value.Identity.PersistentId)
            .ToHashSet(StringComparer.Ordinal);
        timeScale.Scale = originalScale;
        float deadline = Time.realtimeSinceStartup + 30f;
        CharacterActor visitor = null;
        bool spawnAccepted = false;
        while (visitor == null && Time.realtimeSinceStartup < deadline)
        {
            if (!spawnAccepted)
            {
                spawnAccepted = pausedSpawner.TrySpawnCharacter(
                    facilityVisitorDefinitionId,
                    out CharacterSpawnRejection rejection);
                report.facilityVisitorLastSpawnRejection = rejection.ToString();
            }
            visitor = characters.Characters.FirstOrDefault(value => value != null
                && !existingIds.Contains(value.Identity.PersistentId)
                && value.Identity.Data != null
                && value.Identity.Data.id == facilityVisitorDefinitionId);
            if (visitor == null) yield return null;
        }
        Require(visitor != null,
            "Production visitor preparation/spawn did not complete: "
                + report.facilityVisitorLastSpawnRejection
                + "; fixture responses=" + report.facilityVisitorSkillFixtureResponses
                + "; pending=" + generation.PendingRequestCount
                + "; generator=" + generation.LastDiagnostic
                + "; queue=" + queue.QueuedCount + "/" + queue.RunningCount);
        report.facilityVisitorActorId = visitor.Identity.PersistentId;
        while (visitor.CurrentLifecycleState != CharacterLifecycleState.Active
               && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        Require(visitor.CurrentLifecycleState == CharacterLifecycleState.Active
                && !visitor.IsDead
                && !CharacterWorkRoleUtility.TryGetWork(visitor, out _),
            "Spawned facility visitor is not a living external customer.");
        report.facilityVisitorExternal = true;
        CharacterSkillAcquisitionPublicationData[] visitorAcquisitions = scope.Container
            .Resolve<CharacterSkillAcquisitionPublicationRuntime>().Capture().observations
            .Where(value => value.fact.characterId == report.facilityVisitorActorId)
            .ToArray();
        Require(visitorAcquisitions.Length > 0
                && visitorAcquisitions.All(value => !value.pending)
                && visitorAcquisitions.Select(value => value.publicationKey)
                    .Distinct(StringComparer.Ordinal).Count() == visitorAcquisitions.Length
                && visitorAcquisitions.Max(value => value.fact.afterRevision)
                    == visitor.Progression.GrowthState.skillAcquisitionRevision,
            "Prepared visitor skill acquisitions lack completed, unique owner-bound observations.");
        WriteNew("facility-visitor-skill-acquisitions.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(
                visitorAcquisitions, Newtonsoft.Json.Formatting.Indented));
        Facility facility = facilityOwner as Facility;
        string visitFailure = string.Empty;
        Require(facility != null
                && facility.SupportsFacilityRole(CharacterVisitPolicy.GetInterestRoles(visitor))
                && CharacterVisitPolicy.CanVisitBuilding(
                    visitor,
                    facility,
                    alreadyVisited: false,
                    out visitFailure),
            "Spawned external customer cannot use the selected facility: " + visitFailure);
        if (facilityVisitorResponseProviderConfigured)
        {
            queue.ClearIsolatedFacilityVisitorTestResponses();
            facilityVisitorResponseProviderConfigured = false;
        }
        yield return CompleteFacilityUse(
            facility,
            requireTrainingGain: false,
            commandActor: visitor);
        report.facilityObservedUseOutcomes = ReadEntityOutcomes("facility", id)
            .Where(value => !beforeKeys.Contains(OutcomeKey(value))
                && value.outcomeTypeId == "service.facility-visited"
                && value.participants.Any(participant => participant.entityKindId == "character"
                    && participant.roleId == "actor"
                    && participant.participationKind == GameplayParticipationKind.Direct
                    && participant.entityId == report.facilityVisitorActorId))
            .ToList();
        WriteNew("facility-visitor-world.json", saves.ToJson(saves.Capture()));
        Require(report.facilityObservedUseOutcomes.Count == 1,
            "External visitor command did not publish exactly one facility completion.");
    }

    private void ConfigureFacilityVisitorPreparationFixture()
    {
        Require(ShopContinuationSkillResponseFixture.RunChecks(),
            "Facility visitor skill fixture parser checks failed.");
        ICharacterPopulationService population =
            scope.Container.Resolve<ICharacterPopulationService>();
        IFactionContractQuery factions = scope.Container.Resolve<IFactionContractQuery>();
        queue.ConfigureIsolatedAutomaticDispatch(false);
        skillAutomaticDispatchDeferred = true;
        CharacterSO[] eligibleDefinitions = (pausedSpawner?.characters ?? Array.Empty<CharacterSO>())
            .Where(definition => definition != null
                && definition.characterType == CharacterType.Customer
                && CharacterSpawnRules.IsRecruitmentEligible(
                    definition.species != null,
                    definition.species?.ownerSelectable == true,
                    definition.species?.homeFactionId,
                    definition.species != null
                        && !string.IsNullOrWhiteSpace(definition.species.homeFactionId)
                        && factions.IsContractUnlocked(
                            definition.species.homeFactionId,
                            FactionContractKind.Recruitment)))
            .OrderBy(definition => definition.id)
            .ToArray();
        Require(eligibleDefinitions.Length > 0,
            "Official world has no recruitment-admissible authored visitor definition.");
        report.facilityVisitorProfilesBeforePreparation = population.Profiles.Count;
        if (population.Profiles.Count == 0)
        {
            // A fresh official checkpoint may have no external pool yet. The
            // public spawn command starts the normal population preparation;
            // no profile, readiness, skill or admission state is supplied here.
            bool spawned = pausedSpawner.TrySpawnCharacter(
                eligibleDefinitions[0].id,
                out CharacterSpawnRejection preparationRejection);
            report.facilityVisitorPoolPreparationRequested = true;
            report.facilityVisitorPoolPreparationRejection = preparationRejection.ToString();
            Require(!spawned
                    && preparationRejection is CharacterSpawnRejection.PopulationProfilePreparing
                        or CharacterSpawnRejection.PopulationProfileNotCreated,
                "Empty visitor pool preparation returned an unexpected public spawn result: "
                    + preparationRejection);
        }
        report.facilityVisitorProfilesAfterPreparation = population.Profiles.Count;
        var selected = eligibleDefinitions
            .SelectMany(definition => population.Profiles
                .Where(profile => profile != null
                    && profile.isAlive
                    && !profile.isStaff
                    && !profile.isVisiting
                    && profile.characterDataId == definition.id)
                .Select(profile => new { definition, profile }))
            .OrderByDescending(value => value.profile.IsReady)
            .ThenBy(value => value.profile.persistentId, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(selected != null,
            "Official world has no admissible external population profile for facility use.");
        facilityVisitorDefinitionId = selected.definition.id;
        facilityVisitorProfileName = selected.profile.displayName;
        facilityVisitorPreparationOwnerId = selected.profile.persistentId;
        // Population previews are inactive. Drain unrelated preparation at the
        // service authority as well as the queue; queued capture-only replies
        // must not occupy the two generator slots ahead of this one visitor.
        foreach (CharacterProgression progression in UnityEngine.Object.FindObjectsByType<CharacterProgression>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (progression.Actor?.Identity?.PersistentId != facilityVisitorPreparationOwnerId)
                generation.CancelRequests(progression);
        }
        queue.ConfigureIsolatedFacilityVisitorTestResponses(captured =>
        {
            if (captured.ProfileId
                    != LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
                || !generation.TryCapturePendingModuleDispatch(captured.CorrelationId,
                    out CharacterProgression requestOwner, out _, out string prompt, out _)
                || requestOwner.Actor.Identity.PersistentId != facilityVisitorPreparationOwnerId
                || prompt != captured.Prompt
                || !HasExactPromptValue(
                    captured.Prompt,
                    "character=",
                    facilityVisitorProfileName))
            {
                return null;
            }
            string response = ShopContinuationSkillResponseFixture.Build(captured);
            if (response != null)
            {
                report.facilityVisitorSkillFixtureResponses++;
                report.facilityVisitorFixtureApplied = true;
            }
            return response;
        });
        facilityVisitorResponseProviderConfigured = true;
        report.requestScheduling += "; automatic capture dispatch deferred; only exact submitted visitor preparation IDs and the explicit facility request are dispatched; visitor preparation uses explicit isolated skill responses for one persisted external identity; facility command/outcome and evolution request remain production-owned";
    }

    private static bool HasExactPromptValue(
        string prompt,
        string prefix,
        string expected)
    {
        if (string.IsNullOrWhiteSpace(prompt)
            || string.IsNullOrWhiteSpace(prefix)
            || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }
        return prompt.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Any(line => string.Equals(
                line,
                prefix + expected,
                StringComparison.Ordinal));
    }

    private IEnumerator RunFacilityMaterialTrade()
    {
        phase = "trade-route";
        IFactionRuntime factions = scope.Container.Resolve<IFactionRuntime>();
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        string factionId = DungeonFactionIds.Kobold;
        Require(factions.Definitions.Any(value => value.StableId == factionId
                && value.TradeCargo.Any(cargo => cargo.itemId == "material:iron-ingot" && cargo.amount >= 3)),
            "The authored faction no longer offers the required iron; no invented offer is substituted.");
        Require(factions.TryGetFaction(factionId, out DungeonFactionState relationship),
            "Authored trade faction is not present in the actual world.");
        int IronQuantity() => items.GetAllStacks().Where(value => value.ItemId == "material:iron-ingot")
            .Sum(value => value.Quantity);
        var witness = new MaterialTradeWitness
        {
            factionId = factionId, trustBefore = relationship.trust,
            hourBefore = calendar.AbsoluteHour, ironBefore = IronQuantity()
        };
        report.facilityMaterialTrade = witness;
        var beforeKeys = ReadAllOutcomes().Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        WriteNew("facility-trade-before-world.json", saves.ToJson(saves.Capture()));
        // A continuation keeps the existing paid route; never pay/offer again.
        witness.resumedExistingRoute = !string.IsNullOrEmpty(resumedTradeRouteId);
        // The public map command offers physical goods worth 50. It owns
        // selection, actual consumption, rapport, and causally linked outcomes.
        while (!witness.resumedExistingRoute && !factions.IsContractUnlocked(factionId, FactionContractKind.Trade) && witness.gifts < 4)
        {
            Require(factions.TryOfferGoodwill(factionId, 50, out string goodwillFailure),
                "Actual goods-for-goodwill command failed: " + goodwillFailure);
            witness.gifts++;
        }
        Require(witness.resumedExistingRoute || factions.IsContractUnlocked(factionId, FactionContractKind.Trade),
            "Actual physical goodwill did not unlock trade; no rapport override is allowed.");
        factions.TryGetFaction(factionId, out relationship);
        witness.trustAfter = relationship.trust;
        string routeId = resumedTradeRouteId, tradeFailure = string.Empty;
        witness.accepted = witness.resumedExistingRoute || factions.TryRequestTrade(factionId, out routeId, out tradeFailure);
        witness.routeId = routeId;
        witness.failure = witness.accepted ? string.Empty : tradeFailure;
        WriteNew("facility-trade-command.json", JsonUtility.ToJson(witness, true));
        Require(witness.accepted, "Actual paid trade rejected: " + tradeFailure);
        timeScale.Scale = originalScale;
        gameSpeed.SetPaused(false);
        gameSpeed.SetSpeed(5);
        witness.publicSpeedMultiplier = gameSpeed.Speed;
        FactionRouteState Route() => factions.Routes.Single(value => value.routeId == routeId);
        float startedReal = Time.realtimeSinceStartup;
        float startedGame = report.clock.accumulatedGameSeconds;
        int startedFrame = Time.frameCount;
        // Authored route observed in r2 needs 180 game seconds, not 120 wall
        // seconds. The outer 240-wall-second slice remains the hard cap and
        // preserves an incomplete checkpoint if this host cannot simulate it.
        float gameBudget = 240f;
        float nextProgress = startedReal + 15f;
        int progressIndex = 0;
        while (Route().cargoDelivery.state != FactionRouteCargoDeliveryState.Delivered
            && report.clock.accumulatedGameSeconds - startedGame < gameBudget)
        {
            witness.realSeconds = Time.realtimeSinceStartup - startedReal;
            witness.gameSeconds = report.clock.accumulatedGameSeconds - startedGame;
            witness.frames = Time.frameCount - startedFrame;
            witness.route = Route();
            if (report.continuation != null)
                report.continuation.resumedRouteAdvanced |= witness.route.pathIndex > report.continuation.sourceRouteIndex
                    || witness.route.pathIndex == report.continuation.sourceRouteIndex
                        && witness.route.segmentProgress > report.continuation.sourceRouteProgress;
            if (Time.realtimeSinceStartup >= nextProgress)
            {
                WriteNew($"facility-trade-progress-{++progressIndex:D3}.json", JsonUtility.ToJson(witness, true));
                nextProgress = Time.realtimeSinceStartup + 15f;
            }
            yield return null;
        }
        gameSpeed.SetSpeed(originalGameSpeed);
        gameSpeed.SetPaused(originalGamePaused);
        timeScale.Scale = 0;
        witness.route = Route();
        witness.delivered = witness.route.cargoDelivery.state == FactionRouteCargoDeliveryState.Delivered;
        witness.ironAfter = IronQuantity();
        witness.hourAfter = calendar.AbsoluteHour;
        witness.outcomes = ReadAllOutcomes().Where(value => !beforeKeys.Contains(OutcomeKey(value))
            && value.subjects.Any(subject => subject.entityId == factionId || subject.entityId == routeId)).ToList();
        witness.failure = witness.delivered ? string.Empty
            : "Actual trade route did not deliver within the live window; its real checkpoint is preserved.";
        WriteNew("facility-trade-result.json", JsonUtility.ToJson(witness, true));
        WriteNew("facility-trade-after-world.json", saves.ToJson(saves.Capture()));
        Require(witness.delivered && witness.ironAfter >= (witness.resumedExistingRoute ? 3 : witness.ironBefore + 3), witness.failure);
        var publishedOutcomes = ReadAllOutcomes();
        (string RunId, long Sequence) ReadPublicationIdentity(
            string publicationId,
            string phase)
        {
            int separator = publicationId?.LastIndexOf('/') ?? -1;
            long sequence = 0L;
            Require(separator > 0
                    && long.TryParse(
                        publicationId.Substring(separator + 1),
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out sequence)
                    && sequence > 0L,
                "Trade " + phase + " has no canonical publication identity.");
            return (publicationId.Substring(0, separator), sequence);
        }
        foreach (var identity in new[]
        {
            ReadPublicationIdentity(
                witness.route.settlementPublicationId,
                "settlement"),
            ReadPublicationIdentity(
                witness.route.arrivalPublicationId,
                "arrival"),
            ReadPublicationIdentity(witness.route.cargoPublicationId, "cargo")
        })
        {
            Require(publishedOutcomes.Any(value => value.runId == identity.Item1 && value.sequence == identity.Item2
                    && value.publishedRevision > 0),
                "Trade settlement, arrival or cargo lacks its actual published outcome.");
        }
        CancelOutstandingTransport();
    }

    private JObject CaptureLivePlanState() => new()
    {
        ["actor.alive"] = owner != null && !owner.IsDead,
        ["actor.level"] = owner.Progression.Level,
        ["actor.traitScore"] = ReadTraitScore(),
        ["actor.nextSkillUnlock"] = targetUnlock,
        ["request.skillCaptured"] = firstDispatch != null,
        ["request.traitCaptured"] = traitDispatch != null
    };

    private static JObject LiveRequestReceipt(GameplayModuleDispatchEligibilityWitness identity,
        string wireSha256, string checkpointSha256) => new()
    {
        ["profileId"] = identity.profileId, ["ownerId"] = identity.ownerId,
        ["requestId"] = identity.requestId, ["correlationId"] = identity.correlationId,
        ["wireSha256"] = wireSha256, ["checkpointSha256"] = checkpointSha256
    };

    private static JObject LiveTargetRequestReceipt(GameplayModuleDispatchEligibilityWitness identity,
        string wireSha256, string checkpointSha256)
    {
        JObject request = LiveRequestReceipt(identity, wireSha256, checkpointSha256);
        request["targetId"] = identity.targetId;
        return request;
    }

    private JObject CaptureEquipmentLivePlanState(string instanceId) => new()
    {
        ["actor.alive"] = owner != null && !owner.IsDead,
        ["equipment.affinity"] = ReadAffinity(equipmentEvolution.GetState(instanceId), owner.Identity.PersistentId),
        ["request.equipmentCaptured"] = equipmentDispatch != null
    };

    private JObject CaptureFacilityLivePlanState(string facilityId) => new()
    {
        ["facility.exists"] = facilityOwner != null && !facilityOwner.isDestroy
            && facilityOwner.PersistentInstanceId.Value == facilityId,
        ["facility.visitsObserved"] = report.commands.Count(value => value.facilityId == facilityId),
        ["request.facilityPending"] = facilityOwner != null
            && facilityOwner.GetComponent<FacilityEvolutionStateComponent>()?.PendingFormulaPresentation != null,
        ["request.facilityCaptured"] = facilityDispatch != null
    };

    private IEnumerator RunTrainingRequests(string selectedActionId)
    {
        // The plan chooses a supported asynchronous production command macro.
        // Reject an unsupported action before performing any training.
        Require(selectedActionId == null || selectedActionId == LiveNarrativeCausalPlan.ActionId,
            "No live command adapter exists for the selected plan action.");
        // Multiple real experience groups can cross a trait gate on the first
        // completed action. Arm observation before gameplay, not after skill unlock.
        traitPhase = true;
        queue.ConfigureIsolatedRequestCapture(CaptureDispatch);
        if (sourceV9SkillIntake)
        {
            queue.ConfigureIsolatedAutomaticDispatch(false);
            skillAutomaticDispatchDeferred = true;
            report.requestScheduling += "; exact submitted trait/skill transport dispatch during intake; unrelated requests remain queued";
        }
        CancelOutstandingTransport();
        yield return RunTraitWitness();
        traitPhase = false;
        replaying = false;
        CancelOutstandingTransport();
        timeScale.Scale = originalScale;

        while (owner.Progression.Level < targetUnlock) yield return CompleteTraining();
        // Select the existing draft via normal cancellation/resubmission, never CreateDraft.
        ResumeOnlyExistingTarget();
        float dispatchUntil = Time.realtimeSinceStartup + 30f;
        while (firstDispatch == null && Time.realtimeSinceStartup < dispatchUntil)
        {
            if (sourceV9SkillIntake) DispatchExistingSkillTargetForIsolatedWitness();
            yield return null;
        }
        if (firstDispatch == null)
            WriteNew("skill-dispatch-failure-diagnostics.json", new JObject
            {
                ["ownerId"] = owner.Identity.PersistentId,
                ["unlockLevel"] = targetUnlock,
                ["serviceDiagnostic"] = generation.LastDiagnostic,
                ["queueError"] = queue.LastError,
                ["queueState"] = queue.GetRequestDiagnosticsForDebug(),
                ["lastInferenceAudit"] = generation.LastInferenceAudit.HasValue
                    ? JObject.FromObject(generation.LastInferenceAudit.Value) : null,
                ["submittedPromptCaptured"] = skillSubmittedPromptCaptured
            }.ToString());
        Require(firstDispatch != null, "Real training unlock did not dispatch a bound module request: "
            + generation.LastDiagnostic + "; queueError=" + queue.LastError);
    }

    private IEnumerator RunTraitWitness()
    {
        while (ReadTraitScore() < 3) yield return CompleteTraining();
        if (traitDispatch == null) CancelOutstandingTransport();
        float dispatchUntil = Time.realtimeSinceStartup + 30f;
        while (traitDispatch == null && Time.realtimeSinceStartup < dispatchUntil)
        {
            if (sourceV9SkillIntake) DispatchExistingTraitTargetForIsolatedWitness();
            yield return null;
        }
        Require(traitDispatch != null, "Gameplay reached trait milestone 3 without a bound dispatch: " + TraitDiagnostic());
        if (report.resumedExistingTraitRequest)
        {
            report.resumedTraitPendingExact = JsonUtility.ToJson(traitDispatch.pending) == resumedTraitPendingJson;
            report.resumedTraitEvidenceBound = traitDispatch.evidence.Count > 0
                && report.resumedTraitOutcomeKeys.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(traitDispatch.evidence.Select(OutcomeKey));
            Require(report.resumedTraitPendingExact && report.resumedTraitEvidenceBound,
                "Resumed trait dispatch changed the saved request or its authoritative existing evidence.");
        }
        else
        {
            var commandKeys = new HashSet<string>(report.commands.SelectMany(value => value.publishedOutcomes)
                .Select(OutcomeKey), StringComparer.Ordinal);
            report.traitCommandExactEvidenceBound = traitDispatch.evidence.Any(value => commandKeys.Contains(OutcomeKey(value)));
            // The public command can first cancel an existing work action. That
            // real activity may cross the trait gate before training completes.
            // Its persisted aggregate is a supported legacy evidence source, not
            // an exact global outcome. Join observed typed activities AND count
            // transitions to the frozen selected fact; chronology alone is insufficient.
            var frozenContext = traitDispatch.pending.RestorePublicContext();
            Require(frozenContext != null, "Live trait witness requires a frozen public context.");
            foreach (CommandWitness command in report.commands)
            foreach (NarrativePublicFactSnapshot fact in frozenContext.CaptureSnapshot().publicFacts)
            {
                if (LiveNarrativeActivityEvidence.TryMatch(command.observedActivityLedger, fact,
                    traitDispatch.pending.evidenceFactIds, out string detail)
                    || LiveNarrativeActivityEvidence.TryMatchState(command.observedActivityLedger, fact,
                        traitDispatch.pending.evidenceFactIds, out detail))
                    report.traitCommandActivityEvidence.Add(detail);
            }
            report.traitCommandActivityEvidenceBound = report.traitCommandActivityEvidence.Count > 0;
            Require(report.traitCommandExactEvidenceBound || report.traitCommandActivityEvidenceBound,
                "Trait pending consumes neither an exact command outcome nor an observed activity with a persisted ledger transition.");
        }
        timeScale.Scale = 0;
        CharacterActor previousOwner = owner;
        replaying = true;
        Require(saves.TryRestore(saves.FromJson(traitCheckpointJson), out DungeonGameRestoreReport restored),
            "Official trait checkpoint restore failed: " + string.Join(" | ", restored.Errors));
        report.traitOfficialWorldRestore = true;
        Require(scope.Container.Resolve<ICharacterWorldSaveService>().TryGetRestoredActor(traitDispatch.ownerId, out owner),
            "Restored actor index omitted the trait owner.");
        report.traitFreshPublishedOwner = !ReferenceEquals(previousOwner, owner);
        Require(report.traitFreshPublishedOwner && !owner.IsDead, "Trait restore did not publish a fresh living owner.");
        CharacterAcquiredTraitPendingRequestState traitPending = owner.Progression.CaptureAcquiredTraitState()
            .pendingRequests.Single(value => value.requestKey == traitDispatch.pending.requestKey);
        WriteNew("trait-restored-pending.json", JsonUtility.ToJson(traitPending, true));
        WriteNew("trait-restored-owner-binding.json", JsonUtility.ToJson(CharacterNarrativeRequestOwnerBinding.Capture(owner.Progression), true));
        var boundIds = traitPending.evidenceBindings.Select(value => value.publicFactId).ToHashSet(StringComparer.Ordinal);
        var restoredContext = CharacterAcquiredTraitPromptBuilder.BuildPublicMaterial(scope.Container.Resolve<NarrativeRequestContextBuilder>(), owner.Progression,
            traitPending.evidenceFactIds.Where(value => !boundIds.Contains(value)));
        restoredContext = GameplayOutcomeEvidencePublicMaterialComposer.AddExactEvidence(restoredContext, traitPending.evidenceBindings);
        WriteNew("trait-restored-context.json", JsonUtility.ToJson(restoredContext.CaptureSnapshot(), true));
        Require(traitPending.ownerBinding != null && traitPending.ownerBinding.Matches(owner.Progression, out _)
            && JsonUtility.ToJson(traitPending) == JsonUtility.ToJson(traitDispatch.pending)
            && ReadTraitScore() == traitDispatch.experienceScore,
            "Official restore changed trait ownership, milestone, evidence, offers or experience score.");
        report.traitPendingExact = true;
        WriteNew("trait-restored-world.json", saves.ToJson(saves.Capture()));
        CancelOutstandingTransport();
        dispatchUntil = Time.realtimeSinceStartup + 30f;
        while (!report.traitWireReplayExact && Time.realtimeSinceStartup < dispatchUntil)
        {
            if (sourceV9SkillIntake) DispatchExistingTraitTargetForIsolatedWitness();
            yield return null;
        }
        Require(report.traitWireReplayExact, "Restored trait did not dispatch the identical actual wire request: " + TraitDiagnostic());
    }

    private IEnumerator CompleteTraining()
    {
        Facility facility = UnityEngine.Object.FindObjectsByType<Facility>(FindObjectsSortMode.None)
            .Where(value => value != null && value.isActiveAndEnabled && !value.isDestroy
                && value.SupportsFacilityRole(FacilityRole.Training))
            .OrderBy(value => (value.transform.position - owner.transform.position).sqrMagnitude)
            .ThenBy(value => value.PersistentInstanceId.Value, StringComparer.Ordinal).FirstOrDefault();
        Require(facility != null, "Authored world has no live training facility; no fixture is substituted.");
        yield return CompleteFacilityUse(facility, requireTrainingGain: true);
    }

    private IEnumerator CompleteFacilityUse(
        Facility facility,
        bool requireTrainingGain,
        CharacterActor commandActor = null)
    {
        CharacterActor actor = commandActor ?? owner;
        Require(report.commands.Count < 16, "Authored facility actions did not reach the requested live gates within 16 completed uses.");
        Require(actor != null && !actor.IsDead && actor.Brain != null,
            "Controlled facility use requires a living actor with a production Brain.");
        Require(facility.SupportsFacilityRole(CharacterVisitPolicy.GetInterestRoles(actor))
                && CharacterVisitPolicy.CanVisitBuilding(actor, facility, alreadyVisited: false, out _),
            "Controlled use must obey the actor's real facility interest and visit admission policy.");
        string actorId = actor.Identity.PersistentId;
        var beforeIds = new HashSet<string>(
            ReadEntityOutcomes("character", actorId).Select(OutcomeKey),
            StringComparer.Ordinal);
        var command = new CommandWitness
        {
            command = requireTrainingGain ? "training-facility.interact" : "facility.interact",
            actorId = actorId, facilityId = facility.RequirePersistentInstanceId().Value,
            levelBefore = actor.Progression.Level, experienceBefore = actor.Progression.CurrentExperience,
            usesBefore = facility.FacilityState.completedUses, hourBefore = calendar.AbsoluteHour,
            traitScoreBefore = ReadTraitScore()
        };
        command.observedActivityLedger.actorId = actorId;
        command.observedActivityLedger.factsBefore = actor.Progression.NarrativeLedger.Facts.Select(value => value.Clone()).ToList();
        CharacterLog observedLog = actor.LogComponent;
        Require(observedLog != null, "Actual actor activity log is required for causal observation.");
        void CaptureActivity(CharacterLogEntry entry)
        {
            if (entry.Activity != null) command.observedActivityLedger.activities.Add(entry.Activity);
        }
        // Subscribe before arbitration: replacing an ongoing work action emits
        // a real cancellation synchronously from TryBeginExternallyDrivenAction.
        observedLog.OnLogAdded += CaptureActivity;
        controlledFacilityCommandActive = true;
        IDisposable moodObservation = null, conditionObservation = null;
        try
        {
            IGameEventBus events = scope.Container.Resolve<IGameEventBus>();
            void CaptureState(CharacterStateNarrativeProjection projection)
            {
                if (projection.Character.Value == actorId)
                    command.observedActivityLedger.stateChanges.Add(
                        LiveNarrativeStateObservation.FromProjection(projection));
            }
            moodObservation = events.Subscribe<CharacterMoodFactorAppliedEvent>(value =>
            {
                if (CharacterStateNarrativeObserver.TryProject(value, out var projection))
                    CaptureState(projection);
            });
            conditionObservation = events.Subscribe<CharacterConditionChangedEvent>(value =>
            {
                if (CharacterStateNarrativeObserver.TryProject(value, out var projection))
                    CaptureState(projection);
            });
            if (requireTrainingGain && sourceV9SkillExpeditionActor)
            {
                yield return CompleteTrainingThroughRegisteredAi(actor, facility, command);
            }
            else
            {
                Require(actor.Brain.TryBeginExternallyDrivenAction("qa:live-narrative-facility",
                    CharacterActionIntentKind.RoutineNeed, "실제 시설 이용", "이동·이용", "controlled-production-command", out lease),
                    "Actual actor action arbitration rejected facility use.");
                ownsLease = true;
                leasedActor = actor;
                yield return facility.Interact(actor.BuildingVisitor);
            }
        }
        finally
        {
            moodObservation?.Dispose();
            conditionObservation?.Dispose();
            controlledFacilityCommandActive = false;
            observedLog.OnLogAdded -= CaptureActivity;
            command.observedActivityLedger.factsAfter = actor.Progression.NarrativeLedger.Facts.Select(value => value.Clone()).ToList();
        }
        command.levelAfter = actor.Progression.Level;
        command.experienceAfter = actor.Progression.CurrentExperience;
        command.usesAfter = facility.FacilityState.completedUses;
        command.hourAfter = calendar.AbsoluteHour;
        command.traitScoreAfter = ReadTraitScore();
        command.publishedOutcomes = ReadEntityOutcomes("character", actorId)
            .Where(value => !beforeIds.Contains(OutcomeKey(value))).ToList();
        report.commands.Add(command);
        WriteNew($"command-{report.commands.Count:D3}.json", JsonUtility.ToJson(command, true));
        Require(command.usesAfter == command.usesBefore + 1, "Facility did not complete exactly one real use.");
        Require(!requireTrainingGain || command.levelAfter > command.levelBefore || command.experienceAfter > command.experienceBefore,
            "Completed authored training did not grant experience.");
        Require(command.publishedOutcomes.Any(value => value.outcomeTypeId == "service.facility-visited"),
            "Actual facility completion did not publish its character result.");
        if (ownsLease)
        {
            Require(actor.Brain.EndExternallyDrivenAction(lease), "Actual action lease was lost on completion.");
            ownsLease = false;
            leasedActor = null;
        }
        yield return null;
    }

    private IEnumerator CompleteTrainingThroughRegisteredAi(
        CharacterActor actor, Facility facility, CommandWitness command,
        bool requireStaffDuty = true, Action observe = null, bool mealVisit = false,
        bool recordRetryableMealSupplyCancellation = false, bool restVisit = false,
        bool requireExactUseParent = false)
    {
        Require(!mealVisit || !restVisit, "Registered use cannot be both a meal and a rest.");
        bool exactVisit = mealVisit || restVisit;
        bool exactCompletion = exactVisit || requireExactUseParent;
        bool hasDuty = CharacterWorkRoleUtility.TryGetWork(actor, out AbilityWork work);
        Require(!requireStaffDuty || hasDuty, "Actual staff training requires its duty owner.");
        Require(actor.TryGetAbility(out AbilityShopping shopping),
            "Actual staff training requires its visitor action owner.");
        // Public off-duty policy and candidate commitment retain the authored
        // AI definition, physical path, admission, reservation and executor.
        // An external action lease has no AIAction and cannot be persisted as
        // a resumable facility visit; do not relax that save contract.
        actor.Brain.StopCurrentActionForReplan("qa:staff-training-visit");
        if (hasDuty) work.SetDutyState(AbilityWork.DutyState.OffDuty);
        // Each controlled command models a distinct authored off-duty visit
        // cycle. This is the same visitor-owned command invoked by the duty
        // controller, not a count write or a retry after a rejected candidate.
        shopping.BeginOffDutyVisitCycle();
        command.publicOffDutyVisitCycleStarted = true;
        if (!exactVisit)
        {
            // This fixture issues an explicit training visit, not an autonomous
            // leisure-choice observation. Use the existing command preference;
            // do not assign action.score or bypass normal admission/execution.
            Require(actor.Brain.PreferActionOnNextDecision<AIShopping>(),
                "Explicit training visit has no registered shopping action.");
            command.publicTrainingVisitIntentRequested = true;
        }
        actor.Brain.RequestImmediateReplan(clearFailures: true);
        Predicate<AIActionSet> shoppingAction = value => mealVisit ? value is AIEat : restVisit ? value is AIRest : value is AIShopping;
        AIAction committed = null;
        float startedReal = Time.realtimeSinceStartup;
        float startedGame = scope.Container.Resolve<IGameClock>().Time;
        while (committed == null && Time.realtimeSinceStartup - startedReal < 30f)
        {
            observe?.Invoke();
            // A primitive meal may be finishing after its physical commit.
            // Observe its normal release within the existing admission bound;
            // do not cancel it or commit a second action beside the active owner.
            if (actor.Brain.IsExternallyDrivenActionActive)
            {
                yield return null;
                continue;
            }
            bool found = actor.Brain.TryFindBestScoredAction(shoppingAction,
                out CharacterAiActionCandidate selected);
            AIActionFailure failure = selected.Failure;
            if (!found && !failure.HasFailure)
            {
                // Distinguish a missing registered action from an evaluated
                // zero-score candidate. Observation only; never grant a score.
                string catalog = string.Join("|", (actor.Brain.availableActions ?? Array.Empty<AIAction>())
                    .Where(value => value?.actionset != null)
                    .Select(value => value.actionset.GetType().Name + ":" + value.actionset.Branch));
                Debug.Log($"REGISTERED_FACILITY_CANDIDATE_NONE: actor={command.actorId}; "
                    + $"action={selected.Action?.actionset?.GetType().Name ?? "null"}; score={selected.Score}; "
                    + $"rawScore={selected.Action?.score}; running={actor.Brain.bestAction?.actionset?.GetType().Name ?? "null"}; "
                    + $"hasDuty={hasDuty}; offDuty={hasDuty && work.IsOffDuty}; visits={shopping.visitCount}; catalog={catalog}");
            }
            if (found)
            {
                // Meal staging only observes physical supply. The registered
                // action owns the usable/queueable destination, rather than
                // the QA fixture replacing it with a different food buffer.
                Facility mealDestination = exactVisit ? selected.Destination as Facility : null;
                Require(!mealVisit || selected.Score > 0f && mealDestination != null
                        && mealDestination.HasMealAvailableFor(actor, out CharacterConsumablesFailure available)
                        && !available.IsFailure,
                    "Registered meal candidate lacks positive score and actual physical food.");
                Require(!restVisit || selected.Score > 0f && mealDestination != null
                        && mealDestination.BuildingData.GetAbility<BuildingExpeditionRecoveryAbility>() is { } recoveryAbility
                        && recoveryAbility.healthHealRatio > 0f && recoveryAbility.injuryReduction > 0f
                        && recoveryAbility.stressRecovery > 0f,
                    "Registered rest candidate lacks positive score and an authored recovery effect.");
                var target = exactVisit ? selected : new CharacterAiActionCandidate(selected.Action,
                    selected.Score, selected.Failure, selected.DebugLabel, facility);
                if (actor.Brain.TryCommitActionCandidate(target, out failure))
                {
                    if (exactVisit)
                    {
                        facility = mealDestination;
                        command.facilityId = facility.RequirePersistentInstanceId().Value;
                        command.usesBefore = facility.FacilityState.completedUses;
                    }
                    committed = selected.Action;
                    command.registeredCandidateScore = selected.Score;
                }
            }
            // A dynamic meal buffer is not an immediately usable path/queue.
            // Observe ordinary admission within the existing 30-second bound;
            // NoDestination is never treated as a commit or successful visit.
            Require(committed != null || failure.IsDeferred
                    || mealVisit && !found && failure.Kind == AIActionFailureKind.NoDestination,
                "Actual registered training visit rejected: " + failure.Kind + ": " + failure.Reason);
            if (committed == null) yield return null;
        }
        Require(committed != null && (mealVisit ? committed.actionset is AIEat : restVisit ? committed.actionset is AIRest : committed.actionset is AIShopping)
                && committed.destination == facility,
            "Registered training action did not commit the exact authored facility.");
        HashSet<string> beforeMealOutcomeKeys = exactVisit
            ? ReadEntityOutcomes("character", command.actorId).Select(OutcomeKey).ToHashSet(StringComparer.Ordinal)
            : null;
        IGameClock clock = scope.Container.Resolve<IGameClock>();
        AbilityMove movement = actor.GetComponent<AbilityMove>();
        float executionStartedGame = clock.Time;
        string executionPrefix = string.IsNullOrEmpty(command.activeUseCheckpointFile)
            ? $"training-active-world-{report.commands.Count + 1:D3}" : Path.GetFileNameWithoutExtension(command.activeUseCheckpointFile);
        void RecordMealExecution(string stage)
        {
            if (!exactVisit) return;
            WriteNew(executionPrefix + "-execution-" + stage + ".json", new JObject
            {
                ["origin"] = "readonly registered-facility execution diagnostics; no action/state mutation",
                ["actorId"] = command.actorId, ["facilityId"] = command.facilityId,
                ["stage"] = stage, ["actorX"] = actor.GetNowXY().x, ["actorY"] = actor.GetNowXY().y,
                ["elapsedRealSeconds"] = Time.realtimeSinceStartup - startedReal,
                ["elapsedGameSeconds"] = clock.Time - startedGame,
                ["admissionGameSeconds"] = executionStartedGame - startedGame,
                ["elapsedExecutionGameSeconds"] = committed.HasStarted
                    ? (JToken)(clock.Time - executionStartedGame) : JValue.CreateNull(),
                ["executionGameTimeBound"] = 90f,
                ["sameSelectedAction"] = ReferenceEquals(actor.Brain.bestAction, committed),
                ["selectedStarted"] = committed.HasStarted, ["brainExecuted"] = actor.Brain.isExecuted,
                ["brainEnded"] = actor.Brain.isBestActionEnd, ["phase"] = actor.Brain.CurrentActionPhase,
                ["phaseDetail"] = actor.Brain.CurrentActionPhaseDetail,
                ["externalIntentActive"] = actor.Brain.IsExternallyDrivenActionActive,
                ["externalIntentOwner"] = actor.Brain.ExternalIntentOwnerId,
                ["externalIntentKind"] = actor.Brain.ExternalIntentKind.ToString(),
                ["externalIntentEpoch"] = actor.Brain.ExternalIntentEpoch,
                ["pathSteps"] = committed.pathSteps.Count, ["planKind"] = committed.planKind.ToString(),
                ["shoppingRoutine"] = shopping.HasActiveShoppingRoutineForDiagnostics,
                ["shoppingOutcome"] = shopping.LastVisitOutcome.ToString(),
                ["movementRoutine"] = movement != null && movement.HasActiveMovementRoutineForDiagnostics,
                ["systemMovement"] = movement != null && movement.IsSystemMoveInProgress,
                ["protectedMovement"] = movement != null && movement.HasProtectedSystemMovementOwnership,
                ["movementOwner"] = movement?.ActiveMovementOperationOwnerForDiagnostics ?? string.Empty,
                ["movementVersion"] = movement?.MovementOperationVersionForDiagnostics ?? -1,
                ["movementCancellation"] = movement?.LastMovementCancellationSourceForDiagnostics ?? string.Empty,
                ["actionMovementCancellation"] = movement?.LastActionMovementCancellationReasonForDiagnostics ?? string.Empty,
                ["movementPreemption"] = movement?.LastMovementOperationPreemptionForDiagnostics ?? string.Empty,
                ["movementRejectedOwner"] = movement?.LastRejectedMovementOperationOwnerForDiagnostics ?? string.Empty,
                ["movementFailure"] = movement?.LastGridMoveFailureReason.ToString() ?? "missing",
                ["activeUseObserved"] = command.activeUseObserved, ["useOperationId"] = command.useOperationId,
                ["beforeOutcomeKeys"] = stage == "prepared" ? new JArray(beforeMealOutcomeKeys.OrderBy(value => value, StringComparer.Ordinal)) : null,
                ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
            }.ToString());
        }
        RecordMealExecution("prepared");
        phase = mealVisit ? "skill-normal-nutrition-meal" : restVisit ? "skill-normal-housing-rest-recovery" : $"skill-intake-training-{report.commands.Count + 1:D3}";
        // Candidate commitment only selects. The production decision pipeline
        // separately invokes this actor execution command; use the same owner
        // rather than expecting a later autonomous decision to start our pick.
        Require(actor.TryExecuteSelectedAiAction() && committed.HasStarted,
            "Selected registered training action did not start: " + actor.Brain.LastActionFailure);
        executionStartedGame = clock.Time;
        RecordMealExecution("started");
        command.executionPath = $"Brain(candidate)->CharacterActor.TryExecuteSelectedAiAction->{(mealVisit ? "AIEat" : restVisit ? "AIRest" : "AIShopping")}->AbilityShopping->Facility.Interact";
        command.actionType = committed.actionset.GetType().FullName;
        command.actionBranch = (int)committed.actionset.Branch;
        float nextExecutionObservation = clock.Time + 30f;
        int executionObservationSequence = 0;
        bool OwnMealUseCompleted() => command.activeUseObserved
            && facility.FindActiveUse(command.actorId) == null
            && shopping.LastVisitOutcome == ShoppingVisitOutcome.Completed;
        // Other hungry residents may legitimately use this facility while the
        // selected actor approaches it. A global use count is not that actor's
        // completion identity; keep waiting for its own persisted operation.
        // Admission already has a separate30-real-second bound. r18 spent
        // 62.74game seconds there, leaving only27.30 for the actual executor.
        // Keep90 execution game seconds and180 whole-chain real seconds; do
        // not count pre-execution candidate waiting as executed movement/use.
        while ((exactCompletion ? !OwnMealUseCompleted() : facility.FacilityState.completedUses == command.usesBefore)
               && Time.realtimeSinceStartup - startedReal < 180f
               && clock.Time - executionStartedGame < 90f)
        {
            observe?.Invoke();
            if (exactVisit && clock.Time >= nextExecutionObservation)
            {
                RecordMealExecution($"progress-{++executionObservationSequence:D3}");
                nextExecutionObservation = clock.Time + 30f;
            }
            if (mealVisit && recordRetryableMealSupplyCancellation
                && !shopping.HasActiveShoppingRoutineForDiagnostics
                && shopping.LastVisitOutcome == ShoppingVisitOutcome.Abandoned)
            {
                List<GameplayOutcomeSnapshot> cancellations = ReadEntityOutcomes("character", command.actorId)
                    .Where(value => !beforeMealOutcomeKeys.Contains(OutcomeKey(value))
                        && value.outcomeTypeId == "service.facility-visited"
                        && value.status == GameplayOutcomeStatus.Cancelled
                        && value.participants.Any(participant => participant.entityKindId == "character"
                            && participant.entityId == command.actorId && participant.roleId == "actor")
                        && value.subjects.Any(subject => subject.entityKindId == "facility" && subject.entityId == command.facilityId)
                        && value.facts.Any(fact => fact.factId == MigratedProducerOutcomeIds.OutcomeStatusFact.Value
                            && fact.value == CharacterFacilityUseOutcomeStatus.Cancelled.ToString())
                        && value.facts.Any(fact => fact.factId == MigratedProducerOutcomeIds.ReasonFact.Value
                            && (fact.value == "meal-supply-retry" || fact.value == "meal-no-longer-needed"))).ToList();
                if (cancellations.Count > 0)
                {
                    Require(cancellations.Count == 1, "Natural meal cancellation has ambiguous actor/operation ownership.");
                    string operation = cancellations[0].facts.Single(fact =>
                        fact.factId == MigratedProducerOutcomeIds.OperationFact.Value).value;
                    string prefix = $"facility-visit:facility={command.facilityId}:use=";
                    long sequence = 0;
                    Require(operation.StartsWith(prefix, StringComparison.Ordinal)
                            && long.TryParse(operation.Substring(prefix.Length), out sequence) && sequence > 0
                            && (!command.activeUseObserved || command.useOperationId == operation),
                        "Natural meal cancellation is not the exact retired facility-use operation.");
                    string reason = cancellations[0].facts.Single(fact =>
                        fact.factId == MigratedProducerOutcomeIds.ReasonFact.Value).value;
                    if (reason == "meal-no-longer-needed")
                    {
                        // A real primitive/other meal can satisfy the actor while
                        // this registered visit is pending. Keep this use Cancelled;
                        // only a NEW, earlier physical-consumption outcome may
                        // discharge nutrition preparation, never a need value alone.
                        command.supersedingPhysicalMealOutcomes = ReadEntityOutcomes("character", command.actorId)
                            .Where(value => !beforeMealOutcomeKeys.Contains(OutcomeKey(value))
                                && value.runId == cancellations[0].runId && value.sequence < cancellations[0].sequence
                                && IsPhysicalMealConsumptionWitness(value, command.actorId)).ToList();
                        Require(command.supersedingPhysicalMealOutcomes.Count > 0,
                            "Meal-no-longer-needed cancellation has no new earlier physical meal witness.");
                        command.actualMealSupersededUse = true;
                    }
                    command.useOperationId = operation;
                    command.useOperationSequence = sequence;
                    command.publishedOutcomes = cancellations;
                    command.retryableMealSupplyCancelled = reason == "meal-supply-retry";
                    command.exactCancelledUseParentBound = true;
                    command.visitTerminalStatus = CharacterFacilityUseOutcomeStatus.Cancelled.ToString();
                    command.visitTerminalReason = reason;
                    command.usesAfter = facility.FacilityState.completedUses;
                    string terminal = command.retryableMealSupplyCancelled ? "supply-cancelled" : "superseded";
                    RecordMealExecution(terminal);
                    string checkpoint = string.IsNullOrEmpty(command.activeUseCheckpointFile)
                        ? $"training-active-world-{report.commands.Count + 1:D3}" : Path.GetFileNameWithoutExtension(command.activeUseCheckpointFile);
                    WriteNew(checkpoint + "-" + terminal + ".json", JsonUtility.ToJson(command, true));
                    yield break;
                }
            }
            FacilityUseExecutionState use = facility.FindActiveUse(command.actorId);
            if (use != null)
            {
                Require(use.IsValid(facility.FacilityState.nextUseOperationSequence, out string error)
                        && use.actionType == command.actionType && use.actionBranch == command.actionBranch,
                    "Actual training active-use identity/save invariant failed: " + error);
                Require(!command.activeUseObserved || command.useOperationId == use.operationId
                        && command.useOperationSequence == use.operationSequence,
                    "Selected visit changed its already observed operation identity.");
                command.activeUseObserved = true;
                command.useOperationSequence = use.operationSequence;
                command.useOperationId = use.operationId;
                if (!command.activeUseOfficialSaveCaptured)
                {
                    if (string.IsNullOrEmpty(command.activeUseCheckpointFile))
                        command.activeUseCheckpointFile = $"training-active-world-{report.commands.Count + 1:D3}.json";
                    WriteNew(command.activeUseCheckpointFile, saves.ToJson(saves.Capture()));
                    command.activeUseOfficialSaveCaptured = true;
                }
            }
            bool selectedStillOwned = ReferenceEquals(actor.Brain.bestAction, committed)
                && !actor.Brain.LastActionFailure.HasFailure;
            if (!selectedStillOwned) RecordMealExecution("interrupted");
            Require(selectedStillOwned,
            "Actual training action stopped before completion: actor=" + command.actorId
                + "; operation=" + command.useOperationId + "; current=" + actor.Brain.CurrentActionDebugLabel
                + "; detail=" + actor.Brain.CurrentActionPhaseDetail + "; failure=" + actor.Brain.LastActionFailure);
            yield return null;
        }
        if (exactCompletion)
        {
            RecordMealExecution("terminal");
            command.publishedOutcomes = ReadEntityOutcomes("character", command.actorId)
                .Where(value => value.outcomeTypeId == "service.facility-visited"
                    && value.subjects.Any(subject => subject.entityKindId == "facility" && subject.entityId == command.facilityId)
                    && value.facts.Any(fact => fact.factId == MigratedProducerOutcomeIds.OperationFact.Value
                        && fact.value == command.useOperationId)).ToList();
            command.exactUseParentBound = command.publishedOutcomes.Count == 1
                && command.publishedOutcomes[0].status == GameplayOutcomeStatus.Succeeded
                && command.publishedOutcomes[0].facts.Any(value => value.factId == MigratedProducerOutcomeIds.OutcomeStatusFact.Value
                    && value.value == CharacterFacilityUseOutcomeStatus.Completed.ToString());
        }
        Require(command.activeUseObserved && command.activeUseOfficialSaveCaptured
                && (exactCompletion ? OwnMealUseCompleted() && command.exactUseParentBound
                    && facility.FacilityState.completedUses >= command.usesBefore + 1
                    : facility.FacilityState.completedUses == command.usesBefore + 1),
            "Actual registered visit did not complete its exact operation with an official in-use checkpoint. "
                + $"actor={command.actorId};operation={command.useOperationId};uses={command.usesBefore}->{facility.FacilityState.completedUses};"
                + $"phase={actor.Brain.CurrentActionPhase};outcome={shopping.LastVisitOutcome}");
        // Let the production shopping coroutine publish its own terminal event.
        Require(shopping.LastVisitOutcome == ShoppingVisitOutcome.Completed,
            "Actual training visitor did not report a completed use.");
        command.usesAfter = facility.FacilityState.completedUses;
    }

    private static bool IsPhysicalMealConsumptionWitness(GameplayOutcomeSnapshot value, string actorId)
    {
        bool HasFact(string id) => value.facts.Any(fact => fact.factId == id
            && !string.IsNullOrWhiteSpace(fact.value) && fact.value != "none");
        return value.producerId == "ledger.survival.meal-consumed"
            && value.outcomeTypeId == "survival.meal-consumed" && value.status == GameplayOutcomeStatus.Succeeded
            && value.lifecycle == GameplayOutcomeRecordLifecycle.PublishedAcknowledged && value.publishedRevision > 0
            && value.participants.Any(participant => participant.entityKindId == "character"
                && participant.entityId == actorId && participant.roleId == "actor")
            && HasFact("physical-commit-id") && HasFact("source-stack-ids")
            && value.metrics.Any(metric => metric.metricId == "quantity" && metric.value > 0)
            && value.metrics.Count(metric => metric.metricId == "hunger-before") == 1
            && value.metrics.Count(metric => metric.metricId == "hunger-after") == 1
            && value.metrics.Single(metric => metric.metricId == "hunger-after").value
                > value.metrics.Single(metric => metric.metricId == "hunger-before").value;
    }

    public static void RunMealCancellationWitnessFromCommandLine()
    {
        Debug.Log("FIELD_MEAL_NUTRITION_AUTHORITY_PASS: " + SurvivalDebugScenarios.RunPhysicalMealAuthorityFocused());
        var meal = new GameplayOutcomeSnapshot
        {
            producerId = "ledger.survival.meal-consumed", outcomeTypeId = "survival.meal-consumed",
            status = GameplayOutcomeStatus.Succeeded, lifecycle = GameplayOutcomeRecordLifecycle.PublishedAcknowledged,
            publishedRevision = 1,
            participants = new() { new() { entityKindId = "character", entityId = "actor", roleId = "actor" } },
            facts = new() { new() { factId = "physical-commit-id", value = "commit" }, new() { factId = "source-stack-ids", value = "stack" } },
            metrics = new() { new() { metricId = "quantity", value = 1 }, new() { metricId = "hunger-before", value = 25 }, new() { metricId = "hunger-after", value = 65 } }
        };
        Require(IsPhysicalMealConsumptionWitness(meal, "actor"), "Valid physical meal witness rejected.");
        Require(!IsPhysicalMealConsumptionWitness(meal, "other"), "Another actor's meal accepted.");
        meal.status = GameplayOutcomeStatus.Cancelled;
        Require(!IsPhysicalMealConsumptionWitness(meal, "actor"), "Cancelled meal accepted as consumption.");
        meal.status = GameplayOutcomeStatus.Succeeded;
        meal.metrics[0].value = 0;
        Require(!IsPhysicalMealConsumptionWitness(meal, "actor"), "Zero-quantity meal accepted.");
        meal.metrics[0].value = 1;
        meal.facts[0].value = "none";
        Require(!IsPhysicalMealConsumptionWitness(meal, "actor"), "Meal without physical commit accepted.");
        meal.facts[0].value = "commit";
        meal.facts[1].value = "";
        Require(!IsPhysicalMealConsumptionWitness(meal, "actor"), "Meal without source stack accepted.");
        meal.facts[1].value = "stack";
        meal.metrics[2].value = meal.metrics[1].value;
        Require(!IsPhysicalMealConsumptionWitness(meal, "actor"), "Meal without recovery accepted.");
        Debug.Log("NUTRITION_MEAL_CANCELLATION_WITNESS_PASS: physical commit, actor, quantity and recovery required; cancelled use stays cancelled.");
    }

    private void DispatchExistingTraitTargetForIsolatedWitness()
    {
        traitRuntime.Tick();
        CharacterAcquiredTraitPendingRequestState[] requests = owner.Progression.CaptureAcquiredTraitState()
            .pendingRequests.Where(value => value != null && value.manifestationMilestone == 3
                && (traitDispatch == null || value.requestKey == traitDispatch.pending.requestKey)).ToArray();
        Require(requests.Length <= 1, "Actual trait intake supplied multiple target request identities.");
        if (requests.Length == 1 && traitRuntime.TryCapturePendingModuleDispatch(requests[0].requestKey,
                out CharacterProgression progression, out CharacterAcquiredTraitPendingRequestState pending,
                out _, out _))
        {
            Require(ReferenceEquals(progression, owner.Progression) && pending.requestKey == requests[0].requestKey,
                "Actual trait transport is not bound to the current restored owner/request.");
            queue.TryDispatchQueuedRequestForIsolatedCapture(requests[0].requestKey);
        }
        else if (requests.Length == 1 && (queue.QueuedCount > 0 || queue.RunningCount > 0))
        {
            // Capture has no model responses. Two other actors can occupy the
            // trait service's admission slots before the target even reaches
            // the queue (notably "owner", which sorts after staff IDs). Release
            // only transient transport via the existing cancellation boundary;
            // never edit pending requests or bypass the production concurrency cap.
            string targetBefore = JsonUtility.ToJson(requests[0]);
            int queuedBefore = queue.QueuedCount, runningBefore = queue.RunningCount;
            CancelOutstandingTransport();
            var targetAfter = owner.Progression.CaptureAcquiredTraitState().pendingRequests
                .Single(value => value.requestKey == requests[0].requestKey);
            Require(JsonUtility.ToJson(targetAfter) == targetBefore,
                "Isolated non-target transport cancellation changed the target trait request.");
            WriteNew($"trait-admission-relief-{report.cancelledQueuedRequests + report.cancelledRunningRequests:D3}.json",
                new JObject
                {
                    ["targetRequestKey"] = requests[0].requestKey,
                    ["queuedBefore"] = queuedBefore, ["runningBefore"] = runningBefore,
                    ["targetPendingUnchanged"] = true,
                    ["reason"] = "Response-free isolated capture: target has not been admitted; release competing transient transport only."
                }.ToString());
        }
    }

    private void DispatchExistingSkillTargetForIsolatedWitness()
    {
        generation.Tick();
        string[] targetIds = generation.CapturePendingModuleDispatchIdsForOwner(owner.Identity.PersistentId)
            .Where(id => generation.TryCapturePendingModuleDispatch(id,
                    out CharacterProgression progression, out CharacterSkillDraft draft, out _, out _)
                && ReferenceEquals(progression, owner.Progression)
                && draft.kind == CharacterSkillKind.Active && draft.unlockLevel == targetUnlock
                && (firstDispatch == null || draft.requestKey == firstDispatch.pending.requestKey))
            .ToArray();
        Require(targetIds.Length <= 1, "Actual skill intake supplied multiple target transport identities.");
        if (targetIds.Length == 1)
        {
            if (!skillSubmittedPromptCaptured)
            {
                Require(generation.TryCapturePendingModuleDispatch(targetIds[0],
                        out CharacterProgression progression, out CharacterSkillDraft draft,
                        out string prompt, out bool batch)
                    && ReferenceEquals(progression, owner.Progression),
                    "Target submission vanished before capture serialization.");
                WriteNew("skill-submitted-prompt.txt", prompt);
                WriteNew("skill-submitted-pending.json", JsonUtility.ToJson(draft, true));
                WriteNew("skill-submitted-transport.json", new JObject
                {
                    ["correlationId"] = targetIds[0], ["batch"] = batch,
                    ["ownerId"] = owner.Identity.PersistentId, ["networkSent"] = false,
                    ["scope"] = "submitted-request-before-wire-serialization; not a dispatch success"
                }.ToString());
                skillSubmittedPromptCaptured = true;
            }
            queue.TryDispatchQueuedRequestForIsolatedCapture(targetIds[0]);
        }
    }

    private void ResumeOnlyExistingTarget()
    {
        CharacterSkillDraft target = owner.Progression.Drafts.Single(value => value != null
            && value.kind == CharacterSkillKind.Active && value.unlockLevel == targetUnlock
            && !value.isReady && !value.permanentlyChosen);
        string before = JsonUtility.ToJson(target.Clone());
        bool submitted = target.requestSubmitted;
        CancelOutstandingTransport();
        CharacterSkillDraft after = target.Clone();
        after.requestSubmitted = submitted;
        Require(JsonUtility.ToJson(after) == before, "Public cancellation changed target evidence, offers or request identity.");
        generation.RequestDraft(owner.Progression, target);
    }

    private void CancelOutstandingTransport()
    {
        foreach (CharacterProgression progression in UnityEngine.Object.FindObjectsByType<CharacterProgression>(FindObjectsSortMode.None))
        {
            report.suspendedSkillDrafts += progression.Drafts.Count(value => value != null && value.requestSubmitted);
            generation.CancelRequests(progression);
        }
        // Other profiles share this queue. With no model responses in capture
        // mode their older requests can occupy both slots indefinitely relative
        // to this witness window. Cancel transport work, not gameplay facts.
        report.cancelledQueuedRequests += queue.QueuedCount;
        report.cancelledRunningRequests += queue.RunningCount;
        queue.AbortAllForDebug();
        Require(queue.QueuedCount == 0 && queue.RunningCount == 0,
            "Shared queue cancellation left outstanding transport work.");
    }

    private void CaptureDispatch(LocalLlmCapturedRequest captured)
    {
        report.dispatchCount++;
        try
        {
            Require(report.dispatches.Count < 64, "Unexpected unbounded dispatches during the controlled witness.");
            report.dispatches.Add(new DispatchObservation
            {
                profileId = captured.ProfileId, correlationId = captured.CorrelationId,
                attemptIndex = captured.AttemptIndex, replaying = replaying,
                duringControlledAction = ownsLease || controlledFacilityCommandActive
            });
            Require(captured.CaptureOnly && !captured.NetworkSent, "Unexpected real network dispatch.");
            if (originalSkillPendingDiagnostics)
            {
                CaptureOriginalSkillPendingDispatch(captured);
                return;
            }
            if (!string.IsNullOrEmpty(skillRecruitmentPreparationOwnerId)
                && captured.ProfileId == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id)
            {
                Require(generation.TryCapturePendingModuleDispatch(captured.CorrelationId,
                        out CharacterProgression preparedOwner, out _, out string preparedPrompt, out _)
                        && preparedOwner.Actor.Identity.PersistentId == skillRecruitmentPreparationOwnerId
                        && preparedPrompt == captured.Prompt,
                    "Visitor preparation dispatch differs from its exact production owner/prompt.");
                Require(skillRecruitmentPreparationCaptures.Add(captured.CorrelationId),
                    "Visitor preparation dispatched a duplicate transport identity.");
                return;
            }
            if (sourceV9TraitReplay)
            {
                if (captured.ProfileId
                    == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id)
                {
                    nonTargetSkillTransports.Add(captured.CorrelationId);
                    return;
                }
                if (captured.ProfileId
                    == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id)
                {
                    if (!string.Equals(captured.CorrelationId,
                            traitReplayInput?.RequestId,
                            StringComparison.Ordinal))
                    {
                        nonTargetTraitTransports.Add(captured.CorrelationId);
                        return;
                    }
                    CaptureTraitReplayDispatch(captured);
                    return;
                }
            }
            if (sourceV9SkillReplay
                && captured.ProfileId
                    == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id)
            {
                nonTargetTraitTransports.Add(captured.CorrelationId);
                return;
            }
            if (sourceV9SkillReplay
                && captured.ProfileId
                    == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id)
            {
                if (!string.Equals(captured.CorrelationId,
                    skillReplayInput?.RequestId, StringComparison.Ordinal))
                {
                    nonTargetSkillTransports.Add(captured.CorrelationId);
                    return;
                }
                CaptureSkillReplayDispatch(captured);
                return;
            }
            if (equipmentPhase)
            {
                if (captured.ProfileId
                    == LocalLlmRequestProfiles
                        .CharacterSkillModuleSelection.Id)
                {
                    nonTargetSkillTransports.Add(captured.CorrelationId);
                }
                if (captured.ProfileId
                    == LocalLlmRequestProfiles
                        .AcquiredTraitModuleSelection.Id)
                {
                    nonTargetTraitTransports.Add(captured.CorrelationId);
                }
                if (captured.ProfileId
                    == LocalLlmRequestProfiles
                        .EquipmentEvolutionModuleSelection.Id)
                {
                    if (sourceV9EquipmentReplay)
                        CaptureEquipmentReplayDispatch(captured);
                    else
                        CaptureEquipmentDispatch(captured);
                }
                return;
            }
            if (facilityPhase)
            {
                if (sourceV9FacilityIntake && facilityVisitorResponseProviderConfigured
                    && captured.ProfileId == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id
                    && generation.TryCapturePendingModuleDispatch(captured.CorrelationId,
                        out CharacterProgression visitorOwner, out _, out _, out _)
                    && visitorOwner.Actor.Identity.PersistentId == facilityVisitorPreparationOwnerId)
                    return; // The explicit preparation provider owns this reply.
                if (captured.ProfileId == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id)
                    nonTargetSkillTransports.Add(captured.CorrelationId);
                if (captured.ProfileId == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id)
                    nonTargetTraitTransports.Add(captured.CorrelationId);
                if (captured.ProfileId == LocalLlmRequestProfiles.FacilityEvolutionModuleSelection.Id)
                {
                    if (sourceV9FacilityReplay)
                        CaptureFacilityReplayDispatch(captured);
                    else
                        CaptureFacilityDispatch(captured);
                }
                return;
            }
            if (traitPhase)
            {
                if (captured.ProfileId == LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id)
                    nonTargetSkillTransports.Add(captured.CorrelationId);
                if (captured.ProfileId == LocalLlmRequestProfiles.AcquiredTraitModuleSelection.Id) CaptureTraitDispatch(captured);
                return;
            }
            if (captured.ProfileId != LocalLlmRequestProfiles.CharacterSkillModuleSelection.Id) return;
            Require(GameplayModuleDispatchEligibilityCapture.TryCaptureSkill(generation, captured.CorrelationId,
                out CharacterProgression progression, out CharacterSkillDraft pending, out string prompt, out bool batch,
                out GameplayModuleDispatchEligibilityWitness eligibility),
                "Queue dispatch has no unique live pending owner.");
            Require(prompt == captured.Prompt, "Queued prompt differs from the submitting service.");
            if (progression.Actor.Identity.PersistentId != owner.Identity.PersistentId || pending.unlockLevel != targetUnlock)
            {
                nonTargetSkillTransports.Add(captured.CorrelationId);
                return;
            }
            // A registered visitor action can unlock a draft before its parent
            // facility terminal is published. Observe that same pending after
            // the completed command witness, just as for an external lease.
            if (ownsLease || controlledFacilityCommandActive) return;
            Require(ReferenceEquals(progression, owner.Progression) && ReferenceEquals(progression.Actor, owner),
                "Skill dispatch was bound to a stale actor instance with the same persistent ID.");
            if (replaying)
            {
                WriteNew("replayed-wire.json", captured.WireBodyJson);
                GameplayModuleDispatchEligibilityCapture.RequireSame(firstDispatch.dispatchEligibility, eligibility);
                Require(captured.CorrelationId == firstDispatch.correlationId && prompt == capturedPrompt
                    && captured.WireBodyJson == capturedWire, "Restored pending regenerated or changed the wire request.");
                report.wireReplayExact = true;
                return;
            }
            Require(firstDispatch == null, "Duplicate initial target dispatch.");
            WriteNew("skill-selected-pending.json", JsonUtility.ToJson(pending, true));
            WriteNew("skill-selected-context.json", JsonUtility.ToJson(
                pending.RestoreRequestPublicContext().CaptureSnapshot(), true));
            CommandWitness trigger = report.commands.LastOrDefault(value =>
                value.actorId == owner.Identity.PersistentId && value.command == "training-facility.interact"
                && value.levelBefore < targetUnlock && value.levelAfter >= targetUnlock
                && value.usesAfter == value.usesBefore + 1
                && value.publishedOutcomes.Any(outcome => outcome.outcomeTypeId == "service.facility-visited"
                    && outcome.participants.Any(participant => participant.entityKindId == "character"
                        && participant.entityId == value.actorId && participant.roleId == "actor"
                        && participant.participationKind == GameplayParticipationKind.Direct)));
            report.skillTriggerCommandBound = trigger != null
                && !intakeInitialSkillRequestKeys.Contains(pending.requestKey);
            Require(report.skillTriggerCommandBound,
                "Skill request lacks a new authored unlock caused by an observed completed training command.");
            report.skillTriggerCommandIndex = report.commands.IndexOf(trigger) + 1;
            // CreateDraft selects up to 24 available owner-history outcomes, not
            // necessarily the terminal event that caused this level unlock.
            // Keep the fresh fixture's evidence coverage requirement; resumed
            // history is independently checked against the actual current ledger.
            var evidence = ReadBoundEvidence(pending.outcomeEvidenceBindings,
                requireFacilityCompletion: !report.resumedExistingTraitRequest);
            report.skillSelectedEvidenceContainsFacilityUse = evidence.Any(
                value => value.outcomeTypeId == "service.facility-visited");
            WriteNew("skill-trigger-witness.json", new JObject
            {
                ["schema"] = "dungeonstory-skill-training-trigger-witness@1",
                ["actorId"] = owner.Identity.PersistentId,
                ["requestKey"] = pending.requestKey,
                ["unlockLevel"] = targetUnlock,
                ["initialRequestAbsent"] = true,
                ["commandFile"] = $"command-{report.skillTriggerCommandIndex:D3}.json",
                ["levelBefore"] = trigger.levelBefore,
                ["levelAfter"] = trigger.levelAfter,
                ["commandOutcomeKeys"] = new JArray(trigger.publishedOutcomes.Select(OutcomeKey)),
                ["causalProducerPath"] = "training interaction -> progression level unlock -> EnsureUnlockedDrafts/CreateDraft",
                ["selectedEvidenceOrigin"] = "current published owner history",
                ["selectedOutcomeKeys"] = new JArray(evidence.Select(OutcomeKey)),
                ["selectedEvidenceContainsFacilityUse"] = report.skillSelectedEvidenceContainsFacilityUse
            }.ToString());
            DungeonGameSaveData checkpoint = saves.Capture();
            report.checkpointSections = checkpoint.sections.Count;
            Require(report.checkpointSections == report.registeredSections, "Incomplete dispatch checkpoint.");
            checkpointJson = saves.ToJson(checkpoint);
            capturedPrompt = prompt;
            capturedWire = captured.WireBodyJson;
            firstDispatch = new DispatchWitness
            {
                dispatchEligibility = eligibility,
                profileId = captured.ProfileId, correlationId = captured.CorrelationId, model = captured.Model,
                schemaVersion = captured.SchemaVersion, schemaHash = captured.SchemaHash,
                wireSha256 = Hash(captured.CopyWireBodyBytes()), checkpointSha256 = Hash(Encoding.UTF8.GetBytes(checkpointJson)),
                frame = Time.frameCount, absoluteHour = calendar.AbsoluteHour, batch = batch,
                ownerId = progression.Actor.Identity.PersistentId, pending = pending, evidence = evidence
            };
            WriteNew("dispatch-world.json", checkpointJson);
            WriteNew("dispatch-wire.json", capturedWire);
            WriteNew("dispatch-prompt.txt", capturedPrompt);
            WriteNew("dispatch-witness.json", JsonUtility.ToJson(firstDispatch, true));
        }
        catch (Exception exception)
        {
            callbackFailure ??= exception;
            throw;
        }
    }

    private void CaptureEquipmentDispatch(LocalLlmCapturedRequest captured)
    {
        Require(GameplayModuleDispatchEligibilityCapture.TryCaptureEquipment(equipmentPresentation,
                captured.CorrelationId,
                out CombatEquipmentInstance equipmentInstance,
                out EvolutionNode node,
                out EquipmentEvolutionPresentationRequest pending,
                out string prompt, out GameplayModuleDispatchEligibilityWitness eligibility),
            "Equipment queue dispatch has no unique live pending owner.");
        if (report.equipmentCombat == null
            || !string.Equals(
                equipmentInstance.instanceId,
                report.equipmentCombat.equipmentInstanceId,
                StringComparison.Ordinal))
        {
            return;
        }
        Require(captured.Prompt == prompt
                && string.Equals(
                    pending.targetPersistentId,
                    equipmentInstance.instanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    pending.attunementOwnerPersistentId,
                    report.equipmentCombat.actorId,
                    StringComparison.Ordinal),
            "Equipment dispatch uses a different owner, instance or prompt.");
        if (replaying)
        {
            WriteNew("equipment-replayed-wire.json", captured.WireBodyJson);
            GameplayModuleDispatchEligibilityCapture.RequireSame(equipmentDispatch?.dispatchEligibility, eligibility);
            Require(equipmentDispatch != null
                    && captured.CorrelationId
                        == equipmentDispatch.correlationId
                    && prompt == equipmentPrompt
                    && captured.WireBodyJson == equipmentWire,
                "Restored equipment regenerated or changed its prompt or wire.");
            List<GameplayOutcomeSnapshot> restoredOutcomes = ReadAllOutcomes();
            List<GameplayOutcomeSnapshot> restoredAttacks = equipmentDispatch.evidence
                .Select(original => restoredOutcomes.Single(value =>
                    value.runId == original.runId && value.sequence == original.sequence))
                .ToList();
            List<EquipmentEvolutionFormulaEvidenceRecord> restoredUsage = equipmentDispatch.formulaEvidence
                .Select(original => equipmentInstance.evolution.formulaEvidence.Single(value =>
                    value.evidenceId == original.evidenceId))
                .ToList();
            Require(restoredAttacks.Select(JsonUtility.ToJson)
                        .SequenceEqual(equipmentDispatch.evidence.Select(JsonUtility.ToJson))
                    && restoredUsage.Select(JsonUtility.ToJson)
                        .SequenceEqual(equipmentDispatch.formulaEvidence.Select(JsonUtility.ToJson)),
                "Restored equipment changed original attack or usage evidence.");
            RequireEquipmentAttackOrigins(restoredUsage, restoredAttacks);
            report.equipmentWireReplayExact = true;
            return;
        }

        Require(equipmentDispatch == null,
            "Duplicate initial equipment dispatch.");
        var actual = ReadAllOutcomes();
        var exactEvidence = new List<GameplayOutcomeSnapshot>();
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in
                  pending.gameplayOutcomeEvidence)
        {
            Require(binding.sourceKind == "exact",
                "Equipment witness requires exact current-world outcomes.");
            GameplayOutcomeSnapshot row = actual.SingleOrDefault(value =>
                value.runId == binding.outcomeRunId
                && value.sequence == binding.outcomeSequence);
            Require(row != null
                    && row.deliveryMetadataVersion == 1
                    && row.publishedRevision > 0
                    && row.recordedAbsoluteHour <= calendar.AbsoluteHour
                    && row.absoluteDay <= calendar.Day,
                "Equipment pending refers to an unpublished or non-current outcome.");
            exactEvidence.Add(row);
        }
        HashSet<string> exactIds = pending.gameplayOutcomeEvidence
            .Select(value => value.publicFactId)
            .ToHashSet(StringComparer.Ordinal);
        List<EquipmentEvolutionFormulaEvidenceRecord> formulaEvidence = pending.evidenceIds
            .Where(id => !exactIds.Contains(id))
            .Select(id => equipmentInstance.evolution.formulaEvidence.SingleOrDefault(value =>
                value != null && string.Equals(value.evidenceId, id, StringComparison.Ordinal)))
            .ToList();
        Require(formulaEvidence.Count > 0
                && formulaEvidence.All(value => value != null
                    && value.originalEvent != null
                    && value.originalEvent.narrativeContext != null
                    && value.evidenceId == value.originalEvent.evidenceId
                    && value.eventGroupKey == value.originalEvent.eventId
                    && (value.originalEvent.eventId is "combat:attack" or "combat:hit"
                        or "combat:failed-attack")
                    && value.originalEvent.actorId == report.equipmentCombat.actorId
                    && value.originalEvent.narrativeContext.actorId
                        == report.equipmentCombat.actorId
                    && value.originalEvent.narrativeContext.counterpartyId
                        == report.equipmentCombat.targetId
                    && value.originalEvent.narrativeContext.usedObjectId
                        == equipmentInstance.instanceId),
            "Equipment pending does not bind its actual combat usage evidence.");
        List<GameplayOutcomeSnapshot> attackOutcomes = actual.Where(value =>
                value != null
                && value.outcomeTypeId == "combat.attack-resolved"
                && value.deliveryMetadataVersion == 1
                && value.publishedRevision > 0
                && value.recordedAbsoluteHour <= calendar.AbsoluteHour
                && value.absoluteDay <= calendar.Day
                && (value.facts ?? new List<GameplayOutcomeFactSnapshot>()).Any(fact =>
                    fact != null && fact.factId == "weapon-instance-id"
                    && fact.value == equipmentInstance.instanceId)
                && (value.participants ?? new List<GameplayOutcomeParticipantSnapshot>()).Any(participant =>
                    participant != null && participant.roleId == "actor"
                    && participant.entityId == report.equipmentCombat.actorId)
                && value.participants.Any(participant =>
                    participant != null && participant.roleId == "target"
                    && participant.entityId == report.equipmentCombat.targetId))
            .ToList();
        Require(attackOutcomes.Count >= 30,
            "Equipment request has no matching actual combat outcome stream.");
        RequireEquipmentAttackOrigins(formulaEvidence, attackOutcomes);
        DungeonGameSaveData checkpoint = saves.Capture();
        report.equipmentCheckpointSections = checkpoint.sections.Count;
        Require(report.equipmentCheckpointSections == report.registeredSections,
            "Incomplete equipment dispatch checkpoint.");
        equipmentCheckpointJson = saves.ToJson(checkpoint);
        equipmentPrompt = prompt;
        equipmentWire = captured.WireBodyJson;
        equipmentDispatch = new EquipmentDispatchWitness
        {
            dispatchEligibility = eligibility,
            profileId = captured.ProfileId,
            correlationId = captured.CorrelationId,
            model = captured.Model,
            schemaVersion = captured.SchemaVersion,
            schemaHash = captured.SchemaHash,
            wireSha256 = Hash(captured.CopyWireBodyBytes()),
            checkpointSha256 = Hash(
                Encoding.UTF8.GetBytes(equipmentCheckpointJson)),
            frame = Time.frameCount,
            absoluteHour = calendar.AbsoluteHour,
            ownerId = pending.attunementOwnerPersistentId,
            equipmentInstanceId = equipmentInstance.instanceId,
            targetId = report.equipmentCombat.targetId,
            node = node,
            pending = pending,
            evidence = attackOutcomes,
            formulaEvidence = formulaEvidence.Select(value => value.Clone()).ToList()
        };
        WriteNew("equipment-dispatch-world.json", equipmentCheckpointJson);
        WriteNew("equipment-dispatch-wire.json", equipmentWire);
        WriteNew("equipment-dispatch-prompt.txt", equipmentPrompt);
        WriteNew(
            "equipment-dispatch-witness.json",
            JsonUtility.ToJson(equipmentDispatch, true));
        WriteNew(
            "equipment-selected-pending.json",
            JsonUtility.ToJson(pending, true));
    }

    private static void RequireEquipmentAttackOrigins(
        IReadOnlyList<EquipmentEvolutionFormulaEvidenceRecord> usage,
        IReadOnlyList<GameplayOutcomeSnapshot> attacks)
    {
        var origins = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameplayOutcomeSnapshot attack in attacks)
        {
            List<GameplayOutcomeProvenanceReferenceSnapshot> ids = attack.provenance.Where(value =>
                value.kindId == "domain-commit").ToList();
            Require(ids.Count == 1 && !string.IsNullOrWhiteSpace(ids[0].value)
                    && origins.Add(ids[0].value),
                "Equipment attack stream has a missing or duplicate operation identity.");
        }
        var usedOrigins = new HashSet<string>(StringComparer.Ordinal);
        foreach (EquipmentEvolutionFormulaEvidenceRecord receipt in usage)
        {
            string origin = receipt.originalEvent.narrativeContext.eventInstanceId;
            Require(!string.IsNullOrWhiteSpace(origin) && origins.Contains(origin)
                    && usedOrigins.Add(origin),
                "Equipment usage has no unique exact original attack: " + receipt.evidenceId);
        }
    }

    private void CaptureEquipmentReplayDispatch(LocalLlmCapturedRequest captured)
    {
        Require(equipmentReplayInput != null,
            "Equipment replay dispatch occurred before its fixed input was loaded.");
        Require(equipmentReplayCapture == null,
            "Equipment replay dispatched the same fixed response more than once.");
        Require(string.Equals(
                    captured.ProfileId,
                    equipmentReplayInput.ProfileId,
                    StringComparison.Ordinal)
                && string.Equals(
                    captured.CorrelationId,
                    equipmentReplayInput.RequestId,
                    StringComparison.Ordinal)
                && string.Equals(
                    GameplayModuleSelectionCanonicalJson.Sha256Prefixed(
                        captured.CopyWireBodyBytes()),
                    equipmentReplayInput.SourceWireSha256,
                    StringComparison.Ordinal),
            "Equipment replay changed the source-v9 profile, correlation or wire bytes.");
        Require(equipmentPresentation.TryCapturePendingModuleDispatch(
                captured.CorrelationId,
                out CombatEquipmentInstance equipmentInstance,
                out EvolutionNode node,
                out EquipmentEvolutionPresentationRequest pending,
                out string prompt),
            "Equipment replay dispatch has no unique live pending owner.");
        Require(string.Equals(
                    equipmentInstance.instanceId,
                    equipmentReplayInput.EquipmentInstanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    pending.attunementOwnerPersistentId,
                    equipmentReplayInput.OwnerId,
                    StringComparison.Ordinal)
                && string.Equals(
                    node.nodeId,
                    equipmentReplayInput.FormulaId,
                    StringComparison.Ordinal)
                && string.Equals(prompt, captured.Prompt, StringComparison.Ordinal),
            "Equipment replay dispatch owner, equipment, node or prompt differs from source-v9.");
        equipmentReplayCapture = captured;
        WriteNew("u4-replay-wire.json", captured.WireBodyJson);
        WriteNew("u4-replay-prompt.txt", captured.Prompt);
    }

    private void CaptureSkillReplayDispatch(LocalLlmCapturedRequest captured)
    {
        Require(skillReplayInput != null,
            "Skill replay dispatch occurred before its fixed input was loaded.");
        Require(skillReplayCapture == null,
            "Skill replay dispatched the same fixed response more than once.");
        Require(string.Equals(captured.ProfileId, skillReplayInput.ProfileId,
                    StringComparison.Ordinal)
                && string.Equals(captured.CorrelationId,
                    skillReplayInput.RequestId, StringComparison.Ordinal)
                && string.Equals(
                    GameplayModuleSelectionCanonicalJson.Sha256Prefixed(
                        captured.CopyWireBodyBytes()),
                    skillReplayInput.SourceWireSha256,
                    StringComparison.Ordinal),
            "Skill replay changed the source-v9 profile, correlation or wire bytes.");
        Require(generation.TryCapturePendingModuleDispatch(
                captured.CorrelationId,
                out CharacterProgression progression,
                out CharacterSkillDraft pending,
                out string prompt,
                out bool batch),
            "Skill replay dispatch has no unique live pending owner.");
        Require(batch
                && ReferenceEquals(progression, owner.Progression)
                && ReferenceEquals(progression.Actor, owner)
                && string.Equals(owner.Identity.PersistentId,
                    skillReplayInput.OwnerId, StringComparison.Ordinal)
                && string.Equals(pending.requestKey,
                    skillReplayInput.RequestKey, StringComparison.Ordinal)
                && string.Equals(prompt, captured.Prompt,
                    StringComparison.Ordinal),
            "Skill replay dispatch owner, draft, batch mode or prompt differs from source-v9.");
        skillReplayCapture = captured;
        WriteNew("u4-replay-wire.json", captured.WireBodyJson);
        WriteNew("u4-replay-prompt.txt", captured.Prompt);
    }

    private void CaptureTraitReplayDispatch(LocalLlmCapturedRequest captured)
    {
        Require(traitReplayInput != null,
            "Trait replay dispatch occurred before its fixed input was loaded.");
        Require(traitReplayCapture == null,
            "Trait replay dispatched the same fixed response more than once. "
                + TraitDiagnostic());
        Require(string.Equals(captured.ProfileId, traitReplayInput.ProfileId,
                    StringComparison.Ordinal)
                && string.Equals(captured.CorrelationId,
                    traitReplayInput.RequestId, StringComparison.Ordinal)
                && string.Equals(
                    GameplayModuleSelectionCanonicalJson.Sha256Prefixed(
                        captured.CopyWireBodyBytes()),
                    traitReplayInput.SourceWireSha256,
                    StringComparison.Ordinal),
            "Trait replay changed the source-v9 profile, correlation or wire bytes.");
        Require(traitRuntime.TryCapturePendingModuleDispatch(
                captured.CorrelationId,
                out CharacterProgression progression,
                out CharacterAcquiredTraitPendingRequestState pending,
                out string prompt,
                out NarrativePublicContextMaterialSnapshot publicContext),
            "Trait replay dispatch has no unique live pending owner.");
        Require(ReferenceEquals(progression, owner.Progression)
                && ReferenceEquals(progression.Actor, owner)
                && publicContext != null
                && string.Equals(owner.Identity.PersistentId,
                    traitReplayInput.OwnerId, StringComparison.Ordinal)
                && string.Equals(pending.requestId,
                    traitReplayInput.TraitRequestId, StringComparison.Ordinal)
                && string.Equals(pending.requestKey,
                    traitReplayInput.RequestId, StringComparison.Ordinal)
                && string.Equals(prompt, captured.Prompt,
                    StringComparison.Ordinal),
            "Trait replay dispatch owner, pending identity or prompt differs from source-v9.");
        traitReplayCapture = captured;
        WriteNew("u4-replay-wire.json", captured.WireBodyJson);
        WriteNew("u4-replay-prompt.txt", captured.Prompt);
    }

    private void CaptureFacilityReplayDispatch(LocalLlmCapturedRequest captured)
    {
        Require(facilityReplayInput != null,
            "Facility replay dispatch occurred before its fixed input was loaded.");
        Require(facilityReplayCapture == null,
            "Facility replay dispatched the same fixed response more than once.");
        Require(string.Equals(captured.ProfileId, facilityReplayInput.ProfileId,
                    StringComparison.Ordinal)
                && string.Equals(captured.CorrelationId,
                    facilityReplayInput.RequestId, StringComparison.Ordinal)
                && string.Equals(
                    GameplayModuleSelectionCanonicalJson.Sha256Prefixed(
                        captured.CopyWireBodyBytes()),
                    facilityReplayInput.SourceWireSha256,
                    StringComparison.Ordinal),
            "Facility replay changed the source-v9 profile, correlation or wire bytes.");
        Require(facilityPresentation.TryCapturePendingModuleDispatch(
                captured.CorrelationId,
                out BuildableObject building,
                out FacilityEvolutionFormulaPresentationPendingSnapshot pending,
                out string prompt),
            "Facility replay dispatch has no unique live pending owner.");
        Require(string.Equals(building.PersistentInstanceId.Value,
                    facilityReplayInput.FacilityId, StringComparison.Ordinal)
                && string.Equals(pending.recipeId,
                    facilityReplayInput.RecipeId, StringComparison.Ordinal)
                && string.Equals(pending.node?.nodeId,
                    facilityReplayInput.FormulaId, StringComparison.Ordinal)
                && string.Equals(
                    FacilityFormulaEvolutionPresentationRuntime.BuildPrompt(
                        building, pending),
                    prompt,
                    StringComparison.Ordinal),
            "Facility replay dispatch owner, recipe, node or prompt differs from source-v9.");
        facilityOwner = building;
        facilityReplayCapture = captured;
        WriteNew("u4-replay-wire.json", captured.WireBodyJson);
        WriteNew("u4-replay-prompt.txt", captured.Prompt);
    }

    private void CaptureFacilityDispatch(LocalLlmCapturedRequest captured)
    {
        Require(GameplayModuleDispatchEligibilityCapture.TryCaptureFacility(facilityPresentation, captured.CorrelationId,
            out BuildableObject building, out FacilityEvolutionFormulaPresentationPendingSnapshot pending, out string prompt,
            out GameplayModuleDispatchEligibilityWitness eligibility),
            "Facility queue dispatch has no unique in-flight live owner.");
        if (building.PersistentInstanceId.Value != facilityOwner.PersistentInstanceId.Value) return;
        Require(ReferenceEquals(building, facilityOwner),
            "Facility dispatch uses a stale owner instance.");
        string expectedTransportPrompt = prompt.Contains(
                NarrativeRequestContext.BeginMarker)
            ? prompt
            : NarrativeCultureStyleCatalog.Create(
                    captured.ProfileId,
                    string.Empty,
                    requireCharacterFact: false,
                    requireMotif: LlmStaticSchemaCatalog.Require(
                        captured.ProfileId).PersistentNarrative)
                .AppendToPrompt(prompt);
        Require(string.Equals(
                captured.Prompt,
                expectedTransportPrompt,
                StringComparison.Ordinal),
            "Facility transport prompt differs from the submitting runtime after queue context composition.");
        if (replaying)
        {
            WriteNew("facility-replayed-wire.json", captured.WireBodyJson);
            GameplayModuleDispatchEligibilityCapture.RequireSame(facilityDispatch?.dispatchEligibility, eligibility);
            Require(captured.CorrelationId == facilityDispatch.correlationId
                && prompt == facilityPrompt && captured.WireBodyJson == facilityWire,
                "Restored facility regenerated or changed its prompt or wire.");
            report.facilityWireReplayExact = true;
            return;
        }
        Require(facilityDispatch == null, "Duplicate initial facility dispatch.");
        string publicUsage = FacilityUsagePublicProjection.BuildJson(pending.node,
            building.GetComponent<FacilityEvolutionStateComponent>().InstanceEvolution);
        if (publicUsage != null)
        {
            string publicContent = (string)JObject.Parse(captured.WireBodyJson)["messages"][1]["content"];
            Require(publicContent.Split('\n').Count(line => line.TrimEnd('\r') == publicUsage) == 1,
                "Actual facility model wire lost or duplicated its frozen usage content.");
            WriteNew("facility-public-usage.json", publicUsage);
        }
        var actual = ReadEntityOutcomes("facility", building.PersistentInstanceId.Value);
        var evidence = new List<GameplayOutcomeSnapshot>();
        foreach (var binding in pending.node.gameplayOutcomeEvidence)
        {
            Require(binding.sourceKind == "exact", "Facility witness requires exact current-world outcomes.");
            var row = actual.SingleOrDefault(value => value.runId == binding.outcomeRunId && value.sequence == binding.outcomeSequence);
            Require(row != null && row.deliveryMetadataVersion == 1 && row.publishedRevision > 0
                && row.recordedAbsoluteHour <= calendar.AbsoluteHour && row.absoluteDay <= calendar.Day,
                "Facility pending refers to an unpublished or non-current owner outcome.");
            evidence.Add(row);
        }
        var commandKeys = report.commands.Where(value => value.facilityId == building.PersistentInstanceId.Value)
            .SelectMany(value => value.publishedOutcomes).Concat(report.facilityObservedUseOutcomes)
            .Select(OutcomeKey).ToHashSet(StringComparer.Ordinal);
        DungeonGameSaveData checkpoint = saves.Capture();
        report.facilityCheckpointSections = checkpoint.sections.Count;
        Require(report.facilityCheckpointSections == report.registeredSections, "Incomplete facility dispatch checkpoint.");
        facilityCheckpointJson = saves.ToJson(checkpoint);
        var commands = report.commands.Select((command, index) => new
            { name = $"command-{index + 1:D3}.json", value = JObject.Parse(JsonUtility.ToJson(command)) })
            .ToDictionary(row => row.name, row => row.value, StringComparer.Ordinal);
        var usageEvidence = LiveFacilityUsageEvidence.Create(building.PersistentInstanceId.Value,
            calendar.AbsoluteHour, JObject.Parse(JsonUtility.ToJson(pending)),
            JObject.Parse(facilityCheckpointJson), commands);
        Require(evidence.Any(value => commandKeys.Contains(OutcomeKey(value))) || usageEvidence.Count > 0,
            "Facility request has no selected evidence bound to an actual facility-use completion.");
        if (usageEvidence.Count > 0)
        {
            int checks = LiveFacilityUsageEvidence.VerifyCommandBoundaries(building.PersistentInstanceId.Value,
                calendar.AbsoluteHour, JObject.Parse(JsonUtility.ToJson(pending)),
                JObject.Parse(facilityCheckpointJson), commands, JArray.FromObject(usageEvidence));
            WriteNew("facility-usage-origin-checks.json", new JObject
            {
                ["checksPassed"] = checks, ["selectedOrigins"] = usageEvidence.Count,
                ["fullRequestLineageVerified"] = false, ["priorHistoryVerified"] = false,
                ["trainingEligible"] = false
            }.ToString());
        }
        facilityPrompt = prompt;
        facilityWire = captured.WireBodyJson;
        facilityDispatch = new FacilityDispatchWitness
        {
            dispatchEligibility = eligibility,
            profileId = captured.ProfileId, correlationId = captured.CorrelationId, model = captured.Model,
            schemaVersion = captured.SchemaVersion, schemaHash = captured.SchemaHash,
            wireSha256 = Hash(captured.CopyWireBodyBytes()), checkpointSha256 = Hash(Encoding.UTF8.GetBytes(facilityCheckpointJson)),
            frame = Time.frameCount, absoluteHour = calendar.AbsoluteHour,
            facilityId = building.PersistentInstanceId.Value, pending = pending, evidence = evidence,
            facilityUsageEvidence = usageEvidence
        };
        WriteNew("facility-dispatch-world.json", facilityCheckpointJson);
        WriteNew("facility-dispatch-wire.json", facilityWire);
        WriteNew("facility-dispatch-witness.json", JsonUtility.ToJson(facilityDispatch, true));
        WriteNew("facility-selected-pending.json", JsonUtility.ToJson(pending, true));
    }

    private void CaptureTraitDispatch(LocalLlmCapturedRequest captured)
    {
        Require(GameplayModuleDispatchEligibilityCapture.TryCaptureTrait(traitRuntime, captured.CorrelationId,
            out CharacterProgression progression, out CharacterAcquiredTraitPendingRequestState pending, out string prompt,
            out NarrativePublicContextMaterialSnapshot publicContext,
            out GameplayModuleDispatchEligibilityWitness eligibility),
            "Queue trait dispatch has no unique live pending owner.");
        Require(prompt == captured.Prompt, "Queued trait prompt differs from the submitting runtime.");
        if (progression.Actor.Identity.PersistentId != owner.Identity.PersistentId)
        {
            nonTargetTraitTransports.Add(captured.CorrelationId);
            return;
        }
        Require(ReferenceEquals(progression, owner.Progression) && ReferenceEquals(progression.Actor, owner),
            "Trait dispatch was bound to a stale actor instance with the same persistent ID.");
        // A role/identity outcome can legitimately trigger manifestation before
        // Facility.Interact completes. Do not rewind into an external coroutine
        // that the official world cannot resume. The real persisted pending is
        // observed again via the runtime's normal cancellation/retry boundary.
        if (ownsLease || controlledFacilityCommandActive) return;
        Require(pending.manifestationMilestone == 3 && ReadTraitScore() >= 3,
            "First trait was not dispatched from its real milestone-3 gate.");
        if (replaying)
        {
            WriteNew("trait-replayed-wire.json", captured.WireBodyJson);
            GameplayModuleDispatchEligibilityCapture.RequireSame(traitDispatch?.dispatchEligibility, eligibility);
            WriteNew("trait-replayed-prompt.txt", prompt);
            Require(traitDispatch != null && captured.CorrelationId == traitDispatch.correlationId
                && prompt == traitPrompt && captured.WireBodyJson == traitWire
                && JsonUtility.ToJson(pending) == JsonUtility.ToJson(traitDispatch.pending),
                "Restored trait regenerated or changed its pending/prompt/wire request.");
            report.traitWireReplayExact = true;
            return;
        }
        Require(traitDispatch == null, "Duplicate initial trait dispatch.");
        WriteNew("trait-selected-pending.json", JsonUtility.ToJson(pending, true));
        WriteNew("trait-selected-prompt.txt", prompt);
        WriteNew("trait-selected-context.json", JsonUtility.ToJson(publicContext, true));
        // Some real trait gates freeze cumulative state facts before the later
        // facility outcome is published. Preserve that request; bind only its
        // observed selected aggregate, never a chronologically nearby outcome.
        var aggregateEvidence = new List<AggregateEvidenceBinding>();
        for (int index = 0; index < report.commands.Count; index++)
        {
            CommandWitness command = report.commands[index];
            if (command.actorId != progression.Actor.Identity.PersistentId) continue;
            foreach (NarrativePublicFactSnapshot fact in publicContext.publicFacts)
            {
                string kind = LiveNarrativeActivityEvidence.TryMatchState(
                    command.observedActivityLedger, fact, pending.evidenceFactIds, out _)
                    ? "typed-state-cumulative@1"
                    : LiveNarrativeActivityEvidence.TryMatch(
                        command.observedActivityLedger, fact, pending.evidenceFactIds, out _)
                        ? "typed-activity-cumulative@1" : null;
                if (kind != null)
                    aggregateEvidence.Add(new AggregateEvidenceBinding
                    {
                        commandFile = $"command-{index + 1:D3}.json",
                        publicFactId = fact.factId, evidenceKind = kind
                    });
            }
        }
        var evidence = pending.evidenceBindings.Count > 0
            ? ReadBoundEvidence(pending.evidenceBindings, requireFacilityCompletion: false)
            : new List<GameplayOutcomeSnapshot>();
        Require(evidence.Count > 0 || aggregateEvidence.Count > 0,
            "Trait pending contains neither exact evidence nor verified observed cumulative evidence.");
        DungeonGameSaveData checkpoint = saves.Capture();
        report.traitCheckpointSections = checkpoint.sections.Count;
        Require(report.traitCheckpointSections == report.registeredSections, "Incomplete trait dispatch checkpoint.");
        traitCheckpointJson = saves.ToJson(checkpoint);
        traitPrompt = prompt;
        traitWire = captured.WireBodyJson;
        traitDispatch = new TraitDispatchWitness
        {
            dispatchEligibility = eligibility,
            profileId = captured.ProfileId, correlationId = captured.CorrelationId, model = captured.Model,
            schemaVersion = captured.SchemaVersion, schemaHash = captured.SchemaHash,
            wireSha256 = Hash(captured.CopyWireBodyBytes()), checkpointSha256 = Hash(Encoding.UTF8.GetBytes(traitCheckpointJson)),
            frame = Time.frameCount, absoluteHour = calendar.AbsoluteHour, experienceScore = ReadTraitScore(),
            ownerId = progression.Actor.Identity.PersistentId, pending = pending, evidence = evidence,
            aggregateEvidence = aggregateEvidence
        };
        WriteNew("trait-dispatch-world.json", traitCheckpointJson);
        WriteNew("trait-dispatch-wire.json", traitWire);
        WriteNew("trait-dispatch-prompt.txt", traitPrompt);
        WriteNew("trait-dispatch-witness.json", JsonUtility.ToJson(traitDispatch, true));
    }

    private List<GameplayOutcomeSnapshot> ReadBoundEvidence(IEnumerable<GameplayOutcomeEvidenceBindingSnapshot> bindings,
        bool requireFacilityCompletion = true)
    {
        var actual = ReadOwnerOutcomes();
        var evidence = new List<GameplayOutcomeSnapshot>();
        foreach (GameplayOutcomeEvidenceBindingSnapshot binding in bindings)
        {
            Require(binding.sourceKind == "exact", "This witness requires exact gameplay evidence; compacted intake remains separate.");
            GameplayOutcomeSnapshot row = actual.SingleOrDefault(value => value.runId == binding.outcomeRunId
                && value.sequence == binding.outcomeSequence);
            Require(row != null && row.deliveryMetadataVersion == 1 && row.publishedRevision > 0
                && row.recordedAbsoluteHour <= calendar.AbsoluteHour && row.absoluteDay <= calendar.Day,
                "Pending evidence lacks a real published current-world outcome: " + binding.publicFactId);
            evidence.Add(row);
        }
        Require(evidence.Count > 0, "Pending contains no exact gameplay evidence.");
        Require(!requireFacilityCompletion || evidence.Any(value => value.outcomeTypeId == "service.facility-visited"),
            "Actual training results were not consumed as module-selection evidence.");
        return evidence;
    }

    private int ReadTraitScore() => CharacterAcquiredTraitExperienceScore.Require(owner.Progression.NarrativeLedger);

    private string TraitDiagnostic() => traitRuntime.TryGetLatestDiagnostic(owner.Identity.PersistentId, out var diagnostic)
        ? diagnostic.Status + "/" + diagnostic.IssueCode + ": "
            + diagnostic.Detail
        : "No runtime diagnostic";

    private List<GameplayOutcomeSnapshot> ReadOwnerOutcomes() => ReadEntityOutcomes("character", owner.Identity.PersistentId);

    private List<GameplayOutcomeSnapshot> ReadEntityOutcomes(string entityKind, string entityId)
        => ReadAllOutcomes().Where(value => value.subjects.Any(subject => subject.entityKindId == entityKind
            && subject.entityId == entityId)).ToList();

    private List<GameplayOutcomeSnapshot> ReadAllOutcomes()
    {
        var result = new List<GameplayOutcomeSnapshot>();
        OutcomeCursor cursor = OutcomeCursor.FirstPage(200);
        while (true)
        {
            GameplayOutcomeQueryPage page = outcomes.GetGlobal(cursor, OutcomeFilter.All);
            if (page.Items.Count == 0) break;
            result.AddRange(page.Items.Where(value => value.Exact != null).Select(value => value.Exact));
            cursor = page.NextCursor;
        }
        return result;
    }

    private static string OutcomeKey(GameplayOutcomeSnapshot value) => value.runId + "/" + value.sequence;
    private static string Hash(byte[] bytes) => BitConverter.ToString(SHA256.Create().ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
    private void WriteNew(string name, string contents)
    {
        using var stream = new FileStream(Path.Combine(output, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(contents);
    }
    private void CaptureLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Warning) report.warnings.Add(message);
        else if (type is LogType.Error or LogType.Exception or LogType.Assert) report.errors.Add(message + "\n" + stackTrace);
    }
}
#endif
