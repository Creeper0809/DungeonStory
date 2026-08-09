@echo off
setlocal
pushd "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -Command "$source=Get-Content -LiteralPath '.\Tools\ResearchDocumentationCatalog.ps1' -Raw -Encoding UTF8; & ([scriptblock]::Create($source)) %*"
set "catalog_exit=%ERRORLEVEL%"
popd
exit /b %catalog_exit%
