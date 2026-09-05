# UI Feature Surface Gap Audit

## 목적

이 문서는 2026-07-14 기준 `DungeonStory`에서 런타임 기능은 구현되어 있지만 플레이어용 UI로 충분히 표시되거나 조작되지 않는 항목을 목록화하고, 이후 닫힌 항목의 검증 근거를 함께 기록한다.

이전 `full-game-manual-verification.md`의 `Verified in game`은 Unity Editor PlayMode에서 기능 런타임 경로가 동작한다는 의미가 섞여 있다. 이 문서의 기준은 더 엄격하다. 플레이어가 실제 UI에서 기능을 발견하고, 상태를 보고, 버튼/목록/패널로 조작하고, 결과를 확인할 수 있어야 UI 연결로 본다.

## 판정 기준

| 판정 | 의미 |
|---|---|
| `미연결` | 런타임/이벤트/API는 있으나 화면에는 요약 텍스트, 알림, QA probe 결과, 혹은 아무것도 없다. |
| `부분 연결` | 패널이나 알림은 있으나 진입점, 선택 버튼, 상세 목록, 결과/히스토리 확인 중 핵심이 빠져 있다. |
| `연결됨` | 플레이어가 일반 UI 흐름으로 기능을 실행하고 결과를 볼 수 있다. 이 문서의 gap 표에는 넣지 않는다. |

UI 연결로 세지 않는 것:

- `FullGameManualQaRuntimeProbe` 같은 Editor/QA helper가 직접 런타임 메서드를 호출한 경우
- VContainer로 임시 캐릭터/시설을 주입해서 통과한 경우
- 하단 탭에 숫자/요약문만 보이는 경우
- NoticeFeed 또는 EventAlert에 결과 문장만 뜨는 경우
- 숨은 조작, 우클릭, 디버그 메뉴처럼 UI가 명령 가능성을 설명하지 않는 경우
- `TMP_Text` 하나에 "다음 UI 연결 필요" 목록을 출력하는 경우

## 핵심 결론

최초 감사 시점의 하단 탭 구조는 대부분 `UITabManager`가 생성 패널을 만들고 `UITabContentTextProvider`가 요약 문자열을 넣는 방식이었다. 2026-07-13 P0 연결 작업으로 상점(id 3), 창고(id 4), 운영(id 5), 연구(id 8)는 `P0FeatureSurfacePanel` 전용 경로로 교체되었다. 이어서 시설/방, 운영, 방어, 원정, 직원, 상점 상세, 도감/기록의 P1/P2 임시 요약 UI를 실제 상태 카드와 액션이 있는 `P1P2FeatureSurfacePanel` 및 직원 관리 모드로 교체했다.

`UITabManager.EnsureSpecializedTabContent()`는 직원 탭의 `StaffWorkPriorityPanel`, P0 탭의 `P0FeatureSurfacePanel`, P1/P2 탭의 `P1P2FeatureSurfacePanel`을 라우팅한다. 운영 탭 안에는 정산, 단골 영입, 메타 강화, 런 변수, 경제 상세가 한 스크롤 흐름으로 통합되어 있다.

2026-07-14 재감사에서는 기능 연결 여부뿐 아니라 실제 정보 완전성, 공통 시각 체계, 화면 경계, 알림 밀도, 선택 상태까지 다시 확인했다. 캐릭터 요약의 누락 욕구/체력/프로필, 건물 상세 상태, 직원 9개 능력치, 과대 알림 피드를 보강했으며 HUD/탭/생성 패널/팝업에 공통 불투명 테마를 적용했다.

## P0 연결 완료 및 검증

