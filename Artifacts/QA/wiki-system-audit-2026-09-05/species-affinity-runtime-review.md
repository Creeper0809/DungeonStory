# Species affinity facility runtime review

## Initial population

- `BuildingSpeciesAffinityAbility` has 14 authored building assets, all under `Assets/Resources/SO/Building/P1`: Barracks, BattleDining, BattlefieldDining, GeneralStore, LowFoodShop, ManaStorage, MeatRestaurant, NobleDining, PremiumMeatRestaurant, ResearchLab, RestRoom, TrainingRoom, WarBarracks, and WeaponShop.
- Production references are limited to ability accessors and `FacilityCandidateScorer`; the next read must extract the exact preferred/disliked tag weights and determine whether these P1 assets are deprecated compatibility inputs that are replaced during migration.
- Do not treat this authored count as 14 active public facilities until the compatibility flags, public IDs, and migrator mappings are checked.

## Confirmed runtime formula and compatibility flag

- All 14 P1 assets set `deprecatedCompatibilityAsset: 1`.
- Candidate scoring reads the actor's `Identity.SpeciesTag`, compares it case-insensitively with preferred/disliked arrays, and assigns bias +0.35 or -0.35.
- A preferred candidate transforms normalized score `s` to `lerp(s,1,clamp(0.35*2.2)) = 0.23s+0.77`; a disliked candidate transforms it to `0.65s`. Neutral or missing tags leave the score unchanged.
- The exact tag arrays and save migrator replacements remain to be extracted. Because every authored asset is deprecated compatibility content, no active/public documentation gap is assigned yet.

## Migration evidence

- `ModularFacilityInitialPlacementMigrator` has explicit recipes for all 14 P1 assets and expands each monolithic legacy room/store into current modular facilities before placement. Examples include P1_LowFoodShop→D01+D04+E01, P1_Barracks→R03+G01+G05, P1_ResearchLab→Q01+Q03+Q04+Q06, and P1_ManaStorage→M02+M03+E05.
- The first tag-block search passed a Windows wildcard path directly to `rg` and failed with OS error 123. It is excluded and will be rerun with `--glob '*.asset'` on the directory.

## Authored tag matrix and publication boundary

- Barracks, BattleDining, BattlefieldDining, and WarBarracks prefer Orc only.
- GeneralStore prefers Goblin+Slime and dislikes Demon; LowFoodShop prefers Slime and dislikes Demon.
- ManaStorage prefers Vampire and dislikes Slime; MeatRestaurant prefers Orc and dislikes Slime.
- NobleDining prefers Vampire; PremiumMeatRestaurant prefers Orc+Vampire.
- ResearchLab prefers Vampire and dislikes Orc; RestRoom prefers Slime and dislikes Orc.
- TrainingRoom and WeaponShop prefer Orc and dislike Slime.
- None of the 14 legacy numeric IDs (10–17, 50, 51, 54–56, 59) has a public facility entity. This is consistent with deprecated compatibility exclusion, not a missing public page by itself.
- The replacement modular assets do not inherit these arrays through the migration recipe. Before calling this a lost active feature, confirm every production entry path that invokes `IsLegacyMonolith` and every catalog filter for `deprecatedCompatibilityAsset`.

## Reachability boundary in production scripts

- `ExpandInitialRooms` is called by `GridBuildingRuntime.PlaceInitialBuildings`, so legacy P1 monoliths in authored initial placement are expanded before placement and never reach facility scoring as those legacy BuildingSO instances.
- In non-Editor `Assets/Scripts`, `IsLegacyMonolith` has no caller outside the migrator itself except that initial-placement expansion path. `IsDeprecatedCompatibilityAsset` likewise has only its BuildingSO property definition; no production filter consumer appeared in the focused search.
- This means the tag formula is real code but the only authored tag population is compatibility content removed from the normal initial-placement path. Whether another catalog/build/save path can instantiate those assets requires a repository-wide exact consumer search; do not publish affinity values as active gameplay yet.
- The first repository-wide compatibility search crossed generated content-db and QA CSVs, produced 130k tokens, and was truncated. It is excluded. A narrower source/tool search is required.
- Candidate instantiation paths identified for focused reading are run-start catalog selection, facility shop pools, grid placement by numeric ID, and modular world save restore. The path list alone does not prove legacy reachability.

## Narrow compatibility consumers

