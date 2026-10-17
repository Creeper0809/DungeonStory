# ScriptableObject 모듈 확장 조사·수정

기준: 2026-10-17, `codex/so-modular-extension`, 병합된 `e2ace8bc4016aad8518156ed402e35555a4abc98` 이후 작업.

**상태: 2026-10-18 최종 소스 컴파일·필요한 표적 실행·실제 Play 검증 완료.** 기존 메모리 중단과 시험 준비 실패 기록을 보존하고, 현재 source-pin-r6의 실행 5회만 최종 완료 근거로 사용한다.

## 조사 범위와 판정

원본 C# 4,001개에서 상속을 추적해 first-party SO/직렬화 SO 161개 타입, 원본 파일 147개를 대조했다. 추상 기반 타입과 Editor 시험용 타입도 목록에 포함한다. `.asset` 4,509개를 조사했으며, 124개 작성 타입에 걸쳐 first-party asset/script 연결 3,538쌍을 확인했다. 분류되지 않은 first-party 스크립트 참조는 없다. 연결 쌍 수는 SO 하위 객체 개수와 다르다.

정의 발견 → 실제 조립/등록 → 상태 소유자/명령 → 소비자 → 관련 저장·복원을 확인했다. 기존 기능의 콘텐츠 변형을 거절하거나 무시하는 경계를 수정 대상으로 삼았다. 클래스 크기, enum 사용, 새로운 게임 규칙의 명시적 구현·등록은 그 자체로 결함이 아니다.

[161개 타입별 최종 판정](../../Artifacts/QA/SoModularity-20261017/audit-final.json), [상속 인벤토리](../../Artifacts/QA/SoModularity-20261017/so-inventory.json), [작성 에셋 대조](../../Artifacts/QA/SoModularity-20261017/so-authored-assets.json). 세부 조사는 같은 폴더의 `audit-main.json`, `audit-actors.json`, `audit-buildings.json`, `audit-combat-medical.json`이며, 후보에 대한 최종 유지/제외 판정은 `audit-final.json`이 우선한다.

## 필요한 수정

| 경계 | 확인한 장벽 | 수정 |
|---|---|---|
| 카탈로그 발견 | 아이템 하위 타입 조회 누락, 이야기·캐릭터·축제·적·질병·기후·작물의 전체 개수 고정 | 실제 발견된 정의를 로드한다. 비어 있음·중복 ID·잘못된 정의·참조 누락·기후 가중치·작물별 기본 유전자 조건은 유지한다. 초기 씨앗 지급도 실제 작물 집합을 사용한다. |
| 관측 사건 | 같은 종류의 정의가 둘이면 생성이 막힘 | 기존 적격성 검사와 결정적 가중 선택을 재사용한다. 저장된 사건 ID와 동일 작업 재시도는 기존 영수증을 사용한다. |
| 건물·기후 | 등록된 건물 runtime factory가 DI 조립에서 누락, 관측 시설이 ID 8851에 종속 | 기존 factory 등록이 실제 dispatcher까지 도달한다. 관측 시설은 기존 weather-observation workstation capability로 선택한다. |
| 시설 기록 토큰 | 정의 provider가 생산자와 단절되고 생산 토큰 ID가 고정 | 기존 이벤트 trigger와 지원하는 metric threshold를 정의로 선택한다. 기존 9개 에셋의 조건·지급량은 보존한다. 모르는 metric·지원하지 않는 threshold decay는 거절한다. |
| 캐릭터·AI | 소유자의 추가 action 조합이 초기화 때 제거, 종족 능력을 한 개로 자름, 특성 능력 소비 누락 | 기존 action 조합과 wrapper/clock을 보존한다. 작성한 종족 능력을 모두 전달하고 특성 능력을 기존 레벨 계약에 맞춰 전투 projection에 전달한다. |
| 실제 생성 프로필 | 유효 프로필 재계산이 선택된 phenotype/trait/innate/latent 입력을 archetype 기본값으로 대체 | 원래 생성 프로필과 선택된 특성을 사용하고 원본 프로필 교체 시 캐시를 갱신한다. ID만 있는 DI 준비 단계의 조회·vitals 주입은 기본 신체 상태를 만들지 않는다. |
| 장비 역할 | Powered·ArmorBreaker·SpellBlock 등 역할과 마나 비용이 콘텐츠 ID에 고정 | SO 메타데이터를 snapshot·명령·전투·전력·UI·PPE 판정에 전달한다. 기존 13개 에셋의 역할/비용은 동일하다. 과거 영수증은 현재 콘텐츠로 재분류하지 않는다. |
| 해부학·의복 | 종족 ID로 신체를 재선택하고 크기·착용점을 하드코딩, 기존 부위 매핑을 피해 계산이 무시 | 최초 신체는 실제 profile의 anatomy ID를 사용하고 복원 후에는 저장된 ID를 유지한다. 12개 프로필의 기존 착용 결과를 메타데이터로 보존한다. 기존 `MapsToLegacyBodyPart`를 실제 피해·회복·투영 계산에 연결하며 중복/잘못된 매핑을 거절한다. |

생산 코드를 새 helper/partial/manager 파일로 분할하지 않았다. 기존 파일과 등록 경계를 수정했고, 별도 신규 스크립트는 확장 조합을 실제 소유자에 통과시키는 Editor 검증 파일 하나다. GUID·씬·프리팹·저장 버전 변경이나 사용자 수동 Inspector 작업은 없다.

에셋 변경은 시설 토큰 9개, 장비 13개, 해부학 프로필 12개의 메타데이터다. 기반 에셋 재생성기에도 같은 값을 반영했다. Medical → Economy 참조는 기존 의복 타입을 사용하기 위한 것이며 반대 경로와 assembly 순환은 없다.

