# 수술대 7시설 primary·보정·환자슬롯 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingSurgeryTableAbility` 작성 시설은 M01 응급 처치대, M03 외과 수술대, RF68~72 연구 수술시설까지7개다.
- 일곱 시설은 모두 primary 수술시설이며 작성 tag, success, sterility, speed가 room snapshot과 위험·작업식에 실제 들어간다.
- `patientSlots=1`은 일곱 자산에 작성되어 있지만 ability 정의와 builder 외 runtime consumer가 없다. 현재 동시 환자 제한으로 공개하면 안 된다.
- 공개 시설 페이지와 의료 가이드는 이 계약을 설명하지 않는다. GAP-118/119의 procedure field·일반식과 시설별 작성값은 별도이므로 GAP-185로 분리한다.

## 작성값과 활성 효과

| 시설 | tag | success | speed | sterility | patientSlots |
| --- | --- | ---: | ---: | ---: | ---: |
| M01 응급 처치대 | Emergency | -0.05 | 1.3 | 0.12 | 1, 미소비 |
| M03 외과 수술대 | GeneralSurgery | 0.08 | 1 | 0.35 | 1, 미소비 |
| RF68 장기 재생 수술실 | AgeTreatment | 0.12 | 1 | 0.45 | 1, 미소비 |
| RF69 회춘 수혈실 | AgeTreatment | 0.12 | 1 | 0.45 | 1, 미소비 |
| RF70 룬 동면실 | AgeTreatment | 0.12 | 1 | 0.45 | 1, 미소비 |
| RF71 전신 재생조 | AgeTreatment | 0.12 | 1 | 0.45 | 1, 미소비 |
| RF72 시간 고정실 | AgeTreatment | 0.12 | 1 | 0.45 | 1, 미소비 |

- 모든 `BuildingSurgeryTableAbility`는 `IsPrimaryOperatingFacility=true`, anesthesia0이다.
- `SurgicalFacilityQuery`는 primary와 같은 usable room의 비파괴·비손상 수술시설 abilities를 support로도 합친다. 다른 surgery table도 support에서 tag·success·sterility·speed를 그대로 제공한다.
- snapshot은 tag OR, success/sterility 합, speed 곱 뒤 clamp한다. 따라서 동일 tag가 중복이어도 수치 보정은 시설 수만큼 누적된다.
- 시설 자체의 GAP-119 성공식 clamp 전 입력은 M01 `-0.05+0.12×0.08=-0.0404`, M03 `0.08+0.35×0.08=0.108`, RF 각 `0.12+0.45×0.08=0.156`이다.
- 감염식 입력은 M01 `-0.12×0.45=-0.054`, M03 `-0.35×0.45=-0.1575`, RF 각 `-0.45×0.45=-0.2025`다. M01 speed1.3은 persistent surgery work에 실제 곱해진다.
- M01은 support일 때도 success-0.05를 더하므로 sterility0.12의 성공 기여+0.0096을 합쳐 시설 자체 순 성공 입력은-0.0404다. 대신 감염 입력과 작업속도는 개선한다.

## tag별 현재 절차

- Emergency 요구는 혈액 수혈10WU, 응급 봉합12WU, 이물 제거16WU 세 개다.
- GeneralSurgery 요구는29개다. 이 가운데 다른 tag와 함께 쓰이는 절차도 같은 room support로 모두 충족해야 한다.
- AgeTreatment 요구는 회춘 수혈72WU, 장기 재생96WU, 룬 동면64WU, 시간 고정140WU, 전신 재생180WU 다섯 개다.
- RF68~72는 각각 다른 전용 tag가 아니라 모두 동일 AgeTreatment tag를 제공한다. 현재 facility gate만 보면 어느 하나의 RF도 다섯 AgeTreatment 절차를 모두 충족할 수 있다.
- 같은 방의 다른 primary가 필요한 tag를 support로 제공하면 그 다른 primary도 후보가 될 수 있다. 후보는 최종 success, sterility, persistent facility ID 순으로 정렬된다.

## 공개·UI 경계

- building-9501/9503 facts는 분류·크기뿐이고 summary는 내부 공정 재고·진료와 치료·건설비만 보여 Emergency/GeneralSurgery tag와 보정을 숨긴다.
- building-8868~8872는 모두 생산 작업대·건설비만 요약해 AgeTreatment primary 수술시설 역할 자체를 숨긴다.
- 의료 가이드와 수술 UI는 support 목록, 시설별 success/sterility/speed 분해, patientSlots 미소비 및 RF 다섯 시설의 동일 tag를 설명하지 않는다.

## 정적 검증 경계

- ability 정의와7개 작성 자산,47개 절차의 tag census, room query·snapshot clamp·위험식·persistent work, 공개 시설7페이지·의료 가이드와 기존 수술 보고서를 읽었다.
- 실제 primary 선택, 같은 방 table stack, 동시 수술/환자 슬롯, RF별 AgeTreatment 실행, 저장 왕복은 Play Mode에서 재현하지 않았다.