| 기능 | 연결된 플레이어 UI | PlayMode 실제 클릭 및 상태 변화 | 판정 |
|---|---|---|---|
| 하단 탭 전용 구조 | 상점/창고/운영/연구 탭이 전용 스크롤 패널, 상태 카드, 명시 버튼, 결과 피드백을 표시 | 네 탭 모두 `visible=True`; 활성 UI `RectTransform` 132개 중 invalid/oversized 0 | 연결됨 |
| 일일 시설 상점/기본 구매 | 상품명, 가격, 희귀도, 연구 잠금, 구매/품절 상태 및 결과 피드백 | 일일 상품 `4/4`, 기본 구매 `1/1` 버튼 클릭, 자금 `50000→49040`, 연구 작업 `0→1` | 연결됨 |
| 설계도 연구 | 진행 작업/진행률, 설계도 상태 목록, 시작/취소/+10초 버튼 및 결과 피드백 | 취소/다시 시작/진행 버튼 모두 클릭, 완료 설계도 `0→1` | 연결됨 |
| 창고/재고/보충 | 창고별 카테고리 재고, 상점별 부족량, 수동 보충, 일일 납품 구매 및 결과 피드백 | 보충/납품 버튼 모두 클릭, 창고 `50→55`, 상점 `30→35`, 자금 `49040→48950` | 연결됨 |
| 운영일 정산 | 오늘 정산 버튼, 최근 보고서의 일자/매출/방문/재고/사건 요약 | 정산 버튼 클릭, Day `1→2`, `OperatingDayReport` 생성 | 연결됨 |
| 단골/영입 | 후보명, 상태, 방문 횟수, 만족도, 역할과 영입 버튼/결과 | 후보 영입 버튼 클릭, 영입 인원 `0→1` | 연결됨 |
| 메타 강화 구매 | 보유 재화, 강화명, 레벨, 비용, 효과, 구매 버튼/결과 | 강화 구매 버튼 클릭, 재화 `600→480`, 총 레벨 `0→1` | 연결됨 |

검증은 `Assets/Scenes/SampleScene.unity`를 PlayMode로 열고 실제 `Button.onClick`을 실행한 뒤 런타임 상태를 전후 비교했다. QA 준비는 재현 가능한 자금/메타 재화, 영입 후보, 창고 여유 공간, 보충 대상만 만들며 위 표의 상태 변화는 모두 실제 UI 버튼 경로에서 발생했다.

- 자동 검증 보고서: `Temp/p0-ui-surface-verification-report.txt`
- 화면 캡처: `Temp/p0-ui-shop.png`, `Temp/p0-ui-shop-basic.png`, `Temp/p0-ui-warehouse.png`, `Temp/p0-ui-warehouse-actions.png`, `Temp/p0-ui-operation.png`, `Temp/p0-ui-recruitment.png`, `Temp/p0-ui-meta.png`, `Temp/p0-ui-research.png`
- Unity MCP `Main Camera` 직접 캡처 성공; Overlay Canvas의 플레이어 UI는 `ScreenCapture` 증거로 확인
- 최종 검증 구간 Console Error 0, Warning 0
- 원래 `CharacterAiTestScene` 복원 후 기존 `SelectedOwnerText`의 `LiberationSans SDF` 한글 glyph fallback Warning 4건이 재출력됨. P0 검증 씬/패널에서는 발생하지 않으며 별도 폰트 자산 정리 항목이다.
- P1/P2 연결 완료 후 P0 회귀 재실행: 상점/창고/운영/연구 모두 `stateChanged=True`, 활성 Rect 130개 중 invalid/oversized 0, 검증 구간 Error 0 / Warning 0
- 2026-07-14 통합 UI 최종 회귀: 활성 Rect 140개 중 invalid/oversized 0, 활성 Text 64개, Button 34개, 검증 구간 Error 0 / Warning 0

## P1/P2 연결 완료 및 검증

