# 급수·폐수·물통 시설과 배관 규칙 대조

진행 중인 전체 감사의 부분 결과다. 선택한 4종 모듈은 시설7개·작성 필드34개이며 생활/공정 급배수 전체는 아직 포함하지 않았다. 스크립트·자산·공개 위키 수정 없음.

## 시설별 작성값

| 시설 | 모듈 | 작성값 | 공개 facts |
| --- | --- | --- | --- |
| [전동 양수 펌프](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9816.json>) | BuildingWaterProducerAbility | quality=0; productionPerSecond=0.75; requiresPower=1 | 분류·크기 |
| [상수 탱크](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9817.json>) | BuildingWaterStorageAbility | channels=2; cleanWaterCapacity=120; wastewaterCapacity=0 | 분류·크기 |
| [오수 탱크](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9818.json>) | BuildingWaterStorageAbility | channels=4; cleanWaterCapacity=0; wastewaterCapacity=140 | 분류·크기 |
| [물통 충전소](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9819.json>) | BuildingWaterContainerTransferAbility | waterPerBatch=1; secondsPerBatch=4; bottleTargetStock=10; requiresPower=1 | 분류·크기 |
| [오수 침전조](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9820.json>) | BuildingWastewaterProcessorAbility | wastewaterInput=10; waterOutput=6; outputQuality=1; requiresPower=0; sludgeItemId=industrial:sludge; sludgeAmount=1; secondsPerBatch=14 | 분류·크기 |
| [소독 정수기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9821.json>) | BuildingWastewaterProcessorAbility | wastewaterInput=10; waterOutput=8.5; outputQuality=0; requiresPower=1; sludgeItemId=industrial:sludge; sludgeAmount=1; secondsPerBatch=8 | 분류·크기 |
| [룬 정화 시설](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9822.json>) | BuildingWastewaterProcessorAbility | wastewaterInput=10; waterOutput=9; outputQuality=0; requiresPower=1; sludgeItemId=industrial:sludge; sludgeAmount=1; secondsPerBatch=8 | 분류·크기 |

## 공통 규칙과 추가할 설명

### GAP-066 급수망의 수질별 사용 순서·공유 저장 공간·사용 실패 조건 누락

급수망은 깨끗한 물(Clean), 비음용수(Unsafe), 오염수(Foul)를 별도 재고로 보관하며 세 양의 합이 상수 저장 용량을 사용한다. 최저수질 Clean은 Clean만, Unsafe는 Unsafe→Clean, Foul은 Foul→Unsafe→Clean 순서로 찾는다. 한 번 요청한 양은 그중 한 수질의 재고만으로 충족해야 하며 비교 허용오차는0.0001이다. 비음용수3와 깨끗한 물2가 있어도5를 한 번 요청하면 실패한다. 상수 채널 이름이 CleanWater라는 이유로 그 망의 모든 물이 음용수라고 설명하지 않는다. 수질별 재고·용량은 산업 패널에서 확인하고, 생활 시설의 실제 최소 수질은 각 시설 작성값을 참조한다. 수질별 질병 효과는 질병/욕구 권위 문서와 연결하고 여기서 복제하지 않는다.

### GAP-067 급수 생산·폐수 처리의 처리량·속도 보정·정지 조건 누락

유체는0.5시뮬레이션초 이상 누적한 경과시간으로 생산→물통 이송→폐수 처리→누수·역류를 평가한다. 가동배율=clamp01(1-막힘/100-clamp01(누수/200))이며 생산량=작성 초당량×가동배율×경과초다. 처리기는 상수·하수 양쪽 망, 필요시 전력, 한 배치 폐수와 출력 저장 여유가 모두 있어야 진행시간을 쌓는다. 진행시간은 경과초×가동배율로 늘고 작성 처리시간(최소0.1초)마다 폐수를 소비해 지정 수질의 물과 슬러지를 만든다. 전동 양수 펌프는 깨끗한 물0.75/초이며, 침전조는 폐수10→비음용수6·슬러지1/14초(무전력), 소독 정수기는10→깨끗한 물8.5·슬러지1/8초, 룬 정화 시설은10→깨끗한 물9·슬러지1/8초(둘 다 전력 필요)다. 저장이 없으면 생산물을 보관할 수 없고, 폐수량 또는 출력 공간 부족 시 처리 진행이 멈춘다. 수원 고갈/유속이나 압력 제한은 이 경로에서 확인하지 않았으므로 추가 조건으로 단정하지 않는다.

### GAP-068 하수 역류·막힘·누수와 배관 업무의 실제 수리 공식 누락

