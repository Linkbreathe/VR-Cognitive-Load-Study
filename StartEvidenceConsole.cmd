@echo off
setlocal
cd /d "%~dp0"
set "EVIDENCE_PYTHON=%~dp0Tools\AdaptiveTraining\.venv\Scripts\python.exe"
if not exist "%EVIDENCE_PYTHON%" (
  echo The project Python environment is missing.
  echo See Docs\EvidenceConsoleGuide.md for installation instructions.
  pause
  exit /b 1
)
"%EVIDENCE_PYTHON%" "%~dp0Tools\AdaptiveTraining\launch_evidence_console.py" %*
if errorlevel 1 pause
