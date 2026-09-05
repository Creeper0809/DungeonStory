# DungeonStory 현행 게임 시스템 설계 핸드북

이 핸드북은 게임의 기획과 현재 시스템을 한 흐름으로 읽기 위한 편찬본이다. 각 장은 시스템이 맡는 게임 상태와 서로 맞물리는 지점에 더해, 플레이어에게 어떤 판단을 요구하는지, 그 판단이 왜 흥미로울 수 있는지, 어떤 실패 형태를 피하기 위해 현재 구조를 택했는지를 설명한다. 세부 수치 원장이나 구현 기록과 구현 상태표는 별도 권위 문서가 맡는다.

본문은 클래스명과 변수명보다 플레이 규칙과 콘텐츠 작성 언어를 우선한다. 정확한 코드 식별자와 위치가 필요할 때는 [코드와 문서의 근거 지도](appendix-source-map.md)를, 구현 여부를 확인할 때는 [시스템 구현 권위 체크리스트](../system-implementation-checklist.md)를 사용한다.

## 권장 열람 순서

1. [게임 정체성과 운영 순환](01-game-vision-and-player-loop.md)
2. [시스템 구성과 상태 권위](02-system-map-and-authority.md)
3. [공간 구축·시설·환경 시뮬레이션](03-world-building-facilities-environment.md): 실물 합성, 작성 계보 교체와 같은 개체에 쌓이는 진화를 구분한다
4. [물질 경제·생산·물류 체계](04-items-production-logistics-economy.md): 공통 kg 권위, 부분 운반, 저장 용량과 설명 가능한 물질수지를 포함한다
5. [인물·노동·사회·생명 체계](05-characters-ai-society-health.md)
6. [연구 체계와 전략적 진행](06-research-progression-and-strategy.md)
7. [무력 충돌·원정·세력 관계](07-combat-invasions-expeditions-factions.md)
8. [사건 콘텐츠의 작성과 시스템 통합](08-content-events-and-authoring.md)
9. [저장, 복원과 이어지는 세계](09-save-restore-determinism-and-validation.md)
10. [코드와 문서의 근거 지도](appendix-source-map.md)

상태 표기와 권위 순서는 상위 [문서 안내](../README.md)를 따른다.

구현 여부와 후속 과제는 별도 [시스템 구현 권위 체크리스트](../system-implementation-checklist.md)에서 관리한다.

출시작 비교에서 도출한 운영·물류·인물·사건 설계의 채택 기준은 [상용·인디 시스템 게임 설계 비교](../game-design/reference-game-design-synthesis.md)에 정리되어 있다.

클래스와 어셈블리 수준의 의존 관계, 적용된 디자인 패턴, 콘텐츠별 확장 계약과 저장 트랜잭션은 별도 [코드 아키텍처 문서군](../architecture/code-architecture-guide.md)에서 확인한다. 코드 전체의 조립 순서는 [전체 런타임 구조](../architecture/08-whole-runtime-topology.md), 시스템별 쓰기 권한은 [상태 권위 원장](../architecture/09-state-authority-ledger.md)에 정리되어 있다.

시설이 배치된 뒤 사용 기록과 방 맥락으로 서로 다른 계보를 형성하는 실행 구조는 [시설 합성과 사용 기반 진화](../architecture/systems/19-facility-synthesis-and-use-based-evolution.md)에 따로 정리되어 있다.
