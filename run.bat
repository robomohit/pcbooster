@echo off
setlocal

:: Path to the self-contained executable (adjust if you change output folder)
set EXE_PATH=src\RogCustom.App\bin\Debug\net8.0-windows\win-x64\RogCustom.App.exe

if not exist "%EXE_PATH%" (
    echo RogCustom.App.exe not found at expected location:
    echo %EXE_PATH%
    echo.
    echo Make sure you have built the project first.
    pause
    exit /b 1
)

echo Starting RogCustom...
start "" "%EXE_PATH%"
exit /b 0
