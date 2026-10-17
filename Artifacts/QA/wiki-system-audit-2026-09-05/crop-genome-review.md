# 작물 유전체·종자 계승 후속 감사

상태: 정적 대조 완료. Unity 실행·UI 시연·저장 왕복 검증은 수행하지 않았다. 게임 코드·자산·공개 위키는 수정하지 않았다.

## 판정 요약

- 작성 `CropGenomeDefinitionSO`는 32개, 고유 genome ID 32개, 대상 작물 12개다. 모든 작성 asset은 `GameDomainContentCatalog.asset`에 한 번씩 연결된다.
- 공개 `data/entities/nature/genome-*.json`도 32개라 페이지 누락은 없다. 그러나 32개 모두 `facts: []`, `relations: []`이고 title이 `genome_*` 내부 이름이다.
- 각 작성 유전체는 작물, 품종명, 설명, 저작 revision, tradeoff tag 0~2개, 6개 diploid locus를 가진다. 공개 entry는 설명만 summary로 옮기고 나머지를 투영하지 않는다.
- 가이드 `food-and-ecology.md`는 품종이 환경·수확·종자 회수의 tradeoff를 가질 수 있다는 한 문장만 제공한다. 실제 phenotype 공식, 종자 세대·병원체, 돌연변이, 윤작·토양·질병 결합, cultivar 보존 상한과 수확 트랜잭션은 없다.
- `cultivarName`과 `tradeoffTags`는 정의 검증 밖의 C# 소비처가 확인되지 않았다. 실제 성장·생존·질병·수확은 6 locus에서 계산한 phenotype을 사용한다. tag의 문구를 별도 런타임 효과로 설명하면 안 된다.

## 모집단과 공개 노출

| 항목 | 작성/공개 수 | 결과 |
| --- | ---: | --- |
| 작성 유전체 | 32 | ID 고유32, 작물12, 각 locus6 |
| 공개 유전체 entry | 32 | slug 대응32 |
| 공개 facts | 0 | 32개 모두 빈 목록 |
| 공개 relations | 0 | 32개 모두 빈 목록 |
| 공개 title | 32 | 모두 `genome_*` 내부형 |
| tradeoff tag 수 | 0개12 / 1개11 / 2개9 | 작성 asset에만 존재 |

아래 표의 여섯 숫자는 `저온/고온/성장/수확/질병저항/종자` locus에서 두 allele의 평균이다.

