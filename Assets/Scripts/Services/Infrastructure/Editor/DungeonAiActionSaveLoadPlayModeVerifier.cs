using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>
/// Proves the V24 AI restore contract against the real gameplay container.
/// Transient action/coroutine/path ownership is deliberately not serialized;
/// persistent domain state is restored and the replacement actor replans.
/// </summary>
public static class DungeonAiActionSaveLoadPlayModeVerifier
{
    public const string ReportPath =
            "Artifacts/QA/ai-mid-action-save-load-playmode.txt";
    private const string PendingFlagPath =
        "Temp/ai-mid-action-save-load-playmode.flag";

    [MenuItem("DungeonStory/Debug/QA/Run AI Mid-Action Save-Load Verification")]
    public static void RunFromMenu() => RequestRun();

    public static void RequestRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner();
            return;
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingFlagPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    public static DungeonAiActionSaveLoadPlayModeRunner AttachRunner(GameObject runner)
    {
        if (runner == null) throw new ArgumentNullException(nameof(runner));
        DungeonAiActionSaveLoadPlayModeRunner component =
            runner.AddComponent<DungeonAiActionSaveLoadPlayModeRunner>();
        if (component == null)
        {
            throw new InvalidOperationException(
                "Unity did not attach DungeonAiActionSaveLoadPlayModeRunner.");
        }
        return component;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPendingRun()
    {
        if (!File.Exists(PendingFlagPath)) return;
        File.Delete(PendingFlagPath);
        StartRunner();
    }

    private static void StartRunner()
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                DungeonAiActionSaveLoadPlayModeRunner>() != null)
        {
            Debug.LogWarning("AI mid-action save/load verification is already running.");
            return;
        }

        GameObject runner = new("AI Mid-Action Save-Load Verification");
        runner.SetActive(false);
        DungeonAiActionSaveLoadPlayModeRunner component = AttachRunner(runner);
        runner.SetActive(true);
        component.BeginVerification();
    }
}

public sealed class DungeonAiActionSaveLoadPlayModeRunner : MonoBehaviour
{
    private const string SlotId = "qa_ai_mid_action";
    private readonly List<string> evidence = new();
    private readonly List<string> failures = new();
    private readonly List<string> capturedErrors = new();
    private float originalTimeScale;
    private IDungeonGameSaveSlotService slots;

    private bool verificationStarted;

    private void Start() => BeginVerification();

    public void BeginVerification()
    {
        if (verificationStarted) return;
        verificationStarted = true;
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(DungeonAiActionSaveLoadPlayModeVerifier.ReportPath,
            "# AI mid-action save/load PlayMode verification\nresult=RUNNING\n");
        StartCoroutine(RunVerification());
    }

