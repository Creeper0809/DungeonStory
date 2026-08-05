param(
    [ValidateRange(10, 60)]
    [int]$WaitSeconds = 60
)

$ErrorActionPreference = 'Stop'
$helper = Join-Path $PSScriptRoot 'Invoke-ProjectUnityMcp.ps1'
if (-not (Test-Path -LiteralPath $helper)) {
    throw "Project Unity MCP helper is missing: $helper"
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$editorAssembly = Join-Path $projectRoot 'Library\ScriptAssemblies\Assembly-CSharp-Editor.dll'
if (-not (Test-Path -LiteralPath $editorAssembly)) {
    throw "Unity Editor assembly is missing: $editorAssembly"
}

$latestSource = Get-ChildItem (Join-Path $projectRoot 'Assets\Scripts') `
    -Recurse -Filter '*.cs' |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1
$compiledEditor = Get-Item -LiteralPath $editorAssembly
if (($null -ne $latestSource) -and ($latestSource.LastWriteTimeUtc -gt $compiledEditor.LastWriteTimeUtc)) {
    $staleMessage = "Unity assemblies are stale. Refresh and compile the project before final acceptance. "
    $staleMessage += "Latest source: $($latestSource.FullName) at $($latestSource.LastWriteTimeUtc); "
    $staleMessage += "Editor assembly: $($compiledEditor.LastWriteTimeUtc)."
    throw $staleMessage
}

$code = @'
using System;
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        bool success = DungeonStoryFinalAcceptanceRunner.RunAll(true);
        if (!success)
        {
            throw new InvalidOperationException(
                "DungeonStory final acceptance failed. See Artifacts/QA/final-acceptance-report.txt.");
        }

        result.Log("DungeonStory final acceptance passed.");
    }
}
'@

$arguments = @{
    Code = $code
    Title = 'DungeonStory final acceptance'
} | ConvertTo-Json -Compress
$argumentsBase64 = [Convert]::ToBase64String(
    [Text.Encoding]::UTF8.GetBytes($arguments))

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $helper `
    -ToolName Unity_RunCommand `
    -ArgumentsBase64 $argumentsBase64 `
    -WaitSeconds $WaitSeconds `
    -LogLevel debug
if ($LASTEXITCODE -ne 0) {
    throw "Unity MCP final acceptance failed with exit code $LASTEXITCODE."
}
