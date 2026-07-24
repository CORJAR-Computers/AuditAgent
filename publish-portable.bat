@echo off
chcp 65001 >nul

REM ============================================================
REM AuditAgent - Publicacion Portable (para PCs sin .NET SDK)
REM ============================================================

echo.
echo   ============================================
echo   AuditAgent - Compilacion Portable
echo   ============================================
echo.

where dotnet >nul 2>&1
if %errorlevel% neq 0 (
    echo   [ERROR] .NET 8 SDK no encontrado.
    echo   Descarguelo de: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

if not exist "publish" mkdir publish
if exist "publish\AuditAgent-Portable" rmdir /s /q "publish\AuditAgent-Portable"

echo   Compilando...
echo.

dotnet publish src\AuditAgent.GUI\AuditAgent.GUI.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:PublishTrimmed=false ^
    -o publish\AuditAgent-Portable

if %errorlevel% neq 0 (
    echo.
    echo   [ERROR] La compilacion fallo.
    pause
    exit /b 1
)

echo.
echo   ============================================
echo   Compilacion exitosa!
echo   Archivo: publish\AuditAgent-Portable\AuditAgent.exe
echo   ============================================
echo.
echo   Copie AuditAgent.exe al USB o PC de destino.
echo   Ejecute como Administrador.
echo.
pause