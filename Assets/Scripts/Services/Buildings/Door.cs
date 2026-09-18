using System.Collections.Generic;
using UnityEngine;

public class Door : BuildableObject
{
    public SpriteRenderer VisualRenderer { get; protected set; }
    public virtual bool IsDungeonEntrance => true;
    protected virtual bool ChangesCharacterLayerDuringTraversal => true;
    public DoorAccessStateModule AccessStateModule { get; private set; }
    public DoorAccessPolicyState AccessPolicy => AccessStateModule?.State;
    public DoorOperationStateModule OperationStateModule { get; private set; }
    public bool IsOpen => OperationStateModule?.IsOpen == true;
    public bool IsHeldOpen => OperationStateModule?.IsHeldOpen == true;
    public int OperationRevision => OperationStateModule?.Revision ?? 0;
    public bool ContainsCell(Vector2Int position)
    {
        for (int i = 0; i < buildPoses.Count; i++) if (buildPoses[i] == position) return true;
        return false;
    }

    private readonly HashSet<object> traversalSubjects = new HashSet<object>();
    private readonly Dictionary<int, BuildingDoorTraversalSubjects> traversalSubjectCache =
        new Dictionary<int, BuildingDoorTraversalSubjects>();
    private Material doorVisualMaterial;
    private IDoorAccessStateChangeSink accessStateChangeSink;
    private IBuildingDoorTraversalSubjectPort traversalSubjectPort;
    private DoorAccessLockIndicator accessLockIndicator;

    protected Material DoorVisualMaterialAsset => doorVisualMaterial;

    [VContainer.Inject]
    public void ConstructDoorVisualResources(IGameContentCatalog content)
    {
        doorVisualMaterial = (content
            ?? throw new System.ArgumentNullException(nameof(content)))
            .Media.DoorSpriteMaterial;
    }

    [VContainer.Inject]
    public void ConstructDoorAccess(
        IDoorAccessStateChangeSink accessStateChangeSink,
        IBuildingDoorTraversalSubjectPort traversalSubjectPort)
    {
        this.accessStateChangeSink = accessStateChangeSink
            ?? throw new System.ArgumentNullException(nameof(accessStateChangeSink));
        this.traversalSubjectPort = traversalSubjectPort
            ?? throw new System.ArgumentNullException(nameof(traversalSubjectPort));
    }

    private void OnEnable()
    {
        if (!IsDungeonEntrance)
        {
            return;
        }

        RemoveLegacyInteriorVisual("DoorVisual");
        RemoveLegacyInteriorVisual(InteriorDoorVisualLayout.VisualObjectName);
        if (BuildingData != null)
        {
            ConfigureDungeonVisual(BuildingData);
            ConfigureTraversalCollider();
        }
    }

    private void OnDisable()
    {
        RestoreTrackedCharacterLayers();
    }

