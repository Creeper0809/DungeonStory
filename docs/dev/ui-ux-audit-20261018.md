# DungeonStory UI/UX 전수 평가

작성일: 2026-10-18. 게임 기준 커밋: `d9b3fba53940d43c8350427b01cb245c080ac608`.

## 판정

**게임 시스템의 조작 경로는 많이 연결되어 있지만, 현재 UI/UX를 완성도 높은 운영 게임 인터페이스로 보기는 어렵다.** 직원·물류·생존·시설·침공·원정·연구라는 복잡한 시스템을 다룰 정보는 있는데, 그 정보를 플레이어의 판단 순서로 정리하는 부분이 부족하다. 지금은 기능 확인용 관리 화면의 성격이 강하다.

이 게임에서 UI가 해야 할 일은 `위험/부족 발견 → 원인과 담당자 확인 → 비용·결과 비교 → 명령 → 진행·완료·실패 확인`이다. 긴 텍스트와 버튼을 모두 보여 주는 것만으로는 이 흐름이 성립하지 않는다. 이번 권고는 게임 시스템을 줄이거나 모든 창을 다시 만드는 제안이 아니다. 현재 기능을 유지하면서 위험한 전환, 발견하기 어려운 명령, 지나치게 긴 목록부터 고치는 제안이다.

### 유지해야 할 기반

- 실제 상태와 명령을 연결한 11개 관리 영역, 개체/시설 상세, 원정 전술 화면은 유지한다.
- 연구의 선행 조건·차단 사유·해금 정보, 문 권한의 preset/복사, 의복의 고급 설정 접기는 좋은 기반이다.
- 물리 재고·생산 경로·계약·영수증 같은 게임 고유의 인과관계를 삭제하지 않는다. 요약과 상세의 층위를 나눈다.
- 결과 원장에는 이미 페이지 이동이 있다. 이 기능을 없거나 고장 났다고 판정하지 않는다.
- 공용 카드 버튼은 콜백 뒤 자동 `Refresh()`한다. presenter 안에 `RequestRefresh()`가 없다는 이유만으로 갱신 오류라고 판정하지 않았다.

## 조사 방법과 증거 범위

타이틀·시작 준비·설정·저장·HUD·11개 관리 탭·캐릭터·시설·원정·이벤트·결과 화면의 생성 경로, 호출처와 상태/명령 연결을 조사했다. 좁은 지식베이스 조회 후 현재 C# 원본으로 재확인했다. 아래 표의 `소스`는 구현 확인, `화면`은 현재 게임을 실행해 얻은 이미지 확인이다. 취향에 따른 권고와 실제 결함을 구분한다.

실제 화면은 사용자 데스크톱과 분리된 window station/desktop 안에서 Unity의 Game View 렌더러로 촬영한다. 원본 씬·프리팹·SO·생산 UI는 수정하지 않는다. 버튼 콜백과 공개 presentation 진입점으로 화면을 연다. **마우스로 실제 hit target을 눌렀다는 증거, 키보드/패드 조작 인증, 장시간 자연 플레이·LLM 생성 완료 증거는 아니다.** 시작 준비는 실제 준비 중 화면을 관찰하고, Gameplay는 기존 통제 파티 확정 명령으로 진입했다. 사건/결과 등의 통제 입력은 각각 표시한다.

PC 가로 화면을 대상으로 한다. 세로/모바일 지원을 요구사항으로 추정하지 않는다. 이미지 방향·크기가 잘못된 초기 촬영은 증거에서 제외하고 로그만 보존한다. 촬영 완료 수·해상도 일치·오류·미관찰 상태는 마지막 검증 절에 기록한다.

## 가장 먼저 고칠 것

우선순위는 P0(진행/데이터 안전), P1(핵심 판단·조작), P2(효율·표현)다. 소스만으로 확인한 위험을 실제 데이터 손실 재현이라고 과장하지 않는다.

