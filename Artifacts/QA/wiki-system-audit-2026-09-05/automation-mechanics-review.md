# 자동화·정비 설명 대조

상태: 부분 시스템 감사. 전체 위키 감사 완료가 아니며 Unity 실행·저장 왕복은 하지 않았다.

## 확인 결과

- 카탈로그에 연결된 자동화 작성 시설 27개와 대응 도감 27개를 대조했다. 7필드씩 189개가 도감에서 빠져 있다.
- 도감 facts는 전부 분류·크기이며 본문 요약은 역할·건설 비용만 설명한다. 가이드가 위임한 시설별 자동화 수치를 여기서 찾을 수 없다.
- 27개 모두 최대 모드 자동, 보조 전력 2, 자동 전력 5, 보조 배율 1.35, 자동 작업량 1WU/시뮬레이션초, 정비 소모 1/게임시간, 품질 상한 작성값 0.75다.
- 품질 상한은 실제 소비처가 미확인이다. 0.50~0.90은 Editor 검사 범위이며 현행 시설별 실제 적용값으로 설명할 근거가 아니다.
- 모듈 보유·카탈로그 연결은 모든 공정의 무인 생산 성공을 증명하지 않는다. 자동 생산에는 모드와 작업 칸, 주문·재료·출력 조건이 추가로 필요하다.

## 누락된 공통 규칙

### GAP-052 자동화 모드별 작업 권한과 전환·생산 조건 누락

수동·전동 보조는 주민이 작업하고 자동은 자동 실행기가 작업한다. 자동 모드 전환은 시설 작업 예약, 배정 작업자, 주문의 작업자 예약 중 하나라도 있으면 거부되며 시설의 최대 지원 모드를 넘을 수 없다. 자동 모드에서는 수동 생산을 허용하지 않으며, 자동 실행에는 모드 배타형 생산 작업대와 자동 작업 칸이 필요하다. 자동화 모듈이 있다는 사실만으로 모든 시설·장비 제작 공정이 무인 생산되는 것은 아니다. 자동 실행기는 작업자가 예약하지 않은 준비됨/작업 중 생산 주문을 찾아 재료 투입과 출력 공간을 검사한다. 정전, 고장 100, 주문 없음, 재료 또는 출력 문제로 멈추는 조건을 구분한다. 현재 UI의 산업 화면에서 모드 변경을 누르면 수동→전동 보조→자동→수동 순으로 요청한다. 모드 변경이 이미 진행한 주문과 정비 상태를 초기화하지 않는다는 점도 설명해야 한다. 모듈 27개의 작성·도감 목록은 GAP-055, 정비·속도 공식은 GAP-053/054를 참조한다.

### GAP-053 자동화의 정비·고장·작업 속도 공식과 대기 중 소모 누락

새 자동화 상태는 정비 M=100, 고장 F=0, 수동 모드로 시작한다. 정비 배율은 M≥60이면 1, 그 미만이면 0.45+0.55×M/60이며 고장 배율은 1-0.65×F/100이다. 두 값의 곱을 0.1~1로 제한한 조건 배율 C를 사용한다. 전동 보조의 속도 배율은 전력 공급 및 F<100일 때 작성 보조 배율×C이며, 보조 조건이 성립하지 않으면 이 자동화 보정은 1이다. 다른 작업 중단 조건까지 무시한다는 뜻은 아니다. 자동 실행기의 제출 작업량은 작성 초당WU×C×경과 시뮬레이션초이고 실제 승인량은 주문 잔여량·공정 보정 등에 따른다. 현재 보조 1.35/자동 1WU인 설비에서 M=30,F=20이면 C=0.63075, 보조 0.8515125배, 자동 제출량 0.63075WU/초다. 비수동·전력 공급 상태에서 M은 시간당 정비 소모×clamp(이정표 정비 배율,0.1,1)×경과초/7.5만큼 줄어든다. 게임 하루는 180초·24시간이므로 이 게임 1시간과 현실 1시간을 구분한다. 감소 후 M≤25이면 F에 (25-M)×0.006×경과초를 더하고 0~100으로 제한한다. F=100이면 자동 작업이 정지한다. 소모는 주문 검사보다 먼저이므로 주문 없음·재료 부족·출력 막힘 중에도 발생한다. 수동 또는 전력 미공급 때는 이 소모·고장 증가가 멈춘다. 모드/M/F는 저장 대상이며 불러오기를 정비 초기화로 설명하면 안 된다.

### GAP-054 자동화 정비의 시작·종료 기준과 수리 WU 효과 누락

