# 07. 검색·링크·탐색

## 검색 구조

검색은 두 층으로 구성한다.

1. 별칭 바로가기: 정확한 제목, 이전 이름, stable ID 보조어, 공식 영문명을 현재 공개 projection에서 매칭
2. 전체 텍스트 검색: `/api/search.json`이 현재 공개 projection과 가이드·참조 문서를 요청마다 검색

이 구조는 정확한 콘텐츠 찾기와 설명 문장 찾기를 분리한다. 별도 검색 서버나 외부 SaaS는 V1에 사용하지 않는다.

검색 API와 alias dictionary는 `wiki/game-versions/{game-version}/` 단위로 분리한다. current route는 current 게임 버전만, historical route는 해당 게임 버전만 요청한다. 검색 결과 URL, snippet, filter count와 alias jump가 선택 게임 버전 밖으로 나가면 검증을 실패시킨다.

## 색인 대상

- 제목, 승인 별칭, 요약, 본문, 표의 의미 있는 헤더·셀
- 유형, 카테고리, 스포일러 등급을 API filter 계약으로 기록
- 인포박스의 단위 포함 필드명
- 가이드의 절 제목과 핵심 용어

검색에서 제외한다.

- 전역 navigation의 반복 텍스트
- 내부 source path, stable ID 원문 표시, QA flag
- `internal`/`blocked` 엔터티
- 공개 전 스포일러 제목과 별칭
- footer, cookie-less 테마 설정, 숨은 접근성 보조 반복문

## 한국어 검색

- HTML `lang="ko"`를 기본으로 설정한다.
- 띄어쓰기 변형, 한글/영문 공식명, 흔한 약칭을 curated aliases에 기록한다.
- 초성 검색은 V1 필수 기능으로 두지 않는다. 대표 사용자 테스트에서 필요성이 확인되면 작은 별칭 색인으로 구현한다.
- 오타 교정은 임의 fuzzy 강도를 높이기보다 추천어 사전과 결과 없음 화면으로 시작한다.
- stable ID는 개발 프리뷰에서는 직접 검색 가능하지만 공개 결과에는 ID를 표시하지 않는다.

## 랭킹

기본 우선순위는 exact title > alias > heading > summary > body > table cell이다. 동일 이름은 유형과 카테고리를 함께 표시한다. 가이드를 무조건 엔터티보다 위에 두지 않고 query 의도 세트로 가중치를 검증한다.

## 검색 UX

- 상단 검색은 모든 화면에서 동일한 단축키와 focus 동작을 제공한다.
- 입력 즉시 상위 결과를 제목·유형·짧은 문맥으로 표시한다.
- 전체 결과 화면은 유형·카테고리·스포일러 허용 여부로 필터링한다.
- 결과 없음 화면은 입력을 보존하고 가까운 별칭, 상위 카테고리, 수정 제안 경로를 제공한다.
- 검색 query는 공유 가능한 URL에 남기되 개인 식별 분석 로그는 수집하지 않는다.

## 대표 query 회귀 세트

`wiki/tests/fixtures/search-queries.yml`에 최소 50개를 관리한다.

- 정확한 한국어 표시명
- 띄어쓰기 없는 이름
- 공식 영문명과 약칭
- 원료에서 생산품 찾기
- 시설에서 가능한 작업 찾기
- 연구에서 해금 찾기
- 질병에서 치료 찾기
- 옛 이름에서 새 canonical 페이지 찾기
- 동명이인 구분
- 비공개 자료와 스포일러 미허용 상태의 제목·별칭·문구 누출 금지 query

각 query는 기대 top-1 또는 top-5 stable ID와 금지 결과를 함께 기록한다.

## 그래프 진입점

검색 결과와 entity page는 제작식·아이템·시설·연구에만 `관계 그래프` 진입점을 표시한다. 진입점은 현재 선택 게임 버전과 스포일러 ceiling을 유지한 `/relations/production/{slug}/` 또는 `/relations/research/{slug}/`로 이동한다. 검색 결과가 graph 내부 node label을 별도 문서처럼 중복 색인하지 않으며, graph에서 선택한 node는 canonical 위키 페이지로만 이동한다.

## 자동 링크

자동 링크는 normalized model의 명시적 stable ID relation에만 적용한다. 단순 문자열 치환으로 모든 이름을 링크하지 않는다. curated Markdown에서 `[[표시명]]` 문법을 지원하더라도 빌드 시 slug registry에서 유일하게 해소되어야 한다.

## 역링크

역링크는 다음 그룹으로 보여 준다.

- 생산에 사용
- 생산 결과
- 건설·운영에 필요
- 연구로 해금
- 사건·질병·전투에서 참조
- 관련 가이드에서 언급

공개 대상이 많은 경우 상위 중요 관계만 본문에 보여 주고 전체 결과는 필터 가능한 목록으로 연결한다. 관계 수와 정렬 기준은 결정론적으로 유지한다.

## 추천·관련 문서

V1은 행동 추적 기반 추천을 하지 않는다. typed relation, 동일 카테고리, curated related guide만 사용한다. 추천 이유를 `같은 시설에서 생산`, `이 연구가 해금`, `이 가이드에서 설명`처럼 표시한다.

## 실시간 검색 검증 게이트

`/api/search.json`은 요청 시 현재 공개 projection을 읽는다. 정적 HTML과 Pagefind 색인을 다시 만들지 않으며, 가이드·참조 JSON과 엔터티 모델 수정은 다음 검색 요청에 반영한다.

- Korean query 회귀 세트 통과
- 현재 게임 버전의 공개 문서만 반환
- 스포일러 보호 엔터티·가이드는 결과와 excerpt에서 제외
- 제목 일치 결과를 요약 일치보다 먼저 정렬
- 응답과 reverse proxy가 `no-store`를 지켜 이전 결과를 보관하지 않음
- 검색 form의 키보드 접근성과 결과 링크 확인
