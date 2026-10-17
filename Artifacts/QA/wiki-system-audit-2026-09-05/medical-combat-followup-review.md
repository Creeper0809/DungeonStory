# 의료·수술 및 전투·장비 후속 대조

기준 main `c03d2e0a`. 게임 코드·에셋·위키 수정 없음. 밸런스 영향 없음: 현재 설명과 구현을 읽어 목록화만 했다. Unity 컴파일·플레이·저장 왕복은 실행하지 않았다.

## 조사 범위와 결론

- 수술 작성 자산47개와 공개 도감47개: ID 조인47/47, 필요작업47값과 효과개수47값 일치. 47개 전부 실제 UI로 시행했다는 뜻이 아니다.
- 재료12개×5배율=60값: combat-and-equipment 가이드와 모두 일치.
- 전투 명중·회피·방패·방어·부위확률·출혈·준비점수의 선택된 공식은 현재 구현과 일치한다. 전투 전체/73개 장비 전수 성능 검증으로 확장해 보고하지 않는다.
- 수혈·긴급봉합의 이름/설명과 실제 효과, 종족별 특수 절차의 일반 치유 효과, 착용/운반 중량 계산 차이가 확인됐다.
- 수술 위험·조건의 상세 설명 부재는 게임플레이 미구현과 구분한다. 운영 목표(인구당 병상 수·회복 목표일)도 엔진이 강제하는 불변식이 아니다.

## 확인된 차이

### DIFF-020 장비의 착용 부담과 물리 운반 무게가 다른 계산을 사용

위키 combat-and-equipment 173~176의 `장비 기본 무게×소재 무게 배율×진화 무게 배율`은 실제 `CombatEquipmentStatProjector.Build`에 있다. `CombatEquipmentLoadoutRuntime.GetCarriedWeight`는 이 값을 합산하고 `CharacterStatsProjectionService.GetEquipmentBurdenMultiplier`가 작업·이동 배율에 사용한다.

그러나 `PhysicalItemMassSubjectAdapter.Create`는 정의 gram에 장착 모듈과 잔여 장전탄약 gram을 더하며 소재/진화 무게 배율을 사용하지 않는다. `CharacterCarryInventory.GetCurrentWeight`와 창고 물리 질량 query는 이 물리 subject를 사용한다. 반대로 착용부담 계산은 모듈/탄약 gram 합산을 하지 않는다. UI CharacterSummaryCombatPresenter497은 carry현재무게와 equipment무게를 합쳐 표시한다.

따라서 위키의 소재배율 표 자체가 틀린 것은 아니다. 동일 장비의 운반/보관 중량과 착용 부담은 현재 별도 계산이라는 설명이 없으며, V27 단일 중량 의도와의 구현 불일치 후보다. 합산 UI가 언제 이중계상하는지까지는 미재현이므로 단정하지 않는다.

### DIFF-021 수혈이 혈액 손실을 직접 회복하지 않음

위키 `medical/procedure-blood-transfusion.json`: 혈액 제제로 급격한 혈액 손실을 완화한다고 설명한다.

작성 `procedure_blood-transfusion.asset`: 10WU, blood-pack1, 효과는 HealSurgicalNodeEffect(health14, infectionReduction2) 한 개다. `HealSurgicalNodeEffectHandler`→`CharacterBodyHealthRuntime.TryHealNode(977)`는 부위 체력과 감염을 바꾸지만 bloodLoss와 bleedingPerSecond는 변경하지 않는다. 성공 공통처리에도 수혈 전용 혈액 회복이 없다.

자연 회복이나 별도 의료 주문에서 혈액 손실이 낮아질 수 있다는 것과 수혈 효과가 혈액을 회복한다는 것은 다르다.

### DIFF-022 긴급 봉합의 치유 효과가 출혈을 닫지 않음

위키 `medical/procedure-emergency-suture.json`은 열린 상처를 닫고 출혈·감염 위험을 낮춘다고 한다. 작성 효과는 health8/infectionReduction8의 HealSurgicalNodeEffect뿐이다. TryHealNode에는 출혈률 감소가 없다.