| ID | 우선순위·근거 | 현재 문제 | 보완/추가 | 없애거나 합칠 것 | 완료 조건 |
|---|---|---|---|---|---|
| U01 | P0 위험·소스 | 새 게임 준비 후 자동·빠른·수동 저장을 모두 삭제한다. 난이도 경고는 있으나 별도 작성된 확인창은 생성·호출되지 않는다. 새 런 적용보다 슬롯 삭제가 먼저다. | 삭제 대상·보존/취소 선택을 명시하고 새 런 실패 시 기존 저장이 남는 전환 계약을 둔다. | 연결되지 않은 중복 확인창 구현은 실제 경로로 통합하거나 제거. | 기존 3슬롯이 있는 상태에서 취소/준비 실패/전환 실패 시 보존, 명시적 확정 시에만 의도한 교체. |
| U02 | P1·소스 | 불러오기는 현재 진행 교체 확인 없이 즉시 실행된다. 다른 위험 행동은 4초 내 재클릭 확인이라 방식도 다르다. | 슬롯 시각·진행·현재 미저장 영향과 확인/취소를 같은 위치에 표시. | 짧은 공용 상태 문구에만 의존하는 위험 확인. | 잘못 누른 불러오기/삭제/덮어쓰기를 취소할 수 있고 무엇이 교체되는지 알 수 있음. |
| U03 | P1·소스/화면 | 이벤트 선택지는 최대 4개지만 480px 상세 패널의 네 번째 버튼 끝이 560px에 놓인다. 실제 네 번째 버튼이 화면 밖으로 잘리고 긴 본문이 버튼·HUD까지 넘친다. | 세로 또는 2열 선택지, 스크롤 본문, 비용/결과 설명. | 고정 한 줄 배치와 본문 무제한 overflow. | 4선택지·긴 한국어·큰 글자에서 모든 선택지와 닫기에 접근. |
| U04 | P1·소스 | 콜백 선택지가 하나인 경보는 카드 클릭 시 상세를 열면서 바로 행동도 실행한다. | 카드 클릭은 검토, 선택 버튼은 실행으로 분리. | ‘열기’와 ‘결정’의 중복 역할. | 경보를 읽는 행동으로 결과 있는 명령이 실행되지 않음. |
| U05 | P1·소스 | 직원 `프로필 확인`은 실제 프로필 대신 알림만 띄운다. | 현재 선택 직원을 캐릭터 상세와 연결. | 기능과 다른 버튼 약속. | 버튼 한 번으로 같은 직원의 실제 프로필을 열고 선택을 유지. |
| U06 | P1·소스/화면 | 운영·건물·창고·방어에서 다른 시간 규모의 결정이 긴 동일 스타일 카드로 섞인다. | 위기/차단 상단 요약, 도메인별 하위 탐색, 선택 대상/검색/필터. | 정상 상태의 반복 설명, 한 화면에 펼친 모든 고급 정책. | 부족 자원/작업자 없음/침공 대응에 필요한 명령을 첫 화면 또는 명확한 하위 메뉴에서 찾음. |
| U07 | P1·소스 | 여러 목록을 `.Take(6/8)`로 자르며 전체 접근과 전체/표시 수가 없다. 보고서 아카이브는 종류별 최신 한 건만 만든다. | 전체 수·페이지·기간/종류 검색. 결과 원장의 기존 paging은 재사용 가능. | ‘아카이브’라고 부르면서 이전 항목을 숨기는 표현. | 9개 이상 항목의 마지막 항목에 접근하고 전체 수가 정확함. |
| U08 | P1·소스 | 시작 인원 교체가 드래그에 의존하고 안내는 드래그 시작 뒤 나온다. 명시적 교체 버튼 렌더러는 호출되지 않는다. | 항상 보이는 교체 명령/안내, 키보드 가능한 대안, 변경 전후 비교. | 사용되지 않는 별도 교체 UI 구현. | 처음 온 사용자가 도움 없이 선발/예비 교체 방법을 발견. |
| U09 | P1·소스/화면 | 중요한 실패·성장 확정이 잠깐 나타나는 토스트에 의존한다. 표시 2초+fade 1초, 고정 높이·잘림 설정이다. | 해당 명령 옆의 지속 결과/실패 사유, 관련 상세로 이동, 위험 결정은 별도 확인. | 토스트를 승인·복구의 유일한 통로로 쓰는 방식. | 긴 실패 이유를 다시 읽고 같은 대상에서 재시도 가능. |
| U10 | P1·소스 | 잠긴 건축은 카탈로그에서 제외되며, 잠금 설명이 있는 버튼도 비활성화해 표준 클릭으로 설명을 못 연다. | 잠금 표시·선행 연구 링크·비용/크기/효과의 선택 상세. 배치만 차단. | 이유 없는 비활성/목록에서 완전 소실. | 다음에 지을 시설과 해금 방법을 건축 화면에서 발견. |
| U11 | P1·소스 | 연구 큐가 고정 영역에 54px 행을 60px 간격으로 추가하며 큐 전용 스크롤이 없다. | 큐 viewport/스크롤, 현재 작업 고정, 순서 이동·취소 결과 예고. | 영역 밖에 계속 쌓이는 행. | 긴 큐의 마지막 항목을 읽고 재정렬/취소 가능. |
| U12 | P1·소스/화면 | 수술·일부 결과/기록에 영어, enum, 내부 ID·실패 코드가 노출된다. | 플레이어 용어, 필요한 자원/의사/시설/위험과 다음 행동. 진단 정보는 개발 모드로. | 일반 화면의 내부 식별자·디버그 상태 문자열. | 내부 구현을 몰라도 실패 이유와 해결 행동을 이해. |
| U13 | P1·소스 | 물품 상세의 `버리기`는 즉시 삭제이며 다른 행동과 같은 스타일이다. | 고가/다량/예약 중 물품은 수량·영향을 확인하거나 되돌리기 제공. 저가 폐기물까지 불필요한 확인을 강제하지 않는다. | 위험도와 관계없이 같은 버튼 표현. | 귀중품 스택을 한 번의 오클릭으로 잃지 않음. |
| U14 | P1·화면 | 실제 경보 목록이 관리 패널 우측에 겹쳐 직원 표의 열과 버튼 영역을 가린다. | 경보를 접거나 안전 영역을 확보하고 긴급도/미처리 수만 고정 표시. | 관리 중 정상/과거 경보의 상시 큰 목록. | 직원 표·연구 상세·운영 명령과 경보를 동시에 읽고 조작할 수 있음. |
| U15 | P1·화면 | 연구 첫 화면은 대부분 연결선이고 노드가 아래쪽에 몰려 선택지를 읽기 어렵다. | 초기 맞춤/중심 위치를 실제 viewport 레이아웃 이후 계산하고 읽을 수 있는 확대 수준에서 분야/선행을 탐색. | 읽을 수 없는 전체 그래프를 초기 화면으로 제시. | 처음 열었을 때 선택 연구와 가능한 다음 연구가 보임. |
| U16 | P1·화면 | 건물 진화 카드 등에서 설명 마지막 줄이 카드 아래로 잘리고 다음 카드와 겹친다. 같은 ‘복도’ 재료 선택이 긴 목록에 반복되어 대상 구분도 어렵다. | 본문 높이를 실제 줄 수에 맞추거나 펼침 상세를 두고, 인스턴스는 방/좌표/상태로 구분한다. | 고정 높이의 긴 설명과 구분 정보 없는 반복 카드. | 카드의 원인 설명을 끝까지 읽고 정확한 시설을 선택할 수 있음. |
| U17 | P1·소스/화면 | 건물 상세의 우측 조작 패널이 기존 설명·상태 본문 위에 겹친다. 문, 생산, 냉각기 등 여러 능력에서 반복된다. | 공통 요약과 능력별 조작의 영역을 실제 레이아웃으로 분리하고 닫기/대상 전환을 명시. | 새 조작 패널 아래 남아 있는 중복 본문. | 상태·설명·명령이 서로 가리지 않고 시설을 바꿔도 같은 규칙을 유지. |
| U18 | P1·소스/화면 | 실제 물품 더미 12종이 고정 패널 밖으로 이어져 요약·하단 탭과 겹친다. 행을 58px씩 추가하지만 목록 스크롤/마스크가 없다. | 목록 viewport·스크롤, 고정 요약, 전체 수와 예약/종류 필터. | 화면 높이와 관계없는 무제한 행 배치. | 12종 이상에서 마지막 물품을 읽고 선택하며 요약과 다른 UI를 가리지 않음. |
| U19 | P1 조사·화면 | `직원 관리` 콜백 후 선택 강조만 바뀌고 우선순위 표가 남았다. 1920·1280·2560에서 같은 상태를 관찰했다. | 모드 버튼이 참조하는 presenter와 현재 렌더러가 같은 상태를 쓰는지 확인. 통상 시작 경로에서도 재현 후 수정. | 실제 내용과 맞지 않는 활성 탭 표시. | 실제 입력으로 관리 모드를 열면 관리 내용이 표시되고 재개/재주입 뒤에도 일치. |
| U20 | P1·소스/화면 | 원정 편성은 보유량 0인 모든 장비를 큰 문단으로 나열한다. 보급 버튼이 아래까지 쌓이고 닫기는 공용 명령줄에 가린다. | 보유/필요 장비 중심 비교, 보급 수량 직접 입력, 고정 출발/닫기, 목표 선택 안내. | 전체 장비의 0/0 반복 문단과 한 화면에 펼친 보급 목록. | 선택 목표·부족 자원·출발 가능 여부·닫기를 한 흐름으로 확인. |

