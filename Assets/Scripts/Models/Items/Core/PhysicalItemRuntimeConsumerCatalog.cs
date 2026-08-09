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
        new("component:material-test-coupon", "runtime:equipment-module-testing"),
        new("medical:cross-lineage-medium", "runtime:cross-lineage-reproduction"),
        new("medical:fertility-treatment", "runtime:fertility-treatment"),
        new("medical:isolation-care-kit", "runtime:disease-field-response"),
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
