# DungeonStory Player Automation

빌드된 `DungeonStoryPlaytest.exe`를 Unity Editor 없이 조회하고 조작하는 로컬 자동화 채널이다.

## 보안 경계

- `-automation` 실행 인자가 있어야 열린다.
- `127.0.0.1`에만 바인딩한다.
- 매 요청에 실행 토큰을 요구한다.
- Development 빌드 또는 application identifier가 `.playtest`로 끝나는 전용 빌드에서만 허용한다.
- 일반 Release 빌드에서는 실행 인자를 전달해도 열리지 않는다.

## 실행

```powershell
Builds/HumanPlaytest/DungeonStoryPlaytest.exe `
  -automation `
  -automation-port 48761 `
  -automation-token local-test-token
```

연결 정보는 다음 격리 프로필에 생성된다.

```text
%USERPROFILE%\AppData\LocalLow\DungeonStory\DungeonStoryPlaytest\Automation\bridge.json
```

## 직접 호출

```powershell
tools/player-automation/Invoke-DungeonPlayerAutomation.ps1 -Command game.status
tools/player-automation/Invoke-DungeonPlayerAutomation.ps1 -Command ui.list
tools/player-automation/Invoke-DungeonPlayerAutomation.ps1 -Command ui.click -Target StartNewRunButton
tools/player-automation/Invoke-DungeonPlayerAutomation.ps1 -Command input.key_down -Key D -Duration 1
tools/player-automation/Invoke-DungeonPlayerAutomation.ps1 -Command capture.screen -Path title.png
```

지원 명령은 `ping`, `game.status`, `ui.list`, `ui.click`, `input.pointer_move`,
`input.pointer_click`, `input.key_down`, `input.key_up`, `capture.screen`이다.

## MCP 연결

`dungeon_player_mcp.py`는 외부 패키지가 필요 없는 stdio MCP 서버다. Codex 설정에는 절대 경로로 등록한다.

```toml
[mcp_servers.dungeon-player]
command = "python"
args = ["C:/Users/vulpo/Documents/DungeonStory/tools/player-automation/dungeon_player_mcp.py"]
```

MCP 클라이언트가 서버 목록을 다시 읽은 뒤에는 상태, UI 목록, 클릭, 키 입력,
포인터 입력, 전체 프레임 캡처를 각각 도구로 호출할 수 있다.
