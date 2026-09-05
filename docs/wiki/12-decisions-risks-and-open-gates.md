# 12. 결정 기록·위험·승인 게이트

## 결정 기록

| ID | 결정 | 상태 | 이유 |
|---|---|---|---|
| WIKI-001 | 앱은 `wiki/`에 둔다 | 확정 | Unity, 문서 권위, 앱 의존성 경계 분리 |
| WIKI-002 | 계획은 `docs/wiki/`에 둔다 | 확정 | 구현 전 계약을 영구 추적 |
| WIKI-003 | `docs_final/`은 read-only input이다 | 확정 | 생성 권위와 공개 표현의 충돌 방지 |
| WIKI-004 | 통합 generator는 `Tools/Wiki/`에 둔다 | 확정 | 저장소 단위 검증과 기존 도구 관례 유지 |
| WIKI-005 | Astro on-demand rendering + TypeScript를 쓴다 | 확정 | 문서 수정 뒤 HTML 재생성 없이 다음 요청에 반영 |
| WIKI-006 | 현재 공개 projection을 읽는 검색 API를 쓴다 | 확정 | 정적 검색 색인 재생성 없이 최신 공개 문서를 검색 |
| WIKI-007 | URL은 stable slug registry로 유지한다 | 확정 | 한국어 이름 변경에도 링크 보존 |
| WIKI-008 | 수기 overlay는 설명만 소유한다 | 확정 | 수치·조건의 이중 권위 방지 |
| WIKI-009 | 공개는 allowlist/fail-closed다 | 확정 | 내부·스포일러·미구현 누출 방지 |
| WIKI-010 | V1은 계정·직접 편집·서버 기능이 없다 | 확정 | 범위·보안·운영 단순화 |
| WIKI-011 | 소유자의 NAS를 기본 production host로 둔다 | 확정 | 공개 범위·접근 제어와 versioned content volume을 통제 |
| WIKI-012 | Node runtime을 지원하는 관리형 호스팅은 비상 mirror 후보로 둔다 | 조건부 | on-demand rendering과 공개 projection 경계를 같이 유지 |
| WIKI-013 | 외부 위키는 원리만 참고하고 복제하지 않는다 | 확정 | 독자성·저작권·유지보수 |
| WIKI-014 | DS916+ reverse proxy 뒤 Node standalone 컨테이너를 주 production runtime으로 쓴다 | 확정 | 문서는 mount로 즉시 반영하고 PHP/DB 없이 운영 |
| WIKI-015 | model 생성·검증과 컨테이너 image build는 개발 PC 또는 CI에서만 수행한다 | 확정 | NAS RAM 2GB와 production/toolchain 분리 |
| WIKI-016 | 기존 443 서비스를 종료하고 현재 DDNS hostname:443을 위키로 인계한다 | 확정 | 소유자 승인, 표준 HTTPS URL과 기존 인증서 재사용, 새 WAN port 불필요 |
| WIKI-017 | 배포는 비관리자 SFTP staging을 우선하고 관리자 SSH를 기본안에서 제외한다 | 기준안 | DSM shell 권한 제약과 최소 권한 원칙 |
| WIKI-018 | 첫 production release부터 외부 공개한다 | 확정 | 소유자 결정; LAN/VPN RC는 필수 사전 검증으로 유지 |
| WIKI-019 | 위키 완성·최종 검증 직후 443을 전환하고 실패 시 15분 안에 old backend를 복원한다 | 확정 | 소유자 결정; 무기한 진단보다 빠른 서비스 복구 우선 |
| WIKI-020 | 검증된 progression·narrative·endgame을 모두 공개하되 경고 후 기본 마스킹한다 | 확정 | 완전한 정보 제공과 독자의 스포일러 선택권 병행 |
| WIKI-021 | 게임 로고·대표 이미지·스크린샷을 공식 위키에 사용한다 | 확정 | 소유자가 사용 권리 확인; asset provenance는 계속 기록 |
| WIKI-022 | 포스트 아포칼립스 다크 판타지와 simulation-wiki 정보 밀도를 채택하고 AI 생성풍을 배제한다 | 확정 | 게임 분위기와 실용적 탐색을 우선 |
| WIKI-023 | 현재 게임 버전은 `0.0.1v`, 형식은 `{major}.{minor}.{patch}v`로 한다 | 확정 | 소유자 지정 표기를 유지하고 기계 비교는 숫자 세 부분으로 수행 |
| WIKI-024 | 게임 버전, source snapshot digest, 위키 deployment release ID를 분리한다 | 확정 | 게임 릴리스·원천·배포 artifact의 변경 원인을 혼동하지 않음 |
| WIKI-025 | 게임 내용·공개 사실이 바뀌는 게임 업데이트는 게임 버전 증가와 update record를 갖고 철회된 번호를 재사용하지 않는다 | 확정 | 게임 업데이트 이력과 rollback 신뢰성 보존 |
| WIKI-026 | 모든 플레이어 문서·정규화 데이터는 `wiki/game-versions/{game-version}/` 전체 폴더 snapshot으로 관리한다 | 확정 | 게임 버전 선택 시 당시 문서와 관계를 그대로 열람 |
| WIKI-027 | 새 게임 버전은 직전 게임 버전 폴더 전체를 복사한 뒤 새 폴더만 수정한다 | 확정 | 과거 게임 버전 불변성과 변경 diff 명확화 |
| WIKI-028 | current route와 `/game-versions/{game-version}/` archive를 함께 제공하고 게임 버전별 검색·링크를 격리한다 | 확정 | 동일 문서의 게임 버전별 탐색과 중복 검색 방지 |
| WIKI-029 | 사이트 코드만 바꾸거나 같은 게임 버전 문서를 정정할 때는 게임 버전을 올리지 않고 errata와 deployment release ID로 추적한다 | 확정 | 게임 릴리스 버전과 위키 운영 배포를 섞지 않음 |
| WIKI-030 | 제작·연구·시설 관계는 version-scoped graph slice로 시각화하고 semantic 표를 항상 함께 제공한다 | 확정 | 관계 탐색을 빠르게 하되 그래프 미지원 환경과 접근성을 보장 |
| WIKI-031 | GraphExplorer는 Astro 표준 client script와 Cytoscape.js 후보를 사용해 늦게 로드하며, V1은 작은 중심 관계망과 기본 레이아웃만 제공한다 | 기준안 | framework runtime·전체 그래프·지속 물리 animation을 피하고 성능을 통제 |
| WIKI-032 | 모든 콘텐츠 페이지는 `대상 확인 → 핵심 사실 → 획득/요구 → 사용/영향 → 설명 → 관계 → 이력` 공통 순서를 따르고, 유형별 facts만 바꾼다 | 확정 | 페이지마다 탐색 방법이 달라지는 것을 막고 첫 화면의 실용 정보를 보장 |
| WIKI-033 | 콘텐츠는 `홈 → 시스템 허브 → 카테고리/목록 → canonical 엔터티 → 관계·가이드·게임 버전`으로 탐색하며, 나무위키 게임 문서에서는 정보 계층만 참고한다 | 확정 | 도감과 가이드를 구분하면서도 플레이어가 상위·동급·하위 관계를 잃지 않게 하고, 외부 위키 복제를 방지 |
| WIKI-034 | 홈/전체 둘러보기는 icon grid `DirectoryBoard`, 깊은 시스템 허브는 icon rail + 링크 행 `SystemIndexBoard`를 사용한다 | 확정 | 사용자가 원하는 한눈에 훑는 관련 문서 구조를 제공하되, 레퍼런스의 외형·문장·자산을 복제하지 않음 |

