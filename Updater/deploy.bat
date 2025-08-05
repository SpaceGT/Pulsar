@echo off
setlocal enabledelayedexpansion

REM Check if the required parameters are passed
REM (3rd param will be blank if there are not enough)
if "%~3" == "" (
    echo ERROR: Missing required parameters
    exit /b 1
)

REM Extract locations from parameters
for %%F in ("%~1") do set "SOURCE=%%~dpF"
for %%F in ("%~1") do set "UPDATER=%%~nxF"
set PULSAR=%~2
set LICENSE=%~3

REM Remove trailing backslash if applicable
if "%SOURCE:~-1%"=="\" set SOURCE=%SOURCE:~0,-1%
if "%PULSAR:~-1%"=="\" set PULSAR=%PULSAR:~0,-1%
if "%LICENSE:~-1%"=="\" set LICENSE=%LICENSE:~0,-1%

echo Deploy location is "%PULSAR%"

REM Ensure the Pulsar directory exists
if not exist "%PULSAR%" (
    echo Creating "Pulsar\" folder"
    mkdir "%PULSAR%" >NUL 2>&1
)

REM Copy updater into Pulsar directory
echo Copying "%UPDATER%"

for /l %%i in (1, 1, 10) do (
    copy /y /b "%SOURCE%\%UPDATER%" "%PULSAR%\" >NUL 2>&1

    if !ERRORLEVEL! NEQ 0 (
        REM "timeout" requires input redirection which is not supported,
        REM so we use ping as a way to delay the script between retries.
        ping -n 2 127.0.0.1 >NUL 2>&1
    ) else (
        copy /y /b "%SOURCE%\%UPDATER%.config" "%PULSAR%\" >NUL 2>&1
        goto BREAK_LOOP
    )
)

REM This part will only be reached if the loop has been exhausted
REM Any success would skip to the BREAK_LOOP label below
echo Could not copy "%UPDATER%".
exit /b 1

:BREAK_LOOP

REM Copy License to Pulsar directory
echo Copying License
copy /y /b "%LICENSE%" "%PULSAR%\" >NUL 2>&1

exit /b 0
