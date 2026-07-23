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
for %%F in ("%~1") do set "COMPILER=%%~nxF"
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

REM Get the library directory
set SHARED_DIR=%PULSAR%\Libraries
if not exist "%SHARED_DIR%" (
    echo Creating "Pulsar\Libraries\"
    mkdir "%SHARED_DIR%" >NUL 2>&1
)

REM Get the compiler directory
set COMPILER_DIR=%SHARED_DIR%\Compiler
if exist "%COMPILER_DIR%" (
    echo Clearing "Pulsar\Libraries\Compiler"

    for /l %%i in (1, 1, 10) do (
        rmdir /s /q "%COMPILER_DIR%"

        if !ERRORLEVEL! NEQ 0 (
            REM "timeout" requires input redirection which is not supported,
            REM so we use ping as a way to delay the script between retries.
            ping -n 2 127.0.0.1 >NUL 2>&1
        ) else (
            goto BREAK_LOOP
        )
    )

    REM This part will only be reached if the loop has been exhausted
    REM Any success would skip to the BREAK_LOOP label below
    echo Could not copy "%COMPILER%".
    exit /b 1
) else (
    echo Creating "Pulsar\Libraries\Compiler"
)

:BREAK_LOOP
mkdir "%COMPILER_DIR%" >NUL 2>&1
echo Switching to "Pulsar\Libraries\Compiler"

REM Copy compiler into compiler directory
echo Copying "%COMPILER%"
copy /y /b "%SOURCE%\%COMPILER%" "%COMPILER_DIR%\" >NUL 2>&1
copy /y /b "%SOURCE%\%COMPILER%.config" "%COMPILER_DIR%\" >NUL 2>&1

REM Copy License to Pulsar directory
echo Copying License
copy /y /b "%LICENSE%" "%PULSAR%\" >NUL 2>&1

REM Copy compiler dependencies
echo Copying "Pulsar.Protocol.dll"
copy /y /b "%SOURCE%\Pulsar.Protocol.dll" "%COMPILER_DIR%\" >NUL 2>&1

echo Copying "Newtonsoft.Json.dll"
copy /y /b "%SOURCE%\Newtonsoft.Json.dll" "%COMPILER_DIR%\" >NUL 2>&1

echo Copying "Mono.Cecil.dll"
copy /y /b "%SOURCE%\Mono.Cecil.dll" "%COMPILER_DIR%\" >NUL 2>&1

echo Copying "NLog.dll"
copy /y /b "%SOURCE%\NLog.dll" "%COMPILER_DIR%\" >NUL 2>&1

echo Copying "Microsoft.CodeAnalysis.*.dll"
copy /y /b "%SOURCE%\Microsoft.CodeAnalysis.dll" "%COMPILER_DIR%\" >NUL 2>&1
copy /y /b "%SOURCE%\Microsoft.CodeAnalysis.CSharp.dll" "%COMPILER_DIR%\" >NUL 2>&1

echo Copying "System.*.dll"
copy /y /b "%SOURCE%\System.Collections.Immutable.dll" "%COMPILER_DIR%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.Memory.dll" "%COMPILER_DIR%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.Runtime.CompilerServices.Unsafe.dll" "%COMPILER_DIR%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.Reflection.Metadata.dll" "%COMPILER_DIR%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.Numerics.Vectors.dll" "%COMPILER_DIR%\" >NUL 2>&1

exit /b 0