## 소유자 승인 게이트

| Gate | 결정 | 상태 | 남은 조건 |
|---|---|---|---|
| G1 공개 범위 | 첫 production부터 인터넷 공개 | 소유자 승인 | 기술 release gate 통과 |
| G2 NAS 공개 보안 | router mapping, 방화벽/자동 차단, phpMyAdmin 격리, package update | 미완료 | 완료 전 외부 공개 금지 |
| G3 canonical host | 현재 DDNS hostname:443 사용; custom domain은 후속 ADR | 소유자 승인 | 443 handover rehearsal |
| G4 스포일러 | 검증된 전 tier 공개, 경고 후 기본 마스킹·접기 | 소유자 승인 | 검색·metadata·접근성 누출 검사 |
| G5 브랜드 | 게임 로고·대표 이미지·스크린샷 사용, 지정 디자인 톤 적용 | 소유자 승인 | asset별 provenance와 visual QA |
| G6 라이선스 | 별도 재사용 허가 전까지 all rights reserved 안내 | 기본 확정 | 외부 자산은 개별 권리 확인 |
| G7 기여 | issue/PR 링크 | 기준안 | 수정 UI 제외 |

소유자 판단이 필요했던 G1·G3·G4·G5는 해결됐다. G2의 기술 보안 검증과 G6의 실제 표시, 각 구현 acceptance test가 production 공개 전 필수다.

## 주요 위험

