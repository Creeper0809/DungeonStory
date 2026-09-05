# 전력망·축전·차단기 대조

상태: 부분 대조 완료, 전체 시스템 감사 진행 중. 스크립트·자산·공개 위키는 수정하지 않았다.

## 모집단과 판정

전력 모듈73개(발전3·소비67·축전1·차단2)는 모두 카탈로그와 공개 도감에 연결되어 있다. 작성값220개 중 실제전력계산에 직접쓰는191개가 도감에서 빠져 있다. 나머지는 자동화모드로 대체되는 소비량27개와 무연료발전기의 미사용연료설정2개다. 작성값과 플레이어에게 안내할 실제값을 구분한다. 위키facts는73개 모두 분류·크기만 표시한다.

## 발전기

| 시설 | 작성값 | 적용 시 주의 |
| --- | --- | --- |
| [마나 발전기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9812.json) | 발전/초: 32, 연료 필요: 1, 연료ID: resource:mana-crystal, 연료1개당 초: 90 | 현재 작성값 |
| [수차 발전기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9811.json) | 발전/초: 10, 연료 필요: 0, 연료ID: material:low-fuel, 연료1개당 초: 60 | 연료ID·가동시간은 사용하지 않음 |
| [증기 발전기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9810.json) | 발전/초: 18, 연료 필요: 1, 연료ID: material:low-fuel, 연료1개당 초: 60 | 현재 작성값 |

## 전력 소비 시설

