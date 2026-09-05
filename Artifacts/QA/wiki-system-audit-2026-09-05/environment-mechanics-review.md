# 환경장·노출·작업 안전 부분 감사

전체 시스템 감사는 진행 중이다. 이 보고서는 환경 계산과 작성시설11개를 대조한 부분 결과이며 코드·자산·공개 위키를 수정하지 않았다. 밸런스 영향 없음.

## 모집단과 누락

열원2·공기3·덕트3·조명5·보호함1, 총14모듈의 선택50필드를 도감11개와 대조했다.11개 모두 루트→도메인 카탈로그에 연결되어 있다. 도감 facts에는 분류·크기만 있으며 성능 수치가 없다. 공통 메타필드·조명 settings 참조는50필드에 포함하지 않는다.

| ID | 시설 | 선택 모듈 | 도감 판정 |
| --- | --- | --- | --- |
| 9824 | [전기 아크등](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9824.json>) | Lighting | 성능 수치 미노출 |
| 1066 | [샹들리에](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1066.json>) | Lighting | 성능 수치 미노출 |
| 1065 | [바닥화로](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1065.json>) | Lighting | 성능 수치 미노출 |
| 1064 | [벽횃불](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1064.json>) | Lighting | 성능 수치 미노출 |
| 1505 | [배기팬](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1505.json>) | AirExchange, AirDuct | 성능 수치 미노출 |
| 1504 | [송풍구](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1504.json>) | AirExchange, AirDuct | 성능 수치 미노출 |
| 1503 | [환기덕트](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1503.json>) | AirDuct | 성능 수치 미노출 |
| 1502 | [공조기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1502.json>) | ThermalEmitter, AirExchange | 성능 수치 미노출 |
| 1501 | [냉각기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1501.json>) | ThermalEmitter | 성능 수치 미노출 |
| 1070 | [촛대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1070.json>) | Lighting | 성능 수치 미노출 |
| 1500 | [보호장비보관함](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1500.json>) | ProtectiveEquipmentLocker | 성능 수치 미노출 |

필드별 원문값·요약·facts·원본/도감 hash는 [JSON](environment-mechanics-review.json)에 보존했다. 사용되지 않는 보호함 정원과 비활성 배기값 등을 현재 적용 효과로 세지 않는다.

## 보완할 문서

### GAP-081 온도·공기·조명 확산과 벽·문·덕트의 실제 작용 누락

- 담당 문서: weather-seasons-and-environment
- 현재 설명: 날씨 문서는 칸별 환경과 연결 변경을 개괄하지만 확산·실내외 복귀·벽 차단·덕트 효과를 설명하지 않는다. 문 개방과 지붕·배관이 환경 연결을 바꾼다는 표현은 현재 환경장 코드의 판정과 구분해야 한다.
- 추가해야 할 내용: 환경장은 시뮬레이션1초마다 인접 상하좌우의 온도·공기·조명을 교환한 뒤 열원·환기·조명을 적용한다. 유효 이웃의 차이×교환율을 평균하며 교환율은 일반0.12/문0.55와 두 칸의 덕트값 중 최댓값이다. 빛의 이웃 교환에는0.6을 곱한다. 실외/실내 온도는 외기에0.35/0.08, 공기질은100에0.5/0.015, 조명은70/20에0.5/0.08의 비율로 접근한다. 확산 뒤 온도는-50~80, 공기·조명은0~100이다. 실내 판정은 DungeonInterior이며 벽은 이웃과 발생원 직선 효과를 막고 문은 차단하지 않는다. 현재 코드는 문의 열림 정도를 읽지 않으므로 개폐별 효과를 확정해서 쓰지 말고 구현 확인 결과와 함께 정정한다. 덕트는 두 칸의 국소 교환율이지 멀리 떨어진 방을 잇는 별도 공기망이 아니다.
- 확인 심볼: StepDiffusion / RebuildTopology / VisitRadius / HasLineOfEffect

### GAP-082 환경시설 11개의 성능·목표 온도 조절·효과 범위 누락