- Source/tool search finds the deprecated flag used by the public content-database generator and audit/debug code, but no ordinary runtime catalog filter.
- Run-start selection samples low-star catalog buildings without directly checking the deprecated flag; however any selected P1 monolith then passes through initial-placement expansion.
- World-save restore resolves the saved numeric building ID directly and does not call the initial-placement migrator or reject deprecated compatibility assets. Therefore old saves containing P1 IDs can restore the legacy object and the affinity scorer remains reachable for backward compatibility.
- Facility-shop pools delegate to `IsDailyShopBuildingCandidate`/`CanEnterBasicPurchase`; their exact unlock/deprecated behavior and the P1 `unlocked` values remain to be checked before closing new-game reachability.
- All 14 affinity P1 assets have `unlocked: 0`. However, `IsDailyShopBuildingCandidate` checks only non-null, non-movement, non-wall, and star<=2; it does not inspect `unlocked` or deprecated status. `CanEnterBasicPurchase` likewise checks only star<=2 after an external unlock-state condition.
- Therefore the daily-shop caller must be inspected: if it passes the raw catalog population, deprecated P1 affinity facilities can still be offered and built, making the affinity behavior active rather than save-only compatibility.
- Both daily runtime and day-settlement projection call `FacilityShopService.CreateDailyOffers` with an `IFacilityShopCatalog`; the overload passes `catalog.Buildings` unchanged to the weak candidate filter.
- A combined catalog search included one nonexistent directory (`Assets/Scripts/Services/Content`) and exited 1. The valid matches identify `GameDomainContentCatalogSO`, but the catalog property's filtering behavior must be read directly; do not infer from the partial command.
- `GameDomainContentCatalogSO.GetAll<BuildingSO>` filters only null and duplicate references, not deprecated content. The runtime adapter is `DataCatalogFacilityShopCatalog` backed by `IDataCatalog`; its property implementation is the remaining decisive check.
- The broad `IFacilityShopCatalog` search itself was truncated at 11k tokens, so only the exact adapter/file location is retained; the next read is the provider file alone.
- Direct provider read confirms `DataCatalogFacilityShopCatalog.Buildings` returns every non-null value from `IDataCatalog.GetData<BuildingSO>()` with no deprecated/unlocked filter. `GameContentDataCatalog` builds its type index from `GameDomainContentCatalogSO.GetAll<DataScriptableObject>()`, again without such a filter.
- If the 14 P1 assets are referenced by `GameDomainContentCatalog.asset`, daily offers can sample those with star<=2 despite `unlocked:0`; catalog membership and star derivation are the remaining checks.

## Daily shop reachability confirmed

- All 14 P1 affinity assets are referenced by `GameDomainContentCatalog.asset` and therefore enter `IDataCatalog`/`IFacilityShopCatalog`.
- `GetBuildingStar` returns quality star when present, defense star for defense facilities, otherwise1. Thus the low-star P1 compatibility assets satisfy the daily candidate's <=2 gate even though `unlocked:0` and deprecated are ignored.
- These assets are therefore not only old-save compatibility data: at least the low-star subset is reachable in normal daily facility offers. A purchased/built legacy facility can retain the affinity arrays and use the real scorer formula because the modular migrator is limited to initial placement.
- Purchase inspection confirms a random `FacilityBuildingOffer` keeps the exact BuildingSO in `FacilityShopPurchaseResult`; `ApplyPurchase` does not reject deprecated content. Basic offers alone mutate the basic unlock state. The downstream purchase-event/installation-kit consumer still must be checked to prove direct construction from a random legacy offer.
- The downstream is confirmed: `BlueprintResearchRuntime.OnTriggerEvent(FacilityShopPurchasedEvent)` unlocks the purchased building ID and spawns a loose `facility-kit:<id>` at the delivery dropoff. Construction accepts that kit for the exact BuildingSO. No deprecated gate appears in this path.
- This is primarily a deprecated-content leakage/implementation problem. Public pages correctly omit the P1 IDs, so documenting all 14 as current facilities would be wrong. Check existing facility-shop/deprecated-content owners before creating a new WIM or implementation uncertainty; the affinity values themselves should remain excluded from the reverse wiki-gap count.
- Static star projection shows 13 of the 14 affinity assets satisfy the daily <=2 gate; only P1_WarBarracks resolves to star3. There is no existing GAP or WIM owner for deprecated P1 daily-shop leakage.

## Final disposition

- No new reverse documentation GAP: the 14 affinity assets are explicitly deprecated compatibility content, have no public entity pages, and should not be documented as current facilities.
- Add direct-audit WIM-063 (`different`): the active daily facility-shop catalog does not honor deprecated/unlocked status, so 13 legacy P1 facilities can be offered, purchased, unlocked, and materialized as installation kits. Some of those then expose the otherwise compatibility-only species-affinity scorer.
- The correct resolution is to filter deprecated compatibility assets from run-start/daily/basic offer populations while retaining numeric-ID lookup for old-save restore. If legacy affinity is intended for modular replacements, it needs a separately authored current-content design rather than accidental P1 leakage.
- No game code/assets/public wiki were changed and no Play Mode run was performed.