    private IEnumerator RunVerification()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory("Artifacts/QA");
        originalTimeScale = Time.timeScale;
        Application.logMessageReceived += CaptureLog;
        // C# iterator blocks cannot yield from a try block with catch. The
        // coroutine wrapper below owns exception capture and always reaches
        // this deterministic cleanup epilogue.
        yield return RunGuarded();
        Application.logMessageReceived -= CaptureLog;
        Time.timeScale = originalTimeScale;
        slots?.Delete(SlotId);
        WriteReport();
        EditorApplication.isPlaying = false;
    }

    private IEnumerator RunGuarded()
    {
        IEnumerator routine = Run();
        while (true)
        {
            object current;
            try
            {
                if (!routine.MoveNext())
                {
                    yield break;
                }
                current = routine.Current;
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
                yield break;
            }
            yield return current;
        }
    }

    private IEnumerator Run()
    {
        DungeonRuntimeLifetimeScope scope = FindScope();
        float scopeDeadline = Time.realtimeSinceStartup + 10f;
        while (scope?.Container == null && Time.realtimeSinceStartup < scopeDeadline)
        {
            yield return null;
            scope = FindScope();
        }
        Check(scope?.Container != null, "runtime container resolved");
        if (scope?.Container == null)
        {
            yield break;
        }

        CharacterActor[] party = FindPersistentParty();
        if (party.Length == 0)
        {
            string result = StartPartyPreparationPlayModeVerifier
                .RunFastCommitForDebug();
            evidence.Add("startParty=" + OneLine(result));
            yield return null;
            party = FindPersistentParty();
        }

        float partyDeadline = Time.realtimeSinceStartup + 10f;
        while (party.Length == 0 && Time.realtimeSinceStartup < partyDeadline)
        {
            yield return null;
            party = FindPersistentParty();
        }
        Check(party.Length > 0, $"persistent party exists; count={party.Length}");
        if (party.Length == 0)
        {
            yield break;
        }

        // A start-party commit can replace the scene scope. Resolve every
        // authority again from the scope that owns the published party.
        scope = FindScopeFor(party[0]);
        Check(scope?.Container != null, "published party runtime scope resolved");
        if (scope?.Container == null)
        {
            yield break;
        }

        slots = scope.Container.Resolve<IDungeonGameSaveSlotService>();
        ICharacterWorldSaveService characterSaves =
            scope.Container.Resolve<ICharacterWorldSaveService>();
        IGridSystemProvider grids = scope.Container.Resolve<IGridSystemProvider>();
        Check(grids.TryGetGrid(out Grid grid) && grid != null,
            "authoritative grid resolved");
        if (grid == null)
        {
            yield break;
        }

        CharacterActor original = party
            .Where(actor => actor != null
                && actor.gameObject.activeInHierarchy
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
            .OrderBy(actor => actor.IsOwner ? 0 : 1)
            .ThenBy(actor => actor.GetInstanceID())
            .FirstOrDefault();
        Check(original != null, "active persistent actor selected");
        AbilityMove originalMove = original?.GetAbility<AbilityMove>();
        Check(originalMove != null, "selected actor has movement executor");
        if (original == null || originalMove == null)
        {
            yield break;
        }

        string persistentId = characterSaves.GetOrAssignPersistentId(original);
        int originalInstanceId = original.GetInstanceID();
        Vector2Int start = grid.GetXY(original.transform.position);
        Check(TryStartLongSystemMove(originalMove, grid, start, out Vector2Int destination,
                out string moveMessage),
            "live system movement started; " + moveMessage);
        if (!originalMove.IsSystemMoveInProgress)
        {
            yield break;
        }

        Time.timeScale = Mathf.Min(originalTimeScale <= 0f ? 1f : originalTimeScale, .1f);
        yield return null;
        yield return null;
        Check(originalMove.IsSystemMoveInProgress,
            "movement is still live at the save boundary");

        Vector2Int savedPosition = grid.GetXY(original.transform.position);
        string path = slots.Save(SlotId, prettyPrint: true);
        Check(File.Exists(path), "mid-action slot file was written");
        Check(originalMove.IsSystemMoveInProgress,
            "save capture did not mutate the live movement owner");
        evidence.Add(
            $"saved actor={persistentId}; instance={originalInstanceId}; "
            + $"start={start}; cell={savedPosition}; destination={destination}; path={path}");

        bool loaded = slots.TryLoad(SlotId, out DungeonGameRestoreReport report);
        Check(loaded && report.Success,
            "mid-action slot restored; errors=" + string.Join(" | ", report.Errors));
        if (!loaded || !report.Success)
        {
            yield break;
        }

        Check(characterSaves.TryGetRestoredActor(persistentId, out CharacterActor restored)
                && restored != null,
            "restored actor is addressable by the original persistent id");
        if (restored == null)
        {
            yield break;
        }

        AbilityMove restoredMove = restored.GetAbility<AbilityMove>();
        IGridSystemProvider restoredGrids = FindScopeFor(restored).Container
            .Resolve<IGridSystemProvider>();
        Check(restoredGrids.TryGetGrid(out Grid restoredGrid) && restoredGrid != null,
            "restored authoritative grid resolved");
        if (restoredGrid == null)
        {
            yield break;
        }
        Vector2Int restoredPosition = restoredGrid.GetXY(restored.transform.position);
        Check(restored.GetInstanceID() != originalInstanceId,
            $"old actor instance retired; old={originalInstanceId}; new={restored.GetInstanceID()}");
        Check(restoredPosition == savedPosition,
            $"restored position equals captured authoritative cell; {savedPosition}->{restoredPosition}");
        Check(restoredMove != null && !restoredMove.IsSystemMoveInProgress,
            "transient movement coroutine was not serialized or rebound");
        Check(restored.Brain != null
                && !restored.Brain.IsExternallyDrivenActionActive,
            "transient external AI intent was not serialized or rebound");
        ICharacterLifetimeQuery restoredLifetime = FindScopeFor(restored).Container
            .Resolve<ICharacterLifetimeQuery>();
        float retirementDeadline = Time.realtimeSinceStartup + 2f;
        while (restoredLifetime.AllCharacters.Any(actor => actor != null
                    && actor.GetInstanceID() == originalInstanceId)
            && Time.realtimeSinceStartup < retirementDeadline)
        {
            // Unity finalizes Destroy and invokes OnDestroy at the frame
            // boundary. The restore transaction is synchronous, but lifetime
            // registry retirement is intentionally tied to that callback.
            yield return null;
        }
        Check(!restoredLifetime.AllCharacters.Any(actor => actor != null
                && actor.GetInstanceID() == originalInstanceId),
            "retired actor is absent from the lifetime registry");

        Type saveType = typeof(DungeonCharacterSaveData);
        string[] forbiddenTransientFields =
        {
            "bestAction", "activeAction", "actionEpoch", "movementPath",
            "path", "activeWorkRun", "coroutine"
        };
        string[] serializedFields = saveType
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(field => field.Name)
            .ToArray();
        Check(!serializedFields.Any(field => forbiddenTransientFields.Any(forbidden =>
                    field.Contains(forbidden, StringComparison.OrdinalIgnoreCase))),
            "character save contract excludes transient action/path/coroutine ownership");

        DungeonRuntimeLifetimeScope restoredScope = FindScopeFor(restored);
        IWorldItemStackRuntime restoredItems =
            restoredScope.Container.Resolve<IWorldItemStackRuntime>();
        ICharacterNeedBalanceRuntime restoredNeedBalance =
            restoredScope.Container.Resolve<ICharacterNeedBalanceRuntime>();
        AIAction[] restoredOriginalActions = restored.Brain.availableActions;
        AIAction drinkAction = restoredOriginalActions?.FirstOrDefault(action =>
            action?.actionset is AIDrink);
        CharacterNeedResponseProfile thirstResponse = restoredNeedBalance
            .GetResponse(CharacterCondition.THIRST);
        bool validRoutineBand = thirstResponse.routineStart
            > thirstResponse.emergencyStart + 1f;
        Check(restoredItems != null && restoredNeedBalance != null
                && drinkAction != null && validRoutineBand,
            "post-restore routine drink fixture resolved; "
            + $"items={restoredItems != null}; drink={drinkAction != null}; "
            + $"emergency={thirstResponse.emergencyStart:0.##}; "
            + $"routine={thirstResponse.routineStart:0.##}");
        if (restoredItems == null || restoredNeedBalance == null
            || drinkAction == null || !validRoutineBand)
        {
            yield break;
        }

        foreach (CharacterCondition condition in restored.Stats.StatSnapshot.Keys.ToArray())
        {
            SetNeed(restored, condition, 100f);
        }
        float thirstBeforeReplan = Mathf.Lerp(
            thirstResponse.emergencyStart,
            thirstResponse.routineStart,
            .5f);
        SetNeed(restored, CharacterCondition.THIRST, thirstBeforeReplan);
        int waterBeforeReplan = CountWorldItem(
            restoredItems,
            "resource:clean-water");
        Check(waterBeforeReplan > 0,
            $"post-restore physical drink supply exists; water={waterBeforeReplan}");
        if (waterBeforeReplan <= 0)
        {
            yield break;
        }

        long startsBefore = restored.Brain.RuntimeActionStartCount;
        long gameplayProgressBefore = restored.Brain.GameplayProgressRevision;
        CharacterAiRuntimeGateSnapshot gateBefore =
            restored.Brain.CaptureRuntimeGateSnapshot();
        restored.Brain.StopCurrentActionForReplan(
            "AI save/load verifier prepares deterministic routine drink.");
        restored.Brain.availableActions = new[] { drinkAction };
        restored.Brain.PreferActionOnNextDecision<AIDrink>(180f);
        restored.Brain.RequestImmediateDecision(
            "AI save/load verifier requests urgent post-restore self-care.");
        Time.timeScale = Mathf.Max(20f, originalTimeScale);
        bool sawPostRestoreActionOwner = false;
        bool sawPostRestoreExternalOwner = false;
        bool sawDrinkSelection = false;
        float replanDeadline = Time.realtimeSinceStartup + 12f;
        while (restored != null
            && restored.Brain != null
            && (restored.Brain.CaptureRuntimeGateSnapshot().ActionTerminals
                    <= gateBefore.ActionTerminals
                || CountWorldItem(restoredItems, "resource:clean-water")
                    >= waterBeforeReplan
                || GetNeed(restored, CharacterCondition.THIRST)
                    <= thirstBeforeReplan)
            && Time.realtimeSinceStartup < replanDeadline)
        {
            sawPostRestoreActionOwner |=
                restored.Brain.RuntimeActionStartCount > startsBefore;
            sawPostRestoreExternalOwner |=
                restored.Brain.IsExternallyDrivenActionActive;
            sawDrinkSelection |= restored.Brain.bestAction?.actionset is AIDrink;
            yield return null;
        }
        if (restored?.Brain != null)
        {
            sawPostRestoreActionOwner |=
                restored.Brain.RuntimeActionStartCount > startsBefore;
            sawPostRestoreExternalOwner |=
                restored.Brain.IsExternallyDrivenActionActive;
            sawDrinkSelection |= restored.Brain.bestAction?.actionset is AIDrink;
        }

        Check(restored != null && restored.Brain != null,
            "replacement actor remains valid after post-restore scheduler ticks");
        if (restored?.Brain != null)
        {
            CharacterAiRuntimeGateSnapshot gate = restored.Brain
                .CaptureRuntimeDiagnostics().Gate;
            long actionStartsDelta =
                restored.Brain.RuntimeActionStartCount - startsBefore;
            long gameplayProgressDelta =
                restored.Brain.GameplayProgressRevision - gameplayProgressBefore;
            float thirstAfterReplan = GetNeed(
                restored,
                CharacterCondition.THIRST);
            int waterAfterReplan = CountWorldItem(
                restoredItems,
                "resource:clean-water");
            Check(sawPostRestoreActionOwner && sawDrinkSelection
                    && !sawPostRestoreExternalOwner,
                $"replacement actor acquired one fresh execution owner; "
                + $"actionOwner={sawPostRestoreActionOwner}; "
                + $"externalOwner={sawPostRestoreExternalOwner}; "
                + $"drinkSelected={sawDrinkSelection}; "
                + $"actionDelta={actionStartsDelta}; "
                + $"action={restored.Brain.CurrentActionDebugLabel}; "
                + $"phase={restored.Brain.CurrentActionPhase}");
            Check(gameplayProgressDelta > 0
                    && thirstAfterReplan > thirstBeforeReplan
                    && waterAfterReplan == waterBeforeReplan - 1,
                $"replacement actor produced typed gameplay progress; "
                + $"delta={gameplayProgressDelta}; "
                + $"thirst={thirstBeforeReplan:0.##}->{thirstAfterReplan:0.##}; "
                + $"water={waterBeforeReplan}->{waterAfterReplan}");
            Check(gate.InvariantAnomalies == 0,
                $"post-restore AI invariants remain clean; anomalies={gate.InvariantAnomalies}");
            Check(!restored.Brain.HasRunningAction
                    || gate.LiveActions == 1,
                $"running action has exactly one live owner; running={restored.Brain.HasRunningAction}; live={gate.LiveActions}");
            evidence.Add(
                $"restored actor={persistentId}; instance={restored.GetInstanceID()}; "
                + $"actionStartsDelta={actionStartsDelta}; "
                + $"gameplayProgressDelta={gameplayProgressDelta}; "
                + $"gate={restored.Brain.CaptureRuntimeDiagnostics().FormatDeltaFrom(default)}");
        }

        restored.Brain.availableActions = restoredOriginalActions;

        yield return VerifyMidConstructionHaulSaveRestore(
            restored,
            persistentId);

        Check(capturedErrors.Count == 0,
            "no unexpected Error/Exception/Assert logs; " + string.Join(" | ", capturedErrors));
    }

    private static bool TryStartLongSystemMove(
        AbilityMove move,
        Grid grid,
        Vector2Int start,
        out Vector2Int destination,
        out string message)
    {
        destination = start;
        message = "no reachable distant cell";
        GridPathSearchResult reachable = grid.SearchPath(start);
        List<Vector2Int> candidates = grid.GetCells()
            .Where(cell => cell != null && grid.IsWalkable(cell.Position))
            .Select(cell => cell.Position)
            // Prefer the actor's current floor. Trying distant cells on other
            // disconnected floors first can consume the bounded path-search
            // budget before a valid same-floor candidate is evaluated.
            .Where(position => position != start
                && position.y == start.y
                && reachable.ContainsPosition(position))
            .OrderByDescending(position =>
                Mathf.Abs(position.x - start.x) + Mathf.Abs(position.y - start.y))
            .ThenBy(position => position.y)
            .ThenBy(position => position.x)
            .ToList();
        foreach (Vector2Int candidate in candidates)
        {
            if (move.TryStartSystemMove(
                    candidate,
                    DoorAccessOverrideKind.DirectCommand,
                    out message)
                && move.IsSystemMoveInProgress)
            {
                destination = candidate;
                return true;
            }
        }
        return false;
    }

    private IEnumerator VerifyMidConstructionHaulSaveRestore(
        CharacterActor actor,
        string persistentId)
    {
        DungeonRuntimeLifetimeScope scope = FindScopeFor(actor);
        IGridSystemProvider grids = scope?.Container?.Resolve<IGridSystemProvider>();
        IWorldItemStackRuntime items = scope?.Container?.Resolve<IWorldItemStackRuntime>();
        IWorkOrderRuntime orders = scope?.Container?.Resolve<IWorkOrderRuntime>();
        IDungeonGameSaveService gameSaves =
            scope?.Container?.Resolve<IDungeonGameSaveService>();
        IItemQuantityReservationService reservations =
            scope?.Container?.Resolve<IItemQuantityReservationService>();
        ICharacterWorldSaveService characterSaves =
            scope?.Container?.Resolve<ICharacterWorldSaveService>();
        ICharacterAiWorldRegistry world =
            scope?.Container?.Resolve<ICharacterAiWorldRegistry>();
        Check(scope?.Container != null && grids != null && items != null
                && orders != null && gameSaves != null && reservations != null
                && characterSaves != null && world != null
                && grids.TryGetGrid(out Grid grid) && grid != null,
            "HAUL_SAVE_FIXTURE_AUTHORITIES_READY");
        if (scope?.Container == null || grids == null || items == null
            || orders == null || gameSaves == null || reservations == null
            || characterSaves == null || world == null
            || !grids.TryGetGrid(out grid) || grid == null)
        {
            yield break;
        }

        CharacterActor[] actors = FindPersistentParty();
        foreach (CharacterActor candidate in actors)
        {
            candidate.SetAiPaused(true);
            candidate.Brain?.StopCurrentActionForReplan(
                "AI save/load verifier isolates construction haul.");
            candidate.GetAbility<AbilityMove>()?.CancelActiveMovement();
            candidate.GetComponent<AbilityHaul>()?.StopHauling(
                "AI save/load verifier isolates construction haul.");
        }
        yield return null;
        yield return null;

        BuildingSO warehouseDefinition = FindBuildingAsset(definition =>
            definition.unlocked
            && definition.GetStorageCapacity() > 0
            && definition.StoresAllCategories());
        BuildingSO constructionDefinition = FindBuildingAsset(definition =>
            definition != null
            && definition.unlocked
            && !definition.IsDoor
            && definition.GetConstructionMaterials().Count > 0
            && definition.width <= 2
            && definition.height <= 2);
        Check(warehouseDefinition != null && constructionDefinition != null,
            "HAUL_SAVE_AUTHORED_FIXTURE_DEFINITIONS_READY");
        if (warehouseDefinition == null || constructionDefinition == null)
        {
            yield break;
        }

        Vector2Int origin = actor.GetNowXY();
        GridPathSearchResult reachable = grid.SearchPath(origin);
        Vector2Int[] positions = reachable.GetReachablePositions()
            .Where(position => position.y == origin.y)
            .Where(position => CanHostBuilding(grid, warehouseDefinition, position)
                || CanHostBuilding(grid, constructionDefinition, position))
            .Distinct()
            .OrderBy(position => position.x)
            .ThenBy(position => position.y)
            .ToArray();
        bool hasFixturePositions = TryChooseSeparatedFixturePositions(
            grid,
            positions,
            warehouseDefinition,
            constructionDefinition,
            out Vector2Int warehousePosition,
            out Vector2Int sitePosition);
        Check(hasFixturePositions,
            "HAUL_SAVE_REACHABLE_FIXTURE_POSITIONS_READY");
        if (!hasFixturePositions)
        {
            yield break;
        }

        Facility warehouse = CreateInjectedFacility(
            scope,
            grid,
            warehouseDefinition,
            warehousePosition,
            "QA_SaveLoad_Haul_Warehouse");
        bool warehouseRegistered = warehouse != null
            && grid.RegisterOccupant(
                warehouse,
                warehouseDefinition.Placement.Layer,
                warehouseDefinition.GetGridPosList(warehousePosition),
                warehouseDefinition.Placement.IsMovement);
        Check(warehouseRegistered && warehouse?.Inventory != null,
            "HAUL_SAVE_WAREHOUSE_REGISTERED");
        if (!warehouseRegistered || warehouse?.Inventory == null)
        {
            yield break;
        }

        GameObject siteObject = new("QA_SaveLoad_Haul_ConstructionSite");
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        InjectGameObject(scope, siteObject);
        site.SetGrid(grid);
        site.Initialization(constructionDefinition, sitePosition);
        siteObject.transform.position = grid.GetWorldPos(sitePosition);
        bool siteRegistered = grid.RegisterOccupant(
            site,
            GridLayer.Construction,
            constructionDefinition.GetGridPosList(sitePosition),
            false);
        Check(siteRegistered, "HAUL_SAVE_CONSTRUCTION_SITE_REGISTERED");
        if (!siteRegistered)
        {
            yield break;
        }

        bool orderCreated = orders.TryCreateConstructionOrder(
            site,
            constructionDefinition,
            sitePosition,
            out string orderId,
            out string orderFailure);
        WorkOrderProgressState order = null;
        Check(orderCreated && orders.TryGetOrderFor(
                site,
                BuiltInWorkTypeIds.Construct,
                out order),
            "HAUL_SAVE_CONSTRUCTION_ORDER_CREATED; " + orderFailure);
        if (!orderCreated || order == null
            || order.ItemMaterialRequirements == null
            || order.ItemMaterialRequirements.Count == 0)
        {
            yield break;
        }
        site.ConfigureSite(orderId, () => true, () => { });
        bool constructionAuthorityPublished = string.Equals(
                site.WorkOrderId,
                orderId,
                StringComparison.Ordinal)
            && world.Buildings.Contains(site);
        Check(constructionAuthorityPublished,
            "HAUL_SAVE_CONSTRUCTION_AUTHORITY_PUBLISHED; "
            + $"siteOrder={site.WorkOrderId}; worldContains={world.Buildings.Contains(site)}");
        if (!constructionAuthorityPublished)
        {
            yield break;
        }

        KeyValuePair<string, int> material = order.ItemMaterialRequirements
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .First();
        string warehouseDestination =
            WarehouseStorageIdentity.RequireDestinationId(warehouse);
        bool materialSeeded = items.SpawnItemAt(
            material.Key,
            material.Value,
            warehousePosition,
            WorldItemStackState.Stored,
            warehouseDestination,
            out int spawned)
            && spawned == material.Value;
        orders.RefreshMaterialsReady(site);
        Check(materialSeeded && CountDestinationWorldQuantity(
                items,
                order.MaterialDestinationId,
                material.Key) == material.Value,
            "HAUL_SAVE_CONSTRUCTION_MATERIAL_ROUTED_EXACT; "
            + $"item={material.Key}; quantity={spawned}; destination={order.MaterialDestinationId}");
        if (!materialSeeded)
        {
            yield break;
        }

        WorldItemStackSnapshot[] routedMaterialStacks = items.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && string.Equals(
                    stack.ItemId,
                    material.Key,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    order.MaterialDestinationId,
                    StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> routedMaterialStackIds = routedMaterialStacks
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        bool exactHaulCandidateIsolated = routedMaterialStacks.Length > 0;
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                     .Where(stack => stack != null && stack.Quantity > 0)
                     .OrderBy(stack => stack.StackId, StringComparer.Ordinal))
        {
            if (routedMaterialStackIds.Contains(stack.StackId))
            {
                exactHaulCandidateIsolated &= items.PrioritizeHaul(stack.StackId);
                continue;
            }

            bool canCompeteForHaul = !stack.Forbidden
                && (stack.State == WorldItemStackState.Loose
                    || stack.State == WorldItemStackState.FacilityBuffer
                    || stack.State == WorldItemStackState.Stored
                        && stack.HasDestinationPosition);
            if (canCompeteForHaul)
            {
                exactHaulCandidateIsolated &=
                    items.SetForbidden(stack.StackId, true);
            }
        }
        Check(exactHaulCandidateIsolated,
            "HAUL_SAVE_EXACT_CONSTRUCTION_CANDIDATE_ISOLATED; "
            + $"targetStacks={string.Join(",", routedMaterialStackIds)}");
        if (!exactHaulCandidateIsolated)
        {
            yield break;
        }

        AIAction[] originalActions = actor.Brain.availableActions;
        AIAction haulAction = originalActions?.FirstOrDefault(value =>
            value?.actionset is AIHaul);
        AbilityHaul haul = AbilityHaul.Ensure(actor);
        AbilityWork work = actor.GetAbility<AbilityWork>();
        work?.WorkPriorities?.SetPriority(
            BuiltInWorkTypeIds.Haul,
            WorkPriorityLevel.Priority1);
        Check(haulAction != null && haul != null && work?.WorkPriorities != null
                && work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Haul)
                    != WorkPriorityLevel.Off,
            "HAUL_SAVE_PRODUCTION_AI_ACTION_READY");
        if (haulAction == null || haul == null || work?.WorkPriorities == null)
        {
            yield break;
        }

        SetNeutralNeeds(actor);
        actor.Brain.availableActions = new[] { haulAction };
        actor.Brain.PreferActionOnNextDecision<AIHaul>(180f);
        actor.SetAiPaused(false);
        actor.Brain.RequestImmediateDecision(
            "AI save/load verifier starts production construction haul.");
        Time.timeScale = Mathf.Max(12f, originalTimeScale);

        HaulDeliveryIntentSaveData savedIntent = null;
        float pickupDeadline = Time.realtimeSinceStartup + 30f;
        while (Time.realtimeSinceStartup < pickupDeadline)
        {
            savedIntent = haul.CaptureDeliveryIntentForSave();
            if (savedIntent?.HasCommittedPickup == true)
            {
                break;
            }
            yield return null;
        }
        Time.timeScale = 0f;
        int committedQuantity = GetIntentQuantity(savedIntent, material.Key);
        Check(savedIntent?.HasCommittedPickup == true
                && committedQuantity > 0,
            "HAUL_SAVE_MID_PICKUP_COMMITTED; "
            + DescribeIntent(savedIntent));
        Check(savedIntent != null
                && string.Equals(
                    savedIntent.destinationId,
                    order.MaterialDestinationId,
                    StringComparison.Ordinal),
            "HAUL_SAVE_MID_PICKUP_DESTINATION_EXACT; "
            + DescribeIntent(savedIntent));
        if (savedIntent?.HasCommittedPickup != true || committedQuantity <= 0)
        {
            yield break;
        }

        int conservedAtSave = CountConservedMaterial(
            items,
            orders,
            orderId,
            material.Key);
        DungeonCharacterSaveData savedActorPayload = characterSaves
            .Capture(grid)
            .actors
            .SingleOrDefault(value => string.Equals(
                value.persistentId,
                persistentId,
                StringComparison.Ordinal));
        Check(savedActorPayload?.haulDeliveryIntent?.HasCommittedPickup == true
                && string.Equals(
                    savedActorPayload.haulDeliveryIntent.destinationId,
                    order.MaterialDestinationId,
                    StringComparison.Ordinal)
                && GetIntentQuantity(
                    savedActorPayload.haulDeliveryIntent,
                    material.Key) == committedQuantity,
            "HAUL_SAVE_V18_CHARACTER_PAYLOAD_EXACT; "
            + DescribeIntent(savedActorPayload?.haulDeliveryIntent));
        HaulQuantitySnapshot quantityAtSave = CaptureHaulQuantitySnapshot(
            actor,
            items,
            orders,
            reservations,
            savedIntent,
            orderId,
            material.Key);
        string savePath = slots.Save(SlotId, prettyPrint: true);
        Check(File.Exists(savePath),
            "HAUL_SAVE_V18_SLOT_CAPTURED_AT_COMMITTED_PICKUP");
        if (!File.Exists(savePath))
        {
            yield break;
        }

        string canonicalSaveJson = File.ReadAllText(savePath);
        bool negativeRestoresRejected = VerifyTamperedHaulRestoresFailAtomically(
            gameSaves,
            canonicalSaveJson,
            actor,
            items,
            orders,
            reservations,
            characterSaves,
            persistentId,
            savedIntent,
            site,
            orderId,
            material.Key);
        if (!negativeRestoresRejected)
        {
            yield break;
        }

        int firstCompletionConserved = -1;
        for (int restoreAttempt = 1; restoreAttempt <= 2; restoreAttempt++)
        {
            int restoreInvocationFrame = Time.frameCount;
            bool loaded = slots.TryLoad(
                SlotId,
                out DungeonGameRestoreReport restoreReport);
            bool restoreReturnedWithoutFrameAdvance =
                Time.frameCount == restoreInvocationFrame;
            float restoredTimeScaleBeforeFreeze = Time.timeScale;
            Check(loaded && restoreReport.Success,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_SUCCEEDED; "
                + string.Join(" | ", restoreReport.Errors));
            if (!loaded || !restoreReport.Success
                || !characterSaves.TryGetRestoredActor(
                    persistentId,
                    out CharacterActor restoredActor)
                || restoredActor == null)
            {
                yield break;
            }
            restoredActor.SetAiPaused(true);
            Time.timeScale = 0f;

            DungeonRuntimeLifetimeScope restoredScope = FindScopeFor(restoredActor);
            IWorldItemStackRuntime restoredItems = restoredScope.Container
                .Resolve<IWorldItemStackRuntime>();
            IWorkOrderRuntime restoredOrders = restoredScope.Container
                .Resolve<IWorkOrderRuntime>();
            IItemQuantityReservationService restoredReservations =
                restoredScope.Container.Resolve<IItemQuantityReservationService>();
            AbilityHaul restoredHaul = AbilityHaul.Ensure(restoredActor);
            AbilityMove restoredMove = restoredActor.GetAbility<AbilityMove>();
            HaulDeliveryIntentSaveData restoredIntent =
                restoredHaul.CaptureDeliveryIntentForSave();
            int restoredCommitment = GetIntentQuantity(
                restoredIntent,
                material.Key);
            HaulQuantitySnapshot quantityBeforeWake =
                CaptureHaulQuantitySnapshot(
                    restoredActor,
                    restoredItems,
                    restoredOrders,
                    restoredReservations,
                    restoredIntent,
                    orderId,
                    material.Key);
            CharacterAiRuntimeGateSnapshot gateBeforeWake =
                restoredActor.Brain.CaptureRuntimeGateSnapshot();
            Check(restoredActor.IsAiPaused()
                    && restoreReturnedWithoutFrameAdvance
                    && Time.timeScale == 0f
                    && restoredHaul.HasBoundDeliveryIntent
                    && restoredIntent?.HasCommittedPickup == true,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_INTENT_BOUND_BEFORE_AI_WAKE; "
                + $"sameFrame={restoreReturnedWithoutFrameAdvance}; "
                + $"paused={restoredActor.IsAiPaused()}; "
                + $"restoredScale={restoredTimeScaleBeforeFreeze:0.###}; "
                + $"frozenScale={Time.timeScale:0.###}; "
                + DescribeIntent(restoredIntent));
            Check(restoredIntent != null
                    && string.Equals(
                        restoredIntent.destinationId,
                        savedIntent.destinationId,
                        StringComparison.Ordinal)
                    && restoredCommitment == committedQuantity,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_DESTINATION_QUANTITY_EXACT; "
                + DescribeIntent(restoredIntent));
            Check(!restoredHaul.IsHauling
                    && restoredHaul.RoutineHeartbeat == 0
                    && string.IsNullOrWhiteSpace(restoredHaul.ActivePathDebug)
                    && restoredMove != null
                    && !restoredMove.IsSystemMoveInProgress
                    && !restoredMove.HasActiveMovementRoutineForDiagnostics
                    && string.IsNullOrWhiteSpace(
                        restoredMove.ActiveMovementOperationOwnerForDiagnostics)
                    && !restoredActor.Brain.HasRunningAction
                    && gateBeforeWake.LiveActions == 0,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_INERT_BEFORE_AI_WAKE; "
                + $"hauling={restoredHaul.IsHauling}; "
                + $"heartbeat={restoredHaul.RoutineHeartbeat}; "
                + $"haulPath={restoredHaul.ActivePathDebug}; "
                + $"moveActive={restoredMove?.HasActiveMovementRoutineForDiagnostics}; "
                + $"moveOwner={restoredMove?.ActiveMovementOperationOwnerForDiagnostics}; "
                + $"brainRunning={restoredActor.Brain.HasRunningAction}; "
                + $"liveActions={gateBeforeWake.LiveActions}");
            Check(quantityBeforeWake.Equals(quantityAtSave),
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_PHYSICAL_QUANTITIES_UNCHANGED_BEFORE_AI_WAKE; "
                + $"expected={quantityAtSave}; actual={quantityBeforeWake}");

            ConstructionSite restoredSite = FindConstructionSite(
                sitePosition,
                constructionDefinition.id);
            Check(restoredSite != null,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_CONSTRUCTION_SITE_REBOUND");
            if (restoredSite == null)
            {
                yield break;
            }

            int committedBeforeRefresh = restoredItems
                .GetCommittedHaulDeliveryQuantity(
                    order.MaterialDestinationId,
                    material.Key);
            int routedBeforeRefresh = CountDestinationWorldQuantity(
                restoredItems,
                order.MaterialDestinationId,
                material.Key);
            restoredOrders.RefreshMaterialsReady(restoredSite);
            int committedAfterRefresh = restoredItems
                .GetCommittedHaulDeliveryQuantity(
                    order.MaterialDestinationId,
                    material.Key);
            int routedAfterRefresh = CountDestinationWorldQuantity(
                restoredItems,
                order.MaterialDestinationId,
                material.Key);
            Check(committedBeforeRefresh == committedQuantity
                    && committedAfterRefresh == committedQuantity
                    && routedAfterRefresh == routedBeforeRefresh,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_DUPLICATE_REQUEST_ZERO; "
                + $"committed={committedBeforeRefresh}->{committedAfterRefresh}; "
                + $"routed={routedBeforeRefresh}->{routedAfterRefresh}");

            AIAction restoredHaulAction = restoredActor.Brain.availableActions?
                .FirstOrDefault(value => value?.actionset is AIHaul);
            Check(restoredHaulAction != null,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_AI_HAUL_ACTION_READY");
            if (restoredHaulAction == null)
            {
                yield break;
            }
            foreach (CharacterActor candidate in FindPersistentParty())
            {
                candidate.SetAiPaused(true);
            }
            SetNeutralNeeds(restoredActor);
            Check(!restoredActor.Brain.HasRunningAction,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_NO_ACTION_BEFORE_AI_WAKE");
            restoredActor.Brain.availableActions = new[] { restoredHaulAction };
            restoredActor.Brain.PreferActionOnNextDecision<AIHaul>(180f);
            restoredActor.SetAiPaused(false);
            restoredActor.Brain.RequestImmediateDecision(
                "AI save/load verifier wakes rebound construction haul.");
            Time.timeScale = Mathf.Max(12f, originalTimeScale);

            bool sawBrainHaul = false;
            float deliveryDeadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deliveryDeadline
                && (restoredHaul.HasBoundDeliveryIntent
                    || restoredActor.Brain.HasRunningAction
                    || (CountDestinationWorldQuantity(
                            restoredItems,
                            order.MaterialDestinationId,
                            material.Key,
                            WorldItemStackState.FacilityBuffer) < committedQuantity
                        && GetDeliveredMaterialQuantity(
                            restoredOrders,
                            orderId,
                            material.Key) < committedQuantity)))
            {
                sawBrainHaul |=
                    restoredActor.Brain.bestAction?.actionset is AIHaul;
                yield return null;
            }
            sawBrainHaul |= restoredActor.Brain.bestAction?.actionset is AIHaul;
            restoredActor.SetAiPaused(true);
            CharacterAiRuntimeGateSnapshot gateAfterDelivery =
                restoredActor.Brain.CaptureRuntimeGateSnapshot();
            int bufferedAfterDelivery = CountDestinationWorldQuantity(
                restoredItems,
                order.MaterialDestinationId,
                material.Key,
                WorldItemStackState.FacilityBuffer);
            int deliveredAfterDelivery = GetDeliveredMaterialQuantity(
                restoredOrders,
                orderId,
                material.Key);
            Check(!restoredHaul.HasBoundDeliveryIntent
                    && (bufferedAfterDelivery >= committedQuantity
                        || deliveredAfterDelivery >= committedQuantity),
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_DELIVERY_COMPLETED; "
                + $"bound={restoredHaul.HasBoundDeliveryIntent}; "
                + $"buffer={bufferedAfterDelivery}; "
                + $"delivered={deliveredAfterDelivery}");
            Check(sawBrainHaul
                    && gateAfterDelivery.ActionStarts
                        == gateBeforeWake.ActionStarts + 1
                    && gateAfterDelivery.ActionTerminals
                        == gateBeforeWake.ActionTerminals + 1
                    && gateAfterDelivery.LiveActions == 0
                    && !restoredActor.Brain.HasRunningAction,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_BRAIN_AIHAUL_EXACT_ONCE; "
                + $"saw={sawBrainHaul}; "
                + $"starts={gateBeforeWake.ActionStarts}->{gateAfterDelivery.ActionStarts}; "
                + $"terminals={gateBeforeWake.ActionTerminals}->{gateAfterDelivery.ActionTerminals}; "
                + $"live={gateAfterDelivery.LiveActions}; "
                + $"running={restoredActor.Brain.HasRunningAction}");

            int conservedAfter = CountConservedMaterial(
                restoredItems,
                restoredOrders,
                orderId,
                material.Key);
            Check(conservedAfter == conservedAtSave,
                $"HAUL_SAVE_RESTORE_{restoreAttempt}_CONSERVATION_EXACT; "
                + $"expected={conservedAtSave}; actual={conservedAfter}");
            if (restoreAttempt == 1)
            {
                firstCompletionConserved = conservedAfter;
            }
            else
            {
                Check(conservedAfter == firstCompletionConserved,
                    "HAUL_SAVE_REPEATED_RESTORE_CONSERVATION_EXACT; "
                    + $"first={firstCompletionConserved}; second={conservedAfter}");
            }
            Time.timeScale = 0f;
        }
    }

    private enum HaulRestoreTamperKind
    {
        DestinationId,
        DestinationKind,
        DeliveryPosition,
        DropPosition,
        MissingFacilityBufferAuthority
    }

    private bool VerifyTamperedHaulRestoresFailAtomically(
        IDungeonGameSaveService gameSaves,
        string canonicalSaveJson,
        CharacterActor actor,
        IWorldItemStackRuntime items,
        IWorkOrderRuntime orders,
        IItemQuantityReservationService reservations,
        ICharacterWorldSaveService characterSaves,
        string persistentId,
        HaulDeliveryIntentSaveData savedIntent,
        ConstructionSite site,
        string orderId,
        string itemId)
    {
        foreach (HaulRestoreTamperKind kind in Enum.GetValues(
                     typeof(HaulRestoreTamperKind)))
        {
            string marker = ToTamperMarker(kind);
            string before = CaptureHaulAtomicFingerprint(
                actor,
                site,
                items,
                orders,
                reservations,
                savedIntent.operationId,
                orderId,
                itemId);
            bool lookupFoundBefore = characterSaves.TryGetRestoredActor(
                persistentId,
                out CharacterActor lookupActorBefore);
            bool prepared = TryCreateTamperedHaulSave(
                gameSaves,
                canonicalSaveJson,
                persistentId,
                orderId,
                kind,
                out DungeonGameSaveData tampered,
                out string tamperDetail);
            Check(prepared,
                $"HAUL_SAVE_NEGATIVE_{marker}_FIXTURE_READY; {tamperDetail}");
            if (!prepared)
            {
                return false;
            }

            bool accepted = gameSaves.TryRestore(
                tampered,
                out DungeonGameRestoreReport report);
            bool expectedRejection = !accepted
                && !report.Success
                && HasExpectedHaulTamperRejection(report, kind);
            Check(expectedRejection,
                $"HAUL_SAVE_NEGATIVE_{marker}_WHOLE_RESTORE_REJECTED; "
                + string.Join(" | ", report.Errors));
            if (!expectedRejection)
            {
                return false;
            }

            CharacterActor liveActor = FindPersistentParty()
                .SingleOrDefault(candidate => string.Equals(
                    candidate.Identity?.PersistentId,
                    persistentId,
                    StringComparison.Ordinal));
            ConstructionSite liveSite = site != null
                ? FindConstructionSite(site.centerPos, site.BuildingData.id)
                : null;
            string after = CaptureHaulAtomicFingerprint(
                actor,
                site,
                items,
                orders,
                reservations,
                savedIntent.operationId,
                orderId,
                itemId);
            bool lookupFoundAfter = characterSaves.TryGetRestoredActor(
                persistentId,
                out CharacterActor lookupActorAfter);
            bool lookupUnchanged = lookupFoundAfter == lookupFoundBefore
                && ReferenceEquals(lookupActorAfter, lookupActorBefore);
            bool unchanged = ReferenceEquals(liveActor, actor)
                && ReferenceEquals(liveSite, site)
                && lookupUnchanged
                && string.Equals(before, after, StringComparison.Ordinal);
            Check(unchanged,
                $"HAUL_SAVE_NEGATIVE_{marker}_ATOMIC_ROLLBACK_UNCHANGED; "
                + $"actor={ReferenceEquals(liveActor, actor)}; "
                + $"site={ReferenceEquals(liveSite, site)}; "
                + $"lookup={lookupFoundBefore}->{lookupFoundAfter}:"
                + $"{lookupUnchanged}; "
                + $"before={before}; after={after}");
            if (!unchanged)
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasExpectedHaulTamperRejection(
        DungeonGameRestoreReport report,
        HaulRestoreTamperKind kind)
    {
        string errors = string.Join(" | ", report?.Errors
            ?? Array.Empty<string>());
        if (string.IsNullOrWhiteSpace(errors))
        {
            return false;
        }

        return kind switch
        {
            HaulRestoreTamperKind.DestinationId =>
                ContainsOrdinalIgnoreCase(errors, "Haul delivery")
                && (ContainsOrdinalIgnoreCase(errors, "lease")
                    || ContainsOrdinalIgnoreCase(errors, "destination")),
            HaulRestoreTamperKind.DestinationKind =>
                ContainsOrdinalIgnoreCase(errors, "Haul delivery")
                && (ContainsOrdinalIgnoreCase(errors, "lease")
                    || ContainsOrdinalIgnoreCase(errors, "destination")),
            HaulRestoreTamperKind.DeliveryPosition =>
                ContainsOrdinalIgnoreCase(
                    errors,
                    "haul-restore-destination-authority-mismatch")
                || ContainsOrdinalIgnoreCase(errors, "haul-destination"),
            HaulRestoreTamperKind.DropPosition =>
                ContainsOrdinalIgnoreCase(
                    errors,
                    "haul-restore-destination-authority-mismatch")
                || ContainsOrdinalIgnoreCase(errors, "haul-destination"),
            HaulRestoreTamperKind.MissingFacilityBufferAuthority =>
                ContainsOrdinalIgnoreCase(
                    errors,
                    "haul-destination-construction-order-missing-or-ambiguous")
                || (ContainsOrdinalIgnoreCase(errors, "Haul delivery")
                    && ContainsOrdinalIgnoreCase(errors, "destination"))
                || ((ContainsOrdinalIgnoreCase(errors, "work-order")
                        || ContainsOrdinalIgnoreCase(errors, "construction"))
                    && ContainsOrdinalIgnoreCase(errors, "missing")),
            _ => false
        };
    }

    private static bool ContainsOrdinalIgnoreCase(string source, string value) =>
        source?.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool TryCreateTamperedHaulSave(
        IDungeonGameSaveService gameSaves,
        string canonicalSaveJson,
        string persistentId,
        string orderId,
        HaulRestoreTamperKind kind,
        out DungeonGameSaveData tampered,
        out string detail)
    {
        tampered = gameSaves.FromJson(canonicalSaveJson);
        detail = string.Empty;
        DungeonCharacterWorldSaveData characters =
            DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(
                tampered,
                CharacterWorldSaveSection.Id);
        DungeonCharacterSaveData actor = characters.actors?
            .SingleOrDefault(value => string.Equals(
                value.persistentId,
                persistentId,
                StringComparison.Ordinal));
        HaulDeliveryIntentSaveData intent = actor?.haulDeliveryIntent;
        if (intent == null || !intent.HasCommittedPickup)
        {
            detail = "canonical committed intent missing";
            return false;
        }

        switch (kind)
        {
            case HaulRestoreTamperKind.DestinationId:
                intent.destinationId += ":tampered";
                detail = "destinationId=" + intent.destinationId;
                break;
            case HaulRestoreTamperKind.DestinationKind:
                intent.destinationKind = intent.destinationKind
                    == WorldItemHaulDestinationKind.FacilityBuffer
                        ? WorldItemHaulDestinationKind.Warehouse
                        : WorldItemHaulDestinationKind.FacilityBuffer;
                detail = "destinationKind=" + intent.destinationKind;
                break;
            case HaulRestoreTamperKind.DeliveryPosition:
                intent.deliveryGridX = IncrementWithoutOverflow(intent.deliveryGridX);
                detail = $"delivery=({intent.deliveryGridX},{intent.deliveryGridY})";
                break;
            case HaulRestoreTamperKind.DropPosition:
                intent.dropGridX = IncrementWithoutOverflow(intent.dropGridX);
                detail = $"drop=({intent.dropGridX},{intent.dropGridY})";
                break;
            case HaulRestoreTamperKind.MissingFacilityBufferAuthority:
                DungeonWorkOrderSaveData workOrders =
                    DungeonSaveSectionPayload.ReadOrNew<DungeonWorkOrderSaveData>(
                        tampered,
                        WorkOrdersSaveSection.Id);
                int removed = workOrders.orders?.RemoveAll(value => value != null
                    && string.Equals(
                        value.workOrderId,
                        orderId,
                        StringComparison.Ordinal)) ?? 0;
                if (removed != 1)
                {
                    detail = $"expected one work-order authority, removed={removed}";
                    return false;
                }
                DungeonSaveSectionPayload.Write(
                    tampered,
                    WorkOrdersSaveSection.Id,
                    DungeonWorkOrderSaveData.CurrentVersion,
                    DungeonSaveRestorePhase.RuntimeState,
                    workOrders);
                detail = "removedWorkOrder=" + orderId;
                break;
            default:
                detail = "unsupported tamper kind";
                return false;
        }

        DungeonSaveSectionPayload.Write(
            tampered,
            CharacterWorldSaveSection.Id,
            CharacterWorldSaveSection.CurrentVersion,
            DungeonSaveRestorePhase.Characters,
            characters);
        // Keep the envelope manifest canonical so rejection proves the haul
        // restore authority, not a stale checksum created by this fixture.
        tampered.manifest = DungeonSaveManifest.Capture(tampered.sections);
        return true;
    }

    private static int IncrementWithoutOverflow(int value) =>
        value == int.MaxValue ? value - 1 : value + 1;

    private static string ToTamperMarker(HaulRestoreTamperKind kind) => kind switch
    {
        HaulRestoreTamperKind.DestinationId => "DESTINATION_ID",
        HaulRestoreTamperKind.DestinationKind => "DESTINATION_KIND",
        HaulRestoreTamperKind.DeliveryPosition => "DELIVERY_POSITION",
        HaulRestoreTamperKind.DropPosition => "DROP_POSITION",
        HaulRestoreTamperKind.MissingFacilityBufferAuthority =>
            "MISSING_FACILITY_BUFFER_AUTHORITY",
        _ => "UNKNOWN"
    };

    private static string CaptureHaulAtomicFingerprint(
        CharacterActor actor,
        ConstructionSite site,
        IWorldItemStackRuntime items,
        IWorkOrderRuntime orders,
        IItemQuantityReservationService reservations,
        string operationId,
        string orderId,
        string itemId)
    {
        AbilityHaul haul = actor != null ? AbilityHaul.Ensure(actor) : null;
        HaulDeliveryIntentSaveData intent = null;
        string intentFailure = string.Empty;
        try
        {
            intent = haul?.CaptureDeliveryIntentForSave();
        }
        catch (Exception exception)
        {
            intentFailure = exception.GetType().Name + ":" + exception.Message;
        }

        reservations.TryGetLeasesByOwner(
            operationId,
            out IReadOnlyList<ItemQuantityLease> leases);
        string leaseState = string.Join(",", (leases
                ?? Array.Empty<ItemQuantityLease>())
            .Where(value => value != null)
            .OrderBy(value => value.leaseId, StringComparer.Ordinal)
            .Select(value =>
            {
                string sliceState = string.Join("/", (value.slices
                        ?? new List<ItemLeaseSlice>())
                    .Where(slice => slice != null)
                    .OrderBy(slice => slice.stackId, StringComparer.Ordinal)
                    .Select(slice => $"{slice.stackId}:{slice.quantity}:"
                        + slice.expectedStackSignature));
                return $"{value.leaseId}:{value.ownerOperationId}:"
                    + $"{value.purpose}:{value.aggregationCohortId}:"
                    + $"{value.remainingQuantity}:[{sliceState}]";
            }));
        string stackState = string.Join(",", items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => $"{value.StackId}:{value.State}:{value.Quantity}:"
                + $"{value.ReservedQuantity}:{value.Position}:"
                + $"{value.DestinationId}:{value.StackSignature}"));
        string carryState = string.Join(",", (actor?.CarryInventory?.Capture()?.items
                ?? new List<CharacterCarriedItemSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
            .Select(value => $"{value.carriedStackId}:{value.sourceStackId}:"
                + $"{value.ownerOperationId}:{value.itemId}:{value.quantity}"));
        WorkOrderSaveData order = orders.Capture().orders
            .SingleOrDefault(value => string.Equals(
                value.workOrderId,
                orderId,
                StringComparison.Ordinal));
        string orderMaterialState = order == null
            ? string.Empty
            : string.Join("/", (order.itemMaterials
                    ?? new List<WorkOrderItemMaterialSaveData>())
                .Where(value => value != null)
                .OrderBy(value => value.itemId, StringComparer.Ordinal)
                .Select(value => $"{value.itemId}:{value.required}:{value.delivered}"));
        string orderState = order == null
            ? "missing"
            : $"{order.workOrderId}:{order.status}:{order.completedWork}:"
                + $"{order.materialDestinationId}:[{orderMaterialState}]";
        return $"actor={actor?.GetInstanceID()}@{actor?.GetNowXY()}:"
            + $"{actor?.CurrentLifecycleState}:{actor?.IsAiPaused()};"
            + $"site={site?.GetInstanceID()}:{site?.centerPos}:{site?.gameObject.activeSelf};"
            + $"haulBound={haul?.HasBoundDeliveryIntent};"
            + $"intent={DescribeIntent(intent)}:{intentFailure};"
            + $"leases={leaseState};stacks={stackState};carry={carryState};"
            + $"order={orderState}";
    }

    private readonly struct HaulQuantitySnapshot : IEquatable<HaulQuantitySnapshot>
    {
        public HaulQuantitySnapshot(
            int world,
            int carried,
            int committed,
            int leased,
            int routed,
            int delivered,
            int conserved)
        {
            World = world;
            Carried = carried;
            Committed = committed;
            Leased = leased;
            Routed = routed;
            Delivered = delivered;
            Conserved = conserved;
        }

        public int World { get; }
        public int Carried { get; }
        public int Committed { get; }
        public int Leased { get; }
        public int Routed { get; }
        public int Delivered { get; }
        public int Conserved { get; }

        public bool Equals(HaulQuantitySnapshot other) =>
            World == other.World
            && Carried == other.Carried
            && Committed == other.Committed
            && Leased == other.Leased
            && Routed == other.Routed
            && Delivered == other.Delivered
            && Conserved == other.Conserved;

        public override bool Equals(object value) =>
            value is HaulQuantitySnapshot other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            World,
            Carried,
            Committed,
            Leased,
            Routed,
            Delivered,
            Conserved);

        public override string ToString() =>
            $"world={World},carry={Carried},committed={Committed},"
            + $"leased={Leased},routed={Routed},delivered={Delivered},"
            + $"conserved={Conserved}";
    }

    private static HaulQuantitySnapshot CaptureHaulQuantitySnapshot(
        CharacterActor actor,
        IWorldItemStackRuntime items,
        IWorkOrderRuntime orders,
        IItemQuantityReservationService reservations,
        HaulDeliveryIntentSaveData intent,
        string orderId,
        string itemId)
    {
        string operationId = intent?.operationId?.Trim() ?? string.Empty;
        reservations.TryGetLeasesByOwner(
            operationId,
            out IReadOnlyList<ItemQuantityLease> leases);
        int leased = (leases ?? Array.Empty<ItemQuantityLease>())
            .Where(value => value != null)
            .Sum(value => Mathf.Max(0, value.remainingQuantity));
        int carried = (actor?.CarryInventory?.Capture()?.items
                ?? new List<CharacterCarriedItemSaveData>())
            .Where(value => value != null
                && string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal)
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal))
            .Sum(value => Mathf.Max(0, value.quantity));
        string destinationId = intent?.destinationId?.Trim() ?? string.Empty;
        return new HaulQuantitySnapshot(
            CountWorldItem(items, itemId),
            carried,
            items.GetCommittedHaulDeliveryQuantity(destinationId, itemId),
            leased,
            CountDestinationWorldQuantity(items, destinationId, itemId),
            GetDeliveredMaterialQuantity(orders, orderId, itemId),
            CountConservedMaterial(items, orders, orderId, itemId));
    }

    private static DungeonRuntimeLifetimeScope FindScope() =>
        UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate?.Container != null);

    private static BuildingSO FindBuildingAsset(Func<BuildingSO, bool> predicate)
    {
        foreach (string guid in AssetDatabase.FindAssets(
                     "t:BuildingSO",
                     new[] { "Assets/Resources/SO/Building" }))
        {
            BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && predicate(definition))
            {
                return definition;
            }
        }
        return null;
    }

    private static bool CanHostBuilding(
        Grid grid,
        BuildingSO definition,
        Vector2Int position)
    {
        if (grid == null || definition == null)
        {
            return false;
        }
        foreach (Vector2Int occupied in definition.GetGridPosList(position))
        {
            GridCell cell = grid.GetGridCell(occupied);
            if (cell == null
                || !grid.IsWalkable(occupied)
                || cell.GetOccupant(GridLayer.Building) != null
                || cell.GetOccupant(GridLayer.Construction) != null
                || cell.GetOccupant(GridLayer.Character) != null
                || cell.GetOccupant(GridLayer.DownedCharacter) != null)
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryChooseSeparatedFixturePositions(
        Grid grid,
        IReadOnlyList<Vector2Int> candidates,
        BuildingSO warehouse,
        BuildingSO construction,
        out Vector2Int warehousePosition,
        out Vector2Int sitePosition)
    {
        warehousePosition = default;
        sitePosition = default;
        foreach (Vector2Int source in candidates)
        {
            if (!CanHostBuilding(grid, warehouse, source))
            {
                continue;
            }
            HashSet<Vector2Int> sourceFootprint = new(
                warehouse.GetGridPosList(source));
            foreach (Vector2Int destination in candidates
                         .OrderByDescending(value => Mathf.Abs(value.x - source.x)))
            {
                if (Mathf.Abs(destination.x - source.x) < 8
                    || !CanHostBuilding(grid, construction, destination)
                    || construction.GetGridPosList(destination)
                        .Any(sourceFootprint.Contains))
                {
                    continue;
                }
                warehousePosition = source;
                sitePosition = destination;
                return true;
            }
        }
        return false;
    }

    private static Facility CreateInjectedFacility(
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        BuildingSO definition,
        Vector2Int position,
        string objectName)
    {
        if (scope?.Container == null || grid == null || definition == null)
        {
            return null;
        }
        GameObject instance = new(objectName);
        Facility facility = instance.AddComponent<Facility>();
        InjectGameObject(scope, instance);
        facility.SetGrid(grid);
        facility.Initialization(definition, position);
        facility.SetRuntimeGridPosition(position);
        return facility;
    }

    private static void InjectGameObject(
        DungeonRuntimeLifetimeScope scope,
        GameObject target)
    {
        if (scope?.Container == null || target == null)
        {
            return;
        }
        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
    }

    private static ConstructionSite FindConstructionSite(
        Vector2Int position,
        int buildingId) => UnityEngine.Object
        .FindObjectsByType<ConstructionSite>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None)
        .FirstOrDefault(site => site != null
            && site.centerPos == position
            && site.BuildingData?.id == buildingId);

    private static int CountDestinationWorldQuantity(
        IWorldItemStackRuntime items,
        string destinationId,
        string itemId,
        WorldItemStackState? state = null) => items?.GetAllStacks()
        .Where(stack => stack != null
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal)
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
            && (!state.HasValue || stack.State == state.Value))
        .Sum(stack => stack.Quantity) ?? 0;

    private static int CountConservedMaterial(
        IWorldItemStackRuntime items,
        IWorkOrderRuntime orders,
        string orderId,
        string itemId)
    {
        int physical = items?.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity) ?? 0;
        WorkOrderSaveData order = orders?.Capture()?.orders?
            .FirstOrDefault(value => string.Equals(
                value.workOrderId,
                orderId,
                StringComparison.Ordinal));
        int delivered = order?.itemMaterials?
            .Where(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal))
            .Sum(value => Mathf.Max(0, value.delivered)) ?? 0;
        return physical + delivered;
    }

    private static int GetDeliveredMaterialQuantity(
        IWorkOrderRuntime orders,
        string orderId,
        string itemId)
    {
        WorkOrderSaveData order = orders?.Capture()?.orders?
            .FirstOrDefault(value => string.Equals(
                value.workOrderId,
                orderId,
                StringComparison.Ordinal));
        return order?.itemMaterials?
            .Where(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal))
            .Sum(value => Mathf.Max(0, value.delivered)) ?? 0;
    }

    private static int GetIntentQuantity(
        HaulDeliveryIntentSaveData intent,
        string itemId) => intent?.commitments?
        .Where(commitment => commitment != null
            && string.Equals(
                commitment.itemId,
                itemId,
                StringComparison.Ordinal))
        .Sum(commitment => Mathf.Max(0, commitment.quantity)) ?? 0;

    private static string DescribeIntent(HaulDeliveryIntentSaveData intent)
    {
        if (intent == null)
        {
            return "intent=<null>";
        }
        string itemSummary = string.Join(",",
            (intent.commitments ?? new List<HaulDeliveryItemCommitmentSaveData>())
            .Where(value => value != null)
            .Select(value => $"{value.itemId}:{value.quantity}:{value.carriedStackId}"));
        return $"operation={intent.operationId}; owner={intent.ownerCharacterId}; "
            + $"destination={intent.destinationKind}/{intent.destinationId}; "
            + $"delivery=({intent.deliveryGridX},{intent.deliveryGridY}); "
            + $"drop=({intent.dropGridX},{intent.dropGridY}); "
            + $"committed={intent.HasCommittedPickup}; "
            + $"items={itemSummary}";
    }

    private static void SetNeutralNeeds(CharacterActor actor)
    {
        if (actor?.Stats == null)
        {
            return;
        }
        foreach (CharacterCondition condition in actor.Stats.StatSnapshot.Keys.ToArray())
        {
            SetNeed(actor, condition, 100f);
        }
    }

    private static DungeonRuntimeLifetimeScope FindScopeFor(CharacterActor actor) =>
        UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate?.Container != null
                && candidate.gameObject.scene == actor.gameObject.scene)
        ?? FindScope();

    private static CharacterActor[] FindPersistentParty() =>
        CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null
                && actor.Identity != null
                && !string.IsNullOrWhiteSpace(actor.Identity.PersistentId)
                && !actor.IsDead
                && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned)
            .OrderBy(actor => actor.IsOwner ? 0 : 1)
            .ThenBy(actor => actor.Identity.PersistentId, StringComparer.Ordinal)
            .ToArray();

    private static void SetNeed(
        CharacterActor actor,
        CharacterCondition condition,
        float target)
    {
        if (actor?.Stats != null
            && actor.Stats.TryGetConditionValue(condition, out float current))
        {
            actor.ChangesStat(condition, target - current);
        }
    }

    private static float GetNeed(
        CharacterActor actor,
        CharacterCondition condition)
    {
        return actor?.Stats != null
            && actor.Stats.TryGetConditionValue(condition, out float value)
            ? value
            : float.NaN;
    }

    private static int CountWorldItem(
        IWorldItemStackRuntime items,
        string itemId) => items?.GetAllStacks()
        .Where(stack => stack != null
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
        .Sum(stack => stack.Quantity) ?? 0;

    private void Check(bool condition, string message)
    {
        (condition ? evidence : failures).Add((condition ? "PASS " : "FAIL ") + message);
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            capturedErrors.Add(type + ": " + OneLine(condition));
        }
    }

    private void WriteReport()
    {
        StringBuilder report = new(4096);
        report.AppendLine("# AI mid-action save/load PlayMode verification");
        report.AppendLine("policy=transient action/path/coroutine discarded; persistent domain intent restored; actor replans");
        report.AppendLine("haulDelivery=pickup-committed intent rebound before AI wake; duplicate request forbidden; repeated restore conserved");
        report.AppendLine("result=" + (failures.Count == 0 ? "PASS" : "FAIL"));
        report.AppendLine("failures=" + failures.Count);
        report.AppendLine();
        foreach (string line in evidence)
        {
            report.AppendLine(line);
        }
        foreach (string line in failures)
        {
            report.AppendLine(line);
        }
        File.WriteAllText(DungeonAiActionSaveLoadPlayModeVerifier.ReportPath,
            report.ToString());
        if (failures.Count == 0)
        {
            Debug.Log("AI_MID_ACTION_SAVE_LOAD=PASS; report="
                + DungeonAiActionSaveLoadPlayModeVerifier.ReportPath);
        }
        else
        {
            Debug.LogError("AI_MID_ACTION_SAVE_LOAD=FAIL; "
                + string.Join(" | ", failures));
        }
    }

    private static string OneLine(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('\r', ' ').Replace('\n', ' ');
}
