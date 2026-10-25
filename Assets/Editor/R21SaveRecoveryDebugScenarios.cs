#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R21SaveRecoveryDebugScenarios
{
    public static void RunAll()
    {
        var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        VerifyOriginAliases(content);
        VerifyRegions(content);
        VerifyAmmunition(content);
        VerifyIncidentOwners(content);
        Require(CharacterSpeciesIncidentIds.DeclaredIds.Count() == 11
            && CharacterSpeciesIncidentIds.DiscontentIds.Count() == 6,
            "Species incident ownership changed unexpectedly.");
        Require(!CharacterSpeciesIncidentIds.IsDiscontentDriven(CharacterSpeciesIncidentIds.HumanAdjustment)
            && !CharacterSpeciesIncidentIds.IsDiscontentDriven(CharacterSpeciesIncidentIds.AdventurerIncursion),
            "Externally owned incidents entered the discontent retry loop.");
        Debug.Log(SurgeryDebugScenarios.VerifyAnatomyProfiles());
        Debug.Log("R21_SAVE_RECOVERY=PASS; generated-origin; preserved-legacy-loyalty; region-restore-retry; ammunition-canonical-payload");
    }

    private static void VerifyIncidentOwners(IGameContentDefinitionSource content)
    {
        var handlers = WimKoboldLoreDebugScenarios.CreateGuardedRegistry();
        foreach (string incident in new[] { CharacterSpeciesIncidentIds.HumanAdjustment,
                     CharacterSpeciesIncidentIds.AdventurerIncursion })
        {
            var species = content.GetAll<CharacterSpeciesSO>().Single(value => value.IncidentId == incident);
            var state = new CharacterSpeciesRuntimeState();
            string before = JsonUtility.ToJson(state);
            Require(!handlers.TryExecute(new SpeciesIncidentContext(null, species, state), out string summary)
                && string.IsNullOrEmpty(summary) && JsonUtility.ToJson(state) == before,
                "An externally owned species incident mutated generic discontent state.");
        }
        WimKoboldLoreDebugScenarios.VerifyKoboldHoardingIsLoreOnly();
    }

    private static void VerifyOriginAliases(IGameContentDefinitionSource content)
    {
        var catalog = new CharacterNarrativeCatalog(content);
        var reactions = catalog.Backgrounds.SelectMany(background => background.factionReactions
            .Where(row => row.id == "faction:old-nobility" || row.id == "faction:iron-consortium")
            .Select(row => (background, row))).ToArray();
        Require(reactions.Any(value => value.row.id == "faction:old-nobility")
            && reactions.Any(value => value.row.id == "faction:iron-consortium"),
            "The authored legacy faction aliases have no origin reaction fixtures.");
        int index = 0;
        foreach (var reaction in reactions)
        {
            string faction = BackgroundFactionReactionRules.CanonicalFactionId(reaction.row.id);
            var archetype = Object.Instantiate(content.GetAll<EnemyArchetypeDefinitionSO>()
                .First(value => value.factionId == faction));
            try
            {
                archetype.individualGeneration.minimumLoyalty = 50f;
                archetype.individualGeneration.maximumLoyalty = 50f;
                archetype.individualGeneration.allowedBackgroundIds = new List<string> { reaction.background.StableId };
                var (factory, narrative) = Factory(content, archetype);
                var generated = factory.Create(archetype.stableId,
                    CharacterId.FromStableSuffix("r21-origin-" + index++), "qa:r21");
                float expected = 50f + (reaction.row.weight - 1f) * 12f;
                Require(Mathf.Abs(generated.loyalty - expected) < .001f,
                    "The actual enemy factory ignored its authored origin reaction.");
                var saved = JsonUtility.FromJson<EnemyIndividualSaveData>(JsonUtility.ToJson(generated));
                // Existing individuals retain the loyalty they earned before this alias was connected.
                saved.loyalty = 50f;
                var (freshFactory, freshNarrative) = Factory(content, archetype);
                var blueprint = freshFactory.RequireBlueprint(saved);
                freshFactory.EnsureCharacterDomains(blueprint);
                string before = JsonUtility.ToJson(freshNarrative.Capture());
                freshFactory.EnsureCharacterDomains(blueprint);
                Require(JsonUtility.ToJson(freshNarrative.Capture()) == before,
                    "A restored origin registration was applied twice.");
                Require(freshNarrative.TryGet(new CharacterId(saved.characterId), out var restored)
                    && restored.Loyalty == 50f, "Restore recalculated historical loyalty.");
                var broken = saved.Clone();
                broken.originFactionId = "human:missing-r21";
                bool rejected = false;
                try { freshFactory.RequireBlueprint(broken); }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected && JsonUtility.ToJson(freshNarrative.Capture()) == before,
                    "A broken saved faction changed live origin state.");
                freshFactory.EnsureCharacterDomains(freshFactory.RequireBlueprint(saved));
                Require(JsonUtility.ToJson(freshNarrative.Capture()) == before,
                    "Retry after rejecting a broken faction changed restored origin state.");
            }
            finally { Object.DestroyImmediate(archetype); }
        }
    }

    private static (EnemyIndividualFactory, CharacterNarrativeRuntime) Factory(
        IGameContentDefinitionSource content, EnemyArchetypeDefinitionSO archetype)
    {
        var root = new DungeonRuntimeAggregateRootStore();
        var narrativeCatalog = new CharacterNarrativeCatalog(content);
        var narrative = new CharacterNarrativeRuntime(root, narrativeCatalog);
        var species = new ResourceCharacterSpeciesCatalog(content);
        var life = new CharacterLifeRuntime(root, species, new RandomStreamProvider(root));
        return (new EnemyIndividualFactory(new SingleArchetype(archetype), narrativeCatalog,
            narrative, narrative, life, life, species, content), narrative);
    }

    private static void VerifyRegions(IGameContentDefinitionSource content)
    {
        var definitions = content.GetAll<OffenseCampaignCatalogSO>().Single().Regions;
        var live = new OffenseRegionRuntime(definitions);
        var definition = definitions.First();
        var target = new OffenseTargetDefinition
        {
            regionId = definition.id + "-typo", factionId = definition.factionId,
            strategicPressureAxis = StrategicPressureAxis.Logistics, strategicPressureAmount = 15f
        };
        string before = JsonUtility.ToJson(live.Capture());
        Require(!live.TryApplyTargetPressure(target, 1, out _, out _, out _)
            && JsonUtility.ToJson(live.Capture()) == before, "An unknown region created phantom state.");
        target.regionId = definition.id;
        Require(live.TryApplyTargetPressure(target, 1, out _, out _, out float applied) && applied == 15f,
            "Repairing the region reference did not allow the actual pressure command.");
        var saved = JsonUtility.FromJson<DungeonOffenseRegionSaveData>(JsonUtility.ToJson(live.Capture()));
        saved.regions.Add(new OffenseRegionState
        {
            regionId = "retired-r21", displayName = "Historical region", factionId = "retired-faction", logisticsDamage = 7f
        });
        var extended = definitions.Concat(new[] { new OffenseCampaignRegionDefinition
            { id = "new-r21", displayName = "New region", factionId = "new-faction" } }).ToArray();
        var restored = new OffenseRegionRuntime(extended);
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
        Require(restored.Regions.Single(value => value.regionId == definition.id).logisticsDamage == 15f
            && restored.Regions.Single(value => value.regionId == "retired-r21").logisticsDamage == 7f
            && restored.Regions.Single(value => value.regionId == "new-r21").logisticsDamage == 0f,
            "Region restoration lost existing pressure or failed to initialize an appended definition.");
        string restoredBefore = JsonUtility.ToJson(restored.Capture());
        var broken = JsonUtility.FromJson<DungeonOffenseRegionSaveData>(restoredBefore);
        broken.regions.Single(value => value.regionId == definition.id).factionId = "wrong-r21";
        bool rejected = false;
        try { restored.BuildRestoreCandidate(broken); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && JsonUtility.ToJson(restored.Capture()) == restoredBefore,
            "A rejected region restore partially changed pressure.");
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
        Require(JsonUtility.ToJson(restored.Capture()) == restoredBefore, "Region restore retry duplicated pressure.");
        target.regionId = "retired-r21";
        target.factionId = "retired-faction";
        Require(!restored.TryApplyTargetPressure(target, 1, out _, out _, out _),
            "Historical state authorized an unregistered new target.");
    }

    private static void VerifyAmmunition(IGameContentDefinitionSource content)
    {
        var ammunition = content.GetAll<ResourceItemDefinitionSO>().Single(value => value.ItemId == "ammo:arrow-iron");
        var materializer = new CombatAmmunitionPreparedOutputMaterializer();
        var descriptor = new ProductionOutputCapabilityDescriptor("r21-output", ammunition.ItemId,
            materializer.CapabilityId, materializer.CapabilityVersion, materializer.ComponentCodecId,
            materializer.ComponentCodecVersion, "r21-descriptor");
        var created = materializer.Create(descriptor, ammunition);
        var saved = JsonUtility.FromJson<PreparedPayload>(JsonUtility.ToJson(new PreparedPayload
            { payload = created.CanonicalPayload, fingerprint = created.Fingerprint }));
        var restored = new CombatAmmunitionPreparedOutputMaterializer().ValidateAndDecode(
            descriptor, ammunition, saved.payload, saved.fingerprint);
        Require(restored.CanonicalPayload == created.CanonicalPayload && restored.Fingerprint == created.Fingerprint,
            "A persisted ammunition kind changed its canonical output or fingerprint.");
        bool rejected = false;
        try { materializer.ValidateAndDecode(descriptor, ammunition, saved.payload + "-corrupt", saved.fingerprint); }
        catch (ProductionPreparedOutputComponentCodecException) { rejected = true; }
        Require(rejected, "Corrupted ammunition payload was accepted.");
        Require(materializer.ValidateAndDecode(descriptor, ammunition, saved.payload, saved.fingerprint).Fingerprint
            == created.Fingerprint, "A valid ammunition retry failed after a rejected payload.");
    }

    [Serializable] private sealed class PreparedPayload { public string payload; public string fingerprint; }
    private sealed class SingleArchetype : IEnemyArchetypeCatalog
    {
        private readonly EnemyArchetypeDefinitionSO value;
        public SingleArchetype(EnemyArchetypeDefinitionSO value) => this.value = value;
        public IReadOnlyList<EnemyArchetypeDefinitionSO> All => new[] { value };
        public bool TryGet(string id, out EnemyArchetypeDefinitionSO definition)
        { definition = id == value.stableId ? value : null; return definition != null; }
        public EnemyArchetypeDefinitionSO Require(string id) => TryGet(id, out var found)
            ? found : throw new InvalidOperationException("Unknown R21 origin archetype.");
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
}
#endif
