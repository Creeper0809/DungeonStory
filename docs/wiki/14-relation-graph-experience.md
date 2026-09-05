# 14. 제작·연구 관계 그래프 경험

## 목표

제작식, 아이템, 시설, 연구 노드가 서로 어떻게 이어지는지 **보이게** 만든다. 그래프는 표를 대체하는 장식이 아니라, 플레이어가 "무엇을 먼저 만들고 무엇을 거쳐야 하는가"를 빠르게 찾는 탐색 도구다.

기본 화면은 특정 아이템·제작식·연구를 중심으로 한 작은 관계망이다. 전체 게임 데이터를 한 캔버스에 모두 올리는 방식은 V1 범위에서 금지한다. 밀집한 전역 그래프는 읽기보다 길 찾기를 어렵게 만들고 모바일·키보드·스포일러 정책도 망가뜨린다.

## 지원하는 그래프

| 그래프 | 중심 노드 | 방향 | 플레이어 질문 |
|---|---|---|---|
| 제작 흐름 | 아이템 또는 제작식 | `재료 → 제작식·시설 → 결과물` | 이 아이템은 무엇으로 만들며, 어디에 쓰이는가? |
| 연구 흐름 | 연구 노드 | `선행 연구 → 연구 → 해금` | 이 연구를 위해 무엇이 필요하고, 무엇이 열리는가? |
| 시설 흐름 | 시설 | `시설 ↔ 가능한 제작식 ↔ 입출력` | 이 시설의 역할과 병목은 무엇인가? |
| 경로 비교 | 출발·목표 아이템 | 선택한 유효 경로 | 어떤 제작 경로가 목표까지 이어지는가? |

사건·스토리·엔드게임 관계는 기존 스포일러 정책을 먼저 따른다. 제목조차 스포일러인 노드는 안전한 대체 라벨만 제공하고, 사용자가 해당 등급을 허용하기 전에는 그래프 데이터·검색·edge label에서 제외한다.

## 화면 구조

```text
┌─────────────────────────────────────────────────────────────────────┐
│ [제작 흐름] [연구 흐름]  검색: 철괴                         [초기화] │
├───────────────┬──────────────────────────────────┬──────────────────┤
│ 필터          │ 관계 그래프                       │ 선택 상세        │
│ • 게임 버전   │   철광석 → 제련소 → 철괴          │ 철괴             │
│ • 방향        │              └→ 강철 제작식       │ 획득·사용처      │
│ • 깊이 1–3    │   연구 ─────────────┘             │ 표에서 보기      │
│ • 노드 유형   │                                  │ 관련 가이드      │
│ • 관계 유형   │ [확대] [축소] [전체 보기] [표]    │                  │
└───────────────┴──────────────────────────────────┴──────────────────┘
```

- 큰 화면은 `필터 → 그래프 → 선택 상세`의 3영역으로 둔다. 상세에는 선택 노드의 짧은 설명과 일반 위키 페이지로 가는 링크만 둔다.
- 모바일은 검색·선택 상세·필터를 그래프 위아래의 접이식 영역으로 바꾸고, 그래프 자체는 가로 스크롤 없이 pinch/버튼 확대를 제공한다.
- 그래프를 읽기 어렵거나 JavaScript가 꺼진 경우에는 같은 관계를 방향·유형·수량·조건을 가진 semantic 표와 목록으로 즉시 제공한다. 그래프만으로 중요한 사실을 숨기지 않는다.

## 노드와 edge의 의미

### 노드

| 유형 | 모양 | 표시 원칙 | 선택 시 기본 동작 |
|---|---|---|---|
| 아이템·원료·중간재 | 둥근 사각형 | 이름, 유형 아이콘, 필요한 경우 핵심 단위 | item 페이지와 획득·사용 관계 표시 |
| 제작식·생산 주문 | 육각형 | 제작식 이름과 핵심 시설 | 입력·출력·부산물·작업 조건 표시 |
| 시설·방 | 사각형 | 시설 이름과 역할 | 가능한 작업과 요구·병목 표시 |
| 연구 | 마름모 | 연구 이름과 선행 수 | 선행·해금·관련 가이드 표시 |
| 카테고리 묶음 | 옅은 그룹 영역 | 축약된 종류명만 | 펼치기 전에는 하위 노드 수만 표시 |

색은 노드 유형 보조 수단일 뿐이다. 모든 유형은 모양, 아이콘, 텍스트 label로 함께 구분한다. 현재 디자인 방향의 저채도 바탕과 황동 계열 강조를 유지하고, 네온 선·무의미한 glow·물리 시뮬레이션 애니메이션은 쓰지 않는다.

### edge

| 관계 유형 | 방향 | edge label | 비고 |
|---|---|---|---|
| `consumes` | 아이템 → 제작식 | 필요 수량·조건 | 입력 재료 |
| `produces` | 제작식 → 아이템 | 결과 수량·품질 조건 | 부산물도 별도 edge |
| `runs_at` | 시설 → 제작식 | 작업 가능 | 시설이 필수일 때만 강한 edge |
| `requires` | 선행 연구 → 연구 | 선행 | 연구 그래프의 기본 방향 |
| `unlocks` | 연구 → 콘텐츠 | 해금 | 아이템·시설·제작식으로 이어짐 |
| `alternative` | 동등한 콘텐츠 사이 | 대체 | 기본적으로 접고 요청 시 표시 |

