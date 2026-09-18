#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using DungeonStory.Operation;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// Main-owned integration witness for the actual funeral producer. Preparation
// is controlled, but the memorial facility, physical kit, grief owner, funeral
// command, typed effect, alert projection and whole-save restore are production.
public sealed class Wim040FuneralLifeEventLiveWitness
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-040-funeral-life-event-live.txt";

    private static bool running;
    private readonly List<string> lines = new();
    private IGameTimeScaleController timeScale;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running,
            "Start once in a fresh disposable main Play session.");
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime is not initialized.");
        IDisposable persistence =
            scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "Cannot protect user save files.");
        persistence.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        GameManager host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing main coroutine host.");
        Wim040FuneralLifeEventLiveWitness runner = new()
        {
            timeScale = scope.Container.Resolve<IGameTimeScaleController>()
        };
        runner.Pause();
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try
        {
            host.StartCoroutine(runner.Observe());
        }
        catch
        {
            running = false;
            throw;
        }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        Stack<IEnumerator> pending = new();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object current = null;
            bool moved;
            try
            {
                moved = pending.Peek().MoveNext();
                if (moved) current = pending.Peek().Current;
            }
            catch (Exception error)
            {
                failure = error;
                break;
            }
            if (!moved)
            {
                (pending.Pop() as IDisposable)?.Dispose();
                continue;
            }
            if (current is IEnumerator nested) pending.Push(nested);
            else yield return current;
        }
        while (pending.Count > 0)
            (pending.Pop() as IDisposable)?.Dispose();
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add(
            "scope=actual registered ISocialCareCommand with one active owner participant, controlled archived same-species death/grief/trauma seed, authored RF87 memorial placement, one actual physical funeral kit, exact receipt/Trauma/alert and current whole-save restore; preparation is not natural death, construction, crafting or autonomous funeral scheduling evidence");
        lines.Add(
            "cleanup=paused disposable protected Play; disk persistence disabled before owner selection; operator stops without scene/profile/save writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(failure == null
            ? "WIM040 funeral life-event live PASS"
            : "WIM040 funeral life-event live FAIL: " + failure.Message);
        running = false;
    }

    private IEnumerator Run()
    {
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime disappeared.");
        OwnerRunManager owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner preparation is unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(out _, out string expansionFailure),
                "Tier-zero preparation failed: " + expansionFailure);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null,
                "Normal party UI did not publish an owner.");
        }

        CharacterActor participant = owner.CurrentOwnerActor;
        participant.SetAiPaused(true);
        CharacterId participantId = participant.Identity.TypedPersistentId;
        CharacterId deceasedId = new(
            "character:wim040:funeral-live-deceased");
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        int absoluteDay = Math.Max(1, calendar.Day);
        IKinshipCommand kinship = scope.Container.Resolve<IKinshipCommand>();
        IGriefTraumaService grief = scope.Container.Resolve<IGriefTraumaService>();
        IPsychosocialPersistence psychosocial =
            scope.Container.Resolve<IPsychosocialPersistence>();
        V20CampaignRuntime campaign = scope.Container.Resolve<V20CampaignRuntime>();
        IWorldItemStackRuntime items =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        IDungeonGameSaveService saves =
            scope.Container.Resolve<IDungeonGameSaveService>();
        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();

        CharacterSpeciesId speciesId =
            new(participant.SpeciesTag);
        Require(scope.Container.Resolve<ICharacterSpeciesDefinitionCatalog>()
                .TryGetDefinition(
                    speciesId,
                    out CharacterSpeciesDefinitionSO species)
            && species.funeralCulture != null,
            "Owner species has no authored funeral culture.");
        string funeralFacilityTag =
            species.funeralCulture.requiredFacilityTag;
        BuildableObject memorial = RequireMemorial(
            scope.Container,
            participant.GetNowXY(),
            funeralFacilityTag);
        string memorialId = memorial.PersistentInstanceId.Value;
        kinship.ArchiveDeath(
            deceasedId,
            speciesId,
            birthAbsoluteDay: 1,
            deathAbsoluteDay: absoluteDay,
            famous: false,
            new HouseholdId("household:wim040:funeral-live"),
            generation: 1);
        CharacterLifeDeathRecord death = new(
            deceasedId,
            CharacterDeathCauseCode.Unknown,
            absoluteDay,
            new CoreGridCell(
                participant.GetNowXY().x,
                participant.GetNowXY().y),
            new[] { participantId });
        grief.RecordDeath(
            participantId,
            death,
            GriefRelationshipKind.Household);
        grief.ApplyTraumaDelta(
            participantId,
            "qa:wim040:funeral-live-seed",
            absoluteDay,
            20f);
        Require(grief.TryGet(participantId, out CharacterGriefAggregate seeded)
            && seeded.NeedsFuneral(deceasedId)
            && Math.Abs(seeded.Trauma - 20f) < 0.001f,
            "Controlled funeral grief/trauma precondition failed.");

        const string kitId = "supply:funeral-preparation-kit";
        int Quantity() => items.GetAllStacks()
            .Where(value => value.ItemId == kitId)
            .Sum(value => value.Quantity);
        int beforeKits = Quantity();
        Require(items.SpawnItemAt(
                kitId,
                1,
                participant.GetNowXY(),
                WorldItemStackState.Loose,
                string.Empty,
                out int added)
            && added == 1
            && Quantity() == beforeKits + 1,
            "Controlled physical funeral kit preparation failed.");

        List<EventAlertRequest> alerts = new();
        List<V20ContentEffectsResolvedEvent> effects = new();
        using IDisposable alertSubscription =
            events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                if (value.request?.SourceId.StartsWith(
                        "observed-life:",
                        StringComparison.Ordinal) == true)
                    alerts.Add(value.request);
            });
        using IDisposable effectSubscription =
            events.Subscribe<V20ContentEffectsResolvedEvent>(value =>
            {
                if (value.DefinitionId == "life-event:grave-visit")
                    effects.Add(value);
            });

        string actionId = "funeral-operation:wim040:live";
        Require(scope.Container.Resolve<ISocialCareCommand>().TryHoldFuneral(
                actionId,
                deceasedId,
                new[] { participantId },
                memorialId,
                out DomainFailure failure),
            "Actual funeral command failed: " + failure.Code);
        Require(Quantity() == beforeKits,
            "Actual funeral did not consume exactly one physical preparation kit.");
        Require(grief.TryGet(participantId, out CharacterGriefAggregate completed)
            && !completed.NeedsFuneral(deceasedId)
            && Math.Abs(completed.Trauma - 10f) < 0.001f,
            "Actual funeral did not apply funeral relief 8 plus grave-visit Trauma delta -2 exactly once.");
        SocietyEventWorldSaveData society = campaign.CaptureSociety();
        V20ActiveEventSaveData occurrence = society.recentResolvedEvents.Single(
            value => value.definitionId == "life-event:grave-visit"
                && value.participantCharacterIds.SequenceEqual(
                    new[] { participantId.Value }));
        Require(
            society.successfulLifeEventOperations.Count(value =>
                value != null
                && value.sourceOperationId == actionId
                && value.occurrenceInstanceId == occurrence.instanceId) == 1
            && alerts.Count == 1
            && alerts[0].IsResolved
            && alerts[0].SourceId == occurrence.instanceId
            && effects.Count == 1
            && effects[0].PhysicalEffectsApplied
            && effects[0].Effects.Count(value => value != null
                && value.kind == V20ContentEffectKind.Trauma
                && Math.Abs(value.amount + 2f) < 0.001f) == 1,
            "Actual funeral did not publish one exact grave-visit receipt, Trauma effect event and resolved alert.");
        lines.Add(
            "PASS actual funeral producer consumed one physical kit, completed exact grief, applied Trauma 20->10 and published one grave-visit receipt/effect/alert");

        DungeonGameSaveData saved = saves.FromJson(
            saves.ToJson(saves.Capture()));
        Require(saves.TryRestore(saved, out DungeonGameRestoreReport restore)
            && restore.Success,
            "Current whole funeral save restore failed: "
                + string.Join(" | ", restore.Errors));
        participant = owner.CurrentOwnerActor;
        Require(participant != null
            && participant.Identity != null
            && participant.Identity.PersistentId == participantId.Value,
            "Current whole restore did not republish the exact owner actor.");
        participant.SetAiPaused(true);
        grief = scope.Container.Resolve<IGriefTraumaService>();
        campaign = scope.Container.Resolve<V20CampaignRuntime>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        Require(grief.TryGet(participantId, out CharacterGriefAggregate restored)
            && !restored.NeedsFuneral(deceasedId)
            && Math.Abs(restored.Trauma - 10f) < 0.001f
            && items.GetAllStacks().Where(value => value.ItemId == kitId)
                .Sum(value => value.Quantity) == beforeKits
            && campaign.CaptureSociety().successfulLifeEventOperations.Count(
                value => value != null
                    && value.sourceOperationId == actionId) == 1,
            "Current whole restore lost funeral grief, physical consumption or receipt state.");
        string societyBeforeReplay =
            JsonUtility.ToJson(campaign.CaptureSociety());
        string milestonesBeforeReplay =
            JsonUtility.ToJson(campaign.CaptureMilestones());
        string psychosocialBeforeReplay = JsonUtility.ToJson(
            scope.Container.Resolve<IPsychosocialPersistence>().Capture());
        string kinshipBeforeReplay = JsonUtility.ToJson(
            scope.Container.Resolve<IKinshipHouseholdPersistence>().Capture());
        string itemsBeforeReplay = JsonUtility.ToJson(items.Capture());
        Require(!scope.Container.Resolve<ISocialCareCommand>().TryHoldFuneral(
                actionId,
                deceasedId,
                new[] { participantId },
                memorialId,
                out _)
            && JsonUtility.ToJson(campaign.CaptureSociety())
                == societyBeforeReplay
            && JsonUtility.ToJson(campaign.CaptureMilestones())
                == milestonesBeforeReplay
            && JsonUtility.ToJson(scope.Container
                    .Resolve<IPsychosocialPersistence>().Capture())
                == psychosocialBeforeReplay
            && JsonUtility.ToJson(scope.Container
                    .Resolve<IKinshipHouseholdPersistence>().Capture())
                == kinshipBeforeReplay
            && JsonUtility.ToJson(items.Capture()) == itemsBeforeReplay
            && alerts.Count == 1
            && effects.Count == 1,
            "Restored funeral replay changed world state or republished effects.");
        lines.Add(
            "PASS current whole save restores funeral ownership and exact replay changes no state, item, effect or alert");
    }

    private static BuildableObject RequireMemorial(
        IObjectResolver container,
        Vector2Int start,
        string funeralFacilityTag)
    {
        IBuildingWorldQuery buildings =
            container.Resolve<IBuildingWorldQuery>();
        BuildableObject existing = buildings.Buildings
            .Where(value => value != null && !value.isDestroy)
            .OrderBy(value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault(value =>
                value.HasSemanticTag("workstation:v19:memorial")
                && value.HasSemanticTag(funeralFacilityTag)
                && value.TryGetNearestWorkAccessGridPosition(
                    value.Grid,
                    start,
                    out Vector2Int destination)
                && value.Grid.GetMovePathTo(start, destination)?.Count > 0);
        if (existing != null)
            return existing;

        BuildingSO definition = Resources.Load<BuildingSO>(
            "SO/Building/ResearchOverhaul/RF87_추모실");
        Require(definition != null
            && definition.Abilities.OfType<BuildingSemanticTagsAbility>()
                .Any(value => value.tags.Contains(
                    "workstation:v19:memorial",
                    StringComparer.Ordinal)
                    && value.tags.Contains(
                        funeralFacilityTag,
                        StringComparer.Ordinal)),
            "Authored RF87 memorial facility is missing its semantic contract.");
        Require(container.Resolve<IGridSystemProvider>()
                .TryGetGrid(out Grid grid)
            && grid != null,
            "Main grid is unavailable for controlled memorial placement.");
        GridPlacementValidator geometry = new();
        IRoomLayoutCache rooms = container.Resolve<IRoomLayoutCache>();
        Vector2Int? anchor = null;
        for (int y = 0; y < grid.height && !anchor.HasValue; y++)
        {
            for (int x = 0; x < grid.width && !anchor.HasValue; x++)
            {
                Vector2Int candidate = new(x, y);
                IReadOnlyList<Vector2Int> footprint =
                    definition.GetGridPosList(candidate);
                if (!rooms.TryGetRoom(grid, candidate, out RoomInstance room)
                    || room == null
                    || !room.IsUsable
                    || !footprint.All(room.ContainsCell)
                    || !geometry.AreInsideHorizontalBounds(grid, footprint, 1)
                    || !geometry.CanBuildInArea(grid, definition, footprint)
                    || !geometry.CanOccupy(
                        grid,
                        definition.Placement.Layer,
                        footprint)
                    || !geometry.HasSupportBelow(grid, footprint))
                    continue;
                bool reachable = BuildingWorkAccessRules.EnumerateCandidates(
                        footprint,
                        definition.IsGridMovement)
                    .Any(access => grid.IsValidGridPos(access)
                        && grid.IsWalkable(access)
                        && grid.GetMovePathTo(start, access)?.Count > 0);
                if (reachable)
                    anchor = candidate;
            }
        }
        Require(anchor.HasValue,
            "No legal reachable footprint exists for controlled RF87 placement.");
        DungeonStoryGridBuildingController controller =
            FindFirstObjectByType<DungeonStoryGridBuildingController>();
        string placementFailure = "building-controller-missing";
        Require(controller != null
            && controller.TryPlaceInitialBuildings(
                new[]
                {
                    new InitialBuildInfo
                    {
                        Building = definition,
                        Position = anchor.Value
                    }
                },
                out placementFailure),
            "Controlled RF87 placement failed: " + placementFailure);
        BuildableObject placed = buildings.Buildings
            .Where(value => value != null && !value.isDestroy)
            .OrderBy(value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault(value => ReferenceEquals(
                    value.BuildingData,
                    definition)
                && value.HasSemanticTag("workstation:v19:memorial"));
        Require(placed != null,
            "Placed RF87 was not published through the building query.");
        return placed;
    }

    private void Pause()
    {
        GameManager host = FindFirstObjectByType<GameManager>();
        if (host != null) host.isPause = true;
        if (timeScale != null) timeScale.Scale = 0;
    }

    private static void Click(string name)
    {
        Button button = Resources.FindObjectsOfTypeAll<Button>()
            .SingleOrDefault(value => value != null
                && value.name == name
                && value.gameObject.scene.isLoaded
                && value.gameObject.activeInHierarchy);
        Require(button != null
            && button.IsInteractable()
            && PlayModeVerificationFrameWait.DispatchPointerClick(
                button.gameObject,
                Vector2.zero),
            "Actual UI is unavailable: " + name);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException(reason);
    }
}
#endif
