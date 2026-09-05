# 콘텐츠 설계 기록

이 디렉터리는 전략·사건 콘텐츠를 실제 플레이 선택으로 설계하는 작업 기록이다. 생성된 콘텐츠 DB는 현재 자산과 코드 근거를 소유하고, 이 기록은 플레이어 경험과 목표 연결 계약을 소유한다.

연구, 시설, 생애 사건, 서비스 사고, 계절 사건, 세력 계약은 각각 분리된 CSV로 관리한다. 공통 열에는 콘텐츠를 가로지르는 연결만 기록한다.

| 기록 | 범위 |
|---|---|
| [연구 포트폴리오](research-portfolio.csv) | 현존 연구가 어떤 문제·주력·전환을 만드는지 |
| [시설 포트폴리오](facility-portfolio.csv) | 시설군이 공간·물류·기반망·이력으로 어떤 선택을 묶는지 |
| [생애 사건 통합](life-event-integration.csv) | 인물 이력에서 출발하는 지속 사건 |
| [서비스 사고 통합](service-incident-integration.csv) | 서비스 영수증과 공간 운영에서 출발하는 사건 |
| [계절 사건 통합](seasonal-event-integration.csv) | 환경과 기반망을 시험하는 지속 사건 |
| [세력 계약 통합](faction-contract-integration.csv) | 실물 화물·경로·의무를 남기는 계약 |
| [세력 장 통합](faction-chapter-integration.csv) | 여섯 세력의 접촉·압력·교차 갈등·방향 선택·위기·결산 36개 |
| [손님 요청 통합](guest-request-integration.csv) | 객실·식사·의료·보안·검역·교역을 잠그는 요청 14개 |
| [축제 통합](festival-integration.csv) | 공간·물자·인력·사고·문화 후속을 갖는 축제 16개 |
| [축제 원본 대조](festival-source-reconciliation.csv) | 중복 stable ID를 구형 자산 경로와 V20 설계 행에 연결 |
| [문화 관습 통합](cultural-practice-integration.csv) | 종족·가구의 물자·공간·노동·기억을 남기는 관습 20개 |
| [전수 설계 범위](full-design-scope.csv) | 현재 자산별 설계 분모, 작성 계약과 다음 전수 기록 |
| [밸런스·진행 검증 계약](../balance-progression-validation-contract.md) | 공통 단위, 경제 판정선, 투자 회수, 난도·이정표의 수치 검증 순서 |
| [반복 플레이 메타 검증](../replay-meta-validation.md) | 여섯 전략 포트폴리오, 지배 전략 탐지, 다중 시드와 승인 문턱 |
| [전략 포트폴리오 조립 계약](strategic-portfolio-assembly.md) | 연구·시설·생산·물품의 전수 계약을 여섯 실제 런 포트폴리오로 조립하는 규칙 |
| [포트폴리오 운영 약속](portfolio-operating-commitments.csv) | 여섯 포트폴리오의 10·30·120·400·960일 가동 콘텐츠·압력·계약·전환을 실제 stable ID로 결속한 30개 행 |
| [포트폴리오 선택 폐쇄 기록](portfolio-choice-closure.md) | 단계별 선택 비용, 여섯 운영의 충돌, 정적 압력·지배 대조와 수치 조정 순서 |
| [수치 검증 결속](balance-validation-binding.csv) | 직접 수치 유형이 들어갈 비용 기록·비교 감사·결정론 검증 범위 |
| [콘텐츠 수치 인덱스](balance-numeric-authority.csv) | 전수 콘텐츠 종류별 정확한 수치 위치·필수 필드·정적 비교 축 |
| [포트폴리오 단계 예산](portfolio-stage-budgets.csv) | 여섯 기준 빌드의 10·30·120·400·960일 노동·비축·압력·회복 수치 |
| [포트폴리오 수치 곡선](portfolio-numeric-curve.md) | 단계 예산을 누적 운영·주력·연구·예비 WU 곡선으로 만든 30개 비교 행 |
| [포트폴리오 수치 조립 원장](portfolio-numeric-assembly.md) | 여섯 경로의 연구 선행 폐쇄·시설 BOM·공간·WU를 30·120·400·960일 예산에 결속한 24개 조립 판정 |
| [포트폴리오 연구 목표](portfolio-research-targets.csv) | 여섯 경로가 실제로 선택하는 연구 75개 |
| [포트폴리오 시설 목표](portfolio-facility-targets.csv) | 연구가 열어 주는 실제 시설 71개와 수량·조립 역할 |
| [포트폴리오 조립 가능성](portfolio-assembly-feasibility.csv) | 연구·시설 예산, 선행 연구, 시설 BOM 누락을 함께 확인한 24개 행 |
| [기후별 정적 비교](portfolio-context-static-score.csv) | 다섯 현재 기후에서 경제·안보·회복·확장 강약을 비교한 30개 행 |
| [쌍별 지배 판정](portfolio-pairwise-dominance-audit.csv) | 여섯 포트폴리오 15쌍의 정적 Pareto 대조 |
| [인구·압력 단계 매트릭스](portfolio-pressure-stage-model.md) | 3·6·12·24인과 후기 24인에서 식량·기반망·방어·의료·계약 압력을 함께 대조한 30개 행 |
| [포트폴리오 재조정 기록](portfolio-rebalancing-decisions.csv) | 정적 실패에서 실제로 옮긴 WU, 포기한 처리량, 확보한 생존 여유 |
| [전투·사건 판정선](combat-and-event-bands.csv) | 36개 조우와 사건·계약·환대·축제의 손실·보상·회복·지배 방지 수치 |
| [조우 수치 원장](combat/encounter-numeric-ledger.md) | 현재 에셋에서 다시 읽은 36개 조우의 적 편성·배율·목표·전장·대응 태그·보상 단가 |
| [적 수치 원장](combat/enemy-numeric-ledger.csv) | 현재 에셋에서 다시 읽은 36개 적의 체력·공격·장비·전술 가중치·보상 단가 |
| [사건 수치 원장](events/event-numeric-ledger.md) | 현재 에셋에서 다시 읽은 생애·서비스·계절·계약·세력 장·손님·축제·관습의 BOM·kg·단가·기한·효과 |
| [사건 대응 노동 원장](events/event-response-labor-model.md) | 172개 사건의 대응·파견·복구 WU를 물자 제작 WU와 분리해 고정한 8개 원장 |
| [의료 시술 수치 원장](medical/procedure-numeric-ledger.md) | 현재 에셋에서 다시 읽은 47개 시술의 WU·위험·마취·구속·재료 BOM·kg·단가 |
| [질병 수치 원장](medical/disease-numeric-ledger.csv) | 현재 에셋에서 다시 읽은 16개 질병의 감염·중증도·기간·백신·만성화·대응 경로 |