소스 근거: [타이틀](../../Assets/Scripts/Views/UI/DungeonTitleUi.cs), [새 런 전환](../../Assets/Scripts/Services/Infrastructure/DungeonSceneNavigation.cs), [저장](../../Assets/Scripts/Views/UI/DungeonSaveUi.cs), [이벤트 배치](../../Assets/Scripts/Views/UI/Core/EventAlertUiFactory.cs), [이벤트 동작](../../Assets/Scripts/Views/UI/Core/EventAlertViewPresenter.cs), [직원 관리](../../Assets/Scripts/Views/Character/UI/StaffManagementSurfacePanel.cs), [준비 조작](../../Assets/Scripts/Views/Character/UI/StartPartyPreparationUiController.cs), [알림](../../Assets/Scripts/Views/UI/Core/NoticeFeedPresenter.cs), [건축](../../Assets/Scripts/Controllers/Grid/DungeonStory/UI/GridConstructTab.cs), [연구](../../Assets/Scripts/Views/UI/ResearchTreeWindow.cs). 세부 심볼/행 근거는 같은 증거 폴더의 정적 조사 노트에 보존한다.

## 전체 화면별 판단과 정리 대상

아래의 ‘삭제’는 주로 중복 표현·진단 노출·사용되지 않는 UI 경로를 뜻한다. 생산·물류·의료·정책·영수증 시스템 자체를 삭제하라는 권고가 아니다.