    public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)
    {
        base.Initialization(buildingSO, buildPos);
        AccessStateModule = new DoorAccessStateModule(
            () =>
            {
                accessStateChangeSink?.NotifyDoorPolicyChanged();
                RefreshAccessIndicator();
            });
        RegisterStateModule(AccessStateModule);
        OperationStateModule = new DoorOperationStateModule(RefreshOperationVisual);
        RegisterStateModule(OperationStateModule);
        RefreshAccessIndicator();
        if (IsDungeonEntrance)
        {
            ConfigureDungeonVisual(buildingSO);
            ConfigureTraversalCollider();
        }

        BoxCollider2D doorCollider = GetComponent<BoxCollider2D>();
        if (doorCollider != null)
        {
            doorCollider.isTrigger = true;
        }
    }

    private void RefreshAccessIndicator()
    {
        if (accessLockIndicator == null)
        {
            accessLockIndicator = GetComponent<DoorAccessLockIndicator>();
        }

        if (accessLockIndicator == null)
        {
            accessLockIndicator = gameObject.AddComponent<DoorAccessLockIndicator>();
        }

        accessLockIndicator.Refresh(AccessPolicy?.IsRestricted == true);
    }

    [GameplayInternalOnly("Actual movement admission, never path enumeration", "AbilityMoveTraversalGuard;WildlifeActor")]
    internal bool TryOpenForTraversal(GridTraversalContext context, IDoorAccessQuery access, out string reason)
    {
        if (!isActiveAndEnabled || isDestroy || IsDetachedRestoreCandidate || OperationStateModule == null)
        { reason = "door-unavailable"; return false; }
        if (!access.CanUse(this, context, out reason)) return false;
        OperationStateModule.Set(true, IsHeldOpen);
        return true;
    }

    [GameplayInternalOnly("Existing player door command adapter", "DoorAccessUnityAdapter")]
    internal bool SetHeldOpen(bool held)
    {
        if (!isActiveAndEnabled || isDestroy || IsDetachedRestoreCandidate || OperationStateModule == null) return false;
        OperationStateModule.Set(held || IsOccupied(), held);
        return true;
    }

    private bool IsOccupied()
    {
        if (Grid == null) return true; // Cannot safely close until bound to a world.
        foreach (Vector2Int position in buildPoses)
        {
            GridCell cell = Grid.GetGridCell(position);
            if (cell?.GetOccupant(GridLayer.Character) != null
                || cell?.GetOccupant(GridLayer.DownedCharacter) != null
                || cell?.GetOccupant(GridLayer.Wildlife) != null) return true;
        }
        return traversalSubjectPort.IsDoorPassageOccupied(this);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || IsDetachedRestoreCandidate || OperationStateModule == null) return;
        if (IsOpen && !IsHeldOpen && !IsOccupied()) OperationStateModule.Set(false, false);
        RefreshOperationVisual();
    }

    private void RefreshOperationVisual()
    {
        if (VisualRenderer != null)
            VisualRenderer.transform.localRotation = Quaternion.Euler(0f, IsOpen ? 70f : 0f, 0f);
    }

    private void ConfigureTraversalCollider()
    {
        BoxCollider2D doorCollider = GetComponent<BoxCollider2D>();
        if (doorCollider == null)
        {
            return;
        }

        doorCollider.isTrigger = true;
        doorCollider.size = DungeonDoorVisualLayout.TraversalColliderSize;
        doorCollider.offset = DungeonDoorVisualLayout.TraversalColliderOffset;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        BuildingDoorTraversalSubjects subjects = ResolveTraversalSubjects(collision);
        KeepSubjectBehindWall(subjects.First);
        KeepSubjectBehindWall(subjects.Second);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        BuildingDoorTraversalSubjects subjects = GetCachedTraversalSubjects(collision);
        KeepSubjectBehindWall(subjects.First);
        KeepSubjectBehindWall(subjects.Second);
    }

    private void RemoveLegacyInteriorVisual(string childName)
    {
        Transform legacyVisual = transform.Find(childName);
        if (legacyVisual == null)
        {
            return;
        }

        legacyVisual.gameObject.SetActive(false);
        if (Application.isPlaying)
        {
            Destroy(legacyVisual.gameObject);
        }
        else
        {
            DestroyImmediate(legacyVisual.gameObject);
        }
    }

    private void ConfigureDungeonVisual(BuildingSO buildingSO)
    {
        Sprite sprite = buildingSO != null
            ? buildingSO.sprite != null ? buildingSO.sprite : buildingSO.icon
            : null;
        Transform visualTransform = transform.Find(DungeonDoorVisualLayout.VisualObjectName);
        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject(DungeonDoorVisualLayout.VisualObjectName);
            visualTransform = visualObject.transform;
            visualTransform.SetParent(transform, false);
        }

        VisualRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (VisualRenderer == null)
        {
            VisualRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        visualTransform.localPosition = Vector3.zero;
        visualTransform.localRotation = Quaternion.identity;
        visualTransform.localScale = DungeonDoorVisualLayout.CalculateScale(sprite);
        VisualRenderer.sprite = sprite;
        VisualRenderer.color = Color.white;
        DoorVisualMaterial.Apply(VisualRenderer, doorVisualMaterial);
        VisualRenderer.sortingLayerName = DungeonDoorVisualLayout.SortingLayerName;
        VisualRenderer.sortingOrder = DungeonDoorVisualLayout.SortingOrder;
        VisualRenderer.enabled = sprite != null;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        int colliderId = collision != null ? collision.GetInstanceID() : 0;
        BuildingDoorTraversalSubjects subjects = GetCachedTraversalSubjects(collision);
        RestoreSubjectLayer(subjects.First);
        RestoreSubjectLayer(subjects.Second);
        traversalSubjectCache.Remove(colliderId);
    }

    private BuildingDoorTraversalSubjects GetCachedTraversalSubjects(
        Collider2D collision)
    {
        if (collision == null)
        {
            return default;
        }

        int colliderId = collision.GetInstanceID();
        if (!traversalSubjectCache.TryGetValue(
                colliderId,
                out BuildingDoorTraversalSubjects subjects))
        {
            subjects = ResolveTraversalSubjects(collision);
            traversalSubjectCache[colliderId] = subjects;
        }

        return subjects;
    }

    private BuildingDoorTraversalSubjects ResolveTraversalSubjects(
        Collider2D collision)
    {
        if (BuildingData == null
            || !ChangesCharacterLayerDuringTraversal
            || collision == null
            || traversalSubjectPort == null)
        {
            return default;
        }

        return traversalSubjectPort.ResolveTraversalSubjects(collision);
    }

    private void KeepSubjectBehindWall(object subject)
    {
        if (traversalSubjectPort == null
            || !traversalSubjectPort.IsTraversalSubjectAvailable(subject))
        {
            return;
        }

        traversalSubjects.Add(subject);
        traversalSubjectPort.ChangeTraversalSortingLayer(
            subject,
            DungeonDoorVisualLayout.TraversalSortingLayerName);
    }

    private void RestoreSubjectLayer(object subject)
    {
        if (traversalSubjectPort == null
            || !traversalSubjectPort.IsTraversalSubjectAvailable(subject))
        {
            return;
        }

        traversalSubjects.Remove(subject);
        traversalSubjectPort.ChangeTraversalSortingLayer(
            subject,
            DungeonDoorVisualLayout.DefaultCharacterSortingLayerName);
    }

    private void RestoreTrackedCharacterLayers()
    {
        if (traversalSubjectPort != null)
        {
            foreach (object subject in traversalSubjects)
            {
                if (traversalSubjectPort.IsTraversalSubjectAvailable(subject))
                {
                    traversalSubjectPort.ChangeTraversalSortingLayer(
                        subject,
                        DungeonDoorVisualLayout.DefaultCharacterSortingLayerName);
                }
            }
        }

        traversalSubjects.Clear();
        traversalSubjectCache.Clear();
    }
}
