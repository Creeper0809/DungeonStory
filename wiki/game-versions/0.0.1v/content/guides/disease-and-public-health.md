---
id: disease-and-public-health
title: 질병, 격리와 공중보건
summary: 전파, 감시, 격리, 예방과 치료가 질병별 처치와 어떻게 나뉘는지 설명한다.
system: health
spoiler_tier: none
---

질병 처치는 환자 한 명의 치료와 정착 전체의 전파 관리로 나뉜다. 환자의 증상과 신체 상태는 개별 질병 문서에서 보고, 감염 경로와 생활 환경은 공중보건으로 관리한다.

## 전파를 막는 운영

공기 전파는 환기, 격리와 출입 관리가 중요하다. 물과 음식으로 이어지는 질병은 급수, 보관, 위생과 폐기물 처리를 함께 본다. 혈액과 체액 노출은 전투·구조·수술의 보호구와 청결 절차에 연결된다. 환자를 옮길 때는 병상까지의 경로가 다른 생활 구역과 얼마나 겹치는지도 확인한다.

## 감시와 격리

| 단계 | 운영할 일 |
| --- | --- |
| 발견 | 증상, 노출 경로, 환자의 현재 업무와 이동 구역을 확인한다. |
| 분리 | 병상, 화장실, 식사·급수, 의료진 출입을 따로 마련한다. |
| 치료 | 질병과 부상, 신체 기능에 맞는 진단·약품·처치를 배정한다. |
| 관찰 | 접촉자, 물·음식, 환기와 오염 구역을 함께 점검한다. |
| 복구 | 격리 해제 뒤 청소, 폐기물 처리, 물자 보충과 업무 복귀를 정한다. |

## 감염 판정

```text
감염 확률
= min(80%,
  기본 감염률
  × 노출 시간 ÷ 24시간
  × (1 - 면역 ÷ 100)
  × 감수성
  × 환경 계수)
```

노출 시간은 하루 최대 24시간까지 합산한다. 같은 질병과 같은 주민의 여러 노출은 환경 계수를 적용한 시간으로 모은 뒤 하루 판정에서 사용한다. 질병별 기본 감염률, 전파 경로, 잠복기, 전염 기간과 중증도는 개별 질병 페이지가 소유한다.

백신 접종 면역은 70에서 시작하고 기본 상태에서 하루 0.05씩 감소한다. 자연 회복 면역은 80에서 시작하고 하루 0.02씩 감소한다. 종족·특성과 질병별 면역 유지 배율이 실제 감소 속도를 보정한다.

## 유행 상태와 의료 용량

같은 질병의 확진이 최근 10일 안에 3건 쌓이면 유행 상태가 시작된다. 마지막 신규 확진 뒤 14일이 지나면 유행 상태가 끝난다. 확진 날짜, 유행 상태와 면역은 저장 뒤에도 이어진다.

| 운영 기준 | 목표 |
| --- | ---: |
| 평시 병상 | 인구 10명당 1개 |
| 유행 대응 격리·회복 자리 | 인구 5명당 1개 |
| 감염병의 완전 노출 1일 기본 위험 | 7~25% |

현재 도감은 감염병 15개와 별도 상태인 핵 부식 1개를 보여 준다. 감염병 전파 검사는 15개를 대상으로 하고, 의료 정의 전수에는 핵 부식을 포함한 16개가 들어간다.

## 질병 도감

다음 문서는 질병별 증상과 처치의 권위 문서다. 전파와 격리의 공통 규칙은 이 문서에서 확인한다.

| 질병 | 질병 | 질병 |
| --- | --- | --- |
| [재먼지폐](/entry/medical/disease-ash-lung/) | [혈액소모병](/entry/medical/disease-blood-wasting/) | [동굴 독감](/entry/medical/disease-cave-flu/) |
| [심층 기생충증](/entry/medical/disease-deep-parasitosis/) | [꿈곰팡이증](/entry/medical/disease-dream-mold/) | [잿불열](/entry/medical/disease-ember-fever/) |
| [유리혈증](/entry/medical/disease-glass-blood/) | [녹무리 감염](/entry/medical/disease-green-swarm/) | [장부패증](/entry/medical/disease-gut-rot/) |
| [마나두창](/entry/medical/disease-mana-pox/) | [밤갈증병](/entry/medical/disease-night-thirst/) | [적열병](/entry/medical/disease-red-fever/) |
| [점액역병](/entry/medical/disease-slime-blight/) | [포자폐증](/entry/medical/disease-spore-lung/) | [백색포자병](/entry/medical/disease-white-spore/) |
| [핵 부식](/entry/medical/condition-core-corrosion/) |  |  |

[의료와 수술](/guide/medical-care-and-surgery/)은 환자를 진단·안정화·치료하는 순서를, [시설과 환경](/guide/infrastructure/)은 물·환기·위생망을, [건강과 공동체](/guide/health-and-community/)는 건강 문서의 진입점을 설명한다.
