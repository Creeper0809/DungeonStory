# 위키 → 현재 구현 누락·부분·의미 차이 전수 원장

상태: **후속 정적 조사 진행 중**. 기존 구조화 facts·relations 전수는 유지되지만 `in_game_description` 의미 소비 대조에서 새 항목이 발견되어 종료 판정을 다시 열었다. 이 파일은 `missing-register.*`와 반대 방향의 권위다. Play Mode 재현과 구현 완료를 뜻하지 않는다.

- `missing`: 위키가 약속한 생산 실행 경로 또는 필드 소비자가 현재 구현에 없다.
- `partial`: 핵심 일부는 있으나 위키가 설명한 조건·UI·물류·도메인 연결 일부가 없다.
- `different`: 기능은 있으나 순서·수치·의미가 위키와 다르다.
- `documentation-only`: 구현 결함이 아니라 위키 상세 설명만 부족하다. 구현 보완 집계에서 제외한다.
- 정적 근거는 Unity Play Mode 재현과 구분한다. `미발견`은 검색한 production callsite 범위에서의 판정이다.

현재 판정: 63건 = missing 19, partial 24, different 17, documentation-only 3.

## 기획 처분 권위

2026-09-06 사용자 결정까지 반영한 실행 처분은 다음과 같다. 이 처분은 정적 불일치 분류(`missing/partial/different/documentation-only`)와 별개이며, 구현자가 `different`를 임의로 위키 또는 코드 중 한쪽에 맞추지 못하게 하는 권위다.

- `wiki-change-confirmed` 13건: `WIM-024`, `WIM-025`, `WIM-026`, `WIM-027`, `WIM-031`, `WIM-034`, `WIM-038`, `WIM-049`, `WIM-057`, `WIM-058`, `WIM-059`, `WIM-060`, `WIM-061`
- `implementation-change-confirmed` 47건: `WIM-001`~`WIM-014`, `WIM-016`~`WIM-023`, `WIM-028`~`WIM-030`, `WIM-032`~`WIM-033`, `WIM-035`~`WIM-037`, `WIM-039`~`WIM-048`, `WIM-050`~`WIM-056`
- `validation-required` 1건: `WIM-015`
- `decision-pending` 2건: `WIM-062`, `WIM-063`

확정 결정:

- `WIM-015`: 99 WU/일과 45 WU/일 중 하나를 문서만 보고 고르지 않는다. 고정 seed 연구 실행에서 실제 승인 연구 WU/일과 완료일을 측정한다. UI는 가능한 경우 현재 연구원·도구·시설·관리기술을 반영한 동적 예상치를 사용하고, 동적 계산이 불가능할 때만 V27 effective 45 WU/일을 fallback으로 사용한다. 99 WU/일은 레거시 이론값이며, 실측이 99에 가까우면 중복 배율 여부를 먼저 감사한다.
- `WIM-016`: 빛·온도·물·토양 충족시간을 실제 생육 gate로 구현한다.
- `WIM-020`: 실제 kg를 장착·운반·창고의 단일 물리 질량 권위로 사용하며 별도 작성 착용 질량을 두지 않는다.
- `WIM-021`: 수혈은 이미 잃은 혈액량을 실제로 회복한다.
- `WIM-022`: 긴급봉합은 지속 출혈률을 실제로 감소시킨다.
- `WIM-023`: 이식·보철·보강 수술은 실제 신체 부위 설치·교체·보강 상태를 남긴다.
- `WIM-031`: 결제는 중앙 금화 계정 권위를 유지한다. 위키에서 결제마다 물리 금화를 운반한다는 설명을 제거하고, 금고는 보유 한도·도난·보호 기능으로 설명한다.
- `WIM-036`: 강장제·각성제·진통제는 각각 피로·연구/비전·통증에 직접 작용하는 고유 효과를 구현한다.
- `WIM-038`: 현재 일일 후보 평가, 정의·분류 쿨다운, 주민 점유와 일반/비상 슬롯 규칙을 위키에 정확히 투영한다. 기존의 단순 2~5일/1~2일·동시 최대2 약속은 제거한다.
- `WIM-045`: 문에 실제 열림/닫힘 상태를 구현하고 이동 차단과 환경 전파에 반영한다. 출입 정책은 누가 문을 열 수 있는지를 결정한다.
- `WIM-049`: 이 항목은 던전 침입이 아니라 플레이어가 나가는 원정 전투 소유다. 적은 작성된 조우와 캠페인 보정으로 생성하고 `requiredPower`는 그 난이도를 설명하는 권장 원정대 전투력으로 정의한다. 권장값의 정확도는 승률 회귀로 검증하며 표시값이 적 능력치를 다시 조정하는 입력이 되지 않는다.
- `WIM-054`: mutable `captive health`를 독립 권위로 사용하지 않는다. 포로도 공통 신체·부상·질병 상태를 사용하고, 포로 건강도는 이를 0~100으로 투영한 읽기 전용 값으로 만든다. 노역 조건과 포로 상호작용 피해도 실제 신체 상태를 사용한다.
- `WIM-061`: 나팔은 침입 집결 전용이다. 화재는 나팔과 무관하게 화재 대응 AI가 처리하며 위키에서 나팔의 화재 효능을 제거한다.

