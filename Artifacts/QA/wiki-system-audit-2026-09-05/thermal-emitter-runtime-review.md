# 열 방출기 런타임 대조

- E10 냉각기는 target8°C, 설정 범위2~8°C, 초당3°C, 반경3이며 E11 공조기는 target22°C, 설정 범위2~30°C, 초당2.5°C, 반경4다.
- 환경 field는 instance별 thermostat override 또는 authored target을 택해 반경 cell에 mode·목표·속도·거리·deltaTime을 적용한다. 설정 값은 canonical building ID와 범위 검증을 거쳐 저장/복원된다.
- 공개 페이지는 전력 소비·열 공급만 표시한다.
