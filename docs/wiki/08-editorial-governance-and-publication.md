# 08. 편집·검수·공개 정책

## 역할

| 역할 | 책임 |
|---|---|
| 게임 권위 소유자 | 구현 사실·밸런스·공개 범위 최종 승인 |
| 위키 편집자 | 소개·공략·예시·카테고리·별칭 작성 |
| 도메인 검수자 | 해당 시스템 사실과 플레이 맥락 검수 |
| 파이프라인 관리자 | 생성기·스키마·CI·결정론 유지 |
| 릴리스 승인자 | 스포일러·미디어·호스팅·배포 승인 |

한 사람이 여러 역할을 맡을 수 있지만 수치 변경과 공개 승인을 자동 생성기가 대신하지 않는다.

## 콘텐츠 상태

- `draft`: 작업 중, 공개 빌드 제외
- `review`: 사실·문장·스포일러 검수 대기
- `published`: 공개 허용
- `retired`: canonical에서 제거하고 적절한 리디렉션 또는 폐기 설명 제공
- `blocked`: 원천 오류, 권리, 보안, 미구현 불일치로 공개 금지

## 공개·스포일러 모델

`visibility`와 `spoiler_tier`를 별도로 둔다.

| visibility | 의미 |
|---|---|
| `public` | 일반 공개 가능 |
| `spoiler` | 공개 사이트에는 존재하지만 사용자의 명시적 허용 전 제목·본문 보호 |
| `internal` | 개발 프리뷰 전용 |
| `blocked` | 어떤 위키 빌드에서도 콘텐츠로 사용 금지; QA에만 기록 |

| spoiler tier | 예 |
|---|---|
| `none` | 기본 규칙, 초반 공개 콘텐츠 |
| `progression` | 연구 해금·중후반 생산망 |
| `narrative` | 사건 결과·세력 비밀 |
| `endgame` | 최종 조우·결말·엔드리스 핵심 |

스포일러 설정 전에는 검색 suggestion, metadata description, related card, sitemap 제목에서도 해당 표현을 노출하지 않는다. V1 구현이 이를 완전하게 보장하지 못하면 해당 tier 페이지를 통째로 비공개한다.

소유자 결정에 따라 검증된 `progression`, `narrative`, `endgame` 콘텐츠도 공개 artifact에 포함한다. 단, 다음 공개 계약을 지킨다.

- 스포일러 구간 앞에 해당 tier와 영향을 설명하는 명시적 경고를 표시한다.
- 본문·이미지·결과 표는 기본 접힘 또는 마스킹 상태이며 독자의 명시적 펼치기 전에는 읽히지 않는다.
- 제목 자체가 스포일러면 검색·카드·목차·metadata에서 안전한 대체 라벨을 사용한다.
- 개별 펼치기와 전체 스포일러 허용을 제공하되 선택은 브라우저 로컬에만 저장한다.
- 색·blur만으로 가리지 않고 키보드와 screen reader에서도 경고와 펼치기 순서를 보장한다.
- 개발자 전용, 미구현, 근거 불충분, `blocked` 자료는 “전부 공개” 결정의 대상이 아니다.

## 편집 경로

### 사실·수치 변경

원천 C#/Unity 자산 또는 승인 문서 → 필요한 밸런스 기록·검증 → knowledge base 재생성 → wiki 재생성. 위키 overlay로 우회하지 않는다.

### 설명·공략 변경

curated overlay 또는 guide source → 언어·링크·사실 검수 → preview → 공개 manifest 상태 변경.

### 이름·URL 변경

원천 표시명 변경 → slug registry 검토 → 기존 slug를 alias/redirect로 유지 → 검색 회귀 갱신.

### 이미지 변경

권리·출처 manifest → crop/optimization → alt text 검수 → 스포일러 등급 → visual regression.

게임 로고·대표 이미지·스크린샷의 공식 위키 사용 권한은 소유자가 확인했다. 그래도 asset별 원본 경로, 제작/소유 주체, 변형 여부, 스포일러 tier를 manifest에 남긴다. 외부 자산이나 제3자 제작물은 이 포괄 승인에 포함하지 않는다.

