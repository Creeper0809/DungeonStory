# 시설 건설 비용 전수 대조

상태: 이 보고서의 건설 WU·BOM 대조 완료. 전체 시스템·위키 감사는 진행 중이다.

## 범위와 결론

Resources의 BuildingSO 419개를 MonoScript GUID로 전수 열거했다. 일반 시설 347개·랜드마크 9개·내부 작업 대상 42개는 공개 도감과 연결되고, 폐기된 호환용 정의 21개는 공개 제외 대상이다. 시설 도감의 나머지 27페이지는 청사진 7·진화 6·합성 9·서비스 5개로, 건축물 비용 비교에 섞지 않았다.

- GAP-109: 건설 WU가 다른 시설 103개와 재료 수량이 다른 시설 42개. 중복을 뺀 시설은 144개다.
- GAP-110: 시설 91개의 건설 재료 142행이 '외 N개'로 생략됐다.
- GAP-111: 랜드마크 9개에서 건설 WU 9개와 재료 39행이 빠지고 영어 작성용 설명이 나온다.

일반 시설 347개의 전체 BOM 1113행은 929행 일치, 42행 수량 오류, 142행 미표시다. 공개 BuildingSO 398개 모두 facts는 분류·크기뿐이며 자체 관계는 없다. 역참조 321개는 연구 해금 272개와 시설 관계 49개이고 아이템 역참조는 0개다. 따라서 빠진 건설 재료를 화면의 다른 관계표에서 확인할 수도 없다.

전체 419행, 폐기된 정의 21개와 내부 대상 42개, 나머지 시설 도감 27개의 경로·현재 값·공개 값은 [기계 판독 원장](facility-construction-review.json)에 보존했다. 해시 추적은 파일 완독이나 실제 게임 실행을 의미하지 않는다.

## 현재 건설 비용의 권위

[V27BalanceWorkCalculator](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs>)는 자산에 적힌 건설 WU를 그대로 반환한다. [실제 건설 주문 생성](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs>)도 이 계산기와 GetConstructionMaterials를 읽는다. 사용 가능한 설치 키트를 선택하는 경로는 BOM 대신 키트 1개를 요구하며, 여기의 표는 일반 건설 재료 비용이다.

도감은 생성된 설명을 표시한다. [콘텐츠 설명 생성기](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Tools/Documentation/generate_content_database.py>)는 건설 재료를 네 종류까지만 요약하고, [위키 요약 선택](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Tools/Wiki/generate_wiki_model.py>)은 기존 description을 우선한다. [실제 도감 구성](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/src/components/EntryContent.astro>)과 [데이터 조회](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/src/lib/wiki-data.ts>)에는 Unity 자산에서 건설 비용을 다시 계산하는 경로가 없다.

## GAP-109: 현재 작성값과 다른 비용

표의 화살표는 '위키 표시 → 현재 자산'이다. 현재 작성값을 승인되지 않은 새 밸런스로 재설계하는 것이 아니라, 사용 중인 건설 비용과 문서의 차이를 기록한다.

