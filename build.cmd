@echo off
REM build.cmd — Release build of the full solution. Pass extra args to dotnet build.
setlocal
pushd "%~dp0"
dotnet build NotepadMinus.sln -c Release --nologo %*
set "RC=%ERRORLEVEL%"
popd
exit /b %RC%
