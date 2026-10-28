# main 코드 구조 재검사 R29

## 구현 상태

R29-01~07의 수정을 완료했고 현재 소스의 표적 Unity 검사와 필요한 저장/복원/재시도 검증을 통과했다. GitHub main 통합은 진행 중이다. 실행의 실제 범위와 실패 이력은 [progress.md](../../progress.md), 기계 검증 증거는 [R29 manifest](../../Artifacts/QA/StructureR29/verification.json)를 따른다.

| 항목 | 반영한 최소 수정 |
|---|---|
| R29-01 | 알림과 명령의 시설 선택을 한 함수로 공유. 백신은 기존 계약에 맞는 의료 버퍼에서 소비. SO 역할 변경 없음 |
| R29-02 | 기존 실행 서비스의 시설/recipe 물 합산 및 지원 조건을 생산 UI도 재사용 |
| R29-03 | 공개 원장은 깊은 사본, 내부 추론 authority는 원래 원본을 사용해 소비와 롤백 유지 |
| R29-04 | Parts/TryGet/추출 결과/설치 결과를 기존 부품 복사 함수로 보호. 내부 설치 소유자는 유지 |
| R29-05 | 기존 두 지원 링크를 불변 값으로 만들고 태그/목록 보호. 새 관리자 없음 |
| R29-06 | 기존 AI 목표/충동의 사본 조회. Set/Clear 후처리와 직렬화 필드는 유지 |
| R29-07 | 기존 경제 세 소유자의 공개 조회6곳에서 원본 및 중첩 행 공유 차단 |

아래 감사 본문은 수정 전 main b75021b88의 판정 기록이다. 해당 원본 링크도 그 commit에 고정해 수정 후 코드와 혼동하지 않게 했다.

기준: `origin/main b75021b88737b99aab0c0437d1255afd0e25c3f8`, 2026-10-28. 보고서의 과거 완료 표시는 판정 근거로 사용하지 않았다. 게임 코드, 실제 등록과 호출, 관련 에셋을 대조했다. SDK 구현과 원본 Documents checkout의 미반영 작업은 제외했다.

## 판정 요약

감사 당시 확인한 필수 수정은 7개 묶음이다. 1번과 2번은 현행 코드와 에셋의 조건 불일치다. 나머지는 조회자가 원본을 수정해 소유자 명령이나 캐시 갱신을 우회할 수 있는 경계다. 후자는 현재 외부 소비자의 잘못된 쓰기나 저장 손상을 실제 플레이에서 관측했다는 뜻이 아니다.

| 항목 | 우선순위 | 실제 문제 | 최소 수정 범위 |
|---|---|---|---|
| R29-01 | P1 | 질병 대응 시설 선택이 알림과 명령에서 갈라짐. 현행 백신 연구실은 두 경로의 Medical 조건에 맞지 않음 | 기존 시설 capability 조회를 공유하고 접종 목적지를 일치시킴 |
| R29-02 | P2 | 생산 조합 표시가 시설 자체의 공정 물과 폐수 요구량을 누락 | 기존 공정 조건 계산을 조회에서도 사용 |
| R29-03 | P2 | 캐릭터 서사 원장의 행과 소비 횟수를 일반 조회자에게 원본으로 노출 | 일반 조회와 내부 증거 소비 명령의 권한 구분 |
| R29-04 | P2 | 수술 부품의 조회 및 명령 결과 4개 출구가 저장 원본 행을 노출 | 기존 부품 복사 함수를 공개 반환 경계에 적용 |
| R29-05 | P2 | 생산 및 서비스 지원 링크가 가변 캐시 원본으로 노출 | 링크 값과 바깥 목록을 읽기 전용으로 제공 |
| R29-06 | P2 | AI 목표와 충동의 원본 필드 변경으로 commitment 갱신을 우회 가능 | 기존 Blackboard 명령을 유지하고 조회는 사본 제공 |
| R29-07 | P2 | 자동 구매, 거래 원장과 자원 정책의 공개 조회 6곳이 저장 행을 공유 | 각 기존 소유자의 Clone을 공개 반환에 적용 |

