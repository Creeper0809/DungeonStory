using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum OffenseBattleTeam
{
    Allies,
    Enemies
}

public enum OffenseBattleActionType
{
    BasicAttack,
    Guard,
    Ability,
    Retreat,
    Reload,
    SwitchWeapon,
    SetFireMode
}

public enum OffenseBattleOutcome
{
    InProgress,
    Victory,
    Defeat,
    Retreated,
    AbortedOwnerDeath
}

public enum OffenseBattleStatusType
{
    Guard,
    Vulnerability,
    DamageOverTime,
    AttackModifier
}

[Serializable]
public sealed class OffenseBattleStats
{
    public OffenseBattleStats(
        float maxHealth,
        float attack,
        float strength,
        float toughness,
        float dexterity,
        float moveSpeed,
        float shooting = -1f,
        float evasion = -1f)
    {
        MaxHealth = Mathf.Max(1f, maxHealth);
        Attack = Mathf.Max(0f, attack);
        Strength = Mathf.Max(0f, strength);
        Toughness = Mathf.Max(0f, toughness);
        Dexterity = Mathf.Max(0f, dexterity);
        MoveSpeed = Mathf.Max(0f, moveSpeed);
        Shooting = shooting < 0f ? Attack : Mathf.Max(0f, shooting);
        Evasion = evasion < 0f ? MoveSpeed : Mathf.Max(0f, evasion);
    }

    public float MaxHealth { get; }
    public float Attack { get; }
    public float Strength { get; }
    public float Toughness { get; }
    public float Dexterity { get; }
    public float MoveSpeed { get; }
    public float Shooting { get; }
    public float Evasion { get; }
}

[Serializable]
public sealed class OffenseBattleStatus
{
    public OffenseBattleStatus(
        string id,
        OffenseBattleStatusType type,
        float value,
        int remainingTurns,
        string sourceId)
    {
        Id = id ?? string.Empty;
        Type = type;
        Value = Mathf.Max(0f, value);
        RemainingTurns = Mathf.Max(1, remainingTurns);
        SourceId = sourceId ?? string.Empty;
    }

    public string Id { get; }
    public OffenseBattleStatusType Type { get; }
    public float Value { get; private set; }
    public int RemainingTurns { get; private set; }
    public string SourceId { get; }

    internal void Refresh(float value, int turns)
    {
        Value = Mathf.Max(Value, Mathf.Max(0f, value));
        RemainingTurns = Mathf.Max(RemainingTurns, Mathf.Max(1, turns));
    }

    internal bool ConsumeTurn()
    {
        RemainingTurns = Mathf.Max(0, RemainingTurns - 1);
        return RemainingTurns <= 0;
    }
}

public sealed class OffenseBattleCombatant
{
    private readonly List<CharacterCombatAbilityDefinition> abilities;
    private readonly IReadOnlyList<CharacterCombatAbilityDefinition> abilitiesView;
    private readonly List<OffenseBattleStatus> statuses = new List<OffenseBattleStatus>();
    private readonly IReadOnlyList<OffenseBattleStatus> statusesView;
    private readonly Dictionary<string, int> cooldowns = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly List<CharacterBodyPartHealthState> bodyParts = new List<CharacterBodyPartHealthState>();
    private readonly IReadOnlyList<CharacterBodyPartHealthState> bodyPartsView;
    private IReadOnlyList<CombatArmorSnapshot> armor = Array.Empty<CombatArmorSnapshot>();

    public OffenseBattleCombatant(
        string persistentId,
        string displayName,
        string speciesTag,
        OffenseBattleTeam team,
        OffenseBattleStats stats,
        float currentHealth,
        IEnumerable<CharacterCombatAbilityDefinition> abilities = null,
        int portraitDataId = -1,
        OffenseFormationSlot formation = OffenseFormationSlot.Front)
    {
        PersistentId = string.IsNullOrWhiteSpace(persistentId)
            ? throw new ArgumentException("A combatant requires a persistent ID.", nameof(persistentId))
            : persistentId;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? persistentId : displayName;
        SpeciesTag = speciesTag ?? string.Empty;
        Team = team;
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
        CurrentHealth = Mathf.Clamp(currentHealth, 0f, stats.MaxHealth);
        this.abilities = abilities?
            .Where(ability => ability != null && ability.IsValid)
            .GroupBy(ability => ability.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList() ?? new List<CharacterCombatAbilityDefinition>();
        abilitiesView = this.abilities.AsReadOnly();
        statusesView = statuses.AsReadOnly();
        bodyPartsView = bodyParts.AsReadOnly();
        ResetBodyParts();
        PortraitDataId = portraitDataId;
        Formation = formation;
    }

    public string PersistentId { get; }
    public string DisplayName { get; }
    public string SpeciesTag { get; }
    public OffenseBattleTeam Team { get; }
    public OffenseBattleStats Stats { get; private set; }
    public float CurrentHealth { get; private set; }
    public float HealthRatio => CurrentHealth / Mathf.Max(1f, Stats.MaxHealth);
    public bool IsDead => CurrentHealth <= 0f || IsVitalPartDestroyed();
    public bool IsDowned { get; private set; }
    public bool CanTakeTurn => !IsDead && !IsDowned && !PinnedThisTurn;
    public float InitiativePenalty { get; private set; }
    public float Initiative => Mathf.Max(
        0f,
        Stats.Dexterity * 2f * Manipulation
        + Stats.MoveSpeed * Mobility
        - InitiativePenalty);
    public float TotalDamageTaken { get; private set; }
    public int PortraitDataId { get; }
    public OffenseFormationSlot Formation { get; private set; }
    public int TurnsStarted { get; private set; }
    public IReadOnlyList<CharacterCombatAbilityDefinition> Abilities => abilitiesView;
    public IReadOnlyList<OffenseBattleStatus> Statuses => statusesView;
    public IReadOnlyList<CharacterBodyPartHealthState> BodyParts => bodyPartsView;
    public CombatWeaponSnapshot Weapon { get; private set; } = CombatWeaponSnapshot.CreateUnarmed();
    public IReadOnlyList<CombatArmorSnapshot> Armor => armor;
    public float Suppression { get; private set; }
    public float BloodLoss { get; private set; }
    public float Consciousness => CalculateConsciousness();
    public float Manipulation => CalculateLimbAverage(CombatBodyPart.LeftArm, CombatBodyPart.RightArm);
    public float Mobility => CalculateLimbAverage(CombatBodyPart.LeftLeg, CombatBodyPart.RightLeg);
    public bool PinnedThisTurn { get; private set; }
    public CombatBodyPart LastHitBodyPart { get; private set; } = CombatBodyPart.Torso;
    public float CoverBlockChance { get; private set; }
    public CombatFireMode FireMode { get; private set; } = CombatFireMode.Aimed;

    public void SetCombatEquipment(
        CombatWeaponSnapshot weapon,
        IReadOnlyList<CombatArmorSnapshot> armor)
    {
        Weapon = weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        this.armor = armor ?? Array.Empty<CombatArmorSnapshot>();
    }

    public void SetCover(float blockChance)
    {
        CoverBlockChance = Mathf.Clamp01(blockChance);
    }

    public void SetFireMode(CombatFireMode mode)
    {
        FireMode = mode;
    }

    public int GetCooldown(string abilityId)
    {
        return !string.IsNullOrWhiteSpace(abilityId)
            && cooldowns.TryGetValue(abilityId, out int value)
                ? Mathf.Max(0, value)
                : 0;
    }

    internal void SetCooldown(string abilityId, int turns)
    {
        if (!string.IsNullOrWhiteSpace(abilityId))
        {
            cooldowns[abilityId] = Mathf.Max(0, turns);
        }
    }

    internal void AdjustCooldowns(int delta)
    {
        foreach (string id in cooldowns.Keys.ToArray())
        {
            cooldowns[id] = delta <= -99
                ? 0
                : Mathf.Max(0, cooldowns[id] + delta);
        }
    }

    internal IReadOnlyDictionary<string, int> GetCooldownSnapshot()
    {
        return new Dictionary<string, int>(cooldowns, StringComparer.Ordinal);
    }

    internal void RestoreCooldowns(IEnumerable<KeyValuePair<string, int>> values)
    {
        cooldowns.Clear();
        foreach (KeyValuePair<string, int> pair in values)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
            {
                cooldowns[pair.Key] = pair.Value;
            }
        }
    }

    internal void BeginTurn()
    {
        if (TurnsStarted > 0)
        {
            foreach (string id in cooldowns.Keys.ToArray())
            {
                cooldowns[id] = Mathf.Max(0, cooldowns[id] - 1);
            }
        }

        TurnsStarted++;
        PinnedThisTurn = Suppression >= 75f;
        Suppression = Mathf.Max(0f, Suppression - 12f);
        if (BloodLoss > 0f && !IsDead)
        {
            ApplyRawDamage(Mathf.Max(0.25f, BloodLoss * 0.0125f));
        }

        UpdateDowned();
    }

