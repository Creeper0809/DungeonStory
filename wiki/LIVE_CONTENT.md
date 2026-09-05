# 문서 즉시 반영 운영

이 위키는 Astro 정적 HTML 묶음을 배포하지 않는다. NAS의 Node 컨테이너가 요청마다 `/app/game-versions/`의 공개 문서를 읽어 HTML을 만든다. `docker-compose.live.yml`의 읽기 전용 volume이 NAS의 공개 문서 폴더를 컨테이너에 연결한다.

## 바로 반영되는 변경

- `game-versions/<게임 버전>/content/guides/*.md`
- `work-references.json`, `need-references.json`, 신체 구조와 탐색 JSON
- 공개 모델의 `data/entities`, `data/navigation`, `data/relations` 파일과 스포일러 상세 API

위 파일을 NAS volume에 원자적으로 교체하면 다음 새로고침부터 해당 문서와 `/api/search.json` 결과에 반영된다. 서버 재시작, Astro 빌드, Pagefind 색인 생성은 필요 없다. 엔터티와 분류 목록은 파일 수정 시각을 기준으로 캐시를 즉시 무효화한다.

`data/`는 생성 결과다. 게임 원본 또는 `docs_final`을 고친 경우에는 개발 환경에서 `npm run model`과 두 validator를 먼저 실행하고, 검증된 `game-versions/<새 버전>/` 또는 정정된 공개 파일만 NAS volume으로 올린다. 새 게임 업데이트는 기존 버전 폴더를 복사해 새 버전 폴더를 만들고 registry를 갱신한다.

## 별도 배포가 필요한 변경

Astro 템플릿, CSS, 클라이언트 JavaScript, Node 의존성, Dockerfile은 컨테이너 이미지에 들어간다. 이 코드만 이미지 재빌드와 컨테이너 재시작이 필요하다. 문서와 데이터 수정에는 해당하지 않는다.

## NAS 운영 경계

- 컨테이너는 `127.0.0.1:4321`에서만 열고 DSM reverse proxy가 HTTPS 443에서 연결한다.
- `game-versions/`만 읽기 전용 mount한다. 게임 원본, `docs_final`, 계정 정보와 NAS 운영 설정은 컨테이너에 넣지 않는다.
- HTML과 `/api/search.json`은 `Cache-Control: no-store`로 제공한다. reverse proxy에서도 이 경로를 캐시하지 않는다.
- 문서 편집은 임시 파일 이름으로 올린 뒤 같은 volume 안에서 원자 교체한다. 반만 기록된 JSON이나 Markdown은 서버가 숨기지 않고 요청 오류로 드러낸다.
