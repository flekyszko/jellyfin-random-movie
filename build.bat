@echo off
REM Creates a distributable plugin zip under dist\ for manual install into Jellyfin.
REM Files are only created here; nothing is deleted.

setlocal
set ROOT=%~dp0
set PROJ=%ROOT%src\Jellyfin.Plugin.RandomMovie
set OUT=%PROJ%\bin\Release\net9.0
set DIST=%ROOT%dist\RandomMovie_1.0.1.0

echo Building Release...
call dotnet build -c Release "%PROJ%\Jellyfin.Plugin.RandomMovie.csproj"
if errorlevel 1 exit /b 1

if not exist "%DIST%" mkdir "%DIST%"

copy /y "%OUT%\Jellyfin.Plugin.RandomMovie.dll" "%DIST%\"
copy /y "%PROJ%\runtimeconfig.json" "%DIST%\"
copy /y "%ROOT%meta.json" "%DIST%\"

echo Packaging zip...
powershell -NoProfile -Command "Compress-Archive -Path '%DIST%\*' -DestinationPath '%ROOT%dist\RandomMovie-1.0.1.0.zip' -Force"

echo.
echo Done. Zip: %ROOT%dist\RandomMovie-1.0.1.0.zip
echo Install: copy dist\RandomMovie_1.0.1.0 fajlat a jellyfin plugin mappaba, majd inditsd ujra a szervert.
endlocal