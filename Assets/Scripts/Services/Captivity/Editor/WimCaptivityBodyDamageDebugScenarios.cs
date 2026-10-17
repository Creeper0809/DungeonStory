#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

[InitializeOnLoad]
public static class WimCaptivityBodyDamageDebugScenarios
{
    public const string RequestPath =
        "Temp/wim-054-captivity-body-damage.request";
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-054-captivity-body-damage.txt";
    private const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";
    private static bool runnerCreated;

    static WimCaptivityBodyDamageDebugScenarios()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/Captivity/Request WIM-054 Verification")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        File.Delete(ReportPath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath)
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        }

        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }

        if (change != PlayModeStateChange.EnteredPlayMode
            || runnerCreated
            || !File.Exists(RequestPath))
        {
            return;
        }

        runnerCreated = true;
        new GameObject("WIM-054 Captivity Body Damage Runner")
            .AddComponent<WimCaptivityBodyDamageRunner>();
    }
}

public sealed class WimCaptivityBodyDamageRunner : MonoBehaviour
{
    private const float TimeoutSeconds = 20f;
    private readonly List<string> rows = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();

    private DungeonRuntimeLifetimeScope scope;
    private IDungeonGameSaveService saves;
    private ICharacterAiWorldRegistry world;
    private ICharacterBodyHealthQuery bodyHealth;
    private ICharacterBodyHealthCommand bodyCommands;
    private ICaptivityRuntime captivity;
    private ICaptivityCommandService commands;
    private ICaptivityWorkReadinessQuery readiness;
    private CaptivityInteractionRegistry interactions;
    private ICaptivityFeatureSectionPresenter presenter;
    private IWorldItemStackRuntime items;
    private IWorldFilthQuery filth;
    private WorldItemRepository itemRepository;
    private IDungeonItemCatalogProvider itemCatalog;
    private DungeonGameSaveData baseline;
    private string baselineFilthSignature = string.Empty;
    private BuildableObject fixtureHousing;
    private string fixtureHousingId = string.Empty;
    private string captiveId = string.Empty;
    private string wardenId = string.Empty;
    private CharacterActor captive;
    private CharacterActor warden;
    private AbilityWork wardenWork;
    private AbilityWork.DutyState oldDuty;
    private WorkPriorityLevel oldWardenPriority;
    private WorkPriorityLevel oldHaulPriority;
    private bool workSettingsCaptured;
    private string lastInteractionDestinationId = string.Empty;
    private float oldTimeScale;
    private bool oldRunInBackground;
    private bool finishing;

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        Application.logMessageReceived += CaptureConsoleIssue;
        oldTimeScale = Time.timeScale;
        oldRunInBackground = Application.runInBackground;
        Application.runInBackground = true;
        Time.timeScale = 8f;

