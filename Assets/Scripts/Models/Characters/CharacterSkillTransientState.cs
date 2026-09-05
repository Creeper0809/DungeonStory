using System;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public interface ICharacterSkillTransientStateRegistry
{
    bool TryEnter(CharacterId characterId, string key);
    void Exit(CharacterId characterId, string key);
    void BeginWork(
        CharacterId characterId,
        WorkTypeId workTypeId,
        float speedMultiplier);
    void EndWork(CharacterId characterId);
    float GetWorkSpeedMultiplier(CharacterId characterId);
    void Reset(CharacterId characterId);
    void ResetAll();
}

/// <summary>
/// Single live and definition-bound authority for temporary work-speed state.
/// Direct registry callers may not bypass the authored skill clamp with an
/// arbitrary, non-finite, or over-maximum multiplier.
/// </summary>
public static class CharacterSkillWorkSpeedAuthority
{
    public const string Schema = "character-skill-work-speed-authority@1";
    public const float MinimumRuntimeMultiplier = 0.1f;
    public const float NeutralMultiplier = 1f;
    public const float MaximumAuthoredBonus = 1.5f;
    public const float MaximumRuntimeMultiplier =
        NeutralMultiplier + MaximumAuthoredBonus;

    public static float ResolveFromAuthoredBonus(float bonus)
    {
        if (float.IsNaN(bonus) || float.IsInfinity(bonus))
            throw new InvalidOperationException(
                "Character skill work-speed bonus must be finite.");
        return NeutralMultiplier + Math.Clamp(
            bonus,
            0f,
            MaximumAuthoredBonus);
    }

    public static float RequireRuntimeMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier)
            || float.IsInfinity(multiplier)
            || multiplier < MinimumRuntimeMultiplier
            || multiplier > MaximumRuntimeMultiplier)
        {
            throw new InvalidOperationException(
                "Character skill work-speed multiplier is outside the "
                + "canonical runtime envelope.");
        }
        return multiplier;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[DisallowMultipleComponent]
public sealed class CharacterSkillTransientState : MonoBehaviour
{
    private ICharacterSkillTransientStateRegistry registry;
    private CharacterId characterId;

    public bool IsConfigured => registry != null && characterId.IsValid;

    public float WorkSpeedMultiplier => registry != null && characterId.IsValid
        ? registry.GetWorkSpeedMultiplier(characterId)
        : 1f;

    public static CharacterSkillTransientState Ensure(Component owner)
    {
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        return owner.GetComponent<CharacterSkillTransientState>()
            ?? owner.gameObject.AddComponent<CharacterSkillTransientState>();
    }

    public void Configure(
        ICharacterSkillTransientStateRegistry registry,
        CharacterId characterId)
    {
        this.registry = registry
            ?? throw new ArgumentNullException(nameof(registry));
        if (!characterId.IsValid)
        {
            throw new ArgumentException(
                "A persistent character ID is required.",
                nameof(characterId));
        }

        this.characterId = characterId;
    }

    public bool TryEnter(string key)
    {
        return RequireRegistry().TryEnter(characterId, key);
    }

    public void Exit(string key)
    {
        RequireRegistry().Exit(characterId, key);
    }

    public void BeginWork(WorkTypeId workTypeId, float speedMultiplier)
    {
        RequireRegistry().BeginWork(characterId, workTypeId, speedMultiplier);
    }

    public void EndWork()
    {
        RequireRegistry().EndWork(characterId);
    }

    public void Clear()
    {
        if (registry != null && characterId.IsValid)
        {
            registry.Reset(characterId);
        }
    }

    private ICharacterSkillTransientStateRegistry RequireRegistry() => registry
        ?? throw new InvalidOperationException(
            $"{nameof(CharacterSkillTransientState)} requires scoped registry configuration.");
}