| 우선순위 | 기능 | 연결된 플레이어 UI | PlayMode 실제 클릭 및 상태 변화 | 판정 |
|---|---|---|---|---|
| P1 보강 | 방 경계/가구 성향 | 시설 탭 첫 영역에 폐쇄/출입문, 면적, 문/벽 수, 내부 시설 수, 가구 역할 합산 방 성향, 진화 압력을 분리 표시 | 실제 Grid에 Hallway 22칸, `Door` 1개, `Wall` 1개와 식사/훈련 시설 2개를 배치. `문 1 / 벽 1`, `방 성향 식사 + 훈련` 카드 클릭 후 `선택됨` 확인 | 연결됨 |
| P1 | 런 시작 변수/침공 변수 | 운영 탭 변수 카드, 시작/운영/침공 선택 버튼과 현재 효과 표시 | 운영 변수 `0→1`, 침공 변수 선택 후 런 시작 상태 확인 | 연결됨 |
| P1 | 시설 합성 | 방 성향 영역과 분리된 재료 카드, 레시피/가능 여부, 실행 버튼과 결과 피드백 | 재료 선택 `1→2`, 합성 후 `2→0`, 결과 시설 생성 확인 | 연결됨 |
| P1 | 시설 진화 | 시설별 후보/승인·거절 사유, 진화 버튼과 결과 상태 | 승인 후보 버튼 클릭, 시설 signature 변경과 runtime 결과 확인 | 연결됨 |
| P1 | 침공 위협도 | 방어 탭 위협/단계/경고 카드와 위협 증가 액션 | 위협도 `1003.1→1013.1`, stage `Candidate` 확인 | 연결됨 |
| P1 | 침입자 추적/상태 | 활성 침입자 목록, 추적/스폰 액션과 현재 상태 | 활성 침입자 `0→1`, 추적 버튼 실행 확인 | 연결됨 |
| P1 | 방어 시설 효과/쿨다운 | 시설별 효과, 발동 조건, 쿨다운, 상태, 발동 버튼 | 발동 클릭 후 cooldown `0→1` 확인 | 연결됨 |
| P1 | 침공 전투 리포트 | 현재/과거 전투 결과와 상세 선택 | 리포트 history `0→1`, 결과 상세 선택, 현재 전투 resolved 확인 | 연결됨 |
| P1 | 직원 불만/반란 | 직원 관리 모드의 불만 단계/원인과 격리 액션 | `LocalRebellion` 표시, 격리 클릭 후 `isolated=True` | 연결됨 |
| P1 | 사장 우선 명령/반란 제압 | 선택 지휘자, 가능한 시설/반란 대상 카드와 명령 버튼 | `마력 저장소` 우선 명령 후 반란 대상 `P1 UI Rebel` 제압 명령 확인 | 연결됨 |
| P1 | 원정 탭 연결 | 원정 탭에서 월드맵/편성 패널 진입 | 월드맵 및 원정 패널 버튼 클릭 후 두 패널 visible 확인 | 연결됨 |
| P1 | 원정 보상 | 대상/출발/완료 결과, 보상 상세와 히스토리 | active `0→1`, history `0→1`, 자금 `0→80`, 보상 상세 선택 | 연결됨 |
| P1 | 직원 상태/근무/휴식 | 직원 로스터와 근무 상태 전환 버튼 | 근무 버튼 클릭 후 off-duty `False→True` | 연결됨 |
| P1 | 캐릭터 프로필/종족/특성 | 직원 상세 카드의 이름, 종족, 역할, 특성/능력 | `P1 UI Guard`, 종족 `QA` 프로필 카드 선택 확인 | 연결됨 |
| P1 | AI/LLM/페르소나/무드 | 페르소나, 요청 큐/오류, 무드 impulse 상태와 실행 액션 | 페르소나/무드 액션 클릭, mood request 상태 변경 확인 | 연결됨 |
| P2 | 도감/기록 | 카테고리/항목 목록, 선택 상세, 기록 archive | 카테고리와 항목 클릭, 43개 entry 중 선택 상태 확인 | 연결됨 |
| P2 | 상점 상품/가격/계산대 상태 | 상점별 상품, 재고, 가격, 대기/직원/self-service/절도 위험 상세 | 4개 상점 탐색, 상품 카드 클릭과 선택 상세 확인 | 연결됨 |
| P2 | 경제/HUD 세부 효과 | 현재 장부, 최근 정산, 히스토리 상세 전환 | 정산 실행, current/history 클릭, 보고서 1개와 매출 123 확인 | 연결됨 |
| P2 | 알림/이벤트 히스토리 | 보고서/이벤트 전환, 카테고리 필터, 기록 상세 | 보고서/이벤트/필터/기록 상세 버튼을 순서대로 클릭, 20개 로그 중 선택 확인 | 연결됨 |

