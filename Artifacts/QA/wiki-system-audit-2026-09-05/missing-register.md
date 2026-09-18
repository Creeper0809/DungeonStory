# 확인된 구현 → 공개 위키 GAP 원장

상태: **2026-09-14 의미 재검토 반영 완료**. 활성 254건, 전체 275건 중 이번 재검토 반영 275건.

기존 ID를 유지한다. 철회·중복·근거 미충족 항목은 [제외 이력](missing-register-history-2026-09-08.md)에 보존한다.

## 공개 위키 작성 기준

- 작성 목록은 확인된 플레이어 규칙·조건·비용·수치·결과를 담는다. 게임 규칙의 예외·제외 조건도 작성 대상이다.
- 비작성 목록은 잘못된 주장, 미확인 일반화, 중복 범위와 개발 전용 정보를 담는다. 감사 한계는 별도 기록한다.
- 작성·비작성 목록이 충돌하면 어느 쪽도 자동 우선하지 않는다. 근거로 충돌을 해결하기 전 게시하지 않는다.
- 근거 경로·행·코드 심볼·감사 상태는 검증용이다. 이 원장을 고친 것이며 공개 위키에 게시한 것은 아니다.
- 이번 검증은 정적 대조다. Unity 실행·실제 UI 조작·저장 왕복의 전수 성공을 뜻하지 않는다.

## GAP-001 시작 후보의 전체·부분 재추첨

- 분류: 설명 누락
- 보완할 문서: starting-party
- 현재 확인한 차이: 시작 안내에는 후보 선택만 있고 부분별 횟수, 전체 재추첨의 재충전, 각 묶음의 유지·교체 범위가 없다.
- **위키에 작성할 정보:**
  - 정체성·재능·기술 부분 재추첨은 인물별로 각각 3회다. 전체 재추첨은 정체성·숙련 시드·잠재력을 다시 만들고 세 부분 횟수를 모두 3회로 재충전한다.
  - 정체성 재추첨은 잠재력 등급을 유지한다. 재능 재추첨은 정체성을 읽어 시작 프로필과 숙련 시드·잠재력을 갱신한다. 기술 재추첨은 준비된 기술 후보를 교체한다.
- **위키에 작성하지 않을 정보:**
  - 항목별 추가 비작성 정보 없음. 공통 기준 적용.
- 감사 원본 근거: [StartPartyPreparationService.cs 244행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs:244>): 전체·부분 재추첨 구현 / [OwnerSelectionPanel.cs 529행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Character/UI/OwnerSelectionPanel.cs:529>): 실제 UI 호출
- 감사 위키 근거: [starting-party.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/starting-party.md:9>): 선택 후보만 설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 시작 안내에는 후보 선택만 있고 부분별 횟수, 전체 재추첨의 재충전, 각 묶음의 유지·교체 범위가 없다.

## GAP-002 시작 기술 자동 확정과 시작 가능 조건

- 분류: 조건 누락
- 보완할 문서: starting-party
- 현재 확인한 차이: 자동 확정과 인물별 시작 조건이 안내에 없다. 기존 UI 비활성 미검증 중 정적 버튼 gate는 두 준비 UI의 interactable 조건으로 확인했다.
- **위키에 작성할 정보:**
  - 일반 시작 인물의 1레벨 액티브 후보는 준비 과정에서 자동 확정된다.
  - 시작하려면 사장은 고정 기술 4칸을 갖추고, 일반 인물은 준비된 첫 액티브·선택된 액티브·첫 패시브를 모두 갖춰야 한다. 준비 인원 모두가 이 조건을 만족해야 시작 버튼이 활성화된다.
- **위키에 작성하지 않을 정보:**
  - 실제 화면 클릭·생성 요청 실패·재시도까지 실행 검증됐다는 설명
- 감사 원본 근거: [StartPartyPreparationService.cs 56행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs:56>): 사장/일반 준비 완료 조건 / [StartPartyPreparationService.cs 660행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs:660>): 액티브 confirmed:true / [StartPartyPreparationUiController.cs 474행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Character/UI/StartPartyPreparationUiController.cs:474>): 전원 IsReadyToStart로 시작 가능 계산 / [OwnerSelectionPanel.cs 425행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Character/UI/OwnerSelectionPanel.cs:425>): start.interactable=canStart
- 감사 위키 근거: [starting-party.md 20행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/starting-party.md:20>): 시작 숙련만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: UI 실입력 및 생성 서비스 실패 경로는 실행하지 않음
- 이번 재검토: 자동 확정과 인물별 시작 조건이 안내에 없다. 기존 UI 비활성 미검증 중 정적 버튼 gate는 두 준비 UI의 interactable 조건으로 확인했다.

## GAP-003 던전 주인의 고정 기술

- 분류: 시스템 누락
- 보완할 문서: starting-party
- 현재 확인한 차이: 고정 슬롯은 4칸이다. 기존 FixedSlotCount 미확인은 현재 선언과 준비 조건에서 해소했지만 권능별 효과는 검증되지 않았다.
- **위키에 작성할 정보:**
  - 사장은 일반 시작 액티브·패시브 선택 대신 전용 고정 기술 4칸을 사용한다.
- **위키에 작성하지 않을 정보:**
  - 개별 권능의 실제 효과 수치와 발동 범위가 검증됐다는 설명
  - fallback 설명문을 실제 효과의 증거로 사용하는 내용
- 감사 원본 근거: [StartPartyPreparationSnapshot.cs 112행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationSnapshot.cs:112>): FixedSlotCount=4 / [StartPartyPreparationService.cs 52행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs:52>): 사장 고정 기술 수로 시작 gate
- 감사 위키 근거: [starting-party.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/starting-party.md:15>): 중심 인물·원정 제외만 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 권능별 authored 효과와 실제 발동 consumer 미검증
- 이번 재검토: 고정 슬롯은 4칸이다. 기존 FixedSlotCount 미확인은 현재 선언과 준비 조건에서 해소했지만 권능별 효과는 검증되지 않았다.

## GAP-004 전투 난이도와 생존 압력의 분리

- 분류: 설정 누락·오표기
- 보완할 문서: starting-party
- 현재 확인한 차이: 전투 난이도와 생존 압력은 독립 입력이고 적 생성은 전투 난이도를 소비한다. 욕구 안내는 생존 압력 보정을 낮은/높은 난이도로 잘못 표기한다.
- **위키에 작성할 정보:**
  - 전투 난이도와 생존 압력은 독립 설정
  - 원정 적 체력/공격/주도권: 쉬움 0.8/0.8/1, 보통 1/1/1, 어려움 1.25/1.2/1.1. 생존 압력은 느긋함·표준·가혹함
- **위키에 작성하지 않을 정보:**
  - 생존 압력의 욕구 소모 보정을 전투 난이도 배율로 부르는 표현
- 감사 원본 근거: [StartPartyPreparationService.cs 424행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs:424>): 두 입력 분리 / [DungeonDifficulty.cs 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Run/Core/DungeonDifficulty.cs:39>): 적 배율 / [EnemyEncounterFactory.cs 196행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/EnemyEncounterFactory.cs:196>): 적 생성에서 소비
- 감사 위키 근거: [need-references.json 12행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/need-references.json:12>): 낮은/높은 난이도 표기
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 전투 난이도와 생존 압력은 독립 입력이고 적 생성은 전투 난이도를 소비한다. 욕구 안내는 생존 압력 보정을 낮은/높은 난이도로 잘못 표기한다.

## GAP-005 생존 압력의 추가 보정과 붕괴 주기

- 분류: 수치 누락
- 보완할 문서: needs/*
- 현재 확인한 차이: 욕구 문서는 기본 소모/회복 배율만 제공하며 별도 결핍 증가·회복 보정과 붕괴 유예를 설명하지 않는다.
- **위키에 작성할 정보:**
  - 느긋함·표준·가혹함 순서로 결핍 부담 증가 배율은 0.75/1/1.25, 회복 배율은 1.15/1/0.85다.
  - 강제 붕괴 유예는 각각 45/30/24게임초다.
- **위키에 작성하지 않을 정보:**
  - 개인용 지속 물 소비와 모든 고부담 피해 타이머의 실행 연결이 검증됐다는 설명
- 감사 원본 근거: [SurvivalBalanceSettings.asset 59행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Survival/SurvivalBalanceSettings.asset:59>): 세 압력별 설정 / [CharacterDeprivationRuntime.cs 1400행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/CharacterDeprivationRuntime.cs:1400>): 결핍 보정 소비
- 감사 위키 근거: [need-references.json 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/need-references.json:23>): 피해는 일정 간격이라고만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 개인용 물 소비 및 피해 타이머 호출 사슬 미완료
- 이번 재검토: 욕구 문서는 기본 소모/회복 배율만 제공하며 별도 결핍 증가·회복 보정과 붕괴 유예를 설명하지 않는다.

## GAP-006 숙련 감소의 최종 정지점

- 분류: 본문 오류
- 보완할 문서: residents-and-work
- 현재 확인한 차이: 현재 공개 표·예제는 1,199.999 XP로 정확하지만 같은 문서 93줄 본문은 기술자 하한에 고정된다고 적어 모순이다.
- **위키에 작성할 정보:**
  - 숙련 감소로 전문가에서 기술자로 강등되면 1,199.999 XP에서 감소가 멈춘다. 기술자 등급의 시작점은 400 XP이므로 이를 기술자 하한이라고 부르면 안 된다.
- **위키에 작성하지 않을 정보:**
  - 항목별 추가 비작성 정보 없음. 공통 기준 적용.
- 감사 원본 근거: [CharacterProficiencyDomain.cs 368행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Characters/CharacterProficiencyDomain.cs:368>): 감소 정지점 / [CharacterProficiencyDomain.cs 83행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Characters/CharacterProficiencyDomain.cs:83>): 기술자 경계400
- 감사 위키 근거: [residents-and-work.md 93행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:93>): 하한 오표기 / [residents-and-work.md 97행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:97>): 정확한 예제
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 현재 공개 표·예제는 1,199.999 XP로 정확하지만 같은 문서 93줄 본문은 기술자 하한에 고정된다고 적어 모순이다.

## GAP-008 멘토·학생의 배정 조건

- 분류: 조건 누락
- 보완할 문서: family-education-and-legacy
- 현재 확인한 차이: 기존 공통 교육 안내는 배정 거부 조건을 생략한다. CanAssign 앞부분과 시설 검색을 다시 읽어 생존·현장·서로다름·시설 조건까지 구체화했다.
- **위키에 작성할 정보:**
  - 멘토와 학생은 서로 다른 살아 있는 현재 던전 인물이어야 하고, 가르칠 숙련 및 파손되지 않은 멘토 학원이 필요하다.
  - 멘토는 해당 숙련 전문가 이상이며 학생보다 최소 1등급·200 XP 높아야 한다. 양방향 관계 모두 -0.20 이상이어야 하고 한 멘토는 동시에 학생 3명까지 맡는다.
- **위키에 작성하지 않을 정보:**
  - 정착 신분별 멘토링 허용표 전체를 확인했다는 내용
- 감사 원본 근거: [CareerMentorshipService.cs 148행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/CareerMentorshipService.cs:148>): 멘토 등급·XP·관계·학생 상한
- 감사 위키 근거: [family-education-and-legacy.md 36행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:36>): 교육 WU와 경험치만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: CanParticipateInMentoring의 모든 신분별 세부 정책은 별도 미검토
- 이번 재검토: 기존 공통 교육 안내는 배정 거부 조건을 생략한다. CanAssign 앞부분과 시설 검색을 다시 읽어 생존·현장·서로다름·시설 조건까지 구체화했다.

## GAP-009 멘토링의 관계 배율과 멘토 보상

- 분류: 수식 일부 누락
- 보완할 문서: family-education-and-legacy
- 현재 확인한 차이: 관계 보정의 정확한 구간과 학생 추가 배율·멘토 XP는 공개 안내에 없다. 변경된 교육 실행기는 물리 수업과 보상 commit 뒤 같은 XP식을 적용한다.
- **위키에 작성할 정보:**
  - 양방향 관계 중 낮은 값 사용: -0.20 미만 수업 중단, -0.20 이상 0 미만 0.8배, 0 이상 0.5 미만 1배, 0.5 이상 1.1배
  - 학생 XP에는 멘토의 학생 경험치 보정 효과도 곱함
  - 멘토는 30×0.08×0.25=0.6 XP를 받으며 연습 시각 갱신
- **위키에 작성하지 않을 정보:**
  - 관계 배율만 충족하면 물리 교육·보상 확정 조건 없이 XP를 받는다는 설명
- 감사 원본 근거: [CareerApplicationAdapter.cs 191행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/CareerApplicationAdapter.cs:191>): 학생 일일 경험치·멘토 효과 / [CareerApplicationAdapter.cs 218행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/CareerApplicationAdapter.cs:218>): 멘토 .6 XP와 연습 갱신 / [CareerApplicationAdapter.cs 276행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/CareerApplicationAdapter.cs:276>): 양방향 최소 관계와 구간
- 감사 위키 근거: [family-education-and-legacy.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:39>): 관계 보정은 수식에만 존재
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 관계 보정의 정확한 구간과 학생 추가 배율·멘토 XP는 공개 안내에 없다. 변경된 교육 실행기는 물리 수업과 보상 commit 뒤 같은 XP식을 적용한다.

## GAP-010 종족별 번식 방식·단계·기간

- 분류: 시스템·표 누락
- 보완할 문서: family-education-and-legacy
- 현재 확인한 차이: 인간이 추가되어 현재 등록·소비되는 번식 프로필은 11개다. 공개 가족 안내와 번식 도감은 단계 개수만 요약하여 모드별 단계 기간이 빠져 있다.
- **위키에 작성할 정보:**
  - 현재 번식 대상은 인간·모험가·오크·악마·야수인·흡혈귀·하피·코볼트·마이코니드·슬라임·골렘의 11종이다.
  - 임신형의 시도/임신/출산/회복 기간은 인간·모험가·오크 1/40/1/20일, 악마 1/50/1/25일, 야수인 1/32/1/15일, 흡혈귀 1/60/1/30일이다.
  - 슬라임은 시도1일·생체량 축적15일·핵 분열5일·안정10일, 골렘은 골격 조립15일·핵 각인10일·인격 각인10일·가동5일이다.
  - 산란형의 시도/알 형성/부화/회복은 하피 1/12/24/12일, 코볼트 1/10/20/10일이다. 마이코니드는 시도1일·포자 혼합5일·균사 확장20일·자실체 형성10일이다.
- **위키에 작성하지 않을 정보:**
  - 이전 10종 수를 현재 전체 번식 대상 수로 쓰는 내용
- 감사 원본 근거: [Reproduction_Human.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Human.asset:21>): 시도/임신/출산/회복1/40/1/20 / [Reproduction_Orc.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Orc.asset:21>): 임신형 단계 / [Reproduction_Slime.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Slime.asset:21>): 핵분열 단계 / [Reproduction_Golem.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Golem.asset:21>): 조립 단계 / [GameDomainContentCatalog.asset 1914행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/GameDomainContentCatalog.asset:1914>): 번식11프로필 등록1914~1924 / [ReproductionCommandRuntime.cs 129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs:129>): 부모종족 정의 조회;147상대;175선택표현형 정의 실제사용 / [LifeSimulationContracts.cs 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/LifeSimulationContracts.cs:39>): 단계번호 의미 / [Reproduction_Adventurer.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Adventurer.asset:21>): 현재 단계별 일수 직접 재독 / [Reproduction_Beastkin.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Beastkin.asset:21>): 현재 단계별 일수 직접 재독 / [Reproduction_Demon.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Demon.asset:21>): 현재 단계별 일수 직접 재독 / [Reproduction_Vampire.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Vampire.asset:21>): 현재 단계별 일수 직접 재독 / [Reproduction_Harpy.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Harpy.asset:21>): 현재 단계별 일수 직접 재독 / [Reproduction_Kobold.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Kobold.asset:21>): 현재 단계별 일수 직접 재독 / [Reproduction_Myconid.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Myconid.asset:21>): 현재 단계별 일수 직접 재독
- 감사 위키 근거: [family-education-and-legacy.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:15>): 성인 나이 설명 / [reproduction-golem.json 10행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/reproduction-golem.json:10>): 단계4개 요약만 / [reproduction-human.json 10행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/reproduction-human.json:10>): 현행 인간 번식 요약은 단계 수4만, 단계별 기간 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 이번 재검토: 인간이 추가되어 현재 등록·소비되는 번식 프로필은 11개다. 공개 가족 안내와 번식 도감은 단계 개수만 요약하여 모드별 단계 기간이 빠져 있다.

## GAP-011 번식 가능한 조합과 교차 계통 조건

- 분류: 조건 누락
- 보완할 문서: family-education-and-legacy
- 현재 확인한 차이: 현재 pair 검사는 골렘·친족·다른 번식 모드의 배양 요구를 거부한다. 공개 가족 안내에는 이 제한이 없다. 운반자 선택은 명시한 부모 ID를 우선해 단순 역할 제한으로 일반화할 수 없다.
- **위키에 작성할 정보:**
  - 골렘 조립형과 유전적 교잡은 허용되지 않는다. 친족 제한 판정에 걸린 조합은 번식 계획을 만들 수 없다.
  - 부모의 번식 방식이 다르면 교차 계통 배양 시설이 필요하다.
- **위키에 작성하지 않을 정보:**
  - 친족 차수별 전체 허용표
  - 명시적으로 운반자를 지정한 경우까지 생식 역할이 항상 검증된다는 주장
- 감사 원본 근거: [ReproductionDomain.cs 549행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:549>): 명시적 pair 제한 / [ReproductionCommandRuntime.cs 188행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs:188>): 운반자 판정 호출
- 감사 위키 근거: [family-education-and-legacy.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:13>): 일반 가족 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 친족 차수별 전체 제한은 미검토; 명시 운반자 선택은 자동 역할 선택과 별도임
- 이번 재검토: 현재 pair 검사는 골렘·친족·다른 번식 모드의 배양 요구를 거부한다. 공개 가족 안내에는 이 제한이 없다. 운반자 선택은 명시한 부모 ID를 우선해 단순 역할 제한으로 일반화할 수 없다.

## GAP-012 번식 성공률과 유산 판정

- 분류: 수식·위험 누락
- 보완할 문서: family-education-and-legacy
- 현재 확인한 차이: 실제 Attempt 첫날 성공률과 임신 중 유산 조건은 공개 기본 성공률 요약만으로 알 수 없다.
- **위키에 작성할 정보:**
  - 성공률=기본확률×clamp((건강+영양)/200,0.1,1)×clamp(가임계수,0,1). 임신 단계에서 건강30 미만 또는 영양20 미만이면 하루 유산 확률 0.10/임신안정도(0.5~1.5). 연령·유전 보정도 가임계수에 반영
- **위키에 작성하지 않을 정보:**
  - Attempt가 없는 골렘 단계에도 매일 성공률 판정을 적용한다는 설명
- 감사 원본 근거: [ReproductionDomain.cs 273행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:273>): 임신 단계 유산 / [ReproductionDomain.cs 285행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:285>): Attempt 첫날 판정 / [ReproductionDomain.cs 561행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:561>): 성공 확률식 / [ReproductionApplicationAdapter.cs 102행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/ReproductionApplicationAdapter.cs:102>): 일일 컨텍스트 전달
- 감사 위키 근거: [family-education-and-legacy.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:13>): 조건/확률 없음 / [reproduction-golem.json 10행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/reproduction-golem.json:10>): 기본 성공률 공통 요약
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 실제 Attempt 첫날 성공률과 임신 중 유산 조건은 공개 기본 성공률 요약만으로 알 수 없다.

## GAP-013 부화·배양의 환경 대기와 실패

- 분류: 실패 조건 누락
- 보완할 문서: family-education-and-legacy
- 현재 확인한 차이: 모드별 온도 대기·연속 실패·복구 초기화 규칙이 공개 가족 안내에 없다.
- **위키에 작성할 정보:**
  - 알·포자·핵분열은 온도 범위 밖에서 단계 일수 진행이 멈추며 연속 3일이면 실패한다. 정상 온도로 돌아오면 연속 위험일수가 0이 된다.
  - 현재 일일 번식 환경 판정에는 실외 온도가 사용된다.
- **위키에 작성하지 않을 정보:**
  - 임신과 골렘에도 같은 온도 중단 규칙을 적용한다는 설명
- 감사 원본 근거: [ReproductionDomain.cs 301행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:301>): 환경 의존 모드 및3일 실패 / [ReproductionApplicationAdapter.cs 102행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/ReproductionApplicationAdapter.cs:102>): 실외 온도 소비
- 감사 위키 근거: [family-education-and-legacy.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:13>): 가족 생활 조건 일반론
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-018
- 이번 재검토: 모드별 온도 대기·연속 실패·복구 초기화 규칙이 공개 가족 안내에 없다.

## GAP-015 자녀의 종족·유전 특성·적성

- 분류: 시스템·수식 누락
- 보완할 문서: family-education-and-legacy
- 현재 확인한 차이: 부모 종족 선택·유전 후보 한도·적성 평균과 변동의 현재 원본은 스냅샷과 같고 가족 안내는 해당 규칙을 제공하지 않는다.
- **위키에 작성할 정보:**
  - 부모 종족 중 결정론적 선택, 부모별 최대3개 유전 후보에서 발현 최대4·잠재 최대2, 적성=부모 적성 정수평균+결정론적 -5~5(0~100 제한)
- **위키에 작성하지 않을 정보:**
  - 항목별 추가 비작성 정보 없음. 공통 기준 적용.
- 감사 원본 근거: [ReproductionDomain.cs 582행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:582>): 종족 선택 / [ReproductionDomain.cs 600행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:600>): 부모 각3 후보·중복 제거·4/2 / [ReproductionDomain.cs 618행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:618>): 적성 평균±5 / [ReproductionCommandRuntime.cs 225행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs:225>): 계획에서 유전 선택 / [ReproductionCommandRuntime.cs 432행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs:432>): 적성 상속 소비
- 감사 위키 근거: [family-education-and-legacy.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:13>): 가족 일반론만
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 부모 종족 선택·유전 후보 한도·적성 평균과 변동의 현재 원본은 스냅샷과 같고 가족 안내는 해당 규칙을 제공하지 않는다.

## GAP-016 생식 치료제의 사용 시점과 효과

- 분류: 아이템 효과·조건 누락
- 보완할 문서: family-education-and-legacy
- 현재 확인한 차이: 아이템은 일반 용도를 이미 설명하지만 소비 시점·수량·효과 배율과 골렘 제외가 빠져 있다.
- **위키에 작성할 정보:**
  - 생식 치료제는 번식 계획을 시작할 때 1개 소비한다. 가임 계수는 1.2배(최대 1), 임신 안정도는 1.15배로 보정한다.
  - 골렘 조립에는 이 생식 치료 보정을 적용하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 생식 치료라는 일반 용도 자체도 위키에 전혀 없다는 표현
- 감사 원본 근거: [ReproductionDomain.cs 251행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:251>): 계획 중 선택·골렘 거부 / [ReproductionDomain.cs 572행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:572>): 효과식 / [ReproductionCommandRuntime.cs 328행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs:328>): 시설 검사 후 비용 예약/실제 소비
- 감사 위키 근거: [medical-fertility-treatment.json 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/medical-fertility-treatment.json:18>): 일반 치료 용도는 이미 기재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 이번 재검토: 아이템은 일반 용도를 이미 설명하지만 소비 시점·수량·효과 배율과 골렘 제외가 빠져 있다.

## GAP-017 가족 계획 정책과 자동 계획 주기

- 분류: 시스템 누락
- 보완할 문서: family-education-and-legacy
- 현재 확인한 차이: 가족 정책별 동작과 허용 정책의 10일 평가·첫 조합 1건 계획 생성 조건이 공개 안내에 없다.
- **위키에 작성할 정보:**
  - 중지·계획·허용 정책
  - 허용 정책은 이전 평가부터10일마다 성인 후보를 검사하고 계획 가능한 첫 조합 하나를 생성
  - 생식 역할 없음·조립자는 이 자동 평가에서 제외
- **위키에 작성하지 않을 정보:**
  - 자동 가족 계획 생성과 번식 과정의 자동 시작을 같은 행위로 설명하는 내용
- 감사 원본 근거: [ReproductionDomain.cs 625행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:625>): 10일 / [ReproductionCommandRuntime.cs 560행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/ReproductionCommandRuntime.cs:560>): 일일 이벤트와 후보 필터·첫 계획 반환
- 감사 위키 근거: [family-education-and-legacy.md 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:11>): 가족 계획 정책 없음
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 가족 정책별 동작과 허용 정책의 10일 평가·첫 조합 1건 계획 생성 조건이 공개 안내에 없다.

## GAP-018 번식 도감의 공통 문장이 모드별 예외를 숨김

- 분류: 도감 오류
- 보완할 문서: entry/character/reproduction-*
- 현재 확인한 차이: 공통 도감 문장은 골렘에도 성공률·생존 온도 판정이 적용되는 것처럼 적었지만 해당 실행 모드에는 적용되지 않는다.
- **위키에 작성할 정보:**
  - 골렘 조립은 골격·핵·인격·가동 단계를 진행하며 생식 시도 단계의 성공률 판정을 하지 않는다.
  - 범위를 벗어난 온도로 단계가 중단되는 번식 모드는 알·포자·핵분열뿐이다. 임신·골렘은 이 규칙의 대상이 아니다.
- **위키에 작성하지 않을 정보:**
  - 골렘 도감의 기본 성공률 0.35와 온도 0~45를 실제 단계 실패 조건으로 안내하는 내용
- 감사 원본 근거: [Reproduction_Golem.asset 22행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Reproduction/Reproduction_Golem.asset:22>): Attempt 없는13~16단계 / [ReproductionDomain.cs 285행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:285>): Attempt에서만 성공률 적용 / [ReproductionDomain.cs 301행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/ReproductionDomain.cs:301>): 골렘 온도 적용 제외
- 감사 위키 근거: [reproduction-golem.json 10행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/reproduction-golem.json:10>): 성공률0.35·생존온도0~45 안내
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-010, GAP-012, GAP-013
- 이번 재검토: 공통 도감 문장은 골렘에도 성공률·생존 온도 판정이 적용되는 것처럼 적었지만 해당 실행 모드에는 적용되지 않는다.

## GAP-019 동굴 독감·핵 부식의 개별 질병 수치·성격 누락

- 분류: 도감 필드 누락
- 보완할 문서: entry/medical/* (DiseaseDefinitionSO)
- 현재 확인한 차이: 동굴 독감 도감은 상세 facts가 비어 있고, 핵 부식과 전염성 질병의 개별 수치·백신 차이를 공중보건 공통 문서만으로 알 수 없다.
- **위키에 작성할 정보:**
  - 동굴 독감은 잠복기 2일, 기본 전염 기간 6일, 기본 감염률 0.18, 중증도 25다.
  - 핵 부식은 전염성 감염병과 구별되는 만성 상태이며 백신을 적용하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 질병 16개 전부의 세부 수치와 등록 상태를 다시 확인했다고 쓰는 내용
- 감사 원본 근거: [Disease_disease-cave-flu.asset 17행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Diseases/Disease_disease-cave-flu.asset:17>): 잠복2·전염6·0.18·25 / [Disease_condition-core-corrosion.asset 17행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Population/Diseases/Disease_condition-core-corrosion.asset:17>): 비전염·만성·백신불가
- 감사 위키 근거: [disease-and-public-health.md 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/disease-and-public-health.md:37>): 개별 페이지에 수치를 위임 / [disease-cave-flu.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/medical/disease-cave-flu.json:2>): facts빈 배열
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 16개 원본 전수 값과 루트 등록 미재검증
- 이번 재검토: 동굴 독감 도감은 상세 facts가 비어 있고, 핵 부식과 전염성 질병의 개별 수치·백신 차이를 공중보건 공통 문서만으로 알 수 없다.

## GAP-020 질병 현장 대응의 물 끓이기·구충 비용과 효과

- 분류: 행동·비용·효과 누락
- 보완할 문서: disease-and-public-health / entry/medical/*
- 현재 확인한 차이: 현장 대응 두 종류의 실물 비용과 중증도 감소가 공중보건 안내에 없다.
- **위키에 작성할 정보:**
  - 질병 현장 대응 중 물 끓이기는 깨끗한 물 2개를 쓰고 대상 질병의 중증도를 12 낮춘다. 구충 대응은 해독제 1개를 쓰고 중증도를 26 낮춘다.
- **위키에 작성하지 않을 정보:**
  - 32개 모든 대응의 시설 선택 가능성·성공적인 물리 소비가 전수 검증됐다는 내용
- 감사 원본 근거: [DiseaseFieldResponseRuntime.cs 53행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/DiseaseFieldResponseRuntime.cs:53>): 두 대응의 비용·중증도 감소 규칙
- 감사 위키 근거: [disease-and-public-health.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/disease-and-public-health.md:21>): 치료 배정 개요만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 32개 전수 실행 연결 미완료
- 이번 재검토: 현장 대응 두 종류의 실물 비용과 중증도 감소가 공중보건 안내에 없다.

## GAP-021 질병 중증도가 기분과 장기에 주는 일일 부담

- 분류: 피해·상태 수식 누락
- 보완할 문서: disease-and-public-health
- 현재 확인한 차이: 질병의 일일 기분 투영과 표적 장기 감염 부담, 만성 분모 30은 공개 안내에 없다. 변경된 PopulationHealthRuntime에서도 이 일일 표적과 부담식은 유지된다.
- **위키에 작성할 정보:**
  - 증상 기간의 매일 기분 변화는 -max(1, clamp(중증도/100,0,1)×10)이며 같은 질병 기분 요인을 갱신한다
  - 매일 해당 기능을 가진 장기 중 생명 유지 우선, 기능 가중치 우선, 노드 ID 순으로 하나를 골라 감염 부담을 중증도/max(1, 기본 전염 기간)만큼 더한다
  - 만성 상태는 기간 대신 30을 쓴다
- **위키에 작성하지 않을 정보:**
  - 직접 업무·이동 배율 getter를 검증 없이 실제 적용 규칙으로 안내하는 내용
- 감사 원본 근거: [PopulationHealthRuntime.cs 278행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/PopulationHealthRuntime.cs:278>): 중증도 정규화 및 기분 투영 / [PopulationHealthRuntime.cs 611행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/PopulationHealthRuntime.cs:611>): 생명기관/가중치/ID 순 선택 / [PopulationHealthRuntime.cs 619행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/PopulationHealthRuntime.cs:619>): 중증도/기간, 만성30 부담
- 감사 위키 근거: [disease-and-public-health.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/disease-and-public-health.md:9>): 증상은 개별 문서에 위임 / [disease-cave-flu.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/medical/disease-cave-flu.json:2>): 상세 없음
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 질병의 일일 기분 투영과 표적 장기 감염 부담, 만성 분모 30은 공개 안내에 없다. 변경된 PopulationHealthRuntime에서도 이 일일 표적과 부담식은 유지된다.

## GAP-022 공기·비말 노출의 공간 묶음과 환경·감염자 수 배율

- 분류: 전파 조건·환경 배율 누락
- 보완할 문서: disease-and-public-health
- 현재 확인한 차이: 공개 공중보건은 일반 경로와 노출 합산을 이미 설명하지만 공기·비말의 공간 묶음, 24시간, 공기질·감염자수 배율이 없다.
- **위키에 작성할 정보:**
  - 공기·비말 감염 노출은 같은 방에 있는 살아 있는 인물을 묶고, 방이 없으면 같은 칸을 묶어 계산한다. 같은 질병의 전염자를 제외한 나머지 인물이 대상이다.
  - 한 번의 일일 노출에 24시간을 기록한다. 공기질 0~100의 환경 배율은 1.5~0.75로 선형 감소하고, 감염자 수 배율 min(2,1+0.15×(감염자 수-1))을 곱한다. 환경 조회 실패 시 환경 배율은 1이다.
- **위키에 작성하지 않을 정보:**
  - 음식·물·혈액 경로의 존재 자체를 새 누락으로 중복 등록하는 내용
  - 다른 모든 노출 생산 조건까지 검증됐다는 내용
- 감사 원본 근거: [PopulationHealthRuntime.cs 538행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/PopulationHealthRuntime.cs:538>): 실제 주변 노출 집계 / [PopulationHealthRuntime.cs 566행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/PopulationHealthRuntime.cs:566>): 공기질 배율 / [PopulationHealthRuntime.cs 590행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/PopulationHealthRuntime.cs:590>): 24시간 및 감염자 수 배율
- 감사 위키 근거: [disease-and-public-health.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/disease-and-public-health.md:13>): 공기·물·음식·혈액 전파 이미 설명 / [disease-and-public-health.md 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/disease-and-public-health.md:37>): 노출 합산도 이미 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 음식/수질/혈액/마나 producer 전수 미확인
- 이번 재검토: 공개 공중보건은 일반 경로와 노출 합산을 이미 설명하지만 공기·비말의 공간 묶음, 24시간, 공기질·감염자수 배율이 없다.

## GAP-023 질병 회복 기간과 치료 완치 면역의 차이

- 분류: 기간·면역 수식 누락
- 보완할 문서: disease-and-public-health
- 현재 확인한 차이: 공개 문서는 자연 회복·백신 기본값은 제공하지만 치료 완치 기본값과 회복 기간·획득/유지 배율의 하한이 빠져 있다.
- **위키에 작성할 정보:**
  - 전염 기간은 max(1, ceil(max(1,기본 기간)/max(0.05,회복 속도)))일이다.
  - 치료 완치의 기본 면역은 35, 기본 일일 면역 감소는 0.08이다. 획득 면역은 clamp(max(0,기본 면역)×max(0.05,면역 획득 배율),0,100), 일일 감소는 max(0,기본 감소)/max(0.05,면역 유지 배율)로 계산한다.
- **위키에 작성하지 않을 정보:**
  - 이미 공개된 자연 회복·백신의 기본 면역을 새 누락으로 다시 세는 내용
  - 면역 획득 배율의 하한 0.05를 빠뜨린 식
- 감사 원본 근거: [PopulationHealthDomain.cs 602행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/PopulationHealthDomain.cs:602>): 완치 제거 및 면역35/0.08 / [PopulationHealthDomain.cs 992행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/PopulationHealthDomain.cs:992>): 회복기간 / [PopulationHealthDomain.cs 1000행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/PopulationHealthDomain.cs:1000>): 획득배율 최소0.05 / [PopulationHealthDomain.cs 1007행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/PopulationHealthDomain.cs:1007>): 유지배율 분모 최소0.05
- 감사 위키 근거: [disease-and-public-health.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/disease-and-public-health.md:39>): 백신·자연회복은 이미 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 이번 재검토: 공개 문서는 자연 회복·백신 기본값은 제공하지만 치료 완치 기본값과 회복 기간·획득/유지 배율의 하한이 빠져 있다.

## GAP-024 상업 화덕 진화의 성급·방 점수·정체성 조건

- 분류: 도감 조건·수치·관계 구분 누락
- 보완할 문서: facility-growth / entry/facility/facilityevolutionrecipe-*
- 현재 확인한 차이: 상업 화덕에서 확인된 성급·방 점수·정체성·추가 비용 조건이 공개 시설 성장 안내에 없다. 필수 가구도 존재하므로 이 부분 목록을 완전한 조건표로 오인하면 안 된다.
- **위키에 작성할 정보:**
  - 상업 화덕 진화는 성급 1에서 2로 바뀌며 사용 가능한 방, 식사 점수 20 이상, 요리 점수 14 이상을 요구한다.
  - 정체성 점수의 서비스/물류 가중치는 0.6/0.4, 최소 정체성 점수는 0.1이다. 별도 작성 재료 비용과 기록 토큰 비용은 없다.
- **위키에 작성하지 않을 정보:**
  - 필수 고유 가구 GUID의 실제 대상을 확인하지 않고 조건표가 완전하다고 쓰는 내용
  - 진화 6개 전체의 정확한 조건을 승인하는 내용
- 감사 원본 근거: [EV_CommercialGrill.asset 36행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/FacilityEvolution/P1/EV_CommercialGrill.asset:36>): 성급·방점수·비용 / [FacilityEvolutionPanel.cs 76행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/FacilityEvolution/UI/FacilityEvolutionPanel.cs:76>): 런타임 진화 호출
- 감사 위키 근거: [facility-growth.md 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/facility-growth.md:37>): 검사 범주만 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 필수 고유 가구 GUID 대상과 6개 진화 전체 조건은 미확인
- 이번 재검토: 상업 화덕에서 확인된 성급·방 점수·정체성·추가 비용 조건이 공개 시설 성장 안내에 없다. 필수 가구도 존재하므로 이 부분 목록을 완전한 조건표로 오인하면 안 된다.

## GAP-025 시설 진화의 추가 변이 선택 기준

- 분류: 보상 조건·우선순위 누락
- 보완할 문서: facility-growth
- 현재 확인한 차이: 공개 안내의 최대 2개 설명 외에 실제 선택 순서와 점수 문턱, 정체성 사용 조건이 없다.
- **위키에 작성할 정보:**
  - 추가 변이는 허용 태그 중 제안된 태그를 먼저 검토한 뒤 증거 점수 내림차순·태그 순서로 보충하고 최대 2개 선택한다. 모든 추가 태그는 증거 점수 0.35 이상이어야 한다.
  - 정체성 압력을 사용하는 진화에서 점수가 clamp01(최소 점수+0.2) 이상이고 태그 증거가 0.4 이상이면 추가 후보가 된다. 또는 태그 압력이 0.5 이상이거나 증거가 0.65 이상이면 후보가 된다.
- **위키에 작성하지 않을 정보:**
  - 8개 태그 각각의 효과와 모든 증거 생산 조건을 검증했다고 쓰는 내용
- 감사 원본 근거: [FacilityEvolutionMutations.cs 54행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionMutations.cs:54>): 제안 우선 / [FacilityEvolutionMutations.cs 68행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionMutations.cs:68>): UsesIdentityPressure 필수 / [FacilityEvolutionMutations.cs 108행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionMutations.cs:108>): 증거0.35
- 감사 위키 근거: [facility-growth.md 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/facility-growth.md:37>): 최대2개만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 8태그 상세 신호/결과효과 전체 미검토
- 이번 재검토: 공개 안내의 최대 2개 설명 외에 실제 선택 순서와 점수 문턱, 정체성 사용 조건이 없다.

## GAP-026 오크 기본 신체의 체력·기능 가중치 누락

- 분류: 도감 수치·부위 구성 누락
- 보완할 문서: health / entry/medical/anatomy-*
- 현재 확인한 차이: 공개 기본 신체 문서는 인간 머리18/뇌12와0.35/0.65를 설명하고 오크 묶음은 엄니·전투심장만 연결한다. 현재 오크 기본 머리22/뇌15·가중치0.3/0.7은 누락이다. 반면 슬라임 네 부위는 공개 종족 묶음과 세부 본문에 이미 모두 있으므로 누락 예시에서 삭제한다.
- **위키에 작성할 정보:**
  - 오크의 머리는 최대 체력22·의식/시야 가중치0.3, 뇌는 최대 체력15·의식 가중치0.7이며 두 부위 모두 생명 기관이다. 인간 기본 수치인 머리18·뇌12와 가중치0.35/0.65를 오크에 그대로 적용하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 12개 프로필147개 노드 전체 검증을 전제한 수치표
  - 슬라임 외막·핵·감각 젤·위족의 값이 위키에 없다는 주장
- 감사 원본 근거: [anatomy_orc.asset 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Anatomy/anatomy_orc.asset:21>): 머리22·가중치0.3·vital;33뇌15·0.7·vital / [AnatomyProfileCatalog.cs 67행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/AnatomyProfileCatalog.cs:67>): 종족별 프로필 선택
- 감사 위키 근거: [anatomy-references.json 6행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/anatomy-references.json:6>): head/brain 인간 기준18/12·0.35/0.65 / [anatomy-profile-groups.json 5행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/anatomy-profile-groups.json:5>): 오크 엄니·전투심장만;슬라임은네부위전체 / [AnatomyIndexContent.astro 51행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/src/components/AnatomyIndexContent.astro:51>): 종족 묶음 reference_ids를 실제 렌더링
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 나머지 종족 전체 기본 노드와 147개 값의 재전수는 미실시.
- 이번 재검토: 공개 기본 신체 문서는 인간 머리18/뇌12와0.35/0.65를 설명하고 오크 묶음은 엄니·전투심장만 연결한다. 현재 오크 기본 머리22/뇌15·가중치0.3/0.7은 누락이다. 반면 슬라임 네 부위는 공개 종족 묶음과 세부 본문에 이미 모두 있으므로 누락 예시에서 삭제한다.

## GAP-027 특수 기관이 기능을 전부 담당한다는 잘못된 설명

- 분류: 기능 기여율 설명 오류
- 보완할 문서: health/slime-core, fungal-hypha-core, construct-sensor-head, construct-sensor-core, construct-power-core
- 현재 확인한 차이: 특수기관 5개 설명의 '전부'는 현재 가중평균 구현과 작성 신체 구조에 모순이다. 변경된 신체 계산도 동일한 가중평균을 사용한다.
- **위키에 작성할 정보:**
  - 슬라임 핵은 핵심 기능 가중치1, 외막0.7로 핵의 비중은약58.82%이며 의식만100%다
  - 균사 중심 균핵과 골렘 동력핵은 몸체와 핵심/의식 기능을1:1로 나누고, 골렘 감응석 머리와 감응 핵도 시야를1:1로 나눈다
  - 생명 기관의 체력이0이 되면 기능 기여율과 무관하게 사망 판정한다.
- **위키에 작성하지 않을 정보:**
  - 가중치 1을 해당 기능 전체 100%로 동일시하는 설명
- 감사 원본 근거: [CharacterBodyHealthStateRules.cs 329행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterBodyHealthStateRules.cs:329>): 가중 평균 계산 / [anatomy_slime.asset 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Anatomy/anatomy_slime.asset:25>): 외막Core 가중치0.7 / [anatomy_construct.asset 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Anatomy/anatomy_construct.asset:25>): 머리/감응핵 모두 시야 가중치1 / [anatomy_fungal.asset 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Anatomy/anatomy_fungal.asset:49>): 몸통/중심균핵의공통기능
- 감사 위키 근거: [special-anatomy-references.json 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/special-anatomy-references.json:23>): 슬라임핵 전부 / [special-anatomy-references.json 29행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/special-anatomy-references.json:29>): 균핵 전부 / [special-anatomy-references.json 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/special-anatomy-references.json:34>): 감응석머리 전부 / [special-anatomy-references.json 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/special-anatomy-references.json:35>): 감응핵 전부 / [special-anatomy-references.json 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/special-anatomy-references.json:37>): 동력핵 전부
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 특수기관 5개 설명의 '전부'는 현재 가중평균 구현과 작성 신체 구조에 모순이다. 변경된 신체 계산도 동일한 가중평균을 사용한다.

## GAP-028 신체 기능 효율의 감염·이식·모듈 적용 순서

- 분류: 공식·보정 순서 누락
- 보완할 문서: health/injury-states
- 현재 확인한 차이: 개별 끝점 설명만으로 감염·거부의 곱셈, 이식 효율과 모듈 가산 순서 및 최종 가중평균을 알 수 없다. 현재 공식은 그대로다.
- **위키에 작성할 정보:**
  - 결손이면 기능0. 그 외 기능=max(0,체력비율×(1-0.5×clamp01(감염/100))×(1-0.6×clamp01(거부부담/100))×max(0,이식효율)+모듈보너스)임을 명시한다
  - 감염과 거부는 곱으로 함께 적용되고 모듈은 곱셈 뒤 더한다
  - 기능별 결과는 연결 노드 효율을 max(0.01,노드가중치)로 가중 평균한다
  - 예로 체력50%·감염100·거부100·이식효율1·모듈0이면 노드 기능10%다
  - 혈액 손실의 의식·순환 보정과 다운 판정은 이 노드 식과 구별해 참조한다
- **위키에 작성하지 않을 정보:**
  - 항목별 추가 비작성 정보 없음. 공통 기준 적용.
- 감사 원본 근거: [AnatomyModels.cs 265행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Medical/Core/AnatomyModels.cs:265>): ConditionFactor/FunctionalEfficiency / [CharacterBodyHealthStateRules.cs 337행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterBodyHealthStateRules.cs:337>): 가중치하한·정규화
- 감사 위키 근거: [anatomy-references.json 259행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/anatomy-references.json:259>): 부상 상태 끝점/합쳐 계산만
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 개별 끝점 설명만으로 감염·거부의 곱셈, 이식 효율과 모듈 가산 순서 및 최종 가중평균을 알 수 없다. 현재 공식은 그대로다.

## GAP-029 감염·이식 거부의 지속 피해와 생명기관 손상

- 분류: 위험 임계값·지속 피해 누락
- 보완할 문서: health/injury-states / medical-care-and-surgery
- 현재 확인한 차이: 현재 주민 Tick은 감염·거부의 지속 피해를 합산하며 결손 부위는 건너뛴다. publication에서 이 실제 제외 규칙을 do_not_write로 분류한 것은 의미 오류다.
- **위키에 작성할 정보:**
  - 주민의 부위 감염이40을 넘으면 초당 clamp01((감염-40)/60)×0.055, 거부부담이35를 넘으면 초당 clamp01((거부-35)/65)×0.04의 부위 피해가 발생한다
  - 두 피해를 합산하고 그35%는 전체 체력에도 적용한다
  - 이 합병증으로 생명기관 체력이0이 되면 전체 체력이 남아도 사망할 수 있으므로 단순 기능 저하와 구분해 치료 우선순위를 설명한다
  - 결손 부위는 이 합병증 피해 계산에서 제외한다.
- **위키에 작성하지 않을 정보:**
  - 주민용 합병증 식을 검증 없이 야생동물에 동일 적용하는 내용
- 감사 원본 근거: [CharacterBodyHealthRuntime.cs 213행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs:213>): 실제 Tick 호출 / [CharacterBodyHealthRuntime.cs 1508행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs:1508>): 결손 제외 / [CharacterBodyHealthRuntime.cs 1513행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs:1513>): 감염·거부 피해와35% 전체 피해 / [CharacterBodyHealthRuntime.cs 1536행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs:1536>): 생명기관 사망
- 감사 위키 근거: [anatomy-references.json 259행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/anatomy-references.json:259>): 효율저하·생명기관 파괴 일반설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 현재 주민 Tick은 감염·거부의 지속 피해를 합산하며 결손 부위는 건너뛴다. publication에서 이 실제 제외 규칙을 do_not_write로 분류한 것은 의미 오류다.

## GAP-030 업무 우선순위 화면의 실제 설정과 행동 안내의 구분

- 분류: 조작·설정값·조건 누락
- 보완할 문서: residents-and-work / work/*
- 현재 확인한 차이: 실제 버튼 순환과 자율 운반 배율·인수 배송 재개 예외가 공개 개요에 없다. 변경된 AIHaul은 운반 환경 보정과 비상 보급 경로도 별도로 가지므로 전체 AI 규칙으로 일반화하지 않는다.
- **위키에 작성할 정보:**
  - 업무 우선순위 버튼은 1→2→3→꺼짐 순서로 바뀐다.
  - 자율 운반의 점수 배율은 우선순위 1/2/3에 각각 1/0.78/0.48이다. 이미 인수한 배송은 자율 운반을 꺼도 재개 대상이며 운반 가능 조건을 다시 검사한다. 이 경우 점수는 최소 0.98이다.
- **위키에 작성하지 않을 정보:**
  - 31/42개 업무와 25개 기본값을 검증한 전체 표
  - 우선순위 배율만으로 최종 운반 점수가 확정된다는 설명
- 감사 원본 근거: [WorkPriorityLevel.cs 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Work/WorkPriorityLevel.cs:11>): 순환 / [StaffWorkPriorityPanel.cs 478행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Character/UI/StaffWorkPriorityPanel.cs:478>): UI쓰기 / [AIHaul.cs 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/Action/AIHaul.cs:39>): 인수 배송 재개 / [AIHaul.cs 64행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/Action/AIHaul.cs:64>): 운반 우선순위 배율
- 감사 위키 근거: [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 우선순위 개요
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 31/42개 목록과25개기본값미확인
- 이번 재검토: 실제 버튼 순환과 자율 운반 배율·인수 배송 재개 예외가 공개 개요에 없다. 변경된 AIHaul은 운반 환경 보정과 비상 보급 경로도 별도로 가지므로 전체 AI 규칙으로 일반화하지 않는다.

## GAP-031 비상 예비 노동량과 부족·회복 판정 기준

- 분류: 운영 지표·계산식 누락
- 보완할 문서: residents-and-work
- 현재 확인한 차이: 예비 목표식과 부족 진입/회복의 서로 다른 경계가 운영 안내에 없다.
- **위키에 작성할 정보:**
  - 기본 비상 예비 목표는 max(12 WU, 생산 가능 성인 수×3 WU, 위험별 예측 필요량의 최댓값)이다. 황색 경보는 1.5배, 적색 경보는 2배를 적용한다.
  - 예비 가용률 75% 미만은 붕괴 위험, 75~100%는 취약, 100~125%는 적정, 125% 이상은 잉여다. 회복 시에는 현재 단계별 85/110/135%를 충족해 2시간 유지해야 한 단계 개선된다.
- **위키에 작성하지 않을 정보:**
  - 모든 가용 노동·의료 예측·인구 포함 경계가 전수 검증됐다는 설명
- 감사 원본 근거: [SettlementEmergencyReservePlanner.cs 269행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementEmergencyReservePlanner.cs:269>): 목표max 계산 / [SettlementAlertRuntime.cs 600행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementAlertRuntime.cs:600>): 가용률 구간과회복경계
- 감사 위키 근거: [residents-and-work.md 155행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:155>): 긴급대응 우선순위 개요
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 가용WU contributor/의료예측/인구집계 전체 미확인
- 관련 GAP(제외 이력 포함): GAP-037
- 이번 재검토: 예비 목표식과 부족 진입/회복의 서로 다른 경계가 운영 안내에 없다.

## GAP-032 정착지 위협 경보의 발생·유지·해제 조건

- 분류: 상태 전이·경계값 누락
- 보완할 문서: invasions-and-defence
- 현재 확인한 차이: 날짜 기반 침입 경고와 별개인 정착지 경보 단계·완화 지연·의료 임계값이 공개 안내에 없다.
- **위키에 작성할 정보:**
  - 정착지 위협 경보는 녹색·황색·적색으로 구분한다. 낮아진 위협이 2게임시간 유지되면 적색→황색 또는 황색→녹색으로 한 단계 내려간다.
  - 의료 위협 집계에서 다운된 인물 2명 이상은 적색 경보 기준이다.
- **위키에 작성하지 않을 정보:**
  - 침입 날짜 예고와 정착지 위협 경보를 같은 지표로 취급하는 내용
  - 모든 사건의 상승·해제·안정 타이머 재설정을 전수 검증했다는 설명
- 감사 원본 근거: [SettlementAlertRuntime.cs 530행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementAlertRuntime.cs:530>): 2시간 단계 하향 / [SettlementThreatEventAdapter.cs 151행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementThreatEventAdapter.cs:151>): 의료 인원 경보
- 감사 위키 근거: [invasions-and-defence.md 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:11>): 자연 침입 경고만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체 사건상승/해제/타이머재설정 분기 미전수
- 이번 재검토: 날짜 기반 침입 경고와 별개인 정착지 경보 단계·완화 지연·의료 임계값이 공개 안내에 없다.

## GAP-033 비상 동원 인원·업무 중단과 평시 복귀

- 분류: 자동 동원·중단 예외·복귀 절차 누락
- 보완할 문서: residents-and-work / invasions-and-defence
- 현재 확인한 차이: 비상 대응 인원 계산과 보호 업무의 전환 제외가 일반 업무 안내에 없다. 변경된 현재 동원 경로에서도 두 조건이 남아 있다.
- **위키에 작성할 정보:**
  - 적색 동원의 목표 인원은 max(1,ceil(현재 예비 필요 WU/인당 최대 예비 대응 WU))이며 이미 배정된 인원을 뺀 만큼 추가로 선발한다.
  - 중단 불가 핵심 업무, 보호된 회복 업무, 이미 비상 대응 중인 업무는 일반 강제 전환 후보에서 제외한다.
- **위키에 작성하지 않을 정보:**
  - 모든 업무가 즉시 취소된다는 설명
  - 평시 복귀·폐기·저장 복원의 원자성이 전수 검증됐다는 내용
- 감사 원본 근거: [CharacterAlarmResponseRuntime.cs 565행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterAlarmResponseRuntime.cs:565>): 위험/예비 max 및 적색2배 / [CharacterAlarmResponseRuntime.cs 572행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterAlarmResponseRuntime.cs:572>): 목표 인원 올림 / [CharacterAlarmResponseRuntime.cs 602행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterAlarmResponseRuntime.cs:602>): 세 보호 flag 제외
- 감사 위키 근거: [invasions-and-defence.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:9>): 생산중단 준비 개요 / [residents-and-work.md 155행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:155>): 긴급대응 일반론
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체복귀 및25개업무분류 미확인
- 이번 재검토: 비상 대응 인원 계산과 보호 업무의 전환 제외가 일반 업무 안내에 없다. 변경된 현재 동원 경로에서도 두 조건이 남아 있다.

## GAP-034 수면 휴식·휴무 문서가 실제 응답 임계값과 불일치

- 분류: 현재 수치 불일치·회복 조건 오표기
- 보완할 문서: needs/sleep / residents-and-work
- 현재 확인한 차이: 현행 수면 설정 60/30/70은 구형 위키 1/35·25/55와 충돌한다. 기존 publication은 내부 클래스 체인을 공개 목록에 넣고 실제 예외 규칙을 비작성 목록으로 잘못 분류했다.
- **위키에 작성할 정보:**
  - 수면의 응답 기준은 일상 휴식 60, 긴급 휴식 30, 회복 목표 70이다.
  - 휴식 업무가 켜져 있으면 수면 30 이하에서 휴식 보호에 들어가고, 수면 70 이상이면서 최소 6게임초를 쉬어야 보호가 해제된다.
  - 일반 주민은 수면 30 이하일 때 자동 휴무에 들어갈 수 있다. 복귀에는 최소 8게임초와 수면 70·기분 45·배변 55·위생 45 이상을 모두 요구한다.
  - 사장은 일반 주민의 자동 휴무 대상에서 제외된다. 생활 욕구로 중단한 작업은 해당 욕구의 회복 목표도 충족해야 하며, 수면 회복만으로 다른 작업 제약까지 해제되지는 않는다.
- **위키에 작성하지 않을 정보:**
  - 내부 클래스·호출 체인·구형 프리팹 필드명
  - 구형 수면 1/35 및 25/55 수치를 현행 규칙으로 소개하는 내용
- 감사 원본 근거: [SurvivalBalanceSettings.asset 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Survival/SurvivalBalanceSettings.asset:35>): 수면60/30/70 / [WorkDutyController.cs 119행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkDutyController.cs:119>): 휴식보호 / [WorkDutyController.cs 317행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkDutyController.cs:317>): 복귀 gate / [AbilityWork.cs 351행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Ability/AbilityWork.cs:351>): 수면 응답 emergency/resume 소비
- 감사 위키 근거: [need-references.json 62행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/need-references.json:62>): sleep상충임계 / [residents-and-work.md 144행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:144>): 수면55복귀
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 이번 재검토: 현행 수면 설정 60/30/70은 구형 위키 1/35·25/55와 충돌한다. 기존 publication은 내부 클래스 체인을 공개 목록에 넣고 실제 예외 규칙을 비작성 목록으로 잘못 분류했다.

## GAP-035 일상 작업의 기분 저하 중단 기준

- 분류: 작업 상태 전환·시간·명령 예외 누락
- 보완할 문서: residents-and-work / work/*
- 현재 확인한 차이: 기분 18% 작업 중단은 실제 분기이며 공개 안내에 없다. 교대 45/4는 작성 기본값만 확인되어 유효 프리팹·전체 gate 확인 전 확정 규칙으로 쓰지 않는다.
- **위키에 작성할 정보:**
  - 일상 작업 중 기분이 최대치의 18% 이하이면 작업을 중단한다.
- **위키에 작성하지 않을 정보:**
  - 프리팹 유효값·교대 gate를 확인하지 않고 모든 일상 업무가 45초 교대·4초 대기를 따른다고 쓰는 내용
  - 모든 중단 이후 자동 복귀가 보장된다는 내용
- 감사 원본 근거: [WorkDutyController.cs 615행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkDutyController.cs:615>): 기분 .18 이하 작업중단 / [AbilityWork.cs 38행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Ability/AbilityWork.cs:38>): 교대/대기 작성 기본값, 공개 확정 제외
- 감사 위키 근거: [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 교대라는말만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 45초 교대·4초 대기의 프리팹 유효값 및 전체 실행 gate 미검증
- 관련 GAP(제외 이력 포함): GAP-034
- 이번 재검토: 기분 18% 작업 중단은 실제 분기이며 공개 안내에 없다. 교대 45/4는 작성 기본값만 확인되어 유효 프리팹·전체 gate 확인 전 확정 규칙으로 쓰지 않는다.

## GAP-036 시공·대형 사업의 동시 인원과 작업자별 기여 감소

- 분류: 공동 작업 정원·효율 계산 누락
- 보완할 문서: residents-and-work / spaces / research
- 현재 확인한 차이: 정원과 추가 인원의 기여 감소 곡선이 공개 안내에 없다. 규모별 계약이며 개별 연구 작성값 전수와는 구별한다.
- **위키에 작성할 정보:**
  - 시공 규모별 최대 인원은 소형 2, 중형 3, 산업 4, 대형 사업 6, 랜드마크 8명이다. 연구 규모별 기본 인원은 일반 1, 협력 2, 대규모 4명이다.
  - 일반 프로젝트의 순번별 기여 배율은 1/0.85/0.75/0.65/0.55/0.45/0.40/0.35, 연구는 1/0.70/0.45/0.25다.
- **위키에 작성하지 않을 정보:**
  - 180개 연구가 모두 특정 인원 설정임을 검증한 표
  - UI 예상 단축 시간이 이 배율만으로 항상 계산된다는 설명
- 감사 원본 근거: [SettlementLaborBalanceRules.cs 46행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementLaborBalanceRules.cs:46>): 기여곡선 / [SettlementLaborBalanceRules.cs 154행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementLaborBalanceRules.cs:154>): 규모별정원 / [ProjectWorkforceRuntime.cs 143행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/ProjectWorkforceRuntime.cs:143>): 등록정원 일치검증
- 감사 위키 근거: [residents-and-work.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:13>): 스킬/우선순위 개요
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 180개연구·UI기여수식 미전수
- 이번 재검토: 정원과 추가 인원의 기여 감소 곡선이 공개 안내에 없다. 규모별 계약이며 개별 연구 작성값 전수와는 구별한다.

## GAP-037 운영 화면의 노동 WU 회계와 30일 인당 노동 지수

- 분류: 운영 지표·공식·집계 범위 누락
- 보완할 문서: residents-and-work
- 현재 확인한 차이: 운영 화면의 산출·실현·보장 성장 차이 및 완료일 기반 중앙값 계산이 공개 안내에 없다.
- **위키에 작성할 정보:**
  - 전환 가능한 산출 WU=max(0,실제 노동+공정 환산 산출-손실)이고 산출 등가 WU는 여기에 자동화 WU를 더한다.
  - 실현 성장 WU=max(0,전환 가능 산출-필수 유지-장비·시설 유지), 보장 성장 WU=max(0,실현 성장-비상 예비 목표)다.
  - 일별 인당 순노동 지수는 실현 성장 WU를 생산 가능 성인 수와 기준 인당 유효 WU로 나눈다. 완료된 최근 30일 이내의 기록으로 중앙값을 구하며, 짝수 개면 가운데 두 값의 평균을 쓴다.
- **위키에 작성하지 않을 정보:**
  - 자동화 산출을 실현/보장 성장에도 그대로 더한다는 설명
  - 모든 유지·손실 생산자가 빠짐없이 연결됐다는 설명
- 감사 원본 근거: [SettlementLaborAccountingRuntime.cs 205행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementLaborAccountingRuntime.cs:205>): 산출/성장식 / [SettlementLaborAccountingRuntime.cs 364행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementLaborAccountingRuntime.cs:364>): 완료일 인당지수 / [SettlementLaborAccountingRuntime.cs 386행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/SettlementLaborAccountingRuntime.cs:386>): 중앙값
- 감사 위키 근거: [residents-and-work.md 155행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:155>): 회계지표 설명없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 유지채널·손실·자동화producer 미전수
- 관련 GAP(제외 이력 포함): GAP-031, GAP-038
- 이번 재검토: 운영 화면의 산출·실현·보장 성장 차이 및 완료일 기반 중앙값 계산이 공개 안내에 없다.

## GAP-038 일반 손님 영입의 이민 정책과 정착지 수용 심사

- 분류: 정책·수치·영입 제한·0값 예외 누락
- 보완할 문서: guests-services-and-performance
- 현재 확인한 차이: 일반 영입의 정책별 수용 기준과 지표 0일 때 검사 예외가 공개 안내에 없다.
- **위키에 작성할 정보:**
  - 보수적/균형/개방 정책의 비축 기준은 각각 30/14/7일, 위생 위험 상한 25/40/60, 질병 위험 상한 20/35/50, 예비 가용률 하한 1.25/1/0.85, 인당 노동 지수 하한 1/0.9/0.8이다.
  - 미치료 인물이 있거나 확정 적색 경보이면 일반 영입 완료를 거부한다. 인당 노동 지수와 최근 보장 성장 WU가 0이면 그 지표의 하한 검사를 건너뛰며, 양수일 때는 각각 정책 하한과 3 WU를 검사한다.
- **위키에 작성하지 않을 정보:**
  - 위 기준만으로 침상·모든 인구 계수와 모든 영입 유형이 허용된다고 단정하는 내용
- 감사 원본 근거: [SettlementPopulationCapacityRuntime.cs 144행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Recruitment/SettlementPopulationCapacityRuntime.cs:144>): 0값조건제외 / [SettlementPopulationCapacityRuntime.cs 166행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Recruitment/SettlementPopulationCapacityRuntime.cs:166>): 정책별한계
- 감사 위키 근거: [guests-services-and-performance.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:41>): 방문만족과10일만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 주거계수·양쪽호출·UIcycle 미전수
- 관련 GAP(제외 이력 포함): GAP-039
- 이번 재검토: 일반 영입의 정책별 수용 기준과 지표 0일 때 검사 예외가 공개 안내에 없다.

## GAP-039 단골·일반 영입 후보가 되는 방문 횟수와 만족도

- 분류: 획득 조건·누적 계산·상태 유지 누락
- 보완할 문서: guests-services-and-performance
- 현재 확인한 차이: 공개 영입 안내에는 2회·65점과 후보 상태 유지 규칙이 없다. 변경된 GameplayScene의 현재 작성값도 2/65다.
- **위키에 작성할 정보:**
  - 현행 시작 씬의 단골 및 일반 영입 후보 기준은 누적 방문 2회 이상·평균 만족도 65 이상이다. 조건을 한 번 만족해 켜진 단골/후보 상태는 이후 기록 갱신에서 자동으로 꺼지지 않는다.
- **위키에 작성하지 않을 정보:**
  - 방문 횟수를 입장일 수 또는 모든 시설 이용 횟수라고 단정하는 내용
- 감사 원본 근거: [GameplayScene.unity 7620행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scenes/GameplayScene.unity:7620>): 단골/후보 2회65점 / [DungeonRegularCustomerSaveData.cs 213행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Recruitment/Core/DungeonRegularCustomerSaveData.cs:213>): 누적방문및상태유지
- 감사 위키 근거: [guests-services-and-performance.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:41>): 방문만족의정확한값없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 방문event/MOOD producer 미확인
- 관련 GAP(제외 이력 포함): GAP-038
- 이번 재검토: 공개 영입 안내에는 2회·65점과 후보 상태 유지 규칙이 없다. 변경된 GameplayScene의 현재 작성값도 2/65다.

## GAP-040 후발 영입자의 숙련·레벨 보정과 원정 진행 기준

- 분류: 성장 보정 수치·선정 방식 누락
- 보완할 문서: guests-services-and-performance
- 현재 확인한 차이: 원정 목표별 숙련 하한과 별도 레벨 보정·방문 보너스가 공개 영입 설명에 없다.
- **위키에 작성할 정보:**
  - 원정 완료 목표 수가 0/1/2/3/4개 이상이면 영입자의 상위 두 숙련 XP 하한은 0/100/250/400/600이다.
  - 별도 레벨 하한은 1/18/32/44/최대레벨이다. 방문 3회 이상이고 완료 목표도 3개 이상이면 레벨 하한에 2를 더하되 최대레벨을 넘지 않는다.
- **위키에 작성하지 않을 정보:**
  - 숙련 두 개만 보정되고 레벨은 보정되지 않는다는 설명
  - XP 학습 배율 우회와 최대레벨 수치를 미검증 상태로 단정하는 내용
- 감사 원본 근거: [RecruitedCharacterActivationService.cs 170행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Recruitment/RecruitedCharacterActivationService.cs:170>): 자동기술·숙련/레벨보정 / [RecruitedCharacterActivationService.cs 245행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Recruitment/RecruitedCharacterActivationService.cs:245>): 레벨하한 / [RecruitedCharacterActivationService.cs 270행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Recruitment/RecruitedCharacterActivationService.cs:270>): 방문가산 / [DungeonRegularCustomerSaveData.cs 33행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Recruitment/Core/DungeonRegularCustomerSaveData.cs:33>): XP하한
- 감사 위키 근거: [guests-services-and-performance.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:41>): 두전문숙련만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 최대레벨50정의·XP학습배율우회 미검토
- 이번 재검토: 원정 목표별 숙련 하한과 별도 레벨 보정·방문 보너스가 공개 영입 설명에 없다.

## GAP-041 비전 색인철의 연구 보조 효과와 내구도 소비

- 분류: 아이템 실제 용도·효과·소모 조건 누락
- 보완할 문서: research / entry/item/record-arcane-index
- 현재 확인한 차이: 아이템 가격은 현재 양쪽49로 같고 누락이 아니다. 실제 연구 효과와 마모 규칙이 빠져 있으며 기존 publication의 이력 연결 지시는 공개 원고가 아니다.
- **위키에 작성할 정보:**
  - 연구 시설에 비전 색인철 1개가 준비되면 승인 연구 WU에 1.1배 효과를 적용하며 승인 WU당 내구도 0.01을 소비한다. 도구 공급이 준비되지 않으면 기본 연구 WU로 진행한다.
- **위키에 작성하지 않을 정보:**
  - 가격이 원본49·도감44라는 오래된 비교
  - 감사 GAP 번호·가격 철회 이력·코드 클래스명을 공개 본문에 넣는 내용
  - 실게임 소모·교체·저장 왕복을 실행 검증했다고 쓰는 내용
- 감사 원본 근거: [ResearchArcaneIndexEquipmentPolicySource.cs 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/ResearchArcaneIndexEquipmentPolicySource.cs:31>): 도구1/효과1.1/마모.01 / [ResearchWorkExecutionAdapter.cs 433행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs:433>): 미공급 기본작업 / [ResearchWorkExecutionAdapter.cs 448행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs:448>): 승인WU당 마모 / [ResearchWorkExecutionAdapter.cs 534행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs:534>): 효과 배율 소비
- 감사 위키 근거: [research.md 16행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/research.md:16>): 시설/보관일반안내 / [record-arcane-index.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/record-arcane-index.json:2>): 기본facts만 / [record-arcane-index.json 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/record-arcane-index.json:13>): 현재 기준가격49, 불일치 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 연구효과의 정적 연결 확인; 실게임 물리도구 소모/교체 왕복 미실행
- 관련 GAP(제외 이력 포함): GAP-042
- 이번 재검토: 아이템 가격은 현재 양쪽49로 같고 누락이 아니다. 실제 연구 효과와 마모 규칙이 빠져 있으며 기존 publication의 이력 연결 지시는 공개 원고가 아니다.

## GAP-043 연구 180개의 시설 기능 요구와 제공 시설 수치 누락

- 분류: 조건·도감 필드 누락
- 보완할 문서: research / entry/research/* / entry/facility/*
- 현재 확인한 차이: 공개 연구의 시설 수용력이라는 표현에는 기능별 정착지 합산과 가동 조건이 빠져 있다.
- **위키에 작성할 정보:**
  - 연구 시설 기능은 사용 가능한 연구 역할 방에 있고 활성·비파손 상태인 시설들에서 기능별로 정착지 전체 합산한다. 자체 완결 방은 제외하며 전력을 쓰는 시설은 가동 전력이 필요하다.
  - 각 기능 기여는 작성 수용량의 최소 1을 더하며 연구가 요구하는 기능별 수용량과 비교한다.
- **위키에 작성하지 않을 정보:**
  - 연구180개·시설8개 수치와 연구실16 도감 대응을 현재 전수 확인했다는 내용
- 감사 원본 근거: [ResearchFacilityCapacityAdapter.cs 155행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/ResearchFacilityCapacityAdapter.cs:155>): 기능합산 / [ResearchFacilityCapacityAdapter.cs 197행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/ResearchFacilityCapacityAdapter.cs:197>): 가동gate
- 감사 위키 근거: [research.md 44행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/research.md:44>): 시설수용력일반표현
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 180개필요수량/8시설수치및16공개대응미전수
- 이번 재검토: 공개 연구의 시설 수용력이라는 표현에는 기능별 정착지 합산과 가동 조건이 빠져 있다.

## GAP-044 필수·선행 우회 청사진과 물리 보관 조건 설명 누락

- 분류: 시스템·실행 조건 누락
- 보완할 문서: research / entry/facility/facility-blueprint-* / entry/item/research-blueprint-*
- 현재 확인한 차이: 필수/선행 우회의 서로 다른 gate와 구매→실물 보관이라는 조건이 공개 연구 안내에 없다.
- **위키에 작성할 정보:**
  - 필수 청사진 연구는 선행 연구 완료와 실물 청사진의 보관을 모두 요구한다.
  - 선행 우회 청사진은 보관 중이면 선행 연구를 건너뛰지만 시설 기능 요구는 면제하지 않는다. 청사진 구매는 실물 배송이므로 구매 자체가 즉시 연구 완료를 뜻하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 180개 연구/7종 청사진의 전체 분류 수와 보관 질량·버퍼 수치를 확정하는 내용
- 감사 원본 근거: [BlueprintResearchProjectCoordinator.cs 207행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchProjectCoordinator.cs:207>): Required/Shortcutgate / [BlueprintResearchRuntime.cs 643행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs:643>): 실물청사진배송
- 감사 위키 근거: [research.md 16행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/research.md:16>): 보관조건만언급
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 보관질량/7종·180개집계/시설buffer세부미전수
- 관련 GAP(제외 이력 포함): GAP-045, GAP-046
- 이번 재검토: 필수/선행 우회의 서로 다른 gate와 구매→실물 보관이라는 조건이 공개 연구 안내에 없다.

## GAP-045 연구 대기열 제거 시 진척 보존과 다음 연구 선택

- 분류: 조작·상태 전이 설명 누락
- 보완할 문서: research
- 현재 확인한 차이: 공개 연구 안내는 대기열 제거 시 진척 보존과 다음 활성 연구 선택을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 연구를 대기열에서 제거해도 이미 쌓인 연구 진척은 보존된다. 제거 후에는 남아 있는 대기열에서 다음으로 실행 가능한 연구를 다시 선택한다.
- **위키에 작성하지 않을 정보:**
  - 드래그·잠금 표시·의존 연구 자동 등록 전체를 확인했다고 쓰는 내용
- 감사 원본 근거: [BlueprintResearchProjectCoordinator.cs 93행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchProjectCoordinator.cs:93>): 제거후진척보존 / [BlueprintResearchProjectCoordinator.cs 207행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchProjectCoordinator.cs:207>): 조건검사
- 감사 위키 근거: [research.md 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/research.md:57>): 연구항목안내만 / [milestones-runs-and-meta.md 24행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/milestones-runs-and-meta.md:24>): 큐진행저장만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 드래그/잠금UI/의존자동등록전체미검증
- 관련 GAP(제외 이력 포함): GAP-044
- 이번 재검토: 공개 연구 안내는 대기열 제거 시 진척 보존과 다음 활성 연구 선택을 설명하지 않는다.

## GAP-046 비전 기초 청사진(6104)의 즉시 해금 설명 오류

- 분류: 원본 실행과 본문 불일치·관계 누락
- 보완할 문서: entry/facility/facility-blueprint-* / entry/item/research-blueprint-* / research
- 현재 확인한 차이: 6104 도감의 즉시 해금 설명은 실물 배송 및 대상 연구 진행 경로와 충돌한다.
- **위키에 작성할 정보:**
  - 비전 기초 청사진(6104)은 구매 시 실물 청사진이 하차장에 배송된다. 관련 연구의 선행·보관·시설 조건에 따라 연구를 진행하며 구매만으로 즉시 해금되는 것이 아니다.
- **위키에 작성하지 않을 정보:**
  - 나머지 13문서와 청사진7종의 분류를 전수 재확인했다는 내용
  - 원본 설명 오류 조사 및 감사 관리 메모를 공개 위키에 싣는 내용
- 감사 원본 근거: [BlueprintResearchRuntime.cs 638행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs:638>): 구매시실물배송 / [BP_ArcaneBasics.asset 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Blueprint/P1/BP_ArcaneBasics.asset:31>): 대상연구ID / [BlueprintResearchProjectCoordinator.cs 223행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchProjectCoordinator.cs:223>): 필수보관검사
- 감사 위키 근거: [research-blueprint-6104.json 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/research-blueprint-6104.json:18>): 즉시해금서사 / [research-blueprint-6104.json 24행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/research-blueprint-6104.json:24>): 즉시해금요약
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 7종14문서전수미완료
- 관련 GAP(제외 이력 포함): GAP-044
- 이번 재검토: 6104 도감의 즉시 해금 설명은 실물 배송 및 대상 연구 진행 경로와 충돌한다.

## GAP-047 기억 잔재의 도감 분석·지역 정찰 비용과 처리 순서 누락

- 분류: 시스템·수치·중단 조건 누락
- 보완할 문서: research / expeditions / entry/item/captivity-memory-residue
- 현재 확인한 차이: 공개 설명은 연구·정찰용이라는 용도만 있어 작업량과 정보망 약화 효과·중복/포화 gate가 없다. 정찰+10의 실제 대상은 intelligenceDamage다.
- **위키에 작성할 정보:**
  - 기억 잔재 처리의 필요 작업량은 24 WU다. 지역 정찰 결과는 해당 지역의 정보망 약화 값을 10만큼 높인다.
  - 정보망 약화가 이미 99.999 이상인 지역은 새 정찰 작업을 받지 않는다. 같은 지역의 대기 정찰이 이미 있어도 중복 예약을 거부한다.
- **위키에 작성하지 않을 정보:**
  - 정찰 효과를 알 수 없는 별도 '정찰 점수+10'으로 표현하는 내용
  - 8개 단서 순서·수송 취소·실물 반환·재배정 전체를 검증했다고 쓰는 내용
- 감사 원본 근거: [KnowledgeResidueProcessingRuntime.cs 52행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs:52>): 24WU / [KnowledgeResidueProcessingRuntime.cs 156행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs:156>): 포화거부 / [KnowledgeResidueProcessingRuntime.cs 831행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/KnowledgeResidueProcessingRuntime.cs:831>): 정찰10효과
- 감사 위키 근거: [factions-contracts-and-prisoners.md 142행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:142>): 연구/정찰용도만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 8단서·일반연구우선·실물반환·재배정·UI미전수
- 이번 재검토: 공개 설명은 연구·정찰용이라는 용도만 있어 작업량과 정보망 약화 효과·중복/포화 gate가 없다. 정찰+10의 실제 대상은 intelligenceDamage다.

## GAP-048 금단의 도약 확률·진척량·사용 횟수·후유증 누락

- 분류: 특성 사용법·수치·예외 누락
- 보완할 문서: research / entry/character/trait-302
- 현재 확인한 차이: 특성의 시도 개요에 비해 인물·프로젝트별 횟수, 확률, 후유증 기간·배율이 빠져 있다.
- **위키에 작성할 정보:**
  - 금단의 도약은 인물별로 같은 연구 프로젝트에서 한 번만 시도할 수 있다. 판정 확률은 돌파 10%, 퇴보 20%, 변화 없음 70%다.
  - 시도 뒤 1일의 후유증이 남고 해당 연구 효과 배율은 0.7이다.
- **위키에 작성하지 않을 정보:**
  - 전체 연구 WU의 +25%/-10%가 실제로 적용되는 소비까지 검증됐다는 설명
  - UI 첫 보유자 선택 등 모든 사용 제약을 확정하는 내용
- 감사 원본 근거: [ExtremeTraitRuntime.cs 224행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs:224>): 인물프로젝트1회·후유증·hash / [Trait_302_forbidden-leap.asset 54행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_302_forbidden-leap.asset:54>): 0.7효과 / [Trait_302_forbidden-leap.asset 66행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_302_forbidden-leap.asset:66>): 확률/진척작성값
- 감사 위키 근거: [trait-302.json 1행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-302.json:1>): 효과개수와개요만 / [research.md 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/research.md:34>): 일일연구기준만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제총WU적용/첫UI보유자선택미검증
- 이번 재검토: 특성의 시도 개요에 비해 인물·프로젝트별 횟수, 확률, 후유증 기간·배율이 빠져 있다.

## GAP-049 연구 작업시간과 승인 작업량 보정 공식 누락

- 분류: 공식·적용 순서·기술 구분 누락
- 보완할 문서: research / residents-and-work
- 현재 확인한 차이: 공개 연구는 하루 기준 WU를 설명하지만 승인 WU에 연구 모듈 합을 적용하는 식이 없다.
- **위키에 작성할 정보:**
  - 연구 승인 WU를 A, 연구 보너스 모듈 합을 S라 하면 모듈 적용 후 작업량은 A×(1+S/4)다. 이는 하루 기준 연구량과 별도의 작업 기여 보정이다.
- **위키에 작성하지 않을 정보:**
  - 5/18 작성 모듈 값·모든 시간 보정·메타/잔재 분기와 UI99 제한을 검증했다고 쓰는 내용
- 감사 원본 근거: [BlueprintResearchContracts.cs 368행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchContracts.cs:368>): 승인WU정규화 / [CharacterSkillRuntimeEffects.cs 277행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs:277>): research모듈합보너스 / [ResearchProgressRules.cs 8행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Research/Core/ResearchProgressRules.cs:8>): 정규화4
- 감사 위키 근거: [research.md 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/research.md:34>): 45/50일일기준만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 5/18·메타/잔재·UI99·시간배율미전수
- 관련 GAP(제외 이력 포함): GAP-036, GAP-041, GAP-050
- 이번 재검토: 공개 연구는 하루 기준 WU를 설명하지만 승인 WU에 연구 모듈 합을 적용하는 식이 없다.

## GAP-050 계승 강화 9종의 구매·효과·기록 보존 규칙 누락

- 분류: 시스템·가격·최대레벨·저장 범위 누락
- 보완할 문서: milestones-runs-and-meta
- 현재 확인한 차이: 현행9종 authored 정의는 가격·레벨·효과가 그대로이고 공개 계승 안내에는 정확한 표가 없다. 사장 효과의 생성 특성 수/상한과 적용 예외를 구체화하고 감사 메모를 제거한다.
- **위키에 작성할 정보:**
  - 계승 강화는 재화가 충분하고 최대 레벨 미만일 때 가격을 지불하고 1레벨 오른다.
  - 시작 시설 후보 +1: 가격80/최대2레벨; 사장 생성 특성 목표수·상한 +1:90/1; 첫날 기본 구매 목록 +1:100/3; 특수 조합식 기록 보존 슬롯 +1:120/3.
  - 사장 최대체력 레벨당 +8%:110/3; 침입 경고 문턱 레벨당 -8%:90/2; 상단 보급 비용 레벨당 -4%:100/3; 방어 시설 구매가 레벨당 -5%:110/3; 연구 작업량 레벨당 +8%:110/3.
  - 사장 특성 강화는 직원에게 적용되지 않으며, 후보 충돌·부족 때문에 실제 특성이 항상 정확히 1개 더 생긴다고 보장할 수 없다. 특수 조합식 기록 강화는 다음 런에 보존할 수 있는 슬롯을 늘린다.
- **위키에 작성하지 않을 정보:**
  - 사장 강화를 후보 수 증가 또는 미연결 효과라고 소개하는 내용
  - 침입 후보 문턱까지 내려가는 동작의 설계 의도를 확정하는 내용
  - 실게임 구매/다음 런 저장 왕복을 완료했다고 쓰는 내용
- 감사 원본 근거: [GameDomainContentCatalog.asset 2444행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/GameDomainContentCatalog.asset:2444>): 9종 정의:80/2,90/1,100/3,120/3,110/3,90/2,100/3,110/3,110/3; 정수+1 및 +.08/-.08/-.04/-.05/+.08 / [AuthoredGameplayCatalog.cs 302행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/AuthoredGameplayCatalog.cs:302>): 정의에서 효과 객체를 생성 / [MetaProgressionModel.cs 134행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Meta/Core/MetaProgressionModel.cs:134>): 정의조회·최대레벨/재화 거부·가격차감 및1레벨 / [MetaProgressionEffects.cs 148행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Meta/Core/MetaProgressionEffects.cs:148>): 효과레벨 합산 및 배율 하한 .05 / [MetaProgressionRuntime.cs 62행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Core/MetaProgressionRuntime.cs:62>): 구매 성공 이벤트 / [OperationsFeatureSurfacePresenter.cs 351행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/UI/Core/OperationsFeatureSurfacePresenter.cs:351>): 계승 강화 UI에서 구매 명령 / [RunStartVariableSelector.cs 70행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/RunStartVariableSelector.cs:70>): 시작시설 후보 기본3+보너스 / [DungeonPreparationLifetimeScope.cs 84행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/DungeonPreparationLifetimeScope.cs:84>): 시작준비에 프로필 특성보너스 query 등록 / [StartPartyPreparationService.cs 180행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs:180>): 프로필 사장 특성보너스 query 실제 소비 / [StartPartyPreparationService.cs 505행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs:505>): 사장만 bonus 전달; 직원0 / [StartPartyPreparationService.cs 1013행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/StartPartyPreparationService.cs:1013>): 선택기의 최대4+bonus와 생성bonus에 적용 / [CharacterTraitSelectionRules.cs 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterTraitSelectionRules.cs:37>): 실제 목표수=min(최대,무작위특성수+bonus), 이후 충돌필터
- 감사 위키 근거: [milestones-runs-and-meta.md 28행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/milestones-runs-and-meta.md:28>): 프로필 계승을 이정표로만 위임; 9종 구매/수치 없음 / [starting-party.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/starting-party.md:9>): 시작 구성 안내에는 계승 사장특성 개수 보정 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 9종 정적 정의·consumer 연결 검증 완료. 실게임 구매/다음런 저장왕복은 미실행; 침입후보문턱 하락의 설계의도는 구현만으로 확정 불가
- 이번 재검토: 현행9종 authored 정의는 가격·레벨·효과가 그대로이고 공개 계승 안내에는 정확한 표가 없다. 사장 효과의 생성 특성 수/상한과 적용 예외를 구체화하고 감사 메모를 제거한다.

## GAP-051 런 종료 계승 재화의 산식과 집계 기준 누락

- 분류: 결산 공식·입력 의미·상한 누락
- 보완할 문서: milestones-runs-and-meta
- 현재 확인한 차이: 공개 계승 문서에는 현재 계산기의 보상 항목·상한·위협 점수 및 반올림 산식이 없다. 입력 집계의 의미는 미검증으로 남긴다.
- **위키에 작성할 정보:**
  - 런 종료 계승 재화는 다음 합에 난이도 배율(최소0.1)을 곱하고 정수 반올림한 뒤 최소0으로 제한한다: 생존초÷60×8 + 생존 운영일×35 + 결산 횟수×10 + 방어 성공×30 + 최대 위협단계 점수×12 + 최종 침입위협×0.25 + min(60,첫 시설발견 수×4) + min(60,첫 조합식해금 수×10) + 원정성공 수×25.
  - 위협단계 점수는 Warning=1, Candidate/Safety=2, 그 외0이다. 각 횟수 항목은 음수를0으로 제한한다.
- **위키에 작성하지 않을 정보:**
  - 운영일·결산·최대위협 집계 생산자 정의나 현재 씬 난이도0.65/1/1.45를 전수 확인한 것처럼 쓰는 내용
  - 같은 런 결과의 저장 왕복까지 검증됐다는 내용
- 감사 원본 근거: [MetaProgressionCalculator.cs 14행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Meta/Core/MetaProgressionCalculator.cs:14>): 보상항목·상한·난도곱 / [MetaProgressionCalculator.cs 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Meta/Core/MetaProgressionCalculator.cs:27>): 위협단계점수
- 감사 위키 근거: [milestones-runs-and-meta.md 24행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/milestones-runs-and-meta.md:24>): 런종료결산만설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 계산caller·producer집계·씬난도·저장동일성미전수
- 이번 재검토: 공개 계승 문서에는 현재 계산기의 보상 항목·상한·위협 점수 및 반올림 산식이 없다. 입력 집계의 의미는 미검증으로 남긴다.

## GAP-052 자동화 모드별 작업 권한과 전환·생산 조건 누락

- 분류: 모드·작업자·주문 조건 누락
- 보완할 문서: infrastructure / production
- 현재 확인한 차이: 공개 자동화 개요에는 모드 권한·예약 및 재료 대기 주문 재시도 규칙이 없다.
- **위키에 작성할 정보:**
  - 자동화는 수동·보조·자동 모드로 구분하며 모드 전환 때 수동 작업 예약을 검사한다.
  - 자동 모드는 작업자 예약이 없는 준비됨·진행 중·재료 대기 주문을 대상으로 한다. 재료 대기 주문도 시작 검사를 다시 실행해 입력 요청을 갱신하며, 재료를 확보한 주문 하나에 한 번의 작업을 제출한다.
- **위키에 작성하지 않을 정보:**
  - 재료 대기 주문을 자동화 대상에서 제외하는 낡은 상태 목록
  - 모든 공정 lane과 실제 UI 순환을 전수 검증했다고 쓰는 내용
- 감사 원본 근거: [AutomationRuntime.cs 236행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/AutomationRuntime.cs:236>): 세종류수동예약검사 / [AutomationRuntime.cs 397행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/AutomationRuntime.cs:397>): WaitingForMaterials포함 / [AutomationRuntime.cs 408행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/AutomationRuntime.cs:408>): 재료준비BeginWork
- 감사 위키 근거: [infrastructure.md 44행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:44>): 모드와예약개요만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든공정lane및UI순환미전수
- 이번 재검토: 공개 자동화 개요에는 모드 권한·예약 및 재료 대기 주문 재시도 규칙이 없다.

## GAP-053 자동화의 정비·고장·작업 속도 공식과 대기 중 소모 누락

- 분류: 수치·상태 전이·시간 단위 누락
- 보완할 문서: infrastructure
- 현재 확인한 차이: 대기 중 정비·저정비 고장·속도 보정은 공개 개요에 없고 기존 작성 범위는 실제 식을 누락했다. WU하한0.01과 상태 상수까지 현재 코드로 구체화했다.
- **위키에 작성할 정보:**
  - 전력이 있는 비수동 모드에서는 주문이 없어 대기 중이어도 정비가 감소한다. 정비 감소량은 시간당 작성 소모×이정표 정비배율(0.1~1)×경과 게임시간이다.
  - 감소 후 정비 M이25 이하이면 고장 F가 (25-M)×0.006×경과초만큼 늘며 0~100에 제한한다. F가100이면 자동 가동을 멈춘다.
  - 정비 배율은 M≥60이면1, 그 아래는0.45~1 선형 보간이다. 고장 배율은 F=0~100에서1~0.35 선형 보간이며 두 값의 곱을0.1~1에 제한한다.
  - 자동 제출 WU=max(0.01,작성 초당WU)×상태 배율×경과초다.
- **위키에 작성하지 않을 정보:**
  - 대기 중에는 정비가 소모되지 않는다는 설명
  - 예제 및 저장 왕복까지 검증됐다는 내용
- 감사 원본 근거: [AutomationRuntime.cs 356행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/AutomationRuntime.cs:356>): 주문전정비/고장증가 / [AutomationRuntime.cs 392행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/AutomationRuntime.cs:392>): 소모뒤주문검사 / [AutomationRuntime.cs 422행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/AutomationRuntime.cs:422>): 자동WU최소0.01 / [AutomationRuntime.cs 81행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/AutomationRuntime.cs:81>): 조건배율
- 감사 위키 근거: [infrastructure.md 55행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:55>): 보조1.35/시간정비1만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 저장 왕복과 시간 진행 예제는 미실행
- 이번 재검토: 대기 중 정비·저정비 고장·속도 보정은 공개 개요에 없고 기존 작성 범위는 실제 식을 누락했다. WU하한0.01과 상태 상수까지 현재 코드로 구체화했다.

## GAP-054 자동화 정비의 시작·종료 기준과 수리 WU 효과 누락

- 분류: 수리 조건·비용·우선순위 누락
- 보완할 문서: infrastructure / work/repair
- 현재 확인한 차이: 자동화 정비의 진입·필요 WU·회복 효과가 공개 안내에 없다.
- **위키에 작성할 정보:**
  - 자동화 정비는 정비 M<85 또는 고장 F>0.01인 시설이 대상이다. 남은 정비 WU는 max(85-M,2F)다.
  - 승인 정비 1 WU마다 정비가1 증가하고 고장이0.5 감소한다. 목표 정비와 고장 해소 조건을 충족하면 완료한다.
- **위키에 작성하지 않을 정보:**
  - 다른 수리 종류까지 포함한 전체 작업 우선순위·긴급도식
- 감사 원본 근거: [RepairWorkExecutionHandler.cs 440행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/Work/RepairWorkExecutionHandler.cs:440>): 잔여WU와정비명령 / [RepairWorkExecutionHandler.cs 514행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/Work/RepairWorkExecutionHandler.cs:514>): 85/F0.01후보 / [AutomationCoreModels.cs 221행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Automation/Core/AutomationCoreModels.cs:221>): 정비효과
- 감사 위키 근거: [infrastructure.md 44행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:44>): 수리후보일반설명 / [infrastructure.md 67행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:67>): 재가동일반론
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 수리종류전체순서/긴급도식미검증
- 이번 재검토: 자동화 정비의 진입·필요 WU·회복 효과가 공개 안내에 없다.

## GAP-055 대장 작업대 자동화 모드·전력·품질·정비 설정 누락

- 분류: 도감 필드 누락
- 보완할 문서: facility entries / infrastructure
- 현재 확인한 차이: 대장 작업대 도감에는 분류·크기만 있고7개 자동화 설정이 없다. 확인된 시설 한 곳의 정확한 값으로 작성 문안을 한정한다.
- **위키에 작성할 정보:**
  - 대장 작업대의 최대 모드는 자동이다. 보조 모드 전력2·작업배율1.35, 자동 모드 전력5·초당WU1·자동 품질상한75점이며 정비 소모는 게임시간당1이다.
- **위키에 작성하지 않을 정보:**
  - 27개 시설189필드의 전수 재검증 완료라는 표현
  - 자동 품질상한에 실제 소비자가 없다는 설명
- 감사 원본 근거: [S08_대장작업대.asset 203행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/S08_대장작업대.asset:203>): 7개자동화필드 / [ProductionAssemblyBridgeAdapter.cs 117행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionAssemblyBridgeAdapter.cs:117>): 품질상한현재소비
- 감사 위키 근거: [building-1019.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1019.json:2>): 분류/크기만 / [production-quality-and-supply.md 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:57>): 시설수치에위임
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 27개189필드전수미재검증
- 관련 GAP(제외 이력 포함): GAP-052, GAP-053, GAP-056
- 이번 재검토: 대장 작업대 도감에는 분류·크기만 있고7개 자동화 설정이 없다. 확인된 시설 한 곳의 정확한 값으로 작성 문안을 한정한다.

## GAP-056 자동 품질 상한의 설계 범위를 현재 적용 수치로 안내

- 분류: 설계·작성값·실행 미확인 혼동
- 보완할 문서: production-quality-and-supply / infrastructure
- 현재 확인한 차이: 공개 자동 품질 설명은 설계 범위만 제시해 현재 시설값과 혼합 기여 적용 조건을 알 수 없다. 변경된 생산 품질 경로에도 실제 상한 소비가 있다.
- **위키에 작성할 정보:**
  - 자동 작업이 참여한 생산은 시설의 자동 품질상한을 적용한다. 대장 작업대의 작성값0.75는 품질75점이다.
  - 전체 완료 WU에서 수동 기여 WU를 뺀 값이 max(0.001,완료WU×0.000001)를 넘으면 자동 참여로 판정한다. 수동만 참여한 경우 이 자동 상한은100점이다.
- **위키에 작성하지 않을 정보:**
  - 자동 품질상한에 소비자가 없다는 설명
  - 모든 시설의 실제 상한이 설계범위0.50~0.90 또는 일괄75점이라는 설명
- 감사 원본 근거: [ProductionAssemblyBridgeAdapter.cs 116행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionAssemblyBridgeAdapter.cs:116>): 작성상한×100전달 / [ProductionAutomaticQualityRules.cs 29행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionAutomaticQualityRules.cs:29>): 자동참여WU있으면상한적용 / [ProductionBillRuntime.cs 1391행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs:1391>): 출력 품질에서 자동상한 결정
- 감사 위키 근거: [infrastructure.md 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:57>): 0.50~0.90 / [production-quality-and-supply.md 55행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:55>): 시설별범위처럼설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 27개작성값전수·장비별출력검증은미수행
- 관련 GAP(제외 이력 포함): GAP-055
- 이번 재검토: 공개 자동 품질 설명은 설계 범위만 제시해 현재 시설값과 혼합 기여 적용 조건을 알 수 없다. 변경된 생산 품질 경로에도 실제 상한 소비가 있다.

## GAP-057 시설 구조 내구도·균열과 수리 시작·회복 기준 누락

- 분류: 수치·조건·공통 규칙 누락
- 보완할 문서: infrastructure / invasions-and-defence / work:repair
- 현재 확인한 차이: 구조 균열·손상과 구조 수리의 정확한 경계가 공개 개요에 없다. 기존2대1 결함 후보는 사실과 구분해 제한으로 남긴다.
- **위키에 작성할 정보:**
  - 구조 체력이 최대의75% 초과이면 균열 없음,50% 초과~75% 이하이면 잔금,25% 초과~50% 이하이면 균열,25% 이하이면 심각한 균열이다. 파괴되지 않은 시설도 체력이50% 이하이면 손상 상태로 판정한다.
  - 구조 수리는 현재 구조 HP를 시설의 최대HP까지 회복하는 작업이며 시설별 수리 HP/WU를 구분해 표시한다.
- **위키에 작성하지 않을 정보:**
  - 2HP/WU와1HP/WU의 차이를 재현된 현재 결함으로 단정하는 내용
  - 삭제·복원 원자성을 확인했다고 쓰는 내용
- 감사 원본 근거: [BuildingStructuralIntegrityRuntime.cs 247행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingStructuralIntegrityRuntime.cs:247>): damaged50% / [BuildingStructuralIntegrityRuntime.cs 337행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingStructuralIntegrityRuntime.cs:337>): 균열3단계 / [RepairWorkExecutionHandler.cs 204행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/Work/RepairWorkExecutionHandler.cs:204>): 남은구조WU계산및승인
- 감사 위키 근거: [invasions-and-defence.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:40>): 구조내구도감소개요 / [infrastructure.md 67행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:67>): 재가동개요
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 2대1WU차이·삭제성공·복원클램프미검증
- 이번 재검토: 구조 균열·손상과 구조 수리의 정확한 경계가 공개 개요에 없다. 기존2대1 결함 후보는 사실과 구분해 제한으로 남긴다.

## GAP-058 구조 모듈 시설12개와 기본 벽·문3개의 도감 수치60개 누락

- 분류: 도감 필드 누락
- 보완할 문서: facility entries / infrastructure
- 현재 확인한 차이: 현재 활성 구조모듈은12개다. 기존10개 외 대형보관선반·전기 제련 도가니가 추가되어 기본벽·문3개 포함15개60필드가 된다. 두 추가 도감에도 구조값이 없다.
- **위키에 작성할 정보:**
  - 구조 모듈 작성 시설12개와 기본 벽·문3개를 구분한다. 표시 값은 최대HP/강도/수리HP·WU/돌파 가능 여부다.
  - 강화 낙하문450/24/2, 기본 랜드마크5개1400/38/3, 후기 랜드마크4개2400/60/3, 대형보관선반112/8/7, 전기 제련 도가니148/12/2다.
  - 기본 내벽300/18/2, 내벽 문120/8/2, 문220/14/2이며 이15개는 모두 돌파 가능이다.
- **위키에 작성하지 않을 정보:**
  - 13개/52필드를 현재 전체 모집단이라고 표기하는 내용
  - 복도에 기본 구조값이 생긴다는 이유로 침입자 공격 대상이라고 확장하는 내용
  - 랜드마크 영문 작성 메모 조사 등 감사 운영 내용을 공개하는 내용
- 감사 원본 근거: [DefenseLinkedDropGate.asset 92행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/P1/DefenseLinkedDropGate.asset:92>): {"breachable":"1","toughness":"24","maxHitPoints":"450","repairHitPointsPerWork":"2"} / [L01_대형보관선반.asset 185행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/L01_대형보관선반.asset:185>): {"breachable":"1","toughness":"8","maxHitPoints":"112","repairHitPointsPerWork":"7"} / [I16_전기_제련_도가니.asset 175행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I16_전기_제련_도가니.asset:175>): {"breachable":"1","toughness":"12","maxHitPoints":"148","repairHitPointsPerWork":"2"} / [building_landmark_accord-hall.asset 104행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_accord-hall.asset:104>): {"breachable":"1","toughness":"38","maxHitPoints":"1400","repairHitPointsPerWork":"3"} / [building_landmark_arcane-spire.asset 106행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_arcane-spire.asset:106>): {"breachable":"1","toughness":"60","maxHitPoints":"2400","repairHitPointsPerWork":"3"} / [building_landmark_lineage-vault.asset 104행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_lineage-vault.asset:104>): {"breachable":"1","toughness":"60","maxHitPoints":"2400","repairHitPointsPerWork":"3"} / [building_landmark_sealed-garden.asset 104행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_sealed-garden.asset:104>): {"breachable":"1","toughness":"38","maxHitPoints":"1400","repairHitPointsPerWork":"3"} / [building_landmark_sovereign-citadel.asset 104행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_sovereign-citadel.asset:104>): {"breachable":"1","toughness":"38","maxHitPoints":"1400","repairHitPointsPerWork":"3"} / [building_landmark_steel-colossus.asset 106행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_steel-colossus.asset:106>): {"breachable":"1","toughness":"60","maxHitPoints":"2400","repairHitPointsPerWork":"3"} / [building_landmark_surface-gate.asset 104행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_surface-gate.asset:104>): {"breachable":"1","toughness":"38","maxHitPoints":"1400","repairHitPointsPerWork":"3"} / [building_landmark_temporal-sanctum.asset 106행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_temporal-sanctum.asset:106>): {"breachable":"1","toughness":"60","maxHitPoints":"2400","repairHitPointsPerWork":"3"} / [building_landmark_truth-observatory.asset 104행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_truth-observatory.asset:104>): {"breachable":"1","toughness":"38","maxHitPoints":"1400","repairHitPointsPerWork":"3"} / [BuildableObject.cs 403행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildableObject.cs:403>): authored 모듈이 없을 때 기본벽·문 구조 생성 / [GameDomainContentCatalog.asset 264행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/GameDomainContentCatalog.asset:264>): 대형보관선반 등록
- 감사 위키 근거: [building-7.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-7.json:2>): 내벽: facts 분류/크기뿐; HP/강도/수리/파괴가능 없음 / [building-8.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8.json:2>): 내벽 문: facts 분류/크기뿐; HP/강도/수리/파괴가능 없음 / [building-0.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-0.json:2>): 복도: facts 분류/크기뿐; HP/강도/수리/파괴가능 없음 (복도는52필드 집계 제외) / [building-1.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1.json:2>): 문: facts 분류/크기뿐; HP/강도/수리/파괴가능 없음 / [building-1804.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1804.json:2>): 문 연동 강화 낙하문: facts 분류/크기뿐; HP/강도/수리/파괴가능 없음 / [building-landmark-sealed-garden.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-sealed-garden.json:2>): 봉인 생태정원: facts 분류/크기뿐; HP/강도/수리/파괴가능 없음 / [building-1050.json 10행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1050.json:10>): 분류·크기와 건설 요약만 / [building-9825.json 10행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9825.json:10>): 구조 설정 미표시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 작성12개·기본3개의 정적 정의 대조. 모든 시설의 건설·침입·수리·저장 실입력은 미실행
- 관련 GAP(제외 이력 포함): GAP-057
- 이번 재검토: 현재 활성 구조모듈은12개다. 기존10개 외 대형보관선반·전기 제련 도가니가 추가되어 기본벽·문3개 포함15개60필드가 된다. 두 추가 도감에도 구조값이 없다.

## GAP-059 침입자의 구조물 피해·강도 보정·격노와 공격 간격 공식 누락

- 분류: 전투 수치·조건 누락
- 보완할 문서: invasions-and-defence
- 현재 확인한 차이: 구조 피해와 강도 반감·격노 효과·막힘 문턱은 공개 침입 안내에 없다. 변경된 실행기에도3초·0.65 규칙은 남아 있다.
- **위키에 작성할 정보:**
  - 침입자의 기본 구조 피해는 근접 명중 성능×5×0.75와 근접 위력 성능×5×0.45를 합한 뒤 전투·근접·구조 피해 배율을 적용하며 최소1이다.
  - 최종 구조 피해=max(1,기본 구조피해-max(0,강도)×0.5)이며 격노 중에는1.25배다. 막힘이3초 이어지면 격노하며 공격 간격에0.65를 곱한다.
- **위키에 작성하지 않을 정보:**
  - 모든 접근 시점·공격속도 하한·저장 재개가 검증됐다는 설명
- 감사 원본 근거: [InvasionIntruderCombatRules.cs 17행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Invasion/InvasionIntruderCombatRules.cs:17>): 성능기반구조피해 / [InvasionIntruderCombatRules.cs 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Invasion/InvasionIntruderCombatRules.cs:37>): 강도/격노 / [InvasionIntruderExecutionCoordinator.cs 553행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Invasion/InvasionIntruderExecutionCoordinator.cs:553>): 3초 격노 / [InvasionIntruderExecutionCoordinator.cs 598행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Invasion/InvasionIntruderExecutionCoordinator.cs:598>): 공격간격 .65
- 감사 위키 근거: [invasions-and-defence.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:40>): 구조피해일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 간격최소/공격속도·저장·접근시간전체미검증
- 이번 재검토: 구조 피해와 강도 반감·격노 효과·막힘 문턱은 공개 침입 안내에 없다. 변경된 실행기에도3초·0.65 규칙은 남아 있다.

## GAP-060 돌파 경로의 선택 비용·공격 칸 예약·우회로 전환 조건 누락

- 분류: AI 경로·조건 누락
- 보완할 문서: invasions-and-defence
- 현재 확인한 차이: 실제 돌파 경로의 비용과 외부 차단 제외가 공개 침입 안내에 없다.
- **위키에 작성할 정보:**
  - 돌파 경로 비용에는 구조 HP+강도×3+예상 공격 횟수×35와 위험도×30이 반영된다. 외부 차단으로 표시된 칸은 돌파 후보에서 제외한다.
- **위키에 작성하지 않을 정보:**
  - 정상길 우선·다칸 공격 예약·길이 열린 뒤 전환의 전체 실행을 검증했다고 쓰는 내용
- 감사 원본 근거: [DefenseBreachPlanner.cs 551행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Invasion/Core/DefenseBreachPlanner.cs:551>): 돌파비용/외부차단제외 / [DefenseBreachPlanner.cs 451행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Invasion/Core/DefenseBreachPlanner.cs:451>): 공격칸예약메서드
- 감사 위키 근거: [invasions-and-defence.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:40>): 경로비교일반론
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 정상길우선/다칸예약/실제전환caller미전수
- 관련 GAP(제외 이력 포함): GAP-059
- 이번 재검토: 실제 돌파 경로의 비용과 외부 차단 제외가 공개 침입 안내에 없다.

## GAP-061 전력망 연결·우선 공급·최소 가동 비율과 저장 설명 누락·오류

- 분류: 조건 누락·저장 설명 오류
- 보완할 문서: infrastructure
- 현재 확인한 차이: 공개 infrastructure의 생산/수요/공급 저장 서술은 실제 저장 필드와 다르다. 현행 노드별 전달 배분과 최소 가동 조건의 설명도 없다.
- **위키에 작성할 정보:**
  - 전력 공급은 연결망의 실제 전달 경로와 시설별 요청량·최소 공급 비율을 고려해 배분한다. 연결을 껐거나 차단됐거나 공급 비율이 최소 기준에 못 미친 시설은 가동 전력을 받지 못한다.
  - 저장되는 전력 상태는 시설별 연결·우선순위·축전량·남은 연료 시간·열·고장·차단 상태 및 미완료 연료 처리 정보다. 생산량·수요량·공급률은 연결망에서 다시 계산하는 값이다.
- **위키에 작성하지 않을 정보:**
  - 전체 망에 남은 전력만 min으로 나누면 현행 공급을 완전히 설명한다는 내용
  - 공개 원문처럼 생산·수요·공급 요약 수치 자체가 저장된다고 쓰는 내용
  - 모든 연결/UI 제한을 전수 검증한 표
- 감사 원본 근거: [ElectricalNetworkRuntime.cs 293행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:293>): 저장 필드 / [ElectricalNetworkRuntime.cs 916행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:916>): 노드별 flow 할당 / [ElectricalNetworkRuntime.cs 920행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:920>): 연결·차단·최소비율 가동
- 감사 위키 근거: [infrastructure.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:40>): 생산/수요/공급 결과가 저장된다고 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든 연결/우선순위/UI·시설별 최소값 미전수
- 관련 GAP(제외 이력 포함): GAP-080
- 이번 재검토: 공개 infrastructure의 생산/수요/공급 저장 서술은 실제 저장 필드와 다르다. 현행 노드별 전달 배분과 최소 가동 조건의 설명도 없다.

## GAP-062 발전기 연료의 실제 소비·현장 버퍼·가동시간과 고장 보정 누락

- 분류: 발전 조건·수치·물류 누락
- 보완할 문서: infrastructure / facility producer entries
- 현재 확인한 차이: 현장 실물 연료 공급 및 고장에 따른 발전량 저하가 공개 연료 개요에 없다. 변경된 발전 경로의 추가 용량 배율은 생략한 단일 최종 발전식으로 단정하지 않는다.
- **위키에 작성할 정보:**
  - 연료 발전기는 지정된 현장 목적지에 실물 연료가 공급돼 소비 처리되어야 가동한다. 남은 연료 시간이 소진되면 다음 연료1개를 요구한다.
  - 작성 발전량에는 clamp01(1-고장/125) 보정이 적용되고, 현재 망의 추가 용량 배율도 적용된다. 연료가 필요 없는 발전기에는 작성 연료ID·시간을 비용으로 적용하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 원격 재고 숫자만 있으면 즉시 연료가 소비된다는 설명
  - 모든 발전기의 개별 작성량·연료 저장 복구를 실행 검증했다고 쓰는 내용
- 감사 원본 근거: [ElectricalNetworkRuntime.cs 861행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:861>): 고장 보정 후 capacityMultiplier / [ElectricalNetworkRuntime.cs 1073행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:1073>): 무연료 모드 예외 / [ElectricalNetworkRuntime.cs 1098행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:1098>): 현장 연료 commit / [ElectricalNetworkRuntime.cs 1114행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:1114>): 연료1개 배송요청
- 감사 위키 근거: [infrastructure.md 36행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:36>): 발전/연료 연결의 개요만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 3종 작성값·실제 연료 저장왕복 미전수
- 관련 GAP(제외 이력 포함): GAP-065
- 이번 재검토: 현장 실물 연료 공급 및 고장에 따른 발전량 저하가 공개 연료 개요에 없다. 변경된 발전 경로의 추가 용량 배율은 생략한 단일 최종 발전식으로 단정하지 않는다.

## GAP-063 축전지 용량·충방전 한도·양방향 손실과 이정표 보정 누락

- 분류: 에너지 저장 수식·조건 누락
- 보완할 문서: infrastructure / facility storage entry
- 현재 확인한 차이: 양방향 손실·용량·실사용량 정산이 공개 위키에 없다. 기존 publication은 손실/용량 문장까지 삭제 대상으로 잘못 분류했으므로 실제 수치와 식을 복구한다.
- **위키에 작성할 정보:**
  - 축전지의 작성 용량은240, 초당 충·방전 한도30, 효율0.92다. 실제 방전에 사용한 출력량만큼 저장량에서 출력×시간/효율을 뺀다.
  - 충전 입력 요청은 min(빈 용량/(시간×효율),초당 이송한도)이며 저장 증가량은 실제 입력×시간×효율이다. 같은 틱에 방전한 축전지는 충전하지 않는다.
  - 이정표는 잔여 손실에 곱해 효율을 보정한다: 최종 효율=1-(1-작성 효율)×손실 배율(0~1).
- **위키에 작성하지 않을 정보:**
  - 배분 전에 항상 강제 방전하거나 가득 찬 배터리가 입력을 버린다는 낡은 결함 주장
  - 전체 네트워크에서 정격30이 항상 실공급30을 보장한다는 설명
- 감사 원본 근거: [I04_축전지.asset 124행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I04_축전지.asset:124>): 240/30/0.92 작성값 / [ElectricalNetworkRuntime.cs 931행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:931>): 실사용 출력 정산 / [ElectricalNetworkRuntime.cs 943행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:943>): 방전 제외 및 빈공간 충전 제한 / [ElectricalNetworkRuntime.cs 1326행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:1326>): 이정표 손실 보정
- 감사 위키 근거: [infrastructure.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:31>): 전력·축전 일반 개요
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체 전력망 경로·이정표 적용 예제 시뮬레이션 미실행
- 관련 GAP(제외 이력 포함): GAP-065
- 이번 재검토: 양방향 손실·용량·실사용량 정산이 공개 위키에 없다. 기존 publication은 손실/용량 문장까지 삭제 대상으로 잘못 분류했으므로 실제 수치와 식을 복구한다.

## GAP-064 전력 과부하·열·고장 누적과 망 전체 차단·복구 기준 누락

- 분류: 고장 수식·복구 조건 누락
- 보완할 문서: infrastructure
- 현재 확인한 차이: 공개 전력 안내에는 정량 열·고장·차단·복구 규칙이 없고 기존 문안은 이미 고쳐진 전력 없음 처리를 오독했다.
- **위키에 작성할 정보:**
  - 과부하 비율은 수요/(발전량+실사용 축전지 방전량)이다. 공급이0.001 이하이면 비율0으로 취급한다.
  - 비율이1보다 크면 열이 초당(비율-1)×18 늘고, 그렇지 않으면 초당8 감소한다. 전력이 있는 상태에서 열이75를 넘으면 고장이 초당(열-75)×0.02 늘며0~100에 제한된다.
  - 비율이 차단기 허용률(max(1,작성값))을 넘고 열이 차단열(max(1,작성값)) 이상이면 차단한다. 수동 복구는 열60 미만에서 가능하다.
- **위키에 작성하지 않을 정보:**
  - 가짜0.01 분모·방전 제외·정전이나 차단 중 지속 가열이라는 낡은 설명
  - 복구불능이 재현된 결함이라는 표현
- 감사 원본 근거: [ElectricalNetworkRuntime.cs 967행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:967>): 발전+방전량으로 과부하 / [ElectricalNetworkRuntime.cs 1297행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:1297>): 무전력 비율0 / [ElectricalNetworkRuntime.cs 1302행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:1302>): 열·고장 / [ElectricalNetworkRuntime.cs 1316행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:1316>): 차단조건 / [ElectricalNetworkRuntime.cs 259행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:259>): 열60 복구 제한
- 감사 위키 근거: [infrastructure.md 63행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:63>): 망별 고장 범위 / [infrastructure.md 67행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:67>): 복구 개요만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 차단기/제어반 두 작성값·실제 복구 UI 왕복 미전수
- 이번 재검토: 공개 전력 안내에는 정량 열·고장·차단·복구 규칙이 없고 기존 문안은 이미 고쳐진 전력 없음 처리를 오독했다.

## GAP-065 전력 시설75개의 도감 수치197개와 대체·비사용29개 구분 누락

- 분류: 도감 필드 누락·작성값과 적용값 구분
- 보완할 문서: facility entries / infrastructure
- 현재 확인한 차이: 현재 전력모듈75개/226선택필드다. 태양등·인공태양 소비6필드가 추가돼 직접 적용197·모드대체27·미사용2로 바뀌었다.75개 공개 도감 모두 facts는 분류/크기뿐이다.
- **위키에 작성할 정보:**
  - 증기·수차·마나 발전기의 기본 초당 발전량은18·10·32다. 증기는 저급 연료1개당60초, 마나는 마나 수정1개당90초 가동하며 수차는 연료가 필요 없다. 축전지는 용량240·초당 이송30·기본 효율0.92다.
  - 소비시설69개의 기본 우선순위는 중요2·필수9·생산58개, 최소 공급 비율은1이25개·0.5가15개·0.75가29개다.
  - 태양등은 전력4, 인공태양은12를 요구하며 둘 다 필수 우선순위·최소 공급 비율1이다.
  - 자동화 시설27개의 작성 소비5는 모드별 소비 프로필로 대체된다. 수차의 연료ID·시간은 무연료 모드에서 쓰지 않는다.
  - 회로 차단기의 과부하 허용비/차단 열은1.15/100, 변압 제어반은1.3/130이다.
- **위키에 작성하지 않을 정보:**
  - 이전73개/220필드를 현재 전체로 소개하는 내용
  - 모드에 가려진27개 수요5를 고정 소비량으로 소개하는 내용
  - 내부 감사 파일명·필드 집계 수를 게임 기능 설명 대신 나열하는 내용
- 감사 원본 근거: [SR16_자동 급배수 제어기.asset 152행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ServiceRooms/SR16_자동 급배수 제어기.asset:152>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"1"} / [SR15_자동 객실 배정판.asset 152행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ServiceRooms/SR15_자동 객실 배정판.asset:152>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"1"} / [SR06_자동 계산대.asset 152행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ServiceRooms/SR06_자동 계산대.asset:152>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"1"} / [SR05_보온 배식대.asset 152행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ServiceRooms/SR05_보온 배식대.asset:152>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"1"} / [WS28_실내_생장_제어기.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS28_실내_생장_제어기.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS26_마나_응축기.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS26_마나_응축기.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS25_무균_약품_보관함.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS25_무균_약품_보관함.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS23_마나_안정기.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS23_마나_안정기.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS20_정밀_연마기.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS20_정밀_연마기.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS18_연기_포집_후드.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS18_연기_포집_후드.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS12_냉장_준비대.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS12_냉장_준비대.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS10_전기_오븐.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS10_전기_오븐.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS07_분별_증류탑.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS07_분별_증류탑.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS06_세척_병입대.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS06_세척_병입대.asset:130>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [WS04_온도_제어_발효조.asset 131행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS04_온도_제어_발효조.asset:131>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"1"} / [E11_공조기.asset 181행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E11_공조기.asset:181>): {"demandPerSecond":"10","priority":"3","minimumSupplyFraction":"0.75"} / [E10_냉각기.asset 176행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E10_냉각기.asset:176>): {"demandPerSecond":"8","priority":"3","minimumSupplyFraction":"0.75"} / [D03_조리손질대.asset 88행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D03_조리손질대.asset:88>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [D02_고기그릴.asset 200행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D02_고기그릴.asset:200>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [D01_간이화덕.asset 197행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D01_간이화덕.asset:197>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [G06_전리품거치대.asset 171행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/G06_전리품거치대.asset:171>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [E14_배기팬.asset 160행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E14_배기팬.asset:160>): {"demandPerSecond":"5","priority":"3","minimumSupplyFraction":"0.5"} / [E13_송풍구.asset 160행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E13_송풍구.asset:160>): {"demandPerSecond":"3","priority":"3","minimumSupplyFraction":"0.5"} / [P14_증류기.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P14_증류기.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P13_퇴비장.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P13_퇴비장.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P12_무두질대.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P12_무두질대.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P11_직조기.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P11_직조기.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P10_비전단조대.asset 92행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P10_비전단조대.asset:92>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P09_귀금세공대.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P09_귀금세공대.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P08_제강로.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P08_제강로.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P07_용광로.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P07_용광로.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P06_광석선별대.asset 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P06_광석선별대.asset:99>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P05_석재절단대.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P05_석재절단대.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P04_숯가마.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P04_숯가마.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P03_제재소.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P03_제재소.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P02_양조장.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P02_양조장.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P01_제분소.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P01_제분소.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [S08_대장작업대.asset 161행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/S08_대장작업대.asset:161>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P25_폐기소각로.asset 166행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P25_폐기소각로.asset:166>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P21_대장간.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P21_대장간.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P20_몽직기.asset 92행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P20_몽직기.asset:92>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P19_연금대.asset 104행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P19_연금대.asset:104>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P18_약제대.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P18_약제대.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P17_사료배합대.asset 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P17_사료배합대.asset:91>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P16_훈연대.asset 85행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P16_훈연대.asset:85>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [P15_조리대.asset 85행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P15_조리대.asset:85>): {"demandPerSecond":"5","priority":"4","minimumSupplyFraction":"0.75"} / [I20_인공태양.asset 134행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I20_인공태양.asset:134>): {"demandPerSecond":"12","priority":"3","minimumSupplyFraction":"1"} / [I19_태양등.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I19_태양등.asset:130>): {"demandPerSecond":"4","priority":"3","minimumSupplyFraction":"1"} / [I17_룬_조율실.asset 134행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I17_룬_조율실.asset:134>): {"demandPerSecond":"9","priority":"4","minimumSupplyFraction":"1"} / [I16_전기_제련_도가니.asset 132행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I16_전기_제련_도가니.asset:132>): {"demandPerSecond":"7","priority":"4","minimumSupplyFraction":"1"} / [I15_전기_아크등.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I15_전기_아크등.asset:130>): {"demandPerSecond":"1.5","priority":"3","minimumSupplyFraction":"1"} / [I13_룬_정화_시설.asset 147행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I13_룬_정화_시설.asset:147>): {"demandPerSecond":"4","priority":"3","minimumSupplyFraction":"1"} / [I12_소독_정수기.asset 143행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I12_소독_정수기.asset:143>): {"demandPerSecond":"4","priority":"3","minimumSupplyFraction":"1"} / [I10_물통_충전소.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I10_물통_충전소.asset:128>): {"demandPerSecond":"1.5","priority":"1","minimumSupplyFraction":"1"} / [I07_전동_양수_펌프.asset 120행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I07_전동_양수_펌프.asset:120>): {"demandPerSecond":"4","priority":"1","minimumSupplyFraction":"1"} / [I06_변압_제어반.asset 121행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I06_변압_제어반.asset:121>): {"tripHeat":"130","overloadTolerance":"1.3"} / [I05_회로_차단기.asset 121행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I05_회로_차단기.asset:121>): {"tripHeat":"100","overloadTolerance":"1.15"} / [I04_축전지.asset 121행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I04_축전지.asset:121>): {"efficiency":"0.92","transferPerSecond":"30","capacity":"240"} / [I03_마나_발전기.asset 125행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I03_마나_발전기.asset:125>): {"productionPerSecond":"32","secondsPerFuel":"90","requiresFuel":"1","fuelItemId":"resource:mana-crystal"} / [I02_수차_발전기.asset 123행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I02_수차_발전기.asset:123>): {"productionPerSecond":"10","secondsPerFuel":"60","requiresFuel":"0","fuelItemId":"material:low-fuel"} / [I01_증기_발전기.asset 121행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I01_증기_발전기.asset:121>): {"productionPerSecond":"18","secondsPerFuel":"60","requiresFuel":"1","fuelItemId":"material:low-fuel"} / [C10_고속_컨베이어.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C10_고속_컨베이어.asset:128>): {"demandPerSecond":"1","priority":"4","minimumSupplyFraction":"0.5"} / [C09_오버플로_배출_게이트.asset 131행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C09_오버플로_배출_게이트.asset:131>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C08_층간_물류_리프트.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C08_층간_물류_리프트.asset:128>): {"demandPerSecond":"0.4","priority":"4","minimumSupplyFraction":"0.5"} / [C07_우선순위_게이트.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C07_우선순위_게이트.asset:128>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C06_컨베이어_필터.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C06_컨베이어_필터.asset:128>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C05_컨베이어_합류기.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C05_컨베이어_합류기.asset:128>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C04_컨베이어_분배기.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C04_컨베이어_분배기.asset:128>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C03_컨베이어_출력기.asset 129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C03_컨베이어_출력기.asset:129>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C02_컨베이어_입력기.asset 129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C02_컨베이어_입력기.asset:129>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C01U_컨베이어_상향.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C01U_컨베이어_상향.asset:128>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C01R_컨베이어_우향.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C01R_컨베이어_우향.asset:128>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C01L_컨베이어_좌향.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C01L_컨베이어_좌향.asset:128>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [C01D_컨베이어_하향.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C01D_컨베이어_하향.asset:128>): {"demandPerSecond":"0.5","priority":"4","minimumSupplyFraction":"0.5"} / [A01_자동화_제어반.asset 119행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/A01_자동화_제어반.asset:119>): {"demandPerSecond":"3","priority":"4","minimumSupplyFraction":"1"} / [ElectricalNetworkRuntime.cs 916행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ElectricalNetworkRuntime.cs:916>): 작성 최소공급비율 소비 / [GameDomainContentCatalog.asset 261행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/GameDomainContentCatalog.asset:261>): I19/I20 root 등록
- 감사 위키 근거: [building-1715.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1715.json:2>): 자동 급배수 제어기: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1714.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1714.json:2>): 자동 객실 배정판: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1705.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1705.json:2>): 자동 계산대: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1704.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1704.json:2>): 보온 배식대: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1609.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1609.json:2>): 전기 오븐: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1606.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1606.json:2>): 분별 증류탑: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-9828.json 10행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9828.json:10>): 전력 역할만, 전력4·우선순위·최소비율 미표시 / [building-9829.json 10행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9829.json:10>): 전력 역할만, 전력12·우선순위·최소비율 미표시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 75개 현재 작성모듈·도감 정적대조. 모든 개별 생산/생활효과 및 저장 실동작은 미실행
- 관련 GAP(제외 이력 포함): GAP-061, GAP-062, GAP-063, GAP-064
- 이번 재검토: 현재 전력모듈75개/226선택필드다. 태양등·인공태양 소비6필드가 추가돼 직접 적용197·모드대체27·미사용2로 바뀌었다.75개 공개 도감 모두 facts는 분류/크기뿐이다.

## GAP-066 급수망의 수질별 사용 순서·공유 저장 공간·사용 실패 조건 누락

- 분류: 수질·소비 조건 누락
- 보완할 문서: infrastructure
- 현재 확인한 차이: 현재 소비·용량 코드는 기존 단일 수질 전량 충족과 공유 용량을 유지한다. 원장의 UI 문장은 검증 범위 밖인데 write에 들어가 있어 제거한다.
- **위키에 작성할 정보:**
  - 급수망의 깨끗한 물·비음용수·오염수는 별도 재고이지만 세 양의 합이 상수 저장 용량을 사용한다.
  - 최소 수질이 깨끗한 물이면 그 물만, 비음용수이면 비음용수→깨끗한 물, 오염수이면 오염수→비음용수→깨끗한 물 순서로 찾는다.
  - 한 요청은 한 수질의 재고만으로 전량을 충족해야 한다. 비음용수3과 깨끗한 물2가 있어도5의 단일 요청은 실패한다.
- **위키에 작성하지 않을 정보:**
  - 산업 패널의 수질별 표시와 개별 시설 최소 수질을 이 검토로 전수 승인하는 내용
  - 상수 채널의 모든 물이 음용수라는 설명
- 감사 원본 근거: [FluidNetworkRuntime.cs 247행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:247>): 한 수질씩 요청 전량 비교 / [FluidNodeState.cs 105행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Core/FluidNodeState.cs:105>): 최소수질별 순서 / [FluidNetworkRuntime.cs 2318행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:2318>): 세 수질 합산 저장공간
- 감사 위키 근거: [infrastructure.md 32행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:32>): 유체 개요만 / [infrastructure.md 36행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:36>): 수질별 소비 규칙 없음
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 산업 UI 표시·개별 생활시설 값은 이 판정 범위 밖
- 이번 재검토: 현재 소비·용량 코드는 기존 단일 수질 전량 충족과 공유 용량을 유지한다. 원장의 UI 문장은 검증 범위 밖인데 write에 들어가 있어 제거한다.

## GAP-067 급수 생산·폐수 처리의 처리량·속도 보정·정지 조건 누락

- 분류: 처리 수식·출력·시설 조건 누락
- 보완할 문서: infrastructure
- 현재 확인한 차이: 현재 급수·폐수 공통 실행은 전력·양쪽망·입출력 용량으로 진행을 gate하지만 공개 위키는 처리/정지 조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 급수 생산은 유효 생산량·필요 전력·상수망 연결을 갖췄을 때 작성 생산량×현재 유량 배율×경과시간만큼 생산한다.
  - 폐수 처리기는 필요한 전력, 폐수망과 상수망 연결, 입력 폐수 전량, 출력 저장 여유를 모두 갖춰야 진척이 쌓인다. 처리시간은 최소0.1초로 제한하며 진척은 유량 배율을 적용해 쌓고 완료 배치만큼 입력을 빼고 출력을 더한다.
- **위키에 작성하지 않을 정보:**
  - 모든 시설 작성 처리량과0.5초 호출 순서를 전수 검증했다는 내용
- 감사 원본 근거: [FluidNetworkRuntime.cs 1987행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:1987>): 물 생산 조건·유량 / [FluidNetworkRuntime.cs 2034행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:2034>): 폐수 양쪽망·입출력용량 / [FluidNetworkRuntime.cs 2052행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:2052>): 처리 진척과 배치 정산
- 감사 위키 근거: [infrastructure.md 32행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:32>): 상하수도 개요 / [infrastructure.md 67행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:67>): 정비 개요
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 4종 작성값·0.5초 틱순서 미재검증
- 관련 GAP(제외 이력 포함): GAP-069
- 이번 재검토: 현재 급수·폐수 공통 실행은 전력·양쪽망·입출력 용량으로 진행을 gate하지만 공개 위키는 처리/정지 조건을 설명하지 않는다.

## GAP-068 하수 역류·막힘·누수와 배관 업무의 실제 수리 공식 누락

- 분류: 상태 전이·노동 비용 누락 및 업무 설명 정정
- 보완할 문서: infrastructure / work plumbing
- 현재 확인한 차이: 수리량·현장 누수와 만수 역류의 수치 공백이 남아 있다. 현재 유체 런타임에서 식은 유지된다.
- **위키에 작성할 정보:**
  - 배관 정비 작업량은8+막힘×0.25+누수×0.3 WU다.
  - 누수 손실은 min(해당 노드 총물량,누수×0.001×경과초×계량 보정)이며0.02를 넘게 새면 누수량×2의 하수 오물을 오염값0.25로 만든다.
  - 폐수망이 용량에 가깝게 가득 차면 역류를 발생시킨다. 역류는 막힘을 max(1,역류량×2)만큼 늘리고0~100에 제한한다.
- **위키에 작성하지 않을 정보:**
  - 누수가 자연적으로 발생하지 않는다고 단정하는 내용
  - 모든 UI 평균·등록 실행기 연결을 전수 확인했다는 내용
- 감사 원본 근거: [PlumbingWorkExecutionHandler.cs 85행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/PlumbingWorkExecutionHandler.cs:85>): 수리 WU / [FluidNetworkRuntime.cs 2094행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:2094>): 누수량과 오물 / [FluidNetworkRuntime.cs 2125행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:2125>): 만수 역류 / [FluidNetworkRuntime.cs 2163행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:2163>): 역류 막힘 증가
- 감사 위키 근거: [infrastructure.md 67행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:67>): 누수·압력·수리 개요만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 자연 누수 원인·UI 산술평균·등록 실행기 전체 caller 미전수
- 이번 재검토: 수리량·현장 누수와 만수 역류의 수치 공백이 남아 있다. 현재 유체 런타임에서 식은 유지된다.

## GAP-069 급수·저장·정수·물통 시설7개의 도감 설정34개와 역할 설명 누락

- 분류: 도감 작성 필드·관계 누락
- 보완할 문서: facility entries / infrastructure
- 현재 확인한 차이: 7개 도감 facts는 분류/크기뿐이고 현재 작성 처리량·용량·병입 역할을 알 수 없다. 정확히 재독한 수치만 공개 표에 넣는다.
- **위키에 작성할 정보:**
  - 전동 양수 펌프는 깨끗한 물을 초당0.75 생산한다. 상수 탱크는 상수120, 오수 탱크는 폐수140을 저장한다.
  - 물통 충전소는 배치당 물1, 기준4초, 병입 목표 재고10이다.
  - 오수 침전조는 폐수10→비음용수6/14초, 소독 정수기는10→깨끗한 물8.5/8초, 룬 정화 시설은10→깨끗한 물9/8초다. 세 처리기의 배치당 슬러지는1개다.
- **위키에 작성하지 않을 정보:**
  - 34필드 전체 및 카탈로그 연결을 새로 완전 검증했다고 쓰는 내용
- 감사 원본 근거: [I07_전동_양수_펌프.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I07_전동_양수_펌프.asset:130>): 생산 수질/량/전력 / [I08_상수_탱크.asset 123행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I08_상수_탱크.asset:123>): 상수120/폐수0 / [I09_오수_탱크.asset 123행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I09_오수_탱크.asset:123>): 상수0/폐수140 / [I10_물통_충전소.asset 138행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I10_물통_충전소.asset:138>): 1/4초/재고10/전력 / [I11_오수_침전조.asset 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I11_오수_침전조.asset:130>): 10→6/슬러지1/14초 / [I12_소독_정수기.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I12_소독_정수기.asset:135>): 10→8.5/슬러지1/8초 / [I13_룬_정화_시설.asset 139행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I13_룬_정화_시설.asset:139>): 10→9/슬러지1/8초 / [FluidNetworkRuntime.cs 1798행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:1798>): 생산 실행 / [FluidNetworkRuntime.cs 1840행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:1840>): 처리 실행
- 감사 위키 근거: [building-9816.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9816.json:2>): 펌프 facts에 수치 없음 / [building-9819.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9819.json:19>): 충전소 역할 전력소비만 / [building-9822.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9822.json:2>): 정화시설 facts 수치 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 채널2필드와 카탈로그 직접참조 미재검증
- 관련 GAP(제외 이력 포함): GAP-067, GAP-070
- 이번 재검토: 7개 도감 facts는 분류/크기뿐이고 현재 작성 처리량·용량·병입 역할을 알 수 없다. 정확히 재독한 수치만 공개 표에 넣는다.

## GAP-070 물통 충전소의 양방향 모드·공용 재고 목표·중단 조건 누락

- 분류: 플레이어 조작·물리 이송 규칙 누락
- 보완할 문서: infrastructure / inventory-and-carrying
- 현재 확인한 차이: 공개 충전소 요약에는 양방향 모드·공용 목표 재고·진척 초기화·대기 조건이 없다.
- **위키에 작성할 정보:**
  - 물통 충전소는 비활성·관로에서 병입·물통으로 관로 급수 모드를 가진다. 모드를 설정하면 같은 모드를 다시 지정해도 작업 진척이0으로 초기화된다.
  - 병입 목표는 충전소별 버퍼가 아니라 같은 깨끗한 물 아이템의 전체 스택 수량 합계다. 목표 이상이면 병입을 멈춘다.
  - 가동 중 완료 배치 하나를 처리하고 성공하면 필요 진척을 빼며 실패하면 진척을 한 배치분까지만 보존한다. 관로 급수는 출력 저장 여유가 필요하다.
- **위키에 작성하지 않을 정보:**
  - 물리 거래의 실패·복구·저장 왕복이 검증됐다는 내용
- 감사 원본 근거: [FluidNetworkRuntime.cs 1213행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:1213>): 모드 설정·진척 초기화 / [FluidNetworkRuntime.cs 1570행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:1570>): 틱 완료/실패 진척 / [FluidNetworkRuntime.cs 1601행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:1601>): 전체 스택 목표 / [FluidNetworkRuntime.cs 1669행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:1669>): 급수 저장 여유
- 감사 위키 근거: [building-9819.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9819.json:19>): 이송 역할 미설명 / [infrastructure.md 32행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:32>): 유체 개요만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: UI순환·정전 경로·실제 물리거래 저장왕복 미실행
- 관련 GAP(제외 이력 포함): GAP-069
- 이번 재검토: 공개 충전소 요약에는 양방향 모드·공용 목표 재고·진척 초기화·대기 조건이 없다.

## GAP-071 생활 시설의 급수·배수량과 단수 시 이용·폐기물 처리 규칙 누락

- 분류: 소비량·실패·대체 이용 조건 누락
- 보완할 문서: infrastructure / hygiene / elimination
- 현재 확인한 차이: 생활4시설의 작성 급배수량은 유지된다. 변경된 현장 사용도 시작 때 관로/수동 물을 확보하고 완료 때 배수·대체물을 처리하며 공개 도감은 이를 제공하지 않는다.
- **위키에 작성할 정보:**
  - 시설 사용 시작에 물을 소비하고 완료 때 폐수를 처리한다
  - 작성 기준은 샤워0.45/0.45, 목욕통1/1, 세면대0.15/0.15, 변기0.25/0.25(물/폐수)이며 모두 깨끗한 물을 요구한다
  - 생활용 물 배율은 물과 폐수 양을 함께 보정하므로 작성값과 실제 사용량을 구분한다
  - 샤워·목욕통은 관로 급수가 필요하고, 세면대·변기는 지정 버퍼의 깨끗한 물로 수동 공급할 수 있다
  - 변기만 물 없이 이용하는 분기가 있으며 완료 시 하수 오물8, 오염값0.45를 만든다
  - 정상 이용의 폐수가 배수되지 않으면 세면대는 슬러지, 변기는 분뇨를 max(1,ceil(폐수량))개 만들고, 해당 대체물이 없는 시설은 폐수망에 배출을 시도해 역류 경로로 이어진다
- **위키에 작성하지 않을 정보:**
  - 취소 시 환급·완료·저장 원자성을 검증 없이 보장하는 내용
- 감사 원본 근거: [Facility.cs 427행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:427>): 이용시 급수 실행 / [Facility.cs 621행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:621>): 완료시 배수 실행 / [I14_샤워_시설.asset 131행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I14_샤워_시설.asset:131>): .45/.45 / [H04_목욕통.asset 212행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H04_목욕통.asset:212>): 1/1 / [H03_세면대.asset 191행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H03_세면대.asset:191>): .15/.15·수동·슬러지 / [H01_변기.asset 175행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H01_변기.asset:175>): .25/.25·수동/무수·분뇨 / [WaterFixtureUseRuntime.cs 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/WaterFixtureUseRuntime.cs:99>): 물배율·폐수비례 및 물소비 / [WaterFixtureUseRuntime.cs 179행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/WaterFixtureUseRuntime.cs:179>): 무수 사용8/.45 / [WaterFixtureUseRuntime.cs 210행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/WaterFixtureUseRuntime.cs:210>): 배수 실패 대체물
- 감사 위키 근거: [building-9823.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9823.json:2>): 샤워 facts / [building-1060.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1060.json:2>): 목욕 facts / [building-1059.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1059.json:2>): 세면 facts / [building-1057.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1057.json:2>): 변기 facts
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 사용 중 취소·저장왕복 미실행, 원장도 확정 주장하지 않음
- 관련 GAP(제외 이력 포함): GAP-074
- 이번 재검토: 생활4시설의 작성 급배수량은 유지된다. 변경된 현장 사용도 시작 때 관로/수동 물을 확보하고 완료 때 배수·대체물을 처리하며 공개 도감은 이를 제공하지 않는다.

## GAP-072 수동 급수의 시설별 잔량·물통 반올림·운반 목적지 조건 누락

- 분류: 실물 소비·잔량·공급 조건 누락
- 보완할 문서: infrastructure / inventory-and-carrying
- 현재 확인한 차이: 시설별 잔량과 물통 정수 반올림, 목적지별 물리 입력 규칙이 공개 안내에 없다. 현재 구현은 목적지를 확보하므로 옛 누락 후보와 분리한다.
- **위키에 작성할 정보:**
  - 수동 급수의 남은 물은 시설별로 보존한다. 필요한 추가 물통 수=max(0,ceil(요청 물량-시설 잔량-0.0001))이다.
  - 해당 목적지로 배달된 미예약 입력을 사용하고 부족한 양에 대해서만 실물 배송을 요청한다. 현행 경로는 수동 급수 목적지를 동적으로 확보한다.
- **위키에 작성하지 않을 정보:**
  - 보조시설 수동 급수 목적지가 무조건 없다는 낡은 결함 주장
  - 공정 사전검사의 잔량 미반영이 재현된 결함이라는 내용
  - 저장 왕복 안전성의 확정
- 감사 원본 근거: [FluidNetworkRuntime.cs 999행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:999>): 잔량 반영 올림 / [FluidNetworkRuntime.cs 1006행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:1006>): 목적지 입력 선택 / [FluidNetworkRuntime.cs 883행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/FluidNetworkRuntime.cs:883>): 시설 잔량과 물리입력 정산
- 감사 위키 근거: [infrastructure.md 32행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:32>): 잔량/물통 규칙 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 공정 잔량 사전검사 실제 재현·저장왕복 미실행
- 관련 GAP(제외 이력 포함): GAP-073
- 이번 재검토: 시설별 잔량과 물통 정수 반올림, 목적지별 물리 입력 규칙이 공개 안내에 없다. 현재 구현은 목적지를 확보하므로 옛 누락 후보와 분리한다.

## GAP-073 생산의 시설·조합식·보조 설비 급배수 합산과 의료 경로 차이 누락

- 분류: 합산 비용·중단 조건·경로별 차이 누락
- 보완할 문서: production / infrastructure / medical-care-and-surgery
- 현재 확인한 차이: 공개 조합식 물 수치는 조합식 자체 값이고 시설·보조 합산과 의료 별도 경로를 설명하지 않는다.
- **위키에 작성할 정보:**
  - 생산 한 사이클의 물·폐수는 기본 시설, 조합식, 서로 다른 연결 보조시설의 요구를 더한다. 같은 보조시설ID는 한 번만 포함한다.
  - 수동 급수는 참여하는 요구의 수동 허용을 모두 만족해야 한다.
  - 수술은 일반 생산 주문 합산과 별도의 유체 사용 경로를 가지며 배수 실패 시 슬러지 대체물을 처리하는 분기가 있다.
- **위키에 작성하지 않을 정보:**
  - 모든 수술의 실제 가용성·사이클 재시도 중복소비 방지·중간 실패 원자성을 검증했다는 내용
- 감사 원본 근거: [ProductionCycleUtilityService.cs 159행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionCycleUtilityService.cs:159>): 보조ID 중복제거 / [ProductionCycleUtilityService.cs 195행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionCycleUtilityService.cs:195>): 수동허용 AND / [ProductionCycleUtilityService.cs 243행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionCycleUtilityService.cs:243>): 보조 합산 / [recipe_curd.asset 53행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Recipes/Workshop/recipe_curd.asset:53>): .4/4.2 / [D01_간이화덕.asset 232행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D01_간이화덕.asset:232>): .25/.25 / [WS15_치즈_응고조.asset 111행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS15_치즈_응고조.asset:111>): .2/.2 / [SurgeryRuntime.cs 692행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:692>): 의료 전용 소비 / [ProcessFluidUseRuntime.cs 218행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ProcessFluidUseRuntime.cs:218>): 의료 배수실패 슬러지
- 감사 위키 근거: [recipe-curd.json 12행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-curd.json:12>): 사이클당 깨끗한 물 .4만 / [infrastructure.md 36행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:36>): 공정별 합산 미설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 사이클 idempotency 및 실제 수술 가용성/중간실패 원자성 미검증
- 관련 GAP(제외 이력 포함): GAP-072, GAP-075
- 이번 재검토: 공개 조합식 물 수치는 조합식 자체 값이고 시설·보조 합산과 의료 별도 경로를 설명하지 않는다.

## GAP-074 생활·공정·생산 보조시설의 급배수 설정과 역할별 도감 누락

- 분류: 도감 작성 필드 누락
- 보완할 문서: facility entries / infrastructure
- 현재 확인한 차이: 전체 현재 활성 유체모듈은 생활4·공정18·보조28=50시설,244선택필드로 유지된다. 변경된 D01/D02/H03/H04/M10/M12/WS08/WS09도 해당 수량은 그대로이고50개 도감 facts는 분류/크기뿐이다.
- **위키에 작성할 정보:**
  - 생활시설의 사용당 물/폐수는 변기0.25/0.25, 세면대0.15/0.15, 목욕통1/1, 샤워 시설0.45/0.45다. 변기·세면대는 수동 급수를 허용하고 목욕통·샤워는 허용하지 않는다. 무수 이용은 변기만 허용한다.
  - 공정 시설18개 중 조리 시설5개는 사이클당 물/폐수0.25/0.25, 수술 시설13개는0.2/0.2를 요구한다.
  - 급배수를 추가하는 생산 보조시설9개와 추가 요구가0인19개를 구분한다. 예를 들어 담금 당화조는 물0.25·폐수0·수동 급수 허용, 치즈 응고조는0.2/0.2·수동 불가, 실내 생장 제어기는0.2/0.05·수동 불가다.
- **위키에 작성하지 않을 정보:**
  - 감사 JSON/MD 파일명을 공개 게임 설명으로 복사하는 내용
  - 관련 필드0을 해당 시설의 모든 기능 미지원이라고 부르는 내용
  - 카탈로그 등록을 모든 건설·수술 실행 완료로 설명하는 내용
- 감사 원본 근거: [RF72_시간_고정실.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF72_시간_고정실.asset:141>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [RF71_전신_재생조.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF71_전신_재생조.asset:141>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [RF70_룬_동면실.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF70_룬_동면실.asset:141>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [RF69_회춘_수혈실.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF69_회춘_수혈실.asset:141>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [RF68_장기_재생_수술실.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF68_장기_재생_수술실.asset:141>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [WS28_실내_생장_제어기.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS28_실내_생장_제어기.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0.2","allowsManualWaterFallback":"0","wastewaterComposition":"8","supportId":"production-support:ws28","wastewaterPerCycle":"0.05","outputMultiplier":"1"} / [WS27_대장_도구함.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS27_대장_도구함.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws27","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS26_마나_응축기.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS26_마나_응축기.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws26","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS25_무균_약품_보관함.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS25_무균_약품_보관함.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws25","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS24_직조_보조_선반.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS24_직조_보조_선반.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws24","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS23_마나_안정기.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS23_마나_안정기.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws23","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS22_세공_도구함.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS22_세공_도구함.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws22","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS21_도가니_선반.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS21_도가니_선반.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws21","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS20_정밀_연마기.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS20_정밀_연마기.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws20","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS19_목재_처리조.asset 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS19_목재_처리조.asset:99>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0.15","allowsManualWaterFallback":"0","wastewaterComposition":"7","supportId":"production-support:ws19","wastewaterPerCycle":"0.1","outputMultiplier":"1"} / [WS18_연기_포집_후드.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS18_연기_포집_후드.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws18","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS17_영양_배합_저울.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS17_영양_배합_저울.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws17","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS16_치즈_숙성_선반.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS16_치즈_숙성_선반.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"1","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws16","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS15_치즈_응고조.asset 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS15_치즈_응고조.asset:99>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0.2","allowsManualWaterFallback":"0","wastewaterComposition":"1","supportId":"production-support:ws15","wastewaterPerCycle":"0.2","outputMultiplier":"1"} / [WS14_염장_절임조.asset 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS14_염장_절임조.asset:99>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"1","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0.2","allowsManualWaterFallback":"0","wastewaterComposition":"4","supportId":"production-support:ws14","wastewaterPerCycle":"0.2","outputMultiplier":"1"} / [WS13_향신료_선반.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS13_향신료_선반.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws13","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS12_냉장_준비대.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS12_냉장_준비대.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws12","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS11_세척_전처리_싱크.asset 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS11_세척_전처리_싱크.asset:99>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0.25","allowsManualWaterFallback":"0","wastewaterComposition":"2","supportId":"production-support:ws11","wastewaterPerCycle":"0.25","outputMultiplier":"1"} / [WS10_전기_오븐.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS10_전기_오븐.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws10","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS09_벽돌_오븐.asset 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS09_벽돌_오븐.asset:99>): {"qualityModifier":"0","featureTags":"","requiresFuel":"1","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws09","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS08_화덕_가마솥.asset 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS08_화덕_가마솥.asset:99>): {"qualityModifier":"0","featureTags":"","requiresFuel":"1","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws08","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS07_분별_증류탑.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS07_분별_증류탑.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0.5","allowsManualWaterFallback":"0","wastewaterComposition":"7","supportId":"production-support:ws07","wastewaterPerCycle":"0.4","outputMultiplier":"1"} / [WS06_세척_병입대.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS06_세척_병입대.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0.25","allowsManualWaterFallback":"0","wastewaterComposition":"1","supportId":"production-support:ws06","wastewaterPerCycle":"0.25","outputMultiplier":"1"} / [WS05_숙성_오크통.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS05_숙성_오크통.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"1","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws05","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS04_온도_제어_발효조.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS04_온도_제어_발효조.asset:100>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"1","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"1","compatibleWorkstationTags":"","cleanWaterPerCycle":"0.1","allowsManualWaterFallback":"0","wastewaterComposition":"5","supportId":"production-support:ws04","wastewaterPerCycle":"0.1","outputMultiplier":"1"} / [WS03_수동_발효조.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS03_수동_발효조.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"1","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws03","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS02_담금_당화조.asset 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS02_담금_당화조.asset:99>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0.25","allowsManualWaterFallback":"1","wastewaterComposition":"0","supportId":"production-support:ws02","wastewaterPerCycle":"0","outputMultiplier":"1"} / [WS01_미세_체_선반.asset 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS01_미세_체_선반.asset:98>): {"qualityModifier":"0","featureTags":"","requiresFuel":"0","kind":"0","fuelPerCycle":"1","fuelItemId":"resource:log","workSpeedMultiplier":"1","batchCapacity":"1","maximumLinkedInstancesPerWorkstation":"1","requiresPower":"0","compatibleWorkstationTags":"","cleanWaterPerCycle":"0","allowsManualWaterFallback":"0","wastewaterComposition":"0","supportId":"production-support:ws01","wastewaterPerCycle":"0","outputMultiplier":"1"} / [D03_조리손질대.asset 152행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D03_조리손질대.asset:152>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.25","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"1","cleanWaterPerCycle":"0.25"} / [D02_고기그릴.asset 232행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D02_고기그릴.asset:232>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.25","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"1","cleanWaterPerCycle":"0.25"} / [D01_간이화덕.asset 229행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D01_간이화덕.asset:229>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.25","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"1","cleanWaterPerCycle":"0.25"} / [H04_목욕통.asset 208행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H04_목욕통.asset:208>): {"allowsManualWaterFallback":"0","manualWasteItemId":"","minimumQuality":"0","wastewaterPerUse":"1","allowsDryFallback":"0","cleanWaterPerUse":"1"} / [H03_세면대.asset 187행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H03_세면대.asset:187>): {"allowsManualWaterFallback":"1","manualWasteItemId":"industrial:sludge","minimumQuality":"0","wastewaterPerUse":"0.15","allowsDryFallback":"0","cleanWaterPerUse":"0.15"} / [H01_변기.asset 172행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H01_변기.asset:172>): {"allowsManualWaterFallback":"1","manualWasteItemId":"resource:manure","minimumQuality":"0","wastewaterPerUse":"0.25","allowsDryFallback":"1","cleanWaterPerUse":"0.25"} / [M12_비전개조대.asset 160행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M12_비전개조대.asset:160>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [M11_격리회복침상.asset 164행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M11_격리회복침상.asset:164>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [M10_면역조절기.asset 156행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M10_면역조절기.asset:156>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [M09_순환이식대.asset 164행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M09_순환이식대.asset:164>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [M07_재활보조대.asset 162행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M07_재활보조대.asset:162>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [M03_외과수술대.asset 162행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M03_외과수술대.asset:162>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [M02_해부대.asset 151행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M02_해부대.asset:151>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [M01_응급처치대.asset 163행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M01_응급처치대.asset:163>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.2","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"6","cleanWaterPerCycle":"0.2"} / [P16_훈연대.asset 149행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P16_훈연대.asset:149>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.25","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"1","cleanWaterPerCycle":"0.25"} / [P15_조리대.asset 149행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P15_조리대.asset:149>): {"allowsManualWaterFallback":"1","wastewaterPerCycle":"0.25","workTypeIds":"","minimumQuality":"0","wastewaterComposition":"1","cleanWaterPerCycle":"0.25"} / [I14_샤워_시설.asset 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I14_샤워_시설.asset:128>): {"allowsManualWaterFallback":"0","manualWasteItemId":"","minimumQuality":"0","wastewaterPerUse":"0.45","allowsDryFallback":"0","cleanWaterPerUse":"0.45"}
- 감사 위키 근거: [building-1609.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1609.json:2>): 전기 오븐: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1608.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1608.json:2>): 벽돌 오븐: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1607.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1607.json:2>): 화덕·가마솥: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1606.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1606.json:2>): 분별 증류탑: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1605.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1605.json:2>): 세척·병입대: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-1604.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1604.json:2>): 숙성 오크통: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 50개 현재 작성값·도감 정적대조. 모든 시설의 건설·수술 및 저장 왕복 실행 아님
- 관련 GAP(제외 이력 포함): GAP-069, GAP-071, GAP-073
- 이번 재검토: 전체 현재 활성 유체모듈은 생활4·공정18·보조28=50시설,244선택필드로 유지된다. 변경된 D01/D02/H03/H04/M10/M12/WS08/WS09도 해당 수량은 그대로이고50개 도감 facts는 분류/크기뿐이다.

## GAP-075 조합식의 폐수량·폐수 종류·수동 공급 조건 누락과 물 수치의 범위 불명확

- 분류: 도감 작성 필드·표시 범위 누락
- 보완할 문서: recipe entries / production
- 현재 확인한 차이: 응유 도감은 조합식 물0.4만 표시하고 폐수·수동 조건과 최종 합산 범위를 설명하지 않는다.
- **위키에 작성할 정보:**
  - 응유 조합식 자체는 사이클당 물0.4·폐수4.2를 요구하고 수동 급수를 허용한다. 이 물0.4는 시설·보조 설비의 추가 물을 포함한 총량이 아니다.
  - 예를 들어 간이화덕0.25/0.25와 치즈 보조0.2/0.2를 함께 쓰면 총 물0.85·폐수4.65다.
- **위키에 작성하지 않을 정보:**
  - 폐수 성분 enum3의 실제 성격을 라벨 대조 없이 해석하는 내용
  - 355개 조합식 수량·성분을 전수 검증한 표
- 감사 원본 근거: [recipe_curd.asset 53행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Recipes/Workshop/recipe_curd.asset:53>): 물·폐수·성분·수동허용 / [ProductionCycleUtilityService.cs 192행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionCycleUtilityService.cs:192>): 조합식 수요 실제 합산
- 감사 위키 근거: [recipe-curd.json 12행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/recipe/recipe-curd.json:12>): 물.4만 표시, 폐수와 수동 조건 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 355개 조합식 집계·모든 도감·성분 enum 라벨 미전수
- 관련 GAP(제외 이력 포함): GAP-073
- 이번 재검토: 응유 도감은 조합식 물0.4만 표시하고 폐수·수동 조건과 최종 합산 범위를 설명하지 않는다.

## GAP-076 컨베이어 반입·경로·속도·화물 수용량과 예약 제외 조건 누락

- 분류: 설명·조건·단위 누락
- 보완할 문서: infrastructure / inventory-and-carrying
- 현재 확인한 차이: 목적지·한 스택·최단 구간·한 틱 한 구간 규칙은 현재 코드에 있지만 공개 운송 개요에는 없다. 현행 UI는 실제 목적지 선택·해제 명령을 실행하므로 UI 부재 후보는 삭제한다.
- **위키에 작성할 정보:**
  - 자동 반입 포트는 목적지가 지정되어야 적재하며, 한 번의 반입 처리에서 포트마다 성공한 한 스택까지만 적재한다.
  - 경로는 연결 구간 수가 가장 짧은 경로로 정한다. 같은 길이 후보의 탐색 순서는 시설 식별자 순으로 정해진다.
  - 화물 진행도는 속도×경과시간만큼 증가하지만 한 번의 갱신에서 다음 한 구간까지만 이동한다. 화물 수용량은 개별 아이템 개수가 아닌 운송 화물 개수다.
  - 산업 화면에서 목적지를 선택하거나 해제할 수 있다. 목적지 해제는 새 자동 적재를 막으며 이미 운반 중인 화물의 목적지는 유지한다.
- **위키에 작성하지 않을 정보:**
  - 예약 품목 제외·보호 출력의 전체 분기를 검증한 설명
  - 한 갱신에 속도만큼 여러 구간을 건너뛴다는 설명
- 감사 원본 근거: [ConveyorRuntime.cs 736행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:736>): 목적지 없는 자동반입 거부 / [ConveyorRuntime.cs 747행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:747>): 한 스택 성공 후 break / [ConveyorRuntime.cs 1097행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:1097>): 벨트 capacity 우선 / [ConveyorRoutePlanner.cs 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRoutePlanner.cs:30>): BFS와ID 정렬 / [ConveyorRuntime.cs 631행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:631>): 속도 누적·한 구간 / [IndustrialFeatureSurfacePresenter.cs 719행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/UI/IndustrialFeatureSurfacePresenter.cs:719>): 선택·해제 UI;737목적지 명령
- 감사 위키 근거: [infrastructure.md 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:34>): 컨베이어 개요만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 예약 제외·보호 출력·재시도 전체 분기의 정적 전수는 별도이다.
- 관련 GAP(제외 이력 포함): GAP-079
- 이번 재검토: 목적지·한 스택·최단 구간·한 틱 한 구간 규칙은 현재 코드에 있지만 공개 운송 개요에는 없다. 현행 UI는 실제 목적지 선택·해제 명령을 실행하므로 UI 부재 후보는 삭제한다.

## GAP-077 컨베이어 품목·소재·품질·신선도·오염 필터의 결합 조건 누락

- 분류: 조건·UI 조작 범위 누락
- 보완할 문서: infrastructure
- 현재 확인한 차이: 정적·동적 필터 결합과 타입별 검사 규칙이 공개 개요에 없다. 현행 화면의 품목·분류·소재 편집과 실제 변경 명령을 다시 읽었으므로 조회 API만 있다는 후보는 삭제한다.
- **위키에 작성할 정보:**
  - 품목 목록과 재고 분류 목록은 둘 중 하나에 맞으면 통과하며, 둘 다 비어 있으면 이 조건으로 제한하지 않는다. 에셋 기본 필터와 사용자가 지정한 필터는 모두 만족해야 한다.
  - 소재·품질은 장비 인스턴스를 검사하고, 신선도·오염 허용은 식품 상태를 검사한다. 품목/분류 통과만으로 추가 조건이 면제되지는 않는다.
  - 금지품 허용은 기본 설정과 사용자 설정 중 하나라도 허용하면 통과한다.
  - 산업 화면에서 품목·분류·장비 소재 목록을 편집할 수 있다. 일반 원료의 소재를 고르는 기능으로 설명하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 최대 신선도 편집·모든 UI 증분과 미등록 스택의 동작을 확인한 설명
- 감사 원본 근거: [ConveyorPayloadAdmissionPolicy.cs 68행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorPayloadAdmissionPolicy.cs:68>): 금지품 OR / [ConveyorPayloadAdmissionPolicy.cs 88행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorPayloadAdmissionPolicy.cs:88>): 품목/분류 OR, static/runtime AND / [ConveyorPayloadAdmissionPolicy.cs 158행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorPayloadAdmissionPolicy.cs:158>): 장비 인스턴스 필요 / [ConveyorPayloadAdmissionPolicy.cs 187행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorPayloadAdmissionPolicy.cs:187>): 오염과 신선도 / [IndustrialFeatureSurfacePresenter.cs 644행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/UI/IndustrialFeatureSurfacePresenter.cs:644>): 품목/분류/소재 목록 편집 / [IndustrialFeatureSurfacePresenter.cs 887행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/UI/IndustrialFeatureSurfacePresenter.cs:887>): SetAdvancedFilter 호출
- 감사 위키 근거: [infrastructure.md 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:34>): 필터 결합 규칙 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 최대신선도/모든 UI 증분·카탈로그 미등록스택 분기 미검증
- 이번 재검토: 정적·동적 필터 결합과 타입별 검사 규칙이 공개 개요에 없다. 현행 화면의 품목·분류·소재 편집과 실제 변경 명령을 다시 읽었으므로 조회 API만 있다는 후보는 삭제한다.

## GAP-078 컨베이어 정체·교착 판정과 넘침 배출 정책·대기시간 누락

- 분류: 수치·상태 전이·예외 누락
- 보완할 문서: infrastructure
- 현재 확인한 차이: 공개 배출 정책 공백은 실제지만 원장의 창고 후 바닥 설명은 현재 보존 분기와 반대다. 확인된 타이머·보존·승인 조건으로 한정한다.
- **위키에 작성할 정보:**
  - 수동 정지나 정전으로 멈춘 동안은 정체 시간을 누적하지 않고 타이머를 초기화한다.
  - 가까운 창고 우선·지정 예비창고 우선 정책은 입고에 실패해도 화물을 벨트에 보존한다. 정책 이름과 달리 창고 실패 뒤 자동 바닥 배출하지 않는다.
  - 바닥 배출은 명시적 바닥 배출 정책 또는 화물별 수동 승인 정책에서만 가능하다. 수동 승인 정책은 해당 화물의 승인이 필요하며 배출 좌표 후보는 최대8개를 확인한다.
- **위키에 작성하지 않을 정보:**
  - 창고 실패 시 바닥에 흘리는 두 정책이라는 설명
  - 30초 문턱·교착 판정·모든 배출 좌표순·창고 질량 검증을 재전수한 설명
- 감사 원본 근거: [ConveyorRuntime.cs 555행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:555>): 수동정지 시간 미누적 / [ConveyorRuntime.cs 563행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:563>): 정전 시간 미누적 / [ConveyorRuntime.cs 854행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:854>): 창고 실패시 보존 / [ConveyorRuntime.cs 856행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:856>): 바닥 허용 정책 제한 / [ConveyorRuntime.cs 965행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:965>): 최대8 후보 / [ConveyorRuntime.cs 811행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:811>): 수동승인 검사
- 감사 위키 근거: [infrastructure.md 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:34>): 배출정책/대기 규칙 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 30초·교착·바닥좌표순·전체 창고질량 검증 미전수
- 이번 재검토: 공개 배출 정책 공백은 실제지만 원장의 창고 후 바닥 설명은 현재 보존 분기와 반대다. 확인된 타이머·보존·승인 조건으로 한정한다.

## GAP-079 컨베이어 기능 시설40개의 운송 설정·입출력 역할 도감 누락

- 분류: 도감 필드·조건별 적용 범위 누락
- 보완할 문서: facility entries / infrastructure
- 현재 확인한 차이: 현행 에셋40개·284필드와 공개40개 도감 본문을 재확인했다. 도감은 분류·크기만 제시하며 위 운송 역할/설정은 없다. 겹친 수용량2개와 비활성 필터를 실제 적용값과 구분한다.
- **위키에 작성할 정보:**
  - 전용 운송 시설13개와 포트를 가진 일반 생산시설27개가 있다. 일반 생산시설27개는 입출력 양방향 포트와 화물4개 수용량을 가진다.
  - 입력기·출력기의 실제 수용량은 화물2개다. 함께 작성된 포트 수용량4는 벨트 수용량2에 가려져 적용되지 않는다.
  - 포트29개의 초기 목적지는 비어 있어 운송 목적지 설정이 필요하다.
  - 전용 벨트13개는 초기 품목·분류·소재 제한이 없고 품질·신선도 필터는 꺼져 있다. 꺼진 필터의 품질0~7·신선도0~1 범위를 활성 제한으로 표시하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 작성 필드284개를284개의 독립 기능으로 세는 설명
  - 우선순위 게이트·층간 리프트의 이름만으로 확인되지 않은 우선 분배·층간 이동을 추가한 설명
- 감사 원본 근거: [D03_조리손질대.asset 138행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D03_조리손질대.asset:138>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [D02_고기그릴.asset 218행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D02_고기그릴.asset:218>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [D01_간이화덕.asset 215행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D01_간이화덕.asset:215>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [G06_전리품거치대.asset 189행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/G06_전리품거치대.asset:189>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P14_증류기.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P14_증류기.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P13_퇴비장.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P13_퇴비장.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P12_무두질대.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P12_무두질대.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P11_직조기.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P11_직조기.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P10_비전단조대.asset 142행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P10_비전단조대.asset:142>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P09_귀금세공대.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P09_귀금세공대.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P08_제강로.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P08_제강로.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P07_용광로.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P07_용광로.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P06_광석선별대.asset 149행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P06_광석선별대.asset:149>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P05_석재절단대.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P05_석재절단대.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P04_숯가마.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P04_숯가마.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P03_제재소.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P03_제재소.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P02_양조장.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P02_양조장.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P01_제분소.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P01_제분소.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [S08_대장작업대.asset 211행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/S08_대장작업대.asset:211>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P25_폐기소각로.asset 184행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P25_폐기소각로.asset:184>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P21_대장간.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P21_대장간.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P20_몽직기.asset 142행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P20_몽직기.asset:142>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P19_연금대.asset 154행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P19_연금대.asset:154>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P18_약제대.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P18_약제대.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P17_사료배합대.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P17_사료배합대.asset:141>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P16_훈연대.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P16_훈연대.asset:135>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [P15_조리대.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P15_조리대.asset:135>): BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"2"} / [C10_고속_컨베이어.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C10_고속_컨베이어.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"2","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"2","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [C09_오버플로_배출_게이트.asset 138행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C09_오버플로_배출_게이트.asset:138>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"[]","allowForbidden":"0","allowedStockCategories":"","capacity":"2","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"}; BuildingConveyorOverflowAbility: {"stallSeconds":"30","defaultPolicy":"0"} / [C08_층간_물류_리프트.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C08_층간_물류_리프트.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"0.8","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"2","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [C07_우선순위_게이트.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C07_우선순위_게이트.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"1","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [C06_컨베이어_필터.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C06_컨베이어_필터.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"1","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [C05_컨베이어_합류기.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C05_컨베이어_합류기.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"2","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [C04_컨베이어_분배기.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C04_컨베이어_분배기.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"2","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [C03_컨베이어_출력기.asset 136행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C03_컨베이어_출력기.asset:136>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"[]","allowForbidden":"0","allowedStockCategories":"","capacity":"2","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"}; BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"1"} / [C02_컨베이어_입력기.asset 136행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C02_컨베이어_입력기.asset:136>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"2","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"}; BuildingConveyorPortAbility: {"destinationId":"","capacity":"4","mode":"0"} / [C01U_컨베이어_상향.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C01U_컨베이어_상향.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"1","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [C01R_컨베이어_우향.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C01R_컨베이어_우향.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"1","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [C01L_컨베이어_좌향.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C01L_컨베이어_좌향.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"1","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [C01D_컨베이어_하향.asset 135행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/C01D_컨베이어_하향.asset:135>): BuildingConveyorSegmentAbility: {"allowedMaterialIds":"[]","filterQuality":"0","maximumQuality":"7","allowedItemIds":"[]","speed":"1","requiresPower":"1","outputDirections":"","allowForbidden":"0","allowedStockCategories":"","capacity":"1","minimumFreshness01":"0","filterFreshness":"0","allowContaminated":"1","maximumFreshness01":"1","minimumQuality":"0"} / [ConveyorRuntime.cs 1097행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:1097>): 벨트 수용량이 포트보다 우선
- 감사 위키 근거: [building-9844.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9844.json:2>): 컨베이어 입력기: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-9842.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9842.json:2>): 컨베이어 상향: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-9840.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9840.json:2>): 컨베이어 우향: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-9841.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9841.json:2>): 컨베이어 좌향: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-9843.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9843.json:2>): 컨베이어 하향: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음 / [building-9847.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9847.json:2>): 컨베이어 합류기: 현재 facts는 분류/크기만. 원본 해당 기능 수치 없음
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 에셋·현재 공개 JSON 정적 전수이며 모든 네트워크의 실플레이·저장 왕복 검증은 아니다.
- 관련 GAP(제외 이력 포함): GAP-076, GAP-077, GAP-078
- 이번 재검토: 현행 에셋40개·284필드와 공개40개 도감 본문을 재확인했다. 도감은 분류·크기만 제시하며 위 운송 역할/설정은 없다. 겹친 수용량2개와 비활성 필터를 실제 적용값과 구분한다.

## GAP-080 컨베이어 경로 저장 설명 오류와 복원 후 재계산·승인 상태 누락

- 분류: 저장 설명 불일치·재개 조건 누락
- 보완할 문서: infrastructure
- 현재 확인한 차이: 위키의 경로/순서 저장 설명은 저장 DTO와 반대다. 저장 데이터와 복원 후 재계산·수동승인 초기화를 분리하여 고친다.
- **위키에 작성할 정보:**
  - 저장에는 화물·스택 식별자, 현재/이전 구간, 목적지, 진행도, 정지 경과와 노드 가동·필터·배출 설정이 포함된다.
  - 전체 경로 목록과 경로 인덱스는 저장하지 않으며 복원 후 현재 연결망에서 다시 계산한다. 화물별 수동 배출 승인도 저장하지 않는다.
  - 복원 후 연결망 투영을 초기화하거나 연결·필터·가동 설정을 바꾸면 경로와 정지 타이머·재시도 시점이 초기화된다.
- **위키에 작성하지 않을 정보:**
  - 현재 경로 및 순서를 영구 저장한다는 설명
  - 저장된 정체 시간이 항상 그대로 이어진다는 설명
  - 실행 재현 없이 타이머 초기화를 의도된 기능 또는 악용 확정으로 규정하는 설명
- 감사 원본 근거: [ConveyorPersistence.cs 179행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorPersistence.cs:179>): payload 저장목록 / [IndustrialInfrastructureModels.cs 320행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureModels.cs:320>): DTO 경로/인덱스 없음 / [ConveyorRuntime.cs 1201행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:1201>): 경로 무효화시 타이머 초기화 / [ConveyorRuntime.cs 1239행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:1239>): 복원시 승인집합 Clear / [ConveyorRuntime.cs 537행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Industrial/ConveyorRuntime.cs:537>): 토폴로지 갱신시 무효화
- 감사 위키 근거: [infrastructure.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:40>): 현재 경로 및 순서 저장이라는 오류
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 저장 왕복·동일 재개 결과 및 설계 의도는 미검증.
- 관련 GAP(제외 이력 포함): GAP-061
- 이번 재검토: 위키의 경로/순서 저장 설명은 저장 DTO와 반대다. 저장 데이터와 복원 후 재계산·수동승인 초기화를 분리하여 고친다.

## GAP-081 온도·공기·조명 확산과 벽·문·덕트의 실제 작용 누락

- 분류: 환경 계산·연결 조건 누락
- 보완할 문서: weather-seasons-and-environment
- 현재 확인한 차이: 정량 확산 규칙은 공개 개요에 없다. 실제 문 개폐를 소비하고 닫힌 문 양끝의 열·공기만 차단하므로 이전 반대 주장은 제거한다. 병합 이력201의 외부 공기100·덕트0.65를 소유 항목에 복원한다.
- **위키에 작성할 정보:**
  - 주변 칸의 온도·공기 교환 계수는 일반0.12, 문이 있는 경계0.55다. 양쪽 칸의 덕트 계수가 더 크면 그 값을 사용하며, 주변 칸 기여는 이웃 수로 나눈다.
  - 닫힌 문은 양끝 칸 사이의 온도·공기 교환을 차단하지만 빛 교환은 막지 않는다. 빛의 이웃 교환에는 선택된 교환 계수의0.6배를 적용한다.
  - 외기 온도로 가까워지는 계수는 실외0.35·실내0.08, 공기100으로 가까워지는 계수는 실외0.5·실내0.015다. 이는 이웃 사이 교환과 별도 항이다.
  - 환기덕트·송풍구·배기팬의 덕트 교환 계수는0.65다. 외부 공기와 교환하는 발생원의 목표 공기 상태는100이다.
- **위키에 작성하지 않을 정보:**
  - 문 열림 상태를 읽지 않는다거나 닫힌 문이 열·공기 흐름을 막지 않는다는 설명
  - 닫힌 문이 외기 완화 항까지 모두 없애거나 빛을 차단한다는 설명
- 감사 원본 근거: [EnvironmentalFieldSimulationRules.cs 61행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalFieldSimulationRules.cs:61>): 실내/실외·이웃 확산 상수 / [EnvironmentalFieldSimulationRules.cs 170행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalFieldSimulationRules.cs:170>): 이웃수 평균 및 외기항 / [EnvironmentalFieldSimulationRules.cs 224행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalFieldSimulationRules.cs:224>): 덕트 최대값;230닫힌문 열/공기 차단;236빛 유지 / [EnvironmentalFieldRuntime.cs 812행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:812>): Door.IsOpen 실제 소비 / [EnvironmentalFieldRuntime.cs 692행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:692>): 외부공기 목표100 / [E12_환기덕트.asset 134행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E12_환기덕트.asset:134>): 덕트0.65 / [E13_송풍구.asset 151행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E13_송풍구.asset:151>): 덕트0.65 / [E14_배기팬.asset 151행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E14_배기팬.asset:151>): 덕트0.65
- 감사 위키 근거: [weather-seasons-and-environment.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:41>): 칸별 환경 개요 / [weather-seasons-and-environment.md 43행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:43>): 문 개방시 연결변경 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 발생원 직선 거리효과와 전역 경로의 모든 조합은 미전수.
- 이번 재검토: 정량 확산 규칙은 공개 개요에 없다. 실제 문 개폐를 소비하고 닫힌 문 양끝의 열·공기만 차단하므로 이전 반대 주장은 제거한다. 병합 이력201의 외부 공기100·덕트0.65를 소유 항목에 복원한다.

## GAP-082 냉각·공조·환기·아크등의 확인된 성능과 목표 온도 조절 누락

- 분류: 도감 기능·작성 수치·조작 설명 누락
- 보완할 문서: infrastructure / entry/facility/building-*
- 현재 확인한 차이: 확인된 냉각기·공조기·환기 발생원·아크등 수치와 목표 조절은 공개 도감/가이드에 없다. 병합 이력202의 온도·속도·반경과201의 공기 성능을 복원하고 미전수11개 전체 주장은 좁힌다.
- **위키에 작성할 정보:**
  - 냉각기는 기본 목표8℃, 조절 범위2~8℃, 초당 변화량3℃, 반경3칸이다. 공조기는 기본22℃, 조절2~30℃, 초당2.5℃, 반경4칸이다. 시설 화면에서 목표 온도를2℃씩 조절하며 시설별 범위로 제한된다.
  - 공조기·송풍구·배기팬의 공기 변화율은 각각 초당6·8·12, 반경은4·3·4칸이다. 모두 외부 공기 교환으로 목표100을 사용한다.
  - 전기 아크등의 작성 광도는1.2, 반경5.5이며 격자 적용 반경은 올림하여6칸이다. 조명은 시설 활성 상태와 필요한 전력·연료 공급 조건을 확인한 뒤 환경값과 발광 표시를 함께 갱신한다.
- **위키에 작성하지 않을 정보:**
  - 11개50필드 전체를 승인한 환경시설 표
  - 작성된 광도1.2를 모든 칸의 최종 조명120이라고 쓰는 내용
  - 보관 정원·냉각 역방향·배기 이상을 재현된 결함으로 단정하는 내용
- 감사 원본 근거: [E10_냉각기.asset 159행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E10_냉각기.asset:159>): 8℃/범위2~8/3℃초/반경3 / [E11_공조기.asset 155행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E11_공조기.asset:155>): 22℃/범위2~30/2.5℃초/반경4;169공기6 / [E13_송풍구.asset 143행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E13_송풍구.asset:143>): 공기8/반경3 / [E14_배기팬.asset 143행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E14_배기팬.asset:143>): 공기12/반경4 / [I15_전기_아크등.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I15_전기_아크등.asset:141>): 광도1.2/반경5.5 / [EnvironmentalFieldRuntime.cs 241행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:241>): 목표온도clamp / [EnvironmentalFieldRuntime.cs 567행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:567>): 조명 전력/연료 gates;569emission / [EnvironmentalFieldRuntime.cs 713행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:713>): 빛 반경올림 / [EnvironmentalBuildingPanelPresenter.cs 85행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/EnvironmentalBuildingPanelPresenter.cs:85>): 목표온도 ±2℃
- 감사 위키 근거: [building-9824.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9824.json:19>): 전력소비만 역할 설명 / [weather-seasons-and-environment.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:39>): 환경시설 개요
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 현재11개50필드 전체·보관 정원·모든 냉각/배기 경계 동작을 승인하지 않는다.
- 관련 GAP(제외 이력 포함): GAP-081, GAP-085
- 이번 재검토: 확인된 냉각기·공조기·환기 발생원·아크등 수치와 목표 조절은 공개 도감/가이드에 없다. 병합 이력202의 온도·속도·반경과201의 공기 성능을 복원하고 미전수11개 전체 주장은 좁힌다.

## GAP-083 추위·더위·공기 노출과 시각 피로의 누적·회복 공식 누락

- 분류: 노출 수식·단계 경계 누락
- 보완할 문서: weather-seasons-and-environment
- 현재 확인한 차이: 정확한 누적·회복·단계 수식이 가이드에 없다. 현행 캐릭터 실행기는 조명 적응이 켜지면 기존 시각 노출을 억제하므로 이전 '네 노출을 항상 갱신' 설명에 예외를 추가한다.
- **위키에 작성할 정보:**
  - 노출은0~100 범위다. 편안한 조건에서는 초당1.5씩 회복한다. 단계 상승 문턱은25/50/75/100이며 부담·기능 저하·위급에서 내려갈 때는 해당 문턱보다5 낮아져야 한다.
  - 공기70 이상은 공기 노출이 늘지 않는다.70→40 구간은 나빠진 비율 x에 대해 초당0.15×x^1.5,40→20은0.5+1.5×x^1.5,20 미만은2씩 늘어난다.
  - 온도는 종족별 편안함·안전·치명 경계와 보호 효과를 적용한다. 편안함→안전 경계 구간은0.15×x^1.5, 안전→치명 구간은0.5+1.5×x^1.5, 치명 경계 이상은2의 기본 노출률을 쓴다.
  - 기존 시각 피로는 조명 적응 기능이 꺼진 정밀작업·수술·긴급수술에서만 누적된다. 조명50 이상은 누적0,50 미만에서는 어두워진 비율 x에 따라0.15+0.85×x^1.5씩 늘어난다. 조명 적응이 켜졌거나 정밀작업이 아니면 기존 시각 피로는 회복 조건으로 처리된다.
  - 사망자와 원정 중인 인물은 이 현장 노출 갱신에서 제외된다.
- **위키에 작성하지 않을 정보:**
  - 모든 인물이 어둠에서 항상 기존 시각 피로를 누적한다는 설명
  - 종족·의복·침구의 모든 보호 공급값과 저장 왕복을 전수 검증했다는 설명
- 감사 원본 근거: [CharacterEnvironmentRuntime.cs 505행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:505>): 사망/원정 제외 / [CharacterEnvironmentRuntime.cs 535행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:535>): 조명적응 선택;555legacy시각 조건 / [CharacterEnvironmentRuntime.cs 577행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:577>): 적응 활성/비정밀이면 기존 시각 회복 / [EnvironmentalCoreDomain.cs 340행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalCoreDomain.cs:340>): 단계 문턱과하강5 / [EnvironmentalCoreDomain.cs 366행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalCoreDomain.cs:366>): 회복1.5 / [EnvironmentalCoreDomain.cs 537행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalCoreDomain.cs:537>): 공기구간 / [EnvironmentalCoreDomain.cs 559행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalCoreDomain.cs:559>): 시각구간 / [EnvironmentalCoreDomain.cs 569행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalCoreDomain.cs:569>): 온도구간
- 감사 위키 근거: [weather-seasons-and-environment.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:41>): 노출 정성적 개요만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 종족·의복·침구 전체 보호 프로필 및 조명 적응 자체의 모든 수치·저장 왕복은 미전수.
- 관련 GAP(제외 이력 포함): GAP-084
- 이번 재검토: 정확한 누적·회복·단계 수식이 가이드에 없다. 현행 캐릭터 실행기는 조명 적응이 켜지면 기존 시각 노출을 억제하므로 이전 '네 노출을 항상 갱신' 설명에 예외를 추가한다.

## GAP-084 환경 노출의 작업·이동·기분·피해·명중률 보정 누락

- 분류: 파생 효과·전투 보정 수치 누락
- 보완할 문서: weather-seasons-and-environment
- 현재 확인한 차이: 생리 단계별 속도·기분·피해 정량 공백은 실제다. 기존 전투 문서의 조명/적용 순서는 제외하고, 변경된 현재 작업 실행기의 조명 적응 추가 배율을 명시한다.
- **위키에 작성할 정보:**
  - 부담/기능 저하/위급/붕괴의 일반 작업 기본 배율은0.9/0.75/0.5/0.1, 정밀작업 기본 배율은0.85/0.6/0.35/0.35다. 실제 작업 속도에는 현재 조명 적응 배율도 별도로 결합되므로 이 표가 최종 배율은 아니다.
  - 같은 단계의 이동 배율은0.95/0.85/0.7/0.1이며 생리 노출의 명중 감점은 기능 저하10%p, 위급·붕괴25%p다.
  - 부담·기능 저하·위급 단계로 바뀌면15초 동안 기분이 각각5·10·20 감소한다. 붕괴 단계 진입은 억압100을 더한다.
  - 위급 이상 노출은10초마다 최대 체력1%의 비치명 피해를 준다. 치명 온도 또는 공기20 미만에서는 별도로 초당 최대 체력1%의 사망 가능한 피해를 준다.
- **위키에 작성하지 않을 정보:**
  - 공개 전투 문서에 이미 있는 조명40/25/10%p 및 감점 적용 순서까지 누락이라고 세는 설명
  - 기본 단계표만으로 조명 적응까지 포함한 최종 작업 속도를 계산하는 설명
- 감사 원본 근거: [EnvironmentPolicyDomain.cs 163행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentPolicyDomain.cs:163>): 일반/정밀 기본작업배율 / [CharacterEnvironmentRuntime.cs 148행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:148>): 현재 조명적응 배율 결합 / [EnvironmentalCoreDomain.cs 519행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalCoreDomain.cs:519>): 이동;529명중감점 / [CharacterEnvironmentRuntime.cs 645행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:645>): 단계진입기분·억압 / [CharacterEnvironmentRuntime.cs 683행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:683>): 위급10초1%비치명 / [CharacterEnvironmentRuntime.cs 595행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:595>): 치명환경1%/초 사망가능
- 감사 위키 근거: [weather-seasons-and-environment.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:41>): 단계별 효과 없음 / [combat-and-equipment.md 82행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:82>): 이미 감점순서 설명 / [combat-and-equipment.md 87행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:87>): 이미 조명 정량감점 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 정밀작업 실제 호출자의 모든 분기와 조명 적응 전체 수치의 전수는 미실시.
- 관련 GAP(제외 이력 포함): GAP-083
- 이번 재검토: 생리 단계별 속도·기분·피해 정량 공백은 실제다. 기존 전투 문서의 조명/적용 순서는 제외하고, 변경된 현재 작업 실행기의 조명 적응 추가 배율을 명시한다.

## GAP-085 환경 위험에 따른 작업 거부·방한복 착용·재배정·대피 조건 누락

- 분류: 작업 시작·중단·복귀 조건 누락
- 보완할 문서: residents-and-work
- 현재 확인한 차이: 기존 출판 필드는 작업 지시만 남아 있어 실제15/10·25·1초·단계 조건을 복원한다. 시작 시 보호 시도와 진행 중 중단 예외를 분리한다.
- **위키에 작성할 정보:**
  - 냉기 노출15 이상이면 냉기 휴식 잠금이 켜지고10 미만이 되어야 풀린다.10 이상15 미만에서는 기존 잠금 상태를 유지한다.
  - 일반 작업에서 보호가 필요하고 종료 시 냉기 노출이25 이상으로 예측되면 방한 보호복 자동 착용을 시도한 뒤 위험을 다시 예측한다. 강제 작업과 안전 예외 작업은 이 자동 착용 조건에서 제외된다.
  - 진행 작업은1초 간격으로 환경을 다시 확인한다. 생리·시각 단계 중 높은 쪽이 기능 저하 이상이면 재배정, 위급 이상이거나 치명 환경이 예측되면 대피를 시도한다.
  - 진행 중인 긴급수술·방어·안전 업무는 위1초 환경 중단 검사에서 제외된다.
- **위키에 작성하지 않을 정보:**
  - 보호함의 거리 검사만으로 실제 도달 가능 경로가 검증됐다고 쓰는 내용
  - 전체 대피칸 우선순위·예측시간·보호복 선택 정책을 전수 승인한 설명
- 감사 원본 근거: [EnvironmentPolicyDomain.cs 87행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentPolicyDomain.cs:87>): 냉기잠금15/10 / [EnvironmentWorkPolicyUnityAdapter.cs 142행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentWorkPolicyUnityAdapter.cs:142>): 보호필요·강제/예외 제외·종료냉기25와착용 / [WorkTaskExecutor.cs 2796행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:2796>): 1초재검사·안전예외 / [WorkTaskExecutor.cs 2813행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:2813>): 실제높은단계와대피/재배정
- 감사 위키 근거: [weather-seasons-and-environment.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:31>): 의복/휴식 개요 / [weather-seasons-and-environment.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:41>): 업무제한 정책 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 예측시간 적분·대피칸 우선순위·시설12개 설정·보호복 선택/해금 전체는 미전수.
- 관련 GAP(제외 이력 포함): GAP-082, GAP-083
- 이번 재검토: 기존 출판 필드는 작업 지시만 남아 있어 실제15/10·25·1초·단계 조건을 복원한다. 시작 시 보호 시도와 진행 중 중단 예외를 분리한다.

## GAP-086 보관 온도에 따른 음식 신선도 감소 배율 누락

- 분류: 보관·부패 수식 누락
- 보완할 문서: food-and-ecology
- 현재 확인한 차이: 일일 음식 실행기와 부패 계산을 다시 확인했으며 공개 식량 본문에 정량 온도 효과가 없다. 장기 안전 온도는 현재 수술 부품 실행기2698에서 실제 소비하므로 미연결 후보는 삭제하되 음식 누락과 별개로 둔다.
- **위키에 작성할 정보:**
  - 음식 부패의 온도 배율은 2^((보관 온도−20)/10)이며0.25~4로 제한된다.20℃는1배,10℃는0.5배,0℃ 이하는0.25배,30℃는2배,40℃ 이상은4배다.
  - 일일 신선도 감소 계산은180×온도 배율이며 보존 처리된 음식은 여기에0.25를 곱한다.
- **위키에 작성하지 않을 정보:**
  - 장기 보존 안전 온도를 실제로 읽는 실행 경로가 없다는 설명
  - 음식 보존 상태 생성과 장기 보존의 전체 수명·시설 작동까지 검증한 설명
- 감사 원본 근거: [EnvironmentalCoreDomain.cs 354행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Environment/Core/EnvironmentalCoreDomain.cs:354>): 2배곡선·0.25~4 / [SurvivalFoodSpoilageRuntime.cs 179행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodSpoilageRuntime.cs:179>): 180×환경배율;보존0.25 / [SurvivalFoodRuntime.cs 341행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs:341>): advanceTime:true 일일 호출 / [SurgicalPartRuntime.cs 2698행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs:2698>): 장기 안전온도 실제 소비
- 감사 위키 근거: [food-and-ecology.md 67행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:67>): 식량 구분만 / [food-and-ecology.md 212행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:212>): 보관온도 정량효과 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 보존 상태 생성 및 장기 보존 전체 수명/시설 실행은 별도 미전수.
- 이번 재검토: 일일 음식 실행기와 부패 계산을 다시 확인했으며 공개 식량 본문에 정량 온도 효과가 없다. 장기 안전 온도는 현재 수술 부품 실행기2698에서 실제 소비하므로 미연결 후보는 삭제하되 음식 누락과 별개로 둔다.

## GAP-087 초기 기후 보호 기간과 계절별 외기 계산식 누락

- 분류: 시작 예외·달력·기온 공식 누락
- 보완할 문서: weather-seasons-and-environment
- 현재 확인한 차이: 공개 가이드는 이미 외기식의 개념을 설명하지만 첫5일 보호와 정확한 날짜식·잡음 범위는 없다. 해당 수치만 추가 공백으로 좁힌다.
- **위키에 작성할 정보:**
  - 시작1~5일은 맑음·외기20℃·일일 잡음0으로 고정된다.6일부터 정상 날씨·계절 계산을 적용한다.
  - 한 계절은30일,1년은120일이다. 정상 외기는 기후 평균+진폭×sin(2π×(연중일−30)/120)+현재 날씨 보정+일일 잡음으로 계산하며 일일 잡음 범위는−2~2℃다.
- **위키에 작성하지 않을 정보:**
  - 평균+계절 사인파+날씨+잡음이라는 개념 자체가 공개 안내에 없다는 설명
  - 기본 시작 기후 선택 또는 실제 게임 일자 진행까지 실행 검증한 설명
- 감사 원본 근거: [ClimateDomain.cs 101행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs:101>): 보호5일·맑음·잡음0·20℃ / [ClimateDomain.cs 158행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs:158>): 보호기간 및 정상 전환 / [ClimateDomain.cs 184행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs:184>): 연중일과sin식 / [ClimateDomain.cs 261행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs:261>): 잡음−2+4×unit / [CoreSessionContracts.cs 24행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/CoreSessionContracts.cs:24>): 계절30일·년120일 / [ClimateRuntime.cs 146행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs:146>): 일일갱신 실행
- 감사 위키 근거: [weather-seasons-and-environment.md 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:23>): 사인/날씨/잡음 개념은 이미 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 기본 시작 기후 선택과 실제 런 일자 진행은 미실행.
- 관련 GAP(제외 이력 포함): GAP-089
- 이번 재검토: 공개 가이드는 이미 외기식의 개념을 설명하지만 첫5일 보호와 정확한 날짜식·잡음 범위는 없다. 해당 수치만 추가 공백으로 좁힌다.

## GAP-088 날씨 교체 시점·계절 가중치·지속일 선택 규칙 누락

- 분류: 확률의 의미·상태 전이 누락
- 보완할 문서: weather-seasons-and-environment
- 현재 확인한 차이: 현재6개 날씨의 기간·계절가중치를 직접 재합산했고 계절합100·겨울폭염0이 유지된다. 도감에는 값이 있으나 교체 시점·재선택·확률 의미는 없다.
- **위키에 작성할 정보:**
  - 보호기간 이후에는 현재 날씨의 남은 일수를 하루마다1 줄여0이 되면 그날의 계절 가중치로 날씨를 다시 고른다. 계절이 바뀌었다는 이유만으로 진행 중 날씨를 즉시 교체하지 않는다.
  - 양수 가중치만 후보이며 선택 확률은 가중치/합계다. 현재6개 날씨의 가중치 합은 각 계절100이고 겨울 폭염은0이라 추첨에서 제외된다.
  - 선택한 날씨의 지속기간은 작성된 최소~최대 정수 일수에서 별도로 고른다. 직전 날씨를 제외하지 않아 같은 날씨가 다시 선택될 수 있다.
  - 가중치는 교체 때의 추첨 확률이지 달력상의 점유 일수 비율이 아니다. 기후·현재 날씨·남은 일수·일일 잡음은 저장한다.
- **위키에 작성하지 않을 정보:**
  - 난수 스트림까지 포함한 저장 왕복을 실행 검증한 설명
- 감사 원본 근거: [ClimateDomain.cs 165행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs:165>): 잔여일 감소와 교체 / [ClimateDomain.cs 237행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs:237>): 양수가중치 후보·현재날씨 제외없음 / [ClimateDomain.cs 255행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs:255>): 최소~최대 정수 기간 / [ClimateDomain.cs 195행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ClimateDomain.cs:195>): 기후·날씨·잔여일·잡음 저장 / [WeatherFront_clear.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/WeatherFront_clear.asset:18>): 최소·최대 기간과 사계절 가중치 현재값 재독 / [WeatherFront_cold-snap.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/WeatherFront_cold-snap.asset:18>): 최소·최대 기간과 사계절 가중치 현재값 재독 / [WeatherFront_fog.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/WeatherFront_fog.asset:18>): 최소·최대 기간과 사계절 가중치 현재값 재독 / [WeatherFront_heatwave.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/WeatherFront_heatwave.asset:18>): 최소·최대 기간과 사계절 가중치 현재값 재독 / [WeatherFront_rain.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/WeatherFront_rain.asset:18>): 최소·최대 기간과 사계절 가중치 현재값 재독 / [WeatherFront_storm.asset 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/World/Climate/WeatherFront_storm.asset:18>): 최소·최대 기간과 사계절 가중치 현재값 재독
- 감사 위키 근거: [weather-seasons-and-environment.md 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:25>): 기후 준비만 / [weather-heatwave.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/weather-heatwave.json:2>): 가중치·기간수치만 / [weather-clear.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/weather-clear.json:2>): 선택시점 미설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 정적6개 날씨 비교이며 저장 난수 왕복은 미실행.
- 이번 재검토: 현재6개 날씨의 기간·계절가중치를 직접 재합산했고 계절합100·겨울폭염0이 유지된다. 도감에는 값이 있으나 교체 시점·재선택·확률 의미는 없다.

## GAP-090 기상 관측탑의 장비·내구도·예보 표시 조건 누락

- 분류: 시설 운영·도구 수치·표시 의미 누락
- 보완할 문서: weather-seasons-and-environment / entry/facility/building-8851 / observation item entries
- 현재 확인한 차이: 실물 장비 공급·내구/일일 마모·현재 날씨 잔여일 기반 표시가 실제 호출 경로에 있다. 공개 탑은 생산 작업대로 요약하고 장비 도감은 관측 내구 역할을 누락한다.
- **위키에 작성할 정보:**
  - 기상 관측탑은 계절 역서1개와 기상 관측 도구1개를 실물 장비 슬롯에 공급해야 관측 장비가 가동한다.
  - 계절 역서의 내구도는180·일일 마모0.25, 기상 관측 도구는 내구도120·일일 마모1이다. 정상 일일 마모만 계산하면 각각720회·120회분이다.
  - 관측 장비가 가동할 때 화면의 예보 일수는 현재 날씨 남은 일수를 최소1·최대3일로 제한한 값이며, 장비가 가동하지 않으면0이다.
  - 일일 장비 유지 대상은 운영 가능한 관측탑을 지속 식별자 순으로 정렬한 첫 탑이다.
- **위키에 작성하지 않을 정보:**
  - 실제 미래 날씨 세 개를 미리 확정해 예측한다는 설명
  - 관측탑을 생산 작업대로만 설명하는 내용
  - 건설 직후 갱신 지연·장비 교체/고갈·다중탑 저장 왕복을 재현했다고 쓰는 내용
- 감사 원본 근거: [ClimateDurableEquipmentPolicySource.cs 28행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateDurableEquipmentPolicySource.cs:28>): 역서·도구 각각1 / [ClimateDurableEquipmentRuntime.cs 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateDurableEquipmentRuntime.cs:11>): 일일마모0.25/1 / [ClimateDurableEquipmentRuntime.cs 52행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateDurableEquipmentRuntime.cs:52>): 슬롯 공급·일일마모 / [ItemPrimitives.cs 400행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Items/Core/ItemPrimitives.cs:400>): 내구180/120 / [ClimateRuntime.cs 106행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs:106>): 장비 가동과예보min3max1 / [ClimateRuntime.cs 153행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ClimateRuntime.cs:153>): firstID관측탑 일일유지 / [UIManager.cs 111행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/UI/UIManager.cs:111>): 예보일수 HUD
- 감사 위키 근거: [building-8851.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8851.json:19>): 생산작업대 오해 / [book-seasonal-almanac.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/book-seasonal-almanac.json:2>): 장비/내구정보 없음 / [tool-weather-observation-kit.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/tool-weather-observation-kit.json:2>): 장비/내구정보 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 건설 직후 갱신 지연·교체/고갈·다중 탑 저장 왕복은 미실행.
- 관련 GAP(제외 이력 포함): GAP-042
- 이번 재검토: 실물 장비 공급·내구/일일 마모·현재 날씨 잔여일 기반 표시가 실제 호출 경로에 있다. 공개 탑은 생산 작업대로 요약하고 장비 도감은 관측 내구 역할을 누락한다.

## GAP-091 계절 사건의 결정적 선정·연도 초기화·마감일 규칙 누락

- 분류: 발생 조건·수명주기 누락
- 보완할 문서: weather-seasons-and-environment / events-and-choices
- 현재 확인한 차이: 순차 진행은 이미 공개되어 있다. 남은 차이는 후보의 결정적 선정, 연도 전환 시 완료 후보 초기화, 마감일 당일 유지이다. 현재 코드에는 표류 화물 만료 정산이 끝나지 않으면 마감일 경과 뒤에도 제거를 미루는 예외가 추가되어 있다.
- **위키에 작성할 정보:**
  - 후보는 실행 시드·날짜·사건 ID로 정해지는 고정 순서에서 선정되며 같은 조건이면 같은 결과가 된다. 완료한 계절 사건의 후보 제외 목록은 게임 연도가 바뀌면 초기화된다.
  - 마감일은 시작일+지속일이며 마감일 당일까지 활성 상태다. 표류 화물 사건은 기한이 지나도 만료 정산이 끝날 때까지 제거가 보류될 수 있다.
- **위키에 작성하지 않을 정보:**
  - 모든 계절 사건이 기한 다음 날 반드시 즉시 사라진다는 설명
- 감사 원본 근거: [V20CampaignRuntime.cs 4982행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:4982>): 일일 평가·연도 완료목록 초기화·만료 및 화물 정산 예외 / [V20CampaignRuntime.cs 5041행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5041>): 후보 필터와 duration 선택 / [V20CampaignRuntime.cs 5934행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5934>): 마감일=start+duration / [V20CampaignRuntime.cs 6391행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:6391>): 지속일 EventRoll에는 참가자 포함; 선정 hash는6401별도
- 감사 위키 근거: [events-and-choices.md 74행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/events-and-choices.md:74>): 진행 중 계절 사건 종료 뒤 다음 후보 평가가 이미 있음 / [weather-seasons-and-environment.md 47행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:47>): 작성 개수/기간/영향영역만 표시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체 계절 사건의 개별 발동 조건·효과를 전수 재인증하지 않았다.
- 이번 재검토: 순차 진행은 이미 공개되어 있다. 남은 차이는 후보의 결정적 선정, 연도 전환 시 완료 후보 초기화, 마감일 당일 유지이다. 현재 코드에는 표류 화물 만료 정산이 끝나지 않으면 마감일 경과 뒤에도 제거를 미루는 예외가 추가되어 있다.

## GAP-092 떠돌이 종자상의 시작 호의 효과와 사건 효과 단계 누락

- 분류: 도감 효과 누락·작성 의도와 실행의 혼동
- 보완할 문서: entry/event/seasonal-* / weather-seasons-and-environment
- 현재 확인한 차이: 떠돌이 종자상은 시작 시 균사 세력 호의를 4 올리지만 해당 도감에는 기간·종료효과 개수만 있다. 축제 식재료 경쟁의 -120은 작성값은 확인했으나 현재 금액 결제 경로는 이번 승인 범위에서 제외한다.
- **위키에 작성할 정보:**
  - 떠돌이 종자상은 사건 시작 때 균사 세력 호의를 +4 적용한다. 시작 효과, 매일 효과, 종료 효과는 서로 다른 단계이며 이 사건의 일일 효과는 없다.
- **위키에 작성하지 않을 정보:**
  - 영향 영역·강도 메타데이터만으로 물 동결·해충·발화 등이 실행된다고 단정하는 설명
  - 축제 식재료 경쟁이 실제로 금화120을 결제한다는 미검증 설명
- 감사 원본 근거: [V20CampaignRuntime.cs 5078행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5078>): 선정 사건 startEffects 실제 적용 / [V20CampaignRuntime.cs 5466행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5466>): FactionRapport→ApplyFactionChange / [seasonal_spring-seed-exchange.asset 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-seed-exchange.asset:37>): 균사호의+4/기간2/일일없음 / [seasonal_summer-festival-scarcity.asset 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_summer-festival-scarcity.asset:37>): 금액-120은 작성값만 확인
- 감사 위키 근거: [weather-seasons-and-environment.md 47행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:47>): 영향영역과 강도의 작성 목표를 실제 사건 효과처럼 서술 / [seasonal-spring-seed-exchange.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-spring-seed-exchange.json:2>): 본문전체는기간/종료효과개수만;호의+4없음 / [seasonal-summer-festival-scarcity.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/seasonal-summer-festival-scarcity.json:2>): 시작금액-120없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체 사건 수·누락 수·Threat 후속 소비자를 전수 재계산하지 않았다.
- 이번 재검토: 떠돌이 종자상은 시작 시 균사 세력 호의를 4 올리지만 해당 도감에는 기간·종료효과 개수만 있다. 축제 식재료 경쟁의 -120은 작성값은 확인했으나 현재 금액 결제 경로는 이번 승인 범위에서 제외한다.

## GAP-093 사건 작업 지연의 기간·범위·속도 배율 누락

- 분류: 업무 속도·범위·기간 공식 누락
- 보완할 문서: events-and-choices / residents-and-work
- 현재 확인한 차이: 위키는 후속 결과 일반 설명만 있고 실제 지연의 같은 범위 기간 연장과 서로 다른 활성 지연 배율을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 같은 작업 범위의 양수 지연은 기존 만료일과 오늘 중 늦은 날에 기간을 더한다. 음수 지연은 남은 기간을 줄이고 만료한 지연은 제거된다.
  - 해당 작업에 걸리는 활성 지연 n개의 공통 속도 배율은 max(0.5, 0.8^n)이다. 홍수 범위는 파종·수확·운반·시설 재고 보충에 적용된다.
- **위키에 작성하지 않을 정보:**
  - 같은 범위 효과를 여러 번 받으면 매번 별도0.8배 중첩이 생긴다는 설명
- 감사 원본 근거: [V20CampaignRuntime.cs 1544행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:1544>): 공통지연0.8/최저0.5 / [V20CampaignRuntime.cs 2621행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:2621>): 해당 범위 활성 지연수→속도 / [V20CampaignRuntime.cs 5522행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5522>): 동일scope 만료연장·단축 / [V20CampaignRuntime.cs 5558행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5558>): flood 네 capability 매핑
- 감사 위키 근거: [events-and-choices.md 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/events-and-choices.md:42>): 일반 후속 결과만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 사건83개 등 작성 모집단의 전수 수는 재계산하지 않았다. 별도 엔드리스 물류 보정은 이 공통 지연 배율과 구분한다.
- 이번 재검토: 위키는 후속 결과 일반 설명만 있고 실제 지연의 같은 범위 기간 연장과 서로 다른 활성 지연 배율을 설명하지 않는다.

## GAP-094 현재 종족 11종의 쾌적·안전·치명 온도66값 누락

- 분류: 종족별 수치·환경 조건 누락
- 보완할 문서: species-culture-and-life / 종족 도감
- 현재 확인한 차이: 현재 종족 에셋은 인간을 포함해11종이다. 기존10종 전제로 고정된 제목·작성 범위는 오래되었다. 종족 온도6필드를 실제 환경 판정에 사용하지만 종족 도감은 이 수치를 제공하지 않는다.
- **위키에 작성할 정보:**
  - 각 종족의 쾌적/안전/치명 온도 범위를 °C로 표기한다: 모험가15~27/0~40/-10~48, 수인10~29/-4~40/-12~48, 악마20~34/10~46/-2~56, 골렘-5~35/-20~50/-35~65, 하피7~25/-5~36/-15~44, 인간15~27/0~40/-10~48.
  - 코볼트11~28/-2~40/-10~48, 균사인8~22/0~32/-8~40, 오크12~30/-5~42/-15~50, 슬라임16~24/5~34/0~40, 뱀파이어8~22/0~34/-10~42. 수치는 보호 보정 전 종족 기본 범위이다.
- **위키에 작성하지 않을 정보:**
  - 현재 종족 정의가10개뿐이라는 설명
  - 안전 범위라는 이유로 모든 환경 피해가 없어지거나 모든 종족이 시작 선택 가능하다는 설명
- 감사 원본 근거: [Species_Adventurer.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Adventurer.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Beastkin.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Beastkin.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Demon.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Demon.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Golem.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Golem.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Harpy.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Harpy.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Human.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Human.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Kobold.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Kobold.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Myconid.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Myconid.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Orc.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Orc.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Slime.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Slime.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [Species_Vampire.asset 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/Species/Species_Vampire.asset:42>): 쾌적/안전/치명 최소·최대6필드 현재 확인 / [CharacterEnvironmentRuntime.cs 520행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:520>): 종족열프로필+보호→온도율 판정 / [GameDomainContentCatalog.asset 708행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/GameDomainContentCatalog.asset:708>): 새 인간 종족 GUID 등록
- 감사 위키 근거: [characterspecies-1.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/characterspecies-1.json:2>): 10종 facts에서 온도6필드 누락 확인 / [species-culture-and-life.md 22행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md:22>): 기후 적응 일반 항목만 제공 / [characterspecies-11.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/characterspecies-11.json:2>): 인간 도감 본문에 온도6필드 없음
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 현재11개 온도필드·카탈로그 인간 연결·환경 소비를 정적으로 확인했다. Unity 플레이·전체 종족 등장 경로는 인증하지 않았다.
- 이번 재검토: 현재 종족 에셋은 인간을 포함해11종이다. 기존10종 전제로 고정된 제목·작성 범위는 오래되었다. 종족 온도6필드를 실제 환경 판정에 사용하지만 종족 도감은 이 수치를 제공하지 않는다.

## GAP-095 온도 보호의 치명 경계 제한과 대표 작업복 선택 누락

- 분류: 보정 공식·중첩·상한 누락
- 보완할 문서: weather-seasons-and-environment
- 현재 확인한 차이: 보호가 치명 온도 자체를 옮기는 것이 아니라 안전·쾌적 범위를 제한 안에서 보정한다는 차이가 위키에 없다. 현재 보호 합성에는 특성·원단 재질·대표 작업복이 들어간다.
- **위키에 작성할 정보:**
  - 보호 보정 후 안전 최저는 치명 최저+2°C보다 낮아질 수 없고 안전 최고는 치명 최고-2°C보다 높아질 수 없다. 쾌적 경계도 보정된 안전·치명 경계 안으로 제한되며 치명 최저·최고 자체는 바뀌지 않는다.
  - 장착 작업복은 방한 제공 여부, 쾌적 최저 보정이 낮은 순서, 냉기 노출 배율이 낮은 순서, 인스턴스 ID 순서로 대표 한 벌을 정한다. 선택 특성 보호·의복 재질 보호·대표 작업복 보호를 합성한다.
- **위키에 작성하지 않을 정보:**
  - 작업복 한 벌마다 정의상 보호 전체가 무조건 중첩된다는 설명
- 감사 원본 근거: [CharacterEnvironmentModels.cs 16행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentModels.cs:16>): 안전/쾌적 clamp·치명 유지 / [EnvironmentalWorkwearRuntime.cs 635행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs:635>): 대표 작업복 정렬 / [EnvironmentalWorkwearRuntime.cs 755행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs:755>): 특성+재질+대표작업복 합성
- 감사 위키 근거: [weather-seasons-and-environment.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:31>): 의복·휴식 공간을 운영 항목으로만 제시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 의복 재질별 보호 환산식 전체는 이 항목의 승인 범위 밖이다.
- 이번 재검토: 보호가 치명 온도 자체를 옮기는 것이 아니라 안전·쾌적 범위를 제한 안에서 보정한다는 차이가 위키에 없다. 현재 보호 합성에는 특성·원단 재질·대표 작업복이 들어간다.

## GAP-096 환경 작업복4종의 보호 수치와 종족 기본 온도 조건 누락

- 분류: 장비 수치 누락·설명 조건 부족
- 보완할 문서: 환경 작업복 도감 / species-culture-and-life
- 현재 확인한 차이: 도감의 필요 연구·8°C 근무 요약에는 실제 보호6값과 종족 기본 온도에 따른 차이가 없다.
- **위키에 작성할 정보:**
  - 쾌적최저/안전최저 보정과 냉기노출배율은 방한작업복 -8/-8°C·0.35배, 룬방한복 -10/-10°C·0.2배, 슬라임보온패드 -4/-4°C·0.6배, 운반하네스0/0°C·1배다. 네 장비 모두 쾌적최고·안전최고 보정0, 열노출배율1이다.
  - 이 값은 종족 기본 온도 프로필에 적용되는 보호 보정이므로 같은 작업복·8°C라도 종족별 결과가 같다고 보장하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 8°C에서 어떤 종족도 무조건 상시 안전하다는 설명
- 감사 원본 근거: [ColdWorkSuit.asset 32행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Environment/Workwear/ColdWorkSuit.asset:32>): 보호6필드 현재 대조 / [RuneColdSuit.asset 32행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Environment/Workwear/RuneColdSuit.asset:32>): 보호6필드 현재 대조 / [SlimeWarmingPad.asset 33행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Environment/Workwear/SlimeWarmingPad.asset:33>): 보호6필드 현재 대조 / [HaulingHarness.asset 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Environment/Workwear/HaulingHarness.asset:31>): 보호6필드 현재 대조 / [EnvironmentalWorkwearRuntime.cs 771행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs:771>): 대표 장착 작업복 Protection 소비
- 감사 위키 근거: [workwear-cold-work-suit.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/combat/workwear-cold-work-suit.json:2>): facts에는 필요 연구만 / [workwear-cold-work-suit.json 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/combat/workwear-cold-work-suit.json:34>): 8°C 상시 근무 요약은 종족 조건을 표시하지 않음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: allowedSpecies 전 필드의 UI·장착 게이트 일치는 별도 확인 대상이다.
- 이번 재검토: 도감의 필요 연구·8°C 근무 요약에는 실제 보호6값과 종족 기본 온도에 따른 차이가 없다.

## GAP-097 정식침대·이층침대의 휴식 배정 시 방한 보정 누락

- 분류: 시설 효과·조건·수치 누락
- 보완할 문서: 침대 도감 / weather-seasons-and-environment
- 현재 확인한 차이: 실제 조건은 Rest 작업 배정과 시설 Rest 역할이며 실제 수면 위치 검사와 동일하지 않다. 위키는 휴식 공간 일반론만 제공한다.
- **위키에 작성할 정보:**
  - 휴식 작업에 배정되어 있고 배정 시설이 휴식 역할을 지원할 때 정식침대는 쾌적최저-5°C·안전최저-2.5°C, 이층침대는 각각-4°C·-2°C를 더한다. 두 경우 냉기노출배율은0.6을 곱한다.
- **위키에 작성하지 않을 정보:**
  - 침대가 방 전체 온도를 올리거나 실제 수면 위치 확인 뒤에만 이 보정이 생긴다는 설명
- 감사 원본 근거: [CharacterEnvironmentRuntime.cs 615행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:615>): Rest 배정+역할 검사와-보호/-.5보호/*.6 / [R02_정식침대.asset 182행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/R02_정식침대.asset:182>): coldProtection 5 / [R03_이층침대.asset 184행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/R03_이층침대.asset:184>): coldProtection 4
- 감사 위키 근거: [weather-seasons-and-environment.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:31>): 휴식 공간만 언급
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 방 가열·냉각의 별도 실행은 이 침구 보정으로 인증하지 않는다.
- 이번 재검토: 실제 조건은 Rest 작업 배정과 시설 Rest 역할이며 실제 수면 위치 검사와 동일하지 않다. 위키는 휴식 공간 일반론만 제공한다.

## GAP-098 의복의 체형·크기·부위·개구부 장착 조건 누락

- 분류: 착용·교체 규칙 및 도감 필드 누락
- 보완할 문서: species-culture-and-life / 의복 아이템 도감
- 현재 확인한 차이: 일반 의복 소개는 실제 장착 거절 조건을 설명하지 않는다.56종 전체의 개별 설정 대신 공통 장착 판정만 남긴다.
- **위키에 작성할 정보:**
  - 의복 체형은 Any 또는 착용자의 실제 체형과 맞아야 하고 필요한 해부 부위가 모두 있어야 한다. 정사이즈형은 크기가 같아야 하며 조절형은 크기 차이1단계까지 허용한다.
  - 봉인되는 특수 부위에 필요한 개구부는 해당 개조가 존재하고 닫히지 않은 상태여야 한다. 슬라임을 포함한 생물 주민은 기본 인간형 착용 표면을 가지며 실제 결손 부위는 착용 가능 부위에서 제외된다.
- **위키에 작성하지 않을 정보:**
  - 56종의 모든 장착·겹침·자동선택 경로가 검증되었다는 설명
- 감사 원본 근거: [CharacterApparelRuntime.cs 69행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterApparelRuntime.cs:69>): 체형/부위/크기/개구부 판정 / [CharacterApparelRuntime.cs 148행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterApparelRuntime.cs:148>): 기본표면과결손부위 / [EnvironmentalWorkwearRuntime.cs 858행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs:858>): anatomy.CanEquip 직접 소비
- 감사 위키 근거: [species-culture-and-life.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md:35>): 의복 수량·세탁 목표·원단 일반 설명만 존재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 공통 CanEquip 및 직접 호출을 정적으로 확인했다. 전체 UI·자율 장착 실행은 재현하지 않았다.
- 이번 재검토: 일반 의복 소개는 실제 장착 거절 조건을 설명하지 않는다.56종 전체의 개별 설정 대신 공통 장착 판정만 남긴다.

## GAP-099 의복 원단 후보·정렬·단일 원단 수량 선택 규칙 누락

- 분류: 재료 비교·선택 규칙 누락 및 적용 상태 확인 필요
- 보완할 문서: production-quality-and-supply / species-culture-and-life / 원단 아이템 도감
- 현재 확인한 차이: 위키 원단 소개에는 실제 재료 후보 필터와 정책별 정렬이 없다. 취급 난이도 정책은 현재 난이도 수치가 아니라 MaterialId로 정렬하므로 정책 이름만으로 기능을 설명하면 잘못이다.
- **위키에 작성할 정보:**
  - 금지되지 않고 사용 가능 수량이 있으며 세탁·오염 조건을 통과하고 의복 허용 재질 태그에 맞는 물리 원단만 후보가 된다. 지정 원단 정책은 지정 ID만 선택한다.
  - 최저가·최고보온·최저중량·최고내구 정책은 각각 가격 오름차순·보온 내림차순·중량배율 오름차순·내구 내림차순으로 정렬하고 같은 원단 종류만 묶어 필요 수량을 채운다.
- **위키에 작성하지 않을 정보:**
  - 취급 난이도 정책이 실제 HandlingDifficulty 수치를 비교한다는 설명
  - 원단12종 모든 비교 수치가 확인되었다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 2087행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:2087>): 현재 관련 본문 확인
- 감사 위키 근거: [species-culture-and-life.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md:35>): 의복 수량·세탁 목표·원단 일반 설명만 존재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 기타 정책의 MaterialId 정렬은 구현 상태로 분리 기록한다. 전체 원단표 및 각 재질 효과 소비는 이 항목에서 인증하지 않는다.
- 이번 재검토: 위키 원단 소개에는 실제 재료 후보 필터와 정책별 정렬이 없다. 취급 난이도 정책은 현재 난이도 수치가 아니라 MaterialId로 정렬하므로 정책 이름만으로 기능을 설명하면 잘못이다.

## GAP-100 의복 세탁·건조의 배치 작업량과 완료 상태 누락

- 분류: 생활 관리 실행 규칙·시설 역할 누락
- 보완할 문서: species-culture-and-life / 세탁·건조 시설 도감
- 현재 확인한 차이: 위키의 세탁 대상 설명에는 완료 후 수분·오염 처리와 배치 전체 작업량이 없다.
- **위키에 작성할 정보:**
  - 유효한 물리 의복을 중복 제거하여 세탁·건조 배치를 만든다. 동력 세탁은 배치4WU, 손세탁12WU, 건조24WU이며 품목 수를 곱하지 않는다.
  - 손세탁 완료는 오염0·수분100, 동력 세탁 완료는 오염0·수분0, 건조 완료는 수분0으로 만들고 기존 오염은 유지한다.
- **위키에 작성하지 않을 정보:**
  - 현재 의복 작업이 승인 노동을 전혀 소비하지 않는다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 557행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:557>): 세탁4/12·건조24WU / [ApparelWorkOrderRuntime.cs 1506행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1506>): 대상중복제거·물리의복검사 / [ApparelWorkOrderRuntime.cs 1539행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1539>): 배치 requiredWork=work / [ApparelWorkOrderRuntime.cs 1876행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1876>): 완료 수분·오염 분기 / [ResearchFacilityOperationFallbackHandler.cs 153행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Research/ResearchFacilityOperationFallbackHandler.cs:153>): 현재는 context.ApprovedWork만 ApplyWork에 전달
- 감사 위키 근거: [species-culture-and-life.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md:35>): 의복 수량·세탁 목표·원단 일반 설명만 존재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: Unity 작업 실행 및 전체 저장 복원은 재현하지 않았다. 최대 배치 수 상수는 이 작성 범위에 포함하지 않는다.
- 이번 재검토: 위키의 세탁 대상 설명에는 완료 후 수분·오염 처리와 배치 전체 작업량이 없다.

## GAP-101 의복 수선의 내구도 경계·재료·회복량 누락

- 분류: 수치·비용·대상 조건 누락
- 보완할 문서: species-culture-and-life / 수선 접수대·수선 재료 도감
- 현재 확인한 차이: 위키에는 내구도20·60에 따른 수선 가능 여부와 재료·결과가 없다.
- **위키에 작성할 정보:**
  - 내구도20 미만은 수선 주문 대상이 될 수 없다. 내구도20이상60미만은 재봉실1+수선천1과18WU가 들고 완료 내구도70이 된다.
  - 내구도60이상은8WU이며 완료 시 기존 내구도+25, 최대100으로 회복한다.
- **위키에 작성하지 않을 정보:**
  - 모든 내구도가 같은 비용·같은 회복량으로 수선된다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 586행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:586>): 현재 관련 본문 확인 / [ResearchFacilityOperationFallbackHandler.cs 153행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Research/ResearchFacilityOperationFallbackHandler.cs:153>): 현재는 context.ApprovedWork만 ApplyWork에 전달
- 감사 위키 근거: [species-culture-and-life.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md:35>): 의복 수량·세탁 목표·원단 일반 설명만 존재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 물리 대상 선택과 승인 노동 경로는 정적 확인이다. 플레이·복원 재현은 하지 않았다.
- 이번 재검토: 위키에는 내구도20·60에 따른 수선 가능 여부와 재료·결과가 없다.

## GAP-102 탈의 칸막이의 기존 개구부 여닫기와 새 개조 구분 누락

- 분류: 사용법·변경 범위 누락
- 보완할 문서: species-culture-and-life / 탈의 칸막이 도감
- 현재 확인한 차이: 짧은 옷장 작업은 기존 개조 범위의 닫힘 상태만 바꾸며 새 개구부를 만들거나 크기를 바꾸지 않는다.
- **위키에 작성할 정보:**
  - 탈의 칸막이의 짧은 작업은 이미 존재하는 개구부만 닫거나 다시 연다. 새 개구부 개조와 크기 변경은 별도 개조 작업으로 처리한다.
- **위키에 작성하지 않을 정보:**
  - 탈의 칸막이만으로 새 개구부를 자르거나 의복 크기를 바꿀 수 있다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 2050행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:2050>): 현재 관련 본문 확인
- 감사 위키 근거: [species-culture-and-life.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md:35>): 의복 수량·세탁 목표·원단 일반 설명만 존재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 선택 UI와 모든 개조 정의를 전수 인증하지 않았다.
- 관련 GAP(제외 이력 포함): GAP-098
- 이번 재검토: 짧은 옷장 작업은 기존 개조 범위의 닫힘 상태만 바꾸며 새 개구부를 만들거나 크기를 바꾸지 않는다.

## GAP-103 의복 주문의 일반 실패·재시도·복원 경계 누락

- 분류: 상태 전이·실패·저장 규칙 누락
- 보완할 문서: species-culture-and-life (world-state에서 참조)
- 현재 확인한 차이: 위키는 일반 실패의 진척 초기화 및 재시도 간격을 설명하지 않는다. 모든 대기 상태를 같은 실패 처리로 일반화하면 안 된다.
- **위키에 작성할 정보:**
  - 일반적인 예약·시설 실패로 대기 상태로 돌아가면 예약을 해제하고 해당 주문의 작업 진척을0으로 되돌린다. 재시도 간격은0.25→0.5→1게임시간으로 늘어난다.
  - 일반 저장 주문 목록에는 완료 주문을 제외한다. 출력·최종 수선 확정 대기는 일반 실패와 구분하며 복원 시 재검증 또는 최종 확정 대기로 나뉜다.
- **위키에 작성하지 않을 정보:**
  - 출력 대기를 포함한 모든 대기 전환이 무조건 진척0이 된다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 2221행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:2221>): 현재 관련 본문 확인 / [ApparelWorkOrderRuntime.cs 333행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:333>): retry 간격3개 / [ApparelWorkOrderRuntime.cs 872행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:872>): 일반 Capture Completed 제외; terminal 별도capture / [ApparelWorkOrderRuntime.cs 992행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:992>): 복원 상태 분기와 retry시각0
- 감사 위키 근거: [species-culture-and-life.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md:35>): 의복 수량·세탁 목표·원단 일반 설명만 존재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체 저장 왕복·물리 출력 성공을 실행 검증하지 않았다.
- 이번 재검토: 위키는 일반 실패의 진척 초기화 및 재시도 간격을 설명하지 않는다. 모든 대기 상태를 같은 실패 처리로 일반화하면 안 된다.

## GAP-104 의복 맞춤 제작의 원단 수량·크기·개조별 작업량 누락

- 분류: 재료량·작업량 공식 누락
- 보완할 문서: production (species-culture-and-life·의복 도감에서 참조)
- 현재 확인한 차이: 제작 주문이 사용하는 원단 수량과 계산기에 위임하는 작업량 공식이 위키에 없다.
- **위키에 작성할 정보:**
  - 필요 원단은 max(1,ceil(2×재봉계수))개다. 계산기의 기본 작업량은 [10+12×면적등급+4×착용부위수+개조WU]×크기배율×원단작업배율이며 면적등급은 ceil(재봉계수)를1~5로 제한한다.
  - 꼬리 구멍+4WU·날개 틈+8WU·뿔 여유+3WU를 적용하고 크기 배율은 소형0.75·보통1·대형1.30이다. 결과를 가장 가까운2WU 단위로 반올림하고 최소2WU로 한다.
- **위키에 작성하지 않을 정보:**
  - 개별 의복56종과 모든 원단 조합의 계산 결과를 전수 검증했다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 498행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:498>): 필요원단 max1ceil2계수 / [ApparelWorkOrderRuntime.cs 526행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:526>): 계산기 위임 및 fallback / [V23BalanceWorkCalculator.cs 187행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/V23BalanceWorkCalculator.cs:187>): 면적·부위·개조·크기·원단 WU 공식 / [V23BalanceWorkCalculator.cs 312행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/V23BalanceWorkCalculator.cs:312>): 2WU반올림·최소2 / [V27BalanceWorkCalculator.cs 53행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs:53>): 의복 계산은 before로 위임
- 감사 위키 근거: [species-culture-and-life.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md:35>): 의복 수량·세탁 목표·원단 일반 설명만 존재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 계산기가 없는 fallback22×max(0.5,재봉계수)는 정상 주입 경로의 표준 공식과 분리한다. 전체 DI/플레이 조합은 인증하지 않았다.
- 이번 재검토: 제작 주문이 사용하는 원단 수량과 계산기에 위임하는 작업량 공식이 위키에 없다.

## GAP-105 의복 품질의 시설 숙련·복잡도 보정 누락

- 분류: 공통 공식에 들어가는 의복별 수치 누락
- 보완할 문서: production-quality-and-supply
- 현재 확인한 차이: 공통 품질 소개는 의복 주문의 시설 숙련·복잡도 인수를 설명하지 않는다.
- **위키에 작성할 정보:**
  - 의복 품질 판정의 시설 보정은 (시설 제작숙련점수-50)×0.08이고 도구 보정은0이다. 복잡도 페널티 인수는 max(0,재봉계수-1)×4다. 유효한 가중 관련 숙련이 없으면 숙련50을 사용한다.
- **위키에 작성하지 않을 정보:**
  - 모든 시설58개와 작업자 기여 결과를 전수 확인했다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 1583행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1583>): 현재 관련 본문 확인
- 감사 위키 근거: [production-quality-and-supply.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:39>): 공통 목표 품질 규칙만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 가중 숙련 누적·전체 시설 설정 전수는 이 행의 승인 범위에서 제외한다.
- 이번 재검토: 공통 품질 소개는 의복 주문의 시설 숙련·복잡도 인수를 설명하지 않는다.

## GAP-106 신들린 영감의 신화 조건·반복 권태·성공 초기화 누락

- 분류: 특성 조건·확률·기분 효과 누락
- 보완할 문서: character/trait-300 (production-quality-and-supply에서 참조)
- 현재 확인한 차이: 위키 공통 품질 설명에는 직접 의복 경로의3%·기여60% 조건과 반복 기분 규칙이 없다. 현재 신화 성공 시 반복 상태 자체를 초기화하는 점도 기존 문장에 빠졌다.
- **위키에 작성할 정보:**
  - 신화 제작을 허용한 의복에서 마지막 기여자가 신들린 영감 특성을 갖고 작업 기여 비중60%이상이면 고정 판정값으로3% 신화 가능성을 평가한다.
  - 같은 품목은2회까지 권태가 없고 이후 회마다 기분-2씩, 최저-10이 적용된다. 품목이 바뀌거나48시간이 지나면 반복 횟수가 초기화된다. 신화 성공도 반복 횟수를0으로 초기화하며 기분+10을2일 부여한다.
- **위키에 작성하지 않을 정보:**
  - 모든 제작·무기·방어구 경로에서 같은 특성이 검증되었다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 1683행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1683>): 현재 관련 본문 확인 / [Trait_300_possessed-inspiration.asset 63행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_300_possessed-inspiration.asset:63>): 3%/60%/2회무료/-2/-10/48시간/+10/2일 / [ApparelWorkOrderRuntime.cs 1590행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1590>): 마지막기여자/비중/허용정의/고정hash / [CharacterIdentityRuntime.cs 1184행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityRuntime.cs:1184>): 48시간/품목 변경 초기화;1214신화 성공 초기화·기분
- 감사 위키 근거: [production-quality-and-supply.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:39>): 공통 목표 품질 규칙만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 직접 의복·특성 기분 경로의 정적 확인이다. 전체 확률 분포나 모든 제작 경로는 실행 검증하지 않았다.
- 이번 재검토: 위키 공통 품질 설명에는 직접 의복 경로의3%·기여60% 조건과 반복 기분 규칙이 없다. 현재 신화 성공 시 반복 상태 자체를 초기화하는 점도 기존 문장에 빠졌다.

## GAP-107 의복 반복 제작의 목표 수량·시도 안전 한도·작업 예산 누락

- 분류: 주문 설정·중단 조건 누락
- 보완할 문서: production-quality-and-supply
- 현재 확인한 차이: 공통 목표 품질 안내에는 다음 반복 진입 전 안전 한도 검사와 누적 작업 예산이 없다.
- **위키에 작성할 정보:**
  - 합격품 목표 수량을 채우면 반복을 끝낸다. 안전 한도 모드는 현재 반복 시도 단계와 누적 제작·해체 작업량이 한도에 닿으면 다음 반복을 중지하며 무제한 모드는 이 안전 한도를 사용하지 않는다.
  - 안전 한도는 다음 반복의 중단 기준이며 설정 숫자만큼 불합격품의 제작·해체를 모두 완결한다는 보장이 아니다.
- **위키에 작성하지 않을 정보:**
  - 기본 안전 한도가 실패품10회의 완결을 보장한다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 1866행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1866>): 현재 관련 본문 확인 / [ApparelWorkOrderRuntime.cs 538행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:538>): 시도index0 초기화 / [ApparelWorkOrderRuntime.cs 1706행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1706>): 제작WU합산 및 합격 목표 먼저 검사 / [ApparelWorkOrderRuntime.cs 1817행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1817>): 해체WU 누적;1836++ 이후 한도 검사
- 감사 위키 근거: [production-quality-and-supply.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:39>): 공통 목표 품질 규칙만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든 실패 조합의 실시간 반복·저장 왕복은 재현하지 않았다. / 정확한 완결 횟수는 실패·해체·작업예산 경계에 따라 달라 이 작성 문안에 단일 횟수로 보장하지 않는다.
- 이번 재검토: 공통 목표 품질 안내에는 다음 반복 진입 전 안전 한도 검사와 누적 작업 예산이 없다.

## GAP-108 의복 불합격품 처리와 자동 해체 회수량 누락

- 분류: 불합격품 처리·회수 비용 누락
- 보완할 문서: production-quality-and-supply (production에서 참조)
- 현재 확인한 차이: 공통 품질 설명에는 실제 불합격 의복 보관·판매 대기·해체와 안전 한도 경계가 없다.
- **위키에 작성할 정보:**
  - 불합격품은 물리 의복으로 남고 정책에 따라 보관·판매 대기 또는 해체로 진행한다. 판매 대기는 즉시 현금화가 아니다.
  - 자동 해체의 원단 회수량은 floor(투입 원단×0.5×max(0,마지막 제작자 회수배율))이고 작업량은 max(0.1,제작WU×0.2)다. 해체에 들어가기 전에 안전 한도에 걸리면 불합격 의복이 남을 수 있다.
- **위키에 작성하지 않을 정보:**
  - 판매 대기 즉시 현금 획득
  - 안전 한도와 무관하게 모든 불합격품을 반드시 해체한다는 설명
- 감사 원본 근거: [ApparelWorkOrderRuntime.cs 1717행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1717>): 현재 관련 본문 확인 / [ApparelWorkOrderRuntime.cs 1742행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1742>): 제작자 회수배율과 회수수량계산 / [ApparelSpecialThroughputContributor.cs 226행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ApparelSpecialThroughputContributor.cs:226>): 호출되는 해체WU 권위;129/130상수0.2/0.1 / [ApparelWorkOrderRuntime.cs 1795행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs:1795>): 실제 해체 실행 결과에 따른 output/pending 상태
- 감사 위키 근거: [production-quality-and-supply.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:39>): 공통 목표 품질 규칙만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 물리 출력·회수 보존과 저장 복원을 실행 재현하지 않았다.
- 이번 재검토: 공통 품질 설명에는 실제 불합격 의복 보관·판매 대기·해체와 안전 한도 경계가 없다.

## GAP-110 룬 버스 결합기의 건설 재료 개별 도감 링크 누락

- 분류: 도감 필드·관계 누락
- 보완할 문서: entry/facility/building-* (전체 건설 재료와 아이템 링크)
- 현재 확인한 차이: 룬 버스 결합기 도감은8재료와 수량을 이미 모두 표시하므로 '외 N개' 생략 주장은 철회한다. 남는 차이는 이8재료에 대한 개별 아이템 도감 관계 링크가 없는 점이다.
- **위키에 작성할 정보:**
  - 룬 버스 결합기의 건설 재료인 석재 블록16·강철10·정밀 부품6·룬 도체4·마나 차폐판2·마나 결정4·공학 도면2·룬 버스 결합기1을 각각 해당 아이템 도감으로 연결한다.
- **위키에 작성하지 않을 정보:**
  - 시설91개의142재료가 현재 외N개로 생략되어 있다는 설명
- 감사 원본 근거: [RF23_룬_버스_결합기.asset 155행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF23_룬_버스_결합기.asset:155>): 8재료수량16/10/6/4/2/4/2/1
- 감사 위키 근거: [building-8823.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8823.json:19>): 8재료 모두 표시 / [building-8823.json 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8823.json:15>): relations 빈 배열
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 확정 링크 누락은 building-8823의 관계 배열에 한정한다. 전체 시설 UI의 링크 동작을 플레이 검증한 것은 아니다.
- 이번 재검토: 룬 버스 결합기 도감은8재료와 수량을 이미 모두 표시하므로 '외 N개' 생략 주장은 철회한다. 남는 차이는 이8재료에 대한 개별 아이템 도감 관계 링크가 없는 점이다.

## GAP-111 진실 관측소 도감의 영어 작성 문구·내부 타입명 노출

- 분류: 도감 설명·건설 비용 누락
- 보완할 문서: entry/facility/building-landmark-* (milestones-runs-and-meta에서 참조)
- 현재 확인한 차이: 진실 관측소는3660WU와4재료24/17/9/4를 이미 공개한다. 남은 확인 오류는 한국어 기능 설명 대신 내부 모듈 타입명과 'V20 hand-authored milestone landmark'라는 작성 메모를 표시한다는 점이다.
- **위키에 작성할 정보:**
  - 새로 게시할 규칙 없음. 아래 문서 정정 조치만 적용.
- **위키에 작성하지 않을 정보:**
  - 현재 진실 관측소의 건설 비용이 누락되었다는 설명
  - 확인하지 않은 관측·버프 기능을 이름만으로 만들어 넣는 설명
- 감사 원본 근거: [building_landmark_truth-observatory.asset 78행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_truth-observatory.asset:78>): 3660WU / [building_landmark_truth-observatory.asset 94행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_truth-observatory.asset:94>): 4재료 수량
- 감사 위키 근거: [building-landmark-truth-observatory.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-truth-observatory.json:19>): 비용 공개 및 영어/internal 타입 문구 잔존
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 나머지8랜드마크의 역할·비용을 전수 확인하지 않았다. 새 기능 설명은 실제 랜드마크 소비 경로 확인 뒤 추가해야 한다.
- 문서 정정 조치(공개 원고 아님): 진실 관측소의 공개 설명에서 내부 모듈 타입명·영어 작성 메모를 제거하고 한국어 명칭과 이미 확인된 건설 비용을 유지한다.
- 관련 GAP(제외 이력 포함): GAP-163
- 이번 재검토: 진실 관측소는3660WU와4재료24/17/9/4를 이미 공개한다. 남은 확인 오류는 한국어 기능 설명 대신 내부 모듈 타입명과 'V20 hand-authored milestone landmark'라는 작성 메모를 표시한다는 점이다.

## GAP-112 운반 멜빵의 이번 운반용 장착과 완료 시 소모·해제 누락

- 분류: 조건·수치·예외 누락
- 보완할 문서: inventory-and-carrying (entry/item/tool-hauling-harness와 entry/combat/workwear-hauling-harness에서 참조)
- 현재 확인한 차이: 가이드에는 용량 배율만 있고 이번 운반을 위해 새로 장착한 멜빵에만 적용하는 완료 소모·해제 조건이 없다. 기존 멜빵이면 새 장착 플래그가 설정되지 않는다.
- **위키에 작성할 정보:**
  - 이미 운반 멜빵을 착용했으면 이를 사용하고, 없으면 운반 멜빵 장착을 시도한다. 이번 운반에서 새로 장착한 경우에만 완료 정리가 수행된다.
  - 이 완료 정리에서 소모 적용이 요청되고 멜빵 실물이 있으면 현재 내구도에서1을 빼며, 이어 장비 해제를 시도한다. 미리 착용하던 멜빵에는 이 경로의 완료 정리가 적용되지 않는다.
- **위키에 작성하지 않을 정보:**
  - 다른 환경 작업복을 입으면 멜빵 장착 시도 자체를 하지 않는다는 설명
  - 0내구가120으로 회복된다는 재현되지 않은 주장
- 감사 원본 근거: [CharacterCarryInventory.cs 560행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/CharacterCarryInventory.cs:560>): 기존멜빵 또는TryEquip·이번운반flag / [CharacterCarryInventory.cs 584행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/CharacterCarryInventory.cs:584>): 이번flag+applyWear시내구-1·609해제
- 감사 위키 근거: [inventory-and-carrying.md 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/inventory-and-carrying.md:27>): 운반 멜빵의 용량 배율만 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체 교체·취소의 원자성과 UI를 실행 재현하지 않았다. 멜빵 준비·완료 메서드의 직접 규칙만 확인했다.
- 이번 재검토: 가이드에는 용량 배율만 있고 이번 운반을 위해 새로 장착한 멜빵에만 적용하는 완료 소모·해제 조건이 없다. 기존 멜빵이면 새 장착 플래그가 설정되지 않는다.

## GAP-113 작물12종 파종·수확WU와 공통 온도 성장 조건 누락

- 분류: 도감 수치·조건 누락 및 가이드 과장
- 보완할 문서: food-and-ecology / entry/nature/crop-*
- 현재 확인한 차이: 현재12작물의 파종·수확WU와 온도 범위는 도감에 없다. 파종·수확은 실제 작업 진척의 완료 경계이며 실내/야외 모두 온도 판정을 먼저 통과해야 한다.
- **위키에 작성할 정보:**
  - 파종/수확WU·기본온도°C: 동굴버섯3/5·3~26, 혈엽4/7·8~30, 그늘섬유4/7·5~30, 황혼곡3/6·4~30, 월화5/9·5~24, 몽엽5/8·6~26, 밤포도5/8·8~32, 잿불뿌리4/7·2~28, 포자 삼4/7·6~30, 서리 아마4/7·-8~20, 습지 갈대4/7·8~34, 잿불 목화4/7·14~42.
  - 성장은 실내·야외 모두 현재 재배지의 온도 관측이 필요하고 기본 범위에 품종의 내한·내열 확장을 적용한다. 실내와 야외의 성장 배율은 서로 다르며 광량과 물 부족도 별도로 성장을 제한한다.
- **위키에 작성하지 않을 정보:**
  - 적정 온도가 야외에만 적용되거나 WU가 곧 총 생장 시간이라는 설명
- 감사 원본 근거: [CropPlotRuntime.cs 1112행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:1112>): 파종WU실제소비;1139수확 / [CropPlotRuntime.cs 3374행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:3374>): 실내분기전온도/광량판정 / [CropHarvestSpecialThroughputContributor.cs 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropHarvestSpecialThroughputContributor.cs:91>): 실내·야외배율 / [CropHarvestSpecialThroughputContributor.cs 119행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropHarvestSpecialThroughputContributor.cs:119>): 품종내한내열반영 / [crop_cave_mushroom.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/crop_cave_mushroom.asset:34>): 파종3/수확5WU;39온도3..26 / [crop_bloodleaf.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/crop_bloodleaf.asset:34>): 파종4/수확7WU;39온도8..30 / [crop_shade_fiber.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/crop_shade_fiber.asset:34>): 파종4/수확7WU;39온도5..30 / [crop_twilight_grain.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/crop_twilight_grain.asset:34>): 파종3/수확6WU;39온도4..30 / [crop_moonflower.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/crop_moonflower.asset:34>): 파종5/수확9WU;39온도5..24 / [crop_dreamleaf.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/crop_dreamleaf.asset:34>): 파종5/수확8WU;39온도6..26 / [crop_night_grape.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/crop_night_grape.asset:34>): 파종5/수확8WU;39온도8..32 / [crop_ember_root.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/crop_ember_root.asset:34>): 파종4/수확7WU;39온도2..28 / [crop_spore-hemp.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/V22Textiles/crop_spore-hemp.asset:34>): 파종4/수확7WU;39온도6..30 / [crop_frost-flax.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/V22Textiles/crop_frost-flax.asset:34>): 파종4/수확7WU;39온도-8..20 / [crop_mire-reed.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/V22Textiles/crop_mire-reed.asset:34>): 파종4/수확7WU;39온도8..34 / [crop_ember-cotton.asset 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Crops/V22Textiles/crop_ember-cotton.asset:34>): 파종4/수확7WU;39온도14..42
- 감사 위키 근거: [crop-cave-mushroom.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-cave-mushroom.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-bloodleaf.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-bloodleaf.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-shade-fiber.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-shade-fiber.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-twilight-grain.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-twilight-grain.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-moonflower.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-moonflower.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-dreamleaf.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-dreamleaf.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-night-grape.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-night-grape.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-ember-root.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-ember-root.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-spore-hemp.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-spore-hemp.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-frost-flax.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-frost-flax.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-mire-reed.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-mire-reed.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음 / [crop-ember-cotton.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/crop-ember-cotton.json:2>): 전체 facts/relations/summary 직접 대조: 연구·일반생장 요약, WU/온도 범위 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 토양·유전체 전체 수확량 공식과 플레이 재현은 제외한다.
- 이번 재검토: 현재12작물의 파종·수확WU와 온도 범위는 도감에 없다. 파종·수확은 실제 작업 진척의 완료 경계이며 실내/야외 모두 온도 판정을 먼저 통과해야 한다.

## GAP-114 재배 물의 파종 초기량·일일 소비·보충·성장 제한 누락

- 분류: 물리 입력·반올림·저장 조건 누락
- 보완할 문서: food-and-ecology / entry/nature/crop-*
- 현재 확인한 차이: 기존 '파종 때 전체 주기 물을 ceil 산정하고 날씨를 고정한다'는 규칙은 현재와 다르다. 현재 파종 물은1이고 물은 현재 날씨에 따라 시간별로 소모된다. 위키의 성장에 물이라는 일반 설명에는 이 동작이 없다.
- **위키에 작성할 정보:**
  - 물 수요가 있는 작물은 파종할 때 깨끗한 물1을 투입하며 저장 용량은2다. 일일 수요는 작물 기본 일일물×시설 물배율이고 시간당 소비는 일일 수요/24다.
  - 비·폭풍 때 현재 시간당 소비를0.5배로 한다. 이 소비 함수는 실내 여부로 구분하지 않는다. 현재 물이 일일 수요보다 적으면 보충을 요청하며1단위씩 보충한다. 물에 따른 성장 배율은 clamp01(현재물/일일수요), 수요0이면1이다.
- **위키에 작성하지 않을 정보:**
  - 파종 때 성장 주기 전체 물을 한 번에 확정·소비한다는 설명
  - 파종 날씨가 이후 물 소비까지 고정된다는 설명
  - 비·폭풍 물 절약을 현재 코드상 야외 전용이라고 쓰는 설명
- 감사 원본 근거: [CropCycleInputRequirementAuthority.cs 151행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropCycleInputRequirementAuthority.cs:151>): 수요>0이면초기물1 / [CropPlotModels.cs 48행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Core/CropPlotModels.cs:48>): 초기1/용량2/보충1·일일/시간소비·성장배율 / [CropPlotRuntime.cs 2148행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:2148>): 현재날씨시간소비·수요미만보충 / [CropPlotRuntime.cs 2212행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:2212>): 물성장배율실제소비 / [CropPlotRuntime.cs 3319행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:3319>): 동결snapshot은파종요구량이며성장기물소비고정아님
- 감사 위키 근거: [food-and-ecology.md 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:25>): 주기 실물 물 계산은 설명하지 않음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 보충·파종의 모든 실패 및 저장 복원은 실행 재현하지 않았다. 이전ceil주기식은 삭제 대상으로 정정한다.
- 이번 재검토: 기존 '파종 때 전체 주기 물을 ceil 산정하고 날씨를 고정한다'는 규칙은 현재와 다르다. 현재 파종 물은1이고 물은 현재 날씨에 따라 시간별로 소모된다. 위키의 성장에 물이라는 일반 설명에는 이 동작이 없다.

## GAP-115 작물의 야외 고사·해충·질병 일일 판정 누락

- 분류: 실패 조건·확률 누락
- 보완할 문서: food-and-ecology
- 현재 확인한 차이: 기본 임계 수치는 여전히 존재하지만 현재 야외 고사 범위에는 품종의 내한·내열 확장을 먼저 적용한다. 기존 '작성온도±5'만으로 설명하면 품종 영향을 빠뜨린다.
- **위키에 작성할 정보:**
  - 야외에서 품종 내한·내열을 반영한 적정 범위보다5°C 넘게 벗어난 날이 연속3일이면 고사하며 정상 범위로 돌아오면 연속 일수가0이 된다. 실내는 이 일일 온도 고사 판정에서 제외한다.
  - 해충압85이상은 일일25% 고사 판정을 하고, 질병 발병 확률은 질병압/500×품종 위험도에 따른다.
- **위키에 작성하지 않을 정보:**
  - 실내가 모든 성장 온도 검사에서 면제된다는 설명
- 감사 원본 근거: [CropPlotRuntime.cs 3528행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:3528>): 품종확장후야외±5검사 / [CropEcologyDomain.cs 431행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs:431>): 연속3일 및해충85/25%·질병압/500
- 감사 위키 근거: [food-and-ecology.md 29행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:29>): 병원체·오염 일반 설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 일일 생태 권위와 호출의 정적 대조다. Unity 실행은 하지 않았다.
- 이번 재검토: 기본 임계 수치는 여전히 존재하지만 현재 야외 고사 범위에는 품종의 내한·내열 확장을 먼저 적용한다. 기존 '작성온도±5'만으로 설명하면 품종 영향을 빠뜨린다.

## GAP-116 재배시설4종의 성장·물수요 배율과 주기 소모품 누락

- 분류: 시설 운영 수치·비용 누락
- 보완할 문서: food-and-ecology / entry/facility/building-9702, building-9703, building-8856, building-8897
- 현재 확인한 차이: 4시설 작성값은 유지되지만 물배율은 이제 전체 주기 선결제보다 일일 물수요 계산에 소비된다. 건설비만 있는 도감에 운영 설정과 주기 소모품이 없다.
- **위키에 작성할 정보:**
  - 성장배율/물수요배율/주기퇴비/주기연료는 야외경작지1/1/0/0, 실내재배조1.25/0.85/1/1, 균사선반1.15/0.8/1/0, 온실1.5/0.75/1/1이다.
  - 건설 재료와 별도로 파종 주기 소모품은 야외 질산염비료1, 실내 버섯배지1+질산염비료1, 균사선반 접종통나무1, 온실 영양제1이다. 퇴비에는 소모품 절감 배율을 적용해 올림하고 최소1이며 선택 연료와 추가 소모품은 요구량에 합산한다.
- **위키에 작성하지 않을 정보:**
  - 시설 성장 배율만으로 최종 생장 시간을 확정하거나 물배율을 주기 전체 선결제 식으로 설명하는 문장
- 감사 원본 근거: [P23_야외경작지.asset 188행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P23_야외경작지.asset:188>): 야외1095: 1/1/0/0; nitrate-fertilizer1 / [P24_실내재배조.asset 190행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/P24_실내재배조.asset:190>): 실내1096: 1.25/0.85/1/1; mushroom-substrate1+nitrate-fertilizer1 / [RF13_균사_재배_선반.asset 112행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF13_균사_재배_선반.asset:112>): 균사8813: 1.15/0.8/1/0; inoculated-log1 / [RF54_재배_온실.asset 112행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF54_재배_온실.asset:112>): 온실8854: 1.5/0.75/1/1; greenhouse-nutrient1 / [CropCycleInputRequirementAuthority.cs 160행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropCycleInputRequirementAuthority.cs:160>): 퇴비·연료·추가소모품 요구량 / [CropPlotModels.cs 55행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Core/CropPlotModels.cs:55>): 시설물배율→일일수요 / [CropHarvestSpecialThroughputContributor.cs 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropHarvestSpecialThroughputContributor.cs:91>): 시설성장배율실내/야외소비 / [CropPlotRuntime.cs 2212행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:2212>): 물×환경성장 배율
- 감사 위키 근거: [building-1095.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1095.json:19>): 도감 전체는 분류/크기/건설비/역할만; 운영 배율·주기 비용 없음 / [building-1096.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1096.json:19>): 도감 전체는 분류/크기/건설비/역할만; 운영 배율·주기 비용 없음 / [building-8813.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8813.json:19>): 도감 전체는 분류/크기/건설비/역할만; 운영 배율·주기 비용 없음 / [building-8854.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8854.json:19>): 도감 전체는 분류/크기/건설비/역할만; 운영 배율·주기 비용 없음 / [food-and-ecology.md 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:25>): 실물 씨앗과 일반 환경조건/실내외 비교만
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 4시설 설정·파종 요구량·성장/물소비 직접 경로의 정적 확인이다. 플레이·물리 commit 전체 실패를 인증하지 않는다.
- 이번 재검토: 4시설 작성값은 유지되지만 물배율은 이제 전체 주기 선결제보다 일일 물수요 계산에 소비된다. 건설비만 있는 도감에 운영 설정과 주기 소모품이 없다.

## GAP-117 원정 이동 구간 시간의 경로·날씨 배율과 잔여 시간 처리 누락

- 분류: 공식·구현 연결 불일치
- 보완할 문서: expeditions / weather-seasons-and-environment
- 현재 확인한 차이: 현재 이동 시계는 단순2.5×의료×시설이 아니라 경로비용과 현재 날씨가 포함된 구간 시간이다. 초과시간도 무조건 이월이 아니라 다음 이동을 계속할 수 있을 때 남은 tick시간을 소비한다.
- **위키에 작성할 정보:**
  - 구간 시간은2.5초×타일 경로비용×max(1,의료 이동배율)×이정표 배율(0.1~1)×시설 배율이다. 경로비용에는 저장된 경로 날씨배율과 현재 기상전선의 원정 이동배율을 곱한 값이 들어간다.
  - 이동 구간 시작 때 비용·날씨·지속시간을 고정한다. 한 tick에 구간을 마치고 계속 이동 가능한 경우 남은 시간을 다음 구간에 쓰며 전투·결정 대기 등 계속할 수 없는 경계에서는 중지한다.
- **위키에 작성하지 않을 정보:**
  - 날씨가 항상1이라 실제 이동 시간에 영향을 주지 않는다는 설명
  - 초과 tick시간이 항상 버려지거나 항상 무조건 이월된다는 설명
- 감사 원본 근거: [OffenseTravelAndDecisionRuntime.cs 296행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs:296>): 기본구간2.5초 / [OffenseTravelAndDecisionRuntime.cs 697행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs:697>): 잔여시간 while·canCarryElapsed / [OffenseTravelAndDecisionRuntime.cs 867행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs:867>): 경로날씨×현재기상전선·비용·의료·시설 구간시간 / [OffenseTravelAndDecisionRuntime.cs 910행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs:910>): 구간스냅샷 고정
- 감사 위키 근거: [expeditions.md 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/expeditions.md:27>): 기후·지형·이동 일반 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 경로탐색 전체 프로필·모든 이벤트 경계와 저장 왕복은 실행 재현하지 않았다.
- 이번 재검토: 현재 이동 시계는 단순2.5×의료×시설이 아니라 경로비용과 현재 날씨가 포함된 구간 시간이다. 초과시간도 무조건 이월이 아니라 다음 이동을 계속할 수 있을 때 남은 tick시간을 소비한다.

## GAP-119 수술 임상단계별 환경 위험0.25 가중치 누락

- 분류: 수식·환경 상태 전이 누락
- 보완할 문서: medical-care-and-surgery
- 현재 확인한 차이: 정상·극한 환경값과 대기는 이미 공개되어 있다. 실제 남은 차이는 각 임상단계의 최초 도달 때 환경 위험0.25를 확률에 반영하는 식이다.
- **위키에 작성할 정보:**
  - 각 임상단계에 처음 도달할 때 환경 성공페널티의0.25배를 성공확률에서 빼고 결과를0.05~0.98로 제한한다. 감염·출혈·장기손상은 각각 환경 추가위험의0.25배를 더하고0~1로 제한한다.
  - 같은 임상단계를 다시 기록하지 않으므로 같은 단계 도달 기록으로 위험을 중복 가산하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 기본 성공 공식·의료진 모든 성능 상한·모든 합병증 결과까지 전수 검증했다는 설명
- 감사 원본 근거: [SurgeryRuntime.cs 1500행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:1500>): 최초단계기록→위험반영 / [SurgeryEnvironmentRuntime.cs 167행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryEnvironmentRuntime.cs:167>): 중복단계방지 / [SurgeryEnvironmentRuntime.cs 206행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryEnvironmentRuntime.cs:206>): 0.25가중치 / [SurgeryEnvironmentRiskEvaluator.cs 125행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryEnvironmentRiskEvaluator.cs:125>): 확률반영식과clamp / [SurgeryRuntime.cs 1788행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:1788>): 최종successChance소비
- 감사 위키 근거: [medical-care-and-surgery.md 123행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:123>): 정상/극한값이미공개 / [medical-care-and-surgery.md 125행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:125>): 단계위험누적과5초대기는설명하지만0.25및확률반영식은없음 / [medical-care-and-surgery.md 44행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:44>): 성공/합병증영향요소는일반설명;정확한단계가중치없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 단계 위험 갱신과 최종 성공확률 소비를 정적으로 확인했다. 플레이 재현은 하지 않았다.
- 이번 재검토: 정상·극한 환경값과 대기는 이미 공개되어 있다. 실제 남은 차이는 각 임상단계의 최초 도달 때 환경 위험0.25를 확률에 반영하는 식이다.

## GAP-120 수술15/20/50/15 단계WU와 성공 후 관찰시간 누락

- 분류: 진행·실패·회복 상태 누락
- 보완할 문서: medical-care-and-surgery
- 현재 확인한 차이: 진행 보존·중단 상처·일회 결과는 공개되어 있다. 단계별WU비중과 성공 후10초×후유증기간배율은 위키에 없다.
- **위키에 작성할 정보:**
  - 수술 총 작업량은 마취15%·절개20%·처치50%·봉합15%로 나뉘고 누적WU로 단계를 진행한다.
  - 성공 후 관찰시간은10초×max(0,환자의후유증기간배율)이며 관찰 종료시각에 도달해야 완료 정산으로 넘어간다.
- **위키에 작성하지 않을 정보:**
  - 성공 후 관찰시간이 항상 고정10초라는 설명
- 감사 원본 근거: [SurgeryRuntime.cs 1157행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:1157>): 4단계WU비율 / [SurgeryRuntime.cs 1506행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:1506>): 누적단계경계 / [SurgeryRuntime.cs 1855행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:1855>): 성공후Recovering·10초배율 / [SurgeryRuntime.cs 174행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:174>): 관찰시각후완료정산 / [SurgeryRuntime.cs 17행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:17>): RecoverySeconds10
- 감사 위키 근거: [medical-care-and-surgery.md 129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:129>): 단계/작업량저장·절개피해는기재하나비율없음 / [medical-care-and-surgery.md 131행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:131>): 성공효과1회는기재하나관찰10초×배율없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실패 강도별 모든 신체 피해·사망·감염 및 최종 저장 중복 처리를 전수 실행 검증하지 않았다.
- 이번 재검토: 진행 보존·중단 상처·일회 결과는 공개되어 있다. 단계별WU비중과 성공 후10초×후유증기간배율은 위키에 없다.

## GAP-121 수술 도감의 조사 자리표시자 오류

- 분류: 도감 문장 품질·효과 불일치
- 보완할 문서: entry/medical/procedure-*
- 현재 확인한 차이: procedure-beastkin-sprint-joint의 을(를) 자리표시자가 남는다. 수혈은 혈액손실 회복 명령을 실제 전달하므로 미연결이라는 이전 주장은 틀리며 가이드의 혈액손실-25와 모순하지 않는다.
- **위키에 작성할 정보:**
  - 질주 관절 이식 도감의 '을(를)' 자리표시자를 문장에 맞는 한국어 조사로 바꾼다.
- **위키에 작성하지 않을 정보:**
  - 수혈의 혈액손실 회복이 미연결이라는 설명
  - 현재 자리표시자 오류가 정확히24개라는 미집계 단정
- 감사 원본 근거: [procedure_blood-transfusion.asset 56행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Procedures/procedure_blood-transfusion.asset:56>): RecoverBloodLossEffect 작성 / [SurgicalProcedureEffectHandlers.cs 275행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalProcedureEffectHandlers.cs:275>): RecoverBloodLossEffect→ApplyTreatment bloodLossReduction=amount
- 감사 위키 근거: [procedure-beastkin-sprint-joint.json 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/medical/procedure-beastkin-sprint-joint.json:42>): 을(를) 자리표시자 잔존 / [medical-care-and-surgery.md 51행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:51>): 혈액손실-25 실제 효과 공개
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 해당 직접 사례의 문장 오류만 확정한다. 수술 도감 전체 오류 개수를 재산출하지 않았다.
- 이번 재검토: procedure-beastkin-sprint-joint의 을(를) 자리표시자가 남는다. 수혈은 혈액손실 회복 명령을 실제 전달하므로 미연결이라는 이전 주장은 틀리며 가이드의 혈액손실-25와 모순하지 않는다.

## GAP-122 구조 대상 선택의 치료 우선순위·미안정·거리 정렬 누락

- 분류: 업무 순서·작업량·상태 전이 누락 및 흐름 정정
- 보완할 문서: medical-care-and-surgery / work medicine rescue·treatment
- 현재 확인한 차이: 안정화→운반→치료 순서와WU는 공개되어 있다. 구조 AI가 실제 예약하는 후보의3단계 정렬만 남는 차이다.
- **위키에 작성할 정보:**
  - 구조자는 가능한 주문을 치료 우선순위가 높은 순서, 아직 안정화하지 않은 환자 우선, 구조자와의 맨해튼 거리가 가까운 순서로 정렬하여 첫 후보를 예약한다.
- **위키에 작성하지 않을 정보:**
  - 공개된 일반 치료 작업 순서·WU가 전부 누락되었다는 설명
- 감사 원본 근거: [AbilityRescue.cs 131행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/AbilityRescue.cs:131>): 실제구조행동이TryReserveBestOrder호출 / [CharacterMedicalRuntime.cs 413행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs:413>): 가용주문필터→carePriority내림→미안정우선→맨해튼거리→첫후보 / [CharacterMedicalRuntime.cs 427행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs:427>): 선택된주문에rescuerId및실제예약상태기록
- 감사 위키 근거: [medical-care-and-surgery.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:9>): 일반적우선순위필요만언급 / [medical-care-and-surgery.md 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:30>): 대상/작업순서/WU/효과는공개;후보정렬규칙없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 환자당 활성 주문1개 불변식과 운반 중단의 모든 하역·복원 경로는 이 항목에서 인증하지 않는다.
- 이번 재검토: 안정화→운반→치료 순서와WU는 공개되어 있다. 구조 AI가 실제 예약하는 후보의3단계 정렬만 남는 차이다.

## GAP-123 구조치료 추출혈액의 효율·기본 기분 요인 누락

- 분류: 도감 필드·시설 배율·물리 재료·부작용 누락
- 보완할 문서: medical-care-and-surgery / item and facility entries
- 현재 확인한 차이: 구조치료의 추출혈액 효율0.55와 기분요인-4/240초/1중첩은 해당 치료열·아이템 도감에 없다. 생활시설 대체재 열의0.55·일반 기분 저하 설명은 구조치료 수치 표기를 대신하지 않는다.
- **위키에 작성할 정보:**
  - 구조치료에 추출혈액을 쓰면 치료 회복 예산에0.55배를 적용한다. 치료 완료 시 추출혈액 기분요인 기본-4를240초, 최대1중첩으로 전달한다.
  - 이 수치는 기분 정책 적용 전 기본 요인이므로 캐릭터별 면역·보정 이후 최종 기분 감소가 항상4라는 뜻은 아니다.
- **위키에 작성하지 않을 정보:**
  - 약품·의료시설 전체 수치가 누락되었다는 설명
  - 추출혈액 사용 시 모든 캐릭터의 최종기분이 무조건4감소한다는 설명
- 감사 원본 근거: [CharacterMedicalRuntime.cs 845행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs:845>): 추출혈액효율0.55가실제치료회복예산에곱해지고863부작용호출 / [CharacterMedicalRuntime.cs 926행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterMedicalRuntime.cs:926>): 기분요인medical:extracted-blood,-4,240초,maxStacks1 / [CharacterActor.cs 930행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterActor.cs:930>): ApplyMoodFactor→실제MoodPolicy.ApplySeconds위임
- 감사 위키 근거: [medical-care-and-surgery.md 33행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:33>): 구조추출혈액실물1개는공개 / [medical-care-and-surgery.md 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:34>): 구조열엔0.55/-4/240없음;0.55와일반기분저하는생활시설열 / [captivity-extracted-blood.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/captivity-extracted-blood.json:2>): 전체도감facts무게/적재/가격;치료효율·기분값없음 / [factions-contracts-and-prisoners.md 142행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:142>): 의료사용만일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 다른 약품·시설 전수 및 감염·불안 별도 이벤트 소비는 범위 밖이다.
- 이번 재검토: 구조치료의 추출혈액 효율0.55와 기분요인-4/240초/1중첩은 해당 치료열·아이템 도감에 없다. 생활시설 대체재 열의0.55·일반 기분 저하 설명은 구조치료 수치 표기를 대신하지 않는다.

## GAP-124 룬 활의 재장전 입력1.05초와 민첩 보정 누락

- 분류: 도감 필드·제작 투입·전투 입력값 누락
- 보완할 문서: entry/combat/equipment-item-* / combat-and-equipment
- 현재 확인한 차이: 룬 활 도감은 사거리24·탄약·추가부품·제작WU를 이미 공개한다. 현재 실제 재장전에는 작성값1.05초와 민첩 배율이 사용되지만 도감에는 없다.
- **위키에 작성할 정보:**
  - 룬 활 기본 재장전 입력은1.05초다. 실제 재장전 시간은 max(0.15초,1.05초×clamp(1.2-민첩×0.035,0.55,1.2))이다.
- **위키에 작성하지 않을 정보:**
  - 전투장비61종의 모든 수치가 누락되었다는 설명
- 감사 원본 근거: [W31_RuneBow.asset 54행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Combat/Equipment/W31_RuneBow.asset:54>): 사거리별계수;64사거리24;65탄약7;73탄창1/74재장전1.05 / [CombatResolutionService.cs 633행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatResolutionService.cs:633>): reload=max.15,작성초×민첩clamp / [CharacterCombatCommandRuntime.cs 1036행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterCombatCommandRuntime.cs:1036>): 명령이reload계산소비
- 감사 위키 근거: [equipment-item-weapon-rune-bow.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/combat/equipment-item-weapon-rune-bow.json:2>): facts/relations 전체 직접 확인: WU/사거리/탄약/부품 이미 공개;재장전/사거리별계수 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 사거리별 계수와61종 전체 설정·전투 소비는 승인하지 않았다. 룬활 재장전 경로만 정적 확인했다.
- 이번 재검토: 룬 활 도감은 사거리24·탄약·추가부품·제작WU를 이미 공개한다. 현재 실제 재장전에는 작성값1.05초와 민첩 배율이 사용되지만 도감에는 없다.

## GAP-125 정밀 조준기의 장착 조건·등급 및 상태 배율 누락

- 분류: 도감 필드·획득 조건·배율 공식 누락
- 보완할 문서: entry/combat/module-* / combat-and-equipment / expeditions
- 현재 확인한 차이: 정밀 조준기 도감의 facts/relations가 비어 있다. 기존 증거110행 Benefits/Burdens는 장착 부품이 아니라 진화 노드 경로이므로 잘못 연결되었다. 실제 장착 모듈은 PowerPerGrade/UtilityPerGrade×등급×상태를 합산한다.
- **위키에 작성할 정보:**
  - 정밀 조준기의 등급당 기본 power는0.045, utility는0.035다. 장착 모듈 배율은 max(0.1,1+Σ[해당 계수×clamp(등급,1,4)×clamp01(상태)])다.
  - 장착하려면 장비와 계통이 같고 감정 완료·상태0.75이상·복원 또는 조율 완료여야 한다.4등급은 룬 조율도 필요하다.
- **위키에 작성하지 않을 정보:**
  - 장착 개량 부품이 진화 노드의 Benefits/Burdens를 그대로 소비한다는 설명
  - 20종 획득 조건·개별 효과가 모두 확인되었다는 설명
- 감사 원본 근거: [EM03.asset 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Combat/EquipmentModules/EM03.asset:19>): 계통0·power.045·utility.035 / [EquipmentModuleRuntime.cs 866행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/EquipmentModuleRuntime.cs:866>): 계통·감정·상태.75·복원·4등급룬조율 / [CombatEquipmentStatProjector.cs 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatEquipmentStatProjector.cs:130>): 실제장착모듈등급×상태합산 / [CombatEquipmentLoadoutRuntime.cs 373행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatEquipmentLoadoutRuntime.cs:373>): power/utility모듈배율 소비
- 감사 위키 근거: [module-weapon-precision-sight.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/combat/module-weapon-precision-sight.json:2>): facts/relations 빈 배열
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 정밀 조준기 작성값·공통 장착 gate·장착 모듈 배율을 정적 대조했다. power/utility별 모든 하위 전투효과와 획득 경로는 별도 범위이다.
- 이번 재검토: 정밀 조준기 도감의 facts/relations가 비어 있다. 기존 증거110행 Benefits/Burdens는 장착 부품이 아니라 진화 노드 경로이므로 잘못 연결되었다. 실제 장착 모듈은 PowerPerGrade/UtilityPerGrade×등급×상태를 합산한다.

## GAP-126 야생동물의 비활동 계절 이탈·번식철 등장 가중치 누락

- 분류: 도감 facts·계절·축산 조건 누락
- 보완할 문서: entry/nature/* / food-and-ecology
- 현재 확인한 차이: 야생동물 일반 안내에는 비활동 계절의 이탈 의도 전환과 번식철 재스폰 가중치1.65가 없다.
- **위키에 작성할 정보:**
  - 현재 계절이 해당 야생동물의 활동 계절이 아니면 맵 이탈 의도로 전환한다. 번식 계절이 일치하면 재스폰 후보 가중치에1.65를 곱한다.
- **위키에 작성하지 않을 정보:**
  - 번식철에는 모든 동물이 반드시1.65배 수량으로 생성된다는 설명
  - 18종 전투·축산·특수산물 전체가 검증되었다는 설명
- 감사 원본 근거: [WildlifeEcosystemRuntime.cs 238행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Wildlife/Core/WildlifeEcosystemRuntime.cs:238>): 비활동계절LeaveMap의도 / [WildlifeEcosystemRuntime.cs 555행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Wildlife/Core/WildlifeEcosystemRuntime.cs:555>): 실제후보추첨ScoreRespawnWeight / [WildlifeEcosystemRuntime.cs 849행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Wildlife/Core/WildlifeEcosystemRuntime.cs:849>): 번식철1.65가중치
- 감사 위키 근거: [food-and-ecology.md 179행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:179>): 야생동물 요약과202축산5초점검은 공개;활동계절이탈/1.65 미서술
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 가중치와 의도 전환의 정적 확인이다. 실제 탈출 완료·장기 개체수 분포는 실행 재현하지 않았다.
- 이번 재검토: 야생동물 일반 안내에는 비활동 계절의 이탈 의도 전환과 번식철 재스폰 가중치1.65가 없다.

## GAP-128 직원·용병 임금·체불·예측 비용식 누락

- 분류: 고용 비용·실패 상태·UI 설명 누락
- 보완할 문서: economy-and-trade / guests-services-and-performance
- 현재 확인한 차이: 임금 수요 일반 설명에는 직원과 용병의 다른 단가·체불 처리·예측식이 없다.
- **위키에 작성할 정보:**
  - 직원 일급은30+레벨×2+수당, 용병은60+레벨×4+장비수당이며 창립자·하수인 일급은0이다. 직원은 ID순으로 당일 임금+체불 전액을 지급하거나 체불로 남긴다.
  - 용병 임금 지급 실패는 계약 종료로 이어진다. n일 예측 비용은 활성 계약 일급합×n+직원 체불액1회이다.
- **위키에 작성하지 않을 정보:**
  - 직원 체불액을 예측 일수만큼 반복해서 곱한다는 설명
- 감사 원본 근거: [EmploymentContractRuntime.cs 58행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/EmploymentContractRuntime.cs:58>): 직원/용병 단가 / [EmploymentContractRuntime.cs 197행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/EmploymentContractRuntime.cs:197>): 체불 합산 / [EmploymentContractRuntime.cs 110행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/EmploymentContractRuntime.cs:110>): 예측식 및 130현행계약별 일급 / [EmploymentContractRuntime.cs 232행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/EmploymentContractRuntime.cs:232>): 용병 지급 실패시 EndMercenaryContract
- 감사 위키 근거: [economy-and-trade.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md:13>): 중앙 결제만 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 시설별 수당·기분·HUD·계약 종료 뒤 실제 퇴장 전체는 이 정적 확인 범위 밖이다.
- 이번 재검토: 임금 수요 일반 설명에는 직원과 용병의 다른 단가·체불 처리·예측식이 없다.

## GAP-129 자동구매의3일 보호금·가용예산·일일 중복방지 누락

- 분류: 운영 기능·물류/예산 조건 누락
- 보완할 문서: economy-and-trade / inventory-and-carrying
- 현재 확인한 차이: 현재 보호금·가용예산식은 위키에 없다. 초기25 미확인이라는 기존 한계도 현재 상태 기본값과 복원 기본값이500인 것으로 해소한다.
- **위키에 작성할 정보:**
  - 보호금은 max(설정최저잔액,직원·용병3일예측비용+유료시설계약3일예측비용)이다. 자동구매 가용예산은 min(설정일일예산,max(0,현재잔액-보호금))이다.
  - 현재 기본 일일 예산은500이고 저장값이 없을 때 복원 기본값도500이다. 같은 날 또는 더 오래된 상점 갱신일은 처리하지 않으며 예산이 없어도 그 갱신일을 처리한 날로 기록한다.
- **위키에 작성하지 않을 정보:**
  - 기본 일일예산이25라는 설명
  - 예산 부족이면 같은 날 상점 갱신을 무제한 재시도한다는 설명
- 감사 원본 근거: [AutoProcurementRuntime.cs 668행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/AutoProcurementRuntime.cs:668>): 임금3일+시설계약3일 보호금 / [AutoProcurementRuntime.cs 736행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/AutoProcurementRuntime.cs:736>): 잔액-보호금과 일일예산 최소 / [AutoProcurementRuntime.cs 779행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/AutoProcurementRuntime.cs:779>): 복원 기본예산500 / [AutoProcurementRuntime.cs 721행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/AutoProcurementRuntime.cs:721>): 날짜중복방지·예산검사전lastday기록 / [EconomyTransactionLedgerRuntime.cs 55행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/EconomyTransactionLedgerRuntime.cs:55>): 상태기본예산500
- 감사 위키 근거: [economy-and-trade.md 38행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md:38>): 구매의 선차감/환불만 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든 개별 offer실패·UI·구매 저장 원자성은 실행 검증하지 않았다.
- 이번 재검토: 현재 보호금·가용예산식은 위키에 없다. 초기25 미확인이라는 기존 한계도 현재 상태 기본값과 복원 기본값이500인 것으로 해소한다.

## GAP-130 지역 공급 계약의 해금·주기·규모·보상식 누락

- 분류: 계약 유형·공식·진행 조건 누락
- 보완할 문서: factions-contracts-and-prisoners / economy-and-trade
- 현재 확인한 차이: 지역 공급 생성기는 주민수·완료 연구수 기반이며 일반 세력 계약의 최근10일EWU 부담률과 다르다. 그 별도 생성 규칙이 위키에 없다.
- **위키에 작성할 정보:**
  - 상권통합 해금 후3일 주기로 최대3개의 지역 공급 제안을 만든다. 세 번째 제안에는 보조 품목을 붙인다. P=clamp(floor(주민수/3),0,10), R=clamp(floor(완료연구수/12),0,5), i=제안인덱스0~2로 계산한다.
  - 요구량은 원료 clamp(20+3P+5R+4i,20,80), 중간재 clamp(10+2P+3R+2i,10,40), 기타 clamp(2+floor(P/2)+R+i,2,12)이다. 보상은 내부 단가합(최소1)×1.2×clamp(사업배율,1,1.25)을 정수 반올림하고 최소1로 한다.
- **위키에 작성하지 않을 정보:**
  - 지역 공급 요구량을 일반 세력 계약의EWU 부담률식으로 설명하는 문장
- 감사 원본 근거: [RegionalSupplyContractRuntime.cs 126행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs:126>): 3일 주기 / [RegionalSupplyContractRuntime.cs 428행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs:428>): 최대3제안 / [RegionalSupplyContractRuntime.cs 190행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs:190>): 상권통합 gate / [RegionalSupplyContractRuntime.cs 457행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs:457>): 규모권위/보상권위 실제호출 / [ResourceEconomyPlanningModels.cs 496행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ResourceEconomyPlanningModels.cs:496>): P/R/유형별 규모식 / [StockCategoryCatalog.cs 86행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Buildings/Core/StockCategoryCatalog.cs:86>): 현재 보상식;73/74배율1.2/1.25
- 감사 위키 근거: [factions-contracts-and-prisoners.md 29행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:29>): 일반 계약 EWU 부담률
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 물리 집결·납품 commit·입금·목적지 정리·전체 복원은 이 생성 규칙 승인 범위 밖이다.
- 이번 재검토: 지역 공급 생성기는 주민수·완료 연구수 기반이며 일반 세력 계약의 최근10일EWU 부담률과 다르다. 그 별도 생성 규칙이 위키에 없다.

## GAP-131 혈엽 기본종·동토불뿌리 품종명·유전형 효과 누락

- 분류: 도감 facts·품종 비교 수치 누락
- 보완할 문서: entry/nature/genome-* / food-and-ecology
- 현재 확인한 차이: 두 품종 도감은 파일명식 제목·일반 요약만 있고 실제 품종명·작물·유전형이 없다. 전체32종이 아니라 이 두 품종과 실제 성장·온도 소비만 확정한다.
- **위키에 작성할 정보:**
  - 혈엽 기본종은 혈엽 작물이며6유전자쌍이 모두0/0이다. 따라서 내한·내열 추가0°C, 성장·수량·질병위험 배율1, 추가 종자 보정0이다.
  - 동토불뿌리는 잿불뿌리 품종이며 내한2/2·수량1/1, 나머지는0/0이다. 내한 범위를5°C 확장하고 수량 배율은1.05이며 성장·질병위험 배율1, 추가 종자0이다.
- **위키에 작성하지 않을 정보:**
  - 32품종 전체의 수치·교환효과를 전수 승인했다는 설명
- 감사 원본 근거: [Genome_bloodleaf.asset 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/CropGenomes/Genome_bloodleaf.asset:25>): crop:bloodleaf·bloodleaf기본종·6loci전부0 / [genome_ember-root_cold.asset 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Ecology/Cultivars/genome_ember-root_cold.asset:25>): 동토불뿌리;내한2/2·수량1/1,나머지0;tradeoff metadata / [CropEcologyDomain.cs 79행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs:79>): locus→phenotype 변환 / [CropHarvestSpecialThroughputContributor.cs 91행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropHarvestSpecialThroughputContributor.cs:91>): 유전형성장배율·119내한내열소비
- 감사 위키 근거: [genome-bloodleaf-base.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/genome-bloodleaf-base.json:2>): facts/relations비어있음;품종payload없음 / [genome-ember-root-cold.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/nature/genome-ember-root-cold.json:2>): facts/relations비어있음;품종payload없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 직접 두 에셋과 공통 유전형 환산·성장/온도 소비의 정적 확인이다. 모든 수량·질병·종자 효과의 실행 재현은 하지 않았다.
- 이번 재검토: 두 품종 도감은 파일명식 제목·일반 요약만 있고 실제 품종명·작물·유전형이 없다. 전체32종이 아니라 이 두 품종과 실제 성장·온도 소비만 확정한다.

## GAP-132 작물의 수확 변이·반환 종자와 미참조 동결품종 상한 누락

- 분류: 유전 상태·물리 종자·저장/트랜잭션 규칙 누락
- 보완할 문서: food-and-ecology / inventory-and-carrying
- 현재 확인한 차이: 일반 수명주기 안내에는 수확 변이·종자 반환·병원체와 활성/미참조 동결 품종 상한을 구분한 설명이 없다.
- **위키에 작성할 정보:**
  - 일반 수확에서 부모 유전형의 각 유전자 좌위는1% 변이 가능성을 평가하고 세대가1증가한다. 반환 종자 기본 수는2~4이며 종자 병원체 압력에는 부모 압력의0.5배를 적용한다.
  - 작물당 활성 품종 상한은12, 외부 참조가 없는 동결 품종의 보관 상한은32다.32는 모든 동결 품종의 총수 상한이 아니다.
- **위키에 작성하지 않을 정보:**
  - 외부 참조 여부와 관계없이 동결 품종이 총32개뿐이라는 설명
- 감사 원본 근거: [CropEcologyDomain.cs 349행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs:349>): 활성품종12 / [CropEcologyDomain.cs 791행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs:791>): locus1% 돌연변이 / [CropEcologyDomain.cs 472행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs:472>): 실제 수확 변이·반환씨앗·병원체 / [CropEcologyDomain.cs 350행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs:350>): 상한 이름은 MaximumUnreferencedFrozenCultivarsPerCrop;770 해당목록 pruning / [CropEcologyRuntime.cs 389행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropEcologyRuntime.cs:389>): 실행 Harvest/prepare/commit 서비스가 도메인 호출
- 감사 위키 근거: [food-and-ecology.md 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:27>): 재배 일반 수명주기 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 최초 재고·유전형의 물리 commit·외부 참조 보호 및 저장 왕복 전체는 승인하지 않는다. 참조 Preflight는 이 행의 근거로 사용하지 않는다.
- 이번 재검토: 일반 수명주기 안내에는 수확 변이·종자 반환·병원체와 활성/미참조 동결 품종 상한을 구분한 설명이 없다.

## GAP-135 수인 위기 계약의7일 기한·관계 효과값 누락

- 분류: 도감 facts·실행 조건 누락
- 보완할 문서: entry/event/faction-contract-* / factions-contracts-and-prisoners
- 현재 확인한 차이: 개별 도감의 약3개·효과개수는 이미 공개지만 수락일+7일과 성공 호의8·의무1, 실패 원한12는 없다.
- **위키에 작성할 정보:**
  - 수인 위기 계약 기한은 수락일+7일이다. 성공하면 해당 세력 호의+8·의무+1을 적용하고 실패하면 원한+12를 적용한다.
- **위키에 작성하지 않을 정보:**
  - 세력계약18종과 행정 인장의 전체 소비를 검증했다는 설명
- 감사 원본 근거: [contract_beastkin_crisis.asset 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Factions/Contracts/contract_beastkin_crisis.asset:25>): 7일;28표준약3 consume;38호의8/의무1;47원한12 / [V20CampaignRuntime.cs 3335행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:3335>): 물자계약수락기한=현재일+작성기한 / [V20CampaignRuntime.cs 4432행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:4432>): 배송완료성공효과 / [V20CampaignRuntime.cs 5175행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5175>): 기한실패효과 / [V20CampaignRuntime.cs 5466행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5466>): 호의/원한/의무관계효과 실제소비
- 감사 위키 근거: [faction-contract-beastkin-crisis.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/event/faction-contract-beastkin-crisis.json:2>): 도감 전체: 물품수량/효과개수만;기한/효과값 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 한 계약의 작성값·기한설정·성공/실패 관계효과만 정적으로 확인했다.
- 이번 재검토: 개별 도감의 약3개·효과개수는 이미 공개지만 수락일+7일과 성공 호의8·의무1, 실패 원한12는 없다.

## GAP-136 균사 세력 교역·보급 화물과22/38일 재사용시간 누락

- 분류: 세력별 화물·가격 권위·재사용시간 누락 및 표현 불일치
- 보완할 문서: factions-contracts-and-prisoners / economy-and-trade / world map
- 현재 확인한 차이: 공통 유료교역·동맹 예산·증표 규칙은 공개되어 있지만 균사 세력의 개별 화물6행과 요청별 재사용시간은 없다.
- **위키에 작성할 정보:**
  - 균사 세력 교역 화물은 버섯10·퇴비8·습포5이며 교역 재사용시간은22일이다. 보급 화물은 해독제8·표준약8·수프10이며 보급 재사용시간은38일이다.
- **위키에 작성하지 않을 정보:**
  - 6세력36화물행 전체가 미공개이며 검증 완료라는 설명
- 감사 원본 근거: [Faction_Myconid_MycelialGrove.asset 241행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Factions/Dungeons/Faction_Myconid_MycelialGrove.asset:241>): 교역3종/248보급3종;261교역22일/보급38일/지원10일 / [FactionRuntime.cs 1191행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:1191>): 요청종류별 정의cooldown 선택 / [FactionRuntime.cs 1234행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:1234>): TradeCargo/SupplyCargo 실제 선택
- 감사 위키 근거: [factions-contracts-and-prisoners.md 45행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:45>): 유료/동맹혜택예산은 이미 공개;53증표1·10일·실패미소비도 공개
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 직접 확인한 균사 세력만 승인한다. 다른 세력 개별표는 전수 확인하지 않았다.
- 이번 재검토: 공통 유료교역·동맹 예산·증표 규칙은 공개되어 있지만 균사 세력의 개별 화물6행과 요청별 재사용시간은 없다.

## GAP-137 곡물죽의 영양35·기본 기분0·신선시간360초 누락

- 분류: 도감 facts·소비 분류 수치 누락
- 보완할 문서: entry/item/food-* / food-and-ecology
- 현재 확인한 차이: 곡물죽 도감은 무게·적재·가격과 비건 단순식만 설명한다. 음식 입력 영양35·기분0·신선360초가 없고 실제 식사 완료는 영양을 허기 회복에 사용한다.
- **위키에 작성할 정보:**
  - 곡물죽은 영양35, 기본 기분 효과0, 작성 신선시간360초다. 정상 식사 효과 적용에서 영양35만큼 허기 회복을 요청한다.
- **위키에 작성하지 않을 정보:**
  - 식단 위반·오염까지 포함해 곡물죽은 항상 기분 변화가 없다는 설명
  - 식사22종/음식feature47건 전수가 모두 확인되었다는 설명
- 감사 원본 근거: [food_grain_porridge.asset 46행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/food_grain_porridge.asset:46>): productionkindFood;56FoodFeature 영양35/기분0/신선360/품질0 / [CharacterConsumableModels.cs 143행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Core/CharacterConsumableModels.cs:143>): meal definition이 음식 영양/기분/품질/식단 매핑 / [CharacterConsumablesRuntime.cs 1149행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:1149>): 소비완료효과호출 / [CharacterConsumablesRuntime.cs 2483행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:2483>): 영양허기회복·식단/오염별기분예외
- 감사 위키 근거: [food-grain-porridge.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/food-grain-porridge.json:2>): 전체도감:무게/겹침/가격·비건단순식;영양/기분/신선도수치없음 / [food-and-ecology.md 118행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:118>): 공통 식사설명과 개별수치 누락 구별
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 신선도 전체 상태 전이와 오염·식단 예외의 최종 결과는 이 단일 음식 입력 검토와 구분한다.
- 이번 재검토: 곡물죽 도감은 무게·적재·가격과 비건 단순식만 설명한다. 음식 입력 영양35·기분0·신선360초가 없고 실제 식사 완료는 영양을 허기 회복에 사용한다.

## GAP-138 일반 식사 선택의 정책·품질상한과4초 계획·예약 만료 누락

- 분류: 실행 조건·물류·효과·저장 권위 누락
- 보완할 문서: food-and-ecology / residents-and-work / inventory-and-carrying
- 현재 확인한 차이: 현재 일반 자동 식사는 문화금지·식단·품질 상한과 오염 조건을 사용하지만 긴급 허기 때는 일부 금지·오염 조건을 우회한다. 기존 일괄 gate 설명은 이 예외를 빠뜨렸다. 시설 식사계획의4초 및 예약 만료도 가이드에 없다.
- **위키에 작성할 정보:**
  - 일반 자동 식사는 문화적으로 금지된 음식·식단 불허 음식·오염된 음식을 제외하고 개인 품질 상한 이하에서 선택한다. 품질상한 상속값은 Fine이다. 긴급 허기 선택은 문화·식단·오염 조건을 우회할 수 있으나 품질 상한은 유지한다.
  - 시설 식사계획은 생성 뒤4초가 지나기 전 확정하지 않고 예약 만료 시 취소한다. 예약 재연결에 성공하면 만료 시각을 현재+15초 이상으로 연장한다.
- **위키에 작성하지 않을 정보:**
  - 식단·문화금지가 긴급 식사에서도 절대 우회되지 않는다는 설명
  - 주머니 간식 등 모든 소비 경로가 동일한 시설 식사계획을 사용한다는 설명
- 감사 원본 근거: [CharacterConsumablesRuntime.cs 148행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:148>): 품질상속Fine·문화/식단정책 / [CharacterConsumablesRuntime.cs 267행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:267>): 자동후보·긴급우회·품질유지 / [CharacterConsumablesRuntime.cs 933행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:933>): 예약재연결/만료/생성4초후commit / [CharacterConsumablesRuntime.cs 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:18>): MealActionSeconds4
- 감사 위키 근거: [food-and-ecology.md 118행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:118>): 해당 일반 안내와 공개된 일부 경계 확인
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 일반 자동 후보와 시설 식사계획의 좁은 정적 확인이다. 모든 문턱·배송·효과·ledger·저장 왕복은 인증하지 않는다.
- 이번 재검토: 현재 일반 자동 식사는 문화금지·식단·품질 상한과 오염 조건을 사용하지만 긴급 허기 때는 일부 금지·오염 조건을 우회한다. 기존 일괄 gate 설명은 이 예외를 빠뜨렸다. 시설 식사계획의4초 및 예약 만료도 가이드에 없다.

## GAP-139 품목 재고 정책 기본값·초과 처리5종·판매 정산 대기 누락

- 분류: 재고 자동화·물리 판매·저장 상태 설명 누락
- 보완할 문서: inventory-and-carrying / economy-and-trade
- 현재 확인한 차이: 공개 자동판매 흐름과 별개로 품목별 최소·목표·최대와 초과 처리 분기가 없다.
- **위키에 작성할 정보:**
  - 새 품목 정책은 기본 비활성이고 최소10·목표20·최대40, 초과품 보류로 시작한다. 활성 정책의 초과량은 max(0,보유량-최대재고)다.
  - 초과 처리는 판매·가공·퇴비화·분해·보류 중 정책을 따른다. 미완료 판매 정산이 있으면 새 초과 처리를 시작하지 않고 정산 복구를 기다린다.
- **위키에 작성하지 않을 정보:**
  - 정책 생성만으로 자동판매가 즉시 활성화된다는 설명
- 감사 원본 근거: [ResourceStockPolicyRuntime.cs 158행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs:158>): 현행 관련 정의/직접 경로 확인 / [ResourceStockPolicyRuntime.cs 315행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs:315>): pending sale 복구 gate/활성/초과/5처리 실제분기
- 감사 위키 근거: [economy-and-trade.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md:40>): 해당 일반 안내와 공개된 일부 경계 확인
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 비상비축·출고 제외 집계·최소묶음·전체 물리 정산/복원은 범위 밖이다.
- 이번 재검토: 공개 자동판매 흐름과 별개로 품목별 최소·목표·최대와 초과 처리 분기가 없다.

## GAP-140 소매 구매 확정 성공 후 재고·구매수·수입 집계 경계 누락

- 분류: 소매 재고·서비스·범죄·저장 규칙 설명 누락
- 보완할 문서: guests-services-and-performance / economy-and-trade / facility retail entries
- 현재 확인한 차이: 공개 자동판매 일반 설명과 별개로 소매 구매의 확정 성공 뒤 구매수·재고 사건·수입을 반영하는 경계를 확인했다. 내부직원 무수입 분기의 존재는 직원 구매 진입 허용을 증명하지 않으므로 기존 문장의 '같은 구매 경로' 주장은 철회한다.
- **위키에 작성할 정보:**
  - 소매 손님 구매가 확정 성공한 뒤에만 구매 수와 재고 소비 사건을 반영하고 수입 대상 방문자의 가격을 영업수입에 더한다. 구매확정이 실패하거나 결과가 미해결이면 그 구매를 성공 집계하지 않는다.
- **위키에 작성하지 않을 정보:**
  - CreatesRevenueFor=false 분기만을 근거로 내부 직원이 소매시설에서 손님처럼 구매할 수 있다고 설명하는 문장
- 감사 원본 근거: [Shop.cs 188행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Shop.cs:188>): 내부직원CreatesRevenueFor=false / [ShopCustomerInteractionService.cs 163행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/ShopCustomerInteractionService.cs:163>): 방문자의 createsRevenue 계산 / [ShopCustomerInteractionService.cs 392행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/ShopCustomerInteractionService.cs:392>): commit성공만 구매/재고사건;406수입조건 / [CharacterVisitPolicy.cs 68행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Ability/CharacterVisitPolicy.cs:68>): 직원소매구매방문거부 / [AbilityShopping.cs 805행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Ability/AbilityShopping.cs:805>): 쇼핑후보가방문gate먼저검사
- 감사 위키 근거: [economy-and-trade.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md:40>): 물리 자동판매 안내이며 방문자별 소매수입 분리 미설명 / [guests-services-and-performance.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:21>): 공통서비스 흐름만;내부직원 무수입 미서술
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 개별 시설·절도·대기 이탈·취소·복원 전체는 이 좁은 소매 집계 검토에서 제외한다. / 내부 직원의 구매 진입 허용 여부는 이 집계 분기로 인증하지 않는다. 별도 직원 진입 gate는GAP-240담당의 직접 근거를 따른다. / 현재 직원은 구매 상점의 자율 방문 후보로 선택되지 않는다. 이는 직접 소매 API의 모든 호출이 불가능하다는 증명은 아니다.
- 이번 재검토: 공개 자동판매 일반 설명과 별개로 소매 구매의 확정 성공 뒤 구매수·재고 사건·수입을 반영하는 경계를 확인했다. 내부직원 무수입 분기의 존재는 직원 구매 진입 허용을 증명하지 않으므로 기존 문장의 '같은 구매 경로' 주장은 철회한다.

## GAP-141 음료·기호물질9종의 분류·연구·기본 효과 입력 누락

- 분류: 도감 facts·관계·효과 수치 누락
- 보완할 문서: entry/item/drug-* / entry/item/food-night-* / food-and-ecology
- 현재 확인한 차이: 9종의 현재 카탈로그 연결과 작성 물질 필드를 대조했다. 변경된3도감의 서술이 추가되었어도 숫자 필드·연구 관계는 여전히 없다. 현재 작성 useClass는 비중독성·중독성·유흥성이고 이9종 안에 Medicine은 없다.
- **위키에 작성할 정보:**
  - 각 항목은 '중독기본값/과다복용기본확률/내성증가/시간당금단; 기본기분/작업효과/전투효과; 지속초'로 표기한다. 혈화 촉진제(중독성,각성제):0.14/0.06/0.18/0.03;1/0.16/0.2;150. 몽엽 진통제(중독성,진통·마취):0.08/0.02/0.12/0.02;4/0/-0.03;240. 환각균 증류액(유흥성,증류):0.09/0.04/0.13/0.025;10/-0.12/-0.08;210.
  - 마나 각성제(중독성,각성제):0.11/0.05/0.16/0.025;2/0/0.08;180. 월화차(비중독성,약초학):0/0.002/0/0;3/0.04/0;180. 밤포도주(유흥성,발효):0.04/0.025/0.08/0.015;7/-0.04/-0.04;240.
  - 활력 강장제(비중독성,증류):0/0.006/0/0;2/0/0;150. 밤 증류주(유흥성,주류 증류·숙성):0.07/0.045/0.12/0.025;9/-0.08/-0.06;240. 황혼 맥주(유흥성,발효):0.025/0.015/0.05/0.008;5/-0.02/-0.02;180. 중독기본값은 실제1회 중독확률과 같지 않으며 실제 내성·중독식은 공통 물질 규칙을 따른다.
- **위키에 작성하지 않을 정보:**
  - Medicine 분류의 작성품이 이9종에 존재한다는 설명
  - 작성 중독기본값을 그대로1회 중독확률이라고 표시하는 설명
- 감사 원본 근거: [SubstanceDefinitionView.cs 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/SubstanceDefinitionView.cs:42>): 현행 관련 정의/직접 경로 확인 / [drug_blood_stimulant.asset 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/drug_blood_stimulant.asset:57>): Substance 작성payload 전부 직접대조;ResearchGate도확인 / [drug_dreamleaf_analgesic.asset 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/drug_dreamleaf_analgesic.asset:57>): Substance 작성payload 전부 직접대조;ResearchGate도확인 / [drug_hallucinogenic_distillate.asset 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/drug_hallucinogenic_distillate.asset:57>): Substance 작성payload 전부 직접대조;ResearchGate도확인 / [drug_mana_awakener.asset 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/drug_mana_awakener.asset:57>): Substance 작성payload 전부 직접대조;ResearchGate도확인 / [drug_moonflower_tea.asset 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/drug_moonflower_tea.asset:57>): Substance 작성payload 전부 직접대조;ResearchGate도확인 / [drug_night_wine.asset 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/drug_night_wine.asset:57>): Substance 작성payload 전부 직접대조;ResearchGate도확인 / [drug_vitality_tonic.asset 56행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/drug_vitality_tonic.asset:56>): Substance 작성payload 전부 직접대조;ResearchGate도확인 / [food_night_spirit.asset 47행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/Workshop/food_night_spirit.asset:47>): Substance 작성payload 전부 직접대조;ResearchGate도확인 / [food_twilight_beer.asset 47행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/Workshop/food_twilight_beer.asset:47>): Substance 작성payload 전부 직접대조;ResearchGate도확인 / [SubstanceDefinitionView.cs 5행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/SubstanceDefinitionView.cs:5>): Medicine0/NonAddictive1/Addictive2/Recreational3 / [ItemDefinitionCatalog.asset 147행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/ItemDefinitionCatalog.asset:147>): 7drug147~153·밤증류주771·황혼맥주778GUID현재확인 / [CharacterConsumablesRuntime.cs 1882행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:1882>): 내성/중독/과다복용 소비 / [CharacterConsumablesRuntime.cs 2535행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:2535>): 기본기분×내성 / [CharacterConsumablesRuntime.cs 3073행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:3073>): 활성물질 작업/전투효과 합산
- 감사 위키 근거: [food-and-ecology.md 118행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:118>): 해당 일반 안내와 공개된 일부 경계 확인 / [drug-blood-stimulant.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/drug-blood-stimulant.json:2>): facts3개만/relations빈배열/일반summary;물질필드미노출 / [drug-dreamleaf-analgesic.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/drug-dreamleaf-analgesic.json:2>): facts3개만/relations빈배열/일반summary;물질필드미노출 / [drug-hallucinogenic-distillate.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/drug-hallucinogenic-distillate.json:2>): facts3개만/relations빈배열/일반summary;물질필드미노출 / [drug-mana-awakener.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/drug-mana-awakener.json:2>): facts3개만/relations빈배열/일반summary;물질필드미노출 / [drug-moonflower-tea.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/drug-moonflower-tea.json:2>): facts3개만/relations빈배열/일반summary;물질필드미노출 / [drug-night-wine.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/drug-night-wine.json:2>): facts3개만/relations빈배열/일반summary;물질필드미노출 / [drug-vitality-tonic.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/drug-vitality-tonic.json:2>): facts3개만/relations빈배열/일반summary;물질필드미노출 / [food-night-spirit.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/food-night-spirit.json:2>): facts3개만/relations빈배열/일반summary;물질필드미노출 / [food-twilight-beer.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/food-twilight-beer.json:2>): facts3개만/relations빈배열/일반summary;물질필드미노출
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 9종 기본 입력·연구·카탈로그 연결·공통효과 소비의 정적 확인이다. 플레이와 모든 특수효과는 인증하지 않는다. 몽엽 질병대응은GAP-020과 중복 승인하지 않는다.
- 이번 재검토: 9종의 현재 카탈로그 연결과 작성 물질 필드를 대조했다. 변경된3도감의 서술이 추가되었어도 숫자 필드·연구 관계는 여전히 없다. 현재 작성 useClass는 비중독성·중독성·유흥성이고 이9종 안에 Medicine은 없다.

## GAP-142 물질 내성·중독·과다복용·금단의 실제 처리식 누락

- 분류: 정책·상태 전이·물리 소비·효과·저장 권위 누락
- 보완할 문서: residents-and-work / food-and-ecology / inventory-and-carrying
- 현재 확인한 차이: 기본 물질 입력과 별개로 실제 소비에서 적용하는 내성·중독·기분·금단 계산이 공개되지 않았다. 작성중독기본값을1회확률로 해석하면 틀리다.
- **위키에 작성할 정보:**
  - t=복용 전 내성/100일 때 과다복용확률은 clamp01(기본확률×(1+t))다. 내성은 작성증가량만큼 더해0~100, 중독 누적치는 기본중독값×100×(0.65+t)를 더해0~100으로 제한한다. 기존중독자 또는 누적60이상이면 중독 상태이며 별도로 기본중독값×0.2 확률도 판정한다.
  - 기분 효과는 기본기분×(1-0.55t)다. 과다복용은 max(4,최대HP×0.12) 피해와 기본기분-12/300초를 적용한다. 중독자는 마지막 복용1게임시간 뒤부터 작성시간당금단을 누적하고 금단20이상에서는 -Lerp(3,14,금단/100)의2초 기분 요인을 갱신한다.
- **위키에 작성하지 않을 정보:**
  - 작성 중독기본값이 실제1회중독확률과 동일하다는 설명
- 감사 원본 근거: [CharacterConsumablesRuntime.cs 1882행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:1882>): 복용전내성/중독/과다복용식 / [CharacterConsumablesRuntime.cs 2229행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:2229>): 1게임시간후금단·20문턱기분 / [CharacterConsumablesRuntime.cs 2535행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs:2535>): 기분감쇠·과다복용피해/기분
- 감사 위키 근거: [food-and-ecology.md 118행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:118>): 해당 일반 안내와 공개된 일부 경계 확인
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 정책5종·AI선택·물리배송·재시도·저장왕복 전체는 범위 밖이다.
- 이번 재검토: 기본 물질 입력과 별개로 실제 소비에서 적용하는 내성·중독·기분·금단 계산이 공개되지 않았다. 작성중독기본값을1회확률로 해석하면 틀리다.

## GAP-143 장비 오버클럭3단계 비용·기간·과부하·고장 누락

- 분류: 장비 강화 운영·위험·가격 설명 누락
- 보완할 문서: combat-and-equipment / equipment instance UI
- 현재 확인한 차이: 가이드의 일반 배율·가치 설명에는 단계별 비용·초기 과부하·고장 확률과 활성 거부 조건이 없다.
- **위키에 작성할 정보:**
  - 오버클럭1/2/3단계 성능 배율은1.1/1.2/1.35이고 비용은 장비가치×0.15/0.35/0.7을 올림한다. 지속시간180게임초, 시작 과부하는10/25/50이다.
  - 동작 고장확률은0/3/8%이고 고장 시 과부하+10, 방어구·방패 내구 손상0.08을 적용한다. 이미 활성 중이거나 과부하100이면 다시 활성화할 수 없다.
- **위키에 작성하지 않을 정보:**
  - 모든 시설의 오버클럭 소비자가 금고방어 하나뿐이라는 전역 부재 주장
- 감사 원본 근거: [EquipmentOverclockRuntime.cs 45행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/EquipmentOverclockRuntime.cs:45>): 180초 / [EquipmentOverclockRuntime.cs 185행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/EquipmentOverclockRuntime.cs:185>): 성능배율 / [EquipmentOverclockRuntime.cs 200행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/EquipmentOverclockRuntime.cs:200>): 고장확률·과부하·장비종류별손상 / [EquipmentOverclockRuntime.cs 318행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/EquipmentOverclockRuntime.cs:318>): 활성/100 gate·비용·시작과부하·상태작성 / [CombatResolutionService.cs 78행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatResolutionService.cs:78>): 공격고장판정 / [CombatResolutionService.cs 749행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatResolutionService.cs:749>): 피해배율소비
- 감사 위키 근거: [combat-and-equipment.md 127행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:127>): 오버클럭 배율만 표시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 개별 장비·시설 모든 소비와 저장왕복은 인증하지 않는다. 전투 명령의 고장·배율 소비는 현재 재독했다.
- 이번 재검토: 가이드의 일반 배율·가치 설명에는 단계별 비용·초기 과부하·고장 확률과 활성 거부 조건이 없다.

## GAP-144 정밀 재단조3옵션 요금과 설정 실패 취소·환불 누락

- 분류: 장비 강화 선택·작업·가격 계약 누락
- 보완할 문서: combat-and-equipment / equipment reforge UI
- 현재 확인한 차이: 가이드에는 가치가 정밀 비용에 쓰인다는 설명만 있고 선택 요율과 주문 설정 실패의 환불 순서가 없다.
- **위키에 작성할 정보:**
  - 정밀 보정20%·부담 억제30%·외부 기술지원15% 중 선택한 요율을 장비가치에 합산 적용하고 전체 금액을 올림한다.
  - 주문 생성 후 비용을 지출하며, 지출 실패 시 주문을 취소한다. 비용을 낸 뒤 정밀 옵션 설정에 실패하면 주문 취소와 환불을 요청한다.
- **위키에 작성하지 않을 정보:**
  - 세 옵션이 항상 모두 적용되거나 각각 별도로 반올림된다는 설명
- 감사 원본 근거: [ReforgePrecisionService.cs 68행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/ReforgePrecisionService.cs:68>): 3옵션 요금 / [ReforgePrecisionService.cs 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/ReforgePrecisionService.cs:103>): 설정 실패 주문취소/환불
- 감사 위키 근거: [combat-and-equipment.md 150행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:150>): 재단조 가치 일반 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 옵션 수 제한·성능 분산·작업량 감소의 실제 효과와 실패복원 전체는 인증하지 않는다.
- 이번 재검토: 가이드에는 가치가 정밀 비용에 쓰인다는 설명만 있고 선택 요율과 주문 설정 실패의 환불 순서가 없다.

## GAP-145 금고각인 쇠뇌대의30골드 발사·예산·고장 전 결제 누락

- 분류: 유료 방어 시설 정책·결제·저장 설명 누락
- 보완할 문서: invasions-and-defence / economy-and-trade / entry/facility/building-9961
- 현재 확인한 차이: 도감에는 건설비만 있고 발사비30·기본 침공예산300·위협기준0 및 보호잔액 gate가 없다. 고장 판정 전 금화가 차감되므로 효과0이어도 비용을 이미 지불할 수 있다.
- **위키에 작성할 정보:**
  - 금고각인 쇠뇌대 작성 발사비는30골드, 기본 침공예산300, 최소위협0이다. 자동/보스/위협 정책과 침공 누적예산·보호잔액을 통과해야 발사를 승인한다.
  - 승인 과정에서 금화를 먼저 차감하고 침공 지출을 기록한 뒤 오버클럭 고장을 판정한다. 따라서 고장으로 효과가0이어도 발사비30은 이미 지불한 상태다.
- **위키에 작성하지 않을 정보:**
  - 오버클럭 고장일 때 발사비가 차감되지 않는다는 설명
- 감사 원본 근거: [TreasuryDefenseRuntime.cs 48행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/TreasuryDefenseRuntime.cs:48>): 침공32보존 / [TreasuryDefenseRuntime.cs 330행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/TreasuryDefenseRuntime.cs:330>): 예산 및보호금검사 / [P1_TreasuryBoltThrower.asset 188행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/P1/P1_TreasuryBoltThrower.asset:188>): shotCost30/defaultThreat0/defaultBudget300 / [TreasuryDefenseBuildingPanelPresenter.cs 20행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/TreasuryDefenseBuildingPanelPresenter.cs:20>): 위협/예산/보호금 UI선택지 / [TreasuryDefenseRuntime.cs 175행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Treasury/TreasuryDefenseRuntime.cs:175>): 결제→spend누적→고장0또는배율반환
- 감사 위키 근거: [building-9961.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9961.json:19>): 건설비/방어역할만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 자동사격·정책저장·침공32개 기록 보존 왕복은 실행 재현하지 않았다.
- 이번 재검토: 도감에는 건설비만 있고 발사비30·기본 침공예산300·위협기준0 및 보호잔액 gate가 없다. 고장 판정 전 금화가 차감되므로 효과0이어도 비용을 이미 지불할 수 있다.

## GAP-146 전략 원정 현장자금 선배정·현장 증감 규칙 누락

- 분류: 원정 자금 소유권·결제·귀환 설명 누락
- 보완할 문서: expeditions / economy-and-trade / world map
- 현재 확인한 차이: 전략 출발 시 중앙잔액에서 현장자금을 미리 빼고 현장 선택에서는 원정별 자금을 사용한다는 차이가 가이드에 없다. 구형 비세계지도 경로는 중앙잔액을 사용한다.
- **위키에 작성할 정보:**
  - 전략 원정에 배정하는 현장자금은 출발 때 중앙잔액에서 먼저 차감한다. 세계지도 원정의 현장 선택 비용은 현장자금에서 내며 부족하면 지출하지 못하고 양수 금화 효과는 현장자금에 더한다.
  - 구형 원정 선택은 같은 금화 효과에도 중앙잔액을 사용하므로 두 경로의 자금 권위를 구분한다.
- **위키에 작성하지 않을 정보:**
  - 현장 지출 때마다 중앙잔액에서 다시 같은 금액을 차감한다는 설명
- 감사 원본 근거: [OffenseExpeditionLaunchService.cs 229행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/OffenseExpeditionLaunchService.cs:229>): 전략FieldFunds>0 중앙선차감 / [OffenseDecisionEffects.cs 210행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/Strategic/OffenseDecisionEffects.cs:210>): world travel 현장자금 지출 / [OffenseExpeditionModel.cs 1077행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/OffenseExpeditionModel.cs:1077>): 보유미만지출거부·차감·양수가산
- 감사 위키 근거: [expeditions.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/expeditions.md:35>): 현장결정만 일반 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 종결·귀환·전멸·취소의 일회반환과 저장 전체는 승인하지 않는다.2026-09-14 현재Launch/Model 좁은경로를 재독했다.
- 이번 재검토: 전략 출발 시 중앙잔액에서 현장자금을 미리 빼고 현장 선택에서는 원정별 자금을 사용한다는 차이가 가이드에 없다. 구형 비세계지도 경로는 중앙잔액을 사용한다.

## GAP-147 적대 소문 완화와 원정지 정보의 결제 선택지 누락

- 분류: 외부 영향 자원 교환·정보 해금 설명 누락
- 보완할 문서: guests-services-and-performance / expeditions / factions-contracts-and-prisoners
- 현재 확인한 차이: 공개 원정 설명에는 작성비용과 정보 해금의 네 결제 경로·만료 거점 거부 조건이 없다.
- **위키에 작성할 정보:**
  - 소문 완화의 현재 작성 최대 감소량은15이고 명성 비용10·골드 비용200이다. 원정지 정보는 명성10·골드200·정찰60 또는 부적 경로로 해금할 수 있다.
  - 만료한 거점은 정보 해금에서 다시 거부하며 결제 성공한 경로에서만 정보를 해금한다.
- **위키에 작성하지 않을 정보:**
  - 모든 결제 수단을 한 번의 정보 해금에 동시에 요구한다는 설명
- 감사 원본 근거: [ExternalInfluenceRuntime.cs 211행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs:211>): 소문 rules 전달 / [ExternalInfluenceRuntime.cs 326행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs:326>): 정보4결제 분기 / [CoreSessionRules.asset 47행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/CoreSessionRules.asset:47>): 현행 정보/소문 비용 / [ExternalInfluenceRuntime.cs 324행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs:324>): 네 결제실행/성공시에만 unlock;354만료gate
- 감사 위키 근거: [expeditions.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/expeditions.md:35>): 정보구매 비용 설명 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 소문 부분 감소의 비례올림·공포 방어·침입자별 감쇠와 UI 횟수는 인증하지 않는다.
- 이번 재검토: 공개 원정 설명에는 작성비용과 정보 해금의 네 결제 경로·만료 거점 거부 조건이 없다.

## GAP-149 사회 사건의 가중 선정과 비영수증 해결기록256상한 누락

- 분류: 사건 스케줄러·결정론·상태 수명주기 설명 누락
- 보완할 문서: events-and-choices
- 현재 확인한 차이: 가이드의 일일·슬롯·재발 설명에는 결정적 가중 선정식이 없다. 현재 해결기록은 전체256이 아니라 성공 인생사건 영수증이 소유하지 않는 발생건만256으로 제한한다.
- **위키에 작성할 정보:**
  - 일일 후보 사건과 참가자는 시드·날짜·사건·참가자에 따른 고정 판정값을 관련 가중치로 나눈 순서에서 고른다. 사건은 참가자 중 가장 큰 가중치, 참가자 선정은 해당 참가자 가중치를 사용한다. 여러 관련 가중치는 곱하고0.1~10으로 제한한다.
  - 해결 기록 중 성공 인생사건의 영수증에 연결되지 않은 일반 발생건은 최근256개를 남긴다. 영수증이 보유한 발생건은 이 삭제 대상에서 제외되므로 전체 기록 수는256을 넘을 수 있다.
- **위키에 작성하지 않을 정보:**
  - 해결 사건 전체가 어떤 참조에도 관계없이256개로 잘린다는 설명
- 감사 원본 근거: [V20CampaignApplicationAdapter.cs 1783행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignApplicationAdapter.cs:1783>): 현재주민ContentWeights작성 / [V20CampaignRuntime.cs 5193행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5193>): 일일자동후보실제정렬 / [V20CampaignRuntime.cs 5310행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:5310>): 정의최대가중치/참가자가중치·.1~10 / [V20CampaignRuntime.cs 6391행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:6391>): EventRoll시드/ID/날짜/정렬참가자 / [V20CampaignRuntime.cs 6459행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:6459>): 영수증소유제외일반발생256상한
- 감사 위키 근거: [events-and-choices.md 60행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/events-and-choices.md:60>): 일일/슬롯/재발/자동개수는기재;가중stablehash입력및최근256없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 구체 문화·특성별 가중치 전수와 저장·재발 원자성은 인증하지 않는다. 문자열 해시 키는 공개 게임 설명 대신 감사근거에 둔다.
- 이번 재검토: 가이드의 일일·슬롯·재발 설명에는 결정적 가중 선정식이 없다. 현재 해결기록은 전체256이 아니라 성공 인생사건 영수증이 소유하지 않는 발생건만256으로 제한한다.

## GAP-150 현재 카탈로그 새싹제의 개별 도감·운영 필드 누락

- 분류: 축제 도감 페이지·facts·관계 누락
- 보완할 문서: species-culture-and-life / events-and-choices / entry/character/festival-*
- 현재 확인한 차이: 현재 도감의 festival 엔티티는12개이고 새싹제 엔티티는 없다. 중복 에셋 중 실제 GameDomainContentCatalog는 V20/Society의 새싹제를 참조함을 확인했다. 따라서 확정 범위는 새싹제1종과 그 운영값으로 좁히며 다른3누락·16종 전필드는 아직 승인하지 않는다.
- **위키에 작성할 정보:**
  - 새싹제는 봄15일, 최소참가자8명, 황혼곡 종자12개를 요구하는 축제다. 행사장 요구는 좌석8·식탁8·서비스 수용1·행사 공간8칸이다.
  - 결과별 기본 효과은 성공 기분+6/10일·세력호의+3, 부분성공 기분+2/5일·호의0, 실패 기분-3/4일·호의-2다. 실제 개인 기분 효과는 참석비율 보정을 거치므로 기본값을 무조건 모든 주민의 최종값으로 쓰지 않는다.
- **위키에 작성하지 않을 정보:**
  - 현재4개 누락 모두와16축제 모든 결과를 전수 확인했다는 설명
  - 같은ID의 Population 폴더 에셋을 카탈로그 참조 확인 없이 권위로 사용하는 설명
- 감사 원본 근거: [festival_sprout.asset 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Society/Festivals/festival_sprout.asset:15>): 현재새싹제정의·일정·회장·재료·3결과 / [GameDomainContentCatalog.asset 1234행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/GameDomainContentCatalog.asset:1234>): V20새싹제GUID참조·Population동명GUID미참조 / [FestivalDefinitionCatalog.cs 10행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FestivalDefinitionCatalog.cs:10>): content source16고유정의 카탈로그 / [FestivalExecutionRuntime.cs 1305행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FestivalExecutionRuntime.cs:1305>): 유효참석자등급→작성결과·참석비율기분소비
- 감사 위키 근거: [festival-weapon-vigil.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/festival-weapon-vigil.json:2>): facts빈배열, 준비물관계만 제공
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 새싹제 카탈로그 GUID·작성값·현재 실행 결과 소비를 정적으로 대조했다. 다른15축제 및 전체 행사 진행·저장 왕복은 인증하지 않는다.
- 이번 재검토: 현재 도감의 festival 엔티티는12개이고 새싹제 엔티티는 없다. 중복 에셋 중 실제 GameDomainContentCatalog는 V20/Society의 새싹제를 참조함을 확인했다. 따라서 확정 범위는 새싹제1종과 그 운영값으로 좁히며 다른3누락·16종 전필드는 아직 승인하지 않는다.

## GAP-151 축제의 실제 진행·물자 선소비·참석 기반 결과 등급 누락

- 분류: 축제 실행 조건·물리 비용·효과 설명 누락
- 보완할 문서: species-culture-and-life / events-and-choices / inventory-and-carrying
- 현재 확인한 차이: 기존 즉시 Schedule→Resolve와 성공/부분/실패별 비용100/50/0 설명은 현재 실행과 다르다. 현재는 개최 전 물자를 요구량대로 소비하고 Running으로 들어가 실제 참석 시간을 채운 뒤 유효 참석자로 결과를 정한다.
- **위키에 작성할 정보:**
  - 축제는 공지일부터 개최 마감시각 전까지 예약할 수 있으며, 시작 때 축제에 정해진 요구 물자를 납품 목적지에서 소비 확정한 뒤 진행 상태로 들어간다. 예약했다고 즉시 성공 효과를 받지 않는다.
  - 계획 참석 시간이 끝난 뒤 유효 참가자가 최소참가자수 이상이면 성공, 최소참가자수의 절반을 올림한 수(최소1명) 이상이면 부분성공, 그 미만이면 실패다. 기분·슬픔전환은 유효 개인의 참석비율 보정을 거쳐 적용한다.
- **위키에 작성하지 않을 정보:**
  - 현재 축제가 즉시 Schedule→Resolve로 끝난다는 설명
  - 현재 결과 등급별 비용이100%/50%/0%라는 설명
  - 첫시설을 단순 열거순으로 선택한다고 단정하는 설명
- 감사 원본 근거: [FuneralFestivalRuntime.cs 236행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FuneralFestivalRuntime.cs:236>): 실제축제명령으로위임 / [FestivalExecutionRuntime.cs 543행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FestivalExecutionRuntime.cs:543>): 예약가능공지·마감경계 / [FestivalExecutionRuntime.cs 584행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FestivalExecutionRuntime.cs:584>): 작성물자전체목적지요구 / [FestivalExecutionRuntime.cs 1935행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FestivalExecutionRuntime.cs:1935>): 물자요구량합산 / [FestivalExecutionRuntime.cs 1260행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FestivalExecutionRuntime.cs:1260>): 실물소비확정후Running / [FestivalExecutionRuntime.cs 717행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FestivalExecutionRuntime.cs:717>): 실제시간미완료Resolve거부 / [FestivalExecutionRuntime.cs 1305행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FestivalExecutionRuntime.cs:1305>): 유효참석자등급·참석비율효과
- 감사 위키 근거: [species-culture-and-life.md 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md:23>): 사회활동의 축제만 일반언급
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 회장 선택 전체·모든 참가자 행동·중단 및 저장 원자성은 실행 검증하지 않았다. 구형 façade 근거 대신 현재FestivalExecutionRuntime을 사용했다.
- 관련 GAP(제외 이력 포함): GAP-150
- 이번 재검토: 기존 즉시 Schedule→Resolve와 성공/부분/실패별 비용100/50/0 설명은 현재 실행과 다르다. 현재는 개최 전 물자를 요구량대로 소비하고 Running으로 들어가 실제 참석 시간을 채운 뒤 유효 참석자로 결과를 정한다.

## GAP-152 방 인상·청결의 업무 속도·시간·시설 선호 보정 누락

- 분류: 방 품질 수치·작업 속도·기분 효과 설명 누락
- 보완할 문서: spaces / residents-and-work
- 현재 확인한 차이: 배치 안내에는 인상60%+청결40%를 운영 속도와 시설 선호로 사용하는 식이 없다.
- **위키에 작성할 정보:**
  - 운영·연구·재고보충·경비·제작은 유효 방 환경점수S=clamp(인상×0.6+청결×0.4,0,100)를 사용한다. 속도는 clamp(0.85+S×0.003,0.85,1.15), 소요시간 배율은 그 역수다. 해당 업무가 아니거나 활성 방 환경이 없으면 시간배율1이다.
  - 시설 선호점수는 유효 환경에서8+20×S/100, 관측 스냅샷이 없으면8, 비활성 환경의 방필수 시설이면-15다. 완료 경험의 인상·청결 기분 요인은 설정된180초를 사용한다.
- **위키에 작성하지 않을 정보:**
  - 방 환경이 모든 업무에 같은 배율로 적용된다는 설명
- 감사 원본 근거: [RoomEnvironmentAdapter.cs 145행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:145>): 5업무·점수·속도식 / [RoomEnvironmentAdapter.cs 327행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:327>): 시간역수·선호식 / [AbilityWork.cs 327행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Ability/AbilityWork.cs:327>): 배정시설환경시간조회 / [RoomEnvironmentExperienceAdapter.cs 113행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentExperienceAdapter.cs:113>): 청결/기분 소비 / [RoomEnvironmentSettings.asset 36행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/Config/RoomEnvironmentSettings.asset:36>): 기분180초와작성delta
- 감사 위키 근거: [spaces.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:21>): 공간별운영기준만 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 방4점수 원시입력·바닥오염·기분 모든 문턱은 범위 밖이다.
- 이번 재검토: 배치 안내에는 인상60%+청결40%를 운영 속도와 시설 선호로 사용하는 식이 없다.

## GAP-153 방 역할 거부와 공유 식사·상점 실효정원 누락

- 분류: 방 역할·시설 유효성·수용력 설명 누락
- 보완할 문서: spaces / facility entries
- 현재 확인한 차이: 공간 설명에는 방필수 시설의 역할 거부와 실효정원식이 없다. 원장 경로S03_잠금진열선반.asset는 실제S03_잠금진열장.asset로 정정해야 하며 이를 공개 작성 문장으로 나눠 넣은 것이 의미 오류였다.
- **위키에 작성할 정보:**
  - 방이 필요한 시설은 방 없음·사용 불가·요구 역할 불일치 때 거부된다. 방이 필요 없거나 방 없음·독립형 방·사용불가 또는 기본정원0인 실효정원 계산은 기본정원을 그대로 반환한다.
  - 공유방의 식사 시설 정원은 max(기본정원,min(좌석정원,식탁정원)), 구매 시설은 max(기본정원,기본정원+서비스정원)이다. 실제 입장 잔여정원은 실효정원-현재사용자로 계산한다.
- **위키에 작성하지 않을 정보:**
  - 시설187/13종·역할13종·모든 부품을 전수 승인했다는 설명
- 감사 원본 근거: [RoomFacilityPolicyDomain.cs 75행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Rooms/Core/RoomFacilityPolicyDomain.cs:75>): 방없음/사용불가/역할불일치 거부;115실효정원식 / [RoomFacilityPolicyAdapter.cs 155행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomFacilityPolicyAdapter.cs:155>): 실제시설 입력→권위 계산 / [BuildableObject.cs 123행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildableObject.cs:123>): EffectiveCapacity 포워딩 / [BuildingOccupancyAssignment.cs 52행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingOccupancyAssignment.cs:52>): 실제잔여정원 소비 / [FacilityCandidateScorer.cs 245행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/FacilityCandidateScorer.cs:245>): AI후보 정원소비 / [S03_잠금진열장.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/S03_잠금진열장.asset:30>): 실재 작성id1014;원장 파일명 선반→진열장
- 감사 위키 근거: [spaces.md 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:49>): 방경계/시설소속 재계산만;정원식/예외/역할거부 미서술
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 원장 참조 경로 오기를S03_잠금진열장.asset로 수정한다. 해당 경로 교정은 게임 규칙 작성 문안이 아니라 감사 문서 수정이다. 전체 개별시설 설정은 범위 밖이다.
- 이번 재검토: 공간 설명에는 방필수 시설의 역할 거부와 실효정원식이 없다. 원장 경로S03_잠금진열선반.asset는 실제S03_잠금진열장.asset로 정정해야 하며 이를 공개 작성 문장으로 나눠 넣은 것이 의미 오류였다.

## GAP-154 문 권한 우선순위·집단 정책·제한 자물쇠 표시 누락

- 분류: 문 출입 정책·UI·저장 설명 누락
- 보완할 문서: spaces / prisoners / guests / wildlife
- 현재 확인한 차이: 일반 출입 설명에는 강제통과 예외와 기본권한의 개인차단 우선순위·집단·제한 표시가 없다. 자물쇠는 현재 정책의 제한 여부를 표시하며 통과 불가능을 모든 행위자에게 보장하는 표시는 아니다.
- **위키에 작성할 정보:**
  - 출입 집단은 소유자·직원·손님·포로·침입자·야생동물·포획야생동물이다. 기본권한에서는 개인차단을 먼저 적용하고 개인허용 또는 허용집단이면 통과한다.
  - 강제통과/임시권한 예외는 개인차단보다 먼저 적용될 수 있다. 문 정책이 제한 상태이면 문에 자물쇠 표시가 켜지며 정책 변경 때 갱신된다.
  - 정책은 모두허용·직원전용·손님구역·감방·축사 프리셋을 갖고 문 정책 복사·붙여넣기·같은방 문 적용 기능이 있다.
- **위키에 작성하지 않을 정보:**
  - 개인차단이 강제통과를 포함한 모든 예외보다 무조건 우선한다는 설명
  - 자물쇠가 표시되면 누구도 해당 문을 통과할 수 없다는 설명
- 감사 원본 근거: [DoorAccessModels.cs 7행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Access/DoorAccessModels.cs:7>): 그룹/프리셋 / [DoorAccessService.cs 72행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Buildings/Core/DoorAccessService.cs:72>): override가 개인거부보다 우선 / [DoorAccessStateModule.cs 63행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Access/DoorAccessStateModule.cs:63>): v1정책 JSON저장 / [DoorAccessUnityAdapter.cs 107행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/DoorAccessUnityAdapter.cs:107>): 복사/붙이기/방일괄 / [DoorAccessLockIndicator.cs 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Buildings/Core/DoorAccessLockIndicator.cs:11>): restricted일때시각루트활성 / [Door.cs 74행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Door.cs:74>): 정책변경시표시갱신 / [Door.cs 97행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Door.cs:97>): IsRestricted→표시Refresh
- 감사 위키 근거: [spaces.md 51행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:51>): 출입정책 일반 설명만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: UI입력·모든 이동 직전 caller·저장왕복은 이 정책/표시 정적 확인으로 인증하지 않는다. GAP-263자물쇠표시는 이 행에 병합한다.
- 이번 재검토: 일반 출입 설명에는 강제통과 예외와 기본권한의 개인차단 우선순위·집단·제한 표시가 없다. 자물쇠는 현재 정책의 제한 여부를 표시하며 통과 불가능을 모든 행위자에게 보장하는 표시는 아니다.

## GAP-155 문화10종 방 선호값과 시설 후보 점수 보정 누락

- 분류: 문화별 방 선호 수치·시설 선택 공식 누락
- 보완할 문서: species-culture-and-life / spaces / culture entries
- 현재 확인한 차이: 10문화 도감의 일반 서술에는 역할·온도·환기·광량·청결·공간 선호 수치가 없다. 현재 개척자 연맹 변경 에셋도 해당 선호값은 유지한다. 개인공간 조건은 문이 정확히1개가 아니라1개 이상이다.
- **위키에 작성할 정보:**
  - 표 순서는 선호역할;온도°C±허용;최소환기;광량범위;최소청결;공간이다. 밤궁정:휴식·집무;18±7;40;0~30;55;개인. 합류수:휴식·위생;22±5;40;20~70;70;공유. 무기철야:식사·훈련;20±10;40;25~85;30;공유. 포자정원:식사·휴식;20±5;45;5~35;40;공유. 도구씨족:훈련·창고;24±7;50;45~100;40;공유.
  - 높은둥지:휴식;18±10;70;50~100;35;공유. 핵공명:마나·창고;18±15;50;30~90;60;개인. 재의계약:마나·집무;27±8;35;20~70;40;개인. 무리의화로:식사·휴식;24±8;35;20~80;30;공유. 개척자연맹:식사·휴식;20±10;45;55~100;40;공유.
  - 시설 후보 문화 보정은 선호역할+0.08·선호시설+0.12이며 유효 방이 없으면-0.08이다. 온도·환기·청결·광량 충족점수 평균E에(E-0.5)×0.16을 더한다. 공유 선호는 면적12이상·빈칸3이상, 개인 선호는 면적8이하·문1개이상일 때+0.05, 미충족-0.05이며 최종 보정은-0.2~0.2다.
- **위키에 작성하지 않을 정보:**
  - 개인 공간은 문이 정확히1개일 때만 충족된다는 설명
- 감사 원본 근거: [CharacterCultureGameplayRuntime.cs 56행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/CharacterCultureGameplayRuntime.cs:56>): 선택문화→역할/시설bias;71환경여부;77~97환경/공유/개인 공식 / [FacilityScoringContext.cs 84행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/FacilityScoringContext.cs:84>): 문화런타임 호출 / [FacilityCandidateScorer.cs 1166행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/FacilityCandidateScorer.cs:1166>): cultureBias 계산 후1208실제후보점수에가산 / [culture_vampire-nightcourt.asset 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_vampire-nightcourt.asset:31>): 역할bitset/온도±허용/환기/조명/청결/공간: 516/18±7/환40/조명0..30/청결55/개인 / [culture_slime-confluence.asset 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_slime-confluence.asset:31>): 역할bitset/온도±허용/환기/조명/청결/공간: 260/22±5/40/20..70/70/공유 / [culture_orc-vigil.asset 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_orc-vigil.asset:31>): 역할bitset/온도±허용/환기/조명/청결/공간: 9/20±10/40/25..85/30/공유 / [culture_myconid-grove.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_myconid-grove.asset:30>): 역할bitset/온도±허용/환기/조명/청결/공간: 5/20±5/45/5..35/40/공유 / [culture_kobold-toolclan.asset 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_kobold-toolclan.asset:31>): 역할bitset/온도±허용/환기/조명/청결/공간: 72/24±7/50/45..100/40/공유 / [culture_harpy-aerie.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_harpy-aerie.asset:30>): 역할bitset/온도±허용/환기/조명/청결/공간: 4/18±10/70/50..100/35/공유 / [culture_golem-core.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_golem-core.asset:30>): 역할bitset/온도±허용/환기/조명/청결/공간: 96/18±15/50/30..90/60/개인 / [culture_demon-contract.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_demon-contract.asset:30>): 역할bitset/온도±허용/환기/조명/청결/공간: 544/27±8/35/20..70/40/개인 / [culture_beastkin-pack.asset 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_beastkin-pack.asset:31>): 역할bitset/온도±허용/환기/조명/청결/공간: 5/24±8/35/20..80/30/공유 / [culture_adventurer-frontier.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Narrative/Cultures/culture_adventurer-frontier.asset:30>): 역할bitset/온도±허용/환기/조명/청결/공간: 5/20±10/45/55..100/40/공유 / [BuildingPrimitives.cs 16행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Buildings/Core/BuildingPrimitives.cs:16>): 선호역할bitset해석
- 감사 위키 근거: [culture-vampire-nightcourt.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-vampire-nightcourt.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음 / [culture-slime-confluence.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-slime-confluence.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음 / [culture-orc-vigil.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-orc-vigil.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음 / [culture-myconid-grove.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-myconid-grove.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음 / [culture-kobold-toolclan.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-kobold-toolclan.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음 / [culture-harpy-aerie.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-harpy-aerie.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음 / [culture-golem-core.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-golem-core.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음 / [culture-demon-contract.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-demon-contract.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음 / [culture-beastkin-pack.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-beastkin-pack.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음 / [culture-adventurer-frontier.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/culture-adventurer-frontier.json:2>): facts[];음식/관습관계·문화서술만,방선호수치없음
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 작성값·공통선호/AI연결 정적 확인이며 플레이·밸런스 적정성은 인증하지 않는다. 각 환경 충족점수의 전체 정규화식은 이 표와 구분한다.
- 이번 재검토: 10문화 도감의 일반 서술에는 역할·온도·환기·광량·청결·공간 선호 수치가 없다. 현재 개척자 연맹 변경 에셋도 해당 선호값은 유지한다. 개인공간 조건은 문이 정확히1개가 아니라1개 이상이다.

## GAP-156 방 소음·개인실 조건과 관찰자별 청결 변화 기분 누락

- 분류: 방 상태 판정·특성 조건부 효과·저장 경계 누락
- 보완할 문서: spaces / entry/character/trait-201 / trait-222 / trait-245
- 현재 확인한 차이: 특성 도감은 규칙 개수만 보여주고 방 조건의 생성 기준과 청소강박의 청결 변화 기분을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 같은 유효 방에 자신 이외의 생산·제작·훈련·마나 시설이 있으면 소음 조건을, 면적8이하·문1개이상이면 개인실 조건을 만든다. 이 조건을 시설 욕구 회복에 전달한다.
  - 청결 변화는 주민별로 직전에 관찰한 같은 방 청결값이 있을 때 비교한다. 청소 강박은 악화 사건에 기본기분-4, 청소 개선 사건에+2를1일 적용한다.
- **위키에 작성하지 않을 정보:**
  - 방 첫 관찰만으로 이전 상태와의 변화 사건이 반드시 발생한다는 설명
- 감사 원본 근거: [RoomEnvironmentExperienceAdapter.cs 165행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentExperienceAdapter.cs:165>): noise/private생산 / [RoomEnvironmentExperienceAdapter.cs 179행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentExperienceAdapter.cs:179>): 관찰자별직전청결 비교 / [Facility.cs 1164행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:1164>): 실제 욕구회복에 방condition전달 / [CharacterIdentityDomainAdapters.cs 1134행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityDomainAdapters.cs:1134>): 청결변화구독→관찰자cleaned/dirty / [Trait_245_compulsive-cleaner.asset 78행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Traits/General/Trait_245_compulsive-cleaner.asset:78>): dirty-4/cleaned+2/1일 authored규칙
- 감사 위키 근거: [trait-201.json 12행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-201.json:12>): 효과/규칙개수만 / [trait-222.json 12행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-222.json:12>): 효과/규칙개수만 / [trait-245.json 12행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-245.json:12>): 효과2/규칙4·일반요약만;기분수치없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 다른 특성의 최종 수면·기분과 관찰자 기록 저장 여부 전체는 인증하지 않는다.
- 관련 GAP(제외 이력 포함): GAP-157
- 이번 재검토: 특성 도감은 규칙 개수만 보여주고 방 조건의 생성 기준과 청소강박의 청결 변화 기분을 설명하지 않는다.

## GAP-157 얕은잠 특성의 방 소음 조건 수면회복0.8배 누락

- 분류: 특성 도감 효과·조건·정체성 규칙 상세 누락
- 보완할 문서: entry/character/trait-* / species-culture-and-life / residents-and-work
- 현재 확인한 차이: 얕은잠 도감의 효과·규칙 개수에는 소음 조건 수면회복0.8배가 없다. 실제 Rest 원천 수면 회복은 선택 특성의 조건부 효과를 투영해 회복량에 곱한다.
- **위키에 작성할 정보:**
  - 얕은잠 특성은 방 소음 조건이 활성일 때 휴식으로 얻는 수면 회복에0.8배를 적용한다. 소음 조건은 같은 방의 다른 생산·제작·훈련·마나 시설에서 생긴다.
- **위키에 작성하지 않을 정보:**
  - 특성100종·효과167개·조건50개·정체성108개 전체가 검증되었다는 설명
- 감사 원본 근거: [Trait_201_light-sleeper.asset 56행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V20/Traits/General/Trait_201_light-sleeper.asset:56>): sleep-recovery바인딩0.8+조건GUID / [effect_character_sleep-recovery_multiply.asset 17행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Effects/Definitions/effect_character_sleep-recovery_multiply.asset:17>): target sleep-recovery/Multiply / [room_noise.asset 16행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Effects/Conditions/room_noise.asset:16>): conditionId room:noise / [RoomEnvironmentExperienceAdapter.cs 145행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentExperienceAdapter.cs:145>): 현재유효방의 다른생산/제작/훈련/마나시설→room:noise / [Facility.cs 1164행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:1164>): 회복명령에실제condition전달 / [CharacterBuildingVisitorPort.cs 324행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterBuildingVisitorPort.cs:324>): 회복Sleep/Rest와조건전달 / [CharacterStats.cs 292행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterStats.cs:292>): Rest수면회복공유배율곱 / [CharacterStatsProjectionService.cs 323행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs:323>): 상세통계조건부투영 / [CharacterDerivedStatsSnapshot.cs 737행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Effects/Runtime/CharacterDerivedStatsSnapshot.cs:737>): 선택특성을 현재 효과 소스로 수집 / [CharacterGameplayEffectProjector.cs 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Effects/Runtime/CharacterGameplayEffectProjector.cs:49>): 조건비활성바인딩 억제
- 감사 위키 근거: [trait-201.json 12행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-201.json:12>): 효과/정체성개수뿐;조건/수치payload미공개
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 얕은잠 나머지 알람/정체성과 모든 특성 전체를 인증하지 않는다. 겹치는 방 조건 생성은GAP-156과 중복 계수하지 않는다.
- 관련 GAP(제외 이력 포함): GAP-048, GAP-106, GAP-156, GAP-158, GAP-159, GAP-160, GAP-161, GAP-162
- 이번 재검토: 얕은잠 도감의 효과·규칙 개수에는 소음 조건 수면회복0.8배가 없다. 실제 Rest 원천 수면 회복은 선택 특성의 조건부 효과를 투영해 회복량에 곱한다.

## GAP-158 사선 각성의 전투당1회·위급 조건·효과·후유증 누락

- 분류: 극단 전투 특성 실행·상태·저장 설명 누락
- 보완할 문서: combat-and-equipment / entry/character/trait-301
- 현재 확인한 차이: 특성 도감에는 위급 발동 경계와 현재 활성·종료 효과 수치가 없다.
- **위키에 작성할 정보:**
  - 새 전투마다1회, 의식20%이하 또는 체력20%미만에서 사선 각성을 발동할 수 있다. 활성 중 통증·위급체력에 따른 전투 효율 페널티를 억제하고 작성 전투력1.5배·이동1.2배를 적용한다.
  - 전투 명령 완료 또는 취소로 각성이 끝나면2일간 작업0.5배 후유증이 남는다.
- **위키에 작성하지 않을 정보:**
  - 사선 각성이 남은 체력과 무관하게 사망까지 막는다는 설명
- 감사 원본 근거: [ExtremeTraitRuntime.cs 180행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs:180>): encounter별1회 / [CombatRuntimeStatFactory.cs 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatRuntimeStatFactory.cs:27>): 체력비율·의식.2·각성효율억제 / [CharacterCombatCommandRuntime.cs 1171행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CharacterCombatCommandRuntime.cs:1171>): 완료EndLastStand·1229취소 / [Trait_301_last-stand.asset 54행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_301_last-stand.asset:54>): 1.5/1.2/0.5·20%·2일
- 감사 위키 근거: [trait-301.json 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-301.json:27>): 조건/수치없는 설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 현재 발동/효율억제/종료 caller와 작성배율의 정적 확인이다. 실제 전투·상태 저장왕복은 인증하지 않는다.
- 관련 GAP(제외 이력 포함): GAP-157
- 이번 재검토: 특성 도감에는 위급 발동 경계와 현재 활성·종료 효과 수치가 없다.

## GAP-159 기적의 집도의 중대수술12/18/70 판정·후유증 누락

- 분류: 극단 의료 특성 확률·결정론·결과 적용 설명 누락
- 보완할 문서: health-and-body / entry/character/trait-303
- 현재 확인한 차이: 추상 특성 설명에는 중대수술 조건·단회 판정·실패강제와 후유증 수치가 없다.
- **위키에 작성할 정보:**
  - 응급 절차이거나 사망확률15%이상 또는 성공확률50%이하이면 중대 수술이다. 해당 집도의는 같은 수술ID에 한 번만 고정 판정하며 기적12%는 정상 수술 효과를 적용하는 성공을 강제하고 합병증18%는 중대실패를 강제한다. 나머지70%는 기존 성공/실패 난수를 따른다.
  - 이 특성 판정을 수행한 경우1일간 작업0.6배 후유증이 남는다.
- **위키에 작성하지 않을 정보:**
  - 일반70% 분기가 수술 성공70%를 보장한다는 설명
- 감사 원본 근거: [SurgeryRuntime.cs 1766행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:1766>): 현재중대gate·판정·강제결과 / [ExtremeTraitRuntime.cs 269행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs:269>): 사용ID/고정hash/후유증 / [Trait_303_miracle-surgery.asset 66행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_303_miracle-surgery.asset:66>): 12/18및1일
- 감사 위키 근거: [trait-303.json 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-303.json:27>): 조건/수치없는 설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 중대 gate·판정·후유증과 성공/실패 소비의 정적 확인이다. 수술 플레이·사용ID저장 왕복은 인증하지 않는다.
- 관련 GAP(제외 이력 포함): GAP-157
- 이번 재검토: 추상 특성 설명에는 중대수술 조건·단회 판정·실패강제와 후유증 수치가 없다.

## GAP-160 황금 수확의24시간 예약·담당자 고정·12/18 위험 판정 누락

- 분류: 극단 농업 특성 시간·산출·트랜잭션 설명 누락
- 보완할 문서: agriculture-and-livestock / entry/character/trait-304 / inventory-and-carrying
- 현재 확인한 차이: 현재 수확가능 밭에 담당자 예약·24시간 대기·같은 담당자 판정과 고정 확률이 도감에 없다.
- **위키에 작성할 정보:**
  - 수확준비 또는 수확중인 밭에 황금 수확 담당자를 지정하면24시간 뒤 같은 담당자만 예약 판정을 진행할 수 있다. 이미 담당자가 지정된 밭에는 다시 예약할 수 없다.
  - 실행 시드·밭·시도·담당자로 고정되는 판정은 대성공12%·손실18%·일반70%이며 손실은 작성0.5배 결과를 사용한다.
- **위키에 작성하지 않을 정보:**
  - 대성공 최종 작물/종자2.5배·1.5배와 모든 출력 commit을 이 검토가 보장한다는 설명
- 감사 원본 근거: [ExtremeTraitRuntime.cs 362행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs:362>): prepare / [ExtremeTraitRuntime.cs 455행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs:455>): commit/ack / [Trait_304_golden-harvest.asset 70행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_304_golden-harvest.asset:70>): 24시간/12%/18%/손실0.5 / [ExtremeTraitRuntime.cs 299행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs:299>): pending예약과작성지연 / [ExtremeTraitRuntime.cs 399행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs:399>): 시간/밭gate·고정hash·확률분기 / [CropPlotRuntime.cs 1727행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:1727>): 수확가능밭·담당자중복거부·예약 / [CropPlotRuntime.cs 1237행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:1237>): 같은담당자preparedgate
- 감사 위키 근거: [trait-304.json 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-304.json:27>): 추상 설명만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 담당자·시간·위험분기만 정적 확인했다. 최종 효과투영·사망pending·물리출력·저장왕복은 범위 밖이다.
- 관련 GAP(제외 이력 포함): GAP-157, GAP-132
- 이번 재검토: 현재 수확가능 밭에 담당자 예약·24시간 대기·같은 담당자 판정과 고정 확률이 도감에 없다.

## GAP-161 한계 돌파의 지정 생산주문·5초 유지 갱신·종료 누락

- 분류: 극단 생산 특성 주문·상태 lease·위험 설명 누락
- 보완할 문서: production-and-automation / residents-and-work / entry/character/trait-305
- 현재 확인한 차이: 추상 특성 설명에는 지정 주문의 작업 시작 성공 뒤 활성화하고 작업 성공으로 유지시간을 갱신하는 조건이 없다.
- **위키에 작성할 정보:**
  - 한계 돌파는 지정 생산주문·지정 작업자의 사전조건을 검사하고 실제 작업 시작에 성공한 뒤 활성화한다. 같은 주문의 성공 작업이 이어질 때마다 유지시간을5초로 갱신한다.
  - 생산 주기를 완료하면 종료하고, 마지막 갱신 뒤5초를 초과해도 만료되어 후유증 상태로 넘어간다.
- **위키에 작성하지 않을 정보:**
  - 작업 시작이 실패해도 한계 돌파가 무조건 먼저 활성화된다는 설명
- 감사 원본 근거: [ProductionBillSceneFacade.cs 278행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionBillSceneFacade.cs:278>): 긴급생산설정 / [Trait_305_production-limit-break.asset 54행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_305_production-limit-break.asset:54>): 활성/후유증수치 / [ProductionBillSceneFacade.cs 158행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionBillSceneFacade.cs:158>): 사전검증→work성공→특성활성 / [ProductionBillSceneFacade.cs 222행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionBillSceneFacade.cs:222>): 성공작업lease갱신·cycle완료종료 / [ExtremeTraitRuntime.cs 697행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs:697>): 5초lease;752갱신/771초과만료
- 감사 위키 근거: [trait-305.json 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-305.json:27>): 조건/수치없는 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 작업·피로·사고·후유증 배율의 모든 소비와 저장왕복은 범위 밖이다. 실패주입 원자성 시험은 하지 않았다.
- 관련 GAP(제외 이력 포함): GAP-157
- 이번 재검토: 추상 특성 설명에는 지정 주문의 작업 시작 성공 뒤 활성화하고 작업 성공으로 유지시간을 갱신하는 조건이 없다.

## GAP-162 마력 과충전의30% 미만 발동·체력/장비 대가·20초 및 후유증 누락

- 분류: 극단 비전 특성 명령·물리 비용·상태 저장 설명 누락
- 보완할 문서: combat-and-equipment / entry/character/trait-306
- 현재 확인한 차이: 도감에는 마나 경계·중복발동 거부·대가·활성/후유증 시간이 없다.
- **위키에 작성할 정보:**
  - 마나가30%미만이고 과충전 활성 또는 후유증이 남아 있지 않아야 발동한다. 활성시간20초 뒤1일 후유증이 이어진다.
  - 발동 시 최대체력의15% 피해와 선택 장비 최대내구도의25% 손상 명령을 적용한다.
- **위키에 작성하지 않을 정보:**
  - 마나30%일 때도 발동하거나 활성/후유증 중에 다시 겹쳐 켤 수 있다는 설명
- 감사 원본 근거: [ExtremeTraitRuntime.cs 812행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuntime.cs:812>): 마나/활성gate와시계 / [ArcaneOverchargeCommandRuntime.cs 75행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/ArcaneOverchargeCommandRuntime.cs:75>): 체력/장비피해 / [Trait_306_arcane-overcharge.asset 70행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_306_arcane-overcharge.asset:70>): 30%/20초/15%/25%/1일
- 감사 위키 근거: [trait-306.json 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-306.json:27>): 조건/수치없는 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 장비 선택 UI·실제 비전위력1.6/마나회복0.5 배율의 모든 소비·저장왕복은 인증하지 않는다. 손상 명령의 최종 방어·내구 처리도 플레이 재현하지 않았다.
- 관련 GAP(제외 이력 포함): GAP-157
- 이번 재검토: 도감에는 마나 경계·중복발동 거부·대가·활성/후유증 시간이 없다.

## GAP-163 외부 자원 도감의 내부 타입·생성용 영어 설명 노출

- 분류: 내부 콘텐츠 공개 정책·도감 분류·placeholder 설명 오류
- 보완할 문서: facility navigation / exterior activities / expeditions / guests / security
- 현재 확인한 차이: 외부 자원 엔티티는 내부 런타임 정체성을 갖는 원본을 대상으로 내부 모듈명과 생성용 영어 문구를 그대로 보여준다.42개 전부의 메뉴·연구 부재는 확인되지 않았으므로 건설 불가능을 단정하지 않는다.
- **위키에 작성할 정보:**
  - 새로 게시할 규칙 없음. 아래 문서 정정 조치만 적용.
- **위키에 작성하지 않을 정보:**
  - 42개 모든 런타임 시설은 건설 메뉴·연구에 절대로 나타나지 않는다는 설명
  - 원본에서 확인하지 않은 자원 획득·건설 기능을 새로 설명하는 문장
- 감사 원본 근거: [RuntimeBuildingArchetypeCatalog.cs 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/RuntimeBuildingArchetypeCatalog.cs:13>): 내부runtime정체성 / [RuntimeBuildingArchetypeCatalog.cs 75행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/RuntimeBuildingArchetypeCatalog.cs:75>): 외부zone별정의요구
- 감사 위키 근거: [building-runtime-world-resource-node.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-runtime-world-resource-node.json:19>): 내부타입/영문/18WU를건설사양으로노출
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 18WU·석재1이 플레이어 실제 건설 비용으로 소비되는지는 이 정체성 근거만으로 확정하지 않는다. 비용 표시는 일반 건설 사양 승인에서 제외하고 별도 진입점 확인이 필요하다.
- 문서 정정 조치(공개 원고 아님): 외부 자원 도감의 내부 모듈 타입명과 'Generated immutable runtime building archetype authority'라는 작성용 설명을 공개 문장에서 제거한다.
- 관련 GAP(제외 이력 포함): GAP-111
- 이번 재검토: 외부 자원 엔티티는 내부 런타임 정체성을 갖는 원본을 대상으로 내부 모듈명과 생성용 영어 문구를 그대로 보여준다.42개 전부의 메뉴·연구 부재는 확인되지 않았으므로 건설 불가능을 단정하지 않는다.

## GAP-164 외부 구역 자동생성 조건·20초 마모·예외 누락

- 분류: 외부 구역 운영·업무·원정 이동·저장 설명 누락
- 보완할 문서: infrastructure / residents-and-work / expeditions / guests-services-and-performance
- 현재 확인한 차이: 원정 일반 안내에는 외부 구역 기본생성 조건과 상태 마모 주기가 없다.
- **위키에 작성할 정보:**
  - 외부 구역이 하나라도 있으면 기본 구역 자동생성을 건너뛰고, 없을 때만 기본 구역을 만든다.
  - 20초 상태 주기마다 입구와 원정 집결지를 제외한 외부 구역은 경과 주기수당 청결0.7 감소·손상0.08 증가를 적용한다.
- **위키에 작성하지 않을 정보:**
  - 일부 기본 구역이 없어도 기존 구역과 무관하게 항상 자동 보충된다는 설명
- 감사 원본 근거: [ExteriorActivityRuntime.cs 162행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs:162>): 20초tick / [ExteriorActivityRuntime.cs 749행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs:749>): 기본zone생성 / [ExteriorActivityRuntime.cs 904행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs:904>): 청결/손상마모 / [ExteriorActivityRuntime.cs 769행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs:769>): 기존zone존재시자동생성생략 / [ExteriorActivityRuntime.cs 287행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs:287>): 20초조건tick실행 / [ExteriorActivityRuntime.cs 895행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs:895>): 두zone제외한 마모실행
- 감사 위키 근거: [expeditions.md 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/expeditions.md:25>): 일반이동설명만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 8종 레이어·개별 업무·출입·쓰러짐 구조·저장복원 전체는 범위 밖이다.
- 관련 GAP(제외 이력 포함): GAP-163
- 이번 재검토: 원정 일반 안내에는 외부 구역 기본생성 조건과 상태 마모 주기가 없다.

## GAP-165 외부 사건의180초 주기·활성0 조건·환경 확률식 누락

- 분류: 외부 사건 발생·handler·물리 결과·UI·저장 설명 누락
- 보완할 문서: events-and-choices / guests-services-and-performance / economy-and-trade / inventory-and-carrying / infrastructure
- 현재 확인한 차이: 일반 사건 설명에는 별도 외부 사건의 발생 검사와 환경·순찰 배율이 없다.
- **위키에 작성할 정보:**
  - 외부 사건은180초 주기에 활성 외부사건이 없을 때 발생을 검사한다. 확률은 clamp(0.18+야간위험×0.004+날씨압력×0.12-clamp(순찰준비도,0,100)×0.0025,0.08,0.72)다.
  - 발생 판정을 통과하면 사건 간격 조건을 통과한 처리기 후보에서 종류별 가중치로 선택한다.
- **위키에 작성하지 않을 정보:**
  - 180초마다 외부사건이 반드시 발생하거나 기존 사건과 무제한 겹친다는 설명
- 감사 원본 근거: [ExteriorDomain.cs 187행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Exterior/Core/ExteriorDomain.cs:187>): 발생확률식 / [ExteriorActivityRuntime.cs 163행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs:163>): 180초 / [ExteriorActivityRuntime.cs 297행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs:297>): 180초검사 / [ExteriorActivityRuntime.cs 1014행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Exterior/ExteriorActivityRuntime.cs:1014>): active0/환경확률/pacing후 가중후보
- 감사 위키 근거: [events-and-choices.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/events-and-choices.md:13>): 사건원인일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 6종 개별 화물·절도·구조·환불·물리 정산 및 저장 전체는 인증하지 않는다.
- 이번 재검토: 일반 사건 설명에는 별도 외부 사건의 발생 검사와 환경·순찰 배율이 없다.

## GAP-166 시설 완료 욕구회복의 작성값 우선·식사 별도 처리 누락

- 분류: 시설별 욕구 회복 payload·작성값 우선순위·저장 설명 누락
- 보완할 문서: facility entries / residents-and-work / health-and-community
- 현재 확인한 차이: 시설 역할 소개에는 작성 회복값과 역할 fallback이 중첩되지 않는 점, 실제 식사 소비 분기에서는 이 일반 회복을 호출하지 않는 점이 없다.
- **위키에 작성할 정보:**
  - 물리 식사 소비를 처리하는 이용은 음식 효과 경로를 사용하고 일반 시설 작성 회복을 별도로 중첩하지 않는다. 그 외 이용의 작성 욕구회복이 하나라도 있으면 작성값을 적용하고 역할별 기본 회복을 더하지 않는다.
  - 작성 회복이 없는 경우 역할 fallback을 사용한다. 예를 들어 휴식은 수면35·기분12, 훈련은 재미15·기분5이며 이는 작성값이 있을 때 추가 보너스가 아니다.
- **위키에 작성하지 않을 정보:**
  - 19시설의 모든 정상 이용이 작성 회복과 역할 회복을 동시에 받는다는 설명
- 감사 원본 근거: [Facility.cs 616행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:616>): meal분기else에서만작성recovery / [Facility.cs 1090행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:1090>): HasEffect면작성회복후return
- 감사 위키 근거: [building-1021.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1021.json:19>): 회복역할만설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 19시설114입력 전수와 캐릭터별 최종 회복량은 인증하지 않는다.
- 관련 GAP(제외 이력 포함): GAP-138
- 이번 재검토: 시설 역할 소개에는 작성 회복값과 역할 fallback이 중첩되지 않는 점, 실제 식사 소비 분기에서는 이 일반 회복을 호출하지 않는 점이 없다.

## GAP-167 훈련시설4종의 일반경험치24·전투숙련0.50·기분 누락

- 분류: 훈련 이용 progression·숙련 분기·기분 payload 설명 누락
- 보완할 문서: residents-and-work / facility entries
- 현재 확인한 차이: 현행4훈련시설을 직접 읽어 일반경험치24와180초 기분4/3/5/4를 확인했다. 실제 이용 완료가 모듈 일반경험치와 별도 전투훈련을 모두 호출한다. 위키는 훈련 역할·일반1XP만 있어 이 구분과 수치가 없다.
- **위키에 작성할 정보:**
  - 훈련허수아비·사격과녁·중량훈련석·대련매트는 정상 이용 완료에 일반경험치24를 지급한다. 기본기분은 각각+4/+3/+5/+4, 지속180초다.
  - 별도 전투훈련 경로는 시설 주숙련이 근접전투 또는 원거리전투일 때 해당 숙련 경험치0.50을 훈련으로 지급한다. 사격과녁은 원거리, 나머지3시설은 근접이다. 일반경험치24와 이 숙련값은 서로 다른 보상이다.
- **위키에 작성하지 않을 정보:**
  - 일반경험치24를 전투숙련24로 설명하는 문장
- 감사 원본 근거: [BuildingAbility.cs 534행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs:534>): 일반XP·기분입력소비 / [CharacterBuildingVisitorPort.cs 502행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterBuildingVisitorPort.cs:502>): 완료모듈+전투훈련호출 / [AbilityWork.cs 267행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Ability/AbilityWork.cs:267>): 주숙련근접/원거리만.50 / [T01_훈련허수아비.asset 163행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/T01_훈련허수아비.asset:163>): XP24·기분4/3/5/4·180초;47주숙련 / [T02_사격과녁.asset 163행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/T02_사격과녁.asset:163>): XP24·기분4/3/5/4·180초;47주숙련 / [T03_중량훈련석.asset 158행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/T03_중량훈련석.asset:158>): XP24·기분4/3/5/4·180초;47주숙련 / [T04_대련매트.asset 132행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/T04_대련매트.asset:132>): XP24·기분4/3/5/4·180초;47주숙련
- 감사 위키 근거: [residents-and-work.md 74행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:74>): 연습1XP일반기준만 / [building-1040.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1040.json:19>): 훈련역할·건설비만;보상값없음 / [building-1041.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1041.json:19>): 훈련역할·건설비만;보상값없음 / [building-1042.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1042.json:19>): 훈련역할·건설비만;보상값없음 / [building-1043.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1043.json:19>): 훈련역할·건설비만;보상값없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 4시설작성·완료호출·숙련매핑 정적 확인이다. 기분정책/숙련감쇠 이후 최종 상태와 실제 플레이는 인증하지 않는다.
- 이번 재검토: 현행4훈련시설을 직접 읽어 일반경험치24와180초 기분4/3/5/4를 확인했다. 실제 이용 완료가 모듈 일반경험치와 별도 전투훈련을 모두 호출한다. 위키는 훈련 역할·일반1XP만 있어 이 구분과 수치가 없다.

## GAP-168 목욕통·침대3종의 기본 회복 입력과 일반 이용 적용 누락

- 분류: 시설 이용 생명력·부상·원정 stress payload·적용 조건·저장 설명 누락
- 보완할 문서: expeditions / medical-care-and-surgery / health-and-community / facility entries
- 현재 확인한 차이: 4시설 입력과 일반 이용 완료 호출을 확인했다. 도감의 원정대 회복이라는 설명과 달리 귀환자·stress양수 조건은 없다. 최대체력 비율은 성능 보정 전 입력이며 현재 없는 P1_RestRoom/P1_Washroom 경로를 근거에 유지하면 오류다.
- **위키에 작성할 정보:**
  - 체력회복 기본입력(최대체력비율)/부상감소/스트레스감소는 목욕통0.08/0.12/26, 간이침대0.12/0.03/12, 정식침대0.22/0.08/22, 이층침대0.18/0.05/18이다.
  - 정상 시설 이용 완료의 회복은 원정 귀환자에게만 제한되지 않는다. 체력회복은 추가 상처회복 성능 배율을 거치며 사망자는 회복하지 않으므로 표의 비율이 최종 회복량을 보장하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 모든 이용자가 최대체력8/12/22/18%를 그대로 확정 회복한다는 설명
  - 현재 없는P1휴식방·세면장을 현행 에셋이라고 안내하는 문장
- 감사 원본 근거: [H04_목욕통.asset 175행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H04_목욕통.asset:175>): 0.08/0.12/26 / [R01_간이침대.asset 160행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/R01_간이침대.asset:160>): 0.12/0.03/12 / [R02_정식침대.asset 164행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/R02_정식침대.asset:164>): 0.22/0.08/22 / [R03_이층침대.asset 166행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/R03_이층침대.asset:166>): 0.18/0.05/18 / [Facility.cs 621행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:621>): 정상 이용 후 ApplyFacilityUseCompleted 호출 / [ModularFacilityRuntimeEffects.cs 151행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/ModularFacilityRuntimeEffects.cs:151>): 모든 IBuildingUseCompletedRuntimeAbility 실행 / [BuildingAbility.cs 333행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs:333>): 회복 입력을 actor port로 전달 / [CharacterLifecycle.cs 316행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterLifecycle.cs:316>): 귀환자/stress 양수 gate 없이 Heal/부상 감소/stress clamp / [CharacterStats.cs 861행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterStats.cs:861>): 사망 회복 제외 및 wound-recovery 성능 곱
- 감사 위키 근거: [building-1060.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1060.json:19>): 원정대 회복 역할만; 수치/일반사용자 효과 없음 / [building-1020.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1020.json:19>): 원정대 회복 역할만; 수치/일반사용자 효과 없음 / [building-1021.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1021.json:19>): 원정대 회복 역할만; 수치/일반사용자 효과 없음 / [building-1022.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1022.json:19>): 원정대 회복 역할만; 수치/일반사용자 효과 없음 / [expeditions.md 54행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/expeditions.md:54>): 의료 가이드로 귀환 회복 링크만 / [medical-care-and-surgery.md 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:34>): 다른 의료 처리 효과는 있으나 4시설 이용완료 회복 수치 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 구P1_RestRoom.asset·P1_Washroom.asset는 현재F Assets내동명이동본도 발견하지 못했다. 폐기/이동 원인을 추정하지 않고 현재4개 실재 자산만 사용한다. 전체 캐릭터저장과 최종 회복은 실행 재현하지 않았다.
- 이번 재검토: 4시설 입력과 일반 이용 완료 호출을 확인했다. 도감의 원정대 회복이라는 설명과 달리 귀환자·stress양수 조건은 없다. 최대체력 비율은 성능 보정 전 입력이며 현재 없는 P1_RestRoom/P1_Washroom 경로를 근거에 유지하면 오류다.

## GAP-169 경계 신호 나팔의 최대내구120 누락

- 분류: 보안 시설 capability·물리 장비 공급·침입 집결 효과·내구도 설명 누락
- 보완할 문서: invasions-and-defence / residents-and-work / facility entries / item/tool-watch-signal-horn
- 현재 확인한 차이: 도감에 서술이 추가되었지만 최대내구120은 여전히 없다. 공급1개·집결+6초·사용내구1·실패미소모는 이미 공개되어 중복 누락에서 제거한다.
- **위키에 작성할 정보:**
  - 새 경계 신호 나팔의 최대내구도는120이다. 기존 내구 컴포넌트가 없는 새 실물은 현재 내구도도120으로 생성한다.
- **위키에 작성하지 않을 정보:**
  - 재고가 재등장하거나 저장을 불러올 때마다 사용한 나팔 내구가120으로 회복된다는 설명
- 감사 원본 근거: [ItemPrimitives.cs 408행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Items/Core/ItemPrimitives.cs:408>): 나팔최대120·423기본현재=최대 / [WorldItemSpawner.cs 107행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/WorldItemSpawner.cs:107>): 실제물리스폰의BuildInstanceComponents / [WorldItemSpawner.cs 401행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/WorldItemSpawner.cs:401>): 기존내구컴포넌트가없는지원도구에CreateDurability삽입
- 감사 위키 근거: [tool-watch-signal-horn.json 2행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/tool-watch-signal-horn.json:2>): 전체facts무게2.35/적재1/가격131만;최대내구없음 / [invasions-and-defence.md 48행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:48>): 물리1/+6초/내구1/실패미소모이미공개;최대120은없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 기본 생성내구 입력만 확인했다.6시설 capability·경보charge부재·원자소모·복원 전수는 범위 밖이다.
- 이번 재검토: 도감에 서술이 추가되었지만 최대내구120은 여전히 없다. 공급1개·집결+6초·사용내구1·실패미소모는 이미 공개되어 중복 누락에서 제거한다.

## GAP-170 장비 정비의 방어구·방패 대상과35→90% 정책·작업량 누락

- 분류: 장비 정비 정책·수리 주문·물리 물류·작업/재료 공식·저장/UI 설명 누락
- 보완할 문서: combat-and-equipment / residents-and-work / facility/building-8830 / inventory-and-carrying
- 현재 확인한 차이: 수리이력 일반 안내에는 주문 대상·기본정책·손실비율 작업량과 침공 중 대기가 없다.
- **위키에 작성할 정보:**
  - 정비 주문은 방어구와 방패를 대상으로 하며 다른 장비종류는 거부한다. 표준 정책은 내구35% 기준으로 수리해90% 목표를 사용한다. 수리 작업량은12+28×손실내구비율WU다.
  - 침공 중 탈착이 허용되지 않으면 전투 종료 대기로 두고, 그렇지 않으면 물리 운반을 준비한다.
- **위키에 작성하지 않을 정보:**
  - 모든 무기·방어구가 같은 정비 주문 대상이라는 설명
- 감사 원본 근거: [EquipmentMaintenanceRuntime.cs 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs:25>): 표준35%/90% / [EquipmentMaintenanceRuntime.cs 793행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs:793>): 12+28×손실WU / [EquipmentMaintenanceRuntime.cs 738행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs:738>): Armor/Shield외거부 / [EquipmentMaintenanceRuntime.cs 785행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/EquipmentMaintenanceRuntime.cs:785>): 실제WU/목표/전투대기상태와PrepareDelivery
- 감사 위키 근거: [combat-and-equipment.md 340행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:340>): 수리이력일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 다른정책·RF30키트·시설배율·동시수리슬롯·전체정산저장은 인증하지 않는다.
- 이번 재검토: 수리이력 일반 안내에는 주문 대상·기본정책·손실비율 작업량과 침공 중 대기가 없다.

## GAP-171 골렘의 충전 문턱·마나 소비·작업량과 저충전 효과 누락

- 분류: 종족 대체 욕구·시설 업무·재료 소비·상태 효과·저장 설명 누락
- 보완할 문서: species-culture-and-life / residents-and-work / facility/building-1037 / item/resource-mana-crystal
- 현재 확인한 차이: 현재 마력 저장조는 마나 결정1·100WU·충전+50이며 골렘의 새 충전은35이하에서 허용된다. 진행 중 충전은 문턱을 다시 적용하지 않고 예약 재료 소비 실패 시 완료 직전 진척을 남긴다. 해당 도감은 역할만 보여 이 조건·수치를 제공하지 않는다.
- **위키에 작성할 정보:**
  - 골렘은 충전량35이하에서 새 충전을 시작할 수 있다. 마력 저장조 충전은 마나 결정1개와100WU가 필요하며 완료하면 충전량을50만큼 회복하되 최대100을 넘지 않는다. 이미 진행 중인 충전은35문턱 때문에 중단되지 않는다.
  - 충전 완료 시 예약한 마나 결정을 소비한다. 소비가 실패하면 완료 직전 작업 진척을 보존한다.
  - 충전량은 초당0.035×종족 방전 배율만큼 감소한다.25미만이면 저충전 기분-10을5초간 갱신하고,0이면 초당 최대체력의0.25%에 해당하는 비살상 피해가 적용된다(각 갱신의 최소 피해0.1).
- **위키에 작성하지 않을 정보:**
  - 충전 메서드에 없는 시설 도착·배송 완료 검사를 충전의 확정 선행조건이라고 설명하는 문장
  - savev3·취소·이동 전체가 검증됐다는 설명
- 감사 원본 근거: [SpeciesRuntime.cs 396행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/SpeciesRuntime.cs:396>): 골렘·진행중 허용·신규 충전35이하·세계 재료 확인 / [SpeciesRuntime.cs 459행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/SpeciesRuntime.cs:459>): 세계stack 정렬·예약·진척 시작 / [SpeciesRuntime.cs 516행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/SpeciesRuntime.cs:516>): 충전작업 누적·예약물 소비·실패 직전진척 보존·성공시 충전 및 해제 / [SpeciesRuntime.cs 386행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/SpeciesRuntime.cs:386>): 충전량 합계0~100 제한 / [SpeciesRuntime.cs 810행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/SpeciesRuntime.cs:810>): 초당.035×종족chargeRateMultiplier 방전;824저충전기분;834방전비살상피해 / [M02_마력저장조.asset 177행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/M02_마력저장조.asset:177>): 마나1/100WU/+50
- 감사 위키 근거: [building-1037.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1037.json:19>): 욕구회복/생산/보관만안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 충전 직접 메서드의 세계stack 예약/소비를 확인한 정적 대조다. 전체 이동·AI·저장 왕복·취소 실행은 재현하지 않았다.
- 이번 재검토: 현재 마력 저장조는 마나 결정1·100WU·충전+50이며 골렘의 새 충전은35이하에서 허용된다. 진행 중 충전은 문턱을 다시 적용하지 않고 예약 재료 소비 실패 시 완료 직전 진척을 남긴다. 해당 도감은 역할만 보여 이 조건·수치를 제공하지 않는다.

## GAP-172 서비스 운영 모드별 지원시설 효과와 새 세션 적용 경계 누락

- 분류: 서비스 허브/프로세스의 mode gate·stage timing·가격/만족·결제·세션 저장/UI 설명 누락
- 보완할 문서: guests-services-and-performance / economy-and-trade / facility entries 1003·1012·1020~1022·1059~1060·9501
- 현재 확인한 차이: 현재 Direct는 허브 기본값을 사용하고 그 외 모드는 연결 지원시설의 정원·수입·만족도를 더하고 작업속도를 곱한다. 시작 시 선택한 모드의 공정으로 세션 계약을 만든다. 가이드의 공통 세션 설명에는 이 차이와 모드 변경의 신규 세션 경계가 없다.
- **위키에 작성할 정보:**
  - 직접 운영(Direct)은 허브의 기본 정원·요금·만족도와 기본 작업속도를 사용한다. 그 밖의 운영 모드에서는 연결 지원시설의 정원·수입·만족도 보정을 더하고 작업속도 배율을 곱한다. 최종 정원은 최소1명이다.
  - 모드를 바꿔도 이미 시작한 서비스 계약은 바뀌지 않으며 새 손님 세션부터 바뀐 모드가 적용된다.
- **위키에 작성하지 않을 정보:**
  - 서비스 허브가 정확히8종·공정이5종이라고 전수 인증하는 설명
  - 모든 결제·파괴취소·저장 경계가 검증됐다는 설명
- 감사 원본 근거: [ServiceSessionRuntime.cs 241행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs:241>): Direct기본값/다른모드지원시설가산;275정원최소1 / [ServiceSessionRuntime.cs 377행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs:377>): 현재mode의process→387계약고정→394TryBegin / [ServiceRoomBuildingPanelPresenter.cs 187행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ServiceRoomBuildingPanelPresenter.cs:187>): 모드전환은신규손님부터안내
- 감사 위키 근거: [guests-services-and-performance.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:21>): 공통세션/실물흐름은기재;Direct/Managed/Automated수치차이와새세션경계미기재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 8허브 전체 수치·5공정 시간표·결제 및 저장 왕복은 이번 모드별 정적 대조에 포함하지 않았다.
- 이번 재검토: 현재 Direct는 허브 기본값을 사용하고 그 외 모드는 연결 지원시설의 정원·수입·만족도를 더하고 작업속도를 곱한다. 시작 시 선택한 모드의 공정으로 세션 계약을 만든다. 가이드의 공통 세션 설명에는 이 차이와 모드 변경의 신규 세션 경계가 없다.

## GAP-173 서비스 지원시설의 같은 방·호환 허브 연결 우선순위 누락

- 분류: 서비스 지원시설 호환·결정론 링크·feature gate·modifier·전력/UI 설명 누락
- 보완할 문서: guests-services-and-performance / facility entries 1006~1008·1013·1700~1715
- 현재 확인한 차이: 지원시설은 유효한 방에서 같은Grid·같은방·호환 허브만 후보로 삼고 맨해튼 거리,동거리시 안정키 순서로 하나를 선택한다. 가이드는 일반 구역 배치를 설명하지만 이 연결 우선순위를 설명하지 않는다.
- **위키에 작성할 정보:**
  - 서비스 지원시설은 같은 구역 격자와 같은 방 안의 호환되는 허브에만 연결된다. 후보가 여러 개면 가로·세로 이동거리 합이 가장 짧은 허브를 선택하고, 거리가 같으면 고정된 내부 식별 순서로 하나를 선택한다.
- **위키에 작성하지 않을 정보:**
  - 20종 전체 효과·전력 수치와 미사용 필드가 전수 검증됐다는 설명
- 감사 원본 근거: [ServiceRoomLinkRuntime.cs 210행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/ServiceRooms/ServiceRoomLinkRuntime.cs:210>): 같은Grid/room필터 / [ServiceRoomLinkRuntime.cs 216행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/ServiceRooms/ServiceRoomLinkRuntime.cs:216>): 거리/stablekey선택
- 감사 위키 근거: [guests-services-and-performance.md 43행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:43>): 서비스구역일반배치
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 거리 동률의 고정 선택은 확인했지만 플레이어가 지정하는 임의 우선순위로 해석하지 않는다.20종 개별 수치와 전력 전체는 승인 범위가 아니다.
- 관련 GAP(제외 이력 포함): GAP-172
- 이번 재검토: 지원시설은 유효한 방에서 같은Grid·같은방·호환 허브만 후보로 삼고 맨해튼 거리,동거리시 안정키 순서로 하나를 선택한다. 가이드는 일반 구역 배치를 설명하지만 이 연결 우선순위를 설명하지 않는다.

## GAP-174 시설별 실물 급유·잔여 공급시간 규칙 누락

- 분류: 생존 연료 업무·시설별 작성값·전역 위험 계산·저장/UI 설명 누락
- 보완할 문서: residents-and-work / infrastructure / weather-seasons-and-environment / facility entries 1000·1001·1064~1066·1070·1607~1608·9505·9508·9510·9512~9513
- 현재 확인한 차이: 기존 전역 일일 Fuel 수요(기본+한파1 및 위협배율) 주장은 현재 ProcessDailySurvival에 존재하지 않는다. 현재 급유는 시설의 연료 설정과 전용 납품 목적지를 사용하여 실물 소비 영수증을 확인한 뒤 공급시간을 갱신한다. 기반시설 가이드는 일반 연료 설명만 제공한다.
- **위키에 작성할 정보:**
  - 급유할 때는 해당 시설에 정해진 연료를 정해진 수량만큼(최소1개) 시설의 납품 지점에서 소비한다. 실제 소비가 확인되면 그 시설에 정해진 연료 지속시간을 부여한다.
  - 이미 연료가 공급 중인 시설은 급유를 요청해도 연료를 추가 소비하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 전역 일일 연료 소비를 기본수요+한파1×위협배율로 계산한다는 구형 설명
  - 13종의 개별 지속시간·조명 안전 합·야간위험+18·warmth 부재가 전수 확인됐다는 설명
- 감사 원본 근거: [SurvivalFoodRuntime.cs 332행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs:332>): 현재 일일생존 처리: 음식·날씨·부패·물·위험·건강; 구형Fuel일일경로 없음 / [SurvivalFoodRuntime.cs 542행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs:542>): Refuel작업에서SurvivalFacilityWorkRules 호출 / [SurvivalFacilityWorkRules.cs 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFacilityWorkRules.cs:34>): 실제stockRuntime.TryCommitFacilityFuel 위임 / [SurvivalFoodStockRuntime.cs 297행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodStockRuntime.cs:297>): 공급중이면 소비0 반환 / [SurvivalFoodStockRuntime.cs 310행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodStockRuntime.cs:310>): 시설 설정 수량·목적지·급유 후 지속시간으로 intent 작성 / [SurvivalFoodStockRuntime.cs 329행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodStockRuntime.cs:329>): 전용목적지의 물리sink 소비 / [SurvivalFoodStockRuntime.cs 1397행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodStockRuntime.cs:1397>): 실물 영수증이 있을 때1415 공급시간 결과 게시
- 감사 위키 근거: [infrastructure.md 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:25>): 연료/환경일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 시설별 공통 급유 경로를 정적으로 확인했다.13시설 모집단·개별 지속시간·야간위험·UI 및 저장 장애 복구의 실행 검증은 하지 않았다.
- 이번 재검토: 기존 전역 일일 Fuel 수요(기본+한파1 및 위협배율) 주장은 현재 ProcessDailySurvival에 존재하지 않는다. 현재 급유는 시설의 연료 설정과 전용 납품 목적지를 사용하여 실물 소비 영수증을 확인한 뒤 공급시간을 갱신한다. 기반시설 가이드는 일반 연료 설명만 제공한다.

## GAP-175 화덕·가마솥과 벽돌 오븐의 대체 연료·주기 내 선택 고정 누락

- 분류: 생산 보조 연료 허용/금지·결정론 선택·물리 입력·저장/UI 설명 누락
- 보완할 문서: production-quality-and-supply / production / facility entries 1607·1608
- 현재 확인한 차이: 두 시설의 현재 연료 profile은 원목·저급연료·석탄·숯·사일리지·양초를 허용하고 FuelValue2이상을 요구하며 흑색화약을 금지한다. 사용 지원노드당 연료1을 소비하고 선택을 현재 생산 주기 동안 유지한다. 정상·실패배치 종료에서 선택을 초기화하므로 주문 전체 고정은 오기다. 두 도감은 현재 생산 보조 역할만 안내하며 선택 규칙은 없다.
- **위키에 작성할 정보:**
  - 화덕·가마솥과 벽돌 오븐은 원목,저급연료,석탄,숯,사일리지,양초를 연료로 사용할 수 있다. 연료값은 각각10,6,20,24,4,2이며 최소 연료값2가 필요하다. 흑색화약은 사용할 수 없다. 실제 사용하는 지원시설마다 생산 주기당 연료1개를 소비한다.
  - 새 연료는 가용 재고가 있는 후보를 우선하여 고른다. 그 다음 위에 나열한 연료 우선순위,가격÷연료값,가장 오래된 재고,아이템 식별 순서로 결정한다.
  - 선택한 연료가 허용 대상인 동안은 현재 주기 중 품절되어도 다른 연료로 자동 전환하지 않는다. 선택은 저장되지만 정상 완료나 실패 배치 종료 때 초기화되어 다음 주기에 다시 선택할 수 있다.
- **위키에 작성하지 않을 정보:**
  - 주문 전체에서 연료 선택이 영구 고정된다는 설명
  - bufferCapacity24를 실제 연료 적재 정원으로 공개하는 설명
- 감사 원본 근거: [WS08_화덕_가마솥.asset 117행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS08_화덕_가마솥.asset:117>): fuel1 및 허용6/금지black-powder/min2/priority/buffer24 / [WS09_벽돌_오븐.asset 116행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ProductionSupport/WS09_벽돌_오븐.asset:116>): WS08와 같은 fuel/profile / [ResourceEconomyModels.cs 63행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ResourceEconomyModels.cs:63>): 명시금지→허용목록/태그→FuelValue threshold / [ProductionInputLogisticsService.cs 378행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionInputLogisticsService.cs:378>): 필요 지원 tag에서 중복 node1회 비용; 선택은 427부터 재고 미검사로 기존값 유지 / [ProductionInputLogisticsService.cs 442행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionInputLogisticsService.cs:442>): 가용재고 우선 후 priority/단가·값/oldest stack/ID / [ProductionAggregateState.cs 516행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Production/Core/ProductionAggregateState.cs:516>): 선택 저장; 849 save selectedSupplies 복원 / [ProductionBillStateCodec.cs 709행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillStateCodec.cs:709>): 선택 key/item 저장; 1377 canonical/order/item validation / [ProductionBillRuntime.cs 1525행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs:1525>): 정상완료ClearSelectedSupplies;2334실패배치완료도초기화 / [ProductionBuildingPanelPresenter.cs 695행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs:695>): 유틸리티에 물리 연료 표시 / [resource_log.asset 48행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/resource_log.asset:48>): 원목 FuelValue10 / [material_low_fuel.asset 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/material_low_fuel.asset:49>): 저급연료6 / [resource_coal.asset 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/resource_coal.asset:49>): 석탄20 / [material_charcoal.asset 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/material_charcoal.asset:49>): 숯24 / [feed_silage.asset 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/Workshop/feed_silage.asset:49>): 사일리지4 / [craft_candle.asset 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Economy/Items/craft_candle.asset:49>): 양초2
- 감사 위키 근거: [building-1607.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1607.json:19>): 현재facts분류/크기·relations빈배열·생산보조 역할과 건설비만 공개 / [building-1608.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1608.json:19>): 현재facts분류/크기·relations빈배열·생산보조 역할과 건설비만 공개 / [production-quality-and-supply.md 43행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:43>): 공통 연료 경쟁/운영 목표만; 57시설별 소비 소유권 안내
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 저장 필드와 초기화 코드를 확인한 정적 대조이며 실제 품절 플레이·저장 왕복은 재현하지 않았다. bufferCapacity의 실제 정원 소비는 승인 범위에서 제외한다.
- 이번 재검토: 두 시설의 현재 연료 profile은 원목·저급연료·석탄·숯·사일리지·양초를 허용하고 FuelValue2이상을 요구하며 흑색화약을 금지한다. 사용 지원노드당 연료1을 소비하고 선택을 현재 생산 주기 동안 유지한다. 정상·실패배치 종료에서 선택을 초기화하므로 주문 전체 고정은 오기다. 두 도감은 현재 생산 보조 역할만 안내하며 선택 규칙은 없다.

## GAP-176 식재료 선반·저장함의 보존식 전환과 조리 산출량 누락

- 분류: 식량 보존 부품·방 결합·조리 산출·부패 추적 경계 설명 누락
- 보완할 문서: food-and-ecology / facility entries 1009·1054 / item entries survival-preserved-food·survival-cooked-meal
- 현재 확인한 차이: 현재 조리 완료는 방 운영부품 목록의 첫 보존 능력을 선택하여 일반 식사 대신 보존식을 산출한다. 식재료 선반은1개,식재료 저장함은2개로 작성되어 실제 수량 선택에 사용된다. 도감의 일반 보존 역할에는 이 출력 전환·수량이 없다.
- **위키에 작성할 정보:**
  - 조리시설의 같은 방 운영부품에 보존 능력이 있으면 조리 결과가 일반 식사에서 보존식으로 바뀐다. 보존 부품이 여럿이면 운영부품 목록에서 첫 번째 보존 능력을 사용한다.
  - 식재료 선반의 보존식 산출량은 조리 완료당1개,식재료 저장함은2개다.
- **위키에 작성하지 않을 정보:**
  - 보존식 신선도는 항상0이며 어떤 경로에서도 부패하지 않는다는 설명
  - 두 보존 부품의 산출량을 합산한다는 설명
- 감사 원본 근거: [SurvivalFacilityWorkRules.cs 8행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFacilityWorkRules.cs:8>): 방운영Parts첫Preservation;profile예외시자기능력 / [SurvivalFoodRuntime.cs 1060행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs:1060>): 보존능력 조회→보존식ID/수량 선택→1073실물산출 / [D10_식재료선반.asset 143행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D10_식재료선반.asset:143>): preservedMealsPerCook1 / [L05_식재료저장함.asset 143행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/L05_식재료저장함.asset:143>): preservedMealsPerCook2
- 감사 위키 근거: [building-1009.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1009.json:19>): 보존역할만 / [building-1054.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1054.json:19>): 보존역할만
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 방 운영profile을 사용할 수 없어 예외가 발생하는 경우 자기 시설의 보존 능력을 조회하는 분기가 있다. freshness·부패 소비자 전체와 모든 오류 경계는 이 출력/수량 확인에 포함하지 않았다.
- 이번 재검토: 현재 조리 완료는 방 운영부품 목록의 첫 보존 능력을 선택하여 일반 식사 대신 보존식을 산출한다. 식재료 선반은1개,식재료 저장함은2개로 작성되어 실제 수량 선택에 사용된다. 도감의 일반 보존 역할에는 이 출력 전환·수량이 없다.

## GAP-177 재활 보조대·룬 봉합기의 수술 역할과 개별 보정 누락

- 분류: 수술시설 역할·지원 결합·속도/성공/무균 수치·UI 설명 누락
- 보완할 문서: medical-care-and-surgery / facility entries 9507·9513
- 현재 확인한 차이: 재활 보조대는 재활 주시설·속도1.6이며 룬 봉합기는 재활/룬봉합 지원·속도1.35·무균0.12·기본성공보정0.08이다. 실제 같은 방 집계와 작업속도 소비에 연결된다. 가이드의 공통 같은 방 조건은 기공개지만 개별 도감에는 이 역할과 수치가 없다.
- **위키에 작성할 정보:**
  - 재활 보조대는 재활 수술의 주시설이며 시설 작업속도 배율은1.6이다. 룬 봉합기는 같은 방의 지원시설로 재활·룬봉합 기능을 제공하며 속도1.35배,무균도+0.12,기본 시설 성공보정+0.08을 제공한다.
  - 주시설과 지원시설의 속도는 곱하고 무균도·기본 성공보정은 더한다. 합산 시설속도는0.25~3배,무균도는0~1,기본 성공보정은-0.25~0.35로 제한된다. 이 값은 다른 수술 조건과 함께 적용되므로 최종 성공률 증가량과 같지 않다.
- **위키에 작성하지 않을 정보:**
  - 재활 관련 절차2종·30/88WU·rejection/mana 무소비를 전수 확인했다고 설명하는 문장
- 감사 원본 근거: [M07_재활보조대.asset 149행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M07_재활보조대.asset:149>): 속도1.6·주시설1·rune0 / [M13_룬봉합기.asset 139행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M13_룬봉합기.asset:139>): 속도1.35·주시설0·rune1 / [BuildingSurgeryAbilities.cs 160행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs:160>): 재활/룬태그·rune일때무균.12/기본성공.08 / [SurgicalFacilityQuery.cs 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalFacilityQuery.cs:25>): 사용가능 주시설과 같은방의 손상없는 지원능력 수집;65태그합·무균/성공/마취합·속도곱 / [SurgeryRuntimeContracts.cs 84행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs:84>): 시설합산값 제한:무균/마취0~1,속도.25~3,기본성공-.25~.35 / [SurgeryRiskEvaluator.cs 66행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRiskEvaluator.cs:66>): 기본시설보정·무균*.08·마취필수일때마취*.03을 성공계산에 별개 반영;최종.05~.98 / [SurgeryWorkExecutionHandler.cs 116행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryWorkExecutionHandler.cs:116>): 합산시설속도를155실제지속작업에전달 / [SurgeryRuntime.cs 663행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:663>): 주문수술 시설snapshot 확인 후676 실제riskEvaluator에전달
- 감사 위키 근거: [building-9507.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9507.json:19>): 태그/보정없는일반역할/건설비 / [medical-care-and-surgery.md 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:42>): 같은방시설/추가재료조건이미공개 / [building-9513.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9513.json:19>): 룬봉합기현재내부공정재고역할/건설비뿐
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 개별 작성값·공통 수집/수술속도 소비를 정적으로 확인했다. 모든 절차·소모품·UI 분해·저장 왕복의 실행 검증은 하지 않았다.
- 관련 GAP(제외 이력 포함): GAP-118, GAP-119
- 이번 재검토: 재활 보조대는 재활 주시설·속도1.6이며 룬 봉합기는 재활/룬봉합 지원·속도1.35·무균0.12·기본성공보정0.08이다. 실제 같은 방 집계와 작업속도 소비에 연결된다. 가이드의 공통 같은 방 조건은 기공개지만 개별 도감에는 이 역할과 수치가 없다.

## GAP-178 해부대의 주시설 기능·무균도·기본 성공보정 누락

- 분류: 수술시설 역할·같은 방 지원·성공/무균 수치·방 표시 설명 누락
- 보완할 문서: medical-care-and-surgery / facility entry 9502
- 현재 확인한 차이: 해부대는 해부 기능을 제공하는 주시설로 속도1·무균0.1·기본 성공보정0.02를 실제 수술 시설 계산에 제공한다. 개별 도감에는 이 수치가 없다. 사체 적출26WU와 시설 요구는 가이드에 이미 공개되어 전체 누락이 아니다.
- **위키에 작성할 정보:**
  - 해부대는 해부 수술의 주시설이다. 시설 작업속도 배율은1,무균도는0.1,기본 시설 성공보정은+0.02다. 무균도는 성공도 계산에 별도로 반영되며,이 수치를 최종 성공률+2%로 단순 환산하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 사체 적출26WU·시설 요구가 위키 전체에서 누락되었다는 설명
  - 모든 절차 중 해부 요구가 정확히1개라는 미확인 전수 수치
- 감사 원본 근거: [M02_해부대.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M02_해부대.asset:141>): 해당시설현재작성값 / [BuildingSurgeryAbilities.cs 45행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs:45>): surgicalfacility태그/보정소유 / [SurgicalFacilityQuery.cs 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalFacilityQuery.cs:25>): 사용가능 주시설과 같은방의 손상없는 지원능력 수집;65태그합·무균/성공/마취합·속도곱 / [SurgeryRuntimeContracts.cs 84행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs:84>): 시설합산값 제한:무균/마취0~1,속도.25~3,기본성공-.25~.35 / [SurgeryRiskEvaluator.cs 66행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRiskEvaluator.cs:66>): 기본시설보정·무균*.08·마취필수일때마취*.03을 성공계산에 별개 반영;최종.05~.98 / [SurgeryWorkExecutionHandler.cs 116행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryWorkExecutionHandler.cs:116>): 합산시설속도를155실제지속작업에전달 / [SurgeryRuntime.cs 663행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:663>): 주문수술 시설snapshot 확인 후676 실제riskEvaluator에전달
- 감사 위키 근거: [building-9502.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9502.json:19>): 태그/보정없는일반역할/건설비 / [medical-care-and-surgery.md 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:42>): 같은방시설/추가재료조건이미공개
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 개별 작성값과 수술 계산 기여를 정적으로 확인했다. 모든 해부 절차·방 명명·저장 계약의 최신 실행 인증은 아니다.
- 관련 GAP(제외 이력 포함): GAP-118, GAP-119
- 이번 재검토: 해부대는 해부 기능을 제공하는 주시설로 속도1·무균0.1·기본 성공보정0.02를 실제 수술 시설 계산에 제공한다. 개별 도감에는 이 수치가 없다. 사체 적출26WU와 시설 요구는 가이드에 이미 공개되어 전체 누락이 아니다.

## GAP-179 마취 장치의 지원 기능·마취도와 성공 계산 기여 누락

- 분류: 수술 지원시설·태그/재료 요구 분리·성공 보정·미소비 필드 설명 누락
- 보완할 문서: medical-care-and-surgery / facility entry 9505
- 현재 확인한 차이: 현재 마취 장치의 안정도0.35는 마취도0.35와 기본 시설 성공보정0.0105를 제공한다. 실제 성공 공식은 마취 필수 절차에서 합산 마취도×0.03을 별도로 적용하므로0.0105를 최종 성공률 변화 전체로 쓰면 틀리다. 현재 도감은 내부 공정 재고 역할·건설비만 설명한다.
- **위키에 작성할 정보:**
  - 마취 장치는 수술 주시설이 아닌 마취 지원시설이다. 마취도0.35와 기본 시설 성공보정+0.0105를 제공한다.
  - 마취가 필수인 수술은 합산 마취도×0.03이 성공도 계산에 추가로 반영된다. 실제 성공률은 다른 조건과 함께 계산되어5~98%로 제한되므로 마취 장치가 무조건 같은 최종 확률 증가를 보장하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 마취 지원시설만 두면 마취제 실물 요구가 사라진다는 설명
  - 필수11/태그8/예외3 전수 개수 및 itemCost 미소비가 인증됐다는 설명
- 감사 원본 근거: [M05_마취장치.asset 139행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M05_마취장치.asset:139>): 현행anesthesiaStability.35 / [BuildingSurgeryAbilities.cs 80행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs:80>): surgicalfacility태그/보정소유 / [SurgicalFacilityQuery.cs 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalFacilityQuery.cs:25>): 사용가능 주시설과 같은방의 손상없는 지원능력 수집;65태그합·무균/성공/마취합·속도곱 / [SurgeryRuntimeContracts.cs 84행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs:84>): 시설합산값 제한:무균/마취0~1,속도.25~3,기본성공-.25~.35 / [SurgeryRiskEvaluator.cs 66행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRiskEvaluator.cs:66>): 기본시설보정·무균*.08·마취필수일때마취*.03을 성공계산에 별개 반영;최종.05~.98 / [SurgeryWorkExecutionHandler.cs 116행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryWorkExecutionHandler.cs:116>): 합산시설속도를155실제지속작업에전달 / [SurgeryRuntime.cs 663행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:663>): 주문수술 시설snapshot 확인 후676 실제riskEvaluator에전달
- 감사 위키 근거: [building-9505.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9505.json:19>): 현재내부공정재고 역할·건설비뿐;마취수치없음 / [medical-care-and-surgery.md 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:42>): 같은방시설/추가재료조건이미공개
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 마취도와 실제 위험 계산의 연결을 확인했다. 전체 절차·마취제 물류·저장 왕복·UI 실행은 검증하지 않았다.
- 관련 GAP(제외 이력 포함): GAP-118, GAP-119
- 이번 재검토: 현재 마취 장치의 안정도0.35는 마취도0.35와 기본 시설 성공보정0.0105를 제공한다. 실제 성공 공식은 마취 필수 절차에서 합산 마취도×0.03을 별도로 적용하므로0.0105를 최종 성공률 변화 전체로 쓰면 틀리다. 현재 도감은 내부 공정 재고 역할·건설비만 설명한다.

## GAP-180 세정대의 개별 무균·성공 보정과 지원시설별 재료 추가 누락

- 분류: 수술 지원시설·성공/감염 보정·시설 재료비·snapshot 시점 설명 누락
- 보완할 문서: medical-care-and-surgery / facility entry 9504
- 현재 확인한 차이: 세정대는 무균도0.3·기본 시설 성공보정0.024를 제공하며 실제 수술 주문 계획은 연결된 세정 능력마다 물1·소독제1을 추가한다. 가이드에 세정대와 물/소독제 조건은 이미 있지만 개별 보정과 각 지원시설별 합산은 없다.
- **위키에 작성할 정보:**
  - 세정대는 멸균 지원시설이며 무균도+0.3과 기본 시설 성공보정+0.024를 제공한다. 합산 무균도는 성공도 계산에도 별도로×0.08이 반영되므로+0.024를 최종 성공률 변화 전체로 해석하지 않는다.
  - 수술 주문에 포함되는 세정 지원시설마다 깨끗한 물1개와 소독제1개가 추가로 필요하다.
- **위키에 작성하지 않을 정보:**
  - 세정대와 물·소독제 요구 자체가 위키에서 전혀 설명되지 않았다는 문장
  - 절차31/47개수·모든 재료 동결/물류/저장 시점이 검증됐다는 설명
- 감사 원본 근거: [M04_세정대.asset 139행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M04_세정대.asset:139>): 해당시설현재작성값 / [BuildingSurgeryAbilities.cs 62행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs:62>): surgicalfacility태그/보정소유 / [SurgicalFacilityQuery.cs 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalFacilityQuery.cs:25>): 사용가능 주시설과 같은방의 손상없는 지원능력 수집;65태그합·무균/성공/마취합·속도곱 / [SurgeryRuntimeContracts.cs 84행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs:84>): 시설합산값 제한:무균/마취0~1,속도.25~3,기본성공-.25~.35 / [SurgeryRiskEvaluator.cs 66행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRiskEvaluator.cs:66>): 기본시설보정·무균*.08·마취필수일때마취*.03을 성공계산에 별개 반영;최종.05~.98 / [SurgeryWorkExecutionHandler.cs 116행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryWorkExecutionHandler.cs:116>): 합산시설속도를155실제지속작업에전달 / [SurgeryOrderPlanningService.cs 395행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryOrderPlanningService.cs:395>): 지원시설+주시설의 세정능력을 각각 순회하여water/disinfect 추가 / [SurgeryRuntime.cs 663행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:663>): 주문수술 시설snapshot 확인 후676 실제riskEvaluator에전달
- 감사 위키 근거: [building-9504.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9504.json:19>): 태그/보정없는일반역할/건설비 / [medical-care-and-surgery.md 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:42>): 같은방시설/추가재료조건이미공개
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 현재 주문 계획의 지원시설 및 주시설 순회와 추가량을 확인했다. 최종 재료 소비·물류·방 변경 재평가·UI 분해·저장 왕복 전체를 재현한 것은 아니다.
- 관련 GAP(제외 이력 포함): GAP-118, GAP-119
- 이번 재검토: 세정대는 무균도0.3·기본 시설 성공보정0.024를 제공하며 실제 수술 주문 계획은 연결된 세정 능력마다 물1·소독제1을 추가한다. 가이드에 세정대와 물/소독제 조건은 이미 있지만 개별 보정과 각 지원시설별 합산은 없다.

## GAP-181 비전 개조대의 추가 마나·수술 보정·변이 부담 하한 누락

- 분류: 수술 주/지원시설·시설 재료비·성공/합병증 보정·변이 하한 설명 누락
- 보완할 문서: medical-care-and-surgery / facility entry 9512
- 현재 확인한 차이: 같은 방의 비전 개조대는 주문 재료에 마나 결정 2개와 수술 성공 보정 +0.12를 추가한다. 수술 부담 효과에는 같은 방 비전 시설의 변이 하한 중 최댓값을 적용한다. 비전 개조대의 하한은 부담 8이며, 그보다 높은 절차 기본 부담을 낮추지 않는다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 같은 방의 비전 개조대는 주문 재료에 마나 결정 2개와 수술 성공 보정 +0.12를 추가한다.
  - 수술 부담 효과에는 같은 방 비전 시설의 변이 하한 중 최댓값을 적용한다. 비전 개조대의 하한은 부담 8이며, 그보다 높은 절차 기본 부담을 낮추지 않는다.
- **위키에 작성하지 않을 정보:**
  - 공통 절차·일반 저장 재개는 이미 공개돼 있다.
  - 부담8을 변이 발생 확률8% 또는 모든 수술의 고정 결과로 쓰지 않는다.
- 감사 원본 근거: [SurgeryOrderPlanningService.cs 424행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryOrderPlanningService.cs:424>): 같은 방Arcane 능력의마나비용가산 / [SurgicalProcedureEffectHandlers.cs 1095행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalProcedureEffectHandlers.cs:1095>): 1113하한×100,1118부담최소값적용 / [M12_비전개조대.asset 149행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M12_비전개조대.asset:149>): 성공.12/최소변이.08/마나2
- 감사 위키 근거: [medical-care-and-surgery.md 61행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:61>): 88WU·필수 시설·기본 효과 기재 / [medical-care-and-surgery.md 44행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:44>): 일반 시설 위험 입력 설명 / [medical-care-and-surgery.md 129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:129>): 수술 저장/재개 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 이번 재검토: 구체 게임 규칙·제외·검증 한계를 분리

## GAP-182 장기 보관함의 신선도·보존 용기·가동 조건 누락

- 분류: 장기 보관·물리 물류·이중 용량·연료·신선도·수술 support 설명 누락
- 보완할 문서: medical-care-and-surgery / facility entry 9508
- 현재 확인한 차이: 자연 장기의 초기 신선도는 360 게임초다. 정상 보존 중에는 게임 1초마다 신선도가 2/15초 감소하고, 그 밖에는 게임 1초마다 1초 감소한다. 손상되지 않은 보관함 버퍼, 안전한 보존 환경, 필요한 연료 잔량을 충족하고 장기별 보존 용기 1개 소비를 완료해야 보존 혜택을 받는다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 자연 장기의 초기 신선도는 360 게임초다. 정상 보존 중에는 게임 1초마다 신선도가 2/15초 감소하고, 그 밖에는 게임 1초마다 1초 감소한다.
  - 손상되지 않은 보관함 버퍼, 안전한 보존 환경, 필요한 연료 잔량을 충족하고 장기별 보존 용기 1개 소비를 완료해야 보존 혜택을 받는다.
- **위키에 작성하지 않을 정보:**
  - 자동 운반·연료 선택·저장 전체를 이 검증만으로 확정하지 않는다.
- 감사 원본 근거: [SurgicalPartRuntime.cs 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs:18>): 초기360초·보존2/15상수 / [SurgicalPartRuntime.cs 2173행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs:2173>): 가동시설·용기보존성공이면2/15,그밖은1배감소 / [BuildingSurgeryAbilities.cs 102행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs:102>): capacity8와 보존시설 지원 능력 / [SurgicalPartRuntime.cs 2522행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs:2522>): 용기1개 소비 및 2690 보존 환경·시설·연료 가동 검사 / [SurgicalPartRuntime.cs 107행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalPartRuntime.cs:107>): 일시정지 제외 및 clock.DeltaTime을 TickFreshness에 전달; 게임 시간 기준
- 감사 위키 근거: [medical-care-and-surgery.md 55행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:55>): 자연 장기를 보존 가능하다고만 설명 / [medical-care-and-surgery.md 131행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:131>): 적출/설치 저장에 대한 설명만 존재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 연료 질량 우선선택·12500g·전체 저장 왕복 미검증.
- 이번 재검토: 구체 게임 규칙·제외·검증 한계를 분리

## GAP-183 보철 조립대의 수술 지원과 제작 품질 구별 누락

- 분류: 수술 support·작업속도·성공보정·보철 제작·품질·저장 설명 누락
- 보완할 문서: medical-care-and-surgery / facility entry 9506
- 현재 확인한 차이: 보철 조립대는 수술 속도 ×1.1과 성공 보정 +0.06을 제공한다. 이 성공 보정은 제작 부품 품질 가산이 아니다. 제작 부품은 생산 작업이 확정한 작업자 품질을 받아 생성된다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 보철 조립대는 수술 속도 ×1.1과 성공 보정 +0.06을 제공한다.
  - 이 성공 보정은 제작 부품 품질 가산이 아니다. 제작 부품은 생산 작업이 확정한 작업자 품질을 받아 생성된다.
- **위키에 작성하지 않을 정보:**
  - 골렘26WU·필수조립대·설치효율×품질은 기존 설명과 중복이다.
- 감사 원본 근거: [BuildingSurgeryAbilities.cs 181행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs:181>): M06 support 및 qualityBonus->SuccessBonus / [M06_보철조립대.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M06_보철조립대.asset:141>): speed1.1 qualityBonus0.06 / [SurgicalPartProductionOutputHandler.cs 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalPartProductionOutputHandler.cs:128>): 부품 생성에 WorkerQuality를 전달
- 감사 위키 근거: [medical-care-and-surgery.md 97행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:97>): 골렘 필수 조립대와26WU 기재 / [medical-care-and-surgery.md 58행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:58>): 설치 효율×품질 기재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 세 제작식32/32/36WU와skill/58계산 전체 미검증.
- 이번 재검토: 구체 게임 규칙·제외·검증 한계를 분리

## GAP-184 이식 지원시설의 추가 재료와 수술 보정 누락

- 분류: 수술 주/지원시설·시설 재료비·성공/합병증·거부반응·미소비 필드 설명 누락
- 보완할 문서: medical-care-and-surgery / facility entries 9509·9510·9511
- 현재 확인한 차이: 이식 자격이 필요한 절차에는 같은 방 지원 시설의 비용을 합산한다. 순환 이식대는 혈액팩 1개, 면역 조절기는 면역억제제 1개, 격리 회복 침상은 추가 재료 0개다. 순환 이식대·면역 조절기·격리 회복 침상의 성공 보정은 각각 +0.14/+0.08/+0.05, 청정도는 +0.05/+0.05/+0.20이다. 순환 이식대는 속도 ×1.15와 마취 보정 +0.1도 제공한다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 이식 자격이 필요한 절차에는 같은 방 지원 시설의 비용을 합산한다. 순환 이식대는 혈액팩 1개, 면역 조절기는 면역억제제 1개, 격리 회복 침상은 추가 재료 0개다.
  - 순환 이식대·면역 조절기·격리 회복 침상의 성공 보정은 각각 +0.14/+0.08/+0.05, 청정도는 +0.05/+0.05/+0.20이다. 순환 이식대는 속도 ×1.15와 마취 보정 +0.1도 제공한다.
- **위키에 작성하지 않을 정보:**
  - M10거부35%감소·이종이식15.6·필수시설은 이미 공개됐다.
  - M09/M11의거부감소필드를 실제효과로 쓰지 않는다. 현재immuneControl시설만 소비한다.
- 감사 원본 근거: [SurgeryOrderPlanningService.cs 407행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryOrderPlanningService.cs:407>): Transplant 요구시에만지원시설혈액/억제제합산 / [SurgicalProcedureEffectHandlers.cs 1095행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalProcedureEffectHandlers.cs:1095>): immuneControl시설거부감소최댓값만사용 / [BuildingSurgeryAbilities.cs 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs:128>): 시설 tag/primary/sterility/speed/마취 투영 / [M09_순환이식대.asset 148행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M09_순환이식대.asset:148>): 해당 시설의 성공·재료 지원값 / [M10_면역조절기.asset 140행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M10_면역조절기.asset:140>): 해당 시설의 성공·재료 지원값 / [M11_격리회복침상.asset 148행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M11_격리회복침상.asset:148>): 해당 시설의 성공·재료 지원값
- 감사 위키 근거: [medical-care-and-surgery.md 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:57>): 장기이식58WU와 필수시설 / [medical-care-and-surgery.md 60행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:60>): 이종이식72WU·거부15.6 / [medical-care-and-surgery.md 64행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:64>): M10 감소35% 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: M11별도회복속도 전수소비처 미검증.
- 이번 재검토: 구체 게임 규칙·제외·검증 한계를 분리

## GAP-185 수술대별 성공·청정도·속도 보정 누락

- 분류: 수술 주/지원시설·tag·성공/합병증·작업속도·미소비 환자슬롯 설명 누락
- 보완할 문서: medical-care-and-surgery / facility entries 9501·9503·8868~8872
- 현재 확인한 차이: 응급처치대는 성공 보정 −0.05·속도 ×1.3·청정도 0.12, 외과 수술대는 성공 보정 +0.08·속도 ×1·청정도 0.35다. 장기 재생 수술실·회춘 수혈실·룬 동면실·전신 재생조·시간 고정실은 각각 성공 보정 +0.12·속도 ×1·청정도 0.45다. 같은 방 지원 시설의 성공 보정·청정도는 더하고 속도 배율은 곱한다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 응급처치대는 성공 보정 −0.05·속도 ×1.3·청정도 0.12, 외과 수술대는 성공 보정 +0.08·속도 ×1·청정도 0.35다.
  - 장기 재생 수술실·회춘 수혈실·룬 동면실·전신 재생조·시간 고정실은 각각 성공 보정 +0.12·속도 ×1·청정도 0.45다. 같은 방 지원 시설의 성공 보정·청정도는 더하고 속도 배율은 곱한다.
- **위키에 작성하지 않을 정보:**
  - 공통AgeTreatment태그를 다섯시설의모든노화절차호환으로 쓰지 않는다. 절차별주시설제한과위키109정정은GAP-261이다.
  - patientSlots만으로동시환자효과를단정하지않는다.
- 감사 원본 근거: [SurgicalFacilityQuery.cs 46행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalFacilityQuery.cs:46>): 손상/파괴제외;65~69지원합산/속도곱 / [RF68_장기_재생_수술실.asset 155행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF68_장기_재생_수술실.asset:155>): tag8192 및0.12/1/0.45 / [SurgeryRiskEvaluator.cs 66행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRiskEvaluator.cs:66>): 성공 보정+청정도×.08 및 마취 기여 실제 사용 / [SurgicalFacilityQuery.cs 99행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalFacilityQuery.cs:99>): 절차별 PrimaryFacilityDefinitionId 검사; 공통 태그만으로 호환 후보가 되지 않음 / [M01_응급처치대.asset 149행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M01_응급처치대.asset:149>): 수술 성공·속도·청정도 작성값 / [M03_외과수술대.asset 148행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M03_외과수술대.asset:148>): 수술 성공·속도·청정도 작성값 / [RF69_회춘_수혈실.asset 154행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF69_회춘_수혈실.asset:154>): 수술 성공·속도·청정도 작성값 / [RF70_룬_동면실.asset 154행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF70_룬_동면실.asset:154>): 수술 성공·속도·청정도 작성값 / [RF71_전신_재생조.asset 154행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF71_전신_재생조.asset:154>): 수술 성공·속도·청정도 작성값 / [RF72_시간_고정실.asset 154행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/ResearchOverhaul/RF72_시간_고정실.asset:154>): 수술 성공·속도·청정도 작성값
- 감사 위키 근거: [medical-care-and-surgery.md 109행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:109>): 다섯 시설 모두 같은 노화치료 자격 명시 / [medical-care-and-surgery.md 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:42>): 같은방 합산 일반 원칙 명시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 환자슬롯소비처·실제수술실행/왕복미검증.
- 이번 재검토: 구체 게임 규칙·제외·검증 한계를 분리

## GAP-186 일반 생산 능력의 작업 완료 물리 입고·부분 생산 누락

- 분류: 일반 생산시설 category 산출·수량 보정·창고 입고·부분 실패·저장 상태 설명 누락
- 보완할 문서: production / facility entries 1000·1001·9825·1031·1036·1037·1039
- 현재 확인한 차이: 일반 생산 능력은 운영·연구 완료 때 설정 수량, 방문자·진화 배율과 생산 보너스로 요청 수량을 계산한다. 같은 방 창고부터 입고하고 남은 수량은 같은 격자의 다른 창고에 시도한다. 실제 물리 입고 수량만 생산 실적에 기록한다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 일반 생산 능력은 운영·연구 완료 때 설정 수량, 방문자·진화 배율과 생산 보너스로 요청 수량을 계산한다.
  - 같은 방 창고부터 입고하고 남은 수량은 같은 격자의 다른 창고에 시도한다. 실제 물리 입고 수량만 생산 실적에 기록한다.
- **위키에 작성하지 않을 정보:**
  - 일반생산주문의 재공품·출력공간대기와동일경로로쓰지않는다.
- 감사 원본 근거: [ModularFacilityRuntimeEffects.cs 225행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/ModularFacilityRuntimeEffects.cs:225>): 완료gate,235요청량,240입고실적 / [ModularFacilityRuntimeEffects.cs 350행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/ModularFacilityRuntimeEffects.cs:350>): 같은 방→격자 창고 물리 입고 실제 수량만 반환
- 감사 위키 근거: [production.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md:15>): 생산주문·WIP·출력인계 경로 / [production.md 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md:27>): 주문의 출력공간 대기 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 7시설별수량·상품선택·전체저장미검증.
- 이번 재검토: 구체 게임 규칙·제외·검증 한계를 분리

## GAP-187 조명의 시설별 전력·연료 가동과 환경 조도 범위 누락

- 분류: 환경 조도 source·시각 Light2D·room clipping·전력/연료 gating 경계 설명 누락
- 보완할 문서: infrastructure / spaces / weather-seasons-and-environment / facility entries 1064·1065·1066·1070·9824
- 현재 확인한 차이: 전력 소비 능력이 있는 조명은 해당 시설의 전력이, 연료 소비 능력이 있는 조명은 해당 시설의 연료가 필요하다. 화면의 발광과 환경 조도에 같은 발광 조건을 사용한다. 환경 조도는 반경을 올림한 격자 범위에서 세기와 거리를 반영하며, 장벽 칸과 효과 경로가 막힌 칸은 제외한다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 전력 소비 능력이 있는 조명은 해당 시설의 전력이, 연료 소비 능력이 있는 조명은 해당 시설의 연료가 필요하다. 화면의 발광과 환경 조도에 같은 발광 조건을 사용한다.
  - 환경 조도는 반경을 올림한 격자 범위에서 세기와 거리를 반영하며, 장벽 칸과 효과 경로가 막힌 칸은 제외한다.
- **위키에 작성하지 않을 정보:**
  - 전력·연료미연결주장은현재코드와반대다.
  - 연료를전역보유여부로쓰지않는다. 현재HasFuelSupply(source.Building)이다.
  - 시설별세기·반경표는GAP-082owner다.
- 감사 원본 근거: [EnvironmentalFieldRuntime.cs 711행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:711>): 반경ceil·거리·장벽·경로검사 / [ModularFacilityRuntimeEffects.cs 38행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/ModularFacilityRuntimeEffects.cs:38>): LightingRuntimeConfig와 Light2D 구성 / [EnvironmentalFieldRuntime.cs 896행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:896>): source생성;조명gate987~989 / [EnvironmentalFieldRuntime.cs 563행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:563>): 시설별전력/연료와공통발광조건 / [EnvironmentalFieldRuntime.cs 981행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalFieldRuntime.cs:981>): 전력/연료능력에따라gate설정 / [E01_벽횃불.asset 146행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E01_벽횃불.asset:146>): 세기.75/반경2.8 재확인 / [I15_전기_아크등.asset 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Industrial/I15_전기_아크등.asset:141>): 세기1.2/반경5.5 재확인
- 감사 위키 근거: [infrastructure.md 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:23>): 온도 공기 조명을 일반 환경으로 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든방의시각clipping·수술연료요청실행미검증.
- 관련 GAP(제외 이력 포함): GAP-082
- 이번 재검토: 현행 의미 오류 정정: 전역 연료→시설별 연료

## GAP-188 외부 휴식처의 자격·기분·원정 스트레스 회복 누락

- 분류: runtime 외부 구역 Rest 후보 조건·완료 효과·비효과·activity 설명 누락
- 보완할 문서: infrastructure / residents-and-work / expeditions / exterior activities
- 현재 확인한 차이: 외부 휴식처에서는 기분이 85 미만이거나 원정 스트레스가 0보다 클 때 휴식 후보가 된다. 기본 1.4초 휴식을 완료하면 기분 +4를 180초 적용하고 원정 스트레스를 8 회복한다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 외부 휴식처에서는 기분이 85 미만이거나 원정 스트레스가 0보다 클 때 휴식 후보가 된다.
  - 기본 1.4초 휴식을 완료하면 기분 +4를 180초 적용하고 원정 스트레스를 8 회복한다.
- **위키에 작성하지 않을 정보:**
  - 일반피로와원정스트레스를혼동하지않는다.
  - 생성·유지관리는GAP-164와중복작성하지않는다.
- 감사 원본 근거: [BuildingAbility.cs 430행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs:430>): 기본1.4초/+4/+8/180초 / [BuildingAbility.cs 440행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs:440>): 외부 휴식처 및Mood/ExpeditionStress 후보 / [CoreBuildingAbilityHandlers.cs 382행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Buildings/Core/CoreBuildingAbilityHandlers.cs:382>): 실제 Rest완료에서 기분요인·스트레스 회복 적용
- 감사 위키 근거: [residents-and-work.md 129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:129>): 기분 휴식 일반설명 / [residents-and-work.md 131행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:131>): 피로 회복 일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 후보urgency전체산식미검증.
- 관련 GAP(제외 이력 포함): GAP-164
- 이번 재검토: 구체 게임 규칙·제외·검증 한계를 분리

## GAP-189 시설 진화 기여와 실제 방 환경 효과 구별 누락

- 분류: 시설 tag/score/metric 기여·recipe gate·환경 score consumer·비활성 작성값 설명 누락
- 보완할 문서: facility-growth / infrastructure / facility entries
- 현재 확인한 차이: 시설의 방 프로필 기여가 꺼져 있어도 시설 역할과 방어 기여까지 모두 사라지는 것은 아니다. 방 프로필에 기여하는 시설의 호화·위생 점수는 실제 방 환경값에도 더해진다. 모든 진화 점수가 환경 성능을 높이는 것은 아니다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 시설의 방 프로필 기여가 꺼져 있어도 시설 역할과 방어 기여까지 모두 사라지는 것은 아니다.
  - 방 프로필에 기여하는 시설의 호화·위생 점수는 실제 방 환경값에도 더해진다. 모든 진화 점수가 환경 성능을 높이는 것은 아니다.
- **위키에 작성하지 않을 정보:**
  - 127자산을127독립GAP으로세지않는다.
  - 프로필기여꺼짐을시설의모든영향없음으로쓰지않는다.
  - 내부태그누계·validator목록은가이드내용이아니다.
- 감사 원본 근거: [RoomProfile.cs 254행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityEvolution/RoomProfile.cs:254>): contribution flag로 tags/scores/metrics만 gate / [RoomProfile.cs 291행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityEvolution/RoomProfile.cs:291>): role와defense 기여는 flag 밖에서 항상 추가 / [RoomEnvironmentAdapter.cs 428행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:428>): 작성 evolution score에서 Luxury/Hygiene만 환경 값에 가산 / [FacilityEvolutionMutations.cs 128행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionMutations.cs:128>): Combat 점수24를 실제 진화 변이 가중에 사용 / [E12_환기덕트.asset 123행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/E12_환기덕트.asset:123>): contributesToRoomProfile false인 작성 예
- 감사 위키 근거: [facility-growth.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/facility-growth.md:35>): 방프로필의 면적·밀도·위생·연구·마나·방어·물류 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 127/125/2는이전조사수치이며이번모집단재산출안했으므로현행수치주장에서제외.
- 이번 재검토: 구체 게임 규칙·제외·검증 한계를 분리

## GAP-190 대장작업대의 제작 허용 목록·연구·재질 조건 누락

- 분류: 시설별 장비/탄약 제작 허용 목록·주문 gate·물리 재료·품질 반복·저장/UI 설명 누락
- 보완할 문서: production-quality-and-supply / combat-and-equipment / facility entry 1019
- 현재 확인한 차이: 대장작업대에서는 시설 허용 목록에 있는 장비만 주문할 수 있고, 장비별 필수 연구도 끝내야 한다. 시설별 허용 재질 정책에 맞는 구체 재료를 선택해야 하며, 허용되지 않은 장비 주문은 거부된다. 현재 공개 본문은 이 구체 효과·조건을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 대장작업대에서는 시설 허용 목록에 있는 장비만 주문할 수 있고, 장비별 필수 연구도 끝내야 한다.
  - 시설별 허용 재질 정책에 맞는 구체 재료를 선택해야 하며, 허용되지 않은 장비 주문은 거부된다.
- **위키에 작성하지 않을 정보:**
  - 공통품질공식·내부seed·저장절차는신규누락으로중복작성하지않는다.
- 감사 원본 근거: [CombatEquipmentCraftingRuntime.cs 159행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs:159>): 정의별 RequiredResearch gate / [CombatEquipmentCraftingRuntime.cs 202행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs:202>): 시설별 재질 허용 정책 변경 / [S08_대장작업대.asset 88행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/S08_대장작업대.asset:88>): 63개 authored 제작 ID 시작; 31/21/9/기타2 재집계 / [CombatEquipmentCraftingRuntime.cs 411행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatEquipmentCraftingRuntime.cs:411>): 주문 생성에서 시설 허용 snapshot 확인 후 미허용 거부 / [EquipmentCraftingBuildingAbilityHandler.cs 76행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/Buildings/EquipmentCraftingBuildingAbilityHandler.cs:76>): 시설 능력 allowlist를 실제 제작 handler에 전달
- 감사 위키 근거: [production-quality-and-supply.md 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:11>): 공통 품질공식 / [production-quality-and-supply.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:39>): 불가능 목표일 때 재료 미소비 대기
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 63개목록에셋해시는이전검증과동일하나이번출력에서전항목을열거하지않음;작성시실제목록사용.
- 관련 GAP(제외 이력 포함): GAP-124, GAP-125
- 이번 재검토: 구체 게임 규칙·제외·검증 한계를 분리

## GAP-191 서커스 관람석의 관객 정원·선발 조건 누락

- 분류: 공연 room fixture 조건·customer 선택/이동·수익 정산·저장/UI·미소비 작성값 설명 누락
- 보완할 문서: factions-contracts-and-prisoners / guests-services-and-performance / facility entry 1202
- 현재 확인한 차이: 관람석은 시설당 관객 정원2를 제공한다. 같은 방 정원의 합만큼 살아 있는 손님을 방 중심까지 가까운 순서로 선발한다. 무대가 사용 가능한 정식 방에 있고 유효 관람석과 실제 출입문이 있어야 공연 공간 조건을 충족한다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 관람석은 시설당 관객 정원2를 제공한다. 같은 방 정원의 합만큼 살아 있는 손님을 방 중심까지 가까운 순서로 선발한다.
  - 무대가 사용 가능한 정식 방에 있고 유효 관람석과 실제 출입문이 있어야 공연 공간 조건을 충족한다.
- **위키에 작성하지 않을 정보:**
  - 시야 품질 필드의 효과와 저장 내부 정보를 확정 규칙으로 쓰지 않는다.
- 감사 원본 근거: [CircusRuntime.cs 981행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:981>): usable room·관람석·문 gate / [CircusRuntime.cs 1003행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:1003>): capacity 합계만큼 Customer 거리순 선발 / [CS02_관람석.asset 140행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CS02_관람석.asset:140>): capacity2/sightQuality0.8
- 감사 위키 근거: [building-1202.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1202.json:19>): 모듈명과 건설비뿐 / [guests-services-and-performance.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:31>): 공연 공간 일반조건
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 좌석 좌표 전체·sightQuality 전수 소비처·실행/저장 재현 미검증.
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리

## GAP-192 야수 우리의 수용 정원·생포 체력·돌봄 보안 규칙 누락

- 분류: 포획 야생동물 수용/방·문 gate·물리 급식·급수·탈출 위험·축산 작업량·저장 설명 누락
- 보완할 문서: food-and-ecology / factions-contracts-and-prisoners / facility entry 1203
- 현재 확인한 차이: 야수 우리 정원은2이며 생포 및 우리에서 태어난 새끼 등록은 정원을 초과할 수 없다. 생포 대상의 현재 체력은 최대치의35% 이하여야 한다. 야수 우리의 보안값은 55다. 탈출 위험은 길들임 여부, 보안, 굶주림·갈증, 오염, 방과 문 상태를 함께 반영한다. 보안 기여는 (100−보안)에 길든 동물 0.08, 그 밖의 동물 0.35를 곱한다. 급식·급수 필요값은 각각2이며 돌봄 위치에 있는 동물의 욕구를 보고 물자 소비·배송을 처리한다. 현재 공개 가이드에는 이 구체 조건·차이가 설명되어 있지 않다.
- **위키에 작성할 정보:**
  - 야수 우리 정원은2이며 생포 및 우리에서 태어난 새끼 등록은 정원을 초과할 수 없다. 생포 대상의 현재 체력은 최대치의35% 이하여야 한다.
  - 야수 우리의 보안값은 55다. 탈출 위험은 길들임 여부, 보안, 굶주림·갈증, 오염, 방과 문 상태를 함께 반영한다. 보안 기여는 (100−보안)에 길든 동물 0.08, 그 밖의 동물 0.35를 곱한다.
  - 급식·급수 필요값은 각각2이며 돌봄 위치에 있는 동물의 욕구를 보고 물자 소비·배송을 처리한다.
- **위키에 작성하지 않을 정보:**
  - 이 필요값을 실제 하루마다 고정2개씩 무조건 소비한다는 뜻으로 쓰지 않는다.
  - 이미 공개된5초 갱신·우리 정책과 내부 outbox·저장은 중복 작성하지 않는다.
- 감사 원본 근거: [WildlifeCaptureRuntime.cs 320행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs:320>): 35%체력 gate / [WildlifeCaptureRuntime.cs 345행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs:345>): capacity 점유제한 / [WildlifeCaptureRuntime.cs 495행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs:495>): 출생등록 같은capacity gate / [CB01_야수우리.asset 134행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CB01_야수우리.asset:134>): capacity2, security55, Food2/Water2 / [WildlifeCaptureRuntime.cs 1204행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs:1204>): 돌봄이 authored dailyFood를 전달;1125 dailyWater / [WildlifeCaptureRuntime.cs 1228행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs:1228>): 길들임별 .08/.35와 baseSecurity를 escape risk에 사용
- 감사 위키 근거: [building-1203.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1203.json:19>): 모듈명뿐 / [food-and-ecology.md 202행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:202>): 5초 갱신·우리 정책 명시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든 축산 작업량·물류·저장 왕복은 미검증.
- 관련 GAP(제외 이력 포함): GAP-251
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리

## GAP-193 감방 구속대의 포로 수용 정원 누락

- 분류: 포로 housing 선정/점유·배치 조건·interaction 진입·저장/복원·작성 필드 비소비 설명 누락
- 보완할 문서: factions-contracts-and-prisoners / spaces / facility entry 1200
- 현재 확인한 차이: 감방 구속대의 포로 정원은1명이며 이미 배정된 포로가 정원에 도달하면 새 배정을 거부한다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 감방 구속대의 포로 정원은1명이며 이미 배정된 포로가 정원에 도달하면 새 배정을 거부한다.
- **위키에 작성하지 않을 정보:**
  - 방·포로가 쓸 수 없는 문·빈칸 조건은 기존 가이드에 있다.
  - 복원 점유 검증과 미사용 내부 필드는 이 항목의 신규 플레이 규칙이 아니다.
- 감사 원본 근거: [CaptivityRuntime.cs 756행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CaptivityRuntime.cs:756>): assigned>=ability.capacity 거부 / [CP01_감방구속대.asset 129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CP01_감방구속대.asset:129>): capacity1
- 감사 위키 근거: [building-1200.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1200.json:19>): 포로수용 요약 / [factions-contracts-and-prisoners.md 64행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:64>): 포로가 쓸수 있는 문/빈칸 조건 명시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: restraintSlots·baseSecurity 전수 소비처와 저장v3 전체는 미검증.
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리

## GAP-194 서커스 매표소의 표값 배율·관객당 수익 누락

- 분류: 공연 venue 수익 배율/가산·다중 fixture 합성·예측/정산·저장 설명 누락
- 보완할 문서: guests-services-and-performance / factions-contracts-and-prisoners / facility entry 1204
- 현재 확인한 차이: 같은 공연 방의 매표소마다 표값 배율1.15를 곱하고 관객당 가산 수익1을 더한다. 전체 표값 배율은1~2.5로 제한된다. 주문 생성 때 표값은 기본 표값×합성 배율을 반올림한 값(최소1)으로 고정한다. 정산 수익은 관객 수×(고정 표값+관객당 가산 수익)이다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 같은 공연 방의 매표소마다 표값 배율1.15를 곱하고 관객당 가산 수익1을 더한다. 전체 표값 배율은1~2.5로 제한된다.
  - 주문 생성 때 표값은 기본 표값×합성 배율을 반올림한 값(최소1)으로 고정한다. 정산 수익은 관객 수×(고정 표값+관객당 가산 수익)이다.
- **위키에 작성하지 않을 정보:**
  - 코드 필드명과 저장 검증 스키마는 가이드에 싣지 않는다.
- 감사 원본 근거: [CircusProgramForecastProjectionAdapter.cs 95행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusProgramForecastProjectionAdapter.cs:95>): 동일방 시설능력 합성 또는 무대 검증 / [CircusRuntime.cs 594행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:594>): live 주문/정산 소비 / [CircusSaveValidation.cs 220행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Captivity/Core/CircusSaveValidation.cs:220>): 주문 수치의 저장복원 검증 / [CT01_매표소.asset 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CT01_매표소.asset:133>): 시설의 실제 작성 수치
- 감사 위키 근거: [building-1204.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1204.json:19>): 모듈명·건설비만 기재 / [guests-services-and-performance.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:31>): 공연인력/공간/시간 일반설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 현재 정적 연결 확인이며 실제 공연·저장 왕복 실행은 하지 않았다.
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리

## GAP-195 서커스 도박 창구의 수익·만족도 변동 누락

- 분류: 공연 venue 관객당 수익 가산·만족도 난수 범위·예측/정산·저장 설명 누락
- 보완할 문서: guests-services-and-performance / factions-contracts-and-prisoners / facility entry 1205
- 현재 확인한 차이: 같은 공연 방의 도박 창구마다 관객당 가산 수익3과 만족도 변동폭5를 더한다. 공연 주문에 가산 수익과 변동폭이 고정된다. 정산 시 합성 변동폭의 음수~양수 범위에서 만족도 변화를 뽑아 기본·시설 만족도에 더하고 최종0~100으로 제한한다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 같은 공연 방의 도박 창구마다 관객당 가산 수익3과 만족도 변동폭5를 더한다.
  - 공연 주문에 가산 수익과 변동폭이 고정된다. 정산 시 합성 변동폭의 음수~양수 범위에서 만족도 변화를 뽑아 기본·시설 만족도에 더하고 최종0~100으로 제한한다.
- **위키에 작성하지 않을 정보:**
  - 코드 필드명·난수 내부 상태·저장 스키마를 가이드에 싣지 않는다.
  - 가산 수익도 도박 결과에 따라 무작위로 변한다고 쓰지 않는다.
- 감사 원본 근거: [CircusProgramForecastProjectionAdapter.cs 105행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusProgramForecastProjectionAdapter.cs:105>): 동일방 시설능력 합성 또는 무대 검증 / [CircusRuntime.cs 580행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:580>): live 주문/정산 소비 / [CircusSaveValidation.cs 220행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Captivity/Core/CircusSaveValidation.cs:220>): 주문 수치의 저장복원 검증 / [CG01_도박창구.asset 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CG01_도박창구.asset:133>): 시설의 실제 작성 수치
- 감사 위키 근거: [building-1205.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1205.json:19>): 모듈명·건설비만 기재 / [guests-services-and-performance.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:31>): 공연인력/공간/시간 일반설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 예측 화면과 실제 실행·저장 왕복의 재현은 하지 않았다.
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리

## GAP-196 서커스 진행자 단상의 만족도·준비 작업량 보정 누락

- 분류: 공연 venue 만족도 보정·준비 작업량 배율·예측/정산·저장 설명 누락
- 보완할 문서: guests-services-and-performance / factions-contracts-and-prisoners / facility entry 1206
- 현재 확인한 차이: 같은 공연 방의 CA01 진행자 단상마다 만족도6을 더하고 준비 작업량에0.9를 곱한다. 시설 만족도 합계는0~35로 제한된다. 준비 요구량은 max(1, 무대 기본 준비량×합성 배율)로 주문 생성 때 고정한다. 고정 만족도 가산은 정산에 더하고 최종 만족도를 0~100으로 제한한다. 이 구체 수치·소비는 공개 가이드에 없다.
- **위키에 작성할 정보:**
  - 같은 공연 방의 진행자 단상마다 만족도6을 더하고 준비 작업량에0.9를 곱한다. 시설 만족도 합계는0~35로 제한된다.
  - 준비 요구량은 max(1, 무대 기본 준비량×합성 배율)로 주문 생성 때 고정한다. 고정 만족도 가산은 정산에 더하고 최종 만족도를 0~100으로 제한한다.
- **위키에 작성하지 않을 정보:**
  - 코드 필드명과 저장 검증 메타데이터는 가이드에 싣지 않는다.
- 감사 원본 근거: [CircusProgramForecastProjectionAdapter.cs 117행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusProgramForecastProjectionAdapter.cs:117>): 동일방 시설능력 합성 또는 무대 검증 / [CircusRuntime.cs 268행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:268>): live 주문/정산 소비 / [CircusSaveValidation.cs 220행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Captivity/Core/CircusSaveValidation.cs:220>): 주문 수치의 저장복원 검증 / [CA01_진행자단상.asset 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CA01_진행자단상.asset:133>): 시설의 실제 작성 수치
- 감사 위키 근거: [building-1206.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1206.json:19>): 모듈명·건설비만 기재 / [guests-services-and-performance.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:31>): 공연인력/공간/시간 일반설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 공연·저장 왕복 실행은 미검증.
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리

## GAP-197 공연 위험 장치의 사고 확률·만족도 가산 누락

- 분류: 공연 venue 사고 확률·만족도 보정·예측/실행·저장 설명 누락
- 보완할 문서: guests-services-and-performance / factions-contracts-and-prisoners / facility entry 1207
- 현재 확인한 차이: 같은 공연 방의 위험 장치마다 사고 확률0.08과 만족도8을 더한다. 시설 사고 가산은0~0.5,만족도 합계는0~35로 제한된다. 생성한 주문에 보정이 고정되며 공연 시간의25%가 지난 뒤 한 번 사고를 판정한다. 판정 확률은 기본 사고율+시설 가산을0~1로 제한한 값이다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 같은 공연 방의 위험 장치마다 사고 확률0.08과 만족도8을 더한다. 시설 사고 가산은0~0.5,만족도 합계는0~35로 제한된다.
  - 생성한 주문에 보정이 고정되며 공연 시간의25%가 지난 뒤 한 번 사고를 판정한다. 판정 확률은 기본 사고율+시설 가산을0~1로 제한한 값이다.
- **위키에 작성하지 않을 정보:**
  - 코드 필드명·저장 검증 메타데이터는 가이드에 싣지 않는다.
  - 0.08을 기본 확률의8%배로 쓰지 않는다.
- 감사 원본 근거: [CircusProgramForecastProjectionAdapter.cs 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusProgramForecastProjectionAdapter.cs:130>): 동일방 시설능력 합성 또는 무대 검증 / [CircusRuntime.cs 689행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:689>): live 주문/정산 소비 / [CircusSaveValidation.cs 220행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Captivity/Core/CircusSaveValidation.cs:220>): 주문 수치의 저장복원 검증 / [CH01_위험장치.asset 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CH01_위험장치.asset:133>): 시설의 실제 작성 수치
- 감사 위키 근거: [building-1207.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1207.json:19>): 모듈명·건설비만 기재 / [guests-services-and-performance.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:31>): 공연인력/공간/시간 일반설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 사고·저장 왕복 실행은 미검증.
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리

## GAP-198 공연 치료 구역의 사고 피해 감소·대상별 반올림 누락

- 분류: 공연 venue 사고 피해 배율·다중 fixture 합성·실행/저장 설명 누락
- 보완할 문서: guests-services-and-performance / factions-contracts-and-prisoners / facility entry 1208
- 현재 확인한 차이: 같은 공연 방의 치료 구역마다 사고 피해에0.65를 곱하고 최종 배율을0.25~1로 제한한다. 배율은 주문 생성 때 고정한다. 기본 사고 피해8에 치료 구역1개만 적용되면 인물에게5.2 신체 피해가 전달된다. 야생동물은 피해를 정수 올림하므로6이 전달된다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 같은 공연 방의 치료 구역마다 사고 피해에0.65를 곱하고 최종 배율을0.25~1로 제한한다. 배율은 주문 생성 때 고정한다.
  - 기본 사고 피해8에 치료 구역1개만 적용되면 인물에게5.2 신체 피해가 전달된다. 야생동물은 피해를 정수 올림하므로6이 전달된다.
- **위키에 작성하지 않을 정보:**
  - 5.2/6을 모든 치료 구역 조합의 고정 피해로 쓰지 않는다.
- 감사 원본 근거: [CircusProgramForecastProjectionAdapter.cs 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusProgramForecastProjectionAdapter.cs:141>): 치료구역 배율곱 및0.25~1 clamp / [CircusRuntime.cs 714행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:714>): 기본8×배율 / [CircusRuntime.cs 722행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:722>): 주민float ApplyBodyDamage / [CircusRuntime.cs 726행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:726>): 야생동물ceil 정수 ApplyDamage / [CM01_치료구역.asset 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CM01_치료구역.asset:133>): 시설의 실제 작성 수치
- 감사 위키 근거: [building-1208.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1208.json:19>): 모듈명만 존재 / [guests-services-and-performance.md 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:30>): 의료 안전 일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 피해·저장 왕복 실행은 미검증.
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리

## GAP-199 공개 형벌 장치의 잔혹 공연 전용 만족·오염·목격 효과 누락

- 분류: 잔혹 공연 조건부 만족도·오염·목격자 기분 효과와 저장 설명 누락
- 보완할 문서: guests-services-and-performance / factions-contracts-and-prisoners / facility entry 1209
- 현재 확인한 차이: 같은 공연 방의 공개 형벌 장치는 잔혹 공연에서만 만족도 8을 더하고 오염 배율 1.35를 곱하며, 목격자 기분 패널티 기준을 기본 3과 5 중 큰 값으로 올린다. 일반 공연에는 적용하지 않는다. 주문에 효과가 고정된다. 잔혹 공연 종료 오염 기본3에 합성 오염 배율을 적용하며,목격자의 위험 감수 성향에 따라 패널티 기준을1.25~0.45배 한 뒤 최소1의 기분 감소를360초 부여한다. 현재 공개 가이드에는 이 구체 조건·차이가 설명되어 있지 않다.
- **위키에 작성할 정보:**
  - 같은 공연 방의 공개 형벌 장치는 잔혹 공연에서만 만족도 8을 더하고 오염 배율 1.35를 곱하며, 목격자 기분 패널티 기준을 기본 3과 5 중 큰 값으로 올린다. 일반 공연에는 적용하지 않는다.
  - 주문에 효과가 고정된다. 잔혹 공연 종료 오염 기본3에 합성 오염 배율을 적용하며,목격자의 위험 감수 성향에 따라 패널티 기준을1.25~0.45배 한 뒤 최소1의 기분 감소를360초 부여한다.
- **위키에 작성하지 않을 정보:**
  - 위험 감수 성향이 아닌 모든 목격자에게 고정-5라고 쓰지 않는다.
  - 코드 필드명·저장 검증 메타데이터는 가이드에 싣지 않는다.
- 감사 원본 근거: [CircusProgramForecastProjectionAdapter.cs 151행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusProgramForecastProjectionAdapter.cs:151>): 동일방 시설능력 합성 또는 무대 검증 / [CircusRuntime.cs 759행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:759>): live 주문/정산 소비 / [CircusSaveValidation.cs 220행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Captivity/Core/CircusSaveValidation.cs:220>): 주문 수치의 저장복원 검증 / [CP02_공개형벌장치.asset 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CP02_공개형벌장치.asset:133>): 시설의 실제 작성 수치 / [CircusRuntime.cs 620행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:620>): 잔혹/전투오염 기본3에시설배율 적용
- 감사 위키 근거: [building-1209.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1209.json:19>): 모듈명·건설비만 기재 / [guests-services-and-performance.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:31>): 공연인력/공간/시간 일반설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 공연·목격자 실행·저장 왕복은 미검증.
- 관련 GAP(제외 이력 포함): GAP-196, GAP-197
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리

## GAP-200 중앙 무대의 공연자 정원·표값·준비 작업·시간 누락

- 분류: 공연 무대 입력값·주문 고정·작업/저장 설명 누락
- 보완할 문서: guests-services-and-performance / factions-contracts-and-prisoners / facility entry 1201
- 현재 확인한 차이: 중앙 무대의 포로 공연자 정원은2,기본 표값은12,준비 작업량은16,공연 시간은45초다. 유효한 무대에서 공연을 생성하며 시설 보정이 반영된 표값·준비 작업량은 최소1,공연 시간은 최소5초로 고정된다. 준비 작업을 누적해 요구량을 채운 뒤 공연한다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 중앙 무대의 포로 공연자 정원은2,기본 표값은12,준비 작업량은16,공연 시간은45초다.
  - 유효한 무대에서 공연을 생성하며 시설 보정이 반영된 표값·준비 작업량은 최소1,공연 시간은 최소5초로 고정된다. 준비 작업을 누적해 요구량을 채운 뒤 공연한다.
- **위키에 작성하지 않을 정보:**
  - 코드 필드명·저장 검증 스키마는 가이드에 싣지 않는다.
- 감사 원본 근거: [CircusProgramForecastProjectionAdapter.cs 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusProgramForecastProjectionAdapter.cs:41>): 동일방 시설능력 합성 또는 무대 검증 / [CircusRuntime.cs 268행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CircusRuntime.cs:268>): live 주문/정산 소비 / [CircusSaveValidation.cs 220행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Captivity/Core/CircusSaveValidation.cs:220>): 주문 수치의 저장복원 검증 / [CS01_중앙무대.asset 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Captivity/CS01_중앙무대.asset:133>): 시설의 실제 작성 수치
- 감사 위키 근거: [building-1201.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1201.json:19>): 모듈명·건설비만 기재 / [guests-services-and-performance.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:31>): 공연인력/공간/시간 일반설명
- 감사 상태: `owner-gap-confirmed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 공연·저장 왕복 실행은 미검증.
- 이번 재검토: 플레이 규칙을 평문으로 명시하고 내부 저장 스키마·미검증을 분리 준비 작업 누적은 실제 규칙이므로 write에 유지하고, 비작성 대상에 섞였던 감사 메모는 제거했다.

## GAP-203 위생 시설 청소 완료의 방 구성품 청결 복구 누락

- 분류: 시설 청소 능력·work type gate·같은 방 구성품 청결도 적용 설명 누락
- 보완할 문서: residents-and-work / disease-and-public-health / facility entries 1057-1063
- 현재 확인한 차이: 변기·화장실 칸막이·세면대·목욕통·수건걸이·청소도구함·바닥 배수구의 청소 완료는 대상 시설만이 아니라 같은 운영 방 구성품의 청결을 100으로 설정한다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 변기·화장실 칸막이·세면대·목욕통·수건걸이·청소도구함·바닥 배수구의 청소 완료는 대상 시설만이 아니라 같은 운영 방 구성품의 청결을 100으로 설정한다.
- **위키에 작성하지 않을 정보:**
  - 저장 부재는 확인된 규칙이 아니므로 작성하지 않는다.
- 감사 원본 근거: [H01_변기.asset 163행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H01_변기.asset:163>): restoredCleanliness100(다른H02~07도확인) / [ModularFacilityRuntimeEffects.cs 273행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/ModularFacilityRuntimeEffects.cs:273>): work:clean gate 및모든 Parts 청결설정
- 감사 위키 근거: [spaces.md 17행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:17>): 청결조건을 준비하라는 일반서술 / [infrastructure.md 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:23>): 환경청결 일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 청결 상태의 전체 저장 경계와 실제 청소 재현은 미검증.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-204 조리 손질대의 도축 작업·야생 사체 산출 누락

- 분류: 도축 시설 work gate·사체 우선순위·종별 실물 산출·비상 인간형 처리 설명 누락
- 보완할 문서: food-and-ecology / residents-and-work / facility entry 1002 / work reference butchering
- 현재 확인한 차이: 조리 손질대의 도축 기본 작업값은1이며,도축 대상 야생 사체를 선택해 종별 도축 산출물로 물리 변환한다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 조리 손질대의 도축 기본 작업값은1이며,도축 대상 야생 사체를 선택해 종별 도축 산출물로 물리 변환한다.
- **위키에 작성하지 않을 정보:**
  - 인간형 비상 도축의 산출·기분·목격·위생 효과는 별도 생존 검증 없이 작성하지 않는다.
  - 사체 수량 전체를 없앤다고 산출물도 스택 수만큼 자동 배수된다고 쓰지 않는다.
- 감사 원본 근거: [D03_조리손질대.asset 255행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D03_조리손질대.asset:255>): Butcher1초 / [WildlifeCarcassService.cs 351행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Wildlife/WildlifeCarcassService.cs:351>): 최적 사체선택 후 인간형과 종별분기 / [WildlifeCarcassService.cs 388행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Wildlife/WildlifeCarcassService.cs:388>): ButcherYields 전체stack 변환과 freshness제거
- 감사 위키 근거: [food-and-ecology.md 166행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:166>): 사체회수와 처리 일반기준 / [food-and-ecology.md 204행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:204>): 도축 일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 인간형4/2·기분-16/-9·900초·위생-18 전체 효과 미검증.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-205 생산 주문이 없는 조리 시설의 수동 조리와 주문 경계 누락

- 분류: 조리 facility별 식량/연료 입력·작업량·기본 식사 산출·production bill 우선 설명 누락
- 보완할 문서: food-and-ecology / residents-and-work / facility entries 1000-1002 / work reference cooking
- 현재 확인한 차이: 생산 주문 시설이 아닌 간이 화덕/고기 그릴의 수동 조리는 식량1과 연료1을 사용하고 기본 일반 식사2/3을 만든다. 기본 작업값은1.1/1.3이다. 생산 주문이 실행되면 그 주문이 완료 효과를 소유한다. 조리 손질대처럼 생산 작업대인 시설에서 주문이 막히거나 없다고 구형 수동 조리로 우회하지 않는다. 같은 방에 보존 능력이 있으면 수동 조리 산출은 일반 식사 대신 해당 보존 능력의 보존식과 수량을 사용한다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 생산 주문 시설이 아닌 간이 화덕/고기 그릴의 수동 조리는 식량1과 연료1을 사용하고 기본 일반 식사2/3을 만든다. 기본 작업값은1.1/1.3이다.
  - 생산 주문이 실행되면 그 주문이 완료 효과를 소유한다. 조리 손질대처럼 생산 작업대인 시설에서 주문이 막히거나 없다고 구형 수동 조리로 우회하지 않는다.
  - 같은 방에 보존 능력이 있으면 수동 조리 산출은 일반 식사 대신 해당 보존 능력의 보존식과 수량을 사용한다.
- **위키에 작성하지 않을 정보:**
  - D03의1→1 에셋 필드를 상시 수동 생산 효과로 안내하지 않는다.
  - 기본 작업값을 작업자 속도와 무관한 실제 완료 초로 단정하지 않는다.
- 감사 원본 근거: [D01_간이화덕.asset 175행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D01_간이화덕.asset:175>): Food1->2/1.1초/연료 / [D02_고기그릴.asset 178행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D02_고기그릴.asset:178>): Food1->3/1.3초/연료 / [D03_조리손질대.asset 247행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/D03_조리손질대.asset:247>): Food1->1/1초/연료없음 / [SurvivalFoodRuntime.cs 1021행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs:1021>): 수동 조리 재고 검사·소비·보존 능력의 산출 치환 / [SurvivalWorkExecutionHandler.cs 200행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/Work/SurvivalWorkExecutionHandler.cs:200>): 생산주문 실행;258작업대 수동우회 차단;272공정유체 소비
- 감사 위키 근거: [production.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md:15>): 현재 일반생산주문 흐름 / [food-and-ecology.md 125행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:125>): 조리품 예시만기재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 현재 정적 후보·실행 분기 확인이며 실제 물자 소비/출력 실패 원자성은 미검증.
- 관련 GAP(제외 이력 포함): GAP-176, GAP-186
- 이번 재검토: 생산 주문과 수동 조리 실제 실행 경계를 닫아 적용 범위를 확정

## GAP-206 엄폐 시설의 차단률·방향·내구도와 파괴 경계 누락

- 분류: 엄폐 facility별 차단 확률·방향각·내구도 감쇠·파괴/저장 설명 누락
- 보완할 문서: combat-and-equipment / invasions-and-defence / facility entries 9601-9603
- 현재 확인한 차이: 목재 바리케이드·자루 방책·화살막이의 기본 차단률은 각각 0.35/0.55/0.70, 엄폐 내구도는 60/90/110이다. 차단률은 남은 내구도 비율과 방향 보정의 영향을 받는다. 들어오는 각도15° 이하/35° 이하/55° 이하의 방향 보정은1/0.75/0.4이며 그 밖은0이다. 엄폐에 가하는 피해가 남은 내구도 이상이면 즉시 제거를 요청한다. 내구도0 뒤 한 번 더 맞아야 파괴되는 규칙이 아니다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 목재 바리케이드·자루 방책·화살막이의 기본 차단률은 각각 0.35/0.55/0.70, 엄폐 내구도는 60/90/110이다. 차단률은 남은 내구도 비율과 방향 보정의 영향을 받는다.
  - 들어오는 각도15° 이하/35° 이하/55° 이하의 방향 보정은1/0.75/0.4이며 그 밖은0이다.
  - 엄폐에 가하는 피해가 남은 내구도 이상이면 즉시 제거를 요청한다. 내구도0 뒤 한 번 더 맞아야 파괴되는 규칙이 아니다.
- **위키에 작성하지 않을 정보:**
  - 제거 요청의 성공과 물리 회수·저장 원자성을 이 정적 확인만으로 보장하지 않는다.
- 감사 원본 근거: [CombatCoverDurability.cs 106행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatCoverDurability.cs:106>): 피해시HP감소 및50%손상 / [CombatGridServices.cs 365행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatGridServices.cs:365>): blockChance×durability 전달 / [C01_WoodBarricade.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Combat/C01_WoodBarricade.asset:100>): .35/HP60; 동종C02 .55/90, C03 .70/110 재확인 / [CombatModels.cs 546행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Combat/Core/CombatModels.cs:546>): 15/35/55도별1/.75/.4 배율 / [CombatResolutionService.cs 171행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatResolutionService.cs:171>): 차단 시 max(.5,baseDamage*.18) 엄폐 피해 / [CombatCoverDurability.cs 56행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/CombatCoverDurability.cs:56>): damage<currentHP면 감산, 그렇지 않으면62 destructiveLoss 제거 요청 / [C02_SackBulwark.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Combat/C02_SackBulwark.asset:100>): 자루 방책 차단률.55,103 내구90;31 현재 명칭 / [C03_ArrowScreen.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Combat/C03_ArrowScreen.asset:100>): 화살막이 차단률.7,103 내구110;31 현재 명칭
- 감사 위키 근거: [combat-and-equipment.md 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:57>): High95%만서술 / [building-9601.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9601.json:19>): 엄폐모듈명뿐
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 제거 실패·자재 회수·저장 왕복 실행 미검증.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-207 방어 시설의 가동 중단·마모·고장 효과 누락

- 분류: 방어 facility family/trigger/effect·전력·물리 보급·condition/cooldown·저장 설명 누락
- 보완할 문서: invasions-and-defence / combat-and-equipment / facility entries 30-59,1800-1805,9961
- 현재 확인한 차이: 방어 시설은 상태25 미만(위험 운전 강제 예외),필요 전력 부족,재사용 대기,보급·장전 조건을 검사한다. 발동하면 필요한 보급과 상태를 소모한다. 마모는 고장 위험을 높이며 걸림은 효과0배,오발은0.5배,정상은1배다. 공개 가이드·도감의 인용 구간에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 방어 시설은 상태25 미만(위험 운전 강제 예외),필요 전력 부족,재사용 대기,보급·장전 조건을 검사한다.
  - 발동하면 필요한 보급과 상태를 소모한다. 마모는 고장 위험을 높이며 걸림은 효과0배,오발은0.5배,정상은1배다.
- **위키에 작성하지 않을 정보:**
  - 23종 모두의 개별 효과·수량과 저장 내부 정보를 이 공통 규칙으로 승인하지 않는다.
- 감사 원본 근거: [DefenseFacilityRuntime.cs 106행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs:106>): CanActivate 경로 / [DefenseFacilityRuntime.cs 165행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs:165>): cooldown gate / [DefenseFacilityRuntime.cs 212행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs:212>): wear와jam판정 / [DefenseFacilitySystem.cs 296행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Defense/DefenseFacilitySystem.cs:296>): 실제 효과 실행 직전에 TryBeginActivation 호출 / [DefenseFacilityRuntime.cs 131행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs:131>): condition25 미만 강제위험 운전 예외;141 전력,165 cooldown,177 보급 / [DefenseFacilityRuntime.cs 203행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Defense/DefenseFacilityRuntime.cs:203>): 소모·마모 후jam/misfire;222 효과배율0/.5/1
- 감사 위키 근거: [invasions-and-defence.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:40>): 방어파괴·경로일반설명 / [combat-and-equipment.md 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:57>): 엄폐일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 시설별 모든 효과·물리 복원·실제 발동 실행은 미검증.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-208 급수 시설별 산출량·기본 작업값 누락

- 분류: 급수 facility별 water per work·작업량·한파 차단·실물 산출 경로 설명 누락
- 보완할 문서: food-and-ecology / residents-and-work / facility entries 1052,1059,1060
- 현재 확인한 차이: 세면대·목욕통·통더미의 급수 산출은 각각 4/6/3개, 기본 작업값은 0.9/1.2/1.1이다. 완료 시 시설 위치에 물을 생성하며 생성 실패 시 창고 입고를 시도한다. 현재 공개 가이드에는 이 구체 조건·차이가 설명되어 있지 않다.
- **위키에 작성할 정보:**
  - 세면대·목욕통·통더미의 급수 산출은 각각 4/6/3개, 기본 작업값은 0.9/1.2/1.1이다.
  - 완료 시 시설 위치에 물을 생성하며 생성 실패 시 창고 입고를 시도한다.
- **위키에 작성하지 않을 정보:**
  - 현행 급수 능력과 가용성 검사에는 예전 한파 차단 구분이 없다. H03/H04 한파 차단·L03 허용이라고 쓰지 않는다.
  - 기공개 급수 역할을 신규 누락으로 중복 작성하지 않는다.
- 감사 원본 근거: [H03_세면대.asset 171행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H03_세면대.asset:171>): 4/0.9 / [H04_목욕통.asset 185행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H04_목욕통.asset:185>): 6/1.2 / [L03_통더미.asset 153행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/L03_통더미.asset:153>): 3/1.1 / [SurvivalFacilityWorkRules.cs 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFacilityWorkRules.cs:31>): 현재CanDrawWater는능력존재만확인;한파분기없음 / [SurvivalFoodRuntime.cs 968행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs:968>): 급수량소비와물리출력
- 감사 위키 근거: [building-1052.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1052.json:19>): 보관/용수취수역할 / [building-1059.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1059.json:19>): 급수역할 / [building-1060.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1060.json:19>): 급수역할
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 완료 시간은 작업 속도의 영향을 받으며 PlayMode 실행·출력 실패 원자성은 미검증.
- 이번 재검토: 현행 변경 정정: 한파 차단 구분 삭제

## GAP-209 환기 시설의 전역 위생 위험과 방 환기 기여 구별 누락

- 분류: 환기 facility별 위생/연기 위험 감소와 전역 생존/room environment 소비 차이 설명 누락
- 보완할 문서: disease-and-public-health / spaces / facility entries 1059,1060,1062,1063,1072
- 현재 확인한 차이: 방 표지판/세면대/목욕통/청소도구함/바닥배수구의 위생·연기 감소값은 각각3·3/10·4/16·6/12·4/18·8이다. 전역 위생 위험 계산은 위생 감소값만 빼며,방 환기 기여에는 위생 감소값+연기 감소값×0.5를 더한다. 공개 가이드·도감의 인용 구간에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 방 표지판/세면대/목욕통/청소도구함/바닥배수구의 위생·연기 감소값은 각각3·3/10·4/16·6/12·4/18·8이다.
  - 전역 위생 위험 계산은 위생 감소값만 빼며,방 환기 기여에는 위생 감소값+연기 감소값×0.5를 더한다.
- **위키에 작성하지 않을 정보:**
  - 연기 감소값이 전역 위생 위험에서 직접 차감된다고 쓰지 않는다.
  - 방 환기 기여를 칸별 공기질이 같은 수치만큼 즉시 오른다는 뜻으로 쓰지 않는다.
- 감사 원본 근거: [SurvivalFacilityWorkRules.cs 271행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFacilityWorkRules.cs:271>): 전역hygiene만합산 / [RoomEnvironmentAdapter.cs 464행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:464>): 방위생+연기×0.5 / [H07_바닥배수구.asset 140행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/H07_바닥배수구.asset:140>): 18/8(다른4개도직접확인) / [SurvivalFacilityWorkRules.cs 163행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/SurvivalFacilityWorkRules.cs:163>): 전역 위생 위험에서환기위생값차감
- 감사 위키 근거: [weather-seasons-and-environment.md 33행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:33>): 공기/환기일반설명 / [building-1059.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1059.json:19>): 환기역할만기재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 환경 변화 실행은 미검증.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-210 보관 시설의 질량 한도·방 재고 정원 표기 누락

- 분류: 시설별 보관 용량·카테고리 제한·물리 재고/저장·방 운영 재고 정원 설명 누락
- 보완할 문서: inventory-and-carrying / spaces / facility entries 1015,1018,1025,1028,1032-1037,1050-1056,1062,1802-1803,9508
- 현재 확인한 차이: 보관 시설의 물리 창고는 질량 한도와 허용 재고 분류를 가진다. 방 재고 정원과 물리 창고의 질량 한도는 서로 다른 값이다. 통더미는 방 정원14와 물리 질량 한도12500g을 별도로 가진다. 가이드는 시설별 용량을 도감에서 확인하라고 하지만 통더미 도감에는 보관 역할만 있고 이 두 한도 값은 없다.
- **위키에 작성할 정보:**
  - 보관 시설의 물리 창고는 질량 한도와 허용 재고 분류를 가진다. 방 재고 정원과 물리 창고의 질량 한도는 서로 다른 값이다. 통더미는 방 정원14와 물리 질량 한도12500g을 별도로 가진다.
- **위키에 작성하지 않을 정보:**
  - 21시설 전수 수치가 확인됐다고 쓰지 않는다.
  - 방 정원을 질량값과 같은 단위로 합치거나 중복 한도로 해석하지 않는다.
- 감사 원본 근거: [Facility.cs 88행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:88>): 물리질량양수이면Warehouse생성 / [Facility.cs 110행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:110>): Warehouse 상태모듈등록 / [RoomFacilityPolicyAdapter.cs 55행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomFacilityPolicyAdapter.cs:55>): 별도category room capacity조회 / [L03_통더미.asset 144행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Modular/L03_통더미.asset:144>): 방정원14·물리질량12500g
- 감사 위키 근거: [inventory-and-carrying.md 51행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/inventory-and-carrying.md:51>): 시설별용량은시설도감에있다고안내 / [building-1052.json 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1052.json:19>): 보관역할만있고용량없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 21시설의 현재 정원·질량 전수 집계와 모든 방 정원 소비처는 미검증.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-211 생산 출력의 바닥 넘침 허용·대기 조건 누락

- 분류: 생산 주문 출력 예약 한도·시설별 완충 배치·물리 출력 수용량·배출 실패 조건 설명 누락
- 보완할 문서: production / facility entries with BuildingProductionBufferAbility (146)
- 현재 확인한 차이: 소비 시설·창고로 출력을 보낼 수 없으면 바닥 넘침이 허용된 시설은 지정된 인접 위치로 남은 출력을 보낸다. 보철 조립대는 바닥 넘침을 허용하지 않으므로 호환 창고도 없으면 출력 불가로 대기한다. 현재 공개 가이드에는 이 구체 조건·차이가 설명되어 있지 않다.
- **위키에 작성할 정보:**
  - 소비 시설·창고로 출력을 보낼 수 없으면 바닥 넘침이 허용된 시설은 지정된 인접 위치로 남은 출력을 보낸다.
  - 보철 조립대는 바닥 넘침을 허용하지 않으므로 호환 창고도 없으면 출력 불가로 대기한다.
- **위키에 작성하지 않을 정보:**
  - 기공개 출력 공간 대기·품질 보존은 중복 작성하지 않는다.
  - 옛 개수 용량을 현재 물리 질량 용량으로 쓰지 않는다.
- 감사 원본 근거: [ProductionDistributionRuntime.cs 379행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionDistributionRuntime.cs:379>): compatible-warehouse-missing-and-overflow-disabled 실패경로 / [M06_보철조립대.asset 159행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Medical/M06_보철조립대.asset:159>): allowOverflowDump0 / [ProductionDistributionRuntime.cs 358행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionDistributionRuntime.cs:358>): 허용 시 actual buffered output을 facility.Position+OverflowOffset으로 이동 / [ProductionOutputBufferCapacityProjector.cs 387행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionOutputBufferCapacityProjector.cs:387>): physical capacity는 reachable prepared branch 기반이며 legacy count와 별도
- 감사 위키 근거: [production.md 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md:27>): 출력공간부족대기와품질보존은이미설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 146시설 등 전체 집계·실제 출력 재현은 미검증.
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리

## GAP-212 수동 준비 배치의 보조 처리기 연결·동시 점유 누락

- 분류: 수동 배치 recipe별 support tag·같은 방 연결·점유 정원·처리 중단·저장 설명 누락
- 보완할 문서: production / facility entries 1602-1604,1613,1615 / passive batch recipe entries
- 현재 확인한 차이: 수동 준비가 끝난 배치 생산은 연결된 적격 보조 처리기의 빈 자리를 점유한 뒤 처리 단계로 전환한다. 이미 처리 중인 다른 주문이 차지한 수가 처리기 정원 이상이면 그 처리기를 사용할 수 없다. 선택된 처리기와 처리 중 필요 자원 조건은 마무리에도 다시 검사한다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 수동 준비가 끝난 배치 생산은 연결된 적격 보조 처리기의 빈 자리를 점유한 뒤 처리 단계로 전환한다.
  - 이미 처리 중인 다른 주문이 차지한 수가 처리기 정원 이상이면 그 처리기를 사용할 수 없다. 선택된 처리기와 처리 중 필요 자원 조건은 마무리에도 다시 검사한다.
- **위키에 작성하지 않을 정보:**
  - 8레시피·5시설 전수가 검증됐다고 쓰지 않는다.
  - 내부 노드 ID·복원 필드 목록은 공개 규칙과 분리한다.
- 감사 원본 근거: [ProductionBillRuntime.cs 1014행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs:1014>): 마무리때고정support node utility재검사 / [ProductionBillRuntime.cs 2201행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs:2201>): 현재node ID의utility검사 / [ProductionBillRuntime.cs 1286행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs:1286>): Preparing 완료→TryOccupyBatchSupport→Processing / [ProductionBillRuntime.cs 2240행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs:2240>): 선택한 support node ID 고정 / [ProductionCycleUtilityService.cs 573행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ProductionCycleUtilityService.cs:573>): 다른 Processing bill 점유수를 capacity와 비교;597 용량초과/부재 구별
- 감사 위키 근거: [production.md 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md:18>): 실행시설과작업자만정하는일반설명 / [production.md 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md:27>): 준비출력대기설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든 레시피/시설·실행/저장 왕복 미검증.
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리

## GAP-213 장례·공동 추모의 시설·준비 키트·고인 조건 누락

- 분류: 종족별 장례 시설 gate·물리 준비품 예약/소비·공동 추모 조건·슬픔/외상 상태 설명 누락
- 보완할 문서: social care / facility entry 8887 / funeral culture entries 10 / funeral preparation supply
- 현재 확인한 차이: 개별 장례는 추모 시설 자격과 고인의 문화 시설 자격을 함께 갖춘 시설에서 장례 준비 키트1개를 예약하고,해당 장례가 필요한 살아 있는 참가자를 대상으로 한다. 공동 추모는 서로 다른 고인3명 이상,가장 이른 사망일과 가장 늦은 사망일의 차이가10일 이하,모든 고인의 문화 자격을 만족하는 추모 시설을 요구한다. 준비 키트는 고인 수만큼 필요하다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 개별 장례는 추모 시설 자격과 고인의 문화 시설 자격을 함께 갖춘 시설에서 장례 준비 키트1개를 예약하고,해당 장례가 필요한 살아 있는 참가자를 대상으로 한다.
  - 공동 추모는 서로 다른 고인3명 이상,가장 이른 사망일과 가장 늦은 사망일의 차이가10일 이하,모든 고인의 문화 자격을 만족하는 추모 시설을 요구한다. 준비 키트는 고인 수만큼 필요하다.
- **위키에 작성하지 않을 정보:**
  - 10일 조건을 현재부터 최근10일 이내에 사망한 사람만 가능하다고 바꾸지 않는다.
  - 10종 전체 문화·심리 회복 기간을 이 검증으로 확정하지 않는다.
- 감사 원본 근거: [FuneralFestivalRuntime.cs 285행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FuneralFestivalRuntime.cs:285>): memorial+culture tag 선택및ID정렬 / [FuneralFestivalRuntime.cs 294행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FuneralFestivalRuntime.cs:294>): 장례준비kit1예약 / [FuneralFestivalRuntime.cs 379행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FuneralFestivalRuntime.cs:379>): 공동추모고인3명gate / [FuneralFestivalRuntime.cs 406행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/FuneralFestivalRuntime.cs:406>): 10일gate
- 감사 위키 근거: [family-education-and-legacy.md 28행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:28>): 장례일반설명 / [family-education-and-legacy.md 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:30>): 10일3명은위기설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 10종 목록·8/15일 회복·trauma8·소비 원자성·실행 재현은 미검증.
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리

## GAP-214 대형 사업 완료의 생산·실내 재배·계약 보정 누락

- 분류: 대형 사업 사무실 gate·연구/BOM/작업량·배송/commit·완료 보정·저장/UI 설명 누락
- 보완할 문서: economy planning / facility entry 1026 / authority-office and prerequisite research entries
- 현재 확인한 차이: 대형 사업 완료에 따라 채석장 생산량은1.25배,실내 농장 재배 산출은1.2배,연금술·약제·증류 시설 산출은1.15배가 된다. 지역 교역소 사업이 완료되면 계약 보상에1.25배를 적용한다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 대형 사업 완료에 따라 채석장 생산량은1.25배,실내 농장 재배 산출은1.2배,연금술·약제·증류 시설 산출은1.15배가 된다.
  - 지역 교역소 사업이 완료되면 계약 보상에1.25배를 적용한다.
- **위키에 작성하지 않을 정보:**
  - 방어 준비·원정 적재 등 모든 대형 사업 효과가 연결됐다고 쓰지 않는다.
  - 6사업 전체 BOM·저장 내부 명세를 이 항목에 승인하지 않는다.
- 감사 원본 근거: [GrandProjectRuntime.cs 109행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/GrandProjectRuntime.cs:109>): 6개사업520..1100WU정의 / [GrandProjectRuntime.cs 512행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/GrandProjectRuntime.cs:512>): 완료사업에따라출력배율설정 / [GrandProjectApplicationAdapter.cs 45행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/Planning/GrandProjectApplicationAdapter.cs:45>): 사무실찾기live adapter / [ProductionOutputFactor.cs 109행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionOutputFactor.cs:109>): 시설 tag의 대형 사업 생산배율 소비 / [CropPlotRuntime.cs 1295행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/CropPlotRuntime.cs:1295>): 실내 작물 crop-indoor 배율 실제 소비 / [RegionalSupplyContractRuntime.cs 494행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/RegionalSupplyContractRuntime.cs:494>): 계약 보상에 ContractRewardMultiplier 전달 / [GrandProjectRuntime.cs 217행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/GrandProjectRuntime.cs:217>): 지역교역소 완료 계약보상1.25배
- 감사 위키 근거: [factions-contracts-and-prisoners.md 185행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:185>): 대형사업은정식주민업무로만언급
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 진행 조건과 전체 사업 BOM·방어/원정 소비처·실제 완료·저장 왕복은 미검증.
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리

## GAP-215 폐기물 원산지별 자동 처리·직접 급여 제한 누락

- 분류: 부패물 원산지별 기본 정책·지원 처분·자동 주문·포획 야생동물 급여·독성 한도·저장 설명 누락
- 보완할 문서: economy waste processing / production and food-and-ecology guides / facility 1085·1097 / waste and recipe entries
- 현재 확인한 차이: 원산지별 처분 정책이 켜져 있고 보관·직접 급여 이외 처분을 선택했을 때만 해당 원산지의 폐기물 자동 처리 주문을 만든다. 직접 급여는 정책이 켜진 직접 급여 원산지이며 독성 기준 미만·정책의 최대 오염 이하이고,동물 식성과 해당 원산지의 급여 규칙이 있어야 한다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 원산지별 처분 정책이 켜져 있고 보관·직접 급여 이외 처분을 선택했을 때만 해당 원산지의 폐기물 자동 처리 주문을 만든다.
  - 직접 급여는 정책이 켜진 직접 급여 원산지이며 독성 기준 미만·정책의 최대 오염 이하이고,동물 식성과 해당 원산지의 급여 규칙이 있어야 한다.
- **위키에 작성하지 않을 정보:**
  - 원산지별 기본값·고정 주기·저장 내부 스키마를 검증 없이 작성하지 않는다.
- 감사 원본 근거: [WasteProcessingRuntime.cs 331행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/WasteProcessingRuntime.cs:331>): enabled이며보관/직접급여아닌정책만자동bill / [WasteProcessingRuntime.cs 382행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/WasteProcessingRuntime.cs:382>): enabled/directFeed/독성임계/설정오염한도검사 / [WasteProcessingRuntime.cs 235행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/WasteProcessingRuntime.cs:235>): exact destination 후보선택
- 감사 위키 근거: [food-and-ecology.md 206행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:206>): 폐기물위생일반주의 / [production.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md:15>): 일반주문만서술
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 4정책 전체 기본값·79/80·10초·영양0.9·저장 왕복 미검증.
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리

## GAP-216 대협약의 계절말 신호 키트·경비 공격 지원 누락

- 분류: 대협약 보상 발동일·시설 semantic gate·정확 물리 키트 보급/소비·방어 전투 보정·저장 설명 누락
- 보완할 문서: invasion defense / ending-monster-accord / alliance-signals research / facility 8812 / alliance-signal-kit
- 현재 확인한 차이: 대협약 보상을 얻은 뒤 양수 날짜가 계절 길이의 배수인 날에 동맹 신호 지원을 사용할 수 있다. 가동 중인 보안 시설 중 동맹 신호 자격을 가진 시설의 전용 보급칸에 신호 키트 정확히1개가 준비되면 지원 활성화를 요청한다. 활성화된 당일 경비의 공격력에1.15배를 적용한다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 대협약 보상을 얻은 뒤 양수 날짜가 계절 길이의 배수인 날에 동맹 신호 지원을 사용할 수 있다.
  - 가동 중인 보안 시설 중 동맹 신호 자격을 가진 시설의 전용 보급칸에 신호 키트 정확히1개가 준비되면 지원 활성화를 요청한다. 활성화된 당일 경비의 공격력에1.15배를 적용한다.
- **위키에 작성하지 않을 정보:**
  - 다른 신호 나팔의 침입 지연6초와 혼동하지 않는다.
  - 신호 키트가 여러 개여도 정상 처리된다는 식의 우회 설명은 쓰지 않는다.
- 감사 원본 근거: [V20CampaignRuntime.cs 1960행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:1960>): 대협약reward+계절말지원일 / [DefenseCombatExecutor.cs 885행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Invasion/DefenseCombatExecutor.cs:885>): 활성당일×1.15 / [DefenseCombatExecutor.cs 914행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Invasion/DefenseCombatExecutor.cs:914>): exact 키트1개및지원활성화 / [InvasionDefenseKitSupplyRuntime.cs 14행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Invasion/InvasionDefenseKitSupplyRuntime.cs:14>): alliance-signals tag
- 감사 위키 근거: [invasions-and-defence.md 48행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:48>): 다른신호나팔의진입지연6초만설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 정확한 소비·pending receipt 복원 결합과 실제 활성화 재현은 미검증.
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리

## GAP-217 시설의 공통 업무 거부·청소 필요·손상 정지 누락

- 분류: 시설 work bitset·작업량·후보 거부·손상 예외·동적 capability 설명 누락
- 보완할 문서: residents-and-work / infrastructure / facility entries
- 현재 확인한 차이: 시설이 지원하지 않는 업무는 배정할 수 없으며 청결75 이상이면 청소 업무를 불필요로 거부한다. 손상 시 정지하도록 정해진 시설이 손상되면 수리를 제외한 업무 배정을 거부한다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 시설이 지원하지 않는 업무는 배정할 수 없으며 청결75 이상이면 청소 업무를 불필요로 거부한다.
  - 손상 시 정지하도록 정해진 시설이 손상되면 수리를 제외한 업무 배정을 거부한다.
- **위키에 작성하지 않을 정보:**
  - 31/260/368 전수 플래그·작업량 집계와 모든 대체 경로를 검증했다고 쓰지 않는다.
- 감사 원본 근거: [BuildingOccupancyAssignment.cs 610행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingOccupancyAssignment.cs:610>): unsupported 거부 / [BuildingOccupancyAssignment.cs 618행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingOccupancyAssignment.cs:618>): Clean 필요없음 거부 / [BuildingOccupancyAssignment.cs 626행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingOccupancyAssignment.cs:626>): disabledWhenDamaged 및Repair예외 / [BuildingOccupancyAssignment.cs 462행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingOccupancyAssignment.cs:462>): 청소필요 임계75
- 감사 위키 근거: [residents-and-work.md 101행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:101>): 업무배정일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체 시설 작업량·모든 대체 경로·실제 업무 재현 미검증.
- 관련 GAP(제외 이력 포함): GAP-203
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리

## GAP-218 운영일 보고의 시설 유지비·손상 수리 예상비 구별 누락

- 분류: 시설별 경제 작성값·운영일 결산·건설 gate·전투/강화 입력 설명 누락
- 보완할 문서: economy-and-trade / invasions-and-defence / facility entries
- 현재 확인한 차이: 운영일 보고용 시설 집계는 벽·문·이동 시설을 제외한 시설의 유지비를 합산한다. 손상 시설의 수리 예상비는 별도 항목으로 같은 시설 유지비 값을 합산한다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 운영일 보고용 시설 집계는 벽·문·이동 시설을 제외한 시설의 유지비를 합산한다.
  - 손상 시설의 수리 예상비는 별도 항목으로 같은 시설 유지비 값을 합산한다.
- **위키에 작성하지 않을 정보:**
  - 보고용 예상비를 곧바로 실제 화폐 차감이라고 쓰지 않는다.
  - 단계 해금·침입 평가액·오버클럭·환급까지 연결됐다고 쓰지 않는다.
- 감사 원본 근거: [OperatingDaySettlementApplicationAdapter.cs 449행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Operation/OperatingDaySettlementApplicationAdapter.cs:449>): 유지비/수리비구분 / [OperatingDaySettlementApplicationAdapter.cs 466행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Operation/OperatingDaySettlementApplicationAdapter.cs:466>): 유지비합산 / [OperatingDaySettlementApplicationAdapter.cs 471행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Operation/OperatingDaySettlementApplicationAdapter.cs:471>): 손상시설수리비합산
- 감사 위키 근거: [economy-and-trade.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md:9>): 거래·경제일반설명 / [invasions-and-defence.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/invasions-and-defence.md:40>): 시설내구도와경로설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 334시설 분포·실제 차감 권위·전체 경제 필드 소비처 미검증.
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리

## GAP-219 손님의 시설 선택에 쓰이는 선호 점수 누락

- 분류: 시설 semantic tag의 대소문자 무시 매칭·AI 시설 선택·기분 충동 대상·손님 선호/평판 기억 설명 누락
- 보완할 문서: guests-services-and-performance / residents-and-work / facility entries
- 현재 확인한 차이: 손님 시설 선택의 선호 점수는 종족 태그 선호·인물 모델 선호·개인 성향 선호의 평균을0~1로 제한한 값이다. 이 선호 점수는 최종 시설 후보 점수에 가중치0.14로 반영되며 욕구·재고·가격·혼잡 등 다른 요소와 함께 선택에 영향을 준다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 손님 시설 선택의 선호 점수는 종족 태그 선호·인물 모델 선호·개인 성향 선호의 평균을0~1로 제한한 값이다.
  - 이 선호 점수는 최종 시설 후보 점수에 가중치0.14로 반영되며 욕구·재고·가격·혼잡 등 다른 요소와 함께 선택에 영향을 준다.
- **위키에 작성하지 않을 정보:**
  - 선호가 곧 시설 선택 확률14%라고 쓰지 않는다.
  - 모든 의미 태그의 AI 명시 목표·사회 기억 경로까지 승인하지 않는다.
- 감사 원본 근거: [FacilityCandidateScorer.cs 1581행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/FacilityCandidateScorer.cs:1581>): 종족/모델/persona평균 / [FacilityCandidateScorer.cs 1191행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/FacilityCandidateScorer.cs:1191>): preference×0.14로실제후보점수계산
- 감사 위키 근거: [guests-services-and-performance.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:31>): 문화적합성일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 태그 전수 집계·대소문자·명시 목표·기억 실행 경로 미검증.
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리

## GAP-220 방 온도 지원 점수와 침상 직접 냉기 보호의 구별 누락

- 분류: 시설별 온도 작성값·방 환경 fallback 합산·물리 환경 우선·진화/UI·침상 노출 보호 범위 설명 누락
- 보완할 문서: spaces / weather-seasons-and-environment / facility entries 1000,1001,1402,1021,1022
- 현재 확인한 차이: 방 온도 지원 점수는 가동 가능한 구형 온도 능력에서 온도 편차 절댓값+냉기 보호+열 보호를 합산한다. 별도 열 방출 능력이 있는 시설은 이 중복 합산에서 제외한다. 직접 침상 보호는 휴식 업무로 휴식 역할 시설에 배정되어 냉기 보호가 양수일 때만 적용한다. 쾌적 최저온도를 보호값만큼,안전 최저온도를 절반만큼 낮추고 냉기 노출 배율0.6을 더한다. 공개 가이드 인용에는 이 구체 규칙이 없다. 과거 원장의 침상 참조 GAP-072는 급수 항목을 가리키는 오기이며 GAP-097로 정정한다.
- **위키에 작성할 정보:**
  - 방 온도 지원 점수는 가동 가능한 구형 온도 능력에서 온도 편차 절댓값+냉기 보호+열 보호를 합산한다. 별도 열 방출 능력이 있는 시설은 이 중복 합산에서 제외한다.
  - 직접 침상 보호는 휴식 업무로 휴식 역할 시설에 배정되고 냉기 보호가 양수일 때만 적용한다. 쾌적 최저 온도를 보호값만큼, 안전 최저 온도를 그 절반만큼 낮추고 냉기 노출에 0.6배를 적용한다.
- **위키에 작성하지 않을 정보:**
  - 방 지원 점수와 실제 칸 온도·인물 보호를 같은 값으로 쓰지 않는다.
  - 침상의 개별 수치는 GAP-097 소유 범위에서 작성하며 중복하지 않는다.
- 감사 원본 근거: [CharacterEnvironmentRuntime.cs 615행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:615>): Rest업무/역할gate / [CharacterEnvironmentRuntime.cs 637행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterEnvironmentRuntime.cs:637>): coldProtection 실제노출보정 / [RoomEnvironmentAdapter.cs 445행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:445>): 가동연료필요·열방출능력중복제외 후 구형온도지원합산
- 감사 위키 근거: [spaces.md 17행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:17>): 온도조건일반준비 / [weather-seasons-and-environment.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/weather-seasons-and-environment.md:41>): 칸별온도설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 5시설 전수 수치·환경장 우선순위·모든 진화 조건 소비·실행 재현 미검증.
- 관련 GAP(제외 이력 포함): GAP-084, GAP-152, GAP-097
- 이번 재검토: 현행 실행 조건과 구체 효과로 작성 범위를 정정하고 미검증·내부 정보를 분리 원장 자체의 침상 owner 참조 오기도 함께 명시했다.

## GAP-221 원정 전투의 종족·성장 능력 편성 누락

- 분류: 원정 전투 능력의 편성 원본·종족 fallback·대상/진형/cooldown gate·효과/장비 소비 설명 누락
- 보완할 문서: combat-and-equipment / expeditions / character growth reference
- 현재 확인한 차이: 원정 전투 능력 목록은 유효한 종족 능력1개,장착한 성장 액티브 스킬,원정 분야 궁극기를 합친다. 같은 능력 ID는 중복 제거한다. 종족에 작성된 능력이 우선이고,지원되는 종족 기본 능력을 보충한 뒤 첫 능력1개를 선택한다. 현재 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 원정 전투 능력 목록은 유효한 종족 능력1개,장착한 성장 액티브 스킬,원정 분야 궁극기를 합친다. 같은 능력 ID는 중복 제거한다.
  - 종족에 작성된 능력이 우선이고,지원되는 종족 기본 능력을 보충한 뒤 첫 능력1개를 선택한다.
- **위키에 작성하지 않을 정보:**
  - 종족별 수치·대상/진형·전력 소비5·UI 전체를 이 편성 확인으로 승인하지 않는다.
- 감사 원본 근거: [OffenseCombatAbilities.cs 51행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/OffenseCombatAbilities.cs:51>): Active/Offense궁극기 편성 / [OffenseCombatAbilities.cs 83행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/OffenseCombatAbilities.cs:83>): authored우선+종족fallback 후첫1개 / [OffenseEncounterCatalog.cs 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/OffenseEncounterCatalog.cs:49>): 전투 참가자 구성에GetAbilities 실제전달
- 감사 위키 근거: [combat-and-equipment.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:9>): 기지/원정공통공격계산만설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 세 종족 효과 수치와 모든 명령·UI 실행은 미검증.
- 이번 재검토: 실제 게임 규칙과 내부 구현·미검증 범위 분리

## GAP-222 궁극기의 원정·침입·운영일 발동과 사용 제한 누락

- 분류: 성장 궁극기의 도메인별 발동 시점·성공 후 사용 마킹·serial 기반 제한·알림 설명 누락
- 보완할 문서: residents-and-work / combat-and-equipment / invasions-and-defence
- 현재 확인한 차이: 원정 궁극기는 같은 전투에서 한 번 사용하며 명령이 수락된 뒤 사용을 기록한다. 방어 궁극기는 침입마다,운영 궁극기는 운영일마다 살아 있는 인물들을 대상으로 자동 발동을 시도한다. 해당 분야·회차의 미사용 조건을 검사하고 먼저 사용을 기록한 뒤 효과를 적용한다. 현재 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 원정 궁극기는 같은 전투에서 한 번 사용하며 명령이 수락된 뒤 사용을 기록한다.
  - 방어 궁극기는 침입마다,운영 궁극기는 운영일마다 살아 있는 인물들을 대상으로 자동 발동을 시도한다. 해당 분야·회차의 미사용 조건을 검사하고 먼저 사용을 기록한 뒤 효과를 적용한다.
- **위키에 작성하지 않을 정보:**
  - 모든 경로가 효과 실패 시 사용 횟수를 보존한다고 쓰지 않는다.
  - 자동 대상이 정식 주민에만 한정된다고 쓰지 않는다.
  - 내부 회차 ID·해시·저장 필드명을 가이드에 싣지 않는다.
- 감사 원본 근거: [CharacterProgression.cs 326행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterProgression.cs:326>): domain+useLimits gate / [CharacterProgression.cs 389행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterProgression.cs:389>): 성공시MarkUsed / [CharacterSkillRuntimeEffects.cs 685행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs:685>): 침입·운영일 실제 이벤트 구독 / [CharacterSkillRuntimeEffects.cs 700행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs:700>): characterWorld의 모든 생존 actor;710Defense,730Management 자동 호출 / [CharacterSkillRuntimeEffects.cs 740행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs:740>): TryMarkUltimateUsed 성공 뒤748/752 효과 적용 / [OffenseBattleRuntime.cs 866행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/OffenseBattleRuntime.cs:866>): 명령수락후891원정궁극기사용기록
- 감사 위키 근거: [residents-and-work.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:13>): 일반스킬작업효과 / [combat-and-equipment.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:9>): 일반공격순서
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 효과 예외 rollback·실제 발동/왕복 재현 미검증.
- 이번 재검토: 실제 게임 규칙과 내부 구현·미검증 범위 분리

## GAP-223 성장 스킬의 업무 속도·청소·수리·연구·서비스 보정 누락

- 분류: 패시브·고정 스킬의 trigger 범위·임시 작업 속도·완료 보정·해제 및 상한 설명 누락
- 보완할 문서: residents-and-work / production / economy-and-trade / research reference
- 현재 확인한 차이: 업무 시작 때 시작·완료 발동형의 작업 속도 보너스를 함께 계산해 해당 업무 동안 적용하고 업무 종료 때 해제한다. 완료 발동형 청소·수리 보너스는 각각 기본1배에 보너스/100을 더해 최대3배로 제한한다. 생산 산출은1~4배,추가 생산 수량은 음수 없이 반올림한 보너스다. 연구는 진행 시간에 비례한 추가 연구량을 받고 서비스 수익도 해당 성장 스킬 보정을 읽는다. 패시브·고유 스킬은 발동 종류에 맞게 합산하며 사용 기록이 있는 운영 궁극기의 모듈도 합산한다. 현재 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 업무 시작 때 시작·완료 발동형의 작업 속도 보너스를 함께 계산해 해당 업무 동안 적용하고 업무 종료 때 해제한다.
  - 완료 발동형 청소·수리 보너스는 각각 기본1배에 보너스/100을 더해 최대3배로 제한한다. 생산 산출은1~4배,추가 생산 수량은 음수 없이 반올림한 보너스다.
  - 연구는 진행 시간에 비례한 추가 연구량을 받고 서비스 수익도 해당 성장 스킬 보정을 읽는다. 패시브·고유 스킬은 발동 종류에 맞게 합산하며 사용 기록이 있는 운영 궁극기의 모듈도 합산한다.
- **위키에 작성하지 않을 정보:**
  - 모든 취소·예외·중첩 해제의 안전을 플레이 기능으로 과장하지 않는다.
  - 운영 궁극기의 보정이 그날 종료와 동시에 사라진다는 주장은 이 소비자 조건에 없다.
- 감사 원본 근거: [CharacterSkillRuntimeEffects.cs 107행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs:107>): BeginWork의두trigger속도합 / [CharacterSkillRuntimeEffects.cs 253행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs:253>): output1..4/stock등관리modifier 계산 / [WorkTaskExecutor.cs 696행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:696>): 실제 BeginWork;850,955,2891 등 종료 해제 / [CleanWorkExecutionHandler.cs 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/Work/CleanWorkExecutionHandler.cs:19>): 청소 속도 보정 실제 소비 / [RepairWorkExecutionHandler.cs 167행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Combat/Work/RepairWorkExecutionHandler.cs:167>): 수리 속도 보정 실제 소비 / [BlueprintResearchContracts.cs 375행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchContracts.cs:375>): 승인 연구량에 성장 보너스 가산 / [CharacterBuildingVisitorPort.cs 118행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterBuildingVisitorPort.cs:118>): 수익·생산·stock 보정의 실제 방문자 입력 / [CharacterSkillRuntimeEffects.cs 315행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs:315>): Passive+OwnerFixed;323~329 관리궁극기 합산 조건
- 감사 위키 근거: [residents-and-work.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:13>): 스킬작업속도/품질/사고일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든 예외/취소의 수명·실제 업무 재현 미검증.
- 관련 GAP(제외 이력 포함): GAP-186
- 이번 재검토: 실제 게임 규칙과 내부 구현·미검증 범위 분리

## GAP-224 주민 성장 스킬의 레벨·서사 후보 조건 누락

- 분류: 성장 레벨의 active/passive/ultimate 후보 생성·조건부 해금·자동 선택·재개 설명 누락
- 보완할 문서: residents-and-work / character growth reference
- 현재 확인한 차이: 최대 성장 레벨은50이며 액티브 후보는 레벨1·5·30에 열린다. 첫 패시브는 레벨1,궁극기는 최대 레벨에서 비어 있을 때 생성된다. 두 번째 패시브는 레벨25 이상,패시브 빈 슬롯,의미 있는 서사 기록8개 이상·서사 분야3개 이상을 함께 요구한다. 현재 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 최대 성장 레벨은50이며 액티브 후보는 레벨1·5·30에 열린다. 첫 패시브는 레벨1,궁극기는 최대 레벨에서 비어 있을 때 생성된다.
  - 두 번째 패시브는 레벨25 이상,패시브 빈 슬롯,의미 있는 서사 기록8개 이상·서사 분야3개 이상을 함께 요구한다.
- **위키에 작성하지 않을 정보:**
  - 숙련 XP 등급과 성장 레벨을 같은 해금 축으로 쓰지 않는다.
  - 자동 선택 우선순위와 내부 화면 동기화는 검증 없이 작성하지 않는다.
- 감사 원본 근거: [CharacterSkillSystemSettings.asset 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/CharacterSkillSystemSettings.asset:15>): max50 및25/8/3,active1/5/30 / [CharacterProgression.cs 612행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterProgression.cs:612>): 설정해금레벨별draft / [CharacterProgression.cs 631행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterProgression.cs:631>): 2ndpassive gate와궁극기
- 감사 위키 근거: [residents-and-work.md 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:37>): 숙련등급XP서술이며별개성장draft없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 자동 선택·준비/장착 화면 동기화·실제 해금 재현 미검증.
- 이번 재검토: 실제 게임 규칙과 내부 구현·미검증 범위 분리

## GAP-225 성장 스킬 후보 수·희귀도·효과 조합 제한 누락

- 분류: active/passive/ultimate 후보 수·희귀도·module 조합 한계·비동기 생성 실패 처리 설명 누락
- 보완할 문서: residents-and-work / character growth reference
- 현재 확인한 차이: 액티브는 후보3개,패시브·궁극기는 후보1개를 만든다. 패시브는 해금 레벨25 미만이면 고급,25 이상이면 희귀이며 궁극기는 전설이다. 효과 조합 예산은 일반/고급/희귀/영웅/전설 순서로3/5/8/12/18이다. 허용된 효과 변형만 예산 안에서 조합하며 액티브 후보 모두가 희귀 미만이면 다음 상위 희귀도 보정이 예약된다. 현재 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 액티브는 후보3개,패시브·궁극기는 후보1개를 만든다. 패시브는 해금 레벨25 미만이면 고급,25 이상이면 희귀이며 궁극기는 전설이다.
  - 효과 조합 예산은 일반/고급/희귀/영웅/전설 순서로3/5/8/12/18이다. 허용된 효과 변형만 예산 안에서 조합하며 액티브 후보 모두가 희귀 미만이면 다음 상위 희귀도 보정이 예약된다.
- **위키에 작성하지 않을 정보:**
  - 서버 응답 검증·네트워크 timeout·대체 생성의 내부 운영 명세는 제외한다.
- 감사 원본 근거: [CharacterSkillGenerationService.cs 383행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs:383>): Active3나머지1 / [CharacterSkillGenerationService.cs 182행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs:182>): variant budget필터 / [CharacterSkillGenerationService.cs 1034행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs:1034>): PreparedRuleFallback 생성후commit / [CharacterSkillGenerationService.cs 372행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs:372>): 요청키 기반 결정론적 난수;383 후보수;393 패시브/궁극기 희귀도 / [CharacterSkillGenerationService.cs 410행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs:410>): Active draft 전체Rare 미만이면 upper rarity pity / [CharacterSkillSystemSettings.asset 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Character/CharacterSkillSystemSettings.asset:100>): 희귀도 budget3/5/8/12/18 / [CharacterSkillGenerationService.cs 579행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs:579>): 응답 후보수가 draft와 다르면 거부
- 감사 위키 근거: [residents-and-work.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:13>): 일반스킬효과만기재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 희귀도 전체 확률표·실제 후보 생성/실패 복구 재현은 미검증.
- 이번 재검토: 실제 게임 규칙과 내부 구현·미검증 범위 분리

## GAP-226 장착 장비 중량의 이동·작업 감속 공식 누락

- 분류: 장착 중량 burden의 물리 이동성·운반 수용량·감속 시작/하한 및 업무 합성 설명 누락
- 보완할 문서: combat-and-equipment / residents-and-work
- 현재 확인한 차이: 장착 중량이 기능 수용량의50%를 넘으면 이동·작업 배율은 1-0.35×(장착중량/수용량-0.5)로 감소하고0.45~1로 제한된다. 수용량은 max(8kg,25kg×신체 이동 기능)×운반 수용량 배율이며 최종값은 양수 하한0.0001kg을 갖는다. 현재 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 장착 중량이 기능 수용량의50%를 넘으면 이동·작업 배율은 1-0.35×(장착중량/수용량-0.5)로 감소하고0.45~1로 제한된다.
  - 수용량은 max(8kg,25kg×신체 이동 기능)×운반 수용량 배율이며 최종값은 양수 하한0.0001kg을 갖는다.
- **위키에 작성하지 않을 정보:**
  - 장착 장비 중량과 운반 중인 모든 물품 중량을 같은 입력으로 쓰지 않는다.
  - 다른 모든 속도·환경 보정까지 이 항목에 중복 나열하지 않는다.
- 감사 원본 근거: [CharacterStatsProjectionService.cs 55행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs:55>): capacity·overload·clamp실제식 / [CharacterStatsProjectionService.cs 187행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs:187>): 이동burden소비 / [CharacterStatsProjectionService.cs 221행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs:221>): 업무burden소비 / [ItemPrimitives.cs 95행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Items/Core/ItemPrimitives.cs:95>): 기준수용량25kg
- 감사 위키 근거: [combat-and-equipment.md 150행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/combat-and-equipment.md:150>): 무게가착용부담에반영된다고만기재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 다른 환경/보정 전체와 실제 이동·업무 재현은 미검증.
- 관련 GAP(제외 이력 포함): GAP-084
- 이번 재검토: 실제 게임 규칙과 내부 구현·미검증 범위 분리

## GAP-227 신체 기능 임계치·병목과 숙련 합성 성능 누락

- 분류: 신체 노드 기능 효율→functional capacity→업무/전투 성능의 적용성·가중/병목·숙련/효과 합성 설명 누락
- 보완할 문서: residents-and-work / health-and-community / combat-and-equipment / work references
- 현재 확인한 차이: 적용 가능한 필수 신체 기능이 요구 임계치에 못 미치면 작업 성능을 사용할 수 없다. 종족·해부상 미적용인 기능은 필수 검사 전에 제외되므로 모든 기능 부재가 실패는 아니다. 기능 가중평균을 병목 기능별0.25+0.75×기능값의 최솟값으로 제한한다. 역방향 채널은1/max(0.05,기능값)을 사용한다. 최종 성능은 기본값×기능 계수×숙련 계수×효과 계수×맥락 계수다. 따라서 숙련만 높여도 신체 기능 제한을 무시하지 못한다. 현재 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 적용 가능한 필수 신체 기능이 요구 임계치에 못 미치면 작업 성능을 사용할 수 없다. 종족·해부상 미적용인 기능은 필수 검사 전에 제외되므로 모든 기능 부재가 실패는 아니다.
  - 기능 가중평균을 병목 기능별0.25+0.75×기능값의 최솟값으로 제한한다. 역방향 채널은1/max(0.05,기능값)을 사용한다.
  - 최종 성능은 기본값×기능 계수×숙련 계수×효과 계수×맥락 계수다. 따라서 숙련만 높여도 신체 기능 제한을 무시하지 못한다.
- **위키에 작성하지 않을 정보:**
  - 전력 순환의 세부 신체 산출·모든 선택자·각 해부 노드의 효과를 일괄 승인하지 않는다.
- 감사 원본 근거: [CharacterPerformanceQuery.cs 307행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs:307>): 미적용skip·필수임계검사·가중/병목합성 / [CharacterPerformanceQuery.cs 351행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs:351>): 역방향계수 변환 / [CharacterPerformanceQuery.cs 402행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs:402>): 기본×기능×숙련×효과×맥락 / [WorkExecutionRegistry.cs 945행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkExecutionRegistry.cs:945>): 작업Speed 평가·미적용거부·실제workSpeed소비
- 감사 위키 근거: [health-and-community.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/health-and-community.md:15>): 신체구조문서로안내 / [residents-and-work.md 43행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:43>): XP기준성능보간만서술
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든 노드 생산자·개별 공식 실행 재현 미검증.
- 관련 GAP(제외 이력 포함): GAP-228
- 이번 재검토: 실제 게임 규칙과 내부 구현·미검증 범위 분리

## GAP-228 업무별 속도·사고 성능 공식과 제작 입력 구별 누락

- 분류: 작성 performance formula의 업무/결과 channel 매핑·exact lookup·실행 실패 경계 설명 누락
- 보완할 문서: residents-and-work / work references / health-and-community
- 현재 확인한 차이: 업무의 속도와 사고 위험은 서로 다른 성능 공식을 선택하며 속도 평가값은 실제 초당 작업량에,위험 평가값은 사고 판정에 사용한다. 제작 속도의 기능 기여는 정신 유지 20%·시각 식별 20%·정밀 조작 40%·동력 순환 10%·생명 반응 10%다. 정신 유지는 필수, 정밀 조작은 필수·병목이고 두 기능의 필수 임계치는 0.1이다. 일반 제작은 제작 숙련을 사용한다. 룬 제작에만 제작 80%와 학술 20%를 적용한다. 에셋의 보조 selector를 모든 제작의 학술20% 효과로 읽으면 잘못이다. 현재 공개 가이드에는 업무별 이 구체 입력·구분이 없다.
- **위키에 작성할 정보:**
  - 업무의 속도와 사고 위험은 서로 다른 성능 공식을 선택하며 속도 평가값은 실제 초당 작업량에,위험 평가값은 사고 판정에 사용한다.
  - 제작 속도의 기능 기여는 정신 유지 20%·시각 식별 20%·정밀 조작 40%·동력 순환 10%·생명 반응 10%다. 정신 유지는 필수, 정밀 조작은 필수·병목이고 두 기능의 필수 임계치는 0.1이다.
  - 일반 제작은 제작 숙련을 사용한다. 룬 제작에만 제작 80%와 학술 20%를 적용한다.
- **위키에 작성하지 않을 정보:**
  - 모든 공식107개·업무 매핑60개가 개별 실행 검증됐다고 쓰지 않는다.
  - 공통 합성식은 GAP-227과 중복 작성하지 않는다.
- 감사 원본 근거: [CharacterPerformanceQuery.cs 107행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs:107>): work/channel미매핑예외 / [WorkTaskExecutor.cs 3710행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:3710>): AccidentRisk실제조회 / [performance_work_craft_speed.asset 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_craft_speed.asset:15>): Craft Speed 공식; capacityInputs 및 Craft/rune 보조숙련; 45 work:craft 매핑 / [WorkExecutionRegistry.cs 945행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkExecutionRegistry.cs:945>): Speed 평가 결과를945 workSpeed로 사용 / [WorkExecutionRegistry.cs 957행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkExecutionRegistry.cs:957>): WorkAmountCalculator.CalculateWorkPerSecond 실제 trace / [CharacterPerformanceContracts.cs 5행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Foundation/CharacterPerformanceContracts.cs:5>): 기능ID0/1/9/4/7의실제의미 / [CharacterPerformanceContracts.cs 58행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Foundation/CharacterPerformanceContracts.cs:58>): role5=기여+필수,role7=기여+병목+필수 / [CharacterWorkPerformanceContextResolver.cs 129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/CharacterWorkPerformanceContextResolver.cs:129>): 룬 대상만 제작/학술80:20;147~150 보조 없으면 주 숙련으로 context override / [CharacterPerformanceQuery.cs 479행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs:479>): selector일 때만 context override 후 주/보조 가중;497~511 해석 / [CharacterProficiencyDomain.cs 629행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Characters/CharacterProficiencyDomain.cs:629>): 일반 Craft 기본 프로필은 제작 단독
- 감사 위키 근거: [residents-and-work.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:15>): 업무별숙련대응표만존재 / [work-references.json 38행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/work-references.json:38>): 제작 일반 준비/확인 및 숙련 링크만 있고 기능·성능 공식 매핑 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 현행107/60/30 모집단 재집계는 하지 않았으며 제작 예시와 공통 실행 연결만 재확인.
- 관련 GAP(제외 이력 포함): GAP-227
- 이번 재검토: 실제 게임 규칙과 내부 구현·미검증 범위 분리

## GAP-229 승인 작업량·피로·시설 손상에 따른 사고와 신체 피해 누락

- 분류: 업무 사고 hazard 계산·성능 사고 배율·노드 선택·실패 활동 설명 누락
- 보완할 문서: residents-and-work / health-and-community / work references
- 현재 확인한 차이: 승인 작업량이 양수이면 사고 확률은 1-exp(-0.001×승인 작업량×사고 성능×피로 배율×시설 손상 배율)이다. 같은 작업 행동에서 사고가 난 뒤에는 다시 판정하지 않는다. 피로 배율은1+수면 회복 목표 대비 부족 비율이고,시설 배율은1+(1-시설 내구도 비율)이다. 해당 상태가 없으면 그 배율은1이다. 사고가 나면 남아 있고 체력이 양수인 부위 중 하나를 균등 선택해 피해2·추가 출혈0을 적용하고 작업을 중단한다. 현재 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 승인 작업량이 양수이면 사고 확률은 1-exp(-0.001×승인 작업량×사고 성능×피로 배율×시설 손상 배율)이다. 같은 작업 행동에서 사고가 난 뒤에는 다시 판정하지 않는다.
  - 피로 배율은1+수면 회복 목표 대비 부족 비율이고,시설 배율은1+(1-시설 내구도 비율)이다. 해당 상태가 없으면 그 배율은1이다.
  - 사고가 나면 남아 있고 체력이 양수인 부위 중 하나를 균등 선택해 피해2·추가 출혈0을 적용하고 작업을 중단한다.
- **위키에 작성하지 않을 정보:**
  - 옛 식처럼 피로·시설 손상 배율을 생략하지 않는다.
  - 부위 ID 정렬·사유 문자열·실행 예외·저장 조인 내부 정보를 가이드에 싣지 않는다.
  - 업무별 매핑과 공통 성능식은 GAP-228/227에서 다룬다.
- 감사 원본 근거: [WorkTaskExecutor.cs 130행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:130>): hazard0.001·피해2상수 / [WorkTaskExecutor.cs 3169행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:3169>): 승인work뒤사고판정호출 / [WorkTaskExecutor.cs 3676행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:3676>): 3735성능×피로×시설배율,3738hazard,3789부위선택·피해·중단 / [CharacterWorkPerformanceContextResolver.cs 166행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/CharacterWorkPerformanceContextResolver.cs:166>): 수면부족비율피로1~2배·시설내구도1~2배
- 감사 위키 근거: [residents-and-work.md 68행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:68>): 사고·강제중단XP0.10만설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 사고·부상 사건·공정 화재 후속 경로·저장 왕복은 미검증.
- 관련 GAP(제외 이력 포함): GAP-227, GAP-228
- 이번 재검토: 현행 사고식에 새 피로·시설 손상 배율이 있으므로 옛 식을 정정

## GAP-230 숙련 학습의 적성·종족 보정과 자율 업무 선호 누락

- 분류: 완료 업무의 승인량 기반 XP 입력·학습 배율·종족 업무 적성·autonomous utility 설명 누락
- 보완할 문서: residents-and-work / species entries / work references
- 현재 확인한 차이: 주·보조 적성을 작업 비중으로 합친 뒤 0.70+0.006×가중 적성을 계산한다. 획득 경험 배율과 공통 배율(최소0.1)을 곱한 결과를0.70~1.75로 제한한 뒤 종족 강점1.10·약점0.90을 곱한다. 종족 강점 업무는 자율 후보 점수+10,약점 업무는-10을 받는다. 현재 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 주·보조 적성을 작업 비중으로 합친 뒤 0.70+0.006×가중 적성을 계산한다. 획득 경험 배율과 공통 배율(최소0.1)을 곱한 결과를0.70~1.75로 제한한 뒤 종족 강점1.10·약점0.90을 곱한다.
  - 종족 강점 업무는 자율 후보 점수+10,약점 업무는-10을 받는다.
- **위키에 작성하지 않을 정보:**
  - 이미 공개된 승인 작업량·난이도·반복·주보조 스킬 분배식을 신규 누락으로 중복 작성하지 않는다.
  - +10/-10을 작업 선택 확률 증감으로 쓰지 않는다.
- 감사 원본 근거: [CharacterProficiencyLearningRules.cs 32행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/CharacterProficiencyLearningRules.cs:32>): 주부적성가중과학습clamp / [CharacterProficiencyLearningRules.cs 19행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/CharacterProficiencyLearningRules.cs:19>): 그뒤종족학습배율 / [CharacterSpeciesWorkAptitudeRules.cs 6행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/CharacterSpeciesWorkAptitudeRules.cs:6>): 강약학습·utility±10 / [WorkTargetSelector.cs 1029행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTargetSelector.cs:1029>): 실제자율업무후보종족선호가산
- 감사 위키 근거: [residents-and-work.md 45행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:45>): 승인WU×학습×반복공식이미기재 / [residents-and-work.md 57행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:57>): 주부스킬작업비중이미기재 / [residents-and-work.md 70행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:70>): 학습최종범위만설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 지위 경험·해체0.2·모든 학습 상위 소비 경로·실행 재현은 미검증.
- 이번 재검토: 실제 게임 규칙과 내부 구현·미검증 범위 분리

## GAP-231 은퇴 주민의 안전 업무·멘토링 예외·하루4시간 제한 누락

- 분류: 은퇴 상태의 work availability gate·안전 업무 allowlist·일일 누적/저장 설명 누락
- 보완할 문서: residents-and-work / character entries / work references
- 현재 확인한 차이: 은퇴 주민의 기본 허용 업무는 청소·연구·접객·제작·조리·공연·파종·수확·동물 돌봄9종이며 하루 안전 작업은 게임시간4시간까지다. 운영 업무라도 해당 멘토 학원에 실제 멘토 또는 학생으로 배정된 은퇴자는 예외로 허용된다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 은퇴 주민의 기본 허용 업무는 청소·연구·접객·제작·조리·공연·파종·수확·동물 돌봄9종이며 하루 안전 작업은 게임시간4시간까지다.
  - 운영 업무라도 해당 멘토 학원에 실제 멘토 또는 학생으로 배정된 은퇴자는 예외로 허용된다.
- **위키에 작성하지 않을 정보:**
  - 9업무 밖은 모두 금지라고 쓰지 않는다.
  - 게임시간4시간을 실제 벽시계4시간으로 쓰지 않는다.
- 감사 원본 근거: [WorkExecutionRegistry.cs 368행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkExecutionRegistry.cs:368>): availability에은퇴gate적용 / [WorkExecutionRegistry.cs 443행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkExecutionRegistry.cs:443>): 9개기본안전업무 / [WorkExecutionRegistry.cs 456행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkExecutionRegistry.cs:456>): 멘토학원Operate예외 / [CareerHouseholdDomain.cs 286행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Species/Core/CareerHouseholdDomain.cs:286>): 하루4시간 / [CareerApplicationAdapter.cs 69행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/CareerApplicationAdapter.cs:69>): 은퇴실제work시간기록
- 감사 위키 근거: [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 일반작업허용조건 / [family-education-and-legacy.md 28행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/family-education-and-legacy.md:28>): 노화역할변경일반서술
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체 저장·실제 은퇴 작업 재현 미검증.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-232 직원 불만 단계·업무 차단·속도·상담 효과 누락

- 분류: 정식 주민의 기분·저기분 일수 기반 불만 단계, 속도/근무 gate, 이탈·반란 대응 및 저장 설명 누락
- 보완할 문서: residents-and-work / health-and-community / work references
- 현재 확인한 차이: 기본 규칙은 기분50 이하의 연속 일수를 센다. 기분8 이하가 반란,15 이하 또는 기분25 이하가4일 지속되면 이탈 단계다. 그보다 심한 단계가 아니면 기분25 이하 또는 낮은 기분3일은 업무 차질,기분35 이하 또는 낮은 기분2일은 효율 저하,기분50 이하는 낮은 만족 단계다. 낮은 만족/효율 저하/업무 차질의 속도 배율은0.95/0.8/0.6이다. 업무 차질·이탈·반란 단계는 새 업무 시작을 막는다. 상담 가능한 불만 직원에게 기분+25×협상 배율을240초 적용하고 낮은 기분 연속 일수를0으로 되돌린 뒤 단계를 다시 판정한다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 기본 규칙은 기분50 이하의 연속 일수를 센다. 기분8 이하가 반란,15 이하 또는 기분25 이하가4일 지속되면 이탈 단계다.
  - 그보다 심한 단계가 아니면 기분25 이하 또는 낮은 기분3일은 업무 차질,기분35 이하 또는 낮은 기분2일은 효율 저하,기분50 이하는 낮은 만족 단계다.
  - 낮은 만족/효율 저하/업무 차질의 속도 배율은0.95/0.8/0.6이다. 업무 차질·이탈·반란 단계는 새 업무 시작을 막는다.
  - 상담 가능한 불만 직원에게 기분+25×협상 배율을240초 적용하고 낮은 기분 연속 일수를0으로 되돌린 뒤 단계를 다시 판정한다.
- **위키에 작성하지 않을 정보:**
  - 안정 상태·이미 영구 이탈 또는 반란 중인 직원도 같은 상담을 받을 수 있다고 쓰지 않는다.
  - 반란 제압·영구 이탈·저장 전체를 이 검증으로 승인하지 않는다.
- 감사 원본 근거: [StaffDiscontentSystem.cs 158행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/StaffDiscontentSystem.cs:158>): 일일기분연속일수·단계갱신 / [StaffDiscontentSystem.cs 162행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/StaffDiscontentSystem.cs:162>): 반란격리와owner위협 / [StaffDiscontentSystem.cs 495행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/StaffDiscontentSystem.cs:495>): 기본rules로단계판정 / [DungeonStaffDiscontentSaveData.cs 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Work/DungeonStaffDiscontentSaveData.cs:41>): 기본단계/연속일수/상담25/속도.95 .8 .6 / [WorkDutyController.cs 260행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkDutyController.cs:260>): 실제 업무 시작 시불만 차단 / [CharacterStatsProjectionService.cs 204행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs:204>): actual staff efficiency multiplier를work stats에 반영 / [StaffDiscontentSystem.cs 262행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/StaffDiscontentSystem.cs:262>): 상담MoodFactor 적용 및240초
- 감사 위키 근거: [residents-and-work.md 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:133>): 결핍→붕괴/폭력/이탈일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 반란 제압 대상·영구 생명주기·실제 상담·저장 왕복은 미검증.
- 관련 GAP(제외 이력 포함): GAP-223
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-233 생산 주문 작업자 정책의 실제 선택과 표시 오류 누락

- 분류: 제작·장비·의복 주문의 worker policy mode, narrative qualification, 후보 우선순위와 실패 설명 누락
- 보완할 문서: production-quality-and-supply / combat-and-equipment / apparel crafting / residents-and-work
- 현재 확인한 차이: 생산 주문의 작업자 버튼은 속도 우선→품질 우선→제작 숙련400XP 이상·품질 우선→속도 우선으로 순환한다. 세 번째 상태의 현재 화면 문구는 '민첩 7+'지만 실제 제한은 민첩이 아니라 제작 숙련 400 XP 이상이다. 공통 작업자 정책은 명시적 제외를 먼저 적용한 뒤 누구나·특정 인물·규칙·특정 인물 또는 규칙을 평가한다. 현재 공개 가이드에는 이 구체 조건·차이가 설명되어 있지 않다.
- **위키에 작성할 정보:**
  - 생산 주문의 작업자 버튼은 속도 우선→품질 우선→제작 숙련400XP 이상·품질 우선→속도 우선으로 순환한다.
  - 세 번째 상태의 현재 화면 문구는 '민첩 7+'지만 실제 제한은 민첩이 아니라 제작 숙련 400 XP 이상이다.
  - 공통 작업자 정책은 명시적 제외를 먼저 적용한 뒤 누구나·특정 인물·규칙·특정 인물 또는 규칙을 평가한다.
- **위키에 작성하지 않을 정보:**
  - 공통 정책의 모든 인물·경력·특성 필드를 현재 UI에서 자유롭게 편집할 수 있다고 쓰지 않는다.
  - 민첩7 이상이 실제 조건이라고 쓰지 않는다.
- 감사 원본 근거: [V23WorkerPolicyContracts.cs 193행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/V23WorkerPolicyContracts.cs:193>): 명시적제외먼저검사 / [V23WorkerPolicyContracts.cs 203행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/V23WorkerPolicyContracts.cs:203>): Anyone/Specific/RuleSet/OR 분기 / [V23WorkerPolicyContracts.cs 224행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/V23WorkerPolicyContracts.cs:224>): skill XP/career 조건검사 / [ProductionBuildingPanelPresenter.cs 289행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs:289>): 작업자버튼에서SetWorkerPolicy(NextWorkerPolicy)실제호출 / [ProductionBuildingPanelPresenter.cs 548행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ProductionBuildingPanelPresenter.cs:548>): 민첩7+표시지만560순환정책은제작400XP / [ProductionBillRuntime.cs 1065행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs:1065>): 실제작업시workerPolicy로적격성검사
- 감사 위키 근거: [production-quality-and-supply.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md:39>): 불가능품질의대기만기재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 다른 장비·의복·건설 화면 전체와 후보 정렬·재료 미소비·실제 입력은 미검증.
- 관련 GAP(제외 이력 포함): GAP-190
- 이번 재검토: 실제 UI 정책과 '민첩7+' 표시가 불일치; 내부 정책 전체를 공개 편집가능 기능으로 쓰지 않음

## GAP-235 우선 작업의 위험 재확인·근무 복귀 효과 누락

- 분류: direct priority-work command 상태 전이·위험 확인·실패 설명 누락
- 보완할 문서: residents-and-work / work references
- 현재 확인한 차이: 강제 우선 작업에서 환경 위험 경고가 나오면 같은 시설·같은 업무를 다시 명령해야 확인으로 인정한다. 대상 평가 실패 때는 배정하지 않는다. 수락된 우선 작업은 즉시 근무 상태로 복귀시키고 대상을 배정한 뒤 AI 재계획을 요청한다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 강제 우선 작업에서 환경 위험 경고가 나오면 같은 시설·같은 업무를 다시 명령해야 확인으로 인정한다. 대상 평가 실패 때는 배정하지 않는다.
  - 수락된 우선 작업은 즉시 근무 상태로 복귀시키고 대상을 배정한 뒤 AI 재계획을 요청한다.
- **위키에 작성하지 않을 정보:**
  - 메모리의 우선 대상 필드를 저장·불러오기 뒤 보존되는 영속 계약으로 설명하지 않는다.
- 감사 원본 근거: [WorkCommandHandler.cs 85행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkCommandHandler.cs:85>): 동일target/type 두번째확인 / [WorkCommandHandler.cs 105행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkCommandHandler.cs:105>): 실패시배정없이반환 / [WorkCommandHandler.cs 111행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkCommandHandler.cs:111>): 메모리target 및OnDuty/assign/replan
- 감사 위키 근거: [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 긴급직접명령존재만기재
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 모든 위험 등급 생산자·진압 경로·저장·실제 명령 재현 미검증.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-238 직접 업무 명령의 일시 불가 보존·긴급 근무 복귀 누락

- 분류: AI autonomous work target selection·utility score·priority fallback 설명 누락
- 보완할 문서: residents-and-work / work references
- 현재 확인한 차이: 자동 업무 선택은 직접 지정한 목표를 우선 평가하고,재사용 대기·일시 시작 불가·목적지 점유·경로 부재·평가 지연 때 명령을 유지한다. 비번 중에도 실제로 이용 가능한 긴급 업무의 긴급도60 이상을 찾으면 근무로 복귀한다. 후보 탐색이 아직 지연 중인 상태만으로 긴급 업무를 찾았다고 처리하지 않는다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 자동 업무 선택은 직접 지정한 목표를 우선 평가하고,재사용 대기·일시 시작 불가·목적지 점유·경로 부재·평가 지연 때 명령을 유지한다.
  - 비번 중에도 실제로 이용 가능한 긴급 업무의 긴급도60 이상을 찾으면 근무로 복귀한다. 후보 탐색이 아직 지연 중인 상태만으로 긴급 업무를 찾았다고 처리하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 일시 불가가 아니라 대상이 영구 무효여도 명령이 무조건 남는다고 쓰지 않는다.
  - 전체 점수·캐시·버전·저장 내부 명세는 제외한다.
- 감사 원본 근거: [WorkTargetSelector.cs 160행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTargetSelector.cs:160>): 직접지정목표우선 / [WorkTargetSelector.cs 180행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTargetSelector.cs:180>): 일시불가명령보존 / [WorkTargetSelector.cs 245행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTargetSelector.cs:245>): 긴급urgency60 / [WorkTargetSelector.cs 1211행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTargetSelector.cs:1211>): 직접명령보존실패유형명시;152긴급발견후근무전환
- 감사 위키 근거: [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 우선순위등일반조건 / [residents-and-work.md 107행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:107>): 긴급구조선행일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 전체 후보 점수와 모든 명령 실패·실행 재현 미검증.
- 관련 GAP(제외 이력 포함): GAP-235, GAP-217
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-239 시설 작업 발판의 좌우 접근·방문객 출입 구별 누락

- 분류: facility work access position·actor traversal·execution path 설명 누락
- 보완할 문서: residents-and-work / getting-started / construction references
- 현재 확인한 차이: 일반 시설의 작업 발판은 점유칸의 좌우 인접칸에서 찾으며 이동용 시설은 점유칸 자체를 사용할 수 있다. 작업자는 걷기 가능하고 도달 가능하며 시설 작업 접근이 허용된 발판이 필요하다. 방문객 서비스 출입 허용과는 별도로 작업 접근을 판단한다. 공개 가이드 인용에는 이 구체 규칙이 없다.
- **위키에 작성할 정보:**
  - 일반 시설의 작업 발판은 점유칸의 좌우 인접칸에서 찾으며 이동용 시설은 점유칸 자체를 사용할 수 있다.
  - 작업자는 걷기 가능하고 도달 가능하며 시설 작업 접근이 허용된 발판이 필요하다. 방문객 서비스 출입 허용과는 별도로 작업 접근을 판단한다.
- **위키에 작성하지 않을 정보:**
  - 내부 좌표 순서·캐시·저장 여부는 플레이 안내에서 제외한다.
- 감사 원본 근거: [WorkTargetSelectionRules.cs 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTargetSelectionRules.cs:98>): GridMovement점유칸또는좌우칸 / [WorkTargetSelectionRules.cs 121행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTargetSelectionRules.cs:121>): 발판도달가능/access/costgate
- 감사 위키 근거: [spaces.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:9>): 방동선일반설명 / [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 구역/업무배정일반설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 선택·이동·시작의 모든 호출자와 실제 발판 이동은 미검증.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-240 손님과 직원의 상점 구매 이용 차이 누락

- 분류: character population role authority·visitor/staff AI action·service access 설명 누락
- 보완할 문서: guests-services-and-performance / residents-and-work / recruitment references
- 현재 확인한 차이: 손님은 직원으로 취급하지 않는다. 직원은 구매 역할의 소매 시설을 쇼핑 방문 후보로 선택할 수 없다. 훈련·연구·마나·화장실·위생 시설 이용 전체가 금지되는 것은 아니다. 현재 공개 본문에는 이 구체 조건이 없다.
- **위키에 작성할 정보:**
  - 손님은 직원으로 취급하지 않는다.
  - 직원은 구매 역할의 소매 시설을 쇼핑 방문 후보로 선택할 수 없다. 훈련·연구·마나·화장실·위생 시설 이용 전체가 금지되는 것은 아니다.
- **위키에 작성하지 않을 정보:**
  - 모든 서비스가 직원에게 금지된다고 쓰지 않는다.
  - 영입 프로필·캐시 변환 내부 메커니즘은 신규 위키 규칙에서 제외한다.
- 감사 원본 근거: [CharacterWorkRoleUtility.cs 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/CharacterWorkRoleUtility.cs:18>): Customer이면workerfalse / [CharacterVisitPolicy.cs 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Ability/CharacterVisitPolicy.cs:27>): staff Purchase상점판정 / [CharacterVisitPolicy.cs 68행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Ability/CharacterVisitPolicy.cs:68>): staff상점거부 / [AbilityShopping.cs 805행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Ability/AbilityShopping.cs:805>): 실제 방문 후보에서 CharacterVisitPolicy.CanVisitBuilding을 먼저 적용
- 감사 위키 근거: [guests-services-and-performance.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:41>): 영입뒤일반주민수요만서술
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 영입 전환 전체·모든 서비스 카탈로그·실제 방문 재현은 미검증. 직접 소매 API까지 모든 직원 구매가 불가능하다는 판정은 아니다.
- 이번 재검토: 구체 게임 규칙과 제외·검증 한계 분리

## GAP-241 업무 재배정의 단일 작업자 선택과 비강제 중단 보호 누락

- 분류: workforce wake/replan arbitration·running action interruption·priority haul dispatch 설명 누락
- 보완할 문서: residents-and-work / inventory-and-carrying / production references
- 현재 확인한 차이: 단일 작업자 재배정에서는 가능한 후보 중 쉬고 있는 인물을 먼저 고려하고, 같은 상태에서는 업무 적합 점수로 한 명을 선택한다. 선택한 작업자는 요청 업무를 600초 동안 선호하고 즉시 다시 판단한다. 강제 중단을 허용하지 않은 요청은 이미 행동 중인 작업자를 보호한다. 해당 구체 조건은 현재 인용 공개 가이드에 없다.
- **위키에 작성할 정보:**
  - 단일 작업자 재배정에서는 가능한 후보 중 쉬고 있는 인물을 먼저 고려하고, 같은 상태에서는 업무 적합 점수로 한 명을 선택한다.
  - 선택한 작업자는 요청 업무를 600초 동안 선호하고 즉시 다시 판단한다. 강제 중단을 허용하지 않은 요청은 이미 행동 중인 작업자를 보호한다.
- **위키에 작성하지 않을 정보:**
  - 운반 인원 전파 수와 모든 호출자의 강제 중단 권한은 공개 규칙으로 확정하지 않는다.
- 감사 원본 근거: [WorkforceReplanService.cs 169행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkforceReplanService.cs:169>): idle 우선 및 점수 비교 후 한 명 선택; 185~194 선호와 재계획 / [WorkforceReplanService.cs 461행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkforceReplanService.cs:461>): 실제 보호 조건은 !forceInterrupt && work != null && brain.HasRunningAction
- 감사 위키 근거: [residents-and-work.md 103행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:103>): 허용 업무·우선순위·구역·교대·직접 명령을 설명하나 단일 선택 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 운반 중재 전체와 실제 재배정 실행은 미검증. 600초 선호는 현재 호출 인수이며 벽시계 단위로 단정하지 않는다.
- 이번 재검토: 모호한 작성 지시를 현재 게임 규칙으로 구체화하고 감사 메모·제외·미검증을 분리.

## GAP-243 업무별 주·보조 숙련 선택 규칙 누락

- 분류: work performance proficiency resolver·authored override·combat/compound selection 설명 누락
- 보완할 문서: residents-and-work / combat-and-equipment / production references
- 현재 확인한 차이: 시설 운영과 건설·수리·배관·해체·대공사는 시설에 작성된 해당 숙련 조합을 우선 사용한다. 경비는 현재 원거리 무기를 쓰면 원거리 전투 숙련을, 아니면 근접 전투 숙련을 사용한다. 사냥은 식량 생산 80%와 같은 무기 기준 전투 숙련 20%를 사용한다. 포로 감시는 사교 80%와 근접·원거리 중 현재 경험치가 높은 전투 숙련 20%를 사용하며, 동률이면 근접을 고른다. 룬 제작은 제작 80%와 학술 20%를 사용한다. 휴식에는 학습 숙련 배율을 적용하지 않는다. 해당 구체 조건은 현재 인용 공개 가이드에 없다.
- **위키에 작성할 정보:**
  - 시설 운영과 건설·수리·배관·해체·대공사는 시설에 작성된 해당 숙련 조합을 우선 사용한다.
  - 경비는 현재 원거리 무기를 쓰면 원거리 전투 숙련을, 아니면 근접 전투 숙련을 사용한다. 사냥은 식량 생산 80%와 같은 무기 기준 전투 숙련 20%를 사용한다.
  - 포로 감시는 사교 80%와 근접·원거리 중 현재 경험치가 높은 전투 숙련 20%를 사용하며, 동률이면 근접을 고른다. 룬 제작은 제작 80%와 학술 20%를 사용한다.
  - 휴식에는 학습 숙련 배율을 적용하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 숙련 누락 오류 메시지와 유효성 검사 구현은 플레이 규칙에서 제외한다.
- 감사 원본 근거: [CharacterWorkPerformanceContextResolver.cs 76행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/CharacterWorkPerformanceContextResolver.cs:76>): 시설 authored profile 우선, 90 운영 누락 거부, 96 경비, 101 휴식, 115~135 사냥·감시·룬 제작 80:20 / [CharacterWorkPerformanceContextResolver.cs 200행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/CharacterWorkPerformanceContextResolver.cs:200>): 활성 무기 원거리 여부; 212~233 현재 경험치가 높은 전투 숙련, 동률 근접 / [CharacterWorkPerformanceContextResolver.cs 255행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/CharacterWorkPerformanceContextResolver.cs:255>): 운영·건설 계열별 시설 작성 프로필 선택
- 감사 위키 근거: [residents-and-work.md 45행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:45>): 숙련 성장 설명; 업무별 80:20 조합은 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 현재 숙련 선택 분기만 정적으로 확인했으며 모든 시설 작성값과 성능 채널 소비·실행은 전수 재현하지 않았다.
- 관련 GAP(제외 이력 포함): GAP-227, GAP-228
- 이번 재검토: 모호한 작성 지시를 현재 게임 규칙으로 구체화하고 감사 메모·제외·미검증을 분리.

## GAP-244 생존·당직·여가 AI 기본 우선 점수 누락

- 분류: routine AI group priority·need threshold·utility multiplier 설명 누락
- 보완할 문서: residents-and-work / guests-services-and-performance / needs references
- 현재 확인한 차이: 생존의 가장 강한 필요도 n(0~1)에 대한 기본 점수는 n≤0.05면 0, 0.05<n<0.25면 25n, 0.25≤n<0.65면 35+30n, n≥0.65면 95+5n이다. 직원이 아니거나 휴무 중이면 당직 기본 점수는 0이다. 근무 중에는 생존 압력이 0.25 이하일 때 82이며, 압력이 1로 커질수록 선형으로 8까지 낮아진다. 여가 후보는 직원이면 휴무 중이거나 재미 필요도가 양수일 때, 비직원이면 쇼핑 능력이 있을 때 열릴 수 있다. 기본 점수는 70×clamp01(max(재미 필요도, 기분 필요도×0.75, 쇼핑 필요도)×clamp01(1−긴급 생존 필요도×0.85))이다. 이 수치는 기분과 상황 보정 전 기본 우선 점수이며 행동 선택 확률이 아니다. 해당 구체 조건은 현재 인용 공개 가이드에 없다.
- **위키에 작성할 정보:**
  - 생존의 가장 강한 필요도 n(0~1)에 대한 기본 점수는 n≤0.05면 0, 0.05<n<0.25면 25n, 0.25≤n<0.65면 35+30n, n≥0.65면 95+5n이다.
  - 직원이 아니거나 휴무 중이면 당직 기본 점수는 0이다. 근무 중에는 생존 압력이 0.25 이하일 때 82이며, 압력이 1로 커질수록 선형으로 8까지 낮아진다.
  - 여가 후보는 직원이면 휴무 중이거나 재미 필요도가 양수일 때, 비직원이면 쇼핑 능력이 있을 때 열릴 수 있다. 기본 점수는 70×clamp01(max(재미 필요도, 기분 필요도×0.75, 쇼핑 필요도)×clamp01(1−긴급 생존 필요도×0.85))이다.
  - 이 수치는 기분과 상황 보정 전 기본 우선 점수이며 행동 선택 확률이 아니다.
- **위키에 작성하지 않을 정보:**
  - 최종 상황 보정 전체와 실제 행동 선택률을 이 기본식으로 대체하지 않는다.
- 감사 원본 근거: [CharacterAiJobGiver.cs 531행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterAiJobGiver.cs:531>): 생존 0/.25/.65 분기; 546 기본 strongestNeed*25 / [CharacterAiJobGiver.cs 554행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterAiJobGiver.cs:554>): worker와 on-duty 검사 뒤 568 Lerp(8,82) / [CharacterAiJobGiver.cs 590행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterAiJobGiver.cs:590>): 여가 max와 생존 억제 .85 계산 / [CharacterAiJobGiver.cs 476행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterAiJobGiver.cs:476>): 기본 그룹 점수→기분 보정→상황 점수로 소비 / [CharacterAiJobGiver.cs 621행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterAiJobGiver.cs:621>): 직원은 휴무 또는 FunUrgency>0, 비직원은 쇼핑 능력
- 감사 위키 근거: [residents-and-work.md 107행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:107>): 긴급 구조 우선만 설명; 그룹 효용 공식 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 최종 상황 배율과 실전 선택 빈도는 미검증.
- 관련 GAP(제외 이력 포함): GAP-035, GAP-238
- 이번 재검토: 모호한 작성 지시를 현재 게임 규칙으로 구체화하고 감사 메모·제외·미검증을 분리.

## GAP-245 기분에 따른 업무·여가 기본 우선도 보정 누락

- 분류: mood impulse runtime·AI routine/job/final utility bias·interruption 설명 누락
- 보완할 문서: residents-and-work / needs-mood / guests-services-and-performance
- 현재 확인한 차이: 기분 72 이상에서 양수인 기본 우선도는 근무 중 업무에 +2~10, 휴무 중 여가에 +1~6을 기분이 100에 가까워질수록 더한다. 기분 38 미만이면 기분이 0에 가까워질수록 업무 루틴 점수에 1→0.55배를 적용하고, 여가 점수 하한은 18→44, 대기 점수 하한은 22→52로 올린다. 같은 저기분 구간에서 개별 업무 후보 점수는 1→0.2배로 줄고, 기다리기·둘러보기 후보 점수 하한은 0.48→0.9로 높아진다. 이는 점수 보정이며 반드시 그 행동을 실행한다는 뜻은 아니다. 해당 구체 조건은 현재 인용 공개 가이드에 없다.
- **위키에 작성할 정보:**
  - 기분 72 이상에서 양수인 기본 우선도는 근무 중 업무에 +2~10, 휴무 중 여가에 +1~6을 기분이 100에 가까워질수록 더한다.
  - 기분 38 미만이면 기분이 0에 가까워질수록 업무 루틴 점수에 1→0.55배를 적용하고, 여가 점수 하한은 18→44, 대기 점수 하한은 22→52로 올린다.
  - 같은 저기분 구간에서 개별 업무 후보 점수는 1→0.2배로 줄고, 기다리기·둘러보기 후보 점수 하한은 0.48→0.9로 높아진다. 이는 점수 보정이며 반드시 그 행동을 실행한다는 뜻은 아니다.
- **위키에 작성하지 않을 정보:**
  - 명시 발행된 기분 충동에만 의존하는 강제 행동·목표 승격을 자연 발생 규칙으로 쓰지 않는다.
  - 행동 중단·자율 대기의 모든 조건을 위 수치만으로 설명하지 않는다.
- 감사 원본 근거: [CharacterMoodImpulseUtility.cs 6행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterMoodImpulseUtility.cs:6>): Good/Low/Critical mood 경계 / [CharacterMoodImpulseUtility.cs 79행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterMoodImpulseUtility.cs:79>): 낮은 mood 업무 감쇠; 193 job score 감쇠 / [CharacterVisitorControlJourneyPlayModeVerifier.cs 602행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/Editor/CharacterVisitorControlJourneyPlayModeVerifier.cs:602>): Assets에서 TryPublishMoodImpulse의 유일한 외부 호출자 / [CharacterAiJobGiver.cs 150행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/CharacterAiJobGiver.cs:150>): 개별 후보 기분 보정 소비, 485 루틴 기분 보정 소비
- 감사 위키 근거: [residents-and-work.md 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:133>): 결핍·기분 저하의 효율/붕괴 결과를 일반 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 실제 JobGiver의 루틴·개별 후보 소비를 확인했다. TryPublishMoodImpulse 외부 호출자는 현재 Assets 검색에서 Editor 검증기뿐이다. 전체 continuation·최종 자율성 적용과 실행 재현은 미검증.
- 관련 GAP(제외 이력 포함): GAP-246, GAP-247
- 이번 재검토: 모호한 작성 지시를 현재 게임 규칙으로 구체화하고 감사 메모·제외·미검증을 분리.

## GAP-249 시설 개체 진화의 동시 주문 배제와 최소 촉매 단계 누락

- 분류: facility evolution instance order·catalyst progression·physical input authority·recalibration 설명 누락
- 보완할 문서: facility-growth
- 현재 확인한 차이: 시설 성장 가이드는 개체 진화·촉매·승인 시 입력 고정·재조율을 이미 설명한다. 남은 차이는 개조·재조율·이전의 동시 배제와 후보별 최소 촉매 단계 조건이다.
- **위키에 작성할 정보:**
  - 개조·재조율·이전 주문 중 하나가 이미 진행 중인 시설에는 새 개체 진화 주문을 승인할 수 없다.
  - 후보가 최소 촉매 단계를 요구하면 촉매 정의를 해석할 수 있어야 하고, 선택한 촉매의 진행 단계가 그 최솟값 이상이어야 한다.
- **위키에 작성하지 않을 정보:**
  - 이미 설명된 승인 시 입력 고정과 재조율을 별도 신규 누락으로 중복 작성하지 않는다.
  - 암흑 수지 수량과 복원 내부 절차는 이번 공개 범위에서 제외한다.
- 감사 원본 근거: [FacilityInstanceEvolutionRuntime.cs 194행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityEvolution/FacilityInstanceEvolutionRuntime.cs:194>): 3종 주문 동시 배제 / [FacilityInstanceEvolutionRuntime.cs 204행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityEvolution/FacilityInstanceEvolutionRuntime.cs:204>): 고위험 후보의 촉매 검증; 214 최소 progression / [FacilityInstanceEvolutionRuntime.cs 223행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityEvolution/FacilityInstanceEvolutionRuntime.cs:223>): 선택 family/level/module 고정
- 감사 위키 근거: [facility-growth.md 17행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/facility-growth.md:17>): 인스턴스 진화 설명 / [facility-growth.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/facility-growth.md:41>): 승인 시 재료·촉매·WU 고정, 43 재조율 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 촉매 수량·물리 입력 보관·복원 전체와 실제 승인 실행은 미검증.
- 관련 GAP(제외 이력 포함): GAP-024, GAP-025
- 이번 재검토: 모호한 작성 지시를 현재 게임 규칙으로 구체화하고 감사 메모·제외·미검증을 분리.

## GAP-250 야생동물의 일일 질병 노출 거리와 시간 누락

- 분류: wildlife disease vector·daily exposure·route priority 설명 누락
- 보완할 문서: food-and-ecology / disease-and-public-health
- 현재 확인한 차이: 질병 벡터가 있는 살아 있는 야생동물은 하루 한 번, 자신으로부터 맨해튼 거리 2칸 이내의 살아 있는 캐릭터에 노출을 더한다. 대상은 주민으로만 제한되지 않는다. 같은 칸이면 질병별 노출 1시간, 거리 1~2칸이면 0.5시간을 환경 계수 1로 더한다. 질병이 허용한 경로 중 접촉→혈액→공기→비말→환경→음식→물→마나 노출 순서에서 첫 경로 하나를 선택한다. 노출 누적은 즉시 감염 확정을 뜻하지 않는다. 해당 구체 조건은 현재 인용 공개 가이드에 없다.
- **위키에 작성할 정보:**
  - 질병 벡터가 있는 살아 있는 야생동물은 하루 한 번, 자신으로부터 맨해튼 거리 2칸 이내의 살아 있는 캐릭터에 노출을 더한다. 대상은 주민으로만 제한되지 않는다.
  - 같은 칸이면 질병별 노출 1시간, 거리 1~2칸이면 0.5시간을 환경 계수 1로 더한다.
  - 질병이 허용한 경로 중 접촉→혈액→공기→비말→환경→음식→물→마나 노출 순서에서 첫 경로 하나를 선택한다. 노출 누적은 즉시 감염 확정을 뜻하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 노출 대상이 주민뿐이라고 쓰지 않는다.
  - 이 노출 함수만으로 최종 감염 확률이나 확진을 보장하지 않는다.
- 감사 원본 근거: [WildlifeRuntime.cs 250행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Wildlife/WildlifeRuntime.cs:250>): PublishDailyExposure 실제 호출 / [WildlifeDiseaseVectorRuntime.cs 32행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Wildlife/WildlifeDiseaseVectorRuntime.cs:32>): !IsDead만 거르며 resident/type 필터 없음 / [WildlifeDiseaseVectorRuntime.cs 45행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Wildlife/WildlifeDiseaseVectorRuntime.cs:45>): Manhattan 2 이내; 61 같은칸1h/인접.5h;75 경로 우선순위
- 감사 위키 근거: [disease-and-public-health.md 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/disease-and-public-health.md:37>): 일일24h 노출 집계 설명; 야생동물 반경 없음 / [food-and-ecology.md 196행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:196>): 야생동물 생태 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 질병 집계 이후의 확률 판정·실제 노출 실행은 이번 항목에서 재검증하지 않았다.
- 관련 GAP(제외 이력 포함): GAP-022
- 이번 재검토: 모호한 작성 지시를 현재 게임 규칙으로 구체화하고 감사 메모·제외·미검증을 분리.

## GAP-251 포획 동물 급여의 소모량과 오염 사료 결과 누락

- 분류: captured wildlife feed·physical batch disposition·disease outcome persistence 설명 누락
- 보완할 문서: food-and-ecology / production
- 현재 확인한 차이: 포획 동물에게 먹이를 한 번 급여할 때 실제 먹이 1개를 소비하고, 적용 영양만큼 허기(0~1)를 낮춘다. 급여의 질병 판정이 발동하면 건강이 1 감소하고 급여 질병 심각도가 25 올라 최대 100이 된다. 건강은 0 아래로 내려가지 않는다. 공개 본문은 관련 시스템을 일반 설명하지만 이 세부 규칙은 빠져 있다.
- **위키에 작성할 정보:**
  - 포획 동물에게 먹이를 한 번 급여할 때 실제 먹이 1개를 소비하고, 적용 영양만큼 허기(0~1)를 낮춘다.
  - 급여의 질병 판정이 발동하면 건강이 1 감소하고 급여 질병 심각도가 25 올라 최대 100이 된다. 건강은 0 아래로 내려가지 않는다.
- **위키에 작성하지 않을 정보:**
  - 급여 확정 기록의 내부 필드·재시도·복원 절차는 공개 규칙에서 제외한다.
  - 오염 사료가 매번 질병을 일으킨다고 쓰지 않는다.
- 감사 원본 근거: [CapturedWildlifeFeedOutbox.cs 93행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CapturedWildlifeFeedOutbox.cs:93>): 허기 target; 95 건강 -1; 98 sickness +25 / [CapturedWildlifeFeedOutbox.cs 146행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/CapturedWildlifeFeedOutbox.cs:146>): 대상에 결과 적용 후 173 acknowledge, 181 clear / [WildlifeCaptureRuntime.cs 1518행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Captivity/WildlifeCaptureRuntime.cs:1518>): 급여 시 실제 1개 Sink,1532 결과 기록,1541 결과 적용
- 감사 위키 근거: [food-and-ecology.md 200행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:200>): 사료·공간·건강 조건 설명; 정확한 급여 결과 없음 / [continuity.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:21>): 일회성 결과 보존은 공통으로 이미 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 급여 결과의 현재 실행 연결을 정적으로 확인했다. 모든 먹이 선택 조건·질병 확률·확정 재시도와 실제 급여 재현은 미검증.
- 이번 재검토: 현재 원본과 공개 본문을 대조하여 구체 규칙만 작성 대상으로 남기고 기존 설명·내부 구현·미확인 연결을 분리.

## GAP-252 그림자늑대 식량 습격의 표적과 동시 진행 제한 누락

- 분류: wildlife food raid·spawn·reachable loose stack·physical disposition 설명 누락
- 보완할 문서: food-and-ecology / production
- 현재 확인한 차이: 그림자늑대 습격은 수량이 남아 있고 보관·운반 상태가 아닌 바닥의 식량 중 외부 진입로에서 도달 가능한 묶음을 표적으로 삼는다. 표적은 진입로로부터의 맨해튼 거리순으로 우선하며 같은 거리에서는 묶음 식별자 순서가 적용된다. 기존 습격이 아직 종결되지 않았으면 새 식량 습격을 시작하지 않는다. 표적이 없더라도 출현 가능한 늑대는 노출 식량을 찾는 상태로 시작할 수 있다. 공개 본문은 관련 시스템을 일반 설명하지만 이 세부 규칙은 빠져 있다.
- **위키에 작성할 정보:**
  - 그림자늑대 습격은 수량이 남아 있고 보관·운반 상태가 아닌 바닥의 식량 중 외부 진입로에서 도달 가능한 묶음을 표적으로 삼는다.
  - 표적은 진입로로부터의 맨해튼 거리순으로 우선하며 같은 거리에서는 묶음 식별자 순서가 적용된다. 기존 습격이 아직 종결되지 않았으면 새 식량 습격을 시작하지 않는다.
  - 표적이 없더라도 출현 가능한 늑대는 노출 식량을 찾는 상태로 시작할 수 있다.
- **위키에 작성하지 않을 정보:**
  - 표적 교체·사망 시점·도난 확정 기록의 모든 예외는 공개 규칙으로 확정하지 않는다.
- 감사 원본 근거: [WildlifeBehaviorRuntime.cs 75행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Wildlife/WildlifeBehaviorRuntime.cs:75>): loose+reachable 필터와 entry 거리/ID 정렬 / [WildlifeBehaviorRuntime.cs 102행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Wildlife/WildlifeBehaviorRuntime.cs:102>): 미종결 주문 있으면 새 습격 차단; 124 실제 shadow_wolf spawn / [ExternalInfluenceRuntime.cs 530행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs:530>): 예약 습격에서 TryBeginFoodRaid 호출 / [WildlifeBehaviorRuntime.cs 797행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Wildlife/WildlifeBehaviorRuntime.cs:797>): Loose·Food·수량>0 조건 / [WildlifeBehaviorRuntime.cs 133행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Wildlife/WildlifeBehaviorRuntime.cs:133>): 표적 없어도 출현한 늑대에 빈 표적으로 주문 작성
- 감사 위키 근거: [food-and-ecology.md 185행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:185>): 늑대는 입구 포식자라는 설명만 존재 / [food-and-ecology.md 196행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:196>): 일반 생태 압력 요인만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 접근·표적·동시 진행 분기만 정적으로 확인했다. 실제 이동, 도난 후 사망과 모든 경로 실패는 미검증.
- 관련 GAP(제외 이력 포함): GAP-253
- 이번 재검토: 현재 원본과 공개 본문을 대조하여 구체 규칙만 작성 대상으로 남기고 기존 설명·내부 구현·미확인 연결을 분리.

## GAP-253 생태 압력의 경보·그림자늑대 습격 예약·완화 조건 누락

- 분류: ecology pressure·weather/exposed food·raid scheduling·persistence 설명 누락
- 보완할 문서: food-and-ecology / weather-seasons-and-environment
- 현재 확인한 차이: 운영일 시작에 한파 여부와 외부 진입로에서 접근 가능한 노출 식량 묶음 수를 생태 압력 계산에 사용한다. 생태 압력이 60 이상이면 경보를 한 번 내고, 80 이상이며 예정·진행 중인 습격이 없으면 식량 습격을 예약한다. 예약 시간이 끝나면 그림자늑대 2마리의 출현을 시도한다. 습격이 끝나거나 출현에 실패하면 생태 압력을 max(35, 기존 압력−45)로 바꾸고 경보 상태를 초기화한다. 출현에 실패한 습격은 식량을 소비하지 않는다. 공개 본문은 관련 시스템을 일반 설명하지만 이 세부 규칙은 빠져 있다.
- **위키에 작성할 정보:**
  - 운영일 시작에 한파 여부와 외부 진입로에서 접근 가능한 노출 식량 묶음 수를 생태 압력 계산에 사용한다.
  - 생태 압력이 60 이상이면 경보를 한 번 내고, 80 이상이며 예정·진행 중인 습격이 없으면 식량 습격을 예약한다. 예약 시간이 끝나면 그림자늑대 2마리의 출현을 시도한다.
  - 습격이 끝나거나 출현에 실패하면 생태 압력을 max(35, 기존 압력−45)로 바꾸고 경보 상태를 초기화한다. 출현에 실패한 습격은 식량을 소비하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 정확한 압력 증가량·저장 필드·모든 습격 종결 보고를 이번 규칙에 추가하지 않는다.
  - 출현 시도 2마리가 항상 실제 출현 2마리를 보장한다고 쓰지 않는다.
- 감사 원본 근거: [ExternalInfluenceRuntime.cs 476행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs:476>): 운영일 이벤트에서 한파와 reachable food count 전달 / [ExternalInfluenceDomainRuntime.cs 50행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ExternalInfluenceDomainRuntime.cs:50>): 60/80/35/45 임계치; 389~418 중복 방지 분기 / [ExternalInfluenceRuntime.cs 530행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs:530>): wolfCount 2와 생성 실패 무손실 분기 / [ExternalInfluenceDomainRuntime.cs 462행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/CoreSession/ExternalInfluenceDomainRuntime.cs:462>): 해결·실패 후 pressure 완화와 경보 초기화
- 감사 위키 근거: [food-and-ecology.md 196행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:196>): 생태 변화의 일반 영향만 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 도난·취소별 종결 결과와 저장 복원 전체는 미검증. 예약 대기 시간은 작성값을 재대조하지 않아 이번 공개 범위에서 확정하지 않았다.
- 관련 GAP(제외 이력 포함): GAP-252
- 이번 재검토: 현재 원본과 공개 본문을 대조하여 구체 규칙만 작성 대상으로 남기고 기존 설명·내부 구현·미확인 연결을 분리.

## GAP-254 길잡이 부적의 원정 정보 해금 소비량 누락

- 분류: expedition intel trail charm·physical disposition·save restore 설명 누락
- 보완할 문서: expeditions / production
- 현재 확인한 차이: 길잡이 부적의 위험·약점 해독 기능과 명칭은 현재 아이템 본문에 있다. 남은 차이는 미해금 정보에 실제 부적 1개가 소비되며 이미 해금된 정보에는 재소비하지 않는다는 조건이다.
- **위키에 작성할 정보:**
  - 원정 정보가 아직 해금되지 않은 원정지에서 길잡이 부적을 결제 수단으로 고르면 실제 부적 1개를 소비해 정보를 해금한다. 이미 해금된 정보에는 부적을 다시 소비하지 않는다.
- **위키에 작성하지 않을 정보:**
  - 아이템 명칭을 추적 부적으로 쓰지 않는다.
  - 아이템 본문에 이미 있는 위험·약점 해독 기능을 신규 누락으로 중복 작성하지 않는다.
  - 내부 확정 기록의 필드·재시도·복원 구현은 공개 규칙에서 제외한다.
- 감사 원본 근거: [ExternalInfluenceRuntime.cs 660행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs:660>): 실제 stack 1개 Sink commit 후 pending / [ExternalInfluenceTrailCharmOutbox.cs 98행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceTrailCharmOutbox.cs:98>): intel unlock 후 terminal 확인·ack·clear / [ExternalInfluenceRuntime.cs 316행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ExternalInfluenceRuntime.cs:316>): 이미 해금되면 결제 전 반환,332 TrailCharm 실제 소비 호출
- 감사 위키 근거: [resource-trail-charm.json 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/resource-trail-charm.json:18>): 숨겨진 원정지 위험·약점 해독 설명 / [resource-trail-charm.json 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/resource-trail-charm.json:25>): 현재 명칭 길잡이 부적 / [expeditions.md 44행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/expeditions.md:44>): 원정 소비·인계는 공통 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 소비 및 정보 확정 연결을 정적으로 확인했다. 모든 저장·재시도와 실제 UI 해금은 미검증.
- 이번 재검토: 현재 원본과 공개 본문을 대조하여 구체 규칙만 작성 대상으로 남기고 기존 설명·내부 구현·미확인 연결을 분리.

## GAP-255 세력 계약 개방 수치와 호의 물자 효율 누락

- 분류: faction contract threshold·goodwill physical transfer·betrayal penalty 설명 누락
- 보완할 문서: factions-contracts-and-prisoners / economy-and-trade
- 현재 확인한 차이: 협상 봉쇄 중에는 계약이 열리지 않는다. 호의/원한 조건은 교역 ≥20/≤70, 영입 ≥35/≤55, 보급 ≥50/≤40, 지원군 ≥70/≤25다. 지원군은 동맹 사업 완료와 의무 증표 보유도 필요하다. 호의 물자 제안에는 예약되지 않은 실물 가치 50 이상이 필요하다. 기본 호의 증가는 소비 가치÷10의 정수 몫을 1~10으로 제한한 값이다. 기본 증가량에 0.85의 배신 상처 수 제곱을 곱해 반올림한 뒤 최소 1을 적용한다. 증가 후 호의는 −100~100으로 제한된다. 공개 본문은 관련 시스템을 일반 설명하지만 이 세부 규칙은 빠져 있다.
- **위키에 작성할 정보:**
  - 협상 봉쇄 중에는 계약이 열리지 않는다. 호의/원한 조건은 교역 ≥20/≤70, 영입 ≥35/≤55, 보급 ≥50/≤40, 지원군 ≥70/≤25다. 지원군은 동맹 사업 완료와 의무 증표 보유도 필요하다.
  - 호의 물자 제안에는 예약되지 않은 실물 가치 50 이상이 필요하다. 기본 호의 증가는 소비 가치÷10의 정수 몫을 1~10으로 제한한 값이다.
  - 기본 증가량에 0.85의 배신 상처 수 제곱을 곱해 반올림한 뒤 최소 1을 적용한다. 증가 후 호의는 −100~100으로 제한된다.
- **위키에 작성하지 않을 정보:**
  - 이미 명시된 의무 증표 1개 소비와 재사용 시간을 신규 누락으로 다시 작성하지 않는다.
  - 실물 이전의 내부 확정·복원 필드는 공개 규칙에서 제외한다.
- 감사 원본 근거: [FactionRuntime.cs 192행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:192>): 협상 봉쇄 검사 후 201~210 계약별 수치 / [FactionRuntime.cs 295행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:295>): unreserved 물자 가치 50 이상 / [FactionRuntime.cs 314행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:314>): 실제 소비가치/10 clamp 뒤 반올림·최소1·.85^scar 감쇠 / [OffenseWorldMapPanelStrategicDetails.cs 484행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicDetails.cs:484>): 전략 지도 UI에서 TryOfferGoodwill 호출
- 감사 위키 근거: [factions-contracts-and-prisoners.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:13>): 관계별 해금 정성 설명 / [factions-contracts-and-prisoners.md 53행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:53>): 지원군 증표 1개 및10일 재사용 이미 명시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 물자 선정 및 이전 실패·재시도 전체, 실제 UI 거래는 미검증.
- 관련 GAP(제외 이력 포함): GAP-258
- 이번 재검토: 현재 원본과 공개 본문을 대조하여 구체 규칙만 작성 대상으로 남기고 기존 설명·내부 구현·미확인 연결을 분리.

## GAP-256 세력 화물 수량의 반올림 방식과 하역 대기 누락

- 분류: faction route travel·cargo delivery state·exact source publication 설명 누락
- 보완할 문서: factions-contracts-and-prisoners / economy-and-trade
- 현재 확인한 차이: 도착 화물의 품목별 수량은 원래 수량×경로 전력÷100을 가장 가까운 정수로 반올림하며, 정확히 반이면 짝수 쪽을 고른다. 결과는 최소 1개다. 예를 들어 3개×80%는 2개이지 3개가 아니다. 하역 지점을 확보하지 못하면 준비된 화물은 물리 재고로 인계되지 않고 대기한다. 공개 본문은 관련 시스템을 일반 설명하지만 이 세부 규칙은 빠져 있다.
- **위키에 작성할 정보:**
  - 도착 화물의 품목별 수량은 원래 수량×경로 전력÷100을 가장 가까운 정수로 반올림하며, 정확히 반이면 짝수 쪽을 고른다. 결과는 최소 1개다. 예를 들어 3개×80%는 2개이지 3개가 아니다.
  - 하역 지점을 확보하지 못하면 준비된 화물은 물리 재고로 인계되지 않고 대기한다.
- **위키에 작성하지 않을 정보:**
  - 나머지가 있으면 항상 올림한다고 쓰지 않는다.
  - 이미 설명된 이동 후 물리 재고 인계를 독립 누락으로 중복 작성하지 않는다.
  - 화물 발행 중 저장 차단과 확정 기록 필드는 공개 규칙에서 제외한다.
- 감사 원본 근거: [FactionRuntime.cs 1665행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:1665>): 나머지>50 또는 .5이며 whole 홀수일 때만+1; 최소1 / [FactionRuntime.cs 1530행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:1530>): 도착 처리에서 trade/supply/restitution을 실제 cargo delivery로 전달 / [FactionRuntime.cs 1569행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:1569>): Ready 및 하역점 없으면 false로 대기 / [FactionRuntime.cs 169행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:169>): AdvanceRoutes에 SecondsPerHex20 전달
- 감사 위키 근거: [factions-contracts-and-prisoners.md 45행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:45>): 두 화물이 이동·도착 후 물리 재고로 들어옴을 명시
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 이동 지연·발행 중 저장·확정 재시도 전부와 실제 하역은 미검증.
- 관련 GAP(제외 이력 포함): GAP-257
- 이번 재검토: 현재 원본과 공개 본문을 대조하여 구체 규칙만 작성 대상으로 남기고 기존 설명·내부 구현·미확인 연결을 분리.

## GAP-258 세력 배신의 관계 손실·협상 봉쇄·배상 및 회복 조건 누락

- 분류: faction betrayal·embargo·restitution·recovery 설명 누락
- 보완할 문서: factions-contracts-and-prisoners / economy-and-trade
- 현재 확인한 차이: 배신하면 대상 세력의 신뢰는 −100이 되고 배신 상처가 1 늘며 협상이 10일 동안 봉쇄된다. 다른 세력의 신뢰도 15씩 줄어 최저 −100이 된다. 관계 원한에는 대상 세력 +35, 다른 세력 +10을 반영한다. 필요한 배상 가치는 실제 약탈 가치의 1.5배를 올림한 값이다. 신뢰 회복에는 협상 봉쇄 기간 경과, 배상 완료, 회복 사건 완료가 함께 필요하다. 봉쇄 기간만 끝났다고 신뢰가 자동으로 회복되지는 않는다. 공개 본문은 관련 시스템을 일반 설명하지만 이 세부 규칙은 빠져 있다.
- **위키에 작성할 정보:**
  - 배신하면 대상 세력의 신뢰는 −100이 되고 배신 상처가 1 늘며 협상이 10일 동안 봉쇄된다. 다른 세력의 신뢰도 15씩 줄어 최저 −100이 된다.
  - 관계 원한에는 대상 세력 +35, 다른 세력 +10을 반영한다. 필요한 배상 가치는 실제 약탈 가치의 1.5배를 올림한 값이다.
  - 신뢰 회복에는 협상 봉쇄 기간 경과, 배상 완료, 회복 사건 완료가 함께 필요하다. 봉쇄 기간만 끝났다고 신뢰가 자동으로 회복되지는 않는다.
- **위키에 작성하지 않을 정보:**
  - 내부 배상 기록 초기화를 실제 세력 원한 초기화로 설명하지 않는다.
  - 모든 회복 후 최종 관계 수치를 이번 항목에서 확정하지 않는다.
- 감사 원본 근거: [FactionDomainRuntime.cs 337행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Factions/Core/FactionDomainRuntime.cs:337>): trust/scar/봉쇄;344 ceil1.5;363 peer-15 / [FactionRuntime.cs 477행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Factions/FactionRuntime.cs:477>): 실제 ApplyBetrayal 후 rapport delta와 grievance+35/+10 반영 / [FactionDomainRuntime.cs 523행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Factions/Core/FactionDomainRuntime.cs:523>): 봉쇄 만료+배상+회복 사건 조건 / [FactionDomainRuntime.cs 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Factions/Core/FactionDomainRuntime.cs:30>): BetrayalEmbargoDays=10,339 현재일+10 적용 / [OffenseWorldMapPanelStrategicDetails.cs 572행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Offense/Strategic/OffenseWorldMapPanelStrategicDetails.cs:572>): 전략 지도 UI에서 TryBetray 호출
- 감사 위키 근거: [factions-contracts-and-prisoners.md 13행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:13>): 일반 갈등·보복 설명 / [factions-contracts-and-prisoners.md 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md:23>): 후속 원한/보복 정성 설명
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 현재 배신 소비 경로·회복 조건을 정적으로 확인했다. 배상 확정 기록의 모든 재시도와 회복 후 관계 투영 값·실제 실행은 미검증.
- 관련 GAP(제외 이력 포함): GAP-255
- 이번 재검토: 현재 원본과 공개 본문을 대조하여 구체 규칙만 작성 대상으로 남기고 기존 설명·내부 구현·미확인 연결을 분리.

## GAP-259 자연 수원의 선택 기준·소비·재생 규칙 누락

- 분류: world water source·quality selection·regeneration·disease contamination 설명 누락
- 보완할 문서: water / food-and-ecology / disease-and-public-health
- 현재 확인한 차이: 위키 물 욕구와 주민 가이드에는 수원·오염·병원체 및 자연 수원 음수가 이미 있다. 남는 차이는 잔량>0.05, 수질 우선 후 거리, 안전 음수의 추가 접근 검사, 실제 잔량 소비와 재생 배율식이다. 병원체 제거 API는 외부 호출자가 없어 사용 가능한 정화 규칙으로 인정하지 않는다.
- **위키에 작성할 정보:**
  - 자연 수원 후보는 잔량이 0.05보다 많아야 하며, 오염수를 허용하지 않는 검색에서는 심하게 오염된 수원을 제외한다. 수질을 먼저 비교한 뒤 같은 수질이면 맨해튼 거리가 가까운 수원을 고른다.
  - 안전한 음수 계획은 이 검색 결과가 깨끗한 물인지와 실제로 접근 가능한지를 추가 확인한다. 가까운 수원이라는 이유만으로 무조건 마시지 않는다.
  - 물은 남은 양까지만 소비하며 잔량은 음수가 되지 않는다. 재생량은 수원별 초당 재생량×현재 재생 배율×게임 경과 시간이고 최대 저장량을 넘지 않는다.
- **위키에 작성하지 않을 정보:**
  - 병원체 제거 API의 존재만으로 플레이어가 수원을 정화할 수 있다고 쓰지 않는다.
  - 수원·오염·병원체 개념 자체가 위키에 없다고 쓰지 않는다.
- 감사 원본 근거: [WorldWaterRuntime.cs 176행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldWaterRuntime.cs:176>): 잔량·foul 필터 뒤 수질/거리 정렬 / [WorldWaterRuntime.cs 194행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldWaterRuntime.cs:194>): 207 남은 양까지 소비;117~138 현재 재생 배율×DeltaTime,capacity 상한 / [WorldWaterRuntime.cs 228행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldWaterRuntime.cs:228>): Water route 제한;214 pathogen 및 품질 하한 / [WorldWaterRuntime.cs 262행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldWaterRuntime.cs:262>): 병원체 ID만 제거; 외부 실게임 호출자 미확인이므로 공개 사용 기능에서 제외 / [CharacterSafeDrinkPlanner.cs 300행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/CharacterSafeDrinkPlanner.cs:300>): 실제 safe drink planner에서 source 검색 / [V20CampaignRuntime.cs 2363행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Run/V20CampaignRuntime.cs:2363>): 현재 엔드리스 기후와 미해결 계절 사건의 재생 배율 중 최솟값
- 감사 위키 근거: [residents-and-work.md 125행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/residents-and-work.md:125>): 수원·저장처·급수·오염·병원체 이미 설명 / [infrastructure.md 40행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:40>): 망의 수질별 양과 수동운반 설명 / [need-references.json 49행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/need-references.json:49>): 물 욕구 본문도 수원·오염·병원체와 자연 수원 음수를 설명하나 후보 잔량/수질 우선/거리 세부 없음
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: TryClearPathogen 외부 실게임 호출자는 Assets 전체 검색에서 확인되지 않았다. 따라서 병원체 제거와 수질 복구 분리를 사용 가능한 정화 규칙으로 승인하지 않는다. 현재 계절·엔드리스 재생 배율의 모든 작성값과 실제 음수·재생은 미검증.
- 관련 GAP(제외 이력 포함): GAP-066
- 이번 재검토: 현재 원본과 공개 본문을 대조하여 구체 규칙만 작성 대상으로 남기고 기존 설명·내부 구현·미확인 연결을 분리.

## GAP-260 바닥 오물의 누적·청소 작업량·지역 청결 감점 누락

- 분류: world filth·work target·infection weighted cleanliness 설명 누락
- 보완할 문서: health-and-community / residents-and-work
- 현재 확인한 차이: 같은 칸·오물 종류·벽 얼룩 여부·배출자의 오물은 합쳐진다. 추가량은 최소 0.1이며 기존 오물에 합칠 때 양을 최대 100으로 제한한다. 새 오물 생성량 전체에 100 상한이 있는 것은 아니다. 오물 1을 제거하는 데 청소 작업량 12 WU가 필요하다. 같은 칸에서는 감염 위험이 높은 오물부터, 위험이 같으면 양이 많은 오물부터 청소한다. 지정된 맨해튼 반경 안의 오물량×(0.5+감염 위험도)를 합산하고 0~100으로 제한한 값을 지역 청결 감점으로 쓴다. 주민은 반경 1의 감점이 15를 넘으면 오염·감염 부담이 누적된다. 방 환경은 각 칸의 청결 감점을 모아 평균×0.65+최댓값×0.35를 0~70으로 제한한 감점으로 사용한다. 공개 본문은 관련 시스템을 일반 설명하지만 이 세부 규칙은 빠져 있다.
- **위키에 작성할 정보:**
  - 같은 칸·오물 종류·벽 얼룩 여부·배출자의 오물은 합쳐진다. 추가량은 최소 0.1이며 기존 오물에 합칠 때 양을 최대 100으로 제한한다. 새 오물 생성량 전체에 100 상한이 있는 것은 아니다.
  - 오물 1을 제거하는 데 청소 작업량 12 WU가 필요하다. 같은 칸에서는 감염 위험이 높은 오물부터, 위험이 같으면 양이 많은 오물부터 청소한다.
  - 지정된 맨해튼 반경 안의 오물량×(0.5+감염 위험도)를 합산하고 0~100으로 제한한 값을 지역 청결 감점으로 쓴다. 주민은 반경 1의 감점이 15를 넘으면 오염·감염 부담이 누적된다.
  - 방 환경은 각 칸의 청결 감점을 모아 평균×0.65+최댓값×0.35를 0~70으로 제한한 감점으로 사용한다.
- **위키에 작성하지 않을 정보:**
  - 오물별 누적 상한을 새로 생성되는 모든 오물의 상한으로 일반화하지 않는다.
  - 청소 대상 오브젝트의 생성·제거·저장 내부 절차는 공개 규칙에서 제외한다.
- 감사 원본 근거: [WorldFilthRuntime.cs 331행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldFilthRuntime.cs:331>): 신규 최소.1;332 키 매칭;344 신규량 무상한;356 병합100cap / [WorldFilthRuntime.cs 379행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldFilthRuntime.cs:379>): work/12만큼 제거;402 infection desc/amount desc / [WorldFilthRuntime.cs 438행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldFilthRuntime.cs:438>): Manhattan반경 amount*lerp(.5,1.5,risk) clamp100 / [CharacterDeprivationRuntime.cs 1084행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/CharacterDeprivationRuntime.cs:1084>): 현재 주민위치 반경1 감점 소비 / [RoomEnvironmentAdapter.cs 489행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs:489>): 칸별 감점 수집;497 평균.65+최대.35 clamp70→509 방 환경 계산에 전달
- 감사 위키 근거: [disease-and-public-health.md 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/disease-and-public-health.md:23>): 청소/폐기 복구 일반 원칙 / [infrastructure.md 23행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:23>): 청결과 오염원이 환경에 영향
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 청소량·순서·생활 및 방 환경 소비를 정적으로 확인했다. 작업 대상 제거 전 분기와 실제 청소 실행은 미검증.
- 관련 GAP(제외 이력 포함): GAP-205
- 이번 재검토: 현재 원본과 공개 본문을 대조하여 구체 규칙만 작성 대상으로 남기고 기존 설명·내부 구현·미확인 연결을 분리.

## GAP-261 노화 치료 절차별 전용 주시설 조건의 위키 오류

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: medical-care-and-surgery / residents-and-work
- 현재 확인한 차이: 위키 의료 가이드 109행은 다섯 시설이 공통 후보라고 설명하지만 현재 절차 에셋은 각각 전용 주시설을 지정하고 후보 조회와 주문 생성이 이를 검사한다.
- **위키에 작성할 정보:**
  - 장기 재생은 장기 재생 수술실, 혈액 회춘은 회춘 수혈실, 룬 동면은 룬 동면실, 전신 재생은 전신 재생조, 시간 고정은 시간 고정실을 주시설로 사용한다.
  - 같은 노화 치료 자격을 가진 다른 시설로 주시설을 대체할 수 없다. 지정 주시설도 손상되지 않고 사용 가능한 방에 있어야 한다.
- **위키에 작성하지 않을 정보:**
  - 이미 공개된 회춘 나이 제한·1년 대기와 일반 수술 절차를 신규 누락으로 중복 작성하는 내용
  - 연령 치료 명령만 시설을 엄격히 제한하고 일반 수술은 다섯 시설을 서로 대체한다는 과거 설명
- 감사 원본 근거: [SurgicalFacilityQuery.cs 89행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgicalFacilityQuery.cs:89>): 절차별 주시설 ID 확인 후 시설 자격·방·손상 검사. 후보 조회도 같은 판정 사용. / [SurgeryRuntime.cs 1059행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Medical/SurgeryRuntime.cs:1059>): 직접 지정한 시설에도 절차 기반 판정을 적용. / [AgeTreatmentCommandRuntime.cs 115행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AgeTreatmentCommandRuntime.cs:115>): 치료 명령은 일반 수술 주문 생성에 위임. 직접 BuildingId 검사라는 이전 근거 삭제. / [procedure_organ-regeneration.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Procedures/procedure_organ-regeneration.asset:30>): 전용 주시설 8868 지정. / [procedure_blood-rejuvenation.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Procedures/procedure_blood-rejuvenation.asset:30>): 전용 주시설 8869 지정. / [procedure_rune-hibernation.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Procedures/procedure_rune-hibernation.asset:30>): 전용 주시설 8870 지정. / [procedure_whole-body-regeneration.asset 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Procedures/procedure_whole-body-regeneration.asset:30>): 전용 주시설 8871 지정. / [procedure_temporal-stasis.asset 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Medical/Procedures/procedure_temporal-stasis.asset:31>): 전용 주시설 8872 지정.
- 감사 위키 근거: [medical-care-and-surgery.md 109행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:109>): 5시설 공통 자격과 선택 수술을 이미 설명. / [medical-care-and-surgery.md 114행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/medical-care-and-surgery.md:114>): 성인+5년 하한·1년 재사용 대기를 이미 설명.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-185
- 이번 재검토: 명령·일반 수술의 불일치라는 오래된 주장을 제거하고 현재 공통 전용시설 제한과 위키 오류로 정정.

## GAP-262 단골·영입 기록의 저장 범위 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: guests-services-and-performance / continuity / recruitment references
- 현재 확인한 차이: 손님 가이드는 서비스 세션의 보존과 영입 조건을 설명하지만 단골·영입 이력 자체의 저장 범위는 구체적으로 설명하지 않는다.
- **위키에 작성할 정보:**
  - 이민 정책, 손님별 방문 횟수와 평균 만족도, 단골 여부, 영입 후보 여부, 영입 완료 여부와 성공 영입일은 저장 후 복원된다.
- **위키에 작성하지 않을 정보:**
  - 고유 ID 정규화·배열 순서·저장 단계·기능 비트 등 내부 저장 형식
  - 일반적인 원자적 복원 절차를 이 항목의 신규 차이로 중복 작성하는 내용
- 감사 원본 근거: [RegularCustomerSaveSection.cs 30행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Recruitment/RegularCustomerSaveSection.cs:30>): 정책·방문/만족·단골·후보·영입일 capture 및 70행부터 restore. / [RegularCustomerSaveSection.cs 24행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Core/Save/RegularCustomerSaveSection.cs:24>): 단골 저장 구역의 capture 및 후보 검증 후 publish.
- 감사 위키 근거: [guests-services-and-performance.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:21>): 이용 세션 계속성만 설명. / [guests-services-and-performance.md 41행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:41>): 후보·영입 조건은 있으나 해당 기록 보존은 구체적으로 없음. / [continuity.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/continuity.md:39>): 버전·의존성·임시복원·원자 게시 일반 계약 이미 있음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-038, GAP-039
- 이번 재검토: 엄격한 저장 계약이라는 과도한 제목을 실제 플레이어 기록 보존으로 좁힘.

## GAP-264 시설 절도 확률과 운영 위험 지수의 계산 설명 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: economy-and-trade / guests-services-and-performance / facility retail entries
- 현재 확인한 차이: 공개 손님 안내에는 절도 사고 개요만 있으며 현재 절도 확률의 선행 조건·개별 항과 운영 위험 지수의 구분이 없다.
- **위키에 작성할 정보:**
  - 시설이 없거나 장바구니 물품 수 또는 재고가 0이면 절도 확률은 0이다. 기본 압력 0.01에 직원 없음 +0.07 또는 직원 있음 -0.03을 더한다.
  - 낮은 수치의 압력 P(값,편안함,위기)=clamp((편안함-값)/(편안함-위기),0,1)이다. 기분 항은 0.08×P(기분,65,15)이며, 욕구 항 0.05×max(P(허기,45,5),0.6×P(재미,40,0),0.4×P(수면,35,0),0.9×P(배설,55,10),0.55×P(위생,45,5))와 별도로 더한다.
  - 혼잡 항은 0.04×clamp(clamp(이용자수/수용량,0,1)+무직원 계산대기 시 0.5,0,1)이며 수용량이 0이면 이 항은 0이다.
  - 장바구니 가치가 양수일 때 가치 항은 0.05×clamp(clamp(가치/500,0,1)+0.5×clamp((물품수-1)/3,0,1),0,1)이다. 손상 시설은 0.05, 재고가 보충 요청 기준 이하이면 0.0125를 추가한다.
  - 합산 압력에 시설 보정과 0 하한, 손님의 범죄 위험 배율을 적용한 뒤 0~1로 제한한 값이 절도 확률이다. 운영 위험은 물품수 1 기준 절도 확률×10에 무직원 계산대기 시 0.04를 더한 지수로, 실제 사건 확률과 동일하지 않다.
- **위키에 작성하지 않을 정보:**
  - 기분까지 욕구 최댓값 안에 넣는 잘못된 공식
  - 운영 위험 지수를 별도 사고 발생 확률이나 최대 100%의 값으로 소개하는 내용
- 감사 원본 근거: [FacilityCrimeSettings.asset 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/Config/FacilityCrimeSettings.asset:15>): 기본·감독·기분·욕구·혼잡·가치·손상 계수와 위험 지수 배율. / [FacilityCrimeRiskUtility.cs 59행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/FacilityCrimeRiskUtility.cs:59>): 선행 조건과 보정 순서. / [FacilityCrimeRiskUtility.cs 145행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/FacilityCrimeRiskUtility.cs:145>): 기분은 별도 가산, 욕구만 최댓값. / [FacilityCrimeRiskUtility.cs 157행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/FacilityCrimeRiskUtility.cs:157>): 혼잡·가치 각각 중간 clamp를 적용. / [FacilityCrimeRiskUtility.cs 79행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/FacilityCrimeRiskUtility.cs:79>): 물품수1 기준 운영 위험 지수.
- 감사 위키 근거: [economy-and-trade.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md:15>): 가격/거래 규칙에 절도 압력 수치 없음. / [guests-services-and-performance.md 35행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:35>): 절도 사고와 대응 개요만 있음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-140
- 이번 재검토: 기분과 욕구를 같은 max에 묶은 산식 오류 및 중간 clamp 누락 정정.

## GAP-265 시설·상점·창고 관리 탭의 집계 표시 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: inventory-and-carrying / economy-and-trade / facility management
- 현재 확인한 차이: 재고 가이드는 물리 용량을 설명하지만 관리 탭에 표시되는 집계 값과 구분은 안내하지 않는다.
- **위키에 작성할 정보:**
  - 시설 관리 요약은 전체 시설·방문 시설·작업 시설·손상 시설 수를 구분한다. 상점 요약은 상점 수와 재고가 있는 상점·품절 상점을 나눈다.
  - 창고 탭은 창고 수, 물리 재고 총개수와 카테고리별 개수, 현재 적재 중량과 최대 중량을 kg로 표시한다. 개수와 중량은 서로 다른 합계다.
- **위키에 작성하지 않을 정보:**
  - 화면의 '다음 UI 연결 후보'·'창고 탭에 들어가야 할 핵심' 목록을 이미 구현된 기능으로 소개하는 내용
  - 카탈로그 순회나 enum 숫자 등 내부 표시 구현
- 감사 원본 근거: [BuildingManagementSummaryQuery.cs 114행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Buildings/Core/BuildingManagementSummaryQuery.cs:114>): 시설/상점/창고 요약 합산. / [UITabContentTextProvider.cs 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/UI/UITabContentTextProvider.cs:141>): 상점 집계 뒤 개발 후보 문구 구분. / [UITabContentTextProvider.cs 175행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/UI/UITabContentTextProvider.cs:175>): 창고 실제 집계 출력과 미구현 후보 문구 구분.
- 감사 위키 근거: [inventory-and-carrying.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/inventory-and-carrying.md:39>): 물리 용량 원칙은 있지만 관리 UI 집계 안내는 없음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 이번 재검토: 작성 안내에 실제 화면 집계와 개발 후보 문구를 분리.

## GAP-266 건설 메뉴 순서와 시설 구매 가격식 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: infrastructure / facility-growth / economy-and-trade
- 현재 확인한 차이: 시설 성장·경제 가이드에 건설 메뉴의 분류 순서와 시설 구매 가격 계산이 없다.
- **위키에 작성할 정보:**
  - 건설 메뉴 분류 순서는 기타→벽/문→상점→특수→이동→생산→제작→자원이며 내벽문은 벽/문에 속한다.
  - 시설 구매 기본가는 max(25,max(1,별등급)×분류 가중치+희귀도 가산-기본 구매 할인)이다. 가중치는 기타·벽/문·이동 100, 상점 90, 특수 140, 생산 110, 제작 120, 자원 130이다.
  - 시설 상품은 별 2개 이상이면 희귀 가산80, 그보다 낮으면 일반 가산0이며 기본 구매 할인은20이다. 기본가에 최소0.05인 시설 구매 배율을 곱해 반올림하고 최종가는 최소1이다.
- **위키에 작성하지 않을 정보:**
  - 내부 분류 ID·정렬 숫자
  - 이 시설 구매식을 아이템 소매·청사진 구매·건설 자재 비용에 일괄 적용하는 내용
  - 가격 함수에 남은 특수 희귀도 가산160을 실제 시설 상품에도 적용된다고 쓰는 내용. 현재 시설 상품 생성은 일반·희귀만 선택한다.
- 감사 원본 근거: [GameDomainContentCatalog.asset 3167행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/GameDomainContentCatalog.asset:3167>): 8개 분류의 표시 순서와 가격 가중치. / [GridConstructTab.cs 466행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Controllers/Grid/DungeonStory/UI/GridConstructTab.cs:466>): 내벽문을 벽/문 메뉴로 배정. / [FacilityShopSystem.cs 602행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityShop/FacilityShopSystem.cs:602>): 시설 기본가·희귀도·할인·반올림·최저가격과 배율 하한. / [FacilityShopSystem.cs 555행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/FacilityShop/FacilityShopSystem.cs:555>): 상품 생성은 ResolveBuildingRarity 결과만 전달하며591행에서 별2 이상은Rare, 이하는Common. Special 가산은 이 경로에서 사용되지 않는다.
- 감사 위키 근거: [economy-and-trade.md 15행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/economy-and-trade.md:15>): 아이템 가격·교역만 있고 시설 구매 가격식 없음. / [facility-growth.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/facility-growth.md:9>): 성장 경로 개요이며 시설 구매 분류/가격 입력은 없음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 이번 재검토: 코드 분류를 공개 메뉴명으로 바꾸고 실제 가격식의 배율·최종 하한까지 명시.

## GAP-267 건설·철거의 위치·구역·지지 조건 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: spaces / infrastructure / facility-growth
- 현재 확인한 차이: 공간 가이드는 층과 통로의 원칙을 설명하지만 실제 설치·철거를 막는 조건을 구체적으로 나누지 않는다.
- **위키에 작성할 정보:**
  - 해금된 건물의 전체 설치 면적이 격자 안에 있고 좌우 끝에서 한 칸 이상 떨어져야 한다. 내벽문은 설치된 내벽 한 칸에만 설치하며, 다른 건물은 해당 설치 층이 비어 있어야 한다.
  - 던전 내부는 구역상 건설을 허용한다. 외부 문은 통행 가능한 구역에, 통로·설비·컨베이어는 입구·투하 구역·외부 통로에도 설치할 수 있다. 입구에는 문과 구조벽도 구역상 허용되지만 이후의 개별 조건과 공사 안전 검사는 별도다.
  - 건물 밑면의 각 칸에 지지가 필요하되 격자 최하단 y=0은 아래 지지 검사를 하지 않는다. 철거 후 바로 위 점유물을 받칠 통로·건물이 남지 않으면 철거할 수 없다.
- **위키에 작성하지 않을 정보:**
  - 구형 금화 건설 조건을 실제 지불 조건으로 안내하는 내용
  - 구역 허용만으로 공사 안전·해금·자재·다른 설치 조건까지 모두 충족했다고 설명하는 내용
- 감사 원본 근거: [GridBuildingRuntime.cs 1048행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Grid/Building/GridBuildingRuntime.cs:1048>): 해금·경계·내벽문·구역·층점유·지지 및 개별조건 검사. / [GridPlacementValidator.cs 34행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Grid/Placement/GridPlacementValidator.cs:34>): 밑면 지지와 철거 후 상단 지지 검사. / [GridCellAreaRules.cs 62행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Grid/Core/GridCellAreaRules.cs:62>): 내부·입구·외부 구역 허용 범위. / [GridBuildingRuntime.cs 1165행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Grid/Building/GridBuildingRuntime.cs:1165>): 구형 ConditionNeedMoney는 적용하지 않음.
- 감사 위키 근거: [spaces.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:39>): 레이어/벽/계단 원칙만 있고 설치 gate 표 없음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-236
- 이번 재검토: 설치 gate를 플레이어 조건으로 풀어 쓰고 공사 안전과 혼동하지 않게 수정.

## GAP-268 선택 건물 상세 패널에서 확인할 정보 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: infrastructure / inventory-and-carrying / residents-and-work
- 현재 확인한 차이: 시설·재고 가이드에는 건물·공사·오물 선택 시 상세 패널에서 읽을 수 있는 정보가 모여 있지 않다.
- **위키에 작성할 정보:**
  - 일반 건물 상세에서 손상 여부·레벨·위치·분류, 이용자/수용량·방문 예약, 시설 역할·지원 업무·필요 작업자를 확인한다.
  - 재고 시설은 현재/최대 재고와 보충 필요 여부를, 창고는 현재/최대 kg를 확인한다. 장비 제작 시설은 제작 가능 품목 또는 대기 주문의 남은 작업량과 재료 이동 상태를 확인한다.
  - 공사 현장에서는 안전 안내, 주문 상태와 완료/필요 작업량·진행률, 전달된 재료와 예약 작업자를 확인한다.
  - 오물에서는 종류·위치·양, 해당 위치 오염원의 최대 감염 위험과 청결 감점, 청소 작업량·우선 청소 여부를 확인한다.
- **위키에 작성하지 않을 정보:**
  - UI 조회 객체·지역화 키·내부 주문 ID와 저장 권위 설명
  - 패널의 필요 작업자 수만으로 실제 동시 배치 인원·생산 속도를 단정하는 내용
- 감사 원본 근거: [BuildingSummaryFormatter.cs 105행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingSummaryFormatter.cs:105>): 방문·업무·필요작업자 표시. / [BuildingSummaryFormatter.cs 141행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingSummaryFormatter.cs:141>): 오물 집계, 감염은 최대값. / [BuildingSummaryFormatter.cs 202행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildingSummaryFormatter.cs:202>): 장비 제작·재고·공사 표시 항목. / [UIBuildingInfo.cs 234행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/UIBuildingInfo.cs:234>): 상세 패널에 formatter 결과 연결.
- 감사 위키 근거: [infrastructure.md 27행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:27>): 설비별 점검 원칙만 있고 공통 상세 패널 안내 없음. / [inventory-and-carrying.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/inventory-and-carrying.md:39>): 용량 원칙과 표시 UI 의미를 구분.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-260, GAP-236, GAP-210, GAP-265
- 이번 재검토: 조회/저장 내부 계약을 제외하고 관찰 가능한 표시 의미로 구체화.

## GAP-269 구조벽 공사의 안전 대기와 강제 공사 경고 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: spaces / infrastructure / residents-and-work
- 현재 확인한 차이: 공간 가이드는 동선 영향만 설명하며 구조벽 공사의 자동 대기 사유와 강제 공사의 차이를 안내하지 않는다.
- **위키에 작성할 정보:**
  - 문이 아닌 구조벽 공사는 작업자 퇴로, 입구·외부 동선, 먼저 필요한 문, 남은 공사 접근을 차례로 검사하고 안전하지 않으면 해당 사유로 대기한다.
  - 강제 공사는 불안전 판정을 '강제 공사: 갇힘 가능' 경고로 바꾸어 안전 검사상 진행을 허용한다. 강제 지시가 갇힘을 방지해 주는 것은 아니다.
- **위키에 작성하지 않을 정보:**
  - 출구 후보가 없는 경우까지 탈출을 보장한다고 설명하는 내용
  - 강제 안전 허용만으로 다른 작업 자격·접근·재료 조건도 모두 우회한다고 설명하는 내용
- 감사 원본 근거: [ConstructionSafetyPlanner.cs 47행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Grid/Building/ConstructionSafetyPlanner.cs:47>): 강제 경고는 IsSafe=true로 전환. / [ConstructionSafetyPlanner.cs 100행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Grid/Building/ConstructionSafetyPlanner.cs:100>): 퇴로·입구·문·남은 공사 순서, 135행 구조벽 한정. / [WorkTargetEvaluator.cs 114행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTargetEvaluator.cs:114>): 강제 여부 전달 후 안전하지 않으면 후보 거부.
- 감사 위키 근거: [spaces.md 37행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:37>): 통로 영향만 설명하며 공사 시작 안전 검사·강제 경고 없음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 퇴로 검사는 출구 후보 자체가 없으면 true를 반환한다. 안전 표시를 모든 상황의 탈출 보장으로 일반화하지 않는다.
- 관련 GAP(제외 이력 포함): GAP-267
- 이번 재검토: 강제 명령도 안전 실패한다는 오해를 없애고 경고로 허용되는 실제 흐름을 명시.

## GAP-270 계단의 인접 층 연결과 이동 시간 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: spaces / residents-and-work
- 현재 확인한 차이: 공간 가이드는 층 연결, 도감은 크기를 설명하지만 계단을 이용할 때의 연결 범위와 이동 시간을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 계단은 같은 가로 위치의 바로 위·아래 층을 연결한다. 현재 계단 크기는 3×2칸이다.
  - 층 중앙 접근은 이동 속도 0.9배로 진행하고, 이동 중 모습을 숨긴 채 2초를 기다린 뒤 반대 층에 배치하며 0.06초 후 논리 목적지에 정렬하고 모습을 복원한다. 접근 시간을 포함한 전체 소요 시간은 고정 2초가 아니다.
- **위키에 작성하지 않을 정보:**
  - 경로 비용의 내부 단위와 렌더링 좌표·시각 복원 코드
  - 중단·실패를 포함한 모든 상황에서 시각 복구를 실험으로 검증했다고 주장하는 내용
- 감사 원본 근거: [Stair.asset 113행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/Stair.asset:113>): 크기3×2 및121행 이동2초. / [Grid.cs 1152행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Grid/Core/Grid.cs:1152>): 계단은 동일x·y차이1 연결. / [Stair.cs 42행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Stair.cs:42>): 0.9배 접근·2초 숨김·0.06초 후 정렬, finally 표시 복원.
- 감사 위키 근거: [spaces.md 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:39>): 층 연결만 설명. / [building-4.json 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-4.json:18>): 크기/건설비 외 시간·경로비용 없음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 이번 재검토: 내부 경로 비용/렌더 계약을 공개 정보에서 분리하고 실제 소요 시간을 명확히 함.

## GAP-271 시설 이용·작업·계산 위치의 기본 규칙 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: infrastructure / guests-services-and-performance / residents-and-work
- 현재 확인한 차이: 서비스 가이드는 이동·대기 순서만 설명하며 이용자·작업자·계산대 접근 위치가 다를 수 있음을 안내하지 않는다.
- **위키에 작성할 정보:**
  - 별도 위치가 지정되지 않은 시설은 이용자가 시설이 놓인 층의 점유 칸 중 출발 가로 위치에 가장 가까운 칸을 사용하고, 찾지 못하면 가로 중앙을 사용한다.
  - 기본 작업 위치는 같은 층의 왼쪽 끝 점유 칸 중심에서 오른쪽 끝 점유 칸 중심까지 거리의 85%, 계산 위치는 75% 지점이다. 이 숫자는 위치 비율이며 작업·계산 속도 배율이 아니다. 지정된 위치가 있으면 지정 위치를 우선한다.
- **위키에 작성하지 않을 정보:**
  - 퇴장 위치 목적의 등록만으로 모든 퇴장 동작이 그 위치를 사용한다고 설명하는 내용
  - 시설 에셋 수·빈 슬롯 수 전수 집계와 목적 ID 같은 개발 감사 정보
- 감사 원본 근거: [BuildingSO.cs 79행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/SO/BuildingSO.cs:79>): 기본 이용/작업85%/계산75% 위치. / [BuildableObject.SpatialAndInteraction.cs 97행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/BuildableObject.SpatialAndInteraction.cs:97>): 동일층 최근접 점유칸 및132행 지정 위치 조회. / [Facility.cs 833행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:833>): 작업 위치 이동. / [ShopCustomerInteractionService.cs 240행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/ShopCustomerInteractionService.cs:240>): 계산 위치를 실제 이동 목표로 사용.
- 감사 위키 근거: [infrastructure.md 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/infrastructure.md:11>): 설치 흐름만 있고 접근 위치 없음. / [guests-services-and-performance.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:21>): 서비스 이동/대기 흐름만 있음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-239
- 이번 재검토: 속도와 위치 비율을 분리하고 실행되지 않는 퇴장 목적과 에셋 총수 주장을 제외.

## GAP-272 새 게임의 초기 내부 공간과 입구·투하 구역 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: spaces / getting-started / inventory-and-carrying
- 현재 확인한 차이: 공간 가이드에는 초기 내부 폭과 입구·투하·외부 구역의 배치가 없다. 장면의 27열은 초기 재조정 전 값이지 새 게임 완료 후 내부 폭이 아니다.
- **위키에 작성할 정보:**
  - 현재 기본 새 게임의 초기 내부 공간은 29열·높이 3칸이며 입구 왼쪽에 폭 3칸의 투하 구역이 있다.
  - 현재 기본 격자에서 내부 가로 범위는 x=17~45, 입구는 (17,0), 투하 구역은 x=14~16의 지면이다. 내부 밖은 입구와 같은 높이만 외부 통로이고 다른 높이는 차단 외부 구역이다. 입구 칸은 일반 내부 칸과 별도로 취급한다.
- **위키에 작성하지 않을 정보:**
  - 장면에 저장된 27열을 플레이어가 시작하는 최종 내부 폭으로 소개하는 내용
  - 렌더링 z값·초기화 순서·장면 저장 필드 등 개발 정보
- 감사 원본 근거: [GameplayScene.unity 6339행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scenes/GameplayScene.unity:6339>): 60×3·중앙27열·투하폭3의 초기 장면 설정. / [GridSystemManager.cs 393행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Controllers/Grid/System/GridSystemManager.cs:393>): 중앙 시작열17 계산 및411행 구역 우선순위. / [DungeonSceneNavigation.cs 453행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/DungeonSceneNavigation.cs:453>): 새 게임 Tier0 재조정 호출. / [DungeonSpaceExpansionRuntime.cs 533행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs:533>): 27열 장면을29열 초기 공간으로 맞춤.
- 감사 위키 근거: [spaces.md 11행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:11>): 초기 구역 좌표·폭 없음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 검증 한계·감사 메모: 좌표는 현재 기본 장면과 새 게임 재조정의 정적 계산이다. 임의 지도·기존 저장에 동일 좌표를 적용하지 않는다.
- 관련 GAP(제외 이력 포함): GAP-273
- 이번 재검토: 장면 seed와 플레이 시작 상태를 분리. 렌더링 제외 문장을 작성 목록에서 제거.

## GAP-273 연구에 따른 내부 공간 확장 폭과 방해 조건 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: facility-growth / spaces / research-and-blueprints
- 현재 확인한 차이: 연구·시설 성장 가이드는 해금만 설명하며 채광 연구에 따른 실제 내부 폭 확장과 방해 점유물 조건이 없다.
- **위키에 작성할 정보:**
  - 내부 폭은 시작 29열에서 채석장 연구 완료 시 51열, 석재 가공 연구 완료 시 71열, 심부 채굴 연구 완료 시 87열로 늘어난다. 높이는 3칸이며 기존 입구와 내부 왼쪽 경계는 유지한다.
  - 새 내부로 바뀔 칸에 방해 점유물이 있으면 확장이 거부된다. 자원 노드와 야생동물 자체는 이 차단 대상에서 제외된다.
  - 확장 뒤 기존 위치를 사용할 수 없는 야생동물은 유효한 가까운 칸으로 옮기며, 이동 후보가 없으면 확장을 거부한다.
- **위키에 작성하지 않을 정보:**
  - 단계별 예상 인구12·18·24를 영입 상한·확장 선행 조건으로 소개하는 내용
  - 격자 최대 폭104·교체 게시·ID·이벤트·동률 정렬 규칙 같은 내부 구현
- 감사 원본 근거: [DungeonSpaceExpansionRuntime.cs 79행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs:79>): 연구별29/51/71/87열과 높이3. / [mining_quarry.asset 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Research/Projects/mining_quarry.asset:25>): 연구 ID와 표시명 채석장. / [mining_stonecutting.asset 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Research/Projects/mining_stonecutting.asset:25>): 연구 ID와 표시명 석재 가공. / [mining_deep.asset 25행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Research/Projects/mining_deep.asset:25>): 연구 ID와 표시명 심부 채굴. / [DungeonSpaceExpansionRuntime.cs 455행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs:455>): 전환 칸 점유 검사 및500행 입구/시작열 보존. / [DungeonSpaceExpansionRuntime.cs 564행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs:564>): 자원 노드·야생동물은 점유 차단 예외. / [GridSystemManager.cs 171행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Controllers/Grid/System/GridSystemManager.cs:171>): 야생동물 재배치 후보와 없을 때 거부.
- 감사 위키 근거: [research.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/research.md:21>): 해금 일반 설명뿐 공간 확장 수치 없음. / [facility-growth.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/facility-growth.md:9>): 시설 성장과 달리 내부 폭 확장 설명 없음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-272
- 이번 재검토: 예상 인구를 실제 제한으로 오해하지 않게 제외하고 공개 내용에서 내부 게시 계약 제거.

## GAP-274 자연 연못의 생성·수량과 수심별 통행 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: water / spaces / food-and-ecology
- 현재 확인한 차이: 식량·공간 가이드는 물의 환경 조건만 설명하고 자연 연못의 생성 및 얕은 물·깊은 물의 이동 차이를 안내하지 않는다.
- **위키에 작성할 정보:**
  - 수원이 하나도 없고 외부 통로에 야외 지면이 있을 때 기본 자연 연못을 만든다. 가장 긴 연속 지면 구간을 고르고 길이가 같으면 오른쪽 구간을 우선하며, 바깥쪽부터 최대 4칸을 사용한다.
  - 첫 칸은 깊은 물, 나머지는 얕은 물이다. 깊은 물의 용량은 40·기본 초당 회복량은 0.035, 얕은 물은 18·0.06이며 실제 회복에는 환경 배율이 적용된다. 공간이 짧으면 연못도 4칸보다 작다.
  - 깊은 물은 통행할 수 없고 얕은 물에서는 지형 이동 속도가 0.65배가 된다.
- **위키에 작성하지 않을 정보:**
  - 자연 연못이 항상 정확히4칸이라는 설명
  - 물 수량 고갈만으로 자동 마른 땅이 된다는 설명
  - 수원 저장 ID·지형 동기화·복원용 제거 처리
- 감사 원본 근거: [WorldWaterRuntime.cs 390행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldWaterRuntime.cs:390>): 기본 수원 생성 조건 및 깊은40/.035·얕은18/.06. / [WorldWaterRuntime.cs 427행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldWaterRuntime.cs:427>): 최장·오른쪽 연속 구간의 바깥쪽부터 최대4칸. / [WorldWaterRuntime.cs 129행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Survival/WorldWaterRuntime.cs:129>): 회복량은 배율과 시간 적용 후 용량 상한. / [GridCell.cs 39행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Grid/Core/GridCell.cs:39>): 깊은물 통행 불가·얕은물 지형속도0.65배.
- 감사 위키 근거: [food-and-ecology.md 9행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/food-and-ecology.md:9>): 물/환경 일반조건만. / [spaces.md 33행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/spaces.md:33>): 수심별 이동 차이 없음.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-259
- 이번 재검토: 바뀐 줄번호 및 회복 배율을 보완하고 지형 제거의 내부 처리를 공개 규칙과 분리.

## GAP-275 시설 이용 시간과 완료 예상 시간의 차이 안내 누락

- 분류: 플레이어 규칙·안내의 누락 또는 오류
- 보완할 문서: guests-services-and-performance / research / residents-and-work / facility entries
- 현재 확인한 차이: 서비스 가이드에는 이용 단계만 있으며 실제 이용 시간 선택과 주민의 시설 완료 예상 시간 계산이 다를 수 있음을 설명하지 않는다.
- **위키에 작성할 정보:**
  - 일반 시설 이용 시간은 서비스 세션에 양수 시간이 있으면 그 시간을, 그렇지 않으면 시설 기본 이용 시간을 사용하고 방문객의 체류 시간 배율을 곱한다. 식사를 실제로 수행하는 경로는 이 대기 시간만으로 식사를 끝내지 않는다.
  - 시설 선택용 완료 예상 시간은 이동 예상 시간에 앞선 이용자·예약자 수와 수용량으로 계산한 회차 수×예상 서비스 시간을 더한다. 회차는 최소1이며 예상 서비스 시간은 시설 기본값 최소0.1초, 식사일 때 최소4초에 방문객 체류 배율 최소0.1을 곱한다. 이는 정확한 서비스 잔여 시간이나 완료 보장이 아니다.
- **위키에 작성하지 않을 정보:**
  - 시설 역할·필요 작업자 수로 계산하는 구형 연구 시간 배율을 현재 승인 WU 연구 경로에 다시 곱하는 설명
  - 연구 책상 이용시간1.5초를 연구 완료 시간으로 소개하는 내용
  - 시설 에셋 총수·worker 필드 분포 같은 감사 집계
- 감사 원본 근거: [Facility.cs 385행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Buildings/Facility.cs:385>): 실제 세션시간 우선 및 체류배율, 물리 식사는 Linger 제외. / [FacilityCandidateScorer.cs 245행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/AI/FacilityCandidateScorer.cs:245>): 예상 회차·시설시간 하한·식사4초·방문객배율 하한. / [BlueprintResearchRuntime.cs 174행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs:174>): 승인WU와 시간 기반 연구 경로 분기. / [ResearchWorkExecutionAdapter.cs 338행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs:338>): 실제 작업 승인량은 ApplyApprovedResearchWork로 전달.
- 감사 위키 근거: [building-1030.json 18행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1030.json:18>): 역할·건설비만 있고 이용시간/필요작업자 의미 없음. / [guests-services-and-performance.md 21행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/guests-services-and-performance.md:21>): 서비스 흐름만 설명. / [research.md 31행](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/research.md:31>): WU/도달목표만 설명.
- 감사 상태: `owner-gap-confirmed-narrowed` / `global_deduplication=resolved`
- 관련 GAP(제외 이력 포함): GAP-049, GAP-172, GAP-153
- 이번 재검토: 실제 이용 시간에도 최소0.1 배율이 적용된다는 오류를 제거. 구형 연구 배율의 전 경로 일반화를 제외.