| 화면/기능 | 적합성 | 보완할 것 | 추가할 것 | 빼거나 합칠 것 |
|---|---|---|---|---|
| 타이틀·계속하기 | 기본 선택 명확, 저장 전환 보호 부족 | 저장 슬롯의 진행·시각·버전·호환 상태를 쉽게 비교 | 새 게임의 기존 런 보존/교체 확인 | 빈 슬롯의 과도한 비중, 종료의 과도한 시각 강조 |
| 난이도·생존 압력 | 두 축 분리는 적합 | 선택이 전투/소비/운영에 주는 실제 차이 | 추천 기본값과 변경 가능 여부 | 모호한 난이도 수식어만으로 설명 |
| 사장 선택 | 핵심 시작 결정에 적합 | 사장별 능력·운영 영향 비교 | 선택 후 시작 구성 요약 | 이름/설명 반복, 장식 없는 큰 빈 영역 |
| 시작 파티 정체성·재능·스킬 | 정보는 있으나 준비 상태/선택 조작 취약 | 생성 중/선택 필요/완료를 구분하고 막힌 이유를 담당 인원에 표시 | 보이는 교체·리롤, 선발 역할/숙련 비교, 생성 실패 재시도 | 숨은 드래그 전용 조작, 준비 중 빈 초상과 의미 불명 점 표시 |
| 화면 설정 | 기본 기능 있음 | 글자 크기 적용 범위·즉시 미리보기 | UI 배율과 최소 가독성 기준 | 소지량 같은 게임 규칙을 화면 설정에 섞는 구조 |
| 소리 설정 | 기반 적합 | 채널별 음량 의미·실제 재생으로 확인 | 음소거 상태, 미리듣기 | 불필요한 반복 설명 |
| 접근성 설정 | 별도 위치는 좋음, 실제 적용 범위 점검 필요 | 고대비/동작 감소/큰 글자의 공통 적용 | 색 외 상태 표시, 키보드 포커스, 툴팁 재열기 | 침공 정책을 접근성에 섞기 |
| 개발 설정 | 개발 중 필요 | 일반 플레이 설정과 명확히 분리 | 개발 모드 표시 | 출시 기본 흐름의 내부 진단 명령 |
| 저장·불러오기·복귀 | 슬롯은 구분되나 위험 동작 방식 불균일 | 행 단위 위험 확인·실패 이유·복구 안내 | 현재 진행과 대상 슬롯 비교 | 4초 재클릭만으로 중요한 확인을 끝내는 방식 |
| HUD·시간·속도·날씨 | 운영 게임 필수, 우선순위 부족 | 위험 상태/일시정지/현재 모드의 구분 | 식량·임금·위협의 임계 경고와 관련 화면 이동 | 정상 자원 10범주를 늘 같은 중요도로 강조 |
| 11개 하단 관리 탭 | 도메인 접근은 충분 | 좁은 화면 라벨·선택 강조, 핵심/보조 구분 | 미처리/완료 badge, 단축키 안내 | 모든 도메인 동일 비중의 고정 확장 |
| 건축 범주·시설 선택 | 배치는 빠르나 비교 부족 | 시설 이름/효과/크기/비용·잠금 상세 | 검색·잠긴 시설 보기·연구 이동 | 아이콘만으로 결정을 요구 |
| 건설 배치·공사 현장 | 실제 상태 연결은 유지 | 배치 가능/불가 이유, 취소 시 회수/손실 | 공정·담당자·남은 자원과 취소 확인 | 결과 예고 없는 즉시 공사 취소 |
| 건물 관리·방/대피 | 기능 혼합으로 부적합 | 방/시설 목록/합성/진화를 하위 탐색으로 분리 | 이름·상태 검색, 월드에서 찾기 | 긴 목록의 무제한 동일 카드 |
| 건물 공통 상세 | 능력별 조건부 표시는 적합 | 상단 상태와 핵심 조작, 섹션 접기 | 입력 부족/출력 막힘/인력 없음 등 원인 링크 | 모든 세부 정책을 처음부터 펼치기 |
| 생산 주문·경로·품질 | 게임에 꼭 필요 | 주문 표, 목표·진행·차단·출력 목적지의 비교 | 다음 소비처·예상 산출·명령 결과 | 큰 카드 반복, 반복적인 숫자 변경 버튼 |
| 농업·처리·수확 | 상태 변수 적합 | 성장·물·온도·노동을 원인별 표시 | 다음 작업·예상 수확·위험 수확 예고 | 적용 불가 처리의 무조건 노출 |
| 축산·산물 | 필요 기능 | 개체/먹이/온도/생산량의 상관관계 | 수용량·문제 개체 이동·다음 산물 | 정상 조건의 긴 반복 설명 |
| 의복·장비 제작 | 고급 설정 접기는 적합 | 재료/신체/숙련 조건 충돌 설명 | 주문 설정으로 나올 결과 요약 | 기본 화면의 고급 세부 옵션 |
| 서비스·허브 연결·유료 시설 | 게임 고유 운영에 필수 | 허브/방 연결과 영업 가능 원인 | 수요·대기·처리량·계약 수입 요약 | 연결 불가 대상을 실행 가능한 것처럼 표시 |
| 환경·온도 | 제어는 가능, 판단 지원 부족 | 현재/목표/비용/영향 범위를 함께 표시 | 직접 입력·preset·영향받는 방 이동 | ±2만 반복 클릭시키는 조작 |
| 문·출입 권한 | preset·복사는 좋음 | 권한 적용 범위와 현재 선택 강조 | 차단될 대상/경로 미리보기·되돌리기 | 전체 개인 목록 기본 펼침 |
| 시설 진화·합성 | 조건은 있으나 비교 취약 | 전/후 능력·유지비·촉매·비가역성 | 후보 비교·부족 자원 링크 | 목록을 별도 화면과 중복 관리하는 표현 |
| 금고 방어·서커스·수술 시설 | 특수 능력 유지 | 현재 수행 가능 상태·원인·담당자 | 다음 행동/유지 비용의 맥락 정보 | 의미 없는 비활성 행동의 반복 |
| 직원 근무·우선순위 | 표 형태는 적합 | 1/2/3/X 범례, 가로 이동/고정 이름·헤더 | 역할 preset·일괄 배정·변경 요약 | 항상 보이는 Brain 디버그 요약 |
| 직원 관리·직접 명령 | 실제 명령은 있음 | 프로필 연결, 비용/거절/지속시간 설명 | 현재 명령·중단/재시도 결과 | 토스트만 띄우는 가짜 프로필 버튼 |
| 캐릭터 상태·기분·건강 | 개체 중심 조회 적합 | 가장 중요한 이상 상태·치료 필요를 위로 | 원인과 담당 시설/정책으로 이동 | 모든 정상 수치 동일 강조 |
| 캐릭터 성장·스킬·기록·인구 | 필요하지만 다중 탐색 부담 | 미선택 성장/준비 상태와 다음 행동 | 선택 전 비용/효과/영구성 비교 | 내부 ID와 진단 문자열 |
| 캐릭터 전투·장비 | 관리/전술 연결 필요 | 장비 조건·명중/피해·거리·명령 설명 | 불가 이유/필요 탄약·도구 링크 | 값 나열만 하고 영향 설명 없는 표현 |
| 캐릭터 세부 7탭 | 깊은 조회는 유지 | 주 화면과 중복을 줄이고 읽는 순서 정리 | 검색/필터·기준값 대비 변화 원인 | `!`만 쓰는 상세 진입 표식 |
| 멘토·학생·숙련 | 장기 육성에 적합 | 멘토/학생/시설/숙련 선택 절차 명시 | 자격·비용·예상 학습 결과 | 월드 클릭 단계의 숨은 상태 |
| 수술·의료 | 시스템 중요도 대비 표현 미흡 | 한국어 용어·필수 자원·의사·시설·위험 | 예약 상태·취소 영향·복구 가능한 실패 설명 | 영문 필드·FailureCode·고정 본문 overflow |
| 기억 삭제·포획·노역 | 위험 결정이므로 보호 필요 | 관계/처우/진행/소유 대상 명확화 | 비가역성·비용·중단/석방 영향 | 운영 탭 속 평범한 버튼으로 위험 결정 처리 |
| 상점·자동 조달·소매 | 명령 충분, 중복/긴 조작 | 상품 비교·보호 자금/예산 직접 입력 | 구매 결과·납품 상태·전체 상품 접근 | 구매/찜의 상품 목록 중복 |
| 창고·운반·납품·계약 | 인과관계 정보는 필요 | 부족/초과/예약/이동 중 구분 | 공급처→운반자→목적지 추적·도착 예상 | 정상 창고 반복 설명·전체 수 없는 상위 8개 |
| 아이템 더미·개별 물품·부패/시체 | 물리 물류의 핵심 진입점 | 긴 상세와 목록 스크롤, 수량/예약/목적지를 플레이어 용어로 | 운반 불가 원인·관련 담당자/시설 이동 | Lease/Slice/복원 진단 및 내부 목적지 ID의 일반 노출 |
| 금고·재정 | 전망 제공은 좋음 | 고정비·잔액·오늘 순수익을 구분 | 기간/항목 필터·거래 관련 명령 이동 | 최근 8건 문자열만 제공 |
| 운영·인력·정비·생존·외부·계승 | 현재 정보구조 부적합 | 즉시 조치와 장기 정책 분리 | 하위 탐색/검색·위험별 담당자/원인 링크 | 정상/개발 정보의 상시 전개 |
| 방어·침공·대피·경비 | 기능 많으나 전시 우선순위 약함 | 현재 침입/대피 우선, 정책 편집은 별도 | 위험 대상 월드 이동·전체/표시 수 | 전시에도 보고/정책 카드가 같은 비중 |
| 원정 요약·세계 지도 | 단계 구분 필요 | 선택 목표의 위험·보상·조건 유지 | 목표→정찰→편성→출정 단계 표시 | 완료 목표가 기본 목록을 차지 |
| 원정 편성·현장·전투 | 별도 전술 화면 유지 | 멤버/보급 부족과 행동 결과 예측 | 목표/진행/철수 영향의 공통 헤더 | 전환 시 대상 맥락 소실 |
| 연구 트리·상세·큐 | 정보 기반은 좋으나 첫 화면·긴 큐 취약 | 초기 맞춤, 검색·차단·해금 시각 계층과 큐 스크롤 | 선행/해금 직접 이동·완료 알림 | 긴 설명을 모두 같은 크기로 나열 |
| 도감·보고·이벤트·결과 원장 | 기록 분류 좋음, 접근성 불균일 | 작은 mode 전환 컨트롤·검색/기간 | 과거 보고·이벤트 전체 접근 | 모드 변경을 큰 카드 4개로 표현 |
| 산업·규모/정책 | 운영과 역할 구분 필요 | 현재 단계·다음 조건·영향 요약 | 관련 시설/자원으로 이동 | 운영과 중복된 설명 |
| 야생동물·생태 | 필요한 맥락, 고정 크기 위험 | 상태 본문 스크롤·8행동 묶기 | 위험/산출/먹이/수용량·적용 가능 이유 | 적용 불가 행동을 모두 한 줄로 배치 |
| 방 검사·환경 overlay | 월드와 원인 연결은 좋음 | 기여 시설 목록의 펼침/스크롤 | 주요 원인과 개선 행동·시설 위치 | 잘리는 contributor 문자열 |
| 경보·선택지·알림 | 필수이나 읽기/실행 경계 취약 | 상세 열기와 선택 분리·긴 본문·4선택지 | 보이는 닫기·이력·실패 재시도 | 우클릭만 가능한 숨은 dismiss |
| 런 종료·계승 결과 | 보상 확인은 가능 | 내부 enum/ID를 의미 있는 결과로 | 원인·핵심 결정·계승 보상·다음 시작 비교 | 다음 런 버튼만 있고 결과 탐색이 약한 구성 |
| 별도 CodexPanel/FacilitySynthesisPanel/FacilityEvolutionPanel | 개발용 단독 화면과 현재 관리 탭 역할 중복 후보 | 실제 플레이 진입·직렬화 참조가 필요한지 구분 | 필요하면 개발 메뉴라고 명시 | 일반 호출을 찾지 못한 단독 factory/패널은 추가 참조 확인 후 정리 후보. 해당 도메인 기능은 유지 |

## 개선 순서와 수용 기준

1. **진행/저장 보호와 잘못된 명령부터**: U01~U05. 기존 게임 상태 권위를 바꾸지 않고 위험 전환과 UI 진입을 바로잡는다. 동시에 읽거나 선택할 수 없는 U14~U20의 배치·모드 문제를 정리한다. U19는 통상 시작 경로 재현이 먼저다.
2. **매일 반복하는 운영 흐름**: U06~U11. 운영·건물·창고·방어의 정상 상태는 접고 차단을 위로 둔다. 목록 수 제한에는 전체 접근을 제공한다. 기능별로 별도의 새 창을 무조건 만들지 않는다.
3. **개체/시설의 원인 설명**: 생산·의료·근무·환경·원정에서 `안 되는 이유 → 해결 대상`을 연결한다. 버튼 비활성만으로 설명을 대신하지 않는다.
4. **공통 표현과 접근성**: 글자 크기, 대비, 포커스, 버튼 높이, 명령 결과를 공통화하고 수술/내부 ID/개발 문구를 정리한다. 픽셀 아트 월드와 메뉴의 분위기를 맞추는 작업은 기능적 문제 뒤에 한다.

