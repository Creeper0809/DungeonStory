#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.ServiceRooms;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class MigratedProducerEconomyServiceEntryE2EDebugScenarios
{
    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds { get; } =
        new[]
        {
            MigratedProducerOutcomeKind.FacilityVisitEvent,
            MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent,
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt,
            MigratedProducerOutcomeKind.RegularCustomerRecruitResult,
            MigratedProducerOutcomeKind.RegularCustomerRecruitDeliveryResult,
            MigratedProducerOutcomeKind.RegularCustomerVisitResult,
            MigratedProducerOutcomeKind.ServiceModeChangeResult
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyFacilityVisitEntry();
            VerifyFacilityUseDetailedOutbox();
            VerifyPhysicalItemTransformEntry();
            VerifyServiceModeChangeEntry();
            VerifyMetaUpgradePurchaseEntry();
            VerifyRegularCustomerVisitEntry();
            VerifyRegularCustomerRecruitEntry();
            VerifyRegularCustomerRecruitDeliveryEntry();
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static void VerifyMetaObserverFocused() =>
        VerifyMetaUpgradePurchaseEntry();

    public static void RunRecruitmentObserversForBatch()
    {
        RegularCustomerFactObserverDebugScenarios.RunOrThrow();
        VerifyRegularCustomerVisitEntry();
        VerifyRegularCustomerRecruitEntry();
        VerifyRegularCustomerRecruitDeliveryEntry();
        VerifyRegularCustomerMercenaryDeliveryCostEntry();
        Debug.Log("B108_RECRUITMENT_EXISTING_ENTRIES=PASS");
    }

    public static void RunFacilityUseDetailedBatch()
    {
        VerifyFacilityUseDetailedOutbox();
        Debug.Log(
            "PASS: A01 facility-use detailed parent, terminal states, "
            + "save/reload retry, and no-duplicate checks.");
    }

    public static void RunFacilitySuspendedCancellationBatch()
    {
        foreach (string caseId in new[] { "approach", "service", "meal" })
        {
            bool meal = caseId == "meal";
            using var scenario = new ActualFacilityUseScenario(
                MigratedProducerOutcomeFaultMode.None, useRealCharacter: false,
                resumableAction: true, useDuration: 5f, pendingMeal: meal);
            scenario.PauseAt(use => caseId == "approach" ? use.phase == FacilityUseExecutionPhase.Approach : meal
                ? use.phase == FacilityUseExecutionPhase.Effects && !string.IsNullOrEmpty(use.mealOperationId)
                : use.phase == FacilityUseExecutionPhase.Service && use.timerInitialized);
            FacilityUseValues before = FacilityUseValues.FromSnapshot(scenario.Visitor.VisitorSnapshot);
            scenario.CancelPausedUse();
            GameplayOutcomeSnapshot terminal = scenario.RequireLatestExact();
            string exact = JsonUtility.ToJson(terminal);
            Require(terminal.status == GameplayOutcomeStatus.Cancelled
                    && scenario.ActiveUses == 0 && scenario.CompletedUses == 0,
                "Suspended-use cancellation did not retire one exact owner before resumption.");
            scenario.ResumePausedRoutine();
            Require(scenario.Probe.CommitCount(MigratedProducerOutcomeKind.FacilityVisitEvent) == 1
                    && JsonUtility.ToJson(scenario.RequireLatestExact()) == exact
                    && scenario.ControlledVisitor.RecoveryCalls == 0
                    && before.EqualsValues(FacilityUseValues.FromSnapshot(scenario.Visitor.VisitorSnapshot)),
                "Cancelled suspended iterator changed recovery or republished its terminal parent.");
        }
        using (var malformed = new ActualFacilityUseScenario(
            MigratedProducerOutcomeFaultMode.None, useRealCharacter: false,
            resumableAction: true, useDuration: 5f))
        {
            malformed.PauseAt(use => use.phase == FacilityUseExecutionPhase.Service && use.timerInitialized);
            malformed.RemovePausedOwnerWithoutCancellation();
            bool rejected = false;
            try { malformed.ResumePausedRoutine(); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected && malformed.Probe.CommitCount(MigratedProducerOutcomeKind.FacilityVisitEvent) == 0,
                "A still-current action with an unexplained missing execution was silently accepted.");
        }
        RunFacilityUseContinuationScenarios();
        Debug.Log("FACILITY_SUSPENDED_CANCELLATION=PASS; approach-cancel=1; service-cancel=1; pending-meal-cancel=1; "
            + "missing-current-owner-rejected=1; duplicate-parent=0; controlled-iterator-not-natural-play=1");
    }

    public static void RunFacilityVisitObserverFocused()
    {
        VerifyFacilityVisitEntry();
        VerifyFacilityUseDetailedOutbox();
        RunFacilitySuspendedCancellationBatch();
        Debug.Log("B118_FACILITY_EXISTING_ENTRIES=PASS");
    }

    public static void RunG01FromBatch()
    {
        VerifyRegularCustomerRecruitEntry();
        VerifyRegularCustomerRecruitDeliveryEntry();
        G01MercenaryEvidence mercenary =
            VerifyRegularCustomerMercenaryDeliveryCostEntry();

        string reportPath = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_G01_RECRUIT_DELIVERY_REPORT_PATH");
        Require(
            !string.IsNullOrWhiteSpace(reportPath),
            "The G01 report path was not supplied by the isolated launcher.");
        string fullPath = Path.GetFullPath(reportPath);
        string directory = Path.GetDirectoryName(fullPath);
        Require(
            !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory),
            "The G01 report directory does not exist.");

        using FileStream stream = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(stream);
        writer.Write(
            "U0_G01_RECRUIT_DELIVERY=PASS\n"
            + "staff=contract-committed,delivery-distinct,pending-before-apply,pending-after-apply\n"
            + "restore=regular-customer-json,fresh-runtime,parent-only-retry\n"
            + "identity=customer-and-actor-match,result-key-retained\n"
            + "mercenary=actual-employment-contract,actual-money-account,fee="
            + mercenary.Fee.ToString(CultureInfo.InvariantCulture)
            + ",spend-count="
            + mercenary.SpendCount.ToString(CultureInfo.InvariantCulture)
            + ",balance-before="
            + mercenary.BalanceBefore.ToString(CultureInfo.InvariantCulture)
            + ",balance-after="
            + mercenary.BalanceAfter.ToString(CultureInfo.InvariantCulture)
            + "\n"
            + "exactly-once=contract1,delivery-parent1,mercenary-contract1,payment1,replay0\n"
            + "customer-id=" + mercenary.CustomerId + "\n"
            + "delivery-result-key=" + mercenary.DeliveryResultKey + "\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_G01_RECRUIT_DELIVERY=PASS; report=" + fullPath);
    }

    public static void RunFacilityUseContinuationScenarios()
    {
        using var scenario = new ActualFacilityUseScenario(
            MigratedProducerOutcomeFaultMode.None, useRealCharacter: false,
            resumableAction: true, useDuration: 5f);
        scenario.PauseAt(use => use.phase == FacilityUseExecutionPhase.Service
            && use.timerInitialized && use.remainingSeconds == 3f);
        FacilityUseExecutionState first = scenario.CurrentExecution.Clone();
        Require(!first.effectsCommitted && scenario.ControlledVisitor.RecoveryCalls == 0,
            "Facility applied recovery before its saved service wait completed.");
        scenario.RoundTripActiveExecution();
        Require(scenario.CurrentExecution.remainingSeconds == 3f
            && scenario.CurrentExecution.operationId == first.operationId,
            "Facility restore reset elapsed service time or operation identity.");
        scenario.PauseAt(use => use.phase == FacilityUseExecutionPhase.Exit && use.effectsCommitted);
        Require(scenario.ControlledVisitor.RecoveryCalls == 1
            && scenario.ControlledVisitor.UseCompletionCalls == 1,
            "Resumed facility did not apply its recovery exactly once.");
        FacilityUseValues effects = FacilityUseValues.FromSnapshot(scenario.Visitor.VisitorSnapshot);
        scenario.RoundTripActiveExecution();
        scenario.RunInteract();
        Require(scenario.CompletedUses == 1 && scenario.ActiveUses == 0 && scenario.PendingUses == 0
            && scenario.ControlledVisitor.RecoveryCalls == 1
            && scenario.ControlledVisitor.UseCompletionCalls == 1
            && scenario.Probe.CommitCount(MigratedProducerOutcomeKind.FacilityVisitEvent) == 1,
            "Post-effect restore duplicated recovery, use completion, or parent ledger.");
        Require(scenario.Visitor.VisitorSnapshot.Sleep == effects.Sleep
            && scenario.Visitor.VisitorSnapshot.CurrentHealth == effects.Health,
            "Post-effect restore changed already committed need/health state.");
        RequireFacilityEvidence(scenario.RequireLatestExact(), scenario.ActorId,
            scenario.FacilityId, FacilityRole.Rest, CharacterFacilityUseOutcomeStatus.Completed,
            string.Empty, string.Empty, FacilityUseValues.FromSnapshot(first.before.ToSnapshot()), effects);
        VerifyActualFacilityInteractEntry();
        Debug.Log("FACILITY_USE_COROUTINE_CONTINUATION=PASS; service-wait=1; after-effects=1; "
            + "same-operation=1; single-recovery=1; single-parent=1; controlled-clock-not-playmode=1");
    }

    private static void VerifyMetaUpgradePurchaseEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent;

        using (var scenario = new MetaPurchaseScenario(Kind))
        {
            string before = "level=" + scenario.Level
                + "|currency=" + scenario.Currency;
            Require(
                scenario.Purchase()
                && scenario.Level == 1
                && scenario.Currency == 90,
                "The meta-upgrade production entry did not mutate its owner.");
            string after = "level=" + scenario.Level
                + "|currency=" + scenario.Currency;
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new MetaPurchaseScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            Require(
                !scenario.Purchase()
                && scenario.Level == 0
                && scenario.Currency == 100
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected meta-upgrade purchase left currency or level mutation.");
        }

        using (var scenario = new MetaPurchaseScenario(
                   Kind,
                   deliveryFault: true))
        {
            Require(
                scenario.Purchase()
                && scenario.Level == 1
                && scenario.Currency == 90,
                "A durably pending meta-upgrade purchase was rolled back.");
            int level = scenario.Level;
            int currency = scenario.Currency;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.Level == level && scenario.Currency == currency,
                "Delivery retry duplicated the meta-upgrade purchase.");
        }
    }

    private static void VerifyPhysicalItemTransformEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.PhysicalItemTransformReceipt;

        using (var scenario = new PhysicalTransformScenario(Kind))
        {
            string before = "sourceQuantity=" + scenario.SourceQuantity
                + "|outputQuantity=" + scenario.OutputQuantity;
            Require(
                scenario.Transform(out PhysicalItemTransformFailureCode code)
                && code == PhysicalItemTransformFailureCode.None
                && scenario.SourceQuantity == 2
                && scenario.OutputQuantity == 1,
                "The physical-transform production entry did not mutate its owner.");
            string after = "sourceQuantity=" + scenario.SourceQuantity
                + "|outputQuantity=" + scenario.OutputQuantity;
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new PhysicalTransformScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            Require(
                !scenario.Transform(out PhysicalItemTransformFailureCode code)
                && code == PhysicalItemTransformFailureCode.OutcomeCommitFailed
                && scenario.SourceQuantity == 3
                && scenario.OutputQuantity == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected physical transform left source/output mutation.");
        }

        using (var scenario = new PhysicalTransformScenario(
                   Kind,
                   deliveryFault: true))
        {
            Require(
                scenario.Transform(out _)
                && scenario.SourceQuantity == 2
                && scenario.OutputQuantity == 1,
                "A durably pending physical transform was rolled back.");
            int source = scenario.SourceQuantity;
            int output = scenario.OutputQuantity;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.SourceQuantity == source
                && scenario.OutputQuantity == output,
                "Delivery retry duplicated the physical transform.");
        }
    }

    private static void VerifyServiceModeChangeEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.ServiceModeChangeResult;

        using (var scenario = new ServiceModeScenario(Kind))
        {
            string before = "mode=" + scenario.Mode;
            ServiceModeChangeResult result = scenario.SwitchToDirect();
            Require(
                result.Succeeded
                && scenario.Mode == ServiceOperationMode.Direct,
                "The service-mode production entry did not mutate its owner.");
            string after = "mode=" + scenario.Mode;
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new ServiceModeScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            ServiceModeChangeResult result = scenario.SwitchToDirect();
            Require(
                !result.Succeeded
                && scenario.Mode == ServiceOperationMode.Managed
                && scenario.Outcomes.Transaction.Capture().owners.Count == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected service-mode change left a partial mode mutation.");
        }

        using (var scenario = new ServiceModeScenario(
                   Kind,
                   deliveryFault: true))
        {
            ServiceModeChangeResult result = scenario.SwitchToDirect();
            Require(
                result.Succeeded
                && scenario.Mode == ServiceOperationMode.Direct,
                "A durably pending service-mode change was rolled back.");
            ServiceOperationMode committed = scenario.Mode;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.Mode == committed,
                "Delivery retry duplicated or reversed the service-mode mutation.");
        }
    }

    private static void VerifyFacilityVisitEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.FacilityVisitEvent;

        using (var scenario = new FacilityVisitScenario(Kind))
        {
            string before = "completedUses=" + scenario.CompletedUses;
            scenario.CommitVisit();
            Require(
                scenario.CompletedUses == 1,
                "The facility-visit production entry did not mutate its owner.");
            string after = "completedUses=" + scenario.CompletedUses;
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new FacilityVisitScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            RequireThrows<InvalidOperationException>(scenario.CommitVisit);
            Require(
                scenario.CompletedUses == 1
                && scenario.PendingUses == 1
                && scenario.Outcomes.Transaction.Capture().owners.Count == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected facility parent did not preserve its durable owner mutation and retry state.");
            scenario.AllowCommitAndReconcile();
            Require(
                scenario.CompletedUses == 1
                && scenario.PendingUses == 0
                && scenario.Probe.CommitCount(Kind) == 1,
                "Facility parent retry duplicated its already committed owner mutation.");
        }

        using (var scenario = new FacilityVisitScenario(
                   Kind,
                   deliveryFault: true))
        {
            scenario.CommitVisit();
            int committedUses = scenario.CompletedUses;
            Require(
                committedUses == 1,
                "A durably pending facility visit was rolled back.");
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.CompletedUses == committedUses,
                "Delivery retry duplicated the facility visit mutation.");
        }
    }

    private static void VerifyFacilityUseDetailedOutbox()
    {
        VerifyActualFacilityInteractEntry();

        (FacilityRole Role, string Label, Action<FacilityUseValues> Mutate)[]
            completedCases =
            {
                (FacilityRole.Meal, "meal", value => value.Hunger += 35f),
                (FacilityRole.Rest, "rest", value => value.Sleep += 35f),
                (FacilityRole.Toilet, "toilet", value => value.Excretion += 70f),
                (FacilityRole.Hygiene, "hygiene", value => value.Hygiene += 60f),
                (FacilityRole.Entertainment, "recreation", value => value.Fun += 20f)
            };

        foreach ((FacilityRole role, string label, Action<FacilityUseValues> mutate)
                 in completedCases)
        {
            using var scenario = new FacilityVisitScenario(
                MigratedProducerOutcomeKind.FacilityVisitEvent);
            var before = new FacilityUseValues();
            var after = before.Clone();
            mutate(after);
            string child = ChildKey(label);
            FacilityVisitCommitResult result = scenario.CommitDetailed(
                role,
                CharacterFacilityUseOutcomeStatus.Completed,
                effectsCommitted: true,
                failureCode: string.Empty,
                before.ToSnapshot(scenario.ActorId),
                after.ToSnapshot(scenario.ActorId),
                new[] { child },
                serviceSessionId: "service-session:" + label);
            Require(
                result.DurablyCommitted
                && scenario.CompletedUses == 1
                && scenario.PendingUses == 0,
                "Detailed facility completion did not close exactly once: "
                + label + "; detail=" + result.DetailCode);
            GameplayOutcomeSnapshot exact = scenario.RequireLatestExact();
            RequireFacilityEvidence(
                exact,
                scenario.ActorId,
                scenario.FacilityId,
                role,
                CharacterFacilityUseOutcomeStatus.Completed,
                string.Empty,
                child,
                before,
                after);
        }

        VerifyFacilityTerminalCase(
            CharacterFacilityUseOutcomeStatus.Failed,
            effectsCommitted: false,
            "water-resource-unavailable",
            expectedCompletedUses: 0);
        VerifyFacilityTerminalCase(
            CharacterFacilityUseOutcomeStatus.Cancelled,
            effectsCommitted: false,
            "action-replaced-during-interaction",
            expectedCompletedUses: 0);
        VerifyFacilityTerminalCase(
            CharacterFacilityUseOutcomeStatus.Partial,
            effectsCommitted: true,
            "ExternalPaymentRejected",
            expectedCompletedUses: 1);

        using var pending = new FacilityVisitScenario(
            MigratedProducerOutcomeKind.FacilityVisitEvent,
            MigratedProducerOutcomeFaultMode.RejectCommit);
        var pendingBefore = new FacilityUseValues();
        var pendingAfter = pendingBefore.Clone();
        pendingAfter.Mood += 12f;
        pendingAfter.Sleep += 35f;
        string restChild = ChildKey("rest-save-retry");
        FacilityVisitCommitResult rejected = pending.CommitDetailed(
            FacilityRole.Rest,
            CharacterFacilityUseOutcomeStatus.Partial,
            effectsCommitted: true,
            failureCode: "parent-commit-rejected",
            pendingBefore.ToSnapshot(pending.ActorId),
            pendingAfter.ToSnapshot(pending.ActorId),
            new[] { restChild },
            serviceSessionId: "service-session:save-retry");
        Require(
            !rejected.DurablyCommitted
            && rejected.Pending
            && pending.CompletedUses == 1
            && pending.PendingUses == 1,
            "Facility parent rejection did not preserve its exact retry boundary.");
        pending.RoundTripFacilityState();
        pending.AllowCommitAndReconcile();
        int commitsAfterRetry = pending.Probe.CommitCount(
            MigratedProducerOutcomeKind.FacilityVisitEvent);
        pending.AllowCommitAndReconcile();
        Require(
            pending.CompletedUses == 1
            && pending.PendingUses == 0
            && commitsAfterRetry == 1
            && pending.Probe.CommitCount(
                MigratedProducerOutcomeKind.FacilityVisitEvent) == 1,
            "Facility save/reload retry duplicated recovery or its parent result.");
        RequireFacilityEvidence(
            pending.RequireLatestExact(),
            pending.ActorId,
            pending.FacilityId,
            FacilityRole.Rest,
            CharacterFacilityUseOutcomeStatus.Partial,
            "parent-commit-rejected",
            restChild,
            pendingBefore,
            pendingAfter);
    }

    private static void VerifyActualFacilityInteractEntry()
    {
        using (var completed = new ActualFacilityUseScenario(
                   MigratedProducerOutcomeFaultMode.None,
                   useRealCharacter: true))
        {
            FacilityUseValues before = FacilityUseValues.FromSnapshot(
                completed.Visitor.VisitorSnapshot);
            completed.RunInteract();
            FacilityUseValues after = FacilityUseValues.FromSnapshot(
                completed.Visitor.VisitorSnapshot);
            Require(
                completed.CompletedUses == 1
                && completed.PendingUses == 0
                && completed.Probe.CommitCount(
                    MigratedProducerOutcomeKind.FacilityVisitEvent) == 1,
                "Facility.Interact did not complete its production owner exactly once.");
            Require(
                after.Sleep > before.Sleep
                && after.Health > before.Health
                && after.Injury < before.Injury
                && after.Stress < before.Stress,
                "Facility.Interact did not apply the actual rest and expedition recovery transitions.");
            RequireFacilityEvidence(
                completed.RequireLatestExact(),
                completed.ActorId,
                completed.FacilityId,
                FacilityRole.Rest,
                CharacterFacilityUseOutcomeStatus.Completed,
                string.Empty,
                completed.RestChildResultKey,
                before,
                after);
        }

        using (var failed = new ActualFacilityUseScenario(
                   MigratedProducerOutcomeFaultMode.None,
                   useRealCharacter: false,
                   canMove: false))
        {
            FacilityUseValues before = FacilityUseValues.FromSnapshot(
                failed.Visitor.VisitorSnapshot);
            failed.RunInteract();
            FacilityUseValues after = FacilityUseValues.FromSnapshot(
                failed.Visitor.VisitorSnapshot);
            Require(
                failed.CompletedUses == 0
                && failed.PendingUses == 0
                && failed.ControlledVisitor.LastFailureKind
                    == BuildingInteractionFailureKind.ActorUnavailable,
                "Facility.Interact did not preserve the actor-cannot-move failure boundary.");
            RequireFacilityEvidence(
                failed.RequireLatestExact(),
                failed.ActorId,
                failed.FacilityId,
                FacilityRole.Rest,
                CharacterFacilityUseOutcomeStatus.Failed,
                "actor-cannot-move",
                string.Empty,
                before,
                after);
        }

        using (var cancelled = new ActualFacilityUseScenario(
                   MigratedProducerOutcomeFaultMode.None,
                   useRealCharacter: false,
                   canMove: true,
                   replaceActionDuringMove: true))
        {
            FacilityUseValues before = FacilityUseValues.FromSnapshot(
                cancelled.Visitor.VisitorSnapshot);
            cancelled.RunInteract();
            FacilityUseValues after = FacilityUseValues.FromSnapshot(
                cancelled.Visitor.VisitorSnapshot);
            Require(
                cancelled.CompletedUses == 0
                && cancelled.PendingUses == 0
                && cancelled.ControlledVisitor.LastCancellationKind
                    == BuildingInteractionFailureKind.ActionReplaced,
                "Facility.Interact did not preserve action replacement as cancellation.");
            RequireFacilityEvidence(
                cancelled.RequireLatestExact(),
                cancelled.ActorId,
                cancelled.FacilityId,
                FacilityRole.Rest,
                CharacterFacilityUseOutcomeStatus.Cancelled,
                "action-replaced-during-interaction",
                string.Empty,
                before,
                after);
        }

        using (var pending = new ActualFacilityUseScenario(
                   MigratedProducerOutcomeFaultMode.RejectCommit,
                   useRealCharacter: true))
        {
            FacilityUseValues before = FacilityUseValues.FromSnapshot(
                pending.Visitor.VisitorSnapshot);
            pending.RunInteract();
            FacilityUseValues applied = FacilityUseValues.FromSnapshot(
                pending.Visitor.VisitorSnapshot);
            Require(
                pending.CompletedUses == 1
                && pending.PendingUses == 1
                && pending.Probe.CommitCount(
                    MigratedProducerOutcomeKind.FacilityVisitEvent) == 0,
                "Facility.Interact did not retain its completed mutation and pending parent after rejection.");
            pending.RoundTripFacilityState();
            pending.AllowCommitAndReconcile();
            pending.AllowCommitAndReconcile();
            FacilityUseValues reconciled = FacilityUseValues.FromSnapshot(
                pending.Visitor.VisitorSnapshot);
            Require(
                pending.CompletedUses == 1
                && pending.PendingUses == 0
                && pending.Probe.CommitCount(
                    MigratedProducerOutcomeKind.FacilityVisitEvent) == 1
                && applied.EqualsValues(reconciled),
                "Facility.Interact save/reload reconciliation duplicated recovery or its parent.");
            RequireFacilityEvidence(
                pending.RequireLatestExact(),
                pending.ActorId,
                pending.FacilityId,
                FacilityRole.Rest,
                CharacterFacilityUseOutcomeStatus.Completed,
                string.Empty,
                pending.RestChildResultKey,
                before,
                applied);
        }
    }

    private static void VerifyFacilityTerminalCase(
        CharacterFacilityUseOutcomeStatus status,
        bool effectsCommitted,
        string failureCode,
        int expectedCompletedUses)
    {
        using var scenario = new FacilityVisitScenario(
            MigratedProducerOutcomeKind.FacilityVisitEvent);
        var before = new FacilityUseValues();
        var after = before.Clone();
        string child = string.Empty;
        if (effectsCommitted)
        {
            after.Fun += 20f;
            child = ChildKey("payment-partial");
        }
        FacilityVisitCommitResult result = scenario.CommitDetailed(
            FacilityRole.Entertainment,
            status,
            effectsCommitted,
            failureCode,
            before.ToSnapshot(scenario.ActorId),
            after.ToSnapshot(scenario.ActorId),
            child.Length == 0 ? Array.Empty<string>() : new[] { child },
            serviceSessionId: "service-session:terminal");
        Require(
            result.DurablyCommitted
            && scenario.CompletedUses == expectedCompletedUses,
            "Facility terminal case did not preserve its exact mutation boundary: "
            + status);
        RequireFacilityEvidence(
            scenario.RequireLatestExact(),
            scenario.ActorId,
            scenario.FacilityId,
            FacilityRole.Entertainment,
            status,
            failureCode,
            child,
            before,
            after);
    }

    private static void RequireFacilityEvidence(
        GameplayOutcomeSnapshot exact,
        string actorId,
        string facilityId,
        FacilityRole role,
        CharacterFacilityUseOutcomeStatus status,
        string failureCode,
        string childResultKey,
        FacilityUseValues before,
        FacilityUseValues after)
    {
        Require(
            exact != null
            && exact.subjects.Any(value =>
                value.entityKindId == MigratedProducerOutcomeIds.CharacterKind.Value
                && value.entityId == actorId)
            && exact.subjects.Any(value =>
                value.entityKindId == MigratedProducerOutcomeIds.FacilityKind.Value
                && value.entityId == facilityId),
            "Facility use did not expose both actor and facility subjects.");
        RequireFact(exact, MigratedProducerOutcomeIds.FacilityRoleFact, role.ToString());
        RequireFact(exact, MigratedProducerOutcomeIds.OutcomeStatusFact, status.ToString());
        RequireFacilitySummary(exact, status);
        RequireFact(
            exact,
            MigratedProducerOutcomeIds.ReasonFact,
            string.IsNullOrWhiteSpace(failureCode) ? "none" : failureCode);
        RequireFact(
            exact,
            MigratedProducerOutcomeIds.ChildResultKeysFact,
            string.IsNullOrWhiteSpace(childResultKey) ? "none" : childResultKey);
        RequireNumericFact(
            exact,
            MigratedProducerOutcomeIds.InjuryBeforeFact,
            before.Injury);
        RequireNumericFact(
            exact,
            MigratedProducerOutcomeIds.InjuryAfterFact,
            after.Injury);
        RequireMetric(exact, MigratedProducerOutcomeIds.HealthBeforeMetric, before.Health);
        RequireMetric(exact, MigratedProducerOutcomeIds.HealthAfterMetric, after.Health);
        RequireMetric(exact, MigratedProducerOutcomeIds.StressBeforeMetric, before.Stress);
        RequireMetric(exact, MigratedProducerOutcomeIds.StressAfterMetric, after.Stress);
        RequireMetric(exact, MigratedProducerOutcomeIds.MoodBeforeMetric, before.Mood);
        RequireMetric(exact, MigratedProducerOutcomeIds.MoodAfterMetric, after.Mood);
        RequireMetric(exact, MigratedProducerOutcomeIds.HungerBeforeMetric, before.Hunger);
        RequireMetric(exact, MigratedProducerOutcomeIds.HungerAfterMetric, after.Hunger);
        RequireMetric(exact, MigratedProducerOutcomeIds.FunBeforeMetric, before.Fun);
        RequireMetric(exact, MigratedProducerOutcomeIds.FunAfterMetric, after.Fun);
        RequireMetric(exact, MigratedProducerOutcomeIds.SleepBeforeMetric, before.Sleep);
        RequireMetric(exact, MigratedProducerOutcomeIds.SleepAfterMetric, after.Sleep);
        RequireMetric(exact, MigratedProducerOutcomeIds.ExcretionBeforeMetric, before.Excretion);
        RequireMetric(exact, MigratedProducerOutcomeIds.ExcretionAfterMetric, after.Excretion);
        RequireMetric(exact, MigratedProducerOutcomeIds.HygieneBeforeMetric, before.Hygiene);
        RequireMetric(exact, MigratedProducerOutcomeIds.HygieneAfterMetric, after.Hygiene);
    }

    private static void RequireFacilitySummary(
        GameplayOutcomeSnapshot exact,
        CharacterFacilityUseOutcomeStatus status)
    {
        string expectedEnding = status switch
        {
            CharacterFacilityUseOutcomeStatus.Completed => "이용을 마쳤다.",
            CharacterFacilityUseOutcomeStatus.Partial => "이용을 일부 마쳤다.",
            CharacterFacilityUseOutcomeStatus.Cancelled => "이용을 취소했다.",
            CharacterFacilityUseOutcomeStatus.Failed => "이용에 실패했다.",
            _ => "이용 결과가 확정됐다."
        };
        string summary = exact.facts
            .Single(value =>
                value.factId == MigratedProducerOutcomeIds.SummaryFact.Value)
            .value;
        Require(
            summary.EndsWith(expectedEnding, StringComparison.Ordinal)
            && !summary.Contains("actor=", StringComparison.Ordinal)
            && !summary.Contains("operation=", StringComparison.Ordinal),
            "Facility use summary leaked internal identifiers or used a non-Korean status: "
            + summary);
    }

    private static void RequireFact(
        GameplayOutcomeSnapshot exact,
        GameplayOutcomeFactId factId,
        string expected)
    {
        Require(
            exact.facts.Any(value =>
                value.factId == factId.Value
                && value.value == expected),
            "Facility use fact mismatch: " + factId.Value + "=" + expected);
    }

    private static void RequireMetric(
        GameplayOutcomeSnapshot exact,
        GameplayMetricId metricId,
        double expected)
    {
        Require(
            exact.metrics.Any(value =>
                value.metricId == metricId.Value
                && Math.Abs(value.value - expected) < 0.0001d),
            "Facility use metric mismatch: " + metricId.Value + "=" + expected);
    }

    private static void RequireNumericFact(
        GameplayOutcomeSnapshot exact,
        GameplayOutcomeFactId factId,
        double expected)
    {
        Require(
            exact.facts.Any(value =>
                value.factId == factId.Value
                && double.TryParse(
                    value.value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double actual)
                && Math.Abs(actual - expected) < 0.0001d),
            "Facility use numeric fact mismatch: "
            + factId.Value + "=" + expected);
    }

    private static string ChildKey(string label) => new GameplayResultKey(
        "test-facility-child",
        new GameplayOperationId("facility-child:" + label),
        1L,
        0).ToString();

    private sealed class FacilityUseValues
    {
        internal float Health { get; set; } = 70f;
        internal float MaximumHealth { get; set; } = 100f;
        internal float Injury { get; set; } = 0.4f;
        internal float Stress { get; set; } = 30f;
        internal float Mood { get; set; } = 40f;
        internal float Hunger { get; set; } = 20f;
        internal float Fun { get; set; } = 15f;
        internal float Sleep { get; set; } = 25f;
        internal float Excretion { get; set; } = 10f;
        internal float Hygiene { get; set; } = 35f;

        internal FacilityUseValues Clone() => new()
        {
            Health = Health,
            MaximumHealth = MaximumHealth,
            Injury = Injury,
            Stress = Stress,
            Mood = Mood,
            Hunger = Hunger,
            Fun = Fun,
            Sleep = Sleep,
            Excretion = Excretion,
            Hygiene = Hygiene
        };

        internal BuildingVisitorSnapshot ToSnapshot(string actorId) => new(
            actorId,
            "시설 이용 검증자",
            Vector3.zero,
            isRuntimeActive: true,
            isInternalStaff: false,
            canMove: true,
            stayDurationMultiplier: 1f,
            revenueMultiplier: 1f,
            personalityPatience: 1f,
            modelPatience: 1f,
            crimeRiskMultiplier: 1f,
            productionOutputMultiplier: 1f,
            stockProductionBonus: 0,
            expeditionStress: Stress,
            mood: Mood,
            hunger: Hunger,
            fun: Fun,
            sleep: Sleep,
            excretion: Excretion,
            hygiene: Hygiene,
            currentHealth: Health,
            maximumHealth: MaximumHealth,
            injurySeverity: Injury,
            activeConditionIds: new[] { "condition:test-fatigue" });

        internal static FacilityUseValues FromSnapshot(
            BuildingVisitorSnapshot snapshot) => new()
        {
            Health = snapshot.CurrentHealth,
            MaximumHealth = snapshot.MaximumHealth,
            Injury = snapshot.InjurySeverity,
            Stress = snapshot.ExpeditionStress,
            Mood = snapshot.Mood,
            Hunger = snapshot.Hunger,
            Fun = snapshot.Fun,
            Sleep = snapshot.Sleep,
            Excretion = snapshot.Excretion,
            Hygiene = snapshot.Hygiene
        };

        internal bool EqualsValues(FacilityUseValues other) =>
            other != null
            && Approximately(Health, other.Health)
            && Approximately(MaximumHealth, other.MaximumHealth)
            && Approximately(Injury, other.Injury)
            && Approximately(Stress, other.Stress)
            && Approximately(Mood, other.Mood)
            && Approximately(Hunger, other.Hunger)
            && Approximately(Fun, other.Fun)
            && Approximately(Sleep, other.Sleep)
            && Approximately(Excretion, other.Excretion)
            && Approximately(Hygiene, other.Hygiene);

        private static bool Approximately(float left, float right) =>
            Mathf.Abs(left - right) < 0.0001f;
    }

    private static void VerifyRegularCustomerVisitEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.RegularCustomerVisitResult;

        using (var scenario = new RegularCustomerScenario(Kind))
        {
            string before = "visitCount=" + scenario.VisitCount;
            scenario.Visit();
            Require(
                scenario.VisitCount == 1,
                "The regular-customer production visit did not mutate its owner.");
            string after = "visitCount=" + scenario.VisitCount;
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            scenario.Visit();
            Require(
                scenario.VisitCount == 0
                && scenario.Outcomes.Transaction.Capture().owners.Count == 0
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected regular-customer visit left a partial mutation.");
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   deliveryFault: true))
        {
            scenario.Visit();
            int committedVisits = scenario.VisitCount;
            Require(
                committedVisits == 1,
                "A durably pending regular-customer visit was rolled back.");
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.VisitCount == committedVisits,
                "Delivery retry duplicated the regular-customer visit mutation.");
        }
    }

    private static void VerifyRegularCustomerRecruitEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.RegularCustomerRecruitResult;

        using (var scenario = new RegularCustomerScenario(Kind))
        {
            scenario.MakeRecruitCandidate();
            scenario.Probe.ClearObservations();
            string before = "isRecruited=" + scenario.IsRecruited
                + "|actorType=" + scenario.Actor.characterType;
            Require(
                scenario.Recruit(),
                "The regular-customer production recruit entry failed.");
            Require(
                scenario.IsRecruited,
                "The regular-customer recruit owner did not advance.");
            string after = "isRecruited=" + scenario.IsRecruited
                + "|actorType=" + scenario.Actor.characterType;
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                before,
                after);
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            scenario.MakeRecruitCandidate();
            scenario.Probe.ClearObservations();
            Require(
                !scenario.Recruit()
                && !scenario.IsRecruited
                && scenario.Actor.characterType == CharacterType.Customer
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected regular-customer recruit left owner or actor mutation.");
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   deliveryFault: true))
        {
            scenario.MakeRecruitCandidate();
            scenario.Probe.ClearObservations();
            Require(
                scenario.Recruit() && scenario.IsRecruited,
                "A durably pending regular-customer recruit was rolled back.");
            CharacterType committedType = scenario.Actor.characterType;
            MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                scenario.DeliveryFault);
            Require(
                scenario.IsRecruited
                && scenario.Actor.characterType == committedType,
                "Delivery retry duplicated or reversed regular-customer recruitment.");
        }
    }

    private static void VerifyRegularCustomerRecruitDeliveryEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.RegularCustomerRecruitDeliveryResult;

        using (var scenario = new RegularCustomerScenario(Kind))
        {
            scenario.MakeRecruitCandidate();
            scenario.Probe.ClearObservations();
            Require(
                scenario.Recruit()
                && scenario.IsRecruited
                && !scenario.DeliveryPending
                && scenario.DeliveryApplied
                && scenario.Actor.characterType == CharacterType.NPC,
                "The recruit contract did not complete its actual actor delivery.");
            _ = MigratedProducerOutcomeE2EAssertions.RequireExact(
                scenario.Outcomes,
                Kind,
                scenario.Probe,
                "delivery=pending",
                "delivery=committed");
            Require(
                !string.IsNullOrWhiteSpace(scenario.DeliveryResultKey),
                "The completed recruit delivery did not retain its result key.");
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectReservation))
        {
            scenario.MakeRecruitCandidate();
            scenario.Probe.ClearObservations();
            Require(
                scenario.Recruit()
                && scenario.IsRecruited
                && scenario.DeliveryPending
                && !scenario.DeliveryApplied
                && scenario.Actor.characterType == CharacterType.Customer
                && scenario.Probe.ReservationCount(Kind) == 1
                && scenario.Probe.CommitAttemptCount(Kind) == 0,
                "A rejected delivery reservation activated or closed the recruit.");
        }

        using (var scenario = new RegularCustomerScenario(
                   Kind,
                   MigratedProducerOutcomeFaultMode.RejectCommit))
        {
            scenario.MakeRecruitCandidate();
            scenario.Probe.ClearObservations();
            Require(
                scenario.Recruit()
                && scenario.IsRecruited
                && scenario.DeliveryPending
                && scenario.DeliveryApplied
                && scenario.Actor.characterType == CharacterType.NPC
                && RejectionReachedCommit(scenario.Probe, Kind),
                "A rejected delivery commit did not preserve the applied pending phase.");

            string pendingJson = scenario.CaptureSaveJson();
            scenario.RestoreSaveJson(pendingJson);
            Require(
                scenario.DeliveryPending
                && scenario.DeliveryApplied
                && string.Equals(
                    scenario.DeliveryActorId,
                    scenario.CustomerId,
                    StringComparison.Ordinal),
                "The applied recruit-delivery phase did not survive save restore.");

            scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            scenario.RetryDelivery();
            Require(
                !scenario.DeliveryPending
                && scenario.DeliveryApplied
                && scenario.Actor.characterType == CharacterType.NPC
                && scenario.Probe.CommitCount(Kind) == 1
                && !string.IsNullOrWhiteSpace(scenario.DeliveryResultKey),
                "Restored recruit delivery did not commit exactly once.");
            scenario.RetryDelivery();
            Require(
                scenario.Probe.CommitCount(Kind) == 1,
                "Terminal recruit delivery replay duplicated its outcome.");
        }
    }

    private static G01MercenaryEvidence
        VerifyRegularCustomerMercenaryDeliveryCostEntry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.RegularCustomerRecruitDeliveryResult;
        using var scenario = new MercenaryRegularCustomerScenario(
            Kind,
            MigratedProducerOutcomeFaultMode.RejectCommit);
        scenario.MakeRecruitCandidate();
        scenario.Probe.ClearObservations();

        int balanceBefore = scenario.Balance;
        Require(
            scenario.Hire(out int firstDailyFee)
            && firstDailyFee > 0
            && scenario.IsRecruited
            && scenario.DeliveryPending
            && scenario.DeliveryApplied
            && scenario.Actor.characterType == CharacterType.NPC
            && scenario.SpendCount == 1
            && scenario.LastSpentAmount == firstDailyFee
            && scenario.Balance == balanceBefore - firstDailyFee
            && scenario.MercenaryContractCount == 1
            && RejectionReachedCommit(scenario.Probe, Kind),
            "A rejected mercenary delivery parent did not preserve its "
            + "contract, actor, fee, and applied pending phase exactly once.");

        string pendingJson = scenario.CaptureSaveJson();
        scenario.RestoreSaveJson(pendingJson);
        Require(
            scenario.DeliveryPending
            && scenario.DeliveryApplied
            && string.Equals(
                scenario.DeliveryActorId,
                scenario.CustomerId,
                StringComparison.Ordinal)
            && scenario.SpendCount == 1
            && scenario.Balance == balanceBefore - firstDailyFee
            && scenario.MercenaryContractCount == 1,
            "The mercenary pending-delivery save roundtrip lost identity "
            + "or duplicated its advance payment.");

        scenario.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
        scenario.RetryDelivery();
        Require(
            !scenario.DeliveryPending
            && scenario.DeliveryApplied
            && scenario.Actor.characterType == CharacterType.NPC
            && scenario.Probe.CommitCount(Kind) == 1
            && scenario.SpendCount == 1
            && scenario.Balance == balanceBefore - firstDailyFee
            && scenario.MercenaryContractCount == 1
            && !string.IsNullOrWhiteSpace(scenario.DeliveryResultKey),
            "The restored mercenary delivery did not commit its parent "
            + "without charging or creating the contract again.");
        scenario.RetryDelivery();
        Require(
            scenario.Probe.CommitCount(Kind) == 1
            && scenario.SpendCount == 1
            && scenario.MercenaryContractCount == 1,
            "Terminal mercenary delivery replay duplicated a parent, "
            + "contract, or payment.");

        return new G01MercenaryEvidence(
            scenario.CustomerId,
            scenario.DeliveryResultKey,
            firstDailyFee,
            scenario.SpendCount,
            balanceBefore,
            scenario.Balance);
    }

    private readonly struct G01MercenaryEvidence
    {
        internal G01MercenaryEvidence(
            string customerId,
            string deliveryResultKey,
            int fee,
            int spendCount,
            int balanceBefore,
            int balanceAfter)
        {
            CustomerId = customerId;
            DeliveryResultKey = deliveryResultKey;
            Fee = fee;
            SpendCount = spendCount;
            BalanceBefore = balanceBefore;
            BalanceAfter = balanceAfter;
        }

        internal string CustomerId { get; }
        internal string DeliveryResultKey { get; }
        internal int Fee { get; }
        internal int SpendCount { get; }
        internal int BalanceBefore { get; }
        internal int BalanceAfter { get; }
    }

    internal sealed class MercenaryRegularCustomerScenario : IDisposable
    {
        private const int InitialBalance = 10000;
        private GameObject runtimeObject;
        private readonly BuildableObject visitFacility;
        private readonly BuildableObject tavern;
        private readonly ScenarioMoneyAccount money;
        private readonly EmploymentContractRuntime employment;
        private readonly DungeonRuntimeAggregateRootStore aggregateRoot;
        private readonly IGameEventBus events;
        private readonly IGameSessionStateProvider session;
        private readonly IBuildingWorldQuery buildingWorld;
        private readonly IOffenseQuery offense;
        private readonly ISettlementPopulationCapacityQuery populationCapacity;
        private readonly ICharacterPopulationService population;

        internal MercenaryRegularCustomerScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode)
        {
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:g01-mercenary-" + Guid.NewGuid().ToString("N"));
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
            Facts = new RegularCustomerFactEditorFixture(Outcomes, Probe);

            Actor = RegularCustomerScenario.CreateCustomerForEntryFixture();
            CustomerId = RegularCustomerService.GetCustomerId(Actor);
            visitFacility = RegularCustomerScenario.CreateFacilityForEntryFixture();
            tavern = CreateMercenaryTavern();
            buildingWorld = new FixedBuildingWorld(tavern);
            aggregateRoot = new DungeonRuntimeAggregateRootStore();
            money = new ScenarioMoneyAccount(InitialBalance);
            events = new DungeonStory.Foundation.GameEventBus();
            session = new FixedGameSessionStateProvider(20);
            offense = new FixedOffenseQuery();
            populationCapacity = new AcceptingPopulationCapacity();
            population = DefaultProxy.Create<ICharacterPopulationService>();
            employment = new EmploymentContractRuntime(
                DefaultProxy.Create<ICharacterWorldQuery>(),
                DefaultProxy.Create<ICharacterWorldPersistenceIdentityQuery>(),
                DefaultProxy.Create<ICombatEquipmentRuntime>(),
                money,
                events,
                new TreasuryEconomyAggregateStateStore(aggregateRoot));

            runtimeObject = new GameObject("G01 Mercenary Recruitment E2E");
            Runtime = AddRuntime(runtimeObject);
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal RegularCustomerFactEditorFixture Facts { get; }
        internal RegularCustomerRuntime Runtime { get; private set; }
        internal CharacterActor Actor { get; }
        internal string CustomerId { get; }
        private RegularCustomerRecord Record => Runtime.State.TryGetRecord(
                CustomerId,
                out RegularCustomerRecord record)
            ? record
            : null;
        internal bool IsRecruited => Record?.IsRecruited == true;
        internal bool DeliveryPending => Record?.RecruitDeliveryPending == true;
        internal bool DeliveryApplied => Record?.RecruitDeliveryApplied == true;
        internal string DeliveryActorId =>
            Record?.RecruitDeliveryActorId ?? string.Empty;
        internal string DeliveryResultKey =>
            Record?.RecruitDeliveryOutcomeResultKey ?? string.Empty;
        internal int SpendCount => money.SpendCount;
        internal int LastSpentAmount => money.LastSpentAmount;
        internal int Balance => money.Balance;
        internal int MercenaryContractCount => employment.MercenaryContracts.Count;

        internal void MakeRecruitCandidate()
        {
            for (int index = 0; index < 4; index++)
            {
                Runtime.OnTriggerEvent(new FacilityVisitEvent(
                    CharacterActor.From(Actor),
                    visitFacility));
            }
            Require(
                Record?.IsRecruitCandidate == true,
                "The production visit path did not create a G01 mercenary candidate.");
        }

        internal bool Hire(out int firstDailyFee) =>
            Runtime.TryHireMercenary(CustomerId, out _, out firstDailyFee);

        internal void RetryDelivery() =>
            Runtime.RetryPendingRecruitDeliveriesForDebug();

        internal string CaptureSaveJson()
        {
            var catalog = new FixedRunCharacterCatalog(Actor.data);
            var adapter = new RegularCustomerPersistenceAdapter(Runtime, catalog);
            return new RegularCustomerSaveSection(adapter, adapter).Capture();
        }

        internal void RestoreSaveJson(string payloadJson)
        {
            GameObject replacement = new("G01 Mercenary Recruitment Restored");
            RegularCustomerRuntime restored = AddRuntime(replacement);
            var catalog = new FixedRunCharacterCatalog(Actor.data);
            var adapter = new RegularCustomerPersistenceAdapter(restored, catalog);
            var section = new RegularCustomerSaveSection(adapter, adapter);
            var report = new DungeonGameRestoreReport();
            section.Restore(payloadJson, section.SectionVersion, report);
            Require(
                report.Success,
                "Pending G01 mercenary delivery restore failed: "
                + string.Join(" | ", report.Errors));
            Object.DestroyImmediate(runtimeObject);
            runtimeObject = replacement;
            Runtime = restored;
        }

        private RegularCustomerRuntime AddRuntime(GameObject host)
        {
            RegularCustomerRuntime runtime =
                host.AddComponent<RegularCustomerRuntime>();
            runtime.ConstructRecruitmentRuntime(
                new DungeonStory.Foundation.UnityUiClock(),
                new RegularCustomerCharacterServices(
                    new RecruitActivation(Actor),
                    population),
                events,
                employment,
                buildingWorld,
                session,
                money,
                offense,
                populationCapacity,
                aggregateRoot,
                Facts.Publisher,
                Facts.Recovery);
            return runtime;
        }

        private static BuildableObject CreateMercenaryTavern()
        {
            BuildingSO source = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Modular/D12_술음료장.asset")
                ?? throw new InvalidOperationException(
                    "The authored D12 mercenary tavern is missing.");
            BuildingSO data = Object.Instantiate(source);
            GameObject gameObject = new("G01 Mercenary Tavern");
            BuildableObject building = gameObject.AddComponent<BuildableObject>();
            building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(building);
            building.Initialization(data, new Vector2Int(2, 0));
            Require(
                building.BuildingData?
                    .GetAbility<BuildingMercenaryHiringAbility>() != null,
                "The authored D12 fixture has no mercenary hiring ability.");
            return building;
        }

        public void Dispose()
        {
            Facts.Dispose();
            if (visitFacility?.BuildingData != null)
                Object.DestroyImmediate(visitFacility.BuildingData);
            if (visitFacility != null)
                Object.DestroyImmediate(visitFacility.gameObject);
            if (tavern?.BuildingData != null)
                Object.DestroyImmediate(tavern.BuildingData);
            if (tavern != null)
                Object.DestroyImmediate(tavern.gameObject);
            if (Actor?.data != null)
                Object.DestroyImmediate(Actor.data);
            if (Actor != null)
                Object.DestroyImmediate(Actor.gameObject);
            if (runtimeObject != null)
                Object.DestroyImmediate(runtimeObject);
        }
    }

    private sealed class FixedBuildingWorld : IBuildingWorldQuery
    {
        internal FixedBuildingWorld(params BuildableObject[] buildings) =>
            Buildings = buildings ?? Array.Empty<BuildableObject>();

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    private sealed class AcceptingPopulationCapacity :
        ISettlementPopulationCapacityQuery
    {
        public SettlementPopulationCapacitySnapshot CapturePopulationCapacity() =>
            default;

        public SettlementPopulationAcceptance EvaluateImmigration(
            SettlementImmigrationPolicy policy) =>
            new(true, string.Empty, "G01 controlled capacity accepted.");
    }

    private sealed class FixedOffenseQuery : IOffenseQuery
    {
        public OffenseCampaignSnapshot Capture() => new()
        {
            IsAvailable = true,
            CompletedTargetCount = 0
        };

        public bool TryGetTargetDefinition(
            string targetId,
            out OffenseTargetDefinition definition)
        {
            definition = null;
            return false;
        }
    }

    private sealed class ScenarioMoneyAccount : IGameMoneyAccount
    {
        internal ScenarioMoneyAccount(int balance) => Balance = balance;

        public int Balance { get; private set; }
        internal int SpendCount { get; private set; }
        internal int LastSpentAmount { get; private set; }

        public bool CanSpend(int amount) => amount >= 0 && Balance >= amount;

        public bool TrySpend(int amount, out string reason) =>
            TrySpend(amount, default, out reason);

        public bool TrySpend(
            int amount,
            EconomyTransactionContext context,
            out string reason)
        {
            if (!CanSpend(amount))
            {
                reason = "insufficient-funds";
                return false;
            }
            Balance -= amount;
            SpendCount++;
            LastSpentAmount = amount;
            reason = string.Empty;
            return true;
        }

        public void Add(int amount) => Balance += amount;

        public void Add(int amount, EconomyTransactionContext context) =>
            Add(amount);

        public void SetBalance(
            int amount,
            EconomyTransactionContext context) => Balance = Math.Max(0, amount);
    }

    internal sealed class RegularCustomerScenario : IDisposable
    {
        private GameObject runtimeObject;
        private readonly BuildableObject facility;

        internal RegularCustomerScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-regular-" + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
            Facts = new RegularCustomerFactEditorFixture(Outcomes, Probe);

            Actor = CreateCustomerForEntryFixture();
            runtimeObject = new GameObject("Migrated Regular Customer E2E");
            Runtime = runtimeObject.AddComponent<RegularCustomerRuntime>();
            Runtime.ConstructRecruitmentRuntime(
                new DungeonStory.Foundation.UnityUiClock(),
                new RecruitActivation(Actor),
                new DungeonStory.Foundation.GameEventBus(),
                new FixedGameSessionStateProvider(20),
                Facts.Publisher,
                Facts.Recovery);
            facility = CreateFacilityForEntryFixture();
            CustomerId = RegularCustomerService.GetCustomerId(Actor);
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal RegularCustomerFactEditorFixture Facts { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal RegularCustomerRuntime Runtime { get; private set; }
        internal CharacterActor Actor { get; }
        internal string CustomerId { get; }
        internal int VisitCount => Runtime.State.TryGetRecord(
                CustomerId,
                out RegularCustomerRecord record)
            ? record.VisitCount
            : 0;
        internal bool IsRecruited => Runtime.State.IsRecruited(CustomerId);
        private RegularCustomerRecord Record => Runtime.State.TryGetRecord(
                CustomerId,
                out RegularCustomerRecord record)
            ? record
            : null;
        internal bool DeliveryPending => Record?.RecruitDeliveryPending == true;
        internal bool DeliveryApplied => Record?.RecruitDeliveryApplied == true;
        internal string DeliveryActorId =>
            Record?.RecruitDeliveryActorId ?? string.Empty;
        internal string DeliveryResultKey =>
            Record?.RecruitDeliveryOutcomeResultKey ?? string.Empty;

        internal void Visit() => Runtime.OnTriggerEvent(
            new FacilityVisitEvent(CharacterActor.From(Actor), facility));

        internal void MakeRecruitCandidate()
        {
            for (int index = 0; index < 4; index++)
                Visit();
            Require(
                Runtime.State.TryGetRecord(
                    CustomerId,
                    out RegularCustomerRecord record)
                && record.IsRecruitCandidate,
                "The production visit path did not create a recruit candidate.");
        }

        internal bool Recruit() => Runtime.TryRecruit(CustomerId, out _);

        internal void RetryDelivery() =>
            Runtime.RetryPendingRecruitDeliveriesForDebug();

        internal string CaptureSaveJson()
        {
            var catalog = new FixedRunCharacterCatalog(Actor.data);
            var adapter = new RegularCustomerPersistenceAdapter(Runtime, catalog);
            return new RegularCustomerSaveSection(adapter, adapter).Capture();
        }

        internal void RestoreSaveJson(string payloadJson)
        {
            GameObject replacement = new("Migrated Regular Customer E2E Restored");
            RegularCustomerRuntime restored =
                replacement.AddComponent<RegularCustomerRuntime>();
            restored.ConstructRecruitmentRuntime(
                new DungeonStory.Foundation.UnityUiClock(),
                new RecruitActivation(Actor),
                new DungeonStory.Foundation.GameEventBus(),
                new FixedGameSessionStateProvider(20),
                Facts.Publisher,
                Facts.Recovery);
            var catalog = new FixedRunCharacterCatalog(Actor.data);
            var adapter = new RegularCustomerPersistenceAdapter(restored, catalog);
            var section = new RegularCustomerSaveSection(adapter, adapter);
            var report = new DungeonGameRestoreReport();
            section.Restore(payloadJson, section.SectionVersion, report);
            Require(report.Success,
                "Pending recruit delivery save restore failed: "
                + string.Join(" | ", report.Errors));
            Object.DestroyImmediate(runtimeObject);
            runtimeObject = replacement;
            Runtime = restored;
        }

        public void Dispose()
        {
            Facts.Dispose();
            if (facility?.BuildingData != null)
                Object.DestroyImmediate(facility.BuildingData);
            if (facility != null)
                Object.DestroyImmediate(facility.gameObject);
            if (Actor?.data != null)
                Object.DestroyImmediate(Actor.data);
            if (Actor != null)
                Object.DestroyImmediate(Actor.gameObject);
            if (runtimeObject != null)
                Object.DestroyImmediate(runtimeObject);
        }

        internal static CharacterActor CreateCustomerForEntryFixture(
            bool includeMovement = false)
        {
            GameObject gameObject = new("Migrated Regular Customer");
            gameObject.AddComponent<SpriteRenderer>();
            CharacterAiEditorTestDependencies.EnsureCharacterProgression(
                gameObject);
            CharacterActor actor = gameObject.AddComponent<CharacterActor>();
            if (includeMovement)
            {
                gameObject.AddComponent<AbilityMove>();
                gameObject.AddComponent<AbilityWork>();
                gameObject.AddComponent<AbilityShopping>();
            }
            CharacterSO data =
                CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
                    CharacterType.Customer,
                    "원장 검증 손님",
                    "Slime");
            CharacterAiEditorTestDependencies.Inject(gameObject);
            actor.Initialization(data);
            actor.characterType = CharacterType.Customer;
            actor.Identity.SetPersistentId(
                "character:world:migrated-e2e:regular-customer");
            actor.stats ??= new Dictionary<CharacterCondition, float>();
            actor.stats[CharacterCondition.HUNGER] = 100f;
            actor.stats[CharacterCondition.SLEEP] = 100f;
            actor.stats[CharacterCondition.FUN] = 100f;
            actor.stats[CharacterCondition.MOOD] = 90f;
            return actor;
        }

        internal static BuildableObject CreateFacilityForEntryFixture()
        {
            GameObject gameObject = new("Migrated Regular Customer Facility");
            BuildableObject facility = gameObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            data.id = 9910;
            data.objectName = "원장 검증 시설";
            data.width = 1;
            data.height = 1;
            data.category = BuildingCategory.Shop;
            data.Facility = new FacilityData
            {
                roles = FacilityRole.Meal,
                capacity = 1
            };
            data.Facility.SetSupportedWorkTypeIds(
                new[] { BuiltInWorkTypeIds.Operate });
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(data, Vector2Int.zero);
            return facility;
        }
    }

    private sealed class FacilityVisitScenario : IDisposable
    {
        private readonly CharacterActor visitor;
        private readonly BuildableObject facility;
        private readonly BuildingVisitEventPublisher publisher;
        private readonly EditorFacilityUseFactFixture facilityUseFacts;

        internal FacilityVisitScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-facility-visit-"
                + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
            facilityUseFacts = new EditorFacilityUseFactFixture(
                Outcomes,
                Probe);
            publisher = new BuildingVisitEventPublisher(
                new DungeonStory.Foundation.GameEventBus(),
                new FixedGameSessionStateProvider(7),
                facilityUseFacts.Facts,
                facilityUseFacts.Facts);
            visitor = RegularCustomerScenario.CreateCustomerForEntryFixture();
            facility = RegularCustomerScenario.CreateFacilityForEntryFixture();
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal int CompletedUses => facility.FacilityState.completedUses;
        internal int PendingUses => facility.FacilityState.pendingUses.Count;
        internal string ActorId => visitor.Identity.PersistentId;
        internal string FacilityId =>
            facility.RequirePersistentInstanceId().Value;

        internal void CommitVisit() => publisher.CommitVisit(
            CharacterActor.From(visitor),
            facility);

        internal FacilityVisitCommitResult CommitDetailed(
            FacilityRole role,
            CharacterFacilityUseOutcomeStatus status,
            bool effectsCommitted,
            string failureCode,
            BuildingVisitorSnapshot before,
            BuildingVisitorSnapshot after,
            IReadOnlyList<string> childResultKeys,
            string serviceSessionId)
        {
            facility.Facility.roles = role;
            long sequence = facility.AllocateFacilityUseOperationSequence();
            return publisher.CommitVisit(
                CharacterActor.From(visitor),
                facility,
                new CharacterFacilityUseOutcomeReceipt
                {
                    OperationSequence = sequence,
                    OperationId = "facility-visit:facility=" + FacilityId
                        + ":use=" + sequence,
                    ActorId = ActorId,
                    ActorDisplayName = visitor.BuildingDisplayName,
                    FacilityRole = role.ToString(),
                    ServiceSessionId = serviceSessionId,
                    Status = status,
                    EffectsCommitted = effectsCommitted,
                    FailureCode = failureCode,
                    Before = before,
                    After = after,
                    ChildResultKeys = childResultKeys
                });
        }

        internal GameplayOutcomeSnapshot RequireLatestExact()
        {
            GameplayResultKey key = Probe.LatestCommittedKey(
                MigratedProducerOutcomeKind.FacilityVisitEvent);
            Require(
                key.IsValid,
                "The detailed facility parent did not retain a valid result key.");
            Require(
                Outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity),
                "The detailed facility parent identity was not queryable.");
            Require(
                Outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact),
                "The detailed facility parent snapshot was not queryable.");
            return exact;
        }

        internal void RoundTripFacilityState()
        {
            var module = new FacilityRuntimeStateModule(facility);
            string payload = module.CaptureState();
            Require(
                module.TryRestoreState(
                    module.CurrentVersion,
                    payload,
                    out string error),
                "Facility use pending state did not survive JSON restore: "
                + error);
        }

        internal void AllowCommitAndReconcile()
        {
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            FacilityVisitCommitResult result =
                publisher.ReconcilePendingVisit(facility);
            Require(
                result.DurablyCommitted,
                "Facility parent retry remained deferred: "
                + result.DetailCode);
        }

        public void Dispose()
        {
            facilityUseFacts?.Dispose();
            if (facility?.BuildingData != null)
                Object.DestroyImmediate(facility.BuildingData);
            if (facility != null)
                Object.DestroyImmediate(facility.gameObject);
            if (visitor?.data != null)
                Object.DestroyImmediate(visitor.data);
            if (visitor != null)
                Object.DestroyImmediate(visitor.gameObject);
        }
    }

    private sealed class ActualFacilityUseScenario : IDisposable
    {
        private readonly Facility facility;
        private readonly BuildingSO facilityData;
        private readonly BuildingVisitEventPublisher publisher;
        private readonly EditorFacilityUseFactFixture facilityUseFacts;
        private readonly CharacterActor realActor;
        private readonly AIRest resumeAction;
        private Stack<IEnumerator> pausedStack;

        internal ActualFacilityUseScenario(
            MigratedProducerOutcomeFaultMode faultMode,
            bool useRealCharacter,
            bool canMove = true,
            bool replaceActionDuringMove = false,
            bool resumableAction = false,
            float useDuration = 0f, bool pendingMeal = false)
        {
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:a01-facility-interact-" + Guid.NewGuid().ToString("N"));
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = MigratedProducerOutcomeKind.FacilityVisitEvent,
                FaultMode = faultMode
            };

            var events = new GameEventBus();
            facilityUseFacts = new EditorFacilityUseFactFixture(
                Outcomes,
                Probe);
            publisher = new BuildingVisitEventPublisher(
                events,
                new FixedGameSessionStateProvider(7),
                facilityUseFacts.Facts,
                facilityUseFacts.Facts);
            GameObject facilityObject = new("A01 Facility.Interact Fixture");
            facility = facilityObject.AddComponent<Facility>();
            CharacterAiEditorTestDependencies.InjectWithRoomPolicy(
                facility,
                PermissiveFacilityRoomPolicy.Instance);
            FieldInfo clockField = typeof(BuildableObject).GetField(
                "gameClock",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Require(
                clockField != null,
                "A01 fixture could not locate the injected building clock.");
            clockField.SetValue(facility, new FacilityInteractionClock());
            facility.ConstructBuildableObjectEventBus(
                events,
                publisher,
                new BuildingInfoPresentationAdapter(events));

            facilityData = ScriptableObject.CreateInstance<BuildingSO>();
            facilityData.id = 9911;
            facilityData.objectName = "A01 회복실";
            facilityData.width = 1;
            facilityData.height = 1;
            facilityData.category = BuildingCategory.Shop;
            facilityData.runtimeArchetype = BuildingRuntimeArchetypeKind.Facility;
            facilityData.unlocked = true;
            var abilities = new BuildingAbilityCollection();
            abilities.Add(new BuildingExpeditionRecoveryAbility());
            facilityData.ReplaceAbilities(abilities);
            facilityData.Facility = new FacilityData
            {
                roles = pendingMeal ? FacilityRole.Meal : FacilityRole.Rest,
                capacity = 1,
                useDuration = useDuration
            };
            facilityData.Facility.SetSupportedWorkTypeIds(
                new[] { BuiltInWorkTypeIds.Operate });
            facilityData.AbilityModules.EnsureStableIds();
            facility.Initialization(facilityData, Vector2Int.zero);

            if (useRealCharacter)
            {
                realActor =
                    RegularCustomerScenario.CreateCustomerForEntryFixture(
                        includeMovement: true);
                realActor.name = "A01 실제 시설 이용자";
                realActor.stats[CharacterCondition.HUNGER] = 20f;
                realActor.stats[CharacterCondition.FUN] = 15f;
                realActor.stats[CharacterCondition.SLEEP] = 25f;
                realActor.stats[CharacterCondition.EXCRETION] = 10f;
                realActor.stats[CharacterCondition.HYGIENE] = 35f;
                realActor.stats[CharacterCondition.MOOD] = 40f;
                float maximumHealth = realActor.Stats.MaxHealth;
                Require(
                    maximumHealth > 0f,
                    "A01 real character fixture has no health authority.");
                realActor.ApplyDamage(
                    maximumHealth * 0.3f,
                    "a01-facility-interact-fixture");
                realActor.SetInjurySeverity(0.4f);
                realActor.Lifecycle.ApplyStressDelta(30f);
                Visitor = realActor.BuildingVisitor;
            }
            else
            {
                if (resumableAction) resumeAction = ScriptableObject.CreateInstance<AIRest>();
                ControlledVisitor = new ControlledFacilityVisitor(
                    canMove,
                    replaceActionDuringMove,
                    resumeAction != null ? new AIAction(resumeAction, AIActionPlan.AtDestination(facility)) : null);
                Visitor = ControlledVisitor;
                if (pendingMeal)
                {
                    ControlledVisitor.PendingMeal = true;
                    typeof(Facility).GetField("mealConsumptionRuntime", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(facility, new UnusedControlledMealRuntime());
                }
            }

            Require(
                facility.CanQueueVisit(Visitor, out string admissionFailure),
                "A01 Facility.Interact fixture is not admissible: "
                + admissionFailure);
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal IBuildingVisitorPort Visitor { get; }
        internal ControlledFacilityVisitor ControlledVisitor { get; }
        internal int CompletedUses => facility.FacilityState.completedUses;
        internal int PendingUses => facility.FacilityState.pendingUses.Count;
        internal int ActiveUses => facility.FacilityState.activeUses.Count;
        internal FacilityUseExecutionState CurrentExecution => facility.FacilityState.activeUses.Single();
        internal string ActorId => Visitor.BuildingCharacterId.Value;
        internal string FacilityId =>
            facility.RequirePersistentInstanceId().Value;
        internal string RestChildResultKey =>
            realActor?.Stats?.LastRestOutcomeResultKey ?? string.Empty;

        internal void RunInteract() =>
            DrainRoutine(facility.Interact(Visitor));

        internal void PauseAt(Func<FacilityUseExecutionState, bool> predicate)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(facility.Interact(Visitor));
            for (int step = 0; step < 256 && stack.Count > 0; step++)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext()) stack.Pop();
                else if (current.Current is IEnumerator nested) stack.Push(nested);
                if (ActiveUses == 1 && predicate(CurrentExecution)) { pausedStack = stack; return; }
            }
            throw new InvalidOperationException("Production coroutine did not reach the requested checkpoint.");
        }

        internal void CancelPausedUse()
        {
            ControlledVisitor.EndAction();
            facility.ReleaseTransientCharacterOwnership(Visitor, "qa-suspended-use-cancel");
        }

        internal void RemovePausedOwnerWithoutCancellation()
        {
            FieldInfo stateField = typeof(BuildableObject).GetField(
                "facilityState", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Facility fault fixture could not locate the owned state.");
            FacilityRuntimeState state = stateField.GetValue(facility) as FacilityRuntimeState
                ?? throw new InvalidOperationException("Facility fault fixture has no owned state.");
            state.activeUses.Clear();
        }

        internal void ResumePausedRoutine()
        {
            Require(pausedStack != null, "No suspended facility iterator is owned by this fixture.");
            for (int step = 0; step < 256 && pausedStack.Count > 0; step++)
            {
                IEnumerator current = pausedStack.Peek();
                if (!current.MoveNext()) pausedStack.Pop();
                else if (current.Current is IEnumerator nested) pausedStack.Push(nested);
            }
            Require(pausedStack.Count == 0, "Suspended facility iterator did not stop within its finite budget.");
        }

        internal void RoundTripActiveExecution()
        {
            RoundTripFacilityState();
            // A restored world has no old occupancy or IEnumerator stack.
            // Do not call the live cancellation path: this models publication,
            // not a player cancelling an action.
            facility.EndUse(Visitor);
        }

        internal GameplayOutcomeSnapshot RequireLatestExact()
        {
            GameplayResultKey key = Probe.LatestCommittedKey(
                MigratedProducerOutcomeKind.FacilityVisitEvent);
            Require(
                key.IsValid,
                "Facility.Interact did not retain a valid parent result key.");
            Require(
                Outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity),
                "Facility.Interact parent identity was not queryable.");
            Require(
                Outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact),
                "Facility.Interact parent snapshot was not queryable.");
            return exact;
        }

        internal void RoundTripFacilityState()
        {
            var module = new FacilityRuntimeStateModule(facility);
            string payload = module.CaptureState();
            Require(
                module.TryRestoreState(
                    module.CurrentVersion,
                    payload,
                    out string error),
                "Actual Facility.Interact pending state did not survive JSON restore: "
                + error);
        }

        internal void AllowCommitAndReconcile()
        {
            Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            FacilityVisitCommitResult result =
                publisher.ReconcilePendingVisit(facility);
            Require(
                result.DurablyCommitted,
                "Actual Facility.Interact parent retry remained deferred: "
                + result.DetailCode);
        }

        private static void DrainRoutine(IEnumerator root)
        {
            Require(root != null, "Facility.Interact returned no coroutine.");
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            int steps = 0;
            while (stack.Count > 0)
            {
                Require(
                    ++steps <= 4096,
                    "Facility.Interact exceeded the deterministic coroutine budget.");
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    stack.Pop();
                }
                else if (current.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }
            }
        }

        public void Dispose()
        {
            facilityUseFacts?.Dispose();
            if (resumeAction != null) Object.DestroyImmediate(resumeAction);
            if (realActor?.data != null)
                Object.DestroyImmediate(realActor.data);
            if (realActor != null)
                Object.DestroyImmediate(realActor.gameObject);
            if (facilityData != null)
                Object.DestroyImmediate(facilityData);
            if (facility != null)
                Object.DestroyImmediate(facility.gameObject);
        }
    }

    private sealed class FacilityInteractionClock : IGameClock
    {
        public float DeltaTime => 1f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    // The controlled visitor owns its explicit pending result. Any accidental
    // direct call into this marker runtime is a test failure, never a fake meal.
    private sealed class UnusedControlledMealRuntime : IMealConsumptionRuntime
    {
        public bool HasMealAvailable(CharacterActor actor, BuildableObject facility,
            out CharacterConsumablesFailure failure) => throw new InvalidOperationException("Unexpected marker query.");
        public bool TryGetMealOperationResult(ConsumableOperationId operationId,
            out MealConsumptionResult result) => throw new InvalidOperationException("Unexpected marker query.");
        public bool TryConsumeMeal(CharacterActor actor, BuildableObject facility,
            out MealConsumptionResult result) => throw new InvalidOperationException("Unexpected marker command.");
        public bool TryConsumeMeal(ConsumeMealCommand command,
            out MealConsumptionResult result) => throw new InvalidOperationException("Unexpected marker command.");
    }

    private sealed class PermissiveFacilityRoomPolicy : IBuildingRoomPolicyPort
    {
        internal static readonly PermissiveFacilityRoomPolicy Instance = new();

        public bool IsFacilityRoleAvailable(
            IBuildingWorldEntryPort building,
            FacilityRole requestedRole,
            out string rejectReason)
        {
            rejectReason = string.Empty;
            return true;
        }

        public float GetRoomUtilityScore(
            IBuildingWorldEntryPort building,
            FacilityRole role) => 1f;

        public int GetEffectiveCapacity(IBuildingWorldEntryPort building) => 1;

        public BuildingRoomOperationalSnapshot GetOperationalProfile(
            IBuildingWorldEntryPort building) => new(
            Array.Empty<IBuildingWorldEntryPort>(),
            hasRoom: false,
            isUsableRoom: true,
            qualityScore: 1f,
            seatCapacity: 0,
            tableCapacity: 0,
            serviceCapacity: 1,
            StockCategory.General,
            new Dictionary<StockCategory, int>());
    }

    private sealed class ControlledFacilityVisitor : IBuildingVisitorPort
    {
        private readonly CharacterId characterId = new(
            "character:world:a01-controlled:" + Guid.NewGuid().ToString("N"));
        private readonly bool canMove;
        private readonly bool replaceActionDuringMove;
        private readonly object actionToken;
        private readonly FacilityUseValues values = new();
        private Vector3 position;
        private bool actionEnded;
        private object workerPoseToken;

        internal ControlledFacilityVisitor(
            bool canMove,
            bool replaceActionDuringMove,
            AIAction resumableAction = null)
        {
            this.canMove = canMove;
            this.replaceActionDuringMove = replaceActionDuringMove;
            actionToken = resumableAction != null ? resumableAction
                : replaceActionDuringMove ? new object() : null;
        }

        internal BuildingInteractionFailureKind LastFailureKind { get; private set; }
        internal BuildingInteractionFailureKind LastCancellationKind { get; private set; }
        internal int RecoveryCalls { get; private set; }
        internal int UseCompletionCalls { get; private set; }
        internal bool PendingMeal { get; set; }
        internal void EndAction() => actionEnded = true;

        public CharacterId BuildingCharacterId => characterId;
        public string BuildingDisplayName => "A01 통제 방문자";
        public bool IsBuildingInteractionAvailable => true;
        public BuildingVisitorSnapshot VisitorSnapshot => new(
            characterId.Value,
            BuildingDisplayName,
            position,
            isRuntimeActive: true,
            isInternalStaff: false,
            canMove,
            stayDurationMultiplier: 1f,
            revenueMultiplier: 1f,
            personalityPatience: 1f,
            modelPatience: 1f,
            crimeRiskMultiplier: 1f,
            productionOutputMultiplier: 1f,
            stockProductionBonus: 0,
            expeditionStress: values.Stress,
            mood: values.Mood,
            hunger: values.Hunger,
            fun: values.Fun,
            sleep: values.Sleep,
            excretion: values.Excretion,
            hygiene: values.Hygiene,
            currentHealth: values.Health,
            maximumHealth: values.MaximumHealth,
            injurySeverity: values.Injury,
            activeConditionIds: new[] { "condition:test-fatigue" });
        public IBuildingShoppingVisitorPort Shopping => null;
        public object CurrentActionToken => actionToken;
        public bool IsCurrentActionEnded => actionEnded;

        public bool IsCurrentAction(object expectedAction) =>
            expectedAction == null
            || (!actionEnded && ReferenceEquals(actionToken, expectedAction));

        public void SetActionPhase(
            string phase,
            IBuildingWorldEntryPort destination,
            string detail = null)
        {
        }

        public void NotifyFacilityQueueHeartbeat(int queuePosition)
        {
        }

        public void NotifyFacilityServiceHeartbeat()
        {
        }

        public void ReportInteractionFailure(
            BuildingInteractionFailureKind failureKind,
            string detail,
            IBuildingWorldEntryPort destination) =>
            LastFailureKind = failureKind;

        public void ReportInteractionCancellation(
            BuildingInteractionFailureKind failureKind,
            string detail,
            IBuildingWorldEntryPort destination) =>
            LastCancellationKind = failureKind;

        public IEnumerator MoveTo(
            Vector3 target,
            float speed,
            object expectedAction) => MoveToRoutine(target);

        public IEnumerator MoveToWithResult(
            Vector3 target,
            float speed,
            object expectedAction,
            Action<string> completed)
        {
            yield return MoveToRoutine(target);
            completed(string.Empty);
        }

        private IEnumerator MoveToRoutine(Vector3 target)
        {
            position = target;
            if (replaceActionDuringMove)
                actionEnded = true;
            yield break;
        }

        public IEnumerator MoveToGrid(Vector2Int target)
        {
            position = new Vector3(target.x, target.y, position.z);
            yield break;
        }

        public void SetWorldPosition(Vector3 target)
        {
            workerPoseToken = null;
            position = target;
        }

        public void BeginWorkerPose(object allocation) =>
            workerPoseToken = allocation;

        public void EndWorkerPose(object allocation, Vector3? restoredPosition)
        {
            if (!ReferenceEquals(workerPoseToken, allocation))
                return;

            workerPoseToken = null;
            if (restoredPosition.HasValue)
                SetWorldPosition(restoredPosition.Value);
            ChangeLayer("Default");
        }

        public void HideForTraversal(float failSafeDelay)
        {
        }

        public void RestoreTraversalVisibility()
        {
        }

        public void ChangeLayer(string layerName)
        {
            workerPoseToken = null;
        }

        public void FaceRight()
        {
        }

        public void ApplyMoodFactor(
            string sourceId,
            string description,
            float amount,
            float duration,
            int stackLimit) =>
            values.Mood = Mathf.Clamp(values.Mood + amount, 0f, 100f);

        public void RecordActivity(
            IBuildingWorldEntryPort facility,
            BuildingActivitySnapshot activity)
        {
        }

        public void RememberFacilityExperience(
            IBuildingWorldEntryPort facility,
            float sentiment,
            string detail)
        {
        }

        public void ApplyNeedRecovery(BuildingNeedRecoverySnapshot recovery)
        {
            RecoveryCalls++;
            values.Sleep = Mathf.Clamp(values.Sleep + recovery.Sleep, 0f, 100f);
            values.Mood = Mathf.Clamp(values.Mood + recovery.Mood, 0f, 100f);
            values.Fun = Mathf.Clamp(values.Fun + recovery.Fun, 0f, 100f);
            values.Hunger = Mathf.Clamp(values.Hunger + recovery.Hunger, 0f, 100f);
            values.Excretion = Mathf.Clamp(
                values.Excretion + recovery.Excretion,
                0f,
                100f);
            values.Hygiene = Mathf.Clamp(
                values.Hygiene + recovery.Hygiene,
                0f,
                100f);
        }

        public BuildingNeedRecoveryResult ApplyNeedRecoveryDetailed(
            BuildingNeedRecoverySnapshot recovery)
        {
            ApplyNeedRecovery(recovery);
            return new BuildingNeedRecoveryResult(Array.Empty<string>());
        }

        public bool TryConsumeMeal(
            object mealRuntime,
            IBuildingWorldEntryPort facility,
            out BuildingMealUseSnapshot result)
        {
            if (PendingMeal)
            {
                result = new BuildingMealUseSnapshot(false, string.Empty, "Controlled pending meal", 0,
                    acceptedPending: true, operationId: "meal:controlled-pending");
                return false;
            }
            result = new BuildingMealUseSnapshot(
                false,
                "meal-not-configured",
                string.Empty,
                0);
            return false;
        }

        public bool TryGetMealConsumptionResult(
            object mealRuntime,
            string operationId,
            out BuildingMealUseSnapshot result) =>
            TryConsumeMeal(mealRuntime, null, out result);

        public bool TryConsumeRecreationalSubstance(
            IBuildingWorldEntryPort facility,
            out BuildingRecreationalSubstanceUseSnapshot result)
        {
            result = new BuildingRecreationalSubstanceUseSnapshot(
                false,
                "substance-not-configured",
                string.Empty,
                false,
                false);
            return false;
        }

        public void ApplyRoomExperience(
            object roomExperienceRuntime,
            IBuildingWorldEntryPort facility,
            string activityId)
        {
        }

        public BuildingRoomExperienceResult ApplyRoomExperienceDetailed(
            object roomExperienceRuntime,
            IBuildingWorldEntryPort facility,
            string activityId) => new(false, Array.Empty<string>());

        public void ApplyFacilityUseCompleted(IBuildingWorldEntryPort facility)
        {
            UseCompletionCalls++;
            if (facility is BuildableObject building)
                ModularFacilityRuntimeEffects.ApplyUseCompleted(this, building);
        }

        public void ApplyExpeditionRecovery(
            float healthHealRatio,
            float injuryReduction,
            float stressRecovery)
        {
            values.Health = Mathf.Clamp(
                values.Health + values.MaximumHealth * Mathf.Max(0f, healthHealRatio),
                0f,
                values.MaximumHealth);
            values.Injury = Mathf.Max(0f, values.Injury - injuryReduction);
            values.Stress = Mathf.Clamp(
                values.Stress - Mathf.Max(0f, stressRecovery),
                0f,
                100f);
        }

        public void AddExperience(int amount)
        {
        }

        public void ApplyNeedDelta(string needId, float amount)
        {
        }

        public void AddCarriedItem(
            string sourceId,
            string itemDefinitionId,
            int quantity)
        {
        }
    }

    private sealed class ServiceModeScenario : IDisposable
    {
        private readonly ServiceSessionRuntime runtime;
        private readonly DomainFactDispatcher modeFacts;
        private readonly DungeonRuntimeAggregateRootStore modeStore;
        private readonly BuildableObject hub;
        private readonly BuildingSO hubData;

        internal ServiceModeScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false,
            bool? unknownModeReply = null,
            string fixedHubId = null)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-service-mode-"
                + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
            modeFacts = CreateServiceModeFacts(Outcomes, Probe);

            CoreSessionRulesSO rulesAsset =
                AssetDatabase.LoadAssetAtPath<CoreSessionRulesSO>(
                    "Assets/Resources/SO/Content/CoreSessionRules.asset")
                ?? throw new InvalidOperationException(
                    "Authored core-session rules are missing.");
            modeStore = new DungeonRuntimeAggregateRootStore();
            var restoreCandidates = new RestoreWorldCandidateIndex();
            runtime = new ServiceSessionRuntime(
                new DungeonStory.Foundation.UnityUiClock(),
                DefaultProxy.Create<IBuildingWorldQuery>(),
                DefaultProxy.Create<IServiceRoomLinkRuntime>(),
                DefaultProxy.Create<IServiceProcessCatalog>(),
                new ServiceSessionRuntime.Dependencies(
                    new DungeonStory.Foundation.UnityGameClock(),
                    DefaultProxy.Create<IGameMoneyAccount>(),
                    DefaultProxy.Create<IPowerInfrastructureQuery>(),
                    DefaultProxy.Create<IServiceRoomResearchQuery>(),
                    new FixedRulesProvider(rulesAsset.CreateRuntimeDefinition()),
                    new FixedGameSessionStateProvider(9),
                    unknownModeReply.HasValue ? (IDomainFactPublisher)new ModeReplyLoss(modeFacts, unknownModeReply.Value) : modeFacts,
                    modeFacts),
                modeStore,
                restoreCandidates,
                DefaultProxy.Create<
                    ICharacterWorldPersistenceIdentityQuery>());

            BuildingSO source = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Modular/D04_배식카운터.asset")
                ?? throw new InvalidOperationException(
                    "The Direct service-hub fixture asset is missing.");
            hubData = Object.Instantiate(source);
            GameObject gameObject = new("Migrated Service Mode Hub");
            hub = gameObject.AddComponent<BuildableObject>();
            hub.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            if (fixedHubId != null) hub.RestorePersistentIdentity(new BuildingInstanceId(fixedHubId));
            CharacterAiEditorTestDependencies.Inject(hub);
            hub.Initialization(hubData, Vector2Int.zero);
            restoreCandidates.SetFacilityCandidate(
                new Grid(1, 1),
                new[] { hub });
            restoreCandidates.SetCharacterCandidate(
                Array.Empty<CharacterActor>());
            string hubId = hub.RequirePersistentInstanceId().Value;
            var save = new ServiceRoomsSaveData();
            save.hubs.Add(new ServiceHubModeSaveData
            {
                hubId = hubId,
                mode = ServiceOperationMode.Managed
            });
            runtime.PublishRestoreCandidate(
                runtime.PrepareRestoreCandidate(save));
            Require(
                Mode == ServiceOperationMode.Managed,
                "The service-mode fixture did not restore its pre-command owner state.");
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal ServiceSessionRuntime ModeRuntime => runtime;
        internal ServiceSessionAggregate ModeAggregate => modeStore.GetOrCreate(() => new ServiceSessionAggregate());
        internal int ModeLeaseCount => modeFacts.ActiveLeaseCount;
        internal ServiceOperationMode Mode =>
            runtime.GetHubSnapshot(hub).Mode;

        internal ServiceModeChangeResult SwitchToDirect() =>
            runtime.SwitchToDirect(hub);

        public void Dispose()
        {
            runtime?.Dispose();
            modeFacts?.Dispose();
            if (hub != null)
                Object.DestroyImmediate(hub.gameObject);
            if (hubData != null)
                Object.DestroyImmediate(hubData);
        }
    }

    private static DomainFactDispatcher CreateServiceModeFacts(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        var dispatcher = new DomainFactDispatcher();
        try
        {
            dispatcher.Subscribe(
                "fixture:service-mode-changed",
                new MigratedProducerDomainFactProbeObserver<
                    ServiceModeChangedFact>(
                    ServiceModeChangedFactMapping.Capture,
                    outcomes.Transaction,
                    new GameplayDomainFactCommitGroup(
                        outcomes.Recorder,
                        outcomes.Ledger),
                    probe));
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    private sealed class PhysicalTransformScenario : IDisposable
    {
        private const string SourceItemId = "material:lumber";
        private readonly WorldItemRepository repository;
        private readonly PhysicalStockQuery stockQuery;
        private readonly DomainFactDispatcher physicalFacts;
        private readonly PhysicalItemTransformService service;
        private readonly string sourceStackId;
        private readonly string outputItemId;
        private readonly Vector2Int position = new(47, 9);

        internal PhysicalTransformScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-physical-transform-"
                + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
            repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            IDungeonItemCatalogProvider catalog =
                EditorItemCatalogFactory.Create();
            var definitions = new GenericDefinitionPhysicalItemMassProjector(
                catalog);
            IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(
                new IPhysicalItemMassProjector[] { definitions });
            stockQuery = new PhysicalStockQuery(repository, catalog, massQuery);
            long sourceMass = massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)SourceItemId).Value;
            outputItemId = catalog.All
                .Where(definition => definition != null
                    && definition.MaxStack > 1
                    && !string.Equals(
                        definition.ItemId,
                        SourceItemId,
                        StringComparison.Ordinal)
                    && !PhysicalItemIds.TryGetEquipmentDefinitionId(
                        definition.ItemId,
                        out _)
                    && !PhysicalItemIds.IsEquipmentModule(definition.ItemId)
                    && massQuery.GetDefinitionUnitMass(
                        (ItemDefinitionId)definition.ItemId).Value <= sourceMass)
                .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
                .Select(definition => definition.ItemId)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No bounded physical-transform output fixture exists.");
            IItemMarkerPresenter markers =
                EditorNullItemMarkerPresenter.Instance;
            physicalFacts = Outcomes.CreatePhysicalTransformationFacts(Probe);
            service = new PhysicalItemTransformService(
                repository,
                new WorldItemSpawner(catalog, repository, markers),
                massQuery,
                catalog,
                markers,
                new FixedGameSessionStateProvider(11),
                physicalFacts, clock: new DungeonStory.Foundation.UnityUiClock());
            sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                SourceItemId,
                3,
                WorldItemStackState.Loose,
                position: position);
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal int SourceQuantity => stockQuery.GetAllStacks()
            .Where(record => record != null
                && string.Equals(
                    record.StackId,
                    sourceStackId,
                    StringComparison.Ordinal))
            .Sum(record => record.Quantity);
        internal int OutputQuantity => stockQuery.GetAllStacks()
            .Where(record => record != null
                && string.Equals(
                    record.ItemId,
                    outputItemId,
                    StringComparison.Ordinal))
            .Sum(record => record.Quantity);

        internal bool Transform(out PhysicalItemTransformFailureCode code) =>
            service.TryTransformQuantity(
                sourceStackId,
                1,
                new[]
                {
                    new PhysicalItemTransformOutput(
                        outputItemId,
                        1,
                        position)
                },
                "migrated-e2e:physical-transform",
                "migrated-e2e",
                out _,
                out code,
                out _);

        public void Dispose()
        {
            physicalFacts.Dispose();
        }
    }

    private static DomainFactDispatcher CreateMetaFacts(
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        var dispatcher = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(
            outcomes.Recorder,
            outcomes.Ledger);
        try
        {
            dispatcher.Subscribe(
                "fixture:meta-upgrade-purchased",
                new MigratedProducerDomainFactProbeObserver<
                    MetaUpgradePurchaseDomainFact>(
                    MetaProgressionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            dispatcher.Subscribe(
                "fixture:meta-run-result",
                new MigratedProducerDomainFactProbeObserver<
                    MetaRunResultDomainFact>(
                    MetaProgressionFactMapping.Capture,
                    outcomes.Transaction,
                    commits,
                    probe));
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    private sealed class MetaPurchaseScenario : IDisposable
    {
        private const string UpgradeId = "meta:migrated-e2e:upgrade";
        private readonly GameObject runtimeObject;
        private readonly MetaProgressionRuntime runtime;
        private readonly DomainFactDispatcher domainFacts;

        internal MetaPurchaseScenario(
            MigratedProducerOutcomeKind target,
            MigratedProducerOutcomeFaultMode faultMode =
                MigratedProducerOutcomeFaultMode.None,
            bool deliveryFault = false)
        {
            DeliveryFault = new MigratedProducerOutcomeDeliveryFault
            {
                TargetKind = target,
                Enabled = deliveryFault
            };
            Outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:migrated-entry-meta-purchase-"
                + Guid.NewGuid().ToString("N"),
                deliveryFault: DeliveryFault);
            Probe = new MigratedProducerOutcomeProbeTransaction(
                Outcomes.Transaction,
                Outcomes.Recorder)
            {
                TargetKind = target,
                FaultMode = faultMode
            };
            domainFacts = CreateMetaFacts(Outcomes, Probe);
            var catalog = new SingleMetaCatalog(new MetaUpgradeDefinition(
                UpgradeId,
                MetaProgressionBranch.OperationKnowledge,
                "원장 검증 강화",
                "실제 구매 진입 검증",
                10,
                1,
                Array.Empty<IMetaUpgradeEffect>()));
            runtimeObject = new GameObject("Migrated Meta Purchase E2E");
            runtime = runtimeObject.AddComponent<MetaProgressionRuntime>();
            runtime.Construct(
                new MetaRunResultBuilder(),
                new MetaApplicationPort(),
                new DungeonStory.Foundation.UnityGameClock(),
                catalog,
                new DungeonRuntimeAggregateRootStore());
            runtime.ConstructDomainFacts(domainFacts);
            runtime.AddCurrencyForDebug(100);
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        internal MigratedProducerOutcomeDeliveryFault DeliveryFault { get; }
        internal int Level => runtime.State.GetUpgradeLevel(UpgradeId);
        internal int Currency => runtime.State.AvailableCurrency;

        internal bool Purchase() =>
            runtime.TryPurchaseUpgrade(UpgradeId, out _);

        public void Dispose()
        {
            domainFacts.Dispose();
            if (runtimeObject != null)
                Object.DestroyImmediate(runtimeObject);
        }
    }

    private sealed class SingleMetaCatalog : IMetaUpgradeDefinitionCatalog
    {
        private readonly MetaUpgradeDefinition definition;

        internal SingleMetaCatalog(MetaUpgradeDefinition definition) =>
            this.definition = definition
                ?? throw new ArgumentNullException(nameof(definition));

        public IReadOnlyCollection<MetaUpgradeDefinition> All =>
            new[] { definition };

        public MetaUpgradeDefinition Get(string id) =>
            string.Equals(definition.id, id, StringComparison.Ordinal)
                ? definition
                : null;

        public MetaUpgradeDefinition Require(string id) =>
            Get(id) ?? throw new KeyNotFoundException(id);
    }

    private sealed class MetaApplicationPort : IMetaRuntimeApplicationPort
    {
        public void Bind(IMetaRuntimeEventSink runtime)
        {
        }

        public void Unbind(IMetaRuntimeEventSink runtime)
        {
        }

        public MetaRunEnvironmentSnapshot CaptureRunEnvironment() =>
            new(1f, DungeonDifficulty.Normal,
                DungeonSurvivalPressure.Standard);

        public void PublishUpgradePurchased(
            MetaUpgradePurchasedEvent purchasedEvent,
            string message)
        {
        }

        public void PublishRunResult(RunResultReadyEvent readyEvent)
        {
        }

        public void ShowRunResult(RunResultSnapshot result)
        {
        }
    }

    private sealed class FixedRulesProvider : ICoreSessionRulesProvider
    {
        internal FixedRulesProvider(CoreSessionRulesDefinition rules) =>
            CoreSessionRules = rules
                ?? throw new ArgumentNullException(nameof(rules));

        public CoreSessionRulesDefinition CoreSessionRules { get; }
    }

    public class DefaultProxy : DispatchProxy
    {
        public DefaultProxy()
        {
        }

        internal static TContract Create<TContract>()
            where TContract : class =>
            DispatchProxy.Create<TContract, DefaultProxy>();

        protected override object Invoke(
            MethodInfo targetMethod,
            object[] arguments)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (!parameters[index].IsOut && !parameterType.IsByRef)
                    continue;
                Type valueType = parameterType.IsByRef
                    ? parameterType.GetElementType()
                    : parameterType;
                arguments[index] = valueType != null && valueType.IsValueType
                    ? Activator.CreateInstance(valueType)
                    : null;
            }
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            if (returnType.IsArray)
                return Array.CreateInstance(returnType.GetElementType(), 0);
            if (returnType.IsGenericType)
            {
                Type definition = returnType.GetGenericTypeDefinition();
                if (definition == typeof(IReadOnlyList<>)
                    || definition == typeof(IReadOnlyCollection<>)
                    || definition == typeof(IEnumerable<>))
                {
                    return Array.CreateInstance(
                        returnType.GetGenericArguments()[0],
                        0);
                }
            }
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private sealed class RecruitActivation : IRecruitedCharacterActivationService
    {
        private readonly CharacterActor actor;

        internal RecruitActivation(CharacterActor actor) =>
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));

        public bool TryValidateActivation(
            RegularCustomerRecord record,
            out string message)
        {
            bool matches = record != null
                && string.Equals(
                    actor.Identity?.PersistentId,
                    record.CustomerId,
                    StringComparison.Ordinal);
            message = matches ? string.Empty : "actor missing";
            return matches;
        }

        public bool TryActivate(
            RegularCustomerRecord record,
            out CharacterActor actor,
            out string message)
        {
            actor = this.actor;
            if (record != null
                && string.Equals(
                    actor?.Identity?.PersistentId,
                    record.CustomerId,
                    StringComparison.Ordinal))
            {
                actor.characterType = CharacterType.NPC;
                actor.SetLifecycleState(CharacterLifecycleState.Active);
                if (actor.GetComponent<AbilityWork>() == null)
                    actor.gameObject.AddComponent<AbilityWork>();
                actor.RefreshAbilityCache();
            }
            else
            {
                actor = null;
            }
            message = actor == null ? "actor missing" : "activated";
            return actor != null;
        }
    }

    private sealed class FixedRunCharacterCatalog : IRunCharacterCatalog
    {
        internal FixedRunCharacterCatalog(params CharacterSO[] characters) =>
            Characters = characters ?? Array.Empty<CharacterSO>();

        public IReadOnlyCollection<CharacterSO> Characters { get; }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static bool RejectionReachedCommit(
        MigratedProducerOutcomeProbeTransaction probe,
        MigratedProducerOutcomeKind kind) =>
        probe.ReservationCount(kind) > 0
        && probe.CommitAttemptCount(kind) > 0
        && probe.CommitCount(kind) == 0;

    private static void RequireThrows<TException>(Action action)
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
        throw new InvalidOperationException(
            "The production entry did not surface the injected "
            + typeof(TException).Name + ".");
    }
}
#endif
