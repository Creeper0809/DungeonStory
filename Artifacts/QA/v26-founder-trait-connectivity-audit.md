# V26 창립자 특성 연결성 전수 감사

감사 기준일: 2026-08-11  
최종 판정: **PASS — 541/541 연결 행, 고아 0건**

이 판정은 정의나 투영 코드가 존재한다는 뜻만이 아니다. 창립자 특성의 공용 효과와 정체성 규칙이 실제 생산자, 권위, 조건, 소비자, 저장 또는 재계산, UI·AI 관찰, 결정론적 검증까지 연결되었는지를 검사한 결과다.

## 자동 감사 범위

| 범주 | 결과 |
|---|---:|
| 전체 source-to-consumer 행 | 541 / 541 |
| 공용 효과 target | 45 / 45 |
| 공용 효과 condition | 45 / 45 |
| 정체성 사건·행동 | 63 / 63 |
| AI 행동 의미 태그 | 38 / 38 |
| 지속 욕구 | 9 / 9 |
| 극한형 특성 | 7 / 7 |
| 효과·정체성 public API | 104 / 104 |
| private/internal/protected helper | 77 / 77 |
| 정체성 및 런타임 직렬화 필드 | 126 / 126 |
| 고아 endpoint | 0 |

public API, 비공개 helper와 직렬화 필드 수는 고정 목록이 아니다. 감사기가 효과 런타임, 정체성 런타임, 핵심 정의 파일을 매 실행마다 리플렉션과 소스 역참조로 다시 열거한다. 새 함수나 필드를 추가하면 총 행 수가 자동으로 증가하며, 의도 속성이나 소비 경로가 없으면 실패한다.

Unity MCP 실행 결과:

```text
V26_TRAIT_CONNECTIVITY_MANIFEST=PASS; rows=541; targets=45; conditions=45; identity=63; behaviors=38; needs=9; extremes=7; publicApis=104; helperMethods=77; serializedFields=126; orphans=0
PHASE147_AUDIT=PASS founder=100 rerolls=100000 avg=2.3965 negative=27.920% extreme=0.962% multiExtreme=0.0000% mythic=3.008% normalMythic=0
```

행별 증거는 `Artifacts/QA/v26-founder-trait-source-consumer-manifest.md`에 있다.

## 이번 심층 감사에서 새로 발견한 실제 문제

| 문제 | 원인 | 수정 |
|---|---|---|
| 극한형 수치의 이중 권위 | 정체성 규칙과 `GameplayEffectBinding` 양쪽에 전투·이동·작업·사고·피로·회복 배율이 직렬화되어 있었다. | 정체성 규칙에서는 발동 조건·확률·선택 비용·상태만 유지했다. 공용 수치는 binding 하나만 권위로 남겼다. |
| 사선 각성 전투력 이중 적용 | 전투 스냅샷이 규칙 배율을 직접 곱하고 실제 전투 명령이 공용 `combat-power`를 다시 곱했다. | 전투력은 공용 효과를 한 번만 적용한다. 사선 각성 규칙은 임계 체력·통증 페널티 무효화와 상태 전이만 담당한다. |
| 신화 확률·기여율 authored 값 무시 | 특성 300 SO에 값이 있어도 실제 제작 판정은 별도 상수를 사용했다. | 무기·방어구·의복 제작기가 선택된 `ExtremeCraftInspirationRule`의 `mythicChance`와 `minimumContributionShare`를 직접 읽는다. 0%/100% 경계 검증을 추가했다. |
| 죽은 공용 효과 parameter | `GameplayEffectBinding.parameters`가 직렬화만 되고 투영기·조건기 어디에서도 소비되지 않았다. | 확장점을 가장한 죽은 필드와 타입을 제거하고 에셋을 다시 빌드했다. |
| 극한형 결과 DTO의 미사용 값 | 마력 과충전 결과가 위력·지속시간을 반환했지만 호출자는 비용만 소비했다. 황금 수확도 규칙과 binding에서 산출 배율을 중복 소유했다. | 마력 과충전 DTO는 실제 명령 비용만 반환한다. 위력과 수확 보너스는 공용 효과가 소유하고, 규칙은 판정·손실·상태만 소유한다. |
| 감사 범위 누락 | 이전 감사가 27개 명령을 수동 목록으로 관리해 `Start`, `Tick`, `Dispose`, 상태 `Set/Restore/Remove`, lease 갱신·만료와 비공개 helper를 놓쳤다. | public API 104개와 private/internal/protected helper 77개를 동적으로 열거한다. 상태 변경 함수는 정확히 하나의 의도 속성과 허용 호출 증거를 요구하고, 비공개 함수는 선언 외 사용 증거를 요구한다. |
| 직렬화 고아를 놓치는 구조 | 기존 검증은 effect target과 대표 명령만 확인해 사용되지 않는 public 필드를 잡지 못했다. | 정체성 규칙 필드 59개와 런타임·정의 필드 67개를 자동 열거한다. 실제 소비자가 없으면 제거하거나 사유·제거 조건이 있는 migration-only 계약을 요구한다. |
| 구형 기분 반응 필드의 모호한 생존 | `moodReactions`는 신규 정체성 정책의 권위가 아니지만 직렬화 필드로 남아 있었다. | 신규 작성 금지인 마이그레이션 전용 필드로 명시하고 제거 조건을 기록했다. 실제 기분은 typed identity event와 `CharacterMoodPolicyService`만 사용한다. |

