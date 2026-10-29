# R30 수정과 검증 결과

보고서30항목과 추가 MP01/MP02를 수정했다. 조건부 CP01~06은 아래 계약 근거로 유지했다. 실제 실행의 원래 source와 전체 실패 이력을 보존하며 현행 source 통합 검사와 구별한다.

| 항목 | 변경 | 표적 증거 |
|---|---|---|
| R30-01 | 포획 시작 거절 전파와 예약/행동 소유권 롤백 | [r221r30-impact-so-inspector-r30-first-01](r221r30-impact-so-inspector-r30-first-01/unity.redacted.log); [r246r30-impact-so-inspector-r30-transport-01](r246r30-impact-so-inspector-r30-transport-01/capture.txt) |
| R30-02 | 실제 수용/안전 발판 적격 후 거리 선택을 UI와 귀환에서 공유 | [r246r30-impact-so-inspector-r30-transport-01](r246r30-impact-so-inspector-r30-transport-01/capture.txt) |
| R30-03 | 공연 대상 적격 판정 공유와 획득한 행동만 취소 | [r226r30-impact-so-inspector-r30-second-01](r226r30-impact-so-inspector-r30-second-01/unity.redacted.log) |
| R30-04 | 탄약23개 authored profile과 동일 catalog 소비, 두 builder 생성/복구/no-op | [r246r30-impact-so-inspector-r30-fourth-final-01](r246r30-impact-so-inspector-r30-fourth-final-01/unity.redacted.log); [r233r30-impact-so-inspector-r30-third-remaining-01](r233r30-impact-so-inspector-r30-third-remaining-01/unity.redacted.log) |
| R30-05 | 세 제작 UI의 경험 조건과 실제 판정 일치 | [r221r30-impact-so-inspector-r30-first-01](r221r30-impact-so-inspector-r30-first-01/unity.redacted.log) |
| R30-06 | 원정 scalar snapshot과 소유자 명령 분리 | [r226r30-impact-so-inspector-r30-second-01](r226r30-impact-so-inspector-r30-second-01/unity.redacted.log) |
| R30-07 | 인구 사본 반환과 방문 종료/재바인드 소유 명령 | [r226r30-impact-so-inspector-r30-second-01](r226r30-impact-so-inspector-r30-second-01/unity.redacted.log) |
| R30-08 | 유체망 snapshot 중첩 사본 반환 | [r246r30-impact-so-inspector-r30-play-fluid-01](r246r30-impact-so-inspector-r30-play-fluid-01/r30-fluid-play.txt) |
| R30-09 | 날씨 Kind 메타데이터 소비 | [r221r30-impact-so-inspector-r30-first-01](r221r30-impact-so-inspector-r30-first-01/unity.redacted.log) |
| R30-10 | 화면별 정지 lease와 같은 프레임 입력 소비 | [r242r30-impact-so-inspector-r30-play-pause-01](r242r30-impact-so-inspector-r30-play-pause-01/r30-pause-play.txt) |
| R30-11 | 선택 거절 시 명령 중단과 재주입 모드 상태 단일 소유 | [r248r30-impact-so-inspector-r30-play-staff-01](r248r30-impact-so-inspector-r30-play-staff-01/r30-staff-play.txt) |
| R30-12 | 활성/비전투 원정 수동 실행과 전투 원정의 무변경 거절 | [r233r30-impact-so-inspector-r30-third-remaining-01](r233r30-impact-so-inspector-r30-third-remaining-01/unity.redacted.log) |
| R30-13 | 특수 장기3종 정의/소비와 상태/쌍 슬롯 강도 및 기존 저장 ID 유지 | [r246r30-impact-so-inspector-r30-fourth-final-01](r246r30-impact-so-inspector-r30-fourth-final-01/unity.redacted.log) |
| R30-14 | 농사 작업 지연 capability 소비 | [r228r30-impact-so-inspector-r30-second-remaining-01](r228r30-impact-so-inspector-r30-second-remaining-01/unity.redacted.log) |
| R30-15 | 정의 소유 혜택 사본, 공급7->19와 방어 재정비1.2 실제 소비 | [r246r30-impact-so-inspector-r30-fourth-final-01](r246r30-impact-so-inspector-r30-fourth-final-01/unity.redacted.log); [r233r30-impact-so-inspector-r30-third-remaining-01](r233r30-impact-so-inspector-r30-third-remaining-01/unity.redacted.log) |
| R30-16 | 사거리/효과/역할 메타데이터 기반 전술 대응 | [r233r30-impact-so-inspector-r30-third-remaining-01](r233r30-impact-so-inspector-r30-third-remaining-01/unity.redacted.log) |
| R30-17 | 종족 이름 대신 충전 capability로 마모/방전/충전 처리 | [r249r30-impact-so-inspector-r30-g12-01](r249r30-impact-so-inspector-r30-g12-01/unity.redacted.log) |
| R30-18 | 여섯 참조 컨테이너의 명시적 context | [r226r30-impact-so-inspector-r30-second-01](r226r30-impact-so-inspector-r30-second-01/unity.redacted.log) |
| R30-19 | ID별 용량 하한 제거와 작성 capacity/max stack 소비 | [r226r30-impact-so-inspector-r30-second-01](r226r30-impact-so-inspector-r30-second-01/unity.redacted.log) |
| R30-20 | 질량 설명의 검증/실행 registry 단일화 | [r226r30-impact-so-inspector-r30-second-01](r226r30-impact-so-inspector-r30-second-01/unity.redacted.log) |
| R30-21 | 단골 사본과 ACK 유실/복원/재시도 중복 방지 | [r226r30-impact-so-inspector-r30-second-01](r226r30-impact-so-inspector-r30-second-01/unity.redacted.log) |
| R30-22 | 연구/상점 published 사본과 소유자 명령 | [r236r30-impact-so-inspector-r30-third-pending-01](r236r30-impact-so-inspector-r30-third-pending-01/unity.redacted.log) |
| R30-23 | 메타/추적 사본과 소유자 명령 | [r226r30-impact-so-inspector-r30-second-01](r226r30-impact-so-inspector-r30-second-01/unity.redacted.log) |
| R30-24 | 장비 구성 중첩 사본과 명령 | [r221r30-impact-so-inspector-r30-first-01](r221r30-impact-so-inspector-r30-first-01/unity.redacted.log) |
| R30-25 | 공유 캐시10경계 및 명령/외부 구역의 소유 상태 보호 | [r226r30-impact-so-inspector-r30-second-01](r226r30-impact-so-inspector-r30-second-01/unity.redacted.log); [r231r30-impact-so-inspector-r30-third-01](r231r30-impact-so-inspector-r30-third-01/unity.redacted.log); [r233r30-impact-so-inspector-r30-third-remaining-01](r233r30-impact-so-inspector-r30-third-remaining-01/unity.redacted.log); [r246r30-impact-so-inspector-r30-play-commands-01](r246r30-impact-so-inspector-r30-play-commands-01/r30-commands-play.txt); [r246r30-impact-so-inspector-r30-play-exterior-01](r246r30-impact-so-inspector-r30-play-exterior-01/r30-exterior-play.txt) |
| R30-26 | 문 권한 붙여넣기 거절 이유 표시 | [r248r30-impact-so-inspector-r30-play-door-01](r248r30-impact-so-inspector-r30-play-door-01/r30-door-play.txt) |
| R30-27 | 전투 중첩 사본과 명시적 완료 권한 및 무기 payload 복원 | [r234r30-impact-so-inspector-r30-third-repairs-01](r234r30-impact-so-inspector-r30-third-repairs-01/unity.redacted.log) |
| R30-28 | 방어 정책4반환의 사본과 생성/복제 격리 | [r221r30-impact-so-inspector-r30-first-01](r221r30-impact-so-inspector-r30-first-01/unity.redacted.log) |
| R30-29 | 진화 조회 순수화와 명시적 후보/완료 명령 및 기존 저장 조정 | [r234r30-impact-so-inspector-r30-third-repairs-01](r234r30-impact-so-inspector-r30-third-repairs-01/unity.redacted.log) |
| R30-30 | 원정 안내 문자열 손상 교정 | 문자열 diff와 현재 컴파일. 네 표시 분기의 개별 클릭은 미실행. |

