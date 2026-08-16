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
    SetFireMode,
    DeployCover,
    Advance
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

public sealed class OffenseBattleEncounterRules
{
    private readonly HashSet<string> authoredCounterTags;
    private readonly HashSet<string> availableCounterTags =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> matchedCounterTags =
        new HashSet<string>(StringComparer.Ordinal);

    public OffenseBattleEncounterRules(
        OffenseEncounterObjective objective,
        int roundLimit,
        string objectiveTargetId,
        string objectiveCombatantId,
        IEnumerable<BattlefieldModifierDefinitionSO> modifiers,
        IEnumerable<string> counterTags = null,
        IEnumerable<string> rewardItemIds = null)
    {
        Objective = objective;
        RoundLimit = objective == OffenseEncounterObjective.DefeatAll
            ? 0
            : Mathf.Max(1, roundLimit);
        ObjectiveTargetId = objectiveTargetId?.Trim() ?? string.Empty;
        ObjectiveCombatantId = objectiveCombatantId?.Trim() ?? string.Empty;
        Modifiers = (modifiers ?? Array.Empty<BattlefieldModifierDefinitionSO>())
            .Where(value => value != null)
            .GroupBy(value => value.stableId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        authoredCounterTags = new HashSet<string>(
            (counterTags ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()),
            StringComparer.Ordinal);
        RewardItemIds = (rewardItemIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        MovementMultiplier = Mathf.Clamp(
            Modifiers.Aggregate(1f, (value, modifier) => value * modifier.movementMultiplier),
            0.25f,
            2f);
        AccuracyMultiplier = Mathf.Clamp(
            Modifiers.Aggregate(1f, (value, modifier) => value * modifier.accuracyMultiplier),
            0.25f,
            2f);
        DamageMultiplier = Mathf.Clamp(
            Modifiers.Aggregate(1f, (value, modifier) => value * modifier.damageMultiplier),
            0.25f,
            2f);
    }

    public OffenseEncounterObjective Objective { get; }
    public int RoundLimit { get; }
    public string ObjectiveTargetId { get; }
    public string ObjectiveCombatantId { get; private set; }
    public IReadOnlyList<BattlefieldModifierDefinitionSO> Modifiers { get; }
    public float MovementMultiplier { get; }
    public float AccuracyMultiplier { get; }
    public float DamageMultiplier { get; }
    public IReadOnlyCollection<string> AvailableCounterTags => availableCounterTags;
    public IReadOnlyCollection<string> MatchedCounterTags => matchedCounterTags;
    public IReadOnlyList<string> RewardItemIds { get; }

    public void EvaluatePartyCounters(IEnumerable<OffenseBattleCombatant> combatants)
    {
        availableCounterTags.Clear();
        matchedCounterTags.Clear();

        OffenseBattleCombatant[] allies = (combatants
                ?? Array.Empty<OffenseBattleCombatant>())
            .Where(value => value != null
                && value.Team == OffenseBattleTeam.Allies)
            .ToArray();
        foreach (string tag in OffenseBattleCounterRules.Project(allies))
        {
            availableCounterTags.Add(tag);
        }

        foreach (string tag in authoredCounterTags)
        {
            if (availableCounterTags.Contains(tag))
            {
                matchedCounterTags.Add(tag);
            }
        }
        foreach (BattlefieldModifierDefinitionSO modifier in Modifiers)
        {
            string required = modifier.requiredCounterTag?.Trim() ?? string.Empty;
            if (required.Length > 0 && availableCounterTags.Contains(required))
            {
                matchedCounterTags.Add(required);
            }
        }
    }

    public float GetMovementMultiplier(OffenseBattleTeam team) =>
        GetTeamMultiplier(team, modifier => modifier.movementMultiplier);

    public float GetAccuracyMultiplier(OffenseBattleTeam team) =>
        GetTeamMultiplier(team, modifier => modifier.accuracyMultiplier);

    public float GetDamageMultiplier(OffenseBattleTeam team) =>
        GetTeamMultiplier(team, modifier => modifier.damageMultiplier);

    private float GetTeamMultiplier(
        OffenseBattleTeam team,
        Func<BattlefieldModifierDefinitionSO, float> selector)
    {
        float value = 1f;
        foreach (BattlefieldModifierDefinitionSO modifier in Modifiers)
        {
            bool countered = team == OffenseBattleTeam.Allies
                && !string.IsNullOrWhiteSpace(modifier.requiredCounterTag)
                && matchedCounterTags.Contains(modifier.requiredCounterTag.Trim());
            value *= countered ? 1f : selector(modifier);
        }

        if (team == OffenseBattleTeam.Allies && matchedCounterTags.Count > 0)
        {
            value *= 1f + Mathf.Min(0.15f, matchedCounterTags.Count * 0.03f);
        }
        return Mathf.Clamp(value, 0.25f, 2f);
    }

    public void ResolveProtectedCombatant(IEnumerable<OffenseBattleCombatant> combatants)
    {
        if (Objective != OffenseEncounterObjective.ProtectTarget
            || !string.IsNullOrWhiteSpace(ObjectiveCombatantId))
        {
            return;
        }

        ObjectiveCombatantId = (combatants ?? Array.Empty<OffenseBattleCombatant>())
            .Where(value => value != null && value.Team == OffenseBattleTeam.Allies)
            .OrderBy(value => value.PersistentId, StringComparer.Ordinal)
            .Select(value => value.PersistentId)
            .FirstOrDefault() ?? string.Empty;
    }
}

public static class OffenseBattleCounterRules
{
    public static IReadOnlyCollection<string> Project(
        IEnumerable<OffenseBattleCombatant> combatants)
    {
        OffenseBattleCombatant[] party = (combatants
                ?? Array.Empty<OffenseBattleCombatant>())
            .Where(value => value != null)
            .ToArray();
        HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);
        bool melee = false;
        bool ranged = false;
        bool front = false;
        bool rear = false;

        foreach (OffenseBattleCombatant member in party)
        {
            CombatWeaponSnapshot weapon = member.Weapon
                ?? CombatWeaponSnapshot.CreateUnarmed();
            string weaponId = weapon.DefinitionId ?? string.Empty;
            string ammunitionId = weapon.AmmunitionItemId ?? string.Empty;
            string shieldId = member.Shield.DefinitionId ?? string.Empty;
            CombatEquipmentRoleFlags armorFlags = member.Armor.Aggregate(
                CombatEquipmentRoleFlags.None,
                (value, armor) => value | armor.RoleFlags);
            CombatEquipmentRoleFlags allFlags = weapon.RoleFlags
                | member.Shield.RoleFlags
                | armorFlags;

            melee |= !weapon.IsRanged;
            ranged |= weapon.IsRanged;
            front |= member.Formation == OffenseFormationSlot.Front;
            rear |= member.Formation == OffenseFormationSlot.Rear;

            if (weapon.IsRanged)
            {
                Add(tags, "counter:ranged", "counter:anti-air", "counter:kite");
                if (weapon.MaximumRange >= 6 || weapon.SupportsAimed)
                {
                    Add(tags, "counter:precision", "counter:focus-support");
                }
                if (weapon.SupportsSuppressive)
                {
                    Add(tags, "counter:pin-leader", "counter:split-pressure");
                }
            }
            if (weaponId.Contains("spear", StringComparison.Ordinal)
                || weaponId.Contains("halberd", StringComparison.Ordinal)
                || weaponId.Contains("poleaxe", StringComparison.Ordinal)
                || weaponId.Contains("lance", StringComparison.Ordinal))
            {
                Add(tags, "counter:reach", "counter:brace");
            }
            if ((allFlags & CombatEquipmentRoleFlags.ArmorBreaker) != 0)
            {
                Add(tags,
                    "counter:armor-break",
                    "counter:destroy-cover",
                    "counter:physical-burst",
                    "counter:sabotage");
            }
            if (member.Shield.IsValid)
            {
                Add(tags,
                    "counter:cover",
                    "counter:brace",
                    "counter:guard-backline",
                    "counter:slow-advance");
            }
            if ((allFlags & CombatEquipmentRoleFlags.SpellBlock) != 0
                || ammunitionId.Contains("mana-disruptor", StringComparison.Ordinal))
            {
                Add(tags,
                    "counter:mana-disrupt",
                    "counter:mana-grounding",
                    "counter:dispel");
            }
            if ((allFlags & CombatEquipmentRoleFlags.BlastAndSmokeProtection) != 0
                || member.Armor.Any(value =>
                    value.DefinitionId.Contains("smoke-hood", StringComparison.Ordinal)
                    || value.DefinitionId.Contains("blast-coat", StringComparison.Ordinal)))
            {
                Add(tags, "counter:air-filter", "counter:smoke-hood", "counter:insulated");
            }
            if (ammunitionId.Contains("smoke", StringComparison.Ordinal))
            {
                tags.Add("counter:smoke");
            }
            if (ammunitionId.Contains("tranquilizer", StringComparison.Ordinal))
            {
                tags.Add("counter:nonlethal");
            }
            if (weapon.GunpowderWeapon)
            {
                tags.Add("counter:powder");
            }
            if (weaponId.Contains("rune", StringComparison.Ordinal)
                || weaponId.Contains("mana", StringComparison.Ordinal))
            {
                tags.Add("counter:arcane");
            }
            if ((allFlags & CombatEquipmentRoleFlags.Powered) != 0)
            {
                Add(tags, "counter:engineering", "counter:mobile");
            }
            if (shieldId.Contains("pavise", StringComparison.Ordinal))
            {
                Add(tags, "counter:cover", "counter:slow-advance");
            }
            if (member.Abilities.Any(value => value.Id.Contains("heal", StringComparison.Ordinal)
                || value.Id.Contains("dressing", StringComparison.Ordinal)))
            {
                tags.Add("counter:interrupt-heal");
            }
        }

        if (melee && ranged) tags.Add("counter:mixed-tactics");
        if (front && rear) tags.Add("counter:flank");
        if (party.Length > 0 && party.All(value =>
            !(value.Weapon?.DefinitionId?.Contains("rune", StringComparison.Ordinal) ?? false)
            && !(value.Weapon?.DefinitionId?.Contains("mana", StringComparison.Ordinal) ?? false)))
        {
            tags.Add("counter:mundane");
        }

        return tags.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static void Add(HashSet<string> tags, params string[] values)
    {
        foreach (string value in values) tags.Add(value);
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum OffenseBattleStatusType
{
    Guard,
    Vulnerability,
    DamageOverTime,
    AttackModifier,
    Sedated,
    ManaBlocked,
    SignalSupport,
    SmokeObscured,
    SummonedGuard
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
        Value = type == OffenseBattleStatusType.AttackModifier
            ? Mathf.Clamp(value, -0.9f, 2f)
            : Mathf.Max(0f, value);
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
        Value = Type == OffenseBattleStatusType.AttackModifier
            ? Mathf.Clamp(
                Mathf.Abs(value) > Mathf.Abs(Value) ? value : Value,
                -0.9f,
                2f)
            : Type == OffenseBattleStatusType.Sedated
                ? Mathf.Clamp01(Value + Mathf.Max(0f, value))
            : Mathf.Max(Value, Mathf.Max(0f, value));
        RemainingTurns = Mathf.Max(RemainingTurns, Mathf.Max(1, turns));
    }

    public float Absorb(float incomingDamage)
    {
        if (Type != OffenseBattleStatusType.SummonedGuard
            || incomingDamage <= 0f
            || Value <= 0f)
        {
            return 0f;
        }

        float absorbed = Mathf.Min(Value, incomingDamage);
        Value = Mathf.Max(0f, Value - absorbed);
        return absorbed;
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
        OffenseFormationSlot formation = OffenseFormationSlot.Front,
        bool participatesInInitiative = true)
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
        ParticipatesInInitiative = participatesInInitiative;
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
    public bool ParticipatesInInitiative { get; }
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
    public CombatShieldSnapshot Shield { get; private set; }
    public float Suppression { get; private set; }
    public float BloodLoss { get; private set; }
    public float Consciousness => CalculateConsciousness();
    public float Manipulation => CalculateLimbAverage(CombatBodyPart.LeftArm, CombatBodyPart.RightArm);
    public float Mobility => CalculateLimbAverage(CombatBodyPart.LeftLeg, CombatBodyPart.RightLeg);
    public bool PinnedThisTurn { get; private set; }
    public CombatBodyPart LastHitBodyPart { get; private set; } = CombatBodyPart.Torso;
    public float CoverBlockChance { get; private set; }
    public CombatFireMode FireMode { get; private set; } = CombatFireMode.Aimed;
    public float ArcanePowerMultiplier { get; private set; } = 1f;

    public void SetCombatEquipment(
        CombatWeaponSnapshot weapon,
        IReadOnlyList<CombatArmorSnapshot> armor,
        CombatShieldSnapshot shield = default)
    {
        Weapon = weapon ?? CombatWeaponSnapshot.CreateUnarmed();
        this.armor = armor ?? Array.Empty<CombatArmorSnapshot>();
        Shield = shield;
    }

    public void SetCover(float blockChance)
    {
        CoverBlockChance = Mathf.Clamp01(blockChance);
    }

    public void SetFireMode(CombatFireMode mode)
    {
        FireMode = mode;
    }

    /// <summary>
    /// Captures the arcane projection owned by this detached battle
    /// combatant. Enemy combatants default to a healthy neutral projection;
    /// active-world allies replace it with their live performance snapshot.
    /// </summary>
    public void SetArcanePowerMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier)
            || multiplier < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }
        ArcanePowerMultiplier = multiplier;
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
        float appliedDamage = result.Nonlethal
            ? Mathf.Min(
                result.AppliedDamage,
                Mathf.Max(0f, CurrentHealth - 1f),
                Mathf.Max(0f, part.currentHealth - 1f))
            : result.AppliedDamage;
        part.currentHealth = Mathf.Max(0f, part.currentHealth - appliedDamage);
        part.bleedingPerSecond = Mathf.Max(
            0f,
            part.bleedingPerSecond + result.Bleeding * 0.01f);
        float applied = ApplyRawDamage(appliedDamage);
        if (!result.Nonlethal && IsVitalPartDestroyed())
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
            UpdateDowned();
            return;
        }

