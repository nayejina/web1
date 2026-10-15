# CloudConverter 빌드 (Windows, .NET 8 SDK 필요)
#   .\build.ps1            시연판 + MIP 버전 모두
#   .\build.ps1 -Demo      시연판만   -> dist\CloudConverterDemo\CloudConverter.exe
#   .\build.ps1 -Mip       MIP 버전만 -> dist\CloudConverterMip\CloudConverterMip.exe
param([switch]$Demo, [switch]$Mip)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
if (-not $Demo -and -not $Mip) { $Demo = $true; $Mip = $true }
$app = Join-Path $root "src\CloudConverter.App"

dotnet test (Join-Path $root "tests\CloudConverter.Core.Tests")

if ($Demo) {
    $out = Join-Path $root "dist\CloudConverterDemo"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish $app -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
        -p:DebugType=none -o $out
    Copy-Item (Join-Path $root "README.md") $out
    Compress-Archive -Path $out -DestinationPath (Join-Path $root "dist\CloudConverterDemo.zip") -Force
}

if ($Mip) {
    # MIP SDK 네이티브 DLL(x64 폴더)을 함께 배포해야 하므로 단일 파일이 아닌 폴더로 게시
    $out = Join-Path $root "dist\CloudConverterMip"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish $app -c Release -r win-x64 --self-contained true -p:Edition=Mip -p:DebugType=none -o $out
    Copy-Item (Join-Path $root "README-MIP.md") $out
    Compress-Archive -Path $out -DestinationPath (Join-Path $root "dist\CloudConverterMip.zip") -Force
}
Write-Host "완료: $(Join-Path $root 'dist')"
