# 사건 수치 원장

사건은 텍스트 선택지가 아니라, 재고·공간·기한·관계·다음 압력의 상태를 바꾸는 운영 계약이다. 이 원장은 현재 V20 작성 에셋을 다시 읽고, 요구 아이템은 [아이템 수치 원장](../items/item-numeric-ledger.csv)의 현재 kg와 단가로 환산했다. 각 선택지의 요구는 합산하지 않는다. `choice_contracts`의 중괄호 하나가 하나의 선택지다.

| 원장 | 범위 | 행 수 |
|---|---|---:|
| [생애 사건](life-event-numeric-ledger.csv) | 기한·재발 주기·선택별 요구와 결과 | 32 |
| [서비스 사고](service-incident-numeric-ledger.csv) | 대응 세 갈래와 각 결과 | 8 |
| [계절 사건](seasonal-event-numeric-ledger.csv) | 계절·지속일·시작·일일·종료 효과 | 28 |
| [세력 계약](faction-contract-numeric-ledger.csv) | 화물 BOM·kg·단가·기한·성공·실패 결과 | 18 |
| [세력 장](faction-chapter-numeric-ledger.csv) | 장별 선택 BOM·kg·단가·시설 요구·장기 결과 | 36 |
| [손님 요청](guest-request-numeric-ledger.csv) | 서비스 BOM·kg·단가·객실·기한·성공·실패 결과 | 14 |
| [축제](festival-numeric-ledger.csv) | 계절일·준비 BOM·참가자·성공·부분·실패 결과 | 16 |
| [문화 관습](cultural-practice-numeric-ledger.csv) | 유지 BOM·kg·단가·시설·수용·방치 결과 | 20 |

사건 대응·파견·복구 WU는 [사건 대응 노동 원장](event-response-labor-model.md)이 소유한다. 각 사건의 물자 제작 WU와 사건 대응 WU를 분리해 같은 노동을 두 번 세지 않는다.

효과 표기는 현재 에셋의 효과 코드, 대상 ID, 변화량, 지속일을 보존한다. 코드는 [효과 종류표](effect-kind-legend.csv)에서 `세력 신뢰`, `세력 원한`, `세력 의무`, `화폐`, `작업 지연`, `위협도`, `세계 상태` 같은 실제 상태 변화로 해석한다. 이벤트 종류가 다른 효과를 하나의 화폐로 억지 환산하지 않는다. 이벤트별 비교에는 [전투·사건 판정선](../combat-and-event-bands.csv)의 준비·손실·회복 기준을 함께 적용한다.

축제는 [원본 대조](../festival-source-reconciliation.csv)에서 V20 경로가 단일 설계 권위로 확정된 16개 stable ID만 수치 원장에 넣었다. 구형 `Population/Festivals`의 중복 네 자산은 이 원장에 합산하지 않는다.

사건 작성 에셋은 대응 작업의 직접 WU를 직렬화하지 않는다. 사건 대응 노동 원장은 현재 BOM·kg·단가·기한·선택 갈래를 이용해 각 stable ID의 대응 WU를 문서 권위로 고정한다. 작업 주문이 생기면 해당 영수증은 같은 stable ID·공식·선택지와 결합되어야 한다.