## 재발 방지 규칙

- 수치 효과는 특성 ID별 분기나 정체성 규칙 내부 상수가 아니라 안정 ID의 `GameplayEffectBinding` 하나로 투영한다.
- 정체성 규칙은 발동 조건, 확률, 플레이어 선택 비용, 기억·쿨다운·연속 상태만 소유한다.
- 같은 확률·최소 기여율·지연을 SO와 `const`에 복제하지 않는다. 도메인 판정은 실제 선택 특성의 authored 규칙 값을 읽는다.
- public 상태 변경 API는 의도 속성이 없거나 허용된 비 Editor 호출자가 없으면 감사 실패다. 비공개 helper는 선언 외 호출·delegate 구독·내부 교차 파일 참조가 없으면 실패다.
- 직렬화 필드는 정의·빌더·validator 외부의 실제 소비자가 없으면 감사 실패다. 마이그레이션 필드는 사유와 제거 조건을 모두 요구한다.
- Editor 시나리오의 직접 서비스 호출은 계산 검증일 뿐 실제 실행 경로 증거로 세지 않는다.
- 신규 함수와 필드는 감사 목록을 수동으로 갱신하지 않아도 자동 발견되어야 한다.

이 규칙은 루트 `AGENT.md`의 `게임플레이 연결 완결성 강제 게이트`, `함수 단위 전수 조사 프로토콜`, `고아 재발 방지 자동 게이트`에 반영했다.

## 회귀 증거

| 감사 | 결과 |
|---|---|
| 창립자 연결 manifest | PASS, 541/541, orphan 0 |
| 100종 리롤 | PASS, 100,000회, 평균 2.3965개 |
| 신화 판정 | PASS, 적격 3.0083%, 일반 품질 신화 0건 |
| authored 신화 확률 경계 | PASS, 0%에서 0건, 100%에서 전건 |
| 공유 효과 중첩·중복 억제 | PASS |
| 정체성 상태 저장 왕복·중복 거절 | PASS |
| 공식 전체 월드 PlayMode | PASS, 68/68/68, 기준선 복원, Console Warning/Error 0/0 |

보고서:

- `Artifacts/QA/v26-founder-trait-source-consumer-manifest.md`
- `Artifacts/QA/v26-founder-trait-mythic-audit.txt`
- `Artifacts/QA/v26-founder-industry-bottom-up.md`
- `Artifacts/QA/v26-equipment-readiness-throughput.md`
- `Artifacts/QA/full-world-round-trip-playmode-report.txt`

현재 판정은 **특성·공용 효과·정체성 규칙 연결 완료 및 공식 검증 통과**다. 이것은 전체 게임 밸런스 실전 보정 완료를 뜻하지 않는다. 자연 파티 p10/중앙/p90 일과, 실제 이동·식사·수면, 사고 시점별 손실 WU와 장기 숙련 성장은 다음 밸런스 단계에서 계속 계산한다.