| ID | 판정 | 위키가 약속한 시스템 | 현재 구현 상태 | 필요한 보완 | 기존 근거 |
| --- | --- | --- | --- | --- | --- |
| WIM-001 | partial | 평상·작업·예비 의복 환복 | 환경 작업복 자동 착용만 production 호출 확인 | 일반 의복 선택 UI 또는 자동 환복 정책 연결 | DIFF-001 |
| WIM-002 | missing | 원단이 보온·통기·방수·내구를 결정 | 소재 projector의 production 소비자가 없고 보호는 작업복 고정값 | 소재 투영을 실제 보호·환경 계산에 연결 | DIFF-002 |
| WIM-003 | partial | 착용·비·작업 오염 뒤 세탁·건조·수선 순환 | 세탁/수선 writer는 있으나 상태를 누적시키는 production producer 미발견 | moisture/contamination/durability 발생원을 착용·환경에 연결 | DIFF-003 |
| WIM-004 | partial | 종족별 습도·건조·공기·빛 적응 | 온도·공기노출 일부만 연결, 여러 작성 필드는 복사 외 소비 미발견 | humidity/dryness/light/visual strain을 실제 상태·성능 계산에 연결 | DIFF-004 |
| WIM-005 | missing | 질병 증상의 직접 작업·이동 배율 | diseaseSymptoms를 stats projection이 받지만 실제 작업/이동 배율에 사용하지 않음 | 질병별 배율을 작업·이동 최종 합성에 연결 | DIFF-005 |
| WIM-006 | missing | 자동화 시설 품질 상한 | 27시설 automaticQualityCap 작성값의 runtime 소비자 미발견 | 자동 생산 품질 결정에 상한 적용 | DIFF-006 |
| WIM-007 | missing | 컨베이어 목적지 지정 운영 | SetPortDestination 구현은 있으나 gameplay 호출자 미발견 | 건물 UI/명령→목적지 검증→저장 경로 연결 | DIFF-007 |
| WIM-008 | partial | 컨베이어 품목·분류·재료 필터와 예비창고 | 상태 토글 일부와 overflow enum만 있고 목록/예비창고 UI 미발견 | 정확한 필터 편집 및 reserve destination 선택 UI 연결 | DIFF-008 |
| WIM-009 | partial | 계절 사건의 다영역 pressure·migration 효과 | WorkDelay 등은 작동하지만 pressure11/migration-window의 도메인 소비 미확인 | pressure별 실제 방어·경제·생태 소비자 연결 | DIFF-009 |
| WIM-010 | missing | 메타 강화가 시작 후보를 강화 | 구매·저장·query는 있으나 후보 생성 소비자 미발견 | 시작 후보 생성에 강화값 적용 | DIFF-010 |
| WIM-011 | partial | 정전·연료 고갈 시 조명 상실 | 환경장은 thermal/air 전력만 확인, 순수 조명 중단 연결 미확인 | 조명 시설의 전력/연료 상태를 광량장에 연결 | DIFF-011 |
| WIM-012 | partial | 문 개폐에 따른 환경망 변화 | 방 경계는 문을 보지만 현재 Door에 개폐 상태가 없음 | 실제 개폐 상태와 환경 전파·방 재계산 연결 | DIFF-012 |
| WIM-013 | missing | 전력 연결기의 스위치·통과량 | normallyOpen/maxThroughput 작성값 runtime 소비 미발견 | 네트워크 연결·유량·스위치 명령에 적용 | DIFF-013 |
| WIM-014 | missing | 장기 냉장 보존 | 2~8°C 안전 helper만 있고 장기 열화/보존 소비자 미발견 | 장기 보관 상태와 열화 시간 계산 연결 | DIFF-014 |
| WIM-015 | different | 연구 예상 기간 | UI는 99 WU/일, 공개 V27 일정은 45 WU/일 | 고정 seed 실측으로 동적 예상치를 검증하고, 동적 계산 불가 시 V27 effective 45 WU/일 사용 | DIFF-015 |
| WIM-016 | different | 빛·온도·물·토양 충족 시간만 생육 | 실제 실내 성장식은 시설·달력·유전체 중심이고 현재 칸 조건 전부를 요구하지 않음 | 빛·온도·물·토양 충족시간을 실제 생육 gate로 구현 | DIFF-016 |
| WIM-017 | missing | 날씨·기후가 원정 이동시간 변경 | 운영 출발/귀환은 WeatherMultiplier=1, 경로 비용도 이동 Tick에 미사용 | 날씨·기후·경로 비용을 실제 이동 clock에 연결 | DIFF-017 |
| WIM-018 | missing | 계절·날씨가 축산 생산 주기 변경 | 생산·임신 Tick에 기후/계절 입력 없음 | 축산 주기와 실패/보정에 환경 입력 연결 | DIFF-018 |
| WIM-019 | missing | 종별 전력누출·환기구·마나빛 추종 | MigrationPatternId 작성·복사만 있고 runtime 소비 미발견 | 종별 이동 목적지·선호 비용에 패턴 연결 | DIFF-019 |
| WIM-020 | different | 장비 무게가 기본×소재×진화로 일관 | 착용 부담과 물리 운반/창고 질량이 서로 다른 성분을 사용 | 실제 kg를 장착·운반·창고의 단일 물리 질량 권위로 통일 | DIFF-020 |
| WIM-021 | different | 수혈이 혈액 손실을 완화 | 부위 HP14/감염-2만 적용, bloodLoss 직접 감소 없음 | 수혈이 실제 bloodLoss를 회복하도록 구현 | DIFF-021 |
| WIM-022 | different | 긴급봉합이 출혈을 닫음 | HP8/감염-8만 적용, bleedingPerSecond 감소 없음 | 긴급봉합이 bleedingPerSecond를 실제 감소시키도록 구현 | DIFF-022 |
| WIM-023 | partial | 종족 특화 이식·보철·보강 | 종족별24개가 일반 치유이며 일부 이식도 설치효과 없음 | 해당 수술에 실제 신체 설치·교체·보강 효과 구현 | DIFF-023 |
| WIM-024 | documentation-only | 개별 수술의 조건·효과·위험 상세 | 수술47개 실행은 있으나 도감 필드가 부족 | 구현 작업 없음; 공개 도감 보강 | DIFF-024 |
| WIM-025 | documentation-only | 수술 환경 중단·재개·위험 규칙 | 구현은 연결돼 있으나 위키 설명 부족 | 구현 작업 없음; 공개 가이드 보강 | DIFF-025 |
| WIM-026 | different | 병상 이송 뒤 안정화 | 실제는 현장 안정화 뒤 운반 | 위키 순서 정정 또는 설계 의도대로 실행 순서 변경 | DIFF-026 |
| WIM-027 | documentation-only | 치료를 하나의 의료 흐름으로 설명 | 구조 치료와 생활시설 치료가 별도 정상 경로 | 구현 작업 없음; 두 경로를 위키에서 분리 | DIFF-027 |
| WIM-028 | missing | 해독제가 독소·과다복용을 완화 | detoxReduction의 production 소비자가 UI 외 미발견 | 독소/과다복용 상태에 authored detox 적용 | DIFF-028 |
| WIM-029 | missing | 종별 운반·경비·탐지·포자확산 등 역할 | wildlife와 해당 산업/환경 도메인의 교차 소비자0 | 종별 ability를 각 도메인 작업·효과에 연결 | DIFF-029 |
| WIM-030 | missing | 수정딱정벌레 갑각 부산물 | butcherYields와 husbandryProducts가 비어 있고 갑각 item도 없음 | 갑각 아이템·산출 레시피·도축 물리 출력 추가 | DIFF-030 |
| WIM-031 | different | 금고 접근·운반 실패가 결제를 지연 | 세션 money를 즉시 차감하며 물리 금고/운반 검사 없음 | 중앙 계정 권위를 유지하고 위키에서 결제별 물리 운반 설명 제거 | DIFF-031 |
| WIM-032 | missing | 세력 계약 수락·이행의 플레이 진입점 | 도메인/dispatcher는 있으나 비Editor action 생산자0 | UI/AI 수락·거절·진행 진입점 연결 | DIFF-032 |
| WIM-033 | partial | 계약 물자 예약 뒤 운반·납품 | exact stack 예약·제자리 소비만 있고 목적지/배송/운반자 없음 | 계약 delivery order와 목적지·운반 상태 연결 | DIFF-033 |
| WIM-034 | different | 세력 화물의 무상/재사용 정책 | 교역은 유료 선결제, 보급만 동맹 예산 | 화물 유형별 위키 설명 분리 | DIFF-034 |
| WIM-035 | missing | 호화식2종의 주민 식사 경로 | 기본 Fine 상한을 넘는 production 명령/UI 생산자 미발견 | 품질 정책 UI 또는 호화식 선택 경로 연결 | DIFF-035 |
| WIM-036 | different | 강장제 피로·각성제 연구/비전·진통제 통증 효과 | 실제는 범용 mood/work/combat 배율 중심 | 품목별 피로·연구/비전·통증 typed 상태 효과 구현 | DIFF-036 |
| WIM-037 | missing | 사건 선택 뒤 이동·운반·조건 작업 | 제자리 exact stack 소비만 있고 목적지/배송/작업 없음 | 사건별 operation·delivery·receipt 연결 | DIFF-037 |
| WIM-038 | different | 사건 빈도2~5/1~2일·동시 최대2 | 실제 매일 평가, 31일부터 일반2+비상1 가능 | 현재 일일 평가·쿨다운·주민 점유·일반/비상 슬롯 규칙으로 위키 정정 | DIFF-038 |
| WIM-039 | partial | 사건 UI의 대상·위험·자원·예약/운반·경보 등급/요약 | 설명·마감·선택만 표시, 모두 High이며 치명 등급/요약 없음 | 사건 세부 상태와 경보 정책 UI 연결 | DIFF-039 |
| WIM-040 | partial | 실제 원인에서 시작하고 수술·격리·호위 등 실행 | incident/life trigger가 비고 구체 domain operation은 OPEN | causal trigger·대상 잠금·도메인 operation 구현 | DIFF-040 |
| WIM-041 | partial | 문화별 축제와 공동 공간 참가 | cultureId 비소비, 전 생존자 집계, 이동·점유·축제 작업 없음 | 문화 필터와 실제 참가/시설 점유 작업 연결 | DIFF-041 |
| WIM-042 | partial | 시작 준비 화면의 건강·식단·수면·기후 확인 | 실제 화면은 출신·특성·숙련·기술 중심 | 후보 건강·생활조건 표시 연결 | DIFF-042 |
| WIM-043 | partial | 해빙수 범람이 농지와 운반 통로 영향 | 현재 scope 문자열은 work:haul만 일치 | sow/harvest/animal-care 또는 농지 상태에 범람 효과 연결 | DIFF-043 |
| WIM-044 | missing | 은퇴 요청의 작업지연 -2 효과 | 선행 동일 scope 지연이 없어 정상 경로에서 no-op | 의미 있는 가속/지연 감소 상태 또는 효과 정정 | DIFF-044 |
| WIM-045 | partial | 완공된 벽과 닫힌 문이 이동을 차단 | 벽은 차단하지만 Door에 open/closed 상태가 없고 권한 정책만 통행을 결정 | 실제 개폐 상태를 이동 차단·환경 전파에 연결하고 출입 정책은 개방 권한으로 사용 | direct-audit:spaces |
| WIM-046 | partial | 모든 주민·시설이 8종 대기 이유를 표시 | 공통 운영 진단기는 일반 WorkOrder와 loose 물류만 보며 시설 전력·물·연료·청결·환경·도구, 접근 위험, 일반 retry 상태를 입력으로 받지 않음 | 도메인별 typed blocking reason을 공통 진단 snapshot·운영 UI에 연결 | direct-audit:world-state |
| WIM-047 | partial | 침입 경고가 방향·남은 시간·적 성격·목표 표시 | snapshot/alert는 위협 단계·요인과 일반 임박 문장만 가지며 방향·적 archetype·목표 없음 | 경고 후보에 entry direction·enemy profile·target·countdown을 연결 | direct-audit:invasions |
| WIM-048 | missing | 외곽·비전투 주민의 대피 | 자동 대피는 사장 1명 전용이며 일반 주민 대피 구역·명령 없음 | 일반 주민 역할/위험별 대피 명령과 안전 구역·복귀 lifecycle 구현 | direct-audit:invasions |
| WIM-049 | different | 원정 적 능력치가 목표 requiredPower로 조정 | 실제 원정 적 생성은 목표값이 아니라 캠페인 순번별 고정 reference power 10/16/32/42/60/85 사용 | 원정 전투 소유로 재분류하고 requiredPower를 생성 입력이 아닌 권장 원정대 전투력으로 위키 정정 | direct-audit:offense-expeditions |
| WIM-050 | partial | 런 종료 snapshot이 완료 목표와 선택 흔적을 결산 | 결과·저장은 생존·침입·발견·오펜스 통계만 가지며 완료 이정표 ID와 선택 이력이 없음 | canonical milestone·major choice trace를 결과 snapshot·저장·UI에 연결 | direct-audit:milestones-meta |
| WIM-051 | partial | 엔드리스가 10일마다 1~2개 압력 축을 실제 강화 | 실제는 5개 도메인 ID를 항상 고르지만 active 목록의 production reader·gameplay 적용 경로가 없음 | 축 수 권위를 확정하고 선택 ID를 도메인 runtime modifier에 적용·해제 | direct-audit:milestones-meta |
| WIM-052 | missing | 복합 위기 뒤 최소 5일 회복 창 | cycle·active ID만 저장하고 회복 상태/종료일/gate 없이 매 10일 목록을 교체 | 위기 종료·회복·다음 가능일 lifecycle과 저장·UI gate 구현 | direct-audit:milestones-meta |
| WIM-053 | partial | 원정 결산에 소비·손실·치료·포획·보상 가치를 함께 기록 | 결과 history는 성공·시간·대원 생존/피해·보상 요약만 가지며 품목별 소비, 장비 손실, 치료/회복, 포획/탈출, 전리품 가치가 없음 | 원정 단위 immutable ledger를 결과 snapshot·save·UI에 연결 | direct-audit:expeditions |
| WIM-054 | different | 포로 관리의 건강 손실과 명성50 치료 우선권 | interaction과 특혜는 captive scalar health만 바꾸며 실제 신체·의료 주문에 미적용되고 다음 tick에 actor HP로 덮임 | mutable captive health를 제거하고 공통 신체 권위·파생 건강도·실제 의료 우선순위에 연결 | direct-audit:captivity |
| WIM-055 | partial | 심문이 신뢰도에 따라 실제 정보를 제공 | 공포75 문장 분기는 있으나 정보 item·도감 clue·세력/지역 intelligence 결과가 없음 | canonical 심문 outcome을 저장하고 typed 정보 destination에 exact-once 적용 | direct-audit:captivity |
| WIM-056 | partial | 목표 품질 예상 시도 20회 초과 시 강한 자원 경고 | 목표 품질·최대 시도·도달 불가 gate는 있으나 예상 시도 계산·20회 경고 snapshot/UI가 없음 | 현재 조건의 목표 도달 확률·예상 입력/WU를 계산해 typed warning과 주문 미리보기에 연결 | direct-audit:production-quality |
| WIM-057 | different | 합성 전에 생산 주문·WIP·준비 출력을 모두 비움 | 실제 합성은 열린 생산 권위를 새 결과 시설로 원자 retarget해 보존·이관 | 위키를 보존형 retarget 의미로 정정하거나 명시적 drain-only gate와 회수 절차 구현 | direct-audit:facility-growth |
| WIM-058 | different | 아이템 1,075개의 공개 기준 가격 | 현재 ItemDefinitionSO 권위와 대조하면 198개 가격이 다르고 무게·한 칸 적재 2,150필드는 일치 | 가격 권위를 다시 투영하거나 의도된 별도 공개 가격이면 그 변환 공식을 명시 | direct-audit:entity-item-facts |
| WIM-059 | different | 일반 시설의 공개 설명에 현재 건설 WU와 전체 물리 BOM을 표시 | 공개 일반 시설 347개 중 144개의 요약이 현재 BuildingSO와 다름: WU 103개, 재료 수량 42개이며 한 시설이 양쪽에 중복 | 현재 BuildingSO에서 건설 요약을 다시 투영하고 BOM 생략 정책은 별도 문서 공백으로 유지 | direct-audit:entity-facility-summary |
| WIM-060 | different | 기후 5개의 `annualAmplitudeC`를 연교차로 설명 | 런타임은 평균에서 위아래로 움직이는 계절 기온 진폭으로 사용하므로 날씨·잡음 제외 연교차는 값의 2배 | 라벨을 계절 기온 진폭으로 고치거나 연교차 표시값을 2배로 계산 | direct-audit:entity-climate-summary |
| WIM-061 | partial | 경계 신호 나팔이 침입과 화재 위험을 주민에게 알림 | 침입은 Security 시설·물리 나팔로 집결+6초/내구도1 소비가 작동하지만 화재 production consumer가 없음 | 나팔을 침입 집결 전용으로 위키 정정하고 화재는 독립 화재 대응 AI가 처리 | DIFF-046 |
| WIM-062 | partial | 식사·상점·의료·공연 물품을 서비스 장소까지 운반 후 소비 | 의료는 등록 창고의 Stored medicine/biological stack1개를 그 자리에서 physical Sink하며 의료시설 도착·운반이 없음 | 의료 재료 예약→운반→시설 buffer/도착 확인→소비를 구현하거나 공개 설명을 실제 창고 직접 소비로 정정 | DIFF-047 |
| WIM-063 | different | 공개 current 시설 집합과 일일 시설 상점의 active 후보가 일치 | `deprecatedCompatibilityAsset:1`, `unlocked:0`인 P1 종족선호 시설14개가 root/runtime catalog에 남고 daily filter는 deprecated/unlocked를 보지 않아 star<=2인13개를 판매·unlock·facility-kit화할 수 있음 | old-save numeric lookup은 유지하되 run-start/daily/basic offer 모집단에서 deprecated compatibility를 제외하고, current modular affinity가 필요하면 별도 active 자산으로 재설계 | direct-audit:facility-shop-deprecated |

