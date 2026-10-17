# WIM 완료 주장 엄격 재감사 — 2026-09-13

상태: 전체 완료 판정 기각. 현재 소스와 기존 증거를 대조한 재감사이며, 모든 기능을 새로 Play 실행한 전수 재인증은 아니다. 생산 코드 수정은 하지 않았다.

## 감사 범위와 방법

- 사용자 요청: “다 구현한 거임? 한번 더 엄격히 검증해봐.”
- 비교 권위: 사용자 확정 결정, 기존 goal 본문의 원본47건·연구1건·위키13건·추가062/063·FIRE-01·계획7.4 전체, `wim-implementation-plan.md`, 현재 C#/작성 자산, 기존 PASS 보고서의 실제 범위.
- 완료 체크는 검증 대상이다. 체크박스, 에이전트 완료 주장, 컴파일 성공, 생성기 검증0만으로 실행 연결을 인정하지 않는다.
- 이번 재감사는 생산 코드·테스트 코드·에셋을 수정하지 않는다. 기존 유효 실행 증거는 재사용하며, 관련 코드의 실제 결함과 증거 범위 불일치를 구분한다.

## 확정 발견

### A1. 승인 범위7.4를 임의로 목표 밖으로 빼고 완료 처리함

기존 goal 본문은 “계획7.4의 전역 유지비 전면 제거·계절2종 실제 공급/납품 전환·코볼트 숨기기 설정 전용·요청/축제 장소9군 재사용을 포함한다”고 명시했다. 그런데 직전 종료 기록은 열린7.4.1/7.4.4를 독립 후속으로 제외했다. 사용자가 승인한 범위를 축소한 것이므로 `47/47` 부모 체크만으로 전체 goal을 완료할 수 없다.

- 계획7.4.1: `docs/game-design/wim-implementation-plan.md:1126`, 실제 연료 배송 PASS와 별개로 전역 전망/실제 위험 정리 OPEN 명시.
- 잔여 요구: 같은 파일1134–1135.
- 직전 잘못된 후속 분류: 같은 파일1185 및 `.planning/2026-09-06-wim-implementation-plan/progress.md`의 WIM-040 closure 절.
- 원본47건과 추가 범위는 별도 계수하되 둘 다 승인 범위에서 이행해야 한다.

### A2. 물 재고 전망이 실제 질병·기분 손실을 일으킴 — 실제 코드 결함

`SurvivalFoodRuntime.ProcessDailySurvival`은 일일 갱신에서 `ConsumeDailyWater`→위험 평가→`ApplyHealthConsequences`를 호출한다. `ConsumeDailyWater`는 실제 음용 결과 없이 Stored/Loose 물 아이템 개수로 부족 인원과 부족 일수를 갱신한다. 그 전망값이 `SurvivalEnvironmentRiskEvaluator`에서 위생/질병 위험으로 변환되고, 질병 위험55 이상이면 실제 캐릭터에 Sick과 기분−4를 적용한다.

실제 캐릭터는 `CharacterSafeReliefRunner`의 WorldSource 경로에서 안전한 수원 물을 마시고 갈증을 회복할 수 있다. 이 경로는 창고 물 아이템을 필요로 하지 않으므로, 안전하게 물을 마시는 집단도 재고 전망만으로 병에 걸릴 수 있다.

| 현재 원본 | 근거 |
|---|---|
| `Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs:332` | 일일 전망→위험→건강 적용 호출 사슬 |
| 같은 파일896–910 | 인원수−Stored/Loose 물 개수, 부족 일수 갱신 |
| `Assets/Scripts/Services/Survival/SurvivalFacilityWorkRules.cs:162` | missingWater×8을 sanitationRisk에 적용 |
| 같은 파일170 | shortageDays×12를 diseaseRisk에 적용 |
| `Assets/Scripts/Services/Survival/SurvivalFoodRuntime.cs:939` | 위험55 문턱 뒤 실제 Sick/기분 적용 |
| `Assets/Scripts/Services/Survival/CharacterSafeReliefRunner.cs:604` | 안전한 WorldSource 실제 음용·갈증 회복 |

