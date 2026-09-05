# 포트폴리오 수치 곡선

[포트폴리오 수치 곡선](portfolio-numeric-curve.csv)은 여섯 주력의 10·30·120·400·960일 노동 예산을 누적 투자 곡선으로 바꾼다. `cumulative_*_wu`는 직전 이정표 다음 날부터 현재 이정표까지 해당 단계의 일일 예산이 유지된다는 기준으로 계산한다.

```text
누적 주력 투자 WU = Σ(구간 일수 × daily_primary_wu)
누적 연구 투자 WU = Σ(구간 일수 × daily_research_wu)
누적 예비 WU = Σ(구간 일수 × daily_reserve_wu)
누적 운영 WU = Σ(구간 일수 × life support + logistics + maintenance + security)
```

이 곡선은 자산의 실제 가동 결과가 아니라, 자산 수치와 포트폴리오 조립을 대조할 설계 기준이다. 연구는 [연구 수치 원장](research/research-numeric-ledger.csv), 시설은 [시설 수치 원장](facilities/facility-numeric-ledger.md), 제작·물류는 생산·아이템 원장에서 같은 시점의 누적 투자 안에 들어와야 한다.

## 의도한 격차

| 포트폴리오 | 더 많이 쓰는 축 | 반드시 남기는 약점 |
|---|---|---|
| sanctuary | 식량·물·운반과 계절 비축 | 외부 노출, 고급 연구·군수의 지연 |
| underground | 공간 효율과 배양·환기 주력 | 오염 격리와 공용 공간 경쟁 |
| hospitality | 서비스·계약·예비 인력 | 봉쇄와 평판·기한의 연쇄 손실 |
| industrial | 기반망 운영·정비·방어 | 연료·전력·중앙 고장 |
| expeditionary | 보안·장비·기동 예비 | 부상·탄약과 내부 인력 공백 |
| arcane | 연구·정밀 주력 | 촉매·전문 인력·무균·정전 의존 |

120일에는 어느 포트폴리오도 주력 투자와 연구 투자에서 동시에 1위를 차지하지 않는다. `arcane`은 연구, `underground`는 주력 전환, `industrial`은 기반망 운영, `hospitality`는 예비 인력에 더 많은 예산을 쓴다. 다른 포트폴리오는 비축·물류·보안에 비용을 남긴다. 400일 전에는 세 번째 주력을 더할 수 있는 잉여 예산을 설계하지 않는다.

모든 단계에서 `maximum_single_network_share`를 넘는 단일 공급망 의존은 실패로 처리한다. `maximum_recovery_days`는 같은 핵심망이 끊겼을 때 비축·수동 운전·보조망으로 복귀해야 하는 한계다. 이 두 값은 자급·자동화·교역·원정·비전 어느 하나가 무비용 만능 해법이 되는 것을 막는 기준이다.