자동화 정비는 수동이 아닌 모드에서 정비 M<85 또는 고장 F>0.01이면 수리 후보가 된다. 한 번의 계산에서 남은 정비WU는 max(max(0,85-M),max(0,2F))이며 작업자의 실제 수리WU를 그 잔여량까지만 승인한다. 승인 1WU마다 M+1, F-0.5를 적용하고 각각 0~100으로 제한한다. 작업은 M≥85이고 F≤0.01이면 끝나므로 항상 M=100까지 정비하는 것은 아니다. 예를 들어 M=40,F=10에서는 동시 소모를 제외하면 45WU가 필요하다. 이 자동화 정비 분기에는 자재 소비가 없고 노동을 소비한다. 장비 수리나 다른 구조 수리의 자재 규칙으로 확대하지 않는다. 계속 가동하면 정비 중에도 자동화 소모가 생길 수 있다. 같은 대상에 여러 수리 종류가 있으면 현재 실행 순서는 장비 수리→자동화 정비→구조 수리→방어 시설 정비→일반 손상 처리다. 수동 전환은 M/F를 지우지 않지만 자동화 정비 후보에서는 제외된다. 정비 우선도는 c=max((85-M)/85,F/100)를 0~1로 제한해 0.35+0.65c로 계산하고 다른 수리 긴급도와 큰 값을 사용한다.

### GAP-056 자동 품질 상한의 설계 범위를 현재 적용 수치로 안내

자동 품질 상한의 설계 허용 범위(0.50~0.90), 현재 작성 필드(전 시설 0.75), 실제 생산 품질에 적용되는 규칙을 구분해야 한다. automaticQualityCap의 현재 C# 전수 역검색에서는 선언, 자산 Builder와 Editor 범위 검사만 확인되고 비Editor 소비는 확인되지 않았다. AutomationRuntime은 초당 작업량을 생산 명령에 전달하지만 품질 상한을 전달하지 않는다. 장비 제작의 별도 실행 경로는 실제 작업자와 품질 성능을 요구하므로 이 필드로 모든 무인 장비 품질이 75%에 제한된다고 설명하면 안 된다. 누락 수치를 문서에 채워 넣기 전에 실제 품질 소비처와 적용 단위를 확인하거나 설계·구현 간 불일치로 관리해야 한다. 현재 감사는 스크립트를 수정하지 않으며 구현 미확인 후보를 별도 기록한다.

## 시설별 근거

아래 값은 현재 27개가 모두 같은 것으로 확인했다. 값이 같다는 이유로 고유 시설 경로를 생략하지 않는다. 원본 .meta GUID는 카탈로그 참조와 함께 JSON에 보존했다.

| ID | 시설 | 작성 자산 | 도감 | 자동화 값 노출 |
| --- | --- | --- | --- | --- |
| 1019 | 대장작업대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/S08_대장작업대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1019.json>) | 0/7 |
| 1097 | 폐기 소각로 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P25_폐기소각로.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1097.json>) | 0/7 |
| 1093 | 대장간 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P21_대장간.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1093.json>) | 0/7 |
| 1092 | 몽직기 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P20_몽직기.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1092.json>) | 0/7 |
| 1091 | 연금대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P19_연금대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1091.json>) | 0/7 |
| 1090 | 약제대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P18_약제대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1090.json>) | 0/7 |
| 1089 | 사료 배합대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P17_사료배합대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1089.json>) | 0/7 |
| 1088 | 훈연대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P16_훈연대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1088.json>) | 0/7 |
| 1087 | 조리대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P15_조리대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1087.json>) | 0/7 |
| 1086 | 증류기 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P14_증류기.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1086.json>) | 0/7 |
| 1085 | 퇴비장 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P13_퇴비장.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1085.json>) | 0/7 |
| 1084 | 무두질대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P12_무두질대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1084.json>) | 0/7 |
| 1083 | 직조기 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P11_직조기.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1083.json>) | 0/7 |
| 1082 | 비전 단조대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P10_비전단조대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1082.json>) | 0/7 |
| 1081 | 귀금 세공대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P09_귀금세공대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1081.json>) | 0/7 |
| 1080 | 제강로 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P08_제강로.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1080.json>) | 0/7 |
| 1079 | 용광로 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P07_용광로.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1079.json>) | 0/7 |
| 1078 | 광석 선별대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P06_광석선별대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1078.json>) | 0/7 |
| 1077 | 석재 절단대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P05_석재절단대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1077.json>) | 0/7 |
| 1076 | 숯가마 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P04_숯가마.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1076.json>) | 0/7 |
| 1075 | 제재소 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P03_제재소.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1075.json>) | 0/7 |
| 1074 | 양조장 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P02_양조장.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1074.json>) | 0/7 |
| 1073 | 제분소 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P01_제분소.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1073.json>) | 0/7 |
| 1049 | 전리품거치대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/G06_전리품거치대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1049.json>) | 0/7 |
| 1002 | 조리손질대 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D03_조리손질대.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1002.json>) | 0/7 |
| 1001 | 고기그릴 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D02_고기그릴.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1001.json>) | 0/7 |
| 1000 | 간이화덕 | [원본](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D01_간이화덕.asset>) | [도감](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1000.json>) | 0/7 |

## 범위와 남은 확인

전력망 전체·컨베이어·유체·환경·구조 내구도의 모든 규칙은 아직 대조하지 않았다. 자동화 UI의 작업량 단위와 중단 사유 표시, 품질 상한 소비처는 implementation-uncertainties.md에 분리했다. 밸런스 영향 없음: 이번 변경은 감사 기록뿐이다.
