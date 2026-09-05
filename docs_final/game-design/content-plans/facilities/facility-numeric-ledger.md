# 시설 수치 원장

시설은 고정된 건물 목록이 아니라, 같은 공간을 어떤 기능으로 조립하고 다시 전환할지를 결정하는 물리 부품이다. 이 원장은 현재 직렬화 `BuildingSO` 419개에서 건설 BOM, 설치·수리·청소·운전 WU, 점유 셀, 유지비, 해체 회수율, 처리량 필드를 다시 읽어 고정한다.

| 원장 | 범위 | 행 수 |
|---|---|---:|
| [월드·지형·앵커](facility-numeric-ledger-world-and-topology.csv) | 월드 자원 노드, 토폴로지, 런타임 앵커, 이정표 | 103 |
| [조립 부품](facility-numeric-ledger-modular.csv) | 생활·위생·보관·식사·보안·환경·의례를 재조합하는 모듈 | 83 |
| [연구 확장 시설](facility-numeric-ledger-research-overhaul.csv) | 연구 해금으로 이어지는 고급 기능 부품 | 101 |
| [생산·산업 시설](facility-numeric-ledger-production.csv) | 생산 지지대, P1, 산업, 의복 생산 라인 | 88 |
| [전문 시설](facility-numeric-ledger-specialized.csv) | 의료·서비스·전투·구금·독립 상점 계열 | 44 |

각 행의 `construction_bom`은 실제 건설 작업이 소비하는 아이템과 수량이다. `construction_wu`, `repair_wu`, `clean_wu`, `operate_wu`는 해당 작업을 끝내기 위한 직접 노동량이다. `cells`는 공간 기회비용, `construction_value`·`construction_cost`·`maintenance`·`maintenance_per_game_hour`는 화폐와 지속 부담, `demolition_refund_rate`는 전환 뒤 회수 가능한 가치다.

처리량은 하나의 숫자로 축약하지 않는다. 기능에 따라 병목의 위치가 다르기 때문이다.

- `facility_capacity`: 작업 슬롯 또는 수용량
- `default_batch_capacity`: 한 생산 배치가 쓰는 기본 투입·산출 단위
- `output_buffer_cycles`: 물류가 끊겨도 버틸 수 있는 생산 버퍼
- `service_capacity`: 서비스 허브의 동시 수용량
- `utility_max_throughput`: 전력·물·열·배관 등 기반망의 최대 흐름
- `process_work_seconds`: 개별 기능의 한 회 처리 시간

`—`는 해당 능력 모듈이 없는 시설이며, `0`은 에셋에 명시된 실제 값이다. 이 구분이 없으면 비용이 없는 시설과 비용 체계 밖의 시설을 같은 것으로 취급하게 된다.

시설을 추가하거나 수치를 바꿀 때는 이 다섯 원장의 같은 분류 파일에 행을 추가하고, 다음 네 값을 함께 재판정한다.

```text
설치 투자 = 건설 BOM의 조달 EWU + construction_wu + 운반 WU
전환 손실 = 철거·운반·청소 WU + (1 - demolition_refund_rate) × 회수 불가 가치
공간 효율 = 실효 처리량 ÷ 점유 cells
기반망 압력 = utility_max_throughput을 넘는 동시 가동 수
```

현재 전수 대조에서는 원장 5개 파일의 ID 419개가 건물 자산 분류 419개와 일치한다. 건설 WU와 건설 BOM이 없는 행은 없다. 이 수치는 방 인정, 시설 합성, 서비스·의료·생산 처리량, 해체 후 재조립의 정적 비교에 사용한다.
