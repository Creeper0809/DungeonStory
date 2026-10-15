using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Joins service sessions to executable owners in the same detached envelope.
/// Later survival sections must never be queried through their old live roots.
/// Per-section validators still own phase, physical receipt and actor validation.
/// </summary>
public sealed class ServiceExecutionOwnerSaveValidation :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        if (save == null) throw new ArgumentNullException(nameof(save));
        Validate((save.sections ?? throw new InvalidOperationException("Service owner sections missing."))
            .ToDictionary(section => section.sectionId, StringComparer.Ordinal), report);
    }

    public void Validate(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections,
        DungeonGameRestoreReport report)
    {
        if (sections == null) throw new ArgumentNullException(nameof(sections));
        if (report == null) throw new ArgumentNullException(nameof(report));
        try
        {
            var facilities = Read<ModularFacilityWorldSaveData>(sections, ModularFacilityWorldSaveSection.Id,
                ModularFacilityWorldSaveSection.CurrentSectionVersion);
            var services = Read<ServiceRoomsSaveData>(sections, ServiceRoomsSaveSection.Id, ServiceRoomsSaveData.CurrentVersion);
            ValidateCore(
                services,
                facilities,
                Read<DungeonSurvivalSaveData>(sections, SurvivalResourcesSaveSection.Id, DungeonSurvivalSaveData.CurrentVersion),
                Read<DungeonCharacterConsumablesSaveData>(sections, CharacterConsumablesSaveSection.Id,
                    DungeonCharacterConsumablesSaveData.CurrentVersion), report);
            ServiceSessionSaveData[] retained = services.sessions.Where(value =>
                value != null && value.terminalPhase == ServiceSessionTerminalPhase.Acknowledged).ToArray();
            GameplayOutcomeLedgerSaveData ledger = retained.Length == 0 ? null
                : Read<GameplayOutcomeLedgerSaveData>(sections, GameplayOutcomeLedgerSaveSection.Id,
                    GameplayOutcomeLedgerSaveData.CurrentVersion);
            foreach (ServiceSessionSaveData session in retained)
            {
                GameplayOutcomeSnapshot[] receipts = ledger.exactOutcomes.Where(value => value != null
                    && value.producerId == ServiceSessionOutcomeKey.ProducerId
                    && value.operationId == session.sessionId).ToArray();
                if (receipts.Length != 1 || !ServiceSessionOutcomeReceiptFactory.MatchesRetainedCancellation(
                    session.ToSnapshot(), receipts[0]))
                    report.AddError("Shop cleanup terminal outcome mismatch: " + session.sessionId);
            }
            // Small medical-only envelopes have no character section or shops.
            // Any shop owner requires its character section; full captures also
            // check orphan receipts even when there are no shop modules at all.
            if (sections.ContainsKey(CharacterWorldSaveSection.Id)
                || facilities.buildings.Any(building => building.stateModules.Any(module =>
                    module != null && module.moduleId == BuildingStateModuleIds.ShopInteraction)))
                ValidateShopPurchases(facilities, Read<DungeonCharacterWorldSaveData>(sections,
                    CharacterWorldSaveSection.Id, CharacterWorldSaveSection.CurrentVersion));
        }
        catch (Exception exception)
        {
            report.AddError("Service execution owner preflight failed: " + exception.Message);
        }
    }

    internal static void ValidateShopPurchases(ModularFacilityWorldSaveData facilities,
        DungeonCharacterWorldSaveData characters)
    {
        if (facilities?.buildings == null || characters?.actors == null)
            throw new InvalidOperationException("Shop purchase owner collections missing.");
        var actors = characters.actors.ToDictionary(actor => actor.persistentId, StringComparer.Ordinal);
        var uses = new Dictionary<string, (string facilityId, ShopUseExecutionState use)>(StringComparer.Ordinal);
        foreach (ModularFacilityBuildingSaveData building in facilities.buildings)
        {
            var modules = building.stateModules.Where(module => module != null
                && module.moduleId == BuildingStateModuleIds.ShopInteraction).ToArray();
            if (modules.Length == 0) continue;
            if (modules.Length != 1 || modules[0].version != ShopInteractionState.CurrentVersion)
                throw new InvalidOperationException("Shop purchase owner module cardinality/version mismatch.");
            var state = DungeonSaveJson.Deserialize<ShopInteractionState>(modules[0].payload);
            if (state == null || !state.IsValid(building.persistentInstanceId, out _))
                throw new InvalidOperationException("Invalid shop purchase execution owner.");
            foreach (ShopUseExecutionState use in state.activeUses)
            {
                if (!actors.ContainsKey(use.actorId) || !uses.TryAdd(use.operationId, (building.persistentInstanceId, use)))
                    throw new InvalidOperationException("Shop purchase actor/operation owner mismatch.");
            }
        }
        var bindings = new HashSet<(string operationId, int index)>();
        foreach (DungeonCharacterSaveData actor in actors.Values)
        {
            if (actor.pendingRetailPurchases == null)
                throw new InvalidOperationException("Shop purchase receipt collection missing.");
            foreach (RetailPurchaseRecoverySaveData receipt in actor.pendingRetailPurchases)
            {
                if (receipt == null) throw new InvalidOperationException("Shop purchase receipt missing.");
                receipt.Validate();
                if (receipt.buyerId != actor.persistentId)
                    throw new InvalidOperationException("Shop purchase buyer owner mismatch.");
                if (string.IsNullOrEmpty(receipt.shopUseOperationId)) continue;
                if (receipt.buyerId != actor.persistentId || !uses.TryGetValue(receipt.shopUseOperationId, out var owner)
                    || !bindings.Add((receipt.shopUseOperationId, receipt.shopUseCartIndex)))
                    throw new InvalidOperationException("Shop purchase receipt owner/cardinality mismatch.");
                owner.use.ValidatePurchaseBinding(owner.facilityId, receipt);
            }
        }
        foreach (var owner in uses.Values)
            for (int index = 0; index < owner.use.cart.Count; index++)
                if (!string.IsNullOrEmpty(owner.use.cart[index].pendingPurchaseOperationId)
                    && !bindings.Contains((owner.use.operationId, index)))
                    throw new InvalidOperationException("Shop cart has no matching pending purchase receipt.");
    }

    internal static void ValidateCore(ServiceRoomsSaveData services,
        ModularFacilityWorldSaveData facilities, DungeonSurvivalSaveData survival,
        DungeonCharacterConsumablesSaveData consumables, DungeonGameRestoreReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        if (services?.sessions == null || facilities?.buildings == null
            || survival?.activeTreatmentPlans == null || consumables?.activeDetoxTreatmentPlans == null
            || consumables.activeMealPlans == null || consumables.completedOperations == null
            || consumables.failedMealOperations == null)
            throw new InvalidOperationException("Service execution owner collections missing.");

        var active = new Dictionary<string, ServiceSessionSaveData>(StringComparer.Ordinal);
        var claimedSessions = new HashSet<string>(StringComparer.Ordinal);
        var claimedActors = new HashSet<string>(StringComparer.Ordinal);
        var claimedMeals = new HashSet<string>(StringComparer.Ordinal);
        foreach (ServiceSessionSaveData session in services.sessions)
        {
            if (session == null) throw new InvalidOperationException("Service session row missing.");
            if (session.stage < ServiceSessionStage.Completed && !active.TryAdd(session.sessionId, session))
                report.AddError("Duplicate active service session: " + session.sessionId);
        }

        foreach (ModularFacilityBuildingSaveData building in facilities.buildings)
        {
            if (building?.stateModules == null)
                throw new InvalidOperationException("Facility state modules missing.");
            foreach (BuildingStateModuleSaveData module in building.stateModules
                .Where(value => value != null && value.moduleId == BuildingStateModuleIds.FacilityOperation))
            {
                FacilityRuntimeState state = DungeonSaveJson.Deserialize<FacilityRuntimeState>(module.payload);
                if (state?.activeUses == null)
                    throw new InvalidOperationException("Facility execution list missing.");
                if (!state.IsValid(out string stateFailure))
                {
                    report.AddError(stateFailure);
                    continue;
                }
                foreach (FacilityUseExecutionState use in state.activeUses)
                {
                    if (use == null) throw new InvalidOperationException("Facility execution missing.");
                    ValidateMealOwner(use, building.persistentInstanceId, consumables, claimedMeals, report);
                    if (string.IsNullOrEmpty(use.serviceSessionId))
                    {
                        if (!claimedActors.Add(use.actorId))
                            report.AddError("Duplicate service execution actor: " + use.actorId);
                        continue; // A non-service facility may have a legitimate execution.
                    }
                    Claim(use.serviceSessionId, use.actorId, building.persistentInstanceId, "facility");
                }
            }

            BuildingStateModuleSaveData[] shopModules = building.stateModules
                .Where(value => value != null
                    && value.moduleId == BuildingStateModuleIds.ShopInteraction)
                .ToArray();
            if (shopModules.Length > 1)
            {
                report.AddError("Duplicate shop interaction state module: "
                    + building.persistentInstanceId);
                continue;
            }
            if (shopModules.Length == 0) continue;

            BuildingStateModuleSaveData shopModule = shopModules[0];
            if (shopModule.version != ShopInteractionState.CurrentVersion
                || string.IsNullOrWhiteSpace(shopModule.payload))
            {
                report.AddError("Shop interaction state module is missing/incompatible: "
                    + building.persistentInstanceId);
                continue;
            }

            ShopInteractionState shopState;
            try
            {
                shopState = DungeonSaveJson.Deserialize<ShopInteractionState>(shopModule.payload);
            }
            catch (Exception exception)
            {
                report.AddError("Shop interaction state module is invalid: "
                    + building.persistentInstanceId + ": " + exception.Message);
                continue;
            }
            string shopFailure = string.Empty;
            if (shopState == null
                || !shopState.IsValid(building.persistentInstanceId, out shopFailure))
            {
                report.AddError(string.IsNullOrWhiteSpace(shopFailure)
                    ? "Shop interaction state module is invalid: " + building.persistentInstanceId
                    : shopFailure);
                continue;
            }

            foreach (ShopUseExecutionState use in shopState.activeUses)
            {
                if (string.IsNullOrEmpty(use.serviceSessionId))
                {
                    if (!claimedActors.Add(use.actorId))
                        report.AddError("Duplicate service execution actor: " + use.actorId);
                    continue;
                }
                ServiceSessionSaveData terminal = services.sessions.SingleOrDefault(value =>
                    value.sessionId == use.serviceSessionId && value.stage == ServiceSessionStage.Cancelled);
                if (use.MatchesCancelledSession(terminal?.ToSnapshot(), building.persistentInstanceId))
                {
                    if (!claimedSessions.Add(use.serviceSessionId))
                        report.AddError("Duplicate service execution owner: " + use.serviceSessionId);
                    if (!claimedActors.Add(use.actorId))
                        report.AddError("Duplicate service execution actor: " + use.actorId);
                }
                else Claim(use.serviceSessionId, use.actorId, building.persistentInstanceId, "shop");
            }
        }

        foreach (SurvivalTreatmentPlanSaveData plan in survival.activeTreatmentPlans)
        {
            if (plan == null) throw new InvalidOperationException("Treatment owner missing.");
            if (string.IsNullOrEmpty(plan.serviceSessionId)) continue;
            if (plan.operationId != SurvivalFoodRuntime.TreatmentOperationPrefix + plan.serviceSessionId)
                report.AddError("Treatment operation/session identity mismatch: " + plan.operationId);
            if (active.ContainsKey(plan.serviceSessionId))
            {
                if (plan.phase >= SurvivalTreatmentPlanPhase.ServiceCompleted)
                    report.AddError("Completed treatment still owns an active service: " + plan.operationId);
                Claim(plan.serviceSessionId, plan.patientId, plan.facilityInstanceId, "treatment");
            }
            // The survival section distinguishes completed/outbox/lost-owner
            // plans from a pre-completion plan whose required service is absent.
        }

        foreach (CharacterDetoxTreatmentPlanSaveData plan in consumables.activeDetoxTreatmentPlans)
        {
            if (plan == null) throw new InvalidOperationException("Detox owner missing.");
            const string prefix = SurvivalFoodRuntime.DetoxOperationPrefix;
            if (string.IsNullOrEmpty(plan.operationId) || !plan.operationId.StartsWith(prefix, StringComparison.Ordinal)
                || plan.operationId.Length == prefix.Length)
            {
                report.AddError("Detox operation has no canonical service identity: " + plan.operationId);
                continue;
            }
            // Physical acknowledgement precedes service completion. The bridge
            // removes this plan after completing the original service.
            Claim(plan.operationId.Substring(prefix.Length), plan.characterId, plan.facilityInstanceId, "detox");
        }

        foreach (ServiceSessionSaveData session in active.Values)
            if (!string.IsNullOrEmpty(session.actorId) && !claimedSessions.Contains(session.sessionId))
                report.AddError("Active service session has no executable owner: " + session.sessionId);
        foreach (ServiceSessionSaveData session in services.sessions)
            if (session.terminalPhase == ServiceSessionTerminalPhase.Acknowledged
                && !claimedSessions.Contains(session.sessionId))
                report.AddError("Acknowledged service session has no pending cleanup owner: " + session.sessionId);

        void Claim(string sessionId, string actorId, string hubId, string owner)
        {
            if (!active.TryGetValue(sessionId, out ServiceSessionSaveData session)
                || string.IsNullOrEmpty(actorId) || session.actorId != actorId || session.hubId != hubId)
                report.AddError("Service execution owner mismatch (" + owner + "): " + sessionId);
            if (!claimedSessions.Add(sessionId))
                report.AddError("Duplicate service execution owner: " + sessionId);
            if (!claimedActors.Add(actorId))
                report.AddError("Duplicate service execution actor: " + actorId);
        }
    }

    private static void ValidateMealOwner(FacilityUseExecutionState use, string facilityId,
        DungeonCharacterConsumablesSaveData consumables, HashSet<string> claimed,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrEmpty(use.mealOperationId))
        {
            if (use.mealSucceeded) report.AddError("Facility meal success has no operation owner.");
            return;
        }
        string id = use.mealOperationId;
        var plans = consumables.activeMealPlans.Where(value => value != null && value.planId == id).ToArray();
        var results = consumables.completedOperations.Where(value => value != null && value.operationId == id).ToArray();
        var failures = consumables.failedMealOperations.Where(value => value != null && value.operationId == id).ToArray();
        if (!claimed.Add(id) || plans.Length > 1 || results.Length > 1 || failures.Length > 1
            || plans.Length + results.Length + failures.Length == 0)
        {
            report.AddError("Facility meal operation owner missing or duplicated: " + id);
            return;
        }
        var plan = plans.FirstOrDefault();
        var result = results.FirstOrDefault();
        var failure = failures.FirstOrDefault();
        bool valid = (plan == null || plan.characterId == use.actorId && plan.facilityInstanceId == facilityId)
            && (result == null || result.meal && !result.detox
                && result.characterId == use.actorId && result.facilityInstanceId == facilityId)
            && (failure == null || failure.characterId == use.actorId && failure.facilityInstanceId == facilityId
                && Enum.IsDefined(typeof(CharacterConsumablesFailureCode), failure.failureCode)
                && failure.failureCode is not (CharacterConsumablesFailureCode.None or CharacterConsumablesFailureCode.DeliveryPending)
                && !string.IsNullOrWhiteSpace(failure.detail) && failure.detail == failure.detail.Trim());
        if (plan != null)
        {
            valid &= plan.phase is CharacterMealPlanPhase.Eating or CharacterMealPlanPhase.ItemCommitted
                or CharacterMealPlanPhase.EffectsApplied or CharacterMealPlanPhase.EffectsPublished;
            valid &= (plan.phase == CharacterMealPlanPhase.EffectsPublished) == (result != null);
            valid &= failure == null || failure.recoveryPending && plan.phase != CharacterMealPlanPhase.Eating;
            valid &= result == null || result.itemDefinitionId == plan.itemDefinitionId;
        }
        else if (failure != null)
            valid &= !failure.recoveryPending && result == null;
        if (use.mealSucceeded || use.effectsCommitted)
            valid &= plan == null && result != null && failure == null;
        if (!valid) report.AddError("Facility meal operation owner/phase mismatch: " + id);
    }

    private static T Read<T>(IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections,
        string id, int version) where T : class
    {
        if (!sections.TryGetValue(id, out DungeonSaveSectionEnvelope section)
            || section == null || section.sectionVersion != version || string.IsNullOrWhiteSpace(section.payloadJson))
            throw new InvalidOperationException("Required service owner section missing/incompatible: " + id);
        return DungeonSaveJson.Deserialize<T>(section.payloadJson)
            ?? throw new InvalidOperationException("Service owner payload missing: " + id);
    }
}
