using System;
using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[Serializable]
public sealed class CharacterExpeditionRecoveryState
{
    [Range(0f, 100f)] public float stress;

    public CharacterExpeditionRecoveryState Clone()
    {
        return new CharacterExpeditionRecoveryState
        {
            stress = Mathf.Clamp(stress, 0f, 100f)
        };
    }

    public void CopyFrom(CharacterExpeditionRecoveryState source)
    {
        stress = Mathf.Clamp(source?.stress ?? 0f, 0f, 100f);
    }
}

[DisallowMultipleComponent]
public class CharacterLifecycle : SerializedMonoBehaviour
{
    [SerializeField]
    [ReadOnly]
    private CharacterActor actor;
    [SerializeField]
    [ReadOnly]
    private CharacterIdentity identity;
    [SerializeField]
    [ReadOnly]
    private CharacterAbilityCache abilityCache;
    [SerializeField]
    [ReadOnly]
    private CharacterVisual visual;
    [SerializeField]
    [ReadOnly]
    private CharacterStats stats;
    [SerializeField]
    [ReadOnly]
    private CharacterLog log;
    [SerializeField]
    [ReadOnly]
    private CharacterLifecycleState lifecycleState = CharacterLifecycleState.None;
    [SerializeField]
    [ReadOnly]
    private bool aiPaused;
    [SerializeField]
    private CharacterExpeditionRecoveryState expeditionRecovery = new CharacterExpeditionRecoveryState();
    private IGridSystemProvider gridSystemProvider;
    private GridSystemManager subscribedGridSystem;

    public CharacterLifecycleState CurrentState => lifecycleState;
    public bool IsAiPaused => aiPaused;
    public CharacterExpeditionRecoveryState ExpeditionRecovery => expeditionRecovery ??= new CharacterExpeditionRecoveryState();

