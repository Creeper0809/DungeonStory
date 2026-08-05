using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public enum FacilityOperationalVisualState
{
    Idle = 0,
    Operating = 1,
    MaterialMissing = 2,
    Unpowered = 3,
    DrainBlocked = 4,
    Fault = 5,
    Completed = 6
}

[DisallowMultipleComponent]
public sealed class FacilityOperationalPresentation : MonoBehaviour
{
    private const float PixelSize =
        1f / WorldInteractionPresentationCatalogSO.PixelsPerUnit;
    private BuildableObject building;
    private SpriteRenderer buildingRenderer;
    private SpriteRenderer statusRenderer;
    private FacilityOperationalVisualState state;
    private FacilityOperationalVisualState previousState;
    private float completedUntil = float.NegativeInfinity;
    private int lastPulseFrame = int.MinValue;
    private bool visible;

    public FacilityOperationalVisualState State => state;
    public SpriteRenderer StatusRenderer => statusRenderer;

    public static FacilityOperationalPresentation Ensure(BuildableObject building)
    {
        if (building == null)
        {
            return null;
        }

        FacilityOperationalPresentation presentation =
            building.GetComponent<FacilityOperationalPresentation>();
        if (presentation == null && Application.isPlaying)
        {
            presentation =
                building.gameObject.AddComponent<FacilityOperationalPresentation>();
        }

        presentation?.Configure(building);
        return presentation;
    }

    public void Configure(BuildableObject building)
    {
        this.building = building;
        EnsureRenderer();
        ResolveBuildingRenderer();
        RefreshAnchorAndSorting();
    }

    public void TickPresentation(
        FacilityOperationalVisualState requestedState,
        bool isVisible,
        float gameTime,
        bool reducedMotion)
    {
        if (visible != isVisible)
        {
            visible = isVisible;
            if (!visible)
            {
                statusRenderer.enabled = false;
                return;
            }
        }

        if (previousState == FacilityOperationalVisualState.Operating
            && requestedState == FacilityOperationalVisualState.Idle)
        {
            completedUntil = gameTime + 0.8f;
        }

        previousState = requestedState;
        FacilityOperationalVisualState resolved =
            requestedState == FacilityOperationalVisualState.Idle
                && gameTime <= completedUntil
                    ? FacilityOperationalVisualState.Completed
                    : requestedState;
        if (state != resolved)
        {
            state = resolved;
            ApplyStateVisual();
        }

        if (!visible || state == FacilityOperationalVisualState.Idle)
        {
            statusRenderer.enabled = false;
            return;
        }

        RefreshAnchorAndSorting();
        int pulseFrame = Mathf.Abs(Mathf.FloorToInt(gameTime / 0.2f)) % 4;
        if (pulseFrame == lastPulseFrame)
        {
            return;
        }

        lastPulseFrame = pulseFrame;
        float y = reducedMotion
            || pulseFrame == 0
            || pulseFrame == 3
                ? 0f
                : PixelSize;
        Vector3 local = statusRenderer.transform.localPosition;
        local.y = ResolveBaseLocalY() + y;
        statusRenderer.transform.localPosition = local;
    }

    public void Hide()
    {
        visible = false;
        if (statusRenderer != null)
        {
            statusRenderer.enabled = false;
        }
    }

    private void ApplyStateVisual()
    {
        EnsureRenderer();
        CharacterPresentationSpriteKind spriteKind;
        Color color;
        switch (state)
        {
            case FacilityOperationalVisualState.Operating:
                spriteKind = CharacterPresentationSpriteKind.Spark;
                color = new Color32(117, 219, 192, 235);
                break;
            case FacilityOperationalVisualState.MaterialMissing:
                spriteKind = CharacterPresentationSpriteKind.Crate;
                color = new Color32(229, 177, 81, 245);
                break;
            case FacilityOperationalVisualState.Unpowered:
                spriteKind = CharacterPresentationSpriteKind.Spark;
                color = new Color32(108, 139, 188, 245);
                break;
            case FacilityOperationalVisualState.DrainBlocked:
                spriteKind = CharacterPresentationSpriteKind.Bubble;
                color = new Color32(139, 105, 74, 245);
                break;
            case FacilityOperationalVisualState.Fault:
                spriteKind = CharacterPresentationSpriteKind.Hammer;
                color = new Color32(211, 88, 74, 245);
                break;
            case FacilityOperationalVisualState.Completed:
                spriteKind = CharacterPresentationSpriteKind.Spark;
                color = new Color32(152, 225, 98, 245);
                break;
            default:
                statusRenderer.enabled = false;
                return;
        }

        statusRenderer.sprite = CharacterPresentationSpriteFactory.Get(spriteKind);
        statusRenderer.color = color;
        statusRenderer.enabled = visible;
    }

