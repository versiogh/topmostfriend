@echo off
setlocal
cd /d "%~dp0"
where dotnet >nul 2>nul
if errorlevel 1 (
  echo ERROR: dotnet was not found. Install the .NET 8 SDK and run this file again.
  pause
  exit /b 1
)

echo Building TopMostFriend for Windows x64...
dotnet publish .\TopMostFriend\TopMostFriend.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o .\publish\win-x64
if errorlevel 1 (
  echo.
  echo ERROR: The build failed.
  pause
  exit /b 1
)

echo.
echo OK: .\publish\win-x64\TopMostFriend.exe
pause
