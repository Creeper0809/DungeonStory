using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class OwnerCommandSelectionState
{
    private readonly List<CharacterActor> actors = new();
    private CharacterActor primary;

    internal CharacterActor Primary
    {
        get
        {
            Prune();
            return primary;
        }
        set => primary = value;
    }

    internal IReadOnlyList<CharacterActor> Actors
    {
        get
        {
            Prune();
            return actors;
        }
    }

    internal int Count => actors.Count;

    internal IReadOnlyList<CharacterActor> GetCommandActors()
    {
        Prune();
        if (actors.Count > 0)
        {
            return actors.ToArray();
        }

        return primary != null
            ? new[] { primary }
            : Array.Empty<CharacterActor>();
    }

    internal bool Add(CharacterActor actor)
    {
        if (actor == null || actors.Contains(actor))
        {
            return false;
        }

        actors.Add(actor);
        if (Application.isPlaying)
        {
            WorldCharacterNameplate.Ensure(actor)?.SetCommandSelected(true);
        }
        return true;
    }

    internal void Clear()
    {
        foreach (CharacterActor actor in actors)
        {
            if (actor != null
                && actor.TryGetComponent(out WorldCharacterNameplate nameplate))
            {
                nameplate.SetCommandSelected(false);
            }
        }

        actors.Clear();
        primary = null;
    }

    internal void Prune()
    {
        for (int index = actors.Count - 1; index >= 0; index--)
        {
            CharacterActor actor = actors[index];
            if (IsCommandable(actor))
            {
                continue;
            }

            if (actor != null
                && actor.TryGetComponent(out WorldCharacterNameplate nameplate))
            {
                nameplate.SetCommandSelected(false);
            }
            actors.RemoveAt(index);
        }

        if (!IsCommandable(primary))
        {
            primary = actors.LastOrDefault();
        }
    }

    internal static bool IsCommandable(CharacterActor actor)
    {
        return actor != null
            && !actor.IsDead
            && actor.TryGetAbility(out AbilityWork _);
    }
}

internal sealed class OwnerCommandDragSelector
{
    private readonly OwnerCommandSelectionState selection;
    private readonly IPlayerInputReader input;
    private readonly IUiPointerBlocker uiPointerBlocker;
    private readonly IMainCameraProvider mainCameraProvider;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private bool tracking;
    private Vector3 startScreenPosition;

    internal OwnerCommandDragSelector(
        OwnerCommandSelectionState selection,
        IPlayerInputReader input,
        IUiPointerBlocker uiPointerBlocker,
        IMainCameraProvider mainCameraProvider,
        ICharacterAiWorldRegistry worldRegistry)
    {
        this.selection = selection
            ?? throw new ArgumentNullException(nameof(selection));
        this.input = input ?? throw new ArgumentNullException(nameof(input));
        this.uiPointerBlocker = uiPointerBlocker
            ?? throw new ArgumentNullException(nameof(uiPointerBlocker));
        this.mainCameraProvider = mainCameraProvider
            ?? throw new ArgumentNullException(nameof(mainCameraProvider));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
    }

    internal void Update(
        Camera targetCamera,
        float thresholdPixels,
        bool additive)
    {
        if (input.GetMouseButtonDown(0))
        {
            tracking = !uiPointerBlocker.IsPointerOverUi();
            startScreenPosition = input.MousePosition;
            return;
        }

        if (!tracking || input.GetMouseButton(0))
        {
            return;
        }

        tracking = false;
        Vector3 end = input.MousePosition;
        if (uiPointerBlocker.IsPointerOverUi()
            || Vector2.Distance(startScreenPosition, end) < thresholdPixels)
        {
            return;
        }

        SelectActorsInScreenRect(targetCamera, startScreenPosition, end, additive);
    }

    internal int SelectActorsInScreenRect(
        Camera targetCamera,
        Vector2 start,
        Vector2 end,
        bool additive)
    {
        Camera camera = targetCamera != null
            ? targetCamera
            : mainCameraProvider.Camera;
        if (camera == null)
        {
            return 0;
        }

        Rect selectionRect = Rect.MinMaxRect(
            Mathf.Min(start.x, end.x),
            Mathf.Min(start.y, end.y),
            Mathf.Max(start.x, end.x),
            Mathf.Max(start.y, end.y));
        if (!additive)
        {
            selection.Clear();
        }

        int added = 0;
        foreach (CharacterActor candidate in worldRegistry.Characters)
        {
            CharacterActor actor = CharacterActorCollection.GetCanonical(candidate);
            if (!OwnerCommandSelectionState.IsCommandable(actor)
                || actor.TryGetComponent(out InvasionIntruderRuntime _))
            {
                continue;
            }

            Vector3 screen = camera.WorldToScreenPoint(actor.transform.position);
            if (screen.z >= 0f
                && selectionRect.Contains(screen)
                && selection.Add(actor))
            {
                added++;
            }
        }

        selection.Primary = selection.Actors.LastOrDefault();
        return added;
    }

    internal void Reset()
    {
        tracking = false;
    }
}

internal sealed class OwnerCommandInfoFeedBridge
{
    private readonly IGameEventBus eventBus;
    private readonly Action<InfoFeedEvent> onSelected;
    private IDisposable subscription;

    internal OwnerCommandInfoFeedBridge(
        IGameEventBus eventBus,
        Action<InfoFeedEvent> onSelected)
    {
        this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        this.onSelected = onSelected ?? throw new ArgumentNullException(nameof(onSelected));
    }

    internal void Enable(bool ownerIsActive)
    {
        if (ownerIsActive)
        {
            subscription ??= eventBus.Subscribe<InfoFeedEvent>(onSelected);
        }
    }

    internal void Disable()
    {
        subscription?.Dispose();
        subscription = null;
    }
}
