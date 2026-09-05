# 06. 기술 구조와 저장소 배치

## 선택

Astro와 TypeScript를 사용한다. Node adapter의 on-demand rendering으로 문서 요청마다 현재 게임 버전 projection을 읽어 HTML을 만들고, 표 필터·큰 관계 탐색처럼 필요한 부분만 작은 client script로 제공한다.

## 선택 이유

- 3천 개가 넘는 엔터티를 매 수정마다 HTML로 다시 생성하지 않는다. 문서와 공개 모델을 고친 뒤 다음 요청에서 바로 보이게 한다.
- Astro는 server rendering, schema 검증과 낮은 기본 JavaScript 비용을 함께 제공한다. React를 추가해도 이 데이터 반영 문제는 해결하지 못하므로 React runtime은 넣지 않는다.
- 템플릿형 문서 프레임워크보다 독자적인 3열 레이아웃, typed infobox, 생산·연구 관계를 구현하기 쉽다.
- Synology reverse proxy 뒤의 작은 Node 컨테이너가 HTML을 렌더링한다. 문서·데이터는 read-only volume으로 mount하므로 컨테이너를 다시 만들지 않아도 반영된다.

## 채택하지 않은 기본안

| 대안 | 보류 이유 |
|---|---|
| Starlight | 검색과 문서 기능은 좋지만 엔터티 중심 레이아웃을 위해 핵심 shell을 크게 override해야 함 |
| Docusaurus | React runtime과 문서 플러그인 중심 구조가 정적 데이터 투영에 비해 무거움 |
| Next.js | V1에 서버·API·React 앱 기능이 필요하지 않음 |
| MkDocs | 가이드 문서에는 적합하지만 typed relation과 맞춤 인포박스 구현 경계가 약함 |
| 순수 HTML 생성 | 장기 템플릿·컴포넌트·접근성 유지 비용이 큼 |
| 별도 저장소 | 게임 원천과 source digest가 쉽게 분리되고 동시 변경 검증이 어려움 |

## 제안 저장소 구조

```text
DungeonStory/
├─ Tools/Wiki/
│  ├─ generate_wiki_model.py
│  ├─ validate_wiki_model.py
│  ├─ verify_wiki_determinism.py
│  ├─ audit_publication.py
│  └─ rebuild_wiki.ps1
├─ docs/wiki/                         # 현재 계획·운영 계약
└─ wiki/
   ├─ package.json
   ├─ package-lock.json
   ├─ astro.config.mjs
   ├─ tsconfig.json
   ├─ game-versions/
   │  ├─ registry.json
   │  └─ 0.0.1v/
   │     ├─ game-version.json
   │     ├─ update.md
   │     ├─ content/
   │     │  ├─ guides/
   │     │  ├─ curated/
   │     │  ├─ publication.yml
   │     │  ├─ slug-registry.csv
   │     │  └─ waivers.yml
   │     ├─ data/
   │     │  ├─ manifest.json
   │     │  ├─ entities/
   │     │  ├─ graph/
   │     │  ├─ relations/
   │     │  ├─ navigation/
   │     │  └─ search/
   │     └─ media-manifest.json
   ├─ public/
   │  ├─ media/by-hash/
   │  ├─ icons/
   │  └─ robots.txt
   ├─ src/
   │  ├─ components/
   │  │  ├─ GraphExplorer.astro
   │  │  └─ graph-explorer.ts
   │  ├─ layouts/
   │  ├─ pages/
   │  ├─ styles/
   │  ├─ lib/
   │  ├─ schemas/
   │  └─ i18n/
   ├─ tests/
   ├─ .generated/                    # ignored
   └─ dist/                          # ignored
```

Node와 npm 버전은 구현 시 선택한 Astro 버전의 공식 지원 범위 안에서 정확히 pin한다. 전역 설치에 의존하지 않고 lockfile과 `npm ci`를 CI 기준으로 사용한다.

`wiki/game-versions/registry.json`은 기계가 읽는 게임 버전 목록·현재 공개 게임 버전 권위이고, 각 게임 버전 폴더의 `game-version.json`이 자기 상태·parent·digest·source snapshot을 소유한다. 계획 기준 파일인 [`GAME_VERSION`](GAME_VERSION), `registry.current_game_version`, current 폴더명이 다르면 CI를 실패시킨다. `package.json` version과 source snapshot digest는 이를 대신하지 않는다.

새 게임 버전은 `Tools/Wiki/new_game_version.ps1`로 직전 게임 버전 폴더 전체를 복사해 만든다. UI 코드와 content-addressed media blob은 공용이지만 모든 플레이어 문서, 공개 정책, slug registry, 정규화 데이터는 게임 버전 폴더 안에 자기완결로 고정한다.

## 모듈 경계

- Python 통합 도구: Unity YAML/기존 인덱스/문서의 정규화와 저장소 단위 검증
- TypeScript schema: 생성 JSON과 UI가 기대하는 계약 검증
- Astro page/layout: 요청 parameter와 versioned projection을 읽어 semantic HTML 조립
- framework-free client script: 실시간 검색 API, 표 filter, 목차, mobile drawer
- `GraphExplorer`: version-scoped graph slice를 늦게 읽고 node 선택·필터·keyboard·표 fallback을 연결하는 framework-free client script
- 선택적 island: 관계 그래프가 실제 사용성 검증을 통과할 때만 추가

React, Vue 같은 UI 런타임은 V1 기본 의존성에 넣지 않는다. 한 기능이 필요로 할 때 번들·접근성 비용을 측정한 ADR 후 추가한다.