| genome ID | crop ID | tradeoff tags | 6-locus 평균 |
| --- | --- | --- | --- |
| genome:bloodleaf:base | crop:bloodleaf | 없음 | 0/0/0/0/0/0 |
| genome:bloodleaf:heat | crop:bloodleaf | cost:disease-resistance | 0/2/0/1/-2/0 |
| genome:cave-mushroom:base | crop:cave-mushroom | 없음 | 0/0/0/0/0/0 |
| genome:cave-mushroom:dry | crop:cave-mushroom | cost:seed-yield | 1/1/0/0/1/-2 |
| genome:dreamleaf:base | crop:dreamleaf | 없음 | 0/0/0/0/0/0 |
| genome:dreamleaf:cold | crop:dreamleaf | cost:growth-time | 2/0/-2/1/0/0 |
| genome:ember-cotton:base | crop:ember-cotton | 없음 | 0/0/0/0/0/0 |
| genome:ember-cotton:bulk | crop:ember-cotton | tradeoff:quality, role:bulk-fiber | -1/-1/2/2/-1/1 |
| genome:ember-cotton:climate | crop:ember-cotton | tradeoff:yield, role:fine-fiber | 0/2/-1/-1/2/0 |
| genome:ember-root:base | crop:ember-root | 없음 | 0/0/0/0/0/0 |
| genome:ember-root:cold | crop:ember-root | cost:fertility-consumption | 2/0/0/1/0/0 |
| genome:ember-root:heavy | crop:ember-root | cost:fertility-consumption, cost:growth-time | 0/0/-1/2/0/0 |
| genome:frost-flax:base | crop:frost-flax | 없음 | 0/0/0/0/0/0 |
| genome:frost-flax:bulk | crop:frost-flax | tradeoff:quality, role:bulk-fiber | -1/-1/2/2/-1/1 |
| genome:frost-flax:climate | crop:frost-flax | tradeoff:yield, role:fine-fiber | 2/0/-1/-1/2/0 |
| genome:mire-reed:base | crop:mire-reed | 없음 | 0/0/0/0/0/0 |
| genome:mire-reed:bulk | crop:mire-reed | tradeoff:quality, role:bulk-fiber | -1/-1/2/2/-1/1 |
| genome:mire-reed:climate | crop:mire-reed | tradeoff:yield, role:fine-fiber | 2/0/-1/-1/2/0 |
| genome:moonflower:base | crop:moonflower | 없음 | 0/0/0/0/0/0 |
| genome:moonflower:heat | crop:moonflower | cost:disease-resistance | 0/2/0/1/-1/0 |
| genome:night-grape:base | crop:night-grape | 없음 | 0/0/0/0/0/0 |
| genome:night-grape:cluster | crop:night-grape | cost:seed-yield | 0/0/0/2/0/-2 |
| genome:night-grape:cold | crop:night-grape | cost:growth-time | 2/0/-1/1/0/0 |
| genome:shade-fiber:base | crop:shade-fiber | 없음 | 0/0/0/0/0/0 |
| genome:shade-fiber:heat | crop:shade-fiber | cost:seed-yield | 0/2/0/0/1/-1 |
| genome:shade-fiber:long | crop:shade-fiber | cost:temperature-range | -1/-1/0/2/0/0 |
| genome:spore-hemp:base | crop:spore-hemp | 없음 | 0/0/0/0/0/0 |
| genome:spore-hemp:bulk | crop:spore-hemp | tradeoff:quality, role:bulk-fiber | -1/-1/2/2/-1/1 |
| genome:spore-hemp:climate | crop:spore-hemp | tradeoff:yield, role:fine-fiber | 2/0/-1/-1/2/0 |
| genome:twilight-grain:abundant | crop:twilight-grain | cost:disease-resistance | 0/0/0/2/-2/0 |
| genome:twilight-grain:base | crop:twilight-grain | 없음 | 0/0/0/0/0/0 |
| genome:twilight-grain:frost | crop:twilight-grain | cost:growth-time | 2/0/-1/0/1/0 |

평균값 분포는 저온 `-1:5, 0:19, 1:1, 2:7`, 고온 `-1:5, 0:22, 1:1, 2:4`, 성장 `-2:1, -1:7, 0:20, 2:4`, 수확 `-1:4, 0:15, 1:5, 2:8`, 질병저항 `-2:2, -1:5, 0:18, 1:3, 2:4`, 종자 `-2:2, -1:1, 0:25, 1:4`다.

## phenotype 변환과 실제 소비

각 locus의 두 allele 평균을 `m`이라 할 때 현재 변환은 다음과 같다.

| phenotype | 공식 | 실제 소비 |
| --- | --- | --- |
| 저온 허용 | `clamp(m×2.5, -5, 5)`도 | 작물 최저 생존온도를 그만큼 낮춤 |
| 고온 허용 | `clamp(m×2.5, -5, 5)`도 | 작물 최고 생존온도를 그만큼 높임 |
| 성장 배율 | `clamp(1+m×0.08, 0.84, 1.16)` | 실내·실외 성장 배율 합성 |
| 수확 배율 | `clamp(1+m×0.05, 0.90, 1.10)` | 물리 수확물 수량 합성 |
| 질병 위험 배율 | `clamp(1-m×0.12, 0.76, 1.24)` | 일일 질병 압력과 감염 확률 |
| 종자 보너스 | `clamp(round(m×0.5), -1, 1)`개 | 수확 반환 종자 수 |

`CropPlotRuntime`은 성장 때 phenotype을 실내/실외 성장 권위에 전달하고, 날짜가 바뀔 때 저온·고온 허용값으로 치사 범위를 조정한 뒤 질병 위험 배율을 생태 런타임에 전달한다. 수확 서비스는 생태 수확 배율을 작물 기본 수량·시설 출력·작업자·극단 특성·토양 진단과 합성해 실제 물리 수량을 만든다.

## 종자·윤작·질병·돌연변이

