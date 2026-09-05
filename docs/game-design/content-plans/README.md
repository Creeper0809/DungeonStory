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
| [수치 검증 결속](balance-validation-binding.csv) | 28개 전수 콘텐츠 분모가 들어갈 비용 기록·비교 감사·결정론 검증 범위 |

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
| [연구 해금 결속](research/unlocks/) | 같은 연구 분류로 나눈 16개 계약: 보상 분류·의도·실제 필요 연구를 가진 제작·장비·작물·시술 ID를 연결 |

### 조립식 시설

| 기록 | 범위 |
|---|---|
| [시설 청사진](facilities/facility-blueprints.csv) | 상업·요새·생활·비전의 방 기능을 여는 청사진 7개 |
| [시설 합성](facilities/facility-synthesis.csv) | 실제 파츠 이동·해체·재조립이 필요한 합성식 9개 |
| [시설 진화](facilities/facility-evolution.csv) | 사용 이력과 방 조건에서 성장하는 진화식 6개 |
| [건물 자산 분류](facilities/building-asset-classification.csv) | 419개 건물을 플레이어 파츠·이정표·토폴로지·자원·런타임 앵커로 구분 |
| [플레이어 건물 기능 계약](facilities/player-building-functional-contract.csv) | 배치 가능한 파츠 325개의 기능·경쟁 후보·방 인정·가동·전환 규칙 |

### 생산식

| 기록 | 범위 |
|---|---|
| [생산식 계약](production/production-recipe-contract.csv) | 입력·출력·작업대·연구·병목·과잉 생산·대체·복구가 연결된 355개 |

### 아이템

| 기록 | 범위 |
|---|---|
| [일반 아이템 역할 계약](items/generic-item-role-contract.csv) | 기능·kg·가격 근거·재고 압력·대체·회복이 연결된 710개 |
| [원료 아이템 역할 계약](items/resource-item-role-contract.csv) | 생산·가공·보관·운반·소비처 선택이 연결된 364개 |
| [의복 역할 계약](items/apparel-role-contract.csv) | 장비 레이어·신체형·점유·연구·물리 아이템 장착이 연결된 56개 |
| [환경 작업복 역할 계약](items/environmental-workwear-role-contract.csv) | 냉장·운반·종족 조건과 장비 재배정이 연결된 4개 |
| [장비 모듈 빌드 계약](items/equipment-module-build-contract.csv) | 무기·방어구·방패 계보별 전술 분기와 필수 효과 결속이 연결된 20개 |
| [전투 무기 역할 계약](items/combat-weapon-role-contract.csv) | 제작·손 점유·사거리·전투 명령·탄약·수리·원정 보급이 연결된 31개 |
| [전투 방어구 역할 계약](items/combat-armor-role-contract.csv) | 신체 방호·레이어·제작·질량·수리·방패와의 장비 선택이 연결된 21개 |
| [전투 방패 역할 계약](items/combat-shield-role-contract.csv) | 전방 차단·피해 방호·손 점유·전열 배치·수리·원정 적재가 연결된 9개 |

### 종족과 환경

| 기록 | 범위 |
|---|---|
| [종족 운영 계약](context/species-strategy-contract.csv) | 필요·환경·선호 시설·강약 작업·사건 완화·관계로 갈리는 10개 인구 운영 |
| [문화 수용 계약](context/culture-accommodation-contract.csv) | 방·시설·물품·금기·의례·동화·관계 비용으로 갈리는 10개 문화 운영 |
| [기후 운영 계약](context/climate-operation-contract.csv) | 평균·변동 온도와 작업복·시설·작물·비축·근무 재편성으로 갈리는 5개 기후 |
| [세력 전략 계약](context/faction-strategy-contract.csv) | 교역·보급·증원·관계 비용과 자급·안보의 선택이 갈리는 6개 고유 세력 |
| [세력 원본 대조](context/faction-source-reconciliation.csv) | 12개 중복 자산을 6개 고유 ID와 Dungeons authoring root에 연결 |
| [특성 선택 계약](context/traits/) | 이점 41·상충 42·불리 20·극단 7·기벽 3의 선택 가족·비양립·운영·회복 113개 |
| [인구·문화·세력 운영 패키지](context/population-culture-faction-package.csv) | 문화 기본 종족 10개를 필요·강약 작업·수용·세력 외부 경로와 연결 |
| [포트폴리오 맥락 매트릭스](context/portfolio-context-matrix.csv) | 문화·종족 10개와 기후 5개 조합마다 주력·보조·압력·회복을 정한 50개 런 설계 |
| [세력 경로 계약](context/faction-portfolio-route.csv) | 세력 6개가 보완하는 포트폴리오, 늦어지는 투자, 의무와 이탈 경로 |

## 기록 원칙

- 현재 자산 ID가 확인된 행은 그 ID를 쓴다. 시설군처럼 전수 자산 매핑 전인 행은 `설계 기록 ID`와 `작성 자산 매핑 상태`를 분리한다.
- `현재 상태`는 자산·런타임·검증의 사실을 적고, `목표 상태`는 설계가 요구하는 행동을 적는다.
- 물리 BOM, WU, kg, 가격, 기간, 확률과 보상은 이 표에서 새로 정하지 않는다. 필요한 수치는 [전체 게임 밸런스 기준서](../whole-game-balance-baseline.md)의 승인 기록에 연결한다.
- 사건 행은 실제 원인, 고정 대상, 실제 행동, 완료 영수증, 영속 흔적, 후속 반응 중 어느 하나도 비워 두지 않는다. C1~C2 자동 기록은 `해당 없음: 자동 기록 근거`를 명시한다.
- 전략 행은 플레이어 이득만 적지 않는다. 늦어지는 대안, 실패 방식, 비상 회복, 전환 경로를 함께 적는다.

## 추가 콘텐츠 편성 절차

1. 콘텐츠 DB에서 stable ID, 자산 근거, 소유 도메인을 확정한다.
2. 같은 유형의 분리된 표에 문제, 선택, 매몰비용, 늦어지는 대안, 가동 영수증, 실패·회복, 후속 반응을 기록한다.
3. 공용 capability와 도메인 권위를 확인하고, 물리 BOM·WU·kg·저장·결정론 검증은 기준서와 연결한다.
4. [전수 설계 범위](full-design-scope.csv)의 분모와 작성 계약을 갱신하고, 구현·저장·감사 상태는 [시스템 구현 권위 체크리스트](../../system-implementation-checklist.md)에서 판정한다.

`작성 OPEN`은 해당 정의의 플레이 선택·대체재·후속 결과 설계 기록이 미확정 상태임을 표시한다.