대표 수용 시나리오는 식량 부족 원인 찾기, 생산 출력 막힘 해결, 직원 배정, 의료 예약 실패 후 재시도, 침공 중 대피, 원정 편성 부족 해결, 연구 큐 변경, 저장 전환 취소다. 각 시나리오에서 화면 이름만 보고 진입할 수 있는지, 비용/영향을 미리 이해하는지, 완료/실패를 다시 확인할 수 있는지를 검증한다. 단순히 버튼 callback이 실행되거나 컴파일이 통과하는 것으로 UX 완료를 선언하지 않는다.

## 실제 화면 증거와 실행 결과

### 시작과 기본 조작

![타이틀](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/01-title-empty.png)

타이틀의 기본 선택은 명확하다. 다만 새 플레이어에게 비어 있는 저장 슬롯과 종료가 큰 비중을 차지한다. 저장 전환 보호(U01/U02)가 장식 개선보다 먼저다.

![시작 파티 준비](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/05-party-preparation.png)

실제 준비 중 상태다. 초상/특성 생성 완료를 기다리지 않고 촬영했으므로 빈 초상을 영구적인 렌더링 결함으로 판정하지 않는다. ‘어느 인원에게 무엇이 필요한지’는 보이지만 교체 방법과 다음 행동의 발견성은 약하다.

![직원 우선순위](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Staff-top.png)

표 구조는 근무 배정에 적합하다. 그러나 숫자의 범례가 없고, 오른쪽 경보가 작업 열을 가린다. 클릭 우선순위보다 경보 safe area와 표 탐색을 먼저 정리해야 한다.

### 운영과 시설 탐색

![운영 첫 화면](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Operations-top.png)

운영의 첫 화면은 정산과 WU 정보부터 시작한다. 실제 위기/차단을 찾아야 하는 화면에 동일한 카드 표현을 계속 확장하기보다, 조치가 필요한 상태와 장기 정책을 분리해야 한다.

![건물 목록 중간](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Buildings-s0-p20.png)

동일한 ‘복도’가 반복되는 합성 재료 선택 구간이다. 화면을 넘기며 찾는 방식은 시설 수가 늘어날수록 불리하다. 방·좌표·상태로 구분되는 검색/선택 목록이 필요하다.

![건물 진화 설명](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Buildings-s0-p37.png)

설명 줄이 다음 카드에 가려지는 실제 상태다. 이는 취향 문제가 아니라 의사결정 근거를 읽기 어려운 배치 문제다.

![연구 초기 화면](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/11-tab-Research-top.png)

연구의 조건·해금 정보 자체는 풍부하다. 그러나 초기 화면에서 노드보다 연결선이 대부분을 차지하고, 우측 상세도 경보와 겹친다. 초기 viewport와 그래프 탐색을 먼저 고친다.

![세계 지도](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/13-world-map-top.png)

전략 지도로 공간 관계를 보여 주는 방향은 적합하다. 멀리서 모든 지명/세력 라벨을 함께 표시해 겹치는 부분은 확대 단계별 표시와 선택 대상 상세로 분리한다. 이 화면은 원정 출정·연구 해금 성공 증거가 아니다.

### 저장과 캐릭터

![격리 슬롯 저장 화면](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/12-save-populated.png)

실제 격리 저장 명령으로 세 슬롯을 생성한 상태다. 시각과 일차·자금·생존 설정을 보여 주는 점은 유지한다. 불러오기/삭제에 대한 현재 진행 영향과 확인 방식은 통일해야 한다.

![캐릭터 상태](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/20-character-status-top.png)

상태 바와 탭은 개체 중심 조회에 적합하다. ‘!’ 상세 진입과 하단 전투 명령줄은 역할이 모호하고 다른 내용과 겹친다. 일상 운영과 전투 중 명령을 상황에 맞게 분리한다.

![캐릭터 작업 효과 상세](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/21-stats-Work-top.png)

개발 모드가 꺼진 상태인데 `anatomy-node`, `Species`, `effect:capacity`, `proficiency` 등 내부 키와 영어 설명이 본문 대부분을 차지한다. 수치의 근거를 없애는 것이 아니라 ‘부상으로 조작 능력 감소’, ‘숙련으로 작업 속도 증가’처럼 플레이어가 이해할 요약과 펼침 근거로 바꿔야 한다. 통제 파티의 진단 스킬 이름은 이번 UI 문구 결함 판정에서 제외했다.

### 의료·시설·물품

![수술 예약](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/22-surgery-top.png)

실제 의료 조회가 `SurgeryFacilityUnavailable`을 반환하는 상태다. `Procedure / Target / Part / Doctor / Facility`, 영어 위험 설명, 한국어 설명이 섞이고 같은 확률이 반복된다. ‘수술 시설이 없어 예약할 수 없음 → 필요한 시설 보기’로 이어져야 한다. 이번에는 수술 완료/실패를 실행한 것이 아니라 예약 화면과 불가 상태를 관찰했다.

![문과 건물 상세 겹침](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Door-top.png)

권한 preset·복사·같은 방 적용은 이 게임에 맞는 기능이다. 그러나 기존 설명과 상태가 조작 패널 뒤에 남는다(U17). 조작 패널은 뿌리 레이아웃의 오른쪽 38~98%에 놓이고 기존 chrome 숨김은 `ButtonSelection`만 다루므로, 설명 본문을 위한 별도 영역 계약이 필요하다. 시설별 새 클래스를 늘려 해결할 문제가 아니다.

![물품 더미](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/33-item-pile.png)

실제 48개·12종 물품 더미다. 아래 행들이 합계와 하단 메뉴에 겹친다(U18). 적은 물품만 있는 초기 fixture로는 발견하기 어려운 문제다. 물리 재고·예약·운반 목적지 정보는 유지하고 목록의 크기와 탐색을 제한해야 한다.

![직원 모드 불일치](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/24-staff-management-top.png)

실제 관리 버튼 콜백을 실행했는데 초록색 선택만 ‘직원 관리’로 옮겨지고 본문은 우선순위 표다(U19). 단순한 촬영 이름을 관리 화면이 열린 증거로 세지 않았다. 재주입 시 `managementSurface`를 비우는 경로와 기존 버튼 closure의 관계가 조사 후보지만, 원인 확정과 통상 입력 재현은 후속 수정 때 필요하다.

### 선택 사건·원정·전투

![네 가지 사건 선택지](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/50-event-four-choices.png)

![긴 사건 본문](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/51-event-long-detail.png)

