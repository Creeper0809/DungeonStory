# V27 CI 실행 범위

일반 PR과 main push는 `verify_change_scope.py --base <이전 커밋> --head <검증 커밋>`을 실행한다. Git diff의 삭제·이동 전후 경로도 검사한다.

- 문서만 바뀌면 Unity를 다시 실행하지 않는다.
- Unity 입력이 바뀌면 현재 소스 컴파일과 `core`, 실제 씬의 `world` 복원 검사가 필요하다. 알려진 도메인은 `DOMAIN_RULES`로 짧은 회귀 검사를 추가한다. 공용 의존성·미분류 입력·CI 정책 변경은 모든 짧은 검사를 요구한다. 경로 규칙은 의미 의존 그래프의 완전한 증명이 아니므로 미분류 변경을 건너뛰지 않는다.
- `core`는 저장 후보의 원자성, 잘못된 데이터의 무변경 거절, 생산 ACK 전후의 JSON/fresh owner 복원·단일 재시도, 기술 요청의 복원·명시 재시도, 난수/박탈 상태 저장 계약을 실행한다. `character`는 준비/월드 권위·게시 실패 롤백, `effects`는 카탈로그 갱신·효과 조합, `crop`은 실제 Play의 수확 권위·복원 재시도를 검사한다.
- `world`는 기존 공식 GameplayScene 전체 저장 round trip을 한 번 실행한다. JSON 재생성, 실제 section 복원, 후속 처리의 중복 방지, baseline 원상 복원과 console-zero를 요구한다. 자연 AI 전수 조합이나 장기 성능 인증을 의미하지 않는다.

Unity 실행은 [격리 실행 지침](../../docs/dev/unity-workflow.md)을 따른다. 공개 진입점은 `verify_change_scope.py`의 `GROUPS`에 있다. `core/character/effects`는 동기 EditMode execute, `crop/world`는 quit 없이 Play lifecycle을 사용한다. 모든 작성자를 동결한 뒤 현재 입력을 실제 컴파일·실행한다.

`effects` 전체 묶음이 실행 한도를 넘는 경우에는 `EFFECT_PARTS`의 정의/참조, 물리 복구/효과, 후천 특성 세 진입점으로 실행할 수 있다. 기존 전체 진입점과 세 진입점은 같은 검사 본문을 호출한다. 영수증의 `effects.parts`에는 정확히 세 native 결과가 모두 있어야 하며 각각 현재 source/config, 종료, 콘솔, 로그와 summary SHA 검사를 통과해야 한다. 일부 누락, 중복, 다른 진입점, 오래된 입력, 중단 또는 오류가 있으면 상위 effects도 실패한다. 시간/메모리 한도를 늘리거나 검사 항목을 생략하는 방식이 아니다. 다른 네 그룹과 기존 전체 effects 영수증도 같은 기준을 유지한다.

실행 후 `Artifacts/QA/ImpactCI/verification.json`에 source digest/count/path digest, configuration digest, 실행 group/method와 native summary/정제 로그의 경로·SHA256을 기록한다. 로그의 `IMPACT_CI_PASS`는 기존 시나리오가 성공하고 입력·콘솔·cleanup을 확인한 뒤에만 생성된다. CI는 현재 C#/package 입력과 Assets/Packages/ProjectSettings 전체 파일(런타임 JSON·바이너리 포함), 정확한 완료 표식, native exit/중단/컴파일/소스 drift 및 로그 bytes를 다시 비교한다. 누락·중단·오래된 입력의 실행은 실패한다. 이 작은 실행 증거의 전역 입력 결합은 빠른 현재 컴파일/회귀에만 적용하며 장시간 밸런스 증거의 재생성을 요구하지 않는다. PR은 head checkout과 merge-base→head 변경을 사용한다. main push는 실제 병합 tree를 다시 검사하므로 main에 새 변경이 생겼다면 병합 전에 통합 입력으로 검증을 갱신한다.

변경하지 않은 V27 원장과 보고서는 manifest에 기록된 byte 무결성만 확인하고 **과거 기록**으로 표시한다. 현재 코드의 전체 밸런스 PASS로 간주하지 않는다. 원장·승인·밸런스 보고서 자체를 바꾸면 기존 strict `verify_committed_artifacts.py`가 추가 실행된다. 이 검사는 원래 source/scene/seed/복원/무변경 계약을 유지한다. GitHub의 수동 `workflow_dispatch` 역시 전체 출시 인증을 실행한다. 일반 PR에 bootstrap92×32·strict92×32·paired512를 일괄 강제하지 않는다.

밸런스 기준서의 제목 바로 아래에 새 기록을 넣는 경우에는 merge-base와 head의 실제 Git 본문을 비교한다. 제목과 과거 본문 전체가 바이트 단위로 유지되고 새 절에 중복이나 재사용이 없는 `balance:` ID가 있어야 새 기록으로 분류한다. 기존 기록 변경, 삭제, 제목 변경과 불명확한 추가는 계속 전체 출시 인증 대상이다. 이 구분은 원장이나 보고서 파일의 변경에는 적용하지 않으며, 현재 Unity 입력의 영향 검사와 소스 결합도 유지한다.
