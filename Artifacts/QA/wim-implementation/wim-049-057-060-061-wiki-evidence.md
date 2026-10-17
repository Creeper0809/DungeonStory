# WIM-049·057·060·061 위키 정정 근거

조사·수정일: 2026-09-06  
범위: 공개 가이드 3개, `tools/Wiki/generate_wiki_model.py`, 나팔의 검수 가능한 한국어 narrative 원문 및 이 근거 문서를 수정했다. Unity C#, Unity 에셋, 전역 계획과 생성 KB/content-db는 읽기 전용으로 유지했다.

## 조사 경계와 최신성

- 첫 KB 조회: `python -X utf8 Tools/Documentation/query_knowledge_base.py --query "WIM049 WIM057 WIM060 WIM061" --area documents --limit 12 --format markdown`
- 결과: `stale`, failures 22. content-db source digest `b3902a5c663e26ff1f7a9922645caa2ed533d54e7bbdd85761e6d91b97c28b6e`, knowledge-base source digest `0167d00e4e352023c25583435640e7b8213a803b25c92c0fd56f26d26335f257`.
- 따라서 KB·content-db를 재생성하거나 그 생성물을 현재 근거로 사용하지 않았다. `wiki`의 `npm run model`도 실행하지 않았다. 이 생성기는 stale 입력을 읽고 `game-versions/0.0.1v/data/` 전체를 교체하므로, 생성 카드 변경은 아래의 staged writer와 공식 통합 순서까지 보류한다.
- 읽은 감사: `facility-growth-wiki-runtime-review.md`, `invasion-wiki-runtime-review.md`, `security-facility-signal-horn-review.md`, `climate-mechanics-review.md`, `wim-evidence-and-dedup-review.md`.

## 정정 근거

| WIM | 정정한 공개 주장 | 현재 원본·소비자 확인 | 수정 가이드 |
| --- | --- | --- | --- |
| 049 | 원정의 `requiredPower`는 UI에 원정대 전투력과 함께 보이는 권장값이며, 적 능력치를 목표값에서 자동 역산하지 않는다. 조우 선택은 목표 `campaignOrder`, 경로 깊이와 보스 여부에 따른 authored 조우 후보를 사용한다. 전투 능력치 계수는 캠페인 기준 전투력 `10/16/32/42/60/85`에서 계산한다. | `EnemyEncounterFactory.SelectEncounter`·`EncounterCampaign`, `OffenseCampaignCombatBalanceRules.GetCampaignReferencePower`·`CalculateStatScale`, `OffenseExpeditionPanel`의 `권장 {target.requiredPower}` 표시. | `invasions-and-defence.md` |
| 057 | 합성은 주문·WIP·준비 출력·투입/출력 물품을 비우게 하지 않는다. 참여 생산 상태를 결과 시설로 보존 이관하며, 준비·확정 실패는 시작 전 거절하거나 원래 상태로 rollback한다. | `FacilitySynthesisRuntime`의 retarget begin/commit/rollback 경계와 `ProductionFacilityRetargetTransaction.TryBegin`·`TryCommit`·`TryRollback`; transaction은 모든 participant plan을 검증하고 source epoch를 닫을 때까지 유지한다. | `facility-growth.md` |
| 060 | `annualAmplitudeC`는 평균 기온의 위·아래 계절 진폭이고, 날씨·일일 잡음을 제외한 최고-최저 연교차는 진폭의 2배다. 다섯 기후의 값과 계산 입력을 표로 분리했다. | `ClimateZoneDefinitionSO.annualAmplitudeC` → `ClimateZoneDefinition.AnnualAmplitudeC` → `ClimateAggregateState.GetOutdoorTemperature`, 여기서 `AnnualAmplitudeC * sin(...)`을 평균 기온에 더한다. | `weather-seasons-and-environment.md` |
| 061 | 가동 보안 시설에 나팔 1개가 준비되면 침입자 진입까지의 시간이 +6초여서 주민이 대비할 여유를 얻고, spawn 확정시에만 내구도 1을 함께 소비한다. 현재 consumer는 침입 신호뿐이며 화재·소방 행동의 선행 조건이나 consumer는 없다. | `InvasionDirectorRuntime.FindOperational(FacilityCapabilityKind.Security)`·`TryEnsureReady`·`TryCommitRally`, `InvasionSignalHornDurableEquipmentRuntime`, `PhysicalItemRuntimeConsumerCatalog`의 `runtime:invasion-watch-signal`. | `invasions-and-defence.md` |

## 직접 확인한 원본 digest

