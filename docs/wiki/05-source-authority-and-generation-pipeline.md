# 05. 원천 권위와 생성 파이프라인

## 권위 원칙

위키는 사실을 소유하지 않는다. 현재 C#과 Unity 작성 자산, 승인된 밸런스 기준, 구현 상태 권위, `docs_final`의 핸드북과 생성 인덱스를 읽어 공개 가능한 표현으로 투영한다.

| 정보 | 권위 | 위키의 역할 |
|---|---|---|
| 실제 존재·식별자·직렬화 값 | Unity 작성 자산과 현재 코드 | 검증 후 표시 |
| 승인된 수치와 변경 이유 | 밸런스 기준서와 필수 기록 | 승인된 현재값만 표시 |
| 현재 구현 여부 | 시스템 구현 권위 체크리스트 + 코드 증거 | 미구현 목표안 제외 |
| 시스템 설명 | `docs_final/handbook/` | 플레이어 언어로 편집 |
| 아이템 게임 내 설명 | `docs/game-design/content/item-in-game-descriptions.ko.json` | 런타임 카탈로그와 같은 문장을 표시 |
| 관계·역참조·소비 코드 | content DB와 knowledge base | 공개 관계만 재구성 |
| 공략·예시·소개 | 검수된 curated overlay | 사실 필드와 분리 |

생성 인덱스는 탐색과 관계의 권위이지, 그 자체만으로 구현 완료나 플레이 검증을 증명하지 않는다.

아이템 문장을 바꾼 뒤에는 `python -X utf8 Tools/Documentation/sync_item_narratives.py --sync --check`를 실행한다. 이 검사는 검수 JSON과 `InGameNarrativeTextCatalog.asset`의 1,075개 문장이 하나씩 정확히 일치하는지 확인한다. 위키 생성도 같은 검사를 반복하므로 Unity 쪽 재생성으로 상투 문장이 돌아오면 배포 전에 실패한다.

## 파이프라인

```text
C# + Unity authored assets + approved docs
                  │
                  ▼
docs_final content DB / knowledge base freshness 검증
                  │
                  ▼
Tools/Wiki 추출·정규화 → typed normalized model
                  │
        ┌─────────┴─────────┐
        ▼                   ▼
curated overlay 병합     publication manifest
        └─────────┬─────────┘
                  ▼
스키마·참조·스포일러·누출·결정론 검증
                  │
                  ▼
wiki/.generated/ candidate 엔터티·관계·리디렉션·QA
                  │
                  ▼
candidate 검사 → wiki/game-versions/{game-version}/data/ 스냅샷 고정
                  │
                  ▼
Astro Node renderer build → 관계 graph slice 생성 → 실시간 검색 API·링크/접근성/성능 검사
                  │
                  ▼
renderer image + game-versions content volume
```

## 단계별 계약

### 1. freshness gate

`query_knowledge_base.py --status`와 기존 검증기를 먼저 실행한다. `docs_final`이 원천과 stale이면 위키 생성은 즉시 실패한다. stale 상태에서 이전 생성물을 조용히 재사용하지 않는다.

### 2. 추출과 정규화

- 기존 CSV·JSON을 문자열로 즉석 렌더링하지 않고 typed model로 변환한다.
- 단위, nullable 의미, enum, stable ID, relation direction을 유형별 스키마로 검증한다.
- 콘텐츠 수와 관계 수를 입력 manifest와 대조한다.
- 원천 파일 경로는 내부 provenance에 남기되 공개 projection에는 넣지 않는다.

### 3. publication projection

`wiki/game-versions/{game-version}/content/publication.yml`이 해당 게임 버전의 공개 유형·개별 예외·스포일러 등급을 허용 목록으로 소유한다. manifest에 없거나 상태가 `internal`/`blocked`인 엔터티는 그 게임 버전의 HTML, 검색, relation label 어디에도 들어가지 않는다.

### 4. curated overlay

- overlay는 stable ID 또는 guide ID를 키로 사용한다.
- 수기 파일이 존재하지 않아도 데이터 페이지는 생성 가능하다.
- overlay의 대상이 사라졌거나 유형이 바뀌면 빌드 실패다.
- 금지된 사실 필드 덮어쓰기, 깨진 내부 링크, 미승인 visibility 상승은 실패다.

### 5. 관계와 역참조

정방향 typed edge에서 역방향 edge를 다시 계산한다. 양쪽을 별도 수기 관리하지 않는다. 비공개 대상과 연결된 공개 페이지에서는 대상의 이름조차 숨기거나 승인된 일반 문구로 대체한다.

