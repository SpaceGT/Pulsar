@echo off
setlocal enabledelayedexpansion

REM Check if the required parameters are passed
REM (3rd param will be blank if there are not enough)
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

REM Ensure the plugin directory exists
set PLUGIN_DIR=%BIN64%\Plugins
if not exist "PLUGIN_DIR%" (
    echo Creating "Plugins\" folder"
    mkdir "%PLUGIN_DIR%" >NUL 2>&1
)

REM Get the library directory
set DEPENDENCY_DIR=%PLUGIN_DIR%\Libraries
if not exist "%DEPENDENCY_DIR%" (
    echo Creating "Libraries\" folder"
    mkdir "%DEPENDENCY_DIR%" >NUL 2>&1
)

REM Copy loader into main directory
echo Copying "PluginLoader.dll to Bin64\Plugins\loader.dll"

for /l %%i in (1, 1, 10) do (
    copy /y /b "%SOURCE%\PluginLoader.dll" "%PLUGIN_DIR%\loader.dll" >NUL 2>&1

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
echo Could not copy "PluginLoader.dll".
exit /b 1

:BREAK_LOOP
echo Switching to "Bin64\Plugins\Libraries\"

echo Copying "0Harmony.dll"
copy /y /b "%SOURCE%\0Harmony.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

echo Copying "Mono.Cecil.dll"
copy /y /b "%SOURCE%\Mono.Cecil.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

echo Copying "Newtonsoft.Json.dll"
copy /y /b "%SOURCE%\Newtonsoft.Json.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

echo Copying NuGet Packages
copy /y /b "%SOURCE%\NuGet.*.dll" "%DEPENDENCY_DIR%\" >NUL 2>&1

exit /b 0
