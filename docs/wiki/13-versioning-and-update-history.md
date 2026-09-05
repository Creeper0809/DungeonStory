# 13. 게임 버전·업데이트 이력 관리

## 현재 기준

- 현재 게임 버전: `0.0.1v`
- 형식: `{major}.{minor}.{patch}v`
- 허용 정규식: `^[0-9]+\.[0-9]+\.[0-9]+v$`
- 문서 snapshot 상태: `planned`; 실제 public artifact가 배포되면 이 게임 버전 snapshot을 `published`로 승격한다.

`0.0.1v`는 위키나 사이트의 버전이 아니다. 플레이어가 실제로 플레이하는 **게임 릴리스 버전**이다. 정렬과 비교에서는 뒤의 `v`를 제외한 세 숫자를 비교하며, 화면·URL·업데이트 기록에는 `0.0.1v` 전체를 표시한다.

## 서로 다른 식별자

| 식별자 | 예 | 의미 | 변경 조건 |
|---|---|---|---|
| Game version | `0.0.1v` | 플레이어가 보는 게임 릴리스와 그 게임 상태의 문서 기준 | 게임 업데이트가 공개될 때 |
| Source snapshot digest | content/system source digest | 해당 게임 버전 문서를 생성한 코드·자산·승인 문서 상태 | 원천 입력이 달라질 때 |
| Wiki deployment release ID | `0.0.1v-abc123-9f3e` | NAS에 올라간 정확한 위키 artifact | 매 배포 시도 |

이 셋을 하나로 합치지 않는다. 위키 코드·CSS·검색 색인만 고쳐 다시 배포해도 게임 버전은 그대로다. 반대로 게임 업데이트는 새 게임 버전, 새 게임 버전 폴더, update record를 반드시 만든다.

## 게임 버전 증가 규칙

| 증가 | 게임 업데이트의 성격 | 예 |
|---|---|---|
| patch | 버그 수정, 수치·설명 보정, 작은 콘텐츠 조정 | `0.0.1v` → `0.0.2v` |
| minor | 새 시스템, 콘텐츠 영역, 진행 단계 확장 | `0.0.1v` → `0.1.0v` |
| major | 세이브·진행·핵심 규칙의 호환 불가 변경 또는 큰 전환 | `0.1.0v` → `1.0.0v` |

- 새 게임 버전에는 바뀐 게임 사실과 플레이어 영향이 적힌 update record가 필요하다.
- 게임 버전을 건너뛸 수는 있지만 감소·재사용할 수 없다.
- 같은 게임 버전의 위키 코드 재빌드에는 game-version bump를 하지 않고 deployment release ID만 바꾼다.
- 실제 게임 빌드가 바뀌지 않은 문서 오탈자·표현 정정은 게임 버전을 올리지 않는다. append-only errata record와 새 deployment release ID로 추적하며, 이미 고정된 게임 버전 archive는 덮어쓰지 않는다.

## 게임 버전 폴더 모델

모든 플레이어용 문서와 그 문서가 참조하는 정규화 데이터는 **게임 버전별** 자기완결 폴더에 둔다.

```text
wiki/
├─ game-versions/
│  ├─ registry.json
│  ├─ 0.0.1v/
│  │  ├─ game-version.json
│  │  ├─ update.md
│  │  ├─ content/
│  │  │  ├─ guides/
│  │  │  ├─ curated/
│  │  │  ├─ publication.yml
│  │  │  ├─ slug-registry.csv
│  │  │  └─ waivers.yml
│  │  ├─ data/
│  │  │  ├─ manifest.json
│  │  │  ├─ entities/
│  │  │  ├─ graph/
│  │  │  ├─ relations/
│  │  │  ├─ navigation/
│  │  │  └─ search/
│  │  └─ media-manifest.json
│  └─ 0.0.2v/                       # 0.0.1v 전체 복사 후 새 폴더만 수정
└─ errata/
   └─ 0.0.1v/                       # 게임 버전을 바꾸지 않는 공개 문서 정정의 append-only 기록
```

- `content/`는 해당 게임 버전의 모든 수기 문서·공개·URL 정책 snapshot이다.
- `data/`는 해당 게임 버전에서 공개한 정규화 엔터티·관계·graph slice snapshot이다. 게임 구현의 권위는 아니지만 과거 게임 버전의 위키 표시를 재현하기 위해 Git에 추적한다.
- `media-manifest.json`은 그 게임 버전에서 사용한 이미지의 content hash와 설명을 고정한다.
- 실제 미디어 파일은 `wiki/public/media/by-hash/{sha256}.{ext}`에 한 번만 저장한다. 같은 이미지를 게임 버전 폴더마다 복사하지 않는다.
- Astro 컴포넌트와 디자인 shell은 공용이다. 정확한 당시 렌더링 artifact는 NAS의 immutable release에도 보관한다.