        statuses.Add(status);
        UpdateDowned();
    }

    public void RestoreStatuses(IEnumerable<OffenseBattleStatus> values)
    {
        statuses.Clear();
        statuses.AddRange(values?.Where(value => value != null) ?? Array.Empty<OffenseBattleStatus>());
    }

    public void RemoveStatus(OffenseBattleStatus status)
    {
        statuses.Remove(status);
        UpdateDowned();
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
        float sedation = statuses
            .Where(value => value.Type == OffenseBattleStatusType.Sedated)
            .Select(value => value.Value)
            .DefaultIfEmpty(0f)
            .Max();
        IsDowned = !IsDead
            && (Consciousness < 0.25f
                || Mobility < 0.2f
                || sedation >= 0.7f);
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
    public OffenseBattleCommandResult(
        bool accepted,
        string message,
        float amount = 0f,
        bool hit = false,
        bool shieldBlocked = false,
        bool coverBlocked = false)
    {
        Accepted = accepted;
        Message = message ?? string.Empty;
        Amount = Mathf.Max(0f, amount);
        Hit = hit;
        ShieldBlocked = shieldBlocked;
        CoverBlocked = coverBlocked;
    }

    public bool Accepted { get; }
    public string Message { get; }
    public float Amount { get; }
    public bool Hit { get; }
    public bool ShieldBlocked { get; }
    public bool CoverBlocked { get; }
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
    public string encounterId = string.Empty;
    public List<EnemyIndividualSaveData> enemyIndividuals =
        new List<EnemyIndividualSaveData>();
    public OffenseBattleOutcome outcome = OffenseBattleOutcome.InProgress;
    public int roundNumber = 1;
    public int currentOrderIndex;
    public long lastProcessedCommandId;
    public int preparedPlannedTurn;
    public int finalizedPlannedTurn;
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
    public bool participatesInInitiative = true;
    public float currentHealth;
    public float totalDamageTaken;
    public float initiativePenalty;
    public float coverBlockChance;
    public int turnsStarted;
    public OffenseFormationSlot formation;
    public float suppression;
    public float bloodLoss;
    public CombatBodyPart lastHitBodyPart = CombatBodyPart.Torso;
    public CombatFireMode fireMode = CombatFireMode.Aimed;
    public float arcanePowerMultiplier = 1f;
    public bool hasArcanePowerMultiplier;
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

