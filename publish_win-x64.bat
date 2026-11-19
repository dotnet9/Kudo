@echo off
setlocal EnableExtensions
set "SCRIPT_ROOT=%~dp0"
set "target_framework=auto"
set "pause_requested=1"

:parse
if "%~1"=="" goto run
if /I "%~1"=="--no-pause" (
    set "pause_requested=0"
    shift
    goto parse
)
if /I "%~1"=="net10" (
    set "target_framework=net10.0"
    shift
    goto parse
)
if /I "%~1"=="net11" (
    set "target_framework=net11.0"
    shift
    goto parse
)
goto usage

:run
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_ROOT%scripts\publish_demo.ps1" -RuntimeIdentifier win-x64 -TargetFramework "%target_framework%"
set "exit_code=%errorlevel%"
if "%pause_requested%"=="1" if /I not "%CODEX_NO_PAUSE%"=="1" pause
exit /b %exit_code%

:usage
echo Usage: publish_win-x64.bat [net10^|net11] [--no-pause]
exit /b 2