실제 이벤트 UI에 통제 사건을 전달했다. 첫 그림은 네 번째 선택지가 화면 밖으로 나가는 모습, 둘째는 본문이 선택 버튼·경보·하단 메뉴까지 넘어가는 모습이다. 선택 콜백은 무동작 fixture이므로 사건 효과 성공 증거는 아니다. 본문·선택지 영역을 나누고 모든 결정을 읽고 고를 수 있게 하는 것이 우선이다.

![원정 편성](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/35-expedition-view-only-top.png)

공개 presentation 진입점으로 연 목표 미선택 상태다. 연구 해금·정찰·자연 출정 성공을 증명하지 않는다. 그래도 0/0 장비를 모두 나열하고 보급 목록과 닫기가 화면 하단까지 밀리는 문제는 확인할 수 있다(U20). 목표 미선택이면 먼저 목표 선택으로 이끌어야 한다.

![원정 전투 시작](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/53-battle-controlled-top.png)

실제 전투 runtime에 통제 원정 run을 넣어 작성된 ‘외곽 식재료 농장’ 전투를 시작했다. 적·아군·보호 대상·순서·승리 조건은 보인다. 상대 차례에서 ‘행동을 선택하세요’가 누구를 향한 안내인지 모호하고 `Normal`, `Safety` 같은 용어가 섞인다. 전체 전투 승리나 플레이어 턴의 모든 명령은 검증하지 않았다. 하단 던전 전투 명령줄도 계속 보이므로 전술 화면과 월드 명령의 적용 대상을 구분해야 한다.

### 해상도·접근성

![1280 직원 화면](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/41-Staff-1280.png)

1280×720에서도 하단 탭은 유지되지만 표의 오른쪽 열과 아래 영역을 경보·전투 명령줄이 가린다. 2560×1440에서도 같은 상대적 겹침을 관찰했다. 모니터 해상도를 올리는 것으로 해결할 문제가 아니다.

![큰 글자와 고대비 설정](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/07-settings-max-contrast.png)

설정에서 글자 125%와 고대비를 켠 상태는 실제 렌더링했다. 이후 기본값으로 복귀해 나머지를 촬영했다. 따라서 **모든 화면의 큰 글자·고대비 조합을 통과했다고 말하지 않는다.** 글자 배율 적용 기반은 이미 있다. EventAlert/Notice의 색·동작 감소 반영은 개별 경로를 확인해야 하며, 전역 접근성 기능이 전부 없다는 판정은 아니다. 색 이외의 상태 표식과 현재 모드·포커스를 더 명확히 하는 것을 권고한다.

## 검증 결과와 남은 관찰 범위

- 중복 ID는 최신 유효 촬영으로 정리한 **PNG 269장**, 대표 첫 화면/상태 **132개**다. 이것을 서로 다른 독립 UI 132개나 모든 플레이 상태 269개로 해석하지 않는다. 동일 패널의 시설·페이지·해상도 변형을 포함한다.
- 1920×1080을 기본으로 조사했고 1280×720·2560×1440에서는 HUD·직원·운영·연구를 추가 촬영했다. 모든 PNG의 실제 크기와 촬영 메타데이터의 요청/렌더 크기가 일치하며 SHA-256을 보존했다.
- 최종 `capture-r7`: 현재 Editor 소스 컴파일 **24.976083초, C# 오류 0**, 실제 Play 촬영 **126장**, 캡처 manifest `completed=true`, 수집된 실행 오류 **0**, Unity exit **0**. 비대화형 station/desktop 창 생성 프로브도 PASS다. 이는 캡처 실행 성공이며 게임 UX 합격 판정은 아니다.
- 이전 유효 화면은 r4·r6에서 보존했다. r4는 촬영 도구의 원정 창 접근 예외, r6는 긴 반복 촬영을 줄이기 위한 담당 프로세스 중단으로 전체 실행 PASS가 아니다. r1/r2 촬영 도구 결함, r3/r5 메모리 보호 중단 기록도 보존한다. 뒤집힘/해상도 불일치 이미지와 실제 열리지 않은 원정·개발 AI 화면은 최종 인덱스에서 제외했다.
- 시설 8종은 실제 factory·정의·grid 등록으로 통제 배치해 능력별 상세를 열었다. 기억 삭제는 대상 없는 상태, 야생 overlay는 현재 월드 상태, 룸/아이템은 실제 조회 상태를 관찰했다. 자원 소비·공사 완료·연구 해금·판매·의료·기억 삭제 성공의 증거로 세지 않는다.
- **소스로 평가했지만 해당 상태의 실제 이미지는 미확보**: 직원 관리 본문(U19), 개발 AI 탭, 장기 포획/노역·시체 분해·장기 보고 이력·긴 연구 큐·공사 진행/취소·활성 서커스/수술 시설·시설 진화/합성 성공·침공 진행/대피·전투의 모든 턴·LLM 시작 준비 완료. 화면 가족의 전수 조사는 끝냈지만 모든 데이터 조합과 진행 상태를 재현한 인증은 아니다.
- 실제 마우스/키보드/패드, 포커스 이동·탭 순서·hover tooltip·드래그·스크린리더·오디오 사용성·장시간 플레이는 이번 검증에 포함하지 않았다. 위 수용 시나리오의 후속 사용성 검증에서 다룬다.

원본/해시/해상도는 [캡처 인덱스](../../Artifacts/QA/UiUxAudit-20261018/capture-index.json), 실행 결합은 [최종 검증 기록](../../Artifacts/QA/UiUxAudit-20261018/verification-final.json)에 있다. [캐릭터·셸 정적 근거](../../Artifacts/QA/UiUxAudit-20261018/static-character.md)와 [운영·시설 정적 근거](../../Artifacts/QA/UiUxAudit-20261018/static-operations.md)는 세부 심볼 탐색용 부록이며, 최종 판단과 수정 순서는 이 문서가 소유한다.

## 병합 및 변경 범위