### 연구별 포트폴리오

| 기록 | 범위 |
|---|---|
| [농업](research/agriculture.csv) | 채집, 외부 경작, 지하 자급, 온실, 종자와 토양 순환 10개 |
| [물·위생](research/water-and-sanitation.csv) | 수동 위생, 중앙 급배수, 재이용, 룬 정화와 환대 서비스 5개 |
| [산업·자동화](research/industry-and-automation.csv) | 증기·수차·마나, 물류 자동화, 정밀·군수·서비스 자동화 31개 |
| [상업·제작](research/commerce-and-craft.csv) | 창고·환대·교역과 표준 무기·궁시 제작 8개 |
| [방어·전술](research/defense-and-tactics.csv) | 경계·요새·동맹·군수·기동군과 원거리 방어 13개 |
| [생활·생존](research/life-and-survival.csv) | 위생·비축·원정 보급·의료 회복·환경 보호·장례 9개 |
| [약리](research/pharmacology.csv) | 약초·소독·증류·마취·각성제·고급 약제 6개 |
| [채굴·금속](research/mining-and-metallurgy.csv) | 외부 채굴·선별·심부·마나 광맥과 원시·철·강철·비전 금속 14개 |
| [임업·축산](research/forestry-and-husbandry.csv) | 외부 목재·제재·숯·균목림과 포획·사육·번식·선별 13개 |
| [권위·주거](research/authority-and-housing.csv) | 집무·숙소·환대와 방 배정·가구·교육·승계·멘토 11개 |
| [구금·흥행](research/captivity-and-entertainment.csv) | 구속·노역·공연·위험 흥행의 보안·관계·회복 비용 4개 |
| [조리](research/cuisine.csv) | 기본식·제분·제빵·축산식·채식·발효·위생·숙성·복합 주방 10개 |
| [기록·비전](research/records-and-arcane.csv) | 관측·역법·기록·연금·공명·유물·계보와 시간 의료 12개 |
| [섬유](research/textiles.csv) | 섬유·재봉·무두질·직책 방호·층상·룬가죽·몽직물 7개 |
| [임상·예방 의학](research/clinical-and-preventive-medicine.csv) | 접수·관찰·격리·예방·수술·외상·산과·노화 관리 10개 |
| [재건·이식·개조 의학](research/reconstruction-and-augmentation.csv) | 보철·장기·재생·이종 이식·계통·종족 신체·구성체·전신 재생 17개 |
| [연구 수치 원장](research/research-numeric-ledger.csv) | 현재 에셋에서 다시 읽은 180개 연구의 WU·동시 연구자·선행·시설·직접 해금 수 |
| [연구 해금 결속](research/unlocks/) | 같은 연구 분류로 나눈 16개 계약: 보상 분류·의도·실제 필요 연구를 가진 제작·장비·작물·시술 ID를 연결 |

