#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// Root-owned integration witness. Authored pen and domestic birth are controlled
// preparation; actual UI, autonomous follower and current save registry are observed.
public sealed class Wim029CompanionLiveRunner
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-029-companion-live-verified.txt";
    public const string CombatReportPath = "Artifacts/QA/wim-implementation/wim-029-companion-combat-live.txt";
    public const string CareReportPath = "Artifacts/QA/wim-implementation/wim-029-companion-care-live.txt";
    public const string HaulReportPath = "Artifacts/QA/wim-implementation/wim-029-animal-haul-live.txt";
    private static bool running;
    private bool combatOnly;
    private bool careOnly;
    private bool haulOnly;
    private string OutputPath => haulOnly ? HaulReportPath : combatOnly ? CombatReportPath : careOnly ? CareReportPath : ReportPath;
    private readonly List<string> lines = new();
    private DungeonRuntimeLifetimeScope scope;
    private IGameClock clock;
    private IGameTimeScaleController timeScale;
    private ICharacterAiWorldRegistry world;
    private IWildlifeCaptureRuntime capture;
    private IWildlifeCompanionRoleQuery roles;
    private WildlifeRuntime wildlife;
    private WildlifeActor pet;
    private CharacterActor owner;
    private Grid grid;
    private IEnvironmentalFieldPersistence environment;
    private DungeonEnvironmentalFieldSaveData originalEnvironment;
    private DungeonEnvironmentalFieldSaveData safeEnvironment;
    private float nextEnvironmentRefresh;

    public static string StartFocused() => Start(false);
    public static string StartCombat() => Start(true);
    public static string StartCare() => Start(false, true);
    public static string StartHaul() => Start(false, false, true);

    private static string Start(bool combatOnly, bool careOnly = false, bool haulOnly = false)
    {
        Require(Application.isPlaying && !running, "Fresh disposable main Play required.");
        var scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime missing.");
        var persistence = scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "User save protection unavailable.");
        persistence.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        var host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Main coroutine host missing.");
        host.isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        var runner = new Wim029CompanionLiveRunner { combatOnly = combatOnly, careOnly = careOnly, haulOnly = haulOnly };
        Directory.CreateDirectory(Path.GetDirectoryName(runner.OutputPath));
        File.WriteAllText(runner.OutputPath, "result=RUNNING\n");
        running = true;
        try { host.StartCoroutine(runner.Observe()); }
        catch { running = false; throw; }
        return "RUNNING " + runner.OutputPath;
    }

    private IEnumerator Observe()
    {
        var pending = new Stack<IEnumerator>();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object value = null;
            bool moved;
            try { MaintainEnvironment(); moved = pending.Peek().MoveNext(); if (moved) value = pending.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
            if (value is IEnumerator nested) pending.Push(nested); else yield return value;
        }
        try { while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose(); }
        finally
        {
            Pause();
            if (originalEnvironment != null)
                try { environment.Restore(environment.PrepareRestore(originalEnvironment)); }
                catch (Exception error) { failure = failure == null ? error : new AggregateException(failure, error); }
        }
        lines.Add(Diagnostic());
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add(haulOnly
            ? "scope=authored pen/domestic goat and physical ore are controlled preparation; other stock forbidden and human AI paused. Actual role UI, autonomous exact-lot pickup/movement/delivery, carried current save and public lethal interruption. Not natural birth/capture, facility-output hauling, forced drop-failure recovery, hauling throughput balance or six-adult certification."
            : combatOnly
            ? "scope=controlled authored pen/domestic birth and public invasion spawn; actual EventSystem owner assignment, natural entry/defense and companion ticks. No synthetic attack results or HP scaling; any terminal lifecycle injection is labeled in its own row. Not animal haul, encounter balance or six-adult certification."
            : careOnly
            ? "scope=controlled authored pen/domestic birth, one public nonlethal damage and one thirst perturbation; actual player movement, autonomous follow and care return. No remote feed/need reset, synthetic combat result or whole restore rerun. Not animal haul, encounter balance or six-adult certification."
            : "scope=controlled authored initial pen/domestic birth, healthy staff and safe field; actual EventSystem owner assignment/change/clear, autonomous follow/return, current whole registry restore. Not natural capture/birth/construction, reciprocal combat, animal haul, daily balance or six-adult certification.");
        lines.Add("cleanup=paused disposable main Play, operator stops; disk persistence disabled before owner UI; original environment restored");
        File.WriteAllLines(OutputPath, lines);
        Debug.Log("WIM029 companion witness " + (failure == null ? "PASS" : "FAIL: " + failure.Message));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        clock = scope.Container.Resolve<IGameClock>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        capture = scope.Container.Resolve<IWildlifeCaptureRuntime>();
        roles = scope.Container.Resolve<IWildlifeCompanionRoleQuery>();
        wildlife = scope.Container.Resolve<WildlifeRuntime>();
        var runOwner = FindFirstObjectByType<OwnerRunManager>();
        Require(runOwner != null, "Owner preparation missing.");
        if (runOwner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>().TryReconcileNewRunTierZero(
                out var expansion, out string reason) && expansion.CurrentInteriorColumns == 29, "TierZero: " + reason);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(runOwner.CurrentOwnerActor != null, "Actual owner UI did not publish party.");
        }
        Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out grid), "Main grid missing.");
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(true);
        CharacterActor[] eligible = world.Characters.Where(x => x != null && !x.IsDead
            && x.characterType == CharacterType.NPC && x.CurrentLifecycleState == CharacterLifecycleState.Active
            && x.GetComponent<AbilityWork>() != null).OrderBy(x => x.Identity.PersistentId, StringComparer.Ordinal).ToArray();
        Require(eligible.Length >= 2, "Actual staff alternatives missing.");
        owner = eligible[0];
        foreach (var actor in eligible)
        {
            foreach (var need in new[] { CharacterCondition.HUNGER, CharacterCondition.THIRST, CharacterCondition.SLEEP,
                CharacterCondition.HYGIENE, CharacterCondition.EXCRETION, CharacterCondition.FUN })
                actor.Stats.ChangesStat(need, 100 - actor.Stats.GetConditionValue(need, 0));
            foreach (var type in WorkTypeCatalog.All)
                actor.GetComponent<AbilityWork>().SetWorkPriority(type.WorkTypeId, WorkPriorityLevel.Off);
        }
        environment = scope.Container.Resolve<IEnvironmentalFieldPersistence>();
        originalEnvironment = environment.Capture();
        safeEnvironment = JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(JsonUtility.ToJson(originalEnvironment));
        safeEnvironment.cells.Clear();
        for (int y = 0; y < safeEnvironment.height; y++)
            for (int x = 0; x < safeEnvironment.width; x++)
                safeEnvironment.cells.Add(new EnvironmentalCellSaveData
                    { x = x, y = y, temperatureC = 22, airQuality = 100, lightLevel = 100 });
        MaintainEnvironment();

        BuildableObject pen = CreateAuthoredPen();
        Require(scope.Container.Resolve<IRoomLayoutCache>().TryGetRoom(pen, out RoomInstance penRoom) && penRoom.IsUsable,
            "Authored pen must belong to a usable actual room before birth/restore.");
        Vector2Int birthCell = Cells().Where(c => c.AreaType == GridCellAreaType.DungeonInterior
            && penRoom.ContainsCell(c.Position) && grid.IsWalkable(c.Position) && !c.HasOccupantInLayer(GridLayer.Character)
            && !c.HasOccupantInLayer(GridLayer.Wildlife) && !c.HasOccupantInLayer(GridLayer.Construction))
            // Traversable hallway/furniture occupancy is legal for the actual
            // domestic-birth command; do not invent an empty-Building-layer rule.
            .OrderBy(c => pen.buildPoses.Contains(c.Position)).ThenBy(c => Distance(c.Position, pen.centerPos))
            .ThenBy(c => c.Position.y).ThenBy(c => c.Position.x)
            .Select(c => (Vector2Int?)c.Position).FirstOrDefault()
            ?? throw new InvalidOperationException("No legal domestic-birth cell beside authored pen.");
        Require(wildlife.TrySpawnDomesticBirth(haulOnly ? "deep_goat" : "cave_hound", birthCell, out pet, out string born), "Domestic preparation: " + born);
        Require(pet.GridPosition == birthCell, "Domestic fixture silently changed selected position.");
        Require(capture.TryRegisterPenBorn(pet, pen.RequirePersistentInstanceId().Value, birthCell, out string registered),
            "Penned preparation: " + registered);
        lines.Add("prepared=authored pen " + pen.RequirePersistentInstanceId().Value + "; pet=" + pet.WildlifeId
            + "; penCell=" + birthCell + "; hp=" + pet.CurrentHealth + "; actual owner alternatives=" + eligible.Length
            + "; hunger=" + pet.Hunger + "; thirst=" + pet.Thirst);
        Require(pet.Hunger < 0.45f && pet.Thirst < 0.45f, "Domestic fixture already needs care, cannot isolate follow.");

        if (haulOnly)
        {
            yield return ObserveHaul();
            yield break;
        }

        scope.Container.Resolve<IGameEventBus>().ShowInfo(pet);
        yield return null;
        Click("Action_6");
        yield return null;
        Click("Owner_0");
        Require(roles.TryGetCompanion(pet.WildlifeId, out var assigned)
            && assigned.OwnerId.Value == owner.Identity.PersistentId, "Actual UI owner0 not committed.");
        lines.Add("PASS ownerUI=" + assigned.OwnerId.Value);
        FindFirstObjectByType<WildlifeInfoPanel>().OnClose();
        if (combatOnly)
        {
            yield return ObserveCombat();
            yield break;
        }
        Vector2Int initialPet = pet.GridPosition;
        Vector2Int destination = Cells().Where(c => c.AreaType == GridCellAreaType.DungeonInterior
            && grid.IsWalkable(c.Position) && c.Position.y == birthCell.y
            && Distance(c.Position, birthCell) >= 6 && Distance(c.Position, birthCell) <= 10)
            .OrderBy(c => Distance(c.Position, owner.GetNowXY())).ThenBy(c => c.Position.x)
            .Select(c => (Vector2Int?)c.Position).FirstOrDefault()
            ?? throw new InvalidOperationException("No meaningful follow destination.");
        owner.SetAiPaused(false);
        Resume();
        // The path broker may defer a cold request. Wait for its typed result
        // before sending the real movement command; walkability is not reachability.
        var broker = scope.Container.Resolve<IGridPathSearchBroker>();
        GridPathRequestStatus preparedPath = GridPathRequestStatus.Pending;
        float pathStart = Time.realtimeSinceStartup;
        do
        {
            yield return null;
            preparedPath = broker.RequestMovePathTo(grid, owner.GetNowXY(), destination, out _,
                GridPathSearchPriority.Urgent, GridTraversalContext.ForCharacter(
                    CharacterPersistentIdentity.Require(owner), DoorAccessOverrideKind.DirectCommand));
        } while (preparedPath == GridPathRequestStatus.Pending && Time.realtimeSinceStartup - pathStart < 5);
        Require(preparedPath == GridPathRequestStatus.Reachable, "Player destination preparation: " + preparedPath);
        Require(owner.GetComponent<AbilityMove>().TryStartPlayerMove(destination, out string move), "Player move: " + move);
        yield return WaitFor(() => owner.GetNowXY() == destination && !owner.GetComponent<AbilityMove>().IsSystemMoveInProgress,
            40, "Owner real player movement");
        yield return WaitFor(() => pet.GridPosition != initialPet && Distance(pet.GridPosition, owner.GetNowXY()) <= 2,
            40, "Autonomous companion follow");
        Pause();
        lines.Add("PASS autonomousFollow " + initialPet + " -> " + pet.GridPosition + "; owner=" + owner.GetNowXY());

        if (careOnly)
        {
            yield return ObserveCare(birthCell);
            yield break;
        }

        // Real whole-world current-format round trip, then a malformed owner
        // through the same registry so required section candidates are prepared.
        var circus = scope.Container.Resolve<ICircusPersistence>();
        var registry = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        string petId = pet.WildlifeId;
        string ownerId = owner.Identity.PersistentId;
        string exactRole = JsonUtility.ToJson(circus.Capture().capturedWildlife.Single(x => x.wildlifeId == petId).capabilityState);
        var restoreReport = new DungeonGameRestoreReport();
        Require(registry.RestoreAll(registry.CaptureAll(), restoreReport), "Whole current restore: " + string.Join(" | ", restoreReport.Errors));
        pet = wildlife.Wildlife.Single(x => x.WildlifeId == petId);
        owner = world.Characters.Single(x => x.Identity.PersistentId == ownerId);
        Require(roles.TryGetCompanion(petId, out var restored) && restored.OwnerId.Value == ownerId
            && JsonUtility.ToJson(circus.Capture().capturedWildlife.Single(x => x.wildlifeId == petId).capabilityState) == exactRole,
            "Companion owner/cooldown/envelope changed across paused whole restore.");
        lines.Add("PASS wholeRegistryRestore owner/cooldown/envelope exact");
        var bad = JsonUtility.FromJson<CircusSaveData>(JsonUtility.ToJson(circus.Capture()));
        bad.capturedWildlife.Single(x => x.wildlifeId == petId).capabilityState.payloadJson =
            bad.capturedWildlife.Single(x => x.wildlifeId == petId).capabilityState.payloadJson.Replace(ownerId, "character:staff:wim-missing");
        string beforeRejectedRestore = JsonUtility.ToJson(circus.Capture());
        Vector2Int beforeRejectedCell = pet.GridPosition;
        int beforeRejectedHealth = pet.CurrentHealth;
        var badEnvelopes = registry.CaptureAll();
        badEnvelopes.Single(x => x.sectionId == CircusSaveSection.Id).payloadJson = JsonUtility.ToJson(bad);
        var rejectionReport = new DungeonGameRestoreReport();
        bool rejected = !registry.RestoreAll(badEnvelopes, rejectionReport);
        string rejection = string.Join(" | ", rejectionReport.Errors);
        pet = wildlife.Wildlife.Single(x => x.WildlifeId == petId);
        owner = world.Characters.Single(x => x.Identity.PersistentId == ownerId);
        Require(rejected && rejection.Contains("ineligible companion owner 'character:staff:wim-missing'")
            && JsonUtility.ToJson(circus.Capture()) == beforeRejectedRestore
            && pet.GridPosition == beforeRejectedCell && pet.CurrentHealth == beforeRejectedHealth
            && roles.TryGetCompanion(petId, out var retained) && retained.OwnerId.Value == ownerId,
            "Invalid owner whole restore rejected for wrong reason, accepted or mutated live state: " + rejection);
        lines.Add("expectedInvalidOwner=" + rejection);
        lines.Add("PASS invalidOwnerWholeRegistry liveCircus/HP/cell/roleUnchanged");

        scope.Container.Resolve<IGameEventBus>().ShowInfo(pet);
        yield return null;
        Click("Action_6"); yield return null; Click("Owner_1");
        Require(roles.TryGetCompanion(petId, out var changed) && changed.OwnerId.Value != ownerId, "UI owner change failed.");
        lines.Add("PASS ownerChangeUI=" + changed.OwnerId.Value);
        yield return null;
        // Selecting an owner closes only the owner subpanel, not the info window.
        Click("Action_6"); yield return null; Click("ClearRole");
        Require(!roles.TryGetCompanion(petId, out _), "UI clear retained companion role.");
        FindFirstObjectByType<WildlifeInfoPanel>().OnClose();
        Resume();
        yield return WaitFor(() => pet.GridPosition == birthCell, 40, "Unassigned return to actual pen");
        Pause();
        lines.Add("PASS clearUI/actualPenReturn=" + pet.GridPosition);
    }

    private IEnumerator ObserveCombat()
    {
        var director = FindFirstObjectByType<InvasionDirectorRuntime>();
        var naturalThreat = FindFirstObjectByType<InvasionThreatRuntime>();
        Require(director != null && naturalThreat != null, "Live invasion authorities missing.");
        bool wasEnabled = naturalThreat.enabled;
        var defense = scope.Container.Resolve<IDefenseEngagementRuntime>();
        var threat = new InvasionThreatSnapshot(125f, InvasionThreatStage.Candidate,
            new InvasionThreatFactors(6f, 4f, 3f, 1f), 0f, 0f);
        IDisposable injuryObservation = null;
        try
        {
            naturalThreat.enabled = false;
            owner.GetComponent<AbilityWork>().SetDutyState(AbilityWork.DutyState.OnDuty);
            owner.GetComponent<AbilityWork>().SetWorkPriority(BuiltInWorkTypeIds.Guard, WorkPriorityLevel.Priority1);
            owner.SetAiPaused(false);
            Require(director.TrySpawnIntruder(threat, out CharacterActor enemy) && enemy != null,
                "Actual invasion spawn failed.");
            Require(enemy.TryGetComponent<InvasionIntruderRuntime>(out var intruder), "Spawned intruder runtime missing.");
            string enemyId = enemy.Identity.PersistentId;
            int petInitialHealth = pet.CurrentHealth;
            float enemyInitialHealth = enemy.CurrentHealth;
            int companionHitEvents = 0;
            float companionAppliedDamage = 0;
            injuryObservation = scope.Container.Resolve<IGameEventBus>().Subscribe<CharacterInjuredIdentityEvent>(e =>
            {
                // In this isolated live engagement only the companion result port
                // publishes a Slash injury with no Character attacker. This is not
                // claimed as globally unique attack/operation provenance.
                if (e.Character.Value == enemyId && !e.Attacker.IsValid
                    && e.DamageType == CombatDamageType.Slash && e.AppliedDamage > 0)
                { companionHitEvents++; companionAppliedDamage += e.AppliedDamage; }
            });
            lines.Add("invasionPrepared enemy=" + enemyId + "; enemyHP=" + enemyInitialHealth
                + "; petHP=" + petInitialHealth + "; ownerGuardPriority=1; otherStaffGuard=Off");
            Resume();
            yield return WaitFor(() => intruder.HasBreachedDungeonInterior, 60, "Natural invasion entry");
            yield return WaitFor(() => defense.TryGetEngagement(intruder, out var e) && e.LeadGuard == owner,
                15, "Actual owner guard dispatch");
            Require(defense.TryGetEngagement(intruder, out var engagement), "Live engagement disappeared.");
            lines.Add("PASS actualGuardDispatch id=" + engagement.Id + "; ownerAiPaused=" + owner.IsAiPaused()
                + "; enemyCell=" + enemy.GetNowXY() + "; " + Diagnostic());
            float wallStart = Time.realtimeSinceStartup;
            float gameStart = clock.Time;
            float nextSample = clock.Time;
            float latestCooldown = 0;
            bool petReceivedDamage = false;
            while (Time.realtimeSinceStartup - wallStart < 45 && clock.Time - gameStart < 60)
            {
                if (pet.IsAlive)
                    Require(pet.State == WildlifeState.Captured
                        && roles.TryGetCompanion(pet.WildlifeId, out var liveRole)
                        && liveRole.OwnerId.Value == owner.Identity.PersistentId,
                        "Nonterminal combat damage lost companion custody/role.");
                petReceivedDamage |= pet.CurrentHealth < petInitialHealth;
                if (roles.TryGetCompanion(pet.WildlifeId, out var state))
                    latestCooldown = Mathf.Max(latestCooldown, state.NextAttackAt);
                if (clock.Time >= nextSample)
                {
                    lines.Add("combatSample state=" + engagement.State + "; exchanges=" + engagement.ExchangeCount
                        + "; enemyCell=" + enemy.GetNowXY() + "; enemyHP=" + enemy.CurrentHealth
                        + "; companionHits=" + companionHitEvents + "; nextAttack=" + latestCooldown + "; " + Diagnostic());
                    nextSample = clock.Time + 5;
                }
                if (petReceivedDamage && companionHitEvents > 0 && latestCooldown > gameStart) break;
                if (!pet.IsAlive || enemy.IsDead || !engagement.IsActive) break;
                yield return null;
            }
            Pause();
            Require(companionHitEvents > 0 && companionAppliedDamage > 0 && latestCooldown > gameStart,
                "No observed companion hit/cooldown through actual ticks.");
            Require(petReceivedDamage, "No actual reciprocal intruder damage to companion.");
            lines.Add("PASS reciprocalCombat companionHits=" + companionHitEvents + "; appliedDamage=" + companionAppliedDamage
                + "; petHP=" + petInitialHealth + "->" + pet.CurrentHealth + "; enemyHP=" + enemyInitialHealth + "->" + enemy.CurrentHealth
                + "; elapsedGame=" + (clock.Time - gameStart) + "; elapsedWall=" + (Time.realtimeSinceStartup - wallStart));

            string petId = pet.WildlifeId;
            if (pet.IsAlive)
            {
                Require(roles.TryGetCompanion(petId, out var retainedRole)
                    && retainedRole.OwnerId.Value == owner.Identity.PersistentId
                    && engagement.IsActive && engagement.LeadGuard == owner,
                    "Terminal intervention requires a live assigned companion and active owner guard.");
                lines.Add("controlledLifecycleIntervention=public ApplyDamage(current HP), not an observed enemy killing blow");
                pet.ApplyDamage(pet.CurrentHealth, enemy);
                Require(engagement.IsActive && engagement.LeadGuard == owner,
                    "Companion death fabricated lead-guard loss.");
                lines.Add("PASS controlledDeath didNotReleaseLiveLeadGuard");
            }
            Require(!pet.IsAlive && pet.State == WildlifeState.Dead, "Physical companion death not committed.");
            Resume();
            yield return WaitFor(() => !roles.TryGetCompanion(petId, out _), 5, "Dead companion role release");
            Pause();
            var targeting = scope.Container.Resolve<DefenseCompanionCombatRuntime>();
            Require(!targeting.IsEligibleIntruderMeleeTarget(engagement, enemy, new CombatParticipantRef(pet)),
                "Dead companion remained an eligible intruder target.");
            lines.Add("PASS companionDeath roleReleased/deadTargetRejected");
        }
        finally
        {
            injuryObservation?.Dispose();
            naturalThreat.enabled = wasEnabled;
        }
    }

    private IEnumerator ObserveCare(Vector2Int penCell)
    {
        string petId = pet.WildlifeId;
        string ownerId = owner.Identity.PersistentId;
        int healthy = pet.CurrentHealth;
        Require(healthy > 1 && Distance(pet.GridPosition, penCell) >= 3,
            "Care witness needs a live companion meaningfully away from its pen.");
        Require(pet.ApplyDamage(1, null) == 1 && pet.CurrentHealth == healthy - 1,
            "Controlled nonlethal physical damage was not exact.");
        Require(pet.State == WildlifeState.Captured
            && roles.TryGetCompanion(petId, out var afterHit) && afterHit.OwnerId.Value == ownerId,
            "Nonlethal damage changed captured custody or companion owner.");
        float observeUntil = clock.Time + 1.1f;
        Resume();
        yield return WaitFor(() => clock.Time >= observeUntil, 5, "Nonlethal role tick");
        Pause();
        Require(pet.IsAlive && pet.State == WildlifeState.Captured
            && roles.TryGetCompanion(petId, out var afterTick) && afterTick.OwnerId.Value == ownerId,
            "Role tick cleared a nonterminally injured companion.");
        lines.Add("PASS nonlethalPhysicalDamage=" + healthy + "->" + pet.CurrentHealth + "; captured/owner preserved through ticks");

        Vector2Int departure = pet.GridPosition;
        Require(pet.Thirst < 0.45f, "Care fixture already crossed its threshold.");
        pet.ChangeThirst(0.46f - pet.Thirst);
        float thirstAfterPerturbation = pet.Thirst;
        Resume();
        yield return WaitFor(() => pet.GridPosition == penCell && pet.IntentReason.Contains("돌봄"),
            25, "Actual care return");
        Pause();
        Require(pet.State == WildlifeState.Captured && pet.IsAlive
            && roles.TryGetCompanion(petId, out var afterCare) && afterCare.OwnerId.Value == ownerId
            && pet.Thirst >= thirstAfterPerturbation,
            "Care return lost role or silently restored thirst without physical feeding.");
        lines.Add("PASS actualCareReturn=" + departure + "->" + pet.GridPosition
            + "; owner=" + owner.GetNowXY() + "; thirst=" + pet.Thirst
            + "; owner/custody retained; no remote need recovery");
    }

    private IEnumerator ObserveHaul()
    {
        const string oreId = "resource:iron-ore";
        const string custodyId = "item-state:wildlife-haul-custody";
        var items = scope.Container.Resolve<IWorldItemStackRuntime>();
        var haul = scope.Container.Resolve<IWildlifeHaulRoleQuery>();
        var registry = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        var circus = scope.Container.Resolve<ICircusPersistence>();
        string petId = pet.WildlifeId;
        string ownerId = owner.Identity.PersistentId;
        Require(items.MassQuery.GetDefinitionUnitMass(new ItemDefinitionId(oreId)).Value == 1200,
            "Authored ore 1200g changed; update the physical fixture deliberately.");
        var definition = items.CatalogProvider.GetDefinition(oreId);
        var admissibleStores = world.Warehouses.Where(x => x != null && x.HasWarehouseInventory && x.Inventory != null
            && x.Inventory.Accepts(definition.StockCategory) && x.Inventory.GetAcceptableQuantity(oreId, 1) > 0
            && x.Inventory.RemainingMassGrams >= 1200 && x is BuildableObject building && !building.isDestroy).ToArray();
        BuildableObject[] stores = admissibleStores
            .OfType<BuildableObject>().Where(x => !x.isDestroy).ToArray();
        // The production planner splits a lot to the selected warehouse's
        // available grams. 18kg is the goat's ceiling, not a minimum batch.
        Require(stores.Length > 0 && admissibleStores.Sum(x => x.Inventory.RemainingMassGrams) >= 36000,
            "Actual compatible warehouses need 36kg combined spare capacity; partial trips are legal.");
        int forbidden = 0;
        foreach (var stack in items.GetAllStacks().Where(x => !x.Forbidden
            && (x.State == WorldItemStackState.Loose || (x.State == WorldItemStackState.Stored
                && !string.IsNullOrEmpty(x.SourceStorageDestinationId)))).ToArray())
        {
            Require(items.SetForbidden(stack.StackId, true), "Cannot isolate existing loose/outbound stock: " + stack.StackId);
            forbidden++;
        }
        Vector2Int sourceCell = Cells().Where(c => c.AreaType == GridCellAreaType.DungeonInterior
            && grid.IsWalkable(c.Position) && !c.HasOccupantInLayer(GridLayer.Character)
            && !c.HasOccupantInLayer(GridLayer.Wildlife) && !c.HasOccupantInLayer(GridLayer.Construction)
            && !items.GetStacksAt(c.Position, true).Any()
            && stores.Min(s => Distance(s.centerPos, c.Position)) >= 6)
            .OrderBy(c => Distance(c.Position, pet.GridPosition)).ThenBy(c => c.Position.y).ThenBy(c => c.Position.x)
            .Select(c => (Vector2Int?)c.Position).FirstOrDefault()
            ?? throw new InvalidOperationException("No actual walkable ore source at least six cells from warehouse.");
        int beforeOre = items.GetAllStacks().Where(x => x.ItemId == oreId).Sum(x => x.Quantity);
        Require(items.SpawnItemAt(oreId, 30, sourceCell, WorldItemStackState.Loose, string.Empty, out int spawned)
            && spawned == 30, "Controlled physical ore source failed.");
        int expectedOre = beforeOre + 30;
        lines.Add("haulPreparation=30 authored ore/36000g at " + sourceCell + "; existing stock forbidden=" + forbidden
            + "; actual warehouse(s)=" + stores.Length + "; human AI paused; no virtual cargo/source reservation injection");

        scope.Container.Resolve<IGameEventBus>().ShowInfo(pet); yield return null;
        ClickText("독립 운반");
        Require(haul.TryGetHaul(petId, out var initial) && initial.Phase == CapturedWildlifeHaulPhase.Idle
            && !roles.TryGetCompanion(petId, out _), "Actual independent-haul UI did not publish exclusive Idle role.");
        FindFirstObjectByType<WildlifeInfoPanel>().OnClose();
        Resume();
        float began = clock.Time, wall = Time.realtimeSinceStartup;
        yield return WaitFor(() => haul.TryGetHaul(petId, out var value)
            && value.Phase == CapturedWildlifeHaulPhase.CargoOwned, 75, "Natural animal pickup");
        Pause();
        Require(haul.TryGetHaul(petId, out var carrying) && carrying.ItemId == oreId
            && carrying.Quantity > 0 && carrying.Quantity * 1200L <= 18000
            && carrying.DestinationKind == WorldItemHaulDestinationKind.Warehouse,
            "Actual goat cargo exceeded 18kg or used unexpected item/destination.");
        var physicalCargo = items.GetAllStacks().Single(x => x.StackId == carrying.CargoStackId);
        Require(physicalCargo.State == WorldItemStackState.InTransit && physicalCargo.Quantity == carrying.Quantity
            && physicalCargo.Position == pet.GridPosition && physicalCargo.Components.Count(x => x.componentTypeId == custodyId) == 1
            && items.GetAllStacks().Where(x => x.ItemId == oreId).Sum(x => x.Quantity) == expectedOre,
            "Pickup lost/duplicated ore, omitted custody or teleported the physical cargo.");
        lines.Add("PASS autonomousPickup op=" + carrying.OperationId + "; source=" + carrying.SourceStackId
            + "; cargo=" + carrying.CargoStackId + "; quantity=" + carrying.Quantity + "; grams=" + carrying.Quantity * 1200L
            + "; actor/cargoCell=" + pet.GridPosition + "; destination=" + carrying.DestinationId);

        // Capture and restore through the whole current registry, not a detached
        // Circus DTO or a fixture-created hauling intent.
        string roleBefore = JsonUtility.ToJson(circus.Capture().capturedWildlife.Single(x => x.wildlifeId == petId).capabilityState);
        string physicalBefore = HaulPhysicalSignature(items, includeTransientReservations: false);
        var restoredReport = new DungeonGameRestoreReport();
        Require(registry.RestoreAll(registry.CaptureAll(), restoredReport),
            "Carried whole restore failed: " + string.Join(" | ", restoredReport.Errors));
        // Match DungeonGameSaveService's successful-restore lifecycle. The
        // section registry alone deliberately leaves runtime resume pending.
        foreach (var hook in scope.Container.Resolve<IEnumerable<IDungeonSaveRestoreCompletedHook>>())
            hook.OnRestoreCompleted();
        pet = wildlife.Wildlife.Single(x => x.WildlifeId == petId);
        owner = world.Characters.Single(x => x.Identity.PersistentId == ownerId);
        string physicalAfterRestore = HaulPhysicalSignature(items, includeTransientReservations: false);
        if (physicalAfterRestore != physicalBefore)
        {
            var beforeRows = physicalBefore.Split('\n');
            var afterRows = physicalAfterRestore.Split('\n');
            lines.Add("restorePhysicalBeforeOnly=" + string.Join("\n", beforeRows.Except(afterRows).Take(8)));
            lines.Add("restorePhysicalAfterOnly=" + string.Join("\n", afterRows.Except(beforeRows).Take(8)));
        }
        Require(haul.TryGetHaul(petId, out var restored) && restored.Phase == CapturedWildlifeHaulPhase.CargoOwned
            && restored.OperationId == carrying.OperationId && restored.CargoStackId == carrying.CargoStackId
            && items.GetAllStacks().Single(x => x.StackId == restored.CargoStackId).Position == pet.GridPosition
            && JsonUtility.ToJson(circus.Capture().capturedWildlife.Single(x => x.wildlifeId == petId).capabilityState) == roleBefore
            && physicalAfterRestore == physicalBefore, "Carried restore changed role, cargo, route or quantity.");
        lines.Add("PASS carriedWholeRegistry role/cargo/route/physical stock exact");
        lines.Add("restoreComparison=durable physical fields and complete wildlife custody; transient uncommitted reservations are not a saved invariant. Rejected commands below still compare live reservations exactly.");
        physicalBefore = HaulPhysicalSignature(items);

        Require(pet.GridPosition != restored.DeliveryPosition,
            "Remote-delivery rejection needs an actually distant carried lot.");
        Require(!scope.Container.Resolve<IWildlifeHaulItemRuntime>().TryDeliver(restored, out string remoteReason)
            && !string.IsNullOrEmpty(remoteReason) && HaulPhysicalSignature(items) == physicalBefore
            && JsonUtility.ToJson(circus.Capture().capturedWildlife.Single(x => x.wildlifeId == petId).capabilityState) == roleBefore,
            "Items delivery boundary allowed remote completion or mutated rejected cargo.");
        lines.Add("PASS remoteDelivery rejected before mutation; reason=" + remoteReason);

        // Focused preflight contracts use captured live cargo, but are not
        // presented as natural conveyor operation or a whole-registry restore.
        var capturedSections = registry.CaptureAll().ToDictionary(x => x.sectionId, StringComparer.Ordinal);
        var custodyValidator = new WildlifeHaulCargoSaveValidation();
        var validSequenceReport = new DungeonGameRestoreReport();
        custodyValidator.Validate(capturedSections, validSequenceReport);
        Require(validSequenceReport.Errors.Count == 0, "Live haul preflight prerequisite failed.");
        var capturedPhysical = JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
            capturedSections[PhysicalItemsSaveSection.Id].payloadJson);
        Require(capturedPhysical.nextHaulOperationSequence > 1, "Actual haul operation did not advance saved allocator.");
        capturedPhysical.nextHaulOperationSequence = 1;
        capturedSections[PhysicalItemsSaveSection.Id].payloadJson = JsonUtility.ToJson(capturedPhysical);
        var futureSequenceReport = new DungeonGameRestoreReport();
        custodyValidator.Validate(capturedSections, futureSequenceReport);
        Require(futureSequenceReport.Errors.Count > 0 && HaulPhysicalSignature(items) == physicalBefore,
            "Preflight accepted an allocated cargo operation outside the saved allocator sequence.");
        lines.Add("PASS futureOperation preflight rejected; reason=" + string.Join(" | ", futureSequenceReport.Errors));

        var dualPhysical = new DungeonPhysicalItemSaveData
        {
            stacks = new List<WorldItemStackSaveData> { capturedPhysical.stacks.Single(x => x.stackId == restored.CargoStackId) }
        };
        var dualConveyor = new DungeonConveyorInfrastructureSaveData();
        var segmentFixture = new ModularFacilityWorldSaveData
        {
            buildings = new List<ModularFacilityBuildingSaveData>
            {
                new() { persistentInstanceId = "fixture:wim029:conveyor-segment" }
            }
        };
        ConveyorPhysicalCustodySaveValidation.ValidateCore(dualPhysical, dualConveyor, segmentFixture);
        dualConveyor.payloads.Add(new ConveyorPayloadSaveData
        {
            payloadId = dualPhysical.stacks[0].destinationId, itemStackId = restored.CargoStackId,
            segmentBuildingInstanceId = "fixture:wim029:conveyor-segment"
        });
        string dualReason = string.Empty;
        try { ConveyorPhysicalCustodySaveValidation.ValidateCore(dualPhysical, dualConveyor, segmentFixture); }
        catch (InvalidOperationException exception) { dualReason = exception.Message; }
        Require(!string.IsNullOrEmpty(dualReason) && HaulPhysicalSignature(items) == physicalBefore,
            "Conveyor preflight accepted a second owner for the same live wildlife cargo.");
        lines.Add("PASS dualConveyorWildlifeOwner preflight rejected; reason=" + dualReason);

        string circusBeforeInvalid = JsonUtility.ToJson(circus.Capture());
        Vector2Int cellBeforeInvalid = pet.GridPosition;
        int healthBeforeInvalid = pet.CurrentHealth;
        var bad = JsonUtility.FromJson<CircusSaveData>(circusBeforeInvalid);
        var badRole = bad.capturedWildlife.Single(x => x.wildlifeId == petId).capabilityState;
        string validPayload = badRole.payloadJson;
        badRole.payloadJson = validPayload.Replace(carrying.CargoStackId, carrying.SourceStackId);
        Require(badRole.payloadJson != validPayload && carrying.CargoStackId != carrying.SourceStackId,
            "Malformed custody witness must change the actual cargo reference, not an unrelated field.");
        var badEnvelopes = registry.CaptureAll();
        badEnvelopes.Single(x => x.sectionId == CircusSaveSection.Id).payloadJson = JsonUtility.ToJson(bad);
        var badReport = new DungeonGameRestoreReport();
        Require(!registry.RestoreAll(badEnvelopes, badReport) && badReport.Errors.Count > 0
            && HaulPhysicalSignature(items) == physicalBefore && JsonUtility.ToJson(circus.Capture()) == circusBeforeInvalid,
            "Wrong physical cargo reference restored or changed live role/stock during rejection.");
        pet = wildlife.Wildlife.Single(x => x.WildlifeId == petId);
        owner = world.Characters.Single(x => x.Identity.PersistentId == ownerId);
        Require(pet.GridPosition == cellBeforeInvalid && pet.CurrentHealth == healthBeforeInvalid,
            "Rejected cargo restore changed the live animal position or health.");
        lines.Add("PASS invalidCargoWholeRegistry rejected, live role/stock unchanged; reason=" + string.Join(" | ", badReport.Errors));

        // Public UI clear while loaded must retain the same cargo until actual
        // delivery. The command must not return it remotely to source storage.
        scope.Container.Resolve<IGameEventBus>().ShowInfo(pet); yield return null;
        ClickText("독립 운반");
        Require(haul.TryGetHaul(petId, out var releasing) && releasing.Phase == CapturedWildlifeHaulPhase.ReleasePending
            && releasing.CargoStackId == carrying.CargoStackId && releasing.OperationId == carrying.OperationId
            && HaulPhysicalSignature(items) == physicalBefore, "Loaded clear released physical ownership prematurely.");
        FindFirstObjectByType<WildlifeInfoPanel>().OnClose();
        int destinationBefore = items.GetAllStacks().Where(x => x.ItemId == oreId && x.State == WorldItemStackState.Stored
            && x.DestinationId == carrying.DestinationId).Sum(x => x.Quantity);
        Resume();
        yield return WaitFor(() => !haul.TryGetHaul(petId, out _), 60, "Loaded clear/delivery completion");
        Pause();
        Require(items.GetAllStacks().Where(x => x.ItemId == oreId && x.State == WorldItemStackState.Stored
                && x.DestinationId == carrying.DestinationId).Sum(x => x.Quantity) == destinationBefore + carrying.Quantity
            && items.GetAllStacks().Where(x => x.ItemId == oreId).Sum(x => x.Quantity) == expectedOre
            && !items.GetAllStacks().Any(x => x.StackId == carrying.CargoStackId && x.State == WorldItemStackState.InTransit)
            && !items.GetAllStacks().Any(x => x.Components.Any(c => c.componentTypeId == custodyId)),
            "Delivery failed exact quantity/destination or left transit custody orphaned.");
        lines.Add("PASS actualDelivery then clear; destination delta=" + carrying.Quantity + "; remaining role/custody=0");

        scope.Container.Resolve<IGameEventBus>().ShowInfo(pet); yield return null;
        ClickText("독립 운반");
        Require(haul.TryGetHaul(petId, out _), "Actual UI did not reassign haul for remaining source.");
        FindFirstObjectByType<WildlifeInfoPanel>().OnClose();
        Resume();
        yield return WaitFor(() => haul.TryGetHaul(petId, out var value)
            && value.Phase == CapturedWildlifeHaulPhase.CargoOwned, 75, "Second natural animal pickup");
        Pause();
        Require(haul.TryGetHaul(petId, out var dyingCargo) && dyingCargo.ItemId == oreId
            && dyingCargo.OperationId != carrying.OperationId && dyingCargo.Quantity > 0,
            "Reassign reused operation identity or lacked actual cargo.");
        Vector2Int interrupted = pet.GridPosition;
        Require(Distance(interrupted, dyingCargo.DropPosition) > 1,
            "Lethal interruption must be observed in transit, not after destination arrival.");
        pet.ApplyDamage(pet.CurrentHealth, null);
        Require(!pet.IsAlive, "Controlled lethal lifecycle command did not kill the animal.");
        Resume();
        yield return WaitFor(() => items.GetAllStacks().Any(x => x.StackId == dyingCargo.CargoStackId
            && x.State == WorldItemStackState.Loose && x.IsTransientCarryRecoveryDrop), 15, "Physical lethal recovery drop");
        Pause();
        var dropped = items.GetAllStacks().Single(x => x.StackId == dyingCargo.CargoStackId);
        Require(dropped.ItemId == oreId && dropped.Quantity == dyingCargo.Quantity && dropped.Position == interrupted
            && dropped.RecoveryCarrierPersistentId == petId && dropped.RecoveryOwnerOperationId == dyingCargo.OperationId
            && dropped.RecoveryInterruptionKind == WorldItemCarryInterruptionKind.Dead
            && dropped.RecoveryDeadlineGameTime > dropped.DroppedAtGameTime
            && !dropped.Components.Any(x => x.componentTypeId == custodyId)
            && !haul.TryGetHaul(petId, out _) && !circus.Capture().capturedWildlife.Any(x => x.wildlifeId == petId)
            && items.GetAllStacks().Where(x => x.ItemId == oreId).Sum(x => x.Quantity) == expectedOre,
            "Dead-animal cargo was lost, teleported, duplicated or retained by a dead Circus owner.");
        lines.Add("PASS controlledDeath physicalDrop=" + interrupted + "; quantity=" + dropped.Quantity
            + "; operation=" + dropped.RecoveryOwnerOperationId + "; deadline=" + dropped.RecoveryDeadlineGameTime
            + "; dead role/custody=0; ore conserved=" + expectedOre);
        string recoveryBefore = HaulPhysicalSignature(items, includeTransientReservations: false);
        var recoveryReport = new DungeonGameRestoreReport();
        Require(registry.RestoreAll(registry.CaptureAll(), recoveryReport),
            "Recovery drop whole restore failed: " + string.Join(" | ", recoveryReport.Errors));
        foreach (var hook in scope.Container.Resolve<IEnumerable<IDungeonSaveRestoreCompletedHook>>())
            hook.OnRestoreCompleted();
        Require(HaulPhysicalSignature(items, includeTransientReservations: false) == recoveryBefore && !haul.TryGetHaul(petId, out _),
            "Whole restore lost recovery provenance or resurrected the hauling role.");
        lines.Add("PASS recoveryDropWholeRegistry exact; gameSeconds=" + (clock.Time - began)
            + "; wallSeconds=" + (Time.realtimeSinceStartup - wall));
    }

    private static string HaulPhysicalSignature(IWorldItemStackRuntime items, bool includeTransientReservations = true) => string.Join("\n",
        items.GetAllStacks().OrderBy(x => x.StackId, StringComparer.Ordinal).Select(x => x.StackId + "|" + x.ItemId
            + "|" + x.Quantity + "|" + x.State + "|" + x.Position + "|" + x.DestinationId + "|" + x.StackSignature
            + "|" + (includeTransientReservations ? x.ReservedQuantity + "|" + x.ReservedByPersistentId : "transient-not-durable")
            + "|" + x.ReservationSignature + "|" + x.SourceStorageDestinationId
            + "|" + x.HasDestinationPosition + "|" + x.DestinationPosition + "|" + x.DropDisposition
            + "|" + string.Join(";", x.Components.OrderBy(c => c.componentTypeId, StringComparer.Ordinal).Select(c => JsonUtility.ToJson(c)))
            + "|" + x.RecoveryOwnerOperationId + "|" + x.RecoveryCarrierPersistentId + "|" + x.RecoverySourceStackId
            + "|" + x.RecoveryInterruptionKind + "|" + x.DroppedAtGameTime + "|" + x.RecoveryDeadlineGameTime));

    private static void ClickText(string text)
    {
        var matches = Resources.FindObjectsOfTypeAll<Button>().Where(x => x != null && x.gameObject.scene.isLoaded
            && x.gameObject.activeInHierarchy && x.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(t => t.text == text)).ToArray();
        Require(matches.Length == 1 && matches[0].IsInteractable()
            && PlayModeVerificationFrameWait.DispatchPointerClick(matches[0].gameObject, Vector2.zero),
            "Actual unique UI text unavailable: " + text);
    }

    private BuildableObject CreateAuthoredPen()
    {
        var existing = world.Buildings.FirstOrDefault(x => x != null && x.BuildingData.GetBeastPenAbility()?.IsValid == true);
        if (existing != null) return existing;
        var authored = AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building/Captivity" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(x => x, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>).FirstOrDefault(x => x.GetBeastPenAbility()?.IsValid == true);
        Require(authored != null, "Authored BeastPen missing.");
        var geometry = new GridPlacementValidator();
        var rooms = scope.Container.Resolve<IRoomLayoutCache>();
        Vector2Int anchor = Cells().Where(c => c.AreaType == GridCellAreaType.DungeonInterior)
            .OrderBy(c => Distance(c.Position, owner.GetNowXY())).ThenBy(c => c.Position.y).ThenBy(c => c.Position.x)
            .Select(c => c.Position).Where(p => rooms.TryGetRoom(grid, p, out var room) && room.IsUsable
                && authored.GetGridPosList(p).All(room.ContainsCell)
                && geometry.AreInsideHorizontalBounds(grid, authored.GetGridPosList(p), 1)
                && geometry.CanBuildInArea(grid, authored, authored.GetGridPosList(p))
                && geometry.CanOccupy(grid, authored.Placement.Layer, authored.GetGridPosList(p))
                && geometry.HasSupportBelow(grid, authored.GetGridPosList(p)))
            .Select(p => (Vector2Int?)p).FirstOrDefault() ?? throw new InvalidOperationException("No legal pen fixture footprint.");
        var builder = FindFirstObjectByType<DungeonStoryGridBuildingController>();
        Require(builder != null && builder.TryPlaceInitialBuildings(new[] { new InitialBuildInfo { Building = authored, Position = anchor } }, out _),
            "Initial authored pen command failed.");
        return world.Buildings.Single(x => x != null && x.BuildingData.GetBeastPenAbility()?.IsValid == true);
    }
    private IEnumerable<GridCell> Cells()
    {
        for (int y = 0; y < grid.height; y++) for (int x = 0; x < grid.width; x++)
            if (grid.GetGridCell(new Vector2Int(x, y)) is GridCell cell) yield return cell;
    }
    private IEnumerator WaitFor(Func<bool> predicate, float seconds, string stage)
    {
        float start = Time.realtimeSinceStartup;
        while (!predicate() && Time.realtimeSinceStartup - start < seconds) yield return null;
        Require(predicate(), stage + " timed out: " + Diagnostic());
    }
    private void MaintainEnvironment()
    {
        if (safeEnvironment == null || clock.Time < nextEnvironmentRefresh) return;
        environment.Restore(environment.PrepareRestore(safeEnvironment)); nextEnvironmentRefresh = clock.Time + 5;
    }
    private string Diagnostic() => "pet=" + pet?.WildlifeId + "; petCell=" + pet?.GridPosition + "; petHP=" + pet?.CurrentHealth
        + "; hunger=" + pet?.Hunger + "; thirst=" + pet?.Thirst + "; intent=" + pet?.IntentReason
        + "; owner=" + owner?.Identity.PersistentId + "; ownerCell=" + owner?.GetNowXY() + "; gameTime=" + clock?.Time;
    private static int Distance(Vector2Int a, Vector2Int b) => Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y);
    private static void Click(string name)
    {
        var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(x => x != null && x.name == name
            && x.gameObject.scene.isLoaded && x.gameObject.activeInHierarchy);
        Require(button != null && button.IsInteractable()
            && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual UI unavailable: " + name);
    }
    private void Pause() { var game = FindFirstObjectByType<GameManager>(); if (game != null) game.isPause = true; if (timeScale != null) timeScale.Scale = 0; }
    private void Resume() { FindFirstObjectByType<GameManager>().isPause = false; timeScale.Scale = 2; }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
#endif
