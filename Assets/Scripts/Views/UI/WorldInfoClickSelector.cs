using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IWorldInfoClickSelector
{
    bool TryHandleWorldInfoClick();
    bool TryTriggerCharacterUnderPointer();
    bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor);
    bool TryGetPreferredCharacterAtScreenPosition(Vector3 screenPosition, Camera camera, out CharacterActor actor);
    bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor);
}

public sealed class WorldInfoClickSelectionService : IWorldInfoClickSelector
{
    private const int WorldClickGraceFrames = 1;

    private readonly IGridSystemProvider gridSystemProvider;
    private readonly IMainCameraProvider mainCameraProvider;
    private readonly IPlayerInputReader inputReader;
    private readonly IUiPointerBlocker uiPointerBlocker;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IGameEventBus gameEventBus;
    private readonly IGameClock gameClock;
    private int lastHandledFrame = -1;
    private UnityEngine.Object lastHandledTarget;

    public WorldInfoClickSelectionService(
        IGridSystemProvider gridSystemProvider,
        IMainCameraProvider mainCameraProvider,
        IPlayerInputReader inputReader,
        IUiPointerBlocker uiPointerBlocker,
        IWorldItemStackRuntime itemStackRuntime,
        IGameEventBus gameEventBus,
        IGameClock gameClock)
    {
        this.gridSystemProvider = gridSystemProvider ?? throw new ArgumentNullException(nameof(gridSystemProvider));
        this.mainCameraProvider = mainCameraProvider ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        this.inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        this.uiPointerBlocker = uiPointerBlocker ?? throw new ArgumentNullException(nameof(uiPointerBlocker));
        this.itemStackRuntime = itemStackRuntime ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public bool TryHandleWorldInfoClick()
    {
        if (IsWorldClickConsumed())
        {
            return true;
        }

        if (!CanSelectWorldInfo())
        {
            return false;
        }

        bool hasPhysicsHits = TryGetPointerHits(out Collider2D[] hits);
        bool forceItem = inputReader.GetKey(KeyCode.LeftAlt) || inputReader.GetKey(KeyCode.RightAlt);
        CharacterActor actor;
        if (!forceItem && hasPhysicsHits && TryGetPreferredCharacter(hits, out actor))
        {
            TriggerCharacter(actor);
            return true;
        }

        if (!forceItem && hasPhysicsHits && TryGetPreferredWildlife(hits, out WildlifeActor wildlife))
        {
            TriggerWildlife(wildlife);
            return true;
        }

        if (TryGetItemPileUnderPointer(out ItemPileInfoTarget itemTarget, out UnityEngine.Object markerObject))
        {
            MarkWorldClickHandled(markerObject);
            gameEventBus.ShowInfo(itemTarget);
            return true;
        }

        if (forceItem && hasPhysicsHits && TryGetPreferredCharacter(hits, out actor))
        {
            TriggerCharacter(actor);
            return true;
        }

        if (hasPhysicsHits && TryGetPreferredWildlife(hits, out wildlife))
        {
            TriggerWildlife(wildlife);
            return true;
        }

        if ((hasPhysicsHits && TryGetPreferredBuilding(hits, out BuildableObject building))
            || TryGetExactGridStructureUnderPointer(out building))
        {
            MarkWorldClickHandled(building);
            building.TriggerWorldInfoClick();
            return true;
        }

        return false;
    }

    private bool TryGetItemPileUnderPointer(
        out ItemPileInfoTarget target,
        out UnityEngine.Object markerObject)
    {
        target = null;
        markerObject = null;
        GridSystemManager manager = gridSystemProvider.Manager;
        Grid grid = manager != null ? manager.grid : null;
        Camera camera = mainCameraProvider.Camera;
        if (grid == null || camera == null)
        {
            return false;
        }

        Vector3 screenPosition = inputReader.MousePosition;
        screenPosition.z = -camera.transform.position.z;
        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        Vector2Int cell = grid.GetXY(worldPosition);
        return grid.IsValidGridPos(cell)
            && itemStackRuntime.TryGetPileTargetAt(cell, out target, out markerObject);
    }

    private bool TryGetExactGridStructureUnderPointer(out BuildableObject building)
    {
        building = null;
        GridSystemManager manager = gridSystemProvider.Manager;
        Grid grid = manager != null ? manager.grid : null;
        Camera camera = mainCameraProvider.Camera;
        if (grid == null || camera == null)
        {
            return false;
        }

        Vector3 screenPosition = inputReader.MousePosition;
        screenPosition.z = -camera.transform.position.z;
        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        Vector2Int cell = grid.GetXY(worldPosition);
        if (!grid.IsValidGridPos(cell))
        {
            return false;
        }

        building = grid.GetGridCell(cell)?.GetOccupant(GridLayer.Building) as BuildableObject;
        return building != null
            && !building.isDestroy
            && IsExactGridStructureDefinition(building.BuildingData);
    }

    public static bool IsExactGridStructureDefinition(BuildingSO buildingData)
    {
        return buildingData != null
            && buildingData.Placement.Layer == GridLayer.Building
            && (buildingData.IsStructuralWall || buildingData.IsInteriorDoor);
    }

    public bool TryTriggerCharacterUnderPointer()
    {
        if (IsWorldClickConsumed())
        {
            return true;
        }

        if (!CanSelectWorldInfo() || !TryGetPreferredCharacterUnderPointer(out CharacterActor actor))
        {
            return false;
        }

        TriggerCharacter(actor);
        return true;
    }

    public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)
    {
        return TryGetPreferredCharacterAtScreenPosition(inputReader.MousePosition, mainCameraProvider.Camera, out actor);
    }