SO 수정은 [PR #9](https://github.com/Creeper0809/DungeonStory/pull/9)로 원격 main에 병합했다. 병합 커밋은 위 기준 커밋이다. GitHub의 기존 V27 ledger 검사는 `.gitattributes` digest 불일치로 이전 main과 같은 실패가 있어 별도로 남겼다. 이번 SO 변경이 해당 파일·검사 계약을 바꾼 것은 아니며 검사 완화를 하지 않았다.

이번 UI 작업은 평가와 증거 생성이다. 위 개선 권고를 생산 코드에 일괄 적용한 작업이 아니다. Unity Editor 전용 캡처와 해당 진입점만 허용하는 격리 그래픽 실행 경로를 추가했다. 사용자 저장과 미저장 씬을 변경하지 않는다.

## 대표 화면 전체 찾아보기

아래 132개는 대표 이미지의 실제 파일 링크다. 화면 ID는 캡처 시도와 상태를 구분하는 식별자이며 독립 메뉴 수가 아니다. 각 PNG와 같은 이름의 JSON에는 당시 텍스트·버튼·해상도가 있다. 스크롤 중간/마지막 페이지까지 포함한 269장의 전체 목록은 캡처 인덱스를 따른다. 대표 화면은 축소 묶음으로 전체 배치를 검토하고, 본문의 주요 결함은 원본 해상도로 추가 확인했다.

### 시작·설정

| 화면 ID | 원본 화면 | 해상도 |
|---|---|---|
| 01-title-empty | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/01-title-empty.png) | 1920×1080 |
| 02-settings-AccessibilitySettingsTab | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/02-settings-AccessibilitySettingsTab.png) | 1920×1080 |
| 02-settings-AudioSettingsTab | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/02-settings-AudioSettingsTab.png) | 1920×1080 |
| 02-settings-DeveloperSettingsTab | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/02-settings-DeveloperSettingsTab.png) | 1920×1080 |
| 02-settings-DisplaySettingsTab | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/02-settings-DisplaySettingsTab.png) | 1920×1080 |
| 03-difficulty | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/03-difficulty.png) | 1920×1080 |
| 04-owner-selection | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/04-owner-selection.png) | 1920×1080 |
| 05-party-preparation | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/05-party-preparation.png) | 1920×1080 |
| 06-PreparationTab_1_Aptitude | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/06-PreparationTab_1_Aptitude.png) | 1920×1080 |
| 06-PreparationTab_1_Identity | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/06-PreparationTab_1_Identity.png) | 1920×1080 |
| 06-PreparationTab_1_Skill | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/06-PreparationTab_1_Skill.png) | 1920×1080 |
| 07-settings-max-contrast | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/07-settings-max-contrast.png) | 1920×1080 |

### HUD·관리·저장·지도

| 화면 ID | 원본 화면 | 해상도 |
|---|---|---|
| 10-gameplay-hud | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/10-gameplay-hud.png) | 1920×1080 |
| 11-tab-Buildings-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Buildings-top.png) | 1920×1080 |
| 11-tab-Codex-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/11-tab-Codex-top.png) | 1920×1080 |
| 11-tab-Construction-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Construction-top.png) | 1920×1080 |
| 11-tab-Defense-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Defense-top.png) | 1920×1080 |
| 11-tab-Expedition-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Expedition-top.png) | 1920×1080 |
| 11-tab-Industry-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/11-tab-Industry-top.png) | 1920×1080 |
| 11-tab-Operations-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Operations-top.png) | 1920×1080 |
| 11-tab-Research-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/11-tab-Research-top.png) | 1920×1080 |
| 11-tab-Shop-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Shop-top.png) | 1920×1080 |
| 11-tab-Staff-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Staff-top.png) | 1920×1080 |
| 11-tab-Warehouse-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/11-tab-Warehouse-top.png) | 1920×1080 |
| 12-save-empty | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/12-save-empty.png) | 1920×1080 |
| 12-save-populated | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/12-save-populated.png) | 1920×1080 |
| 13-world-map-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r4/unity-evidence/13-world-map-top.png) | 1920×1080 |
| 15-research-selected-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/15-research-selected-top.png) | 1920×1080 |
| 16-treasury-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/16-treasury-top.png) | 1920×1080 |
| 17-P2Action_ArchiveEvents-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/17-P2Action_ArchiveEvents-top.png) | 1920×1080 |
| 17-P2Action_ArchiveReports-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/17-P2Action_ArchiveReports-top.png) | 1920×1080 |
| 17-P2Action_OutcomeLedger-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/17-P2Action_OutcomeLedger-top.png) | 1920×1080 |

### 캐릭터·의료·직원

| 화면 ID | 원본 화면 | 해상도 |
|---|---|---|
| 20-character-combat-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/20-character-combat-top.png) | 1920×1080 |
| 20-character-growth-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/20-character-growth-top.png) | 1920×1080 |
| 20-character-health-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/20-character-health-top.png) | 1920×1080 |
| 20-character-mood-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/20-character-mood-top.png) | 1920×1080 |
| 20-character-population-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/20-character-population-top.png) | 1920×1080 |
| 20-character-records-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/20-character-records-top.png) | 1920×1080 |
| 20-character-status-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r6/unity-evidence/20-character-status-top.png) | 1920×1080 |
| 21-mentorship | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/21-mentorship.png) | 1920×1080 |
| 21-stats-BaseStats-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/21-stats-BaseStats-top.png) | 1920×1080 |
| 21-stats-CombatEquipment-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/21-stats-CombatEquipment-top.png) | 1920×1080 |
| 21-stats-HealthAnatomy-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/21-stats-HealthAnatomy-top.png) | 1920×1080 |
| 21-stats-Modifiers-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/21-stats-Modifiers-top.png) | 1920×1080 |
| 21-stats-Proficiencies-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/21-stats-Proficiencies-top.png) | 1920×1080 |
| 21-stats-Summary-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/21-stats-Summary-top.png) | 1920×1080 |
| 21-stats-Work-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/21-stats-Work-top.png) | 1920×1080 |
| 22-surgery-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/22-surgery-top.png) | 1920×1080 |
| 24-staff-management-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/24-staff-management-top.png) | 1920×1080 |
| 25-memory-erasure-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/25-memory-erasure-top.png) | 1920×1080 |

### 기존 배치 시설