## 유지한 경계와 남은 기능 공백

- 기존 singleton 설정, 계절 가중치 합, 작물의 여섯 유전자 좌위, 개별 faction arc의 장 수와 같은 정의 스키마는 유지했다.
- 기존 capability/strategy와 장비 수식 applicator는 재사용한다. 새 계산 종류나 새 게임 규칙을 추가할 때 필요한 등록을 자동화하지 않았다.
- `ACTOR-003`의 미소비 유전 효과 네 계열은 실행 의미가 정의되지 않은 기능 공백이다. 임의의 능력·환경·신체·마나 효과를 새로 만들지 않았으며 구현 완료로 세지 않는다.
- 일부 종족 사건의 효과 handler와 설명용 태그 역시 새 규칙의 의미가 필요하다. 의도된 lore-only 경로와 작성 메타데이터를 무조건 실행 기능으로 바꾸지 않았다.
- Slime/Quadruped의 공유 부위 등 매핑되지 않은 기존 호환 경로는 유지한다. Avian/Kobold의 기존 팔 별칭이 실제 노드를 가리키지 않는 동작도 이번 메타데이터 연결에서 바꾸지 않았다. 새 프로필은 이미 있는 명시적 부위 매핑으로 같은 피해 기능을 연결할 수 있다.
- 정의가 추가된 건물 상태 모듈을 과거 저장에 소급 적용하려면 기존 저장 계약에 따른 migration이 필요하다. 알 수 없는 모듈을 무시하거나 누락 상태를 가짜 기본값으로 채우지 않는다.

## 최종 검증 결과 — 2026-10-18

[최종 검증 영수증](../../Artifacts/QA/SoModularity-20261017/verification-final.json)이 실행 완료 권위다. [source-pin-r6.json](../../Artifacts/QA/SoModularity-20261017/source-pin-r6.json)의 입력 4,081개와 실제 DLL 47개의 지문이 5회 실행 전후 동일하다. 소스·에셋·asmdef 추가 수정 없이 검증했다.

| 검증 | 실제 결과 |
|---|---|
| 현재 소스 컴파일 | native-r4에서 36.238804초, CS 오류0. 새 어셈블리 reload/import 이후 실제 검사를 실행했다. |
| 새 SO 조합·건물 회귀 | native-r4 PASS/exit0. 추가 카탈로그·적/사건·기후·13번째 작물/17번째 질병·토큰·phenotype/특성/해부학·AI action·관측 시설·장비 역할의 실제 생산/소비, JSON/fresh owner/retry와 기존 건물·생산 회귀를 포함한다. 새 SO 검사 중 consoleErrors0. |
| 캐릭터 정의 회귀 | native-character-r1 PASS/exit0. 작성 정의 factory/editor/JSON·detached clone·personality·action descriptor·character model·work priority 6개 경계 모두 PASS. |
| 사건/영수증 회귀 | native-campaign-r1 PASS/exit0. 현재 root·publication fence·관측 사건 거절/재시도·계절 terminal·life/guest 영수증·물리 child 복원/ACK 전후/부분 재생을 포함한다. |
| 기존 전투/장비 회귀 | native-combat-r1의 기존 V14 전체 검사 PASS/exit0. `CombatSystemDebugScenarios.RunFromMenu`의 성공 marker와 실패/예외 없음까지 확인했다. |
| 실제 GameplayScene Play | native-play-r1 PASS/exit0. 실제 컨테이너의 모듈149·binding153·fact153, 선언/실제 연결 집합의 일치와 module smoke를 확인했다. [Play 결과](../../Artifacts/QA/SoModularity-20261017/native-play-r1/observer-composition-current.json). |
| 포커스 비간섭·메모리 보호 | 5회 모두 비대화형 OS station/desktop·격리 창 probe PASS. 사용자 desktop 폴백 없음. 시작 여유 RAM 약10.75GiB, 메모리 보호 완화/중단 없음. 순수 startup guard188 PASS는 동일 런처의 별도 기존 근거다. |
| 정적 검사와 인덱스 | C#57개 syntax오류0·diff check·보고서 링크 PASS. 기존 에셋 값/assembly 의존 검토 완료. `knowledge-base-close-r2.log`의 query5·content/knowledge/freshness 오류0은 소스 변경이 없어 재사용했고, 재개 시 좁은 지식베이스 조회도 fresh를 확인했다. |

로그는 [실행 증거 폴더](../../Artifacts/QA/SoModularity-20261017/)에 보존했다. native-r1의 namespace 누락, native-r2의 전염 불가 질병 fixture, native-r3의 메모리 보호 중단은 과거 실패이며 최종 PASS로 바꾸지 않았다. 이번 검증에서는 추가 코드 수정이 필요하지 않았다.

현재 SOURCE_EDIT/UNITY_VERIFY 모두 종료, 모든 native terminal/Unity0이다. 이번 SO 수정의 필수 실행 대기는 없다. 관련 소스/계약 변화나 실제 실패 없이 전체 컴파일·Play를 반복하지 않는다.

새 정의의 추가 조합 검사는 실제 Unity EditMode에서 통제된 정의 clone과 실제 runtime/생산자/소비자·JSON/fresh owner로 실행했고, 실제 GameplayScene 컨테이너는 별도 Play로 확인했다. 자연 UI/AI 전수 조합·장기 밸런스·임시 추가 정의를 포함한 공식 전체 월드 저장이나 모든 과거 저장의 migration을 입증하는 작업으로 확대하지 않는다.