### 조립식 시설

| 기록 | 범위 |
|---|---|
| [시설 청사진](facilities/facility-blueprints.csv) | 상업·요새·생활·비전의 방 기능을 여는 청사진 7개 |
| [청사진 수치 원장](facilities/facility-blueprint-numeric-ledger.csv) | 7개 청사진의 기본 가격·연구 WU·희귀도·연구 관문 |
| [시설 합성](facilities/facility-synthesis.csv) | 실제 파츠 이동·해체·재조립이 필요한 합성식 9개 |
| [시설 합성 수치 원장](facilities/facility-synthesis-numeric-ledger.csv) | 9개 합성식의 투입 건물 수·공개 상태·연구 관문·레벨 계승률 |
| [시설 진화](facilities/facility-evolution.csv) | 사용 이력과 방 조건에서 성장하는 진화식 6개 |
| [시설 진화 수치 원장](facilities/facility-evolution-numeric-ledger.csv) | 6개 진화식의 별 등급·방·고정물·이력·정체성 문턱 |
| [건물 자산 분류](facilities/building-asset-classification.csv) | 419개 건물을 플레이어 파츠·이정표·토폴로지·자원·런타임 앵커로 구분 |
| [시설 수치 원장](facilities/facility-numeric-ledger.md) | 현재 에셋에서 다시 읽은 419개 시설의 BOM·설치·수리·청소·운전 WU·공간·유지·처리량 |
| [플레이어 건물 기능 계약](facilities/player-building-functional-contract.csv) | 배치 가능한 파츠 325개의 기능·경쟁 후보·방 인정·가동·전환 규칙 |

### 생산식

| 기록 | 범위 |
|---|---|
| [생산식 계약](production/production-recipe-contract.csv) | 입력·출력·작업대·연구·병목·과잉 생산·대체·복구가 연결된 355개 |
| [생산 수치 원장](production/production-numeric-ledger.csv) | 현재 에셋과 생산 계약에서 다시 읽은 355개 BOM·직접 WU·시간·확률·산출 kg·시장가 |
| [생산 물리 BOM 원장](production/production-physical-bom-ledger.csv) | 물·폐수·고형 입력을 교정한 16개 공정의 최종 질량·직접 WU·시장가 |
| [작물 수치 원장](production/crop-numeric-ledger.md) | 현재 에셋에서 다시 읽은 12개 작물의 종자·수확 kg·가격·수확량·WU·물·시간·온도 |

