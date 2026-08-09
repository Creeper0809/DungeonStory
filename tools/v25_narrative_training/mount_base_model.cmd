@echo off
setlocal
cd /d "%~dp0\..\.."
python tools\v25_narrative_training\mount_base_model.py %*
exit /b %errorlevel%
