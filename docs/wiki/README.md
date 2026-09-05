# DungeonStory 플레이어 위키 구축 계획

## 결론

DungeonStory 위키 애플리케이션은 저장소 루트의 `wiki/`에 만든다. `docs_final/`은 위키의 입력 권위로만 읽고, 사이트 코드·수기 편집·빌드 결과를 넣지 않는다.

| 구분 | 확정 위치 | 추적 여부 | 책임 |
|---|---|---|---|
| 위키 애플리케이션 | `wiki/` | 추적 | Astro UI, 템플릿, 스타일, 앱 전용 스크립트 |
| 위키 계획 문서 | `docs/wiki/` | 추적 | 제품·정보·기술·운영 계약 |
| 저장소 통합 도구 | `Tools/Wiki/` | 추적 | 추출, 정규화, 검증, 전체 재생성 진입점 |
| 게임 버전별 문서 | `wiki/game-versions/{game-version}/content/` | 추적 | 해당 게임 버전의 전체 가이드·수기 설명·공개·URL 정책 스냅샷 |
| 게임 버전별 데이터 | `wiki/game-versions/{game-version}/data/` | 추적 | 해당 게임 버전에서 공개한 정규화 엔터티·관계 스냅샷 |
| 정적 미디어 | `wiki/public/media/` | 추적 | 승인된 로고·스크린샷·아이콘·라이선스 기록 |
| 생성 중간물 | `wiki/.generated/` | 미추적 | candidate 생성·검증용 scratch; 승인 시 게임 버전 폴더에 고정 |
| 배포 산출물 | `wiki/dist/` | 미추적 | Node renderer의 server/client bundle |
| 원천 권위 | `docs_final/`, C#, Unity 작성 자산 | 기존 정책 | 구현 사실, 승인 수치, 설계 설명, 생성 인덱스 |

`wiki/`와 `Tools/Wiki/`는 생성되었다. 로컬 Node renderer와 `0.0.1v` snapshot은 구현·검증 중이며, NAS release candidate와 공개 HTTPS 전환 전까지는 `planned` 상태를 유지한다.

## 제품 한 문장

플레이어가 아이템 하나에서 생산법, 필요 시설, 선행 연구, 사용처, 관련 시스템까지 끊김 없이 이동할 수 있는 한국어 우선 게임 위키를 만든다.

- 현재 게임 버전: [`0.0.1v`](GAME_VERSION)
- 업데이트 기록: [`CHANGELOG.md`](CHANGELOG.md)

## 확정 기본안

- 형태: 나무위키의 탐색 밀도만 차용한 독자 디자인의 공식 읽기 전용 위키
- 프레임워크: Astro on-demand rendering, TypeScript, 스키마 검증
- 검색: 현재 공개 projection을 읽는 실시간 검색 API
- 데이터: `docs_final`과 기존 생성 인덱스에서 결정론적으로 투영
- URL: `/entry/{고정된-슬러그}/`, `/guide/{슬러그}/`, `/category/{슬러그}/`
- 관계 시각화: 아이템·제작식·시설·연구를 중심으로 한 version-scoped 그래프, 필터와 semantic 표 fallback
- 게임 버전 열람: header/footer selector로 같은 문서의 `/game-versions/{game-version}/...` 스냅샷 전환
- 배포: 개발 PC/CI에서 Node renderer image를 만들고, Synology DS916+ reverse proxy 뒤의 경량 컨테이너로 운영
- NAS 경로: 공개 문서 volume을 컨테이너에 read-only mount하고 localhost backend를 검증한 뒤, 기존 서비스를 내리고 현재 DDNS hostname의 HTTPS 443 reverse proxy를 위키로 인계
- 대체 호스팅: NAS 장애 시 Node runtime을 지원하는 관리형 호스팅을 임시 mirror 또는 fallback으로 사용 가능
- 편집: V1은 저장소 PR 기반. 공개 사용자의 직접 편집과 계정 시스템은 범위 밖
- 공개: 첫 production부터 인터넷 공개. 검증된 플레이어용 콘텐츠는 전부 포함하고, 스포일러는 경고 후 기본 마스킹·접기
- 디자인: 포스트 아포칼립스 다크 판타지 + RimWorld 계열 시뮬레이션 위키의 실용적 정보 밀도. 전형적인 AI 생성풍 장식은 배제

## 왜 이 위치인가

- `docs_final/`은 재생성 대상이자 문서 권위다. 앱 파일을 넣으면 재생성 경계와 공개 편집 경계가 섞인다.
- `Assets/` 아래에 두면 Unity가 웹 의존성과 수천 개의 생성 파일을 임포트하려 한다.
- `docs/wiki/`는 계획과 계약에 적합하지만 Node 애플리케이션과 빌드 캐시를 담는 위치는 아니다.
- 별도 저장소는 초기부터 원천 데이터와 위키의 버전이 어긋날 가능성을 키운다.
- 루트 `wiki/`는 Unity와 문서를 건드리지 않으면서 같은 커밋의 게임 데이터로 사이트를 재생성할 수 있다.

## 문서 지도

