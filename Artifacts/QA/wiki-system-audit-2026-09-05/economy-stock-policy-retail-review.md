# 경제 재고 정책·자동 판매·소매 운영 후속 감사

상태: 정적 원본 대조 완료. Unity UI 클릭·Play Mode·저장 왕복 재현은 실행하지 않았다.

## 대조 범위

- 공개 가이드: `economy-and-trade.md`, `guests-services-and-performance.md`
- 재고 정책: 정의, 창고 UI 명령, 주기 평가, 판매/가공/퇴비화/해체, 물리 집결, 보류 정산, 저장
- 소매: 상점 재고 lot, 창고 보충, 가격, 직원 계산대/셀프서비스, 손님 구매, 절도, 저장, 운영 UI
- 시설 도감의 `분류=상점`은 `BuildingCategory.Shop` 건설 메뉴 분류이므로 `BuildingRetailAbility`와 같은 뜻으로 해석하지 않았다.

## 결론

위키의 가격 기준과 자동 판매의 핵심 문장은 현재 코드와 일치한다. `1 gold = 3 EWU`, 외부 구매 목표 1.35배, 외부 판매 회수율 0.60, 일반 소매 1.20배, 품질 계수와 평가당 최대 4개는 대응한다. 판매는 목적지에 실제 집결된 물량을 먼저 Transfer 처리하고 멱등 수입을 지급한다.

그러나 아래 두 운영 시스템은 위키에서 이름 수준만 보이고, 플레이어가 실제로 조작·판단하는 규칙은 누락됐다.

1. `GAP-139`: 품목별 최소/목표/최대 재고, 비상 비축, 초과 처리 다섯 분기와 물리 집결·보류 정산.
2. `GAP-140`: 소매 시설의 exact lot 재고, 창고 보충, 직원 계산대/셀프서비스, 대기 이탈, 가격 배율, 절도와 저장.

## ECON-STOCK-001 재고 정책

### 실제 입력과 UI

- 카탈로그 품목마다 정책을 조회할 수 있으며 신규 기본값은 `최소 10 / 목표 20 / 최대 40`, 비활성, 초과분 보관이다.
- 창고 화면에서 정책 켜기/끄기, 최소·목표·최대 각각 ±5, 초과 처리 순환, 비상 비축 토글을 실제 명령으로 호출한다.
- 임계값은 `0 <= minimum <= target <= maximum`으로 정규화된다.
- 비상 비축은 활성 정책만 가능하며 최소 수량 충족 여부가 별도 readiness로 집계된다.

### 초과 처리

| 값 | 실제 동작 |
| --- | --- |
| Hold | 최대 초과분을 그대로 보관한다. |
| Sell | 하차장 쪽 exact destination을 만들고 초과 재고를 실제 운반한다. 목적지의 `FacilityBuffer` 수량이 최소 판매 묶음 이상일 때 Transfer 영수증을 만든 뒤 수입을 멱등 지급한다. |
| Process | 해당 입력을 쓰는 일반 가공 조합식을 찾아 생산 주문을 만든다. |
| Compost | 퇴비 출력 조합식만 고른다. |
| Dismantle | 회수/해체 계열 조합식만 고른다. |

- 보유량은 같은 item ID의 전체 월드 스택 중 이미 판매·지역계약·대형프로젝트·품질미달판매 목적지로 나간 물량을 제외해 계산한다.
- 판매 가능 여부는 `UnitPrice > 0 && MarketSaleRate > 0`이다.
- 단위 판매수입은 `unitPrice * saleRate`; 1골드 미만이면 합계가 1골드가 되는 최소 묶음을 기다린다. 정산은 `floor(delivered * unitPrice * saleRate)`이다.
- 판매 입력 owner, 물리 Transfer receipt, pending sale, 다음 sequence가 저장된다. 재시작 뒤 물리는 이미 사라졌지만 수입만 미지급된 상태를 복구하도록 설계돼 있다.
- 품질 미달 장비·의복 판매는 일반 초과 정책과 별도 outbox다. exact unique lot, 품질, 장비 world-state, 물리 영수증과 수입·권위 해제 단계를 저장한다.

### 위키 공백

`economy-and-trade.md`는 물리 판매와 가격식은 설명하지만 위 정책 입력, 기본값, 비상 비축, 다섯 처리 분기, 판매 집결 실패 상태, 저장·복구 의미를 설명하지 않는다. `inventory-and-carrying.md`에도 “버퍼, 목표 재고와 자동화” 소유 절은 있으나 이 구체 계약은 없다.

## ECON-RETAIL-001 소매 시설

### 실제 재고와 보충