독립 산술 확인: 성인6명, 재고 물0, 부패0·환기0·별도 위협0·음식 부족0에서 위생 위험48, 질병 위험은 1일38.4→2일50.4→3일62.4다. 실제 갈증100 여부는 이 산식에서 읽지 않는다. 이는 현재 소스와 독립 산술로 확인했으며 이번 감사에서 새3일 Play를 실행한 결과라고 주장하지 않는다.

필요 수정: 보관량 기반 전망은 UI/공급 계획에만 사용하고, 건강 불이익은 실제 음용·갈증·오염 노출 권위에 연결한다. 기존 물 섭취·시설 가동 소비를 없애는 수정은 아니다.

### A3. 6인 최종 통합을 음식·물 정적 산식 보고서로 대체함 — 필수 증거 공백

직전 완료 근거 `v27-balance-six-adult-food-water-loop.txt`는 실제6인 실행이 아니라 `SurvivalClosedLoopCalculator.Assess`의 산식 검증이다. 반복 노동은 crop+cooking+water production+manual refill만 합산한다. 의복 세탁/건조/수선, 실제 운반 지연·예약, 저장 입고/공간, 조명·급수 운영의 결합 부담은 입력에 없다.

- 정적 계산 호출: `Assets/Scripts/Services/Economy/Editor/V27SixAdultSurvivalLoopDebugScenarios.cs:118`.
- 반복 노동 합산: `Assets/Scripts/Services/Economy/V27SurvivalClosedLoopModels.cs:291`.
- 필요한 최종 통합: `docs/game-design/wim-implementation-plan.md:1308`.
- 기존 보고서 결과 자체의 PASS는 보존한다. 이를 최종 물류·의복·생육 통합 PASS로 확장한 보고가 잘못됐다.
- 보고서 입력 digest: `2c817991e2ca8ff193c115224ee92f9b5077e9056014d2be49bbe449ae4fd8dd`.
- 생성 코드에 명시된14개 입력을 동일 ordinal 경로/파일 SHA-256/UTF-8 방식으로 재계산한 현재 digest: `d70ac47245c6fdf0f1afbe5b5d72ee92a39e8918baab3ffc180acf1ca46e326e`.
- digest 차이만으로 기능 실패라고 판정하지 않는다. 그러나 최신 입력/실행 근거로 무조건 인용할 수 없으며, 실제 변경 영향과 최종 통합 측정은 확인해야 한다.
- 이번 감사에서 주 Unity의 읽기 전용 `CapturePopulationStage(6)`를 직접 실행했다. 현재 입력 결과는 `Passed=true`, 재배지3, 반복 노동86.618WU/일(32.1%), 총생산420/순생산399 nutrition/일, 필요 비축 질량77.7kg로 기존 값과 같았다. 구형 보고서 해시와 별개로 **현재 음식·물 정적 계산은 PASS**다. 이 좁은 검사를 최종 통합으로 확대할 수 없다는 판정은 유지한다.

### A4. WIM-040 생애 사건31종이 발생 경로 없이 차단됨 — 구현 누락

`V20CampaignRuntime.cs:4768–4877`의 두 생애 사건 생성 경로는 모두 `dailyCadenceCandidate: true`로 평가한다. 같은 파일4952–4955는 `DailyCadence`가 아닌 생애 사건을 제외한다. 현재32개 작성 정의 중 은퇴1개만 DailyCadence이고31개는 ExternalObservedOnly다. 외부 관측을 받아 이31개를 발생시키는 생산 명령/호출자는 확인되지 않았다. 단순 stable ID 검색 부재가 아니라 생애 사건 생성 지점, 명령 인터페이스와 정책 분기를 함께 추적한 판정이다.

- `ISocietyEventCommand`(같은 파일1281–1289)는 일일 평가와 해결을 제공하며 외부 생애 발생 명령을 제공하지 않는다.
- `wim-040-incident-completion-focused.txt:7`은 일일 후보에서31개가 제외되는 것만 PASS한다. 보고서8행은 isolated scope와 live physical 미실행을 명시한다.
- 사건에 필요한 실제 원인/대상을 연결하는 요구를 비활성화만으로 완료 처리할 수 없다. 근거 없는 일일 무작위 발생으로 되돌리는 것도 해결책이 아니다.
- EnvoyConflict/Sabotage의 명시적 dormant 처리와 이31개 생애 사건의 미구현을 혼동하지 않는다.