- 최초 월드는 작물 12종의 base genome 종자를 각각 4개씩 실제 물리 item stack으로 한 번만 지급한다.
- 종자 lot은 crop ID, cultivar genome ID, generation, pathogen load를 item component에 저장한다. 파종은 종자와 genome의 작물 일치를 검사한다.
- 같은 family를 연작하면 pest +15, disease +10이고 윤작하면 pest -10, disease -5다. 종자 pathogen load의 25%가 밭 disease pressure에 더해진다.
- 매일 기존 disease pressure에 `4×DiseaseRiskMultiplier`가 더해지고, 감염 확률은 `(diseasePressure/500)×DiseaseRiskMultiplier`다. 치사 상태 3일 또는 pest 85 이상에서 일일 25%로 작물이 죽을 수 있다.
- 수확 배율은 pest(`>=60:0.70`, `>=30:0.90`), 같은 family 연작0.85, fertility의0.55~1 보간, 부모 genome 수확 배율, disease의 `1-pressure×0.003`을 곱한다.
- 자식 genome은 6 locus 각각 1% 확률로 변이한다. 변이 locus에서 A/B allele 하나와 ±1 방향을 각각 50%로 고르고 `[-2,2]`로 제한하며 경계에서는 방향을 뒤집는다.
- 기본 반환 종자는 `clamp(2+floor(U×3)+SeedYieldBonus, 2, 4)`개이고, 종자선별 연구가 활성화되면 최종 수량에 +1한다. 반환 lot의 pathogen load는 수확 시 disease pressure의 50%다.
- 수확 후 fertility는 15 감소하고 현재 family가 이전 family로 넘어간다.

## cultivar 보존·저장·재시도

- 작물당 활성 cultivar 상한은 12개, 미참조 frozen cultivar 상한은 32개다.
- 활성 상한을 넘으면 generation/ID 순으로 오래된 후보를 frozen으로 옮긴다. 밭, 외부 물리 종자, 준비된 수확 영수증이 참조하는 genome은 pruning하지 않는다.
- 저장/복원은 최초 종자 지급 여부, 밭, 활성·동결 cultivar, 준비된 수확을 보존하며 중복 ID와 끊어진 참조를 거부한다.
- 수확 prepare는 산출 배율, 자식 genome, 종자 수·lot, 상태 fingerprint를 고정한다. commit은 멱등적이고 밭 상태 drift를 거부하므로 재시도에서 유전 결과를 다시 추첨하지 않는다.
- 고정된 종자 lot은 생산 출력 line의 `SeedLotItemStateCodec` component로 전달되어 실제 물리 종자 stack과 저장 preflight까지 이어진다.

## 문서 보완 판정

1. 32개 유전체 entry에 사람용 품종명, 대상 작물, 6 locus 또는 파생 phenotype, tradeoff tag의 메타데이터 성격을 표시해야 한다. 내부 `genome_*` title과 빈 facts/relations를 그대로 두면 품종 비교가 불가능하다.
2. `food-and-ecology`에 phenotype 공식, 실제 성장·온도·질병·수확 연결, 종자 lot·세대·병원체, 윤작, 돌연변이, 반환 종자, cultivar 상한/pruning, prepare/commit 저장·재시도 규칙을 설명해야 한다.
3. `tradeoffTags`가 별도 효과를 발동한다고 쓰지 않는다. 현재 효과 권위는 loci에서 파생한 phenotype이다.

## 직접 근거

- `Assets/Scripts/Models/Economy/Content/CropGenomeDefinitionSO.cs`
- `Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs`
- `Assets/Scripts/Services/Economy/CropEcologyRuntime.cs`
- `Assets/Scripts/Services/Economy/CropPlotRuntime.cs`
- `Assets/Scripts/Services/Economy/CropHarvestOutputRules.cs`
- `Assets/Scripts/Services/Economy/CropPhysicalTransactionOutbox.cs`
- `Assets/Scripts/Services/Infrastructure/Save/DungeonAggregateReferencePreflight.cs`
- `Assets/Resources/SO/Economy/CropGenomes/`
- `Assets/Resources/SO/V20/Ecology/Cultivars/`
- `wiki/game-versions/0.0.1v/data/entities/nature/genome-*.json`
- `wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md`

KB `CropGenome genome variant gene seed yield growth disease runtime` 질의는 stale, failures2429, 반환0행, exit1이었다. 전체 재빌드는 하지 않고 직접 원본으로 판정했다.
