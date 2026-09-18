#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

// Root-owned cross-domain witness. Reuses only the existing facility fixture
// helpers, not its save restoration, broad test loop or automatic Play exit.
public sealed partial class PhysicalItemLogisticsPlayModeVerificationRunner
{
    public const string Wim023ProducerReportPath =
        "Artifacts/QA/wim-implementation/wim-023-main-prepared-part-production.txt";
    public static bool Wim023ProducerRunning { get; private set; }
    private readonly List<string> wim023ProducerLines = new();
    public const string Wim023ClinicalReportPath =
        "Artifacts/QA/wim-implementation/wim-023-main-produced-part-clinical-chain.txt";
    private bool wim023Clinical;
    private bool wim023Consumer;
    private bool wim023MotionHeat;
    private string Wim023OutputPath => wim023MotionHeat ? Wim023MotionHeatReportPath
        : wim023Consumer ? Wim023ConsumerReportPath
        : wim023Clinical ? Wim023ClinicalReportPath : Wim023ProducerReportPath;

    public static string StartWim023PartProduction() => StartWim023Witness(false);
    public static string StartWim023ClinicalChain() => StartWim023Witness(true);
    public static string StartWim023InstalledConsumer() => StartWim023Witness(false, true);
    public static string StartWim023MotionHeatConsumers() => StartWim023Witness(false, false, true);

    private static string StartWim023Witness(bool clinical, bool consumer = false, bool motionHeat = false)
    {
        Wim023Require(Application.isPlaying && !Wim023ProducerRunning,
            "Fresh disposable main Play required.");
        var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        var host = UnityEngine.Object.FindFirstObjectByType<GameManager>();
        Wim023Require(scope?.Container != null && host != null, "Main runtime unavailable.");
        var save = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Wim023Require(save != null, "Cannot protect user save files.");
        save.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        Wim023Pause(scope);
        var runner = new PhysicalItemLogisticsPlayModeVerificationRunner
            { wim023Clinical = clinical, wim023Consumer = consumer, wim023MotionHeat = motionHeat };
        Directory.CreateDirectory(Path.GetDirectoryName(runner.Wim023OutputPath));
        File.WriteAllText(runner.Wim023OutputPath, "result=RUNNING\n");
        Wim023ProducerRunning = true;
        try { host.StartCoroutine(runner.ObserveWim023PartProduction(scope)); }
        catch { Wim023ProducerRunning = false; throw; }
        return "RUNNING " + runner.Wim023OutputPath;
    }

    private IEnumerator ObserveWim023PartProduction(DungeonRuntimeLifetimeScope scope)
    {
        var pending = new Stack<IEnumerator>();
        pending.Push(RunWim023PartProduction(scope));
        Exception failure = null;
        while (pending.Count > 0)
        {
            object next = null;
            bool moved;
            try
            {
                moved = pending.Peek().MoveNext();
                if (moved) next = pending.Peek().Current;
            }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
            if (next is IEnumerator nested) pending.Push(nested);
            else yield return next;
        }
        try
        {
            while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
            Wim023Pause(scope);
            wim023ProducerLines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
            wim023ProducerLines.Add(wim023MotionHeat
                ? "scope=actual authored Beastkin/Demon, controlled persistent NPC/AbilityWork/proficiency, registered current whole-save historical installation, actual main AbilityMove position commits and CharacterEnvironmentUnityAdapter.Tick exposure changes. Controlled clock and one hot field cell; NOT natural navigation/clinical/weather or work/combat proof."
                : wim023Consumer
                ? "scope=actual authored Kobold actor, registered current whole-save restored installed part/body/order joins, actual main performance/body queries and CombatResolutionService.Resolve with fixed exogenous rolls. Historical installation is prepared through validated restore, NOT natural surgery/clinical or new production proof. Movement/heat/work consumers are not covered by this combat witness."
                : wim023Clinical
                ? "scope=controlled authored Orc party, facilities, qualifications, neutral environment, supplies and fixed outcome stream; real production commands -> actual surgery UI pointer -> natural part/supply haul, patient admission and doctor work -> installed heart effect. NOT natural crafting, uncontrolled clinical-risk/balance or whole-save certification. No admission/progress/result writes."
                : "scope=main production bill + exact physical input + public work commands + prepared-output buffer + unique surgical-part join. Party selection, fixture M06, research prerequisite and physical inputs are controlled preparation. NOT natural AI crafting, input hauling, output hauling, installation, surgery risk or save-roundtrip certification.");
            wim023ProducerLines.Add(wim023Consumer || wim023MotionHeat
                ? "cleanup=paused disposable main Play; prepared historical whole-world restore is in-memory only; user persistence disabled before party selection; no source-asset modification or scene save. Fixture retained until operator stops."
                : "cleanup=paused disposable main Play; runtime fixture/output retained for inspection until operator stops. User persistence disabled before party selection; no whole-world save restore, source-asset modification or scene save.");
            File.WriteAllLines(Wim023OutputPath, wim023ProducerLines);
        }
        finally { Wim023ProducerRunning = false; }
    }

