@echo off
setlocal

pushd "%~dp0"

set "CONFIGURATION=Release"
set "PACKAGES_DIR=artifacts\packages"

if not exist "%PACKAGES_DIR%" mkdir "%PACKAGES_DIR%"
if errorlevel 1 goto :error
for %%F in ("%PACKAGES_DIR%\Kudo.*.nupkg" "%PACKAGES_DIR%\Kudo.*.snupkg") do if exist "%%~fF" del /q "%%~fF"

echo Packing SDK projects for local inspection...
for %%P in (
    "src\Kudo.Core\Kudo.Core.csproj"
    "src\Kudo.Avalonia\Kudo.Avalonia.csproj"
    "src\Kudo.Controls\Kudo.Controls.csproj"
    "src\Kudo.Themes\Kudo.Themes.csproj"
    "src\Kudo.Dock.Themes\Kudo.Dock.Themes.csproj"
    "src\Kudo.Avalonia.3D\Kudo.Avalonia.3D.csproj"
) do (
    echo   %%~nP
    dotnet restore "%%~P" --force -p:Configuration=%CONFIGURATION%
    if errorlevel 1 goto :error
    dotnet pack "%%~P" -c %CONFIGURATION% --no-restore -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:WarningsNotAsErrors=NU5104 -o "%PACKAGES_DIR%"
    if errorlevel 1 goto :error
)

echo.
echo Kudo packages created successfully.
echo Output: %PACKAGES_DIR%

call :maybe_pause
popd
exit /b 0

:error
set "EXIT_CODE=%ERRORLEVEL%"
if "%EXIT_CODE%"=="0" set "EXIT_CODE=1"
echo.
echo NuGet quick packing failed with exit code %EXIT_CODE%.
call :maybe_pause
popd
exit /b %EXIT_CODE%

:maybe_pause
if /i not "%CODEX_NO_PAUSE%"=="1" pause
exit /b 0
