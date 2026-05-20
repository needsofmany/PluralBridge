@echo off
setlocal
cd /d "%~dp0"
echo PluralBridge guided export launcher
echo.
echo This will run the guided local export helper.
echo.
py -3 --version >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    set "PB_PY=py -3"
    goto run_export
)
python --version >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    set "PB_PY=python"
    goto run_export
)
python3 --version >nul 2>nul
if %ERRORLEVEL% EQU 0 (
    set "PB_PY=python3"
    goto run_export
)
echo Python was not found.
echo.
echo Install Python 3, then run this launcher again.
echo Recommended source: https://www.python.org/downloads/windows/
echo.
echo Press Enter to close this window.
pause >nul
exit /b 1
:run_export
echo Python command found: %PB_PY%
echo.
set "PYTHONUNBUFFERED=1"
%PB_PY% "scripts\python\export_regular_user.py" %*
set "PB_RESULT=%ERRORLEVEL%"
echo.
if "%PB_RESULT%"=="0" (
    echo PluralBridge guided export finished.
) else (
    echo PluralBridge guided export stopped with an error.
)
echo.
echo Press Enter to close this window.
pause >nul
exit /b %PB_RESULT%
