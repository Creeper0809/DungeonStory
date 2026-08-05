using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseBattleTeam
{
    Allies,
    Enemies
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseBattleOutcome
{
    InProgress,
    Victory,
    Defeat,
    Retreated,
    AbortedOwnerDeath
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseBattleStatusType
{
    Guard,
    Vulnerability,
    DamageOverTime,
    AttackModifier
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

    public void Refresh(float value, int turns)
    {
        Value = Mathf.Max(Value, Mathf.Max(0f, value));
        RemainingTurns = Mathf.Max(RemainingTurns, Mathf.Max(1, turns));
    }

    public bool ConsumeTurn()
    {
        RemainingTurns = Mathf.Max(0, RemainingTurns - 1);
        return RemainingTurns <= 0;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

    public void SetCooldown(string abilityId, int turns)
    {
        if (!string.IsNullOrWhiteSpace(abilityId))
        {
            cooldowns[abilityId] = Mathf.Max(0, turns);
        }
    }

    public void AdjustCooldowns(int delta)
    {
        foreach (string id in cooldowns.Keys.ToArray())
        {
            cooldowns[id] = delta <= -99
                ? 0
                : Mathf.Max(0, cooldowns[id] + delta);
        }
    }

    public IReadOnlyDictionary<string, int> GetCooldownSnapshot()
    {
        return new Dictionary<string, int>(cooldowns, StringComparer.Ordinal);
    }

    public void RestoreCooldowns(IEnumerable<KeyValuePair<string, int>> values)
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

    public void BeginTurn()
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

    public float ApplyCombatInjury(CombatAttackResult result)
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

    public void RestoreCombatState(
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

    public void RestoreTurnsStarted(int turns)
    {
        TurnsStarted = Mathf.Max(0, turns);
    }

    public float ApplyRawDamage(float amount)
    {
        float before = CurrentHealth;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - Mathf.Max(0f, amount));
        float applied = before - CurrentHealth;
        TotalDamageTaken += applied;
        return applied;
    }

    public float Heal(float amount)
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

    public void RestoreHealth(float currentHealth, float totalDamageTaken)
    {
        CurrentHealth = Mathf.Clamp(currentHealth, 0f, Stats.MaxHealth);
        TotalDamageTaken = Mathf.Max(0f, totalDamageTaken);
        UpdateDowned();
    }

    public void RestoreStats(OffenseBattleStats stats)
    {
        Stats = stats ?? throw new ArgumentNullException(nameof(stats));
    }

    public void AddStatus(OffenseBattleStatus status)
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

    public void RestoreStatuses(IEnumerable<OffenseBattleStatus> values)
    {
        statuses.Clear();
        statuses.AddRange(values?.Where(value => value != null) ?? Array.Empty<OffenseBattleStatus>());
    }

    public void RemoveStatus(OffenseBattleStatus status)
    {
        statuses.Remove(status);
    }

    public int RemoveStatuses(Func<OffenseBattleStatus, bool> predicate, int maximum)
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

    public void SetFormation(OffenseFormationSlot formation)
    {
        Formation = formation;
    }

    public void AddInitiativePenalty(float amount)
    {
        InitiativePenalty = Mathf.Max(0f, InitiativePenalty + Mathf.Max(0f, amount));
    }

    public void RestoreInitiativePenalty(float value)
    {
        InitiativePenalty = Mathf.Max(0f, value);
    }

    public void RestoreFormation(OffenseFormationSlot formation)
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseThrownEquipmentPersistenceState
{
    public string instanceId = string.Empty;
    public string ownerCharacterId = string.Empty;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseBattleCooldownPersistenceState
{
    public string abilityId = string.Empty;
    public int remainingTurns;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OffenseBattleStatusPersistenceState
{
    public string id = string.Empty;
    public OffenseBattleStatusType type;
    public float value;
    public int remainingTurns;
    public string sourceId = string.Empty;
}

