# 설명 누락과 분리해서 확인할 구현 후보

## 의복 유지관리 주문

- [세탁·건조·수선·개구부 작업 대조](apparel-work-maintenance-review.md)의 APPAREL-WORK-U01~07을 기록한다. 주문 WU와 실제 노동 회계, 완료 경계0 WU 재시도, 일반 개조·취소 UI, 시설·의복 자동 선택, 물·세제·전력 소비, 실물 운반/착용 잠금, 민첩 라벨의 제작 숙련 정책을 문서 누락과 구분했다.
- 정의 수치가 있다는 사실과 실제 플레이에서 해당 시간·비용으로 작동한다는 사실은 다르다. API와 저장 함수만으로 UI 제공·복구 성공을 선언하지 않는다. 실제 UI·PlayMode·저장 왕복은 미실행이다.
- 세탁·건조·수선·기존 구멍 여닫기 및 실패/복원 설명의 누락은 GAP-100~103으로 별도 목록화했다. 제작·품질 반복·불합격품 회수·오염/마모 생성자 조사는 아직 남아 있다.

## 종족 환경·의복·원단

- [의복·환경 대조](apparel-environment-review.md)의 APPAREL-U01~06을 추가한다. 소재 성능 투영의 라이브 소비자 부재, 종족 공기·조명·습도 필드의 미연결, 작업복 종족 제한과 첫 장착 효과, 일반 의복 착용 UI, 침구의 실제 수면 조건, 원단 정책·부분예약 수량을 설명 누락과 분리했다.
- 종족 airborneExposureMultiplier는 공기 전파 질병 경로에 소비자가 있으므로 미사용으로 세지 않는다. 원단 보온·무게배율·내구도 제작 재료 선택에는 쓰이지만 완성 의복의 실제 보호 효과 적용 증거는 아니다.
- 이번에는 정적 원본 대조만 했으며 실제 UI·환복·세탁·저장 왕복 또는 코드 수정은 하지 않았다.

## 기후·관측·계절 사건

- [기후 대조](climate-mechanics-review.md)의 CLIMATE-U01~08을 추가한다. 기후 선택·시차, 첫 관측탑 선택, 예보 범위, 사건 기한/연말 경계, 계절 Threat11개와 이동창 flag의 실제 소비, 비용부족 시 하루 평가 전체 거부와 실패 알림, 저장·장비·질병 실행, 작성 강도와 실제 피해의 차이를 분리했다.
- 7개 사건의 물·숯 비용/질병 대상·수치는 관계에 이미 있으므로 전부 미노출로 세지 않는다. Threat 표식만으로 시설 동결·해충·발화를 실행했다고 설명해서도 안 된다.

## 컨베이어 목적지·필터 UI와 재개 동작

- [컨베이어 대조](conveyor-mechanics-review.md)의 구현 확인 사항7개를 추가한다. 특히29개 포트의 목적지 작성값이 모두 비어 있고, 자동반입이 요구하는 SetPortDestination의 비Editor 선언 외 호출은 전체 Assets/Scripts 역검색에서0개다. UI에서 운영 가능한 목적지 설정 경로가 있는지 확인해야 한다.
- 배출 정책 UI에는 확인한 범위에서 지정 창고 선택기가 없으며 품목/분류/소재 필터 목록 편집도 API와 화면 제공 범위가 다르다. 경로·저장·넘침의 다른 확인 사항과 원본 근거는 별도 JSON에 보존한다.
- 저장 타이머/수동 승인 초기화, 배출구의 가동·전력·필터 무시,8개 후보 상한의 기아 가능성을 정적 확인 후보로 남긴다. 실제 재현이나 코드 수정은 수행하지 않았다.

## 자동화 품질 상한과 UI 표시