    private IEnumerator RunWim023PartProduction(DungeonRuntimeLifetimeScope scope)
    {
        var owner = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        Wim023Require(owner != null, "Owner preparation unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            bool expanded = scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                .TryReconcileNewRunTierZero(out _, out string reason);
            Wim023Require(expanded, "TierZero preparation: " + reason);
            var button = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .SingleOrDefault(x => x.name == (wim023Clinical ? "OwnerOption_1002" : "OwnerOption_1001") && x.isActiveAndEnabled);
            Wim023Require(button != null && button.interactable && EventSystem.current != null,
                "Actual owner-selection button unavailable.");
            var pointer = new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Wim023Pause(scope);
            Wim023Require(owner.CurrentOwnerActor != null, "Party did not publish owner.");
        }

        if (wim023MotionHeat)
        {
            yield return RunWim023MotionHeatConsumers(scope);
            yield break;
        }

        if (wim023Consumer)
        {
            yield return RunWim023InstalledCombatConsumer(scope);
            yield break;
        }

        var world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(true);
        var crafter = world.Characters.Where(x => x != null && !x.IsOwner && !x.IsDead
                && x.CurrentLifecycleState == CharacterLifecycleState.Active
                && x.GetComponent<AbilityWork>() != null)
            .OrderBy(x => x.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Wim023Require(crafter != null, "No actual active staff with work capability.");
        var grid = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>()?.grid;
        Wim023Require(grid != null, "Main grid missing.");
        var asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Medical/M06_보철조립대.asset");
        Wim023Require(asset != null, "Authored M06 missing.");
        bool placeable = TryFindRegisterablePosition(grid, asset,
            FindReachableCells(grid, crafter.GetNowXY(), 64), out var position);
        Wim023Require(placeable, "No registerable M06 fixture footprint.");
        var bench = CreateInjectedFacility(scope, grid, asset, position,
            "WIM023_Actual_M06_Producer", registerOnGrid: true);
        Wim023Require(bench != null, "M06 runtime/grid publication failed.");
        wim023ProducerLines.Add("preparation=crafter:" + crafter.Identity.PersistentId
            + "; M06:" + bench.PersistentInstanceId.Value + "@" + position);

        var capacities = scope.Container.Resolve<IFacilityBufferMassCapacityQuery>();
        string outputDestination = ProductionBillRuntime.OutputDestinationPrefix
            + bench.PersistentInstanceId.Value;
        FacilityBufferMassCapacitySnapshot capacity = default;
        // AssessCycleStart is read-only; the prepared-output adapter registers
        // the destination/profile when it reserves the resolved completed batch.
        // A fresh no-bill fixture must not wait for a nonexistent background
        // registration. Verify the actual 7200g profile after real publication.
        bool profileBeforeProduction = capacities.TryGetCapacity(
            outputDestination, position, out capacity);
        wim023ProducerLines.Add("preProductionProfile=" + profileBeforeProduction
            + "; publication capacity is checked after the completed batch commits");

        if (wim023Clinical)
        {
            var produced = ProduceWim023Part(scope, crafter, bench,
                "recipe:surgery:heart-augmentation", "surgery:prosthetic:heart/variant/orc-combat-heart", "heart");
            yield return RunWim023Clinical(scope, grid, owner.CurrentOwnerActor, crafter, bench, produced);
            yield break;
        }

        // Existing baseline and new composed-content output; no synthetic SO.
        ProduceWim023Part(scope, crafter, bench,
            "recipe:surgery:prosthetic-arm", "surgery:prosthetic:arm:left", "arm:left");
        ProduceWim023Part(scope, crafter, bench,
            "recipe:surgery:brain-assist", "surgery:prosthetic:brain/variant/human-neural-assist", "brain");
        Wim023Require(capacities.TryGetCapacity(outputDestination, position, out capacity)
            && capacity.Profile.MaxMassGrams == 7200 && capacity.ReservedMassGrams == 0,
            "Publication changed authored capacity or leaked admission reservation.");
        wim023ProducerLines.Add("PASS cases=2; exact part quantity=2; mass=3600g; capacity=7200g; reserved=0; no direct TryCreateCraftedPart call");
    }

    private SurgicalPartInstance ProduceWim023Part(DungeonRuntimeLifetimeScope scope, CharacterActor crafter,
        Facility bench, string recipeId, string itemId, string nodeId)
    {
        var content = scope.Container.Resolve<IResourceEconomyContentCatalog>();
        var orders = scope.Container.Resolve<IProductionBillOrderCommand>();
        var bills = scope.Container.Resolve<IProductionBillQuery>();
        var work = scope.Container.Resolve<IProductionBillWorkExecution>();
        var items = scope.Container.Resolve<IWorldItemStackRuntime>();
        var parts = scope.Container.Resolve<ISurgicalPartRuntime>();
        Wim023Require(content.TryGetRecipe(recipeId, out var recipe) && recipe != null,
            "Authored recipe missing: " + recipeId);
        var expected = recipe.CaptureCanonicalOutputs().Single(x => ProductionOutputRoleRules.IsPhysical(x.Role));
        Wim023Require(expected.ItemId == itemId && expected.Amount == 1 && expected.Probability == 1f
            && bench.MatchesProductionWorkstation(recipe), "Recipe/output/M06 authored mismatch.");
        if (!string.IsNullOrEmpty(recipe.RequiredResearchId))
            scope.Container.Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch.State.Projects
                .RestoreCompleted(new ResearchProjectId(recipe.RequiredResearchId));
        var priorIds = new HashSet<string>(parts.Parts.Select(x => x.partInstanceId), StringComparer.Ordinal);
        var added = orders.AddBill(bench, recipeId, ProductionOrderMode.RepeatCount, 1);
        Wim023Require(added.Succeeded, "AddBill: " + Wim023Failure(added.Failure));
        var bill = bills.GetBills(bench).Single(x => x.BillId == added.BillId);
        foreach (var input in bill.Inputs)
        {
            bool spawned = items.SpawnItemAt(input.ItemId, input.Amount, bench.centerPos,
                WorldItemStackState.FacilityBuffer, bill.MaterialDestinationId, out int amount);
            Wim023Require(spawned && amount == input.Amount, "Physical input rejected: " + input.ItemId);
        }
        var available = work.CheckWorkAvailability(bench, recipe.WorkTypeId);
        Wim023Require(available.Available, "Work availability: " + Wim023Failure(available.Failure));
        var begun = work.BeginWork(crafter, bench, recipe.WorkTypeId);
        Wim023Require(begun.Succeeded, "BeginWork: " + Wim023Failure(begun.Failure));
        // This deliberately calls the real work command, not natural work AI.
        // No frame/yield between completion and buffer observation: distribution
        // cannot turn the exact committed output into a downstream loose stack.
        var result = work.ExecuteWork(crafter, bench, bill.BillId, recipe.RequiredWork + 1f);
        Wim023Require(result.Succeeded && result.CycleCompleted,
            "ExecuteWork: " + Wim023Failure(result.Failure) + "; outcome=" + result.Outcome
            + "; succeeded=" + result.Succeeded + "; cycleCompleted=" + result.CycleCompleted);
        var created = parts.Parts.Where(x => !priorIds.Contains(x.partInstanceId)).ToArray();
        Wim023Require(created.Length == 1, "Expected exactly one newly owned surgical part.");
        var part = created[0];
        var stacks = items.GetAllStacks().Where(x => x.ItemInstanceId == part.physicalItemInstanceId).ToArray();
        Wim023Require(part.itemDefinitionId == itemId && part.nodeId == nodeId
            && part.kind == SurgicalPartKind.Prosthetic && !part.installed
            && !string.IsNullOrWhiteSpace(part.sourceProductionCommitId)
            && !string.IsNullOrWhiteSpace(part.physicalItemInstanceId)
            && stacks.Length == 1, "Physical part identity/semantic join mismatch.");
        var stack = stacks[0];
        Wim023Require(stack.StackId == part.worldStackId && stack.ItemId == itemId && stack.Quantity == 1
            && stack.State == WorldItemStackState.FacilityOutputBuffer && stack.Position == bench.centerPos
            && stack.DestinationId == bill.OutputDestinationId,
            "Prepared output was not published to its exact physical facility buffer.");
        Wim023Require(items.MassQuery.GetDefinitionUnitMass((ItemDefinitionId)itemId).Value == 1800,
            "Authored finished-part mass drift.");
        Wim023Require(!items.GetAllStacks().Any(x => x.DestinationId == bill.MaterialDestinationId
            && x.Quantity > 0), "Consumed bill input remained in the material buffer.");
        int count = parts.Parts.Count;
        string identity = part.partInstanceId + "/" + part.physicalItemInstanceId + "/" + part.sourceProductionCommitId;
        var retry = work.ExecuteWork(crafter, bench, bill.BillId, recipe.RequiredWork + 1f);
        Wim023Require(!retry.CycleCompleted && parts.Parts.Count == count
            && items.GetAllStacks().Count(x => x.ItemInstanceId == part.physicalItemInstanceId) == 1,
            "Terminal bill replay duplicated the prepared part.");
        wim023ProducerLines.Add("PASS " + recipeId + "; item=" + itemId + "; node=" + nodeId
            + "; bill=" + bill.BillId.Value + "; part/instance/commit=" + identity
            + "; stack=" + stack.StackId + "; output=" + bill.OutputDestinationId
            + "; grams=1800; consumedInputs=" + string.Join(",", bill.Inputs.Select(x => x.ItemId + "x" + x.Amount))
            + "; terminalRetry=" + retry.Outcome);
        return part;
    }

    private IEnumerator RunWim023Clinical(DungeonRuntimeLifetimeScope scope, Grid grid,
        CharacterActor patient, CharacterActor crafter, Facility bench, SurgicalPartInstance produced)
    {
        const string procedureId = "procedure:orc-combat-heart";
        Wim023Require(patient != null && patient.Identity.SpeciesTag == "Orc",
            "Clinical patient must be the actual authored Orc owner, not a synthetic human.");
        var world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        var performance = scope.Container.Resolve<ICharacterPerformanceQuery>();
        var doctor = world.Characters.Where(x => x != null && x != patient && !x.IsDead
                && x.CurrentLifecycleState == CharacterLifecycleState.Active && x.GetAbility<AbilityWork>() != null)
            .Where(x => performance.Evaluate(x, CharacterPerformanceFormulaIds.SurgerySuccess).IsApplicable)
            .OrderByDescending(x => performance.Evaluate(x, CharacterPerformanceFormulaIds.SurgerySuccess).Value)
            .ThenBy(x => x.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Wim023Require(doctor != null, "No actual staff with applicable surgical capability.");
        var haulers = world.Characters.Where(x => x != null && x != patient && x != doctor
                && !x.IsDead && x.CurrentLifecycleState == CharacterLifecycleState.Active
                && x.GetComponent<AbilityHaul>() != null && x.GetAbility<AbilityWork>() != null).ToArray();
        Wim023Require(haulers.Length > 0, "An independent actual hauler is required.");
        var calendar = scope.Container.Resolve<IGameCalendar>();
        var proficiency = scope.Container.Resolve<ICharacterProficiencyCommand>();
        proficiency.AddDirectExperience(CharacterPersistentIdentity.Require(doctor),
            BuiltInCharacterProficiencyIds.Medicine, 900f, calendar.AbsoluteHour, applyLearningMultiplier: false);
        proficiency.AddDirectExperience(CharacterPersistentIdentity.Require(doctor),
            BuiltInCharacterProficiencyIds.Scholarship, 300f, calendar.AbsoluteHour, applyLearningMultiplier: false);
        scope.Container.Resolve<IBlueprintResearchStateService>().GetState().Projects
            .RestoreCompleted(new ResearchProjectId("research:medical:surgery"));
        scope.Container.Resolve<IServiceSessionRuntime>().SetAdvertisingEnabled(ServiceCategory.Medical, false);
        var procedures = scope.Container.Resolve<ISurgicalProcedureCatalog>();
        Wim023Require(procedures.TryGet(procedureId, out var procedure), "Authored Orc procedure missing.");
        var rooms = scope.Container.Resolve<IRoomLayoutCache>();
        var facilities = scope.Container.Resolve<ISurgicalFacilityQuery>();
        var tableAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/Medical/M03_외과수술대.asset");
        var supportAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/Medical/M04_세정대.asset");
        Wim023Require(tableAsset != null && supportAsset != null, "Authored surgery facilities missing.");
        var actorCells = new HashSet<Vector2Int>(world.Characters.Where(x => x != null).Select(x => x.GetNowXY()));
        Facility table = null;
        foreach (var room in rooms.GetLayout(grid).Rooms.Where(x => x != null && x.IsUsable)
                     .OrderBy(x => x.Cells.Min(c => Math.Abs(c.x - patient.GetNowXY().x) + Math.Abs(c.y - patient.GetNowXY().y))))
        {
            var cells = new HashSet<Vector2Int>(room.Cells);
            var candidates = room.Cells.OrderBy(c => c.x).ThenBy(c => c.y).ToArray();
            foreach (var cell in candidates)
            {
                var footprint = tableAsset.GetGridPosList(cell);
                if (footprint.Any(c => !cells.Contains(c) || actorCells.Contains(c)
                    || Math.Abs(c.x - patient.GetNowXY().x) + Math.Abs(c.y - patient.GetNowXY().y) < 4
                    || grid.GetGridCell(c)?.CanOccupy(tableAsset.Placement.Layer) != true)) continue;
                var supportCells = candidates.Where(c => supportAsset.GetGridPosList(c).All(p =>
                    cells.Contains(p) && !footprint.Contains(p) && !actorCells.Contains(p)
                    && grid.GetGridCell(p)?.CanOccupy(supportAsset.Placement.Layer) == true)).ToArray();
                if (supportCells.Length == 0) continue;
                table = CreateInjectedFacility(scope, grid, tableAsset, cell, "WIM023_ClinicalTable", true);
                var support = CreateInjectedFacility(scope, grid, supportAsset, supportCells[0], "WIM023_ClinicalSterilization", true);
                Wim023Require(table != null && support != null, "Clinical facility publication failed.");
                break;
            }
            if (table != null) break;
        }
        rooms.Clear();
        Wim023Require(table != null, "No usable room can hold actual M03 and M04.");
        var facility = facilities.Evaluate(table, procedure);
        Wim023Require(facility.IsAvailable, "Clinical facility unavailable: " + Wim023Failure(facility.BlockFailure));

        var clock = scope.Container.Resolve<IGameClock>();
        var environment = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        var originalEnvironment = environment.Capture();
        var safeEnvironment = JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(JsonUtility.ToJson(originalEnvironment));
        safeEnvironment.cells.Clear();
        for (int y = 0; y < safeEnvironment.height; y++)
            for (int x = 0; x < safeEnvironment.width; x++)
                safeEnvironment.cells.Add(new EnvironmentalCellSaveData
                    { x = x, y = y, temperatureC = 22, airQuality = 100, lightLevel = 100 });
        environment.Restore(environment.PrepareRestore(safeEnvironment));
        foreach (var actor in world.Characters.Where(x => x != null))
        {
            actor.Brain?.StopCurrentActionForReplan("wim023-clinical-preparation");
            actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
            foreach (var need in new[] { CharacterCondition.HUNGER, CharacterCondition.THIRST, CharacterCondition.SLEEP,
                         CharacterCondition.HYGIENE, CharacterCondition.EXCRETION, CharacterCondition.FUN })
                actor.stats[need] = 100f;
        }
        var outcome = scope.Container.Resolve<IRandomStreamProvider>().Get("medical:surgery-outcomes");
        outcome.Restore(1UL); // Existing deterministic success-path fixture; NOT authored risk override.
        var body = scope.Container.Resolve<ICharacterBodyHealthQuery>();
        float beforeMaximum = body.GetVitals(patient).MaximumHealth;
        float beforeCurrent = body.GetVitals(patient).CurrentHealth;
        float producedQuality = produced.quality;
        var effects = scope.Container.Resolve<ICharacterSurgicalPartGameplayEffectSourceQuery>();
        Wim023Require(effects.GetInstalledPartSources(patient).Count == 0, "Patient already has installed effects.");
        var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
            .Where(x => x.isActiveAndEnabled).OrderByDescending(x => x.sortingOrder).FirstOrDefault();
        Wim023Require(canvas != null, "Actual surgery canvas unavailable.");
        scope.Container.Resolve<ICharacterSurgeryWindowService>().Open(patient, canvas.transform);
        yield return null;
        var view = UnityEngine.Object.FindFirstObjectByType<CharacterSurgeryWindowView>();
        Wim023Require(view != null, "Actual surgery window unavailable.");
        Wim023Select(view, "procedure", "Procedure", procedureId);
        Wim023Select(view, "node", "Target", "heart");
        Wim023Select(view, "part", "Part", produced.partInstanceId);
        Wim023Select(view, "doctor", "Doctor", doctor.Identity.PersistentId);
        Wim023Select(view, "facility", "Facility", facilities.GetFacilityId(table));
        var schedule = view.GetComponentsInChildren<Button>(true).Single(x => x.name == "Schedule");
        Wim023Click(schedule);
        var surgery = scope.Container.Resolve<ISurgeryQuery>();
        var order = surgery.ActiveOrders.SingleOrDefault(x => x.IsActive && x.subject.subjectId == patient.Identity.PersistentId);
        Wim023Require(order != null, "Actual pointer scheduling failed: " + string.Join(" | ",
            view.GetComponentsInChildren<TMPro.TMP_Text>(true).Where(x => x.name == "Details").Select(x => x.text)));
        Wim023Require(order.procedureId == procedureId && order.selectedPartInstanceId == produced.partInstanceId
            && order.targetNodeId == "heart" && order.preferredDoctorId == doctor.Identity.PersistentId
            && string.IsNullOrEmpty(order.doctorId)
            && order.completedWork == 0 && !order.patientAdmitted && !order.resultRolled,
            "UI selection or pre-admission authority mismatch.");
        Wim023Click(view.GetComponentsInChildren<Button>(true).Single(x => x.name == "Close"));
        var items = scope.Container.Resolve<IWorldItemStackRuntime>();
        Vector2Int supply = FindReachableCells(grid, crafter.GetNowXY(), 64)
            .First(c => !table.IsWorkAccessGridPosition(grid, c) && !table.buildPoses.Contains(c)
                && c != bench.centerPos);
        foreach (var material in order.materials.Where(x => !x.optional))
            Wim023Require(items.SpawnItemAt(material.itemId, material.quantity, supply,
                    WorldItemStackState.Loose, string.Empty, out int spawned) && spawned == material.quantity,
                "Physical clinical supply rejected: " + material.itemId);
        var fluid = table.BuildingData.GetAbility<BuildingProcessFluidAbility>();
        if (fluid != null && fluid.Supports(BuiltInWorkTypeIds.Surgery) && fluid.cleanWaterPerCycle > 0)
            Wim023Require(items.SpawnItemAt("resource:clean-water", 1, table.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    "plumbing:process-water:" + facilities.GetFacilityId(table) + ":" + BuiltInWorkTypeIds.Surgery.Value,
                    out int water) && water == 1, "Physical process water preparation failed.");

        var doctorWork = doctor.GetAbility<AbilityWork>();
        doctorWork.SetDutyState(AbilityWork.DutyState.OnDuty);
        doctorWork.WorkPriorities.SetPriority(BuiltInWorkTypeIds.Surgery, WorkPriorityLevel.Priority1);
        foreach (var hauler in haulers)
        {
            hauler.GetAbility<AbilityWork>().SetDutyState(AbilityWork.DutyState.OnDuty);
            hauler.GetAbility<AbilityWork>().WorkPriorities.SetPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
            hauler.SetAiPaused(false);
            hauler.Brain?.RequestImmediateReplan(clearFailures: true);
        }
        doctor.SetAiPaused(false);
        doctor.Brain?.PreferWorkActionOnNextDecision(BuiltInWorkTypeIds.Surgery, 120f);
        doctor.Brain?.RequestImmediateReplan(clearFailures: true);
        wim023ProducerLines.Add("clinicalPreparation=Orc actual owner; doctor=" + doctor.Identity.PersistentId
            + "; table=" + facilities.GetFacilityId(table) + "; order=" + order.orderId
            + "; supplies=" + string.Join(",", order.materials.Select(x => x.itemId + "x" + x.quantity))
            + "; outcomeState=1; neutral field22C/air100/light100 refreshed5gameSeconds; preMaxHP=" + beforeMaximum);
        File.WriteAllLines(Wim023OutputPath, wim023ProducerLines.Concat(new[] { "result=RUNNING_CLINICAL" }));
        float wallStart = Time.realtimeSinceStartup, gameStart = clock.Time, nextEnvironment = clock.Time + 5;
        string lastStage = null;
        bool carried = false, delivered = false, admissionMove = false, admitted = false, doctorWorked = false;
        Vector2Int patientStart = patient.GetNowXY();
        UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = false;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 4;
        try
        {
            while (order.IsActive && Time.realtimeSinceStartup - wallStart < 90 && clock.Time - gameStart < 600)
            {
                if (clock.Time >= nextEnvironment)
                {
                    environment.Restore(environment.PrepareRestore(safeEnvironment));
                    nextEnvironment = clock.Time + 5;
                }
                var stack = items.GetAllStacks().SingleOrDefault(x => x.ItemInstanceId == produced.physicalItemInstanceId);
                carried |= stack?.State == WorldItemStackState.Carried;
                delivered |= stack != null && stack.State == WorldItemStackState.FacilityBuffer
                    && stack.DestinationId == order.materialDestinationId;
                admissionMove |= order.admissionMoveRequested && patient.GetAbility<AbilityMove>().IsSystemMoveInProgress;
                admitted |= order.patientAdmitted && table.IsWorkAccessGridPosition(grid, patient.GetNowXY());
                doctorWorked |= doctorWork.isWorking && doctorWork.AssignedWorkTypeId == BuiltInWorkTypeIds.Surgery
                    && order.completedWork > 0;
                string stage = order.state + "/" + order.statusData.code + "/" + stack?.State;
                if (stage != lastStage)
                {
                    wim023ProducerLines.Add("observed=" + stage + "; wu=" + order.completedWork
                        + "; patient=" + patient.GetNowXY() + "; doctor=" + doctor.GetNowXY()
                        + "; wall=" + (Time.realtimeSinceStartup - wallStart) + "; game=" + (clock.Time - gameStart));
                    lastStage = stage;
                }
                yield return null;
            }
            Wim023Pause(scope);
            wim023ProducerLines.Add("clinicalTerminal=" + order.state + "; status=" + order.statusData.code
                + "; wu=" + order.completedWork + "; carried=" + carried + "; delivered=" + delivered
                + "; admissionMove=" + admissionMove + "; admitted=" + admitted + "; doctorWork=" + doctorWorked
                + "; wall=" + (Time.realtimeSinceStartup - wallStart) + "; game=" + (clock.Time - gameStart));
            if (order.state == SurgeryOrderState.TerminalDraining)
                CaptureWim023TerminalDiagnostic(scope, order, world, items);
            wim023ProducerLines.Add("clinicalVitals=" + body.GetVitals(patient).MaximumHealth
                + "/" + body.GetVitals(patient).CurrentHealth + "; before=" + beforeMaximum + "/" + beforeCurrent);
            Wim023Require(order.state == SurgeryOrderState.Completed && order.resultSucceeded && order.resultRolled,
                "Natural surgery did not complete successfully: " + order.state + "/" + order.statusData.code);
            Wim023Require(carried && delivered && admissionMove && admitted && doctorWorked
                && patient.GetNowXY() != patientStart && order.materialsConsumed,
                "Missing actual haul/admission/work/consumption observation.");
            var actual = scope.Container.Resolve<ISurgicalPartRuntime>().Parts.Single(x => x.partInstanceId == produced.partInstanceId);
            var node = scope.Container.Resolve<IAnatomyHealthRuntime>().GetAnatomySnapshot(patient).Nodes.Single(x => x.nodeId == "heart");
            var contribution = effects.GetInstalledPartSources(patient).Single(x => x.SourceRef.SourceId == actual.partInstanceId);
            Wim023Require(actual.installed && actual.installedSubjectId == patient.Identity.PersistentId
                && actual.installationOrderId == order.orderId && node.installedPartId == actual.partInstanceId
                && !items.GetAllStacks().Any(x => x.ItemInstanceId == actual.physicalItemInstanceId),
                "Installed body/part/physical ownership mismatch.");
            var binding = contribution.Effects.Single(x => x.definition.TargetId == GameplayEffectTargetIds.MaximumHealth);
            float maximum = body.GetVitals(patient).MaximumHealth;
            // Independent authored contract: +10% at quality/condition1. Actual
            // crafted quality is retained; never set it to1 just to fit the test.
            float expectedMultiplier = 1f + .10f * node.ConditionFactor * producedQuality;
            Wim023Require(Mathf.Abs(actual.quality - producedQuality) < .0001f
                && Mathf.Abs(node.installedPartEfficiency - producedQuality) < .0001f
                && Mathf.Abs(binding.value - expectedMultiplier) < .0001f
                && Mathf.Abs(maximum - beforeMaximum * expectedMultiplier) < .01f,
                "Installed maximum-health effect not consumed: binding=" + binding.value + "; HP=" + beforeMaximum + "->" + maximum);
            Wim023Require(body.GetVitals(patient).CurrentHealth <= beforeCurrent + .01f,
                "Installing maximum-health effect granted free current health.");
            wim023ProducerLines.Add("PASS actual Orc heart produced->UI->carried->surgery buffer->patient walk/admit->doctor work->installed; maxHP="
                + beforeMaximum + "->" + maximum + "; quality=" + producedQuality + "; condition=" + node.ConditionFactor
                + "; exact single effect=" + expectedMultiplier + "; loose/carried installed copy=0");
        }
        finally
        {
            Wim023Pause(scope);
            environment.Restore(environment.PrepareRestore(originalEnvironment));
        }
    }

    private void CaptureWim023TerminalDiagnostic(DungeonRuntimeLifetimeScope scope, SurgeryOrder order,
        ICharacterAiWorldRegistry world, IWorldItemStackRuntime items)
    {
        // A bounded read-only failure snapshot. Diagnostic failure must not
        // replace the original clinical failure or change any drain state.
        try
        {
            var drain = scope.Container.Resolve<IFacilityBufferDestinationCustodyDrainLiveQuery>().Drains
                .SingleOrDefault(x => x.StepOperationId == order.materialTerminalStepOperationId);
            wim023ProducerLines.Add(drain == null ? "drainDiagnostic=missing child" : "drainDiagnostic=" + drain.Phase
                + "; actors=" + drain.CompletedActorCount + "/" + drain.SourceActorCount
                + "; operations=" + drain.ReleasedOperationCount + "/" + drain.SourceOperationCount
                + "; input=" + drain.InputQuantity + "/" + drain.InputMassGrams + "; commit=" + drain.CommitId);
            if (scope.Container.Resolve<IProductionInputDestinationCustodyDrainService>()
                .TryCapture(order.materialTerminalStepOperationId, out var child))
                wim023ProducerLines.Add("drainChild=" + JsonUtility.ToJson(child));
            foreach (var intent in items.CaptureHaulDeliveryIntentsByDestination(order.materialDestinationId))
                wim023ProducerLines.Add("drainIntent=" + JsonUtility.ToJson(intent));
            foreach (var actor in world.Characters.Where(x => x != null))
            {
                var haul = actor.GetComponent<AbilityHaul>();
                if (haul == null) continue;
                wim023ProducerLines.Add("drainActor=" + actor.Identity.PersistentId
                    + "; frozen=" + haul.IsCapacityRoutingQuiescenceFrozen + "; hauling=" + haul.IsHauling
                    + "; operations=" + string.Join(",", haul.CaptureActiveHaulOperationIds())
                    + "; stage=" + haul.CurrentExecutionStage + "; failure=" + haul.LastFailureReason
                    + "; carried=" + string.Join("|", actor.CarryInventory.Items.Select(x => JsonUtility.ToJson(x))));
            }
        }
        catch (Exception error) { wim023ProducerLines.Add("drainDiagnosticError=" + error.Message); }
    }

    private static void Wim023Select(CharacterSurgeryWindowView view, string field, string row, string id)
    {
        // Read the known view's selection projection; mutate only via real pointer input.
        var options = (IReadOnlyList<SurgeryWindowOption>)typeof(CharacterSurgeryWindowView)
            .GetField(field + "Options", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
        var index = typeof(CharacterSurgeryWindowView).GetField(field + "Index", BindingFlags.Instance | BindingFlags.NonPublic);
        Wim023Require(options.Any(x => x.Id == id), "Actual UI option missing: " + row + "/" + id);
        var next = view.GetComponentsInChildren<Button>(true).Single(x => x.name == "Next" && x.transform.parent.name == row + "Row");
        for (int i = 0; i < options.Count && options[(int)index.GetValue(view)].Id != id; i++) Wim023Click(next);
        Wim023Require(options[(int)index.GetValue(view)].Id == id, "UI selection failed: " + row);
    }

    private static void Wim023Click(Button button)
    {
        Wim023Require(button != null && button.interactable && EventSystem.current != null, "Pointer target unavailable.");
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    private static void Wim023Pause(DungeonRuntimeLifetimeScope scope)
    {
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        UnityEngine.Object.FindFirstObjectByType<GameManager>().isPause = true;
    }

    private static void Wim023Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static string Wim023Failure(DomainFailure failure) =>
        failure.Code + " [" + string.Join("|", failure.Parameters.ToArray()) + "]";
}
#endif