## 변경 요청 체크리스트

- 무엇이 바뀌었고 어떤 독자 질문을 해결하는가
- 사실 필드인지 설명 필드인지
- source digest와 확인한 원천
- 영향을 받는 stable ID, 페이지, 관계, 검색 별칭
- 영향 graph slice, node·edge·필터 결과와 그래프 표 fallback
- 스포일러와 미디어 권리
- 자동 생성·링크·접근성·시각 검증 결과
- 공개 여부와 rollback 방법

## 작성 스타일

- 첫 문단에서 대상의 기능과 중요성을 직접 설명한다.
- 홍보 문구, 모호한 최상급, 근거 없는 메타 평가는 쓰지 않는다.
- 단위와 조건을 생략한 숫자를 쓰지 않는다.
- 현재 구현과 예정 기능을 섞지 않는다.
- 자동 생성 표의 값을 본문에 반복할 때도 데이터 바인딩으로 렌더링한다.
- 공략 판단은 `추천`, `대안`, `주의`를 구분하고 적용 조건을 적는다.
- 다른 게임이나 외부 위키 문장을 복사하지 않는다.
- 외부 위키 참고는 허브·분류·관계 탐색 같은 정보 구조 분석으로 한정한다. 외부의 문장, 표 데이터, 이미지, 문서명 체계, UI/CSS, 커뮤니티 편집 기능을 가져오지 않는다.

## 공개 manifest

`wiki/game-versions/{game-version}/content/publication.yml`은 해당 게임 버전에 대해 최소 다음을 표현한다.

- 허용 콘텐츠 유형과 기본 visibility
- 개별 stable ID 예외
- guide별 상태와 검수자
- spoiler tier 정책
- 공개 가능한 fact field allowlist
- 금지 field/path pattern
- 데이터 revision과 승인 시점

manifest 변경은 코드 변경과 같은 리뷰를 받는다. `internal → public` 승격은 명시적 diff로 보여야 하며 자동 추론하지 않는다.

## waiver 정책

예외를 무시하는 전역 플래그는 금지한다. waiver는 정확한 rule ID와 대상 stable ID, 공개 영향이 없다는 근거, 승인자, 만료 조건을 가져야 한다. 만료되거나 대상 내용이 바뀌면 자동으로 무효화한다.

## 외부 기여

V1은 이슈 또는 PR 제안만 받는다. 제안자는 source authority를 직접 판단하지 않으며, 편집자가 원천과 대조한다. 공개 사이트에 `수정` 버튼을 넣는 경우 정확한 canonical ID와 템플릿이 채워진 저장소 링크만 연다.

## 변경 이력

공개 변경 이력은 플레이어에게 의미 있는 변화만 요약한다. 커밋 로그나 내부 파일 목록을 그대로 노출하지 않는다. 삭제·이름 변경·수치 변경·새 관계를 유형별로 생성하고, 검수된 설명을 선택적으로 덧붙인다.

관계가 바뀌면 해당 graph slice의 추가·삭제·방향 변경도 자동 diff에 포함한다. 그래프의 label·색·배치만 바꾼 변경은 게임 사실 변경으로 쓰지 않되, 접근성·스포일러·성능 검수는 다시 수행한다.

현재 게임 버전과 누적 기록은 [`GAME_VERSION`](GAME_VERSION), [`CHANGELOG.md`](CHANGELOG.md)를 기준으로 하며, 구현 후 게임 버전별 원문은 `wiki/game-versions/{game-version}/update.md`에서 관리한다. 게임 업데이트는 직전 폴더 전체를 복사한 뒤 새 폴더에서만 편집한다. 증가 규칙, 공개 URL, 불변성, 철회·rollback 처리는 [버전·업데이트 이력 관리](13-versioning-and-update-history.md)를 따른다.

published/withdrawn 게임 버전 폴더의 문서 수정은 금지한다. 게임 사실이 바뀌면 새 patch 게임 버전을 복사·생성해 수정하고 update record에 기록한다. 게임 버전이 바뀌지 않는 문서 오류는 `wiki/errata/{game-version}/` append-only 기록과 새 deployment release ID로 추적한다.
