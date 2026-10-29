# R31 항목별 수정과 확인 수준

기준 main: `1d28d006107a71c62c405086c05aca3f3f6ebefc`.
현재 native source: `de6736651e5202a94fdb39731091a1abab146d98c1df338eb912f3c3340b0919` (`r254r31`).

| 항목 | 수정 | 현재 표적 실행 근거 |
|---|---|---|
| R31-01 방문객 취소 | population owner의 취소 명령으로 신분과 방문 상태를 함께 되돌린다. 획득 반환은 복사본이며 바인딩은 ID로 소유 원본을 찾는다. 구형 취소 저장의 모순만 정상화한다. DTO/ID/완료 방문 횟수 계약은 유지한다 | 실제 population service에서 외부 반환 변조 격리, 반복 취소/조회 불변, 공식 저장 생성/읽기 경로, 새 owner 복원, 같은 ID 재입장, 바인딩 후 취소 거절, 실제 퇴장 횟수, 구형 입력과 누락 ID 거절 PASS |
| R31-05 운반 캐시 | 현재 적재 무게, 허용 무게, 질량 권위 revision을 캐시 입력에 포함한다. 실패한 계산의 새 키를 게시하지 않는다 | 실제 운반 planner에서 다른 revision을 고정한 하중/능력 전환, 질량 계산 거절 후 재시도, 운반 DTO JSON과 새 fixture 복원 PASS |
| R31-06 방 환경 캐시 | 환경 필드 Version을 포함하고 계산 성공 뒤에만 결과와 키를 함께 게시한다 | 실제 방 evaluator/query에서 온도/공기/빛만 변경한 재계산, 과거 snapshot 불변, 실패 뒤 재계산 PASS |
| R31-03 연구 후보 | UI는 owner의 현재 프로젝트 자격 조회를 사용한다. 사용한 인물은 건너뛴다. 조회가 대기열을 이동하거나 미준비 인물을 초기화/체력 조회하지 않는다 | 실제 selector와 연구/특성 runtime에서 A 사용 뒤 B 선택, 조회 불변, 중복 거절, identity-state JSON 새 owner 복원 뒤 B 선택, 모두 사용한 경우와 미준비 인물 PASS |
| R31-04 수리 후보 | 예약 snapshot에서 이미 접수된 장비를 제외한 뒤 내구도 순으로 고른다. 기존 owner의 중복 거절은 유지한다 | 실제 presenter와 엄격한 port fixture에서 A의 침공 종료 대기 주문/DTO JSON을 보존하고 다음 요청으로 B를 선택 PASS |
| R31-02 생산 용량 | UI와 실제 owner가 같은 Processing 점유 규칙을 쓴다. Finishing의 남아 있는 node ID만으로 용량을 점유했다고 표시하지 않는다 | 실제 맥주/양조장/WS03 에셋 및 owner/UI 계산에서 Processing 점유와 Finishing 해제 판정 일치 PASS |

## 한계와 보존

- 7개 표적 native 계약이290초/exit0/CS0/consoleIssues0으로 종료했다. 실행 전후21772입력 SHA drift0, 비대화형 private Xvfb와 창 프로브, Unity/display/compiler 종료를 확인했다.
- 통제 fixture 실행이다. 자연 UI 클릭과 렌더링 품질을 검증했다고 쓰지 않는다. 수리 항목의 DTO JSON은 실제 maintenance owner 전체 저장 검증을 뜻하지 않는다. 운반 DTO만 복원한 반례는 물리 물품 보존을 뜻하지 않는다. 방 필드 변경 fixture는 전체 환경 저장 복원을 대신하지 않는다.
- 실제 게임 월드 저장 복원과 기존 영향 CI7개 native 실행도 같은 현재 source에서 모두 정상 종료했다. core330/character220/world260/crop210/effects-traits200/definitions1020/recovery980초, CS0/stop없음/drift0이다. 최종 통합 상태는 `../ImpactCI/verification.json`과 GitHub 결과를 따른다.
- 씬, 프리팹, SO 에셋과 저장 스키마 버전은 바꾸지 않았다. 수동 Inspector 연결 작업은 없다.
- H01~H12는 실제 결함이 확인되지 않은 보류 후보다. 반환 형식이나 줄 수만으로 필수 분할 대상으로 승격하지 않았다.
- 이전 r251/r252/r253은 각각 전체 FAIL이다. 부분 통과를 현재 PASS에 합치지 않았다. 원문과 SHA는 `targeted-runs.json`에 연결한다.