검증은 `Assets/Scenes/SampleScene.unity` PlayMode에서 실제 `Button.onClick`을 호출하고 각 런타임의 전후 상태를 비교했다. UI 캡처는 DX12 비동기 프레임 파손을 피하기 위해 `WaitForEndOfFrame` 뒤 `CaptureScreenshotAsTexture()`로 동기 저장한다.

- 자동 검증 보고서: `Temp/p1-p2-ui-surface-verification-report.txt`
- 방 경계/성향 전용 캡처: `Temp/p1-ui-room-identity.png`
- 기능군 캡처: `Temp/p1-ui-facilities.png`, `Temp/p1-p2-ui-operation.png`, `Temp/p1-ui-defense.png`, `Temp/p1-ui-offense.png`, `Temp/p1-ui-staff.png`, `Temp/p2-ui-shop.png`, `Temp/p2-ui-codex.png`, `Temp/p2-ui-events.png`
- 최종 결과: P1/P2 감사 row `18/18` PASS, 방 formal boundary 카드 클릭/선택 PASS
- 최종 UI bounds: 활성 Rect 211개, invalid 0, oversized 0, 활성 Button 46개
- 최종 검증 구간 Console Error 0, Warning 0
- Unity MCP `Main Camera` 직접 캡처로 월드 방 배치를 확인했다. Screen Space Overlay 플레이어 UI의 가시성과 클릭 후 상태는 위 동기 ScreenCapture로 확인했다.
- 2026-07-14 통합 UI 최종 회귀: 감사 row `18/18` PASS, 활성 Rect 191개 중 invalid/oversized 0, 활성 Button 46개, 검증 구간 Error 0 / Warning 0

## 통합 UI 및 정보 완전성 재감사

| 기능/표면 | 보강 내용 | PlayMode 검증 | 판정 |
|---|---|---|---|
| 공통 HUD/탭/관리 패널 | 불투명 패널, 공통 배경/표면/강조/경고 색, 1920x1080 기준 HUD와 하단 10탭, 전체 폭 520px 관리 표면 | 운영 탭 실제 포인터 클릭 후 탭 id 5 열림, 선택색 `#3D8B70FF`, P0/P1/P2 bounds 0건 | 연결됨 |
| 캐릭터 상태/욕구 | 이름, 종족, 역할, 생애 상태, 현재/최대 체력, 부상, 기분/포만감/재미/휴식/배변/위생, 최근 기록 | 6개 욕구와 체력 meter 바인딩, placeholder 0개, 기분 모델 `74→67`과 slider `0.74→0.67` 일치 | 연결됨 |
| 캐릭터 팝업 UX | 고정 `460x700` 패널, 화면 내 배치, 명시 닫기 버튼, 값별 정상/경고/위험 색 | 실제 `Button.OnPointerClick`으로 닫힘, 화면 경계 PASS | 연결됨 |
| 건물 상세 | 손상/정상, 시설 레벨, 위치/분류, 이용/수용/예약, 시설/업무 역할, 필요 직원, 상점/창고 재고 | 실시간 건물 id 14에서 `정상`, `Lv.1`, `(10,2)`, `0/3`, 예약 0, 휴식, 운영/수리/청소 확인 | 연결됨 |
| 직원 프로필/능력치 | 직원 관리 모드 선택 상태와 공격/판매/연구/이동/근력/맷집/민첩/청소/지구력 9개 수치 | 직원/직원 관리 실제 포인터 클릭, 9개 label PASS, 프로필 카드 `980x156`, 관리 모드 강조 PASS | 연결됨 |
| 실시간 알림 피드 | 42px 무제한 텍스트를 상단 중앙 최대 6개, 18px, 56px 높이, 불투명 배경, 전체 페이드, 클릭 비차단 toast로 교체 | 8개 발생 후 활성 6개, 모두 360px 영역 안, 배경 alpha 1, text/background raycast false | 연결됨 |
| 외벽 입구 통과 렌더 | collider가 먼저 빠져도 sprite가 외벽 frame과 겹치면 뒤 레이어 유지 | `visualAfterColliderExit=True`, frame layer 유지, 최종 `Default` 복귀, 별도 InteriorDoor는 `Default→Default` 유지 | 연결됨 |

