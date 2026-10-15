using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public interface ICombatCommandProjectilePresentation
{
    void Launch(Vector3 start, Vector3 end, CombatWeaponSnapshot weapon);
}

/// <summary>Presentation-only adapter; it never owns attack costs or outcomes.</summary>
public sealed class CombatCommandProjectilePresentation : ICombatCommandProjectilePresentation
{
    private readonly IGameClock clock;
    private readonly IWorldUiHierarchy hierarchy;

    public CombatCommandProjectilePresentation(IGameClock clock, IWorldUiHierarchy hierarchy)
    {
        this.clock = clock ?? throw new System.ArgumentNullException(nameof(clock));
        this.hierarchy = hierarchy ?? throw new System.ArgumentNullException(nameof(hierarchy));
    }

    public void Launch(Vector3 start, Vector3 end, CombatWeaponSnapshot weapon) =>
        CombatCommandProjectileLauncher.Launch(start, end, weapon, clock, hierarchy);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CombatCommandProjectileLauncher
{
    public static void Launch(
        Vector3 start,
        Vector3 end,
        CombatWeaponSnapshot weapon,
        IGameClock gameClock,
        IWorldUiHierarchy worldUiHierarchy)
    {
        float speed = weapon?.Verb switch
        {
            ProjectileVerb projectile => projectile.projectileSpeed,
            RecoverableThrowVerb recoverable => recoverable.projectileSpeed,
            _ => 12f
        };
        CombatProjectilePresentation.Launch(
            start,
            end,
            speed,
            weapon?.Verb?.damageType ?? CombatDamageType.Pierce,
            weapon?.Kind == CombatEquipmentKind.RecoverableThrowingWeapon,
            gameClock,
            worldUiHierarchy);
    }
}