    internal float ApplyCombatInjury(CombatAttackResult result)
    {
        Suppression = Mathf.Clamp(Suppression + result.Suppression, 0f, 100f);
        if (!result.Hit || result.AppliedDamage <= 0f)
        {
            return 0f;
        }

        LastHitBodyPart = result.BodyPart;
        BloodLoss = Mathf.Clamp(BloodLoss + result.Bleeding, 0f, 100f);
        CharacterBodyPartHealthState part = GetBodyPart(result.BodyPart);
        part.currentHealth = Mathf.Max(0f, part.currentHealth - result.AppliedDamage);
        part.bleedingPerSecond = Mathf.Max(
            0f,
            part.bleedingPerSecond + result.Bleeding * 0.01f);
        float applied = ApplyRawDamage(result.AppliedDamage);
        if (IsVitalPartDestroyed())
        {
            ApplyRawDamage(CurrentHealth);
        }

        UpdateDowned();
        return applied;
    }

    internal void RestoreCombatState(
        float suppression,
        float bloodLoss,
        CombatBodyPart lastHitBodyPart,
        CombatFireMode fireMode = CombatFireMode.Aimed)
    {
        Suppression = Mathf.Clamp(suppression, 0f, 100f);
        BloodLoss = Mathf.Clamp(bloodLoss, 0f, 100f);
        LastHitBodyPart = lastHitBodyPart;
        FireMode = fireMode;
        UpdateDowned();
    }

    public CharacterBodyHealthSnapshot CaptureBodyHealth()
    {
        return new CharacterBodyHealthSnapshot(
            bodyParts.Select(CloneBodyPart).ToArray(),
            BloodLoss,
            Suppression,
            Consciousness,
            Manipulation,
            Mobility,
            IsDowned);
    }

    public void ApplyBodyHealth(CharacterBodyHealthSnapshot snapshot)
    {
        if (snapshot.Parts == null || snapshot.Parts.Count == 0)
        {
            return;
        }

        bodyParts.Clear();
        bodyParts.AddRange(snapshot.Parts.Select(CloneBodyPart));
        EnsureBodyParts();
        BloodLoss = Mathf.Clamp(snapshot.BloodLoss, 0f, 100f);
        Suppression = Mathf.Clamp(snapshot.Suppression, 0f, 100f);
        UpdateDowned();
    }

    internal void RestoreTurnsStarted(int turns)
    {
        TurnsStarted = Mathf.Max(0, turns);
    }

    internal float ApplyRawDamage(float amount)
    {
        float before = CurrentHealth;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, amount));
        float applied = before - CurrentHealth;
        TotalDamageTaken += applied;
        return applied;
    }

    internal float Heal(float amount)
    {
        if (IsDead)
        {
            return 0f;
        }

        float before = CurrentHealth;
        CurrentHealth = Mathf.Min(Stats.MaxHealth, CurrentHealth + Mathf.Max(0f, amount));
        float applied = CurrentHealth - before;
        float remaining = applied;
        foreach (CharacterBodyPartHealthState part in bodyParts.OrderBy(part => part.HealthRatio))
        {
            float restored = Mathf.Min(remaining, part.maxHealth - part.currentHealth);
            part.currentHealth += restored;
            part.bleedingPerSecond = Mathf.Max(0f, part.bleedingPerSecond - applied * 0.0025f);
            remaining -= restored;
            if (remaining <= 0f)
            {
                break;
            }
        }

        BloodLoss = Mathf.Max(0f, BloodLoss - applied * 0.5f);
        UpdateDowned();
        return applied;
    }

    internal void RestoreHealth(float currentHealth, float totalDamageTaken)
    {
        CurrentHealth = Mathf.Clamp(currentHealth, 0f, Stats.MaxHealth);
        TotalDamageTaken = Mathf.Max(0f, totalDamageTaken);
        UpdateDowned();
    }

    internal void RestoreStats(OffenseBattleStats stats)
    {
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    internal void AddStatus(OffenseBattleStatus status)
    {
        if (status == null)
        {
            return;
        }

        OffenseBattleStatus existing = statuses.FirstOrDefault(value => value.Id == status.Id);
        if (existing != null)
        {
            existing.Refresh(status.Value, status.RemainingTurns);
            return;
        }

        statuses.Add(status);
    }

    internal void RestoreStatuses(IEnumerable<OffenseBattleStatus> values)
    {
        statuses.Clear();
        statuses.AddRange(values?.Where(value => value != null) ?? Array.Empty<OffenseBattleStatus>());
    }

    internal void RemoveStatus(OffenseBattleStatus status)
    {
        statuses.Remove(status);
    }

    internal int RemoveStatuses(Func<OffenseBattleStatus, bool> predicate, int maximum)
    {
        if (predicate == null || maximum <= 0)
        {
            return 0;
        }

        int removed = 0;
        for (int i = statuses.Count - 1; i >= 0 && removed < maximum; i--)
        {
            if (predicate(statuses[i]))
            {
                statuses.RemoveAt(i);
                removed++;
            }
        }

        return removed;
    }

    internal void SetFormation(OffenseFormationSlot formation)
    {
        Formation = formation;
    }

    internal void AddInitiativePenalty(float amount)
    {
        InitiativePenalty = Mathf.Max(0f, InitiativePenalty + Mathf.Max(0f, amount));
    }

    internal void RestoreInitiativePenalty(float value)
    {
        InitiativePenalty = Mathf.Max(0f, value);
    }

    internal void RestoreFormation(OffenseFormationSlot formation)
    {
        Formation = formation;
    }

    private void ResetBodyParts()
    {
        bodyParts.Clear();
        bodyParts.Add(CreateBodyPart(CombatBodyPart.Head, 18f));
        bodyParts.Add(CreateBodyPart(CombatBodyPart.Torso, 45f));
        bodyParts.Add(CreateBodyPart(CombatBodyPart.LeftArm, 22f));
        bodyParts.Add(CreateBodyPart(CombatBodyPart.RightArm, 22f));
        bodyParts.Add(CreateBodyPart(CombatBodyPart.LeftLeg, 26f));
        bodyParts.Add(CreateBodyPart(CombatBodyPart.RightLeg, 26f));
        UpdateDowned();
    }

    private void EnsureBodyParts()
    {
        EnsureBodyPart(CombatBodyPart.Head, 18f);
        EnsureBodyPart(CombatBodyPart.Torso, 45f);
        EnsureBodyPart(CombatBodyPart.LeftArm, 22f);
        EnsureBodyPart(CombatBodyPart.RightArm, 22f);
        EnsureBodyPart(CombatBodyPart.LeftLeg, 26f);
        EnsureBodyPart(CombatBodyPart.RightLeg, 26f);
    }

    private void EnsureBodyPart(CombatBodyPart bodyPart, float maxHealth)
    {
        CharacterBodyPartHealthState part = bodyParts.FirstOrDefault(value => value.bodyPart == bodyPart);
        if (part == null)
        {
            bodyParts.Add(CreateBodyPart(bodyPart, maxHealth));
            return;
        }

        part.maxHealth = Mathf.Max(1f, part.maxHealth);
        part.currentHealth = Mathf.Clamp(part.currentHealth, 0f, part.maxHealth);
        part.bleedingPerSecond = Mathf.Max(0f, part.bleedingPerSecond);
    }

    private CharacterBodyPartHealthState GetBodyPart(CombatBodyPart bodyPart)
    {
        EnsureBodyParts();
        return bodyParts.First(part => part.bodyPart == bodyPart);
    }

    private float CalculateConsciousness()
    {
        if (bodyParts.Count == 0)
        {
            return 1f;
        }

        float vitalHealth = Mathf.Min(
            GetBodyPart(CombatBodyPart.Head).HealthRatio,
            GetBodyPart(CombatBodyPart.Torso).HealthRatio);
        return Mathf.Clamp01(vitalHealth * Mathf.Lerp(1f, 0.2f, BloodLoss / 100f));
    }

    private float CalculateLimbAverage(CombatBodyPart first, CombatBodyPart second)
    {
        if (bodyParts.Count == 0)
        {
            return 1f;
        }

        return Mathf.Clamp01((GetBodyPart(first).HealthRatio + GetBodyPart(second).HealthRatio) * 0.5f);
    }

    private bool IsVitalPartDestroyed()
    {
        return bodyParts.Count > 0
            && (GetBodyPart(CombatBodyPart.Head).currentHealth <= 0f
                || GetBodyPart(CombatBodyPart.Torso).currentHealth <= 0f);
    }

    private void UpdateDowned()
    {
        IsDowned = !IsDead && (Consciousness < 0.25f || Mobility < 0.2f);
    }

    private static CharacterBodyPartHealthState CreateBodyPart(
        CombatBodyPart bodyPart,
        float maxHealth)
    {
        return new CharacterBodyPartHealthState
        {
            bodyPart = bodyPart,
            maxHealth = maxHealth,
            currentHealth = maxHealth
        };
    }

    private static CharacterBodyPartHealthState CloneBodyPart(CharacterBodyPartHealthState source)
    {
        return new CharacterBodyPartHealthState
        {
            bodyPart = source.bodyPart,
            maxHealth = source.maxHealth,
            currentHealth = source.currentHealth,
            bleedingPerSecond = source.bleedingPerSecond
        };
    }
}