| 화면 ID | 원본 화면 | 해상도 |
|---|---|---|
| 30-building-D01_간이화덕-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-D01_간이화덕-top.png) | 1920×1080 |
| 30-building-D02_고기그릴-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-D02_고기그릴-top.png) | 1920×1080 |
| 30-building-D03_조리손질대-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-D03_조리손질대-top.png) | 1920×1080 |
| 30-building-D04_배식카운터-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-D04_배식카운터-top.png) | 1920×1080 |
| 30-building-D11_고기걸이-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-D11_고기걸이-top.png) | 1920×1080 |
| 30-building-Door-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Door-top.png) | 1920×1080 |
| 30-building-E01_벽횃불-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-E01_벽횃불-top.png) | 1920×1080 |
| 30-building-E03_샹들리에-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-E03_샹들리에-top.png) | 1920×1080 |
| 30-building-E04_소형러그-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-E04_소형러그-top.png) | 1920×1080 |
| 30-building-E05_세력깃발-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-E05_세력깃발-top.png) | 1920×1080 |
| 30-building-E06_액자초상화-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-E06_액자초상화-top.png) | 1920×1080 |
| 30-building-E09_방표지판-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-E09_방표지판-top.png) | 1920×1080 |
| 30-building-Exterior_DropZone_FloorOverlay-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Exterior_DropZone_FloorOverlay-top.png) | 1920×1080 |
| 30-building-Exterior_ExpeditionStaging_WallFixture-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Exterior_ExpeditionStaging_WallFixture-top.png) | 1920×1080 |
| 30-building-Exterior_GuardPost_FloorOverlay-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Exterior_GuardPost_FloorOverlay-top.png) | 1920×1080 |
| 30-building-Exterior_IncidentPoint_CeilingFixture-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Exterior_IncidentPoint_CeilingFixture-top.png) | 1920×1080 |
| 30-building-Exterior_OutdoorRestSpot_WallFixture-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Exterior_OutdoorRestSpot_WallFixture-top.png) | 1920×1080 |
| 30-building-Exterior_PatrolPoint_WallFixture-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Exterior_PatrolPoint_WallFixture-top.png) | 1920×1080 |
| 30-building-Exterior_ReceptionPoint_FloorOverlay-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Exterior_ReceptionPoint_FloorOverlay-top.png) | 1920×1080 |
| 30-building-Hallway-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Hallway-top.png) | 1920×1080 |
| 30-building-InteriorDoor-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-InteriorDoor-top.png) | 1920×1080 |
| 30-building-L01_대형보관선반-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-L01_대형보관선반-top.png) | 1920×1080 |
| 30-building-L02_상자더미-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-L02_상자더미-top.png) | 1920×1080 |
| 30-building-L03_통더미-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-L03_통더미-top.png) | 1920×1080 |
| 30-building-M02_마력저장조-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-M02_마력저장조-top.png) | 1920×1080 |
| 30-building-M03_룬안정기-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-M03_룬안정기-top.png) | 1920×1080 |
| 30-building-Q01_연구책상-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Q01_연구책상-top.png) | 1920×1080 |
| 30-building-Q03_연구용책장-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Q03_연구용책장-top.png) | 1920×1080 |
| 30-building-Q04_시약선반-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Q04_시약선반-top.png) | 1920×1080 |
| 30-building-Q06_설계판-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Q06_설계판-top.png) | 1920×1080 |
| 30-building-R01_간이침대-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-R01_간이침대-top.png) | 1920×1080 |
| 30-building-R02_정식침대-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-R02_정식침대-top.png) | 1920×1080 |
| 30-building-R05_협탁-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-R05_협탁-top.png) | 1920×1080 |
| 30-building-R06_옷장-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-R06_옷장-top.png) | 1920×1080 |
| 30-building-R08_지휘의자-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-R08_지휘의자-top.png) | 1920×1080 |
| 30-building-R09_개인보관함-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-R09_개인보관함-top.png) | 1920×1080 |
| 30-building-R10_침실용책장-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-R10_침실용책장-top.png) | 1920×1080 |
| 30-building-S01_판매카운터-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-S01_판매카운터-top.png) | 1920×1080 |
| 30-building-S02_잡화진열선반-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-S02_잡화진열선반-top.png) | 1920×1080 |
| 30-building-S05_무기거치대-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-S05_무기거치대-top.png) | 1920×1080 |
| 30-building-S06_갑옷거치대-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-S06_갑옷거치대-top.png) | 1920×1080 |
| 30-building-S07_무기보관함-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-S07_무기보관함-top.png) | 1920×1080 |
| 30-building-Stair-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-Stair-top.png) | 1920×1080 |
| 30-building-T01_훈련허수아비-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-T01_훈련허수아비-top.png) | 1920×1080 |
| 30-building-T02_사격과녁-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-T02_사격과녁-top.png) | 1920×1080 |
| 30-building-T03_중량훈련석-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-T03_중량훈련석-top.png) | 1920×1080 |
| 30-building-T04_대련매트-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-T04_대련매트-top.png) | 1920×1080 |
| 30-building-WorldResourceNode-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/30-building-WorldResourceNode-top.png) | 1920×1080 |

### 건축 범주·월드 정보

| 화면 ID | 원본 화면 | 해상도 |
|---|---|---|
| 31-construction-category-0 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/31-construction-category-0.png) | 1920×1080 |
| 31-construction-category-1 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/31-construction-category-1.png) | 1920×1080 |
| 31-construction-category-2 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/31-construction-category-2.png) | 1920×1080 |
| 31-construction-category-3 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/31-construction-category-3.png) | 1920×1080 |
| 31-construction-category-4 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/31-construction-category-4.png) | 1920×1080 |
| 31-construction-category-5 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/31-construction-category-5.png) | 1920×1080 |
| 31-construction-category-6 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/31-construction-category-6.png) | 1920×1080 |
| 31-construction-category-7 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/31-construction-category-7.png) | 1920×1080 |
| 32-room-inspection | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/32-room-inspection.png) | 1920×1080 |
| 33-item-detail | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/33-item-detail.png) | 1920×1080 |
| 33-item-pile | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/33-item-pile.png) | 1920×1080 |
| 33-wildlife | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/33-wildlife.png) | 1920×1080 |
| 33-wildlife-overlay | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/33-wildlife-overlay.png) | 1920×1080 |

### 통제 배치 시설·원정 편성

| 화면 ID | 원본 화면 | 해상도 |
|---|---|---|
| 34-facility-CB01_야수우리-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/34-facility-CB01_야수우리-top.png) | 1920×1080 |
| 34-facility-D04_배식카운터-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/34-facility-D04_배식카운터-top.png) | 1920×1080 |
| 34-facility-Door-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/34-facility-Door-top.png) | 1920×1080 |
| 34-facility-E10_냉각기-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/34-facility-E10_냉각기-top.png) | 1920×1080 |
| 34-facility-P01_제분소-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/34-facility-P01_제분소-top.png) | 1920×1080 |
| 34-facility-P1_TreasuryBoltThrower-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/34-facility-P1_TreasuryBoltThrower-top.png) | 1920×1080 |
| 34-facility-P23_야외경작지-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/34-facility-P23_야외경작지-top.png) | 1920×1080 |
| 34-facility-V22_9301_재단_재봉_작업대-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/34-facility-V22_9301_재단_재봉_작업대-top.png) | 1920×1080 |
| 35-expedition-view-only-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/35-expedition-view-only-top.png) | 1920×1080 |

### 다른 해상도

| 화면 ID | 원본 화면 | 해상도 |
|---|---|---|
| 40-hud-1280 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/40-hud-1280.png) | 1280×720 |
| 40-hud-2560 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/40-hud-2560.png) | 2560×1440 |
| 41-Operations-1280 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/41-Operations-1280.png) | 1280×720 |
| 41-Operations-2560 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/41-Operations-2560.png) | 2560×1440 |
| 41-Research-1280 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/41-Research-1280.png) | 1280×720 |
| 41-Research-2560 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/41-Research-2560.png) | 2560×1440 |
| 41-Staff-1280 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/41-Staff-1280.png) | 1280×720 |
| 41-Staff-2560 | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/41-Staff-2560.png) | 2560×1440 |

### 통제 사건·결과·전투

| 화면 ID | 원본 화면 | 해상도 |
|---|---|---|
| 50-event-four-choices | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/50-event-four-choices.png) | 1920×1080 |
| 51-event-long-detail | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/51-event-long-detail.png) | 1920×1080 |
| 52-run-result-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/52-run-result-top.png) | 1920×1080 |
| 53-battle-controlled-top | [PNG](../../Artifacts/QA/UiUxAudit-20261018/capture-r7/unity-evidence/53-battle-controlled-top.png) | 1920×1080 |