- 담당 문서: infrastructure / entry/facility/building-*
- 현재 설명: 대응 도감11개 facts는 분류와 크기뿐이다. 환기덕트·보호장비보관함은 기능 설명 대신 내부 타입명이 요약에 나타나며, 공통 문서도 시설별 목표·속도·반경과 조절 방법을 설명하지 않는다.
- 추가해야 할 내용: 열원2·공기3·덕트3·조명5·보호함1의14모듈·선택50필드를 대응표로 보완한다. 냉각기는 기본8°C/조절2~8°C/중심3°C초당/반경3/배기배율1.15이고 공조기는22°C/2~30°C/2.5°C초당/반경4다. 조절은 실제 시설 패널에서±2°C이며 범위 밖 입력은 경계값으로 제한한다. 공조기·송풍구·배기팬의 공기회복은각6/8/12초당, 반경4/3/4이며 현재 셋 모두외기교환·전력필요다. 덕트3개는교환율0.65다. 조명5개 세기/반경은벽횃불0.75/2.8,바닥화로0.9/3.2,샹들리에1.15/4.2,촛대0.5/2.1,전기아크등1.2/5.5다. 환경장 빛은세기×100을100으로제한하고 반경은올림정수, 맨해튼거리 d에서1-d/(r+1)로감쇠한다. 보호함은출발/목적지중가까운쪽에서12칸내자동착용조건에쓰인다. 보관정원4와 공조기의비냉각모드배기값 등 비소비·비활성필드를 현재효과로포장하지 않는다. 전력·연료와조명연결, 냉각기역방향온도·배기동작은 보고서의 구현확인사항을 먼저해결한다.50필드를50개독립누락으로세지않는다.
- 확인 심볼: ApplySources / ApplyThermalSource / ApplyLightSource / TrySetTargetTemperature / Render / HasReachableLocker

### GAP-083 추위·더위·공기 노출과 시각 피로의 누적·회복 공식 누락

- 담당 문서: weather-seasons-and-environment
- 현재 설명: 현재 날씨·시설 문서는 환경 위험을 정성적으로 설명하며 건강·업무 참고 문서에도 네 노출의 축적률, 회복률, 단계 경계가 없다.
- 추가해야 할 내용: 살아 있는 정착지 인원은1초마다 현재 칸에서 추위·더위·공기 노출과 시각 피로를0~100으로 누적한다. 원정 중 인원은 이 칸별 계산에서 제외한다. 온도 노출은 종족의 쾌적/안전/치명 경계와 의복·특성·침구 보정을 사용한다. 쾌적범위0, 쾌적경계~안전경계는0.15×정규화거리^1.5, 안전경계밖~치명경계는0.5+1.5×정규화거리^1.5, 치명경계이상은2에 해당 냉/열 보호배율을 곱한다. 공기질70이상0,70~40은0.15×((70-Q)/30)^1.5,40미만~20은0.5+1.5×((40-Q)/20)^1.5,20미만2다. 정밀·수술 작업의 조명50미만에서 시각피로는0.15+0.85×(1-clamp01(L/50))^1.5로 증가한다. 각 쾌적조건에서는1.5/초 회복하며 비정밀작업은 시각피로를 회복한다. 추위와 더위는 온도가 쾌적범위 안일 때만 함께 회복한다. 생리단계는추위/더위/공기중최댓값,시각단계는별도다. 상승문턱25/50/75/100, 하강문턱은부담20미만/기능저하45미만/위급70미만이다.100단계에는 별도의5점유지문턱을 적용하지 않는다. 네 누적값·단계·위급피해타이머·냉기휴식잠금은 저장되며 실제 왕복 검증은 별도다. 종족/의복 개별 수치는 각 권위 항목을 참조한다.
- 확인 심볼: CalculateTemperatureRates / CalculateSideRate / CalculateAirExposureRate / CalculateVisualStrainRate / StepExposure / ResolveBand / Capture

### GAP-084 환경 노출의 작업·이동·기분·피해·명중률 보정 누락

- 담당 문서: weather-seasons-and-environment
- 현재 설명: 전투 문서는 조명 배율을 언급하지만 현재 환경 노출 감점과 어두운 칸의 추가 사격 감점을 설명하지 않는다. 날씨·주민 문서도 단계별 속도와 피해를 제시하지 않는다.
- 추가해야 할 내용: 안정/부담/기능저하/위급/쓰러짐 순으로 실제 일반 작업 배율은1/0.9/0.75/0.5/0.1, 정밀작업은1/0.85/0.6/0.35/0.35, 이동은1/0.95/0.85/0.7/0.1이다. 정밀작업은생리·시각단계중나쁜쪽을사용한다. 부담/기능저하/위급에 진입하면기분-5/-10/-20을15초로요청하고 단계가유지되는동안매초갱신하지않는다. 쓰러짐 진입은억제100추가다. 위급이상에서는10초마다최대HP1%비치명피해, 치명온도또는공기질20미만에서는별도로최대HP1%/초의사망가능피해가들어간다. 환경생리단계가기능저하면명중률10%p,위급/쓰러짐이면25%p감점한다. 사격은현재조명20미만40%p/40미만25%p/50미만10%p를추가감점하고최종5~95%로제한한다. 기존전투조명배율과이감점의적용순서를구분하여참조한다. 작업예측Assessment의새속도표를실제Legacy작업배율과혼동하지않는다.
- 확인 심볼: ResolveLegacyWorkSpeed / ResolveEnvironmentWorkSpeed / ApplyBandEffects / GetMoveSpeed / ApplyEnvironmentAccuracyPenalty

