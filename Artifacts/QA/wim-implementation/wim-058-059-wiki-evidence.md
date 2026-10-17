# WIM-058·059 위키 투영 보류 근거

조사·수정일: 2026-09-06  
범위: 아이템 기준 가격과 시설 건설 WU·BOM의 생성 경로만 다룬다. Unity 자산·C#의 수치와 생성된 content-db/KB/위키 JSON은 수정하지 않았다.

## 현재 작성 경로 확인

| WIM | 현재 권위에서 공개 모델까지의 경로 | 확인 결과 |
| --- | --- | --- |
| 058 | `ItemDefinitionSO.AuthoredUnitPrice` → fresh content-db `authored__unitPrice` → `tools/Wiki/generate_wiki_model.py:fact_rows`의 `기준 가격` | direct projection이다. 고정 가격표, 반올림, 배율 또는 재계산은 없으므로 writer 변경이 필요 없다. |
| 059 | `BuildingWorkAmountAbility.constructionWorkRequired`·`constructionMaterials[]` → `tools/Documentation/generate_content_database.py:reason_for` → fresh content-db `system_role`/`existence_reason` → `tools/Wiki/generate_wiki_model.py:safe_summary` | 기존 설명 생성기는 재료를 네 개로 잘라 `외 N개`를 붙였고, 위키는 authored description을 먼저 읽거나 요약을 500자로 잘라 완전한 건설 summary를 가릴 수 있었다. 전 BOM을 내보내고 `BuildingSO`의 생성 summary를 우선·무절단으로 투영하도록 수정했다. |

`constructionWorkRequired`는 `[InspectorName("건설 작업량")]`의 작성 float이며, 전역 기준서는 `1 WU=실제 작업 1초`로 단위를 정의한다 (`docs/game-design/whole-game-balance-baseline.md:1755`). 따라서 공개 설명은 값을 바꾸지 않고 `작업량 <작성값> WU`로 표기한다. BOM은 각 `ItemAmountDefinition`의 `(itemId, amount)`이며, 수량의 물리 단위는 해당 아이템 정의의 한 개다.

WIM-058의 가격 단위도 `unitPrice`의 **아이템 정의 한 개당 작성 정수값**이다. 현재 source가 통화 기호나 다른 환산 단위를 제공하지 않으므로 wiki는 `기준 가격 <값>`에 통화 표기를 발명하지 않는다.

## 적용한 최소 수정과 정적 증거

- `tools/Documentation/generate_content_database.py`의 `BuildingSO` 설명에서 `summarize_pairs(materials, 4)`를 `summarize_pairs(materials, len(materials))`로 바꿨다. 기존의 item ID·수량 추출과 다른 유형의 요약 제한은 바꾸지 않았다.
- 같은 설명의 `constructionWorkRequired`에만 검증된 `WU` 단위를 붙였다.
- `tools/Wiki/generate_wiki_model.py`는 `BuildingSO`에서 fresh content-db의 `system_role`/`existence_reason`을 authored description보다 먼저 읽고 500자에서 자르지 않는다. 따라서 content-db가 보존한 마지막 BOM pair가 public summary에서도 가려지거나 제거되지 않는다.
- 새 focused test `tools/Documentation/test_generate_content_database.py`는 다섯 재료 fixture를 `reason_for`에 통과시켜 `작업량 12.5 WU`, 다섯 번째 `material:fifth×5`, 그리고 `외 1개` 부재를 확인한다.

실행한 정적 검증:

```powershell
Set-Location Tools/Documentation
python -X utf8 -m unittest test_generate_content_database.py
```

결과: PASS (1 test). 별도 reader check는 600자를 넘는 `BuildingSO`의 generated `system_role`이 `safe_summary` 뒤에도 우선·무절단으로 남음을 확인했다. Unity 실행·컴파일·PlayMode는 수행하지 않았다.

staged source SHA-256: `tools/Documentation/generate_content_database.py` = `8BD2567412EADF43B393213E12DA6F1BC4852A3EE279D48A4F82F5300DA48283`; `tools/Documentation/test_generate_content_database.py` = `11E4CA7EB3F9C4FA560A28A63888CC702B562E319106A85065F932A261199564`; `tools/Wiki/generate_wiki_model.py` = `20DCDB169FA90DF6B982FD8A3B2242446C3983B5DD1DA595EABDF7C1794FB3D5`.

