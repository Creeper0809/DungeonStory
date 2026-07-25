using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
public class CharacterStats : SerializedMonoBehaviour
{
    [SerializeField]
    [ReadOnly]
    private CharacterActor actor;
    [SerializeField]
    [ReadOnly]
    private CharacterIdentity identity;
    [SerializeField]
    [ReadOnly]
    private CharacterVisual visual;
    [SerializeField]
    [ReadOnly]
    private CharacterLifecycle lifecycle;
    [SerializeField]
    [ReadOnly]
    private CharacterLog log;
    [SerializeField]
    private Dictionary<CharacterCondition, float> stats;
    [SerializeField]
    [ReadOnly]
    private float maxHealth = 100f;
    [SerializeField]
    [ReadOnly]
    private float currentHealth = 100f;
    [SerializeField]
    [ReadOnly]
    [Range(0f, 1f)]
    private float injurySeverity;
    [SerializeField, Range(0f, 100f)]
    private float baseMood = CharacterMoodRules.DefaultBaseMood;
    [SerializeField, ReadOnly]
    private List<CharacterMoodMemory> interactionMoodFactors = new List<CharacterMoodMemory>();
    private float lastCalculatedMood = float.NaN;
    private float nextMoodExpiryCheckAt;
    private IStaffDiscontentRuntimeService staffDiscontentRuntimeService;
    private IOwnerRunLifecycleService ownerRunLifecycleService;
    private IMetaProgressionRuntimeReader metaProgressionRuntimeReader;
    private ICharacterPhysicalCapacityQuery physicalCapacityQuery;
    private ICharacterDeprivationRuntime deprivationRuntime;
    private IGameClock gameClock;
    private IGameEventBus gameEventBus;
    [NonSerialized] private ControlledStatDictionary controlledStats;

    public IDictionary<CharacterCondition, float> Stats
    {
        get
        {
            EnsureStats();
            return controlledStats ??= new ControlledStatDictionary(this);
        }
        set
        {
            stats = value != null
                ? new Dictionary<CharacterCondition, float>(value)
                : new Dictionary<CharacterCondition, float>();
            EnsureStats();
            AdoptAssignedMoodAsBase();
            RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
            PublishStatsChanged(includeMood: true);
        }
    }

