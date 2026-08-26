using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Declares physical-item consumers whose requirements are owned by typed
/// runtime commands rather than recipe or building assets. This catalog is an
/// audit bridge only: it never consumes an item and cannot satisfy a request by
/// itself. Each owner id names the runtime that performs the reservation,
/// durability wear, or atomic consumption.
/// </summary>
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class PhysicalItemRuntimeConsumerCatalog
{
    public readonly struct Link
    {
        public Link(string itemId, string ownerId)
        {
            ItemId = itemId ?? string.Empty;
            OwnerId = ownerId ?? string.Empty;
        }

        public string ItemId { get; }
        public string OwnerId { get; }
    }

    private static readonly Link[] RuntimeLinks =
    {
        new(DurableToolItemRules.SeasonalAlmanac, "runtime:climate-forecast"),
        new("food:preserved-ration", "runtime:offense-supply-package"),
        new("medicine:blood-seal-kit", "runtime:offense-supply-package"),
        new("medicine:field-emergency-kit", "runtime:offense-supply-package"),
        new("medicine:mana-core-restraint", "runtime:offense-supply-package"),
        new("medicine:mycelial-culture-pack", "runtime:offense-supply-package"),
        new("medicine:rune-slime-patch", "runtime:offense-supply-package"),
        new("medicine:standard", "runtime:offense-supply-package"),
        new("medicine:temporary-power-bypass", "runtime:offense-supply-package"),
        new("medicine:wing-splint-kit", "runtime:offense-supply-package"),
        new("resource:mana-crystal", "runtime:offense-supply-package"),
        new("tool:field-repair-kit", "runtime:offense-supply-package"),
        new("material:low-fuel", "runtime:offense-urgent-mitigation"),
        new("material:lumber", "runtime:offense-urgent-mitigation"),
        new("medicine:standard", "runtime:offense-urgent-mitigation"),
        new("resource:mana-crystal", "runtime:offense-urgent-mitigation"),
        new("component:material-test-coupon", "runtime:equipment-module-testing"),
        new("medical:cross-lineage-medium", "runtime:cross-lineage-reproduction"),
        new("medical:fertility-treatment", "runtime:fertility-treatment"),
        new("medical:isolation-care-kit", "runtime:disease-field-response"),
        new("component:reclaimed-water-filter", "runtime:disease-field-response"),
        new("drug:dreamleaf-analgesic", "runtime:disease-field-response"),
        new("medicine:antidote", "runtime:disease-field-response"),
        new("medicine:blood-pack", "runtime:disease-field-response"),
        new("resource:clean-water", "runtime:disease-field-response"),
        new("supply:fungicide", "runtime:disease-field-response"),
        new("supply:pest-lure", "runtime:disease-field-response"),
        new("medicine:vaccine:blood-wasting", "runtime:physical-vaccination"),
        new("medicine:vaccine:cave-flu", "runtime:physical-vaccination"),
        new("medicine:vaccine:gut-rot", "runtime:physical-vaccination"),
        new("medicine:vaccine:mana-pox", "runtime:physical-vaccination"),
        new("medicine:vaccine:red-fever", "runtime:physical-vaccination"),
        new("medicine:vaccine:slime-blight", "runtime:physical-vaccination"),
        new("medicine:vaccine:spore-lung", "runtime:physical-vaccination"),
        new("captivity:extracted-blood", "runtime:character-medical-treatment"),
        new("medical:regenerative-medium", "runtime:character-medical-treatment"),
        new("medical:sterile-bandage", "runtime:character-medical-treatment"),
        new("medicine:advanced", "runtime:character-medical-treatment"),
        new("medicine:antiseptic", "runtime:character-medical-treatment"),
        new("medicine:herbal-poultice", "runtime:character-medical-treatment"),
        new("medicine:mycelial-culture-pack", "runtime:character-medical-treatment"),
        new("medicine:standard", "runtime:character-medical-treatment"),
        new("medical:organ-preservation-canister", "runtime:organ-preservation"),
        new("medical:trait-analysis-kit", "runtime:trait-analysis"),
        new("medical:trauma-care-kit", "runtime:trauma-counselling"),
        new(DurableToolItemRules.ArcaneIndex, "runtime:research-indexing"),
        new(DurableToolItemRules.BreedingLedger, "runtime:breeding-record"),
        new(DurableToolItemRules.CareerLedger, "runtime:career-record"),
        new("supply:alliance-signal-kit", "runtime:alliance-defense-signal"),
        new("supply:certified-seed-kit", "runtime:seed-certification"),
        new("supply:defense-mixed-ammo-box", "runtime:defense-ammunition-feed"),
        new("supply:funeral-preparation-kit", "runtime:funeral-preparation"),
        new("supply:performance-prop-box", "runtime:circus-performance"),
        new(DurableToolItemRules.AdministrativeSeal, "runtime:society-administration"),
        new(DurableToolItemRules.BanquetCart, "runtime:circus-banquet-service"),
        new(DurableToolItemRules.InspectionGauge, "runtime:equipment-module-inspection"),
        new(DurableToolItemRules.PrisonerWorkKit, "runtime:prisoner-labour"),
        new(DurableToolItemRules.ReinforcedRestraint, "runtime:prisoner-restraint"),
        new(DurableToolItemRules.RuneIdentificationLens, "runtime:rune-module-identification"),
        new(DurableToolItemRules.WatchSignalHorn, "runtime:invasion-watch-signal"),
        new(DurableToolItemRules.WeatherObservationKit, "runtime:weather-observation")
    };

    public static IReadOnlyList<Link> All => RuntimeLinks;
}
