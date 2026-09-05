# DungeonStory 수치 설계 권위

이 문서는 현재 콘텐츠의 수치값, 정적 계산, 빌드별 목표 곡선을 하나의 설계 기준으로 묶는다. 수치값은 현재 직렬화 에셋에서 다시 읽은 개별 직접 수치 원장이 소유하며, `docs_final/content-db`는 stable ID와 구조 관계를 찾는 전수 스냅샷으로만 사용한다. 이 문서는 어떤 값이 최종값인지, 어떤 비교를 통과해야 하는지, 값 하나를 바꿨을 때 어디까지 다시 판정해야 하는지를 정한다.

[수치 설계 완결성 기록](numeric-finalization-closure.md)은 전수 정의의 직접 수치 원장 연결과 정적 조정 결과를 집계한다.

## 수치의 위치와 우선순위

| 순위 | 문서 | 맡는 범위 |
|---:|---|---|
| 1 | [콘텐츠 수치 인덱스](content-plans/balance-numeric-authority.csv) | 콘텐츠 종류별 분모, 정확한 값 위치, 필수 필드와 비교 축 |
| 2 | [아이템 수치 원장](content-plans/items/item-numeric-ledger.csv) | 현재 직렬화 ItemDefinition 에셋 1,074개의 kg·스택·가격·판매율 |
| 3 | [생산 수치 원장](content-plans/production/production-numeric-ledger.csv) | 현재 직렬화 ItemDefinition·ProductionRecipe 에셋과 생산 계약을 결합한 355개 공정의 BOM·WU·시간·확률·kg·가격 |
| 4 | [연구 수치 원장](content-plans/research/research-numeric-ledger.csv) | 현재 직렬화 ResearchProject 에셋 180개의 연구 WU·동시 연구자·선행·시설·직접 해금 수 |
| 5 | [시설 수치 원장](content-plans/facilities/facility-numeric-ledger.md) | 현재 직렬화 BuildingSO 에셋 419개의 건설 BOM·WU·공간·유지·해체·처리량 |
| 6 | [조우 수치 원장](content-plans/combat/encounter-numeric-ledger.md) | 현재 직렬화 OffenseEncounterSO 에셋 36개의 편성·배율·목표·보상 단가 |
| 7 | [적 수치 원장](content-plans/combat/enemy-numeric-ledger.csv) | 현재 EnemyArchetypeDefinitionSO 36개의 전투·장비·전술·보상 수치 |
| 8 | [전투 장비 수치 원장](content-plans/items/combat-equipment-numeric-ledger.md) | 현재 장비·재질 에셋과 ItemDefinition을 결합한 61개 장비의 물리 BOM·WU·전투 수치 |
| 9 | [의료 시술 수치 원장](content-plans/medical/procedure-numeric-ledger.md) | 현재 SurgicalProcedureSO 47개의 WU·위험·재료 BOM·kg·단가 |
| 10 | [질병 수치 원장](content-plans/medical/disease-numeric-ledger.csv) | 현재 DiseaseDefinitionSO 16개의 감염·중증도·기간·예방·만성 수치 |
| 11 | [작물 수치 원장](content-plans/production/crop-numeric-ledger.md) | 현재 CropDefinitionSO 12개의 종자·수확 kg·가격·수확량·WU·물·시간·환경 조건 |
| 12 | [종족·특성 수치 원장](content-plans/context/population-numeric-ledger.md) | 현재 종족·특성 에셋 123개의 소비·환경·기능·효과 계수 |
| 13 | [사건 수치 원장](content-plans/events/event-numeric-ledger.md) · [사건 대응 노동 원장](content-plans/events/event-response-labor-model.md) | 현재 직렬화 V20 사건의 요구 BOM·kg·단가·기한·효과와 172개 사건 대응·파견·복구 WU |
| 14 | [생산 물리 BOM 원장](content-plans/production/production-physical-bom-ledger.csv) | 물·폐수·고형 입력·산출 질량이 수정된 16개 공정의 최종 물리값 |
| 15 | `docs_final/content-db/fields/**/*.csv` | stable ID·종류·문자열 필드·구조 참조를 찾는 전수 스냅샷 |
| 16 | [전략 포트폴리오 단계 예산](content-plans/portfolio-stage-budgets.csv) | 여섯 빌드가 각 시점에 실제로 감당해야 할 노동·비축·위험 예산 |
| 17 | [포트폴리오 수치 곡선](content-plans/portfolio-numeric-curve.md) | 여섯 빌드의 누적 운영·주력·연구·예비 WU와 회복 한계 |
| 18 | [전투·사건 판정선](content-plans/combat-and-event-bands.csv) | 조우·사건·계약 보상과 손실의 등급별 허용 범위 |
| 19 | [포트폴리오 수치 조립 원장](content-plans/portfolio-numeric-assembly.md) | 여섯 경로의 실제 연구 선행·시설 BOM·공간·WU와 기후별 정적 지배 판정 |
| 20 | [포트폴리오 압력 단계 매트릭스](content-plans/portfolio-pressure-stage-model.md) | 3·6·12·24인과 후기 24인의 식량·기반망·방어·의료·계약 요구 WU와 재배치 한계 |
| 21 | [포트폴리오 운영 약속](content-plans/portfolio-operating-commitments.csv) | 여섯 포트폴리오의 10·30·120·400·960일 가동 콘텐츠, 압력, 계약과 전환을 실제 stable ID로 연결한 30개 행 |
| 22 | [실행 검증 수용 기준](balance-execution-acceptance.md) | 정적 수치를 실행·플레이 자료로 승격할 표본, 수용 조건, 재조정 절차 |