### 아이템

| 기록 | 범위 |
|---|---|
| [일반 아이템 역할 계약](items/generic-item-role-contract.csv) | 기능·kg·가격 근거·재고 압력·대체·회복이 연결된 710개 |
| [원료 아이템 역할 계약](items/resource-item-role-contract.csv) | 생산·가공·보관·운반·소비처 선택이 연결된 364개 |
| [아이템 수치 원장](items/item-numeric-ledger.csv) | 현재 에셋에서 다시 읽은 1,074개 아이템의 kg·스택·가격·판매율 |
| [의복 역할 계약](items/apparel-role-contract.csv) | 장비 레이어·신체형·점유·연구·물리 아이템 장착이 연결된 56개 |
| [의복 수치 원장](items/apparel-numeric-ledger.csv) | 현재 에셋에서 다시 읽은 56개 의복의 물리 kg·가격·기준 질량·신체 점유·소재·재단·연구 |
| [환경 작업복 역할 계약](items/environmental-workwear-role-contract.csv) | 냉장·운반·종족 조건과 장비 재배정이 연결된 4개 |
| [환경 작업복 수치 원장](items/environmental-workwear-numeric-ledger.csv) | 현재 에셋에서 다시 읽은 4개 작업복의 물리 kg·가격·온도 범위·노출 배율·연구 |
| [장비 모듈 빌드 계약](items/equipment-module-build-contract.csv) | 무기·방어구·방패 계보별 전술 분기와 필수 효과 결속이 연결된 20개 |
| [장비 모듈 수치 원장](items/equipment-module-numeric-ledger.csv) | 현재 에셋에서 다시 읽은 20개 모듈의 계보·시대·등급별 위력·유틸리티 |
| [의복·모듈 수치 해설](items/apparel-and-module-numeric-notes.md) | 정의 질량과 물리 아이템 질량, 생산식 WU/BOM, 원정 보상 모듈의 수치 경계를 정리 |
| [전투 장비 수치 원장](items/combat-equipment-numeric-ledger.md) | 현재 에셋에서 다시 읽고 재질을 물리 아이템으로 해석한 무기 31·방어구 21·방패 9개의 BOM·kg·가격·WU·전투값 |
| [전투 무기 역할 계약](items/combat-weapon-role-contract.csv) | 제작·손 점유·사거리·전투 명령·탄약·수리·원정 보급이 연결된 31개 |
| [전투 방어구 역할 계약](items/combat-armor-role-contract.csv) | 신체 방호·레이어·제작·질량·수리·방패와의 장비 선택이 연결된 21개 |
| [전투 방패 역할 계약](items/combat-shield-role-contract.csv) | 전방 차단·피해 방호·손 점유·전열 배치·수리·원정 적재가 연결된 9개 |

### 종족과 환경

