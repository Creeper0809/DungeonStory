# 연구·청사진·시설 조건 대조

상태: 부분 감사. 연구180개 본문·필드와 실제 대기열/보관/작업 경로를 대조했으나 전체 연구 효과·저장·실제 UI 실행 검증은 미완료다. 상세 원본 경로는 [JSON](research-projection-review.json)에 있다.

- 작성 연구180개 전부가 콘텐츠 카탈로그에 연결되어 있다. 공개 도감180개의 필요 작업은 모두 현재 원본과 일치한다.
- 도감facts는 필요 작업과 연구 분야뿐이다. 시설 요구와 청사진 규칙, 동시 인원은 노출되지 않는다. 현재 작성 동시 인원은 모두1명이다.
- 청사진 불필요173개, 필수4개, 선행우회3개다. 없는 청사진을 임의로 모든 연구의 필수 재료로 적으면 안 된다.

## 청사진7개

| 연구 | 설계도 | 규칙 | 현재 설명 문제 |
| --- | --- | --- | --- |
| 비전 연구 | 비전 연구 설계도 (6104) | 필수 | 즉시 해금으로 잘못 설명 |
| 상권 통합 | 상권 통합 설계도 (6191) | 선행 우회 | 희귀 조합식으로 잘못 설명 |
| 상업 확장 | 상업 확장 설계도 (6101) | 필수 | 즉시 해금으로 잘못 설명 |
| 전술 지휘 | 전술 지휘 설계도 (6192) | 선행 우회 | 희귀 조합식으로 잘못 설명 |
| 생활 지원 | 생활 지원 설계도 (6103) | 필수 | 즉시 해금으로 잘못 설명 |
| 비전 공명 | 비전 공명 설계도 (6193) | 선행 우회 | 희귀 조합식으로 잘못 설명 |
| 요새화 | 요새화 설계도 (6102) | 필수 | 즉시 해금으로 잘못 설명 |

동일 설명이 청사진의 시설형·아이템형 도감14개에 반복된다. 시설형에는 연구1개가 '관련'으로 연결되지만 아이템형7개에는 연구관계가 없다. 물리 청사진을 보관대에 운반하고 연구를 진행하는 방식으로 설명해야 한다.

## 연구시설의 제공 기능

| 시설 | 제공 기능 | 별도 보관 기능 | 공개 도감 |
| --- | --- | --- | --- |
| 연구실 (16) | 기초 2, 기록 1, 고급 1 | 없음 | 같은ID/제목 미발견, 공개제외 원인 미확인 |
| 설계판 (1035) | 설계 1 | 없음 | 있음, 연구기능 수치 없음 |
| 표본보관장 (1034) | 표본 1 | 없음 | 있음, 연구기능 수치 없음 |
| 시약선반 (1033) | 시약 1 | 없음 | 있음, 연구기능 수치 없음 |
| 연구용책장 (1032) | 기록 1 | 청사진 보관량8 | 있음, 연구기능 수치 없음 |
| 연금술작업대 (1031) | 기초 1, 비전 1 | 없음 | 있음, 연구기능 수치 없음 |
| 연구책상 (1030) | 기초 1 | 없음 | 있음, 연구기능 수치 없음 |
| 연금대 (1091) | 기초 1, 시약 1, 비전 1 | 없음 | 있음, 연구기능 수치 없음 |

능력은 정착지 전체에서 합산하며 연구인원 정원이나 일반 저장량과 다르다. 유효한 연구역할의 닫힌 방과 문, 시설의 활성·파손 여부를 검사하고 전력소비 시설은 전원이 있어야 한다. 자체완결 방은 이 합산에서 제외한다. 보관대 유효성은 별도 함수이므로 동일한 파손·전력 조건을 임의로 덧붙이지 않는다.

연구실(id16)은 원본 카탈로그와 GameplayScene에 연결되어 있다. 공개 도감 누락/변환 원인을 추가 조사해야 하며 미사용 시설로 확정하지 않았다.

