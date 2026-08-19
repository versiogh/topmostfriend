$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet was not found. Install the .NET 8 SDK.'
}

$runtimes = @('win-x64', 'win-x86')
foreach ($runtime in $runtimes) {
    Write-Host "Publishing $runtime..."
    dotnet publish .\TopMostFriend\TopMostFriend.csproj `
        -c Release `
        -r $runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o ".\publish\$runtime"

    if ($LASTEXITCODE -ne 0) {
        throw "The build for $runtime failed."
    }
}

Write-Host ''
Write-Host 'Build finished:'
Write-Host '  publish\win-x64\TopMostFriend.exe'
Write-Host '  publish\win-x86\TopMostFriend.exe'
