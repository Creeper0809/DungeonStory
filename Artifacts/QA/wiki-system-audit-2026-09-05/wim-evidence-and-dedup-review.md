# WIM-001~063 근거 최신성·중복 감사

상태: **기존 구조화 전수 검증 완료, narrative 후속 진행 중**. 기준 branch `main`, HEAD `c03d2e0a1c68067de48840f3851b388483966a6e`.

## 구조·최신성 검사

- WIM JSON/Markdown은 각각 63행, 고유 ID 63개, `WIM-001~063` 연속, 양쪽 집합 차이 0이다.
- 분류 합계는 missing 19, partial 23, different 16, documentation-only 3이며 구현 보완 대상은 58개다.
- WIM-001~044는 DIFF-001~044와 정확히 1:1 연결된다. 차이 보고서는 DIFF-047까지47개이며 DIFF-045는 WIM-045, DIFF-046은 WIM-061, DIFF-047은 WIM-062에 통합됐다.
- WIM-045~060의 `direct-audit:*` 11종은 아래 대응 보고서가 모두 존재한다.
- 기존 대응 보고서11개가 인용한 현재 저장소 원본 경로325개는 누락0으로 검증됐다. 후속 security 보고서의 직접 원본16개도 누락0이며 통합 unique path 수와 source-set digest 재계산은 진행 중이다.
- 경로+파일 SHA-256을 ordinal 순으로 합친 source-set digest는 `e665a1a3938b109545fbbd1334e88f7e19972f5e9514e5b6f77b6650177dc2c3`이다.
- 이번 감사에서 `Assets`, `wiki`, `docs`, `Tools`의 변경 diff는 0이다. 감사 산출물과 `.planning`만 변경했다.

직접 감사 source 대응:

| source | 근거 보고서 | SHA-256 앞 16자 |
| --- | --- | --- |
| `direct-audit:spaces` | `room-and-door-runtime-review.md` | `bf3e96206a1e001e` |
| `direct-audit:world-state` | `continuity-world-state-runtime-review.md` | `38b224dd42c1b0fc` |
| `direct-audit:invasions` | `invasion-wiki-runtime-review.md` | `e29ae6cf249082fd` |
| `direct-audit:milestones-meta` | `milestones-runs-meta-runtime-review.md` | `0e684b6e92444210` |
| `direct-audit:expeditions` | `expedition-wiki-runtime-review.md` | `d128e19977c56e9b` |
| `direct-audit:captivity` | `captivity-wiki-runtime-review.md` | `2d37013b6cba099c` |
| `direct-audit:production-quality` | `production-wiki-runtime-review.md` | `9cc99c3db0960d04` |
| `direct-audit:facility-growth` | `facility-growth-wiki-runtime-review.md` | `2268417a1b572b0b` |
| `direct-audit:entity-item-facts` | `item-basic-facts-review.json` | `25a62a327d515f77` |
| `direct-audit:entity-facility-summary` | `facility-construction-review.md/.json` | `a11f6ee3aec6a1ff` / `9d41cda4c5b3fe5a` |
| `direct-audit:entity-climate-summary` | `climate-mechanics-review.md/.json` | `56668ded6d1fa814` / `6bf553c2dd7f36c0` |
| `DIFF-046` | `security-facility-signal-horn-review.md` | `9d8936acd3e8df55` |
| `DIFF-047` | `service-room-runtime-review.md` | `0ebb6b823a09b66d` |
| `direct-audit:facility-shop-deprecated` | `species-affinity-runtime-review.md` | `543ac65df6d3497b` |

## WIM별 production·상태·UI 증거 수준

`없음`은 해당 위키 약속의 production 소비자를 정적 역검색에서 찾지 못했다는 뜻이고, 시스템 전체가 없다는 뜻은 아니다. `부분`은 인접 권위와 실행은 있으나 약속한 연결이 빠졌다는 뜻이다. Play Mode는 이번 읽기 전용 감사에서 전부 미실행이므로 정적 판정과 분리한다.