- automaticQualityCap은 작성 27개에서 모두 0.75다. 비Editor C# 소비자는 역검색에서 확인되지 않았으며 0.50~0.90 검사는 Editor 밸런스 범위 검사다. 실제 적용을 확인하기 전에는 무인 생산품의 품질 제한이 구현되어 있다고 설명하지 않는다(GAP-056).
- AutomationRuntime.CreateSnapshot은 PoweredAssist의 배율과 Automatic의 WU/초를 WorkRate 한 필드로 반환한다. IndustrialFeatureSurfacePresenter.AddAutomation은 두 값을 모두 x로 표시한다. 자동 제출WU를 수동 대비 속도 배율로 읽게 되는 단위 혼동이다. 실제 Unity 화면 재현은 미실행이다.
- 같은 UI는 !Operational && Status.IsBlocked일 때만 중단 이유를 보여 준다. Operational은 수동이거나 전력 공급 및 F<100으로 계산되므로, 전력 정상·고장 미만 상태의 주문 없음·재료 부족·출력 막힘은 중단 사유가 표시되지 않을 수 있다. 실제 UI 검증 전에는 재현 완료라고 하지 않는다.
- 근거: AutomationRuntime.cs의 CreateSnapshot/TickFacility, IndustrialFeatureSurfacePresenter.cs의 AddAutomation, IndustrialInfrastructureBuildingAbilities.cs, InfrastructureBalanceCalibrationScenario.cs. 이번 작업에서 스크립트는 수정하지 않았다.

## 계승 프로필과 과거 런 저장의 복원 우선순위

- MetaProfilePersistenceService.Start는 별도 프로필을 State.Restore로 읽으며, MetaProgressionRestoreBuilder.CommitTo는 런 저장의 계승 상태로 Aggregate를 교체한다. Builder에는 최신 프로필과의 병합이 없다.
- State.Merge 정의는 있지만 프로필·슬롯의 실제 부팅 순서와 슬롯 복원 뒤 재적용은 아직 확인하지 않았다. 과거 저장을 열어도 최신 계승 재화·강화가 유지된다고 단정하지 않는다. GAP-050/051의 프로필 기록과 저장 왕복 검증을 구분한다.
- 근거: MetaProfilePersistence.cs, MetaProgressionSaveSection.cs, MetaProgressionRestoreBuilder.cs, MetaProgressionModel.cs. 실제 저장 후 재개는 미실행이다.

아래 항목은 곧바로 공개 위키에 설명을 추가할 대상이 아니다. 정의가 있어도 실제 게임 경로에서 쓰이지 않는 값은 구현 확인 없이 실행 규칙으로 설명하면 안 된다.

## 질병의 직접 작업·이동 배율

- PopulationHealthRuntime.CreateSymptomEffect는 질병 중증도와 표적 기능으로 WorkSpeedMultiplier, MoveSpeedMultiplier, MoodDelta를 만든다.
- IDiseaseSymptomEffectQuery 비 Editor 사용처 전체 검색에서 GetActiveSymptoms는 PopulationHealthApplicationAdapter.ProjectSymptomMood의 기분 반영에 사용된다.
- CharacterStatsProjectionService는 diseaseSymptoms를 생성자로 받지만 해당 필드를 읽는 계산은 현재 확인되지 않았다. 작업·이동 공식은 피로, 결핍, 환경, 장비와 공용 성능값을 사용한다.
- PopulationHealthRuntime.GetWorkSpeedMultiplier/GetMoveSpeedMultiplier의 직접 외부 호출도 확인되지 않았다. 현재 기분과 해부 장기 감염 부담은 각각 실제 일일 이벤트로 반영된다.
- 판정: 직접 속도 배율은 미연결 후보. 공용 신체 기능을 통한 간접 영향과 구분해서 후속 조사한다. GAP-021에는 확인된 기분·장기 부담만 설명 대상으로 기록했다.
- 근거: Assets/Scripts/Services/Character/PopulationHealthRuntime.cs, Assets/Scripts/Models/Species/Core/PopulationHealthDomain.cs, Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs. 런타임 수정이나 실행 테스트는 하지 않았다.

