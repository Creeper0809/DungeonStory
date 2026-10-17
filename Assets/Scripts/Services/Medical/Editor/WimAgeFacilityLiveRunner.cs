#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// Main-owned integration witness. The grid objects and research are controlled
// setup. Actual UI, runtime scheduling and whole-world restore are the subjects.
public sealed class WimAgeFacilityLiveRunner
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-023-age-facility-live.txt";
    private static bool running;
    private static readonly string[] Procedures = {
        "procedure:organ-regeneration", "procedure:blood-rejuvenation",
        "procedure:rune-hibernation", "procedure:whole-body-regeneration", "procedure:temporal-stasis" };
    private static readonly int[] Buildings = { 8868, 8869, 8870, 8871, 8872 };
    private readonly List<string> lines = new();
    private readonly List<string> facilityIds = new();
    private DungeonRuntimeLifetimeScope scope;
    private ICharacterAiWorldRegistry world;
    private IDungeonGameSaveService saves;
    private string patientId;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running, "Start once in fresh disposable main Play.");
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime is not initialized.");
        var saveCommands = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(saveCommands != null, "Cannot protect user saves.");
        saveCommands.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        var game = FindFirstObjectByType<GameManager>();
        Require(game != null, "Missing main coroutine host.");
        game.isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try { game.StartCoroutine(new WimAgeFacilityLiveRunner().Observe()); }
        catch { running = false; throw; }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(Run());
        Exception failure = null;
        while (stack.Count > 0)
        {
            object value = null;
            bool moved;
            try { moved = stack.Peek().MoveNext(); if (moved) value = stack.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (value is IEnumerator nested) stack.Push(nested);
            else yield return value;
        }
        while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add("scope=actual owner/party and surgery selector UI, actual AgeTreatmentCommand and direct Schedule, current full-world JSON; authored placement/research are controlled setup; no clinical work/effect/automatic emergency or whole-WIM023 completion claim");
        lines.Add("cleanup=operator stops disposable Play; no scene/user persistence writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        var owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Missing owner preparation.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                .TryReconcileNewRunTierZero(out var expansion, out string failure)
                && expansion.CurrentInteriorColumns == 29, "TierZero: " + failure);
            var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(x => x != null
                && x.gameObject.scene.isLoaded && x.gameObject.activeInHierarchy && x.name == "OwnerOption_1001");
            Require(button != null && button.IsInteractable()
                && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual owner UI unavailable.");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Party UI did not publish owner.");
        }
        Require(world.TryGetGrid(out Grid grid) && grid != null, "Missing main grid.");
        var profiles = scope.Container.Resolve<IAnatomyProfileCatalog>();
        var patient = world.Characters.Where(x => x != null && !x.IsOwner && !x.IsDead
            && x.characterType == CharacterType.NPC
            && !string.Equals(profiles.GetForSpecies(x.Identity.SpeciesTag).AnatomyFamily, "construct", StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Require(patient != null, "No actual biological staff patient.");
        patientId = patient.Identity.PersistentId;
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(true);
        Require(scope.Container.Resolve<ICharacterLifeQuery>().TryGet(CharacterPersistentIdentity.Require(patient), out _),
            "Actual patient has no life authority.");
        var catalog = scope.Container.Resolve<ISurgicalProcedureCatalog>();
        var query = scope.Container.Resolve<ISurgicalFacilityQuery>();
        var research = scope.Container.Resolve<ProgressionSceneRuntimeReferences>().BlueprintResearch;
        var definitions = AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<BuildingSO>).ToArray();
        for (int i = 0; i < Procedures.Length; i++)
        {
            Require(catalog.TryGet(Procedures[i], out var procedure), "Missing authored procedure " + Procedures[i]);
            Require(research.TryCompleteProjectImmediatelyForVerification(new ResearchProjectId(procedure.RequiredResearchId),
                out string failure), "Research setup: " + failure);
            var definition = definitions.Single(x => x != null && x.id == Buildings[i]);
            facilityIds.Add(Place(grid, patient, definition).RequirePersistentInstanceId().Value);
        }
        // At least one co-located correct support must not qualify the wrong
        // primary. Five-in-one-room is not a gameplay requirement; the starting
        // dungeon legitimately distributes these fixtures among several rooms.
        var roomQuery = scope.Container.Resolve<IRoomLayoutCache>();
        int sharedPairs = 0;
        for (int i = 0; i < facilityIds.Count; i++)
        {
            Require(roomQuery.TryGetRoom(Facility(i), out var room) && room.IsUsable,
                "Authored fixture has no usable room: " + Buildings[i]);
            for (int j = i + 1; j < facilityIds.Count; j++)
                if (room.Furniture.Contains(Facility(j))) sharedPairs++;
        }
        Require(sharedPairs > 0, "Missing the representative co-located support/primary crossing case.");
        for (int i = 0; i < Procedures.Length; i++)
        {
            catalog.TryGet(Procedures[i], out var procedure);
            var candidates = query.GetCandidateFacilities(procedure);
            Require(candidates.Count == 1 && query.GetFacilityId(candidates[0].PrimaryFacility) == facilityIds[i],
                "Candidate list must contain only the exact primary for " + Procedures[i]);
            for (int j = 0; j < facilityIds.Count; j++)
                Require(query.Evaluate(Facility(j), procedure).IsAvailable == (i == j),
                    "Primary/support cross-binding " + Procedures[i] + " / " + Buildings[j]);
        }
        lines.Add("[PASS] actual primary/support query matrix: 5 normal, 20 cross rejected; five singleton candidate lists; co-located distinct primary pairs=" + sharedPairs);

        var uiHost = new GameObject("WimAgeFacilityUiHost", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        uiHost.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        scope.Container.Resolve<ICharacterSurgeryWindowService>().Open(patient, uiHost.transform);
        var view = uiHost.GetComponentInChildren<CharacterSurgeryWindowView>();
        Require(view != null, "Actual surgery view was not created.");
        for (int i = 0; i < Procedures.Length; i++)
        {
            catalog.TryGet(Procedures[i], out var procedure);
            for (int attempts = 0; Text(view, "Procedure") != procedure.DisplayName && attempts < catalog.Procedures.Count; attempts++)
                Click(view.transform.Find("SurgeryPanel/ProcedureRow/Next"));
            Require(Text(view, "Procedure") == procedure.DisplayName, "Actual UI omitted " + Procedures[i]);
            Require(Text(view, "Target") == "-" && Text(view, "Part") == "-",
                "Whole-character UI invents a target/part requirement for " + Procedures[i]);
            string expected = Facility(i).BuildingData.objectName;
            Require(Text(view, "Facility") == expected, "Actual UI selected wrong primary: " + Text(view, "Facility"));
            Click(view.transform.Find("SurgeryPanel/FacilityRow/Next"));
            Require(Text(view, "Facility") == expected, "UI cycles into a cross-facility candidate.");
            if (i == 2)
            {
                Click(view.transform.Find("SurgeryPanel/Commands/Schedule"));
                var uiOrder = scope.Container.Resolve<ISurgeryPersistence>().Capture().orders.SingleOrDefault(x =>
                    x.IsActive && x.subject.subjectId == patientId);
                Require(uiOrder != null && uiOrder.procedureId == Procedures[2] && uiOrder.facilityId == facilityIds[2]
                    && string.IsNullOrEmpty(uiOrder.targetNodeId) && string.IsNullOrEmpty(uiOrder.selectedPartInstanceId),
                    "Actual UI schedule did not publish the correct whole-character order: "
                    + view.transform.Find("SurgeryPanel/Details").GetComponent<TMP_Text>().text);
                Click(view.transform.Find("SurgeryPanel/Commands/CancelOrder"));
                Require(!scope.Container.Resolve<ISurgeryPersistence>().Capture().orders.Single(x => x.orderId == uiOrder.orderId).IsActive,
                    "Actual UI cancel did not close its order.");
            }
        }
        Click(view.transform.Find("SurgeryPanel/Commands/Close"));
        lines.Add("[PASS] actual surgery view pointer selector: all five procedures expose only their authored primary; rune UI schedule/cancel publishes node-less, part-less order");

        var surgery = scope.Container.Resolve<ISurgeryCommandService>();
        var age = scope.Container.Resolve<IAgeTreatmentCommand>();
        var subject = new SurgicalSubjectRef {
            kind = SurgicalSubjectKind.Character, subjectId = patientId,
            displayName = patient.Identity.DisplayName, speciesId = patient.Identity.SpeciesTag,
            willing = true, automaticEmergencyDefault = false };
        string before = SurgeryAndPhysical();
        Require(!surgery.TrySchedule(subject, Procedures[2], "", "", "", facilityIds[0], out _, out var directFailure)
            && directFailure.Code == FailureCode.SurgeryFacilityUnavailable && SurgeryAndPhysical() == before,
            "Direct Schedule bypass mutated state or failed at another boundary: " + directFailure);
        Require(!age.TryCreateOrder(new AgeTreatmentOrderRequest(CharacterPersistentIdentity.Require(patient),
                AgeTreatmentKind.RuneHibernation, "", facilityIds[0]), out _, out var ageFailure)
            && ageFailure.Code == FailureCode.SurgeryFacilityUnavailable && SurgeryAndPhysical() == before,
            "Dedicated age command disagrees with common facility gate: " + ageFailure);
        Require(age.TryCreateOrder(new AgeTreatmentOrderRequest(CharacterPersistentIdentity.Require(patient),
                AgeTreatmentKind.RuneHibernation, "", facilityIds[2]), out var order, out var accepted)
            && order != null && order.IsActive && order.facilityId == facilityIds[2]
            && string.IsNullOrEmpty(order.targetNodeId) && string.IsNullOrEmpty(order.selectedPartInstanceId),
            "Actual whole-character age command incorrectly needs a node/part or failed: " + accepted);
        string orderId = order.orderId;
        lines.Add("[PASS] direct and dedicated wrong-primary commands reject before mutation; actual rune age command schedules without fake node/part");

        var valid = saves.FromJson(saves.ToJson(saves.Capture()));
        var invalidReturn = saves.FromJson(saves.ToJson(valid));
        Require(DungeonSaveSectionPayload.TryRead<DungeonSurgerySaveData>(invalidReturn, SurgerySaveSection.Id, out var returnPayload),
            "Captured whole world has no surgery payload.");
        var badReturn = returnPayload.orders.Single(x => x.orderId == orderId);
        Require(!badReturn.patientAdmitted && badReturn.subjectAiWasPaused,
            "Actual pending order must retain preexisting pause without admission.");
        badReturn.patientReturnRequested = true;
        DungeonSaveSectionPayload.Write(invalidReturn, SurgerySaveSection.Id, DungeonSurgerySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState, returnPayload);
        invalidReturn.manifest = DungeonSaveManifest.Capture(invalidReturn.sections);
        before = SurgeryAndPhysical();
        Require(!saves.TryRestore(invalidReturn, out var returnRejected) && !returnRejected.Success
            && returnRejected.Errors.Any(x => x.Contains(orderId) && x.Contains("admitted-patient state without admission"))
            && SurgeryAndPhysical() == before && Patient().IsAiPaused(),
            "Actual return-without-admission must remain rejected and atomic: " + string.Join(" | ", returnRejected.Errors));
        var invalid = saves.FromJson(saves.ToJson(valid));
        Require(DungeonSaveSectionPayload.TryRead<DungeonSurgerySaveData>(invalid, SurgerySaveSection.Id, out var badSurgery),
            "Captured whole world has no surgery payload.");
        var badOrder = badSurgery.orders.Single(x => x.orderId == orderId);
        badOrder.facilityId = facilityIds[0];
        // Keep the unrelated integrity checksum valid so this mutation reaches
        // the new semantic primary-facility gate, not the existing hash guard.
        badOrder.materialCapacityFingerprint = SurgeryMaterialCapacityFingerprint.Create(badOrder);
        DungeonSaveSectionPayload.Write(invalid, SurgerySaveSection.Id, DungeonSurgerySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState, badSurgery);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        before = SurgeryAndPhysical();
        Require(!saves.TryRestore(invalid, out var rejected) && !rejected.Success
            && rejected.Errors.Any(x => x.Contains(orderId) && x.Contains("facility is unavailable"))
            && SurgeryAndPhysical() == before && Patient().IsAiPaused(),
            "Wrong-primary active whole restore must fail at facility gate without live mutation: " + string.Join(" | ", rejected.Errors));
        Require(saves.TryRestore(valid, out var restored) && restored.Success,
            "Valid current whole-world age order restore failed: " + string.Join(" | ", restored.Errors));
        Pause();
        var restoredOrder = scope.Container.Resolve<ISurgeryPersistence>().Capture().orders.Single(x => x.orderId == orderId);
        Require(restoredOrder.IsActive && restoredOrder.facilityId == facilityIds[2]
            && string.IsNullOrEmpty(restoredOrder.targetNodeId) && string.IsNullOrEmpty(restoredOrder.selectedPartInstanceId),
            "Valid full restore changed whole-character order identity/primary.");
        Require(Patient().IsAiPaused() && restoredOrder.subjectAiWasPaused,
            "Valid full restore lost the patient's preexisting pause state.");
        Require(surgery.TryCancel(orderId, out var cancelled), "Restored order cancellation failed: " + cancelled);
        Require(!scope.Container.Resolve<ISurgeryPersistence>().Capture().orders.Single(x => x.orderId == orderId).IsActive,
            "Restored order did not terminate normally.");
        Require(Patient().IsAiPaused(), "Cancel cleared preexisting patient pause it did not own.");
        lines.Add("[PASS] wrong-primary incoming active whole-world JSON atomically rejected; valid whole restore preserves node-less age order; normal cancel closes");
        lines.Add("[PASS] preexisting AI pause survives actual pending/terminal order and whole restore/cancel; return-without-admission stays atomically rejected");
        Destroy(uiHost);
    }

    private string SurgeryAndPhysical()
    {
        // Ignore top-level wall-clock savedAtUtc; compare authority payloads only.
        var save = saves.Capture();
        return string.Join("\n", save.sections.Where(x => x.sectionId == SurgerySaveSection.Id
            || x.sectionId == PhysicalItemsSaveSection.Id || x.sectionId == CharacterBodyHealthSaveSection.Id)
            .OrderBy(x => x.sectionId, StringComparer.Ordinal).Select(x => x.sectionId + ":" + x.payloadJson));
    }

    private BuildableObject Place(Grid grid, CharacterActor actor, BuildingSO definition)
    {
        Require(scope.Container.Resolve<IWorldDropZoneQuery>().TryGetDeliveryDropoff(out var origin), "Missing dropoff.");
        var traversal = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(actor));
        var access = scope.Container.Resolve<IGridTraversalAccessQuery>();
        var costs = scope.Container.Resolve<IGridTraversalCostPolicy>();
        Vector2Int? anchor = null;
        foreach (var room in scope.Container.Resolve<IRoomLayoutCache>().GetLayout(grid).Rooms
            .Where(x => x.IsUsable).OrderBy(x => x.Bounds.yMin).ThenBy(x => x.Bounds.xMin))
        {
            anchor = room.Cells.OrderBy(x => x.x).ThenBy(x => x.y).Where(p =>
                definition.GetGridPosList(p).All(c => room.ContainsCell(c) && grid.GetGridCell(c) is GridCell cell
                    && cell.CanBuildInArea(definition) && cell.CanOccupy(definition.Placement.Layer))
                && grid.IsWalkable(p) && grid.SearchPathTo(origin, p,
                    c => access.CanTraverse(grid, c, traversal, out _), costs, traversal).GetMoveCostTo(p) != int.MaxValue)
                .Select(p => (Vector2Int?)p).FirstOrDefault();
            if (anchor.HasValue) break;
        }
        Require(anchor.HasValue, "No reachable legal authored footprint for " + definition.id);
        var value = scope.Container.Resolve<IGridBuildingObjectFactory>().Create(grid, definition, anchor.Value);
        Require(value != null, "Actual building factory rejected " + definition.id);
        foreach (var component in value.GetComponentsInChildren<MonoBehaviour>(true)) scope.Container.Inject(component);
        value.SetGrid(grid); value.Initialization(definition, anchor.Value);
        Require(grid.RegisterOccupant(value, definition.Placement.Layer, definition.GetGridPosList(anchor.Value),
            definition.Placement.IsMovement) && scope.Container.Resolve<IBuildingWorldQuery>().Buildings.Contains(value),
            "Building is not registered in actual grid/world.");
        return value;
    }

    private BuildableObject Facility(int i) => Facility(facilityIds[i]);
    private CharacterActor Patient() => world.Characters.Single(x => x != null && x.Identity.PersistentId == patientId);
    private BuildableObject Facility(string id) => scope.Container.Resolve<IBuildingWorldQuery>().Buildings
        .Single(x => x != null && x.RequirePersistentInstanceId().Value == id);
    private static string Text(CharacterSurgeryWindowView view, string row) =>
        view.transform.Find("SurgeryPanel/" + row + "Row/Value").GetComponent<TMP_Text>().text;
    private static void Click(Transform target)
    {
        Require(target != null && target.GetComponent<Button>()?.IsInteractable() == true
            && PlayModeVerificationFrameWait.DispatchPointerClick(target.gameObject, Vector2.zero), "Actual UI pointer click failed.");
    }
    private void Pause()
    {
        var game = FindFirstObjectByType<GameManager>(); if (game != null) game.isPause = true;
        if (scope?.Container != null) scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
    }
    private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }
}
#endif
