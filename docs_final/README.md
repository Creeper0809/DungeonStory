# DungeonStory 설계 문서 체계

이 디렉터리의 설명 문서 진입점은 [현행 게임 시스템 설계 핸드북](handbook/README.md)이고, 구현·콘텐츠·권위의 통합 탐색은 [기술·콘텐츠 지식베이스](knowledge-base/README.md)에서 시작한다. 핸드북은 현재 시스템의 구조와 제작 의도를 설명하며, 지식베이스는 Unity 작성 자산과 C#에서 재생성한 관계를 원문 권위에 연결한다. 구현 여부와 후속 과제는 [시스템 구현 권위 체크리스트](system-implementation-checklist.md)에서 관리한다.

기존 문서는 삭제하거나 대체하지 않았다. 구현 보고서, QA 기록, 생성 보고서, 과거 설계안은 근거와 이력으로 남긴다. 특히 [V27 물리 질량·운반 재조정 계획](game-design/v27-physical-mass-and-hauling-recalibration-plan.md)은 다른 작업이 사용 중인 읽기 전용 자료다. 핸드북에서 내용을 요약하고 링크하지만 원본은 수정하지 않는다.

## 권장 열람 경로

1. [기술·콘텐츠 지식베이스](knowledge-base/README.md)
2. [게임 정체성과 운영 순환](handbook/01-game-vision-and-player-loop.md)
3. [시설 합성과 사용 기반 진화](architecture/systems/19-facility-synthesis-and-use-based-evolution.md)
4. [사용 이력 기반 진화 아키텍처](architecture/07-history-driven-evolution-architecture.md)
5. [시스템 구성과 상태 권위](handbook/02-system-map-and-authority.md)
6. [코드 아키텍처 문서군](architecture/code-architecture-guide.md)
7. [전체 런타임 구조](architecture/08-whole-runtime-topology.md)
8. [상태 권위 원장](architecture/09-state-authority-ledger.md)
9. [도메인 간 거래와 실패 경계](architecture/10-cross-domain-transactions.md)
10. [런타임 스케줄링과 읽기 투영](architecture/11-runtime-scheduling-and-projections.md)
11. [시스템 구현과 성능 설계](architecture/systems/README.md)
12. [콘텐츠 데이터베이스](content-db/README.md)
13. 관심 있는 시스템 장
14. [시스템 구현 권위 체크리스트](system-implementation-checklist.md)

## 문서 권위 체계

| 순위 | 자료 | 용도 |
|---|---|---|
| 1 | 현재 C# 코드와 Unity 작성 자산 | 현재 구현 사실과 콘텐츠 수 |
| 2 | [전체 게임 밸런스 기준서](game-design/whole-game-balance-baseline.md) | 승인된 수치, 변경 기록, 필수 감사 계약 |
| 3 | [V27 물리 질량·운반 재조정 계획](game-design/v27-physical-mass-and-hauling-recalibration-plan.md) | 진행 중인 물리 경제 전환의 상세 계획과 체크포인트 |
| 4 | [시스템 구현 권위 체크리스트](system-implementation-checklist.md) | 구현 확인, 부분 이행, 미구현과 검증 상태의 통합 색인 |
| 5 | [기술·콘텐츠 지식베이스](knowledge-base/README.md) | 코드·콘텐츠·상태 권위·문서 위치를 연결하는 재생성 가능 인덱스 |
| 6 | [콘텐츠 데이터베이스](content-db/README.md) | 작성 자산별 규칙, 연결 관계, 존재 이유와 구현 권위 |
| 7 | `handbook/` | 사람이 읽는 현행 시스템 설명 |
| 8 | 신규 목표 설계 문서 | 아직 구현되지 않은 계약과 콘텐츠 이행 범위 |
| 9 | `docs/generated/`, `docs/implementation-reports/`, `docs/qa/` | 특정 시점의 생성 결과와 검증 증거 |
| 10 | 나머지 기존 설계 문서 | 상세 아이디어와 역사적 맥락 |

구현 사실은 현재 코드를 기준으로 판단하고, 실제 플레이 검증은 별도의 실행 증거로 분류한다.

## 구현 상태 관리

핸드북은 현재 구현된 시스템의 구조와 그 구조가 지향하는 플레이 경험을 설명한다. 구현 확인, 부분 이행, 미구현, 실행 검증 상태는 [시스템 구현 권위 체크리스트](system-implementation-checklist.md)에만 기록한다.

## 작성 자산 현황

현재 유형별 자산 수와 관계 수는 [콘텐츠 유형 색인](content-db/content-type-index.csv)과 [생성 요약](content-db/content-db-summary.json)이 소유한다. 수치를 이 문서에 복제하지 않으므로 콘텐츠 추가·이관 뒤에는 지식베이스 재생성만 수행하면 된다.

