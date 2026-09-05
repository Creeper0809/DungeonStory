# 종족 환경·의복·원단 설명 대조

상태: 부분 대조 완료. 전체 전수 감사는 진행 중이다. 스크립트·자산·위키 본문은 수정하지 않았다. 밸런스 영향 없음.

종족10종의 환경130필드, 작업복4종의 보호24필드, 의복56종과 원단12종의 작성값을 대응 도감82개와 대조했다. 루트 카탈로그 연결은82/82다. 침대2종의 방한 보정도 확인했다. 기존 일반 노출 공식과 자동착용 조건은 GAP-083/085를 참조한다.

## 종족별 온도

단위°C. 아래 안전 범위도 쾌적 범위를 벗어나면 노출이 누적된다. 치명 경계는 포함하며 방한복으로 이동하지 않는다.

|종족|쾌적|안전|치명 최저/최고|
|---|---|---|---|
|뱀파이어|8 ~ 22|0 ~ 34|-10 / 42|
|슬라임|16 ~ 24|5 ~ 34|0 / 40|
|오크|12 ~ 30|-5 ~ 42|-15 / 50|
|균사인|8 ~ 22|0 ~ 32|-8 / 40|
|코볼트|11 ~ 28|-2 ~ 40|-10 / 48|
|하피|7 ~ 25|-5 ~ 36|-15 / 44|
|골렘|-5 ~ 35|-20 ~ 50|-35 / 65|
|데몬|20 ~ 34|10 ~ 46|-2 / 56|
|수인|10 ~ 29|-4 ~ 40|-12 / 48|
|원정자|15 ~ 27|0 ~ 40|-10 / 48|

## 환경 작업복

|작업복|쾌적최저 보정|안전최저 보정|냉기노출 배율|
|---|---:|---:|---:|
|보온 점액 패드|-4|-4|0.6|
|룬 방한복|-10|-10|0.2|
|운반 멜빵|0|0|1|
|방한 작업복|-8|-8|0.35|

최고온도 오프셋은 모두0, 열기노출 배율은 모두1이다. 연구·아이템 관계8개는 이미 공개되어 있다. 데몬은 방한 작업복을 입어도 쾌적최저12°C, 룬 방한복은10°C이므로 8°C/2°C에서 무기한 무노출 근무를 보장하지 않는다.

## 원단 비교

아래 보온·무게배율·내구는 현재 원단 선택 정책이 읽는 값이다. 완성 의복의 실제 보호 효과·최종 무게로 등치하지 않는다. 나머지 작성값과56의복의 체형·겹·부착점은 JSON 전수표에 보존한다.

|원단|보온|선택용 무게배율|내구|
|---|---:|---:|---:|
|포자 삼베|0.35|1.05|78|
|그늘천|0.45|1|60|
|룬가죽|0.72|1.38|125|
|습지 캔버스|0.38|1.35|92|
|가죽|0.52|1.45|105|
|서리 양모직|0.92|1.28|84|
|서리 린넨|0.62|0.82|65|
|잿불 면직물|0.25|0.88|58|
|몽직물|0.58|0.62|88|
|심층 염소모직|0.78|1.3|96|
|일반 모직|0.75|1.2|70|
|동굴 비단|0.48|0.55|74|

## 누락·정정 목록

### GAP-094 종족10종의 쾌적·안전·치명 온도60개와 범위 의미 누락

- 보완할 문서: species-culture-and-life / 종족 도감
- 현재 문서: 종족 가이드는 기후 적응을 언급할 뿐 개별 온도 구간이 없다. 공개 종족10개 도감 facts는 효과 개수 또는 빈 배열이며 온도6필드가 없다.
- 추가·정정할 내용: 종족10종의 쾌적 최저/최고, 안전 최저/최고, 치명 최저/최고를 apparel-environment-review.json의 전수표로 보완한다. 예: 슬라임16~24/5~34/0~40°C, 오크12~30/-5~42/-15~50°C, 뱀파이어8~22/0~34/-10~42°C, 데몬20~34/10~46/-2~56°C, 골렘-5~35/-20~50/-35~65°C다. 안전 범위는 노출이 없는 범위가 아니며 쾌적 밖에서는 누적된다. 치명 최저 이하·최고 이상에는 직접 환경 피해가 생긴다. 원정자도 작성 종족10종에 포함하되 아군 선택 가능 종족으로 오해시키지 않는다. 일반 노출 공식은 GAP-083의 환경 권위 문서로 연결하고 각 종족의 수치는 도감 한 곳에 둔다.
- 심볼: SpeciesEnvironmentProfile.ToThermalProfile / ResourceCharacterSpeciesCatalog.GetRequiredThermalProfile / CharacterEnvironmentRules.CalculateTemperatureRates

