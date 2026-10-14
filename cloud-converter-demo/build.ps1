# CloudConverter 시연 버전 빌드 (Windows, .NET 8 SDK 필요)
# 결과: dist\CloudConverterDemo\CloudConverter.exe (+ config 폴더)  /  dist\CloudConverterDemo.zip
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "dist\CloudConverterDemo"

dotnet test (Join-Path $root "tests\CloudConverter.Core.Tests")
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src\CloudConverter.App") -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -o $out
Copy-Item (Join-Path $root "README.md") $out
Compress-Archive -Path $out -DestinationPath (Join-Path $root "dist\CloudConverterDemo.zip") -Force
Write-Host "완료: $out"