## 같은 이름의 핵 부식 두 문서

- medical/condition-core-corrosion는 DiseaseDefinitionSO의 만성 환경 상태다.
- character/condition-core-corrosion는 노화에 따른 해부 부위 기능 저하 설명을 가진 별도 공개 레코드다.
- 서로 다른 정의인지, 같은 플레이어 상태의 이중 설명인지 노화·환경 상태 경로와 공개 모델의 출처를 추가 대조해야 한다. 같은 이름만으로 중복 삭제 대상이나 누락으로 판정하지 않는다.

## 다인 작업 계산기의 지원 범위와 현재 작성 콘텐츠

- SettlementLaborBalanceRules는 랜드마크8명·자동5명과 연구2/4명 곡선을 지원한다. 그러나 BuildingWorkAmountAbility는 현재 소형/중형/산업시설만 허용하며419개 Resources 필드도 해당3종뿐이다. 현재 연구180개 maximumResearchers는 모두1이다.
- GAP-036은 현재 시공2/3/4명·별도대형사업6명과 연구프로젝트당1명 규칙을 다룬다. 랜드마크/협동연구 수치를 현재 선택 가능한 콘텐츠로 소개하라고 요구하지 않는다. 향후 해당 콘텐츠가 작성되면 공개 도감과 함께 재검토한다.
- EvaluateInvestmentReturn/EvaluateDisasterShadow는 비Editor·비DebugScenarios 소스 역검색에서 정의 외 호출이 없다. 분석 도구의 목표/추정을 실제 플레이에서 자동 실행되는 투자회수·재난규칙으로 오인하지 않는다.

## 이민 수용 심사의 0값 예외

- SettlementPopulationAcceptanceRules는 인당 노동지수와 최근 보장성장WU가 양수일 때만 각각 하한과3WU를 검사한다. 완료일 기록이 없는 초기 상태뿐 아니라 기록된 값이0인 경우에도 검사를 건너뛰는 코드다.
- 이 동작을 의도된 초기 유예로 단정하지 않는다. GAP-038에 현재 동작과 설계 확인 필요성을 함께 남겼다. 감사에서 로직을 수정하거나 실제 플레이를 실행하지 않았다.

## 연구 예상기간과 예약 UI의 별도 기준

- ResearchTreeWindow의 기간 표시는 (미완료 선행WU+현재 잔여WU)÷99(180×0.55)다. research.md의45WU/일(실제 수행량 기록50WU)과 같은 계산으로 설명하면 안 된다. 실제 승인 작업 경로에는 도구·특성·다인 기여·메타 보정이 별도로 있어 이 표시식만으로 실전 완료일을 단정할 수 없다. 표시 기준·설계 목표·실행 평균의 관계를 후속 대조한다.
- Coordinator.Enqueue는 선행연구 자동등록을 지원하지만 ResearchTreeWindow는 Locked 노드의 예약 버튼을 비활성화한다. 따라서 잠긴 목표를 선택해 모든 선행을 예약할 수 있다는 사용법은 아직 증명되지 않았다. GAP-045는 명령 규칙과 실제 버튼의 상태 제한을 함께 적는다.

## 연구실16의 공개 도감 대응

- P1_ResearchLab.asset은 기초2/기록1/고급1을 제공한다. GameDomainContentCatalog와 현재 GameplayScene(23,2)에서 GUID=5e5bcfb2d2f27699482222f9a8279828 참조를 확인했다.
- 공개 facility/building-16.json과 같은제목 연구실은 확인되지 않았다. 연구능력 제공8개 작성시설 중 이 항목만 표준 공개경로에 없다. 공개제외 정책 또는 다른 항목으로의 투영인지 원인을 더 확인해야 한다.
- 자산이 P1 폴더에 있다는 이유만으로 미사용이나 공개제외가 옳다고 가정하지 않는다. 반대로 정적 참조만으로 플레이어의 현재 건설·구매 경로가 모두 열려 있다고도 주장하지 않는다. GAP-043의 기능표와 연구-projection 상세에서 이 한계를 표시한다.

