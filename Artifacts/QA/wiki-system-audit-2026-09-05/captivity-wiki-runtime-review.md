# 포로·하수인·공연 가이드 → 현재 구현 대조

조사일: 2026-09-06  
방향: 공개 `factions-contracts-and-prisoners` 가이드 → production 구현·저장·UI  
기존 계약 18종·세력 route·납품 문제는 `faction-contract-route-review.md`와 WIM-032~034가 소유하므로 재집계하지 않았다.

## 정상 연결

- 살아 있는 downed 침입자 포획, 감방·구속구·현장 안정화·호송 단계가 있고 안정화 WU는 `min(30, 8 + bleeding × 40)`이다.
- 같은 `CharacterActor`를 수용 상태로 전환하므로 이름·종족·문화·성장 상태·신체 부상을 별도 복제 캐릭터로 바꾸지 않는다.
- 순응도·탈출 위험·거짓 복종·몸값 공식과 구속구 내구 보정은 공개 식과 일치한다.
- 탈출 위험 65부터 5초 주기로 2~18%, 거짓 복종 1.4배, 실패 원한+6/위험+8, 성공 보복 `15 + 원한×0.2`가 구현돼 있다.
- 기본 정책 4개와 custom 생성·복제·수정·삭제, 삭제 시 표준 수용 복귀가 UI command까지 연결돼 있다.
- 회유·격리·강압·심문·교화·각인·혈액 추출·기억 추출·강제 개조·타락 의식의 WU·재료·상태 delta는 표와 일치한다.
- 혈액·기억 추출은 실제 item ID와 수량 1을 출력한다. 물리 소비·산출의 더 상세한 문서 공백은 반대 방향 GAP이 소유한다.
- 노역은 순응도50/건강40 gate, 물리 포로 작업 도구, 기본/전체 허용 범위와 회수 lifecycle을 가진다.
- 직접 영입10일, 하수인 전환3일, 재사회화15일×18WU×음식1 및 신뢰/원한/타락 조건이 일치한다.
- 하수인 허용 업무23개, XP50%, 원정·멘토링 제한, 주민 기분 구간, 사회 충돌·몸싸움·통제 이탈 공식이 production runtime에 있다.
- 공연 배정·사고·전투·명성50/75/100 이정표와 직원 계약·석방·전속 투사 선택이 저장된다.
- 포로/정책/상호작용/재사회화/구속구/도구/공연 상태는 save section과 restore validation을 가진다.

## WIM-054 — 포로 관리의 건강 변화가 실제 신체에 적용되지 않음

가이드는 강압·각인·혈액 추출·기억 추출·강제 개조·타락 의식이 건강을 낮추고, 공연 명성50은 식량과 치료 우선권을 연다고 설명한다.

실제 interaction handler는 `HealthDelta`를 반환하지만 `CaptivityInteractionRuntime.ApplyResult`는 `CaptiveState.health` 숫자만 변경한다. 실제 `ICharacterBodyHealthCommand`, 부위 HP, 출혈·감염에는 적용하지 않는다. `CaptivityRuntime.TickRuntime`은 다음 tick에 `EstimateHealth(actor)`로 이 값을 실제 actor HP 비율에서 다시 덮는다.

명성50 특혜도 음식 1개를 전달해 허기35를 회복하고 같은 captive scalar에 health+5를 더할 뿐, 의료 우선순위나 실제 치료 주문을 만들지 않는다. 따라서 공개된 건강 손실과 치료 우선권은 신체·의료 시스템에서 지속되는 효과가 아니다.

판정: `different`.

필요 보완: interaction health delta를 typed body-health operation으로 적용하고, 명성 특혜는 의료 triage/치료 order의 우선순위로 연결한다. captive summary health는 실제 신체 snapshot의 projection으로만 유지한다.

## WIM-055 — 심문이 실제 정보를 생성하지 않음

심문 handler는 공포75 이상이면 결과 문장을 불확실하게 바꾸고 위키와 같은 의지·공포·신뢰·원한 delta를 적용한다. 그러나 신뢰 가능한 경우에도 “조각난 정보 하나를 확보했습니다”라는 문자열 외에 정보 item, 도감 clue, 세력/지역 intelligence, 정찰 압력 변화 또는 후속 선택을 생성하지 않는다.

판정: `partial`.

필요 보완: 공포와 신뢰도에 따라 결정된 canonical interrogation outcome을 한 번 저장하고, 유효한 정보는 도감·세력·원정 정보 중 명시된 typed destination에 exact-once로 적용한다. 거짓 정보도 별도 결과와 검증 가능 provenance를 가져야 한다.

## 원본 SHA-256

| SHA-256 | 원본 |
| --- | --- |
| `4fda0736db9540e03e3e3b60bc67e35609c7ce563ea841b01fcbd34c2453f72a` | `wiki/game-versions/0.0.1v/content/guides/factions-contracts-and-prisoners.md` |
| `131c40d40bbf4ea025c1aa357f21be28c30523023dc774b1116d37ca4e310eb1` | `Assets/Scripts/Services/Captivity/CaptivityRuntime.cs` |
| `1409e70df529eda7af4b0f9b8557462fe217a1584d71cc7f596d7ef467fa2fb5` | `Assets/Scripts/Services/Captivity/CaptivityInteractionRuntime.cs` |
| `1e00335d834031e9dad8717a70add9635ec03f6f04d1e8620731c5ef539ab3e6` | `Assets/Scripts/Models/Captivity/Core/CaptivityInteractionHandlers.cs` |
| `61daf9565a7f58deaf3baef2319e02434f2cceda97ed3ed8e4e6e6759c3953b4` | `Assets/Scripts/Models/Captivity/Core/CaptivityPolicyRuntime.cs` |
| `8ca1773ccbb814683fdf4cc2da691137faf88b239536fe106d9f27f3bd878eed` | `Assets/Scripts/Services/Captivity/CaptivityEscapeRuntime.cs` |
| `0a0627b2b89be471c5ad928bf59880d15ea8442db1fb35b73f590e082156d05c` | `Assets/Scripts/Models/Captivity/Core/CaptivityPerformerRuntime.cs` |
| `84079b6c3c89da535083e98d6fbeffe8c3471bb1a94a9fba3e4e5c28a72cfd40` | `Assets/Scripts/Services/Character/Core/CharacterSettlementStandingQuery.cs` |
| `f8cbb9b3c7771b919bf122eb1b7eada07c81ac740bfdce89122455cd52c2ccc7` | `Assets/Scripts/Views/UI/CaptivityFeatureSectionPresenter.cs` |
| `ec89ff586a56b2c6395cf702a3b67cbcdf182f07a630ce114198f100ed6bc271` | `Assets/Scripts/Services/Infrastructure/Core/Save/CaptivitySaveSection.cs` |

이 보고서는 정적 production 경로 대조다. 기존 Editor/PlayMode verifier의 존재를 이번 실행 증거로 세지 않았다.