## 연구별 요구 조건180개

| 연구 | 작업 WU | 시설 기능 요구 | 청사진 |
| --- | ---: | --- | --- |
| 퇴비·윤작 (research:agriculture:compost) | 28 | 기초 1 | 불필요 |
| 품종 개량 (research:agriculture:cultivar-breeding) | 546 | 기초 1, 설계 1, 고급 1 | 불필요 |
| 외부 경작 (research:agriculture:field) | 28 | 기초 1 | 불필요 |
| 야생 채집 (research:agriculture:gathering) | 17 | 기초 1 | 불필요 |
| 온실 원예 (research:agriculture:greenhouse-horticulture) | 255 | 기초 1, 설계 1, 고급 1 | 불필요 |
| 실내 재배 (research:agriculture:indoor) | 60 | 기초 1, 설계 1 | 불필요 |
| 관개 (research:agriculture:irrigation) | 42 | 기초 1, 설계 1 | 불필요 |
| 생물계절학과 종자 선별 (research:agriculture:phenology) | 268 | 기초 1, 설계 1, 고급 1 | 불필요 |
| 토양 순환과 작물 보호 (research:agriculture:soil-cycles) | 697 | 기초 1, 설계 1, 고급 1 | 불필요 |
| 지하 자급 (research:agriculture:subterranean) | 84 | 기초 1, 설계 1 | 불필요 |
| 비전 연구 (research:arcane:advanced) | 42 | 기초 1, 기록 1, 비전 1 | 필수 |
| 연금 가공 (research:arcane:alchemy) | 28 | 기초 1, 기록 1, 비전 1 | 불필요 |
| 기록 체계 (research:arcane:records) | 17 | 기초 1, 기록 1 | 불필요 |
| 비전 공명 (research:arcane:resonance) | 60 | 기초 1, 기록 1, 비전 1 | 선행 우회 |
| 영주 집무 (research:authority:office) | 42 | 기초 1, 기록 1 | 불필요 |
| 장식과 위신 (research:authority:prestige) | 28 | 기초 1 | 불필요 |
| 기본 숙소 (research:authority:quarters) | 17 | 기초 1 | 불필요 |
| 의식 장식 (research:authority:ritual) | 60 | 기초 1, 기록 1 | 불필요 |
| 목욕 영업 (research:bath-business) | 42 | 기초 1, 설계 1 | 불필요 |
| 기후 제어 (research:climate:environment-control) | 437 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 지역 기후학과 시각 항법 (research:climate:regional-climatology) | 3819 | 기초 1, 기록 1, 비전 1, 고급 1 | 불필요 |
| 기상 관측 (research:climate:weather-observation) | 153 | 기초 1, 기록 1, 비전 1, 고급 1 | 불필요 |
| 상업 확장 (research:commerce:expansion) | 42 | 기초 1, 설계 1 | 필수 |
| 창고 구획 (research:commerce:logistics) | 17 | 기초 1 | 불필요 |
| 상업 진열 (research:commerce:retail) | 28 | 기초 1 | 불필요 |
| 상권 통합 (research:commerce:secure-trade) | 60 | 기초 1, 설계 1 | 선행 우회 |
| 피의 흥행 (research:control:blood-show) | 60 | 기초 1, 기록 1 | 불필요 |
| 노역 감독 (research:control:labor) | 28 | 기초 1 | 불필요 |
| 구속 관리 (research:control:restraints) | 17 | 기초 1 | 불필요 |
| 흥행 운영 (research:control:show) | 42 | 기초 1, 기록 1 | 불필요 |
| 제빵 (research:cuisine:baking) | 60 | 기초 1, 설계 1 | 불필요 |
| 제어 발효 (research:cuisine:controlled-fermentation) | 84 | 기초 1, 설계 1 | 불필요 |
| 농산 조리 (research:cuisine:crops) | 17 | 기초 1 | 불필요 |
| 주류 증류·숙성 (research:cuisine:distilling-aging) | 115 | 기초 1, 설계 1, 고급 1 | 불필요 |
| 발효 (research:cuisine:fermentation) | 60 | 기초 1, 설계 1 | 불필요 |
| 주방 위생 (research:cuisine:kitchen-hygiene) | 84 | 기초 1, 설계 1 | 불필요 |
| 호화·보존식 (research:cuisine:lavish) | 84 | 기초 1, 설계 1 | 불필요 |
| 축산 조리 (research:cuisine:livestock) | 42 | 기초 1, 설계 1 | 불필요 |
| 제분·제빵 (research:cuisine:milling) | 28 | 기초 1 | 불필요 |
| 채식 조리 (research:cuisine:vegan) | 42 | 기초 1, 설계 1 | 불필요 |
| 동맹 신호학 (research:defense:alliance-signals) | 153 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 복도 기구학 (research:defense:corridor-mechanisms) | 60 | 기초 2, 설계 1 | 불필요 |
| 요새화 (research:defense:fortification) | 28 | 기초 1, 설계 1 | 필수 |
| 사격 방책 (research:defense:ranged-positions) | 42 | 기초 1, 설계 1 | 불필요 |
| 원격 통제 (research:defense:remote-control) | 84 | 기초 2, 설계 1 | 불필요 |
| 룬 식별 (research:defense:rune-identification) | 84 | 기초 2, 설계 1 | 불필요 |
| 공성 요새화 (research:defense:siege-fortification) | 115 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 방어 보급학 (research:defense:supply) | 42 | 기초 1, 설계 1 | 불필요 |
| 전술 지휘 (research:defense:tactical-command) | 60 | 기초 2, 설계 1 | 선행 우회 |
| 경계 근무 (research:defense:watch) | 17 | 기초 1, 설계 1 | 불필요 |
| 저온 작업 보호 (research:environment:cold-work) | 42 | 기초 1, 설계 1 | 불필요 |
| 룬 단열학 (research:environment:rune-insulation) | 84 | 기초 1, 설계 1 | 불필요 |
| 방어구 재단 (research:equipment:armor-tailoring) | 60 | 기초 1, 설계 1 | 불필요 |
| 관절식 판금 (research:equipment:articulated-plate) | 115 | 기초 1, 설계 1, 고급 1 | 불필요 |
| 탄도학 (research:equipment:ballistics) | 255 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 흑색화약 배합 (research:equipment:black-powder) | 191 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 궁시 제작학 (research:equipment:bowyery) | 60 | 기초 1, 설계 1 | 불필요 |
| 야전 정비학 (research:equipment:field-maintenance) | 84 | 기초 2, 설계 1 | 불필요 |
| 점화 기구학 (research:equipment:ignition-mechanisms) | 255 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 산업 계측학 (research:equipment:industrial-metrology) | 437 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 장비 계보 결속 (research:equipment:lineage-binding) | 546 | 기초 1, 기록 1, 비전 1, 고급 1 | 불필요 |
| 사슬 편조 (research:equipment:mail-weaving) | 84 | 기초 1, 설계 1 | 불필요 |
| 재료 시험학 (research:equipment:material-testing) | 255 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 기계식 투사 (research:equipment:mechanical-projectiles) | 84 | 기초 2, 설계 1 | 불필요 |
| 모듈식 장비 골격 (research:equipment:modular-frames) | 437 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 동력 보조 갑주 (research:equipment:powered-armor) | 546 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 정밀 부품 장착 (research:equipment:precision-fitting) | 328 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 내압 화기와 방폭 (research:equipment:pressure-barrels) | 655 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 시제품 공학 (research:equipment:prototype-engineering) | 328 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 유물 부품 감정과 복원 (research:equipment:relic-appraisal) | 306 | 기초 1, 기록 1, 비전 1, 고급 1 | 불필요 |
| 룬 부품 조율 (research:equipment:rune-module-tuning) | 546 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 탄약 규격화 (research:equipment:standard-ammunition) | 328 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 무기 형식학 (research:equipment:weapon-patterns) | 42 | 기초 1, 설계 1 | 불필요 |
| 숯가마 (research:forestry:charcoal) | 42 | 기초 1, 설계 1 | 불필요 |
| 실내 균목림 (research:forestry:fungal) | 84 | 기초 1, 설계 1 | 불필요 |
| 벌목 (research:forestry:logging) | 28 | 기초 1 | 불필요 |
| 제재 (research:forestry:sawmill) | 28 | 기초 1 | 불필요 |
| 벌목 도구 (research:forestry:tools) | 17 | 기초 1 | 불필요 |
| 목재 처리 (research:forestry:treated) | 60 | 기초 1, 설계 1 | 불필요 |
| 통제 유전 (research:genetics:controlled-heredity) | 819 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 교차계통 안정화 (research:genetics:cross-lineage-stabilization) | 4364 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 유전 기록과 형질 분석 (research:genetics:hereditary-records) | 691 | 기초 1, 기록 1, 비전 1, 고급 1 | 불필요 |
| 격리 의학 (research:health:isolation-medicine) | 255 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 병원체 관찰과 면역 혈청학 (research:health:pathogen-observation) | 519 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 예방접종과 유행병 통제 (research:health:vaccination) | 982 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 환대 운영 (research:hospitality-operations) | 42 | 기초 1, 기록 1 | 불필요 |
| 방 배정과 가족 생활구획 (research:housing:room-assignment) | 237 | 기초 1, 기록 1 | 불필요 |
| 번식 관리 (research:husbandry:breeding) | 60 | 기초 1, 설계 1 | 불필요 |
| 야생 포획 (research:husbandry:capture) | 17 | 기초 1 | 불필요 |
| 사료·깔짚 (research:husbandry:feed) | 42 | 기초 1, 설계 1 | 불필요 |
| 계절 번식 (research:husbandry:seasonal-breeding) | 191 | 기초 1, 설계 1, 고급 1 | 불필요 |
| 선별 사육 (research:husbandry:selective) | 84 | 기초 1, 설계 1 | 불필요 |
| 축사 관리 (research:husbandry:stable) | 28 | 기초 1 | 불필요 |
| 길들이기 (research:husbandry:taming) | 42 | 기초 1, 설계 1 | 불필요 |
| 동력 보조 가공 (research:industry:assisted-processing) | 328 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 자동 주문과 재고 감지 (research:industry:automatic-bills) | 873 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 자동 위생과 산업 안전 (research:industry:automatic-sanitation) | 873 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 차단기와 산업 정비 (research:industry:breakers) | 582 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 컨베이어와 물류 포트 (research:industry:conveyor) | 582 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 심연 공장 (research:industry:dark-foundry) | 546 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 산업 조명 (research:industry:electric-lighting) | 328 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 전기 제련과 산업 냉각 (research:industry:electric-smelting) | 655 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 분기·필터·우선순위 제어 (research:industry:junctions) | 910 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 승강·오버플로·고속 운송 (research:industry:lifts) | 1200 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 라인 균형과 방어 보급 (research:industry:line-balancing) | 873 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 마나 동력과 룬 전력망 (research:industry:mana-power) | 764 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 공장 공학 (research:industry:powered-tools) | 700 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 정밀 자동화 (research:industry:precision) | 437 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 룬 자동화 (research:industry:rune-automation) | 546 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 증기 동력과 배전 (research:industry:steam-power) | 446 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 산업 저장과 변압 (research:industry:storage) | 582 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 수차 발전 (research:industry:waterwheel) | 255 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 계절 역법 (research:life:seasonal-calendar) | 84 | 기초 1, 기록 1, 비전 1, 고급 1 | 불필요 |
| 이형 개조 (research:medical:aberrant-augmentation) | 153 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 해부학 (research:medical:anatomy) | 42 | 기초 1, 표본 1 | 불필요 |
| 조류 보철학 (research:medical:avian-prosthetics) | 84 | 기초 1, 표본 1 | 불필요 |
| 혈액 회춘 (research:medical:blood-rejuvenation) | 2182 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 혈술 개조 (research:medical:bloodcraft-augmentation) | 115 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 구성체 핵 공학 (research:medical:construct-core-engineering) | 115 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 구성체 핵 정비 (research:medical:construct-core-maintenance) | 84 | 기초 1, 표본 1 | 불필요 |
| 노인의학과 만성 관리 (research:medical:geriatric-medicine) | 582 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 노인학과 생물학적 연령 계측 (research:medical:gerontology) | 519 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 마핵 공학 (research:medical:mana-core-engineering) | 115 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 균사 접목학 (research:medical:mycelial-grafting) | 84 | 기초 1, 표본 1 | 불필요 |
| 장기 보존 (research:medical:organ-preservation) | 84 | 기초 1, 표본 1 | 불필요 |
| 장기 재생 (research:medical:organ-regeneration) | 3273 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 보철 공학 (research:medical:prosthetics) | 84 | 기초 1, 표본 1 | 불필요 |
| 의료 접수 (research:medical-reception) | 42 | 기초 1, 표본 1 | 불필요 |
| 재생 배양 (research:medical:regenerative-culture) | 2182 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 생식 의학 (research:medical:reproductive-medicine) | 255 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 룬 동면 (research:medical:rune-hibernation) | 2182 | 기초 1, 기록 1, 비전 1, 고급 1 | 불필요 |
| 점액 생체공학 (research:medical:slime-bioengineering) | 84 | 기초 1, 표본 1 | 불필요 |
| 외과술 (research:medical:surgery) | 60 | 기초 1, 표본 1 | 불필요 |
| 시간 고정 (research:medical:temporal-stasis) | 5455 | 기초 1, 기록 1, 비전 1, 고급 1 | 불필요 |
| 트라우마 의학 (research:medical:trauma-medicine) | 255 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 전신 재생 (research:medical:whole-body-regeneration) | 5455 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 이종 이식 (research:medical:xenotransplant) | 115 | 기초 1, 표본 1, 고급 1 | 불필요 |
| 고급 단조 (research:metallurgy:advanced) | 60 | 기초 1, 설계 1 | 불필요 |
| 흑강 (research:metallurgy:blacksteel) | 84 | 기초 1, 설계 1 | 불필요 |
| 철제 가공 (research:metallurgy:iron) | 28 | 기초 1 | 불필요 |
| 귀금 세공 (research:metallurgy:precious) | 84 | 기초 1, 설계 1 | 불필요 |
| 원시 단조 (research:metallurgy:primitive) | 17 | 기초 1 | 불필요 |
| 제강 (research:metallurgy:steel) | 42 | 기초 1, 설계 1 | 불필요 |
| 심부 채굴 (research:mining:deep) | 60 | 기초 1, 설계 1, 고급 1 | 불필요 |
| 마나 시추 (research:mining:mana) | 84 | 기초 1, 설계 1 | 불필요 |
| 채석장 (research:mining:quarry) | 28 | 기초 1 | 불필요 |
| 광석 선별 (research:mining:sorting) | 42 | 기초 1, 설계 1 | 불필요 |
| 석재 가공 (research:mining:stonecutting) | 42 | 기초 1, 설계 1 | 불필요 |
| 노천 채석 (research:mining:surface) | 17 | 기초 1 | 불필요 |
| 고급 약리 (research:pharmacology:advanced) | 84 | 기초 1, 시약 1, 비전 1 | 불필요 |
| 진통·마취 (research:pharmacology:anesthesia) | 60 | 기초 1, 시약 1, 비전 1 | 불필요 |
| 소독·붕대 (research:pharmacology:antiseptic) | 28 | 기초 1, 시약 1 | 불필요 |
| 증류 (research:pharmacology:distillation) | 42 | 기초 1, 시약 1 | 불필요 |
| 약초학 (research:pharmacology:herbalism) | 17 | 기초 1, 시약 1 | 불필요 |
| 각성제 (research:pharmacology:stimulants) | 60 | 기초 1, 시약 1, 비전 1 | 불필요 |
| 기초 배관과 펌프 급수 (research:plumbing:basics) | 130 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 정수와 재이용 (research:plumbing:reuse) | 115 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 룬 정화 순환 (research:plumbing:rune-purification) | 153 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 하수 처리와 수세 위생 (research:plumbing:sewer) | 228 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 서비스 자동화 (research:service-automation) | 328 | 기초 2, 설계 1, 고급 1 | 불필요 |
| 식당 운영학 (research:service:dining-operations) | 84 | 기초 1, 설계 1 | 불필요 |
| 서비스 동선 (research:service-flow) | 28 | 기초 1 | 불필요 |
| 경력 기록 (research:society:career-records) | 115 | 기초 1, 기록 1 | 불필요 |
| 아동 교육과 도제 제도 (research:society:child-education) | 408 | 기초 1, 기록 1 | 불필요 |
| 시신 관리와 장례 의식 (research:society:corpse-care) | 306 | 기초 1 | 불필요 |
| 세대 관리와 보호자 승계 (research:society:generation-management) | 691 | 기초 1, 기록 1 | 불필요 |
| 가구 기록과 영아 돌봄 (research:society:household-records) | 199 | 기초 1, 기록 1 | 불필요 |
| 은퇴와 멘토 제도 (research:society:retirement) | 628 | 기초 1, 기록 1 | 불필요 |
| 야전 식량학 (research:survival:field-rations) | 60 | 기초 1 | 불필요 |
| 의료 회복 (research:survival:medical) | 60 | 기초 1, 표본 1 | 불필요 |
| 식량 보존 (research:survival:preservation) | 42 | 기초 1 | 불필요 |
| 기초 위생 (research:survival:sanitation) | 17 | 기초 1 | 불필요 |
| 계절 저장 (research:survival:seasonal-storage) | 153 | 기초 1 | 불필요 |
| 생활 지원 (research:survival:support) | 28 | 기초 1 | 필수 |
| 몽직물 (research:textile:dreamweave) | 84 | 기초 1, 설계 1 | 불필요 |
| 섬유 가공 (research:textile:fiber) | 17 | 기초 1 | 불필요 |
| 층상 방어구 (research:textile:layered) | 60 | 기초 1, 설계 1 | 불필요 |
| 룬가죽 (research:textile:rune-leather) | 60 | 기초 1, 설계 1 | 불필요 |
| 재봉 (research:textile:tailoring) | 42 | 기초 1, 설계 1 | 불필요 |
| 무두질 (research:textile:tanning) | 28 | 기초 1 | 불필요 |

## 함께 확인한 실행 규칙

- 대기열 제거는 진행을 지우지 않는다. 활성 연구를 옮길 수 없고 선행 순서를 뒤집는 이동도 거부한다. 조건이 막힌 연구는 중단으로 남겨 다음 실행 가능한 연구를 찾는다.
- 기억 잔재는1개와24WU로 도감 단서(총8개)를 순서대로 분석하거나 지역 정보망 피해를10 증가시킨다(최대100). 일반 연구가 활성화되어 있으면 잔재 작업보다 먼저 처리된다.
- 기억 잔재 도감 분석은1건, 정찰은 동일지역1건씩 예약할 수 있다. 완료 전에 대상이 무효가 되면 실물을 시설 앞에 반환한다. 시설이 사라지면 재배정하되 진행WU는 유지한다.
- 연구 화면 예상기간은99WU/일을 사용하는 표시식이고 가이드의45WU/일 기준과 다르다. 실제 실행량으로 어느 한쪽을 단정하지 않고 후속 대조 대상으로 분리한다.

공개 위키·게임 원본 변경 없음. 밸런스 영향 없음. 모든 시스템 전수 감사 완료를 뜻하지 않는다.