아이템 kg·스택·가격·판매율은 아이템 수치 원장이 기준값이다. 생산식 BOM·직접 WU·시간·물·폐수·확률·산출 kg·가격은 생산 수치 원장이 기준값이다. 연구 WU·동시 연구자·선행 연구·필요 시설·직접 해금은 연구 수치 원장이 기준값이다. 시설의 건설 BOM·설치·수리·청소·운전 WU·공간·유지·해체 회수·처리량은 시설 수치 원장이 기준값이다. 조우 편성·적 수·배율·목표·전장·대응 태그·보상 단가는 조우 수치 원장이 기준값이다. 사건의 요구 BOM·kg·단가·기한·효과·지속일은 사건 수치 원장이, 사건 대응·파견·복구 WU는 사건 대응 노동 원장이 기준값이다. 콘텐츠 DB는 stable ID와 구조를 찾는 전수 스냅샷으로 사용한다. 16개 수정 공정은 생산 물리 BOM 원장의 값이 우선한다.

의복 정의 56개, 환경 작업복 4개, 장비 모듈 20개는 각각 [의복 수치 원장](content-plans/items/apparel-numeric-ledger.csv), [환경 작업복 수치 원장](content-plans/items/environmental-workwear-numeric-ledger.csv), [장비 모듈 수치 원장](content-plans/items/equipment-module-numeric-ledger.csv)이 기준값이다. 의복 정의의 `base_weight_kg`와 물리 아이템 질량은 서로 다른 권위로 보존한다. 보관·운반·거래에는 물리 아이템 질량과 가격을, 재단·소재 계산에는 의복 정의의 기준 질량을 쓴다. 제작 WU와 BOM은 산출 아이템을 가진 생산식 원장, 모듈의 획득 비용은 원정 보상 원장과 조우 보상 원장에서 판정한다.

문서 작업으로 새 수치를 정할 때는 해당 ID의 값 위치를 바꾸지 않는다. 변경안은 먼저 이 문서의 판정식과 비교 축을 통과한 뒤, 콘텐츠별 변경 기록에 이전값·최종값·대체안·영향받는 빌드·다시 확인할 범위를 남긴다. 값이 바뀌지 않은 콘텐츠는 현재 authored 값을 설계 기준값으로 유지한다.

## 공통 계산식

### 생산과 물리 흐름

```text
직접 WU = requiredWork + preparationWork + finishingWork

공정 질량 잔차(g)
= 고형 입력 질량(g)
+ cleanWaterPerCycle × 1,000
- 산출 수량 × 산출 단위 질량(g)
- wastewaterPerCycle × 1,000

유효 처리량
= 가용 작업대 lane × 가용 effective WU ÷ 직접 WU
```

Transform 공정의 잔차는 0보다 작을 수 없다. 0보다 큰 잔차는 공정 손실, 부산물, 폐수, 포장 회수 중 하나의 이름과 실제 처리 경로를 가진다. Source 공정의 외부 질량은 세계·생물·마법 생산원 계약에 기록한다. Sink 공정은 산출 없이 물질을 폐기하는 종착 경로다.

### 경제와 가격

```text
단위 EWU
= (입력 EWU + 직접 제작 WU + 운반 WU + 전력·정비 배부 WU
   + 부패·오염·실패의 기대 손실)
 ÷ 기대 유효 산출량

판매 순수익
= 판매 수입 - 재조달 원가 - 운반·보관·서비스 비용 - 실패 기대 손실

계약 순수익
= 보상 가치 - 화물 EWU - 호위·기한·관계 유지 비용 - 실패 기대 손실
```