## 구현 보완 집계에서 제외한 항목

WIM-024, WIM-025, WIM-027은 현재까지 확인한 근거상 구현 누락이 아니라 위키 설명 부족이다. `missing-register.*`의 문서 보완 소유자와 중복 집계한다.

`continuity`의 section/version/dependency preflight, detached staging, cross-section validation, participant rollback과 aggregate root publish는 현재 구조와 일치해 새 WIM으로 세지 않았다. 상세 근거는 [저장 연속성·운영 병목 진단 대조](continuity-world-state-runtime-review.md)에 있다.

`production`의 작업자별 기여·물리 WIP·prepared output·출력 공간 대기·commit/acknowledgement·저장 복원 계약은 현재 구조와 일치한다. 품질 공식과 반복 XP도 일치하며, 자동 품질 상한 미적용은 기존 WIM-006으로 유지한다. 새 차이는 예상 시도 자원 경고 WIM-056 하나다. 상세 근거는 [생산·품질·공급 대조](production-wiki-runtime-review.md)에 있다.

`facility-growth`의 계보 후보 검증, 운영 기록, 개체 세대·촉매·작업·활성/휴면, 물리 포장 이전은 현재 구조와 일치한다. 합성만 위키의 drain-only 설명과 실제 보존형 retarget 의미가 달라 WIM-057로 분리했다. 상세 근거는 [시설 성장 대조](facility-growth-wiki-runtime-review.md)에 있다.

