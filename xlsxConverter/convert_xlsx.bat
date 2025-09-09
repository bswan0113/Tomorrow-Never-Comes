@echo off
setlocal

set "POWERSHELL_SCRIPT_NAME=convert_dialogue_xlsx.ps1"
set "POWERSHELL_SCRIPT_PATH=%~dp0%POWERSHELL_SCRIPT_NAME%"

if "%~3"=="" (
    goto :interactive_menu
) else (
    set "MODE=%~1"
    set "INPUT_FILE=%~2"
    set "OUTPUT_FILE=%~3"
    goto :execute_powershell
)

:interactive_menu
cls
echo ===================================================
echo      General Complex CSV <-> XLSX Converter
echo ===================================================
echo.
echo Please choose a conversion direction:
echo.
echo   1. CSV (Ugly Format)      ->  XLSX (Human-Readable)
echo.
echo   2. XLSX (Human-Readable)  ->  CSV (Ugly Format)
echo.

:get_choice
set "CHOICE="
set /p CHOICE="Enter your choice (1 or 2): "
if "%CHOICE%"=="1" goto :setup_csv_to_xlsx
if "%CHOICE%"=="2" goto :setup_xlsx_to_csv
echo Invalid choice. Please press 1 or 2.
goto :get_choice

:setup_csv_to_xlsx
set "MODE=to-xlsx"
echo.
set /p INPUT_FILE="Enter the name of the source CSV file (e.g., data.csv): "
set /p OUTPUT_FILE="Enter the name for the new XLSX file (e.g., data_edited.xlsx): "
goto :execute_powershell

:setup_xlsx_to_csv
rem ▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼▼
rem --- 여기가 수정된 부분입니다 ---
set "MODE=to-csv"
echo.
set /p INPUT_FILE="Enter the name of the source XLSX file (e.g., data_edited.xlsx): "
set /p OUTPUT_FILE="Enter the name for the new CSV file (e.g., data_final.csv): "
rem ▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲▲
goto :execute_powershell

:execute_powershell
echo.
echo ---------------------------------------------------
echo Executing conversion...
echo.
echo   Mode: %MODE%
echo   Input: %INPUT_FILE%
echo   Output: %OUTPUT_FILE%
echo ---------------------------------------------------
echo.
powershell.exe -ExecutionPolicy Bypass -File "%POWERSHELL_SCRIPT_PATH%" -Mode "%MODE%" -InputPath "%INPUT_FILE%" -OutputPath "%OUTPUT_FILE%"
echo.
echo.
echo Conversion process finished.
echo Press any key to exit.
pause > nul
exit /b