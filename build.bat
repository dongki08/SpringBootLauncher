@echo off
chcp 65001 >nul
echo ====================================
echo   Spring Boot Launcher Build Script
echo ====================================
echo.

REM 빌드 출력 폴더 정리
echo [1/4] 이전 빌드 정리 중...
if exist "server" rmdir /s /q "server"
mkdir "server"
mkdir "server\Installer"
mkdir "server\Launcher"

REM SpringBootLauncher 빌드 (단일 exe)
echo.
echo [2/4] SpringBootLauncher 빌드 중...
dotnet publish SpringBootLauncher\SpringBootLauncher.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:IncludeAllContentForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o server\Launcher

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: SpringBootLauncher 빌드 실패!
    pause
    exit /b 1
)

REM SpringBootInstaller의 app 폴더에 Launcher 복사
echo.
echo [3/4] Launcher를 Installer의 app 폴더로 복사 중...
if not exist "SpringBootInstaller\app" mkdir "SpringBootInstaller\app"
xcopy /Y /I "server\Launcher\SpringBootLauncher.exe" "SpringBootInstaller\app\"

REM SpringBootInstaller 빌드 (단일 exe + installers 폴더)
echo.
echo [4/4] SpringBootInstaller 빌드 중...
dotnet publish SpringBootInstaller\SpringBootInstaller.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:IncludeAllContentForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o server\Installer

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: SpringBootInstaller 빌드 실패!
    pause
    exit /b 1
)

echo.
echo ====================================
echo   빌드 완료!
echo ====================================
echo.
echo 출력 위치:
echo   - Installer: server\Installer\SpringBootInstaller.exe
echo   - Launcher:  server\Launcher\SpringBootLauncher.exe
echo.
echo Installer에 포함된 파일:
echo   - SpringBootInstaller.exe (단일 파일)
echo   - installers\openjdk-17_windows-x64_bin.zip
echo   - installers\SQLServer2022-DEV-x64-KOR.box
echo   - installers\SQLServer2022-DEV-x64-KOR.exe
echo   - app\SpringBootLauncher.exe
echo.

pause
