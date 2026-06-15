@echo off
REM clean.cmd — Remove all build output (out\ plus any stray bin\/obj\ under src\).
setlocal
pushd "%~dp0"
echo [clean] Removing out\ ...
if exist out rd /s /q out
echo [clean] Removing stray bin\ and obj\ under src\ ...
for /d /r src %%d in (bin obj) do if exist "%%d" rd /s /q "%%d"
echo [clean] Done.
popd
exit /b 0