수술 성공의 `order.incisionOpen=false`는 주문의 절개 상태이며 신체 node.bleedingPerSecond와 다른 필드다. SurgeryLogisticsRuntime.PrepareAdmission은 쓰러진 환자에게 별도 일반 치료를 요청할 수 있지만, 이것을 봉합 자체의 지혈 효과로 세지 않는다.

### DIFF-023 종족별 24절차가 일반 치유 효과로 작성됨

24개의 종족 제한 절차는 모두 하나의 HealSurgicalNodeEffect를 사용하고 아래 체력/감염 수치만 다르다. 위키의 명칭에는 이식·접목·보철·보강이 포함되나 이 경로는 InstallSurgicalPartEffect를 호출하지 않고 부품 입력도 요구하지 않는다. 일반 이식/보철 설치 절차 자체는 별도로 존재한다.

특히 야간안 이식, 평형 꼬리깃 이식, 균핵 접목, 정밀 손 보철은 이름으로 예상하는 물리 부품 설치와 다르다. TryHealNode는 missing 부위를 거부하므로 이 치유 효과로 결손을 재건할 수도 없다. 세정·봉합·처치 같은 이름까지 모두 잘못됐다고 하지 않는다.

| procedure ID | 위키 제목 | 체력 회복 | 감염 감소 |
| --- | --- | ---: | ---: |
| procedure:beastkin-sprint-joint | 질주 관절 보강 | 26 | 10 |
| procedure:beastkin-tail-reconstruction | 균형 꼬리 재건 | 28 | 10 |
| procedure:demon-heat-sac | 열낭 강화 | 26 | 10 |
| procedure:demon-mana-core-suture | 마핵 봉합 | 30 | 10 |
| procedure:harpy-air-sac-suture | 기낭 봉합 | 26 | 16 |
| procedure:harpy-feather-regrowth | 깃축 재생 | 20 | 10 |
| procedure:harpy-tail-graft | 평형 꼬리깃 이식 | 28 | 8 |
| procedure:harpy-wing-fixation | 날개 고정 | 32 | 8 |
| procedure:human-neural-assist | 범용 신경 보조기 | 26 | 10 |
| procedure:human-precision-prosthetic | 정밀 보철 조율 | 28 | 10 |
| procedure:kobold-precision-hand | 정밀 손 보철 | 24 | 10 |
| procedure:kobold-tail-balance | 꼬리 평형 보강 | 22 | 10 |
| procedure:myconid-core-graft | 균핵 접목 | 32 | 12 |
| procedure:myconid-hypha-binding | 균사 결속 | 24 | 8 |
| procedure:myconid-regrowth | 균사 재배양 | 36 | 14 |
| procedure:myconid-spore-cleaning | 포자낭 세정 | 12 | 28 |
| procedure:orc-combat-heart | 전투 심장 강화 | 30 | 10 |
| procedure:orc-skeletal-reinforcement | 골격 보강 | 28 | 10 |
| procedure:slime-core-stabilization | 응집핵 안정화 | 22 | 18 |
| procedure:slime-membrane-suture | 외피 봉합 | 26 | 14 |
| procedure:slime-pseudopod-reshape | 위족 재성형 | 30 | 10 |
| procedure:slime-replenishment | 점액 보충 | 18 | 6 |
| procedure:vampire-blood-sac | 혈액낭 처치 | 30 | 10 |
| procedure:vampire-night-eye | 야간안 이식 | 24 | 10 |

모두 targetNodeId가 비어 있다. SurgeryOrderPlanningService는 선택 node 존재와 종족/해부군을 검증하지만 절차 이름에 맞는 특정 기관을 강제하지 않는다. 전체 UI node 선택 제한 재현은 미실행이다.

### DIFF-024 수술 도감의 상세 조건·효과 누락

47개의 facts에는 필요 작업, 필요 연구, 효과 개수만 있으며 개별 효과 값·난도·기본 감염/출혈 위험·마취·강제구속·허용 생체/사체/동물·시설 태그를 표시하지 않는다. relations의 약품/연구 링크만으로 투입량·허용 조건·효과를 설명할 수 없다.