## 금단의 도약 대상 선택과 사용 이력

- ResearchTreeWindow.ResolveForbiddenLeapResearcher는 생존한 특성302 보유자 중 인물ID순 첫 인물을 선택한다. 이미 해당 프로젝트에서 도약을 사용했는지는 후보 필터에 없다. 다른 미사용 인물이 있어도 첫 인물의 사용 이력으로 명령이 거부될 수 있는 구조다.
- 버튼은 선택노드의 Active/Queued/Suspended 상태를 이용하지만 TryForbiddenResearchLeap에는 선택한 프로젝트ID를 넘기지 않는다. BlueprintResearchRuntime이 현재 실제 활성 프로젝트를 해석해 적용한다. 사용자가 선택한 대기노드에 도약을 적용한다고 안내하면 잘못이다.
- GAP-048은 확정한 확률/진척/후유증과 현재 UI 제약을 기록한다. 대상 선택 UI의 의도 확인과 실제 클릭 재현은 미완료이며 이번 감사에서 고치지 않는다.
- 근거: Assets/Scripts/Views/UI/ResearchTreeWindow.cs, Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs, Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs.

## 연구 기간 추정의 선행우회 청사진

- ResearchTreeWindow의 선행WU 재귀 합계는 완료 여부를 확인하지만 Shortcut 청사진으로 우회할 수 있는 선행을 제외하지 않는다. 따라서 표시기간을 실제 실행 경로의 최소필요WU나 현재 생산속도 측정치로 해석하면 안 된다.
- GAP-049의 실제 승인WU 공식과 UI99WU/일 추정치를 구분했다. 우회청사진 상태별 실제 화면 검증은 미실행이다.

## 주인 특성 후보 계승 강화의 실제 소비처

- meta:starting-owner-trait-candidate는 작성 카탈로그·구매UI·레벨변경·프로필 저장과 +1 정수효과 정의가 있다.
- GetStartingOwnerTraitCandidateBonus 및 meta:starting-owner-trait-candidates/상수의 비Editor 역검색에서 모델·Runtime·Reader 전달 외 실제 시작 특성 선택의 소비처는 확인되지 않았다. StartPartyPreparationService 및 RunStartVariableSelector의 관련 선택 경로도 이 조회를 사용하지 않는다.
- 따라서 GAP-050은 강화9종의 존재와 구매 규칙을 적되 이1종의 실제 선택폭 증가를 검증완료로 세지 않는다. 확인한 다른 효과의 정적 소비 경로와도 분리한다. Unity 실행·저장왕복은 수행하지 않았다.
- 근거: GameDomainContentCatalog.asset의 metaUpgrades, MetaProgressionEffects.cs, MetaProgressionRuntime.cs, MetaProgressionRuntimeProvider.cs, RunStartVariableSelector.cs, StartPartyPreparationService.cs.

## 의복 맞춤 제작의 반복·신화·권태 확인 사항

[의복 제작 부분 보고서](apparel-crafting-review.md)의 APPAREL-CRAFT-U01~05에서 근거와 읽은 범위를 추적한다. 문서에 없는 규칙과 구현 결함 후보를 같은 완료 항목으로 세지 않는다.

