# 방어 시설 런타임 대조

- 작성 `BuildingDefenseAbility` 모집단은 23개다. `defense:detection/control/supply/maintenance/barrier/launcher/guard-post/elemental/toxin/scatter-trap/blast-trap/general` family와 별도의 range·star·trigger·effect·보급·전력·jam/misfire 값을 작성한다.
- 공개 facility 파일은 16개뿐이고, 존재하는 파일도 family·range·trigger·effect·supply·power·cooldown을 표시하지 않는다. 나머지 7개 작성 시설은 공개 entity가 없다.
- 발동 전 runtime은 enabled concept·target·timing·range·전력·cooldown·arming policy와 물리 보급을 확인한다. physical supply는 per-activation 수량을 쓸 수 있는 capacity로 물리 배송/commit·복구를 거쳐야 한다.
- 발동은 supply를 줄이고 condition을 낮추며 jam 또는 half-effect misfire를 판정한다. 정상/반발동 뒤 cooldown을 설정하고 effect asset을 target status/damage/delay에 적용한다. facility state와 진행 중 물리 commit은 save/restore 후보 검증을 거친다.
