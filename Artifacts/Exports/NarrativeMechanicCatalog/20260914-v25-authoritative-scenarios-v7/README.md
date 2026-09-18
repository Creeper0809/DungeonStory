# DungeonStory V25 Narrative Mechanic Package

This immutable package contains controlled C# fixtures. It is not evidence of natural gameplay or human approval.

## Regeneration

In the authoritative DungeonStory Unity project, run `DungeonStory/Narrative/Export Mechanic Catalog`. The exporter captures every fixture through its named production producer and validator, computes source provenance, and writes a create-new directory through an atomic staging move. Existing versions are never overwritten.

## Validation

Verify every SHA-256 entry in `delivery_manifest.json`, then require its `currentCommit`, `inputDigest`, and `catalogHash` to match the package. Validate `scenarios_100.json` and `negative_scenarios.json` against `scenario_schema.json`. Run `NarrativeMechanicCatalogExporter.CompareIndependentExports(() => new NarrativeMechanicCatalogUnityAssetSource()).RequireByteIdentical()` in the authoritative Unity Editor and supply the separately produced evidence gates before claiming training eligibility.

## AI consumption

The current NarrativeAI catalog importer reads only `catalog.json`; it does not ingest scenario files. A separate scenario adapter may consume `scenarios_100.json` and `negative_scenarios.json` only after `delivery_manifest.json` and `scenario_schema.json` validation. Never pass the raw `request` object to a model. Build model input from an explicit, versioned allowlist for each profile and exclude `responseJson`, expected or accepted outcomes, failure reasons, validator results, internal state, `authorityContext`, and `fixtureInput`. Only intended request fields, legal candidates, and public facts may cross that boundary. Positive examples are exactly the accepted records in `scenarios_100.json`; command or validator rejections in `negative_scenarios.json` are evaluation-only and never count toward the 100 positives or become model input. `scenarios_100.json` retains schemaVersion 1. `negative_scenarios.json` uses schemaVersion 2 so a production command rejected before candidate construction can truthfully carry an empty `fullLegalCandidates` array; an adapter must not fabricate candidates for such a case. Do not infer gameplay authority from generated prose.