- 다음 시도 번호를 증가시킨 뒤 한도에 걸리면 품질 난수 번호가 이전 값으로 남는다. Failed 주문도 저장하며 복원은 번호 불일치를 거부한다. 기본10회·전부 불합격인 정적 모델에서9회 후 실패하며 번호는9/8이다. 실제 저장 왕복은 미실행이다.
- 신화 승격은 존재하지만 품질 목표 UI와 사전 최고 품질 판정은 전설까지만 지원한다.
- 가중 품질 능력0을50으로 바꾸는 분기는 기여 기록 부재와 실제0을 구별하지 않는다.
- 의복 BOM/WU가 생산식에만 있다는 수치 원장 설명과 맞춤 제작의 자체 수량·WU 계산이 다르다. 연구 해금과 두 경로의 공개 안내는 후속 대조다.
- 영감60% 기여 조건과 반복 권태 기록 조건이 다르며, 횟수 초기화가 기존 기분 효과 즉시 제거를 뜻하는지는 확인하지 않았다.

## 조합식 보존 슬롯의 실제 의미

- MetaProgressionRuntime.EndRun은 이번 런의 해금ID 전체를 문자열순으로 정렬해 슬롯수만큼 선택하고 MetaProgressionState.PreserveRecipes가 기존집합에 추가한다. 기존에 보존한ID를 먼저 제외하지 않으므로 다음 런에서도 앞쪽ID가 재선택될 수 있다.
- 프로필 전체 보존집합에는 슬롯수 상한을 다시 적용하지 않는다. '전체 최대3개를 사용자가 골라 교체하는 보존칸'이라는 설명은 현재 코드와 다르다. 실제 소비는 시설합성·시설진화 공개조건이며 모든 일반제작식 연구조건을 우회한다고 볼 근거는 없다.
- GAP-050에 현재 동작을 보존했고 사용자가 선택하는 슬롯으로 바꾸거나 기존기록 우선 제외 여부를 정하는 것은 별도 구현 결정이다.

## 침입 경고 강화와 침입후보 문턱

- 카탈로그의 침입경고 강화 설명은 더 이른 경고를 말한다. 하지만 InvasionThreatRuntime.GetWarningThresholdMultiplier는 TryRaiseWarning의 warningThreshold뿐 아니라 TickCandidateDelay의 candidateThreshold와 강제후보 처리에도 곱해진다.
- 레벨당-8% 강화는 알림 문턱과 실제 침입후보 문턱을 함께 낮춘다. 단순한 경고 편의 보너스라고 설명하면 부수효과를 숨기게 된다. GAP-050에는 현재 두 적용처를 모두 기록했으며 의도된 설계인지 판단하는 작업은 남았다.
- 근거: Assets/Resources/SO/Content/GameDomainContentCatalog.asset의 meta:invasion-warning-accuracy, Assets/Scripts/Services/Invasion/InvasionThreatRuntime.cs의 TryRaiseWarning/TickCandidateDelay/GetWarningThresholdMultiplier. 실제 시드 시뮬레이션은 하지 않았다.

## 기본 구조 수리의 남은 작업량과 HP 회복률

- BuildingStructuralIntegrityDefaults는 명시 모듈이 없는 내벽·문·복도에2HP/WU를 부여한다. ExecuteStructuralRepair는 남은 작업량을 SO.GetAbility의 회복률로 나누며 명시 모듈이 없으면1HP/WU를 사용한다.
- 실제 ApplyRepairWork는 런타임2HP/WU를 소비하므로 마지막 틱의 승인WU 상한과 남은WU 기록이 실제 필요한 양보다 클 수 있다. 예를 들어 HP가1 부족하고 틱 작업이1WU라면 실행기 상한은1WU지만 HP 회복에는0.5WU만 필요하다. 이는 원본 식의 정적 비교이며 실제 프레임·노동회계 실행 검증은 하지 않았다.
- GAP-057에는 확정한 후보·HP 회복 규칙만 기록하고 이 불일치를 의도된 수리 비용으로 안내하지 않는다.
- 근거: Assets/Scripts/Services/Buildings/BuildingStructuralIntegrityRuntime.cs, Assets/Scripts/Services/Combat/Work/RepairWorkExecutionHandler.cs, Assets/Scripts/Services/Buildings/BuildableObject.cs.

## 복도의 기본 구조 모듈과 침입자 대상 층

