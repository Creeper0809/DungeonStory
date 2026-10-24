# SO 관계 전수 감사 — 2026-10-24

## R19-01~20 수정 및 필요한 실행 검증 완료

기준 main은 886973b7이다. 아래는 그 감사에서 찾은 20묶음의 수정 결과이며, 모든 미래 콘텐츠와 임의 코드 변경까지 자동으로 안전하다는 보증이 아니다. 기존 에셋, 게시 ID, GUID 및 저장 DTO 버전은 유지했다.

| 항목 | 수정 | 필요한 확인 경계 |
|---|---|---|
| R19-01 | 실제 120일 생산 권위에서 기존 엔딩 flag를 호환 투영 | 119일 JSON 후120일 완료, 같은 날/복원 후 무중복 |
| R19-02 | 실제 시설 능력과 운영 판정을 snapshot과 live query에 공유 | 실제 시설 projector, preflight, 장 선택, 불가 시 무변경과 복원 |
| R19-03 | 실제 장비 counter가 있을 때만 기존9키를 호환 투영 | 빈 파티, 실제 장비와 spore 전투 계산, unknown 거절 |
| R19-04 | WorkType symbol과 저장 ID의 대응을 보호 | ID 교환/제거/rename 반례와 정상 추가 |
| R19-05 | behavior registry와 domain에서 후보 생성 | 잘못된 행도 수정 후보 유지, unknown 거절, 실제 Inspector |
| R19-06 | 실제 등록된 서비스 hub에서 선택 | 다른 hub와 unknown 거절 |
| R19-07 | mode feature를 시작/단계/완료 명령에서 강제 | 실제 방/지원 연결, V7 JSON, 실패 후 복구와 결제1회 |
| R19-08 | 직접 BaseGenome 참조가 초기 씨앗과 인증의 권위 | suffix와 다른 등록 genome, 초기 지급/복원/중복 거절 |
| R19-09 | 촉매 feature와 runtime parser가 같은 ID 의미를 검증 | 현재 촉매와 잔재, family/potency/residue 모순 거절과 수정 재시도 |
| R19-10 | 실제 score/metric 생산자에서 조건 key 선택 | max-only unknown 거절, provider 정의의 자유 key 유지 |
| R19-11 | 실제 integer4/multiplier5 대상에서 종류별 선택 | 잘못된 종류/빈 target 거절, pending Inspector 문맥 |
| R19-12 | 시작 숙련도 후보를 실제 허용9종으로 일치 | 등록 SO와 owner 허용 집합 대조 |
| R19-13 | capacity의 미정의 enum과 null 행 거절 | 현재 정의와 invalid/수정 재시도 |
| R19-14 | incident/category/mode를 의미가 표시된 숫자 선택기로 연결 | 정확한6/5/2 집합, 실제 Inspector와 invalid 거절 |
| R19-15 | 정의의 작업자 정책에서 실행 인스턴스 ID 작성 금지 | 현재 정책, invalid 모드/ID 거절, runtime DTO JSON 보존 |
| R19-16 | 게시된 에셋에도 새 빈 identity 추가 거절 | C#/Python 반례, 기존 legacy 빈 값 보존 |
| R19-17 | formula parameter/mirror/applicator/stat을 실제 권위에 연결 | 종류/owner 선택과 거절, pending 입력과 atomic Undo |
| R19-18 | 생산 registry ID와 version을 함께 선택하고 catalog 조립에서 등록 확인 | unknown ID/version 조기 거절, empty 계약과 payload 보존 |
| R19-19 | 실제 tools/weather 생산자에서 조건 tag 선택 | 현재 후보, unknown 거절과 복구 |
| R19-20 | 미소비 nest/migration/선호 family를 숨김 legacy metadata로 명시 | 실제 생태 관계만 집계, 현재 species 정의와 미정의 season 거절 |

새 SO 분리나 저장 구조 재편은 하지 않았다. 기존 코드 registry와 provider 정의를 선택 권위로 사용한다. 독립 게임 기능이 없는 legacy 필드를 새 기능으로 표시하지 않는다. 전수 seed와 장기 밸런스 인증을 실행한 것으로 세지 않는다.

[R19 작성/실행/Inspector 영수증](Artifacts/QA/ImpactCI/r19-verification.json), [변경 영향 실행](Artifacts/QA/ImpactCI/verification.json), [공식 월드 저장/복원](Artifacts/QA/ImpactCI/world-round-trip.txt), [Inspector 격리](Artifacts/QA/ImpactCI/r19-inspector-isolation.json).

수용한 각 native 실행은 C# 오류0, consoleErrors0, source drift0이다. 실제 Inspector는 인증된 private Xvfb에서 Layout/Repaint와 pending 입력, Apply/Undo를 확인했다. 진행과 서비스의 통제된 실제 명령, 실제 장비 snapshot의 counter 계산, 공식 GameplayScene의 저장/복원을 구별한다. 자연 AI의 모든 콘텐츠 발동이나 사람의 전체 UI 조작을 전수 인증한 결과는 아니다.

기존3544에셋/10721게시 키/WorkType31 보존, Python77개 및 고정 분석기 검증을 통과했다. r134 컴파일 및 r135 작성 실패는 실패로 보존했으며 과거 PASS를 현재 source로 바꾸지 않았다. 원본 Documents checkout과 과거 QA를 보존했다.

실행 source `4b61fd8575a91115682a4a50267930a9d1cd27b997499d82c59df79ca0588b0b`, configuration `06520602d53b966e38f0a99f33686b51805b2ae9e72c0f7a3e56d13e3c7ad147`. GitHub 통합 완료 여부는 실제 PR과 CI 결과를 따른다.


작성과 runtime 증거는 r137sr의 실제 PASS와 당시 source/configuration을 유지했다. r138sr과 전체21,674입력을 대조하여 차이가 Editor pending reader 한 파일뿐임을 확인했다. 저장과 실제 소비, 정의와 에셋은 동일하고, 변경된 입력 복사와 실제 Inspector 및 필수 영향5그룹은 최종 입력에서 실행했다. 이전 PASS의 식별자를 현재 것으로 바꾸지 않았다.

## 수정 전 main886973b7 재감사 — 당시 잔여20묶음

기준은 원격 main `886973b7e1aa3a3acf8af0de79f26af8a350e50b`다. 아래 R18 완료 표는 그 수정 범위의 기록이며, 전체 SO 관계에 잔여가 없다는 뜻으로 사용하지 않는다. 이번에는 현재 선언, 실제 에셋, Inspector, 게시 검사, 실제 생산자와 소비자를 다시 대조했다. **수정 전 판정이다.** 특히 엔딩 flag, 세력 시설 조건, 전투 counter는 현재 콘텐츠와 실행 코드 사이의 단절이다.

이전 감사에서 별도 SO가 없는 코드 registry 관계를 자유 의미 키로 제외하고, 값이 유효하거나 검증기가 존재하는 것을 실제 소비와 동일시한 부분이 있었다. 이 판정을 정정한다. 반대로 새 identity를 정의하는 필드와 임의 표시 문구를 모두 외래키로 잠그지는 않는다.

당시 요청은 조사였다. 해당 감사에서는 게임 코드와 에셋을 수정하거나 Unity, 실제 Inspector, Play와 저장/복원을 새로 실행하지 않았다. 아래의 실제 함수 변형 재현 2건은 Python 게시 검증기 호출이며 Unity 실행 증거가 아니다.

### 모집단과 현재 값 대조

| 확인 대상 | 결과 | 보장 범위 |
|---|---:|---|
| 현재 게임 SO 타입 | 142 | 상속, partial, 중첩 레코드, SerializeReference 구현을 포함한 탐색 |
| 직렬화 선언 | 2,458 | 넓은 관계 후보 879개 외의 숫자/문자열 필드도 검토 |
| ContentReference 선언 | 199 | 표식 존재와 실제 소비의 정합성을 별도 확인 |
| 실제 게임 SO 에셋 | 3,544 | YAML 파싱 오류 0, 미게시 대상 0 |
| 직접 지정된 GUID / asset fileID | 8,962 / 4,731 | 지정된 대상의 누락과 metadata 중복 0. 필수 미선택과 문자열 의미 검사는 별도 |
| 게시 보호 필드 | 10,721 | 현재 에셋과 기존 보호값 일치. 아래 변경 반례의 차단을 뜻하지 않음 |
| Roslyn Editor / Player 구문 분석 | 오류 0 | Unity 컴파일과 런타임 성공을 뜻하지 않음 |

새 추출과 읽기 전용 대조는 외부 [R19 조사 폴더](C:/Users/vulpo/Documents/Codex/2026-10-24/so-full-audit/main-reaudit-r19)에 보존했다. 원본 Documents checkout과 과거 QA를 유지했고 생성 지식베이스는 갱신하지 않았다. 좁은 ContentReferenceCatalog query는 fresh였으며 결과가 없었던 다른 query를 부재 증명으로 사용하지 않았다.

### 잔여 목록

각 행은 수정 대상 묶음이며 필드 수나 이미 깨진 에셋 수가 아니다. P1을 먼저 처리하고 P2는 기존 경로의 국소 보완으로 다룬다. 값이 정상인 항목을 현재 플레이 장애로 부풀리지 않는다.

| ID | 우선순위 | 남은 경계 | 현재 에셋과 영향 |
|---|---|---|---|
| R19-01 | P1 | 엔딩 요구 flag와 생산 flag 불일치 | 등록된 ‘시간 없는 성역’ 1개가 생산되지 않는 flag를 요구 |
| R19-02 | P1 | 시설 capability 조건의 두 평가 경로 불일치 | 36개 세력 장의 support/bargain 72선택이 snapshot에 없는 capability key를 조회 |
| R19-03 | P1 | 전투 counter 키에 생산자가 없는 값과 수기 입력 | 9개 미생산 키, encounter 12개와 enemy 9개의 힌트에 포함. spore-bloom counter는 실제 3개 encounter에서 성립 불가 |
| R19-04 | P1 | WorkType의 symbol과 저장 ID 대응 변경 보호 누락 | 현재 31개 정상. Craft/Cook ID 교환은 실제 verifier에서 통과 |
| R19-05 | P2 | skill module의 C# behavior 관계가 수기이고 고장난 행을 고칠 후보도 행 자체에서 역산 | 현재 24개 정상. 오타/다른 domain이면 후보 열거 또는 실제 생성에서 실패 |
| R19-06 | P2 | ServiceProcess.ownerHubTag가 수기이며 provider membership 검사가 없음 | 현재 5개 정상. 알 수 없는 정규 문자열도 정의/카탈로그 검사 통과 |
| R19-07 | P2 | process mode의 requiredFeatureTags가 실행에서 누락 | 현재 15개 mode 목록은 비어 있음. 새 요구 feature를 넣어도 session에서 강제되지 않음 |
| R19-08 | P2 | Crop.BaseGenome와 `:base` suffix가 각각 기본 유전체를 결정 | 현재 12개 일치. 같은 crop의 다른 등록 genome 선택 시 초기 씨앗과 certified seed가 서로 달라짐 |
| R19-09 | P2 | 촉매 item ID와 feature의 family/potency/residue가 독립 권위 | 현재 촉매 147개/잔재 21개 일치. 유효 범위의 feature 수정은 runtime 의미에 반영되지 않을 수 있음 |
| R19-10 | P2 | 시설 진화 requiredRoomScores/Metrics의 key가 수기 관계 | 현재 값 오류는 발견하지 않음. 미등록 max-only key는 0으로 처리되어 조건 통과 가능 |
| R19-11 | P2 | 메타 효과 id가 자유 identity처럼 입력되지만 실제로는 닫힌 코드 target 관계 | 현재 9개 정상. 오타나 integer/multiplier 종류 불일치가 조용한 효과 0/중립값으로 이어짐 |
| R19-12 | P2 | 시작 origin/history의 proficiency 후보와 실제 허용 집합 불일치 | 현재 15개 profile/내장 9종 정상. 유효한 확장 proficiency도 선택 후 owner 검사에서 거절 |
| R19-13 | P2 | 성능 공식 capacityId 숫자에 미정의 enum 거절 없음 | 현재 107개 공식/518행 정상. 99 같은 값은 정의 검사 뒤 실제 조회에서 예외 |
| R19-14 | P2 | CoreSession의 incident/service 연결이 수기 숫자 | 현재 값 정상. incident 99는 positive 검사 통과 후 어떤 실제 사건에도 연결되지 않음. service 숫자는 범위 검사는 있지만 의미 선택 UI가 없음 |
| R19-15 | P2 | Building 기본 작업자 정책에 실행 인스턴스 ID를 수기로 작성 가능 | 현재 400개 정책의 특정/제외 ID는 모두 빈 값. stale 특정 ID를 넣으면 새 건설 주문의 모든 작업자를 배제 가능 |
| R19-16 | P2 | 기존 게시 SO의 새 빈 identity field 게시를 거절하지 않음 | 현재 신규 빈 값 0. 실제 compare_baselines에서 새 빈 필드 추가 통과, 이후 빈 값이 게시값으로 고정될 위험 |
| R19-17 | P2, 작성면 | formula의 parameter/applicator/stat 관계 및 일부 mirror가 수기 | 현재 계약 정상이고 실제 formula 검사는 엄격함. 닫힌 코드 축을 참조하는 입력과 파생 mirror를 계속 직접 편집 |
| R19-18 | P2 | 생산 mass/cost capability registry 관계가 수기이며 거절이 늦음 | 현재 mass process-loss 307개와 cost weighted-output-share 3개 정상. 미등록 정규 키는 catalog를 지나 출력 완료/다중 출력 가치 계산에서야 거절 |
| R19-19 | P2, 작성면 | 원정 선택 requiredTag가 실제 context tag와 수기 연결 | 현재 tools 1개는 실제 생산됨. 오타는 변환 표시만 조용히 비활성화 |
| R19-20 | P2, 계약 정리 | 관계로 보이는 ecology/침입 필드의 실제 값 소비가 없음 | wildlife nest/migration은 ecology 관계 수에 포함. 침입 family 목록은 내용 대신 Count만 사용 |

### 현재 연결이 끊긴 세 경계

**R19-01.** [엔딩 에셋](Assets/Resources/SO/V20/Milestones/Endings/ending_timeless-sanctuary.asset#L34)의 `temporal:population-sustained`는 현재 root domain에 등록되어 있다. [일일 생산](Assets/Scripts/Services/Run/V20MilestoneWorldSnapshotProjector.cs#L199)과 [120일 누적](Assets/Scripts/Services/Run/V20MilestoneRuntime.cs#L550)은 `temporal:productivity-qualified-today`와 `temporal:productivity-sustained`를 만든다. 다른 현재 WorldFlag 효과에도 population-sustained 생산자가 없다. [실제 엔딩 평가](Assets/Scripts/Services/Run/V20MilestoneRuntime.cs#L204)는 요구 flag를 그대로 검사하므로, 새 정상 진행에서 이 엔딩 조건을 만족시킬 수 없다. requiredFlags/excludedFlags는 자유 문구가 아니라 flag 생산자와의 관계다. 실제 생산 목록과 작성 목록을 연결하고, 요구와 의미가 일치하도록 고쳐야 한다. 저장된 flag를 무조건 지우거나 검사 자체를 제거하는 해결은 부적절하다.

**R19-02.** [에셋의 시설 조건](Assets/Resources/SO/V20/Factions/Chapters/chapter_beastkin_1.asset#L48)은 빈 building ID와 Administration 같은 capability를 쓴다. [실제 preflight](Assets/Scripts/Services/Infrastructure/ContentRequirementEvaluator.cs#L64)는 운영 중인 시설 query로 검사한다. [같은 서비스의 다음 단계](Assets/Scripts/Services/Run/V20ContentResolutionService.cs#L346)는 수정하지 않은 요청 snapshot으로 candidate를 호출한다. [TryResolveChapter](Assets/Scripts/Services/Run/V20CampaignRuntime.cs#L544)는 [공통 조건식](Assets/Scripts/Services/Run/V20ContentDefinitionRules.cs#L21)의 `capability:Administration` 등의 key를 다시 요구하지만, [snapshot 생산자](Assets/Scripts/Services/Run/V20MilestoneWorldSnapshotProjector.cs#L77)는 building ID count만 만든다. 일반 UI도 [world.Build 결과](Assets/Scripts/Services/Infrastructure/V21ContentAlertChoiceActionDispatcher.cs#L223)를 사용한다. 따라서 정상 시설이 있어도 support/bargain은 두 번째 비교에서 실패하는 경로이며, 시설 조건 없는 refuse와 구별해야 한다. 운영 여부와 count의 권위를 하나로 맞추고 시설 capability 선택/게시 membership도 함께 보완한다.

**R19-03.** [실제 counter 생산자](Assets/Scripts/Models/Offense/Core/OffenseBattleContracts.cs#L209)의 현재 집합은 32개다. 현재 작성된 키 중 `break-contract`, `break-formation`, `decoy`, `deny-retreat`, `detector`, `fungicide`, `isolate-leader`, `rush`, `separate-cargo`의 counter 키 9개는 이 생산자에 없다. 특히 등록된 [spore-bloom](Assets/Resources/SO/V20/Combat/Modifiers/battlefield_spore-bloom.asset)의 `counter:fungicide`는 encounter_11/23/35에 사용되며 [보정 해제 조건](Assets/Scripts/Models/Offense/Core/OffenseBattleContracts.cs#L172)을 만족시킬 수 없다. enemy 쪽 counter 일부는 힌트에만 쓰이므로 전부 전투 보정 손실로 세지 않는다. 새 counter에 실제 장비/행동 생산 경로를 연결하거나 의도된 지원 집합으로 데이터를 정합화해야 한다. 잘못된 조건을 선택 목록에 추가하는 것만으로 해결되지 않는다.

### 작성과 변경 보호의 구체적인 수용 경계

- **R19-04/16:** [현재 보호기](tools/V27Balance/verify_content_identities.py#L1289)는 WorkType 값 집합만 비교하여 symbol과 ID 교환을 놓친다. [C# 신규 게시 검사](Assets/Editor/ContentIdentityProtection.cs#L410)는 기존 GUID면 새 field 검사도 건너뛰고, Python 비교도 과거 row만 본다. 외부 [재현 결과](C:/Users/vulpo/Documents/Codex/2026-10-24/so-full-audit/main-reaudit-r19/identity-guard-repros.json)는 실제 두 함수의 통과를 기록한다. 현재 legacy 빈 scalar 350개는 기존 값이며 신규 결함으로 세지 않는다. 과거 symbol 대응과 새 identity row를 각각 검증하되 기존 저장 호환은 유지한다.
- **R19-05/17:** [module.capabilityId](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L439)는 [C# registry](Assets/Scripts/Services/Character/Core/CharacterSkillModuleCapabilityRegistry.cs#L114)와의 관계다. 현재 module 목록을 Require해 후보를 만드는 방식은 오타 행이 있을 때 복구 선택창도 중단시킨다. combat/management registry를 원본으로 후보를 제공해야 한다. 현재 pair/forbidden 목록이 일부 비어 있지 않아 공용 검사가 간접 예외로 중단할 수 있으며, 항상 clean 우회한다고 표현하지 않는다. skill/acquired/facility formula의 formatter/applicator는 해당 owner capability의 mirror다. parameterRanges.parameterId와 equipment/공유 appliedParameterIds는 magnitude/duration/count/targetCount의 닫힌 축 관계이고, [장비 statId/applicatorId](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L39)는 허용 조합을 선택하는 관계다. 이미 존재하는 엄격한 ToRuntime/Require 검사는 유지하면서 mirror는 파생/읽기 전용, 관계는 선택으로 작성하게 한다. equipment formatterId는 실제 formatter dispatch 없이 frozen metadata로만 전달되므로 외래키 누락으로 세지 않는다. 새 SO를 축마다 만들 필요는 없다.
- **R19-06/07:** [ownerHubTag](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L32)는 기존 ServiceHub 후보를 재사용할 수 있다. [mode.requiredFeatureTags](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L20)는 [현재 session 검사](Assets/Scripts/Services/ServiceRooms/ServiceSessionRuntime.cs#L1231)가 읽는 hub 목록과 분리되어 있다. 실제 선택 mode의 process 요구와 hub 요구를 한 경로로 검사하거나 폐기된 필드를 정리해야 한다. 빈 현재 목록을 근거로 앞으로도 안전하다고 하지 않는다.
- **R19-08/09:** [CropEcology의 suffix 선택](Assets/Scripts/Services/Economy/CropEcologyRuntime.cs#L352)과 [certified seed의 직접 참조](Assets/Scripts/Services/Economy/CertifiedSeedSpecialThroughputContributor.cs#L188)를 같은 기본 genome 권위로 결합한다. [촉매 feature](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L520)는 [ID parser](Assets/Scripts/Services/Combat/EquipmentEvolutionContracts.cs#L160)의 실제 결과와 검증하거나 한쪽을 파생시켜야 한다. 현재 등록된 다른 cultivar 선택과 유효 범위 feature 변경만으로도 두 권위가 달라질 수 있다.
- **R19-10:** [required metric key](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L208)는 [진화 검사](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionService.cs#L865)가 조회하는 관계다. 없는 key를 0으로 보는 의미는 정상 key가 해당 방에 없는 경우와 전역에 존재하지 않는 오타를 구별하지 못한다. 실제 room producer와 contribution 정의에서 후보를 얻고 전역 미등록 key를 조기에 거절한다. FacilityEvolutionValue.key는 새 metric을 정의하는 provider일 수 있으므로 똑같이 잠그지 않는다.
- **R19-11:** [메타 nested effect.id](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L316)는 [생성자](Assets/Scripts/Services/Run/AuthoredGameplayCatalog.cs#L464)에 전달된다. 생성자는 공란만 거절하지만 [소비자](Assets/Scripts/Models/Meta/Core/MetaProgressionEffects.cs#L95)는 MetaUpgradeEffectIds와 integer/multiplier interface를 exact match한다. 종류별 코드 target 선택과 검증이 필요하다. metaUpgrades[].id라는 자기 게시 identity와 혼동하지 않는다.
- **R19-12/13:** [시작 history](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L28)와 [origin](Assets/Scripts/Models/Characters/CharacterStartingOriginSO.cs#L28)은 내장 proficiency만 허용하지만 일반 후보는 전체 등록 정의다. 확장 허용 정책과 선택/소비 집합을 맞춰야 한다. [capacity 정의 검사](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L74)는 Enum.IsDefined가 빠져 있고, [실제 Get](Assets/Scripts/Services/Foundation/CharacterPerformanceContracts.cs#L234)은 미정의 값에서 예외를 낸다. 이미 enum 선택 UI가 있으므로 별도 reference framework는 필요 없다.
- **R19-14:** [CoreSession 연결 숫자](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L18)는 실제 enum 관계다. allowedIncidentKinds는 양수만 검사하여 99를 허용하고 [실제 사건 비교](Assets/Scripts/Services/Infrastructure/ExperiencePacingApplicationAdapter.cs#L55)는 exact int match한다. enum 선택과 정의 여부 검증을 쓰면 되며 저장된 숫자 의미를 바꾸지 않는다. serviceCategory/operationMode는 현재 범위 검사는 있으므로 이쪽은 수기 작성면 보완이다.
- **R19-15:** [BuildingWorkAmountAbility.defaultWorkerPolicy](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L153)의 [instance ID 목록](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L40)은 CharacterSO의 ID와 다르다. [새 건설 주문](Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs#L618)에 복사되고 [실제 작업자 필터](Assets/Scripts/Services/Character/Work/V23WorkerPolicyContracts.cs#L193)는 PersistentId로 비교한다. SO 초기 정의에는 정적 rule 조건을 두고 실행 인스턴스 선택은 기존 runtime UI가 소유하게 하거나, 별도 정적 지명 계약이 필요하다. CharacterSO를 고르는 것만으로 다른 ID 체계를 연결하지 않는다.
- **R19-18:** [생산 mass](Assets/Scripts/Models/Economy/Content/ProductionMassExplanationAuthoring.cs#L11)와 [cost](Assets/Scripts/Models/Economy/Content/ProductionOutputCostAllocationAuthoring.cs#L12)의 capabilityId/version은 기존 registry 관계다. snapshot의 정규 문자열 검사와 recipe catalog는 membership을 검사하지 않아 오타가 catalog를 통과한다. 실제 mass resolver와 다중 출력 cost resolver는 엄격히 거절하므로 silent 성공으로 세지 않지만, 오류가 실제 생산 경로까지 늦춰진다. 기존 registry에서 선택하고 version을 함께 기록하며 nonempty envelope를 catalog 조립 때 같은 resolver로 검사하면 된다. 정상 empty 계약을 없애거나 canonicalPayload 전체를 새 구조로 쪼개는 요구는 아니다.
- **R19-19:** [requiredTag](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L25)는 현재 [날씨와 보유 도구](Assets/Scripts/Services/Offense/OffenseStrategicExpeditionServices.cs#L586)에서 생산되고 [표시 전환](Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs#L1756)에 사용된다. tools의 생산 단절은 과거 수정으로 해결됐다. 남은 것은 후보/producer membership 작성 검사이며, 이 태그가 보상 효과를 바꾼다고 주장하지 않는다.
- **R19-20:** [wildlife.nestTag/migrationPatternId](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L54)는 projection 뒤 게임 동작의 reader가 없지만 ecology 관계 수에 포함된다. 현재 V20 species 8종이 이 비실행 필드 중 하나 이상에 기대어 gate를 통과한다. [침입 preferredFacilityFamilyIds](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L237)는 실제 family 내용이 아닌 [Count만 경보 식별에 사용](Assets/Scripts/Services/Invasion/InvasionIntruderSystem.cs#L554)한다. 현재 2개 pattern이 목록을 가진다. 실행 관계로 유지하려면 소비자를 연결하고, metadata라면 그런 계약으로 명시해 기능 연결 검사에서 제외한다. 무의미한 ID 선택기만 추가하지 않는다.

### 정상으로 구분한 범위와 남은 확인 한계

기존 R18 선택/검증은 실제 원본에 남아 있다. 종족과 해부 profile/node, 수술 family, trait 고정 규칙, 시설/장비 capability 금지 조합, 생산 workstation/support, service hub feature, habitat/room role/anchor 및 게시 nested ID 보호는 현재 값과 연결을 대조했다. 연구 prerequisite의 직접 SO와 저장 ID 동기화, membership/cycle 검사, faction arc의 chapter/contract 소유 세력 검사도 존재한다. faction arc picker가 다른 세력 SO를 보여주는 편의 문제와 이미 차단되는 잘못된 소유 관계를 새 무검사 단절로 중복 산정하지 않는다.

자유 provider 태그, 자기 identity, 실행 중 생성되는 instance/provenance key, 표시 문구는 별도 SO 관계가 아니다. 다만 닫힌 registry 조회 키나 소비 목록과 exact match하는 키를 이 이유로 제외하지 않는다. lexical icon/vfx key, recipeTags 같은 미소비 metadata를 실제 기능 완료로 세지도 않는다. offense의 human/sealed/hostile 같은 소유 문자열은 외교 faction SO 원장과 다른 문맥이므로 억지로 그 선택기에 연결하지 않는다.

R19는 현재 소스/에셋 대조 및 실제 Python 보호기 반례까지의 결과다. 현재 값이 정상인 필드에 새 native PASS를 붙이지 않았고, 모든 동적 확장과 임의의 미래 데이터에 대한 무결점 보증은 하지 않는다. 위 항목을 고칠 때는 해당 소비, 거절, 필요한 저장/복원/재시도를 표적으로 검증하면 되며 전수 seed나 무관한 전체 실행을 요구하지 않는다.

---

## R18-01~R18-16 수정 및 표적 실행 완료

수정 기준은 main `7b5003c6b6ee0b2d892edc471e10e0ea009e3432`다. 아래 원래 감사는 수정 전 상태이며 이 표가 이번 보완 결과다. 기존 GUID, 게시 ID, 저장 DTO 필드와 버전은 유지했다. 새 런타임 SO나 책임 분리용 클래스를 만들지 않고 기존 정의, registry, 소비자와 선택 경로를 보완했다.

| 항목 | 반영한 보완 | 실제 확인한 경계 |
|---|---|---|
| R18-01 | 작성된 효과5개를 마법 전투/원정 snapshot, 마나 tick, 경보 지연, 감지 반경과 작업 통증 계산에 연결 | 기존 실제 계산 소유자에서1.6/0.5/0.5/1.15/0.75 보정, JSON 복원, 마나 거절 후 재시도와 환불 무중복 |
| R18-02 | 효과 에셋을 자기 유효성 근거로 삼지 않고 선언된 코드 target 및 실제 capacity/proficiency 정의에서 선택 | 미등록 target 거절, 실제 Inspector Layout/Repaint, R18-01 실제 효과 소비 |
| R18-03 | 시설 nested capability6개를 기존 게시 identity 보호에 추가 | 기존 값 보존, C#/Python 경로 대조와 게시 키 변경/삭제 거절 |
| R18-04 | WorkType31개 코드 저장 ID의 이전 revision 비교 | rename/remove와 All 등록 누락 거절, 신규 ID 추가 허용 |
| R18-05 | 수술 family를 실제 anatomy profile에서 선택하고 family-only 조건도 검사 | 미등록 family 및 대상 profile0 거절, 수정 후 재검사, 실제 Inspector |
| R18-06 | lexicon family 선택과 실제 catalog 종족 전체의 라벨 검증 | Adventurer의 실제 family fallback 조회, 잘못된 family 거절과 복구 |
| R18-07 | 고정 특성9개 키를 한 계약에서 선택/검증하고 저장 rule108개를 게시 보호 | 타입/키/누락 거절, 일반 자유 규칙 허용, 게시 rule rename/배열 생략 거절, 실제 Inspector |
| R18-08 | 정책 ID와 version을 registry의 한 선택값으로 편집하고 조립 전에 검사 | root6 및 legacy6의 실제 trade/supply 견적, 잘못된 version/화물+None 거절, 무화물+None 허용, 실제 Inspector Undo |
| R18-09 | 시설 재료를 stock-category 등록 목록에서 선택하고 정의 시점 검사 | 유효 재료/수량 보존, 미등록 키 거절과 수정 후 재검사 |
| R18-10 | 시설 module을 실제 resultBuilding 적격성에 따라 선택하고 조기 거절 | 정확한 owner 후보, 다른 owner 전용 등록 module 거절과 복구 |
| R18-11 | 장비 module 관계를 기존 registry 목록 선택기에 연결 | 실제 후보 집합 비교, 기존 RequireCatalogModuleIds 거절 계약은 코드 대조 |
| R18-12 | recipe 작업장 및 지원 관계를 제공 능력 목록에서 선택 | 해당 작업장 지원만 노출, BatchProcessor 구분, 잘못된 조합 거절, 실제 Inspector |
| R18-13 | 서비스 hub/feature 관계를 실제 호환 제공자에서 선택 | 해당 hub 지원만 노출, 다른 hub 전용 feature 거절, 기존 실제 서비스 계약 실행 |
| R18-14 | 야생동물 habitat를 기존 parser의 닫힌 목록에서 선택 | 정확한 후보 집합, invalid 거절, 실제 Inspector |
| R18-15 | 방 색상 role 선택과 provider 조립 시 검증 | 미등록/중복 role 거절과 복구, 실제 Inspector |
| R18-16 | anchor purpose를 현재 확장 registry에서 선택 | 고정 복사본 대신 현재 등록값 조회, invalid 거절, 실제 Inspector |

실제 변경된 콘텐츠는 구형 세력6개에 누락됐던 정책12개 연결뿐이다. 같은 factionId의 현재 등록 정의와 같은 정책을 사용하며 원래 YAML 전체 prefix와 화물 수량을 보존했다. None을 잘못된 @0으로 보던 문제와 화물 없는 경로의 정상 미선택도 구분했다. published baseline에는 기존10607개 보호값을 보존하여 시설6개와 특성 rule108개만 추가했고 현재3544개 에셋/10721개 키다. 특성13개의 생략된 빈 identityRules 목록은 기존 Unity 초기화 의미를 유지하며 게시된 rule 삭제는 계속 거절한다.

[현재 R18 실행 증거](https://github.com/Creeper0809/DungeonStory/blob/886973b7e1aa3a3acf8af0de79f26af8a350e50b/Artifacts/QA/ImpactCI/r18-verification.json), [필수 변경 영향 실행](https://github.com/Creeper0809/DungeonStory/blob/886973b7e1aa3a3acf8af0de79f26af8a350e50b/Artifacts/QA/ImpactCI/verification.json), [월드 저장과 공식 복원](https://github.com/Creeper0809/DungeonStory/blob/886973b7e1aa3a3acf8af0de79f26af8a350e50b/Artifacts/QA/ImpactCI/world-round-trip.txt), [실제 Inspector 격리](https://github.com/Creeper0809/DungeonStory/blob/886973b7e1aa3a3acf8af0de79f26af8a350e50b/Artifacts/QA/ImpactCI/r18-inspector-isolation.json).

수용한 각 native 실행은 compiler0, consoleErrors0, source drift0이다. 실제 Inspector는 사용자 화면과 분리된 인증 private Xvfb에서 Layout/Repaint와 Apply/Undo를 실행했다. Python CI73개 및 pinned analyzer DSB001~DSB008 검증도 통과했다. 앞선 컴파일 실패, 구형 세력 누락 검출, Undo 정리 실패는 실패로 보존했다. 패키지의 실제 Undo 콜백에 따른 dirty 상태는 전체 JSON/hash 확인 후 원래 상태로 복구하고 전체 원본 SHA 검사에서도 무변경을 확인했다.

검증 범위는 현재 코드와 에셋의 참조, 통제된 실제 소비 계산, 실제 Inspector, 공식 월드 저장/복원 및 필수 영향 회귀다. 자연 AI가 모든 효과를 발동하는 플레이, 사람의 전체 UI 조작, 전수 seed와 장기 밸런스 인증을 수행한 것으로 세지 않는다. 과충전 수치 fixture는 실제 capability를 통제 호출하며 사용자 발동 UI의 새 전수 실행은 아니다. 일반 태그 정의와 새 identity 작성 자체는 허용하고, 기존 저장 join key와 닫힌 registry 관계를 보호한다.

R18-09~R18-16 leaf fixture는 후보 목록, catalog/정의 검증과 Inspector를 직접 실행한다. 각 항목별 창고 차감, 진화 prepare, 생산 cycle, service session, 계절 도착, 역할 색상 및 anchor 소비를 새 시나리오로 모두 실행한 것은 아니다. 기존 소비와 provider 연결은 원본을 대조했고 현재 공식 월드 및 영향 회귀 실행과 구분한다. 관계 선택기를 추가한 것을 해당 게임 기능 전체의 새 플레이 인증으로 표시하지 않는다.

실행 source `d698fc9aa0bed3cbbf664ea586dfa060132e6f186fddd0bd398d163ddcd2a092`, configuration `06a14e16fa1e125a1322b646be49a01566f0a4e5c100dc4c40555f599a693186`. GitHub 통합 결과는 현재 PR과 Goal의 실제 완료 기록을 따른다.

SO 작성 증거는 r130sr의 실제 PASS를 보존했다. 이후 변경은 runtime fixture 한 파일의 동등 부상도 설정, 분할 Tick 관측과 마나 측정 준비 순서뿐이다. 전체21660입력 비교에서 생산 코드/에셋/작성/Inspector 시나리오가 동일함을 확인한 재사용이며, 이전 실행의 source/configuration을 현재 것으로 바꾸지 않았다. 수정된 runtime과 Inspector, 필수 영향5그룹은 최종 입력에서 실행했다.

### 기존 효과 연결에 따른 수치 영향

`arcane:power`는 활성 과충전 중 마법 전투력에만1.6, `arcane:mana-recovery`는 후유증 중 마나 회복에0.5를 적용한다. 경보 지연0.5는 실제 대기 시간에, 위험 감지1.15는 기존 감지 수식 결과를 사용하는 반경에 적용한다. 원정의 마법 배율 snapshot에도 같은 결과를 동결한다. 기존 의미와 게시 ID를 보존하며 target 치환으로 다른 종류의 능력을 바꾸지 않는다.

`character:pain-work-penalty`는 기존 injury efficiency 곡선 `Lerp(1, 0.45, injurySeverity)`의 손실분을 보정한다. 통증 손실이 연결되므로 부상 노동자의 효율은 바뀐다. 부상도0.5에서 무특성0.725, 흉터0.75 보정 후0.79375다. 건강하거나 진통 억제 중이면1이며 장기 손상 자체의 capacity 감소는 별도다. 새 WU/EWU/가격/자원량이나 저장 필드를 추가하지 않는다. 이 변경의 표적 수치 및 복원 결과는 새 실행 증거로 판정한다. 과거 V27 밸런스 인증의 입력과 문서는 변경하지 않는다.

## 수정 전 main7b5003c6b 재감사: 당시 남은 문제16묶음

**수정 전 판정이다.** 기준은 당시 원격 main `7b5003c6b6ee0b2d892edc471e10e0ea009e3432`다. 기존 N01~N07 완료 여부와 별개로 현재 C# 선언, Inspector 작성 경로, 실제 소비자, 에셋 YAML, 게시 ID 보호를 다시 대조했다. 이번 결과는 **현재 콘텐츠의 효과 연결 누락 1묶음과 작성 또는 ID 변경 보호 누락 15묶음**이다. 16개 모두 현재 플레이가 망가졌다는 뜻은 아니다. 현재 값이 정상인 위험과 이미 작성된 효과의 연결 누락을 아래에서 구분한다.

아래는 R18-01~R18-16의 수정 전 감사다. 당시 재감사에서는 게임 코드와 에셋을 변경하거나 Unity를 새로 실행하지 않았다. 과거 N01~N07 실행 성공을 새 항목의 실행 증거로 사용하지 않는다. 기존 보고서에서 닫힌 registry 관계나 실제 exact-match 관계를 단순한 의미 키로 제외한 부분은 이 재감사로 정정한다.

### 현재 모집단과 검사의 한계

| 확인 대상 | 현재 결과 | 의미 |
|---|---:|---|
| 원본 게임 SO 종류 | 142 | 상속, 중첩 직렬화 레코드, SerializeReference 도달 필드 포함 |
| 직렬화 필드 선언 | 2,458 | 에셋 인스턴스별 필드 수와 다름 |
| 넓은 관계 후보 | 879 | 자기 ID, 자유 태그, 표시값도 포함하는 탐색 집합 |
| ContentReference 선언 | 180 | 표식이 없는 관계도 별도로 검토 |
| 실제 게임 SO 에셋 | 3,544 | YAML 파싱 실패 0, 추출된 게임 타입의 미게시 에셋 0 |
| GUID 형태 직접 참조 | 8,962 | script 등 다른 참조도 포함, 누락과 중복 metadata 0 |
| .asset 대상 fileID 참조 | 4,731 | 확인한 대상 anchor 누락 0 |
| Roslyn 구문 추출 | 오류 0 | Unity 컴파일 성공을 뜻하지 않음 |

직접 참조가 멀쩡해도 참조 대상이 실제로 소비되는지, 문자열 관계가 유효한지, 필수 필드가 None인지까지 증명되지는 않는다. 임포트된 모델 등의 모든 subasset fileID, 실제 Inspector 이벤트, 실제 플레이를 이번 정적 감사에서 실행한 것으로 세지 않는다.

현재 원본 자료는 `C:/Users/vulpo/Documents/Codex/2026-10-24/so-full-audit/main-reaudit-r18`에 보존했다. `inventory.json`, `current-assets.json`, `asset-groups.json`, `value-crosscheck.json`은 현재 필드와 에셋 대조 자료다. `actor-findings.md`, `content-findings.md`, `identity-findings.md`, `effect-target-check.json`, `effect-consumer-gap.json`은 아래 판정의 상세 근거다. 생성 지식베이스는 좁은 질의의 최신성을 확인한 뒤 탐색에만 썼으며 재생성하지 않았다.

### 우선순위와 현재 영향

P1은 먼저 연결하거나 보호해야 할 기능 및 저장 경계다. P2는 현재 데이터가 정상이고 주로 작성 실수 방지가 필요한 경계다. 검사가 이미 잘못된 값을 거절하는 경우에는 그 사실도 적었다.

| 번호 | 우선순위 | 남은 경계 | 현재 에셋 상태 |
|---|---|---|---|
| R18-01 | P1 | 특성 효과와 실제 계산 대상의 연결 누락 | 특성 4개에 작성된 효과 5개 |
| R18-02 | P1 | GameplayEffectDefinitionSO.targetId 수기 입력과 대상 검증 누락 | 정의 65개, 관련 문제는 R18-01에 합산 |
| R18-03 | P1 | 시설 진화 capability ID의 게시 및 저장 호환 보호 누락 | 6개 ID는 현재 정상 |
| R18-04 | P1 | WorkType 코드 ID의 이전 저장 키 변경 보호 누락 | 31개 ID는 현재 정상 |
| R18-05 | P1 | 수술 anatomy family 선택과 조건 독립 검증 누락 | 작성 29행, 현재 오류 0 |
| R18-06 | P1 | 신체 상태 lexicon family 관계와 실제 사용 종족 검사 누락 | 7개 lexicon, 현재 오류 0 |
| R18-07 | P1 | 코드와 정확히 일치해야 하는 특성 rule/need 키의 계약 검사 누락 | 해당 계약 9개, 현재 오류 0 |
| R18-08 | P1 | 세력 경제 정책 capability와 version 수기 입력 | 작성 12개, 현재 오류 0 |
| R18-09 | P2 | 시설 진화 materialId 선택과 정의 시점 검사 누락 | 현재 작성 0 |
| R18-10 | P2 | 시설 진화 evolutionModuleId 선택과 조기 검사 누락 | 작성 6개, 현재 오류 0 |
| R18-11 | P2 | 장비 진화 catalogModuleIds 수기 입력 | 작성 39개, 현재 검증은 정상 |
| R18-12 | P2 | 생산 workstation/support의 실제 태그 관계 수기 입력 | 대상 작업장 344개 recipe, 현재 오류 0 |
| R18-13 | P2 | 서비스 hub/feature의 실제 태그 관계 수기 입력 | hub 관계 23행, feature 요구 25행 정상 |
| R18-14 | P2 | 계절 야생동물 requiredHabitatId 수기 입력 | 작성 4개, 현재 검증은 정상 |
| R18-15 | P2 | 방 색상 roleId 선택 및 등록 검사 누락 | 현재 override 0 |
| R18-16 | P2 | 시설 anchor purposeId 수기 입력 | 작성 41개, 현재 오류 0 |

### R18-01: 현재 특성 효과 5개를 실제 소비 경로에서 조회하지 않는다

효과 SO와 binding의 GUID 연결은 정상이다. 그러나 [CharacterGameplayEffectProjector](Assets/Scripts/Services/Effects/Runtime/CharacterGameplayEffectProjector.cs#L28)는 요청한 target과 정의의 target이 정확히 같아야 적용한다. [CharacterPerformanceQuery](Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs#L448)는 각 formula의 `GameplayEffectTargetId`만 조회한다. 이전 연결 manifest에 소비자 이름이 적혀 있다는 것만으로 현재 연결을 인정하지 않았다.

| 현재 특성 에셋과 효과 | 작성값 | 실제 계산 경계 |
|---|---:|---|
| [Trait_306_arcane-overcharge](Assets/Resources/SO/V26/Traits/Founder/Trait_306_arcane-overcharge.asset)의 `arcane:power` | ×1.6 | [마법 전투력 formula](Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_arcane-power.asset#L36)는 `character:combat-power`를 조회 |
| 같은 특성의 `arcane:mana-recovery` | ×0.5 | [마나 회복 formula](Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_mana-recovery.asset#L40)는 `character:recovery-speed`를 조회 |
| [Trait_201_light-sleeper](Assets/Resources/SO/V20/Traits/General/Trait_201_light-sleeper.asset)의 `character:alarm-response-delay` | ×0.5 | [경보 대응 formula](Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_alarm-response.asset#L36)는 효과 target이 비어 있음 |
| [Trait_204_scarred](Assets/Resources/SO/V20/Traits/General/Trait_204_scarred.asset)의 `character:pain-work-penalty` | ×0.75 | [실제 작업 문맥 배율](Assets/Scripts/Services/Character/Core/CharacterStatsProjectionService.cs#L196)에 이 대상의 조회가 없음 |
| [Trait_205_keen-senses](Assets/Resources/SO/V20/Traits/General/Trait_205_keen-senses.asset)의 `character:danger-detection` | ×1.15 | [위험 감지 formula](Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_risk-detection.asset#L36)는 효과 target이 비어 있음 |

실제 소비 진입점도 확인했다. [CombatResolutionService](Assets/Scripts/Services/Combat/CombatResolutionService.cs#L1376)는 ArcanePower formula, [CharacterBodyHealthRuntime](Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs#L204)은 ManaRecovery formula, [CharacterAlarmResponseRuntime](Assets/Scripts/Services/Character/AI/CharacterAlarmResponseRuntime.cs#L583)은 AlarmResponse formula, [CharacterAiNaturalness](Assets/Scripts/Services/Character/AI/CharacterAiNaturalness.cs#L797)는 RiskDetection formula를 사용한다. [파생값 projector](Assets/Scripts/Services/Effects/Runtime/CharacterDerivedStatsSnapshot.cs#L700)에도 이 다섯 키를 새 키로 변환하는 경로가 없다.

따라서 현재 소스상 이 다섯 binding의 보정이 해당 계산 경로에 도달하지 않는다. 특성 전체가 작동하지 않는다는 뜻은 아니다. 같은 특성의 다른 효과, 과충전의 발동 비용이나 상태 규칙은 별도 경로다. 이번에 새 Play로 수치를 재현한 것은 아니므로 결과를 실제 플레이 PASS/FAIL 수치로 부풀리지 않는다.

최소 보완은 기존 의미와 게시 ID를 보존하여 실제 계산 소유자에 연결하는 것이다. 경보 지연 ×0.5와 대응 능력 ×0.5는 반대 효과가 될 수 있으므로 단순 target 치환은 안 된다. 마법 전용 보정이 모든 전투력을 바꾸거나 공통 회복 보정과 이중 적용되지 않게 해야 한다. 필요한 검증은 해당 특성의 활성/비활성 전후 실제 수치, 저장과 복원 후 동일 결과, 중복 적용 없음이다.

### R18-02: 효과의 targetId 자체는 여전히 일반 문자열이다

[GameplayEffectDefinitionSO.targetId](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L9)는 선택기와 관계 표식이 없다. [ValidateDefinition](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L53)은 빈 target을 거절하지만 실제 대상 존재나 소비 계약은 검사하지 않는다. [binding 검증](Assets/Scripts/Services/Foundation/GameplayEffectContracts.cs#L278)도 이 누락을 보완하지 않는다. 오타가 있는 비어 있지 않은 target은 게시 관계 검사에서 빠지고 projector에서는 조용히 적용 대상에서 제외된다.

현재 65개 중 14개는 functional capacity 정의 ID이고 9개는 proficiency ID에 `:starting-xp`를 붙인 관계다. 이 23개는 실제 정의와 일치한다. 나머지 42개는 코드의 효과 target 계약이며 R18-01의 다섯 키도 여기에 속한다. 모두 자유 의미 키라고 제외할 수 없다.

최소 보완은 기존 정의와 실제 소비 계약을 선택 권위로 사용하고, 신규 정의의 알 수 없는 target을 게시 전에 거절하는 것이다. 현재 `GameplayValueTarget` 후보는 효과 에셋 자체에서 추출한 일부 performance key라 이 필드에 그대로 붙이면 자기 자신을 근거로 유효하다고 판단할 수 있다. 새 SO를 key마다 만들 필요는 없다. 오타 거절, 새로 등록한 유효 target 선택, 실제 효과 적용을 표적으로 검증한다.

### R18-03: 시설 진화의 nested capability ID 6개가 보호 목록에서 빠졌다

[FacilityEvolutionRecipeSO.formulaCapabilities](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L43)의 `capabilityId`는 금지 조합 참조와 생성 결과의 저장 envelope에 사용된다. [저장 및 novelty 비교](Assets/Scripts/Services/FacilityEvolution/FacilityFormulaEvolutionAuthority.cs#L1024)도 같은 키를 쓴다.

그러나 [C# nested identity 수집](Assets/Editor/ContentIdentityProtection.cs#L670)과 [Python NESTED_ROUTES](tools/V27Balance/verify_content_identities.py#L99)에 시설 recipe가 없다. 장비 capability는 있지만 시설 capability는 누락이다. 현재 6개 recipe의 보호 row에는 상위 ID만 있으며 이 nested 키는 없다. AlchemyBench capability 문자열만 메모리에서 바꿔 추출한 결과도 전후 모두 빈 nested 집합이었다. 파일을 바꾸거나 전체 CI가 통과한다고 가정한 검증은 하지 않았다.

현재 여섯 ID는 정상이고 forbidden 목록은 비어 있다. 문제는 향후 capability와 관련 formula 문자열을 함께 바꾸면 기존 저장의 capability 정체성이 달라져도 게시 ID 검사에서 이 변경을 잡지 못한다는 점이다. 기존 C#/Python 경로에 같은 scoped nested 규칙과 6개 baseline 항목을 추가하면 된다. 순서 변경 허용, ID 변경 거절, 기존 저장 envelope 해석 유지가 필요한 표적 검증이다.

### R18-04: WorkType의 현재 등록 검사는 과거 저장 키 변경을 막지 않는다

[BuiltInWorkTypeIds](Assets/Scripts/Models/Work/WorkTypeId.cs#L40)의 31개 문자열은 [WorkTypeCatalog](Assets/Scripts/Models/Work/WorkTypeCatalog.cs#L88)가 정의하는 닫힌 코드 계약이다. SO에서의 선택과 현재 membership 검사는 N01로 보완됐다. 하지만 이 코드 상수는 SO identity baseline 밖이므로 이전 revision의 값과 비교하는 변경 보호가 없다.

[WorkAmountSystem](Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs#L2029)은 작업 종류를 저장하고 복원 때 다시 조회한다. 예를 들어 `work:craft`와 현재 SO 참조를 전부 새 이름으로 바꾸면 현재 콘텐츠 검사는 만족해도 과거 저장의 `work:craft`는 풀리지 않는다. [SettlementAlertRuntime](Assets/Scripts/Services/Character/Work/SettlementAlertRuntime.cs#L345)도 복원 시 미등록 작업을 거절한다.

이는 현재 끊어진 에셋이 아니라 코드 변경 시 저장 호환 보호 누락이다. 기존 검증 체계에 저장 프로토콜 ID의 append-only 비교를 추가하거나 명시적 구 키 migration을 요구하면 된다. 임의 이름 변경 거절과 이전 JSON 복원 유지만 검증하며 WorkType마다 새 SO를 만들지 않는다.

### R18-05: 수술의 anatomy family는 수기 관계이고 일부 작성 조합에서 검사를 빠져나간다

[SurgicalProcedureSO.allowedAnatomyFamilies](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L23)는 [AnatomyProfileSO.anatomyFamily](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L17)를 가리킨다. 실제 [수술 계획](Assets/Scripts/Services/Medical/SurgeryOrderPlanningService.cs#L136)은 exact family 비교로 대상을 제한한다.

[현재 문맥 검사](Assets/Scripts/Services/Items/ContentReferenceCatalog.cs#L387)는 target node가 비면 node 검사를 생략하고 allowed species가 비면 species 반복 검사도 하지 않는다. [procedure_amputation](Assets/Resources/SO/Medical/Procedures/procedure_amputation.asset)처럼 둘 다 비어 있는 정의에 family `humanoidd`만 작성하면 적격 profile이 0개여도 이 경로는 거절하지 않는다. 이후 수술 주문은 family 불일치로 거절된다.

현재 18개 anatomy profile은 12개 family를 정의하며 procedure 47개 중 family 작성은 29행이다. 현재 미정의 family와 적격 profile 0개인 procedure는 없다. 기존 family 작성 29건은 species도 작성돼 현재 문맥 검사에 걸릴 수 있지만, family만 쓰는 새 작성 경계는 비어 있다. 같은 profile 집합을 사용하는 family 선택기와 target/species 작성 여부에 독립적인 유효성 검사를 추가하고, 잘못된 family 거절 및 수정 후 정상 주문을 확인하면 된다.

### R18-06: lexicon family fallback의 실제 사용 대상이 검증에서 빠진다

[AnatomyConditionLexiconSO.anatomyFamily](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L38)도 profile family 관계지만 선택기와 존재 검사가 없다. [실제 lookup](Assets/Scripts/Services/Medical/AnatomyConditionLexiconCatalog.cs#L38)은 species가 없으면 family를 사용한다.

[Species_Adventurer](Assets/Resources/SO/Character/Species/Species_Adventurer.asset)는 직접 species 목록에 없어서 [condition_biological](Assets/Resources/SO/Medical/ConditionLexicons/condition_biological.asset)의 `humanoid` fallback에 의존한다. [검증의 RequiredSpecies](Assets/Scripts/Services/Medical/AnatomyConditionLexiconCatalog.cs#L7)는 고정 10종이고 Adventurer가 빠졌다. family를 잘못 바꿔도 직접 species 경로가 있는 10종 검사는 통과할 수 있으며 Adventurer의 [실제 상태 라벨](Assets/Scripts/Views/UI/CharacterDetailedStatsRuntime.cs#L646)은 missing 상태가 된다.

현재 7개 lexicon family는 모두 유효하다. R18-05와 같은 family 선택 권위를 재사용하고 실제 catalog의 종족과 profile에 대해 필요한 조건 라벨을 조회해야 한다. 고정 종족 상수에 하나만 추가하는 수정은 새 종족에서 다시 빠질 수 있다. family 오타 거절과 Adventurer 상태 라벨 lookup이 필요한 검증이다.

### R18-07: 일부 특성 rule/need ID는 일반 수기 ID가 아니라 고정 코드 계약이다

[CharacterTraitSO.identityRules](Assets/Scripts/Content/CharacterTraitSO.cs#L46)의 [ruleId와 needId](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L15)는 일반적으로 빈 값과 trait 내부 중복만 검사한다. 그러나 [ExtremeTraitRuleAccess](Assets/Scripts/Services/Character/Identity/Runtime/ExtremeTraitRuleAccess.cs#L59)는 trait ID, 구체 rule 타입, ruleId가 모두 일치해야 효과를 사용한다. trait 300도 [별도 exact lookup](Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityRuntime.cs#L1630)을 가진다.

trait 300~306의 7개 계약과 trait 213의 [의식 금식 need/rule 계약](Assets/Scripts/Services/Character/Identity/Runtime/CharacterRitualFastingRuntime.cs#L568), 합계 9개는 현재 값이 일치한다. 하지만 `extreme:last-stand`를 비어 있지 않은 오타로 바꾸면 일반 정의 검사는 통과하고 runtime은 rule을 찾지 못한다. 현재 113개 trait의 managed rule 108개 전체를 닫힌 목록으로 만들 필요는 없다. 코드가 특정 타입과 키를 요구하는 이 부분집합만 한 계약에서 선택 또는 읽기 전용으로 제공하고 게시 시 exact 검증하면 된다. 오타 거절과 해당 실제 효과 적용을 표적으로 확인한다.

### R18-08: 세력 경제 정책은 capability와 version을 직접 입력한다

[DungeonFactionDefinitionSO](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L53)의 trade/supply descriptor는 [FactionRouteEconomicPolicyRegistry](Assets/Scripts/Services/Factions/FactionRouteEconomicPolicy.cs#L66)에 등록된 `(capabilityId, capabilityVersion)`을 가리킨다. SO에는 선택기와 registry membership 검증이 없다. 오타나 잘못된 version은 실제 견적 생성 때 `등록되지 않은 세력 경로 경제 정책`으로 거절되어 경로를 만들지 못한다.

정책을 작성한 6개 세력의 12개 descriptor는 모두 등록값이다. descriptor가 없는 구형/호환 에셋 6개는 이 작성값 집계와 구분한다. 기존 registry의 versioned 목록을 선택 권위로 사용하고 실제 조립 시 descriptor를 확인하면 된다. 정상 trade/supply 견적, 잘못된 key/version의 조기 거절을 확인하며 경제 수치나 저장 ID를 바꿀 필요는 없다.

### R18-09: 시설 진화 재료 키는 잘못 입력하면 실제 차감 단계에서야 거절된다

[FacilityEvolutionMaterialRequirement.materialId](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L271)는 `StockCategoryPersistenceId`의 닫힌 키인데 선택기가 없다. recipe 정의 검사도 membership을 확인하지 않는다. [실제 자원 provider](Assets/Scripts/Services/FacilityEvolution/WarehouseFacilityEvolutionResourceProvider.cs#L251)가 진화 시도 때 parse 실패를 거절한다.

현재 6개 recipe의 requiredMaterials는 모두 비어 있으므로 현재 재료 참조 오류는 0개다. 기존 stock-category 선택 목록과 정의 검증을 적용하는 정도면 된다. 유효 재료의 차감/복원/재시도와 잘못된 키의 차감 전 거절이 필요한 좁은 검증이다.

### R18-10: 시설 진화 module ID의 registry 검사가 작성 시점보다 늦다

[FacilityEvolutionFormulaCapabilityDefinition.evolutionModuleId](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L194)는 EvolutionModuleRegistry 관계다. [RequireFormulaCapabilities](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L86)는 문자열과 중복, forbidden 관계를 검사하지만 module membership과 owner 적격성을 확인하지 않는다. 해당 검사는 [실제 진화 prepare/selection](Assets/Scripts/Services/FacilityEvolution/FacilityFormulaEvolutionAuthority.cs#L183)에서 수행한다.

현재 6개 값은 정상이다. owner recipe와 version에 맞는 기존 registry 선택기를 제공하고 조기 검증을 같은 경계에 연결하면 된다. 다른 owner의 module 거절과 수정 후 정상 생성만 표적으로 확인한다. R18-03의 capability ID 변경 보호와는 다른 문제다.

### R18-11: 장비 진화 catalogModuleIds는 검증되지만 여전히 수기 입력이다

[EquipmentEvolutionFormulaCatalogSO.catalogModuleIds](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L162)는 registry의 module ID다. 두 catalog에 20개와 19개, 총 39개가 있으며 현재 누락이나 중복은 없다. [RequireCatalogModuleIds](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L331)는 unknown, 잘못된 prefix, 중복, capability 포함 관계를 이미 명시적으로 거절한다.

필요한 보완은 현재 엄격한 검사를 유지하면서 owner/version별 등록 목록을 Inspector에 제공하는 것이다. 현재 기능 단절이나 silent fallback으로 세지 않는다. 선택 후보와 잘못된 값 거절의 표적 검증으로 충분하다.

### R18-12: 생산 recipe와 시설의 workstation/support 관계가 수기 태그다

[ProductionRecipeSO](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L32)의 `workstationTag`, `requiredSupportTags`, `batchSupportTag`는 시설의 workstation/support 능력과 실제로 매칭되는 값이다. 시설 측 `BuildingProductionWorkstationAbility.workstationTag`, `BuildingProductionSupportAbility.featureTags/compatibleWorkstationTags`까지 같은 작성 경계로 다뤄야 한다.

367개 recipe 중 작업장 대상이 있는 344개는 146개 workstation 정의에 모두 연결된다. 나머지 23개는 Source 19개와 Sink 4개다. required support 50개와 batch support 8개도 현재 누락 0이다. [현재 output-factor 검증](Assets/Scripts/Services/Economy/ProductionMaximumOutputFactorCatalog.cs#L254)은 support 누락을 명시적으로 거절한다.

정의 측 태그를 임의로 동결하거나 태그마다 SO를 만들 필요는 없다. provider를 선택 권위로 쓰고 소비 측에서 유효한 workstation/support를 선택하게 해야 한다. provider rename으로 매칭이 사라지는 경우도 같은 조립 검사에서 드러나야 한다. 대표 recipe 하나의 실제 작업장 및 지원 선택과 오타 거절을 확인한다.

### R18-13: 서비스 방의 hub/feature 태그도 실제 관계지만 선택과 교차 검증이 부족하다

[ServiceRoomAbilities](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L60)의 `serviceHubTag`, `compatibleHubTags`와 required feature는 [실제 서비스 적격성](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L130)에서 exact match한다. 현재 hub 관련 23행과 관리형/자동화형 required feature 25행은 provider에 연결된다. 별도 `supportedProcessIds` 8건은 이미 선택기가 있고 모두 실제 process 5개에 연결된다.

hub/feature 쪽은 process ID 선택기를 고쳤다고 함께 해결되는 관계가 아니다. 현재 값은 정상이며 관련 provider 목록 선택과 조립 시 호환 관계 검사만 추가하는 것이 최소 범위다. 현재 production support 검사와 같은 강도의 서비스 태그 그래프 검사는 확인하지 못했다. 대표 서비스의 정상 처리와 잘못된 hub/feature 조합 거절이 필요한 검증이다.

### R18-14: 계절 야생동물 habitat 키는 닫힌 값인데 수기 입력이다

[SeasonalWildlifeArrivalProfile.requiredHabitatId](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L18)는 `SeasonalWildlifeArrivalRules.TryParseHabitat`가 받는 값이다. 현재 작성 4개는 `grass` 2개와 `lair` 2개이며 종의 선호 habitat와 일치한다. V20StoryContentCatalog가 profile 검증을 호출하여 unknown은 이미 조립 단계에서 거절한다. 기존 허용 목록 선택기만 연결하면 되는 P2 작성 편의 문제이며, 현재 기능이 끊겼다고 세지 않는다.

### R18-15: 방 색상 override의 roleId 오타는 조용히 무시된다

[FacilityRoleColorOverride.roleId](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L14)는 FacilityRoleCatalog key인데 선택기와 unknown/duplicate 검사가 없다. [GetRoleColor](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L145)는 일치하는 override가 없으면 기본 role color를 사용하므로 오타와 의도한 기본색을 구별하지 못한다. 현재 override는 0개다. registry 선택기 및 중복/unknown 거절, 유효 override 한 개의 실제 색상 조회를 확인하면 된다.

### R18-16: 시설 anchor purposeId는 실제 등록 목적과 매칭하는 수기 키다

[FacilityAnchorSlot.purposeId](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L15)는 [FacilityAnchorPurposeCatalog](Assets/Scripts/Services/Buildings/FacilityAnchorPurposeCatalog.cs#L24)의 목적 키와 매칭된다. 현재 400개 건물 중 작성 slot 41개는 모두 `facility.work`로 유효하다. 잘못된 목적을 작성하면 정상 목적 조회가 해당 슬롯을 사용하지 못하고 일반 위치 fallback으로 흐를 수 있다.

기본 목적 선택을 제공하되 이 catalog의 `Register` 확장을 막으면 안 된다. 실제 조립 문맥에서 등록한 목적까지 선택/검증해야 한다. 기본 work slot, 등록한 확장 목적, 잘못된 키를 구별하는 표적 검증이 필요하며 anchor 구조 분리는 불필요하다.

### 별도 낮은 우선순위: 이미 보호되는 입력의 runtime 방어 중복

`SeasonalManaLightningProfile.eligibleBuildingDefinitionIds`는 이미 BuildingNumeric 선택기와 게시 관계 검사를 받는다. 현재 작성 1건 `9825`는 실제 BuildingSO에 존재한다. [runtime profile 검사](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L104)는 양수와 중복만 확인하고 [실제 적용](Assets/Scripts/Services/Run/SeasonalArcaneEventApplicationAdapters.cs#L113)은 목록 일치 건물만 선택한다. 게시 경로를 우회해 stale 숫자를 주입하면 대상이 조용히 없어질 수 있다.

정상 authoring에서 남은 수기 입력이나 무보호 ID로 세지 않으며 위 16묶음에도 포함하지 않는다. 외부 콘텐츠 주입을 지원해야 할 때 runtime catalog에도 존재 검사를 둘 수 있다는 방어 보완이다. 같은 검사를 모든 계층에 복제하는 작업을 우선 필수 수정으로 확대하지 않는다.

### 현재 정상으로 확인한 범위와 제외 이유

- 기존 N02 캠페인 선행 관계 5개는 같은 catalog 안에 존재하며 자기 참조와 순환이 없다. N03 로컬 drawback binding 1개, 일반 스킬 pair/forbidden 관계 8개도 현재 소유 문맥에 맞는다. N04 acquired와 N06 equipment/facility forbidden 및 N07 owner fixed skill의 실제 에셋 목록은 현재 비어 있는 경우를 빈 값으로 그대로 기록했다.
- N02 target ID 6개, N03 binding ID 9개, 스킬/후천 특성 capability 63개, 장비 capability 13개, 스킬 module 24개와 scoped variant 50개는 게시 identity 보호 경로가 있다. 시설 capability만 R18-03처럼 빠졌다.
- 현재 게시 baseline은 3,544개 에셋과 10,607개 보호 항목이다. catalog registration은 3,519행, 고유 GUID 3,517개이며 eligible game SO 3,516개는 자기 baseline row가 있다. 나머지 DOTweenSettings는 명시적 vendor 대상이다. 등록 삭제, 게시 게임 에셋 삭제/GUID 교체, startup root 경로 이동은 기존 Editor/CI가 거절한다. GUID를 유지하는 일반 등록 에셋 이동은 의도적으로 허용된다. 임의의 모든 direct reference 값을 다른 유효 에셋으로 바꾸는 행위까지 baseline이 동결하는 것은 아니다.
- Research unlock 180건, recipe research 367건, 숨겨진 compatibility prerequisite ID 310건은 현재 정의와 맞는다. prerequisite 권위는 object reference이며 숨겨진 ID와 일치 검사도 있다. 세력 chapter/contract/seasonal graph, 원정 site/encounter/weather, character species와 anatomy 및 생애 profile, 현재 수술 node/species와 wildlife prey/disease도 원본 에셋 대조에서 추가 단절을 찾지 못했다.
- BuildingSemanticTags의 `research:*` 117건 중 project ID와 다른 값 21개는 연구 catalog lookup을 하지 않는 개방형 semantic tag다. 이 차이를 끊어진 연구 참조로 보고하지 않는다. Wildlife migrationPatternId 18개는 같은 SO의 embedded profile을 설명하는 프로토콜 라벨이며 외부 pattern SO를 찾지 않는다.
- display name, 설명, 자유 그룹/분류 태그, 자기 identity, 저장 snapshot 문자열 전체를 관계 선택기로 바꾸지 않는다. 다만 실제 provider/consumer exact match나 닫힌 registry lookup에 쓰이면 R18-02 및 R18-07~R18-16처럼 별도로 판정했다.
- `BuildingLightingSettingsSO.targetSortingLayers`는 Unity sorting-layer 이름이며 SO 외래 키가 아니다. 현재 문서의 16묶음에는 넣지 않았다. Unity 프로젝트 설정과 실제 Renderer 계층 연결에 대한 전수 인증도 이번 결과에 포함하지 않는다.

이번 조사는 현재 main에서 확인한 잔여 범위를 정리한 것이며 모든 미래 콘텐츠와 실행 조합에 대한 무결점 보증이 아니다. 아래 N/R/U/I 완료표와 기존 전체 필드 부록은 각 당시 입력의 기록이다. 현재 잔여 판단은 이 R18 절을 우선하며, 완료표만 근거로 “수기 ID와 참조 단절 가능성이 전혀 없다”고 말할 수 없다.

## N01~N07 보완 완료

수정 기준은 main `1f1f990a2`다. 아래 재감사 기록은 수정 전 상태이며, 이번 보완의 결과는 이 표와 현재 소스에 결합된 실행 증거를 따른다. 기존 저장 ID, asset GUID, DTO 필드와 버전은 유지했다. 새 런타임 클래스나 SO 분할은 추가하지 않았다.

| 항목 | 반영한 보완 | 확인한 경계 |
|---|---|---|
| N01 | 작업 종류 7필드를 기존 등록 목록 선택기에 연결하고 소비 경계에서 미등록 값과 중복을 거절 | 생산, 화재, 급배수, 종족 적성, 우선순위 JSON, 실패 후 무변경과 수정 후 재시도 |
| N02 | 캠페인 선행 목표를 같은 SO 안에서 선택 | 미저장 새 행, 다른 SO 배제, 자기 참조와 순환 거절 |
| N03 | 불이익 효과 binding을 해당 모듈 안에서 선택 | 외부 binding 배제, 구조화된 누락 오류, 수정 후 재검사 |
| N04 | 공유 formula의 후보를 일반 스킬과 후천 특성 문맥에 맞게 분기 | 올바른 후보 집합, 잘못된 관계의 생성 전 거절, 수정 후 재시도 |
| N05 | 소비자가 없는 서비스 작업 ID 작성면 제거 | 관련 에셋 5개의 해당 키만 제거, 기존 서비스 실행 검사 |
| N06 | 장비와 시설의 금지 조합을 실제 조합 가능한 capability 목록에 연결 | 유효한 불이익 후보, 미등록 값 거절, 실제 조합 충돌 거절 |
| N07 | 고정 스킬의 모듈과 변형 선택, 생성 snapshot 읽기 전용, 실행과 복원 전 정합성 확인 | 실제 작업 효과, legacy JSON, 실패 전후 무변경, 복원 후 무중복 재시도, 원본 SO 무변경 |

[현재 SO 실행 증거](https://github.com/Creeper0809/DungeonStory/blob/7b5003c6b6ee0b2d892edc471e10e0ea009e3432/Artifacts/QA/ImpactCI/n01-n07-verification.json), [변경 영향 실행 증거](https://github.com/Creeper0809/DungeonStory/blob/7b5003c6b6ee0b2d892edc471e10e0ea009e3432/Artifacts/QA/ImpactCI/verification.json), [월드 저장과 복원](https://github.com/Creeper0809/DungeonStory/blob/7b5003c6b6ee0b2d892edc471e10e0ea009e3432/Artifacts/QA/ImpactCI/world-round-trip.txt), [실제 Inspector 격리 증거](https://github.com/Creeper0809/DungeonStory/blob/7b5003c6b6ee0b2d892edc471e10e0ea009e3432/Artifacts/QA/ImpactCI/n-so-inspector-isolation.json).

Unity 컴파일, 표적 authoring 실행, private Xvfb의 실제 Inspector Layout/Repaint, 필수 영향 5그룹을 통과했다. 모든 수용 실행은 compiler error 0, console error 0, source drift 0을 확인했다. Python CI 67개, 게시 identity 3544개 에셋과 10607개 보호 항목, analyzer DSB001~DSB008을 검증했다. 전수 시드, 장기 밸런스와 무관한 확장 인증은 실행하지 않았다. 초기 실패나 다른 소스 결과를 현재 PASS로 바꾸지 않았다.

실행 입력: source `72a4ca27a89469682d6e4d4b379c3c81d593d3664f5d5c8e85d3b2d67b338bbe`, configuration `ca67916ce8dde4a7f92660d364a30acfaded193341d81ffa6456d23e4e37cb3d`. GitHub 통합 상태는 현재 PR과 Goal 결과를 따른다. 아래 과거 R/U/I 검증의 원본 증거는 main `1f1f990a2`의 파일 이력에 보존된다.

## 수정 전 main 원본 재검토 — 당시 남아 있던 수기 관계

기준은 원격 main `1f1f990a26f457611d438eebd5423a92b4e77a51`이다. 조사 checkout의 HEAD `560cf45b30370006736adf9311a3d47fa77a0f18`와 main의 tree는 `c16083b71c1f16371032f3ede30d361f804f158e`로 같다. 재검토 마지막에도 원격 main SHA가 같음을 확인했다. 아래 기존 30항목 완료 표를 근거로 안전을 추정하지 않고 현재 C# 선언·Inspector 선택 경로·실제 소비자·에셋 YAML을 다시 대조했다.

**현재 값이 유효하다는 것과 수기 관계 입력을 없앴다는 것은 다르다.** 아래에는 수기 관계, 선택 대상이 잘못된 문맥, 작성값의 소비가 없는 경계를 구분했다. WorkType은 코드에 정의된 작업 종류이며, 로컬 관계는 같은 SO 내부의 중첩 ID를 가리킨다. 모두 별도 SO 객체로 바꿀 대상은 아니지만, 사용자가 ID를 직접 입력하는 관계라는 점에서는 남은 범위다. 이전 감사에서 이들을 단순 protocol 또는 이미 검증하는 중첩 관계로 제외한 것은 수기 입력 제거 요청에 비추어 부족한 판정이었다.

이번 재검토에서 확정한 보완 묶음은 **N01–N07의 7개**다. 권장 순서는 예외·기능 누락 위험인 N07/N01 → 잘못된 선택 대상과 미검증 관계인 N04/N06 → 검증은 있지만 수기인 N02/N03 → 소비 없는 작성면 N05다. 이 번호는 아래 과거 R/U/I 완료표와 별개다.

### N01 · P1 · 작업 종류 관계 7필드의 선택기·등록 검증 누락

기존 [ContentReferenceCatalog.cs](Assets/Scripts/Services/Items/ContentReferenceCatalog.cs#L117)는 `ContentReferenceKind.WorkType` 선택 목록을 이미 제공한다. 다음 필드에는 해당 표식이나 전용 선택기가 없다. [WorkTypeId.cs](Assets/Scripts/Models/Work/WorkTypeId.cs#L6)의 생성자와 `IsValid`는 비어 있지 않은지만 확인하므로 등록 여부 검사가 되지 않는다.

| 실제 필드 | 현재 에셋 대조 | 잘못 입력했을 때 소스상 영향 |
|---|---|---|
| [ProductionRecipeSO.workTypeId](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L41) | 레시피 367개, 고유 작업 ID 6개 모두 등록됨 | 미등록 ID도 getter를 통과한다. 시설의 `SupportsWork`가 거절하므로 레시피를 실행할 작업과 연결되지 않는다. |
| [BuildingProcessAccidentFireSourceAbility.workTypeId](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L262) | 전기 제련 도가니 1개, `work:operate` 유효 | 작업 문자열이 다르면 `CaptureFirePlan`이 검증 전에 return하여 공정 사고의 화재 결과를 건너뛴다. |
| [BuildingProcessFluidAbility.workTypeIds](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L166) | 18행, `work:cook`/`work:surgery` 모두 유효 | `Supports`가 false가 되어 시설의 공정 물·폐수 요구량이 0으로 투영된다. 레시피 자체 요구량은 별개로 유지된다. |
| [OffenseUrgentSiteDefinitionSO.mitigationWorkTypeId](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L17) | 긴급 거점 6개 모두 등록된 작업 | 대응 시설을 찾지 못해 새 명령이 시설 없음으로 거절된다. 현재 플레이가 무한 대기한다는 판정은 하지 않는다. |
| [WorkPriorityEntry.workTypeId](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L30) | CharacterSO 16개, 우선순위 465행은 모두 등록된 작업이며 자산 내 중복 0 | 문자열을 직접 역직렬화하며 등록 검사 없이 보존한다. 실제 작업 조회는 해당 행을 찾지 못해 작성한 우선순위 대신 그 작업의 기본 우선순위를 사용한다. |
| [CharacterSpeciesDefinitionSO.strongWorkTypeIds / weakWorkTypeIds](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L255) — 2필드 | 종족 11개의 45개 항목은 모두 등록된 작업이며 같은 종족의 강점/약점 중복 0 | 문자열 비교가 실패하면 작업 적성 보정이 0이 된다. 의도한 학습 배율과 AI 작업 선호 보정이 오류 없이 빠진다. |

소비 근거: [FacilityDefinitionData.SupportsWork](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L128), [WorkAccidentObservationRuntime.CaptureFirePlan](Assets/Scripts/Services/Character/Work/WorkAccidentObservationRuntime.cs#L39), [ProductionCycleUtilityService](Assets/Scripts/Services/Economy/ProductionCycleUtilityService.cs#L182), [OffenseUrgentMitigationRuntime](Assets/Scripts/Services/Offense/Strategic/OffenseUrgentMitigationRuntime.cs#L949), [WorkPriorityProfile.GetPriority](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L118), [CharacterSpeciesWorkAptitudeRules](Assets/Scripts/Services/Character/CharacterSpeciesWorkAptitudeRules.cs#L37).

보완 범위는 기존 WorkType 선택·검증 경로에 이 7개 선언을 포함하는 것이다. 새 작업 SO나 구조 분리는 필요하지 않다. 정상 에셋 값과 저장 ID도 바꿀 이유가 없다. 잘못된 ID의 조기 거절과 급배수/화재/우선순위/종족 적성의 대표 소비 경계가 표적 검증 대상이다.

### N02 · P2 · 캠페인 선행 목표는 여전히 로컬 ID 수기 입력

- 필드: [OffenseTargetDefinition.prerequisiteTargetId](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L180).
- 실제 에셋: [OffenseCampaignCatalog.asset](Assets/Resources/SO/Content/OffenseCampaignCatalog.asset#L60). 목표 6개와 비어 있지 않은 선행 연결 5개가 모두 같은 catalog의 `targets[].id`와 일치한다.
- 소비자는 선행 목표 완료 여부로 다음 목표의 접근 가능 여부를 결정한다. [기존 검증](Assets/Scripts/Services/Offense/OffenseCampaignCatalogSO.cs#L72)은 누락·자기 참조·순환을 거절한다. 현재 끊어진 연결이나 조용한 진행 단절로 세지 않는다.
- 남은 것은 같은 catalog 안의 목표를 고르는 선택기다. 별도 SO로 분리하거나 저장 ID를 변경할 필요는 없다.

### N03 · P2 · 후천 특성의 불이익 효과 연결도 로컬 ID 수기 입력

- 필드: [CharacterAcquiredTraitModuleSO.drawbackBindingIds](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L18). 같은 SO의 `effects[].bindingId`를 가리키므로 단순 의미 문자열이 아니다.
- 실제 에셋: [night-vigil 모듈](Assets/Resources/SO/V25/AcquiredTraits/Modules/acquired-trait_module_night-vigil.asset#L33)의 1개 연결은 현재 `effects`에 존재한다.
- [효과 소비자](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitEffectSource.cs#L69)는 이 목록으로 이익 효과에서 불이익 효과를 제외한다. [모듈 검증](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L151)은 잘못된 연결을 거절한다. 현재 값 결함은 아니다.
- 남은 것은 해당 모듈의 효과만 보여 주는 로컬 선택기다. 기존 binding identity와 저장 계약을 유지할 수 있다.

### N04 · P2 · 공유 formula 선택기가 후천 특성 문맥에서 다른 catalog를 사용

- 필드: [CharacterSkillPairSynergyCostDefinition.otherCapabilityId](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L96), [CharacterSkillCapabilityFormulaDefinition.forbiddenSynergies](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L120). 두 필드의 선택 종류가 `SkillCapability`로 고정되어 있다.
- 이 formula 타입은 일반 스킬뿐 아니라 [후천 특성 모듈](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L24)과 [후천 특성 불이익 정의](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L134)도 사용한다.
- [선택 목록](Assets/Scripts/Services/Items/ContentReferenceCatalog.cs#L127)은 소유자와 무관하게 일반 `CharacterSkillSystemSettingsSO.Modules`만 보여 준다. 반면 [후천 특성 catalog 검증](Assets/Scripts/Services/Character/AI/Editor/CharacterAcquiredTraitFormulaCatalogAssetBuilder.cs#L107)은 후천 특성 capability를 대상으로 금지 조합을 검사한다. 올바른 후천 특성 ID는 공통 선택/참조 검사에서 인정되지 않고, 선택기가 제안한 일반 스킬 ID는 후천 특성 catalog에서 유효한 조합이 아니다.
- 현재 모듈 8개와 불이익 23개의 pair/forbidden 목록은 모두 비어 있다. 현재 깨진 값은 없지만 해당 관계를 사용하는 새 콘텐츠 작성이 막히는 경계다. 기존 R23의 일반 스킬 검증 성공을 이 재사용 문맥의 성공으로 확장하지 않는다.
- 소유 SO/사용 문맥에 맞는 기존 catalog 선택과 검증이 필요하다. 공유 formula를 별도 클래스로 쪼개는 것이 완료 조건은 아니다.

### N05 · P2 · 서비스 작업 ID는 수기 작성면이지만 생산 소비가 없음

- [ServiceProcessSO.workTypeId](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L37)는 일반 문자열이다. 다만 [ValidateDefinition](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L99)이 `WorkTypeCatalog` 등록을 검사하므로 N01의 미검증 필드와는 구분한다.
- 서비스 프로세스 5개 중 [의료 프로세스](Assets/Resources/SO/ServiceRooms/Processes/service_medical_treat.asset)는 `work:treat`를 작성하고 나머지 4개는 비어 있다. 현재 값은 유효하다.
- 값을 읽는 `TryGetWorkTypeId`는 선언 외 C# 호출처가 없다. 현재 작성값이 실제 서비스 작업 연결을 제어한다는 근거가 없으므로 단순히 선택기만 붙여 완료할 항목은 아니다. 실제 서비스 작업의 권위와 대조해 이 작성면을 제거하거나 소비를 연결할지 결정해야 한다. 이번에는 수정하지 않았다.

### N06 · P2 · 장비/시설 진화의 금지 조합 2필드도 미검증 문자열 관계

- 필드: [EquipmentEvolutionFormulaCapabilityDefinition.forbiddenSynergies](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L49), [FacilityEvolutionFormulaCapabilityDefinition.forbiddenSynergies](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L172). 다른 descriptor의 `CapabilityId`와 비교하는 실제 관계이며 자체 ID나 일반 분류 태그가 아니다.
- 두 필드는 일반 문자열 목록이고 선택기가 없다. descriptor는 정규 문자열·중복·자기 참조만 검사하며, 장비/시설 catalog는 각 값이 실제 조합 대상 집합에 존재하는지 검사하지 않는다. 존재하지 않는 ID는 어느 대상과도 일치하지 않아 의도한 금지를 적용할 수 없다. catalog SHA 검사는 별도 보호이므로 단순 에셋 수정으로 모든 실행 검증이 통과한다고 주장하지 않는다.
- 실제 소비는 [NarrativeFormulaModuleSelectionValidator](Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaModuleSelection.cs#L245)의 선택한 이익/불이익 조합 검사다. 현재 단일 descriptor 수치 최적화에서는 이 pair 관계를 소비하지 않는다는 경계도 구분한다.
- 현재 장비 catalog 2개의 정의 13개, 시설 recipe 6개의 정의 6개는 모두 빈 목록이다. 관계 엔트리는 0개이며 현재 끊어진 값으로 세지 않는다. 미래에 작성할 때 필요한 선택/대상 존재 검사 누락이다.

### N07 · P1 · 사장 고정 스킬에 수기 관계와 생성 snapshot 전체가 노출

- 작성 루트: [CharacterSO.ownerFixedSkills](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L24). [CharacterSOEditor](Assets/Editor/CharacterSOEditor.cs#L29)는 이 private serialized 필드와 중첩 DTO를 재귀 `PropertyField`로 편집·저장한다. 읽기 전용 생성 결과가 아니므로 DTO라는 이유로 감사에서 제외할 수 없다.
- 실제 관계는 [CharacterSkillModuleSelection.moduleId / variantId](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L107)다. 전자는 스킬 settings의 모듈, 후자는 선택한 모듈의 변형을 가리키지만 선택기/소유 모듈 검증이 없다.
- [GetSkills](Assets/Scripts/Services/Character/Core/StartPartyPreparationSnapshot.cs#L146)는 표시명이 있는 수기 스킬을 채택하고 그만큼 기본 스킬을 덜 넣는다. 잘못된 모듈이면 [자동 효과 해석](Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs#L133)에서 null이 [Require](Assets/Scripts/Services/Character/Core/CharacterSkillModuleCapabilityRegistry.cs#L125)로 들어가 예외가 날 수 있다. 변형 ID만 틀리면 [legacy 효과량 계산](Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs#L849)은 값 0을 사용할 수 있다.
- `ruleId`는 생성 provenance이며 `combinationId`/`primaryCapabilityId`/`drawbackId`는 생성·formula의 파생 snapshot이다. 각자 SO 선택기를 붙일 대상은 아니지만 같은 Inspector에서 함께 수정할 수 있다. 작성할 입력과 생성 결과를 구분하고 전체 고정 스킬의 정합성을 한 경계에서 검사해야 한다.
- 현재 CharacterSO 16개 중 15개는 `ownerFixedSkills: []`, 구버전 TestCharacter 1개는 필드 누락으로 기본 빈 배열이다. 수기 스킬 인스턴스는 0개이며, 실제 기본 스킬 4쌍(`work_speed/relationship/revenue/mood` + `small`)은 현재 settings에 모두 존재한다. 현재 저장 손상이나 실행 예외를 관측했다는 뜻은 아니다.

### 이번에 확인한 정상 범위와 한계

- 현재 소스를 Roslyn Editor/Player 조건으로 다시 추출했다. 게임 SO 142종, 직렬화 필드 후보 2,459개, 넓은 관계 후보 880개, `ContentReference` 표식 167개다. 구문 오류 0이며 이 결과를 Unity 컴파일 성공으로 부르지 않는다.
- 실제 게임 SO 에셋 3,544개를 파싱하고 원본 `.cs.meta`/`.asset.meta`로 모집단을 교차 확인했다. partial SO의 MonoScript stub까지 포함하며, 게시 목록 밖 게임 SO와 파싱 오류는 0개다.
- nonzero 직접 GUID 참조 8,962건에서 대상 누락·중복 metadata는 0개다. 그중 `.asset` 대상 4,731건의 YAML fileID anchor도 존재한다. imported texture/animation 등 모든 subasset의 fileID나 필수 None 여부 전체를 이 수치로 보장하지 않는다.
- 현재 main을 대상으로 게시 identity 검사 도구를 새로 실행했다. 3,544개 에셋/10,607개 보호 항목이 게시 목록과 일치했다. 이 검사는 일반 문자열 관계가 모두 검증된다는 뜻이 아니다.
- Research 180개 프로젝트의 object/string 선행 참조 불일치 0, blueprint가 연결된 7개의 역방향 연구 ID 불일치 0이다. 조건부 encounter 목표와 authored effect 문자열도 실제 kind/소비 경로에 맞춰 구분했다.
- 개방형 시설 태그, 전투 목표 protocol, 표시 문구, 자체 identity, 런타임 인스턴스 ID를 별도 SO 참조로 오인하지 않았다. 예를 들어 Building의 `research:*` 태그 중 연구 SO ID와 다른 값은 일반 태그 소비 경로에서 쓰이므로 그것만으로 끊어진 연구 참조로 세지 않는다.
- `FestivalDefinitionSO.requiredBuildingDefinitionId`는 비어 있지 않으면 명시적으로 거절하는 폐기된 작성 경로이고 실제 시설 요구는 `venueRequirements`가 소유한다. `StatChange.needId`에는 별도 `CharacterNeedIdDrawer`가 있다. 진화의 `evolutionModuleId`/formatter/applicator는 등록을 확인하는 코드 구현 계약이며, 런타임 목적지·재고 분류 키도 각각 실제 소비 의미에 따라 SO 관계와 구분했다.
- 이번에는 게임 코드/에셋 수정, Unity 실행, Inspector 조작, 저장·복원 재실행, 생성 인덱스 갱신, commit/push를 하지 않았다. 위 오타 영향은 원본 코드 경로로 확인한 판정이며 새 Unity 재현 결과가 아니다.

새 증거는 `C:/Users/vulpo/Documents/Codex/2026-10-24/so-full-audit/main-reaudit-r17`의 `inventory.json`, `current-assets.json`, `current-identity-check.json`, `campaign-reference-check.json`, 분야별 보고서에 보존한다. 이전 실제 실행 증거와 수정 전 감사 출력은 덮어쓰지 않았다. 아래 30항목의 완료는 해당 수정 범위에만 적용되며, 이 재검토에서 확인한 잔여 경계의 완료를 의미하지 않는다.

## 보완 완료 — R01–R24 / U01–U05 / I01

30개 항목의 수정과 변경 영향 검증을 완료했다. 아래 수정 전 감사 기록은 당시의 결함·모집단을 보존한 자료이며 현재 미완료 목록이 아니다. GitHub 통합 상태는 `task_plan.md`와 `progress.md`의 최신 항목을 따른다.

기존3538개 SO의 GUID/type/script metadata와5857개 보호 필드는 값 변경·누락0이다. 새6개 해부 프로필과 필요한 보호 필드를 더한 게시 목록은3544개 SO/10607필드다. 기존 DTO 필드를 삭제하거나 저장 버전을 올리지 않았다. Inspector 선택과 등록 검증을 기존 경로에 붙였고, 사용하지 않는 작성면5종은 제거했다.

증거: [SO 표적 실행 영수증](Artifacts/QA/ImpactCI/so-verification.json), [변경 영향 실행 영수증](https://github.com/Creeper0809/DungeonStory/blob/1f1f990a26f457611d438eebd5423a92b4e77a51/Artifacts/QA/ImpactCI/verification.json), [실제 월드 저장·복원](https://github.com/Creeper0809/DungeonStory/blob/1f1f990a26f457611d438eebd5423a92b4e77a51/Artifacts/QA/ImpactCI/world-round-trip.txt), [Inspector 격리 증거](Artifacts/QA/ImpactCI/so-inspector-isolation.json). 최종 영향 실행 `r113sa`는 source `d9dff3485fe467653d986d00edefd3320544903da16585425ed9ab31bd47ec55`, configuration `6bacd40070886ee1391aa41a9f7e180e61ef163e87ac44ba73af996ca865d737`에 결합한다. SO 표적 검사는 실제 실행 source `9dfd54afcfbdc2be85b6387a731486772f245fc80862e22e6469fa62e2dbb090`를 그대로 기록한다. 두 입력의 차이는 새 특성을 복제하는 별도 Editor fixture 한 파일뿐이며, 게임 코드/에셋·SO 진입점/시나리오는 모두 같은 바이트다. 해당 fixture는 최종 effects에서 다시 실행했다. 재사용의 파일별 SHA 차이를 SO 영수증에 기록했으며 과거 결과를 새 소스의 실행으로 바꾸지 않는다. 실패·부분 실행은 PASS에 포함하지 않는다.

**A**는 `RunSoAuthoringContracts`, **R**은 `RunSoRuntimeContracts`, **I**는 `RunSoInspectorContracts`의 표적 실행이다. **P**는 실제 Unity 게시 후보와 기존 목록 보존/Python 검사의 조합이다. A/R은 통제된 production 객체·서비스 검증이며 I는 전용 가상 화면의 실제 `EditorGUI.PropertyField` 이벤트다. 월드/crop은 실제 Play 실행이다. 미래의 모든 콘텐츠 조합, 수동 클릭 UX 전체, 장기 밸런스의 증명을 뜻하지 않는다.

생성 색인의 `reference-gaps.md`에 새로 표시된 `AgeConditionDefinitionSO.anatomyTargets[*].nodeIds`7개는 전역 SO ID가 아닌 해당 해부 프로필 내부 키다. 일반 문자열 색인은 이 소유 문맥을 해석하지 못한다. 원본 프로필 대조와 R18의 실제7개 노드 피해 검증으로 연결을 확인했으며, 색인의 후보 표시를 지우거나 실행 PASS로 바꾸지 않았다.

| ID | 실제 수정 심볼 / 파일 | 확인한 경계 | 결과 |
|---|---|---|---|
| R01 | `BuildingFuelConsumerAbility.fuelItemId`, `BuildingGolemRechargeAbility.materialItemId`에 Item 참조 — `Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs`; 정확 fixture — `OffenseWorldMapDebugScenarios`/`SoReferenceRemediationLeafADebugScenarios` | **A** `SoReferenceRemediationLeafADebugScenarios.ValidateOrThrow` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R02 | `OffenseSpecificBlueprintRewardSpec.blueprintId`에 `FacilityBlueprintSO.id` 숫자 참조 — `Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs`; 6191/6192/6193 유지 | **A** `SoReferenceRemediationLeafADebugScenarios.ValidateOrThrow` | 완료 — 변경한 작성/등록 경계 A 확인; 변경하지 않은 개별 소비 UI 전체를 새로 실행한 것은 아님 |
| R03 | `AuthoredGameplayEffectRecord.textValue`의 `AuthoredEffectText` 문맥 해석, Species/InvasionPattern resolver — `Assets/Scripts/Content/GameDomainContentCatalogSO.cs`, `Assets/Editor/ContentReferenceDrawer.cs` | **A** `ContentReferenceSelectionDebugScenarios.RunAll` 및 root 관계 검사 | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R04 | `ContentIdentityProtection.CaptureNestedIdentities/CaptureNestedList`; `@nested:` selector/RID 검증 — `Assets/Editor/ContentIdentityProtection.cs`, `tools/V27Balance/verify_content_identities.py` | **A** `ContentReferenceIntegrityDebugScenarios.RunAll`; **P** candidate 후 Python CI | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R05 | `ContentIdentityProtection.CaptureRegistrations`; `@registration:` root/domain/item 등록 검증 — 같은 C# 및 Python verifier | **A** registration 제거 거절; **P** candidate 후 Python CI | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R06 | `V20StoryContentCatalog.ValidateFactionArcMembership`의 faction 소유·chapter 번호 중복/누락/범위 검사 — `Assets/Scripts/Services/Run/V20StoryContentCatalog.cs` | **A** `V20CampaignDebugScenarios.ValidateFactionArcMembershipForR06` | 완료 — 변경한 작성/등록 경계 A 확인; 변경하지 않은 개별 소비 UI 전체를 새로 실행한 것은 아님 |
| R07 | `ResearchPrerequisiteLinkDrawer`가 `prerequisite`와 `prerequisiteId`를 한 편집으로 동기화 — `Assets/Editor/ResearchPrerequisiteLinkDrawer.cs` | **A** `ResearchPrerequisiteLinkDrawerDebugScenarios.RunOrThrow` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R08 | `OffenseEncounterSO.objectiveTargetId`의 `EncounterTarget` 문맥 참조 및 CaptureLeader resolver — `Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs`, content-reference catalog/drawer | **A** `ContentReferenceSelectionDebugScenarios.RunAll` 및 root validation | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R09 | `AnatomySpecies` union, profile-local anatomy-node 선택/검증, slime procedure family `aberrant` 정합 — `SurgicalProcedureSO.cs`, `ContentReferenceCatalog.cs`, `SurgeryContentAssetBuilder.cs`와 4 assets | **A** selection/root 검사; **R** `SurgeryDebugScenarios.VerifyAnatomyProfiles` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R10 | fallback 없는 `ProfileId/LexiconId/RecipeId/TokenId`와 빈 신규 게시 거절 — `AnatomyProfileSO.cs`, `AnatomyConditionLexiconSO.cs`, `FacilityEvolutionRecipeSO.cs`, `FacilityEvolutionRecordTokens.cs`, `ContentIdentityProtection.cs` ; Python CI는 게임 SO 등록 대상의 자체 게시 row도 필수 요구 | **A** anatomy/facility name-independent identity fixtures; **P** publication candidate | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R11 | `@resource-path` 및 고정 `Assets/Resources/SO/GameContentCatalog.asset` 경로 보호 — `ContentIdentityProtection.cs`, Python verifier | **A** move/path 거절; **P** candidate 후 Python CI | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R12 | `BuildingEquipmentCraftingAbility.craftableEquipmentIds`에 Equipment + arrow/bolt aliases — `Assets/Scripts/Models/Buildings/Core/BuildingEquipmentCraftingAbility.cs` | **A** `ContentReferenceSelectionDebugScenarios.RunAll` 및 root validation | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R13 | `minimumSkillId` Proficiency, trait lists WorkerTrait; `CharacterTraitSO.LegacyPolicyNames`; stable/legacy `HasTrait` — `V23CraftingPrimitives.cs`, `CharacterTraitSO.cs`, `CharacterModelData.cs`; 100 alias assets | **A** `V23CraftingDebugScenarios.ValidateWorkerPolicyTraitReferencesForR13`; **P**/Python `@legacy-trait-name:` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R14 | `BuildingSpeciesAffinityAbility.preferredTags/dislikedTags`에 Species + IgnoreCase — `Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs` | **A** `SoReferenceRemediationLeafADebugScenarios.ValidateOrThrow` | 완료 — 변경한 작성/등록 경계 A 확인; 변경하지 않은 개별 소비 UI 전체를 새로 실행한 것은 아님 |
| R15 | `EnvironmentalWorkwearSO.allowedSpecies`에 Species + IgnoreCase — `Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs` | **A** `SoReferenceRemediationLeafADebugScenarios.ValidateOrThrow` | 완료 — 변경한 작성/등록 경계 A 확인; 변경하지 않은 개별 소비 UI 전체를 새로 실행한 것은 아님 |
| R16 | WeatherFront 참조, 도구 공급 태그 생성, `OffenseDecisionStateData.presentationTags` capture/restore — `OffenseDecisionCardSO.cs`, `OffenseStrategicExpeditionServices.cs`, `OffenseStrategicModels.cs`, travel/save validation | **R** `OffenseStrategicDebugScenarios.RunWeatherTravelFocused` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R17 | 18 anatomy profile 등록, `AnatomyProfileSO.LegacySpeciesIds`, 종족→profile 단일 매핑과 legacy restore — medical/species content 및 runtime | **R** `SurgeryDebugScenarios.VerifyAnatomyProfiles`; `WildlifeDebugScenarios.VerifyAuthoredAnatomyAndLegacyRestore` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R18 | profile별 age anatomy target/override와 `AgeConditionAnatomyProjectionRuntime` — `AgeConditionDefinitionSO.cs` 및 character runtime | **R** `AgeConditionAnatomyProjectionDebugScenarios.VerifyAuthoredProfileTargetProjectionFocused` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R19 | authoring 권위 `WildlifeSpeciesSO.preySpeciesIds`; provider의 `Hunts` 기반 reverse 파생; runtime reverse gate 제거 — wildlife SO/models/provider/runtime/builder 및 18 assets | **R** `WildlifeDebugScenarios.VerifyPredationAuthority` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R20 | `CharacterSO.OnValidate`와 `CharacterSOEditor`의 species 객체 단일 편집·derived `speciesTag` — `CharacterSO.cs`, `Assets/Editor/CharacterSOEditor.cs` | **A** `CharacterSOEditor.RunAuthoringAudit` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R21 | `DiseaseDefinitionSO.vaccineItemId`; `DiseaseFieldResponseRuntime.TryResolveVaccineItem`의 명시 아이템 해결/unknown 실패 | **R** `DiseaseFieldResponseOutboxDebugScenarios.RunFromMenu` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R22 | consequence key authoring 검사와 16 key 실제 소비 연결; dryness/mana stability/cross-lineage fixture 포함 — `HeritableTraitDefinitionSO.cs`, deprivation/environment/reproduction/combat/work adapters, `RitualFastingA08DebugScenarios.cs` | **A** `VerifyHeritableTraitConsequenceAuthoring`; **R** `RunHeritableProjectionContracts` + `VerifyHeritableBluntDamage` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R23 | `CharacterSkillSystemSettingsSO.RequireFormulaCatalog`의 capability 존재·pair·forbidden 대칭 검사 — `CharacterSkillSystemSettingsSO.cs` | **A** `CharacterSkillCapabilityDebugScenarios.RunFormulaCatalogRelationScenario` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| R24 | performance formula의 GameplayValueTarget/WorkType 참조와 catalog guards — `CharacterPerformanceFormulaDefinitionSO.cs`, `CharacterPerformanceFormulaCatalog` | **A** `V27CharacterPerformanceDebugScenarios.ValidateFormulaReferenceGuards` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| U01 | 미사용 operator stat 목록 제거; `ProcedureOperatorRequirement.minimumWeightedScore` 유지 — `Assets/Scripts/Models/Medical/Core/SurgeryModels.cs` | **R** `SurgeryDebugScenarios.VerifyOperatorScoreThreshold` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| U02 | `BuildingAnesthesiaAbility.anesthesiaItemId`와 builder/M05 YAML 작성면 제거 — `BuildingSurgeryAbilities.cs`, `SurgeryContentAssetBuilder.cs`, M05 asset | 전용 없음; A/R compile·root 구성의 간접 경계 | 완료 — 미사용 작성면 제거; 정적 diff와 현재 컴파일·연관 실행 확인(전용 동작 테스트 아님) |
| U03 | `DefenseFacilityData.affinityTags`, builder 및 16 P1 asset 블록 제거 — `DefenseFacilityData.cs`, defense builder/assets | **A** `SpeciesFactionDefenseExpansionDebugScenarios.ValidateDefense` | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| U04 | `FactionArcDefinitionSO.relicItemIds`, builder 설정, 6 arc YAML 필드 제거 — `FactionArcDefinitionSO.cs`, story builder/assets | **A** R06 faction-arc 시나리오의 남은 계약 | 완료 — 아래 A/R/P 중 해당 증거 확인 |
| U05 | `BribeOffer.factionId`와 builder 대입 제거 — `OffenseDecisionDefinitions.cs`, offense strategic builder | 전용 없음; R16 runtime 묶음은 bribe 전용 검사가 아님 | 완료 — 미사용 작성면 제거; 정적 diff와 현재 컴파일·연관 실행 확인(전용 동작 테스트 아님) |
| I01 | pending serialized owner 해석 수정; 실제 `ProductionRecipeSO.inputs[0].itemId` Layout/Repaint probe; `RunWithUndoIsolation` — `Assets/Editor/ContentReferenceDrawer.cs`, `ContentReferenceSelectionDebugScenarios.cs` | **I** `VerifyInspectorGUI`, private Xvfb + batchmode graphics | 완료 — 실제 Inspector GUI 5문맥, 입력·Undo·다중 선택 보존 |

### 최종 실행 결과

- `so-authoring`: 170초, native0/C#0/consoleErrors0/drift0.
- `so-runtime`: 270초, native0/C#0/consoleErrors0/drift0.
- `so-inspector`: 150초, native0/C#0/consoleErrors0/drift0.
- `core`: 190초, native0/C#0/consoleErrors0/drift0.
- `character`: 160초, native0/C#0/consoleErrors0/drift0.
- `effects`: 650초, native0/C#0/consoleErrors0/drift0.
- `crop`: 190초, native0/C#0/consoleErrors0/drift0.
- `world`: 250초, native0/C#0/consoleErrors0/drift0.

R04 수치 정정: 원래1157에는 anatomy attachment의 node 참조66개와 저장 identity가 아닌 domain record25개가 섞였다. 수정 전 보호 대상 identity는1066개다. 새6해부 프로필의 노드58개를 추가한 현재 대상은1124개(nested1116 + acquired top capabilityId8)다. 참조 필드를 identity로 오인해 동결하지 않는다.

R22 효과 변경 기록: 기존24개 유전 특성의 작성 배율과 capped modifier 상한을 유지하면서 미연결16행을 기존 소비자에 연결한다. 관절/정밀/이동은 기능 능력, 영양·회복·위험감지·작업속도는 기존 수식의 배율, 저온·고온·저산소·건조·마나 교란 내성은 해당 누적량의 역수 배율이다. 고밀도 골격은 갑옷 내구 소모를 바꾸지 않고 통과한 둔기 신체 피해만 감소시킨다. 룬시는 작업자가 있는 공명 조율 작업만 가속한다. 교차계통 내성은 해당 배양일 때 부모 중 낮은 계수를 적용한다. 마나 저장낭은 기존 기준 마나의 실효 용량을 늘리고 충전 비율을 유지하며, 회복량/소모는 실효 단위로 처리한다. 아이템/재고/비용/해금/저장 버전을 추가하지 않는다. 저장 배율 중복·실패 후 부분 변경은 표적 검증 대상이며 장기 밸런스 인증을 뜻하지 않는다. 과거 V27 전체 밸런스 인증 문서·증거는 재작성하지 않는다.

## 수정 전 감사 기록 — 결론과 읽는 방법

현재 SO 관계는 모두 안전하게 처리된 상태가 아니다. 이전 감사는 `[ContentReference]` 149개 선언과 현재 값의 유효성에 크게 의존해, 표식이 없는 관계·종류별로 의미가 달라지는 일반 문자열·중첩 저장 키·두 입력의 불일치를 놓쳤다. 이전의 “전부 처리했다”는 표현을 철회한다.

이번 문서는 발견 목록만 모은 보고서가 아니다. 게임 소유 SO 전체 타입 목록, 중첩·상속·다형성 필드 목록, 관계 종류별 판정, 현재 에셋과 향후 편집 위험, 원본 위치, 수정 후 필요한 검증을 한 파일에 묶는다. 아래의 타입/필드 목록 추출 완료와 실제 플레이 실행 성공을 혼동하지 않는다. 모든 미래 변경의 무결성을 수학적으로 증명한 문서는 아니다.

기준: `HEAD 22d77a6e804574abcdab7fc0ee94942a56336cba`, `origin/main 5476a748eacb99fc278dee9bcd45419017a512d1`, 두 tree 동일 `e7d00d62cd55a163e66f6b6551aa48716a66fc48`. 게임 소스·에셋은 수정하지 않았다. Unity 실행·저장 파일 변경·인덱스 재생성·push도 하지 않았다. 원본 Documents checkout의 사용자 변경은 별개로 보존했다.

## 조사 방법과 모집단

1. 지식베이스의 `ContentReference` 조회가 fresh임을 확인한 뒤 실제 C#·에셋·소비자를 읽었다. KB는 탐색용이다.
2. Assets 아래 C#를 Roslyn으로 Editor/Player 두 조건에서 구문 분석했다. 구문 오류는 0개다. 표식 유무나 필드 이름의 `Id` 포함 여부를 SO 모집단에 사용하지 않았다.
3. 상속으로 SO를 찾고, 그 직렬화 대상 후보 필드에서 중첩 클래스·리스트·배열·`SerializeReference` 하위 구현으로 내려갔다. 직접 Unity 객체 참조는 별도 분류했다. 일반 문자열도 전부 후보에 포함했다.
4. 142개 게임 소유 타입은 이전 실제 Unity TypeCache 결과와 대조했다. Native 목록의 추가 1개는 vendor `BehaviorDesigner.Runtime.ExternalBehaviorTree`다. Assets 원본의 Editor fixture 20개와 vendor `DamageNumbersPro.Internal.DNPPreset`도 별도 식별했다. 게임 콘텐츠 타입과 합쳐 계산하지 않는다.
5. 2,462개 필드 선언을 추출했다. 문자열/문자열 컬렉션 635개, 참조 표식 149개, 자체 ID 표식 55개, 직접 Unity 객체 참조 59개다. 이 집합은 서로 겹친다. 넓은 관계 검토 후보는 880개이며 **문제 880개라는 뜻이 아니다**. 숫자·enum·Unity 값·중첩 컨테이너도 부록에 남긴다.
6. 필드 타입 해석 미해결 0개, 다중 타입 후보 0개다. SO partial의 다른 선언에는 추가 직렬화 필드가 없음을 확인했다. 게임 소스의 조건부 분기는 `UNITY_EDITOR`와 Editor test 조건을 구분했다. Unity가 해당 필드를 실제로 저장하는지까지 새 실행으로 증명한 수치는 아니며, 구문상 도달 가능한 후보를 보수적으로 포함한 수치다.
7. 직접 참조, ID 조회, 문맥별 선택, 복원 시 재조회, 게시 후 변경/삭제/등록 제외, 코드에서 조립하는 ID를 함께 검토했다. 현재 정상인 값도 작성 경로가 위험하면 별도 항목으로 남겼다.
8. Assets의 .asset.meta 4,509개를 원본 게임 SO script GUID와 대조했다. 게시 목록 밖의 추가 게임 SO 에셋은0개다. 게시3,538개 YAML은 모두 파싱했으며 타입별 개수 합도3,538개다.
9. 게시 에셋의 nonzero GUID 참조 토큰8,950개는 모두 단일 대상에 연결된다. 이 중 .asset 대상4,725건은 YAML fileID anchor도 확인했다. script/texture/animation 등 imported subasset의 fileID 유효성이나 필수 None 여부 전체를 이 수치로 보장하지 않는다.

조사 산출물 원본은 `C:/Users/vulpo/Documents/Codex/2026-10-24/so-full-audit`에 보존한다. 전체 필드 목록은 이 문서 부록에도 포함한다. 감사 보조 도구는 저장소 밖에서 실행했으며 Unity Editor를 시작하지 않았다.

## 우선 처리 순서

| 순서 | 항목 | 현재 상태 |
|---|---|---|
| 1 | R18 노화 대상 노드 / R17 종족 해부 매핑 / R19 포식 관계 | 현재 데이터에서 무노드7조합, 야생동물13종 매핑 누락, 명시 포식11관계 불일치 |
| 2 | R22 유전 특성 효과 키 / R16 도구 선택 태그 | 현재 작성 효과16행의 소비 부재, tools 표시 전환의 생산자 부재 |
| 3 | R04 중첩 저장 키 / R05 등록 유지 / R06 복합 장 번호 / R11 루트 경로 | 저장 복원·진행·시작을 끊을 수 있는 변경 보호 누락 |
| 4 | R01·R02·R03·R13·R14·R21 | 미표식·혼합·문자열 조립 관계. 현재 값이 유효해도 새 작성 오타를 허용 |
| 5 | R07·R20 이중 입력 / R08·R09·R12·R15·R23·R24 작성·검증 문맥 / R10 신규 게시 | 잘못된 연결을 숨기거나 정상 작성/확장을 방해하는 경계 |

확정 보완 묶음은 **R01–R24의24개**다. 필드 수·에셋 수·고유 결함 수를 서로 혼동하지 않도록 관련 문제를 묶었다. 별도로 미연결 작성 필드5종과 실제 Inspector 실행 미확인 경계1종을 남겼다. 필드 부록의 모든 문자열이 수정 대상이라는 뜻은 아니다.

## 확정 문제와 보완 대상

우선순위 P1은 저장 호환성·실제 기능 단절 가능성, P2는 조기 거절이 있으나 잘못된 입력이나 정상 작성의 실패를 허용하는 경계다. 현재 에셋 결함과 재발 위험을 각 항목에서 구분한다.

### R01 · P1 · 건물 연료와 골렘 충전의 실제 아이템 관계 누락

- 선언: [SurvivalBuildingAbilities.cs](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L69)의 `BuildingFuelConsumerAbility.fuelItemId`와 같은 파일89행의 `BuildingGolemRechargeAbility.materialItemId`.
- 실제 소비: [SurvivalFoodStockRuntime.cs](Assets/Scripts/Services/Survival/SurvivalFoodStockRuntime.cs#L133)133/459행의 물리 스택 선택·질량 조회, [SpeciesRuntime.cs](Assets/Scripts/Services/Character/SpeciesRuntime.cs#L542)542/591행의 충전 재료 선택.
- 빠진 보호: 표식이 없어 공통 관계 검사에서 제외된다. 건물 능력 검사는 이 두 아이템의 등록 여부를 확인하지 않는다. 틀린 값이 연료/충전 시점까지 남는다.
- 현재 값: 미표식 연료 능력6개는 모두 resource:log, 충전 능력1개는 resource:mana-crystal로 유효하다. 같은 이름 fuelItemId의 다른31개 값은 이미 표식이 있는 생산 지원28/전력 생산3개이며 이 결함에 합산하지 않았다. 현재 공급 장애를 실행 재현한 결과는 아니다.
- 수정/검증: 기존 Item 선택·등록 검증을 적용한다. 유효 재료 실행과 미등록 재료의 시작 거절을 확인한다. 클래스 분리는 필요하지 않다.

### R02 · P1 · 원정 특정 설계도 보상의 숫자 참조 누락

- 선언/소비: [OffenseWorldMapModel.cs](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L117)117행의 `OffenseSpecificBlueprintRewardSpec.blueprintId`,130행의 `blueprint.id` 비교와 [OffenseRewardContextResolver.cs](Assets/Scripts/Services/Offense/OffenseRewardContextResolver.cs#L196).
- 보상 하위의 다형성 객체에 들어 있는 실제 FacilityBlueprintSO 관계다. 카탈로그는 대상·선행관계를 검사하지만 이 설계도 존재는 검사하지 않는다. 오타는 보상 후보 0개를 만든다.
- 현재6191/6192/6193은 존재한다. 기존 숫자 저장값을 유지하며 선택기와 교차 검증을 붙이고 실제 보상·저장·재시도에서 중복 없음을 검증해야 한다.

### R03 · P1 · `textValue`에 숨은 종족/침입 패턴 관계

- 선언: [GameDomainContentCatalogSO.cs](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L313)의 `AuthoredGameplayEffectRecord.textValue`317행. `GuestDemandMultiplier`일 때 종족 ID, `IntruderPattern`일 때 같은 SO 내부의 침입 패턴 ID다.
- 소비: [AuthoredGameplayCatalog.cs](Assets/Scripts/Services/Run/AuthoredGameplayCatalog.cs#L504)506/518행, [RunVariableEffects.cs](Assets/Scripts/Services/Run/RunVariableEffects.cs#L18)18–35/124–140행. 방문객 생성의 실제 호출은 [CharacterSpawner.cs](Assets/Scripts/Services/Character/CharacterSpawner.cs#L352).
- 오타 결과: 종족은 배율1로 조용히 무효화된다. 침입 패턴은 [InvasionIntruderContentBinding.cs](Assets/Scripts/Services/Invasion/InvasionIntruderContentBinding.cs#L16)의 `catalog.Get(id) ?? catalog.Default`로 기본 패턴으로 대체된다. 정의 검사는 null/None 종류만 거절한다.
- 현재 종족3개(Slime/Orc/Vampire), 침입 패턴5개(hunter/ambusher/breaker/plunderer/straggler)는 유효하다.
- 수정/검증: 종류에 맞는 선택과 등록/지역 목록 검사를 기존 필드에 적용한다. 잘못된 종족이 기본 배율, 잘못된 패턴이 Hunter로 숨겨지지 않는지 표적 검증한다.

### R04 · P1 · 중첩 키와 삭제가 저장 호환성 보호에서 제외됨

- 게시 캡처는 [ContentIdentityProtection.cs](Assets/Editor/ContentIdentityProtection.cs#L595)의 SO 최상위 `[ContentIdentity]` 필드만 수집한다. [verify_content_identities.py](tools/V27Balance/verify_content_identities.py#L257)도 단일 최상위 필드 경로만 허용한다.
- 확정 사례: [AnatomyModels.cs](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L77)의 `nodeId`, [OffenseDecisionDefinitions.cs](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L22)의 `choiceId`. 저장은 해당 값을 보존하며, [CharacterBodyHealthStateRules.cs](Assets/Scripts/Services/Combat/CharacterBodyHealthStateRules.cs#L1147)와 [OffenseAggregateSaveValidation.cs](Assets/Scripts/Services/Offense/OffenseAggregateSaveValidation.cs#L169)는 현재 정의에서 찾지 못하면 복원을 거절한다.
- 이전 좁은 반례에서는 두 값을 GitSnapshot 메모리 blob에서만 변경해 공식 Python 검증기에 넣었고 검사가 통과했다. 이는 검증 누락의 증거이며 실행 성공 증거가 아니다.
- 중첩 키10계열, 현재1,157개 작성 값을 대조했다. 아래 표에 저장 소비와 보호 누락을 구분한다. 목록의 모든 순서 변경이 저장 파손이라는 뜻은 아니다. 안정 키를 유지해도 의미를 바꾸는 변경은 해당 저장 계약에 따라 별도로 판단해야 한다.
- 수정/검증: 실제 저장·재시도에서 참조하는 키만 경로/소유자와 함께 보호하거나 명시적 호환 이관을 둔다. 표시 문구나 의미 없는 모든 문자열까지 동결하지 않는다. 과거 저장을 고정한 변경·삭제 거절 및 복원·재시도 검증이 필요하다.

### R05 · P1 · 파일 보존만으로는 카탈로그 등록 유지가 보장되지 않음

- [ContentIdentityProtection.cs](Assets/Editor/ContentIdentityProtection.cs#L59)는 Assets 전체의 파일을 수집한다. `GameDomainContentCatalogSO.definitions`에서 게시 정의를 제거해도 파일과 ID가 남으면 baseline은 동일하다.
- 실제 조회는 [GameDomainContentCatalogSO.cs](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L36)의 현재 목록만 사용한다. 과거 trait 조회는 [CharacterProgressionProfileProjector.cs](Assets/Scripts/Services/Character/Core/CharacterProgressionProfileProjector.cs#L79)에서 실패할 수 있다.
- 메모리상 실제 카탈로그 항목을 제거해도 공식 identity 검증이 통과했다. 현재 다른 SO가 해당 정의를 참조하면 별도 관계 검사가 잡을 수 있으므로 모든 삭제가 모든 CI를 통과한다고 주장하지 않는다. 과거 저장만 참조하는 정의의 보호가 빠진 문제다.
- 수정/검증: 게시된 운영 등록과 의도된 보존 전용 자산을 구분하고 필요한 등록 유지/폐기·이관 계약을 검사한다. 과거 저장만 참조하는 항목의 등록 제거를 표적 검증한다.

### R06 · P1 · 세력 서사의 목록과 실제 복합 조회 키가 서로 다름

- [FactionArcDefinitionSO.cs](Assets/Scripts/Content/FactionArcDefinitionSO.cs#L8)는 chapterIds/contractIds/relicItemIds를 편집한다. 검사하는 것은 개수와 전역 존재 여부다([V20StoryContentCatalog.cs](Assets/Scripts/Services/Run/V20StoryContentCatalog.cs#L68)).
- 실제 장은 `(factionId, chapterNumber)`로 `Single` 조회한다([V20CampaignRuntime.cs](Assets/Scripts/Services/Run/V20CampaignRuntime.cs#L541), [V20ContentResolutionRequestPlanner.cs](Assets/Scripts/Services/Run/V20ContentResolutionRequestPlanner.cs#L410)). 계약 목록도 arc.contractIds가 아니라 [V20FactionContractQuery.cs](Assets/Scripts/Services/Run/V20FactionContractQuery.cs#L46)의 계약별 factionId에서 나온다.
- 같은 세력의 장 번호 중복·누락은 모든 ID가 존재해도 진행 시 예외를 만들 수 있다. 다른 세력의 존재하는 장/계약을 arc에 선택해도 소유 일치 검사가 없고 실제 조회와 달라진다. 기계적 효과 signature 중복 검사는 이 복합키 검사를 대신하지 않는다.
- 현재6개 arc/36개 chapter/24개 contract를 대조했다. 각 세력의1–6장, arc 장 목록과 소유 세력, arc 계약의 소유 세력은 모두 일치한다. 현재 데이터의 진행 장애를 주장하지 않는다.
- 수정/검증: 실제 조회를 단일 권위로 정하고 arc 목록과 소유/순서/중복을 일치 검증한다. 번호 중복·누락, 타세력 선택, 변경 전 저장의 currentChapter 복원을 확인한다. relic 목록은 능동 보상 소비가 확인되지 않아 아래 미연결 작성 데이터와 구분한다.

### R07 · P2 · 연구 선행조건 선택이 두 필드를 불일치시킴

- [ResearchProjectSO.cs](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L208)의 `ResearchPrerequisiteLink`는 prerequisiteId와 prerequisite SO를 모두 저장한다.
- 공용 선택기는 [ContentReferenceDrawer.cs](Assets/Editor/ContentReferenceDrawer.cs#L237)에서 ID만 쓴다. ID 선택을 A→B로 바꾸면 객체 참조A가 남는다. `IsValid`233행은 일치를 요구하고 런타임은40–51행에서 객체 참조를 사용한다.
- 정의 검사172–176행과 연구 카탈로그 검사가 거절하므로 조용한 잘못된 진행으로 단정하지 않는다. 정상 선택 UI를 썼는데 수동 동기화가 다시 필요한 작성 결함이다. 현재180개 연구/310개 인과 링크를 확인했으며 현재 불일치를 발견한 것은 아니다.
- 수정/검증: SO 선택을 한 번의 편집 권위로 만들고 호환 ID를 함께 갱신하거나 파생시킨다. 선택·None·Undo·다중 편집·기존 직렬화 복원에서 두 표현의 일치를 확인한다.

### R08 · P2 · 포획 목표는 여전히 적 ID를 직접 입력함

- [OffenseEncounterSO.cs](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L48)의 objectiveTargetId는 CaptureLeader에서 EnemyArchetypeDefinitionSO 관계다. [EnemyEncounterFactory.cs](Assets/Scripts/Services/Offense/EnemyEncounterFactory.cs#L401)가 실제 대상 개체를 선택한다.
- 현재36개 조우 중6개 포획 조우가 이 관계를 쓴다. `ValidateDefinition`100–105행은 enemies 목록에 대상이 있는지 이미 검사한다. 따라서 **수기 입력은 남았지만 대상 검사까지 없는 필드는 아니다**.
- ProtectTarget/SabotageTarget의 target:*는 로컬 목적물 키다. 필드 전체를 Enemy SO 선택으로 고정하면 이 정상 문맥이 깨진다. CaptureLeader일 때만 조우가 가진 적 목록으로 선택을 제한한다.

### R09 · P2 · 수술 종족/신체 노드 선택 범위 불일치

- [SurgicalProcedureSO.cs](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L24)의 allowedSpeciesIds 선택은 캐릭터 종족만 허용한다. [SurgeryOrderPlanningService.cs](Assets/Scripts/Services/Medical/SurgeryOrderPlanningService.cs#L246)는 실제 야생동물도 처리한다. `allowsWildlife=true`인 moss_boar 전용 수술을 정상적으로 작성할 수 없다.
- targetNodeId는 전체 해부 프로필 노드 합집합에서 선택한다. 인간용 수술에 construct의 power-core가 선택되고 전역 존재 검사를 통과하지만 실제 대상 노드 검사517–540행에서 거절된다.
- 현재47개 수술은 이 두 반례를 사용하는 작성 상태가 아니다. 확장 시 정상 연결이 거절되거나 잘못된 연결이 늦게 발견되는 문제다.
- 수정/검증: 대상 종류·종족·프로필을 기준으로 선택/검증 범위를 맞춘다. 야생종 전용 수술, 대상에 없는 노드 거절, 혼합 대상 수술의 의도된 범위를 확인한다.

### R10 · P2 · 신규 게시가 빈/유효하지 않은 자체 ID를 고정할 수 있음

- [ContentIdentityProtection.cs](Assets/Editor/ContentIdentityProtection.cs#L294)의 Append 메뉴는 기존 행 보존 후 새 정의를 추가한다. 새 정의의 유효성·운영 등록·초안 여부를 검사하지 않는다.
- 현재 `TestCharacter.asset`의 빈 archetypeId가 baseline에 포함됐다. [CharacterSpawnRequest.cs](Assets/Scripts/Services/Character/SO/CharacterSpawnRequest.cs#L18)의 명시적 archetype spawn 계약에서는 잘못된 값이다. 다만 해당 씬의 legacy 숫자 ID 경로가 있어 현재 게임이 즉시 실패한다고 단정하지 않는다.
- AnatomyProfile(12), AnatomyConditionLexicon(7), FacilityEvolutionRecipe(6), FacilityEvolutionRecordTokenDefinition(9)은 빈 ID일 때 asset.name을 쓰므로 빈 값 그대로 게시한 뒤 이름을 바꾸면 raw identity 보호를 통과하면서 실효 ID가 달라질 수 있다. 현재4종류34개는 모두 명시 ID가 있어 이 rename 반례의 현행 결함은 아니다. 근거: [AnatomyProfileSO.cs:29](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L29), [AnatomyConditionLexiconSO.cs:43](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L43), [FacilityEvolutionRecipeSO.cs:52](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L52), [FacilityEvolutionRecordTokens.cs:28](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L28).
- 수정/검증: 신규 게시 전 타입의 실제 identity 유효성과 게시 의도를 검사한다. legacy 예외를 일반 신규 규칙과 섞지 않고, 빈 ID의 게시 및 이름 변경으로 ID가 변하는 경계를 확인한다.

### R11 · P1 · 시작 카탈로그의 고정 경로는 이동/이름 변경 보호 밖임

- [ResourcesAssetLoader.cs](Assets/Scripts/Services/Infrastructure/Core/ResourcesAssetLoader.cs#L16)는 `SO/GameContentCatalog`를 고정 경로로 읽는다. [ContentReferenceDrawer.cs](Assets/Editor/ContentReferenceDrawer.cs#L214)도 Editor에서 동일 에셋의 고정 경로를 사용한다.
- GUID를 유지한 채 이 에셋과 meta를 이동한 메모리 스냅샷은 공식 identity 검사에서 `1 asset / 0 fields`로 통과했다. 현재 경로는 정상이다. 실제로 이동하면 시작 로더가 실패하고 선택기 목록도 사라진다.
- 일반 SO 이동은 GUID 참조로 안전하다. 이 시작 에셋만 경로 계약이 별도이며, [ContentIdentityProtection.cs](Assets/Editor/ContentIdentityProtection.cs#L812)는 저장/삭제를 다루지만 이 경로의 이동을 막지 않는다. **identity 검사 통과를 전체 CI 통과라고 확대하지 않는다.**
- 수정/검증: 기존 시작 방식에 맞춰 이 경로를 검사하거나 별도 안정 참조를 쓴다. 이동·rename 반례와 정상 시작만 표적 검증한다.

### R12 · P2 · 제작 허용 목록은 장비/탄약 혼합 ID를 수기로 입력함

- [BuildingEquipmentCraftingAbility.cs](Assets/Scripts/Models/Buildings/Core/BuildingEquipmentCraftingAbility.cs#L16)의 `craftableEquipmentIds`는 장비 SO 61개와 코드가 정의한 `craft:ammo:arrow`, `craft:ammo:bolt`를 함께 가리킨다. 현재 63개는 모두 유효하다.
- [CombatCraftDefinitionCatalog.cs](Assets/Scripts/Services/Combat/CombatCraftDefinitionCatalog.cs#L297)는 등록 시 존재를 엄격하게 검사한다. [EquipmentCraftingPanelPresenter.cs](Assets/Scripts/Views/Buildings/UI/EquipmentCraftingPanelPresenter.cs#L74)와 작업 핸들러가 실제 목록을 소비한다.
- 누락은 작성 선택기다. 전부 Equipment SO로 제한하면 정상 탄약이 배제된다. 기존 혼합 제작 카탈로그에서 고르게 하고 현재 시작 검증을 유지한다.

### R13 · P1 · 작업자 정책의 숙련/특성 관계와 표시명 별칭

- [V23CraftingPrimitives.cs](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L42)의 `minimumSkillId`, `requiredTraitIds`, `excludedTraitIds`는 BuildingSO 초기 정책에서 편집 가능하다. 숙련은 Proficiency, 특성은 유전 특성 ID와 일반 특성 ID/표시명이 섞인다.
- [V23WorkerPolicyContracts.cs](Assets/Scripts/Services/Character/Work/V23WorkerPolicyContracts.cs#L225)225–242행이 읽고, [WorkerNarrativeQualificationQuery.cs](Assets/Scripts/Services/Character/Work/WorkerNarrativeQualificationQuery.cs#L28)28–68행이 조회한다. 숙련 오타는 XP 0/기본 등급으로 취급되며, 필수 특성 오타는 작업자 거절, 제외 특성 오타는 제외 실패로 이어진다.
- [CharacterModelData.cs](Assets/Scripts/Services/Character/SO/CharacterModelData.cs#L309)의 특성 비교는 stable ID뿐 아니라 `traitName`도 허용한다. 표시명을 수기로 저장한 정책은 표시명 변경에 취약하다.
- 현재 게시 건물의 이 세 목록/값은 비어 있으며 이 반례의 현행 오타를 발견한 것은 아니다. 제작 UI가 만드는 crafting 정책은 별도의 정상 경로다.
- 수정/검증: 혼합 특성의 실제 정의를 선택하고 저장에는 안정 ID만 쓴다. 과거 이름 별칭이 저장된 정책은 호환 변환을 정의한다. 선택·작업자 필터·정책 저장/복원과 표시명 rename을 확인한다.

### R14 · P1 · 종족 선호/기피의 `Tags` 이름이 SO 관계를 숨김

- [BuildingCoreAbilityDefinitions.cs](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L51)의 `BuildingSpeciesAffinityAbility.preferredTags/dislikedTags`는 임의 태그가 아니다.61–75행에서 종족 문자열과 직접 비교하고, [FacilityCandidateScorer.cs](Assets/Scripts/Services/Character/AI/FacilityCandidateScorer.cs#L1280)1280–1298/1605–1618행이 `actor.SpeciesTag`를 전달한다.
- CharacterSpecies SO의 실제 외래키인데 선택기/등록 검증이 없다. 모르는 태그는 조용히 중립 선호가 된다. 현재 게시 건물에 이 능력 인스턴스는 없어서 현재 동작 장애로 세지 않는다.
- 수정/검증: 기존 종족 선택을 재사용하고 소비자의 `OrdinalIgnoreCase`와 비교 규칙을 맞춘다. 정상 선호·기피 점수와 알 수 없는 종족의 조기 거절을 확인한다.

### R15 · P2 · 작업복의 종족 비교 규칙이 선택 검사와 다름

- [EnvironmentalWorkwearSO.cs](Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs#L39)의 `allowedSpecies` 소비는 대소문자를 구분하지 않지만 선언의 ContentReference는 기본 대소문자 구분을 쓴다. [EnvironmentalWorkwearRuntime.cs](Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs#L259)도 같은 종족 비교를 한다.
- 현재 비어 있지 않은 값은 등록 ID와 정확히 일치한다. 대소문자만 다른 기존/새 값은 런타임 의미가 같은데 새 검사가 거절할 수 있다.
- 수정/검증: 기존 소비 규칙에 맞춰 선택·검증·복원 비교를 통일한다. 대소문자 차이와 실제 미등록 ID를 구분해 검증한다.

### R16 · P2 · 원정 사건의 날씨 조건은 수기 SO 관계이고 일부 선택 태그는 공급되지 않음

- [OffenseDecisionCardSO.cs](Assets/Scripts/Models/Offense/Core/OffenseDecisionCardSO.cs#L16)의 `requiredWorldTags`는 현재 날씨 사건 6개에서 WeatherFront SO ID를 담는다.5개 distinct ID가 모두 존재하지만 선택기/날씨 관계 검사가 없다.
- 실제 생산자는 [OffenseStrategicExpeditionServices.cs](Assets/Scripts/Services/Offense/OffenseStrategicExpeditionServices.cs#L555)로, context.tags에 완료한 구간의 weatherFrontId만 넣는다. [OffenseTravelAndDecisionRuntime.cs](Assets/Scripts/Services/Offense/Strategic/OffenseTravelAndDecisionRuntime.cs#L1743)는 모든 요구 태그가 있어야 카드를 선택한다. 오타가 있으면 해당 사건이 조용히 후보에서 제외된다.
- 별도로 `01_travel_broken_bridge`의 left 선택은 `requiredTag=tools`를 가진다. 현재 비 Editor 생산 경로는 tools 태그를 만들지 않으므로1756행의 transformed label/description 경로에 도달하지 못한다. 이것은 Weather SO 관계가 아니라 **의미 태그 생산자와 소비자의 단절**이다. 보상 효과 자체가 변경된다고 주장하지 않는다.
- 수정/검증: 현재 날씨 관계와 의미 태그의 종류를 구분해 선택/검증한다. tools 전환을 유지하려면 실제 보유 도구 조건에서 태그를 제공하고, 불필요한 작성 필드라면 제거한다. 날씨별 사건 후보와 선택 표시 전환만 표적 확인한다.

### R17 · P1 · 종족→해부 관계의 두 권위와 실제 등록 누락

- [CharacterSpeciesDefinitionSO.cs](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L241)는 `anatomyProfileId`를 가지지만, 의료 카탈로그는 [AnatomyProfileSO.cs](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L18)의 `speciesIds` 역방향 목록으로 다시 매핑한다.
- [AnatomyProfileCatalog.cs](Assets/Scripts/Services/Medical/AnatomyProfileCatalog.cs#L32)32–42행은 중복 종족을 `TryAdd`로 먼저 등록한 항목에 고정하고,67–75행은 미등록 종족을 humanoid로 바꾼다. 현재 중복 등록은 없지만 이중 매핑 일치·전체 종족 coverage 검증이 없다.
- **현재 캐릭터 종족 11개 중 Adventurer만 역방향 등록이 없다.** 명시값과 default가 모두 `anatomy:humanoid`라 문제가 가려져 있다. 나머지10종은 일치한다.
- **현재 야생동물 18종 중13종이 역방향 등록에서 빠졌다.** ash_crawler, carrion_drake, cave_hound, crystal_beetle, deep_goat, ember_lizard, frost_ram, glow_moth, mana_wisp, mire_leech, silk_spider, spore_elk, tunnel_mole이다. [WildlifeAnatomyHealthRuntime.cs](Assets/Scripts/Services/Medical/WildlifeAnatomyHealthRuntime.cs#L287)는 최초 상태 생성 때 이 조회를 사용하므로 이13종은 humanoid 프로필을 받는다. 원본 데이터와 실제 코드 분기로 확인했으며 Play 재현은 하지 않았다.
- 수정/검증: 종족별 해부 매핑의 권위를 하나로 정하고 미등록·중복·상호 불일치를 시작 시 검사한다.13종을 모두 quadruped로 임의 지정해서는 안 된다. 곤충/정령 등 각 종의 의도에 맞는 프로필이 필요하다. 이미 저장된 잘못된 프로필의 처리도 정해야 한다.

### R18 · P1 · 노화 조건의 전역 노드 존재 검사가 실제 종족 호환성을 보장하지 않음

- [AgeConditionDefinitionSO.cs](Assets/Scripts/Models/Species/Core/AgeConditionDefinitionSO.cs#L11)의 `affectedAnatomyNodeIds`는 전체 해부 노드 중 존재 여부만 확인한다. [CharacterLifeDomain.cs](Assets/Scripts/Models/Species/Core/CharacterLifeDomain.cs#L483)는 생물/구조물 여부와 기존 보유 여부로 후보를 정한다.
- 실제 적용은 [AgeConditionAnatomyProjectionRuntime.cs](Assets/Scripts/Services/Character/AgeConditionAnatomyProjectionRuntime.cs#L94)에서 대상 몸의 노드와 교집합을 구하고, 없으면 `age-condition-anatomy-node-unavailable`로 남긴다. 이 pending은 같은 클래스61–63행의 필수 재개에서도 실패할 수 있다.
- **현재 생물학3조건×10종=30조합 중7조합이 무노드다.** 장기 섬유화×Beastkin/Demon/Harpy/Kobold/Slime, 심장 퇴행×Demon/Myconid. 구조물3조건×Golem까지 포함한 현재33개 eligible 조합을 대조했다. 구조물3개는 노드가 있다.
- 수정/검증: 실제 대상 종족/프로필과 맞는 조건만 선택되도록 하거나 조건의 대상 노드를 올바르게 작성한다. 현재 실패7조합, 정상 대표, 이미 pending인 노화 저장의 복원·재시도를 확인한다. 전체 seed 실행은 필요 없다.

### R19 · P1 · 야생동물의 포식 목록은 존재하지만 양쪽 조건이 불일치함

- [WildlifeSpeciesSO.cs](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L50)의 `preySpeciesIds/predatorSpeciesIds`는 둘 다 선택기가 있으나 관계 존재만 검사한다.
- [WildlifeEcosystemRuntime.cs](Assets/Scripts/Models/Wildlife/Core/WildlifeEcosystemRuntime.cs#L1045)의 실제 조건은 포식자의 Hunts뿐 아니라 먹잇감의 비어 있지 않은 predator 목록에 해당 포식자가 들어 있어야 한다. 별도로 먹잇감 diet, 체력, 위치 등도 제한한다.
- **현재 명시 prey 관계16개 중11개가 역방향 목록에서 막힌다.** carrion_drake→cave_rat/shadow_hare, cave_hound→cave_rat/shadow_hare, cave_rat→cave_rat, mire_leech→rune_deer/deep_goat, moss_boar→cave_rat, rune_deer→cave_rat, shadow_hare→cave_rat, silk_spider→cave_rat. 나머지5개도 사냥 실행 성공을 보장하는 수치는 아니며 정적인 관계 조건을 통과한 수치다.
- 같은 종의 다른 개체를 먹는 설정 자체를 무조건 오류로 판정하지 않는다. 현재 코드가 제외하는 것은 같은 개체이며 종간 관계의 의도는 별도로 정해야 한다.
- 수정/검증: 한 방향에서 역목록을 파생하거나 두 목록의 일치 계약을 둔다. 설계상 금지할 관계와 허용할 관계를 정한 뒤 실제 후보 선택과 거절 사유를 표적 확인한다.

### R20 · P1 · CharacterSO의 종족 ID 선택이 다른 객체 참조에 덮임

- [CharacterSO.cs](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L11)는 `speciesTag`와 `species` 객체를 모두 편집한다.38–43행 getter는 객체가 있으면 그 값을 우선한다.
- 공통 문자열 선택기는 ID만 바꾸고, [ContentReferenceCatalog.cs](Assets/Scripts/Services/Items/ContentReferenceCatalog.cs#L224)는 직접 Unity 객체 내부를 순회하지 않는다. 서로 다른 유효 종족을 선택해도 두 입력의 일치를 보장하지 않는다.
- 현재16개 CharacterSO는 둘이 일치한다. 다만 ID 선택만 바꾸면 화면의 작성 의도와 스폰되는 종족이 달라질 수 있다. R07의 연구는 불일치가 거절되는 반면 이 경계는 객체 우선으로 숨겨진다.
- 수정/검증: 종족 SO 선택을 권위로 두고 legacy tag를 동기화/파생한다. 양쪽 개별 편집, 스폰, 기존 저장/기존 에셋 변환을 확인한다.

### R21 · P1 · 백신 아이템 관계가 질병 ID 문자열 조립에 숨어 있음

- [DiseaseFieldResponseRuntime.cs](Assets/Scripts/Services/Character/DiseaseFieldResponseRuntime.cs#L168)는 vaccine 대응에서 `medicine:vaccine:`+질병 ID 접미사로 실제 ItemDefinition을 선택한다. [DiseaseDefinitionSO.cs](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L9)의 `fieldResponseIds`와 질병/아이템 정의 사이에 이 이름 규칙의 명시 관계/교차 검사가 없다.
- 현재 질병16개 중 백신 대응7개와 대응 아이템7개는 일치한다. 질병/아이템을 새로 만들면서 이름 규칙이 달라지면 연결이 끊긴다. 기존 게시 ID rename 보호만으로 새 조합을 보호하지 못한다.
- 같은 runtime469–476행은 알 수 없는 비백신 response를 generic isolation-care-kit 규칙으로 대체한다. response 문자열 오타를 명시 실패로 잡지 못한다.
- 수정/검증: 현재 대응 레코드에 실제 아이템 선택/매핑을 두거나 기존 명명 계약을 명시 검증한다. 새 SO 계층을 반드시 만들 필요는 없다. 백신 누락·잘못된 질병 대상·response 오타와 정상 사용/소모/복원/재시도를 확인한다.

### R22 · P1 · 유전 특성의 문자열 대상 중 실제 효과 소비가 없는 항목

- [HeritableTraitDefinitionSO.cs](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L61)의 `targetId`는 SO ID라기보다 `(kind,targetId)` 효과 키다. 유효성 검사는 종류와 배율만 보며 실제 소비 등록을 요구하지 않는다.
- [HeritableTraitRuntimeEffects.cs](Assets/Scripts/Services/Character/HeritableTraitRuntimeEffects.cs#L33)와 resolver는 정확히 일치하는 키의 질의가 들어올 때만 효과를 돌려준다. 현재 소스에서 AnatomyCapacity5, EnvironmentalTolerance5, Aptitude2, ManaAffinity3, Fertility/cross-lineage1의16개 대상은 이 질의를 하는 실제 소비자가 없다.
- 특성24개에는 총31개 consequence 행이 있다. **31행 중16행(29 distinct 키 중16개)이 미연결**이다. 자산 단위로는11개 전부 미연결,5개 일부 미연결,8개 모두 소비 경로가 있다. 해당 특성 전체가 항상 무효라는 뜻이 아니다. 같은 특성의 Hunger/Movement/ManaOverloadDamage 같은 보조 효과는 [CharacterDeprivationRuntime.cs](Assets/Scripts/Services/Survival/CharacterDeprivationRuntime.cs#L1280), [RoomEnvironmentExperienceAdapter.cs](Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentExperienceAdapter.cs#L574) 등에서 소비된다.
- 수정/검증: SO 선택기를 붙이는 것으로 끝낼 수 없다. 유지할 효과는 기존 능력/환경/숙련 소비에 연결하고 의도하지 않은 효과는 작성 데이터에서 제거한다. 해당 키의 실제 권위값/파생값이 바뀌는 표적 검증을 둔다.

### R23 · P2 · 스킬 수식의 지역 capability 교차관계 검사 위치가 다름

- [CharacterSkillSystemSettingsSO.cs](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L94)의 `otherCapabilityId`, `forbiddenSynergies`는 같은 settings의 모듈 capability 관계다. 일반 SO ID와 구분해야 하지만 직접 문자열 작성이 남아 있다.
- `RequireFormulaCatalog`632–643행은 각 descriptor를 생성하지만 전체 집합의 pair/forbidden 대상 존재·역방향 일치를 검사하지 않는다. 해당 검사는 [CharacterSkillFormulaCatalogAssetBuilder.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterSkillFormulaCatalogAssetBuilder.cs#L195)195–225행의 Editor builder에 있다. builder를 통하지 않은 수정의 조기 거절 경계가 다르다.
- **formatterId/applicatorId는 같은 settings623–627행에서 런타임 검사한다.** 이 두 필드는 결함 목록에서 제외한다. 현재24모듈의 관계는 정상이다. 또한 [NarrativeFormulaCore.cs:613](Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaCore.cs#L613)613–635행은 실제 선택된 pair의 비용 비대칭을 늦게 거절한다. 따라서 모든 런타임 검사가 없다는 뜻은 아니다.
- 수정/검증: 기존 집합 검사를 실제 카탈로그 생성 경계에서도 재사용하고 지역 capability 목록에서 선택하게 한다. 없는 대상·비대칭 pair/forbidden과 정상 생성을 표적 확인한다.

### R24 · P2 · 성능 수식의 작업/효과 대상 키는 registry 관계인데 오타를 조기에 잡지 않음

- [CharacterPerformanceFormulaDefinitionSO.cs](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L48)의 `executionWorkTypeId`, `gameplayEffectTargetId`는 SO 관계와 유사하지만 실제 소유자는 코드 registry다. 작업 ID 검사는 prefix만 확인한다.
- [CharacterPerformanceQuery.cs](Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs#L398)와 [CharacterGameplayEffectProjector.cs](Assets/Scripts/Services/Effects/Runtime/CharacterGameplayEffectProjector.cs#L22)의 소비 대상이 다르면 효과 기여가 사라지고 기본 수식값만 남을 수 있다.
- 현재107개 수식 중 work 값60개(30종×2 channel), effect 값91개(22종)는 실제 키와 일치한다. 현재 오타를 주장하지 않는다.
- 수정/검증: SO를 새로 만들지 않고 실제 WorkType/효과 대상 registry에서 선택·존재 검증을 재사용한다. 정상 효과 기여와 한 글자 오타 거절을 확인한다.

## 미연결 작성 필드 — 안전한 관계로 세지 않은 항목

| 필드 | 현재 상태/실제 경계 | 처리 방향 |
|---|---|---|
| `ProcedureOperatorStatRequirement.statId` | [SurgeryModels.cs](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L196)196–215행에 있으나 [SurgeryRuntimeContracts.cs](Assets/Scripts/Services/Medical/SurgeryRuntimeContracts.cs#L23)는 고정 SurgerySuccess 수식과 minimumWeightedScore를 사용한다.47개 수술의 stats는 모두 빈 목록이다. | 작성 금지/제거 또는 기존 성능 수식에 연결. 현재 적용되는 스탯 SO 관계라고 가장하지 않는다. |
| `BuildingAnesthesiaAbility.anesthesiaItemId` | 현재 medicine:anesthetic 한 값은 존재하지만 실제 경로에서 이 ID로 아이템을 소비하는 호출은 확인되지 않았다. 마취 보너스 소비와 아이템 ID 소비는 다르다. | 소모 설계가 있으면 기존 재고 명령에 연결, 없으면 필드 제거/비활성화. |
| `DefenseFacilityData.affinityTags` | 직렬화 가능하지만 실제 읽기 소비를 확인하지 못했다. | 관계 안전 완료로 세지 않고 유지 의도 확인 후 정리. |
| `FactionArcDefinitionSO.relicItemIds` | 전역 아이템 존재 검사는 있으나 arc의 이 목록을 직접 보상 권위로 쓰는 호출은 확인되지 않았다. 실제 계약 보상과 구분한다. | 목록의 용도를 명시하거나 실제 보상 목록과 일치 검증. |
| `BribeOffer.factionId` | [OffenseDecisionEffects.cs](Assets/Scripts/Services/Offense/Strategic/OffenseDecisionEffects.cs#L253)의 ResolveBribe는 금액/수락/결과를 처리하나 이 세력 필드를 읽지 않는다. | 실제 세력별 행위가 필요하면 연결, 아니면 작성 필드 정리. |

이 표의 항목은 현재 빈 데이터/미사용 소비 경계이며, 존재하는 SO가 깨졌다고 임의로 단정하지 않는다. 그렇다고 전체 관계 완료 선언에서 조용히 빼지도 않는다.


## 실제 Inspector 실행 확인이 필요한 경계

`ContentReferenceDrawer.Owner`148–160행은 SerializedProperty 경로를 아직 반영되지 않은 실제 IList에 적용한다. 새 목록 요소가 SerializedObject에만 추가된 시점에서 일반 itemId의 GetPropertyHeight/ResolveReference가 불리면 이전 빈 IList[0] 접근이 가능하다. 기존 테스트는 새 요소의 TryAssign 직접 호출 또는 effect/narrative 전용 분기를 다룬다. **실제 기본 Inspector가 Apply하는 시점까지 이번에 실행 재현하지 않았으므로 확정 UI 재현과 구분한다.** Unity 검증을 할 때 신규 중첩 목록·다형성 교체·Undo·다중 선택의 draw까지 확인해야 한다.

## 분류 원칙과 제외 근거

- SO 자체 ID는 다른 SO 선택 대상 필드와 다르다. 게시 호환성 문제는 자체 키 보호 항목으로 기록한다.
- 런타임 캐릭터·스택·작업·원정 개체 ID, 영수증/요청 ID, 서사 증거 snapshot은 SO 정의 ID로 무조건 바꾸면 안 된다. 다만 SO에 저장된 초기 snapshot이 정의 모듈을 다시 조회하는 부분은 별도 관계로 추적한다.
- `requiredHabitatId`는 grass/water/burrow/brush/lair를 WildlifeHabitatType으로 파싱하는 의미 키다([SeasonalWildlifeArrivalRuntime.cs](Assets/Scripts/Services/Run/SeasonalWildlifeArrivalRuntime.cs#L9)). 해당 종의 선호 서식지와도 비교한다. 누락된 Habitat SO가 아니다.
- WorkTypeId, StockCategory 저장 키, AI/전술 태그, state flag, GameplayEffect capability/formula registry 키는 실제 정의 소유자가 코드 registry라면 SO 참조로 세지 않는다. 동작을 무시하는 오타가 있더라도 그 위험은 별도로 명시하며 가짜 SO를 만들지 않는다.
- actionName은 AI 표시/진단 이름, description/title/sourceNote 등은 문구다. 이름에 문자열이나 ID가 포함됐다는 이유만으로 외래키라고 판정하지 않는다.
- 직접 객체의 None은 모두 결함이 아니다. 필수/선택 여부와 실제 소비를 확인한다. 현재 값이 유효한 관계도 선택/삭제/등록 제외 경계가 안전하다는 뜻은 아니다.
- `ResearchUnlockBundleDefinitionSO.researchId`는180개 설계 묶음의 Editor 작성/감사 메타데이터다. 선택기와 존재 검사가 있고 실제 해금 권위는 ResearchProject 보상 경로다. 직접 플레이 소비가 없다는 이유만으로180개 연구 기능이 끊겼다고 판정하지 않는다([ResearchUnlockBundleDefinitionSO.cs:54](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L54)).
- 이미 있는 상위 ID·GUID·물리 삭제 보호, 루트 타입/필수 객체 검사, 연구 그래프 순환 검사, 현재 문화 활동 연결 검사는 인정한다. 이번 발견 때문에 기존 검사가 전부 무효라고 주장하지 않는다.

## 필요한 검증의 경계

이 문서는 수정 계획에 필요한 조사 결과다. 코드 작성·전체 seed 검증·새 Unity 실행은 하지 않았다. 수정 시 관계 선택→정의 검증→실제 소비를 먼저 확인하고, 저장에 들어가는 키/영수증에 영향이 있는 항목만 변경 전 저장의 복원과 같은 요청 재시도를 붙인다. 모든 SO 조합의 전수 실행이나 불필요한 클래스 분리를 요구하지 않는다.

문서와 원본 링크·추출 목록 대조는 이번에 검증한다. 기존 r101 실행 PASS는 같은 소스의 기존 검사 범위에 한정하며, 새로 발견한 반례의 PASS로 사용하지 않는다.

## 중첩 키 전체 판정 — R04 상세

공식 identity 검사는 최상위 scalar만 비교한다. 아래 키의 현재 값은 모두 비어 있지 않다. 값이 오늘 유효한지와 게시 후 변경을 막는지는 별도다. V20의 고정 choice 이름처럼 별도 도메인 검사가 잡는 일부 변경도 있으므로 모든 rename이 모든 검사에서 통과한다고 주장하지 않는다.

| 계열 | 현재 작성 수 | 정의 | 저장/실제 재조회 | 변경 영향 |
|---|---:|---|---|---|
| 해부 nodeId | 12에셋 / 214행 | [AnatomyModels.cs:77](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L77) | [CharacterBodyHealthStateRules.cs:1147](Assets/Scripts/Services/Combat/CharacterBodyHealthStateRules.cs#L1147) | 과거 신체 노드 조회 실패 |
| 원정 사건 choiceId | 54 / 108 | [OffenseDecisionDefinitions.cs:22](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L22) | [OffenseAggregateSaveValidation.cs:169](Assets/Scripts/Services/Offense/OffenseAggregateSaveValidation.cs#L169) | 저장한 선택이 현재 카드에 없음 |
| V20 chapter/event/incident choiceId | 76중64 / 172 | [V20AuthoredContentContracts.cs:200](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L200) | [V20CampaignSaveCodec.cs:457](Assets/Scripts/Services/Run/V20CampaignSaveCodec.cs#L457) | 현재 정의와 커밋된 선택 불일치 |
| 원정 campaign target id | 1 / 6 | [OffenseWorldMapModel.cs:167](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L167) | [OffenseAggregateSaveValidation.cs:75](Assets/Scripts/Services/Offense/OffenseAggregateSaveValidation.cs#L75) | 해금/선택/완료한 목표 조회 실패 |
| 생산 outputLineId | 367중363 / 369 | [ProductionPrimitives.cs:212](Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs#L212) | [ProductionExactCapabilityOutputRestoreJoin.cs:337](Assets/Scripts/Services/Economy/ProductionExactCapabilityOutputRestoreJoin.cs#L337) | 진행 중 output join·의미 digest 불일치 |
| 스킬 module/variant/drawback/capability | 1 / 114(24/50/8/32) | [CharacterSkillSystemSettingsSO.cs:38](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L38) | [CharacterSkillFormulaGeneration.cs:616](Assets/Scripts/Services/Character/Core/CharacterSkillFormulaGeneration.cs#L616) | 저장한 제안·스킬 envelope 해석 불일치 |
| 획득 특성 binding/drawback/capability/reaction | 9 / 91(32/23/31/5) | [GameplayEffectContracts.cs:232](Assets/Scripts/Services/Foundation/GameplayEffectContracts.cs#L232) | [CharacterAcquiredTraitValidation.cs:1054](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitValidation.cs#L1054) | 고정 조합/재정의/반응의 exact 검사 실패 |
| 건물 생산/보안 abilityId | 13 / 13(7/6) | [BuildingAbilityContracts.cs:211](Assets/Scripts/Models/Buildings/Core/BuildingAbilityContracts.cs#L211) | [BuildingStateModule.cs:138](Assets/Scripts/Services/Buildings/BuildingStateModule.cs#L138) | 저장 module과 현재 module ID 불일치 |
| GameDomain 중첩 record id | 1 / 57 | [GameDomainContentCatalogSO.cs:17](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L17) | [RunVariableSaveSection.cs:175](Assets/Scripts/Services/Run/RunVariableSaveSection.cs#L175) | 최소32개 저장 연계, 나머지25개 runtime/public 키 |
| 장비 수식 capabilityId | 2 / 13 | [EquipmentEvolutionFormulaDefinitionSO.cs:334](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L334) | [EquipmentItemStateCodec.cs:389](Assets/Scripts/Models/Combat/Core/EquipmentItemStateCodec.cs#L389) | 장비 진화 envelope의 capability 조회 실패 |

총1,157개는 작성된 키의 발생 수다. 모두 같은 위험의 저장 키라는 뜻은 아니다. 건물의 다른2,739개 abilityId는 영속 상태와의 직접 join이 확인되지 않아 저장 파손 수치에 넣지 않았다. GameDomain의25개도 저장 키로 확대하지 않았다. 공식 Python 검사에 메모리 blob만 바꿔 넣은 추가 표적 probe는5에셋/6필드가 통과했다. 실제 저장 파일을 바꾸거나 Unity 복원을 실행한 결과는 아니다.

## SO 전체 타입 목록 — 142개

상속/추상 기반 타입도 포함한다. 에셋0은 곧 고아 코드라는 뜻이 아니다. 아래 에셋 수는 게시 목록의 정확한 script type 기준이며, 필드 수는 그 타입에서 상속·중첩·다형성을 따라 도달한 선언 수다. 여러 root가 같은 선언을 공유하므로 행별 필드 수를 더해2,462와 비교하면 안 된다.

| SO 타입 | 선언 | 게시 에셋 | 도달 필드 | 관계 후보 | 관련 보완 |
|---|---|---:|---:|---:|---|
| `AIActionSet` | [AIActionSet.cs](Assets/Scripts/Services/Character/AI/Action/AIActionSet.cs#L8) | 0 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AICollapse` | [AIDeprivationBreakdownsAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIDeprivationBreakdownsAdapter.cs#L76) | 0 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIDeprivationBreakdownAction` | [AIDeprivationBreakdownsAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIDeprivationBreakdownsAdapter.cs#L3) | 0 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIDesperateDrink` | [AIDeprivationBreakdownsAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIDeprivationBreakdownsAdapter.cs#L54) | 0 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIDesperateEat` | [AIDeprivationBreakdownsAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIDeprivationBreakdownsAdapter.cs#L65) | 0 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIDesperateRelief` | [AIDeprivationBreakdownsAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIDeprivationBreakdownsAdapter.cs#L43) | 0 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIDrink` | [AIDrink.cs](Assets/Scripts/Services/Character/AI/Action/AIDrink.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIEat` | [AIEatAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIEatAdapter.cs#L5) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIExitDungeon` | [AIExitDungeonAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIExitDungeonAdapter.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIFacilityRoleAction` | [AIFacilityRoleActionAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIFacilityRoleActionAdapter.cs#L5) | 3 | 4 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIHaul` | [AIHaul.cs](Assets/Scripts/Services/Character/AI/Action/AIHaul.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIHunt` | [AIHunt.cs](Assets/Scripts/Services/Character/AI/Action/AIHunt.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AILookAround` | [AILookAroundAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AILookAroundAdapter.cs#L4) | 1 | 5 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIPrimitiveBucketWash` | [AIPrimitiveBucketWash.cs](Assets/Scripts/Services/Character/AI/Action/AIPrimitiveBucketWash.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIPrimitiveFieldMeal` | [AIPrimitiveFieldMeal.cs](Assets/Scripts/Services/Character/AI/Action/AIPrimitiveFieldMeal.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIPrimitiveFloorRest` | [AIPrimitiveFloorRest.cs](Assets/Scripts/Services/Character/AI/Action/AIPrimitiveFloorRest.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIPrimitiveLatrine` | [AIPrimitiveLatrine.cs](Assets/Scripts/Services/Character/AI/Action/AIPrimitiveLatrine.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIPrimitiveSurvivalAction` | [AIPrimitiveSurvivalActions.cs](Assets/Scripts/Services/Character/AI/Action/AIPrimitiveSurvivalActions.cs#L3) | 0 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIRescue` | [AIRescue.cs](Assets/Scripts/Services/Character/AI/Action/AIRescue.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIRest` | [AIRestAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIRestAdapter.cs#L5) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIShopping` | [AIShoppingAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIShoppingAdapter.cs#L5) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AISubstanceUse` | [AISubstanceUse.cs](Assets/Scripts/Services/Character/AI/Action/AISubstanceUse.cs#L3) | 1 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIViolentBreakdown` | [AIDeprivationBreakdownsAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIDeprivationBreakdownsAdapter.cs#L87) | 0 | 3 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIWait` | [AIWaitAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Actions/AIWaitAdapter.cs#L6) | 1 | 7 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AIWork` | [AIWork.cs](Assets/Scripts/Services/Character/AI/Action/AIWork.cs#L4) | 1 | 6 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AgeConditionDefinitionSO` | [AgeConditionDefinitionSO.cs](Assets/Scripts/Models/Species/Core/AgeConditionDefinitionSO.cs#L6) | 6 | 4 | 3 | R18 |
| `AnatomyConditionLexiconSO` | [AnatomyConditionLexiconSO.cs](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L29) | 7 | 9 | 7 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `AnatomyProfileSO` | [AnatomyProfileSO.cs](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L7) | 12 | 27 | 11 | R17 |
| `ApparelDefinitionSO` | [ApparelDefinitionSO.cs](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L221) | 56 | 26 | 11 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `BattlefieldModifierDefinitionSO` | [BattlefieldModifierDefinitionSO.cs](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L6) | 12 | 10 | 5 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `BuildingLightingSettingsSO` | [BuildingLightingSettingsSO.cs](Assets/Scripts/Models/Buildings/Core/BuildingLightingSettingsSO.cs#L4) | 0 | 4 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `BuildingSO` | [BuildingSO.cs](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L133) | 400 | 469 | 99 | R01, R12, R13, R14, 미연결 |
| `CareerPositionDefinitionSO` | [CareerPositionDefinitionSO.cs](Assets/Scripts/Models/Species/Core/CareerPositionDefinitionSO.cs#L11) | 6 | 6 | 3 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CharacterAcquiredTraitModuleSO` | [CharacterAcquiredTraitModuleSO.cs](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L6) | 8 | 48 | 20 | R23 |
| `CharacterAcquiredTraitSettingsSO` | [CharacterAcquiredTraitSettingsSO.cs](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L6) | 1 | 59 | 22 | R23 |
| `CharacterAiNaturalnessSettingsSO` | [CharacterAiNaturalnessSettingsSO.cs](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L4) | 1 | 14 | 6 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CharacterAiPerfSettingsSO` | [CharacterAiPerformanceModels.cs](Assets/Scripts/Services/Character/AI/CharacterAiPerformanceModels.cs#L6) | 1 | 8 | 4 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CharacterAmbitionDefinitionSO` | [CharacterAmbitionDefinitionSO.cs](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L7) | 18 | 47 | 22 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CharacterBackgroundDefinitionSO` | [CharacterBackgroundDefinitionSO.cs](Assets/Scripts/Content/CharacterBackgroundDefinitionSO.cs#L5) | 12 | 17 | 9 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CharacterFunctionalCapacityDefinitionSO` | [CharacterFunctionalCapacityDefinitionSO.cs](Assets/Scripts/Services/Foundation/CharacterFunctionalCapacityDefinitionSO.cs#L5) | 14 | 4 | 4 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CharacterPerformanceFormulaDefinitionSO` | [CharacterPerformanceFormulaDefinitionSO.cs](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L36) | 107 | 15 | 7 | R24 |
| `CharacterSO` | [CharacterSO.cs](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L7) | 16 | 178 | 89 | R10, R20 |
| `CharacterSkillSystemSettingsSO` | [CharacterSkillSystemSettingsSO.cs](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L484) | 1 | 100 | 31 | R23 |
| `CharacterSpeciesDefinitionSO` | [CharacterSpeciesDefinitionSO.cs](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L237) | 0 | 60 | 30 | R17 |
| `CharacterSpeciesSO` | [CharacterSpeciesSO.cs](Assets/Scripts/Models/Species/Core/CharacterSpeciesSO.cs#L4) | 11 | 105 | 41 | R17 |
| `CharacterStartingHistorySO` | [CharacterStartingHistorySO.cs](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L6) | 9 | 10 | 6 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CharacterStartingOriginSO` | [CharacterStartingOriginSO.cs](Assets/Scripts/Models/Characters/CharacterStartingOriginSO.cs#L6) | 6 | 6 | 4 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CharacterTraitSO` | [CharacterTraitSO.cs](Assets/Scripts/Content/CharacterTraitSO.cs#L23) | 113 | 133 | 37 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ClimateZoneDefinitionSO` | [ClimateZoneDefinitionSO.cs](Assets/Scripts/Content/ClimateZoneDefinitionSO.cs#L4) | 5 | 5 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CombatArmorSO` | [CombatArmorSO.cs](Assets/Scripts/Models/Economy/Content/CombatArmorSO.cs#L5) | 21 | 46 | 15 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CombatEquipmentDefinitionSO` | [CombatEquipmentDefinitions.cs](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L7) | 0 | 39 | 14 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CombatShieldSO` | [CombatShieldSO.cs](Assets/Scripts/Models/Economy/Content/CombatShieldSO.cs#L4) | 9 | 43 | 14 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CombatWeaponSO` | [CombatWeaponSO.cs](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L6) | 31 | 61 | 17 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `Consideration` | [Consideration.cs](Assets/Scripts/Services/Character/AI/Consideration/Consideration.cs#L6) | 0 | 1 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ConsiderationCanLookAround` | [ConsiderationCanLookAround.cs](Assets/Scripts/Services/Character/AI/Consideration/ConsiderationCanLookAround.cs#L3) | 1 | 1 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ConsiderationFacilityNeed` | [ConsiderationFacilityNeedAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationFacilityNeedAdapter.cs#L3) | 6 | 3 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ConsiderationIsVisitable` | [ConsiderationIsVisitableAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationIsVisitableAdapter.cs#L2) | 1 | 2 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ConsiderationRandom` | [ConsiderationRandomAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationRandomAdapter.cs#L3) | 2 | 3 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ConsiderationShoppingCount` | [ConsiderationShoppingCountAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationShoppingCountAdapter.cs#L5) | 1 | 1 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ConsiderationShouldExitDungeon` | [ConsiderationShouldExitDungeonAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationShouldExitDungeonAdapter.cs#L3) | 1 | 1 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ConsiderationStat` | [ConsiderationStatAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationStatAdapter.cs#L5) | 4 | 3 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ConsiderationWorkNeed` | [ConsiderationWorkNeedAdapter.cs](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationWorkNeedAdapter.cs#L3) | 1 | 3 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CraftMaterialDefinitionSO` | [CraftMaterialDefinitionSO.cs](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L4) | 12 | 16 | 6 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CropDefinitionSO` | [CropDefinitionSO.cs](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L68) | 12 | 17 | 8 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CropGenomeDefinitionSO` | [CropGenomeDefinitionSO.cs](Assets/Scripts/Models/Economy/Content/CropGenomeDefinitionSO.cs#L6) | 32 | 11 | 6 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `CulturalPracticeDefinitionSO` | [CulturalPracticeDefinitionSO.cs](Assets/Scripts/Content/CulturalPracticeDefinitionSO.cs#L7) | 20 | 41 | 19 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DataScriptableObject` | [DataScriptableObject.cs](Assets/Scripts/Models/Economy/Content/DataScriptableObject.cs#L4) | 0 | 1 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DefenseBurnEffectSO` | [DefenseBurnEffectSO.cs](Assets/Scripts/Services/Defense/DefenseBurnEffectSO.cs#L3) | 2 | 5 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DefenseChargeEffectSO` | [DefenseChargeEffectSO.cs](Assets/Scripts/Services/Defense/DefenseChargeEffectSO.cs#L3) | 3 | 6 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DefenseCorrosionEffectSO` | [DefenseCorrosionEffectSO.cs](Assets/Scripts/Services/Defense/DefenseCorrosionEffectSO.cs#L3) | 3 | 4 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DefenseDamageEffectSO` | [DefenseDamageEffectSO.cs](Assets/Scripts/Services/Defense/DefenseDamageEffectSO.cs#L3) | 11 | 4 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DefenseEffectSO` | [DefenseEffectSO.cs](Assets/Scripts/Services/Defense/DefenseEffectSO.cs#L15) | 0 | 4 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DefenseGuardAttackEffectSO` | [DefenseGuardAttackEffectSO.cs](Assets/Scripts/Services/Defense/DefenseGuardAttackEffectSO.cs#L3) | 2 | 5 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DefenseSlowEffectSO` | [DefenseSlowEffectSO.cs](Assets/Scripts/Services/Defense/DefenseSlowEffectSO.cs#L3) | 3 | 4 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DiseaseDefinitionSO` | [DiseaseDefinitionSO.cs](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L4) | 16 | 15 | 9 | R21 |
| `DungeonAudioLibrarySO` | [DungeonAudioLibrarySO.cs](Assets/Scripts/Content/DungeonAudioLibrarySO.cs#L4) | 1 | 8 | 8 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DungeonFactionDefinitionSO` | [DungeonFactionDefinitionSO.cs](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L34) | 12 | 19 | 11 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `DungeonStory.Content.CoreSession.CoreSessionRulesSO` | [CoreSessionRulesSO.cs](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L34) | 1 | 28 | 3 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `EndingDefinitionSO` | [EndingDefinitionSO.cs](Assets/Scripts/Content/EndingDefinitionSO.cs#L7) | 9 | 41 | 19 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `EndlessCrisisPolicyDefinitionSO` | [EndlessCrisisPolicyDefinitionSO.cs](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L58) | 1 | 10 | 3 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `EnemyAbilityDefinitionSO` | [EnemyAbilityDefinitionSO.cs](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L28) | 18 | 12 | 6 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `EnemyArchetypeDefinitionSO` | [EnemyArchetypeDefinitionSO.cs](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L78) | 36 | 48 | 28 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `EnvironmentalWorkwearSO` | [EnvironmentalWorkwearSO.cs](Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs#L7) | 4 | 14 | 7 | R15 |
| `EquipmentEvolutionFormulaCatalogSO` | [EquipmentEvolutionFormulaDefinitionSO.cs](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L150) | 2 | 42 | 14 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `EquipmentModuleDefinitionSO` | [EquipmentModuleDefinitionSO.cs](Assets/Scripts/Models/Combat/Core/EquipmentModuleDefinitionSO.cs#L5) | 20 | 12 | 6 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `FacilityBlueprintSO` | [FacilityBlueprintSO.cs](Assets/Scripts/Models/FacilityShop/Core/FacilityBlueprintSO.cs#L4) | 7 | 12 | 8 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `FacilityCrimeSettingsSO` | [FacilityCrimeSettingsSO.cs](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L5) | 1 | 9 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `FacilityEvolutionRecipeSO` | [FacilityEvolutionRecipeSO.cs](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L6) | 6 | 71 | 29 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `FacilityEvolutionRecordTokenDefinitionSO` | [FacilityEvolutionRecordTokens.cs](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L28) | 9 | 11 | 8 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `FacilitySynthesisRecipeSO` | [FacilitySynthesisRecipeSO.cs](Assets/Scripts/Services/Synthesis/FacilitySynthesisRecipeSO.cs#L5) | 9 | 9 | 7 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `FactionAllianceBenefitBudgetSO` | [FactionAllianceBenefitBudgetSO.cs](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L16) | 1 | 10 | 3 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `FactionArcDefinitionSO` | [FactionArcDefinitionSO.cs](Assets/Scripts/Content/FactionArcDefinitionSO.cs#L5) | 6 | 9 | 8 | R06 |
| `FactionChapterDefinitionSO` | [FactionChapterDefinitionSO.cs](Assets/Scripts/Content/FactionChapterDefinitionSO.cs#L7) | 36 | 47 | 23 | R06 |
| `FactionContractDefinitionSO` | [FactionContractDefinitionSO.cs](Assets/Scripts/Content/FactionContractDefinitionSO.cs#L29) | 24 | 43 | 20 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `FestivalDefinitionSO` | [FestivalDefinitionSO.cs](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L6) | 20 | 36 | 14 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `FuneralCultureSO` | [FuneralCultureSO.cs](Assets/Scripts/Models/Species/Core/FuneralCultureSO.cs#L4) | 11 | 4 | 4 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `GameContentCatalogSO` | [GameContentCatalogSO.cs](Assets/Scripts/Content/GameContentCatalogSO.cs#L6) | 1 | 5 | 5 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `GameData` | [GameData.cs](Assets/Scripts/Models/GameData/GameData.cs#L3) | 1 | 4 | 0 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `GameDomainContentCatalogSO` | [GameDomainContentCatalogSO.cs](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L8) | 1 | 79 | 36 | R03, R05 |
| `GameMediaCatalogSO` | [GameMediaCatalogSO.cs](Assets/Scripts/Content/GameMediaCatalogSO.cs#L7) | 1 | 5 | 5 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `GameplayEffectConditionDefinitionSO` | [GameplayEffectConditionDefinitionSO.cs](Assets/Scripts/Services/Foundation/GameplayEffectConditionDefinitionSO.cs#L3) | 45 | 3 | 3 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `GameplayEffectDefinitionSO` | [GameplayEffectDefinitionSO.cs](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L4) | 65 | 9 | 3 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `GenericItemDefinitionSO` | [GenericItemDefinitionSO.cs](Assets/Scripts/Models/Economy/Content/GenericItemDefinitionSO.cs#L4) | 703 | 68 | 22 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `GuestRequestDefinitionSO` | [GuestRequestDefinitionSO.cs](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L8) | 14 | 58 | 25 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `HeritableTraitDefinitionSO` | [HeritableTraitDefinitionSO.cs](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L6) | 24 | 13 | 7 | R22 |
| `InGameNarrativeTextCatalogSO` | [InGameNarrativeTextCatalogSO.cs](Assets/Scripts/Content/InGameNarrativeTextCatalogSO.cs#L45) | 1 | 5 | 3 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ItemDefinitionCatalogSO` | [ItemDefinitionCatalogSO.cs](Assets/Scripts/Models/Economy/Content/ItemDefinitionCatalogSO.cs#L7) | 1 | 1 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ItemDefinitionSO` | [ItemDefinitionSO.cs](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L548) | 0 | 68 | 22 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ItemHaulingSettingsSO` | [ItemHaulingSettingsSO.cs](Assets/Scripts/Services/Items/ItemHaulingSettingsSO.cs#L4) | 1 | 1 | 0 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `LifeEventDefinitionSO` | [LifeEventDefinitionSO.cs](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L13) | 32 | 53 | 22 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `MaterialEconomicProfileSO` | [MaterialEconomicProfileSO.cs](Assets/Scripts/Models/Economy/Content/MaterialEconomicProfileSO.cs#L7) | 0 | 6 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `OffenseCampaignCatalogSO` | [OffenseCampaignCatalogSO.cs](Assets/Scripts/Services/Offense/OffenseCampaignCatalogSO.cs#L6) | 1 | 30 | 16 | R02 |
| `OffenseDecisionCardSO` | [OffenseDecisionCardSO.cs](Assets/Scripts/Models/Offense/Core/OffenseDecisionCardSO.cs#L5) | 54 | 39 | 18 | R16, 미연결 |
| `OffenseEncounterSO` | [OffenseEncounterSO.cs](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L27) | 36 | 23 | 8 | R08 |
| `OffenseSiteArchetypeSO` | [OffenseSiteArchetypeSO.cs](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L5) | 12 | 22 | 9 | R02 |
| `OffenseUrgentSiteDefinitionSO` | [OffenseUrgentSiteDefinitionSO.cs](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L4) | 6 | 11 | 7 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `OnBuyItemSO` | [OnBuyItemSO.cs](Assets/Scripts/Models/Buildings/Core/OnBuyItemSO.cs#L4) | 0 | 0 | 0 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ProductionRecipeSO` | [ProductionRecipeSO.cs](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L22) | 367 | 49 | 21 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ProficiencyDefinitionSO` | [ProficiencyDefinitionSO.cs](Assets/Scripts/Content/ProficiencyDefinitionSO.cs#L5) | 9 | 6 | 4 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ReproductionProfileSO` | [ReproductionProfileSO.cs](Assets/Scripts/Models/Species/Core/ReproductionProfileSO.cs#L6) | 11 | 9 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ResearchProjectSO` | [ResearchProjectSO.cs](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L7) | 180 | 29 | 19 | R07 |
| `ResearchUnlockBundleDefinitionSO` | [ResearchUnlockBundleDefinitionSO.cs](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L46) | 180 | 9 | 5 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ResourceItemDefinitionSO` | [ResourceItemDefinitionSO.cs](Assets/Scripts/Models/Economy/Content/ResourceItemDefinitionSO.cs#L5) | 366 | 68 | 22 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `RoomEnvironmentSettingsSO` | [RoomEnvironmentSettingsSO.cs](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L20) | 1 | 34 | 3 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `SaleItem` | [SaleItem.cs](Assets/Scripts/Models/Economy/Content/SaleItem.cs#L7) | 4 | 7 | 5 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `SeasonalWorldEventDefinitionSO` | [SeasonalWorldEventDefinitionSO.cs](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L261) | 28 | 89 | 38 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ServiceIncidentDefinitionSO` | [ServiceIncidentDefinitionSO.cs](Assets/Scripts/Content/ServiceIncidentDefinitionSO.cs#L7) | 8 | 46 | 22 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `ServiceProcessSO` | [ServiceProcessSO.cs](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L23) | 5 | 20 | 6 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `SpeciesCultureDefinitionSO` | [SpeciesCultureDefinitionSO.cs](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L29) | 10 | 26 | 14 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `SpeciesLifeHistorySO` | [SpeciesLifeHistorySO.cs](Assets/Scripts/Models/Species/Core/SpeciesLifeHistorySO.cs#L5) | 11 | 8 | 4 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `StatChange` | [StatChange.cs](Assets/Scripts/Models/Buildings/Core/StatChange.cs#L4) | 1 | 2 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `StockInfo` | [StockInfo.cs](Assets/Scripts/Services/Buildings/SO/StockInfo.cs#L7) | 1 | 4 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `SurgicalProcedureSO` | [SurgicalProcedureSO.cs](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L7) | 47 | 49 | 14 | R09, 미연결 |
| `SurvivalBalanceSettingsSO` | [SurvivalBalanceSettingsSO.cs](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L98) | 1 | 18 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `TextileMaterialDefinitionSO` | [TextileMaterialDefinitionSO.cs](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L5) | 12 | 15 | 8 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `TmpKoreanFontSettingsSO` | [TmpKoreanFontSettingsSO.cs](Assets/Scripts/Content/TmpKoreanFontSettingsSO.cs#L6) | 1 | 1 | 1 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `V20AuthoredContentSO` | [V20AuthoredContentContracts.cs](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L6) | 0 | 5 | 4 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `WasteProcessingRulesSO` | [WasteProcessingRulesSO.cs](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L43) | 1 | 20 | 4 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `WeatherFrontDefinitionSO` | [WeatherFrontDefinitionSO.cs](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L4) | 6 | 11 | 2 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `WildlifeHabitatDecorationPaletteSO` | [WildlifeHabitatDecorationPaletteSO.cs](Assets/Scripts/Models/Wildlife/Core/WildlifeHabitatDecorationPaletteSO.cs#L6) | 1 | 7 | 3 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |
| `WildlifeSpeciesSO` | [WildlifeSpeciesSO.cs](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L6) | 18 | 71 | 21 | R19 |
| `WorldInteractionPresentationCatalogSO` | [WorldInteractionPresentationCatalogSO.cs](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L92) | 1 | 17 | 4 | 선언/소비 분류 완료; 무결성 증명 뜻 아님 |

## 전체 직렬화 필드 목록 — 2,462개 고유 선언

분류는 필드 용도이며 PASS 판정이 아니다. `relation-annotated`/`SOrelation`은 관계의 존재를 뜻하며 해당 필드가 모든 변경에 안전하다는 뜻이 아니다. `self-key`/`ownID`는 자체 식별자, `semantic`/`semantic-key`는 코드·지역 의미 키, `runtimeSnapshot`은 생성/저장 표현이다. `unconfirmed`는 위 미연결 작성 필드 표와 같이 소비가 확인되지 않은 경우다. 숫자·enum·중첩 컨테이너도 누락 없이 남긴다. ContentReference/Identity 표식은 별도 열에서 원문대로 기록한다. **본문 보완 항목이 분류 라벨보다 우선한다.**

### `AIActionSet`

도달 SO: `AIActionSet`, `AICollapse`, `AIDeprivationBreakdownAction`, `AIDesperateDrink`, `AIDesperateEat`, `AIDesperateRelief`, `AIDrink`, `AIEat`, `AIExitDungeon`, `AIFacilityRoleAction`, `AIHaul`, `AIHunt`, `AILookAround`, `AIPrimitiveBucketWash`, `AIPrimitiveFieldMeal`, `AIPrimitiveFloorRest`, `AIPrimitiveLatrine`, `AIPrimitiveSurvivalAction`, `AIRescue`, `AIRest`, `AIShopping`, `AISubstanceUse`, `AIViolentBreakdown`, `AIWait`, `AIWork`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `actionName` | `string` | [20](Assets/Scripts/Services/Character/AI/Action/AIActionSet.cs#L20) | display-text | — | — |
| `defaultInterruptPriority` | `int` | [21](Assets/Scripts/Services/Character/AI/Action/AIActionSet.cs#L21) | 수치/설정 | — | — |
| `considerations` | `Consideration[]` | [23](Assets/Scripts/Services/Character/AI/Action/AIActionSet.cs#L23) | direct-object-reference | — | — |

### `AIFacilityRoleAction`

도달 SO: `AIFacilityRoleAction`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `role` | `FacilityRole` | [21](Assets/Scripts/Services/Infrastructure/AI/Actions/AIFacilityRoleActionAdapter.cs#L21) | enum | — | — |

### `AILookAround`

도달 SO: `AILookAround`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `minWaitDuration` | `float` | [13](Assets/Scripts/Services/Infrastructure/AI/Actions/AILookAroundAdapter.cs#L13) | 수치/설정 | — | — |
| `maxWaitDuration` | `float` | [14](Assets/Scripts/Services/Infrastructure/AI/Actions/AILookAroundAdapter.cs#L14) | 수치/설정 | — | — |

### `AIWait`

도달 SO: `AIWait`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `minDuration` | `float` | [15](Assets/Scripts/Services/Infrastructure/AI/Actions/AIWaitAdapter.cs#L15) | 수치/설정 | — | — |
| `maxDuration` | `float` | [16](Assets/Scripts/Services/Infrastructure/AI/Actions/AIWaitAdapter.cs#L16) | 수치/설정 | — | — |
| `onDutyWorkAvailableScore` | `float` | [17](Assets/Scripts/Services/Infrastructure/AI/Actions/AIWaitAdapter.cs#L17) | 수치/설정 | — | — |
| `offDutyVisitAvailableScore` | `float` | [18](Assets/Scripts/Services/Infrastructure/AI/Actions/AIWaitAdapter.cs#L18) | 수치/설정 | — | — |

### `AIWork`

도달 SO: `AIWork`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workType` | `FacilityWorkType` | [13](Assets/Scripts/Services/Character/AI/Action/AIWork.cs#L13) | enum | — | — |
| `minimumDuration` | `float` | [14](Assets/Scripts/Services/Character/AI/Action/AIWork.cs#L14) | 수치/설정 | — | — |
| `workInterruptPriority` | `int` | [15](Assets/Scripts/Services/Character/AI/Action/AIWork.cs#L15) | 수치/설정 | — | — |

### `AgeConditionDefinitionSO`

도달 SO: `AgeConditionDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `conditionId` | `string` | [11](Assets/Scripts/Models/Species/Core/AgeConditionDefinitionSO.cs#L11) | ownID | ContentIdentity | — |
| `displayName` | `string` | [12](Assets/Scripts/Models/Species/Core/AgeConditionDefinitionSO.cs#L12) | display | — | — |
| `constructCondition` | `bool` | [13](Assets/Scripts/Models/Species/Core/AgeConditionDefinitionSO.cs#L13) | 수치/설정 | — | — |
| `affectedAnatomyNodeIds` | `List<string>` | [15](Assets/Scripts/Models/Species/Core/AgeConditionDefinitionSO.cs#L15) | SOrelation | ContentReference(ContentReferenceKind.AnatomyNode) | R18 |

### `AmmunitionItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `ammunitionKindId` | `string` | [455](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L455) | semantic-key | — | — |

### `AnatomyApparelAttachment`

도달 SO: `AnatomyProfileSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `nodeId` | `string` | [101](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L101) | SOrelation | ContentReference(ContentReferenceKind.AnatomyNode) | — |
| `points` | `AnatomyAttachmentPoint` | [102](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L102) | enum | — | — |
| `mode` | `AnatomyApparelAttachmentMode` | [103](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L103) | enum | — | — |

### `AnatomyConditionLexiconEntry`

도달 SO: `AnatomyConditionLexiconSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `condition` | `AnatomyConditionKind` | [10](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L10) | enum | — | — |
| `label` | `string` | [11](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L11) | display | — | — |
| `treatmentVerb` | `string` | [12](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L12) | display | — | — |
| `iconId` | `string` | [13](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L13) | semantic | — | — |
| `vfxId` | `string` | [14](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L14) | semantic | — | — |

### `AnatomyConditionLexiconSO`

도달 SO: `AnatomyConditionLexiconSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `lexiconId` | `string` | [37](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L37) | ownID | ContentIdentity | — |
| `anatomyFamily` | `string` | [38](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L38) | semantic | — | — |
| `speciesIds` | `List<string>` | [40](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L40) | SOrelation | ContentReference(ContentReferenceKind.Species, IgnoreCase = true) | — |
| `entries` | `List<AnatomyConditionLexiconEntry>` | [41](Assets/Scripts/Models/Medical/Core/AnatomyConditionLexiconSO.cs#L41) | 중첩 데이터 | — | — |

### `AnatomyFunctionalCapacityNotApplicable`

도달 SO: `AnatomyProfileSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacityId` | `CharacterFunctionalCapacityId` | [109](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L109) | semantic | — | — |
| `reason` | `string` | [110](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L110) | display | — | — |

### `AnatomyNodeDefinition`

도달 SO: `AnatomyProfileSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `nodeId` | `string` | [77](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L77) | ownID | — | — |
| `displayName` | `string` | [78](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L78) | display | — | — |
| `parentNodeId` | `string` | [80](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L80) | SOrelation | ContentReference(ContentReferenceKind.AnatomyNode) | — |
| `kind` | `AnatomyNodeKind` | [81](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L81) | enum | — | — |
| `functions` | `AnatomyFunction` | [82](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L82) | enum | — | — |
| `maxHealth` | `float` | [83](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L83) | 수치/설정 | — | — |
| `capacityWeight` | `float` | [84](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L84) | 수치/설정 | — | — |
| `vital` | `bool` | [85](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L85) | 수치/설정 | — | — |
| `removable` | `bool` | [86](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L86) | 수치/설정 | — | — |
| `pairedGroupId` | `string` | [87](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L87) | semantic | — | — |
| `legacyBodyPart` | `CombatBodyPart` | [88](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L88) | enum | — | — |
| `mapsToLegacyBodyPart` | `bool` | [89](Assets/Scripts/Models/Medical/Core/AnatomyModels.cs#L89) | 수치/설정 | — | — |

### `AnatomyProfileSO`

도달 SO: `AnatomyProfileSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `profileId` | `string` | [15](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L15) | ownID | ContentIdentity | — |
| `displayName` | `string` | [16](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L16) | display | — | — |
| `anatomyFamily` | `string` | [17](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L17) | semantic | — | — |
| `speciesIds` | `List<string>` | [19](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L19) | SOrelation | ContentReference(ContentReferenceKind.AnatomySpecies, Aliases = "Goblin\|LegacyHumanoid", IgnoreCase = true) | R17 |
| `nodes` | `List<AnatomyNodeDefinition>` | [20](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L20) | 중첩 데이터 | — | — |
| `notApplicableCapacities` | `List<AnatomyFunctionalCapacityNotApplicable>` | [22](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L22) | 중첩 데이터 | — | — |
| `apparelBodyForm` | `ApparelBodyForm` | [24](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L24) | enum | — | — |
| `apparelSize` | `ApparelSizeClass` | [25](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L25) | enum | — | — |
| `apparelBasePoints` | `AnatomyAttachmentPoint` | [26](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L26) | enum | — | — |
| `apparelAttachments` | `List<AnatomyApparelAttachment>` | [27](Assets/Scripts/Models/Medical/Core/AnatomyProfileSO.cs#L27) | 중첩 데이터 | — | — |

### `ApparelDefinitionSO`

도달 SO: `ApparelDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `apparelId` | `string` | [228](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L228) | self-key | ContentIdentity | — |
| `physicalItemId` | `string` | [230](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L230) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `displayName` | `string` | [231](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L231) | authored-value | — | — |
| `description` | `string` | [232](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L232) | authored-value | — | — |
| `bodyForm` | `ApparelBodyForm` | [233](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L233) | enum | — | — |
| `layer` | `ApparelLayer` | [234](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L234) | enum | — | — |
| `fitMode` | `ApparelFitMode` | [235](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L235) | enum | — | — |
| `requiredPoints` | `AnatomyAttachmentPoint` | [236](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L236) | enum | — | — |
| `occupiedPoints` | `AnatomyAttachmentPoint` | [237](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L237) | enum | — | — |
| `sealedOptionalPoints` | `AnatomyAttachmentPoint` | [238](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L238) | enum | — | — |
| `supportedModifications` | `ApparelModificationKind` | [239](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L239) | enum | — | — |
| `useTags` | `ApparelUseTag` | [240](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L240) | semantic-key | — | — |
| `allowedMaterialTags` | `TextileMaterialTag` | [241](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L241) | semantic-key | — | — |
| `tailoringCoefficient` | `float` | [243](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L243) | 수치/설정 | — | — |
| `baseWeight` | `float` | [244](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L244) | 수치/설정 | — | — |
| `allowMythicInspiration` | `bool` | [245](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L245) | 수치/설정 | — | — |
| `requiredResearchId` | `string` | [247](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L247) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |
| `sprite` | `Sprite` | [248](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L248) | presentation-object | — | — |
| `proficiency` | `ProficiencyWorkProfileAuthoring` | [249](Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs#L249) | 중첩 데이터 | — | — |

### `ApplyAgeTreatmentEffect`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `treatment` | `AgeTreatmentEffectKind` | [476](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L476) | enum | — | — |

### `ApplySurgicalBurdenEffect`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `rejection` | `float` | [449](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L449) | 수치/설정 | — | — |
| `mutation` | `float` | [450](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L450) | 수치/설정 | — | — |
| `infection` | `float` | [451](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L451) | 수치/설정 | — | — |

### `ArcaneOverchargeRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `manaThreshold` | `float` | [310](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L310) | 수치/설정 | — | — |
| `durationSeconds` | `int` | [311](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L311) | 수치/설정 | — | — |
| `selfDamageFraction` | `float` | [312](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L312) | 수치/설정 | — | — |
| `equipmentDurabilityFraction` | `float` | [313](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L313) | nonrelation | — | — |
| `aftermathDays` | `int` | [314](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L314) | 수치/설정 | — | — |

### `AuthoredBuildingCategoryRecord`

도달 SO: `GameDomainContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [285](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L285) | self-key | — | — |
| `category` | `BuildingCategory` | [286](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L286) | enum | — | — |
| `displayName` | `string` | [287](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L287) | display-text | — | — |
| `sortOrder` | `int` | [288](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L288) | 수치/설정 | — | — |
| `shopCostWeight` | `int` | [289](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L289) | 수치/설정 | — | — |

### `AuthoredCharacterNeedRecord`

도달 SO: `GameDomainContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [244](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L244) | self-key | — | — |
| `condition` | `CharacterCondition` | [245](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L245) | enum | — | — |
| `displayName` | `string` | [246](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L246) | display-text | — | — |
| `sortOrder` | `int` | [247](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L247) | 수치/설정 | — | — |
| `defaultValue` | `float` | [248](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L248) | 수치/설정 | — | — |
| `workerInitialValue` | `float` | [249](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L249) | 수치/설정 | — | — |
| `relatedFacilityRole` | `FacilityRole` | [250](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L250) | authored-value | — | — |
| `tags` | `CharacterNeedTag` | [251](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L251) | authored-value | — | — |
| `survivalWeight` | `float` | [252](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L252) | 수치/설정 | — | — |
| `criticalMaximum` | `float` | [253](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L253) | 수치/설정 | — | — |
| `criticalLabel` | `string` | [254](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L254) | display-text | — | — |
| `criticalMood` | `float` | [255](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L255) | 수치/설정 | — | — |
| `lowMaximum` | `float` | [256](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L256) | 수치/설정 | — | — |
| `lowLabel` | `string` | [257](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L257) | display-text | — | — |
| `lowMood` | `float` | [258](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L258) | 수치/설정 | — | — |
| `highMinimum` | `float` | [259](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L259) | 수치/설정 | — | — |
| `highLabel` | `string` | [260](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L260) | display-text | — | — |
| `highMood` | `float` | [261](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L261) | 수치/설정 | — | — |

### `AuthoredGameplayEffectRecord`

도달 SO: `GameDomainContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `AuthoredGameplayEffectKind` | [315](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L315) | enum | — | — |
| `id` | `string` | [316](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L316) | semantic-key | — | — |
| `textValue` | `string` | [317](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L317) | confirmed-unvalidated-conditional-relation | — | R03 |
| `numberValue` | `float` | [318](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L318) | 수치/설정 | — | — |
| `stockCategory` | `StockCategory` | [319](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L319) | enum | — | — |
| `defenseOnly` | `bool` | [320](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L320) | 수치/설정 | — | — |

### `AuthoredInvasionPatternRecord`

도달 SO: `GameDomainContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [227](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L227) | self-key | — | — |
| `title` | `string` | [228](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L228) | display-text | — | — |
| `detail` | `string` | [229](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L229) | display-text | — | — |
| `targetPreference` | `InvasionIntruderTargetPreference` | [230](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L230) | authored-value | — | — |
| `directOwnerFocus` | `float` | [231](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L231) | 수치/설정 | — | — |
| `facilityDiversionFocus` | `float` | [232](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L232) | authored-value | — | — |
| `maxFacilityDamageCount` | `int` | [233](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L233) | authored-value | — | — |
| `riskTolerance` | `float` | [234](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L234) | 수치/설정 | — | — |
| `routeCommitmentSeconds` | `float` | [235](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L235) | 수치/설정 | — | — |
| `structureDamageMultiplier` | `float` | [236](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L236) | 수치/설정 | — | — |
| `preferredFacilityFamilyIds` | `List<string>` | [237](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L237) | semantic-key | — | — |

### `AuthoredMetaUpgradeRecord`

도달 SO: `GameDomainContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [188](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L188) | self-key | — | — |
| `branch` | `MetaProgressionBranch` | [189](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L189) | enum | — | — |
| `title` | `string` | [190](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L190) | display-text | — | — |
| `detail` | `string` | [191](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L191) | display-text | — | — |
| `cost` | `int` | [192](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L192) | 수치/설정 | — | — |
| `maxLevel` | `int` | [193](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L193) | 수치/설정 | — | — |
| `effects` | `List<AuthoredGameplayEffectRecord>` | [194](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L194) | 중첩 데이터 | — | — |

### `AuthoredOwnerDoctrineRecord`

도달 SO: `GameDomainContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [214](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L214) | self-key | — | — |
| `speciesTag` | `string` | [216](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L216) | relation-annotated | ContentReference(ContentReferenceKind.Species) | — |
| `title` | `string` | [217](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L217) | display-text | — | — |
| `benefit` | `string` | [218](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L218) | display-text | — | — |
| `tradeoff` | `string` | [219](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L219) | display-text | — | — |
| `effects` | `List<AuthoredGameplayEffectRecord>` | [220](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L220) | 중첩 데이터 | — | — |

### `AuthoredRunVariableRecord`

도달 SO: `GameDomainContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [201](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L201) | self-key | — | — |
| `category` | `RunVariableCategory` | [202](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L202) | enum | — | — |
| `title` | `string` | [203](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L203) | display-text | — | — |
| `detail` | `string` | [204](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L204) | display-text | — | — |
| `importance` | `EventAlertImportance` | [205](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L205) | enum | — | — |
| `activeDays` | `int` | [206](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L206) | 수치/설정 | — | — |
| `effects` | `List<AuthoredGameplayEffectRecord>` | [207](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L207) | 중첩 데이터 | — | — |

### `AuthoredStockCategoryRecord`

도달 SO: `GameDomainContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [268](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L268) | self-key | — | — |
| `category` | `StockCategory` | [269](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L269) | enum | — | — |
| `displayName` | `string` | [270](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L270) | display-text | — | — |
| `shortName` | `string` | [271](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L271) | display-text | — | — |
| `sortOrder` | `int` | [272](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L272) | 수치/설정 | — | — |
| `seedWeight` | `float` | [273](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L273) | 수치/설정 | — | — |
| `deliveryItemId` | `string` | [275](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L275) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `dailyBaseAmount` | `int` | [276](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L276) | 수치/설정 | — | — |
| `dailyUnitCost` | `float` | [277](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L277) | 수치/설정 | — | — |
| `dailyGrowthDivisor` | `int` | [278](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L278) | 수치/설정 | — | — |

### `AutonomousWorkRestrictionRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `actionTag` | `string` | [163](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L163) | semantic | — | — |
| `requiredConditionId` | `string` | [164](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L164) | semantic | — | — |
| `failureReason` | `string` | [165](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L165) | display | — | — |

### `BattlefieldModifierDefinitionSO`

도달 SO: `BattlefieldModifierDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stableId` | `string` | [10](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L10) | self-key | ContentIdentity | — |
| `displayName` | `string` | [11](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L11) | display-text | — | — |
| `description` | `string` | [12](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L12) | display-text | — | — |
| `authoringRevision` | `int` | [13](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L13) | 수치/설정 | — | — |
| `sourceNote` | `string` | [14](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L14) | display-text | — | — |
| `kind` | `BattlefieldModifierKind` | [15](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L15) | enum | — | — |
| `movementMultiplier` | `float` | [16](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L16) | 수치/설정 | — | — |
| `accuracyMultiplier` | `float` | [17](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L17) | 수치/설정 | — | — |
| `damageMultiplier` | `float` | [18](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L18) | 수치/설정 | — | — |
| `requiredCounterTag` | `string` | [19](Assets/Scripts/Models/Offense/Core/BattlefieldModifierDefinitionSO.cs#L19) | semantic-key | — | — |

### `BehaviorUtilityRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `behaviorTag` | `string` | [28](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L28) | semantic | — | — |
| `utilityDelta` | `float` | [29](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L29) | 수치/설정 | — | — |

### `BlueprintBasicPurchaseUnlock`

도달 SO: `FacilityBlueprintSO`, `ResearchProjectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `buildingId` | `int` | [157](Assets/Scripts/Models/FacilityShop/Core/BlueprintUnlock.cs#L157) | relation-annotated | ContentReference(ContentReferenceKind.BuildingNumeric) | — |

### `BlueprintBuildingUnlock`

도달 SO: `FacilityBlueprintSO`, `ResearchProjectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `buildingId` | `int` | [139](Assets/Scripts/Models/FacilityShop/Core/BlueprintUnlock.cs#L139) | relation-annotated | ContentReference(ContentReferenceKind.BuildingNumeric) | — |

### `BlueprintItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `blueprintDefinitionId` | `int` | [499](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L499) | relation-annotated | ContentReference("FacilityBlueprintSO", "id", emptyNumericId: -1) | — |
| `targetResearchId` | `string` | [501](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L501) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |

### `BlueprintRecipeUnlock`

도달 SO: `FacilityBlueprintSO`, `ResearchProjectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `recipeId` | `string` | [175](Assets/Scripts/Models/FacilityShop/Core/BlueprintUnlock.cs#L175) | relation-annotated | ContentReference(ContentReferenceKind.Recipe) | — |

### `BlueprintUnlockCollection`

도달 SO: `FacilityBlueprintSO`, `ResearchProjectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `items` | `List<BlueprintUnlock>` | [32](Assets/Scripts/Models/FacilityShop/Core/BlueprintUnlock.cs#L32) | nested-authored-value | — | — |

### `BribeOffer`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `offerId` | `string` | [73](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L73) | self-key | — | — |
| `factionId` | `string` | [74](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L74) | authored-unused-key | — | 미연결 |
| `price` | `int` | [75](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L75) | 수치/설정 | — | — |
| `outcome` | `BribeOutcomeKind` | [76](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L76) | enum | — | — |
| `acceptancePercent` | `int` | [77](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L77) | 수치/설정 | — | — |
| `acceptedResult` | `string` | [78](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L78) | display-text | — | — |
| `rejectedResult` | `string` | [79](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L79) | display-text | — | — |

### `BuildingAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `abilityId` | `string` | [214](Assets/Scripts/Models/Buildings/Core/BuildingAbilityContracts.cs#L214) | semantic-key | — | — |

### `BuildingAbilityCollection`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `items` | `List<BuildingAbility>` | [43](Assets/Scripts/Models/Buildings/Core/BuildingAbilityContracts.cs#L43) | nested-authored-value | — | — |

### `BuildingActiveHeatFireSourceAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `requiresPower` | `bool` | [213](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L213) | 수치/설정 | — | — |
| `requiresActiveUser` | `bool` | [215](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L215) | 수치/설정 | — | — |
| `ignitionWindowSeconds` | `float` | [217](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L217) | 수치/설정 | — | — |
| `ignitionChancePerWindow` | `float` | [219](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L219) | 수치/설정 | — | — |
| `ignitionIntensity` | `float` | [221](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L221) | 수치/설정 | — | — |

### `BuildingAirDuctAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `exchangeRate` | `float` | [76](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L76) | 수치/설정 | — | — |

### `BuildingAirExchangeAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `targetAirQuality` | `float` | [55](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L55) | authored-value | — | — |
| `qualityPerSecond` | `float` | [58](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L58) | 수치/설정 | — | — |
| `radius` | `int` | [61](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L61) | 수치/설정 | — | — |
| `requiresPower` | `bool` | [64](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L64) | 수치/설정 | — | — |
| `exchangesWithOutside` | `bool` | [67](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L67) | authored-value | — | — |

### `BuildingAnatomyTableAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `successBonus` | `float` | [49](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L49) | 수치/설정 | — | — |
| `workSpeedMultiplier` | `float` | [50](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L50) | 수치/설정 | — | — |

### `BuildingAnesthesiaAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stabilityBonus` | `float` | [84](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L84) | authored-value | — | — |
| `anesthesiaItemId` | `string` | [85](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L85) | unconfirmed | — | 미연결 |
| `anesthesiaCost` | `int` | [86](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L86) | 수치/설정 | — | — |

### `BuildingArcaneSurgeryAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `successBonus` | `float` | [146](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L146) | 수치/설정 | — | — |
| `minimumMutationRisk` | `float` | [147](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L147) | 수치/설정 | — | — |
| `manaCrystalCost` | `int` | [148](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L148) | 수치/설정 | — | — |

### `BuildingAudienceSeatingAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `int` | [36](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L36) | 수치/설정 | — | — |
| `sightQuality` | `float` | [37](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L37) | 수치/설정 | — | — |

### `BuildingAutomationAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `maximumMode` | `AutomationMode` | [316](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L316) | enum | — | — |
| `assistedPowerDemand` | `float` | [324](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L324) | 수치/설정 | — | — |
| `automaticPowerDemand` | `float` | [327](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L327) | 수치/설정 | — | — |
| `assistedWorkMultiplier` | `float` | [330](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L330) | 수치/설정 | — | — |
| `automaticWorkPerSecond` | `float` | [333](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L333) | 수치/설정 | — | — |
| `automaticQualityCap` | `float` | [336](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L336) | 수치/설정 | — | — |
| `maintenancePerGameHour` | `float` | [339](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L339) | 수치/설정 | — | — |

### `BuildingBeastPenAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `int` | [47](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L47) | 수치/설정 | — | — |
| `baseSecurity` | `float` | [48](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L48) | 수치/설정 | — | — |
| `dailyFood` | `float` | [49](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L49) | 수치/설정 | — | — |
| `dailyWater` | `float` | [50](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L50) | 수치/설정 | — | — |
| `tamingWork` | `float` | [51](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L51) | 수치/설정 | — | — |
| `productCollectionWork` | `float` | [52](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L52) | 수치/설정 | — | — |

### `BuildingButcherAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workSeconds` | `float` | [13](Assets/Scripts/Models/Wildlife/Core/BuildingButcherAbility.cs#L13) | 수치/설정 | — | — |

### `BuildingCaptiveHousingAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `int` | [10](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L10) | 수치/설정 | — | — |
| `restraintSlots` | `int` | [11](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L11) | 수치/설정 | — | — |
| `baseSecurity` | `float` | [12](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L12) | 수치/설정 | — | — |
| `acceptsHumanoids` | `bool` | [13](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L13) | semantic-key | — | — |

### `BuildingCircuitBreakerAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `overloadTolerance` | `float` | [72](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L72) | 수치/설정 | — | — |
| `tripHeat` | `float` | [75](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L75) | 수치/설정 | — | — |

### `BuildingCircusAnnouncerAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `satisfactionBonus` | `float` | [84](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L84) | authored-value | — | — |
| `preparationWorkMultiplier` | `float` | [85](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L85) | 수치/설정 | — | — |

### `BuildingCircusGamblingAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `revenuePerAudience` | `int` | [73](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L73) | 수치/설정 | — | — |
| `satisfactionVariance` | `float` | [74](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L74) | authored-value | — | — |

### `BuildingCircusHazardAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `accidentRiskBonus` | `float` | [95](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L95) | authored-value | — | — |
| `satisfactionBonus` | `float` | [96](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L96) | authored-value | — | — |

### `BuildingCircusStageAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `performerCapacity` | `int` | [23](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L23) | 수치/설정 | — | — |
| `baseTicketPrice` | `int` | [24](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L24) | 수치/설정 | — | — |
| `preparationWork` | `float` | [25](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L25) | 수치/설정 | — | — |
| `showDurationSeconds` | `float` | [26](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L26) | 수치/설정 | — | — |

### `BuildingCircusTicketBoothAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `revenueMultiplier` | `float` | [62](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L62) | 수치/설정 | — | — |
| `flatRevenuePerAudience` | `int` | [63](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L63) | 수치/설정 | — | — |

### `BuildingCircusTreatmentZoneAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `accidentDamageMultiplier` | `float` | [106](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L106) | authored-value | — | — |

### `BuildingCleaningAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `restoredCleanliness` | `float` | [208](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L208) | 수치/설정 | — | — |

### `BuildingConveyorOverflowAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `defaultPolicy` | `ConveyorOverflowPolicy` | [305](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L305) | enum | — | — |
| `stallSeconds` | `float` | [309](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L309) | 수치/설정 | — | — |

### `BuildingConveyorPortAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `mode` | `ConveyorPortMode` | [292](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L292) | enum | — | — |
| `destinationId` | `string` | [295](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L295) | semantic-key | — | — |
| `capacity` | `int` | [298](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L298) | 수치/설정 | — | — |

### `BuildingConveyorSegmentAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `speed` | `float` | [238](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L238) | 수치/설정 | — | — |
| `capacity` | `int` | [241](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L241) | 수치/설정 | — | — |
| `outputDirections` | `Vector2Int[]` | [244](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L244) | Unity 값 | — | — |
| `requiresPower` | `bool` | [247](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L247) | 수치/설정 | — | — |
| `allowedItemIds` | `string[]` | [251](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L251) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `allowedStockCategories` | `StockCategory[]` | [254](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L254) | enum | — | — |
| `allowedMaterialIds` | `string[]` | [259](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L259) | relation-annotated | ContentReference("CraftMaterialDefinitionSO", "MaterialId") | — |
| `filterQuality` | `bool` | [262](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L262) | 수치/설정 | — | — |
| `minimumQuality` | `CombatEquipmentQuality` | [265](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L265) | enum | — | — |
| `maximumQuality` | `CombatEquipmentQuality` | [269](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L269) | enum | — | — |
| `filterFreshness` | `bool` | [273](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L273) | 수치/설정 | — | — |
| `minimumFreshness01` | `float` | [276](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L276) | 수치/설정 | — | — |
| `maximumFreshness01` | `float` | [279](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L279) | 수치/설정 | — | — |
| `allowContaminated` | `bool` | [282](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L282) | 수치/설정 | — | — |
| `allowForbidden` | `bool` | [285](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L285) | authored-value | — | — |

### `BuildingCookingAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `inputFood` | `int` | [26](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L26) | 수치/설정 | — | — |
| `cookedMeals` | `int` | [28](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L28) | 수치/설정 | — | — |
| `workSeconds` | `float` | [30](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L30) | 수치/설정 | — | — |
| `requiresFuel` | `bool` | [32](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L32) | 수치/설정 | — | — |

### `BuildingCoverAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `height` | `CombatCoverHeight` | [28](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L28) | enum | — | — |
| `blockChance` | `float` | [30](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L30) | 수치/설정 | — | — |
| `facingDirection` | `Vector2Int` | [32](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L32) | Unity 값 | — | — |
| `allowsCornerPeek` | `bool` | [34](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L34) | 수치/설정 | — | — |
| `coverHitPoints` | `float` | [36](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L36) | 수치/설정 | — | — |

### `BuildingCrimeRiskModifierAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `multiplier` | `float` | [381](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L381) | 수치/설정 | — | — |
| `flatOffset` | `float` | [382](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L382) | 수치/설정 | — | — |

### `BuildingCropIrrigationAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `rangeManhattan` | `int` | [128](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L128) | 수치/설정 | — | — |
| `waterPerRefill` | `float` | [131](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L131) | 수치/설정 | — | — |
| `minimumRefillIntervalSeconds` | `float` | [134](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L134) | 수치/설정 | — | — |

### `BuildingCropPlotAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `indoor` | `bool` | [12](Assets/Scripts/Models/Economy/Content/BuildingCropPlotAbility.cs#L12) | 수치/설정 | — | — |
| `growthMultiplier` | `float` | [13](Assets/Scripts/Models/Economy/Content/BuildingCropPlotAbility.cs#L13) | 수치/설정 | — | — |
| `waterMultiplier` | `float` | [14](Assets/Scripts/Models/Economy/Content/BuildingCropPlotAbility.cs#L14) | 수치/설정 | — | — |
| `compostPerCycle` | `int` | [15](Assets/Scripts/Models/Economy/Content/BuildingCropPlotAbility.cs#L15) | 수치/설정 | — | — |
| `fuelPerCycle` | `int` | [16](Assets/Scripts/Models/Economy/Content/BuildingCropPlotAbility.cs#L16) | 수치/설정 | — | — |
| `cycleSupplyInputs` | `List<ItemAmountDefinition>` | [17](Assets/Scripts/Models/Economy/Content/BuildingCropPlotAbility.cs#L17) | 중첩 데이터 | — | — |

### `BuildingDefenseAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `settings` | `DefenseFacilityData` | [19](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L19) | 중첩 데이터 | — | — |

### `BuildingEconomyAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `constructionValue` | `int` | [12](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L12) | 수치/설정 | — | — |
| `constructionCost` | `int` | [13](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L13) | 수치/설정 | — | — |
| `maintenance` | `int` | [14](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L14) | 수치/설정 | — | — |
| `unlockPhase` | `int` | [15](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L15) | 수치/설정 | — | — |
| `demolitionRefundRate` | `float` | [16](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L16) | 수치/설정 | — | — |

### `BuildingEnvironmentalFireAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `acceptedSources` | `EnvironmentalFireIgnitionSources` | [97](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L97) | enum | — | — |
| `fuelCapacity` | `float` | [101](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L101) | 수치/설정 | — | — |
| `maximumIntensity` | `float` | [103](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L103) | 수치/설정 | — | — |
| `growthPerTick` | `float` | [105](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L105) | 수치/설정 | — | — |
| `fuelConsumedPerTick` | `float` | [107](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L107) | 수치/설정 | — | — |
| `damagePerTick` | `float` | [109](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L109) | 수치/설정 | — | — |
| `minimumSpreadIntensity` | `float` | [111](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L111) | 수치/설정 | — | — |
| `spreadChancePerTick` | `float` | [113](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L113) | 수치/설정 | — | — |
| `spreadIgnitionMultiplier` | `float` | [115](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L115) | 수치/설정 | — | — |
| `waterSuppressionPerUnit` | `float` | [117](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L117) | 수치/설정 | — | — |
| `electricalIgnitionHeat` | `float` | [120](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L120) | 수치/설정 | — | — |
| `electricalIgnitionFault` | `float` | [122](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L122) | 수치/설정 | — | — |
| `electricalIgnitionWindowSeconds` | `float` | [124](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L124) | 수치/설정 | — | — |
| `electricalIgnitionChancePerWindow` | `float` | [126](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L126) | 수치/설정 | — | — |
| `electricalIgnitionIntensity` | `float` | [128](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L128) | 수치/설정 | — | — |

### `BuildingEquipmentCraftingAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `craftableEquipmentIds` | `string[]` | [16](Assets/Scripts/Models/Buildings/Core/BuildingEquipmentCraftingAbility.cs#L16) | confirmed-custom-relation-runtime-guarded | — | R12 |
| `workUnitsPerCycle` | `float` | [20](Assets/Scripts/Models/Buildings/Core/BuildingEquipmentCraftingAbility.cs#L20) | 수치/설정 | — | — |

### `BuildingEquipmentMaintenanceAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workSpeedMultiplier` | `float` | [156](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L156) | 수치/설정 | — | — |
| `simultaneousRepairSlots` | `int` | [157](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L157) | 수치/설정 | — | — |
| `repairSupplyItemId` | `string` | [159](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L159) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `repairSupplyPerQuarterDurability` | `int` | [160](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L160) | authored-value | — | — |

### `BuildingEvolutionAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `settings` | `FacilityEvolutionContributionData` | [59](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L59) | 중첩 데이터 | — | — |

### `BuildingExpeditionRecoveryAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `healthHealRatio` | `float` | [275](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L275) | 수치/설정 | — | — |
| `injuryReduction` | `float` | [276](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L276) | 수치/설정 | — | — |
| `stressRecovery` | `float` | [277](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L277) | 수치/설정 | — | — |

### `BuildingExteriorMaintenanceAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `cleanWorkSeconds` | `float` | [340](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L340) | 수치/설정 | — | — |
| `repairWorkSeconds` | `float` | [341](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L341) | 수치/설정 | — | — |
| `cleanlinessGain` | `float` | [342](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L342) | 수치/설정 | — | — |
| `damageReduction` | `float` | [343](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L343) | 수치/설정 | — | — |

### `BuildingFacilityAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `settings` | `FacilityData` | [11](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L11) | 중첩 데이터 | — | — |

### `BuildingFacilityPartAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `code` | `string` | [184](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L184) | semantic-key | — | — |

### `BuildingFacilitySupplyAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `profiles` | `List<FacilitySupplyProfile>` | [246](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L246) | nested-authored-value | — | — |

### `BuildingFuelConsumerAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `fuelItemId` | `string` | [69](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L69) | confirmed-unvalidated-relation | — | R01 |
| `fuelPerRefuel` | `int` | [71](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L71) | 수치/설정 | — | — |
| `fuelSecondsPerRefuel` | `float` | [73](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L73) | 수치/설정 | — | — |
| `workSeconds` | `float` | [75](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L75) | 수치/설정 | — | — |
| `warmth` | `float` | [77](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L77) | 수치/설정 | — | — |
| `lightSafety` | `float` | [79](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L79) | 수치/설정 | — | — |

### `BuildingGolemRechargeAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `materialItemId` | `string` | [89](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L89) | confirmed-unvalidated-relation | — | R01 |
| `materialQuantity` | `int` | [90](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L90) | 수치/설정 | — | — |
| `requiredWork` | `float` | [91](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L91) | 수치/설정 | — | — |
| `restoredCharge` | `float` | [92](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L92) | 수치/설정 | — | — |

### `BuildingInternalStockAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `int` | [24](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L24) | 수치/설정 | — | — |
| `restockRequestThreshold` | `int` | [25](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L25) | 수치/설정 | — | — |

### `BuildingLightingAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `settings` | `BuildingLightingSettingsSO` | [108](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L108) | direct-object-reference | — | — |
| `intensity` | `float` | [109](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L109) | 수치/설정 | — | — |
| `radius` | `float` | [110](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L110) | 수치/설정 | — | — |

### `BuildingLightingSettingsSO`

도달 SO: `BuildingLightingSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `innerRadiusRatio` | `float` | [20](Assets/Scripts/Models/Buildings/Core/BuildingLightingSettingsSO.cs#L20) | 수치/설정 | — | — |
| `falloffIntensity` | `float` | [21](Assets/Scripts/Models/Buildings/Core/BuildingLightingSettingsSO.cs#L21) | 수치/설정 | — | — |
| `color` | `Color` | [22](Assets/Scripts/Models/Buildings/Core/BuildingLightingSettingsSO.cs#L22) | Unity 값 | — | — |
| `targetSortingLayers` | `string[]` | [23](Assets/Scripts/Models/Buildings/Core/BuildingLightingSettingsSO.cs#L23) | authored-value | — | — |

### `BuildingMedicalAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workSeconds` | `float` | [54](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L54) | 수치/설정 | — | — |
| `severityReduction` | `float` | [56](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L56) | 수치/설정 | — | — |
| `requiresMedicine` | `bool` | [58](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L58) | 수치/설정 | — | — |

### `BuildingMercenaryHiringAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `rolePremium` | `int` | [11](Assets/Scripts/Models/Economy/Content/BuildingMercenaryHiringAbility.cs#L11) | 수치/설정 | — | — |
| `minimumCandidateSatisfaction` | `float` | [14](Assets/Scripts/Models/Economy/Content/BuildingMercenaryHiringAbility.cs#L14) | authored-value | — | — |

### `BuildingNeedRecoveryAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `recovery` | `FacilityNeedRecoveryData` | [66](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L66) | 중첩 데이터 | — | — |

### `BuildingOrganStorageAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `preservationDays` | `float` | [102](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L102) | 수치/설정 | — | — |
| `fuelPerDay` | `int` | [103](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L103) | 수치/설정 | — | — |
| `capacity` | `int` | [104](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L104) | 수치/설정 | — | — |

### `BuildingOutdoorRestAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workSeconds` | `float` | [322](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L322) | 수치/설정 | — | — |
| `moodBonus` | `float` | [323](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L323) | 수치/설정 | — | — |
| `stressRecovery` | `float` | [324](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L324) | 수치/설정 | — | — |
| `moodDurationSeconds` | `float` | [325](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L325) | 수치/설정 | — | — |

### `BuildingOverclockableAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `allowControlled` | `bool` | [9](Assets/Scripts/Models/Economy/Content/BuildingOverclockableAbility.cs#L9) | 수치/설정 | — | — |
| `allowAggressive` | `bool` | [10](Assets/Scripts/Models/Economy/Content/BuildingOverclockableAbility.cs#L10) | 수치/설정 | — | — |
| `allowCritical` | `bool` | [11](Assets/Scripts/Models/Economy/Content/BuildingOverclockableAbility.cs#L11) | 수치/설정 | — | — |

### `BuildingPaidFacilityServiceAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `chargeMode` | `PaidFacilityChargeMode` | [18](Assets/Scripts/Models/Economy/Content/BuildingPaidFacilityServiceAbility.cs#L18) | enum | — | — |
| `cost` | `int` | [21](Assets/Scripts/Models/Economy/Content/BuildingPaidFacilityServiceAbility.cs#L21) | 수치/설정 | — | — |
| `contractId` | `string` | [24](Assets/Scripts/Models/Economy/Content/BuildingPaidFacilityServiceAbility.cs#L24) | semantic-key | — | — |
| `displayName` | `string` | [27](Assets/Scripts/Models/Economy/Content/BuildingPaidFacilityServiceAbility.cs#L27) | authored-value | — | — |

### `BuildingPatrolPostAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workSeconds` | `float` | [305](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L305) | 수치/설정 | — | — |
| `patrolReadinessGain` | `float` | [306](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L306) | 수치/설정 | — | — |
| `incidentDetectionBonus` | `float` | [307](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L307) | authored-value | — | — |

### `BuildingPowerConsumerAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `demandPerSecond` | `float` | [44](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L44) | 수치/설정 | — | — |
| `priority` | `PowerPriority` | [47](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L47) | enum | — | — |
| `minimumSupplyFraction` | `float` | [50](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L50) | 수치/설정 | — | — |

### `BuildingPowerProducerAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `productionPerSecond` | `float` | [26](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L26) | 수치/설정 | — | — |
| `requiresFuel` | `bool` | [29](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L29) | 수치/설정 | — | — |
| `fuelItemId` | `string` | [33](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L33) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `secondsPerFuel` | `float` | [36](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L36) | 수치/설정 | — | — |

### `BuildingPowerStorageAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `float` | [58](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L58) | 수치/설정 | — | — |
| `transferPerSecond` | `float` | [61](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L61) | 수치/설정 | — | — |
| `efficiency` | `float` | [64](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L64) | 수치/설정 | — | — |

### `BuildingPreservationAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `freshnessMultiplier` | `float` | [41](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L41) | 수치/설정 | — | — |
| `preservedMealsPerCook` | `int` | [43](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L43) | 수치/설정 | — | — |

### `BuildingProcessAccidentFireSourceAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workTypeId` | `string` | [262](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L262) | semantic-key | — | — |
| `ignitionIntensity` | `float` | [264](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L264) | 수치/설정 | — | — |

### `BuildingProcessFluidAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workTypeIds` | `string[]` | [166](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L166) | semantic-key | — | — |
| `cleanWaterPerCycle` | `float` | [169](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L169) | 수치/설정 | — | — |
| `wastewaterPerCycle` | `float` | [172](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L172) | 수치/설정 | — | — |
| `wastewaterComposition` | `ProcessWastewaterComposition` | [175](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L175) | enum | — | — |
| `minimumQuality` | `WorldWaterQuality` | [178](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L178) | enum | — | — |
| `allowsManualWaterFallback` | `bool` | [181](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L181) | 수치/설정 | — | — |

### `BuildingProductionAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `outputCategory` | `StockCategory` | [89](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L89) | enum | — | — |
| `amount` | `int` | [90](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L90) | 수치/설정 | — | — |

### `BuildingProductionBufferAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `defaultBatchCapacity` | `int` | [212](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L212) | 수치/설정 | — | — |
| `physicalOutputBufferCycleCapacity` | `int` | [213](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L213) | 수치/설정 | — | — |
| `outputCapacities` | `List<ProductionItemBufferCapacity>` | [214](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L214) | 중첩 데이터 | — | — |
| `allowOverflowDump` | `bool` | [215](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L215) | 수치/설정 | — | — |
| `overflowOffset` | `Vector2Int` | [216](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L216) | Unity 값 | — | — |

### `BuildingProductionOutputDispositionAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `dispositionKind` | `ProductionOutputDispositionAuthoringKind` | [22](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L22) | enum | — | — |
| `ownerCapabilityId` | `string` | [25](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L25) | semantic-key | — | — |
| `reasonCode` | `string` | [28](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L28) | semantic-key | — | — |

### `BuildingProductionSupportAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `supportId` | `string` | [86](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L86) | semantic-key | — | — |
| `featureTags` | `string[]` | [89](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L89) | semantic-key | — | — |
| `compatibleWorkstationTags` | `string[]` | [92](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L92) | semantic-key | — | — |
| `kind` | `ProductionSupportKind` | [95](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L95) | enum | — | — |
| `batchCapacity` | `int` | [98](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L98) | 수치/설정 | — | — |
| `maximumLinkedInstancesPerWorkstation` | `int` | [101](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L101) | 수치/설정 | — | — |
| `requiresPower` | `bool` | [104](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L104) | 수치/설정 | — | — |
| `cleanWaterPerCycle` | `float` | [107](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L107) | 수치/설정 | — | — |
| `wastewaterPerCycle` | `float` | [110](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L110) | 수치/설정 | — | — |
| `wastewaterComposition` | `ProcessWastewaterComposition` | [113](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L113) | enum | — | — |
| `allowsManualWaterFallback` | `bool` | [116](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L116) | 수치/설정 | — | — |
| `requiresFuel` | `bool` | [119](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L119) | 수치/설정 | — | — |
| `fuelItemId` | `string` | [123](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L123) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `fuelPerCycle` | `int` | [126](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L126) | 수치/설정 | — | — |
| `workSpeedMultiplier` | `float` | [129](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L129) | 수치/설정 | — | — |
| `outputMultiplier` | `float` | [132](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L132) | 수치/설정 | — | — |
| `qualityModifier` | `float` | [135](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L135) | 수치/설정 | — | — |

### `BuildingProductionWorkstationAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workstationTag` | `string` | [48](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L48) | semantic-key | — | — |
| `stockSensorInstallationItemId` | `string` | [52](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L52) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `lanePolicy` | `ProductionWorkstationLanePolicy` | [55](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L55) | enum | — | — |
| `manualWorkLaneCount` | `int` | [58](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L58) | 수치/설정 | — | — |
| `automaticWorkLaneCount` | `int` | [61](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L61) | 수치/설정 | — | — |

### `BuildingProstheticAssemblyAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `assemblySpeedMultiplier` | `float` | [185](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L185) | 수치/설정 | — | — |
| `qualityBonus` | `float` | [186](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L186) | 수치/설정 | — | — |

### `BuildingProtectiveEquipmentLockerAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `int` | [85](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L85) | 수치/설정 | — | — |
| `serviceRadius` | `int` | [88](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L88) | 수치/설정 | — | — |

### `BuildingPublicPunishmentAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `cruelSatisfactionBonus` | `float` | [116](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L116) | authored-value | — | — |
| `filthMultiplier` | `float` | [117](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L117) | 수치/설정 | — | — |
| `witnessMoodPenalty` | `float` | [118](Assets/Scripts/Models/Captivity/Core/BuildingCaptivityAbilities.cs#L118) | 수치/설정 | — | — |

### `BuildingQualityAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `star` | `int` | [94](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L94) | 수치/설정 | — | — |

### `BuildingReceptionAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workSeconds` | `float` | [286](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L286) | 수치/설정 | — | — |
| `readinessGain` | `float` | [287](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L287) | 수치/설정 | — | — |
| `firstImpressionBonus` | `float` | [288](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L288) | 수치/설정 | — | — |
| `moodBonus` | `float` | [289](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L289) | 수치/설정 | — | — |
| `moodDurationSeconds` | `float` | [290](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L290) | 수치/설정 | — | — |

### `BuildingRecreationalSubstanceServiceAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `funRecovery` | `float` | [75](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L75) | 수치/설정 | — | — |
| `facilitySentiment` | `float` | [76](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L76) | authored-value | — | — |

### `BuildingRehabilitationAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `adaptationSpeedMultiplier` | `float` | [164](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L164) | 수치/설정 | — | — |
| `rejectionReductionPerWork` | `float` | [165](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L165) | 수치/설정 | — | — |
| `primaryOperatingFacility` | `bool` | [166](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L166) | authored-value | — | — |
| `runeSuture` | `bool` | [167](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L167) | 수치/설정 | — | — |
| `manaCrystalCost` | `int` | [168](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L168) | 수치/설정 | — | — |

### `BuildingResearchArchiveAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `int` | [12](Assets/Scripts/Services/Infrastructure/ResearchBlueprintArchiveAdapter.cs#L12) | 수치/설정 | — | — |

### `BuildingResearchCapacityAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `contributions` | `List<ResearchFacilityContribution>` | [10](Assets/Scripts/Services/Infrastructure/ResearchFacilityCapacityAdapter.cs#L10) | 중첩 데이터 | — | — |

### `BuildingRetailAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `category` | `StockCategory` | [193](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L193) | enum | — | — |

### `BuildingSO`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `objectName` | `string` | [146](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L146) | authored-value | — | — |
| `sprite` | `Sprite` | [147](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L147) | presentation-object | — | — |
| `icon` | `Sprite` | [148](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L148) | presentation-object | — | — |
| `contentDefinitionId` | `string` | [152](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L152) | self-key | ContentIdentity | — |
| `authoringRevision` | `int` | [153](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L153) | 수치/설정 | — | — |
| `sourceNote` | `string` | [154](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L154) | authored-value | — | — |
| `deprecatedCompatibilityAsset` | `bool` | [155](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L155) | 수치/설정 | — | — |
| `useClassification` | `FacilityUseClassification` | [158](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L158) | enum | — | — |
| `researchFacilityCommand` | `ResearchFacilityCommandKind` | [159](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L159) | authored-value | — | — |
| `constructionProficiency` | `ProficiencyWorkProfileAuthoring` | [160](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L160) | 중첩 데이터 | — | — |
| `operationProficiency` | `ProficiencyWorkProfileAuthoring` | [161](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L161) | 중첩 데이터 | — | — |
| `abilityModules` | `BuildingAbilityCollection` | [165](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L165) | nested-authored-value | — | — |
| `width` | `int` | [168](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L168) | authored-value | — | — |
| `height` | `int` | [169](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L169) | 수치/설정 | — | — |
| `layer` | `GridLayer` | [170](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L170) | enum | — | — |
| `category` | `BuildingCategory` | [171](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L171) | enum | — | — |
| `horizontalDraggable` | `bool` | [172](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L172) | 수치/설정 | — | — |
| `verticalDraggable` | `bool` | [173](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L173) | 수치/설정 | — | — |
| `runtimeArchetype` | `BuildingRuntimeArchetypeKind` | [174](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L174) | enum | — | — |
| `tiles` | `Dictionary<BuildingTilemapLayer, Tile>` | [175](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L175) | presentation-object | — | — |
| `movementAnchorOffset` | `Vector2` | [177](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L177) | Unity 값 | — | — |
| `movementTravelTime` | `float` | [179](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L179) | 수치/설정 | — | — |
| `facilityAnchors` | `FacilityAnchorData` | [180](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L180) | nested-authored-value | — | — |
| `OnBuildCondition` | `List<IBuildingCondition>` | [183](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L183) | 중첩 데이터 | — | — |
| `unlocked` | `bool` | [184](Assets/Scripts/Services/Buildings/SO/BuildingSO.cs#L184) | 수치/설정 | — | — |

### `BuildingSeatingAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `int` | [116](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L116) | 수치/설정 | — | — |

### `BuildingSecurityAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `maxAlarmCharges` | `int` | [371](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L371) | 수치/설정 | — | — |
| `chargesPerGuardWork` | `int` | [372](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L372) | 수치/설정 | — | — |

### `BuildingSemanticTagsAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `tags` | `string[]` | [85](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L85) | semantic-key | — | — |

### `BuildingServiceAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `int` | [136](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L136) | 수치/설정 | — | — |
| `contributesStockCategory` | `bool` | [137](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L137) | 수치/설정 | — | — |
| `stockCategory` | `StockCategory` | [138](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L138) | enum | — | — |

### `BuildingServiceHubAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `serviceCategory` | `ServiceCategory` | [9](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L9) | enum | — | — |
| `serviceHubTag` | `string` | [12](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L12) | semantic-key | — | — |
| `supportedProcessIds` | `string[]` | [16](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L16) | relation-annotated | ContentReference("ServiceProcessSO", "ProcessId") | — |
| `baseCapacity` | `int` | [19](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L19) | 수치/설정 | — | — |
| `allowedModes` | `ServiceOperationModeMask` | [22](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L22) | enum | — | — |
| `allowInternalStaffDirectUse` | `bool` | [26](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L26) | 수치/설정 | — | — |
| `paymentPolicy` | `ServicePaymentPolicy` | [29](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L29) | enum | — | — |
| `directPrice` | `int` | [33](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L33) | 수치/설정 | — | — |
| `directSatisfaction` | `float` | [36](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L36) | authored-value | — | — |
| `managedRequiredFeatureTags` | `string[]` | [39](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L39) | semantic-key | — | — |
| `automatedRequiredFeatureTags` | `string[]` | [42](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L42) | semantic-key | — | — |

### `BuildingServiceSupportAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `supportId` | `string` | [89](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L89) | semantic-key | — | — |
| `featureTags` | `string[]` | [92](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L92) | semantic-key | — | — |
| `compatibleHubTags` | `string[]` | [95](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L95) | semantic-key | — | — |
| `modifierType` | `ServiceSupportModifierType` | [98](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L98) | enum | — | — |
| `capacity` | `int` | [101](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L101) | 수치/설정 | — | — |
| `requiresPower` | `bool` | [104](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L104) | 수치/설정 | — | — |
| `cleanWaterPerUse` | `float` | [107](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L107) | 수치/설정 | — | — |
| `wastewaterPerUse` | `float` | [110](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L110) | 수치/설정 | — | — |
| `allowsManualWaterFallback` | `bool` | [113](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L113) | 수치/설정 | — | — |
| `workSpeedMultiplier` | `float` | [116](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L116) | 수치/설정 | — | — |
| `satisfactionModifier` | `float` | [119](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L119) | authored-value | — | — |
| `revenueModifier` | `int` | [122](Assets/Scripts/Services/ServiceRooms/ServiceRoomAbilities.cs#L122) | 수치/설정 | — | — |

### `BuildingSpeciesAffinityAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `preferredTags` | `string[]` | [54](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L54) | confirmed-unvalidated-relation | — | R14 |
| `dislikedTags` | `string[]` | [56](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L56) | confirmed-unvalidated-relation | — | R14 |

### `BuildingSterilizationAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `sterilityBonus` | `float` | [66](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L66) | 수치/설정 | — | — |
| `waterCost` | `int` | [67](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L67) | 수치/설정 | — | — |
| `disinfectantCost` | `int` | [68](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L68) | 수치/설정 | — | — |

### `BuildingStorageAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `category` | `StockCategory` | [102](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L102) | enum | — | — |
| `capacity` | `int` | [103](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L103) | 수치/설정 | — | — |
| `maxStoredMassGrams` | `long` | [104](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L104) | 수치/설정 | — | — |
| `allCategories` | `bool` | [105](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L105) | 수치/설정 | — | — |

### `BuildingStructuralIntegrityAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `maxHitPoints` | `float` | [45](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L45) | 수치/설정 | — | — |
| `toughness` | `float` | [47](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L47) | 수치/설정 | — | — |
| `repairHitPointsPerWork` | `float` | [49](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L49) | 수치/설정 | — | — |
| `breachable` | `bool` | [51](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L51) | 수치/설정 | — | — |

### `BuildingSurgeryTableAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `allowedProcedureTags` | `SurgeryFacilityTag` | [23](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L23) | semantic-key | — | — |
| `successBonus` | `float` | [26](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L26) | 수치/설정 | — | — |
| `workSpeedMultiplier` | `float` | [28](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L28) | 수치/설정 | — | — |
| `baseSterility` | `float` | [30](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L30) | 수치/설정 | — | — |
| `patientSlots` | `int` | [32](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L32) | 수치/설정 | — | — |

### `BuildingTableAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacity` | `int` | [126](Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs#L126) | 수치/설정 | — | — |

### `BuildingTemperatureAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `roomTemperatureOffset` | `float` | [101](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L101) | 수치/설정 | — | — |
| `coldProtection` | `float` | [103](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L103) | 수치/설정 | — | — |
| `heatProtection` | `float` | [105](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L105) | 수치/설정 | — | — |

### `BuildingThermalEmitterAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `mode` | `ThermalEmitterMode` | [19](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L19) | enum | — | — |
| `targetTemperatureC` | `float` | [22](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L22) | authored-value | — | — |
| `playerConfigurable` | `bool` | [25](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L25) | 수치/설정 | — | — |
| `minimumTargetTemperatureC` | `float` | [28](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L28) | authored-value | — | — |
| `maximumTargetTemperatureC` | `float` | [31](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L31) | authored-value | — | — |
| `degreesPerSecond` | `float` | [34](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L34) | 수치/설정 | — | — |
| `radius` | `int` | [37](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L37) | 수치/설정 | — | — |
| `requiresPower` | `bool` | [40](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L40) | 수치/설정 | — | — |
| `exhaustOffset` | `Vector2Int` | [43](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L43) | Unity 값 | — | — |
| `exhaustHeatMultiplier` | `float` | [46](Assets/Scripts/Models/Environment/Core/EnvironmentalBuildingAbilities.cs#L46) | 수치/설정 | — | — |

### `BuildingTrainingAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `moodLabel` | `string` | [358](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L358) | authored-value | — | — |
| `moodAmount` | `float` | [359](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L359) | 수치/설정 | — | — |
| `durationSeconds` | `float` | [360](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L360) | 수치/설정 | — | — |
| `experienceAmount` | `int` | [361](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L361) | 수치/설정 | — | — |

### `BuildingTransplantSupportAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `circulationSupport` | `bool` | [120](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L120) | 수치/설정 | — | — |
| `immuneControl` | `bool` | [121](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L121) | 수치/설정 | — | — |
| `isolationRecovery` | `bool` | [122](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L122) | 수치/설정 | — | — |
| `successBonus` | `float` | [123](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L123) | 수치/설정 | — | — |
| `rejectionReduction` | `float` | [124](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L124) | 수치/설정 | — | — |
| `bloodCost` | `int` | [125](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L125) | 수치/설정 | — | — |
| `immunosuppressantCost` | `int` | [126](Assets/Scripts/Models/Medical/Core/BuildingSurgeryAbilities.cs#L126) | 수치/설정 | — | — |

### `BuildingTreasuryPoweredDefenseAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `shotCost` | `int` | [11](Assets/Scripts/Models/Economy/Content/BuildingTreasuryPoweredDefenseAbility.cs#L11) | 수치/설정 | — | — |
| `defaultMinimumThreat` | `int` | [14](Assets/Scripts/Models/Economy/Content/BuildingTreasuryPoweredDefenseAbility.cs#L14) | 수치/설정 | — | — |
| `defaultInvasionBudget` | `int` | [17](Assets/Scripts/Models/Economy/Content/BuildingTreasuryPoweredDefenseAbility.cs#L17) | 수치/설정 | — | — |
| `defaultBossOnly` | `bool` | [20](Assets/Scripts/Models/Economy/Content/BuildingTreasuryPoweredDefenseAbility.cs#L20) | 수치/설정 | — | — |

### `BuildingUtilityConnectionAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `channels` | `UtilityChannel` | [9](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L9) | enum | — | — |
| `maxThroughput` | `float` | [15](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L15) | 수치/설정 | — | — |
| `normallyOpen` | `bool` | [18](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L18) | 수치/설정 | — | — |

### `BuildingVentilationAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `hygieneRiskReduction` | `float` | [114](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L114) | 수치/설정 | — | — |
| `smokeRiskReduction` | `float` | [116](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L116) | 수치/설정 | — | — |

### `BuildingWastewaterProcessorAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `wastewaterInput` | `float` | [211](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L211) | 수치/설정 | — | — |
| `waterOutput` | `float` | [214](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L214) | 수치/설정 | — | — |
| `outputQuality` | `WorldWaterQuality` | [217](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L217) | enum | — | — |
| `requiresPower` | `bool` | [220](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L220) | 수치/설정 | — | — |
| `sludgeItemId` | `string` | [224](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L224) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `sludgeAmount` | `int` | [227](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L227) | 수치/설정 | — | — |
| `secondsPerBatch` | `float` | [230](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L230) | 수치/설정 | — | — |

### `BuildingWaterContainerTransferAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `waterPerBatch` | `float` | [111](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L111) | 수치/설정 | — | — |
| `secondsPerBatch` | `float` | [114](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L114) | 수치/설정 | — | — |
| `bottleTargetStock` | `int` | [117](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L117) | authored-value | — | — |
| `requiresPower` | `bool` | [120](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L120) | 수치/설정 | — | — |

### `BuildingWaterFixtureAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `cleanWaterPerUse` | `float` | [142](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L142) | 수치/설정 | — | — |
| `wastewaterPerUse` | `float` | [145](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L145) | 수치/설정 | — | — |
| `minimumQuality` | `WorldWaterQuality` | [148](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L148) | enum | — | — |
| `allowsManualWaterFallback` | `bool` | [151](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L151) | 수치/설정 | — | — |
| `allowsDryFallback` | `bool` | [154](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L154) | 수치/설정 | — | — |
| `manualWasteItemId` | `string` | [158](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L158) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |

### `BuildingWaterProducerAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `quality` | `WorldWaterQuality` | [82](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L82) | enum | — | — |
| `productionPerSecond` | `float` | [85](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L85) | 수치/설정 | — | — |
| `requiresPower` | `bool` | [88](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L88) | 수치/설정 | — | — |

### `BuildingWaterSourceAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `waterPerWork` | `int` | [13](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L13) | 수치/설정 | — | — |
| `workSeconds` | `float` | [15](Assets/Scripts/Models/Survival/Core/SurvivalBuildingAbilities.cs#L15) | 수치/설정 | — | — |

### `BuildingWaterStorageAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `channels` | `UtilityChannel` | [96](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L96) | enum | — | — |
| `cleanWaterCapacity` | `float` | [100](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L100) | 수치/설정 | — | — |
| `wastewaterCapacity` | `float` | [103](Assets/Scripts/Services/Infrastructure/Industrial/IndustrialInfrastructureBuildingAbilities.cs#L103) | 수치/설정 | — | — |

### `BuildingWorkAmountAbility`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `constructionProjectScale` | `ProjectScale` | [147](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L147) | enum | — | — |
| `constructionWorkRequired` | `float` | [148](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L148) | 수치/설정 | — | — |
| `repairWorkRequired` | `float` | [149](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L149) | 수치/설정 | — | — |
| `cleanWorkRequired` | `float` | [150](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L150) | 수치/설정 | — | — |
| `researchWorkRequired` | `float` | [151](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L151) | 수치/설정 | — | — |
| `operateWorkRequired` | `float` | [152](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L152) | 수치/설정 | — | — |
| `defaultWorkerPolicy` | `WorkerSelectionPolicySaveData` | [154](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L154) | 중첩 데이터 | — | — |
| `constructionMaterials` | `List<ItemAmountDefinition>` | [158](Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs#L158) | 중첩 데이터 | — | — |

### `CareerPositionDefinitionSO`

도달 SO: `CareerPositionDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `definitionId` | `string` | [16](Assets/Scripts/Models/Species/Core/CareerPositionDefinitionSO.cs#L16) | ownID | ContentIdentity | — |
| `displayName` | `string` | [17](Assets/Scripts/Models/Species/Core/CareerPositionDefinitionSO.cs#L17) | display | — | — |
| `position` | `CareerPositionKind` | [18](Assets/Scripts/Models/Species/Core/CareerPositionDefinitionSO.cs#L18) | enum | — | — |
| `scope` | `CareerPositionScopeKind` | [19](Assets/Scripts/Models/Species/Core/CareerPositionDefinitionSO.cs#L19) | enum | — | — |
| `maximumOccupants` | `int` | [20](Assets/Scripts/Models/Species/Core/CareerPositionDefinitionSO.cs#L20) | 수치/설정 | — | — |
| `requiredFacilityTag` | `string` | [21](Assets/Scripts/Models/Species/Core/CareerPositionDefinitionSO.cs#L21) | semantic | — | — |

### `CharacterAcquiredTraitDrawbackCapabilityDefinition`

도달 SO: `CharacterAcquiredTraitSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `drawbackId` | `string` | [126](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L126) | ownID | — | — |
| `displayName` | `string` | [127](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L127) | display | — | — |
| `description` | `string` | [128](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L128) | display | — | — |
| `domainAffinities` | `List<CharacterNarrativeDomain>` | [129](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L129) | enum | — | — |
| `conflictGroups` | `List<string>` | [130](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L130) | semantic | — | — |
| `maximumCredit` | `int` | [131](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L131) | 수치/설정 | — | — |
| `effects` | `List<GameplayEffectBinding>` | [132](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L132) | 중첩 데이터 | — | — |
| `capabilityId` | `string` | [133](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L133) | semantic | — | — |
| `formula` | `CharacterSkillCapabilityFormulaDefinition` | [134](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L134) | 중첩 데이터 | — | — |

### `CharacterAcquiredTraitFormulaPolicyDefinition`

도달 SO: `CharacterAcquiredTraitSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `formulaVersion` | `int` | [210](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L210) | 수치/설정 | — | — |
| `baseBudget` | `int` | [211](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L211) | 수치/설정 | — | — |
| `powerScale` | `int` | [212](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L212) | 수치/설정 | — | — |
| `softCapK` | `float` | [213](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L213) | 수치/설정 | — | — |
| `minimumImportance` | `float` | [214](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L214) | 수치/설정 | — | — |
| `maximumImportance` | `float` | [215](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L215) | 수치/설정 | — | — |
| `milestoneWeights` | `List<float>` | [216](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L216) | 수치/설정 | — | — |
| `drawbackCredit` | `NarrativeFormulaDrawbackCreditPolicyDefinition` | [217](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L217) | 중첩 데이터 | — | — |
| `triggerFrequencyUnits` | `int` | [218](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L218) | 수치/설정 | — | — |
| `guaranteedProc` | `bool` | [219](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L219) | 수치/설정 | — | — |
| `targetCount` | `int` | [220](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L220) | nonrelation | — | — |
| `catalogSha256` | `string` | [221](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L221) | semantic | — | — |

### `CharacterAcquiredTraitManifestationGateDefinition`

도달 SO: `CharacterAcquiredTraitSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `meaningfulRecordMilestone` | `int` | [9](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitDefinitions.cs#L9) | 수치/설정 | — | — |
| `rarity` | `CharacterSkillRarity` | [10](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitDefinitions.cs#L10) | enum | — | — |
| `budget` | `int` | [11](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitDefinitions.cs#L11) | 수치/설정 | — | — |

### `CharacterAcquiredTraitModuleSO`

도달 SO: `CharacterAcquiredTraitModuleSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `moduleId` | `string` | [11](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L11) | ownID | ContentIdentity | — |
| `displayName` | `string` | [12](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L12) | display | — | — |
| `description` | `string` | [13](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L13) | display | — | — |
| `cost` | `int` | [14](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L14) | 수치/설정 | — | — |
| `conflictGroups` | `List<string>` | [15](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L15) | semantic | — | — |
| `domainAffinities` | `List<CharacterNarrativeDomain>` | [16](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L16) | enum | — | — |
| `effects` | `List<GameplayEffectBinding>` | [17](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L17) | 중첩 데이터 | — | — |
| `drawbackBindingIds` | `List<string>` | [18](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L18) | semantic | — | — |
| `specialReactions` | `List<CharacterAcquiredTraitSpecialReactionDefinition>` | [21](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L21) | 중첩 데이터 | — | — |
| `capabilityId` | `string` | [23](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L23) | semantic | — | — |
| `formula` | `CharacterSkillCapabilityFormulaDefinition` | [24](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitModuleSO.cs#L24) | 중첩 데이터 | — | — |

### `CharacterAcquiredTraitSettingsSO`

도달 SO: `CharacterAcquiredTraitSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `settingsId` | `string` | [11](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L11) | ownID | ContentIdentity | — |
| `maximumActiveTraits` | `int` | [12](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L12) | nonrelation | — | — |
| `manifestationGates` | `List<CharacterAcquiredTraitManifestationGateDefinition>` | [15](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L15) | 중첩 데이터 | — | — |
| `formulaPolicy` | `CharacterAcquiredTraitFormulaPolicyDefinition` | [18](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L18) | 중첩 데이터 | — | — |
| `drawbackCapabilities` | `List<CharacterAcquiredTraitDrawbackCapabilityDefinition>` | [21](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSettingsSO.cs#L21) | 중첩 데이터 | — | — |

### `CharacterAcquiredTraitSpecialReactionDefinition`

도달 SO: `CharacterAcquiredTraitModuleSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `reactionId` | `string` | [43](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L43) | ownID | — | — |
| `trigger` | `CharacterAcquiredTraitReactionTrigger` | [44](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L44) | enum | — | — |
| `ownerRole` | `CharacterAcquiredTraitReactionOwnerRole` | [45](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L45) | enum | — | — |
| `condition` | `CharacterAcquiredTraitReactionCondition` | [46](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L46) | enum | — | — |
| `action` | `CharacterAcquiredTraitReactionAction` | [47](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L47) | enum | — | — |
| `baseValue` | `float` | [48](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L48) | 수치/설정 | — | — |
| `durationDays` | `int` | [49](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L49) | 수치/설정 | — | — |
| `cooldownDays` | `int` | [50](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L50) | 수치/설정 | — | — |
| `maximumTriggersPerDay` | `int` | [51](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L51) | 수치/설정 | — | — |
| `maximumLifetimeTriggers` | `int` | [52](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L52) | 수치/설정 | — | — |
| `displayLabel` | `string` | [53](Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitSpecialReaction.cs#L53) | display | — | — |

### `CharacterAiNaturalnessSettingsSO`

도달 SO: `CharacterAiNaturalnessSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `softLockMinimumSeconds` | `float` | [9](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L9) | 수치/설정 | — | — |
| `softLockMaximumSeconds` | `float` | [10](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L10) | 수치/설정 | — | — |
| `softLockScoreBonus` | `float` | [11](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L11) | 수치/설정 | — | — |
| `nearbyCharacterRadius` | `float` | [14](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L14) | 수치/설정 | — | — |
| `wildlifeThreatRadius` | `float` | [15](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L15) | 수치/설정 | — | — |
| `signalCacheSeconds` | `float` | [16](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L16) | 수치/설정 | — | — |
| `queueWaitThreshold` | `float` | [19](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L19) | 수치/설정 | — | — |
| `shelterWeatherThreshold` | `float` | [20](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L20) | 수치/설정 | — | — |
| `stepAsideFailureThreshold` | `float` | [21](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L21) | nonrelation | — | — |
| `freeFacilityTravelCells` | `float` | [24](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L24) | nonrelation | — | — |
| `facilityTravelUtilityCostPerCell` | `float` | [25](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L25) | nonrelation | — | — |
| `maximumFacilityTravelUtilityPenalty` | `float` | [26](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L26) | nonrelation | — | — |
| `equivalentFacilityUtilityTolerance` | `float` | [27](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L27) | nonrelation | — | — |
| `equivalentFacilityMinimumTravelSavingsCells` | `float` | [28](Assets/Scripts/Models/AI/Core/CharacterAiNaturalnessSettingsSO.cs#L28) | nonrelation | — | — |

### `CharacterAiPerfSettingsSO`

도달 SO: `CharacterAiPerfSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `targetSchedulerAverageMs` | `float` | [12](Assets/Scripts/Services/Character/AI/CharacterAiPerformanceModels.cs#L12) | nonrelation | — | — |
| `targetSchedulerP95Ms` | `float` | [13](Assets/Scripts/Services/Character/AI/CharacterAiPerformanceModels.cs#L13) | nonrelation | — | — |
| `targetGcKbPerFrame` | `float` | [14](Assets/Scripts/Services/Character/AI/CharacterAiPerformanceModels.cs#L14) | nonrelation | — | — |
| `sampleCapacity` | `int` | [15](Assets/Scripts/Services/Character/AI/CharacterAiPerformanceModels.cs#L15) | 수치/설정 | — | — |
| `selectedTickInterval` | `float` | [18](Assets/Scripts/Services/Character/AI/CharacterAiPerformanceModels.cs#L18) | 수치/설정 | — | — |
| `visibleTickInterval` | `float` | [19](Assets/Scripts/Services/Character/AI/CharacterAiPerformanceModels.cs#L19) | 수치/설정 | — | — |
| `offscreenIdleTickInterval` | `float` | [20](Assets/Scripts/Services/Character/AI/CharacterAiPerformanceModels.cs#L20) | nonrelation | — | — |
| `offscreenLongWorkTickInterval` | `float` | [21](Assets/Scripts/Services/Character/AI/CharacterAiPerformanceModels.cs#L21) | 수치/설정 | — | — |

### `CharacterAiPersonality`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `diligence` | `float` | [7](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L7) | 수치/설정 | — | — |
| `curiosity` | `float` | [8](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L8) | 수치/설정 | — | — |
| `selfCare` | `float` | [9](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L9) | 수치/설정 | — | — |
| `patience` | `float` | [10](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L10) | 수치/설정 | — | — |
| `shoppingInterest` | `float` | [11](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L11) | 수치/설정 | — | — |
| `sociability` | `float` | [12](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L12) | nonrelation | — | — |
| `riskTaking` | `float` | [13](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L13) | 수치/설정 | — | — |
| `orderliness` | `float` | [14](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L14) | 수치/설정 | — | — |
| `outdoorPreference` | `float` | [15](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L15) | nonrelation | — | — |
| `noveltySeeking` | `float` | [16](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L16) | 수치/설정 | — | — |
| `routineAdherence` | `float` | [17](Assets/Scripts/Services/Character/AI/CharacterAiPersonality.cs#L17) | 수치/설정 | — | — |

### `CharacterAmbitionDefinitionSO`

도달 SO: `CharacterAmbitionDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `category` | `CharacterAmbitionCategory` | [10](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L10) | enum | — | — |
| `targetProgress` | `int` | [11](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L11) | nonrelation | — | — |
| `deadlineDays` | `int` | [12](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L12) | 수치/설정 | — | — |
| `activationRequirements` | `V20ContentRequirementSet` | [13](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L13) | 중첩 데이터 | — | — |
| `failureConditions` | `V20ContentRequirementSet` | [14](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L14) | 중첩 데이터 | — | — |
| `completionRewards` | `List<V20ContentEffect>` | [15](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L15) | 중첩 데이터 | — | — |
| `relatedEventWeights` | `List<V20WeightedId>` | [16](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L16) | 중첩 데이터 | — | — |
| `cooperationAmbitionIds` | `List<string>` | [17](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L17) | SOrelation | ContentReference(ContentReferenceKind.Ambition) | — |
| `conflictAmbitionIds` | `List<string>` | [18](Assets/Scripts/Content/CharacterAmbitionDefinitionSO.cs#L18) | SOrelation | ContentReference(ContentReferenceKind.Ambition) | — |

### `CharacterBackgroundDefinitionSO`

도달 SO: `CharacterBackgroundDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `startingSkills` | `List<V20SkillBonus>` | [8](Assets/Scripts/Content/CharacterBackgroundDefinitionSO.cs#L8) | 중첩 데이터 | — | — |
| `startingEffects` | `List<V20ContentEffect>` | [9](Assets/Scripts/Content/CharacterBackgroundDefinitionSO.cs#L9) | 중첩 데이터 | — | — |
| `factionReactions` | `List<V20WeightedId>` | [10](Assets/Scripts/Content/CharacterBackgroundDefinitionSO.cs#L10) | semantic | — | — |
| `initialMemoryCode` | `string` | [11](Assets/Scripts/Content/CharacterBackgroundDefinitionSO.cs#L11) | semantic | — | — |

### `CharacterCombatAbilityCollection`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `abilities` | `List<CharacterCombatAbilityDefinition>` | [31](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L31) | 중첩 데이터 | — | — |

### `CharacterCombatAbilityDefinition`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [47](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L47) | ownID | — | — |
| `displayName` | `string` | [48](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L48) | display | — | — |
| `description` | `string` | [49](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L49) | display | — | — |
| `cooldownTurns` | `int` | [50](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L50) | 수치/설정 | — | — |
| `targetRule` | `OffenseBattleTargetRule` | [51](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L51) | semantic | — | — |
| `usableFrom` | `OffenseFormationMask` | [53](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L53) | enum | — | — |
| `targetPositions` | `OffenseFormationMask` | [55](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L55) | nonrelation | — | — |
| `effects` | `List<OffenseCombatEffectModule>` | [58](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L58) | 중첩 데이터 | — | — |

### `CharacterFunctionalCapacityDefinitionSO`

도달 SO: `CharacterFunctionalCapacityDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacityId` | `CharacterFunctionalCapacityId` | [11](Assets/Scripts/Services/Foundation/CharacterFunctionalCapacityDefinitionSO.cs#L11) | semantic | — | — |
| `stableId` | `string` | [12](Assets/Scripts/Services/Foundation/CharacterFunctionalCapacityDefinitionSO.cs#L12) | ownID | ContentIdentity | — |
| `displayName` | `string` | [13](Assets/Scripts/Services/Foundation/CharacterFunctionalCapacityDefinitionSO.cs#L13) | display | — | — |
| `description` | `string` | [14](Assets/Scripts/Services/Foundation/CharacterFunctionalCapacityDefinitionSO.cs#L14) | display | — | — |

### `CharacterIdentityRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `ruleId` | `string` | [15](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L15) | ownID | — | — |
| `priority` | `int` | [16](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L16) | 수치/설정 | — | — |

### `CharacterModelModifiers`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `preferredFacilityRoles` | `FacilityRole` | [10](Assets/Scripts/Models/Characters/CharacterAuthoredModel.cs#L10) | nonrelation | — | — |
| `dislikedFacilityRoles` | `FacilityRole` | [11](Assets/Scripts/Models/Characters/CharacterAuthoredModel.cs#L11) | nonrelation | — | — |
| `preferredWorkTypes` | `FacilityWorkType` | [12](Assets/Scripts/Models/Characters/CharacterAuthoredModel.cs#L12) | enum | — | — |
| `dislikedWorkTypes` | `FacilityWorkType` | [13](Assets/Scripts/Models/Characters/CharacterAuthoredModel.cs#L13) | enum | — | — |

### `CharacterNeedBalanceEntry`

도달 SO: `SurvivalBalanceSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `condition` | `CharacterCondition` | [41](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L41) | enum | — | — |
| `dailyDepletion` | `float` | [42](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L42) | 수치/설정 | — | — |
| `workDepletionPerSecond` | `float` | [43](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L43) | 수치/설정 | — | — |
| `response` | `CharacterNeedResponseProfile` | [44](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L44) | 중첩 데이터 | — | — |

### `CharacterNeedResponseProfile`

도달 SO: `SurvivalBalanceSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `routineStart` | `float` | [22](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L22) | 수치/설정 | — | — |
| `emergencyStart` | `float` | [23](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L23) | 수치/설정 | — | — |
| `resumeTarget` | `float` | [24](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L24) | authored-value | — | — |

### `CharacterPerformanceCapacityInput`

도달 SO: `CharacterPerformanceFormulaDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacityId` | `CharacterFunctionalCapacityId` | [9](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L9) | semantic | — | — |
| `weight` | `float` | [10](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L10) | 수치/설정 | — | — |
| `role` | `CharacterPerformanceInputRole` | [11](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L11) | enum | — | — |
| `requiredThreshold` | `float` | [12](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L12) | 수치/설정 | — | — |

### `CharacterPerformanceFormulaDefinitionSO`

도달 SO: `CharacterPerformanceFormulaDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `formulaId` | `string` | [42](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L42) | ownID | ContentIdentity | — |
| `displayName` | `string` | [43](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L43) | display | — | — |
| `domain` | `CharacterPerformanceFormulaDomain` | [44](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L44) | enum | — | — |
| `resultChannel` | `CharacterPerformanceResultChannel` | [45](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L45) | enum | — | — |
| `baseValue` | `float` | [46](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L46) | 수치/설정 | — | — |
| `capacityInputs` | `List<CharacterPerformanceCapacityInput>` | [47](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L47) | 중첩 데이터 | — | — |
| `primaryProficiencyId` | `string` | [51](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L51) | SOrelation | ContentReference(
         ContentReferenceKind.Proficiency,
         Aliases = "selector:active-combat\|selector:facility-primary") | — |
| `secondaryProficiencyId` | `string` | [55](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L55) | SOrelation | ContentReference(
         ContentReferenceKind.Proficiency,
         Aliases = "selector:active-combat\|selector:facility-secondary\|selector:higher-combat\|selector:rune-scholarship") | — |
| `secondaryProficiencyWeight` | `float` | [56](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L56) | 수치/설정 | — | — |
| `gameplayEffectTargetId` | `string` | [57](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L57) | semantic | — | R24 |
| `executionWorkTypeId` | `string` | [58](Assets/Scripts/Services/Foundation/CharacterPerformanceFormulaDefinitionSO.cs#L58) | semantic | — | R24 |

### `CharacterPotentialRarityProfile`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `potential` | `CharacterPotentialGrade` | [26](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L26) | enum | — | — |
| `rarityWeights` | `List<CharacterWeightedRarity>` | [27](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L27) | 중첩 데이터 | — | — |

### `CharacterPropAttachmentProfile`

도달 SO: `WorldInteractionPresentationCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `speciesOrAnatomyId` | `string` | [51](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L51) | relation-annotated | ContentReference(ContentReferenceKind.SpeciesOrAnatomy, Aliases = "default") | — |
| `carryKind` | `CharacterCarryVisualKind` | [52](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L52) | enum | — | — |
| `rightFacingOffsetPixels` | `Vector2` | [53](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L53) | Unity 값 | — | — |
| `mirrorOffsetX` | `bool` | [54](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L54) | 수치/설정 | — | — |
| `synchronizeFlipX` | `bool` | [55](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L55) | 수치/설정 | — | — |
| `sortingMode` | `CharacterPropSortingMode` | [56](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L56) | enum | — | — |
| `sortingOrderOffset` | `int` | [57](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L57) | 수치/설정 | — | — |
| `minimumBodyOverlapPixels` | `int` | [58](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L58) | 수치/설정 | — | — |
| `lightLoadScale` | `Vector3` | [59](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L59) | Unity 값 | — | — |
| `normalLoadScale` | `Vector3` | [60](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L60) | Unity 값 | — | — |
| `overloadedScale` | `Vector3` | [61](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L61) | Unity 값 | — | — |

### `CharacterRarityBudget`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `rarity` | `CharacterSkillRarity` | [33](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L33) | enum | — | — |
| `budget` | `int` | [34](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L34) | 수치/설정 | — | — |

### `CharacterSO`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `archetypeId` | `string` | [11](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L11) | ownID | ContentIdentity | R10 |
| `visualVariantId` | `string` | [12](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L12) | ownID | — | — |
| `characterType` | `CharacterType` | [13](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L13) | enum | — | — |
| `role` | `CharacterRole` | [14](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L14) | enum | — | — |
| `id` | `int` | [15](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L15) | ownID | ContentIdentity | — |
| `characterName` | `string` | [16](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L16) | display | — | — |
| `speciesTag` | `string` | [17](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L17) | SOrelation | ContentReference(ContentReferenceKind.Species) | R20 |
| `species` | `CharacterSpeciesSO` | [18](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L18) | directObject | — | R20 |
| `traits` | `CharacterTraitSO[]` | [19](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L19) | directObject | — | — |
| `defaultWorkPriorities` | `WorkPriorityProfile` | [20](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L20) | 중첩 데이터 | — | — |
| `aiPersonality` | `CharacterAiPersonality` | [21](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L21) | 중첩 데이터 | — | — |
| `ownerSummary` | `string` | [22](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L22) | display | — | — |
| `ownerPreferredWorkTypes` | `FacilityWorkType` | [23](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L23) | enum | — | — |
| `ownerFixedSkills` | `CharacterSkillInstance[]` | [24](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L24) | 중첩 데이터 | — | — |
| `characterSprite` | `Sprite` | [26](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L26) | directObject | — | — |
| `favoriteStore` | `BuildingSO[]` | [28](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L28) | directObject | — | — |
| `frequencyVisitMax` | `int` | [30](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L30) | 수치/설정 | — | — |
| `frequencyVisitMin` | `int` | [31](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L31) | 수치/설정 | — | — |
| `maxHoldingMoney` | `int` | [33](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L33) | 수치/설정 | — | — |
| `minHoldingMoney` | `int` | [34](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L34) | 수치/설정 | — | — |
| `speedType` | `CharacterSpeedType` | [36](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L36) | enum | — | — |
| `respawnSpeedType` | `CharacterRespawnSpeedType` | [37](Assets/Scripts/Services/Character/SO/CharacterSO.cs#L37) | enum | — | — |

### `CharacterSkillCapabilityFormulaDefinition`

도달 SO: `CharacterAcquiredTraitModuleSO`, `CharacterAcquiredTraitSettingsSO`, `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `appliedParameterIds` | `List<string>` | [104](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L104) | semantic | — | — |
| `baseCost` | `int` | [105](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L105) | 수치/설정 | — | — |
| `triggerFrequencyCostPerUnit` | `int` | [107](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L107) | 수치/설정 | — | — |
| `guaranteedProcCost` | `int` | [109](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L109) | 수치/설정 | — | — |
| `areaCostPerExtraTarget` | `int` | [111](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L111) | nonrelation | — | — |
| `multiEffectCostPerExtraEffect` | `int` | [113](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L113) | 수치/설정 | — | — |
| `pairSynergyCosts` | `List<CharacterSkillPairSynergyCostDefinition>` | [114](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L114) | 중첩 데이터 | — | — |
| `narrativeAffinity` | `float` | [116](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L116) | 수치/설정 | — | — |
| `affinityKeys` | `List<string>` | [117](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L117) | semantic | — | — |
| `conflictGroups` | `List<string>` | [118](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L118) | semantic | — | — |
| `forbiddenSynergies` | `List<string>` | [119](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L119) | semantic | — | R23 |
| `formatterId` | `string` | [120](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L120) | semantic | — | — |
| `applicatorId` | `string` | [121](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L121) | semantic | — | — |
| `parameterRanges` | `List<CharacterSkillFormulaAxisDefinition>` | [122](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L122) | 중첩 데이터 | — | — |

### `CharacterSkillDrawbackCapabilityDefinition`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `drawbackId` | `string` | [272](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L272) | ownID | — | — |
| `capabilityId` | `string` | [273](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L273) | semantic | — | — |
| `displayName` | `string` | [274](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L274) | display | — | — |
| `description` | `string` | [275](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L275) | display | — | — |
| `applicationKind` | `CharacterSkillDrawbackApplicationKind` | [276](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L276) | enum | — | — |
| `effectDefinition` | `GameplayEffectDefinitionSO` | [278](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L278) | directObject | — | — |
| `statDeltaPerCredit` | `float` | [279](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L279) | 수치/설정 | — | — |
| `allowedKinds` | `List<CharacterSkillKind>` | [280](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L280) | enum | — | — |
| `allowedTriggers` | `List<CharacterSkillTrigger>` | [281](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L281) | enum | — | — |
| `domainAffinities` | `List<CharacterNarrativeDomain>` | [282](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L282) | enum | — | — |
| `maximumCredit` | `int` | [283](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L283) | 수치/설정 | — | — |
| `formula` | `CharacterSkillCapabilityFormulaDefinition` | [284](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L284) | 중첩 데이터 | — | — |

### `CharacterSkillDrawbackEffectEnvelope`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `drawbackModuleId` | `string` | [123](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L123) | runtimeSnapshot | — | — |
| `effectId` | `string` | [124](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L124) | runtimeSnapshot | — | — |
| `targetId` | `string` | [125](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L125) | runtimeSnapshot | — | — |
| `operation` | `GameplayEffectOperation` | [126](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L126) | enum | — | — |
| `value` | `float` | [127](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L127) | 수치/설정 | — | — |
| `severityUnits` | `int` | [128](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L128) | 수치/설정 | — | — |
| `displayName` | `string` | [129](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L129) | runtimeSnapshot | — | — |

### `CharacterSkillFormulaAxisDefinition`

도달 SO: `CharacterAcquiredTraitModuleSO`, `CharacterAcquiredTraitSettingsSO`, `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `parameterId` | `string` | [74](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L74) | semantic | — | — |
| `minimumUnits` | `long` | [75](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L75) | 수치/설정 | — | — |
| `maximumUnits` | `long` | [76](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L76) | 수치/설정 | — | — |
| `quantumUnits` | `long` | [77](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L77) | 수치/설정 | — | — |
| `decimalPlaces` | `int` | [78](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L78) | 수치/설정 | — | — |
| `costPerQuantum` | `int` | [79](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L79) | 수치/설정 | — | — |

### `CharacterSkillFormulaCapabilityEnvelope`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capabilityId` | `string` | [415](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L415) | runtimeSnapshot | — | — |
| `formatterId` | `string` | [416](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L416) | runtimeSnapshot | — | — |
| `applicatorId` | `string` | [417](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L417) | runtimeSnapshot | — | — |
| `parameters` | `List<CharacterSkillFormulaParameter>` | [418](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L418) | 중첩 데이터 | — | — |

### `CharacterSkillFormulaParameter`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `parameterId` | `string` | [399](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L399) | runtimeSnapshot | — | — |
| `units` | `long` | [400](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L400) | 수치/설정 | — | — |

### `CharacterSkillFormulaPolicyDefinition`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `formulaVersion` | `int` | [174](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L174) | 수치/설정 | — | — |
| `baseBudget` | `int` | [175](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L175) | 수치/설정 | — | — |
| `powerScale` | `int` | [176](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L176) | 수치/설정 | — | — |
| `softCapK` | `float` | [177](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L177) | 수치/설정 | — | — |
| `minimumImportance` | `float` | [178](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L178) | 수치/설정 | — | — |
| `maximumImportance` | `float` | [179](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L179) | 수치/설정 | — | — |
| `milestoneWeights` | `List<float>` | [180](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L180) | 수치/설정 | — | — |
| `drawbackCredit` | `NarrativeFormulaDrawbackCreditPolicyDefinition` | [181](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L181) | 중첩 데이터 | — | — |
| `triggerCosts` | `List<CharacterSkillTriggerFormulaCostDefinition>` | [183](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L183) | 중첩 데이터 | — | — |
| `targetCosts` | `List<CharacterSkillTargetFormulaCostDefinition>` | [186](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L186) | semantic | — | — |
| `catalogSha256` | `string` | [189](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L189) | semantic | — | — |

### `CharacterSkillInstance`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [165](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L165) | runtimeSnapshot | — | — |
| `ruleId` | `string` | [166](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L166) | runtimeSnapshot | — | — |
| `combinationId` | `string` | [167](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L167) | runtimeSnapshot | — | — |
| `displayName` | `string` | [168](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L168) | runtimeSnapshot | — | — |
| `description` | `string` | [169](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L169) | runtimeSnapshot | — | — |
| `narrativeReason` | `string` | [170](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L170) | runtimeSnapshot | — | — |
| `kind` | `CharacterSkillKind` | [171](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L171) | enum | — | — |
| `rarity` | `CharacterSkillRarity` | [172](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L172) | enum | — | — |
| `trigger` | `CharacterSkillTrigger` | [173](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L173) | enum | — | — |
| `target` | `CharacterSkillTarget` | [174](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L174) | runtimeSnapshot | — | — |
| `targetingMode` | `CharacterSkillTargetingMode` | [175](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L175) | runtimeSnapshot | — | — |
| `effectArea` | `CharacterSkillEffectArea` | [176](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L176) | enum | — | — |
| `areaSize` | `int` | [177](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L177) | 수치/설정 | — | — |
| `ultimateDomain` | `CharacterUltimateDomain` | [178](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L178) | enum | — | — |
| `cooldownTurns` | `int` | [179](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L179) | 수치/설정 | — | — |
| `manualDurationHours` | `int` | [180](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L180) | 수치/설정 | — | — |
| `manualCooldownDays` | `int` | [181](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L181) | 수치/설정 | — | — |
| `usableFrom` | `OffenseFormationMask` | [182](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L182) | enum | — | — |
| `targetPositions` | `OffenseFormationMask` | [183](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L183) | runtimeSnapshot | — | — |
| `modules` | `List<CharacterSkillModuleSelection>` | [184](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L184) | 중첩 데이터 | — | — |
| `requestKey` | `string` | [185](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L185) | runtimeSnapshot | — | — |
| `narrativeTrace` | `NarrativeGenerationTrace` | [186](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L186) | 중첩 데이터 | — | — |
| `formulaVersion` | `int` | [188](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L188) | 수치/설정 | — | — |
| `prominencePolicyVersion` | `int` | [189](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L189) | 수치/설정 | — | — |
| `primaryCapabilityId` | `string` | [190](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L190) | runtimeSnapshot | — | — |
| `formulaCatalogSha256` | `string` | [191](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L191) | runtimeSnapshot | — | — |
| `calculatedCost` | `int` | [192](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L192) | 수치/설정 | — | — |
| `positiveCost` | `int` | [193](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L193) | 수치/설정 | — | — |
| `drawbackCredit` | `int` | [194](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L194) | 수치/설정 | — | — |
| `drawbackId` | `string` | [195](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L195) | runtimeSnapshot | — | — |
| `drawbackEffect` | `CharacterSkillDrawbackEffectEnvelope` | [196](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L196) | 중첩 데이터 | — | — |
| `narrativeBudget` | `int` | [197](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L197) | 수치/설정 | — | — |
| `drawbackEvidenceQualified` | `bool` | [198](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L198) | runtimeSnapshot | — | — |
| `evidenceIds` | `List<string>` | [199](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L199) | runtimeSnapshot | — | — |
| `evidenceBindings` | `List<GameplayOutcomeEvidenceBindingSnapshot>` | [200](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L200) | runtimeSnapshot | — | — |
| `formulaCapabilities` | `List<CharacterSkillFormulaCapabilityEnvelope>` | [201](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L201) | 중첩 데이터 | — | — |
| `presentationId` | `string` | [203](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L203) | runtimeSnapshot | — | — |
| `mechanicalDescription` | `string` | [204](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L204) | runtimeSnapshot | — | — |
| `narrativeFlavor` | `string` | [205](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L205) | runtimeSnapshot | — | — |

### `CharacterSkillModuleRule`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [439](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L439) | ownID | — | — |
| `capabilityId` | `string` | [441](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L441) | semantic | — | — |
| `displayName` | `string` | [442](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L442) | display | — | — |
| `allowedKinds` | `List<CharacterSkillKind>` | [443](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L443) | enum | — | — |
| `allowedTriggers` | `List<CharacterSkillTrigger>` | [444](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L444) | enum | — | — |
| `allowedTargets` | `List<CharacterSkillTarget>` | [445](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L445) | nonrelation | — | — |
| `variants` | `List<CharacterSkillNumericVariant>` | [446](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L446) | 중첩 데이터 | — | — |
| `formula` | `CharacterSkillCapabilityFormulaDefinition` | [448](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L448) | 중첩 데이터 | — | — |

### `CharacterSkillModuleSelection`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `moduleId` | `string` | [107](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L107) | runtimeSnapshot | — | — |
| `variantId` | `string` | [108](Assets/Scripts/Services/Character/Core/CharacterSkillDefinitionData.cs#L108) | runtimeSnapshot | — | — |

### `CharacterSkillNumericVariant`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [40](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L40) | ownID | — | — |
| `displayName` | `string` | [41](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L41) | display | — | — |
| `primaryValue` | `float` | [42](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L42) | 수치/설정 | — | — |
| `secondaryValue` | `float` | [43](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L43) | 수치/설정 | — | — |
| `duration` | `int` | [44](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L44) | 수치/설정 | — | — |
| `count` | `int` | [45](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L45) | 수치/설정 | — | — |
| `cost` | `int` | [46](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L46) | 수치/설정 | — | — |

### `CharacterSkillPairSynergyCostDefinition`

도달 SO: `CharacterAcquiredTraitModuleSO`, `CharacterAcquiredTraitSettingsSO`, `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `otherCapabilityId` | `string` | [96](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L96) | semantic | — | R23 |
| `cost` | `int` | [97](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L97) | 수치/설정 | — | — |

### `CharacterSkillSystemSettingsSO`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `maxLevel` | `int` | [488](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L488) | 수치/설정 | — | — |
| `initialStatTotal` | `int` | [489](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L489) | 수치/설정 | — | — |
| `initialStatMin` | `int` | [490](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L490) | 수치/설정 | — | — |
| `initialStatMax` | `int` | [491](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L491) | 수치/설정 | — | — |
| `levelGrowthStatCap` | `int` | [492](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L492) | 수치/설정 | — | — |
| `identityGrowthWeight` | `float` | [493](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L493) | nonrelation | — | — |
| `secondPassiveMinimumLevel` | `int` | [494](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L494) | 수치/설정 | — | — |
| `secondPassiveMinimumRecords` | `int` | [495](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L495) | 수치/설정 | — | — |
| `secondPassiveMinimumDomains` | `int` | [496](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L496) | 수치/설정 | — | — |
| `activeUnlockLevels` | `int[]` | [497](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L497) | 수치/설정 | — | — |
| `potentialPopulationWeights` | `float[]` | [500](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L500) | 수치/설정 | — | — |
| `potentialRarityProfiles` | `List<CharacterPotentialRarityProfile>` | [501](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L501) | semantic | — | — |
| `missedUpperRarityMultiplier` | `float` | [503](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L503) | 수치/설정 | — | — |
| `initialRetrySeconds` | `float` | [506](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L506) | 수치/설정 | — | — |
| `maximumRetrySeconds` | `float` | [507](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L507) | 수치/설정 | — | — |
| `guestReadyTarget` | `int` | [508](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L508) | nonrelation | — | — |
| `guestReadyLowWatermark` | `int` | [509](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L509) | 수치/설정 | — | — |
| `maximumAliveNonStaffGuests` | `int` | [510](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L510) | 수치/설정 | — | — |
| `rarityBudgets` | `List<CharacterRarityBudget>` | [511](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L511) | 중첩 데이터 | — | — |
| `traitConflicts` | `List<CharacterTraitConflictRule>` | [512](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L512) | semantic | — | — |
| `formulaPolicy` | `CharacterSkillFormulaPolicyDefinition` | [515](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L515) | 중첩 데이터 | — | — |
| `drawbackCapabilities` | `List<CharacterSkillDrawbackCapabilityDefinition>` | [519](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L519) | 중첩 데이터 | — | — |
| `modules` | `List<CharacterSkillModuleRule>` | [523](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L523) | 중첩 데이터 | — | — |

### `CharacterSkillTargetFormulaCostDefinition`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `target` | `CharacterSkillTarget` | [167](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L167) | nonrelation | — | — |
| `targetCount` | `int` | [168](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L168) | nonrelation | — | — |

### `CharacterSkillTriggerFormulaCostDefinition`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `trigger` | `CharacterSkillTrigger` | [159](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L159) | enum | — | — |
| `frequencyUnits` | `int` | [160](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L160) | 수치/설정 | — | — |
| `guaranteedProc` | `bool` | [161](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L161) | 수치/설정 | — | — |

### `CharacterSpeciesDefinitionSO`

도달 SO: `CharacterSpeciesDefinitionSO`, `CharacterSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `int` | [240](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L240) | ownID | — | — |
| `speciesTag` | `string` | [241](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L241) | ownID | ContentIdentity | — |
| `displayName` | `string` | [242](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L242) | display | — | — |
| `ownerSelectionPolicy` | `SpeciesOwnerSelectionPolicy` | [243](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L243) | enum | — | — |
| `homeFactionId` | `string` | [245](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L245) | SOrelation | ContentReference(ContentReferenceKind.Faction) | — |
| `anatomyProfileId` | `string` | [247](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L247) | SOrelation | ContentReference(ContentReferenceKind.AnatomyProfile) | R17 |
| `needs` | `SpeciesNeedProfile` | [248](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L248) | 중첩 데이터 | — | — |
| `environment` | `SpeciesEnvironmentProfile` | [249](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L249) | 중첩 데이터 | — | — |
| `lifeHistory` | `SpeciesLifeHistorySO` | [250](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L250) | directObject | — | — |
| `reproduction` | `ReproductionProfileSO` | [251](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L251) | directObject | — | — |
| `funeralCulture` | `FuneralCultureSO` | [252](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L252) | directObject | — | — |
| `relationTags` | `string[]` | [253](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L253) | semantic | — | — |
| `defenseAffinityTags` | `string[]` | [254](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L254) | semantic | — | — |
| `strongWorkTypeIds` | `string[]` | [255](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L255) | semantic | — | — |
| `weakWorkTypeIds` | `string[]` | [256](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L256) | semantic | — | — |
| `shortDescription` | `string` | [257](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L257) | display | — | — |
| `description` | `string` | [258](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L258) | display | — | — |
| `preferredFacilityLabels` | `string[]` | [259](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L259) | semantic | — | — |
| `dislikedEnvironmentLabels` | `string[]` | [260](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L260) | semantic | — | — |
| `stayDurationMultiplier` | `float` | [261](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L261) | 수치/설정 | — | — |
| `crimeRiskMultiplier` | `float` | [262](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L262) | 수치/설정 | — | — |
| `incidentType` | `CharacterSpeciesIncidentType` | [263](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L263) | semantic | — | — |
| `incident` | `SpeciesIncidentDefinition` | [264](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L264) | semantic | — | — |
| `incidentName` | `string` | [265](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L265) | semantic | — | — |
| `incidentDescription` | `string` | [266](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L266) | display | — | — |
| `incidentMitigatingRoles` | `FacilityRole` | [267](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L267) | nonrelation | — | — |
| `combatPassive` | `SpeciesPassiveDefinition` | [268](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L268) | 중첩 데이터 | — | — |

### `CharacterSpeciesSO`

도달 SO: `CharacterSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `modifiers` | `CharacterModelModifiers` | [8](Assets/Scripts/Models/Species/Core/CharacterSpeciesSO.cs#L8) | 중첩 데이터 | — | — |
| `combatAbilities` | `CharacterCombatAbilityCollection` | [9](Assets/Scripts/Models/Species/Core/CharacterSpeciesSO.cs#L9) | 중첩 데이터 | — | — |
| `effects` | `System.Collections.Generic.List<GameplayEffectBinding>` | [10](Assets/Scripts/Models/Species/Core/CharacterSpeciesSO.cs#L10) | 중첩 데이터 | — | — |

### `CharacterStartingHistorySO`

도달 SO: `CharacterStartingHistorySO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `historyId` | `string` | [11](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L11) | ownID | ContentIdentity | — |
| `displayName` | `string` | [12](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L12) | display | — | — |
| `description` | `string` | [13](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L13) | display | — | — |
| `primaryProficiencyId` | `string` | [15](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L15) | SOrelation | ContentReference(ContentReferenceKind.Proficiency) | — |
| `secondaryProficiencyId` | `string` | [17](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L17) | SOrelation | ContentReference(ContentReferenceKind.Proficiency) | — |
| `primaryBaseExperience` | `int` | [18](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L18) | 수치/설정 | — | — |
| `secondaryBaseExperience` | `int` | [19](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L19) | 수치/설정 | — | — |
| `proficiencyBonuses` | `List<CharacterStartingProficiencyBonus>` | [20](Assets/Scripts/Models/Characters/CharacterStartingHistorySO.cs#L20) | 중첩 데이터 | — | — |

### `CharacterStartingOriginSO`

도달 SO: `CharacterStartingOriginSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `originId` | `string` | [11](Assets/Scripts/Models/Characters/CharacterStartingOriginSO.cs#L11) | ownID | ContentIdentity | — |
| `displayName` | `string` | [12](Assets/Scripts/Models/Characters/CharacterStartingOriginSO.cs#L12) | display | — | — |
| `description` | `string` | [13](Assets/Scripts/Models/Characters/CharacterStartingOriginSO.cs#L13) | display | — | — |
| `proficiencyBonuses` | `List<CharacterStartingProficiencyBonus>` | [14](Assets/Scripts/Models/Characters/CharacterStartingOriginSO.cs#L14) | 중첩 데이터 | — | — |

### `CharacterStartingProficiencyBonus`

도달 SO: `CharacterStartingHistorySO`, `CharacterStartingOriginSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `proficiencyId` | `string` | [46](Assets/Scripts/Models/Characters/CharacterStartingProfileDomain.cs#L46) | SOrelation | ContentReference(ContentReferenceKind.Proficiency) | — |
| `experience` | `int` | [47](Assets/Scripts/Models/Characters/CharacterStartingProfileDomain.cs#L47) | 수치/설정 | — | — |

### `CharacterTraitBehaviorPreference`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `behaviorTag` | `string` | [146](Assets/Scripts/Content/CharacterTraitSO.cs#L146) | semantic | — | — |
| `utilityDelta` | `float` | [147](Assets/Scripts/Content/CharacterTraitSO.cs#L147) | 수치/설정 | — | — |

### `CharacterTraitConflictRule`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `firstTraitId` | `int` | [479](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L479) | SOrelation | ContentReference("CharacterTraitSO", "id", emptyNumericId: 0) | — |
| `secondTraitId` | `int` | [481](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L481) | SOrelation | ContentReference("CharacterTraitSO", "id", emptyNumericId: 0) | — |

### `CharacterTraitEventWeight`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `eventCategoryId` | `string` | [166](Assets/Scripts/Content/CharacterTraitSO.cs#L166) | semantic | — | — |
| `multiplier` | `float` | [167](Assets/Scripts/Content/CharacterTraitSO.cs#L167) | 수치/설정 | — | — |

### `CharacterTraitMoodReaction`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `triggerTag` | `string` | [157](Assets/Scripts/Content/CharacterTraitSO.cs#L157) | semantic | — | — |
| `moodDelta` | `float` | [158](Assets/Scripts/Content/CharacterTraitSO.cs#L158) | 수치/설정 | — | — |
| `durationDays` | `int` | [159](Assets/Scripts/Content/CharacterTraitSO.cs#L159) | 수치/설정 | — | — |

### `CharacterTraitSO`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `traitName` | `string` | [27](Assets/Scripts/Content/CharacterTraitSO.cs#L27) | semantic | — | — |
| `description` | `string` | [28](Assets/Scripts/Content/CharacterTraitSO.cs#L28) | display | — | — |
| `modifiers` | `CharacterModelModifiers` | [29](Assets/Scripts/Content/CharacterTraitSO.cs#L29) | 중첩 데이터 | — | — |
| `combatAbilities` | `CharacterCombatAbilityCollection` | [30](Assets/Scripts/Content/CharacterTraitSO.cs#L30) | 중첩 데이터 | — | — |
| `environmentalProtection` | `ThermalProtectionProfile` | [31](Assets/Scripts/Content/CharacterTraitSO.cs#L31) | 중첩 데이터 | — | — |
| `selectionRarity` | `CharacterTraitSelectionRarity` | [33](Assets/Scripts/Content/CharacterTraitSO.cs#L33) | enum | — | — |
| `polarity` | `CharacterTraitPolarity` | [34](Assets/Scripts/Content/CharacterTraitSO.cs#L34) | enum | — | — |
| `selectionFamilyId` | `string` | [35](Assets/Scripts/Content/CharacterTraitSO.cs#L35) | semantic | — | — |
| `eligibleSpeciesTags` | `List<string>` | [37](Assets/Scripts/Content/CharacterTraitSO.cs#L37) | SOrelation | ContentReference(ContentReferenceKind.Species) | — |
| `earnedWorkExperienceMultiplier` | `float` | [38](Assets/Scripts/Content/CharacterTraitSO.cs#L38) | 수치/설정 | — | — |
| `incompatibilityGroups` | `List<string>` | [39](Assets/Scripts/Content/CharacterTraitSO.cs#L39) | semantic | — | — |
| `behaviorPreferences` | `List<CharacterTraitBehaviorPreference>` | [40](Assets/Scripts/Content/CharacterTraitSO.cs#L40) | semantic | — | — |
| `moodReactions` | `List<CharacterTraitMoodReaction>` | [41](Assets/Scripts/Content/CharacterTraitSO.cs#L41) | 중첩 데이터 | — | — |
| `eventWeights` | `List<CharacterTraitEventWeight>` | [42](Assets/Scripts/Content/CharacterTraitSO.cs#L42) | 중첩 데이터 | — | — |
| `effects` | `List<GameplayEffectBinding>` | [43](Assets/Scripts/Content/CharacterTraitSO.cs#L43) | 중첩 데이터 | — | — |
| `identityRules` | `List<CharacterIdentityRule>` | [44](Assets/Scripts/Content/CharacterTraitSO.cs#L44) | semantic | — | — |

### `CharacterWeightedRarity`

도달 SO: `CharacterSkillSystemSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `rarity` | `CharacterSkillRarity` | [10](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L10) | enum | — | — |
| `weight` | `float` | [11](Assets/Scripts/Services/Character/SO/CharacterSkillSystemSettingsSO.cs#L11) | 수치/설정 | — | — |

### `ClimateZoneDefinitionSO`

도달 SO: `ClimateZoneDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stableId` | `string` | [9](Assets/Scripts/Content/ClimateZoneDefinitionSO.cs#L9) | self-key | ContentIdentity | — |
| `displayName` | `string` | [10](Assets/Scripts/Content/ClimateZoneDefinitionSO.cs#L10) | display-text | — | — |
| `meanTemperatureC` | `float` | [11](Assets/Scripts/Content/ClimateZoneDefinitionSO.cs#L11) | 수치/설정 | — | — |
| `annualAmplitudeC` | `float` | [12](Assets/Scripts/Content/ClimateZoneDefinitionSO.cs#L12) | 수치/설정 | — | — |
| `localHourOffset` | `int` | [13](Assets/Scripts/Content/ClimateZoneDefinitionSO.cs#L13) | 수치/설정 | — | — |

### `CombatArmorPartValue`

도달 SO: `CombatArmorSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `bodyPart` | `CombatBodyPart` | [294](Assets/Scripts/Models/Combat/Core/CombatModels.cs#L294) | enum | — | — |
| `slashDefense` | `float` | [295](Assets/Scripts/Models/Combat/Core/CombatModels.cs#L295) | 수치/설정 | — | — |
| `pierceDefense` | `float` | [296](Assets/Scripts/Models/Combat/Core/CombatModels.cs#L296) | 수치/설정 | — | — |
| `bluntDefense` | `float` | [297](Assets/Scripts/Models/Combat/Core/CombatModels.cs#L297) | 수치/설정 | — | — |

### `CombatArmorSO`

도달 SO: `CombatArmorSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `layer` | `CombatArmorLayer` | [9](Assets/Scripts/Models/Economy/Content/CombatArmorSO.cs#L9) | enum | — | — |
| `collisionTag` | `string` | [10](Assets/Scripts/Models/Economy/Content/CombatArmorSO.cs#L10) | semantic-key | — | — |
| `bodyPartDefense` | `List<CombatArmorPartValue>` | [11](Assets/Scripts/Models/Economy/Content/CombatArmorSO.cs#L11) | 중첩 데이터 | — | — |

### `CombatAttackVerb`

도달 SO: `CombatWeaponSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `attackTime` | `float` | [11](Assets/Scripts/Models/Combat/Core/CombatWeaponPrimitives.cs#L11) | 수치/설정 | — | — |
| `baseDamage` | `float` | [12](Assets/Scripts/Models/Combat/Core/CombatWeaponPrimitives.cs#L12) | 수치/설정 | — | — |
| `penetration` | `float` | [13](Assets/Scripts/Models/Combat/Core/CombatWeaponPrimitives.cs#L13) | 수치/설정 | — | — |
| `damageType` | `CombatDamageType` | [14](Assets/Scripts/Models/Combat/Core/CombatWeaponPrimitives.cs#L14) | enum | — | — |
| `tracking` | `float` | [15](Assets/Scripts/Models/Combat/Core/CombatWeaponPrimitives.cs#L15) | 수치/설정 | — | — |

### `CombatEquipmentCraftMaterial`

도달 SO: `CombatArmorSO`, `CombatEquipmentDefinitionSO`, `CombatShieldSO`, `CombatWeaponSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `category` | `StockCategory` | [133](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L133) | enum | — | — |
| `amount` | `int` | [134](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L134) | 수치/설정 | — | — |

### `CombatEquipmentDefinitionSO`

도달 SO: `CombatArmorSO`, `CombatEquipmentDefinitionSO`, `CombatShieldSO`, `CombatWeaponSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `equipmentId` | `string` | [11](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L11) | self-key | ContentIdentity | — |
| `displayName` | `string` | [12](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L12) | authored-value | — | — |
| `description` | `string` | [13](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L13) | authored-value | — | — |
| `itemId` | `string` | [15](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L15) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `weight` | `float` | [16](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L16) | 수치/설정 | — | — |
| `occupiedHands` | `int` | [17](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L17) | 수치/설정 | — | — |
| `maxDurability` | `float` | [18](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L18) | authored-value | — | — |
| `requiredCraftWork` | `float` | [19](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L19) | 수치/설정 | — | — |
| `defaultMaterialId` | `string` | [21](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L21) | relation-annotated | ContentReference("CraftMaterialDefinitionSO", "MaterialId") | — |
| `primaryMaterialAmount` | `int` | [22](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L22) | 수치/설정 | — | — |
| `allowedMaterialFamilies` | `List<CombatMaterialFamily>` | [23](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L23) | enum | — | — |
| `craftMaterials` | `List<CombatEquipmentCraftMaterial>` | [25](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L25) | 중첩 데이터 | — | — |
| `requiredComponentInputs` | `List<ItemAmountDefinition>` | [27](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L27) | 중첩 데이터 | — | — |
| `requiredResearchId` | `string` | [30](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L30) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |
| `era` | `EquipmentEra` | [31](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L31) | enum | — | — |
| `tier` | `int` | [32](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L32) | 수치/설정 | — | — |
| `slotProfile` | `EquipmentSlotProfile` | [33](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L33) | authored-value | — | — |
| `lineageKind` | `EquipmentLineageKind` | [34](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L34) | enum | — | — |
| `growthEquipment` | `bool` | [35](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L35) | 수치/설정 | — | — |
| `growthBaseStatMultiplier` | `float` | [36](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L36) | 수치/설정 | — | — |
| `proficiency` | `ProficiencyWorkProfileAuthoring` | [37](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L37) | 중첩 데이터 | — | — |
| `effects` | `List<GameplayEffectBinding>` | [38](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L38) | 중첩 데이터 | — | — |
| `allowMythicInspiration` | `bool` | [39](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L39) | 수치/설정 | — | — |
| `roleFlags` | `CombatEquipmentRoleFlags` | [40](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L40) | enum | — | — |
| `arcaneManaCost` | `float` | [41](Assets/Scripts/Models/Economy/Content/CombatEquipmentDefinitions.cs#L41) | 수치/설정 | — | — |

### `CombatRangeProfile`

도달 SO: `CombatWeaponSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `band` | `CombatRangeBand` | [285](Assets/Scripts/Models/Combat/Core/CombatModels.cs#L285) | enum | — | — |
| `accuracyMultiplier` | `float` | [286](Assets/Scripts/Models/Combat/Core/CombatModels.cs#L286) | 수치/설정 | — | — |
| `damageMultiplier` | `float` | [287](Assets/Scripts/Models/Combat/Core/CombatModels.cs#L287) | 수치/설정 | — | — |

### `CombatShieldSO`

도달 SO: `CombatShieldSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `frontalBlockChance` | `float` | [8](Assets/Scripts/Models/Economy/Content/CombatShieldSO.cs#L8) | 수치/설정 | — | — |
| `slashDefense` | `float` | [9](Assets/Scripts/Models/Economy/Content/CombatShieldSO.cs#L9) | 수치/설정 | — | — |
| `pierceDefense` | `float` | [10](Assets/Scripts/Models/Economy/Content/CombatShieldSO.cs#L10) | 수치/설정 | — | — |
| `bluntDefense` | `float` | [11](Assets/Scripts/Models/Economy/Content/CombatShieldSO.cs#L11) | 수치/설정 | — | — |

### `CombatWeaponSO`

도달 SO: `CombatWeaponSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `verbs` | `List<CombatAttackVerb>` | [10](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L10) | 중첩 데이터 | — | — |
| `rangeProfiles` | `List<CombatRangeProfile>` | [11](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L11) | nested-authored-value | — | — |
| `maximumRange` | `int` | [12](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L12) | 수치/설정 | — | — |
| `compatibleAmmunitionItemIds` | `List<string>` | [14](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L14) | relation-annotated | ContentReference(ContentReferenceKind.Ammunition) | — |
| `magazineCapacity` | `int` | [15](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L15) | 수치/설정 | — | — |
| `reloadSeconds` | `float` | [16](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L16) | 수치/설정 | — | — |
| `supportsAimed` | `bool` | [17](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L17) | 수치/설정 | — | — |
| `supportsRapid` | `bool` | [18](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L18) | semantic-key | — | — |
| `supportsSuppressive` | `bool` | [19](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L19) | 수치/설정 | — | — |
| `gunpowderWeapon` | `bool` | [20](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L20) | 수치/설정 | — | — |
| `maximumMisfireChance` | `float` | [21](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L21) | 수치/설정 | — | — |
| `smokeExposure` | `float` | [22](Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs#L22) | 수치/설정 | — | — |

### `Consideration`

도달 SO: `Consideration`, `ConsiderationCanLookAround`, `ConsiderationFacilityNeed`, `ConsiderationIsVisitable`, `ConsiderationRandom`, `ConsiderationShoppingCount`, `ConsiderationShouldExitDungeon`, `ConsiderationStat`, `ConsiderationWorkNeed`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `considerationName` | `string` | [9](Assets/Scripts/Services/Character/AI/Consideration/Consideration.cs#L9) | display-text | — | — |

### `ConsiderationFacilityNeed`

도달 SO: `ConsiderationFacilityNeed`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `role` | `FacilityRole` | [6](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationFacilityNeedAdapter.cs#L6) | enum | — | — |
| `minimumScoreWhenAvailable` | `float` | [7](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationFacilityNeedAdapter.cs#L7) | 수치/설정 | — | — |

### `ConsiderationIsVisitable`

도달 SO: `ConsiderationIsVisitable`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `role` | `FacilityRole` | [5](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationIsVisitableAdapter.cs#L5) | enum | — | — |

### `ConsiderationRandom`

도달 SO: `ConsiderationRandom`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `maxNum` | `float` | [6](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationRandomAdapter.cs#L6) | 수치/설정 | — | — |
| `minNum` | `float` | [7](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationRandomAdapter.cs#L7) | 수치/설정 | — | — |

### `ConsiderationStat`

도달 SO: `ConsiderationStat`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `affectedStat` | `CharacterCondition` | [8](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationStatAdapter.cs#L8) | enum | — | — |
| `curve` | `AnimationCurve` | [9](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationStatAdapter.cs#L9) | Unity 값 | — | — |

### `ConsiderationWorkNeed`

도달 SO: `ConsiderationWorkNeed`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workType` | `FacilityWorkType` | [6](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationWorkNeedAdapter.cs#L6) | enum | — | — |
| `minimumScoreWhenAvailable` | `float` | [7](Assets/Scripts/Services/Infrastructure/AI/Considerations/ConsiderationWorkNeedAdapter.cs#L7) | 수치/설정 | — | — |

### `CraftMaterialDefinitionSO`

도달 SO: `CraftMaterialDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `materialId` | `string` | [11](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L11) | self-key | ContentIdentity | — |
| `itemId` | `string` | [13](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L13) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `displayName` | `string` | [14](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L14) | authored-value | — | — |
| `family` | `CombatMaterialFamily` | [15](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L15) | enum | — | — |
| `damageMultiplier` | `float` | [16](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L16) | 수치/설정 | — | — |
| `penetrationDefenseMultiplier` | `float` | [17](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L17) | 수치/설정 | — | — |
| `durabilityMultiplier` | `float` | [18](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L18) | authored-value | — | — |
| `weightMultiplier` | `float` | [19](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L19) | 수치/설정 | — | — |
| `valueMultiplier` | `float` | [20](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L20) | 수치/설정 | — | — |
| `insulation` | `float` | [21](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L21) | 수치/설정 | — | — |
| `mentalResistance` | `float` | [22](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L22) | 수치/설정 | — | — |
| `arcaneResistance` | `float` | [23](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L23) | 수치/설정 | — | — |
| `tint` | `Color` | [24](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L24) | Unity 값 | — | — |
| `rareMaterial` | `bool` | [25](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L25) | 수치/설정 | — | — |
| `requiredResearchId` | `string` | [27](Assets/Scripts/Models/Economy/Content/CraftMaterialDefinitionSO.cs#L27) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |

### `CropDefinitionSO`

도달 SO: `CropDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `cropId` | `string` | [75](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L75) | self-key | ContentIdentity | — |
| `displayName` | `string` | [76](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L76) | authored-value | — | — |
| `harvestItemId` | `string` | [78](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L78) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `seedItemId` | `string` | [80](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L80) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `baseGenome` | `CropGenomeDefinitionSO` | [81](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L81) | direct-object-reference | — | — |
| `familyGroup` | `CropFamilyGroup` | [82](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L82) | enum | — | — |
| `endemicDisease` | `CropDiseaseKind` | [83](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L83) | enum | — | — |
| `requiredResearchId` | `string` | [85](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L85) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |
| `growthHours` | `float` | [86](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L86) | 수치/설정 | — | — |
| `sowWork` | `float` | [87](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L87) | 수치/설정 | — | — |
| `harvestWork` | `float` | [88](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L88) | 수치/설정 | — | — |
| `dailyWater` | `float` | [89](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L89) | 수치/설정 | — | — |
| `yield` | `int` | [90](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L90) | 수치/설정 | — | — |
| `indoorAllowed` | `bool` | [91](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L91) | 수치/설정 | — | — |
| `temperatureRange` | `Vector2` | [92](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L92) | Unity 값 | — | — |
| `lightProfile` | `CropLightProfile` | [93](Assets/Scripts/Models/Economy/Content/CropDefinitionSO.cs#L93) | authored-value | — | — |

### `CropGenomeDefinitionSO`

도달 SO: `CropGenomeDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `genomeId` | `string` | [10](Assets/Scripts/Models/Economy/Content/CropGenomeDefinitionSO.cs#L10) | self-key | ContentIdentity | — |
| `cropId` | `string` | [12](Assets/Scripts/Models/Economy/Content/CropGenomeDefinitionSO.cs#L12) | relation-annotated | ContentReference("CropDefinitionSO", "CropId") | — |
| `cultivarName` | `string` | [13](Assets/Scripts/Models/Economy/Content/CropGenomeDefinitionSO.cs#L13) | authored-value | — | — |
| `description` | `string` | [14](Assets/Scripts/Models/Economy/Content/CropGenomeDefinitionSO.cs#L14) | authored-value | — | — |
| `authoringRevision` | `int` | [15](Assets/Scripts/Models/Economy/Content/CropGenomeDefinitionSO.cs#L15) | 수치/설정 | — | — |
| `tradeoffTags` | `List<string>` | [16](Assets/Scripts/Models/Economy/Content/CropGenomeDefinitionSO.cs#L16) | semantic-key | — | — |
| `loci` | `List<DiploidLocusSaveData>` | [17](Assets/Scripts/Models/Economy/Content/CropGenomeDefinitionSO.cs#L17) | 중첩 데이터 | — | — |

### `CropTreatmentItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `treatmentKind` | `CropTreatmentKind` | [279](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L279) | enum | — | — |
| `quantityPerApplication` | `int` | [280](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L280) | 수치/설정 | — | — |
| `requiredWork` | `float` | [281](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L281) | 수치/설정 | — | — |
| `effectAmount` | `float` | [282](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L282) | 수치/설정 | — | — |
| `cooldownDays` | `int` | [283](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L283) | 수치/설정 | — | — |

### `CulturalPracticeDefinitionSO`

도달 SO: `CulturalPracticeDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `cultureId` | `string` | [10](Assets/Scripts/Content/CulturalPracticeDefinitionSO.cs#L10) | SOrelation | ContentReference(ContentReferenceKind.Culture) | — |
| `kind` | `CulturalPracticeKind` | [11](Assets/Scripts/Content/CulturalPracticeDefinitionSO.cs#L11) | enum | — | — |
| `requirements` | `V20ContentRequirementSet` | [12](Assets/Scripts/Content/CulturalPracticeDefinitionSO.cs#L12) | 중첩 데이터 | — | — |
| `successEffects` | `List<V20ContentEffect>` | [13](Assets/Scripts/Content/CulturalPracticeDefinitionSO.cs#L13) | 중첩 데이터 | — | — |
| `neglectedEffects` | `List<V20ContentEffect>` | [14](Assets/Scripts/Content/CulturalPracticeDefinitionSO.cs#L14) | 중첩 데이터 | — | — |

### `CultureRoomPreferenceDefinition`

도달 SO: `SpeciesCultureDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `preferredRoles` | `FacilityRole` | [8](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L8) | enum | — | — |
| `idealTemperatureC` | `float` | [9](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L9) | nonrelation | — | — |
| `temperatureToleranceC` | `float` | [10](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L10) | 수치/설정 | — | — |
| `minimumVentilation` | `float` | [11](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L11) | 수치/설정 | — | — |
| `minimumLighting` | `float` | [12](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L12) | 수치/설정 | — | — |
| `maximumLighting` | `float` | [13](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L13) | 수치/설정 | — | — |
| `minimumCleanliness` | `float` | [14](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L14) | 수치/설정 | — | — |
| `prefersSharedSpace` | `bool` | [15](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L15) | 수치/설정 | — | — |
| `prefersPrivateSpace` | `bool` | [16](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L16) | 수치/설정 | — | — |

### `DataScriptableObject`

도달 SO: `ApparelDefinitionSO`, `BuildingSO`, `CharacterTraitSO`, `CraftMaterialDefinitionSO`, `CropDefinitionSO`, `CropGenomeDefinitionSO`, `DataScriptableObject`, `EnvironmentalWorkwearSO`, `FacilityBlueprintSO`, `FacilityEvolutionRecipeSO`, `FacilityEvolutionRecordTokenDefinitionSO`, `FacilitySynthesisRecipeSO`, `GenericItemDefinitionSO`, `ItemDefinitionSO`, `MaterialEconomicProfileSO`, `OffenseDecisionCardSO`, `OffenseEncounterSO`, `OffenseSiteArchetypeSO`, `OffenseUrgentSiteDefinitionSO`, `ProductionRecipeSO`, `ResearchProjectSO`, `ResearchUnlockBundleDefinitionSO`, `ResourceItemDefinitionSO`, `SaleItem`, `StockInfo`, `TextileMaterialDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `int` | [9](Assets/Scripts/Models/Economy/Content/DataScriptableObject.cs#L9) | ownID | ContentIdentity | — |

### `DefenseBurnEffectSO`

도달 SO: `DefenseBurnEffectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `environmentalIgnitionIntensity` | `float` | [7](Assets/Scripts/Services/Defense/DefenseBurnEffectSO.cs#L7) | 수치/설정 | — | — |

### `DefenseChargeEffectSO`

도달 SO: `DefenseChargeEffectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `dischargeThreshold` | `int` | [6](Assets/Scripts/Services/Defense/DefenseChargeEffectSO.cs#L6) | 수치/설정 | — | — |
| `dischargeReason` | `string` | [7](Assets/Scripts/Services/Defense/DefenseChargeEffectSO.cs#L7) | authored-value | — | — |

### `DefenseEffectSO`

도달 SO: `DefenseBurnEffectSO`, `DefenseChargeEffectSO`, `DefenseCorrosionEffectSO`, `DefenseDamageEffectSO`, `DefenseEffectSO`, `DefenseGuardAttackEffectSO`, `DefenseSlowEffectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `amount` | `float` | [17](Assets/Scripts/Services/Defense/DefenseEffectSO.cs#L17) | 수치/설정 | — | — |
| `duration` | `float` | [18](Assets/Scripts/Services/Defense/DefenseEffectSO.cs#L18) | 수치/설정 | — | — |
| `stacks` | `int` | [19](Assets/Scripts/Services/Defense/DefenseEffectSO.cs#L19) | 수치/설정 | — | — |
| `logTag` | `string` | [20](Assets/Scripts/Services/Defense/DefenseEffectSO.cs#L20) | semantic-key | — | — |

### `DefenseFacilityData`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `enabled` | `bool` | [18](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L18) | 수치/설정 | — | — |
| `concept` | `DefenseAttackConcept` | [19](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L19) | enum | — | — |
| `triggerTimings` | `DefenseTriggerTiming` | [20](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L20) | enum | — | — |
| `targetRule` | `DefenseTargetRule` | [21](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L21) | authored-value | — | — |
| `cooldownSeconds` | `float` | [22](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L22) | 수치/설정 | — | — |
| `periodicIntervalSeconds` | `float` | [23](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L23) | 수치/설정 | — | — |
| `range` | `int` | [24](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L24) | 수치/설정 | — | — |
| `star` | `int` | [25](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L25) | 수치/설정 | — | — |
| `combatLogText` | `string` | [26](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L26) | authored-value | — | — |
| `effectAssets` | `DefenseEffectSO[]` | [27](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L27) | direct-object-reference | — | — |
| `supplyKind` | `DefenseSupplyKind` | [29](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L29) | enum | — | — |
| `supplyItemId` | `string` | [31](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L31) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `supplyCategory` | `StockCategory` | [32](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L32) | enum | — | — |
| `supplyCapacity` | `int` | [33](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L33) | 수치/설정 | — | — |
| `supplyPerActivation` | `int` | [34](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L34) | 수치/설정 | — | — |
| `initialSupply` | `int` | [35](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L35) | 수치/설정 | — | — |
| `requiresPower` | `bool` | [36](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L36) | 수치/설정 | — | — |
| `powerDemand` | `float` | [37](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L37) | 수치/설정 | — | — |
| `conditionLossPerActivation` | `float` | [38](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L38) | 수치/설정 | — | — |
| `baseJamChance` | `float` | [39](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L39) | 수치/설정 | — | — |
| `baseMisfireChance` | `float` | [40](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L40) | 수치/설정 | — | — |
| `facilityFamilyId` | `string` | [41](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L41) | semantic-key | — | — |
| `affinityTags` | `string[]` | [42](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L42) | unconfirmed | — | 미연결 |
| `growth` | `DefenseFacilityGrowthData` | [43](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L43) | 중첩 데이터 | — | — |

### `DefenseFacilityGrowthData`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capacityLevel` | `int` | [7](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L7) | 수치/설정 | — | — |
| `resetSpeedLevel` | `int` | [8](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L8) | 수치/설정 | — | — |
| `effectStrengthLevel` | `int` | [9](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L9) | 수치/설정 | — | — |
| `detectionRangeLevel` | `int` | [10](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L10) | 수치/설정 | — | — |
| `identificationLevel` | `int` | [11](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L11) | authored-value | — | — |
| `outageResistanceLevel` | `int` | [12](Assets/Scripts/Services/Defense/DefenseFacilityData.cs#L12) | authored-value | — | — |

### `DefenseGuardAttackEffectSO`

도달 SO: `DefenseGuardAttackEffectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `damageReason` | `string` | [6](Assets/Scripts/Services/Defense/DefenseGuardAttackEffectSO.cs#L6) | authored-value | — | — |

### `DiploidLocusSaveData`

도달 SO: `CropGenomeDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `locus` | `CropGenomeLocus` | [107](Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs#L107) | enum | — | — |
| `alleleA` | `int` | [108](Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs#L108) | 수치/설정 | — | — |
| `alleleB` | `int` | [109](Assets/Scripts/Models/Economy/Content/CropEcologyDomain.cs#L109) | 수치/설정 | — | — |

### `DiseaseDefinitionSO`

도달 SO: `DiseaseDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stableId` | `string` | [9](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L9) | ownID | ContentIdentity | — |
| `displayName` | `string` | [10](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L10) | display | — | — |
| `routes` | `DiseaseTransmissionRoute` | [11](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L11) | enum | — | — |
| `incubationDays` | `int` | [12](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L12) | 수치/설정 | — | — |
| `contagiousDays` | `int` | [13](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L13) | nonrelation | — | — |
| `baseInfectionProbability` | `float` | [14](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L14) | nonrelation | — | — |
| `baseSeverity` | `float` | [15](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L15) | 수치/설정 | — | — |
| `targetSystem` | `DiseaseTargetSystem` | [16](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L16) | nonrelation | — | — |
| `vaccineAllowed` | `bool` | [17](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L17) | 수치/설정 | — | — |
| `chronic` | `bool` | [19](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L19) | 수치/설정 | — | — |
| `description` | `string` | [20](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L20) | display | — | — |
| `authoringRevision` | `int` | [21](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L21) | 수치/설정 | — | — |
| `sourceNote` | `string` | [22](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L22) | display | — | — |
| `symptomProfileId` | `string` | [23](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L23) | semantic | — | — |
| `fieldResponseIds` | `List<string>` | [24](Assets/Scripts/Content/DiseaseDefinitionSO.cs#L24) | semantic | — | R21 |

### `DungeonAudioLibrarySO`

도달 SO: `DungeonAudioLibrarySO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `musicLoop` | `AudioClip` | [8](Assets/Scripts/Content/DungeonAudioLibrarySO.cs#L8) | direct-object-reference | — | — |
| `ambienceLoop` | `AudioClip` | [9](Assets/Scripts/Content/DungeonAudioLibrarySO.cs#L9) | direct-object-reference | — | — |
| `uiClick` | `AudioClip` | [10](Assets/Scripts/Content/DungeonAudioLibrarySO.cs#L10) | direct-object-reference | — | — |
| `confirm` | `AudioClip` | [11](Assets/Scripts/Content/DungeonAudioLibrarySO.cs#L11) | direct-object-reference | — | — |
| `warning` | `AudioClip` | [12](Assets/Scripts/Content/DungeonAudioLibrarySO.cs#L12) | direct-object-reference | — | — |
| `impact` | `AudioClip` | [13](Assets/Scripts/Content/DungeonAudioLibrarySO.cs#L13) | direct-object-reference | — | — |
| `victory` | `AudioClip` | [14](Assets/Scripts/Content/DungeonAudioLibrarySO.cs#L14) | direct-object-reference | — | — |
| `defeat` | `AudioClip` | [15](Assets/Scripts/Content/DungeonAudioLibrarySO.cs#L15) | direct-object-reference | — | — |

### `DungeonFactionDefinitionSO`

도달 SO: `DungeonFactionDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `factionId` | `string` | [42](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L42) | self-key | ContentIdentity | — |
| `displayName` | `string` | [43](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L43) | display-text | — | — |
| `speciesTag` | `string` | [45](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L45) | relation-annotated | ContentReference(ContentReferenceKind.Species) | — |
| `description` | `string` | [46](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L46) | display-text | — | — |
| `relationTags` | `string[]` | [47](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L47) | semantic-key | — | — |
| `tradeTags` | `string[]` | [48](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L48) | semantic-key | — | — |
| `reinforcementRole` | `string` | [49](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L49) | semantic-key | — | — |
| `crest` | `Sprite` | [50](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L50) | direct-object-reference | — | — |
| `tradeCargo` | `List<FactionCargoLine>` | [51](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L51) | 중첩 데이터 | — | — |
| `supplyCargo` | `List<FactionCargoLine>` | [52](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L52) | 중첩 데이터 | — | — |
| `tradeEconomicPolicy` | `FactionRouteEconomicPolicyDescriptor` | [53](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L53) | 중첩 데이터 | — | — |
| `supplyEconomicPolicy` | `FactionRouteEconomicPolicyDescriptor` | [54](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L54) | 중첩 데이터 | — | — |
| `tradeCooldownDays` | `int` | [55](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L55) | 수치/설정 | — | — |
| `supplyCooldownDays` | `int` | [56](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L56) | 수치/설정 | — | — |
| `reinforcementCooldownDays` | `int` | [57](Assets/Scripts/Models/Factions/Core/DungeonFactionDefinitionSO.cs#L57) | 수치/설정 | — | — |

### `DungeonStory.Buildings.FacilityVenueAnchorSelector`

도달 SO: `FestivalDefinitionSO`, `GuestRequestDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `exactBuildingDefinitionIds` | `List<string>` | [12](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L12) | SOrelation | ContentReference(ContentReferenceKind.Building) | — |
| `facilityRoles` | `FacilityRole` | [13](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L13) | nonrelation | — | — |

### `DungeonStory.Buildings.FacilityVenueRequirements`

도달 SO: `FestivalDefinitionSO`, `GuestRequestDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `anchor` | `FacilityVenueAnchorSelector` | [72](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L72) | 중첩 데이터 | — | — |
| `requiredRoomFacilityRoles` | `FacilityRole` | [73](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L73) | nonrelation | — | — |
| `requiredRoomFacilities` | `List<FacilityVenueAnchorSelector>` | [74](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L74) | 중첩 데이터 | — | — |
| `minimumSeats` | `int` | [75](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L75) | 수치/설정 | — | — |
| `minimumTables` | `int` | [76](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L76) | 수치/설정 | — | — |
| `minimumServiceCapacity` | `int` | [77](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L77) | 수치/설정 | — | — |
| `minimumEventCells` | `int` | [78](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L78) | 수치/설정 | — | — |
| `minimumVacantBeds` | `int` | [79](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L79) | 수치/설정 | — | — |
| `requireResidentHeadroom` | `bool` | [80](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L80) | nonrelation | — | — |
| `requireWetBath` | `bool` | [81](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L81) | 수치/설정 | — | — |
| `requireEmergencySurgeryFacility` | `bool` | [82](Assets/Scripts/Models/Buildings/Core/FacilityVenueRequirements.cs#L82) | nonrelation | — | — |

### `DungeonStory.Content.CoreSession.CoreSessionRulesSO`

도달 SO: `DungeonStory.Content.CoreSession.CoreSessionRulesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `randomInvasionStartDay` | `int` | [41](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L41) | 수치/설정 | — | — |
| `growthStartDay` | `int` | [42](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L42) | 수치/설정 | — | — |
| `escalationStartDay` | `int` | [43](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L43) | 수치/설정 | — | — |
| `endlessDefenseStartDay` | `int` | [44](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L44) | 수치/설정 | — | — |
| `firstBossDay` | `int` | [45](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L45) | 수치/설정 | — | — |
| `bossIntervalDays` | `int` | [46](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L46) | 수치/설정 | — | — |
| `rehearsals` | `List<RehearsalRule>` | [47](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L47) | 중첩 데이터 | — | — |
| `externalProblemBands` | `List<ExternalProblemBand>` | [48](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L48) | 중첩 데이터 | — | — |
| `renownIntelCost` | `float` | [52](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L52) | 수치/설정 | — | — |
| `goldIntelCost` | `int` | [53](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L53) | 수치/설정 | — | — |
| `scoutingIntelCost` | `float` | [54](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L54) | 수치/설정 | — | — |
| `dreadDefenseCost` | `float` | [55](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L55) | 수치/설정 | — | — |
| `ecologyRaidCountdownSeconds` | `float` | [56](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L56) | authored-value | — | — |
| `maximumRumorMitigation` | `float` | [57](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L57) | 수치/설정 | — | — |
| `maximumRumorRenownCost` | `int` | [58](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L58) | 수치/설정 | — | — |
| `maximumRumorGoldCost` | `int` | [59](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L59) | 수치/설정 | — | — |
| `debugHistoryLimit` | `int` | [62](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L62) | 수치/설정 | — | — |
| `serviceResearch` | `List<ServiceResearchRule>` | [63](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L63) | 중첩 데이터 | — | — |

### `DungeonStory.Content.CoreSession.ExternalProblemBand`

도달 SO: `DungeonStory.Content.CoreSession.CoreSessionRulesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `lastDayInclusive` | `int` | [20](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L20) | 수치/설정 | — | — |
| `maximumConcurrentProblems` | `int` | [21](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L21) | 수치/설정 | — | — |
| `allowedIncidentKinds` | `List<int>` | [22](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L22) | authored-value | — | — |

### `DungeonStory.Content.CoreSession.RehearsalRule`

도달 SO: `DungeonStory.Content.CoreSession.CoreSessionRulesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `day` | `int` | [11](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L11) | 수치/설정 | — | — |
| `powerMultiplier` | `float` | [12](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L12) | 수치/설정 | — | — |
| `ownerDamageMultiplier` | `float` | [13](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L13) | 수치/설정 | — | — |
| `retreatHealthRatio` | `float` | [14](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L14) | 수치/설정 | — | — |

### `DungeonStory.Content.CoreSession.ServiceResearchRule`

도달 SO: `DungeonStory.Content.CoreSession.CoreSessionRulesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `serviceCategory` | `int` | [28](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L28) | 수치/설정 | — | — |
| `operationMode` | `int` | [29](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L29) | 수치/설정 | — | — |
| `researchId` | `string` | [31](Assets/Scripts/Content/CoreSession/CoreSessionRulesSO.cs#L31) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |

### `DungeonStory.Factions.FactionCargoLine`

도달 SO: `DungeonFactionDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [130](Assets/Scripts/Models/Factions/Core/FactionModels.cs#L130) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `amount` | `int` | [131](Assets/Scripts/Models/Factions/Core/FactionModels.cs#L131) | 수치/설정 | — | — |

### `DungeonStory.Factions.FactionRouteEconomicPolicyDescriptor`

도달 SO: `DungeonFactionDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capabilityId` | `string` | [108](Assets/Scripts/Models/Factions/Core/FactionModels.cs#L108) | semantic-key | — | — |
| `capabilityVersion` | `int` | [109](Assets/Scripts/Models/Factions/Core/FactionModels.cs#L109) | authored-value | — | — |

### `EndingDefinitionSO`

도달 SO: `EndingDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `tier` | `RunMilestoneTier` | [10](Assets/Scripts/Content/EndingDefinitionSO.cs#L10) | enum | — | — |
| `completionRequirements` | `V20ContentRequirementSet` | [11](Assets/Scripts/Content/EndingDefinitionSO.cs#L11) | 중첩 데이터 | — | — |
| `landmarkBuildingId` | `string` | [13](Assets/Scripts/Content/EndingDefinitionSO.cs#L13) | relation-annotated | ContentReference(ContentReferenceKind.Building) | — |
| `permanentRewards` | `List<V20ContentEffect>` | [14](Assets/Scripts/Content/EndingDefinitionSO.cs#L14) | 중첩 데이터 | — | — |
| `counterPressures` | `List<V20ContentEffect>` | [15](Assets/Scripts/Content/EndingDefinitionSO.cs#L15) | 중첩 데이터 | — | — |

### `EndlessCrisisAxisPolicy`

도달 SO: `EndlessCrisisPolicyDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `axis` | `EndlessCrisisAxis` | [19](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L19) | enum | — | — |
| `displayName` | `string` | [20](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L20) | display-text | — | — |
| `singleAxisMultiplier` | `float` | [21](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L21) | 수치/설정 | — | — |
| `compoundAxisMultiplier` | `float` | [22](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L22) | 수치/설정 | — | — |

### `EndlessCrisisPolicyDefinitionSO`

도달 SO: `EndlessCrisisPolicyDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stableId` | `string` | [66](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L66) | self-key | ContentIdentity | — |
| `singleAxisProbabilityPercent` | `int` | [67](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L67) | authored-value | — | — |
| `pressureDurationDays` | `int` | [68](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L68) | 수치/설정 | — | — |
| `singleAxisRecoveryDays` | `int` | [69](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L69) | 수치/설정 | — | — |
| `compoundAxisRecoveryDays` | `int` | [70](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L70) | 수치/설정 | — | — |
| `axes` | `List<EndlessCrisisAxisPolicy>` | [71](Assets/Scripts/Content/EndlessCrisisPolicyDefinitionSO.cs#L71) | 중첩 데이터 | — | — |

### `EnemyAbilityDefinitionSO`

도달 SO: `EnemyAbilityDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stableId` | `string` | [32](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L32) | self-key | ContentIdentity | — |
| `displayName` | `string` | [33](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L33) | display-text | — | — |
| `description` | `string` | [34](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L34) | display-text | — | — |
| `authoringRevision` | `int` | [35](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L35) | 수치/설정 | — | — |
| `sourceNote` | `string` | [36](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L36) | display-text | — | — |
| `cooldownRounds` | `int` | [37](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L37) | 수치/설정 | — | — |
| `targetRule` | `OffenseBattleTargetRule` | [38](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L38) | authored-value | — | — |
| `effects` | `List<EnemyAbilityEffectRecord>` | [39](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L39) | 중첩 데이터 | — | — |

### `EnemyAbilityEffectRecord`

도달 SO: `EnemyAbilityDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `EnemyAbilityEffectKind` | [22](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L22) | enum | — | — |
| `magnitude` | `float` | [23](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L23) | 수치/설정 | — | — |
| `durationRounds` | `int` | [24](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L24) | 수치/설정 | — | — |
| `targetTag` | `string` | [25](Assets/Scripts/Models/Offense/Core/EnemyAbilityDefinitionSO.cs#L25) | semantic-key | — | — |

### `EnemyArchetypeDefinitionSO`

도달 SO: `EnemyArchetypeDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stableId` | `string` | [82](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L82) | self-key | ContentIdentity | — |
| `displayName` | `string` | [83](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L83) | display-text | — | — |
| `description` | `string` | [84](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L84) | display-text | — | — |
| `authoringRevision` | `int` | [85](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L85) | 수치/설정 | — | — |
| `sourceNote` | `string` | [86](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L86) | display-text | — | — |
| `factionId` | `string` | [87](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L87) | semantic-key | — | — |
| `speciesTag` | `string` | [89](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L89) | relation-annotated | ContentReference(ContentReferenceKind.Species, Aliases = "Truth\|Construct") | — |
| `role` | `EnemyCombatRole` | [90](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L90) | enum | — | — |
| `maxHealth` | `float` | [91](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L91) | 수치/설정 | — | — |
| `attack` | `float` | [92](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L92) | 수치/설정 | — | — |
| `strength` | `float` | [93](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L93) | 수치/설정 | — | — |
| `toughness` | `float` | [94](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L94) | 수치/설정 | — | — |
| `dexterity` | `float` | [95](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L95) | 수치/설정 | — | — |
| `moveSpeed` | `float` | [96](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L96) | 수치/설정 | — | — |
| `equipment` | `EnemyEquipmentLoadoutRecord` | [97](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L97) | 중첩 데이터 | — | — |
| `abilityIds` | `List<string>` | [99](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L99) | relation-annotated | ContentReference(ContentReferenceKind.EnemyAbility) | — |
| `tacticalProfile` | `EnemyTacticalProfile` | [100](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L100) | nested-authored-value | — | — |
| `counterTags` | `List<string>` | [101](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L101) | semantic-key | — | — |
| `rewardItemIds` | `List<string>` | [103](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L103) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `bossPhases` | `List<EnemyBossPhaseRecord>` | [104](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L104) | 중첩 데이터 | — | — |
| `individualGeneration` | `EnemyIndividualGenerationProfile` | [105](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L105) | nested-authored-value | — | — |

### `EnemyBossPhaseRecord`

도달 SO: `EnemyArchetypeDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `healthThreshold` | `float` | [40](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L40) | 수치/설정 | — | — |
| `abilityIds` | `List<string>` | [42](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L42) | relation-annotated | ContentReference(ContentReferenceKind.EnemyAbility) | — |
| `tacticalProfileOverrideTag` | `string` | [43](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L43) | semantic-key | — | — |

### `EnemyEquipmentLoadoutRecord`

도달 SO: `EnemyArchetypeDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `weaponDefinitionId` | `string` | [28](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L28) | relation-annotated | ContentReference(ContentReferenceKind.Weapon) | — |
| `armorDefinitionId` | `string` | [30](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L30) | relation-annotated | ContentReference(ContentReferenceKind.Armor) | — |
| `shieldDefinitionId` | `string` | [32](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L32) | relation-annotated | ContentReference(ContentReferenceKind.Shield) | — |
| `ammunitionItemId` | `string` | [34](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L34) | relation-annotated | ContentReference(ContentReferenceKind.Ammunition) | — |

### `EnemyIndividualGenerationProfile`

도달 SO: `EnemyArchetypeDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `minimumGeneralTraits` | `int` | [49](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L49) | authored-value | — | — |
| `maximumGeneralTraits` | `int` | [50](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L50) | authored-value | — | — |
| `minimumExpressedHeritableTraits` | `int` | [51](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L51) | authored-value | — | — |
| `maximumExpressedHeritableTraits` | `int` | [52](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L52) | authored-value | — | — |
| `maximumLatentHeritableTraits` | `int` | [53](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L53) | authored-value | — | — |
| `aptitudeVariance` | `int` | [54](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L54) | 수치/설정 | — | — |
| `combatStatVariance` | `float` | [55](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L55) | 수치/설정 | — | — |
| `minimumLoyalty` | `float` | [56](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L56) | 수치/설정 | — | — |
| `maximumLoyalty` | `float` | [57](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L57) | 수치/설정 | — | — |
| `recruitable` | `bool` | [58](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L58) | 수치/설정 | — | — |
| `militaryTrainingId` | `string` | [59](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L59) | semantic-key | — | — |
| `allowedBackgroundIds` | `List<string>` | [61](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L61) | relation-annotated | ContentReference("CharacterBackgroundDefinitionSO", "StableId") | — |

### `EnemyTacticalProfile`

도달 SO: `EnemyArchetypeDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `attackWeight` | `float` | [11](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L11) | 수치/설정 | — | — |
| `protectWeight` | `float` | [12](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L12) | 수치/설정 | — | — |
| `abilityWeight` | `float` | [13](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L13) | authored-value | — | — |
| `retreatWeight` | `float` | [14](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L14) | 수치/설정 | — | — |
| `retreatHealthFraction` | `float` | [15](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L15) | 수치/설정 | — | — |
| `preferredTargetTags` | `List<string>` | [16](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L16) | semantic-key | — | — |
| `avoidedTargetTags` | `List<string>` | [17](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L17) | semantic-key | — | — |
| `formationTag` | `string` | [18](Assets/Scripts/Models/Offense/Core/EnemyArchetypeDefinitionSO.cs#L18) | semantic-key | — | — |

### `EnvironmentalWorkwearSO`

도달 SO: `EnvironmentalWorkwearSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workwearId` | `string` | [16](Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs#L16) | self-key | ContentIdentity | — |
| `itemDefinitionId` | `string` | [18](Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs#L18) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `displayName` | `string` | [19](Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs#L19) | authored-value | — | — |
| `description` | `string` | [20](Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs#L20) | authored-value | — | — |
| `allowedSpecies` | `string[]` | [22](Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs#L22) | confirmed-filter-parity-mismatch | ContentReference(ContentReferenceKind.Species) | R15 |
| `protection` | `ThermalProtectionProfile` | [23](Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs#L23) | 중첩 데이터 | — | — |
| `requiredResearchId` | `string` | [26](Assets/Scripts/Models/Economy/Content/EnvironmentalWorkwearSO.cs#L26) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |

### `EquipmentEvolutionFormulaCapabilityDefinition`

도달 SO: `EquipmentEvolutionFormulaCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capabilityId` | `string` | [37](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L37) | semantic-key | — | — |
| `displayName` | `string` | [38](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L38) | authored-value | — | — |
| `statId` | `string` | [39](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L39) | semantic-key | — | — |
| `modifierKind` | `EquipmentEvolutionFormulaModifierKind` | [40](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L40) | enum | — | — |
| `baseCost` | `int` | [41](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L41) | 수치/설정 | — | — |
| `triggerFrequencyCostPerUnit` | `int` | [42](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L42) | 수치/설정 | — | — |
| `guaranteedProcCost` | `int` | [43](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L43) | 수치/설정 | — | — |
| `areaCostPerExtraTarget` | `int` | [44](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L44) | authored-value | — | — |
| `multiEffectCostPerExtraEffect` | `int` | [45](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L45) | 수치/설정 | — | — |
| `narrativeAffinity` | `float` | [46](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L46) | 수치/설정 | — | — |
| `affinityKeys` | `List<string>` | [47](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L47) | semantic-key | — | — |
| `conflictGroups` | `List<string>` | [48](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L48) | semantic-key | — | — |
| `forbiddenSynergies` | `List<string>` | [49](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L49) | authored-value | — | — |
| `appliedParameterIds` | `List<string>` | [50](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L50) | semantic-key | — | — |
| `formatterId` | `string` | [51](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L51) | semantic-key | — | — |
| `applicatorId` | `string` | [52](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L52) | semantic-key | — | — |
| `parameterRanges` | `List<EquipmentEvolutionFormulaRangeDefinition>` | [53](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L53) | 중첩 데이터 | — | — |

### `EquipmentEvolutionFormulaCatalogSO`

도달 SO: `EquipmentEvolutionFormulaCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `formulaPolicy` | `EquipmentEvolutionFormulaPolicyDefinition` | [158](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L158) | 중첩 데이터 | — | — |
| `milestoneThresholds` | `List<int>` | [159](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L159) | 수치/설정 | — | — |
| `ordinaryImportance` | `double` | [160](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L160) | 수치/설정 | — | — |
| `historicalImportance` | `double` | [161](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L161) | 수치/설정 | — | — |
| `capabilities` | `List<EquipmentEvolutionFormulaCapabilityDefinition>` | [162](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L162) | 중첩 데이터 | — | — |
| `catalogModuleIds` | `List<string>` | [167](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L167) | semantic-key | — | — |

### `EquipmentEvolutionFormulaPolicyDefinition`

도달 SO: `EquipmentEvolutionFormulaCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `formulaVersion` | `int` | [115](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L115) | 수치/설정 | — | — |
| `catalogSha256` | `string` | [116](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L116) | authored-value | — | — |
| `baseBudget` | `int` | [117](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L117) | 수치/설정 | — | — |
| `powerScale` | `int` | [118](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L118) | 수치/설정 | — | — |
| `softCapK` | `double` | [119](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L119) | 수치/설정 | — | — |
| `minimumImportance` | `double` | [120](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L120) | 수치/설정 | — | — |
| `maximumImportance` | `double` | [121](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L121) | 수치/설정 | — | — |
| `milestoneWeights` | `List<double>` | [122](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L122) | 수치/설정 | — | — |
| `drawbackCredit` | `NarrativeFormulaDrawbackCreditPolicyDefinition` | [123](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L123) | 중첩 데이터 | — | — |

### `EquipmentEvolutionFormulaRangeDefinition`

도달 SO: `EquipmentEvolutionFormulaCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `parameterId` | `string` | [18](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L18) | semantic-key | — | — |
| `minimumUnits` | `long` | [19](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L19) | 수치/설정 | — | — |
| `maximumUnits` | `long` | [20](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L20) | 수치/설정 | — | — |
| `quantumUnits` | `long` | [21](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L21) | 수치/설정 | — | — |
| `decimalPlaces` | `int` | [22](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L22) | 수치/설정 | — | — |
| `costPerQuantum` | `int` | [23](Assets/Scripts/Services/Combat/EquipmentEvolutionFormulaDefinitionSO.cs#L23) | 수치/설정 | — | — |

### `EquipmentItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `equipmentDefinitionId` | `string` | [381](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L381) | relation-annotated | ContentReference("CombatEquipmentDefinitionSO", "EquipmentId") | — |

### `EquipmentModuleDefinitionSO`

도달 SO: `EquipmentModuleDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `moduleId` | `string` | [11](Assets/Scripts/Models/Combat/Core/EquipmentModuleDefinitionSO.cs#L11) | self-key | ContentIdentity | — |
| `displayName` | `string` | [12](Assets/Scripts/Models/Combat/Core/EquipmentModuleDefinitionSO.cs#L12) | authored-value | — | — |
| `description` | `string` | [13](Assets/Scripts/Models/Combat/Core/EquipmentModuleDefinitionSO.cs#L13) | authored-value | — | — |
| `lineageKind` | `EquipmentLineageKind` | [14](Assets/Scripts/Models/Combat/Core/EquipmentModuleDefinitionSO.cs#L14) | enum | — | — |
| `minimumEra` | `EquipmentEra` | [15](Assets/Scripts/Models/Combat/Core/EquipmentModuleDefinitionSO.cs#L15) | enum | — | — |
| `powerPerGrade` | `float` | [16](Assets/Scripts/Models/Combat/Core/EquipmentModuleDefinitionSO.cs#L16) | 수치/설정 | — | — |
| `utilityPerGrade` | `float` | [17](Assets/Scripts/Models/Combat/Core/EquipmentModuleDefinitionSO.cs#L17) | 수치/설정 | — | — |
| `effects` | `List<GameplayEffectBinding>` | [18](Assets/Scripts/Models/Combat/Core/EquipmentModuleDefinitionSO.cs#L18) | 중첩 데이터 | — | — |

### `EventMoodRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `eventId` | `string` | [68](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L68) | semantic | — | — |
| `moodDelta` | `float` | [69](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L69) | 수치/설정 | — | — |
| `durationDays` | `int` | [70](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L70) | 수치/설정 | — | — |

### `EvolutionCatalystItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `family` | `string` | [524](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L524) | semantic-key | — | — |
| `potency` | `int` | [525](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L525) | 수치/설정 | — | — |
| `residue` | `bool` | [526](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L526) | authored-value | — | — |

### `ExtremeCraftInspirationRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `mythicChance` | `float` | [196](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L196) | 수치/설정 | — | — |
| `minimumContributionShare` | `float` | [197](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L197) | 수치/설정 | — | — |
| `repetitionFreeCount` | `int` | [198](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L198) | 수치/설정 | — | — |
| `repetitionMoodStep` | `float` | [199](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L199) | 수치/설정 | — | — |
| `repetitionMoodMinimum` | `float` | [200](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L200) | 수치/설정 | — | — |
| `resetAfterHours` | `int` | [201](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L201) | 수치/설정 | — | — |
| `mythicMoodDelta` | `float` | [202](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L202) | 수치/설정 | — | — |
| `mythicMoodDurationDays` | `int` | [203](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L203) | 수치/설정 | — | — |

### `FacilityAnchorData`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `slots` | `List<FacilityAnchorSlot>` | [28](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L28) | 중첩 데이터 | — | — |

### `FacilityAnchorSlot`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `purposeId` | `string` | [18](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L18) | semantic-key | — | — |
| `offset` | `Vector2` | [20](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L20) | Unity 값 | — | — |

### `FacilityBlueprintSO`

도달 SO: `FacilityBlueprintSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `blueprintName` | `string` | [8](Assets/Scripts/Models/FacilityShop/Core/FacilityBlueprintSO.cs#L8) | authored-value | — | — |
| `description` | `string` | [9](Assets/Scripts/Models/FacilityShop/Core/FacilityBlueprintSO.cs#L9) | authored-value | — | — |
| `rarity` | `FacilityShopRarity` | [10](Assets/Scripts/Models/FacilityShop/Core/FacilityBlueprintSO.cs#L10) | enum | — | — |
| `defaultCost` | `int` | [11](Assets/Scripts/Models/FacilityShop/Core/FacilityBlueprintSO.cs#L11) | 수치/설정 | — | — |
| `researchWorkRequired` | `float` | [12](Assets/Scripts/Models/FacilityShop/Core/FacilityBlueprintSO.cs#L12) | 수치/설정 | — | — |
| `targetResearchProjectId` | `string` | [15](Assets/Scripts/Models/FacilityShop/Core/FacilityBlueprintSO.cs#L15) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |
| `unlocks` | `BlueprintUnlockCollection` | [16](Assets/Scripts/Models/FacilityShop/Core/FacilityBlueprintSO.cs#L16) | 중첩 데이터 | — | — |

### `FacilityCrimeSettingsSO`

도달 SO: `FacilityCrimeSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `baseCrimePressure` | `float` | [13](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L13) | 수치/설정 | — | — |
| `unstaffedSupervisionRisk` | `float` | [14](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L14) | 수치/설정 | — | — |
| `staffedSupervisionReduction` | `float` | [15](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L15) | 수치/설정 | — | — |
| `lowMoodCrimeRiskWeight` | `float` | [16](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L16) | 수치/설정 | — | — |
| `unmetNeedCrimeRiskWeight` | `float` | [17](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L17) | 수치/설정 | — | — |
| `crowdCrimeRiskWeight` | `float` | [18](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L18) | 수치/설정 | — | — |
| `highValueCrimeRiskWeight` | `float` | [19](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L19) | 수치/설정 | — | — |
| `damagedFacilityCrimeRiskWeight` | `float` | [20](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L20) | authored-value | — | — |
| `operationalRiskScale` | `float` | [23](Assets/Scripts/Models/Buildings/Core/FacilityCrimeSettingsSO.cs#L23) | 수치/설정 | — | — |

### `FacilityData`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `roles` | `FacilityRole` | [76](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L76) | enum | — | — |
| `capacity` | `int` | [77](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L77) | 수치/설정 | — | — |
| `useDuration` | `float` | [78](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L78) | 수치/설정 | — | — |
| `requiredWorkers` | `int` | [79](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L79) | 수치/설정 | — | — |
| `supportedWorkTypes` | `FacilityWorkType` | [80](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L80) | enum | — | — |
| `disabledWhenDamaged` | `bool` | [81](Assets/Scripts/Services/Buildings/SO/FacilityDefinitionData.cs#L81) | 수치/설정 | — | — |

### `FacilityEvolutionContributionData`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `contributesToRoomProfile` | `bool` | [195](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L195) | authored-value | — | — |
| `tags` | `string[]` | [196](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L196) | semantic-key | — | — |
| `scores` | `FacilityEvolutionValue[]` | [197](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L197) | 중첩 데이터 | — | — |
| `metrics` | `FacilityEvolutionValue[]` | [198](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L198) | 중첩 데이터 | — | — |

### `FacilityEvolutionFormulaCapabilityDefinition`

도달 SO: `FacilityEvolutionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capabilityId` | `string` | [152](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L152) | semantic-key | — | — |
| `evolutionModuleId` | `string` | [154](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L154) | semantic-key | — | — |
| `baseCost` | `int` | [155](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L155) | 수치/설정 | — | — |
| `triggerFrequencyCostPerUnit` | `int` | [156](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L156) | 수치/설정 | — | — |
| `guaranteedProcCost` | `int` | [157](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L157) | 수치/설정 | — | — |
| `areaCostPerExtraTarget` | `int` | [158](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L158) | authored-value | — | — |
| `multiEffectCostPerExtraEffect` | `int` | [159](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L159) | 수치/설정 | — | — |
| `narrativeAffinity` | `float` | [160](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L160) | 수치/설정 | — | — |
| `affinityKeys` | `List<string>` | [161](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L161) | semantic-key | — | — |
| `conflictGroups` | `List<string>` | [162](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L162) | semantic-key | — | — |
| `forbiddenSynergies` | `List<string>` | [163](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L163) | authored-value | — | — |
| `formatterId` | `string` | [164](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L164) | semantic-key | — | — |
| `applicatorId` | `string` | [165](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L165) | semantic-key | — | — |
| `parameterRanges` | `List<FacilityEvolutionFormulaRangeDefinition>` | [166](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L166) | 중첩 데이터 | — | — |

### `FacilityEvolutionFormulaPolicyDefinition`

도달 SO: `FacilityEvolutionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `formulaVersion` | `int` | [204](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L204) | 수치/설정 | — | — |
| `catalogSha256` | `string` | [206](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L206) | authored-value | — | — |
| `baseBudget` | `int` | [207](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L207) | 수치/설정 | — | — |
| `powerScale` | `int` | [208](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L208) | 수치/설정 | — | — |
| `softCapK` | `float` | [209](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L209) | 수치/설정 | — | — |
| `minimumImportance` | `float` | [210](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L210) | 수치/설정 | — | — |
| `maximumImportance` | `float` | [211](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L211) | 수치/설정 | — | — |
| `milestoneWeights` | `List<float>` | [212](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L212) | 수치/설정 | — | — |
| `drawbackCredit` | `NarrativeFormulaDrawbackCreditPolicyDefinition` | [213](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L213) | 중첩 데이터 | — | — |
| `triggerFrequencyUnits` | `int` | [214](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L214) | 수치/설정 | — | — |
| `guaranteedProc` | `bool` | [215](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L215) | 수치/설정 | — | — |
| `targetCount` | `int` | [216](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L216) | authored-value | — | — |

### `FacilityEvolutionFormulaRangeDefinition`

도달 SO: `FacilityEvolutionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `parameterId` | `string` | [137](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L137) | semantic-key | — | — |
| `minimumUnits` | `long` | [138](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L138) | 수치/설정 | — | — |
| `maximumUnits` | `long` | [139](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L139) | 수치/설정 | — | — |
| `quantumUnits` | `long` | [140](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L140) | 수치/설정 | — | — |
| `decimalPlaces` | `int` | [141](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L141) | 수치/설정 | — | — |
| `costPerQuantum` | `int` | [142](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L142) | 수치/설정 | — | — |

### `FacilityEvolutionMaterialRequirement`

도달 SO: `FacilityEvolutionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `materialId` | `string` | [273](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L273) | semantic-key | — | — |
| `amount` | `int` | [274](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L274) | 수치/설정 | — | — |

### `FacilityEvolutionMetricRequirement`

도달 SO: `FacilityEvolutionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `key` | `string` | [210](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L210) | semantic-key | — | — |
| `requireMin` | `bool` | [211](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L211) | 수치/설정 | — | — |
| `minValue` | `float` | [212](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L212) | 수치/설정 | — | — |
| `requireMax` | `bool` | [213](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L213) | 수치/설정 | — | — |
| `maxValue` | `float` | [214](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L214) | 수치/설정 | — | — |

### `FacilityEvolutionRecipeSO`

도달 SO: `FacilityEvolutionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `evolutionId` | `string` | [10](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L10) | self-key | ContentIdentity | — |
| `displayName` | `string` | [11](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L11) | authored-value | — | — |
| `description` | `string` | [12](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L12) | authored-value | — | — |
| `resultBuilding` | `BuildingSO` | [15](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L15) | direct-object-reference | — | — |
| `fromFacilities` | `BuildingSO[]` | [16](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L16) | direct-object-reference | — | — |
| `fromLineageTags` | `string[]` | [17](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L17) | semantic-key | — | — |
| `requiredStarGrade` | `int` | [18](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L18) | 수치/설정 | — | — |
| `resultStarGrade` | `int` | [19](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L19) | 수치/설정 | — | — |
| `publicByDefault` | `bool` | [22](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L22) | 수치/설정 | — | — |
| `requiredResearchRecipeId` | `string` | [24](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L24) | relation-annotated | ContentReference(ContentReferenceKind.Recipe) | — |
| `requireUsableRoom` | `bool` | [27](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L27) | 수치/설정 | — | — |
| `requiredRoomScores` | `FacilityEvolutionMetricRequirement[]` | [28](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L28) | 중첩 데이터 | — | — |
| `requiredRoomMetrics` | `FacilityEvolutionMetricRequirement[]` | [29](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L29) | 중첩 데이터 | — | — |
| `requiredRoomTags` | `string[]` | [30](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L30) | semantic-key | — | — |
| `requiredUniqueFixtures` | `BuildingSO[]` | [31](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L31) | direct-object-reference | — | — |
| `requiredRecordTokens` | `FacilityEvolutionTokenRequirement[]` | [34](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L34) | 중첩 데이터 | — | — |
| `requiredMaterials` | `FacilityEvolutionMaterialRequirement[]` | [35](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L35) | 중첩 데이터 | — | — |
| `allowedMutationTags` | `string[]` | [36](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L36) | semantic-key | — | — |
| `consumeRecordTokens` | `bool` | [37](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L37) | 수치/설정 | — | — |
| `identityPressureWeights` | `FacilityEvolutionValue[]` | [40](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L40) | nested-authored-value | — | — |
| `minimumIdentityScore` | `float` | [41](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L41) | authored-value | — | — |
| `formulaPolicy` | `FacilityEvolutionFormulaPolicyDefinition` | [45](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L45) | 중첩 데이터 | — | — |
| `formulaCapabilities` | `List<FacilityEvolutionFormulaCapabilityDefinition>` | [47](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs#L47) | 중첩 데이터 | — | — |

### `FacilityEvolutionRecordTokenDefinitionSO`

도달 SO: `FacilityEvolutionRecordTokenDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `tokenId` | `string` | [32](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L32) | self-key | ContentIdentity | — |
| `displayName` | `string` | [33](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L33) | authored-value | — | — |
| `description` | `string` | [34](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L34) | authored-value | — | — |
| `generationTrigger` | `FacilityEvolutionRecordTokenGenerationTrigger` | [37](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L37) | enum | — | — |
| `sourceMetric` | `string` | [39](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L39) | semantic-key | — | — |
| `threshold` | `float` | [40](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L40) | 수치/설정 | — | — |
| `decayPolicy` | `string` | [42](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L42) | authored-value | — | — |
| `consumePolicy` | `FacilityEvolutionRecordTokenConsumePolicy` | [45](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L45) | enum | — | — |
| `recipeTags` | `string[]` | [47](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L47) | semantic-key | — | — |
| `uiHint` | `string` | [48](Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecordTokens.cs#L48) | authored-value | — | — |

### `FacilityEvolutionTokenRequirement`

도달 SO: `FacilityEvolutionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `key` | `string` | [246](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L246) | relation-annotated | ContentReference("FacilityEvolutionRecordTokenDefinitionSO", "EffectiveId") | — |
| `minCount` | `int` | [247](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L247) | 수치/설정 | — | — |

### `FacilityEvolutionValue`

도달 SO: `BuildingSO`, `FacilityEvolutionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `key` | `string` | [167](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L167) | semantic-key | — | — |
| `value` | `float` | [168](Assets/Scripts/Models/Evolution/Facility/FacilityEvolutionTerms.cs#L168) | 수치/설정 | — | — |

### `FacilityNeedRecoveryData`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `sleep` | `float` | [7](Assets/Scripts/Services/Buildings/FacilityNeedRecoveryData.cs#L7) | 수치/설정 | — | — |
| `mood` | `float` | [8](Assets/Scripts/Services/Buildings/FacilityNeedRecoveryData.cs#L8) | 수치/설정 | — | — |
| `fun` | `float` | [9](Assets/Scripts/Services/Buildings/FacilityNeedRecoveryData.cs#L9) | 수치/설정 | — | — |
| `hunger` | `float` | [10](Assets/Scripts/Services/Buildings/FacilityNeedRecoveryData.cs#L10) | 수치/설정 | — | — |
| `excretion` | `float` | [11](Assets/Scripts/Services/Buildings/FacilityNeedRecoveryData.cs#L11) | 수치/설정 | — | — |
| `hygiene` | `float` | [12](Assets/Scripts/Services/Buildings/FacilityNeedRecoveryData.cs#L12) | 수치/설정 | — | — |

### `FacilityRoleColorOverride`

도달 SO: `RoomEnvironmentSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `roleId` | `string` | [16](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L16) | semantic-key | — | — |
| `color` | `Color` | [17](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L17) | Unity 값 | — | — |

### `FacilitySupplyItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `fuelValue` | `float` | [360](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L360) | 수치/설정 | — | — |
| `nutritionValue` | `float` | [361](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L361) | 수치/설정 | — | — |
| `feedEligible` | `bool` | [362](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L362) | 수치/설정 | — | — |

### `FacilitySupplyProfile`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `FacilitySupplyKind` | [55](Assets/Scripts/Models/Economy/Content/ResourceEconomyModels.cs#L55) | enum | — | — |
| `requiredTags` | `ResourceIngredientTag` | [56](Assets/Scripts/Models/Economy/Content/ResourceEconomyModels.cs#L56) | semantic-key | — | — |
| `minimumValue` | `float` | [57](Assets/Scripts/Models/Economy/Content/ResourceEconomyModels.cs#L57) | 수치/설정 | — | — |
| `bufferCapacity` | `int` | [58](Assets/Scripts/Models/Economy/Content/ResourceEconomyModels.cs#L58) | 수치/설정 | — | — |
| `allowedItemIds` | `List<string>` | [60](Assets/Scripts/Models/Economy/Content/ResourceEconomyModels.cs#L60) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `forbiddenItemIds` | `List<string>` | [62](Assets/Scripts/Models/Economy/Content/ResourceEconomyModels.cs#L62) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `priorityItemIds` | `List<string>` | [64](Assets/Scripts/Models/Economy/Content/ResourceEconomyModels.cs#L64) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |

### `FacilitySynthesisRecipeSO`

도달 SO: `FacilitySynthesisRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `recipeId` | `string` | [9](Assets/Scripts/Services/Synthesis/FacilitySynthesisRecipeSO.cs#L9) | self-key | ContentIdentity | — |
| `displayName` | `string` | [10](Assets/Scripts/Services/Synthesis/FacilitySynthesisRecipeSO.cs#L10) | authored-value | — | — |
| `description` | `string` | [11](Assets/Scripts/Services/Synthesis/FacilitySynthesisRecipeSO.cs#L11) | authored-value | — | — |
| `resultBuilding` | `BuildingSO` | [12](Assets/Scripts/Services/Synthesis/FacilitySynthesisRecipeSO.cs#L12) | direct-object-reference | — | — |
| `materialBuildings` | `BuildingSO[]` | [13](Assets/Scripts/Services/Synthesis/FacilitySynthesisRecipeSO.cs#L13) | direct-object-reference | — | — |
| `publicByDefault` | `bool` | [14](Assets/Scripts/Services/Synthesis/FacilitySynthesisRecipeSO.cs#L14) | 수치/설정 | — | — |
| `requiredResearchRecipeId` | `string` | [16](Assets/Scripts/Services/Synthesis/FacilitySynthesisRecipeSO.cs#L16) | relation-annotated | ContentReference(ContentReferenceKind.Recipe) | — |
| `levelInheritanceRatio` | `float` | [17](Assets/Scripts/Services/Synthesis/FacilitySynthesisRecipeSO.cs#L17) | 수치/설정 | — | — |

### `FactionAllianceBenefitBudgetSO`

도달 SO: `FactionAllianceBenefitBudgetSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `schemaVersion` | `int` | [25](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L25) | 수치/설정 | — | — |
| `approvedBalanceSourceDigest` | `string` | [26](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L26) | semantic-key | — | — |
| `capacityMilliEwu` | `long` | [27](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L27) | 수치/설정 | — | — |
| `refillNumeratorMilliEwu` | `long` | [28](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L28) | 수치/설정 | — | — |
| `refillDenominatorDays` | `long` | [29](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L29) | 수치/설정 | — | — |
| `routeCosts` | `List<FactionAllianceBenefitRouteCostRecord>` | [30](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L30) | 중첩 데이터 | — | — |

### `FactionAllianceBenefitRouteCostRecord`

도달 SO: `FactionAllianceBenefitBudgetSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `factionId` | `string` | [10](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L10) | relation-annotated | ContentReference(ContentReferenceKind.Faction) | — |
| `cooldownDays` | `int` | [11](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L11) | 수치/설정 | — | — |
| `supplyQuoteSourceDigest` | `string` | [12](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L12) | semantic-key | — | — |
| `debitMilliEwu` | `long` | [13](Assets/Scripts/Models/Factions/Core/FactionAllianceBenefitBudgetSO.cs#L13) | 수치/설정 | — | — |

### `FactionArcDefinitionSO`

도달 SO: `FactionArcDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `factionId` | `string` | [8](Assets/Scripts/Content/FactionArcDefinitionSO.cs#L8) | relation-annotated-contextual-gap | ContentReference(ContentReferenceKind.Faction) | — |
| `chapterIds` | `List<string>` | [9](Assets/Scripts/Content/FactionArcDefinitionSO.cs#L9) | relation-annotated-contextual-gap | ContentReference(ContentReferenceKind.FactionChapter) | R06 |
| `contractIds` | `List<string>` | [10](Assets/Scripts/Content/FactionArcDefinitionSO.cs#L10) | relation-annotated-contextual-gap | ContentReference(ContentReferenceKind.FactionContract) | R06 |
| `relicItemIds` | `List<string>` | [11](Assets/Scripts/Content/FactionArcDefinitionSO.cs#L11) | relation-annotated-no-active-consumer | ContentReference(ContentReferenceKind.Item) | R06 |

### `FactionChapterDefinitionSO`

도달 SO: `FactionChapterDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `factionId` | `string` | [10](Assets/Scripts/Content/FactionChapterDefinitionSO.cs#L10) | relation-annotated-contextual-gap | ContentReference(ContentReferenceKind.Faction) | R06 |
| `chapterNumber` | `int` | [11](Assets/Scripts/Content/FactionChapterDefinitionSO.cs#L11) | 수치/설정 | — | R06 |
| `kind` | `FactionChapterKind` | [12](Assets/Scripts/Content/FactionChapterDefinitionSO.cs#L12) | enum | — | — |
| `crossFactionId` | `string` | [13](Assets/Scripts/Content/FactionChapterDefinitionSO.cs#L13) | relation-annotated | ContentReference(ContentReferenceKind.Faction) | — |
| `triggerRequirements` | `V20ContentRequirementSet` | [14](Assets/Scripts/Content/FactionChapterDefinitionSO.cs#L14) | 중첩 데이터 | — | — |
| `choices` | `List<V20ChoiceDefinition>` | [15](Assets/Scripts/Content/FactionChapterDefinitionSO.cs#L15) | 중첩 데이터 | — | — |

### `FactionContractDefinitionSO`

도달 SO: `FactionContractDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `factionId` | `string` | [32](Assets/Scripts/Content/FactionContractDefinitionSO.cs#L32) | relation-annotated-contextual-gap | ContentReference(ContentReferenceKind.Faction) | — |
| `seasonalEventId` | `string` | [34](Assets/Scripts/Content/FactionContractDefinitionSO.cs#L34) | relation-annotated | ContentReference("SeasonalWorldEventDefinitionSO", "StableId") | — |
| `kind` | `V20FactionContractKind` | [35](Assets/Scripts/Content/FactionContractDefinitionSO.cs#L35) | enum | — | — |
| `deadlineDays` | `int` | [36](Assets/Scripts/Content/FactionContractDefinitionSO.cs#L36) | 수치/설정 | — | — |
| `completionRequirements` | `V20ContentRequirementSet` | [37](Assets/Scripts/Content/FactionContractDefinitionSO.cs#L37) | 중첩 데이터 | — | — |
| `successEffects` | `List<V20ContentEffect>` | [38](Assets/Scripts/Content/FactionContractDefinitionSO.cs#L38) | 중첩 데이터 | — | — |
| `failureEffects` | `List<V20ContentEffect>` | [39](Assets/Scripts/Content/FactionContractDefinitionSO.cs#L39) | 중첩 데이터 | — | — |

### `FestivalDefinitionSO`

도달 SO: `FestivalDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `festivalId` | `string` | [11](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L11) | ownID | ContentIdentity | — |
| `displayName` | `string` | [12](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L12) | display | — | — |
| `description` | `string` | [13](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L13) | display | — | — |
| `authoringRevision` | `int` | [14](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L14) | 수치/설정 | — | — |
| `sourceNote` | `string` | [15](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L15) | display | — | — |
| `season` | `Season` | [16](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L16) | enum | — | — |
| `dayOfSeason` | `int` | [18](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L18) | 수치/설정 | — | — |
| `convertsActiveGrief` | `bool` | [19](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L19) | 수치/설정 | — | — |
| `cultureId` | `string` | [21](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L21) | SOrelation | ContentReference(ContentReferenceKind.Culture) | — |
| `allowsVisitors` | `bool` | [23](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L23) | 수치/설정 | — | — |
| `requiredBuildingDefinitionId` | `string` | [24](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L24) | semantic | — | — |
| `venueRequirements` | `FacilityVenueRequirements` | [25](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L25) | 중첩 데이터 | — | — |
| `requiredItems` | `List<FestivalItemRequirement>` | [26](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L26) | semantic | — | — |
| `minimumParticipants` | `int` | [27](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L27) | 수치/설정 | — | — |
| `successOutcome` | `FestivalOutcomeDefinition` | [28](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L28) | 중첩 데이터 | — | — |
| `partialOutcome` | `FestivalOutcomeDefinition` | [29](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L29) | 중첩 데이터 | — | — |
| `failureOutcome` | `FestivalOutcomeDefinition` | [30](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L30) | 중첩 데이터 | — | — |

### `FestivalItemRequirement`

도달 SO: `FestivalDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemDefinitionId` | `string` | [58](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L58) | SOrelation | ContentReference(ContentReferenceKind.Item) | — |
| `amount` | `int` | [59](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L59) | 수치/설정 | — | — |

### `FestivalOutcomeDefinition`

도달 SO: `FestivalDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `moodDelta` | `float` | [66](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L66) | 수치/설정 | — | — |
| `moodDurationDays` | `int` | [67](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L67) | 수치/설정 | — | — |
| `factionRapportDelta` | `int` | [68](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L68) | nonrelation | — | — |
| `griefConversionPercent` | `float` | [69](Assets/Scripts/Models/Species/Core/FestivalDefinitionSO.cs#L69) | 수치/설정 | — | — |

### `FoodItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `quality` | `MealQualityTier` | [75](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L75) | enum | — | — |
| `qualityBand` | `MealQualityBand` | [76](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L76) | enum | — | — |
| `servingRole` | `MealServingRole` | [77](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L77) | enum | — | — |
| `nutrition` | `float` | [78](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L78) | 수치/설정 | — | — |
| `mood` | `float` | [79](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L79) | 수치/설정 | — | — |
| `freshnessSeconds` | `float` | [80](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L80) | 수치/설정 | — | — |
| `preserved` | `bool` | [81](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L81) | 수치/설정 | — | — |

### `ForbiddenResearchLeapRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `breakthroughChance` | `float` | [238](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L238) | 수치/설정 | — | — |
| `setbackChance` | `float` | [239](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L239) | 수치/설정 | — | — |
| `breakthroughProgress` | `float` | [240](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L240) | 수치/설정 | — | — |
| `setbackProgress` | `float` | [241](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L241) | 수치/설정 | — | — |
| `aftermathDays` | `int` | [242](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L242) | 수치/설정 | — | — |

### `FuneralCultureSO`

도달 SO: `FuneralCultureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `cultureId` | `string` | [9](Assets/Scripts/Models/Species/Core/FuneralCultureSO.cs#L9) | ownID | ContentIdentity | — |
| `speciesTag` | `string` | [11](Assets/Scripts/Models/Species/Core/FuneralCultureSO.cs#L11) | SOrelation | ContentReference(ContentReferenceKind.Species) | — |
| `ritualName` | `string` | [12](Assets/Scripts/Models/Species/Core/FuneralCultureSO.cs#L12) | semantic | — | — |
| `requiredFacilityTag` | `string` | [13](Assets/Scripts/Models/Species/Core/FuneralCultureSO.cs#L13) | semantic | — | — |

### `GameContentCatalogSO`

도달 SO: `GameContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemDefinitions` | `ScriptableObject` | [21](Assets/Scripts/Content/GameContentCatalogSO.cs#L21) | direct-object-reference | — | — |
| `worldPresentation` | `ScriptableObject` | [22](Assets/Scripts/Content/GameContentCatalogSO.cs#L22) | direct-object-reference | — | — |
| `characterSkillSettings` | `ScriptableObject` | [23](Assets/Scripts/Content/GameContentCatalogSO.cs#L23) | direct-object-reference | — | — |
| `media` | `ScriptableObject` | [24](Assets/Scripts/Content/GameContentCatalogSO.cs#L24) | direct-object-reference | — | — |
| `domainCatalogs` | `List<ScriptableObject>` | [25](Assets/Scripts/Content/GameContentCatalogSO.cs#L25) | direct-object-reference | — | — |

### `GameData`

도달 SO: `GameData`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `startingMoney` | `int` | [6](Assets/Scripts/Models/GameData/GameData.cs#L6) | 수치/설정 | — | — |
| `startingDay` | `int` | [7](Assets/Scripts/Models/GameData/GameData.cs#L7) | 수치/설정 | — | — |
| `startingGameSpeed` | `int` | [8](Assets/Scripts/Models/GameData/GameData.cs#L8) | 수치/설정 | — | — |
| `secondsPerDay` | `float` | [9](Assets/Scripts/Models/GameData/GameData.cs#L9) | 수치/설정 | — | — |

### `GameDomainContentCatalogSO`

도달 SO: `GameDomainContentCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `definitions` | `List<ScriptableObject>` | [15](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L15) | direct-object-reference | — | R05 |
| `metaUpgrades` | `List<AuthoredMetaUpgradeRecord>` | [17](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L17) | 중첩 데이터 | — | — |
| `runVariables` | `List<AuthoredRunVariableRecord>` | [18](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L18) | 중첩 데이터 | — | — |
| `ownerDoctrines` | `List<AuthoredOwnerDoctrineRecord>` | [19](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L19) | 중첩 데이터 | — | — |
| `invasionPatterns` | `List<AuthoredInvasionPatternRecord>` | [20](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L20) | 중첩 데이터 | — | — |
| `characterNeeds` | `List<AuthoredCharacterNeedRecord>` | [21](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L21) | 중첩 데이터 | — | — |
| `stockCategories` | `List<AuthoredStockCategoryRecord>` | [22](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L22) | 중첩 데이터 | — | — |
| `buildingCategories` | `List<AuthoredBuildingCategoryRecord>` | [23](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L23) | nested-authored-value | — | — |
| `coreSessionRules` | `CoreSessionRulesSO` | [24](Assets/Scripts/Content/GameDomainContentCatalogSO.cs#L24) | direct-object-reference | — | — |

### `GameMediaCatalogSO`

도달 SO: `GameMediaCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `audioLibrary` | `DungeonAudioLibrarySO` | [14](Assets/Scripts/Content/GameMediaCatalogSO.cs#L14) | direct-object-reference | — | — |
| `audioMixer` | `AudioMixer` | [15](Assets/Scripts/Content/GameMediaCatalogSO.cs#L15) | direct-object-reference | — | — |
| `koreanFontSettings` | `TmpKoreanFontSettingsSO` | [16](Assets/Scripts/Content/GameMediaCatalogSO.cs#L16) | direct-object-reference | — | — |
| `titleIcon` | `Sprite` | [17](Assets/Scripts/Content/GameMediaCatalogSO.cs#L17) | direct-object-reference | — | — |
| `doorSpriteMaterial` | `Material` | [18](Assets/Scripts/Content/GameMediaCatalogSO.cs#L18) | direct-object-reference | — | — |

### `GameplayEffectBinding`

도달 SO: `CharacterAcquiredTraitModuleSO`, `CharacterAcquiredTraitSettingsSO`, `CharacterSpeciesSO`, `CharacterTraitSO`, `CombatArmorSO`, `CombatEquipmentDefinitionSO`, `CombatShieldSO`, `CombatWeaponSO`, `EquipmentModuleDefinitionSO`, `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResearchProjectSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `bindingId` | `string` | [234](Assets/Scripts/Services/Foundation/GameplayEffectContracts.cs#L234) | ownID | — | — |
| `definition` | `GameplayEffectDefinitionSO` | [235](Assets/Scripts/Services/Foundation/GameplayEffectContracts.cs#L235) | directObject | — | — |
| `value` | `float` | [236](Assets/Scripts/Services/Foundation/GameplayEffectContracts.cs#L236) | 수치/설정 | — | — |
| `condition` | `GameplayEffectConditionDefinitionSO` | [237](Assets/Scripts/Services/Foundation/GameplayEffectContracts.cs#L237) | directObject | — | — |

### `GameplayEffectConditionDefinitionSO`

도달 SO: `GameplayEffectConditionDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `numericId` | `int` | [6](Assets/Scripts/Services/Foundation/GameplayEffectConditionDefinitionSO.cs#L6) | self-key | ContentIdentity | — |
| `conditionId` | `string` | [7](Assets/Scripts/Services/Foundation/GameplayEffectConditionDefinitionSO.cs#L7) | self-key | ContentIdentity | — |
| `description` | `string` | [8](Assets/Scripts/Services/Foundation/GameplayEffectConditionDefinitionSO.cs#L8) | display-text | — | — |

### `GameplayEffectDefinitionSO`

도달 SO: `GameplayEffectDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `numericId` | `int` | [7](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L7) | self-key | ContentIdentity | — |
| `effectId` | `string` | [8](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L8) | self-key | ContentIdentity | — |
| `targetId` | `string` | [9](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L9) | semantic-key | — | — |
| `operation` | `GameplayEffectOperation` | [10](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L10) | enum | — | — |
| `projectionPhase` | `GameplayEffectProjectionPhase` | [11](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L11) | enum | — | — |
| `allowedSources` | `GameplayEffectSourceKind` | [12](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L12) | enum | — | — |
| `stackingPolicy` | `GameplayEffectStackingPolicy` | [14](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L14) | enum | — | — |
| `minimumResult` | `float` | [16](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L16) | 수치/설정 | — | — |
| `maximumResult` | `float` | [17](Assets/Scripts/Services/Foundation/GameplayEffectDefinitionSO.cs#L17) | 수치/설정 | — | — |

### `GameplayExperienceAssessmentSnapshot`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `policyVersion` | `int` | [112](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L112) | 수치/설정 | — | — |
| `observerId` | `string` | [113](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L113) | runtimeSnapshot | — | — |
| `category` | `int` | [115](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L115) | 수치/설정 | — | — |
| `importance` | `float` | [116](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L116) | 수치/설정 | — | — |
| `reasonCode` | `string` | [117](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L117) | runtimeSnapshot | — | — |
| `explanationKo` | `string` | [118](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L118) | runtimeSnapshot | — | — |
| `evidenceMetricIds` | `List<string>` | [119](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L119) | runtimeSnapshot | — | — |
| `evidenceFactIds` | `List<string>` | [120](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L120) | runtimeSnapshot | — | — |
| `evidenceRoleIds` | `List<string>` | [121](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L121) | runtimeSnapshot | — | — |

### `GameplayOutcomeEvidenceBindingSnapshot`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `schemaVersion` | `int` | [17](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L17) | 수치/설정 | — | — |
| `sourceKind` | `string` | [18](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L18) | runtimeSnapshot | — | — |
| `publicFactId` | `string` | [19](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L19) | runtimeSnapshot | — | — |
| `outcomeRunId` | `string` | [20](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L20) | runtimeSnapshot | — | — |
| `outcomeSequence` | `long` | [21](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L21) | 수치/설정 | — | — |
| `compactedMemoryId` | `string` | [22](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L22) | runtimeSnapshot | — | — |
| `outcomeTypeId` | `string` | [23](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L23) | runtimeSnapshot | — | — |
| `subjectKindId` | `string` | [24](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L24) | runtimeSnapshot | — | — |
| `subjectId` | `string` | [25](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L25) | runtimeSnapshot | — | — |
| `perspectiveKind` | `int` | [26](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L26) | 수치/설정 | — | — |
| `firstDay` | `int` | [27](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L27) | 수치/설정 | — | — |
| `lastDay` | `int` | [28](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L28) | 수치/설정 | — | — |
| `occurrenceCount` | `int` | [29](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L29) | 수치/설정 | — | — |
| `anchorRevision` | `int` | [30](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L30) | 수치/설정 | — | — |
| `status` | `int` | [31](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L31) | 수치/설정 | — | — |
| `subjectSalience` | `float` | [32](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L32) | 수치/설정 | — | — |
| `experience` | `GameplayExperienceAssessmentSnapshot` | [35](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L35) | 중첩 데이터 | — | — |
| `influenceUseCount` | `int` | [36](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L36) | 수치/설정 | — | — |
| `influenceRevision` | `int` | [37](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L37) | 수치/설정 | — | — |
| `canonicalFactText` | `string` | [38](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L38) | runtimeSnapshot | — | — |
| `roleIds` | `List<string>` | [39](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L39) | runtimeSnapshot | — | — |
| `metricIds` | `List<string>` | [40](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L40) | runtimeSnapshot | — | — |
| `metricReferenceIds` | `List<string>` | [41](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L41) | runtimeSnapshot | — | — |
| `factIds` | `List<string>` | [42](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L42) | runtimeSnapshot | — | — |
| `semanticTags` | `List<string>` | [43](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L43) | runtimeSnapshot | — | — |
| `participants` | `List<GameplayOutcomeEvidenceParticipantBindingSnapshot>` | [44](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L44) | 중첩 데이터 | — | — |
| `metrics` | `List<GameplayOutcomeEvidenceMetricBindingSnapshot>` | [45](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L45) | 중첩 데이터 | — | — |
| `facts` | `List<GameplayOutcomeEvidenceFactBindingSnapshot>` | [46](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L46) | 중첩 데이터 | — | — |
| `provenance` | `List<GameplayOutcomeEvidenceProvenanceBindingSnapshot>` | [47](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L47) | 중첩 데이터 | — | — |
| `locationId` | `string` | [48](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L48) | runtimeSnapshot | — | — |
| `roomId` | `string` | [49](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L49) | runtimeSnapshot | — | — |
| `locationX` | `int` | [50](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L50) | 수치/설정 | — | — |
| `locationY` | `int` | [51](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L51) | 수치/설정 | — | — |
| `operationId` | `string` | [52](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L52) | runtimeSnapshot | — | — |
| `parentOutcomeId` | `string` | [53](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L53) | runtimeSnapshot | — | — |
| `rootOperationId` | `string` | [54](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L54) | runtimeSnapshot | — | — |
| `causationRelationId` | `string` | [55](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L55) | runtimeSnapshot | — | — |

### `GameplayOutcomeEvidenceFactBindingSnapshot`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `factId` | `string` | [198](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L198) | runtimeSnapshot | — | — |
| `value` | `string` | [199](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L199) | runtimeSnapshot | — | — |

### `GameplayOutcomeEvidenceMetricBindingSnapshot`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `metricId` | `string` | [173](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L173) | runtimeSnapshot | — | — |
| `unitId` | `string` | [174](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L174) | runtimeSnapshot | — | — |
| `referenceKindId` | `string` | [175](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L175) | runtimeSnapshot | — | — |
| `referenceId` | `string` | [176](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L176) | runtimeSnapshot | — | — |
| `value` | `double` | [177](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L177) | 수치/설정 | — | — |
| `minimum` | `double` | [178](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L178) | 수치/설정 | — | — |
| `maximum` | `double` | [179](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L179) | 수치/설정 | — | — |
| `sampleCount` | `int` | [180](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L180) | 수치/설정 | — | — |

### `GameplayOutcomeEvidenceParticipantBindingSnapshot`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `entityKindId` | `string` | [140](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L140) | runtimeSnapshot | — | — |
| `entityId` | `string` | [141](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L141) | runtimeSnapshot | — | — |
| `roleId` | `string` | [142](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L142) | runtimeSnapshot | — | — |
| `participationKind` | `int` | [143](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L143) | 수치/설정 | — | — |
| `hasPerceptionEvidence` | `bool` | [144](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L144) | runtimeSnapshot | — | — |
| `displayText` | `string` | [145](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L145) | runtimeSnapshot | — | — |
| `displayRevision` | `string` | [146](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L146) | runtimeSnapshot | — | — |
| `locale` | `string` | [147](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L147) | runtimeSnapshot | — | — |
| `pronunciationMode` | `int` | [148](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L148) | 수치/설정 | — | — |
| `pronunciationValue` | `string` | [149](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L149) | runtimeSnapshot | — | — |
| `finalConsonant` | `int` | [150](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L150) | 수치/설정 | — | — |
| `pronunciationRevision` | `string` | [151](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L151) | runtimeSnapshot | — | — |

### `GameplayOutcomeEvidenceProvenanceBindingSnapshot`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kindId` | `string` | [211](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L211) | runtimeSnapshot | — | — |
| `value` | `string` | [212](Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs#L212) | runtimeSnapshot | — | — |

### `GoldenHarvestRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `delayHours` | `int` | [277](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L277) | 수치/설정 | — | — |
| `jackpotChance` | `float` | [278](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L278) | 수치/설정 | — | — |
| `lossChance` | `float` | [279](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L279) | 수치/설정 | — | — |
| `failureYieldMultiplier` | `float` | [280](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L280) | 수치/설정 | — | — |

### `GuestRequestDefinitionSO`

도달 SO: `GuestRequestDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `retirementReason` | `string` | [12](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L12) | display-text | — | — |
| `kind` | `GuestRequestKind` | [14](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L14) | enum | — | — |
| `riskTier` | `ExperienceEventRiskTier` | [15](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L15) | enum | — | — |
| `riskReason` | `string` | [16](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L16) | display-text | — | — |
| `deadlineDays` | `int` | [17](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L17) | 수치/설정 | — | — |
| `serviceRequirements` | `V20ContentRequirementSet` | [18](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L18) | 중첩 데이터 | — | — |
| `venueRequirements` | `FacilityVenueRequirements` | [19](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L19) | 중첩 데이터 | — | — |
| `successEffects` | `List<V20ContentEffect>` | [20](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L20) | 중첩 데이터 | — | — |
| `failureEffects` | `List<V20ContentEffect>` | [21](Assets/Scripts/Content/GuestRequestDefinitionSO.cs#L21) | 중첩 데이터 | — | — |

### `HealSurgicalNodeEffect`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `health` | `float` | [393](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L393) | 수치/설정 | — | — |
| `infectionReduction` | `float` | [394](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L394) | 수치/설정 | — | — |

### `HeritableTraitConsequence`

도달 SO: `HeritableTraitDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `HeritableTraitConsequenceKind` | [63](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L63) | enum | — | — |
| `targetId` | `string` | [64](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L64) | semantic | — | R22 |
| `multiplierDelta` | `float` | [65](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L65) | 수치/설정 | — | — |

### `HeritableTraitDefinitionSO`

도달 SO: `HeritableTraitDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `traitId` | `string` | [11](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L11) | ownID | ContentIdentity | — |
| `displayName` | `string` | [12](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L12) | display | — | — |
| `description` | `string` | [13](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L13) | display | — | — |
| `authoringRevision` | `int` | [14](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L14) | 수치/설정 | — | — |
| `sourceNote` | `string` | [15](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L15) | display | — | — |
| `category` | `HeritableTraitCategory` | [16](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L16) | enum | — | — |
| `incompatibilityGroup` | `string` | [17](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L17) | semantic | — | — |
| `aptitudeModifier` | `int` | [18](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L18) | 수치/설정 | — | — |
| `compatibleSpeciesTags` | `List<string>` | [20](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L20) | SOrelation | ContentReference(ContentReferenceKind.Species) | — |
| `consequences` | `List<HeritableTraitConsequence>` | [21](Assets/Scripts/Models/Species/Core/HeritableTraitDefinitionSO.cs#L21) | 중첩 데이터 | — | — |

### `InGameNarrativeTextCatalogSO`

도달 SO: `InGameNarrativeTextCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `entries` | `List<InGameNarrativeTextEntry>` | [53](Assets/Scripts/Content/InGameNarrativeTextCatalogSO.cs#L53) | 중첩 데이터 | — | — |

### `InGameNarrativeTextEntry`

도달 SO: `InGameNarrativeTextCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `InGameNarrativeTextKind` | [19](Assets/Scripts/Content/InGameNarrativeTextCatalogSO.cs#L19) | enum | — | — |
| `stableId` | `string` | [21](Assets/Scripts/Content/InGameNarrativeTextCatalogSO.cs#L21) | relation-annotated | ContentReference(ContentReferenceKind.NarrativeTarget) | — |
| `inGameDescription` | `string` | [22](Assets/Scripts/Content/InGameNarrativeTextCatalogSO.cs#L22) | display-text | — | — |
| `worldBranchTag` | `string` | [23](Assets/Scripts/Content/InGameNarrativeTextCatalogSO.cs#L23) | semantic-key | — | — |

### `IncidentWeightRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `incidentId` | `string` | [180](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L180) | semantic | — | — |
| `multiplier` | `float` | [181](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L181) | 수치/설정 | — | — |

### `InstallSurgicalPartEffect`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `partKind` | `SurgicalPartKind` | [439](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L439) | enum | — | — |
| `requiredItemDefinitionId` | `string` | [441](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L441) | SOrelation | ContentReference(ContentReferenceKind.Item) | — |
| `efficiency` | `float` | [442](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L442) | 수치/설정 | — | — |

### `InstallationItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `buildingDefinitionId` | `int` | [477](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L477) | relation-annotated | ContentReference(ContentReferenceKind.BuildingNumeric) | — |

### `InstalledSurgicalPartEffectItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `effects` | `List<GameplayEffectBinding>` | [402](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L402) | 중첩 데이터 | — | — |

### `ItemAmountDefinition`

도달 SO: `BuildingSO`, `CombatArmorSO`, `CombatEquipmentDefinitionSO`, `CombatShieldSO`, `CombatWeaponSO`, `ProductionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [167](Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs#L167) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `amount` | `int` | [168](Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs#L168) | 수치/설정 | — | — |

### `ItemDefinitionCatalogSO`

도달 SO: `ItemDefinitionCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `definitions` | `List<ItemDefinitionSO>` | [14](Assets/Scripts/Models/Economy/Content/ItemDefinitionCatalogSO.cs#L14) | direct-object-reference | — | — |

### `ItemDefinitionSO`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [555](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L555) | self-key | ContentIdentity | — |
| `displayName` | `string` | [556](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L556) | authored-value | — | — |
| `description` | `string` | [557](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L557) | authored-value | — | — |
| `stockCategory` | `StockCategory` | [560](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L560) | enum | — | — |
| `unitWeight` | `float` | [561](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L561) | 수치/설정 | — | — |
| `maxStack` | `int` | [562](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L562) | 수치/설정 | — | — |
| `sprite` | `Sprite` | [563](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L563) | presentation-object | — | — |
| `unitPrice` | `int` | [566](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L566) | 수치/설정 | — | — |
| `features` | `List<ItemFeatureDefinition>` | [569](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L569) | 중첩 데이터 | — | — |

### `ItemHaulingSettingsSO`

도달 SO: `ItemHaulingSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `maxCarryMultiplier` | `float` | [12](Assets/Scripts/Services/Items/ItemHaulingSettingsSO.cs#L12) | 수치/설정 | — | — |

### `LastStandRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `healthThreshold` | `float` | [222](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L222) | 수치/설정 | — | — |
| `aftermathDays` | `int` | [223](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L223) | 수치/설정 | — | — |

### `LegacyWasteItemRuleRecord`

도달 SO: `WasteProcessingRulesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [38](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L38) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `origin` | `WasteOriginKind` | [39](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L39) | enum | — | — |
| `contamination` | `float` | [40](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L40) | 수치/설정 | — | — |

### `LifeEventDefinitionSO`

도달 SO: `LifeEventDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `category` | `LifeEventCategory` | [16](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L16) | enum | — | — |
| `riskTier` | `ExperienceEventRiskTier` | [17](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L17) | enum | — | — |
| `riskReason` | `string` | [18](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L18) | display-text | — | — |
| `automatic` | `bool` | [19](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L19) | 수치/설정 | — | — |
| `emergency` | `bool` | [20](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L20) | 수치/설정 | — | — |
| `responseDeadlineDays` | `int` | [21](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L21) | 수치/설정 | — | — |
| `cooldownDays` | `int` | [22](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L22) | 수치/설정 | — | — |
| `frequencyRule` | `LifeEventFrequencyRule` | [23](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L23) | enum | — | — |
| `occurrencePolicy` | `LifeEventOccurrencePolicy` | [24](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L24) | enum | — | — |
| `triggerRequirements` | `V20ContentRequirementSet` | [25](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L25) | 중첩 데이터 | — | — |
| `choices` | `List<V20ChoiceDefinition>` | [26](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L26) | 중첩 데이터 | — | — |
| `automaticEffects` | `List<V20ContentEffect>` | [27](Assets/Scripts/Content/LifeEventDefinitionSO.cs#L27) | 중첩 데이터 | — | — |

### `MaintainSurgicalPartEffect`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `durability` | `float` | [424](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L424) | nonrelation | — | — |
| `contaminationReduction` | `float` | [425](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L425) | 수치/설정 | — | — |

### `MarketItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `saleRate` | `float` | [48](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L48) | 수치/설정 | — | — |

### `MaterialEconomicProfileSO`

도달 SO: `MaterialEconomicProfileSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `physicalItemId` | `string` | [14](Assets/Scripts/Models/Economy/Content/MaterialEconomicProfileSO.cs#L14) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `intrinsicValue` | `float` | [15](Assets/Scripts/Models/Economy/Content/MaterialEconomicProfileSO.cs#L15) | 수치/설정 | — | — |
| `handlingDifficulty` | `float` | [16](Assets/Scripts/Models/Economy/Content/MaterialEconomicProfileSO.cs#L16) | 수치/설정 | — | — |
| `salvageRetention` | `float` | [17](Assets/Scripts/Models/Economy/Content/MaterialEconomicProfileSO.cs#L17) | 수치/설정 | — | — |
| `consumableDuringCraft` | `bool` | [18](Assets/Scripts/Models/Economy/Content/MaterialEconomicProfileSO.cs#L18) | 수치/설정 | — | — |

### `MedicalProcedureSupplyItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `procedureId` | `string` | [224](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L224) | relation-annotated | ContentReference("SurgicalProcedureSO", "ProcedureId") | — |

### `MedicineItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `supportsInjuryTreatment` | `bool` | [92](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L92) | 수치/설정 | — | — |
| `treatmentPotency` | `float` | [93](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L93) | 수치/설정 | — | — |
| `infectionReduction` | `float` | [94](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L94) | 수치/설정 | — | — |
| `detoxReduction` | `float` | [95](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L95) | 수치/설정 | — | — |
| `painReduction` | `float` | [96](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L96) | 수치/설정 | — | — |

### `MiracleSurgeryRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `miracleChance` | `float` | [259](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L259) | 수치/설정 | — | — |
| `complicationChance` | `float` | [260](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L260) | 수치/설정 | — | — |
| `aftermathDays` | `int` | [261](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L261) | 수치/설정 | — | — |

### `MoodImmunityRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `eventId` | `string` | [87](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L87) | semantic | — | — |

### `MoodTransformRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `eventId` | `string` | [100](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L100) | semantic | — | — |
| `multiplier` | `float` | [101](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L101) | 수치/설정 | — | — |
| `additiveDelta` | `float` | [102](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L102) | 수치/설정 | — | — |

### `NarrativeFormulaDrawbackCreditPolicyDefinition`

도달 SO: `CharacterAcquiredTraitSettingsSO`, `CharacterSkillSystemSettingsSO`, `EquipmentEvolutionFormulaCatalogSO`, `FacilityEvolutionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `playerChoiceMaximumBudgetFraction` | `float` | [7](Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaDrawbackPolicyDefinition.cs#L7) | 수치/설정 | — | — |
| `automaticMaximumBudgetFraction` | `float` | [8](Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaDrawbackPolicyDefinition.cs#L8) | 수치/설정 | — | — |
| `absoluteMaximumCredit` | `int` | [9](Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaDrawbackPolicyDefinition.cs#L9) | 수치/설정 | — | — |
| `requireNegativeEvidenceForAutomatic` | `bool` | [10](Assets/Scripts/Models/NarrativeMechanics/NarrativeFormulaDrawbackPolicyDefinition.cs#L10) | nonrelation | — | — |

### `NarrativeGenerationTrace`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `schemaId` | `string` | [20](Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs#L20) | runtimeSnapshot | — | — |
| `schemaVersion` | `int` | [21](Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs#L21) | 수치/설정 | — | — |
| `schemaHash` | `string` | [22](Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs#L22) | runtimeSnapshot | — | — |
| `cultureStyleId` | `string` | [23](Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs#L23) | runtimeSnapshot | — | — |
| `usedMotifIds` | `string[]` | [24](Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs#L24) | runtimeSnapshot | — | — |
| `usedCharacterFactIds` | `string[]` | [25](Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs#L25) | runtimeSnapshot | — | — |
| `verdict` | `NarrativeQualityVerdict` | [26](Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs#L26) | enum | — | — |
| `retryCount` | `int` | [27](Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs#L27) | 수치/설정 | — | — |
| `usedFallback` | `bool` | [28](Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs#L28) | 수치/설정 | — | — |

### `OffenseAttackModifierEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `multiplierDelta` | `float` | [264](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L264) | 수치/설정 | — | — |
| `turns` | `int` | [265](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L265) | 수치/설정 | — | — |

### `OffenseCampaignCatalogSO`

도달 SO: `OffenseCampaignCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `targets` | `List<OffenseTargetDefinition>` | [11](Assets/Scripts/Services/Offense/OffenseCampaignCatalogSO.cs#L11) | nested-authored-value | — | — |

### `OffenseCleanseEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `maximumStatuses` | `int` | [333](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L333) | 수치/설정 | — | — |

### `OffenseConditionalAmplifyEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `extraDamageMultiplier` | `float` | [369](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L369) | 수치/설정 | — | — |
| `healthThreshold` | `float` | [370](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L370) | 수치/설정 | — | — |

### `OffenseCooldownAdjustEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `turnDelta` | `int` | [395](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L395) | 수치/설정 | — | — |

### `OffenseDamageEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `basicDamageMultiplier` | `float` | [129](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L129) | 수치/설정 | — | — |
| `flatDamage` | `float` | [130](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L130) | 수치/설정 | — | — |
| `hitCount` | `int` | [131](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L131) | 수치/설정 | — | — |

### `OffenseDamageOverTimeEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `damagePerTurn` | `float` | [201](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L201) | 수치/설정 | — | — |
| `turns` | `int` | [202](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L202) | 수치/설정 | — | — |

### `OffenseDecisionCardSO`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `cardId` | `string` | [12](Assets/Scripts/Models/Offense/Core/OffenseDecisionCardSO.cs#L12) | self-key | ContentIdentity | — |
| `stage` | `OffenseDecisionStage` | [13](Assets/Scripts/Models/Offense/Core/OffenseDecisionCardSO.cs#L13) | authored-value | — | — |
| `title` | `string` | [14](Assets/Scripts/Models/Offense/Core/OffenseDecisionCardSO.cs#L14) | display-text | — | — |
| `situation` | `string` | [15](Assets/Scripts/Models/Offense/Core/OffenseDecisionCardSO.cs#L15) | display-text | — | — |
| `requiredWorldTags` | `List<string>` | [16](Assets/Scripts/Models/Offense/Core/OffenseDecisionCardSO.cs#L16) | 조건부 날씨 관계 / R16 | — | R16 |
| `choices` | `List<OffenseDecisionChoiceDefinition>` | [17](Assets/Scripts/Models/Offense/Core/OffenseDecisionCardSO.cs#L17) | 중첩 데이터 | — | — |

### `OffenseDecisionChoiceDefinition`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `choiceId` | `string` | [22](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L22) | self-key | — | — |
| `label` | `string` | [23](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L23) | display-text | — | — |
| `description` | `string` | [24](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L24) | display-text | — | — |
| `requiredTag` | `string` | [25](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L25) | semantic-key | — | R16 |
| `transformedLabel` | `string` | [26](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L26) | display-text | — | — |
| `transformedDescription` | `string` | [27](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L27) | display-text | — | — |
| `directionLabel` | `string` | [28](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L28) | display-text | — | — |
| `severity` | `int` | [29](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L29) | 수치/설정 | — | — |
| `mayStartCombat` | `bool` | [30](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L30) | 수치/설정 | — | — |
| `mayCauseInjury` | `bool` | [31](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L31) | 수치/설정 | — | — |
| `mayMoveExpedition` | `bool` | [32](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L32) | 수치/설정 | — | — |
| `effects` | `List<OffenseDecisionEffectDefinition>` | [34](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L34) | 중첩 데이터 | — | — |

### `OffenseDelayEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `initiativePenalty` | `float` | [246](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L246) | 수치/설정 | — | — |

### `OffenseEncounterSO`

도달 SO: `OffenseEncounterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `encounterId` | `string` | [34](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L34) | self-key | ContentIdentity | — |
| `displayName` | `string` | [35](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L35) | display-text | — | — |
| `minimumSiteStrength` | `int` | [36](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L36) | 수치/설정 | — | — |
| `maximumSiteStrength` | `int` | [37](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L37) | 수치/설정 | — | — |
| `elite` | `bool` | [38](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L38) | 수치/설정 | — | — |
| `boss` | `bool` | [39](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L39) | 수치/설정 | — | — |
| `enemyHealthMultiplier` | `float` | [40](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L40) | 수치/설정 | — | — |
| `enemyDamageMultiplier` | `float` | [41](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L41) | 수치/설정 | — | — |
| `enemyAccuracyMultiplier` | `float` | [42](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L42) | 수치/설정 | — | — |
| `additionalEnemyCount` | `int` | [43](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L43) | 수치/설정 | — | — |
| `objectiveHealthMultiplier` | `float` | [44](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L44) | 수치/설정 | — | — |
| `objectiveControlResistanceMultiplier` | `float` | [45](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L45) | 수치/설정 | — | — |
| `objective` | `OffenseEncounterObjective` | [46](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L46) | enum | — | — |
| `objectiveRoundLimit` | `int` | [47](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L47) | 수치/설정 | — | — |
| `objectiveTargetId` | `string` | [48](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L48) | main-reviewed-conditional-relation-runtime-guarded | — | R08 |
| `battlefieldModifierIds` | `List<string>` | [50](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L50) | relation-annotated | ContentReference(ContentReferenceKind.BattlefieldModifier) | — |
| `counterTags` | `List<string>` | [51](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L51) | semantic-key | — | — |
| `rewardItemIds` | `List<string>` | [53](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L53) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `enemies` | `List<OffenseEnemyArchetypeEntry>` | [54](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L54) | 중첩 데이터 | — | — |

### `OffenseEnemyArchetypeEntry`

도달 SO: `OffenseEncounterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `enemyArchetypeId` | `string` | [12](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L12) | relation-annotated | ContentReference(ContentReferenceKind.EnemyArchetype) | — |
| `minimumCount` | `int` | [13](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L13) | 수치/설정 | — | — |
| `maximumCount` | `int` | [14](Assets/Scripts/Models/Offense/Core/OffenseEncounterSO.cs#L14) | 수치/설정 | — | — |

### `OffenseEquipmentWearDecisionEffect`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `durabilityDamage` | `float` | [140](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L140) | authored-value | — | — |

### `OffenseExposureDecisionEffect`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `amount` | `float` | [102](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L102) | 수치/설정 | — | — |

### `OffenseGoldDecisionEffect`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `amount` | `int` | [56](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L56) | 수치/설정 | — | — |
| `bribe` | `BribeOffer` | [57](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L57) | 중첩 데이터 | — | — |

### `OffenseGuardEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `damageReduction` | `float` | [177](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L177) | 수치/설정 | — | — |
| `turns` | `int` | [178](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L178) | 수치/설정 | — | — |

### `OffenseHealEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `flatAmount` | `float` | [156](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L156) | 수치/설정 | — | — |
| `damageDealtRatio` | `float` | [157](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L157) | 수치/설정 | — | — |

### `OffenseInjuryDecisionEffect`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `maxHealthRatio` | `float` | [109](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L109) | 수치/설정 | — | — |
| `nonLethal` | `bool` | [110](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L110) | 수치/설정 | — | — |

### `OffenseLootDecisionEffect`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stockCategory` | `StockCategory` | [117](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L117) | enum | — | — |
| `amount` | `int` | [118](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L118) | 수치/설정 | — | — |

### `OffenseMultiTargetEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `targetCount` | `int` | [413](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L413) | nonrelation | — | — |
| `splashMultiplier` | `float` | [414](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L414) | 수치/설정 | — | — |

### `OffensePhysicalItemRewardSpec`

도달 SO: `OffenseCampaignCatalogSO`, `OffenseSiteArchetypeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [36](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L36) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |

### `OffenseReconDecisionEffect`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `revealCount` | `int` | [125](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L125) | 수치/설정 | — | — |

### `OffenseRepositionEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `offset` | `int` | [351](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L351) | 수치/설정 | — | — |

### `OffenseRewardPreview`

도달 SO: `OffenseCampaignCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `grantSpec` | `OffenseRewardGrantSpec` | [27](Assets/Scripts/Models/Offense/Core/OffenseSiteValueContracts.cs#L27) | 중첩 데이터 | — | — |
| `displayLabel` | `string` | [28](Assets/Scripts/Models/Offense/Core/OffenseSiteValueContracts.cs#L28) | display-text | — | — |
| `configuredAmount` | `int` | [29](Assets/Scripts/Models/Offense/Core/OffenseSiteValueContracts.cs#L29) | 수치/설정 | — | — |

### `OffenseSiteArchetypeSO`

도달 SO: `OffenseSiteArchetypeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `siteTypeId` | `string` | [12](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L12) | self-key | ContentIdentity | — |
| `displayName` | `string` | [13](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L13) | display-text | — | — |
| `description` | `string` | [14](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L14) | display-text | — | — |
| `factionId` | `string` | [15](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L15) | semantic-key | — | — |
| `pressureAxis` | `StrategicPressureAxis` | [16](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L16) | enum | — | — |
| `pressureAmount` | `float` | [17](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L17) | 수치/설정 | — | — |
| `minimumStrength` | `int` | [18](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L18) | 수치/설정 | — | — |
| `maximumStrength` | `int` | [19](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L19) | 수치/설정 | — | — |
| `minimumLifetimeDays` | `int` | [20](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L20) | 수치/설정 | — | — |
| `maximumLifetimeDays` | `int` | [21](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L21) | 수치/설정 | — | — |
| `hiddenUntilDiscovered` | `bool` | [22](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L22) | authored-value | — | — |
| `canMove` | `bool` | [23](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L23) | 수치/설정 | — | — |
| `dynamicSpawnEligible` | `bool` | [24](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L24) | 수치/설정 | — | — |
| `rewards` | `List<OffenseSiteRewardDefinition>` | [25](Assets/Scripts/Models/Offense/Core/OffenseSiteArchetypeSO.cs#L25) | 중첩 데이터 | — | — |

### `OffenseSiteRewardDefinition`

도달 SO: `OffenseSiteArchetypeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `grantSpec` | `OffenseRewardGrantSpec` | [58](Assets/Scripts/Models/Offense/Core/OffenseSiteValueContracts.cs#L58) | 중첩 데이터 | — | — |
| `displayLabel` | `string` | [59](Assets/Scripts/Models/Offense/Core/OffenseSiteValueContracts.cs#L59) | display-text | — | — |
| `baseAmount` | `int` | [60](Assets/Scripts/Models/Offense/Core/OffenseSiteValueContracts.cs#L60) | 수치/설정 | — | — |
| `amountPerStrength` | `int` | [61](Assets/Scripts/Models/Offense/Core/OffenseSiteValueContracts.cs#L61) | 수치/설정 | — | — |

### `OffenseSmokeEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `obscuration` | `float` | [288](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L288) | 수치/설정 | — | — |
| `turns` | `int` | [289](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L289) | 수치/설정 | — | — |

### `OffenseSpecificBlueprintRewardSpec`

도달 SO: `OffenseCampaignCatalogSO`, `OffenseSiteArchetypeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `blueprintId` | `int` | [117](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L117) | confirmed-unvalidated-relation | — | R02 |

### `OffenseStockRewardSpec`

도달 SO: `OffenseCampaignCatalogSO`, `OffenseSiteArchetypeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stockCategory` | `StockCategory` | [16](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L16) | enum | — | — |

### `OffenseStressDecisionEffect`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `amount` | `float` | [95](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L95) | 수치/설정 | — | — |

### `OffenseSummonEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `interceptHealth` | `float` | [312](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L312) | 수치/설정 | — | — |
| `turns` | `int` | [313](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L313) | 수치/설정 | — | — |

### `OffenseSupplyDecisionEffect`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `supplyType` | `OffenseSupplyType` | [48](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L48) | enum | — | — |
| `amount` | `int` | [49](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L49) | 수치/설정 | — | — |

### `OffenseTargetDefinition`

도달 SO: `OffenseCampaignCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [169](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L169) | self-key | — | — |
| `title` | `string` | [170](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L170) | display-text | — | — |
| `description` | `string` | [171](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L171) | display-text | — | — |
| `kind` | `OffenseTargetKind` | [172](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L172) | enum | — | — |
| `regionId` | `string` | [173](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L173) | semantic-key | — | — |
| `regionDisplayName` | `string` | [174](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L174) | display-text | — | — |
| `factionId` | `string` | [175](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L175) | semantic-key | — | — |
| `strategicPressureAxis` | `StrategicPressureAxis` | [176](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L176) | enum | — | — |
| `strategicPressureAmount` | `float` | [177](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L177) | 수치/설정 | — | — |
| `campaignOrder` | `int` | [178](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L178) | 수치/설정 | — | — |
| `prerequisiteTargetId` | `string` | [179](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L179) | validated-nested-relation | — | — |
| `revealsTruth` | `bool` | [180](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L180) | 수치/설정 | — | — |
| `truthText` | `string` | [181](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L181) | display-text | — | — |
| `distance` | `float` | [182](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L182) | 수치/설정 | — | — |
| `danger` | `float` | [183](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L183) | 수치/설정 | — | — |
| `durationSeconds` | `float` | [184](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L184) | 수치/설정 | — | — |
| `requiredMembers` | `int` | [185](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L185) | 수치/설정 | — | — |
| `requiredPower` | `float` | [186](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L186) | 수치/설정 | — | — |
| `seasonalOccurrenceInstanceId` | `string` | [187](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L187) | runtime-instance-id | — | — |
| `authoredEncounterId` | `string` | [189](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L189) | relation-annotated | ContentReference("OffenseEncounterSO", "encounterId") | — |
| `encounterPreviewText` | `string` | [190](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L190) | display-text | — | — |
| `encounterRewardPreviewText` | `string` | [191](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L191) | display-text | — | — |
| `rewards` | `OffenseRewardPreview[]` | [192](Assets/Scripts/Services/Offense/OffenseWorldMapModel.cs#L192) | 중첩 데이터 | — | — |

### `OffenseTimeDecisionEffect`

도달 SO: `OffenseDecisionCardSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `elapsedHours` | `float` | [132](Assets/Scripts/Models/Offense/Core/OffenseDecisionDefinitions.cs#L132) | 수치/설정 | — | — |

### `OffenseUrgentSiteDefinitionSO`

도달 SO: `OffenseUrgentSiteDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `urgentSiteId` | `string` | [11](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L11) | self-key | ContentIdentity | — |
| `displayName` | `string` | [12](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L12) | display-text | — | — |
| `description` | `string` | [13](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L13) | display-text | — | — |
| `modifierKind` | `OffenseThreatModifierKind` | [14](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L14) | enum | — | — |
| `maximumStrength` | `float` | [15](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L15) | 수치/설정 | — | — |
| `maximumMitigation` | `float` | [16](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L16) | 수치/설정 | — | — |
| `mitigationWorkTypeId` | `string` | [17](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L17) | semantic-key | — | — |
| `mitigationItemId` | `string` | [19](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L19) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `mitigationItemAmount` | `int` | [20](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L20) | authored-value | — | — |
| `mitigationWork` | `float` | [21](Assets/Scripts/Models/Offense/Core/OffenseUrgentSiteDefinitionSO.cs#L21) | 수치/설정 | — | — |

### `OffenseVulnerabilityEffect`

도달 SO: `CharacterSpeciesSO`, `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `increasedDamage` | `float` | [222](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L222) | 수치/설정 | — | — |
| `turns` | `int` | [223](Assets/Scripts/Models/Combat/Core/CharacterCombatAbilityModels.cs#L223) | 수치/설정 | — | — |

### `PackagedLotItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `packageTareGrams` | `int` | [106](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L106) | 수치/설정 | — | — |
| `tareDisposition` | `PackageTareDisposition` | [107](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L107) | enum | — | — |
| `containerItemId` | `string` | [110](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L110) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |

### `PathogenSampleItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `diseaseId` | `string` | [205](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L205) | relation-annotated | ContentReference(ContentReferenceKind.Disease) | — |

### `PersistentNeedRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `needId` | `string` | [44](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L44) | semantic | — | — |
| `satisfiedEventId` | `string` | [45](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L45) | semantic | — | — |
| `deprivedEventId` | `string` | [46](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L46) | semantic | — | — |
| `deprivationDays` | `int` | [47](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L47) | 수치/설정 | — | — |
| `deprivedMoodDelta` | `float` | [48](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L48) | 수치/설정 | — | — |
| `satisfiedMoodDelta` | `float` | [49](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L49) | 수치/설정 | — | — |
| `moodDurationDays` | `int` | [50](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L50) | 수치/설정 | — | — |

### `PostActionConsequenceRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `actionTag` | `string` | [118](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L118) | semantic | — | — |
| `directOrdersOnly` | `bool` | [119](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L119) | 수치/설정 | — | — |
| `moodDelta` | `float` | [120](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L120) | 수치/설정 | — | — |
| `stressDelta` | `float` | [121](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L121) | 수치/설정 | — | — |
| `durationDays` | `int` | [122](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L122) | 수치/설정 | — | — |

### `ProcedureOperatorRequirement`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stats` | `List<ProcedureOperatorStatRequirement>` | [207](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L207) | 중첩 데이터 | — | — |
| `minimumWeightedScore` | `float` | [208](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L208) | 수치/설정 | — | — |

### `ProcedureOperatorStatRequirement`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `statId` | `string` | [199](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L199) | semantic | — | 미연결 |
| `weight` | `float` | [200](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L200) | 수치/설정 | — | — |
| `minimumValue` | `int` | [201](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L201) | 수치/설정 | — | — |

### `ProductionItemBufferCapacity`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [204](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L204) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `capacity` | `int` | [205](Assets/Scripts/Services/Economy/ProductionWorkshopAbilities.cs#L205) | 수치/설정 | — | — |

### `ProductionItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `ResourceItemKind` | [28](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L28) | enum | — | — |
| `ingredientTags` | `ResourceIngredientTag` | [29](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L29) | semantic-key | — | — |
| `sharedIntermediate` | `bool` | [30](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L30) | 수치/설정 | — | — |

### `ProductionLimitBreakRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `aftermathDays` | `int` | [297](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L297) | 수치/설정 | — | — |

### `ProductionMassExplanationAuthoring`

도달 SO: `ProductionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capabilityId` | `string` | [11](Assets/Scripts/Models/Economy/Content/ProductionMassExplanationAuthoring.cs#L11) | semantic-key | — | — |
| `contractVersion` | `int` | [12](Assets/Scripts/Models/Economy/Content/ProductionMassExplanationAuthoring.cs#L12) | 수치/설정 | — | — |
| `canonicalPayload` | `string` | [13](Assets/Scripts/Models/Economy/Content/ProductionMassExplanationAuthoring.cs#L13) | semantic-key | — | — |

### `ProductionOutputCostAllocationAuthoring`

도달 SO: `ProductionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capabilityId` | `string` | [12](Assets/Scripts/Models/Economy/Content/ProductionOutputCostAllocationAuthoring.cs#L12) | semantic-key | — | — |
| `contractVersion` | `int` | [13](Assets/Scripts/Models/Economy/Content/ProductionOutputCostAllocationAuthoring.cs#L13) | 수치/설정 | — | — |
| `canonicalPayload` | `string` | [14](Assets/Scripts/Models/Economy/Content/ProductionOutputCostAllocationAuthoring.cs#L14) | semantic-key | — | — |

### `ProductionOutputDefinition`

도달 SO: `ProductionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `outputLineId` | `string` | [214](Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs#L214) | semantic-key | — | — |
| `role` | `ProductionOutputRole` | [215](Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs#L215) | enum | — | — |
| `itemId` | `string` | [217](Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs#L217) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `amount` | `int` | [218](Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs#L218) | 수치/설정 | — | — |
| `probability` | `float` | [219](Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs#L219) | authored-value | — | — |

### `ProductionRecipeSO`

도달 SO: `ProductionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `recipeId` | `string` | [29](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L29) | self-key | ContentIdentity | — |
| `displayName` | `string` | [30](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L30) | authored-value | — | — |
| `description` | `string` | [31](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L31) | authored-value | — | — |
| `facilityTag` | `string` | [32](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L32) | semantic-key | — | — |
| `workstationTag` | `string` | [33](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L33) | semantic-key | — | — |
| `requiredSupportTags` | `List<string>` | [34](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L34) | semantic-key | — | — |
| `batchSupportTag` | `string` | [36](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L36) | semantic-key | — | — |
| `processKind` | `ProductionProcessKind` | [37](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L37) | enum | — | — |
| `flowRole` | `ProductionFlowRole` | [38](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L38) | enum | — | — |
| `processClass` | `ProductionProcessClass` | [39](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L39) | enum | — | — |
| `processClassAuthored` | `bool` | [40](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L40) | 수치/설정 | — | — |
| `workTypeId` | `string` | [41](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L41) | semantic-key | — | — |
| `proficiency` | `ProficiencyWorkProfileAuthoring` | [42](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L42) | 중첩 데이터 | — | — |
| `requiredResearchId` | `string` | [44](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L44) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |
| `requiredWork` | `float` | [45](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L45) | 수치/설정 | — | — |
| `preparationWork` | `float` | [46](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L46) | 수치/설정 | — | — |
| `finishingWork` | `float` | [47](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L47) | 수치/설정 | — | — |
| `processingGameHours` | `float` | [48](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L48) | 수치/설정 | — | — |
| `optimalTemperatureC` | `Vector2` | [49](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L49) | Unity 값 | — | — |
| `warningTemperatureC` | `Vector2` | [50](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L50) | Unity 값 | — | — |
| `cleanWaterPerCycle` | `float` | [51](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L51) | 수치/설정 | — | — |
| `wastewaterPerCycle` | `float` | [52](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L52) | 수치/설정 | — | — |
| `wastewaterComposition` | `ProcessWastewaterComposition` | [53](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L53) | enum | — | — |
| `allowsManualWaterFallback` | `bool` | [54](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L54) | 수치/설정 | — | — |
| `spoilageItemId` | `string` | [56](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L56) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `massExplanation` | `ProductionMassExplanationAuthoring` | [58](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L58) | 중첩 데이터 | — | — |
| `outputCostAllocation` | `ProductionOutputCostAllocationAuthoring` | [60](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L60) | 중첩 데이터 | — | — |
| `inputs` | `List<ItemAmountDefinition>` | [61](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L61) | 중첩 데이터 | — | — |
| `outputs` | `List<ProductionOutputDefinition>` | [62](Assets/Scripts/Models/Economy/Content/ProductionRecipeSO.cs#L62) | 중첩 데이터 | — | — |

### `ProficiencyDefinitionSO`

도달 SO: `ProficiencyDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `displayOrder` | `int` | [10](Assets/Scripts/Content/ProficiencyDefinitionSO.cs#L10) | 수치/설정 | — | — |

### `ProficiencyWorkProfileAuthoring`

도달 SO: `ApparelDefinitionSO`, `BuildingSO`, `CombatArmorSO`, `CombatEquipmentDefinitionSO`, `CombatShieldSO`, `CombatWeaponSO`, `ProductionRecipeSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `primaryProficiencyId` | `string` | [86](Assets/Scripts/Services/Foundation/CharacterProficiencyPrimitives.cs#L86) | relation-annotated | ContentReference(ContentReferenceKind.Proficiency) | — |
| `secondaryProficiencyId` | `string` | [88](Assets/Scripts/Services/Foundation/CharacterProficiencyPrimitives.cs#L88) | relation-annotated | ContentReference(ContentReferenceKind.Proficiency) | — |
| `primaryWeight` | `float` | [89](Assets/Scripts/Services/Foundation/CharacterProficiencyPrimitives.cs#L89) | 수치/설정 | — | — |
| `combinationMode` | `ProficiencyCombinationMode` | [90](Assets/Scripts/Services/Foundation/CharacterProficiencyPrimitives.cs#L90) | enum | — | — |
| `recommendedRank` | `CharacterProficiencyRank` | [91](Assets/Scripts/Services/Foundation/CharacterProficiencyPrimitives.cs#L91) | enum | — | — |
| `minimumRiskRank` | `CharacterProficiencyRank` | [92](Assets/Scripts/Services/Foundation/CharacterProficiencyPrimitives.cs#L92) | enum | — | — |

### `ProjectileVerb`

도달 SO: `CombatWeaponSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `projectileSpeed` | `float` | [34](Assets/Scripts/Models/Combat/Core/CombatWeaponPrimitives.cs#L34) | 수치/설정 | — | — |

### `RecoverBloodLossEffect`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `amount` | `float` | [411](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L411) | 수치/설정 | — | — |

### `RecoverableThrowVerb`

도달 SO: `CombatWeaponSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `projectileSpeed` | `float` | [44](Assets/Scripts/Models/Combat/Core/CombatWeaponPrimitives.cs#L44) | 수치/설정 | — | — |

### `ReduceSurgicalBurdenEffect`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `rejection` | `float` | [458](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L458) | 수치/설정 | — | — |
| `mutation` | `float` | [459](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L459) | 수치/설정 | — | — |
| `infection` | `float` | [460](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L460) | 수치/설정 | — | — |

### `RelationshipMemoryRule`

도달 SO: `CharacterTraitSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `eventId` | `string` | [140](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L140) | semantic | — | — |
| `relationshipDelta` | `float` | [141](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L141) | 수치/설정 | — | — |
| `dailyDecay` | `float` | [142](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L142) | 수치/설정 | — | — |
| `apologyCanClear` | `bool` | [143](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L143) | 수치/설정 | — | — |
| `restitutionRequired` | `bool` | [144](Assets/Scripts/Services/Foundation/CharacterIdentityRules.cs#L144) | 수치/설정 | — | — |

### `RemoveSurgicalNodeEffect`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `createExtractedPart` | `bool` | [432](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L432) | 수치/설정 | — | — |

### `ReproductionPhaseDefinition`

도달 SO: `ReproductionProfileSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `phase` | `ReproductionPhaseKind` | [63](Assets/Scripts/Models/Species/Core/LifeSimulationContracts.cs#L63) | enum | — | — |
| `durationDays` | `int` | [64](Assets/Scripts/Models/Species/Core/LifeSimulationContracts.cs#L64) | 수치/설정 | — | — |

### `ReproductionProfileSO`

도달 SO: `ReproductionProfileSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `definitionId` | `string` | [11](Assets/Scripts/Models/Species/Core/ReproductionProfileSO.cs#L11) | ownID | ContentIdentity | — |
| `speciesTag` | `string` | [13](Assets/Scripts/Models/Species/Core/ReproductionProfileSO.cs#L13) | SOrelation | ContentReference(ContentReferenceKind.Species) | — |
| `mode` | `ReproductionMode` | [14](Assets/Scripts/Models/Species/Core/ReproductionProfileSO.cs#L14) | enum | — | — |
| `baseSuccessChance` | `float` | [15](Assets/Scripts/Models/Species/Core/ReproductionProfileSO.cs#L15) | 수치/설정 | — | — |
| `viableTemperatureMinimum` | `float` | [16](Assets/Scripts/Models/Species/Core/ReproductionProfileSO.cs#L16) | 수치/설정 | — | — |
| `viableTemperatureMaximum` | `float` | [17](Assets/Scripts/Models/Species/Core/ReproductionProfileSO.cs#L17) | 수치/설정 | — | — |
| `phases` | `List<ReproductionPhaseDefinition>` | [18](Assets/Scripts/Models/Species/Core/ReproductionProfileSO.cs#L18) | 중첩 데이터 | — | — |

### `ResearchFacilityContribution`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capability` | `ResearchFacilityCapabilityId` | [38](Assets/Scripts/Models/Research/Core/ResearchFacilityContracts.cs#L38) | semantic-key | — | — |
| `capacity` | `int` | [39](Assets/Scripts/Models/Research/Core/ResearchFacilityContracts.cs#L39) | 수치/설정 | — | — |

### `ResearchFacilityRequirement`

도달 SO: `ResearchProjectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capability` | `ResearchFacilityCapabilityId` | [22](Assets/Scripts/Models/Research/Core/ResearchFacilityContracts.cs#L22) | authored-value | — | — |
| `requiredCount` | `int` | [23](Assets/Scripts/Models/Research/Core/ResearchFacilityContracts.cs#L23) | 수치/설정 | — | — |

### `ResearchGateItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `requiredResearchId` | `string` | [58](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L58) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |

### `ResearchPrerequisiteLink`

도달 SO: `ResearchProjectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `prerequisiteId` | `string` | [211](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L211) | dual-authority-relation | ContentReference(ContentReferenceKind.Research) | R07 |
| `prerequisite` | `ResearchProjectSO` | [212](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L212) | dual-authority-direct-reference | — | R07 |
| `kind` | `ResearchPrerequisiteKind` | [213](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L213) | enum | — | — |
| `reason` | `string` | [214](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L214) | display-text | — | — |

### `ResearchProjectSO`

도달 SO: `ResearchProjectSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `projectId` | `string` | [17](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L17) | self-key | ContentIdentity | — |
| `displayName` | `string` | [18](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L18) | display-text | — | — |
| `description` | `string` | [19](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L19) | display-text | — | — |
| `field` | `ResearchField` | [20](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L20) | enum | — | — |
| `requiredWork` | `float` | [21](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L21) | 수치/설정 | — | — |
| `maximumResearchers` | `int` | [22](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L22) | 수치/설정 | — | — |
| `prerequisites` | `List<ResearchProjectSO>` | [23](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L23) | direct-object-reference | — | — |
| `prerequisiteLinks` | `List<ResearchPrerequisiteLink>` | [24](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L24) | 중첩 데이터 | — | — |
| `blueprintRule` | `ResearchBlueprintRule` | [26](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L26) | enum | — | — |
| `blueprint` | `FacilityBlueprintSO` | [27](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L27) | direct-object-reference | — | — |
| `unlocks` | `BlueprintUnlockCollection` | [28](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L28) | 중첩 데이터 | — | — |
| `facilityRequirements` | `List<ResearchFacilityRequirement>` | [29](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L29) | nested-authored-value | — | — |
| `icon` | `Sprite` | [31](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L31) | direct-object-reference | — | — |
| `effects` | `List<GameplayEffectBinding>` | [32](Assets/Scripts/Models/Research/Core/ResearchProjectSO.cs#L32) | 중첩 데이터 | — | — |

### `ResearchUnlockBundleDefinitionSO`

도달 SO: `ResearchUnlockBundleDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `researchId` | `string` | [54](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L54) | relation-annotated-no-active-consumer | ContentReference(ContentReferenceKind.Research) | — |
| `role` | `ResearchUnlockBundleRole` | [55](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L55) | enum | — | — |
| `designIntent` | `string` | [56](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L56) | display-text | — | — |
| `rewardGroups` | `List<ResearchUnlockRewardGroup>` | [57](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L57) | 중첩 데이터 | — | — |
| `singletonReason` | `string` | [58](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L58) | display-text | — | — |

### `ResearchUnlockRewardGroup`

도달 SO: `ResearchUnlockBundleDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `ResearchRewardKind` | [21](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L21) | enum | — | — |
| `displayOrder` | `int` | [22](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L22) | 수치/설정 | — | — |
| `heading` | `string` | [23](Assets/Scripts/Models/Research/Core/ResearchUnlockBundleDefinitionSO.cs#L23) | display-text | — | — |

### `RoomEnvironmentSettingsSO`

도달 SO: `RoomEnvironmentSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `spaciousAreaMinimum` | `float` | [28](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L28) | 수치/설정 | — | — |
| `spaciousAreaMaximum` | `float` | [29](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L29) | 수치/설정 | — | — |
| `spaciousAreaWeight` | `float` | [30](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L30) | 수치/설정 | — | — |
| `spaciousFreeCellWeight` | `float` | [31](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L31) | 수치/설정 | — | — |
| `beautyBaseline` | `float` | [32](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L32) | 수치/설정 | — | — |
| `luxuryMultiplier` | `float` | [33](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L33) | 수치/설정 | — | — |
| `beautyDamagePenalty` | `float` | [34](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L34) | 수치/설정 | — | — |
| `beautyCrowdingThreshold` | `float` | [35](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L35) | 수치/설정 | — | — |
| `beautyCrowdingPenalty` | `float` | [36](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L36) | 수치/설정 | — | — |
| `cleanlinessBaseline` | `float` | [37](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L37) | 수치/설정 | — | — |
| `hygieneFacilityContribution` | `float` | [38](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L38) | authored-value | — | — |
| `hygieneContributionMaximum` | `float` | [39](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L39) | 수치/설정 | — | — |
| `cleanStreakContribution` | `float` | [40](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L40) | 수치/설정 | — | — |
| `cleanStreakContributionMaximum` | `float` | [41](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L41) | 수치/설정 | — | — |
| `cleanlinessDamagePenalty` | `float` | [42](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L42) | 수치/설정 | — | — |
| `cleanlinessCrowdingThreshold` | `float` | [43](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L43) | 수치/설정 | — | — |
| `cleanlinessCrowdingPenalty` | `float` | [44](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L44) | 수치/설정 | — | — |
| `impressivenessBeautyWeight` | `float` | [45](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L45) | 수치/설정 | — | — |
| `impressivenessSpaciousnessWeight` | `float` | [46](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L46) | 수치/설정 | — | — |
| `impressivenessCleanlinessWeight` | `float` | [47](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L47) | 수치/설정 | — | — |
| `impressivenessQualityWeight` | `float` | [48](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L48) | 수치/설정 | — | — |
| `moodDurationSeconds` | `float` | [51](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L51) | 수치/설정 | — | — |
| `awfulRoomMood` | `float` | [52](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L52) | 수치/설정 | — | — |
| `poorRoomMood` | `float` | [53](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L53) | 수치/설정 | — | — |
| `goodRoomMood` | `float` | [54](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L54) | 수치/설정 | — | — |
| `excellentRoomMood` | `float` | [55](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L55) | 수치/설정 | — | — |
| `filthyRoomMood` | `float` | [56](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L56) | 수치/설정 | — | — |
| `dirtyRoomMood` | `float` | [57](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L57) | 수치/설정 | — | — |
| `cleanRoomMood` | `float` | [58](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L58) | 수치/설정 | — | — |
| `roleColorOverrides` | `List<FacilityRoleColorOverride>` | [61](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L61) | nested-authored-value | — | — |
| `mixedColor` | `Color` | [63](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L63) | Unity 값 | — | — |
| `undefinedColor` | `Color` | [65](Assets/Scripts/Models/Rooms/Core/RoomEnvironmentSettingsSO.cs#L65) | Unity 값 | — | — |

### `SaleItem`

도달 SO: `SaleItem`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemName` | `string` | [11](Assets/Scripts/Models/Economy/Content/SaleItem.cs#L11) | authored-value | — | — |
| `itemDefinitionId` | `string` | [13](Assets/Scripts/Models/Economy/Content/SaleItem.cs#L13) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `category` | `StockCategory` | [14](Assets/Scripts/Models/Economy/Content/SaleItem.cs#L14) | enum | — | — |
| `cost` | `int` | [15](Assets/Scripts/Models/Economy/Content/SaleItem.cs#L15) | 수치/설정 | — | — |
| `itemSprite` | `Sprite` | [16](Assets/Scripts/Models/Economy/Content/SaleItem.cs#L16) | presentation-object | — | — |
| `buyevent` | `OnBuyItemSO[]` | [17](Assets/Scripts/Models/Economy/Content/SaleItem.cs#L17) | direct-object-reference | — | — |

### `SeasonalDriftCargoProfile`

도달 SO: `SeasonalWorldEventDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [63](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L63) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `exactQuantity` | `int` | [64](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L64) | 수치/설정 | — | — |

### `SeasonalExpeditionPhysicalRewardProfile`

도달 SO: `SeasonalWorldEventDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [149](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L149) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `displayLabel` | `string` | [150](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L150) | display-text | — | — |
| `exactQuantity` | `int` | [151](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L151) | 수치/설정 | — | — |

### `SeasonalFeedSelfHeatingFireProfile`

도달 SO: `SeasonalWorldEventDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `minimumPhysicalQuantity` | `int` | [231](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L231) | 수치/설정 | — | — |
| `ignitionIntensity` | `float` | [232](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L232) | 수치/설정 | — | — |

### `SeasonalManaLightningProfile`

도달 SO: `SeasonalWorldEventDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `eligibleBuildingDefinitionIds` | `List<int>` | [93](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L93) | relation-annotated | ContentReference(ContentReferenceKind.BuildingNumeric) | — |
| `minimumHeat` | `float` | [94](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L94) | 수치/설정 | — | — |
| `minimumFault` | `float` | [95](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L95) | 수치/설정 | — | — |
| `riskWindowSeconds` | `float` | [96](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L96) | 수치/설정 | — | — |
| `additionalIgnitionChancePerWindow` | `float` | [97](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L97) | 수치/설정 | — | — |
| `ignitionIntensity` | `float` | [98](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L98) | 수치/설정 | — | — |

### `SeasonalSpecialExpeditionProfile`

도달 SO: `SeasonalWorldEventDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `purpose` | `SeasonalSpecialExpeditionPurpose` | [157](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L157) | enum | — | — |
| `siteArchetypeId` | `string` | [159](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L159) | relation-annotated | ContentReference("OffenseSiteArchetypeSO", "siteTypeId") | — |
| `title` | `string` | [160](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L160) | display-text | — | — |
| `description` | `string` | [161](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L161) | display-text | — | — |
| `minimumDistanceSteps` | `int` | [162](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L162) | 수치/설정 | — | — |
| `maximumDistanceSteps` | `int` | [163](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L163) | 수치/설정 | — | — |
| `recommendedDanger` | `float` | [164](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L164) | 수치/설정 | — | — |
| `durationSeconds` | `float` | [165](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L165) | 수치/설정 | — | — |
| `requiredMembers` | `int` | [166](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L166) | 수치/설정 | — | — |
| `recommendedPower` | `float` | [167](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L167) | 수치/설정 | — | — |
| `campaignOrder` | `int` | [168](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L168) | 수치/설정 | — | — |
| `authoredEncounterId` | `string` | [170](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L170) | relation-annotated | ContentReference("OffenseEncounterSO", "encounterId") | — |
| `encounterPreviewText` | `string` | [171](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L171) | display-text | — | — |
| `encounterRewardPreviewText` | `string` | [172](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L172) | display-text | — | — |
| `factionId` | `string` | [173](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L173) | semantic-key | — | — |
| `physicalRewards` | `List<SeasonalExpeditionPhysicalRewardProfile>` | [174](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L174) | 중첩 데이터 | — | — |

### `SeasonalWildlifeArrivalProfile`

도달 SO: `SeasonalWorldEventDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `speciesId` | `string` | [16](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L16) | relation-annotated | ContentReference("WildlifeSpeciesSO", "SpeciesId") | — |
| `exactCount` | `int` | [17](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L17) | 수치/설정 | — | — |
| `requiredHabitatId` | `string` | [18](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L18) | semantic-key | — | — |
| `qualification` | `SeasonalWildlifeArrivalQualification` | [19](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L19) | enum | — | — |

### `SeasonalWorldEventDefinitionSO`

도달 SO: `SeasonalWorldEventDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `season` | `Season` | [264](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L264) | enum | — | — |
| `minimumDurationDays` | `int` | [265](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L265) | 수치/설정 | — | — |
| `maximumDurationDays` | `int` | [266](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L266) | 수치/설정 | — | — |
| `worldWaterRegenerationMultiplier` | `float` | [267](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L267) | 수치/설정 | — | — |
| `powerAvailableSupplyCapacityMultiplier` | `float` | [268](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L268) | 수치/설정 | — | — |
| `pipedWaterThroughputMultiplier` | `float` | [269](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L269) | 수치/설정 | — | — |
| `pipedWaterFreezeThresholdC` | `float` | [270](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L270) | 수치/설정 | — | — |
| `pipedWaterRecoveryThresholdC` | `float` | [271](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L271) | 수치/설정 | — | — |
| `cropPrimaryBatchLossPercent` | `int` | [272](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L272) | 수치/설정 | — | — |
| `requiredWeatherFrontId` | `string` | [274](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L274) | relation-annotated | ContentReference("WeatherFrontDefinitionSO", "stableId") | — |
| `wildlifeArrivalProfile` | `SeasonalWildlifeArrivalProfile` | [275](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L275) | nested-authored-value | — | — |
| `driftCargoProfile` | `SeasonalDriftCargoProfile` | [276](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L276) | nested-authored-value | — | — |
| `manaLightningProfile` | `SeasonalManaLightningProfile` | [277](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L277) | nested-authored-value | — | — |
| `specialExpeditionProfile` | `SeasonalSpecialExpeditionProfile` | [278](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L278) | nested-authored-value | — | — |
| `feedSelfHeatingFireProfile` | `SeasonalFeedSelfHeatingFireProfile` | [279](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L279) | nested-authored-value | — | — |
| `affectedDomainIds` | `List<string>` | [280](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L280) | semantic-key | — | — |
| `triggerRequirements` | `V20ContentRequirementSet` | [281](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L281) | 중첩 데이터 | — | — |
| `startEffects` | `List<V20ContentEffect>` | [282](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L282) | 중첩 데이터 | — | — |
| `dailyEffects` | `List<V20ContentEffect>` | [283](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L283) | 중첩 데이터 | — | — |
| `endEffects` | `List<V20ContentEffect>` | [284](Assets/Scripts/Content/SeasonalWorldEventDefinitionSO.cs#L284) | 중첩 데이터 | — | — |

### `ServiceIncidentDefinitionSO`

도달 SO: `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `ServiceIncidentKind` | [10](Assets/Scripts/Content/ServiceIncidentDefinitionSO.cs#L10) | enum | — | — |
| `riskTier` | `ExperienceEventRiskTier` | [11](Assets/Scripts/Content/ServiceIncidentDefinitionSO.cs#L11) | enum | — | — |
| `riskReason` | `string` | [12](Assets/Scripts/Content/ServiceIncidentDefinitionSO.cs#L12) | display-text | — | — |
| `triggerRequirements` | `V20ContentRequirementSet` | [13](Assets/Scripts/Content/ServiceIncidentDefinitionSO.cs#L13) | 중첩 데이터 | — | — |
| `responses` | `List<V20ChoiceDefinition>` | [14](Assets/Scripts/Content/ServiceIncidentDefinitionSO.cs#L14) | 중첩 데이터 | — | — |

### `ServiceModeProcessContract`

도달 SO: `ServiceProcessSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `mode` | `ServiceOperationMode` | [10](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L10) | enum | — | — |
| `activeStages` | `ServiceProcessStageMask` | [11](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L11) | authored-value | — | — |
| `receptionSeconds` | `float` | [13](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L13) | 수치/설정 | — | — |
| `waitingSeconds` | `float` | [14](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L14) | 수치/설정 | — | — |
| `serviceSeconds` | `float` | [15](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L15) | 수치/설정 | — | — |
| `paymentSeconds` | `float` | [16](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L16) | 수치/설정 | — | — |
| `cleanupSeconds` | `float` | [17](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L17) | 수치/설정 | — | — |
| `basePrice` | `int` | [18](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L18) | 수치/설정 | — | — |
| `satisfaction` | `float` | [19](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L19) | authored-value | — | — |
| `requiredFeatureTags` | `string[]` | [20](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L20) | semantic-key | — | — |

### `ServiceProcessSO`

도달 SO: `ServiceProcessSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `processId` | `string` | [30](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L30) | self-key | ContentIdentity | — |
| `serviceCategory` | `ServiceCategory` | [31](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L31) | enum | — | — |
| `ownerHubTag` | `string` | [32](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L32) | semantic-key | — | — |
| `modeContracts` | `ServiceModeProcessContract[]` | [33](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L33) | 중첩 데이터 | — | — |
| `workTypeId` | `string` | [37](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L37) | semantic-key | — | — |
| `cleanWater` | `float` | [38](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L38) | 수치/설정 | — | — |
| `wastewater` | `float` | [39](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L39) | 수치/설정 | — | — |
| `allowsManualWaterFallback` | `bool` | [40](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L40) | 수치/설정 | — | — |
| `paymentPolicy` | `ServicePaymentPolicy` | [43](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L43) | enum | — | — |
| `requiresCleanup` | `bool` | [45](Assets/Scripts/Models/Rooms/Core/ServiceProcessSO.cs#L45) | 수치/설정 | — | — |

### `SpeciesCultureDefinitionSO`

도달 SO: `SpeciesCultureDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `defaultSpeciesId` | `string` | [32](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L32) | SOrelation | ContentReference(ContentReferenceKind.Species) | — |
| `preferredItemIds` | `List<string>` | [33](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L33) | SOrelation | ContentReference(ContentReferenceKind.Item) | — |
| `forbiddenItemIds` | `List<string>` | [34](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L34) | SOrelation | ContentReference(ContentReferenceKind.Item) | — |
| `preferredFacilityIds` | `List<string>` | [35](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L35) | SOrelation | ContentReference(ContentReferenceKind.Building) | — |
| `environmentalPreferences` | `List<string>` | [36](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L36) | semantic | — | — |
| `roomPreference` | `CultureRoomPreferenceDefinition` | [37](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L37) | semantic | — | — |
| `etiquetteRules` | `List<string>` | [38](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L38) | semantic | — | — |
| `ceremonyIds` | `List<string>` | [39](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L39) | SOrelation | ContentReference(ContentReferenceKind.Practice) | — |
| `otherCultureAttitudes` | `List<V20WeightedId>` | [40](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L40) | 중첩 데이터 | — | — |
| `assimilationDays` | `int` | [41](Assets/Scripts/Content/SpeciesCultureDefinitionSO.cs#L41) | 수치/설정 | — | — |

### `SpeciesEnvironmentProfile`

도달 SO: `CharacterSpeciesDefinitionSO`, `CharacterSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `comfortMinimum` | `float` | [140](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L140) | 수치/설정 | — | — |
| `comfortMaximum` | `float` | [141](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L141) | 수치/설정 | — | — |
| `safeMinimum` | `float` | [142](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L142) | 수치/설정 | — | — |
| `safeMaximum` | `float` | [143](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L143) | 수치/설정 | — | — |
| `lethalMinimum` | `float` | [144](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L144) | 수치/설정 | — | — |
| `lethalMaximum` | `float` | [145](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L145) | 수치/설정 | — | — |
| `comfortableAirMinimum` | `float` | [146](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L146) | 수치/설정 | — | — |
| `lightAdaptationEnabled` | `bool` | [147](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L147) | 수치/설정 | — | — |
| `comfortableLightMinimum` | `float` | [148](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L148) | 수치/설정 | — | — |
| `comfortableLightMaximum` | `float` | [149](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L149) | 수치/설정 | — | — |
| `airborneExposureMultiplier` | `float` | [150](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L150) | 수치/설정 | — | — |
| `visualStrainMultiplier` | `float` | [151](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L151) | 수치/설정 | — | — |
| `preferredHumidity` | `float` | [152](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L152) | nonrelation | — | — |
| `drynessSensitivity` | `float` | [153](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L153) | 수치/설정 | — | — |

### `SpeciesIncidentDefinition`

도달 SO: `CharacterSpeciesDefinitionSO`, `CharacterSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `incidentId` | `string` | [202](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L202) | ownID | — | — |
| `displayName` | `string` | [203](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L203) | display | — | — |
| `description` | `string` | [204](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L204) | display | — | — |
| `mitigatingRoles` | `FacilityRole` | [205](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L205) | enum | — | — |
| `triggerTags` | `string[]` | [206](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L206) | semantic | — | — |

### `SpeciesLifeHistorySO`

도달 SO: `SpeciesLifeHistorySO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `definitionId` | `string` | [10](Assets/Scripts/Models/Species/Core/SpeciesLifeHistorySO.cs#L10) | ownID | ContentIdentity | — |
| `speciesTag` | `string` | [12](Assets/Scripts/Models/Species/Core/SpeciesLifeHistorySO.cs#L12) | SOrelation | ContentReference(ContentReferenceKind.Species) | — |
| `infantEndAgeYears` | `int` | [14](Assets/Scripts/Models/Species/Core/SpeciesLifeHistorySO.cs#L14) | 수치/설정 | — | — |
| `adolescentStartAgeYears` | `int` | [16](Assets/Scripts/Models/Species/Core/SpeciesLifeHistorySO.cs#L16) | nonrelation | — | — |
| `adultAgeYears` | `int` | [17](Assets/Scripts/Models/Species/Core/SpeciesLifeHistorySO.cs#L17) | nonrelation | — | — |
| `elderAgeYears` | `int` | [18](Assets/Scripts/Models/Species/Core/SpeciesLifeHistorySO.cs#L18) | 수치/설정 | — | — |
| `untreatedExpectedLifeYears` | `float` | [19](Assets/Scripts/Models/Species/Core/SpeciesLifeHistorySO.cs#L19) | 수치/설정 | — | — |
| `construct` | `bool` | [20](Assets/Scripts/Models/Species/Core/SpeciesLifeHistorySO.cs#L20) | 수치/설정 | — | — |

### `SpeciesNeedProfile`

도달 SO: `CharacterSpeciesDefinitionSO`, `CharacterSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `hungerRateMultiplier` | `float` | [71](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L71) | 수치/설정 | — | — |
| `thirstRateMultiplier` | `float` | [72](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L72) | 수치/설정 | — | — |
| `sleepRateMultiplier` | `float` | [73](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L73) | 수치/설정 | — | — |
| `hygieneRateMultiplier` | `float` | [74](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L74) | 수치/설정 | — | — |
| `socialNeedMultiplier` | `float` | [75](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L75) | 수치/설정 | — | — |
| `chargeRateMultiplier` | `float` | [76](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L76) | 수치/설정 | — | — |
| `integrityWearMultiplier` | `float` | [77](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L77) | 수치/설정 | — | — |
| `diet` | `MealDietClass` | [78](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L78) | enum | — | — |
| `metabolism` | `SpeciesMetabolismKind` | [79](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L79) | enum | — | — |
| `treatment` | `SpeciesTreatmentKind` | [80](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L80) | enum | — | — |

### `SpeciesPassiveDefinition`

도달 SO: `CharacterSpeciesDefinitionSO`, `CharacterSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `passiveId` | `string` | [215](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L215) | ownID | — | — |
| `displayName` | `string` | [216](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L216) | display | — | — |
| `description` | `string` | [217](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L217) | display | — | — |
| `mechanicTags` | `string[]` | [218](Assets/Scripts/Models/Species/Core/CharacterSpeciesDefinitionSO.cs#L218) | semantic | — | — |

### `StatChange`

도달 SO: `StatChange`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `needId` | `string` | [9](Assets/Scripts/Models/Buildings/Core/StatChange.cs#L9) | semantic-key | — | — |
| `value` | `int` | [10](Assets/Scripts/Models/Buildings/Core/StatChange.cs#L10) | 수치/설정 | — | — |

### `StockInfo`

도달 SO: `StockInfo`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `shopId` | `int` | [11](Assets/Scripts/Services/Buildings/SO/StockInfo.cs#L11) | self-key | ContentIdentity | — |
| `stocks` | `List<Tuple<SaleItem,int>>` | [12](Assets/Scripts/Services/Buildings/SO/StockInfo.cs#L12) | 수치/설정 | — | — |
| `multifly` | `float` | [13](Assets/Scripts/Services/Buildings/SO/StockInfo.cs#L13) | 수치/설정 | — | — |

### `SubstanceItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `substanceId` | `string` | [307](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L307) | semantic-key | — | — |
| `useClass` | `SubstanceUseClass` | [308](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L308) | enum | — | — |
| `addictionChance` | `float` | [309](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L309) | 수치/설정 | — | — |
| `overdoseChance` | `float` | [310](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L310) | 수치/설정 | — | — |
| `toleranceGain` | `float` | [311](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L311) | 수치/설정 | — | — |
| `withdrawalPerHour` | `float` | [312](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L312) | 수치/설정 | — | — |
| `moodEffect` | `float` | [313](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L313) | 수치/설정 | — | — |
| `workSpeedEffect` | `float` | [314](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L314) | 수치/설정 | — | — |
| `combatEffect` | `float` | [315](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L315) | 수치/설정 | — | — |
| `fatigueAccumulationReduction` | `float` | [316](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L316) | 수치/설정 | — | — |
| `researchSpeedEffect` | `float` | [317](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L317) | 수치/설정 | — | — |
| `suppressesPerceivedPain` | `bool` | [318](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L318) | 수치/설정 | — | — |
| `durationSeconds` | `float` | [319](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L319) | 수치/설정 | — | — |

### `SurgicalMaterialRequirement`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [358](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L358) | SOrelation | ContentReference(ContentReferenceKind.Item) | — |
| `quantity` | `int` | [359](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L359) | 수치/설정 | — | — |
| `optional` | `bool` | [360](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L360) | 수치/설정 | — | — |

### `SurgicalProcedureEffect`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `effectId` | `string` | [377](Assets/Scripts/Models/Medical/Core/SurgeryModels.cs#L377) | ownID | — | — |

### `SurgicalProcedureSO`

도달 SO: `SurgicalProcedureSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `procedureId` | `string` | [15](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L15) | ownID | ContentIdentity | — |
| `displayName` | `string` | [16](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L16) | display | — | — |
| `description` | `string` | [17](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L17) | display | — | — |
| `kind` | `SurgicalProcedureKind` | [18](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L18) | enum | — | — |
| `family` | `MedicalProcedureFamily` | [19](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L19) | enum | — | — |
| `urgency` | `MedicalProcedureUrgency` | [21](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L21) | enum | — | — |
| `allowedAnatomyFamilies` | `List<string>` | [23](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L23) | semantic | — | — |
| `allowedSpeciesIds` | `List<string>` | [25](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L25) | SOrelation | ContentReference(ContentReferenceKind.Species, IgnoreCase = true) | R09 |
| `operatorRequirement` | `ProcedureOperatorRequirement` | [26](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L26) | 중첩 데이터 | — | — |
| `targetNodeId` | `string` | [28](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L28) | SOrelation | ContentReference(ContentReferenceKind.AnatomyNode) | R09 |
| `requiredResearchId` | `string` | [30](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L30) | SOrelation | ContentReference(ContentReferenceKind.Research) | — |
| `requiredFacilityTags` | `SurgeryFacilityTag` | [31](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L31) | nonrelation | — | — |
| `primaryFacilityDefinitionId` | `int` | [33](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L33) | SOrelation | ContentReference(ContentReferenceKind.BuildingNumeric, emptyNumericId: 0) | — |
| `requiredWork` | `float` | [34](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L34) | 수치/설정 | — | — |
| `difficultyPenalty` | `float` | [35](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L35) | 수치/설정 | — | — |
| `baseInfectionRisk` | `float` | [36](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L36) | 수치/설정 | — | — |
| `baseBleedingRisk` | `float` | [37](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L37) | 수치/설정 | — | — |
| `requiresAnesthesia` | `bool` | [38](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L38) | 수치/설정 | — | — |
| `requiresRestraintForUnwilling` | `bool` | [39](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L39) | 수치/설정 | — | — |
| `allowsLivingSubject` | `bool` | [40](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L40) | 수치/설정 | — | — |
| `allowsCorpseSubject` | `bool` | [41](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L41) | 수치/설정 | — | — |
| `allowsWildlife` | `bool` | [42](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L42) | 수치/설정 | — | — |
| `materials` | `List<SurgicalMaterialRequirement>` | [43](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L43) | 중첩 데이터 | — | — |
| `effects` | `List<SurgicalProcedureEffect>` | [44](Assets/Scripts/Models/Medical/Core/SurgicalProcedureSO.cs#L44) | 중첩 데이터 | — | — |

### `SurvivalBalanceSettingsSO`

도달 SO: `SurvivalBalanceSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `dayLengthSeconds` | `float` | [107](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L107) | 수치/설정 | — | — |
| `needs` | `List<CharacterNeedBalanceEntry>` | [109](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L109) | 중첩 데이터 | — | — |
| `pressures` | `List<SurvivalPressureBalanceProfile>` | [111](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L111) | 중첩 데이터 | — | — |

### `SurvivalPressureBalanceProfile`

도달 SO: `SurvivalBalanceSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `pressure` | `DungeonSurvivalPressure` | [63](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L63) | enum | — | — |
| `depletionMultiplier` | `float` | [64](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L64) | 수치/설정 | — | — |
| `recoveryMultiplier` | `float` | [65](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L65) | 수치/설정 | — | — |
| `personalContinuousWaterMultiplier` | `float` | [66](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L66) | 수치/설정 | — | — |
| `deprivationBurdenGainMultiplier` | `float` | [67](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L67) | 수치/설정 | — | — |
| `deprivationBurdenRecoveryMultiplier` | `float` | [68](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L68) | 수치/설정 | — | — |
| `forcedBreakdownDelaySeconds` | `float` | [69](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L69) | 수치/설정 | — | — |
| `highBurdenDamageIntervalSeconds` | `float` | [70](Assets/Scripts/Models/Survival/Core/SurvivalBalanceSettingsSO.cs#L70) | 수치/설정 | — | — |

### `TextileMaterialDefinitionSO`

도달 SO: `TextileMaterialDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `materialId` | `string` | [12](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L12) | self-key | ContentIdentity | — |
| `physicalItemId` | `string` | [14](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L14) | relation-annotated | ContentReference(ContentReferenceKind.Item) | — |
| `displayName` | `string` | [15](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L15) | authored-value | — | — |
| `description` | `string` | [16](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L16) | authored-value | — | — |
| `tags` | `TextileMaterialTag` | [17](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L17) | semantic-key | — | — |
| `warmth` | `float` | [18](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L18) | 수치/설정 | — | — |
| `heatResistance` | `float` | [19](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L19) | 수치/설정 | — | — |
| `waterResistance` | `float` | [20](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L20) | 수치/설정 | — | — |
| `airborneResistance` | `float` | [21](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L21) | 수치/설정 | — | — |
| `sterility` | `float` | [22](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L22) | 수치/설정 | — | — |
| `durability` | `float` | [23](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L23) | authored-value | — | — |
| `weightMultiplier` | `float` | [24](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L24) | 수치/설정 | — | — |
| `dryingRate` | `float` | [25](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L25) | 수치/설정 | — | — |
| `requiredResearchId` | `string` | [27](Assets/Scripts/Models/Economy/Content/TextileMaterialDefinitionSO.cs#L27) | relation-annotated | ContentReference(ContentReferenceKind.Research) | — |

### `ThermalProtectionProfile`

도달 SO: `CharacterTraitSO`, `EnvironmentalWorkwearSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `comfortMinimumOffset` | `float` | [9](Assets/Scripts/Models/Environment/Core/ThermalProtectionProfile.cs#L9) | 수치/설정 | — | — |
| `comfortMaximumOffset` | `float` | [10](Assets/Scripts/Models/Environment/Core/ThermalProtectionProfile.cs#L10) | 수치/설정 | — | — |
| `safeMinimumOffset` | `float` | [11](Assets/Scripts/Models/Environment/Core/ThermalProtectionProfile.cs#L11) | 수치/설정 | — | — |
| `safeMaximumOffset` | `float` | [12](Assets/Scripts/Models/Environment/Core/ThermalProtectionProfile.cs#L12) | 수치/설정 | — | — |
| `coldExposureMultiplier` | `float` | [13](Assets/Scripts/Models/Environment/Core/ThermalProtectionProfile.cs#L13) | 수치/설정 | — | — |
| `heatExposureMultiplier` | `float` | [14](Assets/Scripts/Models/Environment/Core/ThermalProtectionProfile.cs#L14) | 수치/설정 | — | — |

### `TmpKoreanFontSettingsSO`

도달 SO: `TmpKoreanFontSettingsSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `font` | `TMP_FontAsset` | [10](Assets/Scripts/Content/TmpKoreanFontSettingsSO.cs#L10) | direct-object-reference | — | — |

### `V20AuthoredContentSO`

도달 SO: `CharacterAmbitionDefinitionSO`, `CharacterBackgroundDefinitionSO`, `CulturalPracticeDefinitionSO`, `EndingDefinitionSO`, `FactionArcDefinitionSO`, `FactionChapterDefinitionSO`, `FactionContractDefinitionSO`, `GuestRequestDefinitionSO`, `LifeEventDefinitionSO`, `ProficiencyDefinitionSO`, `SeasonalWorldEventDefinitionSO`, `ServiceIncidentDefinitionSO`, `SpeciesCultureDefinitionSO`, `V20AuthoredContentSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stableId` | `string` | [8](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L8) | ownID | ContentIdentity | — |
| `displayName` | `string` | [9](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L9) | display | — | — |
| `description` | `string` | [10](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L10) | display | — | — |
| `authoringRevision` | `int` | [11](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L11) | 수치/설정 | — | — |
| `sourceNote` | `string` | [12](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L12) | display | — | — |

### `V20CharacterRequirement`

도달 SO: `CharacterAmbitionDefinitionSO`, `CulturalPracticeDefinitionSO`, `EndingDefinitionSO`, `FactionChapterDefinitionSO`, `FactionContractDefinitionSO`, `GuestRequestDefinitionSO`, `LifeEventDefinitionSO`, `SeasonalWorldEventDefinitionSO`, `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `minimumLifeStage` | `CharacterLifeStage` | [72](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L72) | nonrelation | — | — |
| `maximumLifeStage` | `CharacterLifeStage` | [73](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L73) | nonrelation | — | — |
| `requiredTraitId` | `string` | [75](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L75) | SOrelation | ContentReference("CharacterTraitSO", "DefinitionId") | — |
| `excludedTraitId` | `string` | [77](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L77) | SOrelation | ContentReference("CharacterTraitSO", "DefinitionId") | — |
| `minimumHealth` | `int` | [78](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L78) | 수치/설정 | — | — |

### `V20ChoiceDefinition`

도달 SO: `FactionChapterDefinitionSO`, `LifeEventDefinitionSO`, `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `choiceId` | `string` | [202](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L202) | self-key | — | — |
| `title` | `string` | [203](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L203) | display-text | — | — |
| `outcomeText` | `string` | [204](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L204) | display-text | — | — |
| `requirements` | `V20ContentRequirementSet` | [205](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L205) | 중첩 데이터 | — | — |
| `effects` | `List<V20ContentEffect>` | [206](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L206) | 중첩 데이터 | — | — |

### `V20ContentEffect`

도달 SO: `CharacterAmbitionDefinitionSO`, `CharacterBackgroundDefinitionSO`, `CulturalPracticeDefinitionSO`, `EndingDefinitionSO`, `FactionChapterDefinitionSO`, `FactionContractDefinitionSO`, `GuestRequestDefinitionSO`, `LifeEventDefinitionSO`, `SeasonalWorldEventDefinitionSO`, `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `V20ContentEffectKind` | [185](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L185) | enum | — | — |
| `targetId` | `string` | [186](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L186) | SOrelation | ContentReference(ContentReferenceKind.EffectTarget) | — |
| `amount` | `float` | [187](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L187) | 수치/설정 | — | — |
| `durationDays` | `int` | [188](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L188) | 수치/설정 | — | — |

### `V20ContentRequirementSet`

도달 SO: `CharacterAmbitionDefinitionSO`, `CulturalPracticeDefinitionSO`, `EndingDefinitionSO`, `FactionChapterDefinitionSO`, `FactionContractDefinitionSO`, `GuestRequestDefinitionSO`, `LifeEventDefinitionSO`, `SeasonalWorldEventDefinitionSO`, `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `items` | `List<V20ItemAmountRequirement>` | [119](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L119) | semantic | — | — |
| `facilities` | `List<V20FacilityRequirement>` | [120](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L120) | 중첩 데이터 | — | — |
| `research` | `List<V20ResearchRequirement>` | [121](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L121) | 중첩 데이터 | — | — |
| `characters` | `List<V20CharacterRequirement>` | [122](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L122) | 중첩 데이터 | — | — |
| `factions` | `List<V20FactionRequirement>` | [123](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L123) | semantic | — | — |
| `worldMetrics` | `List<V20WorldMetricRequirement>` | [124](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L124) | 중첩 데이터 | — | — |
| `requiredFlags` | `List<string>` | [125](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L125) | semantic | — | — |
| `excludedFlags` | `List<string>` | [126](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L126) | semantic | — | — |

### `V20FacilityRequirement`

도달 SO: `CharacterAmbitionDefinitionSO`, `CulturalPracticeDefinitionSO`, `EndingDefinitionSO`, `FactionChapterDefinitionSO`, `FactionContractDefinitionSO`, `GuestRequestDefinitionSO`, `LifeEventDefinitionSO`, `SeasonalWorldEventDefinitionSO`, `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `buildingDefinitionId` | `string` | [57](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L57) | SOrelation | ContentReference(ContentReferenceKind.Building) | — |
| `capabilityId` | `string` | [58](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L58) | semantic | — | — |
| `minimumCount` | `int` | [59](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L59) | 수치/설정 | — | — |
| `mustBeOperational` | `bool` | [60](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L60) | 수치/설정 | — | — |

### `V20FactionRequirement`

도달 SO: `CharacterAmbitionDefinitionSO`, `CulturalPracticeDefinitionSO`, `EndingDefinitionSO`, `FactionChapterDefinitionSO`, `FactionContractDefinitionSO`, `GuestRequestDefinitionSO`, `LifeEventDefinitionSO`, `SeasonalWorldEventDefinitionSO`, `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `factionId` | `string` | [84](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L84) | SOrelation | ContentReference(ContentReferenceKind.Faction) | — |
| `minimumRapport` | `int` | [85](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L85) | 수치/설정 | — | — |
| `maximumGrievance` | `int` | [86](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L86) | 수치/설정 | — | — |
| `minimumObligationTokens` | `int` | [87](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L87) | 수치/설정 | — | — |

### `V20ItemAmountRequirement`

도달 SO: `CharacterAmbitionDefinitionSO`, `CulturalPracticeDefinitionSO`, `EndingDefinitionSO`, `FactionChapterDefinitionSO`, `FactionContractDefinitionSO`, `GuestRequestDefinitionSO`, `LifeEventDefinitionSO`, `SeasonalWorldEventDefinitionSO`, `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemDefinitionId` | `string` | [49](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L49) | SOrelation | ContentReference(ContentReferenceKind.Item) | — |
| `amount` | `int` | [50](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L50) | 수치/설정 | — | — |
| `consume` | `bool` | [51](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L51) | 수치/설정 | — | — |

### `V20ResearchRequirement`

도달 SO: `CharacterAmbitionDefinitionSO`, `CulturalPracticeDefinitionSO`, `EndingDefinitionSO`, `FactionChapterDefinitionSO`, `FactionContractDefinitionSO`, `GuestRequestDefinitionSO`, `LifeEventDefinitionSO`, `SeasonalWorldEventDefinitionSO`, `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `researchNumericId` | `int` | [66](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L66) | SOrelation | ContentReference(ContentReferenceKind.ResearchNumeric) | — |

### `V20SkillBonus`

도달 SO: `CharacterBackgroundDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `skillId` | `string` | [228](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L228) | SOrelation | ContentReference(ContentReferenceKind.Proficiency) | — |
| `experience` | `int` | [229](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L229) | 수치/설정 | — | — |

### `V20WeightedId`

도달 SO: `CharacterAmbitionDefinitionSO`, `CharacterBackgroundDefinitionSO`, `SpeciesCultureDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `id` | `string` | [221](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L221) | SOrelation | ContentReference(ContentReferenceKind.WeightedTarget) | — |
| `weight` | `float` | [222](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L222) | 수치/설정 | — | — |

### `V20WorldMetricRequirement`

도달 SO: `CharacterAmbitionDefinitionSO`, `CulturalPracticeDefinitionSO`, `EndingDefinitionSO`, `FactionChapterDefinitionSO`, `FactionContractDefinitionSO`, `GuestRequestDefinitionSO`, `LifeEventDefinitionSO`, `SeasonalWorldEventDefinitionSO`, `ServiceIncidentDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `kind` | `V20WorldMetricKind` | [112](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L112) | enum | — | — |
| `minimumValue` | `float` | [113](Assets/Scripts/Content/V20AuthoredContentContracts.cs#L113) | 수치/설정 | — | — |

### `VaccineItemFeature`

도달 SO: `GenericItemDefinitionSO`, `ItemDefinitionSO`, `ResourceItemDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `diseaseId` | `string` | [181](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L181) | relation-annotated | ContentReference(ContentReferenceKind.Disease) | — |
| `doses` | `int` | [182](Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs#L182) | 수치/설정 | — | — |

### `WasteFeedRuleRecord`

도달 SO: `WasteProcessingRulesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `diet` | `WildlifeDietType` | [28](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L28) | enum | — | — |
| `origin` | `WasteOriginKind` | [29](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L29) | enum | — | — |
| `nutrition` | `float` | [30](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L30) | 수치/설정 | — | — |
| `diseaseChance` | `float` | [31](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L31) | 수치/설정 | — | — |

### `WasteOriginRuleRecord`

도달 SO: `WasteProcessingRulesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `origin` | `WasteOriginKind` | [10](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L10) | enum | — | — |
| `defaultDisposition` | `WasteDispositionKind` | [11](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L11) | enum | — | — |
| `defaultMaximumFeedContamination` | `float` | [12](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L12) | 수치/설정 | — | — |
| `supportedDispositions` | `List<WasteDispositionKind>` | [13](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L13) | enum | — | — |

### `WasteProcessingRulesSO`

도달 SO: `WasteProcessingRulesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `tickIntervalSeconds` | `float` | [50](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L50) | 수치/설정 | — | — |
| `toxicThreshold` | `float` | [51](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L51) | 수치/설정 | — | — |
| `origins` | `List<WasteOriginRuleRecord>` | [52](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L52) | 중첩 데이터 | — | — |
| `recipes` | `List<WasteRecipeBindingRecord>` | [53](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L53) | nested-authored-value | — | — |
| `feedRules` | `List<WasteFeedRuleRecord>` | [54](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L54) | 중첩 데이터 | — | — |
| `legacyItems` | `List<LegacyWasteItemRuleRecord>` | [55](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L55) | nested-authored-value | — | — |

### `WasteRecipeBindingRecord`

도달 SO: `WasteProcessingRulesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `origin` | `WasteOriginKind` | [19](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L19) | enum | — | — |
| `disposition` | `WasteDispositionKind` | [20](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L20) | enum | — | — |
| `recipeId` | `string` | [22](Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs#L22) | relation-annotated | ContentReference(ContentReferenceKind.Recipe) | — |

### `WeatherFrontDefinitionSO`

도달 SO: `WeatherFrontDefinitionSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `stableId` | `string` | [9](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L9) | self-key | ContentIdentity | — |
| `displayName` | `string` | [10](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L10) | display-text | — | — |
| `kind` | `WeatherFrontKind` | [11](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L11) | enum | — | — |
| `minimumDurationDays` | `int` | [12](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L12) | 수치/설정 | — | — |
| `maximumDurationDays` | `int` | [13](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L13) | 수치/설정 | — | — |
| `temperatureModifierC` | `float` | [14](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L14) | 수치/설정 | — | — |
| `expeditionTravelMultiplier` | `float` | [15](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L15) | 수치/설정 | — | — |
| `springWeight` | `float` | [16](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L16) | 수치/설정 | — | — |
| `summerWeight` | `float` | [17](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L17) | 수치/설정 | — | — |
| `autumnWeight` | `float` | [18](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L18) | 수치/설정 | — | — |
| `winterWeight` | `float` | [19](Assets/Scripts/Content/WeatherFrontDefinitionSO.cs#L19) | 수치/설정 | — | — |

### `WildlifeButcherYield`

도달 SO: `WildlifeSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [57](Assets/Scripts/Models/Wildlife/Core/WildlifePrimitives.cs#L57) | SOrelation | ContentReference(ContentReferenceKind.Item) | — |
| `amount` | `int` | [58](Assets/Scripts/Models/Wildlife/Core/WildlifePrimitives.cs#L58) | 수치/설정 | — | — |

### `WildlifeCompanionRoleProfile`

도달 SO: `WildlifeSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `closeRadiusCells` | `int` | [12](Assets/Scripts/Models/Wildlife/Core/WildlifeCompanionRoleProfile.cs#L12) | 수치/설정 | — | — |
| `resumeFollowDistanceCells` | `int` | [13](Assets/Scripts/Models/Wildlife/Core/WildlifeCompanionRoleProfile.cs#L13) | 수치/설정 | — | — |
| `ownerTargetLeashCells` | `int` | [14](Assets/Scripts/Models/Wildlife/Core/WildlifeCompanionRoleProfile.cs#L14) | nonrelation | — | — |
| `decisionIntervalSeconds` | `float` | [15](Assets/Scripts/Models/Wildlife/Core/WildlifeCompanionRoleProfile.cs#L15) | 수치/설정 | — | — |
| `baseAttackDamage` | `float` | [16](Assets/Scripts/Models/Wildlife/Core/WildlifeCompanionRoleProfile.cs#L16) | 수치/설정 | — | — |
| `meleeRangeCells` | `int` | [17](Assets/Scripts/Models/Wildlife/Core/WildlifeCompanionRoleProfile.cs#L17) | 수치/설정 | — | — |
| `attackCooldownSeconds` | `float` | [18](Assets/Scripts/Models/Wildlife/Core/WildlifeCompanionRoleProfile.cs#L18) | 수치/설정 | — | — |
| `maximumCompanionsPerOwner` | `int` | [19](Assets/Scripts/Models/Wildlife/Core/WildlifeCompanionRoleProfile.cs#L19) | 수치/설정 | — | — |

### `WildlifeHabitatDecorationPaletteSO`

도달 SO: `WildlifeHabitatDecorationPaletteSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `flowerSprites` | `List<Sprite>` | [15](Assets/Scripts/Models/Wildlife/Core/WildlifeHabitatDecorationPaletteSO.cs#L15) | directObject | — | — |
| `treeSprites` | `List<Sprite>` | [18](Assets/Scripts/Models/Wildlife/Core/WildlifeHabitatDecorationPaletteSO.cs#L18) | directObject | — | — |
| `rockSprites` | `List<Sprite>` | [19](Assets/Scripts/Models/Wildlife/Core/WildlifeHabitatDecorationPaletteSO.cs#L19) | directObject | — | — |
| `flowersPerGrassPatch` | `int` | [22](Assets/Scripts/Models/Wildlife/Core/WildlifeHabitatDecorationPaletteSO.cs#L22) | 수치/설정 | — | — |
| `flowersPerBrushPatch` | `int` | [23](Assets/Scripts/Models/Wildlife/Core/WildlifeHabitatDecorationPaletteSO.cs#L23) | 수치/설정 | — | — |
| `scatteredTreeSpacing` | `int` | [24](Assets/Scripts/Models/Wildlife/Core/WildlifeHabitatDecorationPaletteSO.cs#L24) | 수치/설정 | — | — |
| `scatteredRockSpacing` | `int` | [25](Assets/Scripts/Models/Wildlife/Core/WildlifeHabitatDecorationPaletteSO.cs#L25) | 수치/설정 | — | — |

### `WildlifeHaulRoleProfile`

도달 SO: `WildlifeSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `maxCargoMassGrams` | `long` | [12](Assets/Scripts/Models/Wildlife/Core/WildlifeHaulRoleProfile.cs#L12) | 수치/설정 | — | — |
| `loadedMoveSpeedMultiplier` | `float` | [13](Assets/Scripts/Models/Wildlife/Core/WildlifeHaulRoleProfile.cs#L13) | 수치/설정 | — | — |
| `decisionIntervalSeconds` | `float` | [14](Assets/Scripts/Models/Wildlife/Core/WildlifeHaulRoleProfile.cs#L14) | 수치/설정 | — | — |

### `WildlifeHusbandryProductDefinition`

도달 SO: `WildlifeSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `itemId` | `string` | [12](Assets/Scripts/Models/Wildlife/Core/WildlifeHusbandryProfile.cs#L12) | SOrelation | ContentReference(ContentReferenceKind.Item) | — |
| `amount` | `int` | [13](Assets/Scripts/Models/Wildlife/Core/WildlifeHusbandryProfile.cs#L13) | 수치/설정 | — | — |
| `intervalDays` | `float` | [14](Assets/Scripts/Models/Wildlife/Core/WildlifeHusbandryProfile.cs#L14) | 수치/설정 | — | — |
| `femaleOnly` | `bool` | [15](Assets/Scripts/Models/Wildlife/Core/WildlifeHusbandryProfile.cs#L15) | 수치/설정 | — | — |
| `adultOnly` | `bool` | [16](Assets/Scripts/Models/Wildlife/Core/WildlifeHusbandryProfile.cs#L16) | 수치/설정 | — | — |

### `WildlifeMigrationProfile`

도달 SO: `WildlifeSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `capability` | `WildlifeMigrationCapability` | [25](Assets/Scripts/Models/Wildlife/Core/WildlifeMigrationProfile.cs#L25) | nonrelation | — | — |
| `detectionRadiusCells` | `int` | [27](Assets/Scripts/Models/Wildlife/Core/WildlifeMigrationProfile.cs#L27) | 수치/설정 | — | — |
| `signalScore` | `float` | [29](Assets/Scripts/Models/Wildlife/Core/WildlifeMigrationProfile.cs#L29) | 수치/설정 | — | — |
| `minimumWaterRemainingRatio` | `float` | [31](Assets/Scripts/Models/Wildlife/Core/WildlifeMigrationProfile.cs#L31) | 수치/설정 | — | — |
| `minimumCarcassQuantity` | `int` | [33](Assets/Scripts/Models/Wildlife/Core/WildlifeMigrationProfile.cs#L33) | 수치/설정 | — | — |
| `minimumTemperatureC` | `float` | [35](Assets/Scripts/Models/Wildlife/Core/WildlifeMigrationProfile.cs#L35) | 수치/설정 | — | — |
| `maximumTemperatureC` | `float` | [37](Assets/Scripts/Models/Wildlife/Core/WildlifeMigrationProfile.cs#L37) | 수치/설정 | — | — |

### `WildlifeSpeciesSO`

도달 SO: `WildlifeSpeciesSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `speciesId` | `string` | [10](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L10) | ownID | ContentIdentity | — |
| `displayName` | `string` | [11](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L11) | display | — | — |
| `description` | `string` | [12](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L12) | display | — | — |
| `sprite` | `Sprite` | [13](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L13) | directObject | — | — |
| `maxHealth` | `int` | [14](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L14) | 수치/설정 | — | — |
| `moveSpeed` | `float` | [15](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L15) | 수치/설정 | — | — |
| `fearSensitivity` | `float` | [16](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L16) | 수치/설정 | — | — |
| `aggression` | `float` | [17](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L17) | 수치/설정 | — | — |
| `retaliationDamage` | `int` | [18](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L18) | 수치/설정 | — | — |
| `spawnWeight` | `float` | [19](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L19) | 수치/설정 | — | — |
| `herdSize` | `int` | [20](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L20) | 수치/설정 | — | — |
| `canEnterDungeon` | `bool` | [21](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L21) | 수치/설정 | — | — |
| `carcassWeight` | `float` | [22](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L22) | 수치/설정 | — | — |
| `diet` | `WildlifeDietType` | [24](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L24) | enum | — | — |
| `preferredHabitats` | `List<WildlifeHabitatType>` | [25](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L25) | enum | — | — |
| `territoryRadius` | `float` | [26](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L26) | 수치/설정 | — | — |
| `dailyFoodNeed` | `float` | [27](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L27) | 수치/설정 | — | — |
| `dailyWaterNeed` | `float` | [28](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L28) | 수치/설정 | — | — |
| `restPreference` | `float` | [29](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L29) | nonrelation | — | — |
| `predationDrive` | `float` | [30](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L30) | 수치/설정 | — | — |
| `fleePreference` | `float` | [31](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L31) | nonrelation | — | — |
| `butcherYields` | `List<WildlifeButcherYield>` | [32](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L32) | 중첩 데이터 | — | — |
| `domesticable` | `bool` | [34](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L34) | 수치/설정 | — | — |
| `tamingDifficulty` | `float` | [35](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L35) | 수치/설정 | — | — |
| `adultAgeDays` | `float` | [36](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L36) | nonrelation | — | — |
| `maximumAgeDays` | `float` | [37](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L37) | 수치/설정 | — | — |
| `gestationDays` | `float` | [38](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L38) | 수치/설정 | — | — |
| `laysEggs` | `bool` | [39](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L39) | 수치/설정 | — | — |
| `bodySize` | `float` | [40](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L40) | 수치/설정 | — | — |
| `manureIntervalDays` | `float` | [41](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L41) | 수치/설정 | — | — |
| `productComfortMinimumTemperatureC` | `float` | [43](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L43) | 수치/설정 | — | — |
| `productComfortMaximumTemperatureC` | `float` | [46](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L46) | 수치/설정 | — | — |
| `husbandryProducts` | `List<WildlifeHusbandryProductDefinition>` | [48](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L48) | 중첩 데이터 | — | — |
| `authoringRevision` | `int` | [51](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L51) | 수치/설정 | — | — |
| `preySpeciesIds` | `List<string>` | [53](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L53) | SOrelation | ContentReference("WildlifeSpeciesSO", "SpeciesId") | R19 |
| `predatorSpeciesIds` | `List<string>` | [55](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L55) | SOrelation | ContentReference("WildlifeSpeciesSO", "SpeciesId") | R19 |
| `nestTag` | `string` | [56](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L56) | semantic | — | — |
| `breedingSeason` | `Season` | [57](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L57) | enum | — | — |
| `migrationPatternId` | `string` | [58](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L58) | semantic | — | — |
| `migrationProfile` | `WildlifeMigrationProfile` | [59](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L59) | semantic | — | — |
| `hasCompanionRoleProfile` | `bool` | [60](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L60) | nonrelation | — | — |
| `companionRoleProfile` | `WildlifeCompanionRoleProfile` | [61](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L61) | semantic | — | — |
| `hasHaulRoleProfile` | `bool` | [62](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L62) | nonrelation | — | — |
| `haulRoleProfile` | `WildlifeHaulRoleProfile` | [63](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L63) | semantic | — | — |
| `diseaseVectorIds` | `List<string>` | [65](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L65) | SOrelation | ContentReference(ContentReferenceKind.Disease) | — |
| `activeSeasons` | `List<Season>` | [66](Assets/Scripts/Models/Wildlife/Core/WildlifeSpeciesSO.cs#L66) | enum | — | — |

### `WorkPriorityEntry`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `workTypeId` | `string` | [30](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L30) | semantic | — | — |
| `priority` | `WorkPriorityLevel` | [31](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L31) | enum | — | — |

### `WorkPriorityProfile`

도달 SO: `CharacterSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `schemaVersion` | `int` | [66](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L66) | 수치/설정 | — | — |
| `priorities` | `List<WorkPriorityEntry>` | [67](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L67) | 중첩 데이터 | — | — |
| `legacyOperate` | `WorkPriorityLevel` | [71](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L71) | enum | — | — |
| `legacyRestock` | `WorkPriorityLevel` | [73](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L73) | enum | — | — |
| `legacyRepair` | `WorkPriorityLevel` | [75](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L75) | enum | — | — |
| `legacyClean` | `WorkPriorityLevel` | [77](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L77) | enum | — | — |
| `legacyResearch` | `WorkPriorityLevel` | [79](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L79) | enum | — | — |
| `legacyGuard` | `WorkPriorityLevel` | [81](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L81) | enum | — | — |
| `legacyRescue` | `WorkPriorityLevel` | [83](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L83) | enum | — | — |
| `legacyRest` | `WorkPriorityLevel` | [85](Assets/Scripts/Services/Character/Work/WorkPriorityProfile.cs#L85) | enum | — | — |

### `WorkerSelectionPolicySaveData`

도달 SO: `BuildingSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `mode` | `WorkerSelectionMode` | [37](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L37) | enum | — | — |
| `matchMode` | `WorkerRequirementMatchMode` | [38](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L38) | enum | — | — |
| `sortMode` | `WorkerCandidateSortMode` | [39](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L39) | enum | — | — |
| `specificCharacterIds` | `List<string>` | [40](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L40) | runtime-snapshot-id | — | — |
| `excludedCharacterIds` | `List<string>` | [41](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L41) | runtime-snapshot-id | — | — |
| `minimumSkillId` | `string` | [42](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L42) | confirmed-unvalidated-relation | — | R13 |
| `minimumSkillExperience` | `int` | [43](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L43) | 수치/설정 | — | — |
| `minimumCareerRank` | `int` | [44](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L44) | 수치/설정 | — | — |
| `requiredTraitIds` | `List<string>` | [45](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L45) | confirmed-mixed-relation-unvalidated | — | R13 |
| `excludedTraitIds` | `List<string>` | [46](Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs#L46) | confirmed-mixed-relation-unvalidated | — | R13 |

### `WorldInteractionPresentationCatalogSO`

도달 SO: `WorldInteractionPresentationCatalogSO`.

| 필드 | 타입 | 원본 | 분류 | 관계/자체 ID 표식 | 보완 |
|---|---|---|---|---|---|
| `walkFrameSeconds` | `float` | [102](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L102) | 수치/설정 | — | — |
| `maximumImpactRotation` | `float` | [103](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L103) | 수치/설정 | — | — |
| `impactDuration` | `float` | [104](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L104) | 수치/설정 | — | — |
| `worldWaterTile` | `Tile` | [106](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L106) | direct-object-reference | — | — |
| `worldFilthTile` | `Tile` | [107](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L107) | direct-object-reference | — | — |
| `propProfiles` | `List<CharacterPropAttachmentProfile>` | [108](Assets/Scripts/Views/UI/Core/WorldInteractionPresentationCatalogSO.cs#L108) | nested-authored-value | — | — |

## 게임 SO 에셋 전체 목록 — 3,538개

폴더 한 곳으로 제한하지 않고 게시 GUID 전체를 현재 파일과 대조했다. 각 행은 해당 `.asset` 원본이다. 위 필드 타입에 자산이 없으면 추상/미사용 여부를 혼동하지 않는다. 아래 BuildingSO는400개이며, 그중349개가 빈 contentDefinitionId를 숫자 ID로 해석하는 기존 형식이다. ResourceItem366개 중85개의 숫자 id=0, SaleItem4개 중1개의 숫자 id=0 역시 타입 전체 개수와 구분했다.

<details>
<summary>AIDrink — 1개</summary>

- [Drink](<Assets/Resources/SO/AI/Action/Drink.asset>)

</details>

<details>
<summary>AIEat — 1개</summary>

- [Eat](<Assets/Resources/SO/AI/Action/Eat.asset>)

</details>

<details>
<summary>AIExitDungeon — 1개</summary>

- [ExitDungeon](<Assets/Resources/SO/AI/Action/ExitDungeon.asset>)

</details>

<details>
<summary>AIFacilityRoleAction — 3개</summary>

- [Hygiene](<Assets/Resources/SO/AI/Action/Hygiene.asset>)
- [Recreation](<Assets/Resources/SO/AI/Action/Recreation.asset>)
- [Toilet](<Assets/Resources/SO/AI/Action/Toilet.asset>)

</details>

<details>
<summary>AIHaul — 1개</summary>

- [Haul](<Assets/Resources/SO/AI/Action/Haul.asset>)

</details>

<details>
<summary>AIHunt — 1개</summary>

- [Hunt](<Assets/Resources/SO/AI/Action/Hunt.asset>)

</details>

<details>
<summary>AILookAround — 1개</summary>

- [LookAround](<Assets/Resources/SO/AI/Action/LookAround.asset>)

</details>

<details>
<summary>AIPrimitiveBucketWash — 1개</summary>

- [PrimitiveBucketWash](<Assets/Resources/SO/AI/Action/PrimitiveBucketWash.asset>)

</details>

<details>
<summary>AIPrimitiveFieldMeal — 1개</summary>

- [PrimitiveFieldMeal](<Assets/Resources/SO/AI/Action/PrimitiveFieldMeal.asset>)

</details>

<details>
<summary>AIPrimitiveFloorRest — 1개</summary>

- [PrimitiveFloorRest](<Assets/Resources/SO/AI/Action/PrimitiveFloorRest.asset>)

</details>

<details>
<summary>AIPrimitiveLatrine — 1개</summary>

- [PrimitiveLatrine](<Assets/Resources/SO/AI/Action/PrimitiveLatrine.asset>)

</details>

<details>
<summary>AIRescue — 1개</summary>

- [Rescue](<Assets/Resources/SO/AI/Action/Rescue.asset>)

</details>

<details>
<summary>AIRest — 1개</summary>

- [Rest](<Assets/Resources/SO/AI/Action/Rest.asset>)

</details>

<details>
<summary>AIShopping — 1개</summary>

- [Shopping](<Assets/Resources/SO/AI/Action/Shopping.asset>)

</details>

<details>
<summary>AISubstanceUse — 1개</summary>

- [SubstanceUse](<Assets/Resources/SO/AI/Action/SubstanceUse.asset>)

</details>

<details>
<summary>AIWait — 1개</summary>

- [Wait](<Assets/Resources/SO/AI/Action/Wait.asset>)

</details>

<details>
<summary>AIWork — 1개</summary>

- [Work](<Assets/Resources/SO/AI/Action/Work.asset>)

</details>

<details>
<summary>AgeConditionDefinitionSO — 6개</summary>

- [AgeCondition_age-cardiac-degeneration](<Assets/Resources/SO/Population/AgeConditions/AgeCondition_age-cardiac-degeneration.asset>)
- [AgeCondition_age-neural-degeneration](<Assets/Resources/SO/Population/AgeConditions/AgeCondition_age-neural-degeneration.asset>)
- [AgeCondition_age-organ-fibrosis](<Assets/Resources/SO/Population/AgeConditions/AgeCondition_age-organ-fibrosis.asset>)
- [AgeCondition_core-corrosion](<Assets/Resources/SO/Population/AgeConditions/AgeCondition_core-corrosion.asset>)
- [AgeCondition_frame-fatigue](<Assets/Resources/SO/Population/AgeConditions/AgeCondition_frame-fatigue.asset>)
- [AgeCondition_rune-circuit-wear](<Assets/Resources/SO/Population/AgeConditions/AgeCondition_rune-circuit-wear.asset>)

</details>

<details>
<summary>AnatomyConditionLexiconSO — 7개</summary>

- [condition_biological](<Assets/Resources/SO/Medical/ConditionLexicons/condition_biological.asset>)
- [condition_demon](<Assets/Resources/SO/Medical/ConditionLexicons/condition_demon.asset>)
- [condition_golem](<Assets/Resources/SO/Medical/ConditionLexicons/condition_golem.asset>)
- [condition_harpy](<Assets/Resources/SO/Medical/ConditionLexicons/condition_harpy.asset>)
- [condition_myconid](<Assets/Resources/SO/Medical/ConditionLexicons/condition_myconid.asset>)
- [condition_slime](<Assets/Resources/SO/Medical/ConditionLexicons/condition_slime.asset>)
- [condition_vampire](<Assets/Resources/SO/Medical/ConditionLexicons/condition_vampire.asset>)

</details>

<details>
<summary>AnatomyProfileSO — 12개</summary>

- [anatomy_avian](<Assets/Resources/SO/Medical/Anatomy/anatomy_avian.asset>)
- [anatomy_beastkin](<Assets/Resources/SO/Medical/Anatomy/anatomy_beastkin.asset>)
- [anatomy_construct](<Assets/Resources/SO/Medical/Anatomy/anatomy_construct.asset>)
- [anatomy_demon](<Assets/Resources/SO/Medical/Anatomy/anatomy_demon.asset>)
- [anatomy_fungal](<Assets/Resources/SO/Medical/Anatomy/anatomy_fungal.asset>)
- [anatomy_human](<Assets/Resources/SO/Medical/Anatomy/anatomy_human.asset>)
- [anatomy_humanoid](<Assets/Resources/SO/Medical/Anatomy/anatomy_humanoid.asset>)
- [anatomy_kobold](<Assets/Resources/SO/Medical/Anatomy/anatomy_kobold.asset>)
- [anatomy_orc](<Assets/Resources/SO/Medical/Anatomy/anatomy_orc.asset>)
- [anatomy_quadruped](<Assets/Resources/SO/Medical/Anatomy/anatomy_quadruped.asset>)
- [anatomy_slime](<Assets/Resources/SO/Medical/Anatomy/anatomy_slime.asset>)
- [anatomy_vampire](<Assets/Resources/SO/Medical/Anatomy/anatomy_vampire.asset>)

</details>

<details>
<summary>ApparelDefinitionSO — 56개</summary>

- [apparel_apron](<Assets/Resources/SO/Apparel/Definitions/apparel_apron.asset>)
- [apparel_belt](<Assets/Resources/SO/Apparel/Definitions/apparel_belt.asset>)
- [apparel_blouse](<Assets/Resources/SO/Apparel/Definitions/apparel_blouse.asset>)
- [apparel_boots](<Assets/Resources/SO/Apparel/Definitions/apparel_boots.asset>)
- [apparel_ceremonial_dress](<Assets/Resources/SO/Apparel/Definitions/apparel_ceremonial_dress.asset>)
- [apparel_chest_wrap](<Assets/Resources/SO/Apparel/Definitions/apparel_chest_wrap.asset>)
- [apparel_cloak](<Assets/Resources/SO/Apparel/Definitions/apparel_cloak.asset>)
- [apparel_cold_work_suit](<Assets/Resources/SO/Apparel/Definitions/apparel_cold_work_suit.asset>)
- [apparel_contract_sash](<Assets/Resources/SO/Apparel/Definitions/apparel_contract_sash.asset>)
- [apparel_daily_robe](<Assets/Resources/SO/Apparel/Definitions/apparel_daily_robe.asset>)
- [apparel_envoy_coat](<Assets/Resources/SO/Apparel/Definitions/apparel_envoy_coat.asset>)
- [apparel_farmer_workwear](<Assets/Resources/SO/Apparel/Definitions/apparel_farmer_workwear.asset>)
- [apparel_festival_vest](<Assets/Resources/SO/Apparel/Definitions/apparel_festival_vest.asset>)
- [apparel_footwraps](<Assets/Resources/SO/Apparel/Definitions/apparel_footwraps.asset>)
- [apparel_formal_coat](<Assets/Resources/SO/Apparel/Definitions/apparel_formal_coat.asset>)
- [apparel_gloves](<Assets/Resources/SO/Apparel/Definitions/apparel_gloves.asset>)
- [apparel_golem_functional_lining](<Assets/Resources/SO/Apparel/Definitions/apparel_golem_functional_lining.asset>)
- [apparel_hat](<Assets/Resources/SO/Apparel/Definitions/apparel_hat.asset>)
- [apparel_hauling_harness](<Assets/Resources/SO/Apparel/Definitions/apparel_hauling_harness.asset>)
- [apparel_heat_work_suit](<Assets/Resources/SO/Apparel/Definitions/apparel_heat_work_suit.asset>)
- [apparel_hooded_robe](<Assets/Resources/SO/Apparel/Definitions/apparel_hooded_robe.asset>)
- [apparel_horn_ring](<Assets/Resources/SO/Apparel/Definitions/apparel_horn_ring.asset>)
- [apparel_keeper_coat](<Assets/Resources/SO/Apparel/Definitions/apparel_keeper_coat.asset>)
- [apparel_loincloth_underwear](<Assets/Resources/SO/Apparel/Definitions/apparel_loincloth_underwear.asset>)
- [apparel_long_underpants](<Assets/Resources/SO/Apparel/Definitions/apparel_long_underpants.asset>)
- [apparel_lower_underwear](<Assets/Resources/SO/Apparel/Definitions/apparel_lower_underwear.asset>)
- [apparel_miner_workwear](<Assets/Resources/SO/Apparel/Definitions/apparel_miner_workwear.asset>)
- [apparel_mourning_clothes](<Assets/Resources/SO/Apparel/Definitions/apparel_mourning_clothes.asset>)
- [apparel_raincoat](<Assets/Resources/SO/Apparel/Definitions/apparel_raincoat.asset>)
- [apparel_ritual_robe](<Assets/Resources/SO/Apparel/Definitions/apparel_ritual_robe.asset>)
- [apparel_rune_cold_suit](<Assets/Resources/SO/Apparel/Definitions/apparel_rune_cold_suit.asset>)
- [apparel_scarf](<Assets/Resources/SO/Apparel/Definitions/apparel_scarf.asset>)
- [apparel_shorts](<Assets/Resources/SO/Apparel/Definitions/apparel_shorts.asset>)
- [apparel_skirt](<Assets/Resources/SO/Apparel/Definitions/apparel_skirt.asset>)
- [apparel_sky_chorus_shawl](<Assets/Resources/SO/Apparel/Definitions/apparel_sky_chorus_shawl.asset>)
- [apparel_sleep_bottom](<Assets/Resources/SO/Apparel/Definitions/apparel_sleep_bottom.asset>)
- [apparel_sleep_top](<Assets/Resources/SO/Apparel/Definitions/apparel_sleep_top.asset>)
- [apparel_slime_warming_pad](<Assets/Resources/SO/Apparel/Definitions/apparel_slime_warming_pad.asset>)
- [apparel_smith_apron](<Assets/Resources/SO/Apparel/Definitions/apparel_smith_apron.asset>)
- [apparel_smoke_protection_hood](<Assets/Resources/SO/Apparel/Definitions/apparel_smoke_protection_hood.asset>)
- [apparel_socks](<Assets/Resources/SO/Apparel/Definitions/apparel_socks.asset>)
- [apparel_spore_garden_cloak](<Assets/Resources/SO/Apparel/Definitions/apparel_spore_garden_cloak.asset>)
- [apparel_spore_protection_hood](<Assets/Resources/SO/Apparel/Definitions/apparel_spore_protection_hood.asset>)
- [apparel_sterile_gown](<Assets/Resources/SO/Apparel/Definitions/apparel_sterile_gown.asset>)
- [apparel_surgical_apron](<Assets/Resources/SO/Apparel/Definitions/apparel_surgical_apron.asset>)
- [apparel_tail_guard](<Assets/Resources/SO/Apparel/Definitions/apparel_tail_guard.asset>)
- [apparel_tail_ribbon](<Assets/Resources/SO/Apparel/Definitions/apparel_tail_ribbon.asset>)
- [apparel_trousers](<Assets/Resources/SO/Apparel/Definitions/apparel_trousers.asset>)
- [apparel_tunic](<Assets/Resources/SO/Apparel/Definitions/apparel_tunic.asset>)
- [apparel_undershirt](<Assets/Resources/SO/Apparel/Definitions/apparel_undershirt.asset>)
- [apparel_vest](<Assets/Resources/SO/Apparel/Definitions/apparel_vest.asset>)
- [apparel_waterproof_work_suit](<Assets/Resources/SO/Apparel/Definitions/apparel_waterproof_work_suit.asset>)
- [apparel_weapon_vigil_cloak](<Assets/Resources/SO/Apparel/Definitions/apparel_weapon_vigil_cloak.asset>)
- [apparel_wing_cloak](<Assets/Resources/SO/Apparel/Definitions/apparel_wing_cloak.asset>)
- [apparel_wing_harness](<Assets/Resources/SO/Apparel/Definitions/apparel_wing_harness.asset>)
- [apparel_work_shirt](<Assets/Resources/SO/Apparel/Definitions/apparel_work_shirt.asset>)

</details>

<details>
<summary>BattlefieldModifierDefinitionSO — 12개</summary>

- [battlefield_broken-pillars](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_broken-pillars.asset>)
- [battlefield_civilian-corridor](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_civilian-corridor.asset>)
- [battlefield_collapsing-ceiling](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_collapsing-ceiling.asset>)
- [battlefield_elevated-gallery](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_elevated-gallery.asset>)
- [battlefield_flooded-floor](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_flooded-floor.asset>)
- [battlefield_hostage-chain](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_hostage-chain.asset>)
- [battlefield_mana-storm](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_mana-storm.asset>)
- [battlefield_narrow-bridge](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_narrow-bridge.asset>)
- [battlefield_powder-smoke](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_powder-smoke.asset>)
- [battlefield_sealed-exit](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_sealed-exit.asset>)
- [battlefield_spore-bloom](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_spore-bloom.asset>)
- [battlefield_unstable-device](<Assets/Resources/SO/V20/Combat/Modifiers/battlefield_unstable-device.asset>)

</details>

<details>
<summary>BuildingSO — 400개</summary>

- [CA01_진행자단상](<Assets/Resources/SO/Building/Captivity/CA01_진행자단상.asset>)
- [CB01_야수우리](<Assets/Resources/SO/Building/Captivity/CB01_야수우리.asset>)
- [CG01_도박창구](<Assets/Resources/SO/Building/Captivity/CG01_도박창구.asset>)
- [CH01_위험장치](<Assets/Resources/SO/Building/Captivity/CH01_위험장치.asset>)
- [CM01_치료구역](<Assets/Resources/SO/Building/Captivity/CM01_치료구역.asset>)
- [CP01_감방구속대](<Assets/Resources/SO/Building/Captivity/CP01_감방구속대.asset>)
- [CP02_공개형벌장치](<Assets/Resources/SO/Building/Captivity/CP02_공개형벌장치.asset>)
- [CS01_중앙무대](<Assets/Resources/SO/Building/Captivity/CS01_중앙무대.asset>)
- [CS02_관람석](<Assets/Resources/SO/Building/Captivity/CS02_관람석.asset>)
- [CT01_매표소](<Assets/Resources/SO/Building/Captivity/CT01_매표소.asset>)
- [C01_WoodBarricade](<Assets/Resources/SO/Building/Combat/C01_WoodBarricade.asset>)
- [C02_SackBulwark](<Assets/Resources/SO/Building/Combat/C02_SackBulwark.asset>)
- [C03_ArrowScreen](<Assets/Resources/SO/Building/Combat/C03_ArrowScreen.asset>)
- [Door](<Assets/Resources/SO/Building/Door.asset>)
- [Elevator](<Assets/Resources/SO/Building/Elevator.asset>)
- [Hallway](<Assets/Resources/SO/Building/Hallway.asset>)
- [A01_자동화_제어반](<Assets/Resources/SO/Building/Industrial/A01_자동화_제어반.asset>)
- [C01D_컨베이어_하향](<Assets/Resources/SO/Building/Industrial/C01D_컨베이어_하향.asset>)
- [C01L_컨베이어_좌향](<Assets/Resources/SO/Building/Industrial/C01L_컨베이어_좌향.asset>)
- [C01R_컨베이어_우향](<Assets/Resources/SO/Building/Industrial/C01R_컨베이어_우향.asset>)
- [C01U_컨베이어_상향](<Assets/Resources/SO/Building/Industrial/C01U_컨베이어_상향.asset>)
- [C02_컨베이어_입력기](<Assets/Resources/SO/Building/Industrial/C02_컨베이어_입력기.asset>)
- [C03_컨베이어_출력기](<Assets/Resources/SO/Building/Industrial/C03_컨베이어_출력기.asset>)
- [C04_컨베이어_분배기](<Assets/Resources/SO/Building/Industrial/C04_컨베이어_분배기.asset>)
- [C05_컨베이어_합류기](<Assets/Resources/SO/Building/Industrial/C05_컨베이어_합류기.asset>)
- [C06_컨베이어_필터](<Assets/Resources/SO/Building/Industrial/C06_컨베이어_필터.asset>)
- [C07_우선순위_게이트](<Assets/Resources/SO/Building/Industrial/C07_우선순위_게이트.asset>)
- [C08_층간_물류_리프트](<Assets/Resources/SO/Building/Industrial/C08_층간_물류_리프트.asset>)
- [C09_오버플로_배출_게이트](<Assets/Resources/SO/Building/Industrial/C09_오버플로_배출_게이트.asset>)
- [C10_고속_컨베이어](<Assets/Resources/SO/Building/Industrial/C10_고속_컨베이어.asset>)
- [I01_증기_발전기](<Assets/Resources/SO/Building/Industrial/I01_증기_발전기.asset>)
- [I02_수차_발전기](<Assets/Resources/SO/Building/Industrial/I02_수차_발전기.asset>)
- [I03_마나_발전기](<Assets/Resources/SO/Building/Industrial/I03_마나_발전기.asset>)
- [I04_축전지](<Assets/Resources/SO/Building/Industrial/I04_축전지.asset>)
- [I05_회로_차단기](<Assets/Resources/SO/Building/Industrial/I05_회로_차단기.asset>)
- [I06_변압_제어반](<Assets/Resources/SO/Building/Industrial/I06_변압_제어반.asset>)
- [I07_전동_양수_펌프](<Assets/Resources/SO/Building/Industrial/I07_전동_양수_펌프.asset>)
- [I08_상수_탱크](<Assets/Resources/SO/Building/Industrial/I08_상수_탱크.asset>)
- [I09_오수_탱크](<Assets/Resources/SO/Building/Industrial/I09_오수_탱크.asset>)
- [I10_물통_충전소](<Assets/Resources/SO/Building/Industrial/I10_물통_충전소.asset>)
- [I11_오수_침전조](<Assets/Resources/SO/Building/Industrial/I11_오수_침전조.asset>)
- [I12_소독_정수기](<Assets/Resources/SO/Building/Industrial/I12_소독_정수기.asset>)
- [I13_룬_정화_시설](<Assets/Resources/SO/Building/Industrial/I13_룬_정화_시설.asset>)
- [I14_샤워_시설](<Assets/Resources/SO/Building/Industrial/I14_샤워_시설.asset>)
- [I15_전기_아크등](<Assets/Resources/SO/Building/Industrial/I15_전기_아크등.asset>)
- [I16_전기_제련_도가니](<Assets/Resources/SO/Building/Industrial/I16_전기_제련_도가니.asset>)
- [I17_룬_조율실](<Assets/Resources/SO/Building/Industrial/I17_룬_조율실.asset>)
- [I18_계보_기록실](<Assets/Resources/SO/Building/Industrial/I18_계보_기록실.asset>)
- [I19_태양등](<Assets/Resources/SO/Building/Industrial/I19_태양등.asset>)
- [I20_인공태양](<Assets/Resources/SO/Building/Industrial/I20_인공태양.asset>)
- [U01_전력선](<Assets/Resources/SO/Building/Industrial/U01_전력선.asset>)
- [U02_상수관](<Assets/Resources/SO/Building/Industrial/U02_상수관.asset>)
- [U03_하수관](<Assets/Resources/SO/Building/Industrial/U03_하수관.asset>)
- [U04_통합_기반_덕트](<Assets/Resources/SO/Building/Industrial/U04_통합_기반_덕트.asset>)
- [InteriorDoor](<Assets/Resources/SO/Building/InteriorDoor.asset>)
- [M01_응급처치대](<Assets/Resources/SO/Building/Medical/M01_응급처치대.asset>)
- [M02_해부대](<Assets/Resources/SO/Building/Medical/M02_해부대.asset>)
- [M03_외과수술대](<Assets/Resources/SO/Building/Medical/M03_외과수술대.asset>)
- [M04_세정대](<Assets/Resources/SO/Building/Medical/M04_세정대.asset>)
- [M05_마취장치](<Assets/Resources/SO/Building/Medical/M05_마취장치.asset>)
- [M06_보철조립대](<Assets/Resources/SO/Building/Medical/M06_보철조립대.asset>)
- [M07_재활보조대](<Assets/Resources/SO/Building/Medical/M07_재활보조대.asset>)
- [M08_장기보관함](<Assets/Resources/SO/Building/Medical/M08_장기보관함.asset>)
- [M09_순환이식대](<Assets/Resources/SO/Building/Medical/M09_순환이식대.asset>)
- [M10_면역조절기](<Assets/Resources/SO/Building/Medical/M10_면역조절기.asset>)
- [M11_격리회복침상](<Assets/Resources/SO/Building/Medical/M11_격리회복침상.asset>)
- [M12_비전개조대](<Assets/Resources/SO/Building/Medical/M12_비전개조대.asset>)
- [M13_룬봉합기](<Assets/Resources/SO/Building/Medical/M13_룬봉합기.asset>)
- [D01_간이화덕](<Assets/Resources/SO/Building/Modular/D01_간이화덕.asset>)
- [D02_고기그릴](<Assets/Resources/SO/Building/Modular/D02_고기그릴.asset>)
- [D03_조리손질대](<Assets/Resources/SO/Building/Modular/D03_조리손질대.asset>)
- [D04_배식카운터](<Assets/Resources/SO/Building/Modular/D04_배식카운터.asset>)
- [D05_소형식탁](<Assets/Resources/SO/Building/Modular/D05_소형식탁.asset>)
- [D06_대형연회식탁](<Assets/Resources/SO/Building/Modular/D06_대형연회식탁.asset>)
- [D07_목제의자](<Assets/Resources/SO/Building/Modular/D07_목제의자.asset>)
- [D08_푹신한의자](<Assets/Resources/SO/Building/Modular/D08_푹신한의자.asset>)
- [D09_긴벤치](<Assets/Resources/SO/Building/Modular/D09_긴벤치.asset>)
- [D10_식재료선반](<Assets/Resources/SO/Building/Modular/D10_식재료선반.asset>)
- [D11_고기걸이](<Assets/Resources/SO/Building/Modular/D11_고기걸이.asset>)
- [D12_술음료장](<Assets/Resources/SO/Building/Modular/D12_술음료장.asset>)
- [E01_벽횃불](<Assets/Resources/SO/Building/Modular/E01_벽횃불.asset>)
- [E02_바닥화로](<Assets/Resources/SO/Building/Modular/E02_바닥화로.asset>)
- [E03_샹들리에](<Assets/Resources/SO/Building/Modular/E03_샹들리에.asset>)
- [E04_소형러그](<Assets/Resources/SO/Building/Modular/E04_소형러그.asset>)
- [E05_세력깃발](<Assets/Resources/SO/Building/Modular/E05_세력깃발.asset>)
- [E06_액자초상화](<Assets/Resources/SO/Building/Modular/E06_액자초상화.asset>)
- [E07_촛대](<Assets/Resources/SO/Building/Modular/E07_촛대.asset>)
- [E08_해골피장식](<Assets/Resources/SO/Building/Modular/E08_해골피장식.asset>)
- [E09_방표지판](<Assets/Resources/SO/Building/Modular/E09_방표지판.asset>)
- [E10_냉각기](<Assets/Resources/SO/Building/Modular/E10_냉각기.asset>)
- [E11_공조기](<Assets/Resources/SO/Building/Modular/E11_공조기.asset>)
- [E12_환기덕트](<Assets/Resources/SO/Building/Modular/E12_환기덕트.asset>)
- [E13_송풍구](<Assets/Resources/SO/Building/Modular/E13_송풍구.asset>)
- [E14_배기팬](<Assets/Resources/SO/Building/Modular/E14_배기팬.asset>)
- [G01_경비초소책상](<Assets/Resources/SO/Building/Modular/G01_경비초소책상.asset>)
- [G02_경보종](<Assets/Resources/SO/Building/Modular/G02_경보종.asset>)
- [G03_순찰상황판](<Assets/Resources/SO/Building/Modular/G03_순찰상황판.asset>)
- [G04_전술지도탁자](<Assets/Resources/SO/Building/Modular/G04_전술지도탁자.asset>)
- [G05_전투깃발](<Assets/Resources/SO/Building/Modular/G05_전투깃발.asset>)
- [G06_전리품거치대](<Assets/Resources/SO/Building/Modular/G06_전리품거치대.asset>)
- [H01_변기](<Assets/Resources/SO/Building/Modular/H01_변기.asset>)
- [H02_화장실칸막이](<Assets/Resources/SO/Building/Modular/H02_화장실칸막이.asset>)
- [H03_세면대](<Assets/Resources/SO/Building/Modular/H03_세면대.asset>)
- [H04_목욕통](<Assets/Resources/SO/Building/Modular/H04_목욕통.asset>)
- [H05_수건걸이](<Assets/Resources/SO/Building/Modular/H05_수건걸이.asset>)
- [H06_청소도구함](<Assets/Resources/SO/Building/Modular/H06_청소도구함.asset>)
- [H07_바닥배수구](<Assets/Resources/SO/Building/Modular/H07_바닥배수구.asset>)
- [L01_대형보관선반](<Assets/Resources/SO/Building/Modular/L01_대형보관선반.asset>)
- [L02_상자더미](<Assets/Resources/SO/Building/Modular/L02_상자더미.asset>)
- [L03_통더미](<Assets/Resources/SO/Building/Modular/L03_통더미.asset>)
- [L04_자루더미](<Assets/Resources/SO/Building/Modular/L04_자루더미.asset>)
- [L05_식재료저장함](<Assets/Resources/SO/Building/Modular/L05_식재료저장함.asset>)
- [L06_무기로커](<Assets/Resources/SO/Building/Modular/L06_무기로커.asset>)
- [L07_마력보관함](<Assets/Resources/SO/Building/Modular/L07_마력보관함.asset>)
- [L08_보호장비보관함](<Assets/Resources/SO/Building/Modular/L08_보호장비보관함.asset>)
- [M01_마력수정선반](<Assets/Resources/SO/Building/Modular/M01_마력수정선반.asset>)
- [M02_마력저장조](<Assets/Resources/SO/Building/Modular/M02_마력저장조.asset>)
- [M03_룬안정기](<Assets/Resources/SO/Building/Modular/M03_룬안정기.asset>)
- [M04_의식초점석](<Assets/Resources/SO/Building/Modular/M04_의식초점석.asset>)
- [P01_제분소](<Assets/Resources/SO/Building/Modular/P01_제분소.asset>)
- [P02_양조장](<Assets/Resources/SO/Building/Modular/P02_양조장.asset>)
- [P03_제재소](<Assets/Resources/SO/Building/Modular/P03_제재소.asset>)
- [P04_숯가마](<Assets/Resources/SO/Building/Modular/P04_숯가마.asset>)
- [P05_석재절단대](<Assets/Resources/SO/Building/Modular/P05_석재절단대.asset>)
- [P06_광석선별대](<Assets/Resources/SO/Building/Modular/P06_광석선별대.asset>)
- [P07_용광로](<Assets/Resources/SO/Building/Modular/P07_용광로.asset>)
- [P08_제강로](<Assets/Resources/SO/Building/Modular/P08_제강로.asset>)
- [P09_귀금세공대](<Assets/Resources/SO/Building/Modular/P09_귀금세공대.asset>)
- [P10_비전단조대](<Assets/Resources/SO/Building/Modular/P10_비전단조대.asset>)
- [P11_직조기](<Assets/Resources/SO/Building/Modular/P11_직조기.asset>)
- [P12_무두질대](<Assets/Resources/SO/Building/Modular/P12_무두질대.asset>)
- [P13_퇴비장](<Assets/Resources/SO/Building/Modular/P13_퇴비장.asset>)
- [P14_증류기](<Assets/Resources/SO/Building/Modular/P14_증류기.asset>)
- [P15_조리대](<Assets/Resources/SO/Building/Modular/P15_조리대.asset>)
- [P16_훈연대](<Assets/Resources/SO/Building/Modular/P16_훈연대.asset>)
- [P17_사료배합대](<Assets/Resources/SO/Building/Modular/P17_사료배합대.asset>)
- [P18_약제대](<Assets/Resources/SO/Building/Modular/P18_약제대.asset>)
- [P19_연금대](<Assets/Resources/SO/Building/Modular/P19_연금대.asset>)
- [P20_몽직기](<Assets/Resources/SO/Building/Modular/P20_몽직기.asset>)
- [P21_대장간](<Assets/Resources/SO/Building/Modular/P21_대장간.asset>)
- [P22_심부채석장](<Assets/Resources/SO/Building/Modular/P22_심부채석장.asset>)
- [P23_야외경작지](<Assets/Resources/SO/Building/Modular/P23_야외경작지.asset>)
- [P24_실내재배조](<Assets/Resources/SO/Building/Modular/P24_실내재배조.asset>)
- [P25_폐기소각로](<Assets/Resources/SO/Building/Modular/P25_폐기소각로.asset>)
- [Q01_연구책상](<Assets/Resources/SO/Building/Modular/Q01_연구책상.asset>)
- [Q02_연금술작업대](<Assets/Resources/SO/Building/Modular/Q02_연금술작업대.asset>)
- [Q03_연구용책장](<Assets/Resources/SO/Building/Modular/Q03_연구용책장.asset>)
- [Q04_시약선반](<Assets/Resources/SO/Building/Modular/Q04_시약선반.asset>)
- [Q05_표본보관장](<Assets/Resources/SO/Building/Modular/Q05_표본보관장.asset>)
- [Q06_설계판](<Assets/Resources/SO/Building/Modular/Q06_설계판.asset>)
- [R01_간이침대](<Assets/Resources/SO/Building/Modular/R01_간이침대.asset>)
- [R02_정식침대](<Assets/Resources/SO/Building/Modular/R02_정식침대.asset>)
- [R03_이층침대](<Assets/Resources/SO/Building/Modular/R03_이층침대.asset>)
- [R04_휴식용소파](<Assets/Resources/SO/Building/Modular/R04_휴식용소파.asset>)
- [R05_협탁](<Assets/Resources/SO/Building/Modular/R05_협탁.asset>)
- [R06_옷장](<Assets/Resources/SO/Building/Modular/R06_옷장.asset>)
- [R07_영주집무책상](<Assets/Resources/SO/Building/Modular/R07_영주집무책상.asset>)
- [R08_지휘의자](<Assets/Resources/SO/Building/Modular/R08_지휘의자.asset>)
- [R09_개인보관함](<Assets/Resources/SO/Building/Modular/R09_개인보관함.asset>)
- [R10_침실용책장](<Assets/Resources/SO/Building/Modular/R10_침실용책장.asset>)
- [S01_판매카운터](<Assets/Resources/SO/Building/Modular/S01_판매카운터.asset>)
- [S02_잡화진열선반](<Assets/Resources/SO/Building/Modular/S02_잡화진열선반.asset>)
- [S03_잠금진열장](<Assets/Resources/SO/Building/Modular/S03_잠금진열장.asset>)
- [S04_잡화상자](<Assets/Resources/SO/Building/Modular/S04_잡화상자.asset>)
- [S05_무기거치대](<Assets/Resources/SO/Building/Modular/S05_무기거치대.asset>)
- [S06_갑옷거치대](<Assets/Resources/SO/Building/Modular/S06_갑옷거치대.asset>)
- [S07_무기보관함](<Assets/Resources/SO/Building/Modular/S07_무기보관함.asset>)
- [S08_대장작업대](<Assets/Resources/SO/Building/Modular/S08_대장작업대.asset>)
- [T01_훈련허수아비](<Assets/Resources/SO/Building/Modular/T01_훈련허수아비.asset>)
- [T02_사격과녁](<Assets/Resources/SO/Building/Modular/T02_사격과녁.asset>)
- [T03_중량훈련석](<Assets/Resources/SO/Building/Modular/T03_중량훈련석.asset>)
- [T04_대련매트](<Assets/Resources/SO/Building/Modular/T04_대련매트.asset>)
- [DefenseControlDesk](<Assets/Resources/SO/Building/P1/DefenseControlDesk.asset>)
- [DefenseCorridorDetector](<Assets/Resources/SO/Building/P1/DefenseCorridorDetector.asset>)
- [DefenseLinkedDropGate](<Assets/Resources/SO/Building/P1/DefenseLinkedDropGate.asset>)
- [DefenseMaintenanceBench](<Assets/Resources/SO/Building/P1/DefenseMaintenanceBench.asset>)
- [DefenseSupplyDepot](<Assets/Resources/SO/Building/P1/DefenseSupplyDepot.asset>)
- [DefenseWallLauncher](<Assets/Resources/SO/Building/P1/DefenseWallLauncher.asset>)
- [P1_AlarmCoil](<Assets/Resources/SO/Building/P1/P1_AlarmCoil.asset>)
- [P1_CorrosionFreezer](<Assets/Resources/SO/Building/P1/P1_CorrosionFreezer.asset>)
- [P1_FireVent](<Assets/Resources/SO/Building/P1/P1_FireVent.asset>)
- [P1_IceVent](<Assets/Resources/SO/Building/P1/P1_IceVent.asset>)
- [P1_LightningPillar](<Assets/Resources/SO/Building/P1/P1_LightningPillar.asset>)
- [P1_PoisonPool](<Assets/Resources/SO/Building/P1/P1_PoisonPool.asset>)
- [P1_SpikeTrap](<Assets/Resources/SO/Building/P1/P1_SpikeTrap.asset>)
- [P1_StormFireTrap](<Assets/Resources/SO/Building/P1/P1_StormFireTrap.asset>)
- [P1_TreasuryBoltThrower](<Assets/Resources/SO/Building/P1/P1_TreasuryBoltThrower.asset>)
- [P1_VenomSpikeTrap](<Assets/Resources/SO/Building/P1/P1_VenomSpikeTrap.asset>)
- [WS01_미세_체_선반](<Assets/Resources/SO/Building/ProductionSupport/WS01_미세_체_선반.asset>)
- [WS02_담금_당화조](<Assets/Resources/SO/Building/ProductionSupport/WS02_담금_당화조.asset>)
- [WS03_수동_발효조](<Assets/Resources/SO/Building/ProductionSupport/WS03_수동_발효조.asset>)
- [WS04_온도_제어_발효조](<Assets/Resources/SO/Building/ProductionSupport/WS04_온도_제어_발효조.asset>)
- [WS05_숙성_오크통](<Assets/Resources/SO/Building/ProductionSupport/WS05_숙성_오크통.asset>)
- [WS06_세척_병입대](<Assets/Resources/SO/Building/ProductionSupport/WS06_세척_병입대.asset>)
- [WS07_분별_증류탑](<Assets/Resources/SO/Building/ProductionSupport/WS07_분별_증류탑.asset>)
- [WS08_화덕_가마솥](<Assets/Resources/SO/Building/ProductionSupport/WS08_화덕_가마솥.asset>)
- [WS09_벽돌_오븐](<Assets/Resources/SO/Building/ProductionSupport/WS09_벽돌_오븐.asset>)
- [WS10_전기_오븐](<Assets/Resources/SO/Building/ProductionSupport/WS10_전기_오븐.asset>)
- [WS11_세척_전처리_싱크](<Assets/Resources/SO/Building/ProductionSupport/WS11_세척_전처리_싱크.asset>)
- [WS12_냉장_준비대](<Assets/Resources/SO/Building/ProductionSupport/WS12_냉장_준비대.asset>)
- [WS13_향신료_선반](<Assets/Resources/SO/Building/ProductionSupport/WS13_향신료_선반.asset>)
- [WS14_염장_절임조](<Assets/Resources/SO/Building/ProductionSupport/WS14_염장_절임조.asset>)
- [WS15_치즈_응고조](<Assets/Resources/SO/Building/ProductionSupport/WS15_치즈_응고조.asset>)
- [WS16_치즈_숙성_선반](<Assets/Resources/SO/Building/ProductionSupport/WS16_치즈_숙성_선반.asset>)
- [WS17_영양_배합_저울](<Assets/Resources/SO/Building/ProductionSupport/WS17_영양_배합_저울.asset>)
- [WS18_연기_포집_후드](<Assets/Resources/SO/Building/ProductionSupport/WS18_연기_포집_후드.asset>)
- [WS19_목재_처리조](<Assets/Resources/SO/Building/ProductionSupport/WS19_목재_처리조.asset>)
- [WS20_정밀_연마기](<Assets/Resources/SO/Building/ProductionSupport/WS20_정밀_연마기.asset>)
- [WS21_도가니_선반](<Assets/Resources/SO/Building/ProductionSupport/WS21_도가니_선반.asset>)
- [WS22_세공_도구함](<Assets/Resources/SO/Building/ProductionSupport/WS22_세공_도구함.asset>)
- [WS23_마나_안정기](<Assets/Resources/SO/Building/ProductionSupport/WS23_마나_안정기.asset>)
- [WS24_직조_보조_선반](<Assets/Resources/SO/Building/ProductionSupport/WS24_직조_보조_선반.asset>)
- [WS25_무균_약품_보관함](<Assets/Resources/SO/Building/ProductionSupport/WS25_무균_약품_보관함.asset>)
- [WS26_마나_응축기](<Assets/Resources/SO/Building/ProductionSupport/WS26_마나_응축기.asset>)
- [WS27_대장_도구함](<Assets/Resources/SO/Building/ProductionSupport/WS27_대장_도구함.asset>)
- [WS28_실내_생장_제어기](<Assets/Resources/SO/Building/ProductionSupport/WS28_실내_생장_제어기.asset>)
- [RF01_채집_바구니_작업대](<Assets/Resources/SO/Building/ResearchOverhaul/RF01_채집_바구니_작업대.asset>)
- [RF02_중력식_수문](<Assets/Resources/SO/Building/ResearchOverhaul/RF02_중력식_수문.asset>)
- [RF03_동굴_재배_선반](<Assets/Resources/SO/Building/ResearchOverhaul/RF03_동굴_재배_선반.asset>)
- [RF04_문장_깃발_제작대](<Assets/Resources/SO/Building/ResearchOverhaul/RF04_문장_깃발_제작대.asset>)
- [RF05_의식_화로](<Assets/Resources/SO/Building/ResearchOverhaul/RF05_의식_화로.asset>)
- [RF06_운반_멜빵_걸이](<Assets/Resources/SO/Building/ResearchOverhaul/RF06_운반_멜빵_걸이.asset>)
- [RF07_가격표_게시판](<Assets/Resources/SO/Building/ResearchOverhaul/RF07_가격표_게시판.asset>)
- [RF08_피의_무대_배수구](<Assets/Resources/SO/Building/ResearchOverhaul/RF08_피의_무대_배수구.asset>)
- [RF09_포로_작업_도구함](<Assets/Resources/SO/Building/ResearchOverhaul/RF09_포로_작업_도구함.asset>)
- [RF100_탄도_시험장](<Assets/Resources/SO/Building/ResearchOverhaul/RF100_탄도_시험장.asset>)
- [RF101_흑강_주조_보조로](<Assets/Resources/SO/Building/ResearchOverhaul/RF101_흑강_주조_보조로.asset>)
- [RF10_강화_구속구_선반](<Assets/Resources/SO/Building/ResearchOverhaul/RF10_강화_구속구_선반.asset>)
- [RF11_공연_소품_보관대](<Assets/Resources/SO/Building/ResearchOverhaul/RF11_공연_소품_보관대.asset>)
- [RF12_동맹_신호기](<Assets/Resources/SO/Building/ResearchOverhaul/RF12_동맹_신호기.asset>)
- [RF13_균사_재배_선반](<Assets/Resources/SO/Building/ResearchOverhaul/RF13_균사_재배_선반.asset>)
- [RF14_벌목_키트_걸이](<Assets/Resources/SO/Building/ResearchOverhaul/RF14_벌목_키트_걸이.asset>)
- [RF15_쐐기_도끼_작업대](<Assets/Resources/SO/Building/ResearchOverhaul/RF15_쐐기_도끼_작업대.asset>)
- [RF16_방부_처리_목재대](<Assets/Resources/SO/Building/ResearchOverhaul/RF16_방부_처리_목재대.asset>)
- [RF17_번식_장부대](<Assets/Resources/SO/Building/ResearchOverhaul/RF17_번식_장부대.asset>)
- [RF18_혈통_촉진제_선반](<Assets/Resources/SO/Building/ResearchOverhaul/RF18_혈통_촉진제_선반.asset>)
- [RF19_마구_선반](<Assets/Resources/SO/Building/ResearchOverhaul/RF19_마구_선반.asset>)
- [RF20_조련용_고삐_걸이](<Assets/Resources/SO/Building/ResearchOverhaul/RF20_조련용_고삐_걸이.asset>)
- [RF21_동력_공구날_연마대](<Assets/Resources/SO/Building/ResearchOverhaul/RF21_동력_공구날_연마대.asset>)
- [RF22_자동_세척기](<Assets/Resources/SO/Building/ResearchOverhaul/RF22_자동_세척기.asset>)
- [RF23_룬_버스_결합기](<Assets/Resources/SO/Building/ResearchOverhaul/RF23_룬_버스_결합기.asset>)
- [RF24_방어시설_장전기](<Assets/Resources/SO/Building/ResearchOverhaul/RF24_방어시설_장전기.asset>)
- [RF25_시제품_연구실](<Assets/Resources/SO/Building/ResearchOverhaul/RF25_시제품_연구실.asset>)
- [RF26_재료_시험기](<Assets/Resources/SO/Building/ResearchOverhaul/RF26_재료_시험기.asset>)
- [RF27_기계_기초대](<Assets/Resources/SO/Building/ResearchOverhaul/RF27_기계_기초대.asset>)
- [RF28_냉각_매니폴드](<Assets/Resources/SO/Building/ResearchOverhaul/RF28_냉각_매니폴드.asset>)
- [RF29_유량계](<Assets/Resources/SO/Building/ResearchOverhaul/RF29_유량계.asset>)
- [RF30_정비_부품함](<Assets/Resources/SO/Building/ResearchOverhaul/RF30_정비_부품함.asset>)
- [RF31_전동_선반](<Assets/Resources/SO/Building/ResearchOverhaul/RF31_전동_선반.asset>)
- [RF32_정밀_게이지](<Assets/Resources/SO/Building/ResearchOverhaul/RF32_정밀_게이지.asset>)
- [RF33_룬_제어반](<Assets/Resources/SO/Building/ResearchOverhaul/RF33_룬_제어반.asset>)
- [RF34_무기_도면걸이](<Assets/Resources/SO/Building/ResearchOverhaul/RF34_무기_도면걸이.asset>)
- [RF35_방어구_맞춤대](<Assets/Resources/SO/Building/ResearchOverhaul/RF35_방어구_맞춤대.asset>)
- [RF36_궁시_지그](<Assets/Resources/SO/Building/ResearchOverhaul/RF36_궁시_지그.asset>)
- [RF37_권양_작업대](<Assets/Resources/SO/Building/ResearchOverhaul/RF37_권양_작업대.asset>)
- [RF38_사슬_조립틀](<Assets/Resources/SO/Building/ResearchOverhaul/RF38_사슬_조립틀.asset>)
- [RF39_관절_지그](<Assets/Resources/SO/Building/ResearchOverhaul/RF39_관절_지그.asset>)
- [RF40_화약_분쇄소](<Assets/Resources/SO/Building/ResearchOverhaul/RF40_화약_분쇄소.asset>)
- [RF41_탄약_압착기](<Assets/Resources/SO/Building/ResearchOverhaul/RF41_탄약_압착기.asset>)
- [RF42_부품_감정대](<Assets/Resources/SO/Building/ResearchOverhaul/RF42_부품_감정대.asset>)
- [RF43_부품_복원_작업대](<Assets/Resources/SO/Building/ResearchOverhaul/RF43_부품_복원_작업대.asset>)
- [RF44_정밀_장착대](<Assets/Resources/SO/Building/ResearchOverhaul/RF44_정밀_장착대.asset>)
- [RF45_성장형_골격_지그](<Assets/Resources/SO/Building/ResearchOverhaul/RF45_성장형_골격_지그.asset>)
- [RF46_계측_작업대](<Assets/Resources/SO/Building/ResearchOverhaul/RF46_계측_작업대.asset>)
- [RF47_구성체_핵_공학대](<Assets/Resources/SO/Building/ResearchOverhaul/RF47_구성체_핵_공학대.asset>)
- [RF48_배식_운영판](<Assets/Resources/SO/Building/ResearchOverhaul/RF48_배식_운영판.asset>)
- [RF49_계절력_기록대](<Assets/Resources/SO/Building/ResearchOverhaul/RF49_계절력_기록대.asset>)
- [RF50_작물_달력대](<Assets/Resources/SO/Building/ResearchOverhaul/RF50_작물_달력대.asset>)
- [RF51_기상_관측탑](<Assets/Resources/SO/Building/ResearchOverhaul/RF51_기상_관측탑.asset>)
- [RF52_토양_검사대](<Assets/Resources/SO/Building/ResearchOverhaul/RF52_토양_검사대.asset>)
- [RF53_계절_저장_선반](<Assets/Resources/SO/Building/ResearchOverhaul/RF53_계절_저장_선반.asset>)
- [RF54_재배_온실](<Assets/Resources/SO/Building/ResearchOverhaul/RF54_재배_온실.asset>)
- [RF55_번식_일정_축사](<Assets/Resources/SO/Building/ResearchOverhaul/RF55_번식_일정_축사.asset>)
- [RF56_기후_제어실](<Assets/Resources/SO/Building/ResearchOverhaul/RF56_기후_제어실.asset>)
- [RF57_가구_등록대](<Assets/Resources/SO/Building/ResearchOverhaul/RF57_가구_등록대.asset>)
- [RF58_보육실](<Assets/Resources/SO/Building/ResearchOverhaul/RF58_보육실.asset>)
- [RF59_산과실](<Assets/Resources/SO/Building/ResearchOverhaul/RF59_산과실.asset>)
- [RF60_교실](<Assets/Resources/SO/Building/ResearchOverhaul/RF60_교실.asset>)
- [RF61_도제_작업대](<Assets/Resources/SO/Building/ResearchOverhaul/RF61_도제_작업대.asset>)
- [RF62_계보_관리실](<Assets/Resources/SO/Building/ResearchOverhaul/RF62_계보_관리실.asset>)
- [RF63_노화_평가대](<Assets/Resources/SO/Building/ResearchOverhaul/RF63_노화_평가대.asset>)
- [RF64_연령_계측기](<Assets/Resources/SO/Building/ResearchOverhaul/RF64_연령_계측기.asset>)
- [RF65_노인_병상](<Assets/Resources/SO/Building/ResearchOverhaul/RF65_노인_병상.asset>)
- [RF66_만성_관리실](<Assets/Resources/SO/Building/ResearchOverhaul/RF66_만성_관리실.asset>)
- [RF67_재생_배양조](<Assets/Resources/SO/Building/ResearchOverhaul/RF67_재생_배양조.asset>)
- [RF68_장기_재생_수술실](<Assets/Resources/SO/Building/ResearchOverhaul/RF68_장기_재생_수술실.asset>)
- [RF69_회춘_수혈실](<Assets/Resources/SO/Building/ResearchOverhaul/RF69_회춘_수혈실.asset>)
- [RF70_룬_동면실](<Assets/Resources/SO/Building/ResearchOverhaul/RF70_룬_동면실.asset>)
- [RF71_전신_재생조](<Assets/Resources/SO/Building/ResearchOverhaul/RF71_전신_재생조.asset>)
- [RF72_시간_고정실](<Assets/Resources/SO/Building/ResearchOverhaul/RF72_시간_고정실.asset>)
- [RF73_감염_진단대](<Assets/Resources/SO/Building/ResearchOverhaul/RF73_감염_진단대.asset>)
- [RF74_격리_병동](<Assets/Resources/SO/Building/ResearchOverhaul/RF74_격리_병동.asset>)
- [RF75_혈청_검사대](<Assets/Resources/SO/Building/ResearchOverhaul/RF75_혈청_검사대.asset>)
- [RF76_백신_연구실](<Assets/Resources/SO/Building/ResearchOverhaul/RF76_백신_연구실.asset>)
- [RF77_역학_상황판](<Assets/Resources/SO/Building/ResearchOverhaul/RF77_역학_상황판.asset>)
- [RF78_유전_기록고](<Assets/Resources/SO/Building/ResearchOverhaul/RF78_유전_기록고.asset>)
- [RF79_형질_분석기](<Assets/Resources/SO/Building/ResearchOverhaul/RF79_형질_분석기.asset>)
- [RF80_유전_상담실](<Assets/Resources/SO/Building/ResearchOverhaul/RF80_유전_상담실.asset>)
- [RF81_교차계통_배양기](<Assets/Resources/SO/Building/ResearchOverhaul/RF81_교차계통_배양기.asset>)
- [RF82_방_배정대](<Assets/Resources/SO/Building/ResearchOverhaul/RF82_방_배정대.asset>)
- [RF83_가족실_칸막이](<Assets/Resources/SO/Building/ResearchOverhaul/RF83_가족실_칸막이.asset>)
- [RF84_보호자_등록소](<Assets/Resources/SO/Building/ResearchOverhaul/RF84_보호자_등록소.asset>)
- [RF85_상담실](<Assets/Resources/SO/Building/ResearchOverhaul/RF85_상담실.asset>)
- [RF86_시신_처리대](<Assets/Resources/SO/Building/ResearchOverhaul/RF86_시신_처리대.asset>)
- [RF87_추모실](<Assets/Resources/SO/Building/ResearchOverhaul/RF87_추모실.asset>)
- [RF88_기후_지도실](<Assets/Resources/SO/Building/ResearchOverhaul/RF88_기후_지도실.asset>)
- [RF89_원정_천문시계실](<Assets/Resources/SO/Building/ResearchOverhaul/RF89_원정_천문시계실.asset>)
- [RF90_종자_선별대](<Assets/Resources/SO/Building/ResearchOverhaul/RF90_종자_선별대.asset>)
- [RF91_방제_조제대](<Assets/Resources/SO/Building/ResearchOverhaul/RF91_방제_조제대.asset>)
- [RF92_작물_병리실](<Assets/Resources/SO/Building/ResearchOverhaul/RF92_작물_병리실.asset>)
- [RF93_육종_온실](<Assets/Resources/SO/Building/ResearchOverhaul/RF93_육종_온실.asset>)
- [RF94_경력_기록대](<Assets/Resources/SO/Building/ResearchOverhaul/RF94_경력_기록대.asset>)
- [RF95_은퇴자_휴게실](<Assets/Resources/SO/Building/ResearchOverhaul/RF95_은퇴자_휴게실.asset>)
- [RF96_멘토_학원](<Assets/Resources/SO/Building/ResearchOverhaul/RF96_멘토_학원.asset>)
- [RF97_공명_조율실](<Assets/Resources/SO/Building/ResearchOverhaul/RF97_공명_조율실.asset>)
- [RF98_보안_거래_금고](<Assets/Resources/SO/Building/ResearchOverhaul/RF98_보안_거래_금고.asset>)
- [RF99_방어_제어반](<Assets/Resources/SO/Building/ResearchOverhaul/RF99_방어_제어반.asset>)
- [SR01_숙박 접수대](<Assets/Resources/SO/Building/ServiceRooms/SR01_숙박 접수대.asset>)
- [SR02_목욕 접수대](<Assets/Resources/SO/Building/ServiceRooms/SR02_목욕 접수대.asset>)
- [SR03_의료 분류대](<Assets/Resources/SO/Building/ServiceRooms/SR03_의료 분류대.asset>)
- [SR04_순번판](<Assets/Resources/SO/Building/ServiceRooms/SR04_순번판.asset>)
- [SR05_보온 배식대](<Assets/Resources/SO/Building/ServiceRooms/SR05_보온 배식대.asset>)
- [SR06_자동 계산대](<Assets/Resources/SO/Building/ServiceRooms/SR06_자동 계산대.asset>)
- [SR07_슬라임 전용 좌석](<Assets/Resources/SO/Building/ServiceRooms/SR07_슬라임 전용 좌석.asset>)
- [SR08_오크 전용 좌석](<Assets/Resources/SO/Building/ServiceRooms/SR08_오크 전용 좌석.asset>)
- [SR09_뱀파이어 전용 좌석](<Assets/Resources/SO/Building/ServiceRooms/SR09_뱀파이어 전용 좌석.asset>)
- [SR10_객실 정리함](<Assets/Resources/SO/Building/ServiceRooms/SR10_객실 정리함.asset>)
- [SR11_목욕 위생대](<Assets/Resources/SO/Building/ServiceRooms/SR11_목욕 위생대.asset>)
- [SR12_의료 호출판](<Assets/Resources/SO/Building/ServiceRooms/SR12_의료 호출판.asset>)
- [SR13_주문 접수대](<Assets/Resources/SO/Building/ServiceRooms/SR13_주문 접수대.asset>)
- [SR14_분리 계산대](<Assets/Resources/SO/Building/ServiceRooms/SR14_분리 계산대.asset>)
- [SR15_자동 객실 배정판](<Assets/Resources/SO/Building/ServiceRooms/SR15_자동 객실 배정판.asset>)
- [SR16_자동 급배수 제어기](<Assets/Resources/SO/Building/ServiceRooms/SR16_자동 급배수 제어기.asset>)
- [Stair](<Assets/Resources/SO/Building/Stair.asset>)
- [V22_9301_재단_재봉_작업대](<Assets/Resources/SO/Building/V22Apparel/V22_9301_재단_재봉_작업대.asset>)
- [V22_9302_문양_장식_작업대](<Assets/Resources/SO/Building/V22Apparel/V22_9302_문양_장식_작업대.asset>)
- [V22_9303_손세탁_수조](<Assets/Resources/SO/Building/V22Apparel/V22_9303_손세탁_수조.asset>)
- [V22_9304_실내_건조대](<Assets/Resources/SO/Building/V22Apparel/V22_9304_실내_건조대.asset>)
- [V22_9305_동력_세탁_건조기](<Assets/Resources/SO/Building/V22Apparel/V22_9305_동력_세탁_건조기.asset>)
- [V22_9306_의복_진열대](<Assets/Resources/SO/Building/V22Apparel/V22_9306_의복_진열대.asset>)
- [V22_9307_탈의_칸막이](<Assets/Resources/SO/Building/V22Apparel/V22_9307_탈의_칸막이.asset>)
- [V22_9308_수선_접수대](<Assets/Resources/SO/Building/V22Apparel/V22_9308_수선_접수대.asset>)
- [V22_9309_섬유_선별대](<Assets/Resources/SO/Building/V22Apparel/V22_9309_섬유_선별대.asset>)
- [V22_9310_침지_정련조](<Assets/Resources/SO/Building/V22Apparel/V22_9310_침지_정련조.asset>)
- [V22_9311_수동_방적기](<Assets/Resources/SO/Building/V22Apparel/V22_9311_수동_방적기.asset>)
- [V22_9312_축융_마감대](<Assets/Resources/SO/Building/V22Apparel/V22_9312_축융_마감대.asset>)
- [V22_9313_동력_방적기](<Assets/Resources/SO/Building/V22Apparel/V22_9313_동력_방적기.asset>)
- [V22_9314_동력_직조기](<Assets/Resources/SO/Building/V22Apparel/V22_9314_동력_직조기.asset>)
- [Wall](<Assets/Resources/SO/Building/Wall.asset>)
- [Exterior_DropZone_Building](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_DropZone_Building.asset>)
- [Exterior_DropZone_CeilingFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_DropZone_CeilingFixture.asset>)
- [Exterior_DropZone_FloorOverlay](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_DropZone_FloorOverlay.asset>)
- [Exterior_DropZone_Hallway](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_DropZone_Hallway.asset>)
- [Exterior_DropZone_WallFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_DropZone_WallFixture.asset>)
- [Exterior_Entrance_Building](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_Entrance_Building.asset>)
- [Exterior_Entrance_CeilingFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_Entrance_CeilingFixture.asset>)
- [Exterior_Entrance_FloorOverlay](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_Entrance_FloorOverlay.asset>)
- [Exterior_Entrance_Hallway](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_Entrance_Hallway.asset>)
- [Exterior_Entrance_WallFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_Entrance_WallFixture.asset>)
- [Exterior_ExpeditionStaging_Building](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ExpeditionStaging_Building.asset>)
- [Exterior_ExpeditionStaging_CeilingFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ExpeditionStaging_CeilingFixture.asset>)
- [Exterior_ExpeditionStaging_FloorOverlay](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ExpeditionStaging_FloorOverlay.asset>)
- [Exterior_ExpeditionStaging_Hallway](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ExpeditionStaging_Hallway.asset>)
- [Exterior_ExpeditionStaging_WallFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ExpeditionStaging_WallFixture.asset>)
- [Exterior_GuardPost_Building](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_GuardPost_Building.asset>)
- [Exterior_GuardPost_CeilingFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_GuardPost_CeilingFixture.asset>)
- [Exterior_GuardPost_FloorOverlay](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_GuardPost_FloorOverlay.asset>)
- [Exterior_GuardPost_Hallway](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_GuardPost_Hallway.asset>)
- [Exterior_GuardPost_WallFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_GuardPost_WallFixture.asset>)
- [Exterior_IncidentPoint_Building](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_IncidentPoint_Building.asset>)
- [Exterior_IncidentPoint_CeilingFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_IncidentPoint_CeilingFixture.asset>)
- [Exterior_IncidentPoint_FloorOverlay](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_IncidentPoint_FloorOverlay.asset>)
- [Exterior_IncidentPoint_Hallway](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_IncidentPoint_Hallway.asset>)
- [Exterior_IncidentPoint_WallFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_IncidentPoint_WallFixture.asset>)
- [Exterior_OutdoorRestSpot_Building](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_OutdoorRestSpot_Building.asset>)
- [Exterior_OutdoorRestSpot_CeilingFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_OutdoorRestSpot_CeilingFixture.asset>)
- [Exterior_OutdoorRestSpot_FloorOverlay](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_OutdoorRestSpot_FloorOverlay.asset>)
- [Exterior_OutdoorRestSpot_Hallway](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_OutdoorRestSpot_Hallway.asset>)
- [Exterior_OutdoorRestSpot_WallFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_OutdoorRestSpot_WallFixture.asset>)
- [Exterior_PatrolPoint_Building](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_PatrolPoint_Building.asset>)
- [Exterior_PatrolPoint_CeilingFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_PatrolPoint_CeilingFixture.asset>)
- [Exterior_PatrolPoint_FloorOverlay](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_PatrolPoint_FloorOverlay.asset>)
- [Exterior_PatrolPoint_Hallway](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_PatrolPoint_Hallway.asset>)
- [Exterior_PatrolPoint_WallFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_PatrolPoint_WallFixture.asset>)
- [Exterior_ReceptionPoint_Building](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ReceptionPoint_Building.asset>)
- [Exterior_ReceptionPoint_CeilingFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ReceptionPoint_CeilingFixture.asset>)
- [Exterior_ReceptionPoint_FloorOverlay](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ReceptionPoint_FloorOverlay.asset>)
- [Exterior_ReceptionPoint_Hallway](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ReceptionPoint_Hallway.asset>)
- [Exterior_ReceptionPoint_WallFixture](<Assets/Resources/SO/Buildings/RuntimeArchetypes/Exterior_ReceptionPoint_WallFixture.asset>)
- [WorldFilthWorkTarget](<Assets/Resources/SO/Buildings/RuntimeArchetypes/WorldFilthWorkTarget.asset>)
- [WorldResourceNode](<Assets/Resources/SO/Buildings/RuntimeArchetypes/WorldResourceNode.asset>)
- [building_landmark_accord-hall](<Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_accord-hall.asset>)
- [building_landmark_arcane-spire](<Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_arcane-spire.asset>)
- [building_landmark_lineage-vault](<Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_lineage-vault.asset>)
- [building_landmark_sealed-garden](<Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_sealed-garden.asset>)
- [building_landmark_sovereign-citadel](<Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_sovereign-citadel.asset>)
- [building_landmark_steel-colossus](<Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_steel-colossus.asset>)
- [building_landmark_surface-gate](<Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_surface-gate.asset>)
- [building_landmark_temporal-sanctum](<Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_temporal-sanctum.asset>)
- [building_landmark_truth-observatory](<Assets/Resources/SO/V20/Milestones/Landmarks/building_landmark_truth-observatory.asset>)

</details>

<details>
<summary>CareerPositionDefinitionSO — 6개</summary>

- [career-position_chief-physician](<Assets/Resources/SO/Population/Careers/career-position_chief-physician.asset>)
- [career-position_chief-researcher](<Assets/Resources/SO/Population/Careers/career-position_chief-researcher.asset>)
- [career-position_foreman](<Assets/Resources/SO/Population/Careers/career-position_foreman.asset>)
- [career-position_guard-captain](<Assets/Resources/SO/Population/Careers/career-position_guard-captain.asset>)
- [career-position_mentor](<Assets/Resources/SO/Population/Careers/career-position_mentor.asset>)
- [career-position_steward](<Assets/Resources/SO/Population/Careers/career-position_steward.asset>)

</details>

<details>
<summary>CharacterAcquiredTraitModuleSO — 8개</summary>

- [acquired-trait_module_battle-temper](<Assets/Resources/SO/V25/AcquiredTraits/Modules/acquired-trait_module_battle-temper.asset>)
- [acquired-trait_module_night-vigil](<Assets/Resources/SO/V25/AcquiredTraits/Modules/acquired-trait_module_night-vigil.asset>)
- [acquired-trait_module_ritual-attunement](<Assets/Resources/SO/V25/AcquiredTraits/Modules/acquired-trait_module_ritual-attunement.asset>)
- [acquired-trait_module_scar-memory](<Assets/Resources/SO/V25/AcquiredTraits/Modules/acquired-trait_module_scar-memory.asset>)
- [acquired-trait_module_social-bridge](<Assets/Resources/SO/V25/AcquiredTraits/Modules/acquired-trait_module_social-bridge.asset>)
- [acquired-trait_module_steady-hands](<Assets/Resources/SO/V25/AcquiredTraits/Modules/acquired-trait_module_steady-hands.asset>)
- [acquired-trait_module_survival-instinct](<Assets/Resources/SO/V25/AcquiredTraits/Modules/acquired-trait_module_survival-instinct.asset>)
- [acquired-trait_module_work-rhythm](<Assets/Resources/SO/V25/AcquiredTraits/Modules/acquired-trait_module_work-rhythm.asset>)

</details>

<details>
<summary>CharacterAcquiredTraitSettingsSO — 1개</summary>

- [acquired-trait-settings](<Assets/Resources/SO/V25/AcquiredTraits/acquired-trait-settings.asset>)

</details>

<details>
<summary>CharacterAiNaturalnessSettingsSO — 1개</summary>

- [CharacterAiNaturalnessSettings](<Assets/Resources/SO/AI/CharacterAiNaturalnessSettings.asset>)

</details>

<details>
<summary>CharacterAiPerfSettingsSO — 1개</summary>

- [CharacterAiPerfSettings](<Assets/Resources/Config/CharacterAiPerfSettings.asset>)

</details>

<details>
<summary>CharacterAmbitionDefinitionSO — 18개</summary>

- [ambition_avenge-fallen](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_avenge-fallen.asset>)
- [ambition_become-steward](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_become-steward.asset>)
- [ambition_break-debt](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_break-debt.asset>)
- [ambition_build-clinic](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_build-clinic.asset>)
- [ambition_cultural-voice](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_cultural-voice.asset>)
- [ambition_end-hunger](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_end-hunger.asset>)
- [ambition_faction-champion](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_faction-champion.asset>)
- [ambition_faction-peacemaker](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_faction-peacemaker.asset>)
- [ambition_field-scholar](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_field-scholar.asset>)
- [ambition_find-origin](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_find-origin.asset>)
- [ambition_guard-captain](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_guard-captain.asset>)
- [ambition_map-depths](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_map-depths.asset>)
- [ambition_master-healer](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_master-healer.asset>)
- [ambition_master-smith](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_master-smith.asset>)
- [ambition_mentor-generation](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_mentor-generation.asset>)
- [ambition_raise-family](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_raise-family.asset>)
- [ambition_restore-lineage](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_restore-lineage.asset>)
- [ambition_worthy-guardian](<Assets/Resources/SO/V20/Narrative/Ambitions/ambition_worthy-guardian.asset>)

</details>

<details>
<summary>CharacterBackgroundDefinitionSO — 12개</summary>

- [background_archive-caretaker](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_archive-caretaker.asset>)
- [background_camp-refugee](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_camp-refugee.asset>)
- [background_caravan-artisan](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_caravan-artisan.asset>)
- [background_deserter](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_deserter.asset>)
- [background_exiled-heir](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_exiled-heir.asset>)
- [background_guild-apprentice](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_guild-apprentice.asset>)
- [background_inn-servant](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_inn-servant.asset>)
- [background_mine-bondworker](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_mine-bondworker.asset>)
- [background_monastery-novice](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_monastery-novice.asset>)
- [background_ritual-test-subject](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_ritual-test-subject.asset>)
- [background_siege-survivor](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_siege-survivor.asset>)
- [background_tenant-farmer](<Assets/Resources/SO/V20/Narrative/Backgrounds/background_tenant-farmer.asset>)

</details>

<details>
<summary>CharacterFunctionalCapacityDefinitionSO — 14개</summary>

- [capacity_arcane-conduction](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_arcane-conduction.asset>)
- [capacity_auditory-sensing](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_auditory-sensing.asset>)
- [capacity_communication](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_communication.asset>)
- [capacity_immune-defense](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_immune-defense.asset>)
- [capacity_intake-processing](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_intake-processing.asset>)
- [capacity_mental-maintenance](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_mental-maintenance.asset>)
- [capacity_physical-mobility](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_physical-mobility.asset>)
- [capacity_physical-power](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_physical-power.asset>)
- [capacity_power-circulation](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_power-circulation.asset>)
- [capacity_precision-manipulation](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_precision-manipulation.asset>)
- [capacity_purification-processing](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_purification-processing.asset>)
- [capacity_respiratory-exchange](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_respiratory-exchange.asset>)
- [capacity_visual-discernment](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_visual-discernment.asset>)
- [capacity_vitality-response](<Assets/Resources/SO/V27/CharacterPerformance/Capacities/capacity_vitality-response.asset>)

</details>

<details>
<summary>CharacterPerformanceFormulaDefinitionSO — 107개</summary>

- [performance_combat_arcane-power](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_arcane-power.asset>)
- [performance_combat_defense-reaction](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_defense-reaction.asset>)
- [performance_combat_evasion](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_evasion.asset>)
- [performance_combat_mana-recovery](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_mana-recovery.asset>)
- [performance_combat_melee-hit](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_melee-hit.asset>)
- [performance_combat_melee-power](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_melee-power.asset>)
- [performance_combat_movement](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_movement.asset>)
- [performance_combat_ranged-hit](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_ranged-hit.asset>)
- [performance_composite_mobility-execution](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_composite_mobility-execution.asset>)
- [performance_composite_precision-execution](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_composite_precision-execution.asset>)
- [performance_composite_recovery-foundation](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_composite_recovery-foundation.asset>)
- [performance_composite_situational-awareness](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_composite_situational-awareness.asset>)
- [performance_composite_sustained-execution](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_composite_sustained-execution.asset>)
- [performance_medical_complication-risk](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_complication-risk.asset>)
- [performance_medical_disease-recovery](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_disease-recovery.asset>)
- [performance_medical_disease-resistance](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_disease-resistance.asset>)
- [performance_medical_immunity-gain](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_immunity-gain.asset>)
- [performance_medical_immunity-retention](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_immunity-retention.asset>)
- [performance_medical_surgery-speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_surgery-speed.asset>)
- [performance_medical_surgery-success](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_surgery-success.asset>)
- [performance_medical_treatment-efficiency](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_treatment-efficiency.asset>)
- [performance_medical_treatment-speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_treatment-speed.asset>)
- [performance_medical_wound-recovery](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_medical_wound-recovery.asset>)
- [performance_social_crowd-sensitivity](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_social_crowd-sensitivity.asset>)
- [performance_social_negative-mood-duration](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_social_negative-mood-duration.asset>)
- [performance_social_negotiation](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_social_negotiation.asset>)
- [performance_social_relationship-recovery](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_social_relationship-recovery.asset>)
- [performance_social_spending](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_social_spending.asset>)
- [performance_social_wait-patience](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_social_wait-patience.asset>)
- [performance_survival_alarm-response](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_alarm-response.asset>)
- [performance_survival_cold-exposure](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_cold-exposure.asset>)
- [performance_survival_fatigue-rate](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_fatigue-rate.asset>)
- [performance_survival_food-consumption](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_food-consumption.asset>)
- [performance_survival_food-poisoning](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_food-poisoning.asset>)
- [performance_survival_haul-capacity](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_haul-capacity.asset>)
- [performance_survival_heat-exposure](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_heat-exposure.asset>)
- [performance_survival_movement-speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_movement-speed.asset>)
- [performance_survival_nutrition-efficiency](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_nutrition-efficiency.asset>)
- [performance_survival_risk-detection](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_survival_risk-detection.asset>)
- [performance_work_animal-care_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_animal-care_accident.asset>)
- [performance_work_animal-care_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_animal-care_speed.asset>)
- [performance_work_butcher_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_butcher_accident.asset>)
- [performance_work_butcher_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_butcher_speed.asset>)
- [performance_work_clean_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_clean_accident.asset>)
- [performance_work_clean_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_clean_speed.asset>)
- [performance_work_construct_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_construct_accident.asset>)
- [performance_work_construct_quality](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_construct_quality.asset>)
- [performance_work_construct_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_construct_speed.asset>)
- [performance_work_cook_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_cook_accident.asset>)
- [performance_work_cook_quality](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_cook_quality.asset>)
- [performance_work_cook_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_cook_speed.asset>)
- [performance_work_craft_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_craft_accident.asset>)
- [performance_work_craft_quality](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_craft_quality.asset>)
- [performance_work_craft_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_craft_speed.asset>)
- [performance_work_dismantle_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_dismantle_accident.asset>)
- [performance_work_dismantle_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_dismantle_speed.asset>)
- [performance_work_draw-water_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_draw-water_accident.asset>)
- [performance_work_draw-water_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_draw-water_speed.asset>)
- [performance_work_gather_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_gather_accident.asset>)
- [performance_work_gather_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_gather_speed.asset>)
- [performance_work_grand-project_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_grand-project_accident.asset>)
- [performance_work_grand-project_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_grand-project_speed.asset>)
- [performance_work_guard_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_guard_accident.asset>)
- [performance_work_guard_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_guard_speed.asset>)
- [performance_work_harvest_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_harvest_accident.asset>)
- [performance_work_harvest_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_harvest_speed.asset>)
- [performance_work_harvest_yield](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_harvest_yield.asset>)
- [performance_work_haul_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_haul_accident.asset>)
- [performance_work_haul_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_haul_speed.asset>)
- [performance_work_hunt_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_hunt_accident.asset>)
- [performance_work_hunt_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_hunt_speed.asset>)
- [performance_work_logging_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_logging_accident.asset>)
- [performance_work_logging_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_logging_speed.asset>)
- [performance_work_operate_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_operate_accident.asset>)
- [performance_work_operate_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_operate_speed.asset>)
- [performance_work_perform_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_perform_accident.asset>)
- [performance_work_perform_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_perform_speed.asset>)
- [performance_work_plumbing_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_plumbing_accident.asset>)
- [performance_work_plumbing_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_plumbing_speed.asset>)
- [performance_work_quarry_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_quarry_accident.asset>)
- [performance_work_quarry_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_quarry_speed.asset>)
- [performance_work_reception_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_reception_accident.asset>)
- [performance_work_reception_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_reception_speed.asset>)
- [performance_work_refuel_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_refuel_accident.asset>)
- [performance_work_refuel_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_refuel_speed.asset>)
- [performance_work_repair_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_repair_accident.asset>)
- [performance_work_repair_quality](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_repair_quality.asset>)
- [performance_work_repair_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_repair_speed.asset>)
- [performance_work_rescue_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_rescue_accident.asset>)
- [performance_work_rescue_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_rescue_speed.asset>)
- [performance_work_research_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_research_accident.asset>)
- [performance_work_research_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_research_speed.asset>)
- [performance_work_rest_fatigue-recovery](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_rest_fatigue-recovery.asset>)
- [performance_work_rest_sleep-recovery](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_rest_sleep-recovery.asset>)
- [performance_work_rest_wound-recovery](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_rest_wound-recovery.asset>)
- [performance_work_restock_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_restock_accident.asset>)
- [performance_work_restock_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_restock_speed.asset>)
- [performance_work_sow_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_sow_accident.asset>)
- [performance_work_sow_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_sow_speed.asset>)
- [performance_work_surgery_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_surgery_accident.asset>)
- [performance_work_surgery_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_surgery_speed.asset>)
- [performance_work_threat-mitigation_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_threat-mitigation_accident.asset>)
- [performance_work_threat-mitigation_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_threat-mitigation_speed.asset>)
- [performance_work_treat_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_treat_accident.asset>)
- [performance_work_treat_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_treat_speed.asset>)
- [performance_work_warden_accident](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_warden_accident.asset>)
- [performance_work_warden_speed](<Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_work_warden_speed.asset>)

</details>

<details>
<summary>CharacterSO — 16개</summary>

- [Customer_Orc](<Assets/Resources/SO/Character/Customer_Orc.asset>)
- [Customer_Vampire](<Assets/Resources/SO/Character/Customer_Vampire.asset>)
- [Customer_Beastkin](<Assets/Resources/SO/Character/ExpandedSpecies/Customer_Beastkin.asset>)
- [Customer_Demon](<Assets/Resources/SO/Character/ExpandedSpecies/Customer_Demon.asset>)
- [Customer_Golem](<Assets/Resources/SO/Character/ExpandedSpecies/Customer_Golem.asset>)
- [Customer_Harpy](<Assets/Resources/SO/Character/ExpandedSpecies/Customer_Harpy.asset>)
- [Customer_Human](<Assets/Resources/SO/Character/ExpandedSpecies/Customer_Human.asset>)
- [Customer_Kobold](<Assets/Resources/SO/Character/ExpandedSpecies/Customer_Kobold.asset>)
- [Customer_Myconid](<Assets/Resources/SO/Character/ExpandedSpecies/Customer_Myconid.asset>)
- [Intruder_Breakthrough](<Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset>)
- [New Character SO](<Assets/Resources/SO/Character/New Character SO.asset>)
- [Owner_Orc](<Assets/Resources/SO/Character/Owners/Owner_Orc.asset>)
- [Owner_Slime](<Assets/Resources/SO/Character/Owners/Owner_Slime.asset>)
- [Owner_Vampire](<Assets/Resources/SO/Character/Owners/Owner_Vampire.asset>)
- [Staff_Generic](<Assets/Resources/SO/Character/Staff/Staff_Generic.asset>)
- [TestCharacter](<Assets/Scripts/Services/Character/SO/TestCharacter.asset>)

</details>

<details>
<summary>CharacterSkillSystemSettingsSO — 1개</summary>

- [CharacterSkillSystemSettings](<Assets/Resources/SO/Character/CharacterSkillSystemSettings.asset>)

</details>

<details>
<summary>CharacterSpeciesSO — 11개</summary>

- [Species_Adventurer](<Assets/Resources/SO/Character/Species/Species_Adventurer.asset>)
- [Species_Beastkin](<Assets/Resources/SO/Character/Species/Species_Beastkin.asset>)
- [Species_Demon](<Assets/Resources/SO/Character/Species/Species_Demon.asset>)
- [Species_Golem](<Assets/Resources/SO/Character/Species/Species_Golem.asset>)
- [Species_Harpy](<Assets/Resources/SO/Character/Species/Species_Harpy.asset>)
- [Species_Human](<Assets/Resources/SO/Character/Species/Species_Human.asset>)
- [Species_Kobold](<Assets/Resources/SO/Character/Species/Species_Kobold.asset>)
- [Species_Myconid](<Assets/Resources/SO/Character/Species/Species_Myconid.asset>)
- [Species_Orc](<Assets/Resources/SO/Character/Species/Species_Orc.asset>)
- [Species_Slime](<Assets/Resources/SO/Character/Species/Species_Slime.asset>)
- [Species_Vampire](<Assets/Resources/SO/Character/Species/Species_Vampire.asset>)

</details>

<details>
<summary>CharacterStartingHistorySO — 9개</summary>

- [StartingHistory_caravan-broker](<Assets/Resources/SO/Character/StartingProfiles/Histories/StartingHistory_caravan-broker.asset>)
- [StartingHistory_cook](<Assets/Resources/SO/Character/StartingProfiles/Histories/StartingHistory_cook.asset>)
- [StartingHistory_field-medic](<Assets/Resources/SO/Character/StartingProfiles/Histories/StartingHistory_field-medic.asset>)
- [StartingHistory_frontier-farmer](<Assets/Resources/SO/Character/StartingProfiles/Histories/StartingHistory_frontier-farmer.asset>)
- [StartingHistory_garrison-guard](<Assets/Resources/SO/Character/StartingProfiles/Histories/StartingHistory_garrison-guard.asset>)
- [StartingHistory_mason](<Assets/Resources/SO/Character/StartingProfiles/Histories/StartingHistory_mason.asset>)
- [StartingHistory_records-clerk](<Assets/Resources/SO/Character/StartingProfiles/Histories/StartingHistory_records-clerk.asset>)
- [StartingHistory_scout](<Assets/Resources/SO/Character/StartingProfiles/Histories/StartingHistory_scout.asset>)
- [StartingHistory_workshop-artisan](<Assets/Resources/SO/Character/StartingProfiles/Histories/StartingHistory_workshop-artisan.asset>)

</details>

<details>
<summary>CharacterStartingOriginSO — 6개</summary>

- [StartingOrigin_caravan-city](<Assets/Resources/SO/Character/StartingProfiles/Origins/StartingOrigin_caravan-city.asset>)
- [StartingOrigin_frontier](<Assets/Resources/SO/Character/StartingProfiles/Origins/StartingOrigin_frontier.asset>)
- [StartingOrigin_granary-village](<Assets/Resources/SO/Character/StartingProfiles/Origins/StartingOrigin_granary-village.asset>)
- [StartingOrigin_quarry-commune](<Assets/Resources/SO/Character/StartingProfiles/Origins/StartingOrigin_quarry-commune.asset>)
- [StartingOrigin_temple-hospice](<Assets/Resources/SO/Character/StartingProfiles/Origins/StartingOrigin_temple-hospice.asset>)
- [StartingOrigin_workshop-quarter](<Assets/Resources/SO/Character/StartingProfiles/Origins/StartingOrigin_workshop-quarter.asset>)

</details>

<details>
<summary>CharacterTraitSO — 113개</summary>

- [Trait_BigEater](<Assets/Resources/SO/Character/Traits/Trait_BigEater.asset>)
- [Trait_Clean](<Assets/Resources/SO/Character/Traits/Trait_Clean.asset>)
- [Trait_ColdResistantSlime](<Assets/Resources/SO/Character/Traits/Trait_ColdResistantSlime.asset>)
- [Trait_Fighter](<Assets/Resources/SO/Character/Traits/Trait_Fighter.asset>)
- [Trait_Frugal](<Assets/Resources/SO/Character/Traits/Trait_Frugal.asset>)
- [Trait_Impatient](<Assets/Resources/SO/Character/Traits/Trait_Impatient.asset>)
- [Trait_Researcher](<Assets/Resources/SO/Character/Traits/Trait_Researcher.asset>)
- [Trait_Rich](<Assets/Resources/SO/Character/Traits/Trait_Rich.asset>)
- [Trait_Timid](<Assets/Resources/SO/Character/Traits/Trait_Timid.asset>)
- [Trait_200_tireless](<Assets/Resources/SO/V20/Traits/General/Trait_200_tireless.asset>)
- [Trait_201_light-sleeper](<Assets/Resources/SO/V20/Traits/General/Trait_201_light-sleeper.asset>)
- [Trait_202_sure-footed](<Assets/Resources/SO/V20/Traits/General/Trait_202_sure-footed.asset>)
- [Trait_203_delicate-frame](<Assets/Resources/SO/V20/Traits/General/Trait_203_delicate-frame.asset>)
- [Trait_204_scarred](<Assets/Resources/SO/V20/Traits/General/Trait_204_scarred.asset>)
- [Trait_205_keen-senses](<Assets/Resources/SO/V20/Traits/General/Trait_205_keen-senses.asset>)
- [Trait_206_heavy-build](<Assets/Resources/SO/V20/Traits/General/Trait_206_heavy-build.asset>)
- [Trait_207_quick-healer](<Assets/Resources/SO/V20/Traits/General/Trait_207_quick-healer.asset>)
- [Trait_208_slow-metabolism](<Assets/Resources/SO/V20/Traits/General/Trait_208_slow-metabolism.asset>)
- [Trait_209_hot-blooded](<Assets/Resources/SO/V20/Traits/General/Trait_209_hot-blooded.asset>)
- [Trait_210_sugar-seeker](<Assets/Resources/SO/V20/Traits/General/Trait_210_sugar-seeker.asset>)
- [Trait_211_iron-stomach](<Assets/Resources/SO/V20/Traits/General/Trait_211_iron-stomach.asset>)
- [Trait_212_temperature-sensitive](<Assets/Resources/SO/V20/Traits/General/Trait_212_temperature-sensitive.asset>)
- [Trait_213_ritual-faster](<Assets/Resources/SO/V20/Traits/General/Trait_213_ritual-faster.asset>)
- [Trait_214_salt-craver](<Assets/Resources/SO/V20/Traits/General/Trait_214_salt-craver.asset>)
- [Trait_215_calm](<Assets/Resources/SO/V20/Traits/General/Trait_215_calm.asset>)
- [Trait_216_brooding](<Assets/Resources/SO/V20/Traits/General/Trait_216_brooding.asset>)
- [Trait_217_cheerful](<Assets/Resources/SO/V20/Traits/General/Trait_217_cheerful.asset>)
- [Trait_218_grudge-holder](<Assets/Resources/SO/V20/Traits/General/Trait_218_grudge-holder.asset>)
- [Trait_219_forgiving](<Assets/Resources/SO/V20/Traits/General/Trait_219_forgiving.asset>)
- [Trait_220_anxious-planner](<Assets/Resources/SO/V20/Traits/General/Trait_220_anxious-planner.asset>)
- [Trait_221_thrill-seeker](<Assets/Resources/SO/V20/Traits/General/Trait_221_thrill-seeker.asset>)
- [Trait_222_private](<Assets/Resources/SO/V20/Traits/General/Trait_222_private.asset>)
- [Trait_223_meticulous](<Assets/Resources/SO/V20/Traits/General/Trait_223_meticulous.asset>)
- [Trait_224_improviser](<Assets/Resources/SO/V20/Traits/General/Trait_224_improviser.asset>)
- [Trait_225_routine-keeper](<Assets/Resources/SO/V20/Traits/General/Trait_225_routine-keeper.asset>)
- [Trait_226_teacher](<Assets/Resources/SO/V20/Traits/General/Trait_226_teacher.asset>)
- [Trait_227_salvager](<Assets/Resources/SO/V20/Traits/General/Trait_227_salvager.asset>)
- [Trait_228_night-worker](<Assets/Resources/SO/V20/Traits/General/Trait_228_night-worker.asset>)
- [Trait_229_quality-proud](<Assets/Resources/SO/V20/Traits/General/Trait_229_quality-proud.asset>)
- [Trait_230_fast-learner](<Assets/Resources/SO/V20/Traits/General/Trait_230_fast-learner.asset>)
- [Trait_231_host](<Assets/Resources/SO/V20/Traits/General/Trait_231_host.asset>)
- [Trait_232_blunt](<Assets/Resources/SO/V20/Traits/General/Trait_232_blunt.asset>)
- [Trait_233_mediator](<Assets/Resources/SO/V20/Traits/General/Trait_233_mediator.asset>)
- [Trait_234_loyal](<Assets/Resources/SO/V20/Traits/General/Trait_234_loyal.asset>)
- [Trait_235_status-conscious](<Assets/Resources/SO/V20/Traits/General/Trait_235_status-conscious.asset>)
- [Trait_236_outsider-friendly](<Assets/Resources/SO/V20/Traits/General/Trait_236_outsider-friendly.asset>)
- [Trait_237_clannish](<Assets/Resources/SO/V20/Traits/General/Trait_237_clannish.asset>)
- [Trait_238_merciful](<Assets/Resources/SO/V20/Traits/General/Trait_238_merciful.asset>)
- [Trait_239_ruthless](<Assets/Resources/SO/V20/Traits/General/Trait_239_ruthless.asset>)
- [Trait_240_cautious](<Assets/Resources/SO/V20/Traits/General/Trait_240_cautious.asset>)
- [Trait_241_daredevil](<Assets/Resources/SO/V20/Traits/General/Trait_241_daredevil.asset>)
- [Trait_242_truth-seeker](<Assets/Resources/SO/V20/Traits/General/Trait_242_truth-seeker.asset>)
- [Trait_243_pragmatist](<Assets/Resources/SO/V20/Traits/General/Trait_243_pragmatist.asset>)
- [Trait_244_collector](<Assets/Resources/SO/V20/Traits/General/Trait_244_collector.asset>)
- [Trait_245_compulsive-cleaner](<Assets/Resources/SO/V20/Traits/General/Trait_245_compulsive-cleaner.asset>)
- [Trait_246_storyteller](<Assets/Resources/SO/V20/Traits/General/Trait_246_storyteller.asset>)
- [Trait_247_rough-hands](<Assets/Resources/SO/V26/Traits/Founder/Trait_247_rough-hands.asset>)
- [Trait_248_field-builder](<Assets/Resources/SO/V26/Traits/Founder/Trait_248_field-builder.asset>)
- [Trait_249_fine-craft-sense](<Assets/Resources/SO/V26/Traits/Founder/Trait_249_fine-craft-sense.asset>)
- [Trait_250_palate](<Assets/Resources/SO/V26/Traits/Founder/Trait_250_palate.asset>)
- [Trait_251_numbers-only](<Assets/Resources/SO/V26/Traits/Founder/Trait_251_numbers-only.asset>)
- [Trait_252_careful-hand](<Assets/Resources/SO/V26/Traits/Founder/Trait_252_careful-hand.asset>)
- [Trait_253_sociable](<Assets/Resources/SO/V26/Traits/Founder/Trait_253_sociable.asset>)
- [Trait_254_strength-first](<Assets/Resources/SO/V26/Traits/Founder/Trait_254_strength-first.asset>)
- [Trait_255_range-sense](<Assets/Resources/SO/V26/Traits/Founder/Trait_255_range-sense.asset>)
- [Trait_256_foraging-eye](<Assets/Resources/SO/V26/Traits/Founder/Trait_256_foraging-eye.asset>)
- [Trait_257_tool-affinity](<Assets/Resources/SO/V26/Traits/Founder/Trait_257_tool-affinity.asset>)
- [Trait_258_close-focus](<Assets/Resources/SO/V26/Traits/Founder/Trait_258_close-focus.asset>)
- [Trait_259_talkative](<Assets/Resources/SO/V26/Traits/Founder/Trait_259_talkative.asset>)
- [Trait_300_possessed-inspiration](<Assets/Resources/SO/V26/Traits/Founder/Trait_300_possessed-inspiration.asset>)
- [Trait_301_last-stand](<Assets/Resources/SO/V26/Traits/Founder/Trait_301_last-stand.asset>)
- [Trait_302_forbidden-leap](<Assets/Resources/SO/V26/Traits/Founder/Trait_302_forbidden-leap.asset>)
- [Trait_303_miracle-surgery](<Assets/Resources/SO/V26/Traits/Founder/Trait_303_miracle-surgery.asset>)
- [Trait_304_golden-harvest](<Assets/Resources/SO/V26/Traits/Founder/Trait_304_golden-harvest.asset>)
- [Trait_305_production-limit-break](<Assets/Resources/SO/V26/Traits/Founder/Trait_305_production-limit-break.asset>)
- [Trait_306_arcane-overcharge](<Assets/Resources/SO/V26/Traits/Founder/Trait_306_arcane-overcharge.asset>)
- [Trait_400_fieldwork-aptitude](<Assets/Resources/SO/V26/Traits/Founder/Trait_400_fieldwork-aptitude.asset>)
- [Trait_401_construction-aptitude](<Assets/Resources/SO/V26/Traits/Founder/Trait_401_construction-aptitude.asset>)
- [Trait_402_crafting-aptitude](<Assets/Resources/SO/V26/Traits/Founder/Trait_402_crafting-aptitude.asset>)
- [Trait_403_cooking-aptitude](<Assets/Resources/SO/V26/Traits/Founder/Trait_403_cooking-aptitude.asset>)
- [Trait_404_scholarship-aptitude](<Assets/Resources/SO/V26/Traits/Founder/Trait_404_scholarship-aptitude.asset>)
- [Trait_405_medicine-aptitude](<Assets/Resources/SO/V26/Traits/Founder/Trait_405_medicine-aptitude.asset>)
- [Trait_406_social-aptitude](<Assets/Resources/SO/V26/Traits/Founder/Trait_406_social-aptitude.asset>)
- [Trait_407_melee-aptitude](<Assets/Resources/SO/V26/Traits/Founder/Trait_407_melee-aptitude.asset>)
- [Trait_408_ranged-aptitude](<Assets/Resources/SO/V26/Traits/Founder/Trait_408_ranged-aptitude.asset>)
- [Trait_409_diligent](<Assets/Resources/SO/V26/Traits/Founder/Trait_409_diligent.asset>)
- [Trait_410_agile](<Assets/Resources/SO/V26/Traits/Founder/Trait_410_agile.asset>)
- [Trait_411_prudent](<Assets/Resources/SO/V26/Traits/Founder/Trait_411_prudent.asset>)
- [Trait_412_patient](<Assets/Resources/SO/V26/Traits/Founder/Trait_412_patient.asset>)
- [Trait_413_light-eater](<Assets/Resources/SO/V26/Traits/Founder/Trait_413_light-eater.asset>)
- [Trait_414_focused](<Assets/Resources/SO/V26/Traits/Founder/Trait_414_focused.asset>)
- [Trait_415_brave-heart](<Assets/Resources/SO/V26/Traits/Founder/Trait_415_brave-heart.asset>)
- [Trait_416_quick-study](<Assets/Resources/SO/V26/Traits/Founder/Trait_416_quick-study.asset>)
- [Trait_417_climate-adapted](<Assets/Resources/SO/V26/Traits/Founder/Trait_417_climate-adapted.asset>)
- [Trait_500_fieldwork-insensitive](<Assets/Resources/SO/V26/Traits/Founder/Trait_500_fieldwork-insensitive.asset>)
- [Trait_501_construction-clumsy](<Assets/Resources/SO/V26/Traits/Founder/Trait_501_construction-clumsy.asset>)
- [Trait_502_crafting-clumsy](<Assets/Resources/SO/V26/Traits/Founder/Trait_502_crafting-clumsy.asset>)
- [Trait_503_cooking-clumsy](<Assets/Resources/SO/V26/Traits/Founder/Trait_503_cooking-clumsy.asset>)
- [Trait_504_scholarship-insensitive](<Assets/Resources/SO/V26/Traits/Founder/Trait_504_scholarship-insensitive.asset>)
- [Trait_505_medicine-clumsy](<Assets/Resources/SO/V26/Traits/Founder/Trait_505_medicine-clumsy.asset>)
- [Trait_506_shy](<Assets/Resources/SO/V26/Traits/Founder/Trait_506_shy.asset>)
- [Trait_507_melee-clumsy](<Assets/Resources/SO/V26/Traits/Founder/Trait_507_melee-clumsy.asset>)
- [Trait_508_ranged-clumsy](<Assets/Resources/SO/V26/Traits/Founder/Trait_508_ranged-clumsy.asset>)
- [Trait_509_lazy](<Assets/Resources/SO/V26/Traits/Founder/Trait_509_lazy.asset>)
- [Trait_510_slow-footed](<Assets/Resources/SO/V26/Traits/Founder/Trait_510_slow-footed.asset>)
- [Trait_511_careless](<Assets/Resources/SO/V26/Traits/Founder/Trait_511_careless.asset>)
- [Trait_512_poor-waiter](<Assets/Resources/SO/V26/Traits/Founder/Trait_512_poor-waiter.asset>)
- [Trait_513_overeater](<Assets/Resources/SO/V26/Traits/Founder/Trait_513_overeater.asset>)
- [Trait_514_distracted](<Assets/Resources/SO/V26/Traits/Founder/Trait_514_distracted.asset>)
- [Trait_515_combat-passive](<Assets/Resources/SO/V26/Traits/Founder/Trait_515_combat-passive.asset>)
- [Trait_516_slow-study](<Assets/Resources/SO/V26/Traits/Founder/Trait_516_slow-study.asset>)
- [Trait_517_cold-vulnerable](<Assets/Resources/SO/V26/Traits/Founder/Trait_517_cold-vulnerable.asset>)
- [Trait_518_heat-vulnerable](<Assets/Resources/SO/V26/Traits/Founder/Trait_518_heat-vulnerable.asset>)

</details>

<details>
<summary>ClimateZoneDefinitionSO — 5개</summary>

- [ClimateZone_ember-wastes](<Assets/Resources/SO/World/Climate/ClimateZone_ember-wastes.asset>)
- [ClimateZone_frost-rift](<Assets/Resources/SO/World/Climate/ClimateZone_frost-rift.asset>)
- [ClimateZone_mana-stormlands](<Assets/Resources/SO/World/Climate/ClimateZone_mana-stormlands.asset>)
- [ClimateZone_mycelial-depths](<Assets/Resources/SO/World/Climate/ClimateZone_mycelial-depths.asset>)
- [ClimateZone_temperate-cave](<Assets/Resources/SO/World/Climate/ClimateZone_temperate-cave.asset>)

</details>

<details>
<summary>CombatArmorSO — 21개</summary>

- [A01_ClothHood](<Assets/Resources/SO/Combat/Equipment/A01_ClothHood.asset>)
- [A02_Gambeson](<Assets/Resources/SO/Combat/Equipment/A02_Gambeson.asset>)
- [A03_LeatherCap](<Assets/Resources/SO/Combat/Equipment/A03_LeatherCap.asset>)
- [A04_LeatherArmor](<Assets/Resources/SO/Combat/Equipment/A04_LeatherArmor.asset>)
- [A05_MailCoif](<Assets/Resources/SO/Combat/Equipment/A05_MailCoif.asset>)
- [A06_MailShirt](<Assets/Resources/SO/Combat/Equipment/A06_MailShirt.asset>)
- [A07_IronHelmet](<Assets/Resources/SO/Combat/Equipment/A07_IronHelmet.asset>)
- [A08_Breastplate](<Assets/Resources/SO/Combat/Equipment/A08_Breastplate.asset>)
- [A09_Brigandine](<Assets/Resources/SO/Combat/Equipment/A09_Brigandine.asset>)
- [A10_ScaleCoat](<Assets/Resources/SO/Combat/Equipment/A10_ScaleCoat.asset>)
- [A11_ClosedPlateHelm](<Assets/Resources/SO/Combat/Equipment/A11_ClosedPlateHelm.asset>)
- [A12_ArticulatedPlate](<Assets/Resources/SO/Combat/Equipment/A12_ArticulatedPlate.asset>)
- [A13_BlastCoat](<Assets/Resources/SO/Combat/Equipment/A13_BlastCoat.asset>)
- [A14_SmokeHood](<Assets/Resources/SO/Combat/Equipment/A14_SmokeHood.asset>)
- [A15_PoweredHarness](<Assets/Resources/SO/Combat/Equipment/A15_PoweredHarness.asset>)
- [A16_RuneWardMail](<Assets/Resources/SO/Combat/Equipment/A16_RuneWardMail.asset>)
- [A17_BlacksteelCarapace](<Assets/Resources/SO/Combat/Equipment/A17_BlacksteelCarapace.asset>)
- [A18_JackOfPlates](<Assets/Resources/SO/Combat/Equipment/A18_JackOfPlates.asset>)
- [A19_PowderCuirass](<Assets/Resources/SO/Combat/Equipment/A19_PowderCuirass.asset>)
- [A20_PaddedHood](<Assets/Resources/SO/Combat/Equipment/A20_PaddedHood.asset>)
- [A21_HardenedLeatherCoat](<Assets/Resources/SO/Combat/Equipment/A21_HardenedLeatherCoat.asset>)

</details>

<details>
<summary>CombatShieldSO — 9개</summary>

- [S01_WoodShield](<Assets/Resources/SO/Combat/Equipment/S01_WoodShield.asset>)
- [S02_IronShield](<Assets/Resources/SO/Combat/Equipment/S02_IronShield.asset>)
- [S03_Buckler](<Assets/Resources/SO/Combat/Equipment/S03_Buckler.asset>)
- [S04_TowerShield](<Assets/Resources/SO/Combat/Equipment/S04_TowerShield.asset>)
- [S05_RuneShield](<Assets/Resources/SO/Combat/Equipment/S05_RuneShield.asset>)
- [S06_PoweredShield](<Assets/Resources/SO/Combat/Equipment/S06_PoweredShield.asset>)
- [S07_BlacksteelShield](<Assets/Resources/SO/Combat/Equipment/S07_BlacksteelShield.asset>)
- [S08_ManaBuckler](<Assets/Resources/SO/Combat/Equipment/S08_ManaBuckler.asset>)
- [S09_Pavise](<Assets/Resources/SO/Combat/Equipment/S09_Pavise.asset>)

</details>

<details>
<summary>CombatWeaponSO — 31개</summary>

- [W01_Dagger](<Assets/Resources/SO/Combat/Equipment/W01_Dagger.asset>)
- [W02_Longsword](<Assets/Resources/SO/Combat/Equipment/W02_Longsword.asset>)
- [W03_Spear](<Assets/Resources/SO/Combat/Equipment/W03_Spear.asset>)
- [W04_Mace](<Assets/Resources/SO/Combat/Equipment/W04_Mace.asset>)
- [W05_Shortbow](<Assets/Resources/SO/Combat/Equipment/W05_Shortbow.asset>)
- [W06_Longbow](<Assets/Resources/SO/Combat/Equipment/W06_Longbow.asset>)
- [W07_Crossbow](<Assets/Resources/SO/Combat/Equipment/W07_Crossbow.asset>)
- [W08_Javelin](<Assets/Resources/SO/Combat/Equipment/W08_Javelin.asset>)
- [W09_ThrowingAxe](<Assets/Resources/SO/Combat/Equipment/W09_ThrowingAxe.asset>)
- [W10_Falchion](<Assets/Resources/SO/Combat/Equipment/W10_Falchion.asset>)
- [W11_Warhammer](<Assets/Resources/SO/Combat/Equipment/W11_Warhammer.asset>)
- [W12_Halberd](<Assets/Resources/SO/Combat/Equipment/W12_Halberd.asset>)
- [W13_Greatsword](<Assets/Resources/SO/Combat/Equipment/W13_Greatsword.asset>)
- [W14_CompositeBow](<Assets/Resources/SO/Combat/Equipment/W14_CompositeBow.asset>)
- [W15_WindlassCrossbow](<Assets/Resources/SO/Combat/Equipment/W15_WindlassCrossbow.asset>)
- [W16_Handgonne](<Assets/Resources/SO/Combat/Equipment/W16_Handgonne.asset>)
- [W17_MatchlockPistol](<Assets/Resources/SO/Combat/Equipment/W17_MatchlockPistol.asset>)
- [W18_Arquebus](<Assets/Resources/SO/Combat/Equipment/W18_Arquebus.asset>)
- [W19_SiegeArbalest](<Assets/Resources/SO/Combat/Equipment/W19_SiegeArbalest.asset>)
- [W20_RuneBlade](<Assets/Resources/SO/Combat/Equipment/W20_RuneBlade.asset>)
- [W21_ManaLance](<Assets/Resources/SO/Combat/Equipment/W21_ManaLance.asset>)
- [W22_RepeatingCrossbow](<Assets/Resources/SO/Combat/Equipment/W22_RepeatingCrossbow.asset>)
- [W23_MatchlockLongGun](<Assets/Resources/SO/Combat/Equipment/W23_MatchlockLongGun.asset>)
- [W24_SniperArquebus](<Assets/Resources/SO/Combat/Equipment/W24_SniperArquebus.asset>)
- [W25_HeavyMatchlock](<Assets/Resources/SO/Combat/Equipment/W25_HeavyMatchlock.asset>)
- [W26_Shotgun](<Assets/Resources/SO/Combat/Equipment/W26_Shotgun.asset>)
- [W27_PoweredStrikingGauntlet](<Assets/Resources/SO/Combat/Equipment/W27_PoweredStrikingGauntlet.asset>)
- [W28_BlacksteelPoleaxe](<Assets/Resources/SO/Combat/Equipment/W28_BlacksteelPoleaxe.asset>)
- [W29_Estoc](<Assets/Resources/SO/Combat/Equipment/W29_Estoc.asset>)
- [W30_Pollaxe](<Assets/Resources/SO/Combat/Equipment/W30_Pollaxe.asset>)
- [W31_RuneBow](<Assets/Resources/SO/Combat/Equipment/W31_RuneBow.asset>)

</details>

<details>
<summary>ConsiderationCanLookAround — 1개</summary>

- [CanLookAround](<Assets/Resources/SO/AI/Consideration/CanLookAround.asset>)

</details>

<details>
<summary>ConsiderationFacilityNeed — 6개</summary>

- [NeedHygiene](<Assets/Resources/SO/AI/Consideration/NeedHygiene.asset>)
- [NeedMeal](<Assets/Resources/SO/AI/Consideration/NeedMeal.asset>)
- [NeedPurchase](<Assets/Resources/SO/AI/Consideration/NeedPurchase.asset>)
- [NeedRecreation](<Assets/Resources/SO/AI/Consideration/NeedRecreation.asset>)
- [NeedRest](<Assets/Resources/SO/AI/Consideration/NeedRest.asset>)
- [NeedToilet](<Assets/Resources/SO/AI/Consideration/NeedToilet.asset>)

</details>

<details>
<summary>ConsiderationIsVisitable — 1개</summary>

- [IsThereVisitable](<Assets/Resources/SO/AI/Consideration/IsThereVisitable.asset>)

</details>

<details>
<summary>ConsiderationRandom — 2개</summary>

- [RandomForShopping 1](<Assets/Resources/SO/AI/Consideration/RandomForShopping 1.asset>)
- [RandomForShopping](<Assets/Resources/SO/AI/Consideration/RandomForShopping.asset>)

</details>

<details>
<summary>ConsiderationShoppingCount — 1개</summary>

- [New Consideration Shopping Count](<Assets/Resources/SO/AI/Consideration/New Consideration Shopping Count.asset>)

</details>

<details>
<summary>ConsiderationShouldExitDungeon — 1개</summary>

- [ShouldExitDungeon](<Assets/Resources/SO/AI/Consideration/ShouldExitDungeon.asset>)

</details>

<details>
<summary>ConsiderationStat — 4개</summary>

- [Fun](<Assets/Resources/SO/AI/Consideration/Fun.asset>)
- [Hunger](<Assets/Resources/SO/AI/Consideration/Hunger.asset>)
- [Mood](<Assets/Resources/SO/AI/Consideration/Mood.asset>)
- [Sleep](<Assets/Resources/SO/AI/Consideration/Sleep.asset>)

</details>

<details>
<summary>ConsiderationWorkNeed — 1개</summary>

- [NeedWork](<Assets/Resources/SO/AI/Consideration/NeedWork.asset>)

</details>

<details>
<summary>CraftMaterialDefinitionSO — 12개</summary>

- [material_blacksteel](<Assets/Resources/SO/Economy/Materials/material_blacksteel.asset>)
- [material_bone](<Assets/Resources/SO/Economy/Materials/material_bone.asset>)
- [material_cloth](<Assets/Resources/SO/Economy/Materials/material_cloth.asset>)
- [material_dreamweave](<Assets/Resources/SO/Economy/Materials/material_dreamweave.asset>)
- [material_gold](<Assets/Resources/SO/Economy/Materials/material_gold.asset>)
- [material_iron](<Assets/Resources/SO/Economy/Materials/material_iron.asset>)
- [material_leather](<Assets/Resources/SO/Economy/Materials/material_leather.asset>)
- [material_rune_leather](<Assets/Resources/SO/Economy/Materials/material_rune_leather.asset>)
- [material_steel](<Assets/Resources/SO/Economy/Materials/material_steel.asset>)
- [material_stone](<Assets/Resources/SO/Economy/Materials/material_stone.asset>)
- [material_wood](<Assets/Resources/SO/Economy/Materials/material_wood.asset>)
- [material_wool](<Assets/Resources/SO/Economy/Materials/material_wool.asset>)

</details>

<details>
<summary>CropDefinitionSO — 12개</summary>

- [crop_ember-cotton](<Assets/Resources/SO/Economy/Crops/V22Textiles/crop_ember-cotton.asset>)
- [crop_frost-flax](<Assets/Resources/SO/Economy/Crops/V22Textiles/crop_frost-flax.asset>)
- [crop_mire-reed](<Assets/Resources/SO/Economy/Crops/V22Textiles/crop_mire-reed.asset>)
- [crop_spore-hemp](<Assets/Resources/SO/Economy/Crops/V22Textiles/crop_spore-hemp.asset>)
- [crop_bloodleaf](<Assets/Resources/SO/Economy/Crops/crop_bloodleaf.asset>)
- [crop_cave_mushroom](<Assets/Resources/SO/Economy/Crops/crop_cave_mushroom.asset>)
- [crop_dreamleaf](<Assets/Resources/SO/Economy/Crops/crop_dreamleaf.asset>)
- [crop_ember_root](<Assets/Resources/SO/Economy/Crops/crop_ember_root.asset>)
- [crop_moonflower](<Assets/Resources/SO/Economy/Crops/crop_moonflower.asset>)
- [crop_night_grape](<Assets/Resources/SO/Economy/Crops/crop_night_grape.asset>)
- [crop_shade_fiber](<Assets/Resources/SO/Economy/Crops/crop_shade_fiber.asset>)
- [crop_twilight_grain](<Assets/Resources/SO/Economy/Crops/crop_twilight_grain.asset>)

</details>

<details>
<summary>CropGenomeDefinitionSO — 32개</summary>

- [Genome_bloodleaf](<Assets/Resources/SO/Economy/CropGenomes/Genome_bloodleaf.asset>)
- [Genome_cave-mushroom](<Assets/Resources/SO/Economy/CropGenomes/Genome_cave-mushroom.asset>)
- [Genome_dreamleaf](<Assets/Resources/SO/Economy/CropGenomes/Genome_dreamleaf.asset>)
- [Genome_ember-root](<Assets/Resources/SO/Economy/CropGenomes/Genome_ember-root.asset>)
- [Genome_moonflower](<Assets/Resources/SO/Economy/CropGenomes/Genome_moonflower.asset>)
- [Genome_night-grape](<Assets/Resources/SO/Economy/CropGenomes/Genome_night-grape.asset>)
- [Genome_shade-fiber](<Assets/Resources/SO/Economy/CropGenomes/Genome_shade-fiber.asset>)
- [Genome_twilight-grain](<Assets/Resources/SO/Economy/CropGenomes/Genome_twilight-grain.asset>)
- [genome_ember-cotton_base](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_ember-cotton_base.asset>)
- [genome_ember-cotton_bulk](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_ember-cotton_bulk.asset>)
- [genome_ember-cotton_climate](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_ember-cotton_climate.asset>)
- [genome_frost-flax_base](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_frost-flax_base.asset>)
- [genome_frost-flax_bulk](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_frost-flax_bulk.asset>)
- [genome_frost-flax_climate](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_frost-flax_climate.asset>)
- [genome_mire-reed_base](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_mire-reed_base.asset>)
- [genome_mire-reed_bulk](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_mire-reed_bulk.asset>)
- [genome_mire-reed_climate](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_mire-reed_climate.asset>)
- [genome_spore-hemp_base](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_spore-hemp_base.asset>)
- [genome_spore-hemp_bulk](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_spore-hemp_bulk.asset>)
- [genome_spore-hemp_climate](<Assets/Resources/SO/Economy/CropGenomes/V22Textiles/genome_spore-hemp_climate.asset>)
- [genome_bloodleaf_heat](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_bloodleaf_heat.asset>)
- [genome_cave-mushroom_dry](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_cave-mushroom_dry.asset>)
- [genome_dreamleaf_cold](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_dreamleaf_cold.asset>)
- [genome_ember-root_cold](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_ember-root_cold.asset>)
- [genome_ember-root_heavy](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_ember-root_heavy.asset>)
- [genome_moonflower_heat](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_moonflower_heat.asset>)
- [genome_night-grape_cluster](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_night-grape_cluster.asset>)
- [genome_night-grape_cold](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_night-grape_cold.asset>)
- [genome_shade-fiber_heat](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_shade-fiber_heat.asset>)
- [genome_shade-fiber_long](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_shade-fiber_long.asset>)
- [genome_twilight-grain_abundant](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_twilight-grain_abundant.asset>)
- [genome_twilight-grain_frost](<Assets/Resources/SO/V20/Ecology/Cultivars/genome_twilight-grain_frost.asset>)

</details>

<details>
<summary>CulturalPracticeDefinitionSO — 20개</summary>

- [practice_adventurer-name-token](<Assets/Resources/SO/V20/Narrative/Practices/practice_adventurer-name-token.asset>)
- [practice_adventurer-return-table](<Assets/Resources/SO/V20/Narrative/Practices/practice_adventurer-return-table.asset>)
- [practice_beastkin-pack-meal](<Assets/Resources/SO/V20/Narrative/Practices/practice_beastkin-pack-meal.asset>)
- [practice_beastkin-scent-vigil](<Assets/Resources/SO/V20/Narrative/Practices/practice_beastkin-scent-vigil.asset>)
- [practice_demon-oath-embers](<Assets/Resources/SO/V20/Narrative/Practices/practice_demon-oath-embers.asset>)
- [practice_demon-third-bell](<Assets/Resources/SO/V20/Narrative/Practices/practice_demon-third-bell.asset>)
- [practice_golem-core-polish](<Assets/Resources/SO/V20/Narrative/Practices/practice_golem-core-polish.asset>)
- [practice_golem-memory-plaque](<Assets/Resources/SO/V20/Narrative/Practices/practice_golem-memory-plaque.asset>)
- [practice_harpy-dawn-chorus](<Assets/Resources/SO/V20/Narrative/Practices/practice_harpy-dawn-chorus.asset>)
- [practice_harpy-first-flight](<Assets/Resources/SO/V20/Narrative/Practices/practice_harpy-first-flight.asset>)
- [practice_kobold-bench-feast](<Assets/Resources/SO/V20/Narrative/Practices/practice_kobold-bench-feast.asset>)
- [practice_kobold-tool-naming](<Assets/Resources/SO/V20/Narrative/Practices/practice_kobold-tool-naming.asset>)
- [practice_myconid-shared-mist](<Assets/Resources/SO/V20/Narrative/Practices/practice_myconid-shared-mist.asset>)
- [practice_myconid-spore-return](<Assets/Resources/SO/V20/Narrative/Practices/practice_myconid-spore-return.asset>)
- [practice_orc-shared-cauldron](<Assets/Resources/SO/V20/Narrative/Practices/practice_orc-shared-cauldron.asset>)
- [practice_orc-weapon-vigil](<Assets/Resources/SO/V20/Narrative/Practices/practice_orc-weapon-vigil.asset>)
- [practice_slime-clear-water](<Assets/Resources/SO/V20/Narrative/Practices/practice_slime-clear-water.asset>)
- [practice_slime-core-ring](<Assets/Resources/SO/V20/Narrative/Practices/practice_slime-core-ring.asset>)
- [practice_vampire-consent-cup](<Assets/Resources/SO/V20/Narrative/Practices/practice_vampire-consent-cup.asset>)
- [practice_vampire-incense-memory](<Assets/Resources/SO/V20/Narrative/Practices/practice_vampire-incense-memory.asset>)

</details>

<details>
<summary>DefenseBurnEffectSO — 2개</summary>

- [P1_FireVent_2_Burn](<Assets/Resources/SO/Defense/Effects/P1/P1_FireVent_2_Burn.asset>)
- [P1_StormFireTrap_2_Burn](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_StormFireTrap_2_Burn.asset>)

</details>

<details>
<summary>DefenseChargeEffectSO — 3개</summary>

- [P1_LightningPillar_2_Charge](<Assets/Resources/SO/Defense/Effects/P1/P1_LightningPillar_2_Charge.asset>)
- [P1_AlarmCoil_2_Charge](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_AlarmCoil_2_Charge.asset>)
- [P1_StormFireTrap_3_Charge](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_StormFireTrap_3_Charge.asset>)

</details>

<details>
<summary>DefenseCorrosionEffectSO — 3개</summary>

- [P1_PoisonPool_2_Corrosion](<Assets/Resources/SO/Defense/Effects/P1/P1_PoisonPool_2_Corrosion.asset>)
- [P1_CorrosionFreezer_2_Corrosion](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_CorrosionFreezer_2_Corrosion.asset>)
- [P1_VenomSpikeTrap_2_Corrosion](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_VenomSpikeTrap_2_Corrosion.asset>)

</details>

<details>
<summary>DefenseDamageEffectSO — 11개</summary>

- [DefenseLinkedDropGate_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/DefenseLinkedDropGate_1_Damage.asset>)
- [DefenseWallLauncher_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/DefenseWallLauncher_1_Damage.asset>)
- [P1_FireVent_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/P1_FireVent_1_Damage.asset>)
- [P1_IceVent_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/P1_IceVent_1_Damage.asset>)
- [P1_LightningPillar_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/P1_LightningPillar_1_Damage.asset>)
- [P1_PoisonPool_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/P1_PoisonPool_1_Damage.asset>)
- [P1_SpikeTrap_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/P1_SpikeTrap_1_Damage.asset>)
- [P1_AlarmCoil_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_AlarmCoil_1_Damage.asset>)
- [P1_CorrosionFreezer_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_CorrosionFreezer_1_Damage.asset>)
- [P1_StormFireTrap_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_StormFireTrap_1_Damage.asset>)
- [P1_VenomSpikeTrap_1_Damage](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_VenomSpikeTrap_1_Damage.asset>)

</details>

<details>
<summary>DefenseGuardAttackEffectSO — 2개</summary>

- [P1_AlarmCoil_3_GuardAttack](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_AlarmCoil_3_GuardAttack.asset>)
- [P1_StormFireTrap_4_GuardAttack](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_StormFireTrap_4_GuardAttack.asset>)

</details>

<details>
<summary>DefenseSlowEffectSO — 3개</summary>

- [DefenseLinkedDropGate_2_Slow](<Assets/Resources/SO/Defense/Effects/P1/DefenseLinkedDropGate_2_Slow.asset>)
- [P1_IceVent_2_Slow](<Assets/Resources/SO/Defense/Effects/P1/P1_IceVent_2_Slow.asset>)
- [P1_CorrosionFreezer_3_Slow](<Assets/Resources/SO/Defense/Effects/P1/Synthesis/P1_CorrosionFreezer_3_Slow.asset>)

</details>

<details>
<summary>DiseaseDefinitionSO — 16개</summary>

- [Disease_condition-core-corrosion](<Assets/Resources/SO/Population/Diseases/Disease_condition-core-corrosion.asset>)
- [Disease_disease-blood-wasting](<Assets/Resources/SO/Population/Diseases/Disease_disease-blood-wasting.asset>)
- [Disease_disease-cave-flu](<Assets/Resources/SO/Population/Diseases/Disease_disease-cave-flu.asset>)
- [Disease_disease-gut-rot](<Assets/Resources/SO/Population/Diseases/Disease_disease-gut-rot.asset>)
- [Disease_disease-mana-pox](<Assets/Resources/SO/Population/Diseases/Disease_disease-mana-pox.asset>)
- [Disease_disease-red-fever](<Assets/Resources/SO/Population/Diseases/Disease_disease-red-fever.asset>)
- [Disease_disease-slime-blight](<Assets/Resources/SO/Population/Diseases/Disease_disease-slime-blight.asset>)
- [Disease_disease-spore-lung](<Assets/Resources/SO/Population/Diseases/Disease_disease-spore-lung.asset>)
- [disease_ash-lung](<Assets/Resources/SO/V20/Ecology/Diseases/disease_ash-lung.asset>)
- [disease_deep-parasitosis](<Assets/Resources/SO/V20/Ecology/Diseases/disease_deep-parasitosis.asset>)
- [disease_dream-mold](<Assets/Resources/SO/V20/Ecology/Diseases/disease_dream-mold.asset>)
- [disease_ember-fever](<Assets/Resources/SO/V20/Ecology/Diseases/disease_ember-fever.asset>)
- [disease_glass-blood](<Assets/Resources/SO/V20/Ecology/Diseases/disease_glass-blood.asset>)
- [disease_green-swarm](<Assets/Resources/SO/V20/Ecology/Diseases/disease_green-swarm.asset>)
- [disease_night-thirst](<Assets/Resources/SO/V20/Ecology/Diseases/disease_night-thirst.asset>)
- [disease_white-spore](<Assets/Resources/SO/V20/Ecology/Diseases/disease_white-spore.asset>)

</details>

<details>
<summary>DungeonAudioLibrarySO — 1개</summary>

- [DungeonAudioLibrary](<Assets/Resources/Audio/DungeonAudioLibrary.asset>)

</details>

<details>
<summary>DungeonFactionDefinitionSO — 12개</summary>

- [Faction_Beastkin_RedPawPost](<Assets/Resources/SO/Factions/Dungeons/Faction_Beastkin_RedPawPost.asset>)
- [Faction_Demon_AshContractCourt](<Assets/Resources/SO/Factions/Dungeons/Faction_Demon_AshContractCourt.asset>)
- [Faction_Golem_StoneveinFoundry](<Assets/Resources/SO/Factions/Dungeons/Faction_Golem_StoneveinFoundry.asset>)
- [Faction_Harpy_StormNest](<Assets/Resources/SO/Factions/Dungeons/Faction_Harpy_StormNest.asset>)
- [Faction_Kobold_DeepGearWarren](<Assets/Resources/SO/Factions/Dungeons/Faction_Kobold_DeepGearWarren.asset>)
- [Faction_Myconid_MycelialGrove](<Assets/Resources/SO/Factions/Dungeons/Faction_Myconid_MycelialGrove.asset>)
- [faction_dungeon_beastkin](<Assets/Resources/SO/Factions/faction_dungeon_beastkin.asset>)
- [faction_dungeon_demon](<Assets/Resources/SO/Factions/faction_dungeon_demon.asset>)
- [faction_dungeon_golem](<Assets/Resources/SO/Factions/faction_dungeon_golem.asset>)
- [faction_dungeon_harpy](<Assets/Resources/SO/Factions/faction_dungeon_harpy.asset>)
- [faction_dungeon_kobold](<Assets/Resources/SO/Factions/faction_dungeon_kobold.asset>)
- [faction_dungeon_myconid](<Assets/Resources/SO/Factions/faction_dungeon_myconid.asset>)

</details>

<details>
<summary>DungeonStory.Content.CoreSession.CoreSessionRulesSO — 1개</summary>

- [CoreSessionRules](<Assets/Resources/SO/Content/CoreSessionRules.asset>)

</details>

<details>
<summary>EndingDefinitionSO — 9개</summary>

- [ending_arcane-ascension](<Assets/Resources/SO/V20/Milestones/Endings/ending_arcane-ascension.asset>)
- [ending_dungeon-sovereignty](<Assets/Resources/SO/V20/Milestones/Endings/ending_dungeon-sovereignty.asset>)
- [ending_eternal-lineage](<Assets/Resources/SO/V20/Milestones/Endings/ending_eternal-lineage.asset>)
- [ending_monster-accord](<Assets/Resources/SO/V20/Milestones/Endings/ending_monster-accord.asset>)
- [ending_sealed-paradise](<Assets/Resources/SO/V20/Milestones/Endings/ending_sealed-paradise.asset>)
- [ending_steel-apotheosis](<Assets/Resources/SO/V20/Milestones/Endings/ending_steel-apotheosis.asset>)
- [ending_surface-hegemony](<Assets/Resources/SO/V20/Milestones/Endings/ending_surface-hegemony.asset>)
- [ending_timeless-sanctuary](<Assets/Resources/SO/V20/Milestones/Endings/ending_timeless-sanctuary.asset>)
- [ending_truth-revealed](<Assets/Resources/SO/V20/Milestones/Endings/ending_truth-revealed.asset>)

</details>

<details>
<summary>EndlessCrisisPolicyDefinitionSO — 1개</summary>

- [EndlessCrisisPolicy](<Assets/Resources/SO/V20/Milestones/Endless/EndlessCrisisPolicy.asset>)

</details>

<details>
<summary>EnemyAbilityDefinitionSO — 18개</summary>

- [enemy-ability_arcane-null](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_arcane-null.asset>)
- [enemy-ability_armor-break](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_armor-break.asset>)
- [enemy-ability_blood-drain](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_blood-drain.asset>)
- [enemy-ability_charge](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_charge.asset>)
- [enemy-ability_core-repair](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_core-repair.asset>)
- [enemy-ability_field-dressing](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_field-dressing.asset>)
- [enemy-ability_frost-bind](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_frost-bind.asset>)
- [enemy-ability_hook-pull](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_hook-pull.asset>)
- [enemy-ability_poison-cloud](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_poison-cloud.asset>)
- [enemy-ability_powder-shot](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_powder-shot.asset>)
- [enemy-ability_rally](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_rally.asset>)
- [enemy-ability_retreat-cover](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_retreat-cover.asset>)
- [enemy-ability_rune-ward](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_rune-ward.asset>)
- [enemy-ability_shield-wall](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_shield-wall.asset>)
- [enemy-ability_smoke-screen](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_smoke-screen.asset>)
- [enemy-ability_summon-minion](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_summon-minion.asset>)
- [enemy-ability_suppressive-volley](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_suppressive-volley.asset>)
- [enemy-ability_truth-seal](<Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_truth-seal.asset>)

</details>

<details>
<summary>EnemyArchetypeDefinitionSO — 36개</summary>

- [enemy_crown-chaplain](<Assets/Resources/SO/V20/Combat/Enemies/enemy_crown-chaplain.asset>)
- [enemy_crown-levy](<Assets/Resources/SO/V20/Combat/Enemies/enemy_crown-levy.asset>)
- [enemy_crown-longbow](<Assets/Resources/SO/V20/Combat/Enemies/enemy_crown-longbow.asset>)
- [enemy_crown-marshal](<Assets/Resources/SO/V20/Combat/Enemies/enemy_crown-marshal.asset>)
- [enemy_crown-tower-guard](<Assets/Resources/SO/V20/Combat/Enemies/enemy_crown-tower-guard.asset>)
- [enemy_inquisition-censor](<Assets/Resources/SO/V20/Combat/Enemies/enemy_inquisition-censor.asset>)
- [enemy_inquisition-high-judge](<Assets/Resources/SO/V20/Combat/Enemies/enemy_inquisition-high-judge.asset>)
- [enemy_inquisition-purifier](<Assets/Resources/SO/V20/Combat/Enemies/enemy_inquisition-purifier.asset>)
- [enemy_inquisition-rune-sniper](<Assets/Resources/SO/V20/Combat/Enemies/enemy_inquisition-rune-sniper.asset>)
- [enemy_inquisition-ward-knight](<Assets/Resources/SO/V20/Combat/Enemies/enemy_inquisition-ward-knight.asset>)
- [enemy_legion-arquebus](<Assets/Resources/SO/V20/Combat/Enemies/enemy_legion-arquebus.asset>)
- [enemy_legion-captain](<Assets/Resources/SO/V20/Combat/Enemies/enemy_legion-captain.asset>)
- [enemy_legion-pavise](<Assets/Resources/SO/V20/Combat/Enemies/enemy_legion-pavise.asset>)
- [enemy_legion-pikeman](<Assets/Resources/SO/V20/Combat/Enemies/enemy_legion-pikeman.asset>)
- [enemy_legion-surgeon](<Assets/Resources/SO/V20/Combat/Enemies/enemy_legion-surgeon.asset>)
- [enemy_merchant-alchemist](<Assets/Resources/SO/V20/Combat/Enemies/enemy_merchant-alchemist.asset>)
- [enemy_merchant-caravan-blade](<Assets/Resources/SO/V20/Combat/Enemies/enemy_merchant-caravan-blade.asset>)
- [enemy_merchant-crossbow](<Assets/Resources/SO/V20/Combat/Enemies/enemy_merchant-crossbow.asset>)
- [enemy_merchant-factor](<Assets/Resources/SO/V20/Combat/Enemies/enemy_merchant-factor.asset>)
- [enemy_merchant-hired-bulwark](<Assets/Resources/SO/V20/Combat/Enemies/enemy_merchant-hired-bulwark.asset>)
- [enemy_neutral-clockwork](<Assets/Resources/SO/V20/Combat/Enemies/enemy_neutral-clockwork.asset>)
- [enemy_neutral-mercenary](<Assets/Resources/SO/V20/Combat/Enemies/enemy_neutral-mercenary.asset>)
- [enemy_neutral-smoke-sapper](<Assets/Resources/SO/V20/Combat/Enemies/enemy_neutral-smoke-sapper.asset>)
- [enemy_rival-packbreaker](<Assets/Resources/SO/V20/Combat/Enemies/enemy_rival-packbreaker.asset>)
- [enemy_rival-pactbinder](<Assets/Resources/SO/V20/Combat/Enemies/enemy_rival-pactbinder.asset>)
- [enemy_rival-siege-frame](<Assets/Resources/SO/V20/Combat/Enemies/enemy_rival-siege-frame.asset>)
- [enemy_rival-spore-shepherd](<Assets/Resources/SO/V20/Combat/Enemies/enemy_rival-spore-shepherd.asset>)
- [enemy_rival-stormcaller](<Assets/Resources/SO/V20/Combat/Enemies/enemy_rival-stormcaller.asset>)
- [enemy_settler-barricadier](<Assets/Resources/SO/V20/Combat/Enemies/enemy_settler-barricadier.asset>)
- [enemy_settler-herbalist](<Assets/Resources/SO/V20/Combat/Enemies/enemy_settler-herbalist.asset>)
- [enemy_settler-hunter](<Assets/Resources/SO/V20/Combat/Enemies/enemy_settler-hunter.asset>)
- [enemy_settler-ranger-chief](<Assets/Resources/SO/V20/Combat/Enemies/enemy_settler-ranger-chief.asset>)
- [enemy_settler-trapper](<Assets/Resources/SO/V20/Combat/Enemies/enemy_settler-trapper.asset>)
- [enemy_truth-archivist](<Assets/Resources/SO/V20/Combat/Enemies/enemy_truth-archivist.asset>)
- [enemy_truth-null-warden](<Assets/Resources/SO/V20/Combat/Enemies/enemy_truth-null-warden.asset>)
- [enemy_truth-sealkeeper](<Assets/Resources/SO/V20/Combat/Enemies/enemy_truth-sealkeeper.asset>)

</details>

<details>
<summary>EnvironmentalWorkwearSO — 4개</summary>

- [ColdWorkSuit](<Assets/Resources/SO/Environment/Workwear/ColdWorkSuit.asset>)
- [HaulingHarness](<Assets/Resources/SO/Environment/Workwear/HaulingHarness.asset>)
- [RuneColdSuit](<Assets/Resources/SO/Environment/Workwear/RuneColdSuit.asset>)
- [SlimeWarmingPad](<Assets/Resources/SO/Environment/Workwear/SlimeWarmingPad.asset>)

</details>

<details>
<summary>EquipmentEvolutionFormulaCatalogSO — 2개</summary>

- [EquipmentEvolutionFormulaCatalog](<Assets/Resources/SO/EquipmentEvolutionFormulaCatalog.asset>)
- [EquipmentEvolutionFormulaCatalogV3](<Assets/Resources/SO/EquipmentEvolutionFormulaCatalogV3.asset>)

</details>

<details>
<summary>EquipmentModuleDefinitionSO — 20개</summary>

- [EM01](<Assets/Resources/SO/Combat/EquipmentModules/EM01.asset>)
- [EM02](<Assets/Resources/SO/Combat/EquipmentModules/EM02.asset>)
- [EM03](<Assets/Resources/SO/Combat/EquipmentModules/EM03.asset>)
- [EM04](<Assets/Resources/SO/Combat/EquipmentModules/EM04.asset>)
- [EM05](<Assets/Resources/SO/Combat/EquipmentModules/EM05.asset>)
- [EM06](<Assets/Resources/SO/Combat/EquipmentModules/EM06.asset>)
- [EM07](<Assets/Resources/SO/Combat/EquipmentModules/EM07.asset>)
- [EM08](<Assets/Resources/SO/Combat/EquipmentModules/EM08.asset>)
- [EM09](<Assets/Resources/SO/Combat/EquipmentModules/EM09.asset>)
- [EM10](<Assets/Resources/SO/Combat/EquipmentModules/EM10.asset>)
- [EM11](<Assets/Resources/SO/Combat/EquipmentModules/EM11.asset>)
- [EM12](<Assets/Resources/SO/Combat/EquipmentModules/EM12.asset>)
- [EM13](<Assets/Resources/SO/Combat/EquipmentModules/EM13.asset>)
- [EM14](<Assets/Resources/SO/Combat/EquipmentModules/EM14.asset>)
- [EM15](<Assets/Resources/SO/Combat/EquipmentModules/EM15.asset>)
- [EM16](<Assets/Resources/SO/Combat/EquipmentModules/EM16.asset>)
- [EM17](<Assets/Resources/SO/Combat/EquipmentModules/EM17.asset>)
- [EM18](<Assets/Resources/SO/Combat/EquipmentModules/EM18.asset>)
- [EM19](<Assets/Resources/SO/Combat/EquipmentModules/EM19.asset>)
- [EM20](<Assets/Resources/SO/Combat/EquipmentModules/EM20.asset>)

</details>

<details>
<summary>FacilityBlueprintSO — 7개</summary>

- [BP_ArcaneBasics](<Assets/Resources/SO/Blueprint/P1/BP_ArcaneBasics.asset>)
- [BP_BattleDining](<Assets/Resources/SO/Blueprint/P1/BP_BattleDining.asset>)
- [BP_CommercialBasics](<Assets/Resources/SO/Blueprint/P1/BP_CommercialBasics.asset>)
- [BP_DefenseBasics](<Assets/Resources/SO/Blueprint/P1/BP_DefenseBasics.asset>)
- [BP_StormFireTrap](<Assets/Resources/SO/Blueprint/P1/BP_StormFireTrap.asset>)
- [BP_SupportBasics](<Assets/Resources/SO/Blueprint/P1/BP_SupportBasics.asset>)
- [BP_TrapChain](<Assets/Resources/SO/Blueprint/P1/BP_TrapChain.asset>)

</details>

<details>
<summary>FacilityCrimeSettingsSO — 1개</summary>

- [FacilityCrimeSettings](<Assets/Resources/Config/FacilityCrimeSettings.asset>)

</details>

<details>
<summary>FacilityEvolutionRecipeSO — 6개</summary>

- [EV_AlchemyBench](<Assets/Resources/SO/FacilityEvolution/P1/EV_AlchemyBench.asset>)
- [EV_ArcheryTarget](<Assets/Resources/SO/FacilityEvolution/P1/EV_ArcheryTarget.asset>)
- [EV_CommercialGrill](<Assets/Resources/SO/FacilityEvolution/P1/EV_CommercialGrill.asset>)
- [EV_RitualFocus](<Assets/Resources/SO/FacilityEvolution/P1/EV_RitualFocus.asset>)
- [EV_SecureDisplay](<Assets/Resources/SO/FacilityEvolution/P1/EV_SecureDisplay.asset>)
- [EV_TacticalTable](<Assets/Resources/SO/FacilityEvolution/P1/EV_TacticalTable.asset>)

</details>

<details>
<summary>FacilityEvolutionRecordTokenDefinitionSO — 9개</summary>

- [RT_CleanServiceStreak](<Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_CleanServiceStreak.asset>)
- [RT_FrequentBrawls](<Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_FrequentBrawls.asset>)
- [RT_GuardRallyPoint](<Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_GuardRallyPoint.asset>)
- [RT_HighMeatConsumption](<Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_HighMeatConsumption.asset>)
- [RT_HighTurnoverService](<Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_HighTurnoverService.asset>)
- [RT_IntruderBloodied](<Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_IntruderBloodied.asset>)
- [RT_MercenaryHangout](<Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_MercenaryHangout.asset>)
- [RT_NoblePatronage](<Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_NoblePatronage.asset>)
- [RT_OutlawRumor](<Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_OutlawRumor.asset>)

</details>

<details>
<summary>FacilitySynthesisRecipeSO — 9개</summary>

- [RS_AlchemyBench](<Assets/Resources/SO/Synthesis/P1/RS_AlchemyBench.asset>)
- [RS_BattleBanner](<Assets/Resources/SO/Synthesis/P1/RS_BattleBanner.asset>)
- [RS_CommercialGrill](<Assets/Resources/SO/Synthesis/P1/RS_CommercialGrill.asset>)
- [RS_LogisticsShelf](<Assets/Resources/SO/Synthesis/P1/RS_LogisticsShelf.asset>)
- [RS_ManaReservoir](<Assets/Resources/SO/Synthesis/P1/RS_ManaReservoir.asset>)
- [RS_PatrolBoard](<Assets/Resources/SO/Synthesis/P1/RS_PatrolBoard.asset>)
- [RS_RitualFocus](<Assets/Resources/SO/Synthesis/P1/RS_RitualFocus.asset>)
- [RS_SecureDisplay](<Assets/Resources/SO/Synthesis/P1/RS_SecureDisplay.asset>)
- [RS_TacticalTable](<Assets/Resources/SO/Synthesis/P1/RS_TacticalTable.asset>)

</details>

<details>
<summary>FactionAllianceBenefitBudgetSO — 1개</summary>

- [FactionAllianceBenefitBudget](<Assets/Resources/SO/Factions/FactionAllianceBenefitBudget.asset>)

</details>

<details>
<summary>FactionArcDefinitionSO — 6개</summary>

- [arc_beastkin](<Assets/Resources/SO/V20/Factions/Arcs/arc_beastkin.asset>)
- [arc_demon](<Assets/Resources/SO/V20/Factions/Arcs/arc_demon.asset>)
- [arc_golem](<Assets/Resources/SO/V20/Factions/Arcs/arc_golem.asset>)
- [arc_harpy](<Assets/Resources/SO/V20/Factions/Arcs/arc_harpy.asset>)
- [arc_kobold](<Assets/Resources/SO/V20/Factions/Arcs/arc_kobold.asset>)
- [arc_myconid](<Assets/Resources/SO/V20/Factions/Arcs/arc_myconid.asset>)

</details>

<details>
<summary>FactionChapterDefinitionSO — 36개</summary>

- [chapter_beastkin_1](<Assets/Resources/SO/V20/Factions/Chapters/chapter_beastkin_1.asset>)
- [chapter_beastkin_2](<Assets/Resources/SO/V20/Factions/Chapters/chapter_beastkin_2.asset>)
- [chapter_beastkin_3](<Assets/Resources/SO/V20/Factions/Chapters/chapter_beastkin_3.asset>)
- [chapter_beastkin_4](<Assets/Resources/SO/V20/Factions/Chapters/chapter_beastkin_4.asset>)
- [chapter_beastkin_5](<Assets/Resources/SO/V20/Factions/Chapters/chapter_beastkin_5.asset>)
- [chapter_beastkin_6](<Assets/Resources/SO/V20/Factions/Chapters/chapter_beastkin_6.asset>)
- [chapter_demon_1](<Assets/Resources/SO/V20/Factions/Chapters/chapter_demon_1.asset>)
- [chapter_demon_2](<Assets/Resources/SO/V20/Factions/Chapters/chapter_demon_2.asset>)
- [chapter_demon_3](<Assets/Resources/SO/V20/Factions/Chapters/chapter_demon_3.asset>)
- [chapter_demon_4](<Assets/Resources/SO/V20/Factions/Chapters/chapter_demon_4.asset>)
- [chapter_demon_5](<Assets/Resources/SO/V20/Factions/Chapters/chapter_demon_5.asset>)
- [chapter_demon_6](<Assets/Resources/SO/V20/Factions/Chapters/chapter_demon_6.asset>)
- [chapter_golem_1](<Assets/Resources/SO/V20/Factions/Chapters/chapter_golem_1.asset>)
- [chapter_golem_2](<Assets/Resources/SO/V20/Factions/Chapters/chapter_golem_2.asset>)
- [chapter_golem_3](<Assets/Resources/SO/V20/Factions/Chapters/chapter_golem_3.asset>)
- [chapter_golem_4](<Assets/Resources/SO/V20/Factions/Chapters/chapter_golem_4.asset>)
- [chapter_golem_5](<Assets/Resources/SO/V20/Factions/Chapters/chapter_golem_5.asset>)
- [chapter_golem_6](<Assets/Resources/SO/V20/Factions/Chapters/chapter_golem_6.asset>)
- [chapter_harpy_1](<Assets/Resources/SO/V20/Factions/Chapters/chapter_harpy_1.asset>)
- [chapter_harpy_2](<Assets/Resources/SO/V20/Factions/Chapters/chapter_harpy_2.asset>)
- [chapter_harpy_3](<Assets/Resources/SO/V20/Factions/Chapters/chapter_harpy_3.asset>)
- [chapter_harpy_4](<Assets/Resources/SO/V20/Factions/Chapters/chapter_harpy_4.asset>)
- [chapter_harpy_5](<Assets/Resources/SO/V20/Factions/Chapters/chapter_harpy_5.asset>)
- [chapter_harpy_6](<Assets/Resources/SO/V20/Factions/Chapters/chapter_harpy_6.asset>)
- [chapter_kobold_1](<Assets/Resources/SO/V20/Factions/Chapters/chapter_kobold_1.asset>)
- [chapter_kobold_2](<Assets/Resources/SO/V20/Factions/Chapters/chapter_kobold_2.asset>)
- [chapter_kobold_3](<Assets/Resources/SO/V20/Factions/Chapters/chapter_kobold_3.asset>)
- [chapter_kobold_4](<Assets/Resources/SO/V20/Factions/Chapters/chapter_kobold_4.asset>)
- [chapter_kobold_5](<Assets/Resources/SO/V20/Factions/Chapters/chapter_kobold_5.asset>)
- [chapter_kobold_6](<Assets/Resources/SO/V20/Factions/Chapters/chapter_kobold_6.asset>)
- [chapter_myconid_1](<Assets/Resources/SO/V20/Factions/Chapters/chapter_myconid_1.asset>)
- [chapter_myconid_2](<Assets/Resources/SO/V20/Factions/Chapters/chapter_myconid_2.asset>)
- [chapter_myconid_3](<Assets/Resources/SO/V20/Factions/Chapters/chapter_myconid_3.asset>)
- [chapter_myconid_4](<Assets/Resources/SO/V20/Factions/Chapters/chapter_myconid_4.asset>)
- [chapter_myconid_5](<Assets/Resources/SO/V20/Factions/Chapters/chapter_myconid_5.asset>)
- [chapter_myconid_6](<Assets/Resources/SO/V20/Factions/Chapters/chapter_myconid_6.asset>)

</details>

<details>
<summary>FactionContractDefinitionSO — 24개</summary>

- [contract_beastkin_crisis](<Assets/Resources/SO/V20/Factions/Contracts/contract_beastkin_crisis.asset>)
- [contract_beastkin_strategic](<Assets/Resources/SO/V20/Factions/Contracts/contract_beastkin_strategic.asset>)
- [contract_beastkin_supply](<Assets/Resources/SO/V20/Factions/Contracts/contract_beastkin_supply.asset>)
- [contract_beastkin_winter_fuel_demand](<Assets/Resources/SO/V20/Factions/Contracts/contract_beastkin_winter_fuel_demand.asset>)
- [contract_demon_crisis](<Assets/Resources/SO/V20/Factions/Contracts/contract_demon_crisis.asset>)
- [contract_demon_strategic](<Assets/Resources/SO/V20/Factions/Contracts/contract_demon_strategic.asset>)
- [contract_demon_supply](<Assets/Resources/SO/V20/Factions/Contracts/contract_demon_supply.asset>)
- [contract_demon_winter_fuel_demand](<Assets/Resources/SO/V20/Factions/Contracts/contract_demon_winter_fuel_demand.asset>)
- [contract_golem_crisis](<Assets/Resources/SO/V20/Factions/Contracts/contract_golem_crisis.asset>)
- [contract_golem_strategic](<Assets/Resources/SO/V20/Factions/Contracts/contract_golem_strategic.asset>)
- [contract_golem_supply](<Assets/Resources/SO/V20/Factions/Contracts/contract_golem_supply.asset>)
- [contract_golem_winter_fuel_demand](<Assets/Resources/SO/V20/Factions/Contracts/contract_golem_winter_fuel_demand.asset>)
- [contract_harpy_crisis](<Assets/Resources/SO/V20/Factions/Contracts/contract_harpy_crisis.asset>)
- [contract_harpy_strategic](<Assets/Resources/SO/V20/Factions/Contracts/contract_harpy_strategic.asset>)
- [contract_harpy_supply](<Assets/Resources/SO/V20/Factions/Contracts/contract_harpy_supply.asset>)
- [contract_harpy_winter_fuel_demand](<Assets/Resources/SO/V20/Factions/Contracts/contract_harpy_winter_fuel_demand.asset>)
- [contract_kobold_crisis](<Assets/Resources/SO/V20/Factions/Contracts/contract_kobold_crisis.asset>)
- [contract_kobold_strategic](<Assets/Resources/SO/V20/Factions/Contracts/contract_kobold_strategic.asset>)
- [contract_kobold_supply](<Assets/Resources/SO/V20/Factions/Contracts/contract_kobold_supply.asset>)
- [contract_kobold_winter_fuel_demand](<Assets/Resources/SO/V20/Factions/Contracts/contract_kobold_winter_fuel_demand.asset>)
- [contract_myconid_crisis](<Assets/Resources/SO/V20/Factions/Contracts/contract_myconid_crisis.asset>)
- [contract_myconid_strategic](<Assets/Resources/SO/V20/Factions/Contracts/contract_myconid_strategic.asset>)
- [contract_myconid_supply](<Assets/Resources/SO/V20/Factions/Contracts/contract_myconid_supply.asset>)
- [contract_myconid_winter_fuel_demand](<Assets/Resources/SO/V20/Factions/Contracts/contract_myconid_winter_fuel_demand.asset>)

</details>

<details>
<summary>FestivalDefinitionSO — 20개</summary>

- [festival_high-sun](<Assets/Resources/SO/Population/Festivals/festival_high-sun.asset>)
- [festival_long-night-memorial](<Assets/Resources/SO/Population/Festivals/festival_long-night-memorial.asset>)
- [festival_sprout](<Assets/Resources/SO/Population/Festivals/festival_sprout.asset>)
- [festival_storage](<Assets/Resources/SO/Population/Festivals/festival_storage.asset>)
- [festival_ash-oath](<Assets/Resources/SO/V20/Society/Festivals/festival_ash-oath.asset>)
- [festival_blood-lantern](<Assets/Resources/SO/V20/Society/Festivals/festival_blood-lantern.asset>)
- [festival_clear-confluence](<Assets/Resources/SO/V20/Society/Festivals/festival_clear-confluence.asset>)
- [festival_core-resonance](<Assets/Resources/SO/V20/Society/Festivals/festival_core-resonance.asset>)
- [festival_dungeon-accord-day](<Assets/Resources/SO/V20/Society/Festivals/festival_dungeon-accord-day.asset>)
- [festival_frontier-map-night](<Assets/Resources/SO/V20/Society/Festivals/festival_frontier-map-night.asset>)
- [festival_high-sun](<Assets/Resources/SO/V20/Society/Festivals/festival_high-sun.asset>)
- [festival_long-night-memorial](<Assets/Resources/SO/V20/Society/Festivals/festival_long-night-memorial.asset>)
- [festival_many-tables](<Assets/Resources/SO/V20/Society/Festivals/festival_many-tables.asset>)
- [festival_open-sky-chorus](<Assets/Resources/SO/V20/Society/Festivals/festival_open-sky-chorus.asset>)
- [festival_pack-first-hunt](<Assets/Resources/SO/V20/Society/Festivals/festival_pack-first-hunt.asset>)
- [festival_spore-bloom](<Assets/Resources/SO/V20/Society/Festivals/festival_spore-bloom.asset>)
- [festival_sprout](<Assets/Resources/SO/V20/Society/Festivals/festival_sprout.asset>)
- [festival_storage](<Assets/Resources/SO/V20/Society/Festivals/festival_storage.asset>)
- [festival_tool-clan-fair](<Assets/Resources/SO/V20/Society/Festivals/festival_tool-clan-fair.asset>)
- [festival_weapon-vigil](<Assets/Resources/SO/V20/Society/Festivals/festival_weapon-vigil.asset>)

</details>

<details>
<summary>FuneralCultureSO — 11개</summary>

- [Funeral_Adventurer](<Assets/Resources/SO/Population/Funeral/Funeral_Adventurer.asset>)
- [Funeral_Beastkin](<Assets/Resources/SO/Population/Funeral/Funeral_Beastkin.asset>)
- [Funeral_Demon](<Assets/Resources/SO/Population/Funeral/Funeral_Demon.asset>)
- [Funeral_Golem](<Assets/Resources/SO/Population/Funeral/Funeral_Golem.asset>)
- [Funeral_Harpy](<Assets/Resources/SO/Population/Funeral/Funeral_Harpy.asset>)
- [Funeral_Human](<Assets/Resources/SO/Population/Funeral/Funeral_Human.asset>)
- [Funeral_Kobold](<Assets/Resources/SO/Population/Funeral/Funeral_Kobold.asset>)
- [Funeral_Myconid](<Assets/Resources/SO/Population/Funeral/Funeral_Myconid.asset>)
- [Funeral_Orc](<Assets/Resources/SO/Population/Funeral/Funeral_Orc.asset>)
- [Funeral_Slime](<Assets/Resources/SO/Population/Funeral/Funeral_Slime.asset>)
- [Funeral_Vampire](<Assets/Resources/SO/Population/Funeral/Funeral_Vampire.asset>)

</details>

<details>
<summary>GameContentCatalogSO — 1개</summary>

- [GameContentCatalog](<Assets/Resources/SO/GameContentCatalog.asset>)

</details>

<details>
<summary>GameData — 1개</summary>

- [GameData](<Assets/Scripts/Models/GameData/GameData.asset>)

</details>

<details>
<summary>GameDomainContentCatalogSO — 1개</summary>

- [GameDomainContentCatalog](<Assets/Resources/SO/Content/GameDomainContentCatalog.asset>)

</details>

<details>
<summary>GameMediaCatalogSO — 1개</summary>

- [GameMediaCatalog](<Assets/Resources/SO/Content/GameMediaCatalog.asset>)

</details>

<details>
<summary>GameplayEffectConditionDefinitionSO — 45개</summary>

- [accident_fall-slip](<Assets/Resources/SO/V26/Effects/Conditions/accident_fall-slip.asset>)
- [event_hostile-execution](<Assets/Resources/SO/V26/Effects/Conditions/event_hostile-execution.asset>)
- [relationship_first-apology](<Assets/Resources/SO/V26/Effects/Conditions/relationship_first-apology.asset>)
- [relationship_negative](<Assets/Resources/SO/V26/Effects/Conditions/relationship_negative.asset>)
- [room_noise](<Assets/Resources/SO/V26/Effects/Conditions/room_noise.asset>)
- [shift_day](<Assets/Resources/SO/V26/Effects/Conditions/shift_day.asset>)
- [shift_night](<Assets/Resources/SO/V26/Effects/Conditions/shift_night.asset>)
- [state_arcane-overcharge-aftermath](<Assets/Resources/SO/V26/Effects/Conditions/state_arcane-overcharge-aftermath.asset>)
- [state_arcane-overcharge](<Assets/Resources/SO/V26/Effects/Conditions/state_arcane-overcharge.asset>)
- [state_emergency-stocked](<Assets/Resources/SO/V26/Effects/Conditions/state_emergency-stocked.asset>)
- [state_forbidden-leap-aftermath](<Assets/Resources/SO/V26/Effects/Conditions/state_forbidden-leap-aftermath.asset>)
- [state_formal-status](<Assets/Resources/SO/V26/Effects/Conditions/state_formal-status.asset>)
- [state_golden-harvest-jackpot](<Assets/Resources/SO/V26/Effects/Conditions/state_golden-harvest-jackpot.asset>)
- [state_insulted](<Assets/Resources/SO/V26/Effects/Conditions/state_insulted.asset>)
- [state_last-stand-aftermath](<Assets/Resources/SO/V26/Effects/Conditions/state_last-stand-aftermath.asset>)
- [state_last-stand](<Assets/Resources/SO/V26/Effects/Conditions/state_last-stand.asset>)
- [state_miracle-surgery-aftermath](<Assets/Resources/SO/V26/Effects/Conditions/state_miracle-surgery-aftermath.asset>)
- [state_pain](<Assets/Resources/SO/V26/Effects/Conditions/state_pain.asset>)
- [state_production-limit-break-aftermath](<Assets/Resources/SO/V26/Effects/Conditions/state_production-limit-break-aftermath.asset>)
- [state_production-limit-break](<Assets/Resources/SO/V26/Effects/Conditions/state_production-limit-break.asset>)
- [state_ritual-fast-ended](<Assets/Resources/SO/V26/Effects/Conditions/state_ritual-fast-ended.asset>)
- [state_ritual-fasting](<Assets/Resources/SO/V26/Effects/Conditions/state_ritual-fasting.asset>)
- [state_sated](<Assets/Resources/SO/V26/Effects/Conditions/state_sated.asset>)
- [state_sweet-fed](<Assets/Resources/SO/V26/Effects/Conditions/state_sweet-fed.asset>)
- [temperature_cold](<Assets/Resources/SO/V26/Effects/Conditions/temperature_cold.asset>)
- [temperature_comfortable](<Assets/Resources/SO/V26/Effects/Conditions/temperature_comfortable.asset>)
- [temperature_hot](<Assets/Resources/SO/V26/Effects/Conditions/temperature_hot.asset>)
- [temperature_uncomfortable](<Assets/Resources/SO/V26/Effects/Conditions/temperature_uncomfortable.asset>)
- [terrain_rough](<Assets/Resources/SO/V26/Effects/Conditions/terrain_rough.asset>)
- [work_clean-maintenance](<Assets/Resources/SO/V26/Effects/Conditions/work_clean-maintenance.asset>)
- [work_clean](<Assets/Resources/SO/V26/Effects/Conditions/work_clean.asset>)
- [work_contaminated-food](<Assets/Resources/SO/V26/Effects/Conditions/work_contaminated-food.asset>)
- [work_contamination](<Assets/Resources/SO/V26/Effects/Conditions/work_contamination.asset>)
- [work_craft-finished](<Assets/Resources/SO/V26/Effects/Conditions/work_craft-finished.asset>)
- [work_dangerous](<Assets/Resources/SO/V26/Effects/Conditions/work_dangerous.asset>)
- [work_emergency](<Assets/Resources/SO/V26/Effects/Conditions/work_emergency.asset>)
- [work_long-shift](<Assets/Resources/SO/V26/Effects/Conditions/work_long-shift.asset>)
- [work_mentoring](<Assets/Resources/SO/V26/Effects/Conditions/work_mentoring.asset>)
- [work_not-clean](<Assets/Resources/SO/V26/Effects/Conditions/work_not-clean.asset>)
- [work_not-research](<Assets/Resources/SO/V26/Effects/Conditions/work_not-research.asset>)
- [work_on-schedule](<Assets/Resources/SO/V26/Effects/Conditions/work_on-schedule.asset>)
- [work_precision](<Assets/Resources/SO/V26/Effects/Conditions/work_precision.asset>)
- [work_research](<Assets/Resources/SO/V26/Effects/Conditions/work_research.asset>)
- [work_retry-after-failure](<Assets/Resources/SO/V26/Effects/Conditions/work_retry-after-failure.asset>)
- [work_substitute-material](<Assets/Resources/SO/V26/Effects/Conditions/work_substitute-material.asset>)

</details>

<details>
<summary>GameplayEffectDefinitionSO — 65개</summary>

- [effect_arcane_mana-recovery_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_arcane_mana-recovery_multiply.asset>)
- [effect_arcane_power_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_arcane_power_multiply.asset>)
- [effect_capacity_arcane-conduction_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_arcane-conduction_multiply.asset>)
- [effect_capacity_auditory-sensing_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_auditory-sensing_multiply.asset>)
- [effect_capacity_communication_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_communication_multiply.asset>)
- [effect_capacity_immune-defense_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_immune-defense_multiply.asset>)
- [effect_capacity_intake-processing_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_intake-processing_multiply.asset>)
- [effect_capacity_mental-maintenance_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_mental-maintenance_multiply.asset>)
- [effect_capacity_physical-mobility_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_physical-mobility_multiply.asset>)
- [effect_capacity_physical-power_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_physical-power_multiply.asset>)
- [effect_capacity_power-circulation_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_power-circulation_multiply.asset>)
- [effect_capacity_precision-manipulation_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_precision-manipulation_multiply.asset>)
- [effect_capacity_purification-processing_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_purification-processing_multiply.asset>)
- [effect_capacity_respiratory-exchange_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_respiratory-exchange_multiply.asset>)
- [effect_capacity_visual-discernment_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_visual-discernment_multiply.asset>)
- [effect_capacity_vitality-response_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_capacity_vitality-response_multiply.asset>)
- [effect_character_accident-chance_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_accident-chance_multiply.asset>)
- [effect_character_alarm-response-delay_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_alarm-response-delay_multiply.asset>)
- [effect_character_cold-exposure_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_cold-exposure_multiply.asset>)
- [effect_character_combat-power_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_combat-power_multiply.asset>)
- [effect_character_combat-stress_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_combat-stress_multiply.asset>)
- [effect_character_consumption_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_consumption_multiply.asset>)
- [effect_character_crowd-sensitivity_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_crowd-sensitivity_multiply.asset>)
- [effect_character_danger-detection_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_danger-detection_multiply.asset>)
- [effect_character_disease-recovery-speed_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_disease-recovery-speed_multiply.asset>)
- [effect_character_disease-resistance_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_disease-resistance_multiply.asset>)
- [effect_character_earned-work-xp_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_earned-work-xp_multiply.asset>)
- [effect_character_fatigue-rate_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_fatigue-rate_multiply.asset>)
- [effect_character_food-poisoning-chance_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_food-poisoning-chance_multiply.asset>)
- [effect_character_heat-exposure_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_heat-exposure_multiply.asset>)
- [effect_character_immunity-gain_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_immunity-gain_multiply.asset>)
- [effect_character_immunity-retention_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_immunity-retention_multiply.asset>)
- [effect_character_maximum-health_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_maximum-health_multiply.asset>)
- [effect_character_mentee-xp_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_mentee-xp_multiply.asset>)
- [effect_character_move-speed_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_move-speed_multiply.asset>)
- [effect_character_negative-mood-duration_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_negative-mood-duration_multiply.asset>)
- [effect_character_pain-work-penalty_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_pain-work-penalty_multiply.asset>)
- [effect_character_recovery-speed_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_recovery-speed_multiply.asset>)
- [effect_character_relationship-recovery_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_relationship-recovery_multiply.asset>)
- [effect_character_research-speed_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_research-speed_multiply.asset>)
- [effect_character_sleep-recovery_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_sleep-recovery_multiply.asset>)
- [effect_character_spending_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_spending_multiply.asset>)
- [effect_character_wait-patience_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_wait-patience_multiply.asset>)
- [effect_character_work-speed_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_character_work-speed_multiply.asset>)
- [effect_combat_evasion-chance_add-flat](<Assets/Resources/SO/V26/Effects/Definitions/effect_combat_evasion-chance_add-flat.asset>)
- [effect_craft_quality-score_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_craft_quality-score_addflat.asset>)
- [effect_damage_blunt-taken_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_damage_blunt-taken_multiply.asset>)
- [effect_environment_comfort-minimum-offset_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_environment_comfort-minimum-offset_addflat.asset>)
- [effect_environment_safe-minimum-offset_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_environment_safe-minimum-offset_addflat.asset>)
- [effect_food_spoilage-detection_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_food_spoilage-detection_multiply.asset>)
- [effect_harvest_seed-yield_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_harvest_seed-yield_multiply.asset>)
- [effect_harvest_yield_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_harvest_yield_multiply.asset>)
- [effect_medical_aftermath-duration_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_medical_aftermath-duration_multiply.asset>)
- [effect_proficiency_construction-engineering_starting-xp_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_proficiency_construction-engineering_starting-xp_addflat.asset>)
- [effect_proficiency_crafting_starting-xp_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_proficiency_crafting_starting-xp_addflat.asset>)
- [effect_proficiency_fieldwork_starting-xp_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_proficiency_fieldwork_starting-xp_addflat.asset>)
- [effect_proficiency_food-production_starting-xp_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_proficiency_food-production_starting-xp_addflat.asset>)
- [effect_proficiency_medicine_starting-xp_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_proficiency_medicine_starting-xp_addflat.asset>)
- [effect_proficiency_melee-combat_starting-xp_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_proficiency_melee-combat_starting-xp_addflat.asset>)
- [effect_proficiency_ranged-combat_starting-xp_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_proficiency_ranged-combat_starting-xp_addflat.asset>)
- [effect_proficiency_scholarship_starting-xp_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_proficiency_scholarship_starting-xp_addflat.asset>)
- [effect_proficiency_social_starting-xp_addflat](<Assets/Resources/SO/V26/Effects/Definitions/effect_proficiency_social_starting-xp_addflat.asset>)
- [effect_social_negotiation_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_social_negotiation_multiply.asset>)
- [effect_work_haul-capacity_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_work_haul-capacity_multiply.asset>)
- [effect_work_salvage-yield_multiply](<Assets/Resources/SO/V26/Effects/Definitions/effect_work_salvage-yield_multiply.asset>)

</details>

<details>
<summary>GenericItemDefinitionSO — 703개</summary>

- [ammo_arrow](<Assets/Resources/SO/Items/Definitions/ammo_arrow.asset>)
- [ammo_bolt](<Assets/Resources/SO/Items/Definitions/ammo_bolt.asset>)
- [captivity_extracted_blood](<Assets/Resources/SO/Items/Definitions/captivity_extracted_blood.asset>)
- [captivity_memory_residue](<Assets/Resources/SO/Items/Definitions/captivity_memory_residue.asset>)
- [captivity_restraints](<Assets/Resources/SO/Items/Definitions/captivity_restraints.asset>)
- [dark_bone](<Assets/Resources/SO/Items/Definitions/dark_bone.asset>)
- [dark_humanoid_corpse](<Assets/Resources/SO/Items/Definitions/dark_humanoid_corpse.asset>)
- [dark_humanoid_meat](<Assets/Resources/SO/Items/Definitions/dark_humanoid_meat.asset>)
- [equipment_item_armor_articulated_plate](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_articulated_plate.asset>)
- [equipment_item_armor_blacksteel_carapace](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_blacksteel_carapace.asset>)
- [equipment_item_armor_blast_coat](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_blast_coat.asset>)
- [equipment_item_armor_breastplate](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_breastplate.asset>)
- [equipment_item_armor_brigandine](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_brigandine.asset>)
- [equipment_item_armor_closed_plate_helm](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_closed_plate_helm.asset>)
- [equipment_item_armor_cloth_hood](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_cloth_hood.asset>)
- [equipment_item_armor_gambeson](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_gambeson.asset>)
- [equipment_item_armor_hardened_leather_coat](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_hardened_leather_coat.asset>)
- [equipment_item_armor_iron_helmet](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_iron_helmet.asset>)
- [equipment_item_armor_jack_of_plates](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_jack_of_plates.asset>)
- [equipment_item_armor_leather](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_leather.asset>)
- [equipment_item_armor_leather_cap](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_leather_cap.asset>)
- [equipment_item_armor_mail_coif](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_mail_coif.asset>)
- [equipment_item_armor_mail_shirt](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_mail_shirt.asset>)
- [equipment_item_armor_padded_hood](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_padded_hood.asset>)
- [equipment_item_armor_powder_cuirass](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_powder_cuirass.asset>)
- [equipment_item_armor_powered_harness](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_powered_harness.asset>)
- [equipment_item_armor_rune_ward_mail](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_rune_ward_mail.asset>)
- [equipment_item_armor_scale_coat](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_scale_coat.asset>)
- [equipment_item_armor_smoke_hood](<Assets/Resources/SO/Items/Definitions/equipment_item_armor_smoke_hood.asset>)
- [equipment_item_shield_blacksteel](<Assets/Resources/SO/Items/Definitions/equipment_item_shield_blacksteel.asset>)
- [equipment_item_shield_buckler](<Assets/Resources/SO/Items/Definitions/equipment_item_shield_buckler.asset>)
- [equipment_item_shield_iron](<Assets/Resources/SO/Items/Definitions/equipment_item_shield_iron.asset>)
- [equipment_item_shield_mana_buckler](<Assets/Resources/SO/Items/Definitions/equipment_item_shield_mana_buckler.asset>)
- [equipment_item_shield_pavise](<Assets/Resources/SO/Items/Definitions/equipment_item_shield_pavise.asset>)
- [equipment_item_shield_powered](<Assets/Resources/SO/Items/Definitions/equipment_item_shield_powered.asset>)
- [equipment_item_shield_rune](<Assets/Resources/SO/Items/Definitions/equipment_item_shield_rune.asset>)
- [equipment_item_shield_tower](<Assets/Resources/SO/Items/Definitions/equipment_item_shield_tower.asset>)
- [equipment_item_shield_wood](<Assets/Resources/SO/Items/Definitions/equipment_item_shield_wood.asset>)
- [equipment_item_weapon_arquebus](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_arquebus.asset>)
- [equipment_item_weapon_blacksteel_poleaxe](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_blacksteel_poleaxe.asset>)
- [equipment_item_weapon_composite_bow](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_composite_bow.asset>)
- [equipment_item_weapon_crossbow](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_crossbow.asset>)
- [equipment_item_weapon_dagger](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_dagger.asset>)
- [equipment_item_weapon_estoc](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_estoc.asset>)
- [equipment_item_weapon_falchion](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_falchion.asset>)
- [equipment_item_weapon_greatsword](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_greatsword.asset>)
- [equipment_item_weapon_halberd](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_halberd.asset>)
- [equipment_item_weapon_handgonne](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_handgonne.asset>)
- [equipment_item_weapon_heavy_matchlock](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_heavy_matchlock.asset>)
- [equipment_item_weapon_javelin](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_javelin.asset>)
- [equipment_item_weapon_longbow](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_longbow.asset>)
- [equipment_item_weapon_longsword](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_longsword.asset>)
- [equipment_item_weapon_mace](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_mace.asset>)
- [equipment_item_weapon_mana_lance](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_mana_lance.asset>)
- [equipment_item_weapon_matchlock_long_gun](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_matchlock_long_gun.asset>)
- [equipment_item_weapon_matchlock_pistol](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_matchlock_pistol.asset>)
- [equipment_item_weapon_pollaxe](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_pollaxe.asset>)
- [equipment_item_weapon_powered_striking_gauntlet](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_powered_striking_gauntlet.asset>)
- [equipment_item_weapon_repeating_crossbow](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_repeating_crossbow.asset>)
- [equipment_item_weapon_rune_blade](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_rune_blade.asset>)
- [equipment_item_weapon_rune_bow](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_rune_bow.asset>)
- [equipment_item_weapon_shortbow](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_shortbow.asset>)
- [equipment_item_weapon_shotgun](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_shotgun.asset>)
- [equipment_item_weapon_siege_arbalest](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_siege_arbalest.asset>)
- [equipment_item_weapon_sniper_arquebus](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_sniper_arquebus.asset>)
- [equipment_item_weapon_spear](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_spear.asset>)
- [equipment_item_weapon_throwing_axe](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_throwing_axe.asset>)
- [equipment_item_weapon_warhammer](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_warhammer.asset>)
- [equipment_item_weapon_windlass_crossbow](<Assets/Resources/SO/Items/Definitions/equipment_item_weapon_windlass_crossbow.asset>)
- [evolution_catalyst_arcane_1](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_1.asset>)
- [evolution_catalyst_arcane_10](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_10.asset>)
- [evolution_catalyst_arcane_11](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_11.asset>)
- [evolution_catalyst_arcane_12](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_12.asset>)
- [evolution_catalyst_arcane_13](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_13.asset>)
- [evolution_catalyst_arcane_14](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_14.asset>)
- [evolution_catalyst_arcane_15](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_15.asset>)
- [evolution_catalyst_arcane_16](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_16.asset>)
- [evolution_catalyst_arcane_17](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_17.asset>)
- [evolution_catalyst_arcane_18](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_18.asset>)
- [evolution_catalyst_arcane_19](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_19.asset>)
- [evolution_catalyst_arcane_2](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_2.asset>)
- [evolution_catalyst_arcane_20](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_20.asset>)
- [evolution_catalyst_arcane_21](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_21.asset>)
- [evolution_catalyst_arcane_3](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_3.asset>)
- [evolution_catalyst_arcane_4](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_4.asset>)
- [evolution_catalyst_arcane_5](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_5.asset>)
- [evolution_catalyst_arcane_6](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_6.asset>)
- [evolution_catalyst_arcane_7](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_7.asset>)
- [evolution_catalyst_arcane_8](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_8.asset>)
- [evolution_catalyst_arcane_9](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_arcane_9.asset>)
- [evolution_catalyst_authority_1](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_1.asset>)
- [evolution_catalyst_authority_10](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_10.asset>)
- [evolution_catalyst_authority_11](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_11.asset>)
- [evolution_catalyst_authority_12](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_12.asset>)
- [evolution_catalyst_authority_13](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_13.asset>)
- [evolution_catalyst_authority_14](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_14.asset>)
- [evolution_catalyst_authority_15](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_15.asset>)
- [evolution_catalyst_authority_16](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_16.asset>)
- [evolution_catalyst_authority_17](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_17.asset>)
- [evolution_catalyst_authority_18](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_18.asset>)
- [evolution_catalyst_authority_19](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_19.asset>)
- [evolution_catalyst_authority_2](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_2.asset>)
- [evolution_catalyst_authority_20](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_20.asset>)
- [evolution_catalyst_authority_21](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_21.asset>)
- [evolution_catalyst_authority_3](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_3.asset>)
- [evolution_catalyst_authority_4](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_4.asset>)
- [evolution_catalyst_authority_5](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_5.asset>)
- [evolution_catalyst_authority_6](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_6.asset>)
- [evolution_catalyst_authority_7](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_7.asset>)
- [evolution_catalyst_authority_8](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_8.asset>)
- [evolution_catalyst_authority_9](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_authority_9.asset>)
- [evolution_catalyst_defense_1](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_1.asset>)
- [evolution_catalyst_defense_10](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_10.asset>)
- [evolution_catalyst_defense_11](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_11.asset>)
- [evolution_catalyst_defense_12](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_12.asset>)
- [evolution_catalyst_defense_13](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_13.asset>)
- [evolution_catalyst_defense_14](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_14.asset>)
- [evolution_catalyst_defense_15](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_15.asset>)
- [evolution_catalyst_defense_16](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_16.asset>)
- [evolution_catalyst_defense_17](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_17.asset>)
- [evolution_catalyst_defense_18](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_18.asset>)
- [evolution_catalyst_defense_19](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_19.asset>)
- [evolution_catalyst_defense_2](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_2.asset>)
- [evolution_catalyst_defense_20](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_20.asset>)
- [evolution_catalyst_defense_21](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_21.asset>)
- [evolution_catalyst_defense_3](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_3.asset>)
- [evolution_catalyst_defense_4](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_4.asset>)
- [evolution_catalyst_defense_5](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_5.asset>)
- [evolution_catalyst_defense_6](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_6.asset>)
- [evolution_catalyst_defense_7](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_7.asset>)
- [evolution_catalyst_defense_8](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_8.asset>)
- [evolution_catalyst_defense_9](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_defense_9.asset>)
- [evolution_catalyst_industry_1](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_1.asset>)
- [evolution_catalyst_industry_10](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_10.asset>)
- [evolution_catalyst_industry_11](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_11.asset>)
- [evolution_catalyst_industry_12](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_12.asset>)
- [evolution_catalyst_industry_13](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_13.asset>)
- [evolution_catalyst_industry_14](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_14.asset>)
- [evolution_catalyst_industry_15](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_15.asset>)
- [evolution_catalyst_industry_16](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_16.asset>)
- [evolution_catalyst_industry_17](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_17.asset>)
- [evolution_catalyst_industry_18](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_18.asset>)
- [evolution_catalyst_industry_19](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_19.asset>)
- [evolution_catalyst_industry_2](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_2.asset>)
- [evolution_catalyst_industry_20](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_20.asset>)
- [evolution_catalyst_industry_21](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_21.asset>)
- [evolution_catalyst_industry_3](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_3.asset>)
- [evolution_catalyst_industry_4](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_4.asset>)
- [evolution_catalyst_industry_5](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_5.asset>)
- [evolution_catalyst_industry_6](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_6.asset>)
- [evolution_catalyst_industry_7](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_7.asset>)
- [evolution_catalyst_industry_8](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_8.asset>)
- [evolution_catalyst_industry_9](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_industry_9.asset>)
- [evolution_catalyst_offense_1](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_1.asset>)
- [evolution_catalyst_offense_10](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_10.asset>)
- [evolution_catalyst_offense_11](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_11.asset>)
- [evolution_catalyst_offense_12](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_12.asset>)
- [evolution_catalyst_offense_13](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_13.asset>)
- [evolution_catalyst_offense_14](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_14.asset>)
- [evolution_catalyst_offense_15](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_15.asset>)
- [evolution_catalyst_offense_16](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_16.asset>)
- [evolution_catalyst_offense_17](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_17.asset>)
- [evolution_catalyst_offense_18](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_18.asset>)
- [evolution_catalyst_offense_19](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_19.asset>)
- [evolution_catalyst_offense_2](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_2.asset>)
- [evolution_catalyst_offense_20](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_20.asset>)
- [evolution_catalyst_offense_21](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_21.asset>)
- [evolution_catalyst_offense_3](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_3.asset>)
- [evolution_catalyst_offense_4](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_4.asset>)
- [evolution_catalyst_offense_5](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_5.asset>)
- [evolution_catalyst_offense_6](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_6.asset>)
- [evolution_catalyst_offense_7](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_7.asset>)
- [evolution_catalyst_offense_8](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_8.asset>)
- [evolution_catalyst_offense_9](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_offense_9.asset>)
- [evolution_catalyst_survival_1](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_1.asset>)
- [evolution_catalyst_survival_10](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_10.asset>)
- [evolution_catalyst_survival_11](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_11.asset>)
- [evolution_catalyst_survival_12](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_12.asset>)
- [evolution_catalyst_survival_13](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_13.asset>)
- [evolution_catalyst_survival_14](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_14.asset>)
- [evolution_catalyst_survival_15](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_15.asset>)
- [evolution_catalyst_survival_16](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_16.asset>)
- [evolution_catalyst_survival_17](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_17.asset>)
- [evolution_catalyst_survival_18](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_18.asset>)
- [evolution_catalyst_survival_19](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_19.asset>)
- [evolution_catalyst_survival_2](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_2.asset>)
- [evolution_catalyst_survival_20](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_20.asset>)
- [evolution_catalyst_survival_21](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_21.asset>)
- [evolution_catalyst_survival_3](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_3.asset>)
- [evolution_catalyst_survival_4](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_4.asset>)
- [evolution_catalyst_survival_5](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_5.asset>)
- [evolution_catalyst_survival_6](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_6.asset>)
- [evolution_catalyst_survival_7](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_7.asset>)
- [evolution_catalyst_survival_8](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_8.asset>)
- [evolution_catalyst_survival_9](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_survival_9.asset>)
- [evolution_catalyst_universal_1](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_1.asset>)
- [evolution_catalyst_universal_10](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_10.asset>)
- [evolution_catalyst_universal_11](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_11.asset>)
- [evolution_catalyst_universal_12](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_12.asset>)
- [evolution_catalyst_universal_13](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_13.asset>)
- [evolution_catalyst_universal_14](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_14.asset>)
- [evolution_catalyst_universal_15](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_15.asset>)
- [evolution_catalyst_universal_16](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_16.asset>)
- [evolution_catalyst_universal_17](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_17.asset>)
- [evolution_catalyst_universal_18](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_18.asset>)
- [evolution_catalyst_universal_19](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_19.asset>)
- [evolution_catalyst_universal_2](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_2.asset>)
- [evolution_catalyst_universal_20](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_20.asset>)
- [evolution_catalyst_universal_21](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_21.asset>)
- [evolution_catalyst_universal_3](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_3.asset>)
- [evolution_catalyst_universal_4](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_4.asset>)
- [evolution_catalyst_universal_5](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_5.asset>)
- [evolution_catalyst_universal_6](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_6.asset>)
- [evolution_catalyst_universal_7](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_7.asset>)
- [evolution_catalyst_universal_8](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_8.asset>)
- [evolution_catalyst_universal_9](<Assets/Resources/SO/Items/Definitions/evolution_catalyst_universal_9.asset>)
- [evolution_facility_package](<Assets/Resources/SO/Items/Definitions/evolution_facility_package.asset>)
- [evolution_residue_1](<Assets/Resources/SO/Items/Definitions/evolution_residue_1.asset>)
- [evolution_residue_10](<Assets/Resources/SO/Items/Definitions/evolution_residue_10.asset>)
- [evolution_residue_11](<Assets/Resources/SO/Items/Definitions/evolution_residue_11.asset>)
- [evolution_residue_12](<Assets/Resources/SO/Items/Definitions/evolution_residue_12.asset>)
- [evolution_residue_13](<Assets/Resources/SO/Items/Definitions/evolution_residue_13.asset>)
- [evolution_residue_14](<Assets/Resources/SO/Items/Definitions/evolution_residue_14.asset>)
- [evolution_residue_15](<Assets/Resources/SO/Items/Definitions/evolution_residue_15.asset>)
- [evolution_residue_16](<Assets/Resources/SO/Items/Definitions/evolution_residue_16.asset>)
- [evolution_residue_17](<Assets/Resources/SO/Items/Definitions/evolution_residue_17.asset>)
- [evolution_residue_18](<Assets/Resources/SO/Items/Definitions/evolution_residue_18.asset>)
- [evolution_residue_19](<Assets/Resources/SO/Items/Definitions/evolution_residue_19.asset>)
- [evolution_residue_2](<Assets/Resources/SO/Items/Definitions/evolution_residue_2.asset>)
- [evolution_residue_20](<Assets/Resources/SO/Items/Definitions/evolution_residue_20.asset>)
- [evolution_residue_21](<Assets/Resources/SO/Items/Definitions/evolution_residue_21.asset>)
- [evolution_residue_3](<Assets/Resources/SO/Items/Definitions/evolution_residue_3.asset>)
- [evolution_residue_4](<Assets/Resources/SO/Items/Definitions/evolution_residue_4.asset>)
- [evolution_residue_5](<Assets/Resources/SO/Items/Definitions/evolution_residue_5.asset>)
- [evolution_residue_6](<Assets/Resources/SO/Items/Definitions/evolution_residue_6.asset>)
- [evolution_residue_7](<Assets/Resources/SO/Items/Definitions/evolution_residue_7.asset>)
- [evolution_residue_8](<Assets/Resources/SO/Items/Definitions/evolution_residue_8.asset>)
- [evolution_residue_9](<Assets/Resources/SO/Items/Definitions/evolution_residue_9.asset>)
- [facility_kit_0](<Assets/Resources/SO/Items/Definitions/facility_kit_0.asset>)
- [facility_kit_1](<Assets/Resources/SO/Items/Definitions/facility_kit_1.asset>)
- [facility_kit_1000](<Assets/Resources/SO/Items/Definitions/facility_kit_1000.asset>)
- [facility_kit_1001](<Assets/Resources/SO/Items/Definitions/facility_kit_1001.asset>)
- [facility_kit_1002](<Assets/Resources/SO/Items/Definitions/facility_kit_1002.asset>)
- [facility_kit_1003](<Assets/Resources/SO/Items/Definitions/facility_kit_1003.asset>)
- [facility_kit_1004](<Assets/Resources/SO/Items/Definitions/facility_kit_1004.asset>)
- [facility_kit_1005](<Assets/Resources/SO/Items/Definitions/facility_kit_1005.asset>)
- [facility_kit_1006](<Assets/Resources/SO/Items/Definitions/facility_kit_1006.asset>)
- [facility_kit_1007](<Assets/Resources/SO/Items/Definitions/facility_kit_1007.asset>)
- [facility_kit_1008](<Assets/Resources/SO/Items/Definitions/facility_kit_1008.asset>)
- [facility_kit_1009](<Assets/Resources/SO/Items/Definitions/facility_kit_1009.asset>)
- [facility_kit_1010](<Assets/Resources/SO/Items/Definitions/facility_kit_1010.asset>)
- [facility_kit_1011](<Assets/Resources/SO/Items/Definitions/facility_kit_1011.asset>)
- [facility_kit_1012](<Assets/Resources/SO/Items/Definitions/facility_kit_1012.asset>)
- [facility_kit_1013](<Assets/Resources/SO/Items/Definitions/facility_kit_1013.asset>)
- [facility_kit_1014](<Assets/Resources/SO/Items/Definitions/facility_kit_1014.asset>)
- [facility_kit_1015](<Assets/Resources/SO/Items/Definitions/facility_kit_1015.asset>)
- [facility_kit_1016](<Assets/Resources/SO/Items/Definitions/facility_kit_1016.asset>)
- [facility_kit_1017](<Assets/Resources/SO/Items/Definitions/facility_kit_1017.asset>)
- [facility_kit_1018](<Assets/Resources/SO/Items/Definitions/facility_kit_1018.asset>)
- [facility_kit_1019](<Assets/Resources/SO/Items/Definitions/facility_kit_1019.asset>)
- [facility_kit_1020](<Assets/Resources/SO/Items/Definitions/facility_kit_1020.asset>)
- [facility_kit_1021](<Assets/Resources/SO/Items/Definitions/facility_kit_1021.asset>)
- [facility_kit_1022](<Assets/Resources/SO/Items/Definitions/facility_kit_1022.asset>)
- [facility_kit_1023](<Assets/Resources/SO/Items/Definitions/facility_kit_1023.asset>)
- [facility_kit_1024](<Assets/Resources/SO/Items/Definitions/facility_kit_1024.asset>)
- [facility_kit_1025](<Assets/Resources/SO/Items/Definitions/facility_kit_1025.asset>)
- [facility_kit_1026](<Assets/Resources/SO/Items/Definitions/facility_kit_1026.asset>)
- [facility_kit_1027](<Assets/Resources/SO/Items/Definitions/facility_kit_1027.asset>)
- [facility_kit_1028](<Assets/Resources/SO/Items/Definitions/facility_kit_1028.asset>)
- [facility_kit_1029](<Assets/Resources/SO/Items/Definitions/facility_kit_1029.asset>)
- [facility_kit_1030](<Assets/Resources/SO/Items/Definitions/facility_kit_1030.asset>)
- [facility_kit_1031](<Assets/Resources/SO/Items/Definitions/facility_kit_1031.asset>)
- [facility_kit_1032](<Assets/Resources/SO/Items/Definitions/facility_kit_1032.asset>)
- [facility_kit_1033](<Assets/Resources/SO/Items/Definitions/facility_kit_1033.asset>)
- [facility_kit_1034](<Assets/Resources/SO/Items/Definitions/facility_kit_1034.asset>)
- [facility_kit_1035](<Assets/Resources/SO/Items/Definitions/facility_kit_1035.asset>)
- [facility_kit_1036](<Assets/Resources/SO/Items/Definitions/facility_kit_1036.asset>)
- [facility_kit_1037](<Assets/Resources/SO/Items/Definitions/facility_kit_1037.asset>)
- [facility_kit_1038](<Assets/Resources/SO/Items/Definitions/facility_kit_1038.asset>)
- [facility_kit_1039](<Assets/Resources/SO/Items/Definitions/facility_kit_1039.asset>)
- [facility_kit_1040](<Assets/Resources/SO/Items/Definitions/facility_kit_1040.asset>)
- [facility_kit_1041](<Assets/Resources/SO/Items/Definitions/facility_kit_1041.asset>)
- [facility_kit_1042](<Assets/Resources/SO/Items/Definitions/facility_kit_1042.asset>)
- [facility_kit_1043](<Assets/Resources/SO/Items/Definitions/facility_kit_1043.asset>)
- [facility_kit_1044](<Assets/Resources/SO/Items/Definitions/facility_kit_1044.asset>)
- [facility_kit_1045](<Assets/Resources/SO/Items/Definitions/facility_kit_1045.asset>)
- [facility_kit_1046](<Assets/Resources/SO/Items/Definitions/facility_kit_1046.asset>)
- [facility_kit_1047](<Assets/Resources/SO/Items/Definitions/facility_kit_1047.asset>)
- [facility_kit_1048](<Assets/Resources/SO/Items/Definitions/facility_kit_1048.asset>)
- [facility_kit_1049](<Assets/Resources/SO/Items/Definitions/facility_kit_1049.asset>)
- [facility_kit_1050](<Assets/Resources/SO/Items/Definitions/facility_kit_1050.asset>)
- [facility_kit_1051](<Assets/Resources/SO/Items/Definitions/facility_kit_1051.asset>)
- [facility_kit_1052](<Assets/Resources/SO/Items/Definitions/facility_kit_1052.asset>)
- [facility_kit_1053](<Assets/Resources/SO/Items/Definitions/facility_kit_1053.asset>)
- [facility_kit_1054](<Assets/Resources/SO/Items/Definitions/facility_kit_1054.asset>)
- [facility_kit_1055](<Assets/Resources/SO/Items/Definitions/facility_kit_1055.asset>)
- [facility_kit_1056](<Assets/Resources/SO/Items/Definitions/facility_kit_1056.asset>)
- [facility_kit_1057](<Assets/Resources/SO/Items/Definitions/facility_kit_1057.asset>)
- [facility_kit_1058](<Assets/Resources/SO/Items/Definitions/facility_kit_1058.asset>)
- [facility_kit_1059](<Assets/Resources/SO/Items/Definitions/facility_kit_1059.asset>)
- [facility_kit_1060](<Assets/Resources/SO/Items/Definitions/facility_kit_1060.asset>)
- [facility_kit_1061](<Assets/Resources/SO/Items/Definitions/facility_kit_1061.asset>)
- [facility_kit_1062](<Assets/Resources/SO/Items/Definitions/facility_kit_1062.asset>)
- [facility_kit_1063](<Assets/Resources/SO/Items/Definitions/facility_kit_1063.asset>)
- [facility_kit_1064](<Assets/Resources/SO/Items/Definitions/facility_kit_1064.asset>)
- [facility_kit_1065](<Assets/Resources/SO/Items/Definitions/facility_kit_1065.asset>)
- [facility_kit_1066](<Assets/Resources/SO/Items/Definitions/facility_kit_1066.asset>)
- [facility_kit_1067](<Assets/Resources/SO/Items/Definitions/facility_kit_1067.asset>)
- [facility_kit_1068](<Assets/Resources/SO/Items/Definitions/facility_kit_1068.asset>)
- [facility_kit_1069](<Assets/Resources/SO/Items/Definitions/facility_kit_1069.asset>)
- [facility_kit_1070](<Assets/Resources/SO/Items/Definitions/facility_kit_1070.asset>)
- [facility_kit_1071](<Assets/Resources/SO/Items/Definitions/facility_kit_1071.asset>)
- [facility_kit_1072](<Assets/Resources/SO/Items/Definitions/facility_kit_1072.asset>)
- [facility_kit_1073](<Assets/Resources/SO/Items/Definitions/facility_kit_1073.asset>)
- [facility_kit_1074](<Assets/Resources/SO/Items/Definitions/facility_kit_1074.asset>)
- [facility_kit_1075](<Assets/Resources/SO/Items/Definitions/facility_kit_1075.asset>)
- [facility_kit_1076](<Assets/Resources/SO/Items/Definitions/facility_kit_1076.asset>)
- [facility_kit_1077](<Assets/Resources/SO/Items/Definitions/facility_kit_1077.asset>)
- [facility_kit_1078](<Assets/Resources/SO/Items/Definitions/facility_kit_1078.asset>)
- [facility_kit_1079](<Assets/Resources/SO/Items/Definitions/facility_kit_1079.asset>)
- [facility_kit_1080](<Assets/Resources/SO/Items/Definitions/facility_kit_1080.asset>)
- [facility_kit_1081](<Assets/Resources/SO/Items/Definitions/facility_kit_1081.asset>)
- [facility_kit_1082](<Assets/Resources/SO/Items/Definitions/facility_kit_1082.asset>)
- [facility_kit_1083](<Assets/Resources/SO/Items/Definitions/facility_kit_1083.asset>)
- [facility_kit_1084](<Assets/Resources/SO/Items/Definitions/facility_kit_1084.asset>)
- [facility_kit_1085](<Assets/Resources/SO/Items/Definitions/facility_kit_1085.asset>)
- [facility_kit_1086](<Assets/Resources/SO/Items/Definitions/facility_kit_1086.asset>)
- [facility_kit_1087](<Assets/Resources/SO/Items/Definitions/facility_kit_1087.asset>)
- [facility_kit_1088](<Assets/Resources/SO/Items/Definitions/facility_kit_1088.asset>)
- [facility_kit_1089](<Assets/Resources/SO/Items/Definitions/facility_kit_1089.asset>)
- [facility_kit_1090](<Assets/Resources/SO/Items/Definitions/facility_kit_1090.asset>)
- [facility_kit_1091](<Assets/Resources/SO/Items/Definitions/facility_kit_1091.asset>)
- [facility_kit_1092](<Assets/Resources/SO/Items/Definitions/facility_kit_1092.asset>)
- [facility_kit_1093](<Assets/Resources/SO/Items/Definitions/facility_kit_1093.asset>)
- [facility_kit_1094](<Assets/Resources/SO/Items/Definitions/facility_kit_1094.asset>)
- [facility_kit_1095](<Assets/Resources/SO/Items/Definitions/facility_kit_1095.asset>)
- [facility_kit_1096](<Assets/Resources/SO/Items/Definitions/facility_kit_1096.asset>)
- [facility_kit_1097](<Assets/Resources/SO/Items/Definitions/facility_kit_1097.asset>)
- [facility_kit_1200](<Assets/Resources/SO/Items/Definitions/facility_kit_1200.asset>)
- [facility_kit_1201](<Assets/Resources/SO/Items/Definitions/facility_kit_1201.asset>)
- [facility_kit_1202](<Assets/Resources/SO/Items/Definitions/facility_kit_1202.asset>)
- [facility_kit_1203](<Assets/Resources/SO/Items/Definitions/facility_kit_1203.asset>)
- [facility_kit_1204](<Assets/Resources/SO/Items/Definitions/facility_kit_1204.asset>)
- [facility_kit_1205](<Assets/Resources/SO/Items/Definitions/facility_kit_1205.asset>)
- [facility_kit_1206](<Assets/Resources/SO/Items/Definitions/facility_kit_1206.asset>)
- [facility_kit_1207](<Assets/Resources/SO/Items/Definitions/facility_kit_1207.asset>)
- [facility_kit_1208](<Assets/Resources/SO/Items/Definitions/facility_kit_1208.asset>)
- [facility_kit_1209](<Assets/Resources/SO/Items/Definitions/facility_kit_1209.asset>)
- [facility_kit_1500](<Assets/Resources/SO/Items/Definitions/facility_kit_1500.asset>)
- [facility_kit_1501](<Assets/Resources/SO/Items/Definitions/facility_kit_1501.asset>)
- [facility_kit_1502](<Assets/Resources/SO/Items/Definitions/facility_kit_1502.asset>)
- [facility_kit_1503](<Assets/Resources/SO/Items/Definitions/facility_kit_1503.asset>)
- [facility_kit_1504](<Assets/Resources/SO/Items/Definitions/facility_kit_1504.asset>)
- [facility_kit_1505](<Assets/Resources/SO/Items/Definitions/facility_kit_1505.asset>)
- [facility_kit_1600](<Assets/Resources/SO/Items/Definitions/facility_kit_1600.asset>)
- [facility_kit_1601](<Assets/Resources/SO/Items/Definitions/facility_kit_1601.asset>)
- [facility_kit_1602](<Assets/Resources/SO/Items/Definitions/facility_kit_1602.asset>)
- [facility_kit_1603](<Assets/Resources/SO/Items/Definitions/facility_kit_1603.asset>)
- [facility_kit_1604](<Assets/Resources/SO/Items/Definitions/facility_kit_1604.asset>)
- [facility_kit_1605](<Assets/Resources/SO/Items/Definitions/facility_kit_1605.asset>)
- [facility_kit_1606](<Assets/Resources/SO/Items/Definitions/facility_kit_1606.asset>)
- [facility_kit_1607](<Assets/Resources/SO/Items/Definitions/facility_kit_1607.asset>)
- [facility_kit_1608](<Assets/Resources/SO/Items/Definitions/facility_kit_1608.asset>)
- [facility_kit_1609](<Assets/Resources/SO/Items/Definitions/facility_kit_1609.asset>)
- [facility_kit_1610](<Assets/Resources/SO/Items/Definitions/facility_kit_1610.asset>)
- [facility_kit_1611](<Assets/Resources/SO/Items/Definitions/facility_kit_1611.asset>)
- [facility_kit_1612](<Assets/Resources/SO/Items/Definitions/facility_kit_1612.asset>)
- [facility_kit_1613](<Assets/Resources/SO/Items/Definitions/facility_kit_1613.asset>)
- [facility_kit_1614](<Assets/Resources/SO/Items/Definitions/facility_kit_1614.asset>)
- [facility_kit_1615](<Assets/Resources/SO/Items/Definitions/facility_kit_1615.asset>)
- [facility_kit_1616](<Assets/Resources/SO/Items/Definitions/facility_kit_1616.asset>)
- [facility_kit_1617](<Assets/Resources/SO/Items/Definitions/facility_kit_1617.asset>)
- [facility_kit_1618](<Assets/Resources/SO/Items/Definitions/facility_kit_1618.asset>)
- [facility_kit_1619](<Assets/Resources/SO/Items/Definitions/facility_kit_1619.asset>)
- [facility_kit_1620](<Assets/Resources/SO/Items/Definitions/facility_kit_1620.asset>)
- [facility_kit_1621](<Assets/Resources/SO/Items/Definitions/facility_kit_1621.asset>)
- [facility_kit_1622](<Assets/Resources/SO/Items/Definitions/facility_kit_1622.asset>)
- [facility_kit_1623](<Assets/Resources/SO/Items/Definitions/facility_kit_1623.asset>)
- [facility_kit_1624](<Assets/Resources/SO/Items/Definitions/facility_kit_1624.asset>)
- [facility_kit_1625](<Assets/Resources/SO/Items/Definitions/facility_kit_1625.asset>)
- [facility_kit_1626](<Assets/Resources/SO/Items/Definitions/facility_kit_1626.asset>)
- [facility_kit_1627](<Assets/Resources/SO/Items/Definitions/facility_kit_1627.asset>)
- [facility_kit_1700](<Assets/Resources/SO/Items/Definitions/facility_kit_1700.asset>)
- [facility_kit_1701](<Assets/Resources/SO/Items/Definitions/facility_kit_1701.asset>)
- [facility_kit_1702](<Assets/Resources/SO/Items/Definitions/facility_kit_1702.asset>)
- [facility_kit_1703](<Assets/Resources/SO/Items/Definitions/facility_kit_1703.asset>)
- [facility_kit_1704](<Assets/Resources/SO/Items/Definitions/facility_kit_1704.asset>)
- [facility_kit_1705](<Assets/Resources/SO/Items/Definitions/facility_kit_1705.asset>)
- [facility_kit_1706](<Assets/Resources/SO/Items/Definitions/facility_kit_1706.asset>)
- [facility_kit_1707](<Assets/Resources/SO/Items/Definitions/facility_kit_1707.asset>)
- [facility_kit_1708](<Assets/Resources/SO/Items/Definitions/facility_kit_1708.asset>)
- [facility_kit_1709](<Assets/Resources/SO/Items/Definitions/facility_kit_1709.asset>)
- [facility_kit_1710](<Assets/Resources/SO/Items/Definitions/facility_kit_1710.asset>)
- [facility_kit_1711](<Assets/Resources/SO/Items/Definitions/facility_kit_1711.asset>)
- [facility_kit_1712](<Assets/Resources/SO/Items/Definitions/facility_kit_1712.asset>)
- [facility_kit_1713](<Assets/Resources/SO/Items/Definitions/facility_kit_1713.asset>)
- [facility_kit_1714](<Assets/Resources/SO/Items/Definitions/facility_kit_1714.asset>)
- [facility_kit_1715](<Assets/Resources/SO/Items/Definitions/facility_kit_1715.asset>)
- [facility_kit_1800](<Assets/Resources/SO/Items/Definitions/facility_kit_1800.asset>)
- [facility_kit_1801](<Assets/Resources/SO/Items/Definitions/facility_kit_1801.asset>)
- [facility_kit_1802](<Assets/Resources/SO/Items/Definitions/facility_kit_1802.asset>)
- [facility_kit_1803](<Assets/Resources/SO/Items/Definitions/facility_kit_1803.asset>)
- [facility_kit_1804](<Assets/Resources/SO/Items/Definitions/facility_kit_1804.asset>)
- [facility_kit_1805](<Assets/Resources/SO/Items/Definitions/facility_kit_1805.asset>)
- [facility_kit_2](<Assets/Resources/SO/Items/Definitions/facility_kit_2.asset>)
- [facility_kit_30](<Assets/Resources/SO/Items/Definitions/facility_kit_30.asset>)
- [facility_kit_31](<Assets/Resources/SO/Items/Definitions/facility_kit_31.asset>)
- [facility_kit_32](<Assets/Resources/SO/Items/Definitions/facility_kit_32.asset>)
- [facility_kit_33](<Assets/Resources/SO/Items/Definitions/facility_kit_33.asset>)
- [facility_kit_34](<Assets/Resources/SO/Items/Definitions/facility_kit_34.asset>)
- [facility_kit_4](<Assets/Resources/SO/Items/Definitions/facility_kit_4.asset>)
- [facility_kit_52](<Assets/Resources/SO/Items/Definitions/facility_kit_52.asset>)
- [facility_kit_53](<Assets/Resources/SO/Items/Definitions/facility_kit_53.asset>)
- [facility_kit_57](<Assets/Resources/SO/Items/Definitions/facility_kit_57.asset>)
- [facility_kit_58](<Assets/Resources/SO/Items/Definitions/facility_kit_58.asset>)
- [facility_kit_7](<Assets/Resources/SO/Items/Definitions/facility_kit_7.asset>)
- [facility_kit_8](<Assets/Resources/SO/Items/Definitions/facility_kit_8.asset>)
- [facility_kit_8801](<Assets/Resources/SO/Items/Definitions/facility_kit_8801.asset>)
- [facility_kit_8802](<Assets/Resources/SO/Items/Definitions/facility_kit_8802.asset>)
- [facility_kit_8803](<Assets/Resources/SO/Items/Definitions/facility_kit_8803.asset>)
- [facility_kit_8804](<Assets/Resources/SO/Items/Definitions/facility_kit_8804.asset>)
- [facility_kit_8805](<Assets/Resources/SO/Items/Definitions/facility_kit_8805.asset>)
- [facility_kit_8806](<Assets/Resources/SO/Items/Definitions/facility_kit_8806.asset>)
- [facility_kit_8807](<Assets/Resources/SO/Items/Definitions/facility_kit_8807.asset>)
- [facility_kit_8808](<Assets/Resources/SO/Items/Definitions/facility_kit_8808.asset>)
- [facility_kit_8809](<Assets/Resources/SO/Items/Definitions/facility_kit_8809.asset>)
- [facility_kit_8810](<Assets/Resources/SO/Items/Definitions/facility_kit_8810.asset>)
- [facility_kit_8811](<Assets/Resources/SO/Items/Definitions/facility_kit_8811.asset>)
- [facility_kit_8812](<Assets/Resources/SO/Items/Definitions/facility_kit_8812.asset>)
- [facility_kit_8813](<Assets/Resources/SO/Items/Definitions/facility_kit_8813.asset>)
- [facility_kit_8814](<Assets/Resources/SO/Items/Definitions/facility_kit_8814.asset>)
- [facility_kit_8815](<Assets/Resources/SO/Items/Definitions/facility_kit_8815.asset>)
- [facility_kit_8816](<Assets/Resources/SO/Items/Definitions/facility_kit_8816.asset>)
- [facility_kit_8817](<Assets/Resources/SO/Items/Definitions/facility_kit_8817.asset>)
- [facility_kit_8818](<Assets/Resources/SO/Items/Definitions/facility_kit_8818.asset>)
- [facility_kit_8819](<Assets/Resources/SO/Items/Definitions/facility_kit_8819.asset>)
- [facility_kit_8820](<Assets/Resources/SO/Items/Definitions/facility_kit_8820.asset>)
- [facility_kit_8821](<Assets/Resources/SO/Items/Definitions/facility_kit_8821.asset>)
- [facility_kit_8822](<Assets/Resources/SO/Items/Definitions/facility_kit_8822.asset>)
- [facility_kit_8823](<Assets/Resources/SO/Items/Definitions/facility_kit_8823.asset>)
- [facility_kit_8824](<Assets/Resources/SO/Items/Definitions/facility_kit_8824.asset>)
- [facility_kit_8825](<Assets/Resources/SO/Items/Definitions/facility_kit_8825.asset>)
- [facility_kit_8826](<Assets/Resources/SO/Items/Definitions/facility_kit_8826.asset>)
- [facility_kit_8827](<Assets/Resources/SO/Items/Definitions/facility_kit_8827.asset>)
- [facility_kit_8828](<Assets/Resources/SO/Items/Definitions/facility_kit_8828.asset>)
- [facility_kit_8829](<Assets/Resources/SO/Items/Definitions/facility_kit_8829.asset>)
- [facility_kit_8830](<Assets/Resources/SO/Items/Definitions/facility_kit_8830.asset>)
- [facility_kit_8831](<Assets/Resources/SO/Items/Definitions/facility_kit_8831.asset>)
- [facility_kit_8832](<Assets/Resources/SO/Items/Definitions/facility_kit_8832.asset>)
- [facility_kit_8833](<Assets/Resources/SO/Items/Definitions/facility_kit_8833.asset>)
- [facility_kit_8834](<Assets/Resources/SO/Items/Definitions/facility_kit_8834.asset>)
- [facility_kit_8835](<Assets/Resources/SO/Items/Definitions/facility_kit_8835.asset>)
- [facility_kit_8836](<Assets/Resources/SO/Items/Definitions/facility_kit_8836.asset>)
- [facility_kit_8837](<Assets/Resources/SO/Items/Definitions/facility_kit_8837.asset>)
- [facility_kit_8838](<Assets/Resources/SO/Items/Definitions/facility_kit_8838.asset>)
- [facility_kit_8839](<Assets/Resources/SO/Items/Definitions/facility_kit_8839.asset>)
- [facility_kit_8840](<Assets/Resources/SO/Items/Definitions/facility_kit_8840.asset>)
- [facility_kit_8841](<Assets/Resources/SO/Items/Definitions/facility_kit_8841.asset>)
- [facility_kit_8842](<Assets/Resources/SO/Items/Definitions/facility_kit_8842.asset>)
- [facility_kit_8843](<Assets/Resources/SO/Items/Definitions/facility_kit_8843.asset>)
- [facility_kit_8844](<Assets/Resources/SO/Items/Definitions/facility_kit_8844.asset>)
- [facility_kit_8845](<Assets/Resources/SO/Items/Definitions/facility_kit_8845.asset>)
- [facility_kit_8846](<Assets/Resources/SO/Items/Definitions/facility_kit_8846.asset>)
- [facility_kit_8847](<Assets/Resources/SO/Items/Definitions/facility_kit_8847.asset>)
- [facility_kit_8848](<Assets/Resources/SO/Items/Definitions/facility_kit_8848.asset>)
- [facility_kit_8849](<Assets/Resources/SO/Items/Definitions/facility_kit_8849.asset>)
- [facility_kit_8850](<Assets/Resources/SO/Items/Definitions/facility_kit_8850.asset>)
- [facility_kit_8851](<Assets/Resources/SO/Items/Definitions/facility_kit_8851.asset>)
- [facility_kit_8852](<Assets/Resources/SO/Items/Definitions/facility_kit_8852.asset>)
- [facility_kit_8853](<Assets/Resources/SO/Items/Definitions/facility_kit_8853.asset>)
- [facility_kit_8854](<Assets/Resources/SO/Items/Definitions/facility_kit_8854.asset>)
- [facility_kit_8855](<Assets/Resources/SO/Items/Definitions/facility_kit_8855.asset>)
- [facility_kit_8856](<Assets/Resources/SO/Items/Definitions/facility_kit_8856.asset>)
- [facility_kit_8857](<Assets/Resources/SO/Items/Definitions/facility_kit_8857.asset>)
- [facility_kit_8858](<Assets/Resources/SO/Items/Definitions/facility_kit_8858.asset>)
- [facility_kit_8859](<Assets/Resources/SO/Items/Definitions/facility_kit_8859.asset>)
- [facility_kit_8860](<Assets/Resources/SO/Items/Definitions/facility_kit_8860.asset>)
- [facility_kit_8861](<Assets/Resources/SO/Items/Definitions/facility_kit_8861.asset>)
- [facility_kit_8862](<Assets/Resources/SO/Items/Definitions/facility_kit_8862.asset>)
- [facility_kit_8863](<Assets/Resources/SO/Items/Definitions/facility_kit_8863.asset>)
- [facility_kit_8864](<Assets/Resources/SO/Items/Definitions/facility_kit_8864.asset>)
- [facility_kit_8865](<Assets/Resources/SO/Items/Definitions/facility_kit_8865.asset>)
- [facility_kit_8866](<Assets/Resources/SO/Items/Definitions/facility_kit_8866.asset>)
- [facility_kit_8867](<Assets/Resources/SO/Items/Definitions/facility_kit_8867.asset>)
- [facility_kit_8868](<Assets/Resources/SO/Items/Definitions/facility_kit_8868.asset>)
- [facility_kit_8869](<Assets/Resources/SO/Items/Definitions/facility_kit_8869.asset>)
- [facility_kit_8870](<Assets/Resources/SO/Items/Definitions/facility_kit_8870.asset>)
- [facility_kit_8871](<Assets/Resources/SO/Items/Definitions/facility_kit_8871.asset>)
- [facility_kit_8872](<Assets/Resources/SO/Items/Definitions/facility_kit_8872.asset>)
- [facility_kit_8873](<Assets/Resources/SO/Items/Definitions/facility_kit_8873.asset>)
- [facility_kit_8874](<Assets/Resources/SO/Items/Definitions/facility_kit_8874.asset>)
- [facility_kit_8875](<Assets/Resources/SO/Items/Definitions/facility_kit_8875.asset>)
- [facility_kit_8876](<Assets/Resources/SO/Items/Definitions/facility_kit_8876.asset>)
- [facility_kit_8877](<Assets/Resources/SO/Items/Definitions/facility_kit_8877.asset>)
- [facility_kit_8878](<Assets/Resources/SO/Items/Definitions/facility_kit_8878.asset>)
- [facility_kit_8879](<Assets/Resources/SO/Items/Definitions/facility_kit_8879.asset>)
- [facility_kit_8880](<Assets/Resources/SO/Items/Definitions/facility_kit_8880.asset>)
- [facility_kit_8881](<Assets/Resources/SO/Items/Definitions/facility_kit_8881.asset>)
- [facility_kit_8882](<Assets/Resources/SO/Items/Definitions/facility_kit_8882.asset>)
- [facility_kit_8883](<Assets/Resources/SO/Items/Definitions/facility_kit_8883.asset>)
- [facility_kit_8884](<Assets/Resources/SO/Items/Definitions/facility_kit_8884.asset>)
- [facility_kit_8885](<Assets/Resources/SO/Items/Definitions/facility_kit_8885.asset>)
- [facility_kit_8886](<Assets/Resources/SO/Items/Definitions/facility_kit_8886.asset>)
- [facility_kit_8887](<Assets/Resources/SO/Items/Definitions/facility_kit_8887.asset>)
- [facility_kit_8888](<Assets/Resources/SO/Items/Definitions/facility_kit_8888.asset>)
- [facility_kit_8889](<Assets/Resources/SO/Items/Definitions/facility_kit_8889.asset>)
- [facility_kit_8890](<Assets/Resources/SO/Items/Definitions/facility_kit_8890.asset>)
- [facility_kit_8891](<Assets/Resources/SO/Items/Definitions/facility_kit_8891.asset>)
- [facility_kit_8892](<Assets/Resources/SO/Items/Definitions/facility_kit_8892.asset>)
- [facility_kit_8893](<Assets/Resources/SO/Items/Definitions/facility_kit_8893.asset>)
- [facility_kit_8894](<Assets/Resources/SO/Items/Definitions/facility_kit_8894.asset>)
- [facility_kit_8895](<Assets/Resources/SO/Items/Definitions/facility_kit_8895.asset>)
- [facility_kit_8896](<Assets/Resources/SO/Items/Definitions/facility_kit_8896.asset>)
- [facility_kit_8897](<Assets/Resources/SO/Items/Definitions/facility_kit_8897.asset>)
- [facility_kit_8898](<Assets/Resources/SO/Items/Definitions/facility_kit_8898.asset>)
- [facility_kit_8899](<Assets/Resources/SO/Items/Definitions/facility_kit_8899.asset>)
- [facility_kit_8900](<Assets/Resources/SO/Items/Definitions/facility_kit_8900.asset>)
- [facility_kit_8901](<Assets/Resources/SO/Items/Definitions/facility_kit_8901.asset>)
- [facility_kit_9201](<Assets/Resources/SO/Items/Definitions/facility_kit_9201.asset>)
- [facility_kit_9202](<Assets/Resources/SO/Items/Definitions/facility_kit_9202.asset>)
- [facility_kit_9203](<Assets/Resources/SO/Items/Definitions/facility_kit_9203.asset>)
- [facility_kit_9204](<Assets/Resources/SO/Items/Definitions/facility_kit_9204.asset>)
- [facility_kit_9205](<Assets/Resources/SO/Items/Definitions/facility_kit_9205.asset>)
- [facility_kit_9206](<Assets/Resources/SO/Items/Definitions/facility_kit_9206.asset>)
- [facility_kit_9207](<Assets/Resources/SO/Items/Definitions/facility_kit_9207.asset>)
- [facility_kit_9208](<Assets/Resources/SO/Items/Definitions/facility_kit_9208.asset>)
- [facility_kit_9209](<Assets/Resources/SO/Items/Definitions/facility_kit_9209.asset>)
- [facility_kit_9301](<Assets/Resources/SO/Items/Definitions/facility_kit_9301.asset>)
- [facility_kit_9302](<Assets/Resources/SO/Items/Definitions/facility_kit_9302.asset>)
- [facility_kit_9303](<Assets/Resources/SO/Items/Definitions/facility_kit_9303.asset>)
- [facility_kit_9304](<Assets/Resources/SO/Items/Definitions/facility_kit_9304.asset>)
- [facility_kit_9305](<Assets/Resources/SO/Items/Definitions/facility_kit_9305.asset>)
- [facility_kit_9306](<Assets/Resources/SO/Items/Definitions/facility_kit_9306.asset>)
- [facility_kit_9307](<Assets/Resources/SO/Items/Definitions/facility_kit_9307.asset>)
- [facility_kit_9308](<Assets/Resources/SO/Items/Definitions/facility_kit_9308.asset>)
- [facility_kit_9309](<Assets/Resources/SO/Items/Definitions/facility_kit_9309.asset>)
- [facility_kit_9310](<Assets/Resources/SO/Items/Definitions/facility_kit_9310.asset>)
- [facility_kit_9311](<Assets/Resources/SO/Items/Definitions/facility_kit_9311.asset>)
- [facility_kit_9312](<Assets/Resources/SO/Items/Definitions/facility_kit_9312.asset>)
- [facility_kit_9313](<Assets/Resources/SO/Items/Definitions/facility_kit_9313.asset>)
- [facility_kit_9314](<Assets/Resources/SO/Items/Definitions/facility_kit_9314.asset>)
- [facility_kit_9501](<Assets/Resources/SO/Items/Definitions/facility_kit_9501.asset>)
- [facility_kit_9502](<Assets/Resources/SO/Items/Definitions/facility_kit_9502.asset>)
- [facility_kit_9503](<Assets/Resources/SO/Items/Definitions/facility_kit_9503.asset>)
- [facility_kit_9504](<Assets/Resources/SO/Items/Definitions/facility_kit_9504.asset>)
- [facility_kit_9505](<Assets/Resources/SO/Items/Definitions/facility_kit_9505.asset>)
- [facility_kit_9506](<Assets/Resources/SO/Items/Definitions/facility_kit_9506.asset>)
- [facility_kit_9507](<Assets/Resources/SO/Items/Definitions/facility_kit_9507.asset>)
- [facility_kit_9508](<Assets/Resources/SO/Items/Definitions/facility_kit_9508.asset>)
- [facility_kit_9509](<Assets/Resources/SO/Items/Definitions/facility_kit_9509.asset>)
- [facility_kit_9510](<Assets/Resources/SO/Items/Definitions/facility_kit_9510.asset>)
- [facility_kit_9511](<Assets/Resources/SO/Items/Definitions/facility_kit_9511.asset>)
- [facility_kit_9512](<Assets/Resources/SO/Items/Definitions/facility_kit_9512.asset>)
- [facility_kit_9513](<Assets/Resources/SO/Items/Definitions/facility_kit_9513.asset>)
- [facility_kit_9601](<Assets/Resources/SO/Items/Definitions/facility_kit_9601.asset>)
- [facility_kit_9602](<Assets/Resources/SO/Items/Definitions/facility_kit_9602.asset>)
- [facility_kit_9603](<Assets/Resources/SO/Items/Definitions/facility_kit_9603.asset>)
- [facility_kit_9801](<Assets/Resources/SO/Items/Definitions/facility_kit_9801.asset>)
- [facility_kit_9802](<Assets/Resources/SO/Items/Definitions/facility_kit_9802.asset>)
- [facility_kit_9803](<Assets/Resources/SO/Items/Definitions/facility_kit_9803.asset>)
- [facility_kit_9804](<Assets/Resources/SO/Items/Definitions/facility_kit_9804.asset>)
- [facility_kit_9810](<Assets/Resources/SO/Items/Definitions/facility_kit_9810.asset>)
- [facility_kit_9811](<Assets/Resources/SO/Items/Definitions/facility_kit_9811.asset>)
- [facility_kit_9812](<Assets/Resources/SO/Items/Definitions/facility_kit_9812.asset>)
- [facility_kit_9813](<Assets/Resources/SO/Items/Definitions/facility_kit_9813.asset>)
- [facility_kit_9814](<Assets/Resources/SO/Items/Definitions/facility_kit_9814.asset>)
- [facility_kit_9815](<Assets/Resources/SO/Items/Definitions/facility_kit_9815.asset>)
- [facility_kit_9816](<Assets/Resources/SO/Items/Definitions/facility_kit_9816.asset>)
- [facility_kit_9817](<Assets/Resources/SO/Items/Definitions/facility_kit_9817.asset>)
- [facility_kit_9818](<Assets/Resources/SO/Items/Definitions/facility_kit_9818.asset>)
- [facility_kit_9819](<Assets/Resources/SO/Items/Definitions/facility_kit_9819.asset>)
- [facility_kit_9820](<Assets/Resources/SO/Items/Definitions/facility_kit_9820.asset>)
- [facility_kit_9821](<Assets/Resources/SO/Items/Definitions/facility_kit_9821.asset>)
- [facility_kit_9822](<Assets/Resources/SO/Items/Definitions/facility_kit_9822.asset>)
- [facility_kit_9823](<Assets/Resources/SO/Items/Definitions/facility_kit_9823.asset>)
- [facility_kit_9824](<Assets/Resources/SO/Items/Definitions/facility_kit_9824.asset>)
- [facility_kit_9825](<Assets/Resources/SO/Items/Definitions/facility_kit_9825.asset>)
- [facility_kit_9826](<Assets/Resources/SO/Items/Definitions/facility_kit_9826.asset>)
- [facility_kit_9827](<Assets/Resources/SO/Items/Definitions/facility_kit_9827.asset>)
- [facility_kit_9828](<Assets/Resources/SO/Items/Definitions/facility_kit_9828.asset>)
- [facility_kit_9829](<Assets/Resources/SO/Items/Definitions/facility_kit_9829.asset>)
- [facility_kit_9840](<Assets/Resources/SO/Items/Definitions/facility_kit_9840.asset>)
- [facility_kit_9841](<Assets/Resources/SO/Items/Definitions/facility_kit_9841.asset>)
- [facility_kit_9842](<Assets/Resources/SO/Items/Definitions/facility_kit_9842.asset>)
- [facility_kit_9843](<Assets/Resources/SO/Items/Definitions/facility_kit_9843.asset>)
- [facility_kit_9844](<Assets/Resources/SO/Items/Definitions/facility_kit_9844.asset>)
- [facility_kit_9845](<Assets/Resources/SO/Items/Definitions/facility_kit_9845.asset>)
- [facility_kit_9846](<Assets/Resources/SO/Items/Definitions/facility_kit_9846.asset>)
- [facility_kit_9847](<Assets/Resources/SO/Items/Definitions/facility_kit_9847.asset>)
- [facility_kit_9848](<Assets/Resources/SO/Items/Definitions/facility_kit_9848.asset>)
- [facility_kit_9849](<Assets/Resources/SO/Items/Definitions/facility_kit_9849.asset>)
- [facility_kit_9850](<Assets/Resources/SO/Items/Definitions/facility_kit_9850.asset>)
- [facility_kit_9851](<Assets/Resources/SO/Items/Definitions/facility_kit_9851.asset>)
- [facility_kit_9852](<Assets/Resources/SO/Items/Definitions/facility_kit_9852.asset>)
- [facility_kit_9860](<Assets/Resources/SO/Items/Definitions/facility_kit_9860.asset>)
- [facility_kit_9961](<Assets/Resources/SO/Items/Definitions/facility_kit_9961.asset>)
- [industrial_sludge](<Assets/Resources/SO/Items/Definitions/industrial_sludge.asset>)
- [item_memory_erasure_seal](<Assets/Resources/SO/Items/Definitions/item_memory_erasure_seal.asset>)
- [medicine_blood_pack](<Assets/Resources/SO/Items/Definitions/medicine_blood_pack.asset>)
- [medicine_blood_seal_kit](<Assets/Resources/SO/Items/Definitions/medicine_blood_seal_kit.asset>)
- [medicine_disinfectant](<Assets/Resources/SO/Items/Definitions/medicine_disinfectant.asset>)
- [medicine_field_emergency_kit](<Assets/Resources/SO/Items/Definitions/medicine_field_emergency_kit.asset>)
- [medicine_immunosuppressant](<Assets/Resources/SO/Items/Definitions/medicine_immunosuppressant.asset>)
- [medicine_mana_core_restraint](<Assets/Resources/SO/Items/Definitions/medicine_mana_core_restraint.asset>)
- [medicine_rune_slime_patch](<Assets/Resources/SO/Items/Definitions/medicine_rune_slime_patch.asset>)
- [medicine_temporary_power_bypass](<Assets/Resources/SO/Items/Definitions/medicine_temporary_power_bypass.asset>)
- [medicine_wing_splint_kit](<Assets/Resources/SO/Items/Definitions/medicine_wing_splint_kit.asset>)
- [research_blueprint_6101](<Assets/Resources/SO/Items/Definitions/research_blueprint_6101.asset>)
- [research_blueprint_6102](<Assets/Resources/SO/Items/Definitions/research_blueprint_6102.asset>)
- [research_blueprint_6103](<Assets/Resources/SO/Items/Definitions/research_blueprint_6103.asset>)
- [research_blueprint_6104](<Assets/Resources/SO/Items/Definitions/research_blueprint_6104.asset>)
- [research_blueprint_6191](<Assets/Resources/SO/Items/Definitions/research_blueprint_6191.asset>)
- [research_blueprint_6192](<Assets/Resources/SO/Items/Definitions/research_blueprint_6192.asset>)
- [research_blueprint_6193](<Assets/Resources/SO/Items/Definitions/research_blueprint_6193.asset>)
- [surgery_contaminated_tissue](<Assets/Resources/SO/Items/Definitions/surgery_contaminated_tissue.asset>)
- [surgery_organ_arm_left](<Assets/Resources/SO/Items/Definitions/surgery_organ_arm_left.asset>)
- [surgery_organ_arm_right](<Assets/Resources/SO/Items/Definitions/surgery_organ_arm_right.asset>)
- [surgery_organ_brain](<Assets/Resources/SO/Items/Definitions/surgery_organ_brain.asset>)
- [surgery_organ_core](<Assets/Resources/SO/Items/Definitions/surgery_organ_core.asset>)
- [surgery_organ_eye_left](<Assets/Resources/SO/Items/Definitions/surgery_organ_eye_left.asset>)
- [surgery_organ_eye_right](<Assets/Resources/SO/Items/Definitions/surgery_organ_eye_right.asset>)
- [surgery_organ_heart](<Assets/Resources/SO/Items/Definitions/surgery_organ_heart.asset>)
- [surgery_organ_kidney_left](<Assets/Resources/SO/Items/Definitions/surgery_organ_kidney_left.asset>)
- [surgery_organ_kidney_right](<Assets/Resources/SO/Items/Definitions/surgery_organ_kidney_right.asset>)
- [surgery_organ_leg_left](<Assets/Resources/SO/Items/Definitions/surgery_organ_leg_left.asset>)
- [surgery_organ_leg_right](<Assets/Resources/SO/Items/Definitions/surgery_organ_leg_right.asset>)
- [surgery_organ_liver](<Assets/Resources/SO/Items/Definitions/surgery_organ_liver.asset>)
- [surgery_organ_lung_left](<Assets/Resources/SO/Items/Definitions/surgery_organ_lung_left.asset>)
- [surgery_organ_lung_right](<Assets/Resources/SO/Items/Definitions/surgery_organ_lung_right.asset>)
- [surgery_organ_pseudopods](<Assets/Resources/SO/Items/Definitions/surgery_organ_pseudopods.asset>)
- [surgery_organ_sensory_gel](<Assets/Resources/SO/Items/Definitions/surgery_organ_sensory_gel.asset>)
- [surgery_organ_stomach](<Assets/Resources/SO/Items/Definitions/surgery_organ_stomach.asset>)
- [surgery_prosthetic_arm_left](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_arm_left.asset>)
- [surgery_prosthetic_arm_right](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_arm_right.asset>)
- [surgery_prosthetic_balance-tail](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_balance-tail.asset>)
- [surgery_prosthetic_balance-tail_variant_kobold-tail-balance](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_balance-tail_variant_kobold-tail-balance.asset>)
- [surgery_prosthetic_brain](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_brain.asset>)
- [surgery_prosthetic_brain_variant_human-neural-assist](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_brain_variant_human-neural-assist.asset>)
- [surgery_prosthetic_core](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_core.asset>)
- [surgery_prosthetic_eye_left](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_eye_left.asset>)
- [surgery_prosthetic_eye_right](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_eye_right.asset>)
- [surgery_prosthetic_hand_left](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_hand_left.asset>)
- [surgery_prosthetic_heart](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_heart.asset>)
- [surgery_prosthetic_heart_variant_orc-combat-heart](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_heart_variant_orc-combat-heart.asset>)
- [surgery_prosthetic_heat-sac_variant_demon-heat-sac](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_heat-sac_variant_demon-heat-sac.asset>)
- [surgery_prosthetic_hypha-core](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_hypha-core.asset>)
- [surgery_prosthetic_kidney_left](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_kidney_left.asset>)
- [surgery_prosthetic_kidney_right](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_kidney_right.asset>)
- [surgery_prosthetic_leg_left](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_leg_left.asset>)
- [surgery_prosthetic_leg_left_variant_beastkin-sprint-joint](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_leg_left_variant_beastkin-sprint-joint.asset>)
- [surgery_prosthetic_leg_right](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_leg_right.asset>)
- [surgery_prosthetic_liver](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_liver.asset>)
- [surgery_prosthetic_lung_left](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_lung_left.asset>)
- [surgery_prosthetic_lung_right](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_lung_right.asset>)
- [surgery_prosthetic_night-eye_left](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_night-eye_left.asset>)
- [surgery_prosthetic_pseudopods](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_pseudopods.asset>)
- [surgery_prosthetic_sensory_gel](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_sensory_gel.asset>)
- [surgery_prosthetic_stomach](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_stomach.asset>)
- [surgery_prosthetic_torso](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_torso.asset>)
- [surgery_prosthetic_wing_left](<Assets/Resources/SO/Items/Definitions/surgery_prosthetic_wing_left.asset>)
- [survival_cooked_meal](<Assets/Resources/SO/Items/Definitions/survival_cooked_meal.asset>)
- [survival_preserved_food](<Assets/Resources/SO/Items/Definitions/survival_preserved_food.asset>)
- [survival_raw_food](<Assets/Resources/SO/Items/Definitions/survival_raw_food.asset>)
- [survival_tainted_food](<Assets/Resources/SO/Items/Definitions/survival_tainted_food.asset>)
- [wild_carcass_ash_crawler](<Assets/Resources/SO/Items/Definitions/wild_carcass_ash_crawler.asset>)
- [wild_carcass_ash_goat](<Assets/Resources/SO/Items/Definitions/wild_carcass_ash_goat.asset>)
- [wild_carcass_carrion_drake](<Assets/Resources/SO/Items/Definitions/wild_carcass_carrion_drake.asset>)
- [wild_carcass_cave_hound](<Assets/Resources/SO/Items/Definitions/wild_carcass_cave_hound.asset>)
- [wild_carcass_cave_rat](<Assets/Resources/SO/Items/Definitions/wild_carcass_cave_rat.asset>)
- [wild_carcass_crystal_beetle](<Assets/Resources/SO/Items/Definitions/wild_carcass_crystal_beetle.asset>)
- [wild_carcass_deep_goat](<Assets/Resources/SO/Items/Definitions/wild_carcass_deep_goat.asset>)
- [wild_carcass_ember_lizard](<Assets/Resources/SO/Items/Definitions/wild_carcass_ember_lizard.asset>)
- [wild_carcass_frost_ram](<Assets/Resources/SO/Items/Definitions/wild_carcass_frost_ram.asset>)
- [wild_carcass_glow_moth](<Assets/Resources/SO/Items/Definitions/wild_carcass_glow_moth.asset>)
- [wild_carcass_mana_wisp](<Assets/Resources/SO/Items/Definitions/wild_carcass_mana_wisp.asset>)
- [wild_carcass_mire_leech](<Assets/Resources/SO/Items/Definitions/wild_carcass_mire_leech.asset>)
- [wild_carcass_moss_boar](<Assets/Resources/SO/Items/Definitions/wild_carcass_moss_boar.asset>)
- [wild_carcass_rune_deer](<Assets/Resources/SO/Items/Definitions/wild_carcass_rune_deer.asset>)
- [wild_carcass_shadow_hare](<Assets/Resources/SO/Items/Definitions/wild_carcass_shadow_hare.asset>)
- [wild_carcass_shadow_wolf](<Assets/Resources/SO/Items/Definitions/wild_carcass_shadow_wolf.asset>)
- [wild_carcass_silk_spider](<Assets/Resources/SO/Items/Definitions/wild_carcass_silk_spider.asset>)
- [wild_carcass_spore_elk](<Assets/Resources/SO/Items/Definitions/wild_carcass_spore_elk.asset>)
- [wild_carcass_tunnel_mole](<Assets/Resources/SO/Items/Definitions/wild_carcass_tunnel_mole.asset>)
- [wild_carcass_twilight_chicken](<Assets/Resources/SO/Items/Definitions/wild_carcass_twilight_chicken.asset>)
- [wild_rot](<Assets/Resources/SO/Items/Definitions/wild_rot.asset>)
- [relic_beastkin_1](<Assets/Resources/SO/V20/Items/FactionRelics/relic_beastkin_1.asset>)
- [relic_beastkin_2](<Assets/Resources/SO/V20/Items/FactionRelics/relic_beastkin_2.asset>)
- [relic_beastkin_3](<Assets/Resources/SO/V20/Items/FactionRelics/relic_beastkin_3.asset>)
- [relic_demon_1](<Assets/Resources/SO/V20/Items/FactionRelics/relic_demon_1.asset>)
- [relic_demon_2](<Assets/Resources/SO/V20/Items/FactionRelics/relic_demon_2.asset>)
- [relic_demon_3](<Assets/Resources/SO/V20/Items/FactionRelics/relic_demon_3.asset>)
- [relic_golem_1](<Assets/Resources/SO/V20/Items/FactionRelics/relic_golem_1.asset>)
- [relic_golem_2](<Assets/Resources/SO/V20/Items/FactionRelics/relic_golem_2.asset>)
- [relic_golem_3](<Assets/Resources/SO/V20/Items/FactionRelics/relic_golem_3.asset>)
- [relic_harpy_1](<Assets/Resources/SO/V20/Items/FactionRelics/relic_harpy_1.asset>)
- [relic_harpy_2](<Assets/Resources/SO/V20/Items/FactionRelics/relic_harpy_2.asset>)
- [relic_harpy_3](<Assets/Resources/SO/V20/Items/FactionRelics/relic_harpy_3.asset>)
- [relic_kobold_1](<Assets/Resources/SO/V20/Items/FactionRelics/relic_kobold_1.asset>)
- [relic_kobold_2](<Assets/Resources/SO/V20/Items/FactionRelics/relic_kobold_2.asset>)
- [relic_kobold_3](<Assets/Resources/SO/V20/Items/FactionRelics/relic_kobold_3.asset>)
- [relic_myconid_1](<Assets/Resources/SO/V20/Items/FactionRelics/relic_myconid_1.asset>)
- [relic_myconid_2](<Assets/Resources/SO/V20/Items/FactionRelics/relic_myconid_2.asset>)
- [relic_myconid_3](<Assets/Resources/SO/V20/Items/FactionRelics/relic_myconid_3.asset>)

</details>

<details>
<summary>GuestRequestDefinitionSO — 14개</summary>

- [guest-request_allergen-banquet](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_allergen-banquet.asset>)
- [guest-request_bodyguard-kit](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_bodyguard-kit.asset>)
- [guest-request_coronation-feast](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_coronation-feast.asset>)
- [guest-request_disease-sample](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_disease-sample.asset>)
- [guest-request_emergency-surgery](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_emergency-surgery.asset>)
- [guest-request_flood-refuge](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_flood-refuge.asset>)
- [guest-request_memorial-performance](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_memorial-performance.asset>)
- [guest-request_militia-arms](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_militia-arms.asset>)
- [guest-request_monster-circus](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_monster-circus.asset>)
- [guest-request_persecuted-family](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_persecuted-family.asset>)
- [guest-request_plague-screening](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_plague-screening.asset>)
- [guest-request_precision-barter](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_precision-barter.asset>)
- [guest-request_sealed-archive](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_sealed-archive.asset>)
- [guest-request_winter-fuel-auction](<Assets/Resources/SO/V20/Society/Services/GuestRequests/guest-request_winter-fuel-auction.asset>)

</details>

<details>
<summary>HeritableTraitDefinitionSO — 24개</summary>

- [heritable_abundant-seed](<Assets/Resources/SO/V20/Traits/Heritable/heritable_abundant-seed.asset>)
- [heritable_broad-immunity](<Assets/Resources/SO/V20/Traits/Heritable/heritable_broad-immunity.asset>)
- [heritable_cold-blood-adaptation](<Assets/Resources/SO/V20/Traits/Heritable/heritable_cold-blood-adaptation.asset>)
- [heritable_cross-lineage-tolerance](<Assets/Resources/SO/V20/Traits/Heritable/heritable_cross-lineage-tolerance.asset>)
- [heritable_dense-bone](<Assets/Resources/SO/V20/Traits/Heritable/heritable_dense-bone.asset>)
- [heritable_dream-reception](<Assets/Resources/SO/V20/Traits/Heritable/heritable_dream-reception.asset>)
- [heritable_efficient-digestion](<Assets/Resources/SO/V20/Traits/Heritable/heritable_efficient-digestion.asset>)
- [heritable_expanded-lung](<Assets/Resources/SO/V20/Traits/Heritable/heritable_expanded-lung.asset>)
- [heritable_flexible-joints](<Assets/Resources/SO/V20/Traits/Heritable/heritable_flexible-joints.asset>)
- [heritable_focused-antibodies](<Assets/Resources/SO/V20/Traits/Heritable/heritable_focused-antibodies.asset>)
- [heritable_heat-shedding](<Assets/Resources/SO/V20/Traits/Heritable/heritable_heat-shedding.asset>)
- [heritable_hollow-bone](<Assets/Resources/SO/V20/Traits/Heritable/heritable_hollow-bone.asset>)
- [heritable_mana-grounding](<Assets/Resources/SO/V20/Traits/Heritable/heritable_mana-grounding.asset>)
- [heritable_mana-reservoir](<Assets/Resources/SO/V20/Traits/Heritable/heritable_mana-reservoir.asset>)
- [heritable_rapid-burn](<Assets/Resources/SO/V20/Traits/Heritable/heritable_rapid-burn.asset>)
- [heritable_rapid-repair](<Assets/Resources/SO/V20/Traits/Heritable/heritable_rapid-repair.asset>)
- [heritable_regrowing-tissue](<Assets/Resources/SO/V20/Traits/Heritable/heritable_regrowing-tissue.asset>)
- [heritable_reinforced-joints](<Assets/Resources/SO/V20/Traits/Heritable/heritable_reinforced-joints.asset>)
- [heritable_rune-sight](<Assets/Resources/SO/V20/Traits/Heritable/heritable_rune-sight.asset>)
- [heritable_slow-aging](<Assets/Resources/SO/V20/Traits/Heritable/heritable_slow-aging.asset>)
- [heritable_slow-fertility](<Assets/Resources/SO/V20/Traits/Heritable/heritable_slow-fertility.asset>)
- [heritable_stable-gestation](<Assets/Resources/SO/V20/Traits/Heritable/heritable_stable-gestation.asset>)
- [heritable_toxin-filter](<Assets/Resources/SO/V20/Traits/Heritable/heritable_toxin-filter.asset>)
- [heritable_water-retention](<Assets/Resources/SO/V20/Traits/Heritable/heritable_water-retention.asset>)

</details>

<details>
<summary>InGameNarrativeTextCatalogSO — 1개</summary>

- [InGameNarrativeTextCatalog](<Assets/Resources/SO/InGameNarrativeTextCatalog.asset>)

</details>

<details>
<summary>ItemDefinitionCatalogSO — 1개</summary>

- [ItemDefinitionCatalog](<Assets/Resources/SO/Content/ItemDefinitionCatalog.asset>)

</details>

<details>
<summary>ItemHaulingSettingsSO — 1개</summary>

- [ItemHaulingSettings](<Assets/Resources/SO/Items/ItemHaulingSettings.asset>)

</details>

<details>
<summary>LifeEventDefinitionSO — 32개</summary>

- [life-event_apprentice-mistake](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_apprentice-mistake.asset>)
- [life-event_captains-test](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_captains-test.asset>)
- [life-event_childhood-bully](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_childhood-bully.asset>)
- [life-event_clinic-shortage](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_clinic-shortage.asset>)
- [life-event_cultural-petition](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_cultural-petition.asset>)
- [life-event_dangerous-friendship](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_dangerous-friendship.asset>)
- [life-event_disputed-thesis](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_disputed-thesis.asset>)
- [life-event_elder-birthday](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_elder-birthday.asset>)
- [life-event_family-room](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_family-room.asset>)
- [life-event_first-forbidden-door](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_first-forbidden-door.asset>)
- [life-event_first-lost-tooth](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_first-lost-tooth.asset>)
- [life-event_first-safe-task](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_first-safe-task.asset>)
- [life-event_foundling-question](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_foundling-question.asset>)
- [life-event_grave-visit](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_grave-visit.asset>)
- [life-event_guardian-oath](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_guardian-oath.asset>)
- [life-event_household-meal](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_household-meal.asset>)
- [life-event_inherited-debt](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_inherited-debt.asset>)
- [life-event_killer-sighted](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_killer-sighted.asset>)
- [life-event_last-lesson](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_last-lesson.asset>)
- [life-event_lineage-relic](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_lineage-relic.asset>)
- [life-event_masterpiece-commission](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_masterpiece-commission.asset>)
- [life-event_mentor-favor](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_mentor-favor.asset>)
- [life-event_newborn-welcome](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_newborn-welcome.asset>)
- [life-event_position-rivalry](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_position-rivalry.asset>)
- [life-event_quiet-promotion](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_quiet-promotion.asset>)
- [life-event_retiree-story](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_retiree-story.asset>)
- [life-event_retirement-request](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_retirement-request.asset>)
- [life-event_shared-lullaby](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_shared-lullaby.asset>)
- [life-event_shift-saved](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_shift-saved.asset>)
- [life-event_stolen-design](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_stolen-design.asset>)
- [life-event_story-compressed](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_story-compressed.asset>)
- [life-event_tool-inheritance](<Assets/Resources/SO/V20/Narrative/LifeEvents/life-event_tool-inheritance.asset>)

</details>

<details>
<summary>OffenseCampaignCatalogSO — 1개</summary>

- [OffenseCampaignCatalog](<Assets/Resources/SO/Content/OffenseCampaignCatalog.asset>)

</details>

<details>
<summary>OffenseDecisionCardSO — 54개</summary>

- [01_travel_broken_bridge](<Assets/Resources/SO/Offense/Cards/01_travel_broken_bridge.asset>)
- [02_travel_toll_gate](<Assets/Resources/SO/Offense/Cards/02_travel_toll_gate.asset>)
- [03_travel_wounded_scout](<Assets/Resources/SO/Offense/Cards/03_travel_wounded_scout.asset>)
- [04_travel_black_rain](<Assets/Resources/SO/Offense/Cards/04_travel_black_rain.asset>)
- [05_travel_false_milestone](<Assets/Resources/SO/Offense/Cards/05_travel_false_milestone.asset>)
- [06_travel_abandoned_cart](<Assets/Resources/SO/Offense/Cards/06_travel_abandoned_cart.asset>)
- [07_travel_narrow_pass](<Assets/Resources/SO/Offense/Cards/07_travel_narrow_pass.asset>)
- [08_recon_smoke_column](<Assets/Resources/SO/Offense/Cards/08_recon_smoke_column.asset>)
- [09_recon_watch_rotation](<Assets/Resources/SO/Offense/Cards/09_recon_watch_rotation.asset>)
- [10_recon_buried_map](<Assets/Resources/SO/Offense/Cards/10_recon_buried_map.asset>)
- [11_recon_prisoner_signal](<Assets/Resources/SO/Offense/Cards/11_recon_prisoner_signal.asset>)
- [12_recon_old_runes](<Assets/Resources/SO/Offense/Cards/12_recon_old_runes.asset>)
- [13_recon_counter_scout](<Assets/Resources/SO/Offense/Cards/13_recon_counter_scout.asset>)
- [14_recon_supply_marks](<Assets/Resources/SO/Offense/Cards/14_recon_supply_marks.asset>)
- [15_negotiation_border_merchant](<Assets/Resources/SO/Offense/Cards/15_negotiation_border_merchant.asset>)
- [16_negotiation_deserter](<Assets/Resources/SO/Offense/Cards/16_negotiation_deserter.asset>)
- [17_negotiation_hostage_exchange](<Assets/Resources/SO/Offense/Cards/17_negotiation_hostage_exchange.asset>)
- [18_negotiation_false_relic](<Assets/Resources/SO/Offense/Cards/18_negotiation_false_relic.asset>)
- [19_negotiation_gate_captain](<Assets/Resources/SO/Offense/Cards/19_negotiation_gate_captain.asset>)
- [20_negotiation_competing_raiders](<Assets/Resources/SO/Offense/Cards/20_negotiation_competing_raiders.asset>)
- [21_negotiation_choir_envoy](<Assets/Resources/SO/Offense/Cards/21_negotiation_choir_envoy.asset>)
- [22_infiltration_drain](<Assets/Resources/SO/Offense/Cards/22_infiltration_drain.asset>)
- [23_infiltration_rope_wall](<Assets/Resources/SO/Offense/Cards/23_infiltration_rope_wall.asset>)
- [24_infiltration_service_cart](<Assets/Resources/SO/Offense/Cards/24_infiltration_service_cart.asset>)
- [25_infiltration_sleeping_hounds](<Assets/Resources/SO/Offense/Cards/25_infiltration_sleeping_hounds.asset>)
- [26_infiltration_signal_bell](<Assets/Resources/SO/Offense/Cards/26_infiltration_signal_bell.asset>)
- [27_infiltration_powder_store](<Assets/Resources/SO/Offense/Cards/27_infiltration_powder_store.asset>)
- [28_infiltration_locked_archive](<Assets/Resources/SO/Offense/Cards/28_infiltration_locked_archive.asset>)
- [29_camp_dry_cave](<Assets/Resources/SO/Offense/Cards/29_camp_dry_cave.asset>)
- [30_camp_night_watch](<Assets/Resources/SO/Offense/Cards/30_camp_night_watch.asset>)
- [31_camp_shared_ration](<Assets/Resources/SO/Offense/Cards/31_camp_shared_ration.asset>)
- [32_camp_weapon_care](<Assets/Resources/SO/Offense/Cards/32_camp_weapon_care.asset>)
- [33_camp_bad_dream](<Assets/Resources/SO/Offense/Cards/33_camp_bad_dream.asset>)
- [34_camp_local_herbs](<Assets/Resources/SO/Offense/Cards/34_camp_local_herbs.asset>)
- [35_camp_distant_horns](<Assets/Resources/SO/Offense/Cards/35_camp_distant_horns.asset>)
- [36_loot_locked_chest](<Assets/Resources/SO/Offense/Cards/36_loot_locked_chest.asset>)
- [37_loot_wounded_enemy](<Assets/Resources/SO/Offense/Cards/37_loot_wounded_enemy.asset>)
- [38_loot_burning_store](<Assets/Resources/SO/Offense/Cards/38_loot_burning_store.asset>)
- [39_loot_cursed_weapon](<Assets/Resources/SO/Offense/Cards/39_loot_cursed_weapon.asset>)
- [40_loot_prison_records](<Assets/Resources/SO/Offense/Cards/40_loot_prison_records.asset>)
- [41_loot_enemy_banner](<Assets/Resources/SO/Offense/Cards/41_loot_enemy_banner.asset>)
- [42_loot_hidden_cellar](<Assets/Resources/SO/Offense/Cards/42_loot_hidden_cellar.asset>)
- [43_return_following_tracks](<Assets/Resources/SO/Offense/Cards/43_return_following_tracks.asset>)
- [44_return_flooded_road](<Assets/Resources/SO/Offense/Cards/44_return_flooded_road.asset>)
- [45_return_broken_pack](<Assets/Resources/SO/Offense/Cards/45_return_broken_pack.asset>)
- [46_return_victory_song](<Assets/Resources/SO/Offense/Cards/46_return_victory_song.asset>)
- [47_return_field_surgery](<Assets/Resources/SO/Offense/Cards/47_return_field_surgery.asset>)
- [48_return_last_toll](<Assets/Resources/SO/Offense/Cards/48_return_last_toll.asset>)
- [49_return_home_lights](<Assets/Resources/SO/Offense/Cards/49_return_home_lights.asset>)
- [50_travel_rain_drift_cargo](<Assets/Resources/SO/Offense/Cards/50_travel_rain_drift_cargo.asset>)
- [51_travel_fog_guide](<Assets/Resources/SO/Offense/Cards/51_travel_fog_guide.asset>)
- [52_travel_heatwave_shade_shelter](<Assets/Resources/SO/Offense/Cards/52_travel_heatwave_shade_shelter.asset>)
- [53_travel_cold_snap_frost_camp](<Assets/Resources/SO/Offense/Cards/53_travel_cold_snap_frost_camp.asset>)
- [54_travel_storm_exposed_hideout](<Assets/Resources/SO/Offense/Cards/54_travel_storm_exposed_hideout.asset>)

</details>

<details>
<summary>OffenseEncounterSO — 36개</summary>

- [encounter_01](<Assets/Resources/SO/Offense/Encounters/encounter_01.asset>)
- [encounter_02](<Assets/Resources/SO/Offense/Encounters/encounter_02.asset>)
- [encounter_03](<Assets/Resources/SO/Offense/Encounters/encounter_03.asset>)
- [encounter_04](<Assets/Resources/SO/Offense/Encounters/encounter_04.asset>)
- [encounter_05](<Assets/Resources/SO/Offense/Encounters/encounter_05.asset>)
- [encounter_06](<Assets/Resources/SO/Offense/Encounters/encounter_06.asset>)
- [encounter_07](<Assets/Resources/SO/V20/Combat/Encounters/encounter_07.asset>)
- [encounter_08](<Assets/Resources/SO/V20/Combat/Encounters/encounter_08.asset>)
- [encounter_09](<Assets/Resources/SO/V20/Combat/Encounters/encounter_09.asset>)
- [encounter_10](<Assets/Resources/SO/V20/Combat/Encounters/encounter_10.asset>)
- [encounter_11](<Assets/Resources/SO/V20/Combat/Encounters/encounter_11.asset>)
- [encounter_12](<Assets/Resources/SO/V20/Combat/Encounters/encounter_12.asset>)
- [encounter_13](<Assets/Resources/SO/V20/Combat/Encounters/encounter_13.asset>)
- [encounter_14](<Assets/Resources/SO/V20/Combat/Encounters/encounter_14.asset>)
- [encounter_15](<Assets/Resources/SO/V20/Combat/Encounters/encounter_15.asset>)
- [encounter_16](<Assets/Resources/SO/V20/Combat/Encounters/encounter_16.asset>)
- [encounter_17](<Assets/Resources/SO/V20/Combat/Encounters/encounter_17.asset>)
- [encounter_18](<Assets/Resources/SO/V20/Combat/Encounters/encounter_18.asset>)
- [encounter_19](<Assets/Resources/SO/V20/Combat/Encounters/encounter_19.asset>)
- [encounter_20](<Assets/Resources/SO/V20/Combat/Encounters/encounter_20.asset>)
- [encounter_21](<Assets/Resources/SO/V20/Combat/Encounters/encounter_21.asset>)
- [encounter_22](<Assets/Resources/SO/V20/Combat/Encounters/encounter_22.asset>)
- [encounter_23](<Assets/Resources/SO/V20/Combat/Encounters/encounter_23.asset>)
- [encounter_24](<Assets/Resources/SO/V20/Combat/Encounters/encounter_24.asset>)
- [encounter_25](<Assets/Resources/SO/V20/Combat/Encounters/encounter_25.asset>)
- [encounter_26](<Assets/Resources/SO/V20/Combat/Encounters/encounter_26.asset>)
- [encounter_27](<Assets/Resources/SO/V20/Combat/Encounters/encounter_27.asset>)
- [encounter_28](<Assets/Resources/SO/V20/Combat/Encounters/encounter_28.asset>)
- [encounter_29](<Assets/Resources/SO/V20/Combat/Encounters/encounter_29.asset>)
- [encounter_30](<Assets/Resources/SO/V20/Combat/Encounters/encounter_30.asset>)
- [encounter_31](<Assets/Resources/SO/V20/Combat/Encounters/encounter_31.asset>)
- [encounter_32](<Assets/Resources/SO/V20/Combat/Encounters/encounter_32.asset>)
- [encounter_33](<Assets/Resources/SO/V20/Combat/Encounters/encounter_33.asset>)
- [encounter_34](<Assets/Resources/SO/V20/Combat/Encounters/encounter_34.asset>)
- [encounter_35](<Assets/Resources/SO/V20/Combat/Encounters/encounter_35.asset>)
- [encounter_36](<Assets/Resources/SO/V20/Combat/Encounters/encounter_36.asset>)

</details>

<details>
<summary>OffenseSiteArchetypeSO — 12개</summary>

- [archive](<Assets/Resources/SO/Offense/Sites/archive.asset>)
- [armory](<Assets/Resources/SO/Offense/Sites/armory.asset>)
- [caravan](<Assets/Resources/SO/Offense/Sites/caravan.asset>)
- [farm](<Assets/Resources/SO/Offense/Sites/farm.asset>)
- [fixed_rival-dungeon](<Assets/Resources/SO/Offense/Sites/fixed_rival-dungeon.asset>)
- [fixed_truth-core](<Assets/Resources/SO/Offense/Sites/fixed_truth-core.asset>)
- [patrol](<Assets/Resources/SO/Offense/Sites/patrol.asset>)
- [prisoner_convoy](<Assets/Resources/SO/Offense/Sites/prisoner_convoy.asset>)
- [quarry](<Assets/Resources/SO/Offense/Sites/quarry.asset>)
- [ritual_site](<Assets/Resources/SO/Offense/Sites/ritual_site.asset>)
- [ruin](<Assets/Resources/SO/Offense/Sites/ruin.asset>)
- [watchtower](<Assets/Resources/SO/Offense/Sites/watchtower.asset>)

</details>

<details>
<summary>OffenseUrgentSiteDefinitionSO — 6개</summary>

- [defense_disruptor](<Assets/Resources/SO/Offense/UrgentSites/defense_disruptor.asset>)
- [dissonant_choir](<Assets/Resources/SO/Offense/UrgentSites/dissonant_choir.asset>)
- [heat_emitter](<Assets/Resources/SO/Offense/UrgentSites/heat_emitter.asset>)
- [rot_fog_nest](<Assets/Resources/SO/Offense/UrgentSites/rot_fog_nest.asset>)
- [shadow_screen](<Assets/Resources/SO/Offense/UrgentSites/shadow_screen.asset>)
- [siege_observer](<Assets/Resources/SO/Offense/UrgentSites/siege_observer.asset>)

</details>

<details>
<summary>ProductionRecipeSO — 367개</summary>

- [V3R01_깨끗한_물](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R01_깨끗한_물.asset>)
- [V3R02_황](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R02_황.asset>)
- [V3R03_납광석](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R03_납광석.asset>)
- [V3R04_초석](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R04_초석.asset>)
- [V3R05_종이](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R05_종이.asset>)
- [V3R06_납괴](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R06_납괴.asset>)
- [V3R07_납탄](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R07_납탄.asset>)
- [V3R08_밧줄](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R08_밧줄.asset>)
- [V3R09_기계_부품](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R09_기계_부품.asset>)
- [V3R100_강화_구속구](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R100_강화_구속구.asset>)
- [V3R101_공연_소품_상자](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R101_공연_소품_상자.asset>)
- [V3R102_연회_운반_수레](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R102_연회_운반_수레.asset>)
- [V3R103_밀폐형_계절_보관함](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R103_밀폐형_계절_보관함.asset>)
- [V3R104_동맹_신호_키트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R104_동맹_신호_키트.asset>)
- [V3R105_복도식_기폭_장치](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R105_복도식_기폭_장치.asset>)
- [V3R106_룬_식별_렌즈](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R106_룬_식별_렌즈.asset>)
- [V3R107_공성_보강_키트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R107_공성_보강_키트.asset>)
- [V3R108_경계_신호_나팔](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R108_경계_신호_나팔.asset>)
- [V3R109_재료_시험편](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R109_재료_시험편.asset>)
- [V3R10_정밀_부품](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R10_정밀_부품.asset>)
- [V3R110_검사_게이지](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R110_검사_게이지.asset>)
- [V3R111_절연_배선_묶음](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R111_절연_배선_묶음.asset>)
- [V3R112_수차_구동축](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R112_수차_구동축.asset>)
- [V3R113_재생수_필터_카트리지](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R113_재생수_필터_카트리지.asset>)
- [V3R114_룬_정화_결정](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R114_룬_정화_결정.asset>)
- [V3R115_형질_검사_키트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R115_형질_검사_키트.asset>)
- [V3R116_교차계통_안정화_배지](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R116_교차계통_안정화_배지.asset>)
- [V3R117_격리_치료_꾸러미](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R117_격리_치료_꾸러미.asset>)
- [V3R118_회춘_혈청](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R118_회춘_혈청.asset>)
- [V3R119_골렘_핵_케이스](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R119_골렘_핵_케이스.asset>)
- [V3R11_룬_도체](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R11_룬_도체.asset>)
- [V3R120_장기_보존_용기](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R120_장기_보존_용기.asset>)
- [V3R121_장기_재생_골격](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R121_장기_재생_골격.asset>)
- [V3R122_재생_배양액](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R122_재생_배양액.asset>)
- [V3R123_생식_치료제](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R123_생식_치료제.asset>)
- [V3R124_룬_동면_촉매](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R124_룬_동면_촉매.asset>)
- [V3R125_트라우마_치료_꾸러미](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R125_트라우마_치료_꾸러미.asset>)
- [V3R126_소이_화살](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R126_소이_화살.asset>)
- [V3R127_소이_볼트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R127_소이_볼트.asset>)
- [V3R128_연막_탄약](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R128_연막_탄약.asset>)
- [V3R129_철갑_탄약](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R129_철갑_탄약.asset>)
- [V3R12_무균_천](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R12_무균_천.asset>)
- [V3R130_산탄_탄약](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R130_산탄_탄약.asset>)
- [V3R131_신호탄](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R131_신호탄.asset>)
- [V3R132_흑강_볼트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R132_흑강_볼트.asset>)
- [V3R133_룬_탄약통](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R133_룬_탄약통.asset>)
- [V3R134_진정_다트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R134_진정_다트.asset>)
- [V3R135_마나_차단_볼트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R135_마나_차단_볼트.asset>)
- [V3R136_방어시설용_혼합_탄약_상자](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R136_방어시설용_혼합_탄약_상자.asset>)
- [V3R13_흑색화약](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R13_흑색화약.asset>)
- [V3R14_층상_충전재](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R14_층상_충전재.asset>)
- [V3R15_성장형_장비_골격](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R15_성장형_장비_골격.asset>)
- [V3R16_질산_비료](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R16_질산_비료.asset>)
- [V3R17_공학_도면](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R17_공학_도면.asset>)
- [V3R18_납_균형추](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R18_납_균형추.asset>)
- [V3R19_마나_차폐판](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R19_마나_차폐판.asset>)
- [V3R20_발파_장약](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R20_발파_장약.asset>)
- [V3R21_함정_산탄통](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R21_함정_산탄통.asset>)
- [V3R22_무균_붕대](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R22_무균_붕대.asset>)
- [V3R23_종이_탄약통](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R23_종이_탄약통.asset>)
- [V3R24_재고_감지반](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R24_재고_감지반.asset>)
- [V3R25_정비_키트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R25_정비_키트.asset>)
- [V3R26_동력_공구날](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R26_동력_공구날.asset>)
- [V3R27_마나_탐침](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R27_마나_탐침.asset>)
- [V3R28_정밀_게이지](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R28_정밀_게이지.asset>)
- [V3R29_시제품_설계_묶음](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R29_시제품_설계_묶음.asset>)
- [V3R30_공장_설치_도면](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R30_공장_설치_도면.asset>)
- [V3R31_공성_균형추_조립품](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R31_공성_균형추_조립품.asset>)
- [V3R32_룬_조율_차폐판](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R32_룬_조율_차폐판.asset>)
- [V3R33_마핵_케이스](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R33_마핵_케이스.asset>)
- [V3R34_룬_제어반](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R34_룬_제어반.asset>)
- [V3R35_룬_버스_결합기](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R35_룬_버스_결합기.asset>)
- [V3R36_무균_균사_이식편](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R36_무균_균사_이식편.asset>)
- [V3R37_점액_응고틀](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R37_점액_응고틀.asset>)
- [V3R38_방폭_외투_내피](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R38_방폭_외투_내피.asset>)
- [V3R39_브리간딘_안감](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R39_브리간딘_안감.asset>)
- [V3R40_심부_승강기](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R40_심부_승강기.asset>)
- [V3R41_탐광_키트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R41_탐광_키트.asset>)
- [V3R42_합금_도가니](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R42_합금_도가니.asset>)
- [V3R43_야전_수리_키트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R43_야전_수리_키트.asset>)
- [V3R44_흑강_방어_장갑판](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R44_흑강_방어_장갑판.asset>)
- [V3R45_동력_갑주_관절](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R45_동력_갑주_관절.asset>)
- [V3R46_몽직물_룬_안감](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R46_몽직물_룬_안감.asset>)
- [V3R47_몽직물_의식_장식](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R47_몽직물_의식_장식.asset>)
- [V3R48_룬가죽_장비_안감](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R48_룬가죽_장비_안감.asset>)
- [V3R49_룬가죽_조율_끈](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R49_룬가죽_조율_끈.asset>)
- [V3R50_독성_함정_도포제](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R50_독성_함정_도포제.asset>)
- [V3R51_종이_풀칠](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R51_종이_풀칠.asset>)
- [V3R52_직물_경화제](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R52_직물_경화제.asset>)
- [V3R53_균사_재배_배지](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R53_균사_재배_배지.asset>)
- [V3R54_전신_재생_배지](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R54_전신_재생_배지.asset>)
- [V3R55_시간_고정_인장](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R55_시간_고정_인장.asset>)
- [V3R56_해충_유인제](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R56_해충_유인제.asset>)
- [V3R57_식물성_살충제](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R57_식물성_살충제.asset>)
- [V3R58_살균제](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R58_살균제.asset>)
- [V3R66_동굴_독감_백신](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R66_동굴_독감_백신.asset>)
- [V3R67_적열병_백신](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R67_적열병_백신.asset>)
- [V3R68_장부패증_백신](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R68_장부패증_백신.asset>)
- [V3R69_포자폐증_백신](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R69_포자폐증_백신.asset>)
- [V3R70_마나두창_백신](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R70_마나두창_백신.asset>)
- [V3R71_혈액소모병_백신](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R71_혈액소모병_백신.asset>)
- [V3R72_점액역병_백신](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R72_점액역병_백신.asset>)
- [V3R73_적층_목재](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R73_적층_목재.asset>)
- [V3R74_경화_가죽](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R74_경화_가죽.asset>)
- [V3R75_사슬_망](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R75_사슬_망.asset>)
- [V3R76_판금_소재판](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R76_판금_소재판.asset>)
- [V3R77_용수철강](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R77_용수철강.asset>)
- [V3R78_총열강](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R78_총열강.asset>)
- [V3R79_과립_화약](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R79_과립_화약.asset>)
- [V3R80_탄약지](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R80_탄약지.asset>)
- [V3R81_절연_직물](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R81_절연_직물.asset>)
- [V3R82_정밀_광학계](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R82_정밀_광학계.asset>)
- [V3R83_마나_합금](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R83_마나_합금.asset>)
- [V3R84_무균_복합재](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R84_무균_복합재.asset>)
- [V3R85_인증_품종_종자_꾸러미](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R85_인증_품종_종자_꾸러미.asset>)
- [V3R86_온실_영양액](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R86_온실_영양액.asset>)
- [V3R87_기후_제어_매니폴드](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R87_기후_제어_매니폴드.asset>)
- [V3R88_기상_관측_도구함](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R88_기상_관측_도구함.asset>)
- [V3R89_계절력_책자](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R89_계절력_책자.asset>)
- [V3R90_접종_원목](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R90_접종_원목.asset>)
- [V3R91_비전_색인철](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R91_비전_색인철.asset>)
- [V3R92_행정_인장](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R92_행정_인장.asset>)
- [V3R93_방_칸막이_키트](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R93_방_칸막이_키트.asset>)
- [V3R94_경력_장부](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R94_경력_장부.asset>)
- [V3R95_번식_장부](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R95_번식_장부.asset>)
- [V3R96_종족_장례_준비품](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R96_종족_장례_준비품.asset>)
- [V3R97_운반_멜빵](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R97_운반_멜빵.asset>)
- [V3R98_가격표_게시판](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R98_가격표_게시판.asset>)
- [V3R99_포로_작업_도구](<Assets/Resources/SO/Economy/Recipes/ResearchOverhaul/V3R99_포로_작업_도구.asset>)
- [recipe_v22_apparel_apron](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_apron.asset>)
- [recipe_v22_apparel_belt](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_belt.asset>)
- [recipe_v22_apparel_blouse](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_blouse.asset>)
- [recipe_v22_apparel_boots](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_boots.asset>)
- [recipe_v22_apparel_ceremonial_dress](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_ceremonial_dress.asset>)
- [recipe_v22_apparel_chest_wrap](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_chest_wrap.asset>)
- [recipe_v22_apparel_cloak](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_cloak.asset>)
- [recipe_v22_apparel_cold_work_suit](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_cold_work_suit.asset>)
- [recipe_v22_apparel_contract_sash](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_contract_sash.asset>)
- [recipe_v22_apparel_daily_robe](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_daily_robe.asset>)
- [recipe_v22_apparel_envoy_coat](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_envoy_coat.asset>)
- [recipe_v22_apparel_farmer_workwear](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_farmer_workwear.asset>)
- [recipe_v22_apparel_festival_vest](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_festival_vest.asset>)
- [recipe_v22_apparel_footwraps](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_footwraps.asset>)
- [recipe_v22_apparel_formal_coat](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_formal_coat.asset>)
- [recipe_v22_apparel_gloves](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_gloves.asset>)
- [recipe_v22_apparel_golem_functional_lining](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_golem_functional_lining.asset>)
- [recipe_v22_apparel_hat](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_hat.asset>)
- [recipe_v22_apparel_hauling_harness](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_hauling_harness.asset>)
- [recipe_v22_apparel_heat_work_suit](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_heat_work_suit.asset>)
- [recipe_v22_apparel_hooded_robe](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_hooded_robe.asset>)
- [recipe_v22_apparel_horn_ring](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_horn_ring.asset>)
- [recipe_v22_apparel_keeper_coat](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_keeper_coat.asset>)
- [recipe_v22_apparel_loincloth_underwear](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_loincloth_underwear.asset>)
- [recipe_v22_apparel_long_underpants](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_long_underpants.asset>)
- [recipe_v22_apparel_lower_underwear](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_lower_underwear.asset>)
- [recipe_v22_apparel_miner_workwear](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_miner_workwear.asset>)
- [recipe_v22_apparel_mourning_clothes](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_mourning_clothes.asset>)
- [recipe_v22_apparel_raincoat](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_raincoat.asset>)
- [recipe_v22_apparel_ritual_robe](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_ritual_robe.asset>)
- [recipe_v22_apparel_rune_cold_suit](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_rune_cold_suit.asset>)
- [recipe_v22_apparel_scarf](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_scarf.asset>)
- [recipe_v22_apparel_shorts](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_shorts.asset>)
- [recipe_v22_apparel_skirt](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_skirt.asset>)
- [recipe_v22_apparel_sky_chorus_shawl](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_sky_chorus_shawl.asset>)
- [recipe_v22_apparel_sleep_bottom](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_sleep_bottom.asset>)
- [recipe_v22_apparel_sleep_top](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_sleep_top.asset>)
- [recipe_v22_apparel_slime_warming_pad](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_slime_warming_pad.asset>)
- [recipe_v22_apparel_smith_apron](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_smith_apron.asset>)
- [recipe_v22_apparel_smoke_protection_hood](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_smoke_protection_hood.asset>)
- [recipe_v22_apparel_socks](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_socks.asset>)
- [recipe_v22_apparel_spore_garden_cloak](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_spore_garden_cloak.asset>)
- [recipe_v22_apparel_spore_protection_hood](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_spore_protection_hood.asset>)
- [recipe_v22_apparel_sterile_gown](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_sterile_gown.asset>)
- [recipe_v22_apparel_surgical_apron](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_surgical_apron.asset>)
- [recipe_v22_apparel_tail_guard](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_tail_guard.asset>)
- [recipe_v22_apparel_tail_ribbon](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_tail_ribbon.asset>)
- [recipe_v22_apparel_trousers](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_trousers.asset>)
- [recipe_v22_apparel_tunic](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_tunic.asset>)
- [recipe_v22_apparel_undershirt](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_undershirt.asset>)
- [recipe_v22_apparel_vest](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_vest.asset>)
- [recipe_v22_apparel_waterproof_work_suit](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_waterproof_work_suit.asset>)
- [recipe_v22_apparel_weapon_vigil_cloak](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_weapon_vigil_cloak.asset>)
- [recipe_v22_apparel_wing_cloak](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_wing_cloak.asset>)
- [recipe_v22_apparel_wing_harness](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_wing_harness.asset>)
- [recipe_v22_apparel_work_shirt](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_apparel_work_shirt.asset>)
- [recipe_v22_mending_scrap](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_mending_scrap.asset>)
- [recipe_v22_sewing_kit](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_sewing_kit.asset>)
- [recipe_v22_sewing_thread](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_sewing_thread.asset>)
- [recipe_v22_spin_cave_silk](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_cave_silk.asset>)
- [recipe_v22_spin_common_wool](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_common_wool.asset>)
- [recipe_v22_spin_deep_goat_wool](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_deep_goat_wool.asset>)
- [recipe_v22_spin_dreamweave](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_dreamweave.asset>)
- [recipe_v22_spin_ember_cotton](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_ember_cotton.asset>)
- [recipe_v22_spin_frost_linen](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_frost_linen.asset>)
- [recipe_v22_spin_frost_wool](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_frost_wool.asset>)
- [recipe_v22_spin_mire_canvas](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_mire_canvas.asset>)
- [recipe_v22_spin_powered_cave_silk](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_cave_silk.asset>)
- [recipe_v22_spin_powered_common_wool](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_common_wool.asset>)
- [recipe_v22_spin_powered_deep_goat_wool](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_deep_goat_wool.asset>)
- [recipe_v22_spin_powered_dreamweave](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_dreamweave.asset>)
- [recipe_v22_spin_powered_ember_cotton](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_ember_cotton.asset>)
- [recipe_v22_spin_powered_frost_linen](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_frost_linen.asset>)
- [recipe_v22_spin_powered_frost_wool](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_frost_wool.asset>)
- [recipe_v22_spin_powered_mire_canvas](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_mire_canvas.asset>)
- [recipe_v22_spin_powered_shade_cloth](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_shade_cloth.asset>)
- [recipe_v22_spin_powered_spore_hemp](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_powered_spore_hemp.asset>)
- [recipe_v22_spin_shade_cloth](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_shade_cloth.asset>)
- [recipe_v22_spin_spore_hemp](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_spin_spore_hemp.asset>)
- [recipe_v22_weave_cave_silk](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_cave_silk.asset>)
- [recipe_v22_weave_common_wool](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_common_wool.asset>)
- [recipe_v22_weave_deep_goat_wool](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_deep_goat_wool.asset>)
- [recipe_v22_weave_dreamweave](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_dreamweave.asset>)
- [recipe_v22_weave_ember_cotton](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_ember_cotton.asset>)
- [recipe_v22_weave_frost_linen](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_frost_linen.asset>)
- [recipe_v22_weave_frost_wool](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_frost_wool.asset>)
- [recipe_v22_weave_mire_canvas](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_mire_canvas.asset>)
- [recipe_v22_weave_shade_cloth](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_shade_cloth.asset>)
- [recipe_v22_weave_spore_hemp](<Assets/Resources/SO/Economy/Recipes/V22Apparel/recipe_v22_weave_spore_hemp.asset>)
- [recipe_brined_vegetable](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_brined_vegetable.asset>)
- [recipe_curd](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_curd.asset>)
- [recipe_dough](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_dough.asset>)
- [recipe_expedition_ration_pack](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_expedition_ration_pack.asset>)
- [recipe_fermented_liquor](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_fermented_liquor.asset>)
- [recipe_fermented_pickle](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_fermented_pickle.asset>)
- [recipe_fermented_vinegar](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_fermented_vinegar.asset>)
- [recipe_fresh_curd](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_fresh_curd.asset>)
- [recipe_grape_juice](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_grape_juice.asset>)
- [recipe_grape_syrup](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_grape_syrup.asset>)
- [recipe_malt](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_malt.asset>)
- [recipe_malt_porridge](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_malt_porridge.asset>)
- [recipe_night_spirit](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_night_spirit.asset>)
- [recipe_preserved_vegetable](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_preserved_vegetable.asset>)
- [recipe_ration_mixture](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_ration_mixture.asset>)
- [recipe_salted_meat](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_salted_meat.asset>)
- [recipe_salted_meat_stew](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_salted_meat_stew.asset>)
- [recipe_seasoned_filling](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_seasoned_filling.asset>)
- [recipe_silage](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_silage.asset>)
- [recipe_stuffed_mushroom](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_stuffed_mushroom.asset>)
- [recipe_twilight_beer](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_twilight_beer.asset>)
- [recipe_vegetable_pie](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_vegetable_pie.asset>)
- [recipe_washed_vegetable](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_washed_vegetable.asset>)
- [recipe_young_wine](<Assets/Resources/SO/Economy/Recipes/Workshop/recipe_young_wine.asset>)
- [recipe_advanced_medicine](<Assets/Resources/SO/Economy/Recipes/recipe_advanced_medicine.asset>)
- [recipe_alcohol](<Assets/Resources/SO/Economy/Recipes/recipe_alcohol.asset>)
- [recipe_anesthetic](<Assets/Resources/SO/Economy/Recipes/recipe_anesthetic.asset>)
- [recipe_antidote](<Assets/Resources/SO/Economy/Recipes/recipe_antidote.asset>)
- [recipe_antiseptic](<Assets/Resources/SO/Economy/Recipes/recipe_antiseptic.asset>)
- [recipe_arrow_bone](<Assets/Resources/SO/Economy/Recipes/recipe_arrow_bone.asset>)
- [recipe_arrow_iron](<Assets/Resources/SO/Economy/Recipes/recipe_arrow_iron.asset>)
- [recipe_arrow_rune](<Assets/Resources/SO/Economy/Recipes/recipe_arrow_rune.asset>)
- [recipe_arrow_steel](<Assets/Resources/SO/Economy/Recipes/recipe_arrow_steel.asset>)
- [recipe_bedding_animal](<Assets/Resources/SO/Economy/Recipes/recipe_bedding_animal.asset>)
- [recipe_bedding_straw](<Assets/Resources/SO/Economy/Recipes/recipe_bedding_straw.asset>)
- [recipe_blacksteel](<Assets/Resources/SO/Economy/Recipes/recipe_blacksteel.asset>)
- [recipe_blood_stimulant](<Assets/Resources/SO/Economy/Recipes/recipe_blood_stimulant.asset>)
- [recipe_boar_stew](<Assets/Resources/SO/Economy/Recipes/recipe_boar_stew.asset>)
- [recipe_bolt_bone](<Assets/Resources/SO/Economy/Recipes/recipe_bolt_bone.asset>)
- [recipe_bolt_iron](<Assets/Resources/SO/Economy/Recipes/recipe_bolt_iron.asset>)
- [recipe_bolt_rune](<Assets/Resources/SO/Economy/Recipes/recipe_bolt_rune.asset>)
- [recipe_bolt_steel](<Assets/Resources/SO/Economy/Recipes/recipe_bolt_steel.asset>)
- [recipe_bone_charm](<Assets/Resources/SO/Economy/Recipes/recipe_bone_charm.asset>)
- [recipe_bowstring_fiber](<Assets/Resources/SO/Economy/Recipes/recipe_bowstring_fiber.asset>)
- [recipe_bowstring_sinew](<Assets/Resources/SO/Economy/Recipes/recipe_bowstring_sinew.asset>)
- [recipe_candle](<Assets/Resources/SO/Economy/Recipes/recipe_candle.asset>)
- [recipe_charcoal](<Assets/Resources/SO/Economy/Recipes/recipe_charcoal.asset>)
- [recipe_cheese](<Assets/Resources/SO/Economy/Recipes/recipe_cheese.asset>)
- [recipe_cheese_mushroom](<Assets/Resources/SO/Economy/Recipes/recipe_cheese_mushroom.asset>)
- [recipe_cloth](<Assets/Resources/SO/Economy/Recipes/recipe_cloth.asset>)
- [recipe_cold_work_suit](<Assets/Resources/SO/Economy/Recipes/recipe_cold_work_suit.asset>)
- [recipe_compost_animal](<Assets/Resources/SO/Economy/Recipes/recipe_compost_animal.asset>)
- [recipe_compost_manure](<Assets/Resources/SO/Economy/Recipes/recipe_compost_manure.asset>)
- [recipe_compost_mixed](<Assets/Resources/SO/Economy/Recipes/recipe_compost_mixed.asset>)
- [recipe_compost_plant](<Assets/Resources/SO/Economy/Recipes/recipe_compost_plant.asset>)
- [recipe_dog_food](<Assets/Resources/SO/Economy/Recipes/recipe_dog_food.asset>)
- [recipe_dog_food_fresh](<Assets/Resources/SO/Economy/Recipes/recipe_dog_food_fresh.asset>)
- [recipe_dreamleaf_analgesic](<Assets/Resources/SO/Economy/Recipes/recipe_dreamleaf_analgesic.asset>)
- [recipe_dreamweave](<Assets/Resources/SO/Economy/Recipes/recipe_dreamweave.asset>)
- [recipe_egg_pancake](<Assets/Resources/SO/Economy/Recipes/recipe_egg_pancake.asset>)
- [recipe_fang_poison](<Assets/Resources/SO/Economy/Recipes/recipe_fang_poison.asset>)
- [recipe_garden_meal](<Assets/Resources/SO/Economy/Recipes/recipe_garden_meal.asset>)
- [recipe_gold_ingot](<Assets/Resources/SO/Economy/Recipes/recipe_gold_ingot.asset>)
- [recipe_gold_leaf](<Assets/Resources/SO/Economy/Recipes/recipe_gold_leaf.asset>)
- [recipe_gold_ornament](<Assets/Resources/SO/Economy/Recipes/recipe_gold_ornament.asset>)
- [recipe_grain_porridge](<Assets/Resources/SO/Economy/Recipes/recipe_grain_porridge.asset>)
- [recipe_hallucinogenic_distillate](<Assets/Resources/SO/Economy/Recipes/recipe_hallucinogenic_distillate.asset>)
- [recipe_hay_feed](<Assets/Resources/SO/Economy/Recipes/recipe_hay_feed.asset>)
- [recipe_herbal_poultice](<Assets/Resources/SO/Economy/Recipes/recipe_herbal_poultice.asset>)
- [recipe_incinerate_animal](<Assets/Resources/SO/Economy/Recipes/recipe_incinerate_animal.asset>)
- [recipe_incinerate_forbidden](<Assets/Resources/SO/Economy/Recipes/recipe_incinerate_forbidden.asset>)
- [recipe_incinerate_mixed](<Assets/Resources/SO/Economy/Recipes/recipe_incinerate_mixed.asset>)
- [recipe_incinerate_plant](<Assets/Resources/SO/Economy/Recipes/recipe_incinerate_plant.asset>)
- [recipe_iron_ingot](<Assets/Resources/SO/Economy/Recipes/recipe_iron_ingot.asset>)
- [recipe_iron_slag_block](<Assets/Resources/SO/Economy/Recipes/recipe_iron_slag_block.asset>)
- [recipe_jerky](<Assets/Resources/SO/Economy/Recipes/recipe_jerky.asset>)
- [recipe_lavish_meat](<Assets/Resources/SO/Economy/Recipes/recipe_lavish_meat.asset>)
- [recipe_lavish_vegan](<Assets/Resources/SO/Economy/Recipes/recipe_lavish_vegan.asset>)
- [recipe_leather](<Assets/Resources/SO/Economy/Recipes/recipe_leather.asset>)
- [recipe_loot_appraisal](<Assets/Resources/SO/Economy/Recipes/recipe_loot_appraisal.asset>)
- [recipe_low_fuel_animal](<Assets/Resources/SO/Economy/Recipes/recipe_low_fuel_animal.asset>)
- [recipe_low_fuel_manure](<Assets/Resources/SO/Economy/Recipes/recipe_low_fuel_manure.asset>)
- [recipe_low_fuel_plant](<Assets/Resources/SO/Economy/Recipes/recipe_low_fuel_plant.asset>)
- [recipe_low_fuel_rot](<Assets/Resources/SO/Economy/Recipes/recipe_low_fuel_rot.asset>)
- [recipe_mana_awakener](<Assets/Resources/SO/Economy/Recipes/recipe_mana_awakener.asset>)
- [recipe_meat_pie](<Assets/Resources/SO/Economy/Recipes/recipe_meat_pie.asset>)
- [recipe_medical_vial](<Assets/Resources/SO/Economy/Recipes/recipe_medical_vial.asset>)
- [recipe_milling_flour](<Assets/Resources/SO/Economy/Recipes/recipe_milling_flour.asset>)
- [recipe_moonflower_tea](<Assets/Resources/SO/Economy/Recipes/recipe_moonflower_tea.asset>)
- [recipe_mushroom_soup](<Assets/Resources/SO/Economy/Recipes/recipe_mushroom_soup.asset>)
- [recipe_night_wine](<Assets/Resources/SO/Economy/Recipes/recipe_night_wine.asset>)
- [recipe_preserved_ration](<Assets/Resources/SO/Economy/Recipes/recipe_preserved_ration.asset>)
- [recipe_resin_balm](<Assets/Resources/SO/Economy/Recipes/recipe_resin_balm.asset>)
- [recipe_ritual_reagent](<Assets/Resources/SO/Economy/Recipes/recipe_ritual_reagent.asset>)
- [recipe_roasted_meat](<Assets/Resources/SO/Economy/Recipes/recipe_roasted_meat.asset>)
- [recipe_root_stew](<Assets/Resources/SO/Economy/Recipes/recipe_root_stew.asset>)
- [recipe_rot_toxin](<Assets/Resources/SO/Economy/Recipes/recipe_rot_toxin.asset>)
- [recipe_rune_cold_suit](<Assets/Resources/SO/Economy/Recipes/recipe_rune_cold_suit.asset>)
- [recipe_rune_leather](<Assets/Resources/SO/Economy/Recipes/recipe_rune_leather.asset>)
- [recipe_sawmill_lumber](<Assets/Resources/SO/Economy/Recipes/recipe_sawmill_lumber.asset>)
- [recipe_slime_warming_pad](<Assets/Resources/SO/Economy/Recipes/recipe_slime_warming_pad.asset>)
- [recipe_soap](<Assets/Resources/SO/Economy/Recipes/recipe_soap.asset>)
- [recipe_solvent](<Assets/Resources/SO/Economy/Recipes/recipe_solvent.asset>)
- [recipe_standard_medicine](<Assets/Resources/SO/Economy/Recipes/recipe_standard_medicine.asset>)
- [recipe_starch](<Assets/Resources/SO/Economy/Recipes/recipe_starch.asset>)
- [recipe_steel_ingot](<Assets/Resources/SO/Economy/Recipes/recipe_steel_ingot.asset>)
- [recipe_stone_block](<Assets/Resources/SO/Economy/Recipes/recipe_stone_block.asset>)
- [recipe_stone_ornament](<Assets/Resources/SO/Economy/Recipes/recipe_stone_ornament.asset>)
- [recipe_surgery_artificial_eye](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_artificial_eye.asset>)
- [recipe_surgery_balance_tail](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_balance_tail.asset>)
- [recipe_surgery_brain_assist](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_brain_assist.asset>)
- [recipe_surgery_heart_augmentation](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_heart_augmentation.asset>)
- [recipe_surgery_heat_sac](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_heat_sac.asset>)
- [recipe_surgery_hypha_core](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_hypha_core.asset>)
- [recipe_surgery_night_eye](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_night_eye.asset>)
- [recipe_surgery_precision_hand](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_precision_hand.asset>)
- [recipe_surgery_prosthetic_arm](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_prosthetic_arm.asset>)
- [recipe_surgery_prosthetic_leg](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_prosthetic_leg.asset>)
- [recipe_surgery_pseudopods](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_pseudopods.asset>)
- [recipe_surgery_reinforced_torso](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_reinforced_torso.asset>)
- [recipe_surgery_sprint_joint](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_sprint_joint.asset>)
- [recipe_surgery_tail_balance_augmentation](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_tail_balance_augmentation.asset>)
- [recipe_surgery_wing](<Assets/Resources/SO/Economy/Recipes/recipe_surgery_wing.asset>)
- [recipe_syrup](<Assets/Resources/SO/Economy/Recipes/recipe_syrup.asset>)
- [recipe_tallow](<Assets/Resources/SO/Economy/Recipes/recipe_tallow.asset>)
- [recipe_trail_charm](<Assets/Resources/SO/Economy/Recipes/recipe_trail_charm.asset>)
- [recipe_treated_lumber](<Assets/Resources/SO/Economy/Recipes/recipe_treated_lumber.asset>)
- [recipe_vitality_tonic](<Assets/Resources/SO/Economy/Recipes/recipe_vitality_tonic.asset>)
- [recipe_wool_cloth](<Assets/Resources/SO/Economy/Recipes/recipe_wool_cloth.asset>)
- [source_animal_blood](<Assets/Resources/SO/Economy/Recipes/source_animal_blood.asset>)
- [source_animal_bone](<Assets/Resources/SO/Economy/Recipes/source_animal_bone.asset>)
- [source_animal_egg](<Assets/Resources/SO/Economy/Recipes/source_animal_egg.asset>)
- [source_animal_fang](<Assets/Resources/SO/Economy/Recipes/source_animal_fang.asset>)
- [source_animal_fat](<Assets/Resources/SO/Economy/Recipes/source_animal_fat.asset>)
- [source_animal_feather](<Assets/Resources/SO/Economy/Recipes/source_animal_feather.asset>)
- [source_animal_hide](<Assets/Resources/SO/Economy/Recipes/source_animal_hide.asset>)
- [source_animal_horn](<Assets/Resources/SO/Economy/Recipes/source_animal_horn.asset>)
- [source_animal_manure](<Assets/Resources/SO/Economy/Recipes/source_animal_manure.asset>)
- [source_animal_meat](<Assets/Resources/SO/Economy/Recipes/source_animal_meat.asset>)
- [source_animal_milk](<Assets/Resources/SO/Economy/Recipes/source_animal_milk.asset>)
- [source_animal_wool](<Assets/Resources/SO/Economy/Recipes/source_animal_wool.asset>)
- [source_grass](<Assets/Resources/SO/Economy/Recipes/source_grass.asset>)
- [source_logging](<Assets/Resources/SO/Economy/Recipes/source_logging.asset>)
- [source_quarry](<Assets/Resources/SO/Economy/Recipes/source_quarry.asset>)
- [source_saltstone](<Assets/Resources/SO/Economy/Recipes/source_saltstone.asset>)
- [source_spoilage_animal](<Assets/Resources/SO/Economy/Recipes/source_spoilage_animal.asset>)
- [source_spoilage_forbidden](<Assets/Resources/SO/Economy/Recipes/source_spoilage_forbidden.asset>)
- [source_spoilage_mixed](<Assets/Resources/SO/Economy/Recipes/source_spoilage_mixed.asset>)
- [source_spoilage_plant](<Assets/Resources/SO/Economy/Recipes/source_spoilage_plant.asset>)

</details>

<details>
<summary>ProficiencyDefinitionSO — 9개</summary>

- [proficiency_construction-engineering](<Assets/Resources/SO/V25/Proficiencies/proficiency_construction-engineering.asset>)
- [proficiency_crafting](<Assets/Resources/SO/V25/Proficiencies/proficiency_crafting.asset>)
- [proficiency_fieldwork](<Assets/Resources/SO/V25/Proficiencies/proficiency_fieldwork.asset>)
- [proficiency_food-production](<Assets/Resources/SO/V25/Proficiencies/proficiency_food-production.asset>)
- [proficiency_medicine](<Assets/Resources/SO/V25/Proficiencies/proficiency_medicine.asset>)
- [proficiency_melee-combat](<Assets/Resources/SO/V25/Proficiencies/proficiency_melee-combat.asset>)
- [proficiency_ranged-combat](<Assets/Resources/SO/V25/Proficiencies/proficiency_ranged-combat.asset>)
- [proficiency_scholarship](<Assets/Resources/SO/V25/Proficiencies/proficiency_scholarship.asset>)
- [proficiency_social](<Assets/Resources/SO/V25/Proficiencies/proficiency_social.asset>)

</details>

<details>
<summary>ReproductionProfileSO — 11개</summary>

- [Reproduction_Adventurer](<Assets/Resources/SO/Population/Reproduction/Reproduction_Adventurer.asset>)
- [Reproduction_Beastkin](<Assets/Resources/SO/Population/Reproduction/Reproduction_Beastkin.asset>)
- [Reproduction_Demon](<Assets/Resources/SO/Population/Reproduction/Reproduction_Demon.asset>)
- [Reproduction_Golem](<Assets/Resources/SO/Population/Reproduction/Reproduction_Golem.asset>)
- [Reproduction_Harpy](<Assets/Resources/SO/Population/Reproduction/Reproduction_Harpy.asset>)
- [Reproduction_Human](<Assets/Resources/SO/Population/Reproduction/Reproduction_Human.asset>)
- [Reproduction_Kobold](<Assets/Resources/SO/Population/Reproduction/Reproduction_Kobold.asset>)
- [Reproduction_Myconid](<Assets/Resources/SO/Population/Reproduction/Reproduction_Myconid.asset>)
- [Reproduction_Orc](<Assets/Resources/SO/Population/Reproduction/Reproduction_Orc.asset>)
- [Reproduction_Slime](<Assets/Resources/SO/Population/Reproduction/Reproduction_Slime.asset>)
- [Reproduction_Vampire](<Assets/Resources/SO/Population/Reproduction/Reproduction_Vampire.asset>)

</details>

<details>
<summary>ResearchProjectSO — 180개</summary>

- [agriculture_compost](<Assets/Resources/SO/Research/Projects/agriculture_compost.asset>)
- [agriculture_cultivar_breeding](<Assets/Resources/SO/Research/Projects/agriculture_cultivar_breeding.asset>)
- [agriculture_field](<Assets/Resources/SO/Research/Projects/agriculture_field.asset>)
- [agriculture_gathering](<Assets/Resources/SO/Research/Projects/agriculture_gathering.asset>)
- [agriculture_greenhouse_horticulture](<Assets/Resources/SO/Research/Projects/agriculture_greenhouse_horticulture.asset>)
- [agriculture_indoor](<Assets/Resources/SO/Research/Projects/agriculture_indoor.asset>)
- [agriculture_irrigation](<Assets/Resources/SO/Research/Projects/agriculture_irrigation.asset>)
- [agriculture_phenology](<Assets/Resources/SO/Research/Projects/agriculture_phenology.asset>)
- [agriculture_soil_cycles](<Assets/Resources/SO/Research/Projects/agriculture_soil_cycles.asset>)
- [agriculture_subterranean](<Assets/Resources/SO/Research/Projects/agriculture_subterranean.asset>)
- [arcane_advanced](<Assets/Resources/SO/Research/Projects/arcane_advanced.asset>)
- [arcane_alchemy](<Assets/Resources/SO/Research/Projects/arcane_alchemy.asset>)
- [arcane_records](<Assets/Resources/SO/Research/Projects/arcane_records.asset>)
- [arcane_resonance](<Assets/Resources/SO/Research/Projects/arcane_resonance.asset>)
- [authority_office](<Assets/Resources/SO/Research/Projects/authority_office.asset>)
- [authority_prestige](<Assets/Resources/SO/Research/Projects/authority_prestige.asset>)
- [authority_quarters](<Assets/Resources/SO/Research/Projects/authority_quarters.asset>)
- [authority_ritual](<Assets/Resources/SO/Research/Projects/authority_ritual.asset>)
- [bath_business](<Assets/Resources/SO/Research/Projects/bath_business.asset>)
- [climate_environment_control](<Assets/Resources/SO/Research/Projects/climate_environment_control.asset>)
- [climate_regional_climatology](<Assets/Resources/SO/Research/Projects/climate_regional_climatology.asset>)
- [climate_weather_observation](<Assets/Resources/SO/Research/Projects/climate_weather_observation.asset>)
- [commerce_expansion](<Assets/Resources/SO/Research/Projects/commerce_expansion.asset>)
- [commerce_logistics](<Assets/Resources/SO/Research/Projects/commerce_logistics.asset>)
- [commerce_retail](<Assets/Resources/SO/Research/Projects/commerce_retail.asset>)
- [commerce_secure_trade](<Assets/Resources/SO/Research/Projects/commerce_secure_trade.asset>)
- [control_blood_show](<Assets/Resources/SO/Research/Projects/control_blood_show.asset>)
- [control_labor](<Assets/Resources/SO/Research/Projects/control_labor.asset>)
- [control_restraints](<Assets/Resources/SO/Research/Projects/control_restraints.asset>)
- [control_show](<Assets/Resources/SO/Research/Projects/control_show.asset>)
- [cuisine_baking](<Assets/Resources/SO/Research/Projects/cuisine_baking.asset>)
- [cuisine_controlled_fermentation](<Assets/Resources/SO/Research/Projects/cuisine_controlled_fermentation.asset>)
- [cuisine_crops](<Assets/Resources/SO/Research/Projects/cuisine_crops.asset>)
- [cuisine_distilling_aging](<Assets/Resources/SO/Research/Projects/cuisine_distilling_aging.asset>)
- [cuisine_fermentation](<Assets/Resources/SO/Research/Projects/cuisine_fermentation.asset>)
- [cuisine_kitchen_hygiene](<Assets/Resources/SO/Research/Projects/cuisine_kitchen_hygiene.asset>)
- [cuisine_lavish](<Assets/Resources/SO/Research/Projects/cuisine_lavish.asset>)
- [cuisine_livestock](<Assets/Resources/SO/Research/Projects/cuisine_livestock.asset>)
- [cuisine_milling](<Assets/Resources/SO/Research/Projects/cuisine_milling.asset>)
- [cuisine_vegan](<Assets/Resources/SO/Research/Projects/cuisine_vegan.asset>)
- [defense_alliance_signals](<Assets/Resources/SO/Research/Projects/defense_alliance_signals.asset>)
- [defense_corridor_mechanisms](<Assets/Resources/SO/Research/Projects/defense_corridor_mechanisms.asset>)
- [defense_fortification](<Assets/Resources/SO/Research/Projects/defense_fortification.asset>)
- [defense_ranged_positions](<Assets/Resources/SO/Research/Projects/defense_ranged_positions.asset>)
- [defense_remote_control](<Assets/Resources/SO/Research/Projects/defense_remote_control.asset>)
- [defense_rune_identification](<Assets/Resources/SO/Research/Projects/defense_rune_identification.asset>)
- [defense_siege_fortification](<Assets/Resources/SO/Research/Projects/defense_siege_fortification.asset>)
- [defense_supply](<Assets/Resources/SO/Research/Projects/defense_supply.asset>)
- [defense_tactical_command](<Assets/Resources/SO/Research/Projects/defense_tactical_command.asset>)
- [defense_watch](<Assets/Resources/SO/Research/Projects/defense_watch.asset>)
- [environment_cold_work](<Assets/Resources/SO/Research/Projects/environment_cold_work.asset>)
- [environment_rune_insulation](<Assets/Resources/SO/Research/Projects/environment_rune_insulation.asset>)
- [equipment_armor_tailoring](<Assets/Resources/SO/Research/Projects/equipment_armor_tailoring.asset>)
- [equipment_articulated_plate](<Assets/Resources/SO/Research/Projects/equipment_articulated_plate.asset>)
- [equipment_ballistics](<Assets/Resources/SO/Research/Projects/equipment_ballistics.asset>)
- [equipment_black_powder](<Assets/Resources/SO/Research/Projects/equipment_black_powder.asset>)
- [equipment_bowyery](<Assets/Resources/SO/Research/Projects/equipment_bowyery.asset>)
- [equipment_field_maintenance](<Assets/Resources/SO/Research/Projects/equipment_field_maintenance.asset>)
- [equipment_ignition_mechanisms](<Assets/Resources/SO/Research/Projects/equipment_ignition_mechanisms.asset>)
- [equipment_industrial_metrology](<Assets/Resources/SO/Research/Projects/equipment_industrial_metrology.asset>)
- [equipment_lineage_binding](<Assets/Resources/SO/Research/Projects/equipment_lineage_binding.asset>)
- [equipment_mail_weaving](<Assets/Resources/SO/Research/Projects/equipment_mail_weaving.asset>)
- [equipment_material_testing](<Assets/Resources/SO/Research/Projects/equipment_material_testing.asset>)
- [equipment_mechanical_projectiles](<Assets/Resources/SO/Research/Projects/equipment_mechanical_projectiles.asset>)
- [equipment_modular_frames](<Assets/Resources/SO/Research/Projects/equipment_modular_frames.asset>)
- [equipment_powered_armor](<Assets/Resources/SO/Research/Projects/equipment_powered_armor.asset>)
- [equipment_precision_fitting](<Assets/Resources/SO/Research/Projects/equipment_precision_fitting.asset>)
- [equipment_pressure_barrels](<Assets/Resources/SO/Research/Projects/equipment_pressure_barrels.asset>)
- [equipment_prototype_engineering](<Assets/Resources/SO/Research/Projects/equipment_prototype_engineering.asset>)
- [equipment_relic_appraisal](<Assets/Resources/SO/Research/Projects/equipment_relic_appraisal.asset>)
- [equipment_rune_module_tuning](<Assets/Resources/SO/Research/Projects/equipment_rune_module_tuning.asset>)
- [equipment_standard_ammunition](<Assets/Resources/SO/Research/Projects/equipment_standard_ammunition.asset>)
- [equipment_weapon_patterns](<Assets/Resources/SO/Research/Projects/equipment_weapon_patterns.asset>)
- [forestry_charcoal](<Assets/Resources/SO/Research/Projects/forestry_charcoal.asset>)
- [forestry_fungal](<Assets/Resources/SO/Research/Projects/forestry_fungal.asset>)
- [forestry_logging](<Assets/Resources/SO/Research/Projects/forestry_logging.asset>)
- [forestry_sawmill](<Assets/Resources/SO/Research/Projects/forestry_sawmill.asset>)
- [forestry_tools](<Assets/Resources/SO/Research/Projects/forestry_tools.asset>)
- [forestry_treated](<Assets/Resources/SO/Research/Projects/forestry_treated.asset>)
- [genetics_controlled_heredity](<Assets/Resources/SO/Research/Projects/genetics_controlled_heredity.asset>)
- [genetics_cross_lineage_stabilization](<Assets/Resources/SO/Research/Projects/genetics_cross_lineage_stabilization.asset>)
- [genetics_hereditary_records](<Assets/Resources/SO/Research/Projects/genetics_hereditary_records.asset>)
- [health_isolation_medicine](<Assets/Resources/SO/Research/Projects/health_isolation_medicine.asset>)
- [health_pathogen_observation](<Assets/Resources/SO/Research/Projects/health_pathogen_observation.asset>)
- [health_vaccination](<Assets/Resources/SO/Research/Projects/health_vaccination.asset>)
- [hospitality_operations](<Assets/Resources/SO/Research/Projects/hospitality_operations.asset>)
- [housing_room_assignment](<Assets/Resources/SO/Research/Projects/housing_room_assignment.asset>)
- [husbandry_breeding](<Assets/Resources/SO/Research/Projects/husbandry_breeding.asset>)
- [husbandry_capture](<Assets/Resources/SO/Research/Projects/husbandry_capture.asset>)
- [husbandry_feed](<Assets/Resources/SO/Research/Projects/husbandry_feed.asset>)
- [husbandry_seasonal_breeding](<Assets/Resources/SO/Research/Projects/husbandry_seasonal_breeding.asset>)
- [husbandry_selective](<Assets/Resources/SO/Research/Projects/husbandry_selective.asset>)
- [husbandry_stable](<Assets/Resources/SO/Research/Projects/husbandry_stable.asset>)
- [husbandry_taming](<Assets/Resources/SO/Research/Projects/husbandry_taming.asset>)
- [industry_assisted_processing](<Assets/Resources/SO/Research/Projects/industry_assisted_processing.asset>)
- [industry_automatic_bills](<Assets/Resources/SO/Research/Projects/industry_automatic_bills.asset>)
- [industry_automatic_sanitation](<Assets/Resources/SO/Research/Projects/industry_automatic_sanitation.asset>)
- [industry_breakers](<Assets/Resources/SO/Research/Projects/industry_breakers.asset>)
- [industry_conveyor](<Assets/Resources/SO/Research/Projects/industry_conveyor.asset>)
- [industry_dark_foundry](<Assets/Resources/SO/Research/Projects/industry_dark_foundry.asset>)
- [industry_electric_lighting](<Assets/Resources/SO/Research/Projects/industry_electric_lighting.asset>)
- [industry_electric_smelting](<Assets/Resources/SO/Research/Projects/industry_electric_smelting.asset>)
- [industry_junctions](<Assets/Resources/SO/Research/Projects/industry_junctions.asset>)
- [industry_lifts](<Assets/Resources/SO/Research/Projects/industry_lifts.asset>)
- [industry_line_balancing](<Assets/Resources/SO/Research/Projects/industry_line_balancing.asset>)
- [industry_mana_power](<Assets/Resources/SO/Research/Projects/industry_mana_power.asset>)
- [industry_powered_tools](<Assets/Resources/SO/Research/Projects/industry_powered_tools.asset>)
- [industry_precision](<Assets/Resources/SO/Research/Projects/industry_precision.asset>)
- [industry_rune_automation](<Assets/Resources/SO/Research/Projects/industry_rune_automation.asset>)
- [industry_steam_power](<Assets/Resources/SO/Research/Projects/industry_steam_power.asset>)
- [industry_storage](<Assets/Resources/SO/Research/Projects/industry_storage.asset>)
- [industry_waterwheel](<Assets/Resources/SO/Research/Projects/industry_waterwheel.asset>)
- [life_seasonal_calendar](<Assets/Resources/SO/Research/Projects/life_seasonal_calendar.asset>)
- [medical_aberrant_augmentation](<Assets/Resources/SO/Research/Projects/medical_aberrant_augmentation.asset>)
- [medical_anatomy](<Assets/Resources/SO/Research/Projects/medical_anatomy.asset>)
- [medical_avian_prosthetics](<Assets/Resources/SO/Research/Projects/medical_avian_prosthetics.asset>)
- [medical_blood_rejuvenation](<Assets/Resources/SO/Research/Projects/medical_blood_rejuvenation.asset>)
- [medical_bloodcraft_augmentation](<Assets/Resources/SO/Research/Projects/medical_bloodcraft_augmentation.asset>)
- [medical_construct_core_engineering](<Assets/Resources/SO/Research/Projects/medical_construct_core_engineering.asset>)
- [medical_construct_core_maintenance](<Assets/Resources/SO/Research/Projects/medical_construct_core_maintenance.asset>)
- [medical_geriatric_medicine](<Assets/Resources/SO/Research/Projects/medical_geriatric_medicine.asset>)
- [medical_gerontology](<Assets/Resources/SO/Research/Projects/medical_gerontology.asset>)
- [medical_mana_core_engineering](<Assets/Resources/SO/Research/Projects/medical_mana_core_engineering.asset>)
- [medical_mycelial_grafting](<Assets/Resources/SO/Research/Projects/medical_mycelial_grafting.asset>)
- [medical_organ_preservation](<Assets/Resources/SO/Research/Projects/medical_organ_preservation.asset>)
- [medical_organ_regeneration](<Assets/Resources/SO/Research/Projects/medical_organ_regeneration.asset>)
- [medical_prosthetics](<Assets/Resources/SO/Research/Projects/medical_prosthetics.asset>)
- [medical_reception](<Assets/Resources/SO/Research/Projects/medical_reception.asset>)
- [medical_regenerative_culture](<Assets/Resources/SO/Research/Projects/medical_regenerative_culture.asset>)
- [medical_reproductive_medicine](<Assets/Resources/SO/Research/Projects/medical_reproductive_medicine.asset>)
- [medical_rune_hibernation](<Assets/Resources/SO/Research/Projects/medical_rune_hibernation.asset>)
- [medical_slime_bioengineering](<Assets/Resources/SO/Research/Projects/medical_slime_bioengineering.asset>)
- [medical_surgery](<Assets/Resources/SO/Research/Projects/medical_surgery.asset>)
- [medical_temporal_stasis](<Assets/Resources/SO/Research/Projects/medical_temporal_stasis.asset>)
- [medical_trauma_medicine](<Assets/Resources/SO/Research/Projects/medical_trauma_medicine.asset>)
- [medical_whole_body_regeneration](<Assets/Resources/SO/Research/Projects/medical_whole_body_regeneration.asset>)
- [medical_xenotransplant](<Assets/Resources/SO/Research/Projects/medical_xenotransplant.asset>)
- [metallurgy_advanced](<Assets/Resources/SO/Research/Projects/metallurgy_advanced.asset>)
- [metallurgy_blacksteel](<Assets/Resources/SO/Research/Projects/metallurgy_blacksteel.asset>)
- [metallurgy_iron](<Assets/Resources/SO/Research/Projects/metallurgy_iron.asset>)
- [metallurgy_precious](<Assets/Resources/SO/Research/Projects/metallurgy_precious.asset>)
- [metallurgy_primitive](<Assets/Resources/SO/Research/Projects/metallurgy_primitive.asset>)
- [metallurgy_steel](<Assets/Resources/SO/Research/Projects/metallurgy_steel.asset>)
- [mining_deep](<Assets/Resources/SO/Research/Projects/mining_deep.asset>)
- [mining_mana](<Assets/Resources/SO/Research/Projects/mining_mana.asset>)
- [mining_quarry](<Assets/Resources/SO/Research/Projects/mining_quarry.asset>)
- [mining_sorting](<Assets/Resources/SO/Research/Projects/mining_sorting.asset>)
- [mining_stonecutting](<Assets/Resources/SO/Research/Projects/mining_stonecutting.asset>)
- [mining_surface](<Assets/Resources/SO/Research/Projects/mining_surface.asset>)
- [pharmacology_advanced](<Assets/Resources/SO/Research/Projects/pharmacology_advanced.asset>)
- [pharmacology_anesthesia](<Assets/Resources/SO/Research/Projects/pharmacology_anesthesia.asset>)
- [pharmacology_antiseptic](<Assets/Resources/SO/Research/Projects/pharmacology_antiseptic.asset>)
- [pharmacology_distillation](<Assets/Resources/SO/Research/Projects/pharmacology_distillation.asset>)
- [pharmacology_herbalism](<Assets/Resources/SO/Research/Projects/pharmacology_herbalism.asset>)
- [pharmacology_stimulants](<Assets/Resources/SO/Research/Projects/pharmacology_stimulants.asset>)
- [plumbing_basics](<Assets/Resources/SO/Research/Projects/plumbing_basics.asset>)
- [plumbing_reuse](<Assets/Resources/SO/Research/Projects/plumbing_reuse.asset>)
- [plumbing_rune_purification](<Assets/Resources/SO/Research/Projects/plumbing_rune_purification.asset>)
- [plumbing_sewer](<Assets/Resources/SO/Research/Projects/plumbing_sewer.asset>)
- [service_automation](<Assets/Resources/SO/Research/Projects/service_automation.asset>)
- [service_dining_operations](<Assets/Resources/SO/Research/Projects/service_dining_operations.asset>)
- [service_flow](<Assets/Resources/SO/Research/Projects/service_flow.asset>)
- [society_career_records](<Assets/Resources/SO/Research/Projects/society_career_records.asset>)
- [society_child_education](<Assets/Resources/SO/Research/Projects/society_child_education.asset>)
- [society_corpse_care](<Assets/Resources/SO/Research/Projects/society_corpse_care.asset>)
- [society_generation_management](<Assets/Resources/SO/Research/Projects/society_generation_management.asset>)
- [society_household_records](<Assets/Resources/SO/Research/Projects/society_household_records.asset>)
- [society_retirement](<Assets/Resources/SO/Research/Projects/society_retirement.asset>)
- [survival_field_rations](<Assets/Resources/SO/Research/Projects/survival_field_rations.asset>)
- [survival_medical](<Assets/Resources/SO/Research/Projects/survival_medical.asset>)
- [survival_preservation](<Assets/Resources/SO/Research/Projects/survival_preservation.asset>)
- [survival_sanitation](<Assets/Resources/SO/Research/Projects/survival_sanitation.asset>)
- [survival_seasonal_storage](<Assets/Resources/SO/Research/Projects/survival_seasonal_storage.asset>)
- [survival_support](<Assets/Resources/SO/Research/Projects/survival_support.asset>)
- [textile_dreamweave](<Assets/Resources/SO/Research/Projects/textile_dreamweave.asset>)
- [textile_fiber](<Assets/Resources/SO/Research/Projects/textile_fiber.asset>)
- [textile_layered](<Assets/Resources/SO/Research/Projects/textile_layered.asset>)
- [textile_rune_leather](<Assets/Resources/SO/Research/Projects/textile_rune_leather.asset>)
- [textile_tailoring](<Assets/Resources/SO/Research/Projects/textile_tailoring.asset>)
- [textile_tanning](<Assets/Resources/SO/Research/Projects/textile_tanning.asset>)

</details>

<details>
<summary>ResearchUnlockBundleDefinitionSO — 180개</summary>

- [agriculture_compost](<Assets/Resources/SO/Research/UnlockBundles/agriculture_compost.asset>)
- [agriculture_cultivar_breeding](<Assets/Resources/SO/Research/UnlockBundles/agriculture_cultivar_breeding.asset>)
- [agriculture_field](<Assets/Resources/SO/Research/UnlockBundles/agriculture_field.asset>)
- [agriculture_gathering](<Assets/Resources/SO/Research/UnlockBundles/agriculture_gathering.asset>)
- [agriculture_greenhouse_horticulture](<Assets/Resources/SO/Research/UnlockBundles/agriculture_greenhouse_horticulture.asset>)
- [agriculture_indoor](<Assets/Resources/SO/Research/UnlockBundles/agriculture_indoor.asset>)
- [agriculture_irrigation](<Assets/Resources/SO/Research/UnlockBundles/agriculture_irrigation.asset>)
- [agriculture_phenology](<Assets/Resources/SO/Research/UnlockBundles/agriculture_phenology.asset>)
- [agriculture_soil_cycles](<Assets/Resources/SO/Research/UnlockBundles/agriculture_soil_cycles.asset>)
- [agriculture_subterranean](<Assets/Resources/SO/Research/UnlockBundles/agriculture_subterranean.asset>)
- [arcane_advanced](<Assets/Resources/SO/Research/UnlockBundles/arcane_advanced.asset>)
- [arcane_alchemy](<Assets/Resources/SO/Research/UnlockBundles/arcane_alchemy.asset>)
- [arcane_records](<Assets/Resources/SO/Research/UnlockBundles/arcane_records.asset>)
- [arcane_resonance](<Assets/Resources/SO/Research/UnlockBundles/arcane_resonance.asset>)
- [authority_office](<Assets/Resources/SO/Research/UnlockBundles/authority_office.asset>)
- [authority_prestige](<Assets/Resources/SO/Research/UnlockBundles/authority_prestige.asset>)
- [authority_quarters](<Assets/Resources/SO/Research/UnlockBundles/authority_quarters.asset>)
- [authority_ritual](<Assets/Resources/SO/Research/UnlockBundles/authority_ritual.asset>)
- [bath_business](<Assets/Resources/SO/Research/UnlockBundles/bath_business.asset>)
- [climate_environment_control](<Assets/Resources/SO/Research/UnlockBundles/climate_environment_control.asset>)
- [climate_regional_climatology](<Assets/Resources/SO/Research/UnlockBundles/climate_regional_climatology.asset>)
- [climate_weather_observation](<Assets/Resources/SO/Research/UnlockBundles/climate_weather_observation.asset>)
- [commerce_expansion](<Assets/Resources/SO/Research/UnlockBundles/commerce_expansion.asset>)
- [commerce_logistics](<Assets/Resources/SO/Research/UnlockBundles/commerce_logistics.asset>)
- [commerce_retail](<Assets/Resources/SO/Research/UnlockBundles/commerce_retail.asset>)
- [commerce_secure_trade](<Assets/Resources/SO/Research/UnlockBundles/commerce_secure_trade.asset>)
- [control_blood_show](<Assets/Resources/SO/Research/UnlockBundles/control_blood_show.asset>)
- [control_labor](<Assets/Resources/SO/Research/UnlockBundles/control_labor.asset>)
- [control_restraints](<Assets/Resources/SO/Research/UnlockBundles/control_restraints.asset>)
- [control_show](<Assets/Resources/SO/Research/UnlockBundles/control_show.asset>)
- [cuisine_baking](<Assets/Resources/SO/Research/UnlockBundles/cuisine_baking.asset>)
- [cuisine_controlled_fermentation](<Assets/Resources/SO/Research/UnlockBundles/cuisine_controlled_fermentation.asset>)
- [cuisine_crops](<Assets/Resources/SO/Research/UnlockBundles/cuisine_crops.asset>)
- [cuisine_distilling_aging](<Assets/Resources/SO/Research/UnlockBundles/cuisine_distilling_aging.asset>)
- [cuisine_fermentation](<Assets/Resources/SO/Research/UnlockBundles/cuisine_fermentation.asset>)
- [cuisine_kitchen_hygiene](<Assets/Resources/SO/Research/UnlockBundles/cuisine_kitchen_hygiene.asset>)
- [cuisine_lavish](<Assets/Resources/SO/Research/UnlockBundles/cuisine_lavish.asset>)
- [cuisine_livestock](<Assets/Resources/SO/Research/UnlockBundles/cuisine_livestock.asset>)
- [cuisine_milling](<Assets/Resources/SO/Research/UnlockBundles/cuisine_milling.asset>)
- [cuisine_vegan](<Assets/Resources/SO/Research/UnlockBundles/cuisine_vegan.asset>)
- [defense_alliance_signals](<Assets/Resources/SO/Research/UnlockBundles/defense_alliance_signals.asset>)
- [defense_corridor_mechanisms](<Assets/Resources/SO/Research/UnlockBundles/defense_corridor_mechanisms.asset>)
- [defense_fortification](<Assets/Resources/SO/Research/UnlockBundles/defense_fortification.asset>)
- [defense_ranged_positions](<Assets/Resources/SO/Research/UnlockBundles/defense_ranged_positions.asset>)
- [defense_remote_control](<Assets/Resources/SO/Research/UnlockBundles/defense_remote_control.asset>)
- [defense_rune_identification](<Assets/Resources/SO/Research/UnlockBundles/defense_rune_identification.asset>)
- [defense_siege_fortification](<Assets/Resources/SO/Research/UnlockBundles/defense_siege_fortification.asset>)
- [defense_supply](<Assets/Resources/SO/Research/UnlockBundles/defense_supply.asset>)
- [defense_tactical_command](<Assets/Resources/SO/Research/UnlockBundles/defense_tactical_command.asset>)
- [defense_watch](<Assets/Resources/SO/Research/UnlockBundles/defense_watch.asset>)
- [environment_cold_work](<Assets/Resources/SO/Research/UnlockBundles/environment_cold_work.asset>)
- [environment_rune_insulation](<Assets/Resources/SO/Research/UnlockBundles/environment_rune_insulation.asset>)
- [equipment_armor_tailoring](<Assets/Resources/SO/Research/UnlockBundles/equipment_armor_tailoring.asset>)
- [equipment_articulated_plate](<Assets/Resources/SO/Research/UnlockBundles/equipment_articulated_plate.asset>)
- [equipment_ballistics](<Assets/Resources/SO/Research/UnlockBundles/equipment_ballistics.asset>)
- [equipment_black_powder](<Assets/Resources/SO/Research/UnlockBundles/equipment_black_powder.asset>)
- [equipment_bowyery](<Assets/Resources/SO/Research/UnlockBundles/equipment_bowyery.asset>)
- [equipment_field_maintenance](<Assets/Resources/SO/Research/UnlockBundles/equipment_field_maintenance.asset>)
- [equipment_ignition_mechanisms](<Assets/Resources/SO/Research/UnlockBundles/equipment_ignition_mechanisms.asset>)
- [equipment_industrial_metrology](<Assets/Resources/SO/Research/UnlockBundles/equipment_industrial_metrology.asset>)
- [equipment_lineage_binding](<Assets/Resources/SO/Research/UnlockBundles/equipment_lineage_binding.asset>)
- [equipment_mail_weaving](<Assets/Resources/SO/Research/UnlockBundles/equipment_mail_weaving.asset>)
- [equipment_material_testing](<Assets/Resources/SO/Research/UnlockBundles/equipment_material_testing.asset>)
- [equipment_mechanical_projectiles](<Assets/Resources/SO/Research/UnlockBundles/equipment_mechanical_projectiles.asset>)
- [equipment_modular_frames](<Assets/Resources/SO/Research/UnlockBundles/equipment_modular_frames.asset>)
- [equipment_powered_armor](<Assets/Resources/SO/Research/UnlockBundles/equipment_powered_armor.asset>)
- [equipment_precision_fitting](<Assets/Resources/SO/Research/UnlockBundles/equipment_precision_fitting.asset>)
- [equipment_pressure_barrels](<Assets/Resources/SO/Research/UnlockBundles/equipment_pressure_barrels.asset>)
- [equipment_prototype_engineering](<Assets/Resources/SO/Research/UnlockBundles/equipment_prototype_engineering.asset>)
- [equipment_relic_appraisal](<Assets/Resources/SO/Research/UnlockBundles/equipment_relic_appraisal.asset>)
- [equipment_rune_module_tuning](<Assets/Resources/SO/Research/UnlockBundles/equipment_rune_module_tuning.asset>)
- [equipment_standard_ammunition](<Assets/Resources/SO/Research/UnlockBundles/equipment_standard_ammunition.asset>)
- [equipment_weapon_patterns](<Assets/Resources/SO/Research/UnlockBundles/equipment_weapon_patterns.asset>)
- [forestry_charcoal](<Assets/Resources/SO/Research/UnlockBundles/forestry_charcoal.asset>)
- [forestry_fungal](<Assets/Resources/SO/Research/UnlockBundles/forestry_fungal.asset>)
- [forestry_logging](<Assets/Resources/SO/Research/UnlockBundles/forestry_logging.asset>)
- [forestry_sawmill](<Assets/Resources/SO/Research/UnlockBundles/forestry_sawmill.asset>)
- [forestry_tools](<Assets/Resources/SO/Research/UnlockBundles/forestry_tools.asset>)
- [forestry_treated](<Assets/Resources/SO/Research/UnlockBundles/forestry_treated.asset>)
- [genetics_controlled_heredity](<Assets/Resources/SO/Research/UnlockBundles/genetics_controlled_heredity.asset>)
- [genetics_cross_lineage_stabilization](<Assets/Resources/SO/Research/UnlockBundles/genetics_cross_lineage_stabilization.asset>)
- [genetics_hereditary_records](<Assets/Resources/SO/Research/UnlockBundles/genetics_hereditary_records.asset>)
- [health_isolation_medicine](<Assets/Resources/SO/Research/UnlockBundles/health_isolation_medicine.asset>)
- [health_pathogen_observation](<Assets/Resources/SO/Research/UnlockBundles/health_pathogen_observation.asset>)
- [health_vaccination](<Assets/Resources/SO/Research/UnlockBundles/health_vaccination.asset>)
- [hospitality_operations](<Assets/Resources/SO/Research/UnlockBundles/hospitality_operations.asset>)
- [housing_room_assignment](<Assets/Resources/SO/Research/UnlockBundles/housing_room_assignment.asset>)
- [husbandry_breeding](<Assets/Resources/SO/Research/UnlockBundles/husbandry_breeding.asset>)
- [husbandry_capture](<Assets/Resources/SO/Research/UnlockBundles/husbandry_capture.asset>)
- [husbandry_feed](<Assets/Resources/SO/Research/UnlockBundles/husbandry_feed.asset>)
- [husbandry_seasonal_breeding](<Assets/Resources/SO/Research/UnlockBundles/husbandry_seasonal_breeding.asset>)
- [husbandry_selective](<Assets/Resources/SO/Research/UnlockBundles/husbandry_selective.asset>)
- [husbandry_stable](<Assets/Resources/SO/Research/UnlockBundles/husbandry_stable.asset>)
- [husbandry_taming](<Assets/Resources/SO/Research/UnlockBundles/husbandry_taming.asset>)
- [industry_assisted_processing](<Assets/Resources/SO/Research/UnlockBundles/industry_assisted_processing.asset>)
- [industry_automatic_bills](<Assets/Resources/SO/Research/UnlockBundles/industry_automatic_bills.asset>)
- [industry_automatic_sanitation](<Assets/Resources/SO/Research/UnlockBundles/industry_automatic_sanitation.asset>)
- [industry_breakers](<Assets/Resources/SO/Research/UnlockBundles/industry_breakers.asset>)
- [industry_conveyor](<Assets/Resources/SO/Research/UnlockBundles/industry_conveyor.asset>)
- [industry_dark_foundry](<Assets/Resources/SO/Research/UnlockBundles/industry_dark_foundry.asset>)
- [industry_electric_lighting](<Assets/Resources/SO/Research/UnlockBundles/industry_electric_lighting.asset>)
- [industry_electric_smelting](<Assets/Resources/SO/Research/UnlockBundles/industry_electric_smelting.asset>)
- [industry_junctions](<Assets/Resources/SO/Research/UnlockBundles/industry_junctions.asset>)
- [industry_lifts](<Assets/Resources/SO/Research/UnlockBundles/industry_lifts.asset>)
- [industry_line_balancing](<Assets/Resources/SO/Research/UnlockBundles/industry_line_balancing.asset>)
- [industry_mana_power](<Assets/Resources/SO/Research/UnlockBundles/industry_mana_power.asset>)
- [industry_powered_tools](<Assets/Resources/SO/Research/UnlockBundles/industry_powered_tools.asset>)
- [industry_precision](<Assets/Resources/SO/Research/UnlockBundles/industry_precision.asset>)
- [industry_rune_automation](<Assets/Resources/SO/Research/UnlockBundles/industry_rune_automation.asset>)
- [industry_steam_power](<Assets/Resources/SO/Research/UnlockBundles/industry_steam_power.asset>)
- [industry_storage](<Assets/Resources/SO/Research/UnlockBundles/industry_storage.asset>)
- [industry_waterwheel](<Assets/Resources/SO/Research/UnlockBundles/industry_waterwheel.asset>)
- [life_seasonal_calendar](<Assets/Resources/SO/Research/UnlockBundles/life_seasonal_calendar.asset>)
- [medical_aberrant_augmentation](<Assets/Resources/SO/Research/UnlockBundles/medical_aberrant_augmentation.asset>)
- [medical_anatomy](<Assets/Resources/SO/Research/UnlockBundles/medical_anatomy.asset>)
- [medical_avian_prosthetics](<Assets/Resources/SO/Research/UnlockBundles/medical_avian_prosthetics.asset>)
- [medical_blood_rejuvenation](<Assets/Resources/SO/Research/UnlockBundles/medical_blood_rejuvenation.asset>)
- [medical_bloodcraft_augmentation](<Assets/Resources/SO/Research/UnlockBundles/medical_bloodcraft_augmentation.asset>)
- [medical_construct_core_engineering](<Assets/Resources/SO/Research/UnlockBundles/medical_construct_core_engineering.asset>)
- [medical_construct_core_maintenance](<Assets/Resources/SO/Research/UnlockBundles/medical_construct_core_maintenance.asset>)
- [medical_geriatric_medicine](<Assets/Resources/SO/Research/UnlockBundles/medical_geriatric_medicine.asset>)
- [medical_gerontology](<Assets/Resources/SO/Research/UnlockBundles/medical_gerontology.asset>)
- [medical_mana_core_engineering](<Assets/Resources/SO/Research/UnlockBundles/medical_mana_core_engineering.asset>)
- [medical_mycelial_grafting](<Assets/Resources/SO/Research/UnlockBundles/medical_mycelial_grafting.asset>)
- [medical_organ_preservation](<Assets/Resources/SO/Research/UnlockBundles/medical_organ_preservation.asset>)
- [medical_organ_regeneration](<Assets/Resources/SO/Research/UnlockBundles/medical_organ_regeneration.asset>)
- [medical_prosthetics](<Assets/Resources/SO/Research/UnlockBundles/medical_prosthetics.asset>)
- [medical_reception](<Assets/Resources/SO/Research/UnlockBundles/medical_reception.asset>)
- [medical_regenerative_culture](<Assets/Resources/SO/Research/UnlockBundles/medical_regenerative_culture.asset>)
- [medical_reproductive_medicine](<Assets/Resources/SO/Research/UnlockBundles/medical_reproductive_medicine.asset>)
- [medical_rune_hibernation](<Assets/Resources/SO/Research/UnlockBundles/medical_rune_hibernation.asset>)
- [medical_slime_bioengineering](<Assets/Resources/SO/Research/UnlockBundles/medical_slime_bioengineering.asset>)
- [medical_surgery](<Assets/Resources/SO/Research/UnlockBundles/medical_surgery.asset>)
- [medical_temporal_stasis](<Assets/Resources/SO/Research/UnlockBundles/medical_temporal_stasis.asset>)
- [medical_trauma_medicine](<Assets/Resources/SO/Research/UnlockBundles/medical_trauma_medicine.asset>)
- [medical_whole_body_regeneration](<Assets/Resources/SO/Research/UnlockBundles/medical_whole_body_regeneration.asset>)
- [medical_xenotransplant](<Assets/Resources/SO/Research/UnlockBundles/medical_xenotransplant.asset>)
- [metallurgy_advanced](<Assets/Resources/SO/Research/UnlockBundles/metallurgy_advanced.asset>)
- [metallurgy_blacksteel](<Assets/Resources/SO/Research/UnlockBundles/metallurgy_blacksteel.asset>)
- [metallurgy_iron](<Assets/Resources/SO/Research/UnlockBundles/metallurgy_iron.asset>)
- [metallurgy_precious](<Assets/Resources/SO/Research/UnlockBundles/metallurgy_precious.asset>)
- [metallurgy_primitive](<Assets/Resources/SO/Research/UnlockBundles/metallurgy_primitive.asset>)
- [metallurgy_steel](<Assets/Resources/SO/Research/UnlockBundles/metallurgy_steel.asset>)
- [mining_deep](<Assets/Resources/SO/Research/UnlockBundles/mining_deep.asset>)
- [mining_mana](<Assets/Resources/SO/Research/UnlockBundles/mining_mana.asset>)
- [mining_quarry](<Assets/Resources/SO/Research/UnlockBundles/mining_quarry.asset>)
- [mining_sorting](<Assets/Resources/SO/Research/UnlockBundles/mining_sorting.asset>)
- [mining_stonecutting](<Assets/Resources/SO/Research/UnlockBundles/mining_stonecutting.asset>)
- [mining_surface](<Assets/Resources/SO/Research/UnlockBundles/mining_surface.asset>)
- [pharmacology_advanced](<Assets/Resources/SO/Research/UnlockBundles/pharmacology_advanced.asset>)
- [pharmacology_anesthesia](<Assets/Resources/SO/Research/UnlockBundles/pharmacology_anesthesia.asset>)
- [pharmacology_antiseptic](<Assets/Resources/SO/Research/UnlockBundles/pharmacology_antiseptic.asset>)
- [pharmacology_distillation](<Assets/Resources/SO/Research/UnlockBundles/pharmacology_distillation.asset>)
- [pharmacology_herbalism](<Assets/Resources/SO/Research/UnlockBundles/pharmacology_herbalism.asset>)
- [pharmacology_stimulants](<Assets/Resources/SO/Research/UnlockBundles/pharmacology_stimulants.asset>)
- [plumbing_basics](<Assets/Resources/SO/Research/UnlockBundles/plumbing_basics.asset>)
- [plumbing_reuse](<Assets/Resources/SO/Research/UnlockBundles/plumbing_reuse.asset>)
- [plumbing_rune_purification](<Assets/Resources/SO/Research/UnlockBundles/plumbing_rune_purification.asset>)
- [plumbing_sewer](<Assets/Resources/SO/Research/UnlockBundles/plumbing_sewer.asset>)
- [service_automation](<Assets/Resources/SO/Research/UnlockBundles/service_automation.asset>)
- [service_dining_operations](<Assets/Resources/SO/Research/UnlockBundles/service_dining_operations.asset>)
- [service_flow](<Assets/Resources/SO/Research/UnlockBundles/service_flow.asset>)
- [society_career_records](<Assets/Resources/SO/Research/UnlockBundles/society_career_records.asset>)
- [society_child_education](<Assets/Resources/SO/Research/UnlockBundles/society_child_education.asset>)
- [society_corpse_care](<Assets/Resources/SO/Research/UnlockBundles/society_corpse_care.asset>)
- [society_generation_management](<Assets/Resources/SO/Research/UnlockBundles/society_generation_management.asset>)
- [society_household_records](<Assets/Resources/SO/Research/UnlockBundles/society_household_records.asset>)
- [society_retirement](<Assets/Resources/SO/Research/UnlockBundles/society_retirement.asset>)
- [survival_field_rations](<Assets/Resources/SO/Research/UnlockBundles/survival_field_rations.asset>)
- [survival_medical](<Assets/Resources/SO/Research/UnlockBundles/survival_medical.asset>)
- [survival_preservation](<Assets/Resources/SO/Research/UnlockBundles/survival_preservation.asset>)
- [survival_sanitation](<Assets/Resources/SO/Research/UnlockBundles/survival_sanitation.asset>)
- [survival_seasonal_storage](<Assets/Resources/SO/Research/UnlockBundles/survival_seasonal_storage.asset>)
- [survival_support](<Assets/Resources/SO/Research/UnlockBundles/survival_support.asset>)
- [textile_dreamweave](<Assets/Resources/SO/Research/UnlockBundles/textile_dreamweave.asset>)
- [textile_fiber](<Assets/Resources/SO/Research/UnlockBundles/textile_fiber.asset>)
- [textile_layered](<Assets/Resources/SO/Research/UnlockBundles/textile_layered.asset>)
- [textile_rune_leather](<Assets/Resources/SO/Research/UnlockBundles/textile_rune_leather.asset>)
- [textile_tailoring](<Assets/Resources/SO/Research/UnlockBundles/textile_tailoring.asset>)
- [textile_tanning](<Assets/Resources/SO/Research/UnlockBundles/textile_tanning.asset>)

</details>

<details>
<summary>ResourceItemDefinitionSO — 366개</summary>

- [V3I01_깨끗한_물](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I01_깨끗한_물.asset>)
- [V3I02_황](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I02_황.asset>)
- [V3I03_납광석](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I03_납광석.asset>)
- [V3I04_초석](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I04_초석.asset>)
- [V3I05_종이](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I05_종이.asset>)
- [V3I06_납괴](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I06_납괴.asset>)
- [V3I07_납탄](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I07_납탄.asset>)
- [V3I08_밧줄](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I08_밧줄.asset>)
- [V3I09_기계_부품](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I09_기계_부품.asset>)
- [V3I100_강화_구속구](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I100_강화_구속구.asset>)
- [V3I101_공연_소품_상자](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I101_공연_소품_상자.asset>)
- [V3I102_연회_운반_수레](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I102_연회_운반_수레.asset>)
- [V3I103_밀폐형_계절_보관함](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I103_밀폐형_계절_보관함.asset>)
- [V3I104_동맹_신호_키트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I104_동맹_신호_키트.asset>)
- [V3I105_복도식_기폭_장치](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I105_복도식_기폭_장치.asset>)
- [V3I106_룬_식별_렌즈](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I106_룬_식별_렌즈.asset>)
- [V3I107_공성_보강_키트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I107_공성_보강_키트.asset>)
- [V3I108_경계_신호_나팔](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I108_경계_신호_나팔.asset>)
- [V3I109_재료_시험편](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I109_재료_시험편.asset>)
- [V3I10_정밀_부품](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I10_정밀_부품.asset>)
- [V3I110_검사_게이지](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I110_검사_게이지.asset>)
- [V3I111_절연_배선_묶음](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I111_절연_배선_묶음.asset>)
- [V3I112_수차_구동축](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I112_수차_구동축.asset>)
- [V3I113_재생수_필터_카트리지](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I113_재생수_필터_카트리지.asset>)
- [V3I114_룬_정화_결정](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I114_룬_정화_결정.asset>)
- [V3I115_형질_검사_키트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I115_형질_검사_키트.asset>)
- [V3I116_교차계통_안정화_배지](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I116_교차계통_안정화_배지.asset>)
- [V3I117_격리_치료_꾸러미](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I117_격리_치료_꾸러미.asset>)
- [V3I118_회춘_혈청](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I118_회춘_혈청.asset>)
- [V3I119_골렘_핵_케이스](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I119_골렘_핵_케이스.asset>)
- [V3I11_룬_도체](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I11_룬_도체.asset>)
- [V3I120_장기_보존_용기](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I120_장기_보존_용기.asset>)
- [V3I121_장기_재생_골격](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I121_장기_재생_골격.asset>)
- [V3I122_재생_배양액](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I122_재생_배양액.asset>)
- [V3I123_생식_치료제](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I123_생식_치료제.asset>)
- [V3I124_룬_동면_촉매](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I124_룬_동면_촉매.asset>)
- [V3I125_트라우마_치료_꾸러미](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I125_트라우마_치료_꾸러미.asset>)
- [V3I126_소이_화살](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I126_소이_화살.asset>)
- [V3I127_소이_볼트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I127_소이_볼트.asset>)
- [V3I128_연막_탄약](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I128_연막_탄약.asset>)
- [V3I129_철갑_탄약](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I129_철갑_탄약.asset>)
- [V3I12_무균_천](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I12_무균_천.asset>)
- [V3I130_산탄_탄약](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I130_산탄_탄약.asset>)
- [V3I131_신호탄](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I131_신호탄.asset>)
- [V3I132_흑강_볼트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I132_흑강_볼트.asset>)
- [V3I133_룬_탄약통](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I133_룬_탄약통.asset>)
- [V3I134_진정_다트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I134_진정_다트.asset>)
- [V3I135_마나_차단_볼트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I135_마나_차단_볼트.asset>)
- [V3I136_방어시설용_혼합_탄약_상자](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I136_방어시설용_혼합_탄약_상자.asset>)
- [V3I137_계보_인장](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I137_계보_인장.asset>)
- [V3I138_개량_부품](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I138_개량_부품.asset>)
- [V3I13_흑색화약](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I13_흑색화약.asset>)
- [V3I14_층상_충전재](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I14_층상_충전재.asset>)
- [V3I15_성장형_장비_골격](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I15_성장형_장비_골격.asset>)
- [V3I16_질산_비료](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I16_질산_비료.asset>)
- [V3I17_공학_도면](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I17_공학_도면.asset>)
- [V3I18_납_균형추](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I18_납_균형추.asset>)
- [V3I19_마나_차폐판](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I19_마나_차폐판.asset>)
- [V3I20_발파_장약](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I20_발파_장약.asset>)
- [V3I21_함정_산탄통](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I21_함정_산탄통.asset>)
- [V3I22_무균_붕대](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I22_무균_붕대.asset>)
- [V3I23_종이_탄약통](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I23_종이_탄약통.asset>)
- [V3I24_재고_감지반](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I24_재고_감지반.asset>)
- [V3I25_정비_키트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I25_정비_키트.asset>)
- [V3I26_동력_공구날](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I26_동력_공구날.asset>)
- [V3I27_마나_탐침](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I27_마나_탐침.asset>)
- [V3I28_정밀_게이지](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I28_정밀_게이지.asset>)
- [V3I29_시제품_설계_묶음](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I29_시제품_설계_묶음.asset>)
- [V3I30_공장_설치_도면](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I30_공장_설치_도면.asset>)
- [V3I31_공성_균형추_조립품](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I31_공성_균형추_조립품.asset>)
- [V3I32_룬_조율_차폐판](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I32_룬_조율_차폐판.asset>)
- [V3I33_마핵_케이스](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I33_마핵_케이스.asset>)
- [V3I34_룬_제어반](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I34_룬_제어반.asset>)
- [V3I35_룬_버스_결합기](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I35_룬_버스_결합기.asset>)
- [V3I36_무균_균사_이식편](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I36_무균_균사_이식편.asset>)
- [V3I37_점액_응고틀](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I37_점액_응고틀.asset>)
- [V3I38_방폭_외투_내피](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I38_방폭_외투_내피.asset>)
- [V3I39_브리간딘_안감](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I39_브리간딘_안감.asset>)
- [V3I40_심부_승강기](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I40_심부_승강기.asset>)
- [V3I41_탐광_키트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I41_탐광_키트.asset>)
- [V3I42_합금_도가니](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I42_합금_도가니.asset>)
- [V3I43_야전_수리_키트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I43_야전_수리_키트.asset>)
- [V3I44_흑강_방어_장갑판](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I44_흑강_방어_장갑판.asset>)
- [V3I45_동력_갑주_관절](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I45_동력_갑주_관절.asset>)
- [V3I46_몽직물_룬_안감](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I46_몽직물_룬_안감.asset>)
- [V3I47_몽직물_의식_장식](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I47_몽직물_의식_장식.asset>)
- [V3I48_룬가죽_장비_안감](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I48_룬가죽_장비_안감.asset>)
- [V3I49_룬가죽_조율_끈](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I49_룬가죽_조율_끈.asset>)
- [V3I50_독성_함정_도포제](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I50_독성_함정_도포제.asset>)
- [V3I51_종이_풀칠](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I51_종이_풀칠.asset>)
- [V3I52_직물_경화제](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I52_직물_경화제.asset>)
- [V3I53_균사_재배_배지](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I53_균사_재배_배지.asset>)
- [V3I54_전신_재생_배지](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I54_전신_재생_배지.asset>)
- [V3I55_시간_고정_인장](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I55_시간_고정_인장.asset>)
- [V3I56_해충_유인제](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I56_해충_유인제.asset>)
- [V3I57_식물성_살충제](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I57_식물성_살충제.asset>)
- [V3I58_살균제](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I58_살균제.asset>)
- [V3I59_동굴_독감_항원_표본](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I59_동굴_독감_항원_표본.asset>)
- [V3I60_적열병_항원_표본](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I60_적열병_항원_표본.asset>)
- [V3I61_장부패증_항원_표본](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I61_장부패증_항원_표본.asset>)
- [V3I62_포자폐증_항원_표본](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I62_포자폐증_항원_표본.asset>)
- [V3I63_마나두창_항원_표본](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I63_마나두창_항원_표본.asset>)
- [V3I64_혈액소모병_항원_표본](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I64_혈액소모병_항원_표본.asset>)
- [V3I65_점액역병_항원_표본](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I65_점액역병_항원_표본.asset>)
- [V3I66_동굴_독감_백신](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I66_동굴_독감_백신.asset>)
- [V3I67_적열병_백신](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I67_적열병_백신.asset>)
- [V3I68_장부패증_백신](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I68_장부패증_백신.asset>)
- [V3I69_포자폐증_백신](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I69_포자폐증_백신.asset>)
- [V3I70_마나두창_백신](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I70_마나두창_백신.asset>)
- [V3I71_혈액소모병_백신](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I71_혈액소모병_백신.asset>)
- [V3I72_점액역병_백신](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I72_점액역병_백신.asset>)
- [V3I73_적층_목재](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I73_적층_목재.asset>)
- [V3I74_경화_가죽](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I74_경화_가죽.asset>)
- [V3I75_사슬_망](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I75_사슬_망.asset>)
- [V3I76_판금_소재판](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I76_판금_소재판.asset>)
- [V3I77_용수철강](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I77_용수철강.asset>)
- [V3I78_총열강](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I78_총열강.asset>)
- [V3I79_과립_화약](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I79_과립_화약.asset>)
- [V3I80_탄약지](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I80_탄약지.asset>)
- [V3I81_절연_직물](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I81_절연_직물.asset>)
- [V3I82_정밀_광학계](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I82_정밀_광학계.asset>)
- [V3I83_마나_합금](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I83_마나_합금.asset>)
- [V3I84_무균_복합재](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I84_무균_복합재.asset>)
- [V3I85_인증_품종_종자_꾸러미](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I85_인증_품종_종자_꾸러미.asset>)
- [V3I86_온실_영양액](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I86_온실_영양액.asset>)
- [V3I87_기후_제어_매니폴드](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I87_기후_제어_매니폴드.asset>)
- [V3I88_기상_관측_도구함](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I88_기상_관측_도구함.asset>)
- [V3I89_계절력_책자](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I89_계절력_책자.asset>)
- [V3I90_접종_원목](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I90_접종_원목.asset>)
- [V3I91_비전_색인철](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I91_비전_색인철.asset>)
- [V3I92_행정_인장](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I92_행정_인장.asset>)
- [V3I93_방_칸막이_키트](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I93_방_칸막이_키트.asset>)
- [V3I94_경력_장부](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I94_경력_장부.asset>)
- [V3I95_번식_장부](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I95_번식_장부.asset>)
- [V3I96_종족_장례_준비품](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I96_종족_장례_준비품.asset>)
- [V3I97_운반_멜빵](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I97_운반_멜빵.asset>)
- [V3I98_가격표_게시판](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I98_가격표_게시판.asset>)
- [V3I99_포로_작업_도구](<Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I99_포로_작업_도구.asset>)
- [apparel_apron](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_apron.asset>)
- [apparel_belt](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_belt.asset>)
- [apparel_blouse](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_blouse.asset>)
- [apparel_boots](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_boots.asset>)
- [apparel_ceremonial_dress](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_ceremonial_dress.asset>)
- [apparel_chest_wrap](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_chest_wrap.asset>)
- [apparel_cloak](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_cloak.asset>)
- [apparel_contract_sash](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_contract_sash.asset>)
- [apparel_daily_robe](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_daily_robe.asset>)
- [apparel_envoy_coat](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_envoy_coat.asset>)
- [apparel_farmer_workwear](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_farmer_workwear.asset>)
- [apparel_festival_vest](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_festival_vest.asset>)
- [apparel_footwraps](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_footwraps.asset>)
- [apparel_formal_coat](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_formal_coat.asset>)
- [apparel_gloves](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_gloves.asset>)
- [apparel_golem_functional_lining](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_golem_functional_lining.asset>)
- [apparel_hat](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_hat.asset>)
- [apparel_heat_work_suit](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_heat_work_suit.asset>)
- [apparel_hooded_robe](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_hooded_robe.asset>)
- [apparel_horn_ring](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_horn_ring.asset>)
- [apparel_keeper_coat](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_keeper_coat.asset>)
- [apparel_loincloth_underwear](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_loincloth_underwear.asset>)
- [apparel_long_underpants](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_long_underpants.asset>)
- [apparel_lower_underwear](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_lower_underwear.asset>)
- [apparel_miner_workwear](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_miner_workwear.asset>)
- [apparel_mourning_clothes](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_mourning_clothes.asset>)
- [apparel_raincoat](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_raincoat.asset>)
- [apparel_ritual_robe](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_ritual_robe.asset>)
- [apparel_scarf](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_scarf.asset>)
- [apparel_shorts](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_shorts.asset>)
- [apparel_skirt](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_skirt.asset>)
- [apparel_sky_chorus_shawl](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_sky_chorus_shawl.asset>)
- [apparel_sleep_bottom](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_sleep_bottom.asset>)
- [apparel_sleep_top](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_sleep_top.asset>)
- [apparel_smith_apron](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_smith_apron.asset>)
- [apparel_smoke_protection_hood](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_smoke_protection_hood.asset>)
- [apparel_socks](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_socks.asset>)
- [apparel_spore_garden_cloak](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_spore_garden_cloak.asset>)
- [apparel_spore_protection_hood](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_spore_protection_hood.asset>)
- [apparel_sterile_gown](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_sterile_gown.asset>)
- [apparel_surgical_apron](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_surgical_apron.asset>)
- [apparel_tail_guard](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_tail_guard.asset>)
- [apparel_tail_ribbon](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_tail_ribbon.asset>)
- [apparel_trousers](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_trousers.asset>)
- [apparel_tunic](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_tunic.asset>)
- [apparel_undershirt](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_undershirt.asset>)
- [apparel_vest](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_vest.asset>)
- [apparel_waterproof_work_suit](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_waterproof_work_suit.asset>)
- [apparel_weapon_vigil_cloak](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_weapon_vigil_cloak.asset>)
- [apparel_wing_cloak](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_wing_cloak.asset>)
- [apparel_wing_harness](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_wing_harness.asset>)
- [apparel_work_shirt](<Assets/Resources/SO/Economy/Items/V22Apparel/apparel_work_shirt.asset>)
- [fiber_cave_silk](<Assets/Resources/SO/Economy/Items/V22Apparel/fiber_cave_silk.asset>)
- [fiber_deep_goat_wool](<Assets/Resources/SO/Economy/Items/V22Apparel/fiber_deep_goat_wool.asset>)
- [fiber_ember_cotton](<Assets/Resources/SO/Economy/Items/V22Apparel/fiber_ember_cotton.asset>)
- [fiber_frost_flax](<Assets/Resources/SO/Economy/Items/V22Apparel/fiber_frost_flax.asset>)
- [fiber_frost_wool](<Assets/Resources/SO/Economy/Items/V22Apparel/fiber_frost_wool.asset>)
- [fiber_mire_reed](<Assets/Resources/SO/Economy/Items/V22Apparel/fiber_mire_reed.asset>)
- [fiber_spore_hemp](<Assets/Resources/SO/Economy/Items/V22Apparel/fiber_spore_hemp.asset>)
- [material_cave_silk](<Assets/Resources/SO/Economy/Items/V22Apparel/material_cave_silk.asset>)
- [material_common_wool](<Assets/Resources/SO/Economy/Items/V22Apparel/material_common_wool.asset>)
- [material_deep_goat_wool](<Assets/Resources/SO/Economy/Items/V22Apparel/material_deep_goat_wool.asset>)
- [material_ember_cotton](<Assets/Resources/SO/Economy/Items/V22Apparel/material_ember_cotton.asset>)
- [material_frost_linen](<Assets/Resources/SO/Economy/Items/V22Apparel/material_frost_linen.asset>)
- [material_frost_wool](<Assets/Resources/SO/Economy/Items/V22Apparel/material_frost_wool.asset>)
- [material_mending_scrap](<Assets/Resources/SO/Economy/Items/V22Apparel/material_mending_scrap.asset>)
- [material_mire_canvas](<Assets/Resources/SO/Economy/Items/V22Apparel/material_mire_canvas.asset>)
- [material_sewing_thread](<Assets/Resources/SO/Economy/Items/V22Apparel/material_sewing_thread.asset>)
- [material_spore_hemp](<Assets/Resources/SO/Economy/Items/V22Apparel/material_spore_hemp.asset>)
- [seed_lot_ember_cotton](<Assets/Resources/SO/Economy/Items/V22Apparel/seed_lot_ember_cotton.asset>)
- [seed_lot_frost_flax](<Assets/Resources/SO/Economy/Items/V22Apparel/seed_lot_frost_flax.asset>)
- [seed_lot_mire_reed](<Assets/Resources/SO/Economy/Items/V22Apparel/seed_lot_mire_reed.asset>)
- [seed_lot_spore_hemp](<Assets/Resources/SO/Economy/Items/V22Apparel/seed_lot_spore_hemp.asset>)
- [tool_sewing_kit](<Assets/Resources/SO/Economy/Items/V22Apparel/tool_sewing_kit.asset>)
- [yarn_cave_silk](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_cave_silk.asset>)
- [yarn_common_wool](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_common_wool.asset>)
- [yarn_deep_goat_wool](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_deep_goat_wool.asset>)
- [yarn_dreamweave](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_dreamweave.asset>)
- [yarn_ember_cotton](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_ember_cotton.asset>)
- [yarn_frost_linen](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_frost_linen.asset>)
- [yarn_frost_wool](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_frost_wool.asset>)
- [yarn_mire_canvas](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_mire_canvas.asset>)
- [yarn_shade_cloth](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_shade_cloth.asset>)
- [yarn_spore_hemp](<Assets/Resources/SO/Economy/Items/V22Apparel/yarn_spore_hemp.asset>)
- [craft_fermented_vinegar](<Assets/Resources/SO/Economy/Items/Workshop/craft_fermented_vinegar.asset>)
- [feed_silage](<Assets/Resources/SO/Economy/Items/Workshop/feed_silage.asset>)
- [food_expedition_ration_pack](<Assets/Resources/SO/Economy/Items/Workshop/food_expedition_ration_pack.asset>)
- [food_fermented_pickle](<Assets/Resources/SO/Economy/Items/Workshop/food_fermented_pickle.asset>)
- [food_fresh_curd](<Assets/Resources/SO/Economy/Items/Workshop/food_fresh_curd.asset>)
- [food_grape_syrup](<Assets/Resources/SO/Economy/Items/Workshop/food_grape_syrup.asset>)
- [food_malt_porridge](<Assets/Resources/SO/Economy/Items/Workshop/food_malt_porridge.asset>)
- [food_night_spirit](<Assets/Resources/SO/Economy/Items/Workshop/food_night_spirit.asset>)
- [food_preserved_vegetable](<Assets/Resources/SO/Economy/Items/Workshop/food_preserved_vegetable.asset>)
- [food_salted_meat_stew](<Assets/Resources/SO/Economy/Items/Workshop/food_salted_meat_stew.asset>)
- [food_stuffed_mushroom](<Assets/Resources/SO/Economy/Items/Workshop/food_stuffed_mushroom.asset>)
- [food_twilight_beer](<Assets/Resources/SO/Economy/Items/Workshop/food_twilight_beer.asset>)
- [food_vegetable_pie](<Assets/Resources/SO/Economy/Items/Workshop/food_vegetable_pie.asset>)
- [material_brined_vegetable](<Assets/Resources/SO/Economy/Items/Workshop/material_brined_vegetable.asset>)
- [material_curd](<Assets/Resources/SO/Economy/Items/Workshop/material_curd.asset>)
- [material_dough](<Assets/Resources/SO/Economy/Items/Workshop/material_dough.asset>)
- [material_fermented_liquor](<Assets/Resources/SO/Economy/Items/Workshop/material_fermented_liquor.asset>)
- [material_grape_juice](<Assets/Resources/SO/Economy/Items/Workshop/material_grape_juice.asset>)
- [material_malt](<Assets/Resources/SO/Economy/Items/Workshop/material_malt.asset>)
- [material_ration_mixture](<Assets/Resources/SO/Economy/Items/Workshop/material_ration_mixture.asset>)
- [material_salted_meat](<Assets/Resources/SO/Economy/Items/Workshop/material_salted_meat.asset>)
- [material_seasoned_filling](<Assets/Resources/SO/Economy/Items/Workshop/material_seasoned_filling.asset>)
- [material_washed_vegetable](<Assets/Resources/SO/Economy/Items/Workshop/material_washed_vegetable.asset>)
- [material_young_wine](<Assets/Resources/SO/Economy/Items/Workshop/material_young_wine.asset>)
- [ammo_arrow_bone](<Assets/Resources/SO/Economy/Items/ammo_arrow_bone.asset>)
- [ammo_arrow_iron](<Assets/Resources/SO/Economy/Items/ammo_arrow_iron.asset>)
- [ammo_arrow_rune](<Assets/Resources/SO/Economy/Items/ammo_arrow_rune.asset>)
- [ammo_arrow_steel](<Assets/Resources/SO/Economy/Items/ammo_arrow_steel.asset>)
- [ammo_bolt_bone](<Assets/Resources/SO/Economy/Items/ammo_bolt_bone.asset>)
- [ammo_bolt_iron](<Assets/Resources/SO/Economy/Items/ammo_bolt_iron.asset>)
- [ammo_bolt_rune](<Assets/Resources/SO/Economy/Items/ammo_bolt_rune.asset>)
- [ammo_bolt_steel](<Assets/Resources/SO/Economy/Items/ammo_bolt_steel.asset>)
- [container_medical_vial](<Assets/Resources/SO/Economy/Items/container_medical_vial.asset>)
- [craft_bone_charm](<Assets/Resources/SO/Economy/Items/craft_bone_charm.asset>)
- [craft_candle](<Assets/Resources/SO/Economy/Items/craft_candle.asset>)
- [craft_fang_poison](<Assets/Resources/SO/Economy/Items/craft_fang_poison.asset>)
- [craft_gold_ornament](<Assets/Resources/SO/Economy/Items/craft_gold_ornament.asset>)
- [craft_resin_balm](<Assets/Resources/SO/Economy/Items/craft_resin_balm.asset>)
- [craft_ritual_reagent](<Assets/Resources/SO/Economy/Items/craft_ritual_reagent.asset>)
- [craft_soap](<Assets/Resources/SO/Economy/Items/craft_soap.asset>)
- [craft_stone_ornament](<Assets/Resources/SO/Economy/Items/craft_stone_ornament.asset>)
- [drug_blood_stimulant](<Assets/Resources/SO/Economy/Items/drug_blood_stimulant.asset>)
- [drug_dreamleaf_analgesic](<Assets/Resources/SO/Economy/Items/drug_dreamleaf_analgesic.asset>)
- [drug_hallucinogenic_distillate](<Assets/Resources/SO/Economy/Items/drug_hallucinogenic_distillate.asset>)
- [drug_mana_awakener](<Assets/Resources/SO/Economy/Items/drug_mana_awakener.asset>)
- [drug_moonflower_tea](<Assets/Resources/SO/Economy/Items/drug_moonflower_tea.asset>)
- [drug_night_wine](<Assets/Resources/SO/Economy/Items/drug_night_wine.asset>)
- [drug_vitality_tonic](<Assets/Resources/SO/Economy/Items/drug_vitality_tonic.asset>)
- [equipment_cold_work_suit](<Assets/Resources/SO/Economy/Items/equipment_cold_work_suit.asset>)
- [equipment_rune_cold_suit](<Assets/Resources/SO/Economy/Items/equipment_rune_cold_suit.asset>)
- [equipment_slime_warming_pad](<Assets/Resources/SO/Economy/Items/equipment_slime_warming_pad.asset>)
- [feed_dog_food](<Assets/Resources/SO/Economy/Items/feed_dog_food.asset>)
- [feed_dog_food_fresh](<Assets/Resources/SO/Economy/Items/feed_dog_food_fresh.asset>)
- [feed_hay](<Assets/Resources/SO/Economy/Items/feed_hay.asset>)
- [food_boar_stew](<Assets/Resources/SO/Economy/Items/food_boar_stew.asset>)
- [food_cheese_mushroom](<Assets/Resources/SO/Economy/Items/food_cheese_mushroom.asset>)
- [food_egg_pancake](<Assets/Resources/SO/Economy/Items/food_egg_pancake.asset>)
- [food_garden_meal](<Assets/Resources/SO/Economy/Items/food_garden_meal.asset>)
- [food_grain_porridge](<Assets/Resources/SO/Economy/Items/food_grain_porridge.asset>)
- [food_jerky](<Assets/Resources/SO/Economy/Items/food_jerky.asset>)
- [food_lavish_meat](<Assets/Resources/SO/Economy/Items/food_lavish_meat.asset>)
- [food_lavish_vegan](<Assets/Resources/SO/Economy/Items/food_lavish_vegan.asset>)
- [food_meat_pie](<Assets/Resources/SO/Economy/Items/food_meat_pie.asset>)
- [food_mushroom_soup](<Assets/Resources/SO/Economy/Items/food_mushroom_soup.asset>)
- [food_preserved_ration](<Assets/Resources/SO/Economy/Items/food_preserved_ration.asset>)
- [food_roasted_meat](<Assets/Resources/SO/Economy/Items/food_roasted_meat.asset>)
- [food_root_stew](<Assets/Resources/SO/Economy/Items/food_root_stew.asset>)
- [husbandry_bedding](<Assets/Resources/SO/Economy/Items/husbandry_bedding.asset>)
- [material_alchemical_solvent](<Assets/Resources/SO/Economy/Items/material_alchemical_solvent.asset>)
- [material_alcohol](<Assets/Resources/SO/Economy/Items/material_alcohol.asset>)
- [material_blacksteel_ingot](<Assets/Resources/SO/Economy/Items/material_blacksteel_ingot.asset>)
- [material_bowstring](<Assets/Resources/SO/Economy/Items/material_bowstring.asset>)
- [material_charcoal](<Assets/Resources/SO/Economy/Items/material_charcoal.asset>)
- [material_cheese](<Assets/Resources/SO/Economy/Items/material_cheese.asset>)
- [material_cloth](<Assets/Resources/SO/Economy/Items/material_cloth.asset>)
- [material_compost](<Assets/Resources/SO/Economy/Items/material_compost.asset>)
- [material_dreamweave](<Assets/Resources/SO/Economy/Items/material_dreamweave.asset>)
- [material_flour](<Assets/Resources/SO/Economy/Items/material_flour.asset>)
- [material_gold_ingot](<Assets/Resources/SO/Economy/Items/material_gold_ingot.asset>)
- [material_iron_ingot](<Assets/Resources/SO/Economy/Items/material_iron_ingot.asset>)
- [material_leather](<Assets/Resources/SO/Economy/Items/material_leather.asset>)
- [material_low_fuel](<Assets/Resources/SO/Economy/Items/material_low_fuel.asset>)
- [material_lumber](<Assets/Resources/SO/Economy/Items/material_lumber.asset>)
- [material_rot_toxin](<Assets/Resources/SO/Economy/Items/material_rot_toxin.asset>)
- [material_rune_leather](<Assets/Resources/SO/Economy/Items/material_rune_leather.asset>)
- [material_starch](<Assets/Resources/SO/Economy/Items/material_starch.asset>)
- [material_steel_ingot](<Assets/Resources/SO/Economy/Items/material_steel_ingot.asset>)
- [material_stone_block](<Assets/Resources/SO/Economy/Items/material_stone_block.asset>)
- [material_syrup](<Assets/Resources/SO/Economy/Items/material_syrup.asset>)
- [material_tallow](<Assets/Resources/SO/Economy/Items/material_tallow.asset>)
- [material_treated_lumber](<Assets/Resources/SO/Economy/Items/material_treated_lumber.asset>)
- [medicine_advanced](<Assets/Resources/SO/Economy/Items/medicine_advanced.asset>)
- [medicine_anesthetic](<Assets/Resources/SO/Economy/Items/medicine_anesthetic.asset>)
- [medicine_antidote](<Assets/Resources/SO/Economy/Items/medicine_antidote.asset>)
- [medicine_antiseptic](<Assets/Resources/SO/Economy/Items/medicine_antiseptic.asset>)
- [medicine_herbal_poultice](<Assets/Resources/SO/Economy/Items/medicine_herbal_poultice.asset>)
- [medicine_standard](<Assets/Resources/SO/Economy/Items/medicine_standard.asset>)
- [offense_appraised_valuables](<Assets/Resources/SO/Economy/Items/offense_appraised_valuables.asset>)
- [offense_unappraised_loot](<Assets/Resources/SO/Economy/Items/offense_unappraised_loot.asset>)
- [resource_blood](<Assets/Resources/SO/Economy/Items/resource_blood.asset>)
- [resource_bloodleaf](<Assets/Resources/SO/Economy/Items/resource_bloodleaf.asset>)
- [resource_bone](<Assets/Resources/SO/Economy/Items/resource_bone.asset>)
- [resource_cave_mushroom](<Assets/Resources/SO/Economy/Items/resource_cave_mushroom.asset>)
- [resource_coal](<Assets/Resources/SO/Economy/Items/resource_coal.asset>)
- [resource_crystal_beetle_carapace](<Assets/Resources/SO/Economy/Items/resource_crystal_beetle_carapace.asset>)
- [resource_dark_resin](<Assets/Resources/SO/Economy/Items/resource_dark_resin.asset>)
- [resource_dreamleaf](<Assets/Resources/SO/Economy/Items/resource_dreamleaf.asset>)
- [resource_egg](<Assets/Resources/SO/Economy/Items/resource_egg.asset>)
- [resource_ember_root](<Assets/Resources/SO/Economy/Items/resource_ember_root.asset>)
- [resource_fang](<Assets/Resources/SO/Economy/Items/resource_fang.asset>)
- [resource_fat](<Assets/Resources/SO/Economy/Items/resource_fat.asset>)
- [resource_feather](<Assets/Resources/SO/Economy/Items/resource_feather.asset>)
- [resource_gold_ore](<Assets/Resources/SO/Economy/Items/resource_gold_ore.asset>)
- [resource_grass_straw](<Assets/Resources/SO/Economy/Items/resource_grass_straw.asset>)
- [resource_hide](<Assets/Resources/SO/Economy/Items/resource_hide.asset>)
- [resource_horn](<Assets/Resources/SO/Economy/Items/resource_horn.asset>)
- [resource_iron_ore](<Assets/Resources/SO/Economy/Items/resource_iron_ore.asset>)
- [resource_log](<Assets/Resources/SO/Economy/Items/resource_log.asset>)
- [resource_mana_crystal](<Assets/Resources/SO/Economy/Items/resource_mana_crystal.asset>)
- [resource_manure](<Assets/Resources/SO/Economy/Items/resource_manure.asset>)
- [resource_meat](<Assets/Resources/SO/Economy/Items/resource_meat.asset>)
- [resource_milk](<Assets/Resources/SO/Economy/Items/resource_milk.asset>)
- [resource_moonflower](<Assets/Resources/SO/Economy/Items/resource_moonflower.asset>)
- [resource_night_grape](<Assets/Resources/SO/Economy/Items/resource_night_grape.asset>)
- [resource_rune_dust](<Assets/Resources/SO/Economy/Items/resource_rune_dust.asset>)
- [resource_saltstone](<Assets/Resources/SO/Economy/Items/resource_saltstone.asset>)
- [resource_shade_fiber](<Assets/Resources/SO/Economy/Items/resource_shade_fiber.asset>)
- [resource_stone](<Assets/Resources/SO/Economy/Items/resource_stone.asset>)
- [resource_trail_charm](<Assets/Resources/SO/Economy/Items/resource_trail_charm.asset>)
- [resource_twilight_grain](<Assets/Resources/SO/Economy/Items/resource_twilight_grain.asset>)
- [resource_wool](<Assets/Resources/SO/Economy/Items/resource_wool.asset>)
- [seed_lot_bloodleaf](<Assets/Resources/SO/Economy/Items/seed_lot_bloodleaf.asset>)
- [seed_lot_cave_mushroom](<Assets/Resources/SO/Economy/Items/seed_lot_cave_mushroom.asset>)
- [seed_lot_dreamleaf](<Assets/Resources/SO/Economy/Items/seed_lot_dreamleaf.asset>)
- [seed_lot_ember_root](<Assets/Resources/SO/Economy/Items/seed_lot_ember_root.asset>)
- [seed_lot_moonflower](<Assets/Resources/SO/Economy/Items/seed_lot_moonflower.asset>)
- [seed_lot_night_grape](<Assets/Resources/SO/Economy/Items/seed_lot_night_grape.asset>)
- [seed_lot_shade_fiber](<Assets/Resources/SO/Economy/Items/seed_lot_shade_fiber.asset>)
- [seed_lot_twilight_grain](<Assets/Resources/SO/Economy/Items/seed_lot_twilight_grain.asset>)
- [waste_animal_rot](<Assets/Resources/SO/Economy/Items/waste_animal_rot.asset>)
- [waste_forbidden_rot](<Assets/Resources/SO/Economy/Items/waste_forbidden_rot.asset>)
- [waste_mixed_rot](<Assets/Resources/SO/Economy/Items/waste_mixed_rot.asset>)
- [waste_plant_rot](<Assets/Resources/SO/Economy/Items/waste_plant_rot.asset>)
- [medicine_mycelial_culture_pack](<Assets/Resources/SO/Items/Definitions/medicine_mycelial_culture_pack.asset>)

</details>

<details>
<summary>RoomEnvironmentSettingsSO — 1개</summary>

- [RoomEnvironmentSettings](<Assets/Resources/Config/RoomEnvironmentSettings.asset>)

</details>

<details>
<summary>SaleItem — 4개</summary>

- [도란검](<Assets/Resources/SO/Stock/Item/도란검.asset>)
- [도란방패](<Assets/Resources/SO/Stock/Item/도란방패.asset>)
- [모험용품](<Assets/Resources/SO/Stock/Item/모험용품.asset>)
- [햄버거](<Assets/Resources/SO/Stock/Item/햄버거.asset>)

</details>

<details>
<summary>SeasonalWorldEventDefinitionSO — 28개</summary>

- [seasonal_autumn-caravan-rush](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_autumn-caravan-rush.asset>)
- [seasonal_autumn-early-frost](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_autumn-early-frost.asset>)
- [seasonal_autumn-harvest-dispute](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_autumn-harvest-dispute.asset>)
- [seasonal_autumn-migration-window](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_autumn-migration-window.asset>)
- [seasonal_autumn-predator-descent](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_autumn-predator-descent.asset>)
- [seasonal_autumn-rot-cart](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_autumn-rot-cart.asset>)
- [seasonal_autumn-spoiled-silage](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_autumn-spoiled-silage.asset>)
- [seasonal_spring-drift-cargo](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-drift-cargo.asset>)
- [seasonal_spring-fever-camp](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-fever-camp.asset>)
- [seasonal_spring-migrant-herd](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-migrant-herd.asset>)
- [seasonal_spring-seed-exchange](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-seed-exchange.asset>)
- [seasonal_spring-spore-rain](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-spore-rain.asset>)
- [seasonal_spring-thaw-flood](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-thaw-flood.asset>)
- [seasonal_spring-washed-road](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_spring-washed-road.asset>)
- [seasonal_summer-dry-well](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_summer-dry-well.asset>)
- [seasonal_summer-festival-scarcity](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_summer-festival-scarcity.asset>)
- [seasonal_summer-heat-grid](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_summer-heat-grid.asset>)
- [seasonal_summer-mana-lightning](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_summer-mana-lightning.asset>)
- [seasonal_summer-smoke-valley](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_summer-smoke-valley.asset>)
- [seasonal_summer-vermin-bloom](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_summer-vermin-bloom.asset>)
- [seasonal_summer-wounded-mercenaries](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_summer-wounded-mercenaries.asset>)
- [seasonal_winter-cave-flu-wave](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_winter-cave-flu-wave.asset>)
- [seasonal_winter-deep-echo](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_winter-deep-echo.asset>)
- [seasonal_winter-frozen-pipes](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_winter-frozen-pipes.asset>)
- [seasonal_winter-fuel-demand](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_winter-fuel-demand.asset>)
- [seasonal_winter-hungry-pack](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_winter-hungry-pack.asset>)
- [seasonal_winter-memorial-envoys](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_winter-memorial-envoys.asset>)
- [seasonal_winter-whiteout](<Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_winter-whiteout.asset>)

</details>

<details>
<summary>ServiceIncidentDefinitionSO — 8개</summary>

- [service-incident_brawl](<Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_brawl.asset>)
- [service-incident_contamination](<Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_contamination.asset>)
- [service-incident_culturalinsult](<Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_culturalinsult.asset>)
- [service-incident_envoyconflict](<Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_envoyconflict.asset>)
- [service-incident_forbiddenmeal](<Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_forbiddenmeal.asset>)
- [service-incident_medicalcollapse](<Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_medicalcollapse.asset>)
- [service-incident_sabotage](<Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_sabotage.asset>)
- [service-incident_theft](<Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_theft.asset>)

</details>

<details>
<summary>ServiceProcessSO — 5개</summary>

- [service_bathing_wash](<Assets/Resources/SO/ServiceRooms/Processes/service_bathing_wash.asset>)
- [service_dining_meal](<Assets/Resources/SO/ServiceRooms/Processes/service_dining_meal.asset>)
- [service_lodging_rest](<Assets/Resources/SO/ServiceRooms/Processes/service_lodging_rest.asset>)
- [service_medical_treat](<Assets/Resources/SO/ServiceRooms/Processes/service_medical_treat.asset>)
- [service_retail_sale](<Assets/Resources/SO/ServiceRooms/Processes/service_retail_sale.asset>)

</details>

<details>
<summary>SpeciesCultureDefinitionSO — 10개</summary>

- [culture_adventurer-frontier](<Assets/Resources/SO/V20/Narrative/Cultures/culture_adventurer-frontier.asset>)
- [culture_beastkin-pack](<Assets/Resources/SO/V20/Narrative/Cultures/culture_beastkin-pack.asset>)
- [culture_demon-contract](<Assets/Resources/SO/V20/Narrative/Cultures/culture_demon-contract.asset>)
- [culture_golem-core](<Assets/Resources/SO/V20/Narrative/Cultures/culture_golem-core.asset>)
- [culture_harpy-aerie](<Assets/Resources/SO/V20/Narrative/Cultures/culture_harpy-aerie.asset>)
- [culture_kobold-toolclan](<Assets/Resources/SO/V20/Narrative/Cultures/culture_kobold-toolclan.asset>)
- [culture_myconid-grove](<Assets/Resources/SO/V20/Narrative/Cultures/culture_myconid-grove.asset>)
- [culture_orc-vigil](<Assets/Resources/SO/V20/Narrative/Cultures/culture_orc-vigil.asset>)
- [culture_slime-confluence](<Assets/Resources/SO/V20/Narrative/Cultures/culture_slime-confluence.asset>)
- [culture_vampire-nightcourt](<Assets/Resources/SO/V20/Narrative/Cultures/culture_vampire-nightcourt.asset>)

</details>

<details>
<summary>SpeciesLifeHistorySO — 11개</summary>

- [Life_Adventurer](<Assets/Resources/SO/Population/Life/Life_Adventurer.asset>)
- [Life_Beastkin](<Assets/Resources/SO/Population/Life/Life_Beastkin.asset>)
- [Life_Demon](<Assets/Resources/SO/Population/Life/Life_Demon.asset>)
- [Life_Golem](<Assets/Resources/SO/Population/Life/Life_Golem.asset>)
- [Life_Harpy](<Assets/Resources/SO/Population/Life/Life_Harpy.asset>)
- [Life_Human](<Assets/Resources/SO/Population/Life/Life_Human.asset>)
- [Life_Kobold](<Assets/Resources/SO/Population/Life/Life_Kobold.asset>)
- [Life_Myconid](<Assets/Resources/SO/Population/Life/Life_Myconid.asset>)
- [Life_Orc](<Assets/Resources/SO/Population/Life/Life_Orc.asset>)
- [Life_Slime](<Assets/Resources/SO/Population/Life/Life_Slime.asset>)
- [Life_Vampire](<Assets/Resources/SO/Population/Life/Life_Vampire.asset>)

</details>

<details>
<summary>StatChange — 1개</summary>

- [Hamburger](<Assets/Resources/SO/Stock/Item/Onbuy/Hamburger.asset>)

</details>

<details>
<summary>StockInfo — 1개</summary>

- [S01_SalesCounterStock](<Assets/Resources/SO/Stock/Modular/S01_SalesCounterStock.asset>)

</details>

<details>
<summary>SurgicalProcedureSO — 47개</summary>

- [procedure_amputation](<Assets/Resources/SO/Medical/Procedures/procedure_amputation.asset>)
- [procedure_arcane-modification](<Assets/Resources/SO/Medical/Procedures/procedure_arcane-modification.asset>)
- [procedure_beastkin-sprint-joint](<Assets/Resources/SO/Medical/Procedures/procedure_beastkin-sprint-joint.asset>)
- [procedure_beastkin-tail-reconstruction](<Assets/Resources/SO/Medical/Procedures/procedure_beastkin-tail-reconstruction.asset>)
- [procedure_blood-rejuvenation](<Assets/Resources/SO/Medical/Procedures/procedure_blood-rejuvenation.asset>)
- [procedure_blood-transfusion](<Assets/Resources/SO/Medical/Procedures/procedure_blood-transfusion.asset>)
- [procedure_corpse-organ-extraction](<Assets/Resources/SO/Medical/Procedures/procedure_corpse-organ-extraction.asset>)
- [procedure_demon-heat-sac](<Assets/Resources/SO/Medical/Procedures/procedure_demon-heat-sac.asset>)
- [procedure_demon-mana-core-suture](<Assets/Resources/SO/Medical/Procedures/procedure_demon-mana-core-suture.asset>)
- [procedure_emergency-suture](<Assets/Resources/SO/Medical/Procedures/procedure_emergency-suture.asset>)
- [procedure_foreign-body-removal](<Assets/Resources/SO/Medical/Procedures/procedure_foreign-body-removal.asset>)
- [procedure_golem-body-recast](<Assets/Resources/SO/Medical/Procedures/procedure_golem-body-recast.asset>)
- [procedure_golem-coolant-refill](<Assets/Resources/SO/Medical/Procedures/procedure_golem-coolant-refill.asset>)
- [procedure_golem-power-core](<Assets/Resources/SO/Medical/Procedures/procedure_golem-power-core.asset>)
- [procedure_golem-sensor-core](<Assets/Resources/SO/Medical/Procedures/procedure_golem-sensor-core.asset>)
- [procedure_golem-servo-alignment](<Assets/Resources/SO/Medical/Procedures/procedure_golem-servo-alignment.asset>)
- [procedure_harpy-air-sac-suture](<Assets/Resources/SO/Medical/Procedures/procedure_harpy-air-sac-suture.asset>)
- [procedure_harpy-feather-regrowth](<Assets/Resources/SO/Medical/Procedures/procedure_harpy-feather-regrowth.asset>)
- [procedure_harpy-tail-graft](<Assets/Resources/SO/Medical/Procedures/procedure_harpy-tail-graft.asset>)
- [procedure_harpy-wing-fixation](<Assets/Resources/SO/Medical/Procedures/procedure_harpy-wing-fixation.asset>)
- [procedure_human-neural-assist](<Assets/Resources/SO/Medical/Procedures/procedure_human-neural-assist.asset>)
- [procedure_human-precision-prosthetic](<Assets/Resources/SO/Medical/Procedures/procedure_human-precision-prosthetic.asset>)
- [procedure_implant-installation](<Assets/Resources/SO/Medical/Procedures/procedure_implant-installation.asset>)
- [procedure_kobold-precision-hand](<Assets/Resources/SO/Medical/Procedures/procedure_kobold-precision-hand.asset>)
- [procedure_kobold-tail-balance](<Assets/Resources/SO/Medical/Procedures/procedure_kobold-tail-balance.asset>)
- [procedure_live-organ-extraction](<Assets/Resources/SO/Medical/Procedures/procedure_live-organ-extraction.asset>)
- [procedure_myconid-core-graft](<Assets/Resources/SO/Medical/Procedures/procedure_myconid-core-graft.asset>)
- [procedure_myconid-hypha-binding](<Assets/Resources/SO/Medical/Procedures/procedure_myconid-hypha-binding.asset>)
- [procedure_myconid-regrowth](<Assets/Resources/SO/Medical/Procedures/procedure_myconid-regrowth.asset>)
- [procedure_myconid-spore-cleaning](<Assets/Resources/SO/Medical/Procedures/procedure_myconid-spore-cleaning.asset>)
- [procedure_natural-organ-transplant](<Assets/Resources/SO/Medical/Procedures/procedure_natural-organ-transplant.asset>)
- [procedure_orc-combat-heart](<Assets/Resources/SO/Medical/Procedures/procedure_orc-combat-heart.asset>)
- [procedure_orc-skeletal-reinforcement](<Assets/Resources/SO/Medical/Procedures/procedure_orc-skeletal-reinforcement.asset>)
- [procedure_organ-regeneration](<Assets/Resources/SO/Medical/Procedures/procedure_organ-regeneration.asset>)
- [procedure_organ-repair](<Assets/Resources/SO/Medical/Procedures/procedure_organ-repair.asset>)
- [procedure_prosthetic-installation](<Assets/Resources/SO/Medical/Procedures/procedure_prosthetic-installation.asset>)
- [procedure_rehabilitation](<Assets/Resources/SO/Medical/Procedures/procedure_rehabilitation.asset>)
- [procedure_rune-hibernation](<Assets/Resources/SO/Medical/Procedures/procedure_rune-hibernation.asset>)
- [procedure_slime-core-stabilization](<Assets/Resources/SO/Medical/Procedures/procedure_slime-core-stabilization.asset>)
- [procedure_slime-membrane-suture](<Assets/Resources/SO/Medical/Procedures/procedure_slime-membrane-suture.asset>)
- [procedure_slime-pseudopod-reshape](<Assets/Resources/SO/Medical/Procedures/procedure_slime-pseudopod-reshape.asset>)
- [procedure_slime-replenishment](<Assets/Resources/SO/Medical/Procedures/procedure_slime-replenishment.asset>)
- [procedure_temporal-stasis](<Assets/Resources/SO/Medical/Procedures/procedure_temporal-stasis.asset>)
- [procedure_vampire-blood-sac](<Assets/Resources/SO/Medical/Procedures/procedure_vampire-blood-sac.asset>)
- [procedure_vampire-night-eye](<Assets/Resources/SO/Medical/Procedures/procedure_vampire-night-eye.asset>)
- [procedure_whole-body-regeneration](<Assets/Resources/SO/Medical/Procedures/procedure_whole-body-regeneration.asset>)
- [procedure_xenograft](<Assets/Resources/SO/Medical/Procedures/procedure_xenograft.asset>)

</details>

<details>
<summary>SurvivalBalanceSettingsSO — 1개</summary>

- [SurvivalBalanceSettings](<Assets/Resources/SO/Survival/SurvivalBalanceSettings.asset>)

</details>

<details>
<summary>TextileMaterialDefinitionSO — 12개</summary>

- [textile_cave_silk](<Assets/Resources/SO/Apparel/Materials/textile_cave_silk.asset>)
- [textile_common_wool](<Assets/Resources/SO/Apparel/Materials/textile_common_wool.asset>)
- [textile_deep_goat_wool](<Assets/Resources/SO/Apparel/Materials/textile_deep_goat_wool.asset>)
- [textile_dreamweave](<Assets/Resources/SO/Apparel/Materials/textile_dreamweave.asset>)
- [textile_ember_cotton](<Assets/Resources/SO/Apparel/Materials/textile_ember_cotton.asset>)
- [textile_frost_linen](<Assets/Resources/SO/Apparel/Materials/textile_frost_linen.asset>)
- [textile_frost_wool](<Assets/Resources/SO/Apparel/Materials/textile_frost_wool.asset>)
- [textile_leather](<Assets/Resources/SO/Apparel/Materials/textile_leather.asset>)
- [textile_mire_canvas](<Assets/Resources/SO/Apparel/Materials/textile_mire_canvas.asset>)
- [textile_rune_leather](<Assets/Resources/SO/Apparel/Materials/textile_rune_leather.asset>)
- [textile_shade_cloth](<Assets/Resources/SO/Apparel/Materials/textile_shade_cloth.asset>)
- [textile_spore_hemp](<Assets/Resources/SO/Apparel/Materials/textile_spore_hemp.asset>)

</details>

<details>
<summary>TmpKoreanFontSettingsSO — 1개</summary>

- [TMPKoreanFontSettings](<Assets/Resources/Config/TMPKoreanFontSettings.asset>)

</details>

<details>
<summary>WasteProcessingRulesSO — 1개</summary>

- [WasteProcessingRules](<Assets/Resources/SO/Economy/WasteProcessingRules.asset>)

</details>

<details>
<summary>WeatherFrontDefinitionSO — 6개</summary>

- [WeatherFront_clear](<Assets/Resources/SO/World/Climate/WeatherFront_clear.asset>)
- [WeatherFront_cold-snap](<Assets/Resources/SO/World/Climate/WeatherFront_cold-snap.asset>)
- [WeatherFront_fog](<Assets/Resources/SO/World/Climate/WeatherFront_fog.asset>)
- [WeatherFront_heatwave](<Assets/Resources/SO/World/Climate/WeatherFront_heatwave.asset>)
- [WeatherFront_rain](<Assets/Resources/SO/World/Climate/WeatherFront_rain.asset>)
- [WeatherFront_storm](<Assets/Resources/SO/World/Climate/WeatherFront_storm.asset>)

</details>

<details>
<summary>WildlifeHabitatDecorationPaletteSO — 1개</summary>

- [WildlifeHabitatDecorationPalette](<Assets/Resources/SO/Wildlife/WildlifeHabitatDecorationPalette.asset>)

</details>

<details>
<summary>WildlifeSpeciesSO — 18개</summary>

- [wildlife_ash_crawler](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_ash_crawler.asset>)
- [wildlife_carrion_drake](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_carrion_drake.asset>)
- [wildlife_cave_hound](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_cave_hound.asset>)
- [wildlife_crystal_beetle](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_crystal_beetle.asset>)
- [wildlife_deep_goat](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_deep_goat.asset>)
- [wildlife_ember_lizard](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_ember_lizard.asset>)
- [wildlife_frost_ram](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_frost_ram.asset>)
- [wildlife_glow_moth](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_glow_moth.asset>)
- [wildlife_mana_wisp](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_mana_wisp.asset>)
- [wildlife_mire_leech](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_mire_leech.asset>)
- [wildlife_silk_spider](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_silk_spider.asset>)
- [wildlife_spore_elk](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_spore_elk.asset>)
- [wildlife_tunnel_mole](<Assets/Resources/SO/V20/Ecology/Wildlife/wildlife_tunnel_mole.asset>)
- [Wildlife_CaveRat](<Assets/Resources/SO/Wildlife/Species/Wildlife_CaveRat.asset>)
- [Wildlife_MossBoar](<Assets/Resources/SO/Wildlife/Species/Wildlife_MossBoar.asset>)
- [Wildlife_RuneDeer](<Assets/Resources/SO/Wildlife/Species/Wildlife_RuneDeer.asset>)
- [Wildlife_ShadowHare](<Assets/Resources/SO/Wildlife/Species/Wildlife_ShadowHare.asset>)
- [Wildlife_ShadowWolf](<Assets/Resources/SO/Wildlife/Species/Wildlife_ShadowWolf.asset>)

</details>

<details>
<summary>WorldInteractionPresentationCatalogSO — 1개</summary>

- [WorldInteractionPresentationCatalog](<Assets/Resources/SO/Presentation/WorldInteractionPresentationCatalog.asset>)

</details>

## 별도 분류한 Editor fixture / vendor

Assets C#에서 찾은 추가21종은 게임 콘텐츠142종에 합산하지 않았다. 기존 native 목록의 ExternalBehaviorTree도 vendor로 분리했다.

- `CharacterAiActionDescriptorDebugScenarios.TestDescriptorAction` — [CharacterAiActionDescriptorDebugScenarios.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiActionDescriptorDebugScenarios.cs)
- `CharacterAiActionScenarioMatrixDebugScenarios.MatrixDestinationAction` — [CharacterAiActionScenarioMatrixDebugScenarios.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiActionScenarioMatrixDebugScenarios.cs)
- `CharacterAiLifecycleFaultPlayModeRunner.LifecycleProbeAction` — [CharacterAiLifecycleFaultPlayModeVerifier.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiLifecycleFaultPlayModeVerifier.cs)
- `CharacterAiPriorityCornerCaseDebugScenarios.FixedScoreConsideration` — [CharacterAiPriorityCornerCaseDebugScenarios.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPriorityCornerCaseDebugScenarios.cs)
- `CharacterAiPriorityCornerCaseDebugScenarios.TestActionSet` — [CharacterAiPriorityCornerCaseDebugScenarios.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPriorityCornerCaseDebugScenarios.cs)
- `CharacterDefinitionBoundaryDebugScenarios.DescriptorProbe` — [CharacterDefinitionBoundaryDebugScenarios.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterDefinitionBoundaryDebugScenarios.cs)
- `CustomerAiDebugScenarios.FixedScoreConsideration` — [CustomerAiDebugScenarios.cs](Assets/Scripts/Services/Character/AI/Editor/CustomerAiDebugScenarios.cs)
- `CustomerAiDebugScenarios.PrimitiveFallbackProbe` — [CustomerAiDebugScenarios.cs](Assets/Scripts/Services/Character/AI/Editor/CustomerAiDebugScenarios.cs)
- `DamageNumbersPro.Internal.DNPPreset` — [DNPPreset.cs](Assets/Plugins/DamageNumbersPro/Scripts/Internal/DNPPreset.cs)
- `DebugProbeDefenseEffectSO` — [DefenseFacilityDebugScenarios.cs](Assets/Scripts/Services/Defense/Editor/DefenseFacilityDebugScenarios.cs)
- `MigratedProducerCombatDefenseEntryE2EDebugScenarios.D06ThrowingEffect` — [D06DefenseFacilityEffectOutcomeDebugScenarios.cs](Assets/Scripts/Services/Defense/Editor/D06DefenseFacilityEffectOutcomeDebugScenarios.cs)
- `ProbeContinuableWorkActionSet` — [CharacterAiPlanProbeActions.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanProbeActions.cs)
- `ProbeEatActionSet` — [CharacterAiPlanProbeActions.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanProbeActions.cs)
- `ProbeExitDungeonActionSet` — [CharacterAiPlanProbeActions.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanProbeActions.cs)
- `ProbeLookAroundActionSet` — [CharacterAiPlanProbeActions.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanProbeActions.cs)
- `ProbeOneShotWorkActionSet` — [CharacterAiPlanProbeActions.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanProbeActions.cs)
- `ProbeShoppingActionSet` — [CharacterAiPlanProbeActions.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanProbeActions.cs)
- `ProbeVolatileWorkActionSet` — [CharacterAiPlanProbeActions.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanProbeActions.cs)
- `ProbeWaitActionSet` — [CharacterAiPlanProbeActions.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanProbeActions.cs)
- `ProbeWorkActionSet` — [CharacterAiPlanProbeActions.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiPlanProbeActions.cs)
- `SurvivalHardLockProbeActionSet` — [CharacterAiNaturalnessDebugScenarios.cs](Assets/Scripts/Services/Character/AI/Editor/CharacterAiNaturalnessDebugScenarios.cs)

## 감사 재현 범위와 종료 조건

- 파일 목록142종, 고유 필드2,462개, 넓은 후보880개를 대조했다. 표식 기반 목록149개만으로 전체 조사를 대신하지 않았다. 미할당 후보0개다.
- 게시 에셋3,538개와 전체 .asset.meta4,509개를 교차 확인했다. 게시 밖의 추가 게임 SO 에셋0개, YAML 파싱 실패0개다.
- 본문 소스 링크/행과 파일 목록을 검사했다. 미연결 소비, 실제 Inspector 미실행, imported subasset fileID 검증 범위를 명시했다.
- 게임 소스·에셋·저장 데이터는 변경하지 않았다. Unity 실행·전체 seed/CI 재실행·push는 하지 않았다. 이번 결과는 **감사 완료**이며 **수정 완료/실행 검증 완료가 아니다**.
- 수정 완료 선언은 R01–R24의 처리와 위 미연결 작성 필드의 유지/제거 결정, 변경한 경계의 필요한 실제 검증 결과를 보고 따로 해야 한다.