    [Inject]
    public void ConstructCharacterLifecycle(IGridSystemProvider gridSystemProvider)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new System.ArgumentNullException(nameof(gridSystemProvider));
        TrySubscribeToGridChanges();
    }

    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
    }

    private void Start()
    {
        TrySubscribeToGridChanges();
    }

    private void OnEnable()
    {
        TrySubscribeToGridChanges();
    }

    private void OnDisable()
    {
        UnsubscribeFromGridChanges();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGridChanges();
    }

    public void Bind(CharacterActor owner)
    {
        actor = owner;
        identity = GetComponent<CharacterIdentity>();
        abilityCache = GetComponent<CharacterAbilityCache>();
        visual = GetComponent<CharacterVisual>();
        stats = GetComponent<CharacterStats>();
        log = GetComponent<CharacterLog>();
    }

    public void SetAiPaused(bool value)
    {
        if (aiPaused == value)
        {
            return;
        }

        aiPaused = value;
        if (!value
            && lifecycleState == CharacterLifecycleState.Active
            && actor != null
            && actor.Brain != null)
        {
            actor.Brain.RequestImmediateReplan(clearFailures: false);
        }
    }

    public bool BeginExpedition()
    {
        if (actor == null
            || (stats != null && stats.IsDead)
            || (identity != null && identity.IsOwner)
            || lifecycleState == CharacterLifecycleState.OnExpedition)
        {
            return false;
        }

        if (!CanStartExpedition(out _))
        {
            return false;
        }

        if (abilityCache != null && abilityCache.TryGetAbility(out AbilityWork work))
        {
            work.PrepareForExpedition();
        }

        log?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Lifecycle,
            CharacterActivityOutcomes.Departed,
            "원정 출발",
            actionId: "expedition",
            sentiment: 0.15f));
        SetLifecycleState(CharacterLifecycleState.OnExpedition);
        visual?.SetRenderersVisible(false);
        return true;
    }

    public void EndExpedition(bool alive = true)
    {
        if (actor == null)
        {
            return;
        }

        if (alive && (stats == null || !stats.IsDead))
        {
            visual?.SetRenderersVisible(true);
            SetLifecycleState(CharacterLifecycleState.Active);
            log?.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Lifecycle,
                CharacterActivityOutcomes.Returned,
                "원정 복귀",
                actionId: "expedition",
                sentiment: 0.55f));
            return;
        }

        visual?.SetRenderersVisible(true);
        stats?.Die("원정 중 사망");
    }

    public bool CanStartExpedition(out string reason)
    {
        reason = string.Empty;
        if (actor == null || stats == null)
        {
            reason = "character-state-missing";
            return false;
        }

        float maximumHealth = Mathf.Max(1f, stats.MaxHealth);
        if (stats.CurrentHealth < maximumHealth * 0.25f)
        {
            reason = "expedition-health-too-low";
            return false;
        }

        if (ExpeditionRecovery.stress >= 80f)
        {
            reason = "expedition-stress-too-high";
            return false;
        }

        return true;
    }

    public void RecordExpeditionReturn(float stress, bool alive)
    {
        if (!alive)
        {
            ExpeditionRecovery.stress = 0f;
            return;
        }

        ExpeditionRecovery.stress = Mathf.Clamp(
            Mathf.Max(ExpeditionRecovery.stress, stress),
            0f,
            100f);
    }

    public void ApplyExpeditionRecovery(float healthHealRatio, float injuryReduction, float stressReduction)
    {
        if (actor == null || stats == null)
        {
            return;
        }

        if (healthHealRatio > 0f)
        {
            actor.Heal(stats.MaxHealth * Mathf.Clamp01(healthHealRatio));
        }

        if (injuryReduction > 0f)
        {
            actor.SetInjurySeverity(Mathf.Max(0f, actor.InjurySeverity - injuryReduction));
        }

        ExpeditionRecovery.stress = Mathf.Clamp(
            ExpeditionRecovery.stress - Mathf.Max(0f, stressReduction),
            0f,
            100f);
    }

    public void RestoreExpeditionRecovery(CharacterExpeditionRecoveryState source)
    {
        ExpeditionRecovery.CopyFrom(source);
    }

    public void SetLifecycleState(CharacterLifecycleState nextState)
    {
        lifecycleState = nextState;
        if (nextState != CharacterLifecycleState.Active)
        {
            aiPaused = false;
        }

        if (actor == null)
        {
            return;
        }

        if (nextState == CharacterLifecycleState.Active)
        {
            visual?.EnsureVisibleForActiveLifecycle();
            if (actor.Brain == null) return;

            actor.Brain.RequestImmediateReplan(clearFailures: true);
            return;
        }

        if (actor.Brain == null) return;

        actor.Brain.bestAction = null;
        actor.Brain.isExecuted = false;
        actor.Brain.isBestActionEnd = false;
        actor.Brain.ClearPathSearchCache();
    }

    public Vector2Int GetNowXY()
    {
        if (!TryGetGrid(out Grid grid))
        {
            return Vector2Int.zero;
        }

        Vector2Int startPos = grid.GetXY(transform.position);
        startPos = grid.IsValidGridPos(startPos) ? startPos : Vector2Int.zero;
        return startPos;
    }

    public IEnumerator SnapToWalkableGridWhenReady()
    {
        for (int i = 0; i < 30; i++)
        {
            if (lifecycleState == CharacterLifecycleState.SpawningOutside
                || lifecycleState == CharacterLifecycleState.EnteringDungeon
                || lifecycleState == CharacterLifecycleState.ExitingDungeon
                || lifecycleState == CharacterLifecycleState.PreparingExpedition
                || lifecycleState == CharacterLifecycleState.DepartingExpedition
                || lifecycleState == CharacterLifecycleState.ReturningExpedition
                || lifecycleState == CharacterLifecycleState.OnExpedition
                || lifecycleState == CharacterLifecycleState.Despawned)
            {
                yield break;
            }

            if (TryGetGrid(out Grid grid))
            {
                Vector2Int currentPos = grid.GetXY(transform.position);
                if (grid.IsValidGridPos(currentPos) && grid.IsWalkable(currentPos))
                {
                    yield break;
                }

                if (grid.TryFindNearbyWalkablePositionOnSameFloor(currentPos, out Vector2Int walkablePosition))
                {
                    transform.position = grid.GetWorldPos(walkablePosition);
                    actor?.Brain?.ClearPathSearchCache();
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void TrySubscribeToGridChanges()
    {
        if (subscribedGridSystem != null
            || gridSystemProvider == null
            || !gridSystemProvider.TryGetManager(out GridSystemManager manager))
        {
            return;
        }

        subscribedGridSystem = manager;
        subscribedGridSystem.OnGridObjectChanged += HandleGridObjectChanged;
    }

    private void UnsubscribeFromGridChanges()
    {
        if (subscribedGridSystem == null)
        {
            return;
        }

        subscribedGridSystem.OnGridObjectChanged -= HandleGridObjectChanged;
        subscribedGridSystem = null;
    }

    private void HandleGridObjectChanged()
    {
        if (lifecycleState != CharacterLifecycleState.Active || !TryGetGrid(out Grid grid))
        {
            return;
        }

        Vector2Int currentPos = grid.GetXY(transform.position);
        if (!grid.IsValidGridPos(currentPos) || !grid.IsMovementBlockedByWall(currentPos))
        {
            return;
        }

        actor?.GetAbility<AbilityMove>()?.CancelActiveMovement();
        if (grid.TryFindNearbyWalkablePositionOnSameFloor(currentPos, out Vector2Int walkablePosition))
        {
            transform.position = grid.GetWorldPos(walkablePosition);
        }

        actor?.Brain?.RequestImmediateReplan(clearFailures: true);
    }

    private bool TryGetGrid(out Grid grid)
    {
        if (gridSystemProvider == null)
        {
            grid = null;
            return false;
        }

        return gridSystemProvider.TryGetGrid(out grid);
    }
}
