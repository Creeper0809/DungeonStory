# P1 6.4 대표 합성 트리 구현 보고

## 목표

시설 합성이 단발 기능으로 끝나지 않게, 첫 프로토타입에서 확인할 수 있는 대표 합성 트리를 만든다.

이번 단계는 모든 시설을 5성까지 만드는 것이 아니라, 식당/함정/경비 계열이 최소 1성에서 3성까지 이어지는지 검증하는 데 집중한다.

## 구현 파일

- `Assets/Scripts/Synthesis/Editor/P1FacilitySynthesisAssetBuilder.cs`
- `Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs`
- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`

## 추가된 3성 시설

```text
3성 전장의 식당
- 기반: 전투 식당 + 병영
- 성격: 오크/전투형 식당
- 구현: Shop 기반 경영 시설

3성 귀족의 식당
- 기반: 고급 고기 식당 + 마력 저장소
- 성격: 뱀파이어/마력/체류형 식당
- 구현: Shop 기반 경영 시설

3성 부식 냉각 함정
- 기반: 맹독 가시 함정 + 냉기 분사구
- 성격: 부식 + 감속 방어 시설
- 구현: DefenseFacility 기반 방어 시설

3성 폭뢰 분사구
- 기반: 경보 코일 + 화염 분사구
- 성격: 화염 + 축전 + 경비 연계 특수 방어 시설
- 구현: DefenseFacility 기반 방어 시설

3성 전투 병영
- 기반: 병영 + 무기점
- 성격: 경비 대응 강화 시설
- 구현: DefenseFacility 기반 경비 시설
```

## 추가된 조합식

공개 조합식:

```text
전투 식당 + 병영 -> 전장의 식당
고급 고기 식당 + 마력 저장소 -> 귀족의 식당
맹독 가시 함정 + 냉기 분사구 -> 부식 냉각 함정
병영 + 무기점 -> 전투 병영
```

특수 조합식:

```text
경보 코일 + 화염 분사구 -> 폭뢰 분사구
```

`폭뢰 분사구`는 `recipe_trap_chain_3` 연구가 완료되어야 조합식이 보인다.

## 구현 로직

대표 트리는 기존 6.3 합성 런타임을 그대로 사용한다.

1. `P1FacilitySynthesisAssetBuilder`가 3성 결과 시설 `BuildingSO`와 `FacilitySynthesisRecipeSO`를 생성한다.
2. 공개 조합식은 `publicByDefault = true`로 둔다.
3. 특수 조합식은 `publicByDefault = false`와 `requiredResearchRecipeId`를 사용한다.
4. 합성 실행은 기존처럼 배치된 시설을 재료로 선택한다.
5. 성공 시 재료 시설은 제거되고, 첫 번째 재료 위치에 결과 시설이 생성된다.
6. 결과 시설 레벨은 재료 평균 레벨과 조합식 계승 비율로 계산된다.

이번 단계에서 하이브리드 식당은 일단 `Shop` 기반으로 둔다. 현재 방어 발동 런타임은 `DefenseFacility` 컴포넌트 중심이므로, 식당의 방어 시너지는 추후 별도 시너지 시스템이 생긴 뒤 연결하는 것이 안전하다.

## 검증

`FacilitySynthesisDebugScenarios`에 다음 검증을 추가했다.

- 대표 트리 3성 시설과 조합식 에셋 존재 여부
- 식당 계열 3성 두 갈래 합성
- 함정 계열 3성 합성
- 경비/훈련 계열 3성 합성
- 3성 특수 조합식의 연구 해금 전/후 가시성

`FacilityDebugScenarios`의 P1 시설 수 검증도 3성 식당 2개와 신규 stock 정보를 반영해 갱신했다.

## 다음 연결 지점

다음 단계인 도감 시스템에서는 이 조합식 데이터를 읽어 시설 도감에 표시하면 된다.

도감에서 보여줄 수 있는 정보:

- 공개 조합식
- 연구로 해금된 특수 조합식
- 미해금 특수 조합식의 힌트
- 결과 시설의 역할, 공격 컨셉, 별 등급
