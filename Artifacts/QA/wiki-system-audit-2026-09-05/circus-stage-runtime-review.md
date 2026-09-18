# 중앙 무대 런타임 대조

- CS01은 performerCapacity2, baseTicketPrice12, preparationWork16, showDurationSeconds45다. 공개 도감은 모듈명·분류·크기·건설비만 표시한다.
- 유효 무대만 공연을 시작할 수 있고 포로 공연자는 최대2명으로 잘린다. 표값은 venue 수익 배율을 반영해 최소1, 준비 작업은 venue 배율 포함 최소1, 공연 시간은 최소5초로 주문에 고정된다.
- 준비 완료 전에는 실제 작업이 누적되고 저장 복원은 준비량/진행량/공연 시간을 재검증한다. 지식베이스는 stale이어서 직접 원본만 사용했다.