내부 비교는 `1 gold = 3 EWU`를 사용한다. 외부 구매는 같은 EWU보다 25~50% 비싸고, 외부 판매는 30~50% 낮다. 가역 제작·판매·해체·품질 재굴림은 투입 EWU의 95%를 넘겨 회수하지 못한다.

### 전투와 사건

```text
조우 준비 비용
= 장비 제작·수리 WU + 탄약·소모품 EWU + 치료 여유 + 전투 인력의 기회비용

사건 순효과
= 즉시 보상 + 장기 상태 가치
- 준비 물자 - 대응 노동 - 손실 확률 × 손실 규모
- 이후 계약·관계·생산 압력
```

승률만으로 조우를 비교하지 않는다. 부상·탄약·장비 손상·원정 공백·회복일을 함께 기록한다. 사건은 성공 보상만으로 평가하지 않는다. 준비하지 않았을 때의 손실, 준비해도 남는 약점, 다음 사건에 남는 상태가 같은 행에 있어야 한다.

## 정적 지배 판정

두 선택지가 같은 목적을 해결할 때 아래 다섯 축을 비교한다.

| 축 | 낮을수록 유리한 값 | 높을수록 유리한 값 |
|---|---|---|
| 경제 | EWU, 직접 WU, 운반량, 유지비, 전환 손실 | 순수익, 비축일, 회수율 |
| 공간·기반망 | 면적, 전력·물·열·정비 부하, 공통 고장 범위 | 처리량, 복구 독립성 |
| 전투 | 장비·탄약·치료 비용, 기동·손 점유, 부상 회복일 | 대상 조우 승률, 방어 범위, 원정 지속일 |
| 사건 | 준비 비용, 실패 손실, 관계 채무 | 대응 경로 수, 회복 속도, 장기 기회 |
| 전환 | 철거·운반·청소·재교육·계약 해지 비용 | 다른 포트폴리오와의 연결, 비상 유지력 |

A가 B보다 모든 축에서 같거나 좋고, A만 가진 상황 한정 약점·고유 대응·전환 비용도 없으면 A는 지배 선택이다. A의 수치 상향, B의 비용 하향, B만 해결하는 압력 추가, 두 선택의 입력·공간·인력 경쟁 강화 중 하나로 해소한다.

한 선택이 특정 환경 또는 조우에서 강한 것은 허용한다. 모든 기후·인구 단계·외부 압력에서 같은 포트폴리오가 식량 안정성, 첫 이정표 속도, 방어 회복, 자본 축적을 동시에 1위로 차지하면 설계 결함이다.

## 문서에서 고정한 범위

- Transform 328개는 1,074개 weighted item 기준 질량 생성 0건으로 닫혔다.
- 생산 계약 355개 중 구체 `ProductionRecipeSO` 335개는 현재 입력 BOM 불일치 0건이다. 나머지 20개 `source:*`는 세계 생산원 계약이다.
- 아이템 1,074개는 현재 직렬화 에셋을 다시 읽은 kg·스택·가격·판매율 원장으로 전수 추적한다.
- 생산식 355개는 현재 직렬화 에셋과 최신 생산 계약을 다시 읽은 BOM·WU·시간·확률·kg·가격 원장으로 전수 추적한다.
- 시설, 연구, 장비, 특성, 사건, 세력, 기후는 콘텐츠 DB stable ID 행과 유형별 수치 인덱스로 전수 추적한다.
- 여섯 포트폴리오의 시점별 노동·비축·압력 예산은 [단계 예산표](content-plans/portfolio-stage-budgets.csv)에 고정한다.
- 조우·계약·계절 사건·서비스 사건의 보상과 손실은 [전투·사건 판정선](content-plans/combat-and-event-bands.csv)에서 같은 경제 단위와 회복 기간으로 비교한다.
- 여섯 포트폴리오의 연구·시설 조립은 [포트폴리오 수치 조립 원장](content-plans/portfolio-numeric-assembly.md)에서 stable ID, 선행 연구, 건설 BOM·kg·가격·공간까지 결속한다.

실행 검증은 문서 수치의 적용 여부와 플레이 결과를 확인하는 별도 단계다. 정적 설계는 여기서 끝내고, 시뮬레이션과 플레이 자료는 [밸런스 검증 현황](balance-validation-status.md)에 정의한 판정 순서로 합류한다.