public sealed class OffenseBattleCommand
{
    public OffenseBattleCommand(
        long commandId,
        string actorId,
        OffenseBattleActionType actionType,
        string targetId = "",
        string abilityId = "")
    {
        CommandId = commandId;
        ActorId = actorId ?? string.Empty;
        ActionType = actionType;
        TargetId = targetId ?? string.Empty;
        AbilityId = abilityId ?? string.Empty;
    }

    public long CommandId { get; }
    public string ActorId { get; }
    public OffenseBattleActionType ActionType { get; }
    public string TargetId { get; }
    public string AbilityId { get; }
}

public sealed class OffenseBattleCommandResult
{
    public OffenseBattleCommandResult(bool accepted, string message, float amount = 0f)
    {
        Accepted = accepted;
        Message = message ?? string.Empty;
        Amount = Mathf.Max(0f, amount);
    }

    public bool Accepted { get; }
    public string Message { get; }
    public float Amount { get; }
}

[Serializable]
public sealed class OffenseBattlePersistenceState
{
    public string battleId = string.Empty;
    public string expeditionId = string.Empty;
    public string targetId = string.Empty;
    public string targetTitle = string.Empty;
    public DungeonDifficulty difficulty = DungeonDifficulty.Normal;
    public OffenseBattleOutcome outcome = OffenseBattleOutcome.InProgress;
    public int roundNumber = 1;
    public int currentOrderIndex;
    public long lastProcessedCommandId;
    public List<string> initiativeOrder = new List<string>();
    public List<string> log = new List<string>();
    public List<OffenseThrownEquipmentPersistenceState> thrownEquipment =
        new List<OffenseThrownEquipmentPersistenceState>();
    public List<OffenseBattleCombatantPersistenceState> combatants =
        new List<OffenseBattleCombatantPersistenceState>();
}

[Serializable]
public sealed class OffenseThrownEquipmentPersistenceState
{
    public string instanceId = string.Empty;
    public string ownerCharacterId = string.Empty;
}

[Serializable]
public sealed class OffenseBattleCombatantPersistenceState
{
    public string persistentId = string.Empty;
    public float maxHealth;
    public float attack;
    public float strength;
    public float toughness;
    public float dexterity;
    public float moveSpeed;
    public float shooting;
    public float evasion;
    public float currentHealth;
    public float totalDamageTaken;
    public float initiativePenalty;
    public int turnsStarted;
    public OffenseFormationSlot formation;
    public float suppression;
    public float bloodLoss;
    public CombatBodyPart lastHitBodyPart = CombatBodyPart.Torso;
    public CombatFireMode fireMode = CombatFireMode.Aimed;
    public List<CharacterBodyPartHealthState> bodyParts = new List<CharacterBodyPartHealthState>();
    public List<OffenseBattleCooldownPersistenceState> cooldowns =
        new List<OffenseBattleCooldownPersistenceState>();
    public List<OffenseBattleStatusPersistenceState> statuses =
        new List<OffenseBattleStatusPersistenceState>();
}

[Serializable]
public sealed class OffenseBattleCooldownPersistenceState
{
    public string abilityId = string.Empty;
    public int remainingTurns;
}

[Serializable]
public sealed class OffenseBattleStatusPersistenceState
{
    public string id = string.Empty;
    public OffenseBattleStatusType type;
    public float value;
    public int remainingTurns;
    public string sourceId = string.Empty;
}

public sealed class OffenseBattleSession
{
    private const int MaxLogEntries = 60;
    private readonly List<OffenseBattleCombatant> combatants;
    private readonly IReadOnlyList<OffenseBattleCombatant> combatantsView;
    private readonly List<string> initiativeOrder = new List<string>();
    private readonly IReadOnlyList<string> initiativeOrderView;
    private readonly List<string> log = new List<string>();
    private readonly IReadOnlyList<string> logView;
    private readonly ICombatResolutionService combatResolution;
    private readonly ICombatEquipmentRuntime combatEquipmentRuntime;
    private readonly Dictionary<string, string> thrownOwnerByInstance =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private bool recoverableEquipmentFinalized;
    private int currentOrderIndex = -1;

    public OffenseBattleSession(
        string battleId,
        string expeditionId,
        string targetId,
        string targetTitle,
        DungeonDifficulty difficulty,
        IEnumerable<OffenseBattleCombatant> combatants,
        ICombatResolutionService combatResolution = null,
        ICombatEquipmentRuntime combatEquipmentRuntime = null)
        : this(
            battleId,
            expeditionId,
            targetId,
            targetTitle,
            difficulty,
            combatants,
            combatResolution,
            combatEquipmentRuntime,
            true)
    {
    }

