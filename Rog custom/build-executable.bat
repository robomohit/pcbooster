@echo off
echo Building RogCustom executables...

echo.
echo Publishing ConsolePoC...
cd /d "%~dp0src\RogCustom.ConsolePoC"
dotnet publish -c Release -o publish --self-contained true -r win-x64 -p:PublishSingleFile=true

echo.
echo Publishing WPF App...
cd /d "%~dp0src\RogCustom.App"
dotnet publish -c Release -o publish --self-contained true -r win-x64 -p:PublishSingleFile=true

echo.
echo Build complete!
echo ConsolePoC: %~dp0src\RogCustom.ConsolePoC\publish\RogCustom.ConsolePoC.exe
echo WPF App: %~dp0src\RogCustom.App\publish\RogCustom.App.exe
echo.
pause