원장 `surgery-projection-followup-review.json`에 선택 authored 필드와 도감 facts를 전수 보존했다. 정적 정의47개와 공개47개의 필드 대조이며, 모든 위험값이 실제 확률로 소비됨을 증명하는 것은 아니다. 예를 들어 표시용 deathChance와 실제 실패 후 중증도 추첨의 관계는 추가 검증 대상이다.

### DIFF-025 수술 환경 중단·재개·위험 합산 규칙이 설명되지 않음

환경이 중요하다는 일반 문장은 있으나 플레이어가 수술을 다시 시작시키는 구체 조건이 없다.

- 정상 환경: 온도16~28°C, 공기≥70, 밝기≥70.
- 극한: 온도<8 또는>35, 공기<40, 밝기<40.
- 비응급 수술은 극한 환경에서 시작/다음 단계 경계에 대기한다. 응급 수술은 지속하며 위험 보정은 받는다.
- 대기 후 정상 환경이 연속5초 유지되어야 재개한다.
- 완료된 각 임상 단계는 한 번만 위험을 적용하고 가중치0.25를 사용한다.
- 온도 극한/중간 불량의 success penalty=0.16/0.08, bleeding added=0.10/0.05.
- 공기 극한/중간 불량의 success penalty=0.12/0.06, infection added=0.25/0.12.
- 밝기 극한/중간 불량의 success penalty=0.20/0.10, organDamage added=0.18/0.08.
- 의사·환자의 노출 단계도 별도 보정한다. 계산된 위험값이 모두 독립적으로 추첨되는지는 추가 확인 대상이다.

연결은 SurgeryRuntime614~778 → SurgeryEnvironmentRuntime.ApplyCurrentStageRisk/ApplyRisk → SurgeryEnvironmentRiskEvaluator.Apply로 확인했다. 단순 DI등록만 근거로 삼지 않았다.

## 맞는 설명: 오탐에서 제외

- 품질 배율8개: 0.80/0.90/1.00/1.10/1.20/1.32/1.48/1.70 일치.
- 장비 준비점수 상한60%, 무기35/방어구30/방패15%와 빈 탄약50% 일치.
- 원거리 명중0.45+사격×0.025+민첩×0.01, 방식1.25/0.75/0.55, 5~95% clamp 및 밝기 감점40/25/10%p 일치.
- 피격확률12/40/12/12/12/12%, 방패35°/70° 경계, 방어구 내구배율0.35~1, 둔기감소 상한0.48, 베기/찌르기 비관통0.52~0.16 및 방어층 존재 시 최저피해0.5 일치.
- 출혈: resolver는 피해×0.02/0.12이나 CharacterBodyHealthRuntime475/480에서 다시0.01을 곱한다. 위키의 초당0.02%/0.12%는 맞다.
- 위키 방어 예시를 독립 산술로 재계산하면 18.7×0.9×0.37=6.2271이다.
- 수술 환경·재활 효과는 서비스가 존재할 뿐인 고아가 아니다. 재활은 치유와 부담 감소를 통해 해당 신체 기능에 간접 영향을 준다.

## 검증 한계·오류 기록

KB query `CombatResolutionService Surgery`, areas code/content, limit10은 stale2429/반환0행이었다. content digest `76cce09a76ded556dc74ac400c571e92136f17f86fc3697dfb9eb30dca669871`, system digest `74d85480f35dc5704306579326fa8f38e3bb5841f0dbaab8e697f4be25522a06`. 생성물을 사용·재생성하지 않았다.

파서 첫 시도에서 YAML `\xB7`를 JSON으로 직접 읽어 실패했고 의미가 같은 `\u00B7`로 변환하여 재검사했다. 가죽은 별도 허용재질 표까지 두 행 매칭돼 최초 거짓 불일치였으며 숫자행 직접 재독으로 정정했다. 잘린 출력/없는 추정 파일/Windows 경로 wildcard 실패는 검토 완료나 부재 증거에 포함하지 않는다.

이번 보고서는 의료·전투 전체 목표 완료를 주장하지 않는다. 수술 효과 실행·저장 경계, 장비73개 상세 도감과 실제 전투 적용·진화, 백신/격리·일반치료 전수, 나머지 경제·사건·계약 등이 남았다.

