# DungeonStory V25 Narrative Mechanic Package

This immutable package contains controlled C# fixtures. It is not evidence of natural gameplay or human approval.

## Regeneration

In the authoritative DungeonStory Unity project, run `DungeonStory/Narrative/Export Mechanic Catalog`. The exporter captures every fixture through its named production producer and validator, computes source provenance, and writes a create-new directory through an atomic staging move. Existing versions are never overwritten.

## Validation

Verify every SHA-256 entry in `delivery_manifest.json`, then require its `currentCommit`, `inputDigest`, and `catalogHash` to match the package. Validate `scenarios_100.json` and `negative_scenarios.json` against `scenario_schema.json`. `continuity_scenarios.json` is a separate deterministic boundary-witness document; it is never added to the 100 accepted scenarios or 14 rejected scenarios. Run `NarrativeMechanicCatalogExporter.CompareIndependentExports(() => new NarrativeMechanicCatalogUnityAssetSource()).RequireByteIdentical()` in the authoritative Unity Editor and supply the separately produced evidence gates before claiming training eligibility.

## AI consumption

The current NarrativeAI catalog importer reads only `catalog.json`; it does not ingest scenario files. A separate scenario adapter may consume `scenarios_100.json` and `negative_scenarios.json` only after `delivery_manifest.json` and `scenario_schema.json` validation. Its model-input allowlist is exactly `publicNarrativeContext` and `publicFacts`. It must never consume raw `request`, raw prompt, response, audit, semantic hashes, or fixture data. In particular exclude `responseJson`, expected or accepted outcomes, failure reasons, validator results, internal state, `authorityContext`, `fixtureInput`, `originalFactId`, and `sourceSubjectId`. Positive examples are exactly the accepted records in `scenarios_100.json`; command or validator rejections in `negative_scenarios.json` are evaluation-only and never count toward the 100 positives or become model input. `scenarios_100.json` uses schemaVersion 2 and `negative_scenarios.json` uses schemaVersion 3; an adapter must not fabricate candidates for a rejected command with an empty `fullLegalCandidates` array. Do not infer gameplay authority from generated prose.
