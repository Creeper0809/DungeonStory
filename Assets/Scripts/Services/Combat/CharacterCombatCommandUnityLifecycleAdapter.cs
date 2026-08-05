using System;
using System.Linq;
using VContainer.Unity;

public sealed class CharacterCombatCommandUnityLifecycleAdapter :
    IInitializable,
    ITickable,
    IDisposable
{
    private readonly CharacterCombatCommandRuntime runtime;
    private readonly CharacterCombatCommandWorldServices world;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private IDisposable recoveredSubscription;

    public CharacterCombatCommandUnityLifecycleAdapter(
        CharacterCombatCommandRuntime runtime,
        CharacterCombatCommandWorldServices world,
        CharacterCombatCommandCombatServices combat)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        bodyHealth = (combat ?? throw new ArgumentNullException(nameof(combat)))
            .BodyHealth;
    }

    public void Initialize()
    {
        recoveredSubscription = world.GameEventBus.Subscribe<
            CharacterBodyHealthRecoveredEvent>(OnCharacterRecovered);
    }

    public void Tick() => runtime.TickFrame();

    public void Dispose()
    {
        recoveredSubscription?.Dispose();
        recoveredSubscription = null;
        foreach (CharacterActor actor in world.WorldRegistry.Characters.ToArray())
        {
            if (actor != null && runtime.IsInCombatStance(actor))
            {
                runtime.ReleaseCombatStanceForLifecycle(actor);
            }
        }

        runtime.ClearLifecycleState();
    }

    private void OnCharacterRecovered(CharacterBodyHealthRecoveredEvent gameEvent)
    {
        CharacterActor patient = gameEvent.Actor;
        if (patient == null || bodyHealth.GetSnapshot(patient).Downed)
        {
            return;
        }

        runtime.CompleteRecoveredRescues(
            CharacterPersistentIdentity.Require(patient).Value);
    }
}
