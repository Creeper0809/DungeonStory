using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

[Serializable]
public sealed class CharacterBodyPartHealthState
{
    public CombatBodyPart bodyPart;
    [Min(1f)] public float maxHealth = 20f;
    [Min(0f)] public float currentHealth = 20f;
    [Min(0f)] public float bleedingPerSecond;

    public float HealthRatio => currentHealth / Mathf.Max(1f, maxHealth);
}

[Serializable]
public sealed class CharacterBodyHealthState
{
    public string characterId = string.Empty;
    public List<CharacterBodyPartHealthState> parts = new List<CharacterBodyPartHealthState>();
    [Range(0f, 100f)] public float bloodLoss;
    [Range(0f, 100f)] public float suppression;
    public bool downed;
    public string lastDamageReason = string.Empty;
}

[Serializable]
public sealed class DungeonCharacterBodyHealthSaveData
{
    public List<CharacterBodyHealthState> characters = new List<CharacterBodyHealthState>();
}

public readonly struct CharacterBodyHealthSnapshot
{
    public CharacterBodyHealthSnapshot(
        IReadOnlyList<CharacterBodyPartHealthState> parts,
        float bloodLoss,
        float suppression,
        float consciousness,
        float manipulation,
        float mobility,
        bool downed)
    {
        Parts = parts ?? Array.Empty<CharacterBodyPartHealthState>();
        BloodLoss = Mathf.Clamp(bloodLoss, 0f, 100f);
        Suppression = Mathf.Clamp(suppression, 0f, 100f);
        Consciousness = Mathf.Clamp01(consciousness);
        Manipulation = Mathf.Clamp01(manipulation);
        Mobility = Mathf.Clamp01(mobility);
        Downed = downed;
    }

    public IReadOnlyList<CharacterBodyPartHealthState> Parts { get; }
    public float BloodLoss { get; }
    public float Suppression { get; }
    public float Consciousness { get; }
    public float Manipulation { get; }
    public float Mobility { get; }
    public bool Downed { get; }
}

public interface ICharacterBodyHealthRuntime
{
    CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor);
    CharacterBodyHealthSnapshot GetSnapshot(string characterId);
    void ApplyCombatResult(CharacterActor target, CombatAttackResult result, string reason);
    void ApplySnapshot(CharacterActor target, CharacterBodyHealthSnapshot snapshot, string reason);
    void AddSuppression(CharacterActor target, float amount);
    void Heal(CharacterActor target, float amount, bool stopBleeding);
    float GetTotalBleeding(CharacterActor target);
    float GetMissingPartHealth(CharacterActor target);
    bool Stabilize(CharacterActor target);
    bool ApplyTreatment(CharacterActor target, float partHealthAmount, float bloodLossReduction);
    DungeonCharacterBodyHealthSaveData Capture();
    void Restore(DungeonCharacterBodyHealthSaveData saveData);
}

