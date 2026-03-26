$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $root "src"
$out = Join-Path $root "publish"

Write-Host "Building RogCustom..." -ForegroundColor Cyan
dotnet publish "$src\RogCustom.App\RogCustom.App.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained `
    -o $out `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Published to: $out" -ForegroundColor Green
Write-Host "Run: $out\RogCustom.App.exe" -ForegroundColor Yellow
