using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class OffenseThreatGameplayBridge : ITickable
{
    private const string MoodFactorId = "offense:urgent-site:mood";
    private const string RestFactorId = "offense:urgent-site:rest";
    private readonly IWorldThreatModifierQuery threatModifiers;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IGameClock gameClock;
    private float nextRefreshAt;

    public OffenseThreatGameplayBridge(
        IWorldThreatModifierQuery threatModifiers,
        ICharacterWorldQuery characterWorld,
        IGameClock gameClock)
    {
        this.threatModifiers = threatModifiers
            ?? throw new ArgumentNullException(nameof(threatModifiers));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public void Tick()
    {
        if (gameClock.IsPaused || gameClock.Time < nextRefreshAt)
        {
            return;
        }

        nextRefreshAt = gameClock.Time + 8f;
        float moodStrength = threatModifiers
            .GetModifier(OffenseThreatModifierKind.Mood)
            .EffectiveStrength;
        float restStrength = threatModifiers
            .GetModifier(OffenseThreatModifierKind.Rest)
            .EffectiveStrength;

        foreach (CharacterActor actor in characterWorld.Characters)
        {
            if (actor == null || actor.IsDead || actor.Stats == null)
            {
                continue;
            }

            ApplyOrClear(
                actor,
                MoodFactorId,
                "멀리서 불협한 성가가 들림",
                -12f * moodStrength,
                moodStrength);
            ApplyOrClear(
                actor,
                RestFactorId,
                "긴급 거점의 소음으로 쉬지 못함",
                -6f * restStrength,
                restStrength);
        }
    }

    private static void ApplyOrClear(
        CharacterActor actor,
        string id,
        string label,
        float value,
        float strength)
    {
        if (strength <= 0.001f)
        {
            actor.Stats.RemoveMoodFactor(id);
            return;
        }

        actor.ApplyMoodFactor(
            id,
            label,
            Mathf.Min(-0.5f, value),
            12f,
            1);
    }
}
