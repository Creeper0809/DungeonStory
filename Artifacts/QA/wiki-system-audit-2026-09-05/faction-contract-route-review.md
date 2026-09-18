# 세력 계약·교역 경로 위키 교차 감사

상태: 정적 원본 대조 완료. Unity 실행·실제 UI 클릭·저장 왕복은 수행하지 않았다.

## 결론

- 작성 세력 계약은 6세력 × 3종 = 18개이고 공개 계약 페이지도 18개다. 요구 물품·수량은 18/18 일치한다.
- 공개 페이지는 성공 효과 2개·실패 효과 1개라는 개수만 보이며, 기한·계약 종류·효과 종류/대상/값·행정 시설/인장 조건을 보여주지 않는다.
- 계약 도메인과 범용 alert dispatcher는 존재하지만, 작성 계약 수락/결과 action ID를 실제로 생산하는 비Editor 호출자는 발견하지 못했다. Editor debug scenario만 직접 호출한다.
- 완료 시 요구 물자는 계약 목적지에 운반되지 않는다. 전체 월드 스택에서 가능한 exact stack을 ID 순으로 예약해 그 자리에서 원자 소비한다. 위키의 계약 물류·운반 경로 설명과 다르다.
- 신뢰도 기반 교역/보급/지원군은 별도 FactionRuntime 경로이며 실제 월드맵 UI 호출자가 있다. 교역은 유료, 보급은 동맹 혜택 예산, 지원군은 의무 증표를 사용한다.

## 작성 계약 18개

| stable ID | 종류 | 기한 | 요구 물품 | 성공 | 실패 |
| --- | --- | ---: | --- | --- | --- |
| faction-contract:beastkin:crisis | Crisis | 7일 | medicine:standard ×3 | 우호 +8, 의무증표 +1 | 원한 +12 |
| faction-contract:beastkin:strategic | Strategic | 45일 | equipment-item:shield:tower ×4 | 우호 +15, 의무증표 +1 | 원한 +7 |
| faction-contract:beastkin:supply | Supply | 20일 | food:salted-meat-stew ×8 | 우호 +8, 의무증표 +1 | 원한 +7 |
| faction-contract:demon:crisis | Crisis | 7일 | component:rune-conductor ×1 | 우호 +8, 의무증표 +1 | 원한 +12 |
| faction-contract:demon:strategic | Strategic | 45일 | tool:administrative-seal ×18 | 우호 +15, 의무증표 +1 | 원한 +7 |
| faction-contract:demon:supply | Supply | 20일 | material:paper ×8 | 우호 +8, 의무증표 +1 | 원한 +7 |
| faction-contract:golem:crisis | Crisis | 7일 | tool:maintenance-kit ×1 | 우호 +8, 의무증표 +1 | 원한 +12 |
| faction-contract:golem:strategic | Strategic | 45일 | component:rune-conductor ×8 | 우호 +15, 의무증표 +1 | 원한 +7 |
| faction-contract:golem:supply | Supply | 20일 | component:machine-parts ×2 | 우호 +8, 의무증표 +1 | 원한 +7 |
| faction-contract:harpy:crisis | Crisis | 7일 | medicine:antiseptic ×6 | 우호 +8, 의무증표 +1 | 원한 +12 |
| faction-contract:harpy:strategic | Strategic | 45일 | tool:weather-observation-kit ×6 | 우호 +15, 의무증표 +1 | 원한 +7 |
| faction-contract:harpy:supply | Supply | 20일 | material:rope ×8 | 우호 +8, 의무증표 +1 | 원한 +7 |
| faction-contract:kobold:crisis | Crisis | 7일 | tool:field-repair-kit ×1 | 우호 +8, 의무증표 +1 | 원한 +12 |
| faction-contract:kobold:strategic | Strategic | 45일 | component:engineering-drawing ×18 | 우호 +15, 의무증표 +1 | 원한 +7 |
| faction-contract:kobold:supply | Supply | 20일 | component:machine-parts ×2 | 우호 +8, 의무증표 +1 | 원한 +7 |
| faction-contract:myconid:crisis | Crisis | 7일 | supply:fungicide ×4 | 우호 +8, 의무증표 +1 | 원한 +12 |
| faction-contract:myconid:strategic | Strategic | 45일 | tool:weather-observation-kit ×6 | 우호 +15, 의무증표 +1 | 원한 +7 |
| faction-contract:myconid:supply | Supply | 20일 | resource:cave-mushroom ×16 | 우호 +8, 의무증표 +1 | 원한 +7 |

공개 facts 합계는 36개, relations 합계는 18개다. relations의 요구 수량 불일치는 0개다. facts의 효과 개수는 맞지만 효과 의미와 값은 없다.

## 운영 진입점 차이