    public bool IsDead => currentHealth <= 0f;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float InjurySeverity => injurySeverity;
    public float Mood => GetMoodSnapshot().Value;
    public IReadOnlyDictionary<CharacterCondition, float> StatSnapshot => CreateStatSnapshot();
    public event Action<IReadOnlyDictionary<CharacterCondition, float>> OnStatChange;
    public event Action<CharacterMoodSnapshot> OnMoodChange;

    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
    }

    [Inject]
    public void ConstructCharacterStats(
        IStaffDiscontentRuntimeService staffDiscontentRuntimeService,
        IOwnerRunLifecycleService ownerRunLifecycleService,
        IMetaProgressionRuntimeReader metaProgressionRuntimeReader,
        IGameClock gameClock,
        ICharacterPhysicalCapacityQuery physicalCapacityQuery = null,
        ICharacterDeprivationRuntime deprivationRuntime = null,
        IGameEventBus gameEventBus = null)
    {
        this.staffDiscontentRuntimeService = staffDiscontentRuntimeService
            ?? throw new ArgumentNullException(nameof(staffDiscontentRuntimeService));
        this.ownerRunLifecycleService = ownerRunLifecycleService
            ?? throw new ArgumentNullException(nameof(ownerRunLifecycleService));
        this.metaProgressionRuntimeReader = metaProgressionRuntimeReader
            ?? throw new ArgumentNullException(nameof(metaProgressionRuntimeReader));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.physicalCapacityQuery = physicalCapacityQuery;
        this.deprivationRuntime = deprivationRuntime;
        this.gameEventBus = gameEventBus;

        if (actor != null)
        {
            RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        }
    }

    public void Bind(CharacterActor owner)
    {
        actor = owner;
        identity = GetComponent<CharacterIdentity>();
        visual = GetComponent<CharacterVisual>();
        lifecycle = GetComponent<CharacterLifecycle>();
        log = GetComponent<CharacterLog>();
        EnsureStats();
        lastCalculatedMood = float.NaN;
        if (gameClock != null)
        {
            RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        }
    }

    private void Update()
    {
        if (interactionMoodFactors == null
            || interactionMoodFactors.Count == 0
            || RequireGameClock().Time < nextMoodExpiryCheckAt)
        {
            return;
        }

        nextMoodExpiryCheckAt = RequireGameClock().Time + 0.25f;
        RecalculateMood(notify: true, forceNotify: false, adoptExternalOverride: true);
    }

    public IEnumerator ChangeStatByTick()
    {
        while (true)
        {
            float hungerMultiplier = actor != null && actor.PersonaRuntime != null
                ? actor.PersonaRuntime.GetConditionCurveMultiplier(CharacterCondition.HUNGER)
                : 1f;
            float excretionMultiplier = actor != null && actor.PersonaRuntime != null
                ? actor.PersonaRuntime.GetConditionCurveMultiplier(CharacterCondition.EXCRETION)
                : 1f;
            float thirstMultiplier = actor != null && actor.PersonaRuntime != null
                ? actor.PersonaRuntime.GetConditionCurveMultiplier(CharacterCondition.THIRST)
                : 1f;
            float hygieneMultiplier = actor != null && actor.PersonaRuntime != null
                ? actor.PersonaRuntime.GetConditionCurveMultiplier(CharacterCondition.HYGIENE)
                : 1f;
            ChangesStat(CharacterCondition.HUNGER, -5f * hungerMultiplier);
            ChangesStat(CharacterCondition.THIRST, -6f * thirstMultiplier);
            ChangesStat(CharacterCondition.EXCRETION, -3f * excretionMultiplier);
            ChangesStat(CharacterCondition.HYGIENE, -1.5f * hygieneMultiplier);
            yield return new WaitForSeconds(5f);
        }
    }

    public void ChangesStat(CharacterCondition condition, float value)
    {
        if (DungeonDebugRuntimeRules.ShouldFreezeNeed(condition, value))
        {
            return;
        }

        EnsureStats();
        if (condition == CharacterCondition.MOOD)
        {
            ApplyMoodFactor(
                "legacy:mood-adjustment",
                value >= 0f ? "최근 좋은 경험" : "최근 불편한 경험",
                value,
                120f,
                4);
            return;
        }

        SynchronizeExternalMoodOverride();
        float previousValue = stats[condition];
        stats[condition] = Mathf.Clamp(stats[condition] + value, 0, 100);
        float nextValue = stats[condition];
        if (actor?.Progression != null
            && ((previousValue >= 20f && nextValue < 20f)
                || (previousValue <= 80f && nextValue > 80f)))
        {
            actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Need,
                $"need:{condition.ToString().ToLowerInvariant()}",
                string.Empty,
                nextValue < 20f ? "critical" : "satisfied",
                nextValue);
        }
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: true);
    }

    public void ApplyMoodFactor(
        string id,
        string label,
        float value,
        float durationSeconds = 180f,
        int maxStacks = 1)
    {
        if (string.IsNullOrWhiteSpace(id)
            || string.IsNullOrWhiteSpace(label)
            || Mathf.Approximately(value, 0f))
        {
            return;
        }

        EnsureStats();
        SynchronizeExternalMoodOverride();
        float now = RequireGameClock().Time;
        PruneExpiredMoodFactors(now);
        CharacterMoodMemory factor = interactionMoodFactors.Find(item => item != null && item.Id == id);
        if (factor == null)
        {
            factor = new CharacterMoodMemory(id, label, value, durationSeconds, maxStacks, now);
            interactionMoodFactors.Add(factor);
        }
        else
        {
            factor.Apply(label, value, durationSeconds, maxStacks, now);
        }

        nextMoodExpiryCheckAt = now + 0.25f;
        RecalculateMood(notify: true, forceNotify: true, adoptExternalOverride: false);
        if (actor?.Progression != null
            && !id.StartsWith("skill:", StringComparison.Ordinal))
        {
            actor.Progression.RecordNarrative(
                CharacterNarrativeDomain.Mood,
                id,
                string.Empty,
                value >= 0f ? "positive" : "negative",
                value);
        }
    }

    public bool RemoveMoodFactor(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || interactionMoodFactors == null)
        {
            return false;
        }

        EnsureStats();
        SynchronizeExternalMoodOverride();
        int removed = interactionMoodFactors.RemoveAll(item => item != null && item.Id == id);
        if (removed > 0)
        {
            RecalculateMood(notify: true, forceNotify: true, adoptExternalOverride: false);
        }

        return removed > 0;
    }

    public CharacterMoodSnapshot GetMoodSnapshot()
    {
        EnsureStats();
        SynchronizeExternalMoodOverride();
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        return BuildMoodSnapshot(RequireGameClock().Time);
    }

    public int GetCharacterStat(CharacterStatType statType)
    {
        return actor != null && actor.Progression != null
            ? actor.Progression.GetFinalStat(statType)
            : identity != null && identity.Profile != null ? identity.Profile.GetStat(statType) : 5;
    }

    public int GetCharacterStat(string statId)
    {
        return actor != null && actor.Progression != null
            ? actor.Progression.GetFinalStat(statId)
            : identity != null && identity.Profile != null ? identity.Profile.GetStat(statId) : 0;
    }

    public float GetMoveSpeed()
    {
        float baseSpeed = identity != null && identity.Data != null ? identity.Data.moveSpeed : 1f;
        float statMultiplier = Mathf.Clamp(
            1f + ((GetCharacterStat(CharacterStatType.MoveSpeed) - 5) * 0.08f),
            0.5f,
            1.8f);
        float injuryMultiplier = GetInjuryEfficiencyMultiplier();
        float bodyMultiplier = physicalCapacityQuery?.GetMoveMultiplier(actor) ?? 1f;
        return baseSpeed
            * statMultiplier
            * (GetEffectiveProfile()?.GetMoveModifierOnly() ?? 1f)
            * GetFatigueEfficiencyMultiplier()
            * Mathf.Min(injuryMultiplier, bodyMultiplier)
            * (deprivationRuntime?.GetMoveSpeedMultiplier(actor) ?? 1f);
    }

    public float GetConsumptionMultiplier()
    {
        return GetEffectiveProfile()?.GetConsumptionMultiplier() ?? 1f;
    }

    public float GetStayDurationMultiplier()
    {
        return GetEffectiveProfile()?.GetStayDurationMultiplier() ?? 1f;
    }

    public float GetCrowdSensitivityMultiplier()
    {
        return GetEffectiveProfile()?.GetCrowdSensitivityMultiplier() ?? 1f;
    }

    public float GetWorkSpeedMultiplier(WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? CalculateWorkSpeedMultiplier(definition)
            : 1f;
    }

    public float GetWorkPreferenceScore(WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? GetEffectiveProfile()?.GetWorkPreferenceScore(definition.WorkTypeId) ?? 0.5f
            : 0.5f;
    }

    private float CalculateWorkSpeedMultiplier(WorkTypeDefinition definition)
    {
        IStaffDiscontentRuntimeService discontentService =
            RuntimeDependency.Require(staffDiscontentRuntimeService, this);
        float discontentMultiplier = actor != null
            ? discontentService.GetWorkEfficiencyMultiplier(actor)
            : 1f;
        CharacterStatType workStat = GetBestWorkStat(definition.Type);
        float statMultiplier = Mathf.Clamp(
            1f + ((GetCharacterStat(workStat) - 5) * 0.06f),
            0.5f,
            2f);
        float injuryMultiplier = GetInjuryEfficiencyMultiplier();
        float bodyMultiplier = physicalCapacityQuery?.GetWorkMultiplier(actor, definition.WorkTypeId) ?? 1f;
        return statMultiplier
            * (GetEffectiveProfile()?.GetWorkModifierOnly(definition.WorkTypeId) ?? 1f)
            * GetFatigueEfficiencyMultiplier()
            * Mathf.Min(injuryMultiplier, bodyMultiplier)
            * discontentMultiplier
            * CharacterSkillRuntimeEffects.GetWorkSpeedMultiplier(actor)
            * (deprivationRuntime?.GetWorkSpeedMultiplier(actor) ?? 1f);
    }

    public float GetFacilityPreferenceScore(FacilityRole roles)
    {
        return GetEffectiveProfile()?.GetFacilityPreferenceScore(roles) ?? 0.5f;
    }

    public float GetAccidentChanceMultiplier()
    {
        CharacterRuntimeProfile profile = GetEffectiveProfile();
        float enduranceMultiplier = Mathf.Clamp(
            1f - ((GetCharacterStat(CharacterStatType.Endurance) - 5) * 0.03f),
            0.5f,
            1.5f);
        float toughnessMultiplier = Mathf.Clamp(
            1f - ((GetCharacterStat(CharacterStatType.Toughness) - 5) * 0.02f),
            0.6f,
            1.4f);
        return (profile?.GetAccidentModifierOnly() ?? 1f)
            * enduranceMultiplier
            * toughnessMultiplier;
    }

    public CharacterSpeciesIncidentType GetIncidentType()
    {
        return GetEffectiveProfile()?.GetIncidentType() ?? CharacterSpeciesIncidentType.None;
    }

    public float GetCrimeRiskMultiplier()
    {
        return GetEffectiveProfile()?.GetCrimeRiskMultiplier() ?? 1f;
    }

    public float GetCombatPowerMultiplier()
    {
        return (GetEffectiveProfile()?.GetCombatPowerMultiplier() ?? 1f)
            * GetInjuryEfficiencyMultiplier();
    }

    public float GetSpendingMultiplier()
    {
        float statMultiplier = Mathf.Clamp(
            1f + ((GetCharacterStat(CharacterStatType.Sales) - 5) * 0.05f),
            0.5f,
            2f);
        return statMultiplier * (GetEffectiveProfile()?.GetSpendingModifierOnly() ?? 1f);
    }

    private CharacterRuntimeProfile GetEffectiveProfile()
    {
        return actor != null && actor.Progression != null
            ? actor.Progression.GetEffectiveRuntimeProfile()
            : identity?.Profile;
    }

    private static CharacterStatType GetBestWorkStat(FacilityWorkType workTypes)
    {
        if ((workTypes & FacilityWorkType.Construct) != 0) return CharacterStatType.Dexterity;
        if ((workTypes & FacilityWorkType.Research) != 0) return CharacterStatType.Research;
        if ((workTypes & FacilityWorkType.Guard) != 0) return CharacterStatType.Attack;
        if ((workTypes & FacilityWorkType.Clean) != 0) return CharacterStatType.Cleaning;
        if ((workTypes & FacilityWorkType.DrawWater) != 0) return CharacterStatType.Endurance;
        if ((workTypes & FacilityWorkType.Cook) != 0) return CharacterStatType.Dexterity;
        if ((workTypes & FacilityWorkType.Treat) != 0) return CharacterStatType.Research;
        if ((workTypes & FacilityWorkType.Refuel) != 0) return CharacterStatType.Strength;
        if ((workTypes & FacilityWorkType.Restock) != 0) return CharacterStatType.Strength;
        if ((workTypes & FacilityWorkType.Repair) != 0) return CharacterStatType.Dexterity;
        if ((workTypes & FacilityWorkType.Operate) != 0) return CharacterStatType.Sales;
        if ((workTypes & FacilityWorkType.Rescue) != 0) return CharacterStatType.Toughness;
        return CharacterStatType.Endurance;
    }

    public float GetFatigueEfficiencyMultiplier()
    {
        EnsureStats();
        if (!stats.TryGetValue(CharacterCondition.SLEEP, out float sleep))
        {
            return 1f;
        }

        return Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(sleep / 100f));
    }

    public float GetInjuryEfficiencyMultiplier()
    {
        return Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(injurySeverity));
    }

    public void ApplyDamage(float amount, string reason = "")
    {
        if (amount <= 0f || IsDead || DungeonDebugRuntimeRules.ShouldBlockFriendlyDamage(actor)) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        injurySeverity = Mathf.Clamp01(1f - (currentHealth / Mathf.Max(1f, maxHealth)));
        ApplyMoodFactor(
            "health:injury",
            "몸을 다침",
            -Mathf.Clamp(amount * 0.25f, 2f, 10f),
            180f,
            2);
        log?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Damaged,
            string.IsNullOrWhiteSpace(reason)
                ? $"피해 {amount:0.#}"
                : $"피해 {amount:0.#}: {reason}",
            actionId: "health:damage",
            reasonCode: reason,
            value: amount,
            sentiment: -0.8f,
            bubbleEligible: true));

        if (currentHealth <= 0f)
        {
            Die(reason);
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        injurySeverity = Mathf.Clamp01(1f - (currentHealth / Mathf.Max(1f, maxHealth)));
        ApplyMoodFactor(
            "health:relief",
            "치료받아 안도함",
            Mathf.Clamp(amount * 0.15f, 1f, 6f),
            120f,
            1);
        log?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Completed,
            $"회복 {amount:0.#}",
            actionId: "health:heal",
            value: amount,
            sentiment: 0.55f));
    }

    public void ScaleMaxHealth(float multiplier)
    {
        float safeMultiplier = Mathf.Max(0.01f, multiplier);
        maxHealth = Mathf.Max(1f, maxHealth * safeMultiplier);
        currentHealth = Mathf.Clamp(currentHealth * safeMultiplier, 0f, maxHealth);
        injurySeverity = Mathf.Clamp01(1f - (currentHealth / Mathf.Max(1f, maxHealth)));
    }

    public void SetInjurySeverity(float value)
    {
        injurySeverity = Mathf.Clamp01(value);
        currentHealth = Mathf.Clamp(maxHealth * (1f - injurySeverity), 1f, maxHealth);
        log?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Changed,
            $"부상도 변경: {Mathf.RoundToInt(injurySeverity * 100f)}%",
            actionId: "health:injury-severity",
            value: injurySeverity,
            sentiment: -injurySeverity));
    }

    public void Die(string reason = "")
    {
        if (lifecycle != null && lifecycle.CurrentState == CharacterLifecycleState.Despawned) return;

        visual?.SetRenderersVisible(true);
        currentHealth = 0f;
        injurySeverity = 1f;
        log?.AddActivity(CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Defeated,
            string.IsNullOrWhiteSpace(reason) ? "사망" : $"사망: {reason}",
            actionId: "health:death",
            reasonCode: reason,
            value: 1f,
            sentiment: -1f,
            bubbleEligible: true));
        lifecycle?.SetLifecycleState(CharacterLifecycleState.Despawned);

        RuntimeDependency.Require(gameEventBus, this)
            .Publish(new CharacterDeathEvent(actor, reason));

        if (identity != null && identity.IsOwner && actor != null)
        {
            RuntimeDependency.Require(ownerRunLifecycleService, this).HandleOwnerDeath(actor, reason);
        }
    }

    public void RecalculateVitals(bool resetCurrentHealth)
    {
        int toughness = GetCharacterStat(CharacterStatType.Toughness);
        int endurance = GetCharacterStat(CharacterStatType.Endurance);
        maxHealth = 60f + (toughness * 8f) + (endurance * 4f);
        if (identity != null && identity.IsOwner)
        {
            maxHealth *= ResolveMetaProgressionRuntimeReader().GetOwnerMaxHealthMultiplier();
        }

        if (resetCurrentHealth || currentHealth <= 0f)
        {
            currentHealth = maxHealth;
            injurySeverity = 0f;
        }
        else
        {
            currentHealth = Mathf.Clamp(currentHealth, 1f, maxHealth);
            injurySeverity = Mathf.Clamp01(1f - (currentHealth / Mathf.Max(1f, maxHealth)));
        }
    }

    public void RestorePersistentState(
        IReadOnlyDictionary<CharacterCondition, float> savedStats,
        float savedCurrentHealth,
        float savedInjurySeverity,
        float savedBaseMood,
        IReadOnlyList<CharacterMoodFactorSnapshot> savedInteractionMoodFactors)
    {
        stats = savedStats != null
            ? new Dictionary<CharacterCondition, float>(savedStats)
            : new Dictionary<CharacterCondition, float>();
        baseMood = Mathf.Clamp(savedBaseMood, 0f, 100f);
        interactionMoodFactors ??= new List<CharacterMoodMemory>();
        interactionMoodFactors.Clear();
        EnsureStats();
        stats[CharacterCondition.MOOD] = baseMood;

        float now = RequireGameClock().Time;
        if (savedInteractionMoodFactors != null)
        {
            foreach (CharacterMoodFactorSnapshot factor in savedInteractionMoodFactors)
            {
                if (factor == null
                    || factor.Kind != CharacterMoodFactorKind.Interaction
                    || string.IsNullOrWhiteSpace(factor.Id)
                    || string.IsNullOrWhiteSpace(factor.Label)
                    || Mathf.Approximately(factor.Value, 0f)
                    || factor.RemainingSeconds <= 0f)
                {
                    continue;
                }

                interactionMoodFactors.Add(new CharacterMoodMemory(
                    factor.Id,
                    factor.Label,
                    factor.Value,
                    factor.RemainingSeconds,
                    1,
                    now));
            }
        }

        RecalculateVitals(resetCurrentHealth: true);
        currentHealth = Mathf.Clamp(savedCurrentHealth, 0f, maxHealth);
        injurySeverity = Mathf.Clamp01(savedInjurySeverity);
        nextMoodExpiryCheckAt = now + 0.25f;
        lastCalculatedMood = float.NaN;
        RecalculateMood(notify: true, forceNotify: true, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: false);
    }

    private void EnsureStats()
    {
        stats ??= new Dictionary<CharacterCondition, float>();
        interactionMoodFactors ??= new List<CharacterMoodMemory>();
        foreach (CharacterNeedDefinition definition in CharacterNeedCatalog.All)
        {
            EnsureStat(definition.Condition, definition.DefaultValue);
        }

        EnsureStat(CharacterCondition.MOOD, baseMood);
    }

    private void AdoptAssignedMoodAsBase()
    {
        float requestedMood = stats.TryGetValue(CharacterCondition.MOOD, out float assigned)
            ? Mathf.Clamp(assigned, 0f, 100f)
            : CharacterMoodRules.DefaultBaseMood;
        float factorTotal = CalculateFactorTotal(
            BuildMoodSnapshot(RequireGameClock().Time).Factors);
        baseMood = Mathf.Clamp(requestedMood - factorTotal, 0f, 100f);
        lastCalculatedMood = requestedMood;
    }

    private void SynchronizeExternalMoodOverride()
    {
        if (float.IsNaN(lastCalculatedMood)
            || !stats.TryGetValue(CharacterCondition.MOOD, out float currentMood)
            || Mathf.Approximately(currentMood, lastCalculatedMood))
        {
            return;
        }

        float factorTotal = CalculateFactorTotal(
            BuildMoodSnapshot(RequireGameClock().Time).Factors);
        baseMood = Mathf.Clamp(currentMood - factorTotal, 0f, 100f);
        lastCalculatedMood = Mathf.Clamp(currentMood, 0f, 100f);
    }

    private void RecalculateMood(
        bool notify,
        bool forceNotify,
        bool adoptExternalOverride)
    {
        EnsureStats();
        if (adoptExternalOverride)
        {
            SynchronizeExternalMoodOverride();
        }

        float now = RequireGameClock().Time;
        bool expired = PruneExpiredMoodFactors(now);
        CharacterMoodSnapshot snapshot = BuildMoodSnapshot(now);
        float previous = stats.TryGetValue(CharacterCondition.MOOD, out float current)
            ? current
            : snapshot.Value;
        stats[CharacterCondition.MOOD] = snapshot.Value;
        lastCalculatedMood = snapshot.Value;

        if (notify && (forceNotify || expired || !Mathf.Approximately(previous, snapshot.Value)))
        {
            OnStatChange?.Invoke(CreateStatSnapshot());
            OnMoodChange?.Invoke(snapshot);
        }
    }

    private void SetStatValue(CharacterCondition condition, float value)
    {
        EnsureStats();
        stats[condition] = Mathf.Clamp(value, 0f, 100f);
        if (condition == CharacterCondition.MOOD)
        {
            AdoptAssignedMoodAsBase();
        }

        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: true);
    }

    private bool RemoveStatValue(CharacterCondition condition)
    {
        EnsureStats();
        bool removed = stats.Remove(condition);
        if (!removed)
        {
            return false;
        }

        EnsureStats();
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: true);
        return true;
    }

    private void ResetStatValues()
    {
        stats.Clear();
        EnsureStats();
        AdoptAssignedMoodAsBase();
        RecalculateMood(notify: false, forceNotify: false, adoptExternalOverride: false);
        PublishStatsChanged(includeMood: true);
    }

    private void PublishStatsChanged(bool includeMood)
    {
        OnStatChange?.Invoke(CreateStatSnapshot());
        if (includeMood)
        {
            OnMoodChange?.Invoke(BuildMoodSnapshot(RequireGameClock().Time));
        }
    }

    private IReadOnlyDictionary<CharacterCondition, float> CreateStatSnapshot()
    {
        EnsureStats();
        return new ReadOnlyDictionary<CharacterCondition, float>(
            new Dictionary<CharacterCondition, float>(stats));
    }

    private sealed class ControlledStatDictionary : IDictionary<CharacterCondition, float>
    {
        private readonly CharacterStats owner;

        public ControlledStatDictionary(CharacterStats owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public float this[CharacterCondition key]
        {
            get
            {
                owner.EnsureStats();
                return owner.stats[key];
            }
            set => owner.SetStatValue(key, value);
        }

        public ICollection<CharacterCondition> Keys
        {
            get
            {
                owner.EnsureStats();
                return new List<CharacterCondition>(owner.stats.Keys);
            }
        }

        public ICollection<float> Values
        {
            get
            {
                owner.EnsureStats();
                return new List<float>(owner.stats.Values);
            }
        }

        public int Count
        {
            get
            {
                owner.EnsureStats();
                return owner.stats.Count;
            }
        }

        public bool IsReadOnly => false;

        public void Add(CharacterCondition key, float value)
        {
            if (ContainsKey(key))
            {
                throw new ArgumentException($"Stat '{key}' already exists.", nameof(key));
            }

            owner.SetStatValue(key, value);
        }

        public bool ContainsKey(CharacterCondition key)
        {
            owner.EnsureStats();
            return owner.stats.ContainsKey(key);
        }

        public bool Remove(CharacterCondition key)
        {
            return owner.RemoveStatValue(key);
        }

        public bool TryGetValue(CharacterCondition key, out float value)
        {
            owner.EnsureStats();
            return owner.stats.TryGetValue(key, out value);
        }

        public void Add(KeyValuePair<CharacterCondition, float> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            owner.ResetStatValues();
        }

        public bool Contains(KeyValuePair<CharacterCondition, float> item)
        {
            return TryGetValue(item.Key, out float value)
                && EqualityComparer<float>.Default.Equals(value, item.Value);
        }

        public void CopyTo(KeyValuePair<CharacterCondition, float>[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            foreach (KeyValuePair<CharacterCondition, float> pair in this)
            {
                array[arrayIndex++] = pair;
            }
        }

        public bool Remove(KeyValuePair<CharacterCondition, float> item)
        {
            return Contains(item) && Remove(item.Key);
        }

        public IEnumerator<KeyValuePair<CharacterCondition, float>> GetEnumerator()
        {
            owner.EnsureStats();
            return new Dictionary<CharacterCondition, float>(owner.stats).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    private bool PruneExpiredMoodFactors(float now)
    {
        if (interactionMoodFactors == null)
        {
            return false;
        }

        return interactionMoodFactors.RemoveAll(item => item == null || item.IsExpired(now)) > 0;
    }

    private CharacterMoodSnapshot BuildMoodSnapshot(float now)
    {
        List<CharacterMoodFactorSnapshot> factors = CharacterMoodRules.BuildNeedFactors(stats);
        if (interactionMoodFactors != null)
        {
            foreach (CharacterMoodMemory factor in interactionMoodFactors)
            {
                if (factor != null && !factor.IsExpired(now))
                {
                    factors.Add(factor.CreateSnapshot(now));
                }
            }
        }

        float mood = Mathf.Clamp(baseMood + CalculateFactorTotal(factors), 0f, 100f);
        return new CharacterMoodSnapshot(mood, baseMood, factors);
    }

    private static float CalculateFactorTotal(IReadOnlyList<CharacterMoodFactorSnapshot> factors)
    {
        float total = 0f;
        if (factors == null)
        {
            return total;
        }

        for (int i = 0; i < factors.Count; i++)
        {
            if (factors[i] != null)
            {
                total += factors[i].Value;
            }
        }

        return total;
    }

    private IMetaProgressionRuntimeReader ResolveMetaProgressionRuntimeReader()
    {
        return RuntimeDependency.Require(metaProgressionRuntimeReader, this);
    }

    private IGameClock RequireGameClock()
    {
        return gameClock
            ?? throw new InvalidOperationException(
                $"{nameof(CharacterStats)} requires {nameof(IGameClock)} injection.");
    }

    private void EnsureStat(CharacterCondition condition, float defaultValue)
    {
        if (!stats.ContainsKey(condition))
        {
            stats[condition] = defaultValue;
        }
    }

}