| 시설 | 건설 WU | 재료 수량 |
| --- | --- | --- |
| [간이화덕 (1000)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1000.json>) | 261 → 322 | 일치 |
| [고기그릴 (1001)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1001.json>) | 364 → 476 | 일치 |
| [조리손질대 (1002)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1002.json>) | 일치 | 처리 목재 9 → 13 |
| [배식카운터 (1003)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1003.json>) | 451 → 509 | 일치 |
| [고기걸이 (1010)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1010.json>) | 일치 | 목재 5 → 7 |
| [술음료장 (1011)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1011.json>) | 241 → 280 | 일치 |
| [판매카운터 (1012)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1012.json>) | 141 → 197 | 일치 |
| [잠금진열장 (1014)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1014.json>) | 428 → 469 | 일치 |
| [대장작업대 (1019)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1019.json>) | 일치 | 처리 목재 9 → 13 |
| [이층침대 (1022)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1022.json>) | 일치 | 천 8 → 11 |
| [옷장 (1025)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1025.json>) | 일치 | 처리 목재 5 → 6 |
| [개인보관함 (1028)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1028.json>) | 일치 | 처리 목재 5 → 6 |
| [연구책상 (1030)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1030.json>) | 일치 | 처리 목재 8 → 12 |
| [연금술작업대 (1031)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1031.json>) | 312 → 477 | 일치 |
| [표본보관장 (1034)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1034.json>) | 일치 | 처리 목재 4 → 5 |
| [훈련허수아비 (1040)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1040.json>) | 316 → 383 | 일치 |
| [사격과녁 (1041)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1041.json>) | 316 → 383 | 일치 |
| [중량훈련석 (1042)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1042.json>) | 324 → 385 | 일치 |
| [대련매트 (1043)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1043.json>) | 393 → 449 | 일치 |
| [경비초소책상 (1044)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1044.json>) | 일치 | 처리 목재 8 → 12 |
| [경보종 (1045)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1045.json>) | 424 → 593 | 일치 |
| [순찰상황판 (1046)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1046.json>) | 일치 | 처리 목재 8 → 12 |
| [전술지도탁자 (1047)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1047.json>) | 594 → 633 | 일치 |
| [전투깃발 (1048)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1048.json>) | 447 → 503 | 일치 |
| [전리품거치대 (1049)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1049.json>) | 501 → 581 | 일치 |
| [대형보관선반 (1050)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1050.json>) | 141 → 199 | 일치 |
| [통더미 (1052)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1052.json>) | 일치 | 처리 목재 5 → 6 |
| [변기 (1057)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1057.json>) | 440 → 499 | 일치 |
| [세면대 (1059)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1059.json>) | 368 → 484 | 일치 |
| [목욕통 (1060)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1060.json>) | 637 → 738 | 일치 |
| [바닥배수구 (1063)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1063.json>) | 368 → 484 | 일치 |
| [벽횃불 (1064)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1064.json>) | 465 → 521 | 일치 |
| [바닥화로 (1065)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1065.json>) | 423 → 484 | 일치 |
| [샹들리에 (1066)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1066.json>) | 514 → 683 | 일치 |
| [촛대 (1070)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1070.json>) | 474 → 530 | 일치 |
| [해골피장식 (1071)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1071.json>) | 63 → 64 | 일치 |
| [제분소 (1073)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1073.json>) | 312 → 467 | 일치 |
| [양조장 (1074)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1074.json>) | 312 → 467 | 일치 |
| [제재소 (1075)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1075.json>) | 384 → 555 | 일치 |
| [숯가마 (1076)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1076.json>) | 318 → 476 | 일치 |
| [석재 절단대 (1077)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1077.json>) | 312 → 467 | 일치 |
| [광석 선별대 (1078)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1078.json>) | 312 → 467 | 일치 |
| [용광로 (1079)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1079.json>) | 318 → 476 | 일치 |
| [직조기 (1083)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1083.json>) | 312 → 467 | 일치 |
| [무두질대 (1084)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1084.json>) | 312 → 467 | 일치 |
| [퇴비장 (1085)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1085.json>) | 312 → 467 | 일치 |
| [조리대 (1087)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1087.json>) | 312 → 467 | 일치 |
| [훈연대 (1088)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1088.json>) | 318 → 476 | 일치 |
| [사료 배합대 (1089)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1089.json>) | 312 → 467 | 일치 |
| [약제대 (1090)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1090.json>) | 312 → 467 | 일치 |
| [야외 경작지 (1095)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1095.json>) | 일치 | 철괴 2 → 3 |
| [실내 재배조 (1096)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1096.json>) | 384 → 569 | 일치 |
| [폐기 소각로 (1097)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1097.json>) | 895 → 1205 | 일치 |
| [중앙 무대 (1201)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1201.json>) | 372 → 526 | 일치 |
| [냉각기 (1501)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1501.json>) | 456 → 683 | 일치 |
| [공조기 (1502)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1502.json>) | 456 → 683 | 일치 |
| [환기덕트 (1503)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1503.json>) | 353 → 494 | 일치 |
| [송풍구 (1504)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1504.json>) | 354 → 530 | 일치 |
| [배기팬 (1505)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1505.json>) | 354 → 530 | 일치 |
| [담금·당화조 (1601)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1601.json>) | 67 → 495 | 일치 |
| [온도 제어 발효조 (1603)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1603.json>) | 90 → 539 | 일치 |
| [세척·병입대 (1605)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1605.json>) | 90 → 539 | 일치 |
| [분별 증류탑 (1606)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1606.json>) | 90 → 539 | 일치 |
| [전기 오븐 (1609)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1609.json>) | 90 → 539 | 일치 |
| [세척·전처리 싱크 (1610)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1610.json>) | 67 → 495 | 일치 |
| [냉장 준비대 (1611)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1611.json>) | 90 → 539 | 일치 |
| [염장·절임조 (1613)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1613.json>) | 67 → 495 | 일치 |
| [치즈 응고조 (1614)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1614.json>) | 67 → 495 | 일치 |
| [연기 포집 후드 (1617)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1617.json>) | 90 → 539 | 일치 |
| [목재 처리조 (1618)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1618.json>) | 67 → 495 | 일치 |
| [정밀 연마기 (1619)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1619.json>) | 90 → 539 | 일치 |
| [마나 안정기 (1622)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1622.json>) | 90 → 539 | 일치 |
| [무균 약품 보관함 (1624)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1624.json>) | 90 → 539 | 일치 |
| [마나 응축기 (1625)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1625.json>) | 90 → 539 | 일치 |
| [실내 생장 제어기 (1627)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1627.json>) | 90 → 539 | 일치 |
| [순번판 (1703)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1703.json>) | 일치 | 목재 6 → 8 |
| [슬라임 전용 좌석 (1706)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1706.json>) | 일치 | 목재 6 → 8 |
| [오크 전용 좌석 (1707)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1707.json>) | 일치 | 목재 6 → 8 |
| [뱀파이어 전용 좌석 (1708)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1708.json>) | 일치 | 목재 6 → 8 |
| [객실 정리함 (1709)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1709.json>) | 일치 | 목재 6 → 8 |
| [목욕 위생대 (1710)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1710.json>) | 일치 | 목재 6 → 8 |
| [의료 호출판 (1711)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1711.json>) | 일치 | 목재 6 → 8 |
| [복도 침입 감지기 (1800)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1800.json>) | 일치 | 처리 목재 8 → 12 |
| [방어 통제대 (1801)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1801.json>) | 일치 | 처리 목재 11 → 16 |
| [탄약·촉매 보급고 (1802)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1802.json>) | 510 → 603 | 일치 |
| [함정 정비대 (1803)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1803.json>) | 510 → 603 | 일치 |
| [채집 바구니 작업대 (8801)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8801.json>) | 일치 | 처리 목재 5 → 7 |
| [중력식 수문 (8802)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8802.json>) | 일치 | 철괴 6 → 7 |
| [가격표 게시판 (8807)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8807.json>) | 일치 | 철괴 2 → 3 |
| [피의 무대 배수구 (8808)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8808.json>) | 276 → 422 | 일치 |
| [공연 소품 보관대 (8811)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8811.json>) | 일치 | 철괴 2 → 3 |
| [벌목 키트 걸이 (8814)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8814.json>) | 일치 | 처리 목재 5 → 7 |
| [쐐기 도끼 작업대 (8815)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8815.json>) | 612 → 877 | 일치 |
| [혈통 촉진제 선반 (8818)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8818.json>) | 504 → 732 | 일치 |
| [마구 선반 (8819)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8819.json>) | 일치 | 처리 목재 5 → 7 |
| [조련용 고삐 걸이 (8820)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8820.json>) | 일치 | 처리 목재 5 → 7 |
| [무기 도면걸이 (8834)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8834.json>) | 일치 | 처리 목재 3 → 5 |
| [배식 운영판 (8848)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8848.json>) | 일치 | 철괴 2 → 3 |
| [작물 달력대 (8850)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8850.json>) | 일치 | 처리 목재 2 → 3 |
| [가구 등록대 (8857)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8857.json>) | 일치 | 처리 목재 3 → 5 |
| [도제 작업대 (8861)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8861.json>) | 504 → 732 | 일치 |
| [계보 관리실 (8862)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8862.json>) | 일치 | 처리 목재 3 → 5 |
| [역학 상황판 (8877)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8877.json>) | 일치 | 처리 목재 3 → 5 |
| [유전 기록고 (8878)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8878.json>) | 일치 | 처리 목재 3 → 5 |
| [방 배정대 (8882)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8882.json>) | 일치 | 철괴 3 → 4 |
| [보호자 등록소 (8884)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8884.json>) | 일치 | 처리 목재 3 → 5 |
| [상담실 (8885)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8885.json>) | 일치 | 목재 15 → 19 |
| [추모실 (8887)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8887.json>) | 일치 | 목재 15 → 19 |
| [종자 선별대 (8890)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8890.json>) | 504 → 732 | 일치 |
| [재단·재봉 작업대 (9301)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9301.json>) | 249 → 306 | 일치 |
| [문양·장식 작업대 (9302)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9302.json>) | 249 → 333 | 일치 |
| [손세탁 수조 (9303)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9303.json>) | 653 → 647 | 일치 |
| [실내 건조대 (9304)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9304.json>) | 609 → 675 | 일치 |
| [의복 진열대 (9306)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9306.json>) | 일치 | 목재 6 → 9 |
| [탈의 칸막이 (9307)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9307.json>) | 609 → 675 | 일치 |
| [수선 접수대 (9308)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9308.json>) | 609 → 675 | 일치 |
| [섬유 선별대 (9309)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9309.json>) | 249 → 333 | 일치 |
| [침지·정련조 (9310)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9310.json>) | 307 → 315 | 목재 2 → 3 |
| [수동 방적기 (9311)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9311.json>) | 249 → 306 | 일치 |
| [축융·마감대 (9312)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9312.json>) | 249 → 333 | 일치 |
| [재활 보조대 (9507)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9507.json>) | 637 → 854 | 일치 |
| [전력선 (9801)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9801.json>) | 404 → 463 | 일치 |
| [상수관 (9802)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9802.json>) | 404 → 463 | 일치 |
| [하수관 (9803)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9803.json>) | 404 → 463 | 일치 |
| [통합 기반 덕트 (9804)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9804.json>) | 353 → 494 | 일치 |
| [물통 충전소 (9819)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9819.json>) | 일치 | 철괴 5 → 6 |
| [오수 침전조 (9820)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9820.json>) | 591 → 845 | 일치 |
| [소독 정수기 (9821)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9821.json>) | 591 → 845 | 일치 |
| [룬 정화 시설 (9822)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9822.json>) | 582 → 882 | 일치 |
| [샤워 시설 (9823)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9823.json>) | 389 → 530 | 일치 |
| [전기 아크등 (9824)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9824.json>) | 374 → 531 | 일치 |
| [전기 제련 도가니 (9825)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9825.json>) | 355 → 467 | 일치 |
| [컨베이어 우향 (9840)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9840.json>) | 618 → 883 | 일치 |
| [컨베이어 좌향 (9841)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9841.json>) | 618 → 883 | 일치 |
| [컨베이어 상향 (9842)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9842.json>) | 618 → 883 | 일치 |
| [컨베이어 하향 (9843)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9843.json>) | 618 → 883 | 일치 |
| [컨베이어 입력기 (9844)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9844.json>) | 618 → 883 | 일치 |
| [컨베이어 출력기 (9845)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9845.json>) | 618 → 883 | 일치 |
| [컨베이어 분배기 (9846)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9846.json>) | 618 → 883 | 일치 |
| [컨베이어 합류기 (9847)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9847.json>) | 618 → 883 | 일치 |
| [컨베이어 필터 (9848)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9848.json>) | 618 → 883 | 일치 |
| [우선순위 게이트 (9849)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9849.json>) | 618 → 883 | 일치 |
| [층간 물류 리프트 (9850)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9850.json>) | 618 → 883 | 일치 |
| [고속 컨베이어 (9852)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9852.json>) | 618 → 883 | 일치 |

