using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Combat/Weapon", order = 10)]
public sealed class CombatWeaponSO : CombatEquipmentDefinitionSO
{
    [SerializeReference] private List<CombatAttackVerb> verbs = new List<CombatAttackVerb>();
    [SerializeField] private List<CombatRangeProfile> rangeProfiles = new List<CombatRangeProfile>();
    [Min(1), SerializeField] private int maximumRange = 1;
    [SerializeField] private string ammunitionItemId = string.Empty;
    [Min(0), SerializeField] private int magazineCapacity;
    [Min(0f), SerializeField] private float reloadSeconds = 1f;
    [SerializeField] private bool supportsAimed = true;
    [SerializeField] private bool supportsRapid;
    [SerializeField] private bool supportsSuppressive;

    public override CombatEquipmentKind Kind =>
        verbs?.FirstOrDefault(verb => verb != null)?.Kind ?? CombatEquipmentKind.MeleeWeapon;
    public IReadOnlyList<CombatAttackVerb> Verbs => verbs ??= new List<CombatAttackVerb>();
    public IReadOnlyList<CombatRangeProfile> RangeProfiles => rangeProfiles ??= new List<CombatRangeProfile>();
    public int MaximumRange => Mathf.Max(1, maximumRange);
    public string AmmunitionItemId => ammunitionItemId?.Trim() ?? string.Empty;
    public int MagazineCapacity => Mathf.Max(0, magazineCapacity);
    public float ReloadSeconds => Mathf.Max(0f, reloadSeconds);
    public bool SupportsAimed => supportsAimed;
    public bool SupportsRapid => supportsRapid;
    public bool SupportsSuppressive => supportsSuppressive;

    public CombatWeaponSnapshot CreateSnapshot(CombatEquipmentInstance instance, int verbIndex = 0)
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
            ReloadSeconds,
            SupportsAimed,
            SupportsRapid,
            SupportsSuppressive);
    }
}