    private OffenseBattleSession(
        string battleId,
        string expeditionId,
        string targetId,
        string targetTitle,
        DungeonDifficulty difficulty,
        IEnumerable<OffenseBattleCombatant> combatants,
        ICombatResolutionService combatResolution,
        ICombatEquipmentRuntime combatEquipmentRuntime,
        bool startImmediately)
    {
        BattleId = string.IsNullOrWhiteSpace(battleId) ? Guid.NewGuid().ToString("N") : battleId;
        ExpeditionId = expeditionId ?? string.Empty;
        TargetId = targetId ?? string.Empty;
        TargetTitle = targetTitle ?? string.Empty;
        Difficulty = difficulty;
        this.combatants = combatants?
            .Where(combatant => combatant != null)
            .GroupBy(combatant => combatant.PersistentId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList() ?? new List<OffenseBattleCombatant>();
        combatantsView = this.combatants.AsReadOnly();
        initiativeOrderView = initiativeOrder.AsReadOnly();
        logView = log.AsReadOnly();
        this.combatResolution = combatResolution
            ?? new CombatResolutionService(
                new UnityCombatRandomSource(
                    new DungeonStory.Foundation.RandomStreamProvider(1)));
        this.combatEquipmentRuntime = combatEquipmentRuntime;

        Outcome = startImmediately ? ResolveOutcome() : OffenseBattleOutcome.InProgress;
        if (startImmediately && Outcome == OffenseBattleOutcome.InProgress)
        {
            RoundNumber = 1;
            BuildInitiativeOrder();
            currentOrderIndex = 0;
            PrepareCurrentTurn();
            if (!IsComplete && CurrentActor?.PinnedThisTurn == true)
            {
                AddLog($"{CurrentActor.DisplayName}은(는) 제압되어 이번 행동을 잃었습니다.");
                AdvanceTurn();
            }
            AddLog($"{TargetTitle} 전투가 시작되었습니다.");
        }
    }

    public string BattleId { get; }
    public string ExpeditionId { get; }
    public string TargetId { get; }
    public string TargetTitle { get; }
    public DungeonDifficulty Difficulty { get; }
    public OffenseBattleOutcome Outcome { get; private set; }
    public int RoundNumber { get; private set; }
    public long LastProcessedCommandId { get; private set; }
    public int CurrentOrderIndex => currentOrderIndex;
    public IReadOnlyList<OffenseBattleCombatant> Combatants => combatantsView;
    public IReadOnlyList<string> InitiativeOrder => initiativeOrderView;
    public IReadOnlyList<string> Log => logView;
    public bool IsComplete => Outcome != OffenseBattleOutcome.InProgress;
    public OffenseBattleCombatant CurrentActor => currentOrderIndex >= 0
        && currentOrderIndex < initiativeOrder.Count
            ? FindCombatant(initiativeOrder[currentOrderIndex])
            : null;

    public OffenseBattleCombatant FindCombatant(string persistentId)
    {
        return combatants.FirstOrDefault(combatant => string.Equals(
            combatant.PersistentId,
            persistentId,
            StringComparison.Ordinal));
    }

    public OffenseBattlePersistenceState CapturePersistentState()
    {
        return new OffenseBattlePersistenceState
        {
            battleId = BattleId,
            expeditionId = ExpeditionId,
            targetId = TargetId,
            targetTitle = TargetTitle,
            difficulty = Difficulty,
            outcome = Outcome,
            roundNumber = RoundNumber,
            currentOrderIndex = currentOrderIndex,
            lastProcessedCommandId = LastProcessedCommandId,
            initiativeOrder = initiativeOrder.ToList(),
            log = log.ToList(),
            thrownEquipment = thrownOwnerByInstance
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new OffenseThrownEquipmentPersistenceState
                {
                    instanceId = pair.Key,
                    ownerCharacterId = pair.Value
                })
                .ToList(),
            combatants = combatants.Select(combatant => new OffenseBattleCombatantPersistenceState
            {
                persistentId = combatant.PersistentId,
                maxHealth = combatant.Stats.MaxHealth,
                attack = combatant.Stats.Attack,
                strength = combatant.Stats.Strength,
                toughness = combatant.Stats.Toughness,
                dexterity = combatant.Stats.Dexterity,
                moveSpeed = combatant.Stats.MoveSpeed,
                shooting = combatant.Stats.Shooting,
                evasion = combatant.Stats.Evasion,
                currentHealth = combatant.CurrentHealth,
                totalDamageTaken = combatant.TotalDamageTaken,
                initiativePenalty = combatant.InitiativePenalty,
                turnsStarted = combatant.TurnsStarted,
                formation = combatant.Formation,
                suppression = combatant.Suppression,
                bloodLoss = combatant.BloodLoss,
                lastHitBodyPart = combatant.LastHitBodyPart,
                fireMode = combatant.FireMode,
                bodyParts = combatant.BodyParts
                    .Select(part => new CharacterBodyPartHealthState
                    {
                        bodyPart = part.bodyPart,
                        maxHealth = part.maxHealth,
                        currentHealth = part.currentHealth,
                        bleedingPerSecond = part.bleedingPerSecond
                    })
                    .ToList(),
                cooldowns = combatant.GetCooldownSnapshot()
                    .Where(pair => pair.Value > 0)
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new OffenseBattleCooldownPersistenceState
                    {
                        abilityId = pair.Key,
                        remainingTurns = pair.Value
                    })
                    .ToList(),
                statuses = combatant.Statuses.Select(status => new OffenseBattleStatusPersistenceState
                {
                    id = status.Id,
                    type = status.Type,
                    value = status.Value,
                    remainingTurns = status.RemainingTurns,
                    sourceId = status.SourceId
                }).ToList()
            }).ToList()
        };
    }

    public static OffenseBattleSession Restore(
        OffenseBattlePersistenceState state,
        IEnumerable<OffenseBattleCombatant> configuredCombatants,
        ICombatResolutionService combatResolution = null,
        ICombatEquipmentRuntime combatEquipmentRuntime = null)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        OffenseBattleSession session = new OffenseBattleSession(
            state.battleId,
            state.expeditionId,
            state.targetId,
            state.targetTitle,
            state.difficulty,
            configuredCombatants,
            combatResolution,
            combatEquipmentRuntime,
            false);
        foreach (OffenseBattleCombatantPersistenceState saved in state.combatants
            ?? new List<OffenseBattleCombatantPersistenceState>())
        {
            OffenseBattleCombatant combatant = session.FindCombatant(saved?.persistentId);
            if (combatant == null) continue;

            if (saved.maxHealth > 0f)
            {
                combatant.RestoreStats(new OffenseBattleStats(
                    saved.maxHealth,
                    saved.attack,
                    saved.strength,
                    saved.toughness,
                    saved.dexterity,
                    saved.moveSpeed,
                    saved.shooting,
                    saved.evasion));
            }
            combatant.RestoreHealth(saved.currentHealth, saved.totalDamageTaken);
            combatant.RestoreInitiativePenalty(saved.initiativePenalty);
            combatant.RestoreTurnsStarted(saved.turnsStarted);
            combatant.RestoreFormation(saved.formation);
            combatant.RestoreCombatState(
                saved.suppression,
                saved.bloodLoss,
                saved.lastHitBodyPart,
                saved.fireMode);
            if (saved.bodyParts != null && saved.bodyParts.Count > 0)
            {
                combatant.ApplyBodyHealth(new CharacterBodyHealthSnapshot(
                    saved.bodyParts,
                    saved.bloodLoss,
                    saved.suppression,
                    1f,
                    1f,
                    1f,
                    false));
            }
            combatant.RestoreCooldowns((saved.cooldowns
                    ?? new List<OffenseBattleCooldownPersistenceState>())
                .Where(value => value != null)
                .Select(value => new KeyValuePair<string, int>(value.abilityId, value.remainingTurns)));
            combatant.RestoreStatuses((saved.statuses
                    ?? new List<OffenseBattleStatusPersistenceState>())
                .Where(value => value != null)
                .Select(value => new OffenseBattleStatus(
                    value.id,
                    value.type,
                    value.value,
                    value.remainingTurns,
                    value.sourceId)));
        }

        session.RestoreTurnState(
            state.roundNumber,
            state.initiativeOrder,
            state.currentOrderIndex,
            state.lastProcessedCommandId,
            state.outcome,
            state.log);
        foreach (OffenseThrownEquipmentPersistenceState thrown in state.thrownEquipment
            ?? new List<OffenseThrownEquipmentPersistenceState>())
        {
            if (thrown != null && !string.IsNullOrWhiteSpace(thrown.instanceId))
            {
                session.thrownOwnerByInstance[thrown.instanceId] =
                    thrown.ownerCharacterId ?? string.Empty;
            }
        }

        session.FinalizeRecoverableEquipment();
        return session;
    }

    public bool TryExecuteCommand(
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result)
    {
        if (command == null)
        {
            result = new OffenseBattleCommandResult(false, "명령이 없습니다.");
            return false;
        }

        if (IsComplete)
        {
            result = new OffenseBattleCommandResult(false, "이미 끝난 전투입니다.");
            return false;
        }

        if (command.CommandId <= LastProcessedCommandId)
        {
            result = new OffenseBattleCommandResult(false, "이미 처리한 명령입니다.");
            return false;
        }

        OffenseBattleCombatant actor = CurrentActor;
        if (actor == null || !actor.CanTakeTurn
            || !string.Equals(actor.PersistentId, command.ActorId, StringComparison.Ordinal))
        {
            result = new OffenseBattleCommandResult(false, "현재 행동할 캐릭터가 아닙니다.");
            return false;
        }

        if (!TryResolveCommand(actor, command, out result))
        {
            return false;
        }

        LastProcessedCommandId = command.CommandId;
        Outcome = ResolveOutcome();
        if (!IsComplete)
        {
            AdvanceTurn();
        }

        FinalizeRecoverableEquipment();
        return true;
    }

    public OffenseBattleCommand CreateEnemyCommand(long commandId)
    {
        OffenseBattleCombatant actor = CurrentActor;
        if (actor == null || actor.Team != OffenseBattleTeam.Enemies || !actor.CanTakeTurn)
        {
            return null;
        }

        List<OffenseBattleCombatant> targets = combatants
            .Where(target => target.Team == OffenseBattleTeam.Allies
                && !target.IsDead
                && !target.IsDowned)
            .Where(target => IsReachableByBasicAttack(actor, target))
            .OrderBy(target => WouldBasicAttackKill(actor, target) ? 0 : 1)
            .ThenBy(target => target.HealthRatio)
            .ThenByDescending(target => target.Stats.Attack)
            .ThenBy(target => target.PersistentId, StringComparer.Ordinal)
            .ToList();
        OffenseBattleCombatant target = targets.FirstOrDefault();
        if (target == null)
        {
            return null;
        }

        CharacterCombatAbilityDefinition bestAbility = actor.Abilities
            .Where(ability => actor.GetCooldown(ability.Id) <= 0
                && ability.TargetRule == OffenseBattleTargetRule.Enemy
                && IsPositionAllowed(ability.UsableFrom, actor.Formation)
                && combatants.Any(candidate => IsAbilityTargetValid(actor, candidate, ability)))
            .OrderByDescending(EstimateAbilityDamageMultiplier)
            .ThenBy(ability => ability.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (bestAbility != null && EstimateAbilityDamageMultiplier(bestAbility) > 1.05f)
        {
            OffenseBattleCombatant abilityTarget = combatants
                .Where(candidate => IsAbilityTargetValid(actor, candidate, bestAbility))
                .OrderBy(candidate => candidate.HealthRatio)
                .ThenBy(candidate => candidate.PersistentId, StringComparer.Ordinal)
                .First();
            return new OffenseBattleCommand(
                commandId,
                actor.PersistentId,
                OffenseBattleActionType.Ability,
                abilityTarget.PersistentId,
                bestAbility.Id);
        }

        if (actor.HealthRatio < 0.25f)
        {
            return new OffenseBattleCommand(
                commandId,
                actor.PersistentId,
                OffenseBattleActionType.Guard,
                actor.PersistentId);
        }

        return new OffenseBattleCommand(
            commandId,
            actor.PersistentId,
            OffenseBattleActionType.BasicAttack,
            target.PersistentId);
    }

    public float CalculateBasicDamage(OffenseBattleCombatant source, OffenseBattleCombatant target)
    {
        if (source == null || target == null)
        {
            return 0f;
        }

        float attackMultiplier = 1f + source.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.AttackModifier)
            .Sum(status => status.Value);
        CombatWeaponSnapshot weapon = source.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        CombatAttackVerb verb = weapon.Verb ?? CombatWeaponSnapshot.CreateUnarmed().Verb;
        CombatRangeBand band = CombatRangeRules.GetBand(GetFormationDistance(source, target));
        float rangeDamage = Mathf.Max(0.1f, weapon.GetDamageMultiplier(band));
        return Mathf.Max(
            1f,
            (verb.baseDamage
                + (weapon.IsRanged ? source.Stats.Shooting * 0.45f : source.Stats.Attack * 0.75f)
                + source.Stats.Strength * 0.35f)
            * rangeDamage
            * Mathf.Max(0.1f, attackMultiplier)
            - target.Stats.Toughness * 0.2f);
    }

    public CombatAttackPreview PreviewBasicAttack(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        if (source == null || target == null)
        {
            return new CombatAttackPreview(
                false,
                "공격 대상을 선택하세요.",
                CombatRangeBand.OutOfRange,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f);
        }

        int distance = GetFormationDistance(source, target);
        return combatResolution.Preview(new CombatAttackRequest(
            $"{BattleId}:preview",
            source.PersistentId,
            target.PersistentId,
            CreateCombatStats(source),
            CreateCombatStats(target),
            source.Weapon ?? CombatWeaponSnapshot.CreateUnarmed(),
            distance,
            source.FireMode,
            target.CoverBlockChance > 0f
                ? new CombatCoverSnapshot(
                    CombatCoverHeight.Low,
                    target.CoverBlockChance,
                    0f,
                    "offense-cover")
                : default,
            defenderDowned: target.IsDowned,
            defenderMeleeLocked: distance <= 1,
            attackerSuppression: source.Suppression,
            defenderSuppression: target.Suppression,
            defenderArmor: target.Armor));
    }

    public int GetFormationDistanceForPreview(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        return source == null || target == null
            ? 0
            : GetFormationDistance(source, target);
    }

    internal float ApplyDamage(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target,
        float rawAmount)
    {
        if (target == null || target.IsDead)
        {
            return 0f;
        }

        float guard = target.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Guard)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float vulnerability = target.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Vulnerability)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float finalAmount = Mathf.Max(1f, rawAmount * (1f - Mathf.Clamp01(guard)) * (1f + vulnerability));
        float applied = target.ApplyRawDamage(finalAmount);
        if (target.IsDead)
        {
            AddLog($"{target.DisplayName}이(가) 쓰러졌습니다.");
            CompactFormation(target.Team);
        }
        else if (target.IsDowned)
        {
            AddLog($"{target.DisplayName}은(는) 부상으로 쓰러졌습니다.");
            CompactFormation(target.Team);
        }

        return applied;
    }

    internal float Heal(OffenseBattleCombatant target, float amount)
    {
        return target?.Heal(amount) ?? 0f;
    }

    internal void AddStatus(
        OffenseBattleCombatant target,
        OffenseBattleStatusType type,
        float value,
        int turns,
        string sourceId,
        string statusId)
    {
        target?.AddStatus(new OffenseBattleStatus(statusId, type, value, turns, sourceId));
    }

    internal void Delay(OffenseBattleCombatant target, float amount)
    {
        target?.AddInitiativePenalty(amount);
    }

    internal int Cleanse(OffenseBattleCombatant target, int maximum)
    {
        return target?.RemoveStatuses(
            status => status.Type == OffenseBattleStatusType.Vulnerability
                || status.Type == OffenseBattleStatusType.DamageOverTime
                || (status.Type == OffenseBattleStatusType.AttackModifier && status.Value < 0f),
            maximum) ?? 0;
    }

    internal void AdjustCooldowns(OffenseBattleCombatant target, int delta)
    {
        target?.AdjustCooldowns(delta);
    }

    internal IReadOnlyList<OffenseBattleCombatant> GetLivingTeam(OffenseBattleTeam team)
    {
        return combatants.Where(combatant => combatant.Team == team && !combatant.IsDead).ToArray();
    }

    internal void Reposition(OffenseBattleCombatant target, int offset)
    {
        if (target == null || offset == 0)
        {
            return;
        }

        int next = Mathf.Clamp((int)target.Formation + offset, 0, 2);
        target.SetFormation((OffenseFormationSlot)next);
    }

    public void AbortForOwnerDeath()
    {
        if (IsComplete)
        {
            return;
        }

        Outcome = OffenseBattleOutcome.AbortedOwnerDeath;
        AddLog("사장이 쓰러져 원정 전투가 중단되었습니다.");
    }

    internal void RestoreTurnState(
        int roundNumber,
        IEnumerable<string> restoredInitiativeOrder,
        int restoredCurrentOrderIndex,
        long lastProcessedCommandId,
        OffenseBattleOutcome outcome,
        IEnumerable<string> restoredLog)
    {
        RoundNumber = Mathf.Max(1, roundNumber);
        initiativeOrder.Clear();
        initiativeOrder.AddRange((restoredInitiativeOrder ?? Array.Empty<string>())
            .Where(id => FindCombatant(id) != null)
            .Distinct(StringComparer.Ordinal));
        if (initiativeOrder.Count == 0 && outcome == OffenseBattleOutcome.InProgress)
        {
            BuildInitiativeOrder();
        }

        currentOrderIndex = Mathf.Clamp(restoredCurrentOrderIndex, 0, Mathf.Max(0, initiativeOrder.Count - 1));
        LastProcessedCommandId = Math.Max(0, lastProcessedCommandId);
        Outcome = outcome;
        log.Clear();
        log.AddRange((restoredLog ?? Array.Empty<string>()).TakeLast(MaxLogEntries));
    }

    private bool TryResolveCommand(
        OffenseBattleCombatant actor,
        OffenseBattleCommand command,
        out OffenseBattleCommandResult result)
    {
        switch (command.ActionType)
        {
            case OffenseBattleActionType.BasicAttack:
                return TryBasicAttack(actor, command.TargetId, out result);
            case OffenseBattleActionType.Guard:
                AddStatus(
                    actor,
                    OffenseBattleStatusType.Guard,
                    0.5f,
                    1,
                    actor.PersistentId,
                    $"common-guard:{actor.PersistentId}");
                AddLog($"{actor.DisplayName}이(가) 방어 태세를 취했습니다.");
                result = new OffenseBattleCommandResult(true, "방어 태세");
                return true;
            case OffenseBattleActionType.Retreat:
                if (actor.Team != OffenseBattleTeam.Allies)
                {
                    result = new OffenseBattleCommandResult(false, "적은 후퇴 명령을 사용할 수 없습니다.");
                    return false;
                }

                Outcome = OffenseBattleOutcome.Retreated;
                AddLog($"{actor.DisplayName}의 지시로 원정대가 후퇴했습니다.");
                result = new OffenseBattleCommandResult(true, "후퇴했습니다.");
                return true;
            case OffenseBattleActionType.Ability:
                return TryUseAbility(actor, command.TargetId, command.AbilityId, out result);
            case OffenseBattleActionType.Reload:
                return TryReloadWeapon(actor, out result);
            case OffenseBattleActionType.SwitchWeapon:
                return TrySwitchWeapon(actor, command.AbilityId, out result);
            case OffenseBattleActionType.SetFireMode:
                return TrySetFireMode(actor, command.AbilityId, out result);
            default:
                result = new OffenseBattleCommandResult(false, "지원하지 않는 행동입니다.");
                return false;
        }
    }

    private bool TryBasicAttack(
        OffenseBattleCombatant actor,
        string targetId,
        out OffenseBattleCommandResult result)
    {
        OffenseBattleCombatant target = FindCombatant(targetId);
        if (!IsValidTarget(actor, target, OffenseBattleTargetRule.Enemy)
            || !IsReachableByBasicAttack(actor, target))
        {
            result = new OffenseBattleCommandResult(false, "공격할 수 없는 대상입니다.");
            return false;
        }

        CombatWeaponSnapshot weapon = actor.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        int distance = GetFormationDistance(actor, target);
        CombatAttackResult resolved = combatResolution.Resolve(new CombatAttackRequest(
            $"{BattleId}:{LastProcessedCommandId + 1}:basic",
            actor.PersistentId,
            target.PersistentId,
            CreateCombatStats(actor),
            CreateCombatStats(target),
            weapon,
            distance,
            actor.FireMode,
            target.CoverBlockChance > 0f
                ? new CombatCoverSnapshot(CombatCoverHeight.Low, target.CoverBlockChance, 0f, "offense-cover")
                : default,
            defenderDowned: target.HealthRatio <= 0.15f,
            defenderMeleeLocked: distance <= 1,
            attackerSuppression: actor.Suppression,
            defenderSuppression: target.Suppression,
            defenderArmor: target.Armor));
        if (!resolved.Executed)
        {
            result = new OffenseBattleCommandResult(false, resolved.FailureReason);
            return false;
        }

        if (weapon.RequiresAmmo && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            combatEquipmentRuntime?.TryConsumeLoadedAmmo(weapon.InstanceId);
            if (combatEquipmentRuntime != null
                && combatEquipmentRuntime.TryGetActiveWeapon(
                    actor.PersistentId,
                    out CombatWeaponSnapshot refreshed))
            {
                actor.SetCombatEquipment(refreshed, actor.Armor);
            }
        }
        else if (weapon.Verb?.DropsWeaponOnUse == true
            && !string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            thrownOwnerByInstance[weapon.InstanceId] = actor.PersistentId;
            actor.SetCombatEquipment(CombatWeaponSnapshot.CreateUnarmed(), actor.Armor);
        }

        float damage = ApplyResolvedCombatDamage(actor, target, resolved);
        string combatMessage = resolved.Hit
            ? $"{actor.DisplayName}이(가) {target.DisplayName}의 {GetBodyPartName(resolved.BodyPart)}에 {damage:0.#} 피해를 줬습니다."
            : resolved.CoverBlocked
                ? $"{target.DisplayName}이(가) 엄폐물 뒤에서 공격을 피했습니다."
                : resolved.Evaded
                    ? $"{target.DisplayName}이(가) 공격을 회피했습니다."
                    : $"{actor.DisplayName}의 공격이 빗나갔습니다.";
        AddLog(combatMessage);
        result = new OffenseBattleCommandResult(true, combatMessage, damage);
        return true;
    }

    private bool TryReloadWeapon(
        OffenseBattleCombatant actor,
        out OffenseBattleCommandResult result)
    {
        CombatWeaponSnapshot weapon = actor?.Weapon;
        if (actor == null
            || weapon == null
            || !weapon.RequiresAmmo
            || string.IsNullOrWhiteSpace(weapon.InstanceId)
            || combatEquipmentRuntime == null
            || !combatEquipmentRuntime.TryReloadFromCharacterInventory(
                actor.PersistentId,
                weapon.InstanceId,
                out int consumedAmmo)
            || consumedAmmo <= 0
            || !combatEquipmentRuntime.TryGetActiveWeapon(
                actor.PersistentId,
                out CombatWeaponSnapshot refreshed))
        {
            result = new OffenseBattleCommandResult(false, "재장전할 탄약이 없습니다.");
            return false;
        }

        actor.SetCombatEquipment(refreshed, actor.Armor);
        result = new OffenseBattleCommandResult(
            true,
            $"{actor.DisplayName}이(가) {consumedAmmo}발을 재장전했습니다.");
        return true;
    }

    private bool TrySwitchWeapon(
        OffenseBattleCombatant actor,
        string instanceId,
        out OffenseBattleCommandResult result)
    {
        string failureReason = string.Empty;
        if (actor == null
            || combatEquipmentRuntime == null
            || !combatEquipmentRuntime.TrySetActiveWeapon(
                actor.PersistentId,
                instanceId,
                out failureReason)
            || !combatEquipmentRuntime.TryGetActiveWeapon(
                actor.PersistentId,
                out CombatWeaponSnapshot weapon))
        {
            result = new OffenseBattleCommandResult(
                false,
                string.IsNullOrWhiteSpace(failureReason)
                    ? "교체할 무기가 없습니다."
                    : failureReason);
            return false;
        }

        actor.SetCombatEquipment(weapon, actor.Armor);
        result = new OffenseBattleCommandResult(
            true,
            $"{actor.DisplayName}이(가) 무기를 교체했습니다.");
        return true;
    }

    private static bool TrySetFireMode(
        OffenseBattleCombatant actor,
        string modeId,
        out OffenseBattleCommandResult result)
    {
        if (actor == null
            || !Enum.TryParse(modeId, ignoreCase: true, out CombatFireMode mode)
            || !SupportsFireMode(actor.Weapon, mode))
        {
            result = new OffenseBattleCommandResult(false, "이 무기로 사용할 수 없는 사격 모드입니다.");
            return false;
        }

        actor.SetFireMode(mode);
        result = new OffenseBattleCommandResult(
            true,
            $"사격 모드를 {GetFireModeName(mode)}(으)로 변경했습니다.");
        return true;
    }

    private static bool SupportsFireMode(CombatWeaponSnapshot weapon, CombatFireMode mode)
    {
        if (weapon == null || !weapon.IsRanged)
        {
            return false;
        }

        return mode switch
        {
            CombatFireMode.Aimed => weapon.SupportsAimed,
            CombatFireMode.Rapid => weapon.SupportsRapid,
            CombatFireMode.Suppressive => weapon.SupportsSuppressive,
            _ => false
        };
    }

    private static string GetFireModeName(CombatFireMode mode)
    {
        return mode switch
        {
            CombatFireMode.Rapid => "속사",
            CombatFireMode.Suppressive => "제압",
            _ => "조준"
        };
    }

    private void FinalizeRecoverableEquipment()
    {
        if (!IsComplete || recoverableEquipmentFinalized)
        {
            return;
        }

        recoverableEquipmentFinalized = true;
        bool battleRecovered = Outcome == OffenseBattleOutcome.Victory;
        foreach (KeyValuePair<string, string> thrown in thrownOwnerByInstance)
        {
            OffenseBattleCombatant owner = FindCombatant(thrown.Value);
            if (!battleRecovered || owner == null || owner.IsDead)
            {
                combatEquipmentRuntime?.TryMarkLost(thrown.Key);
            }
        }
    }

    private bool TryUseAbility(
        OffenseBattleCombatant actor,
        string targetId,
        string abilityId,
        out OffenseBattleCommandResult result)
    {
        CharacterCombatAbilityDefinition ability = actor.Abilities.FirstOrDefault(value => string.Equals(
            value.Id,
            abilityId,
            StringComparison.Ordinal));
        if (ability == null)
        {
            result = new OffenseBattleCommandResult(false, "사용할 수 없는 능력입니다.");
            return false;
        }

        int cooldown = actor.GetCooldown(ability.Id);
        if (cooldown > 0)
        {
            result = new OffenseBattleCommandResult(false, $"재사용까지 {cooldown}턴 남았습니다.");
            return false;
        }

        if (!IsPositionAllowed(ability.UsableFrom, actor.Formation))
        {
            result = new OffenseBattleCommandResult(
                false,
                $"{OffenseFormationUtility.GetDisplayName(actor.Formation)}에서는 이 능력을 사용할 수 없습니다.");
            return false;
        }

        OffenseBattleCombatant target = ability.TargetRule == OffenseBattleTargetRule.Self
            ? actor
            : FindCombatant(targetId);
        if (!IsAbilityTargetValid(actor, target, ability))
        {
            result = new OffenseBattleCommandResult(false, "능력을 사용할 수 없는 대상입니다.");
            return false;
        }

        OffenseBattleEffectContext context = new OffenseBattleEffectContext(this, actor, target);
        foreach (OffenseCombatEffectModule effect in ability.Effects)
        {
            effect?.Apply(context);
        }

        actor.SetCooldown(ability.Id, ability.CooldownTurns);
        AddLog($"{actor.DisplayName}이(가) {ability.DisplayName}을(를) 사용했습니다.");
        result = new OffenseBattleCommandResult(true, ability.DisplayName, context.DamageDealt);
        return true;
    }

    private void AdvanceTurn()
    {
        int attempts = 0;
        do
        {
            currentOrderIndex++;
            if (currentOrderIndex >= initiativeOrder.Count)
            {
                RoundNumber++;
                BuildInitiativeOrder();
                currentOrderIndex = 0;
            }

            PrepareCurrentTurn();
            Outcome = ResolveOutcome();
            if (!IsComplete && CurrentActor?.PinnedThisTurn == true)
            {
                AddLog($"{CurrentActor.DisplayName}은(는) 제압되어 이번 행동을 잃었습니다.");
            }
            attempts++;
        }
        while (!IsComplete
            && (CurrentActor == null || !CurrentActor.CanTakeTurn)
            && attempts <= combatants.Count * 2);
    }

    private void PrepareCurrentTurn()
    {
        OffenseBattleCombatant actor = CurrentActor;
        if (actor == null || actor.IsDead || actor.IsDowned)
        {
            return;
        }

        actor.BeginTurn();
        foreach (OffenseBattleStatus status in actor.Statuses.ToArray())
        {
            if (status.Type == OffenseBattleStatusType.DamageOverTime)
            {
                OffenseBattleCombatant source = FindCombatant(status.SourceId);
                float damage = ApplyDamage(source, actor, status.Value);
                AddLog($"{actor.DisplayName}이(가) 지속 피해 {damage:0.#}을 받았습니다.");
            }

            if (status.ConsumeTurn())
            {
                actor.RemoveStatus(status);
            }
        }
    }

    private void BuildInitiativeOrder()
    {
        initiativeOrder.Clear();
        initiativeOrder.AddRange(combatants
            .Where(combatant => !combatant.IsDead && !combatant.IsDowned)
            .OrderByDescending(combatant => combatant.Initiative)
            .ThenBy(combatant => combatant.PersistentId, StringComparer.Ordinal)
            .Select(combatant => combatant.PersistentId));
    }

    private OffenseBattleOutcome ResolveOutcome()
    {
        bool alliesAlive = combatants.Any(combatant =>
            combatant.Team == OffenseBattleTeam.Allies
            && !combatant.IsDead
            && !combatant.IsDowned);
        bool enemiesAlive = combatants.Any(combatant =>
            combatant.Team == OffenseBattleTeam.Enemies
            && !combatant.IsDead
            && !combatant.IsDowned);
        if (!alliesAlive) return OffenseBattleOutcome.Defeat;
        if (!enemiesAlive) return OffenseBattleOutcome.Victory;
        return Outcome is OffenseBattleOutcome.Retreated or OffenseBattleOutcome.AbortedOwnerDeath
            ? Outcome
            : OffenseBattleOutcome.InProgress;
    }

    private bool IsValidTarget(
        OffenseBattleCombatant actor,
        OffenseBattleCombatant target,
        OffenseBattleTargetRule rule)
    {
        if (actor == null || target == null || target.IsDead)
        {
            return false;
        }

        return rule switch
        {
            OffenseBattleTargetRule.Self => ReferenceEquals(actor, target),
            OffenseBattleTargetRule.Ally => actor.Team == target.Team,
            OffenseBattleTargetRule.Enemy => actor.Team != target.Team && !target.IsDowned,
            _ => false
        };
    }

    private bool IsAbilityTargetValid(
        OffenseBattleCombatant actor,
        OffenseBattleCombatant target,
        CharacterCombatAbilityDefinition ability)
    {
        return ability != null
            && IsValidTarget(actor, target, ability.TargetRule)
            && IsPositionAllowed(ability.TargetPositions, target.Formation);
    }

    private bool IsReachableByBasicAttack(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        if (source == null || target == null || target.IsDead || target.IsDowned)
        {
            return false;
        }

        CombatWeaponSnapshot weapon = source.Weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        int distance = GetFormationDistance(source, target);
        if (distance > weapon.MaximumRange
            || weapon.GetAccuracyMultiplier(CombatRangeRules.GetBand(distance)) <= 0f)
        {
            return false;
        }

        if (weapon.IsRanged)
        {
            return true;
        }

        bool hasForwardTarget = combatants.Any(candidate => candidate.Team == target.Team
            && !candidate.IsDead
            && !candidate.IsDowned
            && candidate.Formation != OffenseFormationSlot.Rear);
        return !hasForwardTarget || target.Formation != OffenseFormationSlot.Rear;
    }

    private static bool IsPositionAllowed(OffenseFormationMask mask, OffenseFormationSlot slot)
    {
        return (mask & OffenseFormationUtility.ToMask(slot)) != 0;
    }

    private void CompactFormation(OffenseBattleTeam team)
    {
        OffenseBattleCombatant[] survivors = combatants
            .Where(combatant => combatant.Team == team
                && !combatant.IsDead
                && !combatant.IsDowned)
            .OrderBy(combatant => combatant.Formation)
            .ThenBy(combatant => combatant.PersistentId, StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        for (int index = 0; index < survivors.Length; index++)
        {
            survivors[index].RestoreFormation((OffenseFormationSlot)index);
        }
    }

    private bool WouldBasicAttackKill(OffenseBattleCombatant source, OffenseBattleCombatant target)
    {
        return CalculateBasicDamage(source, target) >= target.CurrentHealth;
    }

    private float ApplyResolvedCombatDamage(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target,
        CombatAttackResult resolved)
    {
        if (!resolved.Hit)
        {
            target.ApplyCombatInjury(resolved);
            return 0f;
        }

        float guard = target.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Guard)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float vulnerability = target.Statuses
            .Where(status => status.Type == OffenseBattleStatusType.Vulnerability)
            .Select(status => status.Value)
            .DefaultIfEmpty(0f)
            .Max();
        float adjustedDamage = Mathf.Max(
            0.5f,
            resolved.AppliedDamage
            * (1f - Mathf.Clamp01(guard))
            * (1f + vulnerability));
        CombatAttackResult adjusted = new CombatAttackResult(
            resolved.Executed,
            resolved.Hit,
            resolved.CoverBlocked,
            resolved.Evaded,
            resolved.BodyPart,
            resolved.RawDamage,
            adjustedDamage,
            resolved.Bleeding,
            resolved.Suppression,
            resolved.ArmorDurabilityDamage,
            resolved.ArmorInstanceId,
            resolved.FailureReason,
            resolved.ShieldBlocked,
            resolved.CoverSourceId,
            resolved.CoverDamage,
            resolved.ArmorDurabilityHits);
        float applied = target.ApplyCombatInjury(adjusted);
        if (resolved.ArmorDurabilityHits.Count > 0)
        {
            for (int i = 0; i < resolved.ArmorDurabilityHits.Count; i++)
            {
                CombatArmorDurabilityHit hit = resolved.ArmorDurabilityHits[i];
                combatEquipmentRuntime?.TryApplyDurabilityDamage(hit.InstanceId, hit.Damage);
            }
        }
        else if (!string.IsNullOrWhiteSpace(resolved.ArmorInstanceId))
        {
            combatEquipmentRuntime?.TryApplyDurabilityDamage(
                resolved.ArmorInstanceId,
                resolved.ArmorDurabilityDamage);
        }

        if (target.IsDead)
        {
            AddLog($"{target.DisplayName}이(가) 쓰러졌습니다.");
            CompactFormation(target.Team);
        }

        return applied;
    }

    private static CombatStatSnapshot CreateCombatStats(OffenseBattleCombatant combatant)
    {
        float manipulation = combatant.Manipulation * combatant.Consciousness;
        float mobility = combatant.Mobility * combatant.Consciousness;
        return new CombatStatSnapshot(
            combatant.Stats.Attack * manipulation,
            combatant.Stats.Shooting * manipulation,
            combatant.Stats.Evasion * mobility,
            combatant.Stats.MoveSpeed * mobility,
            combatant.Stats.Strength * manipulation,
            combatant.Stats.Toughness,
            combatant.Stats.Dexterity * manipulation,
            Mathf.Min(combatant.HealthRatio, combatant.Consciousness));
    }

    private static int GetFormationDistance(
        OffenseBattleCombatant source,
        OffenseBattleCombatant target)
    {
        return 1 + ((int)source.Formation + (int)target.Formation) * 4;
    }

    private static string GetBodyPartName(CombatBodyPart bodyPart)
    {
        return bodyPart switch
        {
            CombatBodyPart.Head => "머리",
            CombatBodyPart.Torso => "몸통",
            CombatBodyPart.LeftArm => "왼팔",
            CombatBodyPart.RightArm => "오른팔",
            CombatBodyPart.LeftLeg => "왼다리",
            CombatBodyPart.RightLeg => "오른다리",
            _ => "몸"
        };
    }

    private static float EstimateAbilityDamageMultiplier(CharacterCombatAbilityDefinition ability)
    {
        float estimate = 0f;
        foreach (OffenseCombatEffectModule effect in ability?.Effects ?? Array.Empty<OffenseCombatEffectModule>())
        {
            if (effect is OffenseDamageEffect)
            {
                estimate += 1.1f;
            }
            else if (effect is OffenseVulnerabilityEffect || effect is OffenseDamageOverTimeEffect)
            {
                estimate += 0.3f;
            }
        }

        return estimate;
    }

    private void AddLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        log.Add(message);
        if (log.Count > MaxLogEntries)
        {
            log.RemoveRange(0, log.Count - MaxLogEntries);
        }
    }
}

