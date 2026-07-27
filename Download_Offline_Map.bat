@echo off
setlocal EnableExtensions
set "FINAL_STATUS=FAILED"
set "FINAL_MESSAGE=The operation did not complete."
set "EXIT_CODE=1"

set "MAP_URL=%~1"
if not defined MAP_URL set "MAP_URL=https://github.com/roshannn/Streaming_Map_Demo/releases/download/offline-map-v1/offline-map.parts.json"
set "MAP_TARGET=%~2"
if not defined MAP_TARGET set "MAP_TARGET=%~dp0projects\com.saab.map-streamer\OfflineMaps\stock"

for %%I in ("%MAP_TARGET%") do (
    set "MAP_TARGET=%%~fI"
    set "MAP_PARENT=%%~dpI"
    set "MAP_NAME=%%~nxI"
)

set "MAP_ZIP=%MAP_PARENT%%MAP_NAME%.download.zip"
set "MAP_EXTRACT=%MAP_PARENT%%MAP_NAME%.download"
set "MAP_PARTS=%MAP_ZIP%.parts"

echo.
echo Install location: %MAP_TARGET%
echo.

echo Checking for an existing offline map...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Test-OfflineMapInstallation.ps1" -ManifestUrl "%MAP_URL%" -Target "%MAP_TARGET%"
set "MAP_CHECK_RESULT=%ERRORLEVEL%"
if "%MAP_CHECK_RESULT%"=="0" (
    echo.
    echo Offline map is already installed and matches the current release.
    echo Nothing needs to be downloaded.
    set "FINAL_STATUS=UP TO DATE"
    set "FINAL_MESSAGE=Existing files were verified. No download was needed."
    set "EXIT_CODE=0"
    goto :finish
)
if not "%MAP_CHECK_RESULT%"=="10" (
    echo ERROR: Could not determine whether the offline map is current.
    set "FINAL_MESSAGE=The existing installation could not be checked."
    set "EXIT_CODE=%MAP_CHECK_RESULT%"
    goto :finish
)

echo.
echo Offline map is missing, incomplete, or different from the current release.

if exist "%MAP_EXTRACT%" (
    set "RECOVERED_EXTRACTION=1"
    echo A previous incomplete extraction was found:
    echo   %MAP_EXTRACT%
    echo Removing it so extraction can restart without downloading again...
    rmdir /s /q "%MAP_EXTRACT%"
    if exist "%MAP_EXTRACT%" (
        echo ERROR: The incomplete extraction folder could not be removed.
        set "FINAL_MESSAGE=An incomplete extraction folder could not be removed."
        goto :finish
    )
)

if not exist "%MAP_PARENT%" mkdir "%MAP_PARENT%"

if exist "%MAP_ZIP%" set "HAD_EXISTING_ARCHIVE=1"
if exist "%MAP_PARTS%\" set "HAD_EXISTING_PARTS=1"

echo Downloading package...
echo An interrupted download can be resumed by running this command again.
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Download-OfflineMapPackage.ps1" -Url "%MAP_URL%" -Destination "%MAP_ZIP%"
if errorlevel 1 (
    echo ERROR: Download failed. The partial ZIP was retained for resuming.
    set "FINAL_MESSAGE=Download failed. Partial files were retained for resuming."
    goto :finish
)

echo Extracting package...
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ^
  "$ErrorActionPreference='Stop'; Expand-Archive -LiteralPath $env:MAP_ZIP -DestinationPath $env:MAP_EXTRACT -Force"
if errorlevel 1 (
    echo ERROR: ZIP extraction failed.
    set "FINAL_MESSAGE=The downloaded ZIP could not be extracted."
    goto :finish
)

set "MAP_PAYLOAD=%MAP_EXTRACT%"
if not exist "%MAP_PAYLOAD%\map.gzd" (
    if exist "%MAP_EXTRACT%\stock\map.gzd" set "MAP_PAYLOAD=%MAP_EXTRACT%\stock"
)

if not exist "%MAP_PAYLOAD%\map.gzd" (
    echo ERROR: The ZIP does not contain map.gzd at its root or under stock\.
    echo Extracted files were retained at:
    echo   %MAP_EXTRACT%
    set "FINAL_MESSAGE=The downloaded package did not contain map.gzd."
    goto :finish
)
if not exist "%MAP_PAYLOAD%\nodes\" (
    echo ERROR: The ZIP does not contain the required nodes folder.
    echo Extracted files were retained at:
    echo   %MAP_EXTRACT%
    set "FINAL_MESSAGE=The downloaded package did not contain the nodes folder."
    goto :finish
)

if exist "%MAP_TARGET%\" (
    echo.
    echo An offline map already exists at:
    echo   %MAP_TARGET%
    choice /C YN /N /M "Replace it? The old folder will be retained as a backup. [Y/N] "
    if errorlevel 2 (
        echo Installation cancelled. The downloaded files were retained.
        set "FINAL_STATUS=CANCELLED"
        set "FINAL_MESSAGE=Replacement was cancelled. Downloaded files were retained."
        set "EXIT_CODE=2"
        goto :finish
    )

    call :backupExisting
    if errorlevel 1 (
        set "FINAL_MESSAGE=The existing map could not be moved to a backup."
        goto :finish
    )
)

move "%MAP_PAYLOAD%" "%MAP_TARGET%" >nul
if errorlevel 1 (
    echo ERROR: Could not install the extracted map.
    set "FINAL_MESSAGE=The extracted map could not be moved into place."
    goto :finish
)

if exist "%MAP_EXTRACT%\" rmdir /s /q "%MAP_EXTRACT%"
del /q "%MAP_ZIP%" >nul 2>&1

echo.
echo Offline map installed successfully:
echo   %MAP_TARGET%
set "FINAL_STATUS=INSTALLED"
set "FINAL_MESSAGE=The current offline map was assembled, verified, and installed."
set "EXIT_CODE=0"
goto :finish

:backupExisting
for /f %%I in ('powershell.exe -NoLogo -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss"') do set "MAP_STAMP=%%I"
set "MAP_BACKUP=%MAP_TARGET%.backup-%MAP_STAMP%"
move "%MAP_TARGET%" "%MAP_BACKUP%" >nul
if errorlevel 1 (
    echo ERROR: Could not move the existing map to a backup folder.
    exit /b 1
)
echo Previous map retained at:
echo   %MAP_BACKUP%
exit /b 0

:finish
echo.
echo ============================================================
echo OFFLINE MAP INSTALLER SUMMARY
echo ============================================================
echo Status:   %FINAL_STATUS%
echo Result:   %FINAL_MESSAGE%
echo Location: %MAP_TARGET%
if defined MAP_BACKUP echo Backup:   %MAP_BACKUP%
if defined HAD_EXISTING_PARTS echo Resume:   Existing partial or completed parts were reused where valid.
if defined HAD_EXISTING_ARCHIVE echo Cache:    An existing assembled archive was checked before downloading.
if defined RECOVERED_EXTRACTION echo Recovery: Incomplete extraction output was removed and restarted.
if exist "%MAP_ZIP%" echo Resume:   %MAP_ZIP%
echo ============================================================
echo.
echo Press any key to exit...
pause >nul
endlocal & exit /b %EXIT_CODE%