    private void EnsureRenderer()
    {
        if (statusRenderer != null)
        {
            return;
        }

        Transform existing = transform.Find("FacilityStatusEffect");
        GameObject target = existing != null
            ? existing.gameObject
            : new GameObject("FacilityStatusEffect");
        target.transform.SetParent(transform, worldPositionStays: false);
        statusRenderer = target.GetComponent<SpriteRenderer>();
        if (statusRenderer == null)
        {
            statusRenderer = target.AddComponent<SpriteRenderer>();
        }

        statusRenderer.enabled = false;
    }

    private void RefreshAnchorAndSorting()
    {
        EnsureRenderer();
        if (buildingRenderer == null)
        {
            statusRenderer.transform.localPosition =
                new Vector3(0f, PixelSize, 0f);
            return;
        }

        statusRenderer.sortingLayerID = buildingRenderer.sortingLayerID;
        statusRenderer.sortingOrder = buildingRenderer.sortingOrder + 3;
        Vector3 worldAnchor = new Vector3(
            buildingRenderer.bounds.center.x,
            buildingRenderer.bounds.max.y + (2f * PixelSize),
            building.transform.position.z);
        statusRenderer.transform.localPosition =
            building.transform.InverseTransformPoint(worldAnchor);
    }

    private float ResolveBaseLocalY()
    {
        if (buildingRenderer == null)
        {
            return PixelSize;
        }

        Vector3 worldAnchor = new Vector3(
            buildingRenderer.bounds.center.x,
            buildingRenderer.bounds.max.y + (2f * PixelSize),
            building.transform.position.z);
        return building.transform.InverseTransformPoint(worldAnchor).y;
    }

    private void ResolveBuildingRenderer()
    {
        buildingRenderer = null;
        if (building == null)
        {
            return;
        }

        SpriteRenderer[] renderers =
            building.GetComponentsInChildren<SpriteRenderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer candidate = renderers[index];
            if (candidate != null && candidate != statusRenderer)
            {
                buildingRenderer = candidate;
                return;
            }
        }
    }
}

