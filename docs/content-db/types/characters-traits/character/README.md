# CharacterSO

종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다

총 15개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/characters-traits/character.csv)
- [중첩 작성 필드 CSV](../../../fields/characters-traits/character.csv)
- [정방향 관계 CSV](../../../relations/characters-traits/character.csv)
- [역방향 관계 CSV](../../../incoming/characters-traits/character.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `character-archetype:0` | 테스트 캐릭터 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | type-consumer-registration-unverified | active-authored | 0 | [TestCharacter.asset](../../../../../Assets/Scripts/Services/Character/SO/TestCharacter.asset) |
| `character-archetype:1` | asd | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [New Character SO.asset](../../../../../Assets/Resources/SO/Character/New Character SO.asset) |
| `character-archetype:1001` | 슬라임 사장 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | ownerPreferredWorkTypes=14 | catalog-registered-static-consumer | active-authored | 0 | [Owner_Slime.asset](../../../../../Assets/Resources/SO/Character/Owners/Owner_Slime.asset) |
| `character-archetype:1002` | 오크 사장 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | ownerPreferredWorkTypes=37 | catalog-registered-static-consumer | active-authored | 0 | [Owner_Orc.asset](../../../../../Assets/Resources/SO/Character/Owners/Owner_Orc.asset) |
| `character-archetype:1003` | 뱀파이어 사장 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | ownerPreferredWorkTypes=17 | catalog-registered-static-consumer | active-authored | 0 | [Owner_Vampire.asset](../../../../../Assets/Resources/SO/Character/Owners/Owner_Vampire.asset) |
| `character-archetype:1101` | 일반 직원 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Staff_Generic.asset](../../../../../Assets/Resources/SO/Character/Staff/Staff_Generic.asset) |
| `character-archetype:2` | 그롬 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Customer_Orc.asset](../../../../../Assets/Resources/SO/Character/Customer_Orc.asset) |
| `character-archetype:2001` | 돌파형 침입자 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Intruder_Breakthrough.asset](../../../../../Assets/Resources/SO/Character/Intruders/Intruder_Breakthrough.asset) |
| `character-archetype:3` | 세라 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Customer_Vampire.asset](../../../../../Assets/Resources/SO/Character/Customer_Vampire.asset) |
| `character-archetype:9004` | 라카 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Customer_Beastkin.asset](../../../../../Assets/Resources/SO/Character/ExpandedSpecies/Customer_Beastkin.asset) |
| `character-archetype:9005` | 아자라 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Customer_Demon.asset](../../../../../Assets/Resources/SO/Character/ExpandedSpecies/Customer_Demon.asset) |
| `character-archetype:9006` | 티크 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Customer_Kobold.asset](../../../../../Assets/Resources/SO/Character/ExpandedSpecies/Customer_Kobold.asset) |
| `character-archetype:9007` | 모르 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Customer_Myconid.asset](../../../../../Assets/Resources/SO/Character/ExpandedSpecies/Customer_Myconid.asset) |
| `character-archetype:9008` | 세라 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Customer_Harpy.asset](../../../../../Assets/Resources/SO/Character/ExpandedSpecies/Customer_Harpy.asset) |
| `character-archetype:9009` | 바살트-7 | 종족·기초 능력·특성·작업 우선순위·AI 성향을 묶은 작성 캐릭터 원형을 제공한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [Customer_Golem.asset](../../../../../Assets/Resources/SO/Character/ExpandedSpecies/Customer_Golem.asset) |
