@echo off
setlocal
cd /d "%~dp0\..\.."
if not exist ".venv-v25-sft\Scripts\python.exe" (
  echo SFT environment missing. Run tools\v25_narrative_training\setup_sft_environment.cmd first.
  exit /b 1
)
".venv-v25-sft\Scripts\python.exe" tools\v25_narrative_training\train_sft.py %*
endlocal
