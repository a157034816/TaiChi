@echo off
setlocal
pushd "%~dp0"

set "PYTHON_EXE=python"
set "PYTHON_ARGS=-X utf8"

if defined TAICHI_PYTHON_EXE (
  set "PYTHON_EXE=%TAICHI_PYTHON_EXE%"
  goto run_launcher
)

where "%PYTHON_EXE%" >nul 2>nul
if errorlevel 1 (
  where py >nul 2>nul
  if errorlevel 1 (
    echo Python not found. Use TAICHI_PYTHON_EXE to override the Python path.
    popd
    exit /b 1
  )
  set "PYTHON_EXE=py"
  set "PYTHON_ARGS=-3 -X utf8"
)

:run_launcher
"%PYTHON_EXE%" %PYTHON_ARGS% "%~dp0build_and_launch_project.py"
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
  echo.
  echo Launcher exited with code %EXIT_CODE%.
  pause
)

popd
exit /b %EXIT_CODE%