public sealed class FacilityOperationalPresentationScheduler :
    ITickable,
    IDisposable
{
    private const int MaximumFacilitiesPerTick = 32;
    private const float ViewportMargin = 0.08f;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IPowerInfrastructureQuery power;
    private readonly IAutomationInfrastructureQuery automation;
    private readonly IMainCameraProvider cameraProvider;
    private readonly IGameClock gameClock;
    private readonly IDungeonUserSettingsService userSettings;
    private readonly Dictionary<BuildableObject, FacilityOperationalPresentation>
        presentations =
            new Dictionary<BuildableObject, FacilityOperationalPresentation>();
    private readonly List<BuildableObject> orderedBuildings =
        new List<BuildableObject>();
    private int buildingVersion = int.MinValue;
    private int cursor;

    public FacilityOperationalPresentationScheduler(
        ICharacterAiWorldRegistry worldRegistry,
        IPowerInfrastructureQuery power,
        IAutomationInfrastructureQuery automation,
        IMainCameraProvider cameraProvider,
        IGameClock gameClock,
        IDungeonUserSettingsService userSettings)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.automation = automation
            ?? throw new ArgumentNullException(nameof(automation));
        this.cameraProvider = cameraProvider
            ?? throw new ArgumentNullException(nameof(cameraProvider));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.userSettings = userSettings
            ?? throw new ArgumentNullException(nameof(userSettings));
    }

    public void Tick()
    {
        SynchronizeBuildingsIfNeeded();
        if (orderedBuildings.Count == 0)
        {
            return;
        }

        Camera camera = null;
        try
        {
            camera = cameraProvider.Camera;
        }
        catch (InvalidOperationException)
        {
            // Scene teardown can release the camera first.
        }

        int count = Mathf.Min(MaximumFacilitiesPerTick, orderedBuildings.Count);
        for (int index = 0; index < count; index++)
        {
            if (cursor >= orderedBuildings.Count)
            {
                cursor = 0;
            }

            BuildableObject building = orderedBuildings[cursor++];
            if (building == null
                || building.isDestroy
                || !presentations.TryGetValue(
                    building,
                    out FacilityOperationalPresentation presentation))
            {
                continue;
            }

            bool visible = IsInsideViewport(camera, building.transform.position);
            presentation.TickPresentation(
                ResolveState(building),
                visible,
                gameClock.Time,
                userSettings.Current.reducedMotion);
        }
    }

    public void Dispose()
    {
        foreach (FacilityOperationalPresentation presentation
                 in presentations.Values)
        {
            presentation?.Hide();
        }

        presentations.Clear();
        orderedBuildings.Clear();
        cursor = 0;
    }

    private void SynchronizeBuildingsIfNeeded()
    {
        if (buildingVersion == worldRegistry.BuildingVersion)
        {
            return;
        }

        buildingVersion = worldRegistry.BuildingVersion;
        presentations.Clear();
        orderedBuildings.Clear();
        IReadOnlyList<BuildableObject> buildings = worldRegistry.Buildings;
        for (int index = 0; index < buildings.Count; index++)
        {
            BuildableObject building = buildings[index];
            if (building == null || building.isDestroy)
            {
                continue;
            }

            FacilityOperationalPresentation presentation =
                FacilityOperationalPresentation.Ensure(building);
            if (presentation == null)
            {
                continue;
            }

            orderedBuildings.Add(building);
            presentations[building] = presentation;
        }

        orderedBuildings.Sort(CompareBuildings);
        cursor = orderedBuildings.Count > 0 ? cursor % orderedBuildings.Count : 0;
    }

    private FacilityOperationalVisualState ResolveState(BuildableObject building)
    {
        if (building.IsDamaged)
        {
            return FacilityOperationalVisualState.Fault;
        }

        if (automation.TryGetFacility(
                building,
                out AutomationFacilitySnapshot automationState))
        {
            string reason = automationState.Status.Code.ToString();
            if (Contains(reason, "전력") || !automationState.Powered)
            {
                return FacilityOperationalVisualState.Unpowered;
            }

            if (Contains(reason, "배수") || Contains(reason, "폐수"))
            {
                return FacilityOperationalVisualState.DrainBlocked;
            }

            if (Contains(reason, "고장")
                || automationState.Fault >= 0.5f
                || automationState.Maintenance <= 0f)
            {
                return FacilityOperationalVisualState.Fault;
            }

            if (Contains(reason, "재료")
                || Contains(reason, "입력")
                || Contains(reason, "재고")
                || Contains(reason, "공간"))
            {
                return FacilityOperationalVisualState.MaterialMissing;
            }

            if (automationState.Operational)
            {
                return FacilityOperationalVisualState.Operating;
            }
        }

        BuildingPowerConsumerAbility consumer =
            building.BuildingData?.GetAbility<BuildingPowerConsumerAbility>();
        if (consumer != null && !power.IsPowered(building))
        {
            return FacilityOperationalVisualState.Unpowered;
        }

        return building.WorkerReservation != null || building.CurrentUserCount > 0
            ? FacilityOperationalVisualState.Operating
            : FacilityOperationalVisualState.Idle;
    }

    private static bool IsInsideViewport(Camera camera, Vector3 worldPosition)
    {
        if (camera == null)
        {
            return false;
        }

        Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
        return viewport.z >= 0f
            && viewport.x >= -ViewportMargin
            && viewport.x <= 1f + ViewportMargin
            && viewport.y >= -ViewportMargin
            && viewport.y <= 1f + ViewportMargin;
    }

    private static bool Contains(string value, string token)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int CompareBuildings(
        BuildableObject first,
        BuildableObject second)
    {
        int idCompare = first.id.CompareTo(second.id);
        if (idCompare != 0)
        {
            return idCompare;
        }

        int xCompare = first.centerPos.x.CompareTo(second.centerPos.x);
        return xCompare != 0
            ? xCompare
            : first.centerPos.y.CompareTo(second.centerPos.y);
    }
}
