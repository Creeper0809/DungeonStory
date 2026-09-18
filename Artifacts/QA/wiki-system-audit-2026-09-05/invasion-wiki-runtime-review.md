# 침입·방어 위키→구현 대조

정적 C#/위키 대조다. 실제 침입 Play Mode는 이번 절편에서 실행하지 않았다.

## 정상 연결로 확인한 범위

- 10/20/30일 25%/50%/75% 예행 침입은 `ExperiencePacingRuntime`과 `DungeonRunFlowApplicationAdapter`에서 다음 침입 profile로 무장된다.
- 40일 이후 보스 주기, 예행 mask·active day, boss armed/active/cycle은 save section으로 이어진다.
- 침입자 rally, open path와 breach 후보, 구조 내구도 피해와 재경로, 경비 교전·후퇴, owner 최종 방어가 별도 런타임으로 존재한다.
- 침입 aggregate는 위협, 정책, 캠페인 operation과 개별 침입자/rally/대피 상태를 저장한다.

## WIM-047 경고 정보 부족

위키는 침입 경고가 접근 방향, 남은 시간, 적의 성격과 목표를 알려 준다고 설명한다. `InvasionThreatSnapshot`은 threat/stage/factors/pendingDelayRemaining/safetyRemaining만 가진다. 실제 warning detail은 던전 가치·소문·시간·위험 요인을 열거하고, candidate detail도 정찰대 목격과 임박 문장뿐이다. 방향·적 archetype·공격 목표가 alert payload에 없다.

## WIM-048 일반 주민 대피 부재

위키는 외곽 주민과 비전투 생활권 주민의 대피 경로를 방어 준비의 일부로 설명한다. production의 자동 대피 구현은 `InvasionOwnerEvacuationService` 하나이며 `TryGetOwner`로 사장 1명만 선택한다. 사장실 또는 입구에서 먼 안전 칸으로 이동시키고 AI를 일시 중지한 뒤 침입 종료 시 복귀시킨다. 일반 주민 집단, 대피 구역, 역할별 비전투 대피 명령은 이 경로에 없다.

## WIM-049 requiredPower 공식의 실제 권위 차이

가이드는 적 기본 능력치를 목표의 `requiredPower`로 조정한다고 적고 `sqrt(requiredPower / 10)` 공식을 제시한다. 실제 `EnemyEncounterFactory`는 target.requiredPower를 받지 않고 encounter ID로 campaign order를 구한 뒤 `OffenseCampaignCombatBalanceRules`의 고정 reference power 10/16/32/42/60/85를 사용한다. 이 차이는 같은 캠페인 안에서 requiredPower가 다른 목표도 동일 campaign scale을 받는다는 뜻이다.

## 근거 해시

| 파일 | SHA-256 |
| --- | --- |
| invasions-and-defence.md | `E5344175F3292D4FC9A7F0E95DDC115A2D3A2A8C60CC8FF3586D634F8CD1F364` |
| InvasionThreatSystem.cs | `B7FA111E733DD57E794E60840A62F867203F0608D5E2B713E8B75F979E1EEB24` |
| InvasionPrimitives.cs | `01079F0B2F8A6D34CDBA0210947375C8624E756F3F630FD7861EB44BFD119E8C` |
| InvasionThreatRuntime.cs | `534238C85194DD9CFC9C47F54CF53C43808BC03341E06E1C3F1D9A9784C72E52` |
| InvasionOwnerEvacuationService.cs | `9CF10FB54AE77BD13FBE631C97A529F430CBDA6C7B35F26CC96A1CF13EB04B68` |
| OffenseCampaignCombatBalanceRules.cs | `EC2380C09031EE90BDE448A418D8E544BE090EFF5AA8BB3D03D807D9586EE3B0` |
| EnemyEncounterFactory.cs | `25FCD61FA3FBC07F624E588D1063857435A6FF0EAC32EAE87291E220CA43BCF9` |
| DungeonRunFlowApplicationAdapter.cs | `C29949370F657CE268265AA8DE074C9CCD411D098216F53719AEDCBF75444851` |

