@echo off
setlocal
cd /d "%~dp0\..\.."
powershell -NoProfile -Command "$e='Artifacts/Training/V25/models/sft-qwen3-1.7b-v1/training_evidence.json'; if(Test-Path $e){Get-Content $e -Encoding UTF8; exit 0}; $p=Get-CimInstance Win32_Process | Where-Object { $_.Name -like 'python*' -and $_.CommandLine -like '*train_sft.py*' }; if($p){'SFT RUNNING'; $p | Select-Object ProcessId,ParentProcessId,CommandLine | Format-List; Get-Content 'Artifacts/Training/V25/logs/sft-full.stderr.log' -Tail 20}else{'SFT IS NOT RUNNING AND HAS NO COMPLETION EVIDENCE'; Get-Content 'Artifacts/Training/V25/logs/sft-full.stderr.log' -Tail 40}"
endlocal