public static class OffenseEncounterCatalog
{
    public static string GetEnemySummary(int campaignOrder)
    {
        return Mathf.Clamp(campaignOrder, 1, 6) switch
        {
            1 => "농장 집행관",
            2 => "상단 경비 + 석궁병",
            3 => "무기고 경비 2명 + 대장",
            4 => "마력 파수기 2명 + 마나 이상체",
            5 => "경쟁 던전 3종족 파티",
            _ => "봉인 수호자 2명 + 진실의 감시자"
        };
    }

    public static OffenseBattleCombatant CreateAlly(
        CharacterActor actor,
        string persistentId,
        OffenseFormationSlot formation = OffenseFormationSlot.Front,
        float stress = 0f,
        ExpeditionEquipmentStatBlock equipment = null)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity;
        float stressMultiplier = Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(stress / 100f));
        equipment ??= ExpeditionEquipmentStatBlock.Empty;
        float maxHealth = Mathf.Max(1f, actor.MaxHealth + equipment.maxHealth);
        return new OffenseBattleCombatant(
            persistentId,
            identity != null ? identity.DisplayName : actor.name,
            identity != null ? identity.SpeciesTag : string.Empty,
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(
                maxHealth,
                actor.GetCharacterStat(CharacterStatType.Attack) * stressMultiplier + equipment.attack,
                actor.GetCharacterStat(CharacterStatType.Strength) * stressMultiplier + equipment.strength,
                actor.GetCharacterStat(CharacterStatType.Toughness) * stressMultiplier + equipment.toughness,
                actor.GetCharacterStat(CharacterStatType.Dexterity) * stressMultiplier + equipment.dexterity,
                actor.GetCharacterStat(CharacterStatType.MoveSpeed) * stressMultiplier + equipment.moveSpeed,
                actor.GetCharacterStat(CharacterStatType.Shooting) * stressMultiplier,
                actor.GetCharacterStat(CharacterStatType.Evasion) * stressMultiplier),
            Mathf.Clamp(actor.CurrentHealth + equipment.maxHealth, 0f, maxHealth),
            CharacterCombatAbilityCatalog.GetAbilities(actor),
            identity?.Data != null ? identity.Data.id : -1,
            formation);
    }

    public static IReadOnlyList<OffenseBattleCombatant> CreateEnemies(
        OffenseTargetDefinition target,
        DungeonDifficulty difficulty,
        OffenseRouteNode routeNode = null)
    {
        int stage = Mathf.Clamp(target?.campaignOrder ?? 1, 1, 6);
        DungeonDifficultyMultipliers multipliers = DungeonDifficultyRules.GetOffenseMultipliers(difficulty);
        List<EnemyTemplate> templates = stage switch
        {
            1 => new List<EnemyTemplate>
            {
                Enemy("farm-enforcer", "농장 집행관", "Human", 82f, 7f, 6f, 5f, 5f, 4f)
            },
            2 => new List<EnemyTemplate>
            {
                Enemy("caravan-guard", "상단 경비", "Human", 88f, 7f, 7f, 7f, 5f, 4f),
                Enemy("caravan-crossbow", "석궁병", "Human", 68f, 9f, 5f, 4f, 8f, 5f, AimedShot())
            },
            3 => new List<EnemyTemplate>
            {
                Enemy("armory-guard-a", "무기고 경비 A", "Human", 92f, 8f, 7f, 8f, 5f, 4f),
                Enemy("armory-guard-b", "무기고 경비 B", "Human", 92f, 8f, 7f, 8f, 5f, 4f),
                Enemy("armory-captain", "무기고 대장", "Human", 118f, 10f, 9f, 9f, 7f, 5f, CaptainBreak())
            },
            4 => new List<EnemyTemplate>
            {
                Enemy("arcane-sentry-a", "마력 파수기 A", "Construct", 96f, 9f, 6f, 9f, 7f, 4f, ArcaneBurn()),
                Enemy("arcane-sentry-b", "마력 파수기 B", "Construct", 96f, 9f, 6f, 9f, 7f, 4f, ArcaneBurn()),
                Enemy("mana-anomaly", "마나 이상체", "Arcane", 125f, 11f, 8f, 7f, 9f, 6f, ManaRend())
            },
            5 => new List<EnemyTemplate>
            {
                Enemy("rival-slime", "경쟁 던전 슬라임", "Slime", 120f, 10f, 7f, 10f, 6f, 4f, CharacterCombatAbilityCatalog.CreateSlimeBarrier()),
                Enemy("rival-orc", "경쟁 던전 오크", "Orc", 145f, 12f, 11f, 11f, 7f, 5f, CharacterCombatAbilityCatalog.CreateOrcCrush()),
                Enemy("rival-vampire", "경쟁 던전 뱀파이어", "Vampire", 112f, 11f, 8f, 7f, 12f, 7f, CharacterCombatAbilityCatalog.CreateVampireDrain())
            },
            _ => new List<EnemyTemplate>
            {
                Enemy("sealkeeper-a", "봉인 수호자 A", "Truth", 132f, 12f, 9f, 11f, 9f, 5f, SealBrand()),
                Enemy("sealkeeper-b", "봉인 수호자 B", "Truth", 132f, 12f, 9f, 11f, 9f, 5f, SealBrand()),
                Enemy("truth-warden", "진실의 감시자", "Truth", 210f, 15f, 13f, 13f, 11f, 6f, TruthRend())
            }
        };

        int enemyCount = routeNode == null || routeNode.IsBoss
            ? templates.Count
            : Mathf.Min(templates.Count, routeNode.Depth <= 1 ? 1 : 2);
        float encounterScale = routeNode == null || routeNode.IsBoss
            ? 1f
            : Mathf.Clamp(routeNode.DangerMultiplier, 0.65f, 1.1f);
        return templates.Take(enemyCount).Select((template, index) => new OffenseBattleCombatant(
            $"enemy:{target?.id ?? "unknown"}:{template.Id}:{index}",
            template.Name,
            template.Species,
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(
                template.Health * multipliers.EnemyHealth * encounterScale,
                template.Attack * multipliers.EnemyAttack * encounterScale,
                template.Strength * multipliers.EnemyAttack * encounterScale,
                template.Toughness * encounterScale,
                template.Dexterity * multipliers.EnemyInitiative * encounterScale,
                template.MoveSpeed * multipliers.EnemyInitiative * encounterScale),
            template.Health * multipliers.EnemyHealth * encounterScale,
            template.Abilities,
            formation: (OffenseFormationSlot)Mathf.Clamp(index, 0, 2))).ToArray();
    }

    private static EnemyTemplate Enemy(
        string id,
        string name,
        string species,
        float health,
        float attack,
        float strength,
        float toughness,
        float dexterity,
        float moveSpeed,
        params CharacterCombatAbilityDefinition[] abilities)
    {
        return new EnemyTemplate(
            id,
            name,
            species,
            health,
            attack,
            strength,
            toughness,
            dexterity,
            moveSpeed,
            abilities);
    }

    private static CharacterCombatAbilityDefinition AimedShot()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.aimed-shot",
            "조준 사격",
            "강한 피해를 주고 행동을 늦춥니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(1.25f),
            new OffenseDelayEffect(2f));
    }

    private static CharacterCombatAbilityDefinition CaptainBreak()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.captain-break",
            "갑옷 파괴",
            "피해와 취약을 적용합니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(1.3f),
            new OffenseVulnerabilityEffect(0.2f));
    }

    private static CharacterCombatAbilityDefinition ArcaneBurn()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.arcane-burn",
            "비전 화상",
            "지속 피해를 남깁니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(0.8f),
            new OffenseDamageOverTimeEffect(5f, 2));
    }

    private static CharacterCombatAbilityDefinition ManaRend()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.mana-rend",
            "마력 절단",
            "큰 피해와 행동 지연을 줍니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(1.45f),
            new OffenseDelayEffect(3f));
    }

    private static CharacterCombatAbilityDefinition SealBrand()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.seal-brand",
            "봉인의 낙인",
            "취약과 지속 피해를 남깁니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(0.9f),
            new OffenseVulnerabilityEffect(0.2f),
            new OffenseDamageOverTimeEffect(6f, 2));
    }

    private static CharacterCombatAbilityDefinition TruthRend()
    {
        return new CharacterCombatAbilityDefinition(
            "enemy.truth-rend",
            "진실 절단",
            "강한 피해를 주고 방어를 무너뜨립니다.",
            2,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(1.55f),
            new OffenseVulnerabilityEffect(0.3f));
    }

    private sealed class EnemyTemplate
    {
        public EnemyTemplate(
            string id,
            string name,
            string species,
            float health,
            float attack,
            float strength,
            float toughness,
            float dexterity,
            float moveSpeed,
            IEnumerable<CharacterCombatAbilityDefinition> abilities)
        {
            Id = id;
            Name = name;
            Species = species;
            Health = health;
            Attack = attack;
            Strength = strength;
            Toughness = toughness;
            Dexterity = dexterity;
            MoveSpeed = moveSpeed;
            Abilities = abilities?.Where(ability => ability != null).ToArray()
                ?? Array.Empty<CharacterCombatAbilityDefinition>();
        }

        public string Id { get; }
        public string Name { get; }
        public string Species { get; }
        public float Health { get; }
        public float Attack { get; }
        public float Strength { get; }
        public float Toughness { get; }
        public float Dexterity { get; }
        public float MoveSpeed { get; }
        public IReadOnlyList<CharacterCombatAbilityDefinition> Abilities { get; }
    }
}
