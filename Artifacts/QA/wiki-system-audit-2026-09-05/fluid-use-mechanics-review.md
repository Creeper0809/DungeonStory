# 생활·공정 급배수와 조합식 도감 대조

전체 감사의 부분 결과다. 시설50개와 조합식355개의 선택 급배수 필드를 전수 대조했으며, 실제 게임 실행·저장 왕복은 수행하지 않았다. 스크립트·자산·공개 위키는 수정하지 않았다.

## 판정 요약

- 시설50개에서244개 선택 필드를 대조했다. 물·폐수를 사용하는31개는168필드이며, 보조시설19개의76필드는 모두0인 중립값이다.
- 조합식355개·4필드1420개를 대조했다. 물을 쓰는24개의 공개 물 수치는 모두 일치한다. 물·폐수 수요가 없는331개를 누락으로 세지 않는다.
- 물 사용 조합식24개 중 폐수 발생17개, 수동 급수 허용16개다. 폐수량·종류와 수동허용은 공개 facts에 없다.
- 생성 KB는 stale539건, 반환0행이다. 현재 작성 자산·C#·위키 JSON을 직접 대조했다. digest와 읽기 범위는 JSON에 보존한다.

## 시설별 작성값

수치는 작성값이다. 생활 시설은 사용 시 생활용 물 배율을 추가로 적용한다. 공정 시설은 적용 업무를 함께 읽어야 한다. 표의0은 해당 기능을 지원하지 않는 값이며, 모든 필드에 현재 효과가 있다고 간주하지 않는다.

