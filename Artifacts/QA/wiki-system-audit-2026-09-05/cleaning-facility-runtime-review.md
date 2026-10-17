# 청소 ability 시설 런타임 대조

- 작성 모집단은 H01 변기, H02 화장실칸막이, H03 세면대, H04 목욕통, H05 수건걸이, H06 청소도구함, H07 바닥배수구의 7개이며 모두 `restoredCleanliness=100`을 작성한다.
- `CleaningBuildingAbilityHandler`는 완료한 작업이 `work:clean`일 때만 core handler로 전달한다. 다른 work type 또는 대상 건물이 없으면 효과는 없다.
- `ModularFacilityRuntimeEffects.ApplyCleaning`은 대상 건물의 operational room profile에서 찾은 모든 `BuildableObject` part에 `SetCleanliness(100)`을 호출하고, `work:clean` 완료 활동을 기록한다. 별도 청결도 누적 state나 저장 payload는 만들지 않는다.
- 공개 facility 1057~1063은 이 수치·work gate·같은 방 구성품 전체 적용을 설명하지 않고, 관련 공개 가이드도 시설 청소 효과를 일반적인 작업 언급으로만 다룬다.
