@echo off
setlocal

set PROJECT_NAME=CodeSnip

:MENU
cls

echo ======================================
echo         %PROJECT_NAME% Build System
echo ======================================
echo.
echo 1^) Clean
echo 2^) Build
echo 3^) Publish self-contained
echo 4^) Publish self-contained trimmed
echo 0^) Exit
echo.

set /p choice=Choice:

if "%choice%"=="1" goto CLEAN
if "%choice%"=="2" goto BUILD_WIN
if "%choice%"=="3" goto PUBLISH_WIN
if "%choice%"=="4" goto PUBLISH_WIN_TRIMMED
if "%choice%"=="0" exit /b 0

echo.
echo Invalid option.
pause
goto MENU

:CLEAN
echo.
echo Cleaning...
dotnet clean
goto WAIT

:BUILD_WIN
echo.
echo Building Release...
dotnet build -c Release
goto WAIT

:PUBLISH_WIN
echo.
echo Publishing self-contained...
dotnet publish -c Release --self-contained true  -p:PublishSingleFile=false
goto WAIT

:PUBLISH_WIN_TRIMMED
echo.
echo Publishing self-contained trimmed ...
dotnet publish -c Release --self-contained true  -p:PublishSingleFile=false -p:PublishTrimmed=true
goto WAIT

:WAIT
echo.
pause
goto MENU