public sealed class CharacterBodyHealthRuntime :
    ICharacterBodyHealthRuntime,
    ITickable
{
    public readonly struct CharacterDownedEvent
    {
        public CharacterDownedEvent(CharacterActor actor)
        {
            Actor = actor;
        }

        public CharacterActor Actor { get; }
    }

    public readonly struct CharacterRecoveredEvent
    {
        public CharacterRecoveredEvent(CharacterActor actor)
        {
            Actor = actor;
        }

        public CharacterActor Actor { get; }
    }

    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock gameClock;
    private readonly IGameEventBus gameEventBus;
    private readonly Dictionary<string, CharacterBodyHealthState> states =
        new Dictionary<string, CharacterBodyHealthState>(StringComparer.Ordinal);

    public CharacterBodyHealthRuntime(
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        IGameEventBus gameEventBus)
    {
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public void Tick()
    {
        float delta = gameClock.DeltaTime;
        if (delta <= 0f)
        {
            return;
        }

        foreach (CharacterActor actor in worldRegistry.Characters)
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            string id = GetId(actor);
            if (!states.TryGetValue(id, out CharacterBodyHealthState state))
            {
                continue;
            }

            float bleeding = state.parts.Sum(part => Mathf.Max(0f, part.bleedingPerSecond));
            if (bleeding > 0f)
            {
                state.bloodLoss = Mathf.Clamp(state.bloodLoss + bleeding * delta, 0f, 100f);
                actor.ApplyDamage(bleeding * 0.12f * delta, "출혈");
                if (state.bloodLoss >= 100f && !actor.IsDead)
                {
                    actor.Die("과다 출혈");
                }
            }

            state.suppression = Mathf.Max(0f, state.suppression - 5f * delta);
            bool wasDowned = state.downed;
            UpdateDowned(state);
            SyncLifecycle(actor, state, wasDowned);
        }
    }

    public CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor)
    {
        return actor == null
            ? EmptySnapshot()
            : BuildSnapshot(GetOrCreate(GetId(actor)));
    }

    public CharacterBodyHealthSnapshot GetSnapshot(string characterId)
    {
        return !string.IsNullOrWhiteSpace(characterId)
            && states.TryGetValue(characterId, out CharacterBodyHealthState state)
                ? BuildSnapshot(state)
                : EmptySnapshot();
    }

    public void ApplyCombatResult(CharacterActor target, CombatAttackResult result, string reason)
    {
        if (target == null || target.IsDead || !result.Executed)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(GetId(target));
        state.suppression = Mathf.Clamp(state.suppression + result.Suppression, 0f, 100f);
        if (!result.Hit || result.AppliedDamage <= 0f)
        {
            bool wasDowned = state.downed;
            UpdateDowned(state);
            SyncLifecycle(target, state, wasDowned);
            return;
        }

        CharacterBodyPartHealthState part = state.parts.First(item => item.bodyPart == result.BodyPart);
        part.currentHealth = Mathf.Max(0f, part.currentHealth - result.AppliedDamage);
        part.bleedingPerSecond += result.Bleeding * 0.01f;
        state.lastDamageReason = reason ?? string.Empty;
        target.ApplyDamage(result.AppliedDamage, reason);

        if (!target.IsDead
            && (result.BodyPart == CombatBodyPart.Head || result.BodyPart == CombatBodyPart.Torso)
            && part.currentHealth <= 0f)
        {
            target.Die(result.BodyPart == CombatBodyPart.Head ? "머리 치명상" : "몸통 치명상");
        }

        bool wasDownedAfterHit = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDownedAfterHit);
    }

    public void ApplySnapshot(
        CharacterActor target,
        CharacterBodyHealthSnapshot snapshot,
        string reason)
    {
        if (target == null || snapshot.Parts == null || snapshot.Parts.Count == 0)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(GetId(target));
        state.parts = snapshot.Parts.Select(ClonePart).ToList();
        EnsureParts(state);
        state.bloodLoss = Mathf.Clamp(snapshot.BloodLoss, 0f, 100f);
        state.suppression = Mathf.Clamp(snapshot.Suppression, 0f, 100f);
        state.lastDamageReason = reason ?? string.Empty;
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
    }

    public void AddSuppression(CharacterActor target, float amount)
    {
        if (target == null || amount <= 0f)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(GetId(target));
        state.suppression = Mathf.Clamp(state.suppression + amount, 0f, 100f);
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
    }

    public void Heal(CharacterActor target, float amount, bool stopBleeding)
    {
        if (target == null || amount <= 0f)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(GetId(target));
        float remaining = amount;
        foreach (CharacterBodyPartHealthState part in state.parts.OrderBy(part => part.HealthRatio))
        {
            float restored = Mathf.Min(remaining, part.maxHealth - part.currentHealth);
            part.currentHealth += restored;
            remaining -= restored;
            if (stopBleeding)
            {
                part.bleedingPerSecond = 0f;
            }

            if (remaining <= 0f)
            {
                break;
            }
        }

        state.bloodLoss = Mathf.Max(0f, state.bloodLoss - amount * 0.5f);
        target.Heal(amount);
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
    }

    public float GetTotalBleeding(CharacterActor target)
    {
        if (target == null)
        {
            return 0f;
        }

        CharacterBodyHealthState state = GetOrCreate(GetId(target));
        return state.parts.Sum(part => Mathf.Max(0f, part.bleedingPerSecond));
    }

    public float GetMissingPartHealth(CharacterActor target)
    {
        if (target == null)
        {
            return 0f;
        }

        CharacterBodyHealthState state = GetOrCreate(GetId(target));
        return state.parts.Sum(part => Mathf.Max(0f, part.maxHealth - part.currentHealth));
    }

    public bool Stabilize(CharacterActor target)
    {
        if (target == null || target.IsDead)
        {
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(GetId(target));
        bool changed = false;
        foreach (CharacterBodyPartHealthState part in state.parts)
        {
            if (part.bleedingPerSecond <= 0f)
            {
                continue;
            }

            part.bleedingPerSecond = 0f;
            changed = true;
        }

        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
        return changed;
    }

    public bool ApplyTreatment(
        CharacterActor target,
        float partHealthAmount,
        float bloodLossReduction)
    {
        if (target == null || target.IsDead)
        {
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(GetId(target));
        float remaining = Mathf.Max(0f, partHealthAmount);
        float restoredTotal = 0f;
        foreach (CharacterBodyPartHealthState part in state.parts.OrderBy(part => part.HealthRatio))
        {
            float restored = Mathf.Min(remaining, part.maxHealth - part.currentHealth);
            part.currentHealth += restored;
            remaining -= restored;
            restoredTotal += restored;
            if (remaining <= 0f)
            {
                break;
            }
        }

        float previousBloodLoss = state.bloodLoss;
        state.bloodLoss = Mathf.Max(0f, state.bloodLoss - Mathf.Max(0f, bloodLossReduction));
        if (restoredTotal > 0f)
        {
            target.Heal(restoredTotal);
        }

        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
        return restoredTotal > 0f || state.bloodLoss < previousBloodLoss;
    }

    public DungeonCharacterBodyHealthSaveData Capture()
    {
        return new DungeonCharacterBodyHealthSaveData
        {
            characters = states.Values.Select(CloneState).ToList()
        };
    }

    public void Restore(DungeonCharacterBodyHealthSaveData saveData)
    {
        states.Clear();
        foreach (CharacterBodyHealthState source in saveData?.characters ?? new List<CharacterBodyHealthState>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.characterId)
                || states.ContainsKey(source.characterId))
            {
                continue;
            }

            CharacterBodyHealthState restored = CloneState(source);
            EnsureParts(restored);
            UpdateDowned(restored);
            states.Add(restored.characterId, restored);
        }

        foreach (CharacterActor actor in worldRegistry.Characters)
        {
            string id = GetId(actor);
            if (states.TryGetValue(id, out CharacterBodyHealthState state))
            {
                SyncLifecycle(actor, state, wasDowned: !state.downed);
            }
        }
    }

    private CharacterBodyHealthState GetOrCreate(string characterId)
    {
        if (states.TryGetValue(characterId, out CharacterBodyHealthState state))
        {
            EnsureParts(state);
            return state;
        }

        state = new CharacterBodyHealthState
        {
            characterId = characterId
        };
        EnsureParts(state);
        states.Add(characterId, state);
        return state;
    }

    private static void EnsureParts(CharacterBodyHealthState state)
    {
        state.parts ??= new List<CharacterBodyPartHealthState>();
        EnsurePart(state, CombatBodyPart.Head, 18f);
        EnsurePart(state, CombatBodyPart.Torso, 45f);
        EnsurePart(state, CombatBodyPart.LeftArm, 22f);
        EnsurePart(state, CombatBodyPart.RightArm, 22f);
        EnsurePart(state, CombatBodyPart.LeftLeg, 26f);
        EnsurePart(state, CombatBodyPart.RightLeg, 26f);
    }

    private static void EnsurePart(CharacterBodyHealthState state, CombatBodyPart bodyPart, float maxHealth)
    {
        CharacterBodyPartHealthState part = state.parts.FirstOrDefault(item => item.bodyPart == bodyPart);
        if (part == null)
        {
            state.parts.Add(new CharacterBodyPartHealthState
            {
                bodyPart = bodyPart,
                maxHealth = maxHealth,
                currentHealth = maxHealth
            });
            return;
        }

        part.maxHealth = Mathf.Max(1f, part.maxHealth);
        part.currentHealth = Mathf.Clamp(part.currentHealth, 0f, part.maxHealth);
        part.bleedingPerSecond = Mathf.Max(0f, part.bleedingPerSecond);
    }

    private static CharacterBodyHealthSnapshot BuildSnapshot(CharacterBodyHealthState state)
    {
        CharacterBodyPartHealthState head = state.parts.First(part => part.bodyPart == CombatBodyPart.Head);
        CharacterBodyPartHealthState torso = state.parts.First(part => part.bodyPart == CombatBodyPart.Torso);
        CharacterBodyPartHealthState leftArm = state.parts.First(part => part.bodyPart == CombatBodyPart.LeftArm);
        CharacterBodyPartHealthState rightArm = state.parts.First(part => part.bodyPart == CombatBodyPart.RightArm);
        CharacterBodyPartHealthState leftLeg = state.parts.First(part => part.bodyPart == CombatBodyPart.LeftLeg);
        CharacterBodyPartHealthState rightLeg = state.parts.First(part => part.bodyPart == CombatBodyPart.RightLeg);
        float consciousness = Mathf.Min(head.HealthRatio, torso.HealthRatio)
            * Mathf.Lerp(1f, 0.2f, state.bloodLoss / 100f);
        float manipulation = (leftArm.HealthRatio + rightArm.HealthRatio) * 0.5f;
        float mobility = (leftLeg.HealthRatio + rightLeg.HealthRatio) * 0.5f;
        return new CharacterBodyHealthSnapshot(
            state.parts.Select(ClonePart).ToArray(),
            state.bloodLoss,
            state.suppression,
            consciousness,
            manipulation,
            mobility,
            state.downed);
    }

    private static void UpdateDowned(CharacterBodyHealthState state)
    {
        CharacterBodyHealthSnapshot snapshot = BuildSnapshot(state);
        if (state.downed)
        {
            state.downed = snapshot.Consciousness < 0.35f
                || snapshot.Mobility < 0.3f
                || snapshot.BloodLoss >= 70f;
            return;
        }

        state.downed = snapshot.Consciousness < 0.25f || snapshot.Mobility < 0.2f;
    }

    private void SyncLifecycle(
        CharacterActor actor,
        CharacterBodyHealthState state,
        bool wasDowned)
    {
        if (actor == null || actor.IsDead)
        {
            return;
        }

        if (state.downed)
        {
            if (!wasDowned || actor.CurrentLifecycleState != CharacterLifecycleState.Downed)
            {
                gameEventBus.Publish(new CharacterDownedEvent(actor));
                if (Application.isPlaying)
                {
                    DefenseCombatPresentation presentation =
                        DefenseCombatPresentation.Ensure(actor);
                    presentation?.SetDowned(true);
                    presentation?.SetStatus("쓰러짐", combatActive: true);
                }
            }

            return;
        }

        if (wasDowned || actor.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            gameEventBus.Publish(new CharacterRecoveredEvent(actor));
            if (Application.isPlaying)
            {
                DefenseCombatPresentation presentation =
                    DefenseCombatPresentation.Ensure(actor);
                presentation?.SetDowned(false);
                presentation?.SetStatus("회복 중", combatActive: false);
            }
        }
    }

    private static CharacterBodyHealthState CloneState(CharacterBodyHealthState source)
    {
        return new CharacterBodyHealthState
        {
            characterId = source.characterId ?? string.Empty,
            parts = source.parts?.Select(ClonePart).ToList() ?? new List<CharacterBodyPartHealthState>(),
            bloodLoss = Mathf.Clamp(source.bloodLoss, 0f, 100f),
            suppression = Mathf.Clamp(source.suppression, 0f, 100f),
            downed = source.downed,
            lastDamageReason = source.lastDamageReason ?? string.Empty
        };
    }

    private static CharacterBodyPartHealthState ClonePart(CharacterBodyPartHealthState source)
    {
        return new CharacterBodyPartHealthState
        {
            bodyPart = source.bodyPart,
            maxHealth = source.maxHealth,
            currentHealth = source.currentHealth,
            bleedingPerSecond = source.bleedingPerSecond
        };
    }

    private static CharacterBodyHealthSnapshot EmptySnapshot()
    {
        return new CharacterBodyHealthSnapshot(
            Array.Empty<CharacterBodyPartHealthState>(),
            0f,
            0f,
            1f,
            1f,
            1f,
            false);
    }

    private static string GetId(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId;
        return !string.IsNullOrWhiteSpace(id)
            ? id
            : $"scene-actor:{actor?.GetInstanceID() ?? 0}";
    }
}
