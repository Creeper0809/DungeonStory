# V25 8,000건 사람 검수 안내

검수 대상은 `Artifacts/Training/V25/review`의 CSV 8개이며 각 파일은 1,000건이다. CSV는 교환용 원본이므로 직접 편집하는 대신 로컬 검수 UI를 사용한다.

## 로컬 검수 UI 실행

`tools/v25_narrative_training/start_reviewer.cmd`를 실행하거나 프로젝트 루트에서 다음 명령을 사용한다.

```powershell
python tools/v25_narrative_training/reviewer/server.py --open
```

- 서버는 `127.0.0.1`의 임의 포트에만 바인딩하며 외부 서비스로 데이터를 전송하지 않는다.
- 판정, 메모와 수정 초안은 `Artifacts/Review/V25/reviewer_state.json`에 원자적으로 자동 저장된다. 데이터셋을 다시 생성해도 이 파일은 삭제되지 않는다.
- 원본 CSV 8개는 변경하지 않는다.
- `1`은 A 승인, `2`는 B 승인, `R`은 수정, `D`는 폐기, `S`는 건너뛰기, `U`는 직전 판정 되돌리기다.
- 상투어, 잘못된 F/M 참조, JSON 오류, A/B 규칙 필드 차이와 명확한 조사 오류는 자동 강조된다. 경고는 보조 정보이며 자동 승인으로 계산하지 않는다.
- 프로필·문화·분할·검수 상태·경고 유형·유사 문장군으로 필터링할 수 있다.
- 현재 화면 일괄 처리는 최대 20건이며 화면에 표시된 `APPLY N` 확인 문구가 필요하다.

검수 결과는 UI의 `CSV 내보내기`로 `Artifacts/Review/V25/reviewer_export.csv`에 기록한다.

## 입력할 열

- `verdict`: `APPROVE`, `REWRITE`, `DROP` 중 하나
- `selected_candidate`: `APPROVE`일 때 `A` 또는 `B`
- `rewrite`: `REWRITE`일 때 정적 프로필 스키마를 지키는 완전한 JSON
- `issue_tags`: 쉼표 구분. 권장값은 `FACT`, `VOICE`, `GRAMMAR`, `CLICHE`, `DUPLICATE`, `FORMAT`, `SECRET`, `MECHANIC`
- `reviewer_note`: 판단 근거 또는 수정 메모

## 판단 순서

1. 후보가 `Fxx` 사실 밖의 인물·관계·사건을 만들지 않았는지 본다.
2. 나이, 종족, 출신, 특성, 경력, 관계를 뒤집지 않았는지 본다.
3. `Mxx` 모티프가 억지 나열이 아니라 인물의 선택과 연결되는지 본다.
4. 두 후보 중 더 구체적이고 자연스러운 쪽을 고른다.
5. 둘 다 나쁘지만 고칠 가치가 있으면 JSON 전체를 `rewrite`에 작성한다.
6. 사실 패킷 자체가 부자연스럽거나 학습에 해로우면 `DROP`한다.

평가용 `held_out` 2,000건도 같은 기준으로 검수하지만 학습 파일에는 합치지 않는다. UI에서 CSV를 내보낸 뒤 다음 명령으로 병합한다.

```powershell
python tools/v25_narrative_training/apply_human_review.py --review-csv Artifacts/Review/V25/reviewer_export.csv
```

중간 진행을 점검하려면 `--allow-partial`을 붙인다. 스크립트는 존재하지 않는 참조, 변경된 규칙 필드, 잘못된 JSON을 거부한다. 격리 평가 2,000건은 내보내기에는 포함되지만 학습 파일에는 자동 혼합되지 않는다.
