@echo off
setlocal
cd /d "%~dp0.."
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Release.ps1" %*
if errorlevel 1 (
  echo.
  echo Release build failed. See the messages above.
  pause
)
exit /b %errorlevel%