### GAP-085 환경 위험에 따른 작업 거부·방한복 착용·재배정·대피 조건 누락

- 담당 문서: residents-and-work
- 현재 설명: 주민·시설 문서는 환경과 보호구, 교대를 일반적으로 언급하지만 작업 시작 예측과 냉기휴식15/10, 작업 중단50/75, 자동착용·대피칸 선택을 설명하지 않는다.
- 추가해야 할 내용: 작업 시작 시 현재 노출에 이동경로와 남은 작업의 예측노출을 더해 평가한다. 일반 작업은예측위급/치명환경을거부하며 냉기노출15이상에서휴식잠금을켜10미만에서해제한다. 잠금중추위노출이있는경로/목적지작업을막는다. 추위종료예측25이상에서보호장비자동착용을시도하고재평가한다. 보호함은출발지나목적지에서맨해튼12칸내에있어야하며소지재고와해금된보호복중방한성능순으로선택한다. 이는실제경로접근과수용정원검증을뜻하지않는다. 진행작업은1초마다점검하여실제노출기능저하(상승50)에서진행률보존·재배정, 위급(상승75)또는치명채널이면대피를요청한다. 대피칸은도달가능칸중회복가능여부→노출속도→경로비용→좌표순이다. 강제시작은시작제한을넘지만환경피해를면제하지않는다. 현재예측은경로한칸을노출1초분으로누적하고쾌적이동중회복을빼지않으므로실제시간적분과구분한다. Defense 예외의상충과신분/업무별특수예외진입은구현확인사항으로남긴다.
- 확인 심볼: AssessStart / Project / ResolveColdCooldown / Decide / TryAutoEquipForCold / HasReachableLocker / ShouldInterruptForEnvironment / TryFindEvacuationCell

### GAP-086 보관 온도에 따른 음식 신선도 감소 배율 누락

- 담당 문서: food-and-ecology
- 현재 설명: 식량 문서는 부패 손실과 저장 조건을 언급하지만 현재 칸 온도에 따른 신선도 감소와 보존식 추가배율을 설명하지 않는다.
- 추가해야 할 내용: 환경장이 준비된 경우 음식의온도부패배율은clamp(2^((T-20)/10),0.25,4)다.0/10/20/30/40°C에서각0.25/0.5/1/2/4배이며 범위를넘어도최소0.25·최대4배로제한한다. 식품부패의시간진행처리1회는180×온도배율만큼남은신선도초를빼고, 보존상태면여기에0.25를곱한다. 따라서저온과보존효과는곱해지며낮은온도만으로시간감소가완전히멈추지않는다. 환경장이없을때만폭염1.35/한파0.45/일반1의기상분기가사용된다. 해당처리의하루호출주기·신선도초의게임시간환산·보존상태생성·식품전체와장기보관은후속조사로연결한다. IsOrganPreservationSafe의2~8°C정의는라이브소비가미확인이므로음식공식과섞어검증된장기규칙으로쓰지않는다.
- 확인 심볼: GetFoodSpoilageMultiplier / SurvivalFoodSpoilageRuntime.Process / ReadFreshness

## 구현 확인 사항

### ENV-U01 조명의 전력·연료 연동

EnvironmentalFieldSourceDescriptor.RequiresPower는 thermal/air의 requiresPower만 OR한다. 조명만 가진 전기 아크등의 필드 밝기는 자체 PowerConsumer를 여기서 검사하지 않는다. 횃불·화로·촛대·샹들리에의 FuelConsumer도 이 경로에 없다.

확인할 일: 실제 화면 밝기/환경장 조명과 전력·연료 고갈의 일치 여부 확인. 일반 전원 필요 설명으로 숨기지 않는다.

### ENV-U02 냉각기의 역방향 온도와 배기