## coordinated fresh generation 뒤 전수 대조 절차

아래 전수 집합과 숫자는 과거 감사의 1,075/398/198/144를 재사용하지 않는다. 통합 작업자가 동기화와 content-db/KB 생성 직후 남긴 fresh manifest를 입력으로 삼고, 현재 publication filter를 통과한 행으로 매번 모집단을 계산한다.

1. `GenericItemDefinitionSO`와 `ResourceItemDefinitionSO`의 공개 대상마다 `(content_type, stable_id, slugify(stable_id), authored__unitPrice)` ledger를 만든다. 각 ledger 행은 public item entity의 ID·slug와 정확히 하나의 `기준 가격` fact를 가져야 하며, fact 문자열은 `authored__unitPrice`의 무변환 문자열과 같아야 한다. 0도 작성값이므로 누락시키지 않는다. 이 ledger에는 `unitPrice = 아이템 정의 1개당 작성 정수값`을 함께 기록한다.
2. 공개 대상 `BuildingSO`를 전수 열거하고, `BuildingWorkAmountAbility`가 있는 정의마다 `(building stable_id, constructionWorkRequired, [(itemId, amount), ...])` ledger를 만든다. WU는 원시 작성값 그대로 비교하고, BOM은 순서·item ID·수량을 모두 비교한다. 해당 ability가 없는 공개 BuildingSO는 WU/BOM 없음으로 별도 기록해 비용 0으로 추정하지 않는다.
3. fresh content-db의 BuildingSO description은 모든 ledger BOM pair를 각각 한 번 포함해야 한다. `외 N개`는 0건이어야 하며, 다섯 번째 이후 pair도 ID와 수량이 정확해야 한다.
4. `npm run model` 뒤 public facility summary는 같은 complete description을 title-display로만 치환한 결과여야 한다. 검증 ledger는 원본 item ID와 amount를 보존하고, public card에서는 대응하는 현재 공개 item title과 수량을 비교한다. 500자 절단, 누락, 중복, 순서 변경은 실패다.
5. 전수 ledger의 대상 집합·ID·단위·수량과 generated entity fact/summary를 함께 보관한 뒤 `npm run audit` 및 `npm run verify:determinism`을 통과해야 한다. 한 표본이나 generator 존재만으로는 WIM-058/059를 닫지 않는다.

## 생성 경계와 상태

- 현재 KB freshness는 이전 조사에서 stale였으므로 이 작업자는 content-db/KB/model을 재생성하지 않았다.
- 먼저 sole integration worker가 staged narrative JSON을 `InGameNarrativeTextCatalog.asset`으로 동기화한 뒤, 이번 Documentation generator 변경을 포함하여 content-db와 KB를 한 번만 생성해야 한다. 이 문서 변경은 그 한 번의 KB rebuild 전에 작업자에게 전달해야 한다.
- fresh model이 준비되면 같은 배치에서 WIM-058/059/060/061을 생성·감사한다. 생성 JSON을 직접 편집하지 않는다.

| WIM | 현재 상태 |
| --- | --- |
| 058 | writer mapping 확인 및 전수 대조 절차 준비됨. fresh content-db/KB/model과 전수 ledger 검증 전 OPEN. |
| 059 | full-BOM/WU source writer 수정 및 5+ fixture PASS. sole worker의 fresh generation, complete source-vs-public ledger, model audit·determinism 전 OPEN. |

## 2026-09-08 fresh source→public actual ledger

통합 작업자가 제공한 fresh content-db manifest(`docs_final/content-db/generation-manifest.json`, SHA-256 `9E1993BE340E7C07873228FD58F1144285289E1DAE3175FB0B0FA684608F50CD`, source digest `1e7d30fb88490c3e337915fc8b6a9dd523ca11e75994eac4281eff4b06ea5b20`)와 `npm run model`의 public entity를 대상으로, 출력 파일을 수정하지 않는 scoped parser를 한 번 실행했다. parser는 기존 `Tools/Wiki/generate_wiki_model.py`의 `load_rows`·`classification_reason`·`public_title`·`fact_rows`·`safe_summary`와 같은 publication rule만 사용했다.