- 현재 modular 작성 자산 중 `BuildingRetailAbility` 보유 시설은 7개다: 판매카운터, 잡화진열선반, 잠금진열장, 잡화상자, 무기거치대, 갑옷거치대, 무기보관함.
- 상점 재고 권위는 `RetailStockLotSnapshot`이다. 일반품 lot도 item ID·수량·unit grams·source operation을 가지며, unique 장비/의복은 instance ID와 component fingerprint를 가진 정확히 1개짜리 lot만 허용한다.
- 작성 초기 재고는 일반품만 물리 lot으로 활성화할 수 있다. unique item은 count-only 작성값으로 인스턴스를 만들어내지 않고 창고에서 exact physical restock으로 들어와야 한다.
- 보충은 상점이 허용하는 상품 정의와 창고의 정확한 item 재고를 찾고, 별도 운반 작업이 exact lot을 상점에 인계해야 재고가 증가한다.
- 내부 재고 정원과 같은 방의 카테고리별 저장 보조 정원을 합쳐 수용량을 계산한다. 상점 재고·활성화된 작성 상품·진행 중 보충 operation ID는 시설 동적 저장 상태에 포함된다.
- 현재 공개 시설 중 `BuildingRequiresStockAbility`를 가진 것은 판매카운터 S01(id1012) 한 개다. 나머지10개 작성 자산은 deprecated compatibility 시설이다. S01은 내부 재고가 비면 방문 admission을 `재고 없음`으로 거부하고, AI 시설 후보의 재고 점수는 `CurrentStock / max(1, InternalStockCapacity)`로 내려간다. restock 판정은 일반 임계값보다 먼저 빈 required stock을 즉시 보충 대상으로 만든다. 이 marker와 세 소비 경로는 공개 시설 페이지와 가이드에 없다.

### 구매·가격·서비스

- 기본 소매가는 내부 단가의 1.20배를 올림해 최소 1골드로 만든다. 시설별 작성 배율과 계산 직원의 revenue multiplier가 추가되며 직원 가산은 최대 1.15다. 실제 장바구니 가격은 최종 배율을 곱해 내림한다.
- 내부 직원 이용은 수입을 만들지 않는다. 외부 손님 구매만 `GuestServiceIncome`으로 중앙 금화 권위에 입금된다.
- 시설이 staffed service와 Operate 업무를 모두 요구하면 계산 직원이 필요하다. 그렇지 않으면 셀프 계산대다.
- 직원이 필요한데 없으면 손님은 대기하며, 성격 인내·보이는 대기열에 따라 초조/서비스 요청/포기 단계로 진행한다. 포기 시 기분·관계·활동 기록과 경고가 생긴다.
- 구매 성공 시 exact retail lot 하나가 외부 Sink로 commit되고 나서 수입이 지급된다.

### 절도

- 외부 손님만 절도 판정을 한다. 직원 감독, 대기열/혼잡, 손님 욕구, 장바구니 가치, 시설 손상, 손님 범죄 배율이 확률에 들어간다.
- 절도가 발생하면 exact lot 하나를 먼저 꺼내 물리 external Sink로 commit한다. commit 실패 시 같은 lot을 복원하며, 성공하면 손실가·범죄 사건·활동을 기록하고 판매 수입은 없다.
- 상점 화면은 현재 재고/정원, 대기 인원, 직원 필요 또는 셀프 계산대, 가격 배율, 1개 장바구니 기준 절도 위험, 상품별 가격·수량을 표시한다.

### 위키 공백

`guests-services-and-performance.md`는 상점 경로와 실제 물건 이동을 일반적으로만 말한다. 위의 재고 lot/unique 제약, 보충 source, 정원, required-stock 빈재고 입장 거부·후보 점수·즉시 보충, 직원·셀프서비스 분기, 가격 가산, 계산 대기 단계, exact purchase commit, 절도 실패 복원과 저장 권위는 설명하지 않는다. 시설 도감도 역할·크기·건설비 중심이라 개별 retail category/stock/checkout 조건을 보여 주지 않는다.

## 정상 일치 및 오판 방지

- 위키의 외부 판매 중앙값 `0.20 gold/EWU`는 `1/3 gold/EWU * 0.60`과 일치한다.
- 품질 계수 0.70/0.82/1.00/1.08/1.16/1.26/1.40과 최대 4개 정산은 일치한다. 신화품 1.60은 자동 품질미달 판매에서 제외되므로 표에 없는 것이 수치 오류는 아니다.
- `building-1000` 등 22개 시설의 `분류=상점`은 broad build category다. 실제 소매 능력 7개와 다르지만, 이 사실만으로 도감 오류라고 세지 않는다.
- 실제 클릭·손님 방문·저장/복원을 이번 읽기 전용 감사에서 실행하지 않았다. 비Editor UI command 연결과 저장 section 등록은 정적으로 확인했다.

## 직접 근거

기계 판독 가능한 경로·SHA-256은 `economy-stock-policy-retail-evidence.json`에 기록한다.
