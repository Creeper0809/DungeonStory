using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

[DrawWithUnity]
public class CharacterAbility : SerializedMonoBehaviour
{
    protected CharacterActor actor;
    protected CharacterIdentity identity;
    protected CharacterAbilityCache abilityCache;
    protected CharacterStats stats;
    protected CharacterLifecycle lifecycle;
    protected CharacterLog log;
    protected Grid grid;
    protected AbilityMove move;
    private IGridSystemProvider gridSystemProvider;

    [Inject]
    public void ConstructCharacterAbility(IGridSystemProvider gridSystemProvider)
    {
        this.gridSystemProvider = gridSystemProvider
            ?? throw new System.ArgumentNullException(nameof(gridSystemProvider));
        CacheCommonReferences();
    }

    protected virtual void Awake()
    {
        actor = GetComponent<CharacterActor>();
        CacheSplitComponents();
    }

    protected virtual void Start()
    {
        CacheCommonReferences();
    }

    protected void CacheCommonReferences()
    {
        CacheLocalReferences();
        grid = TryGetGrid(out Grid resolvedGrid) ? resolvedGrid : null;
    }

    protected void CacheLocalReferences()
    {
        if (actor == null)
        {
            actor = GetComponent<CharacterActor>();
        }

        CacheSplitComponents();

        move = null;
        if (abilityCache != null)
        {
            abilityCache.TryGetAbility(out move);
        }
    }

    protected bool TryGetGrid(out Grid resolvedGrid)
    {
        return RuntimeDependency.Require(gridSystemProvider, this).TryGetGrid(out resolvedGrid);
    }

    private void CacheSplitComponents()
    {
        identity = GetComponent<CharacterIdentity>();
        abilityCache = GetComponent<CharacterAbilityCache>();
        stats = GetComponent<CharacterStats>();
        lifecycle = GetComponent<CharacterLifecycle>();
        log = GetComponent<CharacterLog>();
    }

    public virtual void Initializtion(CharacterSO data)
    {
        CacheCommonReferences();
    }
}