## 추가 실패 경계

- MP01: R30_STEP=PASS; name=carry-restore-atomic; / 원래 전체 결과는 1.
- MP02: R30_STEP=PASS; name=seed-corruption; / 원래 전체 결과는 1.

## 조건부 판정

- CP01: 유전 공식의 고정 의미 키 계약 유지. 현행 누락 공식 또는 실패한 생산자 반례 없음.
- CP02: 단일 야전 키트 정책 유지. 현행 종족의 소비 불일치가 없으며 새 종족 일반 키트 경로는 기존 계약.
- CP03: 적 표시/수명 호스트와 전투 정의를 구별. 실제 적 수치/장비/능력은 별도 정의를 소비하므로 공통 원형을 일괄 교체하지 않음.
- CP04: 조립 검증기의 도메인 지식은 현행 책임. 실제 필드 경로 결합만 R30-18에서 수정.
- CP05: 고정 시작/귀환/사건 시나리오 안정 ID 유지. 일반 전술/충전 소비의 ID 분기는 R30-16/17에서 수정.
- CP06: 작성 SO는 불변 정의 계약. 14개 노출 후보의 실제 런타임 작성/소비 불일치가 입증되지 않아 일괄 복제나 구조 분리하지 않음. 런타임 상태/캐시 노출과 구별.

## 확인 범위

- 과거 부분 PASS와 전체 FAIL은 원래 source와 결과로 보존했다.
- 현재 source의 다섯 통합 그룹과 개별 표적 실행을 구별했다.
- UI의 실제 포인터/상태/배치 경계는 확인했고 픽셀 및 사람의 시각 평가는 미실행이다.
- 수술과 운반에는 통제 fixture가 포함된다. 장기 밸런스와 서사 모델 추론은 인증하지 않았다.
- R30-30은 문자열 diff와 현재 컴파일을 확인했으며 네 표시 분기의 개별 클릭은 미실행이다.
- 보고된 결함을 닫는 결과이며 모든 미래 콘텐츠 조합이나 모든 실행 경로의 무결함 보장은 아니다.
