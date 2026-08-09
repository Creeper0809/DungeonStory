@echo off
setlocal
cd /d "%~dp0\..\.."
if not exist ".venv-v25-sft\Scripts\python.exe" py -3.11 -m venv .venv-v25-sft
if errorlevel 1 exit /b %errorlevel%
".venv-v25-sft\Scripts\python.exe" -m pip install --upgrade pip
if errorlevel 1 exit /b %errorlevel%
".venv-v25-sft\Scripts\python.exe" -m pip install -r tools\v25_narrative_training\requirements-sft.txt
if errorlevel 1 exit /b %errorlevel%
".venv-v25-sft\Scripts\python.exe" -c "import torch,transformers,trl,peft,bitsandbytes; assert torch.cuda.is_available(); print(torch.__version__, torch.version.cuda, torch.cuda.get_device_name(0))"
endlocal
