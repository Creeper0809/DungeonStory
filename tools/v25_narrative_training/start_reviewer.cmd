@echo off
setlocal
cd /d "%~dp0\..\.."
python tools\v25_narrative_training\reviewer\server.py --open
if errorlevel 1 (
  echo.
  echo Reviewer failed to start. Check that Python 3 is installed and the V25 artifacts exist.
  pause
)