| 기록 | 범위 |
|---|---|
| [종족 운영 계약](context/species-strategy-contract.csv) | 필요·환경·선호 시설·강약 작업·사건 완화·관계로 갈리는 10개 인구 운영 |
| [종족·특성 수치 원장](context/population-numeric-ledger.md) | 현재 에셋에서 다시 읽은 10개 종족과 113개 특성의 소비·환경·기능·효과 계수 |
| [문화 수용 계약](context/culture-accommodation-contract.csv) | 방·시설·물품·금기·의례·동화·관계 비용으로 갈리는 10개 문화 운영 |
| [문화 수치 원장](context/species-culture-numeric-ledger.csv) | 10개 문화의 동화일·온도·환기·조명·청결·공간 선호·의례 관계 수 |
| [기후 운영 계약](context/climate-operation-contract.csv) | 평균·변동 온도와 작업복·시설·작물·비축·근무 재편성으로 갈리는 5개 기후 |
| [기후 수치 원장](context/climate-numeric-ledger.csv) | 5개 기후의 평균 온도·연간 진폭·현지 시간 |
| [세력 전략 계약](context/faction-strategy-contract.csv) | 교역·보급·증원·관계 비용과 자급·안보의 선택이 갈리는 6개 고유 세력 |
| [세력 수치 원장](context/dungeon-faction-numeric-ledger.csv) | 6개 세력의 교역·보급 화물 kg·시장가·쿨다운 |
| [세력 원본 대조](context/faction-source-reconciliation.csv) | 12개 중복 자산을 6개 고유 ID와 Dungeons authoring root에 연결 |
| [특성 선택 계약](context/traits/) | 이점 41·상충 42·불리 20·극단 7·기벽 3의 선택 가족·비양립·운영·회복 113개 |
| [인구·문화·세력 운영 패키지](context/population-culture-faction-package.csv) | 문화 기본 종족 10개를 필요·강약 작업·수용·세력 외부 경로와 연결 |
| [포트폴리오 맥락 매트릭스](context/portfolio-context-matrix.csv) | 문화·종족 10개와 기후 5개 조합마다 주력·보조·압력·회복을 정한 50개 런 설계 |
| [세력 경로 계약](context/faction-portfolio-route.csv) | 세력 6개가 보완하는 포트폴리오, 늦어지는 투자, 의무와 이탈 경로 |

## 실행 검증

| 기록 | 범위 |
|---|---|
| [실행 검증 수용 기준](../balance-execution-acceptance.md) | 경제·전투·사건·시설 전환·저장·플레이 세션에서 수집할 표본, 수용 조건, 실패 뒤 재조정 규칙 |

## 기록 원칙

- 현재 자산 ID가 확인된 행은 그 ID를 쓴다. 시설군처럼 전수 자산 매핑 전인 행은 `설계 기록 ID`와 `작성 자산 매핑 상태`를 분리한다.
- `현재 상태`는 자산·런타임·검증의 사실을 적고, `목표 상태`는 설계가 요구하는 행동을 적는다.
- 물리 BOM, WU, kg, 가격, 기간, 확률과 보상은 [콘텐츠 수치 인덱스](balance-numeric-authority.csv)와 [수치 설계 권위](../numeric-balance-authority.md)에서 고정한다. 개별 계약은 그 수치가 만드는 선택·대체·가동·회복을 기록한다.
- 사건 행은 실제 원인, 고정 대상, 실제 행동, 완료 영수증, 영속 흔적, 후속 반응 중 어느 하나도 비워 두지 않는다. C1~C2 자동 기록은 `해당 없음: 자동 기록 근거`를 명시한다.
- 전략 행은 플레이어 이득만 적지 않는다. 늦어지는 대안, 실패 방식, 비상 회복, 전환 경로를 함께 적는다.

## 추가 콘텐츠 편성 절차

1. 콘텐츠 DB에서 stable ID, 자산 근거, 소유 도메인을 확정한다.
2. 같은 유형의 분리된 표에 문제, 선택, 매몰비용, 늦어지는 대안, 가동 영수증, 실패·회복, 후속 반응을 기록한다.
3. 공용 capability와 도메인 권위를 확인하고, 물리 BOM·WU·kg·가격·처리량·확률·보상을 수치 인덱스와 해당 콘텐츠 DB 행에 연결한다.
4. [전수 설계 범위](full-design-scope.csv)의 분모와 작성 계약을 갱신하고, 구현·저장·감사 상태는 [시스템 구현 권위 체크리스트](../../system-implementation-checklist.md)에서 판정한다.

`작성 OPEN`은 해당 정의의 플레이 선택·대체재·후속 결과 설계 기록이 미확정 상태임을 표시한다.