| 원본 | SHA-256 |
| --- | --- |
| `Assets/Scripts/Services/Offense/EnemyEncounterFactory.cs` | `25FCD61FA3FBC07F624E588D1063857435A6FF0EAC32EAE87291E220CA43BCF9` |
| `Assets/Scripts/Models/Offense/Core/OffenseCampaignCombatBalanceRules.cs` | `EC2380C09031EE90BDE448A418D8E544BE090EFF5AA8BB3D03D807D9586EE3B0` |
| `Assets/Scripts/Services/Offense/OffenseExpeditionPanel.cs` | `D2F704C1334ED0204AB89B1A706B6B4AE58D47B2462F800716DB3A6F672AB426` |
| `Assets/Scripts/Services/Synthesis/FacilitySynthesisRuntime.cs` | `FCB62FC36E262D621404FFFD0565C5C19173C470441A2B9E4DF26F7BBACA93EF` |
| `Assets/Scripts/Services/Economy/ProductionFacilityRetargetTransaction.cs` | `B91C0A8386C65700F1C4EF5D662A23C1846EFE65E335AF5CADCAE62C35781FEE` |
| `Assets/Scripts/Models/CoreSession/ClimateDomain.cs` | `48AB2F6479DFAE0884CCDC254908BCF7DF059BCA6A3A2729F0050BB198965A66` |
| `Assets/Scripts/Content/ClimateZoneDefinitionSO.cs` | `45674DC35C377B0777CBB52007C342E30325025FAA2EA132191C4871F1CE651E` |
| `Assets/Scripts/Services/Invasion/InvasionDirectorRuntime.cs` | `34E38EB24664F076C66C2FE3478BC3B682F23EE727678FA72231853B1592191C` |
| `Assets/Scripts/Services/Invasion/InvasionSignalHornDurableEquipmentRuntime.cs` | `570E0916ADCCDBFAE829BB8ACF0BD45E832F4C585D62FB0E2B841B1F10364F53` |
| `Assets/Scripts/Models/Items/Core/PhysicalItemRuntimeConsumerCatalog.cs` | `48CE15E36721F64861DB847E3262DC21255A870A0F30A3924CB2601A8DED400B` |

## 생성 모델 정정 stage와 통합 순서

- `tools/Wiki/generate_wiki_model.py`에 `climate_zone_summary`를 추가했다. `ClimateZoneDefinitionSO`는 CSV의 구형 `연교차` prose를 재사용하지 않고, `authored__annualAmplitudeC`로 **계절 기온 진폭**과 `2 × 진폭`의 날씨·일일 잡음 제외 최고-최저차를 생성한다.
- `docs/game-design/content/item-in-game-descriptions.ko.json`의 `tool:watch-signal-horn` 설명과 `lore_sentence`를 침입 진입 전 대비 신호로 정정했다. 가동 보안 시설에서 침입자 진입까지의 시간이 늘어나 주민이 대비한다는 효과를 명시했고, 화재·날씨 확인 주장은 제거했으며 기존 `practice:harpy-chorus` lore anchor를 보존했다.
- `Assets/Resources/SO/InGameNarrativeTextCatalog.asset`은 Unity 에셋 소유 범위라 수정하지 않았다. Sol/통합 작업자가 저장소 루트에서 실행할 정확한 승인 명령은 `python -X utf8 Tools/Documentation/sync_item_narratives.py --sync --check`이다. 이 명령이 검수 JSON의 1,075개 문장을 runtime catalogue로 동기화하고 일치 여부를 검사한다.
- 그 단일 asset sync 뒤 통합 작업자가 KB를 한 번 재생성하면, 이 작업자는 `npm run model`, `npm run audit`, `npm run verify:determinism`으로 `data/entities/nature/climate-*.json`와 `data/entities/item/tool-watch-signal-horn.json`을 공식 생성 경로로 갱신·검증한다. 생성 JSON은 직접 수정하지 않았다.
- staged source SHA-256: `tools/Wiki/generate_wiki_model.py` = `20DCDB169FA90DF6B982FD8A3B2242446C3983B5DD1DA595EABDF7C1794FB3D5`; `docs/game-design/content/item-in-game-descriptions.ko.json` = `1C1E5E8AA95ACD0DF97C33357C37FC53E41DA6A8CAB2E7C47016737B3D473F1A`; sync 전 runtime catalog = `747460801D5FE3E388C78CC25063669650FB39D674FC6E1F78E14DA189F769DA`.
- 위키 가이드 Markdown은 `LIVE_CONTENT.md`에 정의된 직접 제공 원본이며, WIM-049/057/060/061 설명은 현재 C# 실행 경로와 일치한다.
- 밸런스 영향 없음: 숫자·BOM·진행·실행 경로는 바꾸지 않았고, 현재 소스의 의미를 가이드에 반영했다. Unity 실행·컴파일·PlayMode는 범위 밖이라 수행하지 않았다.

## 검증

- `git diff --check -- wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md wiki/game-versions/0.0.1v/content/guides/facility-growth.md wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md` — PASS.
- `npm run audit` (wiki): `validate:model` PASS (entities 2905), `validate:authority` PASS, `validate:coverage` PASS (handbooks 9 / public sections 101 / checklist 19), `test:markdown` PASS (5/5), `astro check` PASS.
- `validate:model`의 navigation/link integrity 및 Markdown test의 전 버전 가이드 fence 검증을 함께 통과했다. 새 링크를 추가하지 않아 별도 URL 변경은 없다.
- staged writer 정적 확인: 다섯 `ClimateZoneDefinitionSO` row가 각각 `14→28`, `5→10`, `16→32`, `12→24`, `8→16`의 진폭/최고-최저차 summary를 만든다. `tool:watch-signal-horn` authority prose는 화재 문구 없이 lore sentence 종료·quality error 0을 통과했고, 침입자 진입 전 주민 대비 효과를 명시한다. 모델 생성 후 최종 audit·결정론 검증은 위 통합 순서의 대기 항목이다.

