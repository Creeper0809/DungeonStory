param(
    [ValidateRange(5, 60)]
    [int]$WaitSeconds = 12
)

$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot 'Invoke-ProjectUnityMcp.ps1'
if (-not (Test-Path -LiteralPath $helper)) {
    throw "Project Unity MCP helper is missing: $helper"
}

$code = @'
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        V19SaveAtomicityPlayModeFacade.RequestRun();
        result.Log("Queued V19 save atomicity PlayMode verification.");
    }
}
'@

$arguments = @{
    Code = $code
    Title = 'Queue V19 save atomicity verification'
} | ConvertTo-Json -Compress
$argumentsBase64 = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($arguments))

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $helper `
    -ToolName Unity_RunCommand `
    -ArgumentsBase64 $argumentsBase64 `
    -WaitSeconds $WaitSeconds `
    -LogLevel info
if ($LASTEXITCODE -ne 0) {
    throw "Unity MCP V19 save request failed with exit code $LASTEXITCODE."
}
