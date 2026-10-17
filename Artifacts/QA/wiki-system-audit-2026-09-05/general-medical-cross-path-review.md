# 의료 두 경로·약품 22종·시설 10종 교차 감사

기준 main `c03d2e0a`. 정적 C#/자산/위키 대조이며 Unity 플레이 재현·저장왕복·UI 입력 결과가 아니다. 게임·에셋·공개 위키 변경 없음, 밸런스 영향 없음.

## 기존 원장의 교정 범위

GAP-019/020/023의 질병·현장 대응·백신은 재집계하지 않는다. GAP-122/123의 구조 치료 조사에 다음 근거를 보강한다.

- GAP-123과 `general-medical-order-review.md`의 6종 표는 전체 후보가 아니다. 모든 Resources의 MedicineItemFeature 22개를 조사하면 지원 플래그 true는13개지만 실제 Resource 타입 필터를 통과하는 것은7개다. 빠졌던 `medicine:mycelial-culture-pack`은 ResourceItemDefinitionSO이며 root item catalog에 연결된다. 나머지6개 Generic 타입은 일반 구조 치료 목록에서 제외된다.
- `workSeconds`는 구조 치료 WU 공식에서는 쓰지 않지만 생활시설 치료 업무의 필요 WU로 소비된다. 전체 게임에서 미사용 필드로 판정하면 틀리다.
- 쓰러지지 않은 환자를 거부하는 것은 CharacterMedicalRuntime의 구조 치료 주문이다. 별도 생활 건강 상태 치료와 질병 대응·수술까지 불가능하다고 일반화하지 않는다.
- 구조자가 환자까지 이동→현장 안정화→운반→치료를 수행하는 실제 AI 호출을 확인했다. 병상에서 별도 의료진에게 자동 인계하는 구조로 설명하지 않는다.

## 두 치료 경로의 차이

| 항목 | 구조 치료 | 생활시설 치료 |
| --- | --- | --- |
| 실행 | AIRescue→AbilityRescue→CharacterMedicalRuntime | SurvivalWorkExecutionHandler→SurvivalBuildingAbilityHandler→SurvivalFoodRuntime |
| 대상 | Downed 환자별 명시적 주문 | SurvivalHealthSaveData 활성 상태 중 하나 |
| 환자 선택 | 치료 우선순위·미안정 우선·거리 | 직접 서비스는 첫 활성 상태, 그 외 Infected 우선→중증도 |
| 위치 | 접근·현장 안정화·환자 운반·시설 도착 확인 | 선택 함수에 환자 거리/시설 점유 조건 없음; 서비스 허브는 별도 세션 검증 |
| 필요 WU | 안정화 min(30,8+총출혈×40), 치료20+손실부위HP×0.8+혈액손실×0.4 | 시설 workSeconds, 하한0.1 WU |
| 약품 선택 | Resource 타입·Medicine kind·SupportsInjuryTreatment 필터와 목표 potency 거리 | 창고 Medicine 카테고리, item ID→stack ID 순. 같은 feature 필터 없음 |
| 약품 소비 | 시설 목적지 exact item1개, 첫 치료WU 전 | 작업 완료 시 창고의 사용가능한 카테고리 실물1개 |
| 대체 | 특정 extracted-blood1개 | Biological 카테고리1개 |
| 치유 | 총 HP 예산=시설severityReduction×40×약효×시술자 치료효율 | severity-=시설severityReduction×효율, 남은 시간-=180×효율, aggregate HP+=16×효율 |
| 혈액손실 | 보통25 감소 / 추출혈14 감소 | 이 함수는 body bloodLoss를 직접 변경하지 않음 |
| 부위 | 낮은 체력비율의 legacy parts부터 총 예산 분배→표면 노드 동기화 | 대상 부위 선택 없음 |
| 종료 | 다운 해제, 계속 다운이면 재산정해 반복 | severity≤0.05 또는 남은 시간≤0이면 상태 제거; 아니면 Recovering |

생활시설의 약품 효율은 일반1 / 혈액 대체0.55로 고정이며 개별 약품 potency/감염/통증/해독 수치를 여기서 읽지 않는다. 대체 시 추가 Exposed0.25/240초·기분-4/240초를 준다. 창고 카테고리 소비는 물리 Sink 경로가 있으므로 추상 아이템 삭제라고 부르지는 않는다. 다만 실제 환자 위치와 다른 시설에서의 치료 가능성, 서비스 세션의 위치 제한, 포장재 처리는 추가 실행 검증 대상이다.

구조 치료의 안정화는 실제 bleedingPerSecond를0으로 만든 뒤 감염 부담8을 부여한다. 이 효과와 수술 긴급봉합의 치유-only 효과(DIFF-022)는 다르다. 일반 ApplyTreatment의 표면노드 동기화는 모든 내부장기 치료를 뜻하지 않는다.

## 약품 작성값과 실제 선택 경계

모든22개 공개 도감 facts는 무게·한 칸 적재·기준 가격만 제공한다. 아래는 **작성값**이며 전부 실행되는 효능표가 아니다.

