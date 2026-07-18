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

REM Copy License to Pulsar directory
echo Copying License
copy /y /b "%LICENSE%" "%PULSAR%\" >NUL 2>&1

REM Get the library directory
set SHARED_DIR=%PULSAR%\Libraries
if not exist "%SHARED_DIR%" (
    echo Creating "Pulsar\Libraries\"
    mkdir "%SHARED_DIR%" >NUL 2>&1
)
set INTERFACE=%SHARED_DIR%\Interface
if exist "%INTERFACE%" (
    echo Clearing "Pulsar\Libraries\Interface"
    rmdir /s /q "%INTERFACE%"
) else (
    echo Creating "Pulsar\Libraries\Interface"
)
mkdir "%INTERFACE%" >NUL 2>&1
echo Switching to "Pulsar\Libraries\Interface"

REM Copy launcher into Pulsar directory
echo Copying "%LAUNCHER%"

for /l %%i in (1, 1, 10) do (
    copy /y /b "%SOURCE%\%NAME%.exe" "%INTERFACE%\" >NUL 2>&1

    if !ERRORLEVEL! NEQ 0 (
        REM "timeout" requires input redirection which is not supported,
        REM so we use ping as a way to delay the script between retries.
        ping -n 2 127.0.0.1 >NUL 2>&1
    ) else (
        if "%FRAMEWORK%"==".NETCoreApp" (
            copy /y /b "%SOURCE%\%NAME%.dll" "%INTERFACE%\" >NUL 2>&1
            copy /y /b "%SOURCE%\%NAME%.runtimeconfig.json" "%INTERFACE%\" >NUL 2>&1
            copy /y /b "%SOURCE%\%NAME%.deps.json" "%INTERFACE%\" >NUL 2>&1
        ) else (
            copy /y /b "%SOURCE%\%NAME%.exe.config" "%INTERFACE%\" >NUL 2>&1
        )

        goto BREAK_LOOP
    )
)

REM This part will only be reached if the loop has been exhausted
REM Any success would skip to the BREAK_LOOP label below
echo Could not copy "%LAUNCHER%".
exit /b 1

:BREAK_LOOP

REM Copy other dependencies
echo Copying "MicroCom.Runtime.dll"
copy /y /b "%SOURCE%\MicroCom.Runtime.dll" "%INTERFACE%\" >NUL 2>&1

echo Copying "HarfBuzzSharp.dll"
copy /y /b "%SOURCE%\HarfBuzzSharp.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\libHarfBuzzSharp.dll" "%INTERFACE%\" >NUL 2>&1

echo Copying "SkiaSharp.dll"
copy /y /b "%SOURCE%\SkiaSharp.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\libSkiaSharp.dll" "%INTERFACE%\" >NUL 2>&1

echo Copying "av_libglesv2.dll"
copy /y /b "%SOURCE%\av_libglesv2.dll" "%INTERFACE%\" >NUL 2>&1

echo Copying "MicroCom.Runtime.dll"
copy /y /b "%SOURCE%\MicroCom.Runtime.dll" "%INTERFACE%\" >NUL 2>&1

echo Copying "Microsoft.Bcl.AsyncInterfaces.dll"
copy /y /b "%SOURCE%\Microsoft.Bcl.AsyncInterfaces.dll" "%INTERFACE%\" >NUL 2>&1

echo Copying "AnimatedImage.*.dll"
copy /y /b "%SOURCE%\AnimatedImage.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\AnimatedImage.Avalonia.dll" "%INTERFACE%\" >NUL 2>&1

echo Copying "Avalonia.*.dll"
copy /y /b "%SOURCE%\Avalonia.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Base.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Controls.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Dialogs.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Markup.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Markup.Xaml.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.MicroCom.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.OpenGL.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Remote.Protocol.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Skia.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Themes.Fluent.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Win32.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\Avalonia.Metal.dll" "%INTERFACE%\" >NUL 2>&1


echo Copying "System.*.dll"
copy /y /b "%SOURCE%\System.Buffers.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.Memory.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.ValueTuple.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.ComponentModel.Annotations.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.Numerics.Vectors.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.Runtime.CompilerServices.Unsafe.dll" "%INTERFACE%\" >NUL 2>&1
copy /y /b "%SOURCE%\System.Threading.Tasks.Extensions.dll" "%INTERFACE%\" >NUL 2>&1

exit /b 0
