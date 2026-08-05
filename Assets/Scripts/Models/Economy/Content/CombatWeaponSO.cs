using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(menuName = "DungeonStory/Combat/Weapon", order = 10)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatWeaponSO : CombatEquipmentDefinitionSO
{
    [SerializeReference] private List<CombatAttackVerb> verbs = new List<CombatAttackVerb>();
    [SerializeField] private List<CombatRangeProfile> rangeProfiles = new List<CombatRangeProfile>();
    [Min(1), SerializeField] private int maximumRange = 1;
    [SerializeField] private List<string> compatibleAmmunitionItemIds = new List<string>();
    [Min(0), SerializeField] private int magazineCapacity;
    [Min(0f), SerializeField] private float reloadSeconds = 1f;
    [SerializeField] private bool supportsAimed = true;
    [SerializeField] private bool supportsRapid;
    [SerializeField] private bool supportsSuppressive;
    [SerializeField] private bool gunpowderWeapon;
    [Range(0f, 1f), SerializeField] private float maximumMisfireChance = 0.18f;
    [Min(0f), SerializeField] private float smokeExposure = 8f;

    public override CombatEquipmentKind Kind =>
        verbs?.FirstOrDefault(verb => verb != null)?.Kind ?? CombatEquipmentKind.MeleeWeapon;
    public IReadOnlyList<CombatAttackVerb> Verbs => verbs ??= new List<CombatAttackVerb>();
    public IReadOnlyList<CombatRangeProfile> RangeProfiles => rangeProfiles ??= new List<CombatRangeProfile>();
    public int MaximumRange => Mathf.Max(1, maximumRange);
    public IReadOnlyList<ItemDefinitionId> CompatibleAmmunitionItemIds =>
        CombatAmmunitionPolicy.Normalize(compatibleAmmunitionItemIds);
    public string AmmunitionItemId =>
        CombatAmmunitionPolicy.GetPreferred(CompatibleAmmunitionItemIds).Value;
    public int MagazineCapacity => Mathf.Max(0, magazineCapacity);
    public float ReloadSeconds => Mathf.Max(0f, reloadSeconds);
    public bool SupportsAimed => supportsAimed;
    public bool SupportsRapid => supportsRapid;
    public bool SupportsSuppressive => supportsSuppressive;
    public bool GunpowderWeapon => gunpowderWeapon;
    public float MaximumMisfireChance => Mathf.Clamp01(maximumMisfireChance);
    public float SmokeExposure => Mathf.Max(0f, smokeExposure);

    public CombatWeaponSnapshot CreateSnapshot(
        CombatEquipmentInstance instance,
        int verbIndex = 0,
        CraftMaterialDefinitionSO material = null,
        float evolutionDamageMultiplier = 1f,
        float evolutionPenetrationMultiplier = 1f,
        float evolutionAccuracyMultiplier = 1f,
        float evolutionReloadMultiplier = 1f)
    {
        CombatAttackVerb verb = Verbs.Count > 0
            ? Verbs[Mathf.Clamp(verbIndex, 0, Verbs.Count - 1)]
            : new MeleeStrikeVerb();
        return new CombatWeaponSnapshot(
            EquipmentId,
            instance?.instanceId,
            verb.Kind,
            verb,
            RangeProfiles,
            MaximumRange,
            instance?.quality ?? CombatEquipmentQuality.Normal,
            AmmunitionItemId,
            MagazineCapacity,
            instance?.loadedAmmo ?? 0,
            ReloadSeconds * Mathf.Max(0.05f, evolutionReloadMultiplier),
            SupportsAimed,
            SupportsRapid,
            SupportsSuppressive,
            (material?.DamageMultiplier ?? 1f)
                * BaseStatMultiplier
                * Mathf.Max(0.05f, evolutionDamageMultiplier),
            (material?.PenetrationDefenseMultiplier ?? 1f)
                * BaseStatMultiplier
                * Mathf.Max(0.05f, evolutionPenetrationMultiplier),
            evolutionAccuracyMultiplier,
            gunpowderWeapon,
            instance?.durabilityRatio ?? 1f,
            MaximumMisfireChance,
            SmokeExposure);
    }
}