1. [제품 범위와 독자](01-product-scope-and-audiences.md)
2. [정보 구조와 URL](02-information-architecture-and-routes.md)
3. [콘텐츠 모델과 문서 템플릿](03-content-model-and-page-templates.md)
4. [시각·상호작용 디자인](04-visual-and-interaction-design.md)
5. [권위와 생성 파이프라인](05-source-authority-and-generation-pipeline.md)
6. [기술 구조와 저장소 배치](06-technical-architecture-and-repository-layout.md)
7. [검색·링크·탐색](07-search-links-and-discovery.md)
8. [편집·검수·공개 정책](08-editorial-governance-and-publication.md)
9. [테스트·접근성·성능·보안](09-quality-accessibility-performance-security.md)
10. [배포·운영·복구](10-deployment-operations-and-observability.md)
11. [구현 로드맵과 완료 조건](11-implementation-roadmap-and-acceptance.md)
12. [결정 기록·위험·승인 게이트](12-decisions-risks-and-open-gates.md)
13. [버전·업데이트 이력 관리](13-versioning-and-update-history.md)
14. [제작·연구 관계 그래프 경험](14-relation-graph-experience.md)
15. [페이지 구성과 콘텐츠 경험 명세](15-page-composition-and-content-experience.md)
16. [외부 위키 참고 기반 콘텐츠 구조](16-reference-informed-content-architecture.md)
17. [디렉터리 보드와 허브 탐색 명세](17-directory-board-and-hub-navigation.md)

## 소유자 승인 결과

| 항목 | 확정 결정 |
|---|---|
| 공개 범위 | 첫 production 배포부터 인터넷 공개 |
| 443 전환 | 위키 완성·최종 검증 직후 즉시 전환 |
| 실패 복구 | 외부 HTTPS·핵심 페이지·검색·404 중 하나라도 실패하면 15분 안에 기존 backend 복원 |
| 스포일러 | 스토리·연구·엔드게임까지 전부 수록하되 경고 후 기본 마스킹·접기 |
| 미디어 권리 | 게임 로고·대표 이미지·스크린샷의 공식 위키 사용 승인 |
| 디자인 | 포스트 아포칼립스 다크 판타지, 시뮬레이션 위키형 고밀도 정보 설계, AI 생성풍 배제 |

제품·콘텐츠·디자인 결정은 완료됐다. 외부 공개는 구현 완료 외에도 방화벽·자동 차단 정책, 현재 WAN 443 target, reverse-proxy rollback, phpMyAdmin 격리, 인증서 갱신 같은 기술 게이트를 모두 통과해야 한다.

## 구현 상태

- 상태: NAS on-demand renderer·versioned content volume 배포와 공개 HTTPS 전환 완료
- 문서 기준 게임 버전: `0.0.1v` (`planned`)
- 게임 버전 저장: `wiki/game-versions/0.0.1v/` 최초 snapshot과 이후 전체 폴더 복사용 `Tools/Wiki/new_game_version.ps1` 구현
- 공개 투영: 3,475개 원천 레코드 중 2,904개 확인 항목, 3,582개 공개 관계 생성; 수동 검토·중복 ID·미해결 참조·비공개 내부형은 QA 보고서로만 제외 기록
- 페이지 경험: 공통 shell, 홈/전체 디렉터리 보드, 시스템 icon rail, 카테고리 도감, 엔터티 relation 표·필터 그래프, 정적 검색, game-version/current·archive route 구현
- 운영 투명성: `/status/`에서 공개 snapshot 상태·문서/관계 수·검증 digest·권리 고지를 제공하되 원천 경로와 개발용 자료는 노출하지 않음
- 스포일러: 161개 보호 문서는 초기 HTML과 search index에서 숨기고 명시적 열기 뒤 전용 static payload를 읽음
- 로컬 검증: TypeScript/Astro 검사, 결정론 digest, 공개 projection·문서 권위 검증, current/historical on-demand route, 실시간 검색과 스포일러 기본 마스킹을 확인한다. renderer code와 versioned content volume은 별도 release digest로 기록한다.
- 배포 검증: release별 checksum, root-owned staging, localhost current/archive smoke, Synology reverse-proxy authority, 외부 HTTPS current/archive route와 `no-store` 응답 확인
- 남은 운영 보강: DSM 방화벽·자동 차단 정책, container/image 정리 보존 기간, 실제 15분 rollback rehearsal
- 기준일: 2026-09-04
- 데이터 확인: 콘텐츠 3,475건, 관계 6,346건, 지식베이스 링크 오류 0건
- 알려진 원천 예외: 콘텐츠 참조 54건, 런타임 도메인 ID 85건, 수동 검토 49건, 중복 typed-ID 그룹 10건
- 밸런스 분류: `밸런스 영향 없음`

## 외부 기술 근거

- [Astro Content Loader API](https://docs.astro.build/en/reference/content-loader-reference/)
- [Astro 파일·동적 라우팅](https://docs.astro.build/en/basics/astro-pages/)
- [Astro on-demand rendering](https://docs.astro.build/en/guides/on-demand-rendering/)
- 실제 대상은 Synology DS916+ / DSM 7.1.1 / Web Station이며, 구현 단계에서 해당 DSM 세대의 공식 Web Station·reverse proxy·TLS 절차를 버전 고정 근거로 추가한다.
- [GitHub Pages 사용자 정의 워크플로](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages)와 [Cloudflare Pages의 Astro 배포](https://developers.cloudflare.com/pages/framework-guides/deploy-an-astro-site/)는 fallback 검토 자료로만 유지한다.