`V20CampaignRuntime.TryAcceptContract/TryResolveContract`와 `V21ContentAlertChoiceActionDispatcher`는 계약 요청을 처리할 수 있다. 그러나 `V21ContentAlertActionIds.FactionContractAccept/FactionContractOutcome`의 비Editor 호출자는 선언 외 0개이고, 계약 action kind 문자열도 같은 파일 내부에서만 확인됐다. 따라서 현재 정적 근거로는 실제 UI/AI가 작성 계약 18개를 제시·수락·완료시키는 연결이 없다.

수락/결과는 Administration capability 시설과 물리 행정 인장을 요구한다. 이 조건도 위키 계약 절에 없다. 계약 상태는 세력별 활성 1개, 완료/실패 재수락 금지, 수락일+기한, 기한이 지난 다음 날 실패로 저장된다.

## 물류 설명과 실제 소비

위키는 계약 물자를 예약하고 운반할 인원과 경로를 확인한다고 설명한다. 작성 계약의 실제 성공 경로는 다음과 같다.

1. world snapshot이 모든 양수 스택 수량을 item ID별 합산한다.
2. preflight가 Forbidden이 아니고 AvailableQuantity>0인 스택을 StackId ordinal 순으로 고른다.
3. DirectPlayerOrder reservation을 잡는다.
4. 성공효과 commit에서 `TryConsumeReserved`로 해당 스택을 원자 소비한다.

계약 전용 목적지, 배송 주문, 운반자, 계약 물류 경로는 이 체인에 없다. 지역 공급 계약의 실제 집결·운반 경로와 혼동하면 안 된다.

## 별도 세력 교역·보급·지원군

| 세력 | 교역 재사용 | 보급 재사용 | 지원군 | 교역 화물 | 보급 화물 |
| --- | ---: | ---: | ---: | --- | --- |
| faction:dungeon:beastkin | 7일 | 20일 | 10일 | resource:meat×8, resource:hide×5, feed:dog-food×6 | food:preserved-ration×12, material:leather×8, feed:hay×10 |
| faction:dungeon:demon | 23일 | 84일 | 10일 | resource:mana-crystal×6, craft:gold-ornament×3, craft:ritual-reagent×4 | drug:mana-awakener×6, material:blacksteel-ingot×5, craft:ritual-reagent×8 |
| faction:dungeon:golem | 27일 | 99일 | 10일 | material:iron-ingot×8, material:stone-block×10, resource:mana-crystal×4 | material:steel-ingot×10, material:blacksteel-ingot×5, resource:mana-crystal×8 |
| faction:dungeon:harpy | 16일 | 22일 | 10일 | resource:feather×10, ammo:arrow-iron×12, ammo:bolt-bone×8 | ammo:arrow-steel×16, ammo:bolt-steel×12, resource:feather×14 |
| faction:dungeon:kobold | 25일 | 49일 | 10일 | resource:iron-ore×10, ammo:bolt-iron×12, material:iron-ingot×5 | ammo:bolt-steel×16, material:steel-ingot×8, material:lumber×10 |
| faction:dungeon:myconid | 22일 | 38일 | 10일 | resource:cave-mushroom×10, material:compost×8, medicine:herbal-poultice×5 | medicine:antidote×8, medicine:standard×8, food:mushroom-soup×10 |

6세력 모두 교역 policy는 `faction-economy:paid-market-purchase`, 보급 policy는 `faction-economy:alliance-benefit`이다. 위키의 최소 7/20/10일은 실제 하한과 일치하지만 세력별 산출값은 공개되지 않는다. “세력의 무상 화물”이라는 표현과 달리 교역 상단은 견적 금화를 먼저 지불하고, 게시 실패에는 exact 환불 복구 경계가 있다. 보급만 동맹 혜택 예산을 사용한다.

해금은 교역 rapport≥20/grievance≤70, 보급 ≥50/≤40, 지원군 ≥70/≤25 + 동맹 프로젝트 + 의무증표>0이다. 월드맵 패널에서 호의/교역/보급/동맹프로젝트/지원군 명령으로 연결된다.

## 판정과 범위

- 도감 설명 누락: 18개 계약의 기한·효과 값·행정 조건.
- 구현 연결 차이: authored 계약 수락/결과 producer 부재.
- 실행 의미 차이: authored 계약은 목적지 배송이 아니라 전역 exact-stack 직접 소비.
- 경제 설명 불일치: 교역 화물은 유료이며 보급 화물과 권위가 다름.
- 설명 누락: 6세력 36개 화물 line, 개별 cooldown, 지불/동맹예산/환불 경계.

이는 일반 세력 서비스 전체가 미구현이라는 뜻이 아니다. 작성 계약18개와 신뢰도 기반 route 서비스, 지역 공급 계약은 서로 다른 세 시스템이다.

## 근거와 한계

KB query는 stale2429/반환0이어서 생성 인덱스를 근거로 쓰지 않았다. 직접 원본은 evidence JSON에 SHA-256으로 고정한다. 해시는 완독이나 실행 증거가 아니다.