| ID | production 판정 | 상태·저장 경계 | UI·관찰 경계 | 중복 귀속 |
| --- | --- | --- | --- | --- |
| WIM-001 | 작업복 자동착용만 있음 | apparel state 저장 있음 | 일반 환복 진입점 없음 | 의복-환복 |
| WIM-002 | 원단 projector 소비 없음 | 파생값, 저장 불필요 | 보호 표시와 실제 계산 단절 | 의복-소재 |
| WIM-003 | 세탁·수선 writer만 있음 | apparel component 저장 있음 | 환경 누적 관찰 부족 | 의복-상태 |
| WIM-004 | 온도·노출 일부만 있음 | 종족 정의 불변 | 미적용 필드 표시 부족 | 종족-환경 |
| WIM-005 | 질병 기분·신체만 있음 | 질병 상태 저장 있음 | 직접 작업·이동 영향 없음 | 의료-질병 |
| WIM-006 | 자동 생산은 있음, cap 소비 없음 | 시설 정의 불변 | 목표 품질 제한 미반영 | 생산-품질 |
| WIM-007 | conveyor runtime만 있음 | 목적지 상태 계약 있음 | gameplay 설정 진입점 없음 | 컨베이어-명령 |
| WIM-008 | 필터 일부만 있음 | reserve destination 저장 일부 | 상세 필터·예비창고 UI 없음 | 컨베이어-UI |
| WIM-009 | pressure ID 저장만 있음 | campaign pressure 저장 있음 | 도메인 결과 관찰 없음 | 계절-pressure |
| WIM-010 | meta 보너스 구매만 있음 | meta progression 저장 있음 | 시작 후보 반영 없음 | 시작-meta |
| WIM-011 | thermal/air 전력 조건만 있음 | 환경장 재계산 | 순수 조명 상실 미관찰 | 환경-전력 |
| WIM-012 | 문 존재만 환경망에 반영 | 물리 open state 없음 | 환경 단절 표시 없음 | 문-환경 |
| WIM-013 | breaker 별도, connector 필드 미소비 | 시설 정의 불변 | 스위치·통과량 미반영 | 전력-연결기 |
| WIM-014 | 안전 온도 query만 있음 | 저장품 상태는 있음 | 장기 열화 결과 없음 | 저장-보존 |
| WIM-015 | 연구 실행 있음 | 연구 진행 저장 있음 | UI 99와 문서 45 WU/일 차이 | 연구-표시 |
| WIM-016 | 작물 성장 있음, 조건 의미 다름 | plot 상태 저장 있음 | 실제 multiplier 관찰 제한 | 농업-생육 |
| WIM-017 | 원정 이동 있음, 기후 배율 미사용 | travel 상태 저장 있음 | 날씨 이동 영향 없음 | 기후-원정 |
| WIM-018 | 축산 주기 있음, 기후 입력 없음 | 동물 상태 저장 있음 | 계절 영향 없음 | 기후-축산 |
| WIM-019 | 일반 야생 이동만 있음 | 종 정의 불변 | 종별 pattern 관찰 없음 | 야생-이동 |
| WIM-020 | 착용·물리 질량 계산 둘 다 있음 | 각각 저장 경계 존재 | 서로 다른 중량 의미 | 장비-질량 |
| WIM-021 | 수혈 처치는 있음 | 신체 상태 저장 있음 | bloodLoss 직접 감소 없음 | 수술-효과 |
| WIM-022 | 봉합 처치는 있음 | 신체 상태 저장 있음 | 출혈률 직접 감소 없음 | 수술-효과 |
| WIM-023 | 종족 수술은 있음 | 수술·신체 저장 있음 | 설치/보철 결과 일부 없음 | 수술-종족 |
| WIM-024 | procedure 실행 있음 | 수술 주문 저장 있음 | 상세 도감만 부족 | 문서 전용-수술 |
| WIM-025 | 환경 중단·재개 있음 | active surgery 저장 있음 | 규칙 설명만 부족 | 문서 전용-수술환경 |
| WIM-026 | 구조 실행 있음, 순서 다름 | 구조 계획 상태 있음 | 위키 순서와 다름 | 구조-순서 |
| WIM-027 | 두 치료 경로 모두 있음 | 각자 상태/소유권 존재 | 구분 설명만 부족 | 문서 전용-치료 |
| WIM-028 | antiparasitic 별도, detox 소비 없음 | consumable/질병 상태 저장 | detox 수치 결과 없음 | 의료-해독 |
| WIM-029 | 일반 축산·야생만 있음 | 종 정의 불변 | 종별 산업 역할 없음 | 야생-역할 |
| WIM-030 | 사체 처리 있음, 갑각 output 없음 | 물리 yield transaction 있음 | 갑각 결과 없음 | 야생-산출 |
| WIM-031 | 계정 결제는 있음 | 중앙 gold 저장 있음 | 물리 금고 출납 설명과 다름 | 경제-금고 |
| WIM-032 | 계약 정의/상태만 있음 | 계약 content 존재 | gameplay 진입점 없음 | 계약-진입 |
| WIM-033 | 계약 진행 일부 있음 | 납품 소유권 불완전 | 물리 납품 흐름 없음 | 계약-물류 |
| WIM-034 | 거래·보급 결제 있음 | account transaction 저장 | 설명 정책과 다름 | 계약-결제 |
| WIM-035 | 일반 식사 소비 있음 | 식사 ledger/save 있음 | 호화식 후보 경로 없음 | 음식-소비 |
| WIM-036 | substance 소비 있음 | tolerance/addiction 저장 | typed 효과 의미와 다름 | 물질-효과 |
| WIM-037 | generic 사건 효과만 있음 | campaign state 저장 | 이동·운반·작업 없음 | 사건-실행 |
| WIM-038 | scheduler 있음, 빈도 다름 | cooldown/active 저장 | 발생 상한 표시 다름 | 사건-빈도 |
| WIM-039 | 상태 일부만 있음 | campaign state 저장 | 세부 대상·경보 UI 부족 | 사건-UI |
| WIM-040 | generic trigger/effect 일부 | campaign state 저장 | causal context 부족 | 사건-원인 |
| WIM-041 | 축제 결과만 있음 | festival state 저장 | 현장 참가 작업 부족 | 축제-참가 |
| WIM-042 | 시작 후보 생성 있음 | 시작 snapshot 존재 | 생활·건강 정보 부족 | 시작-UI |
| WIM-043 | work delay만 일부 있음 | pressure/delay 저장 | 농업 직접 영향 부족 | 계절-농업 |
| WIM-044 | delay runtime 있음, 음수 no-op | campaign delay 저장 | 은퇴 효과 결과 없음 | 사건-지연 |
| WIM-045 | 출입 정책은 있음 | door policy 저장, 물리 state 없음 | open/closed 표현 불일치 | 문-물리 |
| WIM-046 | 일부 wait reason 있음 | 진단은 파생값 | 전체 공통 관찰면 없음 | 진단-대기 |
| WIM-047 | 침입 경고는 있음 | invasion state 저장 | 방향·성격·목표 부족 | 침입-경고 |
| WIM-048 | 사장 대피만 있음 | 일반 evacuation state 없음 | 일반 주민 명령 없음 | 침입-대피 |
| WIM-049 | 적 생성 있음, scale 기준 다름 | encounter/invasion 저장 | 목표 power 의미 다름 | 침입-스케일 |
| WIM-050 | run 결과 있음 | 결과 save에 필드 없음 | 목표·선택 결산 없음 | 메타-결산 |
| WIM-051 | crisis ID 선택만 있음 | active ID 저장 있음 | 실제 압력 관찰 없음 | 엔드리스-압력 |
| WIM-052 | cycle 갱신만 있음 | recovery state/date 없음 | 회복 gate 표시 없음 | 엔드리스-회복 |
| WIM-053 | 원정 결과 요약은 있음 | 상세 ledger 저장 없음 | 손실·치료·포획 결산 없음 | 원정-결산 |
| WIM-054 | captive scalar만 있음 | scalar 저장, body와 재동기화 | 치료 우선권 미반영 | 포로-건강 |
| WIM-055 | 심문 문장만 있음 | intelligence outcome 저장 없음 | 실제 정보 결과 없음 | 포로-정보 |
| WIM-056 | 품질 주문은 있음 | 주문 목표/시도 저장 있음 | 예상 자원 경고 없음 | 생산-품질예측 |
| WIM-057 | 합성은 보존 retarget | production ownership 저장 유지 | drain-only 설명과 다름 | 시설-합성 |
| WIM-058 | 아이템 권위 있음 | 불변 authoring | 공개 가격 198개 stale | 도감-아이템 |
| WIM-059 | 건설 권위 있음 | 불변 authoring | 공개 건설 요약 144개 stale | 도감-시설 |
| WIM-060 | 기후 공식 있음 | 기후 state 저장 있음 | 진폭을 연교차로 오표기 | 도감-기후 |
| WIM-061 | 침입 집결+6초·나팔 wear1 있음 | durable slot/내구도 저장 있음 | 화재 경보 consumer 없음 | 보안-신호나팔 |
| WIM-062 | 식사 physical service 등 일부 있음 | 의료는 warehouse stack physical Sink | 의료시설 배송/도착 없음 | 서비스-의료물류 |
| WIM-063 | P1 initial migration/old-save lookup 있음 | deprecated P1을 daily shop에서 판매·kit화 가능 | active offer 모집단 filter 없음 | 상점-호환자산누출 |