### GAP-095 특성·의복·침구의 온도 보호 합산과 치명선 제한 공식 누락

- 보완할 문서: weather-seasons-and-environment
- 현재 문서: 환경 문서는 의복과 휴식 공간을 준비하라고만 안내한다. 종족의 온도 경계에 보호값이 어떻게 더해지는지, 노출 배율과 치명선의 관계는 설명하지 않는다.
- 추가·정정할 내용: 온도 보호 오프셋은 특성·환경 작업복·휴식시설·공용 효과에서 합산하고 냉기/열기 노출 배율은 곱한다. 최종 노출 배율은0.05~2로 제한한다. 보정 안전최저=max(치명최저+2,기본안전최저+안전최저오프셋), 안전최고=min(치명최고-2,기본안전최고+안전최고오프셋)이다. 쾌적최저=clamp(기본쾌적최저+오프셋,보정안전최저,치명최고-2), 쾌적최고=clamp(기본쾌적최고+오프셋,치명최저+2,보정안전최고)다. 치명 온도 자체와 그 경계의 직접 피해는 방한 배율로 면제되지 않는다. 실제 현재 환경 보호 조회는 장착 목록에서 환경 작업복으로 해석되는 첫 항목 하나를 사용한다. 여러 의복의 소재 성능을 전부 합산한다고 설명해서는 안 된다. 선택 순서·겹침 및 소재 투영 연결 문제는 APPAREL-U01/03으로 분리한다.
- 심볼: CharacterEnvironmentProtectionResolver.Resolve / ThermalProtectionProfile.Add / SpeciesThermalProfileExtensions.Apply / ThermalProtectionSnapshot / CharacterThermalGameplayEffectProjection.Apply

### GAP-096 환경 작업복4종의 보호 수치와 종족별 냉장 근무 조건 누락

- 보완할 문서: 환경 작업복 도감 / species-culture-and-life
- 현재 문서: 4개 작업복 도감은 연구 요구와 아이템 링크를 보여 주지만 보호24필드는 없다. 방한 작업복은8°C 상시 근무, 룬 방한복은2°C 근무로 요약되어 종족 차이를 파악하기 어렵다.
- 추가·정정할 내용: 방한 작업복은 쾌적최저/안전최저-8°C·냉기노출0.35배, 룬 방한복은-10°C·0.2배, 보온 점액 패드는-4°C·0.6배다. 모두 최고온도 오프셋0·열기노출1배이고 운반 멜빵은 온도 보호가 중립이다. 연구·아이템 관계8개는 이미 표시되므로 유지한다. 8°C/2°C 근무 가능성을 모든 종족의 무노출 보장으로 쓰지 않는다. 다른 보호가 없으면 데몬의 쾌적최저는 방한복 착용 후12°C, 룬복 착용 후10°C이므로 각각8°C/2°C에서도 냉기가 누적된다. 슬라임 패드의 allowedSpecies=Slime은 작성값이나 실제 착용 경로가 이를 직접 검사하지 않으므로 확정된 종족 제한으로 표시하기 전에 APPAREL-U03을 해소해야 한다. 자동착용의 공통 시작 조건은 GAP-085에서 참조한다.
- 심볼: EnvironmentalWorkwearSO.Protection / EnvironmentalWorkwearRuntime.TryGetEquippedItemInstance / TryEquip / TryAutoEquipForCold

### GAP-097 정식침대·이층침대의 휴식 중 방한 보정 누락

- 보완할 문서: 침대 도감 / weather-seasons-and-environment
- 현재 문서: 두 침대 도감은 분류·크기와 일반 회복 역할만 표시한다. 환경 문서와 욕구 참고 문서에도 침구 방한값과 계산 조건이 없다.
- 추가·정정할 내용: Rest 업무로 배정된 시설이 Rest 역할을 제공하고 coldProtection>0이면 쾌적최저를coldProtection만큼, 안전최저를그 절반만큼 낮추며 냉기노출에0.6배를 곱한다. 정식침대의5는-5°C/-2.5°C/0.6배, 이층침대의4는-4°C/-2°C/0.6배다. 이는 실내 온도를 그만큼 올리는 수치가 아니다. 방 온도 오프셋 및 열 보호 작성값은 별도 소비 경로 확인이 필요하다. 해당 helper는 실제 수면중 여부나 침상 위치를 직접 검사하지 않으므로 실제 잠든 동안에만 적용된다고 단정하지 않는다. 조건 확인 문제는 APPAREL-U05로 분리한다.
- 심볼: ApplySleepingInsulation / BuildingTemperatureAbility.coldProtection / AssignedWorkType / SupportsRole