개별 아이템·시설·생산식·특성·사건·연구·전투·의료·세계 콘텐츠의 안정 식별자, 핵심 규칙, 참조 관계와 존재 이유는 [콘텐츠 데이터베이스](content-db/README.md)에서 조회한다. 연구의 다형성 시설·조합식 해금과 콘텐츠 측 연구 요구를 합친 결과는 [연구 해금 인덱스](knowledge-base/relations/research-unlocks.csv)에서 조회한다. 원본과 생성물의 일치 여부는 각 생성물의 `generation-manifest.json`으로 검증한다.

## 세부 설계 및 권위 기록

- [시스템 콘텐츠 통합 설계](game-design/systemic-content-integration-design.md): 사건과 선택을 실제 상태 변화·실행 기록·후속 반응으로 연결하는 목표 설계
- [시스템 콘텐츠 상세 카탈로그](game-design/systemic-content-event-catalog.md): 계획된 사건·문화·축제·계약 카탈로그
- [전략 빌드·연구·시설 포트폴리오](game-design/strategic-build-and-progression-design.md): 체크리스트형 진행을 전략 선택으로 바꾸는 목표 설계
- [전략 수직 슬라이스](game-design/strategic-vertical-slices.md): 식량, 동력, 방어의 대체 해법을 실제 운영 장면과 시설 전환으로 구체화한 설계
- [사건 수직 슬라이스](game-design/systemic-event-vertical-slices.md): 생애, 서비스, 계절, 세력 사건의 실제 원인·작업·영수증·후폭풍을 구체화한 설계
- [콘텐츠 설계 기록](game-design/content-plans/README.md): 연구, 시설, 생애 사건, 서비스 사고, 계절 사건, 세력 계약을 분리해 작성하는 목표 콘텐츠 표
- [상용·인디 시스템 게임 설계 비교](game-design/reference-game-design-synthesis.md): 출시작의 운영·물류·사건·인물 설계에서 DungeonStory에 적용할 원리와 제외할 구조를 정리한 비교 자료
- [코드 아키텍처 문서군](architecture/code-architecture-guide.md): 디자인 패턴, 확장 계약, 런타임 조립, 콘텐츠 디스패치, 응용 워크플로와 저장 트랜잭션을 주제별로 나눈 기술 문서
- [전체 런타임 구조](architecture/08-whole-runtime-topology.md): 작성 정의, 도메인 상태, 응용 조율, Unity 경계, Presentation과 영속성이 한 런타임으로 조립되는 구조
- [상태 권위 원장](architecture/09-state-authority-ledger.md): 주요 상태 가족별 작성 기준, 쓰기 권위, 명령, 읽기 투영, save section과 금지된 우회 경로
- [도메인 간 거래와 실패 경계](architecture/10-cross-domain-transactions.md): 건설, 생산, 수술, 전투, 시설, 사건, 원정과 복원에서 권위를 인계하고 재시도를 판정하는 방식
- [런타임 스케줄링과 읽기 투영](architecture/11-runtime-scheduling-and-projections.md): 게임·UI 시계, EntryPoint, 작업 예산, cadence, dirty, revision, cache와 복원 뒤 재구축 원칙
- [사용 이력 기반 진화 아키텍처](architecture/07-history-driven-evolution-architecture.md): 시설과 장비가 공유하는 이력 압축, 결정론적 후보, 진화 노드, 제한된 서술과 저장 가능한 작업 구조
- [시스템 구현과 성능 설계](architecture/systems/README.md): 각 런타임 시스템의 상태 권위, 실행 순서, 자료 구조, 적용된 최적화와 측정이 필요한 한계를 나눈 기술 문서
- [시설 합성과 사용 기반 진화](architecture/systems/19-facility-synthesis-and-use-based-evolution.md): 실물 합성, 작성 계보 교체, 개체별 이력 진화, 재조율, 포장 운반과 재설치를 구분한 시설 성장 구조
- [전체 게임 밸런스 기준서](game-design/whole-game-balance-baseline.md): 콘텐츠 변경 전에 따라야 하는 기록·검증 권위

## 편찬 기준

핸드북은 현재 코드에서 확인되는 상태의 책임, 행동이 시스템 사이를 건너는 경계, 물자 이동, 저장과 복원, 콘텐츠 조립 지점에 제작 의도와 재미의 근거를 연결한다. 대형 종합 문서와 생성 보고서는 세부 근거로 남긴다. 게임 기획의 완성도는 시스템 구성과 콘텐츠 상호작용을 기준으로 평가하며, 아직 제작하지 않은 화면 연출은 평가 범위에서 제외한다. 정확한 클래스명과 필드명은 핸드북 본문에 반복하지 않고 [근거 지도](handbook/appendix-source-map.md)와 [시스템 구현 권위 체크리스트](system-implementation-checklist.md)에서 관리한다.