한 edge는 하나의 typed relation만 나타낸다. 여러 의미를 한 선에 합치거나 문자열에서 관계를 추론하지 않는다. edge를 선택하면 관계 설명, 조건, 수량의 공개 가능 필드만 상세 영역에 표시한다.

## 필터·탐색 계약

필터는 선택한 게임 버전 범위 안에서만 작동하며 URL query로 공유할 수 있다. 브라우저 새로고침·뒤로가기에도 같은 그래프 상태를 복원한다.

| 제어 | 값 | 기본값 | 동작 |
|---|---|---|---|
| 중심 검색 | 공개 제목·승인 별칭 | 현재 문서의 stable ID | 동일 게임 버전의 공개 노드만 후보로 제시 |
| 그래프 종류 | 제작 / 연구 / 시설 / 경로 비교 | 현재 페이지 문맥 | 허용되지 않은 relation type을 제거 |
| 방향 | 입력만 / 출력만 / 양방향 | 양방향 | 시작 노드에서의 탐색 방향 변경 |
| 깊이 | 1, 2, 3 | 1 | 각 방향의 hop 수; 깊이 3은 node 예산 안에서만 표시 |
| 노드 유형 | item, recipe, facility, research | 현재 그래프에 유효한 전부 | 숨긴 노드 때문에 끊긴 경로를 별도 표시 |
| 관계 유형 | `consumes` 등 | 그래프 종류별 핵심 관계 | edge와 고립 node를 함께 갱신 |
| 스포일러 | 사이트 전역 허용 상태 이하 | 전역 설정 | 허용되지 않은 노드는 애초에 model에 포함하지 않음 |
| 레이아웃 | 계층 / 중심 | 그래프별 기본값 | 데이터·경로는 바꾸지 않고 배치만 변경 |

- depth 증가, `모두 펼치기`, 전체 게임 그래프 전환은 node/edge 예산을 넘으면 즉시 거부하고 범위를 좁히는 안내를 한다. 조용히 노드를 버리지 않는다.
- 기본 예산은 desktop `60 nodes / 100 edges`, mobile `30 nodes / 45 edges`다. 수치는 실제 fixture와 성능 측정으로 조정하며, 초과 시 어떤 filter가 결과를 줄일지 제시한다.
- 숨긴 필터 결과 때문에 선택 노드가 고립될 때는 "필터로 연결 N개가 숨겨짐"을 표시하고 한 번의 action으로 해당 필터를 해제할 수 있게 한다.
- 비교는 player build나 저장 데이터를 요구하지 않는다. 공개된 제작 조건·연구 관계만으로 설명하고, 개인 보유량·선호를 추측하지 않는다.

## 상호작용과 편의성

- 노드 선택: click/tap/Enter로 선택하고 상세 영역을 갱신한다. hover는 보조 미리보기일 뿐 필수 정보를 숨기지 않는다.
- 탐색: 선택 노드에서 `입력 펼치기`, `출력 펼치기`, `연구 펼치기`, `이 노드를 중심으로`를 제공한다. 확장은 node 예산을 검사한 뒤 한 단계씩만 한다.
- 보기: 확대·축소·맞춤 보기·선택 경로 강조·필터 초기화를 제공한다. pan/zoom 상태는 공유 URL의 기본 상태가 아니며, 선택·필터·깊이만 URL에 저장한다.
- 키보드: Tab으로 controls와 일반 표에 이동하고, 그래프 canvas에는 별도 `그래프 탐색 모드` 진입 버튼을 둔다. 진입 후 화살표는 가장 가까운 방향 노드, Enter는 선택, Escape는 상세 영역으로 돌아간다. canvas는 모든 노드를 직접 tab-stop으로 만들지 않는다.
- 노드 상세와 필터 결과는 `aria-live="polite"`로 짧게 알리고, pointer hover·layout animation은 읽어 주지 않는다.
- 일반 콘텐츠 링크·breadcrumb·검색은 그래프 선택으로 바뀌지 않는다. 선택 상세에서만 canonical 페이지로 이동한다.

## 데이터와 생성 계약

그래프는 기존 normalized entity/relation model의 별도 projection이다. UI가 `docs_final` CSV나 C# 소스, 내부 파일 경로를 직접 읽지 않는다.

```text
공개 normalized entities + typed relations + publication manifest
                         │
                         ▼
                 graph projection validator
                         │
                         ▼
  wiki/game-versions/{game-version}/data/graph/
  ├─ manifest.json
  ├─ nodes/{stable-id}.json
  ├─ slices/production/{stable-id}.json
  ├─ slices/research/{stable-id}.json
  ├─ slices/facility/{stable-id}.json
  └─ qa/graph-report.json
```