### A5. WIM-040 작업 사고의 피해와 사건 알림/대응이 연결되지 않음 — 구현 누락

`Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs:3672–3806`에는 승인 WU, 실제 작업자/피로/시설에 따른 사고 추첨과 신체 피해가 있다. 그러나 성공 후3774–3805는 신체 피해, 선택적인 화재 receipt, 개인 activity를 남길 뿐 Society 사건을 생성하지 않는다. `ServiceIncidentKind`와 실제 capture 호출자에도 작업 사고 연결이 없다.

계획693행은 기존 사건 알림에 피해 대상·발생 원인·실제 피해·필요 대응을 표시하도록 요구한다. 계획698행의 “ServiceIncident ... 작업 사고 ... 생산자” 종료 주장은 실제 코드와 다르다. 사고 피해가 아예 없는 것은 아니며, 새 사고 엔진이 필요한 것도 아니다. 기존 사고 결과를 기존 알림/대응 경로에 연결하는 부분이 남았다. 새로운 독립 사건 종류를 임의 추가하는 결론은 내리지 않는다.

확인된040 부분은 유지한다: 실제 식사/오염·절도·신체 충돌·문화 충돌 생산자, 대체 식사/응급 처치/격리 이동의 receipt 이전 효과 금지, 활성 operation의 저장 join. 완료된 replacement history는 이력 보존 차이로 owner 부재를 허용하므로 “모든 과거 operation의 orphan 양방향 거부”로 확대하지 않는다.

### A6. WIM-041 축제 제출 참가자 입력과 실행 대상이 다름 — 계약 결함

- `FestivalScheduleRequest.ParticipantIds`: `Assets/Scripts/Services/Character/FuneralFestivalRuntime.cs:63–70`.
- 같은 파일212–229의 `AttendFestival` 호출자가 지정한 참가자를 request에 담는다. `V20ContentResolutionService.cs:751–759`의 일반 개최 경로도 참가자 목록을 담는다.
- `FestivalExecutionRuntime.Schedule`(505–643)는 request의 ParticipantIds를 읽지 않고 BuildPreview 결과를 저장한다(624행).
- `BuildPreview`(417–452)는 전체 적격 직원에서 최대 인원부터 장소를 찾고 참가자를 새로 고른다. 따라서 제출된 부분집합·다른 순서의 대상은 보존되지 않으며, 계획7.4.4의 최소 인원 preview/실제 제출 인원 배정 계약과 불일치한다.
- 기존 `wim-041-festival-execution-live.txt:8`은 실제 이동·소비·중단·저장에 유효하지만 test-only festival과 venue selector를 사용했다. 실제 authored venue query와 Schedule을 연결한 이 경계의 증거는 아니다.

필요 수정/확인: 기존 문화·필수 업무 제외를 유지하면서 참가자 입력과 preview/배정 계약을 일치시키고, 실제 작성 축제/venue를 이용하는 대표1건으로 입력 대상·실제 배정/참석을 확인한다. 새 참가자 선택 UI나 전체16종 자연 달력 반복을 요구하지 않는다.

잠재 문제는 현재 결함과 분리한다: `SocietyVenueQuery.CaptureClaims`(697–733)는 guest claim만 조회하고 축제 예약을 합산하지 않는다. 그러나 live16의 날짜는 고유하고 18시 시작·2~5시간 지속·최대2일 준비창도 서로 겹치지 않는다. 따라서 “현재 두 축제가 실제 같은 시간 중복 개최됨”이라고 보고하지 않으며, 신규 동시간 축제를 추가할 때의 계약 보완으로 기록한다. 이번에는 새 필수 테스트/완료 차단을 추가하지 않는다.

## 완료 기록 정정과 남은 작업

| 구분 | 재감사 후 상태 |
|---|---|
| 원본 WIM47건 | 기존 완료45/47(95.7% 항목 수) 유지,040/041 재개방 |
| 연구1건·위키13건 | 기존1/1·13/13 유지; 이번에 전부 새로 실행/재작성한 것이 아님 |
| 추가062/063·FIRE | 기존2/2·4/4 증거 유지 |
| 승인7.4 | 7.4.1 물 전망/건강 연결,7.4.4 실제 참가자/장소 배정 계약 잔여 |
| 최종8.4 | 대표6인 물류·의복·질량·생육 통합 검증 대기 |

