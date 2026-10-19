@echo off
setlocal
cd /d "%~dp0.."
where dotnet >nul 2>nul
if errorlevel 1 (
  echo The .NET 10 SDK is needed to run the source in development mode.
  echo End users should install artifacts\AudioBridge-Setup.msi instead.
  pause
  exit /b 1
)
dotnet run --project "src\AudioBridge\AudioBridge.csproj"
if errorlevel 1 pause


