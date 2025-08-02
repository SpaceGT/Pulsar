@echo off
setlocal enabledelayedexpansion

REM Check if the required parameters are passed
REM (4th param will be blank if there are not enough)
if "%~2" == "" (
    echo ERROR: Missing required parameters
    exit /b 1
)

REM Extract parameters and remove quotes
set SOURCE=%~1
set BIN64=%~2

REM Remove trailing backslash if applicable
if "%SOURCE:~-1%"=="\" set SOURCE=%SOURCE:~0,-1%
if "%BIN64:~-1%"=="\" set BIN64=%BIN64:~0,-1%

echo Deploy location is "%BIN64%"

REM Ensure the Pulsar directory exists
set PULSAR_DIR=%BIN64%\Pulsar
if not exist "%PULSAR_DIR%" (
    echo Creating "Pulsar\" folder"
    mkdir "%PULSAR_DIR%" >NUL 2>&1
)

echo Switching to "Bin64\Pulsar\"

REM Copy launcher into game directory
echo Copying "Pulsar.exe"

for /l %%i in (1, 1, 10) do (
    copy /y /b "%SOURCE%\Pulsar.exe" "%PULSAR_DIR%\Pulsar.exe" >NUL 2>&1

    if !ERRORLEVEL! NEQ 0 (
        REM "timeout" requires input redirection which is not supported,
        REM so we use ping as a way to delay the script between retries.
        ping -n 2 127.0.0.1 >NUL 2>&1
    ) else (
        copy /y /b "%SOURCE%\Pulsar.exe.config" "%PULSAR_DIR%\Pulsar.exe.config" >NUL 2>&1
        goto BREAK_LOOP
    )
)

REM This part will only be reached if the loop has been exhausted
REM Any success would skip to the BREAK_LOOP label below
echo Could not copy "Pulsar.exe".
exit /b 1

:BREAK_LOOP

REM Get the library directory
set DEPENDENCY_DIR=%PULSAR_DIR%\Libraries
if not exist "%DEPENDENCY_DIR%" (
    echo Creating "Pulsar\Libraries\" folder"
    mkdir "%DEPENDENCY_DIR%" >NUL 2>&1
)

echo Switching to "Bin64\Pulsar\Libraries\"

REM Copy Pulsar dependencies
echo Copying "Pulsar.Legacy.Plugin.dll"
copy /y /b "%SOURCE%\Pulsar.Legacy.Plugin.dll" "%DEPENDENCY_DIR%\Pulsar.Legacy.Plugin.dll" >NUL 2>&1

echo Copying "Pulsar.Shared.dll"
copy /y /b "%SOURCE%\Pulsar.Shared.dll" "%DEPENDENCY_DIR%\Pulsar.Shared.dll" >NUL 2>&1

echo Copying "Pulsar.Compiler.dll"
copy /y /b "%SOURCE%\Pulsar.Compiler.dll" "%DEPENDENCY_DIR%\Pulsar.Compiler.dll" >NUL 2>&1
copy /y /b "%SOURCE%\Pulsar.Compiler.dll.config" "%DEPENDENCY_DIR%\Pulsar.Compiler.dll.config" >NUL 2>&1

REM Copy other dependencies
echo Copying "0Harmony.dll"
copy /y /b "%SOURCE%\0Harmony.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

echo Copying "Mono.Cecil.dll"
copy /y /b "%SOURCE%\Mono.Cecil.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

echo Copying "Newtonsoft.Json.dll"
copy /y /b "%SOURCE%\Newtonsoft.Json.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

echo Copying "NLog.dll"
copy /y /b "%SOURCE%\NLog.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

echo Copying "protobuf-net.dll"
copy /y /b "%SOURCE%\protobuf-net.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

echo Copying "NuGet.*.dll"
copy /y /b "%SOURCE%\NuGet.*.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

exit /b 0
