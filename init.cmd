@echo off
REM init.cmd — first-time setup: verify .NET 8 SDK and restore NuGet packages.
setlocal
pushd "%~dp0"

echo [init] Checking .NET SDKs...
dotnet --list-sdks | findstr /b "8." >nul
if errorlevel 1 (
  echo [init] ERROR: .NET 8 SDK not found. Install from https://dotnet.microsoft.com/download/dotnet/8.0
  popd & exit /b 1
)

echo [init] Restoring NuGet packages...
dotnet restore NotepadMinus.sln
set "RC=%ERRORLEVEL%"

popd
exit /b %RC%