### GAP-098 의복56종의 체형·사이즈·착용 부위·겹침과 개조 조건 누락

- 보완할 문서: species-culture-and-life / 의복 아이템 도감
- 현재 문서: 종족 가이드는 의복3벌과 특수체형 장비의 목표 효율만 안내한다. 의복56개 아이템 facts에는 무게·적재·가격만 있으며 체형·맞음새·부착 부위·겹침 조건이 없다.
- 추가·정정할 내용: 의복별 체형(Humanoid/Construct/Any), 겹(속옷/내의/겉옷/갑옷/장신구), 맞음새, 필수·점유 부위와 꼬리/날개/뿔 개조 조건을 전수표에서 표시한다. 골렘은 Construct이고 다른 종족은 슬라임도 Humanoid로 판정한다. 크기는 코볼트 Small, 오크/골렘 Large, 그 외 Medium이다. Sized는 같은 크기, Adjustable은 한 단계 차이까지 허용하며 Accessory는 크기 검사를 받지 않는다. 신체에 필요한 부위가 없거나 필수 개구부가 닫혀 있으면 거부한다. 같은 겹에서 점유 부위가 겹치는 옷은 교체하며 다른 겹은 그 검사에서 제외한다. 전투 방어층 공식과 의복 착용 슬롯을 혼동하지 않는다. 정적 실행은 환경작업복 자동착용→TryPlanChange→TryCommitChange에서 확인했으며 56종 전체의 직접 선택 UI·자율 환복 경로는 APPAREL-U04로 남긴다. 인접 크기의 기분-5/이동0.97은 미연결 소재투영기 값이므로 현재 적용 규칙으로 공개하지 않는다.
- 심볼: AnatomyAttachmentQuery.CanEquip / GetBodyForm / GetSize / GetAvailablePoints / RequiredOpenings / CharacterApparelAggregate.TryPlanChange / TryCommitChange

### GAP-099 원단12종의 비교 수치·의복 재료 선택 규칙과 미연결 성능 구분 누락

- 보완할 문서: production-quality-and-supply / species-culture-and-life / 원단 아이템 도감
- 현재 문서: 원단12개 아이템은 무게·적재·가격만 표시한다. 가이드는 원단의 보온·방수·내구가 다르다고 쓰지만 비교 수치나 실제 제작 재료 선택 조건을 설명하지 않는다.
- 추가·정정할 내용: 의복은 allowedMaterialTags와 한 개 이상 태그가 겹치는 원단을 사용한다. 재료 선택은 ExactMaterial/ID순/최저가격/최고보온/최저무게배율/최고내구를 구분하며 Ready 상태이고 사용 가능하며 금지되지 않은 재고에서 필요 수량을 한 소재로 모은다. 여러 소재를 합쳐 최소량을 채우지는 않는다. 최고보온은 warmth, 최저무게는 weightMultiplier, 최고내구는 durability를 읽으므로 12원단의 해당36값을 비교할 수 있게 한다. 전체8수치96개 전수표에서 heatResistance/waterResistance/airborneResistance/sterility/dryingRate의60값은 작성되어 있지만 실제 보호·건조 소비가 확인되지 않았음을 감사 기록으로 분리한다. 소재·품질·내구·젖음·오염·개구부 보정은 ApparelMaterialProjector에 있으나 사용처를 찾지 못했으므로 보호 수치가 실제 주민에 적용된다고 쓰면 안 된다. 최저가공난도 정책은 현재 MaterialId순이라는 불일치와 부분예약 선택 문제는 APPAREL-U06에 남긴다.
- 심볼: TextileMaterialDefinitionSO / ApparelWorkOrderRuntime.TrySelectMaterial / ApparelMaterialSelectionPolicy / ApparelMaterialProjector.GetOrCreate

## 구현 확인 사항

아래 항목은 문장을 추가해 해결한 것으로 처리하지 않는다. 정적 확인 결과이며 실제 재현·코드 수정은 하지 않았다.

### APPAREL-U01 소재 성능 투영의 라이브 소비자 부재

