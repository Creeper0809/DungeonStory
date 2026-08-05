param(
    [Parameter(Mandatory = $true)]
    [string]$ToolName,

    [string]$ArgumentsJson = '{}',

    [string]$ArgumentsBase64 = '',

    [ValidateRange(1, 60)]
    [int]$WaitSeconds = 8,

    [ValidateSet('error', 'info', 'debug')]
    [string]$LogLevel = 'debug'
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$relayPath = 'C:\Users\vulpo\.unity\relay\relay_win.exe'
if (-not (Test-Path -LiteralPath $relayPath)) {
    throw "Unity MCP relay is missing: $relayPath"
}

# Resolve only the live Editor for this exact project. Asset import workers are
# excluded because their command lines contain -batchMode rather than
# -projectpath. This script never starts, stops, or drives the Editor itself.
$escapedProject = [Regex]::Escape($projectRoot)
$editor = Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq 'Unity.exe' -and
        $_.CommandLine -match ('(?i)-projectpath\s+"?' + $escapedProject) -and
        $_.CommandLine -notmatch '(?i)-batchMode'
    } |
    Select-Object -First 1
if ($null -eq $editor) {
    throw "No live Unity Editor owns project '$projectRoot'."
}

# Validate before opening the relay so malformed user JSON cannot leave a
# needless child process behind. Base64 is convenient when a caller must cross
# Windows PowerShell's command-line quote parser.
if (-not [string]::IsNullOrWhiteSpace($ArgumentsBase64)) {
    $ArgumentsJson = [System.Text.Encoding]::UTF8.GetString(
        [Convert]::FromBase64String($ArgumentsBase64))
}
$arguments = $ArgumentsJson | ConvertFrom-Json
$request = @{
    jsonrpc = '2.0'
    id = 2
    method = 'tools/call'
    params = @{
        name = $ToolName
        arguments = $arguments
    }
} | ConvertTo-Json -Compress -Depth 30

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = $relayPath
$startInfo.Arguments = '--mcp --project-path "{0}" --instance-id {1} --log {2}' -f `
    $projectRoot,
    $editor.ProcessId,
    $LogLevel
$startInfo.UseShellExecute = $false
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.CreateNoWindow = $true

$relay = [System.Diagnostics.Process]::Start($startInfo)
$stdin = $relay.StandardInput
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)

function Send-McpMessage([string]$message) {
    $bytes = $utf8WithoutBom.GetBytes($message + "`n")
    $stdin.BaseStream.Write($bytes, 0, $bytes.Length)
    $stdin.BaseStream.Flush()
}

try {
    # Windows PowerShell's redirected StreamWriter emits one UTF-8 BOM. Put it
    # on an otherwise empty line, then write all protocol messages as raw
    # BOM-less UTF-8. The relay deliberately ignores that malformed blank line.
    $stdin.WriteLine('')
    $stdin.Flush()

    Start-Sleep -Milliseconds 900
    Send-McpMessage '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"Codex","version":"1.0"}}}'
    Start-Sleep -Seconds 2
    Send-McpMessage '{"jsonrpc":"2.0","method":"notifications/initialized"}'
    Start-Sleep -Milliseconds 600
    Send-McpMessage $request
    Start-Sleep -Seconds $WaitSeconds
}
finally {
    # Only the exact relay child created above is terminated. The Unity Editor
    # and its named-pipe bridge are never stopped or restarted.
    if (-not $relay.HasExited) {
        $relay.Kill()
        $relay.WaitForExit()
    }
}

$stdout = $relay.StandardOutput.ReadToEnd()
$stderr = $relay.StandardError.ReadToEnd()
if (-not [string]::IsNullOrWhiteSpace($stdout)) {
    Write-Output $stdout.TrimEnd()
}
if (-not [string]::IsNullOrWhiteSpace($stderr)) {
    Write-Output $stderr.TrimEnd()
}

$toolResponse = $null
foreach ($line in ($stdout -split "`r?`n")) {
    if ([string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    try {
        $candidate = $line | ConvertFrom-Json -ErrorAction Stop
        if ($candidate.id -eq 2) {
            $toolResponse = $candidate
            break
        }
    }
    catch {
        # Relay notifications and diagnostics are not necessarily JSON-RPC
        # responses. Only the response carrying the tool-call id matters.
    }
}

if ($null -eq $toolResponse) {
    throw "Unity MCP did not return JSON-RPC response id 2 for '$ToolName'."
}
if ($null -ne $toolResponse.error) {
    throw "Unity MCP '$ToolName' returned protocol error: $($toolResponse.error | ConvertTo-Json -Compress -Depth 10)"
}
if ($toolResponse.result.isError -eq $true) {
    $toolError = ($toolResponse.result.content |
        Where-Object { $_.type -eq 'text' } |
        ForEach-Object { $_.text }) -join "`n"
    throw "Unity MCP '$ToolName' failed: $toolError"
}