배관 전용 실행기는 막힘 또는 누수>0.01인 시설을 수리 대상으로 삼는다. 긴급도=clamp01(max(막힘,누수)/100), 필요 작업량=8+막힘×0.25+누수×0.3 WU이며 완료하면 해당 문제값을0으로 만든다. 관로 건설은 이 전용 실행기의 작업이 아니므로 설치와 유지보수 설명을 구분한다. 누수 손실=min(노드의 세 수질 합,누수×0.001×경과초×계량보정)이고 가동 중 유량 계량 기능이 하나라도 있으면 계량보정0.85, 그 외1이다. 오염수→비음용수→깨끗한 물 순으로 빠지고 한 평가의 누출량>0.02면 하수 오물이 생긴다. 하수망이 용량-0.001 이상 차면5초마다 그 망 첫 노드에 역류1을 발생시킨다. 배출 실패·넘침도 초과량만큼 역류하며 막힘은 max(1,역류량×2)만큼 늘어 최대100이다. 역류는 주변 오물을 만들지만 이 함수는 폐수 탱크를 비우지 않으므로 수리만 반복하면 재발할 수 있다. 누수의 자연 발생 원인은 현재 쓰기 경로에서 확인하지 못했으므로 임의 확률을 쓰지 않는다. 패널의 막힘·누수는 망 노드의 산술평균이며 최악 시설값이 아니다.

### GAP-069 급수·저장·정수·물통 시설7개의 도감 설정34개와 역할 설명 누락

fluid-mechanics-review.json/MD의7개 시설별34개 작성 필드를 표시 보완 기준으로 사용한다. 펌프의 수질·생산량·전력, 탱크의 채널·용량, 정수기의 배치 입력/출력·수질·시간·전력·슬러지ID/수량, 물통 충전소의 배치 물·시간·목표 재고·전력이다. 상수 탱크의 세 수질 합산 용량은120, 오수 탱크의 폐수 용량은140이다. 반대 채널의0 용량은 미지원으로 설명하고 추가 저장 공간처럼 세지 않는다. 물통 충전소는 양방향 물통 이송 기능을 요약에 넣고 물·슬러지 아이템으로 연결한다. 이34개는 선택한4종 모듈만의 전수 필드이며 생활 급배수·공정 급배수 시설과 전력·BOM의 전체 대조를 뜻하지 않는다.

### GAP-070 물통 충전소의 양방향 모드·공용 재고 목표·중단 조건 누락

산업 패널의 물통 충전소 모드는 정지→망에서 병입→물통으로 급수→정지 순서로 바뀐다. 새 모드로 바꾸면 진행시간과 대기 사유를 초기화한다. 현재 작성값은4초당 물1, 병입 목표 재고10, 전력 필요다. 병입은 망의 깨끗한 물을 소비해 resource:clean-water 실물1개를 바닥에 만들며, 목표10은 해당 충전소의 출력만이 아니라 전체 스택의 같은 아이템 수량 합으로 판정한다. 급수는 지정 시설 버퍼의 예약되지 않은 깨끗한 물 아이템을 소비하고 망의 저장 여유가 있어야 물을 추가한다. 원료가 없으면 운반 요청을 내고 대기한다. 정전 시 진척이 멈추고, 실패 시 누적 진척은 최대 한 배치분으로 제한된다. 성공한 이송은 틱당 한 배치를 처리하며 같은 모드 재선택/전환으로 진척을 재사용하지 않는다. 물통 물리 거래의 저장 중단·재개는 추가 검증 대상이며 실제 왕복 완료로 보고하지 않는다.

## 검토 한계

- 누수값을 자연적으로 올리는 비Editor 쓰기 경로는 확인하지 못함; 복원과0초기화/복구만 발견
- 역류는 최초 노드의 막힘과오물만 늘리고 가득찬 하수재고를 줄이지 않음
- 폐수처리의 슬러지 생성 실패 반환값은 무시됨; 실제실행/의도미검증
- 물통 병입의 RoundToInt는 현재waterPerBatch=1에서만 대조; 다른작성수치의질량/양보존 미검증
- 생활급배수와 공정급배수의 수동대체·폐기물 분기는 추가도감/실행/저장검토 필요

후속 [생활·공정 급배수 대조](fluid-use-mechanics-review.md)에서 FluidNetworkRuntime의 남은 구간을 읽어 1~2153행 대조를 마쳤고, 공정 실행기와 시설50개·조합식355개의 선택 급배수 필드를 조사했다. 실제 시설 실행, 수동 거래의 실패·저장 왕복은 미검증이다. 설계 의도와 실행 검증이 없는 후보를 실제 플레이 규칙으로 단정하지 않는다.