## 데이터 로딩

요청 처리기는 `wiki/game-versions/registry.json`과 각 `wiki/game-versions/{game-version}/data/manifest.json`을 검사한 뒤 엔터티를 읽는다. 가이드와 참조 JSON은 매 요청 새로 읽고, 엔터티·분류·역링크 cache는 원본 파일의 수정 시각과 크기로 즉시 무효화한다. candidate 생성 중에는 `wiki/.generated/`를 쓰지만 public runtime은 고정된 게임 버전 snapshot만 읽는다. 앱이 `docs_final` CSV 형식에 직접 결합하지 않도록 normalized model을 유일한 UI 입력으로 둔다.

loader는 다음을 실패 처리한다.

- manifest/source digest 누락 또는 stale
- 알 수 없는 schema version
- 중복 stable ID, slug, canonical URL
- 깨진 공개 relation
- 금지된 visibility 또는 source path 노출
- overlay가 가리키는 대상 없음
- 현재 게임 버전 불일치, 중복 update record, 게임 업데이트 release note 없는 변경
- published/withdrawn 게임 버전 폴더 digest 변경
- 게임 버전 내부 링크·검색·관계의 다른 게임 버전 혼입
- graph manifest/slice의 source digest·게임 버전 불일치, 숨겨진 node·edge 누출, relation 표와 방향·수량·count 불일치

## 렌더링 원칙

- 모든 canonical 정보와 내부 링크는 JavaScript 없이 읽고 이동할 수 있어야 한다.
- 필터 전 원본 표는 semantic HTML에 존재한다.
- 스포일러 차단 콘텐츠는 CSS로 가리는 대신 projection 단계에서 제외하거나 명시적 공개 페이지 안의 허용된 접기 블록으로만 렌더링한다.
- 인포박스, 표, breadcrumb, 목차는 유형마다 복제하지 않고 공통 컴포넌트에 schema-driven variant를 둔다.
- graph는 static relation 표 뒤에 늦게 로드한다. JavaScript, canvas, WebGL이 없거나 node 예산을 넘겨도 표·canonical 링크·스포일러 정책은 같은 결과여야 한다.

## 구성

- 배포 base URL과 site URL은 환경 구성으로 전달한다.
- 비밀값이 필요한 기능은 V1에 없다.
- development build는 내부 provenance와 품질 배지를 볼 수 있고 public build는 compile-time flag로 해당 코드를 포함하지 않는다.
- 공개·내부 모드를 런타임 URL query로 전환하지 않는다.

NAS 공급사에 종속된 경로, 계정, 포트, 인증서는 앱 설정에 넣지 않는다. `dist` artifact와 배포 transport를 분리하고, 실제 NAS 값은 저장소에 커밋하지 않는 배포 환경 구성에서 주입한다.

## 확인된 Synology 배포 프로필

- 대상: Synology DS916+, DSM 7.1.1-42962 Update 9, Intel 4-core CPU, RAM 2GB
- 주 호스팅: DSM reverse proxy가 NAS 내부의 작은 Node 컨테이너로 연결한다. PHP와 MariaDB는 위키에 사용하지 않는다.
- 빌드 위치: 개발 PC 또는 CI. NAS에서는 게임 모델을 생성하지 않으며, 검증된 Node 이미지와 versioned public data만 실행한다.
- portal: 컨테이너는 `127.0.0.1:4321`만 열고, HTTPS 443 reverse proxy를 통해서만 공개한다.
- 공개 전환: 소유자가 기존 443 서비스 종료를 승인했다. 기존 reverse-proxy rule을 백업한 뒤 현재 DDNS hostname:443의 backend를 위키 portal로 교체하고, 현재 hostname 인증서를 재사용한다.
- rollback: 전환 실패 시 이전 reverse-proxy rule과 backend를 복원한다. old rule 백업과 위키 내부 URL smoke test 없이는 443을 전환하지 않는다.
- runtime: `wiki/Dockerfile`과 `docker-compose.live.yml`의 Node standalone 컨테이너를 사용한다. 2GB RAM 환경에서 DB, 상시 model build container, React runtime은 채택하지 않는다.
- 배포 transport: 전용 비관리자 계정의 SFTP와 격리된 staging share를 우선 검증한다. 관리자 전용 SSH는 기본 경로로 쓰지 않고, SMB는 LAN 수동 fallback으로만 둔다.

Web Station document root, 내부 port, 계정명, 인증서 식별자는 구현 시 비밀이 아닌 환경별 배포 설정과 운영자 보관 설정으로 분리한다. 공개 endpoint는 HTTPS 443으로 고정하되 실제 hostname과 내부 값을 문서나 저장소에 하드코딩하지 않는다.

## Git 경계

추적: 앱 소스, lockfile, 게임 버전 registry, 모든 게임 버전 폴더의 문서·정규화 데이터·update record·manifest, content-addressed 승인 미디어, 테스트.

미추적: `node_modules`, `.astro`, candidate `.generated`, `dist`, 생성 Pagefind 산출물, 로컬 Lighthouse/Playwright 결과. 게임 버전별 source snapshot은 추적하지만 빌드 artifact는 NAS immutable release와 CI 보존 정책으로 관리한다.

## 확장 경계

향후 계산기·지도·세이브 분석이 필요하면 정적 위키와 별도 route/module로 추가한다. 현재 normalized model을 재사용하되 V1의 정적 배포와 공개 정책을 깨는 기능은 별도 ADR과 위협 모델을 통과해야 한다.
