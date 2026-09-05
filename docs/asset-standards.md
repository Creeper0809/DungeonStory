# 에셋 규격

## Placeholder 이미지 규격

프로토타입용 placeholder도 실제 에셋과 같은 표시 규칙을 따른다.

| 용도 | 원본 크기 | Import PPU | 월드 표시 기준 |
| --- | ---: | ---: | --- |
| 캐릭터 | 64x64 | 64 | 1타일 내에 들어오는 크기, pivot 아래 중앙 |
| 시설/경영 건물 | 96x64 | 16 | 월드 표시는 Tilemap 타일 기준 |
| 방어 시설 | 96x64 | 16 | 월드 표시는 Tilemap 타일 기준 |
| 아이템/재고 | 64x48 | 16 | UI/드롭 표시용 |
| UI 상태 아이콘 | 48x48 | 16 | UI 컨테이너 안에서 비율 유지 |

## UI 표시 규격

원본 이미지 크기와 UI 표시 크기는 분리한다.

- 건설 선택 버튼: `100x100`
- 건설 선택 아이콘 최대 표시 크기: `84x64`
- 아이콘은 항상 비율 유지
- 아이콘은 버튼 안에서 중앙 정렬
- 실제 이미지가 커도 UI 컨테이너를 넘지 않게 한다.

## UI 폰트 규격

- 한글이 들어가는 TMP 텍스트는 `Maplestory Light SDF`를 사용한다.
- 런타임에서 생성되는 TMP 텍스트는 `TMPKoreanFont.Apply`를 통해 폰트를 지정한다.
- 프리팹에 다른 기본 TMP 폰트가 잡혀 있어도, 패널이 열릴 때 `TMPKoreanFont.ApplyToChildren`으로 한글 폰트를 다시 적용한다.

## 월드 표시 규격

- 캐릭터 placeholder는 `64x64 / PPU 64`를 기준으로 한다.
- 캐릭터 pivot은 `Bottom Center`를 기준으로 한다. 캐릭터 transform 위치는 발이 닿는 바닥 위치로 취급한다.
- 16x16 실제 캐릭터 스프라이트는 `PPU 16`을 유지해도 같은 1유닛 크기로 표시된다.
- 시설/방어 건물은 `BuildingSO.tiles`에 연결된 Tilemap 타일로 월드에 표시한다.
- 건물 GameObject는 그리드 점유, 충돌, 상호작용, 데이터 보관용이며 월드 스프라이트를 직접 렌더링하지 않는다.
- 시설/방어 placeholder PNG는 UI 아이콘 또는 Tile asset 제작 원본으로 사용한다.
- 시설/방어 건물을 월드에 표시하려면 PNG를 직접 SpriteRenderer로 붙이지 말고 Tile asset을 만들어 `BuildingSO.tiles`에 연결한다.

## 주의

- 새 캐릭터 placeholder를 추가하면 `Assets/Images/Placeholders/Characters` 아래에 넣고 `PPU 64`, pivot `Bottom Center`로 맞춘다.
- 새 시설 placeholder를 추가하면 `Assets/Images/Placeholders/Facilities` 또는 `Defense` 아래에 넣고 `96x64 / PPU 16`으로 맞춘다.
- UI에서 이미지를 크게 보이게 하고 싶을 때 원본 PNG를 키우지 말고, UI 컨테이너 규격을 조정한다.
