# 시작 파티·난이도·생존 압박 대조

검증일: 2026-09-06  
범위: `starting-party` 공개 가이드의 주장과 현재 작성·서비스·GameplayScene UI·원정 admission 정적 대조  
제외: Unity PlayMode 조작, 실제 화면 캡처, 밸런스 적정성 판단

## 결론

- 던전 주인 1명과 같은 종족 직원 2명으로 시작하고, 같은 종족 직원 후보 6명 중 선발 2명·예비 4명을 교체하는 구조는 구현과 일치한다.
- 던전 주인의 원정 참가 금지는 `OffenseExpeditionService.CanJoinExpedition`에서 명시적으로 거절한다.
- 9개 숙련의 기본 15~45 XP, 주/부 전문 1.50/1.20배, 연령대별 상한 99/174/249/399와 노년 초기 질환은 작성 규칙과 일치한다.
- 전투 난이도와 생존 압박은 타이틀 화면에서 독립 선택되고 run snapshot에 함께 전달된다. 기존 GAP-004~005가 이 설명·수치 공백을 이미 소유한다.
- 현재 시작 기술은 직원별 액티브 후보 1개와 패시브 1개를 만들고 액티브를 즉시 자동 확정한다. 기존 GAP-002의 “사용자가 선택해야 시작 가능” 해석은 코드와 달라 자동 확정·준비 조건으로 정정했다.
- 가이드가 준비 화면에서 확인할 수 있다고 단정한 건강·식단·수면·기후 중, 실제 GameplayScene의 `OwnerSelectionPanel`은 해당 정보를 노출하지 않는다. 이를 DIFF-042로 등록했다.

## 주장별 대조

| 위키 주장 | 구현 근거 | 판정 |
| --- | --- | --- |
| 주인1 + 같은 종족 주민2, 후보6 중2 선택 | `Begin`이 같은 `SpeciesTag`의 Customer를 모아 6개 roster를 만들고 첫2개만 선발한다. `TrySwapWithReserve`로 선발/예비를 교체한다 | 일치 |
| 주인은 원정 불가 | `CanJoinExpedition`이 `identity.IsOwner`를 먼저 거절한다 | 일치 |
| 주인의 종족·출신·과거·나이·특성·숙련 조합 변동 | owner 후보 선택 뒤 `RollIdentity`와 starting profile을 적용한다 | 일치 |
| 숙련9개 기본15~45, 주/부 전문1.50/1.20 | `BuiltInCharacterProficiencyIds.All`, `CharacterStartingProficiencyRules`, `CharacterProficiencySpecializationRules` | 일치 |
| 연령별 상한99/174/249/399, 노년 질환 가능 | `CharacterStartingProfileRules`의 상수와 elder age-condition roll | 일치 |
| 준비 화면에서 현재 건강·특성·식단·수면·기후 확인 | 실제 prefab이 쓰는 `OwnerSelectionPanel`은 출신·특성·잠재력·숙련·기술만 렌더링한다. `StartPartyMemberDetailRenderer`도 나이와 건강 문제 개수 외 생활 조건은 없다 | DIFF-042 |
| 시작 기술 | 서비스는 후보 수를 1로 만들고 즉시 `TryChooseActiveSkill(... confirmed:true)`를 호출한다. UI의 이중 클릭 선택 handler는 이미 선택된 항목을 비활성화하므로 현재 경로에서는 실질 선택이 아니다 | GAP-002 정정 |
| 난이도 | 타이틀 UI가 전투 난이도와 생존 압박을 별도 행으로 선택하고 함께 시작 요청에 넘긴다 | 구현 존재, 공개 설명은 GAP-004~005 |

## 정적 경계

- `StartPartyPreparationUiController`와 `StartPartyMemberDetailRenderer`라는 별도 구성도 코드에 있지만, GameplayScene/Prefab에 직렬화된 실제 컴포넌트는 `OwnerSelectionPanel`이다. 활성 화면 판정은 후자를 기준으로 했다.
- “첫 액티브 선택”용 public API와 UI handler는 남아 있으나 현재 생성 후보 수가 1이고 서비스가 선확정한다. 이는 API 존재만 보고 사용자 선택 기능이 작동한다고 판정하면 안 되는 사례다.
- 실제 플레이에서 화면이 다른 동적 factory로 대체되는지까지는 이번 정적 감사로 증명하지 않는다.

## 원본 해시

| 파일 | SHA-256 |
| --- | --- |
| `wiki/.../starting-party.md` | `690F1F368655662129D02DC6D6066106962BBF48582193085A0ACCC6E1F5798F` |
| `StartPartyPreparationService.cs` | `6247B222F08CB4CBD1BEA0D7DC12EB75E570222B27F027CB2B4E124BAB965E54` |
| `OwnerSelectionPanel.cs` | `4EF5013BA6A375AE0182798079F6DCF924A133E74EAA942655E8C2C79EFC9D04` |
| `StartPartyMemberDetailRenderer.cs` | `49E4BE01D09327811D8659F4C3CCA31140ABD3DFB2ECDE7EF7AE39D3698DA3AA` |
| `OffenseExpeditionService.cs` | `1BAE5523C7BDF6B9C96A410647549CF5E10D4B5176862433971268B4CF86BE0A` |
| `DungeonDifficulty.cs` | `6700FCEFDA347B7DBF35E09AEBCB61B3EDD1E1409EE8B29D2A3FC09E23320FE4` |