        yield return null;
        yield return null;
        yield return RunGuarded();
        Finish();
    }

    private IEnumerator RunGuarded()
    {
        IEnumerator scenario = null;
        try
        {
            scenario = RunScenario();
        }
        catch (Exception exception)
        {
            Fail("UNHANDLED_EXCEPTION", exception.ToString());
        }

        if (scenario == null)
        {
            yield break;
        }

        while (true)
        {
            bool moved;
            object current;
            try
            {
                moved = scenario.MoveNext();
                current = moved ? scenario.Current : null;
            }
            catch (Exception exception)
            {
                Fail("UNHANDLED_EXCEPTION", exception.ToString());
                yield break;
            }

            if (!moved)
            {
                yield break;
            }
            yield return current;
        }
    }

    private IEnumerator RunScenario()
    {
        float scopeDeadline = Time.realtimeSinceStartup + TimeoutSeconds;
        while (Time.realtimeSinceStartup < scopeDeadline)
        {
            scope = FindScope();
            if (scope?.Container != null)
            {
                break;
            }
            yield return null;
        }

        if (scope?.Container == null)
        {
            Fail("RUNTIME_SCOPE", "Dungeon runtime scope was unavailable.");
            yield break;
        }

        saves = Resolve<IDungeonGameSaveService>();
        world = Resolve<ICharacterAiWorldRegistry>();
        bodyHealth = Resolve<ICharacterBodyHealthQuery>();
        bodyCommands = Resolve<ICharacterBodyHealthCommand>();
        captivity = Resolve<ICaptivityRuntime>();
        commands = Resolve<ICaptivityCommandService>();
        readiness = Resolve<ICaptivityWorkReadinessQuery>();
        interactions = Resolve<CaptivityInteractionRegistry>();
        presenter = Resolve<ICaptivityFeatureSectionPresenter>();
        items = Resolve<IWorldItemStackRuntime>();
        filth = Resolve<IWorldFilthQuery>();
        itemRepository = Resolve<WorldItemRepository>();
        itemCatalog = Resolve<IDungeonItemCatalogProvider>();
        if (new object[]
            {
                saves,
                world,
                bodyHealth,
                bodyCommands,
                captivity,
                commands,
                readiness,
                interactions,
                presenter,
                items,
                filth,
                itemRepository,
                itemCatalog
            }.Any(value => value == null))
        {
            Fail("RUNTIME_AUTHORITIES", "One or more production authorities were unavailable.");
            yield break;
        }

        CharacterActor[] actors = FindEligibleActors();
        int ownerCount = world.AllCharacters.Count(actor => actor != null && actor.IsOwner);
        if (actors.Length < 2 || ownerCount != 1)
        {
            rows.Add("INFO\tSTART_PARTY_FIXTURE\trequested-fast-debug-commit");
            StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            float deadline = Time.realtimeSinceStartup + 10f;
            do
            {
                yield return null;
                actors = FindEligibleActors();
                ownerCount = world.AllCharacters.Count(actor => actor != null && actor.IsOwner);
            }
            while ((actors.Length < 2 || ownerCount != 1)
                   && Time.realtimeSinceStartup < deadline);
        }

        warden = actors.FirstOrDefault(actor => actor.Brain != null
            && actor.TryGetAbility(out AbilityWork _));
        captive = actors.FirstOrDefault(actor => actor != warden);
        Check(warden != null && captive != null,
            "LIVE_ACTORS",
            $"eligible={actors.Length};warden={ActorId(warden)};captive={ActorId(captive)}");
        if (warden == null || captive == null)
        {
            yield break;
        }

        Check(ownerCount == 1,
            "LIVE_OWNER",
            $"ownerCount={ownerCount}");
        if (ownerCount != 1)
        {
            yield break;
        }

        baseline = saves.Capture();
        baselineFilthSignature = DescribeFilth();

        wardenId = ActorId(warden);
        captiveId = ActorId(captive);
        fixtureHousing = PlaceAuthoredHousing();
        Check(fixtureHousing != null,
            "AUTHORED_HOUSING_FIXTURE",
            fixtureHousing != null
                ? $"building={fixtureHousingId};position={fixtureHousing.centerPos};asset={fixtureHousing.BuildingData.name}"
                : "No legal authored CP01 placement was found.");
        if (fixtureHousing == null)
        {
            yield break;
        }

        DungeonGameSaveData fixtureSave = saves.Capture();
        Check(PublishConfinedCaptive(fixtureSave, out string restoreDetail),
            "CAPTIVE_FIXTURE_PUBLICATION",
            restoreDetail);
        if (failures.Count > 0)
        {
            yield break;
        }
        yield return null;

        if (!ReacquireFixture())
        {
            Fail("FIXTURE_REACQUIRE", $"warden={wardenId};captive={captiveId};housing={fixtureHousingId}");
            yield break;
        }

        CheckPolicyMetadata();
        CheckCurrentFormatSaveContract();
        if (failures.Count > 0)
        {
            yield break;
        }

        wardenWork = warden.GetComponent<AbilityWork>();
        oldDuty = wardenWork.CurrentDutyState;
        oldWardenPriority = wardenWork.WorkPriorities.GetPriority(
            BuiltInWorkTypeIds.Warden);
        oldHaulPriority = wardenWork.WorkPriorities.GetPriority(
            BuiltInWorkTypeIds.Haul);
        workSettingsCaptured = true;
        wardenWork.SetDutyState(AbilityWork.DutyState.OnDuty);
        wardenWork.SetWorkPriority(
            BuiltInWorkTypeIds.Warden,
            WorkPriorityLevel.Off);
        wardenWork.SetWorkPriority(
            BuiltInWorkTypeIds.Haul,
            WorkPriorityLevel.Priority1);

        CharacterVitalsSnapshot initial = bodyHealth.GetVitals(captive);
        bodyCommands.HealLegacyVitals(captive, initial.MaximumHealth);
        initial = bodyHealth.GetVitals(captive);
        float expectedCoercionDamage = initial.MaximumHealth * 0.06f;
        yield return CompleteInteraction(
            "captivity:coercion",
            StockCategory.General,
            "NONLETHAL_COERCION");
        if (failures.Count > 0)
        {
            yield break;
        }

        CharacterVitalsSnapshot afterCoercion = bodyHealth.GetVitals(captive);
        CheckApprox(
            afterCoercion.CurrentHealth,
            initial.CurrentHealth - expectedCoercionDamage,
            "NONLETHAL_MAX_HP_PERCENT_DAMAGE");
        bool hasNonlethalState = captivity.TryGetCaptive(
            captiveId,
            out CaptiveState nonlethalState);
        Check(!captive.IsDead
                && hasNonlethalState
                && nonlethalState.status == CaptivityStatus.Confined,
            "NONLETHAL_RETURNS_CONFINED",
            $"dead={captive.IsDead};status={nonlethalState?.status}");

        yield return null;
        yield return null;
        CheckApprox(
            bodyHealth.GetVitals(captive).CurrentHealth,
            afterCoercion.CurrentHealth,
            "NEXT_TICK_BODY_DAMAGE_PERSISTS");

        IGridSystemProvider gridProvider = Resolve<IGridSystemProvider>();
        Grid grid = null;
        bool hasGrid = gridProvider != null
            && gridProvider.TryGetGrid(out grid)
            && grid != null;
        Check(hasGrid,
            "WORLD_FILTH_POSITIVE_FIXTURE_GRID",
            $"provider={gridProvider != null};grid={grid != null}");
        if (!hasGrid)
        {
            yield break;
        }
        GridCell filthFixtureCell = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.DungeonInterior
                && grid.IsWalkable(cell.Position)
                && filth.GetAt(cell.Position).Count == 0)
            .OrderBy(cell => Mathf.Abs(cell.Position.x - warden.GetNowXY().x)
                + Mathf.Abs(cell.Position.y - warden.GetNowXY().y))
            .ThenBy(cell => cell.Position.y)
            .ThenBy(cell => cell.Position.x)
            .FirstOrDefault();
        Check(filthFixtureCell != null,
            "WORLD_FILTH_POSITIVE_FIXTURE_CELL",
            $"gridCells={grid.GetCells().Count()};existingFilth={filth.GetAll().Count};warden={warden.GetNowXY()}");
        if (filthFixtureCell == null)
        {
            yield break;
        }
        const float authoredFilthAmount = 7f;
        WorldFilthSnapshot authoredFilth = filth.AddFilth(
            WorldFilthType.Stain,
            filthFixtureCell.Position,
            authoredFilthAmount,
            wardenId,
            infectionRisk: 0.25f);
        WorldFilthSnapshot[] authoredFilthAtCell = filth
            .GetAt(filthFixtureCell.Position)
            .ToArray();
        WorldFilthWorkTarget[] authoredTargets = world.Buildings
            .Where(building => building != null && !building.isDestroy)
            .OfType<WorldFilthWorkTarget>()
            .Where(target => target.centerPos == filthFixtureCell.Position)
            .ToArray();
        Check(!string.IsNullOrEmpty(authoredFilth.FilthId)
                && Mathf.Abs(authoredFilth.Amount - authoredFilthAmount) <= 0.001f
                && authoredFilthAtCell.Length == 1
                && string.Equals(
                    authoredFilthAtCell[0].FilthId,
                    authoredFilth.FilthId,
                    StringComparison.Ordinal)
                && authoredTargets.Length == 1,
            "WORLD_FILTH_POSITIVE_FIXTURE",
            $"id={authoredFilth.FilthId};position={filthFixtureCell.Position};amount={authoredFilth.Amount:R};atCell={authoredFilthAtCell.Length};targets={authoredTargets.Length};targetIds={string.Join(",", authoredTargets.Select(target => target.PersistentInstanceId.Value))}");
        if (failures.Count > 0)
        {
            yield break;
        }

        string filthBeforeRestore = DescribeFilth();
        float filthAmountBeforeRestore = filth.GetAll().Sum(entry => entry.Amount);
        DungeonGameSaveData nonlethalSave = saves.Capture();
        bool nonlethalRestored = saves.TryRestore(
            nonlethalSave,
            out DungeonGameRestoreReport nonlethalRestoreReport);
        Check(nonlethalRestored,
            "NONLETHAL_SAVE_RESTORE",
            nonlethalRestored
                ? "restore=accepted"
                : JoinErrors(nonlethalRestoreReport));
        if (!nonlethalRestored)
        {
            yield break;
        }
        string filthAfterRestore = DescribeFilth();
        float filthAmountAfterRestore = filth.GetAll().Sum(entry => entry.Amount);
        WorldFilthSnapshot restoredAuthoredFilth = filth.GetAll()
            .SingleOrDefault(entry => string.Equals(
                entry.FilthId,
                authoredFilth.FilthId,
                StringComparison.Ordinal));
        BuildableObject[] activeBuildings = world.Buildings
            .Where(building => building != null && !building.isDestroy)
            .ToArray();
        bool activeRegistryIdsUnique = activeBuildings
            .Where(building => building.PersistentInstanceId.IsValid)
            .GroupBy(
                building => building.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .All(group => group.Count() == 1);
        Vector2Int[] requiredFilthPositions = filth.GetAll()
            .Select(entry => entry.Position)
            .Distinct()
            .ToArray();
        WorldFilthWorkTarget[] activeFilthTargets = activeBuildings
            .OfType<WorldFilthWorkTarget>()
            .ToArray();
        bool exactCleaningTargets = requiredFilthPositions.All(position =>
            activeFilthTargets.Count(target =>
                target.centerPos == position) == 1);
        string duplicateRegistryIds = string.Join(
            ",",
            activeBuildings
                .Where(building => building.PersistentInstanceId.IsValid)
                .GroupBy(
                    building => building.PersistentInstanceId.Value,
                    StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key + "x" + group.Count()));
        Check(string.Equals(
                    filthBeforeRestore,
                    filthAfterRestore,
                    StringComparison.Ordinal)
                && activeRegistryIdsUnique
                && requiredFilthPositions.Length > 0
                && exactCleaningTargets
                && activeFilthTargets.Length
                    == requiredFilthPositions.Length
                && !string.IsNullOrEmpty(restoredAuthoredFilth.FilthId)
                && Mathf.Abs(
                    restoredAuthoredFilth.Amount - authoredFilthAmount)
                    <= 0.001f
                && Mathf.Abs(
                    filthAmountAfterRestore - filthAmountBeforeRestore)
                    <= 0.001f,
            "WORLD_FILTH_SAME_FRAME_REPROJECTION_LIFETIME",
            $"filthExact={string.Equals(filthBeforeRestore, filthAfterRestore, StringComparison.Ordinal)};fixtureId={authoredFilth.FilthId};fixturePosition={filthFixtureCell.Position};fixtureAmount={authoredFilthAmount:R}->{restoredAuthoredFilth.Amount:R};totalAmount={filthAmountBeforeRestore:R}->{filthAmountAfterRestore:R};activeRegistryIdsUnique={activeRegistryIdsUnique};duplicateRegistryIds=[{duplicateRegistryIds}];requiredTargets={requiredFilthPositions.Length};activeTargets={activeFilthTargets.Length};targetIds=[{string.Join(",", activeFilthTargets.Select(target => target.PersistentInstanceId.Value + "@" + target.centerPos))}]" );
        if (failures.Count > 0 || !ReacquireFixture())
        {
            yield break;
        }
        CheckApprox(
            bodyHealth.GetVitals(captive).CurrentHealth,
            afterCoercion.CurrentHealth,
            "RESTORE_DOES_NOT_REPEAT_DAMAGE");

        float beforeMedicalRecovery = bodyHealth.GetVitals(captive).CurrentHealth;
        bodyCommands.HealLegacyVitals(captive, 4f);
        float afterMedicalRecovery = bodyHealth.GetVitals(captive).CurrentHealth;
        Check(afterMedicalRecovery > beforeMedicalRecovery,
            "COMMON_BODY_HEAL_COMMAND_VISIBLE",
            $"scope=body-command-not-medicine-workflow;health={beforeMedicalRecovery:0.###}->{afterMedicalRecovery:0.###}");

        CharacterVitalsSnapshot beforeLethalSetup = bodyHealth.GetVitals(captive);
        float lethalCurrent = Mathf.Max(1f, beforeLethalSetup.MaximumHealth * 0.10f);
        bodyCommands.RestoreLegacyVitalsProjection(
            captive,
            beforeLethalSetup.MaximumHealth,
            lethalCurrent,
            injurySeverity: 0f);
        CharacterVitalsSnapshot lethalVitals = bodyHealth.GetVitals(captive);
        int outputBefore = CountItem(CaptivityItemDefinitions.ExtractedBloodItemId);
        CaptiveState stateBeforeUi = captivity.Captives.Single(state =>
            string.Equals(state.captiveId, captiveId, StringComparison.Ordinal));
        string stateJsonBeforeUi = JsonUtility.ToJson(stateBeforeUi);
        ProbeFeatureSurfaceView firstView = new();
        presenter.Present(firstView);
        ProbeFeatureSurfaceView secondView = new();
        presenter.Present(secondView);
        string lethalCard = firstView.FindDetail(
            "Captivity_Interaction_captivity:blood-extraction");
        string expectedLethalProjection =
            $"{lethalVitals.CurrentHealth:0.#}→0/{lethalVitals.MaximumHealth:0.#}";
        Check(lethalCard.Contains("치명적 위험", StringComparison.Ordinal)
                && lethalCard.Contains("18%", StringComparison.Ordinal)
                && lethalCard.Contains("사망", StringComparison.Ordinal)
                && lethalCard.Contains(
                    expectedLethalProjection,
                    StringComparison.Ordinal),
            "PREACTION_LETHAL_WARNING",
            lethalCard);
        Check(string.Equals(
                    lethalCard,
                    secondView.FindDetail(
                        "Captivity_Interaction_captivity:blood-extraction"),
                    StringComparison.Ordinal)
                && Mathf.Approximately(
                    bodyHealth.GetVitals(captive).CurrentHealth,
                    lethalVitals.CurrentHealth)
                && CountItem(CaptivityItemDefinitions.ExtractedBloodItemId)
                    == outputBefore
                && string.Equals(
                    JsonUtility.ToJson(captivity.Captives.Single(state =>
                        string.Equals(
                            state.captiveId,
                            captiveId,
                            StringComparison.Ordinal))),
                    stateJsonBeforeUi,
                    StringComparison.Ordinal),
            "UI_REOPEN_NO_MUTATION_OR_EXECUTE",
            $"health={bodyHealth.GetVitals(captive).CurrentHealth:0.###};output={outputBefore};cards={firstView.CardCount}/{secondView.CardCount}");

        yield return CompleteInteractionThroughRenderedUi(
            "captivity:blood-extraction",
            StockCategory.Medicine,
            expectedLethalProjection,
            "LETHAL_BLOOD_EXTRACTION");
        if (failures.Count > 0)
        {
            yield break;
        }

        int outputAfter = CountItem(CaptivityItemDefinitions.ExtractedBloodItemId);
        string fatalDestinationId = lastInteractionDestinationId;
        bool hasDeadState = captivity.TryGetCaptive(captiveId, out CaptiveState deadState);
        CharacterVitalsSnapshot fatalVitals = bodyHealth.GetVitals(captiveId);
        bool actorDeadOrRetired = captive == null || captive.IsDead;
        Check(actorDeadOrRetired
                && fatalVitals.IsDead
                && hasDeadState
                && deadState.status == CaptivityStatus.Dead,
            "LETHAL_DAMAGE_REACHES_DEAD",
            $"actorDeadOrRetired={actorDeadOrRetired};vitals={Describe(fatalVitals)};status={deadState?.status}");
        Check(outputAfter == outputBefore + 1,
            "FATAL_EXTRACTION_OUTPUT_EXACTLY_ONCE",
            $"item={CaptivityItemDefinitions.ExtractedBloodItemId};count={outputBefore}->{outputAfter}");
        Check(deadState != null
                && !string.IsNullOrWhiteSpace(fatalDestinationId)
                && string.IsNullOrWhiteSpace(deadState.reservedWardenId)
                && string.IsNullOrWhiteSpace(deadState.currentInteractionId)
                && string.IsNullOrWhiteSpace(deadState.interactionMaterialDestinationId)
                && !deadState.interactionMaterialsConsumed
                && !items.GetAllStacks().Any(stack => stack != null
                    && string.Equals(
                        stack.DestinationId,
                        fatalDestinationId,
                        StringComparison.Ordinal)),
            "FATAL_INTERACTION_OWNERSHIP_CLEANUP",
            deadState == null
                ? "state=missing"
                : $"warden={deadState.reservedWardenId};interaction={deadState.currentInteractionId};destination={deadState.interactionMaterialDestinationId};consumed={deadState.interactionMaterialsConsumed}");

        bool retryStarted = commands.TryStartInteraction(
            captiveId,
            "captivity:blood-extraction",
            warden,
            fixtureHousing,
            out string retryReason);
        bool retryAdvanced = commands.AdvanceInteraction(
            captiveId,
            warden,
            1000f,
            out string retryStatus);
        yield return null;
        Check(!retryStarted
                && !retryAdvanced
                && CountItem(CaptivityItemDefinitions.ExtractedBloodItemId)
                    == outputAfter,
            "TERMINAL_RETRY_REJECTED_NO_REPLAY",
            $"start={retryStarted}:{retryReason};advance={retryAdvanced}:{retryStatus};output={outputAfter}");

        DungeonGameSaveData fatalSave = saves.Capture();
        bool fatalRestored = saves.TryRestore(
            fatalSave,
            out DungeonGameRestoreReport fatalRestoreReport);
        Check(fatalRestored,
            "FATAL_SAVE_RESTORE",
            fatalRestored ? "restore=accepted" : JoinErrors(fatalRestoreReport));
        if (!fatalRestored)
        {
            yield break;
        }
        yield return null;
        ReacquireFixture(allowDeadCaptive: true);
        bool hasRestoredDead = captivity.TryGetCaptive(
            captiveId,
            out CaptiveState restoredDead);
        Check(hasRestoredDead
                && restoredDead.status == CaptivityStatus.Dead
                && CountItem(CaptivityItemDefinitions.ExtractedBloodItemId)
                    == outputAfter,
            "FATAL_RESTORE_NO_DAMAGE_OR_OUTPUT_REPLAY",
            $"status={restoredDead?.status};output={CountItem(CaptivityItemDefinitions.ExtractedBloodItemId)}");
    }

    private IEnumerator CompleteInteraction(
        string interactionId,
        StockCategory inputCategory,
        string checkPrefix)
    {
        if (!interactions.TryGet(
                interactionId,
                out ICaptivityInteractionHandler handler))
        {
            Fail(checkPrefix + "_HANDLER", "Handler was not registered.");
            yield break;
        }

        DungeonItemDefinition input = itemCatalog.All
            .Where(definition => definition != null
                && definition.StockCategory == inputCategory
                && definition.MaxStack > 1
                && string.IsNullOrWhiteSpace(definition.EquipmentId))
            .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (input == null)
        {
            Fail(checkPrefix + "_INPUT", $"No authored stackable {inputCategory} item exists.");
            yield break;
        }

        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            itemRepository,
            input.ItemId,
            1,
            WorldItemStackState.Loose,
            position: warden.GetNowXY());
        Check(!string.IsNullOrWhiteSpace(stackId),
            checkPrefix + "_PHYSICAL_INPUT",
            $"item={input.ItemId};stack={stackId};position={warden.GetNowXY()}");
        if (string.IsNullOrWhiteSpace(stackId))
        {
            yield break;
        }

        bool started = commands.TryStartInteraction(
            captiveId,
            interactionId,
            warden,
            fixtureHousing,
            out string startReason);
        Check(started,
            checkPrefix + "_START",
            $"started={started};reason={startReason}");
        if (!started)
        {
            yield break;
        }

        if (captivity.TryGetCaptive(captiveId, out CaptiveState startedState))
        {
            lastInteractionDestinationId =
                startedState.interactionMaterialDestinationId;
        }

        float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
        CharacterAiDecisionTickResult lastDecision = default;
        int decisionCount = 0;
        while (Time.realtimeSinceStartup < deadline
            && !readiness.IsInteractionReady(captiveId, out _))
        {
            if ((warden.Brain.bestAction == null || warden.Brain.isBestActionEnd)
                && (decisionCount & 3) == 0)
            {
                lastDecision = warden.Brain.RunDecisionTreeDirect();
            }
            decisionCount++;
            yield return null;
        }

        bool ready = readiness.IsInteractionReady(
            captiveId,
            out string readinessReason);
        Check(ready,
            checkPrefix + "_PHYSICAL_INPUT_DELIVERED",
            $"ready={ready};reason={readinessReason};decisions={decisionCount};last={lastDecision.Status};action={warden.Brain.bestAction?.actionset?.Branch};phase={warden.Brain.CurrentActionPhase};failure={warden.Brain.LastActionFailure}");
        if (!ready)
        {
            yield break;
        }

        warden.SetAiPaused(true);
        bool completed = commands.AdvanceInteraction(
            captiveId,
            warden,
            handler.RequiredWork,
            out string completionStatus);
        if (!warden.IsDead)
        {
            warden.SetAiPaused(false);
        }
        Check(completed,
            checkPrefix + "_COMPLETE",
            $"completed={completed};status={completionStatus};work={handler.RequiredWork:0.###}");
    }

    private IEnumerator CompleteInteractionThroughRenderedUi(
        string interactionId,
        StockCategory inputCategory,
        string expectedProjection,
        string checkPrefix)
    {
        if (!interactions.TryGet(
                interactionId,
                out ICaptivityInteractionHandler handler))
        {
            Fail(checkPrefix + "_HANDLER", "Handler was not registered.");
            yield break;
        }

        DungeonItemDefinition input = itemCatalog.All
            .Where(definition => definition != null
                && definition.StockCategory == inputCategory
                && definition.MaxStack > 1
                && string.IsNullOrWhiteSpace(definition.EquipmentId))
            .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (input == null)
        {
            Fail(checkPrefix + "_INPUT", $"No authored stackable {inputCategory} item exists.");
            yield break;
        }

        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            itemRepository,
            input.ItemId,
            1,
            WorldItemStackState.Loose,
            position: warden.GetNowXY());
        Check(!string.IsNullOrWhiteSpace(stackId),
            checkPrefix + "_PHYSICAL_INPUT",
            $"item={input.ItemId};stack={stackId};position={warden.GetNowXY()}");
        if (string.IsNullOrWhiteSpace(stackId))
        {
            yield break;
        }

        UITabManager tabManager = UnityEngine.Object.FindFirstObjectByType<UITabManager>(
            FindObjectsInactive.Include);
        Check(tabManager != null,
            checkPrefix + "_OPERATIONS_TAB_MANAGER",
            $"manager={tabManager != null}");
        if (tabManager == null)
        {
            yield break;
        }

        tabManager.ToggleTopTab(TabId.Operations);
        yield return null;
        Canvas.ForceUpdateCanvases();

        P0FeatureSurfacePanel panel = UnityEngine.Object
            .FindObjectsByType<P0FeatureSurfacePanel>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null
                && candidate.gameObject.activeInHierarchy
                && candidate.TryGetComponent(out UITabIdentity identity)
                && identity.Id == TabId.Operations);
        string buttonName = $"Captivity_Interaction_{interactionId}";
        Button button = panel != null
            ? panel.GetComponentsInChildren<Button>(includeInactive: false)
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.name,
                        buttonName,
                        StringComparison.Ordinal))
            : null;
        Transform card = button != null ? button.transform.parent : null;
        string renderedText = card != null
            ? string.Join(" | ", card.GetComponentsInChildren<TMP_Text>(true)
                .Where(text => text != null)
                .Select(text => text.text))
            : string.Empty;
        bool rendered = panel != null
            && button != null
            && button.gameObject.activeInHierarchy
            && button.interactable
            && renderedText.Contains("치명적 위험", StringComparison.Ordinal)
            && renderedText.Contains("18%", StringComparison.Ordinal)
            && renderedText.Contains(expectedProjection, StringComparison.Ordinal)
            && renderedText.Contains("사망", StringComparison.Ordinal);
        Check(rendered,
            checkPrefix + "_RENDERED_DANGER_CARD",
            $"panel={panel != null};button={button != null};active={button?.gameObject.activeInHierarchy};interactable={button?.interactable};text={renderedText}");
        if (!rendered)
        {
            yield break;
        }

        ScrollContainingButton(button);
        yield return null;
        bool clicked = ClickThroughEventSystem(button, out string pointerDetail);
        yield return null;

        bool hasStartedState = captivity.TryGetCaptive(
            captiveId,
            out CaptiveState startedState);
        CharacterActor assignedWarden = hasStartedState
            ? world.AllCharacters.FirstOrDefault(actor => actor != null
                && string.Equals(
                    ActorId(actor),
                    startedState.reservedWardenId,
                    StringComparison.Ordinal))
            : null;
        bool started = clicked
            && hasStartedState
            && startedState.status == CaptivityStatus.Interaction
            && string.Equals(
                startedState.currentInteractionId,
                interactionId,
                StringComparison.Ordinal)
            && assignedWarden != null;
        Check(started,
            checkPrefix + "_EVENTSYSTEM_START",
            $"clicked={clicked};pointer={pointerDetail};status={startedState?.status};interaction={startedState?.currentInteractionId};warden={startedState?.reservedWardenId}");
        if (!started)
        {
            yield break;
        }
        lastInteractionDestinationId =
            startedState.interactionMaterialDestinationId;

        float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
        CharacterAiDecisionTickResult lastDecision = default;
        int decisionCount = 0;
        while (Time.realtimeSinceStartup < deadline
            && !readiness.IsInteractionReady(captiveId, out _))
        {
            if ((warden.Brain.bestAction == null || warden.Brain.isBestActionEnd)
                && (decisionCount & 3) == 0)
            {
                lastDecision = warden.Brain.RunDecisionTreeDirect();
            }
            decisionCount++;
            yield return null;
        }

        bool ready = readiness.IsInteractionReady(
            captiveId,
            out string readinessReason);
        Check(ready,
            checkPrefix + "_PHYSICAL_INPUT_DELIVERED",
            $"ready={ready};reason={readinessReason};decisions={decisionCount};last={lastDecision.Status};action={warden.Brain.bestAction?.actionset?.Branch};phase={warden.Brain.CurrentActionPhase};failure={warden.Brain.LastActionFailure}");
        if (!ready)
        {
            yield break;
        }

        assignedWarden.SetAiPaused(true);
        bool completed = commands.AdvanceInteraction(
            captiveId,
            assignedWarden,
            handler.RequiredWork,
            out string completionStatus);
        if (!assignedWarden.IsDead)
        {
            assignedWarden.SetAiPaused(false);
        }
        Check(completed,
            checkPrefix + "_COMPLETE",
            $"completed={completed};status={completionStatus};work={handler.RequiredWork:0.###};warden={ActorId(assignedWarden)}");
    }

    private void CheckPolicyMetadata()
    {
        Dictionary<string, float> expected = new(StringComparer.Ordinal)
        {
            ["captivity:coercion"] = 6f,
            ["captivity:branding"] = 8f,
            ["captivity:blood-extraction"] = 18f,
            ["captivity:memory-extraction"] = 10f,
            ["captivity:forced-modification"] = 16f,
            ["captivity:corruption-ritual"] = 10f
        };
        bool exact = interactions.All.All(handler =>
            expected.TryGetValue(handler.InteractionId, out float percent)
                ? Mathf.Approximately(
                    handler.BodyDamage.PercentOfMaximumHealth,
                    percent)
                : !handler.BodyDamage.HasDamage);
        Check(exact && interactions.All.Count == 10,
            "IMMUTABLE_DAMAGE_POLICY_MATRIX",
            string.Join(",", interactions.All
                .OrderBy(handler => handler.InteractionId, StringComparer.Ordinal)
                .Select(handler =>
                    $"{handler.InteractionId}={handler.BodyDamage.PercentOfMaximumHealth:0.#}%")));

        CaptivityBodyDamageProjection projection =
            interactions.All.Single(handler => string.Equals(
                    handler.InteractionId,
                    "captivity:blood-extraction",
                    StringComparison.Ordinal))
                .BodyDamage.Project(10f, 100f);
        Check(projection.IsLethal
                && Mathf.Approximately(projection.DamageAmount, 18f)
                && Mathf.Approximately(projection.ExpectedHealth, 0f),
            "SHARED_DAMAGE_PROJECTION",
            $"percent={projection.PercentOfMaximumHealth};damage={projection.DamageAmount};expected={projection.ExpectedHealth};lethal={projection.IsLethal}");
    }

    private void CheckCurrentFormatSaveContract()
    {
        CaptivitySaveData current = (captivity as ICaptivityPersistence)?.Capture();
        string json = JsonUtility.ToJson(current);
        CaptivitySaveData old = JsonUtility.FromJson<CaptivitySaveData>(json);
        old.version = 3;
        DungeonGameRestoreReport oldReport = new();
        CaptivitySaveValidation.Validate(old, oldReport);
        Check(current?.version == 4
                && !json.Contains("\"health\"", StringComparison.Ordinal)
                && !oldReport.Success,
            "CURRENT_FORMAT_BODY_AUTHORITY_ONLY",
            $"version={current?.version};hasHealthField={json.Contains("\"health\"", StringComparison.Ordinal)};oldAccepted={oldReport.Success};errors={JoinErrors(oldReport)}");
    }

    private bool PublishConfinedCaptive(
        DungeonGameSaveData fixtureSave,
        out string detail)
    {
        detail = string.Empty;
        DungeonSaveSectionEnvelope envelope = fixtureSave?.sections?.SingleOrDefault(
            section => string.Equals(
                section.sectionId,
                CaptivitySaveSection.Id,
                StringComparison.Ordinal));
        if (envelope == null)
        {
            detail = "captivity-envelope-missing";
            return false;
        }

        CaptivitySaveData payload = JsonUtility.FromJson<CaptivitySaveData>(
            envelope.payloadJson) ?? new CaptivitySaveData();
        payload.version = CaptivitySaveData.CurrentVersion;
        payload.captureSequence = Mathf.Max(payload.captureSequence, 1);
        payload.captives = new List<CaptiveState>
        {
            new()
            {
                captiveId = captiveId,
                displayName = "!WIM054 신체 피해 포로",
                speciesTag = captive.SpeciesTag,
                status = CaptivityStatus.Confined,
                policyId = CaptivityPolicyIds.Standard,
                housingBuildingId = fixtureHousingId,
                housingPosition = fixtureHousing.centerPos,
                capturePosition = captive.GetNowXY(),
                will = 100f,
                fear = 0f,
                trust = 0f,
                grudge = 0f,
                corruption = 0f,
                compliance = 0f,
                escapeRisk = 0f,
                nextSecurityCheckAt = float.MaxValue,
                capturedAbsoluteDay = 1,
                lastResult = "WIM054 검증 수용"
            }
        };
        if (payload.policies == null
            || !payload.policies.Any(policy => policy != null
                && string.Equals(
                    policy.policyId,
                    CaptivityPolicyIds.Standard,
                    StringComparison.Ordinal)))
        {
            payload.policies ??= new List<CaptivePolicyData>();
            payload.policies.Add(new CaptivePolicyData
            {
                policyId = CaptivityPolicyIds.Standard,
                displayName = "표준 수용"
            });
        }

        envelope.sectionVersion = CaptivitySaveData.CurrentVersion;
        envelope.payloadJson = JsonUtility.ToJson(payload);
        DungeonSaveManifestSectionData manifest = fixtureSave.manifest?.sections
            ?.SingleOrDefault(section => string.Equals(
                section.sectionId,
                CaptivitySaveSection.Id,
                StringComparison.Ordinal));
        if (manifest != null)
        {
            manifest.sectionVersion = CaptivitySaveData.CurrentVersion;
        }

        bool restored = saves.TryRestore(
            fixtureSave,
            out DungeonGameRestoreReport report);
        detail = restored
            ? $"actor={captiveId};housing={fixtureHousingId};sectionVersion={envelope.sectionVersion}"
            : JoinErrors(report);
        return restored;
    }

    private BuildableObject PlaceAuthoredHousing()
    {
        if (!world.TryGetGrid(out Grid grid) || grid == null)
        {
            return null;
        }
        BuildingSO data = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Captivity/CP01_감방구속대.asset");
        if (data?.GetCaptiveHousingAbility()?.IsValid != true)
        {
            return null;
        }

        Vector2Int origin = warden.GetNowXY();
        HashSet<Vector2Int> actorCells = world.AllCharacters
            .Where(actor => actor != null)
            .Select(actor => actor.GetNowXY())
            .ToHashSet();
        Vector2Int? placement = null;
        int bestDistance = int.MaxValue;
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                Vector2Int candidate = new(x, y);
                IReadOnlyList<Vector2Int> footprint = data.GetGridPosList(candidate);
                if (footprint.Count == 0
                    || footprint.Any(actorCells.Contains)
                    || footprint.Any(position => !grid.IsValidGridPos(position)
                        || grid.GetGridCell(position)?.AreaType
                            != GridCellAreaType.DungeonInterior
                        || grid.GetGridCell(position)?.CanOccupy(
                            data.Placement.Layer) != true))
                {
                    continue;
                }

                HashSet<Vector2Int> footprintSet = footprint.ToHashSet();
                bool hasApproach = footprint.Any(position => new[]
                    {
                        Vector2Int.up,
                        Vector2Int.down,
                        Vector2Int.left,
                        Vector2Int.right
                    }.Any(direction =>
                    {
                        Vector2Int stand = position + direction;
                        return grid.IsValidGridPos(stand)
                            && !footprintSet.Contains(stand)
                            && grid.IsWalkable(stand);
                    }));
                if (!hasApproach)
                {
                    continue;
                }

                int distance = Mathf.Abs(candidate.x - origin.x)
                    + Mathf.Abs(candidate.y - origin.y);
                if (distance < bestDistance)
                {
                    placement = candidate;
                    bestDistance = distance;
                }
            }
        }
        if (!placement.HasValue)
        {
            return null;
        }

        GridBuildingFactory factory = new(
            created => scope.Container.InjectGameObject(created.gameObject));
        BuildableObject building = factory.Create(grid, data, placement.Value);
        if (building == null)
        {
            return null;
        }
        building.SetGrid(grid);
        building.Initialization(data, placement.Value);
        IReadOnlyList<Vector2Int> positions = data.GetGridPosList(placement.Value);
        if (!grid.RegisterOccupant(
                building,
                data.Placement.Layer,
                positions,
                data.Placement.IsMovement))
        {
            Destroy(building.gameObject);
            return null;
        }
        world.RegisterBuilding(building);
        fixtureHousingId = building.RequirePersistentInstanceId().Value;
        return building;
    }

    private bool ReacquireFixture(bool allowDeadCaptive = false)
    {
        warden = world.AllCharacters.FirstOrDefault(actor => actor != null
            && string.Equals(ActorId(actor), wardenId, StringComparison.Ordinal));
        captive = world.AllCharacters.FirstOrDefault(actor => actor != null
            && string.Equals(ActorId(actor), captiveId, StringComparison.Ordinal));
        fixtureHousing = world.Buildings.FirstOrDefault(building => building != null
            && !building.isDestroy
            && string.Equals(
                building.PersistentInstanceId.Value,
                fixtureHousingId,
                StringComparison.Ordinal));
        if (workSettingsCaptured && warden != null)
        {
            wardenWork = warden.GetComponent<AbilityWork>();
        }
        return warden != null
            && captive != null
            && fixtureHousing != null
            && (allowDeadCaptive || !captive.IsDead);
    }

    private CharacterActor[] FindEligibleActors() => world.AllCharacters
        .Where(actor => actor != null
            && !actor.IsDead
            && actor.characterType == CharacterType.NPC
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
        .OrderBy(actor => ActorId(actor), StringComparer.Ordinal)
        .ToArray();

    private int CountItem(string itemId) => items.GetAllStacks()
        .Where(stack => stack != null
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
        .Sum(stack => Mathf.Max(0, stack.Quantity));

    private string DescribeFilth() => string.Join(
        "|",
        filth.GetAll()
            .OrderBy(entry => entry.FilthId, StringComparer.Ordinal)
            .Select(entry =>
                entry.FilthId
                + ":"
                + entry.Type
                + ":"
                + entry.Amount.ToString("R")
                + "@"
                + entry.Position
                + ":source="
                + entry.SourceCharacterId
                + ":risk="
                + entry.InfectionRisk.ToString("R")
                + ":wall="
                + entry.WallStain));

    private static void ScrollContainingButton(Button button)
    {
        ScrollRect scroll = button != null
            ? button.GetComponentInParent<ScrollRect>()
            : null;
        RectTransform target = button != null
            ? button.transform as RectTransform
            : null;
        if (scroll == null
            || scroll.content == null
            || scroll.viewport == null
            || target == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        Canvas.ForceUpdateCanvases();
        float overflow = Mathf.Max(
            0f,
            scroll.content.rect.height - scroll.viewport.rect.height);
        Bounds targetBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            scroll.content,
            target);
        if (overflow > 0.1f)
        {
            float distanceFromTop =
                scroll.content.rect.yMax - targetBounds.center.y;
            float desiredOffset = Mathf.Clamp(
                distanceFromTop - scroll.viewport.rect.height * 0.5f,
                0f,
                overflow);
            scroll.verticalNormalizedPosition =
                1f - desiredOffset / overflow;
        }
        Canvas.ForceUpdateCanvases();
    }

    private static bool ClickThroughEventSystem(
        Button button,
        out string detail)
    {
        detail = string.Empty;
        EventSystem eventSystem = EventSystem.current;
        RectTransform rect = button != null
            ? button.transform as RectTransform
            : null;
        if (button == null
            || rect == null
            || eventSystem == null
            || !button.gameObject.activeInHierarchy
            || !button.interactable)
        {
            detail = $"button={button != null};rect={rect != null};eventSystem={eventSystem != null};active={button?.gameObject.activeInHierarchy};interactable={button?.interactable}";
            return false;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            null,
            rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = screenPoint
        };
        List<RaycastResult> hits = new();
        eventSystem.RaycastAll(pointer, hits);
        GameObject target = hits
            .Select(hit => ExecuteEvents.GetEventHandler<IPointerClickHandler>(
                hit.gameObject))
            .FirstOrDefault(candidate => candidate != null);
        bool routedToButton = target == button.gameObject;
        detail = $"point={screenPoint.x:0.#},{screenPoint.y:0.#};hits={hits.Count};target={target?.name};expected={button.name}";
        if (!routedToButton)
        {
            return false;
        }

        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerUpHandler);
        return ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerClickHandler);
    }

    private void Finish()
    {
        if (finishing)
        {
            return;
        }
        finishing = true;
        Application.logMessageReceived -= CaptureConsoleIssue;

        if (workSettingsCaptured && wardenWork != null && !warden.IsDead)
        {
            wardenWork.SetDutyState(oldDuty);
            wardenWork.SetWorkPriority(
                BuiltInWorkTypeIds.Warden,
                oldWardenPriority);
            wardenWork.SetWorkPriority(
                BuiltInWorkTypeIds.Haul,
                oldHaulPriority);
        }

        DungeonGameRestoreReport cleanupReport = null;
        bool restored = baseline != null
            && saves != null
            && saves.TryRestore(baseline, out cleanupReport);
        Check(restored,
            "BASELINE_RESTORE",
            restored ? "restore=accepted" : JoinErrors(cleanupReport));
        if (restored)
        {
            DungeonGameSaveData after = saves.Capture();
            bool sectionsExact = new[]
                {
                    CaptivitySaveSection.Id,
                    CharacterBodyHealthSaveSection.Id,
                    PhysicalItemsSaveSection.Id,
                    DarkSurvivalSaveSection.Id
                }
                .All(sectionId => string.Equals(
                    FindPayload(baseline, sectionId),
                    FindPayload(after, sectionId),
                    StringComparison.Ordinal));
            bool fixtureRemoved = world.Buildings.All(building => building == null
                || building.isDestroy
                || !string.Equals(
                    building.PersistentInstanceId.Value,
                    fixtureHousingId,
                    StringComparison.Ordinal));
            bool filthExact = string.Equals(
                baselineFilthSignature,
                DescribeFilth(),
                StringComparison.Ordinal);
            Check(sectionsExact && fixtureRemoved && filthExact,
                "CLEANUP_EXACT_AUTHORITIES",
                $"sectionsExact={sectionsExact};filthExact={filthExact};baselineFilth=[{baselineFilthSignature}];actualFilth=[{DescribeFilth()}];fixtureRemoved={fixtureRemoved};housing={fixtureHousingId}");
        }
        else
        {
            DestroyFixtureHousingFallback();
        }

        bool passed = failures.Count == 0 && consoleIssues.Count == 0;
        rows.Add($"capturedErrors={consoleIssues.Count};{string.Join(" | ", consoleIssues.Select(Compact))}");
        rows.Add($"RESULT={(passed ? "PASS" : "FAIL")};failures={failures.Count};{string.Join(" | ", failures.Select(Compact))}");
        File.WriteAllText(
            WimCaptivityBodyDamageDebugScenarios.ReportPath,
            string.Join("\n", rows));
        File.Delete(WimCaptivityBodyDamageDebugScenarios.RequestPath);
        Time.timeScale = oldTimeScale;
        Application.runInBackground = oldRunInBackground;
        if (passed)
        {
            Debug.Log("WIM-054 captivity body-damage verification passed. "
                + WimCaptivityBodyDamageDebugScenarios.ReportPath);
        }
        else
        {
            Debug.LogError("WIM-054 captivity body-damage verification failed. "
                + WimCaptivityBodyDamageDebugScenarios.ReportPath);
        }
        EditorApplication.ExitPlaymode();
        Destroy(gameObject);
    }

    private void DestroyFixtureHousingFallback()
    {
        if (fixtureHousing == null)
        {
            return;
        }
        world?.UnregisterBuilding(fixtureHousing);
        fixtureHousing.Grid?.RemoveOccupant(
            fixtureHousing,
            fixtureHousing.BuildingData.Placement.Layer,
            fixtureHousing.BuildingData.GetGridPosList(fixtureHousing.centerPos),
            fixtureHousing.BuildingData.Placement.IsMovement);
        Destroy(fixtureHousing.gameObject);
    }

    private void CaptureConsoleIssue(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            consoleIssues.Add(condition + " " + stackTrace);
        }
    }

    private void Check(bool condition, string name, string detail)
    {
        rows.Add($"{(condition ? "PASS" : "FAIL")}\t{name}\t{Compact(detail)}");
        if (!condition)
        {
            failures.Add(name + ": " + detail);
        }
    }

    private void CheckApprox(float actual, float expected, string name)
    {
        Check(Mathf.Abs(actual - expected) <= 0.01f,
            name,
            $"actual={actual:0.###};expected={expected:0.###}");
    }

    private void Fail(string name, string detail) => Check(false, name, detail);

    private T Resolve<T>() where T : class
    {
        try
        {
            return scope?.Container?.Resolve<T>();
        }
        catch
        {
            return null;
        }
    }

    private static DungeonRuntimeLifetimeScope FindScope() =>
        UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null
                && candidate.Container != null);

    private static string ActorId(CharacterActor actor) =>
        actor?.Identity?.PersistentId ?? string.Empty;

    private static string Describe(CharacterVitalsSnapshot vitals) =>
        $"{vitals.CurrentHealth:0.###}/{vitals.MaximumHealth:0.###};dead={vitals.IsDead}";

    private static string FindPayload(
        DungeonGameSaveData save,
        string sectionId) => save?.sections?.FirstOrDefault(section =>
            string.Equals(section.sectionId, sectionId, StringComparison.Ordinal))
            ?.payloadJson ?? string.Empty;

    private static string JoinErrors(DungeonGameRestoreReport report) =>
        string.Join(" | ", report?.Errors ?? Array.Empty<string>());

    private static string Compact(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "<none>"
            : value.Replace("\r", " ").Replace("\n", " ").Trim();

    private sealed class ProbeFeatureSurfaceView : IFeatureSurfaceView
    {
        private readonly Dictionary<string, string> details =
            new(StringComparer.Ordinal);

        public int CardCount => details.Count;

        public string FindDetail(string actionName) =>
            details.TryGetValue(actionName ?? string.Empty, out string detail)
                ? detail
                : string.Empty;

        public void AddSection(string title, string summary)
        {
        }

        public void AddLabel(string text, float fontSize, float height)
        {
        }

        public void AddDataCard(
            string actionName,
            string title,
            string detail,
            string buttonText,
            Action onClick,
            float height)
        {
            details[actionName ?? string.Empty] = detail ?? string.Empty;
        }

        public void AddControlCard(
            string actionName,
            string title,
            string detail,
            IReadOnlyList<FeatureSurfaceStepper> steppers,
            IReadOnlyList<FeatureSurfaceAction> actions,
            float height)
        {
            details[actionName ?? string.Empty] = detail ?? string.Empty;
        }

        public void ShowFeedback(string message)
        {
        }

        public void RequestRefresh()
        {
        }
    }
}
// Separate focused witness; the existing WIM054 damage/filth scenario is not run.
public sealed class WimInterrogationInformationRunner : MonoBehaviour
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-055-interrogation-information.txt";
    private const string IntruderRuntimeId = "invasion:wim055:interrogation";
    private static readonly string CaptiveId = CharacterId.FromStableSuffix(IntruderRuntimeId).Value;
    private readonly List<string> lines = new();
    private DungeonRuntimeLifetimeScope scope;
    private ICharacterAiWorldRegistry world;
    private ICaptivityRuntime captivity;
    private ICaptivityCommandService commands;
    private IWorldItemStackRuntime items;
    private IGameTimeScaleController timeScale;
    private IGameClock clock;
    private CharacterActor warden;
    private BuildableObject housing;
    private string wardenId;
    private string housingId;
    private Vector2Int captiveSetupCell;
    private IDisposable injectedFailure;
    private bool rejectNotice;

    public static string StartFocused()
    {
        Require(Application.isPlaying, "Start only in a newly protected main Play session.");
        Require(FindFirstObjectByType<WimInterrogationInformationRunner>() == null,
            "WIM055 observer already exists.");
        var current = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        var owner = FindFirstObjectByType<OwnerRunManager>();
        Require(current?.Container != null && owner != null,
            "The main scope and owner preparation are unavailable.");
        var persistence = current.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "Cannot protect the user save: persistence disposal contract unavailable.");
        persistence.Dispose();
        current.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        FindFirstObjectByType<GameManager>().isPause = true;
        current.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        new GameObject("WIM055 Interrogation Information Witness")
            .AddComponent<WimInterrogationInformationRunner>();
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Start()
    {
        IEnumerator run = Run();
        Exception failure = null;
        while (true)
        {
            bool moved;
            object current = null;
            try { moved = run.MoveNext(); if (moved) current = run.Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) break;
            yield return current;
        }
        (run as IDisposable)?.Dispose();
        injectedFailure?.Dispose();
        var game = FindFirstObjectByType<GameManager>();
        if (game != null) game.isPause = true;
        if (timeScale != null) timeScale.Scale = 0f;
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add("cleanup=operator must stop this disposable protected Play; no scene/profile/save writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        Destroy(gameObject);
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        captivity = scope.Container.Resolve<ICaptivityRuntime>();
        commands = scope.Container.Resolve<ICaptivityCommandService>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        clock = scope.Container.Resolve<IGameClock>();
        IEnumerator movementProbe = VerifyEscortMovementBoundary();
        try
        {
            while (movementProbe.MoveNext()) yield return movementProbe.Current;
        }
        finally { (movementProbe as IDisposable)?.Dispose(); }
        var owner = FindFirstObjectByType<OwnerRunManager>();
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(out var expansion, out string expansionFailure)
                    && expansion.CurrentInteriorColumns == 29,
                "Normal authored TierZero preparation failed: " + expansionFailure);
            var ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                .SingleOrDefault(value => value != null && value.gameObject.scene.isLoaded
                    && value.gameObject.activeInHierarchy && value.name == "OwnerOption_1001");
            Require(ownerButton != null && ownerButton.IsInteractable()
                    && PlayModeVerificationFrameWait.DispatchPointerClick(ownerButton.gameObject, Vector2.zero),
                "Actual owner preparation UI is unavailable.");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Actual party UI did not create the main owner.");
            lines.Add("setup=protected actual owner/party UI; no unrelated irrigation test replay");
        }
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        var events = scope.Container.Resolve<IGameEventBus>();
        var codex = scope.Container.Resolve<FacilityFeatureSceneRuntimeReferences>().Codex;
        var notices = FindFirstObjectByType<EventAlertRuntime>();
        Require(codex != null && notices != null, "Actual codex or notices are unavailable.");
        Require(world.TryGetGrid(out Grid grid) && grid != null, "Actual grid is unavailable.");
        warden = world.Characters.Where(actor => actor != null && !actor.IsDead
                && !actor.IsOwner && actor.characterType == CharacterType.NPC
                && actor.Brain != null && actor.GetComponent<AbilityWork>() != null)
            .OrderBy(actor => actor.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Require(warden != null, "No live staff warden is available.");
        wardenId = warden.Identity.PersistentId;
        housing = PlaceHousing(grid);
        housingId = housing.RequirePersistentInstanceId().Value;

        var enemies = scope.Container.Resolve<IEnemyIndividualFactory>();
        var definition = scope.Container.Resolve<IEnemyArchetypeCatalog>().Require("enemy:legion-pikeman");
        Require(definition.factionId == "human:legion" && definition.tacticalProfile.formationTag == "pike-line",
            "Known authored interrogation source changed; reassess fixture expectation.");
        var blueprint = enemies.RequireBlueprint(enemies.Create(definition.stableId,
            new CharacterId(CaptiveId), "wim055-information-focused"));
        var director = FindFirstObjectByType<InvasionDirectorRuntime>();
        Require(director != null && director.ActiveIntruders.Count == 0,
            "Prepare this isolated witness without an existing live invasion.");
        var intruderData = scope.Container.Resolve<IInvasionIntruderDataProvider>().GetRequiredIntruderData(null);
        // A free-standing Intruder is not a persistent world actor. Publish the
        // actual invasion owner before capturing its loadout and captivity join.
        // The controlled rally timer isolates interrogation from a new battle.
        var invasionState = new InvasionIntruderPersistenceState(
            intruderData.id, grid.GetWorldPos(captiveSetupCell), captiveSetupCell,
            InvasionIntruderState.Rallying, 0f, 0f, 0, 100f, 0f, 50f,
            new Dictionary<CharacterCondition, float>(),
            new InvasionIntruderSettings { raidId = IntruderRuntimeId },
            Array.Empty<DefenseStatusSnapshot>(), runtimeId: IntruderRuntimeId,
            rallyRemainingSeconds: 600f,
            raidAwareness: new DefenseRaidAwarenessSaveData { raidId = IntruderRuntimeId },
            enemyIndividual: blueprint.SaveData);
        enemies.EnsureCharacterDomains(blueprint);
        var invasionReport = new DungeonGameRestoreReport();
        Require(director.PrepareRestoreCandidates(new[] { invasionState }, invasionReport) == 1
                && invasionReport.Success,
            "Actual invasion-owner preparation failed: " + string.Join(" | ", invasionReport.Errors));
        director.PublishRestoreCandidates();
        director.CompleteRestoreCandidates();
        var captive = director.ActiveIntruders.Single().IntruderActor;
        Require(captive != null && captive.Identity.PersistentId == CaptiveId
                && captive.characterType == CharacterType.Intruder,
            "Actual invasion publication did not preserve the enemy actor identity/type.");
        captive.SetAiPaused(true);
        // Suppression ends the invasion owner. Capture/escort, not an invented
        // Confined save row, must establish the replacement captivity owner.
        var bodyHealth = scope.Container.Resolve<CharacterBodyHealthRuntime>();
        CharacterBodyHealthSnapshot captiveBody = bodyHealth.GetSnapshot(captive);
        var downedParts = captiveBody.Parts.Select(part => new CharacterBodyPartHealthState
        {
            bodyPart = part.bodyPart,
            maxHealth = part.maxHealth,
            currentHealth = part.bodyPart == CombatBodyPart.LeftLeg
                || part.bodyPart == CombatBodyPart.RightLeg ? part.maxHealth * 0.18f : part.currentHealth,
            bleedingPerSecond = part.bleedingPerSecond
        }).ToArray();
        bodyHealth.ApplySnapshot(captive, new CharacterBodyHealthSnapshot(downedParts,
            captiveBody.BloodLoss, captiveBody.Suppression, captiveBody.Consciousness,
            captiveBody.Manipulation, 0.18f, true), "QA controlled capture-candidate injury");
        Require(bodyHealth.GetSnapshot(captive).Downed,
            "Capture fixture did not establish authoritative body-health downing.");
        director.ActiveIntruders.Single().ResolveSuppressedBy(warden);
        Require(director.ActiveIntruders.Count == 0 && !captive.IsDead
                && captive.CurrentLifecycleState == CharacterLifecycleState.Downed,
            "Suppression did not release the invasion owner to a living downed actor.");
        var carry = CharacterCarryInventory.Ensure(warden);
        Require(carry.TryAdd("qa:wim055:restraint", CaptivityItemDefinitions.RestraintsItemId, 1,
                scope.Container.Resolve<IDungeonItemCatalogProvider>(),
                scope.Container.Resolve<IItemHaulingSettingsProvider>(), out string restraintFailure),
            "Controlled physical restraint supply failed: " + restraintFailure);
        ConfigureWarden();
        Vector3 beforeCapturePosition = captive.transform.position;
        string beforeCaptureCarry = JsonUtility.ToJson(carry.Capture());
        AbilityCaptiveEscort captureAbility = warden.GetComponent<AbilityCaptiveEscort>()
            ?? warden.gameObject.AddComponent<AbilityCaptiveEscort>();
        bool cancelledBeforePickup = false;
        captureAbility.DebugBeforeEscortRoutineStart = _ =>
        {
            cancelledBeforePickup = commands.CancelCapture(CaptiveId, "qa:before-physical-pickup");
        };
        try
        {
            Require(commands.TryOrderCapture(captive, warden, out string cancelledCaptureFailure),
                "Pre-pickup cancellation command setup failed: " + cancelledCaptureFailure);
        }
        finally { captureAbility.DebugBeforeEscortRoutineStart = null; }
        Require(cancelledBeforePickup
                && State().status == CaptivityStatus.AwaitingCapture
                && State().capturePosition == grid.GetXY(beforeCapturePosition)
                && captive.transform.position == beforeCapturePosition
                && JsonUtility.ToJson(carry.Capture()) == beforeCaptureCarry,
            "Pre-pickup cancellation moved the subject or consumed restraints.");
        yield return null;
        Require(!captureAbility.IsEscorting
                && captive.transform.position == beforeCapturePosition
                && JsonUtility.ToJson(carry.Capture()) == beforeCaptureCarry,
            "Cancelled capture retained escort ownership or performed later physical work.");
        lines.Add("actual pre-pickup cancellation=PASS; subject world/capture cell and carried restraint unchanged; action released");
        Require(commands.TryOrderCapture(captive, warden, out string captureFailure),
            "Actual capture order failed: " + captureFailure);
        FindFirstObjectByType<GameManager>().isPause = false;
        timeScale.Scale = 8f;
        float captureStarted = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - captureStarted < 30f
               && State().status != CaptivityStatus.Confined)
            yield return null;
        Pause();
        CaptiveState confined = State();
        Require(confined.status == CaptivityStatus.Confined
                && confined.housingBuildingId == housingId
                && director.ActiveIntruders.Count == 0,
            $"Actual capture/escort did not complete: {confined.status}; {confined.lastResult}; warden={warden.GetNowXY()}; captive={captive.GetNowXY()}.");
        Require(scope.Container.Resolve<ICharacterWorldPersistenceIdentityQuery>()
                .GetPersistentActorIds().Contains(new CharacterId(CaptiveId)),
            "Released, confined enemy has no character-world save owner.");
        DungeonGameSaveData checkpoint = saves.Capture();
        DungeonSaveSectionEnvelope envelope = checkpoint.sections.Single(value => value.sectionId == CaptivitySaveSection.Id);
        CaptivitySaveData captivePayload = JsonUtility.FromJson<CaptivitySaveData>(envelope.payloadJson);
        CaptiveState prepared = captivePayload.captives.Single(value => value.captiveId == CaptiveId);
        // Only the high-fear test condition is controlled; preserve every real
        // capture, restraint, physical lot and character ownership field.
        prepared.will = 100f;
        prepared.fear = 80f;
        prepared.trust = 10f;
        prepared.grudge = 0f;
        prepared.nextSecurityCheckAt = float.MaxValue;
        envelope.payloadJson = JsonUtility.ToJson(captivePayload);
        Require(saves.TryRestore(checkpoint, out DungeonGameRestoreReport setupRestore),
            "Confined checkpoint failed current world joins: " + string.Join(" | ", setupRestore.Errors));
        // Commit deactivates retiring actors immediately; Unity Destroy and
        // lifetime-registry removal complete at the end of this frame.
        yield return null;
        Reacquire();
        Require(world.AllCharacters.Count(value => value != null && value.gameObject.activeInHierarchy
                    && value.Identity.PersistentId == CaptiveId) == 1
                && captivity.TryGetActor(CaptiveId, out CharacterActor restoredCaptive)
                && !ReferenceEquals(captive, restoredCaptive)
                && restoredCaptive.characterType == CharacterType.Intruder
                && restoredCaptive.CurrentLifecycleState == CharacterLifecycleState.Downed,
            "Current restore did not replace the captive with exactly one same-role/lifecycle actor.");
        Require(scope.Container.Resolve<ICharacterNarrativeQuery>().TryGet(new CharacterId(CaptiveId), out var origin)
                && origin.OriginEnemyArchetypeId == "enemy:legion-pikeman"
                && origin.OriginFactionId == "human:legion",
            "Enemy provenance did not survive actual current world restoration.");
        lines.Add("preparation=controlled authored enemy/suppression/input/high-fear; actual invasion owner release/capture/escort/current-world captive replacement PASS; natural battle NOT_RUN");

        // The real EventAlert subscriber is registered first. Inject a failure
        // AFTER it records the alert to witness idempotent replay, not initial delivery loss.
        rejectNotice = true;
        string firstSourceId = CaptivityInterrogationAttemptIdentity.FormatNoticeSourceId(CaptiveId, 1);
        injectedFailure = events.Subscribe<EventAlertRequestedEvent>(value =>
        {
            if (rejectNotice && value.request?.SourceId == firstSourceId)
                throw new InvalidOperationException("qa-injected-interrogation-notice-failure");
        });
        int beforeInput = SupplyOneGeneral();
        ConfigureWarden();
        Require(commands.TryStartInteraction(CaptiveId, "captivity:interrogation", warden, housing, out string startFailure),
            "Actual interrogation start failed: " + startFailure);
        float startedGame = clock.Time;
        float startedWall = Time.realtimeSinceStartup;
        bool preferredFirstWork = false;
        FindFirstObjectByType<GameManager>().isPause = false;
        timeScale.Scale = 8f;
        while (Time.realtimeSinceStartup - startedWall < 30f && !State().interrogationTerminal.HasOutcome)
        {
            PreferReadyWardenWork(ref preferredFirstWork);
            yield return null;
        }
        Pause();
        CaptiveState first = State();
        Require(first.interrogationTerminal.hasInformation && first.interrogationTerminal.HasPendingPublication
                && first.interrogationTerminal.attemptId == 1
                && first.interrogationTerminal.originEnemyArchetypeId == "enemy:legion-pikeman"
                && first.interrogationTerminal.originFactionId == "human:legion"
                && first.interrogationTerminal.formationTag == "pike-line"
                && first.interrogationTerminal.highFearCaution
                && first.interrogationTerminal.informationText.Contains("미확인"),
            $"Automatic Warden did not freeze the expected unverified information outcome; gameSeconds={clock.Time - startedGame:0.###}; work={first.completedInteractionWork}/{first.requiredInteractionWork}; consumed={first.interactionMaterialsConsumed}; phase={warden.Brain.CurrentActionPhase}; failure={warden.Brain.LastActionFailure}; terminal={JsonUtility.ToJson(first.interrogationTerminal)}.");
        Require(Mathf.Approximately(first.will, 92f) && Mathf.Approximately(first.fear, 89f)
                && Mathf.Approximately(first.trust, 6f) && Mathf.Approximately(first.grudge, 7f)
                && GeneralCount() == beforeInput - 1,
            "Automatic interrogation cost or existing state deltas were not applied exactly once.");
        Require(codex.HasInformation(CodexEntryCategory.Monster, first.interrogationTerminal.codexEntryId,
                first.interrogationTerminal.informationText), "Frozen information did not reach actual Codex authority.");
        Require(notices.EventLog.Count(value => value.SourceId == firstSourceId && value.Count == 1) == 1,
            "Fault setup must fail downstream AFTER the real notice is recorded once.");
        lines.Add($"automatic-warden=PASS; gameSeconds={clock.Time - startedGame:0.###}; wallSeconds={Time.realtimeSinceStartup - startedWall:0.###}; input=1; statDelta=-8/+9/-4/+7; notice-recorded=1; downstream-publication-ack-pending=true");

        string frozenInformation = first.interrogationTerminal.informationText;
        DungeonGameSaveData pendingWorld = saves.Capture();
        Require(saves.TryRestore(pendingWorld, out DungeonGameRestoreReport pendingRestore),
            "Pending information/notice current world restore failed: " + string.Join(" | ", pendingRestore.Errors));
        yield return null;
        Reacquire();
        Require(State().interrogationTerminal.informationText == frozenInformation,
            "Restore changed the locked interrogation outcome.");
        rejectNotice = false;
        FindFirstObjectByType<GameManager>().isPause = false;
        timeScale.Scale = 1f;
        float retryStart = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - retryStart < 5f && State().interrogationTerminal.HasPendingPublication)
            yield return null;
        Pause();
        CaptiveState retried = State();
        EventAlertRecord[] firstNotices = notices.EventLog.Where(value => value.SourceId == firstSourceId).ToArray();
        Require(!retried.interrogationTerminal.HasPendingPublication
                && retried.interrogationTerminal.informationText == frozenInformation
                && Mathf.Approximately(retried.will, 92f) && Mathf.Approximately(retried.fear, 89f)
                && Mathf.Approximately(retried.trust, 6f) && Mathf.Approximately(retried.grudge, 7f)
                && GeneralCount() == beforeInput - 1
                && firstNotices.Length == 1 && firstNotices[0].Count == 1
                && firstNotices[0].Title == "심문 성공"
                && firstNotices[0].Detail.Contains("미확인") && firstNotices[0].Detail.Contains("pike-line"),
            "Restored publication retry lost/duplicated information, notice, cost, or state effects.");
        Button noticeButton = FindObjectsByType<Button>(FindObjectsSortMode.None)
            .SingleOrDefault(button => button.gameObject.activeInHierarchy
                && button.name == $"EventAlertButton_{firstNotices.Single().Id}");
        Require(noticeButton != null
                && PlayModeVerificationFrameWait.DispatchPointerClick(noticeButton.gameObject, Vector2.zero),
            "Actual success notice button was not clickable.");
        Require(notices.IsDetailVisible && notices.SelectedRecord?.SourceId == firstSourceId,
            "Notice click did not open this interrogation's actual result.");
        lines.Add("post-notice-downstream-failure/current-world-restore/retry=PASS; same Codex fact; source-id notice1/count1; real notice click/detail=PASS; pre-delivery-notice-failure=NOT_RUN");

        int beforeRepeatInput = SupplyOneGeneral();
        ConfigureWarden();
        // The first attempt already witnesses real Warden AI. For the distinct
        // known-information contract, keep real delivery but explicitly advance
        // through the public command after it arrives (no second AI replay).
        warden.GetComponent<AbilityWork>().SetWorkPriority(BuiltInWorkTypeIds.Warden, WorkPriorityLevel.Off);
        Require(commands.TryStartInteraction(CaptiveId, "captivity:interrogation", warden, housing, out string repeatFailure),
            "Second actual interrogation start failed: " + repeatFailure);
        FindFirstObjectByType<GameManager>().isPause = false;
        timeScale.Scale = 8f;
        float repeatStart = Time.realtimeSinceStartup;
        var repeatReadiness = scope.Container.Resolve<ICaptivityWorkReadinessQuery>();
        while (Time.realtimeSinceStartup - repeatStart < 30f
               && (!repeatReadiness.IsInteractionReady(CaptiveId, out _)
                   || warden.GetComponent<AbilityHaul>()?.IsHauling == true))
            yield return null;
        Pause();
        Require(repeatReadiness.IsInteractionReady(CaptiveId, out string repeatReadyReason)
                && State().completedInteractionWork == 0f && !State().interactionMaterialsConsumed,
            "Known-information command probe did not receive untouched real materials: " + repeatReadyReason);
        Require(commands.AdvanceInteraction(CaptiveId, warden, 18f, out string repeatCompletion),
            "Known-information public completion failed: " + repeatCompletion);
        CaptiveState repeat = State();
        string secondSourceId = CaptivityInterrogationAttemptIdentity.FormatNoticeSourceId(CaptiveId, 2);
        Require(repeat.interrogationTerminal.attemptId == 2 && !repeat.interrogationTerminal.hasInformation
                && !repeat.interrogationTerminal.HasPendingPublication
                && GeneralCount() == beforeRepeatInput - 1
                && notices.EventLog.Count(value => value.SourceId == firstSourceId) == 1
                && notices.EventLog.Count(value => value.SourceId == secondSourceId
                    && value.Title.Contains("추가 정보 없음")) == 1,
            "Repeated known fact was awarded as new information or reused the old attempt notice.");
        Require(!commands.AdvanceInteraction(CaptiveId, warden, 18f, out _)
                && GeneralCount() == beforeRepeatInput - 1 && State().interrogationAttemptSequence == 2,
            "Completed interaction accepted duplicate work or advanced a new attempt.");
        lines.Add("repeat-known-fact=real delivery then explicit public AdvanceInteraction(18WU), no-information/new attempt/source; completed-work replay=rejected/no-consumption; second automatic AI replay NOT_RUN");
        lines.Add("scope=controlled enemy/suppression/high-fear/input/healthy-warden setup; first attempt uses existing typed next-work preference after real delivery, actual capture/escort and Warden AI; second known-fact probe uses public work command; real Codex/notices/current world restore; unprompted priority competition/natural battle/6adult NOT_RUN");
    }

    private void Pause()
    {
        FindFirstObjectByType<GameManager>().isPause = true;
        timeScale.Scale = 0f;
    }

    private void OnDestroy()
    {
        injectedFailure?.Dispose();
        injectedFailure = null;
    }

    private CaptiveState State()
    {
        Require(captivity.TryGetCaptive(CaptiveId, out CaptiveState state), "Fixture captive disappeared.");
        return state;
    }

    private void Reacquire()
    {
        warden = world.AllCharacters.Single(value => value != null && value.Identity.PersistentId == wardenId);
        housing = world.Buildings.Single(value => value != null && value.PersistentInstanceId.Value == housingId);
    }

    private void ConfigureWarden()
    {
        // Controlled integration precondition, once per interaction. Do not
        // freeze decay or bypass the real delivery/work decision loop.
        foreach (CharacterCondition need in new[] { CharacterCondition.HUNGER, CharacterCondition.THIRST,
                     CharacterCondition.SLEEP, CharacterCondition.HYGIENE,
                     CharacterCondition.EXCRETION, CharacterCondition.FUN })
            warden.Stats.ChangesStat(need, 100f - warden.Stats.GetConditionValue(need, 0f));
        lines.Add("warden preparation=needs filled once; normal decay/self-care/actual AI retained; not a survival-network witness");
        foreach (CharacterActor actor in world.Characters.Where(value => value != null && value != warden))
            actor.SetAiPaused(true);
        warden.SetAiPaused(false);
        AbilityWork work = warden.GetComponent<AbilityWork>();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority2);
        work.SetWorkPriority(BuiltInWorkTypeIds.Warden, WorkPriorityLevel.Priority1);
        warden.Brain.RequestImmediateReplan(clearFailures: true);
    }

    private void PreferReadyWardenWork(ref bool preferred)
    {
        if (preferred || warden.GetComponent<AbilityHaul>()?.IsHauling == true
            || !scope.Container.Resolve<ICaptivityWorkReadinessQuery>()
                .IsInteractionReady(CaptiveId, out _)) return;
        preferred = warden.Brain.PreferWorkActionOnNextDecision(BuiltInWorkTypeIds.Warden, 120f);
        Require(preferred, "Ready Warden work could not request the existing typed next-decision preference.");
        lines.Add("typed Warden preference requested after real material readiness and haul completion; normal duty/need/AI gates retained");
    }

    private int SupplyOneGeneral()
    {
        var catalog = scope.Container.Resolve<IDungeonItemCatalogProvider>();
        DungeonItemDefinition input = catalog.All.Where(value => value != null
                && value.StockCategory == StockCategory.General && value.MaxStack > 1
                && string.IsNullOrEmpty(value.EquipmentId))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal).First();
        Require(items.SpawnItemAt(input.ItemId, 1, warden.GetNowXY(), WorldItemStackState.Loose,
                string.Empty, out int quantity) && quantity == 1, "One real General input was not spawned.");
        return GeneralCount();
    }

    private int GeneralCount()
    {
        HashSet<string> ids = scope.Container.Resolve<IDungeonItemCatalogProvider>().All
            .Where(value => value != null && value.StockCategory == StockCategory.General)
            .Select(value => value.ItemId).ToHashSet(StringComparer.Ordinal);
        return items.GetAllStacks().Where(value => ids.Contains(value.ItemId)).Sum(value => value.Quantity);
    }

    private IEnumerator VerifyEscortMovementBoundary()
    {
        var probeObject = new GameObject("WIM055 controlled escort movement port");
        AbilityCaptiveEscort ability = probeObject.AddComponent<AbilityCaptiveEscort>();
        try
        {
            var pending = new EscortMovementProbe();
            ability.Configure(pending, clock);
            ability.StartEscort("qa:wim055:movement");
            yield return null;
            yield return null;
            Require(pending.Requests > 0 && ability.IsEscorting && pending.Owned
                    && pending.Failures == 0 && pending.Pickups == 0 && pending.Carries == 0,
                "Pending path discarded ownership or started physical work.");
            pending.Resolution = CaptivityMovementResolution.Ready;
            for (int i = 0; i < 16 && ability.IsEscorting; i++) yield return null;
            Require(!ability.IsEscorting && !pending.Owned && pending.Failures == 0
                    && pending.Pickups == 1 && pending.Carries == 1 && pending.Completed == 1,
                "Pending-to-ready did not complete restraint/subject/housing exactly once.");

            var cancelled = new EscortMovementProbe();
            ability.Configure(cancelled, clock);
            ability.StartEscort("qa:wim055:movement");
            yield return null;
            Require(cancelled.Requests > 0 && ability.IsEscorting, "Cancel probe never reached Pending.");
            ability.StopEscort("qa:cancel-pending-path");
            cancelled.Resolution = CaptivityMovementResolution.Ready;
            yield return null;
            Require(!ability.IsEscorting && !cancelled.Owned && cancelled.Failures == 1
                    && cancelled.Pickups == 0 && cancelled.Carries == 0 && cancelled.Completed == 0,
                "Cancelled pending path resumed or retained ownership.");

            var unreachable = new EscortMovementProbe { Resolution = CaptivityMovementResolution.Unreachable };
            ability.Configure(unreachable, clock);
            ability.StartEscort("qa:wim055:movement");
            for (int i = 0; i < 4 && ability.IsEscorting; i++) yield return null;
            Require(!ability.IsEscorting && !unreachable.Owned && unreachable.Failures == 1
                    && unreachable.Pickups == 0 && unreachable.Carries == 0 && unreachable.Completed == 0,
                "Unreachable path did not terminate without physical work.");
            lines.Add("controlled-port actual escort coroutine=PASS; Pending retains ownership -> Ready once; pending cancel/no resume; Unreachable terminal; not natural pathfinding evidence");
        }
        finally
        {
            ability.StopEscort("qa:probe-cleanup");
            Destroy(probeObject);
        }
    }

    private sealed class EscortMovementProbe : ICaptiveEscortAbilityPort
    {
        public CaptivityMovementResolution Resolution = CaptivityMovementResolution.Pending;
        public bool Owned;
        public int Requests, Failures, Pickups, Carries, Completed;
        private readonly CaptiveState state = new()
        {
            captiveId = "qa:wim055:movement", status = CaptivityStatus.Stabilizing,
            stabilized = true, restraintStackId = "qa:restraint",
            restraintPickupPosition = new Vector2Int(1, 1), housingPosition = new Vector2Int(3, 1)
        };
        public bool TryBeginActionOwnership(string id, out string failure)
        { Owned = true; failure = string.Empty; return true; }
        public bool HasActionOwnership() => Owned;
        public void EndActionOwnership(bool clearFailures) => Owned = false;
        public bool TryGetState(string id, out CaptiveState value, out Vector2Int position,
            out string name, out string failure)
        { value = state; position = new Vector2Int(2, 1); name = "probe"; failure = string.Empty; return Owned; }
        public CaptivityMovementResolution ResolveMovement(Vector2Int destination,
            CaptivityAbilityAccessKind kind, out IEnumerator movement)
        { Requests++; movement = Resolution == CaptivityMovementResolution.Ready ? EmptyMovement() : null; return Resolution; }
        private static IEnumerator EmptyMovement() { yield break; }
        public bool TryPickupReservedRestraint(CaptiveState value, out string failure)
        { Pickups++; failure = string.Empty; return true; }
        public float AdvanceStabilization(string id, float delta) => 1f;
        public bool TryBeginEscort(string id, out string failure)
        { Carries++; state.status = CaptivityStatus.Escorting; failure = string.Empty; return true; }
        public IDisposable BeginEscortPass(string id) => null;
        public bool TryCompleteEscort(string id, out string failure)
        { Completed++; state.status = CaptivityStatus.Confined; failure = string.Empty; return true; }
        public void FailEscort(string id, string reason) => Failures++;
        public void SetActionPhase(string phase, string detail) { }
        public void RequestImmediateReplan(bool clearFailures) { }
    }

    private BuildableObject PlaceHousing(Grid grid)
    {
        BuildingSO data = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Captivity/CP01_감방구속대.asset");
        Require(data?.GetCaptiveHousingAbility()?.IsValid == true, "Authored CP01 missing.");
        HashSet<Vector2Int> actors = world.AllCharacters.Where(value => value != null)
            .Select(value => value.GetNowXY()).ToHashSet();
        var rooms = scope.Container.Resolve<IRoomLayoutCache>();
        var doors = scope.Container.Resolve<IDoorAccessCommandService>();
        var travelPolicy = scope.Container.Resolve<IGridTraversalCostPolicy>();
        var travelAccess = scope.Container.Resolve<IGridTraversalAccessQuery>();
        var traversal = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(warden));
        foreach (GridCell cell in grid.GetCells()
                     .OrderBy(value => Mathf.Abs(value.Position.x - warden.GetNowXY().x)
                         + Mathf.Abs(value.Position.y - warden.GetNowXY().y))
                     .ThenBy(value => value.Position.y).ThenBy(value => value.Position.x))
        {
            IReadOnlyList<Vector2Int> footprint = data.GetGridPosList(cell.Position);
            if (!rooms.TryGetRoom(grid, cell.Position, out RoomInstance room)
                || !room.IsUsable || room.Doors.Count == 0
                || footprint.Any(position => !room.Cells.Contains(position))
                || !room.Cells.Any(position => !footprint.Contains(position)
                    && !actors.Contains(position) && grid.IsWalkable(position))) continue;
            if (footprint.Count == 0 || footprint.Any(position => actors.Contains(position)
                    || !grid.IsValidGridPos(position)
                    || grid.GetGridCell(position)?.AreaType != GridCellAreaType.DungeonInterior
                    || grid.GetGridCell(position)?.CanOccupy(data.Placement.Layer) != true)) continue;
            if (!footprint.Any(position => new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down }
                    .Any(offset => !footprint.Contains(position + offset) && grid.IsWalkable(position + offset)))) continue;
            Vector2Int[] reachableSetupCells = room.Cells
                .Where(position => !footprint.Contains(position) && !actors.Contains(position)
                    && grid.IsWalkable(position))
                .OrderBy(position => Mathf.Abs(position.x - cell.Position.x)
                    + Mathf.Abs(position.y - cell.Position.y))
                .ThenBy(position => position.y).ThenBy(position => position.x)
                .Where(position => grid.SearchPathTo(warden.GetNowXY(), position,
                        pathCell => !footprint.Contains(pathCell)
                            && travelAccess.CanTraverse(grid, pathCell, traversal, out _), travelPolicy, traversal)
                    .GetMoveCostTo(position) != int.MaxValue)
                .Take(1).ToArray();
            if (reachableSetupCells.Length == 0) continue;
            var factory = scope.Container.Resolve<IGridBuildingObjectFactory>();
            BuildableObject value = factory.Create(grid, data, cell.Position);
            Require(value != null, "CP01 factory returned null.");
            scope.Container.InjectGameObject(value.gameObject);
            value.SetGrid(grid);
            value.Initialization(data, cell.Position);
            Require(grid.RegisterOccupant(value, data.Placement.Layer, footprint, data.Placement.IsMovement),
                "CP01 registration failed after legal placement preflight.");
            world.RegisterBuilding(value);
            captiveSetupCell = reachableSetupCells[0];
            foreach (Door door in room.Doors.OfType<Door>())
                Require(doors.ApplyPreset(door, DoorAccessPreset.Cell),
                    "Existing room door could not apply the actual cell policy.");
            return value;
        }
        throw new InvalidOperationException("No legal current dungeon placement for CP01.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
