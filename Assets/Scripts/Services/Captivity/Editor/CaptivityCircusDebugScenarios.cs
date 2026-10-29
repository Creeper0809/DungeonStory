#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CaptivityCircusDebugScenarios
{
    private const string ReportPath = "Temp/captivity-circus-contracts.tsv";

    internal static string RunLaborContractsForArchitectureBatch() =>
        VerifyCaptiveThresholds() + "\n" + VerifyDurableCaptivityTools()
        + "\n" + VerifyLaborToolAssignmentPendingOutbox();

    public static void RunDoorPolicyOnly()
    {
        string precedence = VerifyDoorAccessPolicy();
        string presets = VerifyDoorAccessPresets();
        var policy = new DoorAccessPolicyState();
        policy.ApplyPreset(DoorAccessPreset.StaffOnly);
        Require(policy.IsGroupAllowed(DoorAccessGroup.Staff) && !policy.IsGroupAllowed(DoorAccessGroup.Customer),
            "Staff-only door admitted customer.");
        policy.ApplyPreset(DoorAccessPreset.CustomerArea);
        Require(policy.IsGroupAllowed(DoorAccessGroup.Customer), "Customer-area door denied customer.");
        Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        File.WriteAllLines("Artifacts/QA/wim-implementation/wim-045-door-policy.txt", new[]
        { "precedence=" + precedence, "presets=" + presets, "staff-customer=PASS", "scope=existing-policy-contracts", "result=PASS" });
    }

    [MenuItem("DungeonStory/Debug/Captivity/Run Captivity And Circus Contracts")]
    public static void RunFromMenu()
    {
        bool success = RunAll(logSuccess: true);
        if (!success)
        {
            Debug.LogError("Captivity and circus contracts failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/Captivity/Run R30 Performer Eligibility Focused")]
    public static void RunR30PerformerEligibilityFocused()
    {
        string result = VerifyR30PerformerEligibility();
        Debug.Log("R30 performer eligibility PASS: " + result);
    }

    public static bool RunAll(bool logSuccess)
    {
        Directory.CreateDirectory("Temp");
        List<string> lines = new List<string> { "case\tresult\tdetails" };
        List<string> errors = new List<string>();

        Run("door_default_and_precedence", VerifyDoorAccessPolicy, lines, errors);
        Run("door_presets", VerifyDoorAccessPresets, lines, errors);
        Run("captive_thresholds_and_clone", VerifyCaptiveThresholds, lines, errors);
        Run("captivity_save_validation", VerifyCaptivitySaveValidation, lines, errors);
        Run("durable_captivity_tools", VerifyDurableCaptivityTools, lines, errors);
        Run(
            "captive_labor_tool_assignment_pending_outbox",
            VerifyLaborToolAssignmentPendingOutbox,
            lines,
            errors);
        Run("interaction_registry_and_materials", VerifyInteractions, lines, errors);
        Run("circus_registry_and_programs", VerifyCircusPrograms, lines, errors);
        Run(
            "r30_circus_performer_eligibility",
            VerifyR30PerformerEligibility,
            lines,
            errors);
        Run("circus_persistent_identity", VerifyCircusPersistentIdentity, lines, errors);
        Run("circus_and_wildlife_save_clone", VerifyCircusSaveModels, lines, errors);
        Run("circus_save_validation", VerifyCircusSaveValidation, lines, errors);
        Run("circus_supply_restore_join", CircusSupplyRestoreJoinFixture.Run, lines, errors);
        Run(
            "circus_performance_supply_exact_owner",
            CircusPerformanceSupplyDebugScenarios.Run,
            lines,
            errors);
        Run(
            "captured_wildlife_feed_pending_outbox",
            CapturedWildlifeFeedOutboxDebugScenarios.Run,
            lines,
            errors);
        Run("captivity_facility_assets", VerifyFacilityAssets, lines, errors);
        Run("constructor_facades", VerifyConstructorFacades, lines, errors);
        Run(
            "captivity_late_publish_failure_preserves_escort_parents",
            VerifyLatePublishFailurePreservesEscortParents,
            lines,
            errors);
        Run(
            "minion_resident_standing",
            MinionResidentStandingDebugScenarios.Run,
            lines,
            errors);

        File.WriteAllLines(ReportPath, lines);
        foreach (string error in errors)
        {
            Debug.LogError(error);
        }

        if (errors.Count == 0 && logSuccess)
        {
            Debug.Log($"Captivity and circus contracts PASS. Report: {ReportPath}");
        }

        return errors.Count == 0;
    }

    private static string VerifyDoorAccessPolicy()
    {
        DoorAccessPolicyState state = new DoorAccessPolicyState();
        Require(state.AllowedGroups == DoorAccessGroup.All, "new door was not open to every group");
        Require(!state.IsRestricted, "new all-access door was marked restricted");

        state.SetGroupAllowed(DoorAccessGroup.Captive, false);
        Require(!state.IsGroupAllowed(DoorAccessGroup.Captive), "captive group remained allowed");
        state.SetIndividualRule("captive:one", DoorAccessIndividualRule.Allow);
        Require(
            state.GetIndividualRule("captive:one") == DoorAccessIndividualRule.Allow,
            "individual allow was not stored");
        state.SetIndividualRule("captive:one", DoorAccessIndividualRule.Deny);
        Require(
            state.GetIndividualRule("captive:one") == DoorAccessIndividualRule.Deny,
            "individual deny did not override allow");

        DoorAccessPolicyState clone = state.Clone();
        Require(clone.AllowedGroups == state.AllowedGroups, "door policy clone lost groups");
        Require(
            clone.GetIndividualRule("captive:one") == DoorAccessIndividualRule.Deny,
            "door policy clone lost individual deny");
        return "all-access default and deny-over-allow precedence preserved";
    }

    private static string VerifyDoorAccessPresets()
    {
        DoorAccessPolicyState state = new DoorAccessPolicyState();
        state.ApplyPreset(DoorAccessPreset.Cell);
        Require(state.IsGroupAllowed(DoorAccessGroup.Owner), "cell denied owner");
        Require(state.IsGroupAllowed(DoorAccessGroup.Staff), "cell denied staff");
        Require(!state.IsGroupAllowed(DoorAccessGroup.Captive), "cell allowed captive");
        Require(!state.IsGroupAllowed(DoorAccessGroup.CaptiveWildlife), "cell allowed captive wildlife");

        state.ApplyPreset(DoorAccessPreset.AnimalPen);
        Require(state.IsGroupAllowed(DoorAccessGroup.CaptiveWildlife), "animal pen denied captive wildlife");
        Require(!state.IsGroupAllowed(DoorAccessGroup.Wildlife), "animal pen allowed ordinary wildlife");
        Require(!state.IsGroupAllowed(DoorAccessGroup.Captive), "animal pen allowed captive");
        return "cell and animal-pen presets separate their contained groups";
    }

    private static string VerifyCaptiveThresholds()
    {
        CaptiveState state = new CaptiveState
        {
            captiveId = "character:captive:test",
            status = CaptivityStatus.Confined,
            compliance = 49f,
            trust = 70f,
            grudge = 30f,
            corruption = 59f
        };

        Require(!state.CanLaborWithBody(100f, bodyAvailable: true),
            "labor unlocked below compliance 50");
        state.compliance = 50f;
        Require(state.CanLaborWithBody(100f, bodyAvailable: true),
            "labor did not unlock at compliance 50");
        Require(!state.CanLaborWithBody(39f, bodyAvailable: true),
            "labor remained available below health 40");
        Require(state.CanLaborWithBody(40f, bodyAvailable: true),
            "labor rejected exact health threshold 40");
        Require(!state.CanLaborWithBody(100f, bodyAvailable: false),
            "labor allowed a body that cannot work");
        Require(state.CanRecruit, "recruitment thresholds rejected a valid captive");
        state.corruption = 60f;
        Require(!state.CanRecruit, "recruitment allowed corruption 60");
        state.corruption = 80f;
        Require(state.CanBecomeMinion, "minion conversion did not unlock at corruption 80");

        CaptiveState clone = state.Clone();
        clone.will = 1f;
        Require(!Mathf.Approximately(state.will, clone.will), "captive clone shared mutable state");
        return "labor, recruitment, and minion thresholds are exact";
    }

    private static string VerifyInteractions()
    {
        ICaptivityInteractionHandler[] handlers =
        {
            new CaptivityPersuasionHandler(),
            new CaptivityIsolationHandler(),
            new CaptivityCoercionHandler(),
            new CaptivityInterrogationHandler(),
            new CaptivityIndoctrinationHandler(),
            new CaptivityBrandingHandler(),
            new CaptivityBloodExtractionHandler(),
            new CaptivityMemoryExtractionHandler(),
            new CaptivityForcedModificationHandler(),
            new CaptivityCorruptionRitualHandler()
        };
        CaptivityInteractionRegistry registry = new CaptivityInteractionRegistry(handlers);
        Require(registry.All.Count == 10, $"expected 10 interactions, got {registry.All.Count}");
        Require(
            handlers.Select(handler => handler.InteractionId).Distinct(StringComparer.Ordinal).Count()
            == handlers.Length,
            "interaction ids were not unique");
        foreach (ICaptivityInteractionHandler handler in handlers)
        {
            Require(handler.RequiredWork > 0f, $"{handler.InteractionId} had no work amount");
            Require(
                handler.MaterialRequirements.Count > 0
                && handler.MaterialRequirements.All(requirement => requirement.Value > 0),
                $"{handler.InteractionId} had no physical material requirement");
            Require(
                registry.TryGet(handler.InteractionId, out ICaptivityInteractionHandler resolved)
                && ReferenceEquals(handler, resolved),
                $"{handler.InteractionId} did not resolve from the registry");
        }

        bool duplicateRejected = false;
        try
        {
            _ = new CaptivityInteractionRegistry(
                new ICaptivityInteractionHandler[]
                {
                    new CaptivityPersuasionHandler(),
                    new CaptivityPersuasionHandler()
                });
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }

        Require(duplicateRejected, "duplicate interaction id was accepted");
        return "10 registered interactions require work and hauled materials";
    }

    private static string VerifyCaptivitySaveValidation()
    {
        CaptivitySaveData payload = new CaptivitySaveData
        {
            captureSequence = 4,
            policySequence = 0,
            policies = new List<CaptivePolicyData>
            {
                new CaptivePolicyData
                {
                    policyId = CaptivityPolicyIds.Standard,
                    displayName = "표준 수용"
                }
            },
            captives = new List<CaptiveState>
            {
                new CaptiveState
                {
                    captiveId = "character:captive:test",
                    displayName = "테스트 포로",
                    speciesTag = "human",
                    status = CaptivityStatus.AwaitingCapture,
                    policyId = CaptivityPolicyIds.Standard
                }
            }
        };
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(payload, validReport);
        Require(
            validReport.Success,
            $"valid captivity payload failed: {string.Join(" | ", validReport.Errors)}");

        CaptivitySaveData duplicate = CloneCaptivityPayload(payload);
        duplicate.captives.Add(duplicate.captives[0].Clone());
        DungeonGameRestoreReport duplicateReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(duplicate, duplicateReport);
        Require(
            !duplicateReport.Success,
            "duplicate captive ID passed strict validation");

        CaptivitySaveData badSequence = CloneCaptivityPayload(payload);
        badSequence.policies.Add(new CaptivePolicyData
        {
            policyId = "captivity:custom:2",
            displayName = "사용자 정책"
        });
        badSequence.policySequence = 1;
        DungeonGameRestoreReport sequenceReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(badSequence, sequenceReport);
        Require(
            !sequenceReport.Success,
            "reused captivity policy sequence passed strict validation");

        CaptivitySaveData escort = CloneCaptivityPayload(payload);
        CaptiveState escortState = escort.captives[0];
        escortState.status = CaptivityStatus.Escorting;
        escortState.reservedCarrierId = "character:staff:carrier";
        escortState.housingBuildingId = "building:test-cell";
        escortState.restraintStackId = "stack:restraint";
        escortState.restraintItemId = CaptivityItemDefinitions.RestraintsItemId;
        escortState.restraintQuantity = 1;
        escortState.restrained = true;
        DungeonGameRestoreReport escortReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(escort, escortReport);
        Require(
            escortReport.Success,
            $"valid escort payload failed: {string.Join(" | ", escortReport.Errors)}");
        CaptiveState normalized = CaptivitySaveValidation
            .CreateRestoredCaptive(escortState);
        Require(
            normalized.status == CaptivityStatus.AwaitingCapture
            && normalized.reservedCarrierId.Length == 0
            && normalized.housingBuildingId.Length == 0
            && normalized.restraintStackId.Length == 0
            && !normalized.restrained,
            "transient escort state was not normalized in the detached candidate");
        return "strict DTO validation rejects duplicates/sequence reuse and safely resets transient escort state";
    }

    private static CaptivitySaveData CloneCaptivityPayload(
        CaptivitySaveData source)
    {
        return new CaptivitySaveData
        {
            version = source.version,
            captureSequence = source.captureSequence,
            policySequence = source.policySequence,
            policies = source.policies.Select(policy => policy.Clone()).ToList(),
            captives = source.captives.Select(captive => captive.Clone()).ToList()
        };
    }

    private static string VerifyDurableCaptivityTools()
    {
        foreach (string itemId in new[]
                 {
                     CaptivityItemDefinitions.PrisonerWorkKitItemId,
                     CaptivityItemDefinitions.ReinforcedRestraintItemId
                 })
        {
            Require(
                DurableToolItemRules.TryGetMaximumDurability(
                    itemId,
                    out float maximum)
                && maximum > 0f,
                $"{itemId} has no durable-tool contract");
            ItemInstanceComponentSaveData component =
                DurableToolItemRules.CreateDurability(itemId, maximum - 7f);
            Require(
                Mathf.Approximately(
                    DurableToolItemRules.ReadCurrentDurability(
                        itemId,
                        new[] { component }),
                    maximum - 7f),
                $"{itemId} durability component did not round-trip");

            ResourceItemDefinitionSO asset = AssetDatabase
                .FindAssets("t:ResourceItemDefinitionSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.ItemId,
                        itemId,
                        StringComparison.Ordinal));
            Require(asset != null, $"{itemId} asset is missing");
            Require(asset.MaxStack == 1, $"{itemId} is not a unique physical item");
        }

        const string captiveId = "character:captive:durable-tool";
        const string instanceId = "item-instance:test-prisoner-work-kit";
        string operationId = CaptivityLaborToolAssignmentIdentity.FormatOperationId(
            captiveId,
            instanceId);
        CaptivitySaveData labor = new CaptivitySaveData
        {
            policies = new List<CaptivePolicyData>
            {
                new CaptivePolicyData
                {
                    policyId = CaptivityPolicyIds.Standard,
                    displayName = "표준 수용"
                }
            },
            captives = new List<CaptiveState>
            {
                new CaptiveState
                {
                    captiveId = captiveId,
                    displayName = "도구 포로",
                    speciesTag = "human",
                    status = CaptivityStatus.Labor,
                    policyId = CaptivityPolicyIds.Standard,
                    housingBuildingId = "building:test-cell",
                    compliance = 60f,
                    laborPermissions = CaptiveLaborPermission.Clean,
                    assignedLaborToolItemId =
                        CaptivityItemDefinitions.PrisonerWorkKitItemId,
                    assignedLaborToolInstanceId =
                        instanceId,
                    assignedLaborToolDurability = 80f,
                    assignedLaborToolMaximumDurability = 100f,
                    laborToolAssignmentOperationId = operationId,
                    laborToolAssignmentCommitId =
                        $"physical-batch-disposition:1:{operationId}:1:1000",
                    laborToolAssignmentSourceStackId =
                        "stack:test-prisoner-work-kit",
                    laborToolAssignmentCompleted = true,
                    nextLaborToolWearAt = 60f
                }
            }
        };
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(labor, validReport);
        Require(
            validReport.Success,
            "valid assigned labor tool failed save validation: "
            + string.Join(" | ", validReport.Errors));

        labor.captives[0].assignedLaborToolDurability = 0f;
        DungeonGameRestoreReport brokenReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(labor, brokenReport);
        Require(
            !brokenReport.Success,
            "labor remained valid with a broken assigned work kit");
        return "captivity tools are unique durable instances and labor save state requires a usable assigned kit";
    }

    private static string VerifyLaborToolAssignmentPendingOutbox()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        var repository = new WorldItemRepository(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        var batch = new PhysicalItemBatchDispositionService(
            repository,
            new PhysicalItemMassQuery(catalog),
            EditorNullItemMarkerPresenter.Instance, clock: new DungeonStory.Foundation.UnityUiClock());
        const string captiveId = "character:captive:outbox";
        const string instanceId = "item-instance:captive-work-kit:outbox";
        ItemInstanceComponentSaveData durability =
            DurableToolItemRules.CreateDurability(
                CaptivityItemDefinitions.PrisonerWorkKitItemId,
                80f);
        string sourceStackId = repository.AddEditorTestStack(
            CaptivityItemDefinitions.PrisonerWorkKitItemId,
            1,
            WorldItemStackState.FacilityBuffer,
            destinationId: "captive-labor-tool:" + captiveId,
            components: new[] { durability },
            itemInstanceId: instanceId);
        string operationId = CaptivityLaborToolAssignmentIdentity.FormatOperationId(
            captiveId,
            instanceId);
        Require(batch.TryCommitPending(
                new[] { new PhysicalItemTransformInput(sourceStackId, 1) },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                CaptivityLaborToolAssignmentOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure),
            "labor-tool fixture could not stage pending transfer: "
                + commitFailure);

        var pending = new CaptiveState
        {
            captiveId = captiveId,
            displayName = "outbox captive",
            speciesTag = "human",
            status = CaptivityStatus.Confined,
            policyId = CaptivityPolicyIds.Standard,
            housingBuildingId = "building:test-cell",
            compliance = 60f,
            pendingLaborPermissions = CaptiveLaborPermission.Clean,
            laborToolDestinationId = "captive-labor-tool:" + captiveId,
            assignedLaborToolItemId =
                CaptivityItemDefinitions.PrisonerWorkKitItemId,
            assignedLaborToolInstanceId = instanceId,
            assignedLaborToolDurability = 80f,
            assignedLaborToolMaximumDurability = 100f,
            laborToolAssignmentOperationId = operationId,
            laborToolAssignmentCommitId = receipt.CommitId,
            laborToolAssignmentSourceStackId = sourceStackId,
            laborToolAssignmentCompleted = false
        };
        CaptivitySaveData pendingSave = CreateLaborToolOutboxSave(pending);
        DungeonGameRestoreReport pendingReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(pendingSave, pendingReport);
        Require(pendingReport.Success,
            "V3 rejected canonical pending labor-tool outbox: "
                + string.Join(" | ", pendingReport.Errors));
        Require(repository.GetEditorTestQuantity(sourceStackId) == 0
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "pending labor-tool transfer did not retain exact custody");

        CaptiveState mismatched = pending.Clone();
        mismatched.laborToolAssignmentCommitId += "1";
        Require(!CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                mismatched,
                batch,
                out _)
            && !mismatched.laborToolAssignmentCompleted
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "mismatched labor-tool receipt mutated domain or physical custody");

        CaptivitySaveData malformedSave = CreateLaborToolOutboxSave(pending);
        malformedSave.captives[0].laborToolAssignmentCommitId += ":tampered";
        DungeonGameRestoreReport malformedReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(malformedSave, malformedReport);
        Require(!malformedReport.Success,
            "V3 accepted malformed labor-tool assignment provenance");

        CaptiveState restored = pending.Clone();
        Require(CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                restored,
                batch,
                out string finalizeFailure),
            "restored labor-tool outbox did not finalize: " + finalizeFailure);
        Require(restored.laborToolAssignmentCompleted
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "labor-tool finalization did not terminalize and acknowledge exactly once");
        restored.laborPermissions = restored.pendingLaborPermissions;
        restored.pendingLaborPermissions = CaptiveLaborPermission.None;
        restored.laborToolDestinationId = string.Empty;
        restored.status = CaptivityStatus.Labor;
        CaptivitySaveData terminalSave = CreateLaborToolOutboxSave(restored);
        DungeonGameRestoreReport terminalReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(terminalSave, terminalReport);
        Require(terminalReport.Success,
            "V3 rejected terminal labor-tool assignment: "
                + string.Join(" | ", terminalReport.Errors));
        Require(CaptivityLaborToolAssignmentOutbox.TryFinalizePending(
                restored,
                batch,
                out string replayFailure)
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "terminal labor-tool replay was not idempotent: " + replayFailure);
        return "Captivity V3 preserves and reconciles exact labor-tool pending Transfer custody";
    }

    private static CaptivitySaveData CreateLaborToolOutboxSave(
        CaptiveState captive) => new CaptivitySaveData
    {
        policies = new List<CaptivePolicyData>
        {
            new CaptivePolicyData
            {
                policyId = CaptivityPolicyIds.Standard,
                displayName = "표준 수용"
            }
        },
        captives = new List<CaptiveState> { captive.Clone() }
    };

    private static string VerifyCircusPrograms()
    {
        ICircusProgramHandler[] handlers =
        {
            new NonlethalActCircusProgram(),
            new DangerousStuntCircusProgram(),
            new CaptiveDuelCircusProgram(),
            new BeastShowCircusProgram(),
            new BeastArenaCircusProgram(),
            new PublicPunishmentCircusProgram(),
            new ExecutionPlayCircusProgram(),
            new PublicCorruptionRitualCircusProgram()
        };
        CircusProgramRegistry registry = new CircusProgramRegistry(handlers);
        IReadOnlyList<CircusProgramModule> programs = registry.Definitions;
        Require(programs.Count == 8, $"expected 8 circus programs, got {programs.Count}");
        Require(
            programs.Select(program => program.programId).Distinct(StringComparer.Ordinal).Count()
            == programs.Count,
            "circus program ids were not unique");
        Require(
            programs.Any(program => program.requiresWildlife && program.usesCombat),
            "wildlife combat program was missing");
        Require(
            programs.Any(program => program.requiresCaptive && !program.publiclyCruel),
            "non-cruel captive performance was missing");
        Require(
            programs.Count(program => program.publiclyCruel) >= 4,
            "cruel performance range was incomplete");
        return "8 programs cover nonlethal, dangerous, combat, wildlife, and cruel shows";
    }

    private static string VerifyR30PerformerEligibility()
    {
        using R30PerformerEligibilityFixture fixture = new();
        const string captiveProgram = "circus:nonlethal-act";
        const string noCaptiveProgram = "circus:trophy-display";

        Require(!fixture.Captivity.CanAssignPerformer(
                fixture.DisallowedId,
                out string policyFailure)
                && !string.IsNullOrWhiteSpace(policyFailure),
            "Captivity assignment query allowed the policy-disallowed captive.");
        Require(fixture.Captivity.CanAssignPerformer(
                fixture.EligibleId,
                out string eligibleFailure),
            "Captivity assignment query rejected the eligible captive: "
            + eligibleFailure);
        IReadOnlyList<string> selected = fixture.Circus
            .GetAutoSelectedPerformerIds(fixture.Stage, captiveProgram);
        Require(selected.SequenceEqual(new[] { fixture.EligibleId }),
            "Auto selection did not exclude the disallowed high-skill captive and injured captive.");
        Require(fixture.Circus
                .GetAutoSelectedPerformerIds(fixture.Stage, noCaptiveProgram)
                .Count == 0,
            "A program that does not need captives auto-selected a performer.");

        VerifyForecastAndScheduleRejectTogether(
            fixture,
            captiveProgram,
            fixture.DisallowedId,
            "policy-disallowed performer");
        VerifyForecastAndScheduleRejectTogether(
            fixture,
            captiveProgram,
            fixture.InjuredId,
            "injured performer");

        CircusProgramForecast forecast = fixture.Circus.GetForecast(
            fixture.Stage,
            captiveProgram,
            CircusLethalityPolicy.StopWhenDowned,
            selected,
            Array.Empty<string>());
        Require(forecast.CanSchedule,
            "Eligible low-skill performer was rejected by forecast: "
            + forecast.FailureReason);
        Require(fixture.Circus.TrySchedule(
                fixture.Stage,
                captiveProgram,
                CircusLethalityPolicy.StopWhenDowned,
                new[] { fixture.EligibleId, fixture.EligibleId },
                Array.Empty<string>(),
                out CircusShowOrder scheduled,
                out string scheduleFailure),
            "Eligible performer was rejected by schedule: " + scheduleFailure);
        Require(scheduled.performerIds.SequenceEqual(
                new[] { fixture.EligibleId }),
            "Schedule did not remove duplicate performer input IDs.");
        Require(!fixture.Captivity.TryAssignPerformer(
                fixture.EligibleId,
                true,
                out string duplicateAssignmentFailure)
                && !string.IsNullOrWhiteSpace(duplicateAssignmentFailure),
            "Captivity owner accepted a duplicate performer assignment.");

        VerifyForecastAndScheduleRejectTogether(
            fixture,
            captiveProgram,
            fixture.EligibleId,
            "already-assigned performer");
        Require(fixture.Circus.GetAutoSelectedPerformerIds(
                fixture.Stage,
                captiveProgram).Count == 0,
            "An active-order performer remained auto-selectable.");
        Require(fixture.Circus.Cancel(scheduled.orderId, "r30-fixture-cancel"),
            "Normal circus cancellation failed.");
        Require(fixture.Circus.GetAutoSelectedPerformerIds(
                fixture.Stage,
                captiveProgram).SequenceEqual(new[] { fixture.EligibleId }),
            "Cancelled performer did not become selectable again.");
        Require(fixture.Circus.TrySchedule(
                fixture.Stage,
                captiveProgram,
                CircusLethalityPolicy.StopWhenDowned,
                new[] { fixture.EligibleId },
                Array.Empty<string>(),
                out CircusShowOrder retried,
                out string retryFailure),
            "Cancelled performer could not be scheduled again: " + retryFailure);
        Require(retried.orderId != scheduled.orderId
                && fixture.Circus.Orders.Count(order => !order.IsTerminal) == 1,
            "Retry created duplicate active orders or reused the prior order ID.");

        return "policy, injury, assignment, empty selection, forecast parity, cancel retry PASS";
    }

    private static void VerifyForecastAndScheduleRejectTogether(
        R30PerformerEligibilityFixture fixture,
        string programId,
        string performerId,
        string caseName)
    {
        CircusProgramForecast forecast = fixture.Circus.GetForecast(
            fixture.Stage,
            programId,
            CircusLethalityPolicy.StopWhenDowned,
            new[] { performerId },
            Array.Empty<string>());
        bool scheduled = fixture.Circus.TrySchedule(
            fixture.Stage,
            programId,
            CircusLethalityPolicy.StopWhenDowned,
            new[] { performerId },
            Array.Empty<string>(),
            out _,
            out string failureReason);
        Require(!forecast.CanSchedule && !scheduled,
            caseName + " was not rejected by both forecast and schedule.");
        Require(!string.IsNullOrWhiteSpace(forecast.FailureReason)
                && string.Equals(
                    forecast.FailureReason,
                    failureReason,
                    StringComparison.Ordinal),
            caseName + " produced different forecast and schedule reasons.");
    }

    private static string VerifyCircusSaveModels()
    {
        CircusShowOrder order = new CircusShowOrder
        {
            orderId = "show:test",
            performerIds = new List<string> { "character:captive:one" },
            wildlifeIds = new List<string> { "wildlife:one" },
            audienceIds = new List<string> { "customer:one" },
            performerPositions = new List<Vector2Int> { new Vector2Int(1, 2) }
        };
        CircusShowOrder clone = order.Clone();
        clone.performerIds.Add("character:captive:two");
        Require(order.performerIds.Count == 1, "circus order clone shared performer list");

        CapturedWildlifeState wildlife = new CapturedWildlifeState
        {
            wildlifeId = "wildlife:one",
            transportState = CapturedWildlifeTransportState.Penned,
            escapeRisk = 42f,
            foodDeliveryPending = true,
            lastCareStatus = "먹이 대기"
        };
        CapturedWildlifeState wildlifeClone = wildlife.Clone();
        wildlifeClone.escapeRisk = 1f;
        Require(
            Mathf.Approximately(wildlife.escapeRisk, 42f),
            "captured wildlife clone shared state");
        return "show collections and wildlife care state clone independently";
    }

    private static string VerifyCircusPersistentIdentity()
    {
        GameObject firstRuntime = new("circus-identity-first");
        GameObject secondRuntime = new("circus-identity-second");
        try
        {
            Require(
                firstRuntime.GetInstanceID() != secondRuntime.GetInstanceID(),
                "circus identity fixture did not create distinct Unity objects");

            CircusCombatant first = new(
                new CircusCombatantIdentity(
                    CircusCombatantKind.Character,
                    "character:persistent:one"),
                firstRuntime,
                () => true);
            CircusCombatant reconstructed = new(
                new CircusCombatantIdentity(
                    CircusCombatantKind.Character,
                    "character:persistent:one"),
                secondRuntime,
                () => true);
            CircusCombatant differentId = new(
                new CircusCombatantIdentity(
                    CircusCombatantKind.Character,
                    "character:persistent:two"),
                firstRuntime,
                () => true);
            CircusCombatant differentKind = new(
                new CircusCombatantIdentity(
                    CircusCombatantKind.Wildlife,
                    "character:persistent:one"),
                firstRuntime,
                () => true);

            Require(
                first.Equals(reconstructed),
                "same persistent circus identity changed across Unity objects");
            Require(
                first.GetHashCode() == reconstructed.GetHashCode(),
                "same persistent circus identity produced different hashes");
            Require(
                !first.Equals(differentId),
                "different circus persistent IDs compared equal");
            Require(
                !first.Equals(differentKind),
                "different circus combatant kinds compared equal");
            Require(
                new HashSet<CircusCombatant>
                {
                    first,
                    reconstructed,
                    differentId,
                    differentKind
                }.Count == 3,
                "circus identity set did not collapse only the reconstructed combatant");

            return "stable kind/id identity survives Unity-object reconstruction";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstRuntime);
            UnityEngine.Object.DestroyImmediate(secondRuntime);
        }
    }

    private static string VerifyCircusSaveValidation()
    {
        CircusProgramRegistry programs = new CircusProgramRegistry(
            new ICircusProgramHandler[] { new TrophyDisplayCircusProgram() });
        CircusShowOrder order = new CircusShowOrder
        {
            orderId = "circus:1",
            stageId = "building:stage",
            stagePosition = new Vector2Int(4, 5),
            roomId = 3,
            programId = "circus:trophy-display",
            state = CircusShowState.Performing,
            wildlifeIds = new List<string> { "wildlife:one" },
            wildlifePositions = new List<Vector2Int> { new Vector2Int(4, 6) },
            preparationWorkRequired = 10f,
            preparationWorkCompleted = 8f,
            elapsedShowSeconds = 4f,
            showDurationSeconds = 20f,
            nextCombatExchangeAt = 0.5f,
            phaseElapsedSeconds = 2f,
            ticketPrice = 1,
            venueAccidentDamageMultiplier = 1f,
            venueFilthMultiplier = 1f,
            statusMessage = "running"
        };
        CapturedWildlifeState wildlife = new CapturedWildlifeState
        {
            wildlifeId = "wildlife:one",
            speciesId = "wildlife:test",
            penId = "building:pen",
            penPosition = new Vector2Int(1, 1),
            capturePosition = new Vector2Int(2, 2),
            assignedShowOrderId = order.orderId,
            transportState = CapturedWildlifeTransportState.MovingToShow,
            remainingCareSeconds = 1f,
            lastCareStatus = "moving"
        };
        CircusSaveData payload = new CircusSaveData
        {
            nextOrderSequence = 1,
            orders = new List<CircusShowOrder> { order },
            capturedWildlife = new List<CapturedWildlifeState> { wildlife }
        };

        DungeonGameRestoreReport valid = new DungeonGameRestoreReport();
        CircusSaveValidation.Validate(payload, programs, valid);
        Require(valid.Success,
            "valid current circus payload failed: " + string.Join(" | ", valid.Errors));

        CircusSaveData duplicate = CloneCircusPayload(payload);
        duplicate.orders.Add(duplicate.orders[0].Clone());
        DungeonGameRestoreReport duplicateReport = new DungeonGameRestoreReport();
        CircusSaveValidation.Validate(duplicate, programs, duplicateReport);
        Require(!duplicateReport.Success, "duplicate circus order was accepted");

        CircusSaveData sequenceReuse = CloneCircusPayload(payload);
        sequenceReuse.nextOrderSequence = 0;
        DungeonGameRestoreReport sequenceReport = new DungeonGameRestoreReport();
        CircusSaveValidation.Validate(sequenceReuse, programs, sequenceReport);
        Require(!sequenceReport.Success, "circus order sequence reuse was accepted");

        CircusSaveData incoherent = CloneCircusPayload(payload);
        incoherent.capturedWildlife[0].transportState =
            CapturedWildlifeTransportState.Penned;
        DungeonGameRestoreReport incoherentReport = new DungeonGameRestoreReport();
        CircusSaveValidation.Validate(incoherent, programs, incoherentReport);
        Require(!incoherentReport.Success,
            "penned wildlife with an active show assignment was accepted");

        CircusShowOrder restoredOrder =
            CircusSaveValidation.CreateRestoredOrder(order);
        Require(
            restoredOrder.state == CircusShowState.Composition
            && restoredOrder.statusMessage == "circus.preparation-resumed",
            "active circus order was not normalized to composition");
        CapturedWildlifeState normalized =
            CircusSaveValidation.CreateRestoredCapturedWildlife(wildlife);
        Require(
            normalized.transportState == CapturedWildlifeTransportState.Penned
            && normalized.assignedShowOrderId.Length == 0
            && normalized.reservedCarrierId.Length == 0,
            "in-flight wildlife was not normalized to a safe pen state");
        return "strict V2 validation rejects duplicates/sequence reuse/coherence errors and normalizes transient work";
    }

    private static CircusSaveData CloneCircusPayload(CircusSaveData source)
    {
        return new CircusSaveData
        {
            version = source.version,
            nextOrderSequence = source.nextOrderSequence,
            nextWildlifeGameplayOutcomeSequence =
                source.nextWildlifeGameplayOutcomeSequence,
            orders = source.orders.Select(order => order.Clone()).ToList(),
            capturedWildlife = source.capturedWildlife
                .Select(state => state.Clone())
                .ToList(),
            wildlifeGameplayOutcomes = source.wildlifeGameplayOutcomes
                .Select(value => value?.Clone())
                .ToList()
        };
    }

    private static string VerifyFacilityAssets()
    {
        VerifyFacility<BuildingCaptiveHousingAbility>(
            "Assets/Resources/SO/Building/Captivity/CP01_감방구속대.asset",
            1200,
            FacilityRole.None);
        VerifyFacility<BuildingCircusStageAbility>(
            "Assets/Resources/SO/Building/Captivity/CS01_중앙무대.asset",
            1201,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingAudienceSeatingAbility>(
            "Assets/Resources/SO/Building/Captivity/CS02_관람석.asset",
            1202,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingBeastPenAbility>(
            "Assets/Resources/SO/Building/Captivity/CB01_야수우리.asset",
            1203,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusTicketBoothAbility>(
            "Assets/Resources/SO/Building/Captivity/CT01_매표소.asset",
            1204,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusGamblingAbility>(
            "Assets/Resources/SO/Building/Captivity/CG01_도박창구.asset",
            1205,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusAnnouncerAbility>(
            "Assets/Resources/SO/Building/Captivity/CA01_진행자단상.asset",
            1206,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusHazardAbility>(
            "Assets/Resources/SO/Building/Captivity/CH01_위험장치.asset",
            1207,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingCircusTreatmentZoneAbility>(
            "Assets/Resources/SO/Building/Captivity/CM01_치료구역.asset",
            1208,
            FacilityRole.Entertainment);
        VerifyFacility<BuildingPublicPunishmentAbility>(
            "Assets/Resources/SO/Building/Captivity/CP02_공개형벌장치.asset",
            1209,
            FacilityRole.Entertainment);
        return "captivity and circus facilities retain ids, roles, and abilities";
    }

    private static string VerifyConstructorFacades()
    {
        Type runtimeAssemblyType = typeof(CaptivityRuntime);
        Type restoreCoordinator = runtimeAssemblyType.Assembly.GetType(
            "CircusRestoreCoordinator",
            throwOnError: true);
        Type restoreStateContext = runtimeAssemblyType.Assembly.GetType(
            "CircusRestoreStateContext",
            throwOnError: true);
        Type[] ownedTypes =
        {
            typeof(CaptivityRuntime),
            typeof(CaptivityCharacterContext),
            typeof(CaptivityWorldContext),
            typeof(CaptivitySessionContext),
            typeof(WildlifeCaptureRuntime),
            typeof(WildlifeCaptureWorldContext),
            typeof(WildlifeCaptureCareContext),
            typeof(WildlifeCaptureSessionContext),
            typeof(CircusRuntime),
            typeof(CircusProgramContext),
            typeof(CircusWorldContext),
            typeof(CircusCombatContext),
            typeof(CircusSessionContext),
            restoreCoordinator,
            restoreStateContext
        };
        string[] violations = ownedTypes
            .SelectMany(type => type.GetConstructors(
                    BindingFlags.Instance
                    | BindingFlags.Public
                    | BindingFlags.NonPublic)
                .Select(constructor => new
                {
                    Type = type,
                    Count = constructor.GetParameters().Length
                }))
            .Where(entry => entry.Count > 8)
            .Select(entry => $"{entry.Type.Name}:{entry.Count}")
            .ToArray();
        Require(
            violations.Length == 0,
            "constructor dependency limit exceeded: "
            + string.Join(", ", violations));

        RequireNullGuard(
            () => new CaptivityRuntime(null, null, null, null, null, null),
            nameof(CaptivityRuntime));
        RequireNullGuard(
            () => new WildlifeCaptureRuntime(null, null, null),
            nameof(WildlifeCaptureRuntime));
        RequireNullGuard(
            () => new CircusRuntime(null, null, null, null),
            nameof(CircusRuntime));
        RequireReflectedNullGuard(restoreCoordinator);
        return "runtime constructors 3/3/4 and restore coordinator 3; all contexts <= 6";
    }

    private static string VerifyLatePublishFailurePreservesEscortParents()
    {
        DungeonRuntimeAggregateRootStore aggregateRootStore = new();
        TrackingEscortRestoreLifecycle escort = new();
        escort.SetParent("character:captive:one", "housing:old");
        escort.SetParent("character:captive:two", "carrier:old");
        string originalParents = escort.CaptureParents();
        EscortProjectionRestoreParticipant escortParticipant = new(escort);
        LateFailingRestoreParticipant lateParticipant = new();
        DungeonSaveSectionRegistry registry = new(
            Array.Empty<IDungeonSaveSection>(),
            aggregateRootStore,
            new IDungeonRestoreTransactionParticipant[]
            {
                escortParticipant,
                lateParticipant
            });

        DungeonGameRestoreReport failureReport = new();
        bool restored = registry.RestoreAll(
            Array.Empty<DungeonSaveSectionEnvelope>(),
            failureReport);
        Require(
            escort.CaptureParents() == originalParents,
            "captivity publish discarded live escort parent state");
        Require(!restored, "late participant did not fail publication");
        Require(
            failureReport.Errors.Any(error => error.Contains(
                lateParticipant.ParticipantId,
                StringComparison.Ordinal)),
            "registry did not report the late participant publication failure");
        Require(
            escort.CaptureParents() == originalParents,
            "captivity rollback did not preserve the exact escort parent map");
        Require(
            escort.ClearCount == 0,
            "captivity rollback cleared escort transient state");

        lateParticipant.ThrowOnPublish = false;
        DungeonGameRestoreReport successReport = new();
        restored = registry.RestoreAll(
            Array.Empty<DungeonSaveSectionEnvelope>(),
            successReport);
        Require(restored && successReport.Success,
            "successful registry publication did not complete");
        Require(
            escort.ClearCount == 1 && escort.CaptureParents().Length == 0,
            "successful completion did not finalize escort transient state");

        return "late failure retains exact parent map; completion clears it once";
    }

    private static T Uninitialized<T>()
        where T : class
    {
        return (T)FormatterServices.GetUninitializedObject(typeof(T));
    }

    private static void RequireNullGuard(Action create, string typeName)
    {
        try
        {
            create();
        }
        catch (ArgumentNullException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{typeName} accepted missing required dependencies.");
    }

    private static void RequireReflectedNullGuard(Type type)
    {
        ConstructorInfo constructor = type.GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic)
            .Single();
        try
        {
            constructor.Invoke(new object[] { null, null, null });
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is ArgumentNullException)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{type.Name} accepted missing required dependencies.");
    }

    private static void VerifyFacility<TAbility>(
        string path,
        int expectedId,
        FacilityRole expectedRole)
        where TAbility : BuildingAbility
    {
        BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        Require(asset != null, $"facility asset missing: {path}");
        Require(asset.id == expectedId, $"{path} id was {asset.id}");
        Require(asset.GetAbility<TAbility>() != null, $"{path} missing {typeof(TAbility).Name}");
        FacilityData facility = asset.Facility;
        Require(facility != null, $"{path} missing facility settings");
        Require(
            expectedRole == FacilityRole.None || (facility.roles & expectedRole) != 0,
            $"{path} missing role {expectedRole}");
    }

    private sealed class R30PerformerEligibilityFixture : IDisposable
    {
        private const string StageAssetPath =
            "Assets/Resources/SO/Building/Captivity/CS01_중앙무대.asset";
        private const string SeatingAssetPath =
            "Assets/Resources/SO/Building/Captivity/CS02_관람석.asset";
        private readonly List<UnityEngine.Object> cleanup = new();
        private readonly List<CharacterActor> actors = new();
        private readonly ICharacterAiWorldRegistry world =
            CharacterAiEditorTestDependencies.WorldRegistry;
        private readonly CharacterBodyHealthRuntime bodyHealth =
            CharacterAiEditorTestDependencies.BodyHealthRuntime;

        internal R30PerformerEligibilityFixture()
        {
            Stage = CreateBuilding(
                "R30 Circus Stage",
                StageAssetPath,
                new BuildingInstanceId("building:r30-circus-stage"),
                new Vector2Int(8, 8));
            BuildableObject seating = CreateBuilding(
                "R30 Circus Seating",
                SeatingAssetPath,
                new BuildingInstanceId("building:r30-circus-seating"),
                new Vector2Int(8, 9));
            GameObject doorObject = new("R30 Circus Door");
            cleanup.Add(doorObject);
            Door door = doorObject.AddComponent<Door>();
            RoomInstance room = new(
                303,
                new[]
                {
                    new Vector2Int(8, 8),
                    new Vector2Int(8, 9),
                    new Vector2Int(9, 8),
                    new Vector2Int(9, 9)
                },
                new[] { Stage, seating },
                new BuildableObject[] { door },
                Array.Empty<BuildableObject>(),
                solidBoundaryCount: 4,
                openBoundaryCount: 0);
            FixedRoomCache rooms = new(room);

            CharacterActor disallowed = CreateActor(
                DisallowedId,
                "Policy Blocked Performer");
            CharacterActor injured = CreateActor(
                InjuredId,
                "Injured Performer");
            CharacterActor eligible = CreateActor(
                EligibleId,
                "Eligible Performer");

            DungeonRuntimeAggregateRootStore captivityStore = new();
            CaptivityActorAccess actorAccess = new(
                captivityStore,
                _ => { });
            R30PerformerPort performerPort = new(actors);
            CaptivityPolicyRuntime policies = new(
                actorAccess,
                performerPort);
            actorAccess.AddState(CreateState(
                DisallowedId,
                disallowed.Identity.DisplayName,
                CaptivityPolicyIds.ForcedLabor,
                99f));
            actorAccess.AddState(CreateState(
                InjuredId,
                injured.Identity.DisplayName,
                CaptivityPolicyIds.Standard,
                80f));
            actorAccess.AddState(CreateState(
                EligibleId,
                eligible.Identity.DisplayName,
                CaptivityPolicyIds.Standard,
                10f));

            bool RejectTransition(string _, out string failureReason)
            {
                failureReason = "R30 performer fixture does not exercise release or recruit.";
                return false;
            }
            CaptivityPerformerRuntime performer = new(
                actorAccess.FindState,
                policies.Find,
                RejectTransition,
                RejectTransition,
                performerPort);
#pragma warning disable SYSLIB0050
            Captivity = (CaptivityRuntime)
                FormatterServices.GetUninitializedObject(
                    typeof(CaptivityRuntime));
#pragma warning restore SYSLIB0050
            SetField(Captivity, "actorAccess", actorAccess);
            SetField(Captivity, "policyRuntime", policies);
            SetField(Captivity, "performerRuntime", performer);
            SetField(Captivity, "queryView", new CaptivityQueryView(
                actorAccess,
                policies));
            SetField(Captivity, "worldRegistry", world);

            bodyHealth.ConfigureVitals(disallowed, 100f, true);
            bodyHealth.ConfigureVitals(injured, 100f, true);
            bodyHealth.ConfigureVitals(eligible, 100f, true);
            bodyHealth.ApplyLocalizedDamage(
                injured,
                CombatBodyPart.LeftArm,
                5f,
                "r30-circus-fixture-injury",
                allowDeath: false);

            IWildlifeCaptureRuntime wildlife =
                NullPort<IWildlifeCaptureRuntime>();
            CircusPerformanceSupplyRuntime supplies = new(
                NullPort<IDurableFacilityEquipmentPolicyQuery>(),
                NullPort<IDurableFacilityEquipmentSlotCommand>(),
                NullPort<IDurableFacilityEquipmentSlotQuery>(),
                NullPort<IDurableFacilityEquipmentUseCommand>(),
                NullPort<IWorldItemStackRuntime>(),
                NullPort<IPhysicalItemBatchDispositionService>());
            CircusProgramContext program = new(
                new CircusProgramRegistry(new ICircusProgramHandler[]
                {
                    new NonlethalActCircusProgram(),
                    new TrophyDisplayCircusProgram()
                }),
                Captivity,
                Captivity,
                wildlife,
                NullPort<IExternalInfluenceRuntime>(),
                supplies);
            CircusWorldContext worldContext = new(
                world,
                NullPort<IGridSystemProvider>(),
                rooms,
                NullPort<IDoorAccessCommandService>(),
                NullPort<IWorldFilthQuery>());
#pragma warning disable SYSLIB0050
            CharacterCombatTransactionFacade combatTransactions =
                (CharacterCombatTransactionFacade)
                FormatterServices.GetUninitializedObject(
                    typeof(CharacterCombatTransactionFacade));
#pragma warning restore SYSLIB0050
            CircusCombatContext combat = new(
                bodyHealth,
                bodyHealth,
                NullPort<ICombatResolutionService>(),
                NullPort<ICombatEquipmentRuntime>(),
                NullPort<ICharacterMedicalQuery>(),
                NullPort<ICharacterMedicalCommand>(),
                NullPort<ICharacterPerformanceQuery>(),
                combatTransactions);
            CircusSessionContext session = new(
                NullPort<IGameMoneyAccount>(),
                CharacterAiEditorTestDependencies.GameClock,
                new RandomStreamProvider(303),
                new GameEventBus(),
                new DungeonRuntimeAggregateRootStore());
            Circus = new CircusRuntime(
                program,
                worldContext,
                combat,
                session);
        }

        internal string DisallowedId => "character:r30-circus-disallowed";
        internal string InjuredId => "character:r30-circus-injured";
        internal string EligibleId => "character:r30-circus-eligible";
        internal BuildableObject Stage { get; }
        internal CaptivityRuntime Captivity { get; }
        internal CircusRuntime Circus { get; }

        public void Dispose()
        {
            Circus.Dispose();
            foreach (CharacterActor actor in actors)
            {
                world.UnregisterCharacterLifetime(actor);
            }
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
                }
            }
        }

        private CharacterActor CreateActor(string id, string displayName)
        {
            CharacterSO data = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    displayName,
                    "human",
                    CharacterRole.Regular);
            cleanup.Add(data);
            GameObject actorObject = new(displayName);
            cleanup.Add(actorObject);
            actorObject.AddComponent<SpriteRenderer>();
            CharacterActor actor = actorObject.AddComponent<CharacterActor>();
            actorObject.AddComponent<AbilityMove>();
            CharacterAiEditorTestDependencies.Inject(actorObject);
            actor.RefreshAbilityCache();
            actor.Initialization(data);
            actor.EnsureRuntimeState();
            actor.Identity.SetPersistentId(id);
            actor.Identity.SetCharacterType(CharacterType.NPC);
            world.RegisterCharacterLifetime(actor);
            actors.Add(actor);
            return actor;
        }

        private BuildableObject CreateBuilding(
            string name,
            string path,
            BuildingInstanceId id,
            Vector2Int position)
        {
            BuildingSO data = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            Require(data != null, "R30 circus fixture asset is missing: " + path);
            GameObject buildingObject = new(name);
            cleanup.Add(buildingObject);
            BuildableObject building =
                buildingObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(building);
            building.RestorePersistentIdentity(id);
            building.Initialization(data, position);
            return building;
        }

        private static CaptiveState CreateState(
            string captiveId,
            string displayName,
            string policyId,
            float skill) => new()
        {
            captiveId = captiveId,
            displayName = displayName,
            status = CaptivityStatus.Confined,
            policyId = policyId,
            restrained = true,
            performerSkill = skill
        };

        private static TPort NullPort<TPort>()
            where TPort : class =>
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<TPort>();

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            field.SetValue(target, value);
        }
    }

    private sealed class R30PerformerPort :
        ICaptivityPerformerPort,
        ICaptivityPolicyActorPort
    {
        private readonly IReadOnlyList<CharacterActor> actors;

        internal R30PerformerPort(IReadOnlyList<CharacterActor> actors)
        {
            this.actors = actors;
        }

        public bool IsActorAvailable(string captiveId) => actors.Any(actor =>
            string.Equals(
                actor?.Identity?.PersistentId,
                captiveId,
                StringComparison.Ordinal));

        public void ApplyAssignmentState(string captiveId, bool assigned)
        {
        }

        public void Publish(CaptivePerformerMilestoneEvent gameEvent)
        {
        }

        public void RaiseAlert(
            string title,
            string message,
            CaptivityMilestoneImportance importance,
            string category)
        {
        }

        public void ConfineLaborer(string captiveId)
        {
        }
    }

    private sealed class FixedRoomCache : IRoomLayoutCache
    {
        private readonly RoomInstance room;

        internal FixedRoomCache(RoomInstance room)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));
        }

        public RoomLayout GetLayout(Grid grid) =>
            new(new[] { room });

        public bool TryGetRoom(
            Grid grid,
            Vector2Int cell,
            out RoomInstance value)
        {
            value = room.ContainsCell(cell) ? room : null;
            return value != null;
        }

        public bool TryGetRoom(
            BuildableObject part,
            out RoomInstance value)
        {
            value = room.ContainsPart(part) ? room : null;
            return value != null;
        }

        public void Clear()
        {
        }
    }

    private sealed class TrackingEscortRestoreLifecycle :
        ICaptivityEscortRestoreLifecycle
    {
        private readonly SortedDictionary<string, string> parents =
            new(StringComparer.Ordinal);

        public int ClearCount { get; private set; }

        public void SetParent(string captiveId, string parentId)
        {
            parents[captiveId] = parentId;
        }

        public string CaptureParents() => string.Join(
            "|",
            parents.Select(entry => $"{entry.Key}={entry.Value}"));

        public void ClearTransientState()
        {
            ClearCount++;
            parents.Clear();
        }

        public void RestoreCaptiveParent(string captiveId)
        {
        }
    }

    private sealed class EscortProjectionRestoreParticipant :
        IDungeonRestoreTransactionParticipant
    {
        private readonly TrackingEscortRestoreLifecycle escort;
        private bool publicationPending;

        public EscortProjectionRestoreParticipant(
            TrackingEscortRestoreLifecycle escort)
        {
            this.escort = escort
                ?? throw new ArgumentNullException(nameof(escort));
        }

        public string ParticipantId => "450.test.captivity-projection";

        public void BeginRestoreCandidate()
        {
            publicationPending = false;
        }

        public void PublishRestoreCandidate()
        {
            publicationPending = true;
        }

        public void RollbackPublishedRestoreCandidate()
        {
            publicationPending = false;
        }

        public void CompleteRestoreCandidate()
        {
            if (!publicationPending)
            {
                throw new InvalidOperationException(
                    "Escort projection completion had no publication.");
            }
            escort.ClearTransientState();
            publicationPending = false;
        }

        public void DiscardRestoreCandidate()
        {
            publicationPending = false;
        }
    }

    private sealed class LateFailingRestoreParticipant :
        IDungeonRestoreTransactionParticipant
    {
        public bool ThrowOnPublish { get; set; } = true;

        public string ParticipantId => "999.test.late-failure";

        public void BeginRestoreCandidate()
        {
        }

        public void PublishRestoreCandidate()
        {
            if (ThrowOnPublish)
            {
                throw new InvalidOperationException(
                    "Intentional late participant publication failure.");
            }
        }

        public void RollbackPublishedRestoreCandidate()
        {
        }

        public void DiscardRestoreCandidate()
        {
        }
    }

    private static void Run(
        string name,
        Func<string> scenario,
        ICollection<string> lines,
        ICollection<string> errors)
    {
        try
        {
            string details = scenario();
            lines.Add($"{name}\tPASS\t{details}");
        }
        catch (Exception exception)
        {
            string message = $"{name}: {exception.Message}";
            lines.Add($"{name}\tFAIL\t{exception.Message}");
            errors.Add(message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