이번에 닫은 구현 체크포인트는0개다. 기존 완료를 뒷받침하지 못하는040/041 부모 체크만 다시 열고 통과한 하위 증거는 보존했다. 목표를 임의 재설정하거나 구현을 시작하지 않았다. 기존 tool goal의 종료 상태/체크 개수 자체는 실행 완료 증거가 아니다.

최소 잔여: 실제 생애 원인 연결과 작업 사고 알림, 축제 제출 대상/배정 계약, 물 전망과 실제 건강 불이익 분리, 그 영향만 확인하는 focused 검증과 대표6인 최종 통합이다. 승인되지 않은 콘텐츠 제외나 새 UI/범용 프레임워크로 해결하지 않는다.

## 감사 범위의 한계

- 메인은7.4/생존·6인 계산·사건/축제의 결함 경로를 직접 추적했다. 독립 작업자는040 실제 생산자/저장 응답과7.4.4 실제 venue/일정에 대해 읽기 전용 교차 검토했다.
- 문서 작업자는 부모 체크·과거 unchecked·증거 scope를 대조했지만47개 최신 생산 경로/실행 증거를 모두 재인증하지는 않았다. 특히001/043/046/053 등에 대해 이 턴의 독립 전수 인증을 주장하지 않는다.
- 문서 작업자의040 CLOSED 판정은 부모 문서에 의존했다. 이를 현재 소스의 생성 경로 단절에 대한 반증으로 인정하지 않았고, 소스를 추적한 독립 검토와 메인의 직접 확인으로 A4/A5를 확정했다.
- 010/055의 과거 unchecked,009의 옛 OPEN,045/056의 과거 manifest PARTIAL/미실행 문구만으로 새 게임 결함을 단정하거나 이미 통과한 경로를 다시 열지 않는다. 최신056 의복·장비·건설 화면 PASS는 보존하며 full-world restore 미실행을 해당 화면 기능 실패로 확대하지 않는다.
- 새 Play 재현은0회다. A2의3일 질병 수치, A4/A5의 생산자 단절, A6의 입력 무시는 소스/계약/독립 계산 확인이다. 새로 실행한 것은 현재 Unity의 읽기 전용6인 정적 계산1회뿐이다.
- 따라서 이 재감사는 **전체 완료가 아니라는 결론을 확정**하지만 나머지 코드에 추가 결함이 절대로 없다는 인증은 아니다.

## 이번에 수행한 확인

- 공식 Unity CLI: F 주 프로젝트 `editor_status` ready/stopped, compiling=false, reload=false. heartbeat `2026-09-13T10:22:10.0517854Z`.
- `recompile_status`: 마지막 컴파일 completed, failed=false, errors=[]. 새 컴파일을 실행한 것이 아니다.
- 공식 `unity command eval`로 `V27SixAdultSurvivalLoopDebugScenarios.CapturePopulationStage(6)` 1회 실행: success=true, diagnostics=[], errors=[], warnings=[]. 원본 자산 읽기와 정적 평가만 수행했으며 `RunAll`/생성물 쓰기/Play는 호출하지 않았다.
- 지식베이스 query는 감사 시작 시 fresh. content digest `2153ea8bc0d50d7f1349f0245b4a8b623228eeddd89d8c0d05d195442ee8c553`, system digest `b2ac38f1ff0f0e81af44cbc09d8a4695722048bad5af84e3bc9f226b1d8c04f7`.
- query `SurvivalFoodRuntime ConsumeDailyWater ConsumeDailyFuel operatingFuel FestivalExecutionRuntime`, 영역 code/authority/persistence, 생성 후보0행. 실제 심볼/원본 역검색으로 전환해 확인했고0행을 미구현 증거로 쓰지 않았다.
- 생성 인덱스의 미해결 콘텐츠 참조25건·수동 검토44건은 별도 분류다. 생성기 실패0은 그 항목의 정상 게임 실행 증명이 아니다.
- 이 감사의 문서/기록 이외에 게임 소스·SO·Unity 씬·사용자 세이브를 수정하지 않았다.