| 위험 | 가능성/영향 | 완화 | 차단 조건 |
|---|---|---|---|
| `docs_final`과 원천 불일치 | 중/매우 큼 | freshness gate, source digest | stale이면 생성 중단 |
| 내부·스포일러 누출 | 중/매우 큼 | allowlist, 별도 artifact, 문자열 검사 | 금지 fixture 한 건이라도 노출 |
| 생성 인덱스의 미해결 참조 | 높음/큼 | 영향 필드별 block/waiver | 공개 관계 정확성 불명 |
| 목표 설계를 현재 기능으로 오인 | 중/큼 | 구현 상태 권위 대조 | current 증거 없음 |
| 한국어 이름 변경으로 URL 파손 | 중/중 | slug registry, alias, redirect | registry 충돌 |
| 수기 공략이 수치와 어긋남 | 중/큼 | 사실 필드 overlay 금지 | 숫자 복제 탐지 |
| 3천+ 페이지 빌드·검색 비대화 | 중/중 | 정적 분할, 예산, 10k fixture | CI/성능 예산 초과 |
| 관계 그래프 접근성 저하 | 중/중 | 표 대안, 선택적 island | 대체 탐색 불가 |
| 미디어 출처·범위 누락 | 낮음/큼 | 소유자 사용 승인 + asset별 provenance manifest | 외부/제3자 자산의 근거 없음 |
| AI 생성풍으로 게임 정체성 약화 | 중/중 | 금지 시각 문법, 실제 게임 자산 우선, 대표 화면 시각 감사 | 정보보다 장식이 우세하거나 화풍 불일치 |
| 기존 대규모 작업과 충돌 | 높음/중 | `wiki/`, `Tools/Wiki/`, `docs/wiki/`로 격리 | 권위/활성 계획 파일 충돌 |
| 호스팅 종속 | 낮음/중 | host-neutral `dist`, 두 후보 | host 전용 runtime 요구 |
| NAS 관리면 노출 | 낮음/매우 큼 | 관리 UI/SMB 비공개, 전용 hostname·reverse proxy | 관리 서비스 WAN 노출 |
| HTTPS 443 인계 실패 | 중/큼 | 기존 rule export, 내부 RC, maintenance window, old backend 복원 | 백업 없는 rule 삭제 또는 rollback 실패 |
| NAS edge 보호 미설정 | 높음/매우 큼 | firewall/router allowlist, 자동 차단 또는 동등 gateway 보호 | 공개 시 방화벽·brute-force 정책 부재 |
| phpMyAdmin 경로 동시 노출 | 중/매우 큼 | wiki portal/hostname 격리와 외부 probe | 공개 endpoint에서 접근 가능 |
| 인증서 이름·갱신 실패 | 중/큼 | 이름 일치 인증서, 만료 감시, 갱신 rehearsal | 경고 또는 만료 임박 |
| NAS 단일 장애점 | 중/큼 | versioned release, snapshot, UPS, 선택적 mirror | 복구·백업 경로 없음 |
| NAS 전용 백업 미구성 | 중/큼 | 재현 가능한 artifact + Web Station/TLS/metadata 별도 백업과 복원 시험 | 시스템 설정 백업만 있고 사이트 복원 경로 없음 |
| 배포 중 불완전 파일 노출 | 중/큼 | staging + checksum + atomic switch | live 디렉터리 직접 덮어쓰기 |
| 의존성 공급망 | 중/중 | 최소 의존성, lockfile, audit | 심각 취약점 미해결 |
| 게임 버전과 실제 artifact 불일치 | 중/큼 | 단일 manifest, release-note gate, footer 외부 확인 | GAME_VERSION·manifest·update page 불일치 |
| 철회 게임 버전 재사용·기록 손실 | 낮음/큼 | immutable update record, `withdrawn` 상태, 단조 증가 검사 | 기존 release note 덮어쓰기 |
| 게임 버전 폴더 복사로 저장소 급증 | 높음/중 | 게임 버전별 크기 report, content-addressed media, 증가 예산 | 예상 밖 대용량 binary·데이터 중복 |
| 과거 게임 버전 오염 | 중/큼 | published folder digest 고정, 직접 수정 차단 | historical 문서·관계 변경 |
| 게임 버전 간 탐색 혼입 | 중/큼 | game-version-scoped route/link/search/relation test | 과거 페이지에서 current 결과 노출 |
| 관계 그래프 과밀·오해 | 높음/중 | 중심 node, depth·node 예산, 방향·유형 filter, 표 fallback | 전체 그래프 기본 노출 또는 조용한 node 절단 |
| graph 공개 누출 | 중/매우 큼 | publication projection, graph JSON/canvas/Pagefind leakage scan | 차단·미허용 스포일러 node/edge/label 노출 |
| graph와 표 불일치 | 중/큼 | typed relation 단일 projection, count/direction/unit diff gate | graph 전용 수치·방향 또는 누락 edge |
| 페이지 정보 과밀·중요 사실 은닉 | 중/중 | 공통 블록 순서, 빈 블록 숨김, 관계 그래프의 작은 미리보기, 3개 화면폭 QA | 첫 화면에 목적·핵심 조건이 없거나 mobile에서 표/목차가 겹침 |
| 도감·가이드·버전 문서의 탐색 단절 또는 외부 위키 복제 | 중/중 | 허브→목록→엔터티 도달성 검사, local navigation 예산, 독자 문장·UI 리뷰 | canonical 고아, 긴 제목 트리, 외부 문장·표·이미지·스킨 재사용 |
| 디렉터리 보드가 링크 나열·아이콘 장식·버전 혼입으로 변질 | 중/중 | version-scoped directory manifest, icon provenance, direct-link/keyboard/mobile/spoiler test | 대상 없는 cell, 아이콘 단독 링크, 다른 snapshot 대상, 미허용 spoiler label 노출 |

