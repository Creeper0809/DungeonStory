# 11. 구현 로드맵과 완료 조건

## 진행 원칙

각 단계는 동작하는 작은 vertical slice와 종료 기준을 가진다. 페이지 수를 먼저 늘리지 않고 권위·공개·URL·검색 계약을 대표 데이터에서 끝낸 뒤 전체 규모로 확장한다.

## Phase 0. 계획 승인

산출물:

- 이 문서 세트 승인
- 외부 공개, 443 전환·15분 복구, 전체 스포일러 공개 방식, 미디어 권리와 디자인 방향 결정
- 현재 게임 버전 `0.0.1v`와 업데이트 기록 계약 승인
- 대표 사용자 질문 30개와 대표 검색 query 50개 초안
- 공개할 첫 콘텐츠 유형과 guide 목록
- 시스템 허브·콘텐츠 도감·개별 문서·관계 도구·게임 버전의 콘텐츠 구조 승인

종료 조건:

- [결정 게이트](12-decisions-risks-and-open-gates.md)의 외부 배포 필수 항목에 소유자 결정이 기록됨
- `wiki/`와 `Tools/Wiki/` 생성 권한 승인

## Phase 1. 기술·데이터 spike

범위:

- `wiki/` Astro 최소 앱과 pinned toolchain
- `Tools/Wiki/` 재생성 진입점
- content/KB freshness gate
- 아이템·시설·생산식·연구에서 각 5~10개 대표 엔터티 정규화
- source manifest와 같은 입력 2회 결정론 검사
- `wiki/game-versions/registry.json`, `wiki/game-versions/0.0.1v/` 전체 기준 폴더와 update source
- 직전 게임 버전 전체를 복사하는 `Tools/Wiki/new_game_version.ps1`
- item·recipe·facility·research typed relation의 graph schema와 대표 graph slice

종료 조건:

- 생성 JSON 수기 수정 없이 대표 4유형 페이지 렌더링
- stale, broken relation, duplicate slug, forbidden field가 의도대로 실패
- published 게임 버전 digest 변경과 게임 버전 간 링크·검색 혼입이 의도대로 실패
- graph node·edge가 관계 표와 다르거나 비공개 대상이 섞이면 의도대로 실패
- NAS의 예정 base path와 hostname 구성을 가정한 로컬 production preview 정상
- Synology Web Station의 전용 document root와 내부 전용 port에서 대표 정적 페이지·상대 경로·404 동작 확인

## Phase 2. publication model

범위:

- publication manifest, spoiler tier, curated overlay, slug registry, waiver schema
- internal/public 별도 artifact
- 공개 필드 allowlist와 leakage scanner
- redirect/alias 생성
- update record schema와 게임 버전/source snapshot/deployment release ID 분리
- 게임 버전 선택기, current/versioned route, 선택 게임 버전 내 링크·관계·검색 격리
- graph projection, node/edge 공개 schema, graph filter·URL·table fallback 계약

종료 조건:

- 차단 fixture가 HTML, sitemap, search input 어디에도 없음
- overlay가 사실 필드를 덮어쓰면 실패
- 이름 변경 fixture가 기존 URL에서 최종 canonical로 한 번에 이동

## Phase 3. 디자인 시스템과 핵심 템플릿

범위:

- 게임 브랜드 자산 감사와 디자인 토큰
- global shell, navigation, search trigger, breadcrumb, TOC, infobox, table, relation list, spoiler, version/errata/empty state
- GraphExplorer: 제작·연구·시설 관계 graph, 방향·유형·깊이 filter, 선택 상세, keyboard/touch, 표 fallback
- 홈, 6개 시스템 허브, 가이드, 아이템, 시설, 생산식, 연구, 검색 대표 화면
- 상위·동급·하위로 이어지는 local navigation과 외부 위키 비복제 검수 기준
- 홈/`/directory/`의 아이콘 디렉터리 보드와 system hub의 icon rail + 링크 인덱스, game-version/spoiler fixture
- mobile/desktop, light/dark, empty/error/loading 상태

종료 조건:

- 실제 데이터로 디자인 승인
- 키보드·200% zoom·contrast·reduced motion 기준 통과
- JavaScript 없이 canonical 본문과 링크 사용 가능
- 아이템·시설·제작식·연구 첫 화면에서 대상·핵심 조건·다음 행동을 확인하고, 360px/768px/1280px 페이지 상태를 통과

## Phase 4. 전체 콘텐츠 투영

범위:

- 73개 원천 유형을 플레이어용 템플릿 family에 매핑
- 전체 공개 허용 엔터티, typed relation, 역링크, 카테고리 생성
- handbook 기반 핵심 시스템 guide 편집
- 대규모 목록의 pagination/virtualization 여부 측정
- 아이템·시설·제작식·연구 4개 page family를 먼저 실제 source fixture로 완결하고, 나머지 family를 공통 블록으로 매핑
- 정착·시설, 생산·물류, 연구·발전, 인물·사회, 생존·의료, 전투·방어/세계·원정/사건의 허브와 도감 분류를 구현
- 각 공개 game-version snapshot에 directory manifest를 생성하고, 모든 보드 target의 canonical·visibility·icon provenance를 검증

종료 조건:

- 모든 공개 엔터티에 canonical URL·title·kind·category 존재
- 공개 broken link와 고아 페이지 0
- 원천 count와 공개/제외/차단 count가 manifest에서 보존 법칙을 만족
- 유형 미매핑 0 또는 명시적 `internal` 분류
- 각 공개 canonical 문서가 시스템 허브 또는 카테고리에서 도달 가능하고, 상위·동급·관계 탐색 경로가 검증됨
- 홈/전체 directory와 대표 system index board의 모든 direct link, spoiler-safe label, 모바일·JavaScript-off 상태가 검증됨

## Phase 5. 검색과 발견성

범위:

- current 공개 projection을 읽는 실시간 검색 API
- alias jump table, filters, result snippets
- production/research 관계 탐색과 compare 표
- graph slice lazy loading, node/edge 예산, 경로 강조, URL restore
- 404 복구, `/updates/`·게임 버전 상세, sitemap/metadata
- 연구·아이템·사건의 domain filter, 안전한 spoiler 검색/자동완성, 게임 버전별 legacy/부재 안내

종료 조건:

- 대표 query top-5 90% 이상
- 비공개 자료 및 스포일러 미허용 상태의 누출 결과 0
- 대표 graph fixture에서 표·graph의 관계 방향/수량/조건 불일치 0, keyboard·모바일·JavaScript fallback 사용 가능
- 검색 API가 배포 base path와 `no-store` 응답에서 정상 동작
- 그래프 없이도 모든 관계를 목록·표로 탐색 가능
- footer·업데이트 목록·상세 페이지의 current 게임 버전이 일치
- 같은 stable ID/guide ID의 게임 버전 전환과 대상 문서 부재 fallback이 정상

## Phase 6. 품질·운영 자동화

범위:

- CI workflow, SFTP 기반 Synology game-version release 배포 도구, link crawler, graph browser test, axe, visual regression, Lighthouse
- dependency/security scan
- artifact leakage scan, game-version/release-note gate, graph projection gate와 release manifest
- 게임 버전 폴더 copy/freeze/digest 검사와 게임 버전별 검색 scope
- rollback rehearsal

종료 조건:

- 깨끗한 checkout에서 단일 명령으로 재생성·검증·build
- CI 2회 artifact의 의미 digest 일치
- 성능 예산과 접근성 차단 기준 통과
- NAS의 이전 deployment release로 원자적으로 복구하는 절차 실제 확인
- 공개 current route와 모든 retained `/game-versions/{game-version}/` archive의 동시 smoke test
- 비관리자 SFTP staging ACL, Web Station release switch, 기존 443 rule export와 위키 backend 전환·복원을 실제 확인

## Phase 7. 공개 전 release candidate

범위:

- 실제 플레이어 시나리오 QA
- LAN/VPN 전용 Web Station HTTPS release candidate QA
- 문장·검색 별칭·카테고리 조정
- 스포일러와 이미지 권리 최종 감사
- 포스트 아포칼립스 다크 판타지 방향과 AI 생성풍 배제 기준의 최종 시각 감사
- 출시 체크리스트와 운영 담당 확정

종료 조건:

- 대표 질문 30개 중 90%를 3회 이하 이동으로 해결
- 릴리스 차단 결함 0
- 승인자가 exact artifact digest를 승인
- `0.0.1v` release note, source digests, known issues를 승인

## Phase 8. 공개 배포

범위:

- NAS production release 승인
- Synology 공개 보안 게이트, 현재 DDNS 인증서, 443 reverse-proxy handover, canonical/sitemap/robots 확인
- 완성 직후 공개 전환, 공개 후 smoke test, 15분 rollback window

종료 조건:

- 주요 URL·검색·404·모바일 확인
- source digests와 배포 commit 기록
- `0.0.1v` 상태·공개일과 public update page 확정
- 실패 판정 시 전환 시작 후 15분 안에 기존 backend 복원
- 후속 결함과 콘텐츠 갱신 절차 인계

## MVP와 V1의 구분

MVP는 내부 프리뷰다. 아이템·시설·생산식·연구와 핵심 guide가 실제 원천에서 생성되고 공개 필터·검색·링크가 검증되는 상태다.

V1은 허용된 전체 콘텐츠, 스포일러, 접근성, 성능, 운영·복구까지 완료되어 외부 독자가 신뢰할 수 있는 상태다. MVP를 공개하면서 V1이라고 부르지 않는다.

## 전체 완료 정의

- 원천과 위키 사실 사이에 수기 숫자 복제 없음
- source freshness와 deterministic generation 자동 검증
- 공개 allowlist·스포일러·내부 정보 누출 검사 통과
- canonical URL, redirect, 검색, 역링크, sitemap 완성
- 시스템 허브 → 목록/검색 → 개별 사실·관계의 대표 탐색 경로와 외부 위키 비복제 검수 통과
- 대표 화면의 desktop/mobile/keyboard 품질 승인
- CI, 배포, rollback, 운영 소유권 확정
- 계획 문서와 실제 구현의 경로·명령·정책 일치

## 구현 시 보고 형식

각 phase 완료 보고에는 다음을 남긴다.

- 변경 파일과 생성 파일 경계
- source digests와 schema/generator version
- 페이지·관계·공개·제외·차단 count
- 실행한 검증과 결과
- 남은 예외와 waiver
- 시각 증거 또는 preview artifact
- 밸런스 영향 분류
