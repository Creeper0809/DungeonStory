using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

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
