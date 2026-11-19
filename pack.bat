@echo off
setlocal

pushd "%~dp0"

set "CONFIGURATION=Release"
set "PACKAGES_DIR=artifacts\packages"
set "TARGET_FRAMEWORK=%~1"
if not defined TARGET_FRAMEWORK set "TARGET_FRAMEWORK=net11.0"
if /I not "%TARGET_FRAMEWORK%"=="net10.0" if /I not "%TARGET_FRAMEWORK%"=="net11.0" goto :invalid_framework

if not exist "%PACKAGES_DIR%" mkdir "%PACKAGES_DIR%"
if errorlevel 1 goto :error
for %%F in ("%PACKAGES_DIR%\Kudo.*.nupkg" "%PACKAGES_DIR%\Kudo.*.snupkg") do if exist "%%~fF" del /q "%%~fF"

echo [1/4] Restoring solution...
dotnet restore Kudo.slnx --force -p:Configuration=%CONFIGURATION%
if errorlevel 1 goto :error

echo [2/4] Building solution...
dotnet build Kudo.slnx -c %CONFIGURATION% --no-restore -m:1 -p:UseSharedCompilation=false
if errorlevel 1 goto :error

echo [3/4] Running %TARGET_FRAMEWORK% tests...
dotnet test Kudo.slnx -c %CONFIGURATION% --no-build --no-restore -m:1 -p:TargetFramework=%TARGET_FRAMEWORK% -p:UseSharedCompilation=false -- xunit.parallelizeTestCollections=false xunit.parallelizeTestCases=false
if errorlevel 1 goto :error

echo [4/4] Packing SDK packages...
for %%P in (
    "src\Kudo.Core\Kudo.Core.csproj"
    "src\Kudo.Avalonia\Kudo.Avalonia.csproj"
    "src\Kudo.Controls\Kudo.Controls.csproj"
    "src\Kudo.Themes\Kudo.Themes.csproj"
    "src\Kudo.Avalonia.3D\Kudo.Avalonia.3D.csproj"
) do (
    dotnet pack %%~P -c %CONFIGURATION% --no-restore -m:1 -p:UseSharedCompilation=false -p:GeneratePackageOnBuild=false -p:WarningsNotAsErrors=NU5104 -o "%PACKAGES_DIR%"
    if errorlevel 1 goto :error
)

echo.
echo Kudo %TARGET_FRAMEWORK% packages created successfully.
echo Output: %PACKAGES_DIR%

call :maybe_pause
popd
exit /b 0

:error
set "EXIT_CODE=%ERRORLEVEL%"
if "%EXIT_CODE%"=="0" set "EXIT_CODE=1"
echo.
echo NuGet packing failed with exit code %EXIT_CODE%.
call :maybe_pause
popd
exit /b %EXIT_CODE%

:invalid_framework
echo.
echo Unsupported target framework "%TARGET_FRAMEWORK%". Use net10.0 or net11.0.
call :maybe_pause
popd
exit /b 2

:maybe_pause
if /i not "%CODEX_NO_PAUSE%"=="1" pause
exit /b 0