- Hallway.asset은Category Wall/Layer Hallway이다. Defaults.TryCreate가 IsStructuralWall OR IsWall을 사용하므로300HP/강도18/2HP·WU 모듈을 생성한다.
- DefenseBreachTargetRuntimeAdapter.TryGetTargetAt은GridLayer.Building의 점유자만 읽는다. 복도에 모듈이 있다는 사실만으로 침입자가 복도를 벽처럼 부술 수 있다고 설명할 수 없다.
- 작성10개와 기본 벽·문3개는 도감 수치52개 누락으로 확정했지만 복도1개의 구조 수치 설명 필요 여부는 다른 피해·복구 소비처를 확인할 때까지 보류한다. 코드와 자산은 바꾸지 않았다.

## 방어 화면의 구조 파괴 예상 시간

- DefenseFeatureQueryService는 HP/max(1,공격자수×10)×(격노?0.65:1)로 표시시간을 추정한다. 실제 구조 공격은 성능값·캐릭터/근접/설정배율·강도·공격간격·격노피해1.25배를 사용한다.
- 따라서 표시시간은 현재 공격자들의 실제 타격시간 예측과 같지 않다. GAP-059/060의 실제 피해와 경로 비용을 이 표시값으로 검증하지 않는다. 화면 숫자의 설명/교정 필요성은 별도 확인 대상으로 남긴다.
- 근거: Assets/Scripts/Views/UI/DefenseFeatureQueryService.cs, Assets/Scripts/Services/Invasion/InvasionIntruderCombatRules.cs, Assets/Scripts/Services/Invasion/InvasionIntruderExecutionCoordinator.cs.

## 전력망의 차단·축전·조작 한계

- 차단 중에도 수요가 남으면 발전량0을 분모로 과부하율을 계산해 열이 계속 증가할 수 있다. 복구 명령은 열60 미만을 요구한다. 대기만으로 냉각·복구할 수 있다고 안내하지 않으며, 실제 조작으로 부하를 제거할 수 있는지와 설계 의도는 별도 검증 대상이다.
- 방전은 원량을 부족량에서 먼저 뺀 뒤 효율을 곱한다. 저장 여유가 있는 다른 축전지가 있어도 손실분 부족을 다시 메우지 않는다.
- 충전은 저장 공간이 부족해도 입력량 전체를 가용 전력에서 뺀다. 앞쪽의 가득 찬 축전지가 뒤쪽 축전기에 갈 전력을 소모할 수 있다.
- 소비자 최소 공급 비율 판정 전에 축전 방전이 진행된다. 판정을 통과한 소비자가 없어도 축전량은 줄어들 수 있으며, 남은 방전량을 다시 저장하는 경로는 확인되지 않았다.
- 위 세 축전 항목은 코드의 계산 순서를 대조한 후보다. 실제 게임 실행이나 버그 수정은 하지 않았다. 이상적인 에너지 보존식을 현재 구현 동작인 것처럼 위키 보완안에 넣지 않는다.
- 연결 모듈의 normallyOpen/maxThroughput은 비Editor 검색에서 선언 외 사용을 확인하지 못했다. 차단 스위치나 전력 전송 한도로 공개하기 전에 소비처가 필요하다.
- 수차 발전기의 연료 필요 값은 false이며 연료ID/가동시간은 이 발전 경로에서 쓰이지 않는다. 수원·유속·배치 제약은 전체 경로를 확인하지 않았으므로 이름만으로 추가하지 않는다.
- 산업 패널의 우선순위 카드는 실제 수요가0인 노드를 제외하고 최대40개만 보여 준다. 소비 시설이67종이라는 사실과 별개로, 살아 있는 모든 인스턴스가 조작 화면에 표시되는지 확인해야 한다.
- 노드별 발전 표시값은 작성 발전량이고 망 전체 발전량은 고장 보정 후 값이다. 두 값을 같은 측정값으로 비교하면 안 된다.
- 근거: ElectricalNetworkRuntime의 EvaluateNetwork/ResolveDischargeRate/ChargeStorage/UpdateOverload/ResetBreaker, IndustrialInfrastructureTopology, IndustrialFeatureSurfacePresenter. 상세 경로·작성값은 power-mechanics-review.json에 기록했다. GAP-061~065와 구분하며 현재 구현을 고치지 않았다.

