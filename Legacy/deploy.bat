@echo off
setlocal enabledelayedexpansion

REM Check if the required parameters are passed
REM (3rd param will be blank if there are not enough)
if "%~4" == "" (
    echo ERROR: Missing required parameters
    exit /b 1
)

REM Extract locations from parameters
for %%F in ("%~1") do set "SOURCE=%%~dpF"
for %%F in ("%~1") do set "LAUNCHER=%%~nxF"
for %%F in ("%~1") do set "NAME=%%~nF"

set PULSAR=%~2
set LICENSE=%~3
set FRAMEWORK=%~4

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

REM Copy launcher into Pulsar directory
echo Copying "%LAUNCHER%"

for /l %%i in (1, 1, 10) do (
    copy /y /b "%SOURCE%\%NAME%.exe" "%PULSAR%\" >NUL 2>&1

    if !ERRORLEVEL! NEQ 0 (
        REM "timeout" requires input redirection which is not supported,
        REM so we use ping as a way to delay the script between retries.
        ping -n 2 127.0.0.1 >NUL 2>&1
    ) else (
        if "%FRAMEWORK%"==".NETCoreApp" (
            copy /y /b "%SOURCE%\%NAME%.dll" "%PULSAR%\" >NUL 2>&1
            copy /y /b "%SOURCE%\%NAME%.runtimeconfig.json" "%PULSAR%\" >NUL 2>&1
            copy /y /b "%SOURCE%\%NAME%.deps.json" "%PULSAR%\" >NUL 2>&1
        ) else (
            copy /y /b "%SOURCE%\%NAME%.exe.config" "%PULSAR%\" >NUL 2>&1
        )

        goto BREAK_LOOP
    )
)

REM This part will only be reached if the loop has been exhausted
REM Any success would skip to the BREAK_LOOP label below
echo Could not copy "%LAUNCHER%".
exit /b 1

:BREAK_LOOP

REM Copy License to Pulsar directory
echo Copying License
copy /y /b "%LICENSE%" "%PULSAR%\" >NUL 2>&1

REM Get the library directory
set SHARED_DIR=%PULSAR%\Libraries
if not exist "%SHARED_DIR%" (
    echo Creating "Pulsar\Libraries\"
    mkdir "%SHARED_DIR%" >NUL 2>&1
)
set LIBRARY_DIR=%SHARED_DIR%\%NAME%
if exist "%LIBRARY_DIR%" (
    echo Clearing "Pulsar\Libraries\%NAME%"
    rmdir /s /q "%LIBRARY_DIR%"
) else (
    echo Creating "Pulsar\Libraries\%NAME%"
)
mkdir "%LIBRARY_DIR%" >NUL 2>&1
echo Switching to "Pulsar\Libraries\%NAME%"

REM Copy Pulsar dependencies
echo Copying "Pulsar.Shared.dll"
copy /y /b "%SOURCE%\Pulsar.Shared.dll" "%LIBRARY_DIR%\" >NUL 2>&1

echo Copying "Pulsar.Compiler.dll"
copy /y /b "%SOURCE%\Pulsar.Compiler.dll" "%LIBRARY_DIR%\" >NUL 2>&1
if "%FRAMEWORK%"==".NETFramework" (
    copy /y /b "%SOURCE%\Pulsar.Compiler.dll.config" "%LIBRARY_DIR%\" >NUL 2>&1
)

REM Copy other dependencies
echo Copying "0Harmony.dll"
copy /y /b "%SOURCE%\0Harmony.dll" "%LIBRARY_DIR%\" >NUL 2>&1

echo Copying "Mono.Cecil.dll"
copy /y /b "%SOURCE%\Mono.Cecil.dll" "%LIBRARY_DIR%\" >NUL 2>&1

echo Copying "Newtonsoft.Json.dll"
copy /y /b "%SOURCE%\Newtonsoft.Json.dll" "%LIBRARY_DIR%\" >NUL 2>&1

echo Copying "Gameloop.Vdf.dll"
copy /y /b "%SOURCE%\Gameloop.Vdf.dll" "%LIBRARY_DIR%\" >NUL 2>&1

echo Copying "NLog.dll"
copy /y /b "%SOURCE%\NLog.dll" "%LIBRARY_DIR%\" >NUL 2>&1

echo Copying "protobuf-net.dll"
copy /y /b "%SOURCE%\protobuf-net.dll" "%LIBRARY_DIR%\" >NUL 2>&1
copy /y /b "%SOURCE%\protobuf-net.Core.dll" "%LIBRARY_DIR%\" >NUL 2>&1

echo Copying "FuzzySharp.dll"
copy /y /b "%SOURCE%\FuzzySharp.dll" "%LIBRARY_DIR%\" >NUL 2>&1

echo Copying "NuGet.*.dll"
copy /y /b "%SOURCE%\NuGet.*.dll" "%LIBRARY_DIR%\" >NUL 2>&1

REM Get the compiler directory
set COMPILER_DIR=%LIBRARY_DIR%\Compiler
if not exist "%COMPILER_DIR%" (
    echo Creating "Pulsar\Libraries\%NAME%\Compiler"
    mkdir "%COMPILER_DIR%" >NUL 2>&1
)
echo Switching to "Pulsar\Libraries\%NAME%\Compiler"

REM Copy compiler dependencies
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
