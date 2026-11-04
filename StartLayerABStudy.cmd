@echo off
setlocal
cd /d "%~dp0"
"%~dp0Tools\AdaptiveTraining\.venv\Scripts\python.exe" "%~dp0Tools\AdaptiveTraining\launch_layer_ab.py" %*
if errorlevel 1 pause