## 급수·폐수 경로의 미확인 부분

- 누수(Leak)를 늘리는 자연 발생 경로는 전체 비Editor C# 쓰기 검색에서 확인하지 못했다. 복원, 복제와0으로 만드는 수리 경로는 있다. 현재 누수 효과 공식과 미래의 발생 확률을 혼동하지 않는다.
- 하수망이 가득 차면5초마다 최초 노드에 역류1을 만들지만 탱크 폐수량은 줄이지 않는다. 배관 수리가 막힘을 없애도 처리·저장 문제가 그대로면 역류가 재발한다.
- 폐수 처리 완료 시 슬러지 SpawnItemAt의 실패값을 무시한다. 출력이 실제로 소실되는 조건·의도는 실행 검증하지 않았다.
- 현재 물통 충전소 waterPerBatch는1이다. 다른 소수 배치에서 RoundToInt로 만든 아이템 수량과 물 양의 보존을 검증한 것은 아니다.
- 생활 시설은 급수 후 이용 완료 시 배수하며 수동 폐기물·재래식 이용 분기가 있다. 공정 급배수와 수동 물통 거래의 나머지 구간·대상 자산·취소/저장 왕복은 추가 조사 대상이다.
- 근거와 읽은 범위: fluid-mechanics-review.json/MD. GAP-066~070은 확인한 공통 규칙과7시설 작성34필드의 설명 누락이며 전체 유체 도메인 완료가 아니다.

## 생활·공정 급배수 후속 구현 확인 후보

- 담금·당화조 WS02는 보조시설 모듈에 물0.25와 수동허용을 작성하지만, FluidFacilityInputOwnerAuthority.BuildDescriptors에는 보조시설·조합식 전용 목적지 생성 분기가 없다. 실제 수동 공급이 가능한지는 확인해야 한다.
- 수동 입력 용량은 시설 자체 물 요구량의 올림값이다. 조리 시설0.25는 물1개분인데 곡물죽 합산 요구는3.65이므로 잔량0에서 물4개가 필요하다. 요청량에 맞게 용량을 확장하는 경로는 이 소유자에서 발견하지 못했다. 실제 입고/실행 재현 전에는 버그 확정이 아닌 불일치 후보다.
- EnsureCycleSupply는 한 스택에 시설 기본 요구량의 올림 수량이 있어야 준비된 것으로 보며 시설 잔량을 빼지 않는다. 실제 소비는 잔량과 여러 스택을 합칠 수 있다. 준비와 소비의 판정 차이는 추가 실행 검증 대상이다.
- 수동 물 거래 준비 후 배수나 다른 소비 지점에서 실패하는 경우, 저장/재시도 중 중복 소비·배출이 없는지는 실행하지 않았다. 주석이나 영수증 필드만으로 원자성을 보장하지 않는다.
- 폐수 종류는 구성과 질량의 기록을 확인했다. 종류별 질병 확률·전용 정수 효율은 소비처를 확인하지 않았으므로 공개 규칙으로 만들지 않는다.
- Facility.cs와 OperatingDaySettlement.cs는 기존 직접근거 이후 변경됐다. 시설 급배수 호출 두 지점은 현재 원문을 재독했으나, 이 파일을 인용한 기존 모든 항목은 재검토 전까지 이전 판정을 현재 실행의 인증으로 쓰지 않는다.
- 근거: fluid-use-mechanics-review.json/MD와 GAP-071~075. 코드·작성 자산·공개 위키는 수정하지 않았다.
