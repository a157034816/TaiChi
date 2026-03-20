@echo off
setlocal

where python >nul 2>nul
if errorlevel 1 (
  echo Missing command: python. Install Python 3.10+ and ensure 'python' is in PATH.
  exit /b 1
)

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_PATH=%SCRIPT_DIR%sdk.py"

python -X utf8 "%SCRIPT_PATH%" %*
exit /b %ERRORLEVEL%

