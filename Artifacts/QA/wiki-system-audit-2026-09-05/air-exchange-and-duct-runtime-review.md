# 공기 교환·덕트 런타임 대조

- E11 공조기는 외부 공기질100을 반경4에, E13 송풍구는 반경3에, E14 배기팬은 반경4에 교환한다. E12와 E13/E14 덕트 교환율은0.65다.
- 외부 교환 source는 authored target 대신100을 쓰며, 환경 필드가 target·qualityPerSecond·거리·deltaTime으로 반경 cell 공기질을 갱신한다. 공개 페이지는 이 실행 계약을 설명하지 않는다.
