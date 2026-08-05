public static class CharacterSummaryCombatTextFormatter
{
    public static string Get(string key, params object[] arguments)
    {
        return CharacterSummaryUiTextQuery.Get(key, arguments);
    }

    public static string FireMode(CombatFireMode mode)
    {
        return mode switch
        {
            CombatFireMode.Rapid => Get("CharacterSummary.Combat.FireMode.Rapid"),
            CombatFireMode.Suppressive => Get("CharacterSummary.Combat.FireMode.Suppressive"),
            _ => Get("CharacterSummary.Combat.FireMode.Aimed")
        };
    }

    public static string RepairState(CombatEquipmentRepairOrderState state)
    {
        return state switch
        {
            CombatEquipmentRepairOrderState.PendingCombatEnd =>
                Get("CharacterSummary.Combat.RepairState.PendingCombatEnd"),
            CombatEquipmentRepairOrderState.WaitingForDelivery =>
                Get("CharacterSummary.Combat.RepairState.WaitingForDelivery"),
            CombatEquipmentRepairOrderState.Ready =>
                Get("CharacterSummary.Combat.RepairState.Ready"),
            CombatEquipmentRepairOrderState.InProgress =>
                Get("CharacterSummary.Combat.RepairState.InProgress"),
            CombatEquipmentRepairOrderState.Completed =>
                Get("CharacterSummary.Combat.RepairState.Completed"),
            _ => Get("CharacterSummary.Combat.RepairState.Cancelled")
        };
    }

    public static string Quality(CombatEquipmentQuality quality)
    {
        return quality switch
        {
            CombatEquipmentQuality.Awful => Get("CharacterSummary.Combat.Quality.Awful"),
            CombatEquipmentQuality.Poor => Get("CharacterSummary.Combat.Quality.Poor"),
            CombatEquipmentQuality.Good => Get("CharacterSummary.Combat.Quality.Good"),
            CombatEquipmentQuality.Excellent => Get("CharacterSummary.Combat.Quality.Excellent"),
            CombatEquipmentQuality.Masterwork => Get("CharacterSummary.Combat.Quality.Masterwork"),
            CombatEquipmentQuality.Legendary => Get("CharacterSummary.Combat.Quality.Legendary"),
            _ => Get("CharacterSummary.Combat.Quality.Normal")
        };
    }

    public static string BodyPart(CombatBodyPart bodyPart)
    {
        return bodyPart switch
        {
            CombatBodyPart.Head => Get("CharacterSummary.Combat.BodyPart.Head"),
            CombatBodyPart.LeftArm => Get("CharacterSummary.Combat.BodyPart.LeftArm"),
            CombatBodyPart.RightArm => Get("CharacterSummary.Combat.BodyPart.RightArm"),
            CombatBodyPart.LeftLeg => Get("CharacterSummary.Combat.BodyPart.LeftLeg"),
            CombatBodyPart.RightLeg => Get("CharacterSummary.Combat.BodyPart.RightLeg"),
            _ => Get("CharacterSummary.Combat.BodyPart.Torso")
        };
    }

    public static string FailureReason(string reasonCode)
    {
        return reasonCode switch
        {
            "equipment.loadout.character_required" =>
                Get("CharacterSummary.Combat.Failure.CharacterRequired"),
            "equipment.loadout.weapon_not_assigned" =>
                Get("CharacterSummary.Combat.Failure.WeaponNotAssigned"),
            "equipment.loadout.insufficient_hands" =>
                Get("CharacterSummary.Combat.Failure.InsufficientHands"),
            "equipment.fire_mode.no_active_ranged_weapon" =>
                Get("CharacterSummary.Combat.Failure.NoActiveRangedWeapon"),
            "equipment.fire_mode.unsupported" =>
                Get("CharacterSummary.Combat.Failure.UnsupportedFireMode"),
            _ => Get("CharacterSummary.Combat.Failure.Unknown", reasonCode ?? string.Empty)
        };
    }
}
