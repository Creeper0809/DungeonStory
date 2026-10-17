# 보철 조립대 수술지원·제작품 품질 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingProstheticAssemblyAbility` 작성 시설은 M06 보철 조립대(id9506) 한 개다.
- M06은 같은 시설 안에 수술 support와 `m06` 수동 생산 작업대라는 두 역할을 함께 갖지만, 두 역할의 속도·품질 값은 같은 경로가 아니다.
- 공개 M06 페이지는 내부 공정 재고·생산 작업대·건설비만 설명하고 수술 support, 보철 recipe와 제작품 quality 계약을 설명하지 않는다. 의료 가이드에도 M06 설명이 없다.
- 기존 GAP-118은 절차별 required facility field, GAP-119는 일반 수술 위험식을 소유할 뿐 M06 작성값과 제작품 품질 경계를 소유하지 않으므로 GAP-183으로 분리한다.

## 수술 support 계약

| 필드 | 작성값 | 현재 실행 |
| --- | ---: | --- |
| assemblySpeedMultiplier | 1.1 | 같은 usable room 수술의 persistent work speed에 곱함 |
| qualityBonus | 0.06 | 제작품 quality가 아니라 수술 `SuccessBonus`에 더함 |
| FacilityTags | ProstheticAssembly | 골렘 수술5개의 시설 요구를 충족 |
| primary | false | M06 단독으로 수술 primary가 될 수 없음 |
| sterility / anesthesia | 0 / 0 | 해당 위험 보정 없음 |

- `SurgicalFacilityQuery`는 primary와 같은 usable room의 비파괴·비손상 수술시설을 모두 support로 합친다. M06 한 대는 speed×1.1, success+0.06을 제공한다.
- 여러 M06을 같은 방에 두면 대수만큼 speed를 곱하고 success를 더한 뒤 snapshot에서 speed0.25~3, success-0.25~0.35 clamp를 적용한다.
- M06 한 대의 수술 성공식 입력은 facility contribution+0.06이고 sterility/anesthesia 추가분은0이다. `SurgeryWorkExecutionHandler`는 현재 room snapshot의 speed를 persistent surgery work multiplier로 사용한다.
- ProstheticAssembly tag를 요구하는 현재 절차는 골렘 몸체 재주조·냉각수 보충·동력핵·감각핵·구동부 조율5개이며 모두26WU다. M06이 primary가 아니므로 다른 primary와 같은 usable room에 있어야 한다.
- 반대로 보철 설치 절차의 required tags는 GeneralSurgery+Sterilization+Anesthesia(28)이며 ProstheticAssembly를 요구하지 않는다. 그래도 같은 방에 M06이 있으면 일반 support 보정은 적용된다.

## `m06` 제작과 품질 계약

- M06의 생산 작성값은 workstationTag `m06`, manual lane1, automatic lane0, output cycle capacity4, overflow dump 비허용이다.
- `facilityTag:m06`인 recipe는 철제 의수32WU, 철제 의족32WU, 인공 안구36WU 세 개다. 각 recipe는 left surgical part1개를 exact output으로 만든다.
- 생산 output 계획은 작업자의 관련 제작 숙련도/58을 0.7~1.25로 clamp해 `WorkerQuality`로 저장한다.
- `SurgicalPartProductionOutputHandler`는 이 WorkerQuality를 part quality로 넘기고 `SurgicalPartRuntime`은0.1~1.75로 clamp해 part와 output component에 기록한다.
- production support의 별도 `qualityModifier`도 output context까지 전달되지만 surgical part handler는 읽지 않는다. M06의 `qualityBonus0.06`은 애초 `ISurgicalFacilityAbility.SuccessBonus`이며 제작품 quality의 직접 consumer가 아니다.
- 제작된 part quality는 설치 때 `effect.efficiency×consumed.quality`에 실제 곱해지고 결과는0.1~1.5로 clamp된다. 따라서 보철 효율을 바꾸는 현재 제작 품질은 M06 시설 보너스가 아니라 작업자 숙련 기반이다.
- resolved output은 workerQuality를 생산 bill save data에 보존하며 surgical part의 quality도 aggregate/save validation 대상이다.

## 공개·UI 경계

- 공개 building-9506 facts는 분류·크기뿐이고 summary는 내부 공정 재고, 생산 작업대, 건설855WU·강철6만 표시한다.
- 의료 가이드는 M06, ProstheticAssembly tag, 수술 보정, 세 보철 recipe와 quality 산출을 설명하지 않는다.
- 수술 UI의 최종 성공률만으로는 M06 support의 +0.06과 speed×1.1을 분해할 수 없다. 공개 설명에서 `qualityBonus`를 제작 품질 보너스로 옮기면 현재 구현과 반대가 된다.

## 정적 검증 경계

- M06 ability/workstation/buffer와 세 recipe, 다섯 골렘 절차 및 보철 설치 절차, facility query·위험·persistent work, 생산 output 품질·저장, surgical part 생성·설치 소비, 공개 페이지와 의료 가이드를 읽었다.
- 실제 생산 queue, 작업자 숙련별 part quality, 다중 M06 수술 속도/성공률, 저장 왕복과 보철 설치 효율은 Play Mode에서 재현하지 않았다.
