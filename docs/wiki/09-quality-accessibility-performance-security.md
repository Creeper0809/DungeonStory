# 09. 테스트·접근성·성능·보안

## 품질 전략

검증은 `원천 → 정규화 모델 → 공개 projection → HTML → 배포 artifact`의 각 경계에서 수행한다. 마지막 브라우저 테스트만으로 데이터 누락이나 비공개 누출을 찾으려 하지 않는다.

## 검증 계층

| 계층 | 주요 검사 | 실패 처리 |
|---|---|---|
| 원천 | knowledge base freshness, 기존 content/KB 검증 | 생성 중단 |
| 모델 | schema, 단위, enum, ID, count, 결정론 | 생성 중단 |
| 공개 | visibility, spoiler, field allowlist, waiver, source path 누출 | 공개 빌드 중단 |
| 관계 | 깨진 edge, 고아, cycle 규칙, backlink 대칭성 | 빌드 중단 또는 명시 waiver |
| URL | slug 중복, redirect chain/loop, Unicode 충돌 | 빌드 중단 |
| HTML | 내부 링크, heading, landmarks, metadata, canonical | 빌드 중단 |
| 브라우저 | 키보드, 반응형, 검색, 표, 스포일러 | release 중단 |
| artifact | 게임 버전, update record, 파일 목록, base path, CSP, sitemap, robots, 비공개 문자열 | 배포 중단 |
| graph | node·edge 방향/수량/조건, graph slice digest, 필터·스포일러 결과, fallback 표 | 배포 중단 |

## 자동 테스트

### Python

- parser/normalizer unit test
- 원천 fixture별 typed model snapshot
- publication allowlist와 leakage test
- forward/backlink 대칭성과 count invariant
- slug/redirect property test
- 같은 입력 2회 생성의 deterministic digest test

### TypeScript/Astro

- schema validation unit test
- infobox·relation·spoiler component test
- 대표 7개 템플릿의 semantic snapshot
- 빈 값, 긴 한국어, 다중 관계, 단위, 알 수 없는 유형 fixture

### 브라우저

- 홈 → 검색 → 엔터티 → 관계 대상 이동
- 키보드로 search dialog, drawer, 목차, spoiler, filter 조작
- mobile/desktop 주요 viewport
- dark/light, reduced motion, 200% zoom
- 리디렉션과 404 복구
- JavaScript 비활성 상태의 canonical 정보 접근

## 접근성 기준

WCAG 2.2 AA를 릴리스 기준으로 둔다.

- semantic landmark와 건너뛰기 링크
- 논리적인 heading 계층
- 모든 상호작용의 키보드·focus 표시
- 텍스트/비텍스트 대비와 색상 외 상태 표현
- 표 caption, scope, 단위, 스크롤 안내
- 아이콘 이름과 이미지 alt text
- dialog focus trap/복귀와 Esc 닫기
- 200% 확대에서 정보·기능 손실 없음
- motion 감소 설정 존중
- 관계 그래프와 동일한 표/목록 대안
- 스포일러 경고를 먼저 읽고 명시적으로 펼치기 전에는 키보드·screen reader에도 보호 본문이 노출되지 않음

axe 계열 자동 검사는 보조 수단이다. 대표 페이지는 키보드와 screen reader landmark를 수동 확인한다.

## 성능 예산

초기 목표는 다음과 같다. 실제 미디어가 들어오면 대표 저사양 모바일 프로필에서 다시 측정한다.

- 일반 문서 초기 JavaScript: gzip 75KB 이하; 검색 색인은 검색을 열 때 지연 로딩
- LCP: 2.5초 이하, CLS: 0.1 이하, INP: 200ms 이하 목표
- 일반 엔터티 HTML: 250KB 이하; 관계가 큰 페이지는 pagination/부분 목록 적용
- 대표 이미지: 반응형 크기와 현대 포맷, width/height 고정
- 외부 폰트·분석·광고 요청: V1 0개
- CI 전체 공개 빌드: 목표 5분 이내; 넘으면 유형별 병목과 증분 전략을 측정한 뒤 결정

Lighthouse 점수 하나만으로 승인하지 않고 실제 Web Vitals 예산과 기능 테스트를 함께 본다.

## 대규모 데이터 테스트

- 3,475개 현행 엔터티뿐 아니라 10,000개 synthetic fixture에서도 메모리와 빌드 시간을 기록한다.
- 관계 1,000개인 병목 엔터티에서 DOM 크기와 검색 색인 크기를 측정한다.
- 모든 엔터티 JSON을 하나의 client bundle로 보내지 않는다.
- 목록 정렬·필터는 필요한 필드만 가진 축약 projection을 사용한다.

## 시각 회귀

홈, 가이드, 일반 엔터티, 관계가 많은 엔터티, 표, 검색, 404의 desktop/mobile 기준 이미지를 관리한다. 1px 차이를 무조건 실패시키지 않고 영역별 threshold와 승인 절차를 둔다. 데이터 텍스트가 자주 바뀌는 영역과 레이아웃 경계를 분리한다.