| ID | SO 종류 | 부상지원 | potency | 감염감소 | 해독 | 통증 | 구조치료 타입/기능 필터 |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- |
| medicine:antiseptic | Resource | 1 | 0.82 | 16 | 0 | 2 | 대상 |
| medicine:antidote | Resource | 0 | 0.6 | 2 | 30 | 0 | 제외 |
| medicine:anesthetic | Resource | 0 | 0.75 | 0 | 0 | 35 | 제외 |
| medicine:advanced | Resource | 1 | 1.35 | 14 | 8 | 12 | 대상 |
| medicine:standard | Resource | 1 | 1 | 8 | 2 | 8 | 대상 |
| medicine:herbal-poultice | Resource | 1 | 0.72 | 2 | 0 | 4 | 대상 |
| medical:regenerative-medium | Resource | 1 | 0.7 | 6 | 0 | 4 | 대상 |
| medical:sterile-bandage | Resource | 1 | 0.85 | 10 | 0 | 8 | 대상 |
| medicine:vaccine:red-fever | Resource | 0 | 0.1 | 0 | 0 | 0 | 제외 |
| medicine:vaccine:cave-flu | Resource | 0 | 0.1 | 0 | 0 | 0 | 제외 |
| medicine:vaccine:slime-blight | Resource | 0 | 0.1 | 0 | 0 | 0 | 제외 |
| medicine:vaccine:blood-wasting | Resource | 0 | 0.1 | 0 | 0 | 0 | 제외 |
| medicine:vaccine:mana-pox | Resource | 0 | 0.1 | 0 | 0 | 0 | 제외 |
| medicine:vaccine:spore-lung | Resource | 0 | 0.1 | 0 | 0 | 0 | 제외 |
| medicine:vaccine:gut-rot | Resource | 0 | 0.1 | 0 | 0 | 0 | 제외 |
| medicine:field-emergency-kit | Generic | 1 | 1 | 0 | 0 | 0 | 제외 |
| medicine:blood-seal-kit | Generic | 1 | 1 | 0 | 0 | 0 | 제외 |
| medicine:disinfectant | Generic | 1 | 1 | 0 | 0 | 0 | 제외 |
| medicine:immunosuppressant | Generic | 1 | 1 | 0 | 0 | 0 | 제외 |
| medicine:rune-slime-patch | Generic | 1 | 1 | 0 | 0 | 0 | 제외 |
| medicine:mycelial-culture-pack | Resource | 1 | 1 | 0 | 0 | 0 | 대상 |
| medicine:wing-splint-kit | Generic | 1 | 1 | 0 | 0 | 0 | 제외 |

타입은 m_Script GUID와 소스 정의로 대조했다. 모든22개는 Production kind3/StockCategory5지만 Generic6개는 ResourceEconomyContentCatalog의 OfType<ResourceItemDefinitionSO>에서 제외된다. 카탈로그 연결/후보 필터를 통과한다는 사실이 현재 재고·시설·연구 조건까지 충족했다는 의미는 아니다.

해독 authored 양수3종은 antidote30, advanced8, standard2다. production 역검색의 실제 읽기는 아이템 정보 UI뿐이고 나머지는 작성·검증·digest다. 질병의 antiparasitic 대응이 해독제1개를 쓰는 것은 확인되지만 고정 중증도26 감소이며 authored detox30을 쓰지 않는다. 따라서 해독제의 “독소와 과다 복용 증상 완화”를 현재 detox 필드로 증명할 수 없다. 다른 독성/중독 소비 전체는 후속 확인 대상으로 유지한다.

마취제·백신은 부상 지원false라 구조 약품에서 빠지는 것이 정상이다. Generic 키트는 수술/원정의 별도 소비처가 있을 수 있으며 여기서 기능 전체 미구현으로 분류하지 않는다.

## 의료 시설 전수 선택 필드

BuildingMedicalAbility를 가진10개 자산의 workSeconds/severityReduction/requiresMedicine30값과 공개10개를 대조했다. facts는 분류·크기뿐이다. 값은 `general-medical-authoring-review.json`에 전수 보존했다.

- 목욕통(building:1060):1.1 WU/0.28/무약품.
- 간이·정식·이층침대:1.4/0.22,1.2/0.38,1.3/0.30, 모두 약품 필요.
- 격리회복침상:1.8/0.65, 약품 필요.
- 응급처치대·외과수술대·재활보조대·순환이식대·비전개조대:각1.8/0.40, 약품 필요.
- 의료 ability 없는 Rest/주수술시설까지 이10개로 덮었다고 주장하지 않는다. 그 시설별 실제 후보 및 고장·예약 동작은 별도다.

## 추가 차이 목록과 교차 연결

- DIFF-026: 위키 치료 흐름의 이송→안정화 순서가 실제 현장 안정화→이송과 반대. GAP-122와 같은 현상을 다른 방향에서 참조하므로 합산 금지.
- DIFF-027: 위키의 단일 치료 설명이 구조/생활건강의 대상·WU·약품 선택·효과 차이를 숨김. 보행 환자 주문 거절은 구조 경로 한정. GAP-122/123 확장 근거다.
- DIFF-028: 해독제의 효과 설명 및 UI 해독 수치와 실제 authored detox 소비 사이 연결 미확인. antiparasitic 고정 효과와 구분한다.

## 근거·검증 한계

KB query `MedicalFieldResponseRuntime Vaccination quarantine`, areas code/content, limit8: stale2429, 반환0. content digest `76cce09a76ded556dc74ac400c571e92136f17f86fc3697dfb9eb30dca669871`, system digest `74d85480f35dc5704306579326fa8f38e3bb5841f0dbaab8e697f4be25522a06`. 인덱스를 재생성하지 않았다.

직접 근거는 `general-medical-cross-path-evidence.json` 및 authoring JSON의 파일 해시/경로에 기록한다. 해시는 조사시점 고정용이며 완독이나 실행증거가 아니다. 작성140값 전사, 공개32개 facts 비교, 구조/생활 두 경로의 관련 함수 정적 대조다. 전역 도감·수치·관계 전수 감사는 아직 미완료다.
