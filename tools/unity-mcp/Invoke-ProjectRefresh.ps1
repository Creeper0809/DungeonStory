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
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(
            ImportAssetOptions.ForceSynchronousImport
            | ImportAssetOptions.ForceUpdate);
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation(
            UnityEditor.Compilation.RequestScriptCompilationOptions.CleanBuildCache);
        result.Log("Requested a synchronous asset refresh and clean script compilation.");
    }
}
'@

$arguments = @{
    Code = $code
    Title = 'DungeonStory refresh and clean compile'
} | ConvertTo-Json -Compress
$argumentsBase64 = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($arguments))

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $helper `
    -ToolName Unity_RunCommand `
    -ArgumentsBase64 $argumentsBase64 `
    -WaitSeconds $WaitSeconds `
    -LogLevel debug
if ($LASTEXITCODE -ne 0) {
    throw "Unity MCP refresh request failed with exit code $LASTEXITCODE."
}