## 중복 근본 원인 검사

- exact duplicate ID 0, exact duplicate title 0이다.
- 같은 도메인에 붙어도 원인이 다른 항목은 합치지 않았다. 예: WIM-012는 환경망의 문 상태 입력, WIM-045는 플레이어가 이해하는 물리 개폐 모델이다.
- WIM-009는 generic pressure 소비, WIM-043은 해빙수 범람의 농업 적용, WIM-060은 기후 도감 단위 의미라 서로 다른 계층이다.
- WIM-010은 시작 후보 생성에 meta 강화 미적용, WIM-042는 후보 UI 정보 부족이다.
- WIM-019는 이동 pattern, WIM-029는 산업·환경 역할, WIM-030은 실제 도축 산출이다.
- WIM-024·025·027은 구현 보완 대상에서 제외하되 삭제하지 않았다. 역방향 문서 공백 원장과 연결되는 documentation-only 증거다.
- WIM-058~060은 개별 엔티티마다 경고를 늘리지 않고 각각 하나의 stale projection 근본 원인으로 접었다.
- WIM-061은 아이템 기본 facts 일치와 별개인 `in_game_description` 의미 소비 문제이며, 실제 침입 효과의 문서 누락은 역방향 GAP-169가 소유한다.
- WIM-062는 물리 stack 소비 자체의 부재가 아니라 의료 치료가 창고 stack을 현장 Sink하여 서비스 장소 배송 조건을 충족하지 않는 문제다. 서비스 모드/지원시설 문서 공백은 역방향 GAP-172~173이 소유한다.
- WIM-063은 deprecated P1의 old-save 복원 자체가 아니라 새 일일 상점 모집단에서 이를 거르지 않는 문제다. P1 종족선호 값을 current 위키에 추가하지 않으며 reverse GAP에도 합산하지 않는다.

## 동적 증거 경계

이번 요청은 게임 코드·ScriptableObject·공개 위키를 바꾸지 않는 읽기 전용 전수 감사다. 따라서 Play Mode를 실행해 각 미구현 경로를 재현하지 않았다. 정적 조사로 “호출자 없음”이 확인된 항목은 향후 구현 후 focused Play Mode가 필요하고, 저장 관련 항목은 구현 시 save→restore→resave와 실패 원자성 테스트가 필요하다. 이 미실행 사실은 WIM 누락을 숨기지 않지만, 현재 정적 판정을 런타임 버그 재현 완료로 과장하지 않는다.
