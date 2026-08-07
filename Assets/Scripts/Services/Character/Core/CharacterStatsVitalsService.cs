using System;
using DungeonStory.Foundation;

/// <summary>
/// Adapts CharacterStats' legacy vitality API to the authoritative body-health
/// aggregate and centralizes vitality side effects.
/// </summary>
public sealed class CharacterStatsVitalsService
{
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthCommand bodyHealthCommands;
    private readonly IGameEventBus gameEventBus;
    private readonly ICharacterDeathEventFactory deathEventFactory;
    private readonly IOwnerRunLifecycleService ownerRunLifecycle;

    public CharacterStatsVitalsService(
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICharacterBodyHealthCommand bodyHealthCommands,
        IGameEventBus gameEventBus,
        ICharacterDeathEventFactory deathEventFactory,
        IOwnerRunLifecycleService ownerRunLifecycle)
    {
        this.bodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        this.bodyHealthCommands = bodyHealthCommands
            ?? throw new ArgumentNullException(nameof(bodyHealthCommands));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.deathEventFactory = deathEventFactory
            ?? throw new ArgumentNullException(nameof(deathEventFactory));
        this.ownerRunLifecycle = ownerRunLifecycle
            ?? throw new ArgumentNullException(nameof(ownerRunLifecycle));
    }

    public void Configure(
        CharacterActor actor,
        float maximumHealth,
        bool resetCurrentHealth) =>
        bodyHealthCommands.ConfigureVitals(
            actor,
            maximumHealth,
            resetCurrentHealth);

    public CharacterVitalsSnapshot GetProjection(
        CharacterStats owner,
        CharacterActor actor,
        CharacterVitalsSnapshot localProjection)
    {
        if (actor == null || !CharacterPersistentIdentity.TryGet(actor, out _))
        {
            return localProjection;
        }

        CharacterVitalsSnapshot authoritative = bodyHealthQuery.GetVitals(actor);
        owner.ApplyVitalsProjection(authoritative);
        return authoritative;
    }

    public void ApplyDamage(
        CharacterActor actor,
        float amount,
        string reason,
        bool allowAggregateDeath) =>
        bodyHealthCommands.ApplyLegacyDamage(
            actor,
            amount,
            reason,
            allowAggregateDeath);

    public void NotifyDamage(
        CharacterStats owner,
        CharacterLog log,
        float amount,
        string reason,
        bool died,
        CharacterDeathCauseCode deathCause) =>
        CharacterVitalsSideEffectAdapter.NotifyDamage(
            owner,
            log,
            amount,
            reason,
            died,
            deathCause);

    public void Heal(CharacterActor actor, float amount) =>
        bodyHealthCommands.HealLegacyVitals(actor, amount);

    public void NotifyHealing(
        CharacterStats owner,
        CharacterLog log,
        float amount) =>
        CharacterVitalsSideEffectAdapter.NotifyHealing(owner, log, amount);

    public void ScaleMaximumHealth(CharacterActor actor, float multiplier) =>
        bodyHealthCommands.ScaleLegacyVitals(actor, multiplier);

    public void SetInjurySeverity(CharacterActor actor, float value) =>
        bodyHealthCommands.SetLegacyInjurySeverity(actor, value);

    public float NotifyInjurySeverity(CharacterLog log, float value) =>
        CharacterVitalsSideEffectAdapter.NotifyInjurySeverity(log, value);

    public void Kill(CharacterActor actor, string reason) =>
        bodyHealthCommands.Kill(actor, reason);

    public void Kill(
        CharacterActor actor,
        CharacterDeathCauseCode cause,
        string reasonCode) =>
        bodyHealthCommands.Kill(actor, cause, reasonCode);

    public void NotifyDeath(
        CharacterStats owner,
        CharacterActor actor,
        CharacterIdentity identity,
        CharacterVisual visual,
        CharacterLifecycle lifecycle,
        CharacterLog log,
        CharacterDeathCauseCode cause,
        string reasonCode) =>
        CharacterVitalsSideEffectAdapter.NotifyDeath(
            owner,
            actor,
            identity,
            visual,
            lifecycle,
            log,
            gameEventBus,
            deathEventFactory,
            ownerRunLifecycle,
            cause,
            reasonCode);

    public void RestoreProjection(
        CharacterActor actor,
        float maximumHealth,
        float currentHealth,
        float injurySeverity) =>
        bodyHealthCommands.RestoreLegacyVitalsProjection(
            actor,
            maximumHealth,
            currentHealth,
            injurySeverity);
}