## 1차 검토 준비도

| WIM | 상태 |
| --- | --- |
| 049 | 1차 검토 준비됨 — 공개 가이드의 자동 역산 주장을 제거하고 authored 조우와 권장값을 분리했다. |
| 057 | 1차 검토 준비됨 — 플레이어용 문구로 진행 주문·WIP·물품의 보존 이관과 실패 복구를 설명한다. |
| 060 | 공개 가이드 및 climate-card writer stage 완료. 공식 model 생성·audit 전에는 OPEN이다. |
| 061 | 공개 가이드 및 narrative authority stage 완료. Sol asset sync와 공식 model 생성·audit 전에는 OPEN이다. |

## 2026-09-08 fresh source→public actual ledger (060·061)

통합 작업자가 제공한 fresh content-db manifest(`docs_final/content-db/generation-manifest.json`, SHA-256 `9E1993BE340E7C07873228FD58F1144285289E1DAE3175FB0B0FA684608F50CD`, source digest `1e7d30fb88490c3e337915fc8b6a9dd523ca11e75994eac4281eff4b06ea5b20`)와 `npm run model` public entity를 대상으로, 출력 파일을 수정하지 않는 scoped parser를 한 번 실행했다. parser는 기존 `Tools/Wiki/generate_wiki_model.py`의 publication filter 및 `safe_summary`를 그대로 사용했다.

060은 publication filter를 통과한 다섯 `ClimateZoneDefinitionSO`의 public summary를 source `safe_summary`와 완전 문자열 비교했다. 061은 `docs/game-design/content/item-in-game-descriptions.ko.json`의 `tool:watch-signal-horn` narrative를 public item entity `in_game_description`과 비교하고, 침입 전 집결 문구 존재 및 화재 문구 부재를 확인했다. 실행 중 새 출력·테스트·Unity asset은 만들거나 수정하지 않았다.

원시 결과:

```json
{
  "060": { "population": 5, "mismatches": 0 },
  "061": { "population": 1, "mismatches": 0 }
}
```

다섯 기후의 계절 진폭/연교차 설명과 나팔 한 항목의 침입 집결 전용 narrative는 모두 fresh public entity와 일치했다. 이는 entity-level 값 대조 기록이다. 뒤이은 `npm run verify:determinism` slug registry write 실패로 manifest가 남지 않아, 전역 audit·결정론 gate 및 WIM 종료 판정은 OPEN으로 유지한다. 이 문서는 해당 출력 트리를 복구하거나 재생성하지 않았다.

## 2026-09-08 D01 결정론 격리·최종 위키 감사

결정론 검증기는 두 `TemporaryDirectory` destination을 사용하고, destination 생성은 canonical spoiler/slug/version/global-registry를 쓰지 않도록 격리했다. 단일 실행에서 `py_compile`과 `npm run verify:determinism`이 종료 코드 0으로 통과했고, 결과 digest는 `8307f8b6ad8f95477de69f78d883fa9cd69e55d826d1a22a31e4c53a3908a9eb`이다. canonical 8,894개 파일의 bytes digest `B64C6D6B7C196B01B629E0927D477AE231A93E4CC1BC9F9F9271166E59AF2877`와 bytes+mtime digest `D71423E4ABB4390C7117B9599E203AB33D8931B1D6A1C2E46E1258AF4FE497F5`는 실행 전후 동일했고 changed path는 0개였다.

그 뒤 `npm run model`을 한 번 실행해 이전 실패가 지운 manifest를 복구했다. 출판 결과는 entities 2,906 / relations 3,848 / 같은 content digest였고, `data/manifest.json` SHA-256은 `0FEAF36FFCA5CCB1ACA03B81470E1C76832159F7C93BDB75D6A58278AFCBB045`다. 이어 실행한 기존 `npm run audit`의 원시 요약은 `validate:model` valid(2,906), authority valid, coverage valid(9/101/19), Markdown 5/5 PASS, Astro errors 0 / warnings 0 / hints 2, 전체 종료 코드 0이다. temporary-directory residue는 0개다.

최종 source SHA-256은 `tools/Wiki/generate_wiki_model.py` `41CCC0DE401846BDEB94F8E8B68215BC533AF7D1BED5171EF7AA9876852ABF6A`, `tools/Wiki/verify_wiki_determinism.py` `394CA1038626A4AD80C57CF324831BA96FF4617E61D004365EDF2D06897866DD`다. 이 결과는 기존 060의 5개 climate summary 및 061의 1개 horn narrative actual ledger와 동일한 fresh snapshot을 검증하므로, 두 항목의 model audit·결정론 gate는 root 종료 판정 준비 상태다.
