# DungeonStory V25 Narrative Mechanic Package

This immutable package contains controlled C# fixtures. It is not evidence of natural gameplay or human approval.

## Regeneration

In the authoritative DungeonStory Unity project, run `DungeonStory/Narrative/Export Mechanic Catalog`. The exporter captures every fixture through its named production producer and validator, computes source provenance, and writes a create-new directory through an atomic staging move. Existing versions are never overwritten.

## Validation

Verify every SHA-256 entry in `delivery_manifest.json`, then require its `currentCommit`, `inputDigest`, and `catalogHash` to match the package. Validate `scenarios_100.json` and `negative_scenarios.json` against `scenario_schema.json`. Run `NarrativeMechanicCatalogExporter.CompareIndependentExports(() => new NarrativeMechanicCatalogUnityAssetSource()).RequireByteIdentical()` in the authoritative Unity Editor and supply the separately produced evidence gates before claiming training eligibility.

## AI consumption

The AI training pipeline must consume `catalog.json` and `scenarios_100.json` together only after delivery-manifest verification. Positive examples are exactly the accepted records in `scenarios_100.json`; validator rejections in `negative_scenarios.json` are evaluation-only and never count toward the 100 positives. Treat `authorityContext` and `fixtureInput` as locked audit provenance, and expose only `publicFacts` to narrative prompting. Do not infer gameplay authority from generated prose.