## 보안·프라이버시

정적 사이트라도 다음을 검증한다.

- curated Markdown의 raw HTML 금지 또는 엄격 sanitize
- 외부 링크의 안전한 속성 및 URL scheme allowlist
- inline script 최소화와 정적 호스팅 가능한 CSP
- source map, 내부 path, stable ID dump, QA artifact 비배포
- secrets와 API key가 필요 없는 구조
- 사용자 query를 서버로 보내지 않는 정적 검색
- analytics·cookie·fingerprinting 없음이 V1 기본
- dependency lockfile, 취약점 검사, Dependabot 또는 동등 자동 갱신 정책
- artifact 문자열 검사로 `Assets/`, `docs_final/`, Windows 절대 경로, `internal` fixture가 없는지 확인
- `GAME_VERSION`, game-version manifest, release note, footer, `/updates/`의 current 게임 버전 일치 검사
- published/withdrawn 게임 버전 폴더와 NAS historical artifact digest 불변 검사
- 게임 버전 선택기의 same-ID 이동, 문서 부재 fallback, 게임 버전별 링크·검색·관계 격리 검사
- graph node·edge가 관계 표와 동일한 방향·수량·조건을 가지는지, graph filter/URL restore/keyboard/표 fallback이 정상인지 검사
- graph JSON, canvas label, Pagefind, screenshot에 `internal`·`blocked`·미허용 spoiler 텍스트가 없는지 검사

NAS를 인터넷에 공개할 경우에는 위 항목에 더해 관리 UI와 위키 서비스의 포트·호스트 분리, 최소 권한 배포 계정, SSH key 인증, brute-force 제한, reverse proxy 보안 헤더, 인증서 자동 갱신과 외부 포트 검사를 릴리스 게이트로 둔다. 위키 공개를 위해 NAS 관리 화면이나 SMB를 WAN에 노출하지 않는다.

확인된 Synology 상태에서는 다음을 별도 production 차단 조건으로 둔다.

- DSM 방화벽과 자동 IP 차단이 현재 비활성이다. LAN/VPN RC에는 허용할 수 있으나, 공개 전에는 router와 NAS 양쪽의 허용 규칙·차단 정책을 명시적으로 검증한다.
- 현재 DDNS hostname의 HTTPS 443은 기존 reverse-proxy 서비스가 사용하지만 소유자가 서비스 종료를 승인했다. 기존 rule을 먼저 백업하고 위키 내부 portal 검증 후 maintenance window에서 한 번만 교체한다.
- 현재 인증서는 기존 hostname용이며 wildcard로 표시되지 않았다. 새 hostname은 그 이름과 일치하는 인증서를 발급·자동 갱신해야 한다.
- Web Station에 phpMyAdmin alias가 존재한다. 공개 hostname/port에서 접근되지 않도록 portal·reverse-proxy 경계를 확인한다.
- DSM 관리 포트, SMB, SSH, SFTP, FTPS는 위키 공개 endpoint가 아니다. 외부 포트 감사에서 필요하지 않은 서비스가 보이면 공개를 중단한다.
- 패키지 보안 업데이트, 인증서 만료 감시, 장기 uptime 이후의 계획된 유지보수와 재부팅 검증을 공개 체크리스트에 포함한다.

SFTP 배포는 비관리자 전용 계정과 staging share ACL의 실제 읽기·쓰기·상위 경로 차단 테스트를 통과해야 한다. DSM상 SSH shell은 관리자 그룹으로 제한되므로 SSH key 인증만으로 이를 최소 권한 경로라고 간주하지 않는다.

## 콘텐츠 보안

입력 문서와 CSV의 문장은 데이터이며 명령이 아니다. 생성 도구나 AI가 그 안의 shell/HTML 지시를 실행하지 않는다. 파일 경로는 저장소 허용 루트 안에서 resolve하고 traversal을 거부한다.

## 릴리스 차단 결함

- 수치·관계가 원천과 다름
- 공개 금지 또는 스포일러 콘텐츠 누출
- 깨진 canonical 링크나 redirect loop
- 키보드로 핵심 기능 사용 불가
- 심각한 색 대비·dialog focus 오류
- 결정론 실패 또는 stale source
- 배포 artifact에 내부 경로·QA 자료 포함
- 게임 버전 감소·재사용 또는 실제 artifact와 게임 버전/update record 불일치
- 과거 게임 버전 문서 변조 또는 선택 게임 버전 밖의 문서·검색 결과 혼입
- node·edge 예산 초과를 조용히 절단하거나 graph에만 관계를 표시해 표 fallback이 누락됨
- 공개 endpoint에서 DSM 관리 UI·phpMyAdmin·파일 서비스가 함께 노출되거나 443 전환 후 기존/신규 backend가 충돌

문장 오탈자나 장식 미세 차이는 후속 수정이 가능하지만 차단 결함과 분리 기록한다.
