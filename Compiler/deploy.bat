@echo off
setlocal

if "%~2" == "" (
    echo ERROR: Missing required parameters
    exit /b 1
)

set "SOURCE=%~1"
set "LIBRARIES=%~2\Libraries"
set "COMPILER=%LIBRARIES%\Compiler"
set "STAGE=%LIBRARIES%\Compiler.%RANDOM%.new"
set "BACKUP=%LIBRARIES%\Compiler.%RANDOM%.old"

if not exist "%LIBRARIES%" (
    mkdir "%LIBRARIES%"
    if ERRORLEVEL 1 exit /b 1
)
mkdir "%STAGE%"
if ERRORLEVEL 1 exit /b 1

echo Copying compiler to "%STAGE%"
xcopy /e /i /y /q "%SOURCE%*" "%STAGE%\" >NUL
if ERRORLEVEL 1 goto ERROR

if exist "%COMPILER%" (
    move /y "%COMPILER%" "%BACKUP%" >NUL
    if ERRORLEVEL 1 goto ERROR
)
move /y "%STAGE%" "%COMPILER%" >NUL
if ERRORLEVEL 1 goto RESTORE
if exist "%BACKUP%" rmdir /s /q "%BACKUP%"

exit /b 0

:RESTORE
if exist "%BACKUP%" move /y "%BACKUP%" "%COMPILER%" >NUL

:ERROR
if exist "%STAGE%" rmdir /s /q "%STAGE%"
exit /b 1