### 6. URL과 리디렉션

slug registry는 stable ID별 현재 canonical slug와 이전 slug를 소유한다. 중복, Unicode 정규화 충돌, 예약 경로 충돌은 빌드를 막는다. 리디렉션은 체인을 허용하지 않고 항상 최종 canonical로 평탄화한다.

### 7. 생성과 색인

Astro Node renderer가 공개 모델만 요청마다 읽어 HTML을 만든다. 검색 API도 같은 공개 모델과 curated guide·reference 문서를 읽으므로 HTML과 검색 결과가 같은 content volume을 기준으로 갱신된다.

## 생성 중간물

`wiki/.generated/`은 새 candidate를 검증하는 미추적 scratch다. 승인된 결과는 같은 구조로 `wiki/game-versions/{game-version}/data/`에 복사하고 digest를 고정해 과거 표시를 재현한다.

```text
manifest.json
entities/{kind}/{stable-id}.json
guides/{guide-id}.json
relations/forward.json
relations/backlinks.json
graph/manifest.json
graph/nodes/{stable-id}.json
graph/slices/{kind}/{stable-id}.json
navigation/categories.json
navigation/redirects.json
search/aliases.json
qa/publication-report.json
qa/source-provenance.json
qa/excluded-records.json
```

`manifest.json`은 game version, parent game version, generator/schema version, content/system source digest, 입력 파일 수, 출력 엔터티·관계 수, 공개·제외 수, slug registry digest, publication manifest digest를 포함한다. production release manifest는 게임 버전 폴더 digest, Git commit과 artifact SHA-256도 갖는다. 시간값은 결정론 비교 대상에서 분리한다.

`graph/manifest.json`은 graph schema version, 게임 버전, source snapshot digest, 공개 node·edge·slice 수, 관계별 count, spoiler ceiling, slice digest를 가진다. graph node·edge는 기존 normalized relation을 그대로 projection하며, graph가 별도의 게임 사실 권위가 되지 않는다.

게임 버전, source snapshot digest, 위키 배포 release ID는 서로 다른 필드다. 자세한 증가·철회·업데이트 기록 계약은 [버전·업데이트 이력 관리](13-versioning-and-update-history.md)를 따른다.

## 알려진 원천 예외 처리

현재 확인된 미해결 참조, runtime-domain ID, manual review, duplicate typed-ID는 전역적으로 숨기지 않는다.

- 공개 필드에 영향을 주면 해당 엔터티를 `blocked` 처리하고 배포를 실패시킨다.
- 공개 필드와 무관하다는 기계적 근거가 있으면 내부 QA에 남기고 공개 가능하다.
- 단순히 링크를 제거해 오류를 감추지 않는다.
- 예외 허용은 ID, 영향 필드, 근거, 소유자, 만료 조건을 waiver 파일에 기록한다.

## 결정론

같은 게임 버전 폴더, source digest, slug registry, publication manifest, curated content로 두 번 생성했을 때 의미 산출물이 byte-identical해야 한다. 정렬, 소수점, 날짜, Unicode, 줄바꿈을 명시적으로 정규화한다. published 게임 버전 폴더 digest 변화는 원천 변경 여부와 관계없이 실패다.

## AI 작업 프로토콜

AI가 위키를 수정할 때 다음 순서를 강제한다.

1. `AGENT.md`와 이 계획의 README를 읽는다.
2. knowledge-base freshness를 검사하고 query로 관련 stable ID·관계를 좁힌다.
3. 사실 변경이면 원천 권위와 밸런스 절차를 수정한다. 생성 JSON이나 HTML은 직접 고치지 않는다.
4. 게임 업데이트에 따른 설명 변경이면 새 게임 버전 폴더를 먼저 만든 뒤 그 폴더의 허용된 curated overlay만 고친다. 게임 버전 변화 없는 설명 정정은 같은 게임 버전의 errata 기록과 별도 위키 배포 release ID로 추적한다.
5. 관계가 바뀌면 graph projection과 해당 slice의 node·edge·스포일러·결정론 검사를 함께 실행한다.
6. 전체 생성·검증을 실행하고 source digest, 영향 페이지, 예외를 보고한다.

AI가 `.generated`, `dist`, 인덱스 행, published/withdrawn 게임 버전 폴더를 직접 패치하면 CI가 실패해야 한다.