`registry.json`은 게임 버전 목록, 상태, parent, 폴더 digest, 현재 공개 게임 버전을 관리한다. [`GAME_VERSION`](GAME_VERSION)은 `registry.current_game_version`과 같아야 한다. `package.json` version은 게임 버전 권위가 아니다.

게임 버전 폴더의 `game-version.json`은 최소 다음을 가진다.

```json
{
  "schema_version": 1,
  "game_version": "0.0.1v",
  "parent_game_version": null,
  "status": "planned",
  "approved_at": "2026-09-02",
  "published_at": null,
  "content_digest": null,
  "source_digests": {}
}
```

## 새 게임 버전 생성: 폴더 복사 후 수정

새 게임 버전은 빈 폴더에서 만들지 않는다. `Tools/Wiki/new_game_version.ps1 -From 0.0.1v -To 0.0.2v`처럼 전용 명령으로 직전 게임 버전 폴더 전체를 복사한다.

명령은 다음을 원자적으로 수행해야 한다.

1. source 게임 버전이 존재하고 `published` 또는 승인된 최초 기준선인지 확인
2. target 형식·단조 증가·미존재 확인
3. source 폴더를 target 임시 폴더로 전체 복사
4. target의 `game-version.json`만 새 게임 버전, parent, `draft`, 빈 공개일로 변경
5. target `update.md`를 새 초안으로 초기화하고 이전 게임 버전 digest를 기록
6. 임시 폴더를 최종 target 이름으로 rename
7. `registry.candidate_game_version`을 갱신하되 `registry.current_game_version`과 [`GAME_VERSION`](GAME_VERSION)은 아직 변경하지 않음

복사 뒤 모든 게임 업데이트 문서·데이터 수정은 target 폴더에서만 한다. 이전 게임 버전 폴더를 고치거나 일부 파일만 새 게임 버전에 symlink하는 방식은 금지한다.

## 불변성과 정정

- `published`·`withdrawn` 게임 버전 폴더는 읽기 전용이다.
- registry에 기록된 폴더 digest와 실제 digest가 다르면 CI를 실패시킨다.
- 새 게임 버전에서 삭제할 문서는 파일을 제거하고 update record에 기록한다. 이전 게임 버전에는 그대로 남는다.
- 게임 버전별 페이지·관계·검색 index는 해당 폴더 안의 자료만 읽는다. current 자료와 섞지 않는다.
- 게임 버전 변화 없는 공개 정정은 `wiki/errata/{game-version}/`에 정정 이유, 영향 문서, 승인일, source digest를 append-only로 기록한다. current route는 해당 정정의 존재를 표시할 수 있지만 archive 원본을 바꾸지 않는다.

폴더 복사는 저장 공간을 사용한다. 빌드는 게임 버전별 파일 수·크기·증가량을 보고한다. parent 대비 비미디어 폴더가 25% 넘게 증가하거나 게임 버전 폴더 안에 승인되지 않은 binary가 들어오면 근거 있는 size waiver 없이는 실패시킨다. `0.0.1v`가 최초 기준 크기를 확정한다. 미디어는 hash 저장소로 중복을 줄이지만 문서와 정규화 데이터의 명시적 복사는 유지한다.

## 업데이트 기록 소스

각 게임 버전의 업데이트 원문은 같은 폴더의 `wiki/game-versions/{game-version}/update.md`다. 한 게임 버전에 하나만 허용하며 다음을 기록한다.

- 게임 버전, parent 게임 버전, status, approved date, published date
- 한 문장 요약과 플레이어 영향
- `added`, `changed`, `fixed`, `data`, `design`, `operations`, `security`
- 새 문서, 변경 문서, 삭제 문서와 자동 계산 count
- known issues와 필요한 migration
- source snapshot digests

[`CHANGELOG.md`](CHANGELOG.md)는 모든 게임 버전의 `update.md`를 최신순으로 요약한 사람용 인덱스다. 수기 요약과 자동 diff를 분리하고, 생성기는 두 게임 버전 폴더의 엔터티·관계·문서 digest를 비교한다. 게임 버전 없는 문서 정정은 같은 게임 버전의 errata로 링크하되 게임 업데이트로 위장하지 않는다.

## 공개 화면과 게임 버전 전환

- 현재 문서: `/entry/{slug}/`, `/guide/{slug}/` 등 current 게임 버전 canonical route
- 과거/명시 게임 버전: `/game-versions/{game-version}/entry/{slug}/`, `/game-versions/{game-version}/guide/{slug}/`
- 게임 버전 홈: `/game-versions/{game-version}/`
- 업데이트: `/updates/`, `/updates/{game-version}/`
- `/changes/`: `/updates/`로 영구 리디렉션