- 통합 UI 자동 검증 보고서: `Temp/phase43-unified-ui-verification-report.txt`
- 캐릭터/알림 캡처: `Temp/phase43-character-notice-verification.png`
- 직원 프로필 캡처: `Temp/phase43-staff-profile-verification.png`
- 건물 상세 캡처: `Temp/phase43-building-summary-themed.png`
- 외벽 통과 캡처/보고: `Temp/phase42-dungeon-door-frame-occlusion-camera.png`, `DoorVisualPlayModeVerifier`
- 통합 UI 검증 결과: 모든 assertion PASS, captured Error 0 / Warning 0

## 남은 UI 미표시/미연결 목록

이 감사에서 분류한 P0/P1/P2 및 통합 UI 정보 row 중 남은 미연결 항목은 없다. 이후 추가되는 신규 기능은 위 닫힘 조건으로 다시 감사한다. 현재 흰색 시설 placeholder sprite는 기능 UI 미연결이 아니라 월드 아트 교체 범위로 분리한다.

## 이미 UI로 어느 정도 연결된 것

아래 항목은 "기능이 UI에 전혀 없다"로 보지는 않는다. 다만 스타일/정보량/진입점 개선은 별도 작업이다.

| 기능 | 현재 연결 상태 |
|---|---|
| 건설 카테고리/건물 선택/고스트/배치 | `GridConstructTab`, 생성 버튼, `UIBuildingSelectButton`, public pointer placement가 연결됨 |
| 건물 정보 패널 | `UIBuildingInfo`와 compact 건물 팝업이 이름, 상태, 레벨, 위치, 이용/예약/수용, 역할, 필요 직원, 재고를 같은 formatter로 표시 |
| 직원 작업 우선순위 | 직원 탭에서 우선순위/직원 관리 모드를 전환하며 우선순위 셀, 근무/불만/명령/프로필/AI 및 9개 능력치를 확인 가능 |
| 이벤트 알림/선택지 | `EventAlertRuntime` detail panel과 choice button은 실제 UI로 동작 |
| 사장 선택/런 결과 표시 | `OwnerSelectionPanel`, `RunResultPanel`은 존재하고 PlayMode에서 표시 검증됨 |
| 캐릭터 피드백/대사 말풍선 | `CharacterFeedbackBubble`, `CharacterDialogueRuntime`은 월드 오버레이로 표시됨 |
| 오펜스 월드맵/원정 편성 패널 | 하단 원정 탭의 명시 버튼에서 두 패널로 진입하고 보상/히스토리까지 확인 가능 |

## 후속 유지 원칙

1. 신규 기능은 탭 진입점, 읽을 수 있는 상태, 명시 액션, 결과 피드백을 함께 구현한다.
2. 직접 런타임 probe만 통과한 기능은 UI 연결 완료로 세지 않는다.
3. 방 성향은 벽/문으로 구획된 방 내부의 가구/시설 역할 합산으로 표시하며 시설 합성과 혼동하지 않는다.
4. 기능 추가 뒤 P0 및 P1/P2 PlayMode 검증기를 함께 회귀 실행한다.

## 닫힘 조건

각 row는 다음 조건을 모두 만족해야 닫는다.

- 일반 플레이어 UI에서 진입 가능
- 핵심 상태가 화면에 표시됨
- 핵심 액션이 버튼/목록/토글/슬라이더 등 명시 UI로 실행됨
- 성공/실패/잠금 사유가 화면에 표시됨
- Unity PlayMode에서 UI 클릭 흐름으로 검증
- Unity MCP camera capture 또는 ScreenCapture로 가시성 확인
- Console error/warning 0 또는 명확히 별도 기록