## 현재 데이터 위험 기준선

계획 작성 시점의 생성 인덱스는 fresh지만 다음 예외가 있다.

- unresolved content references: 54
- unresolved runtime-domain IDs: 85
- manual-review rows: 49
- duplicate typed-ID groups: 10
- knowledge-base broken links: 0

이 숫자는 오류를 자동 허용하는 예산이 아니다. 공개 projection에 미치는 영향을 ID별로 판정하는 시작점이다.

## 구현 중 새 ADR이 필요한 경우

- Astro 외 framework 또는 Node runtime 계약을 바꾸는 SSR 도입
- React/Vue 등 client runtime 도입
- 외부 검색 SaaS 도입
- 별도 저장소로 이동
- URL namespace 또는 stable slug 정책 변경
- 사용자 계정·편집·댓글·analytics 도입
- 공개 권위 순서나 overlay 허용 필드 변경
- NAS 웹 서버 방식 또는 관리형 fallback의 근본 변경

ADR에는 문제, 선택지, 실측, 보안·운영 비용, migration/rollback을 기록한다.

## 구현 시작 체크리스트

- [x] G1·G3·G4·G5 소유자 결정과 G6 기본 권리 표시 기록
- [ ] 현재 활성 V27 작업과 파일 경계 재확인
- [ ] knowledge-base freshness와 source digests 재확인
- [ ] `wiki/`, `Tools/Wiki/` 하위 AGENTS 지침 필요 여부 결정
- [x] NAS 모델·OS, Web Station/Nginx, reverse proxy/TLS, SFTP, 저장 공간의 read-only capability audit
- [ ] 현재 WAN 443 target, 방화벽/자동 차단, phpMyAdmin 격리 검증
- [ ] 전용 document root, 내부 portal port, SFTP ACL, atomic/blue-green switch 실증
- [ ] 기존 443 rule export와 위키 handover/old-backend rollback 리허설
- [ ] Node/npm/Astro 버전 공식 지원 범위 확인과 pin
- [x] 게임 버전 `0.0.1v`와 update-history 계약 기록
- [ ] game-version registry, `wiki/game-versions/0.0.1v/`, update schema, 게임 업데이트 release-note CI gate 구현
- [ ] game-version copy tool, published-folder digest freeze, 게임 버전 선택기와 게임 버전별 search 구현
- [x] 제작·연구·시설 graph UX, 필터·접근성·성능·fallback 계약 기록
- [x] 공통 shell과 콘텐츠·가이드·탐색·상태 페이지의 세부 구성 계약 기록
- [x] 외부 위키 게임 문서 참고 기반의 시스템 허브·도감·엔터티·업데이트 콘텐츠 구조와 비복제 기준 기록
- [x] 사용자 제시 디렉터리 grid와 system index 구조를 DungeonStory 전용 보드·manifest·접근성·게임 버전 계약으로 기록
- [ ] graph projection generator, 대표 fixture, GraphExplorer와 graph QA·성능 gate 구현
- [ ] 대표 4유형 fixture와 금지 fixture 확정
- [ ] publication/slug/overlay schema 리뷰
- [ ] 첫 vertical slice 종료 조건 합의

## 이 계획의 변경 규칙

구현이 계획과 달라지면 코드를 정답으로 두고 문서를 사후 수정하지 않는다. 먼저 이 결정표 또는 별도 ADR에서 차이를 승인하고, 계획·테스트·구현을 같은 변경으로 갱신한다.