전체 Assets/Scripts 비Editor 역검색에서 IApparelMaterialProjector/ApparelMaterialProjector는 정의와 DI 등록뿐이고 ApparelProjectionKey 생성자는 선언 외 사용이 없다. CharacterEnvironmentProtectionResolver는 EnvironmentalWorkwearSO.Protection만 읽으며 소재·품질·내구·젖음·오염을 반영하지 않는다. 투영기의 보온/내열/방수/공기/무균·인접크기 효과를 실제 적용으로 표시하지 않는다. Weight도 BaseWeight×소재배율이 아닌 실물 정의 질량 Query를 읽는다.

### APPAREL-U02 종족 공기·조명·습도 필드와 소비 경로 구분

comfortableAirMinimum, comfortableLightMinimum/Maximum, visualStrainMultiplier, preferredHumidity, drynessSensitivity는 정의·프로필 복사 외 사용이 없었다. 현재 칸별 환경 노출은 일반 공기·조명 기준을 읽는다. airborneExposureMultiplier에는 PopulationHealthRuntime의 공기 전파 질병 소비가 있으므로 미사용으로 세지 않는다. 현재10종족 값은 모두1이다.

### APPAREL-U03 작업복 종족 제한·자동 후보·첫 장착 효과의 불일치

SlimeWarmingPad.allowedSpecies=Slime이나 TryEquip/TryAutoEquipForCold는 AllowsSpecies를 호출하지 않는다. 자동 후보는 재고·연구·방한순으로만 골라 그 후 CanEquip를 검사하므로 최고 후보가 체형 불일치일 때 다른 후보를 재시도하지 않는다. 실제 보호 조회는 layer/occupiedPoints 순의 첫 작업복만 사용한다. Inner인 점액패드가 Outer 방한복을 가릴 수 있으며 허용 조합·효과 우선순위는 실제 시연이 필요하다.

### APPAREL-U04 56종 일반 의복의 착용 UI·자율 환복 경로 미확인

비Editor ICharacterApparelCommand 참조는 정의·구현·DI 등록·EnvironmentalWorkwearRuntime뿐이다. Views 검색에서 TryPlanChange/GetEquipped/해당 인터페이스 호출을 찾지 못했다. 환경작업복 자동착용은 확인했으나 모든 의복의 플레이어 선택·환복 경로가 있다고 주장하지 않는다. 저장·실물 이동은 정적 일부 검토이고 실제 UI/왕복 실행은 미실행이다.

### APPAREL-U05 침구 보호의 실제 수면·위치 조건과 다른 온도 필드

ApplySleepingInsulation은 Rest 업무와 assignedShop의 Rest 역할을 검사한다. 실제 수면 상태·현재 침상 위치는 이 helper의 조건에 없다. roomTemperatureOffset/heatProtection은 이 방한 보정에서 읽지 않으므로 같은 보호 효과로 합치지 않는다. 다른 서비스·시설 이용의 소비 경로는 후속 조사한다.

### APPAREL-U06 원단 정책 명칭과 부분예약 수량 선택

LowestHandlingDifficulty는 가공 난도 수치 대신 MaterialId순이다. TrySelectMaterial은 AvailableQuantity>0으로 후보를 걸러도 수량을 모을 때 Stack.Quantity를 사용한다. 일부 예약된 스택에서 과다 제안 후 하류 예약이 실패하는지, 다른 스택으로 이어갈 수 있는지는 실제 명령 대조가 남았다. DryingRate는 비Editor 전체 역검색에서 소비자가 없었다.

## 검증과 한계

- 종족10/작업복4/의복56/원단12의 개수·ID·공개 대응과 카탈로그82참조를 대조했다.
- 독립 산술45건 오류0. 종족10개 정규화, 크기검사27조합, 방한 예시4개, 배율상한2개, 침구2개다. 실제 C# 실행 테스트가 아니다.
- 기존3153근거·22산출물 해시 변경0. 직접 읽은 도메인 근거273개는 JSON에 경로·해시를 보존한다. 부분 읽기 범위를 별도로 기록했으며 파일 해시가 있다는 이유로 전문 검토 완료로 세지 않는다.
- KB query=`SpeciesThermal Apparel ThermalProtection`, area=code/content/authority, limit8, session80757. stale4건·반환행0. content digest `139a0a989275ecdd5a4a26c10ceb6a1931041c7c928ed0421628faea5cd928c6`, KB digest `ceef8dc8f25f4d327205b15e12346aee0ebc5d6a84aa7eeb1f08af5ce14db0dd`. 생성물을 최신 근거로 사용하거나 재생성하지 않았다.
- Unity 컴파일·실제 UI·착용/세탁 실행·저장 왕복·배포는 미실행이다. 모든 시스템/도감과 전역 의미 중복 감사는 남아 있다.