## GAP-110: '외 N개'로 생략된 재료

앞의 네 재료 외에 필요한 항목이다. 모든 항목은 실제 아이템 ID와 공개 도감이 존재한다. 전체 비용표와 아이템 링크가 필요하다. 시설 8823의 '룬 버스 결합기' 재료는 동명 시설이 아니라 부품 아이템이다.

| 시설 | 생략된 재료와 현재 수량 |
| --- | --- |
| [2성 경보 코일 (53)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-53.json>) | 정밀 부품 1 |
| [3성 부식 냉각 함정 (57)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-57.json>) | 정밀 부품 1 |
| [연금술작업대 (1031)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1031.json>) | 공학 도면 1 |
| [귀금 세공대 (1081)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1081.json>) | 정밀 부품 1 |
| [비전 단조대 (1082)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1082.json>) | 정밀 부품 1, 마나 합금 1, 마나 탐침 1 |
| [연금대 (1091)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1091.json>) | 공학 도면 1 |
| [몽직기 (1092)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1092.json>) | 정밀 부품 1, 마나 합금 1 |
| [대장간 (1093)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1093.json>) | 동력 공구날 1 |
| [심부 채석장 (1094)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-1094.json>) | 심부 승강기 1, 탐광 키트 1 |
| [채집 바구니 작업대 (8801)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8801.json>) | 천 2 |
| [동굴 재배 선반 (8803)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8803.json>) | 기계 부품 2, 천 4 |
| [문장 깃발 제작대 (8804)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8804.json>) | 공학 도면 1 |
| [의식 화로 (8805)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8805.json>) | 공학 도면 1 |
| [운반 멜빵 걸이 (8806)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8806.json>) | 공학 도면 1 |
| [가격표 게시판 (8807)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8807.json>) | 가격표 게시판 2 |
| [균사 재배 선반 (8813)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8813.json>) | 기계 부품 2, 천 4 |
| [벌목 키트 걸이 (8814)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8814.json>) | 천 2 |
| [쐐기 도끼 작업대 (8815)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8815.json>) | 공학 도면 1 |
| [방부 처리 목재대 (8816)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8816.json>) | 공학 도면 1 |
| [혈통 촉진제 선반 (8818)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8818.json>) | 공학 도면 1 |
| [마구 선반 (8819)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8819.json>) | 천 2 |
| [조련용 고삐 걸이 (8820)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8820.json>) | 천 2 |
| [동력 공구날 연마대 (8821)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8821.json>) | 공학 도면 1 |
| [자동 세척기 (8822)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8822.json>) | 공학 도면 1 |
| [룬 버스 결합기 (8823)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8823.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2, 룬 버스 결합기 1 |
| [시제품 연구실 (8825)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8825.json>) | 공학 도면 1 |
| [재료 시험기 (8826)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8826.json>) | 공학 도면 1 |
| [기계 기초대 (8827)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8827.json>) | 공학 도면 1, 공장 설치 도면 1 |
| [냉각 매니폴드 (8828)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8828.json>) | 공학 도면 1 |
| [유량계 (8829)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8829.json>) | 공학 도면 1 |
| [정비 부품함 (8830)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8830.json>) | 공학 도면 1 |
| [전동 선반 (8831)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8831.json>) | 공학 도면 1 |
| [정밀 게이지 (8832)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8832.json>) | 공학 도면 1 |
| [룬 제어반 (8833)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8833.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [방어구 맞춤대 (8835)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8835.json>) | 공학 도면 1 |
| [궁시 지그 (8836)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8836.json>) | 공학 도면 1 |
| [권양 작업대 (8837)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8837.json>) | 공학 도면 1 |
| [사슬 조립틀 (8838)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8838.json>) | 공학 도면 1 |
| [관절 지그 (8839)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8839.json>) | 공학 도면 1 |
| [화약 분쇄소 (8840)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8840.json>) | 깨끗한 물 2 |
| [탄약 압착기 (8841)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8841.json>) | 공학 도면 1 |
| [부품 감정대 (8842)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8842.json>) | 공학 도면 1 |
| [부품 복원 작업대 (8843)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8843.json>) | 공학 도면 1 |
| [정밀 장착대 (8844)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8844.json>) | 공학 도면 1 |
| [성장형 골격 지그 (8845)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8845.json>) | 공학 도면 1 |
| [계측 작업대 (8846)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8846.json>) | 공학 도면 1, 정밀 게이지 1 |
| [구성체 핵 공학대 (8847)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8847.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2, 골렘 핵 케이스 1 |
| [기상 관측탑 (8851)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8851.json>) | 기계 부품 1 |
| [토양 검사대 (8852)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8852.json>) | 공학 도면 1 |
| [계절 저장 선반 (8853)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8853.json>) | 밀폐형 계절 보관함 1 |
| [재배 온실 (8854)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8854.json>) | 기계 부품 2, 천 4 |
| [기후 제어실 (8856)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8856.json>) | 공학 도면 2, 기후 제어 매니폴드 1 |
| [산과실 (8859)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8859.json>) | 기계 부품 2, 깨끗한 물 8 |
| [도제 작업대 (8861)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8861.json>) | 공학 도면 1 |
| [노화 평가대 (8863)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8863.json>) | 기계 부품 2, 깨끗한 물 8 |
| [연령 계측기 (8864)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8864.json>) | 공학 도면 2 |
| [노인 병상 (8865)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8865.json>) | 기계 부품 2, 깨끗한 물 8 |
| [만성 관리실 (8866)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8866.json>) | 기계 부품 2, 깨끗한 물 8 |
| [재생 배양조 (8867)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8867.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [장기 재생 수술실 (8868)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8868.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [회춘 수혈실 (8869)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8869.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [룬 동면실 (8870)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8870.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [전신 재생조 (8871)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8871.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [시간 고정실 (8872)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8872.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [감염 진단대 (8873)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8873.json>) | 기계 부품 2, 깨끗한 물 8 |
| [격리 병동 (8874)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8874.json>) | 기계 부품 2, 깨끗한 물 8 |
| [혈청 검사대 (8875)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8875.json>) | 공학 도면 2 |
| [백신 연구실 (8876)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8876.json>) | 공학 도면 2 |
| [형질 분석기 (8879)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8879.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [유전 상담실 (8880)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8880.json>) | 기계 부품 2, 깨끗한 물 8 |
| [교차계통 배양기 (8881)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8881.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [방 배정대 (8882)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8882.json>) | 방 칸막이 키트 1 |
| [가족실 칸막이 (8883)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8883.json>) | 방 칸막이 키트 1 |
| [시신 처리대 (8886)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8886.json>) | 기계 부품 2, 깨끗한 물 8 |
| [기후 지도실 (8888)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8888.json>) | 공학 도면 2 |
| [원정 천문시계실 (8889)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8889.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [종자 선별대 (8890)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8890.json>) | 공학 도면 1 |
| [방제 조제대 (8891)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8891.json>) | 공학 도면 1 |
| [작물 병리실 (8892)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8892.json>) | 기계 부품 2, 깨끗한 물 8 |
| [육종 온실 (8893)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8893.json>) | 기계 부품 2, 천 4 |
| [공명 조율실 (8897)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8897.json>) | 마나 차폐판 2, 마나 결정 4, 공학 도면 2 |
| [보안 거래 금고 (8898)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8898.json>) | 공학 도면 2 |
| [방어 제어반 (8899)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8899.json>) | 공학 도면 2 |
| [탄도 시험장 (8900)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8900.json>) | 공학 도면 2 |
| [흑강 주조 보조로 (8901)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-8901.json>) | 공학 도면 2 |
| [수차 발전기 (9811)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9811.json>) | 수차 구동축 1 |
| [마나 발전기 (9812)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9812.json>) | 룬 도체 2, 마나 합금 1 |
| [소독 정수기 (9821)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9821.json>) | 재생수 필터 카트리지 1 |
| [룬 정화 시설 (9822)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9822.json>) | 룬 도체 2, 마나 합금 1, 룬 정화 결정 2 |
| [룬 조율실 (9826)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9826.json>) | 마나 합금 1 |
| [금고각인 쇠뇌대 (9961)](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9961.json>) | 정밀 부품 1 |

## GAP-111: 랜드마크 비용과 설명

아홉 도감의 요약은 모두 'V20 hand-authored milestone landmark.'다. 아래 값은 현재 작성 자산의 일반 건설 비용이며, 이정표 달성 조건이나 건설 가능 시점 자체를 증명하는 표는 아니다. 이정표 가이드에는 이 비용을 대신 설명하는 표가 없다.

| 랜드마크 | 건설 WU | 전체 재료 |
| --- | ---: | --- |
| [대협약 회당](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-accord-hall.json>) | 4121 | 처리 목재 36, 석재 블록 18, 강철 16, 종이 12 |
| [영원 계보전](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-lineage-vault.json>) | 5526 | 석재 블록 30, 강철 16, 정밀 부품 8, 룬 도체 4 |
| [비전 승천탑](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-arcane-spire.json>) | 5553 | 석재 블록 36, 강철 27, 룬 도체 12, 마나 결정 16, 시제품 설계 묶음 6 |
| [진실 관측소](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-truth-observatory.json>) | 3660 | 석재 블록 24, 강철 17, 정밀 부품 9, 시제품 설계 묶음 4 |
| [시간 고정 성소](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-temporal-sanctum.json>) | 5553 | 석재 블록 40, 강철 20, 정밀 부품 10, 룬 도체 10, 마나 결정 12 |
| [지상 패권문](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-surface-gate.json>) | 5321 | 석재 블록 30, 강철 24, 흑강 6, 기계 부품 6 |
| [강철 신격상](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-steel-colossus.json>) | 5553 | 석재 블록 32, 강철 30, 흑강 12, 기계 부품 12, 정밀 부품 8 |
| [주권 성채](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-sovereign-citadel.json>) | 3654 | 석재 블록 36, 강철 27, 기계 부품 12, 시제품 설계 묶음 4 |
| [봉인 생태정원](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-landmark-sealed-garden.json>) | 4155 | 처리 목재 45, 석재 블록 27, 깨끗한 물 24, 기계 부품 9 |

## 공개 정책 확인이 필요한 내부 대상

FACILITY-COST-U01: 외부 자원·오염·외부 구역의 레이어별 정의 42개가 일반 시설 도감으로 나온다. RuntimeBuildingArchetypeCatalog가 이들을 작업 대상으로 조회하는 경로를 확인했다. 그러나 건설 선택과 해금의 모든 경로는 아직 대조하지 않았으므로, 공통 작성값인 18 WU와 재료 1개를 '플레이어가 지을 수 있는 비용'으로 안내하라는 누락 항목으로 세지 않는다. 공개 도감에서의 제외 또는 별도 설명 방식은 후속 정책 검토다.

## 검증과 한계

- 독립적인 줄 단위 파서와 모듈 범위 정규식 결과를 대조했다. 자산 419개의 WU·재료 ID·수량과 공개 398개의 WU·필드·관계에 대한 4480검사 오류 0개다.
- 중간 수집 이후 현재 파일의 WU·BOM·공개 요약 변경은 0개였다. 419개 정의는 도메인 카탈로그에서 각 1회 참조되며 해당 도메인은 루트 카탈로그에 연결돼 있다.
- 키트·자동화·전력·유체·수리·진화 규칙의 기존 누락과 건설 비용을 구별한다. 모든 시설 기능·사용자 입력·저장 왕복·Unity 실행 검증은 완료하지 않았다.
- [의복 제작 대조](apparel-crafting-review.md)의 GAP-109는 재단시설 한 개를 확인한 과거 부분 기록이다. 현행 GAP-109는 이 보고서의 144개 시설 범위다.
- KB query: `BuildingWorkAmountAbility CalculateConstruction constructionMaterials`, areas `code/content/authority`, limit 8, session 87390. exit 1, stale 4건, 생성 행 0개다.
- content digest: `139a0a989275ecdd5a4a26c10ceb6a1931041c7c928ed0421628faea5cd928c6`. KB digest: `ceef8dc8f25f4d327205b15e12346aee0ebc5d6a84aa7eeb1f08af5ce14db0dd`. 생성 인덱스를 현재 근거로 사용하거나 재생성하지 않았다.
- 밸런스 영향 없음: 감사 파일만 작성했다. 스크립트·자산·공개 위키·게임 수치·서버는 변경하지 않았다.
