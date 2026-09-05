# DungeonStory 게임 버전 업데이트 기록

현재 게임 버전: `0.0.1v`

이 문서는 플레이어에게 의미 있는 게임 업데이트를 게임 버전별로 기록한다. 커밋 목록이나 생성 파일 목록을 그대로 옮기지 않는다. 게임 버전 규칙과 공개 페이지 계약은 [버전·업데이트 이력 관리](13-versioning-and-update-history.md)를 따른다.

## Unreleased

### 예정

- Web Station 내부 release candidate 및 공개 HTTPS 443 전환 검증

### 로컬 구현 완료

- Astro 정적 앱, Pagefind current-version 검색, version-scoped current/archive route, directory board, system icon rail, 관계 표·필터 그래프 구현
- `Tools/Wiki`의 source projection, publication validation, determinism, artifact audit, game-version folder copy, rebuild entrypoint 구현
- `wiki/game-versions/0.0.1v/` 최초 데이터 snapshot 생성: 확인된 공개 엔터티 2,904개, 공개 관계 3,582개
- 초기 HTML·검색 색인에서 스포일러 상세를 제외하고 명시적 열기 payload로 분리

## 0.0.1v — 게임 버전 문서 기준선

- 승인일: 2026-09-02
- 공개일: 미정
- 상태: `planned`

### 추가

- `docs_final`을 입력 권위로 사용하는 정적 위키 구조
- 엔터티, 생산·연구 관계, 역링크, 정적 검색 계획
- Synology Web Station/Nginx 배포와 15분 rollback 계약
- 게임 버전 manifest, 공개 업데이트 페이지, release-note 검증 계약
- 게임 버전별 전체 문서 폴더 복사, 불변 historical snapshot, 게임 버전 선택기와 과거 문서 route 계약
- 아이템·제작식·시설·연구의 version-scoped 관계 그래프, 필터·keyboard·표 fallback 계약
- 공통 shell과 아이템·시설·제작식·연구·가이드·탐색·업데이트 페이지의 세부 정보 순서와 상태 화면 계약
- 나무위키 게임 문서의 허브·분류·엔터티·업데이트 탐색 패턴을 분석한 DungeonStory 전용 콘텐츠 구조와 외부 위키 비복제 계약
- 홈/전체 둘러보기의 아이콘 디렉터리 보드와 system hub의 icon rail + 링크 인덱스, version/spoiler/icon provenance 계약

### 공개 정책

- 첫 production부터 인터넷 공개
- 검증된 스토리·연구·엔드게임 콘텐츠 전체 수록
- 스포일러 경고와 기본 마스킹·접기

### 디자인

- 포스트 아포칼립스와 다크 판타지 결합
- RimWorld 등 시뮬레이션 위키의 실용적 정보 밀도 참고
- 전형적인 AI 생성풍 장식과 과도한 glow·gradient·glass 효과 배제

### 알려진 제한

- 방화벽·자동 차단, WAN 443 target, phpMyAdmin 격리, SFTP ACL과 실제 rollback은 NAS release candidate 단계에서 검증해야 한다.
- `0.0.1v` snapshot은 공개 전 `planned` 상태이며, 실제 외부 smoke test 전에는 `published`로 바꾸지 않는다.