P1은 현재 기능 연결을 먼저 고칠 항목이다. P2는 영향이 특정 화면이나 소유 경계에 한정되지만, 현재 구조에서 수정할 이유가 확인된 항목이다. 새 프레임워크, 클래스 수 증가, 줄 수 목적 분할은 요구하지 않는다.

## R29-01. 질병 대응의 시설 선택과 capability가 서로 다름

- 생산자: [CampaignPresentationObserver](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/CampaignPresentationObserver.cs#L695)가 대응 ID를 시설 ID로 바꾸고, 항상 `Medical + 특정 definition ID`로 선택지를 만든다. 시설이 없으면 선택지를 생략한다.
- 명령: [DiseaseFieldResponseRuntime](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/DiseaseFieldResponseRuntime.cs#L185)도 같은 ID 분기를 보유한다. 그러나 [ResolveFacility](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/DiseaseFieldResponseRuntime.cs#L473)의 일반 대응은 `PathogenDiagnosis` capability로 찾고, 없으면 8874 의료 시설을 허용한다. 알림은 이 규칙을 사용하지 않는다.
- 현재 에셋 불일치: [동굴 독감](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Resources/SO/Population/Diseases/Disease_disease-cave-flu.asset#L23)은 백신 대응을 허용한다. 두 코드가 요구하는 [8876 백신 연구실](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Resources/SO/Building/ResearchOverhaul/RF76_백신_연구실.asset#L124)의 역할은 80이다. [Medical 비트](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/Buildings/Core/BuildingPrimitives.cs#L31)는 4096이므로 `80 & 4096 == 0`이다.
- 실제 필터: [SupportsCapability](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Infrastructure/FacilityCapabilityQuery.cs#L58)와 [SupportsRole](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L124)는 역할을 검사한다. 따라서 이 에셋 그대로의 백신 연구실은 두 경로의 접종 대상 시설로 선택되지 않는다. 가동 상태를 바꿔도 이 역할 조건은 충족되지 않는다.
- 소비와 저장 경계: 명령의 [백신 실행](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/DiseaseFieldResponseRuntime.cs#L204)은 선정한 instance ID를 [PhysicalVaccinationRuntime](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/PhysicalVaccinationRuntime.cs#L147)에 전달한다. 후자는 정확한 시설 목적지, 백신 1개, pending 영수증과 면역 반영을 처리한다. 현재 단절은 이 명령에 도달하기 전이다.

**필수 수정**: 알림과 실행이 같은 대응별 시설 조회 정책을 사용해야 한다. 8876은 현재 생산 작업대 정의이므로, Medical 역할을 임의 추가하기보다 접종이 연구실에서 가능한지 또는 의료 시설에서 수행되는지 기존 설계와 물리 목적지를 먼저 맞춰야 한다. 진단 capability와 8874 허용 경로도 알림에서 함께 사용한다. 백신 물자 소비와 저장 영수증의 보호 조건을 완화하지 않는다.

**수정 후 필요한 검증**: 실제 현행 시설 정의로 알림 선택과 명령 목적지가 일치하는지, capability를 가진 대체 진단 시설도 표시되는지 확인. 백신 1개 소비와 면역 적용, pending 저장 후 새 복원에서 동일 요청 재시도의 중복 소비 방지 확인. 이번 조사는 실행하지 않았다.

## R29-02. 생산 화면이 공정 조건을 다시 구현하며 시설 물 요구량을 빠뜨림

- 생산자: [생산 조합 행](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs#L436)은 `FormatSupportState` 결과를 실제 생산 조합 설명에 표시한다.
- 중복 판단: [FormatRecipeUtilityState](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs#L871)는 recipe의 물과 폐수 수량, recipe의 수동 급수 허용만 검사한다. [지원 준비 표시](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs#L868)까지 이어지는 경로에서도 시설 자체 공정 수량은 합산하지 않는다.
- 실행 권위: [실제 작업 가능 여부](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/Economy/Content/ProductionBillExecutionRuntime.cs#L127)는 [ValidateFacilityUtilities](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/ProductionCycleUtilityService.cs#L411)를 호출한다. 이 코드는 시설과 recipe 수량을 더하고 두 수동 급수 조건을 조합한다.
- 현행 반례: [P15 조리대](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Resources/SO/Building/Modular/P15_조리대.asset#L149)는 `work:cook`에 물 0.25와 폐수 0.25를 요구한다. [치즈 숙성](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Resources/SO/Economy/Recipes/recipe_cheese.asset#L29)은 같은 작업대와 작업 종류를 사용하지만 recipe 물과 폐수는 둘 다 0이다. [숙성 선반](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Resources/SO/Building/ProductionSupport/WS16_치즈_숙성_선반.asset#L110)의 물과 폐수도 0이다.
- 다른 조건이 충족되고 폐수 연결이 없는 경우, UI의 이 계산은 폐수 문제를 찾지 못하지만 실행은 0.25를 검사한다. [폐수 네트워크 검사](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs#L723)는 네트워크가 없으면 거절한다. 따라서 지원 준비 문구와 실행 거절이 양립한다. 실제 씬을 실행해 관측한 결과가 아니라 현행 입력과 분기의 정적 반례다.

**필수 수정**: 이미 존재하는 공정 서비스의 수량 계산과 실패 이유를 부작용 없는 조회에서 재사용한다. UI는 그 결과를 표현한다. 지원 시설 표시와 실제 주문의 단계별 검사 역할은 유지한다. 별도 UI 규칙 표나 서비스 체계를 추가할 필요는 없다.

**수정 후 필요한 검증**: 현행 조리대와 치즈 조합으로 폐수 연결 전후 표시와 실행 결과 일치 확인. 시설과 recipe가 모두 물을 요구할 때 합계 검사, 수동 급수 허용 조합의 대표 실패 확인. 저장 형식 변경이나 전체 생산 recipe 검증은 요구하지 않는다.

**제외한 반례**: UI는 현재 작업대의 주문을 세고 실행은 전체 주문을 세지만, [지원 연결 생성](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/ProductionWorkshopRuntime.cs#L234)은 지원 시설을 단일 작업대에 연결한다. 다른 작업대와 지원 시설을 공유한다는 가정으로 별도 결함을 만들지 않았다.

## R29-03. 캐릭터 서사 원장 조회가 저장 원본과 증거 소비 권한까지 노출

- 소유자: [NarrativeLedger getter](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/Core/CharacterProgression.cs#L91)가 직렬화 필드 원본을 그대로 반환한다.
- 가변 깊이: [CharacterNarrativeFact](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/Core/CharacterNarrativeLedger.cs#L60)는 증거 ID, 집계 값, `influenceUseCount`, `sourceEventIds`, 이벤트 문맥을 public 필드로 갖는다. 원장의 `facts`, `nextEventSequence`, `Facts`도 같은 원본에 연결된다.
- 실제 소비자: [AI 문맥 구성](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/AI/NarrativeRequestContext.cs#L446)과 [공개 캐릭터 투영](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/AI/NarrativePublicCharacterProjection.cs#L273)이 이 조회를 사용한다. 현재 이 소비자가 원장을 잘못 수정하는 사례는 확인하지 않았다.
- 내부 쓰기 의존: [특성 증거 해석](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/Core/CharacterTraitInferenceTransaction.cs#L146)은 authority가 준 원본 행을 잡는다. [증거 소비](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/Core/CharacterTraitInferenceTransaction.cs#L262)와 실패 시 240행 복구가 이 원본을 직접 변경한다. 따라서 공개 getter에 무조건 Clone을 붙이면 이 내부 계약이 끊긴다.
- 저장 연결: [CapturePersistentState](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/Core/CharacterProgression.cs#L981)와 [월드 저장](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Infrastructure/CharacterWorldSaveService.cs#L1691)는 이 원장을 복사해 저장한다. 조회에서 바꾼 원본은 소유자 명령 없이 다음 저장과 증거 계산에 들어갈 수 있다.

**필수 수정**: 일반 소비자에게는 깊은 사본 또는 불변 조회를 제공하고, 기존 추론 트랜잭션의 증거 소비와 복구는 소유자 내부 명령으로 유지한다. 기존 `Clone`, 증거 검증과 롤백 구조를 사용한다. 단순 getter 치환이나 신규 원장 관리자를 추가하는 작업이 아니다.

**수정 후 필요한 검증**: 조회 사본의 행, 중첩 이벤트 문맥, source ID 목록을 바꿔도 소유자 원장 불변. 기존 명령의 기록과 특성 추론 소비 횟수는 정확히 반영. 게시 실패와 재시도에서 횟수 롤백 및 저장 후 복원 값 일치 확인. 저장 필드와 과거 원장 해석 유지.

## R29-04. 수술 부품이 공개 출구 4개에서 저장 원본으로 반환

- 저장 권위: [parts](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs#L47)는 `SurgeryAggregateStateStore.State.Parts`에 연결된다.
- 공개 출구: [Parts](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs#L109), [TryGet](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs#L121), [TryCreateExtractedPart 결과](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs#L183), [TryConsumeForInstallation 결과](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs#L679). 생성 결과는 `parts.Add(part)`한 동일 객체이고, 설치 결과도 [현재 행 조회](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Medical/SurgicalPartInstallationRuntime.cs#L197)에 연결된다.
- 행의 권한: [SurgicalPartInstance](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L732)에는 신선도와 품질뿐 아니라 예약, 설치 여부, 물리 item ID, 설치 영수증 필드가 public으로 열려 있다. `IReadOnlyList`만으로 행 변경을 막지 못한다.
- 실제 소비자: [수술 선택 UI](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Views/UI/CharacterSurgeryWindow.cs#L612), [능력치 계산](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Effects/Runtime/CharacterDerivedStatsSnapshot.cs#L210), 수술 물류와 효과 처리기가 읽는다. 확인한 일반 소비자의 직접 오염 쓰기는 없었다.
- 저장 연결: [수술 저장](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/Medical/Core/SurgeryPersistence.cs#L23)는 같은 부품 행을 복사한다. 외부 조회에서 예약이나 영수증을 바꾸면 물리 항목과의 일관성 검사를 우회한 상태가 저장 입력에 포함될 수 있다.

**필수 수정**: 공개 조회와 명령 결과는 [기존 CaptureParts](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs#L1167)의 복사 방식을 사용한다. 내부 설치 및 교체 소유자는 현재 행을 계속 명령으로 수정한다. 실패한 out 결과까지 원본이 새지 않는지 확인한다. 항상 false와 null을 반환하는 `TryCreateCraftedPart`를 다섯 번째 원본 출구로 세지 않는다.

**수정 후 필요한 검증**: 4개 출구의 행을 수정해도 저장과 소유자 불변. 실제 추출, 예약, 설치, 설치 재시도가 기존처럼 반영. pending 영수증 저장 후 복원과 동일 설치 재시도에서 중복 소모 없음. 기존 DTO 및 설치 identity 형식 유지.

## R29-05. 생산 및 서비스 지원 링크 조회가 캐시 변경 권한을 함께 전달

- 생산 링크: [ProductionSupportLinkSnapshot](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/ProductionWorkshopRuntime.cs#L6)의 모든 연결 필드에 public setter가 있다. [GetLinks](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/ProductionWorkshopRuntime.cs#L61)는 내부 List를, [TryGetLinkForSupport](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/ProductionWorkshopRuntime.cs#L107)는 내부 행을 반환한다.
- 서비스 링크: [ServiceSupportLinkSnapshot](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/ServiceRooms/ServiceRoomLinkRuntime.cs#L6)과 같은 파일 [GetLinks / TryGetLinkForSupport](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/ServiceRooms/ServiceRoomLinkRuntime.cs#L59)도 같은 구조다. 바깥 타입만 IReadOnlyList이며 행과 원본 List는 보호하지 않는다.
- 실제 소비자: [생산 링크 표시](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Views/Buildings/UI/ProductionWorkshopLinkRenderer.cs#L23), 생산 공정 검사, [서비스 수용량과 속도 계산](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs#L350)이 동일 링크를 읽는다.
- 소유권 문제: 조회자가 `Support`를 교체하거나 반환 List를 변환해 제거할 수 있다. 그러면 buildings/grid revision은 그대로인데 실행자가 보는 연결은 바뀐다. [revision 기반 재구성](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/ServiceRooms/ServiceRoomLinkRuntime.cs#L151)은 이런 쓰기를 감지하지 못한다. 현재 소비자가 실제로 그 쓰기를 했다는 증거는 아니다.
- 저장 경계: 링크는 월드에서 다시 계산하는 파생 캐시다. 부품 원본과 달리 저장 DTO 결함으로 합산하지 않는다. 조회와 실행이 캐시 내부 구조에 과하게 결합된 것이 문제다.

**필수 수정**: 두 기존 링크 값을 생성 이후 불변으로 만들거나 사본을 반환하고, 바깥 목록도 원본 List를 수정할 수 없게 한다. FeatureTags의 실제 배열까지 보호한다. BuildableObject를 복제하거나 새 연결 관리자 계층을 만들 필요는 없다.

**수정 후 필요한 검증**: 두 종류 각각 조회 결과 변경으로 원본 연결 및 Version이 바뀌지 않음. 실제 건물/방 revision 변화에는 재구성되고 생산 지원 및 서비스 capacity가 갱신됨. 파생 캐시이므로 별도 저장 스키마나 전수 시드 검증은 요구하지 않는다.

## R29-06. AI 목표와 충동 조회가 명령의 후처리를 우회할 수 있음

- 원본 출구: [ActiveMacroGoal / ActiveMoodImpulse](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/AI/CharacterBlackboard.cs#L81)는 내부 필드 그대로 반환한다.
- 가변 값: [CharacterMacroGoal](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/AI/Core/AiPrimitives.cs#L88)과 같은 파일의 CharacterMoodImpulse는 타입, 목적지, 유효 시간, 충동 강도 등을 public 필드로 갖는다.
- 실제 소비자: [목표 실행](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/AI/CharacterAiMacroDecisionRunner.cs#L39)과 [충동 평가](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/AI/CharacterMoodImpulseUtility.cs#L467)가 이 값을 읽는다. 일반 소비자의 직접 쓰기는 확인하지 않았다.
- 단일 명령과 차이: [SetMacroGoal](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/AI/CharacterBlackboard.cs#L584)은 변경 시 commitment를 해제한다. [SetMoodImpulse](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Character/AI/CharacterBlackboard.cs#L622)는 강도를 정규화하고 필요한 commitment 해제 및 진단을 처리한다. 원본 필드 직접 쓰기는 이 후처리 없이 다음 판단에 들어간다.
- 저장 경계: 이번 조사에서 이 두 임시 판단 상태의 게임 저장 DTO 연결은 확인하지 못했다. 저장 누락이나 영구 저장 손상 문제로 보고하지 않는다.

**필수 수정**: 조회는 기존 작은 값 객체의 사본이나 불변 값을 제공하고, 변경은 Blackboard의 기존 명령으로 수행한다. SoftLockIntent처럼 자신의 명령을 가진 객체와 public DTO 필드 노출을 구별한다. 새 AI 상태 관리자나 판단 트리를 추가할 필요는 없다.

**수정 후 필요한 검증**: 조회 사본 변경은 실제 목표와 commitment에 영향 없음. 실제 Set 명령은 강도 보정과 commitment 갱신을 유지하고 다음 AI 판단에 반영. 임시 상태 getter 수정에 전체 저장/복원 검증을 붙이지 않는다.

## R29-07. 경제 정책과 거래 원장의 공개 조회 6곳이 현재 저장 행을 공유

하나의 새 경제 관리자로 합치자는 항목이 아니다. 기존 세 소유자의 조회 반환만 각각 보호하면 되는 같은 형태의 경계 누락이다.

| 소유자 | 노출 출구 | 생산자와 실제 소비자 | 소유 규칙을 우회하는 지점 |
|---|---|---|---|
| AutoProcurementRuntime | [StockRules / WishlistRules / LastResults](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/Treasury/AutoProcurementRuntime.cs#L735) | 규칙 Upsert와 상점 갱신이 생산. [상점 화면](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Views/UI/ShopFeatureSurfacePresenter.cs#L393)과 구매 평가가 소비 | 규칙 입력은 Clone 및 정규화하지만 조회는 같은 원본 List와 행 반환. 가격, 수량, enabled 및 결과의 중첩 physicalStacks를 직접 변경 가능 |
| EconomyTransactionLedgerRuntime | [Records](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/Economy/Content/EconomyTransactionLedgerRuntime.cs#L187) | 거래 명령이 기록. [자금 HUD](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Views/UI/TreasuryResourceHudController.cs#L430), 합계 계산과 [성공 거래 검색](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/Economy/Content/EconomyTransactionLedgerRuntime.cs#L256)이 소비 | RecordSuccess/Failure를 거치지 않고 source ID, 성공 여부, 금액, 기록 개수 변경 가능. 원장 검색과 합계의 입력도 바뀜 |
| ResourceStockPolicyRuntime 및 Query | [Runtime.Policies](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs#L128), [Query.Policies](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyQuery.cs#L28) | 초기화와 SetPolicy가 생산. [판매 수요](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/ProductionConsumerDemandAdapters.cs#L570), [예측](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/Planning/ResourceEconomyForecastService.cs#L134), 소유자의 Tick이 소비 | [PolicyView 생성](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs#L1450)은 배열만 새로 만들고 dictionary의 행을 그대로 담음. 행 수정으로 SetPolicy 검증과 Version 갱신을 우회 |

- 가변 데이터 근거: [거래 기록](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/Economy/Content/TreasuryEconomyModels.cs#L63), 같은 파일의 242행 재고 규칙, 269행 찜 규칙, 298행 구매 결과, [자원 정책](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/Economy/Content/ResourceEconomyPlanningModels.cs#L26)은 public 필드를 가진 class다.
- 저장 연결: [자동 구매 Capture](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/Treasury/AutoProcurementRuntime.cs#L882), [원장 Capture](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Models/Economy/Content/EconomyTransactionLedgerRuntime.cs#L271), [재고 정책 Capture](https://github.com/Creeper0809/DungeonStory/blob/b75021b88737b99aab0c0437d1255afd0e25c3f8/Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs#L259)는 각각 같은 소유 상태를 복사해 저장한다. 저장 시 복사한다는 사실은 그 전에 조회로 원본을 바꿀 수 있는 문제를 막지 못한다.
- 반증과 한계: 현재 상점 화면은 새 규칙을 만들어 Upsert에 넘긴다. 원장의 TryGetSuccessfulBySource는 이미 행을 복사하고, 자원 정책의 GetOrCreate도 이미 Clone한다. 이 정상 경로까지 원본 누출로 세지 않았다. 확인한 일반 소비자의 직접 오염 쓰기와 실제 금전 오류를 관측한 것은 아니다.

**필수 수정**: 세 소유자의 기존 Clone/CloneResult를 사용해 위 6개 공개 출구에서 현재 행이 공유되지 않게 한다. 구매 결과의 physicalStacks까지 복사한다. 내부 작업 루프는 소유 원본을 유지하고 DTO 형식과 규칙 명령은 보존한다.

**수정 후 필요한 검증**: 조회의 목록, 행과 중첩 구매 결과를 고쳐도 소유자, 합계, 수요 및 Version 불변. 실제 규칙 명령과 거래는 반영. 기존 저장 왕복과 원장 source 검색, 현재 pending 구매/판매 재시도의 단일 처리 확인. 경제 전체 조합이나 전수 시드를 다시 실행할 필요는 없다.

## 크기와 의존 수만으로 수정 대상으로 만들지 않은 항목

| 확인 대상 | 현행 원본 기준 판정 |
|---|---|
| 2,000줄 초과 타입 15개, 큰 생성자 11개 | 원자적 물리 이동, 저장 복구, AI 수명 조율처럼 함께 있어야 하는 작업도 포함한다. 길이만으로 갓 클래스나 저응집 결함으로 판정하지 않음 |
| 기본 어셈블리의 순환 묶음 21개, 최대 1,004파일 | 파일 단위 타입 참조 그래프의 수치다. 1,004개 독립 결함 또는 필요한 신규 클래스 수가 아님. 이 수치 전체를 줄이는 리팩터링은 필수 목록에서 제외 |
| 게임 asmdef 47개 | Editor/test 전용 제외 41개. 선언 참조 그래프의 순환은 0. 기본 어셈블리 내부 파일 순환과 구별 |
| 등록 코드의 큰 의존 수 | 최고 fan-out은 DungeonWorldSimulationRegistration 346파일. 조립 코드가 등록 대상을 아는 책임 자체는 결함이 아님 |
| 수술 UI 후보 선별과 최종 명령 검증 | 같은 전체 계약이 아님. 운영자 자격과 해부 호환은 기존 공통 규칙을 사용함. 화면 후보 목록과 최종 물리 검증을 무리하게 합치지 않음 |
| 의복 수선의 20 내구도 조건 | 현재 UI와 명령 값이 일치함. 새 정책 변경 필요나 오동작 근거 없이 추가 분리 대상으로 만들지 않음 |
| 극단 특성의 고정 ID | 이름 있는 고정 계약, typed rule, 저장 결과 identity에 연결됨. 임의 일반화하면 기존 계약을 깨뜨릴 수 있음 |
| 기술 capability 등록과 실행/설명 | 신규 실행 종류에 구현과 설명이 필요한 사실 자체는 과결합이 아님. 현행 수치 불일치는 확인하지 못함 |
| 과거 기술 공식 표 | 저장 버전 및 catalog hash 선택을 위한 호환 코드. 최신 표와 단순 중복 제거하지 않음 |
| CharacterStats.Stats | ControlledDictionary가 소유자 명령으로 변경을 위임. 가변 사전처럼 보인다는 이유로 원본 DTO 누출로 세지 않음 |
| StaffDiscontent.State, 연구 State와 SoftLockIntent | 명령을 가진 도메인 객체와 DTO를 구별함. 확인한 일반 소비자는 읽기. 필드 원본 노출 항목에 기계적으로 합산하지 않음 |
| 상점 Lots getter | 가변 선언은 있으나 상점 소유자가 감추며 현재 공개 lot 소비는 깊은 snapshot을 사용함. 실제 활성 raw getter 소비 결함으로 확대하지 않음 |
| Captivity 정책, 세력/방어의 내부 원본, 인구 묘비와 작물 | 최종 공개 조회 또는 하위 owner의 Clone을 확인. 내부 원본 getter를 공개 소비자 누출로 잘못 합산하지 않음 |
| 지식 잔재 Tasks | aggregate 원본과 달리 최종 runtime 조회는 값 snapshot 배열이고 저장과 복원 join은 복사본 |
| 지역 공급 계약의 조회 캐시 | 저장 원본의 깊은 사본을 공유하는 mutable projection. 조회자 간 오염 여지는 있으나 현재 직접 쓰기나 명령이 이 캐시를 쓰는 근거는 미확보. R29-05의 실행용 원본 캐시와 구분하고 우선 필수 목록에 추가하지 않음 |
| 시작 파티 적용기의 카메라 호출 | Unity 월드 적용의 의도된 표시 효과. Services 폴더에서 View 타입을 참조한다는 이유만으로 금지하지 않음 |

R28 이후 실제 사본 반환을 확인한 경계에는 제작/의복 주문, 급여 및 용병, 대형 공사, 지역/이동/긴급 대응, 월드 타일, 침입 상태, 수술 주문이 있다. 완료 표시를 재인용한 것이 아니라 반환과 하위 복사 경로를 확인했다. 이것은 R28 전체 실행을 다시 통과했다는 의미가 아니다.

## 조사 입력과 증거 범위

| 항목 | 실제 수행 |
|---|---|
| 기준 고정 | main SHA 및 원격 main 일치 확인. 원본 checkout 변경 보존 |
| 원본 입력 | 게임 소스와 Editor/test, 관련 SO/씬/프리팹, 기존 분석 도구 15,820파일 SHA 고정 |
| 전체 후보 추출 | Runtime C# 2,985파일, embedded Editor 1,035파일. 공개 상태/목록 후보 1,452개, 단순 원본 객체 반환 후보 23개, 단순 가변 행 목록 후보 76개는 탐색 단서로 사용 |
| 의미 의존 그래프 | 현재 기본 어셈블리 게임 소스 2,200개. SDK 소스 528개는 타입 해석에만 사용. metadata 427개, 누락 0 |
| named assembly 해석 | CUHA의 완료 r218 입력 21,746개를 재해시해 drift 0 확인 후 현재 게임 DLL 회수. 기존 rsp의 게임 참조 45개를 현재 metadata로 교체 |
| 기존 정적 지표 | baseline 변경 없이 Verify PASS. mutable static 0, content escape 0, direct session mutation 0. 이 검사들이 모든 DTO 별칭과 의미 중복을 다루지는 않음 |
| 의미 검토 | 후보의 소유자/호출자/저장 연결과 실제 에셋을 확인. 링크 캐시, AI 판단, 생성/설치 결과, UI와 실행의 조건 조합까지 구분 |
| 실행 검증 | 이번 조사는 Unity 컴파일, Play, 실제 UI 클릭, 저장/복원/재시도를 새로 실행하지 않음 |

의존 그래프와 후보 검색은 범위 전체에 실행했다. 모든 메서드와 가능한 플레이 상태를 실행한 증거는 아니다. 본문의 조건 반례와 공개 원본 반환은 현재 소스에서 확인했고, 플레이 중 외부 오염이 발생했다는 주장과 분리했다. 이 보고서는 구조가 완벽하거나 잔여 결함이 0이라는 인증서가 아니다.

외부 증거 폴더: `C:/Users/vulpo/Documents/Codex/2026-10-28/structure-audit-r29`.

- 입력: `input-files.json`, `input-summary.json`, `game-assemblies.json`.
- 최종 그래프: `final-current-main-graph.json`, `final-current-main.rsp`, `final-graph-inputs.json`, `metadata-provenance.json`. 중간 및 fallback 출력은 판정에서 제외.
- 후보와 원본 대조: `classes.json`, `query-candidates.json`, `additional-simple-return-candidates.json`, `additional-simple-collection-candidates.json`, `ui-inventory.md`, `content-coupling-inventory.md`, `state-query-inventory.md`, `remaining-query-inventory.md`.
- 최종 입력 불변 및 문서 링크 검사는 별도 `audit-verification.json`에 기록한다.

