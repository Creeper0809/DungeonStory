# V25 숙련 밸런스 집중 검증 보고서

## 31개 작업 권위

| 숙련 | 작업 ID |
|---|---|
| 현장 작업 | `work:restock`, `work:haul`, `work:draw-water`, `work:refuel`, `work:gather`, `work:logging`, `work:quarry` |
| 건설·공학 | `work:construct`, `work:repair`, `work:plumbing`, `work:dismantle`, `work:grand-project` |
| 제작 | `work:craft` |
| 식량 생산 | `work:hunt`, `work:butcher`, `work:cook`, `work:sow`, `work:harvest`, `work:animal-care` |
| 학술 | `work:research` |
| 의료 | `work:rescue`, `work:treat`, `work:surgery` |
| 사교 | `work:reception`, `work:warden`, `work:perform` |
| 직접 작업 XP 없음 | `work:operate`, `work:clean`, `work:guard`, `work:rest`, `work:threat-mitigation` |

`work:operate`는 범용 추측을 금지하고 시설의 typed 실행 역할이 별도 숙련을 명시해야 한다. 경비 대기는 전투 XP를 주지 않으며 실제 공격·방어·훈련 사건만 전투 숙련 명령을 호출한다.

## 집중 결과

- 9개 안정 ID의 유효성·중복 없음: PASS
- 31개 작업의 mapped/XPless 단일 처분: PASS
- 99 WU/일 이론 도달 13/51/152/379일: PASS
- 0 WU 및 대기 결과 0 XP: PASS
- 전문가·대가 유예 및 임계 강등: PASS
- 품질 점수 75% 숙련 등급 + 25% 능력치: PASS
- 1 XP 누적 전에는 쇠퇴 시각 미갱신: PASS
- 전투 8 XP/일, 안전 훈련 2 XP/일: PASS
- 동일 전투 사건 키 중복 지급 방지와 저장 상태: PASS
- 시설 419개·조합식 354개·전투 장비 61개·의복 56개 authored profile: PASS
- 자동 생성 연결 부록 `v25-proficiency-authored-mapping.md`: PASS
- 100,000회 품질 표본과 960일 지연 쇠퇴·평생 획득 장부: PASS
- 2,000명 지연 정산: `0.459ms`, 현재 스레드 할당 `0B`, 시간당 전 주민 순회 없음: PASS
- 멘토 관계·등급·정원·하루 한 번·양쪽 30 WU·저장 왕복: PASS
- 방어 런타임 유효 공격·명중·피해·방패 차단·조우 완료 XP: PASS
- `1600×900`, `900×1600` 숙련 탭·멘토 관리 포인터 및 캡처: PASS
- 전체 월드 저장 `68/68/68`, 정규 기준선·라이브 기준선 복원: PASS
- Unity 스크립트 컴파일, Console Error/Warning: 0/0

## 판정 범위

V25 9종 숙련의 구현·집중 밸런스·저장·UI 검증은 완료됐다. 이 판정은 숙련 시스템 범위에 한정하며 전투 승률, 전역 생산 경제와 이정표 도달 시점까지 포함한 전체 게임 밸런스 완료 선언은 아니다.
