# 컨베이어 설명·도감 대조

진행 중인 전수 감사의 부분 보고서다. 스크립트·자산·공개 문서는 수정하지 않았다. 밸런스 영향 없음. 정적 경로 대조이며 실제 게임, UI 입력, 저장 왕복은 실행하지 않았다.

## 모집단과 판정

- 모듈 보유 시설40개, 대응 도감40개, 카탈로그 참조40개다.
- 벨트13개·포트29개·배출구1개, 총43모듈의 작성284필드를 대조했다(195+87+2).
- 도감 facts는 모두 분류·크기뿐이다. 방향 일부는 이름에 표시되므로284개 모두를 독립적인 설명 누락으로 세지 않는다.
- 포트29개의 목적지는 모두 빈 값이다. 입력기9844와 출력기9845는 포트capacity4보다 벨트capacity2가 우선한다.
- 벨트13개 모두 품목·분류·소재 제한이 비어 있고 품질·신선도 필터가 꺼져 있다. 조건부 범위를 활성 효과로 오해하지 않게 한다.
- 상세 작성값·공개 요약·140개 원본 및 공개 파일 SHA-256은 [JSON](conveyor-mechanics-review.json)에 보존한다. 기존 근거2977개를 다시 해시해 변경0을 확인했다.

## 문서에 추가하거나 바로잡을 내용

| 원장 | 내용 | 구분 |
| --- | --- | --- |
| GAP-076 | 자동 반입 조건, 전체 스택 운반, 예약 제외, 경로 선택, 속도와 수용량 | 공통 운송 규칙 누락 |
| GAP-077 | 품목/분류 OR와 작성/현재 필터 AND, 장비·음식 상태 필요 조건, 실제 조작 범위 | 조건 누락 |
| GAP-078 | 정체·교착,30초 대기,4개 배출 정책과 실패 시 화물 유지 | 상태·복구 규칙 누락 |
| GAP-079 | 운송 시설40개의 속도·수용량·포트 역할·배출 설정 | 도감 노출 누락 |
| GAP-080 | 경로 목록/순서를 저장한다는 문장과 실제 재계산 구조의 차이 | 저장 설명 오류 |

세부 요구사항은 [원장](missing-register.md)에 한 번만 적는다. source-coverage.json의 conveyor 절은 production으로 연결되지만 그 문서는 컨베이어 상세 규칙을 설명하지 않는다. 기반 시설 문서가 규칙의 권위를 맡고 생산·재고 문서는 이를 참조하도록 정리할 필요가 있다.

## 시설별 적용 속도·수용량

속도는 현재 구간에서 진행도에 더하는 초당 값이다. 한 틱에는 최대 한 구간을 넘는다. 수용량은 아이템 개수나kg가 아니라 스택 단위의 화물 수다. 아래 수치는 정적 설정과 ResolveCapacity에 따른 값이며40시설의 실제 운송 성공을 의미하지 않는다.