공개 아이템 1,075개의 기초 facts 3,225개는 전수 대조했다. 무게·한 칸 적재 2,150개는 현재 권위와 일치하지만 기준 가격 198개가 달라 WIM-058로 묶었다. 개별 ID·양쪽 값은 [아이템 기초 facts 기계 판독 상세](item-basic-facts-review.json)에 있다.

공개 시설의 구조화 facts와 relations는 현재 재생성 결과와 일치하지만 기존 설명 문자열에는 오래된 건설 비용이 남아 있다. 일반 시설 347개 중 144개가 현재 BuildingSO와 달라 WIM-059로 묶었고, 전체 행은 [시설 건설 비용 전수 대조](facility-construction-review.md)와 [기계 판독 원장](facility-construction-review.json)에 있다.

기후 5개의 원본 평균·진폭 숫자는 현재 정의와 같지만 표시 의미가 런타임 공식과 다르다. 이 의미 불일치는 WIM-060으로 분리했으며 근거는 [기후·날씨·계절 사건 대조](climate-mechanics-review.md)에 있다.

## 기존 전수 범위와 후속 재개 상태

- 공개 엔티티2,905개·facts6,364개·relations3,846개는 [공개 엔티티 전수 대조](public-entity-claims-runtime-review.md)에 56개 원본 타입별 production 소비자와 함께 닫았다.
- WIM-001~060의 기존 source freshness, production caller, 저장/UI 경계와 중복 원인은 [WIM 근거·중복 전수 감사](wim-evidence-and-dedup-review.md)에 닫았다. 후속 WIM-061은 [보안 시설·경계 신호 나팔 대조](security-facility-signal-horn-review.md), WIM-062는 [서비스 허브·지원시설·의료 물류 대조](service-room-runtime-review.md), WIM-063은 [종족 선호·P1 상점 노출 대조](species-affinity-runtime-review.md)에 추가했다.
- 기존 구조화 정적 모집단의 미검토는0이지만 narrative description 의미 소비 후속은 진행 중이다. Play Mode는 읽기 전용 감사 범위 밖이며 각 항목 구현 후 focused regression으로 재개방한다.

`work-references.json`의 정성 업무 42건은 31개 runtime work type과 합성 도메인 작업에 모두 연결했다. 31↔42의 수 차이는 누락 수가 아니며, 외교 등 닫히지 않은 기능은 기존 WIM에 귀속했다. 상세 근거는 [작업 참조 42건 대조](work-reference-runtime-review.md)에 있다.