    public bool TryGetPreferredCharacterAtScreenPosition(Vector3 screenPosition, Camera camera, out CharacterActor actor)
    {
        if (!TryGetHitsAtScreenPosition(screenPosition, camera, out Collider2D[] hits))
        {
            actor = null;
            return false;
        }

        return TryGetPreferredCharacter(hits, out actor);
    }

    public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)
    {
        actor = null;
        int bestScore = int.MinValue;
        float bestZ = float.NegativeInfinity;

        if (hits == null)
        {
            return false;
        }

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            CharacterActor candidate = hit.GetComponentInParent<CharacterActor>();
            if (candidate == null)
            {
                continue;
            }

            int score = GetCharacterClickPriority(candidate);
            float z = candidate.transform.position.z;
            if (actor != null && (score < bestScore || (score == bestScore && z <= bestZ)))
            {
                continue;
            }

            actor = candidate;
            bestScore = score;
            bestZ = z;
        }

        return actor != null;
    }

    private bool CanSelectWorldInfo()
    {
        return gridSystemProvider.Manager.Mode == GridMode.None
            && !uiPointerBlocker.IsPointerOverUi();
    }

    private bool TryGetPointerHits(out Collider2D[] hits)
    {
        return TryGetHitsAtScreenPosition(inputReader.MousePosition, mainCameraProvider.Camera, out hits);
    }

    private static bool TryGetHitsAtScreenPosition(
        Vector3 screenPosition,
        Camera camera,
        out Collider2D[] hits)
    {
        hits = null;
        if (camera == null)
        {
            return false;
        }

        screenPosition.z = -camera.transform.position.z;
        Vector3 worldPosition = camera.ScreenToWorldPoint(screenPosition);
        hits = Physics2D.OverlapPointAll(worldPosition);
        return hits.Length > 0;
    }

    private static bool TryGetPreferredBuilding(Collider2D[] hits, out BuildableObject building)
    {
        building = null;
        int bestLayer = int.MinValue;
        int bestOrder = int.MinValue;
        float bestZ = float.NegativeInfinity;

        foreach (Collider2D hit in hits)
        {
            BuildableObject candidate = hit != null ? hit.GetComponentInParent<BuildableObject>() : null;
            if (candidate == null
                || candidate.isDestroy
                || (candidate is not ConstructionSite
                    && candidate.BuildingData?.Placement.Layer == GridLayer.Hallway))
            {
                continue;
            }

            int candidateLayer = int.MinValue;
            int candidateOrder = int.MinValue;
            foreach (Renderer renderer in candidate.GetComponentsInChildren<Renderer>(true))
            {
                int rendererLayer = SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
                if (rendererLayer > candidateLayer
                    || (rendererLayer == candidateLayer && renderer.sortingOrder > candidateOrder))
                {
                    candidateLayer = rendererLayer;
                    candidateOrder = renderer.sortingOrder;
                }
            }

            if (candidate is ConstructionSite)
            {
                candidateLayer = int.MaxValue / 2;
                candidateOrder = int.MaxValue / 2;
            }

            float candidateZ = candidate.transform.position.z;
            bool isBehindCurrent = building != null
                && (candidateLayer < bestLayer
                    || (candidateLayer == bestLayer && candidateOrder < bestOrder)
                    || (candidateLayer == bestLayer && candidateOrder == bestOrder && candidateZ <= bestZ));
            if (isBehindCurrent)
            {
                continue;
            }

            building = candidate;
            bestLayer = candidateLayer;
            bestOrder = candidateOrder;
            bestZ = candidateZ;
        }

        return building != null;
    }

    private static bool TryGetPreferredWildlife(Collider2D[] hits, out WildlifeActor wildlife)
    {
        wildlife = null;
        int bestScore = int.MinValue;
        float bestZ = float.NegativeInfinity;
        if (hits == null)
        {
            return false;
        }

        foreach (Collider2D hit in hits)
        {
            WildlifeActor candidate = hit != null ? hit.GetComponentInParent<WildlifeActor>() : null;
            if (candidate == null || !candidate.IsAlive)
            {
                continue;
            }

            int score = candidate.IsDangerous ? 50 : 10;
            if (candidate.HuntDesignated)
            {
                score += 25;
            }

            float z = candidate.transform.position.z;
            if (wildlife != null && (score < bestScore || (score == bestScore && z <= bestZ)))
            {
                continue;
            }

            wildlife = candidate;
            bestScore = score;
            bestZ = z;
        }

        return wildlife != null;
    }

    private static int GetCharacterClickPriority(CharacterActor actor)
    {
        int priority = 0;
        if (actor.characterType == CharacterType.NPC)
        {
            priority += 100;
        }

        if (actor.TryGetAbility(out AbilityWork _))
        {
            priority += 50;
        }

        if (!actor.IsDead)
        {
            priority += 10;
        }

        SpriteRenderer renderer = actor.VisualRenderer;
        if (renderer != null)
        {
            priority += renderer.sortingOrder;
        }

        return priority;
    }

    private void TriggerCharacter(CharacterActor actor)
    {
        if (lastHandledFrame == gameClock.FrameCount && lastHandledTarget == actor)
        {
            return;
        }

        MarkWorldClickHandled(actor);
        gameEventBus.ShowInfo(actor);
    }

    private void TriggerWildlife(WildlifeActor wildlife)
    {
        if (lastHandledFrame == gameClock.FrameCount && lastHandledTarget == wildlife)
        {
            return;
        }

        MarkWorldClickHandled(wildlife);
        gameEventBus.ShowInfo(wildlife);
    }

    private void MarkWorldClickHandled(UnityEngine.Object target)
    {
        lastHandledFrame = gameClock.FrameCount;
        lastHandledTarget = target;
    }

    private bool IsWorldClickConsumed()
    {
        if (lastHandledTarget == null)
        {
            return false;
        }

        int elapsedFrames = gameClock.FrameCount - lastHandledFrame;
        return elapsedFrames >= 0
            && elapsedFrames <= WorldClickGraceFrames;
    }
}

public sealed class WorldInfoClickInputController : ITickable
{
    private readonly IPlayerInputReader inputReader;
    private readonly IWorldInfoClickSelector clickSelector;
    private bool wasLeftButtonPressed;

    public WorldInfoClickInputController(
        IPlayerInputReader inputReader,
        IWorldInfoClickSelector clickSelector)
    {
        this.inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
        this.clickSelector = clickSelector ?? throw new ArgumentNullException(nameof(clickSelector));
    }

    public void Tick()
    {
        bool isLeftButtonPressed = inputReader.GetMouseButton(0);
        if (isLeftButtonPressed && !wasLeftButtonPressed)
        {
            clickSelector.TryHandleWorldInfoClick();
        }

        wasLeftButtonPressed = isLeftButtonPressed;
    }
}
