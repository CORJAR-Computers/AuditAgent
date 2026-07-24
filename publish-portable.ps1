# ============================================================
# AuditAgent - Script de publicacion portable (Single File EXE)
# ============================================================
# Genera un archivo .exe unico y autocontenido.
# No requiere instalacion de .NET Runtime en el PC de destino.
#
# USO: .\publish-portable.ps1 [Release|Debug]
# ============================================================

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProjectPath = "src\AuditAgent.GUI\AuditAgent.GUI.csproj"
$OutputDir = "publish\AuditAgent-Portable"

Write-Host "" -ForegroundColor Cyan
Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "  ║     AuditAgent - Publicacion Portable         ║" -ForegroundColor Cyan
Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Verificar .NET 8 SDK
try {
    $dotnetVersion = & dotnet --version 2>&1
    Write-Host "  [OK] .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] .NET 8 SDK no encontrado. Instalar desde: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Red
    exit 1
}

# Limpiar publicacion anterior
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
    Write-Host "  [OK] Limpieza de publicacion anterior" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "  Compilando en modo $Configuration..." -ForegroundColor White
Write-Host ""

# Publicar como Single File EXE
dotnet publish $ProjectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -o $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "  [ERROR] La compilacion fallo con codigo $LASTEXITCODE" -ForegroundColor Red
    exit 1
}

# Verificar resultado
$exePath = Join-Path $OutputDir "AuditAgent.exe"
if (Test-Path $exePath) {
    $size = (Get-Item $exePath).Length / 1MB
    Write-Host "" -ForegroundColor Green
    Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "  ║     Publicacion exitosa!                      ║" -ForegroundColor Green
    Write-Host "  ╠══════════════════════════════════════════════════╣" -ForegroundColor Green
    Write-Host "  ║  Archivo:  AuditAgent.exe                      ║" -ForegroundColor Green
    Write-Host "  ║  Tamano:   $([math]::Round($size, 1)) MB" -ForegroundColor Green
    Write-Host "  ║  Ruta:     $(Resolve-Path $OutputDir)" -ForegroundColor Green
    Write-Host "  ║  Runtime:  .NET 8 (autocontenido)             ║" -ForegroundColor Green
    Write-Host "  ║  Arch:     x64                                ║" -ForegroundColor Green
    Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Copiar AuditAgent.exe al USB o PC de destino." -ForegroundColor Yellow
    Write-Host "  Ejecutar como Administrador. No requiere .NET instalado." -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host "  [ERROR] No se encontro AuditAgent.exe en la salida" -ForegroundColor Red
    exit 1
}