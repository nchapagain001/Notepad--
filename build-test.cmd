@echo off
REM build-test.cmd — Release build of the full solution, then run all tests.
REM Bails out on the first failure.
setlocal
pushd "%~dp0"

dotnet build NotepadMinus.sln -c Release --nologo
if errorlevel 1 ( set "RC=%ERRORLEVEL%" & popd & exit /b %RC% )

dotnet test NotepadMinus.sln -c Release --no-build --nologo
set "RC=%ERRORLEVEL%"

popd
exit /b %RC%