각 slice는 중심 노드, 게임 버전, graph schema version, source snapshot digest, 공개 nodes, 공개 edges, 적용된 spoiler ceiling, 발생 가능한 expansion ID만 포함한다. URL/label/아이콘 필드는 공개 projection에서만 받고 internal stable ID·source path·QA flag를 노출하지 않는다.

생성기는 다음을 실패 처리한다.

- relation의 양끝 중 하나가 공개 graph node로 해소되지 않음
- edge type, 방향, 수량 단위, required condition의 schema 불일치
- 현재 게임 버전 밖 node/edge, 다른 게임 버전 URL, forbidden spoiler node 포함
- 동일 node/edge ID 중복, self-loop 정책 위반, 비결정적 정렬
- 입력/출력 또는 선행/해금 표와 graph projection의 count·digest 불일치
- 예산 초과 slice를 default 그래프로 지정하거나 예산 초과를 숨김 처리

## 클라이언트 구조와 성능

`GraphExplorer`는 Astro의 표준 client-side script로 만든 독립 island다. 정적 페이지·표·관계 목록은 먼저 HTML로 렌더링하고, viewport에 들어오거나 사용자가 `그래프 열기`를 누를 때만 graph data와 시각화 라이브러리를 dynamic import한다.

- 기본 라이브러리 후보는 framework-free `Cytoscape.js`다. directed·compound graph, selector 기반 filtering, collection traversal, touch event, 레이아웃 API를 제공하므로 일반 스크립트 island에서 쓸 수 있다. V1에서는 기본 제공 `breadthfirst`와 `concentric` 레이아웃만 사용하고, 실제 필요성·번들 예산이 검증되기 전 layout extension은 추가하지 않는다.
- 제작·연구의 기본 레이아웃은 위에서 아래로 흐르는 계층형이다. 중심 보기만 `concentric`으로 사용한다. 물리력 기반 animation을 지속 실행하지 않는다.
- graph model과 library는 선택한 slice 하나만 load한다. 모든 게임 버전과 전체 relation graph를 초기에 내려받지 않는다.
- 최초 graph interaction은 p75 `1.5s` 이내, 필터 적용과 depth 1↔2 전환은 p75 `200ms` 이내를 시작 예산으로 둔다. 저사양 모바일 fixture와 실제 공개 데이터로 재측정해 gate를 조정한다.
- 각 게임 버전의 graph schema와 generated slice digest를 manifest에 기록해 historical view가 current graph data와 섞이지 않게 한다.

Astro는 기본적으로 정적 HTML을 보내고 필요한 상호작용만 client script로 둘 수 있으므로, 그래프를 작은 island로 격리한다. Cytoscape.js의 graph model·filter·layout 기능은 이 범위의 directed relation explorer에 맞는다. 구현 시 라이브러리 버전과 license는 lockfile·SBOM에 고정한다.

## 검증과 완료 조건

- 대표 fixture: 단일 제작식, 부산물, 다단계 제작, 대체 제작식, 연구 다중 선행, 연구 다중 해금, 공개 차단 node, version 간 변경/삭제 node
- graph edge와 표의 relation count·방향·단위가 동일하고 두 번 생성한 slice digest가 동일
- type·direction·depth·spoiler filter 조합, URL restore, browser back/forward, 선택 노드 부재·숨김 상태를 browser test로 검증
- mouse/touch/keyboard로 선택·확장·맞춤 보기·표 전환을 완료하고 screen reader에서 동일 관계와 필터 결과를 확인
- JavaScript 실패, canvas/WebGL 미지원, node 예산 초과에서도 semantic 표와 canonical 링크가 사용 가능
- desktop 1280px, tablet 768px, mobile 360px에서 label 겹침·잘림·가로 overflow가 없음
- current와 historical 게임 버전의 node/edge/search result가 서로 섞이지 않음
- 공개 차단·미허용 스포일러 텍스트가 graph JSON, HTML, CSS title, screenshot, Pagefind index 어디에도 없음

## 구현 순서

1. production/research typed relation을 graph schema로 투영하고 graph-report·결정론 검증을 만든다.
2. 아이템 5개·제작식 5개·연구 5개를 대상으로 static 표 + 한 단계 graph slice를 만든다.
3. `GraphExplorer`의 선택·필터·keyboard·fallback을 구현하고 mobile fixture에서 검증한다.
4. depth expansion, version-scoped loading, URL restore, 관계 경로 강조를 추가한다.
5. 전체 공개 콘텐츠, 시설 흐름, graph QA·성능·스포일러 regression으로 확장한다.

그래프가 관계의 권위가 되지는 않는다. 수치와 조건의 권위는 기존 게임 자산·승인 문서이며, 그래프는 검증된 typed relation을 플레이어가 탐색할 수 있게 표현하는 projection이다.

## 외부 기술 근거

- [Cytoscape.js 공식 문서](https://js.cytoscape.org/): directed/compound graph, selector, collection, layout API
- [Astro Islands Architecture](https://docs.astro.build/en/concepts/islands/): 상호작용 영역만 client-side로 격리하는 구조
- [Astro client-side scripts](https://docs.astro.build/en/guides/client-side-scripts/): framework 없이 표준 script로 상호작용을 추가하는 방식