재현 규칙은 다음과 같다. 058은 현재 publication filter를 통과한 `GenericItemDefinitionSO`·`ResourceItemDefinitionSO`마다 public item entity의 `기준 가격` fact 하나를 `authored__unitPrice`와 문자열 그대로 비교했다. 059는 같은 filter의 `BuildingSO` 중 건설 WU/BOM 설명이 있는 행마다 public summary를 source `safe_summary`와 완전 문자열 비교했고, source BOM에서 `재료 … 외 N개` 축약도 별도로 세었다. 이 검사는 새 테스트 파일·생성물·Unity asset을 만들거나 바꾸지 않았다.

원시 결과:

```json
{
  "058": { "population": 1076, "mismatches": 0 },
  "059": { "population": 398, "mismatches": 0, "bom_abbreviations": 0 }
}
```

따라서 058의 1,076개 공개 아이템 가격과 059의 398개 공개 시설 WU·전체 BOM은 이 fresh entity ledger에서 모두 일치했다. 과거의 1,075/198 또는 347/144 표본 수는 이 결과에 사용하지 않았다. 이 항목은 entity-level 값 대조 기록이며, 아래 전역 gate의 실패 때문에 WIM 종료 판정은 아니다.

`npm run verify:determinism`은 이후 slug registry write에서 `InvalidArgument`로 중단되어 manifest를 남기지 못했다. 원인 및 복구는 별도 진단 범위이며, 이 문서는 그 실패 뒤 생성물을 수정하거나 재생성하지 않았다.

## 2026-09-08 D01 결정론 격리·출력 복구 검증

`tools/Wiki/generate_wiki_model.py`의 `destination` 경로를 격리 생성 계약으로 고정했다. `destination`이 지정된 호출은 지정된 data tree만 만들며 canonical spoiler tree, `content/slug-registry.csv`, `game-version.json`, 전역 `registry.json`을 쓰지 않는다. `tools/Wiki/verify_wiki_determinism.py`는 서로 다른 두 `TemporaryDirectory`를 destination으로 전달하고 기존 `content_digest` 두 개를 그대로 비교한다. `destination=None`인 `npm run model`의 출판 동작은 유지했다.

격리 전후 canonical 집합은 `data/` 전체, version spoiler tree, slug registry, version metadata, global registry의 파일 경로·길이·SHA-256·mtime tick을 포함한다. 단일 검증 실행의 원시 결과는 다음과 같다.

```text
py_compile exit=0
verify:determinism exit=0
{"status":"deterministic","game_version":"0.0.1v","content_digest":"8307f8b6ad8f95477de69f78d883fa9cd69e55d826d1a22a31e4c53a3908a9eb"}
canonical files before/after=8894/8894
canonical bytes digest before/after=B64C6D6B7C196B01B629E0927D477AE231A93E4CC1BC9F9F9271166E59AF2877/B64C6D6B7C196B01B629E0927D477AE231A93E4CC1BC9F9F9271166E59AF2877
canonical bytes+mtime digest before/after=D71423E4ABB4390C7117B9599E203AB33D8931B1D6A1C2E46E1258AF4FE497F5/D71423E4ABB4390C7117B9599E203AB33D8931B1D6A1C2E46E1258AF4FE497F5
changed paths=0; manifest before/after verify=missing/missing; temporary-directory residue=0
```

격리 검증 통과 뒤 손상 실행이 지운 manifest를 정상 출판 경로 한 번으로 복구했다. `npm run model`은 `published_record_count=2906`, `published_relation_count=3848`, `content_digest=8307f8b6ad8f95477de69f78d883fa9cd69e55d826d1a22a31e4c53a3908a9eb`으로 종료 코드 0이었다. 복구된 `data/manifest.json`은 483 bytes, SHA-256 `0FEAF36FFCA5CCB1ACA03B81470E1C76832159F7C93BDB75D6A58278AFCBB045`이다.

같은 출력에 `npm run audit`을 한 번 실행했다. `validate:model`은 2,906 entities, document authority는 valid, source coverage는 handbooks 9 / public sections 101 / checklist sections 19, Markdown은 5/5 PASS, Astro는 61 files에서 errors 0 / warnings 0 / hints 2로 종료 코드 0이었다. source SHA-256은 generator `41CCC0DE401846BDEB94F8E8B68215BC533AF7D1BED5171EF7AA9876852ABF6A`, verifier `394CA1038626A4AD80C57CF324831BA96FF4617E61D004365EDF2D06897866DD`다. 이로써 058·059의 기존 1,076/398 actual ledger에 필요한 model audit·결정론 gate가 통과했으며 root 종료 판정 준비 상태다.
