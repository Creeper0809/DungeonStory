# P3 11.1 오펜스 월드맵과 정찰 구현 보고

## 구현 범위

이번 단계는 오펜스 전체가 아니라 `월드맵에서 원정 대상을 발견하고 선택하는 단계`만 구현한다.

아직 구현하지 않은 것:

- 원정대 편성
- 원정 중 캐릭터 이탈 처리
- 자동 원정 전투
- 실제 보상 지급

이 네 가지는 11.2와 11.3에서 이어간다.

## 핵심 구조

추가 파일:

```text
Assets/Scripts/Offense/OffenseWorldMapSystem.cs
Assets/Scripts/Offense/Editor/OffenseWorldMapDebugScenarios.cs
```

`OffenseWorldMapSystem.cs`는 다음을 같이 가진다.

- 원정 대상 데이터 구조
- 월드맵 상태
- 정찰 범위 계산 서비스
- 월드맵 런타임
- 런타임 생성 월드맵 UI
- 월드맵 관련 이벤트

스크립트를 너무 많이 쪼개면 현재 프로젝트 구조가 더 난잡해질 수 있어서, 11.1의 응집된 기능은 한 파일 안에 묶었다.

## 원정 대상 데이터

원정 대상은 `OffenseTargetDefinition`으로 표현한다.

주요 값:

- `id`
- `title`
- `description`
- `kind`
- `distance`
- `danger`
- `durationSeconds`
- `requiredMembers`
- `requiredPower`
- `rewards`

즉 대상은 단순 버튼이 아니라, 이후 원정대 편성/전투/보상 계산으로 이어질 최소 정보를 가진다.

초기 기본 대상:

```text
외곽 식재료 농장
상단 길목
낡은 무기고
마력 유적
경쟁 던전 전초기지
```

## 정찰 로직

`OffenseWorldMapState`는 현재 정찰 레벨과 발견된 대상 ID를 가진다.

정찰 범위:

```text
Lv.0: 거리 8 이하
Lv.1: 거리 14 이하
Lv.2: 거리 22 이하
Lv.3: 전체
```

흐름:

```text
월드맵 시작
-> 현재 정찰 레벨의 범위 계산
-> 범위 안의 대상만 knownTargetIds에 추가
-> 월드맵 UI와 이벤트에 표시
```

정찰 강화 시:

```text
TryUpgradeRecon
-> 정찰 레벨 증가
-> 새 범위 안의 대상 공개
-> OffenseReconUpgradedEvent 발행
-> EventAlertService로 정찰 강화 알림 발행
```

## 월드맵 화면

`OffenseWorldMapPanel`은 런타임 생성 UI다.

구성:

- 상단: 월드맵 / 정찰 레벨 / 현재 스캔 범위
- 왼쪽: 발견된 원정 대상 버튼 목록
- 오른쪽: 선택한 대상 상세 정보
- 목록 하단: 정찰 강화 버튼

이 UI는 아직 최종 UX가 아니라, 11.1 시스템 검증용 화면이다.

## 이벤트

추가 이벤트:

- `OffenseWorldMapChangedEvent`
- `OffenseTargetSelectedEvent`
- `OffenseReconUpgradedEvent`

이벤트를 둔 이유:

- 이후 원정 편성 UI가 대상 선택을 구독할 수 있다.
- 운영일 정산이나 이벤트 로그에 정찰/원정 관련 기록을 남길 수 있다.
- 오펜스 결과가 메타 진행의 `오펜스 성과`로 이어질 수 있다.

## GameManager 연결

`GameManager.EnsureOperatingDaySystems`에서 `OffenseWorldMapRuntime`을 자동 부착한다.

즉 씬에 별도 오브젝트를 수동 배치하지 않아도 월드맵 런타임은 준비된다.

## 디버그 시나리오

Unity 메뉴:

```text
DungeonStory/Debug/Offense/Run P3 World Map Scenarios
```

검증 항목:

- 정찰 Lv.0에서 가까운 대상만 보이는가
- 정찰 강화로 더 먼 대상이 추가 공개되는가
- 대상이 위험도, 보상, 소요 시간, 필요 인력을 가지는가
- 대상 선택 이벤트가 발생하는가
- 월드맵 패널이 런타임 생성되는가

## 구현 판단

오펜스는 바로 자동 전투부터 만들면 위험하다.

이 게임에서 오펜스의 첫 재미는 `내 던전 밖에 무엇이 있고, 어느 위험을 감수할 것인가`를 고르는 데 있다.

그래서 11.1에서는 다음 감각을 먼저 만든다.

```text
처음에는 가까운 대상만 보인다.
정찰을 올리면 더 먼 대상이 열린다.
대상마다 위험과 보상이 다르다.
플레이어는 아직 출발시키기 전에도 선택을 고민할 수 있다.
```

다음 단계는 11.2 원정 편성이다.
