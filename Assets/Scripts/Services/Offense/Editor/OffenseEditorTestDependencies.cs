using DungeonStory.Foundation;

internal static class OffenseEditorTestDependencies
{
    public static IOffenseCampaignCatalog CreateCampaignCatalog() =>
        new ResourceOffenseCampaignCatalog(
            new ResourceGameContentCatalog(
                new UnityGameContentRootLoader()));

    public static ICombatResolutionService CreateCombatResolution() =>
        new DeterministicCombatResolutionService();

    public static ICombatEquipmentRuntime CreateCombatEquipmentRuntime()
    {
        return CombatEquipmentEditorTestFactory.Create(
            new ResourceCombatEquipmentCatalog(
                new ResourceGameContentCatalog(new UnityGameContentRootLoader())),
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            materialCatalog: EmptyResourceEconomyContentCatalog.Instance,
            evolutionModules: EmptyEvolutionModuleRegistry.Instance,
            researchProvider: EditorAllResearchRuntimeProvider.Instance,
            moduleCatalog: EmptyEquipmentModuleCatalog.Instance,
            itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
    }

    private sealed class DeterministicCombatResolutionService : ICombatResolutionService
    {
        public CombatAttackResult Resolve(CombatAttackRequest request)
        {
            float baseDamage = request.Weapon?.Verb != null
                ? request.Weapon.Verb.baseDamage
                : 1f;
            float damage = (baseDamage + request.Attacker.Melee * 0.5f)
                * request.AttackPowerMultiplier;
            return new CombatAttackResult(
                executed: true,
                hit: true,
                coverBlocked: false,
                evaded: false,
                bodyPart: CombatBodyPart.Torso,
                rawDamage: damage,
                appliedDamage: damage,
                bleeding: 0f,
                suppression: 0f,
                armorDurabilityDamage: 0f,
                armorInstanceId: string.Empty,
                failureReason: string.Empty);
        }

        public CombatAttackPreview Preview(CombatAttackRequest request)
        {
            float baseDamage = request.Weapon?.Verb != null
                ? request.Weapon.Verb.baseDamage
                : 1f;
            float damage = (baseDamage + request.Attacker.Melee * 0.5f)
                * request.AttackPowerMultiplier;
            return new CombatAttackPreview(
                true,
                string.Empty,
                CombatRangeRules.GetBand(request.Distance),
                1f,
                0f,
                0f,
                0f,
                damage,
                damage);
        }

        public float CalculateAttackInterval(
            CombatStatSnapshot attacker,
            CombatWeaponSnapshot weapon,
            CombatFireMode mode) => 1f;

        public float CalculateReloadTime(
            CombatStatSnapshot actor,
            CombatWeaponSnapshot weapon) => 1f;

        public float CalculateWeaponSwitchTime(
            CombatStatSnapshot actor,
            float weaponWeight) => 0.5f;
    }
}
