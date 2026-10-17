# CharacterSkill public semantics v17 connectivity manifest

## Scope and authority

- Export: `Artifacts/Exports/NarrativeMechanicCatalog/20260915-v25-authoritative-scenarios-v17-character-skill-semantics-catalog-coverage`
- Current source commit: `c03d2e0a1c68067de48840f3851b388483966a6e` with preserved dirty changes
- Gameplay/balance authority is unchanged. This batch adds a read-only public meaning projection.
- Current C# and authored assets were inspected directly before implementation. The final
  knowledge-base rebuild completed once with zero validation failures and now indexes
  `CharacterSkillCombinationSemantics.cs` as fresh (`content source digest
  951673e88dbd6a184c4f2b3eae0066342a922666f5b45dcafb5dcb4ab89dad6c`, `system source
  digest 1771f577773ed1cdd4f6057b886be4a6ba764b6b6111e51d6d31eeb63ba7f434`).

## Forward/reverse connectivity

| symbol-or-id | definition | live-producer | authority | live-consumer | save-or-recompute | player/ai-observation | deterministic-test | status | evidence |
|---|---|---|---|---|---|---|---|---|---|
| `conditional_amplify/wounded` | `CharacterSkillSystemSettings.asset` | legal combination catalog | authored values `primary=0.35`, `secondary=0.5` | `CharacterSkillRuntimeEffects.ExecuteSkill` | recompute; not saved | live CharacterSkill model prompt and v17 export | below/equal/above threshold plus caster/target mismatch | connected | `CharacterProgressionDebugScenarios.RunAll(false)=true` |
| `conditional_amplify/critical` | `CharacterSkillSystemSettings.asset` | legal combination catalog | authored values `primary=0.7`, `secondary=0.25` | `CharacterSkillRuntimeEffects.ExecuteSkill` | recompute; not saved | live CharacterSkill model prompt and v17 export | below/equal/above threshold plus caster/target mismatch | connected | `CharacterProgressionDebugScenarios.RunAll(false)=true` |
| `CharacterSkillCombinationSemanticsFactory.Create` | `CharacterSkillCombinationSemantics.cs` | legal rule + combination + authored settings | projection of runtime branches; no new gameplay rule | prompt, scenario and catalog serializers | recompute; not saved | structured terms and Korean description | exact canonical validation and mutation rejection | connected | focused CharacterSkill regression PASS |
| `CreateCatalogCoverage` | `CharacterSkillCombinationSemantics.cs` | authored 24 modules/50 variants | same projection factory and authored legality | `characterSkill.semanticsCoverage` | recompute; not saved | catalog-only AI import | 202 entries, 50 pairs, five scopes, missing pair fail-loud | connected | v17 catalog audit PASS |
| `CharacterSkillPromptBuilder.BuildCandidatePacket` | `CharacterSkillGenerationService.cs` | live generated draft | current legal candidates | live model request | recompute per request | `semanticDefinitions` and `semanticKeys` | live packet equals exported public semantics | connected | focused CharacterSkill regression PASS |
| `NarrativeMechanicScenarioCharacterSkillSource` | Editor scenario source | named production producer/validator fixtures | production prompt and shared semantics projection | positive/negative scenario export | export-only | request/full-candidate semantics | 20 scenarios, 36 rules, 375 combinations, 0 missing semantics | connected | v17 raw export audit PASS |
| `NarrativeMechanicCatalogUnityAssetSource` | Editor catalog source | current authored asset capture | current catalog snapshot | `catalog.json` and packet parity | export-only | 24/50 catalog meaning coverage | canonical context order and scope coverage | connected | focused catalog contract PASS |
| `NarrativeMechanicCatalogExporter` | Editor exporter | create-only export command | source provenance and canonical serializers | README/schema/manifests/export files | immutable version directory | exact keys, enums and reproduction policy | independent capture byte identity | connected | Unity console sequence 38420 PASS |
| response DTO five-field contract | existing inference contracts | model response | unchanged ID/name/description/reason contract | existing validator | existing persistence policy unchanged | existing response adapter | illegal ID, budget and duplicate rejections retained | unchanged | pre-coverage V25 14/14 PASS; affected export tests rerun |

## Raw verification summary

- Latest Unity compilation: completed, `failed=false`, errors `[]`.
- `CharacterProgressionDebugScenarios.RunAll(false)`: `true`.
- `VerifyCharacterSkillPublicSemanticsContract`: reflection-invoked focused test returned `true` after base catalog coverage was added.
- `CompareIndependentExports(...).RequireByteIdentical()`: PASS; Unity console sequence `38420`.
- v17 delivery entries: 10; SHA-256 mismatches: 0.
- Export counts: positive 100, negative 14, continuity witnesses 6, EvolutionHistory 15, CharacterSkill 20.
- CharacterSkill export coverage: 36 rules, 375 combinations, 1089 semantic module entries, missing semantics 0, malformed conditional contracts 0.
- Affected scenario second rule: 12/12 options retain `conditional_amplify` and complete selected-target `<=` health-ratio terms.
- Catalog coverage: 202 representative behavior contexts, 24 modules, 50 variants, all five execution scopes.
- Mechanical combination ID set is byte-identical to v15; all 20 CharacterSkill scenario semantic hashes changed.
- `catalogHash`: `sha256:f1d1b2609548000577f6bc5020c3999189bd0d5fee6dd933d2eb68782c70e565`.
- `inputDigest`: `sha256:6defec1086d319ff905773110b9f9305dcde67f34817b2e9af94c0dac470dd30`.
- `delivery_manifest.json` SHA-256: `E4052870B6DE961FE345A64ECA93B19D2DF24D4D7BA746F1C15F244BCD2C061A`.
- `trainingEligible=false`, `humanApprovalClaimed=false`.
- Final knowledge-base rebuild: content/system validation failures `0/0`; freshness query
  returned the public semantics types from `services:character`.
- NOT_RUN: PlayMode, real model inference, SFT/DPO/GGUF, release promotion, AI workspace pin/import.