| 시설 | 작성값 | 적용 시 주의 |
| --- | --- | --- |
| [컨베이어 필터](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9848.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [컨베이어 합류기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9847.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [컨베이어 분배기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9846.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [컨베이어 출력기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9845.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [컨베이어 입력기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9844.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [컨베이어 상향](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9842.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [컨베이어 우향](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9840.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [컨베이어 좌향](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9841.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [룬 조율실](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9826.json) | 소비/초: 9, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [컨베이어 하향](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9843.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [전기 제련 도가니](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9825.json) | 소비/초: 7, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [자동화 제어반](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9860.json) | 소비/초: 3, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [전기 아크등](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9824.json) | 소비/초: 1.5, 기본 우선순위: 3, 최소 공급비율: 1 | 현재 작성값 |
| [룬 정화 시설](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9822.json) | 소비/초: 4, 기본 우선순위: 3, 최소 공급비율: 1 | 현재 작성값 |
| [소독 정수기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9821.json) | 소비/초: 4, 기본 우선순위: 3, 최소 공급비율: 1 | 현재 작성값 |
| [물통 충전소](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9819.json) | 소비/초: 1.5, 기본 우선순위: 1, 최소 공급비율: 1 | 현재 작성값 |
| [자동 급배수 제어기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1715.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [자동 객실 배정판](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1714.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [전동 양수 펌프](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9816.json) | 소비/초: 4, 기본 우선순위: 1, 최소 공급비율: 1 | 현재 작성값 |
| [자동 계산대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1705.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [고속 컨베이어](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9852.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [보온 배식대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1704.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [오버플로 배출 게이트](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9851.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [층간 물류 리프트](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9850.json) | 소비/초: 0.4, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [우선순위 게이트](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9849.json) | 소비/초: 0.5, 기본 우선순위: 4, 최소 공급비율: 0.5 | 현재 작성값 |
| [대장작업대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1019.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [폐기 소각로](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1097.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [대장간](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1093.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [몽직기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1092.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [연금대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1091.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [약제대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1090.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [사료 배합대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1089.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [훈연대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1088.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [조리대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1087.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [증류기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1086.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [퇴비장](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1085.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [무두질대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1084.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [직조기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1083.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [비전 단조대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1082.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [귀금 세공대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1081.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [제강로](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1080.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [용광로](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1079.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [광석 선별대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1078.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [석재 절단대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1077.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [숯가마](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1076.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [제재소](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1075.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [양조장](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1074.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [제분소](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1073.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [전리품거치대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1049.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [배기팬](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1505.json) | 소비/초: 5, 기본 우선순위: 3, 최소 공급비율: 0.5 | 현재 작성값 |
| [송풍구](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1504.json) | 소비/초: 3, 기본 우선순위: 3, 최소 공급비율: 0.5 | 현재 작성값 |
| [공조기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1502.json) | 소비/초: 10, 기본 우선순위: 3, 최소 공급비율: 0.75 | 현재 작성값 |
| [냉각기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1501.json) | 소비/초: 8, 기본 우선순위: 3, 최소 공급비율: 0.75 | 현재 작성값 |
| [조리손질대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1002.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [고기그릴](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1001.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [간이화덕](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1000.json) | 소비/초: 5, 기본 우선순위: 4, 최소 공급비율: 0.75 | 소비량5 고정이 아니라 자동화 모드 프로필 사용 |
| [온도 제어 발효조](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1603.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [세척·병입대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1605.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [분별 증류탑](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1606.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [전기 오븐](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1609.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [냉장 준비대](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1611.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [연기 포집 후드](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1617.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [정밀 연마기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1619.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [마나 안정기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1622.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [무균 약품 보관함](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1624.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [실내 생장 제어기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1627.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |
| [마나 응축기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1625.json) | 소비/초: 1, 기본 우선순위: 4, 최소 공급비율: 1 | 현재 작성값 |

## 축전지

| 시설 | 작성값 | 적용 시 주의 |
| --- | --- | --- |
| [축전지](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9813.json) | 용량: 240, 이송/초: 30, 작성 효율: 0.92 | 현재 작성값 |

## 차단기

| 시설 | 작성값 | 적용 시 주의 |
| --- | --- | --- |
| [변압 제어반](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9815.json) | 허용 부하비율: 1.3, 차단 열: 130 | 현재 작성값 |
| [회로 차단기](F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9814.json) | 허용 부하비율: 1.15, 차단 열: 100 | 현재 작성값 |

## 공통 규칙

- 연결: 전력 채널이 있는 노드의 점유칸이 겹치거나 상하좌우로 닿으면 같은 전력망이다. 대각선은 제외된다. 전력 모듈은 연결 모듈과 별도로 Power 채널을 추가한다.
- 배분: Critical1→Defense2→Essential3→Production4→Optional5, 동순위NodeId순. requested<=0.001이면비율1; 그외fraction=min(requested,max(0,available))/requested. fraction+0.001>=clamp01(minimumSupplyFraction)일 때만Powered=true이며granted를available에서차감.
- 우선순위 조작: 산업 패널 전력 소비 카드의 다음 버튼으로5단계 순환. 수요>0인 노드를 정전 여부·고장내림차순·우선순위·ID순으로 최대40개 표시.
- 발전량: 연료 조건을 통과한 발전기는max(0,작성발전량)×clamp01(1-Fault/125)를 생산한다. 발전량·수요 단위를 근거 없이kW로 바꾸지 않는다.
- 연료: 지정연료1개를 현장물리버퍼에서 소비하여max(1,secondsPerFuel)초를 얻는다. 연료 운반 요청은10초 간격, 버퍼는지정연료4개분의질량. 유효 연료시간이 남아야 생산하며 망 차단 때는 발전/연료시간감소분기를 건너뛴다. 수차는연료조건없음이며 미사용 연료ID/시간을 실제비용으로 표시하지 않는다.
- 축전 효율: η=1-(1-clamp01(작성효율))×clamp(이정표ManaTransferLossMultiplier,0,1). 충전·방전양쪽에η가적용된다. 기본0.92에서이정표배율1이면단순왕복η²=0.8464.
- 충방전: 부족분과이송한도및저장량까지먼저원량을제거하고그양×η를공급한다. 충전은입력×η와빈용량중작은양을저장한다. 충전/방전은노드ID순. 실제잔여량차감및유실예외는별도구현확인항목참조.
- 과부하: R=총요청수요/max(0.01,현재발전량). 축전방전량과실제승인공급은분모에포함하지않는다. R>1이면Heat+=(R-1)×18×delta; 아니면Heat=max(0,Heat-8×delta). Heat>75이면Fault=clamp(Fault+(Heat-75)×0.02×delta,0,100).
- 차단과 복구: R>max(1,overloadTolerance) AND Heat>=max(1,tripHeat)이면차단. 같은망차단기하나라도차단되면이후평가에서망의발전·방전이중단된다. 복구버튼은Heat<60일때만성공하며BreakerTripped=false,Fault=max(0,Fault-10).
- 저장과 재계산: 저장원본은우선순위·축전량·남은연료시간·열·고장·차단상태·연료작업순번과진행영수증. 생산/수요/현재공급결과는저장하지않고원본에서재계산한다. 정전상태불러오기를고장초기화로안내하면안된다.

## 구현 확인 후보

- 차단 중 수요가 남으면 발전0으로 과부하열이 계속 증가하여 단순 대기로Heat60 미만 복구가 불가능할 수 있음
- 축전 방전 원량에서 손실을 먼저 고려하지 않아 남은 축전기가 있어도 손실분 부족을 채우지 않음
- 가득 찬 앞 축전기의 충전 입력 차감이 뒤 축전기에 갈 전력을 소모할 수 있음
- 공급 최소기준 미달로소비자가가동하지않아도배터리는배분전에이미방전될수있음
- normallyOpen/maxThroughput은비Editor검색에서선언외소비확인못함
- 수차 이름만으로 수원·유속 조건을 추가하지 않음; 해당 작성/배치 제약의 전체검토는미완료
- UI 우선순위 최대40개 제한과 실제수요0 제외로 전체시설의설정노출이제한됨
- 노드UI발전량은작성값이고망총발전량은Fault보정값이므로같은측정값으로보면안됨

## 확인 범위

ElectricalNetworkRuntime 및 IndustrialInfrastructureTopology 전문, 전력UI/저장 관련 구간과73개 자산의 전력필드를 직접대조했다. 생성KB는stale(469실패·0행)라현재근거로사용하지않았다. query·digest·원본경로는 [기계 판독 보고서](power-mechanics-review.json)에 있다. 직접소비필드 분류는 필드가 계산에 쓰인다는 뜻이며73개 시설 모두의 실제생산/생활효과 실행을 검증했다는 뜻은 아니다. 실제Unity/UI·저장왕복은미실행이다.