| 시설 ID | 이름 | 속도 | 화물 수용량 | 포트 | 조건 |
| --- | --- | ---: | ---: | --- | --- |
| 1000 | [간이화덕](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1000.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1001 | [고기그릴](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1001.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1002 | [조리손질대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1002.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1019 | [대장작업대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1019.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1049 | [전리품거치대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1049.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1073 | [제분소](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1073.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1074 | [양조장](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1074.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1075 | [제재소](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1075.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1076 | [숯가마](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1076.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1077 | [석재 절단대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1077.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1078 | [광석 선별대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1078.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1079 | [용광로](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1079.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1080 | [제강로](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1080.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1081 | [귀금 세공대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1081.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1082 | [비전 단조대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1082.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1083 | [직조기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1083.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1084 | [무두질대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1084.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1085 | [퇴비장](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1085.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1086 | [증류기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1086.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1087 | [조리대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1087.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1088 | [훈연대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1088.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1089 | [사료 배합대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1089.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1090 | [약제대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1090.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1091 | [연금대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1091.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1092 | [몽직기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1092.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1093 | [대장간](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1093.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 1097 | [폐기 소각로](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1097.json>) | 포트 기본 1 | 4 | 양방향 | 포트 목적지 미지정 |
| 9840 | [컨베이어 우향](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9840.json>) | 1 | 1 | 없음 | 전력 필요 |
| 9841 | [컨베이어 좌향](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9841.json>) | 1 | 1 | 없음 | 전력 필요 |
| 9842 | [컨베이어 상향](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9842.json>) | 1 | 1 | 없음 | 전력 필요 |
| 9843 | [컨베이어 하향](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9843.json>) | 1 | 1 | 없음 | 전력 필요 |
| 9844 | [컨베이어 입력기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9844.json>) | 1 | 2 | 입력 | 전력 필요 |
| 9845 | [컨베이어 출력기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9845.json>) | 1 | 2 | 출력 | 전력 필요 |
| 9846 | [컨베이어 분배기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9846.json>) | 1 | 2 | 없음 | 전력 필요 |
| 9847 | [컨베이어 합류기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9847.json>) | 1 | 2 | 없음 | 전력 필요 |
| 9848 | [컨베이어 필터](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9848.json>) | 1 | 1 | 없음 | 전력 필요 |
| 9849 | [우선순위 게이트](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9849.json>) | 1 | 1 | 없음 | 전력 필요 |
| 9850 | [층간 물류 리프트](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9850.json>) | 0.8 | 2 | 없음 | 전력 필요 |
| 9851 | [오버플로 배출 게이트](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9851.json>) | 1 | 2 | 없음 | 지정 창고 후 바닥 / 30초 |
| 9852 | [고속 컨베이어](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9852.json>) | 2 | 2 | 없음 | 전력 필요 |

## 문서 누락과 분리한 구현 확인 사항

1. SetPortDestination은 비Editor Assets/Scripts 전역 역검색에서 구현·인터페이스 선언 외 호출이 없다. 자동 반입은 목적지를 요구하고29개 작성값은 모두 비어 있다. 사용자에게 목적지를 설정하라고 안내하기 전에 실제 UI 연결을 확인해야 한다.
2. 산업 UI의 배출 정책 버튼은 기존 ReserveWarehouseId를 그대로 넘긴다. 확인한 화면에는 지정 창고 선택기가 없다. 지정 창고가 비어 있는 기본 정책은 바닥 배출로 이어진다.
3. API는 품목·분류·소재 목록과 최대 신선도를 받지만 실제 확인한 UI는 토글, 최소 신선도10%p, 품질 양끝 조작만 제공한다.
4. 넘침 배출구 탐색은 정방향·역방향 연결을 함께 보며 가동·전력·필터를 검사하지 않는다. 처리 상한8은 성공 수가 아니라 후보 수다. 앞선 실패 후보가 뒤의 화물을 지연시키는지는 실제 실행으로 확인해야 한다.
5. 복원한 정지 경과는 연결 투영 초기화로 지워질 수 있고 수동 배출 승인은 저장하지 않는다. 이를 의도된 재개 규칙으로 확정하지 않는다.
6. 우선순위 게이트와 층간 리프트라는 이름에 대응하는 별도 우선 가중치·층간 이동은 확인한 경로 계산에서 찾지 못했다. 전체 관련 기능 대조가 남아 있다.
7. 일반 시설 비용 대조는 별도다. 표본9840은 현재 건설WU883과 도감 요약618이 다르다. 이 보고서의284필드 집계에 비용 비교를 섞지 않았다.

## 근거와 한계

KB query=Conveyor, areas=code/content/authority, limit6. session18555는 exit1, stale4건·반환0행이다. content digest=139a0a989275ecdd5a4a26c10ceb6a1931041c7c928ed0421628faea5cd928c6, KB digest=ceef8dc8f25f4d327205b15e12346aee0ebc5d6a84aa7eeb1f08af5ce14db0dd. 생성물은 재생성하지 않고 현재 원본을 직접 읽었다. 파일별 완독/부분 검토 범위는 JSON의readCoverage에 기록했다. 전체 시스템·도감 감사와 의미 중복 제거는 계속 진행 중이다.