Cool은 max(target,current-amount)이므로 현재 온도가 목표보다 낮으면 목표까지 올린다. 배기는 실제 냉각량이나 가동 필요와 무관하게 rate×1.15를 더한다. Heat의 min도 현재가 목표보다 높으면 낮추지만 이번 작성대상에 Heat 모드는 없다.

확인할 일: 의도와 실제 플레이를 확인하고 설명 또는 구현의 권위를 정한다. 수정을 수행하지 않았다.

### ENV-U03 문 개방·지붕·배관의 환경장 설명

RebuildTopology는 IsWall/IsDoor, AreaType, duct를 읽는다. 문 개방값 자체는 읽지 않고 이웃 문 교환0.55는 고정이다. 공기 덕트도 유체 연결망과 독립된 국소값이다.

확인할 일: 문 개폐와 실내/지붕 분류를 바꾸는 상위 생산자를 조사하여 위키의 개폐·지붕·배관 효과를 분리한다.

### ENV-U04 작업 예측과 실제 노출의 시간 차이

Project는 route칸마다 rate를 더하고 쾌적 경로 회복을 빼지 않는다. 실제 노출은 경과초와 회복을 쓴다. 시작 예측은 이동속도·장비에 따른 실제 체류시간과 다를 수 있다.

확인할 일: 가변 이동속도·긴 경로·쾌적 경로의 예측 보수성과 실제 작업 거부를 확인한다.

### ENV-U05 작업 속도 두 표와 방어 예외 상충

실제 StatsProjection은 ResolveLegacyWorkSpeed를 사용한다. Decide가 반환하는 새 WorkSpeedMultiplier는 검색한 실행 소비처에서 사용이 확인되지 않았다. IsSafetyException은 Defense를 포함하지만 Decide의 safetyException은 제외한다. 현행 일반업무 분류도 문자열 research/craft/medical/treat만 Precision으로 정한다.

확인할 일: 미사용 예측 속도, Defense·Surgery enum의 실제 진입·강제 작업 재점검 정책을 전수 대조한다.

### ENV-U06 보호함 정원·도달 가능성·보호복 선택

capacity4의 소비가 비Editor 검색에서 확인되지 않았다. HasReachableLocker는 출발/목적지와 맨해튼거리만 검사한다. 후보를 종족 적합성으로 먼저 거르지 않고 best를 골라 TryEquip으로 넘긴다.

확인할 일: 의복/종족/예약/실물 접근 경로 전수 검토와 저장 왕복. 정원4를 실제 수용 규칙으로 단정하지 않는다.

### ENV-U07 장기2~8°C 보존 Query의 라이브 소비 미확인

IsOrganPreservationSafe는 선언·구현·null/query adapter 외 비Editor 사용처가 검색되지 않았다.

확인할 일: 의료/장기 물리 보관 권위를 별도로 대조한다. 함수 존재만으로 적용 완료라고 하지 않는다.

### ENV-U08 저장 재개 환경장과 노출 상태

환경장은 기본 외기값과의 차이가0.05 미만인 셀을 생략하고 목표온도를 저장한다. 틱 accumulator·위상은 저장하지 않는다. 노출값·단계·피해타이머·냉기휴식잠금은 저장하지만 workContext는 다시 설정한다. 열원 source 순서와 날씨 복원의 영향은 미검증이다.

확인할 일: 날씨/시설/캐릭터 교차 복원 순서와 비정수 틱 경계에서 실제 왕복 비교. Domain restore의 thermostat -20~45 범위와 authored범위도 구분한다.

## 검증과 한계

- 이전 직접 근거2987개·산출물18개 hash 변경/오류0.
- 독립 산술15건 통과. C#·Unity 실행 테스트가 아니다.
- 시설11개·14모듈·50필드의 원본/도감 대조이며 종족·의복·날씨·수술·음식 전체 조사는 남아 있다.
- 실제 UI 입력·컴파일·PlayMode·저장 왕복을 수행하지 않았다.
- KB는 stale4건으로 생성행0개를 반환했다. query=EnvironmentalField CharacterEnvironment, areas=code/authority/content, limit8, session41968, exit1. content digest=139a0a989275ecdd5a4a26c10ceb6a1931041c7c928ed0421628faea5cd928c6, knowledge-base digest=ceef8dc8f25f4d327205b15e12346aee0ebc5d6a84aa7eeb1f08af5ce14db0dd. 재생성하지 않고 직접 원문으로 대조했다.
- 완독·부분 읽기 범위와64개 근거 경로/hash는 JSON에 명시했다. 전역 의미 중복 검토는 pending이다.

