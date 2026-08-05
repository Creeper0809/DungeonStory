using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class CombatEquipmentUiStatBlock
{
    public static CombatEquipmentUiStatBlock Empty => new CombatEquipmentUiStatBlock();

    public int maxHealth;
    public int attack;
    public int strength;
    public int toughness;
    public int dexterity;
    public int moveSpeed;
}

/// <summary>
/// Owns the combat-tab projection and commands for the character summary view.
/// The MonoBehaviour only forwards the selected actor and generated view handles.
/// </summary>
public sealed class CharacterSummaryCombatPresenter
{
    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICombatEquipmentRuntime equipmentRuntime;
    private readonly ICombatEquipmentCatalog equipmentCatalog;
    private readonly ICombatEquipmentMaintenanceRuntime maintenanceRuntime;
    private readonly IGameEventBus eventBus;

    private TMP_Text summaryText;
    private Button loadoutButton;
    private Button weaponButton;
    private Button reloadButton;
    private Button fireModeButton;
    private Button holdFireButton;
    private Button repairButton;

    public CharacterSummaryCombatPresenter(
        ICharacterBodyHealthQuery bodyHealthQuery,
        ICombatEquipmentRuntime equipmentRuntime,
        ICombatEquipmentCatalog equipmentCatalog,
        ICombatEquipmentMaintenanceRuntime maintenanceRuntime,
        IGameEventBus eventBus)
    {
        this.bodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        this.equipmentRuntime = equipmentRuntime
            ?? throw new ArgumentNullException(nameof(equipmentRuntime));
        this.equipmentCatalog = equipmentCatalog
            ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        this.maintenanceRuntime = maintenanceRuntime
            ?? throw new ArgumentNullException(nameof(maintenanceRuntime));
        this.eventBus = eventBus
            ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public void Bind(
        TMP_Text generatedSummaryText,
        Button generatedLoadoutButton,
        Button generatedWeaponButton,
        Button generatedReloadButton,
        Button generatedFireModeButton,
        Button generatedHoldFireButton,
        Button generatedRepairButton)
    {
        summaryText = generatedSummaryText;
        loadoutButton = generatedLoadoutButton;
        weaponButton = generatedWeaponButton;
        reloadButton = generatedReloadButton;
        fireModeButton = generatedFireModeButton;
        holdFireButton = generatedHoldFireButton;
        repairButton = generatedRepairButton;
    }

    public void ToggleLoadout(CharacterActor actor)
    {
        if (!TryGetRuntime(actor, out string characterId))
        {
            return;
        }

        CharacterCombatLoadoutProfile current = equipmentRuntime.GetActiveProfileSnapshot(characterId);
        string target = string.Equals(
            current?.profileId,
            CombatLoadoutPresetIds.Peace,
            StringComparison.Ordinal)
            ? CombatLoadoutPresetIds.Combat
            : CombatLoadoutPresetIds.Peace;
        bool success = equipmentRuntime.TrySetActiveProfile(characterId, target);
        eventBus.ShowNotice(
            success
                ? target == CombatLoadoutPresetIds.Combat
                    ? CharacterSummaryCombatTextFormatter.Get(
                        "CharacterSummary.Combat.Notice.LoadoutCombat",
                        actor.name)
                    : CharacterSummaryCombatTextFormatter.Get(
                        "CharacterSummary.Combat.Notice.LoadoutPeace",
                        actor.name)
                : CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Notice.LoadoutUnavailable"),
            success ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
        Refresh(actor);
    }

    public void CycleWeapon(CharacterActor actor)
    {
        if (!TryGetRuntime(actor, out string characterId))
        {
            return;
        }

        CharacterCombatLoadoutProfile profile = equipmentRuntime.GetActiveProfileSnapshot(characterId);
        string[] weaponIds = profile?.weaponInstanceIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<string>();
        if (weaponIds.Length == 0)
        {
            eventBus.ShowNotice(
                CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Notice.NoCarriedWeapon"),
                NoticeFeedEvent.Grade.WARNING);
            return;
        }

        int currentIndex = Array.FindIndex(
            weaponIds,
            id => string.Equals(id, profile.activeWeaponInstanceId, StringComparison.Ordinal));
        for (int offset = 1; offset <= weaponIds.Length; offset++)
        {
            string candidate = weaponIds[(Mathf.Max(-1, currentIndex) + offset) % weaponIds.Length];
            if (equipmentRuntime.TrySetActiveWeapon(characterId, candidate, out string failureReason))
            {
                eventBus.ShowNotice(
                    CharacterSummaryCombatTextFormatter.Get(
                        "CharacterSummary.Combat.Notice.WeaponSwitched",
                        actor.name),
                    NoticeFeedEvent.Grade.NONE);
                Refresh(actor);
                return;
            }

            if (offset == weaponIds.Length)
            {
                eventBus.ShowNotice(
                    CharacterSummaryCombatTextFormatter.FailureReason(failureReason),
                    NoticeFeedEvent.Grade.WARNING);
            }
        }
    }

    public void Reload(CharacterActor actor)
    {
        if (!TryGetRuntime(actor, out string characterId)
            || !equipmentRuntime.TryGetActiveWeapon(characterId, out CombatWeaponSnapshot weapon)
            || weapon == null
            || string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            eventBus.ShowNotice(
                CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Notice.NoActiveWeaponToReload"),
                NoticeFeedEvent.Grade.WARNING);
            return;
        }

        bool success = equipmentRuntime.TryReloadFromCharacterInventory(
            characterId,
            weapon.InstanceId,
            out int consumed);
        eventBus.ShowNotice(
            success
                ? CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Notice.Reloaded",
                    actor.name,
                    consumed)
                : CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Notice.ReloadUnavailable"),
            success ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
        Refresh(actor);
    }

    public void CycleFireMode(CharacterActor actor)
    {
        if (!TryGetRuntime(actor, out string characterId))
        {
            return;
        }

        CharacterCombatLoadoutProfile profile = equipmentRuntime.GetActiveProfileSnapshot(characterId);
        CombatFireMode[] modes =
        {
            CombatFireMode.Aimed,
            CombatFireMode.Rapid,
            CombatFireMode.Suppressive
        };
        int currentIndex = Array.IndexOf(modes, profile?.fireMode ?? CombatFireMode.Aimed);
        string lastFailure = CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Notice.NoAvailableFireMode");
        for (int offset = 1; offset <= modes.Length; offset++)
        {
            CombatFireMode candidate = modes[(Mathf.Max(0, currentIndex) + offset) % modes.Length];
            if (equipmentRuntime.TrySetFireMode(characterId, candidate, out lastFailure))
            {
                eventBus.ShowNotice(
                    CharacterSummaryCombatTextFormatter.Get(
                        "CharacterSummary.Combat.Notice.FireModeSelected",
                        actor.name,
                        CharacterSummaryCombatTextFormatter.FireMode(candidate)),
                    NoticeFeedEvent.Grade.NONE);
                Refresh(actor);
                return;
            }
        }

        eventBus.ShowNotice(
            CharacterSummaryCombatTextFormatter.FailureReason(lastFailure),
            NoticeFeedEvent.Grade.WARNING);
    }

    public void ToggleHoldFire(CharacterActor actor)
    {
        if (!TryGetRuntime(actor, out string characterId))
        {
            return;
        }

        CharacterCombatLoadoutProfile profile = equipmentRuntime.GetActiveProfileSnapshot(characterId);
        bool holdFire = !(profile?.holdFire ?? false);
        if (equipmentRuntime.TrySetHoldFire(characterId, holdFire))
        {
            eventBus.ShowNotice(
                holdFire
                    ? CharacterSummaryCombatTextFormatter.Get(
                        "CharacterSummary.Combat.Notice.HoldFire",
                        actor.name)
                    : CharacterSummaryCombatTextFormatter.Get(
                        "CharacterSummary.Combat.Notice.AllowFire",
                        actor.name),
                NoticeFeedEvent.Grade.NONE);
        }

        Refresh(actor);
    }

    public void RequestRepair(CharacterActor actor)
    {
        if (!TryGetRuntime(actor, out string characterId))
        {
            eventBus.ShowNotice(
                CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Notice.RepairInfoUnavailable"),
                NoticeFeedEvent.Grade.WARNING);
            return;
        }

        CharacterCombatLoadoutProfile profile = equipmentRuntime.GetActiveProfileSnapshot(characterId);
        IEnumerable<string> candidateIds = (profile?.armorInstanceIds ?? new List<string>())
            .Concat(string.IsNullOrWhiteSpace(profile?.shieldInstanceId)
                ? Array.Empty<string>()
                : new[] { profile.shieldInstanceId });
        CombatEquipmentInstance candidate = candidateIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => equipmentRuntime.TryGetInstance(id, out CombatEquipmentInstance instance)
                ? instance
                : null)
            .Where(instance => instance != null && instance.durabilityRatio < 0.999f)
            .OrderBy(instance => instance.durabilityRatio)
            .FirstOrDefault();
        if (candidate == null)
        {
            eventBus.ShowNotice(
                CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Notice.NoRepairCandidate"),
                NoticeFeedEvent.Grade.WARNING);
            return;
        }

        bool requested = maintenanceRuntime.TryRequestManualRepair(
            candidate.instanceId,
            out string message);
        eventBus.ShowNotice(
            message,
            requested ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
        Refresh(actor);
    }

    public void Refresh(CharacterActor actor)
    {
        if (summaryText == null)
        {
            return;
        }

        if (actor == null)
        {
            summaryText.text = CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Empty");
            return;
        }

        string characterId = actor.Identity?.PersistentId ?? string.Empty;
        int melee = actor.GetCharacterStat(CharacterStatType.Attack);
        int shooting = actor.GetCharacterStat(CharacterStatType.Shooting);
        int evasion = actor.GetCharacterStat(CharacterStatType.Evasion);
        int move = actor.GetCharacterStat(CharacterStatType.MoveSpeed);
        int strength = actor.GetCharacterStat(CharacterStatType.Strength);
        int dexterity = actor.GetCharacterStat(CharacterStatType.Dexterity);

        float baseRangedHit = Mathf.Clamp(
            0.45f + shooting * 0.025f + dexterity * 0.01f,
            0.05f,
            0.95f);
        float baseMeleeHit = Mathf.Clamp(0.72f + (melee + dexterity) * 0.018f, 0.1f, 0.95f);
        float baseEvasion = Mathf.Clamp(0.02f + evasion * 0.01f + move * 0.003f, 0f, 0.35f);

        StringBuilder builder = new StringBuilder(1536);
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Ability.Title"));
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Ability.Stats",
            melee,
            shooting,
            evasion,
            dexterity,
            strength));
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Ability.RangedHit",
            baseRangedHit * 100f));
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Ability.MeleeHit",
            baseMeleeHit * 100f));
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Ability.Evasion",
            baseEvasion * 100f));

        CharacterBodyHealthSnapshot body = bodyHealthQuery.GetSnapshot(actor);
        builder.AppendLine();
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Body.Title"));
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Body.Functions",
            body.Consciousness * 100f,
            body.Manipulation * 100f,
            body.Mobility * 100f));
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Body.Damage",
            body.BloodLoss,
            body.Suppression,
            body.Downed
                ? CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Summary.Body.DownedSuffix")
                : string.Empty));
        foreach (CharacterBodyPartHealthState part in body.Parts ?? Array.Empty<CharacterBodyPartHealthState>())
        {
            string bleeding = part.bleedingPerSecond > 0f
                ? CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Summary.Body.BleedingSuffix",
                    part.bleedingPerSecond)
                : string.Empty;
            builder.AppendLine(
                $"- {CharacterSummaryCombatTextFormatter.BodyPart(part.bodyPart)} "
                + $"{part.currentHealth:0.#}/{part.maxHealth:0.#}{bleeding}");
        }

        CharacterCombatLoadoutProfile profile = equipmentRuntime.GetActiveProfileSnapshot(characterId);
        builder.AppendLine();
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Equipment.Title"));
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Equipment.Loadout",
            profile?.displayName
                ?? CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Common.Peace")));

        CombatWeaponSnapshot weapon = null;
        if (equipmentRuntime.TryGetActiveWeapon(characterId, out weapon)
            && weapon != null
            && !string.IsNullOrWhiteSpace(weapon.InstanceId)
            && equipmentCatalog.TryGet(weapon.DefinitionId, out CombatEquipmentDefinitionSO weaponDefinition))
        {
            string ammo = weapon.RequiresAmmo
                ? CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Summary.Equipment.LoadedSuffix",
                    weapon.LoadedAmmo,
                    weapon.MagazineCapacity)
                : string.Empty;
            bool hasDerivedStats = equipmentRuntime.TryGetDerivedStats(
                weapon.InstanceId,
                out CombatEquipmentDerivedStats weaponStats);
            string weaponName = hasDerivedStats
                ? weaponStats.DisplayName
                : weaponDefinition.DisplayName;
            builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Equipment.ActiveWeapon",
                weaponName,
                CharacterSummaryCombatTextFormatter.Quality(weapon.Quality),
                weapon.MaximumRange,
                ammo));
            if (hasDerivedStats)
            {
                builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Summary.Equipment.MaterialStats",
                    weaponStats.DamageMultiplier,
                    weaponStats.PenetrationDefenseMultiplier,
                    weaponStats.Weight));
            }
            builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Equipment.FireMode",
                CharacterSummaryCombatTextFormatter.FireMode(
                    profile?.fireMode ?? CombatFireMode.Aimed),
                (profile?.holdFire ?? false)
                    ? CharacterSummaryCombatTextFormatter.Get(
                        "CharacterSummary.Combat.Summary.Equipment.HoldFireSuffix")
                    : string.Empty));
        }
        else
        {
            builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Equipment.Unarmed"));
        }

        AppendEquipmentList(
            builder,
            CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Equipment.Armor"),
            profile?.armorInstanceIds);
        AppendEquipmentList(
            builder,
            CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Equipment.Shield"),
            string.IsNullOrWhiteSpace(profile?.shieldInstanceId)
                ? Array.Empty<string>()
                : new[] { profile.shieldInstanceId });

        EquipmentMaintenancePolicyData maintenancePolicy = maintenanceRuntime.GetPolicy(actor);
        builder.AppendLine();
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Maintenance.Title"));
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Maintenance.Policy",
            maintenancePolicy?.displayName
                ?? CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Common.Standard"),
            maintenancePolicy?.automaticRepair == true
                ? CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Summary.Maintenance.Automatic",
                    maintenancePolicy.sendAtDurability,
                    maintenancePolicy.returnAtDurability)
                : CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Summary.Maintenance.AutomaticOff")));
        CombatEquipmentRepairOrder repairOrder = maintenanceRuntime.Orders
            .FirstOrDefault(order =>
                order != null
                && (string.Equals(order.originalOwnerCharacterId, characterId, StringComparison.Ordinal)
                    || profile?.armorInstanceIds?.Contains(order.equipmentInstanceId, StringComparer.Ordinal) == true
                    || string.Equals(profile?.shieldInstanceId, order.equipmentInstanceId, StringComparison.Ordinal)));
        builder.AppendLine(repairOrder != null
            ? CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Maintenance.RepairActive",
                CharacterSummaryCombatTextFormatter.RepairState(repairOrder.state),
                repairOrder.ProgressRatio,
                repairOrder.materialItemId,
                repairOrder.requiredMaterialAmount,
                repairOrder.completedWork,
                repairOrder.requiredWork)
            : CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Maintenance.RepairNone"));

        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor);
        int arrows = carry?.CountItem(CombatItemDefinitions.ArrowItemId) ?? 0;
        int bolts = carry?.CountItem(CombatItemDefinitions.BoltItemId) ?? 0;
        float carriedWeight = carry?.GetCurrentWeight() ?? 0f;
        float equippedWeight = equipmentRuntime.GetCarriedWeight(characterId);
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Ammunition",
            arrows,
            bolts));
        builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
            "CharacterSummary.Combat.Summary.Weight",
            carriedWeight + equippedWeight,
            carry?.GetMaxAllowedWeight()));

        summaryText.text = builder.ToString().TrimEnd();
        RefreshCommandLabels(profile, weapon);
    }

    internal CombatEquipmentUiStatBlock GetCurrentEquipmentBonuses(CharacterActor actor)
    {
        string characterId = actor?.Identity?.PersistentId;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return CombatEquipmentUiStatBlock.Empty;
        }

        // Detailed equipment contributions are projected by the item-backed
        // equipment runtime once character-stat component effects are exposed.
        return CombatEquipmentUiStatBlock.Empty;
    }

    private bool TryGetRuntime(CharacterActor actor, out string characterId)
    {
        characterId = actor?.Identity?.PersistentId ?? string.Empty;
        if (actor != null && !string.IsNullOrWhiteSpace(characterId))
        {
            return true;
        }

        eventBus.ShowNotice(
            CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Notice.CombatInfoUnavailable"),
            NoticeFeedEvent.Grade.WARNING);
        return false;
    }

    private void RefreshCommandLabels(
        CharacterCombatLoadoutProfile profile,
        CombatWeaponSnapshot weapon)
    {
        SetButtonLabel(
            loadoutButton,
            string.Equals(profile?.profileId, CombatLoadoutPresetIds.Peace, StringComparison.Ordinal)
                ? CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Button.CombatLoadout")
                : CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Button.PeaceLoadout"));
        SetButtonLabel(
            weaponButton,
            CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Button.SwitchWeapon"));
        SetButtonLabel(reloadButton, weapon?.RequiresAmmo == true
            ? CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Button.ReloadCount",
                weapon.LoadedAmmo,
                weapon.MagazineCapacity)
            : CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Button.Reload"));
        SetButtonLabel(
            fireModeButton,
            CharacterSummaryCombatTextFormatter.FireMode(
                profile?.fireMode ?? CombatFireMode.Aimed));
        SetButtonLabel(
            holdFireButton,
            profile?.holdFire == true
                ? CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Button.HoldFire")
                : CharacterSummaryCombatTextFormatter.Get(
                    "CharacterSummary.Combat.Button.AllowFire"));
        SetButtonLabel(
            repairButton,
            CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Button.RequestRepair"));
        if (holdFireButton != null)
        {
            DungeonUiTheme.StyleButton(holdFireButton, selected: profile?.holdFire == true);
        }
    }

    private void AppendEquipmentList(
        StringBuilder builder,
        string label,
        IEnumerable<string> instanceIds)
    {
        string[] ids = instanceIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? Array.Empty<string>();
        if (ids.Length == 0)
        {
            builder.AppendLine(CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Equipment.ListNone",
                label));
            return;
        }

        List<string> rows = new List<string>();
        foreach (string id in ids)
        {
            if (!equipmentRuntime.TryGetInstance(id, out CombatEquipmentInstance instance)
                || !equipmentCatalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition))
            {
                continue;
            }

            if (equipmentRuntime.TryGetDerivedStats(instance.instanceId, out CombatEquipmentDerivedStats stats))
            {
                rows.Add(
                    CharacterSummaryCombatTextFormatter.Get(
                        "CharacterSummary.Combat.Summary.Equipment.ListDerived",
                        stats.DisplayName,
                        CharacterSummaryCombatTextFormatter.Quality(instance.quality),
                        instance.durabilityRatio * 100f,
                        stats.PenetrationDefenseMultiplier,
                        stats.Weight));
            }
            else
            {
                rows.Add(
                    CharacterSummaryCombatTextFormatter.Get(
                        "CharacterSummary.Combat.Summary.Equipment.ListBasic",
                        definition.DisplayName,
                        CharacterSummaryCombatTextFormatter.Quality(instance.quality),
                        instance.durabilityRatio * 100f));
            }
        }

        builder.AppendLine(rows.Count > 0
            ? $"{label}  {string.Join(" · ", rows)}"
            : CharacterSummaryCombatTextFormatter.Get(
                "CharacterSummary.Combat.Summary.Equipment.ListNone",
                label));
    }

    private static void SetButtonLabel(Button button, string text)
    {
        TMP_Text label = button != null
            ? button.transform.Find("Label")?.GetComponent<TMP_Text>()
            : null;
        if (label != null)
        {
            label.text = text ?? string.Empty;
        }
    }

}