| 시설 | 모듈 | 선택 작성값 | 공개 facts |
| --- | --- | --- | --- |
| [샤워 시설](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9823.json>) | BuildingWaterFixtureAbility | cleanWaterPerUse=0.45; wastewaterPerUse=0.45; minimumQuality=0; allowsManualWaterFallback=0; allowsDryFallback=0; manualWasteItemId=(없음) | 분류·크기 |
| [실내 생장 제어기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1627.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0.2; wastewaterPerCycle=0.05; wastewaterComposition=8; allowsManualWaterFallback=0 | 분류·크기 |
| [대장 도구함](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1626.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [마나 응축기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1625.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [무균 약품 보관함](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1624.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [직조 보조 선반](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1623.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [마나 안정기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1622.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [세공 도구함](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1621.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [도가니 선반](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1620.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [정밀 연마기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1619.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [목재 처리조](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1618.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0.15; wastewaterPerCycle=0.1; wastewaterComposition=7; allowsManualWaterFallback=0 | 분류·크기 |
| [연기 포집 후드](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1617.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [영양 배합 저울](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1616.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [치즈 숙성 선반](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1615.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [치즈 응고조](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1614.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=1; allowsManualWaterFallback=0 | 분류·크기 |
| [염장·절임조](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1613.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=4; allowsManualWaterFallback=0 | 분류·크기 |
| [향신료 선반](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1612.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [냉장 준비대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1611.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [세척·전처리 싱크](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1610.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0.25; wastewaterPerCycle=0.25; wastewaterComposition=2; allowsManualWaterFallback=0 | 분류·크기 |
| [전기 오븐](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1609.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [벽돌 오븐](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1608.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [화덕·가마솥](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1607.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [분별 증류탑](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1606.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0.5; wastewaterPerCycle=0.4; wastewaterComposition=7; allowsManualWaterFallback=0 | 분류·크기 |
| [세척·병입대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1605.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0.25; wastewaterPerCycle=0.25; wastewaterComposition=1; allowsManualWaterFallback=0 | 분류·크기 |
| [숙성 오크통](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1604.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [온도 제어 발효조](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1603.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0.1; wastewaterPerCycle=0.1; wastewaterComposition=5; allowsManualWaterFallback=0 | 분류·크기 |
| [수동 발효조](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1602.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [담금·당화조](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1601.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0.25; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=1 | 분류·크기 |
| [미세 체 선반](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1600.json>) | BuildingProductionSupportAbility | cleanWaterPerCycle=0; wastewaterPerCycle=0; wastewaterComposition=0; allowsManualWaterFallback=0 | 분류·크기 |
| [시간 고정실](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8872.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [전신 재생조](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8871.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [룬 동면실](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8870.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [회춘 수혈실](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8869.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [장기 재생 수술실](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8868.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [조리손질대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1002.json>) | BuildingProcessFluidAbility | workTypeIds=work:cook; cleanWaterPerCycle=0.25; wastewaterPerCycle=0.25; wastewaterComposition=1; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [고기그릴](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1001.json>) | BuildingProcessFluidAbility | workTypeIds=work:cook; cleanWaterPerCycle=0.25; wastewaterPerCycle=0.25; wastewaterComposition=1; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [간이화덕](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1000.json>) | BuildingProcessFluidAbility | workTypeIds=work:cook; cleanWaterPerCycle=0.25; wastewaterPerCycle=0.25; wastewaterComposition=1; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [비전 개조대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9512.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [격리 회복 침상](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9511.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [면역 조절기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9510.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [순환 이식대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9509.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [재활 보조대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9507.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [외과 수술대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9503.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [해부대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9502.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [응급 처치대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9501.json>) | BuildingProcessFluidAbility | workTypeIds=work:surgery; cleanWaterPerCycle=0.2; wastewaterPerCycle=0.2; wastewaterComposition=6; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [훈연대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1088.json>) | BuildingProcessFluidAbility | workTypeIds=work:cook; cleanWaterPerCycle=0.25; wastewaterPerCycle=0.25; wastewaterComposition=1; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [조리대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1087.json>) | BuildingProcessFluidAbility | workTypeIds=work:cook; cleanWaterPerCycle=0.25; wastewaterPerCycle=0.25; wastewaterComposition=1; minimumQuality=0; allowsManualWaterFallback=1 | 분류·크기 |
| [목욕통](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1060.json>) | BuildingWaterFixtureAbility | cleanWaterPerUse=1; wastewaterPerUse=1; minimumQuality=0; allowsManualWaterFallback=0; allowsDryFallback=0; manualWasteItemId=(없음) | 분류·크기 |
| [세면대](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1059.json>) | BuildingWaterFixtureAbility | cleanWaterPerUse=0.15; wastewaterPerUse=0.15; minimumQuality=0; allowsManualWaterFallback=1; allowsDryFallback=0; manualWasteItemId=industrial:sludge | 분류·크기 |
| [변기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1057.json>) | BuildingWaterFixtureAbility | cleanWaterPerUse=0.25; wastewaterPerUse=0.25; minimumQuality=0; allowsManualWaterFallback=1; allowsDryFallback=1; manualWasteItemId=resource:manure | 분류·크기 |

## 물을 사용하는 조합식 24개

물 수치는 이미 공개되어 있다. 폐수와 수동 허용은 추가할 정보다. 전체355개의 중립값까지 포함한 대조는 JSON의 recipeRows에 있다. 종류는 코드 enum 이름을 그대로 기록했으며 종류별 질병 효과나 처리 효율을 뜻하지 않는다.

| 조합식 | 물(원본=도감) | 폐수 | 폐수 종류 | 수동 급수 |
| --- | ---: | ---: | --- | --- |
| [월야 비건 만찬](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-lavish-vegan.json>) | 0.3 | 0.25 | FoodProcessWashwater | 불가 |
| [핏빛 호화식](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-lavish-meat.json>) | 0.3 | 0.25 | FoodProcessWashwater | 불가 |
| [약초 찜질약](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-herbal-poultice.json>) | 0.5 | 0 | None | 허용 작성 |
| [황혼곡죽](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-grain-porridge.json>) | 3.4 | 0.2 | FoodProcessWashwater | 허용 작성 |
| [정원 요리](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-garden-meal.json>) | 1.4 | 0.4 | FoodProcessWashwater | 허용 작성 |
| [달걀전](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-egg-pancake.json>) | 0.2 | 0.15 | FoodProcessWashwater | 불가 |
| [잿불뿌리 스튜](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-root-stew.json>) | 1.3 | 0.2 | FoodProcessWashwater | 허용 작성 |
| [고기구이](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-roasted-meat.json>) | 0.3 | 0 | None | 허용 작성 |
| [동굴버섯국](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-mushroom-soup.json>) | 1.9 | 0.2 | FoodProcessWashwater | 허용 작성 |
| [월화차](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-moonflower-tea.json>) | 1.1 | 0.2 | FoodProcessWashwater | 허용 작성 |
| [멧돼지 스튜](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-boar-stew.json>) | 0.25 | 0.2 | FoodProcessWashwater | 불가 |
| [채소 세척](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-washed-vegetable.json>) | 0.25 | 0.25 | FoodProcessWashwater | 불가 |
| [사일리지 발효](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-silage.json>) | 0.2 | 0 | None | 허용 작성 |
| [염장육 스튜](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-salted-meat-stew.json>) | 1.2 | 0 | None | 허용 작성 |
| [고기 염지](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-salted-meat.json>) | 0.1 | 0.1 | FoodProcessWashwater | 허용 작성 |
| [배급식 혼합](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-ration-mixture.json>) | 0.1 | 0.1 | FoodProcessWashwater | 허용 작성 |
| [맥아죽](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-malt-porridge.json>) | 1.5 | 0 | None | 불가 |
| [밤포도 착즙](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-grape-juice.json>) | 0.1 | 0.05 | FoodProcessWashwater | 허용 작성 |
| [발효 식초](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-fermented-vinegar.json>) | 0.7 | 0 | None | 불가 |
| [발효 절임](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-fermented-pickle.json>) | 1.2 | 2 | Brine | 허용 작성 |
| [증류용 발효액](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-fermented-liquor.json>) | 0.7 | 0 | None | 불가 |
| [반죽 치대기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-dough.json>) | 0.6 | 0.2 | FoodProcessWashwater | 허용 작성 |
| [응유 만들기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-curd.json>) | 0.4 | 4.2 | Whey | 허용 작성 |
| [채소 염지](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-brined-vegetable.json>) | 1.4 | 2 | Brine | 허용 작성 |

## 추가할 설명

### GAP-071 생활 시설의 급수·배수량과 단수 시 이용·폐기물 처리 규칙 누락

시설 사용 시작에 물을 소비하고 완료 때 폐수를 처리한다. 작성 기준은 샤워0.45/0.45, 목욕통1/1, 세면대0.15/0.15, 변기0.25/0.25(물/폐수)이며 모두 깨끗한 물을 요구한다. 생활용 물 배율은 물과 폐수 양을 함께 보정하므로 작성값과 실제 사용량을 구분한다. 샤워·목욕통은 관로 급수가 필요하고, 세면대·변기는 지정 버퍼의 깨끗한 물로 수동 공급할 수 있다. 변기만 물 없이 이용하는 분기가 있으며 완료 시 하수 오물8, 오염값0.45를 만든다. 정상 이용의 폐수가 배수되지 않으면 세면대는 슬러지, 변기는 분뇨를 max(1,ceil(폐수량))개 만들고, 해당 대체물이 없는 시설은 폐수망에 배출을 시도해 역류 경로로 이어진다. 이용 중 취소의 환급·완료 보장은 별도 미검증이며 코드 주석만으로 원자성을 보장한다고 쓰지 않는다.

### GAP-072 수동 급수의 시설별 잔량·물통 반올림·운반 목적지 조건 누락

수동 급수에 새로 필요한 물 아이템 수는 max(0,ceil(요청량-시설 잔량-0.0001))이다. 지정 시설 버퍼에 있는 예약되지 않은 resource:clean-water 스택을 사용하며 여러 스택에서 필요한 정수 수량을 모을 수 있다. 사용 뒤 잔량은 max(0,이전 잔량+옮긴 물 수-요청량)으로 같은 시설 노드에 남는다. 예를 들어 잔량0에서0.25를 쓰면 물1개를 소비하고0.75가 남는다. 다른 시설의 잔량이나 창고에 있기만 한 물을 즉시 사용할 수 없다. 부족분은 해당 목적지로 운반을 요청한다. 잔량과 진행 중 거래의 저장 필드는 확인했으나 저장 왕복 실행은 미검증이다. 보조시설 목적지 누락과 소수 잔량을 무시하는 공정 사전 검사 후보가 있으므로 모든 수동허용 필드가 실제 실행 가능하다고 확대 해석하지 않는다.

### GAP-073 생산의 시설·조합식·보조 설비 급배수 합산과 의료 경로 차이 누락

한 생산 사이클의 물·폐수는 해당 업무에 적용되는 시설 모듈, 조합식, 실제로 연결해 사용하는 서로 다른 보조시설의 양을 각각 합산한다. 같은 보조시설이 여러 요구 태그를 제공해도 한 번만 센다. 시설·조합식 가운데 물을 요구하는 모든 항목이 수동 공급을 허용해야 그 시설의 합산 물을 수동으로 댈 수 있고, 보조시설은 자기 허용값을 별도로 따른다. 생산은 각 소비 지점의 깨끗한 물과 폐수 저장 여유가 필요하며 수동 급수 허용이 배수 면제를 뜻하지 않는다. 응유 만들기를 조리 시설(0.25/0.25)과 치즈 응고조(0.2/0.2)로 수행하면 조합식0.4/4.2를 더해 총 물0.85, 폐수4.65가 된다. 도감의0.4는 조합식 자체 값이다. 주문은 사이클별 공정 소비 완료를 기록해 정상 재시도 때 다시 소비하지 않는다. 수술이 호출하는 시설 모듈 전용 경로는 별도로, 작성된 수동허용이 있으면 배수 불가 시 ceil(폐수량), 최소1개의 슬러지를 만들 수 있다. 생산 배치와 같은 규칙으로 설명하면 안 된다. 모든 의료시설의 실제 수술 가용성과 실패 중간 단계의 원자성은 별도 검증 대상이다.

### GAP-074 생활·공정·생산 보조시설의 급배수 설정과 역할별 도감 누락

fluid-use-mechanics-review.json/MD의 시설별 표를 기준으로 생활 시설의 사용당 물·폐수·최소 수질·수동/재래식 허용·대체 폐기물, 공정 시설의 적용 업무·사이클당 물·폐수·성분·최소 수질·수동허용, 보조시설의 물·폐수·성분·수동허용을 구분해 표시한다. 물이나 폐수를 사용하는31개에 선택 필드168개가 있고, 나머지 보조시설19개의76개 값은 모두0이므로 미지원 확인으로 분리한다. 공정18개는 조리5개(각0.25/0.25), 수술13개(각0.2/0.2)다. 양수·저장·정수·물통7개/34필드의 GAP-069와 다른 모집단이다. 카탈로그 등록을 실제 모든 시설의 건설·수술 실행 검증으로 세지 않는다.

### GAP-075 조합식의 폐수량·폐수 종류·수동 공급 조건 누락과 물 수치의 범위 불명확

물 사용 조합식24개 중 폐수가 양수인17개의 배출량과 종류,24개 전체의 수동 공급 허용/불가를 해당 조합식에 표시한다.24개 중 수동허용16개·불가8개다. 물 표시24개는 이미 정확하므로 누락으로 세지 않으며, 조합식 자체의 추가 물이고 시설·연결 보조시설 비용은 별도라는 범위를 밝힌다. 물·폐수 수요가 모두0인331개는 중립값 확인으로 기록한다. 응유4.2의 유청, 채소 염지·발효 절임 각2의 염수처럼 입력 물보다 큰 폐수도 식재료에서 나온 부산물이므로 임의로 물 입력량에 맞춰 고치지 않는다. 생산 질량 변환은 작성 유체1단위=500g이며 근거 없이1L로 표시하지 않는다. 폐수 종류는 현재 구성·질량 기록이며 종류별 추가 질병 확률이나 전용 처리 효율은 별도 소비처가 확인되기 전까지 만들지 않는다.

## 구현 확인이 먼저 필요한 부분

- 담금·당화조의 수동허용은 보조시설 필드에만 있다. 수동 목적지를 만드는 코드에는 보조시설 전용 분기가 없으므로 실제 수동 공급 가능성을 확인해야 한다.
- 수동 입력 버퍼는 시설 자체의 물 요구량을 올림한 수량으로 제한한다. 조합식까지 더한 요구량으로 확장하지 않는다. 조리 시설의 용량1과 곡물죽 합산 요청3.65(빈 잔량에서 물4개)가 맞지 않는 후보가 있다.
- 공정 사전 검사는 시설 잔량을 빼지 않고 한 스택에서 필요한 물을 찾는다. 실제 소비는 잔량과 여러 스택을 사용할 수 있다. 두 경로의 준비 판정이 일치하는지 실행 확인이 필요하다.
- 수동 거래 준비 뒤 다른 소비 지점이나 배수 검사가 실패하는 경우의 재시도·저장 원자성은 미검증이다.
- 이전 근거 이후 Facility.cs와 OperatingDaySettlement.cs가 바뀌었다. 현재 시설 급배수 호출 지점은 재독했지만 두 파일의 모든 이전 감사 항목을 재인증한 것은 아니다.

이 후보들은 설명 누락과 분리한다. 코드 수정, Unity 컴파일·UI 조작·저장 왕복은 이번 조사에서 수행하지 않았다. 밸런스 영향 없음(감사 기록만 변경).