header와 footer에 **게임 버전 선택기**를 둔다. 게임 버전 목록은 root의 작은 `game-version-registry.json`을 읽어 최신 목록을 표시하므로 새 게임 버전 release 때 과거 HTML을 수정하지 않는다. JavaScript가 없어도 `/updates/`의 게임 버전 목록으로 이동할 수 있어야 한다. 게임 버전을 바꾸면 가능한 경우 현재 보고 있던 stable ID/guide ID의 동일 문서로 이동한다. 대상 게임 버전에 문서가 없으면 그 게임 버전 홈에서 “이 게임 버전에는 없는 문서”와 가장 가까운 category/search 링크를 보여 준다.

게임 버전 선택 상태에서는 내부 링크, breadcrumb, 역링크, 관계, 비교, 검색이 모두 선택 게임 버전 안에 머문다. Pagefind index도 게임 버전별로 분리하고 선택한 index만 로드한다. current 검색이 historical 게임 버전 문서를 섞어 보여 주지 않는다.

현재 unversioned route만 sitemap과 검색 엔진 canonical로 삼는다. 과거 게임 버전 route는 공개 열람 가능하지만 `noindex,follow`로 중복 색인을 막고, 동일 문서가 current에 있으면 current canonical을 가리킨다.

## release 절차

1. 게임 업데이트라면 직전 게임 버전 폴더를 새 게임 버전 폴더로 전체 복사한다.
2. 새 폴더에서 문서·정규화 데이터·정책·update record만 수정한다.
3. 두 게임 버전 폴더 diff와 source digests를 검토한다.
4. 새 게임 버전 전체 링크·검색·관계·스포일러·접근성·결정론 검사를 수행한다.
5. candidate 게임 버전의 archive artifact와 unversioned current alias artifact를 build한다.
6. 기존 historical artifact digest가 보존됐는지 확인하고 candidate RC digest를 승인한다.
7. NAS의 immutable `game-version-artifacts/{game-version}/`에 새 archive를 한 번만 배포한다.
8. smoke test 성공 시 새 게임 버전을 `published`로 고정하고 registry/current와 [`GAME_VERSION`](GAME_VERSION)을 원자적으로 전환한다.
9. 이전 게임 버전 route, 동일 문서 게임 버전 전환, 게임 버전별 검색, `/updates/`, footer를 외부에서 확인한다.

게임 버전이 바뀌지 않은 위키 코드·문서 errata 배포는 1~3의 게임 버전 생성 단계를 건너뛴다. 대신 errata record, source digest, deployment release ID를 검증하고 current release만 원자적으로 교체한다.

## rollback과 철회

- rollback하면 공개 footer와 manifest는 실제 서비스 중인 게임 버전을 표시한다.
- rollback 뒤에도 실패한 게임 버전 폴더와 `/updates/{game-version}/` 기록은 보존하되 게임 버전 선택기에서는 `withdrawn` 상태를 표시한다.
- 실패한 새 게임 버전 기록은 삭제하지 않고 `withdrawn`으로 남기며 원인과 대체 게임 버전을 기록한다.
- 철회된 게임 버전 번호는 다시 사용하지 않는다.
- 443 최초 전환 실패 시 `0.0.1v`의 배포 상태를 실패로 기록하고 15분 안에 old backend를 복원한다.

## 자동 차단 규칙

CI와 배포 도구는 다음 경우 실패해야 한다.

- 게임 버전 형식 오류, 감소, 중복 또는 재사용
- `GAME_VERSION`, `registry.current_game_version`, current 폴더명, game-version manifest, update source, 생성 manifest의 게임 버전 불일치
- 게임 업데이트에 game-version bump나 update record가 없음
- update record가 가리키는 source digest와 build manifest 불일치
- published/withdrawn 게임 버전 폴더 digest 변경 또는 이전 게임 버전 직접 수정
- 새 게임 버전이 직전 폴더의 검증된 복사본이 아니거나 parent가 잘못됨
- 게임 버전 내부 링크·검색·관계·graph slice가 다른 게임 버전으로 새어 나감
- 게임 버전 선택기가 같은 문서의 잘못된 stable ID/guide ID로 이동
- 게임 버전별 문서 수·용량이 승인 예산을 비정상적으로 초과
- `planned`·`draft`·`withdrawn` artifact의 production 전환
- footer, `/updates/`, sitemap에 서로 다른 current 게임 버전 노출

## `0.0.1v` 완료 조건

- 실제 사이트와 생성 파이프라인 구현
- `wiki/game-versions/0.0.1v/` 전체 문서·데이터 기준 폴더 생성과 digest 고정
- 게임 버전 선택기와 current/game-versioned route, 게임 버전별 search·relations 검증
- 전체 공개·스포일러·디자인·접근성 acceptance test 통과
- Synology RC, 보안 gate, 443 handover와 15분 rollback rehearsal 통과
- `0.0.1v` 게임 업데이트 기록과 artifact digest 승인
- 외부 smoke test 뒤 상태를 `published`로 변경
