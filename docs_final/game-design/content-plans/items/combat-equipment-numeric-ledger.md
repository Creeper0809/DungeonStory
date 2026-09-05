# 전투 장비 수치 원장

[전투 장비 수치 원장](combat-equipment-numeric-ledger.csv)은 무기 31개, 방어구 21개, 방패 9개의 실제 장비 에셋을 다시 읽어 제작 BOM·kg·단가·제작 WU·내구·손 점유·연구·전투 수치를 고정한다.

장비 에셋의 기본 재질은 `material:iron`처럼 재질 정의 ID로 적혀 있다. 실제 제작은 해당 재질 정의가 가리키는 물리 아이템을 소비한다. 원장은 `CraftMaterialDefinitionSO`의 변환을 적용해 `material:iron → material:iron-ingot`, `material:wood → material:lumber`처럼 실제 kg와 가격을 가진 BOM으로 기록한다. 이 변환 뒤 61개 장비 모두에서 미해결 BOM과 0 이하 BOM 단가가 없다.

`combat_signature`는 장비 종류마다 승패를 가르는 값을 남긴다. 무기는 피해·관통·추적·공격 간격·사거리·장전·오발, 방어구는 레이어와 부위별 참격·관통·둔격 방호, 방패는 세 종류 방호와 전방 차단 확률이다. 이 값과 조우 수치 원장을 결합해 무기·방어구·방패가 같은 조우에서 동시에 최선이 되는지 판정한다.
